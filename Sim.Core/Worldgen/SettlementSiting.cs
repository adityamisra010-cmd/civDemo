namespace Sim.Core.Worldgen;

/// <summary>
/// Deterministic settlement siting (T1.4): argmax over land cells of
/// fertility × water-access, where water-access falls linearly from 1 at the
/// shoreline to 0 at the TUNE cutoff (siting.waterAccessCutoffPx).
///
/// DETERMINISM: the argmax uses the composite key (score DESC, cell id ASC) —
/// a total order, per the constitution's tie-break rule. Scanning cells in
/// ascending id with a strictly-greater comparison implements exactly that key:
/// among equal scores the lowest id wins. The tie-dense siting test pins this.
///
/// Pure function of its inputs — no state, no caches (T1.3 purity mandate).
/// </summary>
public static class SettlementSiting
{
    /// <summary>Chooses the site cell for a new settlement on this terrain.</summary>
    public static int ChooseSite(TerrainSet terrain, SitingConfig cfg) =>
        ChooseSite(terrain.Fertility, terrain.Water, terrain.Size, cfg.WaterAccessCutoffPx);

    /// <summary>
    /// D-025 (T2.3): iterative top-score siting of <paramref name="count"/>
    /// settlements with a minimum TRAVEL-TIME spacing between accepted sites.
    /// Each pick is the argmax by the composite key (score DESC, cell id ASC)
    /// over cells still eligible; after each acceptance the spacing exclusion
    /// grows. TWO-STAGE spacing check (documented shape):
    ///   1. PREFILTER — a spacing-budget-CAPPED Dijkstra from each accepted
    ///      site's lattice origin: any node the capped expansion never reaches
    ///      is trivially far enough (travel &gt; MinSpacingTravel), so the
    ///      exact check touches only the local region around accepted sites
    ///      (the straight-line px reasoning, made exact by the cap).
    ///   2. EXACT — a cell is blocked iff its lattice node's min travel cost
    ///      from any accepted site is &lt; MinSpacingTravel.
    /// Sites are returned in PICK ORDER — settlement id i is the i-th pick
    /// (best first), the deterministic identity every consumer relies on.
    /// Throws when the terrain cannot host <paramref name="count"/> sites at
    /// this spacing (an actionable config/terrain mismatch, never a silent
    /// shortfall).
    /// </summary>
    public static int[] ChooseSites(TerrainSet terrain, SitingConfig cfg, int count)
    {
        int size = terrain.Size;
        ReadOnlySpan<double> fertility = terrain.Fertility;
        ReadOnlySpan<double> water = terrain.Water;
        int[] waterDistance = WaterDistanceBfs(water, size);
        var lattice = Pathing.TraversalLattice.Build(terrain);

        // Min travel cost from any accepted site, per lattice node (∞ start).
        var spacing = new double[lattice.NodeCount];
        Array.Fill(spacing, double.PositiveInfinity);

        var sites = new int[count];
        for (int pick = 0; pick < count; pick++)
        {
            int bestCell = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < fertility.Length; i++)
            {
                if (water[i] >= 0.5) continue; // land candidates only
                double access = 1.0 - waterDistance[i] / (double)cfg.WaterAccessCutoffPx;
                if (access < 0.0) access = 0.0;
                double score = fertility[i] * access;
                // Strictly-greater on an ascending-id scan = (score DESC, id ASC).
                // The spacing test runs only on strict improvements (the
                // lattice-origin lookup is the expensive part); a blocked
                // improver is skipped WITHOUT raising bestScore, so the pick
                // remains the true argmax over eligible cells.
                if (score <= bestScore) continue;
                if (pick > 0)
                {
                    int node = Pathing.LatticeMap.OriginLatticeNode(lattice, size, i);
                    if (spacing[node] < cfg.MinSpacingTravel) continue; // stage-2 exact check
                }
                bestScore = score;
                bestCell = i;
            }
            if (bestCell < 0)
                throw new InvalidOperationException(
                    $"settlement siting could only place {pick} of {count} sites at " +
                    $"minSpacingTravel {cfg.MinSpacingTravel.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    "— terrain too small or spacing too large.");
            sites[pick] = bestCell;

            // Stage-1 prefilter: relax the spacing field with a capped Dijkstra
            // from the new site (no network overlay — spacing is a worldgen
            // property of raw terrain).
            int origin = Pathing.LatticeMap.OriginLatticeNode(lattice, size, bestCell);
            Pathing.Pathfinder.RelaxCappedFrom(lattice, origin, cfg.MinSpacingTravel, spacing);
        }
        return sites;
    }

    /// <summary>
    /// Primitive overload (test surface for the tie-dense proof): fertility and
    /// water rasters as flat row-major spans. Returns the chosen cell id.
    /// Candidates are LAND cells only (water ≥ 0.5 is excluded), so the result
    /// is on land even if every score is 0. Throws if the raster has no land.
    /// </summary>
    public static int ChooseSite(
        ReadOnlySpan<double> fertility, ReadOnlySpan<double> water, int size, int waterAccessCutoffPx)
    {
        if (fertility.Length != size * size || water.Length != size * size)
            throw new ArgumentException("fertility and water must be size*size long");

        int[] waterDistance = WaterDistanceBfs(water, size);

        int bestCell = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < fertility.Length; i++)
        {
            if (water[i] >= 0.5) continue; // land candidates only
            double access = 1.0 - waterDistance[i] / (double)waterAccessCutoffPx;
            if (access < 0.0) access = 0.0;
            double score = fertility[i] * access;
            // Strictly-greater on an ascending-id scan = (score DESC, id ASC).
            if (score > bestScore)
            {
                bestScore = score;
                bestCell = i;
            }
        }

        return bestCell >= 0 ? bestCell
            : throw new InvalidOperationException(
                "settlement siting found no land cell — terrain is all water.");
    }

    /// <summary>
    /// Multi-source BFS grid distance (px steps) from every water cell — the
    /// same fixed expansion order (sources row-major; W, E, N, S) as worldgen's
    /// moisture BFS, restated here because that helper is private to its stage.
    /// Cells unreachable from water (no water at all) keep int.MaxValue.
    /// </summary>
    private static int[] WaterDistanceBfs(ReadOnlySpan<double> water, int size)
    {
        int cells = size * size;
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;

        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) { dist[i] = 0; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }

        while (head < tail)
        {
            int i = queue[head++];
            int x = i % size, y = i / size, d = dist[i] + 1;
            if (x > 0 && dist[i - 1] == int.MaxValue) { dist[i - 1] = d; queue[tail++] = i - 1; }
            if (x < size - 1 && dist[i + 1] == int.MaxValue) { dist[i + 1] = d; queue[tail++] = i + 1; }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue[tail++] = i - size; }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue[tail++] = i + size; }
        }
        return dist;
    }
}
