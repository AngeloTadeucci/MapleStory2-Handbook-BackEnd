using System.Diagnostics;
using System.Text.Json;
using GameParser.DescriptionHelper;
using M2dXmlGenerator;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.Quest;
using Maple2Storage.Enums;
using Maple2Storage.Types;
using SqlKata.Execution;

namespace GameParser.Parsers;

public class QuestParser {
    public static void Parse() {
        Filter.Load(Paths.XmlReader, "NA", "Live");
        Maple2.File.Parser.QuestParser parser = new(Paths.XmlReader, "en");

        foreach ((int Id, string Name, QuestData Data) in parser.Parse()) {
            QuestNameParser.QuestNames.TryGetValue(Id, out QuestNameParser.QuestDescription? questDescription);
            string name = Helper.FixDescription(Name) ?? "";
            if (questDescription is not null) {
                name = questDescription?.Name ?? "";
            }
            string description = questDescription?.Description ?? "";
            string manualDescription = questDescription?.Manual ?? "";
            string completeDescription = questDescription?.Complete ?? "";

            Console.WriteLine($"Parsing quest {Id} - {name}");

            QueryManager.QueryFactory.Query("quests").Insert(new {
                id = Id,
                name,
                description,
                manualDescription,
                completeDescription,
                Data.basic.questType,
                questLevel = Data.basic.standardLevel,
                requiredLevel = Data.require.level,
                requiredQuest = JsonSerializer.Serialize(Data.require.quest),
                selectableQuest = JsonSerializer.Serialize(Data.require.selectableQuest),
                startNpcId = Data.start?.npc ?? 0,
                completeNpcId = Data.complete?.npc ?? 0,
                startRewards = JsonSerializer.Serialize(Convert(Data.acceptReward)),
                completeRewards = JsonSerializer.Serialize(Convert(Data.completeReward)),
            });

            // Insert quest-map relationships
            ParseQuestMaps(Id, Data);
        }
    }

    private static QuestMetadataReward Convert(Reward reward) {
        List<Reward.Item> essentialItem = reward.essentialItem;
        List<Reward.Item> essentialJobItem = reward.essentialJobItem;
        if (FeatureLocaleFilter.FeatureEnabled("GlobalQuestRewardItem")) {
            essentialItem = reward.globalEssentialItem.Count > 0 ? reward.globalEssentialItem : essentialItem;
            essentialJobItem = reward.globalEssentialJobItem.Count > 0 ? reward.globalEssentialJobItem : essentialJobItem;
        }

        return new QuestMetadataReward(
            Meso: reward.money,
            Exp: reward.exp,
            RelativeExp: ToExpType(reward.relativeExp),
            GuildFund: reward.guildFund,
            GuildExp: reward.guildExp,
            GuildCoin: reward.guildCoin,
            Treva: reward.karma,
            Rue: reward.lu,
            MenteeCoin: reward.menteeCoin,
            MissionPoint: reward.missionPoint,
            EssentialItem: essentialItem.Select(item =>
                new QuestMetadataReward.Item(item.code, item.rank, item.count)).Where(x => x.Id != 0).ToList(),
            EssentialJobItem: essentialJobItem.Select(item =>
                new QuestMetadataReward.Item(item.code, item.rank, item.count)).Where(x => x.Id != 0).ToList()
        );
    }

    public record QuestMetadataReward(
    int Meso,
    int Exp,
    ExpType RelativeExp,
    int GuildFund,
    int GuildExp,
    int GuildCoin,
    int MenteeCoin,
    int MissionPoint,
    int Treva,
    int Rue,
    List<QuestMetadataReward.Item> EssentialItem,
    List<QuestMetadataReward.Item> EssentialJobItem) {

        public record Item(int Id, int Rarity, int Amount);
    }

    private static ExpType ToExpType(Maple2.File.Parser.Enum.RelativeExp commonExpType) {
        if (Enum.TryParse(commonExpType.ToString(), out ExpType expType)) {
            return expType;
        }
        return ExpType.none;
    }

    private static void ParseQuestMaps(int questId, QuestData questData) {
        // Extract map IDs from ProgressMap
        if (questData.progressMap?.progressMap != null && questData.progressMap.progressMap.Length > 0) {
            foreach (int mapId in questData.progressMap.progressMap) {
                if (mapId == 0) continue; // Skip invalid map IDs

                try {
                    QueryManager.QueryFactory.Query("quest_maps").Insert(new {
                        quest_id = questId,
                        map_id = mapId
                    });
                } catch {
                    // Skip duplicates (unique index will prevent)
                }
            }
        }

        // Also check complete.map array for additional map associations
        if (questData.complete?.map != null && questData.complete.map.Length > 0) {
            foreach (int mapId in questData.complete.map) {
                if (mapId == 0) continue; // Skip invalid map IDs

                try {
                    QueryManager.QueryFactory.Query("quest_maps").Insert(new {
                        quest_id = questId,
                        map_id = mapId
                    });
                } catch {
                    // Skip duplicates (unique index will prevent)
                }
            }
        }
    }
}
