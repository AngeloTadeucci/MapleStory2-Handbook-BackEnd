using Maple2.File.Parser;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.Table;
using Maple2Storage.Types;

namespace GameParser.Parsers;

/// <summary>
/// Parses map spawn metadata from MapSpawnTag table.
/// Stores spawn configurations that will be matched with xblock region spawns.
/// </summary>
public static class MapSpawnParser {
    public static Dictionary<int, Dictionary<int, MapSpawnData>> MapSpawns { get; private set; } = new();

    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        TableParser parser = new(Paths.XmlReader, "en");

        foreach ((int mapId, IEnumerable<MapSpawnTag.Region> regions) in parser.ParseMapSpawnTag()) {
            if (!MapSpawns.ContainsKey(mapId)) {
                MapSpawns[mapId] = new Dictionary<int, MapSpawnData>();
            }

            foreach (MapSpawnTag.Region region in regions) {
                MapSpawns[mapId][region.spawnPointID] = new MapSpawnData {
                    SpawnPointId = region.spawnPointID,
                    MinDifficulty = region.difficultyMin,
                    MaxDifficulty = region.difficulty,
                    Population = region.population,
                    Cooldown = region.coolTime,
                    Tags = region.tag,
                    PetPopulation = region.petPopulation,
                    PetSpawnRate = region.petSpawnProbability
                };
            }
        }

        Console.WriteLine($"Parsed spawn data for {MapSpawns.Count} maps");
    }

    public static MapSpawnData? GetSpawn(int mapId, int spawnPointId) {
        if (MapSpawns.TryGetValue(mapId, out var spawns)) {
            spawns.TryGetValue(spawnPointId, out MapSpawnData? spawn);
            return spawn;
        }
        return null;
    }
}

public class MapSpawnData {
    public int SpawnPointId { get; init; }
    public int MinDifficulty { get; init; }
    public int MaxDifficulty { get; init; }
    public int Population { get; init; }
    public int Cooldown { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
    public int PetPopulation { get; init; }
    public int PetSpawnRate { get; init; }
}
