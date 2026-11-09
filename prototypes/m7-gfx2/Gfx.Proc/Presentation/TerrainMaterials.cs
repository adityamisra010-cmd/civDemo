using Gfx.Engine.Assets;
using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>
/// The G2 photographic terrain sets (stage render1; PRESENTATION ONLY): five baked PBR sets
/// (<c>scripts/bake-render1.sh</c>) — grass (ambientCG Ground037 via Terrain3D, CC0), gravel (BabylonJS rockyGround,
/// CC-BY-4.0), rock (ambientCG Rock023 via Terrain3D, CC0), sand and soil (BabylonJS sand/dirt, CC-BY-4.0) — each as
/// albedo+height and normal+roughness, plus a procedural macro-variation texture. The eight splat materials map onto
/// the five sets with a per-biome tint (<see cref="MaterialLook"/>). Falls back to G1's procedural materials (and
/// says so) when the baked files are missing.
/// </summary>
public sealed class TerrainMaterials : IDisposable
{
    public static readonly string[] SetNames = { "grass", "gravel", "rock", "sand", "soil" };
    /// <summary>World size (m) of one texture repeat per set.</summary>
    public static readonly float[] TileMetres = { 3.2f, 2.6f, 6.0f, 2.2f, 3.0f };

    public Texture2D[] AlbHt { get; }
    public Texture2D[] NrmRgh { get; }
    public Texture2D Macro { get; }
    public string Source { get; }

    TerrainMaterials(Texture2D[] a, Texture2D[] n, Texture2D macro, string source) { AlbHt = a; NrmRgh = n; Macro = macro; Source = source; }

    static readonly object Gate = new();
    static string? _lastFailure;

    /// <summary>Loads the baked sets, or returns null (with the reason in <paramref name="failure"/>).</summary>
    public static TerrainMaterials? TryLoad(GraphicsDevice gd, out string? failure)
    {
        var a = new Texture2D[5]; var n = new Texture2D[5];
        try
        {
            for (int i = 0; i < 5; i++)
            {
                a[i] = BakedTexture.Load(gd, AssetRoot.Baked($"render1/terrain/{SetNames[i]}_alb_ht.r1tex"));
                n[i] = BakedTexture.Load(gd, AssetRoot.Baked($"render1/terrain/{SetNames[i]}_nrm_rgh.r1tex"));
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            foreach (var t in a) t?.Dispose();
            foreach (var t in n) t?.Dispose();
            failure = $"baked terrain materials unavailable ({e.Message}); run scripts/bake-render1.sh. Using G1 procedural materials.";
            lock (Gate) { if (_lastFailure != failure) { Console.Error.WriteLine("[terrain] " + failure); _lastFailure = failure; } }
            return null;
        }
        failure = null;
        return new TerrainMaterials(a, n, CreateMacro(gd), AssetRoot.Baked("render1/terrain"));
    }

    /// <summary>
    /// 256² tileable RGBA8 macro texture (deterministic): r = broad brightness (fBm), g = hue drift, b = patch mask
    /// (dry or lush patches), a = blend mask between the two detail scales.
    /// </summary>
    public static Texture2D CreateMacro(GraphicsDevice gd)
    {
        const int S = 256;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float r = PNoise.Fbm(u * 4, v * 4, 0x51u, 5, 4);
                float g = PNoise.Fbm(u * 3, v * 3, 0x52u, 4, 3);
                float b = SplatModel.SmoothStep(0.42f, 0.62f, PNoise.Fbm(u * 6, v * 6, 0x53u, 4, 6));
                float a = SplatModel.SmoothStep(0.35f, 0.65f, PNoise.Fbm(u * 8, v * 8, 0x54u, 3, 8));
                px[y * S + x] = new Color(r, g, b, a);
            }
        var tex = new Texture2D(gd, S, S, true, SurfaceFormat.Color);
        var cur = px; int size = S;
        for (int l = 0; l < tex.LevelCount; l++)
        {
            tex.SetData(l, null, cur, 0, cur.Length);
            if (size == 1) break;
            int ns = size / 2; var next = new Color[ns * ns];
            for (int yy = 0; yy < ns; yy++)
                for (int xx = 0; xx < ns; xx++)
                {
                    Color p = cur[2 * yy * size + 2 * xx], q = cur[2 * yy * size + 2 * xx + 1], r = cur[(2 * yy + 1) * size + 2 * xx], s = cur[(2 * yy + 1) * size + 2 * xx + 1];
                    next[yy * ns + xx] = new Color((p.R + q.R + r.R + s.R + 2) / 4, (p.G + q.G + r.G + s.G + 2) / 4, (p.B + q.B + r.B + s.B + 2) / 4, (p.A + q.A + r.A + s.A + 2) / 4);
                }
            cur = next; size = ns;
        }
        return tex;
    }

    public void Dispose()
    {
        foreach (var t in AlbHt) t.Dispose();
        foreach (var t in NrmRgh) t.Dispose();
        Macro.Dispose();
    }
}

/// <summary>
/// Per-biome look of the eight splat materials on the five photographic sets: linear tint, saturation, roughness
/// multiplier. Tints are multipliers near 1 so the calibrated albedo stays in natural ranges (snow is the exception:
/// it whitens the sand set).
/// </summary>
public static class MaterialLook
{
    public readonly record struct Entry(int Set, Vector3 Tint, float Saturation, float RoughMul);

    public static Entry[] For(Biome b)
    {
        // order: Grass, Dirt, Rock, Sand, Snow, ForestFloor, Mud, DryGrass
        var lush = b switch { Biome.Tropical => new Vector3(0.74f, 1.12f, 0.62f), Biome.Cold => new Vector3(0.92f, 1.02f, 0.92f), Biome.Arid => new Vector3(1.75f, 1.50f, 1.05f), _ => new Vector3(0.78f, 1.10f, 0.66f) };
        var lushSat = b == Biome.Arid ? 0.6f : 1.15f;   // arid 'grass' is pale, sparse dry grass
        return new[]
        {
            new Entry(0, lush, lushSat, 1.0f),                                    // Grass
            new Entry(1, b == Biome.Arid ? new Vector3(1.25f, 1.08f, 0.86f) : new Vector3(1.0f, 0.97f, 0.92f), 0.9f, 1.0f), // Dirt: gravel and stony tracks (warm in deserts)
            new Entry(2, b == Biome.Arid ? new Vector3(1.18f, 1.0f, 0.82f) : new Vector3(0.92f, 0.88f, 0.82f), 0.9f, 1.0f), // Rock: a little warm and dark
            new Entry(3, Vector3.One, 1.0f, 1.0f),                                // Sand
            new Entry(3, new Vector3(2.7f, 2.75f, 2.95f), 0.05f, 0.55f),          // Snow (whitened sand set)
            new Entry(4, new Vector3(0.62f, 0.62f, 0.50f), 0.85f, 1.05f),         // ForestFloor: dark litter
            new Entry(4, new Vector3(0.55f, 0.50f, 0.45f), 0.8f, 0.45f),          // Mud: darker, wet (smoother)
            new Entry(0, b == Biome.Arid ? new Vector3(2.15f, 1.75f, 1.15f) : new Vector3(1.45f, 1.10f, 0.62f), b == Biome.Arid ? 0.45f : 0.55f, 1.05f), // DryGrass: straw (pale, sun-bleached in deserts)
        };
    }
}
