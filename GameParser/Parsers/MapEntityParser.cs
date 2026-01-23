using Maple2.File.Flat;
using Maple2.File.Flat.maplestory2library;
using Maple2.File.Parser.Flat;
using Maple2.File.Parser.MapXBlock;
using Maple2.File.Parser.Tools;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class MapEntityParser {
    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");

        // XBlockParser reads entity data from xblock flatbuffer files
        var index = new FlatTypeIndex(Paths.ExportedReader);
        var parser = new XBlockParser(Paths.ExportedReader, index);

        int processedMaps = 0;
        int totalNpcs = 0;
        int totalPortals = 0;
        int totalMobSpawns = 0;

        // Use Parallel() to get the enumerable of map data
        foreach (var map in parser.Parallel()) {
            string xblock = map.xblock.ToLower();
            int? mapId = GetMapIdForXBlock(xblock);

            if (!mapId.HasValue) {
                continue; // Skip xblocks that don't have a corresponding map
            }

            processedMaps++;
            if (processedMaps % 10 == 0) {
                Console.WriteLine($"Processing xblock {processedMaps}: {xblock}");
            }

            // Parse NPCs, Portals, and Mob Spawns
            foreach (var entity in map.entities) {
                // Check if entity is a spawn point (NPC)
                if (entity is ISpawnPointNPC npcSpawn) {
                    ParseNpcSpawn(mapId.Value, npcSpawn);
                    totalNpcs++;
                }

                // Check if entity is a portal
                if (entity is IPortal portal) {
                    ParsePortal(mapId.Value, portal);
                    totalPortals++;
                }

                // Check if entity is a mob spawn region
                if (entity is IMS2RegionSpawnBase mobSpawn) {
                    int mobsSpawned = ParseMobSpawn(mapId.Value, mobSpawn);
                    totalMobSpawns += mobsSpawned;
                }
            }
        }

        Console.WriteLine($"\nProcessed {processedMaps} maps, {totalNpcs} NPCs, {totalPortals} portals, {totalMobSpawns} mob spawns");
    }

    private static int? GetMapIdForXBlock(string xblock) {
        // Query the maps table to find the map ID for this xblock
        var result = QueryManager.QueryFactory.Query("maps")
            .Where("xblock_name", xblock)
            .Select("id")
            .FirstOrDefault();

        if (result == null) return null;
        return (int) result.id;
    }

    private static void ParseNpcSpawn(int mapId, ISpawnPointNPC npcSpawn) {
        // Extract NPC IDs from the NpcList
        foreach (var entry in npcSpawn.NpcList) {
            if (!int.TryParse(entry.Key, out int npcId)) {
                continue;
            }

            // Insert NPC spawn data
            QueryManager.QueryFactory.Query("map_npcs").Insert(new {
                map_id = mapId,
                npc_id = npcId,
                coord_x = (int) npcSpawn.Position.X,
                coord_y = (int) npcSpawn.Position.Y,
                coord_z = (int) npcSpawn.Position.Z,
                rotation_x = (int) npcSpawn.Rotation.X,
                rotation_y = (int) npcSpawn.Rotation.Y,
                rotation_z = (int) npcSpawn.Rotation.Z,
                model_name = "",  // Not available in spawn point
                instance_name = npcSpawn.EntityName ?? "",
                is_spawn_on_field_create = npcSpawn.IsSpawnOnFieldCreate ? 1 : 0,
                is_day_die = 0,  // Not available in ISpawnPointNPC
                is_night_die = 0,  // Not available in ISpawnPointNPC
                patrol_data_uuid = npcSpawn.PatrolData ?? ""
            });
        }
    }

    private static void ParsePortal(int mapId, IPortal portal) {
        QueryManager.QueryFactory.Query("map_portals").Insert(new {
            map_id = mapId,
            portal_id = portal.PortalID,
            name = portal.EntityName ?? "",
            destination_map_id = portal.TargetFieldSN,
            target_portal_id = portal.TargetPortalID,
            coord_x = (int) portal.Position.X,
            coord_y = (int) portal.Position.Y,
            coord_z = (int) portal.Position.Z,
            rotation_x = (int) portal.Rotation.X,
            rotation_y = (int) portal.Rotation.Y,
            rotation_z = (int) portal.Rotation.Z,
            portal_type = portal.PortalType,
            is_enabled = portal.PortalEnable ? 1 : 0,
            is_visible = portal.IsVisible ? 1 : 0,
            minimap_visible = portal.MinimapIconVisible ? 1 : 0,
            trigger_id = 0  // Not directly available in IPortal
        });
    }

    private static int ParseMobSpawn(int mapId, IMS2RegionSpawnBase mobSpawn) {
        // Get spawn metadata from MapSpawnParser
        MapSpawnData? spawnData = MapSpawnParser.GetSpawn(mapId, mobSpawn.SpawnPointID);

        if (spawnData == null) {
            // No spawn metadata for this spawn point, skip it
            return 0;
        }

        // Resolve tags to NPC IDs
        HashSet<int> npcIds = NpcTagLookup.GetNpcIdsForTags(spawnData.Tags);

        if (npcIds.Count == 0) {
            // No NPCs found for these tags, skip it
            return 0;
        }

        // Insert a record for each NPC that can spawn here
        foreach (int npcId in npcIds) {
            QueryManager.QueryFactory.Query("map_mobs").Insert(new {
                map_id = mapId,
                spawn_point_id = mobSpawn.SpawnPointID,
                npc_id = npcId,
                coord_x = (int) mobSpawn.Position.X,
                coord_y = (int) mobSpawn.Position.Y,
                coord_z = (int) mobSpawn.Position.Z,
                rotation_x = (int) mobSpawn.Rotation.X,
                rotation_y = (int) mobSpawn.Rotation.Y,
                rotation_z = (int) mobSpawn.Rotation.Z,
                min_difficulty = spawnData.MinDifficulty,
                max_difficulty = spawnData.MaxDifficulty,
                population = spawnData.Population,
                cooldown = spawnData.Cooldown,
                pet_population = spawnData.PetPopulation,
                pet_spawn_rate = spawnData.PetSpawnRate
            });
        }

        return npcIds.Count;
    }
}
