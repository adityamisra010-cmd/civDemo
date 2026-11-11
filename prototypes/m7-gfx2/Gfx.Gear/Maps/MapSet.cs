using Gfx.Gear.Geometry;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Maps;

/// <summary>What a material function returns for one texel (linear albedo, height in metres above the base surface).</summary>
public struct Texel
{
    public Vector3 Albedo;
    public float Height, Rough, Metal, Ao, Alpha;
    public static Texel Of(Vector3 albedo, float rough, float metal, float height = 0f, float ao = 1f) =>
        new() { Albedo = albedo, Rough = rough, Metal = metal, Height = height, Ao = ao, Alpha = 1f };
}

/// <summary>The surface under one texel, interpolated from the mesh (item space) or given analytically.</summary>
public struct Surf
{
    public Vector3 Position, Normal;
    public Vector2 Uv;
    public Vector4 Mask;            // the mesh's mask colour (0..1 per channel), interpolated
    public int Triangle;
    public float MetresPerTexel;    // world size of one texel (geometric mean of the u and v directions)
}

/// <summary>
/// A PBR map set painted in code: base colour (sRGB, alpha = opacity), tangent-space normal (OpenGL convention: green
/// up, matching <see cref="KitMesh.ComputeTangents"/>), and ORM (R occlusion, G roughness, B metallic; glTF's packing).
/// Painting: analytic regions (<see cref="PaintRect"/>) or a mesh rasterised in UV space (<see cref="PaintMesh"/>);
/// heights become normals scaled by each texel's world size; cavities darken the occlusion; uncovered texels are
/// filled by dilation so mips do not bleed. Deterministic: rasterisation is sequential in triangle order and every
/// texel's shading is a pure function of its inputs (rows may be shaded in parallel).
/// </summary>
public sealed class MapSet
{
    public readonly int Width, Height;
    public readonly string Name;
    readonly float[] _r, _g, _b, _h, _rough, _metal, _ao, _alpha, _mpt;
    readonly int[] _island;     // 0 = uncovered, else island id (normals never difference across islands)

    public byte[] BaseRgba { get; private set; } = Array.Empty<byte>();
    public byte[] NormalRgba { get; private set; } = Array.Empty<byte>();
    public byte[] OrmRgba { get; private set; } = Array.Empty<byte>();
    /// <summary>Strength of the cavity occlusion derived from the height field (0 = none).</summary>
    public float CavityStrength = 0.6f;
    /// <summary>Radius (texels) of the blur that defines "cavity" (height below the local mean).</summary>
    public int CavityRadius = 6;
    /// <summary>Height difference (m) below the local mean that counts as a full cavity.</summary>
    public float CavityDepth = 0.0012f;

    public MapSet(string name, int width, int height)
    {
        Name = name; Width = width; Height = height;
        int n = width * height;
        _r = new float[n]; _g = new float[n]; _b = new float[n]; _h = new float[n]; _rough = new float[n]; _metal = new float[n];
        _ao = new float[n]; _alpha = new float[n]; _mpt = new float[n]; _island = new int[n];
    }

    void Store(int i, in Texel t, float mpt, int island)
    {
        _r[i] = t.Albedo.X; _g[i] = t.Albedo.Y; _b[i] = t.Albedo.Z; _h[i] = t.Height; _rough[i] = t.Rough; _metal[i] = t.Metal;
        _ao[i] = t.Ao; _alpha[i] = t.Alpha; _mpt[i] = mpt; _island[i] = island;
    }

    /// <summary>
    /// Paints an atlas rectangle analytically: f(u, v) with u, v in [0,1] across the rectangle (texel centres).
    /// <paramref name="metresPerU"/>/<paramref name="metresPerV"/>: world size of the whole rectangle along u and v.
    /// <paramref name="covered"/>(u, v) false leaves a texel unpainted (e.g. outside a disk).
    /// </summary>
    public void PaintRect(UvRect rect, double metresPerU, double metresPerV, int island, Func<double, double, Texel> f, Func<double, double, bool>? covered = null)
    {
        int x0 = (int)Math.Floor(rect.U0 * Width), x1 = (int)Math.Ceiling(rect.U1 * Width);
        int y0 = (int)Math.Floor(rect.V0 * Height), y1 = (int)Math.Ceiling(rect.V1 * Height);
        x0 = Math.Clamp(x0, 0, Width); x1 = Math.Clamp(x1, 0, Width); y0 = Math.Clamp(y0, 0, Height); y1 = Math.Clamp(y1, 0, Height);
        double mpt = Math.Sqrt(metresPerU / ((rect.U1 - rect.U0) * Width) * (metresPerV / ((rect.V1 - rect.V0) * Height)));
        Parallel.For(y0, y1, y =>
        {
            for (int x = x0; x < x1; x++)
            {
                double u = ((x + 0.5) / Width - rect.U0) / (rect.U1 - rect.U0);
                double v = ((y + 0.5) / Height - rect.V0) / (rect.V1 - rect.V0);
                if (covered != null && !covered(u, v)) continue;
                var t = f(u, v);
                Store(y * Width + x, t, (float)mpt, island);
            }
        });
    }

