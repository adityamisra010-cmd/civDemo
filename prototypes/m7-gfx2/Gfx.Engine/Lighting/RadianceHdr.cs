using System.Text;

namespace Gfx.Engine.Lighting;

/// <summary>
/// Reader for Radiance RGBE <c>.hdr</c> files (the format of CC0 panoramas): header lines, an empty line, a
/// <c>-Y H +X W</c> resolution line, then scanlines that are either flat RGBE quads or new-style run-length encoded
/// (each scanline starts 2, 2, W/256, W%256, then four RLE channel planes). Decoding follows the published format;
/// RGBE → float uses f = 2^(e − 136) (as stb_image does). Original implementation.
/// </summary>
public static class RadianceHdr
{
    public static EquirectImage Load(string path) => Decode(File.ReadAllBytes(path));

    public static EquirectImage Decode(byte[] data)
    {
        int pos = 0;
        string Line()
        {
            int start = pos;
            while (pos < data.Length && data[pos] != (byte)'\n') pos++;
            string s = Encoding.ASCII.GetString(data, start, pos - start);
            pos++;
            return s;
        }
        string first = Line();
        if (!first.StartsWith("#?", StringComparison.Ordinal)) throw new InvalidDataException("not a Radiance file");
        bool rgbe = false;
        while (true)
        {
            if (pos >= data.Length) throw new InvalidDataException("unterminated header");
            string l = Line();
            if (l.Length == 0) break;
            if (l.StartsWith("FORMAT=", StringComparison.Ordinal)) rgbe = l == "FORMAT=32-bit_rle_rgbe";
        }
        if (!rgbe) throw new InvalidDataException("only FORMAT=32-bit_rle_rgbe is supported");
        var res = Line().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (res.Length != 4 || res[0] != "-Y" || res[2] != "+X") throw new InvalidDataException("only -Y H +X W orientation is supported");
        int h = int.Parse(res[1], System.Globalization.CultureInfo.InvariantCulture);
        int w = int.Parse(res[3], System.Globalization.CultureInfo.InvariantCulture);
        var img = new EquirectImage(w, h);
        var line = new byte[w * 4];
        for (int y = 0; y < h; y++)
        {
            if (w >= 8 && w < 32768 && pos + 4 <= data.Length && data[pos] == 2 && data[pos + 1] == 2 && (data[pos + 2] & 0x80) == 0)
            {
                if (((data[pos + 2] << 8) | data[pos + 3]) != w) throw new InvalidDataException("scanline width mismatch");
                pos += 4;
                for (int c = 0; c < 4; c++)
                {
                    int x = 0;
                    while (x < w)
                    {
                        int count = data[pos++];
                        if (count > 128)
                        {
                            count -= 128; byte v = data[pos++];
                            if (x + count > w) throw new InvalidDataException("bad run");
                            for (int i = 0; i < count; i++) line[(x++) * 4 + c] = v;
                        }
                        else
                        {
                            if (count == 0 || x + count > w) throw new InvalidDataException("bad literal");
                            for (int i = 0; i < count; i++) line[(x++) * 4 + c] = data[pos++];
                        }
                    }
                }
            }
            else
            {
                if (pos + w * 4 > data.Length) throw new InvalidDataException("truncated flat scanline");
                Buffer.BlockCopy(data, pos, line, 0, w * 4); pos += w * 4;
            }
            for (int x = 0; x < w; x++)
            {
                int e = line[x * 4 + 3]; int o = (y * w + x) * 3;
                if (e == 0) { img.Rgb[o] = img.Rgb[o + 1] = img.Rgb[o + 2] = 0; continue; }
                float f = MathF.ScaleB(1f, e - 136);
                img.Rgb[o] = line[x * 4] * f; img.Rgb[o + 1] = line[x * 4 + 1] * f; img.Rgb[o + 2] = line[x * 4 + 2] * f;
            }
        }
        return img;
    }

    /// <summary>Encodes flat (uncompressed) RGBE for tests and tools.</summary>
    public static byte[] EncodeFlat(EquirectImage img)
    {
        var ms = new MemoryStream();
        var head = Encoding.ASCII.GetBytes($"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {img.Height} +X {img.Width}\n");
        ms.Write(head);
        for (int i = 0; i < img.Width * img.Height; i++)
        {
            float r = img.Rgb[3 * i], g = img.Rgb[3 * i + 1], b = img.Rgb[3 * i + 2];
            float m = MathF.Max(r, MathF.Max(g, b));
            if (m < 1e-32f) { ms.Write(new byte[4]); continue; }
            int e = (int)MathF.Floor(MathF.Log2(m)) + 1;
            float s = MathF.ScaleB(1f, 8 - e);
            ms.Write(new[] { (byte)Math.Min(255, (int)(r * s)), (byte)Math.Min(255, (int)(g * s)), (byte)Math.Min(255, (int)(b * s)), (byte)(e + 128) });
        }
        return ms.ToArray();
    }
}
