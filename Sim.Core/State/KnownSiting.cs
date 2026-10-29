using Sim.Core.Pathing;
using Sim.Core.Worldgen;

namespace Sim.Core.State;

/// <summary>
/// D-066 A18 (M6 closure, track FOG; closes the channel ADR-037 §6 and ADR-036 §8.3 recorded) — THE SITING VALUATION OVER
/// WHAT A POLITY KNOWS. <see cref="SettlementSiting.CandidateScores"/> values a cell by region fertility (a box sum over
/// ±fertilityRadiusPx) × water access (a BFS to the nearest water or river cell, falling to 0 at waterAccessCutoffPx) and
/// floors the candidates at a percentile of every positive score of the map — so terrain under nodes a polity has never
/// discovered moves the score of a cell it knows, the floor, and the pick between known cells. This is the SAME valuation
/// read through the knowledge seam (<see cref="PolityKnowledge"/>): what the polity has not discovered contributes NOTHING
/// (the simplest assumption, and the one that cannot leak — an unknown cell is never read):
/// <list type="bullet">
/// <item><b>Region fertility</b>: the (2r+1)² window of <see cref="SettlementSiting.BoxSum"/> (y clamped; x modular on a
///   cylinder, a window as wide as the map counting each column once; clamped on a flat map), summed DIRECTLY over its
///   known cells in row-major order — unknown cells add 0. (A direct sum, not the integral image: an integral image's low
///   bits depend on every cell above-left of the window, which is itself a channel — ADR-037 §8.)</item>
/// <item><b>Water access</b>: the BFS distance to the nearest KNOWN water or river cell (water ≥ 0.5 or river ≥ 0.5, as
///   <see cref="SettlementSiting.WaterDistanceBfs"/> seeds), over the unobstructed 4-neighbour grid (the same metric; a
///   step through an unknown cell reads nothing there), capped at the cutoff beyond which access is 0 anyway.</item>
/// <item><b>The score</b>: region × <see cref="SettlementSiting.WaterAccess"/> for every KNOWN land cell (water &lt; 0.5);
///   0 for water and for every unknown cell.</item>
/// <item><b>The floor</b>: <see cref="SettlementSiting.ScoreFloor"/> (the same percentile) over those scores — the positive
///   scores of the cells the polity knows.</item>
/// <item><b>Known nodes</b>: the lattice nodes the seam answers known — what the AI's reachability flood may cross
///   (<see cref="Pathfinder.ReachableWithin"/>).</item>
/// </list>
/// With every cell known the components are the global ones (the water distance capped where the global is past the
/// cutoff, which values identically; the region sum may differ from the integral image in its last bits), but the AI does
/// not use this class then: <see cref="Systems.Colonization.AiFoundingPolicy"/> asks for it only when the seam restricts
/// something, so a fog-off world is byte-identical to the pre-A18 world (pinned). Pure: reads the terrain and the seam,
/// writes nothing, caches nothing, no floating-point order dependence beyond the fixed row-major sums (law 5).
/// </summary>
public sealed class KnownSiting
{
    /// <summary>Per terrain cell: the known-terrain score (0 for water and for an unknown cell).</summary>
    public required double[] Scores { get; init; }

    /// <summary>The candidate floor over <see cref="Scores"/> (the siting percentile of the positive known scores).</summary>
    public required double Floor { get; init; }

    /// <summary>Per lattice node (the traversal lattice's — the seam's grid): whether the polity knows it.</summary>
    public required bool[] KnownNodes { get; init; }

