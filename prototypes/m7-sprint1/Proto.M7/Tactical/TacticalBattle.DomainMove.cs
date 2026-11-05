using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// LANE N — movement (Arrival phase, after the land movers): vessels, aircraft, and transport operations.
///
/// VESSELS. Each hull has (Pos, Heading, Speed). Per step: an intent (desired heading, target speed) from the formation's
/// directive; heading turns toward it by at most TurnRate * dt (sail: scaled by way on, see <see cref="TurnFactorPermille"/>);
/// speed approaches the target by at most Accel * dt within [0, MaxSpeed * crew factor * sail factor]; the desired end point
/// Pos + Offset(heading, speed * dt) is resolved by the UNCHANGED Slice C separation step on the naval view of the grid
/// (water only, hulls never overlap). A truncated move sets the speed to the distance actually covered (collision/grounding
/// absorbs momentum). Line ahead: the lowest-id active hull leads along an A* path on the naval view; the others keep
/// station rank * spacing astern of the leader. Movement priority between hulls is a counter-RNG draw per (instant, hull)
/// (no side bias, as for land movers).
///
/// AIRCRAFT. Same heading kinematics; a fixed-wing aircraft never flies below MinSpeed (it cannot hover: holding a point means
/// circling it), a rotary-wing aircraft may hover. Every airborne step burns ceil(dt * max(speed, floor) / cruise) fuel
/// (floor = MinSpeed, or the hover burn for rotary wing), so fuel strictly decreases. Bingo: when fuel would not cover the
/// flight home at cruise plus the reserve, the aircraft returns (no order needed, no order can keep it up); it also returns
/// with empty racks or at the damage-abort threshold. Landing at base starts the turnaround; a sortie is spent at take-off.
/// </summary>
public sealed partial class TacticalBattle
{
    private void DomainStep(long t)
    {
        if (_d.Length > 0)
        {
            MoveVessels(t);
            MoveAircraft(t);
        }
        TransportOps(t);
        DomainMinima();
    }

    // ------------------------------------------------------------------ vessels

    private long CrewFactorPermille(DEnt d)
    {
        long need = d.V!.Crew;
        if (d.Crew >= need) return 1000;
        if (d.Aboard == 0) return 0;
        return Math.Max(250, FixedMath.MulDivFloor(d.Crew, 1000, need));
    }

    private long VesselMaxSpeed(DEnt d) => FixedMath.MulDivFloor(d.V!.MaxSpeedMmPerS, CrewFactorPermille(d), 1000);

    /// <summary>Sail: rudder authority needs way on — turn rate x clamp(2 * speed / max, 1/4, 1). Oar / engine: full.</summary>
    private static long TurnFactorPermille(DEnt d)
    {
        if (d.V!.Propulsion != Propulsion.Sail) return 1000;
        long f = FixedMath.MulDivFloor(d.Speed, 2000, d.V.MaxSpeedMmPerS);
        return Math.Clamp(f, 250, 1000);
    }

    private void MoveVessels(long t)
    {
        int n = 0;
        foreach (var d in _d) if (d.Dom == Domain.Naval && d.OnField) n++;
        if (n == 0) return;
        var movers = new Mover[n];
        int m = 0;
        long dt = _r.StepMs;
        foreach (var f in _df)
            if (f.Dom == Domain.Naval && (f.Dir == OrderKind.MoveTo || f.Dir == OrderKind.Patrol) && f.PathLeader != LeadVessel(f)) PlanVesselPath(f);
        for (int i = 0; i < _d.Length; i++)
        {
            var d = _d[i];
            if (d.Dom != Domain.Naval || !d.OnField) continue;
            var v = d.V!;
            VesselIntent(d, _df[d.F], out int want, out long target);
            if (v.Propulsion == Propulsion.Sail && _dr.Wind) want = OffTheWind(want, d.Heading);
            long turn = FixedMath.MulDivFloor(HeadingMotion.MaxTurn(v.TurnRatePerS, dt), TurnFactorPermille(d), 1000);
            int h = HeadingMotion.TurnToward(d.Heading, want, turn);
            long vmax = VesselMaxSpeed(d);
            if (v.Propulsion == Propulsion.Sail) vmax = FixedMath.MulDivFloor(vmax, _dr.SailFactor(h), 1000);
            // A hull turning hard loses way: speed <= vmax x max(1/4, cos(heading error)).
            long cosErr = FixedMath.MulDivFloor(FixedTrig.Cos(FixedTrig.Diff(h, want)), 1000, FixedTrig.One);
            target = Math.Min(target, FixedMath.MulDivFloor(vmax, Math.Max(250, cosErr), 1000));
            long sp = HeadingMotion.Approach(d.Speed, Math.Min(target, vmax), v.AccelMmPerS2, dt, 0, Math.Max(vmax, 0));
            d.Heading = h; d.Speed = sp;
            RamRecordWay(i, sp, d.Pos);                                              // [RAM] impact speed = way before truncation; step start
            Vec2L want2 = d.Pos + HeadingMotion.Offset(h, HeadingMotion.Distance(sp, dt));
            ulong draw = CounterRng.Draw(_setup.Seed, DomainStreams.MovePriority, d.Id, t, 0, 0);
            movers[m++] = new Mover((long)(draw >> 17) << 16 | (long)i, d.Pos, v.RadiusMm, want2);
        }
        var res = Separation.Step(movers, _nav, _costs);
        foreach (var r in res)
        {
            var d = _d[(int)(r.Id & MoverIndexMask)];
            Vec2L wanted = default;
            foreach (var mv in movers) if (mv.Id == r.Id) { wanted = mv.Desired; break; }
            d.Moved = r.Pos != d.Pos;
            if (r.Pos != wanted) d.Speed = FixedMath.MulDivFloor(Vec2L.Distance(d.Pos, r.Pos), 1000, dt);
            d.Pos = r.Pos;
        }
        for (int i = 0; i < _e.Length; i++) if (_carrier[i] >= 0) _e[i].Pos = _d[_carrier[i]].Pos;
    }

