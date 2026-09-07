using System.Xml.Linq;
using System.Numerics;
using System.Globalization;

namespace NifToGltf.Native;

internal sealed record ItemModelAttachment(string Slot, string SelfNode, string TargetNode, bool Replace, string[] Cutting) {
    public float[]? Translation { get; init; }
    public float[]? Rotation { get; init; }
    public string? AttachNode { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Matrix4x4 DummyTransform {
        get {
            if (Rotation?.Any(value => value != 0) == true) throw new NotSupportedException("Nonzero item dummy rotations require verified client rotation order.");
            return Translation is { } xyz ? Matrix4x4.CreateTranslation(xyz[0], xyz[1], xyz[2]) : Matrix4x4.Identity;
        }
    }
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
        XElement[] dummies = entry.Elements("dummy").Where(dummy => (string?)dummy.Attribute("gender") is null || (string?)dummy.Attribute("gender") == gender).ToArray();
        if (dummies.Length > 1) throw new InvalidDataException("Ambiguous item dummy transform.");
        float[]? Vector(string attribute) {
            if ((string?)dummies.SingleOrDefault()?.Attribute(attribute) is not { } value) return null;
            float[] numbers = value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            if (numbers.Length != 3 || numbers.Any(n => !float.IsFinite(n))) throw new InvalidDataException("Invalid item dummy vector.");
            return numbers;
        }
        string Required(XElement element, string attribute) => (string?) element.Attribute(attribute) is { Length: > 0 } value ? value : throw new InvalidDataException($"Missing item attachment {attribute}.");
        return new(Required(matches[0].slot, "name"), Required(entry, "selfnode"), Required(entry, "targetnode"),
            (string?) entry.Attribute("replace") == "1", items[0].Elements("cutting").Elements("mesh")
                .Where(mesh => (string?) mesh.Attribute("gender") is null || (string?) mesh.Attribute("gender") == gender)
                .Select(mesh => Required(mesh, "name")).ToArray()) { Translation = Vector("translation"), Rotation = Vector("rotation"), AttachNode = (string?)entry.Attribute("attachnode") };
    }
}
