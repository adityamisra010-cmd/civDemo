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