    /// <summary>A sailing vessel cannot point inside the no-go zone: it sails close-hauled on the tack nearer its heading.</summary>
    private int OffTheWind(int want, int heading)
    {
        if (FixedTrig.Separation(want, _dr.WindFrom) > _dr.SailNoGo) return want;
        int a = FixedTrig.Norm((long)_dr.WindFrom + _dr.SailNoGo + 1), b = FixedTrig.Norm((long)_dr.WindFrom - _dr.SailNoGo - 1);
        return FixedTrig.Separation(heading, a) <= FixedTrig.Separation(heading, b) ? a : b;
    }

    private void PlanVesselPath(DForm f)
    {
        int lead = LeadVessel(f);
        f.PathLeader = lead;
        f.PathIdx = 0;
        f.Path = Array.Empty<Vec2L>();
        if (lead < 0) return;
        var pr = _navPf!.FindPath(_grid.CellOf(_d[lead].Pos), _grid.CellOf(f.DirPos));
        if (!pr.Found) return;
        var w = PathMovement.Waypoints(_grid, pr.Cells);
        w[^1] = f.DirPos;
        f.Path = w;
        if (f.Path.Length > 1) f.PathIdx = 1;        // the first waypoint is the leader's own cell centre
    }

    private void GoTo(DEnt d, Vec2L p, long vmax, bool brake, out int heading, out long speed)
    {
        long dist = Vec2L.Distance(d.Pos, p);
        heading = dist == 0 ? d.Heading : FixedTrig.Atan2(p.Y - d.Pos.Y, p.X - d.Pos.X);
        long accel = d.V?.AccelMmPerS2 ?? d.A!.AccelMmPerS2;
        speed = brake ? Math.Min(vmax, HeadingMotion.BrakingSpeed(dist, accel)) : vmax;
        // Arrived (vessels): within the arrival tolerance a braking vessel stops and keeps its heading (no orbiting).
        if (brake && d.V is not null && dist <= ArriveTolerance(d.V)) { heading = d.Heading; speed = 0; }
    }

    private long ArriveTolerance(VesselClass v) => Math.Max(v.RadiusMm * 2, _grid.CellSizeMm / 2);

    /// <summary>Nearest TRACKED enemy member of a formation (any domain) from a point, key (distance², id); false if none.</summary>
    private bool NearestTrackedMember(int side, long formationId, Vec2L from, out Vec2L pos, out long id)
    {
        pos = default; id = -1;
        bool found = false; Int128 best = 0;
        int lf = FindForm(formationId);
        if (lf >= 0)
        {
            var k = _k[side];
            foreach (int i in _f[lf].Members)
            {
                if (!_e[i].OnField || !k.Tracked[i]) continue;
                Int128 dd = Vec2L.DistanceSq(from, _e[i].Pos);
                if (!found || dd < best) { found = true; best = dd; pos = _e[i].Pos; id = _e[i].Id; }
            }
            return found;
        }
        int df = FindDForm(formationId);
        if (df < 0) return false;
        var dk = _dk[side];
        foreach (int i in _df[df].Members)
        {
            if (!_d[i].OnField || !dk.Tracked[i]) continue;
            Int128 dd = Vec2L.DistanceSq(from, _d[i].Pos);
            if (!found || dd < best) { found = true; best = dd; pos = _d[i].Pos; id = _d[i].Id; }
        }
        return found;
    }

    /// <summary>Known (ever-detected, not gone) centroid of an enemy formation of any domain.</summary>
    private bool KnownCentroidAny(int side, long formationId, out Vec2L c)
    {
        if (KnownCentroid(side, formationId, out c)) return true;
        int df = FindDForm(formationId);
        c = default;
        if (df < 0) return false;
        var k = _dk[side];
        long sx = 0, sy = 0, n = 0;
        foreach (int i in _df[df].Members)
            if (k.Ever[i] && !k.Gone[i]) { sx += k.LastSeen[i].X; sy += k.LastSeen[i].Y; n++; }
        if (n == 0) return false;
        c = new Vec2L(FixedMath.DivFloor(sx, n), FixedMath.DivFloor(sy, n));
        return true;
    }

