using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.War.Outcomes;

// ===================================================================================================================
// The M7 military record (DIR5 §4.3; C33): wars, participants, battles, sides, casualties by side and cause, leader fates,
// settlement outcomes and prisoner custody movements. M8 owns treaties and relations: no row carries a treaty, alliance or
// relation field (schema test). Plus the outcome world's current state (settlements, polities, custody), so the world's state
// hash is built from these encodings: a field missing here is missing from the hash too (lane S's rule).
// No row is keyed by a battle entity: battle-transient entities collapse into formation counts and are forgotten (DIR5 §2.9,
// "No persistent individual veteran table").
// ===================================================================================================================

/// <summary>A war. EndMs = -1 while it lasts; WinnerSide = -1 unless one side prevailed.</summary>
public sealed record WarRow(int WarId, long StartMs, long EndMs, byte Outcome, int WinnerSide);

/// <summary>A polity's part in a war. LeftMs = -1 while it takes part.</summary>
public sealed record WarParticipantRow(int WarId, int Polity, byte Side, long JoinedMs, long LeftMs, byte Exit);

/// <summary>A battle (field battle, siege, storming). EndCode: the deciding mechanism's own end code (lane TR
/// <c>BattleEndReason</c>, lane S <c>SiegeOutcome</c>, 0 for the stand-in). Turn and Pulse are those of EndMs (the pulse of WarId's clock).</summary>
public sealed record BattleRow(int BattleId, int WarId, byte Kind, byte Mechanism, byte EndCode, long StartMs, long EndMs, long Turn, long Pulse,
                               int SettlementId, int WinnerSide);

/// <summary>One side of a battle: the polity, how many formations and people it brought.</summary>
public sealed record BattleSideRow(int BattleId, byte Side, int Polity, int Formations, long PersonnelStart);

/// <summary>Casualties of one side of a battle by cause (Side 2 = the civilians of a settlement taken in it).</summary>
public sealed record CasualtyRow(int BattleId, byte Side, int Polity, byte Cause, long Count);

/// <summary>A leader-fate transition (append-only). Turn and Pulse are those of AtMs (the pulse of WarId's clock; -1 without a war).
/// ByPolity: who caused it (the taker of the last city, the captor); HeldBy: the captor holding the leader before it (-1: free).</summary>
public sealed record LeaderFateRow(int Seq, int Polity, int LeaderId, byte From, byte To, byte Cause, long AtMs, long Turn, int WarId, long Pulse,
                                   int ByPolity, int HeldBy, int SettlementId);

/// <summary>What happened to a settlement taken in a battle: who took it from whom, the victor's choice and its flows.</summary>
public sealed record SettlementOutcomeRow(int SettlementId, int BattleId, long AtMs, int From, int To, byte Disposition, long LootMoney, long LootGrain,
                                          long Killed, long Fled, long Burnt, long GrainBurnt, long GrievancePermilleYear);

/// <summary>A custody movement of prisoners (append-only). Money: the ransom paid (0 otherwise).</summary>
public sealed record PrisonerRow(int Seq, long AtMs, int Captor, int Origin, byte Action, long Count, long Money, int BattleId);

/// <summary>Current state of a settlement. Taken*: the latest capture (its post-battle window is open only at TakenAtMs). Sackers:
/// the victor's men present at that capture, which set the sack intensity at the window (fix r1, W-R1-10: in the hash).</summary>
public sealed record SettlementStateRow(int Id, bool IsCity, int Controller, bool Razed, int PopulationPolity, int Installation,
                                        int CivAccount, int GrainAccount, int MoneyAccount, int BuildingAccount, long GrievancePermilleYear,
                                        long TakenAtMs, int TakenBy, int TakenFrom, int TakenBattle, int TakenWar, bool Disposed, long Sackers);

/// <summary>Current state of a polity: its leader, the city count at the last settled instant, the latest loss (by whom, which
/// settlement, in which war: fix r1, W-R1-5), standing terms and its accounts (treasury, granary, home troops, refugees, paroled,
/// impressed, enslaved, leader).</summary>
public sealed record PolityStateRow(int Id, int LeaderId, byte Fate, int Seat, int HeldBy, long CitiesSettled, int LastLossBy, int LastLossSettlement,
                                    int LastLossWar, uint Institutions, long MenRansomTerms, long LeaderRansomTerms, int[] Accounts);

