using System.Numerics;

namespace NifToGltf.Native;

internal static class SceneLighting {
    public static Vector3 Ambient(NifDocument document, int mesh, IReadOnlyDictionary<int, int> parents) {
        HashSet<int> ancestry = [mesh];
        int current = mesh;
        while (parents.TryGetValue(current, out current)) {
            if (!ancestry.Add(current)) throw new InvalidDataException("Cycle in light scope.");
        }
        HashSet<int> effects = ancestry.SelectMany(id => document.Nodes[id].Effects).Where(id => id >= 0).ToHashSet();
        foreach (NifNode light in document.Nodes.Values.Where(node => node.AmbientLight is not null))
            if (light.AmbientLight!.Affected.Any(ancestry.Contains)) effects.Add(light.Id);
        foreach (NifNode effect in document.Nodes.Values.Where(node => node.TextureEffectTargets is not null))
            if (effect.TextureEffectTargets!.Any(ancestry.Contains)) effects.Add(effect.Id);
        Vector3 ambient = Vector3.Zero;
        foreach (int id in effects) {
            if (!document.Nodes.TryGetValue(id, out NifNode? node) || node.AmbientLight is not { } light)
                throw new NotSupportedException($"Visible mesh {document.Nodes[mesh].Name} uses scene effect block {id}; appearance requires an explicit material implementation.");
            if (light.Enabled) ambient += light.Ambient * light.Dimmer;
        }
        if (!float.IsFinite(ambient.X) || !float.IsFinite(ambient.Y) || !float.IsFinite(ambient.Z))
            throw new InvalidDataException("Ambient light intensity exceeds float32 range.");
        return ambient;
    }
}
