using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>Ram planner mode of one hull (D-081). Stable codes (state hash).</summary>
public enum RamMode : byte
{
    /// <summary>Closing: an attack run at the target when the run-up and the angle allow it.</summary>
    Close = 0,
    /// <summary>Opening the range: away from the hull just struck, or to the chosen target's beam station.</summary>
    Open = 1,
}

/// <summary>
/// RAM LANE — Director ruling D-081 (2026-11-05) in lane N's naval module.
///
/// RM1 (once per pair). Each hull keeps the sorted ids of the hulls it has rammed (battle state, hashed, replay-safe). A bow
/// contact with a hull already in that list is never a ram again, whatever the speed: it is a collision. Another hull may
/// still ram that target (reading RM-R: "once" is per (rammer, target) pair).
///
/// RM2 (steer away, ram another). After any bow contact the rammer opens the range from the hull it struck; its ram target
/// is the nearest TRACKED, on-field hull of the directive's target formation that it has NOT rammed, by the composite key
/// (distance², id) (<see cref="RamPhysics.ArgMinTarget"/>). With no such hull the ram planner yields and the hull keeps the
/// enemy under its other weapons (lane N's stand-off rule).
///
/// RM3 (run-up to ramming speed). A contact is a ram only when the closing velocity component normal to the target's side
/// (v_n) is &gt;= the class's ramming speed ([r2 RAM2-V1, V2]; round 1 gated on the centre-line closing speed). Only the
/// rammer's own way supplies v_n, and speed comes only from the movement integration (acceleration per step), so the run-up
/// distance follows from the kinematics (<see cref="RamPhysics.RunUpGapMm"/>). The planner presses in only when a straight
/// full-power run from the current speed brings the own way to the speed whose normal component on the target's side is
/// ramming speed (<see cref="RamPhysics.RequiredWayMmPerS"/>) before contact; otherwise it opens the range to the target's
/// beam station, at D_open = run-up from rest + the full-speed turning-circle diameter, and turns in.
///
/// RM4 (realism; mechanisms inside the equations, <see cref="RamPhysics"/>): damage from the kinetic energy of the normal
/// closing component (v_n²); glancing blows deflect (impact angle below the glance angle); the
/// rammer's bow takes a share of the energy on every contact; after a breach the beak is embedded to a depth that grows
/// with the energy and the rammer must back water to free it — t = sqrt(2 depth / a_back) — during which it is held at speed
/// 0 and cannot manoeuvre, so its evasion term is 0 (exposed); people killed aboard the struck hull go through the personnel
/// ledger (the existing DomainHit path). Lane N's 30 s ram cooldown is REMOVED: the run-up and the free time are the
/// physical mechanisms it approximated (D-081 RM-R).
///
/// The ram PLANNER (run-up, steering away, target choice) is the Attack directive's. A Board directive keeps lane N's rule
/// that its approach may ram (lane N: at &gt;= max/2 with a 30 s cooldown), now under the D-081 rules: a ram only at closing
/// speed &gt;= ramming speed, once per (rammer, target) pair; a boarding contact below that is the grappling contact itself,
/// not a collision. (Verification round 1, RAM-V7: the lane's "Board no longer rams" was an undecided behaviour change; it
/// is withdrawn and stays proposal P2 for the Director.)
///
/// Verification round 1 (2026-11-05) also added: an impact needs the two hulls to have come together in the step (RAM-V1 d);
/// in Open mode a hull touching other hulls first clears them all, on the bisector of the widest free sector (RAM-V1 a, c);
/// the run-up check of an attack run is made in the target's frame, so the target's own motion along the line of sight
/// counts (RAM-V3); a rammer's own bow damage credits no source (RAM-V10).
/// </summary>
public sealed partial class TacticalBattle
{
    /// <summary>Per-hull ram state (index = domain entity index). Only ram-capable hulls ever change it.</summary>
    private sealed class RamState
    {
        public long[] Rammed = Array.Empty<long>();          // sorted ids of hulls this hull has rammed (RM1)
        public long LockedUntilMs = long.MinValue;           // beak embedded: backing water until this instant
        public RamMode Mode = RamMode.Close;
        public long Ref = -1;                                // hull being opened from (-1: none)
        public long Target = -1;                             // current ram target (inspection; recomputed every step)
        public long WayMmPerS;                               // way speed of the last movement step (before truncation)
        // [r1 V1 d] Position at the start of the last movement step. Written by the movement phase and read only by the Fire
        // phase of the SAME instant (Detect is scheduled by Step), so it never carries state across instants: not hashed.
        public Vec2L FromPos;
    }

