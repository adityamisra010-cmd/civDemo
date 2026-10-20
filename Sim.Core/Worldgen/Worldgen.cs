using Sim.Core.Kernel;

namespace Sim.Core.Worldgen;

/// <summary>
/// M1 worldgen (D-022, T2-lite): plausible fields, not geological simulation.
/// NOT a turn system — runs once, pre-turn-0, fully deterministic from
/// (config, seed): two generations of the same inputs are byte-identical
/// (the worldgen twin-test pins this).
///
/// Noise is hash-based value-noise fBm — every lattice value is a pure SplitMix64
/// mix of (seed, salt, cell coords). No RNG stream state, no trig, no
/// transcendentals anywhere in the pipeline (moisture decay is rational), so
/// determinism holds by construction rather than by draw-order discipline.
///
/// Pipeline (D-022 order): continental mask → elevation fBm → sea level (exact
/// land-fraction quantile) → temperature (latitude + elevation lapse) →
/// moisture (water-distance BFS) → fertility v0 → movement-cost.
/// Anti-scope: no plate tectonics, no erosion, no climate simulation (ADR-008
/// notes the upgrade path). Rivers arrive in T1.2.
///
/// BATCH 3 WS3 — THE CYLINDER (worldgen.json topology.wrapX; the Director's 2026-10-14 instruction supersedes
/// ADR-032 Final Ruling 3's "flat square raster (no wrap)" and T3.1(b)'s x half). With wrapX:
///  · the EDGE TAPER is y-only — edgeDist = min(y, size−1−y): the poles stay ocean (vertical boundaries are kept),
///    the east and west edges are no longer edges;
///  · the VALUE NOISE is periodic in x: nx = x / size (not x / (size−1)), and an octave's corner column is taken
///    modulo its whole-number period frequency × lacunarity^octave (validated: WorldgenConfigLoader.ValidateTopology);
///  · the RADIAL continental mask is unchanged — it is symmetric about the centre column, so its values at x = 0 and
///    x = size − 1 are equal and it is continuous across the seam; one landmass stays centred (D-009);
///  · HYDROLOGY floods and routes over x-wrapped neighbours, with border sinks on the poles only;
///  · the SLOPE stencil and the river- and water-distance BFS wrap in x.
/// Without it (absent or false) every stage is the pre-batch-3 code path, byte for byte (PreCylinderWorldgen).
/// The resulting <see cref="TerrainSet.WrapX"/> is the flag every later consumer reads.
/// </summary>
public static class Worldgen
{
    // Layer salts: keep each field's noise space disjoint.
    private const ulong MaskSalt = 0x4D41534B_00000001UL;
    private const ulong ElevationSalt = 0x454C4556_00000002UL;

