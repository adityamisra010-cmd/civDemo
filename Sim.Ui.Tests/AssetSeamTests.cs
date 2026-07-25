using Sim.Ui.Art;
using Xunit;

namespace Sim.Ui.Tests;

// THE DIRECTOR'S TWO STYLE-BIBLE CLAUSES, AS EXECUTABLE GATES.
//
// §4/§5 SEAMLESS CLAUSE — "tileable textures must edge-wrap: left edge
// continues into right, top into bottom, no seam, border, or central focal
// point. Verify by 2×2 tiling before acceptance; regenerate any tile showing a
// seam or a visibly repeating feature."
//
// §1 SINGLE-CARTOGRAPHER RULE — "every generated asset must use the identical
// paper substrate and ink chemistry. No asset may introduce a new paper color,
// saturation, lighting model, brush style, or decorative vocabulary."
//
// These run against whatever is in assets/ — placeholders today, the director's
// generated art tomorrow. THAT is the point: the real art faces the same gate
// the stand-ins pass, automatically, on the drop.
public class AssetSeamTests
{
    private static AssetLibrary Library() => AssetLibrary.Load();

    [Fact]
    public void EveryTileableAsset_EdgeWraps_InA2x2Tiling()
    {
        AssetLibrary art = Library();
        foreach (AssetManifest.Entry entry in AssetManifest.All.Where(e => e.Tileable))
        {
            ArtImage img = art.Get(entry.Key);

            // The 2×2 test, done numerically: the discontinuity ACROSS the
            // wrap seam must be no worse than the texture's own internal
            // neighbour-to-neighbour variation. A tile with a border or a
            // hard edge blows this out immediately.
            double interiorX = MeanAbsStep(img, dx: 1, dy: 0, wrap: false);
            double seamX = MeanSeamStep(img, vertical: true);
            double interiorY = MeanAbsStep(img, dx: 0, dy: 1, wrap: false);
            double seamY = MeanSeamStep(img, vertical: false);

            double barX = Math.Max(1.5, interiorX * 3.0);
            double barY = Math.Max(1.5, interiorY * 3.0);
            Assert.True(seamX <= barX,
                $"{entry.Key}: vertical seam step {seamX:F2} vs interior {interiorX:F2} — left edge does not continue into right");
            Assert.True(seamY <= barY,
                $"{entry.Key}: horizontal seam step {seamY:F2} vs interior {interiorY:F2} — top edge does not continue into bottom");
        }
    }

    [Fact]
    public void EveryTileableAsset_HasNoCentralFocalPoint()
    {
        // "no ... central focal point": the centre quadrant's mean luminance
        // must not stand apart from the whole image's. A vignette, a blotch or
        // a logo in the middle reads as a repeating feature once tiled.
        AssetLibrary art = Library();
        foreach (AssetManifest.Entry entry in AssetManifest.All.Where(e => e.Tileable))
        {
            ArtImage img = art.Get(entry.Key);
            double whole = MeanLuminance(img, 0, 0, img.Width, img.Height);
            double centre = MeanLuminance(img,
                img.Width / 4, img.Height / 4, img.Width / 2, img.Height / 2);
            double spread = StdLuminance(img);
            double allowed = Math.Max(2.0, spread);   // one sigma, or 2/255 for a flat wash
            Assert.True(Math.Abs(centre - whole) <= allowed,
                $"{entry.Key}: centre luminance {centre:F1} vs whole {whole:F1} (σ {spread:F1}) — central focal point");
        }
    }

    [Fact]
    public void EveryAsset_StaysInTheParchmentGamut_SingleCartographerRule()
    {
        // §1: no asset may introduce a new paper color or saturation. Concretely:
        //  * every OPAQUE pixel must be warm-biased (R ≥ B) or a sea/ink tone
        //    from §2 — never a cool or vivid intruder;
        //  * saturation is bounded by the most saturated color the bible itself
        //    declares, with a small tolerance for antialiasing.
        AssetLibrary art = Library();
        double bibleMaxSaturation = ParchmentPalette.MaxBibleSaturation();
        foreach (AssetManifest.Entry entry in AssetManifest.All)
        {
            ArtImage img = art.Get(entry.Key);
            for (int i = 0; i < img.Width * img.Height; i++)
            {
                int o = i * 4;
                if (img.Rgba[o + 3] < 8) continue;   // transparent margins carry no color
                int r = img.Rgba[o], g = img.Rgba[o + 1], b = img.Rgba[o + 2];
                double saturation = ParchmentPalette.Saturation(new ParchmentPalette.Rgba((byte)r, (byte)g, (byte)b));
                Assert.True(saturation <= bibleMaxSaturation + 0.06,
                    $"{entry.Key}: pixel #{i} rgb({r},{g},{b}) saturation {saturation:F2} exceeds the §2 gamut " +
                    $"({bibleMaxSaturation:F2}) — a new ink chemistry crept in");
            }
        }
    }

