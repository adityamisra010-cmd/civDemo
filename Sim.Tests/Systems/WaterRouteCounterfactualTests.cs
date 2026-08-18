using System.Globalization;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6b certification directive — THE T3.7-SEED MEASUREMENT. Tests the
/// hypothesis that escalation 1 (transport deadband exceeds the price band
/// for bulk ≥ 8 at map distances) is the model being CORRECT about overland
/// bulk haulage, with the missing piece being D-009's never-built WATER
/// edges rather than any frozen constant. MEASURES, does not design: no
/// water transport is implemented, no bulk value, transport coefficient,
/// price band or map scale is touched.
///
/// REFERENCE CLASS, stated BEFORE any number was computed (the anti-fitting
/// order): pre-modern freight-cost ratios — the classical corpus anchored on
/// Diocletian's Price Edict and corroborated by medieval English cartage-vs-
/// river rates puts ROAD : RIVER per ton-km at roughly 5–10 : 1 and
/// ROAD : SEA at roughly 25–60 : 1. The counterfactual therefore prices
/// water traversal at r × ideal-land cost with the RIVER band
/// r ∈ [0.10, 0.20] as the judged range (a single water class at river
/// rates — treating sea as river — which UNDERSTATES the real advantage:
/// conservative in the direction that cannot manufacture the conclusion).
///
/// METHOD. Rig-local Dijkstra over the stride-4 lattice grid replicating the
/// shipped step model exactly (step a→b = mean(nodeCost_a, nodeCost_b) ×
/// length, √2 diagonals, the Pathfinder formula) with ONE counterfactual
/// change: nodes impassable-because-water become passable at nodeCost = r.
/// No network fast lanes on either side (founding-time comparison — built
/// paths would cheapen BOTH sides). Pair set: tin-richest 3 × tin-poorest 3
/// settlements by founded deposit abundance — the proxy for "largest ore
/// gap" pairs, since Item 0 measured floor-vs-ceiling tin prices tracking
/// exactly this split. CROSSING CONDITION: a bulk-8 good with the measured
/// full-band gap (19.95) trades iff pathCost &lt; 19.95 / (8 × 0.16) = 15.586.
/// Skip-gated after the measurement; docs/t3.6b-review-record.md records it.
///
/// INVALIDATION CONDITION (Q-F, director 2026-07-30; written T3.11 Item 4b).
/// A Skip-gated rig still COMPILES, so it goes stale in silence. These two
/// passes underwrite the reframing of escalation 1 from a CR against frozen
/// constants into a MISSING MECHANISM — the transport packet and T3.10 will
/// cite that conclusion, so the numbers behind it must not be quietly stale.
/// Both passes in this file share the condition; it is stated once here.
///
/// RE-RUN BOTH PASSES BEFORE CITING THEIR NUMBERS IF ANY OF THESE CHANGED:
///   - THE LATTICE STRIDE — the lattice pass is stride-4 and Q-A already
///     records that stride-4 majority-water blocks HIDE RIVERS, which is
///     why the pixel pass exists. Any stride change re-opens both, and
///     changes what the lattice pass can even see;
///   - THE RIVER / WATER MASK — hydrology, the impassable-because-water
///     predicate, or coastline: the counterfactual is defined by exactly
///     which nodes flip to passable;
///   - TRANSPORT COST — `costPerBulkCostUnit`, the Pathfinder step formula,
///     node costs, or the √2 diagonal convention (the rig REPLICATES the
///     shipped step model; if the model moves, the replica is wrong, not
///     merely stale — that is the sharper failure);
///   - BULK VALUES — CrossingCost hardcodes bulk 8;
///   - THE PRICE BAND — CrossingCost hardcodes the full-band gap 19.95, so
///     either band edge moving changes the threshold the whole conclusion
///     turns on;
///   - THE MAP SCALE — km per pixel sets every path cost in real units.
/// NOT invalidating: anything that leaves geometry, water and transport
/// cost alone — demography, needs, housing, prices ELSEWHERE in the band.
///
/// A change to the Pathfinder step model deserves its own emphasis: this
/// rig REPLICATES that model rather than calling it, so such a change makes
/// the rig SILENTLY WRONG rather than out of date. Re-derive, do not re-run.
/// </summary>
public class WaterRouteCounterfactualTests
{
    private const double CrossingCost = 19.95 / (8.0 * 0.16); // 15.586

