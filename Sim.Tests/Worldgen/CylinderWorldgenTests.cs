using Sim.Core.Pathing;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Worldgen;

/// <summary>
/// Batch 3 WS3 — THE WORLD ITSELF IS A CYLINDER (worldgen.json topology.wrapX). Worldgen's pieces that assumed hard
/// east-west edges — the four-sided edge taper, the non-periodic noise, the four-sided hydrology sinks, the x bounds
/// of the slope stencil, the distance BFS and the siting windows — now wrap: the terrain is continuous across the seam,
/// rivers may cross it, and the poles keep their ocean margin. The flat path is pinned byte for byte by
/// WrapAttributionTests; here are the wrapped behaviours and the refusals.
/// </summary>
public class CylinderWorldgenTests
{
    private static WorldgenConfig Dev() => TestConfigs.Worldgen() with
    {
        SizePx = 256,
        ContinentalMask = TestConfigs.Worldgen().ContinentalMask with { EdgeTaperPx = 12 },
    };

    /// <summary>Mean |Δ| of <paramref name="layer"/> between column x and column x + 1 over all rows, for every column
    /// pair including the seam pair (size − 1, 0) — returned as (seam, min interior, max interior).</summary>
    private static (double Seam, double Min, double Max) ColumnSteps(ReadOnlySpan<double> layer, int size)
    {
        double seam = 0.0, min = double.MaxValue, max = 0.0;
        for (int x = 0; x < size; x++)
        {
            int nx = (x + 1) % size;
            double sum = 0.0;
            for (int y = 0; y < size; y++) sum += Math.Abs(layer[y * size + nx] - layer[y * size + x]);
            double mean = sum / size;
            if (x == size - 1) { seam = mean; continue; }
            min = Math.Min(min, mean);
            max = Math.Max(max, mean);
        }
        return (seam, min, max);
    }

