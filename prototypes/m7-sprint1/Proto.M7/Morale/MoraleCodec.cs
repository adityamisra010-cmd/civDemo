using System.Buffers.Binary;

namespace Proto.M7.Morale;

/// <summary>
/// [MOR] One army's campaign-morale settlement at a battle's end (the stand-in for the M7.15 battle → campaign hand-off): the
/// army's side and the reported winner (−1 none), the battle-scale start, end and medical restore, the DR29 decrease, the persons of
/// the beaten side and of the winning side, the DA1 rise, and the campaign morale before and after (micro units).
/// [MOR2, D-083 DB1] Three fields appended (codec version 2): whether the army's formation entered the battle (1) or never reached the
/// field (0), what the battle itself charged at the entry on the battle scale (0 while TacticalRules.EntryMoraleCost is 0), and the
/// settlement's entry term in micro units (MoraleRates.EntryCostPpm when entered, else 0). The decrease is measured from
/// MoraleStart − EntryInBattle, so AfterPpm = clamp(BeforePpm − EntryPpm − DecreasePpm + RisePpm, 0, 1,000,000) by hand from the row.
/// </summary>
public readonly record struct MoraleSettlementRow(int Seq, int ArmyId, long AtMs, byte Side, int WinnerSide, long MoraleStart, long MoraleEnd,
                                                  long Restore, long DecreasePpm, long BeatenPersons, long WinnerSidePersons, long RisePpm,
                                                  long BeforePpm, long AfterPpm, byte Entered, long EntryInBattle, long EntryPpm);

/// <summary>
/// [MOR] Canonical little-endian fixed-length encoding of <see cref="MoraleSettlementRow"/> (CLAUDE.md: every new serialized row type
/// ships a POPULATED-table test with the exact ExpectedLength, a bit-exact round trip and hash equality). Table = [version u8][count
/// i32] rows. The layout of the support tables (lane D's SupportCodec), with its own version byte.
/// [MOR2] Version 2: the row gained Entered (u8, 0 or 1), EntryInBattle (i64) and EntryPpm (i64) after AfterPpm: 93 → 110 bytes. A
/// version-1 table is refused (FormatException), as is an Entered byte other than 0 or 1.
/// </summary>
public static class MoraleCodec
{
    public const byte Version = 2;
    public const int HeaderBytes = 1 + 4;
    public const int SettlementRowBytes = 4 + 4 + 8 + 1 + 4 + 8 * 9 + 1 + 8 * 2;    // 110 (version 1: 93)

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
            if (r.Entered > 1) throw new ArgumentOutOfRangeException(nameof(rows), "Entered is 0 or 1");
            I32(r.Seq); I32(r.ArmyId); I64(r.AtMs); b[at++] = r.Side; I32(r.WinnerSide);
            I64(r.MoraleStart); I64(r.MoraleEnd); I64(r.Restore); I64(r.DecreasePpm); I64(r.BeatenPersons); I64(r.WinnerSidePersons);
            I64(r.RisePpm); I64(r.BeforePpm); I64(r.AfterPpm);
            b[at++] = r.Entered; I64(r.EntryInBattle); I64(r.EntryPpm);                                  // [MOR2] version 2
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
            long start = I64(), end = I64(), restore = I64(), decrease = I64(), beaten = I64(), winnerSide = I64(), rise = I64(),
                 before = I64(), after = I64();
            byte entered = b[at++];
            if (entered > 1) throw new FormatException("morale table: Entered is 0 or 1");                  // [MOR2]
            long entryInBattle = I64(), entryPpm = I64();
            rows[i] = new MoraleSettlementRow(seq, army, atMs, side, winner, start, end, restore, decrease, beaten, winnerSide, rise,
                                              before, after, entered, entryInBattle, entryPpm);
        }
        return rows;
    }
}