    private RamState[] _rs = Array.Empty<RamState>();
    private bool _anyRam;

    private void RamInit()
    {
        _rs = new RamState[_d.Length];
        for (int i = 0; i < _d.Length; i++)
        {
            _rs[i] = new RamState { FromPos = _d[i].Pos };
            if (_d[i].V is { RamDamage: > 0 }) _anyRam = true;
        }
    }

    private static bool CanRam(DEnt d) => d.V is { RamDamage: > 0 };
    private bool RamLocked(int i, long t) => t < _rs[i].LockedUntilMs;
    private bool HasRammedIdx(int i, long targetId) => Array.BinarySearch(_rs[i].Rammed, targetId) >= 0;

    private void AddRammed(int i, long targetId)
    {
        var a = _rs[i].Rammed;
        int k = Array.BinarySearch(a, targetId);
        if (k >= 0) return;
        k = ~k;
        var n = new long[a.Length + 1];
        Array.Copy(a, 0, n, 0, k);
        n[k] = targetId;
        Array.Copy(a, k, n, k + 1, a.Length - k);
        _rs[i].Rammed = n;
    }

    private long BackingAccel(DEnt d) => Math.Max(1, FixedMath.MulDivFloor(d.V!.AccelMmPerS2, _dr.BackingAccelPermille, 1000));

    /// <summary>Surface gap (mm) between two hulls.</summary>
    private static long HullGap(DEnt a, DEnt b) => Vec2L.Distance(a.Pos, b.Pos) - a.V!.RadiusMm - b.V!.RadiusMm;

    /// <summary>D_open = run-up gap from rest + the turning-circle diameter at the hull's maximum speed.</summary>
    private long RamOpenGap(DEnt d)
    {
        long vmax = VesselMaxSpeed(d);
        long run = RamPhysics.RunUpGapMm(0, d.V!.RamSpeedMmPerS, vmax, d.V.AccelMmPerS2, _r.StepMs, d.V.RamLengthMm);
        if (run == long.MaxValue) return long.MaxValue;
        return checked(run + RamPhysics.TurnDiameterMm(vmax, d.V.TurnRatePerS));
    }

    /// <summary>Nearest tracked on-field hull of the formation not yet rammed by hull i, key (distance², id); -1 if none.</summary>
    private int ChooseRamTarget(int i, long formationId)
    {
        var d = _d[i];
        int df = FindDForm(formationId);
        if (df < 0 || _df[df].Dom != Domain.Naval) return -1;
        var mem = _df[df].Members;
        Span<long> ids = mem.Length <= 64 ? stackalloc long[mem.Length] : new long[mem.Length];
        Span<Int128> dsq = mem.Length <= 64 ? stackalloc Int128[mem.Length] : new Int128[mem.Length];
        Span<bool> ok = mem.Length <= 64 ? stackalloc bool[mem.Length] : new bool[mem.Length];
        var k = _dk[d.Side];
        for (int m = 0; m < mem.Length; m++)
        {
            var x = _d[mem[m]];
            ids[m] = x.Id;
            dsq[m] = Vec2L.DistanceSq(d.Pos, x.Pos);
            ok[m] = x.Side != d.Side && x.OnField && k.Tracked[mem[m]] && !HasRammedIdx(i, x.Id);
        }
        int b = RamPhysics.ArgMinTarget(ids, dsq, ok);
        return b < 0 ? -1 : mem[b];
    }

