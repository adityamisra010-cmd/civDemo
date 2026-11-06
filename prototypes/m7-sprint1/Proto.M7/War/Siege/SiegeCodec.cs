using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Time;

namespace Proto.M7.War.Siege;

/// <summary>Persistent state of one installation (the ledger balances are serialized by the ledger, not here).</summary>
public sealed record InstallationRow(int Id, byte Kind, int Controller, long X, long Y, long Capacity, int Civ, int Granary, int Cistern,
    int Magazine, int MissileStore, int Guns, byte Fort, long WallRaw, long WorksRaw, long UnrestRaw, long LoyaltyRaw, int ActiveSiege, bool Revolted,
    long UpkeepCarryNum, long UpkeepCarryDen, long UpkeepDue, long UpkeepPaid, long UpkeepArrears, long ArrearsSinceMs, long UnpaidRaw, long[] CivCarry,
    long TheatreLossSettledMs);

/// <summary>Persistent state of one formation: one capability, one state (Mobile, Garrisoned, Captured). Personnel is the ledger
/// balance of Account.</summary>
public sealed record FormationRow(int Id, int Owner, byte Family, byte Ranged, byte State, int At, long X, long Y, int Account, long[] Carry);

/// <summary>Persistent state of one siege: clock position, health state, pulse accumulators, every carry and pending order, and the
/// besiegers' lane E supply line (fixed at construction, with what it has shipped and delivered).</summary>
public sealed record SiegeRow(int Id, int Attacker, int Installation, int Defender, bool Begun, bool Active, byte Outcome,
    long EndMs, long CapMs, long StartTurnLengthMs, long PulseIndex, long PulseStart, long PulseEnd, long LastSettled,
    long CursorIndex, long CursorStart, long CursorEnd, long[] QRaw, long[] Refs, long LastAssaultMs, bool ReliefArrived, bool ReliefResolved, bool ReliefFailed,
    int[] Counters, long[] Accumulators, long[] FractionCarries, long[] AttCarry, long[] EngCarry, long[] ReliefCarry,
    long[] Pending, int[] PendingLogs, long[] PendingIssued, int[] PendingIssuers, int[] Accounts,
    bool HasLine, long LineLossNum, long LineLossDen, long LineCapacityPerYear, long Shipped, long Delivered, bool RationHeld, long[] ReinfArmCarry);

/// <summary>
/// Canonical little-endian fixed-length encoding of the lane S row types (CLAUDE.md: every new serialized row type ships a
/// POPULATED-table test with exact length, bit-exact round trip and hash equality). Table = [version u8][row count i32] rows.
/// The world's state hash is built from these encodings, so a field missing here is missing from the hash too.
/// </summary>
public static class SiegeCodec
{
    /// <summary>Version 2 (lane S resume, lane E reuse): missile store and arrears clock in the installation row; split ammunition
    /// shares, accumulators and carries, the besiegers' park accounts and supply line in the siege row. Version 3 (fix r1): the
    /// siege row ends with the standing human ration flag (S-V6) and the per-arm reinforcement carries; head-loss carries are in
    /// 10⁻¹⁸ head (S-V1; same field width). Version 4 (fix r2): the installation row ends with the instant up to which its
    /// theatre-clock desertion and unrest are integrated (S2-V2: split at every siege start and end).</summary>
    public const byte Version = 4;
    public const int HeaderBytes = 1 + 4;
    public const int InstallationRowBytes = 4 + 1 + 4 + 3 * 8 + 6 * 4 + 1 + 4 * 8 + 4 + 1 + 7 * 8 + SiegeWorld.FlowCount * 8 + 8;   // 223
    public const int FormationRowBytes = 4 + 4 + 1 + 1 + 1 + 4 + 8 + 8 + 4 + SiegeWorld.FlowCount * 8;                         // 99
    public const int QCount = 18, RefCount = 6, CounterCount = 4, AccCount = 18, FracCount = 18, PendCount = 3, PendLogCount = 4, AccountCount = 15;
    public const int SiegeRowBytes = 4 * 4 + 3 + 7 * 8 + 3 * 8 + QCount * 8 + RefCount * 8 + 8 + 3 + CounterCount * 4 + AccCount * 8
                                   + FracCount * 16 + 4 * SiegeWorld.FlowCount * 8 + 4 * 8 + 4 * 8 + PendCount * 8 + PendLogCount * (4 + 8 + 4)
                                   + AccountCount * 4 + 1 + 5 * 8 + 1 + 4 * 8;                                          // 1,292

