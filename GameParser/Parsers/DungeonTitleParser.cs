using System.Xml;
using GameParser.Tools;
using Maple2Storage.Types;

namespace GameParser.Parsers;

public static class DungeonTitleParser {
    public static readonly Dictionary<int, (string name, string uiDescription)> DungeonTitleNames = [];

    static DungeonTitleParser() {
        XmlDocument? xmlFile =
            Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/stringfieldenterance.xml")));

        if (xmlFile is null) {
            throw new("Failed to load stringfieldenterance.xml");
        }

        XmlNodeList? nodes = xmlFile.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load stringfieldenterance.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            DungeonTitleNames[id] = (node.Attributes?["name"]?.Value ?? "", node.Attributes?["uiDescription"]?.Value ?? "");
        }
    }
}
