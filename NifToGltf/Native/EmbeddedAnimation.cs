namespace NifToGltf.Native;

// Ordinary looping NiTransformController chains embedded in an equipped NIF.
// Other active controllers fail visibly instead of becoming a frozen preview.
internal static class EmbeddedAnimation {
    public static AnimationClip? Read(NifDocument document) {
        List<AnimationTrack> tracks = [];
        double? period = null;
        HashSet<int> visited = [];
        foreach (NifNode node in document.Nodes.Values.Where(n => n.Mesh is not null || document.EquipmentBones.Contains(n.Id))) {
            ReadChain(node.Controller, node);
            foreach (int property in node.Properties) {
                NifReader reader = document.Reader(property);
                document.Name(reader); reader.Refs();
                ReadChain(reader.I32(), node, true);
            }
        }
        return tracks.Count == 0 ? null : new AnimationClip("Embedded_Idle", tracks.ToArray());

        void ReadChain(int controller, NifNode node, bool property = false) {
            while (controller >= 0 && visited.Add(controller)) {
                NifReader reader = document.Reader(controller);
                string type = document.Blocks[controller].Type;
                int next = reader.I32();
                ushort flags = reader.U16();
                double frequency = reader.Float(), phase = reader.Float(), start = reader.Float(), stop = reader.Float();
                int target = reader.I32();
                controller = next;
                if ((flags & 8) == 0) continue;
                // Player hair length is an authored, clamped morph control. Its
                // targets and default weights are exported by MeshDecoder and
                // selected through the simulator's hair-length controls.
                if (type == "NiMorphWeightsController" && flags == 76 && start == 0 && stop <= .0001 &&
                    node.Mesh?.Modifiers.Any(id => (uint) id < document.Blocks.Length && document.Blocks[id].Type == "NiMorphMeshModifier") == true) continue;
                if (type != "NiTransformController" || property) {
                    if (stop > start) throw new NotSupportedException($"Active embedded {type} on {node.Name} requires controller support; animation was not frozen.");
                    continue;
                }
                int interpolator = reader.I32(); reader.Finish();
                if (interpolator < 0) continue;
                NifReader source = document.Reader(interpolator, "NiTransformInterpolator");
                double[][] pose = [Values(source, 3), Values(source, 4), [source.Float()]];
                int data = source.I32(); source.Finish();
                if (data < 0) continue; // Explicitly constant interpolator, no keyed animation.
                AnimationCurve?[] curves = AnimationReader.KeyData(document.Reader(data, "NiTransformData"));
                if (curves.All(c => c is null)) continue;
                if (target != node.Id) throw new InvalidDataException("Embedded controller target does not match its declared node.");
                if (!document.EquipmentBones.Contains(node.Id)) throw new NotSupportedException($"Embedded transform on {node.Name} requires a preserved private joint; animation was not frozen.");
                if (start != 0 || phase != 0 || frequency <= 0 || stop <= start || stop > 3600 || ((flags >> 1) & 3) != 0)
                    throw new NotSupportedException($"Embedded controller timing requires support: {node.Name}, flags={flags}, frequency={frequency}, phase={phase}, range={start}..{stop}.");
                double duration = stop / frequency;
                if (period is { } existing && Math.Abs(existing - duration) > 1e-5)
                    throw new NotSupportedException("Embedded controllers have different loop periods; animation was not frozen.");
                period = duration;
                byte[] channels = Enumerable.Range(0, 3).Select(i => (byte) (curves[i] is not null || pose[i].All(v => double.IsFinite(v) && Math.Abs(v) < 1e30) ? 1 : 0)).ToArray();
                tracks.AddRange(AnimationReader.BuildTracks(node.Name, curves, pose, channels, stop, frequency, 60));
            }
        }
    }
    private static double[] Values(NifReader reader, int count) => Enumerable.Range(0, count).Select(_ => (double) reader.Float()).ToArray();
}
