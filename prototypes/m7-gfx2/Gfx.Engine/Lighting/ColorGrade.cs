using System.Globalization;
using Gfx.Engine.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Lighting;

/// <summary>
/// 3D colour-grading LUT (PRESENTATION ONLY), stored as a 2D strip: width N·N, height N; texel (b·N + r, g) holds the
/// graded colour of display-encoded input (r, g, b)/(N−1). Applied after tonemapping in display (sRGB) space with
/// manual trilinear filtering (two bilinear taps), which GLSL 1.20 can do without 3D textures.
/// </summary>
public sealed class ColorGradeLut
{
    public int Size { get; }
    /// <summary>RGBA floats, row-major over the strip.</summary>
    public float[] Data { get; }

    ColorGradeLut(int size, float[] data) { Size = size; Data = data; }

    public static ColorGradeLut Identity(int n = 32) => FromFunction(n, (r, g, b) => (r, g, b));

    /// <summary>
    /// The G2 house look: a light touch on top of the tonemapper. +6 % saturation (luma-preserving), shadows nudged
    /// cool and highlights warm by under 2 %, a gentle S-curve around mid-grey, and a 0.4 % black lift so deep shadows
    /// keep a little detail. Defined in display sRGB space.
    /// </summary>
    public static ColorGradeLut House(int n = 32) => FromFunction(n, (r, g, b) =>
    {
        double l = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        double sat = 1.06;
        r = l + (r - l) * sat; g = l + (g - l) * sat; b = l + (b - l) * sat;
        double sh = Math.Clamp(1 - l * 2.2, 0, 1), hi = Math.Clamp((l - 0.55) / 0.45, 0, 1);
        r *= 1 - 0.006 * sh + 0.016 * hi; g *= 1 + 0.002 * sh + 0.004 * hi; b *= 1 + 0.018 * sh - 0.016 * hi;
        static double S(double x) { x = Math.Clamp(x, 0, 1); return x + 0.07 * (x - 0.5) * (1 - Math.Abs(2 * x - 1)); }
        static double Lift(double x) => 0.004 + x * (1 - 0.004);
        return (Lift(S(r)), Lift(S(g)), Lift(S(b)));
    });

    public static ColorGradeLut FromFunction(int n, Func<double, double, double, (double r, double g, double b)> f)
    {
        var d = new float[n * n * n * 4];
        for (int b = 0; b < n; b++)
            for (int g = 0; g < n; g++)
                for (int r = 0; r < n; r++)
                {
                    var (o0, o1, o2) = f(r / (double)(n - 1), g / (double)(n - 1), b / (double)(n - 1));
                    int o = (g * n * n + b * n + r) * 4;
                    d[o] = (float)Math.Clamp(o0, 0, 1); d[o + 1] = (float)Math.Clamp(o1, 0, 1); d[o + 2] = (float)Math.Clamp(o2, 0, 1); d[o + 3] = 1;
                }
        return new ColorGradeLut(n, d);
    }

    /// <summary>Reads an Adobe/Resolve <c>.cube</c> 3D LUT (LUT_3D_SIZE N, red varying fastest, domain 0..1).</summary>
    public static ColorGradeLut LoadCube(string path)
    {
        int n = 0; var vals = new List<(double, double, double)>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("LUT_3D_SIZE", StringComparison.Ordinal)) { n = int.Parse(line[11..].Trim(), CultureInfo.InvariantCulture); continue; }
            if (char.IsLetter(line[0])) continue; // TITLE, DOMAIN_MIN/MAX (0..1 assumed)
            var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            vals.Add((double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture), double.Parse(p[2], CultureInfo.InvariantCulture)));
        }
        if (n < 2 || vals.Count != n * n * n) throw new InvalidDataException($"{path}: expected {n}^3 entries, got {vals.Count}");
        return FromFunction(n, (r, g, b) =>
        {
            int ri = (int)Math.Round(r * (n - 1)), gi = (int)Math.Round(g * (n - 1)), bi = (int)Math.Round(b * (n - 1));
            return vals[(bi * n + gi) * n + ri];
        });
    }

    /// <summary>CPU evaluation with the shader's exact manual trilinear filtering.</summary>
    public (double r, double g, double b) Apply(double r, double g, double b)
    {
        int n = Size;
        double fb = Math.Clamp(b, 0, 1) * (n - 1); int b0 = (int)Math.Floor(fb), b1 = Math.Min(b0 + 1, n - 1); double tb = fb - b0;
        (double, double, double) Slice(int bs)
        {
            double fr = Math.Clamp(r, 0, 1) * (n - 1), fg = Math.Clamp(g, 0, 1) * (n - 1);
            int r0 = (int)Math.Floor(fr), g0 = (int)Math.Floor(fg), r1 = Math.Min(r0 + 1, n - 1), g1 = Math.Min(g0 + 1, n - 1);
            double tr = fr - r0, tg = fg - g0;
            double C(int rr, int gg, int c) => Data[(gg * n * n + bs * n + rr) * 4 + c];
            double V(int c) => (C(r0, g0, c) * (1 - tr) + C(r1, g0, c) * tr) * (1 - tg) + (C(r0, g1, c) * (1 - tr) + C(r1, g1, c) * tr) * tg;
            return (V(0), V(1), V(2));
        }
        var (a0, a1, a2) = Slice(b0); var (c0, c1, c2) = Slice(b1);
        return (a0 + (c0 - a0) * tb, a1 + (c1 - a1) * tb, a2 + (c2 - a2) * tb);
    }

    public Texture2D CreateTexture(GraphicsDevice gd) => FloatTextures.Half4(gd, Size * Size, Size, Data);
}
