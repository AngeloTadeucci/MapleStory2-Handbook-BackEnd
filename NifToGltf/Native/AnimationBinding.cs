namespace NifToGltf.Native;

internal static class AnimationBinding {
    public static bool IsUnboundPosedAccumulationTrack(AnimationClip clip, AnimationTrack track,
        IReadOnlyDictionary<string, int> targetCounts) {
        // KMS2 FillInfo (0x141c6de00) leaves missing targets unbound. Keep this
        // compatibility case narrow: an authored posed NonAccum track whose
        // accumulation root exists. Animated or unrelated missing targets fail.
        return !string.IsNullOrEmpty(clip.AccumulationRoot) &&
            track.Node == clip.AccumulationRoot + " NonAccum" && track.Posed &&
            !targetCounts.ContainsKey(track.Node) &&
            targetCounts.TryGetValue(clip.AccumulationRoot, out int roots) && roots == 1;
    }
}
