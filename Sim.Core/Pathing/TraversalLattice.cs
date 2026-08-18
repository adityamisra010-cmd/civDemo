using Sim.Core.Worldgen;

namespace Sim.Core.Pathing;

/// <summary>
/// The traversal lattice (m1 spec §3): a stride-4 sampling of the movement-cost
/// field — 256² nodes at the 1024² default. An UNDER-THE-HOOD grid per D-012's
/// precedent: never design-visible, never a sim unit of place. All pathfinding
/// and isochrones run here; network edges overlay as fast lanes at their
/// endpoints' lattice nodes.
///
/// Derived, immutable, rebuildable: a pure function of
/// (TerrainSet, riverCostFactor, stride) — not state, not a cache. Node ids are
/// row-major and stable. A node is impassable (water) when the block's water
/// fraction is ≥ 0.5; RIVERS DO NOT CHANGE PASSABILITY (river cells are LAND).
///
/// NODE COST (T4.7 — the river-aware cost term). Base: the AVERAGE movement
/// cost over the node's stride×stride terrain block (documented choice: block
/// averaging is stable and representative; point sampling would alias
/// single-pixel features). Before T4.7 that was the WHOLE story and
/// <see cref="TerrainSet.Rivers"/> was never read here at all — so a river
/// threading a block was invisible to every path, catchment and isochrone in
/// the game (proven at T4.9: `water[]` and the river mask are separate arrays
/// and river cells are land, so no stride hides or reveals them).
///
/// THE TERM, and why it is not an area mean. A traveller crossing a block that
/// a river threads does not average the block; they follow the river as far as
/// it goes their way and walk the rest. So the block cost is a PATH-LENGTH
/// weighted blend, not an area-weighted one:
///
///     span      = min(1, riverCells / stride)          // river cells per crossing row
///     nodeCost  = (1 − span) × blockMeanCost + span × riverCostFactor
///
/// A river running straight through the block contributes `stride` cells, so
/// span = 1 and the node costs exactly `riverCostFactor` — which is precisely
/// what the T3.6b PIXEL counterfactual charges for crossing that block along
/// the river (r per cell × stride cells × the ¼-node step scale). The lattice
/// therefore AGREES with the pixel model in the fully-spanning limit, and
/// degrades linearly toward the land mean as the river only clips the block.
/// A block with no river cells takes the untouched `costSum / cells` — the
/// same expression, in the same order, as before T4.7, so a world with no
/// rivers is bit-identical to the pre-T4.7 lattice.
///
/// `riverCostFactor` is a coefficient INSIDE this equation (law 2), never a
/// free-floating buff, and is denominated in the same units as movement cost:
/// a fraction of IDEAL GROUND (movement cost 1.0). Its derivation and its
/// ratified band live in sim.json's `transport._doc`.
/// </summary>
public sealed class TraversalLattice
{
    /// <summary>
    /// Diagonal step length factor: the fixed constant 1.4142135623730951 — the
    /// IEEE-754 double nearest √2 (exactly the rational 6369051672525773 / 2^52).
    /// A named compile-time constant, never a per-call Math.Sqrt — no per-call
    /// ambiguity between call sites.
    /// </summary>
    public const double DiagonalFactor = 1.4142135623730951;

    public int Size { get; }
    public int Stride { get; }
    public double KmPerNode { get; }

    private readonly double[] _nodeCost;
    private readonly bool[] _passable;

    public ReadOnlySpan<double> NodeCost => _nodeCost;
    public ReadOnlySpan<bool> Passable => _passable;

    public int NodeCount => Size * Size;
    public int NodeId(int x, int y) => y * Size + x;
    public (int X, int Y) Coords(int node) => (node % Size, node / Size);
    public bool IsPassable(int node) => _passable[node];

    /// <summary>The global minimum node cost over passable nodes (heuristic input).</summary>
    public double MinNodeCost { get; }

