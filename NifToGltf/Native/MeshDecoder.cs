using System.Numerics;

namespace NifToGltf.Native;

internal sealed record MeshAttribute(int Width, double[] Values);
internal sealed record DecodedPrimitive(Dictionary<string, MeshAttribute> Attributes, uint[] Indices, NifSkin? Skin) {
    public MeshAttribute[] MorphPositions { get; init; } = [];
    public double[] MorphWeights { get; init; } = [];
}

internal static class MeshDecoder {
    public static DecodedPrimitive Decode(NifDocument document, NifNode node, int submesh) {
        NifMesh mesh = node.Mesh ?? throw new InvalidDataException("Not a mesh.");
        if (mesh.Primitive is not (0 or 1)) throw new NotSupportedException($"{node.Name}: primitive type {mesh.Primitive}, expected triangles or triangle strips.");
        if ((uint) submesh >= mesh.Submeshes) throw new InvalidDataException("Invalid submesh.");
        Dictionary<string, MeshAttribute> source = [];
        foreach (StreamBinding binding in mesh.Streams) {
            if (binding.PerInstance || binding.Regions.Length != mesh.Submeshes) throw new NotSupportedException("Invalid/instanced stream mapping.");
            NifStream stream = document.Stream(binding.Block);
            if (stream.Formats.Length != binding.Semantics.Length) throw new InvalidDataException("Semantic/format count mismatch.");
            for (int c = 0; c < binding.Semantics.Length; c++) {
                Semantic semantic = binding.Semantics[c];
                string name = $"{semantic.Name}:{semantic.Index}";
                if (!source.TryAdd(name, new MeshAttribute(NifStream.Components(stream.Formats[c]),
                        stream.ReadComponent(c, binding.Regions[submesh])))) {
                    throw new InvalidDataException($"Duplicate semantic {name}.");
                }
            }
        }
        MeshAttribute Required(string name, int width) {
            if (!source.TryGetValue(name, out MeshAttribute? value) || value.Width != width) {
                throw new InvalidDataException($"{node.Name}: missing or invalid {name}.");
            }
            return value;
        }
        NifSkin? skin = null;
        NifMorph? morph = null;
        foreach (int modifier in mesh.Modifiers) {
            if ((uint) modifier < document.Blocks.Length && document.Blocks[modifier].Type == "NiMorphMeshModifier") {
                if (morph is not null) throw new NotSupportedException("Multiple morph modifiers.");
                morph = document.Morph(modifier);
                continue;
            }
            if (skin is not null) throw new NotSupportedException("Multiple skin modifiers.");
            skin = document.Skin(modifier);
        }
        bool grafted = mesh.Modifiers.Any(document.GraftedSkins.ContainsKey);
        bool sourceSkinned = skin is not null && !grafted;
        string positionKey = sourceSkinned ? "POSITION_BP:0" : "POSITION:0";
        string normalKey = sourceSkinned ? "NORMAL_BP:0" : "NORMAL:0";
        MeshAttribute positions = Required(positionKey, 3);
        int vertices = positions.Values.Length / 3;
        Dictionary<string, MeshAttribute> attributes = new() { ["POSITION"] = positions };
        if (source.ContainsKey(normalKey)) {
            MeshAttribute normals = Required(normalKey, 3);
            double[] values = (double[]) normals.Values.Clone();
            for (int i = 0; i < values.Length; i += 3) {
                double length = Math.Sqrt(values[i] * values[i] + values[i + 1] * values[i + 1] + values[i + 2] * values[i + 2]);
                if (length < 1e-8) throw new InvalidDataException("Zero-length normal.");
                for (int c = 0; c < 3; c++) values[i + c] /= length;
            }
            attributes["NORMAL"] = new MeshAttribute(3, values);
        }
        foreach ((string name, MeshAttribute attribute) in source) {
            if (name.StartsWith("TEXCOORD:")) {
                if (attribute.Width != 2) throw new InvalidDataException("Invalid UV width.");
                // NIF and glTF textures both address the top-left of the decoded image.
                attributes[$"TEXCOORD_{name.Split(':')[1]}"] = attribute;
            }
            if (name.StartsWith("COLOR:")) attributes[$"COLOR_{name.Split(':')[1]}"] = attribute;
        }
        string tangentKey = sourceSkinned ? "TANGENT_BP:0" : "TANGENT:0";
        string binormalKey = sourceSkinned ? "BINORMAL_BP:0" : "BINORMAL:0";
        if (source.ContainsKey(tangentKey) && source.ContainsKey(binormalKey) && attributes.TryGetValue("NORMAL", out MeshAttribute? normal)) {
            double[] tangents = Required(tangentKey, 3).Values;
            double[] binormals = Required(binormalKey, 3).Values;
            if (tangents.Length != vertices * 3 || binormals.Length != vertices * 3) throw new InvalidDataException("Tangent count mismatch.");
            double[] values = new double[vertices * 4];
            for (int i = 0; i < vertices; i++) {
                Vector3 Vector(double[] data) => new((float) data[i * 3], (float) data[i * 3 + 1], (float) data[i * 3 + 2]);
                Vector3 n = Vector(normal.Values), t = Vector(tangents);
                t -= n * Vector3.Dot(n, t);
                if (t.LengthSquared() < 1e-12f) {
                    // Regenerate the whole basis from triangle UV derivatives after decoding indices.
                    values = [];
                    break;
                }
                t = Vector3.Normalize(t);
                values[i * 4] = t.X; values[i * 4 + 1] = t.Y; values[i * 4 + 2] = t.Z;
                values[i * 4 + 3] = Vector3.Dot(Vector3.Cross(n, t), Vector(binormals)) < 0 ? -1 : 1;
            }
            if (values.Length > 0) attributes["TANGENT"] = new MeshAttribute(4, values);
        }
        uint[] indices = Required("INDEX:0", 1).Values.Select(value => ExactIndex(value, vertices)).ToArray();
        if (mesh.Primitive == 1) indices = TriangulateStrip(indices);
        if (indices.Length == 0 || indices.Length % 3 != 0) throw new InvalidDataException("Invalid triangle index count.");
        if (grafted) {
            attributes["JOINTS_0"] = new MeshAttribute(4, new double[vertices * 4]);
            attributes["WEIGHTS_0"] = new MeshAttribute(4, Enumerable.Range(0, vertices * 4).Select(i => i % 4 == 0 ? 1.0 : 0).ToArray());
        } else if (skin is not null) {
            // Software skinning uses the full bone array directly, without a GPU palette.
            MeshAttribute palette = source.ContainsKey("BONE_PALETTE:0") ? Required("BONE_PALETTE:0", 1) :
                (skin.Flags & 1) != 0 ? new(1, Enumerable.Range(0, skin.Bones.Length).Select(i => (double) i).ToArray()) : Required("BONE_PALETTE:0", 1);
            MeshAttribute joints = Required("BLENDINDICES:0", 4);
            if (!source.TryGetValue("BLENDWEIGHT:0", out MeshAttribute? weights) || weights.Width is < 1 or > 4) {
                throw new InvalidDataException("Invalid blend weights.");
            }
            (double[] outputJoints, double[] outputWeights) = DecodeSkin(joints.Values, weights.Values, weights.Width,
                palette.Values, skin.Bones.Length, vertices);
            attributes["JOINTS_0"] = new MeshAttribute(4, outputJoints);
            attributes["WEIGHTS_0"] = new MeshAttribute(4, outputWeights);
        }
        foreach ((string name, MeshAttribute attribute) in attributes) {
            if (attribute.Values.Length != vertices * attribute.Width) throw new InvalidDataException($"{name} count differs from positions.");
        }
        MeshAttribute[] morphPositions = [];
        double[] morphWeights = [];
        if (morph is not null) {
            string semantic = "MORPH_" + morph.Semantics[0].Name;
            MeshAttribute basis = Required(semantic + ":0", 3);
            MeshAttribute weights = Required("MORPHWEIGHTS:0", 1);
            if (weights.Values.Length < morph.Targets || weights.Values[0] != 1) throw new NotSupportedException("Relative morph base weight must be one.");
            if (basis.Values.Length != positions.Values.Length) throw new InvalidDataException("Morph vertex count mismatch.");
            // Target zero is the absolute base. Remaining streams are offsets,
            // exactly as glTF morph targets. The destination stream may be stale.
            attributes["POSITION"] = basis;
            morphPositions = Enumerable.Range(1, morph.Targets - 1).Select(i => Required($"{semantic}:{i}", 3)).ToArray();
            if (morphPositions.Any(target => target.Values.Length != basis.Values.Length)) throw new InvalidDataException("Morph target count mismatch.");
            morphWeights = weights.Values.Skip(1).Take(morph.Targets - 1).ToArray();
        }
        return new DecodedPrimitive(attributes, indices, skin) { MorphPositions = morphPositions, MorphWeights = morphWeights };
    }
    public static uint[] TriangulateStrip(uint[] strip) {
        List<uint> result = [];
        for (int i = 2; i < strip.Length; i++) {
            uint a = strip[i - 2], b = strip[i - 1], c = strip[i];
            if (a == b || b == c || a == c) continue;
            if ((i & 1) != 0) (a, b) = (b, a);
            result.AddRange([a, b, c]);
        }
        return result.ToArray();
    }
    public static void GenerateTangents(Dictionary<string, MeshAttribute> attributes, uint[] indices, int uvSet) {
        if (!attributes.TryGetValue("NORMAL", out MeshAttribute? normals) ||
            !attributes.TryGetValue($"TEXCOORD_{uvSet}", out MeshAttribute? uv)) throw new InvalidDataException("Normal mapping requires normals and UV coordinates.");
        double[] positions = attributes["POSITION"].Values;
        int count = positions.Length / 3;
        Vector3[] tangent = new Vector3[count], bitangent = new Vector3[count];
        Vector3 At(double[] values, int i) => new((float) values[i * 3], (float) values[i * 3 + 1], (float) values[i * 3 + 2]);
        for (int i = 0; i < indices.Length; i += 3) {
            int a = (int) indices[i], b = (int) indices[i + 1], c = (int) indices[i + 2];
            Vector3 e1 = At(positions, b) - At(positions, a), e2 = At(positions, c) - At(positions, a);
            double u1 = uv.Values[b * 2] - uv.Values[a * 2], v1 = uv.Values[b * 2 + 1] - uv.Values[a * 2 + 1],
                u2 = uv.Values[c * 2] - uv.Values[a * 2], v2 = uv.Values[c * 2 + 1] - uv.Values[a * 2 + 1];
            double determinant = u1 * v2 - u2 * v1;
            if (Math.Abs(determinant) < 1e-12) continue;
            Vector3 t = (e1 * (float) v2 - e2 * (float) v1) / (float) determinant,
                bt = (e2 * (float) u1 - e1 * (float) u2) / (float) determinant;
            foreach (int vertex in new[] { a, b, c }) { tangent[vertex] += t; bitangent[vertex] += bt; }
        }
        double[] output = new double[count * 4];
        for (int i = 0; i < count; i++) {
            Vector3 n = At(normals.Values, i), t = tangent[i] - n * Vector3.Dot(n, tangent[i]);
            if (t.LengthSquared() < 1e-12f) t = Vector3.Cross(Math.Abs(n.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY, n);
            t = Vector3.Normalize(t);
            output[i * 4] = t.X; output[i * 4 + 1] = t.Y; output[i * 4 + 2] = t.Z;
            output[i * 4 + 3] = Vector3.Dot(Vector3.Cross(n, t), bitangent[i]) < 0 ? -1 : 1;
        }
        attributes["TANGENT"] = new(4, output);
    }
    public static uint ExactIndex(double value, int limit) {
        if (value < 0 || value >= limit || value != Math.Truncate(value)) throw new InvalidDataException($"Index {value} outside [0,{limit}).");
        return checked((uint) value);
    }
    public static (double[] Joints, double[] Weights) DecodeSkin(double[] joints, double[] weights, int width,
        double[] palette, int boneCount, int vertices) {
        if (width is < 1 or > 4 || joints.Length != vertices * 4 || weights.Length != vertices * width) {
            throw new InvalidDataException("Skin attribute count mismatch.");
        }
        double[] resultJoints = new double[vertices * 4], resultWeights = new double[vertices * 4];
        for (int i = 0; i < vertices; i++) {
            double sum = 0;
            for (int c = 0; c < width; c++) sum += weights[i * width + c];
            if (width < 4 && sum < 1 - 1e-5) resultWeights[i * 4 + width] = 1 - sum;
            for (int c = 0; c < width; c++) resultWeights[i * 4 + c] = weights[i * width + c];
            double total = resultWeights.Skip(i * 4).Take(4).Sum();
            if (Math.Abs(total - 1) > 0.002 || total <= 0) throw new InvalidDataException($"Invalid weight sum {total} at vertex {i}.");
            for (int c = 0; c < 4; c++) {
                double weight = resultWeights[i * 4 + c];
                if (!double.IsFinite(weight) || weight < 0) throw new InvalidDataException("Invalid bone weight.");
                resultWeights[i * 4 + c] = weight / total;
                // Unused slots may hold sentinels. glTF still requires an in-range index.
                if (weight == 0) continue;
                uint paletteIndex = ExactIndex(joints[i * 4 + c], palette.Length);
                resultJoints[i * 4 + c] = ExactIndex(palette[paletteIndex], boneCount);
            }
            // NIF permits repeated influences. glTF requires each nonzero joint to appear once.
            for (int c = 1; c < 4; c++) {
                if (resultWeights[i * 4 + c] == 0) continue;
                for (int previous = 0; previous < c; previous++) {
                    if (resultWeights[i * 4 + previous] > 0 && resultJoints[i * 4 + previous] == resultJoints[i * 4 + c]) {
                        resultWeights[i * 4 + previous] += resultWeights[i * 4 + c];
                        resultWeights[i * 4 + c] = 0;
                        resultJoints[i * 4 + c] = 0;
                        break;
                    }
                }
            }
        }
        return (resultJoints, resultWeights);
    }
}
