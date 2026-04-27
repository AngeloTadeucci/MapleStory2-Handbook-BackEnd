using Maple2.File.Parser;
using Maple2.File.Parser.Xml.Table.Server;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class ServerDropParser {
    public static void Parse() {
        ServerTableParser parser = new(Paths.ServerReader);

        var dropBoxes = parser.ParseIndividualItemDrop().ToList();
        int total = dropBoxes.Sum(box => box.IndividualItemDrop.group.Sum(g => g.v.Count));
        int current = 0;

        Console.WriteLine($"Parsing {total} drop entries from {dropBoxes.Count} drop boxes...");

        foreach ((int boxId, IndividualItemDrop dropBox) in dropBoxes) {
            foreach (IndividualItemDrop.Group group in dropBox.group) {
                foreach (IndividualItemDrop.Group.Item item in group.v) {
                    current++;
                    if (current % 5000 == 0 || current == total) {
                        Console.WriteLine($"Parsing drops: {current}/{total}");
                    }

                    int minCount = item.minCount <= 0 ? 1 : item.minCount;
                    int maxCount = item.maxCount < minCount ? minCount : item.maxCount;
                    short rarity = item.grade.Length > 0 ? item.grade[0] : item.uiItemRank;
                    if (rarity <= 0) {
                        rarity = 1;
                    }

                    QueryManager.QueryFactory.Query("drop_box_items").Insert(new {
                        drop_box_id = boxId,
                        group_id = group.dropGroupID,
                        item_id = item.itemID,
                        item_id2 = item.itemID2,
                        min_count = minCount,
                        max_count = maxCount,
                        weight = item.weight,
                        rarity = (int) rarity,
                        smart_drop_rate = group.smartDropRate,
                        enchant_level = item.enchantLevel,
                    });
                }
            }
        }
    }
}
