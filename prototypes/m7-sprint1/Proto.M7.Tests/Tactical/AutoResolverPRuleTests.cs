using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [P fix r1] Lane P verification round 1. (P-V2) The Slice E rules re-stated on the DEFAULT AutoResolver model 2 — the Sprint 1
/// rule tests (AutoResolverTests, AutoResolverVerificationFixTests) pin model 1 only; (P-V1) the step-0 reveal defect and the
/// reveal window; (P-V4) the DIR5 port compared with the TacticalResolver on the SAME setups (hidden = currently untracked, the
/// Standoff counter across a rout, the setup's own inactivity rule); (P-V3) the charge-impetus wiring; (P-V5) the withdrawal
/// travel time, the detection-draw wiring and rule P1's reference-only spread. Every setup and profile variant is a TEST INPUT.
/// </summary>
public class AutoResolverPRuleTests
{
    private static readonly AutoCoefficients V2 = new();

    private static T Bounded<T>(Func<T> f)
    {
        var t = Task.Run(f);
        if (!t.Wait(TimeSpan.FromSeconds(60))) Assert.Fail("HARNESS GUARD: AutoResolver did not terminate within 60 s (NON-TERMINATION is a FAILURE)");
        return t.Result;
    }

    private static BattleResult Auto(BattleSetup s, AutoCoefficients? c = null)
    {
        var r = Bounded(new AutoBattle(s, c ?? V2).Run);
        TT.AssertConserved(r);
        return r;
    }

    private static (AutoBattle B, BattleResult R) Logged(BattleSetup s, AutoCoefficients? c = null)
    {
        var b = new AutoBattle(s, c ?? V2) { RecordLog = true };
        var r = Bounded(b.Run);
        TT.AssertConserved(r);
        return (b, r);
    }

