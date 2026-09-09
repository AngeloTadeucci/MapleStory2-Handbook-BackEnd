using System.Text;

namespace NifToGltf.Native;

internal sealed record KfmClip(int Event, string File, string Name);
internal sealed record KfmDocument(string Model, string Master, KfmClip[] Clips) {
    public static KfmDocument Read(string path, string? sourceRoot = null) {
        byte[] data = System.IO.File.ReadAllBytes(path);
        int line = Array.IndexOf(data, (byte) '\n');
        if (line < 0 || Encoding.ASCII.GetString(data, 0, line) != ";Gamebryo KFM File Version 30.2.0.3b") {
            throw new NotSupportedException("Expected KFM 30.2.0.3b.");
        }
        NifReader r = new(data);
        r.Take(line + 1);
        if (r.Byte() != 1) throw new NotSupportedException("Big-endian KFM.");
        string model = r.SizedString(), master = r.SizedString();
        r.I32(); r.I32(); r.Float(); r.Float();
        KfmClip[] clips = Enumerable.Range(0, r.Count()).Select(_ => {
            int eventId = r.I32();
            string file = r.SizedString();
            // MS2's 30.2 KFM stores the sequence name here, unlike older KFM's integer index.
            string name = r.SizedString();
            int transitions = r.Count();
            for (int i = 0; i < transitions; i++) {
                r.I32();
                uint type = r.U32();
                if (type == 5) continue;
                r.Float();
                int intermediate = r.Count();
                for (int j = 0; j < intermediate; j++) { r.SizedString(); r.SizedString(); }
                // Observed MS2 transition records contain int32/float32 pairs.
                // Clip export does not reproduce the controller transition graph.
                int pairs = r.Count();
                for (int j = 0; j < pairs; j++) { r.I32(); r.Float(); }
            }
            return new KfmClip(eventId, Resolve(path, file, sourceRoot), name);
        }).ToArray();
        r.I32();
        r.Finish();
        return new KfmDocument(Resolve(path, model, sourceRoot), master, clips);
    }
    private static string Resolve(string kfm, string asset, string? sourceRoot) {
        string directory = Path.GetDirectoryName(Path.GetFullPath(kfm))!;
        string root = Path.GetFullPath(sourceRoot ?? directory).TrimEnd(Path.DirectorySeparatorChar);
        string normalized = asset.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(asset) || Path.IsPathRooted(normalized) || normalized.Contains(':'))
            throw new InvalidDataException("Invalid KFM asset reference.");
        string path = Path.GetFullPath(Path.Combine(directory, normalized));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("KFM asset path escapes its source root.");
        }
        if (System.IO.File.Exists(path)) return path;
        // MS2 paths are case-insensitive, including on a Linux converter host.
        string current = root;
        foreach (string part in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)) {
            string[] matches = Directory.EnumerateFileSystemEntries(current).Where(candidate => string.Equals(Path.GetFileName(candidate), part, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) throw new FileNotFoundException($"KFM asset {asset} missing or ambiguous beside {kfm}.");
            current = matches[0];
        }
        return current;
    }
}
