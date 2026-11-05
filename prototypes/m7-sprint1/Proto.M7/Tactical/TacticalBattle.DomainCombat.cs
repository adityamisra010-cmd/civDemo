using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// LANE N — knowledge, engagement and resolution across domains (combined-arms layer, m7-spec REV:430-433).
///
/// KNOWLEDGE. A side has ONE acquisition draw per (side, target, instant): the land rule. For a LAND target the land loop
/// draws first with its best land-observer chance p_land; this layer then offers the vessel/aircraft observers' best chance
/// p_x and draws with the SAME counter-RNG key, so the target is acquired iff u &lt; max(p_land, p_x): one draw against the
/// best observer of ANY domain (a crowd of observers still does not see better than its best one). Air observers look down:
/// clarity = the concealment of the target's own cell (canopy) — intervening terrain does not mask; vessel observers use the
/// land LOS from mast height. Exposure (moved / fired / in the open) and personal concealment apply as on land. Vessels and
/// aircraft as TARGETS: detection range = observer sight x target signature; LOS from land and sea; no concealment (sea and
/// sky do not hide); grappled hulls are in contact (automatic).
///
/// ENGAGEMENT (per weapon mount, ascending shooter id then mount index): ready (reload elapsed) and ammunition left →
/// eligible targets = TRACKED enemies whose domain the weapon can engage, within [min, max] range (slant range to an
/// airborne target, horizontal range to a ground or sea target), inside the mount's arc (vessels / aircraft), with LOS for direct fire;
/// area fire is refused when a friendly land entity is within blast radius + dispersion of the aim point ("danger close").
/// Limited-ammunition weapons fire only at the formation's designated target (directive target or bombard point).
/// Choice = minimal composite key (priority, distance², entity id), priority 0 = the designated target.
/// Direct fire: hit iff counter-RNG &lt; clamp(acc/(acc+def)) x range falloff. Area fire: impact = aim + uniform offset in a
/// square of half-width range x dispersion; every land entity of EITHER side within the blast radius R is affected with
/// probability (R - d)/R, d = distance from the impact (one counter-RNG draw per (shot, entity)); vessels likewise, d measured
/// to the hull's edge, for surface-capable weapons. All shots at an instant see the pre-damage world; all
/// damage lands before any loss is resolved (the land simultaneity rule).
/// </summary>
public sealed partial class TacticalBattle
{
    // ------------------------------------------------------------------ knowledge

    private void DomainPreDetect()
    {
        for (int s = 0; s < 2; s++) Array.Copy(_k[s].Tracked, _preTracked[s], _e.Length);
    }

    private Q32 ExposureAdjusted(Ent tg, Q32 clarity, long t)
    {
        bool exposed = tg.Moved || tg.RevealUntilMs >= t || clarity == Q32.One;
        Q32 p = exposed ? clarity : clarity.Mul(_r.StationaryConcealment);
        if (tg.P.PersonalConcealment != Q32.One && tg.RevealUntilMs < t) p = p.Mul(tg.P.PersonalConcealment);
        return p;
    }

    private void DomainKnowledge(long t)
    {
        if (_d.Length > 0) LandTargetsSeenByDomain(t);
        if (_d.Length > 0) DomainTargetsSeen(t);
    }

    private void LandTargetsSeenByDomain(long t)
    {
        for (int side = 0; side < 2; side++)
        {
            var k = _k[side];
            for (int ti = 0; ti < _e.Length; ti++)
            {
                var tg = _e[ti];
                if (tg.Side == side || !tg.OnField) continue;
                Q32 best = Q32.Zero; int bestObs = -1;
                for (int oi = 0; oi < _d.Length; oi++)
                {
                    var ob = _d[oi];
                    if (ob.Side != side || !ob.OnField) continue;
                    long sight = ob.V?.SightMm ?? ob.A!.SightMm;
                    if (Vec2L.DistanceSq(ob.Pos, tg.Pos) > (Int128)sight * sight) continue;
                    Q32 clarity;
                    if (ob.Dom == Domain.Air)
                    {
                        var c = _grid.CellOf(tg.Pos);
                        clarity = _conceal.Multiplier(_grid.ClassAt(c.X, c.Y));
                    }
                    else
                    {
                        var los = LineOfSight.Query(_grid, _conceal, ob.Pos, ob.V!.HeightMm, tg.Pos, _r.EyeHeightMm);
                        if (!los.Visible) continue;
                        clarity = los.Clarity;
                    }
                    if (clarity == Q32.Zero) continue;
                    Q32 p = ExposureAdjusted(tg, clarity, t);
                    if (bestObs < 0 || p > best) { best = p; bestObs = oi; }
                }
                if (bestObs < 0) continue;
                if (_preTracked[side][ti] || k.Tracked[ti])
                {
                    k.Tracked[ti] = true; k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t;
                    continue;
                }
                if (!CounterRng.Chance(_setup.Seed, TacticalStreams.Detection, tg.Id, t, side, 0, best)) continue;
                k.Tracked[ti] = true; k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t;
                if (!k.Ever[ti]) { k.Ever[ti] = true; _d[bestObs].Primary = true; }
            }
        }
    }

