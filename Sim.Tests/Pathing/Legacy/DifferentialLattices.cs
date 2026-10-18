using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing.Legacy;

/// <summary>
/// Batch 4 (D-055, T5) — THE LATTICES THE PATHFINDING DIFFERENTIAL RUNS ON. Each case is a (lattice, world) pair the
/// batch-3 oracle (<see cref="LegacyPathfinder"/>) and the optimised <see cref="Pathfinder"/> are both run on, chosen to
/// reach every branch an optimisation could perturb:
/// - <b>walled9</b>: SeamPathfindingTests' 9×9 wall lattice (odd, tie-rich: costs 1.0–3.0 in half steps), flat (two
///   halves, mutually unreachable) and wrapped (joined only across the seam), and wrapped with a gap in the wall;
/// - <b>tie10 / tie12 / tie16</b>: EVEN-sized random-tie lattices — four node costs only (exact f- and g-ties
///   everywhere), about one node in seven impassable — wrapped (an even width hits the +size/2 heuristic tie) and flat;
///   tie11 is the odd control;
/// - <b>lanes10 / lanes12</b>: an even random-tie lattice with a lane world (<see cref="Lanes"/>): DUPLICATE lanes (an
///   exact copy, a reversed cheaper copy), SELF-ANCHORED lanes (an edge from a node id to itself, and two node ids on one
///   lattice node), a lane across the seam, a lane into an impassable node, network node ids with a gap, and TRAVELLED
///   road lanes (ADR-032) beside the skipped rows (not travelled, a missing settlement, both ends on one lattice node);
/// - <b>seam / seam-varied / seam-lanes</b>: the SeamWorld terrain (64×64 lattice, even, wrapped): uniform ideal ground
///   (maximal ties), the hash-varied ground of SeamPathfindingTests.SeamWorld_AStarEqualsDijkstra_AllPairs_OnVariedGround,
///   and uniform ground with lanes across the seam.
/// </summary>
public static class DifferentialLattices
{
    /// <summary>One differential case. <see cref="TargetStride"/> &gt; 1 samples the targets of an all-pairs sweep
    /// (every passable origin; the passable targets whose index ≡ the origin's index modulo the stride) on the big
    /// SeamWorld lattices, where every pair would take minutes; the small lattices take every pair.</summary>
    public sealed record Case(string Name, TraversalLattice Lattice, WorldState World, int TargetStride);

    /// <summary>Every case name, for xUnit theories.</summary>
    public static readonly string[] Names =
    [
        "walled9-flat", "walled9-wrap", "walled9gap-wrap",
        "tie10-wrap", "tie10-flat", "tie11-wrap", "tie12-wrap", "tie16-wrap",
        "lanes10-wrap", "lanes12-wrap", "lanes12-flat",
        "seam", "seam-varied", "seam-lanes",
    ];

    public static IEnumerable<object[]> Theory()
    {
        foreach (string name in Names) yield return [name];
    }

    public static Case Get(string name) => name switch
    {
        "walled9-flat" => new(name, Walled(9, wrap: false), new WorldState(1), 1),
        "walled9-wrap" => new(name, Walled(9, wrap: true), new WorldState(1), 1),
        "walled9gap-wrap" => new(name, Walled(9, wrap: true, gapInWall: true), new WorldState(1), 1),
        "tie10-wrap" => new(name, RandomTie(10, wrap: true, seed: 10), new WorldState(1), 1),
        "tie10-flat" => new(name, RandomTie(10, wrap: false, seed: 10), new WorldState(1), 1),
        "tie11-wrap" => new(name, RandomTie(11, wrap: true, seed: 11), new WorldState(1), 1),
        "tie12-wrap" => new(name, RandomTie(12, wrap: true, seed: 12), new WorldState(1), 1),
        "tie16-wrap" => new(name, RandomTie(16, wrap: true, seed: 16), new WorldState(1), 1),
        "lanes10-wrap" => LaneCase(name, RandomTie(10, wrap: true, seed: 110)),
        "lanes12-wrap" => LaneCase(name, RandomTie(12, wrap: true, seed: 112)),
        "lanes12-flat" => LaneCase(name, RandomTie(12, wrap: false, seed: 112)),
        "seam" => new(name, SeamLattice(varied: false), new WorldState(1), 7),
        "seam-varied" => new(name, SeamLattice(varied: true), new WorldState(1), 11),
        "seam-lanes" => SeamLaneCase(name),
        _ => throw new ArgumentException("unknown case " + name, nameof(name)),
    };

    private static Case LaneCase(string name, TraversalLattice lattice) => new(name, lattice, Lanes(lattice, 7), 1);