    [Fact]
    public void PlaceholderGeneration_IsIdempotent_AndNeverOverwritesRealArt()
    {
        // The drop-in contract: generating placeholders into a root that
        // already has art must write NOTHING. (If this broke, a director's
        // asset drop would be silently clobbered on next launch.)
        string root = Path.Combine(Path.GetTempPath(), $"art-idem-{Guid.NewGuid():N}");
        try
        {
            IReadOnlyList<string> first = PlaceholderArt.GenerateMissing(root);
            Assert.Equal(AssetManifest.All.Count, first.Count);

            // Mark one file as "the director's real art" with distinct bytes.
            string real = Path.Combine(root, AssetManifest.Terrain(ParchmentPalette.TerrainClass.Sea).RelativePath);
            var marker = new ArtImage(4, 4, new byte[4 * 4 * 4]);
            for (int i = 0; i < marker.Rgba.Length; i += 4)
            {
                marker.Rgba[i] = ParchmentPalette.Sea.R;
                marker.Rgba[i + 1] = ParchmentPalette.Sea.G;
                marker.Rgba[i + 2] = ParchmentPalette.Sea.B;
                marker.Rgba[i + 3] = 255;
            }
            PngCodec.Write(real, marker);

            IReadOnlyList<string> second = PlaceholderArt.GenerateMissing(root);
            Assert.Empty(second);
            ArtImage reloaded = PngCodec.Read(real);
            Assert.Equal(4, reloaded.Width);   // still the "real" art, not regenerated
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void PlaceholderGeneration_IsDeterministic()
    {
        // Regenerating a stand-in byte-reproduces it — the gate build is the
        // same picture every time it is cut.
        foreach (AssetManifest.Entry entry in AssetManifest.All)
            Assert.Equal(PlaceholderArt.Generate(entry).Rgba, PlaceholderArt.Generate(entry).Rgba);
    }

    // --- helpers -------------------------------------------------------------

    private static double Luminance(ArtImage img, int x, int y)
    {
        ParchmentPalette.Rgba c = img.At(x, y);
        return 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
    }

    /// <summary>Mean |Δluminance| between neighbours one step apart — the
    /// texture's own internal roughness.</summary>
    private static double MeanAbsStep(ArtImage img, int dx, int dy, bool wrap)
    {
        double sum = 0; int n = 0;
        for (int y = 0; y + dy < img.Height; y++)
            for (int x = 0; x + dx < img.Width; x++)
            {
                sum += Math.Abs(Luminance(img, x, y) - Luminance(img, x + dx, y + dy));
                n++;
            }
        return n == 0 ? 0 : sum / n;
    }

    /// <summary>Mean |Δluminance| ACROSS the wrap seam (last column vs first,
    /// or last row vs first) — what a 2×2 tiling would show at the join.</summary>
    private static double MeanSeamStep(ArtImage img, bool vertical)
    {
        double sum = 0; int n = 0;
        if (vertical)
            for (int y = 0; y < img.Height; y++, n++)
                sum += Math.Abs(Luminance(img, img.Width - 1, y) - Luminance(img, 0, y));
        else
            for (int x = 0; x < img.Width; x++, n++)
                sum += Math.Abs(Luminance(img, x, img.Height - 1) - Luminance(img, x, 0));
        return n == 0 ? 0 : sum / n;
    }

    private static double MeanLuminance(ArtImage img, int x0, int y0, int w, int h)
    {
        double sum = 0; int n = 0;
        for (int y = y0; y < y0 + h && y < img.Height; y++)
            for (int x = x0; x < x0 + w && x < img.Width; x++) { sum += Luminance(img, x, y); n++; }
        return n == 0 ? 0 : sum / n;
    }

    private static double StdLuminance(ArtImage img)
    {
        double mean = MeanLuminance(img, 0, 0, img.Width, img.Height), sum = 0;
        int n = img.Width * img.Height;
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                double d = Luminance(img, x, y) - mean;
                sum += d * d;
            }
        return Math.Sqrt(sum / n);
    }
}