    private long TargetHeight(DEnt d) => d.Dom == Domain.Air ? d.A!.AltitudeMm : d.V!.HeightMm;
    private long Signature(DEnt d) => d.V?.SignaturePercent ?? d.A!.SignaturePercent;

    private void DomainTargetsSeen(long t)
    {
        long S = _grid.CellSizeMm;
        for (int side = 0; side < 2; side++)
        {
            var k = _dk[side];
            for (int ti = 0; ti < _d.Length; ti++)
            {
                var tg = _d[ti];
                if (tg.Side == side || !tg.OnField) continue;
                bool visible = false, contact = false;
                int landObs = -1, domObs = -1;
                long sig = Signature(tg), th = TargetHeight(tg);
                // vessel / aircraft observers
                for (int oi = 0; oi < _d.Length && !visible; oi++)
                {
                    var ob = _d[oi];
                    if (ob.Side != side || !ob.OnField) continue;
                    if (ob.Dom == Domain.Naval && tg.Dom == Domain.Naval)
                    {
                        long reach = ob.V!.RadiusMm + tg.V!.RadiusMm + _dr.BoardingReachMm;
                        if (Vec2L.DistanceSq(ob.Pos, tg.Pos) <= (Int128)reach * reach) { contact = true; visible = true; domObs = oi; break; }
                    }
                    long range = FixedMath.MulDivFloor(ob.V?.SightMm ?? ob.A!.SightMm, sig, 100);
                    if (Vec2L.DistanceSq(ob.Pos, tg.Pos) > (Int128)range * range) continue;
                    if (ob.Dom == Domain.Naval)
                    {
                        var los = LineOfSight.Query(_grid, _conceal, ob.Pos, ob.V!.HeightMm, tg.Pos, th);
                        if (!los.Visible) continue;
                    }
                    visible = true; domObs = oi;
                }
                // land observers
                for (int oi = 0; oi < _e.Length && !visible; oi++)
                {
                    var ob = _e[oi];
                    if (ob.Side != side || !ob.OnField) continue;
                    long range = FixedMath.MulDivFloor(ob.P.SightCells * S, sig, 100);
                    if (Vec2L.DistanceSq(ob.Pos, tg.Pos) > (Int128)range * range) continue;
                    var los = LineOfSight.Query(_grid, _conceal, ob.Pos, _r.EyeHeightMm, tg.Pos, th);
                    if (!los.Visible) continue;
                    visible = true; landObs = oi;
                }
                if (k.Tracked[ti])
                {
                    if (visible) { k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t; }
                    else k.Tracked[ti] = false;
                    continue;
                }
                if (!visible) continue;
                // Sea and sky do not conceal: the best observer's chance is 1 (clarity 1, never stationary-concealed).
                bool detect = contact || CounterRng.Chance(_setup.Seed, TacticalStreams.Detection, tg.Id, t, side, 0, Q32.One);
                if (!detect) continue;
                k.Tracked[ti] = true; k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t;
                if (!k.Ever[ti])
                {
                    k.Ever[ti] = true;
                    if (domObs >= 0) _d[domObs].Primary = true; else _e[landObs].Primary = true;
                }
            }
        }
    }

    // ------------------------------------------------------------------ engagement: target choice

    private readonly struct Shooter
    {
        public readonly long Id; public readonly int Gen; public readonly int Side; public readonly Vec2L Pos; public readonly long Alt;
        public readonly bool HasHeading; public readonly int Heading; public readonly long Eye;
        public readonly OrderKind Dir; public readonly long DirTarget; public readonly Vec2L DirPos;
        public Shooter(long id, int gen, int side, Vec2L pos, long alt, bool hasHeading, int heading, long eye, OrderKind dir, long dirTarget, Vec2L dirPos)
        {
            Id = id; Gen = gen; Side = side; Pos = pos; Alt = alt; HasHeading = hasHeading; Heading = heading; Eye = eye;
            Dir = dir; DirTarget = dirTarget; DirPos = dirPos;
        }
    }