    /// <summary>Own formation's current centroid (any domain), for Escort.</summary>
    private bool OwnCentroid(long formationId, out Vec2L c)
    {
        c = default;
        int lf = FindForm(formationId);
        if (lf >= 0)
        {
            var f = _f[lf];
            bool any = false;
            foreach (int i in f.Members) if (_e[i].OnField) any = true;
            if (!any) return false;
            c = Centroid(f);
            return true;
        }
        int df = FindDForm(formationId);
        if (df < 0) return false;
        bool anyD = false;
        foreach (int i in _df[df].Members) if (_d[i].OnField) anyD = true;
        if (!anyD) return false;
        c = DCentroid(_df[df], airborneOnly: true);
        return true;
    }

    private WeaponSpec? MainBattery(VesselClass v, TargetDomains need)
    {
        foreach (var w in v.Weapons) if ((w.Targets & need) != 0) return w;
        return null;
    }

    private void VesselIntent(DEnt d, DForm f, out int heading, out long speed)
    {
        var v = d.V!;
        long vmax = VesselMaxSpeed(d);
        heading = d.Heading; speed = 0;
        if (v.RamDamage > 0 && RamLocked(DIndex(d.Id), _s.NowMs)) return;          // [RAM] beak embedded: backing water, whatever the order
        if (d.State == EntityState.Routing || f.Dir == OrderKind.Retreat)
        {
            var c = _grid.CellOf(d.Pos);
            int next = _navEdge![d.Side].NextCell(_grid.Index(c.X, c.Y));
            if (next < 0) return;
            GoTo(d, _grid.CellCenter(_grid.IndexX(next), _grid.IndexY(next)), vmax, false, out heading, out speed);
            return;
        }
        switch (f.Dir)
        {
            case OrderKind.MoveTo:
            case OrderKind.Patrol:
            {
                int lead = LeadVessel(f);
                if (lead < 0) return;
                long formSpeed = long.MaxValue;
                foreach (int i in f.Members) if (_d[i].State == EntityState.Active) formSpeed = Math.Min(formSpeed, FixedMath.MulDivFloor(VesselMaxSpeed(_d[i]), 3, 4));
                if (_d[lead] == d)
                {
                    long arrive = Math.Max(v.RadiusMm, _grid.CellSizeMm / 2);
                    while (f.PathIdx < f.Path.Length - 1 && Vec2L.Distance(d.Pos, f.Path[f.PathIdx]) <= arrive) f.PathIdx++;
                    Vec2L wp = f.PathIdx < f.Path.Length ? f.Path[f.PathIdx] : f.DirPos;
                    bool last = f.PathIdx >= f.Path.Length - 1;
                    GoTo(d, wp, formSpeed, last, out heading, out speed);
                    return;
                }
                int rank = 0;
                foreach (int i in f.Members) { if (_d[i] == d) break; if (_d[i].State == EntityState.Active) rank++; }
                var ld = _d[lead];
                Vec2L station;
                if (f.PathIdx >= f.Path.Length - 1 && Vec2L.Distance(ld.Pos, f.DirPos) <= ArriveTolerance(ld.V!))
                {
                    // Leader has arrived: form line abreast on it (alternate sides), e.g. transports side by side off a beach.
                    int side = rank % 2 == 1 ? 1 : -1;
                    long k = (rank + 1) / 2;
                    station = ld.Pos + HeadingMotion.Offset(FixedTrig.Norm(ld.Heading + side * FixedTrig.Quarter), checked(k * LineSpacing(v) / 2));
                }
                else station = ld.Pos - HeadingMotion.Offset(ld.Heading, checked(rank * LineSpacing(v)));
                GoTo(d, station, vmax, true, out heading, out speed);
                return;
            }
            case OrderKind.Attack:
            case OrderKind.Board:
            {
                int tl = FindForm(f.DirTarget);
                if (tl >= 0 && f.Dir == OrderKind.Attack) { BombardIntent(d, f, vmax, out heading, out speed); return; }
                // [RAM] D-081 ram planner (run-up, steer away, once per pair); false = nothing to ram: stand off below.
                if (f.Dir == OrderKind.Attack && v.RamDamage > 0 && RamIntent(d, vmax, f.DirTarget, out heading, out speed)) return;
                Vec2L tp;
                if (!NearestTrackedMember(d.Side, f.DirTarget, d.Pos, out tp, out _) && !KnownCentroidAny(d.Side, f.DirTarget, out tp)) return;
                if (f.Dir == OrderKind.Board)
                {
                    long contact = v.RadiusMm * 4 + _dr.BoardingReachMm;
                    long cap = Vec2L.Distance(d.Pos, tp) <= contact ? Math.Min(vmax, _dr.BoardingSpeedMmPerS) : vmax;
                    GoTo(d, tp, cap, false, out heading, out speed);
                    return;
                }
                var w = MainBattery(v, TargetDomains.Surface);
                if (w is null) return;
                long R = FixedMath.MulDivFloor(w.MaxRangeMm, _dr.PreferredRangePermille, 1000);
                long dist = Vec2L.Distance(d.Pos, tp);
                int bearing = FixedTrig.Atan2(tp.Y - d.Pos.Y, tp.X - d.Pos.X);
                if (w.Arc == MountArc.Broadside) { heading = Abeam(d.Heading, bearing, dist, R); speed = vmax; return; }
                if (w.Arc == MountArc.Bow) { heading = bearing; speed = dist > R ? vmax : 0; return; }
                heading = bearing; speed = dist > R ? vmax : 0;
                return;
            }
            case OrderKind.Bombard:
                BombardIntent(d, f, vmax, out heading, out speed);
                return;
            case OrderKind.Escort:
            {
                if (!OwnCentroid(f.DirTarget, out Vec2L c)) return;
                // Screening station two line spacings abeam of the escorted formation's COURSE (its lead hull's heading when
                // it is a vessel formation); for other formations, its centroid (the hull stops at the arrival tolerance or
                // the shore). Weapons are free, so the escort engages whatever threatens the escorted formation in range.
                Vec2L station = c;
                int ef = FindDForm(f.DirTarget);
                if (ef >= 0 && _df[ef].Dom == Domain.Naval && LeadVessel(_df[ef]) is var lead && lead >= 0)
                    station = c + HeadingMotion.Offset(FixedTrig.Norm((long)_d[lead].Heading + FixedTrig.Quarter), checked(2 * LineSpacing(v)));
                GoTo(d, station, vmax, true, out heading, out speed);
                return;
            }
            default:
                return;       // Hold, Rally: heave to (speed 0), heading kept
        }
    }