    private TraversalLattice(int size, int stride, double kmPerNode, double[] nodeCost, bool[] passable)
    {
        Size = size;
        Stride = stride;
        KmPerNode = kmPerNode;
        _nodeCost = nodeCost;
        _passable = passable;

        double min = double.MaxValue;
        for (int i = 0; i < nodeCost.Length; i++)
            if (passable[i] && nodeCost[i] < min) min = nodeCost[i];
        MinNodeCost = min == double.MaxValue ? 0.0 : min;
    }

    /// <summary>
    /// Build the lattice over <paramref name="terrain"/>. <paramref name="riverCostFactor"/>
    /// is the TUNE constant `transport.riverCostFactor` — river traversal cost as a
    /// fraction of ideal ground. It is a REQUIRED argument on purpose: the lattice must
    /// be identical for every consumer in a world, and a defaulted value is how two call
    /// sites end up on two different lattices.
    /// </summary>
    public static TraversalLattice Build(TerrainSet terrain, double riverCostFactor, int stride = 4)
    {
        if (!(riverCostFactor > 0.0 && riverCostFactor <= 1.0))
            throw new ArgumentOutOfRangeException(nameof(riverCostFactor),
                riverCostFactor,
                "transport.riverCostFactor must be in (0,1] — a river must be cheaper than "
                + "ideal ground to be a corridor at all, and cannot be free.");

        int size = terrain.Size / stride;
        var nodeCost = new double[size * size];
        var passable = new bool[size * size];
        ReadOnlySpan<double> cost = terrain.MovementCost;
        ReadOnlySpan<double> water = terrain.Water;
        ReadOnlySpan<double> rivers = terrain.Rivers;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double costSum = 0.0, waterSum = 0.0;
                int riverCells = 0;
                for (int by = 0; by < stride; by++)
                {
                    int row = (y * stride + by) * terrain.Size + x * stride;
                    for (int bx = 0; bx < stride; bx++)
                    {
                        costSum += cost[row + bx];
                        waterSum += water[row + bx];
                        if (rivers[row + bx] >= 0.5) riverCells++;
                    }
                }
                int cells = stride * stride;
                int node = y * size + x;
                double blockMean = costSum / cells;
                // T4.7: path-length weighted river blend. riverCells == 0 takes the
                // untouched pre-T4.7 expression — bit-identical on riverless worlds.
                nodeCost[node] = riverCells == 0
                    ? blockMean
                    : Blend(blockMean, RiverSpan(riverCells, stride), riverCostFactor);
                passable[node] = waterSum / cells < 0.5;
            }
        }
        return new TraversalLattice(size, stride, terrain.KmPerPx * stride, nodeCost, passable);
    }

    /// <summary>
    /// The fraction of a block crossing that can be made ON the river:
    /// riverCells / stride, saturating at 1 (a meandering river can hold more
    /// than `stride` cells in a block, but a crossing is still one crossing).
    /// Pure and public so tests reproduce the aggregation without duplicating it.
    /// </summary>
    public static double RiverSpan(int riverCells, int stride) =>
        riverCells >= stride ? 1.0 : (double)riverCells / stride;

    /// <summary>The T4.7 cost blend: (1−span)·land + span·river.</summary>
    public static double Blend(double blockMeanCost, double span, double riverCostFactor) =>
        (1.0 - span) * blockMeanCost + span * riverCostFactor;

    /// <summary>Test factory: a handcrafted lattice with explicit costs/passability.</summary>
    public static TraversalLattice FromCosts(int size, double[] nodeCost, bool[] passable, double kmPerNode = 1.0)
    {
        if (nodeCost.Length != size * size || passable.Length != size * size)
            throw new ArgumentException("nodeCost and passable must be size*size long");
        return new TraversalLattice(size, 1, kmPerNode, nodeCost, passable);
    }

    /// <summary>
    /// Cost of one lattice step a→b (must be 8-neighbors): mean node cost times
    /// step length in stride units (1 cardinal, DiagonalFactor diagonal).
    /// </summary>
    public double StepCost(int a, int b)
    {
        (int ax, int ay) = Coords(a);
        (int bx, int by) = Coords(b);
        double length = ax != bx && ay != by ? DiagonalFactor : 1.0;
        return (_nodeCost[a] + _nodeCost[b]) * 0.5 * length;
    }
}
