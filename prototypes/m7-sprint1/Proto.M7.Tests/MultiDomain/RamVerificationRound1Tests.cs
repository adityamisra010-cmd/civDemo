using System.Reflection;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>
/// RAM LANE — verification round 1 (2026-11-05): one semantic test per reproduced finding (RAM-V1 … RAM-V11, see
/// reports/H-RAM.md "Verification round 1"). Every battle test checks the personnel ledger at every completed instant.
/// </summary>
public class RamVerificationRound1Tests
{
    // TEST INPUTS (as in RamTests): a galley without deck archers and an unarmed, ram-less hulk that stays afloat.
    private static readonly VesselClass Rammer = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>() };
    private static readonly VesselClass Hulk = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0, Hull = 1000 };
    private static readonly VesselClass G = DomainProfiles.Galley;
    private const long Y0 = 1_000_000, X0 = 1_000_000, Contact = 16_000;   // two galley radii
    private static readonly long Step = new TacticalRules().StepMs;
    /// <summary>A pivot through a half turn at the galley's 10°/s takes ceil(180 / 20) = 9 steps of 2 s.</summary>
    private const int HalfTurnPivotSteps = 9;

    /// <summary>Open sea; side headings from the exit edges (West: east 0°, North: north 90°).</summary>
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

    /// <summary>Runs until stop(); returns the longest run of step instants at which the hull was active, not backing its beak
    /// out, had a ram target (the planner was steering it) and had not moved since the previous step instant.</summary>
    private static int LongestStill(TacticalBattle b, long id, Func<bool> stop, long maxPulses)
    {
        int run = 0, max = 0; Vec2L? last = null; long lastT = -1;
        Watch(b, stop, () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT || now % Step != 0) return;
            lastT = now;
            var d = D(b, id);
            bool steered = d.State == EntityState.Active && now >= b.RamLockedUntilMs(id) && b.RamTargetOf(id) >= 0;
            run = steered && last == d.Pos ? run + 1 : 0;
            max = Math.Max(max, run);
            last = d.Pos;
        }, maxPulses);
        return max;
    }

    // ================================================================== RAM-V1: no deadlock while opening the range

    /// <summary>
    /// RAM-V1 (c). A rammer that starts touching its target's STERN (on its keel line, where a blow would glance) must open
    /// the range for the beam. The lane's planner steered for the beam station while the target, touching, stayed forward of
    /// its beam — so it pivoted at speed 0 for ever (measured on c2b5f4a: motionless for 178 consecutive steps, no ram in 6
    /// minutes). The clearing rule turns it directly away first: it is never still longer than one half-turn pivot, and rams.
    /// </summary>
    [Fact]
    public void ARammerTouchingItsTargetsSternGetsClearAndStillRams()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0, Y0 + Contact + 500)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Assert.True(RamPhysics.ApproachAngle(new Vec2L(X0, Y0 + Contact + 500), FixedTrig.Quarter, new Vec2L(X0, Y0)) < new DomainRules().RamGlanceAngle,
                    "premise: the rammer starts on the target's keel line");
        int still = LongestStill(b, 1000, () => Executed(b, DomainKinds.Ram).Any(), 12);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.Equal(2000, ram.Target.Id);
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.True(still <= HalfTurnPivotSteps, $"motionless for {still} steps");
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
    }

    /// <summary>
    /// RAM-V1 (a). A rammer boxed between a friendly hull touching its bow and another touching its port quarter: the lane
    /// steered directly away from the bow hull, into the quarter hull, and pressed there for ever (measured on c2b5f4a: 172
    /// consecutive still steps, no ram). The clearing rule escapes on the bisector of the widest free sector — between them —
    /// and the rammer still gets its ram in.
    /// </summary>
    [Fact]
    public void ARammerBoxedBowAndQuarterEscapesBetweenThemAndStillRams()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 5,
            Ship(1, 0, Rammer, new Vec2L(X0, Y0)),
            Ship(4, 0, Hulk, new Vec2L(X0 + Contact + 100, Y0)),                     // touching the bow (gap 100 mm)
            Ship(5, 0, Hulk, new Vec2L(X0 - 15_130, Y0 - 5_507)),                    // touching the port quarter (bearing 200°)
            Ship(2, 1, Hulk, new Vec2L(X0 + 150_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        int still = LongestStill(b, 1000, () => Executed(b, DomainKinds.Ram).Any(), 12);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.Equal(2000, ram.Target.Id);
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.True(still <= HalfTurnPivotSteps, $"motionless for {still} steps");
    }

    /// <summary>
    /// RAM-V1 (clearing rule, pivot). A rammer 3 m off a hull dead ahead (inside the pivot reach, too close for a run) turns
    /// away from it IN PLACE — one bank rows ahead, the other backs — while the hull lies forward of its beam, because making
    /// way there would close on it: the gap never drops below the starting 3 m. It then opens the range and rams.
    /// </summary>
    [Fact]
    public void ARammerClearingAHullAheadPivotsAndNeverClosesOnIt()
    {
        const long start = 3_000;
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + start, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        long minGap = long.MaxValue;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(),
              () => { if (!Executed(b, DomainKinds.Ram).Any()) minGap = Math.Min(minGap, Vec2L.Distance(D(b, 1000).Pos, D(b, 2000).Pos) - Contact); }, 30);
        Assert.Equal(start, minGap);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.Empty(Executed(b, DomainKinds.RamCollision));
    }

    /// <summary>
    /// RAM-V1 (d). An impact needs the hulls to come together. After a ram the rammer (nothing left to ram, no weapons) lies
    /// still; the struck hull, ordered west THROUGH it, presses on its bow every step but cannot move. The lane classified the
    /// struck hull's commanded way (220 mm/s, pre-truncation) as a closing speed and logged a collision every step (measured
    /// on c2b5f4a: 33, one per step from the release to 120 s). Neither hull moves, so there is no impact at all.
    /// </summary>
    [Fact]
    public void AHullPressingOnAnIdleRammersBowMakesNoImpact()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   Script((0, new OrderRequest(2, OrderKind.MoveTo, 24, -1, new Vec2L(X0 - 200_000, Y0)))));
        Vec2L? p1 = null, p2 = null; bool moved = false; long until = long.MaxValue;
        Watch(b, () => b.Scheduler.NowMs >= s.StartMs + 120_000, () =>
        {
            if (!Executed(b, DomainKinds.Ram).Any()) return;
            until = b.RamLockedUntilMs(1000);
            if (b.Scheduler.NowMs < until) return;
            p1 ??= D(b, 1000).Pos; p2 ??= D(b, 2000).Pos;
            if (D(b, 1000).Pos != p1 || D(b, 2000).Pos != p2) moved = true;
        }, 4);
        Assert.False(moved, "premise: both hulls lie pressed together, neither moving");
        Assert.Equal(-1, b.RamTargetOf(1000));                                     // premise: nothing left to ram
        Assert.True(FixedTrig.Separation(D(b, 2000).Heading, FixedTrig.HalfTurn) < FixedTrig.FromDegrees(30), "premise: the struck hull heads into the rammer");
        Assert.DoesNotContain(Executed(b, DomainKinds.RamCollision), e => e.Key.TimeMs >= until);
        Assert.DoesNotContain(Executed(b, DomainKinds.Ram), e => e.Key.TimeMs >= until);
    }

    /// <summary>
    /// RAM-V1, in AI-vs-AI galley battles (seeds 1–20, both sides Attack): no hull the ram planner is steering stays
    /// motionless for longer than two half-turn pivots. Measured on c2b5f4a: 255 consecutive still steps (seed 2, a second
    /// deadlock besides the verifier's), and 15–19 in eight other seeds; after the fix the longest is 13.
    /// </summary>
    [Fact]
    public void NoGalleyIsDeadlockedInAiBattles()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var b = new TacticalBattle(MultiDomainScenarios.LineBattle(G, 50_000, seed), new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Attack, 0, 2)),
                                       new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Attack, 0, 1)));
            var run = new SortedDictionary<long, int>(); var last = new SortedDictionary<long, Vec2L>();
            int max = 0; long lastT = -1;
            RunChecked(b, onInstant: () =>
            {
                long now = b.Scheduler.NowMs;
                if (now == lastT || now % Step != 0) return;
                lastT = now;
                foreach (var d in b.DomainEntities())
                {
                    bool steered = d.State == EntityState.Active && now >= b.RamLockedUntilMs(d.Id) && b.RamTargetOf(d.Id) >= 0;
                    int k = steered && last.TryGetValue(d.Id, out var lp) && lp == d.Pos ? run.GetValueOrDefault(d.Id) + 1 : 0;
                    run[d.Id] = k; last[d.Id] = d.Pos;
                    max = Math.Max(max, k);
                }
            });
            Assert.True(max <= 2 * HalfTurnPivotSteps, $"seed {seed}: a galley lay motionless for {max} steps");
        }
    }

    // ================================================================== RAM-V3: the target's own motion

    /// <summary>
    /// RAM-V3. The attack run's run-up check is made in the target's frame: a target running away along the line of sight
    /// at u leaves at most vmax − u of closing speed. [r2 RAM2-V1] The run needs the OWN way whose normal component on the
    /// target's side is ramming speed, s_req = v_ram / |sin a| (a = the run's angle to the target's keel): when vmax − u &lt;= 0
    /// (the range never closes) or s_req &gt; vmax the planner does not run in (it opens for the beam) — at the step after any
    /// such state the rammer is never in Close mode (round 1's predicate was vmax − u &lt; v_ram, the centre-line gate). The
    /// lane assumed a stationary target (measured on c2b5f4a: Close for 20 steps of 91 in which it could not reach ramming
    /// speed). Control: the same geometry with the target lying still is rammed at ramming speed.
    /// </summary>
    [Theory]
    [InlineData(2_900L)]                                                           // [r2] was 3 200 (see below)
    [InlineData(4_600L)]
    public void TheAttackRunAccountsForTheTargetsOwnMotion(long rammerMax)
    {
        // TEST INPUT 2 900: a rammer slower than the fleeing hulk's 3 450 mm/s, so the range opens for many steps ([r2] round 1
        // used 3 200, which the hulk outran only in the centre-line sense of round 1's gate: under the normal-speed gate it can
        // still close, slowly, and ram — 0 steps in which the range could not close, 2 in which the angle forbade a ram)
        var ram = Rammer with { MaxSpeedMmPerS = rammerMax };
        long tx = X0 + 200_000, ty = Y0 + 300_000;
        var a0 = new Vec2L(tx, ty) + HeadingMotion.Offset(FixedTrig.Norm(FixedTrig.HalfTurn + FixedTrig.FromDegrees(60)), 120_000);
        TacticalBattle Make(bool runs)
        {
            var s = Two(ExitEdge.West, ExitEdge.West, 4, Ship(1, 0, ram, a0), Ship(2, 1, Hulk, new Vec2L(tx, ty)));
            IFormationController c1 = runs ? Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, new Vec2L(X0 + 2_100_000, ty)))) : PassiveController.Instance;
            return new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), c1);
        }
        var b = Make(true);
        int infeasible = 0, violations = 0; bool prevInfeasible = false; long lastT = -1;
        Watch(b, () => b.Scheduler.NowMs >= 200_000 || Executed(b, DomainKinds.Ram).Any() || Executed(b, DomainKinds.RamCollision).Any(), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT || now % Step != 0) return;
            lastT = now;
            if (prevInfeasible) { infeasible++; if (b.RamModeOf(1000) == RamMode.Close) violations++; }
            var a = D(b, 1000); var t = D(b, 2000);
            int bearing = FixedTrig.Atan2(t.Pos.Y - a.Pos.Y, t.Pos.X - a.Pos.X);
            long u = FixedTrig.ScaleCos(t.Speed, FixedTrig.Diff(bearing, t.Heading));   // the target's speed along the line of sight
            prevInfeasible = rammerMax - u <= 0 || RamPhysics.RequiredWayMmPerS(ram.RamSpeedMmPerS, bearing, t.Heading) > rammerMax;
        }, 10);
        Assert.True(infeasible >= 5, $"premise: no run could ram for {infeasible} steps");
        Assert.Equal(0, violations);
        Assert.DoesNotContain(Executed(b, DomainKinds.RamCollision), e => e.B < ram.RamSpeedMmPerS);
        var still = Make(false);
        Watch(still, () => Executed(still, DomainKinds.Ram).Any(), maxPulses: 10);
        Assert.True(Executed(still, DomainKinds.Ram).Single().B >= ram.RamSpeedMmPerS);
    }

    // ================================================================== RAM-V4: rules that had no test

    /// <summary>RAM-V4 (1). While its beak is embedded the rammer is held WHATEVER its order: a MoveTo for it that lands
    /// inside the lock does not move it before the lock ends; after it, the hull obeys the order.</summary>
    [Fact]
    public void TheBeakLockHoldsTheRammerWhateverItsOrder()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2)), (0, new OrderRequest(1, OrderKind.MoveTo, 24, -1, new Vec2L(X0 - 300_000, Y0 - 300_000)))),
                                   PassiveController.Instance);
        Vec2L? atRam = null; long until = 0, tRam = -1; bool movedWhileLocked = false, movedAfter = false;
        Watch(b, () => b.Scheduler.NowMs >= s.StartMs + 80_000, () =>
        {
            var ram = Executed(b, DomainKinds.Ram).FirstOrDefault();
            if (ram.Kind == 0) return;
            if (atRam is null) { atRam = D(b, 1000).Pos; until = b.RamLockedUntilMs(1000); tRam = ram.Key.TimeMs; }
            if (b.Scheduler.NowMs < until && D(b, 1000).Pos != atRam.Value) movedWhileLocked = true;
            if (b.Scheduler.NowMs >= until && D(b, 1000).Pos != atRam.Value) movedAfter = true;
        }, 3);
        long orderAt = s.StartMs + 24 * Step;
        Assert.True(tRam < orderAt && orderAt < until, $"premise: the MoveTo lands inside the lock ({tRam} < {orderAt} < {until})");
        Assert.False(movedWhileLocked);
        Assert.True(movedAfter, "the freed hull obeys its MoveTo");
    }

    /// <summary>RAM-V4 (2). A bow contact that is not a ram (here the RM1 case: full speed into a hull already rammed) arrests
    /// the rammer's way: [r2] its speed at the collision instant is way − v_n — the normal way is absorbed by the struck hull,
    /// the tangential kept (round 1 zeroed the whole way; equal only for a perpendicular contact) — although it struck at
    /// ramming speed and the separation step alone would leave it its way.</summary>
    [Fact]
    public void ACollisionArrestsTheRammersWay()
    {
        var big = Hulk with { RadiusMm = 30_000 };
        long r2 = Rammer.RadiusMm + big.RadiusMm, x1 = X0 + r2 + 120_000;
        var two = MultiDomainScenarios.Fleet(2, 1, big, 2, new Vec2L(x1, Y0)) with
        { Entities = new[] { new EntitySpec(2000, new Vec2L(x1, Y0)), new EntitySpec(2001, new Vec2L(x1 + 80_000, Y0)) } };
        var s = Two(ExitEdge.West, ExitEdge.North, 5, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), two);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Vec2L before = default, prev = default; long lastT = -1;
        Watch(b, () => Executed(b, DomainKinds.RamCollision).Any(), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT) return;
            lastT = now; before = prev; prev = D(b, 1000).Pos;
        }, 40);
        var coll = Executed(b, DomainKinds.RamCollision).First();
        Assert.Equal(2000, coll.Target.Id);
        Assert.True(b.HasRammed(1000, 2000), "premise: a collision on a hull already rammed");
        Assert.True(coll.B >= Rammer.RamSpeedMmPerS, $"premise: it struck at ramming speed ({coll.B})");
        Assert.True(Vec2L.Distance(before, D(b, 1000).Pos) > 0, "premise: it made way in the step of the contact");
        long way = b.RamWayMmPerS(1000);
        Assert.True(way >= coll.B, $"premise: way {way}");
        Assert.Equal(way - coll.B, D(b, 1000).Speed);
        Assert.True(D(b, 1000).Speed < way / 4, $"the normal way is lost: {D(b, 1000).Speed} of {way}");
    }

    /// <summary>RAM-V4 (3). A glancing blow keeps only the tangential way: speed after = way − v_n. [r2] TEST GEOMETRY re-rigged
    /// (RAM2-V1: round 1's run at 25° off a still hull's keel drove in below ramming speed, which is now a collision, not a
    /// ram): <see cref="RamVerificationRound2Tests.ObliqueMeeting"/> — two ram galleys meeting bow to bow obliquely; 1000
    /// drives into 2000's side at ramming speed, but the two ways make the beak slide (impact angle below the glance angle).</summary>
    [Fact]
    public void AGlancingBlowKeepsOnlyItsTangentialWay()
    {
        var b = RamVerificationRound2Tests.ObliqueMeeting();
        long way = -1;
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), () => way = b.RamWayMmPerS(1000), 8);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.Equal(1000, ram.Source.Id);
        Assert.Equal((long)RamOutcome.Glance, ram.A);
        Assert.True(ram.B >= Rammer.RamSpeedMmPerS, $"premise: a ram at ramming speed (v_n {ram.B})");
        Assert.True(way > ram.B, $"premise: way {way} > v_n {ram.B}");
        Assert.Equal(way - ram.B, D(b, 1000).Speed);
        Assert.Equal(Rammer.Hull, D(b, 2000).Hp);                                   // a glance does not hole
    }

    // ================================================================== RAM-V5: mechanisms only the golden saw

    /// <summary>RAM-V5. The beak's depth grows with the blow's energy: a ram at the minimum run-up (1 760 mm/s perpendicular)
    /// embeds floor(2 000 · 1 760² / 4 600²) = 292 mm and frees in ceil(sqrt(2 · 292 / 55) s) = 3 259 ms — not the 8 529 ms
    /// of a full-speed blow.</summary>
    [Fact]
    public void ALighterBlowEmbedsTheBeakLessAndFreesItSooner()
    {
        long gStar = RamPhysics.RunUpGapMm(0, Rammer.RamSpeedMmPerS, Rammer.MaxSpeedMmPerS, Rammer.AccelMmPerS2, Step, Rammer.RamLengthMm);
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + gStar, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), maxPulses: 10);
        var ram = Executed(b, DomainKinds.Ram).Single();
        Assert.Equal(1_760, ram.B);
        long depth = RamPhysics.EnergyScaled(Rammer.RamLengthMm, ram.B, Rammer.MaxSpeedMmPerS);
        Assert.Equal(292, depth);
        long aBack = Rammer.AccelMmPerS2 * new DomainRules().BackingAccelPermille / 1000;
        Assert.Equal(3_259, RamPhysics.FreeRamMs(depth, aBack));
        Assert.Equal(ram.Key.TimeMs + 3_259, b.RamLockedUntilMs(1000));
        Assert.True(RamPhysics.FreeRamMs(Rammer.RamLengthMm, aBack) > 3_259);
    }

    /// <summary>RAM-V5. The approach-angle hysteresis: a rammer that starts 10° off the target's keel line (below the glance
    /// angle) at more than D_open opens for the beam and starts its run only once at least the attack angle (45°) off the keel
    /// line — at every Open → Close switch the angle the planner saw is &gt;= 45°, not merely &gt;= the 20° glance angle.</summary>
    [Fact]
    public void AnAttackRunStartsOnlyFromTheAttackAngleOffTheKeelLine()
    {
        long tx = X0 + 100_000, ty = Y0 + 150_000;
        var a0 = new Vec2L(tx, ty) + HeadingMotion.Offset(FixedTrig.FromDegrees(280), Contact + 100_000);
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, a0), Ship(2, 1, Hulk, new Vec2L(tx, ty)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        var rules = new DomainRules();
        Assert.True(Contact + 100_000 - Contact >= b.RamOpenGapMm(1000), "premise: the rammer starts beyond D_open");
        var switches = new List<int>();
        RamMode prevMode = RamMode.Close; int prevPhi = RamPhysics.ApproachAngle(new Vec2L(tx, ty), FixedTrig.Quarter, a0); long lastT = -1;
        Assert.True(prevPhi < rules.RamGlanceAngle, "premise: it starts below the glance angle");
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), () =>
        {
            long now = b.Scheduler.NowMs;
            if (now == lastT) return;
            lastT = now;
            var m = b.RamModeOf(1000);
            if (prevMode == RamMode.Open && m == RamMode.Close) switches.Add(prevPhi);
            prevMode = m;
            prevPhi = RamPhysics.ApproachAngle(D(b, 2000).Pos, D(b, 2000).Heading, D(b, 1000).Pos);   // what the next step's planner sees
        }, 12);
        Assert.NotEmpty(switches);
        Assert.All(switches, phi => Assert.True(phi >= rules.RamAttackAngle, $"run started {phi * 360.0 / FixedTrig.Turn:F1}° off the keel line"));
        Assert.Equal((long)RamOutcome.Breach, Executed(b, DomainKinds.Ram).Single().A);
    }

    /// <summary>RAM-V5. The ram target is the nearest TRACKED hull, not the nearest hull: 2000 lies 80 m off but is seen by no
    /// hull of the side (TEST INPUT sight 20 m: detection 60 m against the hulk's 300 % signature); 2001 lies 200 m off, seen
    /// by a friendly scout.</summary>
    [Fact]
    public void TheRamTargetIsTheNearestTrackedHull()
    {
        var shortSight = Rammer with { SightMm = 20_000 };
        var scout = Hulk with { SightMm = 20_000 };
        var tf = MultiDomainScenarios.Fleet(2, 1, Hulk, 2, new Vec2L(X0 + 80_000, Y0)) with
        { Entities = new[] { new EntitySpec(2000, new Vec2L(X0 + 80_000, Y0)), new EntitySpec(2001, new Vec2L(X0, Y0 + 200_000)) } };
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, shortSight, new Vec2L(X0, Y0)), Ship(4, 0, scout, new Vec2L(X0, Y0 + 250_000)), tf);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Watch(b, () => b.RamTargetOf(1000) >= 0, maxPulses: 2);
        Assert.Equal(2001, b.RamTargetOf(1000));
    }

    // ================================================================== RAM-V7: Board keeps lane N's ram, under D-081

    /// <summary>
    /// RAM-V7. The lane's "Board no longer rams" was an undecided behaviour change (proposal P2) and is withdrawn: a boarding
    /// approach rams as lane N allowed, under the D-081 rules — a ram at closing speed &gt;= ramming speed (here the galley's
    /// approach, which the cited 0.11 m/s² cannot brake to boarding speed in the last 37 m), once per pair; then it grapples.
    /// Its later, slow contacts are the grapple itself: no collision events.
    /// </summary>
    [Fact]
    public void ABoardingApproachAtRammingSpeedRamsOnceThenGrapples()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, G, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        Watch(b, () => Executed(b, DomainKinds.Boarding).Count() >= 3, maxPulses: 10);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        Assert.Equal(2000, ram.Target.Id);
        Assert.True(ram.B >= G.RamSpeedMmPerS, $"normal {ram.B}");
        Assert.True(b.HasRammed(1000, 2000));
        Assert.True(Executed(b, DomainKinds.Boarding).First().Key.TimeMs >= ram.Key.TimeMs);
        Assert.Empty(Executed(b, DomainKinds.RamCollision));

        // A boarder that never exceeds its 1 m/s boarding cap (TEST INPUT; it starts inside the 37 m contact zone) comes
        // alongside below ramming speed: its beak touches the hull with the range closing, yet that is the grapple — no ram,
        // no collision.
        var slow = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, G, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 14_000, Y0))) with
        { MultiDomain = new MultiDomainSetup { Rules = new DomainRules { BoardingSpeedMmPerS = 1_000 } } };
        var c = new TacticalBattle(slow, Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        long minGap = long.MaxValue, maxSpeed = 0;
        Watch(c, () => c.Scheduler.NowMs >= slow.StartMs + 60_000, () =>
        {
            minGap = Math.Min(minGap, Vec2L.Distance(D(c, 1000).Pos, D(c, 2000).Pos) - Contact);
            maxSpeed = Math.Max(maxSpeed, D(c, 1000).Speed);
        }, 4);
        Assert.True(minGap <= G.RamLengthMm, $"premise: the beak reached the hull (closest gap {minGap})");
        Assert.True(maxSpeed < G.RamSpeedMmPerS, $"premise: below ramming speed ({maxSpeed})");
        Assert.NotEmpty(Executed(c, DomainKinds.Boarding));
        Assert.Empty(Executed(c, DomainKinds.Ram));
        Assert.Empty(Executed(c, DomainKinds.RamCollision));
    }

    // ================================================================== RAM-V10, RAM-V11

    /// <summary>RAM-V10. A rammer's own bow damage is self-inflicted: it is the rammer formation's DamageTaken, and nobody's
    /// DamageDealt (the lane credited it to the struck — unarmed — hull's formation). TEST INPUT: a hulk of hull 50 that one
    /// full-speed ram sinks, so the battle ends.</summary>
    [Fact]
    public void ARammersOwnBowDamageCreditsNoOne()
    {
        var sink = Hulk with { Hull = 50 };
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, sink, new Vec2L(X0 + Contact + 150_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        var r = RunChecked(b);
        var ram = Assert.Single(Executed(b, DomainKinds.Ram));
        long bow = RamPhysics.EnergyScaledPermille(Rammer.RamDamage, Rammer.RamBowDamagePermille, ram.B, Rammer.MaxSpeedMmPerS);
        Assert.Equal(9, bow);
        var f1 = r.Formations.Single(f => f.FormationId == 1);
        var f2 = r.Formations.Single(f => f.FormationId == 2);
        Assert.Equal(bow, f1.DamageTaken);
        Assert.Equal(0, f2.DamageDealt);
        Assert.Equal(sink.Hull + Rammer.RamCrewDamage, f1.DamageDealt);              // hull 50 (capped) + 6 people
        Assert.Equal(0, f2.AttacksMade);
    }

    /// <summary>
    /// RAM-V11 (mutant M20). The rammed list is battle state (it decides RM1), so the state hash must see it. No battle was
    /// found that differs ONLY in it (every way of using up a pair also changes an event or a speed), so this test edits it
    /// through reflection on a live battle: removing the pair changes the hash, restoring it restores the hash.
    /// </summary>
    [Fact]
    public void TheRammedListIsPartOfTheStateHash()
    {
        var s = Two(ExitEdge.West, ExitEdge.North, 4, Ship(1, 0, Rammer, new Vec2L(X0, Y0)), Ship(2, 1, Hulk, new Vec2L(X0 + Contact + 120_000, Y0)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        Watch(b, () => Executed(b, DomainKinds.Ram).Any(), maxPulses: 10);
        Assert.Equal(new long[] { 2000 }, b.RammedBy(1000));
        var rsField = typeof(TacticalBattle).GetField("_rs", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var states = (Array)rsField.GetValue(b)!;
        int index = -1;
        for (int i = 0; i < states.Length; i++)
        {
            var arr = (long[])states.GetValue(i)!.GetType().GetField("Rammed")!.GetValue(states.GetValue(i))!;
            if (arr.Length == 1 && arr[0] == 2000) index = i;
        }
        Assert.True(index >= 0);
        var st = states.GetValue(index)!;
        var rammed = st.GetType().GetField("Rammed")!;
        string with = b.StateHashHex();
        rammed.SetValue(st, Array.Empty<long>());
        string without = b.StateHashHex();
        rammed.SetValue(st, new long[] { 2000 });
        Assert.NotEqual(with, without);
        Assert.Equal(with, b.StateHashHex());
    }
}