    /// <summary>Broadside heading: put the target abeam on the side needing less turn; close in at 45° when beyond the
    /// preferred range, open out at 45° inside half of it.</summary>
    private static int Abeam(int heading, int bearing, long dist, long R)
    {
        int p = FixedTrig.Norm((long)bearing + FixedTrig.Quarter), s = FixedTrig.Norm((long)bearing - FixedTrig.Quarter);
        int sign = FixedTrig.Separation(heading, p) <= FixedTrig.Separation(heading, s) ? 1 : -1;
        long off = dist > R ? FixedTrig.Quarter - FixedTrig.Eighth : dist < R / 2 ? FixedTrig.Quarter + FixedTrig.Eighth : FixedTrig.Quarter;
        return FixedTrig.Norm(bearing + sign * off);
    }

    private bool BombardAim(DForm f, int side, out Vec2L aim)
    {
        if (f.DirTarget >= 0) return KnownCentroidAny(side, f.DirTarget, out aim);
        aim = f.DirPos;
        return true;
    }

    private void BombardIntent(DEnt d, DForm f, long vmax, out int heading, out long speed)
    {
        heading = d.Heading; speed = 0;
        if (!BombardAim(f, d.Side, out Vec2L aim)) return;
        WeaponSpec? w = null;
        foreach (var x in d.V!.Weapons) if ((x.Targets & TargetDomains.Land) != 0) { w = x; break; }
        if (w is null) return;
        long R = FixedMath.MulDivFloor(w.MaxRangeMm, _dr.PreferredRangePermille, 1000);
        long dist = Vec2L.Distance(d.Pos, aim);
        int bearing = FixedTrig.Atan2(aim.Y - d.Pos.Y, aim.X - d.Pos.X);
        if (w.Arc == MountArc.Broadside) { heading = Abeam(d.Heading, bearing, dist, R); speed = dist > R ? vmax : FixedMath.MulDivFloor(vmax, 1, 3); return; }
        heading = bearing;
        speed = dist > R ? Math.Min(vmax, HeadingMotion.BrakingSpeed(dist - R, d.V.AccelMmPerS2)) : 0;
    }

    // ------------------------------------------------------------------ aircraft

    private long BingoFuel(DEnt d) => checked(FixedMath.MulDivCeil(Vec2L.Distance(d.Pos, _airBase[d.Side]), 1000, d.A!.CruiseMmPerS) + _dr.AirReserveMs);

    private bool RacksEmpty(DEnt d)
    {
        bool limited = false;
        for (int m = 0; m < d.Ammo.Length; m++)
        {
            if (d.A!.Weapons[m].Ammo < 0) continue;
            limited = true;
            if (d.Ammo[m] > 0) return false;
        }
        return limited;
    }

    /// <summary>Fuel above bingo, airframe above the damage-abort threshold, and racks not empty.</summary>
    private bool AirCanContinue(DEnt d)
        => d.Fuel > BingoFuel(d)
           && AirFit(d)
           && !RacksEmpty(d);

