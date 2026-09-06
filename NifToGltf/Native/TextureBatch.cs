using System.Text.Json;

namespace NifToGltf.Native;

// Lossless RGBA accompanies PNGs so transparent face recoloring never passes
// through a browser canvas's premultiplied-alpha decode.
internal static class TextureBatch {
    public static int Run(string input, string output) {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Texture output must be empty.");
        Directory.CreateDirectory(output);
        List<object> converted = [], failed = [];
        foreach (string file in Directory.GetFiles(input, "*.dds", SearchOption.AllDirectories).Order()) {
            string relative = Path.GetRelativePath(input, file).Replace('\\', '/').ToLowerInvariant();
            try {
                TexturePixels pixels = DdsTexture.Decode(File.ReadAllBytes(file));
                string target = NativeBatch.Resolve(output, Path.ChangeExtension(relative, ".png"));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, pixels.ToPng());
                File.WriteAllText(Path.ChangeExtension(target, ".json"), JsonSerializer.Serialize(new { width = pixels.Width, height = pixels.Height, rgba = Convert.ToBase64String(pixels.Rgba) }));
                converted.Add(new { input = relative, uri = Path.ChangeExtension(relative, ".png") });
            } catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException) {
                failed.Add(new { input = relative, error = error.Message });
            }
        }
        File.WriteAllText(Path.Combine(output, "texture-report.json"), JsonSerializer.Serialize(new { converted, failed }));
        Console.WriteLine($"Textures: {converted.Count} converted, {failed.Count} failed.");
        return failed.Count > 0 ? 1 : 0;
    }
}
