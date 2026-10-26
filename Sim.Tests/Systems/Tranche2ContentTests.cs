using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// D-063 (tranche 2) — properties of the SHIPPED research content: counts and parity (R5), every node a real effect or a
/// declared future effect (R1 as amended by the CR-025 ruling), the CR-025 disposition,
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

    /// <summary>The founded dev world and its player, for the research card (InfoQuery) — the screens' own notion of what
    /// the simulation reads.</summary>
    private static readonly Lazy<(WorldState World, PolityId Player)> Founded = new(() =>
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return (w, w.Polities[i].Id);
        throw new InvalidOperationException("no player Empire");
    });

    /// <summary>
    /// KNOWN GAPS (CR-025 ruling, 2026-10-26): the nodes that, judged by ACTUAL liveness, have neither a real effect nor a
    /// declared future effect. The test asserts the gap set EQUALS this list, so it may only change deliberately: a node
    /// leaves it when it gains a real effect or a future declaration (and the test fails until it is removed here), and no
    /// new node can join it silently.
    /// <para>History: CR-025 §7.3 found 17 — kiln_updraft, oil_press, papyrus, plank_boat, fulling_mill, sulphuric_acid,
    /// telephone, petroleum_refining, integrated_circuit, nuclear_power, solar_pv, wind_turbine_modern, cloud_computing,
    /// desalination, fusion_power, trace_italienne, rocket — each unlocking only placeholder entities. The node-by-node
    /// audit of CR-025 §8 (2026-10-26) declared each one's future effect (<see cref="GapAuditDisposition"/>), so the list
    /// is empty. Two of them (papyrus, telephone) also have a PROPOSED live effect awaiting the Director's ruling (CR-025
    /// §8.3); it is not applied, and nothing here depends on it.</para>
    /// </summary>
    internal static readonly string[] KnownGapsWithoutARealEffect = [];

    /// <summary>
    /// CR-025 §8 (2026-10-26) — THE APPLIED DISPOSITION of the 17 former known gaps: each declares only future effects, in
    /// this milestone order, with operand null (no registered operand belongs to the realizing systems), and nothing else
    /// changed: no realized effect, the same node key (saves key their rows by it) and the same placeholder unlocks.
    /// </summary>
    internal static readonly (string Id, int Key, string[] Milestones, string[] Unlocks)[] GapAuditDisposition =
    [
        ("kiln_updraft", 48, ["unscheduled"], ["building.kiln"]),
        ("oil_press", 57, ["unscheduled"], ["building.oil_press"]),
        ("papyrus", 90, ["unscheduled"], ["building.library"]),
        ("plank_boat", 95, ["unscheduled"], ["building.harbour"]),
        ("fulling_mill", 182, ["unscheduled"], ["building.fulling_mill"]),
        ("sulphuric_acid", 275, ["unscheduled"], ["building.chemical_works"]),
        ("telephone", 290, ["unscheduled"], ["infra.telephone_network"]),
        ("petroleum_refining", 292, ["unscheduled"], ["building.refinery"]),
        ("integrated_circuit", 317, ["unscheduled"], ["building.semiconductor_fab"]),
        ("nuclear_power", 322, ["unscheduled"], ["building.nuclear_power_station"]),
        ("solar_pv", 346, ["unscheduled"], ["building.solar_farm"]),
        ("wind_turbine_modern", 355, ["unscheduled"], ["building.wind_farm"]),
        ("cloud_computing", 368, ["unscheduled"], ["building.data_centre"]),
        ("desalination", 393, ["unscheduled"], ["building.desalination_plant"]),
        ("fusion_power", 409, ["unscheduled"], ["building.fusion_plant"]),
        ("trace_italienne", 229, ["M7"], ["building.bastion_fort"]),
        ("rocket", 308, ["M7", "unscheduled"], ["building.spaceport", "project.orbital_launch", "unit.missile"]),
    ];

    /// <summary>CR-025 §8 (2026-10-26): <see cref="GapAuditDisposition"/> is what the shipped content says, node for node,
    /// and none of the 17 is a live consumer's input: no realized effect, no level, no unlock some system realizes.</summary>
    [Fact]
    public void TheGapAuditDisposition_IsApplied_FutureOnly_KeysAndUnlocksUnchanged()
    {
        ResearchContent c = C;
        foreach ((string id, int key, string[] milestones, string[] unlocks) in GapAuditDisposition)
        {
            int i = c.IndexOfId(id);
            ResearchNode n = c.Nodes[i];
            Assert.Equal(key, n.Key.Value);
            Assert.Empty(n.RealizedEffects);
            Assert.False(ResearchQuery.IsLevelled(c, i), id);
            Assert.Equal(milestones, n.FutureEffects.Select(f => f.Milestone).ToArray());
            Assert.All(n.FutureEffects, f => Assert.Null(f.Operand));
            Assert.All(n.FutureEffects, f => Assert.False(string.IsNullOrWhiteSpace(f.System) || string.IsNullOrWhiteSpace(f.Description)));
            string[] entities = [.. n.UnlockedEntities.Select(e => c.Entities[e].Id).Order(StringComparer.Ordinal)];
            Assert.Equal(unlocks, entities);
        }
    }

    /// <summary>
    /// D-063 R1, as amended by the Director's CR-025 ruling (2026-10-26): every node produces a REAL gameplay effect — or
    /// declares, explicitly, a FUTURE effect (`effects.future`) that a later milestone's system will realize. "Real" is
    /// actual liveness: the research card's own verdict (InfoQuery, pinned against the systems by
    /// InfoQueryTests.EveryNodesEffect_IsKnowledgeOnly_ExactlyWhenNothingReadsIt) — a realized operand effect on an operand
    /// consumed in play, a level rule, an entity unlock some system realizes (a placeholder unlock does NOT count), an Age
    /// milestone fact, the research stage, the tax edict — or one of the two order gates sim.json names (the card does not
    /// show the assay gate; queue.md 2026-10-26). A future declaration never makes a node simulated (FutureEffectsTests,
    /// InfoQueryTests), and the invariant on what IS implemented is not weakened: every realized effect names an operand
    /// consumed in play.
    /// </summary>
    [Fact]
    public void EveryNode_HasARealEffect_OrADeclaredFutureEffect()
    {
        (WorldState w, PolityId player) = Founded.Value;
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent c = cfg.Research!;
        string[] gates = [cfg.Governance!.TaxationRequires, cfg.Discovery!.AssayRequires!];
        var missing = new List<string>();
        var deadRealized = new List<string>();
        foreach (ResearchNode n in c.Nodes)
        {
            foreach (RealizedEffect e in n.RealizedEffects)
                if (!KnowledgeOperands.Of(e.Operand).ConsumedInPlay) deadRealized.Add(n.Id + ": " + KnowledgeOperands.Of(e.Operand).Id);
            bool real = Array.IndexOf(gates, n.Id) >= 0
                || InfoQuery.Card(w, cfg, player, InfoSubject.Node(n.Key)).Effect == InfoEffect.Simulated;
            if (!real && n.FutureEffects.Count == 0) missing.Add(n.Id);
        }
        Assert.Empty(deadRealized);
        missing.Sort(StringComparer.Ordinal);
        string[] known = [.. KnownGapsWithoutARealEffect];
        Array.Sort(known, StringComparer.Ordinal);
        Assert.True(known.SequenceEqual(missing), "nodes without a real or future effect: " + string.Join(", ", missing));
    }

    /// <summary>
    /// CR-025 ruling (2026-10-26) — THE APPLIED DISPOSITION of the 17 nodes that declared roads.baselineKmPerDay: 8
    /// retargeted to a live operand (delta kept as authored), 6 declaring a future effect with no realized effect, 3 with the
    /// dead delta dropped and every other effect kept. Node keys are unchanged (saves key their rows by them).
    /// </summary>
    [Fact]
    public void TheCr025Disposition_IsApplied_AndNoNodeDeclaresTheUnconsumedSpeedOperand()
    {
        ResearchContent c = C;
        (string Id, int Key, string Operand, double Delta)[] retargets =
        [
            ("horse_domestication", 96, "catchment.hinterlandRadiusKm", 0.05), ("horseshoe", 177, "catchment.hinterlandRadiusKm", 0.03),
            ("internal_combustion", 291, "catchment.hinterlandRadiusKm", 0.04), ("bridge_stone", 151, "roads.maxRouteKm", 0.04),
            ("bridge_trusts", 1285, "roads.maxRouteKm", 0.03), ("turnpike_trusts", 1308, "roads.maxRouteKm", 0.04),
            ("sprung_coach", 516, "governance.authorityDecayCostUnits", 0.05), ("road_houses", 1242, "governance.authorityDecayCostUnits", 0.03),
        ];
        foreach ((string id, int key, string operand, double delta) in retargets)
        {
            ResearchNode n = c.Nodes[c.IndexOfId(id)];
            Assert.Equal(key, n.Key.Value);
            RealizedEffect e = Assert.Single(n.RealizedEffects);
            Assert.Equal(operand, KnowledgeOperands.Of(e.Operand).Id);
            Assert.Equal(delta, e.Delta);
            Assert.Empty(n.FutureEffects);
        }
        (string Id, int Key, string[] Milestones)[] future =
        [
            ("wheel_spoked", 99, ["M7", "unscheduled"]), ("saddle", 123, ["M7"]), ("aluminium", 306, ["M7"]),
            ("integrated_alliance_command", 1178, ["M8", "M7"]), ("pilgrimage", 1248, ["M9"]), ("road_traffic_regulation", 1451, ["unscheduled"]),
        ];
        foreach ((string id, int key, string[] milestones) in future)
        {
            ResearchNode n = c.Nodes[c.IndexOfId(id)];
            Assert.Equal(key, n.Key.Value);
            Assert.Empty(n.RealizedEffects);
            Assert.Equal(milestones, n.FutureEffects.Select(f => f.Milestone).ToArray());
        }
        (string Id, int Key, string[] Kept)[] drops =
        [
            ("road_paved", 150, ["governance.authorityDecayCostUnits"]), ("wheellock", 227, ["society.unrestOutputDrag"]), ("motor_road", 425, []),
        ];
        foreach ((string id, int key, string[] kept) in drops)
        {
            ResearchNode n = c.Nodes[c.IndexOfId(id)];
            Assert.Equal(key, n.Key.Value);
            Assert.Equal(kept, n.RealizedEffects.Select(e => KnowledgeOperands.Of(e.Operand).Id).ToArray());
            Assert.Empty(n.FutureEffects);
        }
        // No node anywhere declares the unconsumed operand as a realized effect (the loader refuses it; this is the content
        // view of the same fact), and only the two freight-time future declarations name it.
        Assert.Empty(c.NodesWith(KnowledgeOperand.RoadsBaselineKmPerDay));
        var naming = new List<string>();
        foreach (ResearchNode n in c.Nodes)
            foreach (FutureEffect f in n.FutureEffects)
                if (f.Operand == KnowledgeOperand.RoadsBaselineKmPerDay) naming.Add(n.Id);
        Assert.Equal(["wheel_spoked", "road_traffic_regulation"], naming);
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
