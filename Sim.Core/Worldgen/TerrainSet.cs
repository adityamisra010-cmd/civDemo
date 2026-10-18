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

    /// <summary>
    /// Batch 3 WS3 — THE WORLD'S EAST-WEST TOPOLOGY: true when the world is a CYLINDER (x periodic with period
    /// <see cref="Size"/>; the poles, y = 0 and y = Size − 1, keep their hard edges), false for the flat square raster.
    /// Set by <see cref="Worldgen.Generate"/> from worldgen.json `topology.wrapX` — the ONLY reader of that config field;
    /// every other consumer (pathing, distances, siting, the UI) reads THIS property, through
    /// <see cref="Pathing.MapTopology"/>.
    ///
    /// NOT folded into <see cref="ContentHash"/>: the hash covers the rasters, and a wrapped world's rasters already differ
    /// from a flat one's at every seed (the y-only taper, the periodic noise, the wrapped hydrology). A snapshot binds the
    /// flag separately (the v33 topology trailer, wired at integration), so a save can never be loaded onto the other
    /// topology over equal rasters.
    /// </summary>
    public bool WrapX { get; }

    private readonly double[] _elevation;
    private readonly double[] _water;        // 1.0 = water, 0.0 = land
    private readonly double[] _temperature;  // °C
    private readonly double[] _moisture;     // [0,1]
    private readonly double[] _fertility;    // [0,1], 0 on water
    private readonly double[] _movementCost; // relative cost per px
    private readonly double[] _rivers;       // 1.0 on river cells (T1.2), else 0.0
    private readonly int[][] _riverPolylines; // head→mouth cell indices, discharge-ranked

    /// <summary>SHA-256 over the canonical layer serialization; computed once at construction.</summary>
    public byte[] ContentHash { get; }

    public ReadOnlySpan<double> Elevation => _elevation;
    public ReadOnlySpan<double> Water => _water;
    public ReadOnlySpan<double> Temperature => _temperature;
    public ReadOnlySpan<double> Moisture => _moisture;
    public ReadOnlySpan<double> Fertility => _fertility;
    public ReadOnlySpan<double> MovementCost => _movementCost;
    public ReadOnlySpan<double> Rivers => _rivers;

    public int RiverPolylineCount => _riverPolylines.Length;
    public ReadOnlySpan<int> RiverPolyline(int index) => _riverPolylines[index];

    public int Index(int x, int y) => y * Size + x;
    public bool IsWater(int x, int y) => _water[Index(x, y)] >= 0.5;

    internal TerrainSet(
        int size, double kmPerPx, double seaLevel,
        double[] elevation, double[] water, double[] temperature,
        double[] moisture, double[] fertility, double[] movementCost,
        double[] rivers, int[][] riverPolylines, bool wrapX = false)
    {
        Pathing.MapTopology.RequireWrapSize(size, wrapX);
        Size = size;
        KmPerPx = kmPerPx;
        SeaLevel = seaLevel;
        WrapX = wrapX;
        _elevation = elevation;
        _water = water;
        _temperature = temperature;
        _moisture = moisture;
        _fertility = fertility;
        _movementCost = movementCost;
        _rivers = rivers;
        _riverPolylines = riverPolylines;
        ContentHash = ComputeContentHash();
    }

    /// <summary>
    /// Batch 3 WS3 — THE TEST AND TOOL FACTORY (recorded under ADR-008): a TerrainSet from explicit layers, for rigs that
    /// need terrain worldgen cannot produce — above all a land strip that crosses the east-west seam of a wrapped world
    /// (the SeamWorld fixture), which no seed is guaranteed to generate. The game never calls it: every played world comes
    /// from <see cref="Worldgen.Generate"/>. Each layer must be Size² long and is COPIED (the set stays immutable); every
    /// polyline cell must be on the map. The content hash is computed exactly as for a generated set.
    /// </summary>
    public static TerrainSet FromLayers(
        int size, double kmPerPx, double seaLevel,
        ReadOnlySpan<double> elevation, ReadOnlySpan<double> water, ReadOnlySpan<double> temperature,
        ReadOnlySpan<double> moisture, ReadOnlySpan<double> fertility, ReadOnlySpan<double> movementCost,
        ReadOnlySpan<double> rivers, IReadOnlyList<int[]> riverPolylines, bool wrapX)
    {
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), size, "a terrain needs at least one cell.");
        if (!(kmPerPx > 0.0) || !double.IsFinite(kmPerPx))
            throw new ArgumentOutOfRangeException(nameof(kmPerPx), kmPerPx, "km per pixel must be a finite distance > 0.");
        int cells = size * size;
        double[] Copy(ReadOnlySpan<double> layer, string name)
        {
            if (layer.Length != cells)
                throw new ArgumentException(name + " must be size*size long", name);
            return layer.ToArray();
        }
        ArgumentNullException.ThrowIfNull(riverPolylines);
        var lines = new int[riverPolylines.Count][];
        for (int p = 0; p < lines.Length; p++)
        {
            int[] line = riverPolylines[p] ?? throw new ArgumentException("a river polyline is null", nameof(riverPolylines));
            for (int i = 0; i < line.Length; i++)
                if ((uint)line[i] >= (uint)cells)
                    throw new ArgumentException("river polyline " + p.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " leaves the map", nameof(riverPolylines));
            lines[p] = [.. line];
        }
        return new TerrainSet(size, kmPerPx, seaLevel,
            Copy(elevation, nameof(elevation)), Copy(water, nameof(water)), Copy(temperature, nameof(temperature)),
            Copy(moisture, nameof(moisture)), Copy(fertility, nameof(fertility)), Copy(movementCost, nameof(movementCost)),
            Copy(rivers, nameof(rivers)), lines, wrapX);
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
        AppendLayer(sha, _rivers, buf);

        // River polylines: count, then per line its length and cell indices —
        // field by field, same discipline as the raster layers.
        BitConverter.TryWriteBytes(buf[..4], _riverPolylines.Length);
        sha.AppendData(buf[..4]);
        for (int p = 0; p < _riverPolylines.Length; p++)
        {
            int[] line = _riverPolylines[p];
            BitConverter.TryWriteBytes(buf[..4], line.Length);
            sha.AppendData(buf[..4]);
            for (int i = 0; i < line.Length; i++)
            {
                BitConverter.TryWriteBytes(buf[..4], line[i]);
                sha.AppendData(buf[..4]);
            }
        }

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
