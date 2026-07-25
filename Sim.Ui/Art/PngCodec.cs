using System.Buffers.Binary;
using System.IO.Compression;

namespace Sim.Ui.Art;

/// <summary>One decoded image: tightly-packed RGBA8, row-major.</summary>
public sealed record ArtImage(int Width, int Height, byte[] Rgba)
{
    public ParchmentPalette.Rgba At(int x, int y)
    {
        int o = (y * Width + x) * 4;
        return new ParchmentPalette.Rgba(Rgba[o], Rgba[o + 1], Rgba[o + 2], Rgba[o + 3]);
    }

    /// <summary>Bilinear sample at WRAPPED uv (the tiling path) — continuous
    /// across the seam by construction, which is what makes a seamless tile
    /// actually read as seamless at every zoom.</summary>
    public ParchmentPalette.Rgba SampleWrapped(double u, double v)
    {
        double fx = u * Width - 0.5, fy = v * Height - 0.5;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        double tx = fx - x0, ty = fy - y0;
        int x1 = Wrap(x0 + 1, Width), y1 = Wrap(y0 + 1, Height);
        x0 = Wrap(x0, Width); y0 = Wrap(y0, Height);

        ParchmentPalette.Rgba c00 = At(x0, y0), c10 = At(x1, y0);
        ParchmentPalette.Rgba c01 = At(x0, y1), c11 = At(x1, y1);
        double r = Lerp(Lerp(c00.R, c10.R, tx), Lerp(c01.R, c11.R, tx), ty);
        double g = Lerp(Lerp(c00.G, c10.G, tx), Lerp(c01.G, c11.G, tx), ty);
        double b = Lerp(Lerp(c00.B, c10.B, tx), Lerp(c01.B, c11.B, tx), ty);
        return new ParchmentPalette.Rgba(TerrainSplat.Byte(r), TerrainSplat.Byte(g), TerrainSplat.Byte(b));
    }

    /// <summary>
    /// SEAM-HIDDEN wrapped sample (the art-drop cross-fade). Real generated
    /// art is only approximately edge-wrapping — a diffusion model has no way
    /// to guarantee the left column continues into the right — so a faint
    /// residual discontinuity survives even in good tiles (measured on the
    /// director's drop: the join is ~1.4–1.7× rougher than the tile's own
    /// interior). This sampler removes it at the SAMPLING stage rather than
    /// asking for a better tile: it takes a second tap half a tile away, where
    /// the seam region maps to tile INTERIOR, and cross-fades to it as the
    /// primary tap approaches an edge. At the seam the second tap supplies
    /// 100% of the colour, so the discontinuity cannot appear in the output;
    /// two tiles away from any edge the primary tap is used untouched, so the
    /// artist's texture is preserved everywhere it is safe to use it.
    /// </summary>
    /// <param name="edgeBand">Fraction of the tile over which the cross-fade
    /// runs (0.12 = the outer 12% of each side).</param>
    public ParchmentPalette.Rgba SampleWrappedCrossFaded(double u, double v, double edgeBand = 0.12)
    {
        double fu = u - Math.Floor(u), fv = v - Math.Floor(v);
        double edgeDistance = Math.Min(Math.Min(fu, 1.0 - fu), Math.Min(fv, 1.0 - fv));
        if (edgeDistance >= edgeBand) return SampleWrapped(u, v);

        // Smoothstep from "all second tap" at the seam to "all primary tap"
        // at the band's inner edge — C1 continuous, so the cross-fade itself
        // introduces no new edge.
        double t = edgeDistance / edgeBand;
        double w = t * t * (3.0 - 2.0 * t);
        ParchmentPalette.Rgba primary = SampleWrapped(u, v);
        ParchmentPalette.Rgba shifted = SampleWrapped(u + 0.5, v + 0.5);
        return new ParchmentPalette.Rgba(
            TerrainSplat.Byte(shifted.R + (primary.R - shifted.R) * w),
            TerrainSplat.Byte(shifted.G + (primary.G - shifted.G) * w),
            TerrainSplat.Byte(shifted.B + (primary.B - shifted.B) * w));
    }

