using Sim.Ui.Art;
using Xunit;

namespace Sim.Ui.Tests;

// ART SUBSTRATE PACKET — headless acceptance for the parchment-atlas renderer.
// Everything here runs without a window (no MonoGame types cross this line):
// splat-weight math, asset-manifest loading, missing-asset fallback, palette
// mapping, and the two style-bible clauses the director added — the
// SINGLE-CARTOGRAPHER RULE (§1: no asset invents a color) and the SEAMLESS
// CLAUSE (§4/§5: tileable art must edge-wrap), both enforced as TESTS rather
// than as prose nobody re-reads.
public class ArtSubstrateTests
{
    // --- palette mapping (§2) ------------------------------------------------

    [Fact]
    public void Palette_MatchesTheStyleBibleHexes_Exactly()
    {
        // The bible is authoritative; these are its §2 values transcribed. A
        // drifted constant is a drifted map — pin every one.
        Assert.Equal((0xEF, 0xE3, 0xC8), Rgb(ParchmentPalette.PaperLight));
        Assert.Equal((0xE3, 0xD3, 0xAE), Rgb(ParchmentPalette.PaperMid));
        Assert.Equal((0xC9, 0xB5, 0x88), Rgb(ParchmentPalette.PaperShade));
        Assert.Equal((0x3A, 0x2E, 0x1F), Rgb(ParchmentPalette.InkPrimary));
        Assert.Equal((0x6B, 0x5A, 0x3E), Rgb(ParchmentPalette.InkSoft));
        Assert.Equal((0xA9, 0xB0, 0x80), Rgb(ParchmentPalette.LowlandGreen));
        Assert.Equal((0x8F, 0x9C, 0x63), Rgb(ParchmentPalette.FertileGreen));
        Assert.Equal((0xC4, 0xB1, 0x83), Rgb(ParchmentPalette.PlainTan));
        Assert.Equal((0xCB, 0xBE, 0x93), Rgb(ParchmentPalette.Arid));
        Assert.Equal((0xA9, 0x8B, 0x63), Rgb(ParchmentPalette.UplandUmber));
        Assert.Equal((0xDC, 0xC9, 0xA0), Rgb(ParchmentPalette.PeakPale));
        Assert.Equal((0x9D, 0xB3, 0xB0), Rgb(ParchmentPalette.Shallows));
        Assert.Equal((0x7C, 0x99, 0xA0), Rgb(ParchmentPalette.Sea));
        Assert.Equal((0x5F, 0x7E, 0x88), Rgb(ParchmentPalette.DeepSea));
        Assert.Equal((0x6E, 0x8A, 0x93), Rgb(ParchmentPalette.River));
        Assert.Equal((0x8C, 0x4A, 0x3A), Rgb(ParchmentPalette.IronRed));
        Assert.Equal((0x5E, 0x7A, 0x6B), Rgb(ParchmentPalette.Verdigris));
        Assert.Equal((0xB0, 0x8A, 0x3E), Rgb(ParchmentPalette.GoldLeaf));

        // Every terrain class resolves to a distinct in-palette wash.
        var seen = new HashSet<(int, int, int)>();
        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
            Assert.True(seen.Add(Rgb(ParchmentPalette.Of((ParchmentPalette.TerrainClass)c))),
                $"terrain class {c} duplicates another class's color");
    }