    private void DomainTargets(long t)
    {
        // Land entities with domain weapons (crew-served guns): stationary, Active, not retreating.
        if (_lwNext is not null)
            for (int i = 0; i < _e.Length; i++)
            {
                var e = _e[i];
                if (_lwNext[i] is null || e.State != EntityState.Active || e.Moved) continue;
                var f = _f[e.F];
                if (f.Dir == OrderKind.Retreat) continue;
                var sh = new Shooter(e.Id, e.Gen, e.Side, e.Pos, 0, false, 0, _r.MuzzleHeightMm, f.Dir, f.DirTarget, f.DirPos);
                ChooseAndFire(t, sh, f.Spec.Weapons!, _lwNext[i], _lwAmmo![i]);
            }
        if (_d.Length == 0) return;
        var pairs = new List<(int, int)>();
        for (int i = 0; i < _d.Length; i++)
        {
            var d = _d[i];
            if (d.State != EntityState.Active || !d.OnField) continue;
            if (d.Dom == Domain.Air && d.Phase != AirPhase.Airborne) continue;
            var f = _df[d.F];
            if (f.Dir == OrderKind.Retreat) continue;
            var ws = d.V?.Weapons ?? d.A!.Weapons;
            long eye = d.Dom == Domain.Naval ? d.V!.HeightMm : d.A!.AltitudeMm;
            if (ws.Length > 0 && d.Aboard > 0 && (d.Dom == Domain.Air || d.Crew > 0))
            {
                var sh = new Shooter(d.Id, d.Gen, d.Side, d.Pos, d.AltitudeMm, true, d.Heading, eye, f.Dir, f.DirTarget, f.DirPos);
                ChooseAndFire(t, sh, ws, d.NextFireMs, d.Ammo);
            }
            if (d.Dom != Domain.Naval) continue;
            // Boarding: a Board-directive hull, slowed, in contact with a vessel of its target formation.
            if (f.Dir == OrderKind.Board && d.Aboard > 0 && d.Speed <= _dr.BoardingSpeedMmPerS)
            {
                int b = ContactVessel(d, f.DirTarget, requireBowArc: false);
                if (b >= 0)
                {
                    var key = (Math.Min(i, b), Math.Max(i, b));
                    if (!pairs.Contains(key))
                    {
                        pairs.Add(key);
                        _s.Schedule(t, EventClass.Fire, 1, d.Id, DomainKinds.Boarding, new EntityRef(_d[b].Id, _d[b].Gen), new EntityRef(d.Id, d.Gen));
                    }
                }
            }
            // Ram: a ram-equipped hull at ramming speed, target inside its bow arc and in contact.
            if (d.V!.RamDamage > 0 && (f.Dir == OrderKind.Attack || f.Dir == OrderKind.Board) && t >= d.RamReadyMs
                && d.Speed * 2 >= d.V.MaxSpeedMmPerS)
            {
                int b = ContactVessel(d, f.DirTarget, requireBowArc: true);
                if (b >= 0) _s.Schedule(t, EventClass.Fire, 2, d.Id, DomainKinds.Ram, new EntityRef(_d[b].Id, _d[b].Gen), new EntityRef(d.Id, d.Gen));
            }
        }
    }

    /// <summary>Nearest enemy vessel of the given formation in grappling contact (key (distance², id)); -1 if none.</summary>
    private int ContactVessel(DEnt d, long formationId, bool requireBowArc)
    {
        int best = -1; Int128 bd = 0;
        for (int j = 0; j < _d.Length; j++)
        {
            var x = _d[j];
            if (x.Side == d.Side || x.Dom != Domain.Naval || !x.OnField) continue;
            if (_df[x.F].Id != formationId) continue;
            long reach = d.V!.RadiusMm + x.V!.RadiusMm + _dr.BoardingReachMm;
            Int128 dd = Vec2L.DistanceSq(d.Pos, x.Pos);
            if (dd > (Int128)reach * reach) continue;
            if (requireBowArc)
            {
                int bearing = FixedTrig.Atan2(x.Pos.Y - d.Pos.Y, x.Pos.X - d.Pos.X);
                if (FixedTrig.Separation(bearing, d.Heading) > FixedTrig.FromDegrees(30)) continue;
            }
            if (best < 0 || dd < bd) { best = j; bd = dd; }
        }
        return best;
    }

    private static bool InArc(in Shooter sh, WeaponSpec w, Vec2L target)
    {
        if (!sh.HasHeading || w.Arc == MountArc.Turret) return true;
        int bearing = FixedTrig.Atan2(target.Y - sh.Pos.Y, target.X - sh.Pos.X);
        if (w.Arc == MountArc.Bow) return FixedTrig.Separation(bearing, sh.Heading) <= w.ArcHalf;
        int port = FixedTrig.Norm((long)sh.Heading + FixedTrig.Quarter), star = FixedTrig.Norm((long)sh.Heading - FixedTrig.Quarter);
        return FixedTrig.Separation(bearing, port) <= w.ArcHalf || FixedTrig.Separation(bearing, star) <= w.ArcHalf;
    }

