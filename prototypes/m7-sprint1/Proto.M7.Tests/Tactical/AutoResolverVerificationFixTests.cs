using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// Slice E verification round (verify-E findings E-V1, E-V3, E-V4 and surviving mutants V1, V2, V3, V4, V6). Every test states
/// a rule of the model and, where the rule is shared, checks it in BOTH resolvers. All setups and profile variants are TEST
/// INPUTS.
/// [P] Lane P: pins the Sprint 1 model explicitly (<see cref="AutoCoefficients.Sprint1"/>). [P fix r1, P-V2] Correction: the
/// earlier claim that "the lane-P model's versions of these rules are in AutoResolverPTests" was false for most of them; the
/// model-2 versions are in AutoResolverPRuleTests: trapped-rout surrender, impassable withdrawal, stationary concealment
/// (which exposed P-V1), hold-morale recovery (both inactivity rules, both resolvers) and the withdrawal travel time. The
/// ambusher-springs and first-detection tests here are legacy-rule tests of model 1; under the setup's legacy rule model 2 now
/// applies the legacy rule too (P-V4(c)).
/// </summary>
public class AutoResolverVerificationFixTests
{
    /// <summary>HARNESS GUARD: the AutoResolver has no round cap (O-4); not finishing in 10 s is a FAILURE (non-termination),
    /// never a rule. A normal auto battle takes well under 10 ms.</summary>
    private static BattleResult Auto(BattleSetup s, AutoCoefficients? c = null)
        => Run(new AutoBattle(s, c ?? AutoCoefficients.Sprint1));

    private static BattleResult Run(AutoBattle b)
    {
        var t = Task.Run(b.Run);
        if (!t.Wait(TimeSpan.FromSeconds(10))) Assert.Fail("HARNESS GUARD: AutoResolver did not terminate within 10 s (NON-TERMINATION)");
        return t.Result;
    }

