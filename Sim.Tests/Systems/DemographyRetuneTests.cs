using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.7 acceptance: the demographic tempo is pre-modern. Crude birth and death
// rates MEASURED from fed autoplay land in the historical bands (CBR 35–50,
// CDR 30–45 per 1000·yr), long-run fed growth sits in [0.05, 0.1] %/yr over a
// stated window, early-childhood mortality dominates cohort 0 at the
// 200–300/1000-births scale, and famine produces THREE measurable phases:
// a mortality spike, a birth deficit, and a post-famine birth rebound fed by
// deferred conceptions (never invented ones).
public class DemographyRetuneTests
{
    private const ulong Seed = 42;

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor ProductionExecutor(SimConfig cfg)
    {
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(CanonicalEra(), PipelineLoader.Load(stream, SystemCatalog.All(cfg)));
    }

    /// <summary>The fed rig: production pipeline with farming cranked so the
    /// deficit ratio is exactly zero on every turn after founding — intrinsic
    /// (food-unconstrained) vital rates are what the historical bands describe.
    /// Cranked yield AND per-farmer output (Leontief: both factors must clear).</summary>
    private static SimConfig FedConfig()
    {
        SimConfig cfg = TestConfigs.Sim();
        return cfg with
        {
            Farming = cfg.Farming with
            {
                YieldPerFarmlandPerYear = 100_000.0, // fed even at the tens of millions the transient reaches
                OutputPerFarmerPerYear = 500.0,
            },
        };
    }

