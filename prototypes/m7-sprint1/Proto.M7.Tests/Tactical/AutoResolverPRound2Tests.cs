using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [P fix r2] Lane P verification round 2 (findings P2-V1 .. P2-V6). (P2-V1) the first-detection progress credit goes to the
/// observer that made the detection, not to the lowest formation id; (P2-V2) a new approach minimum to a TRACKED enemy is
/// progress, whoever moved (tactical UpdateMinima); (P2-V3) the model-2 axis keeps true depth behind both mean anchors;
/// (P2-V4) DIR5 exemption (a) follows the setup's ActiveConcealmentRule; (P2-V5) the convergence-mobility frontage cap is wired
/// to the climb from the attacker toward the holder; (P2-V6) turn-exact pins of the flight end, the objective clause boundary of
/// exemption (b), the one-step reveal window and tracking kept by contact. Every setup and profile variant is a TEST INPUT.
/// </summary>
public class AutoResolverPRound2Tests
{
    private static readonly AutoCoefficients V2 = new();
    private const long S = CellMm;

    private static T Bounded<T>(Func<T> f)
    {
        var t = Task.Run(f);
        if (!t.Wait(TimeSpan.FromSeconds(60))) Assert.Fail("HARNESS GUARD: AutoResolver did not terminate within 60 s (NON-TERMINATION is a FAILURE)");
        return t.Result;
    }

    private static (AutoBattle B, BattleResult R) Auto(BattleSetup s, bool log = false)
    {
        var b = new AutoBattle(s, V2) { RecordLog = log };
        var r = Bounded(b.Run);
        TT.AssertConserved(r);
        return (b, r);
    }

    private static (TacticalBattle B, BattleResult R) Tactical(BattleSetup s)
    {
        var b = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
        var r = TT.RunGuarded(b);
        TT.AssertConserved(r);
        return (b, r);
    }

