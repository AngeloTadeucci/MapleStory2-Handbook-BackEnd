using Maple2.File.Parser;
using Maple2.File.Parser.Tools;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class FurnishingShopParser {
    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        TableParser parser = new(Paths.XmlReader, "en");

        var entries = parser.ParseFurnishingShopUgcAll()
            .Concat(parser.ParseFurnishingShopMaid())
            .GroupBy(entry => entry.Item1)
            .Select(group => group.First())
            .ToList();

        Console.WriteLine($"Parsing {entries.Count} furnishing shop entries...");

        int current = 0;
        foreach (var entry in entries) {
            current++;
            if (current % 100 == 0 || current == entries.Count) {
                Console.WriteLine($"Parsing furnishing shop: {current}/{entries.Count}");
            }

            QueryManager.QueryFactory.Query("furnishing_shop").Insert(new {
                item_id = entry.Item1,
                buyable = entry.Item2.ugcHousingBuy,
                token_type = (int) entry.Item2.ugcHousingMoneyType,
                price = entry.Item2.ugcHousingDefaultPrice,
            });
        }
    }
}