    private static WorldState Founded(SimConfig cfg, int? settlements = null) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, settlements);

    private static long TotalPop(WorldState world)
    {
        long total = 0;
        for (int i = 0; i < world.Buckets.Count; i++) total += world.Buckets[i].Count.Value;
        return total;
    }

    private static (long Births, long Deaths, long Starved) LedgerVitals(WorldState world)
    {
        long births = 0, deaths = 0, starved = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity != ConservedQuantityIds.Population) continue;
            if (row.Reason == ReasonIds.Births) births = row.TotalSourced;
            else if (row.Reason == ReasonIds.Deaths) deaths = row.TotalSunk;
            else if (row.Reason == ReasonIds.Starvation) starved = row.TotalSunk;
        }
        return (births, deaths, starved);
    }

    // --- crude rates + long-run growth (the profile bands) ------------------

    [Fact]
    public void FedAutoplay_CrudeRates_AndLongRunGrowth_InPreModernBands()
    {
        // STATED WINDOW: turns 81–220 at Neolithic dt = 10 — sim-years 800 to
        // 2200 after founding. The founding age distribution is adult-heavy
        // and rings through the age structure for ~80 turns before the stable
        // regime the bands describe sets in; past ~t220 the fed rig's own
        // farmland ceiling catches the exponential (measured), so 1400 stable
        // years is the honest maximal window.
        SimConfig cfg = FedConfig();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);

        for (int t = 1; t <= 80; t++) world = exec.Step(world);
        long popStart = TotalPop(world);
        (long b0, long d0, long s0) = LedgerVitals(world);

        double personYears = 0.0;
        for (int t = 81; t <= 220; t++)
        {
            long before = TotalPop(world);
            world = exec.Step(world);
            // Rates apply to the PREV population (§3.2) — the exposure of
            // turn t is the population entering it, for dt years.
            personYears += before * 10.0;
        }
        long popEnd = TotalPop(world);
        (long b1, long d1, long s1) = LedgerVitals(world);

        Assert.Equal(0, s1 - s0); // the rig IS fed — any starvation voids the measurement
        double cbr = (b1 - b0) / personYears * 1000.0;
        double cdr = (d1 - d0) / personYears * 1000.0;
        double growthPctPerYear = Math.Log(popEnd / (double)popStart) / 1400.0 * 100.0;

        Console.WriteLine(
            $"fed autoplay turns 81–220 (1400 sim-years): CBR {cbr:F1}/1000yr, CDR {cdr:F1}/1000yr, "
            + $"growth {growthPctPerYear:F4} %/yr, pop {popStart} -> {popEnd}");
        Assert.True(cbr is >= 35.0 and <= 50.0, $"crude birth rate {cbr:F1} outside [35, 50]/1000yr");
        Assert.True(cdr is >= 30.0 and <= 45.0, $"crude death rate {cdr:F1} outside [30, 45]/1000yr");
        Assert.True(growthPctPerYear is >= 0.05 and <= 0.1,
            $"long-run fed growth {growthPctPerYear:F4} %/yr outside [0.05, 0.1]");
    }

    // --- early-childhood mortality (cohort 0 dominance) ---------------------

    [Fact]
    public void EarlyChildhoodMortality_Dominates_At200To300Per1000Births()
    {
        // A newborn cohort of 10,000 run through ONE dt = 5 turn of the
        // CANONICAL mortality array (fertility and starvation zeroed) measures
        // the deaths charged to cohort 0 before slot-advance promotes the
        // survivors — the model's under-5 loss per 1000 births. (Euler-linear
        // at width 5 by design; the band is the pre-modern 200–300 scale.)
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        var counts = new long[Cohorts.Count];
        counts[0] = 10_000;
        var exec = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = exec.Run(PopulationExactnessTests.BucketWorld(counts), 1);

        (_, long deaths, _) = LedgerVitals(world);
        Console.WriteLine($"early-childhood deaths per 1000 births: {deaths / 10.0:F0}");
        Assert.True(deaths is >= 2000 and <= 3000,
            $"cohort-0 deaths {deaths}/10000 outside the 200–300/1000 band");

        // Dominance, scoped honestly: cohort 0's rate strictly exceeds every
        // childhood and prime-adult rate (cohorts 1–8, ages 5–45) and towers
        // over childhood proper (1–2). From age 45 the senescence climb is
        // ALLOWED to pass it — that is survivorship declining with age, not a
        // rival infancy notch. (The packet's corner constraints force this
        // shape: capping cohort-0 loss at 300/1000 births while the crude
        // bands demand e0 ≈ 26 pushes the remaining deaths into adulthood.)
        double[] m = cfg.Demographics.MortalityPerYear;
        for (int c = 1; c <= 8; c++)
            Assert.True(m[0] > m[c], $"mortality[0] {m[0]} <= mortality[{c}] {m[c]}");
        for (int c = 1; c <= 2; c++)
            Assert.True(m[0] >= 1.6 * m[c],
                $"mortality[0] {m[0]} does not tower over childhood mortality[{c}] {m[c]}");
    }

    // --- famine: three measurable phases ------------------------------------

    [Fact]
    public void Famine_MortalitySpike_BirthDeficit_PostFamineRebound()
    {
        // Controlled SHALLOW famine on an N = 1 world. Depth is a real design
        // constraint: at dt = 10, starvation (0.12/yr × ratio × age
        // multipliers × 10 years) stacked on the pre-modern base mortality
        // (~0.36/decade) halves a settlement per hot turn — a deep famine
        // leaves nobody to rebound. The rig: a modest-surplus fed config
        // (labor-limited harvest ≈ 1.06× demand, founding store just covering
        // the harvestless first turn), five famine turns at ~77% of demand
        // (the store cushions ~2, the deficit then holds ≈ 0.2), then the fed
        // config restored.
        //
        // Phases 1–2 (mortality spike, birth deficit) are measured from the
        // Ledger's per-turn birth/death deltas. Phase 3 (rebound) uses the
        // T2.5 twin-difference pattern: a LOCKSTEP TWIN with
        // reboundRecoverableFraction = 0 (suppression identical, banking off)
        // is bit-identical through the last suppressed turn, so the birth
        // difference on the FIRST released turn is exactly the reservoir's
        // contribution — no age-structure bias, no per-capita hand-waving.
        // The same twin bounds it above: released extra births never exceed
        // the reservoir observed the turn before (deferred, NOT invented).
        SimConfig fed = TestConfigs.Sim();
        fed = fed with
        {
            Farming = fed.Farming with { YieldPerFarmlandPerYear = 1000.0, OutputPerFarmerPerYear = 1.45 },
            Founding = fed.Founding with { FoodStore = 4000 },
        };
        SimConfig starving = fed with
        {
            Farming = fed.Farming with { YieldPerFarmlandPerYear = 1000.0, OutputPerFarmerPerYear = 1.05 },
        };
        (long[] births, long[] deaths, double[] deficits, double[] reservoirs) = RunFamineSchedule(fed, starving);
        SimConfig fedOff = fed with
        {
            Demographics = fed.Demographics with { ReboundRecoverableFraction = 0.0 },
        };
        SimConfig starvingOff = starving with
        {
            Demographics = starving.Demographics with { ReboundRecoverableFraction = 0.0 },
        };
        (long[] birthsOff, _, _, double[] reservoirsOff) = RunFamineSchedule(fedOff, starvingOff);

        // Baseline from fed turns 4–6 (post-founding-transient, pre-famine).
        double baseBirths = (births[3] + births[4] + births[5]) / 3.0;
        double baseDeaths = (deaths[3] + deaths[4] + deaths[5]) / 3.0;

        // The deficit must have engaged during the famine turns (else every
        // phase assert is vacuous) AND stayed shallow (a deep dt = 10 deficit
        // exterminates the settlement and the rebound has nobody to measure —
        // the depth guard keeps the rig honest about what it claims to test).
        double worstDeficit = 0.0;
        for (int t = 6; t < 11; t++) worstDeficit = Math.Max(worstDeficit, deficits[t]);
        Assert.True(worstDeficit > 0.12, $"famine rig too weak: worst deficit {worstDeficit:F2} <= 0.12");
        Assert.True(worstDeficit < 0.6, $"famine rig too deep: worst deficit {worstDeficit:F2} >= 0.6");

        // Phase 1 — mortality spike: some famine-window turn kills at well
        // above the fed baseline. (Deficit is Prev-read: the window extends
        // one turn past the famine configs — turns 8–13.)
        double spike = 0.0;
        for (int t = 7; t < 13; t++) spike = Math.Max(spike, deaths[t]);
        Assert.True(spike > 1.3 * baseDeaths,
            $"no mortality spike: max famine deaths {spike:F0} <= 1.3x baseline {baseDeaths:F0}");

        // Phase 2 — birth deficit: some famine-window turn conceives at well
        // below the fed baseline (suppression, same one-turn lag).
        double trough = double.MaxValue;
        for (int t = 7; t < 13; t++) trough = Math.Min(trough, births[t]);
        Assert.True(trough < 0.6 * baseBirths,
            $"no birth deficit: min famine births {trough:F0} >= 0.6x baseline {baseBirths:F0}");

        // Phase 3 — rebound on the first RELEASED turn: the first turn after
        // the famine window whose PREV deficit was exactly zero (the release
        // gate). The twin runs are identical until it, so the difference is
        // the reservoir's release alone.
        int release = -1;
        for (int t = 11; t < births.Length; t++)
        {
            if (deficits[t - 1] == 0.0) { release = t; break; }
        }
        Assert.True(release > 0, "no post-famine turn with Prev deficit == 0 — recovery never landed");
        long extra = births[release] - birthsOff[release];
        Assert.True(extra >= 0.25 * baseBirths,
            $"rebound not measurable: released extra births {extra} < 0.25x baseline {baseBirths:F0}");
        // Deferred, not invented: bounded by the bank the twin never built.
        Assert.True(extra <= reservoirs[release - 1] + 1.0,
            $"released {extra} exceeds the reservoir {reservoirs[release - 1]:F2} banked before the turn");
        Assert.True(reservoirsOff[release - 1] == 0.0, "rebound-off twin banked anyway — twin rig broken");

        Console.WriteLine(
            $"famine phases: baseline births {baseBirths:F0}/deaths {baseDeaths:F0} per turn; "
            + $"worst deficit {worstDeficit:F2}, spike {spike:F0}, trough {trough:F0}; release turn {release + 1}: "
            + $"births {births[release]} vs twin {birthsOff[release]} (+{extra}), bank was {reservoirs[release - 1]:F1}");
    }

    /// <summary>6 fed turns, 5 famine turns, 6 fed turns on an N = 1 world;
    /// returns per-turn birth/death(+starvation) deltas, the settlement's
    /// deficit ratio, and the summed cohort-0 ReboundReservoir.</summary>
    private static (long[] Births, long[] Deaths, double[] Deficits, double[] Reservoirs)
        RunFamineSchedule(SimConfig fed, SimConfig starving)
    {
        TurnExecutor fedExec = ProductionExecutor(fed);
        TurnExecutor famineExec = ProductionExecutor(starving);
        WorldState world = Founded(fed, settlements: 1);

        var births = new long[17];
        var deaths = new long[17];
        var deficits = new double[17];
        var reservoirs = new double[17];
        (long pb, long pd, long ps) = (0, 0, 0);
        for (int t = 0; t < 17; t++)
        {
            world = (t is >= 6 and < 11 ? famineExec : fedExec).Step(world);
            (long b, long d, long s) = LedgerVitals(world);
            births[t] = b - pb;
            deaths[t] = d - pd + (s - ps); // famine deaths = base + starvation
            deficits[t] = world.ConsumptionDeficits.Count > 0 ? world.ConsumptionDeficits[0].DeficitRatio : 0.0;
            for (int i = 0; i < world.Buckets.Count; i++)
                if (world.Buckets[i].CohortIdx == 0) reservoirs[t] += world.Buckets[i].ReboundReservoir;
            (pb, pd, ps) = (b, d, s);
        }
        return (births, deaths, deficits, reservoirs);
    }

    // --- deferred, not invented: exact reservoir accounting -----------------

    [Fact]
    public void Rebound_ReleasesOnlyWhatWasBanked_ExactRecurrence()
    {
        // Demographics-only rig, dt = 2.5, one bucket group with the fertile
        // pool parked in the ABSORBING 75+ cohort (mortality zeroed, so the
        // pool is exactly stationary — slot-advance would halve a mid-table
        // cohort every turn and make the recurrence drift) and a hand-set
        // deficit: TWO famine turns bank suppressed conceptions, then fed
        // turns drain the bank. Every value is re-derived with the SAME
        // operation order as the system — bit-exact double equality, no
        // tolerances.
        SimConfig cfg = TestConfigs.Sim();
        double[] fertility = new double[Cohorts.Count];
        fertility[15] = 0.1;
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = fertility,
                MortalityPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0, // isolate fertility: nobody dies
            },
        };
        DemographicsConfig d = cfg.Demographics;
        // deficit 0.2 keeps the suppression PARTIAL at the canonical slope
        // (factor 0.4) — the clamped-to-zero branch is exercised by the
        // famine-phases test; this one pins the linear region's arithmetic.
        const double dt = 2.5, deficit = 0.2;
        var counts = new long[Cohorts.Count];
        counts[15] = 1000;

        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), deficit, 0));
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);

        // Famine turns: births suppressed, bank grows — replay the recurrence.
        double birthsPerYear = fertility[15] * 1000;
        double unsuppressed = birthsPerYear * dt;
        double factor = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * deficit);
        double expectedBank = 0.0, expectedRemainder = 0.0;
        long expectedBorn = 0;
        for (int t = 0; t < 2; t++)
        {
            world = exec.Step(world);
            double totalExact = unsuppressed * factor;
            expectedBank += d.ReboundRecoverableFraction * (unsuppressed - totalExact);
            // dt = 2.5 < width 5: all newborns land in cohort 0 (share 1.0).
            double exact = totalExact + expectedRemainder;
            long born = (long)Math.Floor(exact);
            expectedRemainder = exact - born;
            expectedBorn += born;
            Assert.Equal(expectedBank, world.Buckets[0].ReboundReservoir);
            // Suppression really suppressed: fewer than unsuppressed born.
            Assert.True(born < unsuppressed, "suppression did not reduce births");
        }

        // Fed turns: the deficit row zeroes; the bank drains into births.
        world.ConsumptionDeficits.Ref(0).DeficitRatio = 0.0;
        double drained = 0.0;
        for (int t = 0; t < 3; t++)
        {
            world = exec.Step(world);
            double release = expectedBank * Math.Min(1.0, d.ReboundReleaseRatePerYear * dt);
            expectedBank -= release;
            drained += release;
            double exact = unsuppressed + release + expectedRemainder;
            long born = (long)Math.Floor(exact);
            expectedRemainder = exact - born;
            expectedBorn += born;
            Assert.Equal(expectedBank, world.Buckets[0].ReboundReservoir);
        }

        // Deferred, not invented: everything ever released is bounded by what
        // was banked, and the ledger's total births match the recurrence
        // person-for-person.
        double banked = 2.0 * d.ReboundRecoverableFraction * (unsuppressed - unsuppressed * factor);
        Assert.True(drained <= banked, $"released {drained} exceeds banked {banked}");
        (long ledgerBirths, _, _) = LedgerVitals(world);
        Assert.Equal(expectedBorn, ledgerBirths);
        Assert.True(world.Buckets[0].ReboundReservoir > 0.0, "bank fully drained in 3 turns — release-rate rig too hot");
    }

    [Fact]
    public void ReboundRelease_DtHalving_FirstOrderConvergence()
    {
        // Law-3 pin for the release integrator (rate × dt with the saturation
        // clamp): the same 40 fed sim-years at dt 10 / 5 / 2.5 drain a
        // hand-seeded reservoir; successive refinements should roughly halve
        // the remaining-bank deviation (Euler). Fertility nonzero (release
        // requires living parents) but tiny, so births barely perturb counts.
        SimConfig cfg = TestConfigs.Sim();
        double[] fertility = new double[Cohorts.Count];
        fertility[15] = 1e-9; // absorbing cohort: the parent pool never ages away
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = fertility,
                MortalityPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        const double seedBank = 1000.0;
        // 20 years, not more: at rate 0.08 the dt = 10 factor is already
        // min(1, 0.8) — longer horizons push all three runs into the drained
        // tail where the deviations no longer separate.
        const int horizonYears = 20;
        var finals = new double[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            var counts = new long[Cohorts.Count];
            counts[15] = 1000;
            WorldState world = PopulationExactnessTests.BucketWorld(counts);
            world.Buckets.Ref(0).ReboundReservoir = seedBank;
            var exec = new TurnExecutor(FlatEra(dts[i]), [SystemCatalog.Demographics(cfg)]);
            world = exec.Run(world, (int)(horizonYears / dts[i]));
            finals[i] = world.Buckets[0].ReboundReservoir;
        }

        double l1Coarse = Math.Abs(finals[0] - finals[1]);
        double l1Fine = Math.Abs(finals[1] - finals[2]);
        Assert.True(l1Coarse > 0 && l1Fine > 0, "dt-halving produced no deviation — vacuous");
        double ratio = l1Coarse / l1Fine;
        Assert.True(ratio is >= 1.4 and <= 3.5,
            $"release convergence ratio {ratio:F2} outside [1.4, 3.5] "
            + $"(banks: dt10 {finals[0]:F3}, dt5 {finals[1]:F3}, dt2.5 {finals[2]:F3})");
    }

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");
}