    /// <summary>
    /// Distance for the range test, shooter a (altitude altA) to target b (altitude altB): SLANT range when the target is
    /// airborne (air defence, air-to-air), HORIZONTAL range when the target is on the ground or the sea — an aircraft's
    /// air-to-ground weapons are released at a horizontal distance (bombs over the target, rockets at stand-off).
    /// </summary>
    private static long RangeOf(Vec2L a, long altA, Vec2L b, long altB)
    {
        Int128 dsq = Vec2L.DistanceSq(a, b);
        if (altB != 0) dsq += (Int128)(altA - altB) * (altA - altB);
        return (long)FixedMath.ISqrt((UInt128)dsq);
    }

    private bool DangerClose(int side, Vec2L aim, WeaponSpec w, long range)
    {
        long half = FixedMath.MulDivFloor(range, w.DispersionPermille, 1000);
        long r = checked(w.BlastRadiusMm + half * 2);        // the square's half-diagonal is < 2 x half-width
        Int128 r2 = (Int128)r * r;
        foreach (var e in _e) if (e.Side == side && e.OnField && Vec2L.DistanceSq(e.Pos, aim) <= r2) return true;
        return false;
    }

    private void ChooseAndFire(long t, in Shooter sh, WeaponSpec[] ws, long[] next, int[] ammo)
    {
        for (int m = 0; m < ws.Length; m++)
        {
            if (t < next[m] || ammo[m] == 0) continue;
            var w = ws[m];
            bool limited = w.Ammo > 0;
            // best key (priority, distance², id); the bombard POINT is candidate id -1 at priority 0
            bool found = false; int bp = 0; Int128 bd = 0; long bid = 0; EntityRef target = EntityRef.None; Vec2L aim = default;
            void Offer(int prio, Int128 dsq, long id, EntityRef r, Vec2L p)
            {
                if (found && (prio > bp || (prio == bp && (dsq > bd || (dsq == bd && id >= bid))))) return;
                found = true; bp = prio; bd = dsq; bid = id; target = r; aim = p;
            }
            if (sh.Dir == OrderKind.Bombard && sh.DirTarget < 0 && w.Indirect && (w.Targets & TargetDomains.Land) != 0)
            {
                long d = RangeOf(sh.Pos, sh.Alt, sh.DirPos, 0);
                if (d >= w.MinRangeMm && d <= w.MaxRangeMm && InArc(sh, w, sh.DirPos) && !DangerClose(sh.Side, sh.DirPos, w, d))
                    Offer(0, Vec2L.DistanceSq(sh.Pos, sh.DirPos), -1, EntityRef.None, sh.DirPos);
            }
            bool designate = sh.Dir == OrderKind.Attack || sh.Dir == OrderKind.Bombard || sh.Dir == OrderKind.Board;
            if ((w.Targets & TargetDomains.Land) != 0)
            {
                var k = _k[sh.Side];
                for (int i = 0; i < _e.Length; i++)
                {
                    var e = _e[i];
                    if (e.Side == sh.Side || !e.OnField || !k.Tracked[i]) continue;
                    int prio = designate && _f[e.F].Id == sh.DirTarget ? 0 : 1;
                    if (limited && prio != 0) continue;
                    long d = RangeOf(sh.Pos, sh.Alt, e.Pos, 0);
                    if (d < w.MinRangeMm || d > w.MaxRangeMm || !InArc(sh, w, e.Pos)) continue;
                    Int128 dsq = Vec2L.DistanceSq(sh.Pos, e.Pos);
                    if (found && (prio > bp || (prio == bp && (dsq > bd || (dsq == bd && e.Id >= bid))))) continue;
                    if (w.Indirect) { if (DangerClose(sh.Side, e.Pos, w, d)) continue; }
                    else if (sh.Alt == 0 && !LineOfSight.Query(_grid, _conceal, sh.Pos, sh.Eye, e.Pos, _r.AimHeightMm).Visible) continue;
                    Offer(prio, dsq, e.Id, new EntityRef(e.Id, e.Gen), e.Pos);
                }
            }
            if ((w.Targets & (TargetDomains.Surface | TargetDomains.Air)) != 0)
            {
                var k = _dk[sh.Side];
                for (int i = 0; i < _d.Length; i++)
                {
                    var x = _d[i];
                    if (x.Side == sh.Side || !x.OnField || !k.Tracked[i]) continue;
                    var need = x.Dom == Domain.Air ? TargetDomains.Air : TargetDomains.Surface;
                    if ((w.Targets & need) == 0) continue;
                    int prio = designate && _df[x.F].Id == sh.DirTarget ? 0 : 1;
                    if (limited && prio != 0) continue;
                    long d = RangeOf(sh.Pos, sh.Alt, x.Pos, x.AltitudeMm);
                    if (d < w.MinRangeMm || d > w.MaxRangeMm || !InArc(sh, w, x.Pos)) continue;
                    Int128 dsq = Vec2L.DistanceSq(sh.Pos, x.Pos);
                    if (found && (prio > bp || (prio == bp && (dsq > bd || (dsq == bd && x.Id >= bid))))) continue;
                    if (sh.Alt == 0 && !LineOfSight.Query(_grid, _conceal, sh.Pos, sh.Eye, x.Pos, TargetHeight(x)).Visible) continue;
                    Offer(prio, dsq, x.Id, new EntityRef(x.Id, x.Gen), x.Pos);
                }
            }
            if (!found) continue;
            _s.Schedule(t, EventClass.Fire, 0, sh.Id, DomainKinds.DomainFire, target, new EntityRef(sh.Id, sh.Gen), m, aim.X, aim.Y);
        }
    }

