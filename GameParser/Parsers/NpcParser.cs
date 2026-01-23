using System.Text.Json;
using GameParser.Tools;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.Npc;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class NpcParser {
    private static readonly string[] ClassName = ["Friendly", "Common", "Leader", "Elite", "Boss", "World Boss", "Dungeon Boss"];

    private static readonly Dictionary<string, string> RaceName = new()
    {
        {"unknown", "Unknown"},
        {"plant", "Plant"},
        {"animal", "Beast"},
        {"bug", "Insect"},
        {"mystic", "Divine"},
        {"spirit", "Spirit"},
        {"fairy", "Fair Folk"},
        {"combine", "Humanoid"},
        {"undead", "Undead"},
        {"devil", "Devil"},
        {"machine", "Machine"},
        {"creature", "Inanimate"},
    };

    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.NpcParser parser = new(Paths.XmlReader, "en");
        foreach ((int id, string? name, NpcData? data, List<EffectDummy> dummy) in parser.Parse()) {
            // Build tag lookup for mobs
            NpcTagLookup.AddNpc(id, data.basic.mainTags);

            string? npcName = name;
            string? portrait = data.basic.portrait.ToLower();
            if (PetNameParser.PetNames.TryGetValue(id, out string? petName)) {
                dynamic? item = QueryManager.QueryFactory.Query("items").Where("id", id).FirstOrDefault();
                if (item is not null) {
                    portrait = item.icon_path;
                }

                npcName = petName;
            }

            Console.WriteLine($"Parsing NPC {id} - {npcName}");

            string kfm = data.model.kfm.ToLower();

            List<string> animations = [];
            if (AnimationParser.Animations.TryGetValue(kfm, out List<string>? animation)) {
                animations = animation;
            }

            FieldMetadataParser.FieldMetadata.TryGetValue(id, out List<(string mapName, int mapId)>? fieldMetadata);
            NpcTitleParser.NpcTitle.TryGetValue(id, out string? title);

            title ??= "";
            RaceName.TryGetValue(data.basic.raceString.FirstOrDefault() ?? "", out string? race);

            QueryManager.QueryFactory.Query("npcs").Insert(new {
                id,
                name = string.IsNullOrEmpty(npcName) ? "" : npcName,
                kfm,
                is_boss = data.basic.@class >= 3 && data.basic.friendly == 0,
                npc_type = data.basic.friendly,
                data.basic.gender,
                data.basic.level,
                portrait,
                stats = JsonSerializer.Serialize(new CustomStat(data.stat)),
                animations = JsonSerializer.Serialize(animations),
                race = race ?? "",
                class_name = ClassName.ElementAtOrDefault(data.basic.@class) ?? "",
                field_metadata = JsonSerializer.Serialize(fieldMetadata, SerializeOptions.Options),
                title,
                shop_id = data.basic.shopId,
                skills = JsonSerializer.Serialize(data.skill, SerializeOptions.Options),
            });
        }
    }
}
