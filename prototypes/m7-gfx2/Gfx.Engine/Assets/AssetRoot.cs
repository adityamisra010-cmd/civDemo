using System.Buffers.Binary;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Engine.Assets;

/// <summary>
/// Where external assets live: <c>$G2_ASSETS</c> (default <c>/home/user/g2w/assets</c>), with <c>raw/</c> holding the
/// pinned upstream files (scripts/fetch-assets.sh) and <c>baked/</c> the bake outputs (scripts/bake-render1.sh).
/// Nothing here is committed. Missing files are not an error: callers fall back to procedural content and say so.
/// </summary>
public static class AssetRoot
{
    public const string DefaultRoot = "/home/user/g2w/assets";

    public static string Root
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("G2_ASSETS");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            // a packaged build carries its assets next to the executable
            string local = Path.Combine(AppContext.BaseDirectory, "assets");
            return Directory.Exists(local) ? local : DefaultRoot;
        }
    }

    public static string Raw(string relative) => Path.Combine(Root, "raw", relative);
    public static string Baked(string relative) => Path.Combine(Root, "baked", relative);

    /// <summary>The default image-based-lighting panorama (Poly Haven "Harties cliff view", CC0, via BabylonJS/Assets).</summary>
    public const string DefaultHdri = "babylon-assets/ibl/harties_cliff_view/harties_cliff_view_4k.hdr";
}

/// <summary>
/// Baked texture container written by <c>scripts/bake-render1-terrain.py</c>: little-endian
/// <c>"R1TX" | u32 version(1) | u32 width | u32 height | u32 levels | u32 format(0 = RGBA8) | levels × (w·h·4 bytes)</c>,
/// level 0 first, each level half the previous (min 1). Mip filtering, normal renormalisation and roughness
/// adjustment happen offline in the bake (recorded in PROVENANCE.md).
/// </summary>
public static class BakedTexture
{
    public const uint Magic = 0x58543152; // "R1TX"

    public sealed record Header(int Width, int Height, int Levels, int Format);

    public static Header ReadHeader(ReadOnlySpan<byte> b)
    {
        if (b.Length < 24 || BinaryPrimitives.ReadUInt32LittleEndian(b) != Magic) throw new InvalidDataException("not an R1TX file");
        if (BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) != 1) throw new InvalidDataException("unsupported R1TX version");
        return new Header((int)BinaryPrimitives.ReadUInt32LittleEndian(b[8..]), (int)BinaryPrimitives.ReadUInt32LittleEndian(b[12..]),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(b[16..]), (int)BinaryPrimitives.ReadUInt32LittleEndian(b[20..]));
    }

    /// <summary>Level byte arrays (RGBA8), validated against the header.</summary>
    public static (Header header, byte[][] levels) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var h = ReadHeader(bytes);
        if (h.Format != 0) throw new InvalidDataException($"{path}: unsupported format {h.Format}");
        var levels = new byte[h.Levels][]; int off = 24, w = h.Width, ht = h.Height;
        for (int l = 0; l < h.Levels; l++)
        {
            int n = w * ht * 4;
            if (off + n > bytes.Length) throw new InvalidDataException($"{path}: truncated at level {l}");
            levels[l] = bytes.AsSpan(off, n).ToArray(); off += n;
            w = Math.Max(1, w / 2); ht = Math.Max(1, ht / 2);
        }
        if (off != bytes.Length) throw new InvalidDataException($"{path}: {bytes.Length - off} trailing bytes");
        return (h, levels);
    }

    /// <summary>Uploads a baked file as a mipmapped RGBA8 texture (levels the file lacks are box-filtered here).</summary>
    public static Texture2D Load(GraphicsDevice gd, string path)
    {
        var (h, levels) = Read(path);
        var tex = new Texture2D(gd, h.Width, h.Height, true, SurfaceFormat.Color);
        int w = h.Width, ht = h.Height; byte[] cur = levels[0];
        for (int l = 0; l < tex.LevelCount; l++)
        {
            if (l < levels.Length) cur = levels[l];
            else cur = Downsample(cur, w * 2, ht * 2, w, ht);
            tex.SetData(l, null, cur, 0, cur.Length);
            w = Math.Max(1, w / 2); ht = Math.Max(1, ht / 2);
        }
        return tex;
    }

    static byte[] Downsample(byte[] src, int sw, int sh, int dw, int dh)
    {
        sw = Math.Max(1, sw); sh = Math.Max(1, sh);
        var dst = new byte[dw * dh * 4];
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
                for (int c = 0; c < 4; c++)
                {
                    int x0 = Math.Min(2 * x, sw - 1), x1 = Math.Min(2 * x + 1, sw - 1), y0 = Math.Min(2 * y, sh - 1), y1 = Math.Min(2 * y + 1, sh - 1);
                    int s = src[(y0 * sw + x0) * 4 + c] + src[(y0 * sw + x1) * 4 + c] + src[(y1 * sw + x0) * 4 + c] + src[(y1 * sw + x1) * 4 + c];
                    dst[(y * dw + x) * 4 + c] = (byte)((s + 2) / 4);
                }
        return dst;
    }
}

/// <summary>Float texture helpers shared by the lighting code.</summary>
public static class FloatTextures
{
    /// <summary>RGB(A) float data (row-major, 4 floats per texel) → HalfVector4 texture, single level.</summary>
    public static Texture2D Half4(GraphicsDevice gd, int w, int h, ReadOnlySpan<float> rgba)
    {
        var tex = new Texture2D(gd, w, h, false, SurfaceFormat.HalfVector4);
        tex.SetData(ToHalf(rgba));
        return tex;
    }

    public static HalfVector4[] ToHalf(ReadOnlySpan<float> rgba)
    {
        var d = new HalfVector4[rgba.Length / 4];
        for (int i = 0; i < d.Length; i++)
            d[i] = new HalfVector4(Clamp(rgba[4 * i]), Clamp(rgba[4 * i + 1]), Clamp(rgba[4 * i + 2]), Clamp(rgba[4 * i + 3]));
        return d;
    }

    /// <summary>Half floats saturate at 65504; NaN becomes 0.</summary>
    static float Clamp(float v) => float.IsNaN(v) ? 0f : Math.Clamp(v, -65000f, 65000f);
}
