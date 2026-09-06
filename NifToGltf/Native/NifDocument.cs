using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace NifToGltf.Native;

internal sealed class NifReader(ReadOnlyMemory<byte> data) {
    public int Position { get; private set; }
    public int Remaining => data.Length - Position;
    public ReadOnlyMemory<byte> Take(int count) {
        if (count < 0 || count > Remaining) {
            throw new InvalidDataException($"Read of {count} bytes exceeds block at offset {Position}.");
        }
        ReadOnlyMemory<byte> result = data.Slice(Position, count);
        Position += count;
        return result;
    }
    public byte Byte() => Take(1).Span[0];
    public bool Bool() => Byte() switch {
        0 => false, 1 => true, _ => throw new InvalidDataException("Invalid boolean.")
    };
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2).Span);
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4).Span);
    public int I32() => unchecked((int) U32());
    public int Count(int minimumBytes = 1) {
        uint count = U32();
        if (count > int.MaxValue || count > Remaining / minimumBytes) {
            throw new InvalidDataException($"Invalid array length {count} at {Position - 4}.");
        }
        return (int) count;
    }
    public float Float() {
        float value = BitConverter.Int32BitsToSingle(I32());
        if (!float.IsFinite(value)) {
            throw new InvalidDataException($"Non-finite float at {Position - 4}.");
        }
        return value;
    }
    public Vector3 Vector() => new(Float(), Float(), Float());
    public string SizedString() => Encoding.Latin1.GetString(Take(Count()).Span);
    public int[] Refs() => Enumerable.Range(0, Count(4)).Select(_ => I32()).ToArray();
    public void Finish() {
        if (Remaining != 0) {
            throw new InvalidDataException($"{Remaining} unconsumed bytes at {Position}.");
        }
    }
    public Matrix4x4 Transform(bool translationFirst = false) {
        Vector3 translation = translationFirst ? Vector() : default;
        float[] r = Enumerable.Range(0, 9).Select(_ => Float()).ToArray();
        if (!translationFirst) translation = Vector();
        float scale = Float();
        // NIF bytes are rows of a column-vector matrix. System.Numerics uses row vectors.
        return new Matrix4x4(
            r[0] * scale, r[3] * scale, r[6] * scale, 0,
            r[1] * scale, r[4] * scale, r[7] * scale, 0,
            r[2] * scale, r[5] * scale, r[8] * scale, 0,
            translation.X, translation.Y, translation.Z, 1);
    }
}

internal sealed record NifBlock(string Type, ReadOnlyMemory<byte> Data);
internal sealed record Semantic(string Name, uint Index);
internal sealed record StreamBinding(int Block, bool PerInstance, ushort[] Regions, Semantic[] Semantics);
internal sealed record NifMesh(uint Primitive, int Submeshes, StreamBinding[] Streams, int[] Modifiers);
internal sealed record NifNode(int Id, string Name, ushort Flags, Matrix4x4 Transform, int[] Properties,
    int[] Children, NifMesh? Mesh) {
    public int Controller { get; init; } = -1;
    public int[] ExtraData { get; init; } = [];
    public int[] Effects { get; init; } = [];
    public uint? SortingMode { get; init; }
    public string? MaterialName { get; init; }
}
internal sealed record OmittedContent(int Block, string Type, string Reason);
internal sealed record NifSkin(int Root, Matrix4x4 Transform, int[] Bones, Matrix4x4[] BindTransforms, ushort Flags = 0);
internal sealed record NifMorph(byte Flags, int Targets, Semantic[] Semantics);
internal sealed record StreamRegion(int Start, int Count);

