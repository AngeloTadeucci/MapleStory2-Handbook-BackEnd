using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.Map;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class MapNameParser {
    public static Dictionary<int, string> MapNames { get; private set; } = new Dictionary<int, string>();

    static MapNameParser() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.MapParser parser = new(Paths.XmlReader, "en");
        foreach ((int id, string? name, MapData _) in parser.Parse()) {
            MapNames[id] = name;
        }
    }

    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.MapParser parser = new(Paths.XmlReader, "en");

        var maps = parser.Parse().ToList();
        int total = maps.Count;
        int current = 0;

        Console.WriteLine($"Parsing {total} maps...");

        foreach ((int id, string? name, MapData data) in maps) {
            current++;
            if (current % 100 == 0 || current == total) {
                Console.WriteLine($"Parsing maps: {current}/{total}");
            }
            string xblock = data.xblock.name.ToLower();
            MapImagesParser.MapsImages.TryGetValue(xblock, out (string minimap, string icon, string bg) images);

            // Extract available properties from MapData
            var property = data.property;
            var drop = data.drop;

            // Insert map data with available properties
            QueryManager.QueryFactory.Query("maps").Insert(new {
                id,
                name = string.IsNullOrEmpty(name) ? "" : name,
                minimap = images.minimap ?? "",
                icon = images.icon ?? "",
                bg = images.bg ?? "",
                description = "",
                xblock_name = xblock,
                recommended_level = drop.maplevel,
                drop_rank = drop.droprank,
                max_capacity = property.capacity,
                death_penalty = property.deathPenalty ? 1 : 0,
                flight_enabled = property.checkFly ? 1 : 0,
                climb_enabled = property.checkClimb ? 1 : 0,
                home_returnable = property.homeReturnable ? 1 : 0,
                is_tutorial_map = property.tutorialType > 0 ? 1 : 0,
                revival_return_map_id = property.revivalreturnid,
                enter_return_map_id = property.enterreturnid,
                minimap_width = 0,  // Not available in MapData
                minimap_height = 0,  // Not available in MapData
                bounding_box_min_x = 0,  // Entities not available in MapData from parser
                bounding_box_min_y = 0,
                bounding_box_min_z = 0,
                bounding_box_max_x = 0,
                bounding_box_max_y = 0,
                bounding_box_max_z = 0,
                block_metadata = "{}"  // Blocks not available in MapData from parser
            });

            // Note: Entities (NPCs, Portals) and Blocks are not available in MapData from the parser
            // They would need to be parsed from a different file format (xblock files or flatbuffers)
        }
    }

}