    /// <summary>
    /// Airframe above the damage-abort threshold. Also the launch condition (N-R1-06): there is no repair in battle, so an
    /// aircraft that aborted for damage stays on the ground — a later order cannot relaunch it to spend a sortie and turn
    /// straight back (refused NotReady when no airframe of the formation is fit, ready and has a sortie left).
    /// </summary>
    private bool AirFit(DEnt d) => d.Hp * 1000 > d.A!.Hp * _dr.DamagedAbortPermille;

    private void MoveAircraft(long t)
    {
        long dt = _r.StepMs;
        // Aircraft may overfly the battle area's edge (they orbit and pursue); positions are bounded to the area plus a
        // margin (AirMarginMm) so the documented coordinate range always holds. Off the grid they cannot be seen from the
        // ground (LOS leaves the grid) — see the report's limitations.
        long M = AirMarginMm, W = _grid.Width * _grid.CellSizeMm - 1 + M, H = _grid.Height * _grid.CellSizeMm - 1 + M;
        foreach (var d in _d)
        {
            if (d.Dom != Domain.Air || d.State != EntityState.Active || d.Phase == AirPhase.Based) continue;
            var a = d.A!;
            var f = _df[d.F];
            if (d.Phase == AirPhase.Airborne && !AirCanContinue(d)) d.Phase = AirPhase.Returning;
            AirIntent(d, f, out Vec2L point, out long target, out bool hover);
            int want = point == d.Pos ? d.Heading : FixedTrig.Atan2(point.Y - d.Pos.Y, point.X - d.Pos.X);
            if (hover)
            {
                long dist = Vec2L.Distance(d.Pos, point);
                target = dist <= HoverToleranceMm ? 0 : Math.Min(target, HeadingMotion.BrakingSpeed(dist, a.AccelMmPerS2));
                if (dist <= HoverToleranceMm) want = d.Heading;          // arrived: settle into the hover, keep the heading
                // A helicopter can stop and turn in place: it slows with the bearing error (cos, floored at 0) instead of
                // orbiting its point.
                long cosErr = FixedMath.MulDivFloor(FixedTrig.Cos(FixedTrig.Diff(d.Heading, want)), 1000, FixedTrig.One);
                target = Math.Min(target, FixedMath.MulDivFloor(a.MaxSpeedMmPerS, Math.Max(0, cosErr), 1000));
            }
            d.Heading = HeadingMotion.TurnToward(d.Heading, want, HeadingMotion.MaxTurn(a.TurnRatePerS, dt));
            d.Speed = HeadingMotion.Approach(d.Speed, target, a.AccelMmPerS2, dt, a.MinSpeedMmPerS, a.MaxSpeedMmPerS);
            Vec2L np = d.Pos + HeadingMotion.Offset(d.Heading, HeadingMotion.Distance(d.Speed, dt));
            np = new Vec2L(Math.Clamp(np.X, -M, W), Math.Clamp(np.Y, -M, H));
            d.Moved = np != d.Pos;
            d.Pos = np;
            long floor = a.Kind == AirKind.Rotary ? FixedMath.MulDivFloor(a.CruiseMmPerS, a.HoverBurnPercent, 100) : a.MinSpeedMmPerS;
            long burn = FixedMath.MulDivCeil(dt, Math.Max(d.Speed, floor), a.CruiseMmPerS);
            d.Fuel -= Math.Max(1, burn);
            if (d.Fuel <= 0) { d.Fuel = 0; d.CrashPending = true; continue; }
            if (d.Phase == AirPhase.Returning && Vec2L.DistanceSq(d.Pos, _airBase[d.Side]) <= (Int128)_dr.AirLandRadiusMm * _dr.AirLandRadiusMm)
            {
                d.Phase = AirPhase.Based;
                d.Pos = _airBase[d.Side];
                d.Speed = 0;
                d.Fuel = a.EnduranceMs;
                d.ReadyAtMs = checked(t + a.TurnaroundMs);
                d.Gen = checked(d.Gen + 1);
                d.Primary = true;                     // bounded: one landing per sortie
                var k = _dk[1 - d.Side];
                int di = DIndex(d.Id);
                if (k.Tracked[di]) k.Tracked[di] = false;
            }
        }
    }

    /// <summary>A fixed-wing aircraft within this distance of its patrol point loiters at minimum speed (PLACEHOLDER).</summary>
    public const long LoiterRadiusMm = 2_000_000;

    /// <summary>A rotary-wing aircraft within this distance of its point hovers (PLACEHOLDER).</summary>
    public const long HoverToleranceMm = 25_000;

    /// <summary>How far beyond the battle area an aircraft may fly (PLACEHOLDER: about two turning circles of a fast jet).</summary>
    public const long AirMarginMm = 2_000_000;