    /// <summary>
    /// The ram directive's intent for a ram-capable hull under Attack on a naval formation. Returns false when the ram planner
    /// has nothing to do (no unrammed tracked hull, or the hull can no longer reach ramming speed): the caller then applies
    /// lane N's stand-off rule with the hull's other weapons.
    /// </summary>
    private bool RamIntent(DEnt d, long vmax, long formationId, out int heading, out long speed)
    {
        heading = d.Heading; speed = 0;
        int i = DIndex(d.Id);
        var rs = _rs[i];
        long t = _s.NowMs;
        if (RamLocked(i, t)) return true;                                     // backing water: held, no way on
        int ti = ChooseRamTarget(i, formationId);
        rs.Target = ti < 0 ? -1 : _d[ti].Id;
        if (ti < 0 || vmax < d.V!.RamSpeedMmPerS) { rs.Mode = RamMode.Close; rs.Ref = -1; return false; }
        var tg = _d[ti];
        long open = RamOpenGap(d);
        // [r1 V1 a, c] Clearing: in Open mode a hull that touches other hulls (within the pivot reach) first gets clear of ALL
        // of them, not only of the hull it is opening from or turning for — otherwise steering away from one hull can press it
        // into another (V1 a) and steering for the target's beam station can keep the target forward of the beam for ever (V1 c).
        if (rs.Mode == RamMode.Open && ClearOfHulls(d, vmax, out heading, out speed)) return true;
        if (rs.Mode == RamMode.Open && rs.Ref >= 0 && rs.Ref != tg.Id)
        {
            int ri = DIndex(rs.Ref);
            if (ri >= 0 && _d[ri].OnField && _d[ri].Dom == Domain.Naval && HullGap(d, _d[ri]) < open)
            {
                var rp = _d[ri].Pos;
                heading = rp == d.Pos ? d.Heading : FixedTrig.Atan2(d.Pos.Y - rp.Y, d.Pos.X - rp.X);   // steer away from it
                speed = vmax;                                                   // beyond the pivot reach (cleared above)
                return true;
            }
            rs.Mode = RamMode.Close; rs.Ref = -1;                            // clear of it: go for the target
        }
        // Fouled: the last move was cut short by a hull forward of the beam that the beak is not striking (a friend, another
        // enemy, or the target outside the bow arc): sheer off from it instead of pressing against it — by the clearing rule
        // (the fouling hull lies within the beak's reach, so inside the pivot reach).
        if (rs.Mode == RamMode.Close && d.Speed < rs.WayMmPerS)
        {
            int fo = FouledBy(d, tg);
            if (fo >= 0)
            {
                rs.Mode = RamMode.Open; rs.Ref = _d[fo].Id;
                if (ClearOfHulls(d, vmax, out heading, out speed)) return true;
            }
        }
        long gap = HullGap(d, tg);
        int phi = RamPhysics.ApproachAngle(tg.Pos, tg.Heading, d.Pos);
        if (rs.Mode == RamMode.Open)
        {
            if (gap >= open && phi >= _dr.RamAttackAngle) { rs.Mode = RamMode.Close; rs.Ref = -1; }
            else { BeamStation(d, tg, vmax, open, out heading, out speed); rs.Ref = tg.Id; return true; }
        }
        // Close: abort when the blow would glance or the run-up is too short; otherwise the attack run.
        // [r1 V3] The run-up is integrated in the TARGET's frame: u = the target's speed along the line of sight (+ receding),
        // assumed constant over the run; the range closes at own component - u, at most vmax - u (&lt;= 0: never closes).
        // [r2 RAM2-V1] The run must bring the OWN way to s_req = ceil(v_ram / |sin a|), a = the run's angle to the target's
        // keel — the way whose normal component on the target's side is ramming speed (the ram gate) — i.e. the closing
        // speed to s_req - u. Round 1 aimed the closing speed at v_ram, which an oblique run can reach with v_n &lt; v_ram.
        int bearing = FixedTrig.Atan2(tg.Pos.Y - d.Pos.Y, tg.Pos.X - d.Pos.X);
        long u = FixedTrig.ScaleCos(tg.Speed, FixedTrig.Diff(bearing, tg.Heading));
        long c0 = Math.Max(0, FixedTrig.ScaleCos(d.Speed, FixedTrig.Diff(bearing, d.Heading)) - u);
        long sReq = RamPhysics.RequiredWayMmPerS(d.V.RamSpeedMmPerS, bearing, tg.Heading);
        long need = sReq > vmax || vmax - u <= 0 ? long.MaxValue
                  : RamPhysics.RunUpGapMm(c0, sReq - u, vmax - u, d.V.AccelMmPerS2, _r.StepMs, d.V.RamLengthMm);
        if (phi < _dr.RamGlanceAngle || gap < need)
        {
            rs.Mode = RamMode.Open; rs.Ref = tg.Id;
            BeamStation(d, tg, vmax, open, out heading, out speed);
            if (PivotNear(d, tg, vmax)) speed = 0;
            return true;
        }
        heading = bearing; speed = vmax;
        return true;
    }