    [Fact]
    public void TerritoryTints_AreInkWashes_MutedAndWarmBiased()
    {
        // §2 rule: territory tints are ink washes over parchment at ~35%, not
        // opaque fills, and "if a swatch looks vivid, it's wrong". Two bars:
        // the PLATE must stay inside the gamut the bible itself declares (its
        // most saturated sanctioned ink is gold-leaf — iron-red for borders is
        // explicitly sanctioned, so an arbitrary stricter bar would be MY
        // opinion, not the bible's); and the RENDERED wash — what the director
        // actually sees, plate over parchment at 35% — must be muted outright.
        Assert.Equal(0.35, ParchmentPalette.TerritoryWashStrength, 3);
        double gamut = ParchmentPalette.MaxBibleSaturation();
        var seen = new HashSet<(int, int, int)>();
        for (int id = 0; id < 12; id++)
        {
            ParchmentPalette.Rgba plate = ParchmentPalette.TerritoryInk(id);
            Assert.True(seen.Add(Rgb(plate)), $"settlement {id} duplicates another plate");
            Assert.True(ParchmentPalette.Saturation(plate) <= gamut,
                $"plate {id} saturation {ParchmentPalette.Saturation(plate):F2} exceeds the §2 gamut {gamut:F2}");

            // The rendered wash must not out-shout the map it lies on: the
            // bar is the bible's OWN most saturated land wash (upland umber),
            // so a territory can never look more vivid than the highlands.
            ParchmentPalette.Rgba wash = ParchmentPalette.TerritoryWashOverPaper(id);
            Assert.True(ParchmentPalette.Saturation(wash) <= ParchmentPalette.MaxTerrainWashSaturation(),
                $"territory {id} reads more vivid over parchment ({ParchmentPalette.Saturation(wash):F2}) " +
                $"than the bible's own land washes ({ParchmentPalette.MaxTerrainWashSaturation():F2}) — §2 violation");
            Assert.True(wash.R >= wash.B - 6,
                $"territory {id} reads cool over parchment — the palette is warm-biased");
        }
        // Deterministic and total: negative and huge ids still resolve.
        Assert.Equal(ParchmentPalette.TerritoryInk(0), ParchmentPalette.TerritoryInk(12));
        Assert.Equal(ParchmentPalette.TerritoryInk(11), ParchmentPalette.TerritoryInk(-1));
    }

    // --- splat-weight math ---------------------------------------------------

    [Fact]
    public void SplatWeights_SumToOne_AndAreAllNonNegative()
    {
        Span<double> w = stackalloc double[ParchmentPalette.TerrainClassCount];
        for (int i = 0; i <= 20; i++)
        {
            for (int j = 0; j <= 4; j++)
            {
                double e = i / 20.0, m = j / 4.0;
                foreach (bool water in new[] { false, true })
                {
                    TerrainSplat.Weights(e, e, m, 1.0 - m, water, w);
                    double sum = 0;
                    foreach (double x in w) { Assert.True(x >= 0.0); sum += x; }
                    Assert.Equal(1.0, sum, 9);
                }
            }
        }
    }

    [Fact]
    public void SplatWeights_LandAndWaterNeverMix()
    {
        // Water samples must not paint land washes and vice versa — the one
        // hard boundary in the system (the shoreline), and the ink band draws
        // exactly there.
        Span<double> w = stackalloc double[ParchmentPalette.TerrainClassCount];
        TerrainSplat.Weights(0.4, 0.0, 0.5, 0.5, isWater: false, w);
        for (int c = (int)ParchmentPalette.TerrainClass.Shallows; c < w.Length; c++)
            Assert.Equal(0.0, w[c]);

        TerrainSplat.Weights(0.0, 0.4, 0.5, 0.5, isWater: true, w);
        for (int c = 0; c < (int)ParchmentPalette.TerrainClass.Shallows; c++)
            Assert.Equal(0.0, w[c]);
    }

