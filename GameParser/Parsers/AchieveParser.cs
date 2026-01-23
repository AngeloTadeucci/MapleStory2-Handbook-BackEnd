using System.Xml;
using Maple2Storage.Types;
using SqlKata.Execution;
using GameParser.DescriptionHelper;
using Maple2.File.Parser.Xml.Achieve;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Enum;
using System.Text.Json;
using GameParser.Tools;

namespace GameParser.Parsers;

public static class AchieveParser {
    public static Dictionary<int, string> AchieveNames { get; private set; } = new Dictionary<int, string>();

    public static void Parse() {
        Dictionary<int, (string description, string complete_description)> descriptions = ParseAchieveDescriptions();

        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.AchieveParser parser = new(Paths.XmlReader);

        var achieves = parser.Parse().ToList();
        int total = achieves.Count;
        int current = 0;

        Console.WriteLine($"Parsing {total} achievements...");

        foreach ((int id, string? name, AchieveData data) in achieves) {
            current++;
            if (current % 100 == 0 || current == total) {
                Console.WriteLine($"Parsing achievements: {current}/{total}");
            }

            string fixedName = name ?? "";
            if (name is not null) {
                fixedName = Helper.FixDescription(name);
            }

            AchieveNames[id] = fixedName;

            bool descriptionExists = descriptions.TryGetValue(id, out (string description, string complete_description) value);

            string description = "";
            string complete_description = "";
            if (descriptionExists) {
                description = value.description;
                complete_description = value.complete_description;
            }

            List<GradeStruct> rewards = [];

            foreach (Grade? grade in data.grade) {
                int gradeValue = grade.value;
                ConditionType conditionType = grade.condition.type;
                string[] conditionCode = grade.condition.code;
                long conditionValue = grade.condition.value;
                RewardType rewardType = (RewardType) grade.reward.type;
                int rewardId = grade.reward.code;

                switch (rewardType) {
                    case RewardType.item:
                    case RewardType.shop_weapon:
                    case RewardType.shop_build:
                    case RewardType.shop_ride:

                        if (!ItemNameParser.ItemNames.TryGetValue(rewardId, out string? itemName)) {
                            itemName = rewardId.ToString();
                        }

                        if (rewardType is RewardType.item) {
                            if (grade.reward.value > 0) {
                                itemName += $" x{grade.reward.value}";
                            }
                        } else {
                            itemName += " Unlocked for Purchase";
                        }

                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, itemName));
                        break;
                    case RewardType.beauty_makeup:
                    case RewardType.beauty_skin:
                    case RewardType.beauty_hair:
                        List<string> rewardsNames = [];
                        foreach (string reward in grade.reward.extra) {
                            string[] gender = reward.Split(":");
                            int beautyId = int.Parse(gender.Last());
                            if (!ItemNameParser.ItemNames.TryGetValue(beautyId, out string? rewardName)) {
                                rewardName = reward.ToString();
                            }

                            rewardsNames.Add(reward + ":" + rewardName);
                        }
                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, string.Join(",", rewardsNames)));
                        break;
                    case RewardType.title:
                        if (!TitleNameParser.TitleNames.TryGetValue(rewardId, out string? titleName)) {
                            titleName = rewardId.ToString();
                        }

                        titleName += " Title";

                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, titleName));
                        break;
                    case RewardType.statpoint:
                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, $"Attribute Point x{grade.reward.value}"));
                        break;
                    case RewardType.skillpoint:
                        if (grade.reward.subJobLevel > 0) {
                            rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, $"{grade.reward.value} Rank 2 Skill Points"));
                        } else {
                            rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, $"Skill Point x{grade.reward.value}"));
                        }
                        break;
                    case RewardType.dynamicaction:
                        if (!SkillNameParser.SkillNames.TryGetValue(rewardId, out string? skillName)) {
                            skillName = rewardId.ToString();
                        }

                        skillName += " Emote Unlocked";

                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, skillName));
                        break;
                    default:
                        rewards.Add(new GradeStruct(gradeValue, conditionType, conditionValue, conditionCode, rewardType, rewardId, rewardId.ToString()));
                        break;
                }
            }

            QueryManager.QueryFactory.Query("achieves").Insert(new {
                id,
                name = fixedName,
                description,
                complete_description,
                icon = data.icon.ToLower(),
                grades = JsonSerializer.Serialize(rewards, SerializeOptions.Options)
            });
        }
    }

    private static Dictionary<int, (string description, string complete_description)> ParseAchieveDescriptions() {
        Dictionary<int, (string description, string complete_description)> descriptions = [];
        XmlDocument? xmlFile =
            Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/achievedescription.xml")));

        if (xmlFile is null) {
            throw new("Failed to load achievedescription.xml");
        }

        XmlNodeList? nodes = xmlFile.SelectNodes("/ms2/achieve");
        if (nodes is null) {
            throw new("Failed to load achievedescription.xml");
        }
        foreach (XmlNode node in nodes) {
            int id = int.Parse(node.Attributes?["id"]?.Value ?? "0");
            if (id == 0) {
                continue;
            }

            if (descriptions.ContainsKey(id)) {
                continue;
            }

            string description = Helper.FixDescription(node.Attributes?["desc"]?.Value ?? "");
            string complete_description = Helper.FixDescription(node.Attributes?["complete"]?.Value ?? "");

            if (string.IsNullOrEmpty(description)) {
                description = Helper.FixDescription(node.Attributes?["manualDesc"]?.Value ?? "");
                complete_description = Helper.FixDescription(node.Attributes?["manualComplete"]?.Value ?? "");
            }

            descriptions[id] = (description, complete_description);
        }

        return descriptions;
    }
}

internal record GradeStruct(int grade, ConditionType conditionType, long conditionValue, string[] conditionCode, RewardType rewardType, int rewardId, string readableReward);

internal enum RewardType {
    unknown = 0,
    item = 1,
    title = 2,
    statpoint = 3,
    skillpoint = 4,
    shop_weapon = 5,
    shop_build = 6,
    shop_ride = 7,
    itemcoloring = 8,
    beauty_makeup = 9,
    beauty_skin = 10,
    beauty_hair = 11,
    dynamicaction = 12,
    etc = 12
};
