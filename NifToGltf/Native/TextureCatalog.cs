using System.Security.Cryptography;

namespace NifToGltf.Native;

internal sealed class TextureCatalog(string? roots) {
    private Dictionary<string, string[]>? files;
    private readonly Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
    public string Resolve(string model, string name) {
        string filename = Path.GetFileName(name.Replace('\\', '/'));
        string directory = Path.GetDirectoryName(model)!;
        string? local = Directory.EnumerateFiles(directory).FirstOrDefault(file => string.Equals(Path.GetFileName(file), filename, StringComparison.OrdinalIgnoreCase));
        if (local is not null) return local;
        files ??= (roots?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => string.Equals(Path.GetExtension(file), ".dds", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key!, group => group.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(), StringComparer.OrdinalIgnoreCase);
        if (!files.TryGetValue(filename, out string[]? matches)) throw new FileNotFoundException($"Texture {name} missing beside model and in supplied texture roots.");
        string suffix = name.Replace('\\', '/').TrimStart('/');
        if (suffix.Contains('/')) {
            string[] exact = matches.Where(path => path.Replace('\\', '/').EndsWith('/' + suffix, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length > 0) matches = exact;
        }
        // Archives can repeat an identical image under several paths. Never choose between different images by basename.
        string Hash(string file) {
            if (!hashes.TryGetValue(file, out string? hash)) hashes[file] = hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            return hash;
        }
        if (matches.Length > 1 && matches.Select(Hash).Distinct().Count() != 1) {
            throw new InvalidDataException($"Texture {name} is ambiguous across {matches.Length} distinct-path candidates in supplied texture roots.");
        }
        return matches[0];
    }
}