    /// <summary>SeamPathfindingTests.Walled, restated exactly: costs 1.0 + (i·7 mod 5)·0.5 and an impassable wall down
    /// the two middle columns (a gap in its last row when <paramref name="gapInWall"/>).</summary>
    public static TraversalLattice Walled(int size, bool wrap, bool gapInWall = false)
    {
        var costs = new double[size * size];
        var passable = new bool[size * size];
        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = 1.0 + (i * 7 % 5) * 0.5;
            int x = i % size, y = i / size;
            passable[i] = !(x == size / 2 || x == size / 2 - 1) || (gapInWall && y == size - 1);
        }
        return TraversalLattice.FromCosts(size, costs, passable, wrapX: wrap);
    }

    /// <summary>A size×size lattice whose node costs take only the four values {1.0, 1.5, 2.0, 3.0} (by a SplitMix64 hash
    /// of (seed, node)) — exact path-cost ties everywhere — with about one node in seven impassable.</summary>
    public static TraversalLattice RandomTie(int size, bool wrap, ulong seed)
    {
        double[] palette = [1.0, 1.5, 2.0, 3.0];
        var costs = new double[size * size];
        var passable = new bool[size * size];
        for (int i = 0; i < costs.Length; i++)
        {
            ulong h = SplitMix64.Mix(seed * 0x9E3779B97F4A7C15UL + (ulong)i);
            costs[i] = palette[(int)(h % 4UL)];
            passable[i] = (h >> 8) % 7UL != 0UL;
        }
        return TraversalLattice.FromCosts(size, costs, passable, wrapX: wrap);
    }

    /// <summary>The SeamWorld terrain's lattice (64×64, wrapped): uniform ideal ground, or the hash-varied ground
    /// (movement 1–5 per land cell) of SeamPathfindingTests' all-pairs test.</summary>
    public static TraversalLattice SeamLattice(bool varied)
    {
        TerrainSet t = SeamWorld.Terrain(wrapX: true);
        if (varied)
        {
            double[] movement = t.MovementCost.ToArray();
            for (int i = 0; i < movement.Length; i++)
                if (t.Water[i] < 0.5) movement[i] = 1.0 + (int)(SplitMix64.Mix((ulong)i * 0x9E3779B97F4A7C15UL) % 5UL);
            t = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature,
                t.Moisture, t.Fertility, movement, t.Rivers, [], wrapX: true);
        }
        return TraversalLattice.Build(t, TestConfigs.RiverCostFactor());
    }

    private static Case SeamLaneCase(string name)
    {
        TraversalLattice lattice = SeamLattice(varied: false);
        var w = new WorldState(1);
        int size = lattice.Size, row = size / 2;
        // Lanes across the seam (column 1 ↔ column size − 2, cheaper than the 3 steps), along the strip, a duplicate, and
        // a lane whose cost equals the walk it shortcuts (an exact tie between a lane and lattice steps).
        int[] anchors =
        [
            lattice.NodeId(1, row), lattice.NodeId(size - 2, row), lattice.NodeId(5, row - 6), lattice.NodeId(size - 7, row + 6),
            lattice.NodeId(3, row + 2), lattice.NodeId(size - 3, row + 2),
        ];
        for (int i = 0; i < anchors.Length; i++)
        {
            if (!lattice.IsPassable(anchors[i])) throw new InvalidOperationException("seam-lanes anchor " + i + " is water");
            w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(i), anchors[i]));
        }
        AddEdge(w, 0, 1, 1.5);       // (1, r) ↔ (size − 2, r): 3 seam steps of 1.0 walk, the lane is cheaper
        AddEdge(w, 2, 3, 6.0);
        AddEdge(w, 3, 2, 6.0);       // exact duplicate, reversed
        AddEdge(w, 4, 5, 6.0);       // (3, r + 2) → (size − 3, r + 2) walks 3, 2, 1, 0, size − 1, size − 2, size − 3: 6 steps of 1.0 = 6.0 — an EXACT lane/walk tie
        AddEdge(w, 1, 4, 4.0);
        return new Case(name, lattice, w, 7);
    }

    private static void AddEdge(WorldState w, int a, int b, double cost) =>
        w.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(w.NetworkEdges.Count),
            new NetworkNodeId(a), new NetworkNodeId(b), EdgeTypes.DirtPath, cost));

    /// <summary>
    /// The LANE WORLD over a terrain-less <paramref name="lattice"/> (stride 1, so a site cell is a lattice node). Network
    /// nodes (ids 0–8 with id 4 missing) anchored by hash to nodes of the lattice, plus:
    /// - id 9 on the SAME lattice node as id 0, and an edge 0–9 (a self-anchored lane) and an edge 2–2 (a self edge);
    /// - edges 0–1 twice at one cost and once reversed at half of it (duplicates, both directions);
    /// - an edge between column 0 and column Size − 1 of one row (across the seam when wrapped);
    /// - an edge into an impassable node (its lane is skipped at expansion, never in the overlay);
    /// - hash-chosen edges whose costs come from a tie palette.
    /// Four settlements (ids 0–3, plus id 4 founded on settlement 3's cell) and route rows: travelled roads 0–1 (twice),
    /// 1–2 and 2–3; a road 2–3 NOT complete; a road to a missing settlement (id 99); and a road 3–4, both ends on one
    /// lattice node — the last three are skipped by the overlay exactly as BuildOverlay skips them.
    /// </summary>
    public static WorldState Lanes(TraversalLattice lattice, ulong seed)
    {
        int size = lattice.Size, n = lattice.NodeCount;
        var w = new WorldState(1);
        var passable = new List<int>();
        int impassable = -1;
        for (int i = 0; i < n; i++)
        {
            if (lattice.IsPassable(i)) passable.Add(i);
            else if (impassable < 0) impassable = i;
        }
        int Pick(ulong k) => passable[(int)(SplitMix64.Mix(seed * 1_000_003UL + k) % (ulong)passable.Count)];

        // Node ids 0..8 except 4 (a gap in the id space), then 9 on node 0's anchor.
        int[] ids = [0, 1, 2, 3, 5, 6, 7, 8];
        for (int k = 0; k < ids.Length; k++)
            w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(ids[k]), Pick((ulong)k)));
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(9), w.NetworkNodes[0].LatticeNode));
        // The seam pair: the first row with column 0 and column size − 1 both passable.
        int seamA = -1, seamB = -1;
        for (int y = 0; y < size && seamA < 0; y++)
            if (lattice.IsPassable(lattice.NodeId(0, y)) && lattice.IsPassable(lattice.NodeId(size - 1, y)))
            { seamA = lattice.NodeId(0, y); seamB = lattice.NodeId(size - 1, y); }
        if (seamA < 0) throw new InvalidOperationException("no passable seam pair");
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(10), seamA));
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(11), seamB));
        if (impassable < 0) throw new InvalidOperationException("the lane lattice needs an impassable node");
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(12), impassable));

        AddEdge(w, 0, 1, 2.0);
        AddEdge(w, 0, 1, 2.0);       // exact duplicate
        AddEdge(w, 1, 0, 1.0);       // reversed, cheaper duplicate
        AddEdge(w, 0, 9, 0.5);       // self-anchored: two ids, one lattice node
        AddEdge(w, 2, 2, 1.0);       // self edge
        AddEdge(w, 10, 11, 0.75);    // across the seam (or the whole width, flat)
        AddEdge(w, 3, 12, 1.0);      // into an impassable node
        double[] palette = [1.0, 1.5, 2.0, 3.0, 4.5];
        for (int k = 0; k < 6; k++)
        {
            ulong h = SplitMix64.Mix(seed * 7919UL + (ulong)k);
            int a = ids[(int)(h % (ulong)ids.Length)], b = ids[(int)((h >> 16) % (ulong)ids.Length)];
            AddEdge(w, a, b, palette[(int)((h >> 32) % (ulong)palette.Length)]);
        }

        // Settlements: site cells are lattice nodes here (terrain-less: the lattice is its own raster).
        int[] sites = [Pick(101), Pick(102), Pick(103), Pick(104)];
        for (int s = 0; s < sites.Length; s++)
            w.Settlements.Add(new SettlementRow(new SettlementId(s), sites[s], 0));
        w.Settlements.Add(new SettlementRow(new SettlementId(4), sites[3], 0));   // one cell with settlement 3
        AddRoad(w, 0, 1, 6.0, 0.5, complete: true);
        AddRoad(w, 0, 1, 6.0, 0.25, complete: true);   // a duplicate route, cheaper
        AddRoad(w, 1, 2, 9.0, 0.5, complete: true);
        AddRoad(w, 2, 3, 3.0, 0.5, complete: true);
        AddRoad(w, 2, 3, 1.0, 0.5, complete: false);   // not travelled: skipped
        AddRoad(w, 0, 99, 1.0, 0.5, complete: true);   // a missing settlement: skipped
        AddRoad(w, 3, 4, 1.0, 0.5, complete: true);    // both ends on one lattice node: skipped
        return w;
    }

    private static void AddRoad(WorldState w, int a, int b, double lengthKm, double costFactor, bool complete) =>
        w.TransportEdges.Add(new TransportEdgeRow(w.TransportEdges.Count, new SettlementId(a), new SettlementId(b),
            EdgeTypes.BuiltRoad, TransportModes.Road, complete ? TransportEdgeStates.Complete : 0,
            CapacityTonnesPerYear: 100, LengthKm: lengthKm, Condition: 0, BuiltTurn: 0, UpgradedTurn: 0,
            TargetClass: EdgeTypes.BuiltRoad, Modernization: 0.0, CostFactor: costFactor));

    /// <summary>The passable nodes of <paramref name="lattice"/>, ascending.</summary>
    public static int[] PassableNodes(TraversalLattice lattice)
    {
        var nodes = new List<int>();
        for (int i = 0; i < lattice.NodeCount; i++) if (lattice.IsPassable(i)) nodes.Add(i);
        return [.. nodes];
    }
}
