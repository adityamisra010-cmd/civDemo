using Sim.Core.Worldgen;
using Sim.Ui.Art;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Ui.Tests;

// The bake itself, headless (no window, no MonoGame): it runs on the REAL
// canonical terrain, produces an in-gamut painted sheet, inks its coasts, and
// costs what the packet says it costs.
public class ParchmentBakeTests(ITestOutputHelper output)
{
    private static int Pixel(ParchmentBaker.Result bake, int x, int y)
    {
        int o = (y * bake.Size + x) * 4;
        return (bake.Rgba[o] << 16) | (bake.Rgba[o + 1] << 8) | bake.Rgba[o + 2];
    }

    /// <summary>
    /// Finds 8×8 terrain patches whose elevation/moisture/fertility are
    /// essentially CONSTANT, and returns the mean luminance σ inside them for
    /// the parchment bake and for the T1.7 flat-fill bake. Any variation there
    /// comes from the PAPER, not the world.
    /// </summary>
    private static (double Parchment, double FlatFill, int Patches) TextureInFeaturelessRegions(
        TerrainSet terrain, ParchmentBaker.Result bake, byte[] flatFills)
    {
        int n = terrain.Size, ss = bake.Size / n;
        ReadOnlySpan<double> elev = terrain.Elevation, moist = terrain.Moisture, fert = terrain.Fertility;
        double parchmentSum = 0, flatSum = 0;
        int patches = 0;
        const int P = 8;
        var spans = new List<(double E, double M, double F, int X, int Y)>();
        for (int py = 0; py + P < n; py += P)
        {
            for (int px = 0; px + P < n; px += P)
            {
                double eMin = double.MaxValue, eMax = double.MinValue;
                double mMin = double.MaxValue, mMax = double.MinValue;
                double fMin = double.MaxValue, fMax = double.MinValue;
                for (int y = py; y < py + P; y++)
                    for (int x = px; x < px + P; x++)
                    {
                        int i = y * n + x;
                        eMin = Math.Min(eMin, elev[i]); eMax = Math.Max(eMax, elev[i]);
                        mMin = Math.Min(mMin, moist[i]); mMax = Math.Max(mMax, moist[i]);
                        fMin = Math.Min(fMin, fert[i]); fMax = Math.Max(fMax, fert[i]);
                    }
                spans.Add((eMax - eMin, mMax - mMin, fMax - fMin, px, py));
            }
        }
        // The FLATTEST tenth of the world by combined field span — "featureless"
        // defined relative to this terrain rather than by an invented constant.
        spans.Sort((a, b) => (a.E * 50 + a.M + a.F).CompareTo(b.E * 50 + b.M + b.F));
        int take = Math.Max(1, spans.Count / 10);
        for (int i = 0; i < take; i++)
        {
            (_, _, _, int px, int py) = spans[i];
            patches++;
            parchmentSum += LuminanceSigma(bake.Rgba, bake.Size, px * ss, py * ss, P * ss);
            flatSum += LuminanceSigma(flatFills, n, px, py, P);
        }
        return patches == 0 ? (0, 0, 0) : (parchmentSum / patches, flatSum / patches, patches);
    }

    private static double LuminanceSigma(byte[] rgba, int size, int x0, int y0, int span)
    {
        double sum = 0, sumSq = 0; int n = 0;
        for (int y = y0; y < y0 + span && y < size; y++)
            for (int x = x0; x < x0 + span && x < size; x++)
            {
                int o = (y * size + x) * 4;
                double lum = 0.2126 * rgba[o] + 0.7152 * rgba[o + 1] + 0.0722 * rgba[o + 2];
                sum += lum; sumSq += lum * lum; n++;
            }
        if (n == 0) return 0.0;
        double mean = sum / n;
        return Math.Sqrt(Math.Max(0.0, sumSq / n - mean * mean));
    }

    private static int At(byte[] rgba, int size, int x, int y)
    {
        int o = (y * size + x) * 4;
        return (rgba[o] << 16) | (rgba[o + 1] << 8) | rgba[o + 2];
    }

