using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.Events;

/// <summary>
/// A player order issued while the runner is stopped at an interrupt instant (spec §2.4). Prototype-local;
/// NOT the kernel OrderRecord (changing OrderRecord needs authorization — recon-kernel §7 item 4).
/// Seq = index in the log; it is also the order event's SourceId, so orders at one instant execute in issue order.
/// </summary>
public readonly record struct SubTurnOrder(long InstantMs, int Seq, int Kind, long A, long B);

public sealed class SubTurnOrderLog
{
    private readonly List<SubTurnOrder> _orders = new();

    public IReadOnlyList<SubTurnOrder> Orders => _orders;
    public int Count => _orders.Count;

    /// <summary>Append; instants must be non-decreasing (orders are issued as the timeline advances).</summary>
    public SubTurnOrder Append(long instantMs, int kind, long a, long b)
    {
        if (_orders.Count > 0 && instantMs < _orders[^1].InstantMs)
            throw new InvalidOperationException("order instants must be non-decreasing");
        var o = new SubTurnOrder(instantMs, _orders.Count, kind, a, b);
        _orders.Add(o);
        return o;
    }

    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buf = stackalloc byte[8 + 4 + 4 + 8 + 8];
        foreach (var o in _orders)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buf, o.InstantMs);
            BinaryPrimitives.WriteInt32LittleEndian(buf[8..], o.Seq);
            BinaryPrimitives.WriteInt32LittleEndian(buf[12..], o.Kind);
            BinaryPrimitives.WriteInt64LittleEndian(buf[16..], o.A);
            BinaryPrimitives.WriteInt64LittleEndian(buf[24..], o.B);
            h.AppendData(buf);
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
