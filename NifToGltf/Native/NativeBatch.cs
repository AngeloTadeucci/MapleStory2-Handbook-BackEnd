using System.Text.Json;

namespace NifToGltf.Native;

internal static class NativeBatch {
    public static int Run(string input, string output, string? textures) {
        string root = Path.GetFullPath(input);
        string destination = Path.GetFullPath(output);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) {
            throw new IOException($"Batch output must be empty: {destination}");
        }
        string[] files = Directory.GetFiles(root, "*.nif", SearchOption.AllDirectories).Order().ToArray();
        if (files.Length == 0) throw new FileNotFoundException("No NIF files in batch input.");
        Directory.CreateDirectory(destination);
        List<object> converted = [], failed = [];
        foreach (string file in files) {
            string relative = Path.GetRelativePath(root, file);
            string target = Path.Combine(destination, Path.ChangeExtension(relative, ".gltf"));
            try {
                NifDocument document = NifDocument.Load(file);
                object report = new GltfWriter(document, textures).Write(target);
                converted.Add(new { input = relative, report });
            } catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException) {
                failed.Add(new { input = relative, error = e.Message });
            }
            if ((converted.Count + failed.Count) % 100 == 0) {
                Console.WriteLine($"{converted.Count + failed.Count}/{files.Length}: {converted.Count} converted, {failed.Count} rejected");
            }
        }
        object summary = new { mode = "static", input = root, output = destination, total = files.Length,
            convertedCount = converted.Count, failedCount = failed.Count, converted, failed };
        File.WriteAllText(Path.Combine(destination, "batch-report.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Static batch: {converted.Count}/{files.Length} converted; {failed.Count} rejected. Report: {destination}/batch-report.json");
        return failed.Count == 0 ? 0 : 1;
    }
}