    /// <summary>The valuation of <paramref name="terrain"/> under <paramref name="siting"/> through <paramref name="knowledge"/>.</summary>
    public static KnownSiting Of(TerrainSet terrain, SitingConfig siting, PolityKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(siting);
        ArgumentNullException.ThrowIfNull(knowledge);
        int size = terrain.Size, cells = size * size;
        bool wrap = terrain.WrapX;
        var known = new bool[cells];
        for (int c = 0; c < cells; c++) known[c] = knowledge.KnowsCell(c);

        // Water access: a multi-source BFS from the KNOWN water and river cells, capped at the cutoff.
        ReadOnlySpan<double> water = terrain.Water, rivers = terrain.Rivers, fertility = terrain.Fertility;
        int cutoff = siting.WaterAccessCutoffPx;
        var dist = new int[cells];
        Array.Fill(dist, int.MaxValue);
        var queue = new int[cells];
        int head = 0, tail = 0;
        bool hasRivers = rivers.Length == cells;
        for (int c = 0; c < cells; c++)
            if (known[c] && (water[c] >= 0.5 || (hasRivers && rivers[c] >= 0.5))) { dist[c] = 0; queue[tail++] = c; }
        while (head < tail)
        {
            int i = queue[head++];
            int d = dist[i] + 1;
            if (d >= cutoff) continue;   // access is 0 from the cutoff on: no farther distance is ever read
            int x = i % size, y = i / size;
            int w = MapTopology.NeighbourX(x, -1, size, wrap), e = MapTopology.NeighbourX(x, 1, size, wrap);
            if (w >= 0 && dist[y * size + w] == int.MaxValue) { dist[y * size + w] = d; queue[tail++] = y * size + w; }
            if (e >= 0 && dist[y * size + e] == int.MaxValue) { dist[y * size + e] = d; queue[tail++] = y * size + e; }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue[tail++] = i - size; }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue[tail++] = i + size; }
        }

        int r = siting.FertilityRadiusPx;
        bool full = wrap && 2L * r + 1 >= size;
        var scores = new double[cells];
        for (int c = 0; c < cells; c++)
        {
            if (!known[c] || water[c] >= 0.5 || dist[c] == int.MaxValue) continue;
            double access = SettlementSiting.WaterAccess(dist[c], cutoff);
            if (access <= 0.0) continue;
            scores[c] = Region(fertility, known, size, wrap, full, r, c % size, c / size) * access;
        }

        (int l, _) = PolityKnowledge.Grid(size);
        var nodes = new bool[l * l];
        for (int n = 0; n < nodes.Length; n++) nodes[n] = knowledge.Known(n);
        return new KnownSiting
        {
            Scores = scores,
            Floor = SettlementSiting.ScoreFloor(scores, siting.ScoreFloorPercentile),
            KnownNodes = nodes,
        };
    }

    /// <summary>The region fertility of (x, y): the known cells of BoxSum's window, summed row-major.</summary>
    private static double Region(ReadOnlySpan<double> fertility, bool[] known, int size, bool wrap, bool full, int r, int x, int y)
    {
        if (r <= 0) return known[y * size + x] ? fertility[y * size + x] : 0.0;
        int y0 = Math.Max(0, y - r), y1 = Math.Min(size - 1, y + r);
        double sum = 0.0;
        for (int yy = y0; yy <= y1; yy++)
        {
            int row = yy * size;
            if (full)
            {
                for (int xx = 0; xx < size; xx++) if (known[row + xx]) sum += fertility[row + xx];
            }
            else if (!wrap)
            {
                for (int xx = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r); xx <= x1; xx++)
                    if (known[row + xx]) sum += fertility[row + xx];
            }
            else
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int xx = MapTopology.NeighbourX(x, dx, size, wrap: true);
                    if (known[row + xx]) sum += fertility[row + xx];
                }
            }
        }
        return sum;
    }

    /// <summary>The known-terrain half of <see cref="SettlementSiting.IsFrontierCandidate"/>: the cell is on the map, KNOWN,
    /// land, and its known score is &gt; 0 and not below the known floor.</summary>
    public bool IsCandidate(TerrainSet terrain, int cell)
    {
        if ((uint)cell >= (uint)Scores.Length) return false;
        double score = Scores[cell];
        if (!(score > 0.0)) return false;              // unknown cells and water score 0
        if (terrain.Water[cell] >= 0.5) return false;  // read only for a cell with a known score
        return !(score < Floor);
    }
}
