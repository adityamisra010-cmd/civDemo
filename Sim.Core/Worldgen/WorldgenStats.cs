namespace Sim.Core.Worldgen;

/// <summary>Diagnostic statistics over a TerrainSet (CLI --stats and acceptance tests).</summary>
public static class WorldgenStats
{
    /// <summary>
    /// Point-biserial correlation between "within <paramref name="adjacencyRadiusPx"/>
    /// (Chebyshev) of a river cell" and fertility, over LAND cells — the T1.2
    /// "fertility visibly river-correlated" acceptance number.
    /// </summary>
    public static double FertilityRiverCorrelation(TerrainSet terrain, int adjacencyRadiusPx)
    {
        int size = terrain.Size;
        int cells = size * size;
        ReadOnlySpan<double> water = terrain.Water;
        ReadOnlySpan<double> rivers = terrain.Rivers;
        ReadOnlySpan<double> fertility = terrain.Fertility;

        // Bounded Chebyshev distance to rivers (same construction as the boost).
        var near = new bool[cells];
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;
        for (int i = 0; i < cells; i++)
        {
            if (rivers[i] >= 0.5) { dist[i] = 0; near[i] = true; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }
        while (head < tail)
        {
            int i = queue[head++];
            int d = dist[i] + 1;
            if (d > adjacencyRadiusPx) continue;
            int x = i % size, y = i / size;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int nb = ny * size + nx;
                if (dist[nb] == int.MaxValue) { dist[nb] = d; near[nb] = true; queue[tail++] = nb; }
            }
        }

        // Point-biserial r = (mean1 - mean0) / sd * sqrt(p*(1-p)) over land cells.
        long n0 = 0, n1 = 0;
        double sum0 = 0, sum1 = 0, sumAll = 0, sumSqAll = 0;
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) continue;
            double f = fertility[i];
            sumAll += f;
            sumSqAll += f * f;
            if (near[i]) { n1++; sum1 += f; }
            else { n0++; sum0 += f; }
        }
        long n = n0 + n1;
        if (n == 0 || n0 == 0 || n1 == 0) return 0.0;

        double mean = sumAll / n;
        double variance = sumSqAll / n - mean * mean;
        if (variance <= 0) return 0.0;
        double p = n1 / (double)n;
        return (sum1 / n1 - sum0 / n0) / Math.Sqrt(variance) * Math.Sqrt(p * (1.0 - p));
    }
}
