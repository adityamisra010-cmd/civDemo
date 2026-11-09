namespace Gfx.Proc.Authoritative;

/// <summary>Side outputs of a generation run (for tests and presentation; NOT part of the hashed state).</summary>
public sealed class GenerationTrace
{
    /// <summary>River centre-line cells, source → mouth (empty without a river).</summary>
    public int[] RiverCells { get; internal set; } = Array.Empty<int>();
    /// <summary>River water-surface height (mm) at each centre-line cell; non-increasing by construction.</summary>
    public int[] RiverSurfaceMm { get; internal set; } = Array.Empty<int>();
    public int RoadCellCount { get; internal set; }
}

/// <summary>
/// Deterministic battlefield downscale: integer inputs → <see cref="BattleTerrainGrid"/>. INTEGER ONLY (scanned).
/// Pipeline: fBm + ridged relief (amplitude from movement cost) → coast shelf → thermal erosion (exact mass
/// conservation) → river by least-cost path with a non-increasing surface (drop per step capped) → talus slope limit
/// (lower-only) → sea flooding → road by greedy
/// least-climb walk → class assignment (water, road, beach, slope classes, vegetation by moisture-gated noise).
/// </summary>
public static class BattleTerrainGenerator
{
    const ulong SaltRelief = 0x1000, SaltRidge = 0x2000, SaltMacro = 0x3000, SaltCoast = 0x4000,
        SaltRiver = 0x5000, SaltRoad = 0x6000, SaltVeg = 0x7000;
    public const int ErosionPasses = 6;
    /// <summary>River channel depth below its surface (mm) and surface drop below the bank line (mm).</summary>
    public const int RiverDepthMm = 1500, RiverFreeboardMm = 500;
    /// <summary>Largest river-surface drop between consecutive centre-line cells (mm).</summary>
    public const int RiverMaxDropMm = 100;
    /// <summary>Valley-side ramp (cells) beside the channel, capped at the talus slope.</summary>
    const int BankRamp = 6;

    /// <summary>Talus step (mm) between 4-neighbour cells: 0.6 × the cell size (about 31 degrees).</summary>
    public static int TalusMm(int cellMm) => cellMm * 6 / 10;

    public static BattleTerrainGrid Generate(in BattleTerrainInputs inp) => Generate(inp, out _);

    public static BattleTerrainGrid Generate(in BattleTerrainInputs inp, out GenerationTrace trace)
    {
        inp.Validate();
        trace = new GenerationTrace();
        int W = inp.Width, H = inp.Height, cell = inp.CellMm;
        bool coastal = inp.CoastEdge >= 0;
        ulong seed = IntNoise.SplitMix64(inp.WorldSeed ^ IntNoise.SplitMix64(0xC0FFEE00UL + (ulong)(uint)inp.CampaignCell));
        var g = new BattleTerrainGrid(W, H, cell, inp.ElevationM * 1000L, 0, inp.Biome, seed);
        int[] h = g.HeightMm;

        // 1. relief
        int amp = Math.Clamp(6000 + (inp.MoveCostPermille - 1000) * 30, 3000, 90000);
        int ridgeShare = Math.Clamp((inp.MoveCostPermille - 1500) / 2, 0, 700);
        int lift = coastal ? amp + 3000 : 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                long b = IntNoise.Fbm(seed ^ SaltRelief, x, y, 128, 6);
                long r = ridgeShare > 0 ? IntNoise.Ridged(seed ^ SaltRidge, x, y, 96, 4) : 32768;
                long n = (b * (1000 - ridgeShare) + r * ridgeShare) / 1000;
                long macro = IntNoise.Value(seed ^ SaltMacro, x, y, 256) - 32768L;
                h[g.Index(x, y)] = (int)((n - 32768) * amp / 32768 + macro * amp / 65536) + lift;
            }

