using System.Buffers.Binary;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Time;

namespace Proto.M7.War;

/// <summary>
/// [AP] Coefficients of lane AP's concealment and preparation actions (D-082 DA8; docs/m7-plan/concealment-actions-proposal.md §3, §4).
/// Every value is a coefficient INSIDE an equation (the detection distance, the works build rate, the works-to-cell thresholds), never
/// a standing modifier (Law 2). PROPOSAL design; every magnitude is a PLACEHOLDER with the direction stated.
/// </summary>
public sealed record ConcealmentRules
{
    /// <summary>Camouflage multiplies the distance at which an observer detects the agent (on top of a hide's terrain factor).
    /// PLACEHOLDER 1/2 (direction: concealment from observation shortens detection range, US Army FM 3-21.8 (2007) "cover and
    /// concealment" — lane TR's citation; the magnitude is not cited).</summary>
    public Q32 CamouflageFactor { get; init; } = Q32.FromRatio(1, 2);

    /// <summary>Field works built per sim-year of digging, in ppm of COMPLETE works (parapet and trench line plus the ditch in front),
    /// a linear rate integrated exactly over the elapsed time (Law 3). PLACEHOLDER 360,000,000 ppm per year = complete works in one
    /// 360-day year's 1/360, i.e. one day of the whole formation's labour (direction: a field force could fortify its own position
    /// within a day — the Roman marching camp, Polybius, Histories VI.27-42 [KNOW, not fetched]; magnitude not cited).</summary>
    public long WorksRatePpmPerYear { get; init; } = 360_000_000;

    /// <summary>Complete works, ppm.</summary>
    public const long WorksFullPpm = 1_000_000;

    /// <summary>Works at or above this realize the COVER cells (Earthwork) on the battle grid. PLACEHOLDER: half the works.</summary>
    public long CoverThresholdPpm { get; init; } = 500_000;
    /// <summary>Works at or above this also realize the OBSTACLE cells (Ditch) in front. PLACEHOLDER: the complete works.</summary>
    public long ObstacleThresholdPpm { get; init; } = WorksFullPpm;

    public static ConcealmentRules Proposal() => new();

    public void Validate()
    {
        if (CamouflageFactor.Raw <= 0 || CamouflageFactor > Q32.One) throw new ArgumentOutOfRangeException(nameof(CamouflageFactor), "camouflage factor must be in (0, 1]");
        if (WorksRatePpmPerYear < 0) throw new ArgumentOutOfRangeException(nameof(WorksRatePpmPerYear));
        if (CoverThresholdPpm < 1 || CoverThresholdPpm > WorksFullPpm || ObstacleThresholdPpm < CoverThresholdPpm || ObstacleThresholdPpm > WorksFullPpm)
            throw new ArgumentOutOfRangeException(nameof(CoverThresholdPpm), "1 <= cover threshold <= obstacle threshold <= complete works");
    }
}

/// <summary>[AP] The concealment and preparation states an agent can hold (flags; stable codes).</summary>
[Flags]
public enum ConcealKind : byte
{
    None = 0,
    /// <summary>Hidden: dispersed into the cover of its cell, still, no fires (DA8 "hide").</summary>
    Hidden = 1,
    /// <summary>Camouflaged: disguised by its own effort, in any terrain (DA8 "camouflage").</summary>
    Camouflaged = 2,
    /// <summary>Entrenching / entrenched: digging field works at its cell (DA8 "entrench").</summary>
    Entrenched = 4,
}

/// <summary>[AP] What ended a state (stable codes). None on a Begin row.</summary>
public enum ConcealEnd : byte
{
    None = 0,
    /// <summary>The agent moved or retreated: it left its cover, its camouflage and its works (the works are lost).</summary>
    Moved = 1,
    /// <summary>The agent attacked: an attack reveals the attacker (it ends hiding and camouflage; its works stay).</summary>
    Attacked = 2,
    /// <summary>An enemy polity detected it: an enemy agent's accepted move or retreat ENDED within its shortened detection distance
    /// (judged at the end position only: T3's moves are instantaneous, so passing through does not spot; [AP round 1] proposal §3.2).
    /// [AP round 2] Or the concealment began where an enemy polity detects it (spotted at once, with that order's own log index;
    /// proposal §3.6). "Enemy" = a polity sharing a war with the agent's polity (ActionWindowBook.AreEnemies; proposal §3.7).</summary>
    Spotted = 3,
}

/// <summary>[AP] One change of an agent's concealment or works state, in execution order. LogIndex = the order that caused it
/// (the agent's own action, or the enemy move that spotted it); Begin false rows carry the cause.</summary>
public readonly record struct ConcealmentChange(int Seq, int LogIndex, long AtMs, int AgentId, ConcealKind Kind, bool Begin, ConcealEnd Cause);

