using System.Buffers.Binary;

namespace Proto.M7.Morale;

/// <summary>
/// [MOR] One army's campaign-morale settlement at a battle's end (the stand-in for the M7.15 battle → campaign hand-off): the
/// army's side and the reported winner (−1 none), the battle-scale start, end and medical restore, the DR29 decrease, the persons of
/// the beaten side and of the winning side, the DA1 rise, and the campaign morale before and after (micro units).
/// </summary>
public readonly record struct MoraleSettlementRow(int Seq, int ArmyId, long AtMs, byte Side, int WinnerSide, long MoraleStart, long MoraleEnd,
                                                  long Restore, long DecreasePpm, long BeatenPersons, long WinnerSidePersons, long RisePpm,
                                                  long BeforePpm, long AfterPpm);

/// <summary>
/// [MOR] Canonical little-endian fixed-length encoding of <see cref="MoraleSettlementRow"/> (CLAUDE.md: every new serialized row type
/// ships a POPULATED-table test with the exact ExpectedLength, a bit-exact round trip and hash equality). Table = [version u8][count
/// i32] rows. The layout of the support tables (lane D's SupportCodec), with its own version byte.
/// </summary>
public static class MoraleCodec
{
    public const byte Version = 1;
    public const int HeaderBytes = 1 + 4;
    public const int SettlementRowBytes = 4 + 4 + 8 + 1 + 4 + 8 * 9;    // 93

    public static int ExpectedLength(int rows) => checked(HeaderBytes + rows * SettlementRowBytes);

    public static byte[] Encode(IReadOnlyList<MoraleSettlementRow> rows)
    {
        var b = new byte[ExpectedLength(rows.Count)];
        b[0] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), rows.Count);
        int at = HeaderBytes;
        void I32(int v) { BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v); at += 4; }
        void I64(long v) { BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), v); at += 8; }
        foreach (var r in rows)
        {
            I32(r.Seq); I32(r.ArmyId); I64(r.AtMs); b[at++] = r.Side; I32(r.WinnerSide);
            I64(r.MoraleStart); I64(r.MoraleEnd); I64(r.Restore); I64(r.DecreasePpm); I64(r.BeatenPersons); I64(r.WinnerSidePersons);
            I64(r.RisePpm); I64(r.BeforePpm); I64(r.AfterPpm);
        }
        if (at != b.Length) throw new InvalidOperationException("encoded length mismatch");
        return b;
    }

    public static MoraleSettlementRow[] Decode(byte[] b)
    {
        if (b.Length < HeaderBytes || b[0] != Version) throw new FormatException("morale table header");
        int n = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(1));
        // [MOR, verification round 1, MOR-V1-10] the count is bounded by the bytes present BEFORE ExpectedLength is computed, so a huge
        // count field is refused as a format error (FormatException), not by the checked arithmetic's OverflowException.
        if (n < 0 || n > (b.Length - HeaderBytes) / SettlementRowBytes || b.Length != ExpectedLength(n)) throw new FormatException("morale table length");
        int at = HeaderBytes;
        int I32() { int v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4; return v; }
        long I64() { long v = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(at)); at += 8; return v; }
        var rows = new MoraleSettlementRow[n];
        for (int i = 0; i < n; i++)
        {
            int seq = I32(), army = I32();
            long atMs = I64();
            byte side = b[at++];
            int winner = I32();
            rows[i] = new MoraleSettlementRow(seq, army, atMs, side, winner, I64(), I64(), I64(), I64(), I64(), I64(), I64(), I64(), I64());
        }
        return rows;
    }
}
