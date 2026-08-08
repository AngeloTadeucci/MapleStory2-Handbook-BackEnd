using System.Xml;
using GameParser.Tools;
using Maple2Storage.Types;

namespace GameParser.Parsers;

public static class NpcTitleParser {
    public static readonly Dictionary<int, string> NpcTitle = [];

    static NpcTitleParser() {
        XmlDocument? xmlFile = Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/npctitle.xml")));

        if (xmlFile is null) {
            throw new("Failed to load npctitle.xml");
        }

        XmlNodeList? nodes = xmlFile.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load npctitle.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            NpcTitle[id] = node.Attributes?["name"]?.Value ?? "";
        }
    }
}
