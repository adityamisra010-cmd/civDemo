using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Presentation;

/// <summary>
/// The land (and sea, on coastal maps) beyond the 1 km battle map, out to <see cref="OuterHalfSize"/> (stage render1;
/// PRESENTATION ONLY, never read by the simulation: the battle map ends at its edge). G1 drew nothing there, so high
/// shots showed a grey or orange wall. Square rings with geometric spacing start exactly at the map edge, where the
/// heights are the authoritative edge heights (read only); outward they blend into biome hills, distant ridges and,
/// for a coastal map, a continued coastline falling to a sea floor under a far-sea surface. Colours are biome
/// averages of the photographic materials; the shader adds macro variation, aerial perspective and an edge fade.
/// </summary>
public sealed class FarFieldMesh
{
    public const float OuterHalfSize = 30_000f;
    public const int SidesSegments = 128;
    public MeshData<VertexPNC> Land { get; }
    public MeshData<VertexWater>? Sea { get; }
    public int Rings { get; }

    FarFieldMesh(MeshData<VertexPNC> land, MeshData<VertexWater>? sea, int rings) { Land = land; Sea = sea; Rings = rings; }

    /// <summary>Perimeter point k of 4·M on a square of half-size s (counter-clockwise from (−s, −s)).</summary>
    static Vector2 Perimeter(int k, int m, float s)
    {
        int side = k / m; float f = (k % m) / (float)m;
        return side switch
        {
            0 => new Vector2(-s + 2 * s * f, -s),
            1 => new Vector2(s, -s + 2 * s * f),
            2 => new Vector2(s - 2 * s * f, s),
            _ => new Vector2(-s, s - 2 * s * f),
        };
    }