    private void AirIntent(DEnt d, DForm f, out Vec2L point, out long speed, out bool hover)
    {
        var a = d.A!;
        hover = a.Kind == AirKind.Rotary;
        speed = a.CruiseMmPerS;
        point = d.Pos + HeadingMotion.Offset(d.Heading, 1_000_000);
        if (d.Phase == AirPhase.Returning) { point = _airBase[d.Side]; hover = false; return; }
        switch (f.Dir)
        {
            case OrderKind.Hold:
            case OrderKind.MoveTo:
            case OrderKind.Patrol:
                point = f.DirPos;
                // On station (within LoiterRadiusMm of its point) a fixed-wing aircraft loiters: it asks for no speed at all
                // (maximum endurance) and the minimum-speed floor of the airframe keeps it flying — circling its point, never
                // hovering. A rotary-wing aircraft hovers (see MoveAircraft).
                if (a.Kind == AirKind.FixedWing && Vec2L.DistanceSq(d.Pos, point) <= (Int128)LoiterRadiusMm * LoiterRadiusMm) speed = 0;
                return;
            case OrderKind.Attack:
            {
                int df = FindDForm(f.DirTarget);
                bool airTarget = df >= 0 && _df[df].Dom == Domain.Air;
                if (NearestTrackedMember(d.Side, f.DirTarget, d.Pos, out Vec2L tp, out _))
                {
                    point = tp;
                    if (airTarget) { speed = a.MaxSpeedMmPerS; hover = false; }
                    else if (hover) point = StandOff(d, tp);
                    return;
                }
                if (KnownCentroidAny(d.Side, f.DirTarget, out Vec2L c)) { point = hover ? StandOff(d, c) : c; return; }
                // Nothing known (never seen, or seen to fall): search over the front — the midpoint of the two air bases
                // (known geography, no hidden information).
                point = new Vec2L(FixedMath.DivFloor(_airBase[0].X + _airBase[1].X, 2), FixedMath.DivFloor(_airBase[0].Y + _airBase[1].Y, 2));
                return;
            }
            case OrderKind.Bombard:
                if (BombardAim(f, d.Side, out Vec2L aim)) point = hover ? StandOff(d, aim) : aim;
                return;
            case OrderKind.Escort:
            {
                // The escorted formation is gone (lost, landed, left): the escort mission is over — return to base.
                if (!OwnCentroid(f.DirTarget, out Vec2L c)) { d.Phase = AirPhase.Returning; point = _airBase[d.Side]; hover = false; return; }
                point = c;
                // Intercept: the nearest tracked enemy aircraft within escort radius (= own sight) of the escorted formation.
                var k = _dk[d.Side];
                Int128 best = (Int128)a.SightMm * a.SightMm; int bi = -1;
                for (int i = 0; i < _d.Length; i++)
                {
                    var x = _d[i];
                    if (x.Side == d.Side || x.Dom != Domain.Air || !x.OnField || !k.Tracked[i]) continue;
                    Int128 dd = Vec2L.DistanceSq(c, x.Pos);
                    if (dd <= best && (bi < 0 || dd < best)) { best = dd; bi = i; }
                }
                if (bi >= 0) { point = _d[bi].Pos; speed = a.MaxSpeedMmPerS; hover = false; }
                return;
            }
            default:
                return;
        }
    }

    /// <summary>Rotary wing: hold at 4/5 of its longest air-to-ground weapon range from the aim point (stand-off attack).</summary>
    private static Vec2L StandOff(DEnt d, Vec2L aim)
    {
        long r = 0;
        foreach (var w in d.A!.Weapons) if ((w.Targets & (TargetDomains.Land | TargetDomains.Surface)) != 0) r = Math.Max(r, w.MaxRangeMm);
        r = FixedMath.MulDivFloor(r, 4, 5);
        long dist = Vec2L.Distance(aim, d.Pos);
        if (dist <= r) return d.Pos;
        int bearing = FixedTrig.Atan2(d.Pos.Y - aim.Y, d.Pos.X - aim.X);
        return aim + HeadingMotion.Offset(bearing, r);
    }

    // ------------------------------------------------------------------ transport operations

    /// <summary>
    /// Embark point of a transport formation: the passable land cell nearest its lead hull, key (distance², cell index), whose
    /// centre lies within embark range of that hull. False when none (the transport is not alongside a shore).
    /// </summary>
    private bool EmbarkPoint(DForm t, out Vec2L pt)
    {
        pt = default;
        int lead = LeadVessel(t);
        if (lead < 0) return false;
        return NearestLand(_d[lead].Pos, _dr.EmbarkRangeMm, out pt, out _);
    }

    private bool NearestLand(Vec2L from, long range, out Vec2L pt, out int cell)
    {
        pt = default; cell = -1;
        Int128 best = (Int128)range * range;
        bool found = false;
        var c0 = _grid.CellOf(from);
        long rc = range / _grid.CellSizeMm + 1;
        for (long y = c0.Y - rc; y <= c0.Y + rc; y++)
            for (long x = c0.X - rc; x <= c0.X + rc; x++)
            {
                if (!_grid.IsPassable(_costs, x, y)) continue;
                Vec2L cc = _grid.CellCenter(x, y);
                Int128 dd = Vec2L.DistanceSq(from, cc);
                int idx = _grid.Index(x, y);
                if (dd > best || (found && dd == best && idx > cell)) continue;
                best = dd; pt = cc; cell = idx; found = true;
            }
        return found;
    }

