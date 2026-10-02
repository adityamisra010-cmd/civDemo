using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

/// <summary>
/// Colour arithmetic for the era tokens: hex, mixing, HSL, hue and contrast. Pure double arithmetic
/// on byte RGBA with explicit rounding — the same inputs give the same bytes on every run, which the
/// theme determinism tests pin (Sim.Ui sits outside the simulation's determinism surface, ADR-009,
/// but the presentation is required to be deterministic all the same).
/// </summary>
public static class ThemeColor
{
    public static Rgba Hex(uint rgb) => Rgba.Hex(rgb);

    /// <summary><paramref name="c"/> with alpha <paramref name="alpha"/> in [0,1].</summary>
    public static Rgba Alpha(Rgba c, double alpha) =>
        new(c.R, c.G, c.B, (byte)Math.Round(255.0 * Math.Clamp(alpha, 0.0, 1.0)));

    /// <summary>Linear mix in sRGB bytes: t = 0 → <paramref name="a"/>, t = 1 → <paramref name="b"/>.</summary>
    public static Rgba Mix(Rgba a, Rgba b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        static byte L(byte x, byte y, double t) => (byte)Math.Round(x + (y - x) * t);
        return new Rgba(L(a.R, b.R, t), L(a.G, b.G, t), L(a.B, b.B, t), L(a.A, b.A, t));
    }

    /// <summary>HSV/HSL hue in degrees [0, 360); 0 for a grey.</summary>
    public static double Hue(Rgba c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        if (d <= 1e-12) return 0.0;
        double h = max == r ? (g - b) / d % 6.0 : max == g ? (b - r) / d + 2.0 : (r - g) / d + 4.0;
        h *= 60.0;
        return h < 0 ? h + 360.0 : h;
    }

    /// <summary>HSL saturation in [0, 1].</summary>
    public static double Saturation(Rgba c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0, d = max - min;
        if (d <= 1e-12) return 0.0;
        return d / (1.0 - Math.Abs(2.0 * l - 1.0));
    }

    /// <summary>HSL lightness in [0, 1].</summary>
    public static double Lightness(Rgba c)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0, min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0;
        return (max + min) / 2.0;
    }

    /// <summary>A colour from HSL (hue degrees, saturation and lightness in [0,1]).</summary>
    public static Rgba FromHsl(double h, double s, double l, byte alpha = 255)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);
        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        double m = l - c / 2.0;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        static byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 1.0) * 255.0);
        return new Rgba(B(r + m), B(g + m), B(b + m), alpha);
    }

    /// <summary>WCAG relative luminance.</summary>
    public static double Luminance(Rgba c)
    {
        static double Lin(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    /// <summary>WCAG contrast ratio of two opaque colours (1..21).</summary>
    public static double Contrast(Rgba a, Rgba b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>The shortest angular distance between two hues, in degrees [0, 180].</summary>
    public static double HueDistance(double a, double b)
    {
        double d = Math.Abs(a - b) % 360.0;
        return d > 180.0 ? 360.0 - d : d;
    }

    /// <summary>"#RRGGBB" or "#RRGGBBAA" (alpha only when not opaque), upper-case — the canonical form.</summary>
    public static string Code(Rgba c) => c.A == 255
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}")
        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}");
}