/// <summary>Custody of one origin's men by one captor, with its ration clock (exact carry) and unpaid rations.</summary>
public sealed record CustodyRow(int Captor, int Origin, int Account, long SettledMs, long CarryNum, long CarryDen, long FoodArrears, long ExchangeOffer);

/// <summary>
/// Canonical little-endian fixed-length encoding of the lane W rows (CLAUDE.md: every new serialized row type ships a POPULATED
/// table test with exact length, bit-exact round trip and hash equality). Table = [version u8][row count i32] rows.
/// </summary>
public static class OutcomeCodec
{
    /// <summary>2 since fix r1 (SettlementStateRow gained Sackers, PolityStateRow gained LastLossWar).</summary>
    public const byte Version = 2;
    public const int HeaderBytes = 1 + 4;
    public const int PolityAccounts = 8;

    public const int WarRowBytes = 4 + 8 + 8 + 1 + 4;                                         // 25
    public const int ParticipantRowBytes = 4 + 4 + 1 + 8 + 8 + 1;                             // 26
    public const int BattleRowBytes = 4 + 4 + 1 + 1 + 1 + 8 + 8 + 8 + 8 + 4 + 4;              // 51
    public const int BattleSideRowBytes = 4 + 1 + 4 + 4 + 8;                                  // 21
    public const int CasualtyRowBytes = 4 + 1 + 4 + 1 + 8;                                    // 18
    public const int LeaderFateRowBytes = 4 + 4 + 4 + 1 + 1 + 1 + 8 + 8 + 4 + 8 + 4 + 4 + 4;  // 55
    public const int SettlementOutcomeRowBytes = 4 + 4 + 8 + 4 + 4 + 1 + 7 * 8;               // 81
    public const int PrisonerRowBytes = 4 + 8 + 4 + 4 + 1 + 8 + 8 + 4;                        // 41
    public const int SettlementStateRowBytes = 4 + 1 + 4 + 1 + 4 + 4 + 4 * 4 + 8 + 8 + 4 + 4 + 4 + 4 + 1 + 8;   // 75
    public const int PolityStateRowBytes = 4 + 4 + 1 + 4 + 4 + 8 + 4 + 4 + 4 + 4 + 8 + 8 + PolityAccounts * 4;  // 89
    public const int CustodyRowBytes = 4 + 4 + 4 + 8 + 8 + 8 + 8 + 8;                         // 52