    private static TerrainSet Terrain(int sizePx)
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream) with { SizePx = sizePx };
        return Worldgen.Generate(cfg, 42);
    }

    [Fact]
    public void Bake_ProducesAnInGamutSheet_WithNoFlatColourBlocks()
    {
        TerrainSet terrain = Terrain(256);
        ParchmentBaker.Result bake = ParchmentBaker.Bake(terrain, AssetLibrary.Load(), 42);

        Assert.Equal(256 * ParchmentBaker.Options.Default.Supersample, bake.Size);
        Assert.Equal(bake.Size * bake.Size * 4, bake.Rgba.Length);

        // (a) IN GAMUT (§1 single-cartographer rule): no baked pixel may be
        // more saturated than the bible's own inks.
        double gamut = ParchmentPalette.MaxBibleSaturation() + 0.06;
        var distinct = new HashSet<int>();
        for (int i = 0; i < bake.Size * bake.Size; i++)
        {
            int o = i * 4;
            var c = new ParchmentPalette.Rgba(bake.Rgba[o], bake.Rgba[o + 1], bake.Rgba[o + 2]);
            Assert.True(ParchmentPalette.Saturation(c) <= gamut,
                $"baked pixel #{i} rgb({c.R},{c.G},{c.B}) left the parchment gamut");
            Assert.Equal(255, bake.Rgba[o + 3]);
            distinct.Add((c.R << 16) | (c.G << 8) | c.B);
        }

        // (b) THE PAPER PRINTS THROUGH (§6: "renders as painted parchment ...
        // no flat color blocks"). The instrument is a comparison against THE
        // RENDERER THIS REPLACES, on the same terrain, in the places where the
        // difference must show: patches where the worldgen fields are nearly
        // CONSTANT (open ocean, plateau interiors). The T1.7 hypsometric baker
        // maps fields → colour, so a featureless region comes out featureless;
        // the parchment bake carries the sheet's own mottle and fibre there.
        // (Measured note: the old renderer is NOT blocky where terrain varies —
        // its gradient tracks the elevation noise — so a naive flatness count
        // proves nothing. This does.)
        byte[] oldFills = Sim.Ui.ViewModel.TerrainBaker.Bake(terrain);
        (double parchmentTexture, double flatFillTexture, int patches) =
            TextureInFeaturelessRegions(terrain, bake, oldFills);
        output.WriteLine($"bake {bake.Size}²: {distinct.Count} distinct colours; " +
                         $"paper tooth in {patches} featureless patches — parchment σ {parchmentTexture:F2} " +
                         $"vs T1.7 flat-fills σ {flatFillTexture:F2}; " +
                         $"{bake.MegabytesResident:F1} MB, {bake.BakeMilliseconds:F0} ms");
        Assert.True(patches > 50, "no featureless terrain patches found — the texture probe is vacuous");
        // Bar: 2× the baseline. MEASURED at 2.3× (σ 2.41 vs 1.05) with paper
        // contrast that reads as an aged sheet; the bar is not set higher
        // because §1/§4 call for a SUBTLE substrate — a stand-in loud enough
        // to hit 3× would out-shout the director's real art, which is the
        // thing this placeholder exists to rehearse, not to replace.
        Assert.True(parchmentTexture > flatFillTexture * 2.0,
            $"where the terrain is featureless the parchment sheet shows σ {parchmentTexture:F2} vs the " +
            $"flat-fill renderer's σ {flatFillTexture:F2} — the paper is not printing through");
    }

    [Fact]
    public void Bake_InksTheCoast_DarkerThanBothTheLandAndTheSea()
    {
        // §4 item 4: a thin darker ink band exactly where land meets sea. The
        // shoreline's mean luminance must sit below BOTH of its IMMEDIATE
        // neighbours — the land just inland and the water just offshore. That
        // is what "inked coastline" means: the line reads against what it
        // borders.
        //
        // REFERENCE CORRECTED at the T3.1 merge (measured, stated): this test
        // used to compare the coast ink against OPEN SEA sampled 40+ cells
        // offshore. That is the DEEP wash, which is dark by design (the deep
        // tile's mean luminance is ~95) and is not what the coastline borders.
        // T3.1's edge taper pushes land inward and creates more genuine deep
        // ocean in this 256-px test terrain, which exposed the mis-specified
        // reference: measured coast 107.8, inland 168.9, NEAR-SHORE water
        // 160.9, open sea 112.2. Against its true neighbours the coastline is
        // inked by 61 points (land) and 53 points (near-shore water) - far
        // stronger than the 8-point bar. Against the deep ocean it is 4.4
        // darker, which is a fact about the deep wash, not about the ink. The
        // deep-ocean comparison is kept as a weaker directional check.
        TerrainSet terrain = Terrain(256);
        ParchmentBaker.Result bake = ParchmentBaker.Bake(terrain, AssetLibrary.Load(), 42);
        float[] distance = ParchmentBaker.CoastDistance(terrain.Water, terrain.Size);
        int ss = ParchmentBaker.Options.Default.Supersample;

        double coastSum = 0, landSum = 0, seaSum = 0, nearSum = 0;
        int coastN = 0, landN = 0, seaN = 0, nearN = 0;
        ReadOnlySpan<double> water = terrain.Water;
        for (int y = 0; y < terrain.Size; y++)
        {
            for (int x = 0; x < terrain.Size; x++)
            {
                int t = y * terrain.Size + x;
                int o = ((y * ss) * bake.Size + (x * ss)) * 4;
                double lum = 0.2126 * bake.Rgba[o] + 0.7152 * bake.Rgba[o + 1] + 0.0722 * bake.Rgba[o + 2];
                bool isWater = water[t] >= 0.5;
                if (!isWater && distance[t] <= 1.0) { coastSum += lum; coastN++; }
                else if (!isWater && distance[t] > 8.0) { landSum += lum; landN++; }
                else if (isWater && distance[t] > 40.0) { seaSum += lum; seaN++; }
                else if (isWater && distance[t] > 2.0 && distance[t] <= 8.0) { nearSum += lum; nearN++; }
            }
        }
        Assert.True(coastN > 100 && landN > 100 && seaN > 100, "coast sampling was vacuous");
        double coast = coastSum / coastN, land = landSum / landN, sea = seaSum / seaN;
        double near = nearN > 0 ? nearSum / nearN : double.NaN;
        output.WriteLine($"luminance — coast {coast:F1}, inland {land:F1}, near-shore water {near:F1} (n={nearN}), open sea {sea:F1}");
        Assert.True(nearN > 100, "near-shore water sampling was vacuous");
        Assert.True(coast < land - 8.0, $"coast ({coast:F1}) is not inked against the land ({land:F1})");
        Assert.True(coast < near - 8.0,
            $"coast ({coast:F1}) is not inked against the water it borders ({near:F1})");
        // Directional only: the ink must never be BRIGHTER than the deep wash.
        Assert.True(coast < sea, $"coast ({coast:F1}) is brighter than the open sea ({sea:F1})");
    }

    [Fact]
    public void Bake_IsDeterministic_DespiteTheParallelLoop()
    {
        // Rows are independent; the parallel bake must be byte-identical to
        // itself run to run, or a gate build would differ from its own rerun.
        TerrainSet terrain = Terrain(128);
        AssetLibrary art = AssetLibrary.Load();
        Assert.Equal(
            ParchmentBaker.Bake(terrain, art, 7).Rgba,
            ParchmentBaker.Bake(terrain, art, 7).Rgba);
    }

    [Fact]
    public void Bake_HonoursTheSeedChosenParchmentVariant()
    {
        // Bible §4 item 1: "renderer picks one per world seed". With a
        // MULTI-sheet drop two seeds must land on different paper; with the
        // director's single-sheet drop every seed shares it by design, so the
        // multi-variant case is exercised against a generated three-sheet root.
        TerrainSet terrain = Terrain(128);
        string root = Path.Combine(Path.GetTempPath(), $"art-bakevar-{Guid.NewGuid():N}");
        try
        {
            PlaceholderArt.GenerateMissing(root);
            AssetLibrary multi = AssetLibrary.Load(root);
            Assert.Equal(AssetManifest.ParchmentVariants, multi.ParchmentVariantCount);
            Assert.NotEqual(
                ParchmentBaker.Bake(terrain, multi, 0).Rgba,
                ParchmentBaker.Bake(terrain, multi, 1).Rgba);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

        // And on the SHIPPED drop, the contract is that two seeds landing on
        // DIFFERENT SHEETS bake differently and two seeds landing on the SAME
        // sheet bake identically. Stated in terms of sheets, not variant
        // indices, because a drop may legitimately contain duplicate files:
        // MEASURED on the current drop, base1.png and base2.png are BYTE-
        // IDENTICAL (md5 0b9c2ffc…), so variants 1 and 2 are the same paper
        // and only two DISTINCT sheets exist. Reported to the director; the
        // renderer is correct either way, so this test asserts what istrue
        // of any drop rather than assuming three distinct sheets.
        AssetLibrary shipped = AssetLibrary.Load();
        for (ulong a = 0; a < 6; a++)
        {
            for (ulong b = 0; b < 6; b++)
            {
                bool sameSheet = shipped.ParchmentFor(a).Rgba.AsSpan()
                    .SequenceEqual(shipped.ParchmentFor(b).Rgba);
                bool sameBake = ParchmentBaker.Bake(terrain, shipped, a).Rgba.AsSpan()
                    .SequenceEqual(ParchmentBaker.Bake(terrain, shipped, b).Rgba);
                Assert.True(sameSheet == sameBake,
                    $"seeds {a}/{b}: same sheet = {sameSheet} but same bake = {sameBake} — " +
                    "the seed→paper choice is not reaching the bake");
            }
        }
    }

    [Fact]
    public void Bake_MissingArt_StillPaints_UsingPlaceholders()
    {
        // The no-crash promise end to end: an EMPTY assets root still yields a
        // complete, in-gamut sheet (every tile substituted).
        string root = Path.Combine(Path.GetTempPath(), $"art-bake-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            AssetLibrary empty = AssetLibrary.Load(root);
            int required = AssetManifest.All.Count(e =>
                !(e.Kind == AssetManifest.AssetKind.ParchmentBase && e.Variant > 0));
            Assert.Equal(required, empty.PlaceholderCount);
            ParchmentBaker.Result bake = ParchmentBaker.Bake(Terrain(128), empty, 42);
            Assert.Equal(128 * ParchmentBaker.Options.Default.Supersample, bake.Size);
            bool anyInk = false;
            for (int i = 0; i < bake.Rgba.Length; i += 4) if (bake.Rgba[i] > 0) { anyInk = true; break; }
            Assert.True(anyInk, "the placeholder bake produced an empty sheet");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Bake_CanonicalWorld_FitsTheStatedBudget()
    {
        // The packet's perf claim, measured rather than asserted from theory:
        // the canonical 1024² world bakes ONCE at startup (alongside the ~3 s
        // worldgen) into a fixed 16 MB texture. Nothing here runs per frame,
        // so the 60 fps budget is untouched by construction — the frame cost
        // of the substrate is one textured quad plus one multiply pass.
        TerrainSet terrain = Terrain(1024);
        ParchmentBaker.Result bake = ParchmentBaker.Bake(terrain, AssetLibrary.Load(), 42);
        output.WriteLine($"CANONICAL 1024² → {bake.Size}² sheet, " +
                         $"{bake.MegabytesResident:F1} MB resident, baked in {bake.BakeMilliseconds:F0} ms");
        Assert.Equal(16.0, bake.MegabytesResident, 1);
        Assert.True(bake.BakeMilliseconds < 20_000,
            $"bake took {bake.BakeMilliseconds:F0} ms — startup budget blown");
    }
}