/// <summary>[AP] An accepted Attack action (a war-pulse ENGAGEMENT): who attacked whom, when, whether it was an AMBUSH launched from
/// concealment (the attacker held Hidden or Camouflaged and the target's polity did not detect it at AtMs), the attacker's states
/// before its attack, and the target's field works at AtMs (ppm) and cell. The battle hand-off reads it (<see cref="EngagementBattle"/>).</summary>
public readonly record struct EngagementRecord(int LogIndex, long AtMs, int Attacker, int Target, int AttackerPolity, int TargetPolity, bool Ambush,
                                               ConcealKind AttackerStates, long TargetWorksPpm, long TargetCell);

/// <summary>[AP] One agent's state for persistence (the codec): its flags and the instants each state began (-1 = not held).</summary>
public readonly record struct ConcealmentStateRow(int AgentId, ConcealKind Flags, long HiddenSinceMs, long CamouflagedSinceMs, long EntrenchSinceMs);

/// <summary>
/// [AP] Field works: where an agent can dig, the works it has at an instant, and how the works become cover and obstacle cells on
/// lane TR's battle grid (docs/m7-plan/concealment-actions-proposal.md §4.3). Integer only.
/// </summary>
public static class FieldWorks
{
    /// <summary>Ground an agent can dig field works in (PLACEHOLDER): clear or wooded, firm ground. Not water, walls, buildings,
    /// marsh, fords, mountain rock, or existing works.</summary>
    public static bool Diggable(TerrainClass c) => c is TerrainClass.Road or TerrainClass.Open or TerrainClass.Forest or TerrainClass.Jungle or TerrainClass.Hills;

    /// <summary>The works (ppm of complete works) of an agent entrenching since <paramref name="sinceMs"/>, at <paramref name="nowMs"/>:
    /// min(complete, floor(rate × (now − since) ÷ year)) — the exact integral of the linear rate over the elapsed time, through T1's
    /// exact integrator (RateIntegrator.IntegrateExact with a zero carry). Any partition of the elapsed time into sub-steps gives the
    /// same value (the integrator's invariant; tested), so the works do not depend on where turn or pulse boundaries fall. 0 when
    /// <paramref name="sinceMs"/> &lt; 0 (not entrenching) or now &lt; since.</summary>
    public static long WorksAt(long sinceMs, long nowMs, long ratePpmPerYear)
    {
        if (ratePpmPerYear < 0) throw new ArgumentOutOfRangeException(nameof(ratePpmPerYear));
        if (sinceMs < 0 || nowMs <= sinceMs || ratePpmPerYear == 0) return 0;
        long dt = nowMs - sinceMs;
        // The works are complete from the least dt with floor(rate × dt ÷ year) >= full, i.e. dt >= ceil(full × year ÷ rate); from
        // there on the value is the cap, so the integration never runs on an elapsed time longer than the build itself.
        Int128 toFull = ((Int128)ConcealmentRules.WorksFullPpm * SimInstant.MsPerYear + ratePpmPerYear - 1) / ratePpmPerYear;
        if (dt >= toFull) return ConcealmentRules.WorksFullPpm;
        var carry = ExactFraction.Zero;
        return RateIntegrator.IntegrateExact(ratePpmPerYear, dt, SimInstant.MsPerYear, ref carry);
    }

    /// <summary>What <see cref="Realize"/> placed: cover cells (Earthwork) and obstacle cells (Ditch).</summary>
    public readonly record struct Placement(int CoverCells, int ObstacleCells);

