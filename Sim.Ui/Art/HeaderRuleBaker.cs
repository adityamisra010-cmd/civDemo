namespace Sim.Ui.Art;

/// <summary>
/// D-A1 FIX (director ruling, docs/art-gate-defects.md): the header rule is
/// PROCEDURAL. Two generation rounds failed on this asset — a small ornament
/// floating on a ~99%-empty square field renders as nothing at the pinned
/// texel density (main: 0.46% alpha coverage; the re-drop: 0.90%, ink
/// #A77032 off-palette). A double rule with a repeating lozenge is a
/// straightedge and a stamp: drawn in code it is seamless BY CONSTRUCTION,
/// exactly on-palette, correct in aspect by definition, and needs no alpha
/// keying. The single-cartographer rule is strengthened, not waived —
/// cartographers ruled their lines with instruments, and this is the one
/// element on the page that was never freehand.
///
/// SIZE, DERIVED (not picked). The draw path (PanelFurniture.HeaderRuleUv,
/// UNTOUCHED by this packet) computes scale = nativeH / drawH and shows
/// nativeW / scale screen px per tile. Shipped drawH = 8 px. Choosing
/// vertical supersampling S = 4 for bilinear headroom gives
/// nativeH = 8 × 4 = 32. Target horizontal repeat ~256 screen px (inside the
/// ruled 200–300 band) ⇒ nativeW = 256 × 4 = 1024. One texture = exactly one
/// period. At a future drawH of 12 px the scale becomes 32/12 ≈ 2.67 and the
/// repeat stretches to 1024/2.67 = 384 screen px; at 16 px, 512 px — the
/// period is fixed in TEXTURE space, so it scales proportionally with draw
/// height, keeping ornament proportions correct rather than ornament count
/// constant.
///
/// ANTIALIASING: coverage-based alpha on palette-exact RGB. Every visible
/// pixel's colour is EXACTLY InkPrimary #3A2E1F or InkSoft #6B5A3E; edges are
/// softened only through the alpha channel (line edge rows carry fractional
/// row coverage; the lozenge is 4×4-subsampled against its implicit diamond).
/// Intermediate ALPHA on exact RGB is correct — the compositor blends toward
/// whatever parchment lies beneath; intermediate RGB would bake a paper
/// assumption into the ink. No paper colour is baked anywhere: alpha 0
/// elsewhere, because the grain overlay multiplies separately (style bible
/// §4 item 2) and baked paper would double-grain.
///
/// PURE: doubles and byte output, no MonoGame types — headless-testable in
/// the TerrainBaker family, and deterministic by construction (no RNG, no
/// time, no state).
/// </summary>
public static class HeaderRuleBaker
{
    /// <summary>Native texture size: one horizontal period. See the header
    /// for the derivation (drawH 8 × supersample 4; repeat 256 screen px).</summary>
    public const int NativeWidth = 1024, NativeHeight = 32;

    /// <summary>Supersampling factor: native px per screen px at the shipped
    /// 8 px draw height.</summary>
    public const int Supersample = 4;

    // Geometry, in SCREEN px at the shipped 8 px height (multiplied by
    // Supersample into native rows). Layout inside the 8 px band:
    //   rows 1.0–3.0  : the heavy rule, 2.0 px, InkPrimary
    //   rows 4.5–5.25 : the hairline,   0.75 px, InkSoft
    //   lozenge       : a solid diamond 4.0 px tall × 8.0 px wide, centred on
    //                   the heavy rule's midline, one per period, InkPrimary
    private const double HeavyTop = 1.0, HeavyBottom = 3.0;
    private const double HairTop = 4.5, HairBottom = 5.25;
    private const double LozengeHalfW = 4.0, LozengeHalfH = 2.0;
    private const double LozengeCenterY = 2.0;   // the heavy rule's midline

    /// <summary>
    /// Bake <paramref name="periods"/> horizontal periods (default 1 — the
    /// shipped texture). The multi-period overload exists so the seam test
    /// can assert wraparound equality on raw bytes rather than trust the
    /// modulo arithmetic it is checking.
    /// </summary>
    public static ArtImage Bake(int periods = 1)
    {
        int w = NativeWidth * periods, h = NativeHeight;
        var rgba = new byte[w * h * 4];

        var primary = ParchmentPalette.InkPrimary;
        var soft = ParchmentPalette.InkSoft;

        for (int y = 0; y < h; y++)
        {
            // Row coverage of each band, computed analytically in screen px:
            // the fraction of this native row's [y, y+1) span (in screen
            // units, /Supersample) inside [top, bottom).
            double rowTop = y / (double)Supersample;
            double rowBottom = (y + 1) / (double)Supersample;
            double heavyCov = Overlap(rowTop, rowBottom, HeavyTop, HeavyBottom) * Supersample;
            double hairCov = Overlap(rowTop, rowBottom, HairTop, HairBottom) * Supersample;

            for (int x = 0; x < w; x++)
            {
                // Lozenge coverage by 4×4 subsampling of the diamond's
                // implicit function |dx|/a + |dy|/b <= 1, with dx wrapped to
                // the period so the stamp tiles seamlessly by construction.
                double loz = LozengeCoverage(x % NativeWidth, y);

                // Ink selection: the lozenge and the heavy rule share
                // InkPrimary — overlapping coverage takes the max, never a
                // blend, so RGB stays palette-exact everywhere.
                double primaryCov = System.Math.Max(heavyCov, loz);
                int o = (y * w + x) * 4;
                if (primaryCov > 0.0)
                {
                    rgba[o] = primary.R; rgba[o + 1] = primary.G; rgba[o + 2] = primary.B;
                    rgba[o + 3] = (byte)System.Math.Round(255.0 * System.Math.Clamp(primaryCov, 0.0, 1.0));
                }
                else if (hairCov > 0.0)
                {
                    rgba[o] = soft.R; rgba[o + 1] = soft.G; rgba[o + 2] = soft.B;
                    rgba[o + 3] = (byte)System.Math.Round(255.0 * System.Math.Clamp(hairCov, 0.0, 1.0));
                }
                // else: fully transparent, all four bytes stay 0.
            }
        }
        return new ArtImage(w, h, rgba);
    }

    private static double Overlap(double a0, double a1, double b0, double b1)
    {
        double lo = System.Math.Max(a0, b0), hi = System.Math.Min(a1, b1);
        return System.Math.Max(0.0, hi - lo);
    }

    private static double LozengeCoverage(int x, int y)
    {
        const int Sub = 4;
        double cx = NativeWidth / 2.0;                       // one stamp, centred in the period
        double cy = LozengeCenterY * Supersample;            // native units
        double a = LozengeHalfW * Supersample, b = LozengeHalfH * Supersample;
        int inside = 0;
        for (int sy = 0; sy < Sub; sy++)
        {
            for (int sx = 0; sx < Sub; sx++)
            {
                double px = x + (sx + 0.5) / Sub, py = y + (sy + 0.5) / Sub;
                double dx = px - cx;
                // Wrap to the nearest period image so the stamp is periodic
                // by construction (a stamp near the seam would bleed across).
                if (dx > NativeWidth / 2.0) dx -= NativeWidth;
                if (dx < -NativeWidth / 2.0) dx += NativeWidth;
                double dy = py - cy;
                if (System.Math.Abs(dx) / a + System.Math.Abs(dy) / b <= 1.0) inside++;
            }
        }
        return inside / (double)(Sub * Sub);
    }
}