    /// <summary>
    /// [r1 V1] Clearing rule. The bearings to every on-field hull (either side) whose surface lies within the pivot reach (the
    /// beak's reach plus one step at a quarter of the maximum speed, <see cref="PivotNear"/>) — none: false. Otherwise the escape
    /// heading is the bisector of the WIDEST free sector between those bearings (one hull: directly away from it; two hulls
    /// touching bow and quarter: between them on the open side; first sector in ascending bearing order on a tie), and the hull
    /// pivots in place (speed 0: one bank rows ahead, the other backs) while any of them lies forward of its beam — making
    /// way would close on it — then makes way. Boxed in (no sector wider than a half turn) it holds there, pivoting, until a
    /// neighbour moves.
    /// </summary>
    private bool ClearOfHulls(DEnt d, long vmax, out int heading, out long speed)
    {
        heading = d.Heading; speed = 0;
        long reach = checked(d.V!.RamLengthMm + HeadingMotion.Distance(vmax / 4, _r.StepMs));
        int n = 0;
        int[]? brg = null;
        for (int j = 0; j < _d.Length; j++)
        {
            var x = _d[j];
            if (x == d || x.Dom != Domain.Naval || !x.OnField || x.Pos == d.Pos) continue;
            if (HullGap(d, x) > reach) continue;
            brg ??= new int[_d.Length];
            brg[n++] = FixedTrig.Atan2(x.Pos.Y - d.Pos.Y, x.Pos.X - d.Pos.X);
        }
        if (n == 0) return false;
        Array.Sort(brg!, 0, n);
        long best = -1; int bk = 0;
        for (int k = 0; k < n; k++)
        {
            long next = k + 1 < n ? brg![k + 1] : (long)brg![0] + FixedTrig.Turn;
            long g = next - brg[k];
            if (g > best) { best = g; bk = k; }
        }
        heading = FixedTrig.Norm(brg![bk] + best / 2);
        bool forward = false;
        for (int k = 0; k < n; k++) if (FixedTrig.Separation(brg[k], d.Heading) < FixedTrig.Quarter) forward = true;
        speed = forward ? 0 : vmax;
        return true;
    }

    /// <summary>Nearest on-field hull (either side) within the beak's reach and forward of the beam, except the target when it
    /// lies inside ±30° of the bow (that contact is a strike, <see cref="RamContacts"/>); -1 if none. Key (distance², id).</summary>
    private int FouledBy(DEnt d, DEnt tg)
    {
        int best = -1; Int128 bd = 0;
        for (int j = 0; j < _d.Length; j++)
        {
            var x = _d[j];
            if (x == d || x.Dom != Domain.Naval || !x.OnField) continue;
            if (HullGap(d, x) > d.V!.RamLengthMm) continue;
            int bearing = FixedTrig.Atan2(x.Pos.Y - d.Pos.Y, x.Pos.X - d.Pos.X);
            int off = FixedTrig.Separation(bearing, d.Heading);
            if (off >= FixedTrig.Quarter) continue;
            if (x == tg && off <= FixedTrig.FromDegrees(30)) continue;
            Int128 dd = Vec2L.DistanceSq(d.Pos, x.Pos);
            if (best < 0 || dd < bd) { best = j; bd = dd; }
        }
        return best;
    }

    /// <summary>
    /// A hull turning away from a hull it (nearly) touches pivots in place — one bank rows ahead, the other backs water —
    /// instead of making way into it: true while the other hull lies forward of the beam and within the beak's reach plus one
    /// step at the hard-turn speed (a quarter of the maximum, lane N's turn rule).
    /// </summary>
    private bool PivotNear(DEnt d, DEnt from, long vmax)
    {
        if (from.Pos == d.Pos) return false;
        int bearing = FixedTrig.Atan2(from.Pos.Y - d.Pos.Y, from.Pos.X - d.Pos.X);
        if (FixedTrig.Separation(bearing, d.Heading) >= FixedTrig.Quarter) return false;
        return HullGap(d, from) <= checked(d.V!.RamLengthMm + HeadingMotion.Distance(vmax / 4, _r.StepMs));
    }

