using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

internal static class TableRows
{
    public static T[] Rows<T>(this IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }
}

/// <summary>
/// ADR-032 — the transport / inter-city road foundation, against the Director's FINAL transport
/// rulings (1–34): in-place, proportional modernization; the issuing civilization pays; road class
/// shapes authoritative pathfinding now. Test numbers in comments are ruling 30's list. Most tests run ONLY RoadDevelopmentSystem on a hand-built world whose every
/// input is stated: settlements, the cached pairwise baseline (SettlementDistances, in km because a
/// terrain-less world converts one cost unit to one km), controls, realised trade (the usage proxy),
/// completed research (the gate) and stocks (the cost). The baseline, replay and AI tests run the
/// founded production pipeline.
/// </summary>
public class RoadDevelopmentTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly RoadsConfig Roads = Cfg.Roads!;
    private static readonly int Stone = Cfg.Goods!.IdOf("stone");
    private static readonly int Timber = Cfg.Goods!.IdOf("timber");
    private static readonly int Tools = Cfg.Goods!.IdOf("tools");
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static int KeyOf(string id) => Research.Nodes[Research.IndexOfId(id)].Key.Value;

    /// <summary>
    /// A toy world: settlements 0..controllers.Length-1 (controller -1 = UNRULED, no Controls row),
    /// polities 1 (player) and 2 (AI), a symmetric baseline km per listed pair, and
    /// <paramref name="stock"/> stone and timber at every settlement.
    /// </summary>
    private static WorldState Toy(int[] controllers, (int A, int B, double Km)[] pairs, long stock = 100_000)
    {
        var w = new WorldState(32);
        w.Polities.Add(new PolityRow(Player, CommandSource.Player));
        w.Polities.Add(new PolityRow(Rival, CommandSource.Ai));
        for (int s = 0; s < controllers.Length; s++)
        {
            var id = new SettlementId(s);
            w.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            if (controllers[s] >= 0) w.Controls.Add(new ControlRow(new PolityId(controllers[s]), id, 1.0));
            if (stock > 0)
            {
                ResearchRigs.Stock(w, s, Stone, stock);
                ResearchRigs.Stock(w, s, Timber, stock);
                ResearchRigs.Stock(w, s, Tools, stock);
            }
        }
        foreach ((int a, int b, double km) in pairs)
        {
            w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(a), new SettlementId(b), km));
            w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(b), new SettlementId(a), km));
        }
        return w;
    }

    private static WorldState Trade(WorldState w, int from, int to, long qty)
    {
        w.TradeFlows.Add(new TradeFlowRow(new SettlementId(from), new SettlementId(to), new GoodId(Stone), qty));
        return w;
    }

    private static WorldState Know(WorldState w, PolityId polity, params string[] nodes)
    {
        foreach (string n in nodes) w.ResearchCompleted.Add(new ResearchCompletedRow(polity, new ResearchNodeId(KeyOf(n))));
        return w;
    }

    private static TurnExecutor RoadsOnly(OrderLog? orders) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.RoadDevelopment(Cfg)], orders);

    private static OrderLog Log(params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        return log;
    }

    private static OrderRecord Develop(long turn, PolityId polity, double pct) =>
        OrderRecord.From(turn, polity, OrderKind.DevelopRoads, 0, pct);

    private static WorldState StepOnce(WorldState w, params OrderRecord[] orders) => RoadsOnly(Log(orders)).Step(w);

    private static long StockOf(IReadOnlyWorldState w, int settlement, int good)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, new SettlementId(settlement), new GoodId(good));
        return i < 0 ? 0 : w.GoodStocks[i].Amount.Value;
    }

    private static (int A, int B)[] Pairs(IReadOnlyWorldState w)
    {
        var r = new (int, int)[w.TransportEdges.Count];
        for (int i = 0; i < r.Length; i++) r[i] = (w.TransportEdges[i].A.Value, w.TransportEdges[i].B.Value);
        return r;
    }

    private static readonly SettlementId S0 = new(0), S1 = new(1), S2 = new(2), S3 = new(3);

    /// <summary>A route row as the system would write it: class <paramref name="cls"/>, modernized
    /// <paramref name="fraction"/> toward <paramref name="target"/> (performance via RoadPerformance).</summary>
    private static TransportEdgeRow Route(int id, int a, int b, int cls, double km, int target = 0, double fraction = 0.0)
    {
        if (target == 0) target = cls;
        return new TransportEdgeRow(id, new SettlementId(a), new SettlementId(b), cls, TransportModes.Road, TransportEdgeStates.Complete,
            RoadPerformance.EffectiveCapacity(Roads, cls, target, fraction), km, 0, 1, 1, target, fraction,
            RoadPerformance.EffectiveCostFactor(Roads, cls, target, fraction));
    }

    private static RouteStatus Rank(int a, int b, long usage) => new(new SettlementId(a), new SettlementId(b), 1, usage, -1,
        EdgeTypes.DirtPath, EdgeTypes.DirtPath, 0, EdgeTypes.Trackway, 0, RoadDevelopmentKind.FromBaseline, RouteIneligibility.None);

    // ------------------------------------------------------------------ ROAD MODEL (1–6)

    [Fact]
    public void T01_BaselinePaths_RemainFree_NoOrderNoRoad_TheDirtClassCostsNothing_AndIsNeverAResearchUnlock()
    {
        RoadClassConfig dirt = Roads.ClassOf(EdgeTypes.DirtPath)!;
        Assert.Null(dirt.Entity);
        Assert.Empty(dirt.MaterialsPerKm);

        // 36: the founded production pipeline, no orders: PathBuild still lays its free dirt paths
        // (NetworkEdges, all DirtPath) and the road system builds nothing and spends nothing.
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var ex = new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.DevWorldgen())));
        for (int t = 0; t < 40; t++) w = ex.Step(w);
        Assert.True(w.NetworkEdges.Count > 0, "the free baseline built no dirt path");
        for (int i = 0; i < w.NetworkEdges.Count; i++) Assert.Equal(EdgeTypes.DirtPath, w.NetworkEdges[i].EdgeType);
        Assert.Equal(0, w.TransportEdges.Count);
        Assert.Equal(0, w.RoadDevelopments.Count);
        Assert.Equal(w.NetworkMeta[0].Revision, RoadPerformance.NetworkRevision(w));   // no road: the revision is PathBuild's alone

        // A DevelopRoads order never builds DirtPath and never charges for it, even with nothing researched.
        WorldState toy = Trade(Toy([1, 1], [(0, 1, 100.0)]), 0, 1, 10);
        WorldState after = StepOnce(toy, Develop(0, Player, 100.0));
        Assert.Equal(0, after.TransportEdges.Count);
        Assert.Equal(StockOf(toy, 0, Stone), StockOf(after, 0, Stone));
        Assert.Equal(RouteIneligibility.NoKnownClass, RoadDevelopmentQuery.Routes(toy, Research, Roads, Player)[0].Ineligible);
    }

    [Theory]
    [InlineData(EdgeTypes.DirtPath, EdgeTypes.Trackway, "track_road")]          // 2
    [InlineData(EdgeTypes.Trackway, EdgeTypes.BuiltRoad, "stone_dry")]          // 3
    [InlineData(EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, "road_paved")]        // 4
    [InlineData(EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, "macadam")]         // 5
    [InlineData(EdgeTypes.MacadamRoad, EdgeTypes.Highway, "motor_road")]        // 6
    public void T02to06_EachRoadClass_IsDistinctFromAndStrictlyBetterThanTheOneBelow(int lower, int higher, string unlockingNode)
    {
        Assert.NotEqual(lower, higher);
        RoadClassConfig lo = Roads.ClassOf(lower)!, hi = Roads.ClassOf(higher)!;
        Assert.NotEqual(lo.Entity, hi.Entity);
        Assert.True(hi.SpeedFactor < lo.SpeedFactor, "a better class must be faster");
        Assert.True(hi.CapacityTonnesPerYear > lo.CapacityTonnesPerYear);
        Assert.True(RoadPerformance.Speed(Roads, higher) > RoadPerformance.Speed(Roads, lower));
        // The tier map is exactly the ratified one: PATH {Dirt, Trackway}, ROAD {Built, Paved, Macadam}, HIGHWAY {Highway}.
        int[] tiers = [0, RoadTiers.Path, RoadTiers.Path, RoadTiers.Road, RoadTiers.Road, RoadTiers.Road, RoadTiers.Highway];
        Assert.Equal(tiers[higher], EdgeTypes.TierOf(higher));
        Assert.Equal(EdgeTypes.MotorRoad, EdgeTypes.Highway);
        // The research step that separates them: without it the polity's best class is below `higher`.
        string[] ladder = ["track_road", "stone_dry", "road_paved", "macadam", "motor_road"];
        WorldState w = Toy([1, 1], [(0, 1, 10.0)]);
        foreach (string n in ladder)
        {
            if (n == unlockingNode) break;
            Know(w, Player, n);
        }
        Assert.Equal(lower, RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Player));
        Know(w, Player, unlockingNode);
        Assert.Equal(higher, RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Player));
    }

    [Fact]
    public void Config_RejectsANonMonotoneSpeedLadder()
    {
        string json = TestConfigs.SimJson().Replace("\"speedFactor\": 0.45", "\"speedFactor\": 0.6", StringComparison.Ordinal);
        Assert.NotEqual(TestConfigs.SimJson(), json);
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    // ------------------------------------------------------------------ UPGRADE MODEL (7–14, 27, 30)

    [Fact]
    public void T07_08_12_13_27_30_UpgradingModernizesTheSameRouteInPlace_NeverASecondRoad_RepeatedOrdersContinueIt()
    {
        WorldState w = Trade(Know(Toy([1, 1], [(0, 1, 100.0)]), Player, "track_road"), 0, 1, 5);
        WorldState t1 = StepOnce(w, Develop(0, Player, 100.0));
        TransportEdgeRow first = Assert.Single(t1.TransportEdges.Rows());
        Assert.Equal((EdgeTypes.Trackway, EdgeTypes.Trackway, 0.0), (first.EdgeType, first.TargetClass, first.Modernization));
        Assert.Equal(RoadDevelopmentSystem.KindFromBaseline, t1.RoadDevelopments[^1].Kind);   // the baseline path's route row

        // A repeated order at the best known class changes nothing and duplicates nothing (27).
        WorldState again = StepOnce(t1, Develop(1, Player, 100.0));
        Assert.Equal(t1.TransportEdges.Rows(), again.TransportEdges.Rows());
        Assert.Equal(t1.RoadDevelopments.Count, again.RoadDevelopments.Count);

        // Path tier → road tier → highway tier: ONE row throughout, the same id (7, 8, 13, 30).
        int[] classes = [EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, EdgeTypes.Highway];
        string[][] research = [["stone_dry"], ["road_paved"], ["macadam"], ["motor_road"]];
        WorldState cur = t1;
        for (int k = 0; k < classes.Length; k++)
        {
            Know(cur, Player, research[k]);
            int before = cur.TransportEdges[0].EdgeType;
            cur = StepOnce(cur, Develop(cur.Clock.Turn, Player, 100.0));
            TransportEdgeRow e = Assert.Single(cur.TransportEdges.Rows());
            Assert.Equal(first.Id, e.Id);
            Assert.Equal(first.BuiltTurn, e.BuiltTurn);
            Assert.Equal(first.LengthKm, e.LengthKm);
            Assert.Equal(classes[k], e.EdgeType);
            Assert.Equal(Roads.ClassOf(classes[k])!.SpeedFactor, e.CostFactor);
            RoadDevelopmentRow log = cur.RoadDevelopments[^1];
            Assert.Equal((first.Id, before, classes[k], RoadDevelopmentSystem.KindModernize), (log.Edge, log.FromClass, log.ToClass, log.Kind));
            Assert.Single(TransportQuery.EdgesBetween(cur, S1, S0));   // the old class is not retained as a second road (13)
        }
    }

    [Fact]
    public void T09_10_11_EffectivePerformance_InterpolatesFromOldToTarget()
    {
        int from = EdgeTypes.PavedRoad, to = EdgeTypes.Highway;
        double s0 = RoadPerformance.Speed(Roads, from), s1 = RoadPerformance.Speed(Roads, to);
        // 0% → exactly the old road; 100% → exactly the target; 50% → the speed midpoint.
        Assert.Equal(Roads.ClassOf(from)!.SpeedFactor, RoadPerformance.EffectiveCostFactor(Roads, from, to, 0.0));
        Assert.Equal(Roads.ClassOf(to)!.SpeedFactor, RoadPerformance.EffectiveCostFactor(Roads, from, to, 1.0));
        Assert.Equal(s0 + 0.5 * (s1 - s0), RoadPerformance.EffectiveSpeed(Roads, from, to, 0.5));
        Assert.Equal(1.0 / (s0 + 0.5 * (s1 - s0)), RoadPerformance.EffectiveCostFactor(Roads, from, to, 0.5));
        Assert.Equal(Roads.ClassOf(from)!.CapacityTonnesPerYear, RoadPerformance.EffectiveCapacity(Roads, from, to, 0.0));
        Assert.Equal(Roads.ClassOf(to)!.CapacityTonnesPerYear, RoadPerformance.EffectiveCapacity(Roads, from, to, 1.0));
        long c0 = Roads.ClassOf(from)!.CapacityTonnesPerYear, c1 = Roads.ClassOf(to)!.CapacityTonnesPerYear;
        Assert.Equal(c0 + (c1 - c0) / 2, RoadPerformance.EffectiveCapacity(Roads, from, to, 0.5));
        // Monotone in the fraction.
        double prev = double.MaxValue;
        for (int i = 0; i <= 10; i++)
        {
            double f = RoadPerformance.EffectiveCostFactor(Roads, from, to, i / 10.0);
            Assert.True(f < prev);
            prev = f;
        }

        // The SYSTEM writes exactly these values: a 50%-affordable modernization of a 100 km
        // PavedRoad toward Highway leaves the route at 0.5, cost factor = the midpoint value.
        WorldState w = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road", "stone_dry", "road_paved", "macadam", "motor_road");
        w.TransportEdges.Add(Route(1, 0, 1, from, 100.0));
        ResearchRigs.Stock(w, 0, Stone, 400);       // (12 − 4) × 100 = 800 needed: half
        ResearchRigs.Stock(w, 0, Tools, 1_000);
        TransportEdgeRow e = Assert.Single(StepOnce(w, Develop(0, Player, 100.0)).TransportEdges.Rows());
        Assert.Equal((from, to, 0.5), (e.EdgeType, e.TargetClass, e.Modernization));
        Assert.Equal(RoadPerformance.EffectiveCostFactor(Roads, from, to, 0.5), e.CostFactor);
        Assert.Equal(RoadPerformance.EffectiveCapacity(Roads, from, to, 0.5), e.CapacityTonnesPerYear);
    }

    [Fact]
    public void T14_26_PartialModernization_ConsumesProportionalResources_NeverOverspends_NoDebt()
    {
        // The Director's example in shape: 100 km MacadamRoad → Highway costs (12 − 5) = 7 stone/km
        // and 1 tools/km — 700 stone + 100 tools. Stone for exactly half: 350 → 50%, 350 stone and
        // 50 tools, nothing more.
        WorldState w = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road", "stone_dry", "road_paved", "macadam", "motor_road");
        w.TransportEdges.Add(Route(1, 0, 1, EdgeTypes.MacadamRoad, 100.0));
        ResearchRigs.Stock(w, 0, Stone, 350);
        ResearchRigs.Stock(w, 0, Tools, 1_000);
        WorldState half = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Equal(0, StockOf(half, 0, Stone));
        Assert.Equal(1_000 - 50, StockOf(half, 0, Tools));
        TransportEdgeRow e = Assert.Single(half.TransportEdges.Rows());
        Assert.Equal(0.5, e.Modernization);
        RoadDevelopmentRow log = half.RoadDevelopments[^1];
        Assert.Equal((400L, 0.0, 0.5), (log.MaterialUnits, log.ProgressBefore, log.ProgressAfter));

        // Continuing the SAME route: the remaining half costs the remaining half (350 + 50), and
        // completes it — same id, no second road.
        ResearchRigs.Stock(half, 1, Stone, 10_000);
        WorldState done = StepOnce(half, Develop(1, Player, 100.0));
        TransportEdgeRow f = Assert.Single(done.TransportEdges.Rows());
        Assert.Equal((e.Id, EdgeTypes.Highway, 0.0), (f.Id, f.EdgeType, f.Modernization));
        Assert.Equal(10_000 - 350, StockOf(done, 1, Stone));
        Assert.Equal(1_000 - 100, StockOf(done, 0, Tools));
        Assert.Equal((0.5, 1.0), (done.RoadDevelopments[^1].ProgressBefore, done.RoadDevelopments[^1].ProgressAfter));

        // From the baseline: 100 km trackway = 100 timber; 30 held → 30%, exactly 30 spent.
        WorldState t = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(t, 0, Timber, 30);
        WorldState part = StepOnce(t, Develop(0, Player, 100.0));
        Assert.Equal(0, StockOf(part, 0, Timber));
        Assert.Equal(0.3, Assert.Single(part.TransportEdges.Rows()).Modernization, 15);

        // Nothing held: nothing happens, no row, no flow, no negative stock.
        WorldState broke = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road");
        WorldState none = StepOnce(broke, Develop(0, Player, 100.0));
        Assert.Equal(0, none.TransportEdges.Count);
        Assert.Equal(0, none.RoadDevelopments.Count);
        Assert.Equal(-1, GoodStockIndex.IndexOf(none.GoodStocks, S0, new GoodId(Timber)));
    }

    [Fact]
    public void T26_InsufficientResources_ThePartiallyAffordedRouteEndsTheOrder_NothingFurtherDownIsTouched()
    {
        // Ranked: (0,1) 100 km, (0,2) 300 km, (0,3) 10 km. Trackway = 1 timber/km: 100, 300, 10.
        WorldState w = Know(Toy([1, 1, 1, 1], [(0, 1, 100.0), (0, 2, 300.0), (0, 3, 10.0)], stock: 0), Player, "track_road");
        Trade(w, 0, 1, 30); Trade(w, 0, 2, 20); Trade(w, 0, 3, 10);
        ResearchRigs.Stock(w, 0, Timber, 250);        // route 1 in full (100), route 2 to 150/300 = 50%
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Equal([(0, 1), (0, 2)], Pairs(after));  // route 3 is cheap but lies after the partial one
        Assert.Equal((EdgeTypes.Trackway, 0.0), (after.TransportEdges[0].EdgeType, after.TransportEdges[0].Modernization));
        Assert.Equal((EdgeTypes.DirtPath, EdgeTypes.Trackway, 0.5),
            (after.TransportEdges[1].EdgeType, after.TransportEdges[1].TargetClass, after.TransportEdges[1].Modernization));
        Assert.Equal(0, StockOf(after, 0, Timber));    // exactly 250 spent, never below zero
        Assert.Equal(250, after.RoadDevelopments.Rows().Sum(r => r.MaterialUnits));
    }

    [Fact]
    public void RaisingTheTarget_MidModernization_KeepsTheRoutesPerformance_AndTheSameRow()
    {
        WorldState w = Know(Toy([1, 1], [(0, 1, 100.0)]), Player, "track_road", "stone_dry", "road_paved", "macadam", "motor_road");
        w.TransportEdges.Add(Route(4, 0, 1, EdgeTypes.BuiltRoad, 100.0, EdgeTypes.PavedRoad, 0.4));
        RouteStatus r = Assert.Single(RoadDevelopmentQuery.Routes(w, Research, Roads, Player));
        Assert.Equal((4, EdgeTypes.Highway), (r.Edge, r.TargetClass));
        double speedNow = RoadPerformance.EffectiveSpeed(Roads, EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, 0.4);
        Assert.Equal(speedNow, RoadPerformance.EffectiveSpeed(Roads, EdgeTypes.BuiltRoad, EdgeTypes.Highway, r.StartFraction), 12);
        Assert.True(r.StartFraction < 0.4 && r.StartFraction > 0.0);
        // A civilization that does not know the row's target can neither continue nor lower it:
        // a Trackway part-way to PavedRoad, and a rival that knows only up to BuiltRoad.
        WorldState v = Know(Toy([2, 2], [(0, 1, 100.0)]), Rival, "track_road", "stone_dry");
        v.TransportEdges.Add(Route(4, 0, 1, EdgeTypes.Trackway, 100.0, EdgeTypes.PavedRoad, 0.4));
        Assert.Equal(RouteIneligibility.TargetBeyondIssuerKnowledge, Assert.Single(RoadDevelopmentQuery.Routes(v, Research, Roads, Rival)).Ineligible);
        Assert.Equal(v.TransportEdges.Rows(), StepOnce(v, Develop(0, Rival, 100.0)).TransportEdges.Rows());
    }

    // ------------------------------------------------------------------ PATHFINDING (15–17, 37)

    private static (WorldState World, TraversalLattice Lattice, int From, int To, double BaselineCost) Founded()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, Cfg.Transport.RiverCostFactor);
        int a = LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[0].SiteCell);
        int b = LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[1].SiteCell);
        Pathfinder.PathResult baseline = Pathfinder.FindPath(lattice, w, a, b);
        Assert.True(baseline.Found);
        return (w, lattice, a, b, baseline.TotalCost);
    }

    [Fact]
    public void T15_BetterRoadClasses_ProduceLowerAuthoritativePathCosts()
    {
        (WorldState w, TraversalLattice lattice, int a, int b, double c0) = Founded();
        double km = c0 * LatticeGeometry.KmPerCostUnitOnIdealGround(lattice);
        double previous = double.MaxValue;
        foreach (int cls in EdgeTypes.RoadClasses)
        {
            WorldState r = w.Clone();
            r.TransportEdges.Add(Route(1, w.Settlements[0].Id.Value, w.Settlements[1].Id.Value, cls, km));
            double cost = Pathfinder.FindPath(lattice, r, a, b).TotalCost;
            Assert.True(cost < previous, $"class {cls} is not cheaper than the class below it");
            if (cls != EdgeTypes.DirtPath)
                Assert.Equal(RoadPerformance.TravelCostUnits(r.TransportEdges[0], LatticeGeometry.KmPerCostUnitOnIdealGround(lattice)), cost);
            previous = cost;
        }
        Assert.True(previous < c0);
    }

    [Fact]
    public void T16_PartialModernization_ChangesPathCostProportionally()
    {
        (WorldState w, TraversalLattice lattice, int a, int b, double c0) = Founded();
        double km = c0 * LatticeGeometry.KmPerCostUnitOnIdealGround(lattice);
        int sa = w.Settlements[0].Id.Value, sb = w.Settlements[1].Id.Value;
        double Cost(double f)
        {
            WorldState r = w.Clone();
            r.TransportEdges.Add(Route(1, sa, sb, EdgeTypes.Trackway, km, EdgeTypes.Highway, f));
            return Pathfinder.FindPath(lattice, r, a, b).TotalCost;
        }
        double at0 = Cost(0.0), at50 = Cost(0.5), at100 = Cost(1.0);
        Assert.True(at0 > at50 && at50 > at100);
        Assert.Equal(km * Roads.ClassOf(EdgeTypes.Trackway)!.SpeedFactor / LatticeGeometry.KmPerCostUnitOnIdealGround(lattice), at0, 9);
        Assert.Equal(km * Roads.ClassOf(EdgeTypes.Highway)!.SpeedFactor / LatticeGeometry.KmPerCostUnitOnIdealGround(lattice), at100, 9);
        // Halfway in SPEED: the path cost is the harmonic midpoint of the end costs.
        Assert.Equal(km * RoadPerformance.EffectiveCostFactor(Roads, EdgeTypes.Trackway, EdgeTypes.Highway, 0.5) / LatticeGeometry.KmPerCostUnitOnIdealGround(lattice), at50, 9);
        Assert.Equal(2.0 / (1.0 / at0 + 1.0 / at100), at50, 9);
    }

    [Fact]
    public void T17_37_PathfindingCatchmentsAndFreight_ShareOneRoadPerformance_AndRecomputeDeterministically()
    {
        (WorldState w, TraversalLattice lattice, int a, int b, double c0) = Founded();
        SettlementId x = w.Settlements[0].Id, y = w.Settlements[1].Id;
        var catchment = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Catchment(Cfg)], null);
        WorldState w1 = catchment.Step(w);
        double before = Distance(w1, x, y);
        Assert.Equal(c0, before);

        // A highway route row plus its log row (the revision the system's log moves).
        WorldState withRoad = w1.Clone();
        withRoad.TransportEdges.Add(Route(1, Math.Min(x.Value, y.Value), Math.Max(x.Value, y.Value), EdgeTypes.Highway, c0 * LatticeGeometry.KmPerCostUnitOnIdealGround(lattice)));
        withRoad.RoadDevelopments.Add(new RoadDevelopmentRow(0, Player, 1, x, y, EdgeTypes.DirtPath, EdgeTypes.Highway, 2, 0, 0, 0.0, 1.0));
        Assert.Equal(RoadPerformance.NetworkRevision(w1) + 1, RoadPerformance.NetworkRevision(withRoad));
        WorldState w2 = catchment.Step(withRoad);
        Assert.Equal(withRoad.Clock.Turn, w2.CatchmentSummaries[0].LastRecomputeTurn);   // the road was a recompute event
        double lane = RoadPerformance.TravelCostUnits(withRoad.TransportEdges[0], LatticeGeometry.KmPerCostUnitOnIdealGround(lattice));
        Assert.Equal(lane, Distance(w2, x, y));                                           // the catchment's pairwise cost IS the lane
        Assert.Equal(Pathfinder.FindPath(lattice, withRoad, a, b).TotalCost, Distance(w2, x, y));

        // Freight: the same stored performance — the road hop costs TravelKm, and the pairwise
        // baseline (now the lane) agrees with it.
        TransportQuery.FreightEstimate est = TransportQuery.EstimateFreight(w2, Roads, x, y, 1000);
        Assert.Equal(RoadPerformance.TravelKm(withRoad.TransportEdges[0]), est.CostKm, 9);
        Assert.Equal(lane * TransportQuery.KmPerCostUnit(w2), est.CostKm, 9);

        // Determinism: the same inputs recompute bit-identically, and with no road event the
        // catchment does not recompute at all.
        WorldState w2b = catchment.Step(withRoad.Clone());
        Assert.Equal(WorldHash.ComputeHex(w2), WorldHash.ComputeHex(w2b));
        WorldState w3 = catchment.Step(w2);
        Assert.Equal(w2.CatchmentSummaries[0].LastRecomputeTurn, w3.CatchmentSummaries[0].LastRecomputeTurn);
    }

    private static double Distance(IReadOnlyWorldState w, SettlementId from, SettlementId to)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            if (w.SettlementDistances[i].From == from && w.SettlementDistances[i].To == to) return w.SettlementDistances[i].TravelCost;
        throw new InvalidOperationException("no distance row");
    }

    // ------------------------------------------------------------------ PAYMENT (18–21)

    [Fact]
    public void T18_19_TheIssuingCivilizationPays_FromItsSettlementsInAscendingId_NotFromTheEndpoint()
    {
        // The player controls 0, 1, 2; the route is (1, 2); the endpoints hold NOTHING, settlement
        // 0 (not an endpoint) holds 30 timber and 3 holds the rest — no, 3 is the rival's: untouched.
        WorldState w = Know(Toy([1, 1, 1, 2], [(1, 2, 50.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(w, 0, Timber, 30);
        ResearchRigs.Stock(w, 2, Timber, 0);
        ResearchRigs.Stock(w, 1, Timber, 100);
        ResearchRigs.Stock(w, 3, Timber, 1_000);
        Assert.Equal([0, 1, 2], RoadDevelopmentQuery.PayingSettlements(w, Player).Select(s => s.Value));
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Single(after.TransportEdges.Rows());
        Assert.Equal(0, StockOf(after, 0, Timber));          // drawn first: lowest id of the CIVILIZATION
        Assert.Equal(100 - 20, StockOf(after, 1, Timber));   // then the next
        Assert.Equal(1_000, StockOf(after, 3, Timber));      // never another civilization's goods
        Assert.Equal(50, after.RoadDevelopments[0].MaterialUnits);
    }

    [Fact]
    public void T20_21_RoadsIntoForeignOrUnclaimedTerritory_ChargeTheIssuerOnly_AndAreUnowned()
    {
        // 0 player, 1 rival, 2 UNRULED, 3 player (a non-endpoint purse). The rival and the
        // unruled settlement hold plenty; the player's goods sit only at 3.
        WorldState w = Know(Toy([1, 2, -1, 1], [(0, 1, 50.0), (0, 2, 60.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(w, 1, Timber, 1_000);
        ResearchRigs.Stock(w, 2, Timber, 1_000);
        ResearchRigs.Stock(w, 3, Timber, 500);
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Equal([(0, 1), (0, 2)], Pairs(after));
        Assert.Equal(500 - 110, StockOf(after, 3, Timber));
        Assert.Equal(1_000, StockOf(after, 1, Timber));
        Assert.Equal(1_000, StockOf(after, 2, Timber));
        Assert.All(after.RoadDevelopments.Rows(), r => Assert.Equal(Player, r.Polity));
        // Unowned: the rival routes freight over the road the player paid for.
        TransportQuery.FreightEstimate viaRoad = TransportQuery.EstimateFreight(after, Roads, S1, S0, 100);
        Assert.NotEqual(-1, viaRoad.Links[0]);
        // A rival-only pair is not the player's to develop, but the rival may develop it.
        WorldState w2 = Know(Toy([1, 2, -1], [(1, 2, 40.0)]), Player, "track_road");
        Assert.Empty(RoadDevelopmentQuery.Routes(w2, Research, Roads, Player));
        Know(w2, Rival, "track_road");
        Assert.Single(StepOnce(w2, Develop(0, Rival, 100.0)).TransportEdges.Rows());
        // A civilization whose purse is empty builds nothing, even though the far endpoint is rich.
        WorldState poor = Know(Toy([1, 2], [(0, 1, 50.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(poor, 1, Timber, 1_000);
        Assert.Equal(0, StepOnce(poor, Develop(0, Player, 100.0)).TransportEdges.Count);
    }

    // ------------------------------------------------------------------ DEVELOPMENT (22–25)

    [Fact]
    public void T22_24_HighestUseRoutesFirst_AndThePercentageOfDemandIsRespected()
    {
        // Usages: (0,1) 50, (0,2) 30, (0,3) 20 — total 100.
        WorldState w = Know(Toy([1, 1, 1, 1], [(0, 1, 10.0), (0, 2, 10.0), (0, 3, 10.0)]), Player, "track_road");
        Trade(w, 0, 1, 30); Trade(w, 1, 0, 20);              // both directions count
        Trade(w, 2, 0, 30);
        Trade(w, 0, 3, 20);
        RouteStatus[] ranked = RoadDevelopmentQuery.Ranked(RoadDevelopmentQuery.Routes(w, Research, Roads, Player));
        Assert.Equal([50L, 30L, 20L], ranked.Select(r => r.Usage));

        int[] Built(double pct) => StepOnce(w.Clone(), Develop(0, Player, pct)).TransportEdges.Rows().Select(e => e.B.Value).ToArray();
        Assert.Equal([1], Built(10.0));
        Assert.Equal([1], Built(50.0));
        Assert.Equal([1, 2], Built(50.0001));
        Assert.Equal([1, 2], Built(80.0));
        Assert.Equal([1, 2, 3], Built(80.5));
        Assert.Equal([1, 2, 3], Built(100.0));
        Assert.Equal(Built(37.0), Built(37.0));              // the same input, the same coverage
    }

    [Fact]
    public void T25_WithNoTradeYet_ThePercentageIsOfTheRouteCount_RoundedUp_InIdOrder()
    {
        WorldState w = Know(Toy([1, 1, 1, 1, 1], [(0, 1, 10.0), (0, 2, 10.0), (0, 3, 10.0), (0, 4, 10.0)]), Player, "track_road");
        int Count(double pct) => StepOnce(w.Clone(), Develop(0, Player, pct)).TransportEdges.Count;
        Assert.Equal(1, Count(1.0));
        Assert.Equal(1, Count(25.0));
        Assert.Equal(2, Count(25.5));
        Assert.Equal(2, Count(50.0));
        Assert.Equal(4, Count(100.0));
        Assert.Equal([1, 2], StepOnce(w.Clone(), Develop(0, Player, 50.0)).TransportEdges.Rows().Select(e => e.B.Value));
    }

    [Fact]
    public void T23_Ties_AreBrokenByStableEndpointIds_TieDense_AndInsensitiveToRowOrder()
    {
        var pairs = new List<(int, int, double)>();
        int[] scramble = [7, 2, 11, 4, 9, 1, 12, 6, 3, 10, 5, 8];
        foreach (int b in scramble) pairs.Add((0, b, 10.0));
        WorldState w = Know(Toy(Enumerable.Repeat(1, 13).ToArray(), [.. pairs]), Player, "track_road");
        foreach (int b in scramble) Trade(w, b, 0, 4);
        RouteStatus[] ranked = RoadDevelopmentQuery.Ranked(RoadDevelopmentQuery.Routes(w, Research, Roads, Player));
        Assert.Equal(Enumerable.Range(1, 12), ranked.Select(r => r.B.Value));
        Assert.Equal([1, 2, 3], StepOnce(w, Develop(0, Player, 25.0)).TransportEdges.Rows().Select(e => e.B.Value));

        RouteStatus x = Rank(2, 3, 5), y = Rank(1, 9, 5), z = Rank(1, 4, 5), hi = Rank(9, 10, 6);
        Assert.Equal([hi, z, y, x], RoadDevelopmentQuery.Ranked([x, y, z, hi]));
        Assert.Equal([hi, z, y, x], RoadDevelopmentQuery.Ranked([z, hi, x, y]));
    }

    [Fact]
    public void OneActionPerEmpirePerTurn_AndASecondEmpireSeesTheFirstsModernizationInTheSameStep()
    {
        WorldState w = Know(Know(Toy([1, 2], [(0, 1, 10.0)]), Player, "track_road"), Rival, "track_road");
        WorldState after = StepOnce(w, Develop(0, Player, 100.0), Develop(0, Player, 100.0), Develop(0, Rival, 100.0));
        TransportEdgeRow e = Assert.Single(after.TransportEdges.Rows());
        Assert.Equal(Player, after.RoadDevelopments[0].Polity);
        Assert.Single(after.RoadDevelopments.Rows());
        Assert.Equal(EdgeTypes.Trackway, e.EdgeType);
    }

    // ------------------------------------------------------------------ TOPOLOGY (28, 29)

    [Fact]
    public void T28_Junctions_AreDerivedFromTopology_DegreeEdgesNeighboursModes()
    {
        var w = Toy([1, 1, 1, 1, 1], []);
        w.TransportEdges.Add(Route(1, 0, 1, EdgeTypes.Trackway, 10.0));
        w.TransportEdges.Add(Route(2, 0, 2, EdgeTypes.PavedRoad, 10.0));
        w.TransportEdges.Add(Route(3, 1, 2, EdgeTypes.Trackway, 10.0));
        w.TransportEdges.Add(Route(4, 0, 3, EdgeTypes.BuiltRoad, 10.0));
        w.TransportEdges.Add(Route(5, 0, 1, EdgeTypes.Highway, 10.0));   // a genuinely separate second route 0-1

        Assert.Equal(4, TransportQuery.Degree(w, S0));
        Assert.True(TransportQuery.IsJunction(w, S0));
        Assert.Equal(3, TransportQuery.Degree(w, S1));
        Assert.True(TransportQuery.IsJunction(w, S1));
        Assert.False(TransportQuery.IsJunction(w, S2));
        Assert.False(TransportQuery.IsJunction(w, S3));
        Assert.False(TransportQuery.IsJunction(w, new SettlementId(4)));
        Assert.Equal([0, 1], TransportQuery.Junctions(w).Select(s => s.Value));
        Assert.Equal([1, 2, 3], TransportQuery.Neighbors(w, S0).Select(s => s.Value));
        Assert.Equal([1, 2, 4, 5], TransportQuery.EdgesAt(w, S0).Select(e => e.Id));
        Assert.Equal([TransportModes.Road], TransportQuery.ModesAt(w, S0));
        Assert.Empty(TransportQuery.ModesAt(w, new SettlementId(4)));
        Assert.Equal([2], TransportQuery.SharedNeighbors(w, S0, S1).Select(s => s.Value));
        Assert.Equal(0, w.NetworkNodes.Count);   // no junction entity anywhere
    }

    [Fact]
    public void T29_SeparatePhysicalRoutesCoexist_ModernizationTouchesOnlyTheLowestIdRouteBelowTarget()
    {
        WorldState w = Know(Toy([1, 1], [(0, 1, 100.0)]), Player, "track_road", "stone_dry");
        w.TransportEdges.Add(Route(3, 0, 1, EdgeTypes.Trackway, 80.0));    // a separate, shorter alignment
        w.TransportEdges.Add(Route(9, 0, 1, EdgeTypes.Trackway, 120.0));
        WorldState a = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Equal(2, a.TransportEdges.Count);
        Assert.Equal((3, EdgeTypes.BuiltRoad), (a.TransportEdges[0].Id, a.TransportEdges[0].EdgeType));
        Assert.Equal(w.TransportEdges[1], a.TransportEdges[1]);            // the other route untouched
        WorldState b = StepOnce(a, Develop(1, Player, 100.0));
        Assert.Equal(2, b.TransportEdges.Count);                           // continues with route 9, never a third
        Assert.Equal((9, EdgeTypes.BuiltRoad), (b.TransportEdges[1].Id, b.TransportEdges[1].EdgeType));
        Assert.Equal(120.0, b.TransportEdges[1].LengthKm);
    }

    // ------------------------------------------------------------------ RESEARCH (31–33)

    [Fact]
    public void T31_Research_GatesEligibility_ClassByClass()
    {
        WorldState w = Toy([1, 1], [(0, 1, 10.0)]);
        int Best() => RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Player);
        Assert.Equal(EdgeTypes.DirtPath, Best());
        Know(w, Player, "track_road");
        Assert.Equal(EdgeTypes.Trackway, Best());
        Know(w, Player, "stone_dry");
        Assert.Equal(EdgeTypes.BuiltRoad, Best());
        Know(w, Player, "road_paved");
        Assert.Equal(EdgeTypes.PavedRoad, Best());
        Know(w, Player, "macadam");
        Assert.Equal(EdgeTypes.MacadamRoad, Best());
        Assert.False(RoadDevelopmentQuery.IsClassKnown(w, Research, Roads, Player, EdgeTypes.Highway));
        Know(w, Player, "motor_road");
        Assert.Equal(EdgeTypes.Highway, Best());
        Assert.Equal(EdgeTypes.DirtPath, RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Rival));
    }

    [Fact]
    public void T32_ResearchCompletion_ConstructsNoRoad_OnlyAnOrderDoes()
    {
        WorldState w = Trade(Toy([1, 1], [(0, 1, 10.0)]), 0, 1, 3);
        Know(w, Player, "track_road");
        var ex = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Research(Cfg), SystemCatalog.RoadDevelopment(Cfg)], null);
        WorldState idle = ex.Run(w, 10);
        Assert.Equal(0, idle.TransportEdges.Count);
        Assert.True(RoadDevelopmentQuery.Routes(idle, Research, Roads, Player)[0].Eligible);
        WorldState ordered = StepOnce(idle, Develop(idle.Clock.Turn, Player, 100.0));
        Assert.Equal(1, ordered.TransportEdges.Count);
    }

    // ------------------------------------------------------------------ the action, turn-exact

    [Fact]
    public void PlayerAction_ModernizesRoads_TurnExact_TheEffectFirstExistsInTheStateAfterTheOrdersTurn()
    {
        WorldState w0 = Trade(Know(Toy([1, 1], [(0, 1, 25.0)]), Player, "track_road"), 0, 1, 1);
        var ex = RoadsOnly(Log(Develop(2, Player, 100.0)));
        WorldState w1 = ex.Step(w0), w2 = ex.Step(w1);
        Assert.Equal(0, w1.TransportEdges.Count);
        Assert.Equal(0, w2.TransportEdges.Count);
        WorldState w3 = ex.Step(w2);
        TransportEdgeRow e = Assert.Single(w3.TransportEdges.Rows());
        Assert.Equal(3L, w3.Clock.Turn);
        Assert.Equal((3L, 3L), (e.BuiltTurn, e.UpgradedTurn));
        Assert.Equal(2L, w3.RoadDevelopments[0].Turn);
        Assert.Equal((EdgeTypes.Trackway, TransportModes.Road, TransportEdgeStates.Complete, 0),
            (e.EdgeType, e.Mode, e.State, e.Condition));
        Assert.Equal(Roads.ClassOf(EdgeTypes.Trackway)!.CapacityTonnesPerYear, e.CapacityTonnesPerYear);
        Assert.Equal(25.0, e.LengthKm);
        Assert.Equal(25, w3.RoadDevelopments[0].MaterialUnits);
        Assert.Equal(StockOf(w0, 0, Timber) - 25, StockOf(w3, 0, Timber));
        // The modernization is visible to the network revision in the same state (catchments recompute next step).
        Assert.Equal(RoadPerformance.NetworkRevision(w2) + 1, RoadPerformance.NetworkRevision(w3));
    }

    // ------------------------------------------------------------------ UI surface

    [Fact]
    public void Describe_ExposesEverythingTheRoadUiNeeds_ReadOnly()
    {
        WorldState w = Trade(Know(Toy([1, 1, 1], [(0, 1, 100.0), (0, 2, 40.0)]), Player, "track_road", "stone_dry"), 0, 1, 7);
        w.TransportEdges.Add(Route(5, 0, 1, EdgeTypes.Trackway, 100.0, EdgeTypes.BuiltRoad, 0.25));
        string hash = WorldHash.ComputeHex(w);
        RoadRouteView[] views = RoadDevelopmentQuery.Describe(w, Research, Roads, Cfg.Goods!, Player);
        Assert.Equal(hash, WorldHash.ComputeHex(w));
        Assert.Equal(2, views.Length);
        RoadRouteView v = views[0];
        Assert.Equal((0, 1, 5, EdgeTypes.Trackway, EdgeTypes.BuiltRoad, 7L, true),
            (v.Route.A.Value, v.Route.B.Value, v.Route.Edge, v.Route.CurrentClass, v.Route.TargetClass, v.Route.Usage, v.Route.Eligible));
        Assert.Equal(25.0, v.ModernizationPercent);
        Assert.Equal(w.TransportEdges[0].CostFactor, v.EffectiveCostFactor);
        Assert.Equal(w.TransportEdges[0].CapacityTonnesPerYear, v.CapacityTonnesPerYear);
        // Remaining 75% of (2 stone + 0.5 − 1 timber… floored at 0) per km over 100 km: 150 stone, 0 timber.
        Assert.Equal([(Stone, 150L)], v.EstimatedCost.Select(c => (c.Good.Value, c.Units)));
        RoadRouteView bare = views[1];
        Assert.Equal((-1, RoadDevelopmentKind.FromBaseline, 1.0), (bare.Route.Edge, bare.Route.Kind, bare.EffectiveCostFactor));
    }

    // ------------------------------------------------------------------ AI and player (ruling 25)

    [Fact]
    public void AiAndPlayer_InvokeTheSameOperation_SameOrderShape_SameSelection_SameResult()
    {
        WorldState forPlayer = Trade(Know(Toy([1, 1, 1], [(0, 1, 20.0), (0, 2, 30.0)]), Player, "track_road"), 0, 2, 8);
        WorldState forAi = Trade(Know(Toy([2, 2, 2], [(0, 1, 20.0), (0, 2, 30.0)]), Rival, "track_road"), 0, 2, 8);

        Assert.Null(RoadDevelopmentPolicy.Decide(forPlayer, Cfg, Player));
        OrderRecord ai = Assert.Single(RoadDevelopmentPolicy.OrdersForAi(forAi, Cfg));
        Assert.Equal(RoadDevelopmentQuery.DevelopOrder(forAi, Rival, RoadDevelopmentPolicy.TradedPercent), ai);
        OrderRecord player = RoadDevelopmentQuery.DevelopOrder(forPlayer, Player, ai.Amount);
        Assert.Equal((ai.Kind, ai.TargetId, ai.Amount), (player.Kind, player.TargetId, player.Amount));

        RoadDevelopmentStep[] pPlan = RoadDevelopmentQuery.Plan(forPlayer, Research, Roads, Cfg.Goods!, Player, player.Amount);
        RoadDevelopmentStep[] aPlan = RoadDevelopmentQuery.Plan(forAi, Research, Roads, Cfg.Goods!, Rival, ai.Amount);
        Assert.Equal(pPlan.Select(s => s.Route), aPlan.Select(s => s.Route));

        WorldState p1 = StepOnce(forPlayer, player), a1 = StepOnce(forAi, ai);
        Assert.Equal(p1.TransportEdges.Rows(), a1.TransportEdges.Rows());
        Assert.Equal([(0, 2)], Pairs(a1));
    }

    [Fact]
    public void AiPolicy_IssuesNothing_WhenNothingIsEligibleOrAffordable()
    {
        WorldState noResearch = Toy([2, 2], [(0, 1, 20.0)]);
        Assert.Empty(RoadDevelopmentPolicy.OrdersForAi(noResearch, Cfg));
        WorldState broke = Know(Toy([2, 2], [(0, 1, 20.0)], stock: 0), Rival, "track_road");
        Assert.Empty(RoadDevelopmentPolicy.OrdersForAi(broke, Cfg));
        WorldState able = Know(Toy([2, 2], [(0, 1, 20.0)]), Rival, "track_road");
        Assert.Equal(RoadDevelopmentPolicy.UntradedPercent, Assert.Single(RoadDevelopmentPolicy.OrdersForAi(able, Cfg)).Amount);
        // Partly affordable is enough to act (the system applies the affordable proportion).
        WorldState some = Know(Toy([2, 2], [(0, 1, 20.0)], stock: 0), Rival, "track_road");
        ResearchRigs.Stock(some, 1, Timber, 5);
        Assert.Single(RoadDevelopmentPolicy.OrdersForAi(some, Cfg));
    }

    // ------------------------------------------------------------------ deterioration (ruling 18)

    [Fact]
    public void Deterioration_HasNoGameplay_ConditionIsInert_AndNothingDecaysOverTime()
    {
        WorldState w = Trade(Know(Toy([1, 1, 1], [(0, 1, 20.0), (1, 2, 20.0), (0, 2, 80.0)]), Player, "track_road"), 0, 1, 2);
        WorldState built = StepOnce(w, Develop(0, Player, 100.0));
        WorldState aged = RoadsOnly(null).Run(built, 200);
        Assert.Equal(built.TransportEdges.Rows(), aged.TransportEdges.Rows());

        WorldState worn = built.Clone();
        for (int i = 0; i < worn.TransportEdges.Count; i++) worn.TransportEdges[i] = worn.TransportEdges[i] with { Condition = -987 };
        TransportQuery.FreightEstimate a = TransportQuery.EstimateFreight(built, Roads, S0, S2, 5000);
        TransportQuery.FreightEstimate b = TransportQuery.EstimateFreight(worn, Roads, S0, S2, 5000);
        Assert.Equal((a.CostKm, a.TotalDays, a.BottleneckCapacityTonnesPerYear), (b.CostKm, b.TotalDays, b.BottleneckCapacityTonnesPerYear));
        Know(built, Player, "stone_dry"); Know(worn, Player, "stone_dry");
        Assert.Equal(RoadDevelopmentQuery.Routes(built, Research, Roads, Player).Select(r => (r.A, r.B, r.Edge, r.Kind)),
                     RoadDevelopmentQuery.Routes(worn, Research, Roads, Player).Select(r => (r.A, r.B, r.Edge, r.Kind)));
        Assert.Equal(StepOnce(built, Develop(1, Player, 100.0)).RoadDevelopments.Rows(),
                     StepOnce(worn, Develop(1, Player, 100.0)).RoadDevelopments.Rows());
    }

    // ------------------------------------------------------------------ the travel-time hook

    [Fact]
    public void TravelTime_BetterAndFurtherModernizedRoadsAreFaster_AndTheDirectorsReferenceFigureHolds()
    {
        WorldState w = Toy([1, 1], [(0, 1, 3000.0)]);
        TransportQuery.FreightEstimate dirt = TransportQuery.EstimateFreight(w, Roads, S0, S1, 5000);
        Assert.True(dirt.Reachable);
        Assert.Equal(3000.0 / Roads.BaselineKmPerDay, dirt.TransitDays);
        Assert.Equal(5000.0 / Roads.ClassOf(EdgeTypes.DirtPath)!.CapacityTonnesPerYear * TransportQuery.DaysPerYear, dirt.ThroughputDays);
        Assert.InRange(dirt.TotalDays, 80.0, 100.0);
        Assert.Equal([-1], dirt.Links);

        double previous = dirt.TotalDays;
        foreach (int cls in new[] { EdgeTypes.Trackway, EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, EdgeTypes.Highway })
        {
            WorldState r = w.Clone();
            r.TransportEdges.Add(Route(1, 0, 1, cls, 3000.0));
            TransportQuery.FreightEstimate est = TransportQuery.EstimateFreight(r, Roads, S0, S1, 5000);
            Assert.True(est.TotalDays < previous, $"class {cls} is not faster than the class below it");
            Assert.Equal([1], est.Links);
            Assert.Equal(RoadPerformance.TravelKm(r.TransportEdges[0]), est.CostKm);
            previous = est.TotalDays;
        }
        // Partial modernization lies strictly between its end classes.
        double Days(double f)
        {
            WorldState r = w.Clone();
            r.TransportEdges.Add(Route(1, 0, 1, EdgeTypes.PavedRoad, 3000.0, EdgeTypes.Highway, f));
            return TransportQuery.EstimateFreight(r, Roads, S0, S1, 5000).TransitDays;
        }
        Assert.True(Days(0.0) > Days(0.5) && Days(0.5) > Days(1.0));
        TransportQuery.FreightEstimate twice = TransportQuery.EstimateFreight(w, Roads, S0, S1, 10000);
        Assert.Equal(2.0 * dirt.ThroughputDays, twice.ThroughputDays);
        Assert.Equal(dirt.TransitDays, twice.TransitDays);
    }

    [Fact]
    public void TravelTime_RoutesThroughAJunction_WhenTheBuiltNetworkBeatsTheDirectBaseline()
    {
        WorldState w = Toy([1, 1, 1], [(0, 1, 60.0), (1, 2, 60.0), (0, 2, 100.0)]);
        w.TransportEdges.Add(Route(1, 0, 1, EdgeTypes.Highway, 60.0));
        w.TransportEdges.Add(Route(2, 1, 2, EdgeTypes.Highway, 60.0));
        TransportQuery.FreightEstimate est = TransportQuery.EstimateFreight(w, Roads, S0, S2, 1);
        Assert.Equal([0, 1, 2], est.Path.Select(s => s.Value));
        Assert.Equal([1, 2], est.Links);
        Assert.Equal(2 * 60.0 * Roads.ClassOf(EdgeTypes.Highway)!.SpeedFactor, est.CostKm);
    }

    // ------------------------------------------------------------------ order validation

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(0, -1.0)]
    [InlineData(0, 100.0000001)]
    [InlineData(0, double.NaN)]
    [InlineData(3, 50.0)]
    public void DevelopRoads_MalformedPayload_IsRejectedAtLoad(int target, double pct)
    {
        var log = Log(OrderRecord.From(0, Player, OrderKind.DevelopRoads, target, pct));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(ms));
    }

    [Fact]
    public void DevelopRoads_WellFormedPayload_RoundTripsThroughTheLog()
    {
        var log = Log(Develop(4, Player, 37.5));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog back = OrderLog.Load(ms);
        Assert.Equal(Develop(4, Player, 37.5), back[0]);
    }

    // ------------------------------------------------------------------ PERSISTENCE (34, 35)

    [Fact]
    public void T34_SaveLoad_PreservesRoadStateAndModernizationProgress()
    {
        WorldState w = Know(Toy([1, 1, 1], [(0, 1, 100.0), (0, 2, 70.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(w, 0, Timber, 133);   // route (0,1) in full, (0,2) to 33/70
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Contains(after.TransportEdges.Rows(), e => e.Modernization > 0.0 && e.Modernization < 1.0);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(after, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(after), ms.Length);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(after, back));
        Assert.Equal(WorldHash.ComputeHex(after), WorldHash.ComputeHex(back));
        Assert.Equal(after.TransportEdges.Rows(), back.TransportEdges.Rows());
        // The loaded world continues the same route exactly as the live one does.
        ResearchRigs.Stock(after, 1, Timber, 100); ResearchRigs.Stock(back, 1, Timber, 100);
        Assert.Equal(WorldHash.ComputeHex(StepOnce(after, Develop(1, Player, 100.0))), WorldHash.ComputeHex(StepOnce(back, Develop(1, Player, 100.0))));
    }

    [Fact]
    public void T35_FoundedPipeline_PlayerAndAiRoadOrders_TwinAndReplayAreIdenticalEveryTurn_IncludingTravelTimes()
    {
        WorldState genesis = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42);
        foreach (PolityId p in new[] { Player, Rival }) Know(genesis, p, "track_road");
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        EraTable era = EraTableLoader.Load(eraStream);
        TurnExecutor Pipe(OrderLog log)
        {
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            return new TurnExecutor(era, PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.DevWorldgen() with { AiEmpires = 1 })), log);
        }

        var live = new OrderLog();
        TurnExecutor ex = Pipe(live);
        WorldState w = genesis.Clone();
        var hashes = new List<string>();
        for (int t = 0; t < 30; t++)
        {
            if (t % 5 == 0) live.Append(RoadDevelopmentQuery.DevelopOrder(w, Player, 40.0));
            foreach (OrderRecord o in RoadDevelopmentPolicy.OrdersForAi(w, Cfg)) live.Append(o);
            w = ex.Step(w);
            hashes.Add(WorldHash.ComputeHex(w));
        }
        Assert.True(w.TransportEdges.Count > 0, "no road was developed — the replay proves nothing");
        Assert.Contains(w.RoadDevelopments.Rows(), r => r.Polity == Rival);
        Assert.Contains(w.RoadDevelopments.Rows(), r => r.Polity == Player);
        // No pair ever carries more than one route row: development never laid a parallel road.
        foreach (TransportEdgeRow e in w.TransportEdges.Rows())
            Assert.Single(TransportQuery.EdgesBetween(w, e.A, e.B));

        WorldState genesis2 = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42);
        foreach (PolityId p in new[] { Player, Rival }) Know(genesis2, p, "track_road");
        TurnExecutor replay = Pipe(live);
        WorldState r = genesis2;
        for (int t = 0; t < 30; t++)
        {
            r = replay.Step(r);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(r));
        }
        Assert.True(WorldStates.StateEquals(w, r));
        TransportEdgeRow any = w.TransportEdges[0];
        TransportQuery.FreightEstimate fl = TransportQuery.EstimateFreight(w, Roads, any.A, any.B, 5000);
        TransportQuery.FreightEstimate fr = TransportQuery.EstimateFreight(r, Roads, any.A, any.B, 5000);
        Assert.Equal((fl.CostKm, fl.TotalDays), (fr.CostKm, fr.TotalDays));
    }

    // ------------------------------------------------------------------ content (rulings 16, 17, 18)

    [Fact]
    public void T33_Content_MotorRoad_Key425_A9Engineering_ThreeRuledPrerequisites_UnlocksTheHighwayEntity_Adr029Cost()
    {
        int i = Research.IndexOfId("motor_road");
        Assert.True(i >= 0);
        ResearchNode n = Research.Nodes[i];
        Assert.Equal(425, n.Key.Value);
        Assert.Equal("A9", n.Age);
        Assert.Equal(ResearchTree.Technology, n.Tree);
        Assert.Equal("engineering", Research.Branches[n.Branch].Id);
        Assert.Equal(["automobile_mass", "petroleum_refining", "reinforced_concrete"],
            n.PrerequisiteNodes.Select(p => Research.Nodes[p].Id).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("automobile_mass AND reinforced_concrete AND petroleum_refining", n.Prerequisite!.Source);
        Assert.Equal(["infra.road_highway"], n.UnlockedEntities.Select(e => Research.Entities[e].Id));
        Assert.Equal("motor_road", Research.Entities[Research.EntityIndexOf("infra.road_highway")].Requirement!.Source);
        Assert.All(n.Capabilities, c => Assert.Contains("class", c, StringComparison.Ordinal));   // a class unlock, not a construction
        // ADR-029 methodology: BaseCost = U × K^magnitude, rounded to 10 RP, magnitude = the sum of the six factors.
        double U = Research.Tuning.CostModel.U, K = Research.Tuning.CostModel.K;
        Assert.Equal(Math.Round(U * Math.Pow(K, 8.5) / 10.0) * 10.0, n.BaseCost);
        Assert.Equal(33450.0, n.BaseCost);
        Assert.Equal(1 + Research.Nodes[Research.IndexOfId("reinforced_concrete")].Depth, n.Depth);
        // Chronology: emerged 1924 CE, after every prerequisite's marked date.
        double self = ResearchContentTests.EarliestMarkedYear(n.Emerged)!.Value;
        foreach (int p in n.PrerequisiteNodes)
            Assert.True(ResearchContentTests.EarliestMarkedYear(Research.Nodes[p].Emerged)!.Value <= self, Research.Nodes[p].Id);
    }

    [Fact]
    public void Content_DryDock_RequiresTheCarrack_NotThePoundLock()
    {
        ResearchEntity dock = Research.Entities[Research.EntityIndexOf("building.dry_dock")];
        Assert.Equal("carrack", dock.Requirement!.Source);
        Assert.Contains(dock.Index, Research.Nodes[Research.IndexOfId("carrack")].UnlockedEntities);
        Assert.DoesNotContain(dock.Index, Research.Nodes[Research.IndexOfId("canal_lock")].UnlockedEntities);
    }

    [Fact]
    public void Content_TrackRoad_IsATrackway_NotAnEngineeredRoad()
    {
        ResearchNode n = Research.Nodes[Research.IndexOfId("track_road")];
        Assert.Equal(74, n.Key.Value);                                       // key unchanged
        Assert.Equal("Trackway", n.Name);
        Assert.DoesNotContain("Built trackway", n.Description, StringComparison.Ordinal);
        Assert.Contains("not an engineered road", n.Description, StringComparison.Ordinal);
        Assert.All(n.Capabilities, c => Assert.DoesNotContain("built road", c, StringComparison.OrdinalIgnoreCase));
        // The built road is not the trackway's alone: it also needs dry-stone construction.
        Assert.Equal("track_road AND stone_dry", Research.Entities[Research.EntityIndexOf("infra.road_built")].Requirement!.Source);
    }
}