        // 2. coast shelf: blend toward a -6 m sea floor over a noisy band along the coast edge
        if (coastal)
        {
            int span = inp.CoastEdge is 0 or 2 ? H : W;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var (d, a) = EdgeCoords(inp.CoastEdge, x, y, W, H);
                    int c = span * 28 / 100 + (int)((IntNoise.Value(seed ^ SaltCoast, a, 0, 40) - 32768L) * span / (65536L * 8));
                    if (d >= c) continue;
                    int i = g.Index(x, y);
                    const long floor = -6000;
                    h[i] = (int)(floor + (h[i] - floor) * d * d / ((long)c * c));
                }
        }

        // 3. thermal erosion
        int talus = TalusMm(cell);
        ThermalErode(h, W, H, talus, ErosionPasses);

        // 4. river
        var surface = new int[W * H];
        Array.Fill(surface, int.MaxValue);
        if (inp.River) CarveRiver(g, inp, seed, coastal, talus, surface, trace);
        // 4b. hold every 4-neighbour step to the talus (lower-only), so a channel cut through higher ground becomes a
        // talus-sided valley instead of a one-cell cliff (verification finding 1).
        LimitSlope(h, W, H, talus);

        // 5. water depth: river surfaces, then the sea
        for (int i = 0; i < h.Length; i++)
        {
            int d = surface[i] != int.MaxValue ? surface[i] - h[i] : 0;
            if (coastal) d = Math.Max(d, g.WaterLevelMm - h[i]);
            g.WaterDepthMm[i] = Math.Max(0, d);
        }

        // 6. road
        var road = new bool[W * H];
        trace.RoadCellCount = LayRoad(g, inp, seed, coastal, road);

        // 7. classes
        int thr = 32768 + (700 - inp.MoisturePermille) * 30;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = g.Index(x, y);
                TerrainClass k;
                if (g.WaterDepthMm[i] > 0) k = TerrainClass.Water;
                else if (road[i]) k = TerrainClass.Road;
                else
                {
                    int s = 0, hi = h[i];
                    s = Math.Max(s, Math.Abs(hi - g.ClampedHeight(x + 1, y)));
                    s = Math.Max(s, Math.Abs(hi - g.ClampedHeight(x - 1, y)));
                    s = Math.Max(s, Math.Abs(hi - g.ClampedHeight(x, y + 1)));
                    s = Math.Max(s, Math.Abs(hi - g.ClampedHeight(x, y - 1)));
                    if (coastal && hi - g.WaterLevelMm < 1500 && s < cell / 5) k = TerrainClass.Beach;
                    else if (s >= cell * 55 / 100) k = TerrainClass.Mountain;
                    else if (s >= cell / 4) k = TerrainClass.Hills;
                    else k = inp.Biome == Biome.Arid ? TerrainClass.Desert : TerrainClass.Open;
                    if ((k == TerrainClass.Open || k == TerrainClass.Hills) && inp.Biome != Biome.Arid
                        && IntNoise.Fbm(seed ^ SaltVeg, x, y, 48, 4) > thr)
                        k = inp.Biome == Biome.Tropical ? TerrainClass.Jungle : TerrainClass.Forest;
                }
                g.Class[i] = k;
            }
        return g;
    }

    /// <summary>(distance from edge, coordinate along edge) in cells.</summary>
    static (int d, int a) EdgeCoords(int edge, int x, int y, int W, int H) => edge switch
    {
        0 => (y, x),
        1 => (W - 1 - x, y),
        2 => (H - 1 - y, x),
        _ => (x, y),
    };

    /// <summary>
    /// Integer thermal erosion (Jacobi): every 4-neighbour pair whose height difference exceeds <paramref name="talus"/>
    /// moves a quarter of the excess downhill. Exactly mass-conserving (sum of heights unchanged), order-independent.
    /// </summary>
    public static void ThermalErode(int[] h, int W, int H, int talus, int passes)
    {
        var d = new int[h.Length];
        for (int p = 0; p < passes; p++)
        {
            Array.Clear(d);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (x + 1 < W) Pair(h, d, i, i + 1, talus);
                    if (y + 1 < H) Pair(h, d, i, i + W, talus);
                }
            for (int i = 0; i < h.Length; i++) h[i] += d[i];
        }
    }

    static void Pair(int[] h, int[] d, int a, int b, int talus)
    {
        int diff = h[a] - h[b];
        if (diff > talus) { int m = (diff - talus) / 4; d[a] -= m; d[b] += m; }
        else if (-diff > talus) { int m = (-diff - talus) / 4; d[b] -= m; d[a] += m; }
    }

    /// <summary>
    /// Lower-only slope limit: h[i] = min over j of (h[j] + talus × manhattan(i, j)), computed exactly by one forward and
    /// one backward raster sweep. Afterwards no 4-neighbour step exceeds <paramref name="talus"/>. Deterministic.
    /// </summary>
    public static void LimitSlope(int[] h, int W, int H, int talus)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (x > 0) h[i] = Math.Min(h[i], h[i - 1] + talus);
                if (y > 0) h[i] = Math.Min(h[i], h[i - W] + talus);
            }
        for (int y = H - 1; y >= 0; y--)
            for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x;
                if (x + 1 < W) h[i] = Math.Min(h[i], h[i + 1] + talus);
                if (y + 1 < H) h[i] = Math.Min(h[i], h[i + W] + talus);
            }
    }

    static void CarveRiver(BattleTerrainGrid g, in BattleTerrainInputs inp, ulong seed, bool coastal, int talus, int[] surface, GenerationTrace trace)
    {
        int W = g.Width, H = g.Height; int[] h = g.HeightMm;
        int srcEdge = coastal ? (inp.CoastEdge + 2) % 4 : 0;
        int dstEdge = coastal ? inp.CoastEdge : 2;
        int span = srcEdge is 0 or 2 ? W : H;
        int a0 = span / 4 + IntNoise.Below(IntNoise.Hash(seed ^ SaltRiver, 0, 0), span / 2);
        int sx = srcEdge switch { 1 => W - 1, 3 => 0, _ => a0 };
        int sy = srcEdge switch { 0 => 0, 2 => H - 1, _ => a0 };
        // least-cost path (Dijkstra, integer costs, unique composite priorities → deterministic): per-step length,
        // a valley-floor preference (height above the map minimum) and a climb penalty.
        int n = W * H, hmin = int.MaxValue;
        foreach (int v in h) hmin = Math.Min(hmin, v);
        var dist = new long[n]; Array.Fill(dist, long.MaxValue);
        var prev = new int[n]; Array.Fill(prev, -1);
        var done = new bool[n];
        var pq = new PriorityQueue<int, (long cost, int cell)>(Comparer<(long cost, int cell)>.Create((a, b) => a.cost != b.cost ? a.cost.CompareTo(b.cost) : a.cell.CompareTo(b.cell)));
        int s0 = g.Index(sx, sy);
        dist[s0] = 0; pq.Enqueue(s0, (0, s0));
        int end = -1;
        while (pq.TryDequeue(out int ci, out var pri))
        {
            if (done[ci] || pri.cost != dist[ci]) continue;
            done[ci] = true;
            int cx = ci % W, cy = ci / W;
            if ((EdgeCoords(dstEdge, cx, cy, W, H).d == 0) || (coastal && h[ci] < g.WaterLevelMm - RiverFreeboardMm)) { end = ci; break; }
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int ni = g.Index(nx, ny);
                    if (done[ni]) continue;
                    long step = (dx != 0 && dy != 0 ? 1414L : 1000L) * g.CellMm / 1000
                        + (h[ni] - (long)hmin) / 2 + 8L * Math.Max(0, h[ni] - h[ci]);
                    long nd = dist[ci] + step;
                    if (nd < dist[ni]) { dist[ni] = nd; prev[ni] = ci; pq.Enqueue(ni, (nd, ni)); }
                }
        }
        var path = new List<int>();
        for (int c = end; c >= 0; c = prev[c]) path.Add(c);
        path.Reverse();
        if (path.Count == 0) return;

        var surf = new int[path.Count];
        int run = h[path[0]];
        for (int i = 0; i < path.Count; i++) { run = Math.Min(run, h[path[i]]); surf[i] = run - RiverFreeboardMm; }
        // cap the surface drop per path step (backward pass lowers upstream surfaces, i.e. cuts the channel deeper),
        // so channel cells of nearby path points differ by less than the bank freeboard (verification finding 2).
        for (int i = path.Count - 2; i >= 0; i--) surf[i] = Math.Min(surf[i], surf[i + 1] + RiverMaxDropMm);

        for (int i = 0; i < path.Count; i++)
        {
            int px = path[i] % W, py = path[i] / W;
            int rr = 1 + i * 2 / Math.Max(1, path.Count);
            int s = surf[i];
            for (int dy = -rr - BankRamp; dy <= rr + BankRamp; dy++)
                for (int dx = -rr - BankRamp; dx <= rr + BankRamp; dx++)
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int ni = g.Index(nx, ny), cheb = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (cheb <= rr)
                    {
                        int bed = s - RiverDepthMm + (cheb == rr ? RiverDepthMm / 2 : 0);
                        h[ni] = Math.Min(h[ni], bed);
                        surface[ni] = Math.Min(surface[ni], s);
                    }
                    else h[ni] = Math.Min(h[ni], s + 600 + (cheb - rr - 1) * talus);
                }
        }
        trace.RiverCells = path.ToArray();
        trace.RiverSurfaceMm = surf;
    }

    /// <summary>Two-cell-wide road across the map (W→E, or N→S on east/west coasts); fords rivers, avoids the sea.</summary>
    internal static int LayRoad(BattleTerrainGrid g, in BattleTerrainInputs inp, ulong seed, bool coastal, bool[] road)
    {
        bool alongX = !(coastal && inp.CoastEdge is 1 or 3);
        int L = alongX ? g.Width : g.Height, C = alongX ? g.Height : g.Width;
        int c = inp.CoastEdge switch
        {
            0 => C * 2 / 3,
            2 => C / 3,
            1 => C / 3,
            3 => C * 2 / 3,
            _ => C / 2 + IntNoise.Below(IntNoise.Hash(seed ^ SaltRoad, 1, 1), C / 4) - C / 8,
        };
        int Cell(int l, int cc) => alongX ? g.Index(l, cc) : g.Index(cc, l);
        int count = 0, prevH = g.HeightMm[Cell(0, c)];
        for (int l = 0; l < L; l++)
        {
            long best = long.MaxValue; int bc = c;
            for (int k = 0; k < 3; k++)
            {
                int cand = c + (k == 0 ? 0 : k == 1 ? -1 : 1);
                if (cand < 2 || cand > C - 3) continue;
                int ci = Cell(l, cand);
                long cost = Math.Abs(g.HeightMm[ci] - prevH) + (cand != c ? 150 : 0);
                if (coastal && g.HeightMm[ci] < g.WaterLevelMm + 1000) cost += 1_000_000;
                if (cost < best) { best = cost; bc = cand; }
            }
            c = bc;
            int a = Cell(l, c), b = Cell(l, c + 1);
            prevH = g.HeightMm[a];
            foreach (int i in new[] { a, b })
                if (g.WaterDepthMm[i] == 0 && !road[i]) { road[i] = true; count++; }
        }
        return count;
    }
}