    private static BattleResult Tactical(BattleSetup s) => Tactical(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));

    private static BattleResult Tactical(TacticalBattle b)
    {
        var r = TT.RunGuarded(b);
        TT.AssertConserved(r);
        return r;
    }

    private static long WithdrawnEscaped(FormationResult f) => f.Escaped - f.RoutedEscaped - f.DisengagedEscaped;

    // ------------------------------------------------------------------ P-V1: stationary concealment, the reveal window

    /// <summary>P-V1. Range-6 archers 5 cells from an Ambush-posture infantry waiting in forest, near-perfect stationary factor
    /// (TT.HideRules / HideRulesDir5): model 2 must NEVER detect it — the former reveal test "LastFoughtStep == k - 1" with the
    /// sentinel -1 revealed every formation at step 0 and the archers shot it at once (1,166 / 1,096 / 1,220 damage, measured).
    /// Both resolvers deal 0 damage and end the same way at the same pulse under either inactivity rule; with stationary factor
    /// 1 model 2 finds and shoots the infantry (control).</summary>
    [Theory]
    [InlineData(1UL, false)]
    [InlineData(2UL, false)]
    [InlineData(3UL, false)]
    [InlineData(1UL, true)]
    [InlineData(2UL, true)]
    [InlineData(3UL, true)]
    public void StationaryTargetInConcealingTerrainIsNeverFoundByModelTwo(ulong seed, bool dir5)
    {
        var archers = UnitProfiles.Archers with { Name = "test/archers-range6", RangeCells = 6 };
        BattleSetup S(TacticalRules rules)
        {
            var g = OpenField(); TT.Fill(g, 18, 10, 24, 22, TerrainClass.Forest);
            return TT.Setup(g, DefaultSides(), seed, rules,
                new FormationSpec(1, 0, archers, 60, Cell(14, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 50, Cell(19, 16)) { Posture = Posture.Ambush });
        }
        var hidden = S(dir5 ? TT.HideRulesDir5() : TT.HideRules());
        var (b, a) = Logged(hidden);
        Assert.Equal(-1, b.FirstKnownStep(0, 2));
        Assert.All(b.DetectionDraws.Where(d => d.TargetId == 2), d => Assert.False(d.Detected));
        var t = Tactical(hidden);
        foreach (var r in new[] { a, t })
        {
            Assert.Equal(0, r.Formations[0].DamageDealt);
            Assert.Equal(0, r.Formations[1].Killed);
            Assert.NotEqual(BattleEndReason.SideEliminated, r.Reason);
        }
        Assert.Equal(t.Reason, a.Reason);
        Assert.Equal(t.Pulses, a.Pulses);
        var seen = Auto(S((dir5 ? TT.HideRulesDir5() : TT.HideRules()) with { StationaryConcealment = Q32.One }));
        Assert.True(seen.Formations[0].DamageDealt > 0, "without the stationary factor model 2 finds and shoots the infantry");
    }

    /// <summary>P-V1 / mutant V3. The reveal window is exactly the step after an attack (tactical RevealUntil): a 2-man sniper
    /// team whose personal concealment is 0 (TEST INPUT: undetectable by sight unless revealed) holds (Defend) on open ground and
    /// fires every 5 steps from step 0 at an HI formation marching on it from 19 cells (beyond the HI's 12-cell sight). The team
    /// is first known in the step after the first volley fired while the HI is within its sight: volley at 55, known at 56; the
    /// earlier reveals (steps 1, 6, ..., 51) fall while the HI is still beyond its sight. Without the reveal exception for
    /// personal concealment (V3) the team is found only by contact.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void AConcealedShooterIsRevealedExactlyInTheStepAfterItsVolley(ulong seed)
    {
        var team = DomainProfiles.Sniper with { PersonalConcealment = Q32.Zero };
        var s = TT.Setup(OpenField(), DefaultSides(new[] { Cell(33, 16) }, null), seed, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)),
            new FormationSpec(2, 1, team, 2, Cell(33, 16)) { Posture = Posture.Defend });
        var (b, _) = Logged(s);
        var volleys = b.Log.Where(x => x.FormationId == 2 && (x.Mode & 2) != 0).Select(x => x.Step).ToList();
        Assert.Equal(0L, volleys[0]);
        long known = b.FirstKnownStep(0, 2);
        Assert.Equal(56L, known);
        Assert.Contains(55L, volleys);
        long sight = UnitProfiles.HeavyInfantry.SightCells * s.Grid.CellSizeMm;
        long teamX = b.Log.First(x => x.FormationId == 2).PosMm;
        long Gap(long step) => teamX - b.Log.Single(x => x.FormationId == 1 && x.Step == step).PosMm;
        Assert.True(Gap(56) <= sight, $"in sight at 56: gap {Gap(56)}");
        foreach (long v in volleys.Where(v => v < 55)) Assert.True(Gap(v + 1) > sight, $"reveal at {v + 1}: gap {Gap(v + 1)} must be beyond sight");
        // every draw on the team before 56 had chance 0 (personal concealment 0, not revealed or out of sight)
        Assert.All(b.DetectionDraws.Where(d => d.TargetId == 2 && d.Step < 56), d => Assert.Equal(Q32.Zero, d.Chance));
        Assert.Contains(b.DetectionDraws, d => d.TargetId == 2 && d.Step == 56 && d.Detected && d.Chance == Q32.One);
    }

    // ------------------------------------------------------------------ P-V2: the Slice E rules on model 2

    private static TerrainGrid WaterRing()
    {
        var g = OpenField();
        for (int y = 12; y <= 20; y++) for (int x = 10; x <= 18; x++)
            if (y == 12 || y == 20 || x == 10 || x == 18) g.Set(x, y, TerrainClass.Water);
        return g;
    }

    private static BattleSetup Island(TerrainGrid g, long morale) => TT.Setup(g, DefaultSides(null, new[] { Cell(14, 16) }), 4, null,
        new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)) { Morale = morale },
        new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 16)));

    /// <summary>Spec §4 / E-V3 on model 2: a formation that routs on an island ringed by Water surrenders entirely, in BOTH
    /// resolvers; with the doctrine withdrawal off (TEST INPUT) the rout rule alone gives the same; on open ground the routed
    /// formation escapes except the fitted surrender share (model 2: 0).</summary>
    [Fact]
    public void TrappedRoutSurrendersEntirelyInBothResolversModelTwo()
    {
        var s = Island(WaterRing(), 290);
        foreach (var r in new[] { Tactical(s), Auto(s) })
        {
            var f = r.Formations[0];
            Assert.Equal(20, f.Surrendered);
            Assert.Equal(0, f.Escaped);
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(0, r.Side);
        }
        var noDoctrine = V2 with { DoctrineRetreatMorale = 0 };
        Assert.Equal(20, Auto(s, noDoctrine).Formations[0].Surrendered);
        var open = Auto(Island(OpenField(), 290), noDoctrine).Formations[0];
        Assert.Equal(FixedMath.MulDivFloor(20, noDoctrine.SurrenderPermille, 1000), open.Surrendered);
        Assert.Equal(20 - open.Surrendered, open.RoutedEscaped);
    }

    /// <summary>E-V3 on model 2: below the doctrine's retreat morale on the island the formation cannot withdraw (no passable
    /// path), in BOTH resolvers; on open ground model 2 withdraws it intact.</summary>
    [Fact]
    public void TrappedFormationCannotWithdrawInBothResolversModelTwo()
    {
        var trapped = Island(WaterRing(), 400);
        foreach (var r in new[] { Tactical(trapped), Auto(trapped) }) Assert.Equal(0, WithdrawnEscaped(r.Formations[0]));
        Assert.Equal(20, WithdrawnEscaped(Auto(Island(OpenField(), 400)).Formations[0]));
    }

    /// <summary>Hold morale recovery on model 2: a formation that neither moved nor fought nor took casualties recovers
    /// HoldRecovery per pulse, capped at MoraleMax: 800 + 5 x 20 = 900 and 990 -&gt; 1000, under the setup's inactivity rule
    /// in BOTH resolvers — legacy: both disengage after 5 pulses; DIR5 (default, mutual guard off): both hidden idle
    /// formations rout at the end of pulse 5 (BothEliminated at 300,000 ms, all survivors routed-escaped).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdleFormationRecoversHoldMoraleInBothResolversModelTwo(bool legacy)
    {
        var rules = new TacticalRules();
        var s = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 9);
        s = s with { Sides = DefaultSides(), Formations = new[] { s.Formations[0] with { Morale = 800 }, s.Formations[1] with { Morale = 990 } } };
        if (legacy) s = s with { Rules = TT.Legacy };
        foreach (var r in new[] { Tactical(s), Auto(s) })
        {
            Assert.Equal(legacy ? BattleEndReason.BothDisengaged : BattleEndReason.BothEliminated, r.Reason);
            Assert.Equal(rules.InactivityPulses, r.Pulses);
            Assert.Equal(rules.InactivityPulses * rules.PulseMs, r.EndMs);
            Assert.Equal(800 + rules.InactivityPulses * rules.HoldRecovery, r.Formations[0].MoraleEnd);
            Assert.Equal(rules.MoraleMax, r.Formations[1].MoraleEnd);
            Assert.All(r.Formations, f => Assert.Equal(legacy ? 0 : 20, f.RoutedEscaped));
        }
    }

    /// <summary>Withdrawal travel time on model 2 (mutant V6): the exit field's least-cost distance (cost 1000 x mm on open
    /// ground: the edge field is measured to the edge cell's centre — 280,000 mm from column 14, 380,000 mm from column 19)
    /// at CATCH-UP pace 1,300 x 3/2 x 2 s = 3,900 mm per step: ceil(280,000 / 3,900) = 72 and ceil(380,000 / 3,900) = 98 steps;
    /// the battle ends at the step the withdrawal escapes: EndMs = (steps - 1) x 2,000 = 142,000 / 194,000 ms. (At walking
    /// pace 2,600 mm it would be 108 / 147 steps.)</summary>
    [Theory]
    [InlineData(14L, 280_000_000L, 72L)]
    [InlineData(19L, 380_000_000L, 98L)]
    public void DoctrineWithdrawalTravelsTheExitFieldAtCatchUpPaceInModelTwo(long column, long costMm, long steps)
    {
        var rules = new TacticalRules();
        var s = ParityScenarios.Build(ParityScenarioId.LowMorale, 1, false);
        s = s with { Formations = new[] { s.Formations[0] with { Anchor = Cell(column, 16) }, s.Formations[1] } };
        var cell = s.Grid.CellOf(Cell(column, 16));
        Assert.Equal(costMm, new EdgeField(s.Grid, s.Costs, s.Sides[0].Exit, s.Effects).DistanceAt(s.Grid.Index(cell.X, cell.Y)));
        long perStep = FixedMath.MulDivFloor(rules.RefFootSpeedMmPerS * rules.CatchUpNum, rules.StepMs, 1000 * rules.CatchUpDen);
        Assert.Equal(3_900, perStep);
        Assert.Equal(steps, FixedMath.DivCeil(costMm, perStep * 1000));
        var r = Auto(s, V2 with { DoctrineRetreatMorale = 600 });
        Assert.Equal(50, WithdrawnEscaped(r.Formations[0]));
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal((steps - 1) * rules.StepMs, r.EndMs);
    }

    /// <summary>The rout rule is strict on model 2 (morale &lt; RoutThreshold), as in the tactical engine.</summary>
    [Theory]
    [InlineData(0L, false)]
    [InlineData(-1L, true)]
    public void RoutThresholdIsStrictInModelTwo(long offset, bool routsAtOnce)
    {
        var rules = new TacticalRules();
        var s = ParityScenarios.Build(ParityScenarioId.EqualInfantry, 1, false);
        s = s with { Formations = new[] { s.Formations[0] with { Morale = rules.RoutThreshold + offset }, s.Formations[1] } };
        var r = Auto(s, V2 with { DoctrineRetreatMorale = 0 });
        var a = r.Formations[0];
        if (routsAtOnce)
        {
            Assert.Equal(0, r.EndMs);
            Assert.Equal(0, a.Killed);
            Assert.Equal(a.Start - a.Surrendered, a.RoutedEscaped);
            Assert.Equal(1, r.HoldsField);
        }
        else
        {
            Assert.True(r.EndMs > 0);
            Assert.True(a.Killed > 0, "a formation exactly at the threshold fights");
        }
    }

    // ------------------------------------------------------------------ P-V4: the DIR5 port against the TacticalResolver

    private static BattleSetup StandoffAfterRout(int n, long column = 5)
    {
        var g = OpenField();
        TT.Fill(g, (int)column - 1, 14, (int)column + 1, 18, TerrainClass.Jungle);
        var conceal = ConcealmentTable.Create(new[]
        {
            Q32.One, Q32.One, Q32.FromRatio(1, 2), Q32.Zero, Q32.One, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One, Q32.Zero,
        });
        return new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), conceal, DefaultSides(null, null), new[]
        {
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 12), 10, Cell(column, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(42, 16)) { Posture = Posture.Defend },
            // the short-flight variant keeps side 0 on one row so the auto's axis point of formation 1 is its own cell by the edge
            new FormationSpec(3, 0, UnitProfiles.HeavyInfantry, 10, column <= 2 ? Cell(column, 20) : Cell(5, 2)) { Posture = Posture.Defend },
        }, 7) { Rules = new TacticalRules { InactivityPulses = n } };
    }

    /// <summary>P-V4(b). Formation 1 idles hidden in a jungle patch (never detectable) and routs at the end of pulse N-1; the
    /// two Defend lines stay. The whole-field Standoff counter is NOT reset by the rout (it was in the auto: Standoff one pulse
    /// early, 8 vs 9 and 10 vs 11, measured); the routed formation's flight in transit at the end of pulse N is progress (the
    /// tactical routing members approach their edge), so G restarts there and the Standoff ends pulse 2N: both resolvers
    /// end at the same instant with the same verdicts. Neighbour case (mutant V9b, the reset): formation 1 idles one cell from
    /// its edge, so its flight ends inside pulse N — no member is on the field at that pulse end, no progress (tactical trace
    /// G = 1, 2, 3, 4, 4 MEASURED) — and G, never reset, is still N: the Standoff ends right then, after N + 1 pulses, in both
    /// resolvers (a reset would delay it N - 1 pulses).</summary>
    [Theory]
    [InlineData(4, 5L, 9L, 540_000L)]
    [InlineData(5, 5L, 11L, 660_000L)]
    [InlineData(4, 1L, 5L, 300_000L)]
    [InlineData(5, 1L, 6L, 360_000L)]
    public void StandoffAfterADir5RoutEndsAtTheSameInstantInBothResolvers(int n, long column, long pulses, long endMs)
    {
        var s = StandoffAfterRout(n, column);
        var tb = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
        var t = Tactical(tb);
        var ab = new AutoBattle(s, V2);
        var a = Bounded(ab.Run);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Equal(pulses, r.Pulses);
            Assert.Equal(endMs, r.EndMs);
            Assert.Equal(10, r.Formations.Single(f => f.FormationId == 1).RoutedEscaped);
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.Contains(new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true), ab.InactivityVerdicts);
    }

    /// <summary>P-V4 / mutant V1 (the hidden conjunct). Formation 1 idles in the OPEN, with no objective and a 1-cell sight;
    /// the Defend line 30 cells away sees it from step 0 and keeps tracking it. Tracked = not hidden: it never routs in either
    /// resolver, and the battle ends as a Standoff at the end of pulse N (pulse 0 had the first detection) with the same verdicts.
    /// A port that ignores the hidden conjunct routs formation 1.</summary>
    [Theory]
    [InlineData(4, 5L)]
    [InlineData(5, 6L)]
    public void ATrackedIdleFormationIsNotHiddenAndNeverRoutsInBothResolvers(int n, long pulses)
    {
        var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(null, null), new[]
        {
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(10, 16)),
            new FormationSpec(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 40), 10, Cell(40, 16)) { Posture = Posture.Defend },
        }, 7) { Rules = new TacticalRules { InactivityPulses = n } };
        var tb = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
        var t = Tactical(tb);
        var ab = new AutoBattle(s, V2);
        var a = Bounded(ab.Run);
        Assert.Equal(0, ab.FirstKnownStep(1, 1));
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Equal(pulses, r.Pulses);
            Assert.All(r.Formations, f => { Assert.Equal(10, f.ActiveAtEnd); Assert.Equal(0, f.RoutedEscaped); });
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.DoesNotContain(ab.InactivityVerdicts, v => v.FormationId == 1);
    }

    /// <summary>P-V4(a). "Hidden" is CURRENT tracking, not "never known". Formation 1 (sight 40) stands on the edge of a 20 m
    /// plateau and sees formation 2 (sight 1, no objective: it idles) 30 cells away over a 6 m ridge; it attacks, descends into
    /// the valley, and the ridge cuts the line of sight: formation 2 is lost — hidden again — and, idle and not concealed from
    /// anyone within reach, it routs at the end of its N-th hidden pulse, before formation 1 regains sight on the ridge. Both
    /// resolvers: formation 2 (tracked in pulse 0, hidden from pulse 1) routs at the end of pulse N counted from 0 (side 1
    /// eliminated, N + 1 pulses) with the same verdicts. (The former auto port
    /// kept it "known" forever and never routed it.) Measured limitation: with N = 5 the resolvers differ — the tactical
    /// front rank regains sight before the auto's formation point does (H-P.md verification round 1).</summary>
    [Theory]
    [InlineData(3, 4L)]
    [InlineData(4, 5L)]
    public void AFormationTheEnemyLostIsHiddenAgainAndRoutsInBothResolvers(int n, long pulses)
    {
        var g = OpenField();
        for (int y = 0; y < FieldH; y++)
        {
            for (int x = 0; x <= 12; x++) g.Set(x, y, TerrainClass.Open, 20_000);
            for (int x = 30; x <= 31; x++) g.Set(x, y, TerrainClass.Open, 6_000);
        }
        var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(null, null), new[]
        {
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 40), 10, Cell(12, 16)),
            new FormationSpec(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 1), 10, Cell(42, 16)),
        }, 7) { Rules = new TacticalRules { InactivityPulses = n } };
        var tb = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
        var t = Tactical(tb);
        var ab = new AutoBattle(s, V2);
        var a = Bounded(ab.Run);
        Assert.Equal(0, ab.FirstKnownStep(0, 2));                                   // seen from the plateau at once
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(1, r.Side);
            Assert.Equal(pulses, r.Pulses);
            var f2 = r.Formations.Single(f => f.FormationId == 2);
            Assert.Equal(0, f2.Killed);
            Assert.Equal(10, f2.RoutedEscaped);
        }
        Assert.Equal(tb.InactivityVerdicts, ab.InactivityVerdicts);
        Assert.Equal(new[] { new InactivityVerdict(n, 2, 1, n, InactivityExemption.None, true) }, ab.InactivityVerdicts);   // tracked in pulse 0
    }

    // ------------------------------------------------------------------ P-V3: charge impetus wiring

    /// <summary>P-V3 / mutant V2. Model 2 applies the lane-TR charge impetus 1 + chi x min(1, advance / reference) of THIS step's
    /// advance: the shock infantry (chi 1/2, TEST INPUT) closes the last 3,900 mm at catch-up pace into contact on flat Open
    /// ground (every other factor One), so its first melee attack factor is exactly 3/2; standing in contact afterwards it is
    /// One. Plain HI (chi 0) never gets an impetus factor.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void ChargeImpetusEntersTheModelTwoMeleeAttackFactorExactly(ulong seed)
    {
        var (b, _) = Logged(ParityBatteryP.Build(PScenario.ShockInfantry, seed));
        var melee = b.TerrainTerms.Where(t => t.AttackerId == 1 && t.Mode == 1).ToList();
        Assert.NotEmpty(melee);
        var rules = new TacticalRules();
        long refMm = FixedMath.MulDivFloor(rules.RefFootSpeedMmPerS * rules.CatchUpNum, rules.StepMs, 1000 * rules.CatchUpDen);
        var rows = b.Log.Where(r => r.FormationId == 1).ToList();
        foreach (var t in melee)
        {
            long now = rows.Single(r => r.Step == t.Step).PosMm, before = t.Step == 0 ? now : rows.Single(r => r.Step == t.Step - 1).PosMm;
            long advance = Math.Abs(now - before);
            Assert.Equal(TerrainCombat.ImpetusFactor(ParityBatteryP.ShockInfantry.Impetus, advance, refMm), t.AttackFactor);
        }
        Assert.Equal(Q32.FromRatio(3, 2), melee[0].AttackFactor);
        Assert.Contains(melee, t => t.AttackFactor == Q32.One);
        var (h, _) = Logged(ParityBatteryP.Build(PScenario.EqualInfantry, seed));
        Assert.All(h.TerrainTerms.Where(t => t.Mode == 1), t => Assert.Equal(Q32.One, t.AttackFactor));
    }

    // ------------------------------------------------------------------ P-V5: detection-draw wiring, rule P1's reference spread

    /// <summary>P-V5(2). Every model-2 detection draw aggregates the per-entity chance over the target's LIVING entities:
    /// Chance = 1 - (1 - PerEntity)^n with n = the target's alive count at the draw (the previous step's end state). Checked on
    /// every draw of the lane-P subset scenarios with concealment.</summary>
    [Theory]
    [InlineData(PScenario.JungleAmbush, 1UL)]
    [InlineData(PScenario.ArchersVsInfantryForest, 2UL)]
    [InlineData(PScenario.SniperScreen, 3UL)]
    [InlineData(PScenario.ForestAmbushArchers, 4UL)]
    public void EveryDetectionDrawAggregatesOverTheLivingEntities(PScenario id, ulong seed)
    {
        var (b, _) = Logged(ParityBatteryP.Build(id, seed));
        Assert.NotEmpty(b.DetectionDraws);
        foreach (var d in b.DetectionDraws)
        {
            long alive = d.Step == 0
                ? ParityBatteryP.Build(id, seed).Formations.Single(f => f.Id == d.TargetId).Count
                : b.Log.Single(r => r.FormationId == d.TargetId && r.Step == d.Step - 1).Alive;
            Assert.Equal(alive, d.Entities);
            Assert.Equal(AutoBattle.AnyOf(d.PerEntity, d.Entities), d.Chance);
        }
    }

    /// <summary>P-V5(2) / mutant V5 — teeth. Since round 1 a formation the enemy lost can be RE-detected, so a draw can meet a
    /// depleted target: HI 20 (morale 400, doctrine withdrawal off — TEST INPUTS) attacks an HI 50 line holding (Defend) in
    /// forest, inflicts losses and routs; side 0's second formation (HI 50 with a 2-cell sight, TEST INPUT) arrives later and
    /// must re-detect the now untracked line through draws (MEASURED: 6-14 such draws per seed, the line at 29-31 alive). Every
    /// draw aggregates over the LIVING entities; a draw over the start strength (50) would give a different chance.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void ReacquisitionAfterCasualtiesAggregatesOverTheSurvivors(ulong seed)
    {
        var g = OpenField(); TT.Fill(g, 32, 10, 35, 22, TerrainClass.Forest);
        var s = TT.Setup(g, DefaultSides(new[] { Cell(33, 16) }, null), seed, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(20, 16)) { Morale = 400 },
            new FormationSpec(3, 0, TT.WithSight(UnitProfiles.HeavyInfantry, 2), 50, Cell(1, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 50, Cell(33, 16)) { Posture = Posture.Defend });
        var (b, _) = Logged(s, V2 with { DoctrineRetreatMorale = 0 });
        var draws = b.DetectionDraws.Where(d => d.TargetId == 2).ToList();
        foreach (var d in draws)
        {
            long alive = d.Step == 0 ? 50 : b.Log.Single(r => r.FormationId == 2 && r.Step == d.Step - 1).Alive;
            Assert.Equal(alive, d.Entities);
            Assert.Equal(AutoBattle.AnyOf(d.PerEntity, d.Entities), d.Chance);
        }
        var depleted = draws.Where(d => d.Entities < 50 && d.Chance < Q32.One).ToList();
        Assert.NotEmpty(depleted);                                                   // teeth: the case exists
        Assert.All(depleted, d => Assert.NotEqual(AutoBattle.AnyOf(d.PerEntity, 50), d.Chance));
    }

    private static POutcome[] Alternating(int n, long ca0, long ca1, long cb0, long cb1, long d0, long d1)
    {
        var o = new POutcome[n];
        for (int i = 0; i < n; i++)
            o[i] = i % 2 == 0 ? new POutcome(0, ca0, cb0, 3, d0, BattleEndReason.SideEliminated) : new POutcome(1, ca1, cb1, 3, d1, BattleEndReason.SideEliminated);
        return o;
    }

    /// <summary>P-V5(1) / mutant V4. Rule P1's sampling terms use the REFERENCE's spread only. Reference (n = 160): casualties A
    /// 100 / 300 ‰, B 150 / 350 ‰, duration 100 / 300 s alternating (s = 100.3 ‰ and 100.3 s); candidate: the same means with
    /// twice the spread (0 / 400, 50 / 450, 0 / 400 s). Hand values: floor = ceil(3.6 x s_R x sqrt(2/160)) = ceil(40.38) = 41 ‰
    /// (both casualty bands; above the 30 ‰ substantive band and the 20 ‰ entity floor) and 40,376 ms for the duration (above
    /// 15 % of 200 s = 30,000 ms); the candidate's own spread would give 81 ‰ and 80,752 ms.</summary>
    [Fact]
    public void RuleP1SamplingBandsUseTheReferenceSpreadOnly()
    {
        var r = PStats.Of(Alternating(160, 100, 300, 150, 350, 100_000, 300_000));
        var c = PStats.Of(Alternating(160, 0, 400, 50, 450, 0, 400_000));
        var ck = ParityCriteriaP1.Check(r, c, 50, 50, 2_000);
        Assert.Equal(41, ck.CasABand);
        Assert.Equal(41, ck.CasBBand);
        Assert.Equal(40_376, ck.DurBand);
        Assert.Equal(81, ParityCriteriaP1.MeanSamplingFloor(c.CasASum, c.CasASumSq, c.N, r.N));
        Assert.Equal(80_752, ParityCriteriaP1.MeanSamplingFloor(c.DurSum, c.DurSumSq, c.N, r.N));
        Assert.True(ck.CasAOk && ck.CasBOk && ck.DurOk);                            // equal means pass
        // the reference's band does not widen when the CANDIDATE is noisier: swap the roles and the band follows the new reference
        var swapped = ParityCriteriaP1.Check(c, r, 50, 50, 2_000);
        Assert.Equal(81, swapped.CasABand);
        Assert.Equal(81, swapped.CasBBand);
        Assert.Equal(80_752, swapped.DurBand);
    }
}
