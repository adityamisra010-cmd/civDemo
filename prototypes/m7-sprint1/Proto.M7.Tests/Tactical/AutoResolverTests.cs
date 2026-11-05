using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>Slice E: the AutoResolver (spec §5) and the shared condition equations (ConditionMath).</summary>
public class AutoResolverTests
{
    private static BattleResult Auto(BattleSetup s, AutoCoefficients? c = null, bool log = false)
        => RunBounded(() => new AutoBattle(s, c ?? new AutoCoefficients()) { RecordLog = log }.Run());

    private static AutoBattle AutoLogged(BattleSetup s, AutoCoefficients? c = null)
    {
        var b = new AutoBattle(s, c ?? new AutoCoefficients()) { RecordLog = true };
        RunBounded(b.Run);
        return b;
    }

    /// <summary>HARNESS GUARD: the AutoResolver has no round cap (O-4); a run that does not finish in 30 s is a FAILURE
    /// (non-termination), never a rule.</summary>
    private static T RunBounded<T>(Func<T> f)
    {
        var t = Task.Run(f);
        if (!t.Wait(TimeSpan.FromSeconds(30))) Assert.Fail("HARNESS GUARD: AutoResolver did not terminate within 30 s (non-termination is a FAILURE)");
        return t.Result;
    }