    // ------------------------------------------------------------------ fire

    private Q32 WithFalloff(Q32 p, long dist, long maxRange)
    {
        long fl = _dr.RangeFalloffFloorPermille;
        long factor = fl + FixedMath.MulDivFloor(1000 - fl, Math.Max(0, maxRange - dist), maxRange);
        return new Q32(FixedMath.MulDivFloor(p.Raw, factor, 1000));
    }

    private void DomainFire(long t, in ScheduledEvent ev)
    {
        int li = Array.BinarySearch(_ids, ev.Source.Id);
        int m = (int)ev.A;
        WeaponSpec w; Vec2L pos; long alt; int side;
        if (li >= 0)
        {
            var e = _e[li];
            var f = _f[e.F];
            w = f.Spec.Weapons![m]; pos = e.Pos; alt = 0; side = e.Side;
            if (w.Ammo > 0) _lwAmmo![li][m]--;
            _lwNext![li][m] = checked(t + w.ReloadMs);
            e.Primary = true; e.RevealUntilMs = t + _r.StepMs; f.Attacks++;
        }
        else
        {
            var d = _d[DIndex(ev.Source.Id)];
            var ws = d.V?.Weapons ?? d.A!.Weapons;
            w = ws[m]; pos = d.Pos; alt = d.AltitudeMm; side = d.Side;
            if (w.Ammo > 0) d.Ammo[m]--;
            long reload = d.Dom == Domain.Naval ? FixedMath.MulDivCeil(w.ReloadMs, 1000, Math.Max(1, CrewFactorPermille(d))) : w.ReloadMs;
            d.NextFireMs[m] = checked(t + reload);
            d.Primary = true; _df[d.F].Attacks++;
        }
        long sid = ev.Source.Id;
        bool landTarget = !ev.Target.IsNone && Array.BinarySearch(_ids, ev.Target.Id) >= 0;
        if (ev.Target.IsNone || (landTarget && w.Indirect))
        {
            Vec2L aimPt = new(ev.B, ev.C);
            long range = RangeOf(pos, alt, aimPt, 0);
            long half = FixedMath.MulDivFloor(range, w.DispersionPermille, 1000);
            long span = checked(2 * half + 1);
            long ox = (long)CounterRng.NextBelow(_setup.Seed, DomainStreams.Dispersion, sid, t, m, 0, (ulong)span) - half;
            long oy = (long)CounterRng.NextBelow(_setup.Seed, DomainStreams.Dispersion, sid, t, m, 1, (ulong)span) - half;
            Vec2L impact = new(aimPt.X + ox, aimPt.Y + oy);
            long R = w.BlastRadiusMm;
            foreach (var e in _e)
            {
                if (!e.OnField) continue;
                long dd = Vec2L.Distance(e.Pos, impact);
                if (dd > R) continue;
                // Lethality falls off linearly from the impact: P(effect) = (R - d) / R (1 at the impact point).
                if (CounterRng.Chance(_setup.Seed, DomainStreams.Blast, sid, t, e.Id, (uint)m, Q32.FromRatio(R - dd, R)))
                    _s.Schedule(t, EventClass.Damage, 0, e.Id, DomainKinds.DomainHit, new EntityRef(e.Id, e.Gen), ev.Source, 0, w.Damage);
            }
            if ((w.Targets & TargetDomains.Surface) != 0)
                foreach (var x in _d)
                {
                    if (x.Dom != Domain.Naval || !x.OnField) continue;
                    long dd = Math.Max(0, Vec2L.Distance(x.Pos, impact) - x.V!.RadiusMm);     // distance to the hull's edge
                    if (dd > R) continue;
                    if (CounterRng.Chance(_setup.Seed, DomainStreams.Blast, sid, t, x.Id, (uint)m, Q32.FromRatio(R - dd, R)))
                        _s.Schedule(t, EventClass.Damage, 0, x.Id, DomainKinds.DomainHit, new EntityRef(x.Id, x.Gen), ev.Source, 1, w.HullDamage, w.CrewDamage);
                }
            return;
        }
        if (landTarget)
        {
            var tg = _e[EntIndex(ev.Target.Id)];
            long d = RangeOf(pos, alt, tg.Pos, 0);
            Q32 p = WithFalloff(HitChance(w.Accuracy, _f[tg.F].EffDefence), d, w.MaxRangeMm);
            if (CounterRng.Chance(_setup.Seed, DomainStreams.Hit, sid, t, tg.Id, (uint)m, p))
                _s.Schedule(t, EventClass.Damage, 0, tg.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 0, w.Damage);
            return;
        }
        var x2 = _d[DIndex(ev.Target.Id)];
        long dist = RangeOf(pos, alt, x2.Pos, x2.AltitudeMm);
        long evasion = x2.V?.Evasion ?? x2.A!.Evasion;
        Q32 ph = WithFalloff(HitChance(w.Accuracy, evasion), dist, w.MaxRangeMm);
        if (!CounterRng.Chance(_setup.Seed, DomainStreams.Hit, sid, t, x2.Id, (uint)m, ph)) return;
        if (x2.Dom == Domain.Air) _s.Schedule(t, EventClass.Damage, 0, x2.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 2, w.Damage);
        else _s.Schedule(t, EventClass.Damage, 0, x2.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 1, w.HullDamage, w.CrewDamage);
    }

