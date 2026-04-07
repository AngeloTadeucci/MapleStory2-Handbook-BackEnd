using System.Text.Json;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class BgmParser {
    private const string MetadataPath = "bgm_tracks.json";
    private const string EventMapPath = "Resources/bgm_event_map.json";

    public static void Parse() {
        // Look for bgm_tracks.json in the solution directory or current directory
        string? jsonPath = FindMetadataFile();
        if (jsonPath == null) {
            Console.WriteLine($"BGM metadata file not found. Run FsbExtractor with --ogg first.");
            Console.WriteLine($"Expected: bgm_tracks.json in solution root or GameParser directory.");
            return;
        }

        Console.WriteLine($"Loading BGM metadata from: {jsonPath}");
        string json = File.ReadAllText(jsonPath);

        var tracks = JsonSerializer.Deserialize<List<BgmTrackEntry>>(json);
        if (tracks == null || tracks.Count == 0) {
            Console.WriteLine("No BGM tracks found in metadata file.");
            return;
        }

        // Load event ID mapping (fileName -> eventId)
        var eventMap = LoadEventMap();
        Console.WriteLine($"Loaded {eventMap.Count} BGM event mappings.");

        Console.WriteLine($"Parsing {tracks.Count} BGM tracks...");
        int current = 0;

        foreach (var track in tracks) {
            current++;
            if (current % 50 == 0 || current == tracks.Count) {
                Console.WriteLine($"Parsing BGM tracks: {current}/{tracks.Count}");
            }

            eventMap.TryGetValue(track.name, out int eventId);

            QueryManager.QueryFactory.Query("bgm_tracks").Insert(new {
                name = track.name,
                file_name = track.fileName,
                source_bank = track.source,
                frequency = track.frequency,
                channels = track.channels,
                duration_seconds = track.durationSeconds,
                loop_start = track.loopStart,
                loop_end = track.loopEnd,
                event_id = eventId,
            });
        }
    }

    private static Dictionary<string, int> LoadEventMap() {
        string eventMapPath = Path.Combine(Maple2Storage.Types.Paths.SolutionDir, "GameParser", EventMapPath);
        if (!File.Exists(eventMapPath)) {
            Console.WriteLine($"BGM event map not found at: {eventMapPath}");
            return new Dictionary<string, int>();
        }

        string json = File.ReadAllText(eventMapPath);
        var entries = JsonSerializer.Deserialize<List<BgmEventEntry>>(json);
        if (entries == null) return new Dictionary<string, int>();

        // Map fileName -> eventId (first occurrence wins for duplicates)
        var map = new Dictionary<string, int>();
        foreach (var entry in entries) {
            map.TryAdd(entry.fileName, entry.eventId);
        }
        return map;
    }

    private static string? FindMetadataFile() {
        // Check common locations
        string[] searchPaths = [
            Path.Combine(Maple2Storage.Types.Paths.SolutionDir, "GameParser", "Resources", MetadataPath),
            Path.Combine(Maple2Storage.Types.Paths.SolutionDir, MetadataPath),
            Path.Combine(Maple2Storage.Types.Paths.SolutionDir, "FsbExtractor", "bgm_final", "ogg", MetadataPath),
            Path.Combine(Directory.GetCurrentDirectory(), MetadataPath),
        ];

        foreach (string path in searchPaths) {
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private record BgmTrackEntry(
        int index,
        string name,
        string fileName,
        string source,
        int frequency,
        int channels,
        double durationSeconds,
        int loopStart,
        int loopEnd
    );

    private record BgmEventEntry(int eventId, string fileName);
}
