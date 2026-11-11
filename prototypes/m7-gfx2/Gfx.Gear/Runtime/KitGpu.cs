using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Gfx.Chars.Format;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Gear.Runtime;

/// <summary>The three GPU textures of a map set.</summary>
public sealed class KitTextures : IDisposable
{
    public required Texture2D Base { get; init; }
    public required Texture2D Normal { get; init; }
    public required Texture2D Orm { get; init; }
    public void Dispose() { Base.Dispose(); Normal.Dispose(); Orm.Dispose(); }
}

/// <summary>
/// Uploads map sets with full mip chains built on the CPU: base colour averaged in linear light, normals averaged and
/// renormalised, the lost normal length added to the GGX alpha^2 (a Toksvig-style variance term, 2 (1 - |n|) / |n|),
/// occlusion and metal averaged. Converts generated meshes to the character file's mesh records.
/// </summary>
public static class KitGpu
{
    public static KitTextures Upload(GraphicsDevice gd, MapSet m) => Upload(gd, m.Width, m.Height, m.BaseRgba, m.NormalRgba, m.OrmRgba);

    public static KitTextures Upload(GraphicsDevice gd, int w, int h, byte[] baseRgba, byte[] normalRgba, byte[] ormRgba)
    {
        var (bl, nl, ol) = Mips(w, h, baseRgba, normalRgba, ormRgba);
        return new KitTextures { Base = Tex(gd, w, h, bl), Normal = Tex(gd, w, h, nl), Orm = Tex(gd, w, h, ol) };
    }

    static Texture2D Tex(GraphicsDevice gd, int w, int h, List<byte[]> levels)
    {
        var t = new Texture2D(gd, w, h, true, SurfaceFormat.Color);
        for (int l = 0; l < t.LevelCount; l++)
        {
            var d = levels[Math.Min(l, levels.Count - 1)];
            t.SetData(l, null, d, 0, d.Length);
        }
        return t;
    }

    /// <summary>Mip chains of the three maps (level 0 first, down to 1x1).</summary>
    public static (List<byte[]> bas, List<byte[]> nrm, List<byte[]> orm) Mips(int w, int h, byte[] baseRgba, byte[] normalRgba, byte[] ormRgba)
    {
        var bl = new List<byte[]> { baseRgba }; var nl = new List<byte[]> { normalRgba }; var ol = new List<byte[]> { ormRgba };
        int cw = w, ch = h;
        var lut = new float[256];
        for (int i = 0; i < 256; i++) lut[i] = MapSet.SrgbToLinear((byte)i);
        while (cw > 1 || ch > 1)
        {
            int nw = Math.Max(1, cw / 2), nh = Math.Max(1, ch / 2);
            var pb = bl[^1]; var pn = nl[^1]; var po = ol[^1];
            var b = new byte[nw * nh * 4]; var n = new byte[nw * nh * 4]; var o = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
                for (int x = 0; x < nw; x++)
                {
                    int x0 = Math.Min(2 * x, cw - 1), x1 = Math.Min(2 * x + 1, cw - 1), y0 = Math.Min(2 * y, ch - 1), y1 = Math.Min(2 * y + 1, ch - 1);
                    int[] src = { (y0 * cw + x0) * 4, (y0 * cw + x1) * 4, (y1 * cw + x0) * 4, (y1 * cw + x1) * 4 };
                    int d = (y * nw + x) * 4;
                    // base colour in linear light, alpha linear
                    for (int c = 0; c < 3; c++)
                    {
                        float s = 0; foreach (var k in src) s += lut[pb[k + c]];
                        b[d + c] = MapSet.Srgb(s * 0.25f);
                    }
                    { int s = 0; foreach (var k in src) s += pb[k + 3]; b[d + 3] = (byte)((s + 2) / 4); }
                    // normals: average, renormalise; the shortening widens the roughness
                    var nv = Vector3.Zero;
                    foreach (var k in src) nv += new Vector3(pn[k] / 127.5f - 1f, pn[k + 1] / 127.5f - 1f, pn[k + 2] / 127.5f - 1f);
                    nv *= 0.25f;
                    float len = Math.Clamp(nv.Length(), 1e-4f, 1f);
                    var nn = nv / len;
                    n[d] = Enc(nn.X); n[d + 1] = Enc(nn.Y); n[d + 2] = Enc(nn.Z); n[d + 3] = 255;
                    { int s = 0; foreach (var k in src) s += po[k]; o[d] = (byte)((s + 2) / 4); }
                    {
                        // GGX alpha = roughness^2; average alpha^2 and add the normal variance (Toksvig-style)
                        float a2 = 0; foreach (var k in src) { float r = po[k + 1] / 255f; a2 += r * r * r * r; }
                        a2 *= 0.25f;
                        float var = len < 0.9999f ? (1f - len) / len : 0f;
                        float rough = MathF.Sqrt(MathF.Sqrt(Math.Clamp(a2 + 2f * var, 0f, 1f)));
                        o[d + 1] = MapSet.Unorm(rough);
                    }
                    { int s = 0; foreach (var k in src) s += po[k + 2]; o[d + 2] = (byte)((s + 2) / 4); }
                    o[d + 3] = 255;
                }
            bl.Add(b); nl.Add(n); ol.Add(o);
            cw = nw; ch = nh;
        }
        return (bl, nl, ol);
    }

