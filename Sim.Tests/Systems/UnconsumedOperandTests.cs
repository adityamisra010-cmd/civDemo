using System.Text.Json.Nodes;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// CR-025 — THE OPERAND NO SYSTEM CONSUMES, after the Director's ruling (2026-10-26). `roads.baselineKmPerDay` ("faster
/// overland travel") is read only by the travel-time query, which no simulation system calls (ADR-032 §7), so a realized
/// delta on it changes nothing in play. The ruling's state, pinned:
/// <list type="number">
/// <item>the registry flags exactly that operand as not consumed in play, and the source scan behind the flag still
///   holds (its only read site, <c>TransportQuery.EstimateFreight</c>, has no caller in Sim.Core, Sim.Cli or Sim.Ui) —
///   when a system starts to consume it, the scan fails and the flag, the loader guard and CR-025 are revisited together;</item>
/// <item>THE RECURRENCE GUARD: the loader refuses a realized delta, or a repeatable level effect, on an operand not
///   consumed in play, whatever the delta;</item>
/// <item>no shipped node or level declares one.</item>
/// </list>
/// The 17-node disposition itself is Tranche2ContentTests.TheCr025Disposition_IsApplied_…; the retargeted nodes' teeth are
/// KnowledgeEffectsTeethTests.ACr025RetargetedNode_…; the future declarations are FutureEffectsTests.
/// </summary>
public class UnconsumedOperandTests
{
    private const string Operand = "roads.baselineKmPerDay";

    [Fact]
    public void TheRegistry_FlagsExactlyTheSpeedOperand_AsNotConsumedInPlay()
    {
        KnowledgeOperandInfo[] dead = [.. KnowledgeOperands.All.Where(o => !o.ConsumedInPlay)];
        KnowledgeOperandInfo only = Assert.Single(dead);
        Assert.Equal(KnowledgeOperand.RoadsBaselineKmPerDay, only.Operand);
        Assert.Equal(Operand, only.Id);
        Assert.Contains("CR-025", only.NotConsumed!, StringComparison.Ordinal);
    }

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
        // consume it, this pin fails: clear the registry row's NotConsumed reason (which lifts the loader guard for it),
        // turn the operand's future declarations into realized effects, and update CR-025 and the coverage report.
        Assert.Empty(callers);
    }

    [Theory]
    [InlineData(0.04)]
    [InlineData(0.5)]
    [InlineData(1e-9)]
    public void TheLoader_RefusesARealizedDelta_OnAnOperandNotConsumedInPlay(double delta)
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        KnowledgeEffectsRigs.SetRealized(doc["technologies"]![3]!.AsObject(), ("housing.tauYears", 0.1), (Operand, delta));
        var e = Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(doc.ToJsonString()));
        Assert.Contains("effects.realized[1]", e.Message);
        Assert.Contains("is not consumed in play", e.Message);
        Assert.Contains("effects.future", e.Message);
        // Control: the same document without the dead entry loads.
        KnowledgeEffectsRigs.SetRealized(doc["technologies"]![3]!.AsObject(), ("housing.tauYears", 0.1));
        Assert.Single(KnowledgeEffectsRigs.Load(doc.ToJsonString()).Nodes[3].RealizedEffects);
    }

    [Fact]
    public void TheLoader_RefusesALevelEffect_OnAnOperandNotConsumedInPlay()
    {
        // The shipped content's first repeatable level rule, with its operand swapped for the unconsumed one.
        JsonObject doc;
        using (Stream s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        JsonObject? levelled = null;
        foreach (JsonNode? n in doc["technologies"]!.AsArray())
            if (n!["repeatable"] is JsonObject r && r["levels"] is JsonValue v && v.GetValue<bool>()) { levelled = r; break; }
        Assert.NotNull(levelled);
        JsonArray effects = levelled!["effects"]!.AsArray();
        string original = effects[0]!["operand"]!.GetValue<string>();
        effects[0]!["operand"] = Operand;
        var e = Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(doc.ToJsonString()));
        Assert.Contains("is not consumed in play", e.Message);
        effects[0]!["operand"] = original;   // control: restored, it loads
        Assert.NotNull(KnowledgeEffectsRigs.Load(doc.ToJsonString()));
    }

    [Fact]
    public void NoShippedRealizedOrLevelEffect_NamesAnOperandNotConsumedInPlay()
    {
        ResearchContent c = TestConfigs.Research();
        var dead = new List<string>();
        int levels = 0;
        foreach (ResearchNode n in c.Nodes)
        {
            foreach (RealizedEffect e in n.RealizedEffects)
                if (!KnowledgeOperands.Of(e.Operand).ConsumedInPlay) dead.Add(n.Id + " " + KnowledgeOperands.Of(e.Operand).Id);
            if (n.Levels is { } rule)
                foreach (RepeatableLevelEffect e in rule.Effects)
                {
                    levels++;
                    if (!KnowledgeOperands.All[RepeatableOperands.IndexOf(e.Operand)].ConsumedInPlay) dead.Add(n.Id + " level " + e.Operand);
                }
        }
        Assert.Empty(dead);
        Assert.True(levels > 0);
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            if (!info.ConsumedInPlay) Assert.Empty(c.NodesWith(info.Operand));
    }
}
