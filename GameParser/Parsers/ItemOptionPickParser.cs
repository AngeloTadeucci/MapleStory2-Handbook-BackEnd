using System.Xml;
using Maple2.File.IO.Crypto.Common;
using Maple2Storage.Enums;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;

namespace GameParser.Parsers;

public static class ItemOptionPickParser {
    private static readonly Dictionary<int, ItemOptionPickMetadata> ItemOptionPick = [];

    static ItemOptionPickParser() {
        Dictionary<int, List<ItemOptionPick>> itemOptionPick = [];

        foreach (PackFileEntry? entry in Paths.XmlReader.Files) {
            if (!entry.Name.StartsWith("table/itemoptionpick")) {
                continue;
            }

            XmlDocument? innerDocument = Paths.XmlReader.GetXmlDocument(entry);
            XmlNodeList? nodeList = innerDocument.SelectNodes("/ms2/itemOptionPick");
            if (nodeList is null) {
                throw new("Failed to load itemoptionpick.xml");
            }

            foreach (XmlNode node in nodeList) {
                int id = int.Parse(node.Attributes?["optionPickID"]?.Value ?? "0");

                ItemOptionPick optionPick = new() {
                    Rarity = byte.Parse(node.Attributes?["itemGrade"]?.Value ?? "0")
                };

                //TODO: Add support for constant rates, random values, and random rates
                List<string>? constants = node.Attributes?["constant_value"]?.Value.Split(",").ToList();
                List<string>? staticValues = node.Attributes?["static_value"]?.Value.Split(",").ToList();
                List<string>? staticRates = node.Attributes?["static_rate"]?.Value.Split(",").ToList();

                if (constants is null || staticValues is null || staticRates is null) {
                    continue;
                }

                for (int i = 0; i < constants.Count; i += 2) {
                    if (constants[i] == "") {
                        continue;
                    }
                    ConstantPick constantPick = new() {
                        Stat = ParseItemOptionPickStat(constants[i]),
                        DeviationValue = int.Parse(constants[i + 1])
                    };
                    optionPick.Constants.Add(constantPick);
                }

                for (int i = 0; i < staticValues.Count; i += 2) {
                    if (staticValues[i] == "") {
                        continue;
                    }
                    StaticPick staticPick = new() {
                        Stat = ParseItemOptionPickStat(staticValues[i]),
                        DeviationValue = int.Parse(staticValues[i + 1])
                    };
                    optionPick.StaticValues.Add(staticPick);
                }

                for (int i = 0; i < staticRates.Count; i += 2) {
                    if (staticRates[i] == "") {
                        continue;
                    }
                    StaticPick staticPick = new() {
                        Stat = ParseItemOptionPickStat(staticRates[i]),
                        DeviationValue = int.Parse(staticRates[i + 1])
                    };
                    optionPick.StaticRates.Add(staticPick);
                }

                if (itemOptionPick.TryGetValue(id, out List<ItemOptionPick>? options)) {
                    options.Add(optionPick);
                } else {
                    itemOptionPick[id] =
                    [
                        optionPick
                    ];
                }
            }

            foreach ((int id, List<ItemOptionPick> itemOptions) in itemOptionPick) {
                ItemOptionPick.Add(id, new() {
                    Id = id,
                    ItemOptions = itemOptions
                });
            }
        }
    }

    public static ItemOptionPick? GetMetadata(int id, int rarity) {
        ItemOptionPickMetadata? metadata = ItemOptionPick.Values.FirstOrDefault(x => x.Id == id);
        return metadata?.ItemOptions.FirstOrDefault(x => x.Rarity == rarity);
    }

    private static StatAttribute ParseItemOptionPickStat(string stat) => stat switch {
        "str" => StatAttribute.Str,
        "dex" => StatAttribute.Dex,
        "int" => StatAttribute.Int,
        "luk" => StatAttribute.Luk,
        "hp" => StatAttribute.Hp,
        "hp_rgp" => StatAttribute.HpRegen,
        "hp_inv" => StatAttribute.HpRegenInterval,
        "sp" => StatAttribute.Spirit,
        "sp_rgp" => StatAttribute.SpRegen,
        "sp_inv" => StatAttribute.SpRegenInterval,
        "ep" => StatAttribute.Stamina,
        "ep_rgp" => StatAttribute.StaminaRegen,
        "ep_inv" => StatAttribute.StaminaRegenInterval,
        "asp" => StatAttribute.AttackSpeed,
        "msp" => StatAttribute.MovementSpeed,
        "atp" => StatAttribute.Accuracy,
        "evp" => StatAttribute.Evasion,
        "cap" => StatAttribute.CritRate,
        "cad" => StatAttribute.CritDamage,
        "car" => StatAttribute.CritEvasion,
        "ndd" => StatAttribute.Defense,
        "abp" => StatAttribute.PerfectGuard,
        "jmp" => StatAttribute.JumpHeight,
        "pap" => StatAttribute.PhysicalAtk,
        "map" => StatAttribute.MagicAtk,
        "par" => StatAttribute.PhysicalRes,
        "mar" => StatAttribute.MagicRes,
        "wapmin" => StatAttribute.MinWeaponAtk,
        "wapmax" => StatAttribute.MaxWeaponAtk,
        "dmg" => StatAttribute.MinDamage,
        "pen" => StatAttribute.Pierce,
        "rmsp" => StatAttribute.MountMovementSpeed,
        "bap" => StatAttribute.BonusAtk,
        "bap_pet" => StatAttribute.PetBonusAtk,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, $"Unhandled stat: {stat}")
    };
}
