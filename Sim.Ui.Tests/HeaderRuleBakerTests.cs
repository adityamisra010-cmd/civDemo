using Sim.Ui.Art;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-A1 fix (docs/art-gate-defects.md): the header rule is procedural. These
/// are the four pinned properties plus THE VISIBILITY TEST — the test whose
/// absence let an invisible rule pass its own acceptance twice (texel density
/// is true by construction; "identical ink weight at both widths" is
/// satisfied by zero ink at both). All red proofs are against MUTANTS OF THE
/// GENERATOR, never against the deleted PNG, so they stay reproducible
/// forever (§7.4).
/// </summary>
public class HeaderRuleBakerTests
{
    private static readonly ParchmentPalette.Rgba Primary = ParchmentPalette.InkPrimary;
    private static readonly ParchmentPalette.Rgba Soft = ParchmentPalette.InkSoft;

    [Fact]
    public void Visibility_TheRuleActuallyDrawsInk_AtTheShippedGeometry()
    {
        // THE MISSING TEST. Both failed generated assets carried < 1% alpha
        // coverage on a mostly-empty field and rendered as nothing at texel
        // density 128. The procedural rule's ink lives in a BAND (the rows the
        // heavy rule and hairline span); within that band the coverage must be
        // substantial, and every column must carry at least one fully-opaque
        // pixel so a degenerate all-faint texture cannot pass.
        ArtImage img = HeaderRuleBaker.Bake();

        // The band: screen rows [1.0, 5.25) at supersample 4 → native rows
        // 4..20 inclusive-exclusive (computed from the same constants the
        // generator uses, not re-typed).
        int bandTop = (int)(1.0 * HeaderRuleBaker.Supersample);
        int bandBottom = (int)System.Math.Ceiling(5.25 * HeaderRuleBaker.Supersample);

        long bandPixels = 0, visible = 0;
        for (int y = bandTop; y < bandBottom; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                bandPixels++;
                if (img.Rgba[(y * img.Width + x) * 4 + 3] > 127) visible++;
            }
        }
        double fraction = visible / (double)bandPixels;
        Assert.True(fraction >= 0.15,
            $"alpha>127 on {fraction:P1} of the rule's own band — below the 15% visibility floor; "
            + "an invisible rule has shipped twice and may not ship a third time");
        // §7.5 note: 0.15 is a floor the measured value (~0.52) sits well
        // above, not a limit the value rests against.

        // Every column carries at least one fully-opaque pixel (the heavy
        // rule's interior rows) — a degenerate all-faint texture fails here
        // even if it clears the 15% floor.
        for (int x = 0; x < img.Width; x++)
        {
            bool opaque = false;
            for (int y = 0; y < img.Height && !opaque; y++)
                if (img.Rgba[(y * img.Width + x) * 4 + 3] == 255) opaque = true;
            Assert.True(opaque, $"column {x} has no fully-opaque pixel — the rule is faint or broken there");
        }
    }

    [Fact]
    public void Seamless_ByConstruction_WraparoundBytesEqual()
    {
        // The style bible's SEAMLESS CLAUSE, structurally satisfied and
        // asserted on raw bytes: a two-period bake must repeat exactly —
        // column x equals column x + period for every x. This replaces the
        // statistical seam test the manifest entry used to buy, and is
        // stronger: byte equality, not edge-window statistics.
        ArtImage two = HeaderRuleBaker.Bake(periods: 2);
        int w = HeaderRuleBaker.NativeWidth;
        for (int y = 0; y < two.Height; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int a = (y * two.Width + x) * 4, b = (y * two.Width + x + w) * 4;
                for (int c = 0; c < 4; c++)
                    Assert.True(two.Rgba[a + c] == two.Rgba[b + c],
                        $"seam break at ({x},{y}) channel {c}: {two.Rgba[a + c]} != {two.Rgba[b + c]}");
            }
        }
    }

    [Fact]
    public void PaletteExact_EveryVisiblePixel_IsOneOfTheTwoInks_Exactly()
    {
        // Single-cartographer rule, made exact: every pixel with ANY alpha has
        // RGB equal to InkPrimary #3A2E1F or InkSoft #6B5A3E — no blends, no
        // intermediate ink values. Intermediate ALPHA on exact RGB is correct
        // and permitted: antialiasing softens edges through coverage, and the
        // compositor blends toward whatever parchment lies beneath;
        // intermediate RGB would bake a paper assumption into the ink.
        ArtImage img = HeaderRuleBaker.Bake();
        long visible = 0, primaryCount = 0, softCount = 0;
        for (int i = 0; i < img.Width * img.Height; i++)
        {
            int o = i * 4;
            if (img.Rgba[o + 3] == 0) continue;
            visible++;
            bool isPrimary = img.Rgba[o] == Primary.R && img.Rgba[o + 1] == Primary.G && img.Rgba[o + 2] == Primary.B;
            bool isSoft = img.Rgba[o] == Soft.R && img.Rgba[o + 1] == Soft.G && img.Rgba[o + 2] == Soft.B;
            Assert.True(isPrimary || isSoft,
                $"pixel {i}: rgb({img.Rgba[o]},{img.Rgba[o + 1]},{img.Rgba[o + 2]}) is neither ink — a blend crept in");
            if (isPrimary) primaryCount++; else softCount++;
        }
        // Non-vacuity: both inks are actually used.
        Assert.True(primaryCount > 0 && softCount > 0,
            $"one ink is missing entirely (primary {primaryCount}, soft {softCount}) — the double rule is single");
        Assert.True(visible > 0, "no visible pixel at all — vacuous");
    }

    [Fact]
    public void Deterministic_TwoBakes_ByteIdentical()
    {
        // TerrainBaker-family determinism: same inputs, byte-identical output.
        // Sim.Ui sits outside the determinism surface (ADR-009), so no world
        // hash is at risk — but a generator that varies run to run is a defect
        // in its own right.
        ArtImage a = HeaderRuleBaker.Bake();
        ArtImage b = HeaderRuleBaker.Bake();
        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Rgba, b.Rgba);
    }

    [Fact]
    public void DerivedGeometry_PeriodAndSupersample_MatchTheStatedArithmetic()
    {
        // The size derivation, pinned so it cannot drift from its comment:
        // nativeH = drawH(8) × supersample(4); repeat at the shipped height =
        // nativeW / supersample = 256 screen px, inside the ruled 200–300 band.
        Assert.Equal(8 * HeaderRuleBaker.Supersample, HeaderRuleBaker.NativeHeight);
        double repeatAt8 = HeaderRuleBaker.NativeWidth / (double)HeaderRuleBaker.Supersample;
        Assert.InRange(repeatAt8, 200.0, 300.0);
        ArtImage img = HeaderRuleBaker.Bake();
        Assert.Equal(HeaderRuleBaker.NativeWidth, img.Width);
        Assert.Equal(HeaderRuleBaker.NativeHeight, img.Height);
    }
}