    /// <summary>
    /// The battle grid with the defender's field works: a COPY of <paramref name="grid"/> where, if <paramref name="worksPpm"/> reaches
    /// the cover threshold, every diggable cell under a ground formation of <paramref name="side"/> becomes Earthwork, and, if it
    /// reaches the obstacle threshold, every diggable cell one cell IN FRONT of such a cover cell (toward the enemy: away from the
    /// side's exit edge) that is not itself a cover cell becomes Ditch. Heights are kept. The cells under a formation are those of its
    /// entities' start positions (explicit placements, or the generated slots the battle uses). Cells are visited in ascending
    /// formation id, then entity slot; the result does not depend on the formations' listing order (a set of cells; tested).
    /// </summary>
    public static TerrainGrid Realize(BattleSetup setup, int side, long worksPpm, ConcealmentRules rules, out Placement placed)
    {
        rules.Validate();
        if (side != 0 && side != 1) throw new ArgumentOutOfRangeException(nameof(side));
        var g = setup.Grid;
        var copy = new TerrainGrid(g.Width, g.Height, g.CellSizeMm);
        for (int y = 0; y < g.Height; y++) for (int x = 0; x < g.Width; x++) copy.Set(x, y, g.ClassAt(x, y), g.HeightAt(x, y));
        placed = new Placement(0, 0);
        if (worksPpm < rules.CoverThresholdPpm) return copy;
        var exit = setup.Sides[side].Exit;
        var (fx, fy) = exit switch { ExitEdge.West => (1, 0), ExitEdge.East => (-1, 0), ExitEdge.North => (0, 1), _ => (0, -1) };
        var specs = (FormationSpec[])setup.Formations.Clone();
        Array.Sort(specs, static (a, b) => a.Id.CompareTo(b.Id));
        var cover = new bool[g.CellCount];
        foreach (var f in specs)
        {
            if (f.Side != side || f.Domain != Domain.Land || f.EmbarkedOn is not null) continue;
            int n = f.Entities?.Length ?? f.Count;
            for (int k = 0; k < n; k++)
            {
                Vec2L p = f.Entities is not null ? f.Entities[k].Pos : f.Anchor + TacticalBattle.SlotOffsetFor(setup.Rules, exit, f.Count, k);
                var c = g.CellOf(p);
                if (!g.InBounds(c.X, c.Y)) continue;
                int i = g.Index(c.X, c.Y);
                if (cover[i] || !Diggable(g.ClassAtIndex(i))) continue;
                cover[i] = true;
            }
        }
        int nc = 0, no = 0;
        for (int i = 0; i < cover.Length; i++)
        {
            if (!cover[i]) continue;
            copy.SetClass(g.IndexX(i), g.IndexY(i), TerrainClass.Earthwork);
            nc++;
        }
        if (worksPpm >= rules.ObstacleThresholdPpm)
            for (int i = 0; i < cover.Length; i++)
            {
                if (!cover[i]) continue;
                long x = g.IndexX(i) + fx, y = g.IndexY(i) + fy;
                if (!g.InBounds(x, y)) continue;
                int j = g.Index(x, y);
                if (cover[j] || !Diggable(copy.ClassAtIndex(j))) continue;
                copy.SetClass(x, y, TerrainClass.Ditch);
                no++;
            }
        placed = new Placement(nc, no);
        return copy;
    }
}

/// <summary>
/// [AP] The war-pulse ENGAGEMENT → battle hand-off of lane AP (a stand-in for M7.15's battle creation; docs/m7-plan/
/// concealment-actions-proposal.md §5): from an accepted Attack (<see cref="EngagementRecord"/>) and the battlefield the caller lays
/// out (formations, terrain, thirds), the battle setup the resolvers receive —
///   Defender = the target's side (READING AP-1 (a): the side the engagement names as attacked);
///   Ambusher = the attacker's side iff the engagement was an AMBUSH launched from concealment, else -1 (DA7);
///   the defender's field works realized on the grid (<see cref="FieldWorks.Realize"/>) from the works it had at the instant;
///   the ambusher's camouflage carried in as lane N's personal concealment of its formations' profiles (× CamouflageFactor), the
///   detection mechanism both resolvers already apply (lifted after a volley, as for snipers).
/// A hide is not carried in: a still formation in concealing terrain already gets the tactical stationary-concealment factor in
/// both resolvers. Nothing else changes; every number stays the caller's.
/// </summary>
public static class EngagementBattle
{
    public static BattleSetup Build(BattleSetup field, EngagementRecord e, int attackerSide, ConcealmentRules rules, out FieldWorks.Placement placed)
    {
        if (attackerSide != 0 && attackerSide != 1) throw new ArgumentOutOfRangeException(nameof(attackerSide));
        rules.Validate();
        int defender = 1 - attackerSide;
        var grid = FieldWorks.Realize(field, defender, e.TargetWorksPpm, rules, out placed);
        var forms = (FormationSpec[])field.Formations.Clone();
        if (e.Ambush && (e.AttackerStates & ConcealKind.Camouflaged) != 0)
            for (int i = 0; i < forms.Length; i++)
                if (forms[i].Side == attackerSide)
                    forms[i] = forms[i] with { Profile = forms[i].Profile with { PersonalConcealment = forms[i].Profile.PersonalConcealment.Mul(rules.CamouflageFactor) } };
        return field with { Grid = grid, Formations = forms, Defender = defender, Ambusher = e.Ambush ? attackerSide : -1 };
    }
}