    // Per-step scratch of TransportOps (not state: cleared at the start of every call). Verification round 1, N-R1-01.
    private bool[] _opCredited = Array.Empty<bool>();
    private bool[] _opBound = Array.Empty<bool>();

    /// <summary>
    /// A hull's loading time is ONE budget per step, shared by every formation it embarks or lands and by both operations
    /// (N-R1-01: crediting a full step per formation let a split force multiply a single hull's rate). The hull is credited
    /// <c>dt</c> once, the first time it works in the step; every person boarded or landed spends EmbarkMsPerPerson /
    /// DisembarkMsPerPerson from the same carry, in the fixed order (embarking formations ascending, then landing formations
    /// ascending; hulls ascending). Idle time is not banked: unless the time budget itself is what stopped the hull in this
    /// step (more people were waiting than its time allowed), the carry is cleared at the end of the step.
    /// </summary>
    private void CreditHull(int vi, long dt)
    {
        if (_opCredited[vi]) return;
        _opCredited[vi] = true;
        _d[vi].OpCarryMs = checked(_d[vi].OpCarryMs + dt);
    }

    private void TransportOps(long t)
    {
        long dt = _r.StepMs;
        if (_opCredited.Length != _d.Length) { _opCredited = new bool[_d.Length]; _opBound = new bool[_d.Length]; }
        Array.Clear(_opCredited);
        Array.Clear(_opBound);
        // Embarking: formations in ascending index; their transports' hulls in ascending id.
        for (int fi = 0; fi < _f.Length; fi++)
        {
            int ti = _embarkOnto[fi];
            if (ti < 0) continue;
            var lf = _f[fi];
            foreach (int vi in _df[ti].Members)
            {
                var v = _d[vi];
                if (v.State != EntityState.Active || v.Speed > _dr.StoppedSpeedMmPerS) continue;
                long space = v.V!.Capacity - v.Embarked;
                if (space <= 0) continue;
                var cand = new List<int>();
                Int128 r2 = (Int128)_dr.EmbarkRangeMm * _dr.EmbarkRangeMm;
                foreach (int ei in lf.Members)
                    if (_e[ei].State == EntityState.Active && Vec2L.DistanceSq(_e[ei].Pos, v.Pos) <= r2) cand.Add(ei);
                if (cand.Count == 0) continue;
                CreditHull(vi, dt);
                long byTime = v.OpCarryMs / v.V.EmbarkMsPerPerson;
                long n = Math.Min(Math.Min(byTime, cand.Count), space);
                for (int k = 0; k < n; k++) SetEmbarkState(_e[cand[k]], EntityState.Embarked, vi);
                v.OpCarryMs -= n * v.V.EmbarkMsPerPerson;
                if (n == byTime && n < cand.Count && n < space) _opBound[vi] = true;     // time ran out with people waiting
            }
            if (CountState(lf, EntityState.Active) == 0)
            {
                _embarkOnto[fi] = -1;
                lf.Dir = OrderKind.Hold;
                ClearPath(lf);
            }
        }
        // Landing: formations with a standing Disembark order.
        for (int fi = 0; fi < _f.Length; fi++)
        {
            if (!_landingSet[fi]) continue;
            var lf = _f[fi];
            for (int vi = 0; vi < _d.Length; vi++)
            {
                var v = _d[vi];
                if (v.Dom != Domain.Naval || v.State != EntityState.Active || v.Speed > _dr.StoppedSpeedMmPerS) continue;
                var aboard = new List<int>();
                foreach (int ei in lf.Members) if (_carrier[ei] == vi) aboard.Add(ei);
                if (aboard.Count == 0) continue;
                if (!NearestLand(v.Pos, _dr.LandingRangeMm, out _, out _)) continue;
                CreditHull(vi, dt);
                long byTime = v.OpCarryMs / v.V!.DisembarkMsPerPerson;
                long n = Math.Min(byTime, aboard.Count);
                long landed = 0;
                for (int k = 0; k < n; k++)
                {
                    if (!LandingSpot(v.Pos, out Vec2L spot)) break;
                    var e = _e[aboard[k]];
                    bool first = CountState(lf, EntityState.Active) == 0;
                    SetEmbarkState(e, EntityState.Active, vi);
                    e.Pos = spot;
                    e.RevealUntilMs = t + _dr.LandingExposureMs;
                    e.Moved = true; e.WantedMove = false;
                    e.LastStepMm = 0;                                      // [TR] verify-TR-r1 V13: a landing is not a run-up
                    landed++;
                    if (first)
                    {
                        lf.Anchor = spot;
                        var pr = _pf.FindPath(_grid.CellOf(spot), _grid.CellOf(_landingTo[fi]));
                        if (pr.Found)
                        {
                            var path = PathMovement.Waypoints(_grid, pr.Cells);
                            path[^1] = _landingTo[fi];
                            lf.Dir = OrderKind.MoveTo; lf.DirPos = _landingTo[fi]; lf.Path = path; lf.PathIdx = 0; lf.PathTargetCell = -1;
                        }
                        else { lf.Dir = OrderKind.Hold; ClearPath(lf); }
                    }
                }
                v.OpCarryMs -= landed * v.V.DisembarkMsPerPerson;
                if (landed == n && n == byTime && n < aboard.Count) _opBound[vi] = true;   // time ran out with people aboard
            }
            bool any = false;
            foreach (int ei in lf.Members) if (_e[ei].State == EntityState.Embarked) any = true;
            if (!any) _landingSet[fi] = false;
        }
        // No banking of idle time: a hull that worked this step keeps its carry only if time was what stopped it.
        for (int vi = 0; vi < _opCredited.Length; vi++)
            if (_opCredited[vi] && !_opBound[vi]) _d[vi].OpCarryMs = 0;
    }