    [Fact(Skip = "T3.6b→T3.7 seed measurement rig (~20 s, 3 canonical worldgens) — run manually; docs/t3.6b-review-record.md records the numbers")]
    public void WaterCounterfactual_OreDeadbandCrossing()
    {
        SimConfig cfg = TestConfigs.Sim();
        var sb = new System.Text.StringBuilder();
        foreach (ulong seed in new ulong[] { 42, 7, 303 })
        {
            WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
            TraversalLattice lattice = TraversalLattice.Build(world.Terrain!, cfg.Transport.RiverCostFactor);
            int tin = cfg.Goods!.IdOf("tin-ore");
            int n = world.Settlements.Count;

            var abundance = new double[n];
            for (int i = 0; i < world.Deposits.Count; i++)
                if (world.Deposits[i].Good.Value == tin)
                    abundance[world.Deposits[i].Settlement.Value] = world.Deposits[i].Abundance;
            int[] order = Enumerable.Range(0, n).OrderBy(s => abundance[s]).ToArray();
            int[] poor = order.Take(3).ToArray();
            int[] rich = order.Skip(n - 3).ToArray();

            var origins = new int[n];
            for (int s = 0; s < n; s++)
                origins[s] = LatticeMap.OriginLatticeNode(lattice, world.Terrain!.Size,
                    world.Settlements[s].SiteCell);

            foreach (int a in rich)
            {
                double[] land = Dijkstra(lattice, origins[a], waterCost: double.PositiveInfinity);
                double[] r20 = Dijkstra(lattice, origins[a], waterCost: 0.20);
                double[] r10 = Dijkstra(lattice, origins[a], waterCost: 0.10);
                foreach (int b in poor)
                {
                    double rStar = RStarCrossing(lattice, origins[a], origins[b]);
                    sb.Append(CultureInfo.InvariantCulture,
                        $"seed {seed} pair {a}->{b}: land={land[origins[b]]:F1} r0.20={r20[origins[b]]:F1} "
                        + $"r0.10={r10[origins[b]]:F1} r*={rStar:F3}\n");
                }
            }
        }
        System.IO.File.WriteAllText("/tmp/t36b-water.txt", sb.ToString());
    }

    /// <summary>
    /// PIXEL-RESOLUTION variant — the resolution caveat of the lattice rig,
    /// closed: the stride-4 lattice marks a node water only when its 4×4
    /// block is MAJORITY water, so narrow rivers — the hypothesis under test
    /// — are invisible to it and the lattice counterfactual prices only the
    /// SEA network. Here the Dijkstra runs per PIXEL (4 km): water pixels =
    /// Water ≥ 0.5 OR Rivers &gt; 0 (the same mask the siting BFS seeds from),
    /// priced at r × ideal; land pixels at MovementCost. Step = 0.25 ×
    /// mean(cost) × length (a pixel is a quarter-node, keeping the result in
    /// the SAME cost units as SettlementDistances). The land-only column
    /// validates the model against the lattice rig's numbers.
    /// </summary>
    [Fact(Skip = "T3.6b→T3.7 seed measurement rig (pixel Dijkstra, ~35 s) — run manually; docs/t3.6b-review-record.md records the numbers")]
    public void WaterCounterfactual_PixelResolution_RiversAsRoutes()
    {
        SimConfig cfg = TestConfigs.Sim();
        var sb = new System.Text.StringBuilder();
        foreach (ulong seed in new ulong[] { 42, 7, 303 })
        {
            WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
            TerrainSet terrain = world.Terrain!;
            int tin = cfg.Goods!.IdOf("tin-ore");
            int n = world.Settlements.Count;
            var abundance = new double[n];
            for (int i = 0; i < world.Deposits.Count; i++)
                if (world.Deposits[i].Good.Value == tin)
                    abundance[world.Deposits[i].Settlement.Value] = world.Deposits[i].Abundance;
            int[] order = Enumerable.Range(0, n).OrderBy(s => abundance[s]).ToArray();
            int[] poor = order.Take(3).ToArray();
            int[] rich = order.Skip(n - 3).ToArray();

            foreach (int a in rich)
            {
                double[] land = PixelDijkstra(terrain, world.Settlements[a].SiteCell, double.PositiveInfinity);
                double[] r50 = PixelDijkstra(terrain, world.Settlements[a].SiteCell, 0.50);
                double[] r35 = PixelDijkstra(terrain, world.Settlements[a].SiteCell, 0.35);
                double[] r20 = PixelDijkstra(terrain, world.Settlements[a].SiteCell, 0.20);
                double[] r10 = PixelDijkstra(terrain, world.Settlements[a].SiteCell, 0.10);
                foreach (int b in poor)
                {
                    int cell = world.Settlements[b].SiteCell;
                    sb.Append(CultureInfo.InvariantCulture,
                        $"seed {seed} pair {a}->{b}: pixelLand={land[cell]:F1} r0.50={r50[cell]:F1} "
                        + $"r0.35={r35[cell]:F1} r0.20={r20[cell]:F1} r0.10={r10[cell]:F1} "
                        + $"cross50={(r50[cell] < CrossingCost ? "Y" : "n")} cross35={(r35[cell] < CrossingCost ? "Y" : "n")} "
                        + $"cross20={(r20[cell] < CrossingCost ? "Y" : "n")} cross10={(r10[cell] < CrossingCost ? "Y" : "n")}\n");
                }
            }
        }
        System.IO.File.WriteAllText("/tmp/t36b-water-pixel.txt", sb.ToString());
    }