    public static FarFieldMesh Build(TerrainMeshData t, uint seed)
    {
        var g = t.Grid;
        float s0 = MathF.Min(t.HalfExtentX, t.HalfExtentZ);
        const float q = 1.17f;
        var sizes = new List<float> { s0 };
        while (sizes[^1] < OuterHalfSize) sizes.Add(MathF.Min(sizes[^1] * q, OuterHalfSize));
        int rings = sizes.Count, M = SidesSegments, P = 4 * M;
        float mean = 0; for (int i = 0; i < g.HeightMm.Length; i++) mean += g.HeightMm[i] / 1000f; mean /= g.HeightMm.Length;
        float relief = MathF.Max(20f, t.MaxHeightM - t.MinHeightM);
        var biome = g.Biome;
        int coast = -1;
        // the generator stores the coast edge implicitly: find the edge whose cells are mostly sea
        if (g.WaterLevelMm == 0)
        {
            int best = -1; int bestWet = 0;
            for (int e = 0; e < 4; e++)
            {
                int wet = 0;
                for (int k = 0; k < g.Width; k++)
                {
                    int x = e switch { 1 => g.Width - 1, 3 => 0, _ => k }, y = e switch { 0 => 0, 2 => g.Height - 1, _ => k };
                    if (x < g.Width && y < g.Height && g.WaterDepthMm[g.Index(x, y)] > 0 && g.HeightMm[g.Index(x, y)] < -2000) wet++;
                }
                if (wet > bestWet) { bestWet = wet; best = e; }
            }
            if (bestWet > g.Width * 7 / 10) coast = best;
        }
        // coastline position along the coast normal, measured on the map (mean of the first dry cell per column)
        float coastLine = 0;
        if (coast >= 0)
        {
            float sum = 0; int n = 0;
            for (int k = 0; k < g.Width; k += 4)
            {
                for (int d = 0; d < g.Height; d++)
                {
                    int x = coast switch { 1 => g.Width - 1 - d, 3 => d, _ => k }, y = coast switch { 0 => d, 2 => g.Height - 1 - d, _ => k };
                    if (g.WaterDepthMm[g.Index(x, y)] == 0) { sum += s0 - d * t.CellM; n++; break; }
                }
            }
            coastLine = n > 0 ? sum / n : s0 * 0.4f;
        }

        float Noise(float x, float z, uint salt, int oct, float scale) => PNoise.Fbm(x / scale + 1000f, z / scale + 1000f, seed ^ salt, oct);

        float SeaWard(float x, float z) => coast switch { 0 => -z, 1 => x, 2 => z, 3 => -x, _ => 0 };

        float Height(float x, float z)
        {
            float d = MathF.Max(MathF.Abs(x) - t.HalfExtentX, MathF.Abs(z) - t.HalfExtentZ);
            float edge = t.HeightAt(Math.Clamp(x, -t.HalfExtentX, t.HalfExtentX), Math.Clamp(z, -t.HalfExtentZ, t.HalfExtentZ));
            float hills = mean + relief * 0.9f * (Noise(x, z, 0xF1u, 5, 2600f) - 0.5f) * 2f;
            float ridgeAmp = biome switch { Biome.Cold => 520f, Biome.Arid => 160f, Biome.Tropical => 300f, _ => 380f };
            float ridge = 1f - MathF.Abs(2f * Noise(x, z, 0xF2u, 4, 9000f) - 1f);
            float far = hills + ridgeAmp * ridge * ridge * SplatModel.SmoothStep(3500f, 16000f, MathF.Max(d, 0)) + relief * 0.6f * SplatModel.SmoothStep(0f, 2500f, d);
            float h = MathHelper.Lerp(edge, far, SplatModel.SmoothStep(0f, 1700f, MathF.Max(d, 0)));
            if (coast >= 0)
            {
                float c = SeaWard(x, z) - coastLine + (Noise(x, z, 0xF3u, 3, 1800f) - 0.5f) * 600f;
                float sea = SplatModel.SmoothStep(-80f, 700f, c);
                h = MathHelper.Lerp(h, -6f - 0.004f * MathF.Max(c, 0), sea);
                // keep the shore low next to the sea
                h = MathF.Min(h, MathHelper.Lerp(h, 2f + MathF.Max(-c, 0) * 0.05f, SplatModel.SmoothStep(-1200f, 0f, c)));
            }
            return d <= 0.5f ? edge - 1.0f : h;
        }

        Color Colour(float x, float z, float h, Vector3 n)
        {
            float slope = 1 - n.Y;
            float forest = SplatModel.SmoothStep(0.46f, 0.56f, Noise(x, z, 0xF4u, 4, 1400f));
            float dry = Noise(x, z, 0xF5u, 3, 3000f);
            Vector3 grass, canopy, rock, sand = new(196, 178, 140), snow = new(232, 236, 240);
            switch (biome)
            {
                case Biome.Arid: grass = new(150, 128, 86); canopy = new(104, 98, 62); rock = new(150, 120, 96); forest *= 0.25f; break;
                case Biome.Cold: grass = new(98, 108, 78); canopy = new(46, 60, 46); rock = new(116, 116, 114); break;
                case Biome.Tropical: grass = new(78, 112, 46); canopy = new(38, 66, 30); rock = new(104, 100, 92); forest = MathF.Min(1, forest * 1.4f); break;
                default: grass = new(92, 108, 54); canopy = new(54, 70, 38); rock = new(122, 118, 110); break;
            }
            var c = Vector3.Lerp(grass, grass * new Vector3(1.18f, 1.05f, 0.82f), dry * 0.6f);
            c = Vector3.Lerp(c, canopy, forest);
            c = Vector3.Lerp(c, rock, SplatModel.SmoothStep(0.18f, 0.40f, slope));
            if (coast >= 0) c = Vector3.Lerp(c, sand, SplatModel.SmoothStep(4f, 0.5f, h));
            if (biome == Biome.Cold || biome == Biome.Temperate) c = Vector3.Lerp(c, snow, SplatModel.SmoothStep(biome == Biome.Cold ? 260f : 520f, biome == Biome.Cold ? 360f : 640f, h));
            return new Color((int)c.X, (int)c.Y, (int)c.Z);
        }

        var verts = new VertexPNC[rings * P];
        for (int r = 0; r < rings; r++)
            for (int k = 0; k < P; k++)
            {
                var p = Perimeter(k, M, sizes[r]);
                float h = Height(p.X, p.Y);
                float e = MathF.Max(4f, sizes[r] * 0.02f);
                var n = Vector3.Normalize(new Vector3(Height(p.X - e, p.Y) - Height(p.X + e, p.Y), 2 * e, Height(p.X, p.Y - e) - Height(p.X, p.Y + e)));
                verts[r * P + k] = new VertexPNC(new Vector3(p.X, h, p.Y), n, Colour(p.X, p.Y, h, n));
            }
        var idx = new List<int>((rings - 1) * P * 6);
        for (int r = 0; r < rings - 1; r++)
            for (int k = 0; k < P; k++)
            {
                int a = r * P + k, b = r * P + (k + 1) % P, c = (r + 1) * P + k, d = (r + 1) * P + (k + 1) % P;
                idx.Add(a); idx.Add(c); idx.Add(b); idx.Add(b); idx.Add(c); idx.Add(d);
            }
        var land = new MeshData<VertexPNC>(verts, idx.ToArray());

        MeshData<VertexWater>? sea = null;
        if (coast >= 0)
        {
            // The in-map water's cell corners run from (−0.5 − W/2)·cell to (W − 0.5 − W/2)·cell. The far sea starts 1 m
            // inside that edge, fully transparent, and reaches its depth-driven opacity exactly at the edge, under the
            // in-map water: no crack and no thin band where the bright sea floor shows through (Data.y carries the fade).
            float lo = (-0.5f - g.Width / 2) * t.CellM, hi = (g.Width - 0.5f - g.Width / 2) * t.CellM;
            float half0 = (hi - lo) / 2, centre0 = (hi + lo) / 2;
            var seaSizes = new List<(float half, Vector2 centre, float fade)> { (half0 - 1.0f, new(centre0, centre0), 0f), (half0, new(centre0, centre0), 1f) };
            foreach (var sz in sizes) if (sz > half0 + 40f) seaSizes.Add((sz, Vector2.Zero, 1f));
            var sv = new VertexWater[seaSizes.Count * P];
            for (int r = 0; r < seaSizes.Count; r++)
                for (int k = 0; k < P; k++)
                {
                    var (half, centre, fade) = seaSizes[r];
                    var p = Perimeter(k, M, half) + centre;
                    // water depth over the far field's own sea floor: shallow near the coast, so the far sea continues
                    // the in-map sea's colour instead of starting as a uniform deep plane at the map edge
                    float depth = Math.Clamp(-Height(p.X, p.Y), 0.05f, 30f);
                    sv[r * P + k] = new VertexWater(new Vector3(p.X, -0.03f, p.Y), depth) { Data = new Vector4(depth, fade, 0, 0) };
                }
            var sidx = new List<int>((seaSizes.Count - 1) * P * 6);
            for (int r = 0; r < seaSizes.Count - 1; r++)
                for (int k = 0; k < P; k++)
                {
                    int a = r * P + k, b = r * P + (k + 1) % P, c = (r + 1) * P + k, d = (r + 1) * P + (k + 1) % P;
                    sidx.Add(a); sidx.Add(c); sidx.Add(b); sidx.Add(b); sidx.Add(c); sidx.Add(d);
                }
            sea = new MeshData<VertexWater>(sv, sidx.ToArray());
        }
        return new FarFieldMesh(land, sea, rings);
    }
}
