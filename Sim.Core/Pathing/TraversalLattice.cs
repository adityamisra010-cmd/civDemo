using Sim.Core.Worldgen;

namespace Sim.Core.Pathing;

/// <summary>
/// The traversal lattice (m1 spec §3): a stride-4 sampling of the movement-cost
/// field — 256² nodes at the 1024² default. An UNDER-THE-HOOD grid per D-012's
/// precedent: never design-visible, never a sim unit of place. All pathfinding
/// and isochrones run here; network edges overlay as fast lanes at their
/// endpoints' lattice nodes.
///
/// Derived, immutable, rebuildable: a pure function of (TerrainSet, stride) —
/// not state, not a cache. Node ids are row-major and stable. Node cost is the
/// AVERAGE movement-cost over the node's stride×stride terrain block (documented
/// choice: block averaging is stable and representative; point sampling would
/// alias single-pixel features). A node is impassable (water) when the block's
/// water fraction is ≥ 0.5.
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

    public static TraversalLattice Build(TerrainSet terrain, int stride = 4)
    {
        int size = terrain.Size / stride;
        var nodeCost = new double[size * size];
        var passable = new bool[size * size];
        ReadOnlySpan<double> cost = terrain.MovementCost;
        ReadOnlySpan<double> water = terrain.Water;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double costSum = 0.0, waterSum = 0.0;
                for (int by = 0; by < stride; by++)
                {
                    int row = (y * stride + by) * terrain.Size + x * stride;
                    for (int bx = 0; bx < stride; bx++)
                    {
                        costSum += cost[row + bx];
                        waterSum += water[row + bx];
                    }
                }
                int cells = stride * stride;
                int node = y * size + x;
                nodeCost[node] = costSum / cells;
                passable[node] = waterSum / cells < 0.5;
            }
        }
        return new TraversalLattice(size, stride, terrain.KmPerPx * stride, nodeCost, passable);
    }

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
