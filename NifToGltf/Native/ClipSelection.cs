namespace NifToGltf.Native;

internal static class ClipSelection {
    public static List<AnimationClip> Read(string input, KfmDocument? kfm, string? directory, string? selection) {
        if (selection is null) {
            if (kfm is not null || directory is not null) throw new ArgumentException("Animation sources require an explicit clip selection.");
            return [];
        }
        if (kfm is not null && directory is not null) throw new ArgumentException("Use KFM or an animation directory, not both.");
        bool all = selection.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (all && Path.GetFileNameWithoutExtension(input) is "f_body" or "m_body") throw new ArgumentException("Player bodies require a curated clip list.");
        string[] requested = selection.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (requested.Length == 0 || requested.Distinct(StringComparer.OrdinalIgnoreCase).Count() != requested.Length) throw new ArgumentException("Empty or duplicate clip selection.");
        List<(string File, string? Sequence, string? Name, int? Event)> sources = [];
        if (kfm is not null) {
            KfmClip[] selected = all ? kfm.Clips : requested.Select(name => kfm.Clips.SingleOrDefault(clip => string.Equals(clip.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"KFM clip {name} not found.")).ToArray();
            sources.AddRange(selected.Select(clip => (clip.File, (string?) clip.Name, (string?) clip.Name, (int?) clip.Event)));
        } else if (directory is not null) {
            string[] files = Directory.GetFiles(directory, "*.kf");
            string[] selected = all ? files.Order().ToArray() : requested.Select(name => files.SingleOrDefault(file => Path.GetFileNameWithoutExtension(file).Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"Animation {name} not found in {directory}.")).ToArray();
            sources.AddRange(selected.Select(file => (file, (string?) null, (string?) Path.GetFileNameWithoutExtension(file), (int?) null)));
        } else throw new ArgumentException("Clip selection requires KFM or an animation directory.");
        List<AnimationClip> clips = [];
        List<string> errors = [];
        bool onlyMissing = true;
        foreach (var source in sources) {
            try {
                AnimationClip[] sequences = AnimationReader.ReadAll(source.File, sequenceName: source.Sequence);
                foreach (AnimationClip clip in sequences) {
                    string name = string.IsNullOrWhiteSpace(source.Name) ? clip.Name : source.Name;
                    if (sequences.Length > 1) name += $" [sequence {clip.SourceSequenceBlock}]";
                    clips.Add(clip with { Name = name, SourceEvent = source.Event });
                }
            } catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException) {
                errors.Add(e.Message);
                onlyMissing &= e is FileNotFoundException or DirectoryNotFoundException;
            }
        }
        if (errors.Count > 0) {
            string error = $"{errors.Count} animation(s) rejected; no glTF written:\n{string.Join("\n", errors)}";
            if (onlyMissing) throw new FileNotFoundException(error);
            throw new InvalidDataException(error);
        }
        // An explicitly empty KFM declares a static model. Missing requested clips
        // and failed animation reads still fail above.
        if (all && kfm is { Clips.Length: 0 }) return [];
        return Disambiguate(clips);
    }

    internal static List<AnimationClip> Disambiguate(List<AnimationClip> clips) {
        HashSet<string> duplicates = clips.GroupBy(clip => clip.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<AnimationClip> result = clips.Select(clip => duplicates.Contains(clip.Name) && clip.SourceEvent is not null
            ? clip with { Name = $"{clip.Name} [event {clip.SourceEvent}]" } : clip).ToList();
        if (result.Count == 0 || result.Select(clip => clip.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
            throw new InvalidDataException("No clips or duplicate clip identities.");
        return result;
    }
}
