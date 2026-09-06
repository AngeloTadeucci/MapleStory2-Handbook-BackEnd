using System.Xml.Linq;

namespace NifToGltf.Native;

internal sealed record ItemModelAttachment(string Slot, string SelfNode, string TargetNode, bool Replace, string[] Cutting) {
    public static ItemModelAttachment Read(string xml, string itemId, string source, string variant, string? slot = null) {
        string gender = variant switch { "male" => "0", "female" => "1", _ => throw new ArgumentException("Item attachment requires male or female bodyVariant.") };
        XElement[] items = XDocument.Load(xml).Root!.Elements("ItemModel").Where(item => (string?) item.Attribute("id") == itemId).ToArray();
        if (items.Length != 1) throw new InvalidDataException($"Expected one item model {itemId}, found {items.Length}.");
        string Name(string value) => Path.GetFileNameWithoutExtension(value.Replace('\\', '/').Replace("urn:", ""));
        var matches = items[0].Elements("slots").Elements("slot").SelectMany(slot => slot.Elements("asset").Select(asset => (slot, asset)))
            .Where(pair => (slot is null || (string?) pair.slot.Attribute("name") == slot) &&
                ((string?) pair.asset.Attribute("gender") is null || (string?) pair.asset.Attribute("gender") == gender) &&
                Name((string?) pair.asset.Attribute("name") ?? "").Equals(Name(source), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"Expected one {variant} item asset matching {Path.GetFileName(source)}, found {matches.Length}.");
        XElement entry = matches[0].asset;
        string Required(XElement element, string attribute) => (string?) element.Attribute(attribute) is { Length: > 0 } value ? value : throw new InvalidDataException($"Missing item attachment {attribute}.");
        return new(Required(matches[0].slot, "name"), Required(entry, "selfnode"), Required(entry, "targetnode"),
            (string?) entry.Attribute("replace") == "1", items[0].Elements("cutting").Elements("mesh")
                .Where(mesh => (string?) mesh.Attribute("gender") is null || (string?) mesh.Attribute("gender") == gender)
                .Select(mesh => Required(mesh, "name")).ToArray());
    }
}
