using System.Text.Json.Nodes;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// D-066 A9 (track KNOW, 2026-10-28) — THE ENTITY DISPOSITION REGISTER, docs/m6-plan/deferred-entity-dispositions.md:
/// every research.json entity exactly once, with a disposition the content agrees with. "Realized" requires a consumer
/// link the build enforces (the per-consumer-type rule of scripts/research-gameplay-unlock-audit.py, read here from the
/// same content files: goods.json recipes, projects and founds; sim.json road classes and trade.entity; research.json
/// sectorActivities; unit-families.json realizedBy). "Deferred" requires that no consumer exists and that the row names an
/// owner and a target milestone. The law court (inst.law_code) is the M6 slice entity track CIV realizes (D-066 A2): it is
/// accepted with or without a consumer, so the register holds before and after that track lands.
/// </summary>
public class EntityDispositionRegisterTests
{
    private static JsonObject Content(Func<Stream> open)
    {
        using Stream s = open();
        return JsonNode.Parse(s)!.AsObject();
    }

    /// <summary>The entity ids some enforced consumer links to (test-side set; not sim logic).</summary>
    private static HashSet<string> Consumed()
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        JsonObject goods = Content(Sim.Data.DataFiles.OpenGoods), sim = Content(Sim.Data.DataFiles.OpenSim);
        JsonObject research = Content(Sim.Data.DataFiles.OpenResearch), families = Content(Sim.Data.DataFiles.OpenUnitFamilies);
        void Add(JsonNode? id) { if (id is not null && (string?)id is { } s) consumed.Add(s); }
        foreach (JsonNode? r in goods["recipes"]!.AsArray()) Add(r!["entity"]);
        foreach (JsonNode? p in goods["projects"]!.AsArray()) { Add(p!["entity"]); Add(p["founds"]?["entity"]); }
        foreach (JsonNode? c in sim["roads"]!["classes"]!.AsArray()) Add(c!["entity"]);
        Add(sim["trade"]!["entity"]);
        foreach (JsonNode? sa in research["sectorActivities"]!.AsArray())
            foreach (JsonNode? rr in sa!["researched"]!.AsArray()) Add(rr!["entity"]);
        foreach (JsonNode? f in families["families"]!.AsArray())
            foreach (JsonNode? i in f!["line"]!.AsArray()) Add(i!["realizedBy"]);
        return consumed;
    }

    [Fact]
    public void Register_ListsEveryResearchEntityExactlyOnce_WithTheAuditsRealization()
    {
        string path = Path.Combine(RepoPaths.Root(), "docs", "m6-plan", "deferred-entity-dispositions.md");
        var rows = new List<string[]>();
        bool register = false;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal)) register = line.StartsWith("## 2. Register", StringComparison.Ordinal);
            if (!register || !line.StartsWith("| `", StringComparison.Ordinal)) continue;
            string[] cells = line.Split('|');
            rows.Add([.. cells.Skip(1).Take(cells.Length - 2).Select(c => c.Trim())]);
        }

        JsonObject research = Content(Sim.Data.DataFiles.OpenResearch);
        var kinds = new Dictionary<string, (string Kind, bool Gated)>(StringComparer.Ordinal);   // test-side lookup
        foreach (JsonNode? e in research["entities"]!.AsArray())
            kinds[(string)e!["id"]!] = ((string)e["kind"]!, e["requires"] is not null);
        HashSet<string> consumed = Consumed();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (string[] r in rows)
        {
            string id = r[0].Trim('`');
            Assert.True(seen.Add(id), $"{id} is listed twice");
            Assert.True(kinds.TryGetValue(id, out var k), $"{id} is not a research.json entity");
            Assert.Equal(k.Kind, r[1]);
            string disposition = r[2];
            Assert.False(string.IsNullOrWhiteSpace(r[3]), $"{id}: no owner");
            Assert.False(string.IsNullOrWhiteSpace(r[4]), $"{id}: no target milestone");
            if (id == "inst.law_code")
                Assert.Equal("M6 slice (track CIV)", disposition);
            else if (disposition.StartsWith("realized", StringComparison.Ordinal))
                Assert.True(consumed.Contains(id), $"{id} is registered realized but no enforced consumer links to it");
            else if (disposition == "baseline, not simulated")
                Assert.True(!k.Gated && !consumed.Contains(id), $"{id}: a baseline row has no requirement and no consumer");
            else
            {
                Assert.Equal("deferred", disposition);
                Assert.True(k.Gated && !consumed.Contains(id), $"{id}: a deferred row has a requirement and no consumer");
            }
            string key = disposition.Split(" (")[0];
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        Assert.Equal(kinds.Count, seen.Count);   // every entity, exactly once
        Assert.Equal(135, seen.Count);
        Assert.Equal("M6 slice=1; baseline, not simulated=2; deferred=91; realized=41",
            string.Join("; ", counts.Select(kv => kv.Key + "=" + kv.Value)));
    }
}
