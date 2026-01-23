namespace GameParser;

/// <summary>
/// Static lookup for NPC tags to NPC IDs.
/// Built during NpcParser, used during mob spawn parsing.
/// </summary>
public static class NpcTagLookup {
    private const int MOB_BASE_ID = 20000000;

    public static Dictionary<string, HashSet<int>> TagToNpcIds { get; } = new();

    public static void AddNpc(int npcId, string[] mainTags) {
        // Only process mobs (ID > 20000000)
        if (npcId <= MOB_BASE_ID) {
            return;
        }

        foreach (string tag in mainTags) {
            if (string.IsNullOrWhiteSpace(tag)) {
                continue;
            }

            if (!TagToNpcIds.ContainsKey(tag)) {
                TagToNpcIds[tag] = new HashSet<int>();
            }

            TagToNpcIds[tag].Add(npcId);
        }
    }

    public static HashSet<int> GetNpcIdsForTags(string[] tags) {
        var npcIds = new HashSet<int>();

        foreach (string tag in tags) {
            if (TagToNpcIds.TryGetValue(tag, out HashSet<int>? ids)) {
                foreach (int id in ids) {
                    npcIds.Add(id);
                }
            }
        }

        return npcIds;
    }
}