    [Fact]
    public void SplatWeights_AreContinuous_WithinEachDomain_AndDiscontinuousAtTheCoastline()
    {
        // THE AMENDED CONTRACT (director's coastal-defect ruling): continuity
        // is required WITHIN land and WITHIN water — a small change in the
        // fields may only produce a small change in the weight vector, so no
        // contour banding, no stair-stepping ("if elev > x then class y"
        // thresholding fails this immediately). But the land/water boundary
        // is the ONE discontinuity that is physically real, and it is
        // REQUIRED: crossing the mask must switch the blend completely from
        // the land wash set to the water wash set — the transition falls
        // exactly at the coastline, where the coast ink already draws. A
        // splat that smoothed across the mask would paint water washes onto
        // low-lying land (the defect this amendment was cut against).
        Span<double> a = stackalloc double[ParchmentPalette.TerrainClassCount];
        Span<double> b = stackalloc double[ParchmentPalette.TerrainClassCount];
        const double step = 1e-4;
        for (int i = 0; i < 200; i++)
        {
            // WITHIN LAND: sweep the whole above-sea elevation range.
            double e = i / 200.0;
            TerrainSplat.Weights(e, 0.5, 0.5, 0.5, false, a);
            TerrainSplat.Weights(e + step, 0.5, 0.5, 0.5, false, b);
            double delta = 0;
            for (int c = 0; c < a.Length; c++) delta += Math.Abs(a[c] - b[c]);
            Assert.True(delta < 0.02,
                $"weight vector jumped {delta:F4} for a {step} elevation step at e={e:F3} — hard classification edge on land");

            // WITHIN WATER: sweep the whole depth range.
            TerrainSplat.Weights(0.5, e, 0.5, 0.5, true, a);
            TerrainSplat.Weights(0.5, e + step, 0.5, 0.5, true, b);
            delta = 0;
            for (int c = 0; c < a.Length; c++) delta += Math.Abs(a[c] - b[c]);
            Assert.True(delta < 0.02,
                $"weight vector jumped {delta:F4} for a {step} depth step at d={e:F3} — hard classification edge in water");
        }

        // AT THE COASTLINE: the discontinuity is REQUIRED. The same point,
        // vanishing elevation and depth, flipped across the mask: the land
        // side must carry ALL its weight on land washes, the water side ALL
        // of it on water washes — a total-variation jump of 2, the maximum.
        Span<double> land = stackalloc double[ParchmentPalette.TerrainClassCount];
        Span<double> sea = stackalloc double[ParchmentPalette.TerrainClassCount];
        TerrainSplat.Weights(0.001, 0.001, 0.5, 0.5, isWater: false, land);
        TerrainSplat.Weights(0.001, 0.001, 0.5, 0.5, isWater: true, sea);
        double landOnLand = 0, seaOnWater = 0, jump = 0;
        for (int c = 0; c < land.Length; c++)
        {
            bool waterClass = c >= (int)ParchmentPalette.TerrainClass.Shallows;
            if (!waterClass) landOnLand += land[c];
            if (waterClass) seaOnWater += sea[c];
            jump += Math.Abs(land[c] - sea[c]);
        }
        Assert.True(landOnLand > 0.9999,
            $"land side of the coastline puts only {landOnLand:F6} of its weight on land washes — the mask is not a hard gate");
        Assert.True(seaOnWater > 0.9999,
            $"water side of the coastline puts only {seaOnWater:F6} of its weight on water washes — the mask is not a hard gate");
        Assert.True(jump > 1.999,
            $"total-variation jump across the coastline is {jump:F4}, not 2 — the wash sets bleed across the mask");
    }

    [Fact]
    public void SplatWeights_SteerWithMoistureAndFertility_InTheRightDirection()
    {
        // Semantics, not just smoothness: wet+rich lowland paints MORE fertile
        // green than dry+poor lowland, and dry ground paints more arid.
        Span<double> wet = stackalloc double[ParchmentPalette.TerrainClassCount];
        Span<double> dry = stackalloc double[ParchmentPalette.TerrainClassCount];
        TerrainSplat.Weights(0.16, 0.0, moisture: 0.95, fertility: 0.95, isWater: false, wet);
        TerrainSplat.Weights(0.16, 0.0, moisture: 0.05, fertility: 0.05, isWater: false, dry);

        int fertile = (int)ParchmentPalette.TerrainClass.Fertile;
        int arid = (int)ParchmentPalette.TerrainClass.Arid;
        Assert.True(wet[fertile] > dry[fertile] * 1.5,
            $"fertile weight barely moved with moisture/fertility ({wet[fertile]:F3} vs {dry[fertile]:F3})");
        Assert.True(dry[arid] > wet[arid] * 1.5,
            $"arid weight barely moved with moisture ({dry[arid]:F3} vs {wet[arid]:F3})");
    }

    [Fact]
    public void SplatBlend_StaysInsideTheParchmentGamut()
    {
        // An affine combination of in-palette washes cannot leave the gamut —
        // pinned so a future "brighten the highlands" hack fails here.
        Span<double> w = stackalloc double[ParchmentPalette.TerrainClassCount];
        byte lo = 255, hi = 0;
        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
        {
            ParchmentPalette.Rgba p = ParchmentPalette.Of((ParchmentPalette.TerrainClass)c);
            lo = Math.Min(lo, Math.Min(p.R, Math.Min(p.G, p.B)));
            hi = Math.Max(hi, Math.Max(p.R, Math.Max(p.G, p.B)));
        }
        for (int i = 0; i <= 30; i++)
        {
            TerrainSplat.Weights(i / 30.0, i / 30.0, 0.3, 0.7, i % 2 == 0, w);
            ParchmentPalette.Rgba c = TerrainSplat.Blend(w);
            Assert.InRange(c.R, lo, hi);
            Assert.InRange(c.G, lo, hi);
            Assert.InRange(c.B, lo, hi);
        }
    }

