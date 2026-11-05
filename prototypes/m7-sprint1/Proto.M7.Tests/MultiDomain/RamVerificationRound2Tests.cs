using System.Reflection;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>
/// RAM LANE — verification round 2 (2026-11-05): one semantic test per reproduced finding (RAM2-V1 … RAM2-V7, see
/// reports/H-RAM.md "Verification round 2"). Every battle test checks the personnel ledger at every completed instant.
/// </summary>
public class RamVerificationRound2Tests
{
    // TEST INPUTS (as in RamTests): a galley without deck archers and an unarmed, ram-less hulk that stays afloat.
    private static readonly VesselClass Rammer = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>() };
    private static readonly VesselClass Hulk = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0, Hull = 1000 };
    private const long Y0 = 1_000_000, X0 = 1_000_000, Contact = 16_000;   // two galley radii
    private static readonly long Step = new TacticalRules().StepMs;
    private static long Vr => Rammer.RamSpeedMmPerS;

    private static BattleSetup Two(ExitEdge e0, ExitEdge e1, ulong seed, params FormationSpec[] forms)
        => new(MultiDomainScenarios.OpenSea(50_000), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
               new[] { new SideSpec(e0), new SideSpec(e1) }, forms, seed)
        { MultiDomain = new MultiDomainSetup { Rules = new DomainRules() } };

    private static void Watch(TacticalBattle b, Func<bool> stop, Action? each = null, long maxPulses = 60)
        => RunUntil(b, () =>
        {
            var err = b.PersonnelConsistency();
            Assert.True(err is null, $"personnel ledger inconsistent at {b.Scheduler.NowMs}: {err}");
            each?.Invoke();
            return stop();
        }, maxPulses);

    /// <summary>TEST GEOMETRY: two ram galleys (no deck archers) under mutual Attack, starting as head-on columns 800 m apart
    /// with a 160 m offset (side 0 heading east, side 1 west; seed 4). They meet bow to bow obliquely: 1000 drives into 2000's
    /// side at ramming speed but both ways make the beak slide — a glancing ram (measured: t = 198 s, v_n 1 954, v_c 5 882).</summary>
    public static TacticalBattle ObliqueMeeting()
    {
        var s = Two(ExitEdge.West, ExitEdge.East, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Rammer, new Vec2L(X0 + 800_000, Y0 + 160_000)));
        return new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), Script((0, new OrderRequest(2, OrderKind.Attack, 0, 1))));
    }

    // ================================================================== RAM2-V1: the ram gate is the normal speed

    /// <summary>
    /// RAM2-V1 (pure, exact). A full-way beak strike near the end of a hull moving along its keel: the centres close at only
    /// about 1 430 mm/s (the target's own way carries its centre away along the line of centres), yet the beak drives into
    /// the planking at v_n = the rammer's whole way 4 180 mm/s. Round 1 gated on v_c and filed this as a collision (no damage);
    /// it is a ram, and its breach is decided by the impact angle, its damage by v_n — one speed for gate, breach and damage.
    /// </summary>
    [Fact]
    public void AFullWayStrikeOffTheTargetsCentreIsARamByItsNormalSpeed()
    {
        int glance = new DomainRules().RamGlanceAngle;
        var pb = HeadingMotion.Offset(FixedTrig.FromDegrees(29), Contact + 1_000);  // inside the ±30° bow arc, beak touching
        var imp = RamPhysics.Classify(new Vec2L(0, 0), 0, 4_180, pb, FixedTrig.Quarter, 4_600, Vr, glance);
        Assert.Equal(4_180, imp.NormalMmPerS);                                     // the whole way, perpendicular to the keel
        Assert.True(imp.ClosingMmPerS < Vr, $"premise: centre-line closing {imp.ClosingMmPerS} below ramming speed");
        Assert.True(imp.IsRam);
        Assert.True(imp.Breach);                                                   // atan2(4 180, 4 600) = 42° >= 20°
        // the same contact with the gate on v_c (round 1) would have been no ram: the two speeds disagree by almost 3x here
        Assert.True(imp.NormalMmPerS >= 2 * imp.ClosingMmPerS);
    }

    /// <summary>
    /// RAM2-V1 (battle). The verifier's Probe B geometry (rammer at rest, a fast hulk — TEST INPUT acceleration 2 300 mm/s² —
    /// steaming west across its front 80 m off): the rammer's beak meets the hulk's side with its centre-line closing speed
    /// below ramming speed but its normal speed far above. It is a RAM (round 1: a collision that left the hulk untouched and
    /// stopped the rammer dead): the hulk loses exactly E(RamDamage) hull and E(RamCrewDamage) people at v_n, through the
    /// ledger, and the pair is used up.
    /// </summary>
    [Fact]
    public void ACrossingTargetStruckAtFullWayIsHoledByTheNormalEnergy()
    {
        var fast = Hulk with { AccelMmPerS2 = 2_300 };
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0 + 80_000)), Ship(2, 1, fast, new Vec2L(X0 + 700_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, new Vec2L(X0 - 900_000, Y0)))));
        Watch(b, () => Executed(b, DomainKinds.Ram).Any() || Executed(b, DomainKinds.RamCollision).Any(), maxPulses: 10);
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.True(ram.C < Vr, $"premise: centre-line closing {ram.C} below ramming speed");
        Assert.True(ram.B >= 2 * Vr, $"premise: normal speed {ram.B} far above ramming speed");
        Assert.Equal((long)RamOutcome.Breach, ram.A);
        var hit = Executed(b, DomainKinds.DomainHit).Single(e => e.Target.Id == 2000 && e.Source.Id == 1000 && e.A == 1);
        Assert.Equal(Math.Max(1, RamPhysics.EnergyScaled(Rammer.RamDamage, ram.B, Rammer.MaxSpeedMmPerS)), hit.B);
        Assert.Equal(RamPhysics.EnergyScaled(Rammer.RamCrewDamage, ram.B, Rammer.MaxSpeedMmPerS), hit.C);
        Assert.Equal(fast.Hull - hit.B, D(b, 2000).Hp);
        Assert.Equal(hit.C, b.Personnel.Balance(2, PersonnelPlace.Killed));
        Assert.True(b.HasRammed(1000, 2000));
    }

    /// <summary>
    /// RAM2-V1 (planner). The attack run must bring the OWN way to s_req = ceil(v_ram / |sin a|), a = the run's angle to the
    /// target's keel. Exact against the gate on a run along heading 0: own way s_req is a ram, s_req − 1 is not, for every keel
    /// angle from the glance angle to the beam; s_req = v_ram on the beam; ∞ along the keel.
    /// </summary>
    [Fact]
    public void TheRunNeedsTheWayWhoseNormalComponentIsRammingSpeed()
    {
        int glance = new DomainRules().RamGlanceAngle;
        Assert.Equal(Vr, RamPhysics.RequiredWayMmPerS(Vr, 0, FixedTrig.Quarter));
        Assert.Equal(long.MaxValue, RamPhysics.RequiredWayMmPerS(Vr, 0, FixedTrig.HalfTurn));
        Assert.Equal(3_085, RamPhysics.RequiredWayMmPerS(Vr, 0, FixedTrig.FromDegrees(30)));   // ⌈1 542 / sin 29.997°⌉ (FromDegrees rounds to BAM)
        for (long deg = 20; deg <= 160; deg++)
        {
            int keel = FixedTrig.FromDegrees(deg);
            long req = RamPhysics.RequiredWayMmPerS(Vr, 0, keel);
            Assert.True(RamPhysics.Classify(new Vec2L(0, 0), 0, req, new Vec2L(20_000, 0), keel, 0, Vr, glance).IsRam, $"{deg}°");
            Assert.False(RamPhysics.Classify(new Vec2L(0, 0), 0, req - 1, new Vec2L(20_000, 0), keel, 0, Vr, glance).IsRam, $"{deg}° - 1");
        }
    }

    /// <summary>
    /// RAM2-V1 (planner, battle). A rammer at rest 16 m off a hull that lies 45° off its keel line has room to reach ramming
    /// speed head-on (run-up 14.3 m) but not the 2 181 mm/s of own way a 45° blow needs (21.8 m): it must not press in. Round 1
    /// aimed the run at v_ram and would arrive at ~1 760 mm/s, a normal speed of ~1 240 — a collision. It opens the range,
    /// turns in, and its first contact is a ram at ramming speed.
    /// </summary>
    [Fact]
    public void AnObliqueRunDoesNotPressInWithoutTheWayItsAngleNeeds()
    {
        var tp = new Vec2L(X0 + 200_000, Y0 + 200_000);
        var a0 = tp + HeadingMotion.Offset(FixedTrig.FromDegrees(225), Contact + 16_000);
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, a0), Ship(2, 1, Hulk, tp));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        int phi0 = RamPhysics.ApproachAngle(tp, FixedTrig.Quarter, a0);
        Assert.InRange(phi0, FixedTrig.FromDegrees(44), FixedTrig.FromDegrees(46));
        long gap0 = Vec2L.Distance(a0, tp) - Contact;
        long step = Step;
        Assert.True(gap0 >= RamPhysics.RunUpGapMm(0, Vr, Rammer.MaxSpeedMmPerS, Rammer.AccelMmPerS2, step, Rammer.RamLengthMm), "premise: room for v_ram head-on");
        long req = RamPhysics.RequiredWayMmPerS(Vr, FixedTrig.FromDegrees(45), FixedTrig.Quarter);
        Assert.True(gap0 < RamPhysics.RunUpGapMm(0, req, Rammer.MaxSpeedMmPerS, Rammer.AccelMmPerS2, step, Rammer.RamLengthMm), "premise: no room for s_req");
        Watch(b, () => Executed(b, DomainKinds.Ram).Any() || Executed(b, DomainKinds.RamCollision).Any(), maxPulses: 12);
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.True(ram.B >= Vr, $"normal {ram.B}");
    }

    /// <summary>
    /// RAM2-V1 (planner, target frame). Under the normal-speed gate a fleeing target can still be rammed from its quarter
    /// (its motion along its keel adds no normal speed), so the round-1 rule "vmax − u &lt; v_ram cannot ram" no longer holds;
    /// what remains is that a run whose range can never close (the target's speed along the line of sight u &gt;= vmax) is no
    /// run. TEST INPUT: a rammer of max 2 300 mm/s on the port quarter (45°, 60 m) of a hulk that steams away at 3 450 mm/s
    /// (acceleration 2 300). At the step after any state with u &gt;= vmax the rammer is never in Close mode — including the
    /// states in which the angle alone would allow a ram (s_req &lt;= u), where only the closing check forbids it.
    /// </summary>
    [Fact]
    public void ARammerDoesNotRunAtATargetThatOutrunsIt()
    {
        var ram = Rammer with { MaxSpeedMmPerS = 2_300 };
        var fast = Hulk with { AccelMmPerS2 = 2_300 };
        var tp = new Vec2L(X0 + 100_000, Y0 + 300_000);
        var a0 = tp + HeadingMotion.Offset(FixedTrig.FromDegrees(135), 60_000);
        var s = Two(ExitEdge.West, ExitEdge.West, 4, Ship(1, 0, ram, a0), Ship(2, 1, fast, tp));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, new Vec2L(X0 + 2_100_000, tp.Y)))));
        int outrun = 0, angleWouldAllow = 0, violations = 0; bool prev = false; long lastT = -1;
        Watch(b, () => b.Scheduler.NowMs >= s.StartMs + 120_000, () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT || (now - s.StartMs) % Step != 0) return;
            lastT = now;
            if (prev) { outrun++; if (b.RamModeOf(1000) == RamMode.Close) violations++; }
            var a = D(b, 1000); var t = D(b, 2000);
            int bearing = FixedTrig.Atan2(t.Pos.Y - a.Pos.Y, t.Pos.X - a.Pos.X);
            long u = FixedTrig.ScaleCos(t.Speed, FixedTrig.Diff(bearing, t.Heading));
            prev = ram.MaxSpeedMmPerS - u <= 0;
            if (prev && RamPhysics.RequiredWayMmPerS(Vr, bearing, t.Heading) <= u) angleWouldAllow++;
        }, 4);
        Assert.True(outrun >= 20, $"premise: the target outran the rammer for {outrun} steps");
        Assert.True(angleWouldAllow >= 3, $"premise: {angleWouldAllow} of them with an angle that would allow a ram");
        Assert.Equal(0, violations);
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
    }

    // ================================================================== RAM2-V2: only the rammer's own way rams

    /// <summary>
    /// RAM2-V2 (pure, exhaustive on a 2° grid). D-081 RM3 "the ship must build up to that speed": with its own way one below
    /// ramming speed no contact is a ram — at any heading of either hull and any target speed up to its maximum (the target's
    /// velocity lies along its keel: it adds tangential slide, never normal speed). At ramming speed on the beam it is.
    /// </summary>
    [Fact]
    public void OnlyTheRammersOwnWayCanMakeARam()
    {
        int glance = new DomainRules().RamGlanceAngle;
        int n = 0;
        for (long ha = 0; ha < 360; ha += 2)
            for (long hb = 0; hb < 360; hb += 2)
                foreach (long sb in new long[] { 0, 1_000, Hulk.MaxSpeedMmPerS })
                {
                    int a = FixedTrig.FromDegrees(ha);
                    var pb = HeadingMotion.Offset(a, Contact + 1_000);           // dead ahead of the rammer
                    var imp = RamPhysics.Classify(new Vec2L(0, 0), a, Vr - 1, pb, FixedTrig.FromDegrees(hb), sb, Vr, glance);
                    Assert.True(imp.NormalMmPerS <= Vr - 1, $"v_n {imp.NormalMmPerS} at {ha}/{hb}/{sb}");
                    Assert.False(imp.IsRam);
                    n++;
                }
        Assert.Equal(180 * 180 * 3, n);
        Assert.True(RamPhysics.Classify(new Vec2L(0, 0), 0, Vr, new Vec2L(20_000, 0), FixedTrig.Quarter, Hulk.MaxSpeedMmPerS, Vr, glance).IsRam);
    }

    /// <summary>
    /// RAM2-V2 (battle; the verifier's Probe A). A rammer lying still (TEST INPUT: it sees nothing until contact and has no
    /// weapons, so it never makes way) is run into bow-first by a fast enemy hull (TEST INPUT acceleration 2 300 mm/s²). The
    /// centres close at well over ramming speed, but the rammer built no speed: no ram is credited (round 1: a ram, the pair
    /// used up, Attacks counted). It is a collision at v_n = 0; the pair stays free; nobody is damaged.
    /// </summary>
    [Fact]
    public void AHullLyingStillIsNotCreditedWithARamTheTargetRanInto()
    {
        var blind = Rammer with { SightMm = 1 };
        var fast = Hulk with { AccelMmPerS2 = 2_300 };
        const long Yc = 1_025_000;
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, blind, new Vec2L(X0, Yc)), Ship(2, 1, fast, new Vec2L(X0 + Contact + 200_000, Yc)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, new Vec2L(X0 - 600_000, Yc)))));
        var start = new Vec2L(X0, Yc);
        Watch(b, () => Executed(b, DomainKinds.Ram).Any() || Executed(b, DomainKinds.RamCollision).Any(),
              () => { if (!Executed(b, DomainKinds.RamCollision).Any()) Assert.Equal((start, 0L), (D(b, 1000).Pos, D(b, 1000).Speed)); }, 6);
        Assert.Empty(Executed(b, DomainKinds.Ram));
        var c = Assert.Single(Executed(b, DomainKinds.RamCollision));
        Assert.Equal(1000, c.Source.Id);
        Assert.Equal(0, c.B);
        Assert.True(c.C >= Vr, $"premise: the centres closed at {c.C}");
        Assert.Empty(b.RammedBy(1000));
        Assert.Equal(fast.Hull, D(b, 2000).Hp);
        Assert.Equal(blind.Hull, D(b, 1000).Hp);                                  // no bow damage at v_n = 0
    }

    // ================================================================== RAM2-V4: a glancing ram uses up the pair

    /// <summary>
    /// RAM2-V4 (reading RAM-R1). A ram at ramming speed that glances is still the rammer's one ram on that hull: after 1000's
    /// glancing blow on 2000 (<see cref="ObliqueMeeting"/>) the pair is recorded, 1000 has nothing left to ram (its planner
    /// target is −1 from the next step on), and its next bow contact with 2000 is a collision, not a second ram. 2000 may still
    /// ram 1000 (a different pair).
    /// </summary>
    [Fact]
    public void AGlancingRamUsesUpThePair()
    {
        var b = ObliqueMeeting();
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), maxPulses: 8);
        var g = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.Equal((1000L, 2000L, (long)RamOutcome.Glance), (g.Source.Id, g.Target.Id, g.A));
        Assert.True(g.B >= Vr, $"premise: a ram at ramming speed (v_n {g.B})");
        Assert.True(b.HasRammed(1000, 2000));
        Assert.Equal(new long[] { 2000 }, b.RammedBy(1000));
        long tg = g.Key.TimeMs; long lastT = -1; int steps = 0;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(e => e.Source.Id == 2000), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT || now <= tg || (now - tg) % Step != 0) return;
            lastT = now; steps++;
            Assert.Equal(-1, b.RamTargetOf(1000));
        }, 10);
        Assert.True(steps >= 10, $"premise: watched {steps} steps after the glance");
        Assert.Contains(Executed(b, DomainKinds.RamCollision), e => e.Source.Id == 1000 && e.Target.Id == 2000 && e.Key.TimeMs > tg);
        Assert.Single(Executed(b, DomainKinds.Ram), e => e.Source.Id == 1000);
        Assert.True(b.HasRammed(2000, 1000));                                       // the other pair
    }

    // ================================================================== RAM2-V5: planner state is battle state

    /// <summary>
    /// RAM2-V5 (mutant MV1). The planner's mode and reference hull carry from step to step and decide the next intent, so
    /// the state hash must see them. Twin battles (two hulks; the rammer has rammed 2000 and is opening from it, Mode Open,
    /// Ref 2000) are hash-equal; editing Mode (to Close) or Ref (to −1) in one twin through reflection changes its hash at once
    /// and its course on the next step — so neither is inspection-only. Restoring the field restores the hash.
    /// </summary>
    [Fact]
    public void ThePlannersModeAndReferenceArePartOfTheStateHash()
    {
        TacticalBattle Make()
        {
            long x1 = X0 + Contact + 120_000;
            var tf = MultiDomainScenarios.Fleet(2, 1, Hulk, 2, new Vec2L(x1, Y0)) with
            { Entities = new[] { new EntitySpec(2000, new Vec2L(x1, Y0)), new EntitySpec(2001, new Vec2L(x1, Y0 + 400_000)) } };
            var s = Two(ExitEdge.West, ExitEdge.North, 6, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), tf);
            return new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        }
        // run to a step instant after the beak is free, the rammer clear of the struck hull's pivot reach (10 m off) and still
        // opening from it (below D_open)
        long at = -1;
        var probe = Make();
        Watch(probe, () =>
        {
            var ram = Executed(probe, DomainKinds.Ram).FirstOrDefault();
            long now = probe.Scheduler.NowMs;
            if (ram.Kind == 0 || (now - ram.Key.TimeMs) % Step != 0 || now < probe.RamLockedUntilMs(1000)) return false;
            if (Vec2L.Distance(D(probe, 1000).Pos, D(probe, 2000).Pos) - Contact < 10_000) return false;
            at = now; return true;
        }, maxPulses: 10);
        Assert.Equal(RamMode.Open, probe.RamModeOf(1000));                         // premise: opening from the struck hull
        Assert.True(Vec2L.Distance(D(probe, 1000).Pos, D(probe, 2000).Pos) - Contact < probe.RamOpenGapMm(1000), "premise: still opening");

        foreach (var (field, value) in new (string, object)[] { ("Mode", RamMode.Close), ("Ref", -1L) })
        {
            var a = Make(); var c = Make();
            Watch(a, () => a.Scheduler.NowMs >= at, maxPulses: 10);
            Watch(c, () => c.Scheduler.NowMs >= at, maxPulses: 10);
            Assert.Equal(a.StateHashHex(), c.StateHashHex());
            var st = RamStateOf(c, 1000);
            var f = st.GetType().GetField(field)!;
            object old = f.GetValue(st)!;
            Assert.Equal(field == "Mode" ? RamMode.Open : (object)2000L, old);       // premise: Mode Open, Ref = the struck hull
            f.SetValue(st, value);
            string edited = c.StateHashHex();
            Assert.NotEqual(a.StateHashHex(), edited);                             // the hash sees the field
            f.SetValue(st, old);
            Assert.Equal(a.StateHashHex(), c.StateHashHex());                      // restoring it restores the hash
            f.SetValue(st, value);
            Watch(a, () => a.Scheduler.NowMs >= at + Step, maxPulses: 2);
            Watch(c, () => c.Scheduler.NowMs >= at + Step, maxPulses: 2);
            Assert.NotEqual((D(a, 1000).Pos, D(a, 1000).Heading), (D(c, 1000).Pos, D(c, 1000).Heading));   // and it steers the hull
        }
    }

    private static object RamStateOf(TacticalBattle b, long id)
    {
        var rs = (Array)typeof(TacticalBattle).GetField("_rs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b)!;
        int i = 0; foreach (var d in b.DomainEntities()) { if (d.Id == id) break; i++; }
        return rs.GetValue(i)!;
    }

    // ================================================================== RAM2-V6: rules only the golden saw

    /// <summary>
    /// RAM2-V6 (a). A collision damages the rammer's bow by the energy of its normal speed: in the RM1 scenario (full speed
    /// into a hull already rammed) the rammer's hull drops at the collision instant by exactly
    /// ⌊RamDamage · RamBowDamagePermille · v_n² / (1000 · MaxSpeed²)⌋, through a kind-3 DomainHit on the rammer, and the struck
    /// hull takes nothing.
    /// </summary>
    [Fact]
    public void ACollisionDamagesTheRammersBowByItsNormalEnergy()
    {
        var big = Hulk with { RadiusMm = 30_000 };
        long r2 = Rammer.RadiusMm + big.RadiusMm, x1 = X0 + r2 + 120_000;
        var two = MultiDomainScenarios.Fleet(2, 1, big, 2, new Vec2L(x1, Y0)) with
        { Entities = new[] { new EntitySpec(2000, new Vec2L(x1, Y0)), new EntitySpec(2001, new Vec2L(x1 + 80_000, Y0)) } };
        var s = Two(ExitEdge.West, ExitEdge.North, 5, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), two);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long hpBefore = 0, hp2Before = 0, cur = Rammer.Hull, cur2 = big.Hull, lastT = -1;
        Watch(b, () => Executed(b, DomainKinds.RamCollision).Any(), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT) return;
            lastT = now; hpBefore = cur; hp2Before = cur2; cur = D(b, 1000).Hp; cur2 = D(b, 2000).Hp;
        }, 40);
        var coll = Executed(b, DomainKinds.RamCollision).First();
        Assert.Equal(2000, coll.Target.Id);
        Assert.True(b.HasRammed(1000, 2000), "premise: a collision on a hull already rammed");
        long bow = RamPhysics.EnergyScaledPermille(Rammer.RamDamage, Rammer.RamBowDamagePermille, coll.B, Rammer.MaxSpeedMmPerS);
        Assert.True(bow > 0, $"premise: a bow share at v_n {coll.B}");
        var hit = Executed(b, DomainKinds.DomainHit).Single(e => e.Key.TimeMs == coll.Key.TimeMs && e.Target.Id == 1000);
        Assert.Equal((TacticalBattle.BowHitKind, bow, 0L), (hit.A, hit.B, hit.C));
        Assert.Equal(hpBefore - bow, D(b, 1000).Hp);
        Assert.Equal(hp2Before, D(b, 2000).Hp);                                     // the struck hull takes nothing
    }

    /// <summary>
    /// RAM2-V6 (b). Beak-contact geometry: the beak strikes a hull of the target formation whose surface lies within the
    /// beak's reach (centre distance &lt;= r_a + r_b + L = 18 000 mm) inside ±30° of the bow; two hulls at exactly the same
    /// distance go to the LOWER id, whatever side they lie on (key (distance², id)); a hull 40° off the bow, or 1 mm beyond the
    /// reach dead ahead, is not struck. The fouling scan keys the same way.
    /// </summary>
    [Fact]
    public void TheBeakStrikesInsideItsBowArcAndReachNearestThenLowestId()
    {
        long Struck(Vec2L[] hulls, long[] ids)
        {
            var tf = MultiDomainScenarios.Fleet(2, 1, Hulk, hulls.Length, hulls[0]) with
            { Entities = hulls.Select((p, k) => new EntitySpec(ids[k], p)).ToArray() };
            var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), tf);
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            Assert.Equal(0, D(b, 1000).Heading);
            return b.BeakContactOf(1000, 2);
        }
        var up = new Vec2L(X0 + 15_000, Y0 + 8_500); var down = new Vec2L(X0 + 15_000, Y0 - 8_500);   // 29.5° each side, 17 241 mm
        Assert.Equal(Vec2L.DistanceSq(new Vec2L(X0, Y0), up), Vec2L.DistanceSq(new Vec2L(X0, Y0), down));
        Assert.Equal(2000, Struck(new[] { up, down }, new long[] { 2000, 2001 }));
        Assert.Equal(2000, Struck(new[] { up, down }, new long[] { 2001, 2000 }));
        Assert.Equal(2001, Struck(new[] { new Vec2L(X0 + 15_000, Y0 + 8_501), down }, new long[] { 2000, 2001 }));   // 1 mm farther loses
        Assert.Equal(-1, Struck(new[] { new Vec2L(X0 + 13_000, Y0 + 11_000) }, new long[] { 2000 }));           // 40.2° off the bow, 17 029 mm
        Assert.Equal(2000, Struck(new[] { new Vec2L(X0 + 18_000, Y0) }, new long[] { 2000 }));                  // exactly at reach
        Assert.Equal(-1, Struck(new[] { new Vec2L(X0 + 18_001, Y0) }, new long[] { 2000 }));                    // 1 mm beyond

        // fouling: two friendly hulks 45° off either bow at the same distance (inside the beak's reach) foul the run at a far
        // target — the lower id, whatever side
        long Fouled(long idUp, long idDown)
        {
            var f1 = Ship(idUp, 0, Hulk, new Vec2L(X0 + 12_000, Y0 + 12_000));
            var f2 = Ship(idDown, 0, Hulk, new Vec2L(X0 + 12_000, Y0 - 12_000));
            var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), f1, f2, Ship(2, 1, Hulk, new Vec2L(X0 + 400_000, Y0)));
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            return b.FouledByOf(1000, 2000);
        }
        Assert.Equal(4000, Fouled(4, 5));
        Assert.Equal(4000, Fouled(5, 4));
    }

    // ================================================================== RAM2-V7: an order under the beak lock

    /// <summary>
    /// RAM2-V7. The beak lock holds the HULL, not the order: a MoveTo issued while the beak is embedded is delivered at its
    /// own instant (the formation's directive is MoveTo from that step on, Attack on the step before), and the hull starts to
    /// execute it on the first step instant at or after the end of the lock — turn-exact: still at its impact position and
    /// heading one step before, turned and under way at that step, and further along one step after.
    /// </summary>
    [Fact]
    public void AnOrderIssuedUnderTheBeakLockTakesEffectAtTheFirstStepAfterIt()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2)), (0, new OrderRequest(1, OrderKind.MoveTo, 24, -1, new Vec2L(X0 - 300_000, Y0 - 300_000)))),
                                   PassiveController.Instance);
        var seen = new SortedDictionary<long, (Vec2L P, int H, long V, long Way, OrderKind Dir)>();
        Watch(b, () => b.Scheduler.NowMs >= s.StartMs + 70_000,
              () => seen[b.Scheduler.NowMs] = (D(b, 1000).Pos, D(b, 1000).Heading, D(b, 1000).Speed, b.RamWayMmPerS(1000), b.DomainDirective(1)), 3);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        long tRam = ram.Key.TimeMs, until = b.RamLockedUntilMs(1000), orderAt = s.StartMs + 24 * Step;
        Assert.True(tRam < orderAt && orderAt + Step < until, $"premise: the MoveTo lands inside the lock ({tRam} < {orderAt} < {until})");
        // delivered on time (not held): Attack one step before its instant, MoveTo from it
        Assert.Equal(OrderKind.Attack, seen[orderAt - Step].Dir);
        Assert.Equal(OrderKind.MoveTo, seen[orderAt].Dir);
        long release = tRam + (until - tRam + Step - 1) / Step * Step;              // first step instant >= until
        Assert.True(release - Step < until && release >= until);
        var atImpact = seen[tRam];
        for (long t = tRam + Step; t < release; t += Step)
            Assert.Equal((atImpact.P, atImpact.H, 0L, 0L), (seen[t].P, seen[t].H, seen[t].V, seen[t].Way));   // held, -1 step included
        // release step: the MoveTo takes effect — the hull turns toward the MoveTo point by its full turn (20° per 2 s step)
        // and pulls (a commanded way of one step's acceleration, 220 mm/s); the hull it still touches blocks the move itself,
        // so it pivots in place (measured: it makes way from the fourth step after release, once its bow is clear).
        int toDest = FixedTrig.Atan2(Y0 - 300_000 - atImpact.P.Y, X0 - 300_000 - atImpact.P.X);
        long maxTurn = HeadingMotion.MaxTurn(Rammer.TurnRatePerS, Step);
        Assert.Equal(maxTurn, FixedTrig.Separation(atImpact.H, seen[release].H));
        Assert.Equal(FixedTrig.Separation(atImpact.H, toDest) - maxTurn, FixedTrig.Separation(seen[release].H, toDest));
        Assert.Equal(Rammer.AccelMmPerS2 * Step / 1000, seen[release].Way);
        // +1 step: still executing it (turned further toward the point)
        Assert.Equal(FixedTrig.Separation(atImpact.H, toDest) - 2 * maxTurn, FixedTrig.Separation(seen[release + Step].H, toDest));
    }
}
