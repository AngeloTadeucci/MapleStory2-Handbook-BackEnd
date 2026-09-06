using System.Numerics;
using System.Text.Json;
using NifToGltf.Native;

int tests = 0;
void Test(string name, Action test) {
    test(); tests++;
    Console.WriteLine($"PASS {name}");
}
void Assert(bool condition, string message) {
    if (!condition) throw new Exception(message);
}
void Near(double actual, double expected, double tolerance = 1e-5) => Assert(Math.Abs(actual - expected) <= tolerance, $"{actual} != {expected}");
void Reject(Action action) {
    try { action(); } catch (InvalidDataException) { return; }
    throw new Exception("Expected invalid data rejection.");
}
byte[] Bytes(Action<BinaryWriter> write) {
    using MemoryStream stream = new();
    using BinaryWriter writer = new(stream);
    write(writer); return stream.ToArray();
}
double[] Decode(uint format, byte[] payload) => new NifStream {
    Formats = [format], Regions = [new StreamRegion(0, 1)], Data = payload
}.ReadComponent(0, 0);

Test("all nine formats preserve signed, unsigned and normalized values", () => {
    Near(Decode(0x00010215, Bytes(w => w.Write((ushort) 65535)))[0], 65535);
    Near(Decode(0x00010425, Bytes(w => w.Write(uint.MaxValue)))[0], uint.MaxValue);
    Near(Decode(0x00010435, Bytes(w => w.Write(-1.25f)))[0], -1.25);
    foreach ((uint format, int count) in new[] { (0x00020436u, 2), (0x00030437u, 3), (0x00040438u, 4) }) {
        double[] result = Decode(format, Bytes(w => { for (int i = 0; i < count; i++) w.Write(i + 0.5f); }));
        for (int i = 0; i < count; i++) Near(result[i], i + 0.5);
    }
    Assert(Decode(0x00040108, [0, 1, 128, 255]).SequenceEqual(new double[] { 0, 1, 128, 255 }), "uint8 decoding");
    Near(Decode(0x00040110, [0, 64, 128, 255])[2], 128 / 255.0);
    Assert(Decode(0x00040214, Bytes(w => { w.Write(short.MinValue); w.Write((short) -1); w.Write((short) 0); w.Write(short.MaxValue); }))
        .SequenceEqual(new double[] { -32768, -1, 0, 32767 }), "int16 decoding");
});
Test("interleaved components honor region starts", () => {
    NifStream stream = new() { Formats = [0x00010435, 0x00010215], Regions = [new(1, 1)],
        Data = Bytes(w => { w.Write(1f); w.Write((ushort) 3); w.Write(2f); w.Write((ushort) 9); }) };
    Near(stream.ReadComponent(0, 0)[0], 2);
    Near(stream.ReadComponent(1, 0)[0], 9);
});
Test("palette indices are remapped and implicit fourth weight is retained", () => {
    (double[] joints, double[] weights) = MeshDecoder.DecodeSkin([0, 1, 2, 3], [0.2, 0.3, 0.1], 3, [5, 2, 3, 1], 6, 1);
    Assert(joints.SequenceEqual(new double[] { 5, 2, 3, 1 }), "Palette remapping");
    Near(weights[3], 0.4); Near(weights.Sum(), 1);
});
Test("duplicate influences are combined without changing deformation", () => {
    var result = MeshDecoder.DecodeSkin([0, 1, 0, 1], [0.2, 0.3, 0.1], 3, [5, 2], 6, 1);
    Assert(result.Joints.SequenceEqual(new double[] { 5, 2, 0, 0 }), "Duplicate joints remain");
    Near(result.Weights[0], 0.3); Near(result.Weights[1], 0.7);
    Near(10 * result.Weights[0] + 20 * result.Weights[1], 10 * (0.2 + 0.1) + 20 * (0.3 + 0.4));
});
Test("zero-weight sentinels are sanitized without accepting invalid used joints", () => {
    var result = MeshDecoder.DecodeSkin([0, 255, 255, 255], [1, 0, 0], 3, [2], 3, 1);
    Assert(result.Joints.SequenceEqual(new double[] { 2, 0, 0, 0 }), "Unused joint sanitization");
    Reject(() => MeshDecoder.DecodeSkin([1, 0, 0, 0], [1, 0, 0], 3, [2], 3, 1));
    Reject(() => MeshDecoder.DecodeSkin([0, 0, 0, 0], [1.2, 0, 0], 3, [2], 3, 1));
    Reject(() => MeshDecoder.ExactIndex(3, 3));
});
Test("row-vector transform matches a known rotation and translation", () => {
    byte[] data = Bytes(w => { foreach (float f in new float[] { 0, -1, 0, 1, 0, 0, 0, 0, 1, 10, 20, 30, 2 }) w.Write(f); });
    Vector3 point = Vector3.Transform(Vector3.UnitX, new NifReader(data).Transform());
    Near(point.X, 10); Near(point.Y, 22); Near(point.Z, 30);
});
Test("block bounds reject truncation", () => Reject(() => new NifReader(new byte[3]).U32()));
Test("cubic B-spline reduces to a known Bezier polynomial with four controls", () => {
    foreach (double t in new[] { 0, 0.25, 0.5, 0.75, 1 }) Near(AnimationReader.BSpline([0, 1, 4, 9], 1, t)[0], 3 * t + 6 * t * t);
});
Test("Hermite interpolation respects endpoint tangents", () => {
    Near(AnimationReader.Hermite(0, 1, 1, 1, 0.3), 0.3);
    Near(AnimationReader.Hermite(0, 1, 0, 0, 0.25), 0.15625);
});
Test("adaptive sampling resolves curvature between uniform keys", () => {
    double[] times = AnimationReader.Refine(t => [t * t], [0, 1], 0);
    Assert(times.Length > 2, "Curvature must introduce keys");
    for (int i = 1; i < times.Length; i++) {
        double middle = (times[i - 1] + times[i]) / 2;
        Near((times[i - 1] * times[i - 1] + times[i] * times[i]) / 2, middle * middle, 0.001);
    }
});

