using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Maps;

/// <summary>
/// Shared material painters for the era kit (linear albedo; heights in metres). Each is a pure function of a position
/// (metres, in whatever plane the caller paints) and a seed, so every item variant is reproducible.
/// </summary>
public static class Materials
{
    public static readonly Vector3 BronzeBright = new(0.790f, 0.585f, 0.320f), BronzeAged = new(0.600f, 0.415f, 0.215f), BronzeTarnish = new(0.200f, 0.135f, 0.072f);
    public static readonly Vector3 IronBright = new(0.56f, 0.55f, 0.53f), IronDark = new(0.30f, 0.29f, 0.28f), Rust = new(0.20f, 0.085f, 0.035f);
    public static readonly Vector3 Verdigris = new(0.120f, 0.230f, 0.180f), Dust = new(0.200f, 0.165f, 0.120f);

    /// <summary>Bronze: mottle 0..1, tarnish, verdigris, polish, fresh scratches, dust (all 0..1).</summary>
    public static Texel Bronze(double mottle, double tarnish, double verd, double polish, double fresh, double dust)
    {
        var col = Vector3.Lerp(BronzeAged, BronzeBright, (float)Saturate(0.35 + 0.65 * mottle + 0.35 * polish));
        col = Vector3.Lerp(col, BronzeTarnish, (float)Saturate(tarnish * (1 - 0.6 * polish)));
        col = Vector3.Lerp(col, new Vector3(0.90f, 0.66f, 0.47f), (float)(fresh * 0.75));
        float rough = (float)(0.30 + 0.09 * (1 - mottle) - 0.14 * polish + 0.30 * tarnish - 0.14 * fresh);
        var t = Texel.Of(col, Math.Clamp(rough, 0.10f, 0.9f), 1f);
        if (verd > 0) { t.Albedo = Vector3.Lerp(t.Albedo, Verdigris, (float)verd); t.Rough = MathHelper.Lerp(t.Rough, 0.85f, (float)verd); t.Metal = MathHelper.Lerp(1f, 0f, (float)verd); }
        if (dust > 0) Dusty(ref t, dust);
        return t;
    }

    /// <summary>Forged iron or steel: hammer mottle, rust (0..1, rough dielectric), polish (0..1), fresh scratches.</summary>
    public static Texel Iron(double x, double y, uint seed, double rust, double polish, double fresh = 0, bool steel = false)
    {
        double m = Fbm(x * 40, y * 40, 4, seed);
        double fine = Fbm(x * 300, y * 300, 3, seed + 1);
        var col = Vector3.Lerp(IronDark, IronBright, (float)Saturate(0.35 + 0.5 * m + 0.35 * polish + (steel ? 0.15 : 0)));
        double rustMask = Saturate(rust * Smoothstep(0.45, 0.75, m * 0.6 + fine * 0.4) * 1.6);
        var t = Texel.Of(col, (float)(steel ? 0.24 + 0.1 * fine - 0.1 * polish : 0.40 + 0.12 * fine - 0.12 * polish), 1f, (float)(0.00004 * (m - 0.5)));
        t.Albedo = Vector3.Lerp(t.Albedo, new Vector3(0.82f, 0.81f, 0.78f), (float)(fresh * 0.6));
        t.Rough = MathHelper.Lerp(t.Rough, 0.18f, (float)fresh);
        if (rustMask > 0)
        {
            var rc = Vector3.Lerp(Rust, new Vector3(0.32f, 0.15f, 0.06f), (float)fine);
            t.Albedo = Vector3.Lerp(t.Albedo, rc, (float)rustMask); t.Rough = MathHelper.Lerp(t.Rough, 0.85f, (float)rustMask);
            t.Metal = MathHelper.Lerp(1f, 0f, (float)rustMask); t.Height += (float)(0.00008 * rustMask * fine);
        }
        return t;
    }

    /// <summary>Wood: grain along +v (y), boards of <paramref name="board"/> metres across x (0 = no seams), colour range.</summary>
    public static Texel Wood(double x, double y, uint seed, Vector3 light, Vector3 dark, double board = 0, double rough = 0.7)
    {
        int bi = board > 0 ? (int)Math.Floor(x / board) : 0;
        uint s = seed + (uint)(bi * 31);
        double gx = x + 0.008 * Perlin(y * 3, x * 0.7, s);
        double grain = Fbm(gx * 180, y * 6, 4, s + 1);
        double fig = Ridged(gx * 45, y * 1.5, 3, s + 2);
        var col = Vector3.Lerp(dark, light, (float)(0.5 * grain + 0.5 * fig));
        col *= (float)(0.9 + 0.2 * Rand(bi, 7, 0, seed));
        double h = 0.00006 * (grain - 0.5) + 0.00008 * fig;
        if (board > 0)
        {
            double fx = x / board - Math.Floor(x / board);
            double seam = Smoothstep(0.012, 0.0, Math.Min(fx, 1 - fx) * board / 0.08);
            h -= 0.0004 * seam; col *= (float)(1 - 0.45 * seam);
        }
        return Texel.Of(col, (float)(rough - 0.08 * fig), 0f, (float)h);
    }