    private ref struct W
    {
        private readonly Span<byte> _b;
        public int O;
        public W(Span<byte> b) { _b = b; O = 0; }
        public void I(int v) { BinaryPrimitives.WriteInt32LittleEndian(_b[O..], v); O += 4; }
        public void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(_b[O..], v); O += 8; }
        public void B(byte v) { _b[O] = v; O += 1; }
        public void Z(bool v) { _b[O] = v ? (byte)1 : (byte)0; O += 1; }
        public void LA(long[] a, int n) { if (a.Length != n) throw new ArgumentException("array length"); foreach (var v in a) L(v); }
        public void IA(int[] a, int n) { if (a.Length != n) throw new ArgumentException("array length"); foreach (var v in a) I(v); }
    }

    private ref struct R
    {
        private readonly ReadOnlySpan<byte> _b;
        public int O;
        public R(ReadOnlySpan<byte> b) { _b = b; O = 0; }
        public int I() { int v = BinaryPrimitives.ReadInt32LittleEndian(_b[O..]); O += 4; return v; }
        public long L() { long v = BinaryPrimitives.ReadInt64LittleEndian(_b[O..]); O += 8; return v; }
        public byte B() => _b[O++];
        public bool Z() { byte v = _b[O++]; if (v > 1) throw new FormatException("bad bool"); return v == 1; }
        public long[] LA(int n) { var a = new long[n]; for (int i = 0; i < n; i++) a[i] = L(); return a; }
        public int[] IA(int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = I(); return a; }
    }

    // ------------------------------------------------------------------ installation

    public static void Encode(InstallationRow r, Span<byte> dst)
    {
        var w = new W(dst);
        w.I(r.Id); w.B(r.Kind); w.I(r.Controller); w.L(r.X); w.L(r.Y); w.L(r.Capacity);
        w.I(r.Civ); w.I(r.Granary); w.I(r.Cistern); w.I(r.Magazine); w.I(r.MissileStore); w.I(r.Guns); w.B(r.Fort);
        w.L(r.WallRaw); w.L(r.WorksRaw); w.L(r.UnrestRaw); w.L(r.LoyaltyRaw); w.I(r.ActiveSiege); w.Z(r.Revolted);
        w.L(r.UpkeepCarryNum); w.L(r.UpkeepCarryDen); w.L(r.UpkeepDue); w.L(r.UpkeepPaid); w.L(r.UpkeepArrears); w.L(r.ArrearsSinceMs); w.L(r.UnpaidRaw);
        w.LA(r.CivCarry, SiegeWorld.FlowCount); w.L(r.TheatreLossSettledMs);
        if (w.O != InstallationRowBytes) throw new InvalidOperationException("installation row length drift");
    }

    public static InstallationRow DecodeInstallation(ReadOnlySpan<byte> src)
    {
        var r = new R(src);
        var row = new InstallationRow(r.I(), r.B(), r.I(), r.L(), r.L(), r.L(), r.I(), r.I(), r.I(), r.I(), r.I(), r.I(), r.B(),
            r.L(), r.L(), r.L(), r.L(), r.I(), r.Z(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.LA(SiegeWorld.FlowCount), r.L());
        if (r.O != InstallationRowBytes) throw new FormatException("installation row length drift");
        return row;
    }

    // ------------------------------------------------------------------ formation

    public static void Encode(FormationRow r, Span<byte> dst)
    {
        var w = new W(dst);
        w.I(r.Id); w.I(r.Owner); w.B(r.Family); w.B(r.Ranged); w.B(r.State); w.I(r.At); w.L(r.X); w.L(r.Y); w.I(r.Account);
        w.LA(r.Carry, SiegeWorld.FlowCount);
        if (w.O != FormationRowBytes) throw new InvalidOperationException("formation row length drift");
    }

    public static FormationRow DecodeFormation(ReadOnlySpan<byte> src)
    {
        var r = new R(src);
        var row = new FormationRow(r.I(), r.I(), r.B(), r.B(), r.B(), r.I(), r.L(), r.L(), r.I(), r.LA(SiegeWorld.FlowCount));
        if (r.O != FormationRowBytes) throw new FormatException("formation row length drift");
        return row;
    }

    // ------------------------------------------------------------------ siege

    public static void Encode(SiegeRow r, Span<byte> dst)
    {
        var w = new W(dst);
        w.I(r.Id); w.I(r.Attacker); w.I(r.Installation); w.I(r.Defender); w.Z(r.Begun); w.Z(r.Active); w.B(r.Outcome);
        w.L(r.EndMs); w.L(r.CapMs); w.L(r.StartTurnLengthMs); w.L(r.PulseIndex); w.L(r.PulseStart); w.L(r.PulseEnd); w.L(r.LastSettled);
        w.L(r.CursorIndex); w.L(r.CursorStart); w.L(r.CursorEnd);
        w.LA(r.QRaw, QCount); w.LA(r.Refs, RefCount); w.L(r.LastAssaultMs); w.Z(r.ReliefArrived); w.Z(r.ReliefResolved); w.Z(r.ReliefFailed);
        w.IA(r.Counters, CounterCount); w.LA(r.Accumulators, AccCount); w.LA(r.FractionCarries, FracCount * 2);
        w.LA(r.AttCarry, 4 * SiegeWorld.FlowCount); w.LA(r.EngCarry, 4); w.LA(r.ReliefCarry, 4);
        w.LA(r.Pending, PendCount); w.IA(r.PendingLogs, PendLogCount); w.LA(r.PendingIssued, PendLogCount); w.IA(r.PendingIssuers, PendLogCount);
        w.IA(r.Accounts, AccountCount);
        w.Z(r.HasLine); w.L(r.LineLossNum); w.L(r.LineLossDen); w.L(r.LineCapacityPerYear); w.L(r.Shipped); w.L(r.Delivered);
        w.Z(r.RationHeld); w.LA(r.ReinfArmCarry, 4);
        if (w.O != SiegeRowBytes) throw new InvalidOperationException("siege row length drift");
    }

    public static SiegeRow DecodeSiege(ReadOnlySpan<byte> src)
    {
        var r = new R(src);
        var row = new SiegeRow(r.I(), r.I(), r.I(), r.I(), r.Z(), r.Z(), r.B(),
            r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(), r.L(),
            r.LA(QCount), r.LA(RefCount), r.L(), r.Z(), r.Z(), r.Z(), r.IA(CounterCount), r.LA(AccCount), r.LA(FracCount * 2),
            r.LA(4 * SiegeWorld.FlowCount), r.LA(4), r.LA(4), r.LA(PendCount), r.IA(PendLogCount), r.LA(PendLogCount), r.IA(PendLogCount),
            r.IA(AccountCount), r.Z(), r.L(), r.L(), r.L(), r.L(), r.L(), r.Z(), r.LA(4));
        if (r.O != SiegeRowBytes) throw new FormatException("siege row length drift");
        return row;
    }

    // ------------------------------------------------------------------ export from a world

    public static List<InstallationRow> Installations(SiegeWorld w)
    {
        var list = new List<InstallationRow>();
        foreach (var i in w.InstallationsInternal)
            list.Add(new InstallationRow(i.Spec.Id, (byte)i.Spec.Kind, i.Controller, i.Spec.X, i.Spec.Y, i.Spec.Capacity, i.Civ, i.Granary, i.Cistern,
                i.Magazine, i.MissileStore, i.Guns, (byte)i.Spec.Fort, i.Wall.Raw, i.Works.Raw, i.Unrest.Raw, i.Loyalty.Raw, i.ActiveSiege, i.Revolted,
                i.UpkeepCarry.Num, i.UpkeepCarry.Den, i.UpkeepDue, i.UpkeepPaid, i.UpkeepArrears, i.ArrearsSinceMs, i.UnpaidShare.Raw, (long[])i.CivCarry.Clone(),
                i.TheatreLossSettledMs));
        return list;
    }

    public static List<FormationRow> Formations(SiegeWorld w)
    {
        var list = new List<FormationRow>();
        foreach (var f in w.FormationsInternal)
            list.Add(new FormationRow(f.Spec.Id, f.Spec.Owner, (byte)f.Spec.Family, (byte)f.Spec.Ranged, (byte)f.State, f.At, f.X, f.Y, f.Account,
                (long[])f.Carry.Clone()));
        return list;
    }

    public static List<SiegeRow> Sieges(SiegeWorld w)
    {
        var list = new List<SiegeRow>();
        foreach (var x in w.SiegesInternal)
        {
            var q = new[] { x.NutG.Raw, x.NutC.Raw, x.NutA.Raw, x.Dis.Raw, x.DisA.Raw, x.Morale.Raw, x.MoraleA.Raw, x.Mining.Raw, x.Approach.Raw,
                            x.Ration.Raw, x.AttWorks.Raw, x.HealthD.Raw, x.HealthA.Raw, x.MisShare.Raw, x.MunShare.Raw, x.MisShareA.Raw, x.MunShareA.Raw,
                            x.SupplyA.Raw };
            var refs = new[] { x.DRef, x.ARef, x.G0, x.A0, x.C0, x.LabourRef };
            var acc = new[] { x.DueG, x.DrawG, x.DueC, x.DrawC, x.DueWG, x.DrawWG, x.DueWC, x.DrawWC, x.DueMis, x.DrawMis, x.DueMun, x.DrawMun,
                              x.DueA, x.DrawA, x.DueMisA, x.DrawMisA, x.DueMunA, x.DrawMunA };
            var fr = new[] { x.CFoodG, x.CFoodC, x.CWaterIn, x.CWaterG, x.CWaterC, x.CMis, x.CMun, x.CCorrFood, x.CCorrMun, x.CCorrMis, x.CSupplyA,
                             x.CLineCap, x.CEatA, x.CMisA, x.CMunA, x.CEvac, x.CReinf, x.CReinfA };
            var frac = new long[FracCount * 2];
            for (int k = 0; k < FracCount; k++) { frac[2 * k] = fr[k].Num; frac[2 * k + 1] = fr[k].Den; }
            var accounts = new int[AccountCount];
            for (int k = 0; k < 4; k++) { accounts[k] = x.AttPeople[k]; accounts[4 + k] = x.AttEngines[k]; accounts[8 + k] = x.Relief[k]; }
            accounts[12] = x.AttGranary; accounts[13] = x.AttMunitions; accounts[14] = x.AttMissiles;
            list.Add(new SiegeRow(x.Spec.Id, x.Spec.Attacker, x.Spec.InstallationId, x.Defender, x.Begun, x.Active, (byte)x.Outcome,
                x.EndMs, x.CapMs, x.StartTurnLengthMs, x.PulseIndex, x.PulseStart, x.PulseEnd, x.LastSettled,
                x.Cursor.Index, x.Cursor.StartMs, x.Cursor.EndMs, q, refs, x.LastAssaultMs, x.ReliefArrived, x.ReliefResolved, x.ReliefFailed,
                new[] { x.Assaults, x.FailedAssaults, x.Sallies, x.Mines }, acc, frac,
                (long[])x.AttCarry.Clone(), (long[])x.EngCarry.Clone(), (long[])x.ReliefCarry.Clone(),
                new[] { x.PendRation, x.PendAssault, x.PendSally },
                new[] { x.PendRationLog, x.PendAssaultLog, x.PendSallyLog, x.PendLiftLog },
                new[] { x.PendRationIssued, x.PendAssaultIssued, x.PendSallyIssued, x.PendLiftIssued },
                new[] { x.PendRationIssuer, x.PendAssaultIssuer, x.PendSallyIssuer, x.PendLiftIssuer }, accounts,
                x.HasLine, x.LineLoss.Num, x.LineLoss.Den, x.LineCapacityPerYear, x.Shipped, x.Delivered, x.RationHeld, (long[])x.ReinfArmCarry.Clone()));
        }
        return list;
    }

    // ------------------------------------------------------------------ tables

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

    public delegate void SpanEncoder<T>(T row, Span<byte> dst);
    public delegate T SpanDecoder<T>(ReadOnlySpan<byte> src);

    public static string HashHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
