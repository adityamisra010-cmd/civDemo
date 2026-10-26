using System.Text.Json.Nodes;
using Sim.Core.Systems.Research;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// The research OVERLAYS of the 2026-10-26 ruling (pacing recalibration and cost batches under the combined trial
/// configuration): a measurement-only layer over the shipped research.json. These pin that the shipped defaults stay off,
/// that the trial overlay is exactly the three switches plus its own pacing scale, and that a node patch moves one node's
/// magnitude and cost within the loader's factor ranges and nothing else.
/// </summary>
public sealed class ResearchOverlayTests
{
    private static string Pass => Path.Combine(AllocationScenario.RepoRoot(), "docs", "m6-plan", "knowledge-redesign", "research-cost-pass");
    private static string TrialOverlay => Path.Combine(Pass, "trial", "research-economy-trial.json");

    private static readonly string[] HeldIds =
    [
        // the 17-node audit (CR-025 §8) and CR-025's retargets and future-dependent nodes: never in a cost batch
        "kiln_updraft", "oil_press", "papyrus", "plank_boat", "fulling_mill", "sulphuric_acid", "telephone", "petroleum_refining",
        "integrated_circuit", "nuclear_power", "solar_pv", "wind_turbine_modern", "cloud_computing", "desalination", "fusion_power",
        "trace_italienne", "rocket", "horse_domestication", "horseshoe", "internal_combustion", "bridge_stone", "bridge_trusts",
        "turnpike_trusts", "sprung_coach", "road_houses", "wheel_spoked", "saddle", "aluminium", "integrated_alliance_command",
        "pilgrimage", "road_traffic_regulation",
    ];

    [Fact]
    public void ShippedResearch_KeepsTheThreeTrialSwitchesAbsent()
    {
        JsonObject tuning;
        using (var s = Sim.Data.DataFiles.OpenResearch()) tuning = JsonNode.Parse(s)!.AsObject()["tuning"]!.AsObject();
        Assert.False(tuning.ContainsKey("researchOverflow"));
        Assert.False(tuning.ContainsKey("researchReserve"));
        Assert.False(tuning.ContainsKey("aiGateAwareGoals"));

        ResearchTuning t = ResearchPacingRunner.Config(overflow: false).Research!.Tuning;
        Assert.False(t.ResearchOverflow);
        Assert.Null(t.Reserve);
        Assert.False(t.AiGateAwareGoals);
    }

    [Fact]
    public void TrialOverlay_SetsTheThreeSwitchesAndItsOwnPacingScale_AndNothingElse()
    {
        ResearchContent shipped = ResearchPacingRunner.Config(overflow: false).Research!;
        ResearchContent trial = ResearchPacingRunner.Config(overflow: false, overlays: [TrialOverlay]).Research!;
        Assert.True(trial.Tuning.ResearchOverflow);
        Assert.False(trial.Tuning.ResearchOverflowLane);
        Assert.Equal(new ResearchReserveRule(3.0, 1.0), trial.Tuning.Reserve);
        Assert.True(trial.Tuning.AiGateAwareGoals);

        var overlay = JsonNode.Parse(File.ReadAllText(TrialOverlay))!.AsObject();
        Assert.Equal(["_doc", "tuning"], overlay.Select(kv => kv.Key).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["aiGateAwareGoals", "pacingScale", "researchOverflow", "researchReserve"],
            overlay["tuning"]!.AsObject().Select(kv => kv.Key).Order(StringComparer.Ordinal).ToArray());

        // every node keeps its content cost; only the scale differs, so BaseCost = content cost × the overlay's scale
        Assert.Equal(shipped.Nodes.Count, trial.Nodes.Count);
        for (int i = 0; i < trial.Nodes.Count; i++)
        {
            ResearchNode a = shipped.Nodes[i], b = trial.Nodes[i];
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.CostRationale, b.CostRationale);
            Assert.Equal(b.CostRationale.ContentCost * trial.Tuning.Pacing!.Of(b.Tree, b.AgeNumber), b.BaseCost);
        }
    }

    [Fact]
    public void NodePatch_MovesOnlyThatNodesMagnitudeAndCost_WithinTheFactorRanges()
    {
        string dir = Directory.CreateTempSubdirectory("overlay").FullName;
        string path = Path.Combine(dir, "patch.json");
        File.WriteAllText(path, """{ "nodes": { "fire_making": { "magnitudeStep": -0.5 }, "taxation": { "magnitudeStep": 0.5 } } }""");
        ResearchContent shipped = ResearchPacingRunner.Config(overflow: false).Research!;
        ResearchContent patched = ResearchPacingRunner.Config(overflow: false, overlays: [path]).Research!;
        foreach ((string id, double step) in new[] { ("fire_making", -0.5), ("taxation", 0.5) })
        {
            ResearchCostRationale a = shipped.Nodes[shipped.IndexOfId(id)].CostRationale;
            ResearchCostRationale b = patched.Nodes[patched.IndexOfId(id)].CostRationale;
            Assert.Equal(a.Magnitude + step, b.Magnitude);
            Assert.Equal(b.Magnitude, b.Novelty + b.Difficulty + b.Material + b.Institutional + b.Breadth + b.PrerequisiteComplexity, 9);
            Assert.Equal(Math.Round(92.4 * Math.Pow(2.0, b.Magnitude) / 10.0, MidpointRounding.AwayFromZero) * 10.0, b.ContentCost);
            Assert.Equal(a.Material, b.Material);
            Assert.Equal(a.Institutional, b.Institutional);
        }
        int moved = 0;
        for (int i = 0; i < shipped.Nodes.Count; i++)
            if (shipped.Nodes[i].BaseCost != patched.Nodes[i].BaseCost) moved++;
        Assert.Equal(2, moved);

        File.WriteAllText(path, """{ "nodes": { "no_such_node": { "magnitudeStep": -0.5 } } }""");
        Assert.Throws<ArgumentException>(() => ResearchPacingRunner.Config(overflow: false, overlays: [path]));
        File.WriteAllText(path, """{ "nodes": { "fire_making": { "magnitudeStep": -9.0 } } }""");
        Assert.Throws<ArgumentException>(() => ResearchPacingRunner.Config(overflow: false, overlays: [path]));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void CostBatches_AreDisjoint_LoadOnTheTrialOverlay_AndNeverTouchAHeldNode()
    {
        string[] batches = [.. Directory.GetFiles(Path.Combine(Pass, "trial", "batches"), "*.json").Order(StringComparer.Ordinal)];
        Assert.Equal(8, batches.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);   // test bookkeeping only
        foreach (string batch in batches)
        {
            var nodes = JsonNode.Parse(File.ReadAllText(batch))!.AsObject()["nodes"]!.AsObject();
            Assert.NotEmpty(nodes);
            foreach (KeyValuePair<string, JsonNode?> kv in nodes)
            {
                Assert.True(seen.Add(kv.Key), $"{kv.Key} is in two batches");
                Assert.DoesNotContain(kv.Key, HeldIds);
            }
            ResearchContent rc = ResearchPacingRunner.Config(overflow: false, overlays: [TrialOverlay, batch]).Research!;
            Assert.True(rc.Tuning.ResearchOverflow);
        }
        Assert.Equal(103, seen.Count);
    }
}
