namespace Sim.Ui.ViewModel;

/// <summary>
/// Terrain color mapping (pure view-model, byte RGBA — no MonoGame types).
/// Hypsometric land tint over CONTINUOUS normalized elevation with smooth
/// gradient stops (D-023: explicitly no visible tile/grid structure — every
/// texel gets its own smoothly interpolated color, and the renderer samples
/// the baked texture bilinearly). Water is a distinct depth-shaded blue;
/// rivers overdraw in a lighter blue.
/// </summary>
public static class TerrainPalette
{
    public readonly record struct Rgba(byte R, byte G, byte B, byte A);

    public static readonly Rgba RiverColor = new(0x4E, 0x9A, 0xD6, 0xFF);

    // Hypsometric stops over t = normalized elevation above sea in [0,1]:
    // coastal green → lowland green → dry yellow-green → brown → gray → snow.
    private static readonly (double T, Rgba Color)[] LandStops =
    [
        (0.00, new Rgba(0x35, 0x6E, 0x3A, 0xFF)),
        (0.15, new Rgba(0x4C, 0x8A, 0x45, 0xFF)),
        (0.35, new Rgba(0x8F, 0xA3, 0x52, 0xFF)),
        (0.55, new Rgba(0xA8, 0x8A, 0x54, 0xFF)),
        (0.75, new Rgba(0x8C, 0x78, 0x66, 0xFF)),
        (0.90, new Rgba(0xB0, 0xAC, 0xA6, 0xFF)),
        (1.00, new Rgba(0xF2, 0xF2, 0xEF, 0xFF)),
    ];

    private static readonly Rgba ShallowWater = new(0x2E, 0x5E, 0x8F, 0xFF);
    private static readonly Rgba DeepWater = new(0x14, 0x2C, 0x4A, 0xFF);

    /// <summary>
    /// Land color for normalized above-sea elevation t in [0,1] (clamped):
    /// piecewise-linear interpolation between the hypsometric stops — smooth by
    /// construction, no banding wider than one gradient.
    /// </summary>
    public static Rgba Land(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        for (int i = 1; i < LandStops.Length; i++)
        {
            if (t <= LandStops[i].T)
            {
                double span = LandStops[i].T - LandStops[i - 1].T;
                double f = span <= 0.0 ? 0.0 : (t - LandStops[i - 1].T) / span;
                return Lerp(LandStops[i - 1].Color, LandStops[i].Color, f);
            }
        }
        return LandStops[^1].Color;
    }

    /// <summary>Water color for normalized depth d in [0,1] (clamped): shallow → deep.</summary>
    public static Rgba Water(double d) => Lerp(ShallowWater, DeepWater, Math.Clamp(d, 0.0, 1.0));

    private static Rgba Lerp(Rgba a, Rgba b, double f) => new(
        (byte)Math.Round(a.R + (b.R - a.R) * f),
        (byte)Math.Round(a.G + (b.G - a.G) * f),
        (byte)Math.Round(a.B + (b.B - a.B) * f),
        (byte)Math.Round(a.A + (b.A - a.A) * f));
}
