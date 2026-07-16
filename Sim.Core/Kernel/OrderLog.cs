namespace Sim.Core.Kernel;

/// <summary>M0's order vocabulary: exactly one synthetic toy order (§3.9 pipe proof).</summary>
public enum OrderKind
{
    /// <summary>Adds a flat mm/year bias to one region's rainfall draw this turn
    /// (consumed by WeatherSystem — M0's only order).</summary>
    SetRainBias = 1,
}

/// <summary>
/// One external input to the sim (§3.9): {turn, actorId, payload}. Turn semantics:
/// an order with Turn = t is delivered to the step that transforms turn-t state
/// into turn-(t+1) state (i.e. delivered when Prev.Clock.Turn == t).
/// </summary>
public readonly record struct OrderRecord(long Turn, int ActorId, OrderKind Kind, int TargetId, double Amount);

/// <summary>
/// Append-only order log — the second half of determinism (§3.9) and the save
/// recovery path (D-008): replay(seed, orderLog) must reproduce the run
/// hash-for-hash. A separate artifact from snapshots, with its own IO.
/// </summary>
public sealed class OrderLog
{
    private readonly List<OrderRecord> _records = [];

    public int Count => _records.Count;

    public OrderRecord this[int index] => _records[index];

    /// <summary>Append-only: records may only be added, in nondecreasing turn order.</summary>
    public void Append(OrderRecord record)
    {
        if (_records.Count > 0 && record.Turn < _records[^1].Turn)
            throw new ArgumentException(
                $"order log is append-only in turn order: cannot append turn {record.Turn} " +
                $"after turn {_records[^1].Turn}.");
        _records.Add(record);
    }

    /// <summary>All orders addressed to the step executing from turn-<paramref name="turn"/> state.</summary>
    public OrderBatch BatchFor(long turn)
    {
        int count = 0;
        for (int i = 0; i < _records.Count; i++)
            if (_records[i].Turn == turn) count++;
        if (count == 0) return OrderBatch.Empty;

        var orders = new OrderRecord[count];
        int j = 0;
        for (int i = 0; i < _records.Count; i++)
            if (_records[i].Turn == turn) orders[j++] = _records[i];
        return new OrderBatch(orders);
    }

    // --- IO: separate artifact, own header, field-by-field like the schema -----

    public const int IoVersion = 1;
    private static ReadOnlySpan<byte> Magic => "CIVORDR\0"u8;

    public void Save(Stream destination)
    {
        using var writer = new BinaryWriter(destination, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(IoVersion);
        writer.Write(_records.Count);
        for (int i = 0; i < _records.Count; i++)
        {
            OrderRecord r = _records[i];
            writer.Write(r.Turn);
            writer.Write(r.ActorId);
            writer.Write((int)r.Kind);
            writer.Write(r.TargetId);
            writer.Write(BitConverter.DoubleToInt64Bits(r.Amount));
        }
    }

    public static OrderLog Load(Stream source)
    {
        using var reader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);

        Span<byte> magic = stackalloc byte[8];
        if (reader.Read(magic) != 8 || !magic.SequenceEqual(Magic))
            throw new SnapshotFormatException("not a civ-sim order log: bad magic (expected CIVORDR header).");

        int version = reader.ReadInt32();
        if (version != IoVersion)
            throw new SnapshotFormatException(
                $"order log is version {version}, this build reads only version {IoVersion}.");

        var log = new OrderLog();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            log.Append(new OrderRecord(
                reader.ReadInt64(), reader.ReadInt32(), (OrderKind)reader.ReadInt32(),
                reader.ReadInt32(), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }
        return log;
    }
}
