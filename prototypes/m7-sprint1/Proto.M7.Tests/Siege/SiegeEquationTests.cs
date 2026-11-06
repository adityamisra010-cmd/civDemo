using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>Lane S: the per-pulse siege equations in isolation (C15 tests: monotonicity, rationing trade-off, gunpowder transition,
/// stability for long pulses, convergence as the pulse shrinks).</summary>
public class SiegeEquationTests
{
    private static readonly SiegeCoefficients K = SiegeCoefficients.Default;
    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private static Q32 P(long permille) => Q.Permille(permille);

    // ------------------------------------------------------------------ rationing trade-off (DIR5 §2.2)

    /// <summary>The equation-level half of the rationing trade-off: defence capability falls strictly with the ration, and the
    /// closed-form endurance of a store (<see cref="SiegeEquations.EnduranceMs"/>, a helper the world does not call) grows strictly.
    /// The world-level endurance is SiegeFixR1Tests.Rationing_TheGranaryLastsStrictlyLonger_InARunningSiege (fix r1, S-V10: renamed
    /// from "...AndExtendsEnduranceStrictly", which claimed more than this test shows).</summary>
    [Fact]
    public void Rationing_LowersDefenceStrictly_AndTheClosedFormEnduranceGrowsStrictly()
    {
        var d = new DefenderForce(2_000, 0, 100, 1_000, 1_000 * 1_000, 1_000 * 300, 0, 0, 0, 0);
        long prevCap = long.MaxValue, prevEndurance = -1;
        // At the nutrition equilibrium ν = ration (body reserves settled on the ration), from full ration down to 150‰.
        for (long r = 1000; r >= 150; r -= 50)
        {
            long cap = SiegeEquations.Capability(K, d, Q32.One, Q32.One, Q32.Zero, P(r), Q32.One);
            long endurance = SiegeEquations.EnduranceMs(1_000_000, 9_000L * 1_300, P(r));
            Assert.True(cap < prevCap, $"defence must fall strictly with the ration (r={r})");
            Assert.True(endurance > prevEndurance, $"endurance must grow strictly as the ration falls (r={r})");
            prevCap = cap; prevEndurance = endurance;
        }
        // "to some extent": half rations keep three quarters of the capability (a_n = 0.5), never all of it and never none.
        long full = SiegeEquations.Capability(K, d, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One);
        long half = SiegeEquations.Capability(K, d, Q32.One, Q32.One, Q32.Zero, P(500), Q32.One);
        Assert.Equal(full * 3 / 4, half);
    }

    [Fact]
    public void Nutrition_RelaxesTowardTheRation_WithoutOvershoot_ForAnyPulseLength()
    {
        foreach (long dt in new[] { 60_000L, Day, 21 * Day, 360 * Day })
        {
            var n = SiegeEquations.NutritionStep(K, Q32.One, P(400), dt);
            Assert.True(n.Raw < Q32.OneRaw && n.Raw > P(400).Raw, $"dt={dt}: strictly between the old state and the ration");
        }
    }

    // ------------------------------------------------------------------ attack monotonicity (C15)

    [Fact]
    public void StrongerAttack_WallsFallNoSlower_AndHealthFallsNoSlower()
    {
        var prevWall = Q32.One;
        for (long n = 0; n <= 40; n += 5)
        {
            var dmg = SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, new long[] { 0, 10, n, 0 }, false, Q32.One, Q32.One);
            var w = SiegeEquations.WallStep(K, FortKind.StoneWall, Q32.One, dmg, Q32.One, 7 * Day);
            Assert.True(w <= prevWall, $"{n} bombards: wall must fall no slower");
            prevWall = w;
        }
        // Fire: more incoming casualties per year → a larger loss share, never above 1.
        var prev = Q32.Zero;
        for (long rate = 0; rate <= 10_000_000; rate += 1_000_000)
        {
            var sh = SiegeEquations.FireShare(rate, P(3000), 3_000, 7 * Day);
            Assert.True(sh >= prev && sh.Raw < Q32.OneRaw);
            prev = sh;
        }
        // Health = M × clamp(D/D_ref): non-increasing as capability falls.
        var prevH = Q32.One;
        for (long cap = 1000; cap >= 0; cap -= 100)
        {
            var h = SiegeEquations.Health(P(800), cap, 1000);
            Assert.True(h <= prevH);
            prevH = h;
        }
    }

    [Fact]
    public void AssaultPower_RangedToMeleeRatio_IsSuppressiveSupport_ThatSaturates()
    {
        // 1,000 melee committed; ranged support grows the party's power until ranged = melee (cap ratio 1), then only the
        // ranged men's own (small) assault value is added.
        long Power(long ranged) => SiegeEquations.AssaultPower(K, new long[] { 1_000, ranged, 0, 0 }, RangedKind.Bow, new long[4], false, Q32.One, Q32.One, Q32.Zero, Q32.One);
        long p0 = Power(0), p500 = Power(500), p1000 = Power(1_000), p2000 = Power(2_000);
        Assert.True(p500 > p0 && p1000 > p500);
        Assert.Equal(2 * (1_000 * 1_000 + 1_000 * 400), p1000);                  // (1 + 1) × (melee + ranged at 400‰)
        Assert.Equal(2 * (1_000 * 1_000 + 2_000 * 400), p2000);                  // saturated: the ratio stays at the cap
        Assert.True(p2000 - p1000 < p1000 - p500, "saturation: the second 1,000 bowmen add less than the first 500");
    }

    // ------------------------------------------------------------------ gunpowder transition (state-derived)

    private static long DaysToBreach(FortKind fort, long[] engines, int maxDays = 3_000)
    {
        var w = Q32.One;
        var dmg = SiegeEquations.WallDamagePerYear(K, fort, engines, false, Q32.One, Q32.One);
        for (int day = 1; day <= maxDays; day++)
        {
            w = SiegeEquations.WallStep(K, fort, w, dmg, P(500), Day);
            if (w.Raw <= P(K.BreachPracticablePermille).Raw) return day;
        }
        return int.MaxValue;
    }

    [Fact]
    public void Gunpowder_BreachesMedievalWalls_FasterThanThrowers_ButBastionFortsResist()
    {
        long trebuchets = DaysToBreach(FortKind.StoneWall, new long[] { 0, 20, 0, 0 });
        long bombardsVsStone = DaysToBreach(FortKind.StoneWall, new long[] { 0, 0, 20, 0 });
        long bombardsVsCastle = DaysToBreach(FortKind.Castle, new long[] { 0, 0, 20, 0 });
        long bombardsVsBastion = DaysToBreach(FortKind.BastionFort, new long[] { 0, 0, 20, 0 });
        long modernVsBastion = DaysToBreach(FortKind.BastionFort, new long[] { 0, 0, 0, 20 });
        Assert.True(bombardsVsStone < trebuchets, $"20 bombards {bombardsVsStone} d vs 20 trebuchets {trebuchets} d");
        Assert.True(bombardsVsCastle > bombardsVsStone);
        Assert.True(bombardsVsBastion > 5 * bombardsVsStone, $"the trace italienne: {bombardsVsBastion} d vs {bombardsVsStone} d");
        Assert.True(modernVsBastion < 30, $"modern guns breach even a bastion fort in days ({modernVsBastion})");
        // Earthworks resist even modern guns: the equilibrium E* = b/(b + e) stays high.
        var e = Q.Permille(800);
        var earth = SiegeEquations.EarthDamagePerYear(K, new long[] { 0, 0, 0, 20 }, Q32.One, Q32.One);
        for (int day = 0; day < 365; day++) e = SiegeEquations.FieldWorksStep(K, e, earth, Q32.One, Day);
        Assert.True(e.Raw > P(900).Raw, "field works under 20 modern guns settle near b/(b+e) = 20/20.5");
    }

    [Fact]
    public void Regime_IsAFunctionOfTheBesiegersMachines_NotOfAnyDate()
    {
        Assert.Equal(SiegeRegime.Classical, SiegeEquations.RegimeOf(new long[] { 2, 10, 0, 0 }));
        Assert.Equal(SiegeRegime.Gunpowder, SiegeEquations.RegimeOf(new long[] { 0, 10, 1, 0 }));
        Assert.Equal(SiegeRegime.Modern, SiegeEquations.RegimeOf(new long[] { 0, 0, 5, 1 }));
    }

    [Fact]
    public void FortFactor_IsConvexInIntegrity_AndFieldWorksAdd()
    {
        Assert.Equal(new Q32(Q32.OneRaw * 16), SiegeEquations.FortFactor(K, FortKind.StoneWall, Q32.One, Q32.Zero));   // 1 + 15
        var half = SiegeEquations.FortFactor(K, FortKind.StoneWall, P(500), Q32.Zero);
        Assert.Equal(Q32.OneRaw + Q32.FromRatio(15 * 1000, 4000).Raw, half.Raw);                                     // 1 + 15/4
        Assert.Equal(new Q32(Q32.OneRaw * 6), SiegeEquations.FortFactor(K, FortKind.None, Q32.One, Q32.One));         // 1 + 5 × 1
    }

    // ------------------------------------------------------------------ stability and convergence (dt-correctness, Law 3)

    [Fact]
    public void Disease_StaysInRange_AndConverges_ForAnyPulseLength()
    {
        foreach (long dt in new[] { 60_000L, Day, 21 * Day, 3 * 360 * Day })
        {
            var i = SiegeEquations.DiseaseStep(K, P(500), new Q32(Q32.OneRaw * 8), Q32.Zero, dt);
            Assert.True(i.Raw <= Q32.OneRaw && i.Raw >= P(K.DiseaseFloorPermille).Raw, $"dt={dt}");
        }
        // The same 21 days in 1, 21 and 504 steps: the semi-implicit integration converges as the step shrinks.
        Q32 Run(int steps)
        {
            var i = P(50);
            for (int k = 0; k < steps; k++) i = SiegeEquations.DiseaseStep(K, i, P(2000), P(500), 21 * Day / steps);
            return i;
        }
        long one = Run(1).Raw, day = Run(21).Raw, hour = Run(504).Raw;
        Assert.True(Math.Abs(day - hour) < Math.Abs(one - hour), "daily steps are closer to hourly steps than one 21-day step");
        Assert.True(Math.Abs(day - hour) < Q32.OneRaw / 50, "daily vs hourly within 0.02 prevalence");
    }

    [Fact]
    public void LossRates_AreBounded_AndTheIntegratedLossNeverExceedsTheHeads_ForAnyPulseLength()
    {
        Assert.Equal(K.ThirstMaxPermillePerYear, SiegeEquations.ThirstPermilleYear(K, Q32.Zero));
        Assert.Equal(K.StarvationMaxPermillePerYear, SiegeEquations.StarvationPermilleYear(K, Q32.Zero));
        Assert.Equal(0, SiegeEquations.StarvationPermilleYear(K, P(K.StarvationThresholdPermille)));
        Assert.Equal(K.StarvationMaxPermillePerYear / 2, SiegeEquations.StarvationPermilleYear(K, P(K.StarvationThresholdPermille / 2)));
        foreach (long dt in new[] { 60_000L, 21 * Day, 360 * Day })
        {
            // the same competing rates the world applies: their pro-rata shares of one survival product never exceed the heads
            long[] rates = { 2_000, K.ThirstMaxPermillePerYear, K.StarvationMaxPermillePerYear, 5_000, 1_000 };
            long total = rates.Sum();
            var s = HeadcountLoss.Survival(total, dt);
            long milli = rates.Sum(r => HeadcountLoss.LossMilli(1_000, r, total, s));
            Assert.True(milli <= 1_000 * 1_000, $"dt={dt}");
            Assert.True(SiegeEquations.WallStep(K, FortKind.Castle, P(10), new Q32(Q32.OneRaw * 100), Q32.Zero, dt).Raw >= 0);
        }
    }

    // ------------------------------------------------------------------ lane E reuse: disease, desertion, munitions

    [Fact]
    public void DiseaseDeaths_AtTheBackgroundPrevalence_AreLaneE_CampDisease_AndScaleWithPrevalence_HungerAndSanitation()
    {
        var floor = P(K.DiseaseFloorPermille);
        long laneE = TroopLossRates.DiseasePermilleYear(R, 0, 0, false);
        Assert.Equal(R.DiseaseBasePermilleYear, laneE);                                            // 60 ‰/yr
        Assert.Equal(laneE, SiegeEquations.DiseaseDeathPermilleYear(K, R, floor, Q32.One, false));    // the anchor
        Assert.Equal(laneE * 4, SiegeEquations.DiseaseDeathPermilleYear(K, R, P(4 * K.DiseaseFloorPermille), Q32.One, false));
        // Sanitation: lane E's factor (300 ‰); hunger: lane E's factor (×(1 + 4 × hunger)) on the body state
        Assert.Equal(laneE * R.DiseaseSanitationPermille / 1000, SiegeEquations.DiseaseDeathPermilleYear(K, R, floor, Q32.One, true));
        Assert.Equal(TroopLossRates.DiseasePermilleYear(R, 500, 0, false), SiegeEquations.DiseaseDeathPermilleYear(K, R, floor, P(500), false));
        Assert.Equal(laneE * 3, SiegeEquations.DiseaseDeathPermilleYear(K, R, floor, P(500), false));
    }

    [Fact]
    public void Desertion_IsLaneE_Rate_OnMorale_ArrearsAge_AndHunger()
    {
        Assert.Equal(R.DesertionBasePermilleYear, SiegeEquations.DesertionPermilleYear(R, Q32.One, 0, Q32.One));
        Assert.Equal(R.DesertionBasePermilleYear + R.DesertionLowMoralePermilleYear / 2, SiegeEquations.DesertionPermilleYear(R, P(500), 0, Q32.One));
        Assert.Equal(R.DesertionBasePermilleYear + 3 * R.DesertionPerArrearsMonthPermilleYear, SiegeEquations.DesertionPermilleYear(R, Q32.One, 3, Q32.One));
        Assert.Equal(R.DesertionBasePermilleYear + R.DesertionArrearsCapPermilleYear, SiegeEquations.DesertionPermilleYear(R, Q32.One, 1_000, Q32.One));
        Assert.Equal(R.DesertionBasePermilleYear + R.DesertionHungerPermilleYear / 2, SiegeEquations.DesertionPermilleYear(R, Q32.One, 0, P(500)));
        // the same function lane E's army economy uses (one formula, one table)
        Assert.Equal(TroopLossRates.DesertionPermilleYear(R, 400_000, 2, 300, false), SiegeEquations.DesertionPermilleYear(R, P(400), 2, P(700)));
    }

    [Fact]
    public void GunpowderEngines_BreachOnlyWithMunitions_ThrowersNeedNone_AndShooters_FightDryWithout()
    {
        var bombards = new long[] { 0, 0, 10, 0 };
        var throwers = new long[] { 0, 10, 0, 0 };
        Assert.Equal(Q32.Zero, SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, bombards, false, Q32.One, Q32.Zero));
        Assert.Equal(SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, throwers, false, Q32.One, Q32.One),
                     SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, throwers, false, Q32.One, Q32.Zero));
        var half = SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, bombards, false, Q32.One, P(500)).Raw;
        var full = SiegeEquations.WallDamagePerYear(K, FortKind.StoneWall, bombards, false, Q32.One, Q32.One).Raw;
        Assert.InRange(half, full / 2 - 2, full / 2 + 2);
        // the defenders: bowmen draw missiles, gunmen and guns munitions; each without its ammunition fights hand to hand
        var d = new DefenderForce(0, 0, 0, 1_000, 1_000 * 1_000, 0, 1_000, 1_000 * 1_500, 0, 10);
        long all = SiegeEquations.Capability(K, d, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One);
        long noMissiles = SiegeEquations.Capability(K, d, Q32.Zero, Q32.One, Q32.Zero, Q32.One, Q32.One);
        long noMunitions = SiegeEquations.Capability(K, d, Q32.One, Q32.Zero, Q32.Zero, Q32.One, Q32.One);
        Assert.Equal(all - 1_000 * 1_000 + 1_000 * K.DryRangedPermille, noMissiles);
        Assert.Equal(all - 1_000 * 1_500 - 10 * K.DefenderGunValuePermille + 1_000 * K.DryRangedPermille, noMunitions);
    }

    // ------------------------------------------------------------------ the land-module stand-in

    [Fact]
    public void AssaultStandIn_IsKeyedByIdentity_AndSuccessGrowsWithOdds()
    {
        var a = FormationAssaultStandIn.Instance.Resolve(new AssaultInputs(3_000, 2_000, 11, 4, 5 * Day, 1), K);
        var b = FormationAssaultStandIn.Instance.Resolve(new AssaultInputs(3_000, 2_000, 11, 4, 5 * Day, 1), K);
        Assert.Equal(a, b);
        int Wins(long attack)
        {
            int w = 0;
            for (long t = 0; t < 400; t++)
                if (FormationAssaultStandIn.Instance.Resolve(new AssaultInputs(attack, 1_000, 3, 1, t * Day, 1), K).Success) w++;
            return w;
        }
        int w05 = Wins(500), w10 = Wins(1_000), w20 = Wins(2_000), w40 = Wins(4_000);
        Assert.True(w05 < w10 && w10 < w20 && w20 < w40, $"{w05} {w10} {w20} {w40}");
        Assert.InRange(w10, 120, 280);   // p = 1/2 at even odds (fortune is symmetric): 400 trials
        var r = FormationAssaultStandIn.Instance.Resolve(new AssaultInputs(1, 1_000_000, 3, 1, 0, 1), K);
        Assert.Equal(Q.Permille(K.AssaultAttackerLossCapPermille), r.AttackerLossShare);   // bounded by the cap
    }
}
