using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Gfx.Proc.Authoritative;

/// <summary>
/// AUTHORITATIVE battle terrain (integer only). Row-major W×H cells of <see cref="CellMm"/> mm; per cell: terrain
/// height (mm, relative to the battle datum), water depth (mm, 0 = dry; surface = height + depth) and class.
/// Canonical serialization (little-endian) is the hash input and the save format:
/// <code>
/// "BTG1" | i32 version | i32 W | i32 H | i32 CellMm | i64 DatumMm | i32 WaterLevelMm | u8 Biome | u64 Seed
///        | i32[W*H] HeightMm | i32[W*H] WaterDepthMm | u8[W*H] Class
/// </code>
/// The renderer only READS this object (renderer-invariance test: hash before/after rendering).
/// </summary>
public sealed class BattleTerrainGrid
{
    public const int FormatVersion = 1;
    public const int HeaderBytes = 4 + 4 + 4 + 4 + 4 + 8 + 4 + 1 + 8;

    public int Width { get; }
    public int Height { get; }
    public int CellMm { get; }
    /// <summary>Absolute height of the local datum (campaign cell elevation × 1000).</summary>
    public long DatumMm { get; }
    /// <summary>Sea level relative to the datum (meaningful when the map is coastal).</summary>
    public int WaterLevelMm { get; }
    public Biome Biome { get; }
    public ulong Seed { get; }
    public int[] HeightMm { get; }
    public int[] WaterDepthMm { get; }
    public TerrainClass[] Class { get; }

    public BattleTerrainGrid(int width, int height, int cellMm, long datumMm, int waterLevelMm, Biome biome, ulong seed)
    {
        Width = width; Height = height; CellMm = cellMm; DatumMm = datumMm; WaterLevelMm = waterLevelMm; Biome = biome; Seed = seed;
        HeightMm = new int[width * height]; WaterDepthMm = new int[width * height]; Class = new TerrainClass[width * height];
    }

    public int Index(int x, int y) => y * Width + x;
    public int ClampedHeight(int x, int y) => HeightMm[Index(Math.Clamp(x, 0, Width - 1), Math.Clamp(y, 0, Height - 1))];
    public TerrainClass ClampedClass(int x, int y) => Class[Index(Math.Clamp(x, 0, Width - 1), Math.Clamp(y, 0, Height - 1))];

    public int ExpectedLength => HeaderBytes + Width * Height * (4 + 4 + 1);

    public byte[] ToCanonicalBytes()
    {
        var b = new byte[ExpectedLength]; var s = b.AsSpan();
        "BTG1"u8.CopyTo(s); int o = 4;
        BinaryPrimitives.WriteInt32LittleEndian(s[o..], FormatVersion); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(s[o..], Width); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(s[o..], Height); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(s[o..], CellMm); o += 4;
        BinaryPrimitives.WriteInt64LittleEndian(s[o..], DatumMm); o += 8;
        BinaryPrimitives.WriteInt32LittleEndian(s[o..], WaterLevelMm); o += 4;
        s[o++] = (byte)Biome;
        BinaryPrimitives.WriteUInt64LittleEndian(s[o..], Seed); o += 8;
        foreach (int h in HeightMm) { BinaryPrimitives.WriteInt32LittleEndian(s[o..], h); o += 4; }
        foreach (int d in WaterDepthMm) { BinaryPrimitives.WriteInt32LittleEndian(s[o..], d); o += 4; }
        foreach (var c in Class) s[o++] = (byte)c;
        if (o != b.Length) throw new InvalidOperationException("canonical length mismatch");
        return b;
    }

    public static BattleTerrainGrid FromCanonicalBytes(ReadOnlySpan<byte> s)
    {
        if (s.Length < HeaderBytes || !s[..4].SequenceEqual("BTG1"u8)) throw new FormatException("not a BTG1 grid");
        int o = 4;
        int ver = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4;
        if (ver != FormatVersion) throw new FormatException($"unsupported BTG version {ver}");
        int w = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4;
        int h = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4;
        int cell = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4;
        long datum = BinaryPrimitives.ReadInt64LittleEndian(s[o..]); o += 8;
        int wl = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4;
        var biome = (Biome)s[o++];
        ulong seed = BinaryPrimitives.ReadUInt64LittleEndian(s[o..]); o += 8;
        var g = new BattleTerrainGrid(w, h, cell, datum, wl, biome, seed);
        if (s.Length != g.ExpectedLength) throw new FormatException($"length {s.Length} != expected {g.ExpectedLength}");
        for (int i = 0; i < w * h; i++) { g.HeightMm[i] = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4; }
        for (int i = 0; i < w * h; i++) { g.WaterDepthMm[i] = BinaryPrimitives.ReadInt32LittleEndian(s[o..]); o += 4; }
        for (int i = 0; i < w * h; i++) g.Class[i] = (TerrainClass)s[o++];
        return g;
    }

    public byte[] Hash() => SHA256.HashData(ToCanonicalBytes());
    public string HashHex() => Convert.ToHexStringLower(Hash());

    /// <summary>Cell counts per class (index = class byte), for reports and tests.</summary>
    public int[] ClassHistogram()
    {
        var n = new int[16];
        foreach (var c in Class) n[(int)c]++;
        return n;
    }
}