    private ref struct W
    {
        private readonly Span<byte> _b;
        public int O;
        public W(Span<byte> b) { _b = b; O = 0; }
        public void I(int v) { BinaryPrimitives.WriteInt32LittleEndian(_b[O..], v); O += 4; }
        public void U(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(_b[O..], v); O += 4; }
        public void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(_b[O..], v); O += 8; }
        public void B(byte v) { _b[O] = v; O += 1; }
        public void Z(bool v) { _b[O] = v ? (byte)1 : (byte)0; O += 1; }
        public void End(int expected) { if (O != expected) throw new InvalidOperationException("row length drift"); }
    }

    private ref struct R
    {
        private readonly ReadOnlySpan<byte> _b;
        public int O;
        public R(ReadOnlySpan<byte> b) { _b = b; O = 0; }
        public int I() { int v = BinaryPrimitives.ReadInt32LittleEndian(_b[O..]); O += 4; return v; }
        public uint U() { uint v = BinaryPrimitives.ReadUInt32LittleEndian(_b[O..]); O += 4; return v; }
        public long L() { long v = BinaryPrimitives.ReadInt64LittleEndian(_b[O..]); O += 8; return v; }
        public byte B() => _b[O++];
        public bool Z() { byte v = _b[O++]; if (v > 1) throw new FormatException("bad bool"); return v == 1; }
        public void End(int expected) { if (O != expected) throw new FormatException("row length drift"); }
    }

    public static void Encode(WarRow r, Span<byte> d)
    { var w = new W(d); w.I(r.WarId); w.L(r.StartMs); w.L(r.EndMs); w.B(r.Outcome); w.I(r.WinnerSide); w.End(WarRowBytes); }
    public static WarRow DecodeWar(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new WarRow(r.I(), r.L(), r.L(), r.B(), r.I()); r.End(WarRowBytes); return x; }

    public static void Encode(WarParticipantRow r, Span<byte> d)
    { var w = new W(d); w.I(r.WarId); w.I(r.Polity); w.B(r.Side); w.L(r.JoinedMs); w.L(r.LeftMs); w.B(r.Exit); w.End(ParticipantRowBytes); }
    public static WarParticipantRow DecodeParticipant(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new WarParticipantRow(r.I(), r.I(), r.B(), r.L(), r.L(), r.B()); r.End(ParticipantRowBytes); return x; }

    public static void Encode(BattleRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.BattleId); w.I(r.WarId); w.B(r.Kind); w.B(r.Mechanism); w.B(r.EndCode); w.L(r.StartMs); w.L(r.EndMs); w.L(r.Turn); w.L(r.Pulse);
        w.I(r.SettlementId); w.I(r.WinnerSide); w.End(BattleRowBytes);
    }
    public static BattleRow DecodeBattle(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new BattleRow(r.I(), r.I(), r.B(), r.B(), r.B(), r.L(), r.L(), r.L(), r.L(), r.I(), r.I()); r.End(BattleRowBytes); return x; }

    public static void Encode(BattleSideRow r, Span<byte> d)
    { var w = new W(d); w.I(r.BattleId); w.B(r.Side); w.I(r.Polity); w.I(r.Formations); w.L(r.PersonnelStart); w.End(BattleSideRowBytes); }
    public static BattleSideRow DecodeBattleSide(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new BattleSideRow(r.I(), r.B(), r.I(), r.I(), r.L()); r.End(BattleSideRowBytes); return x; }

    public static void Encode(CasualtyRow r, Span<byte> d)
    { var w = new W(d); w.I(r.BattleId); w.B(r.Side); w.I(r.Polity); w.B(r.Cause); w.L(r.Count); w.End(CasualtyRowBytes); }
    public static CasualtyRow DecodeCasualty(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new CasualtyRow(r.I(), r.B(), r.I(), r.B(), r.L()); r.End(CasualtyRowBytes); return x; }

    public static void Encode(LeaderFateRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.Seq); w.I(r.Polity); w.I(r.LeaderId); w.B(r.From); w.B(r.To); w.B(r.Cause); w.L(r.AtMs); w.L(r.Turn); w.I(r.WarId); w.L(r.Pulse);
        w.I(r.ByPolity); w.I(r.HeldBy); w.I(r.SettlementId); w.End(LeaderFateRowBytes);
    }
    public static LeaderFateRow DecodeLeaderFate(ReadOnlySpan<byte> s)
    {
        var r = new R(s);
        var x = new LeaderFateRow(r.I(), r.I(), r.I(), r.B(), r.B(), r.B(), r.L(), r.L(), r.I(), r.L(), r.I(), r.I(), r.I());
        r.End(LeaderFateRowBytes);
        return x;
    }

    public static void Encode(SettlementOutcomeRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.SettlementId); w.I(r.BattleId); w.L(r.AtMs); w.I(r.From); w.I(r.To); w.B(r.Disposition); w.L(r.LootMoney); w.L(r.LootGrain);
        w.L(r.Killed); w.L(r.Fled); w.L(r.Burnt); w.L(r.GrainBurnt); w.L(r.GrievancePermilleYear); w.End(SettlementOutcomeRowBytes);
    }
    public static SettlementOutcomeRow DecodeSettlementOutcome(ReadOnlySpan<byte> s)
    {
        var r = new R(s);
        var x = new SettlementOutcomeRow(r.I(), r.I(), r.L(), r.I(), r.I(), r.B(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L());
        r.End(SettlementOutcomeRowBytes);
        return x;
    }

    public static void Encode(PrisonerRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.Seq); w.L(r.AtMs); w.I(r.Captor); w.I(r.Origin); w.B(r.Action); w.L(r.Count); w.L(r.Money); w.I(r.BattleId); w.End(PrisonerRowBytes);
    }
    public static PrisonerRow DecodePrisoner(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new PrisonerRow(r.I(), r.L(), r.I(), r.I(), r.B(), r.L(), r.L(), r.I()); r.End(PrisonerRowBytes); return x; }

    public static void Encode(SettlementStateRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.Id); w.Z(r.IsCity); w.I(r.Controller); w.Z(r.Razed); w.I(r.PopulationPolity); w.I(r.Installation);
        w.I(r.CivAccount); w.I(r.GrainAccount); w.I(r.MoneyAccount); w.I(r.BuildingAccount); w.L(r.GrievancePermilleYear);
        w.L(r.TakenAtMs); w.I(r.TakenBy); w.I(r.TakenFrom); w.I(r.TakenBattle); w.I(r.TakenWar); w.Z(r.Disposed); w.L(r.Sackers); w.End(SettlementStateRowBytes);
    }
    public static SettlementStateRow DecodeSettlementState(ReadOnlySpan<byte> s)
    {
        var r = new R(s);
        var x = new SettlementStateRow(r.I(), r.Z(), r.I(), r.Z(), r.I(), r.I(), r.I(), r.I(), r.I(), r.I(), r.L(), r.L(), r.I(), r.I(), r.I(), r.I(), r.Z(), r.L());
        r.End(SettlementStateRowBytes);
        return x;
    }

    public static void Encode(PolityStateRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.Id); w.I(r.LeaderId); w.B(r.Fate); w.I(r.Seat); w.I(r.HeldBy); w.L(r.CitiesSettled); w.I(r.LastLossBy); w.I(r.LastLossSettlement);
        w.I(r.LastLossWar); w.U(r.Institutions); w.L(r.MenRansomTerms); w.L(r.LeaderRansomTerms);
        if (r.Accounts.Length != PolityAccounts) throw new ArgumentException("account array length");
        foreach (var a in r.Accounts) w.I(a);
        w.End(PolityStateRowBytes);
    }
    public static PolityStateRow DecodePolityState(ReadOnlySpan<byte> s)
    {
        var r = new R(s);
        int id = r.I(), leader = r.I(); byte fate = r.B(); int seat = r.I(), held = r.I(); long cities = r.L(); int lossBy = r.I(), lossAt = r.I(), lossWar = r.I();
        uint inst = r.U(); long men = r.L(), lead = r.L();
        var acc = new int[PolityAccounts];
        for (int i = 0; i < acc.Length; i++) acc[i] = r.I();
        r.End(PolityStateRowBytes);
        return new PolityStateRow(id, leader, fate, seat, held, cities, lossBy, lossAt, lossWar, inst, men, lead, acc);
    }

    public static void Encode(CustodyRow r, Span<byte> d)
    {
        var w = new W(d);
        w.I(r.Captor); w.I(r.Origin); w.I(r.Account); w.L(r.SettledMs); w.L(r.CarryNum); w.L(r.CarryDen); w.L(r.FoodArrears); w.L(r.ExchangeOffer);
        w.End(CustodyRowBytes);
    }
    public static CustodyRow DecodeCustody(ReadOnlySpan<byte> s)
    { var r = new R(s); var x = new CustodyRow(r.I(), r.I(), r.I(), r.L(), r.L(), r.L(), r.L(), r.L()); r.End(CustodyRowBytes); return x; }

    // ------------------------------------------------------------------ tables

    public delegate void SpanEncoder<T>(T row, Span<byte> dst);
    public delegate T SpanDecoder<T>(ReadOnlySpan<byte> src);

    public static byte[] EncodeTable<T>(IReadOnlyList<T> rows, int rowBytes, SpanEncoder<T> encode)
    {
        var buf = new byte[HeaderBytes + rows.Count * rowBytes];
        buf[0] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), rows.Count);
        for (int i = 0; i < rows.Count; i++) encode(rows[i], buf.AsSpan(HeaderBytes + i * rowBytes, rowBytes));
        return buf;
    }

    public static List<T> DecodeTable<T>(ReadOnlySpan<byte> src, int rowBytes, SpanDecoder<T> decode)
    {
        if (src.Length < HeaderBytes || src[0] != Version) throw new FormatException("bad table header");
        int n = BinaryPrimitives.ReadInt32LittleEndian(src[1..]);
        if (n < 0 || src.Length != HeaderBytes + (long)n * rowBytes) throw new FormatException("table length does not match its row count");
        var list = new List<T>(n);
        for (int i = 0; i < n; i++) list.Add(decode(src.Slice(HeaderBytes + i * rowBytes, rowBytes)));
        return list;
    }

    public static string HashHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
