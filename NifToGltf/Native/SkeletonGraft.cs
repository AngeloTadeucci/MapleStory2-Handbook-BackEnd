using System.Numerics;

namespace NifToGltf.Native;

internal static class SkeletonGraft {
    public static void Apply(NifDocument gear, NifDocument body, string? attachBone, string? selfNode = null, bool replace = false, Matrix4x4? dummyTransform = null) {
        bool IsSkin(int id) => gear.GraftedSkins.ContainsKey(id) || gear.CanonicalSkins.ContainsKey(id) ||
            ((uint) id < gear.Blocks.Length && gear.Blocks[id].Type == "NiSkinningMeshModifier");
        bool sourceSkinned = gear.Nodes.Values.Any(node => node.Mesh?.Modifiers.Any(IsSkin) == true);
        bool worldSpaceHair = false;
        if (sourceSkinned && replace && attachBone == "HR") {
            HashSet<string> bodyNames = body.Nodes.Values.Select(n => n.Name).ToHashSet();
            bool privateJoints = gear.Nodes.Values.Where(n => n.Mesh is not null).SelectMany(n => n.Mesh!.Modifiers)
                .Where(IsSkin).SelectMany(id => gear.Skin(id).Bones).Any(id => !bodyNames.Contains(gear.Nodes[id].Name));
            if (privateJoints) {
                worldSpaceHair = true;
                NifNode hair = body.Nodes.Values.Single(n => n.Name == "HR");
                attachBone = body.Nodes.Values.Single(n => n.Children.Contains(hair.Id)).Name;
                replace = false;
            }
        }
        bool privateAttachment = sourceSkinned && attachBone is not null && !replace;
        if (privateAttachment && dummyTransform is { } dummy && dummy != Matrix4x4.Identity)
            throw new NotSupportedException("Skinned attachment dummy transforms require verified source support.");
        if (privateAttachment && selfNode is null) throw new ArgumentException("Skinned attachments require an explicit itemmodel selfnode.");
        int? attachmentRoot = null;
        HashSet<int> attachmentBranch = [];
        Dictionary<int, int> gearParents = gear.Nodes.Values.SelectMany(node => node.Children.Select(child => (child, node.Id)))
            .ToDictionary(pair => pair.child, pair => pair.Id);
        Matrix4x4 GearWorld(int id) => gear.Nodes[id].Transform * (gearParents.TryGetValue(id, out int parent) ? GearWorld(parent) : Matrix4x4.Identity);
        Matrix4x4 attachmentParentInverse = Matrix4x4.Identity;
        if (selfNode is not null) {
            NifNode[] selected = gear.Nodes.Values.Where(node => node.Name == selfNode).ToArray();
            bool splitHair = false;
            if (selected.Length == 0 && selfNode == "HR" && replace && !sourceSkinned) {
                // Client hair 10200224/10200006 exposes HR0 and HR1 with separate
                // length morphs. XML addresses their shared HR replacement slot.
                selected = gear.Nodes.Values.Where(node => {
                    string name = node.Name.Split(':')[0];
                    return name.StartsWith("HR", StringComparison.Ordinal) && name[2..].All(char.IsDigit);
                }).ToArray();
                splitHair = selected.Length > 0 && selected.Select(node => gearParents.GetValueOrDefault(node.Id, -1)).Distinct().Count() == 1;
            }
            // Some weapons name both the attachment node and its child mesh
            // Point01. Select the enclosing branch only when it is unambiguous.
            if (selected.Length > 1 && !splitHair) {
                HashSet<int> candidates = selected.Select(node => node.Id).ToHashSet();
                bool HasSelectedAncestor(int id) => gearParents.TryGetValue(id, out int parent) && (candidates.Contains(parent) || HasSelectedAncestor(parent));
                NifNode[] roots = selected.Where(node => !HasSelectedAncestor(node.Id)).ToArray();
                if (roots.Length == 1) selected = roots;
            }
            // Inspected player/garment NIFs use CL_Skin, PA_Skin/PA_Panty and
            // SH_Skin as sibling replacement parts. Robes can have only PA_*.
            // Wrist accessories use GL_Wrist plus GL_Skin without a GL mesh.
            // Like garment skin siblings, both are part of the declared slot.
            bool family = sourceSkinned && replace && selfNode is "CL" or "PA" or "SH" or "GL";
            NifNode[] siblings = family ? gear.Nodes.Values.Where(node => node.Name.StartsWith(selfNode + "_", StringComparison.Ordinal)).ToArray() : [];
            if ((selected.Length > 1 && !splitHair) || (selected.Length == 0 && siblings.Length == 0)) throw new InvalidDataException($"Expected one equipment selfnode {selfNode}, found {selected.Length}.");
            HashSet<int> branch = [];
            void Visit(int id) { if (branch.Add(id) && gear.Nodes.TryGetValue(id, out NifNode? node)) foreach (int child in node.Children) Visit(child); }
            foreach (NifNode part in selected.Concat(siblings)) Visit(part.Id);
            if (privateAttachment) {
                attachmentRoot = selected.Single().Id;
                attachmentBranch.UnionWith(branch);
                // The sign's MT mesh is a sibling of its joint hierarchy. Retain
                // skins whose joints all belong to the selected attachment branch.
                foreach (NifNode mesh in gear.Nodes.Values.Where(node => node.Mesh is not null)) {
                    NifSkin[] skins = mesh.Mesh!.Modifiers.Where(IsSkin).Select(gear.Skin).ToArray();
                    if (skins.Length > 0 && skins.All(skin => skin.Bones.All(attachmentBranch.Contains))) Visit(mesh.Id);
                }
            }
            // Clothing stores the garment and its exposed skin as sibling meshes
            // (CL and CL_Skin), rather than children of the selected CL mesh.
            if (sourceSkinned && replace && selfNode == "CL") {
                foreach (NifNode part in gear.Nodes.Values.Where(node => node.Name.StartsWith("CL_", StringComparison.Ordinal))) Visit(part.Id);
            }
            foreach (NifNode node in gear.Nodes.Values.Where(node => node.Mesh is not null && !branch.Contains(node.Id)).ToArray()) {
                gear.Nodes[node.Id] = node with { Mesh = null };
                gear.Omitted.Add(new(node.Id, "NiMesh", $"Outside itemmodel selfnode {selfNode}."));
            }
            if (!sourceSkinned && gearParents.TryGetValue(selected[0].Id, out int parent) && !Matrix4x4.Invert(GearWorld(parent), out attachmentParentInverse)) {
                throw new InvalidDataException("Singular equipment attachment parent.");
            }
            if (replace && !sourceSkinned) {
                NifNode[] targets = body.Nodes.Values.Where(node => node.Name == attachBone).ToArray();
                if (targets.Length != 1) throw new InvalidDataException($"Expected one replacement target {attachBone}.");
                NifNode[] parents = body.Nodes.Values.Where(node => node.Children.Contains(targets[0].Id)).ToArray();
                if (parents.Length != 1) throw new InvalidDataException("Replacement target needs one body parent.");
                attachBone = parents[0].Name;
            }
        }
        if (!sourceSkinned && attachBone is null) {
            // Only CP -> Head is established by the plan. Other slots require an explicit bone.
            if (!gear.Nodes.Values.Any(node => node.Name == "CP")) {
                throw new ArgumentException("Cannot infer attachment. Supply --attach with a bone name from the body.");
            }
            attachBone = "Bip01 Head";
        }
        NifNode[] matches = body.Nodes.Values.Where(node => node.Mesh is null && node.Name == attachBone).ToArray();
        if ((!sourceSkinned || privateAttachment) && matches.Length != 1) throw new InvalidDataException($"Expected one attachment bone {attachBone}, found {matches.Length}.");
        NifNode[] gearMeshes = gear.Nodes.Values.Where(node => node.Mesh is not null).ToArray();
        NifNode[] bodyNodes = body.Nodes.Values.Where(node => node.Mesh is null).ToArray();
        Dictionary<int, NifSkin> sourceSkins = gearMeshes.SelectMany(node => node.Mesh!.Modifiers).Where(IsSkin).Distinct().ToDictionary(id => id, gear.Skin);
        int nextId = gear.Blocks.Length;
        Dictionary<int, int> mapping = bodyNodes.ToDictionary(node => node.Id, _ => nextId++);
        foreach (NifNode node in bodyNodes) {
            gear.Nodes[mapping[node.Id]] = node with { Id = mapping[node.Id], Properties = [], ExtraData = [], Controller = -1, Effects = [],
                Children = node.Children.Where(mapping.ContainsKey).Select(id => mapping[id]).ToArray() };
        }
        foreach (int root in gear.Roots) {
            gear.Nodes[root] = gear.Nodes[root] with { Name = "Equipment source" };
        }
        int[] skeletonRoots = body.Roots.Select(id => mapping[id]).ToArray();
        gear.Roots = [..gear.Roots, ..skeletonRoots];
        if (sourceSkinned) {
            if (privateAttachment) {
                int root = attachmentRoot!.Value;
                foreach (NifSkin skin in sourceSkins.Values)
                    if (skin.Bones.Any(bone => !attachmentBranch.Contains(bone)))
                        throw new InvalidDataException("Skinned attachment references joints outside its itemmodel branch.");
                if (gearParents.TryGetValue(root, out int parent)) {
                    gear.Nodes[parent] = gear.Nodes[parent] with { Children = gear.Nodes[parent].Children.Where(id => id != root).ToArray() };
                } else if (gear.Roots.Contains(root)) {
                    // Animated hats can attach their entire Scene Root. Move it
                    // out of the scene roots before parenting it to the head.
                    gear.Roots = gear.Roots.Where(id => id != root).ToArray();
                } else throw new InvalidDataException("Private attachment has neither a parent nor a scene root.");
                int target = mapping[matches[0].Id];
                if (worldSpaceHair) {
                    // Replacement hair joints are authored in character space.
                    // Preserve their source bind pose beneath the animated head.
                    // A separate parent keeps source animation keys unchanged.
                    Dictionary<int, int> bodyParents = body.Nodes.Values.SelectMany(n => n.Children.Select(c => (c, n.Id)))
                        .ToDictionary(pair => pair.c, pair => pair.Id);
                    Matrix4x4 BodyWorld(int id) => body.Nodes[id].Transform * (bodyParents.TryGetValue(id, out int p) ? BodyWorld(p) : Matrix4x4.Identity);
                    if (!Matrix4x4.Invert(BodyWorld(matches[0].Id), out Matrix4x4 inverseHead))
                        throw new InvalidDataException("Singular hair replacement parent.");
                    Matrix4x4 sourceParent = gearParents.TryGetValue(root, out int sourceParentId) ? GearWorld(sourceParentId) : Matrix4x4.Identity;
                    int bridge = nextId++;
                    gear.Nodes[bridge] = gear.Nodes[target] with { Id = bridge, Name = "Equipment hair bind parent",
                        Transform = sourceParent * inverseHead, Children = [root] };
                    gear.EquipmentBones.Add(bridge);
                    root = bridge;
                }
                gear.Nodes[target] = gear.Nodes[target] with { Children = [..gear.Nodes[target].Children, root] };
                foreach (int id in attachmentBranch.Where(id => gear.Nodes[id].Mesh is null)) gear.EquipmentBones.Add(id);
                foreach ((int id, NifSkin skin) in sourceSkins) gear.CanonicalSkins[id] = skin with { Root = skeletonRoots.Single() };
                // Rigid children already declare their attachment through the
                // selected source hierarchy. Preserve that local transform.
                foreach (NifNode mesh in gearMeshes.Where(node => !node.Mesh!.Modifiers.Any(IsSkin))) {
                    if (!gearParents.TryGetValue(mesh.Id, out int joint) || !gear.EquipmentBones.Contains(joint))
                        throw new NotSupportedException("Rigid attachment mesh has no joint in the selected itemmodel branch.");
                    int modifier = nextId++;
                    gear.GraftedSkins[modifier] = new NifSkin(skeletonRoots.Single(), Matrix4x4.Identity, [joint], [mesh.Transform]);
                    gear.Nodes[mesh.Id] = mesh with { Mesh = mesh.Mesh! with { Modifiers = [..mesh.Mesh!.Modifiers, modifier] } };
                }
                foreach (NifNode node in gear.Nodes.Values.Where(n => n.Id < gear.Blocks.Length && !attachmentBranch.Contains(n.Id)).ToArray())
                    if (bodyNodes.Any(b => b.Name == node.Name)) gear.Nodes[node.Id] = node with { Name = $"Equipment source/{node.Name}" };
                return;
            }
            Dictionary<string, int> bodyBones = bodyNodes.ToDictionary(node => node.Name, node => mapping[node.Id]);
            foreach ((int id, NifSkin skin) in sourceSkins) {
                int[] bones = skin.Bones.Select(bone => bodyBones.TryGetValue(gear.Nodes[bone].Name, out int target) ? target :
                    throw new InvalidDataException($"Equipment bone {gear.Nodes[bone].Name} is absent from the supplied body.")).ToArray();
                gear.CanonicalSkins[id] = skin with { Root = skeletonRoots.Single(), Bones = bones };
            }
            foreach (NifNode node in gear.Nodes.Values.Where(node => node.Id < gear.Blocks.Length).ToArray()) {
                if (bodyBones.ContainsKey(node.Name)) gear.Nodes[node.Id] = node with { Name = $"Equipment source/{node.Name}" };
            }
            if (gearMeshes.Any(node => !node.Mesh!.Modifiers.Any(IsSkin))) throw new NotSupportedException("Mixed rigid/skinned equipment requires per-mesh attachment metadata.");
            return;
        }
        // Rigid accessories can carry an unused copy of the player hierarchy.
        // Reserve the body's animation names for the grafted, deforming skeleton.
        foreach (NifNode node in gear.Nodes.Values.Where(n => n.Id < gear.Blocks.Length).ToArray())
            if (bodyNodes.Any(b => b.Name == node.Name)) gear.Nodes[node.Id] = node with { Name = $"Equipment source/{node.Name}" };
        foreach (NifNode node in gearMeshes) {
            int modifier = nextId++;
            gear.GraftedSkins[modifier] = new NifSkin(skeletonRoots.Single(), Matrix4x4.Identity,
                [mapping[matches[0].Id]], [GearWorld(node.Id) * attachmentParentInverse * (dummyTransform ?? Matrix4x4.Identity)]);
            // Decode the rigid geometry as before; the writer adds constant joint/weight attributes.
            gear.Nodes[node.Id] = node with { Mesh = node.Mesh! with { Modifiers = [..node.Mesh!.Modifiers, modifier] } };
        }
    }
}
