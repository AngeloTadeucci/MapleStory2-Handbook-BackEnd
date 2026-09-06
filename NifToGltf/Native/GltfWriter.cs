using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NifToGltf.Native;

internal sealed class GltfWriter(NifDocument document, string? textureRoot) {
    private readonly JsonArray nodes = [], meshes = [], skins = [], accessors = [], views = [], materials = [], images = [], textures = [];
    private readonly MemoryStream buffer = new();
    private readonly Dictionary<int, int> nodeMap = [];
    private readonly Dictionary<string, int> textureMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> parents = [];
    private readonly List<object> primitiveReports = [];
    private readonly List<string> hiddenMeshes = [];
    private string[]? textureFiles;
    public static JsonNode Json<T>(T value) => JsonSerializer.SerializeToNode(value)!;
    public static float[] MatrixValues(Matrix4x4 m) => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    public object Write(string output, IReadOnlyList<AnimationClip>? clips = null, bool overwrite = false) {
        foreach (NifNode node in document.Nodes.Values) {
            nodeMap[node.Id] = nodes.Count;
            if (!Matrix4x4.Decompose(node.Transform, out Vector3 scale, out Quaternion rotation, out Vector3 translation)) {
                throw new InvalidDataException($"Cannot decompose {node.Name} transform.");
            }
            nodes.Add(Json(new { name = node.Name, translation = new[] { translation.X, translation.Y, translation.Z },
                rotation = new[] { rotation.X, rotation.Y, rotation.Z, rotation.W }, scale = new[] { scale.X, scale.Y, scale.Z } }));
            foreach (int child in node.Children) {
                if (!parents.TryAdd(child, node.Id)) throw new InvalidDataException($"Multiple parents for block {child}.");
            }
        }
        foreach (NifNode node in document.Nodes.Values) {
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
            JsonArray primitives = [];
            int material = Material(node);
            int? skinIndex = null;
            for (int submesh = 0; submesh < node.Mesh.Submeshes; submesh++) {
                DecodedPrimitive decoded;
                try { decoded = MeshDecoder.Decode(document, node, submesh); }
                catch (Exception e) when (e is InvalidDataException or NotSupportedException) {
                    throw new InvalidDataException($"{document.Path}: block {node.Id} ({node.Name}), submesh {submesh}: {e.Message}", e);
                }
                JsonObject attributes = new();
                foreach ((string semantic, MeshAttribute attribute) in decoded.Attributes) {
                    attributes[semantic] = Accessor(attribute.Values, attribute.Width, semantic == "JOINTS_0" ? 5123 : 5126,
                        34962, semantic == "POSITION");
                }
                int indices = Accessor(decoded.Indices.Select(value => (double) value).ToArray(), 1, 5125, 34963);
                primitives.Add(new JsonObject { ["attributes"] = attributes, ["indices"] = indices, ["material"] = material });
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
        foreach (AnimationClip clip in clips ?? []) {
            JsonArray animationSamplers = [], animationChannels = [];
            Dictionary<string, int[]> targets = document.Nodes.Values.GroupBy(node => node.Name)
                .ToDictionary(group => group.Key, group => group.Select(node => NodeIndex(node.Id)).ToArray());
            HashSet<(int Node, string Path)> used = [];
            Dictionary<string, int> timeAccessors = [];
            foreach (AnimationTrack track in clip.Tracks) {
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
            animations.Add(new JsonObject { ["name"] = clip.Name, ["samplers"] = animationSamplers, ["channels"] = animationChannels });
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
        if (images.Count > 0) { gltf["images"] = images; gltf["textures"] = textures; }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        string temporary = output + $".{Guid.NewGuid():N}.tmp";
        try {
            File.WriteAllText(temporary, gltf.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, output, overwrite);
        } finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return new { source = document.Path, output = Path.GetFullPath(output), nodes = nodes.Count, meshes = meshes.Count,
            skins = skins.Count, textures = textures.Count, animations = animations.Count, hiddenMeshes, primitives = primitiveReports };
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
        List<int> properties = [..node.Properties];
        int parent = node.Id;
        while (parents.TryGetValue(parent, out parent)) properties.AddRange(document.Nodes[parent].Properties);
        JsonObject pbr = new() { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
        JsonObject material = new() { ["name"] = node.Name, ["pbrMetallicRoughness"] = pbr };
        HashSet<string> seen = [];
        foreach (int property in properties) {
            NifReader r = document.Reader(property);
            string type = document.Blocks[property].Type;
            if (!seen.Add(type)) continue;
            if (type is not ("NiMaterialProperty" or "NiTexturingProperty" or "NiAlphaProperty")) continue;
            document.ObjectNet(r);
            if (type == "NiMaterialProperty") {
                r.Vector();
                Vector3 diffuse = r.Vector();
                r.Vector();
                Vector3 emissive = r.Vector();
                r.Float();
                float alpha = r.Float();
                pbr["baseColorFactor"] = Json(new[] { diffuse.X, diffuse.Y, diffuse.Z, alpha }.Select(v => Math.Clamp(v, 0, 1)).ToArray());
                material["emissiveFactor"] = Json(new[] { emissive.X, emissive.Y, emissive.Z }.Select(v => Math.Clamp(v, 0, 1)).ToArray());
            } else if (type == "NiAlphaProperty") {
                ushort flags = r.U16();
                byte cutoff = r.Byte();
                if ((flags & 512) != 0) { material["alphaMode"] = "MASK"; material["alphaCutoff"] = cutoff / 255.0; }
                else if ((flags & 1) != 0) material["alphaMode"] = "BLEND";
            } else {
                r.U16();
                int count = r.Count();
                JsonObject extraTextures = new();
                for (int slot = 0; slot < count; slot++) {
                    if (!r.Bool()) continue;
                    JsonObject texture = TextureDescriptor(r);
                    if (slot == 0) pbr["baseColorTexture"] = texture;
                    else if (slot == 6) material["normalTexture"] = texture;
                    else extraTextures[$"slot{slot}"] = texture;
                    if (slot == 5) r.Take(24);
                    if (slot == 7) r.Float();
                }
                int shaders = r.Count();
                for (int i = 0; i < shaders; i++) {
                    if (!r.Bool()) continue;
                    JsonObject texture = TextureDescriptor(r);
                    uint map = r.U32();
                    extraTextures[$"shader{map}"] = texture;
                }
                if (extraTextures.Count > 0) material["extras"] = new JsonObject { ["nifTextures"] = extraTextures };
            }
            r.Finish();
        }
        materials.Add(material);
        return materials.Count - 1;
    }
    private JsonObject TextureDescriptor(NifReader r) {
        int source = r.I32();
        ushort flags = r.U16();
        r.U16();
        if (r.Bool()) throw new NotSupportedException("UV texture transform.");
        int uv = flags & 255;
        JsonObject info = new() { ["index"] = Texture(source) };
        if (uv != 0) info["texCoord"] = uv;
        return info;
    }
    private int Texture(int block) {
        NifReader r = document.Reader(block, "NiSourceTexture");
        document.ObjectNet(r);
        if (r.Byte() != 1) throw new NotSupportedException($"Embedded texture at block {block}.");
        string name = document.Name(r);
        r.I32(); r.Take(12); r.Byte(); r.Bool(); r.Bool(); r.Finish();
        string fileName = Path.GetFileName(name.Replace('\\', '/'));
        string local = Path.Combine(Path.GetDirectoryName(document.Path)!, fileName);
        string? path = File.Exists(local) ? local : Directory.EnumerateFiles(Path.GetDirectoryName(document.Path)!)
            .FirstOrDefault(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase));
        if (path is null && textureRoot is not null) {
            textureFiles ??= Directory.GetFiles(textureRoot, "*.dds", SearchOption.AllDirectories);
            string[] matches = textureFiles.Where(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) throw new FileNotFoundException($"Texture {name}: expected one match under {textureRoot}, found {matches.Length}.");
            path = matches[0];
        }
        if (path is null) throw new FileNotFoundException($"Texture {name} missing beside {document.Path}; supply --textures.");
        if (textureMap.TryGetValue(path, out int index)) return index;
        byte[] png = DdsTexture.ToPng(File.ReadAllBytes(path));
        index = textures.Count;
        textureMap[path] = index;
        images.Add(Json(new { name = fileName, uri = "data:image/png;base64," + Convert.ToBase64String(png) }));
        textures.Add(Json(new { source = images.Count - 1 }));
        return index;
    }
}
