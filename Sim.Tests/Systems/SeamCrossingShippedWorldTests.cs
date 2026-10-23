using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Migration;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// Batch 3 WS3 — THE SEAM IN A SHIPPED WORLD. Synthetic rigs prove the mechanism; this pins it on the game: canonical
/// seed 16 (1024 px, twelve settlements, worldgen.json as shipped) generates land across the seam (MEASURED: 123 rows
/// with land on both edge columns), and the shortest travel route between settlement 6 (site 880, 402) and
/// settlements 2, 3 and 7 (sites 214, 650 / 253, 377 / 139, 665) crosses it. The pinned pair is 3 → 6.
/// </summary>
public class SeamCrossingShippedWorldTests
{
    /// <summary>The 3 → 6 travel cost on the founded seed-16 world, turn 1 (after the first catchment step), in ideal-ground
    /// cost units (≈ 1,704 km): MEASURED on this tree by a probe calling Pathfinder.FindPath (126 lattice nodes); the
    /// test asserts the route query and the catchment's SettlementDistances row equal it bit for bit.</summary>
    private const double Seed16Route3To6 = 106.52576456018005;

    private static readonly Lazy<WorldState> Seed16 = new(() =>
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 16);
        return UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Catchment(cfg)).Step(w);
    });

    [Fact]
    public void Seed16_TheRouteFromSettlement3To6_CrossesTheSeam_AndIsTheCatchmentsTravelCost()
    {
        WorldState w = Seed16.Value;
        TerrainSet t = w.Terrain!;
        Assert.True(t.WrapX);
        Assert.Equal(253 + 377 * 1024, w.Settlements[3].SiteCell);
        Assert.Equal(880 + 402 * 1024, w.Settlements[6].SiteCell);
        TraversalLattice lattice = TraversalLattice.Build(t, TestConfigs.RiverCostFactor());
        int from = LatticeMap.OriginLatticeNode(lattice, t.Size, w.Settlements[3].SiteCell);
        int to = LatticeMap.OriginLatticeNode(lattice, t.Size, w.Settlements[6].SiteCell);

        RouteQuery.Route route = RouteQuery.Find(lattice, w, from, to);
        Assert.True(route.Found);
        bool crosses = false;
        for (int i = 1; i < route.Nodes.Length; i++)
            if (Math.Abs(route.Nodes[i] % lattice.Size - route.Nodes[i - 1] % lattice.Size) == lattice.Size - 1) crosses = true;
        Assert.True(crosses, "seed 16's 3 -> 6 route does not cross the seam");
        Assert.Equal(Seed16Route3To6, route.TotalCost);
        Assert.Equal(126, route.Nodes.Length);

        double distance = double.NaN;
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            if (w.SettlementDistances[i].From.Value == 3 && w.SettlementDistances[i].To.Value == 6) distance = w.SettlementDistances[i].TravelCost;
        Assert.Equal(route.TotalCost, distance);
    }

    [Fact]
    public void Seed16_TheSameGroundFlat_HasNoSuchShortcut()
    {
        // The SAME terrain layers on a flat raster (TerrainSet.FromLayers, wrapX false): the best 3 -> 6 route must go the
        // long way round over land, strictly dearer than the wrapped one (or not exist).
        WorldState w = Seed16.Value;
        TerrainSet t = w.Terrain!;
        var polylines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = t.RiverPolyline(i).ToArray();
        TerrainSet flat = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature, t.Moisture,
            t.Fertility, t.MovementCost, t.Rivers, polylines, wrapX: false);
        Assert.Equal(t.ContentHash, flat.ContentHash);   // the flag is not in the content hash; the rasters are identical
        TraversalLattice wl = TraversalLattice.Build(t, TestConfigs.RiverCostFactor());
        TraversalLattice fl = TraversalLattice.Build(flat, TestConfigs.RiverCostFactor());
        int from = LatticeMap.OriginLatticeNode(wl, t.Size, w.Settlements[3].SiteCell);
        int to = LatticeMap.OriginLatticeNode(wl, t.Size, w.Settlements[6].SiteCell);
        var bare = new WorldState(16);
        Pathfinder.PathResult wrapped = Pathfinder.FindPath(wl, bare, from, to);
        Pathfinder.PathResult flatPath = Pathfinder.FindPath(fl, bare, from, to);
        Assert.True(wrapped.Found);
        Assert.True(!flatPath.Found || flatPath.TotalCost > wrapped.TotalCost,
            $"flat {flatPath.TotalCost} is not dearer than the seam route {wrapped.TotalCost}");
    }

    private static bool CrossesSeam(int[] nodes, int latticeSize)
    {
        for (int i = 1; i < nodes.Length; i++)
            if (Math.Abs(nodes[i] % latticeSize - nodes[i - 1] % latticeSize) == latticeSize - 1) return true;
        return false;
    }

    private static bool RouteCrossesSeam(TraversalLattice lattice, IReadOnlyWorldState w, int a, int b)
    {
        RouteQuery.Route r = RouteQuery.Find(lattice, w,
            LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[a].SiteCell),
            LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[b].SiteCell));
        return r.Found && CrossesSeam(r.Nodes, lattice.Size);
    }

    /// <summary>
    /// Final nits (verifier WS3-MOVE, 2026-10-15) — PEOPLE MOVE ACROSS THE SEAM IN A SHIPPED WORLD. Flows across the seam
    /// were pinned only on the synthetic SeamWorld. MEASURED search on this tree (canonical cylinder, 300 turns each, the
    /// shipped seeds whose land crosses the seam — 6, 8, 14, 16, 17, 18, 20): only seed 16 has settlement pairs whose
    /// shortest route crosses it (three, all with settlement 6); orderless, no seed traded at all, and on seed 16 no
    /// migration step could be attributed to a seam pair alone. With the trade and recipe knowledge rigged at turn 0
    /// (TestConfigs.KnowTrade + KnowRecipes — the SeamWorld test's rig; seed 16 then trades 6,684 units in 300 turns, none
    /// between a seam pair) it can: in step 94 → 95 settlement 2 gains a migrant (inflow 1), and the only settlement the
    /// migration plan lets send people to 2 that step (a positive push or flight weight — the transfer loop moves no one
    /// on any other pair) is settlement 6 (push 8.35e-5, no flight; outflow 1), across the seam from it. So the migrant
    /// crossed the seam. Asserted as an existence within
    /// <see cref="SeamMigrationHorizon"/> turns, its premise (each such pair's shortest route crosses the seam, on the world
    /// of that step) asserted with it.
    /// </summary>
    [Fact]
    public void Seed16_WithTradeAndRecipesKnown_AMigrantCrossesTheSeam_FromTheOnlySettlementsThatMaySendTo()
    {
        // M6 Track EX (2026-10-22): an older-layer (batch-3 seam) existence rig — the discovery layer off (R4; MEASURED under
        // the shipped concealment: "no migration step within the horizon came only from across the seam").
        SimConfig cfg = TestConfigs.PreM6(TestConfigs.Sim());
        WorldState w = TestConfigs.KnowRecipes(TestConfigs.KnowTrade(WorldFounding.Found(TestConfigs.Worldgen(), cfg, 16), cfg), cfg);
        Assert.True(w.Terrain!.WrapX);
        TurnExecutor ex = UniversityRigs.Production(cfg);
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, TestConfigs.RiverCostFactor());
        int n = w.Settlements.Count;
        var crossesAtFounding = new bool[n, n];
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
                crossesAtFounding[a, b] = crossesAtFounding[b, a] = RouteCrossesSeam(lattice, w, a, b);

        for (int t = 0; t < SeamMigrationHorizon; t++)
        {
            WorldState prev = w;
            w = ex.Step(prev);
            if (prev.Settlements.Count != n || w.Settlements.Count != n) continue;
            MigrationPlan plan = MigrationSystem.Plan(prev, cfg, w.Clock.DtYears);
            for (int d = 0; d < n; d++)
            {
                if (w.MigrationFlows[d].Settlement != prev.Settlements[d].Id || w.MigrationFlows[d].Inflow <= 0) continue;
                var senders = new List<int>();
                bool allAcross = true;
                for (int s = 0; s < n; s++)
                {
                    if (s == d || (plan.Push[s, d] <= 0.0 && plan.FlightWeight[s, d] <= 0.0)) continue;
                    senders.Add(s);
                    allAcross &= crossesAtFounding[s, d];
                }
                if (senders.Count == 0 || !allAcross) continue;

                // Found: the premise on the world of this step — each sender's shortest route to d crosses the seam — and a
                // sender's outflow carries the inflow.
                foreach (int s in senders)
                    Assert.True(RouteCrossesSeam(lattice, prev, s, d),
                        $"turn {prev.Clock.Turn}: the route {prev.Settlements[s].Id.Value} -> {prev.Settlements[d].Id.Value} does not cross the seam");
                Assert.Contains(senders, s => w.MigrationFlows[s].Outflow > 0);
                return;
            }
        }
        Assert.Fail($"no migration step within {SeamMigrationHorizon} turns came only from across the seam");
    }

    /// <summary>The existence horizon of the seam-migration pin: the first such step MEASURED is 94 → 95.</summary>
    private const int SeamMigrationHorizon = 120;
}