    // --- the manifest + library ---------------------------------------------

    [Fact]
    public void Manifest_CoversEveryStyleBibleAsset()
    {
        // §4 items 1–6 (substrate-safe subset), by construction rather than by
        // eye: three parchment variants, a grain layer, one wash per terrain
        // class, the coast hairline, and the UI furniture set.
        Assert.Equal(AssetManifest.ParchmentVariants,
            AssetManifest.All.Count(e => e.Kind == AssetManifest.AssetKind.ParchmentBase));
        Assert.Single(AssetManifest.All, e => e.Kind == AssetManifest.AssetKind.Grain);
        Assert.Equal(ParchmentPalette.TerrainClassCount,
            AssetManifest.All.Count(e => e.Kind == AssetManifest.AssetKind.TerrainWash));
        foreach (AssetManifest.AssetKind kind in new[]
        {
            AssetManifest.AssetKind.CoastHairline, AssetManifest.AssetKind.UiPanel,
            AssetManifest.AssetKind.UiHeaderRule, AssetManifest.AssetKind.UiButtonPlate,
            AssetManifest.AssetKind.UiAnnalsBackground, AssetManifest.AssetKind.UiCompassRose,
            AssetManifest.AssetKind.SettlementMarker,
        })
            Assert.Single(AssetManifest.All, e => e.Kind == kind);

        // Every terrain class is addressable, and keys are unique.
        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
            Assert.NotNull(AssetManifest.Terrain((ParchmentPalette.TerrainClass)c));
        Assert.Equal(AssetManifest.All.Count, AssetManifest.All.Select(e => e.Key).Distinct().Count());
        Assert.Throws<KeyNotFoundException>(() => AssetManifest.Require("terrain/volcano"));
    }

    [Fact]
    public void AssetLibrary_LoadsRealFiles_FromTheShippedAssetsFolder()
    {
        AssetLibrary library = AssetLibrary.Load();
        Assert.Equal(AssetManifest.All.Count, library.Report.Count);
        // Every REQUIRED key loads. Parchment variants beyond the primary are
        // optional (the director shipped one sheet), so they may be absent
        // without counting as placeholders.
        foreach (AssetLibrary.Status r in library.Report)
        {
            bool optionalVariant = r.Key.StartsWith("parchment/base-") && r.Key != "parchment/base-0";
            if (!optionalVariant) Assert.True(r.Loaded, $"{r.Key}: {r.Note}");
        }

        // Loaded images are usable: non-empty, RGBA, addressable.
        ArtImage tile = library.Terrain(ParchmentPalette.TerrainClass.Fertile);
        Assert.True(tile.Width > 0 && tile.Height > 0);
        Assert.Equal(tile.Width * tile.Height * 4, tile.Rgba.Length);
    }