    /// <summary>Steer to the target's beam station on the rammer's side: D_open beyond the surface plus one step of way.</summary>
    private void BeamStation(DEnt d, DEnt tg, long vmax, long open, out int heading, out long speed)
    {
        int beam = RamPhysics.BeamToward(tg.Pos, tg.Heading, d.Pos);
        long r = checked(d.V!.RadiusMm + tg.V!.RadiusMm + open + HeadingMotion.Distance(vmax, _r.StepMs));
        Vec2L s = tg.Pos + HeadingMotion.Offset(beam, r);
        heading = s == d.Pos ? d.Heading : FixedTrig.Atan2(s.Y - d.Pos.Y, s.X - d.Pos.X);
        speed = vmax;
    }

    /// <summary>Movement hook: the way speed of this step (before the separation step truncates the move) and the position
    /// the hull starts the step from.</summary>
    private void RamRecordWay(int i, long way, Vec2L from) { _rs[i].WayMmPerS = way; _rs[i].FromPos = from; }

    /// <summary>
    /// Fire-phase hook: a ram-capable hull under Attack (or Board, see the class summary), not backing water, whose beak
    /// touches a hull of the target formation inside ±30° of its bow (nearest by (distance², id)), the two hulls having come
    /// together in this step, with the range closing. Classified here on the pre-impact world (simultaneity: every contact at
    /// an instant sees the same speeds), applied by <see cref="RamStrike"/> / <see cref="RamCollisionStrike"/>.
    /// </summary>
    private void RamContacts(long t, int i, DEnt d, DForm f)
    {
        if (!CanRam(d) || (f.Dir != OrderKind.Attack && f.Dir != OrderKind.Board) || RamLocked(i, t)) return;
        int b = BeakContact(d, f.DirTarget);
        if (b < 0) return;
        var x = _d[b];
        // [r1 V1 d] An impact needs the hulls to have come together in this step. Pressed together (a move blocked by the
        // separation step), a hull still has a commanded way but neither closes on the other: no impact.
        if (Vec2L.DistanceSq(d.Pos, x.Pos) >= Vec2L.DistanceSq(_rs[i].FromPos, _rs[b].FromPos)) return;
        var imp = RamPhysics.Classify(d.Pos, d.Heading, _rs[i].WayMmPerS, x.Pos, x.Heading, _rs[b].WayMmPerS, d.V!.RamSpeedMmPerS, _dr.RamGlanceAngle);
        if (imp.ClosingMmPerS <= 0) return;                                   // touching but not closing: no impact
        bool ram = imp.IsRam && !HasRammedIdx(i, x.Id);                       // RM1: never twice on the same hull
        if (ram)
            _s.Schedule(t, EventClass.Fire, 2, d.Id, DomainKinds.Ram, new EntityRef(x.Id, x.Gen), new EntityRef(d.Id, d.Gen),
                        (long)(imp.Breach ? RamOutcome.Breach : RamOutcome.Glance), imp.NormalMmPerS, imp.ClosingMmPerS);
        else if (f.Dir == OrderKind.Attack)                                   // a boarder's slow contact is the grapple itself
            _s.Schedule(t, EventClass.Fire, 2, d.Id, DomainKinds.RamCollision, new EntityRef(x.Id, x.Gen), new EntityRef(d.Id, d.Gen),
                        0, imp.NormalMmPerS, imp.ClosingMmPerS);
    }

    /// <summary>Nearest enemy hull of the formation whose surface is within the beak's reach, inside ±30° of the bow.</summary>
    private int BeakContact(DEnt d, long formationId)
    {
        int best = -1; Int128 bd = 0;
        for (int j = 0; j < _d.Length; j++)
        {
            var x = _d[j];
            if (x.Side == d.Side || x.Dom != Domain.Naval || !x.OnField) continue;
            if (_df[x.F].Id != formationId) continue;
            long reach = d.V!.RadiusMm + x.V!.RadiusMm + d.V.RamLengthMm;
            Int128 dd = Vec2L.DistanceSq(d.Pos, x.Pos);
            if (dd > (Int128)reach * reach) continue;
            int bearing = FixedTrig.Atan2(x.Pos.Y - d.Pos.Y, x.Pos.X - d.Pos.X);
            if (FixedTrig.Separation(bearing, d.Heading) > FixedTrig.FromDegrees(30)) continue;
            if (best < 0 || dd < bd) { best = j; bd = dd; }
        }
        return best;
    }

