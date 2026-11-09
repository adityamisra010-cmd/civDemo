using System.Buffers.Binary;

namespace Proto.M7.Support;

/// <summary>
/// [D] Canonical little-endian fixed-length encoding of the support rows (CLAUDE.md: every new serialized row type ships a
/// POPULATED-table test with exact ExpectedLength, bit-exact round trip and hash equality). Table = [version u8][count i32] rows.
/// </summary>
public static class SupportCodec
{
    public const byte Version = 1;
    public const int HeaderBytes = 1 + 4;
    public const int OrderRowBytes = 4 + 8 + 4 + 4 + 1 + 8 + 1;                        // 30
    public const int AllocationRowBytes = 4 + 4 + 8 + 8 + 1 + 8 + 8 + 8 + 8;           // 57
    public const int CareRowBytes = 4 + 8 + 8 + 8 + 8 + 8 + 8;                         // 52
    public const int BattleRowBytes = 4 + 4 + 8 + 8 * 10;                              // 96

    public static int ExpectedLength(int rows, int rowBytes) => checked(HeaderBytes + rows * rowBytes);

    private sealed class W
    {
        private readonly byte[] _b;
        private int _at;
        public W(int length, int count) { _b = new byte[length]; _b[0] = Version; BinaryPrimitives.WriteInt32LittleEndian(_b.AsSpan(1), count); _at = HeaderBytes; }
        public void U8(byte v) => _b[_at++] = v;
        public void I32(int v) { BinaryPrimitives.WriteInt32LittleEndian(_b.AsSpan(_at), v); _at += 4; }
        public void I64(long v) { BinaryPrimitives.WriteInt64LittleEndian(_b.AsSpan(_at), v); _at += 8; }
        public byte[] Done() { if (_at != _b.Length) throw new InvalidOperationException("encoded length mismatch"); return _b; }
    }

    private sealed class R
    {
        private readonly byte[] _b;
        private int _at;
        public R(byte[] b, int rowBytes, out int count)
        {
            if (b.Length < HeaderBytes || b[0] != Version) throw new FormatException("support table header");
            count = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(1));
            if (count < 0 || b.Length != ExpectedLength(count, rowBytes)) throw new FormatException("support table length");
            _b = b; _at = HeaderBytes;
        }
        public byte U8() => _b[_at++];
        public int I32() { int v = BinaryPrimitives.ReadInt32LittleEndian(_b.AsSpan(_at)); _at += 4; return v; }
        public long I64() { long v = BinaryPrimitives.ReadInt64LittleEndian(_b.AsSpan(_at)); _at += 8; return v; }
    }

    public static byte[] EncodeOrders(IReadOnlyList<SupportOrderRow> rows)
    {
        var w = new W(ExpectedLength(rows.Count, OrderRowBytes), rows.Count);
        foreach (var r in rows) { w.I32(r.Index); w.I64(r.AtMs); w.I32(r.Polity); w.I32(r.ArmyId); w.U8(r.Kind); w.I64(r.Target); w.U8(r.Refusal); }
        return w.Done();
    }

    public static SupportOrderRow[] DecodeOrders(byte[] b)
    {
        var r = new R(b, OrderRowBytes, out int n);
        var a = new SupportOrderRow[n];
        for (int i = 0; i < n; i++) a[i] = new SupportOrderRow(r.I32(), r.I64(), r.I32(), r.I32(), r.U8(), r.I64(), r.U8());
        return a;
    }

    public static byte[] EncodeAllocations(IReadOnlyList<SupportAllocationRow> rows)
    {
        var w = new W(ExpectedLength(rows.Count, AllocationRowBytes), rows.Count);
        foreach (var r in rows)
        {
            w.I32(r.ArmyId); w.I32(r.WarId); w.I64(r.PulseIndex); w.I64(r.AtMs); w.U8(r.Kind);
            w.I64(r.Requested); w.I64(r.Limit); w.I64(r.Granted); w.I64(r.PoolAfter);
        }
        return w.Done();
    }

    public static SupportAllocationRow[] DecodeAllocations(byte[] b)
    {
        var r = new R(b, AllocationRowBytes, out int n);
        var a = new SupportAllocationRow[n];
        for (int i = 0; i < n; i++) a[i] = new SupportAllocationRow(r.I32(), r.I32(), r.I64(), r.I64(), r.U8(), r.I64(), r.I64(), r.I64(), r.I64());
        return a;
    }

    public static byte[] EncodeCare(IReadOnlyList<SupportCareRow> rows)
    {
        var w = new W(ExpectedLength(rows.Count, CareRowBytes), rows.Count);
        foreach (var r in rows) { w.I32(r.ArmyId); w.I64(r.FromMs); w.I64(r.ToMs); w.I64(r.Coverage); w.I64(r.WoundedBefore); w.I64(r.Recovered); w.I64(r.Died); }
        return w.Done();
    }

    public static SupportCareRow[] DecodeCare(byte[] b)
    {
        var r = new R(b, CareRowBytes, out int n);
        var a = new SupportCareRow[n];
        for (int i = 0; i < n; i++) a[i] = new SupportCareRow(r.I32(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64());
        return a;
    }

    public static byte[] EncodeBattles(IReadOnlyList<SupportBattleRow> rows)
    {
        var w = new W(ExpectedLength(rows.Count, BattleRowBytes), rows.Count);
        foreach (var r in rows)
        {
            w.I32(r.Seq); w.I32(r.ArmyId); w.I64(r.AtMs); w.I64(r.Persons); w.I64(r.DeadBefore); w.I64(r.Saved); w.I64(r.Dead);
            w.I64(r.Wounded); w.I64(r.Captured); w.I64(r.Deserted); w.I64(r.Coverage); w.I64(r.MoraleBefore); w.I64(r.MoraleAfter);
        }
        return w.Done();
    }

    public static SupportBattleRow[] DecodeBattles(byte[] b)
    {
        var r = new R(b, BattleRowBytes, out int n);
        var a = new SupportBattleRow[n];
        for (int i = 0; i < n; i++)
            a[i] = new SupportBattleRow(r.I32(), r.I32(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64(), r.I64());
        return a;
    }
}
