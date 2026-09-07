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
Test("missing selected KF remains a missing asset instead of a format failure", () => {
    string missing = Path.Combine(Path.GetTempPath(), $"missing-native-{Guid.NewGuid():N}.kf");
    KfmDocument kfm = new("model.nif", "Scene Root", [new(1, missing, "Idle_A")]);
    try { ClipSelection.Read("model.nif", kfm, null, "Idle_A"); }
    catch (FileNotFoundException error) {
        Assert(error.Message.Contains("no glTF written"), "Missing clip was not reported");
        return;
    }
    throw new Exception("Missing selected KF accepted");
});
Test("an authored empty KFM is static while a requested missing clip remains an error", () => {
    string source = "Maple2Storage/Resources/WardrobeSources/Item/0/02/10200074_m_warriorhair_p_a.kfm";
    KfmDocument kfm = File.Exists(source) ? KfmDocument.Read(source) : new("model.nif", "Scene Root", []);
    Assert(kfm.Clips.Length == 0, "Source declares animations");
    Assert(ClipSelection.Read("model.nif", kfm, null, "all").Count == 0, "Static source rejected");
    try { ClipSelection.Read("model.nif", kfm, null, "Idle_A"); }
    catch (FileNotFoundException) { return; }
    throw new Exception("Missing explicit clip accepted");
});
Test("itemmodel attachment selection respects asset identity and body variant", () => {
    string path = Path.Combine(Path.GetTempPath(), $"native-itemmodel-{Guid.NewGuid():N}.xml");
    try {
        File.WriteAllText(path, """
            <ms2><ItemModel id="11800001"><slots><slot name="MT">
              <asset name="urn:cape_m" selfnode="MT_Point01" targetnode="Bip01 Spine1" gender="0" />
              <asset name="Data/Resource/Model/Item/cape_f.nif" selfnode="MT_Point02" targetnode="Bip01 Spine2" gender="1" />
            </slot></slots><cutting><mesh name="CL_Collar" /><mesh name="PA_Belt" gender="1" /></cutting></ItemModel></ms2>
            """);
        ItemModelAttachment male = ItemModelAttachment.Read(path, "11800001", "cape_m.nif", "male");
        ItemModelAttachment female = ItemModelAttachment.Read(path, "11800001", "cape_f.nif", "female");
        Assert(male.TargetNode == "Bip01 Spine1" && female.TargetNode == "Bip01 Spine2", "Gender selection changed attachment");
        Assert(male.Cutting.SequenceEqual(new[] { "CL_Collar" }), "Lost explicit cutting rule");
        Assert(female.Cutting.SequenceEqual(new[] { "CL_Collar", "PA_Belt" }), "Gender-specific cutting was discarded");
        Reject(() => ItemModelAttachment.Read(path, "11800001", "cape_m.nif", "female"));
    } finally { File.Delete(path); }
});
Test("shared KFM geometry retains separate XML attachment identities", () => {
    string directory = Path.Combine(Path.GetTempPath(), $"native-kfm-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try {
        File.WriteAllBytes(Path.Combine(directory, "tail.nif"), []);
        File.WriteAllBytes(Path.Combine(directory, "tail.kf"), []);
        byte[] kfm = Bytes(w => {
            void String(string value) { byte[] data = System.Text.Encoding.ASCII.GetBytes(value); w.Write(data.Length); w.Write(data); }
            w.Write(System.Text.Encoding.ASCII.GetBytes(";Gamebryo KFM File Version 30.2.0.3b\n")); w.Write((byte) 1);
            String(".\\TAIL.nif"); String("Point01");
            w.Write(0); w.Write(0); w.Write(0f); w.Write(0f); w.Write(1);
            w.Write(1); String(".\\TAIL.kf"); String("TailIdle"); w.Write(0); w.Write(0);
        });
        string xml = Path.Combine(directory, "item.xml");
        File.WriteAllText(xml, """
            <ms2><ItemModel id="10"><slots><slot name="HR">
              <asset name="urn:tail" selfnode="Point01" targetnode="First" gender="1" />
              <asset name="urn:tail2" selfnode="Point01" targetnode="Second" gender="1" />
            </slot></slots></ItemModel></ms2>
            """);
        foreach (string name in new[] { "tail", "tail2" }) File.WriteAllBytes(Path.Combine(directory, name + ".kfm"), kfm);
        string first = Path.Combine(directory, "tail.kfm"), second = Path.Combine(directory, "tail2.kfm");
        KfmDocument a = KfmDocument.Read(first), b = KfmDocument.Read(second);
        Assert(a.Model == b.Model && a.Clips.SequenceEqual(b.Clips), "Explicit shared references changed");
        Assert(ItemModelAttachment.Read(xml, "10", first, "female", "HR").TargetNode == "First", "First identity lost");
        Assert(ItemModelAttachment.Read(xml, "10", second, "female", "HR").TargetNode == "Second", "Second identity lost");
    } finally {
        foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
});
Test("only absent posed accumulation targets can remain unbound", () => {
    AnimationTrack track = new("Point01 NonAccum", "translation", [0, 1], [0, 0, 0, 0, 0, 0], 3, "LINEAR") { Posed = true };
    AnimationClip clip = new("tail", [track]) { AccumulationRoot = "Point01" };
    Dictionary<string, int> targets = new() { ["Point01"] = 1 };
    Assert(AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, targets), "Missing posed target rejected");
    Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track with { Posed = false }, targets), "Animated target silently dropped");
    Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track with { Node = "Other NonAccum" }, targets), "Unrelated target dropped");
    Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip with { AccumulationRoot = null }, track, targets), "Guessed accumulation root");
    Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, new Dictionary<string, int>()), "Absent root accepted");
    targets["Point01"] = 2;
    Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, targets), "Ambiguous root accepted");
    targets["Point01"] = 1;
    foreach (int count in new[] { 1, 2 }) {
        targets[track.Node] = count;
        Assert(!AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, targets), "Existing target dropped");
    }
});
string sassySource = "NifToGltf/obj/hair-investigation/source/0/02/00200010_f_pipi_p_a.nif";
if (File.Exists(sassySource)) Test("Sassy source retains Point01 channels and leaves only its absent posed NonAccum unbound", () => {
    NifDocument source = NifDocument.Load(sassySource);
    AnimationClip clip = AnimationReader.Read(Path.ChangeExtension(sassySource, ".kf"));
    Assert(clip.AccumulationRoot == "Point01", "Wrong accumulation root");
    Assert(clip.Tracks.Length == 6 && clip.Tracks.All(track => track.Posed), "Unexpected source animation data");
    var counts = source.Nodes.Values.GroupBy(node => node.Name).ToDictionary(group => group.Key, group => group.Count());
    AnimationTrack[] unbound = clip.Tracks.Where(track => AnimationBinding.IsUnboundPosedAccumulationTrack(clip, track, counts)).ToArray();
    Assert(unbound.Length == 3 && unbound.All(track => track.Node == "Point01 NonAccum"), "Incorrect unbound tracks");
    Assert(clip.Tracks.Except(unbound).All(track => track.Node == "Point01" && counts[track.Node] == 1), "Root tracks lost");
});
else Console.WriteLine("SKIP local Sassy source binding regression: existing extraction is required.");
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
Test("triangle strips alternate winding and keep parity across degenerates", () => {
    Assert(MeshDecoder.TriangulateStrip([0, 1, 2, 3, 3, 4, 5]).SequenceEqual(new uint[] { 0, 1, 2, 2, 1, 3, 3, 4, 5 }), "Strip winding");
});
Test("color baking preserves control detail above the diffuse resolution", () => {
    TexturePixels diffuse = new(1, 1, [255, 255, 255, 128]);
    TexturePixels control = new(2, 1, [255, 0, 0, 255, 0, 0, 0, 255]);
    TexturePixels baked = MaterialColors.Bake(diffuse, control, [Vector3.UnitX, Vector3.Zero, Vector3.UnitZ]);
    Assert(baked.Width == 2 && baked.Height == 1, "Control detail was downsampled");
    Assert(baked.Rgba.SequenceEqual(new byte[] { 255, 0, 0, 128, 0, 0, 255, 128 }), "Color/alpha samples differ");
    Near(MaterialColors.Sample(control, 0, 0.5, wrapS: true).X, 0.5);
    Near(MaterialColors.Sample(control, 0, 0.5, wrapS: false).X, 1);
});
Test("RGB textures respect channel masks, alpha and padded rows", () => {
    byte[] rgba = DdsTexture.DecodeRgb([0, 0, 255, 99, 255, 0, 0, 99], 1, 2, 24, 4, [0xff0000, 0xff00, 0xff, 0]);
    Assert(rgba.SequenceEqual(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }), "BGR row decoding");
    rgba = DdsTexture.DecodeRgb([0, 248], 1, 1, 16, 2, [0xf800, 0x7e0, 0x1f, 0]);
    Assert(rgba.SequenceEqual(new byte[] { 255, 0, 0, 255 }), "RGB565 decoding");
    rgba = DdsTexture.DecodeRgb([1, 2, 3, 4], 1, 1, 32, 4, [255, 65280, 16711680, 4278190080]);
    Assert(rgba.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "RGBA decoding");
    Reject(() => DdsTexture.DecodeRgb([0], 1, 1, 24, 3, [255, 65280, 16711680, 0]));
});
Test("DXT1 transparent selectors and partial blocks preserve pixels", () => {
    byte[] rgba = DdsTexture.DecodeCompressed([0, 0, 255, 255, 255, 255, 255, 255], 1, 1, "DXT1");
    Assert(rgba.SequenceEqual(new byte[4]), "DXT1 alpha");
});
Test("Max UV transforms apply centered translation, rotation then anisotropic scale", () => {
    Matrix3x2 m = TextureTransform.Create(new(0.25f, -0.25f), new(2, 3), MathF.PI / 2, 1, new(0.5f, 0.5f));
    Vector2 p = Vector2.Transform(new(1, 1), m);
    Near(p.X, 0); Near(p.Y, 2.75);
    m = TextureTransform.Create(Vector2.Zero, new(0.5f, 0.5f), 0, 1, new(0.5f, 0.5f));
    p = Vector2.Transform(Vector2.Zero, m);
    Near(p.X, 0.25); Near(p.Y, 0.25);
});
Test("tangent generation follows UV derivatives and mirrored handedness", () => {
    Dictionary<string, MeshAttribute> a = new() {
        ["POSITION"] = new(3, [0,0,0, 1,0,0, 0,1,0]),
        ["NORMAL"] = new(3, [0,0,1, 0,0,1, 0,0,1]),
        ["TEXCOORD_0"] = new(2, [0,0, 1,0, 0,1])
    };
    MeshDecoder.GenerateTangents(a, [0,1,2], 0);
    Near(a["TANGENT"].Values[0], 1); Near(a["TANGENT"].Values[3], 1);
    a["TEXCOORD_0"] = new(2, [0,0, -1,0, 0,1]);
    MeshDecoder.GenerateTangents(a, [0,1,2], 0);
    Near(a["TANGENT"].Values[0], -1); Near(a["TANGENT"].Values[3], -1);
});
Test("client color override uses red, green and alpha while preserving opacity", () => {
    Vector3 result = MaterialColors.Override(new(0.2f, 0.4f, 0.6f), new(0.25f, 0.1f, 0.9f, 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
    Near(result.X, 0.225); Near(result.Y, 0.25); Near(result.Z, 0.675);
    TexturePixels pixels = MaterialColors.Bake(new(1, 1, [20, 40, 60, 70]), new(1, 1, [255, 0, 0, 255]), [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ]);
    Assert(pixels.Rgba.SequenceEqual(new byte[] { 255, 0, 0, 70 }), "Override alpha changed base opacity");
});
Test("batch paths reject output traversal", () => {
    string root = Path.GetTempPath();
    Assert(NativeBatch.Resolve(root, "models/a.gltf").StartsWith(root), "Relative path");
    foreach (string path in new[] { "../a.gltf", "/a.gltf", "C:/a.gltf" }) {
        bool rejected = false;
        try { NativeBatch.Resolve(root, path); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, $"Accepted unsafe path {path}");
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
            JsonElement faceLighting = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "FA").GetProperty("extras").GetProperty("nifLighting");
            Assert(!faceLighting.GetProperty("specularEnabled").GetBoolean(), "Face must not acquire specular without its source enable property");
            JsonElement skinLighting = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "CL_Skin").GetProperty("extras").GetProperty("nifLighting");
            Assert(skinLighting.GetProperty("specularEnabled").GetBoolean(), "Lost source skin specular property");
            Near(skinLighting.GetProperty("power").GetDouble(), 120);
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
        gear.OmitParticles(); // Synthetic skeleton roots do not index the source block array.
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
    Test("hair morph preserves the absolute base, offset target and default weight", () => {
        NifDocument hair = NifDocument.Load(Path.Combine(models, "Item/0/02/10200230_m_hair_a.nif"));
        NifNode node = hair.Nodes.Values.Single(node => node.Mesh is not null);
        DecodedPrimitive primitive = MeshDecoder.Decode(hair, node, 0);
        Assert(primitive.MorphWeights.SequenceEqual(new double[] { 0 }), "Unexpected default morph weight");
        NifMesh mesh = node.Mesh!;
        double[] Source(uint index) {
            StreamBinding binding = mesh.Streams.Single(stream => stream.Semantics.Any(semantic => semantic.Name == "MORPH_POSITION" && semantic.Index == index));
            int component = Array.FindIndex(binding.Semantics, semantic => semantic.Name == "MORPH_POSITION" && semantic.Index == index);
            return hair.Stream(binding.Block).ReadComponent(component, binding.Regions[0]);
        }
        double[] basis = Source(0), offset = Source(1);
        for (int i = 0; i < basis.Length; i++) {
            Near(primitive.Attributes["POSITION"].Values[i], basis[i]);
            Near(primitive.Attributes["POSITION"].Values[i] + 0.7 * primitive.MorphPositions[0].Values[i], basis[i] + 0.7 * offset[i]);
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
    string hoodiePath = Path.Combine(Path.GetDirectoryName(models)!, "NativeSources/Clothing-02/1/14/11400158_f_hoodtshirts.nif");
    if (File.Exists(hoodiePath)) Test("CL replacement retains the garment and sibling exposed-skin mesh", () => {
        NifDocument hoodie = NifDocument.Load(hoodiePath);
        Dictionary<string, double[]> original = hoodie.Nodes.Values.Where(node => node.Mesh is not null)
            .ToDictionary(node => node.Name, node => MeshDecoder.Decode(hoodie, node, 0).Attributes["POSITION"].Values);
        Assert(original.Keys.Order().SequenceEqual(new[] { "CL", "CL_Skin" }), "Unexpected hoodie source parts");
        SkeletonGraft.Apply(hoodie, NifDocument.Load(Path.Combine(models, "Character/female/f_body.nif")), "CL", "CL", true);
        NifNode[] meshes = hoodie.Nodes.Values.Where(node => node.Mesh is not null).ToArray();
        Assert(meshes.Select(node => node.Name).Order().SequenceEqual(original.Keys.Order()), "Discarded clothing part");
        Assert(!hoodie.Omitted.Any(item => item.Type == "NiMesh"), "Reported a clothing part as omitted");
        foreach (NifNode mesh in meshes) Assert(MeshDecoder.Decode(hoodie, mesh, 0).Attributes["POSITION"].Values.SequenceEqual(original[mesh.Name]), "Changed clothing geometry");
    });
    else Console.WriteLine("SKIP hoodie source regression: extract the Clothing-02 acceptance source first.");
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
        string path = Path.Combine(models, "Npc/02/03/02030005/attack_idle_a.kf");
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
if (args.Length > 0) {
    string resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!, "../../.."));
    string neonPath = Path.Combine(resources, "GeloSources/Item/1/18/11850281_c_mtvalentine04.nif");
    if (File.Exists(neonPath)) Test("private neon skeleton retains sibling skinned geometry and source bind matrices", () => {
        NifDocument neon = NifDocument.Load(neonPath);
        NifNode mesh = neon.Nodes.Values.Single(node => node.Name == "MT");
        DecodedPrimitive before = MeshDecoder.Decode(neon, mesh, 0);
        SkeletonGraft.Apply(neon, NifDocument.Load(args[0]), "Scene Root", "MT_Point01");
        DecodedPrimitive after = MeshDecoder.Decode(neon, neon.Nodes[mesh.Id], 0);
        Assert(after.Attributes["POSITION"].Values.SequenceEqual(before.Attributes["POSITION"].Values), "Changed sign geometry");
        Assert(after.Skin!.BindTransforms.SequenceEqual(before.Skin!.BindTransforms), "Changed private bind matrices");
        Assert(after.Skin.Bones.All(neon.EquipmentBones.Contains), "Lost private joints");
        int root = neon.Nodes.Values.Single(node => node.Name == "MT_Point01").Id;
        NifNode parent = neon.Nodes.Values.Single(node => node.Children.Contains(root));
        Assert(parent.Name == "Scene Root" && parent.Id >= neon.Blocks.Length, "Wrong declared attachment");
        Assert(neon.Nodes.Values.Count(node => node.Mesh is not null) == 3, "Discarded rigid wing meshes or main sign");
    });
    string itemXml = Path.Combine(resources, "SimulatorSources/Xml/itemmodel/134.xml");
    if (File.Exists(itemXml)) Test("weapon dummy transforms use the selected body's client offsets", () => {
        ItemModelAttachment female = ItemModelAttachment.Read(itemXml, "13400263", "13400263_FirePrismStar.nif", "female");
        ItemModelAttachment male = ItemModelAttachment.Read(itemXml, "13400263", "13400263_FirePrismStar.nif", "male");
        Assert(female.Slot == "OH" && female.TargetNode == "Weapon_Back_B_Point", "Wrong source attachment");
        Near(Vector3.Transform(Vector3.Zero, female.DummyTransform).X, -6);
        Near(Vector3.Transform(Vector3.Zero, male.DummyTransform).X, -4);
        Near(Vector3.Transform(Vector3.Zero, female.DummyTransform).Y, 6);
    });
    string knuckleXml = Path.Combine(resources, "SimulatorSources/Xml/itemmodel/155.xml");
    if (File.Exists(knuckleXml)) Test("paired knuckles distinguish drawn attachnodes from back dummy transforms", () => {
        foreach (string variant in new[] { "female", "male" })
            foreach (string slot in new[] { "RH", "LH" }) {
                var attachment = ItemModelAttachment.Read(knuckleXml, "15500002", "15500002_Knuckle_001_B.nif", variant, slot);
                Assert(attachment.TargetNode == "Weapon_Back_B_Point", "Lost stowed target");
                Assert(attachment.AttachNode == (slot == "RH" ? "Weapon_Hand_R_Point" : "Weapon_Hand_L_Point"), "Wrong drawn hand");
                Assert(attachment.Rotation!.SequenceEqual(new float[] { 77, 90, 127 }), "Lost source dummy");
            }
    });
    string signClip = Path.Combine(resources, "SimulatorMotion/Item/1/18/11850281_c_mtvalentine04_idle_a.kf");
    if (File.Exists(signClip)) Test("sign source clip retains the four-second heart orbit and private wing tracks", () => {
        var clip = AnimationReader.Read(signClip);
        var heart = clip.Tracks.Single(t => t.Node == "MT_Heart" && t.Path == "translation");
        Near(heart.Times.Last(), 4);
        Near(heart.Values[0], 49.3990936, .001);
        Assert(heart.Values.Where((_, i) => i % 3 == 0).Min() < -40, "Heart orbit frozen");
        Assert(clip.Tracks.Any(t => t.Node == "M_Wing_L" && t.Path == "rotation") && clip.Tracks.Any(t => t.Node == "M_Wing_R" && t.Path == "rotation"), "Lost wing animation");
    });
}
foreach (var (variant, filename) in new[] { ("male", "11600426_m_glfrillband.nif"), ("female", "11600254_f_glsaunakey.nif") }) {
    string source = Path.Combine("Maple2Storage/Resources/WardrobeSources/Item/1/16", filename);
    if (!File.Exists(source)) continue;
    Test($"{variant} wrist slot retains GL_Wrist and GL_Skin without changing source positions", () => {
        NifDocument gear = NifDocument.Load(source);
        var before = gear.Nodes.Values.Where(n => n.Mesh is not null && n.Name.StartsWith("GL_", StringComparison.Ordinal))
            .ToDictionary(n => n.Name, n => MeshDecoder.Decode(gear, n, 0).Attributes["POSITION"].Values);
        Assert(before.Keys.Order().SequenceEqual(new[] { "GL_Skin", "GL_Wrist" }), "Unexpected source wrist family");
        NifDocument body = NifDocument.Load($"Maple2Storage/Resources/Models/Character/{variant}/{variant[0]}_body.nif");
        SkeletonGraft.Apply(gear, body, "GL", "GL", true);
        var meshes = gear.Nodes.Values.Where(n => n.Mesh is not null).ToArray();
        Assert(meshes.Length == 2, "Lost wrist geometry or retained another equipment slot");
        foreach (var mesh in meshes) Assert(MeshDecoder.Decode(gear, mesh, 0).Attributes["POSITION"].Values.SequenceEqual(before[mesh.Name]), "Changed source wrist positions");
    });
}
string rootHat = "Maple2Storage/Resources/WardrobeSources/Item/1/13/11300506_c_cpdancingsnake_f.nif";
if (File.Exists(rootHat)) Test("animated Scene Root attachment becomes one head child with all source geometry", () => {
    NifDocument gear = NifDocument.Load(rootHat);
    int root = gear.Roots.Single();
    var meshes = gear.Nodes.Values.Where(n => n.Mesh is not null).Select(n => n.Id).ToArray();
    var sourceTransform = gear.Nodes[root].Transform;
    NifDocument body = NifDocument.Load("Maple2Storage/Resources/Models/Character/female/f_body.nif");
    SkeletonGraft.Apply(gear, body, "Bip01 Head", "Scene Root", false);
    Assert(!gear.Roots.Contains(root), "Attachment still occurs as a second scene root");
    Assert(gear.Nodes.Values.Single(n => n.Children.Contains(root)).Name == "Bip01 Head", "Not attached to source head target");
    Assert(gear.Nodes[root].Transform == sourceTransform, "Changed source root transform");
    Assert(gear.Nodes.Values.Where(n => n.Mesh is not null).Select(n => n.Id).SequenceEqual(meshes), "Lost source hat geometry");
    Assert(gear.EquipmentBones.Contains(root), "Root is not retained as a private animation bone");
});
string embeddedSnake = "Maple2Storage/Resources/WardrobeSources/Item/1/13/11300506_c_cpdancingsnake_f.nif";
if (File.Exists(embeddedSnake)) Test("embedded snake controllers preserve their authored 4/3-second joint motion", () => {
    NifDocument gear = NifDocument.Load(embeddedSnake);
    SkeletonGraft.Apply(gear, NifDocument.Load("Maple2Storage/Resources/Models/Character/female/f_body.nif"), "Bip01 Head", "Scene Root");
    AnimationClip clip = EmbeddedAnimation.Read(gear) ?? throw new Exception("Embedded animation was frozen");
    Assert(clip.Tracks.Any(t => t.Node == "Bone06" && t.Path == "rotation"), "Lost keyed private joint");
    foreach (AnimationTrack track in clip.Tracks) Near(track.Times.Last(), 4.0 / 3, 1e-6);
    AnimationTrack motion = clip.Tracks.Single(t => t.Node == "Bone06" && t.Path == "rotation");
    Assert(motion.Values.Where((_, i) => i % 4 == 0).Max() - motion.Values.Where((_, i) => i % 4 == 0).Min() > .01, "Snake joint stopped moving");
});
string dinosaur = "Maple2Storage/Resources/WardrobeSources/Item/1/18/11800133_c_mtdoll_dinosaur01.nif";
if (File.Exists(dinosaur)) Test("rigid dinosaur tail reserves body animation names for its deforming skeleton", () => {
    NifDocument gear = NifDocument.Load(dinosaur);
    SkeletonGraft.Apply(gear, NifDocument.Load("Maple2Storage/Resources/Models/Character/female/f_body.nif"), "Bip01 Pelvis", "MT_Point01");
    foreach (string name in new[] { "Bip01", "Bip01 Pelvis", "Bip01 Head" })
        Assert(gear.Nodes.Values.Count(n => n.Name == name) == 1, "Duplicate body animation target: " + name);
});
string morphHair = "Maple2Storage/Resources/WardrobeSources/Item/0/02/00200003_m_coolguy_a.nif";
if (File.Exists(morphHair)) Test("authored hair-length morph controls remain interactive instead of becoming an idle clip", () => {
    NifDocument gear = NifDocument.Load(morphHair);
    Assert(gear.Nodes.Values.Where(n => n.Mesh is not null).SelectMany(n => n.Mesh!.Modifiers).Any(id => gear.Blocks[id].Type == "NiMorphMeshModifier"), "Missing source morph targets");
    Assert(EmbeddedAnimation.Read(gear) is null, "Hair length was treated as time animation");
});
Test("single-axis attachment degrees rotate before source translation without scaling", () => {
    var attachment = new ItemModelAttachment("LH", "Point01", "Weapon_Back_B_Point", false, []) {
        Rotation = [32, 0, 0], Translation = [-6, 8, 1]
    };
    Vector3 origin = Vector3.Transform(Vector3.Zero, attachment.DummyTransform);
    Vector3 y = Vector3.Transform(Vector3.UnitY, attachment.DummyTransform) - origin;
    Near(origin.X, -6); Near(origin.Y, 8); Near(origin.Z, 1);
    Near(y.Y, Math.Cos(32 * Math.PI / 180)); Near(y.Z, Math.Sin(32 * Math.PI / 180)); Near(y.Length(), 1);
    bool rejected = false;
    try { _ = (attachment with { Rotation = [7, 3, -8] }).DummyTransform; } catch (NotSupportedException) { rejected = true; }
    Assert(rejected, "Unverified multi-axis order was accepted");
});
string privateHair = "Maple2Storage/Resources/WardrobeSources/Item/0/02/10200183_m_freeconcept01_c.nif";
if (File.Exists(privateHair)) Test("private C-form hair retains its joint branch at the HR replacement parent", () => {
    NifDocument gear = NifDocument.Load(privateHair);
    gear.OmitParticles();
    Matrix4x4 World(NifDocument d, int id) {
        NifNode? parent = d.Nodes.Values.SingleOrDefault(n => n.Children.Contains(id));
        return d.Nodes[id].Transform * (parent is null ? Matrix4x4.Identity : World(d, parent.Id));
    }
    var sourceWorlds = gear.Nodes.Values.Where(n => n.Mesh is null).ToDictionary(n => n.Id, n => World(gear, n.Id));
    SkeletonGraft.Apply(gear, NifDocument.Load("Maple2Storage/Resources/Models/Character/male/m_body.nif"), "HR", "HR_Point01", true);
    NifNode root = gear.Nodes.Values.Single(n => n.Name == "HR_Point01");
    Assert(gear.EquipmentBones.Contains(root.Id), "Private hair joint was discarded");
    NifNode bridge = gear.Nodes.Values.Single(n => n.Children.Contains(root.Id));
    Assert(bridge.Name == "Equipment hair bind parent", "Missing source-space compensation");
    Assert(gear.Nodes.Values.Single(n => n.Children.Contains(bridge.Id)).Name == "Bip01 Head", "Wrong hair replacement parent");
    foreach (int id in gear.EquipmentBones.Where(sourceWorlds.ContainsKey)) {
        Matrix4x4 expected = sourceWorlds[id], actual = World(gear, id);
        foreach (Vector3 point in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            Assert(Vector3.Distance(Vector3.Transform(point, expected), Vector3.Transform(point, actual)) < .001f, "Private hair bind pose moved");
    }
    Assert(gear.Nodes.Values.Any(n => n.Mesh is not null && n.Name == "HR"), "Hair geometry discarded");
});
string namedHairMesh = "Maple2Storage/Resources/WardrobeSources/Item/0/02/10200039_m_toben_d.nif";
if (File.Exists(namedHairMesh)) Test("HR replacement retains a source mesh named HR colon zero", () => {
    NifDocument gear = NifDocument.Load(namedHairMesh);
    gear.OmitParticles();
    NifNode mesh = gear.Nodes.Values.Single(n => n.Mesh is not null);
    double[] positions = MeshDecoder.Decode(gear, mesh, 0).Attributes["POSITION"].Values;
    SkeletonGraft.Apply(gear, NifDocument.Load("Maple2Storage/Resources/Models/Character/male/m_body.nif"), "HR", "HR", true);
    Assert(gear.Nodes.Values.Count(n => n.Mesh is not null) == 1, "Lost or added hair geometry");
    Assert(MeshDecoder.Decode(gear, gear.Nodes[mesh.Id], 0).Attributes["POSITION"].Values.SequenceEqual(positions), "Changed hair positions");
});
Console.WriteLine($"{tests} tests passed.");