    /// <summary>A ram (D-081): records the pair, then breach (damage, crew, embedded beak) or glance; bow damage either way.</summary>
    private void RamStrike(long t, in ScheduledEvent ev)
    {
        int ai = DIndex(ev.Source.Id);
        var a = _d[ai];
        var v = a.V!;
        long vn = ev.B;
        AddRammed(ai, ev.Target.Id);
        a.Primary = true; _df[a.F].Attacks++;
        Fought(t);                                                       // [MOR3] D-085 DD1-R2 (a): a ram, breach or glance
        if ((RamOutcome)ev.A == RamOutcome.Breach)
        {
            long hull = Math.Max(1, RamPhysics.EnergyScaled(v.RamDamage, vn, v.MaxSpeedMmPerS));
            long crew = RamPhysics.EnergyScaled(v.RamCrewDamage, vn, v.MaxSpeedMmPerS);
            _s.Schedule(t, EventClass.Damage, 0, ev.Target.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 1, hull, crew);
            long depth = Math.Max(1, Math.Min(v.RamLengthMm, RamPhysics.EnergyScaled(v.RamLengthMm, vn, v.MaxSpeedMmPerS)));
            _rs[ai].LockedUntilMs = checked(t + RamPhysics.FreeRamMs(depth, BackingAccel(a)));
            a.Speed = 0;
        }
        else
        {
            a.Speed = Math.Max(0, _rs[ai].WayMmPerS - vn);                    // the beak skids: the normal way is lost
        }
        RamBow(t, a, ev, vn);
        _rs[ai].Mode = RamMode.Open; _rs[ai].Ref = ev.Target.Id;
    }

    /// <summary>A bow contact that is not a ram (normal closing speed below ramming speed, or a hull already rammed — RM1): no
    /// breach; the rammer loses the normal component of its way; its bow takes the energy share (at most 1 below ramming speed).
    /// Rationale [r1 V6]: below the minimum damaging impact speed the struck planking is not broken (Itzhack, Cvikel &amp; Me-Bar
    /// 2024, the source of RamSpeedMmPerS); the arrest of the rammer's way is INFERRED, not sourced: the hulls do not
    /// interpenetrate and no hull displacement is modelled (P6), so the normal way is absorbed by the struck hull. [r2] The code
    /// now applies exactly that — speed max(0, way − v_n), the glance's rule (round 1 zeroed the whole way, which equals this
    /// only for a perpendicular contact; with the RAM2-V1 gate a full-way skim at a shallow angle is a collision and would
    /// otherwise stop dead). A contact on a hull ALREADY rammed leaves the struck hull undamaged whatever the speed (strict RM1,
    /// reading RAM-R5, flagged for the Director in H-RAM.md: RM1 forbids a second ram but does not rule what a full-speed bow
    /// contact does to that hull).
    /// [MOR3] D-085 DD1: not an attack, so not fighting (READING MOR3-R2 (a), morale proposal §12.2): the engine counts no attack, makes
    /// no hit roll and does the struck hull no damage here (only the rammer's own bow is damaged), as the attack count and the
    /// inactivity rule's progress already read it. Alternative (b): a ram attempt resolved as a contact counts as an attack.</summary>
    private void RamCollisionStrike(long t, in ScheduledEvent ev)
    {
        int ai = DIndex(ev.Source.Id);
        var a = _d[ai];
        a.Speed = Math.Max(0, _rs[ai].WayMmPerS - ev.B);                      // [r2] the normal way is absorbed
        RamBow(t, a, ev, ev.B);
        _rs[ai].Mode = RamMode.Open; _rs[ai].Ref = ev.Target.Id;
    }

    /// <summary>DomainHit effect kind of a rammer's own bow damage: vessel hull only (handled like kind 1 with no people).</summary>
    public const long BowHitKind = 3;

