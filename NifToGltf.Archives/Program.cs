using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Maple2.File.IO;

try {
    if (args.Length is < 2 or > 3) throw new ArgumentException("Usage: NifToGltf.Archives archive.m2d regex [empty-output-directory]. Omit output to list only.");
    Regex pattern = new(args[1], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    using M2dReader reader = new(args[0]);
    var selected = reader.Files.Where(file => pattern.IsMatch(file.Name)).ToArray();
    if (args.Length == 2) {
        Console.WriteLine(JsonSerializer.Serialize(selected.Select(file => file.Name), new JsonSerializerOptions { WriteIndented = true }));
        return;
    }
    string destination = Path.GetFullPath(args[2]);
    if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException("Extraction output must be empty.");
    string prefix = Path.TrimEndingDirectorySeparator(destination) + Path.DirectorySeparatorChar;
    HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase);
    foreach (var file in selected) {
        string name = file.Name.Replace('\\', '/');
        string target = Path.GetFullPath(Path.Combine(destination, name));
        if (Path.IsPathRooted(name) || name.Contains(':') || !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !targets.Add(target)) {
            throw new InvalidDataException($"Invalid or duplicate archive path {name}.");
        }
    }
    Directory.CreateDirectory(destination);
    List<object> report = [];
    foreach (var file in selected) {
        string relative = file.Name.Replace('\\', '/');
        string target = Path.Combine(destination, relative);
        byte[] bytes = reader.GetBytes(file);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (FileStream output = new(target, FileMode.CreateNew)) output.Write(bytes);
        report.Add(new { path = relative, length = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
    }
    File.WriteAllText(Path.Combine(destination, "extraction-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Extracted {selected.Length} entries. Source archive was read only.");
} catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or RegexMatchTimeoutException) {
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}