    private void BoardingFight(long t, in ScheduledEvent ev)
    {
        var a = _d[DIndex(ev.Source.Id)];
        var b = _d[DIndex(ev.Target.Id)];
        long na = a.Crew + a.Embarked - a.PendingEmbarkedCasualties, nb = b.Crew + b.Embarked - b.PendingEmbarkedCasualties;
        if (na <= 0 || nb <= 0) return;
        if (a.FightStartAboard == 0) a.FightStartAboard = na;
        if (b.FightStartAboard == 0) b.FightStartAboard = nb;
        a.BoardedBy = DIndex(b.Id); b.BoardedBy = DIndex(a.Id);
        long onB = BoardingCasualties(na, HitChance(a.V!.BoardingMelee, b.V!.BoardingDefence), a.Id, t, b.Id, 0, nb);
        long onA = BoardingCasualties(nb, HitChance(b.V.BoardingMelee, a.V.BoardingDefence), b.Id, t, a.Id, 1, na);
        a.Primary = true; b.Primary = true;
        _df[a.F].Attacks++; _df[b.F].Attacks++;
        if (onB > 0) _s.Schedule(t, EventClass.Damage, 0, b.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 1, 0, onB);
        if (onA > 0) _s.Schedule(t, EventClass.Damage, 0, a.Id, DomainKinds.DomainHit, ev.Source, ev.Target, 1, 0, onA);
        _grappledAt[DIndex(a.Id)] = t; _grappledAt[DIndex(b.Id)] = t;
    }

    /// <summary>Expected casualties = fighters x p x lethality x dt / 10^6, integer part plus a counter-RNG Bernoulli on the
    /// remainder (stochastic rounding: exact expectation, integer outcome), capped at the defenders present.</summary>
    private long BoardingCasualties(long fighters, Q32 p, long attackerId, long t, long defenderId, uint idx, long cap)
    {
        Int128 num = (Int128)fighters * p.Raw * _dr.BoardingLethalityPermillePerS * _r.StepMs;
        Int128 den = (Int128)Q32.OneRaw * 1_000_000;
        long whole = (long)(num / den);
        long rem = (long)(num - (Int128)whole * den);
        Q32 frac = new(FixedMath.MulDivFloor(rem, Q32.OneRaw, (long)den));
        if (CounterRng.Chance(_setup.Seed, DomainStreams.Boarding, attackerId, t, defenderId, idx, frac)) whole++;
        return Math.Min(whole, cap);
    }

    private void RamStrike(long t, in ScheduledEvent ev)
    {
        var a = _d[DIndex(ev.Source.Id)];
        long dmg = Math.Max(1, FixedMath.MulDivFloor(a.V!.RamDamage, a.Speed, a.V.MaxSpeedMmPerS));
        a.RamReadyMs = checked(t + a.V.RamCooldownMs);
        a.Speed = 0;
        a.Primary = true; _df[a.F].Attacks++;
        _s.Schedule(t, EventClass.Damage, 0, ev.Target.Id, DomainKinds.DomainHit, ev.Target, ev.Source, 1, dmg, 0);
    }