    /// <summary>Damage to the rammer's own bow through the DomainHit path. The event's Source is the struck hull (trace
    /// information) but no source is credited with it ([r1 V10]: the bow damage is self-inflicted; see DomainHit).</summary>
    private void RamBow(long t, DEnt a, in ScheduledEvent ev, long vn)
    {
        long bow = RamPhysics.EnergyScaledPermille(a.V!.RamDamage, a.V.RamBowDamagePermille, vn, a.V.MaxSpeedMmPerS);
        if (bow > 0) _s.Schedule(t, EventClass.Damage, 0, a.Id, DomainKinds.DomainHit, ev.Source, ev.Target, BowHitKind, bow, 0);
    }

    /// <summary>Evasion of a hull as a target: 0 while it is backing its beak out of a breach (it cannot manoeuvre).</summary>
    private long VesselEvasion(DEnt x, long t) => CanRam(x) && RamLocked(DIndex(x.Id), t) ? 0 : x.V!.Evasion;

    /// <summary>State hash slot that replaced lane N's RamReadyMs: long.MinValue for every hull that never rammed, so battles
    /// without ram ships hash exactly as before.</summary>
    private long RamHashSlot(int i) => _rs[i].LockedUntilMs;

    /// <summary>Ram planner state of ram-capable hulls (appended after the domain state; empty without ram ships).</summary>
    private void AppendRamState(IncrementalHash h)
    {
        if (!_anyRam) return;
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        for (int i = 0; i < _d.Length; i++)
        {
            if (!CanRam(_d[i])) continue;
            var r = _rs[i];
            L(_d[i].Id); L((long)r.Mode); L(r.Ref); L(r.Target); L(r.WayMmPerS); L(r.Rammed.Length);
            foreach (var x in r.Rammed) L(x);
        }
    }

    // ------------------------------------------------------------------ inspection (tests)

    /// <summary>True when vessel rammerId has rammed vessel targetId (RM1 state).</summary>
    public bool HasRammed(long rammerId, long targetId) => HasRammedIdx(DIndexOrThrow(rammerId), targetId);
    /// <summary>Ids of the hulls a vessel has rammed, ascending.</summary>
    public long[] RammedBy(long rammerId) => (long[])_rs[DIndexOrThrow(rammerId)].Rammed.Clone();
    /// <summary>The ram planner's current target (-1: none).</summary>
    public long RamTargetOf(long vesselId) => _rs[DIndexOrThrow(vesselId)].Target;
    public RamMode RamModeOf(long vesselId) => _rs[DIndexOrThrow(vesselId)].Mode;
    /// <summary>Instant until which the vessel is backing its beak out of a breach (long.MinValue: never rammed).</summary>
    public long RamLockedUntilMs(long vesselId) => _rs[DIndexOrThrow(vesselId)].LockedUntilMs;
    /// <summary>The evasion term a shooter's hit roll uses against this vessel now (0 while it backs its beak out).</summary>
    public long EvasionOf(long vesselId) => VesselEvasion(_d[DIndexOrThrow(vesselId)], _s.NowMs);
    /// <summary>D_open of a vessel at its current crew: the gap it opens to before an attack run.</summary>
    public long RamOpenGapMm(long vesselId) => RamOpenGap(_d[DIndexOrThrow(vesselId)]);
    /// <summary>[r2] Way speed of the vessel's last movement step (the impact speed of a contact at this instant).</summary>
    public long RamWayMmPerS(long vesselId) => _rs[DIndexOrThrow(vesselId)].WayMmPerS;
    /// <summary>[r2 RAM2-V6] The hull of formation <paramref name="formationId"/> the vessel's beak would strike now (bow arc,
    /// beak reach, key (distance², id)); -1 if none. Geometry only (no directive, speed or closure check).</summary>
    public long BeakContactOf(long vesselId, long formationId)
    {
        int b = BeakContact(_d[DIndexOrThrow(vesselId)], formationId);
        return b < 0 ? -1 : _d[b].Id;
    }
    /// <summary>[r2 RAM2-V6] The hull fouling the vessel's run at <paramref name="targetId"/> now (within the beak's reach,
    /// forward of the beam, not the target inside the bow arc; key (distance², id)); -1 if none.</summary>
    public long FouledByOf(long vesselId, long targetId)
    {
        int f = FouledBy(_d[DIndexOrThrow(vesselId)], _d[DIndexOrThrow(targetId)]);
        return f < 0 ? -1 : _d[f].Id;
    }
}
