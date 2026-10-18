using Sim.Core.Worldgen;
using Sim.Ui.Art;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Ui.Tests;

/// <summary>
/// BATCH 3 WS3 — THE PARCHMENT SHEET ON A CYLINDER (<see cref="ParchmentBaker"/>). The game draws the sheet with a U-wrapped
/// sampler, so its last column meets its first: the field samples, the land/water gate and the coast's chamfer are
/// x-periodic, and the wash tiles and paper cross-fade over the last <see cref="ParchmentBaker.SeamFadePx"/> world px into
/// the colour the first columns continue from.
/// <list type="bullet">
/// <item>ACCEPTANCE: the seam column pair's ΔRGB is no larger than the largest ΔRGB of the neighbouring interior column pairs
///   over the same rows — on the canonical world and a 256-px world, for every parchment variant (the paper sheet is the
///   bake's only palette input that varies: it takes no era input at all, so its pixels are the same in every era —
///   pinned by EraSurfaceTests.TheTerrainSubstrate_IsByteIdenticalInEveryEra — and one measurement per variant covers every
///   era palette). The same terrain baked by the FLAT path (the control) shows the seam.</item>
/// <item>A FLAT terrain's bake is byte-identical to the pre-WS3 baker's (hashes measured on the base build).</item>
/// </list>
/// What this cannot see is the GPU's sampling of the sheet across the seam (AddressU = Wrap; SimUiGame) — no device runs
/// headless; it is the manual playtest's.
/// <para>W2 review F9 — WHAT THE METRIC IS: a ROW MEAN of the column pair's |ΔR| + |ΔG| + |ΔB|, dominated by the sheet's
/// per-pixel grain (a typical column step is 12.6–17.5). It sees a seam with NO cross-fade (canonical v0 20.45 against a
/// mid-map step of 14.16) but not a PARTIAL one: with the fade capped at half or 80 % of the way, or narrowed to 8 px, every
/// assertion here still holds (MEASURED, W2 review mutants F9a–F9c: the half-faded seam steps 14.60 against 14.16) — the
/// blend of two independent grains is as smooth a step as the grain itself. A low-frequency measure (the mean of 4–16
/// columns either side) puts even the UNFADED seam below the interior's typical step, because the terrain fields — x-periodic
/// — carry the sheet's broad colour. So a partial fade is not a statistically visible seam; whether the eye sees one is the
/// playtest's.</para>
/// </summary>
public class CylinderBakeTests(ITestOutputHelper output)
{
    private static TerrainSet Terrain(int sizePx, bool? wrapX)
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream) with { SizePx = sizePx };
        if (wrapX is { } w) cfg = cfg with { Topology = new TopologyConfig(w) };
        return Worldgen.Generate(cfg, 42);
    }

    /// <summary>The same layers with the other topology (the bake reads TerrainSet.WrapX, nothing else of it).</summary>
    private static TerrainSet Retopologised(TerrainSet t, bool wrapX)
    {
        var lines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < lines.Length; i++) lines[i] = t.RiverPolyline(i).ToArray();
        return TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature, t.Moisture, t.Fertility,
            t.MovementCost, t.Rivers, lines, wrapX);
    }

    /// <summary>Mean over every row of |ΔR| + |ΔG| + |ΔB| between columns <paramref name="a"/> and <paramref name="b"/>.</summary>
    private static double PairDelta(byte[] rgba, int size, int a, int b)
    {
        long sum = 0;
        for (int y = 0; y < size; y++)
        {
            int oa = (y * size + a) * 4, ob = (y * size + b) * 4;
            sum += Math.Abs(rgba[oa] - rgba[ob]) + Math.Abs(rgba[oa + 1] - rgba[ob + 1]) + Math.Abs(rgba[oa + 2] - rgba[ob + 2]);
        }
        return sum / (double)size;
    }

    /// <summary>(the seam pair's ΔRGB, the largest of the neighbouring interior pairs': eight each side of the seam).</summary>
    private static (double Seam, double Interior) SeamAndNeighbours(ParchmentBaker.Result bake)
    {
        int s = bake.Size;
        double seam = PairDelta(bake.Rgba, s, s - 1, 0), interior = 0;
        for (int j = 0; j < 8; j++)
        {
            interior = Math.Max(interior, PairDelta(bake.Rgba, s, j, j + 1));                 // west of the seam's far side
            interior = Math.Max(interior, PairDelta(bake.Rgba, s, s - 2 - j, s - 1 - j));     // east, inside the cross-fade
        }
        return (seam, interior);
    }

    /// <summary>W2 review F9: the mean ΔRGB of the 64 column pairs about the sheet's middle — far from the seam and its
    /// cross-fade, the sheet's typical column step.</summary>
    private static double MidMapStep(ParchmentBaker.Result bake)
    {
        int s = bake.Size, from = s / 2 - 32;
        double sum = 0;
        for (int j = 0; j < 64; j++) sum += PairDelta(bake.Rgba, s, from + j, from + j + 1);
        return sum / 64.0;
    }

    /// <summary>W2 review F9: how far above the sheet's typical column step (<see cref="MidMapStep"/>) the seam's step may be.
    /// The acceptance (no larger than the largest neighbouring pair) is a row mean whose "neighbours" include pairs inside
    /// the cross-fade; this second bar holds the seam to a typical step measured far from it.</summary>
    public const double SeamOverTypicalStep = 1.10;

    [Theory]
    [InlineData(1024)]
    [InlineData(256)]
    public void TheSeamColumn_IsNoBrighterAStepThanItsInteriorNeighbours_ForEveryParchmentVariant(int sizePx)
    {
        TerrainSet wrapped = Terrain(sizePx, wrapX: true);
        Assert.True(wrapped.WrapX);
        AssetLibrary art = AssetLibrary.Load();
        for (ulong variant = 0; variant < (ulong)AssetManifest.ParchmentVariants; variant++)
        {
            ParchmentBaker.Result bake = ParchmentBaker.Bake(wrapped, art, variant);
            (double seam, double interior) = SeamAndNeighbours(bake);
            double typical = MidMapStep(bake);
            output.WriteLine($"{sizePx}px variant {variant}: seam ΔRGB {seam:0.00}, neighbouring interior max {interior:0.00}, mid-map mean {typical:0.00}");
            Assert.True(seam <= interior, $"{sizePx}px variant {variant}: the seam (ΔRGB {seam:0.00}) shows against its neighbours ({interior:0.00})");
            Assert.True(seam <= SeamOverTypicalStep * typical,
                $"{sizePx}px variant {variant}: the seam (ΔRGB {seam:0.00}) is more than {SeamOverTypicalStep:0.00}x the mid-map step ({typical:0.00})");
        }
        // CONTROL: the same terrain baked FLAT (no periodic samples, no cross-fade) shows the seam — the test has teeth.
        ParchmentBaker.Result flat = ParchmentBaker.Bake(Retopologised(wrapped, false), art, 0);
        (double flatSeam, double flatInterior) = SeamAndNeighbours(flat);
        double flatTypical = MidMapStep(flat);
        output.WriteLine($"{sizePx}px flat control: seam ΔRGB {flatSeam:0.00}, neighbouring interior max {flatInterior:0.00}, mid-map mean {flatTypical:0.00}");
        Assert.True(flatSeam > flatInterior, $"{sizePx}px: the flat bake's seam ({flatSeam:0.00}) is no worse than its neighbours ({flatInterior:0.00}) — the control is void");
        Assert.True(flatSeam > SeamOverTypicalStep * flatTypical, $"{sizePx}px: the flat bake's seam ({flatSeam:0.00}) is a typical step ({flatTypical:0.00}) — the control is void");
    }

    /// <summary>The pre-WS3 baker's output for the FLAT world, measured on the base build (de86ace + batch 2, whose baker and
    /// flat worldgen this change leaves alone): the terrain's ContentHash and the SHA-256 of the sheet's RGBA.</summary>
    [Theory]
    [InlineData(128, "8285174604ce32837c18744e4911543683a1897d0199ed6885182a48bacba259", "1055c1fe4e20740c8176c84265ef820ed4fed827d9f6297ff69e1ac0f4a84229")]
    [InlineData(256, "76c73cf4af075e7beb04e3222b654adaecac4092e8e71a70c1c5ebbecd86aec1", "95d577347d6551df0330ba6184b8981b50fbd077b14d2567b1d2e44dbf24b8cf")]
    public void AFlatWorld_BakesByteIdenticallyToThePreWs3Baker(int sizePx, string terrainHash, string bakeHash)
    {
        TerrainSet flat = Terrain(sizePx, wrapX: false);
        Assert.False(flat.WrapX);
        Assert.Equal(terrainHash, Convert.ToHexStringLower(flat.ContentHash));
        ParchmentBaker.Result bake = ParchmentBaker.Bake(flat, AssetLibrary.Load(), 42);
        Assert.Equal(bakeHash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bake.Rgba)));
    }

    [Fact]
    public void TheCoastDistance_SeesItsNeighboursAcrossTheSeam()
    {
        const int n = 64;
        var water = new double[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) water[y * n + x] = x < 6 ? 0.0 : 1.0;   // land at the western edge only
        float[] wrapped = ParchmentBaker.CoastDistance(water, n, wrapX: true);
        float[] flat = ParchmentBaker.CoastDistance(water, n);
        int row = 32 * n;
        // The last column is water against the land across the seam: a coast on the cylinder, far from one when flat.
        Assert.Equal(0f, wrapped[row + n - 1]);
        Assert.True(flat[row + n - 1] > 50f, $"flat distance {flat[row + n - 1]}");
        Assert.Equal(1f, wrapped[row + n - 2]);
        // Away from the seam the two agree (the land's eastern shore at x = 5 / 6).
        Assert.Equal(flat[row + 5], wrapped[row + 5]);
        Assert.Equal(flat[row + 20], wrapped[row + 20]);
        // A flat call of the wrapped overload is the flat chamfer exactly.
        Assert.Equal(flat, ParchmentBaker.CoastDistance(water, n, wrapX: false));
    }

    /// <summary>
    /// Batch 3 R3 (W2 review F2) — THE BAKE INKS THE COAST FROM THE WRAPPED CHAMFER. A 64-px cylinder whose only land is a
    /// strip at the western edge (x &lt; 6), so its western shore IS the seam: the water at x = 63 meets the land at x = 0.
    /// The sheet is baked twice — as shipped, and with the coast ink and the engraved sea switched off
    /// (<see cref="ParchmentBaker.Options"/> CoastInkPx = EngravedBandPx = −1: no distance qualifies) — and at EVERY texel
    /// the shipped bake must be the bare one with the ink of the WRAPPED coast distance applied (the bake's own formula: a
    /// land texel within CoastInkPx of the coast pulled toward the ink by 0.72·(1 − d/CoastInkPx)², a water texel within the
    /// engraved band hatched toward the soft ink), to the byte's rounding. The flat chamfer differs from the wrapped one
    /// exactly in the seam band (the control: those texels exist, and nowhere else differs) — so a bake that took the FLAT
    /// coast distance (W2 review mutant V07, which the ΔRGB acceptance cannot see: on seed 16 it moved 118 seam texels by up
    /// to 27) leaves the seam's coast uninked and fails here.
    /// </summary>
    [Fact]
    public void TheBake_InksTheCoastAtTheSeam_FromTheWrappedChamfer_AtEveryTexel()
    {
        const int n = 64, strip = 6;
        int cells = n * n;
        var elevation = new double[cells];
        var water = new double[cells];
        var half = new double[cells];
        var one = new double[cells];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                bool land = x < strip;
                elevation[i] = land ? 0.7 : 0.2;
                water[i] = land ? 0.0 : 1.0;
                half[i] = 0.5;
                one[i] = 1.0;
            }
        TerrainSet t = TerrainSet.FromLayers(n, 4.0, 0.5, elevation, water, half, half, half, one, new double[cells], [], wrapX: true);
        Assert.True(t.WrapX);
        AssetLibrary art = AssetLibrary.Load();
        ParchmentBaker.Options o = ParchmentBaker.Options.Default;
        ParchmentBaker.Result inked = ParchmentBaker.Bake(t, art, 0);
        ParchmentBaker.Result bare = ParchmentBaker.Bake(t, art, 0, o with { CoastInkPx = -1, EngravedBandPx = -1 });
        float[] wrapped = ParchmentBaker.CoastDistance(water, n, wrapX: true), flat = ParchmentBaker.CoastDistance(water, n);
        int size = inked.Size, ss = size / n;
        Assert.Equal(n * ss, size);

        // The ink the bake's own formula lays on texel colour c at coast distance d (land: the coast ink; water: the hatching).
        static double Ink(bool isWater, double d, ParchmentBaker.Options o)
        {
            if (!isWater) { if (d > o.CoastInkPx) return 0; double k = 1.0 - d / o.CoastInkPx; return 0.72 * k * k; }
            if (d > o.EngravedBandPx) return 0;
            double phase = d / (o.EngravedBandPx / 3.0);
            double line = Math.Max(0.0, 1.0 - Math.Abs(phase - Math.Round(phase)) * 6.0);
            double fade = 1.0 - d / o.EngravedBandPx;
            return 0.16 * line * fade * fade;
        }

        int judged = 0, seamOnly = 0, clipped = 0;
        double seamBand = o.CoastInkPx + 2.0;   // world px either side of the seam where the two chamfers can disagree near land
        for (int ty = 0; ty < size; ty++)
            for (int tx = 0; tx < size; tx++)
            {
                double wx = (tx + 0.5) / ss - 0.5, wy = (ty + 0.5) / ss - 0.5;
                int off = (ty * size + tx) * 4;
                bool isWater = ParchmentBaker.IsWaterAt(water, n, wx, wy, wrapX: true);
                double dW = ParchmentBaker.SampleF(wrapped, n, wx, wy, wrapX: true), dF = ParchmentBaker.SampleF(flat, n, wx, wy, wrapX: true);
                double inkW = Ink(isWater, dW, o), inkF = Ink(isWater, dF, o);
                ParchmentPalette.Rgba to = isWater ? ParchmentPalette.InkSoft : ParchmentPalette.InkPrimary;
                bool anyClipped = false;
                for (int ch = 0; ch < 3; ch++) anyClipped |= bare.Rgba[off + ch] is 0 or 255;
                if (anyClipped) { clipped++; continue; }   // the bare byte no longer carries the colour the ink was laid on
                judged++;
                for (int ch = 0; ch < 3; ch++)
                {
                    double c = bare.Rgba[off + ch], target = ch == 0 ? to.R : ch == 1 ? to.G : to.B;
                    double want = c + (target - c) * inkW;
                    Assert.True(Math.Abs(inked.Rgba[off + ch] - want) <= 1.0,
                        $"texel ({tx},{ty}) world ({wx:0.00},{wy:0.00}) {(isWater ? "water" : "land")} channel {ch}: baked {inked.Rgba[off + ch]}, "
                        + $"the wrapped chamfer's ink ({dW:0.00} px) gives {want:0.0} (the flat chamfer: {dF:0.00} px)");
                }
                // Where the flat chamfer would ink differently by more than the byte's rounding, the texel is in the seam band.
                double diff = 0;
                for (int ch = 0; ch < 3; ch++)
                {
                    double c = bare.Rgba[off + ch], target = ch == 0 ? to.R : ch == 1 ? to.G : to.B;
                    diff = Math.Max(diff, Math.Abs((target - c) * (inkW - inkF)));
                }
                if (diff > 2.0)
                {
                    seamOnly++;
                    double fromSeam = Math.Min(wx + 0.5, n - 0.5 - wx);
                    Assert.True(fromSeam <= seamBand || isWater && fromSeam <= o.EngravedBandPx + 1,
                        $"texel ({tx},{ty}) is {fromSeam:0.0} px from the seam, yet the flat chamfer would ink it differently");
                }
            }
        output.WriteLine($"judged {judged} texels ({clipped} clipped), {seamOnly} inked differently by the flat chamfer");
        Assert.True(judged > size * size * 9 / 10, $"only {judged} of {size * size} texels judged");
        Assert.True(seamOnly >= 2 * n, $"only {seamOnly} texels tell the wrapped chamfer from the flat one: the rig is void");
    }

    [Fact]
    public void TheFieldSamples_ArePeriodicInX_AndClampedInY()
    {
        const int n = 8;
        var field = new double[n * n];
        var f = new float[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) { field[y * n + x] = x + 10 * y; f[y * n + x] = x + 10 * y; }
        // Half a cell west of column 0 is halfway between column 7 (across the seam) and column 0.
        Assert.Equal(3.5 + 30, ParchmentBaker.Sample(field, n, -0.5, 3.0, wrapX: true));
        Assert.Equal(3.5 + 30, ParchmentBaker.Sample(field, n, 7.5, 3.0, wrapX: true));    // the same place from the east
        Assert.Equal(3.5 + 30, ParchmentBaker.SampleF(f, n, 7.5, 3.0, wrapX: true));
        Assert.Equal(0.0 + 30, ParchmentBaker.Sample(field, n, -0.5, 3.0, wrapX: false));  // flat: clamped onto column 0
        Assert.Equal(7.0 + 30, ParchmentBaker.Sample(field, n, 7.5, 3.0, wrapX: false));   // flat: clamped onto column 7
        // y keeps its edge: past the last row is the last row, wrapped or not.
        Assert.Equal(ParchmentBaker.Sample(field, n, 2.0, 7.0, wrapX: true), ParchmentBaker.Sample(field, n, 2.0, 7.9, wrapX: true));
        Assert.Equal(2.0 + 70, ParchmentBaker.Sample(field, n, 2.0, 7.9, wrapX: true));
    }

    [Fact]
    public void TheWaterGateAndTheDisplayedTexel_WrapInX_AndClampInY()
    {
        const int n = 8;
        var water = new double[n * n];
        water[3 * n + 0] = 1.0;   // one water cell at the western edge
        Assert.True(ParchmentBaker.IsWaterAt(water, n, -0.4, 3.0, wrapX: false));    // flat: clamped onto column 0
        Assert.True(ParchmentBaker.IsWaterAt(water, n, 7.6, 3.0, wrapX: true));      // wrapped: 7.6 rounds to column 8 ≡ 0
        Assert.False(ParchmentBaker.IsWaterAt(water, n, 7.4, 3.0, wrapX: true));     // column 7
        Assert.False(ParchmentBaker.IsWaterAt(water, n, 7.6, 3.0, wrapX: false));    // flat: clamped onto column 7
        Assert.Equal(511, ParchmentBaker.DisplayedTexel(-0.1, 256, 512, wrapX: true));
        Assert.Equal(0, ParchmentBaker.DisplayedTexel(256.0, 256, 512, wrapX: true));
        Assert.Equal(0, ParchmentBaker.DisplayedTexel(-0.1, 256, 512, wrapX: false));
        Assert.Equal(511, ParchmentBaker.DisplayedTexel(256.0, 256, 512, wrapX: false));
        Assert.Equal(ParchmentBaker.DisplayedTexel(100.3, 256, 512), ParchmentBaker.DisplayedTexel(100.3, 256, 512, wrapX: true));
    }
}
