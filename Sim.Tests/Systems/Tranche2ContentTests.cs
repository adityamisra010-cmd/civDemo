using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// D-063 (tranche 2) — properties of the SHIPPED research content: counts and parity (R5), every node a real effect (R1),
/// the Age sweep deadlocks nothing (R2), no prerequisite from a later Age, no operand above its cap, and the repeatables
/// of the final Age in both trees (R4).
/// </summary>
public class Tranche2ContentTests
{
    private static ResearchContent C => TestConfigs.Research();

    private static int Finite(ResearchContent c, ResearchTree tree)
    {
        int count = 0;
        foreach (ResearchNode n in c.Nodes) if (n.Tree == tree && !n.HasRepeatableDescriptor) count++;
        return count;
    }

    [Fact]
    public void Counts_CivicsAre90To100PercentOfTechnology()
    {
        int tech = Finite(C, ResearchTree.Technology), civ = Finite(C, ResearchTree.Civics);
        Assert.Equal(515, tech);
        Assert.Equal(477, civ);
        Assert.InRange(civ, (int)Math.Ceiling(0.9 * tech), tech);
    }

    [Fact]
    public void EveryNode_HasARealEffect()
    {
        // A realized operand effect, a live entity unlock, a level rule, or one of the two order gates sim.json names.
        string[] live = ["building.", "infra.", "recipe.", "activity.", "inst.university"];
        SimConfig cfg = TestConfigs.Sim();
        string[] gates = [cfg.Governance!.TaxationRequires, cfg.Discovery!.AssayRequires!];
        var missing = new List<string>();
        foreach (ResearchNode n in C.Nodes)
        {
            if (n.RealizedEffects.Count > 0 || n.Levels is not null || Array.IndexOf(gates, n.Id) >= 0) continue;
            bool liveUnlock = false;
            foreach (int e in n.UnlockedEntities)
                foreach (string prefix in live)
                    if (C.Entities[e].Id.StartsWith(prefix, StringComparison.Ordinal)) liveUnlock = true;
            if (!liveUnlock) missing.Add(n.Id);
        }
        Assert.Empty(missing);
    }

    /// <summary>CR-025 — CONTENT FREEZE while the ruling is pending: `roads.baselineKmPerDay` has no consuming system
    /// (UnconsumedOperandTests), so its declarations are not realized effects. No node beyond the 17 recorded in CR-025 may
    /// declare it; applying the ruling changes this list (and this test) deliberately.</summary>
    [Fact]
    public void NoNewNode_DeclaresTheUnconsumedSpeedOperand()
    {
        string[] recorded =
        [
            "horse_domestication", "wheel_spoked", "saddle", "road_paved", "bridge_stone", "horseshoe", "wheellock",
            "internal_combustion", "aluminium", "motor_road", "sprung_coach", "integrated_alliance_command", "road_houses",
            "pilgrimage", "bridge_trusts", "turnpike_trusts", "road_traffic_regulation",
        ];
        var declaring = new List<string>();
        foreach (ResearchNode n in C.Nodes)
            foreach (RealizedEffect e in n.RealizedEffects)
                if (e.Operand == KnowledgeOperand.RoadsBaselineKmPerDay) declaring.Add(n.Id);
        declaring.Sort(StringComparer.Ordinal);
        Array.Sort(recorded, StringComparer.Ordinal);
        Assert.Equal(recorded, declaring);
    }

    [Fact]
    public void AgeSweep_ReachesEveryNode_FromScratch()
    {
        ResearchContent c = ResearchAgeSweepTests.ContentWith(sweep: true, routing: true);
        var done = new bool[c.Nodes.Count];
        for (bool changed = true; changed;)
        {
            changed = false;
            bool[] mask = ResearchQuery.AvailableMask(c, done);
            for (int i = 0; i < mask.Length; i++)
                if (mask[i] && !done[i]) { done[i] = true; changed = true; }
        }
        var stuck = new List<string>();
        for (int i = 0; i < done.Length; i++) if (!done[i]) stuck.Add(c.Nodes[i].Id);
        Assert.Empty(stuck);
    }

    [Fact]
    public void NoPrerequisite_IsOfALaterAge()
    {
        var bad = new List<string>();
        foreach (ResearchNode n in C.Nodes)
            foreach (int p in n.PrerequisiteNodes)
                if (C.Nodes[p].AgeNumber > n.AgeNumber) bad.Add(n.Id + " <- " + C.Nodes[p].Id);
        Assert.Empty(bad);
    }

    [Fact]
    public void NoOperand_SumsAboveItsCap()
    {
        var sum = new double[KnowledgeOperands.All.Count];
        foreach (ResearchNode n in C.Nodes)
            foreach (RealizedEffect e in n.RealizedEffects) sum[(int)e.Operand] += e.Delta;
        for (int i = 0; i < sum.Length; i++)
            Assert.True(sum[i] <= KnowledgeOperands.All[i].Cap, KnowledgeOperands.All[i].Id + " sums to " + sum[i]);
    }

    [Fact]
    public void Repeatables_AreFinalAge_InBothTrees_AndLevelled()
    {
        var trees = new HashSet<ResearchTree>();   // test bookkeeping, not sim logic
        foreach (ResearchNode n in C.Nodes)
        {
            if (!n.HasRepeatableDescriptor) continue;
            Assert.Equal(9, n.AgeNumber);
            Assert.NotNull(n.Levels);
            Assert.NotEmpty(n.Levels!.Effects);
            trees.Add(n.Tree);
        }
        Assert.Equal(2, trees.Count);
        Assert.True(C.Tuning.RepeatableLevels && C.Tuning.AgeSweep && C.Tuning.DestinationRouting && C.Tuning.KnowledgeEffects);
    }
}
