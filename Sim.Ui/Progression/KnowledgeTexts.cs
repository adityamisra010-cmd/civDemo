using System.Text.Json;
using Sim.Core.State;

namespace Sim.Ui.Progression;

/// <summary>
/// M6 review (2026-10-23, the Director's item 4) — THE EDUCATIONAL TEXT of each research node, shown in the research
/// detail panel and the info card as plain prose: no citation, source badge, verification label or bibliography reaches
/// the screen. The text comes from Sim.Data's knowledge-texts.json (generated from the knowledge-redesign corpus); where it
/// came from and how far it has been checked stay in the repository (docs/m6-plan/knowledge-redesign/corpus,
/// corpus/unresolved-claims.md) for internal quality control — showing a text does not make it fact-checked. Looked up by
/// the node's stable research.json key (resolved once from research.json's ids); a node without a text has none. Loaded
/// once, immutable, sorted arrays with binary search (no dictionary).
/// </summary>
public static class KnowledgeTexts
{
    private static readonly Lazy<(int[] Keys, string[] Texts)> Table = new(Load);

    /// <summary>The educational text of the node with <paramref name="key"/>, or null.</summary>
    public static string? For(ResearchNodeId key)
    {
        (int[] keys, string[] texts) = Table.Value;
        int i = Array.BinarySearch(keys, key.Value);
        return i >= 0 ? texts[i] : null;
    }

    /// <summary>How many nodes have a text.</summary>
    public static int Count => Table.Value.Keys.Length;

    private static (int[] Keys, string[] Texts) Load()
    {
        var keyOfId = new List<(string Id, int Key)>();
        using (Stream research = Sim.Data.DataFiles.OpenResearch())
        using (JsonDocument doc = JsonDocument.Parse(research))
        {
            foreach (string tree in new[] { "technologies", "civics" })
                foreach (JsonElement n in doc.RootElement.GetProperty(tree).EnumerateArray())
                    keyOfId.Add((n.GetProperty("id").GetString()!, n.GetProperty("key").GetInt32()));
        }
        keyOfId.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        string[] ids = new string[keyOfId.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = keyOfId[i].Id;

        var rows = new List<(int Key, string Text)>();
        using (Stream texts = Sim.Data.DataFiles.OpenKnowledgeTexts())
        using (JsonDocument doc = JsonDocument.Parse(texts))
        {
            foreach (JsonElement t in doc.RootElement.GetProperty("texts").EnumerateArray())
            {
                string id = t.GetProperty("id").GetString()!;
                int at = Array.BinarySearch(ids, id, StringComparer.Ordinal);
                if (at < 0) throw new InvalidDataException($"knowledge-texts.json names '{id}', which is not a research.json node.");
                rows.Add((keyOfId[at].Key, t.GetProperty("text").GetString()!));
            }
        }
        rows.Sort((a, b) => a.Key.CompareTo(b.Key));
        int[] keys = new int[rows.Count];
        string[] values = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++) { keys[i] = rows[i].Key; values[i] = rows[i].Text; }
        return (keys, values);
    }
}
