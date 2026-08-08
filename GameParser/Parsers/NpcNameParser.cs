using System.Xml;
using GameParser.Tools;
using Maple2Storage.Types;

namespace GameParser.Parsers;

public static class NpcNameParser {
    public static readonly Dictionary<int, string> NpcNames = [];
    public static readonly Dictionary<int, string> NpcNamesPlural = [];
    public static readonly Dictionary<int, string> NpcTitles = [];

    static NpcNameParser() {
        XmlDocument? xmlFile = Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/npcname.xml")));
        XmlDocument? xmlFilePlural =
            Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/npcnameplural.xml")));
        XmlDocument? xmlFileTitles = Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/npctitle.xml")));

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
            NpcNames[id] = node.Attributes?["name"]?.Value ?? "";
        }

        nodes = xmlFilePlural.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load itemnameplural.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            NpcNamesPlural[id] = node.Attributes?["name"]?.Value ?? "";
        }

        nodes = xmlFileTitles.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load npctitle.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            NpcTitles[id] = node.Attributes?["name"]?.Value ?? "";
        }
    }
}
