using Sim.Core;
using Sim.Core.Kernel;
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
/// ADR-032 — the transport / inter-city road foundation, against the Director's transport rulings
/// (1–25, 2026-10-02). Most tests run ONLY RoadDevelopmentSystem on a hand-built world whose every
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

    // ------------------------------------------------------------------ baseline (ruling 3)

    [Fact]
    public void BaselinePaths_RemainFree_NoOrderNoRoad_TheDirtClassCostsNothing_AndIsNeverAResearchUnlock()
    {
        RoadClassConfig dirt = Roads.ClassOf(EdgeTypes.DirtPath)!;
        Assert.Null(dirt.Entity);
        Assert.Empty(dirt.MaterialsPerKm);

        // The founded production pipeline, no orders at all: PathBuild lays its free dirt paths
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

        // A DevelopRoads order never builds DirtPath, even with nothing researched.
        WorldState toy = Trade(Toy([1, 1], [(0, 1, 100.0)]), 0, 1, 10);
        WorldState after = StepOnce(toy, Develop(0, Player, 100.0));
        Assert.Equal(0, after.TransportEdges.Count);
        Assert.Equal(StockOf(toy, 0, Stone), StockOf(after, 0, Stone));
        Assert.Equal(RouteIneligibility.NoKnownClass, RoadDevelopmentQuery.Routes(toy, Research, Roads, Player)[0].Ineligible);
    }

    // ------------------------------------------------------------------ ownership and territory (rulings 6, 20)

    [Fact]
    public void Roads_AreUnowned_CrossUnclaimedTerritory_AndConnectDifferentCivilizations()
    {
        // 0 player, 1 rival, 2 UNRULED. The player develops both routes touching its settlement.
        WorldState w = Know(Toy([1, 2, -1], [(0, 1, 50.0), (0, 2, 60.0)]), Player, "track_road");
        Assert.False(EmpireQuery.TryGetController(w, new SettlementId(2), out _));
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));

        Assert.Equal([(0, 1), (0, 2)], Pairs(after).OrderBy(p => p.B));
        // Nothing on the row says whose it is (no owner field exists — TransportSchemaTests); the
        // rival can route freight over the road the player paid for.
        TransportQuery.FreightEstimate viaRoad = TransportQuery.EstimateFreight(after, Roads, new SettlementId(1), new SettlementId(0), 100);
        Assert.True(viaRoad.Reachable);
        Assert.NotEqual(-1, viaRoad.Links[0]);
        // A rival-only pair (1, 2) is not the player's to develop: no endpoint is the player's.
        WorldState w2 = Know(Toy([1, 2, -1], [(1, 2, 40.0)]), Player, "track_road");
        Assert.Empty(RoadDevelopmentQuery.Routes(w2, Research, Roads, Player));
        // ...but the RIVAL may develop the very same pair, into unclaimed territory.
        Know(w2, Rival, "track_road");
        Assert.Single(StepOnce(w2, Develop(0, Rival, 100.0)).TransportEdges.Rows());
    }

    // ------------------------------------------------------------------ the multigraph (rulings 4, 14, 20)

    [Fact]
    public void SameCities_CanCarryParallelPhysicalRoutes_AndASecondRouteDoesNotOverwriteTheFirst()
    {
        WorldState w = Trade(Know(Toy([1, 1], [(0, 1, 100.0)]), Player, "track_road"), 0, 1, 5);
        WorldState t1 = StepOnce(w, Develop(0, Player, 100.0));
        TransportEdgeRow track = Assert.Single(t1.TransportEdges.Rows());
        Assert.Equal(EdgeTypes.Trackway, track.EdgeType);

        // Built road is a better TIER: a new alignment beside the trackway, which stays as it was.
        Know(t1, Player, "stone_dry");
        WorldState t2 = StepOnce(t1, Develop(1, Player, 100.0));
        Assert.Equal(2, t2.TransportEdges.Count);
        Assert.Equal(track, t2.TransportEdges[0]);                       // byte-for-byte unchanged
        Assert.Equal(EdgeTypes.BuiltRoad, t2.TransportEdges[1].EdgeType);
        Assert.NotEqual(track.Id, t2.TransportEdges[1].Id);
        Assert.Equal(RoadDevelopmentSystem.KindNewRoute, t2.RoadDevelopments[^1].Kind);

        // Paved is the SAME tier as built: the built road is upgraded IN PLACE (same id), the
        // trackway still untouched, and no third edge appears.
        Know(t2, Player, "road_paved");
        WorldState t3 = StepOnce(t2, Develop(2, Player, 100.0));
        Assert.Equal(2, t3.TransportEdges.Count);
        Assert.Equal(track, t3.TransportEdges[0]);
        Assert.Equal(t2.TransportEdges[1].Id, t3.TransportEdges[1].Id);
        Assert.Equal(EdgeTypes.PavedRoad, t3.TransportEdges[1].EdgeType);
        Assert.Equal(RoadDevelopmentSystem.KindUpgrade, t3.RoadDevelopments[^1].Kind);
        Assert.Equal((EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad), (t3.RoadDevelopments[^1].FromClass, t3.RoadDevelopments[^1].ToClass));

        // The highway is a new alignment again: three parallel edges, three classes, one pair.
        Know(t3, Player, "motor_road");
        WorldState t4 = StepOnce(t3, Develop(3, Player, 100.0));
        Assert.Equal([EdgeTypes.Trackway, EdgeTypes.PavedRoad, EdgeTypes.Highway],
            TransportQuery.EdgesBetween(t4, new SettlementId(1), new SettlementId(0)).Select(e => e.EdgeType));
        Assert.Equal(EdgeTypes.Highway, TransportQuery.BestClassBetween(t4, new SettlementId(0), new SettlementId(1)));
    }

    [Fact]
    public void RoadClasses_ArePreserved_OnlyTheDevelopedEdgeChanges_AndReplayOfTheSameLogIsIdentical()
    {
        WorldState w = Know(Toy([1, 1, 1], [(0, 1, 30.0), (0, 2, 40.0), (1, 2, 50.0)]), Player, "track_road", "stone_dry");
        Trade(w, 0, 1, 9);
        var log = Log(Develop(0, Player, 100.0), Develop(1, Player, 100.0));
        WorldState a = RoadsOnly(log).Run(w, 3);
        WorldState b = RoadsOnly(log).Run(w.Clone(), 3);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        // Turn 0: the traded pair's usage (9 of 9) covers 100% of demand alone -> one BuiltRoad.
        // Turn 1: the two remaining eligible pairs carry no trade, so 100% is of the route COUNT -> both.
        Assert.Equal(3, a.TransportEdges.Count);
        for (int i = 0; i < a.TransportEdges.Count; i++) Assert.Equal(EdgeTypes.BuiltRoad, a.TransportEdges[i].EdgeType);
        Assert.Equal(EdgeTypes.BuiltRoad, TransportQuery.BestClassBetween(a, new SettlementId(1), new SettlementId(2)));
    }

    // ------------------------------------------------------------------ junctions (ruling 10)

    [Fact]
    public void Junctions_AreDerivedFromTopology_DegreeEdgesNeighboursModes()
    {
        var w = Toy([1, 1, 1, 1, 1], []);
        void Edge(int id, int a, int b, int cls) => w.TransportEdges.Add(new TransportEdgeRow(id, new SettlementId(a), new SettlementId(b),
            cls, TransportModes.Road, TransportEdgeStates.Complete, 1, 10.0, 0, 1, 1));
        Edge(1, 0, 1, EdgeTypes.Trackway);
        Edge(2, 0, 2, EdgeTypes.PavedRoad);
        Edge(3, 1, 2, EdgeTypes.Trackway);
        Edge(4, 0, 3, EdgeTypes.BuiltRoad);
        Edge(5, 0, 1, EdgeTypes.Highway);   // parallel: a second physical edge 0-1

        Assert.Equal(4, TransportQuery.Degree(w, new SettlementId(0)));
        Assert.True(TransportQuery.IsJunction(w, new SettlementId(0)));
        Assert.Equal(3, TransportQuery.Degree(w, new SettlementId(1)));             // 0-1 twice + 1-2
        Assert.True(TransportQuery.IsJunction(w, new SettlementId(1)));
        Assert.False(TransportQuery.IsJunction(w, new SettlementId(2)));            // degree 2: a through-route
        Assert.False(TransportQuery.IsJunction(w, new SettlementId(3)));            // degree 1: a dead end
        Assert.False(TransportQuery.IsJunction(w, new SettlementId(4)));            // degree 0
        Assert.Equal([0, 1], TransportQuery.Junctions(w).Select(s => s.Value));
        Assert.Equal([1, 2, 3], TransportQuery.Neighbors(w, new SettlementId(0)).Select(s => s.Value));
        Assert.Equal([1, 2, 4, 5], TransportQuery.EdgesAt(w, new SettlementId(0)).Select(e => e.Id));
        Assert.Equal([TransportModes.Road], TransportQuery.ModesAt(w, new SettlementId(0)));
        Assert.Empty(TransportQuery.ModesAt(w, new SettlementId(4)));
        Assert.Equal([2], TransportQuery.SharedNeighbors(w, new SettlementId(0), new SettlementId(1)).Select(s => s.Value));
        // No junction entity exists anywhere: the only transport tables are edges and the log.
        Assert.Equal(0, w.NetworkNodes.Count);
    }

    // ------------------------------------------------------------------ research (ruling 15)

    [Fact]
    public void Research_GatesEligibility_ClassByClass()
    {
        WorldState w = Toy([1, 1], [(0, 1, 10.0)]);
        int Best() => RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Player);
        Assert.Equal(EdgeTypes.DirtPath, Best());
        Know(w, Player, "track_road");
        Assert.Equal(EdgeTypes.Trackway, Best());
        Know(w, Player, "stone_dry");
        Assert.Equal(EdgeTypes.BuiltRoad, Best());                  // infra.road_built = track_road AND stone_dry
        Know(w, Player, "road_paved");
        Assert.Equal(EdgeTypes.PavedRoad, Best());
        Know(w, Player, "macadam");
        Assert.Equal(EdgeTypes.MacadamRoad, Best());
        Assert.False(RoadDevelopmentQuery.IsClassKnown(w, Research, Roads, Player, EdgeTypes.Highway));
        Know(w, Player, "motor_road");
        Assert.Equal(EdgeTypes.Highway, Best());
        // Knowledge is per polity: the rival knows nothing.
        Assert.Equal(EdgeTypes.DirtPath, RoadDevelopmentQuery.BestKnownClass(w, Research, Roads, Rival));
    }

    [Fact]
    public void ResearchCompletion_ConstructsNoRoad_OnlyAnOrderDoes()
    {
        // Research system + road system together, the target completes, and many turns pass:
        // no road. The SAME world with one DevelopRoads order: a road.
        WorldState w = Trade(Toy([1, 1], [(0, 1, 10.0)]), 0, 1, 3);
        Know(w, Player, "track_road");
        var ex = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Research(Cfg), SystemCatalog.RoadDevelopment(Cfg)], null);
        WorldState idle = ex.Run(w, 10);
        Assert.Equal(0, idle.TransportEdges.Count);
        Assert.True(RoadDevelopmentQuery.Routes(idle, Research, Roads, Player)[0].Eligible);   // eligible, not built
        WorldState ordered = StepOnce(idle, Develop(idle.Clock.Turn, Player, 100.0));
        Assert.Equal(1, ordered.TransportEdges.Count);
    }

    // ------------------------------------------------------------------ the action (rulings 1, 2, 13)

    [Fact]
    public void PlayerAction_ConstructsRoads_TurnExact_TheEdgeFirstExistsInTheStateAfterTheOrdersTurn()
    {
        WorldState w0 = Trade(Know(Toy([1, 1], [(0, 1, 25.0)]), Player, "track_road"), 0, 1, 1);
        var ex = RoadsOnly(Log(Develop(2, Player, 100.0)));
        WorldState w1 = ex.Step(w0), w2 = ex.Step(w1);
        Assert.Equal(0, w1.TransportEdges.Count);                       // stamped 2: turns 0 and 1 untouched
        Assert.Equal(0, w2.TransportEdges.Count);                       // the state OF turn 2 is not retro-edited
        WorldState w3 = ex.Step(w2);
        TransportEdgeRow e = Assert.Single(w3.TransportEdges.Rows());
        Assert.Equal(3L, w3.Clock.Turn);
        Assert.Equal(3L, e.BuiltTurn);                                  // first visible in turn 3
        Assert.Equal(2L, w3.RoadDevelopments[0].Turn);                  // decided in turn 2
        Assert.Equal((EdgeTypes.Trackway, TransportModes.Road, TransportEdgeStates.Complete, 0),
            (e.EdgeType, e.Mode, e.State, e.Condition));
        Assert.Equal(Roads.ClassOf(EdgeTypes.Trackway)!.CapacityTonnesPerYear, e.CapacityTonnesPerYear);
        Assert.Equal(25.0, e.LengthKm);
        // Cost: 25 km × 1 timber/km = 25 timber, through the Ledger, from the paying endpoint 0.
        Assert.Equal(StockOf(w0, 0, Timber) - 25, StockOf(w3, 0, Timber));
        Assert.Equal(StockOf(w0, 1, Timber), StockOf(w3, 1, Timber));
        Assert.Equal(25, w3.RoadDevelopments[0].MaterialUnits);
    }

    [Fact]
    public void HighestUseRoutesFirst_AndThePercentageOfDemandIsRespected()
    {
        // Usages: (0,1) 50, (0,2) 30, (0,3) 20 — total 100.
        WorldState w = Know(Toy([1, 1, 1, 1], [(0, 1, 10.0), (0, 2, 10.0), (0, 3, 10.0)]), Player, "track_road");
        Trade(w, 0, 1, 30); Trade(w, 1, 0, 20);              // both directions count
        Trade(w, 2, 0, 30);
        Trade(w, 0, 3, 20);
        RouteStatus[] ranked = RoadDevelopmentQuery.Ranked(RoadDevelopmentQuery.Routes(w, Research, Roads, Player));
        Assert.Equal([50L, 30L, 20L], ranked.Select(r => r.Usage));

        int[] Built(double pct) => StepOnce(w.Clone(), Develop(0, Player, pct)).TransportEdges.Rows().Select(e => e.B.Value).ToArray();
        Assert.Equal([1], Built(10.0));          // the first route alone covers 50%
        Assert.Equal([1], Built(50.0));          // exactly 50 of 100: still one
        Assert.Equal([1, 2], Built(50.0001));    // just above: the next by usage
        Assert.Equal([1, 2], Built(80.0));
        Assert.Equal([1, 2, 3], Built(80.5));
        Assert.Equal([1, 2, 3], Built(100.0));
    }

    [Fact]
    public void WithNoTradeYet_ThePercentageIsOfTheRouteCount_RoundedUp_InIdOrder()
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
    public void Ties_AreBrokenByStableEndpointIds_TieDense_AndInsensitiveToRowOrder()
    {
        // Twelve routes, ALL of equal usage, inserted in a scrambled order: the ranking is (A, B)
        // ascending whatever the table order, and the built prefix follows it.
        var pairs = new List<(int, int, double)>();
        int[] scramble = [7, 2, 11, 4, 9, 1, 12, 6, 3, 10, 5, 8];
        foreach (int b in scramble) pairs.Add((0, b, 10.0));
        WorldState w = Know(Toy(Enumerable.Repeat(1, 13).ToArray(), [.. pairs]), Player, "track_road");
        foreach (int b in scramble) Trade(w, b, 0, 4);
        RouteStatus[] ranked = RoadDevelopmentQuery.Ranked(RoadDevelopmentQuery.Routes(w, Research, Roads, Player));
        Assert.Equal(Enumerable.Range(1, 12), ranked.Select(r => r.B.Value));
        Assert.Equal([1, 2, 3], StepOnce(w, Develop(0, Player, 25.0)).TransportEdges.Rows().Select(e => e.B.Value));

        // Ties on usage between pairs with different A: A breaks first, then B.
        var x = new RouteStatus(new SettlementId(2), new SettlementId(3), 1, 5, 1, 2, -1, RoadDevelopmentKind.NewRoute, RouteIneligibility.None);
        var y = new RouteStatus(new SettlementId(1), new SettlementId(9), 1, 5, 1, 2, -1, RoadDevelopmentKind.NewRoute, RouteIneligibility.None);
        var z = new RouteStatus(new SettlementId(1), new SettlementId(4), 1, 5, 1, 2, -1, RoadDevelopmentKind.NewRoute, RouteIneligibility.None);
        var hi = new RouteStatus(new SettlementId(9), new SettlementId(10), 1, 6, 1, 2, -1, RoadDevelopmentKind.NewRoute, RouteIneligibility.None);
        Assert.Equal([hi, z, y, x], RoadDevelopmentQuery.Ranked([x, y, z, hi]));
        Assert.Equal([hi, z, y, x], RoadDevelopmentQuery.Ranked([z, hi, x, y]));
    }

    [Fact]
    public void InsufficientResources_BuildTheAffordablePrefixOnly_NeverOverspend_NeverSkipAhead_NoDebt()
    {
        // Ranked: (0,1) 100 km, (0,2) 300 km, (0,3) 10 km. Trackway = 1 timber/km: 100, 300, 10.
        WorldState w = Know(Toy([1, 1, 1, 1], [(0, 1, 100.0), (0, 2, 300.0), (0, 3, 10.0)], stock: 0), Player, "track_road");
        Trade(w, 0, 1, 30); Trade(w, 0, 2, 20); Trade(w, 0, 3, 10);
        ResearchRigs.Stock(w, 0, Timber, 110);        // pays route 1 (100) and route 3 (10) — but NOT route 2
        WorldState after = StepOnce(w, Develop(0, Player, 100.0));
        Assert.Equal([(0, 1)], Pairs(after));         // route 3 is cheaper but lies AFTER the unaffordable route 2
        Assert.Equal(10, StockOf(after, 0, Timber));  // exactly 100 spent, nothing more
        Assert.Single(after.RoadDevelopments.Rows());

        // Exactly enough: spends to zero, never below.
        WorldState exact = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(exact, 0, Timber, 100);
        WorldState paid = StepOnce(exact, Develop(0, Player, 100.0));
        Assert.Equal(1, paid.TransportEdges.Count);
        Assert.Equal(0, StockOf(paid, 0, Timber));

        // One short: nothing at all, and nothing consumed.
        WorldState shortW = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road");
        ResearchRigs.Stock(shortW, 0, Timber, 99);
        WorldState unpaid = StepOnce(shortW, Develop(0, Player, 100.0));
        Assert.Equal(0, unpaid.TransportEdges.Count);
        Assert.Equal(99, StockOf(unpaid, 0, Timber));
        // A material the payer holds no row for at all: nothing built, no row conjured.
        WorldState none = Know(Toy([1, 1], [(0, 1, 100.0)], stock: 0), Player, "track_road");
        WorldState stillNone = StepOnce(none, Develop(0, Player, 100.0));
        Assert.Equal(0, stillNone.TransportEdges.Count);
        Assert.Equal(-1, GoodStockIndex.IndexOf(stillNone.GoodStocks, new SettlementId(0), new GoodId(Timber)));
    }

    [Fact]
    public void Cost_IsPerKmFromTuning_AnInTierUpgradePaysOnlyTheDifference_AndThePayerIsTheIssuersEndpoint()
    {
        // Endpoint 0 is the RIVAL's, endpoint 1 the player's: the player pays from 1.
        WorldState w = Know(Toy([2, 1], [(0, 1, 10.0)]), Player, "track_road", "stone_dry");
        WorldState built = StepOnce(w, Develop(0, Player, 100.0));
        // BuiltRoad = 2 stone + 0.5 timber per km over 10 km.
        Assert.Equal(StockOf(w, 1, Stone) - 20, StockOf(built, 1, Stone));
        Assert.Equal(StockOf(w, 1, Timber) - 5, StockOf(built, 1, Timber));
        Assert.Equal(StockOf(w, 0, Stone), StockOf(built, 0, Stone));
        // Upgrade BuiltRoad -> PavedRoad: 4 - 2 = 2 stone/km, timber 0.5 - 0.5 = 0.
        Know(built, Player, "road_paved");
        WorldState paved = StepOnce(built, Develop(1, Player, 100.0));
        Assert.Equal(StockOf(built, 1, Stone) - 20, StockOf(paved, 1, Stone));
        Assert.Equal(StockOf(built, 1, Timber), StockOf(paved, 1, Timber));
        Assert.Equal(20, paved.RoadDevelopments[^1].MaterialUnits);
    }

    [Fact]
    public void OneActionPerEmpirePerTurn_AndASecondEmpireSeesTheFirstsRoadsInTheSameStep()
    {
        // Both empires touch the shared pair (0, 1); the player orders first in the log.
        WorldState w = Know(Know(Toy([1, 2], [(0, 1, 10.0)]), Player, "track_road"), Rival, "track_road");
        WorldState after = StepOnce(w, Develop(0, Player, 100.0), Develop(0, Player, 100.0), Develop(0, Rival, 100.0));
        TransportEdgeRow e = Assert.Single(after.TransportEdges.Rows());   // never double-built
        Assert.Equal(Player, after.RoadDevelopments[0].Polity);
        Assert.Single(after.RoadDevelopments.Rows());
        Assert.Equal(EdgeTypes.Trackway, e.EdgeType);
    }

    // ------------------------------------------------------------------ AI and player (ruling 9)

    [Fact]
    public void AiAndPlayer_InvokeTheSameOperation_SameOrderShape_SameSelection_SameResult()
    {
        // Mirror worlds: the player in one, the AI in the other, identical inputs.
        WorldState forPlayer = Trade(Know(Toy([1, 1, 1], [(0, 1, 20.0), (0, 2, 30.0)]), Player, "track_road"), 0, 2, 8);
        WorldState forAi = Trade(Know(Toy([2, 2, 2], [(0, 1, 20.0), (0, 2, 30.0)]), Rival, "track_road"), 0, 2, 8);

        Assert.Null(RoadDevelopmentPolicy.Decide(forPlayer, Cfg, Player));    // the player is never auto-ordered
        OrderRecord ai = Assert.Single(RoadDevelopmentPolicy.OrdersForAi(forAi, Cfg));
        Assert.Equal(RoadDevelopmentQuery.DevelopOrder(forAi, Rival, RoadDevelopmentPolicy.TradedPercent), ai);
        OrderRecord player = RoadDevelopmentQuery.DevelopOrder(forPlayer, Player, ai.Amount);
        Assert.Equal((ai.Kind, ai.TargetId, ai.Amount), (player.Kind, player.TargetId, player.Amount));

        // The same selection function answers both, with the same steps.
        RoadDevelopmentStep[] pPlan = RoadDevelopmentQuery.Plan(forPlayer, Research, Roads, Cfg.Goods!, Player, player.Amount);
        RoadDevelopmentStep[] aPlan = RoadDevelopmentQuery.Plan(forAi, Research, Roads, Cfg.Goods!, Rival, ai.Amount);
        Assert.Equal(pPlan.Select(s => (s.Route, s.Payer)), aPlan.Select(s => (s.Route, s.Payer)));

        WorldState p1 = StepOnce(forPlayer, player), a1 = StepOnce(forAi, ai);
        Assert.Equal(p1.TransportEdges.Rows(), a1.TransportEdges.Rows());
        Assert.Equal([(0, 2)], Pairs(a1));                                     // the traded route, 50% of demand
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
    }

    // ------------------------------------------------------------------ deterioration (ruling 8)

    [Fact]
    public void Deterioration_HasNoGameplay_ConditionIsInert_AndNothingDecaysOverTime()
    {
        WorldState w = Trade(Know(Toy([1, 1, 1], [(0, 1, 20.0), (1, 2, 20.0), (0, 2, 80.0)]), Player, "track_road"), 0, 1, 2);
        WorldState built = StepOnce(w, Develop(0, Player, 100.0));
        WorldState aged = RoadsOnly(null).Run(built, 200);                    // 2,000 sim-years, no order
        Assert.Equal(built.TransportEdges.Rows(), aged.TransportEdges.Rows());

        // Writing ANY condition value changes no query: not travel, not eligibility, not the plan.
        WorldState worn = built.Clone();
        for (int i = 0; i < worn.TransportEdges.Count; i++) worn.TransportEdges[i] = worn.TransportEdges[i] with { Condition = -987 };
        var from = new SettlementId(0); var to = new SettlementId(2);
        TransportQuery.FreightEstimate a = TransportQuery.EstimateFreight(built, Roads, from, to, 5000);
        TransportQuery.FreightEstimate b = TransportQuery.EstimateFreight(worn, Roads, from, to, 5000);
        Assert.Equal((a.CostKm, a.TotalDays, a.BottleneckCapacityTonnesPerYear), (b.CostKm, b.TotalDays, b.BottleneckCapacityTonnesPerYear));
        Know(built, Player, "stone_dry"); Know(worn, Player, "stone_dry");
        Assert.Equal(RoadDevelopmentQuery.Routes(built, Research, Roads, Player), RoadDevelopmentQuery.Routes(worn, Research, Roads, Player));
        Assert.Equal(StepOnce(built, Develop(1, Player, 100.0)).RoadDevelopments.Rows(),
                     StepOnce(worn, Develop(1, Player, 100.0)).RoadDevelopments.Rows());
    }

    // ------------------------------------------------------------------ the travel-time hook

    [Fact]
    public void TravelTime_BetterRoadsAreFaster_AndTheDirectorsReferenceFigureHoldsOnTheBaseline()
    {
        // 3000 km on the free baseline, 5000 t: ~3 in-game months (the Director's figure).
        WorldState w = Toy([1, 1], [(0, 1, 3000.0)]);
        TransportQuery.FreightEstimate dirt = TransportQuery.EstimateFreight(w, Roads, new SettlementId(0), new SettlementId(1), 5000);
        Assert.True(dirt.Reachable);
        Assert.Equal(3000.0 / Roads.BaselineKmPerDay, dirt.TransitDays);
        Assert.Equal(5000.0 / Roads.ClassOf(EdgeTypes.DirtPath)!.CapacityTonnesPerYear * TransportQuery.DaysPerYear, dirt.ThroughputDays);
        Assert.InRange(dirt.TotalDays, 80.0, 100.0);
        Assert.Equal([-1], dirt.Links);

        double previous = dirt.TotalDays;
        foreach (int cls in new[] { EdgeTypes.Trackway, EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, EdgeTypes.Highway })
        {
            WorldState r = w.Clone();
            RoadClassConfig c = Roads.ClassOf(cls)!;
            r.TransportEdges.Add(new TransportEdgeRow(1, new SettlementId(0), new SettlementId(1), cls, TransportModes.Road,
                TransportEdgeStates.Complete, c.CapacityTonnesPerYear, 3000.0, 0, 1, 1));
            TransportQuery.FreightEstimate est = TransportQuery.EstimateFreight(r, Roads, new SettlementId(0), new SettlementId(1), 5000);
            Assert.True(est.TotalDays < previous, $"class {cls} is not faster than the class below it");
            Assert.Equal([1], est.Links);
            previous = est.TotalDays;
        }
        // Throughput is a per-YEAR rate turned into days: twice the tonnage, twice the throughput days.
        TransportQuery.FreightEstimate twice = TransportQuery.EstimateFreight(w, Roads, new SettlementId(0), new SettlementId(1), 10000);
        Assert.Equal(2.0 * dirt.ThroughputDays, twice.ThroughputDays);
        Assert.Equal(dirt.TransitDays, twice.TransitDays);
    }

    [Fact]
    public void TravelTime_RoutesThroughAJunction_WhenTheBuiltNetworkBeatsTheDirectBaseline()
    {
        // Direct baseline 0-2 is 100 km; a highway 0-1-2 (2 × 60 km at speed factor 0.15) is 18 km-equivalent.
        WorldState w = Toy([1, 1, 1], [(0, 1, 60.0), (1, 2, 60.0), (0, 2, 100.0)]);
        RoadClassConfig hw = Roads.ClassOf(EdgeTypes.Highway)!;
        w.TransportEdges.Add(new TransportEdgeRow(1, new SettlementId(0), new SettlementId(1), EdgeTypes.Highway, TransportModes.Road,
            TransportEdgeStates.Complete, hw.CapacityTonnesPerYear, 60.0, 0, 1, 1));
        w.TransportEdges.Add(new TransportEdgeRow(2, new SettlementId(1), new SettlementId(2), EdgeTypes.Highway, TransportModes.Road,
            TransportEdgeStates.Complete, hw.CapacityTonnesPerYear, 60.0, 0, 1, 1));
        TransportQuery.FreightEstimate est = TransportQuery.EstimateFreight(w, Roads, new SettlementId(0), new SettlementId(2), 1);
        Assert.Equal([0, 1, 2], est.Path.Select(s => s.Value));
        Assert.Equal([1, 2], est.Links);
        Assert.Equal(2 * 60.0 * hw.SpeedFactor, est.CostKm);
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

    // ------------------------------------------------------------------ replay determinism on the real pipeline

    [Fact]
    public void FoundedPipeline_PlayerAndAiRoadOrders_TwinAndReplayAreIdenticalEveryTurn()
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

        // LIVE: the player's slider every 5 turns, the AI's policy every turn, into one log.
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
        Assert.True(w.TransportEdges.Count > 0, "no road was built — the replay proves nothing");
        Assert.Contains(w.RoadDevelopments.Rows(), r => r.Polity == Rival);
        Assert.Contains(w.RoadDevelopments.Rows(), r => r.Polity == Player);

        // REPLAY from the recorded log alone, in a fresh executor, from a fresh founding.
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
    }

    // ------------------------------------------------------------------ content (rulings 16, 17, 18)

    [Fact]
    public void Content_MotorRoad_Key425_A9Engineering_ThreeRuledPrerequisites_UnlocksTheHighwayEntity_Adr029Cost()
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