    static byte Enc(float v) => (byte)Math.Clamp((int)MathF.Round((v * 0.5f + 0.5f) * 255f), 0, 255);

    /// <summary>
    /// A generated mesh as a character-file mesh record bound to bones: rigid to one bone (<paramref name="bone"/>), or
    /// with per-vertex joints and weights from <paramref name="skin"/>(vertex) -> up to four (bone, weight) pairs.
    /// The mask colour travels as the vertex colour.
    /// </summary>
    public static MeshDef ToMeshDef(KitMesh m, string name, string material, int bone, Func<int, (int bone, float w)[]>? skin = null, uint flags = 0)
    {
        int n = m.VertexCount;
        var j = new byte[n * 4]; var w = new byte[n * 4]; var col = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            if (skin == null) { j[4 * i] = (byte)bone; w[4 * i] = 255; }
            else
            {
                var inf = skin(i);
                var q = Quantize(inf);
                for (int k = 0; k < 4; k++) { j[4 * i + k] = q[k].bone; w[4 * i + k] = q[k].w; }
            }
            var c = m.Masks[i];
            col[4 * i] = c.R; col[4 * i + 1] = c.G; col[4 * i + 2] = c.B; col[4 * i + 3] = c.A;
        }
        return new MeshDef
        {
            Name = name, Material = material, Flags = flags,
            Positions = m.Positions.ToArray(), Normals = m.Normals.ToArray(), Tangents = m.Tangents.ToArray(), Uvs = m.Uvs.ToArray(),
            Joints = j, Weights = w, Colors = col, Indices = m.Indices.Select(x => (uint)x).ToArray(),
        };
    }

    /// <summary>Up to four influences quantised to bytes summing to 255 (largest remainder), heaviest first.</summary>
    public static (byte bone, byte w)[] Quantize((int bone, float w)[] inf)
    {
        var list = inf.Where(x => x.w > 0).OrderByDescending(x => x.w).ThenBy(x => x.bone).Take(4).ToArray();
        var res = new (byte, byte)[4];
        if (list.Length == 0) { res[0] = (0, 255); return res; }
        float sum = list.Sum(x => x.w);
        var raw = list.Select(x => x.w / sum * 255f).ToArray();
        var fl = raw.Select(x => (int)MathF.Floor(x)).ToArray();
        int rem = 255 - fl.Sum();
        var order = Enumerable.Range(0, raw.Length).OrderByDescending(k => raw[k] - fl[k]).ThenBy(k => k).ToArray();
        for (int k = 0; k < rem; k++) fl[order[k % order.Length]]++;
        for (int k = 0; k < list.Length; k++) res[k] = ((byte)list[k].bone, (byte)fl[k]);
        return res;
    }
}