    // ------------------------------------------------------------------ damage

    private void DomainHit(long t, in ScheduledEvent ev)
    {
        long amount = 0;
        if (ev.A == 0)
        {
            var tg = _e[EntIndex(ev.Target.Id)];
            amount = Math.Min(ev.B, tg.Hp);
            tg.Hp -= amount;
            tg.Primary = true;
            _f[tg.F].DamageTaken += amount;
        }
        else if (ev.A == 2)
        {
            var x = _d[DIndex(ev.Target.Id)];
            amount = Math.Min(ev.B, x.Hp);
            x.Hp -= amount;
            x.Primary = true;
            _df[x.F].DamageTaken += amount;
        }
        else
        {
            var x = _d[DIndex(ev.Target.Id)];
            var f = _df[x.F];
            long hull = Math.Min(ev.B, x.Hp);
            x.Hp -= hull;
            long embEff = x.Embarked - x.PendingEmbarkedCasualties;
            long aboard = x.Crew + embEff;
            long people = Math.Min(ev.C, aboard);
            long emb = aboard == 0 ? 0 : FixedMath.MulDivFloor(people, embEff, aboard);
            long crew = people - emb;
            if (crew > 0)
            {
                x.Crew -= crew;
                _ledger.Transfer(x.CrewAccount, f.AccKilled, crew, t, PersonnelFlowReason.KilledInAction);
                f.KilledStep += crew;
            }
            x.PendingEmbarkedCasualties += emb;
            x.Primary = true;
            amount = hull + people;
            f.DamageTaken += amount;
        }
        CreditSource(ev.Source.Id, amount);
    }

    private void CreditSource(long id, long amount)
    {
        int li = Array.BinarySearch(_ids, id);
        if (li >= 0) { _e[li].Primary = true; _f[_e[li].F].DamageDealt += amount; return; }
        var d = _d[DIndex(id)];
        d.Primary = true;
        _df[d.F].DamageDealt += amount;
    }

    // ------------------------------------------------------------------ resolve (start of the land Resolve)

    private long[] _grappledAt = Array.Empty<long>();

    private void DomainResolve(long t)
    {
        for (int i = 0; i < _d.Length; i++)
        {
            var d = _d[i];
            if (!d.Present) continue;
            if (d.Dom == Domain.Naval)
            {
                if (d.PendingEmbarkedCasualties > 0)
                {
                    long n = Math.Min(d.PendingEmbarkedCasualties, d.Embarked);
                    d.PendingEmbarkedCasualties = 0;
                    foreach (int li in AboardByDraw(i, t, n)) LeaveWithCarrier(li, EntityState.Dead, PersonnelFlowReason.KilledInAction);
                }
                if (d.Hp <= 0) { Sink(d, i, t); continue; }
                if (d.Aboard == 0) { SetDState(d, EntityState.Dead); continue; }        // nobody left aboard: derelict, lost
                if (_grappledAt[i] == t && d.BoardedBy >= 0)
                {
                    var opp = _d[d.BoardedBy];
                    if (opp.Present && d.Aboard * _dr.StrikeDen <= d.FightStartAboard * _dr.StrikeNum && opp.Aboard > d.Aboard)
                    {
                        Capture(d, i);
                        continue;
                    }
                }
                else if (_grappledAt[i] != t) { d.FightStartAboard = 0; d.BoardedBy = -1; }
            }
            else if (d.Phase != AirPhase.Based && (d.Hp <= 0 || d.CrashPending))
            {
                Down(d);
            }
        }
        // Morale and rout of vessel formations (personnel losses, leadership-scaled, the land equation).
        foreach (var f in _df)
        {
            if (f.Dom != Domain.Naval) continue;
            if (f.KilledStep > 0)
            {
                long loss = ConditionMath.MoraleLoss(f.KilledStep, _r.MoraleLossFullStrength, Math.Max(1, f.StartPersonnel), f.Spec.Leadership);
                f.Morale = Math.Max(0, f.Morale - loss);
                f.CasualtiesPulse += f.KilledStep;
                f.KilledStep = 0;
            }
            if (!f.Routed && f.Morale < _r.RoutThreshold)
            {
                bool any = false;
                foreach (int i in f.Members) if (_d[i].State == EntityState.Active) any = true;
                if (!any) continue;
                f.Routed = true;
                foreach (int i in f.Members) if (_d[i].State == EntityState.Active) SetDState(_d[i], EntityState.Routing);
            }
        }
        // Rally (damage control): hull repaired from a bounded pool.
        foreach (var d in _d)
        {
            if (d.Dom != Domain.Naval || d.State != EntityState.Active || _df[d.F].Dir != OrderKind.Rally) continue;
            long h = Math.Min(Math.Min(FixedMath.MulDivFloor(d.V!.RepairPerS, _r.StepMs, 1000), d.RepairPool), d.V.Hull - d.Hp);
            if (h > 0) { d.Hp += h; d.RepairPool -= h; }
        }
        // A routing hull that is grappled by a boarder cannot break away: it strikes (the land rule "routing, blocked, enemy
        // in contact => surrender", for vessels).
        for (int i = 0; i < _d.Length; i++)
            if (_d[i].Dom == Domain.Naval && _d[i].State == EntityState.Routing && _grappledAt[i] == t) Capture(_d[i], i);
        // Withdrawal: routing or retreating vessels that reach their exit edge leave the battle alive.
        foreach (var d in _d)
        {
            if (d.Dom != Domain.Naval) continue;
            bool routing = d.State == EntityState.Routing;
            if (!routing && !(d.State == EntityState.Active && _df[d.F].Dir == OrderKind.Retreat)) continue;
            var c = _grid.CellOf(d.Pos);
            if (_navEdge![d.Side].OnEdge(c.X, c.Y)) { d.RoutedEscape = routing; LeaveBattleAlive(d); continue; }
            if (routing && _navEdge[d.Side].DistanceAt(_grid.Index(c.X, c.Y)) == EdgeField.Unreachable) Capture(d, DIndex(d.Id));
        }
    }

