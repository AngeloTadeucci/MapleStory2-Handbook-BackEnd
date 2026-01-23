using System.Text.Json;
using System.Text.RegularExpressions;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.AdditionalEffect;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public static class AdditionalEffectParser {
    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.AdditionalEffectParser parser = new(Paths.XmlReader);

        var effects = parser.Parse().ToList();
        int total = effects.Count;
        int current = 0;
        int processed = 0;

        Console.WriteLine($"Parsing additional effects...");

        foreach ((int id, IList<AdditionalEffectData> Data) in effects) {
            current++;

            AdditionalEffectDescriptionParser.additionalEffectNames.TryGetValue(id, out List<(int level, string name, string tooltipDescription)>? list);
            if (list == null) {
                continue;
            }

            processed++;
            if (processed % 50 == 0) {
                Console.WriteLine($"Parsing additional effects: {processed} parsed ({current}/{total} processed)");
            }

            string name = list.First().name;

            short[] levels = Data.Select(x => x.BasicProperty.level).ToArray();
            string description = CombineDescriptionsWithDifferences(list.Select(x => x.tooltipDescription).ToList());

            QueryManager.QueryFactory.Query("additional_effects").Insert(new {
                id,
                icon_path = Data.FirstOrDefault()?.UIProperty.icon.Split('/').Last().ToLower() ?? "",
                name,
                description,
                levels = JsonSerializer.Serialize(levels),
            });
        }

        Console.WriteLine($"Completed parsing {processed} additional effects");
    }

    // I liked when chatgpt came and gipity gopity all over the place
    public static string CombineDescriptionsWithDifferences(List<string> descriptions) {
        List<List<float>> aggregatedNumbers = [];

        foreach (string description in descriptions) {
            var numbers = ExtractNumbers(description);
            aggregatedNumbers.Add(numbers);
        }

        string mergedDescriptions = MergeDescriptions(descriptions, aggregatedNumbers);
        return mergedDescriptions;
    }

    public static List<float> ExtractNumbers(string description) {
        return Regex.Matches(description, @"\d+(\.\d+)?")
                    .Cast<Match>()
                    .Select(m => float.Parse(m.Value))
                    .ToList();
    }

    public static string MergeDescriptions(List<string> descriptions, List<List<float>> aggregatedNumbers) {
        List<string> uniqueNumbers = [];

        // Determine the maximum number of valid iterations based on the smallest list in aggregatedNumbers
        int maxIterations = aggregatedNumbers.Min(numbers => numbers.Count);

        for (int i = 0; i < maxIterations; i++) {
            List<float> numbersAtPosition = aggregatedNumbers.Select(numbers => numbers[i]).Distinct().ToList();

            if (numbersAtPosition.Count > 5 && IsLinear(numbersAtPosition)) {
                numbersAtPosition = [numbersAtPosition.First(), numbersAtPosition.Last()];
                uniqueNumbers.Add(string.Join(" ~ ", numbersAtPosition));
            } else {
                uniqueNumbers.Add(string.Join("/", numbersAtPosition));
            }
        }

        // Generate placeholders based on the original descriptions
        string result = descriptions[0];
        List<Match> placeholders = Regex.Matches(result, @"\d+(\.\d+)?").Cast<Match>().ToList();

        // Replace placeholders in the original description with unique numbers
        for (int i = placeholders.Count - 1; i >= 0; i--) {
            var placeholder = placeholders[i];
            if (i < uniqueNumbers.Count) {
                string replacement = uniqueNumbers[i];
                result = result.Remove(placeholder.Index, placeholder.Length).Insert(placeholder.Index, replacement);
            }
        }

        return result;
    }

    public static bool IsLinear(List<float> numbers) {
        if (numbers.Count < 2) return false;

        float tolerance = 0.0001f; // Define a tolerance level for floating-point comparisons
        float difference = numbers[1] - numbers[0];

        for (int i = 1; i < numbers.Count - 1; i++) {
            if (Math.Abs(numbers[i + 1] - numbers[i] - difference) > tolerance) {
                return false;
            }
        }

        return true;
    }
}
