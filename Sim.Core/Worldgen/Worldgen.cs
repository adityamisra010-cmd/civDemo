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
/// </summary>
public static class Worldgen
{
    // Layer salts: keep each field's noise space disjoint.
    private const ulong MaskSalt = 0x4D41534B_00000001UL;
    private const ulong ElevationSalt = 0x454C4556_00000002UL;

    public static TerrainSet Generate(WorldgenConfig cfg, ulong seed)
    {
        int size = cfg.SizePx;
        int cells = size * size;

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
                double nx = x / (double)(size - 1);

                // Radial falloff: 1 at center → 0 at the corner distance, so the
                // continent sits in open sea (D-009: one Europe-class landmass).
                double dx = (x - half) / half, dy = (y - half) / half;
                double radial = 1.0 - Math.Sqrt(dx * dx + dy * dy) / Math.Sqrt(2.0);
                if (radial < 0.0) radial = 0.0;

                double maskNoise = Fbm(seed, MaskSalt, nx, ny, cfg.ContinentalMask.Noise);
                double mask = cfg.ContinentalMask.RadialWeight * radial
                            + cfg.ContinentalMask.NoiseWeight * maskNoise;

                elevation[y * size + x] = Fbm(seed, ElevationSalt, nx, ny, cfg.Elevation) * mask;
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

        // --- 5: moisture — rational decay over BFS water distance ----------------
        int[] waterDistance = WaterDistanceBfs(water, size);
        for (int i = 0; i < cells; i++)
            moisture[i] = 1.0 / (1.0 + waterDistance[i] / cfg.MoistureDecayPx);

        // --- 6: fertility v0 — temperature suitability × moisture, land only -----
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) { fertility[i] = 0.0; continue; }
            double tempSuit = 1.0 - Math.Abs(temperature[i] - cfg.Temperature.FertilityOptimalC)
                                    / cfg.Temperature.FertilityToleranceC;
            if (tempSuit < 0.0) tempSuit = 0.0;
            fertility[i] = tempSuit * moisture[i];
        }

        // --- 7: movement-cost — slope-scaled on land, flat penalty on water ------
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (water[i] >= 0.5) { movementCost[i] = cfg.Movement.WaterCost; continue; }
                double e = elevation[i], slope = 0.0;
                if (x > 0) slope = Math.Max(slope, Math.Abs(e - elevation[i - 1]));
                if (x < size - 1) slope = Math.Max(slope, Math.Abs(e - elevation[i + 1]));
                if (y > 0) slope = Math.Max(slope, Math.Abs(e - elevation[i - size]));
                if (y < size - 1) slope = Math.Max(slope, Math.Abs(e - elevation[i + size]));
                movementCost[i] = cfg.Movement.BaseCost + cfg.Movement.SlopeFactor * slope;
            }
        }

        return new TerrainSet(size, cfg.KmPerPx, seaLevel,
            elevation, water, temperature, moisture, fertility, movementCost);
    }

    /// <summary>
    /// Multi-source BFS grid distance (in px steps) from every water cell.
    /// Deterministic: sources enqueue in row-major order; neighbors expand in the
    /// fixed order W, E, N, S. All-land worlds are impossible (the quantile sea
    /// level guarantees water), so the queue is never empty.
    /// </summary>
    private static int[] WaterDistanceBfs(double[] water, int size)
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

    // --- hash-based value noise ------------------------------------------------

    /// <summary>fBm over value noise; coordinates in [0,1] scaled by cfg frequency.</summary>
    private static double Fbm(ulong seed, ulong salt, double nx, double ny, NoiseConfig cfg)
    {
        double sum = 0.0, amplitude = 1.0, totalAmplitude = 0.0;
        double frequency = cfg.Frequency;
        for (int octave = 0; octave < cfg.Octaves; octave++)
        {
            ulong octaveSalt = SplitMix64.Mix(salt + (ulong)octave * 0x9E3779B97F4A7C15UL);
            sum += amplitude * ValueNoise(seed, octaveSalt, nx * frequency, ny * frequency);
            totalAmplitude += amplitude;
            amplitude *= cfg.Persistence;
            frequency *= cfg.Lacunarity;
        }
        return sum / totalAmplitude; // normalized to [0,1)
    }

    /// <summary>Bilinear value noise with smoothstep fade; corner values are pure integer hashes.</summary>
    private static double ValueNoise(ulong seed, ulong salt, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        double u = tx * tx * (3.0 - 2.0 * tx);
        double v = ty * ty * (3.0 - 2.0 * ty);

        double c00 = Corner(seed, salt, x0, y0);
        double c10 = Corner(seed, salt, x0 + 1, y0);
        double c01 = Corner(seed, salt, x0, y0 + 1);
        double c11 = Corner(seed, salt, x0 + 1, y0 + 1);

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