    /// <summary>The n land entities aboard vessel vi with the smallest (counter-RNG draw, id): deterministic victims.</summary>
    private List<int> AboardByDraw(int vi, long t, long n)
    {
        var all = new List<(ulong Draw, long Id, int Idx)>();
        for (int i = 0; i < _e.Length; i++)
            if (_carrier[i] == vi) all.Add((CounterRng.Draw(_setup.Seed, DomainStreams.Victims, _d[vi].Id, t, _e[i].Id, 0), _e[i].Id, i));
        all.Sort(static (a, b) => a.Draw != b.Draw ? a.Draw.CompareTo(b.Draw) : a.Id.CompareTo(b.Id));
        var r = new List<int>();
        for (int k = 0; k < n && k < all.Count; k++) r.Add(all[k].Idx);
        return r;
    }

    private void Sink(DEnt d, int vi, long t)
    {
        var f = _df[d.F];
        SetDState(d, EntityState.Dead);
        long saved = FixedMath.MulDivFloor(d.Crew, _dr.ShipwreckSurvivalNum, _dr.ShipwreckSurvivalDen);
        long lost = d.Crew - saved;
        _ledger.Transfer(d.CrewAccount, f.AccEscaped, saved, t, PersonnelFlowReason.ShipwreckSurvivor);
        _ledger.Transfer(d.CrewAccount, f.AccLost, lost, t, PersonnelFlowReason.LostWithVessel);
        f.KilledStep += lost;
        d.Crew = 0;
        long emb = d.Embarked;
        long survivors = FixedMath.MulDivFloor(emb, _dr.ShipwreckSurvivalNum, _dr.ShipwreckSurvivalDen);
        var order = AboardByDraw(vi, t, emb);
        for (int k = 0; k < order.Count; k++)
            LeaveWithCarrier(order[k], k < survivors ? EntityState.Escaped : EntityState.Dead,
                             k < survivors ? PersonnelFlowReason.ShipwreckSurvivor : PersonnelFlowReason.LostWithVessel);
    }

    private void Capture(DEnt d, int vi)
    {
        var f = _df[d.F];
        SetDState(d, EntityState.Surrendered);
        _ledger.Transfer(d.CrewAccount, f.AccCaptured, d.Crew, _s.NowMs, PersonnelFlowReason.Captured);
        d.Crew = 0;
        for (int i = 0; i < _e.Length; i++) if (_carrier[i] == vi) LeaveWithCarrier(i, EntityState.Surrendered, null);
    }

    private void Down(DEnt d)
    {
        var f = _df[d.F];
        SetDState(d, EntityState.Dead);
        long saved = FixedMath.MulDivFloor(d.Crew, _dr.BailoutSurvivalNum, _dr.BailoutSurvivalDen);
        _ledger.Transfer(d.CrewAccount, f.AccEscaped, saved, _s.NowMs, PersonnelFlowReason.ShipwreckSurvivor);
        _ledger.Transfer(d.CrewAccount, f.AccLost, d.Crew - saved, _s.NowMs, PersonnelFlowReason.LostWithVessel);
        d.Crew = 0;
    }
}
