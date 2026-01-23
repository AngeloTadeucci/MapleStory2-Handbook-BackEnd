using Maple2.File.IO;

namespace Maple2Storage.Types;

public static class Paths {
    public static readonly string SolutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    public static readonly string ScriptsDir = new(Path.Combine(SolutionDir, "Maple2Storage", "Scripts"));
    public static readonly M2dReader XmlReader = new(Path.Combine(SolutionDir, "Maple2Storage", "Resources", "Xml.m2d"));
    public static readonly M2dReader ExportedReader = new(Path.Combine(SolutionDir, "Maple2Storage", "Resources", "Exported.m2d"));
    public static readonly string ItemWebFinderXml = Path.Combine(SolutionDir, "Maple2Storage", "itemWebfinder.xml");
}