/// <summary>
/// [AP] Canonical little-endian fixed-length encoding of lane AP's serialized rows (CLAUDE.md: every new serialized row type ships a
/// POPULATED-table test with the exact ExpectedLength, a bit-exact round trip and hash equality): [version u8] then three tables, each
/// [count i32] rows — agent states (<see cref="ConcealmentStateRow"/>, 29 bytes), changes (<see cref="ConcealmentChange"/>, 23 bytes),
/// engagements (<see cref="EngagementRecord"/>, 46 bytes).
/// </summary>
public static class ConcealmentCodec
{
    public const byte Version = 1;
    public const int StateRowBytes = 4 + 1 + 8 * 3;                 // 29
    public const int ChangeRowBytes = 4 + 4 + 8 + 4 + 1 + 1 + 1;    // 23
    public const int EngagementRowBytes = 4 + 8 + 4 * 4 + 1 + 1 + 8 + 8;   // 46

    public static int ExpectedLength(int states, int changes, int engagements)
        => checked(1 + 4 + states * StateRowBytes + 4 + changes * ChangeRowBytes + 4 + engagements * EngagementRowBytes);

    public static byte[] Encode(IReadOnlyList<ConcealmentStateRow> states, IReadOnlyList<ConcealmentChange> changes, IReadOnlyList<EngagementRecord> engagements)
    {
        var b = new byte[ExpectedLength(states.Count, changes.Count, engagements.Count)];
        int at = 0;
        b[at++] = Version;
        void I32(int v) { BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v); at += 4; }
        void I64(long v) { BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), v); at += 8; }
        void U8(byte v) => b[at++] = v;
        I32(states.Count);
        foreach (var s in states) { I32(s.AgentId); U8((byte)s.Flags); I64(s.HiddenSinceMs); I64(s.CamouflagedSinceMs); I64(s.EntrenchSinceMs); }
        I32(changes.Count);
        foreach (var c in changes) { I32(c.Seq); I32(c.LogIndex); I64(c.AtMs); I32(c.AgentId); U8((byte)c.Kind); U8(c.Begin ? (byte)1 : (byte)0); U8((byte)c.Cause); }
        I32(engagements.Count);
        foreach (var e in engagements)
        {
            I32(e.LogIndex); I64(e.AtMs); I32(e.Attacker); I32(e.Target); I32(e.AttackerPolity); I32(e.TargetPolity);
            U8(e.Ambush ? (byte)1 : (byte)0); U8((byte)e.AttackerStates); I64(e.TargetWorksPpm); I64(e.TargetCell);
        }
        if (at != b.Length) throw new InvalidOperationException("encoded length mismatch");
        return b;
    }

    public static (ConcealmentStateRow[] States, ConcealmentChange[] Changes, EngagementRecord[] Engagements) Decode(byte[] b)
    {
        if (b.Length < 1 + 4 || b[0] != Version) throw new FormatException("concealment table header");
        int at = 1;
        int I32() { if (at + 4 > b.Length) throw new FormatException("concealment table length"); int v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4; return v; }
        long I64() { if (at + 8 > b.Length) throw new FormatException("concealment table length"); long v = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(at)); at += 8; return v; }
        byte U8() { if (at + 1 > b.Length) throw new FormatException("concealment table length"); return b[at++]; }
        bool Bool() { byte v = U8(); if (v > 1) throw new FormatException("concealment flag byte"); return v == 1; }
        int Count(int rowBytes) { int n = I32(); if (n < 0 || n > (b.Length - at) / rowBytes) throw new FormatException("concealment table count"); return n; }
        var states = new ConcealmentStateRow[Count(StateRowBytes)];
        for (int i = 0; i < states.Length; i++) { int id = I32(); var fl = (ConcealKind)U8(); states[i] = new ConcealmentStateRow(id, fl, I64(), I64(), I64()); }
        var changes = new ConcealmentChange[Count(ChangeRowBytes)];
        for (int i = 0; i < changes.Length; i++)
        {
            int seq = I32(), log = I32(); long t = I64(); int agent = I32(); var kind = (ConcealKind)U8(); bool begin = Bool(); var cause = (ConcealEnd)U8();
            changes[i] = new ConcealmentChange(seq, log, t, agent, kind, begin, cause);
        }
        var eng = new EngagementRecord[Count(EngagementRowBytes)];
        for (int i = 0; i < eng.Length; i++)
        {
            int log = I32(); long t = I64(); int a = I32(), tg = I32(), ap = I32(), tp = I32(); bool amb = Bool(); var st = (ConcealKind)U8();
            eng[i] = new EngagementRecord(log, t, a, tg, ap, tp, amb, st, I64(), I64());
        }
        if (at != b.Length) throw new FormatException("concealment table trailing bytes");
        return (states, changes, eng);
    }
}