internal sealed class NifStream {
    public required uint[] Formats { get; init; }
    public required StreamRegion[] Regions { get; init; }
    public required ReadOnlyMemory<byte> Data { get; init; }
    public int Stride => Formats.Sum(FormatSize);
    public static int Components(uint format) => checked((int) ((format >> 16) & 255));
    public static int FormatSize(uint format) => Components(format) * checked((int) ((format >> 8) & 255));
    public static NifStream Read(NifReader r) {
        int length = checked((int) r.U32());
        r.U32();
        StreamRegion[] regions = Enumerable.Range(0, r.Count(8))
            .Select(_ => new StreamRegion(checked((int) r.U32()), checked((int) r.U32()))).ToArray();
        uint[] formats = Enumerable.Range(0, r.Count(4)).Select(_ => r.U32()).ToArray();
        NifStream stream = new() { Formats = formats, Regions = regions, Data = r.Take(length) };
        r.Bool();
        r.Finish();
        if (stream.Stride == 0 || length % stream.Stride != 0 ||
            regions.Any(region => (long) region.Start + region.Count > length / stream.Stride)) {
            throw new InvalidDataException("Invalid stream stride or region.");
        }
        return stream;
    }
    public double[] ReadComponent(int component, int regionIndex) {
        if ((uint) component >= Formats.Length || (uint) regionIndex >= Regions.Length) {
            throw new InvalidDataException("Component or region index outside stream.");
        }
        uint format = Formats[component];
        int width = Components(format);
        int offset = Formats.Take(component).Sum(FormatSize);
        StreamRegion region = Regions[regionIndex];
        double[] values = new double[checked(region.Count * width)];
        for (int i = 0; i < region.Count; i++) {
            NifReader r = new(Data.Slice(checked((region.Start + i) * Stride + offset), FormatSize(format)));
            for (int c = 0; c < width; c++) {
                values[i * width + c] = format switch {
                    0x00010215 => r.U16(),
                    0x00010425 => r.U32(),
                    0x00010435 or 0x00020436 or 0x00030437 or 0x00040438 => r.Float(),
                    0x00040108 => r.Byte(),
                    0x00040110 => r.Byte() / 255.0,
                    0x00040214 => unchecked((short) r.U16()),
                    _ => throw new NotSupportedException($"Component format 0x{format:X8} is unsupported.")
                };
            }
        }
        return values;
    }
}

