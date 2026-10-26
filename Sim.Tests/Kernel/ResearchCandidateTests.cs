using System.Text.Json.Nodes;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// The CANDIDATE research configuration (Director instruction 2026-10-26, "execute the next approved trial"): a full
/// content file, research-cost-pass/candidate/research.candidate.json, written by tools/apply_overlay.py from the shipped
/// research.json and the overlays its manifest lists. It is NOT the shipped content: these pin that it is exactly the
/// shipped content plus those overlays (so every measurement of the overlays is a measurement of the file), that it
/// differs from the shipped content only where the overlays say, and that the shipped content keeps every switch off.
/// </summary>
public sealed class ResearchCandidateTests
{
    private static string Dir => Path.Combine(AllocationScenario.RepoRoot(), "docs", "m6-plan", "knowledge-redesign",
        "research-cost-pass", "candidate");

    private static (string[] Overlays, string Content) Manifest()
    {
        var m = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, "manifest.json")))!.AsObject();
        string root = Path.Combine(AllocationScenario.RepoRoot(), "docs", "m6-plan", "knowledge-redesign", "research-cost-pass");
        string[] overlays = [.. m["overlays"]!.AsArray().Select(o => Path.Combine(root, (string)o!))];
        return (overlays, Path.Combine(Dir, (string)m["content"]!));
    }

    [Fact]
    public void CandidateFile_IsTheShippedContentPlusItsOverlays_NodeForNode()
    {
        (string[] overlays, string content) = Manifest();
        ResearchContent viaOverlays = ResearchPacingRunner.Config(overflow: false, overlays: overlays).Research!;
        using var goods = Sim.Data.DataFiles.OpenGoods();
        ResearchContent file = ResearchContentLoader.Load(File.ReadAllText(content), GoodsConfigLoader.Load(goods));

        Assert.Equal(viaOverlays.Tuning.ResearchOverflow, file.Tuning.ResearchOverflow);
        Assert.Equal(viaOverlays.Tuning.Reserve, file.Tuning.Reserve);
        Assert.Equal(viaOverlays.Tuning.AiGateAwareGoals, file.Tuning.AiGateAwareGoals);
        Assert.Equal(viaOverlays.Tuning.AiCivicsGateGoals, file.Tuning.AiCivicsGateGoals);
        Assert.Equal(viaOverlays.Tuning.Pacing!.Technology, file.Tuning.Pacing!.Technology);
        Assert.Equal(viaOverlays.Tuning.Pacing.Civics, file.Tuning.Pacing.Civics);
        Assert.Equal(viaOverlays.Nodes.Count, file.Nodes.Count);
        for (int i = 0; i < file.Nodes.Count; i++)
        {
            Assert.Equal(viaOverlays.Nodes[i].Id, file.Nodes[i].Id);
            Assert.Equal(viaOverlays.Nodes[i].CostRationale, file.Nodes[i].CostRationale);
            Assert.Equal(viaOverlays.Nodes[i].BaseCost, file.Nodes[i].BaseCost);
        }
    }

    [Fact]
    public void CandidateFile_DiffersFromShippedOnlyInTheTuningSwitches_TheScale_AndTheBatchedNodes()
    {
        (string[] overlays, string content) = Manifest();
        JsonObject shipped;
        using (var s = Sim.Data.DataFiles.OpenResearch()) shipped = JsonNode.Parse(s)!.AsObject();
        JsonObject candidate = JsonNode.Parse(File.ReadAllText(content))!.AsObject();

        var patched = new SortedSet<string>(StringComparer.Ordinal);
        var tuningKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string o in overlays)
        {
            var ov = JsonNode.Parse(File.ReadAllText(o))!.AsObject();
            if (ov["nodes"] is JsonObject n) foreach (KeyValuePair<string, JsonNode?> kv in n) patched.Add(kv.Key);
            if (ov["tuning"] is JsonObject t) foreach (KeyValuePair<string, JsonNode?> kv in t) if (kv.Key != "_doc") tuningKeys.Add(kv.Key);
        }
        Assert.Equal(["aiCivicsGateGoals", "aiGateAwareGoals", "pacingScale", "researchOverflow", "researchReserve"], tuningKeys.ToArray());

        // every top-level member but the two trees and tuning is byte-equal
        foreach (KeyValuePair<string, JsonNode?> kv in shipped)
            if (kv.Key is not ("technologies" or "civics" or "tuning"))
                Assert.True(JsonNode.DeepEquals(kv.Value, candidate[kv.Key]), kv.Key);
        // tuning: equal apart from the overlay keys
        foreach (KeyValuePair<string, JsonNode?> kv in shipped["tuning"]!.AsObject())
            if (!tuningKeys.Contains(kv.Key))
                Assert.True(JsonNode.DeepEquals(kv.Value, candidate["tuning"]![kv.Key]), "tuning." + kv.Key);
        // nodes: equal apart from the batched nodes' cost and cost rationale
        int moved = 0;
        foreach (string tree in new[] { "technologies", "civics" })
        {
            JsonArray a = shipped[tree]!.AsArray(), b = candidate[tree]!.AsArray();
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                if (JsonNode.DeepEquals(a[i], b[i])) continue;
                string id = (string)a[i]!["id"]!;
                Assert.Contains(id, patched);
                var x = a[i]!.DeepClone().AsObject();
                var y = b[i]!.DeepClone().AsObject();
                x.Remove("cost"); x.Remove("costRationale");
                y.Remove("cost"); y.Remove("costRationale");
                Assert.True(JsonNode.DeepEquals(x, y), id + " differs beyond its cost");
                moved++;
            }
        }
        Assert.Equal(patched.Count, moved);
    }

    [Fact]
    public void ShippedContent_IsNotTheCandidate()
    {
        (_, string content) = Manifest();
        ResearchTuning shipped = ResearchPacingRunner.Config(overflow: false).Research!.Tuning;
        Assert.False(shipped.ResearchOverflow);
        Assert.Null(shipped.Reserve);
        Assert.False(shipped.AiGateAwareGoals);
        Assert.False(shipped.AiCivicsGateGoals);
        using var s = Sim.Data.DataFiles.OpenResearch();
        Assert.NotEqual(new StreamReader(s).ReadToEnd(), File.ReadAllText(content));
    }
}