    public static TerrainSet Generate(WorldgenConfig cfg, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        // THE ONE READER of worldgen.json topology (WS3): every override path (`--size`, UiFounding, test helpers)
        // reaches here without the loader, so the topology rules are re-checked at the door.
        WorldgenConfigLoader.ValidateTopology(cfg);
        bool wrapX = cfg.Topology?.WrapX ?? false;
        int size = cfg.SizePx;
        int cells = size * size;
        // Wrapped noise: x / size puts x = size (the seam's far side) exactly one period on, so the field is periodic;
        // the flat path keeps the pre-batch-3 x / (size − 1) bit for bit.
        double nxScale = wrapX ? size : size - 1;
        int[]? maskPeriods = wrapX ? Periods(cfg.ContinentalMask.Noise) : null;
        int[]? elevationPeriods = wrapX ? Periods(cfg.Elevation) : null;

        var elevation = new double[cells];
        var water = new double[cells];
        var temperature = new double[cells];
        var moisture = new double[cells];
        var fertility = new double[cells];
        var movementCost = new double[cells];

        // --- 1+2: continental mask, elevation fBm --------------------------------
        double half = (size - 1) / 2.0;
        for (int y = 0; y < size; y++)
        {
            double ny = y / (double)(size - 1);
            for (int x = 0; x < size; x++)
            {
                double nx = x / nxScale;

                // Radial falloff: 1 at center → 0 at the corner distance, so the
                // continent sits in open sea (D-009: one Europe-class landmass).
                double dx = (x - half) / half, dy = (y - half) / half;
                double radial = 1.0 - Math.Sqrt(dx * dx + dy * dy) / Math.Sqrt(2.0);
                if (radial < 0.0) radial = 0.0;

                double maskNoise = Fbm(seed, MaskSalt, nx, ny, cfg.ContinentalMask.Noise, maskPeriods);
                double mask = cfg.ContinentalMask.RadialWeight * radial
                            + cfg.ContinentalMask.NoiseWeight * maskNoise;

                // T3.1(b) EDGE TAPER — guarantee an ocean margin. The radial
                // falloff alone reaches zero only at CORNERS (edge-midpoint
                // radial ≈ 0.29), so continents could touch the boundary and
                // read as truncated (measured on seed 42 pre-fix: 185 land
                // cells on the eastern boundary column). A smoothstep ramp
                // over edgeTaperPx crushes near-edge elevation toward 0; the
                // boundary ring is EXACTLY 0, which sits below the quantile
                // sea level for any non-degenerate land fraction — so the
                // world edge is water by construction (10-seed test pins it).
                //
                // WS3: on a cylinder there is no east or west edge, so the taper is y-only — the poles keep their
                // guaranteed ocean margin (vertical boundaries preserved) and land may cross the seam.
                double edgeDist = wrapX
                    ? Math.Min(y, size - 1 - y)
                    : Math.Min(Math.Min(x, size - 1 - x), Math.Min(y, size - 1 - y));
                double te = Math.Min(1.0, edgeDist / (double)cfg.ContinentalMask.EdgeTaperPx);
                double taper = te * te * (3.0 - 2.0 * te);

                elevation[y * size + x] = Fbm(seed, ElevationSalt, nx, ny, cfg.Elevation, elevationPeriods) * mask * taper;
            }
        }

        // --- 3: sea level as the exact land-fraction quantile --------------------
        // Sorting a copy gives the exact threshold: land fraction lands on target
        // by construction (± ties), which is what the 10-seed bounds test relies on.
        var sorted = new double[cells];
        Array.Copy(elevation, sorted, cells);
        Array.Sort(sorted);
        int seaIndex = (int)Math.Clamp((1.0 - cfg.LandFractionTarget) * cells, 0, cells - 1);
        double seaLevel = sorted[seaIndex];

        for (int i = 0; i < cells; i++)
            water[i] = elevation[i] < seaLevel ? 1.0 : 0.0;

        // --- 4: temperature — latitude band + elevation lapse --------------------
        for (int y = 0; y < size; y++)
        {
            double latitude = Math.Abs(y / (double)(size - 1) - 0.5) * 2.0; // 0 equator → 1 pole
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                double aboveSea = elevation[i] > seaLevel ? elevation[i] - seaLevel : 0.0;
                temperature[i] = cfg.Temperature.EquatorC
                               - cfg.Temperature.PoleDropC * latitude
                               - cfg.Temperature.LapsePerElevC * aboveSea;
            }
        }

        // --- 5: hydrology (T1.2; moved BEFORE moisture at T3.1a) -----------------
        Hydrology.Result rivers = Hydrology.Compute(elevation, water, size, cfg.Rivers, wrapX);

        // --- 5b: moisture — rational decay over BFS distance to water OR RIVER
        // cells (T3.1a: the water-access family credits river adjacency — a
        // river valley IS moist ground. Before this fix, moisture measured
        // sea-distance only, so an interior river valley was dry, therefore
        // infertile, therefore never a settlement site: the missing
        // settlement type the m3-spec names). D-022's stage order is
        // preserved: moisture still precedes fertility; hydrology needs only
        // elevation + water and lawfully moves ahead of moisture.
        int[] waterDistance = WaterDistanceBfs(water, rivers.RiverMask, size, wrapX);
        for (int i = 0; i < cells; i++)
            moisture[i] = 1.0 / (1.0 + waterDistance[i] / cfg.MoistureDecayPx);

