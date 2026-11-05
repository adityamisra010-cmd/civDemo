using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.Events;

public enum TraceOutcome : byte
{
    Executed = 1,
    /// <summary>Target or source entity no longer exists with the referenced generation (invalidation).</summary>
    Dropped = 2,
    /// <summary>Cancelled by event id (Seq) before it came due.</summary>
    Cancelled = 3,
}

public readonly record struct TraceEntry(EventKey Key, int Kind, TraceOutcome Outcome, EntityRef Target, EntityRef Source, long A, long B, long C);

/// <summary>
/// Append-only record of every event the scheduler popped (executed, dropped or cancelled), in execution order.
/// TraceHash = SHA-256 over a canonical little-endian encoding (spec §2.3).
/// The CANONICAL ENCODING EXCLUDES Key.Seq: Seq is a scheduler-internal allocation counter whose absolute values
/// legitimately differ between a live run (orders scheduled when issued) and its replay (orders pre-scheduled),
/// while the execution ORDER — which the trace records by position — is identical (proof in Runner docs and
/// ReplayTests). Everything else in the key, plus kind, outcome, refs and payload, is hashed.
/// </summary>
public sealed class EventTrace
{
    public const int EntryBytes = 8 + 1 + 4 + 8 + 4 + 1 + (8 + 4) * 2 + 8 * 3; // = 74
    private const byte EncodingVersion = 1;
    private readonly List<TraceEntry> _entries = new();

    public IReadOnlyList<TraceEntry> Entries => _entries;
    public int Count => _entries.Count;
    public bool Enabled { get; set; } = true;

    public void Append(in TraceEntry e) { if (Enabled) _entries.Add(e); }

    public static void Encode(in TraceEntry e, Span<byte> dst)
    {
        int o = 0;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.Key.TimeMs); o += 8;
        dst[o] = e.Key.Class; o += 1;
        BinaryPrimitives.WriteInt32LittleEndian(dst[o..], e.Key.Priority); o += 4;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.Key.SourceId); o += 8;
        BinaryPrimitives.WriteInt32LittleEndian(dst[o..], e.Kind); o += 4;
        dst[o] = (byte)e.Outcome; o += 1;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.Target.Id); o += 8;
        BinaryPrimitives.WriteInt32LittleEndian(dst[o..], e.Target.Generation); o += 4;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.Source.Id); o += 8;
        BinaryPrimitives.WriteInt32LittleEndian(dst[o..], e.Source.Generation); o += 4;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.A); o += 8;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.B); o += 8;
        BinaryPrimitives.WriteInt64LittleEndian(dst[o..], e.C); o += 8;
        if (o != EntryBytes) throw new InvalidOperationException("trace encoding length drift");
    }

    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> head = stackalloc byte[1 + 8];
        head[0] = EncodingVersion;
        BinaryPrimitives.WriteInt64LittleEndian(head[1..], _entries.Count);
        h.AppendData(head);
        Span<byte> buf = stackalloc byte[EntryBytes];
        foreach (var e in _entries)
        {
            Encode(e, buf);
            h.AppendData(buf);
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
