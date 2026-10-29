using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 closure, track FOG (D-066 §3 "Extent monotone across revolt and loss of control: COMPLETED in M6 (test pin added)";
/// D-040 B2: the known extent is permanent once gained; ADR-044 §6: KnownExtentRow bits are never cleared, and a
/// revolt-born polity knows its seed before the first store).
///
/// THE RIG — ResearchExposureControlChangeTests' revolt corner, on a world WITH TERRAIN and the shipped fog ON: the dev
/// preset (256 px cylinder, seed 42, every settlement the player's), stepped by the SHIPPED relative order of the two
/// systems that matter, discovery then revolt (pipeline.json: colonization, discovery, revolt …). First the player explores
/// from the settlement that will revolt (radius 3 → 6), so its stored extent holds land NO current control re-derives — the
/// explored disc beyond every seed disc. Then that settlement is made destitute (fully unfed, unhoused: the ruled revolt
/// corner, SettlementHappiness.IsRevoltReady) and the next step's revolt founds a new AI polity holding it.
///
/// Pinned, turn-exactly:
/// <list type="bullet">
/// <item>THE PARENT (the loser): every stored word of its extent before the revolt survives every later step, bit for bit,
///   including the explored ring around the place it lost, and PolityKnowledge.Of still answers those nodes known.</item>
/// <item>THE REVOLT-BORN POLITY, on its first world (the step that founds it, whose discovery read PREV — before it existed):
///   no stored row yet, yet PolityKnowledge.Of answers its whole seed disc KNOWN and its settlement known (the query applies
///   the seed); one step later the seed is STORED. It inherits none of the parent's explored land (maps are never shared,
///   ADR-044 E7).</item>
/// <item>CAPTURE (the state a later conqueror — M7 — would leave; no production path transfers a settlement between two
///   standing polities today): the control row moved from the player to the rival, the player keeps every word, the rival
///   knows the captured seed at once and stores it the next step.</item>
/// </list>
/// </summary>
public class DiscoveryExtentControlChangeTests
{
    private static readonly PolityId Player = new(1);

    private static SimConfig Cfg => TestConfigs.Sim();   // the shipped config: fog and discovery ON

    private static TurnExecutor DiscoveryThenRevolt(SimConfig cfg, OrderLog log) =>
        new(GovernanceRigs.FlatEra(), [SystemCatalog.Discovery(cfg), SystemCatalog.Revolt(cfg)], log);

    private static ulong[] Stored(IReadOnlyWorldState w, PolityId p) =>
        DiscoveryQuery.StoredExtent(w, p, DiscoveryQuery.WordCount(DiscoveryQuery.Lattice(w).L));

    private static ulong[] Disc(IReadOnlyWorldState w, int node, int radius)
    {
        (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(w);
        var bits = new ulong[DiscoveryQuery.WordCount(l)];
        DiscoveryQuery.OrDisc(bits, l, wrap, node, radius);
        return bits;
    }

    private static int Count(ulong[] bits)
    {
        int n = 0;
        foreach (ulong b in bits) n += System.Numerics.BitOperations.PopCount(b);
        return n;
    }

    /// <summary>Every bit of <paramref name="before"/> is in <paramref name="after"/>, word for word.</summary>
    private static void AssertSuperset(ulong[] before, ulong[] after, string what)
    {
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++)
            Assert.True(before[i] == (before[i] & after[i]), what + ": word " + i + " lost bits");
    }

    /// <summary>The ruled revolt corner (RevoltSystem: Provision ≤ the threshold): settlement <paramref name="s"/> fully
    /// unfed (deficit ratio 1) and unhoused (its dwellings sunk through the Ledger).</summary>
    private static void Destitute(WorldState w, SettlementId s)
    {
        bool found = false;
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s) { w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = 1.0 }; found = true; }
        if (!found) w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, 1.0, 10_000));
        for (int i = 0; i < w.Housing.Count; i++)
            if (w.Housing[i].Settlement == s && w.Housing[i].Dwellings.Value > 0)
                new Ledger(w.LedgerFlows).Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, w.Housing[i].Dwellings.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    [Fact]
    public void Revolt_TheParentKeepsEveryWord_TheRevoltBornPolityKnowsItsSeedOnItsFirstStep_AndStoresItTheNext()
    {
        SimConfig cfg = Cfg;
        WorldState w0 = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        SettlementId[] held = DiscoveryQuery.Controlled(w0, Player);
        Assert.True(held.Length >= 2, "the player must hold more than one place - its last cannot revolt (D-048 ruling 5)");
        SettlementId lost = held[^1];
        int lostNode = DiscoveryQuery.NodeOfSettlement(w0, lost);
        FogConfig fog = cfg.Fog!;

        // 1. The player explores from the place it will lose: radius 3 → 6, stored in w1.
        var log = new OrderLog();
        Assert.Equal(ExploreVerdict.Ok, DiscoveryQuery.CheckExplore(w0, cfg, Player, lost));
        log.Append(OrderRecord.From(0, Player, OrderKind.Explore, lost.Value, 0.0));
        TurnExecutor exec = DiscoveryThenRevolt(cfg, log);
        WorldState w1 = exec.Step(w0);
        Assert.True(EmpireQuery.ControlsSettlement(w1, Player, lost));
        ulong[] before = Stored(w1, Player);
        int radius = -1;
        for (int i = 0; i < w1.Explorations.Count; i++)
            if (w1.Explorations[i].Polity == Player && w1.Explorations[i].Settlement == lost) radius = w1.Explorations[i].RadiusNodes;
        Assert.True(radius > fog.SeedRadiusNodes, "the expedition was admitted and widened the radius");
        ulong[] explored = Disc(w1, lostNode, radius);
        AssertSuperset(explored, before, "the explored disc is stored");
        // The rig carries land no current control re-derives: explored nodes outside every seed disc of the player.
        ulong[] seeds = new ulong[before.Length];
        foreach (SettlementId s in held)
            DiscoveryQuery.OrDisc(seeds, DiscoveryQuery.Lattice(w1).L, DiscoveryQuery.Lattice(w1).Wrap,
                DiscoveryQuery.NodeOfSettlement(w1, s), fog.SeedRadiusNodes);
        int beyondSeeds = 0;
        for (int i = 0; i < before.Length; i++) beyondSeeds += System.Numerics.BitOperations.PopCount(before[i] & ~seeds[i]);
        Assert.True(beyondSeeds > 0, "the explored ring must lie outside every seed disc - else the rig re-derives it");

        // 2. The place becomes destitute; the next step's revolt founds a new AI polity holding it.
        WorldState ready = w1.Clone();
        Destitute(ready, lost);
        Assert.True(SettlementHappiness.IsRevoltReady(ready, lost, cfg));
        int otherReady = 0;
        foreach (SettlementId s in held) if (s != lost && SettlementHappiness.IsRevoltReady(ready, s, cfg)) otherReady++;
        Assert.Equal(0, otherReady);
        int rosterBefore = ready.Polities.Count;
        WorldState w2 = exec.Step(ready);
        Assert.True(EmpireQuery.TryGetController(w2, lost, out PolityId born));
        Assert.NotEqual(Player, born);
        Assert.Equal(rosterBefore + 1, w2.Polities.Count);
        for (int i = 0; i < ready.Polities.Count; i++) Assert.NotEqual(born, ready.Polities[i].Id);

        // 3. The revolt-born polity on its FIRST world: nothing stored (discovery read PREV, where it did not exist), yet the
        //    seam answers its whole seed disc known and its settlement known.
        Assert.Equal(0, Count(Stored(w2, born)));
        PolityKnowledge first = PolityKnowledge.Of(w2, born, cfg);
        ulong[] seed = Disc(w2, lostNode, fog.SeedRadiusNodes);
        int l = first.LatticeSize;
        for (int n = 0; n < l * l; n++)
            if (DiscoveryQuery.Has(seed, n)) Assert.True(first.Known(n), "seed node " + n + " unknown to the revolt-born polity on its first step");
        Assert.True(first.KnowsSettlement(lost));
        Assert.True(first.Visible(lostNode));
        // Maps are never shared: none of the parent's explored ring beyond the seed is the child's.
        int inherited = 0;
        for (int n = 0; n < l * l; n++)
            if (DiscoveryQuery.Has(explored, n) && !DiscoveryQuery.Has(seed, n) && first.Known(n)) inherited++;
        Assert.Equal(0, inherited);

        // 4. Every later step: the parent keeps every word it held before the revolt; the child stores its seed from w3 on.
        WorldState w = w2;
        for (int t = 0; t < 4; t++)
        {
            ulong[] parent = Stored(w, Player);
            AssertSuperset(before, parent, "the parent's stored extent at turn " + w.Clock.Turn);
            PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
            for (int n = 0; n < l * l; n++)
                if (DiscoveryQuery.Has(before, n)) Assert.True(k.Known(n), "node " + n + " forgotten by the parent at turn " + w.Clock.Turn);
            Assert.True(k.KnowsSettlement(lost), "the parent sighted the place it lost (rule E5)");
            Assert.False(EmpireQuery.ControlsSettlement(w, Player, lost));
            WorldState next = exec.Step(w);
            AssertSuperset(Stored(w, Player), Stored(next, Player), "the parent, one step");
            AssertSuperset(Stored(w, born), Stored(next, born), "the revolt-born polity, one step");
            w = next;
            AssertSuperset(seed, Stored(w, born), "the revolt-born polity's stored seed at turn " + w.Clock.Turn);
        }
    }

    [Fact]
    public void Capture_TheLoserKeepsEveryWord_TheCaptorKnowsTheSeedAtOnce_AndStoresItTheNextStep()
    {
        SimConfig cfg = Cfg;
        WorldState w0 = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        var rival = new PolityId(2);
        SettlementId[] held = DiscoveryQuery.Controlled(w0, Player);
        Assert.True(held.Length >= 2);
        Assert.True(DiscoveryQuery.Controlled(w0, rival).Length >= 1);
        SettlementId taken = held[^1];
        int takenNode = DiscoveryQuery.NodeOfSettlement(w0, taken);
        FogConfig fog = cfg.Fog!;
        var log = new OrderLog();
        log.Append(OrderRecord.From(0, Player, OrderKind.Explore, taken.Value, 0.0));
        TurnExecutor exec = DiscoveryThenRevolt(cfg, log);
        WorldState w1 = exec.Step(w0);
        ulong[] before = Stored(w1, Player);
        ulong[] rivalBefore = Stored(w1, rival);
        ulong[] seed = Disc(w1, takenNode, fog.SeedRadiusNodes);
        int l = DiscoveryQuery.Lattice(w1).L;
        int unknownToRival = 0;
        for (int n = 0; n < l * l; n++) if (DiscoveryQuery.Has(seed, n) && !DiscoveryQuery.Has(rivalBefore, n)) unknownToRival++;
        Assert.True(unknownToRival > 0, "the captured seed must be new to the captor - else the rig proves nothing");

        // The capture: the control row moves to the rival (row order kept).
        WorldState captured = w1.Clone();
        for (int i = 0; i < captured.Controls.Count; i++)
            if (captured.Controls[i].Place == taken) captured.Controls[i] = captured.Controls[i] with { Polity = rival };
        Assert.True(EmpireQuery.ControlsSettlement(captured, rival, taken));

        PolityKnowledge captor = PolityKnowledge.Of(captured, rival, cfg);
        for (int n = 0; n < l * l; n++) if (DiscoveryQuery.Has(seed, n)) Assert.True(captor.Known(n));
        WorldState w = captured;
        for (int t = 0; t < 3; t++)
        {
            WorldState next = exec.Step(w);
            AssertSuperset(before, Stored(next, Player), "the loser at turn " + next.Clock.Turn);
            AssertSuperset(rivalBefore, Stored(next, rival), "the captor's own extent at turn " + next.Clock.Turn);
            AssertSuperset(seed, Stored(next, rival), "the captured seed, stored at turn " + next.Clock.Turn);
            PolityKnowledge k = PolityKnowledge.Of(next, Player, cfg);
            for (int n = 0; n < l * l; n++)
                if (DiscoveryQuery.Has(before, n)) Assert.True(k.Known(n));
            w = next;
        }
    }
}