    private static BattleResult Tactical(BattleSetup s) => TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));

    private static BattleSetup Setup(TerrainGrid g, SideSpec[] sides, ulong seed, TacticalRules? rules, params FormationSpec[] forms)
        => TT.Setup(g, sides, seed, rules, forms);

    /// <summary>Withdrawal escapes = escaped survivors that are neither routed nor disengaged.</summary>
    private static long WithdrawnEscaped(FormationResult f) => f.Escaped - f.RoutedEscaped - f.DisengagedEscaped;

    // ------------------------------------------------------------------ E-V1 / E-V4: zero effective attack

    /// <summary>E-V1 (blocking). A VALID ranged profile (Ranged 1, inside band VL 1-15) at supply 1/2 has effective attack
    /// floor(1 * 3/4) = 0. Two of them inside each other's range used to fire every step for 0 damage, which counted as activity:
    /// the auto battle never ended. Rule: every attack consumes HP (the tactical floor: hit chance HitMin, damage max(1, 0) = 1),
    /// so the battle terminates.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void AutoTerminatesWhenEffectiveRangedAttackFloorsToZero(ulong seed)
    {
        var weak = UnitProfiles.Archers with { Name = "test/weak-ranged", Ranged = 1, Melee = 1, RangeCells = 4 };
        var half = Q32.FromRatio(1, 2);
        Assert.Equal(0, ConditionMath.Attack(1, 0, half));
        var s = Setup(OpenField(), DefaultSides(), seed, null,
            new FormationSpec(1, 0, weak, 10, Cell(20, 16)) { Supply = half },
            new FormationSpec(2, 1, weak, 10, Cell(23, 16)) { Supply = half });
        var a = Auto(s);
        TT.AssertConserved(a);
        Assert.NotEqual(BattleEndReason.None, a.Reason);
        Assert.True(a.Formations[0].DamageDealt > 0 && a.Formations[1].DamageDealt > 0, "zero-attack shots still land at the tactical floor");
        var t = Tactical(s);
        Assert.True(t.Formations[0].DamageDealt > 0 && t.Formations[1].DamageDealt > 0);
    }

    /// <summary>E-V4. Melee 1 at supply 0: effective attack 0. The tactical engine hits at HitMin for max(1, 0) = 1 HP; the auto
    /// model must use the same floor (it dealt nothing before). Exact first melee step with fortune and noise off:
    /// 10 strikers (min(MeleeEngage 1/2 x 20, frontage 1/2 x 20)) x 1 HP x 1000 milli x HitMin.</summary>
    [Fact]
    public void ZeroEffectiveMeleeStrikesAtTheTacticalFloorInBothResolvers()
    {
        var p = UnitProfiles.HeavyInfantry with { Name = "test/weak-melee", Melee = 1 };
        var s = Setup(OpenField(), DefaultSides(new[] { Cell(33, 16) }, new[] { Cell(14, 16) }), 3, null,
            new FormationSpec(1, 0, p, 20, Cell(14, 16)) { Supply = Q32.Zero },
            new FormationSpec(2, 1, p, 20, Cell(33, 16)) { Supply = Q32.Zero });
        Assert.Equal(0, ConditionMath.Attack(1, 0, Q32.Zero));
        var t = Tactical(s);
        var a = Auto(s);
        foreach (var r in new[] { t, a })
        {
            TT.AssertConserved(r);
            Assert.True(r.Formations[0].DamageDealt > 0 && r.Formations[1].DamageDealt > 0, $"{r.Resolver}: zero-attack melee must still wound");
            Assert.True(r.Formations[0].Killed + r.Formations[1].Killed > 0, $"{r.Resolver}: and eventually kill");
        }
        var b = new AutoBattle(s, AutoCoefficients.Sprint1 with { FortunePermille = 0, NoisePermille = 0 }) { RecordLog = true };
        Run(b);
        var first = b.Log.First(r => r.FormationId == 1 && r.Mode == 1);
        Assert.Equal(FixedMath.MulDivFloor(10 * 1 * 1000, new TacticalRules().HitMin.Raw, Q32.OneRaw), first.DamageDealtMilli);
    }

    // ------------------------------------------------------------------ E-V3: no path to the exit edge

    private static TerrainGrid WaterRing()
    {
        var g = OpenField();
        for (int y = 12; y <= 20; y++) for (int x = 10; x <= 18; x++)
            if (y == 12 || y == 20 || x == 10 || x == 18) g.Set(x, y, TerrainClass.Water);
        return g;
    }

    private static BattleSetup Island(TerrainGrid g, long morale) => Setup(g, DefaultSides(null, new[] { Cell(14, 16) }), 4, null,
        new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)) { Morale = morale },
        new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 16)));

    /// <summary>E-V3. Spec §4: a Routing entity with no passable path to its exit edge is Surrendered. A formation that routs on
    /// an island ringed by Water surrenders entirely in BOTH resolvers (the auto model let it escape intact before).</summary>
    [Fact]
    public void TrappedRoutSurrendersEntirelyInBothResolvers()
    {
        var s = Island(WaterRing(), 290);   // below RoutThreshold 300: routs at once
        foreach (var r in new[] { Tactical(s), Auto(s) })
        {
            TT.AssertConserved(r);
            var f = r.Formations[0];
            Assert.Equal(20, f.Surrendered);
            Assert.Equal(0, f.Escaped);
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(0, r.Side);
        }
        // The rout rule alone (doctrine withdrawal off, TEST INPUT): trapped -> all surrender; on open ground the routed
        // formation escapes except the fitted surrender share.
        var noDoctrine = AutoCoefficients.Sprint1 with { DoctrineRetreatMorale = 0 };
        Assert.Equal(20, Auto(s, noDoctrine).Formations[0].Surrendered);
        var open = Auto(Island(OpenField(), 290), noDoctrine).Formations[0];
        Assert.Equal(FixedMath.MulDivFloor(20, noDoctrine.SurrenderPermille, 1000), open.Surrendered);
        Assert.Equal(20 - open.Surrendered, open.RoutedEscaped);
    }

    /// <summary>E-V3. Doctrine withdrawal needs a path: a formation below the retreat morale on the island cannot withdraw to its
    /// edge (the auto model teleported it out along the straight line before). The tactical engine keeps it on the field too
    /// (it ends disengaged, never withdrawn). Control: on open ground the same formation withdraws intact.</summary>
    [Fact]
    public void TrappedFormationCannotWithdrawThroughImpassableTerrain()
    {
        var trapped = Island(WaterRing(), 400);   // 300 <= 400 < DoctrineRetreatMorale 450
        foreach (var r in new[] { Tactical(trapped), Auto(trapped) })
        {
            TT.AssertConserved(r);
            Assert.Equal(0, WithdrawnEscaped(r.Formations[0]));
        }
        Assert.Equal(20, WithdrawnEscaped(Auto(Island(OpenField(), 400)).Formations[0]));
    }

    // ------------------------------------------------------------------ V1: stationary concealment

    /// <summary>Mutant V1. Detection of a target that neither moved nor fought is clarity x StationaryConcealment (spec §4). An
    /// Ambush-posture infantry waiting in forest 5 cells from range-6 archers (no objectives): with a near-perfect stationary
    /// factor the archers never detect it and never shoot, in both resolvers; with factor 1 the same archers find and shoot it.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void StationaryTargetInConcealingTerrainGetsTheStationaryFactor(ulong seed)
    {
        var archers = UnitProfiles.Archers with { Name = "test/archers-range6", RangeCells = 6 };
        BattleSetup S(TacticalRules rules)
        {
            var g = OpenField(); TT.Fill(g, 18, 10, 24, 22, TerrainClass.Forest);
            return Setup(g, DefaultSides(), seed, rules,
                new FormationSpec(1, 0, archers, 60, Cell(14, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 50, Cell(19, 16)) { Posture = Posture.Ambush });
        }
        var hidden = S(TT.HideRules());
        var results = seed == 1 ? new[] { Auto(hidden), Tactical(hidden) } : new[] { Auto(hidden) };
        foreach (var r in results)
        {
            Assert.Equal(0, r.Formations[0].DamageDealt);
            Assert.Equal(0, r.Formations[1].Killed);
            Assert.NotEqual(BattleEndReason.SideEliminated, r.Reason);
        }
        var seen = Auto(S(new TacticalRules { StationaryConcealment = Q32.One }));
        Assert.True(seen.Formations[0].DamageDealt > 0, "without the stationary factor the archers find and shoot the infantry");
    }

    // ------------------------------------------------------------------ V2: the active-concealment clause

    /// <summary>Mutant V2. Spec §4 inactivity rule, test (i): a hidden ambusher with an enemy approaching stays (no
    /// termination) and springs. Infantry crosses a jungle toward its objective; the Ambush-posture defender hides in the
    /// jungle (near-perfect stationary concealment). With the active-concealment clause the ambusher stays and the battle is
    /// fought in BOTH resolvers; with the clause off, the hidden side disengages before the enemy arrives (both resolvers).</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void HiddenAmbusherFacingAnAdvancingEnemyStaysAndSprings(ulong seed)
    {
        BattleSetup S(bool clause)
        {
            var g = OpenField(); TT.Fill(g, 21, 0, 42, FieldH - 1, TerrainClass.Jungle);
            return Setup(g, DefaultSides(new[] { Cell(46, 16) }), seed, TT.HideRules(clause),
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, Cell(22, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 50, Cell(35, 16)) { Posture = Posture.Ambush });
        }
        var on = seed == 1 ? new[] { Auto(S(true)), Tactical(S(true)) } : new[] { Auto(S(true)) };
        foreach (var r in on)
        {
            Assert.Equal(0, r.Formations[1].DisengagedEscaped);
            Assert.True(r.Formations[0].Killed > 0 && r.Formations[1].Killed > 0, $"{r.Resolver}: the ambush is sprung and fought");
        }
        var off = seed == 1 ? new[] { Auto(S(false)), Tactical(S(false)) } : new[] { Auto(S(false)) };
        foreach (var r in off)
        {
            Assert.Equal(BattleEndReason.SideDisengaged, r.Reason);
            Assert.Equal(1, r.Side);
            Assert.Equal(50, r.Formations[1].DisengagedEscaped);
            Assert.Equal(0, r.Formations[0].Killed + r.Formations[1].Killed);
        }
    }

    // ------------------------------------------------------------------ V3: Hold morale recovery

    /// <summary>Mutant V3. A formation that holds without casualties recovers HoldRecovery morale per pulse, capped at MoraleMax.
    /// Mutual standoff beyond sight, no objectives: five idle pulses, then both sides disengage. Exact end morale in BOTH
    /// resolvers: 800 + 5 x 20 = 900, and 990 capped at 1000.</summary>
    [Fact]
    public void IdleFormationRecoversHoldMoraleEachPulseInBothResolvers()
    {
        var rules = new TacticalRules();
        var s = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 9);
        s = s with { Sides = DefaultSides(), Formations = new[] { s.Formations[0] with { Morale = 800 }, s.Formations[1] with { Morale = 990 } } };
        s = s with { Rules = TT.Legacy };   // [TR] parity on the legacy inactivity rule (the AutoResolver implements only that one)
        foreach (var r in new[] { Tactical(s), Auto(s) })
        {
            Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
            Assert.Equal(rules.InactivityPulses, r.Pulses);
            Assert.Equal(800 + rules.InactivityPulses * rules.HoldRecovery, r.Formations[0].MoraleEnd);
            Assert.Equal(rules.MoraleMax, r.Formations[1].MoraleEnd);
        }
    }

    // ------------------------------------------------------------------ V4: withdrawal travel time

    /// <summary>Mutant V4. A doctrine withdrawal leaves the field only after travelling to its exit edge at catch-up speed:
    /// steps = ceil(distance to the edge line / (speed x CatchUp x StepMs / cost)). Hand values (open ground, HI speed 1300 mm/s,
    /// catch-up 3/2, step 2 s, cost 1000): 3900 mm per step; from column 14 (290,000 mm) 75 steps, from column 19 (390,000 mm)
    /// 100 steps. The battle ends at the step the last withdrawing formation escapes: EndMs = (steps - 1) x StepMs.</summary>
    [Theory]
    [InlineData(14L, 290_000L, 75L)]
    [InlineData(19L, 390_000L, 100L)]
    public void DoctrineWithdrawalTakesTheTravelTimeToTheExitEdge(long column, long distanceMm, long steps)
    {
        var rules = new TacticalRules();
        var s = ParityScenarios.Build(ParityScenarioId.LowMorale, 1, false);
        s = s with { Formations = new[] { s.Formations[0] with { Anchor = Cell(column, 16) }, s.Formations[1] } };
        Assert.Equal(distanceMm, new EdgeField(s.Grid, s.Costs, s.Sides[0].Exit).LineDistanceMm(Cell(column, 16)));
        long perStep = FixedMath.MulDivFloor(rules.RefFootSpeedMmPerS * rules.CatchUpNum, rules.StepMs, 1000 * rules.CatchUpDen);
        Assert.Equal(3_900, perStep);
        Assert.Equal(steps, FixedMath.DivCeil(distanceMm, perStep));
        var r = Auto(s, AutoCoefficients.Sprint1 with { DoctrineRetreatMorale = 600 });
        Assert.Equal(50, WithdrawnEscaped(r.Formations[0]));
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal((steps - 1) * rules.StepMs, r.EndMs);
    }

    // ------------------------------------------------------------------ V6: first detection is activity

    /// <summary>Mutant V6. Spec §4: a NEW first detection of an enemy is primary activity. Two Ambush-posture formations in the
    /// open, 8 cells apart (inside sight 12, beyond the 3-cell trigger), no objectives: they detect each other in pulse 0 and
    /// then wait, so the five idle pulses run from pulse 1 and the battle ends after 6 pulses — in BOTH resolvers. Control: at
    /// 13 cells (beyond sight) there is no detection and it ends after 5.</summary>
    [Theory]
    [InlineData(8L, 6L)]
    [InlineData(13L, 5L)]
    public void FirstDetectionIsActivityInBothResolvers(long gapCells, long pulses)
    {
        var s = Setup(OpenField(), DefaultSides(), 2, TT.Legacy,   // [TR] parity on the legacy inactivity rule
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(20, 16)) { Posture = Posture.Ambush },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(20 + gapCells, 16)) { Posture = Posture.Ambush });
        foreach (var r in new[] { Tactical(s), Auto(s) })
        {
            Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
            Assert.Equal(pulses, r.Pulses);
            foreach (var f in r.Formations) Assert.Equal(f.Start, f.DisengagedEscaped);
        }
    }
}