    private static BattleSetup Setup(SideSpec[] sides, int n, params FormationSpec[] forms)
        => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, forms, 7)
        { Rules = new TacticalRules { InactivityPulses = n } };

    // ------------------------------------------------------------------ P2-V1: whose progress a first detection is

    /// <summary>An idle hidden formation (sight 1) at Cell(2, 16); a Defend observer (sight 20 cells) further forward; an enemy
    /// that marches toward its objective and stops about 10 m after the observer first sees it, in the middle of pulse N - 1. The
    /// only pulse-(N-1) event near the idle formation is that first detection, and it is the OBSERVER's progress (tactical
    /// Detect: the Primary of the observer entity). <paramref name="farObjective"/>: the enemy marches on instead (P2-V2).</summary>
    private static BattleSetup FirstDetection(int n, long idleId, long obsId, long stopShiftMm = 0, bool farObjective = false,
                                              long? observerMorale = null)
    {
        long travel = 78_000L * (n - 1) + 49_000L;                          // 1.3 m/s: the enemy stops ~mid pulse n-1
        long objX = 810_000 - travel, obsX = objX + 10_000 - 400_000;
        var obj = farObjective ? Cell(0, 16) : new Vec2L(objX + stopShiftMm, 330_000);
        return Setup(DefaultSides(null, new[] { obj }), n,
            new FormationSpec(idleId, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(2, 16)),
            new FormationSpec(obsId, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 20), 10, new Vec2L(obsX, 330_000)) { Posture = Posture.Defend, Morale = observerMorale },
            new FormationSpec(3, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(40, 16)));
    }

    /// <summary>P2-V1. Before round 2 the auto credited the LOWEST-ID on-field formation of the observing side, so with the idle
    /// formation labelled 1 it was "active" in pulse N - 1 and never routed (measured: no verdict, SideEliminated after 10 / 11
    /// pulses), while relabelled 2 it routed. Now, under either labelling, both resolvers rout the idle formation at the end of
    /// pulse N - 1 and end in a Standoff after 2N pulses at the same instant, with identical verdict lists.</summary>
    [Theory]
    [InlineData(3, 1L, 2L)]
    [InlineData(3, 2L, 1L)]
    [InlineData(5, 1L, 2L)]
    [InlineData(5, 2L, 1L)]
    public void AFirstDetectionIsTheObserversProgressUnderAnyIdLabelling(int n, long idleId, long obsId)
    {
        var s = FirstDetection(n, idleId, obsId);
        var (tb, t) = Tactical(s);
        var (ab, a) = Auto(s);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Equal(2L * n, r.Pulses);
            Assert.Equal(2L * n * 60_000, r.EndMs);
            Assert.Equal(10, r.Formations.Single(f => f.FormationId == idleId).RoutedEscaped);
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.Contains(new InactivityVerdict(n - 1, idleId, 0, n, InactivityExemption.None, true), ab.InactivityVerdicts);
        long k = ab.FirstKnownStep(0, 3);
        Assert.Equal(n - 1, k / 30);                                           // the detection falls in pulse N - 1
    }

    /// <summary>P2-V1 with N = 1: the Defend observer sees the enemy at once (pulse 0). Both labellings, both resolvers: the idle
    /// formation routs at the end of pulse 0, the Standoff ends after 2 pulses (120,000 ms) — before round 2 the auto never routed
    /// formation 1 (SideEliminated after 10 pulses) and, with the clamped axis (P2-V3), ended the relabelled case one pulse late.</summary>
    [Theory]
    [InlineData(1L, 2L)]
    [InlineData(2L, 1L)]
    public void AnImmediateFirstDetectionIsTheObserversProgressInBothResolvers(long idleId, long obsId)
    {
        var s = Setup(DefaultSides(null, new[] { Cell(0, 16) }), 1,
            new FormationSpec(idleId, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(2, 16)),
            new FormationSpec(obsId, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 40), 10, Cell(10, 16)) { Posture = Posture.Defend },
            new FormationSpec(3, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(40, 16)) { Posture = Posture.Defend });
        var (tb, t) = Tactical(s);
        var (ab, a) = Auto(s);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Equal(2, r.Pulses);
            Assert.Equal(120_000, r.EndMs);
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.Contains(new InactivityVerdict(0, idleId, 0, 1, InactivityExemption.None, true), ab.InactivityVerdicts);
    }

    /// <summary>P2-V1 side effect, pinned. The first-detection credit is inactivity-rule progress only: it is NOT activity for
    /// the Hold morale recovery (tactical PulseEnd: a Hold order and no casualties recover, whatever else the pulse held). Before
    /// round 2 the credit set the auto's ActivePulse, which also blocks the recovery. The Defend observer (morale 500) makes the
    /// first detection in pulse N - 1, takes no casualties and recovers 20 in every one of the 2N pulses in both resolvers.</summary>
    [Theory]
    [InlineData(3, 1L, 2L)]
    [InlineData(5, 2L, 1L)]
    public void AFirstDetectionDoesNotStopTheHoldRecovery(int n, long idleId, long obsId)
    {
        var s = FirstDetection(n, idleId, obsId, observerMorale: 500);
        var (_, t) = Tactical(s);
        var (ab, a) = Auto(s);
        Assert.Equal(n - 1, ab.FirstKnownStep(0, 3) / 30);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(2L * n, r.Pulses);
            var obs = r.Formations.Single(f => f.FormationId == obsId);
            Assert.Equal(0, obs.Killed);
            Assert.Equal(500 + 2 * n * 20, obs.MoraleEnd);
        }
    }

    // ------------------------------------------------------------------ P2-V2: approach minima to a tracked enemy

    /// <summary>P2-V2. Same geometry, but the enemy keeps marching toward the idle formation (objective at the far edge): the
    /// idle formation has no order (tactical AI: NoContact in pulses 0..N-1) yet the enemy its side TRACKS comes closer by more
    /// than a cell per pulse, which the tactical engine counts as its progress (UpdateMinima). Neither resolver judges it idle
    /// (before round 2 the auto routed it at the end of pulse N - 1, measured n = 3: {2, f2, 3, None, Routed}).</summary>
    [Theory]
    [InlineData(3, 1L, 2L)]
    [InlineData(3, 2L, 1L)]
    [InlineData(5, 2L, 1L)]
    public void AnApproachingTrackedEnemyIsProgressInBothResolvers(int n, long idleId, long obsId)
    {
        var s = FirstDetection(n, idleId, obsId, farObjective: true);
        var (tb, _) = Tactical(s);
        var (ab, _) = Auto(s);
        Assert.DoesNotContain(tb.InactivityVerdicts, v => v.FormationId == idleId);
        Assert.DoesNotContain(ab.InactivityVerdicts, v => v.FormationId == idleId);
    }

    /// <summary>P2-V2, turn-exact threshold of the approach minimum (auto). The enemy's march is first measured one step after
    /// its first detection (the minimum runs on the PREVIOUS step's tracking, as in the tactical step order); its objective is
    /// then placed so that it stops exactly delta mm nearer the idle formation than that first measurement, inside pulse N - 1.
    /// delta = S - 1: no new minimum, the idle formation routs at the end of pulse N - 1 (exemption None); delta = S and S + 1:
    /// one new minimum (progress), so it is not judged at the end of pulse N - 1 — and from the next pulse start its side, which
    /// now knows the enemy, orders it to attack, so the DIR5 rule never routs it.</summary>
    [Theory]
    [InlineData(3, -1L, false)]
    [InlineData(3, 0L, true)]
    [InlineData(3, 1L, true)]
    [InlineData(5, -1L, false)]
    [InlineData(5, 0L, true)]
    public void AnApproachOfOneCellIsExactlyTheProgressThreshold(int n, long offset, bool progress)
    {
        var (cal, _) = Auto(FirstDetection(n, 2, 1, farObjective: true), log: true);
        long k0 = cal.FirstKnownStep(0, 3);
        long x2 = cal.Log.First(x => x.FormationId == 2).PosMm;
        long x3First = cal.Log.Single(x => x.FormationId == 3 && x.Step == k0 + 1).PosMm;   // the first measurement
        long stopX = x3First - (S + offset);
        var objective = cal.Axis!.At(stopX);
        Assert.Equal(330_000, objective.Y);
        var s = Setup(DefaultSides(null, new[] { objective }), n,
            new FormationSpec(2, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(2, 16)),
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 20), 10, FirstDetection(n, 2, 1).Formations.Single(f => f.Id == 1).Anchor) { Posture = Posture.Defend },
            new FormationSpec(3, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(40, 16)));
        var (b, _) = Auto(s, log: true);
        Assert.Equal(k0, b.FirstKnownStep(0, 3));
        var stop = b.Log.Where(x => x.FormationId == 3).Last(x => x.Moved);
        Assert.Equal(stopX, stop.PosMm);                                       // it stopped exactly there ...
        Assert.Equal(n - 1, stop.Step / 30);                                   // ... inside pulse N - 1
        Assert.Equal(x3First - x2 - (S + offset), stop.PosMm - x2);
        if (progress)
        {
            Assert.DoesNotContain(b.InactivityVerdicts, v => v.FormationId == 2);
            Assert.Equal(0, b.Log.Single(x => x.FormationId == 2 && x.Step == 30L * n - 1).Mode);   // idle through pulse N - 1 ...
            Assert.Contains(b.Log, x => x.FormationId == 2 && x.Step >= 30L * n && x.Moved);      // ... then it attacks
        }
        else Assert.Equal(new[] { new InactivityVerdict(n - 1, 2, 0, n, InactivityExemption.None, true) },
                          b.InactivityVerdicts.Where(v => v.FormationId == 2));
    }

    // ------------------------------------------------------------------ P2-V3: true depth on the model-2 axis

    /// <summary>P2-V3. The rear echelons of the battery's multi-formation scenarios stand 40 m behind their front line. The
    /// model-2 axis used to clamp every projection to [0, Length], moving them up to their side's mean anchor (20 m of depth,
    /// measured). The extended axis puts every formation at its own anchor (all of them lie on the axis line here) and keeps
    /// the true 40,000 mm depth in both orientations.</summary>
    [Theory]
    [InlineData(PScenario.CombinedArms, 1UL)]
    [InlineData(PScenario.CombinedArms, 2UL)]
    [InlineData(PScenario.CombinedArmsVsMass, 1UL)]
    [InlineData(PScenario.SniperScreen, 1UL)]
    [InlineData(PScenario.SniperScreen, 2UL)]
    public void RearEchelonsKeepTheirTrueDepthOnTheModelTwoAxis(PScenario id, ulong seed)
    {
        var s = ParityBatteryP.Build(id, seed);
        var (b, _) = Auto(s, log: true);
        var ax = b.Axis!;
        foreach (var f in s.Formations.Where(f => f.Anchor.Y == 330_000)) Assert.Equal(f.Anchor, ax.At(ax.Project(f.Anchor)));
        int deep = 0;
        foreach (var f in s.Formations)
            foreach (var g in s.Formations)
            {
                if (g.Id <= f.Id || g.Side != f.Side || g.Anchor.Y != f.Anchor.Y) continue;
                long trueDepth = Math.Abs(f.Anchor.X - g.Anchor.X);
                Assert.Equal(trueDepth, Math.Abs(ax.Project(f.Anchor) - ax.Project(g.Anchor)));
                if (trueDepth == 40_000) deep++;
            }
        Assert.True(deep >= 1, "no echelon 40 m deep in this scenario");
    }

    // ------------------------------------------------------------------ P2-V4: exemption (a) and the setup's clause

    /// <summary>P2-V4. An idle infantry in a jungle patch (concealing), an enemy Defend line 3 cells away, both with sight 1.
    /// The tactical exemption (a) applies only when the setup's ActiveConcealmentRule is on; the auto port read it regardless
    /// (measured, clause off: tactical rout + SideEliminated, auto exempt + Standoff). Both resolvers now agree in all four cases.</summary>
    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    [InlineData(false, 5)]
    public void ExemptionAFollowsTheSetupsConcealmentClauseInBothResolvers(bool clause, int n)
    {
        var g = OpenField();
        TT.Fill(g, 4, 14, 6, 18, TerrainClass.Jungle);
        var conceal = ConcealmentTable.Create(new[]
        {
            Q32.One, Q32.One, Q32.FromRatio(1, 2), Q32.Zero, Q32.One, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One, Q32.Zero,
        });
        var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), conceal, DefaultSides(null, null), new[]
        {
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(5, 16)),
            new FormationSpec(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(8, 16)) { Posture = Posture.Defend },
        }, 7) { Rules = new TacticalRules { InactivityPulses = n, ActiveConcealmentRule = clause } };
        var (tb, t) = Tactical(s);
        var (ab, a) = Auto(s);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(clause ? BattleEndReason.Standoff : BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(n, r.Pulses);
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.Contains(clause ? new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.ConcealedEnemyInReach, false)
                               : new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true), ab.InactivityVerdicts);
    }

    // ------------------------------------------------------------------ P2-V5: convergence mobility wiring

    private static long ExpectedCap(BattleSetup s, Vec2L attacker, Vec2L holder)
    {
        var grid = s.Grid; var fx = s.Effects!;
        var ca = grid.CellOf(attacker); var ch = grid.CellOf(holder);
        long cost = s.Costs.Cost(grid.ClassAt(ca.X, ca.Y));
        if (ca != ch)
            cost += fx.ClimbCostPerMm(grid.HeightAt(ch.X, ch.Y) - grid.HeightAt(ca.X, ca.Y),
                                      Vec2L.Distance(grid.CellCenter(ca.X, ca.Y), grid.CellCenter(ch.X, ch.Y)));
        return AutoBattle.ConvergenceCapOf(V2.MaxAttackersPerHolderPermille, V2.ConvergenceMobilityPermille, cost);
    }

    /// <summary>P2-V5 (mutant MV6, the climb sign). In the battle, every melee attack on a Defend holder is capped by
    /// ConvergenceCapOf(cap, w, class cost of the attacker's cell + the Naismith climb FROM the attacker's cell TOWARD the
    /// holder's): attacking up the 10 m crest the cap is exactly 386 permille (m = 193); attacking DOWN onto a holder at the foot
    /// of the same terrace there is no climb and the cap is the open-ground 2,000. The holder's own blows on a non-Defend
    /// attacker carry the fixed 500. (The named test of round 1 checked only the static formula.)</summary>
    [Theory]
    [InlineData(1UL, false)]
    [InlineData(2UL, false)]
    [InlineData(1UL, true)]
    [InlineData(2UL, true)]
    public void ConvergenceCapChargesTheClimbTowardTheHolder(ulong seed, bool descend)
    {
        var s = ParityBatteryP.Build(PScenario.HillCrest, seed);
        if (descend)
        {
            // the same terrace turned round: the attacker's side is the plateau, the holder stands at its foot
            for (int y = 0; y < FieldH; y++)
                for (int x = 0; x < FieldW; x++)
                {
                    bool attackerSide = seed % 2 == 1 ? x < 32 : x > MirrorColumn(32);
                    s.Grid.Set(x, y, TerrainClass.Open, attackerSide ? 10_000 : 0);
                }
        }
        var (b, _) = Auto(s, log: true);
        var onHolder = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 1).ToList();
        var byHolder = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 2).ToList();
        Assert.NotEmpty(onHolder); Assert.NotEmpty(byHolder);
        foreach (var t in onHolder) Assert.Equal(ExpectedCap(s, t.AttackerPos, t.TargetPos), t.PerTargetPermille);
        var acrossTheStep = onHolder.Where(t => s.Grid.CellOf(t.AttackerPos) != s.Grid.CellOf(t.TargetPos)).ToList();
        Assert.NotEmpty(acrossTheStep);
        Assert.All(acrossTheStep, t => Assert.Equal(descend ? 2_000 : 386, t.PerTargetPermille));
        Assert.All(acrossTheStep, t => Assert.True(descend ? t.GradientRaw < 0 : t.GradientRaw > 0));
        Assert.All(byHolder, t => Assert.Equal(V2.MaxAttackersPerTargetPermille, t.PerTargetPermille));
    }

    // ------------------------------------------------------------------ P2-V6: turn-exact pins

    private static BattleSetup IdleOnTheRow(int n, UnitProfile idle, Vec2L[]? objectives = null) =>
        Setup(DefaultSides(objectives, null), n,
            new FormationSpec(1, 0, TT.WithSight(idle, 1), 10, Cell(6, 16)),
            new FormationSpec(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(42, 16)) { Posture = Posture.Defend },
            new FormationSpec(3, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(3, 16)) { Posture = Posture.Defend });

    /// <summary>MV3 (flight end). Formation 1 idles hidden in the open at Cell(6, 16) (exit field 120,000,000 cost-mm) and routs at
    /// the end of pulse N - 1 (step 30N - 1); its flight lasts ceil(120,000,000 / (1000 x per-step catch-up pace)) steps: 29 at
    /// speed 1,400 mm/s, 30 at 1,340, 31 at 1,300 (the HI pace). It has left the field AT its escape step (as a withdrawal at
    /// EscapeStep, and as the tactical routing members that have all escaped by the pulse end): a flight of exactly 30 steps ends
    /// on the next pulse-end step and is NO progress there, so the Standoff (never reset) ends after N + 1 pulses, as for 29; a
    /// flight of 31 steps is still in transit at that pulse end — progress — and the Standoff ends after 2N + 1 pulses.</summary>
    [Theory]
    [InlineData(4, 1_400L, 5L)]
    [InlineData(4, 1_340L, 5L)]
    [InlineData(4, 1_300L, 9L)]
    [InlineData(3, 1_340L, 4L)]
    [InlineData(3, 1_300L, 7L)]
    public void AFlightEndingExactlyOnAPulseEndIsNoProgressThere(int n, long speedMmPerS, long pulses)
    {
        var p = UnitProfiles.HeavyInfantry with { SpeedNum = speedMmPerS, SpeedDen = 1_300 };
        var (b, r) = Auto(IdleOnTheRow(n, p));
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(pulses, r.Pulses);
        Assert.Equal(pulses * 60_000, r.EndMs);
        Assert.Contains(new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true), b.InactivityVerdicts);
        Assert.Equal(10, r.Formations.Single(f => f.FormationId == 1).RoutedEscaped);
    }

    /// <summary>MV4 (objective clause of exemption (b), boundary). Formation 1 idles hidden at Cell(6, 16) with its side's
    /// objective S + delta mm straight to its flank (the objective projects onto its own axis point, so its MoveTo order never
    /// moves it). Within one cell, INCLUSIVE (|p - o|² &lt;= S², the tactical ExemptionsNow), it is exempt (DefensiveHold) and the
    /// battle ends as a Standoff after N pulses; one millimetre farther it routs at the end of pulse N - 1.</summary>
    [Theory]
    [InlineData(4, -1L, false)]
    [InlineData(4, 0L, false)]
    [InlineData(4, 1L, true)]
    [InlineData(5, 0L, false)]
    [InlineData(5, 1L, true)]
    public void OwnObjectiveExemptsExactlyWithinOneCell(int n, long delta, bool routs)
    {
        var at = Cell(6, 16);
        var (b, r) = Auto(IdleOnTheRow(n, UnitProfiles.HeavyInfantry, new[] { new Vec2L(at.X, at.Y + S + delta) }), log: true);
        Assert.All(b.Log.Where(x => x.FormationId == 1), x => Assert.False(x.Moved));
        Assert.Contains(routs ? new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true)
                              : new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.DefensiveHold, false), b.InactivityVerdicts);
        if (!routs)
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Equal(n, r.Pulses);
        }
    }

    /// <summary>MV11 (reveal window). A sniper team with personal concealment 0 fires every 5 steps from step 0 at an infantry
    /// marching on it; the infantry's start is shifted by whole steps of its march (2,600 mm), so its arrival in sight falls on
    /// every residue of the fire interval. Every detection draw on the team has chance exactly One when the team fired in the
    /// step before (tactical RevealUntilMs = t + StepMs: the next step only) and exactly Zero otherwise, so the team is first known
    /// at the first in-sight step that follows a volley. At least one shift puts an in-sight draw TWO steps after a volley with
    /// chance Zero (the window's end; round 1 pinned only its start).</summary>
    [Fact]
    public void TheRevealWindowIsExactlyTheStepAfterAVolley()
    {
        var team = DomainProfiles.Sniper with { PersonalConcealment = Q32.Zero };
        int windowEnds = 0;
        for (int shift = 0; shift <= 6; shift++)
        {
            var s = TT.Setup(OpenField(), DefaultSides(new[] { Cell(33, 16) }, null), 1, null,
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, new Vec2L(Cell(14, 16).X + 2_600 * shift, 330_000)),
                new FormationSpec(2, 1, team, 2, Cell(33, 16)) { Posture = Posture.Defend });
            var (b, _) = Auto(s, log: true);
            var volleys = b.Log.Where(x => x.FormationId == 2 && (x.Mode & 2) != 0).Select(x => x.Step).ToList();
            var draws = b.DetectionDraws.Where(d => d.TargetId == 2 && d.Side == 0).OrderBy(d => d.Step).ToList();
            Assert.NotEmpty(draws);
            foreach (var d in draws) Assert.Equal(volleys.Contains(d.Step - 1) ? Q32.One : Q32.Zero, d.Chance);
            var first = draws.First(d => d.Chance == Q32.One);
            Assert.True(first.Detected);
            Assert.Equal(first.Step, b.FirstKnownStep(0, 2));
            windowEnds += draws.Count(d => volleys.Contains(d.Step - 2) && !volleys.Contains(d.Step - 1));
        }
        Assert.True(windowEnds > 0, "no in-sight draw two steps after a volley: the window's end is not exercised");
    }

    /// <summary>MV2 (tracking kept by contact). Formation 1 (sight 0: it never has a line of sight) stands in contact with an
    /// enemy (sight 0) that withdraws from step 0 (morale 400 &lt; 450; 41 cells from its edge: 211 steps in transit) and stays on
    /// the field, in contact, until its escape step.
    /// The withdrawing enemy is not a target, so formation 1 neither moves nor fights: it is idle. Its tracking by the enemy
    /// rests on contact alone (tactical Detect: kept while visible OR in contact), so it is NOT hidden and the DIR5 rule never
    /// routs it; the battle ends when the enemy escapes, formation 1 holding the field. A port that kept tracking on line of
    /// sight only would lose it every other step and rout it at a pulse end.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AContactKeepsTrackingWithoutLineOfSight(int n)
    {
        var s = Setup(DefaultSides(null, null), n,
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 0), 10, new Vec2L(130_000, 330_000)),
            new FormationSpec(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 0), 10, new Vec2L(131_000, 330_000)) { Morale = 400 });
        var (b, r) = Auto(s, log: true);
        Assert.Equal(0, b.FirstKnownStep(0, 2));
        Assert.Equal(0, b.FirstKnownStep(1, 1));
        Assert.True(r.Pulses > n, "the enemy is still withdrawing at the end of pulse N - 1");
        Assert.DoesNotContain(b.InactivityVerdicts, v => v.FormationId == 1);
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(1, r.Side);
        Assert.Equal(0, r.HoldsField);
        var f1 = r.Formations.Single(f => f.FormationId == 1);
        Assert.Equal(10, f1.ActiveAtEnd);
        Assert.Equal(0, f1.Killed);
    }
}
