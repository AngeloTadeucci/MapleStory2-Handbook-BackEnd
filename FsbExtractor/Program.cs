using System.Diagnostics;
using System.Text.Json;
using FsbExtractor;

bool convertToOgg = args.Contains("--ogg");
var filteredArgs = args.Where(a => a != "--ogg").ToArray();

if (filteredArgs.Length < 2) {
    Console.WriteLine("Usage: FsbExtractor [--ogg] <output-dir> <fsb-file-or-dir> [fsb-file-or-dir...]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --ogg    Convert extracted audio to OGG Vorbis (requires ffmpeg)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  FsbExtractor ./output D:\\Sound\\");
    Console.WriteLine("  FsbExtractor --ogg ./output D:\\Sound\\");
    return 1;
}

string outputDir = filteredArgs[0];
var fsbPaths = new List<string>();

foreach (string arg in filteredArgs.Skip(1)) {
    if (Directory.Exists(arg)) {
        fsbPaths.AddRange(Directory.GetFiles(arg, "*BGM*.fsb"));
    } else if (File.Exists(arg)) {
        fsbPaths.Add(arg);
    } else {
        Console.Error.WriteLine($"Warning: '{arg}' not found, skipping.");
    }
}

if (fsbPaths.Count == 0) {
    Console.Error.WriteLine("No FSB files found.");
    return 1;
}

Console.WriteLine($"Found {fsbPaths.Count} FSB file(s):");
foreach (string path in fsbPaths) {
    Console.WriteLine($"  {Path.GetFileName(path)} ({new FileInfo(path).Length / 1024 / 1024}MB)");
}
Console.WriteLine();

var allSamples = new List<(string fileName, Fsb5Sample sample)>();

foreach (string fsbPath in fsbPaths.OrderBy(p => p)) {
    try {
        var fsb = Fsb5File.Parse(fsbPath);
        Console.WriteLine($"{Path.GetFileName(fsbPath)}: {fsb.SampleCount} samples, codec={fsb.Codec}, version={fsb.Version}");

        fsb.ExtractAll(outputDir);
        fsb.WriteMetadata(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(fsbPath) + "_metadata.json"));

        foreach (var sample in fsb.Samples) {
            allSamples.Add((Path.GetFileName(fsbPath), sample));
        }
    } catch (Exception ex) {
        Console.Error.WriteLine($"Error processing {fsbPath}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Total: {allSamples.Count} tracks extracted to {Path.GetFullPath(outputDir)}");

// Convert to OGG if requested
if (convertToOgg) {
    Console.WriteLine();
    Console.WriteLine("Converting to OGG Vorbis...");

    string oggDir = Path.Combine(outputDir, "ogg");
    Directory.CreateDirectory(oggDir);

    var mp3Files = Directory.GetFiles(outputDir, "*.mp3");
    int converted = 0;
    int failed = 0;

    foreach (string mp3Path in mp3Files) {
        string oggPath = Path.Combine(oggDir, Path.GetFileNameWithoutExtension(mp3Path) + ".ogg");

        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "ffmpeg",
                Arguments = $"-y -v error -i \"{mp3Path}\" -c:a libvorbis -q:a 5 \"{oggPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode == 0) {
            converted++;
        } else {
            failed++;
            string err = process.StandardError.ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(err)) Console.Error.WriteLine($"  ffmpeg error for {Path.GetFileName(mp3Path)}: {err}");
        }

        if (converted % 50 == 0 || converted + failed == mp3Files.Length) {
            Console.WriteLine($"  [{converted + failed}/{mp3Files.Length}] converted ({failed} failed)");
        }
    }

    // Write combined metadata to ogg dir
    var combinedMeta = allSamples.Select((s, i) => new {
        index = i,
        name = s.sample.Name,
        fileName = Fsb5File.SanitizeFileName(s.sample.Name) + ".ogg",
        source = s.fileName,
        frequency = s.sample.Frequency,
        channels = s.sample.Channels,
        durationSeconds = Math.Round(s.sample.DurationSeconds, 2),
        loopStart = s.sample.LoopStart,
        loopEnd = s.sample.LoopEnd,
    }).ToList();

    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(Path.Combine(oggDir, "bgm_tracks.json"), JsonSerializer.Serialize(combinedMeta, options));

    long totalOggSize = Directory.GetFiles(oggDir, "*.ogg").Sum(f => new FileInfo(f).Length);
    Console.WriteLine($"  OGG output: {converted} files, {totalOggSize / 1024 / 1024}MB total");
}

Console.WriteLine();
var byFreq = allSamples.GroupBy(s => s.sample.Frequency);
foreach (var g in byFreq.OrderBy(g => g.Key)) {
    Console.WriteLine($"  {g.Key}Hz: {g.Count()} tracks");
}

double totalDuration = allSamples.Sum(s => s.sample.DurationSeconds);
Console.WriteLine($"  Total duration: {TimeSpan.FromSeconds(totalDuration):hh\\:mm\\:ss}");

return 0;