if (args.Length > 0) {
    NifDocument body = NifDocument.Load(args[0]);
    Test("female source mesh counts and named skinning", () => {
        int vertices = 0, triangles = 0;
        foreach (NifNode node in body.Nodes.Values.Where(node => node.Mesh is not null)) {
            for (int sub = 0; sub < node.Mesh!.Submeshes; sub++) {
                DecodedPrimitive primitive = MeshDecoder.Decode(body, node, sub);
                vertices += primitive.Attributes["POSITION"].Values.Length / 3;
                triangles += primitive.Indices.Length / 3;
                if (primitive.Skin is not { } skin) continue;
                Assert(skin.Bones.All(id => !body.Nodes[id].Name.StartsWith("Biped Object@")), "Renamed source bones");
                double[] weights = primitive.Attributes["WEIGHTS_0"].Values;
                for (int i = 0; i < weights.Length; i += 4) Near(weights.Skip(i).Take(4).Sum(), 1);
            }
        }
        Assert(vertices == 2034 && triangles == 2762, $"Unexpected body counts: {vertices}/{triangles}");
    });
    Test("female bind matrices cancel the source bone world transforms", () => {
        Dictionary<int, int> parents = body.Nodes.Values.SelectMany(n => n.Children.Select(c => (Child: c, Parent: n.Id))).ToDictionary(p => p.Child, p => p.Parent);
        Matrix4x4 World(int id) => body.Nodes[id].Transform * (parents.TryGetValue(id, out int parent) ? World(parent) : Matrix4x4.Identity);
        foreach (NifNode node in body.Nodes.Values.Where(n => n.Mesh is not null)) {
            foreach (int modifier in node.Mesh!.Modifiers) {
                NifSkin skin = body.Skin(modifier);
                for (int i = 0; i < skin.Bones.Length; i++) {
                    float[] actual = GltfWriter.MatrixValues(skin.BindTransforms[i] * World(skin.Bones[i]));
                    float[] identity = GltfWriter.MatrixValues(Matrix4x4.Identity);
                    for (int c = 0; c < 16; c++) Near(actual[c], identity[c], 0.0001);
                }
            }
        }
    });
    Test("glTF round trip preserves triangle counts and skin accessors", () => {
        string path = Path.Combine(Path.GetTempPath(), $"native-nif-{Guid.NewGuid():N}.gltf");
        try {
            new GltfWriter(body, null).Write(path);
            using JsonDocument output = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = output.RootElement;
            byte[] buffer = Convert.FromBase64String(root.GetProperty("buffers")[0].GetProperty("uri").GetString()!.Split(',')[1]);
            Assert(buffer.Length == root.GetProperty("buffers")[0].GetProperty("byteLength").GetInt32(), "Buffer length mismatch");
            Assert(root.GetProperty("meshes").GetArrayLength() == 10 && root.GetProperty("skins").GetArrayLength() == 9, "Mesh/skin counts");
            foreach (JsonElement skin in root.GetProperty("skins").EnumerateArray()) {
                JsonElement accessor = root.GetProperty("accessors")[skin.GetProperty("inverseBindMatrices").GetInt32()];
                Assert(accessor.GetProperty("count").GetInt32() == skin.GetProperty("joints").GetArrayLength(), "Bind matrix count");
            }
            string existing = File.ReadAllText(path);
            bool refused = false;
            try { new GltfWriter(body, null).Write(path); }
            catch (IOException) { refused = true; }
            Assert(refused && File.ReadAllText(path) == existing, "Existing output was replaced without overwrite");
        } finally { File.Delete(path); }
    });
    string models = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!, "../.."));
    Test("rigid CP gear receives every body bone and a head-only skin", () => {
        NifDocument gear = NifDocument.Load(Path.Combine(models, "Item/1/13/11300212_c_duckyballcap01_c.nif"));
        SkeletonGraft.Apply(gear, body, null);
        foreach (NifNode bone in body.Nodes.Values.Where(node => node.Mesh is null)) {
            Assert(gear.Nodes.Values.Count(node => node.Name == bone.Name) == 1, $"Missing or ambiguous grafted bone {bone.Name}");
        }
        foreach (NifNode mesh in gear.Nodes.Values.Where(node => node.Mesh is not null)) {
            DecodedPrimitive primitive = MeshDecoder.Decode(gear, mesh, 0);
            Assert(primitive.Skin!.Bones.Length == 1 && gear.Nodes[primitive.Skin.Bones[0]].Name == "Bip01 Head", "Wrong attachment");
            Assert(primitive.Attributes["JOINTS_0"].Values.All(value => value == 0), "Nonzero rigid joint");
            double[] weights = primitive.Attributes["WEIGHTS_0"].Values;
            for (int i = 0; i < weights.Length; i++) Near(weights[i], i % 4 == 0 ? 1 : 0);
        }
    });
    Test("skinned clothing rebinds by name without changing source bind matrices", () => {
        NifDocument gear = NifDocument.Load(Path.Combine(models, "Item/1/14/11400040_m_event011.nif"));
        NifDocument male = NifDocument.Load(Path.Combine(models, "Character/male/m_body.nif"));
        Dictionary<int, NifSkin> original = gear.Nodes.Values.Where(node => node.Mesh is not null)
            .SelectMany(node => node.Mesh!.Modifiers).Distinct().ToDictionary(id => id, gear.Skin);
        Dictionary<int, string[]> names = original.ToDictionary(pair => pair.Key, pair => pair.Value.Bones.Select(id => gear.Nodes[id].Name).ToArray());
        SkeletonGraft.Apply(gear, male, null);
        foreach ((int id, NifSkin source) in original) {
            NifSkin graft = gear.CanonicalSkins[id];
            Assert(graft.BindTransforms.SequenceEqual(source.BindTransforms), "Changed equipment bind matrices");
            Assert(graft.Bones.Select(bone => gear.Nodes[bone].Name).SequenceEqual(names[id]), "Changed joint order/names");
            Assert(graft.Bones.All(bone => bone >= gear.Blocks.Length), "Joint still targets original partial skeleton");
        }
    });
    string rabbitDirectory = Path.Combine(models, "Npc/21/21000174");
    KfmDocument rabbitKfm = KfmDocument.Read(Directory.GetFiles(rabbitDirectory, "*.kfm").Single());
    Test("KFM resolves all thirteen rabbit clips and the model", () => {
        Assert(rabbitKfm.Clips.Length == 13, "KFM clip count");
        Assert(File.Exists(rabbitKfm.Model), "KFM model resolution");
        Assert(rabbitKfm.Clips.All(clip => File.Exists(clip.File) && clip.Name.Length > 0), "KFM clip resolution");
    });
    Test("all rabbit clips share one visible mesh and bind to exact node names", () => {
        NifDocument rabbit = NifDocument.Load(rabbitKfm.Model);
        AnimationClip[] clips = rabbitKfm.Clips.Select(clip => AnimationReader.Read(clip.File, sequenceName: clip.Name) with { Name = clip.Name }).ToArray();
        foreach (AnimationTrack track in clips.SelectMany(clip => clip.Tracks)) {
            Near(track.Times[0], 0);
            Assert(track.Times[^1] > 0, "Missing end time");
            Assert(track.Values.Length == track.Times.Length * track.Width, "Animation sample count");
            for (int i = 1; i < track.Times.Length; i++) Assert((float) track.Times[i] > (float) track.Times[i - 1], "Times collapse after float32 serialization");
            if (track.Path == "rotation") {
                for (int i = 0; i < track.Values.Length; i += 4) Near(track.Values.Skip(i).Take(4).Sum(v => v * v), 1);
            }
        }
        string path = Path.Combine(Path.GetTempPath(), $"native-clips-{Guid.NewGuid():N}.gltf");
        try {
            new GltfWriter(rabbit, Path.Combine(models, "Textures")).Write(path, clips);
            using JsonDocument output = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = output.RootElement;
            Assert(root.GetProperty("meshes").GetArrayLength() == 1, "Hidden/duplicated meshes exported");
            Assert(root.GetProperty("animations").GetArrayLength() == 13, "Merged animation count");
            for (int i = 0; i < clips.Length; i++) {
                JsonElement animation = root.GetProperty("animations")[i];
                Assert(animation.GetProperty("name").GetString() == clips[i].Name, "Lost clip name");
                for (int j = 0; j < clips[i].Tracks.Length; j++) {
                    int target = animation.GetProperty("channels")[j].GetProperty("target").GetProperty("node").GetInt32();
                    Assert(root.GetProperty("nodes")[target].GetProperty("name").GetString() == clips[i].Tracks[j].Node, "Wrong animation binding");
                }
            }
        } finally { File.Delete(path); }
    });
    Test("duplicate named sequences are rejected without choosing a pose silently", () => {
        string path = Path.Combine(models, "Npc/02/03/02030005/Attack_Idle_A.kf");
        try { AnimationReader.Read(path, sequenceName: "Attack_Idle_A"); }
        catch (NotSupportedException error) {
            Assert(error.Message.Contains("found 2"), "Wrong ambiguity reason");
            return;
        }
        throw new Exception("Ambiguous sequence accepted");
    });
    Test("null KF root produces a conversion error instead of indexing outside blocks", () => {
        byte[] data = File.ReadAllBytes(rabbitKfm.Clips[0].File);
        Assert(NifDocument.Load(rabbitKfm.Clips[0].File).Roots.Length == 1, "Expected one fixture root");
        Array.Fill(data, (byte) 255, data.Length - 4, 4);
        string path = Path.Combine(Path.GetTempPath(), $"native-null-root-{Guid.NewGuid():N}.kf");
        try {
            File.WriteAllBytes(path, data);
            try { AnimationReader.Read(path); }
            catch (NotSupportedException error) {
                Assert(error.Message.Contains("found 0"), "Wrong missing sequence reason");
                return;
            }
            throw new Exception("Null sequence root accepted");
        } finally { File.Delete(path); }
    });
}
Console.WriteLine($"{tests} tests passed.");
