using System.Xml;
using M2dXmlGenerator;

namespace GameParser.Tools;

public static class StringTable {
    /// <summary>
    /// String tables hold several entries for the same id, gated by feature and locale. Resolves them the
    /// way the client does: a matching locale beats an empty one, and among those the highest enabled
    /// feature version wins, with an unspecified feature ranking lowest. Entries whose feature is disabled
    /// or whose locale does not match are dropped. Requires Filter.Load to have run.
    /// </summary>
    public static IEnumerable<(int Id, XmlNode Node)> Resolve(XmlNodeList nodes) {
        Dictionary<int, Entry> resolved = [];

        foreach (XmlNode node in nodes) {
            if (!int.TryParse(node.Attributes?["id"]?.Value, out int id) || id == 0) {
                continue;
            }

            string feature = node.Attributes["feature"]?.Value ?? "";
            string locale = node.Attributes["locale"]?.Value ?? "";

            if (!FeatureLocaleFilter.FeatureEnabled(feature) || !FeatureLocaleFilter.HasLocale(locale)) {
                continue;
            }

            Entry entry = new(node, string.IsNullOrEmpty(locale) ? 0 : 1, string.IsNullOrEmpty(feature) ? -1 : FeatureLocaleFilter.Features[feature], feature);

            if (resolved.TryGetValue(id, out Entry? current) && !entry.Beats(current)) {
                continue;
            }

            resolved[id] = entry;
        }

        return resolved.Select(entry => (entry.Key, entry.Value.Node));
    }

    private sealed record Entry(XmlNode Node, int LocaleRank, int Version, string Feature) {
        public bool Beats(Entry other) {
            if (LocaleRank != other.LocaleRank) {
                return LocaleRank > other.LocaleRank;
            }

            if (Version != other.Version) {
                return Version > other.Version;
            }

            // Same version: the client breaks the tie on feature name ordering.
            return string.CompareOrdinal(Feature, other.Feature) > 0;
        }
    }
}