    private static int Wrap(int i, int n) { i %= n; return i < 0 ? i + n : i; }
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

/// <summary>
/// A minimal, dependency-free PNG reader/writer (D-003: no new packages) for
/// 8-bit RGBA, non-interlaced — enough for every asset in style-bible §4 and
/// for the headless tests that verify them. Deliberately strict: anything it
/// cannot read throws with an actionable message rather than guessing, and the
/// AssetLibrary turns that into a labeled placeholder instead of a crash.
/// </summary>
public static class PngCodec
{
    private static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Write(string path, ArtImage image)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        file.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], image.Height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // color type: RGBA
        ihdr[10] = 0;  // deflate
        ihdr[11] = 0;  // adaptive filtering
        ihdr[12] = 0;  // no interlace
        WriteChunk(file, "IHDR", ihdr);

        // Filter type 0 (None) per scanline: the assets are noise-textured, so
        // fancy filters buy little and cost determinism clarity.
        var raw = new byte[image.Height * (1 + image.Width * 4)];
        for (int y = 0; y < image.Height; y++)
        {
            int dst = y * (1 + image.Width * 4);
            raw[dst] = 0;
            Array.Copy(image.Rgba, y * image.Width * 4, raw, dst + 1, image.Width * 4);
        }
        using var deflated = new MemoryStream();
        using (var z = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        WriteChunk(file, "IDAT", deflated.ToArray());
        WriteChunk(file, "IEND", []);
    }

    public static ArtImage Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException($"'{path}' is not a PNG (bad signature).");

        int width = 0, height = 0, colorType = -1, bitDepth = 0;
        using var idat = new MemoryStream();
        int pos = 8;
        while (pos + 8 <= bytes.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(pos));
            string type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
            int dataAt = pos + 8;
            if (length < 0 || dataAt + length + 4 > bytes.Length)
                throw new InvalidDataException($"'{path}' has a truncated '{type}' chunk.");

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataAt));
                    height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataAt + 4));
                    bitDepth = bytes[dataAt + 8];
                    colorType = bytes[dataAt + 9];
                    if (bytes[dataAt + 12] != 0)
                        throw new InvalidDataException($"'{path}' is interlaced — not supported.");
                    break;
                case "IDAT":
                    idat.Write(bytes, dataAt, length);
                    break;
            }
            pos = dataAt + length + 4;
            if (type == "IEND") break;
        }

        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"'{path}' has no valid IHDR.");
        if (bitDepth != 8 || (colorType != 6 && colorType != 2))
            throw new InvalidDataException(
                $"'{path}': only 8-bit RGB/RGBA PNGs are supported (depth {bitDepth}, color type {colorType}).");

        int channels = colorType == 6 ? 4 : 3;
        int stride = width * channels;
        idat.Position = 0;
        var raw = new byte[height * (1 + stride)];
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = z.Read(raw, read, raw.Length - read);
                if (n <= 0) throw new InvalidDataException($"'{path}': IDAT ended early.");
                read += n;
            }
        }

        // Un-filter (PNG spec §9) into tight rows, then widen RGB → RGBA.
        var rows = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            int src = y * (1 + stride);
            byte filter = raw[src];
            int dst = y * stride, up = dst - stride;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= channels ? rows[dst + i - channels] : 0;
                int b = y > 0 ? rows[up + i] : 0;
                int c = y > 0 && i >= channels ? rows[up + i - channels] : 0;
                int x = raw[src + 1 + i];
                rows[dst + i] = (byte)(filter switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + ((a + b) >> 1),
                    4 => x + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"'{path}': unknown filter {filter}."),
                });
            }
        }

        var rgba = new byte[width * height * 4];
        for (int i = 0, n = width * height; i < n; i++)
        {
            rgba[i * 4] = rows[i * channels];
            rgba[i * 4 + 1] = rows[i * channels + 1];
            rgba[i * 4 + 2] = rows[i * channels + 2];
            rgba[i * 4 + 3] = channels == 4 ? rows[i * channels + 3] : (byte)255;
        }
        return new ArtImage(width, height, rgba);
    }

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    private static void WriteChunk(Stream to, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        for (int i = 0; i < 4; i++) header[4 + i] = (byte)type[i];
        to.Write(header);
        to.Write(data);

        uint crc = Crc32(header[4..8], data);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tail, crc);
        to.Write(tail);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte x in a) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        foreach (byte x in b) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