    [Fact]
    public void AssetLibrary_MissingAndCorruptAssets_FallBackLabeled_NeverThrow()
    {
        // THE no-crash promise. An empty root: everything is a placeholder,
        // every entry is reported as such, and every image is still usable.
        string root = Path.Combine(Path.GetTempPath(), $"art-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            AssetLibrary empty = AssetLibrary.Load(root);
            int required = AssetManifest.All.Count(e =>
                !(e.Kind == AssetManifest.AssetKind.ParchmentBase && e.Variant > 0));
            Assert.Equal(required, empty.PlaceholderCount);
            Assert.All(empty.Report, r => Assert.False(r.Loaded));
            Assert.All(empty.Report, r => Assert.Equal("missing file", r.Note));
            Assert.Contains("PLACEHOLDER", empty.SummaryLine());
            foreach (AssetManifest.Entry e in AssetManifest.All)
                Assert.True(empty.Get(e.Key).Rgba.Length > 0, $"{e.Key} produced no placeholder");

            // A CORRUPT file (not a PNG at all) must also degrade, not throw.
            string corrupt = Path.Combine(root, AssetManifest.Terrain(ParchmentPalette.TerrainClass.Peak).RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(corrupt)!);
            File.WriteAllText(corrupt, "this is not a png");
            AssetLibrary broken = AssetLibrary.Load(root);
            string peakKey = AssetManifest.Terrain(ParchmentPalette.TerrainClass.Peak).Key;
            AssetLibrary.Status peak = broken.Report.First(r => r.Key == peakKey);
            Assert.False(peak.Loaded);
            Assert.Contains("not a PNG", peak.Note);
            Assert.True(broken.Terrain(ParchmentPalette.TerrainClass.Peak).Rgba.Length > 0);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AssetLibrary_EverySeed_DrawsOnRealPaper_NeverAPlaceholder()
    {
        // The drop-integration contract, stated so it holds for ANY number of
        // sheets: whatever variants actually resolved, every world seed is
        // handed REAL paper — never a leftover programmatic stand-in. (Written
        // when the drop was a single sheet; the director has since added
        // base1/base2, so the count is read from the library rather than
        // assumed.)
        AssetLibrary art = AssetLibrary.Load();
        Assert.True(art.ParchmentVariantCount >= 1);
        ArtImage placeholder = PlaceholderArt.Generate(AssetManifest.Require("parchment/base-0"));
        for (ulong seed = 0; seed < 24; seed++)
        {
            ArtImage sheet = art.ParchmentFor(seed);
            Assert.False(sheet.Rgba.AsSpan().SequenceEqual(placeholder.Rgba),
                $"seed {seed} was handed the programmatic stand-in instead of real paper");
            Assert.True(sheet.Width > 256 && sheet.Height > 256,
                $"seed {seed} drew a {sheet.Width}x{sheet.Height} sheet — that is a stand-in size");
        }
    }

    [Fact]
    public void AssetLibrary_MultipleVariants_AreChosenBySeed_Deterministically()
    {
        // Variants remain SUPPORTED (the bible allows 2–3): when a drop
        // provides several sheets, the seed selects among them, stably.
        string root = Path.Combine(Path.GetTempPath(), $"art-variants-{Guid.NewGuid():N}");
        try
        {
            PlaceholderArt.GenerateMissing(root);      // base-0..2 all present and distinct
            AssetLibrary library = AssetLibrary.Load(root);
            Assert.Equal(AssetManifest.ParchmentVariants, library.ParchmentVariantCount);
            Assert.Same(library.ParchmentFor(42), library.ParchmentFor(42));
            Assert.Same(library.ParchmentFor(0), library.ParchmentFor((ulong)AssetManifest.ParchmentVariants));
            var distinct = new HashSet<ArtImage>();
            for (ulong s = 0; s < (ulong)AssetManifest.ParchmentVariants; s++) distinct.Add(library.ParchmentFor(s));
            Assert.Equal(AssetManifest.ParchmentVariants, distinct.Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    // --- PNG codec -----------------------------------------------------------

    [Fact]
    public void PngCodec_RoundTripsExactly_IncludingAlpha()
    {
        var img = new ArtImage(37, 23, new byte[37 * 23 * 4]);
        for (int i = 0; i < img.Rgba.Length; i++) img.Rgba[i] = (byte)((i * 37 + i / 3) & 0xFF);
        string path = Path.Combine(Path.GetTempPath(), $"png-{Guid.NewGuid():N}.png");
        try
        {
            PngCodec.Write(path, img);
            ArtImage back = PngCodec.Read(path);
            Assert.Equal(img.Width, back.Width);
            Assert.Equal(img.Height, back.Height);
            Assert.Equal(img.Rgba, back.Rgba);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PngCodec_WrappedSampling_IsContinuousAcrossTheSeam()
    {
        // The sampler is half the seamlessness guarantee: even a perfect tile
        // shows a seam if sampling clamps instead of wrapping.
        ArtImage tile = AssetLibrary.Load().Terrain(ParchmentPalette.TerrainClass.Plain);
        ParchmentPalette.Rgba justBefore = tile.SampleWrapped(0.9999, 0.5);
        ParchmentPalette.Rgba justAfter = tile.SampleWrapped(1.0001, 0.5);
        Assert.True(Math.Abs(justBefore.R - justAfter.R) <= 2, "u wrap is discontinuous");
        Assert.True(Math.Abs(justBefore.G - justAfter.G) <= 2, "u wrap is discontinuous");
        ParchmentPalette.Rgba below = tile.SampleWrapped(0.5, 0.9999);
        ParchmentPalette.Rgba above = tile.SampleWrapped(0.5, 1.0001);
        Assert.True(Math.Abs(below.B - above.B) <= 2, "v wrap is discontinuous");
    }

    private static (int, int, int) Rgb(ParchmentPalette.Rgba c) => (c.R, c.G, c.B);
}
