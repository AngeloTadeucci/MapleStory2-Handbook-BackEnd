using System.Xml;
using GameParser.Tools;
using Maple2Storage.Types;

namespace GameParser.Parsers;

public static class TitleNameParser {
    public static readonly Dictionary<int, string> TitleNames = [];

    static TitleNameParser() {
        XmlDocument? xmlFile =
            Paths.XmlReader.GetXmlDocument(Paths.XmlReader.Files.First(x => x.Name.StartsWith("string/en/titlename.xml")));

        if (xmlFile is null) {
            throw new("Failed to load titlename.xml");
        }

        XmlNodeList? nodes = xmlFile.SelectNodes("/ms2/key");
        if (nodes is null) {
            throw new("Failed to load titlename.xml");
        }
        foreach ((int id, XmlNode node) in StringTable.Resolve(nodes)) {
            TitleNames[id] = node.Attributes?["name"]?.Value ?? "";
        }
    }
}
