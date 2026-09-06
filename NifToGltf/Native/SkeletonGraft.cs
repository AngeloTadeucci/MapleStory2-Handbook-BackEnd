using System.Numerics;

namespace NifToGltf.Native;

internal static class SkeletonGraft {
    public static void Apply(NifDocument gear, NifDocument body, string? attachBone) {
        bool sourceSkinned = gear.Nodes.Values.Any(node => node.Mesh?.Modifiers.Length > 0);
        if (sourceSkinned && attachBone is not null) throw new ArgumentException("--attach is for rigid gear only.");
        if (!sourceSkinned && attachBone is null) {
            // Only CP -> Head is established by the plan. Other slots require an explicit bone.
            if (!gear.Nodes.Values.Any(node => node.Name == "CP")) {
                throw new ArgumentException("Cannot infer attachment. Supply --attach with a bone name from the body.");
            }
            attachBone = "Bip01 Head";
        }
        NifNode[] matches = body.Nodes.Values.Where(node => node.Mesh is null && node.Name == attachBone).ToArray();
        if (!sourceSkinned && matches.Length != 1) throw new InvalidDataException($"Expected one attachment bone {attachBone}, found {matches.Length}.");
        Dictionary<int, int> gearParents = gear.Nodes.Values.SelectMany(node => node.Children.Select(child => (child, node.Id)))
            .ToDictionary(pair => pair.child, pair => pair.Id);
        Matrix4x4 GearWorld(int id) => gear.Nodes[id].Transform * (gearParents.TryGetValue(id, out int parent) ? GearWorld(parent) : Matrix4x4.Identity);
        NifNode[] gearMeshes = gear.Nodes.Values.Where(node => node.Mesh is not null).ToArray();
        NifNode[] bodyNodes = body.Nodes.Values.Where(node => node.Mesh is null).ToArray();
        Dictionary<int, NifSkin> sourceSkins = gearMeshes.SelectMany(node => node.Mesh!.Modifiers).Distinct().ToDictionary(id => id, gear.Skin);
        int nextId = gear.Blocks.Length;
        Dictionary<int, int> mapping = bodyNodes.ToDictionary(node => node.Id, _ => nextId++);
        foreach (NifNode node in bodyNodes) {
            gear.Nodes[mapping[node.Id]] = node with { Id = mapping[node.Id], Properties = [],
                Children = node.Children.Where(mapping.ContainsKey).Select(id => mapping[id]).ToArray() };
        }
        foreach (int root in gear.Roots) {
            gear.Nodes[root] = gear.Nodes[root] with { Name = "Equipment source" };
        }
        int[] skeletonRoots = body.Roots.Select(id => mapping[id]).ToArray();
        gear.Roots = [..gear.Roots, ..skeletonRoots];
        if (sourceSkinned) {
            Dictionary<string, int> bodyBones = bodyNodes.ToDictionary(node => node.Name, node => mapping[node.Id]);
            foreach ((int id, NifSkin skin) in sourceSkins) {
                int[] bones = skin.Bones.Select(bone => bodyBones.TryGetValue(gear.Nodes[bone].Name, out int target) ? target :
                    throw new InvalidDataException($"Equipment bone {gear.Nodes[bone].Name} is absent from the supplied body.")).ToArray();
                gear.CanonicalSkins[id] = skin with { Root = skeletonRoots.Single(), Bones = bones };
            }
            foreach (NifNode node in gear.Nodes.Values.Where(node => node.Id < gear.Blocks.Length).ToArray()) {
                if (bodyBones.ContainsKey(node.Name)) gear.Nodes[node.Id] = node with { Name = $"Equipment source/{node.Name}" };
            }
            if (gearMeshes.Any(node => node.Mesh!.Modifiers.Length == 0)) throw new NotSupportedException("Mixed rigid/skinned equipment requires per-mesh attachment metadata.");
            return;
        }
        foreach (NifNode node in gearMeshes) {
            int modifier = nextId++;
            gear.GraftedSkins[modifier] = new NifSkin(skeletonRoots.Single(), Matrix4x4.Identity,
                [mapping[matches[0].Id]], [GearWorld(node.Id)]);
            // Decode the rigid geometry as before; the writer adds constant joint/weight attributes.
            gear.Nodes[node.Id] = node with { Mesh = node.Mesh! with { Modifiers = [modifier] } };
        }
    }
}
