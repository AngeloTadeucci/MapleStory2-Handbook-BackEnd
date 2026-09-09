using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NifToGltf.Native;

internal sealed class GltfWriter(NifDocument document, string? textureRoot, TextureCatalog? catalog = null) {
    private readonly JsonArray nodes = [], meshes = [], skins = [], accessors = [], views = [], materials = [], images = [], textures = [];
    private readonly MemoryStream buffer = new();
    private readonly Dictionary<int, int> nodeMap = [];
    private readonly Dictionary<string, int> textureMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> parents = [];
    private readonly List<object> primitiveReports = [];
    private readonly List<string> hiddenMeshes = [];
    private readonly TextureCatalog textureCatalog = catalog ?? new(textureRoot);
    private readonly List<TexturePixels> imagePixels = [];
    private readonly JsonArray samplers = [];
    private readonly Dictionary<ushort, int> samplerMap = [];
    private readonly Dictionary<int, List<TextureTransform>> uvTransforms = [];
    private int nextUv;
    public static JsonNode Json<T>(T value) => JsonSerializer.SerializeToNode(value)!;
    public static float[] MatrixValues(Matrix4x4 m) => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    public object Write(string output, IReadOnlyList<AnimationClip>? clips = null, bool overwrite = false) {
        document.OmitParticles();
        HashSet<int> reachable = [];
        void Visit(int id) {
            if (id < 0 || !reachable.Add(id)) return;
            if (document.Nodes.TryGetValue(id, out NifNode? node)) foreach (int child in node.Children) Visit(child);
        }
        foreach (int root in document.Roots) Visit(root);
        foreach (NifNode node in document.Nodes.Values) {
            nodeMap[node.Id] = nodes.Count;
            if (!Matrix4x4.Decompose(node.Transform, out Vector3 scale, out Quaternion rotation, out Vector3 translation)) {
                throw new InvalidDataException($"Cannot decompose {node.Name} transform.");
            }
            nodes.Add(Json(new { name = node.Name, translation = new[] { translation.X, translation.Y, translation.Z },
                rotation = new[] { rotation.X, rotation.Y, rotation.Z, rotation.W }, scale = new[] { scale.X, scale.Y, scale.Z } }));
            if (node.SortingMode is { } sorting) nodes[^1]!["extras"] = Json(new { nifSortingMode = sorting });
            if (document.EquipmentBones.Contains(node.Id)) {
                nodes[^1]!["extras"] ??= new JsonObject();
                nodes[^1]!["extras"]!["equipmentBone"] = true;
            }
            foreach (int child in node.Children) {
                if (!parents.TryAdd(child, node.Id)) throw new InvalidDataException($"Multiple parents for block {child}.");
            }
        }
        foreach (NifNode node in document.Nodes.Values) {
            if (!reachable.Contains(node.Id)) continue;
            HashSet<int> ancestry = [node.Id];
            int current = node.Id;
            while (parents.TryGetValue(current, out current)) {
                if (!ancestry.Add(current)) throw new InvalidDataException("Cycle in node hierarchy.");
            }
            if (node.Children.Length > 0) nodes[nodeMap[node.Id]]!["children"] = Json(node.Children.Select(NodeIndex));
            if (node.Mesh is null) continue;
            bool hidden = (node.Flags & 1) != 0;
            int ancestor = node.Id;
            while (parents.TryGetValue(ancestor, out ancestor)) hidden |= (document.Nodes[ancestor].Flags & 1) != 0;
            if (hidden) { hiddenMeshes.Add(node.Name); continue; }
            int effectAncestor = node.Id;
            do {
                foreach (int effect in document.Nodes[effectAncestor].Effects.Where(id => id >= 0)) {
                    throw new NotSupportedException($"Visible mesh {node.Name} uses scene effect block {effect} ({document.Blocks[effect].Type}); appearance requires an explicit material implementation.");
                }
            } while (parents.TryGetValue(effectAncestor, out effectAncestor));
            JsonArray primitives = [];
            int material = Material(node);
            int? skinIndex = null;
            double[]? morphWeights = null;
            for (int submesh = 0; submesh < node.Mesh.Submeshes; submesh++) {
                DecodedPrimitive decoded;
                try { decoded = MeshDecoder.Decode(document, node, submesh); }
                catch (Exception e) when (e is InvalidDataException or NotSupportedException) {
                    throw new InvalidDataException($"{document.Path}: block {node.Id} ({node.Name}), submesh {submesh}: {e.Message}", e);
                }
                foreach (TextureTransform transform in uvTransforms[node.Id]) transform.Apply(decoded.Attributes);
                if (materials[material]?["normalTexture"] is JsonObject normalTexture &&
                    (!decoded.Attributes.ContainsKey("TANGENT") || uvTransforms[node.Id].Count > 0)) {
                    MeshDecoder.GenerateTangents(decoded.Attributes, decoded.Indices, normalTexture["texCoord"]?.GetValue<int>() ?? 0);
                }
                JsonObject attributes = new();
                foreach ((string semantic, MeshAttribute attribute) in decoded.Attributes) {
                    attributes[semantic] = Accessor(attribute.Values, attribute.Width, semantic == "JOINTS_0" ? 5123 : 5126,
                        34962, semantic == "POSITION");
                }
                int indices = Accessor(decoded.Indices.Select(value => (double) value).ToArray(), 1, 5125, 34963);
                primitives.Add(new JsonObject { ["attributes"] = attributes, ["indices"] = indices, ["material"] = material });
                if (decoded.MorphPositions.Length > 0) {
                    primitives[^1]!["targets"] = Json(decoded.MorphPositions.Select(target => new {
                        POSITION = Accessor(target.Values, 3, 5126, 34962, true)
                    }).ToArray());
                    if (morphWeights is not null && !morphWeights.SequenceEqual(decoded.MorphWeights)) throw new InvalidDataException("Submeshes disagree on morph weights.");
                    morphWeights = decoded.MorphWeights;
                }
                if (decoded.Skin is { } skin && skinIndex is null) {
                    skinIndex = skins.Count;
                    skins.Add(Json(new { name = node.Name, skeleton = NodeIndex(skin.Root), joints = skin.Bones.Select(NodeIndex).ToArray(),
                        inverseBindMatrices = Accessor(skin.BindTransforms.SelectMany(MatrixValues).Select(value => (double) value).ToArray(), 16, 5126) }));
                }
                primitiveReports.Add(new { block = node.Id, node.Name, submesh, vertices = decoded.Attributes["POSITION"].Values.Length / 3,
                    triangles = decoded.Indices.Length / 3, maximumIndex = decoded.Indices.Max(), joints = decoded.Skin?.Bones.Length ?? 0 });
            }
            nodes[nodeMap[node.Id]]!["mesh"] = meshes.Count;
            if (skinIndex.HasValue) nodes[nodeMap[node.Id]]!["skin"] = skinIndex.Value;
            meshes.Add(new JsonObject { ["name"] = node.Name, ["primitives"] = primitives });
            if (morphWeights is not null) meshes[^1]!["weights"] = Json(morphWeights);
        }
        if (meshes.Count == 0) throw new InvalidDataException("No meshes to export.");
        int[] roots = document.Roots.Where(root => root >= 0).Select(NodeIndex).ToArray();
        if (roots.Length == 0) throw new InvalidDataException("No scene roots.");
        foreach (int root in document.Roots.Where(root => root >= 0)) {
            if (parents.ContainsKey(root)) throw new InvalidDataException("Scene root has a parent.");
        }
        // MS2 uses Z-up and centimeter-sized coordinates. Keep the source skeleton untouched.
        int sceneRoot = nodes.Count;
        nodes.Add(Json(new { name = "MS2 coordinate system", rotation = new[] { -MathF.Sqrt(0.5f), 0, 0, MathF.Sqrt(0.5f) },
            scale = new[] { 0.01f, 0.01f, 0.01f }, children = roots }));
        // Skinning is driven by joint world transforms, not the mesh node's transform.
        // Put skinned mesh instances at scene root to express this explicitly in glTF.
        int[] skinnedNodes = Enumerable.Range(0, nodes.Count).Where(i => nodes[i]?["skin"] is not null).ToArray();
        foreach (JsonNode? node in nodes) {
            if (node?["children"] is JsonArray children) {
                int[] retained = children.Select(child => child!.GetValue<int>()).Except(skinnedNodes).ToArray();
                if (retained.Length == 0) node.AsObject().Remove("children");
                else node["children"] = Json(retained);
            }
        }
        foreach (int index in skinnedNodes) {
            foreach (string property in new[] { "translation", "rotation", "scale" }) nodes[index]!.AsObject().Remove(property);
        }
        JsonArray animations = [];
        List<object> unboundAnimationTargets = [];
        foreach (AnimationClip clip in clips ?? []) {
            JsonArray animationSamplers = [], animationChannels = [];
            Dictionary<string, int[]> targets = document.Nodes.Values.GroupBy(node => node.Name)
                .ToDictionary(group => group.Key, group => group.Select(node => NodeIndex(node.Id)).ToArray());
            Dictionary<string, int> targetCounts = targets.ToDictionary(pair => pair.Key, pair => pair.Value.Length);
            HashSet<(int Node, string Path)> used = [];
            Dictionary<string, int> timeAccessors = [];
            foreach (AnimationTrack track in clip.Tracks) {
                if (AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, targetCounts)) {
                    unboundAnimationTargets.Add(new { clip = clip.Name, node = track.Node, path = track.Path,
                        reason = "Missing posed accumulation target remains unbound, matching client FillInfo behavior." });
                    continue;
                }
                if (!targets.TryGetValue(track.Node, out int[]? matches) || matches.Length != 1) {
                    throw new InvalidDataException($"Clip {clip.Name}: expected one node named {track.Node}, found {matches?.Length ?? 0}.");
                }
                if (!used.Add((matches[0], track.Path))) throw new InvalidDataException($"Duplicate animation channel {clip.Name}/{track.Node}/{track.Path}.");
                string timeKey = Convert.ToBase64String(System.Runtime.InteropServices.MemoryMarshal.AsBytes(track.Times.AsSpan()));
                if (!timeAccessors.TryGetValue(timeKey, out int timeAccessor)) {
                    timeAccessors[timeKey] = timeAccessor = Accessor(track.Times, 1, 5126, bounds: true);
                }
                animationSamplers.Add(Json(new { input = timeAccessor, output = Accessor(track.Values, track.Width, 5126), interpolation = track.Interpolation }));
                animationChannels.Add(Json(new { sampler = animationSamplers.Count - 1, target = new { node = matches[0], path = track.Path } }));
            }
            if (animationChannels.Count == 0) throw new InvalidDataException($"Clip {clip.Name}: no bound animation tracks.");
            JsonObject animation = new() { ["name"] = clip.Name, ["samplers"] = animationSamplers, ["channels"] = animationChannels };
            if (clip.SourceSequenceBlock is not null) animation["extras"] = Json(new {
                sourceSequence = clip.SourceSequence, sourceSequenceBlock = clip.SourceSequenceBlock, sourceEvent = clip.SourceEvent
            });
            animations.Add(animation);
        }
        JsonObject gltf = new() {
            ["asset"] = Json(new { version = "2.0", generator = "MapleStory2 native NIF converter" }),
            ["scene"] = 0, ["scenes"] = Json(new[] { new { nodes = new[] { sceneRoot }.Concat(skinnedNodes).ToArray() } }),
            ["nodes"] = nodes, ["meshes"] = meshes, ["accessors"] = accessors, ["bufferViews"] = views,
            ["buffers"] = Json(new[] { new { byteLength = buffer.Length, uri = "data:application/octet-stream;base64," + Convert.ToBase64String(buffer.ToArray()) } }),
            ["materials"] = materials
        };
        if (skins.Count > 0) gltf["skins"] = skins;
        if (animations.Count > 0) gltf["animations"] = animations;
        if (images.Count > 0) { gltf["images"] = images; gltf["textures"] = textures; gltf["samplers"] = samplers; }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        string temporary = output + $".{Guid.NewGuid():N}.tmp";
        try {
            File.WriteAllText(temporary, gltf.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, output, overwrite);
        } finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return new { source = document.Path, output = Path.GetFullPath(output), nodes = nodes.Count, meshes = meshes.Count,
            skins = skins.Count, textures = textures.Count, animations = animations.Count, hiddenMeshes,
            unboundAnimationTargets, omitted = document.Omitted, primitives = primitiveReports };
    }
    private int NodeIndex(int block) => nodeMap.TryGetValue(block, out int index) ? index :
        throw new NotSupportedException($"Referenced scene block {block} ({document.Blocks[block].Type}) is not supported.");

    private int Accessor(double[] values, int width, int type, int? target = null, bool bounds = false) {
        if (values.Length == 0 || values.Length % width != 0) throw new InvalidDataException("Invalid accessor length.");
        while (buffer.Length % 4 != 0) buffer.WriteByte(0);
        long start = buffer.Length;
        using (BinaryWriter writer = new(buffer, System.Text.Encoding.UTF8, true)) {
            foreach (double value in values) {
                if (!double.IsFinite(value)) throw new InvalidDataException("Non-finite accessor value.");
                if (type == 5126) {
                    float converted = (float) value;
                    if (!float.IsFinite(converted)) throw new InvalidDataException("Accessor value exceeds float32 range.");
                    writer.Write(converted);
                }
                else if (type == 5123) writer.Write(checked((ushort) value));
                else writer.Write(checked((uint) value));
            }
        }
        JsonObject view = new() { ["buffer"] = 0, ["byteOffset"] = start, ["byteLength"] = buffer.Length - start };
        if (target.HasValue) view["target"] = target.Value;
        JsonObject accessor = new() { ["bufferView"] = views.Count, ["componentType"] = type, ["count"] = values.Length / width,
            ["type"] = width switch { 1 => "SCALAR", 2 => "VEC2", 3 => "VEC3", 4 => "VEC4", 16 => "MAT4", _ => throw new InvalidDataException("Invalid vector width.") } };
        if (bounds) {
            accessor["min"] = Json(Enumerable.Range(0, width).Select(c => values.Where((_, i) => i % width == c).Min()).ToArray());
            accessor["max"] = Json(Enumerable.Range(0, width).Select(c => values.Where((_, i) => i % width == c).Max()).ToArray());
        }
        views.Add(view); accessors.Add(accessor);
        return accessors.Count - 1;
    }
    private int Material(NifNode node) {
        uvTransforms[node.Id] = [];
        nextUv = checked((int) (node.Mesh!.Streams.SelectMany(stream => stream.Semantics)
            .Where(semantic => semantic.Name == "TEXCOORD").Select(semantic => semantic.Index + 1).DefaultIfEmpty(0u).Max()));
        List<int> properties = [..node.Properties];
        int parent = node.Id;
        while (parents.TryGetValue(parent, out parent)) properties.AddRange(document.Nodes[parent].Properties);
        JsonObject pbr = new() { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
        JsonObject material = new() { ["name"] = node.Name, ["pbrMetallicRoughness"] = pbr };
        JsonObject lighting = new() { ["specularEnabled"] = false };
        JsonObject renderState = new();
        HashSet<string> seen = [];
        foreach (int property in properties) {
            NifReader r = document.Reader(property);
            string type = document.Blocks[property].Type;
            if (!seen.Add(type)) continue;
            if (type is not ("NiMaterialProperty" or "NiTexturingProperty" or "NiAlphaProperty" or "NiSpecularProperty" or "NiZBufferProperty")) continue;
            document.ObjectNet(r);
            if (type == "NiZBufferProperty") {
                renderState["depthFlags"] = r.U16();
            } else if (type == "NiSpecularProperty") {
                lighting["specularEnabled"] = (r.U16() & 1) != 0;
            } else if (type == "NiMaterialProperty") {
                Vector3 ambient = r.Vector();
                Vector3 diffuse = r.Vector();
                Vector3 specular = r.Vector();
                Vector3 emissive = r.Vector();
                float power = r.Float();
                float alpha = r.Float();
                lighting["ambient"] = Json(new[] { ambient.X, ambient.Y, ambient.Z });
                lighting["specular"] = Json(new[] { specular.X, specular.Y, specular.Z });
                lighting["power"] = power;
                pbr["baseColorFactor"] = Json(new[] { diffuse.X, diffuse.Y, diffuse.Z, alpha }.Select(v => Math.Clamp(v, 0, 1)).ToArray());
                material["emissiveFactor"] = Json(new[] { emissive.X, emissive.Y, emissive.Z }.Select(v => Math.Clamp(v, 0, 1)).ToArray());
            } else if (type == "NiAlphaProperty") {
                ushort flags = r.U16();
                byte cutoff = r.Byte();
                // glTF BLEND cannot also express the source alpha test or depth writes.
                renderState["alphaFlags"] = flags;
                renderState["alphaThreshold"] = cutoff;
                // The face uses alpha testing AND source-alpha blending. Mask alone makes
                // every zero-alpha texel opaque when its source threshold is zero.
                if ((flags & 1) != 0) material["alphaMode"] = "BLEND";
                else if ((flags & 512) != 0) {
                    int comparison = (flags >> 10) & 7;
                    if (comparison is not (4 or 6)) throw new NotSupportedException($"Alpha test comparison {comparison} cannot use glTF MASK directly.");
                    if (comparison == 4 && cutoff == 255) throw new NotSupportedException("Alpha test rejects every fragment.");
                    material["alphaMode"] = "MASK";
                    material["alphaCutoff"] = (cutoff + (comparison == 4 ? 0.5 : 0)) / 255.0;
                }
            } else {
                r.U16();
                int count = r.Count();
                JsonObject extraTextures = new();
                for (int slot = 0; slot < count; slot++) {
                    if (!r.Bool()) continue;
                    JsonObject texture = TextureDescriptor(r, node.Id);
                    if (slot == 0) pbr["baseColorTexture"] = texture;
                    else if (slot == 6) material["normalTexture"] = texture;
                    else extraTextures[$"slot{slot}"] = texture;
                    if (slot == 5) r.Take(24);
                    if (slot == 7) r.Float();
                }
                int shaders = r.Count();
                for (int i = 0; i < shaders; i++) {
                    if (!r.Bool()) continue;
                    JsonObject texture = TextureDescriptor(r, node.Id);
                    uint map = r.U32();
                    extraTextures[$"shader{map}"] = texture;
                }
                if (extraTextures.Count > 0) material["extras"] = new JsonObject { ["nifTextures"] = extraTextures };
            }
            r.Finish();
        }
        ApplyMaterialColors(node, material, pbr);
        if (renderState.Count > 0) material["extras"]!["nifRenderState"] = renderState;
        foreach (int extra in node.ExtraData.Where(id => document.Blocks[id].Type == "NiFloatExtraData")) {
            NifReader r = document.Reader(extra);
            string name = document.Name(r);
            if (name is "ColorBoost" or "FresnelBoost" or "FresnelExponent") {
                lighting[name] = r.Float(); r.Finish();
            }
        }
        if (lighting.Count > 0) material["extras"]!["nifLighting"] = lighting;
        materials.Add(material);
        return materials.Count - 1;
    }
    private JsonObject TextureDescriptor(NifReader r, int node) {
        int source = r.I32();
        ushort flags = r.U16();
        r.U16();
        int uv = flags & 255;
        if (r.Bool()) {
            Vector2 translation = new(r.Float(), r.Float()), scale = new(r.Float(), r.Float());
            float rotation = r.Float();
            uint method = r.U32();
            Vector2 center = new(r.Float(), r.Float());
            Matrix3x2 matrix = TextureTransform.Create(translation, scale, rotation, method, center);
            if (matrix != Matrix3x2.Identity) {
                TextureTransform? existing = uvTransforms[node].FirstOrDefault(transform => transform.Source == uv && transform.Matrix == matrix);
                if (existing is null) {
                    existing = new TextureTransform(uv, nextUv++, matrix);
                    uvTransforms[node].Add(existing);
                }
                uv = existing.Target;
            }
        }
        JsonObject info = new() { ["index"] = Texture(source, flags) };
        if (uv != 0) info["texCoord"] = uv;
        return info;
    }
    private int Texture(int block, ushort flags) {
        NifReader r = document.Reader(block, "NiSourceTexture");
        document.ObjectNet(r);
        bool external = r.Bool();
        string name = document.Name(r);
        int pixels = r.I32(); r.Take(12); r.Byte(); r.Bool(); r.Bool(); r.Finish();
        string fileName = Path.GetFileName(name.Replace('\\', '/'));
        string path = external ? textureCatalog.Resolve(document.Path, name) : $"embedded:{pixels}";
        ushort sampling = (ushort) (flags & 0xFF00);
        string key = $"{path}:{sampling}";
        if (textureMap.TryGetValue(key, out int index)) return index;
        TexturePixels decoded = external ? DdsTexture.Decode(File.ReadAllBytes(path)) : EmbeddedTexture.Decode(document, pixels);
        byte[] png = decoded.ToPng();
        index = textures.Count;
        textureMap[key] = index;
        images.Add(Json(new { name = fileName, uri = "data:image/png;base64," + Convert.ToBase64String(png) }));
        imagePixels.Add(decoded);
        if (!samplerMap.TryGetValue(sampling, out int sampler)) {
            samplerMap[sampling] = sampler = samplers.Count;
            int filter = (flags >> 8) & 15, clamp = (flags >> 12) & 3;
            int min = filter switch { 0 => 9728, 1 => 9729, 2 => 9987, 3 => 9984, 4 => 9985, 5 => 9986, _ => throw new NotSupportedException($"Texture filter {filter}.") };
            samplers.Add(Json(new { wrapS = (clamp & 2) != 0 ? 10497 : 33071, wrapT = (clamp & 1) != 0 ? 10497 : 33071,
                magFilter = filter is 0 or 3 or 5 ? 9728 : 9729, minFilter = min }));
        }
        textures.Add(Json(new { source = images.Count - 1, sampler }));
        return index;
    }
    private void ApplyMaterialColors(NifNode node, JsonObject material, JsonObject pbr) {
        JsonObject extras = material["extras"]?.AsObject() ?? new JsonObject();
        if (material["extras"] is null) material["extras"] = extras;
        extras["nifShader"] = node.MaterialName;
        Dictionary<string, Vector3> colors = [];
        uint? map = null;
        foreach (int extra in node.ExtraData) {
            NifReader r = document.Reader(extra);
            string name = document.Name(r);
            if (document.Blocks[extra].Type == "NiColorExtraData" && name.StartsWith("OverrideColor", StringComparison.Ordinal)) {
                colors[name] = r.Vector(); r.Float(); r.Finish();
            } else if (document.Blocks[extra].Type == "NiIntegerExtraData" && name == "ColorOverrideMapIndex") {
                map = r.U32(); r.Finish();
            }
        }
        if (node.MaterialName is not ("MS2CharacterMaterial" or "MS2CharacterSkinMaterial" or "MS2CharacterHairMaterial") || map is null || colors.Count != 3) return;
        Vector3[] values = Enumerable.Range(0, 3).Select(i => colors[$"OverrideColor{i}"]).ToArray();
        extras["nifOverrideColors"] = Json(values.Select(color => new[] { color.X, color.Y, color.Z }));
        if (extras["nifTextures"]?[$"shader{map}"] is not JsonObject control || pbr["baseColorTexture"] is not JsonObject baseInfo) return;
        if ((control["texCoord"]?.GetValue<int>() ?? 0) != (baseInfo["texCoord"]?.GetValue<int>() ?? 0)) throw new NotSupportedException("Color override uses different base/control UV coordinates.");
        int baseTexture = baseInfo["index"]!.GetValue<int>(), maskTexture = control["index"]!.GetValue<int>();
        JsonNode maskSampler = samplers[textures[maskTexture]!["sampler"]!.GetValue<int>()]!;
        JsonNode baseSampler = samplers[textures[baseTexture]!["sampler"]!.GetValue<int>()]!;
        TexturePixels tinted = MaterialColors.Bake(imagePixels[textures[baseTexture]!["source"]!.GetValue<int>()], imagePixels[textures[maskTexture]!["source"]!.GetValue<int>()], values,
            maskSampler["magFilter"]!.GetValue<int>() != 9728, maskSampler["wrapS"]!.GetValue<int>() == 10497, maskSampler["wrapT"]!.GetValue<int>() == 10497,
            baseSampler["magFilter"]!.GetValue<int>() != 9728, baseSampler["wrapS"]!.GetValue<int>() == 10497, baseSampler["wrapT"]!.GetValue<int>() == 10497);
        extras["nifBaseColorTexture"] = baseInfo.DeepClone();
        extras["nifColorControlTexture"] = control.DeepClone();
        images.Add(Json(new { name = node.Name + " color override", uri = "data:image/png;base64," + Convert.ToBase64String(tinted.ToPng()) }));
        imagePixels.Add(tinted);
        textures.Add(Json(new { source = images.Count - 1, sampler = textures[baseTexture]!["sampler"]!.GetValue<int>() }));
        baseInfo["index"] = textures.Count - 1;
    }
}
