using System.Numerics;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// WS2 (D-054 — the Director's 2026-10-14 instruction, workstream 2) — EDITABLE BUILDING/ROADS MATERIAL SPLIT. Each Empire's
/// standing building share b (ResourceAllocationRow; order kind 12; no row = 100, building first) guarantees building work
/// b shares in 100 of each eligible good the Empire's purse holds when building work starts, and road works the rest, on
/// a turn the Empire's road order is delivered; whatever one side does not need, the other may use. Building work may take
/// G_b = min(D_b, E_b + max(0, E_r − D_r)), E_b = floor(S × b / 100); road works pay later in the step from what remains.
///
/// THE RIG (every figure by hand): the founded dev world (four settlements, all the player's; IncrementalConstructionTests'
/// rig: stone and timber 0, housing's draw 0, turn length 10 years), ONE candidate route S0–S1 whose existing dirt-path row is
/// 60 km long (the player knows the trackway, 1 timber per km: the road order costs exactly 60 timber), 100 timber at S2 and
/// 1,000 stone at S3, every settlement's labour on building, and two committed granary heads (40 timber, 20 stone each):
/// S0's first in the building order (EnqueuedTurn 0, the lower id), S1's second. Only ConstructionSystem and
/// RoadDevelopmentSystem run (in pipeline order), so every unit drawn is construction's or the road's.
/// </summary>
public class ResourceAllocationTests
{
    private static readonly SimConfig Shipped = IncrementalConstructionTests.Cfg;                 // WS1 + WS2 on
    private static readonly SimConfig Off = TestConfigs.PreAllocationRatios(Shipped);
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly int Tools = Shipped.Goods!.IdOf("tools");
    private const long Road = 60, Supply = 100;

    // ================================================================== the rig

    private static WorldState Rig(long timber = Supply, long stone = 1000, bool heads = true)
    {
        WorldState w = Solo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Set(w, S2, Timber, timber);
        Set(w, S3, Stone, stone);
        if (heads)
        {
            Head(w, S0, 0);
            Head(w, S1, 0);
        }
        return w;
    }

