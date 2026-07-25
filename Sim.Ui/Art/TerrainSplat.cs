using Sim.Ui.Art;

namespace Sim.Ui.Art;

/// <summary>
/// THE SPLAT-WEIGHT MATH (style bible §4 item 3): which terrain wash tiles
/// blend, and how much, at one point of the world — a pure function of the
/// worldgen fields (normalized elevation, moisture, fertility, water mask,
/// normalized depth). No MonoGame, no state: the whole thing is testable
/// headless, which is the point (this is the load-bearing visual math).
///
/// DESIGN RULE — CONTINUITY: every weight is a smooth (C0, and C1 except at
/// the deliberate water/land boundary) function of its inputs, so the map has
/// NO class boundaries, no stair-stepping and no banding: adjacent texels with
/// nearly-equal fields get nearly-equal weight vectors. Classification by
/// hard thresholds ("if elevation > 0.55 then upland") is exactly what the
/// bible forbids, and is what the pre-art flat fills effectively did.
///
/// Weights always sum to 1 (normalized at the end), so the blend is an
/// affine combination of in-palette washes and can never leave the gamut.
/// </summary>
public static class TerrainSplat
{
    /// <summary>Elevation band centres for the six land classes, in normalized
    /// above-sea elevation. Weights are Gaussian bumps around these — wide
    /// enough to overlap generously, which is what produces the painted
    /// gradient rather than contour bands.</summary>
    private static readonly double[] LandCentres = [0.02, 0.14, 0.30, 0.46, 0.70, 0.93];
    private const double LandBandWidth = 0.20;

    /// <summary>Depth centres for the three water classes.</summary>
    private static readonly double[] WaterCentres = [0.05, 0.35, 0.85];
    private const double WaterBandWidth = 0.30;

    /// <summary>Moisture/fertility steer the LOWLAND↔FERTILE↔ARID trio inside
    /// the low-elevation bands: wet+fertile ground paints the fertile green,
    /// dry ground paints arid. Bounded multipliers (never a free-floating
    /// buff): each factor stays within [1-Steer, 1+Steer].</summary>
    private const double Steer = 0.85;

    /// <summary>
    /// Blend weights over the nine classes at one sample.
    /// </summary>
    /// <param name="elevationAboveSea">Normalized [0,1] elevation above sea (land only).</param>
    /// <param name="depthBelowSea">Normalized [0,1] depth below sea (water only).</param>
    /// <param name="moisture">Worldgen moisture [0,1].</param>
    /// <param name="fertility">Worldgen fertility [0,1].</param>
    /// <param name="isWater">Water mask.</param>
    /// <param name="weights">Destination span of length <see cref="ParchmentPalette.TerrainClassCount"/>.</param>
    public static void Weights(
        double elevationAboveSea, double depthBelowSea,
        double moisture, double fertility, bool isWater, Span<double> weights)
    {
        if (weights.Length != ParchmentPalette.TerrainClassCount)
            throw new ArgumentException(
                $"weights must be {ParchmentPalette.TerrainClassCount} long", nameof(weights));
        weights.Clear();

        if (isWater)
        {
            double d = Clamp01(depthBelowSea);
            for (int i = 0; i < WaterCentres.Length; i++)
                weights[(int)ParchmentPalette.TerrainClass.Shallows + i] =
                    Bump(d, WaterCentres[i], WaterBandWidth);
        }
        else
        {
            double e = Clamp01(elevationAboveSea);
            for (int i = 0; i < LandCentres.Length; i++)
                weights[i] = Bump(e, LandCentres[i], LandBandWidth);

            // Moisture/fertility steering, bounded and smooth. Fertile green
            // rises with BOTH (a green field needs water and soil); arid rises
            // as moisture falls; lowland green is the neutral middle and is
            // left unsteered so the trio cannot all collapse at once.
            double m = Clamp01(moisture), f = Clamp01(fertility);
            double wet = 2.0 * m - 1.0;          // [-1, 1]
            double rich = 2.0 * f - 1.0;         // [-1, 1]
            weights[(int)ParchmentPalette.TerrainClass.Fertile] *=
                1.0 + Steer * Clamp(0.5 * (wet + rich), -1.0, 1.0);
            weights[(int)ParchmentPalette.TerrainClass.Arid] *= 1.0 - Steer * wet;
            weights[(int)ParchmentPalette.TerrainClass.Plain] *= 1.0 - 0.5 * Steer * rich;
        }

        Normalize(weights);
    }

    /// <summary>Composites the class weights against per-class colors — the
    /// fallback path when a tile asset is missing, and the reference the tests
    /// pin the tiled path against.</summary>
    public static ParchmentPalette.Rgba Blend(ReadOnlySpan<double> weights)
    {
        double r = 0, g = 0, b = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            ParchmentPalette.Rgba c = ParchmentPalette.Of((ParchmentPalette.TerrainClass)i);
            r += c.R * weights[i];
            g += c.G * weights[i];
            b += c.B * weights[i];
        }
        return new ParchmentPalette.Rgba(Byte(r), Byte(g), Byte(b));
    }

    /// <summary>Gaussian-ish bump, strictly positive so the weight vector is
    /// never degenerate (every class contributes a whisper — no hard edges).</summary>
    private static double Bump(double x, double centre, double width)
    {
        double t = (x - centre) / width;
        return Math.Exp(-t * t) + 1e-6;
    }

    private static void Normalize(Span<double> w)
    {
        double sum = 0.0;
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] < 0.0) w[i] = 0.0;   // steering can only damp, never invert
            sum += w[i];
        }
        if (sum <= 0.0) { w[(int)ParchmentPalette.TerrainClass.Plain] = 1.0; return; }
        for (int i = 0; i < w.Length; i++) w[i] /= sum;
    }

    internal static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    internal static byte Byte(double v) => (byte)Math.Clamp(Math.Round(v), 0.0, 255.0);
}
