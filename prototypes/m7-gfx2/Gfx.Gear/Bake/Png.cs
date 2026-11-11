using System.Buffers.Binary;
using System.IO.Compression;

namespace Gfx.Gear.Bake;

/// <summary>
/// Minimal PNG (ISO/IEC 15948) writer and reader for the bake: 8-bit RGB or RGBA, non-interlaced. The writer picks a
/// filter per row (the smallest sum of absolute filtered bytes, the usual heuristic) and compresses with zlib at a
/// fixed level, so the same pixels always give the same bytes on one runtime. The reader handles all five filters.
/// </summary>
public static class Png
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>Encodes RGBA pixels; <paramref name="channels"/> 3 drops alpha (RGB file), 4 keeps it.</summary>
    public static byte[] Encode(int w, int h, byte[] rgba, int channels)
    {
        if (channels != 3 && channels != 4) throw new ArgumentOutOfRangeException(nameof(channels));
        int stride = w * channels;
        var raw = new byte[(stride + 1) * h];
        var prev = new byte[stride]; var cur = new byte[stride];
        var cand = new byte[5][];
        for (int f = 0; f < 5; f++) cand[f] = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                for (int c = 0; c < channels; c++) cur[x * channels + c] = rgba[(y * w + x) * 4 + c];
            int best = 0; long bestSum = long.MaxValue;
            for (int f = 0; f < 5; f++)
            {
                var o = cand[f]; long sum = 0;
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= channels ? cur[i - channels] : 0, b = prev[i], cc = i >= channels ? prev[i - channels] : 0;
                    int p = f switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, _ => Paeth(a, b, cc) };
                    o[i] = (byte)(cur[i] - p);
                    sum += o[i] < 128 ? o[i] : 256 - o[i];
                }
                if (sum < bestSum) { bestSum = sum; best = f; }
            }
            raw[y * (stride + 1)] = (byte)best;
            Buffer.BlockCopy(cand[best], 0, raw, y * (stride + 1) + 1, stride);
            (prev, cur) = (cur, prev);
        }
        byte[] z;
        using (var ms = new MemoryStream())
        {
            using (var zs = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) zs.Write(raw, 0, raw.Length);
            z = ms.ToArray();
        }
        using var o2 = new MemoryStream();
        o2.Write(Signature);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, w); BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
        ihdr[8] = 8; ihdr[9] = (byte)(channels == 4 ? 6 : 2); ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        Chunk(o2, "IHDR", ihdr);
        Chunk(o2, "IDAT", z);
        Chunk(o2, "IEND", Array.Empty<byte>());
        return o2.ToArray();
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(len, data.Length); s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t); s.Write(data);
        uint crc = Crc(t, 0xFFFFFFFFu); crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(c, crc); s.Write(c);
    }

    static readonly uint[] Table = MakeTable();
    static uint[] MakeTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[n] = c; }
        return t;
    }
    static uint Crc(byte[] b, uint c) { foreach (var x in b) c = Table[(c ^ x) & 0xFF] ^ (c >> 8); return c; }

    /// <summary>Decodes an 8-bit RGB or RGBA PNG to RGBA (alpha 255 for RGB).</summary>
    public static (int w, int h, byte[] rgba) Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("not a PNG");
        int p = 8, w = 0, h = 0, ch = 0;
        using var idat = new MemoryStream();
        while (p + 8 <= png.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));
            string type = System.Text.Encoding.ASCII.GetString(png, p + 4, 4);
            var data = png.AsSpan(p + 8, len);
            if (type == "IHDR")
            {
                w = BinaryPrimitives.ReadInt32BigEndian(data); h = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[12] != 0) throw new InvalidDataException("unsupported PNG (8-bit, non-interlaced only)");
                ch = data[9] switch { 6 => 4, 2 => 3, _ => throw new InvalidDataException("unsupported PNG colour type") };
            }
            else if (type == "IDAT") idat.Write(data);
            else if (type == "IEND") break;
            p += 12 + len;
        }
        idat.Position = 0;
        byte[] raw;
        using (var zs = new ZLibStream(idat, CompressionMode.Decompress))
        using (var ms = new MemoryStream()) { zs.CopyTo(ms); raw = ms.ToArray(); }
        int stride = w * ch;
        var outp = new byte[w * h * 4];
        var prev = new byte[stride]; var cur = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int f = raw[y * (stride + 1)];
            for (int i = 0; i < stride; i++)
            {
                int x = raw[y * (stride + 1) + 1 + i];
                int a = i >= ch ? cur[i - ch] : 0, b = prev[i], c = i >= ch ? prev[i - ch] : 0;
                int pr = f switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, 4 => Paeth(a, b, c), _ => throw new InvalidDataException("bad PNG filter") };
                cur[i] = (byte)(x + pr);
            }
            for (int xx = 0; xx < w; xx++)
            {
                int o = (y * w + xx) * 4;
                outp[o] = cur[xx * ch]; outp[o + 1] = cur[xx * ch + 1]; outp[o + 2] = cur[xx * ch + 2]; outp[o + 3] = ch == 4 ? cur[xx * ch + 3] : (byte)255;
            }
            (prev, cur) = (cur, prev);
        }
        return (w, h, outp);
    }
}
