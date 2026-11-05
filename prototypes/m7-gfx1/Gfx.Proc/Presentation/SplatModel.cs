using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Presentation;

/// <summary>Terrain materials, in splat channel order (A.xyzw then B.xyzw).</summary>
public enum Material { Grass = 0, Dirt = 1, Rock = 2, Sand = 3, Snow = 4, ForestFloor = 5, Mud = 6, DryGrass = 7 }

/// <summary>Per-sample splat inputs. Class fractions are a 3×3-blurred one-hot of the authoritative classes.</summary>
public struct SplatInputs
{
    public float[] ClassFraction;   // indexed by TerrainClass byte (length >= 10)
    public float Slope;             // 1 - normal.y
    public float HeightRel;         // 0..1 within the map's height range
    public float Noise;             // 0..1 presentation noise
    public Biome Biome;
}

/// <summary>
/// Splat weights (PRESENTATION). Continuous functions of class fractions, slope, height and noise — no hard
/// thresholds (the idea of Sim.Ui/Art/TerrainSplat.cs: smooth bumps, normalised to sum 1, so blends never leave the
/// palette). The pixel shader further sharpens these by material detail ("height blending").
/// </summary>
public static class SplatModel
{
    public const int Count = 8;

    public static float SmoothStep(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3 - 2 * t); }

    public static float Greenness(Biome b) => b switch { Biome.Temperate => 0.85f, Biome.Tropical => 1f, Biome.Cold => 0.6f, _ => 0.08f };

    public static void Weights(in SplatInputs s, Span<float> w)
    {
        var f = s.ClassFraction;
        float F(TerrainClass c) => f[(int)c];
        float rockSlope = SmoothStep(0.10f, 0.32f, s.Slope);
        float green = Greenness(s.Biome);
        float grassy = (F(TerrainClass.Open) + F(TerrainClass.Hills) * 0.85f + F(TerrainClass.Desert) * 0.15f) * (1 - rockSlope);
        bool dry = s.Biome is Biome.Arid or Biome.Tropical;
        w[(int)Material.Grass] = grassy * green * (0.5f + s.Noise);
        w[(int)Material.DryGrass] = grassy * (1 - green) * (1.5f - s.Noise) + F(TerrainClass.Hills) * 0.15f * (1 - rockSlope);
        w[(int)Material.Dirt] = F(TerrainClass.Road) * 2.5f + grassy * 0.25f * SmoothStep(0.55f, 0.85f, 1 - s.Noise) + F(TerrainClass.Desert) * 0.35f * s.Noise;
        w[(int)Material.Rock] = rockSlope * 1.6f + F(TerrainClass.Mountain);
        w[(int)Material.Sand] = F(TerrainClass.Beach) * 2.5f + F(TerrainClass.Desert) * 1.6f + F(TerrainClass.Water) * (dry ? 1.2f : 0.3f);
        w[(int)Material.Snow] = s.Biome == Biome.Cold
            ? SmoothStep(0.35f, 0.75f, s.HeightRel) * (1 - rockSlope * 0.6f) * 1.5f
            : SmoothStep(0.88f, 1f, s.HeightRel) * F(TerrainClass.Mountain) * (s.Biome == Biome.Temperate ? 1.5f : 0f);
        w[(int)Material.ForestFloor] = (F(TerrainClass.Forest) + F(TerrainClass.Jungle)) * 1.6f * (1 - rockSlope * 0.5f);
        w[(int)Material.Mud] = F(TerrainClass.Water) * (s.Biome == Biome.Arid ? 0.3f : 1.4f)
            + (s.Biome is Biome.Temperate or Biome.Tropical ? grassy * 0.15f * SmoothStep(0.7f, 0.95f, s.Noise) : 0f);
        float sum = 0; for (int i = 0; i < Count; i++) sum += w[i];
        if (sum < 1e-6f) { w.Clear(); w[(int)Material.Grass] = 1; return; }
        for (int i = 0; i < Count; i++) w[i] /= sum;
    }

    /// <summary>Biome palette: LINEAR albedo per material (authored in sRGB, converted).</summary>
    public static Vector3[] Palette(Biome b)
    {
        (int r, int g, int bl)[] s = b switch
        {
            Biome.Arid => new[] { (112, 108, 62), (150, 102, 66), (146, 108, 82), (192, 154, 106), (232, 236, 240), (100, 84, 58), (120, 96, 70), (170, 140, 92) },
            Biome.Tropical => new[] { (60, 112, 40), (124, 80, 52), (96, 94, 86), (222, 206, 166), (232, 236, 240), (52, 62, 30), (80, 62, 44), (140, 140, 70) },
            Biome.Cold => new[] { (88, 104, 72), (96, 86, 72), (104, 106, 108), (150, 146, 132), (236, 240, 248), (58, 60, 48), (70, 64, 56), (130, 124, 96) },
            _ => new[] { (86, 112, 50), (112, 90, 64), (118, 114, 106), (180, 164, 126), (232, 236, 240), (64, 58, 38), (72, 60, 46), (152, 142, 86) },
        };
        var o = new Vector3[Count];
        for (int i = 0; i < Count; i++) o[i] = new Vector3(Lin(s[i].r), Lin(s[i].g), Lin(s[i].bl));
        return o;
    }

    static float Lin(int c) => MathF.Pow(c / 255f, 2.2f);
}