    /// <summary>
    /// Rasterises a mesh in UV space (texel centres, conservative by half a texel so UV-island borders are covered)
    /// and shades every covered texel with <paramref name="shade"/>. Later triangles overwrite earlier ones.
    /// </summary>
    public void PaintMesh(KitMesh m, int island, Func<Surf, Texel> shade, Func<int, bool>? triangleFilter = null)
    {
        int n = Width * Height;
        var tri = new int[n]; Array.Fill(tri, -1);
        var b1 = new float[n]; var b2 = new float[n];
        for (int t = 0; t < m.TriangleCount; t++)
        {
            if (triangleFilter != null && !triangleFilter(t)) continue;
            int i0 = m.Indices[3 * t], i1 = m.Indices[3 * t + 1], i2 = m.Indices[3 * t + 2];
            var a = m.Uvs[i0] * new Vector2(Width, Height); var b = m.Uvs[i1] * new Vector2(Width, Height); var c = m.Uvs[i2] * new Vector2(Width, Height);
            double area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(area) < 1e-12) continue;
            int xa = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)) - 1)), xb = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X)) + 1));
            int ya = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)) - 1)), yb = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y)) + 1));
            // edge lengths in texels, for the conservative test (half a texel outside each edge)
            double lab = Math.Max(1e-9, (b - a).Length()), lbc = Math.Max(1e-9, (c - b).Length()), lca = Math.Max(1e-9, (a - c).Length());
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    double w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    double w2 = 1 - w0 - w1;
                    // distance (texels) outside each edge: w_k * 2|area| / |opposite edge|
                    double s = 2 * Math.Abs(area);
                    double d0 = w0 * s / lbc, d1 = w1 * s / lca, d2 = w2 * s / lab;
                    double minD = Math.Min(d0, Math.Min(d1, d2));
                    if (minD < -0.71) continue;
                    int i = y * Width + x;
                    bool inside = minD >= 0;
                    if (!inside && tri[i] >= 0) continue; // a conservative fringe never overwrites a texel another triangle owns
                    tri[i] = t;
                    // clamp the barycentrics into the triangle for fringe texels
                    double cw0 = Math.Max(0, w0), cw1 = Math.Max(0, w1), cw2 = Math.Max(0, w2), sw = cw0 + cw1 + cw2;
                    b1[i] = (float)(cw1 / sw); b2[i] = (float)(cw2 / sw);
                }
        }
        // per-triangle world size of a texel
        var mptTri = new float[m.TriangleCount];
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int i0 = m.Indices[3 * t], i1 = m.Indices[3 * t + 1], i2 = m.Indices[3 * t + 2];
            double world = Vector3.Cross(m.Positions[i1] - m.Positions[i0], m.Positions[i2] - m.Positions[i0]).Length() * 0.5;
            var a = m.Uvs[i0] * new Vector2(Width, Height); var b = m.Uvs[i1] * new Vector2(Width, Height); var c = m.Uvs[i2] * new Vector2(Width, Height);
            double tex = Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5;
            mptTri[t] = tex > 1e-12 ? (float)Math.Sqrt(world / tex) : 0f;
        }
        Parallel.For(0, Height, y =>
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                int t = tri[i];
                if (t < 0) continue;
                int i0 = m.Indices[3 * t], i1 = m.Indices[3 * t + 1], i2 = m.Indices[3 * t + 2];
                float w1 = b1[i], w2 = b2[i], w0 = 1 - w1 - w2;
                var s = new Surf
                {
                    Position = m.Positions[i0] * w0 + m.Positions[i1] * w1 + m.Positions[i2] * w2,
                    Normal = KitMesh.SafeNormalize(m.Normals[i0] * w0 + m.Normals[i1] * w1 + m.Normals[i2] * w2),
                    Uv = m.Uvs[i0] * w0 + m.Uvs[i1] * w1 + m.Uvs[i2] * w2,
                    Mask = m.Masks[i0].ToVector4() * w0 + m.Masks[i1].ToVector4() * w1 + m.Masks[i2].ToVector4() * w2,
                    Triangle = t,
                    MetresPerTexel = mptTri[t],
                };
                Store(i, shade(s), mptTri[t], island);
            }
        });
    }

    /// <summary>Fills uncovered texels from covered neighbours (iterative, deterministic), so mips never pull in black.</summary>
    void Dilate(int passes)
    {
        int n = Width * Height;
        var cov = new bool[n];
        for (int i = 0; i < n; i++) cov[i] = _island[i] != 0;
        for (int p = 0; p < passes; p++)
        {
            var next = (bool[])cov.Clone();
            bool any = false;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (cov[i]) continue;
                    float r = 0, g = 0, b = 0, h = 0, ro = 0, me = 0, ao = 0, al = 0; int k = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= Width || yy >= Height) continue;
                            int j = yy * Width + xx;
                            if (!cov[j]) continue;
                            r += _r[j]; g += _g[j]; b += _b[j]; h += _h[j]; ro += _rough[j]; me += _metal[j]; ao += _ao[j]; al += _alpha[j]; k++;
                        }
                    if (k == 0) continue;
                    _r[i] = r / k; _g[i] = g / k; _b[i] = b / k; _h[i] = h / k; _rough[i] = ro / k; _metal[i] = me / k; _ao[i] = ao / k; _alpha[i] = al / k;
                    next[i] = true; any = true;
                }
            cov = next;
            if (!any) break;
        }
        // anything still empty: a neutral fill
        for (int i = 0; i < n; i++) if (!cov[i]) { _r[i] = _g[i] = _b[i] = 0.2f; _rough[i] = 0.8f; _ao[i] = 1f; _alpha[i] = 1f; }
    }

    /// <summary>Finishes the maps: normals from heights (per island), cavity occlusion, dilation, 8-bit encoding.</summary>
    public MapSet Finish(int dilatePasses = 8)
    {
        int n = Width * Height;
        var nrm = new byte[n * 4];
        var cavity = new float[n];
        // local mean height (separable box blur within islands) for the cavity term
        if (CavityStrength > 0)
        {
            var tmp = new float[n]; var cnt = new float[n]; var mean = new float[n]; var cnt2 = new float[n];
            int r = CavityRadius;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x; if (_island[i] == 0) continue;
                    float s = 0, c = 0;
                    for (int d = -r; d <= r; d++) { int xx = x + d; if (xx < 0 || xx >= Width) continue; int j = y * Width + xx; if (_island[j] != _island[i]) continue; s += _h[j]; c++; }
                    tmp[i] = s; cnt[i] = c;
                }
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x; if (_island[i] == 0) continue;
                    float s = 0, c = 0;
                    for (int d = -r; d <= r; d++) { int yy = y + d; if (yy < 0 || yy >= Height) continue; int j = yy * Width + x; if (_island[j] != _island[i] || cnt[j] == 0) continue; s += tmp[j]; c += cnt[j]; }
                    mean[i] = c > 0 ? s / c : _h[i]; cnt2[i] = c;
                }
            for (int i = 0; i < n; i++)
                if (_island[i] != 0) cavity[i] = (float)Math.Clamp((mean[i] - _h[i]) / CavityDepth, 0, 1);
        }
        Parallel.For(0, Height, y =>
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                float nx = 0, ny = 0, nz = 1;
                if (_island[i] != 0 && _mpt[i] > 0)
                {
                    int isl = _island[i];
                    float H(int xx, int yy)
                    {
                        if (xx < 0 || yy < 0 || xx >= Width || yy >= Height) return _h[i];
                        int j = yy * Width + xx;
                        return _island[j] == isl ? _h[j] : _h[i];
                    }
                    float dhdu = (H(x + 1, y) - H(x - 1, y)) / (2 * _mpt[i]);
                    float dhdv = (H(x, y + 1) - H(x, y - 1)) / (2 * _mpt[i]);
                    // tangent frame: T = +u, B = image up (-v): n = (-dh/du, +dh/dv, 1)
                    var v = Vector3.Normalize(new Vector3(-dhdu, dhdv, 1f));
                    nx = v.X; ny = v.Y; nz = v.Z;
                }
                nrm[4 * i] = Enc(nx); nrm[4 * i + 1] = Enc(ny); nrm[4 * i + 2] = Enc(nz); nrm[4 * i + 3] = 255;
            }
        });
        for (int i = 0; i < n; i++) _ao[i] *= 1f - CavityStrength * cavity[i];
        Dilate(dilatePasses);
        // dilate the normal map too: copy the normal of the nearest covered texel (repeat passes)
        var covered = new bool[n];
        for (int i = 0; i < n; i++) covered[i] = _island[i] != 0;
        for (int p = 0; p < dilatePasses; p++)
        {
            var next = (bool[])covered.Clone(); bool any = false;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x; if (covered[i]) continue;
                    for (int k = 0; k < 4 && !next[i]; k++)
                    {
                        int xx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), yy = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (xx < 0 || yy < 0 || xx >= Width || yy >= Height) continue;
                        int j = yy * Width + xx; if (!covered[j]) continue;
                        nrm[4 * i] = nrm[4 * j]; nrm[4 * i + 1] = nrm[4 * j + 1]; nrm[4 * i + 2] = nrm[4 * j + 2]; next[i] = true; any = true;
                    }
                }
            covered = next; if (!any) break;
        }
        var bas = new byte[n * 4]; var orm = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            bas[4 * i] = Srgb(_r[i]); bas[4 * i + 1] = Srgb(_g[i]); bas[4 * i + 2] = Srgb(_b[i]); bas[4 * i + 3] = Unorm(_alpha[i]);
            orm[4 * i] = Unorm(_ao[i]); orm[4 * i + 1] = Unorm(_rough[i]); orm[4 * i + 2] = Unorm(_metal[i]); orm[4 * i + 3] = 255;
        }
        BaseRgba = bas; NormalRgba = nrm; OrmRgba = orm;
        return this;
    }

    static byte Enc(float v) => (byte)Math.Clamp((int)MathF.Round((v * 0.5f + 0.5f) * 255f), 0, 255);
    public static byte Unorm(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
    public static byte Srgb(float lin)
    {
        lin = Math.Clamp(lin, 0f, 1f);
        float s = lin <= 0.0031308f ? lin * 12.92f : 1.055f * MathF.Pow(lin, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)MathF.Round(s * 255f), 0, 255);
    }
    public static float SrgbToLinear(byte b)
    {
        float s = b / 255f;
        return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>A linear colour from an sRGB hex string (#RRGGBB).</summary>
    public static Vector3 Hex(string hex)
    {
        uint v = uint.Parse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        return new Vector3(SrgbToLinear((byte)(v >> 16)), SrgbToLinear((byte)(v >> 8)), SrgbToLinear((byte)v));
    }

    /// <summary>SHA-256 of the three encoded maps (determinism fingerprint).</summary>
    public string Hash()
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        sha.TransformBlock(BaseRgba, 0, BaseRgba.Length, null, 0);
        sha.TransformBlock(NormalRgba, 0, NormalRgba.Length, null, 0);
        sha.TransformFinalBlock(OrmRgba, 0, OrmRgba.Length);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>Fraction of texels covered by painting (before dilation).</summary>
    public double Coverage()
    {
        int c = 0; foreach (var i in _island) if (i != 0) c++;
        return (double)c / _island.Length;
    }

    /// <summary>A finished map set from encoded maps (a loaded bake): no paint layers, only the three RGBA arrays.</summary>
    public static MapSet FromBytes(string name, int width, int height, byte[] baseRgba, byte[] normalRgba, byte[] ormRgba)
    {
        int n = width * height * 4;
        if (baseRgba.Length != n || normalRgba.Length != n || ormRgba.Length != n) throw new ArgumentException("map sizes differ");
        return new MapSet(name, width, height, empty: true) { BaseRgba = baseRgba, NormalRgba = normalRgba, OrmRgba = ormRgba };
    }

    MapSet(string name, int width, int height, bool empty)
    {
        Name = name; Width = width; Height = height;
        _r = _g = _b = _h = _rough = _metal = _ao = _alpha = _mpt = Array.Empty<float>();
        _island = Array.Empty<int>();
        _ = empty;
    }

    /// <summary>Raw float access for tests and the bake report.</summary>
    public float HeightAt(int x, int y) => _h[y * Width + x];
    public float RoughAt(int x, int y) => _rough[y * Width + x];
    public float MetalAt(int x, int y) => _metal[y * Width + x];
    public Vector3 AlbedoAt(int x, int y) { int i = y * Width + x; return new Vector3(_r[i], _g[i], _b[i]); }
    public int IslandAt(int x, int y) => _island[y * Width + x];
}