    private static void Head(WorldState w, SettlementId s, long enqueued, int project = Granary)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, enqueued, 0));
    }

    private static OrderRecord Split(long turn, int buildings, PolityId? actor = null, int? target = null) =>
        new(turn, (actor ?? Player).Value, OrderKind.SetResourceAllocation, target ?? (actor ?? Player).Value, buildings);

    private static OrderRecord Roads(long turn, double percent = 100.0, PolityId? actor = null) =>
        OrderRecord.From(turn, actor ?? Player, OrderKind.DevelopRoads, 0, percent);

    private static TurnExecutor BuildAndRoads(SimConfig cfg, OrderLog? log) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], log);

    /// <summary>An independent restatement of ADR-032's single-good charge (have &lt; units: floor(units × have/units) in
    /// doubles, as the road system computes it) — the oracle the road's draw is checked against.</summary>
    private static long RoadCharge(long have, long units) => have >= units ? units : (long)Math.Floor(units * ((double)have / units));

    private static long RoadUnits(IReadOnlyWorldState w)
    {
        long u = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) u += w.RoadDevelopments[i].MaterialUnits;
        return u;
    }

    private static (long P0, long P1, long Built0, long Built1, long Road, long Left) Outcome(IReadOnlyWorldState w) =>
        (P(w, S0), P(w, S1), ConstructionQuery.Built(w, S0, Granary), ConstructionQuery.Built(w, S1, Granary), RoadUnits(w), TotalOf(w, Timber));

    // ================================================================== the split arithmetic

    [Fact]
    public void Split_IsExact_OnAnExhaustiveSmallGrid_AgainstAnIndependentOracle()
    {
        // Every S in [0, 23], D_b and D_r in [0, 26] and b in {0, 1, 7, 33, 50, 99, 100}: the grant against the formula in
        // BigInteger (no shortcut shared with the implementation), and the properties the rule promises.
        int[] shares = [0, 1, 7, 33, 50, 99, 100];
        for (long s = 0; s <= 23; s++)
            for (long db = 0; db <= 26; db++)
                for (long dr = 0; dr <= 26; dr++)
                    foreach (int b in shares)
                    {
                        AllocationSplit x = ResourceAllocation.Split(s, db, dr, b);
                        long eb = (long)(new BigInteger(s) * b / 100);
                        long er = s - eb;
                        long cap = eb + Math.Max(0, er - dr);
                        Assert.Equal((s, eb, er, db, dr, cap, Math.Min(db, cap)),
                            (x.Supply, x.BuildingShare, x.RoadShare, x.BuildingDemand, x.RoadDemand, x.BuildingCap, x.BuildingGrant));
                        Assert.True(x.BuildingGrant <= db && x.BuildingGrant <= s);                      // never more than needed or held
                        Assert.True(s - x.BuildingGrant >= Math.Min(dr, er), "roads can find at least min(D_r, E_r)");
                        if (dr == 0) Assert.Equal(Math.Min(db, s), x.BuildingGrant);                    // no road demand: all
                        if (b == 100) Assert.Equal(Math.Min(db, s), x.BuildingGrant);                   // the default never binds
                    }
    }

    [Theory]
    // S, D_b, D_r, b  → E_b, E_r, G_b
    [InlineData(100, 80, 60, 50, 50, 50, 50)]     // both sides short: each keeps its share
    [InlineData(100, 80, 0, 0, 0, 100, 80)]       // one-sided demand (no road demand): building may use all
    [InlineData(100, 0, 60, 50, 50, 50, 0)]       // one-sided demand (no building demand): building takes nothing
    [InlineData(100, 80, 60, 0, 0, 100, 40)]      // b = 0: building gets only what roads do not need
    [InlineData(100, 80, 60, 100, 100, 0, 80)]    // b = 100: building first
    [InlineData(7, 7, 7, 50, 3, 4, 3)]            // odd S: E_b rounds DOWN, E_r takes the odd unit
    [InlineData(1, 1, 1, 99, 0, 1, 0)]            // S = 1 at 99: floor(0.99) = 0
    [InlineData(0, 10, 10, 50, 0, 0, 0)]          // zero supply
    [InlineData(100, 30, 10, 20, 20, 80, 30)]     // unused road share flows to building (cap 20 + 70)
    public void Split_NamedCases(long s, long db, long dr, int b, long eb, long er, long gb)
    {
        AllocationSplit x = ResourceAllocation.Split(s, db, dr, b);
        Assert.Equal((eb, er, gb), (x.BuildingShare, x.RoadShare, x.BuildingGrant));
    }

    [Fact]
    public void Split_IsOverflowSafe_AtLongMaxValue_AndRefusesBadInput()
    {
        foreach (int b in new[] { 0, 1, 33, 50, 99, 100 })
        {
            long eb = ResourceAllocation.BuildingShare(long.MaxValue, b);
            Assert.Equal((long)(new BigInteger(long.MaxValue) * b / 100), eb);
            AllocationSplit x = ResourceAllocation.Split(long.MaxValue, long.MaxValue, long.MaxValue, b);
            Assert.Equal(long.MaxValue, x.BuildingShare + x.RoadShare);
            Assert.Equal(eb, x.BuildingGrant);                       // D_r = MaxValue ≥ E_r: building keeps exactly its share
        }
        Assert.Equal(0, ResourceAllocation.Split(-5, 3, 3, 50).Supply);   // a negative holding counts as none
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceAllocation.Split(10, -1, 0, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceAllocation.Split(10, 0, -1, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceAllocation.BuildingShare(10, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceAllocation.BuildingShare(10, -1));
    }

    [Fact]
    public void TheEligibleGoods_AreEveryProjectInputAndRoadMaterial_FromContent()
    {
        var expected = new SortedSet<int>();
        foreach (ConstructionProjectEntry p in Shipped.Goods!.Projects!)
            foreach (ProjectInput i in p.Inputs) expected.Add(Shipped.Goods.IdOf(i.Good));
        foreach (RoadClassConfig c in Shipped.Roads!.Classes)
            foreach (RoadMaterialConfig m in c.MaterialsPerKm) expected.Add(Shipped.Goods.IdOf(m.Good));
        Assert.Equal([.. expected], ResourceAllocation.EligibleGoodIds(Shipped));
        Assert.Contains(Timber, ResourceAllocation.EligibleGoodIds(Shipped));
        Assert.Contains(Stone, ResourceAllocation.EligibleGoodIds(Shipped));
        Assert.Contains(Tools, ResourceAllocation.EligibleGoodIds(Shipped));
        Assert.DoesNotContain(Shipped.Goods.GrainId, ResourceAllocation.EligibleGoodIds(Shipped));
    }

    // ================================================================== the rule in the step

    [Theory]
    // b → S0's progress, S1's progress, the road's units, the timber left (100 timber; road 60; two granaries 40 + 20 each)
    [InlineData(100, N, 0 /* completes */, true, true)]
    [InlineData(70, N, 750_000, true, false)]
    [InlineData(50, N, 250_000, true, false)]
    [InlineData(0, N, 0, true, false)]
    public void TheSplitIsHonoured_WithCompetingHeadsAndARoadInOneStep_ExactDraws_NoUnitSpentTwice(
        int b, long p0, long p1, bool built0, bool built1)
    {
        WorldState w = Rig();
        long sunk0 = Sunk(w, Timber), stone0 = TotalOf(w, Stone);
        OrderRecord[] orders = b == ResourceAllocation.DefaultBuildingsPercent ? [Roads(0)] : [Split(0, b), Roads(0)];
        WorldState next = BuildAndRoads(Shipped, Log(orders)).Step(w);

        // G_b = min(80, floor(100 b / 100) + max(0, (100 − E_b) − 60)) — written out per case, not from the implementation.
        long grant = b switch { 100 => 80, 70 => 70, 50 => 50, 0 => 40, _ => throw new InvalidOperationException() };
        long roadGot = RoadCharge(Supply - grant, Road);
        (long P0, long P1, long Built0, long Built1, long Road, long Left) o = Outcome(next);
        Assert.Equal((built0 ? 0 : p0, built1 ? 0 : p1), (o.P0, o.P1));
        Assert.Equal((built0 ? 1L : 0L, built1 ? 1L : 0L), (o.Built0, o.Built1));
        Assert.Equal(roadGot, o.Road);
        Assert.Equal(Supply - grant - roadGot, o.Left);
        long stoneDrawn = (built0 ? 20 : ConstructionQuery.Delivered(20, p0)) + (built1 ? 20 : ConstructionQuery.Delivered(20, p1));
        Assert.Equal(stone0 - stoneDrawn, TotalOf(next, Stone));
        Assert.Equal(grant + roadGot, Sunk(next, Timber) - sunk0);          // every unit leaves once, through the Ledger
        AssertConserved(next);
        if (b != ResourceAllocation.DefaultBuildingsPercent)
            Assert.Equal(new ResourceAllocationRow(Player, b), Assert.Single(next.ResourceAllocations.Rows()));
        else Assert.Equal(0, next.ResourceAllocations.Count);
    }

    [Fact]
    public void WithoutARoadOrder_BuildingWorkMayUseEverything_AtAnySplit()
    {
        foreach (int b in new[] { 0, 30, 100 })
        {
            WorldState next = BuildAndRoads(Shipped, Log(Split(0, b))).Step(Rig());
            Assert.Equal((0L, 0L, 1L, 1L, 0L, Supply - 80), Outcome(next));   // both granaries completed; no road row
        }
    }

    [Fact]
    public void ReallocationWorksInBothDirections_AndTheLastValidOrderOfAStepWins()
    {
        // Turn 0: 30 then 0 (the last valid wins: 0); turn 1: back up to 100 explicitly; turn 2: a foreign-target order
        // (invalid) after a valid 60 — the valid one stands.
        WorldState w = Rig(timber: 0, heads: false);
        TurnExecutor exec = BuildAndRoads(Shipped, Log(Split(0, 30), Split(0, 0), Split(1, 100), Split(2, 60), Split(2, 20, target: 2)));
        WorldState t1 = exec.Step(w), t2 = exec.Step(t1), t3 = exec.Step(t2);
        Assert.Equal(0, w.ResourceAllocations.Count);
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(t1.ResourceAllocations.Rows()));
        Assert.Equal(new ResourceAllocationRow(Player, 100), Assert.Single(t2.ResourceAllocations.Rows()));
        Assert.Equal(new ResourceAllocationRow(Player, 60), Assert.Single(t3.ResourceAllocations.Rows()));

        // ... and the draws follow: lowering the share holds timber back for the road; raising it releases it.
        WorldState low = BuildAndRoads(Shipped, Log(Split(0, 0), Roads(0))).Step(Rig());
        WorldState high = BuildAndRoads(Shipped, Log(Split(0, 0), Split(0, 100), Roads(0))).Step(Rig());
        Assert.Equal(Road, RoadUnits(low));                                     // b = 0: the road is paid in full
        Assert.Equal(RoadCharge(Supply - 80, Road), RoadUnits(high));           // b = 100: building first
    }

    [Fact]
    public void AnOrderStampedT_GovernsTheStepFromT_TurnExact()
    {
        // The split stamped turn 1 is absent from state 1 and present in state 2, and it governs the step 1 → 2: that step's
        // road order is paid in full (b = 0: building work gets only the 40 the road does not need), so of the two heads
        // added at turn 1 the first (S1) completes and the second (S3) gets nothing.
        WorldState w = Rig(heads: false);
        TurnExecutor exec = BuildAndRoads(Shipped, Log(Split(1, 0), Roads(1)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, t1.Clock.Turn);
        Assert.Equal(0, t1.ResourceAllocations.Count);                          // not yet: the order is stamped turn 1
        Assert.Equal(0, RoadUnits(t1));
        Head(t1, S1, 1);
        Head(t1, S3, 1);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(t2.ResourceAllocations.Rows()));
        Assert.Equal(Road, RoadUnits(t2));                                      // the split applied in this very step
        Assert.Equal((1L, 0L), (ConstructionQuery.Built(t2, S1, Granary), ConstructionQuery.Built(t2, S3, Granary)));
        Assert.Equal(0, P(t2, S3));                                             // held for the road works

        // The same turn-1 state under the default (no split): building work first — both heads complete, the road gets 20.
        WorldState d1 = BuildAndRoads(Shipped, Log(Roads(1))).Step(Rig(heads: false));
        Head(d1, S1, 1);
        Head(d1, S3, 1);
        WorldState d2 = BuildAndRoads(Shipped, Log(Roads(1))).Step(d1);
        Assert.Equal((1L, 1L, RoadCharge(Supply - 80, Road)),
            (ConstructionQuery.Built(d2, S1, Granary), ConstructionQuery.Built(d2, S3, Granary), RoadUnits(d2)));
    }

    [Fact]
    public void TheSplitPersistsAcrossTurns_WithNoFurtherOrder()
    {
        TurnExecutor exec = BuildAndRoads(Shipped, Log(Split(0, 0), Roads(2)));
        WorldState t1 = exec.Step(Rig(timber: 0)), t2 = exec.Step(t1);
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(t2.ResourceAllocations.Rows()));
        Set(t2, S2, Timber, Supply);
        WorldState t3 = exec.Step(t2);                                          // turn-2 road under the turn-0 split
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(t3.ResourceAllocations.Rows()));
        Assert.Equal(Road, RoadUnits(t3));
        Assert.Equal(Supply, Sunk(t3, Timber) - Sunk(t2, Timber));             // 60 to the road, 40 to the first head
        Assert.Equal((1L, 0L, 0L), (ConstructionQuery.Built(t3, S0, Granary), ConstructionQuery.Built(t3, S1, Granary), P(t3, S1)));
    }

    [Fact]
    public void StarvationGuard_ARoadOrderThatCanPayNothing_WithholdsNothing()
    {
        // A test content whose trackway also needs tools (1 per km). With no tools the road can pay NOTHING (ADR-032: the
        // order ends at its first step), so it withholds no timber even at b = 0; with tools it holds its 60 timber back.
        RoadClassConfig[] classes = [.. Shipped.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway)
                classes[i] = classes[i] with { MaterialsPerKm = [new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", 1.0)] };
        SimConfig cfg = Shipped with { Roads = Shipped.Roads with { Classes = classes } };

        WorldState none = Rig();                                                // 0 tools
        Assert.Equal(0, TotalOf(none, Tools));
        WorldState a = BuildAndRoads(cfg, Log(Split(0, 0), Roads(0))).Step(none);
        Assert.Equal((0L, 0L, 1L, 1L, 0L, Supply - 80), Outcome(a));           // both granaries took all they needed

        WorldState some = Rig();
        Set(some, S3, Tools, Road);
        WorldState b = BuildAndRoads(cfg, Log(Split(0, 0), Roads(0))).Step(some);
        Assert.Equal((0L, 0L, 1L, 0L, 2 * Road, 0L), Outcome(b));               // 40 to S0's head; 60 timber + 60 tools to the road
    }

    /// <summary>A test content whose trackway needs <paramref name="materials"/> per km.</summary>
    private static SimConfig TrackwayCosting(params RoadMaterialConfig[] materials)
    {
        RoadClassConfig[] classes = [.. Shipped.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway) classes[i] = classes[i] with { MaterialsPerKm = materials };
        return Shipped with { Roads = Shipped.Roads with { Classes = classes } };
    }

    [Theory]
    // b → S0's progress, S1's progress, S0 built, S1 built, the road's units (timber + tools), the timber left.
    // The trackway needs 1 timber and 0.025 tools per km: the 60 km route costs 60 timber and ceil(1.5) = 2 tools; the
    // purse holds 100 timber and 2 tools; the two granaries need 80 timber (D_b), the road 60 timber + 2 tools (D_r, at S).
    [InlineData(100, 0L, 0L, true, true, 0L, 20L)]      // building first: 80 to building; 20 timber + 2 tools pay no step
    [InlineData(90, 0L, 0L, true, true, 0L, 20L)]       // E_b = 90 ≥ D_b: nothing held back
    [InlineData(75, 0L, 0L, true, true, 0L, 20L)]       // E_b = 75 would hold 5 back for a road the 25 + 2 left cannot pay
    [InlineData(71, 0L, 0L, true, true, 0L, 20L)]       // the boundary: 29 timber + 2 tools floors the tools charge to 0
    [InlineData(70, 0L, 750_000L, true, false, 31L, 0L)] // 30 + 2 pays half the route (30 timber + 1 tool): the split binds
    [InlineData(0, 0L, 0L, true, false, 62L, 0L)]       // b = 0: building gets the 40 the road leaves; the road is paid in full
    public void StarvationGuard_ARoadOrderThatCanPayNothingFromWhatTheSplitLeavesIt_WithholdsNothing(
        int b, long p0, long p1, bool built0, bool built1, long road, long left)
    {
        // c2 verifier F1: the guard at full supply alone let b = 71..75 hold timber back from building for a road order
        // that, on what the split left it, could pay NOTHING (ADR-032's min-ratio floors the 2-tool charge to 0) — the
        // timber then sat idle. A road order that can pay nothing from what building work leaves it withholds nothing.
        SimConfig cfg = TrackwayCosting(new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", 0.025));
        WorldState w = Rig();
        Set(w, S3, Tools, 2);
        long tools0 = TotalOf(w, Tools);
        OrderRecord[] orders = b == ResourceAllocation.DefaultBuildingsPercent ? [Roads(0)] : [Split(0, b), Roads(0)];
        WorldState next = BuildAndRoads(cfg, Log(orders)).Step(w);
        Assert.Equal((p0, p1, built0 ? 1L : 0L, built1 ? 1L : 0L, road, left),
            (P(next, S0), P(next, S1), ConstructionQuery.Built(next, S0, Granary), ConstructionQuery.Built(next, S1, Granary),
             RoadUnits(next), TotalOf(next, Timber)));
        Assert.Equal(road == 0 ? 0 : road == 31 ? 1 : 2, tools0 - TotalOf(next, Tools));
        AssertConserved(next);

        // The head's status (the UI's estimate) agrees: S1's head is held for the road works only where the split binds.
        HeadStatusReport? s1 = ConstructionQuery.HeadStatus(w, cfg, S1, orders, 10.0);
        Assert.Equal(built1, s1!.Resolution.Completes);
        Assert.Equal(b is 70 or 0, s1.Cause == MaterialCause.HeldForRoads);

        // The split names the guard, takes the road's demand as nothing, and its forecast IS the step: building work's
        // timber and the road's timber and tools, unit for unit.
        bool guarded = b is 75 or 71;
        AllocationView view = ResourceAllocationQuery.ForEmpire(w, cfg, Player, orders, 10.0)!;
        AllocationSplit timber = view.Split.SplitOf(Timber)!.Value;
        Assert.Equal((guarded, guarded ? 0 : Road), (view.Split.RoadPaysNothingFromItsShare, timber.RoadDemand));
        Assert.False(guarded && timber.HoldsBack);
        long roadTools = tools0 - TotalOf(next, Tools), roadTimber = road - roadTools;
        Assert.Equal(Supply - left - roadTimber, view.Split.BuildingDrawOf(Timber));
        Assert.Equal((roadTimber, roadTools), (view.Split.RoadChargeOf(Timber), view.Split.RoadChargeOf(Tools)));
    }

    [Fact]
    public void BuildingDemand_CountsOnlyTheEmpiresOwnHeads_WithARivalHeadEarlierInTheBuildingOrder()
    {
        // c2 verifier F2. Two Empires (S0, S2 the player's; S1, S3 the rival's). The rival's head at S1 is FIRST in the
        // building order (enqueued turn 0) and needs stone above all (a test project: 2 timber, 40 stone); the player's
        // granary at S0 comes second (turn 1). The player's purse holds 100 timber and 30 stone; the rival's 100 timber
        // and 1,000 stone. The player's split is b = 50 with a road order on S0–S2 (60 km trackway: 60 timber).
        //
        // By hand, the player's D_b is its own granary's need: 40 timber, 20 stone — what the switch-off step draws from
        // its purse. Timber: E_b = 50, E_r = 50 < D_r = 60, cap 50, G_b = 40; stone: D_r = 0, G_b = 20. So the granary is
        // built and the road is paid in full. (Counting the rival's head into the player's demand would spend the
        // player's 30 stone on it, leave the granary none, and grant it 2 timber.)
        const int Quarry = 91;
        SimConfig cfg = WithWorks(Shipped, labor: 0.5, id: Quarry, timber: 2, stone: 40);
        SimConfig off = TestConfigs.PreAllocationRatios(cfg);
        WorldState w = Duo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S2, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S2, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S2, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Set(w, S2, Timber, 100);
        Set(w, S2, Stone, 30);
        Set(w, S3, Timber, 100);
        Set(w, S3, Stone, 1000);
        Head(w, S1, 0, Quarry);                                                  // the rival's, first in the building order
        Head(w, S0, 1);                                                          // the player's granary, second
        OrderRecord[] orders = [Split(0, 50), Roads(0)];

        long PlayerTimber(IReadOnlyWorldState x) => Of(x, S0, Timber) + Of(x, S2, Timber);
        long PlayerStone(IReadOnlyWorldState x) => Of(x, S0, Stone) + Of(x, S2, Stone);
        WorldState offBuilt = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(off)], Log(orders)).Step(w);
        Assert.Equal((40L, 20L), (PlayerTimber(w) - PlayerTimber(offBuilt), PlayerStone(w) - PlayerStone(offBuilt)));   // the switch-off draw

        WorldState built = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg)], Log(orders)).Step(w);
        Assert.Equal((40L, 20L), (PlayerTimber(w) - PlayerTimber(built), PlayerStone(w) - PlayerStone(built)));          // G_b = D_b here
        WorldState next = BuildAndRoads(cfg, Log(orders)).Step(w);
        Assert.Equal((1L, Road), (ConstructionQuery.Built(next, S0, Granary), RoadUnits(next)));
        AssertConserved(next);

        Table<ResourceAllocationRow> rows = ConstructionQuery.AllocationsWith(w, cfg, w.ResourceAllocations, orders);
        EmpireAllocation?[] step = ConstructionQuery.StepAllocations(w, cfg, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks,
            rows, orders, 10.0)!;
        AllocationSplit timber = step[0]!.SplitOf(Timber)!.Value, stone = step[0]!.SplitOf(Stone)!.Value;
        Assert.Equal(Player, step[0]!.Polity);
        Assert.Equal((100L, 40L, Road, 50L, 40L), (timber.Supply, timber.BuildingDemand, timber.RoadDemand, timber.BuildingCap, timber.BuildingGrant));
        Assert.Equal((30L, 20L, 0L, 30L, 20L), (stone.Supply, stone.BuildingDemand, stone.RoadDemand, stone.BuildingCap, stone.BuildingGrant));
        Assert.Null(step[1]);                                                    // the rival: no row, building first
    }

    /// <summary>
    /// c2 verifier F3. 50 timber for two granaries (40 timber, 20 stone each): with no split the first takes 40 timber and
    /// 20 stone, the second the last 10 timber and ceil(20 × 10 / 40) = 5 stone — so the heads DRAW 50 timber and 25 stone,
    /// never the 80 and 40 both need, exactly what the switch-off step draws. At b = 50 with the 60-timber road: E_b = 25,
    /// E_r = 25 &lt; 60, so G_b = 25 &lt; D_b — the split holds 25 timber back, and says so.
    ///
    /// D-055 RE-STATEMENT (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.3 and §1.7, a WS2 pin
    /// moved by D_b = claims [L2-05]; MEASURED on this tree by the agent writing this line). This test was
    /// <c>BuildingDemand_UnderAShortageInTheEmpire_IsWhatItsHeadsWouldDraw_NotTheSumOfTheirNeeds</c>, and pinned the stone
    /// split (Supply, D_b, G_b, HoldsBack) = (1000, 25, 25, False) and the UI's (50, 25). CAUSE: building demand D_b is now
    /// what the heads would DRAW OR SET ASIDE (ConstructionQuery.OwnHeadClaims, through HoldsWalk with no split): the second
    /// granary, at 250,000 after its 10 timber and 5 stone, sets aside its remaining 15 stone (the purse has 975 left) and no
    /// timber (none is left) — stone D_b = 20 + 5 + 15 = 40, timber D_b = 40 + 10 + 0 = 50, still never the 80 and 40 both
    /// need. NEW: stone (1000, 40, 40, False), the UI's (50, 40); the timber split and the step are unchanged (the step's
    /// draws are D_b's draws within the grant; at b = 50 the first head takes the 25 timber grant, 625,000). ATTRIBUTION: the
    /// old figures reproduce under TestConfigs.PreReservations, the batch-3 rule (the second arm, <c>reservations: false</c>).
    /// </summary>
    [Theory]
    [InlineData(true, 40L)]
    [InlineData(false, 25L)]
    public void BuildingDemand_UnderAShortageInTheEmpire_IsWhatItsHeadsWouldDrawOrSetAside_NotTheSumOfTheirNeeds(bool reservations, long stoneDemand)
    {
        SimConfig shipped = reservations ? Shipped : TestConfigs.PreReservations(Shipped);
        SimConfig off = TestConfigs.PreAllocationRatios(shipped);
        WorldState w = Rig(timber: 50);
        OrderRecord[] orders = [Split(0, 50), Roads(0)];
        WorldState offBuilt = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(off)], Log(orders)).Step(w);
        Assert.Equal((50L, 25L), (TotalOf(w, Timber) - TotalOf(offBuilt, Timber), TotalOf(w, Stone) - TotalOf(offBuilt, Stone)));
        // The switch-off step's set-aside is the difference: the second granary's remaining 15 stone (none with the rule off).
        Assert.Equal(reservations ? new[] { new ConstructionHoldRow(Reservations.PolityPurse(Player), Stone, 15) }
            : Array.Empty<ConstructionHoldRow>(), offBuilt.ConstructionHolds.Rows());

        Table<ResourceAllocationRow> rows = ConstructionQuery.AllocationsWith(w, shipped, w.ResourceAllocations, orders);
        EmpireAllocation alloc = ConstructionQuery.StepAllocations(w, shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks,
            rows, orders, 10.0)![0]!;
        AllocationSplit timber = alloc.SplitOf(Timber)!.Value, stone = alloc.SplitOf(Stone)!.Value;
        Assert.Equal((50L, 50L, 25L, 25L, true), (timber.Supply, timber.BuildingDemand, timber.BuildingCap, timber.BuildingGrant, timber.HoldsBack));
        Assert.Equal((1000L, stoneDemand, stoneDemand, false), (stone.Supply, stone.BuildingDemand, stone.BuildingGrant, stone.HoldsBack));

        // The UI's figures are the same reading.
        AllocationView view = ResourceAllocationQuery.ForEmpire(w, shipped, Player, orders, 10.0)!;
        Assert.Equal((50L, stoneDemand), (view.Split.SplitOf(Timber)!.Value.BuildingDemand, view.Split.SplitOf(Stone)!.Value.BuildingDemand));

        // ... and the step: building work takes its 25 timber (S0's head, 25 of 40), the road the 25 left of its 60.
        WorldState next = BuildAndRoads(shipped, Log(orders)).Step(w);
        Assert.Equal((625_000L, 0L, RoadCharge(25, Road), 25 - RoadCharge(25, Road)),
            (P(next, S0), P(next, S1), RoadUnits(next), TotalOf(next, Timber)));
        AssertConserved(next);
    }

    /// <summary>A deterministic xorshift for the randomised rig (the platform RNG is a banned construct).</summary>
    private sealed class XorShift(ulong seed)
    {
        private ulong _s = seed;
        public ulong Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
        public long Below(long n) => (long)(Next() % (ulong)n);
        public int Int(int n) => (int)(Next() % (ulong)n);
    }

    /// <summary>
    /// Two Empires (S0, S2 the player's; S1, S3 the rival's), each with its own route (no shared edge, so each road order's
    /// plan is the same on PREV and NEXT), a trackway needing 1 timber and 0.05 tools per km, a head at every settlement (a
    /// granary or a 30-timber/10-stone test project), random stocks, and random splits and road orders for both, four steps
    /// a trial. Every step, for every Empire with a split: S is its purse; D_b is exactly what the switch-off step draws from
    /// it AND SETS ASIDE (D-055); the forecast building draw is exactly what building work draws, within the grant; the
    /// forecast road charge is exactly what road development draws, at least min(D_r, E_r) is left for it; and where the
    /// guard fired, the road draws nothing and building work drew the switch-off draw. An Empire with no split draws as with
    /// the switch off. The Ledger audit holds and no stock goes negative.
    ///
    /// D-055 RE-STATEMENT (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.3 and §1.7, a WS2 pin
    /// moved by D_b = claims [L2-05]; MEASURED on this tree by the agent writing this line). The batch-3 check
    /// <c>(x.Supply, x.BuildingDemand) == (s0, offDraw)</c> failed on the D-055 tree at its first step ("Expected (33, 26)
    /// Actual (33, 30)"). CAUSE: D_b is now building work's CLAIM — the heads' draws plus what they set aside with no split
    /// (ConstructionQuery.OwnHeadClaims through HoldsWalk) — and the 4 units are a waiting head's set-aside. RESTATED: D_b ==
    /// offDraw + offHeld, where offHeld is the purse's set-aside the switch-off step itself publishes (its
    /// ConstructionHolds rows for that purse and good). With the rule off (TestConfigs.PreReservations, the second arm)
    /// nothing is published, offHeld is 0 at every step, and the check is batch 3's exactly — the old pin reproduces. The
    /// D-055 properties of the split (draws + holds ≤ G_b, holds ≤ the holding after draws, S − d − h ≥ min(D_r, E_r), the
    /// oldest head unblocked) are ConstructionReservationTests' property test (#20).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheForecastIsTheStep_TwoEmpires_RandomSplitsHeadsAndRoads_ConservedAndGuarded(bool reservations)
    {
        SimConfig cfg = WithWorks(TrackwayCosting(new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", 0.05)),
            labor: 0.5, timber: 30, stone: 10);
        if (!reservations) cfg = TestConfigs.PreReservations(cfg);
        SimConfig off = TestConfigs.PreAllocationRatios(cfg);
        var rng = new XorShift(2026);
        int[] goods = [Timber, Stone, Tools];
        int bound = 0, guardedSteps = 0, roadPaid = 0, setAside = 0;
        for (int trial = 0; trial < 50; trial++)
        {
            WorldState w = Duo();
            w.TransportEdges.Clear();
            w.SettlementDistances.Clear();
            w.TradeFlows.Clear();
            foreach (PolityId p in new[] { Player, Rival })
                w.ResearchCompleted.Add(new ResearchCompletedRow(p, Research.Nodes[Research.IndexOfId("track_road")].Key));
            foreach ((int id, SettlementId a, SettlementId b, long km) in new[] { (1, S0, S2, 50L), (2, S1, S3, 40L) })
            {
                w.TransportEdges.Add(new TransportEdgeRow(id, a, b, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
                    100_000, km, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
                w.SettlementDistances.Add(new SettlementDistanceRow(a, b, 100.0));
                w.SettlementDistances.Add(new SettlementDistanceRow(b, a, 100.0));
            }
            foreach (SettlementId s in new[] { S0, S1, S2, S3 })
            {
                AllBuilders(w, s);
                Head(w, s, rng.Int(3), rng.Int(2) == 0 ? Granary : Works);
                Head(w, s, 5);
            }
            for (int turn = 0; turn < 4; turn++)
            {
                foreach (SettlementId s in new[] { S0, S1, S2, S3 })
                {
                    Set(w, s, Timber, rng.Below(60));
                    Set(w, s, Stone, rng.Below(40));
                    Set(w, s, Tools, rng.Below(4));
                }
                long t = w.Clock.Turn;
                var batch = new List<OrderRecord>();
                foreach (PolityId p in new[] { Player, Rival })
                {
                    if (rng.Int(3) != 0) batch.Add(Split(t, rng.Int(4) == 0 ? 100 : rng.Int(101), p));
                    if (rng.Int(4) != 0) batch.Add(Roads(t, 100.0, p));
                }
                OrderLog log = Log([.. batch]);
                Table<ResourceAllocationRow> rows = ConstructionQuery.AllocationsWith(w, cfg, w.ResourceAllocations, batch);
                EmpireAllocation?[] step = ConstructionQuery.StepAllocations(w, cfg, w.ConstructionQueue, w.ConstructionProgress,
                    w.GoodStocks, rows, batch, 10.0)!;
                WorldState built = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg)], log).Step(w);
                WorldState builtOff = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(off)], log).Step(w);
                WorldState next = BuildAndRoads(cfg, log).Step(w);
                AssertConserved(next);
                for (int k = 0; k < w.Polities.Count; k++)
                {
                    PolityId p = w.Polities[k].Id;
                    long Purse(IReadOnlyWorldState x, int g) =>
                        EmpireStockpile.Available(x.GoodStocks, RoadDevelopmentQuery.PayingSettlements(w, p), new GoodId(g));
                    foreach (int g in goods)
                    {
                        long s0 = Purse(w, g), buildDraw = s0 - Purse(built, g), offDraw = s0 - Purse(builtOff, g);
                        long roadDraw = Purse(built, g) - Purse(next, g);
                        long offHeld = 0;
                        for (int i = 0; i < builtOff.ConstructionHolds.Count; i++)
                            if (builtOff.ConstructionHolds[i].Purse == Reservations.PolityPurse(p) && builtOff.ConstructionHolds[i].Good == g)
                                offHeld += builtOff.ConstructionHolds[i].Units;
                        if (!reservations) Assert.Equal(0, offHeld);                // the batch-3 rule sets nothing aside
                        Assert.True(Purse(next, g) >= 0, "no stock goes negative");
                        if (step[k] is not { } a)
                        {
                            Assert.Equal(ResourceAllocation.DefaultBuildingsPercent, ResourceAllocation.EffectivePercent(rows, p));
                            Assert.Equal(offDraw, buildDraw);
                            continue;
                        }
                        AllocationSplit x = a.SplitOf(g)!.Value;
                        Assert.Equal((s0, offDraw + offHeld), (x.Supply, x.BuildingDemand));
                        if (offHeld > 0) setAside++;
                        Assert.Equal(buildDraw, a.BuildingDrawOf(g));
                        Assert.Equal(roadDraw, a.RoadChargeOf(g));
                        Assert.True(buildDraw <= x.BuildingGrant, "building work draws within its grant");
                        Assert.True(s0 - buildDraw >= Math.Min(x.RoadDemand, x.RoadShare), "road works find at least min(D_r, E_r)");
                        if (a.RoadPaysNothingFromItsShare) Assert.Equal((0L, offDraw), (roadDraw, buildDraw));
                        if (x.HoldsBack) bound++;
                        if (roadDraw > 0) roadPaid++;
                    }
                    if (step[k] is { RoadPaysNothingFromItsShare: true }) guardedSteps++;
                }
                w = next;
            }
        }
        Assert.True(bound > 20, $"anti-vacuity: the split held materials back {bound} times");
        Assert.True(guardedSteps > 3, $"anti-vacuity: the guard fired {guardedSteps} times");
        Assert.True(roadPaid > 20, $"anti-vacuity: road works were paid {roadPaid} times");
        if (reservations) Assert.True(setAside > 20, $"anti-vacuity: D_b included a set-aside {setAside} times");
    }

    [Fact]
    public void TheDefault_IsByteIdenticalToTheSwitchOff_AndAnExplicit100DiffersOnlyByItsRow()
    {
        // WS1 on in all three. The default (no row) and the switch off draw identically; an explicit 100 writes its row and
        // nothing else differs.
        foreach (long timber in new[] { 0L, 30L, Supply, 500L })
        {
            WorldState def = BuildAndRoads(Shipped, Log(Roads(0))).Step(Rig(timber));
            WorldState off = BuildAndRoads(Off, Log(Roads(0))).Step(Rig(timber));
            WorldState hundred = BuildAndRoads(Shipped, Log(Split(0, 100), Roads(0))).Step(Rig(timber));
            Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(def));
            Assert.Equal(new ResourceAllocationRow(Player, 100), Assert.Single(hundred.ResourceAllocations.Rows()));
            hundred.ResourceAllocations.Clear();
            Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(hundred));
        }
        // b = 100 is skipped outright: no split object, so no road forecast and no demand pass is made for the Empire.
        WorldState w = Rig();
        w.ResourceAllocations.Add(new ResourceAllocationRow(Player, 100));
        OrderRecord[] batch = [Roads(0)];
        EmpireAllocation?[]? step = ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks,
            w.ResourceAllocations, batch, 10.0);
        Assert.NotNull(step);
        Assert.All(step!, e => Assert.Null(e));
        w.ResourceAllocations[0] = new ResourceAllocationRow(Player, 99);
        step = ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, w.ResourceAllocations, batch, 10.0);
        Assert.NotNull(step![0]);
    }

    [Fact]
    public void WithTheSwitchOff_AKind12OrderChangesNothing()
    {
        WorldState a = BuildAndRoads(Off, Log(Split(0, 0), Roads(0))).Step(Rig());
        WorldState b = BuildAndRoads(Off, Log(Roads(0))).Step(Rig());
        Assert.Equal(0, a.ResourceAllocations.Count);
        Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
    }

    // ================================================================== delivery-time validation

    [Fact]
    public void DeliveryValidation_IgnoresAForeignTarget_ANonRosterActor_AndMalformedSharesAppendedInMemory()
    {
        WorldState w = Rig(timber: 0, heads: false);
        var reference = WorldHash.ComputeHex(BuildAndRoads(Shipped, null).Step(w));
        foreach (OrderRecord bad in new[]
                 {
                     Split(0, 40, target: 2),                                               // another Empire's split
                     Split(0, 40, actor: new PolityId(9)),                                   // not on the roster
                     new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, double.NaN),
                     new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, 2.5),
                     new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, -1.0),
                     new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, 101.0),
                     new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, -1, 50.0),
                 })
        {
            var log = new OrderLog();
            log.Append(bad);                                                        // in memory: Append never validates
            WorldState next = BuildAndRoads(Shipped, log).Step(w);
            Assert.Equal(0, next.ResourceAllocations.Count);
            Assert.Equal(reference, WorldHash.ComputeHex(next));
            Assert.False(ConstructionQuery.AllocationOrderValid(w, Shipped, bad));
        }
        Assert.True(ConstructionQuery.AllocationOrderValid(w, Shipped, Split(0, 0)));
        Assert.True(ConstructionQuery.AllocationOrderValid(w, Shipped, Split(0, 100)));
        Assert.False(ConstructionQuery.AllocationOrderValid(w, Off, Split(0, 40)));                       // switch off
        Assert.False(ConstructionQuery.AllocationOrderValid(w, Shipped, Roads(0)));                        // another kind
    }

    // ================================================================== one rule, two callers (the road order and its charges)

    [Fact]
    public void DeliveredOrderFor_IsTheOrderTheRoadSystemApplies_FirstPerActor_SlotTakenBeforeTheChecks()
    {
        WorldState w = Rig();
        OrderRecord bad = new(0, Player.Value, OrderKind.DevelopRoads, 0, 0.0);   // in memory: out of (0, 100]
        OrderRecord good = Roads(0, 50.0);
        OrderRecord[] badFirst = [bad, good], goodFirst = [good, Roads(0, 100.0)];
        Assert.Null(RoadDevelopmentQuery.DeliveredOrderFor(w, Shipped, badFirst, Player));
        Assert.Equal(good, RoadDevelopmentQuery.DeliveredOrderFor(w, Shipped, goodFirst, Player));
        Assert.Null(RoadDevelopmentQuery.DeliveredOrderFor(w, Shipped, [Roads(0, 50.0, new PolityId(9))], new PolityId(9)));   // not on the roster
        Assert.Null(RoadDevelopmentQuery.DeliveredOrderFor(w, Shipped with { Roads = null }, goodFirst, Player));                 // inert

        // The system agrees: the bad-first batch builds nothing for the player; the good-first batch applies 50%.
        var noHeads = Rig(heads: false);
        WorldState a = BuildAndRoads(Shipped, Log(badFirst)).Step(noHeads);
        Assert.Equal(0, a.RoadDevelopments.Count);
        WorldState b = BuildAndRoads(Shipped, Log(goodFirst)).Step(Rig(heads: false));
        Assert.Equal(Road, RoadUnits(b));
    }

    [Fact]
    public void SimulateCharges_EqualsWhatTheRoadSystemCharges_AcrossPartialStepsAndTheStopRule()
    {
        // Three routes (S0–S1 60 km, S0–S2 30 km, S1–S3 45 km), BuiltRoad-like two-good costs (a test content: the trackway
        // needs 1 timber and 2 stone per km), and every holding from nothing to plenty: the forecast and the system's real
        // charges agree unit for unit.
        RoadClassConfig[] classes = [.. Shipped.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway)
                classes[i] = classes[i] with { MaterialsPerKm = [new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("stone", 2.0)] };
        SimConfig cfg = Shipped with { Roads = Shipped.Roads with { Classes = classes } };
        int checkedCases = 0;
        foreach (long timber in new long[] { 0, 1, 7, 59, 60, 61, 89, 90, 134, 135, 136, 400 })
            foreach (long stone in new long[] { 0, 1, 119, 120, 121, 180, 269, 270, 271, 1000 })
            {
                WorldState w = Rig(timber, stone, heads: false);
                w.TransportEdges.Add(new TransportEdgeRow(2, S0, S2, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
                    100_000, 30, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
                w.TransportEdges.Add(new TransportEdgeRow(3, S1, S3, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
                    100_000, 45, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
                foreach ((SettlementId x, SettlementId y) in new[] { (S0, S2), (S1, S3) })
                {
                    w.SettlementDistances.Add(new SettlementDistanceRow(x, y, 100.0));
                    w.SettlementDistances.Add(new SettlementDistanceRow(y, x, 100.0));
                }
                var available = new long[Math.Max(Timber, Stone) + 1];
                available[Timber] = TotalOf(w, Timber);
                available[Stone] = TotalOf(w, Stone);
                long[] forecast = RoadDevelopmentQuery.SimulateCharges(w, cfg, Player, 100.0, available);
                WorldState next = BuildAndRoads(cfg, Log(Roads(0))).Step(w);
                Assert.Equal((TotalOf(w, Timber) - TotalOf(next, Timber), TotalOf(w, Stone) - TotalOf(next, Stone)),
                    (forecast[Timber], forecast[Stone]));
                checkedCases++;
            }
        Assert.Equal(120, checkedCases);
    }

    [Fact]
    public void SimulateCharges_EqualsWhatTheRoadSystemCharges_WhenALaterRouteNeedsOnlyAGoodTheFirstLeft()
    {
        // The stop rules bite only when a later route could still be paid: a test content whose best class (BuiltRoad,
        // made known by the trackway's entity) needs 1 timber and 2 stone per km, with S0–S1 (60 km) and S1–S3 (45 km)
        // dirt paths and S0–S2 (30 km) already a trackway (1 timber per km) — so S0–S2 costs STONE ONLY. After S0–S1 is
        // paid in part (short of timber) or not at all (no timber), road development ENDS the order and S0–S2 is never
        // paid, although the stone for it is there; the forecast must end with it.
        RoadClassConfig[] classes = [.. Shipped.Roads!.Classes];
        string trackEntity = "";
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway) trackEntity = classes[i].Entity!;
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway + 1)
                classes[i] = classes[i] with { Entity = trackEntity, MaterialsPerKm = [new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("stone", 2.0)] };
        SimConfig cfg = Shipped with { Roads = Shipped.Roads with { Classes = classes } };
        int stopped = 0;
        foreach (long timber in new long[] { 0, 1, 30, 59, 60, 104, 105, 400 })
            foreach (long stone in new long[] { 0, 59, 60, 119, 120, 179, 180, 270, 1000 })
            {
                WorldState w = Rig(timber, stone, heads: false);
                w.TransportEdges.Add(new TransportEdgeRow(2, S0, S2, EdgeTypes.Trackway, TransportModes.Road, TransportEdgeStates.Complete,
                    150_000, 30, 0, 0, 0, EdgeTypes.Trackway, 0.0, 0.85));
                w.TransportEdges.Add(new TransportEdgeRow(3, S1, S3, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
                    100_000, 45, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
                foreach ((SettlementId x, SettlementId y) in new[] { (S0, S2), (S1, S3) })
                {
                    w.SettlementDistances.Add(new SettlementDistanceRow(x, y, 100.0));
                    w.SettlementDistances.Add(new SettlementDistanceRow(y, x, 100.0));
                }
                RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(w, Research, cfg.Roads!, cfg.Goods!, Player, 100.0);
                Assert.Equal(3, plan.Length);
                Assert.Equal((S0, S1), (plan[0].Route.A, plan[0].Route.B));
                RoadMaterialCost stoneOnly = Assert.Single(plan[1].Cost);                    // S0–S2: stone only
                Assert.Equal((Stone, 60L), (stoneOnly.Good.Value, stoneOnly.Units));
                var available = new long[Math.Max(Timber, Stone) + 1];
                available[Timber] = TotalOf(w, Timber);
                available[Stone] = TotalOf(w, Stone);
                long[] forecast = RoadDevelopmentQuery.SimulateCharges(w, cfg, Player, 100.0, available);
                WorldState next = BuildAndRoads(cfg, Log(Roads(0))).Step(w);
                Assert.Equal((TotalOf(w, Timber) - TotalOf(next, Timber), TotalOf(w, Stone) - TotalOf(next, Stone)),
                    (forecast[Timber], forecast[Stone]));
                if (timber < 60 && stone >= 180) { stopped++; Assert.True(next.RoadDevelopments.Count <= 1, "the order ends at S0–S1"); }
            }
        Assert.True(stopped >= 8, "anti-vacuity: cases in which a stone-only route is left unpaid by the stop rules");
    }

    // ================================================================== the estimate the UI reads

    [Fact]
    public void TheHeadsStatus_SaysHeldForRoads_AndTheEstimateAgreesWithTheStep()
    {
        // b = 0 and the road ordered (both pending): S1's head is second in the building order; S0's takes 40 of the 40 the
        // split grants, so S1's waits — the rest is held for the road works.
        WorldState w = Rig();
        OrderRecord[] pending = [Split(0, 0), Roads(0)];
        HeadStatusReport? s1 = ConstructionQuery.HeadStatus(w, Shipped, S1, pending, 10.0);
        Assert.NotNull(s1);
        Assert.False(s1!.Resolution.Progresses);
        Assert.Equal(HeadLimitKind.Materials, s1.Resolution.Limit);
        Assert.Equal(Timber, s1.Resolution.LimitGood);
        Assert.True(s1.Resolution.HeldForRoads);
        Assert.Equal(MaterialCause.HeldForRoads, s1.Cause);
        // Without the split the same head would be taken by the earlier head only partly — it progresses.
        HeadStatusReport? free = ConstructionQuery.HeadStatus(w, Shipped, S1, [Roads(0)], 10.0);
        Assert.True(free!.Resolution.Progresses);
        Assert.False(free.Resolution.HeldForRoads);

        // The estimate is the step: S0's realised draw and progress equal HeadStatus's, at b = 50 (nothing between turn
        // start and building work moves timber or stone in this rig).
        OrderRecord[] fifty = [Split(0, 50), Roads(0)];
        HeadStatusReport? e0 = ConstructionQuery.HeadStatus(w, Shipped, S0, fifty, 10.0);
        HeadStatusReport? e1 = ConstructionQuery.HeadStatus(w, Shipped, S1, fifty, 10.0);
        WorldState next = BuildAndRoads(Shipped, Log(fifty)).Step(w);
        Assert.Equal(N, e0!.Resolution.ProgressAfter);
        Assert.Equal(1, ConstructionQuery.Built(next, S0, Granary));
        Assert.Equal(P(next, S1), e1!.Resolution.ProgressAfter);
        Assert.Equal([10L, 5L], e1.Resolution.Draws);                         // 10 timber, 5 stone: the grant's last 10

        // ResourceAllocationQuery (the UI's figures): the grant and the shares, per good, as the step used them.
        AllocationView? view = ResourceAllocationQuery.ForEmpire(w, Shipped, Player, fifty, 10.0);
        AllocationSplit timber = view!.Split.SplitOf(Timber)!.Value;
        Assert.Equal((Supply, 50L, 50L, 80L, Road, 50L, 50L),
            (timber.Supply, timber.BuildingShare, timber.RoadShare, timber.BuildingDemand, timber.RoadDemand, timber.BuildingCap, timber.BuildingGrant));
        Assert.Equal((50, 100, false, (int?)50), (view.BuildingsPercent, view.CommittedPercent, view.HasRow, view.QueuedPercent));
        Assert.Equal(Supply - TotalOf(next, Timber) - RoadUnits(next), timber.BuildingGrant);
    }

    [Fact]
    public void NoRatio_ForAStatelessSettlement_OrAWorldWithNoControls()
    {
        // S1 unruled: its head is its own purse (it holds 100 timber and 20 stone), untouched by the player's b = 0 — which
        // does bind the player's own head and road (S0's head takes the 40 the road leaves; the road is paid in full).
        WorldState w = Rig();
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != S1) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow r in kept) w.Controls.Add(r);
        Set(w, S1, Timber, Supply);
        Set(w, S1, Stone, 20);
        WorldState next = BuildAndRoads(Shipped, Log(Split(0, 0), Roads(0))).Step(w);
        Assert.Equal((1L, 1L, Road), (ConstructionQuery.Built(next, S1, Granary), ConstructionQuery.Built(next, S0, Granary), RoadUnits(next)));
        Assert.Equal(Supply - 40, Of(next, S1, Timber));                        // S1 paid its own 40 and lent nothing

        WorldState bare = Rig();
        bare.Controls.Clear();
        Assert.Null(ConstructionQuery.StepAllocations(bare, Shipped, bare.ConstructionQueue, bare.ConstructionProgress, bare.GoodStocks,
            bare.ResourceAllocations, [], 10.0));
        Assert.Null(ResourceAllocationQuery.ForEmpire(bare, Shipped, Player, null, 10.0));
    }

    // ================================================================== persistence and determinism

    [Fact]
    public void ASnapshotMidRun_ResumesTheSplit_PerTurnHashesEqualTheUninterruptedRun()
    {
        OrderLog log = Log(Split(0, 30), Roads(1), Roads(2), Split(2, 70), Roads(3));
        TurnExecutor exec = BuildAndRoads(Shipped, log);
        WorldState w = Rig(timber: 400);
        var hashes = new List<string>();
        WorldState cur = w;
        for (int t = 0; t < 4; t++) { cur = exec.Step(cur); hashes.Add(WorldHash.ComputeHex(cur)); }

        WorldState at1 = exec.Step(w);
        Assert.Equal(new ResourceAllocationRow(Player, 30), Assert.Single(at1.ResourceAllocations.Rows()));
        using var ms = new MemoryStream();
        Snapshot.Save(at1, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, w.Terrain);
        Assert.Equal(WorldHash.ComputeHex(at1), WorldHash.ComputeHex(loaded));
        TurnExecutor fresh = BuildAndRoads(Shipped, log);
        cur = loaded;
        for (int t = 1; t < 4; t++) { cur = fresh.Step(cur); Assert.Equal(hashes[t], WorldHash.ComputeHex(cur)); }
        Assert.Equal(new ResourceAllocationRow(Player, 70), Assert.Single(cur.ResourceAllocations.Rows()));
    }

    [Fact]
    public void ASaveWithAnOutOfRangeOrDuplicatedSplit_IsRefusedAtLoad_AndTheStepRefusesToProduceOne()
    {
        WorldState w = Rig(heads: false);
        WorldState RoundTrip(params ResourceAllocationRow[] rows)
        {
            WorldState c = w.Clone();
            foreach (ResourceAllocationRow r in rows) c.ResourceAllocations.Add(r);
            using var ms = new MemoryStream();
            Snapshot.Save(c, ms);
            ms.Position = 0;
            return Snapshot.Load(ms, w.Terrain);
        }
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(RoundTrip(new ResourceAllocationRow(Player, 0)).ResourceAllocations.Rows()));
        Assert.Equal(2, RoundTrip(new ResourceAllocationRow(Player, 100), new ResourceAllocationRow(Rival, 40)).ResourceAllocations.Count);
        Assert.Contains("the building/roads split of polity 1 is 101, outside [0, 100]",
            Assert.Throws<SnapshotFormatException>(() => RoundTrip(new ResourceAllocationRow(Player, 101))).Message);
        Assert.Contains("is -1, outside [0, 100]",
            Assert.Throws<SnapshotFormatException>(() => RoundTrip(new ResourceAllocationRow(Player, -1))).Message);
        Assert.Contains("two building/roads split rows for polity 1",
            Assert.Throws<SnapshotFormatException>(() => RoundTrip(new ResourceAllocationRow(Player, 30), new ResourceAllocationRow(Player, 30))).Message);

        // A state no rule produces, written into the step's own table, is a bug the step names (it never runs on).
        WorldState bad = w.Clone();
        bad.ResourceAllocations.Add(new ResourceAllocationRow(Player, 30));
        bad.ResourceAllocations.Add(new ResourceAllocationRow(Player, 60));
        Assert.Contains("two building/roads split rows", Assert.Throws<InvalidOperationException>(() => BuildAndRoads(Shipped, null).Step(bad)).Message);
    }

    [Fact]
    public void TheSameWorldAndOrders_ReplayToTheSameHash()
    {
        OrderLog log = Log(Split(0, 30), Roads(0), Split(1, 0), Roads(1));
        string a = WorldHash.ComputeHex(BuildAndRoads(Shipped, log).Run(Rig(timber: 250), 3));
        string b = WorldHash.ComputeHex(BuildAndRoads(Shipped, log).Run(Rig(timber: 250), 3));
        Assert.Equal(a, b);
    }

    // ================================================================== the switch matrix

    [Fact]
    public void TheShippedContent_SwitchesTheSplitOn_WithIncrementalConstructionAndTheEmpirePurse()
    {
        Assert.True(Shipped.Construction is { AllocationRatios: true, Incremental: true, EmpireStockpile: true });
        Assert.True(ResourceAllocation.Enabled(Shipped));
        Assert.False(ResourceAllocation.Enabled(Off));
        Assert.Equal(Shipped.Construction with { AllocationRatios = false }, Off.Construction);   // the twin changes the switch alone
        // Every older twin strips it (the R4 rule), and so does the WS1 twin (the loader refuses the split without it).
        foreach (SimConfig twin in new[]
                 {
                     TestConfigs.PreIncrementalConstruction(Shipped), TestConfigs.PreD054Sim(Shipped), TestConfigs.PreD053(Shipped),
                     TestConfigs.PreForager(Shipped), TestConfigs.PreTaxAge(Shipped), TestConfigs.PreAiFounding(Shipped),
                 })
            Assert.False(ResourceAllocation.Enabled(twin));
        // Built in code without the purse or incremental construction, the switch is inert.
        Assert.False(ResourceAllocation.Enabled(TestConfigs.PreEmpireConstructionStockpile(Shipped)));
        Assert.False(ResourceAllocation.Enabled(Shipped with { Construction = Shipped.Construction! with { Incremental = false } }));
    }

    [Theory]
    [InlineData(false, true, "incremental false")]
    [InlineData(true, false, "empireStockpile false")]
    [InlineData(false, false, "incremental false")]
    public void TheLoader_RefusesTheSplitWithoutIncrementalConstructionAndTheEmpirePurse(bool incremental, bool purse, string fragment)
    {
        string json;
        using (var reader = new StreamReader(Sim.Data.DataFiles.OpenSim())) json = reader.ReadToEnd();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        doc["construction"]!["incremental"] = incremental;
        doc["construction"]!["empireStockpile"] = purse;
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("construction.allocationRatios requires construction.incremental and construction.empireStockpile", e.Message);
        Assert.Contains(fragment, e.Message);

        doc["construction"]!["allocationRatios"] = false;                        // without the split, the same block loads
        // D-055 (2026-10-16) CAUSE BLOCK: the shipped block now also carries reservations true, which the loader refuses
        // without incremental (SimConfigLoader, D-055) — so the "same block loads" arm switches it off as well. MEASURED
        // before this line was added: the incremental-false rows threw "construction.reservations requires
        // construction.incremental to be true (D-055) …" here. The split's own refusal above is unchanged.
        doc["construction"]!["reservations"] = false;
        Assert.False(SimConfigLoader.Load(doc.ToJsonString()).Construction!.AllocationRatios);
        doc["construction"]!["incremental"] = true;
        doc["construction"]!["empireStockpile"] = true;
        doc["construction"]!["allocationRatios"] = true;
        Assert.True(SimConfigLoader.Load(doc.ToJsonString()).Construction!.AllocationRatios);
    }
}