    [Fact]
    public void TheTerrainIsContinuousAcrossTheSeam_TenSeeds_LikeAnyInteriorColumnPair()
    {
        // The seam column pair (size − 1, 0) must read like an interior pair, on ten seeds, for elevation and moisture:
        //  · NO CUT — its mean step is no larger than the roughest interior pair's (non-periodic noise would put two
        //    unrelated noise values side by side there: a jump the size of the field's range);
        //  · NO EDGE BAND — the seam columns carry real elevation (the old four-sided taper pinned both to EXACTLY 0, so
        //    the step was 0 and every edge cell sea), and the step is not zero.
        // MEASURED (dev 256 px, seeds 1–10): the seam's elevation step is 0.0011–0.0017, the interior pairs' 0.0017–0.0075.
        // It sits at the SMOOTH end because x = 0 is a value-noise LATTICE CORNER in every octave — each octave's period is
        // a whole number of noise cells, so a corner falls on the seam — where the smoothstep's derivative is zero; the
        // interior all-octave corner pairs ((63, 64), (127, 128), (191, 192) at 256 px) are smooth for the same reason.
        // (W1 fix F6: this comment used to credit the radial mask; the verifier's probe P4c measured the seam step still
        // ranking 0.000–0.161 among the interior pairs with the radial weight set to 0, so the mask is not the cause.)
        // A two-sided interior bound would therefore refuse a continuous world; the cut and the band are what is refused,
        // and the noise's exact periodicity is pinned directly by TheNoiseIsExactlyPeriodicInX_TheFieldAtXPlusOneMapIsTheField.
        WorldgenConfig cfg = Dev();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            Assert.True(t.WrapX);
            int n = t.Size;
            foreach ((string name, double[] layer) in new[] { ("elevation", t.Elevation.ToArray()), ("moisture", t.Moisture.ToArray()) })
            {
                (double seam, _, double max) = ColumnSteps(layer, n);
                Assert.True(seam <= max, $"seed {seed} {name}: seam step {seam} exceeds every interior pair (max {max}) — a cut at the seam");
            }
            (double elevationSeam, double elevationMin, _) = ColumnSteps(t.Elevation, n);
            Assert.True(elevationSeam > 0.25 * elevationMin, $"seed {seed}: seam elevation step {elevationSeam} is a flat band");
            int pinned = 0;
            for (int y = 0; y < n; y++) if (t.Elevation[y * n] == 0.0 || t.Elevation[y * n + n - 1] == 0.0) pinned++;
            Assert.True(pinned <= 2 * cfg.ContinentalMask.EdgeTaperPx, $"seed {seed}: {pinned} rows pin a seam column to 0 (only the pole taper may)");
        }
    }

    /// <summary>W1 fix F6 — THE NOISE ITSELF IS PERIODIC. The fBm worldgen samples (private; read by reflection, the
    /// KnowledgeLeakTests precedent for reaching a non-public surface from a test) evaluated just past the map's east
    /// edge equals its value at the matching column on the west, BIT FOR BIT, for both noise fields (continental mask and
    /// elevation) with the periods Generate derives from the shipped config — f(1 + u, y) = f(u, y) for u = −1/size … 0 —
    /// while the flat field (no periods) differs there: the test can tell periodic noise from non-periodic.</summary>
    [Fact]
    public void TheNoiseIsExactlyPeriodicInX_TheFieldAtXPlusOneMapIsTheField()
    {
        System.Reflection.MethodInfo fbm = typeof(Sim.Core.Worldgen.Worldgen).GetMethod(
            "Fbm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.NotNull(fbm);
        WorldgenConfig cfg = TestConfigs.Worldgen();
        double F(ulong seed, ulong salt, double nx, double ny, NoiseConfig n, int[]? periods) =>
            (double)fbm.Invoke(null, [seed, salt, nx, ny, n, periods])!;
        static int[] Periods(NoiseConfig n)
        {
            var p = new int[n.Octaves];
            for (int o = 0; o < p.Length; o++) p[o] = WorldgenConfigLoader.OctavePeriod(n, o);
            return p;
        }
        int differsFlat = 0, compared = 0;
        foreach ((ulong salt, NoiseConfig noise) in new[] { (0x4D41534B_00000001UL, cfg.ContinentalMask.Noise), (0x454C4556_00000002UL, cfg.Elevation) })
        {
            int[] periods = Periods(noise);
            Assert.All(periods, p => Assert.True(p > 0));
            foreach (int size in new[] { 256, 1024 })
                for (ulong seed = 1; seed <= 5; seed++)
                    for (int y = 0; y < size; y += size / 8)
                    {
                        double ny = y / (double)(size - 1);
                        // The seam pair: column size − 1 and the column one step west of 0 (x = −1), and x = size vs x = 0.
                        foreach ((double east, double west) in new[] { ((size - 1) / (double)size, -1.0 / size), (1.0, 0.0) })
                        {
                            Assert.Equal(F(seed, salt, west, ny, noise, periods), F(seed, salt, east, ny, noise, periods));
                            if (F(seed, salt, west, ny, noise, null) != F(seed, salt, east, ny, noise, null)) differsFlat++;
                            compared++;
                        }
                    }
        }
        Assert.True(differsFlat > compared / 2, $"the non-periodic field matched across the seam {compared - differsFlat}/{compared} times — the check has no teeth");
    }

    [Fact]
    public void TheFlatWorld_StillHasItsFourSidedOceanMargin()
    {
        // PreCylinder keeps T3.1(b) whole: on the flat world every boundary cell is water and the edge columns are
        // pinned to 0 — which is exactly the band the continuity test above refuses on a cylinder.
        WorldgenConfig cfg = TestConfigs.PreCylinderWorldgen(Dev());
        for (ulong seed = 1; seed <= 3; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int n = t.Size;
            for (int i = 0; i < n; i++)
            {
                Assert.True(t.Water[i * n] >= 0.5 && t.Water[i * n + n - 1] >= 0.5);
                Assert.Equal(0.0, t.Elevation[i * n]);
                Assert.Equal(0.0, t.Elevation[i * n + n - 1]);
            }
            (double seam, double min, _) = ColumnSteps(t.Elevation, n);
            Assert.True(seam < min, $"seed {seed}: the flat world's seam is not the flat edge band");
        }
    }

    /// <summary>Final-round fix 4 (the W1 calibration verifier's F6, mutant MA2; D-054 §8) — THE FLAT WORLD SAMPLES THE
    /// NOISE ON THE CLOSED UNIT SQUARE, AS BATCH 2 DID. Off the cylinder, column x samples nx = x / (size − 1) exactly as
    /// row y samples ny = y / (size − 1), so the last column lands on the noise's period endpoint nx = 1 (the cylinder's
    /// x / size stops one column short of it, because there column size IS column 0). v32 saves and batch-2 sessions are
    /// rebuilt on this path, and until now no semantic test defended its x scaling: under a mutant of it only byte-identity
    /// checks failed (the WrapAttributionTests ContentHash literal, the v32 fixture's stored terrain hash) and a rig guard.
    /// Two properties, at sizes that are not powers of two:
    ///  · GRID REFINEMENT — cell (x, y) of an S-px flat world and cell (2x, 2y) of a (2S − 1)-px one sample the same noise
    ///    point, and the radial mask (a ratio over (size − 1) / 2) and the edge taper (a ratio over edgeTaperPx, doubled
    ///    with the grid) are the same ratios, so their elevations are equal BIT FOR BIT. Only a mapping of the form
    ///    k · x / (size − 1) refines this way; x / size does not.
    ///  · THE SCALE — with the mask reduced to its noise (radialWeight 0, noiseWeight 1) and the taper to 1 inside a 1-px
    ///    margin, an interior cell's elevation is exactly the elevation fBm times the mask fBm at (x / (size − 1),
    ///    y / (size − 1)), both evaluated by the private noise primitive (reached by reflection, as in
    ///    TheNoiseIsExactlyPeriodicInX_TheFieldAtXPlusOneMapIsTheField) with no periods (the flat field).</summary>
    [Fact]
    public void TheFlatWorld_SamplesTheNoiseOnTheClosedUnitSquare_ItsLastColumnAtThePeriodEndpoint()
    {
        const int small = 125, large = 2 * small - 1;   // 124 and 248 noise steps across: neither size a power of two
        WorldgenConfig flat = TestConfigs.PreCylinderWorldgen(TestConfigs.Worldgen());
        WorldgenConfig coarse = flat with { SizePx = small, ContinentalMask = flat.ContinentalMask with { EdgeTaperPx = 6 } };
        WorldgenConfig fine = flat with { SizePx = large, ContinentalMask = flat.ContinentalMask with { EdgeTaperPx = 12 } };
        int nonZero = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            TerrainSet a = Sim.Core.Worldgen.Worldgen.Generate(coarse, seed), b = Sim.Core.Worldgen.Worldgen.Generate(fine, seed);
            Assert.False(a.WrapX || b.WrapX);
            for (int y = 0; y < small; y++)
                for (int x = 0; x < small; x++)
                {
                    double e = a.Elevation[y * small + x], refined = b.Elevation[2 * y * large + 2 * x];
                    Assert.True(BitConverter.DoubleToInt64Bits(e) == BitConverter.DoubleToInt64Bits(refined),
                        $"seed {seed}: flat cell ({x},{y}) of the {small}-px world ({e:R}) and cell ({2 * x},{2 * y}) of the " +
                        $"{large}-px world ({refined:R}) sample different noise points: the flat x mapping is no longer " +
                        $"x / (size − 1), under which the last column samples the period endpoint nx = 1");
                    if (e > 0.0) nonZero++;
                }
        }
        Assert.True(nonZero > small * small, $"only {nonZero} compared cells carry elevation — the refinement check is void");

        System.Reflection.MethodInfo fbm = typeof(Sim.Core.Worldgen.Worldgen).GetMethod(
            "Fbm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.NotNull(fbm);
        double F(ulong seed, ulong salt, double nx, double ny, NoiseConfig n) =>
            (double)fbm.Invoke(null, [seed, salt, nx, ny, n, null])!;
        WorldgenConfig noiseOnly = coarse with
        {
            ContinentalMask = coarse.ContinentalMask with { RadialWeight = 0.0, NoiseWeight = 1.0, EdgeTaperPx = 1 },
        };
        const ulong seedB = 2;
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(noiseOnly, seedB);
        for (int y = 1; y < small - 1; y++)
            for (int x = 1; x < small - 1; x++)
            {
                double nx = x / (double)(small - 1), ny = y / (double)(small - 1);
                double want = F(seedB, 0x454C4556_00000002UL, nx, ny, noiseOnly.Elevation)
                            * F(seedB, 0x4D41534B_00000001UL, nx, ny, noiseOnly.ContinentalMask.Noise);
                double got = t.Elevation[y * small + x];
                Assert.True(BitConverter.DoubleToInt64Bits(want) == BitConverter.DoubleToInt64Bits(got),
                    $"flat cell ({x},{y}): elevation {got:R} is not the noise at ({x}/{small - 1}, {y}/{small - 1}) = {want:R} — " +
                    $"the flat x mapping is no longer the batch-2 x / (size − 1)");
            }
    }

    [Fact]
    public void ACanonicalSeed_HasLandAcrossTheSeam()
    {
        // Seeds 6, 8, 14, 16, 17, 18 and 20 of the canonical 1024-px config generate land on both edge columns
        // (MEASURED); seed 16 is the one SeamCrossingShippedWorldTests pins a path on.
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), 16);
        int n = t.Size, rows = 0;
        for (int y = 0; y < n; y++)
            if (t.Water[y * n] < 0.5 && t.Water[y * n + n - 1] < 0.5) rows++;
        Assert.True(rows > 0, "seed 16 has no land crossing the seam");
    }

    [Fact]
    public void Hydrology_OnACylinder_SinksOnlyAtThePoles_AndARiverMayCrossTheSeam()
    {
        // Seed 6 of the canonical config generates a river whose polyline crosses the seam (MEASURED). Every polyline
        // step is a D8 step on the CYLINDER (columns 0 and size − 1 are neighbours), descends strictly, and every
        // terminal land cell (flow target −1) lies on a pole row.
        WorldgenConfig cfg = TestConfigs.Worldgen();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, 6);
        int n = t.Size;
        Hydrology.Result h = Hydrology.Compute(t.Elevation.ToArray(), t.Water.ToArray(), n, cfg.Rivers, t.WrapX);
        int crossing = 0;
        for (int p = 0; p < t.RiverPolylineCount; p++)
        {
            ReadOnlySpan<int> line = t.RiverPolyline(p);
            for (int i = 1; i < line.Length; i++)
            {
                Assert.Equal(line[i], h.FlowTarget[line[i - 1]]);
                Assert.True(h.FilledElevation[line[i]] < h.FilledElevation[line[i - 1]]);
                int dx = MapTopology.AbsDx(line[i - 1] % n, line[i] % n, n, wrap: true);
                Assert.True(dx <= 1 && Math.Abs(line[i] / n - line[i - 1] / n) <= 1, "a polyline step is not a cylinder neighbour");
                if (Math.Abs(line[i] % n - line[i - 1] % n) > 1) crossing++;
            }
        }
        Assert.True(crossing > 0, "seed 6's river no longer crosses the seam — the measured rig is void");
        for (int i = 0; i < n * n; i++)
            if (t.Water[i] < 0.5 && h.FlowTarget[i] == -1)
                Assert.True(i / n == 0 || i / n == n - 1, $"land cell {i % n},{i / n} is a terminal sink off the poles");
    }

    [Fact]
    public void TheLoader_RefusesANonIntegerNoisePeriod_UnderWrap_NamingTheOctave()
    {
        string json = TestConfigs.WorldgenJson();
        // elevation frequency 4, lacunarity 1.5: periods 4, 6, 9, 13.5 … — octave 3 is the first fractional one.
        string fractional = json.Replace("\"frequency\": 4.0,\n    \"persistence\": 0.55,\n    \"lacunarity\": 2.0",
            "\"frequency\": 4.0,\n    \"persistence\": 0.55,\n    \"lacunarity\": 1.5");
        Assert.NotEqual(json, fractional);
        WorldgenConfigException e = Assert.Throws<WorldgenConfigException>(() => WorldgenConfigLoader.Load(fractional));
        Assert.Contains("elevation octave 3", e.Message);
        Assert.Contains("13.5", e.Message);
        // Flat, the same noise is legal (nothing needs to be periodic).
        string flat = fractional.Replace("\"wrapX\": true", "\"wrapX\": false");
        Assert.Equal(1.5, WorldgenConfigLoader.Load(flat).Elevation.Lacunarity);
        // And the generator re-checks (every --size or test override bypasses the loader).
        WorldgenConfig bypass = TestConfigs.Worldgen() with { Elevation = TestConfigs.Worldgen().Elevation with { Frequency = 2.5 } };
        Assert.Contains("elevation octave 0", Assert.Throws<WorldgenConfigException>(
            () => Sim.Core.Worldgen.Worldgen.Generate(bypass with { SizePx = 64 }, 1)).Message);
    }

    [Theory]
    [InlineData(250)]   // not a multiple of the stride: orphan columns at the seam
    [InlineData(8)]     // a 2-node lattice: a node would be its own neighbour
    public void Generate_RefusesAWrappedWorld_TheLatticeCannotTile_NamingSizeAndStride(int size)
    {
        WorldgenConfig cfg = TestConfigs.Worldgen() with { SizePx = size };
        WorldgenConfigException e = Assert.Throws<WorldgenConfigException>(() => Sim.Core.Worldgen.Worldgen.Generate(cfg, 1));
        Assert.Contains("size " + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + " px", e.Message);
        Assert.Contains("stride 4 px", e.Message);
        // Flat, a 250-px world generates (the StrideWorldsFixture case).
        if (size == 250) Assert.False(Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.PreCylinderWorldgen(cfg), 1).WrapX);
    }

    // ------------------------------------------------------------------ the derived layers, recomputed independently

    private static readonly Lazy<TerrainSet> Seed6 = new(() => Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), 6));

    [Fact]
    public void Moisture_IsTheWrappedWaterDistance_BitForBit()
    {
        // moisture = 1 / (1 + d / decay) with d the 4-connected BFS distance to water OR river ON THE CYLINDER — recomputed
        // with SettlementSiting's public twin of worldgen's private BFS. Seed 6 has land on both edge columns, so cells
        // whose nearest water lies across the seam exist and a flat BFS would give them a different moisture.
        TerrainSet t = Seed6.Value;
        WorldgenConfig cfg = TestConfigs.Worldgen();
        int[] d = SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, t.Size, wrapX: true);
        int[] flat = SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, t.Size);
        int differ = 0;
        for (int i = 0; i < d.Length; i++)
        {
            Assert.Equal(1.0 / (1.0 + d[i] / cfg.MoistureDecayPx), t.Moisture[i]);
            if (d[i] != flat[i]) differ++;
        }
        Assert.True(differ > 0, "no cell's water distance crosses the seam on seed 6 — the rig is void");
    }

    [Fact]
    public void Fertility_ReadsTheRiverBoostAcrossTheSeam_BitForBit()
    {
        // fertility = clamp(tempSuit × moisture × (1 + boost if within the adjacency radius of a river, Chebyshev, ON THE
        // CYLINDER)) on land — recomputed with an independent wrapped Chebyshev distance. Seed 6's river crosses the seam.
        TerrainSet t = Seed6.Value;
        WorldgenConfig cfg = TestConfigs.Worldgen();
        int n = t.Size, rad = cfg.Rivers.AdjacencyRadiusPx;
        int acrossSeamOnly = 0;
        for (int i = 0; i < n * n; i++)
        {
            if (t.Water[i] >= 0.5) { Assert.Equal(0.0, t.Fertility[i]); continue; }
            int x = i % n, y = i / n;
            bool near = false, nearFlat = false;
            for (int dy = -rad; dy <= rad && !near; dy++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= n) continue;
                    int xx = ((x + dx) % n + n) % n;
                    if (t.Rivers[yy * n + xx] < 0.5) continue;
                    near = true;
                    if (x + dx >= 0 && x + dx < n) nearFlat = true;
                }
            double tempSuit = Math.Max(0.0, 1.0 - Math.Abs(t.Temperature[i] - cfg.Temperature.FertilityOptimalC) / cfg.Temperature.FertilityToleranceC);
            double f = tempSuit * t.Moisture[i];
            if (near) f *= 1.0 + cfg.Rivers.FertilityBoost;
            Assert.Equal(f > 1.0 ? 1.0 : f, t.Fertility[i]);
            if (near && !nearFlat && tempSuit > 0.0) acrossSeamOnly++;
        }
        Assert.True(acrossSeamOnly > 0, "no cell is river-adjacent only across the seam on seed 6 — the rig is void");
    }

    /// <summary>The canonical config at 256 px, seed 6: land on both seam columns (MEASURED: 113 seam land cells), five of
    /// them whose steepest neighbour lies across the seam, and nine raised (depression-filled) seam cells whose lowest
    /// neighbour lies across it — the rig the slope and flood tests need (seed 6 at 1024 px has no such slope cell).</summary>
    private static readonly Lazy<TerrainSet> Dev6 = new(() =>
        Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = 256 }, 6));

    [Fact]
    public void MovementCost_ReadsTheSlopeAcrossTheSeam_BitForBit()
    {
        // land movement cost = base + slopeFactor × max |Δelevation| over the four neighbours ON THE CYLINDER.
        TerrainSet t = Dev6.Value;
        WorldgenConfig cfg = TestConfigs.Worldgen();
        int n = t.Size, seamLand = 0, acrossIsSteepest = 0;
        for (int y = 0; y < n; y++)
            foreach (int x in new[] { 0, n - 1 })
            {
                int i = y * n + x;
                if (t.Water[i] >= 0.5) continue;
                seamLand++;
                double e = t.Elevation[i], slope = 0.0;
                double across = Math.Abs(e - t.Elevation[y * n + (x == 0 ? n - 1 : 0)]);
                double inside = Math.Abs(e - t.Elevation[y * n + (x == 0 ? 1 : n - 2)]);
                slope = Math.Max(slope, across);
                slope = Math.Max(slope, inside);
                double north = y > 0 ? Math.Abs(e - t.Elevation[i - n]) : 0.0, south = y < n - 1 ? Math.Abs(e - t.Elevation[i + n]) : 0.0;
                slope = Math.Max(slope, Math.Max(north, south));
                Assert.Equal(cfg.Movement.BaseCost + cfg.Movement.SlopeFactor * slope, t.MovementCost[i]);
                if (across > inside && across > north && across > south) acrossIsSteepest++;
            }
        Assert.True(seamLand > 0 && acrossIsSteepest > 0, "no seam cell's steepest neighbour lies across the seam — the rig is void");
    }

    [Fact]
    public void Hydrology_TheFloodRaisesACellOnlyToItsLowestNeighbourOnTheCylinder()
    {
        // PRIORITY FLOOD's defining property (Barnes 2014, the heap pops in non-decreasing order): a cell the flood raised
        // above its own elevation sits exactly one FillEpsilon above its LOWEST neighbour — over the 8-neighbourhood ON
        // THE CYLINDER. A flood that did not wrap would raise a seam cell to its lowest in-map neighbour instead, higher
        // than the spill across the seam.
        TerrainSet t = Dev6.Value;
        int n = t.Size;
        Hydrology.Result h = Hydrology.Compute(t.Elevation.ToArray(), t.Water.ToArray(), n, TestConfigs.Worldgen().Rivers, wrapX: true);
        double[] f = h.FilledElevation;
        int raised = 0, spillAcross = 0;
        for (int y = 1; y < n - 1; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (t.Water[i] >= 0.5 || !(f[i] > t.Elevation[i])) continue;
                raised++;
                double min = double.MaxValue, minAcross = double.MaxValue, minInside = double.MaxValue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        bool across = x + dx < 0 || x + dx >= n;
                        double v = f[(y + dy) * n + ((x + dx) % n + n) % n];
                        min = Math.Min(min, v);
                        if (across) minAcross = Math.Min(minAcross, v); else minInside = Math.Min(minInside, v);
                    }
                Assert.Equal(min + 1e-9, f[i]);
                if (minAcross < minInside) spillAcross++;
            }
        Assert.True(raised > 0 && spillAcross > 0, "no raised cell spills across the seam on this rig — it is void");
    }

    // ------------------------------------------------------------------ siting windows and BFS

    [Fact]
    public void BoxSum_InteriorWindowsAreBitIdenticalToFlat_SeamWindowsSumBothPieces_AWideWindowCountsEachColumnOnce()
    {
        const int size = 24, r = 3;
        var field = new double[size * size];
        ulong h = 99;
        for (int i = 0; i < field.Length; i++) { h = Sim.Core.Kernel.SplitMix64.Mix(h + 1); field[i] = (h >> 11) * (1.0 / (1UL << 53)); }
        double[] flat = SettlementSiting.BoxSum(field, size, r);
        double[] wrap = SettlementSiting.BoxSum(field, size, r, wrapX: true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (x - r >= 0 && x + r <= size - 1)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(flat[i]), BitConverter.DoubleToInt64Bits(wrap[i]));   // interior: same bits
                else
                    Assert.Equal(Brute(field, size, x, y, r), wrap[i], 10);   // the seam window, both pieces (sums of ~25 terms)
            }
        // r ≥ size/2: every column once, never twice.
        double[] wide = SettlementSiting.BoxSum(field, size, 40, wrapX: true);
        Assert.Equal(Brute(field, size, 5, 5, size), wide[5 * size + 5], 10);

        static double Brute(double[] f, int n, int cx, int cy, int rad)
        {
            double s = 0.0;
            var seen = new bool[n];
            for (int dy = -rad; dy <= rad; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= n) continue;
                Array.Clear(seen);
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int x = ((cx + dx) % n + n) % n;
                    if (seen[x]) continue;
                    seen[x] = true;
                    s += f[y * n + x];
                }
            }
            return s;
        }
    }

    [Fact]
    public void WaterDistanceBfs_WrapsTheSeam()
    {
        const int size = 16;
        var water = new double[size * size];
        water[5 * size + 0] = 1.0;   // one water cell at the west edge
        int[] wrapped = SettlementSiting.WaterDistanceBfs(water, default, size, wrapX: true);
        int[] flat = SettlementSiting.WaterDistanceBfs(water, default, size);
        Assert.Equal(1, wrapped[5 * size + size - 1]);   // its east-edge neighbour, across the seam
        Assert.Equal(size - 1, flat[5 * size + size - 1]);
        Assert.Equal(flat[5 * size + 3], wrapped[5 * size + 3]);   // the short way is inside the map there
    }

    /// <summary>W1 (verifier's surviving mutant M44) — THE --stats CORRELATION MEASURES THE BOOST IT DESCRIBES, ON THE
    /// CYLINDER TOO. WorldgenStats.FertilityRiverCorrelation's "near a river" set is a Chebyshev BFS that wraps x like the
    /// fertility boost it measures. Rig: the seam world's strip with a river down column 0 and the fertile east edge
    /// (x ≥ Size − 3, fertility 0.9; elsewhere 0.6), which is within 3 px of the river only across the seam. The
    /// statistic must equal the point-biserial correlation computed here from a brute-force near set (modular x on the
    /// cylinder, clipped flat) — and the two topologies must disagree.</summary>
    [Fact]
    public void FertilityRiverCorrelation_ReadsTheRiverAcrossTheSeam()
    {
        const int r = 3;
        double Measure(bool wrap, out double brute)
        {
            TerrainSet t = SeamWorld.Terrain(wrap);
            int n = t.Size;
            double[] rivers = new double[n * n], fertility = t.Fertility.ToArray();
            for (int i = 0; i < n * n; i++)
            {
                if (t.Water[i] >= 0.5) continue;
                if (i % n == 0) rivers[i] = 1.0;
                fertility[i] = i % n >= n - 3 ? 0.9 : 0.6;
            }
            TerrainSet rig = TerrainSet.FromLayers(n, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature, t.Moisture,
                fertility, t.MovementCost, rivers, [], wrap);
            // Brute force: a land cell is near when some river cell is within Chebyshev r (dx modular when wrapping).
            long n0 = 0, n1 = 0;
            double s0 = 0, s1 = 0, sa = 0, sq = 0;
            for (int i = 0; i < n * n; i++)
            {
                if (t.Water[i] >= 0.5) continue;
                int x = i % n, y = i / n;
                bool near = false;
                for (int j = 0; j < n * n && !near; j++)
                {
                    if (rivers[j] < 0.5) continue;
                    int dx = Math.Abs(j % n - x);
                    if (wrap) dx = Math.Min(dx, n - dx);
                    near = Math.Max(dx, Math.Abs(j / n - y)) <= r;
                }
                double f = fertility[i];
                sa += f; sq += f * f;
                if (near) { n1++; s1 += f; } else { n0++; s0 += f; }
            }
            long all = n0 + n1;
            double mean = sa / all, p = n1 / (double)all;
            brute = (s1 / n1 - s0 / n0) / Math.Sqrt(sq / all - mean * mean) * Math.Sqrt(p * (1.0 - p));
            return WorldgenStats.FertilityRiverCorrelation(rig, r);
        }
        double wrapped = Measure(true, out double bruteWrapped);
        double flat = Measure(false, out double bruteFlat);
        Assert.Equal(bruteWrapped, wrapped, 12);
        Assert.Equal(bruteFlat, flat, 12);
        Assert.True(wrapped > flat + 0.05, $"the seam did not move the statistic: wrapped {wrapped}, flat {flat}");
    }
}