    /// <summary>
    /// A free landing point near a hull: passable land cells whose centre is within landing range, ascending (distance², cell
    /// index); in each, a 5 x 5 lattice of points at slot spacing around the centre, ascending (offset distance², dy, dx); the
    /// first point whose disc overlaps no on-field land entity.
    /// </summary>
    private bool LandingSpot(Vec2L from, out Vec2L spot)
    {
        spot = default;
        var cells = new List<(Int128 D, int Idx)>();
        var c0 = _grid.CellOf(from);
        long rc = _dr.LandingRangeMm / _grid.CellSizeMm + 1;
        Int128 r2 = (Int128)_dr.LandingRangeMm * _dr.LandingRangeMm;
        for (long y = c0.Y - rc; y <= c0.Y + rc; y++)
            for (long x = c0.X - rc; x <= c0.X + rc; x++)
            {
                if (!_grid.IsPassable(_costs, x, y)) continue;
                Int128 dd = Vec2L.DistanceSq(from, _grid.CellCenter(x, y));
                if (dd <= r2) cells.Add((dd, _grid.Index(x, y)));
            }
        cells.Sort(static (a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Idx.CompareTo(b.Idx));
        long sp = Math.Max(_r.SlotSpacingMm, 2 * _r.EntityRadiusMm + 1);
        foreach (var (_, idx) in cells)
        {
            Vec2L cc = _grid.CellCenter(_grid.IndexX(idx), _grid.IndexY(idx));
            for (int ring = 0; ring <= 2; ring++)
                for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        Vec2L p = new(cc.X + dx * sp, cc.Y + dy * sp);
                        var pc = _grid.CellOf(p);
                        if (!_grid.IsPassable(_costs, pc.X, pc.Y)) continue;
                        bool free = true;
                        foreach (var e in _e)
                            if (e.OnField && Separation.Overlaps(e.Pos, _r.EntityRadiusMm, p, _r.EntityRadiusMm)) { free = false; break; }
                        if (free) { spot = p; return true; }
                    }
        }
        return false;
    }

    // ------------------------------------------------------------------ activity minima (inactivity rule)

    private void DomainMinima()
    {
        long S = _grid.CellSizeMm;
        for (int i = 0; i < _d.Length; i++)
        {
            var d = _d[i];
            if (!d.OnField) continue;
            long near = NearestTrackedEnemyDistance(d);
            if (near >= 0) TrackD(ref d.BestEnemy, near, S, d);
            var objs = _setup.Sides[d.Side].Objectives;
            if (objs.Length > 0)
            {
                long best = long.MaxValue;
                foreach (var o in objs) best = Math.Min(best, Vec2L.Distance(d.Pos, o));
                TrackD(ref d.BestObj, best, S, d);
            }
            if (d.Dom == Domain.Naval && (d.State == EntityState.Routing || _df[d.F].Dir == OrderKind.Retreat))
                TrackD(ref d.BestEdge, _navEdge![d.Side].LineDistanceMm(d.Pos), S, d);
        }
    }

    private static void TrackD(ref long best, long dist, long cell, DEnt d)
    {
        if (best < 0) { best = dist; return; }
        if (dist <= best - cell) { best = dist; d.Primary = true; }
    }

    private long NearestTrackedEnemyDistance(DEnt d)
    {
        Int128 best = -1;
        var k = _k[d.Side];
        for (int i = 0; i < _e.Length; i++)
        {
            if (_e[i].Side == d.Side || !_e[i].OnField || !k.Tracked[i]) continue;
            Int128 dd = Vec2L.DistanceSq(d.Pos, _e[i].Pos);
            if (best < 0 || dd < best) best = dd;
        }
        var dk = _dk[d.Side];
        for (int i = 0; i < _d.Length; i++)
        {
            if (_d[i].Side == d.Side || !_d[i].OnField || !dk.Tracked[i]) continue;
            Int128 dd = Vec2L.DistanceSq(d.Pos, _d[i].Pos);
            if (best < 0 || dd < best) best = dd;
        }
        return best < 0 ? -1 : (long)FixedMath.ISqrt((UInt128)best);
    }
}