    /// <summary>Leather: pebbled grain, darker creases, a little sheen.</summary>
    public static Texel Leather(double x, double y, uint seed, Vector3 colour, double wear = 0.4)
    {
        var (f1, f2, _, _, _) = Worley(x / 0.0018, y / 0.0018, seed, 0.9);
        double crease = Smoothstep(0.08, 0.0, f2 - f1);
        double broad = Fbm(x * 25, y * 25, 4, seed + 1);
        var col = colour * (float)(0.8 + 0.35 * broad) * (float)(1 - 0.35 * crease);
        double scuff = wear * Smoothstep(0.65, 0.85, Fbm(x * 60, y * 60, 3, seed + 2));
        col = Vector3.Lerp(col, colour * 1.35f, (float)scuff);
        return Texel.Of(col, (float)(0.55 + 0.15 * crease - 0.15 * scuff), 0f, (float)(0.00008 * (1 - f1) - 0.00006 * crease));
    }

    /// <summary>Paint over a ground (the caller's texel): flat colour with brush texture, chipped where <paramref name="chip"/> &gt; 0.5.</summary>
    public static Texel Paint(Texel ground, Vector3 colour, double x, double y, uint seed, double chip)
    {
        double brush = 0.92 + 0.16 * Fbm(x * 160, y * 18, 3, seed);
        if (chip > 0.5) { ground.Albedo *= 0.85f; return ground; }
        var t = Texel.Of(colour * (float)brush, (float)(0.6 + 0.12 * Fbm(x * 90, y * 90, 2, seed + 1)), 0f, ground.Height + 0.00012f);
        return t;
    }

    /// <summary>
    /// Straight scratches: each 4 cm cell holds up to two segments of random direction and length; returns the scratch
    /// intensity at (x, y) (metres, any plane), 0..1. <paramref name="density"/> 0..1 sets how many cells have them.
    /// </summary>
    public static double Scratches(double x, double y, uint seed, double density, double cell = 0.04)
    {
        double best = 0;
        int cx = (int)Math.Floor(x / cell), cy = (int)Math.Floor(y / cell);
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
                for (int k = 0; k < 2; k++)
                {
                    int gx = cx + i, gy = cy + j;
                    if (Rand(gx, gy, k, seed) > density) continue;
                    double px = (gx + Rand(gx, gy, k + 10, seed)) * cell, py = (gy + Rand(gx, gy, k + 20, seed)) * cell;
                    double a = Math.PI * Rand(gx, gy, k + 30, seed), len = cell * (0.4 + 1.2 * Rand(gx, gy, k + 40, seed));
                    double ex = Math.Cos(a), ey = Math.Sin(a);
                    double along = (x - px) * ex + (y - py) * ey, across = -(x - px) * ey + (y - py) * ex;
                    if (Math.Abs(along) > len * 0.5) continue;
                    double wdt = 0.00035 + 0.0004 * Rand(gx, gy, k + 50, seed);
                    double v = Math.Exp(-across * across / (wdt * wdt)) * Math.Sin(Math.PI * (along / len + 0.5));
                    best = Math.Max(best, v);
                }
        return best;
    }

    public static void Dusty(ref Texel t, double dust)
    {
        t.Albedo = Vector3.Lerp(t.Albedo, Dust, (float)dust);
        t.Rough = MathHelper.Lerp(t.Rough, 0.92f, (float)dust);
        t.Metal = MathHelper.Lerp(t.Metal, 0f, (float)dust);
    }

    /// <summary>Linen or wool cloth: a plain weave (threads every <paramref name="pitch"/> m), colour, slub variation.</summary>
    public static Texel Cloth(double x, double y, uint seed, Vector3 colour, double pitch = 0.0008)
    {
        double wx = Math.Sin(x / pitch * Math.PI), wy = Math.Sin(y / pitch * Math.PI);
        double weave = 0.5 + 0.5 * (wx * wy);
        double slub = Fbm(x * 12, y * 400, 3, seed);
        var col = colour * (float)(0.85 + 0.2 * weave) * (float)(0.92 + 0.16 * slub);
        return Texel.Of(col, 0.85f, 0f, (float)(0.00006 * weave));
    }
}