internal sealed class NifDocument {
    public required string Path { get; init; }
    public required string[] Strings { get; init; }
    public required NifBlock[] Blocks { get; init; }
    public required int[] Roots { get; set; }
    public Dictionary<int, NifNode> Nodes { get; } = [];
    public Dictionary<int, NifSkin> GraftedSkins { get; } = [];
    public Dictionary<int, NifSkin> CanonicalSkins { get; } = [];
    public List<OmittedContent> Omitted { get; } = [];
    private readonly Dictionary<int, NifStream> streams = [];
    public string Name(NifReader r) {
        uint index = r.U32();
        return index == uint.MaxValue ? "" : index < Strings.Length ? Strings[index] :
            throw new InvalidDataException($"Invalid string index {index}.");
    }
    public string ObjectNet(NifReader r) {
        string name = Name(r);
        r.Refs();
        r.I32();
        return name;
    }
    public NifReader Reader(int block, string? type = null) {
        if ((uint) block >= Blocks.Length || type is not null && Blocks[block].Type != type) {
            throw new InvalidDataException($"Invalid {type ?? "block"} reference {block}.");
        }
        return new NifReader(Blocks[block].Data);
    }
    public NifStream Stream(int block) {
        if (!streams.TryGetValue(block, out NifStream? stream)) {
            if ((uint) block >= Blocks.Length || Blocks[block].Type.Split('\x01')[0] != "NiDataStream") {
                throw new InvalidDataException($"Block {block} is not a data stream.");
            }
            streams[block] = stream = NifStream.Read(Reader(block));
        }
        return stream;
    }
    public NifSkin Skin(int block) {
        if (GraftedSkins.TryGetValue(block, out NifSkin? grafted)) return grafted;
        if (CanonicalSkins.TryGetValue(block, out NifSkin? canonical)) return canonical;
        NifReader r = Reader(block, "NiSkinningMeshModifier");
        r.Take(r.Count(2) * 2);
        r.Take(r.Count(2) * 2);
        ushort flags = r.U16();
        int root = r.I32();
        Matrix4x4 transform = r.Transform();
        int[] bones = r.Refs();
        Matrix4x4[] binds = bones.Select(_ => r.Transform()).ToArray();
        if ((flags & 2) != 0) r.Take(checked(bones.Length * 16));
        r.Finish();
        if (!Nodes.ContainsKey(root) || bones.Any(bone => !Nodes.ContainsKey(bone))) {
            throw new InvalidDataException($"Skin {block} references a missing bone.");
        }
        return new NifSkin(root, transform, bones, binds, flags);
    }
    public NifMorph Morph(int block) {
        NifReader r = Reader(block, "NiMorphMeshModifier");
        r.Take(r.Count(2) * 2); r.Take(r.Count(2) * 2);
        byte flags = r.Byte();
        int targets = r.U16();
        Semantic[] semantics = Enumerable.Range(0, r.Count(12)).Select(_ => {
            Semantic semantic = new(Name(r), r.U32());
            if (r.U32() != 0) throw new NotSupportedException("Normalized morph semantics require nonlinear normalization.");
            return semantic;
        }).ToArray();
        r.Finish();
        if ((flags & 1) == 0 || (flags & 2) != 0 || targets < 2 || semantics.Length != 1 ||
            semantics[0].Name is not ("POSITION" or "POSITION_BP") || semantics[0].Index != 0) {
            throw new NotSupportedException("Expected relative position morph targets with source normals.");
        }
        return new(flags, targets, semantics);
    }
    public static NifDocument Load(string path) {
        byte[] data = File.ReadAllBytes(path);
        int line = Array.IndexOf(data, (byte) '\n');
        string header = line < 0 ? "" : Encoding.ASCII.GetString(data, 0, line);
        bool olderAnimation = header == "Gamebryo File Format, Version 30.1.0.3" && System.IO.Path.GetExtension(path).Equals(".kf", StringComparison.OrdinalIgnoreCase);
        if (header != "Gamebryo File Format, Version 30.2.0.3" && !olderAnimation) {
            throw new NotSupportedException($"{path}: expected NIF 30.2.0.3 or KF 30.1.0.3.");
        }
        NifReader r = new(data);
        r.Take(line + 1);
        if (r.U32() != (olderAnimation ? 0x1E010003u : 0x1E020003u) || r.Byte() != 1) throw new NotSupportedException("Unsupported NIF version/endian.");
        r.U32();
        int blockCount = r.Count();
        r.Take(r.Count());
        string[] types = Enumerable.Range(0, r.U16()).Select(_ => r.SizedString()).ToArray();
        int[] indices = Enumerable.Range(0, blockCount).Select(_ => r.U16() & 0x7FFF).ToArray();
        int[] sizes = Enumerable.Range(0, blockCount).Select(_ => checked((int) r.U32())).ToArray();
        int stringCount = r.Count();
        uint maxLength = r.U32();
        string[] strings = Enumerable.Range(0, stringCount).Select(_ => r.SizedString()).ToArray();
        if (strings.Any(value => value.Length > maxLength)) throw new InvalidDataException("Invalid maximum string length.");
        r.Take(r.Count(4) * 4);
        NifBlock[] blocks = indices.Select((type, i) => new NifBlock(
            type < types.Length ? types[type] : throw new InvalidDataException("Invalid block type."), r.Take(sizes[i]))).ToArray();
        int[] roots = r.Refs();
        r.Finish();
        NifDocument document = new() { Path = System.IO.Path.GetFullPath(path), Strings = strings, Blocks = blocks, Roots = roots };
        for (int i = 0; i < blocks.Length; i++) {
            if (blocks[i].Type is not ("NiNode" or "NiMesh" or "NiSortAdjustNode")) continue;
            try {
                document.Nodes[i] = document.ReadNode(i);
            } catch (Exception e) when (e is InvalidDataException or OverflowException or NotSupportedException) {
                throw new InvalidDataException($"{path}: block {i} ({blocks[i].Type}): {e.Message}", e);
            }
        }
        Dictionary<int, int> parents = [];
        foreach (NifNode node in document.Nodes.Values) {
            foreach (int child in node.Children) {
                if (child >= blocks.Length || !parents.TryAdd(child, node.Id)) throw new InvalidDataException("Invalid child reference or multiple parents.");
            }
        }
        foreach (int id in document.Nodes.Keys) {
            HashSet<int> ancestry = [id];
            int current = id;
            while (parents.TryGetValue(current, out current)) {
                if (!ancestry.Add(current)) throw new InvalidDataException("Cycle in NIF node hierarchy.");
            }
        }
        if (roots.Any(root => root < -1 || root >= blocks.Length)) throw new InvalidDataException("Invalid NIF root reference.");
        return document;
    }
    private NifNode ReadNode(int id) {
        NifReader r = Reader(id);
        string name = Name(r);
        int[] extras = r.Refs();
        int controller = r.I32();
        ushort flags = r.U16();
        Matrix4x4 transform = r.Transform(true);
        int[] properties = r.Refs();
        r.I32(); // Collision object does not affect static geometry.
        int[] children = [];
        NifMesh? mesh = null;
        string? materialName = null;
        uint? sortingMode = null;
        int[] effects = [];
        if (Blocks[id].Type is "NiNode" or "NiSortAdjustNode") {
            children = r.Refs().Where(child => child >= 0).ToArray();
            effects = r.Refs();
            if (Blocks[id].Type == "NiSortAdjustNode") sortingMode = r.U32();
        } else {
            int materials = r.Count(8);
            string[] materialNames = Enumerable.Range(0, materials).Select(_ => Name(r)).ToArray();
            r.Take(materials * 4);
            int activeMaterial = r.I32();
            if (materials > 0) materialName = materialNames[(uint) activeMaterial < materials ? activeMaterial : 0];
            r.Bool();
            uint primitive = r.U32();
            int submeshes = r.U16();
            if (r.Bool()) throw new NotSupportedException("Instanced mesh.");
            r.Take(16);
            StreamBinding[] bindings = Enumerable.Range(0, r.Count()).Select(_ => {
                int block = r.I32();
                bool perInstance = r.Bool();
                ushort[] regions = Enumerable.Range(0, r.U16()).Select(_ => r.U16()).ToArray();
                Semantic[] semantics = Enumerable.Range(0, r.Count(8)).Select(_ => new Semantic(Name(r), r.U32())).ToArray();
                return new StreamBinding(block, perInstance, regions, semantics);
            }).ToArray();
            mesh = new NifMesh(primitive, submeshes, bindings, r.Refs());
        }
        r.Finish();
        return new NifNode(id, name, flags, transform, properties, children, mesh) {
            ExtraData = extras, Controller = controller, SortingMode = sortingMode, Effects = effects, MaterialName = materialName
        };
    }

    public void OmitParticles() {
        HashSet<int> removed = [];
        for (int i = 0; i < Blocks.Length; i++) {
            string type = Blocks[i].Type;
            if (type is "NiPSParticleSystem" or "NiPSMeshParticleSystem") {
                removed.Add(i);
                if (!Omitted.Any(entry => entry.Block == i)) Omitted.Add(new(i, type, "Particle system and its simulation are outside the non-effect scope."));
            }
        }
        foreach (NifNode node in Nodes.Values.ToArray()) Nodes[node.Id] = node with { Children = node.Children.Where(id => !removed.Contains(id)).ToArray() };
        Roots = Roots.Where(id => !removed.Contains(id)).ToArray();
        // NiPhysXProp is a non-scene resource stored alongside the scene root, not a scene wrapper.
        foreach (int root in Roots.Where(id => (uint) id < Blocks.Length && Blocks[id].Type == "NiPhysXProp").ToArray()) {
            Omitted.Add(new(root, "NiPhysXProp", "Physics resource omitted from static scene; referenced mesh geometry is retained."));
            Roots = Roots.Where(id => id != root).ToArray();
        }
    }
}
