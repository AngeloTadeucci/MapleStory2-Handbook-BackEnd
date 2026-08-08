using System.Xml;
using GameParser.Tools;
using Maple2Storage.Types;

namespace GameParser.Parsers;

public static class ItemNameParser {
    public static readonly Dictionary<int, string> ItemNames = [];
    public static readonly Dictionary<int, string> ItemNamesPlural = [];

    static ItemNameParser() {
        XmlDocument? xmlFile = Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/itemname.xml")));
        XmlDocument? xmlFilePlural =
            Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/itemnameplural.xml")));

        if (xmlFile is null) {
            throw new("Failed to load itemname.xml");
        }

        if (xmlFilePlural is null) {
            throw new("Failed to load itemnameplural.xml");
        }

        XmlNodeList? nodes = xmlFile.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load itemname.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            ItemNames[id] = node.Attributes?["name"]?.Value ?? "";
        }

        nodes = xmlFilePlural.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load itemnameplural.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            ItemNamesPlural[id] = node.Attributes?["name"]?.Value ?? "";
        }
    }
}
