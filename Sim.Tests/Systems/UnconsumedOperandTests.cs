using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// CR-025 — EVIDENCE PINS for the operand `roads.baselineKmPerDay` ("faster overland travel"), which 17 shipped nodes
/// declare and which no simulation system consumes. These tests pin the defect as MEASURED, pending the Director's ruling;
/// they assert a status, not a property the system ought to have (ADR-015 §7.2), and each one is meant to flip when the
/// ruling is applied:
/// <list type="number">
/// <item>the operand's only read site, <c>TransportQuery.EstimateFreight</c>, has no caller in Sim.Core, Sim.Cli or
///   Sim.Ui (the registry row's reader is a query nobody runs);</item>
/// <item>stripping every declaration of the operand from the shipped research content leaves byte-identical a canonical
///   AI run in which every holder has completed all 17 declaring nodes — the declarations change no world — while
///   stripping a live operand the same way does change it.</item>
/// </list>
/// The content-side freeze (no NEW node may declare it) is Tranche2ContentTests.NoNewNode_DeclaresTheUnconsumedSpeedOperand.
/// </summary>
public class UnconsumedOperandTests
{
    private const string Operand = "roads.baselineKmPerDay";

    [Fact]
    public void TheSpeedOperandsOnlyReadSite_HasNoCallerOutsideTests()
    {
        string root = RepoPaths.Root();
        var readers = new List<string>();
        var callers = new List<string>();
        foreach (string project in new[] { "Sim.Core", "Sim.Cli", "Sim.Ui" })
            foreach (string f in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                string text = File.ReadAllText(f);
                if (rel != "Sim.Core/State/KnowledgeOperands.cs" && text.Contains("KnowledgeOperand.RoadsBaselineKmPerDay", StringComparison.Ordinal))
                    readers.Add(rel);
                if (rel != "Sim.Core/State/TransportQuery.cs" && text.Contains("EstimateFreight(", StringComparison.Ordinal))
                    callers.Add(rel);
            }
        // The one read site is the shipper overload of the travel-time hook ...
        Assert.Equal(["Sim.Core/State/TransportQuery.cs"], readers);
        // ... and nothing outside the tests calls the hook (ADR-032 §7: "Not consumed yet"). When a system starts to
        // consume it, this pin fails: update CR-025's status and the tranche-2 coverage report, then remove this pin.
        Assert.Empty(callers);
    }

    [Fact]
    public void StrippingTheSpeedOperand_ChangesNoWorld_WhenEveryDeclaringNodeIsComplete()
    {
        // The canonical AI run reaches none of the 17 nodes within 300 turns (measured: the first version of this pin
        // failed its non-vacuity guard), so the nodes are GRANTED: every holder of the founded canonical world completes
        // all 17, plus one CONTROL node declaring a live operand (demographics.mortalityPerYear). Three contents run the
        // same granted world with the AI on: shipped; shipped minus every roads.baselineKmPerDay declaration; shipped minus
        // every demographics.mortalityPerYear declaration. Removing the live operand changes the world (the method has
        // teeth); removing the speed operand changes nothing, hash for hash.
        SimConfig shipped = TestConfigs.Sim();
        ResearchContent c = shipped.Research!;
        Assert.True(c.Tuning.KnowledgeEffects);
        SimConfig withoutSpeed = shipped with { Research = Without(Operand, out List<string> declaring) };
        SimConfig withoutControl = shipped with { Research = Without(ControlOperand, out List<string> controlNodes) };
        Assert.Equal(17, declaring.Count);

        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, shipped, 42);
        var holders = new List<PolityId>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            PolityId h = SettlementKnowledge.HolderOf(w, w.Settlements[i].Id);
            if (!holders.Contains(h)) holders.Add(h);
        }
        Assert.True(holders.Count >= 2);
        foreach (PolityId h in holders)
        {
            foreach (string id in declaring) KnowledgeEffectsRigs.Grant(w, c, h, id);
            KnowledgeEffectsRigs.Grant(w, c, h, controlNodes[0]);
        }

        const int turns = 40;
        string live = WorldHash.ComputeHex(RunAi(w.Clone(), shipped, turns));
        Assert.Equal(live, WorldHash.ComputeHex(RunAi(w.Clone(), withoutSpeed, turns)));
        Assert.NotEqual(live, WorldHash.ComputeHex(RunAi(w.Clone(), withoutControl, turns)));
    }

    private const string ControlOperand = "demographics.mortalityPerYear";

    /// <summary>The shipped research content with every realized declaration of <paramref name="operand"/> removed; the
    /// ids of the nodes that declared it, in content order.</summary>
    private static ResearchContent Without(string operand, out List<string> declaring)
    {
        JsonObject doc;
        using (Stream s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        declaring = [];
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? node in doc[tree]!.AsArray())
            {
                if (node!["effects"]?["realized"] is not JsonArray realized) continue;
                for (int i = realized.Count - 1; i >= 0; i--)
                    if ((string?)realized[i]!["operand"] == operand) { realized.RemoveAt(i); declaring.Add((string)node["id"]!); }
            }
        Assert.NotEmpty(declaring);
        return KnowledgeEffectsRigs.Load(doc.ToJsonString());
    }

    private static WorldState RunAi(WorldState w, SimConfig cfg, int turns)
    {
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            w = exec.Step(w);
        }
        return w;
    }
}