    // ------------------------------------------------------------------ ConditionMath (shared by both resolvers)

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(25L)]
    [InlineData(45L)]
    [InlineData(75L)]
    [InlineData(1_000_000L)]
    public void ConditionMathIsTheExactIdentityAtTheDefaults(long stat)
    {
        Assert.Equal(stat, ConditionMath.Attack(stat, 0, Q32.One));
        Assert.Equal(stat, ConditionMath.Defence(stat, 0));
        for (long k = 0; k <= 50; k++)
            Assert.Equal(FixedMath.MulDivFloor(k, 2000, 50), ConditionMath.MoraleLoss(k, 2000, 50, 0));   // the Slice D rule exactly
    }

    [Fact]
    public void ConditionMathHandComputedValues()
    {
        Assert.Equal(75, ConditionMath.Attack(50, 50, Q32.One));                 // 50 * 150/100
        Assert.Equal(67, ConditionMath.Defence(45, 50));                         // floor(45 * 1.5)
        Assert.Equal(Q32.FromRatio(1, 2), ConditionMath.SupplyFactor(Q32.Zero));  // floor 1/2
        Assert.Equal(Q32.One, ConditionMath.SupplyFactor(Q32.One));
        Assert.Equal(31, ConditionMath.Attack(50, 0, Q32.FromRatio(1, 4)));       // floor(50 * 5/8)
        Assert.Equal(480, ConditionMath.MoraleLoss(18, 2000, 50, 50));            // floor(18*2000*100 / (50*150))
        Assert.Equal(720, ConditionMath.MoraleLoss(18, 2000, 50, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionMath.Validate(101, 0, Q32.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionMath.Validate(0, -1, Q32.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionMath.Validate(0, 0, new Q32(Q32.OneRaw + 1)));
    }

    [Fact]
    public void BothResolversRefuseOutOfRangeConditions()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 1);
        var bad = s with { Formations = new[] { s.Formations[0] with { Experience = 101 }, s.Formations[1] } };
        Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalBattle(bad, new KnowledgeAi(), new KnowledgeAi()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutoBattle(bad, new AutoCoefficients()));
    }

    // ------------------------------------------------------------------ the tactical engine consumes the conditions

    [Fact]
    public void TacticalLowSupplyFormationDealsLessDamagePerAttack()
    {
        // Same scenario, same seed: role A at supply 1/4 vs full supply. Damage per attack must drop (attack x 5/8 enters both
        // the hit chance and the damage per hit).
        long full = 0, fullAtt = 0, low = 0, lowAtt = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var lowSetup = ParityScenarios.Build(ParityScenarioId.LowSupply, seed, false);
            var fullSetup = lowSetup with { Formations = new[] { lowSetup.Formations[0] with { Supply = Q32.One }, lowSetup.Formations[1] } };
            var rl = TT.RunGuarded(new TacticalBattle(lowSetup, new KnowledgeAi(), new KnowledgeAi()));
            var rf = TT.RunGuarded(new TacticalBattle(fullSetup, new KnowledgeAi(), new KnowledgeAi()));
            low += rl.Formations[0].DamageDealt; lowAtt += rl.Formations[0].AttacksMade;
            full += rf.Formations[0].DamageDealt; fullAtt += rf.Formations[0].AttacksMade;
        }
        Assert.True(lowAtt > 0 && fullAtt > 0);
        Assert.True(low * fullAtt < full * lowAtt, $"damage/attack low {low}/{lowAtt} must be below full {full}/{fullAtt}");
    }

    [Fact]
    public void TacticalLeadershipReducesMoraleLossPerCasualty()
    {
        // Leadership 50: loss per death 2000*100/(50*150) = 26.67 instead of 40. The formation keeps fighting while moving (no
        // Hold recovery), so its end morale is bounded below by 1000 - ceil(26.67 per death) and is above the no-leadership
        // bound 1000 - 40 * Killed whenever it lost anyone.
        int checkedCases = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var r = TT.RunGuarded(new TacticalBattle(ParityScenarios.Build(ParityScenarioId.LeadershipEdge, seed, false), new KnowledgeAi(), new KnowledgeAi()));
            var a = r.Formations[0];
            if (a.Killed < 3 || a.RoutedEscaped > 0) continue;
            checkedCases++;
            Assert.True(a.MoraleEnd > 1000 - 40 * a.Killed, $"seed {seed}: morale {a.MoraleEnd} after {a.Killed} deaths");
            Assert.True(a.MoraleEnd >= 1000 - FixedMath.MulDivFloor(a.Killed, 2000 * 100, 50 * 150), $"seed {seed}");   // sum of floors <= floor of sum
        }
        Assert.True(checkedCases >= 2, "need at least two non-routed leadership formations with casualties");
    }

    [Fact]
    public void TacticalExperienceEdgeWinsAndEqualFormationsDoNot()
    {
        int expWins = 0, eqWinsA = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            if (TT.RunGuarded(new TacticalBattle(ParityScenarios.Build(ParityScenarioId.ExperienceEdge, seed, false), new KnowledgeAi(), new KnowledgeAi())).HoldsField == 0) expWins++;
            if (TT.RunGuarded(new TacticalBattle(ParityScenarios.Build(ParityScenarioId.EqualInfantry, seed, false), new KnowledgeAi(), new KnowledgeAi())).HoldsField == 0) eqWinsA++;
        }
        Assert.Equal(4, expWins);
        Assert.True(eqWinsA < 4);
    }

    // ------------------------------------------------------------------ AutoResolver contract

    [Fact]
    public void AutoConservesExactlyAcrossTheBattery()
    {
        foreach (var id in ParityBattery.AllScenarioIds())
            for (ulong seed = 1; seed <= 4; seed++)
                foreach (bool m in new[] { false, true })
                {
                    var r = Auto(ParityScenarios.Build(id, seed, m));
                    Assert.Equal(ResolverKind.Auto, r.Resolver);
                    TT.AssertConserved(r);
                    Assert.True(r.Pulses >= 1 && r.Reason != BattleEndReason.None);
                    foreach (var f in r.Formations) Assert.True(f.Killed >= 0 && f.Surrendered >= 0 && f.Survivors >= 0);
                }
    }

    [Fact]
    public void AutoIsDeterministicAndSeedSensitive()
    {
        var a = Auto(ParityScenarios.Build(ParityScenarioId.EqualInfantry, 7, false));
        var b = Auto(ParityScenarios.Build(ParityScenarioId.EqualInfantry, 7, false));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(a.ResultHash(), b.ResultHash());
        var traces = new HashSet<string>();
        for (ulong s = 1; s <= 6; s++) traces.Add(Auto(ParityScenarios.Build(ParityScenarioId.EqualInfantry, s, false)).TraceHash);
        Assert.True(traces.Count >= 5, "different seeds must give different auto battles");
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void AutoIgnoresFormationInputOrder(ulong perm)
    {
        var s = ParityScenarios.Build(ParityScenarioId.CombinedArms, 11, false);
        var p = TestSupport.Permutation(s.Formations.Length, perm);
        var shuffled = s with { Formations = p.Select(i => s.Formations[i]).ToArray() };
        var a = Auto(s); var b = Auto(shuffled);
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Equal(a.ResultHash(), b.ResultHash());
    }

    [Fact]
    public void AutoGivesTheSameOutcomeInBothOrientations()
    {
        // The auto model is formation-level on a projected axis: mirroring the field must not change any per-role number.
        foreach (var id in ParityBattery.AllScenarioIds())
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var a = ParityOutcome.Of(Auto(ParityScenarios.Build(id, seed, false)));
                var m = ParityOutcome.Of(Auto(ParityScenarios.Build(id, seed, true)));
                Assert.Equal(a, m);
            }
    }

    [Fact]
    public void AutoIsSelectedOnlyExplicitlyAndRefusesNaval()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 3);
        var auto = new AutoResolver();
        Assert.Equal(ResolverKind.Auto, BattleResolution.Resolve(s, new KnowledgeAi(), new KnowledgeAi(), ResolverKind.Auto, auto).Resolver);
        Assert.Equal(ResolverKind.Tactical, BattleResolution.Resolve(s, new KnowledgeAi(), new KnowledgeAi()).Resolver);
        var naval = s with { Formations = new[] { s.Formations[0] with { Domain = Domain.Naval }, s.Formations[1] } };
        Assert.Throws<NotSupportedException>(() => auto.Resolve(naval, PassiveController.Instance, PassiveController.Instance));
    }

    [Fact]
    public void CoefficientValidationKeepsEveryContactStepDamaging()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutoResolver(new AutoCoefficients { FortunePermille = 500, NoisePermille = 401 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutoResolver(new AutoCoefficients { MeleeEngagePermille = 0 }));
        _ = new AutoResolver(new AutoCoefficients { FortunePermille = 500, NoisePermille = 400 });
    }

    /// <summary>Tie-dense target choice: two enemy formations at the SAME axis gap; the lower formation id is attacked,
    /// whatever the input order and whichever row holds which id.</summary>
    [Theory]
    [InlineData(3L, 5L, false)]
    [InlineData(5L, 3L, false)]
    [InlineData(3L, 5L, true)]
    [InlineData(5L, 3L, true)]
    public void AutoTargetTieBreaksOnTheLowerFormationId(long northId, long southId, bool reverseInput)
    {
        var forms = new List<FormationSpec>
        {
            new(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)),
            new(northId, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 10)),
            new(southId, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 22)),
        };
        if (reverseInput) forms.Reverse();
        var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { Cell(33, 16) }, new[] { Cell(14, 16) }), forms.ToArray(), 5);
        var b = AutoLogged(s);
        var firstAttack = b.Log.First(r => r.FormationId == 1 && r.Target >= 0);
        Assert.Equal(3, firstAttack.Target);
    }

    [Fact]
    public void AmbushSurpriseWindowSilencesTheVictimForExactlyTheFittedSteps()
    {
        const long window = 5;   // TEST INPUT
        var b = AutoLogged(ParityScenarios.Build(ParityScenarioId.Ambush, 3, false), new AutoCoefficients { AmbushFirstStrikeSteps = window });
        long first = b.Log.First(r => r.Target >= 0).Step;
        for (long k = first; k < first + window; k++)
        {
            var victim = b.Log.Single(r => r.Step == k && r.FormationId == 1);
            var ambusher = b.Log.Single(r => r.Step == k && r.FormationId == 2);
            Assert.Equal(0, victim.Mode);                 // surprised: no blow
            Assert.Equal(1, ambusher.Mode);               // the ambusher strikes
        }
        // ...and the victim strikes back at exactly the first step after the window (it is still fighting then).
        Assert.Equal(1, b.Log.Single(r => r.Step == first + window && r.FormationId == 1).Mode);
    }

    [Fact]
    public void KillPhaseDesynchronisesOtherwiseSymmetricRouts()
    {
        // Fortune and noise off, equal formations on open ground (detection p = 1, so no draw differs between the sides):
        // the only asymmetry left is the per-formation casualty-staircase phase. Without it every battle is a simultaneous
        // rout (BothEliminated); with it 5 of these 40 are decided (measured 2026-11-04; expectation ~ E|phase gap| / deaths per
        // step = (1/3)/1.58 ~ 21 %). The phase is a weak de-synchroniser by design (it must stay unbiased).
        var c = new AutoCoefficients { FortunePermille = 0, NoisePermille = 0 };
        int decided = 0;
        for (ulong seed = 1; seed <= 40; seed++)
            if (Auto(ParityScenarios.Build(ParityScenarioId.EqualInfantry, seed, false), c).Reason == BattleEndReason.SideEliminated) decided++;
        Assert.True(decided >= 3, $"only {decided}/40 decided");
    }

    [Fact]
    public void FrontageLimitCapsTheEngagedShare()
    {
        // 50 v 25: with at most 1/2 attacker per alive target, A's first melee step can bring only floor(25/2) = 12 strikers;
        // with no frontage limit it brings MeleeEngage x 50 = 25. The dealt damage of that step scales accordingly.
        long Dealt(long maxPerTarget)
        {
            var b = AutoLogged(ParityScenarios.Build(ParityScenarioId.Numbers2To1, 2, false),
                new AutoCoefficients { MaxAttackersPerTargetPermille = maxPerTarget, FortunePermille = 0, NoisePermille = 0 });
            var first = b.Log.First(r => r.FormationId == 1 && r.Mode == 1);
            long before = b.Log.Single(r => r.FormationId == 1 && r.Step == first.Step - 1).DamageDealtMilli;
            return first.DamageDealtMilli - before;
        }
        long capped = Dealt(500), open = Dealt(1_000_000);
        // strikers x damage 10 HP x 1000 milli x hit(50 vs 45) — the Slice D hit and damage formulas, floor of the Q32 product
        long Expected(long strikers) => FixedMath.MulDivFloor(strikers * 10_000, Q32.FromRatio(50, 95).Raw, Q32.OneRaw);
        Assert.Equal(Expected(12), capped);
        Assert.Equal(Expected(25), open);
    }

    [Fact]
    public void DoctrineWithdrawalBelowTheRetreatMoraleEscapesWithoutLosses()
    {
        var r = Auto(ParityScenarios.Build(ParityScenarioId.LowMorale, 1, false), new AutoCoefficients { DoctrineRetreatMorale = 600 });
        var a = r.Formations[0];
        Assert.Equal(0, a.Killed);
        Assert.Equal(50, a.Escaped);
        Assert.Equal(0, a.RoutedEscaped);
        Assert.Equal(1, r.HoldsField);
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side);
    }

    /// <summary>The rout rule is strict (morale &lt; RoutThreshold), as in the tactical engine (RoutThresholdIsStrict).</summary>
    [Theory]
    [InlineData(0L, false)]
    [InlineData(-1L, true)]
    public void AutoRoutThresholdIsStrict(long offset, bool routsAtOnce)
    {
        var rules = new TacticalRules();
        var s = ParityScenarios.Build(ParityScenarioId.EqualInfantry, 1, false);
        s = s with { Formations = new[] { s.Formations[0] with { Morale = rules.RoutThreshold + offset }, s.Formations[1] } };
        var r = Auto(s, new AutoCoefficients { DoctrineRetreatMorale = 0 });   // TEST INPUT: no withdrawal, rout rule only
        var a = r.Formations[0];
        if (routsAtOnce)
        {
            Assert.Equal(0, r.EndMs);                      // routed at the first step, before any contact
            Assert.Equal(0, a.Killed);
            Assert.Equal(a.Start - a.Surrendered, a.RoutedEscaped);   // the fitted surrender share of a routed formation
            Assert.Equal(1, r.HoldsField);
        }
        else
        {
            Assert.True(r.EndMs > 0);
            Assert.True(a.Killed > 0, "a formation exactly at the threshold fights");
        }
    }

    [Fact]
    public void RoutedLoserIsRecordedAsRoutedEscapeAndSurrenderFraction()
    {
        var r = Auto(ParityScenarios.Build(ParityScenarioId.ArchersVsInfantryOpen, 2, false), new AutoCoefficients { SurrenderPermille = 100 });
        var hi = r.Formations[1];
        Assert.Equal(0, r.HoldsField);
        Assert.True(hi.RoutedEscaped > 0);
        Assert.Equal(FixedMath.MulDivFloor(hi.Start - hi.Killed, 100, 1000), hi.Surrendered);
        Assert.True(hi.MoraleEnd < new TacticalRules().RoutThreshold);
    }

    [Fact]
    public void MutualStandoffDisengagesAfterExactlyFivePulsesInBothResolvers()
    {
        // Neither side has an objective and they start beyond sight: no activity => the five-pulse rule ends both resolvers'
        // battles at the same pulse with everybody preserved as disengaged survivors.
        var s = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 9);
        s = s with { Sides = DefaultSides() };
        var t = TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));
        var a = Auto(s);
        foreach (var r in new[] { t, a })
        {
            Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
            Assert.Equal(5, r.Pulses);
            Assert.Equal(5 * new TacticalRules().PulseMs, r.EndMs);
            foreach (var f in r.Formations) { Assert.Equal(f.Start, f.DisengagedEscaped); Assert.Equal(0, f.Killed); }
        }
    }

    [Fact]
    public void AutoLowSupplyAndExperienceMoveTheOutcomeTheRightWay()
    {
        int supplyLosses = 0, expWins = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            if (Auto(ParityScenarios.Build(ParityScenarioId.LowSupply, seed, false)).HoldsField == 1) supplyLosses++;
            if (Auto(ParityScenarios.Build(ParityScenarioId.ExperienceEdge, seed, false)).HoldsField == 0) expWins++;
        }
        Assert.Equal(6, supplyLosses);
        Assert.Equal(6, expWins);
    }
}
