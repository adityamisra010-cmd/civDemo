using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tests.Spatial;

internal static class SpatialTestSupport
{
    public static TerrainCostTable Costs => TerrainCostTable.PlaceholderTestTable();
    public static ConcealmentTable Conceal => ConcealmentTable.PlaceholderTestTable();

    public static long R(ulong seed, long i, long j, long n) => (long)CounterRng.NextBelow(seed, 7, i, j, 0, 0, (ulong)n);

    /// <summary>Random grid: mixed passable terrain plus ~wallPct% Wall and some Water (counter RNG, deterministic).</summary>
    public static TerrainGrid RandomGrid(int w, int h, ulong seed, int wallPct, long cellSize = TerrainGrid.DefaultCellSizeMm)
    {
        var g = new TerrainGrid(w, h, cellSize);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                long r = R(seed, x, y, 100);
                TerrainClass c = r < wallPct ? TerrainClass.Wall
                    : r < wallPct + 3 ? TerrainClass.Water
                    : (TerrainClass)R(seed + 1, x, y, 6);           // Road..Mountain
                g.Set(x, y, c, 0);
            }
        return g;
    }

    /// <summary>Exact brute-force oracle of the supercover DEFINITION (membership of dense exact rational samples + corner rule).</summary>
    public static HashSet<(long, long)> Oracle(long S, Vec2L a, Vec2L b)
    {
        var set = new HashSet<(long, long)>();
        long dx = b.X - a.X, dy = b.Y - a.Y;
        long N = 2 * Math.Max(Math.Abs(dx), 1) * Math.Max(Math.Abs(dy), 1);
        for (long k = 0; k <= N; k++)
        {
            long xn = a.X * N + dx * k, yn = a.Y * N + dy * k;          // point = (xn/N, yn/N)
            set.Add((FloorDiv(xn, N * S), FloorDiv(yn, N * S)));
            if (dx != 0 && dy != 0 && (dx * k) % N == 0 && (dy * k) % N == 0)
            {
                long X = a.X + dx * k / N, Y = a.Y + dy * k / N;
                if (X % S == 0 && Y % S == 0)
                {
                    long cx = X / S, cy = Y / S;
                    set.Add((cx, cy)); set.Add((cx - 1, cy)); set.Add((cx, cy - 1)); set.Add((cx - 1, cy - 1));
                }
            }
        }
        return set;
    }

    public static long FloorDiv(long a, long b) { long q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q; }

    public static HashSet<(long, long)> CellSet(List<CoverCell> cells)
    {
        var s = new HashSet<(long, long)>();
        foreach (var c in cells) s.Add((c.X, c.Y));
        return s;
    }

    /// <summary>Independent Dijkstra (O(N^2), no heap, no heuristic) with the same step-cost rules, for optimality checks.</summary>
    public static long DijkstraCost(TerrainGrid g, TerrainCostTable costs, CellCoord s, CellCoord t)
    {
        int n = g.CellCount, W = g.Width;
        var dist = new long[n]; var done = new bool[n];
        Array.Fill(dist, long.MaxValue);
        int si = g.Index(s.X, s.Y), ti = g.Index(t.X, t.Y);
        if (!g.IsPassable(costs, s.X, s.Y) || !g.IsPassable(costs, t.X, t.Y)) return -1;
        dist[si] = 0;
        long S = g.CellSizeMm;
        while (true)
        {
            int u = -1;
            for (int i = 0; i < n; i++) if (!done[i] && dist[i] != long.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0) return -1;
            if (u == ti) return dist[u];
            done[u] = true;
            int x = u % W, y = u / W;
            for (int ddy = -1; ddy <= 1; ddy++)
                for (int ddx = -1; ddx <= 1; ddx++)
                {
                    if (ddx == 0 && ddy == 0) continue;
                    int nx = x + ddx, ny = y + ddy;
                    if (!g.IsPassable(costs, nx, ny)) continue;
                    long ca = costs.Cost(g.ClassAt(x, y)), cb = costs.Cost(g.ClassAt(nx, ny));
                    long step;
                    if (ddx != 0 && ddy != 0)
                    {
                        if (!g.IsPassable(costs, nx, y) || !g.IsPassable(costs, x, ny)) continue;
                        step = (ca + cb) * S * 1414 / 2000;
                    }
                    else step = (ca + cb) * S / 2;
                    int v = ny * W + nx;
                    if (dist[u] + step < dist[v]) dist[v] = dist[u] + step;
                }
        }
    }
}
