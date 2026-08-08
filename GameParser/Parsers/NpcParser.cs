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

    // Root of the exported image tree, i.e. the folder that holds portrait/. Used only to correct
    // portrait folders; leave it unset in .env and portrait paths are written exactly as the XML
    // declares them.
    private static readonly string? ImageDir = Environment.GetEnvironmentVariable("IMAGE_DIR");

    private static int portraitFolderFixes;
    private static int portraitNameFixes;
    private static int portraitModelFixes;

    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.NpcParser parser = new(Paths.XmlReader, "en");
        portraitFolderFixes = 0;
        portraitNameFixes = 0;
        portraitModelFixes = 0;

        var npcs = parser.Parse().ToList();
        int total = npcs.Count;
        int current = 0;

        Console.WriteLine($"Parsing {total} NPCs...");

        foreach ((int id, string? name, NpcData? data, List<EffectDummy> dummy) in npcs) {
            current++;
            if (current % 100 == 0 || current == total) {
                Console.WriteLine($"Parsing NPCs: {current}/{total}");
            }

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

            string kfm = data.model.kfm.ToLower();

            portrait = FixPortrait(portrait, kfm);

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

            foreach (int boxId in data.dropiteminfo.individualDropBoxId.Distinct()) {
                if (boxId == 0) {
                    continue;
                }

                QueryManager.QueryFactory.Query("npc_drop_boxes").Insert(new {
                    npc_id = id,
                    drop_box_id = boxId,
                    drop_type = 0,
                });
            }

            foreach (int boxId in data.dropiteminfo.individualHitDropBoxId.Distinct()) {
                if (boxId == 0) {
                    continue;
                }

                QueryManager.QueryFactory.Query("npc_drop_boxes").Insert(new {
                    npc_id = id,
                    drop_box_id = boxId,
                    drop_type = 1,
                });
            }
        }

        Console.WriteLine(ImageDir is null
            ? "IMAGE_DIR is not set, portraits were left as the XML declares them."
            : $"Corrected the portrait folder of {portraitFolderFixes} NPC(s), the portrait name of {portraitNameFixes} NPC(s) " +
              $"and the portrait art id of {portraitModelFixes} NPC(s).");
    }

    // The npc XML does not always declare the portrait that the client exports. Two mismatches
    // occur, so correct each one only when the declared file is absent and the replacement exists:
    //   - wrong folder: the portrait is declared under mob/ but sits under npc/, or the reverse.
    //     Both directions occur and 178 file names exist in both folders, so never move a portrait
    //     whose declared file is present.
    //   - wrong name: the portrait is named after the art id, while the exported file is named
    //     after the kfm. Blue Rook declares 44110001_b_blueluke01_p.png and the only file that
    //     exists is 23000400_b_blueluke_p.png.
    //   - wrong art id: the declared name belongs to a different NPC altogether. Della Rosa
    //     declares Blue Rook's portrait, and its own sits under the art id of the base model:
    //     the kfm is 44100001_b_primadonna_default and the file is 41110001_b_primadonna_p.png.
    private static string FixPortrait(string portrait, string kfm) {
        const string prefix = "./data/resource/image/";
        if (ImageDir is null || !portrait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
            return portrait;
        }

        string[] segments = portrait[prefix.Length..].Split('/');
        if (segments.Length < 3 || segments[^1].Length == 0 || Exists(segments)) {
            return portrait;
        }

        string folder = segments[^2];
        string sibling = folder switch {
            "mob" => "npc",
            "npc" => "mob",
            _ => "",
        };

        if (sibling.Length != 0) {
            string[] siblingSegments = [.. segments];
            siblingSegments[^2] = sibling;
            if (Exists(siblingSegments)) {
                portraitFolderFixes++;
                return prefix + string.Join('/', siblingSegments);
            }
        }

        if (kfm.Length == 0) {
            return portrait;
        }

        string kfmName = kfm.EndsWith(".kfm", StringComparison.OrdinalIgnoreCase) ? kfm[..^4] : kfm;
        string[] folders = sibling.Length == 0 ? [folder] : [folder, sibling];

        string[] byKfm = [.. segments];
        byKfm[^1] = $"{kfmName}_p.png";
        foreach (string candidateFolder in folders) {
            byKfm[^2] = candidateFolder;
            if (Exists(byKfm)) {
                portraitNameFixes++;
                return prefix + string.Join('/', byKfm);
            }
        }

        // Every portrait is named <artId>_<model>_p.png and so is every kfm, but the two art ids
        // need not agree. Drop the art id and look the model up by name instead. A kfm can also
        // name a variant of a model that has no portrait of its own (..._dark, ..._turned,
        // ..._default), so drop trailing parts as well, nearest match first.
        int idEnd = kfmName.IndexOf('_');
        if (idEnd <= 0 || !kfmName[..idEnd].All(char.IsAsciiDigit)) {
            return portrait;
        }

        string model = kfmName[(idEnd + 1)..];
        for (int dropped = 0; dropped <= 3; dropped++) {
            string[] byModel = [.. segments];
            foreach (string candidateFolder in folders) {
                byModel[^2] = candidateFolder;
                string? match = FindByModel(byModel, model);
                if (match is null) {
                    continue;
                }

                byModel[^1] = match;
                portraitModelFixes++;
                return prefix + string.Join('/', byModel);
            }

            int lastPart = model.LastIndexOf('_');
            if (lastPart < 1) {
                break;
            }

            model = model[..lastPart];
        }

        return portrait;

        static bool Exists(string[] segments) => File.Exists(Path.Combine([ImageDir!, .. segments]));

        // The one file in this folder named after the given model, whatever art id it carries.
        static string? FindByModel(string[] segments, string model) {
            string directory = Path.Combine([ImageDir!, .. segments[..^1]]);
            if (!Directory.Exists(directory)) {
                return null;
            }

            string suffix = $"_{model}_p.png";
            string? found = null;
            foreach (string file in Directory.EnumerateFiles(directory, $"*{suffix}")) {
                string name = Path.GetFileName(file);
                if (name.Length <= suffix.Length || !name[..^suffix.Length].All(char.IsAsciiDigit)) {
                    continue;
                }

                if (found is not null) {
                    // Two art ids for one model: no way to tell which one this NPC uses.
                    return null;
                }

                found = name;
            }

            return found;
        }
    }
}