        // --- 6: fertility v0 — temperature suitability × moisture, land only,
        //         with the river-adjacency boost applied BEFORE finalization ------
        int[] riverDistance = ChebyshevDistanceToRivers(rivers.RiverMask, size, cfg.Rivers.AdjacencyRadiusPx, wrapX);
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) { fertility[i] = 0.0; continue; }
            double tempSuit = 1.0 - Math.Abs(temperature[i] - cfg.Temperature.FertilityOptimalC)
                                    / cfg.Temperature.FertilityToleranceC;
            if (tempSuit < 0.0) tempSuit = 0.0;
            double f = tempSuit * moisture[i];
            if (riverDistance[i] <= cfg.Rivers.AdjacencyRadiusPx)
                f *= 1.0 + cfg.Rivers.FertilityBoost;
            fertility[i] = f > 1.0 ? 1.0 : f;   // finalization clamp
        }

        // --- 7: movement-cost — slope-scaled on land, flat penalty on water ------
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (water[i] >= 0.5) { movementCost[i] = cfg.Movement.WaterCost; continue; }
                double e = elevation[i], slope = 0.0;
                // WS3: the x neighbours wrap on a cylinder (MapTopology.NeighbourX: −1 off a flat map's edge).
                int west = Pathing.MapTopology.NeighbourX(x, -1, size, wrapX);
                int east = Pathing.MapTopology.NeighbourX(x, 1, size, wrapX);
                if (west >= 0) slope = Math.Max(slope, Math.Abs(e - elevation[y * size + west]));
                if (east >= 0) slope = Math.Max(slope, Math.Abs(e - elevation[y * size + east]));
                if (y > 0) slope = Math.Max(slope, Math.Abs(e - elevation[i - size]));
                if (y < size - 1) slope = Math.Max(slope, Math.Abs(e - elevation[i + size]));
                movementCost[i] = cfg.Movement.BaseCost + cfg.Movement.SlopeFactor * slope;
            }
        }

        return new TerrainSet(size, cfg.KmPerPx, seaLevel,
            elevation, water, temperature, moisture, fertility, movementCost,
            rivers.RiverMask, rivers.Polylines, wrapX);
    }

    /// <summary>The whole-number x period of each octave (validated by WorldgenConfigLoader.ValidateTopology).</summary>
    private static int[] Periods(NoiseConfig n)
    {
        var periods = new int[n.Octaves];
        for (int o = 0; o < periods.Length; o++) periods[o] = WorldgenConfigLoader.OctavePeriod(n, o);
        return periods;
    }

    /// <summary>
    /// Chebyshev distance (in px) to the nearest river cell, capped at
    /// <paramref name="maxRadius"/>+1 — a bounded multi-source BFS over the
    /// 8-neighborhood, same fixed expansion order as the water-distance BFS.
    /// </summary>
    private static int[] ChebyshevDistanceToRivers(double[] riverMask, int size, int maxRadius, bool wrapX)
    {
        int cells = size * size;
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;
        for (int i = 0; i < cells; i++)
        {
            if (riverMask[i] >= 0.5) { dist[i] = 0; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }
        while (head < tail)
        {
            int i = queue[head++];
            int d = dist[i] + 1;
            if (d > maxRadius) continue;   // bounded: beyond the boost radius is "far"
            int x = i % size, y = i / size;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = Pathing.MapTopology.NeighbourX(x, dx, size, wrapX), ny = y + dy;
                    if (nx < 0 || ny < 0 || ny >= size) continue;
                    int n = ny * size + nx;
                    if (dist[n] == int.MaxValue) { dist[n] = d; queue[tail++] = n; }
                }
            }
        }
        return dist;
    }

    /// <summary>
    /// Multi-source BFS grid distance (in px steps) from every water cell.
    /// Deterministic: sources enqueue in row-major order; neighbors expand in the
    /// fixed order W, E, N, S. All-land worlds are impossible (the quantile sea
    /// level guarantees water), so the queue is never empty.
    /// </summary>
    private static int[] WaterDistanceBfs(double[] water, double[] rivers, int size, bool wrapX)
    {
        int cells = size * size;
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;

        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5 || rivers[i] >= 0.5) { dist[i] = 0; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }

        while (head < tail)
        {
            int i = queue[head++];
            int x = i % size, y = i / size, d = dist[i] + 1;
            // WS3: W and E wrap on a cylinder; the order W, E, N, S is unchanged.
            int w = Pathing.MapTopology.NeighbourX(x, -1, size, wrapX), e = Pathing.MapTopology.NeighbourX(x, 1, size, wrapX);
            if (w >= 0 && dist[y * size + w] == int.MaxValue) { dist[y * size + w] = d; queue[tail++] = y * size + w; }
            if (e >= 0 && dist[y * size + e] == int.MaxValue) { dist[y * size + e] = d; queue[tail++] = y * size + e; }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue[tail++] = i - size; }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue[tail++] = i + size; }
        }
        return dist;
    }

    // --- hash-based value noise ------------------------------------------------

    /// <summary>fBm over value noise; coordinates in [0,1] scaled by cfg frequency. <paramref name="periodsX"/> (WS3):
    /// per octave, the whole-number x period its corner columns wrap at — null for the flat, non-periodic field.</summary>
    private static double Fbm(ulong seed, ulong salt, double nx, double ny, NoiseConfig cfg, int[]? periodsX)
    {
        double sum = 0.0, amplitude = 1.0, totalAmplitude = 0.0;
        double frequency = cfg.Frequency;
        for (int octave = 0; octave < cfg.Octaves; octave++)
        {
            ulong octaveSalt = SplitMix64.Mix(salt + (ulong)octave * 0x9E3779B97F4A7C15UL);
            sum += amplitude * ValueNoise(seed, octaveSalt, nx * frequency, ny * frequency,
                periodsX is null ? 0 : periodsX[octave]);
            totalAmplitude += amplitude;
            amplitude *= cfg.Persistence;
            frequency *= cfg.Lacunarity;
        }
        return sum / totalAmplitude; // normalized to [0,1)
    }

    /// <summary>Bilinear value noise with smoothstep fade; corner values are pure integer hashes. <paramref name="periodX"/>
    /// &gt; 0 (WS3) makes the field periodic in x: the corner columns are taken modulo it, so column periodX hashes as
    /// column 0 and the field at x + periodX equals the field at x. 0 is the flat, non-periodic field.</summary>
    private static double ValueNoise(ulong seed, ulong salt, double x, double y, int periodX)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        double u = tx * tx * (3.0 - 2.0 * tx);
        double v = ty * ty * (3.0 - 2.0 * ty);

        int xa = x0, xb = x0 + 1;
        if (periodX > 0)
        {
            xa = Pathing.MapTopology.WrapX(xa, periodX);
            xb = Pathing.MapTopology.WrapX(xb, periodX);
        }
        double c00 = Corner(seed, salt, xa, y0);
        double c10 = Corner(seed, salt, xb, y0);
        double c01 = Corner(seed, salt, xa, y0 + 1);
        double c11 = Corner(seed, salt, xb, y0 + 1);

        double top = c00 + (c10 - c00) * u;
        double bottom = c01 + (c11 - c01) * u;
        return top + (bottom - top) * v;
    }

    /// <summary>Lattice-corner value in [0,1): a 53-bit double from one SplitMix64 mix chain.</summary>
    private static double Corner(ulong seed, ulong salt, int xi, int yi)
    {
        ulong packed = ((ulong)(uint)xi << 32) | (uint)yi;
        ulong h = SplitMix64.Mix(SplitMix64.Mix(seed ^ salt) ^ packed);
        return (h >> 11) * (1.0 / 9007199254740992.0); // top 53 bits / 2^53
    }
}
