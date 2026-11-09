using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Presentation;

public enum TreeKind { Broadleaf, Conifer, Palm, Acacia }

/// <summary>
/// Procedural low-poly prop meshes (PRESENTATION, placeholder-grade): trees (4 kinds), bushes, rocks; each with a
/// variant seed and two LODs (0 near, 1 far ≈ quarter the triangles). Vertex colours are sRGB base colours; per-
/// instance tint adds variation. Origin at the base, +Y up, ~1 unit = 1 m before instance scale.
/// </summary>
public static class PropMeshes
{
    static readonly Color Bark = new(86, 64, 46), Leaf = new(66, 104, 42), Needle = new(42, 74, 46), Frond = new(78, 124, 50),
        AcaciaLeaf = new(104, 114, 52), BushLeaf = new(74, 102, 48), Stone = new(132, 126, 118);

    static float R(uint seed, uint k) => PNoise.U01(PNoise.Hash(seed * 7919u + k));

    public static MeshData<VertexPNC> Tree(TreeKind kind, uint seed, int lod)
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        int seg = lod == 0 ? 6 : 3;
        switch (kind)
        {
            case TreeKind.Broadleaf:
            {
                float h = 4.5f + 3f * R(seed, 1);
                AddCylinder(v, ix, Vector3.Zero, new Vector3(0, h, 0), 0.28f, 0.16f, seg, Bark);
                int blobs = lod == 0 ? 3 + (int)(R(seed, 2) * 2) : 1;
                for (int b = 0; b < blobs; b++)
                {
                    float a = R(seed, 10u + (uint)b) * MathHelper.TwoPi, d = b == 0 ? 0 : 1.1f + R(seed, 20u + (uint)b);
                    var c = new Vector3(MathF.Cos(a) * d, h + 0.4f - 0.6f * d + R(seed, 30u + (uint)b), MathF.Sin(a) * d);
                    float r = (b == 0 ? 2.4f : 1.6f) + 0.6f * R(seed, 40u + (uint)b);
                    AddBlob(v, ix, c, new Vector3(r, r * 0.85f, r), lod == 0 ? 5 : 3, lod == 0 ? 8 : 5, seed + (uint)b, 0.22f, Leaf);
                }
                break;
            }
            case TreeKind.Conifer:
            {
                float h = 8f + 5f * R(seed, 1);
                AddCylinder(v, ix, Vector3.Zero, new Vector3(0, h * 0.3f, 0), 0.25f, 0.18f, seg, Bark);
                int tiers = lod == 0 ? 3 + (int)(R(seed, 2) * 2) : 1;
                for (int t = 0; t < tiers; t++)
                {
                    float f = tiers == 1 ? 0 : t / (float)(tiers - 1);
                    float y0 = h * (0.18f + 0.5f * f), y1 = lod == 0 ? y0 + h * (0.45f - 0.15f * f) : h;
                    float r = (2.6f - 1.6f * f) * (0.85f + 0.3f * R(seed, 5u + (uint)t));
                    AddCylinder(v, ix, new Vector3(0, y0, 0), new Vector3(0, y1, 0), r, 0f, lod == 0 ? 8 : 5, Needle, cap: true);
                }
                break;
            }
            case TreeKind.Palm:
            {
                float h = 7f + 4f * R(seed, 1); float lean = 0.8f + 1.2f * R(seed, 2), dir = R(seed, 3) * MathHelper.TwoPi;
                int n = lod == 0 ? 4 : 2; var prev = Vector3.Zero;
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n;
                    var p = new Vector3(MathF.Cos(dir) * lean * t * t, h * t, MathF.Sin(dir) * lean * t * t);
                    AddCylinder(v, ix, prev, p, 0.24f - 0.08f * t, 0.22f - 0.08f * t, seg, Bark); prev = p;
                }
                int fronds = lod == 0 ? 7 : 4;
                for (int f = 0; f < fronds; f++) AddFrond(v, ix, prev, f / (float)fronds * MathHelper.TwoPi + R(seed, 50u + (uint)f), 3.2f + R(seed, 60u + (uint)f), lod == 0 ? 3 : 1, Frond);
                break;
            }
            default: // Acacia: forked trunk, flat wide canopy
            {
                float h = 4f + 2f * R(seed, 1);
                AddCylinder(v, ix, Vector3.Zero, new Vector3(0, h * 0.5f, 0), 0.25f, 0.2f, seg, Bark);
                for (int k = 0; k < 2; k++)
                {
                    float a = R(seed, 7u + (uint)k) * MathHelper.TwoPi;
                    AddCylinder(v, ix, new Vector3(0, h * 0.5f, 0), new Vector3(MathF.Cos(a) * 1.2f, h, MathF.Sin(a) * 1.2f), 0.18f, 0.1f, seg, Bark);
                }
                float r = 3.2f + 1.2f * R(seed, 9);
                AddBlob(v, ix, new Vector3(0, h + 0.3f, 0), new Vector3(r, 0.7f, r * 0.9f), lod == 0 ? 4 : 3, lod == 0 ? 10 : 5, seed, 0.18f, AcaciaLeaf);
                break;
            }
        }
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }

    public static MeshData<VertexPNC> Bush(uint seed, int lod)
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        int n = lod == 0 ? 2 + (int)(R(seed, 1) * 2) : 1;
        for (int b = 0; b < n; b++)
        {
            float a = R(seed, 2u + (uint)b) * MathHelper.TwoPi, d = b == 0 ? 0 : 0.5f;
            float r = 0.7f + 0.4f * R(seed, 9u + (uint)b);
            AddBlob(v, ix, new Vector3(MathF.Cos(a) * d, r * 0.55f, MathF.Sin(a) * d), new Vector3(r, r * 0.75f, r), lod == 0 ? 4 : 3, lod == 0 ? 7 : 4, seed + (uint)b, 0.25f, BushLeaf);
        }
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }

    public static MeshData<VertexPNC> Rock(uint seed, int lod)
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        float sx = 0.8f + 0.6f * R(seed, 1), sy = 0.45f + 0.4f * R(seed, 2), sz = 0.7f + 0.6f * R(seed, 3);
        AddBlob(v, ix, new Vector3(0, sy * 0.35f, 0), new Vector3(sx, sy, sz), lod == 0 ? 5 : 3, lod == 0 ? 8 : 5, seed, 0.35f, Stone);
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }

    public static void AddCylinder(List<VertexPNC> v, List<int> ix, Vector3 a, Vector3 b, float r0, float r1, int seg, Color col, bool cap = false)
    {
        var axis = Vector3.Normalize(b - a);
        var side = MathF.Abs(axis.Y) > 0.9f ? Vector3.Right : Vector3.Up;
        var u = Vector3.Normalize(Vector3.Cross(axis, side)); var w = Vector3.Cross(axis, u);
        int baseI = v.Count;
        float slope = (r0 - r1) / Math.Max(0.001f, (b - a).Length());
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg * MathHelper.TwoPi;
            var dir = u * MathF.Cos(t) + w * MathF.Sin(t);
            var n = Vector3.Normalize(dir + axis * slope);
            v.Add(new VertexPNC(a + dir * r0, n, col)); v.Add(new VertexPNC(b + dir * r1, n, col));
        }
        for (int i = 0; i < seg; i++)
        {
            int p = baseI + 2 * i;
            ix.Add(p); ix.Add(p + 2); ix.Add(p + 1); ix.Add(p + 1); ix.Add(p + 2); ix.Add(p + 3);
        }
        if (cap && r0 > 0)
        {
            int c = v.Count; v.Add(new VertexPNC(a, -axis, col));
            for (int i = 0; i < seg; i++) { ix.Add(c); ix.Add(baseI + 2 * i); ix.Add(baseI + 2 * i + 2); }
        }
    }

    /// <summary>Lumpy ellipsoid: lat-long sphere with hashed radial displacement (seamless around the longitude).</summary>
    public static void AddBlob(List<VertexPNC> v, List<int> ix, Vector3 c, Vector3 radii, int lat, int lon, uint seed, float amp, Color col)
    {
        int baseI = v.Count;
        for (int i = 0; i <= lat; i++)
        {
            float th = i / (float)lat * MathHelper.Pi;
            for (int j = 0; j <= lon; j++)
            {
                float ph = j / (float)lon * MathHelper.TwoPi;
                var d = new Vector3(MathF.Sin(th) * MathF.Cos(ph), MathF.Cos(th), MathF.Sin(th) * MathF.Sin(ph));
                int jj = (i == 0 || i == lat) ? 0 : j % lon;
                float k = 1 + amp * (PNoise.U01(PNoise.Hash(jj, i, seed)) - 0.5f) * 2;
                var p = c + d * radii * k;
                var n = Vector3.Normalize(d / radii);
                byte shade = (byte)(200 + 55 * (d.Y * 0.5f + 0.5f));
                var cc = new Color(col.R * shade / 255, col.G * shade / 255, col.B * shade / 255);
                v.Add(new VertexPNC(p, n, cc));
            }
        }
        int row = lon + 1;
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = baseI + i * row + j, b = a + 1, cI = a + row, d = cI + 1;
                ix.Add(a); ix.Add(cI); ix.Add(b); ix.Add(b); ix.Add(cI); ix.Add(d);
            }
    }

    static void AddFrond(List<VertexPNC> v, List<int> ix, Vector3 top, float ang, float len, int segs, Color col)
    {
        var dir = new Vector3(MathF.Cos(ang), 0, MathF.Sin(ang)); var side = new Vector3(-dir.Z, 0, dir.X);
        int baseI = v.Count;
        for (int s = 0; s <= segs; s++)
        {
            float t = s / (float)segs;
            var p = top + dir * len * t + Vector3.Up * (0.6f * t - 1.6f * t * t);
            float wdt = 0.55f * MathF.Sin(MathHelper.Pi * MathF.Min(1, t * 1.1f + 0.05f));
            var n = Vector3.Normalize(Vector3.Up - dir * (1.6f * t - 0.3f));
            v.Add(new VertexPNC(p - side * wdt, n, col)); v.Add(new VertexPNC(p + side * wdt, n, col));
        }
        for (int s = 0; s < segs; s++)
        {
            int p = baseI + 2 * s;
            ix.Add(p); ix.Add(p + 2); ix.Add(p + 1); ix.Add(p + 1); ix.Add(p + 2); ix.Add(p + 3);
        }
    }
}
