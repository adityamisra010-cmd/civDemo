using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>
/// Procedural material detail textures (PRESENTATION, placeholder-grade). Two tileable 512² RGBA8 textures with
/// CPU-generated mip chains; each channel is one material's detail/luminance pattern (mean ≈ 0.5):
/// DetailA = grass, dirt, rock, sand; DetailB = snow, forest floor, mud, dry grass. Colour comes from the biome
/// palette (shader uniforms), so one texture pair serves every biome. Packing 8 materials into 2 samplers keeps the
/// splat shader within GL 2.1 sampler limits on the Iris Xe fallback.
/// </summary>
public static class MaterialTextures
{
    public const int Size = 512;

    /// <summary>Raw level-0 texels (RGBA, row-major) for both textures; deterministic for a seed.</summary>
    public static (Color[] a, Color[] b) Generate(uint seed)
    {
        var a = new Color[Size * Size]; var b = new Color[Size * Size];
        Parallel.For(0, Size, y =>
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float F(int period, int oct, uint s) => PNoise.Fbm(u * period, v * period, seed + s, oct, period);
                // grass: soft clumps + fine blade speckle
                float grass = 0.25f + 0.55f * F(16, 4, 1) + 0.35f * (PNoise.Value(u * 256, v * 256, seed + 2, 256) - 0.5f);
                // dirt: blotches + pebbles
                float peb = SplatModel.SmoothStep(0.70f, 0.78f, PNoise.Value(u * 96, v * 96, seed + 4, 96));
                float dirt = 0.2f + 0.6f * F(8, 5, 3) + 0.3f * peb;
                // rock: layered fBm with dark ridged cracks
                float rf = F(6, 5, 5);
                float crack = SplatModel.SmoothStep(0.9f, 0.98f, 1 - MathF.Abs(2 * F(10, 3, 6) - 1));
                float rock = 0.15f + 0.75f * rf - 0.35f * crack;
                // sand: warped ripples (integer cycles per tile keep it tileable)
                float rip = 0.5f + 0.5f * MathF.Sin(MathHelper.TwoPi * (24 * u + 1.5f * F(4, 3, 7)));
                float sand = 0.35f + 0.18f * rip + 0.3f * (F(12, 3, 8) - 0.5f) + 0.25f;
                float snow = 0.62f + 0.2f * (F(8, 4, 9) - 0.5f) + 0.12f * SplatModel.SmoothStep(0.85f, 0.95f, PNoise.Value(u * 200, v * 200, seed + 10, 200));
                float leaves = SplatModel.SmoothStep(0.55f, 0.62f, PNoise.Value(u * 64, v * 64, seed + 12, 64));
                float floor = 0.2f + 0.45f * F(12, 3, 11) + 0.35f * leaves;
                float puddle = SplatModel.SmoothStep(0.6f, 0.66f, F(4, 3, 14));
                float mud = 0.3f + 0.4f * F(8, 4, 13) - 0.2f * puddle;
                float streak = PNoise.Fbm(u * 128, v * 8, seed + 16, 2, 0);
                float dryg = 0.15f + 0.5f * F(16, 3, 15) + 0.4f * (PNoise.Value(u * 256, v * 32, seed + 17, 256) - 0.5f) + 0.15f * streak;
                a[y * Size + x] = new Color(grass, dirt, rock, sand);
                b[y * Size + x] = new Color(snow, floor, mud, dryg);
            }
        });
        return (a, b);
    }

    public static (Texture2D a, Texture2D b) Create(GraphicsDevice gd, uint seed)
    {
        var (a, b) = Generate(seed);
        return (Upload(gd, a), Upload(gd, b));
    }

    static Texture2D Upload(GraphicsDevice gd, Color[] level0)
    {
        var tex = new Texture2D(gd, Size, Size, true, SurfaceFormat.Color);
        var cur = level0; int size = Size;
        for (int level = 0; level < tex.LevelCount; level++)
        {
            tex.SetData(level, null, cur, 0, cur.Length);
            if (size == 1) break;
            int ns = size / 2; var next = new Color[ns * ns];
            for (int y = 0; y < ns; y++)
                for (int x = 0; x < ns; x++)
                {
                    Color p = cur[2 * y * size + 2 * x], q = cur[2 * y * size + 2 * x + 1], r = cur[(2 * y + 1) * size + 2 * x], s = cur[(2 * y + 1) * size + 2 * x + 1];
                    next[y * ns + x] = new Color((p.R + q.R + r.R + s.R + 2) / 4, (p.G + q.G + r.G + s.G + 2) / 4, (p.B + q.B + r.B + s.B + 2) / 4, (p.A + q.A + r.A + s.A + 2) / 4);
                }
            cur = next; size = ns;
        }
        return tex;
    }
}
