using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>
/// RAM LANE — Director ruling D-081 (2026-11-05): a ship rams a given ship at most once (RM1), may steer away and ram another
/// (RM2), needs ramming speed built from its own acceleration over a run-up (RM3), with real-world mechanics (RM4).
/// One semantic test per rule; every battle test checks the personnel ledger at every completed instant.
/// </summary>
public class RamTests
{
    // TEST INPUTS: a galley without deck archers (so only ram effects touch hulls) and an unarmed, ram-less hulk that stays
    // afloat (hull 1000) — both keep every D-081 ram value of DomainProfiles.Galley.
    private static readonly VesselClass Rammer = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>() };
    private static readonly VesselClass Hulk = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0, Hull = 1000 };
    private static readonly VesselClass G = DomainProfiles.Galley;
    private const long Y0 = 1_000_000, X0 = 1_000_000, Contact = 16_000;   // two galley radii

    /// <summary>Open sea, side 0 faces east (heading 0), side 1 faces north (heading 90 deg: its keel lies across an eastward
    /// run, so an eastward ram strikes its beam perpendicularly).</summary>
    private static BattleSetup Beam(ulong seed, params FormationSpec[] forms)
        => new(MultiDomainScenarios.OpenSea(50_000), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
               new[] { new SideSpec(ExitEdge.West), new SideSpec(ExitEdge.North) }, forms, seed)
        { MultiDomain = new MultiDomainSetup { Rules = new DomainRules() } };

    private static FormationSpec Hulks(long id, params (long Id, Vec2L Pos)[] hulls)
        => MultiDomainScenarios.Fleet(id, 1, Hulk, hulls.Length, hulls[0].Pos) with
        { Entities = hulls.Select(h => new EntitySpec(h.Id, h.Pos)).ToArray() };

    /// <summary>Runs until stop() holds, asserting the personnel ledger at every completed instant.</summary>
    private static void Watch(TacticalBattle b, Func<bool> stop, Action? each = null, long maxPulses = 60)
        => RunUntil(b, () =>
        {
            var err = b.PersonnelConsistency();
            Assert.True(err is null, $"personnel ledger inconsistent at {b.Scheduler.NowMs}: {err}");
            each?.Invoke();
            return stop();
        }, maxPulses);

    private static long Gap(TacticalBattle b, long a, long c) => Vec2L.Distance(D(b, a).Pos, D(b, c).Pos) - Contact;

    /// <summary>Independent reference of the run-up integration (not RamPhysics): smallest start gap from rest from which the
    /// hull is still out of beak contact at the end of every step before the one that brings it to ramming speed.</summary>
    private static long ReferenceRunUpGap(VesselClass v, long stepMs)
    {
        long dv = v.AccelMmPerS2 * stepMs / 1000, sp = 0, x = 0;
        while (true)
        {
            long next = Math.Min(v.MaxSpeedMmPerS, sp + dv);
            if (next >= v.RamSpeedMmPerS) return x + v.RamLengthMm + 1;
            x += next * stepMs / 1000;
            sp = next;
        }
    }

    private static TacticalBattle Duel(long gap, VesselClass? target = null)
    {
        var s = Beam(4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, target ?? Hulk, new Vec2L(X0 + Contact + gap, Y0)));
        return new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
    }

    // ================================================================== RM4: the pure equations (exact, fixed point)

    [Fact]
    public void ClassificationIsExactAtTheRammingSpeedBoundary()
    {
        long vr = G.RamSpeedMmPerS;
        int glance = new DomainRules().RamGlanceAngle;
        foreach (long s in new[] { vr - 1, vr, vr + 1 })
        {
            // eastward strike on a hull lying north-south, at rest: closing = normal = s exactly, angle 90 deg
            var imp = RamPhysics.Classify(new Vec2L(0, 0), 0, s, new Vec2L(20_000, 0), FixedTrig.Quarter, 0, vr, glance);
            Assert.Equal(s, imp.ClosingMmPerS);
            Assert.Equal(s, imp.NormalMmPerS);
            Assert.Equal(0, imp.TangentialMmPerS);
            Assert.Equal(FixedTrig.Quarter, imp.ImpactAngle);
            Assert.Equal(s >= vr, imp.IsRam);                                          // below ramming speed: a collision
            Assert.Equal(s >= vr, imp.Breach);
        }
        // the target's own motion adds to / subtracts from the closing speed of the centres — but [r2 RAM2-V1, V2] the ram gate
        // is the NORMAL speed, which only the rammer's own way supplies: a hull steaming bow-on INTO a rammer below ramming speed
        // (v_c = v_ram exactly) makes no ram (round 1 counted it one), nor does a stern chase on a fleeing hull
        var into = RamPhysics.Classify(new Vec2L(0, 0), 0, vr - 500, new Vec2L(20_000, 0), FixedTrig.HalfTurn, 500, vr, glance);
        Assert.Equal(vr, into.ClosingMmPerS);
        Assert.Equal(0, into.NormalMmPerS);
        Assert.False(into.IsRam);
        var away = RamPhysics.Classify(new Vec2L(0, 0), 0, vr + 499, new Vec2L(20_000, 0), 0, 500, vr, glance);
        Assert.Equal(vr - 1, away.ClosingMmPerS);
        Assert.False(away.IsRam);
    }

    [Fact]
    public void GlancingBlowsDeflectAndThePerpendicularBlowIsTheStrongest()
    {
        long vr = G.RamSpeedMmPerS, v = G.MaxSpeedMmPerS;
        int glance = new DomainRules().RamGlanceAngle;
        // Target keel at angle k to an eastward full-speed run: impact angle = k (target at rest).
        RamImpact At(long deg, long speed, int g) => RamPhysics.Classify(new Vec2L(0, 0), 0, speed, new Vec2L(20_000, 0), FixedTrig.FromDegrees(deg), 0, vr, g);
        long prev = -1;
        foreach (long deg in new long[] { 25, 35, 45, 60, 75, 90 })
        {
            var imp = At(deg, v, glance);
            Assert.True(imp.IsRam && imp.Breach, $"{deg} deg");
            Assert.Equal(FixedTrig.ScaleSin(v, FixedTrig.FromDegrees(deg)), imp.NormalMmPerS);
            long dmg = RamPhysics.EnergyScaled(G.RamDamage, imp.NormalMmPerS, v);
            Assert.True(dmg > prev, $"damage must grow toward the perpendicular ({deg} deg: {dmg} after {prev})");
            prev = dmg;
        }
        Assert.Equal(G.RamDamage, prev);                                               // perpendicular at MaxSpeed = RamDamage
        // [r2 RAM2-V1] A full-speed run at 15 deg to the keel drives the beak into the side at only 1 190 mm/s: not a ram at all
        // (round 1: a ram that glanced, by the centre-line closing speed).
        var skid = At(15, v, glance);
        Assert.Equal(FixedTrig.ScaleSin(v, FixedTrig.FromDegrees(15)), skid.NormalMmPerS);
        Assert.False(skid.IsRam);
        Assert.False(skid.Breach);
        // A true glance: ramming speed into the side, but the target steaming toward the blow along its keel (155 deg, 2 m/s)
        // makes the beak slide (v_t 6 168): impact angle 17.5 deg < 20 — a ram that skids. The same strike on the hull lying
        // still (v_t 4 169, 25 deg) breaches: the target's own motion enters through the impact angle only.
        var toward = RamPhysics.Classify(new Vec2L(0, 0), 0, v, new Vec2L(20_000, 0), FixedTrig.FromDegrees(155), 2_000, vr, glance);
        Assert.True(toward.NormalMmPerS >= vr && toward.IsRam && !toward.Breach, $"v_n {toward.NormalMmPerS} v_t {toward.TangentialMmPerS}");
        Assert.True(At(155, v, glance).Breach);
        Assert.Equal(toward.NormalMmPerS, At(155, v, glance).NormalMmPerS);
        // The angle floor on its own (TEST INPUT glance angle 40 deg): at 30 deg the normal speed 2 300 >= ramming speed,
        // yet the blow glances; at 50 deg it breaches.
        int g40 = FixedTrig.FromDegrees(40);
        Assert.True(At(30, v, g40).NormalMmPerS >= vr);
        Assert.False(At(30, v, g40).Breach);
        Assert.True(At(50, v, g40).Breach);
        // An oblique blow needs more own way: at 30 deg, 3 000 mm/s is no ram at all (normal 1 500 < 1 542; [r2] round 1 called
        // it a glancing ram) and 3 100 mm/s breaches (normal 1 550).
        Assert.False(At(30, 3_000, glance).Breach);
        Assert.False(At(30, 3_000, glance).IsRam);
        Assert.True(At(30, 3_100, glance).Breach);
    }

    [Fact]
    public void EveryRamEffectScalesWithTheKineticEnergyOfTheNormalSpeed()
    {
        long v = G.MaxSpeedMmPerS;
        Assert.Equal(90, RamPhysics.EnergyScaled(90, v, v));
        Assert.Equal(22, RamPhysics.EnergyScaled(90, v / 2, v));                     // half speed: a quarter of the energy
        Assert.Equal(13, RamPhysics.EnergyScaled(90, 1_760, v));
        Assert.Equal(0, RamPhysics.EnergyScaled(90, 0, v));
        Assert.Equal(9, RamPhysics.EnergyScaledPermille(90, 100, v, v));
        for (long s = 0; s <= 2 * v; s += 37)
        {
            var exact = (System.Numerics.BigInteger)90 * s * s / ((System.Numerics.BigInteger)v * v);
            Assert.Equal((long)exact, RamPhysics.EnergyScaled(90, s, v));
            var bow = (System.Numerics.BigInteger)90 * 100 * s * s / ((System.Numerics.BigInteger)1000 * v * v);
            Assert.Equal((long)bow, RamPhysics.EnergyScaledPermille(90, 100, s, v));
        }
    }

    [Fact]
    public void RunUpAndFreeTimeFollowTheKinematics()
    {
        long step = new TacticalRules().StepMs;
        long g = RamPhysics.RunUpGapMm(0, G.RamSpeedMmPerS, G.MaxSpeedMmPerS, G.AccelMmPerS2, step, G.RamLengthMm);
        Assert.Equal(ReferenceRunUpGap(G, step), g);
        Assert.Equal(14_321, g);                                                      // 220 mm/s per 2 s step; 7 steps of run-up
        // continuous d = v^2 / 2a = 10 808 mm; the integrated run-up lies within one step of it (plus the beak's reach)
        long cont = G.RamSpeedMmPerS * G.RamSpeedMmPerS / (2 * G.AccelMmPerS2);
        Assert.InRange(g - G.RamLengthMm - 1, cont - HeadingMotion.Distance(G.RamSpeedMmPerS, step), cont + HeadingMotion.Distance(G.RamSpeedMmPerS + 220, step));
        Assert.Equal(0, RamPhysics.RunUpGapMm(G.RamSpeedMmPerS, G.RamSpeedMmPerS, G.MaxSpeedMmPerS, G.AccelMmPerS2, step, G.RamLengthMm));
        Assert.Equal(long.MaxValue, RamPhysics.RunUpGapMm(0, G.RamSpeedMmPerS, G.RamSpeedMmPerS - 1, G.AccelMmPerS2, step, G.RamLengthMm));
        // backing a 2 m beak out at half the forward acceleration (55 mm/s^2): t = ceil(sqrt(2 * 2 000 / 55) s) = 8 529 ms
        long t = RamPhysics.FreeRamMs(2_000, 55);
        Assert.Equal(8_529, t);
        Assert.True((Int128)t * t * 55 >= (Int128)2 * 2_000 * 1_000_000 && (Int128)(t - 1) * (t - 1) * 55 < (Int128)2 * 2_000 * 1_000_000);
        Assert.True(RamPhysics.FreeRamMs(292, 55) < t);                               // a lighter blow embeds less, frees sooner
    }

    /// <summary>RM2 target choice: composite key (distance², id); tie-dense (every candidate at the same distance), input order
    /// irrelevant, rammed hulls skipped.</summary>
    [Fact]
    public void RamTargetChoiceIsTieDenseLowestUnrammedId()
    {
        var ids = new long[] { 2011, 2003, 2007, 2001, 2009, 2005, 2004, 2010, 2002, 2006, 2008, 2000 };
        var d = Enumerable.Repeat((Int128)250_000_000, ids.Length).ToArray();
        for (int trial = 0; trial < 2 * ids.Length; trial++)
        {
            // every rotation of the list, forward and reversed (24 input orders)
            var perm = Enumerable.Range(0, ids.Length).Select(k => (k + trial) % ids.Length).ToArray();
            if (trial >= ids.Length) Array.Reverse(perm);
            var pid = perm.Select(i => ids[i]).ToArray();
            var pd = perm.Select(i => d[i]).ToArray();
            var all = pid.Select(_ => true).ToArray();
            Assert.Equal(2000, pid[RamPhysics.ArgMinTarget(pid, pd, all)]);
            var notRammed = pid.Select(x => x != 2000 && x != 2001 && x != 2003).ToArray();   // 2000, 2001, 2003 already rammed
            Assert.Equal(2002, pid[RamPhysics.ArgMinTarget(pid, pd, notRammed)]);
            var nearer = (Int128[])pd.Clone();
            nearer[Array.IndexOf(pid, 2009)] -= 1;                                    // one millimetre² nearer beats any id
            Assert.Equal(2009, pid[RamPhysics.ArgMinTarget(pid, nearer, notRammed)]);
        }
        Assert.Equal(-1, RamPhysics.ArgMinTarget(ids, d, new bool[ids.Length]));
    }

    /// <summary>The same choice inside a battle: five hulls (the Ships cap) at EXACTLY the same distance (Pythagorean lattice,
    /// radius 100 m) with scrambled ids — the planner's target is the lowest id, whatever the input order.</summary>
    [Fact]
    public void TheBattlePlannerPicksTheLowestIdAmongEquidistantHulls()
    {
        var pts = new (long X, long Y)[] { (5, 0), (-3, 4), (0, -5), (4, 3), (-4, -3) };
        var ids = new long[] { 2003, 2001, 2004, 2000, 2002 };
        long Run(bool reversed)
        {
            var hulls = pts.Select((p, k) => (Id: ids[k], Pos: new Vec2L(X0 + p.X * 20_000, Y0 + p.Y * 20_000))).ToArray();
            foreach (var h in hulls) Assert.Equal((Int128)100_000 * 100_000, Vec2L.DistanceSq(new Vec2L(X0, Y0), h.Pos));
            if (reversed) Array.Reverse(hulls);
            var s = Beam(9, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Hulks(2, hulls));
            if (reversed) s = s with { Formations = s.Formations.Reverse().ToArray() };
            var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
            Watch(b, () => b.RamTargetOf(1000) >= 0, maxPulses: 2);
            return b.RamTargetOf(1000);
        }
        Assert.Equal(2000, Run(false));
        Assert.Equal(2000, Run(true));
    }

    // ================================================================== RM3: ramming speed from a run-up

    /// <summary>
    /// RM3 boundary, exact in fixed point: from rest, pointed at a hull lying across its course, with the run-up gap g* given by
    /// the step integration the hull rams on its straight run at closing speed &gt;= ramming speed; with g* - 1 mm it does not
    /// press in at all (no ram, no low-speed contact on the direct run) — it turns away to open the range.
    /// </summary>
    [Fact]
    public void ARunUpJustShortOfTheRequiredDistanceDoesNotRamJustEnoughDoes()
    {
        long step = new TacticalRules().StepMs;
        long gStar = ReferenceRunUpGap(Rammer, step);
        Assert.Equal(RamPhysics.RunUpGapMm(0, Rammer.RamSpeedMmPerS, Rammer.MaxSpeedMmPerS, Rammer.AccelMmPerS2, step, Rammer.RamLengthMm), gStar);

        // Just enough: a straight run (heading 0 throughout) that rams at the first contact, at ramming speed or above.
        var b = Duel(gStar);
        bool turned = false;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), () => { if (D(b, 1000).Heading != 0) turned = true; }, 10);
        var ram = Executed(b, DomainKinds.Ram).Single();
        Assert.False(turned);
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
        Assert.Equal(2000, ram.Target.Id);
        Assert.Equal((long)RamOutcome.Breach, ram.A);
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.Equal(1_760, ram.C);                                                    // 8 steps of 220 mm/s: the first >= 1 542
        Assert.Equal(ram.C, ram.B);                                                    // perpendicular: all of it normal
        long tRam = ram.Key.TimeMs;

        // Just short: the same run would arrive below ramming speed, so the hull never presses in — no contact at all within
        // the direct run's time and two steps more, and it turns away (opening the range) from its first move.
        var c = Duel(gStar - 1);
        bool cTurned = false;
        Watch(c, () => c.Scheduler.NowMs >= tRam + 2 * step, () => { if (D(c, 1000).Heading != 0) cTurned = true; }, 10);
        Assert.Empty(Executed(c, DomainKinds.Ram));
        Assert.Empty(Executed(c, DomainKinds.RamCollision));
        Assert.True(cTurned);
        Assert.True(Gap(c, 1000, 2000) > gStar - 1, "the hull opened the range");
    }

    /// <summary>RM3 manoeuvre: started 5 m off a hull's beam at rest, the ram directive does not press in at low speed; it
    /// opens the range to at least D_open, turns in and strikes at ramming speed.</summary>
    [Fact]
    public void TheRamDirectiveOpensTheRangeAndReachesRammingSpeed()
    {
        var b = Duel(5_000);
        long open = b.RamOpenGapMm(1000), maxGap = 0;
        Assert.True(open > ReferenceRunUpGap(Rammer, new TacticalRules().StepMs));
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), () => maxGap = Math.Max(maxGap, Gap(b, 1000, 2000)), 30);
        var ram = Executed(b, DomainKinds.Ram).Single();
        Assert.Empty(Executed(b, DomainKinds.RamCollision));                          // never pressed in below ramming speed
        Assert.True(maxGap >= open, $"opened to {maxGap}, D_open {open}");
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.Equal((long)RamOutcome.Breach, ram.A);
    }

    // ================================================================== RM1, RM-R, RM2

    /// <summary>
    /// RM1, built explicitly: the rammer rams hull 2000 (TEST INPUT: a large hulk, radius 30 m), backs its beak out, opens
    /// the range, turns in and runs at the formation's other hull 2001 (same class), which lies beyond 2000 — so it rebuilds full ramming
    /// speed and strikes 2000 AGAIN. That second contact is not a ram (no second Ram event, no further hull damage to 2000):
    /// it is a collision.
    /// </summary>
    [Fact]
    public void AShipRamsAGivenShipOnlyOnceEvenAtFullRammingSpeedAgain()
    {
        var big = Hulk with { RadiusMm = 30_000 };
        long r2 = Rammer.RadiusMm + big.RadiusMm, x1 = X0 + r2 + 120_000;
        var two = MultiDomainScenarios.Fleet(2, 1, big, 2, new Vec2L(x1, Y0)) with
        { Entities = new[] { new EntitySpec(2000, new Vec2L(x1, Y0)), new EntitySpec(2001, new Vec2L(x1 + 80_000, Y0)) } };
        var s = Beam(5, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), two);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long targetAtRecontact = -1;
        long open = b.RamOpenGapMm(1000), maxGapAfter = 0;
        long GapBig() => Vec2L.Distance(D(b, 1000).Pos, D(b, 2000).Pos) - r2;
        Watch(b, () =>
        {
            var rams = Executed(b, DomainKinds.Ram).ToArray();
            bool recontact = rams.Length > 0 && Executed(b, DomainKinds.RamCollision).Any(e => e.Target.Id == 2000 && e.Key.TimeMs > rams[0].Key.TimeMs);
            if (recontact) targetAtRecontact = b.RamTargetOf(1000);
            return recontact;
        }, () => { if (Executed(b, DomainKinds.Ram).Any()) maxGapAfter = Math.Max(maxGapAfter, GapBig()); }, 40);
        var first = Executed(b, DomainKinds.Ram).Single();
        Assert.Equal(2000, first.Target.Id);
        Assert.Equal(2001, targetAtRecontact);                                         // it was running at the other hull
        var again = Executed(b, DomainKinds.RamCollision).First(e => e.Target.Id == 2000 && e.Key.TimeMs > first.Key.TimeMs);
        Assert.True(again.B >= Rammer.RamSpeedMmPerS, $"premise: the second contact is at ramming speed ({again.B})");
        Assert.True(maxGapAfter >= open, $"premise: the rammer backed off and opened the range ({maxGapAfter} < {open})");
        Assert.True(b.HasRammed(1000, 2000));
        Assert.Equal(new long[] { 2000 }, b.RammedBy(1000));
        long firstHull = Executed(b, DomainKinds.DomainHit).Single(e => e.Target.Id == 2000 && e.Source.Id == 1000 && e.A == 1).B;
        Assert.Equal(big.Hull - firstHull, D(b, 2000).Hp);                           // only the first ram holed it
    }

    /// <summary>RM2: after ramming 2000 the rammer backs its beak out, steers away from 2000 (opening to D_open) and then
    /// rams the nearest hull it has not rammed, 2001.</summary>
    [Fact]
    public void AfterARamTheShipSteersAwayAndRamsAnotherShip()
    {
        long x1 = X0 + Contact + 120_000;
        var s = Beam(6, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Hulks(2, (2000, new Vec2L(x1, Y0)), (2001, new Vec2L(x1, Y0 + 400_000))));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long open = b.RamOpenGapMm(1000), gapFromFirst = 0; bool switched = false;
        Watch(b, () => Executed(b, DomainKinds.Ram).Count() >= 2, () =>
        {
            if (!Executed(b, DomainKinds.Ram).Any()) return;
            gapFromFirst = Math.Max(gapFromFirst, Gap(b, 1000, 2000));
            if (b.RamTargetOf(1000) == 2001) switched = true;
        }, 40);
        var rams = Executed(b, DomainKinds.Ram).ToArray();
        Assert.Equal(new long[] { 2000, 2001 }, rams.Select(r => r.Target.Id).ToArray());
        Assert.True(rams[1].Key.TimeMs > rams[0].Key.TimeMs);
        Assert.True(switched);
        Assert.True(gapFromFirst >= open, $"steered away to {gapFromFirst} (D_open {open})");
        Assert.All(rams, r => Assert.True(r.B >= Rammer.RamSpeedMmPerS));
        Assert.Equal(new long[] { 2000, 2001 }, b.RammedBy(1000));
    }

    /// <summary>RM-R reading: "once" is per (rammer, target) pair — a different ship may still ram a hull already rammed.</summary>
    [Fact]
    public void ADifferentShipMayRamAHullAlreadyRammed()
    {
        long xt = X0 + Contact + 120_000;
        var s = Beam(8,
            Ship(1, 0, Rammer, new Vec2L(X0, Y0)),
            Ship(3, 0, Rammer, new Vec2L(xt + Contact + 120_000, Y0)),
            Hulks(2, (2000, new Vec2L(xt, Y0))));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2)), (0, new OrderRequest(3, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Watch(b, () => Executed(b, DomainKinds.Ram).Select(e => e.Source.Id).Distinct().Count() == 2, maxPulses: 30);
        var rams = Executed(b, DomainKinds.Ram).ToArray();
        Assert.Equal(2, rams.Length);
        Assert.All(rams, r => Assert.Equal(2000, r.Target.Id));
        Assert.Equal(new long[] { 1000, 3000 }, rams.Select(r => r.Source.Id).OrderBy(x => x).ToArray());
        Assert.True(b.HasRammed(1000, 2000) && b.HasRammed(3000, 2000));
    }

    // ================================================================== RM4: effects of a ram, in battle

    /// <summary>
    /// A full-speed perpendicular ram (run-up longer than vmax²/2a): the struck hull loses exactly RamDamage and RamCrewDamage
    /// people through the ledger; the rammer's bow takes its share; the beak is embedded 2 m and backing it out takes
    /// ceil(sqrt(2 d / a_back)) = 8 529 ms, during which the rammer is held at speed 0, cannot ram, and has no evasion
    /// (exposed). Turn-exact pin of the release, with ±1-step neighbours.
    /// </summary>
    [Fact]
    public void AFullSpeedRamHolesTheTargetDamagesTheBowAndHoldsTheRammerWhileItBacksOut()
    {
        // a second hulk 600 m north gives the freed rammer a next target (otherwise, with no weapons, it would just lie there)
        var s = Beam(4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)),
                     Hulks(2, (2000, new Vec2L(X0 + Contact + 150_000, Y0)), (2001, new Vec2L(X0 + Contact + 150_000, Y0 + 600_000))));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long step = new TacticalRules().StepMs;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), maxPulses: 10);
        var ram = Executed(b, DomainKinds.Ram).Single();
        long t = ram.Key.TimeMs;
        Assert.Equal(Rammer.MaxSpeedMmPerS, ram.C);
        Assert.Equal((long)RamOutcome.Breach, ram.A);
        long until = b.RamLockedUntilMs(1000);
        Assert.Equal(t + RamPhysics.FreeRamMs(Rammer.RamLengthMm, Rammer.AccelMmPerS2 * 500 / 1000), until);
        Assert.Equal(t + 8_529, until);
        var held = new SortedDictionary<long, (Vec2L P, int H, long V, long Ev)>();
        Watch(b, () => b.Scheduler.NowMs >= t + 6 * step, () =>
        {
            var d = D(b, 1000);
            held[b.Scheduler.NowMs] = (d.Pos, d.Heading, d.Speed, b.EvasionOf(1000));
        }, 10);
        var hit = Executed(b, DomainKinds.DomainHit).Single(e => e.Target.Id == 2000 && e.Source.Id == 1000);
        Assert.Equal(Rammer.RamDamage, hit.B);                                        // perpendicular, at MaxSpeed
        Assert.Equal(Rammer.RamCrewDamage, hit.C);
        Assert.Equal(Hulk.Hull - Rammer.RamDamage, D(b, 2000).Hp);
        Assert.Equal(Hulk.Crew - Rammer.RamCrewDamage, D(b, 2000).Crew);
        Assert.Equal(Rammer.RamCrewDamage, b.Personnel.Balance(2, PersonnelPlace.Killed));
        long bow = RamPhysics.EnergyScaledPermille(Rammer.RamDamage, Rammer.RamBowDamagePermille, ram.B, Rammer.MaxSpeedMmPerS);
        Assert.Equal(9, bow);
        Assert.Equal(Rammer.Hull - bow, D(b, 1000).Hp);
        // held while backing out: every step instant before `until` sees the hull still, at speed 0, with no evasion
        var atImpact = held[t];
        for (long k = 1; t + k * step < until; k++)
        {
            var h = held[t + k * step];
            Assert.Equal((atImpact.P, atImpact.H, 0L, 0L), (h.P, h.H, h.V, h.Ev));
        }
        long release = t + 5 * step;                                                   // first step instant >= t + 8 529
        Assert.True(release >= until && release - step < until);
        Assert.Equal(0, held[release - step].Ev);                                      // -1 step: still held
        Assert.Equal(atImpact.P, held[release - step].P);
        Assert.Equal(Rammer.Evasion, held[release].Ev);                                // release: free, evasive again
        Assert.NotEqual((atImpact.H, 0L), (held[release].H, held[release].V));        // turning / under way at release
        Assert.Single(Executed(b, DomainKinds.Ram));                                   // no ram while the beak was embedded
        Assert.Equal(0, held[release].V);                                              // it pivots away from the hull it touches,
        Assert.Empty(Executed(b, DomainKinds.RamCollision));                           // never making way into it
    }

    /// <summary>A rammer backing its beak out makes no strike: the struck hull, ordered to steer west into the locked rammer
    /// right after the impact, presses on its bow with a closing speed — no Ram or collision of the rammer's is recorded until
    /// its beak is free.</summary>
    [Fact]
    public void ARammerBackingItsBeakOutMakesNoStrike()
    {
        var s = Beam(4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   Script((0, new OrderRequest(2, OrderKind.MoveTo, 24, -1, new Vec2L(X0 - 200_000, Y0)))));
        bool pressed = false;
        Watch(b, () => b.Scheduler.NowMs >= 60_000 + s.StartMs, () =>
        {
            // the struck hull turns onto its westward course while the beak is still in it (its moves into the rammer's bow are
            // blocked by the separation step, so its speed reads 0 — its way speed is what closes on the rammer)
            if (Executed(b, DomainKinds.Ram).Any() && b.Scheduler.NowMs < b.RamLockedUntilMs(1000) && D(b, 2000).Heading != FixedTrig.Quarter) pressed = true;
        }, 3);
        var ram = Executed(b, DomainKinds.Ram).First();
        Assert.Equal(s.StartMs + 46_000, ram.Key.TimeMs);                              // premise: the order lands after the ram
        long until = b.RamLockedUntilMs(1000);
        Assert.True(pressed, "premise: the struck hull is steering into the locked rammer");
        Assert.DoesNotContain(Executed(b, DomainKinds.Ram).Concat(Executed(b, DomainKinds.RamCollision)),
                              e => e.Source.Id == 1000 && e.Key.TimeMs > ram.Key.TimeMs && e.Key.TimeMs < until);
    }

    /// <summary>
    /// The ram state is battle state: two duels identical except the backing acceleration (500 vs 499 permille, TEST INPUT)
    /// make the same full-speed ram and hold the rammer over the same steps (both release at the same step), so their traces
    /// are identical to well past the release — but the beak-lock instant differs (8 529 vs 8 607 ms after impact), and the
    /// state hash must see it from the impact on.
    /// </summary>
    [Fact]
    public void TheBeakLockIsPartOfTheStateHash()
    {
        (string Trace, string State, long Until, long T) Run(long permille)
        {
            var s = Beam(4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 150_000, Y0))) with
            { MultiDomain = new MultiDomainSetup { Rules = new DomainRules { BackingAccelPermille = permille } } };
            var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
            Watch(b, () => Executed(b, DomainKinds.Ram).Any(), maxPulses: 10);
            long t = Executed(b, DomainKinds.Ram).Single().Key.TimeMs;
            string atImpact = b.StateHashHex();
            Watch(b, () => b.Scheduler.NowMs >= t + 12_000, maxPulses: 10);
            return (b.Scheduler.Trace.HashHex(), atImpact, b.RamLockedUntilMs(1000), t);
        }
        var a = Run(500); var c = Run(499);
        Assert.Equal(a.T, c.T);
        Assert.Equal(a.T + 8_529, a.Until);
        Assert.Equal(a.T + 8_607, c.Until);                                          // a_back 54 mm/s^2
        Assert.Equal(a.Trace, c.Trace);                                               // same events, same held steps
        Assert.NotEqual(a.State, c.State);                                             // the lock is in the state hash
    }

    /// <summary>A rammer whose run is blocked by a hull it is not striking (here a friendly hulk lying on the line to its
    /// target) sheers off instead of pressing against it, and still gets its ram in: it is never held against the friend
    /// for more than one step.</summary>
    [Fact]
    public void AFouledRammerSheersOffAndStillRams()
    {
        var small = Hulk;
        var s = Beam(5, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(4, 0, small, new Vec2L(X0 + 60_000, Y0)), Ship(2, 1, small, new Vec2L(X0 + 150_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        int run = 0, maxRun = 0, touches = 0; long lastT = -1;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now % s.Rules.StepMs != 0 || now == lastT) return;
            lastT = now;
            bool touching = Gap(b, 1000, 4000) <= Rammer.RamLengthMm;
            if (touching) touches++;
            run = touching && D(b, 1000).Speed == 0 && D(b, 1000).Heading == 0 ? run + 1 : 0;   // pressed bow-on against it
            maxRun = Math.Max(maxRun, run);
        }, 12);
        Assert.True(touches > 0, "premise: the friend lies on the line and the rammer reaches it");
        Assert.True(maxRun <= 1, $"held against the friendly hull for {maxRun} steps");
        var ram = Executed(b, DomainKinds.Ram).Single();
        Assert.Equal(2000, ram.Target.Id);
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS);
    }

    /// <summary>Every contact of the AI-vs-AI galley line battle obeys D-081: no (rammer, target) pair twice; every ram at
    /// normal closing speed &gt;= ramming speed ([r2] was the centre-line speed); every collision below it or on a pair already rammed; breaches carry exactly the
    /// energy-scaled damage and glances none; both breaches and glances occur. The battle replays exactly from its order log
    /// and is pinned (trace golden, re-pinned for D-081 — see H-RAM.md).</summary>
    [Fact]
    public void GalleyLineBattleObeysTheRamRulesInEveryContactAndReplaysExactly()
    {
        var live = new TacticalBattle(MultiDomainScenarios.LineBattle(G, 50_000, 42), new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Attack, 0, 2)),
                                      new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Attack, 0, 1)));
        var r1 = RunChecked(live);
        var seen = new List<(long, long)>();
        int breach = 0, glance = 0;
        foreach (var e in live.Scheduler.Trace.Entries)
        {
            if (e.Outcome != TraceOutcome.Executed) continue;
            if (e.Kind == DomainKinds.Ram)
            {
                Assert.DoesNotContain((e.Source.Id, e.Target.Id), seen);
                seen.Add((e.Source.Id, e.Target.Id));
                Assert.True(e.B >= G.RamSpeedMmPerS);
                // hull-holing hits of this rammer on this target at this instant (kind 1, hull > 0; deck archers never hole a
                // hull, and a rammer's bow damage is kind 3 on the RAMMER)
                var hits = live.Scheduler.Trace.Entries.Where(h => h.Kind == DomainKinds.DomainHit && h.Outcome == TraceOutcome.Executed && h.A == 1 && h.B > 0
                    && h.Key.TimeMs == e.Key.TimeMs && h.Source.Id == e.Source.Id && h.Target.Id == e.Target.Id).ToArray();
                if (e.A == (long)RamOutcome.Breach)
                {
                    breach++;
                    Assert.True(e.B >= G.RamSpeedMmPerS);
                    var h1 = Assert.Single(hits);
                    Assert.Equal(Math.Max(1, RamPhysics.EnergyScaled(G.RamDamage, e.B, G.MaxSpeedMmPerS)), h1.B);
                    Assert.Equal(RamPhysics.EnergyScaled(G.RamCrewDamage, e.B, G.MaxSpeedMmPerS), h1.C);
                }
                else
                {
                    glance++;
                    Assert.Empty(hits);                                                  // a glancing blow does not hole
                }
            }
            else if (e.Kind == DomainKinds.RamCollision)
                Assert.True(e.B < G.RamSpeedMmPerS || seen.Contains((e.Source.Id, e.Target.Id)), $"collision at v_n {e.B} on an unrammed pair");
        }
        Assert.True(breach > 0 && glance > 0, $"premise: breaches {breach}, glances {glance}");
        var replay = new TacticalBattle(MultiDomainScenarios.LineBattle(G, 50_000, 42), ScriptedController.FromLog(live.Orders, 0), ScriptedController.FromLog(live.Orders, 1));
        var r2 = RunChecked(replay);
        Assert.Equal(r1.TraceHash, r2.TraceHash);
        Assert.Equal(r1.StateHash, r2.StateHash);
        Assert.Equal(r1.Personnel!.Hash, r2.Personnel!.Hash);
        Assert.Equal(GalleyTrace, r1.TraceHash);
    }

    /// <summary>Galley line battle (seed 42, both sides Attack at pulse 0) trace golden after D-081 and verification rounds 1
    /// and 2 (H-RAM.md §8, §13.4 and §14: attribution measured; round 2 moved it 01DF25FF… → 6EE765E5… through the
    /// normal-speed gate, v_n from the rammer's own velocity, the planner's s_req and the collision after-speed — each step
    /// of the revert chain changes it).</summary>
    public const string GalleyTrace = "6EE765E52F56CC3F34362203815DB9D8932F94B62F4D84E3D0DFD135F188B2EE";

    // [RAM r1 V7] ABoardingOrderNeverRams was REMOVED with the behaviour it pinned ("Board no longer rams", an undecided
    // change, now proposal P2 only): a boarding approach rams under the D-081 rules again — pinned by
    // RamVerificationRound1Tests.ABoardingApproachAtRammingSpeedRamsOnceThenGrapples.

    /// <summary>
    /// N-R2-07 replacement (lane N pinned a 30 s cooldown there; D-081 RM-R supersedes it). A galley rams a line of three
    /// hulls 40 m apart (TEST INPUT: unarmed, hull 50 — one full-speed ram sinks one): it rams each hull at most once, every
    /// ram at closing speed &gt;= ramming speed, and its rams are spaced by physics alone — each beak lock lasts exactly the free
    /// time of that blow's depth, and the next ram comes no earlier than the first step at which the lock has ended plus the
    /// steps a hull at rest needs to reach ramming speed (8 steps of 220 mm/s: the move of the 8th step is the run-in).
    /// </summary>
    [Fact]
    public void RamsOnALineOfHullsAreSpacedByTheFreeTimeAndTheRunUpNotByATimer()
    {
        var target = Hulk with { Hull = 50 };
        var tf = MultiDomainScenarios.Fleet(2, 1, target, 3, M(1110, 1000)) with
        { Entities = new[] { new EntitySpec(2000, M(1110, 960)), new EntitySpec(2001, M(1110, 1000)), new EntitySpec(2002, M(1110, 1040)) } };
        var s = Sea(50_000, 4, null, Ship(1, 0, Rammer, M(1000, 1000)), tf);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long step = s.Rules.StepMs;
        var locks = new List<(long T, long Until)>();
        Watch(b, () => b.CurrentPulse >= 6, () =>
        {
            var rams = Executed(b, DomainKinds.Ram).ToArray();
            if (rams.Length > locks.Count) locks.Add((rams[^1].Key.TimeMs, b.RamLockedUntilMs(1000)));
        }, 8);
        var all = Executed(b, DomainKinds.Ram).ToArray();
        Assert.Equal(3, all.Length);
        Assert.Equal(3, all.Select(r => r.Target.Id).Distinct().Count());               // RM1: each hull once
        Assert.All(all, r => Assert.True(r.B >= Rammer.RamSpeedMmPerS, $"normal {r.B}"));
        Assert.Equal(EntityState.Dead, DState(b, 2002));                                // premise: a ram sank a hull
        long aBack = Rammer.AccelMmPerS2 * new DomainRules().BackingAccelPermille / 1000;
        long stepsToRamSpeed = (Rammer.RamSpeedMmPerS + Rammer.AccelMmPerS2 * step / 1000 - 1) / (Rammer.AccelMmPerS2 * step / 1000);
        Assert.Equal(8, stepsToRamSpeed);
        for (int k = 0; k < all.Length; k++)
        {
            Assert.Equal(all[k].Key.TimeMs, locks[k].T);
            if (all[k].A == (long)RamOutcome.Breach)
            {
                long depth = Math.Max(1, Math.Min(Rammer.RamLengthMm, RamPhysics.EnergyScaled(Rammer.RamLengthMm, all[k].B, Rammer.MaxSpeedMmPerS)));
                Assert.Equal(all[k].Key.TimeMs + RamPhysics.FreeRamMs(depth, aBack), locks[k].Until);
            }
            if (k == 0 || all[k - 1].A != (long)RamOutcome.Breach) continue;          // after a breach the rammer starts from rest
            long release = (Math.Max(locks[k - 1].Until, all[k - 1].Key.TimeMs) + step - 1) / step * step;   // first step instant at or after the lock
            Assert.True(all[k].Key.TimeMs >= release + (stepsToRamSpeed - 1) * step,
                        $"ram {k} at {all[k].Key.TimeMs}: earlier than the free time ({locks[k - 1].Until}) plus the run-up");
        }
        Assert.Equal(new long[] { 2000, 2001, 2002 }, b.RammedBy(1000));
    }

    private static EntityState DState(TacticalBattle b, long id) => D(b, id).State;
}