    private static double[] PixelDijkstra(TerrainSet terrain, int start, double waterCost)
    {
        int size = terrain.Size;
        ReadOnlySpan<double> mc = terrain.MovementCost;
        ReadOnlySpan<double> water = terrain.Water;
        ReadOnlySpan<double> rivers = terrain.Rivers;
        var isWater = new bool[size * size];
        for (int i = 0; i < isWater.Length; i++)
            isWater[i] = water[i] >= 0.5 || rivers[i] > 0.0;
        var cost = new double[size * size];
        for (int i = 0; i < cost.Length; i++)
            cost[i] = isWater[i] ? waterCost : mc[i];

        var dist = new double[size * size];
        Array.Fill(dist, double.PositiveInfinity);
        var queue = new PriorityQueue<int, double>();
        dist[start] = 0.0;
        queue.Enqueue(start, 0.0);
        int[] dx = [-1, 1, 0, 0, -1, 1, -1, 1];
        int[] dy = [0, 0, -1, 1, -1, -1, 1, 1];
        while (queue.TryDequeue(out int node, out double d))
        {
            if (d > dist[node]) continue;
            int x = node % size, y = node / size;
            double ch = cost[node];
            if (double.IsInfinity(ch)) continue;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int m = ny * size + nx;
                double ct = cost[m];
                if (double.IsInfinity(ct)) continue;
                // 0.25: a pixel is a quarter of a lattice node, so results
                // stay in SettlementDistances' cost units (1 unit = 16 km ideal).
                double step = 0.25 * 0.5 * (ch + ct) * (k < 4 ? 1.0 : TraversalLattice.DiagonalFactor);
                if (dist[node] + step < dist[m])
                {
                    dist[m] = dist[node] + step;
                    queue.Enqueue(m, dist[m]);
                }
            }
        }
        return dist;
    }

    /// <summary>Largest water-cost ratio r at which the pair's path cost
    /// falls below the bulk-8 full-band crossing cost (bisection to 1e-3;
    /// 0 if even free water cannot cross, 1 reported as "land already crosses").</summary>
    private static double RStarCrossing(TraversalLattice lattice, int from, int to)
    {
        if (Dijkstra(lattice, from, double.PositiveInfinity)[to] < CrossingCost) return 1.0;
        if (Dijkstra(lattice, from, 1e-6)[to] >= CrossingCost) return 0.0;
        double lo = 1e-6, hi = 1.0;
        while (hi - lo > 1e-3)
        {
            double mid = 0.5 * (lo + hi);
            if (Dijkstra(lattice, from, mid)[to] < CrossingCost) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>The shipped step model (mean node cost × length, √2 diagonals)
    /// with water nodes opened at <paramref name="waterCost"/>. Water nodes are
    /// exactly the lattice's impassable nodes (TraversalLattice.Build marks a
    /// node impassable iff its block is majority water).</summary>
    private static double[] Dijkstra(TraversalLattice lattice, int start, double waterCost)
    {
        int size = lattice.Size;
        var dist = new double[lattice.NodeCount];
        Array.Fill(dist, double.PositiveInfinity);
        var queue = new PriorityQueue<int, double>();
        dist[start] = 0.0;
        queue.Enqueue(start, 0.0);
        int[] dx = [-1, 1, 0, 0, -1, 1, -1, 1];
        int[] dy = [0, 0, -1, 1, -1, -1, 1, 1];
        while (queue.TryDequeue(out int node, out double d))
        {
            if (d > dist[node]) continue;
            int x = node % size, y = node / size;
            double costHere = lattice.IsPassable(node) ? lattice.NodeCost[node] : waterCost;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int m = ny * size + nx;
                double costThere = lattice.IsPassable(m) ? lattice.NodeCost[m] : waterCost;
                if (double.IsInfinity(costThere) || double.IsInfinity(costHere)) continue;
                double step = 0.5 * (costHere + costThere)
                    * (k < 4 ? 1.0 : TraversalLattice.DiagonalFactor);
                if (dist[node] + step < dist[m])
                {
                    dist[m] = dist[node] + step;
                    queue.Enqueue(m, dist[m]);
                }
            }
        }
        return dist;
    }
}
