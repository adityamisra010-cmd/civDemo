using System.Security.Cryptography;

namespace Sim.Core.Worldgen;

/// <summary>
/// The immutable terrain rasters (D-015/D-022/D-024, ADR-008): six double layers
/// at Size×Size, row-major, fixed after generation. Referenced by WorldState but
/// EXCLUDED from the per-turn Clone (immutable data needs no double buffer); the
/// content hash is computed exactly once here and folded into every WorldHash by
/// the canonical schema, so worlds on different terrain can never hash equal.
/// Terrain mutation (late-era mechanics) would reverse this — see ADR-008's
/// upgrade path — and is out of scope until its milestone.
/// </summary>
public sealed class TerrainSet
{
    public int Size { get; }
    public double KmPerPx { get; }
    public double SeaLevel { get; }

    private readonly double[] _elevation;
    private readonly double[] _water;        // 1.0 = water, 0.0 = land
    private readonly double[] _temperature;  // °C
    private readonly double[] _moisture;     // [0,1]
    private readonly double[] _fertility;    // [0,1], 0 on water
    private readonly double[] _movementCost; // relative cost per px

    /// <summary>SHA-256 over the canonical layer serialization; computed once at construction.</summary>
    public byte[] ContentHash { get; }

    public ReadOnlySpan<double> Elevation => _elevation;
    public ReadOnlySpan<double> Water => _water;
    public ReadOnlySpan<double> Temperature => _temperature;
    public ReadOnlySpan<double> Moisture => _moisture;
    public ReadOnlySpan<double> Fertility => _fertility;
    public ReadOnlySpan<double> MovementCost => _movementCost;

    public int Index(int x, int y) => y * Size + x;
    public bool IsWater(int x, int y) => _water[Index(x, y)] >= 0.5;

    internal TerrainSet(
        int size, double kmPerPx, double seaLevel,
        double[] elevation, double[] water, double[] temperature,
        double[] moisture, double[] fertility, double[] movementCost)
    {
        Size = size;
        KmPerPx = kmPerPx;
        SeaLevel = seaLevel;
        _elevation = elevation;
        _water = water;
        _temperature = temperature;
        _moisture = moisture;
        _fertility = fertility;
        _movementCost = movementCost;
        ContentHash = ComputeContentHash();
    }

    // Canonical, field-by-field per ADR-005 discipline: header values then each
    // layer's raw IEEE-754 bits in fixed declaration order. No raw struct memory.
    private byte[] ComputeContentHash()
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buf = stackalloc byte[8];

        BitConverter.TryWriteBytes(buf[..4], Size);
        sha.AppendData(buf[..4]);
        BitConverter.TryWriteBytes(buf, BitConverter.DoubleToInt64Bits(KmPerPx));
        sha.AppendData(buf);
        BitConverter.TryWriteBytes(buf, BitConverter.DoubleToInt64Bits(SeaLevel));
        sha.AppendData(buf);

        AppendLayer(sha, _elevation, buf);
        AppendLayer(sha, _water, buf);
        AppendLayer(sha, _temperature, buf);
        AppendLayer(sha, _moisture, buf);
        AppendLayer(sha, _fertility, buf);
        AppendLayer(sha, _movementCost, buf);

        return sha.GetHashAndReset();
    }

    private static void AppendLayer(IncrementalHash sha, double[] layer, Span<byte> buf)
    {
        for (int i = 0; i < layer.Length; i++)
        {
            BitConverter.TryWriteBytes(buf, BitConverter.DoubleToInt64Bits(layer[i]));
            sha.AppendData(buf);
        }
    }
}
