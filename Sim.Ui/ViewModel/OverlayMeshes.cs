using Sim.Core.Pathing;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// World-space overlay geometry (T1.8, pure view-model — testable headless).
/// Everything is built FROM WorldState reads; the sim never sees any of it.
/// Rebuilt only when the underlying state changes (the Game keys path geometry
/// on the network revision and catchment geometry on LastRecomputeTurn — cheap
/// UI-side caching, outside the determinism surface).
/// </summary>
public static class OverlayMeshes
{
    /// <summary>TUNE: built-path render width in world (terrain px) units.</summary>
    public const double PathWidthWorldPx = 1.6;

    /// <summary>World-space center of a LATTICE node (stride×stride block center).</summary>
    public static LineGeometry.Vertex LatticeNodeCenter(int node, int latticeSize, int stride) =>
        new(node % latticeSize * stride + stride / 2.0,
            node / latticeSize * stride + stride / 2.0);

    /// <summary>
    /// Quad-strip triangle list for every built network edge — the dirt paths,
    /// drawn in an earth color the player can never confuse with river blue.
    /// Edges resolve through their anchor nodes' lattice positions.
    /// </summary>
    public static LineGeometry.Vertex[] BuildPaths(
        IReadOnlyWorldState world, int latticeSize, int stride)
    {
        // Anchor lookup: network node id → lattice node (ids are table order).
        int maxId = -1;
        for (int i = 0; i < world.NetworkNodes.Count; i++)
            maxId = Math.Max(maxId, world.NetworkNodes[i].Id.Value);
        var anchor = new int[maxId + 1];
        for (int i = 0; i < world.NetworkNodes.Count; i++)
            anchor[world.NetworkNodes[i].Id.Value] = world.NetworkNodes[i].LatticeNode;

        var vertices = new List<LineGeometry.Vertex>();
        for (int i = 0; i < world.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow edge = world.NetworkEdges[i];
            LineGeometry.AppendSegmentQuad(vertices,
                LatticeNodeCenter(anchor[edge.A.Value], latticeSize, stride),
                LatticeNodeCenter(anchor[edge.B.Value], latticeSize, stride),
                PathWidthWorldPx / 2.0);
        }
        return [.. vertices];
    }

    /// <summary>
    /// Translucent-fill triangle list over every catchment-reached lattice
    /// node: one stride×stride world-space quad per CatchmentNodeRow (all
    /// settlements). Six vertices per node block.
    /// </summary>
    public static LineGeometry.Vertex[] BuildCatchmentFill(
        IReadOnlyWorldState world, int latticeSize, int stride)
    {
        var vertices = new List<LineGeometry.Vertex>(world.CatchmentNodes.Count * 6);
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            int node = world.CatchmentNodes[i].LatticeNode;
            double x0 = node % latticeSize * stride, y0 = node / latticeSize * stride;
            double x1 = x0 + stride, y1 = y0 + stride;
            vertices.Add(new(x0, y0)); vertices.Add(new(x1, y0)); vertices.Add(new(x0, y1));
            vertices.Add(new(x0, y1)); vertices.Add(new(x1, y0)); vertices.Add(new(x1, y1));
        }
        return [.. vertices];
    }

    /// <summary>
    /// T2.4: the PARTITION as geometry — one triangle list PER SETTLEMENT
    /// (indexed by settlement row order), each covering exactly the lattice
    /// nodes that settlement owns in the claim table. Because the partition
    /// claims every node at most once (T2.3, by construction), the meshes are
    /// disjoint and meet at clean block boundaries. The renderer tints each
    /// with the settlement's palette color.
    /// </summary>
    public static LineGeometry.Vertex[][] BuildTerritoryFills(
        IReadOnlyWorldState world, int latticeSize, int stride)
    {
        var lists = new List<LineGeometry.Vertex>[world.Settlements.Count];
        for (int s = 0; s < lists.Length; s++) lists[s] = [];

        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            int settlementIndex = -1;
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                if (world.Settlements[s].Id == row.Settlement) { settlementIndex = s; break; }
            }
            if (settlementIndex < 0) continue; // orphan claim row — never rendered

            int node = row.LatticeNode;
            double x0 = node % latticeSize * stride, y0 = node / latticeSize * stride;
            double x1 = x0 + stride, y1 = y0 + stride;
            List<LineGeometry.Vertex> v = lists[settlementIndex];
            v.Add(new(x0, y0)); v.Add(new(x1, y0)); v.Add(new(x0, y1));
            v.Add(new(x0, y1)); v.Add(new(x1, y0)); v.Add(new(x1, y1));
        }

        var result = new LineGeometry.Vertex[lists.Length][];
        for (int s = 0; s < lists.Length; s++) result[s] = [.. lists[s]];
        return result;
    }

    /// <summary>World-space position of a settlement's site cell center.</summary>
    public static LineGeometry.Vertex SettlementPosition(SettlementRow settlement, int terrainSize) =>
        new(settlement.SiteCell % terrainSize + 0.5, settlement.SiteCell / terrainSize + 0.5);

    /// <summary>The stride the M1 systems build their lattice with (TraversalLattice default).</summary>
    public static int LatticeStride(TraversalLattice lattice, int terrainSize) => terrainSize / lattice.Size;
}
