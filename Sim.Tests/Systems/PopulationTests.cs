using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.5 acceptance: fed world grows; unfed world starves to a floor (never
// negative, never NaN, food hits zero exactly, audit exact); Malthus-lite BINDS
// (overshoot-correction cycles measurable, constant run fails the metric);
// dt-halving characterizes the linear-integration error; per-turn audit exact
// for Population AND Food across 200 turns; person-exact reconciliation from
// the ledger alone; equilibrium + per-phase bench reported.
public class PopulationTests
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

    private static WorldState Founded(SimConfig cfg, int? settlements = null) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, settlements);

    private static long TotalPop(WorldState world)
    {
        long total = 0;
        for (int i = 0; i < world.Buckets.Count; i++) total += world.Buckets[i].Count.Value;
        return total;
    }

    private static void AssertRowsSaneAndAuditExact(WorldState world, int turn)
    {
        // Never negative, never NaN — checked per turn, on every row. Remainders
        // must sit in [0,1): the documented invariant that clamp shortfalls are
        // never banked (adversarial finding — a banking regression drove a
        // remainder to 81.74 in famine, so this bound has real teeth; [0,1)
        // implies finite, so no separate IsFinite check is needed).
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            BucketRow row = world.Buckets[i];
            Assert.True(row.Count.Value >= 0, $"turn {turn}: cohort {row.CohortIdx} count {row.Count.Value} < 0");
            Assert.True(
                row.BirthRemainder is >= 0.0 and < 1.0 && row.DeathRemainder is >= 0.0 and < 1.0
                && row.StarvationRemainder is >= 0.0 and < 1.0 && row.AgingRemainder is >= 0.0 and < 1.0,
                $"turn {turn}: remainder outside [0,1) in cohort {row.CohortIdx}");
        }
        for (int i = 0; i < world.FoodStores.Count; i++)
        {
            FoodStoreRow row = world.FoodStores[i];
            Assert.True(row.Store.Value >= 0, $"turn {turn}: food store {row.Store.Value} < 0");
            Assert.True(
                row.HarvestRemainder is >= 0.0 and < 1.0 && row.EatenRemainder is >= 0.0 and < 1.0,
                $"turn {turn}: food remainder outside [0,1)");
        }
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
        {
            double ratio = world.ConsumptionDeficits[i].DeficitRatio;
            Assert.True(double.IsFinite(ratio) && ratio >= 0.0 && ratio <= 1.0,
                $"turn {turn}: deficit ratio {ratio} outside [0,1]");
        }
        Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {turn}: {report}");
    }

    [Fact]
    public void FedWorld_PopulationGrows_FromFoundingSeed_Over100Turns()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        long founding = TotalPop(world);

        for (int t = 1; t <= 100; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, t);
        }

        Assert.True(TotalPop(world) > founding,
            $"fed world did not grow: {TotalPop(world)} <= founding {founding}");
    }

    [Fact]
    public void UnfedWorld_DeclinesToFloor_NeverNegative_FoodHitsZeroExactly()
    {
        // Farming disabled by config — the unfed world eats only its founding store.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with { Farming = cfg.Farming with { YieldPerFarmlandPerYear = 0.0 } };
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        long founding = TotalPop(world);

        long firstZeroFoodTurn = -1;
        for (int t = 1; t <= 150; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, t);

            long store = world.FoodStores[0].Store.Value;
            if (firstZeroFoodTurn < 0 && store == 0) firstZeroFoodTurn = t;
            // With no harvest possible the store can never refill: once zero,
            // EXACTLY zero forever (ClampToAvailable bottoms out, long equality).
            if (firstZeroFoodTurn >= 0)
                Assert.Equal(0, store);
        }

        Assert.True(firstZeroFoodTurn > 0, "food store never hit zero in an unfed world");
        Assert.True(TotalPop(world) < founding,
            $"unfed world did not decline: {TotalPop(world)} >= founding {founding}");
    }

    [Fact]
    public void DeadWorld_StaysDeadCleanly_Forever()
    {
        // Extinction ruling (director, T1.8): terminal extinction is ACCEPTED
        // at M1. This pins the CLEANLINESS: after the last person dies, harvest
        // is zero (Leontief labor side), path labor is zero, food is static,
        // the audit stays exact, and nothing goes NaN — forever. Driven the way
        // the director actually did it: a 0%-farm order.
        SimConfig cfg = TestConfigs.Sim();
        var orders = new OrderLog();
        orders.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 0.0));
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(CanonicalEra(),
            PipelineLoader.Load(stream, SystemCatalog.All(cfg)), orders);
        // N = 1 (D-029 flag): the 0%-farm order targets settlement 0; dead-
        // world cleanliness is a single-settlement semantic — with neighbors
        // alive the world total never hits zero and the test measures nothing.
        WorldState world = Founded(cfg, settlements: 1);

        int extinctionTurn = -1;
        for (int t = 1; t <= 80 && extinctionTurn < 0; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, t);
            if (TotalPop(world) == 0) extinctionTurn = t;
        }
        Assert.True(extinctionTurn > 0, "0% farm never drove extinction in 80 turns");

        long foodAtDeath = world.FoodStores[0].Store.Value;
        double bankAtDeath = world.PathProgress.Count > 0 ? world.PathProgress[0].Banked : 0.0;
        long harvestAtDeath = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Food && row.Reason == ReasonIds.Harvest)
                harvestAtDeath = row.TotalSourced;
        }

        for (int t = 1; t <= 30; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, extinctionTurn + t);
            Assert.Equal(0, TotalPop(world));
            Assert.Equal(foodAtDeath, world.FoodStores[0].Store.Value);
            if (world.PathProgress.Count > 0)
                Assert.Equal(bankAtDeath, world.PathProgress[0].Banked); // zero adults accrue nothing
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.Food && row.Reason == ReasonIds.Harvest)
                    Assert.Equal(harvestAtDeath, row.TotalSourced);
            }
        }
    }

    [Fact]
    public void Pyramid_FormsWideBase_UnderDefaultRates()
    {
        // T2.1 acceptance: after the founding transient the age structure is a
        // wide-base pyramid — quartile sums (4 cohorts = 20 years each) decline
        // with age, and the derived child band outnumbers the elder band.
        // Quartile sums are robust to the slot-advance parity striping at
        // Neolithic dt = 10 (a 10-year turn cannot resolve 5-year sub-structure).
        // Averaged over turns 80–100 (a full Malthus cycle): a single-turn
        // snapshot can land mid-famine, where the age-selective starvation
        // multipliers legitimately notch the base (T2.2 made this visible —
        // the class-system retune shifted the cycle phase at turn 100).
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        var settlement = new SettlementId(0);
        var quartile = new long[4];
        for (int t = 1; t <= 100; t++)
        {
            world = exec.Step(world);
            if (t < 80) continue;
            for (int i = 0; i < world.Buckets.Count; i++)
            {
                BucketRow row = world.Buckets[i];
                if (row.Settlement == settlement) quartile[row.CohortIdx / 4] += row.Count.Value;
            }
        }
        Assert.True(TotalPop(world) > 0, "population died out — pyramid vacuous");
        Assert.True(quartile[0] > quartile[1] && quartile[1] > quartile[2] && quartile[2] > quartile[3],
            $"not wide-base: quartiles {quartile[0]}/{quartile[1]}/{quartile[2]}/{quartile[3]}");
        Assert.True(
            BandViews.Children(world.Buckets, settlement) > BandViews.Elders(world.Buckets, settlement),
            "children do not outnumber elders");
    }

    // --- Malthus-lite -------------------------------------------------------

    /// <summary>
    /// The binding oscillation metric: hysteresis crossings of the trajectory
    /// around its own mean. State flips to "above" only beyond mean·(1+ε) and
    /// to "below" only under mean·(1−ε), so a flat line (or noise within the
    /// band) counts ZERO crossings in either direction. Returns (down, up).
    /// </summary>
    private static (int Down, int Up) MeanCrossings(long[] trajectory, double epsilon)
    {
        double mean = 0.0;
        for (int i = 0; i < trajectory.Length; i++) mean += trajectory[i];
        mean /= trajectory.Length;

        double hi = mean * (1.0 + epsilon), lo = mean * (1.0 - epsilon);
        int state = 0, down = 0, up = 0; // 0 = unknown, +1 above, −1 below
        for (int i = 0; i < trajectory.Length; i++)
        {
            if (trajectory[i] > hi)
            {
                if (state == -1) up++;
                state = 1;
            }
            else if (trajectory[i] < lo)
            {
                if (state == 1) down++;
                state = -1;
            }
        }
        return (down, up);
    }

    [Fact]
    public void MalthusLite_OvershootCorrectionCycles_MeasurableIn1000Turns()
    {
        // WINDOW RESIZED at T2.7 (stated, per packet): at the pre-modern
        // tempo (fed growth ≈ 0.07 %/yr) one full overshoot–crash–recovery
        // cycle spans ~650 sim-years — the dev world's first crash lands near
        // turn 255 and recoveries take centuries, so the old 200-turn window
        // could not contain even one cycle. 1000 turns (sim-years −4000 →
        // ~+1000 across the era table's dt shifts) holds two-plus cycles:
        // crashes measured near turns 255, 535 and 855.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);

        var trajectory = new List<long>();
        for (int t = 1; t <= 1000; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, t); // per-turn audit across the FULL 1000-turn run
            if (t >= 30) trajectory.Add(TotalPop(world)); // post-transient window
        }

        // The sharpened acceptance: the population trajectory crosses its own
        // long-run mean from above AND from below at least twice each — an
        // overshoot-correction cycle is not a one-off, it recurs.
        (int down, int up) = MeanCrossings([.. trajectory], epsilon: 0.02);
        Assert.True(down >= 2 && up >= 2,
            $"Malthus-lite oscillation not measurable: {down} down-crossings, {up} up-crossings");

        // Equilibrium report (acceptance): long-run mean at seed 42.
        double mean = 0.0;
        foreach (long p in trajectory) mean += p;
        mean /= trajectory.Count;
        Console.WriteLine($"equilibrium population @ seed {Seed} (dev 256², turns 30–1000): mean {mean:F0}");
    }

    [Fact]
    public void MalthusMetric_ConstantPopulation_Fails_TheMetricBinds()
    {
        // Rigged rates: no births, no deaths, no aging, no starvation — the
        // population is EXACTLY constant. The oscillation metric must report
        // zero crossings, proving the Malthus assertion cannot pass vacuously.
        SimConfig cfg = TestConfigs.Sim();
        // Aging cannot be disabled (it is structural, derived from dt), but it
        // is a conserving Transfer — the TOTAL stays exactly constant.
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = new double[Cohorts.Count],
                MortalityPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        long founding = TotalPop(world);

        var trajectory = new List<long>();
        for (int t = 1; t <= 200; t++)
        {
            world = exec.Step(world);
            if (t >= 30) trajectory.Add(TotalPop(world));
        }

        Assert.Equal(founding, TotalPop(world)); // really constant
        (int down, int up) = MeanCrossings([.. trajectory], epsilon: 0.02);
        Assert.True(down == 0 && up == 0,
            $"metric counted crossings on a constant run: {down} down, {up} up");
    }

    // --- dt-halving ---------------------------------------------------------

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    [Fact]
    public void DtInvariance_MortalityFlow_ExactAcrossDts()
    {
        // ADR-011 SUPERSESSION of the old first-order dt-halving pin: the
        // exponential-survival micro-kernel makes mortality dt-INVARIANT by
        // construction (every dt composes the same half-year micro-steps), so
        // the strictly stronger pin is EQUALITY — deaths-only decay on the
        // absorbing 75+ cohort to the same sim-year horizon lands on the same
        // count at dt 10 / 5 / 2.5 within ±1 (turn-boundary flooring). The
        // old Euler behavior (ratio ≈ 2 between refinements) fails this
        // loudly, as does any reintroduced turn-scale rate × dt flow.
        SimConfig cfg = TestConfigs.Sim();
        double[] mortality = new double[Cohorts.Count];
        mortality[15] = 0.03;
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = new double[Cohorts.Count],
                MortalityPerYear = mortality,
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        var counts = new long[Cohorts.Count];
        counts[15] = 1_000_000;

        const int horizon = 200; // sim-years: 20 / 40 / 80 turns
        var finals = new long[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            var exec = new TurnExecutor(FlatEra(dts[i]), [SystemCatalog.Demographics(cfg)]);
            WorldState world = exec.Run(
                PopulationExactnessTests.BucketWorld(counts), (int)(horizon / dts[i]));
            finals[i] = world.Buckets[15].Count.Value;
        }

        long expected = (long)(1_000_000 * Math.Exp(-0.03 * horizon)); // ≈ 2478
        Assert.True(Math.Abs(finals[0] - expected) <= 2,
            $"dt10 final {finals[0]} vs closed-form {expected}");
        Assert.True(Math.Abs(finals[0] - finals[1]) <= 1,
            $"dt10 {finals[0]} vs dt5 {finals[1]} — not dt-invariant");
        Assert.True(Math.Abs(finals[1] - finals[2]) <= 1,
            $"dt5 {finals[1]} vs dt2.5 {finals[2]} — not dt-invariant");
    }

    [Fact]
    public void SlotAdvanceAging_OneCoarseStepEqualsTwoFineSteps_Exact()
    {
        // The aging operator's dt-correctness at the era table's integral-slot
        // dts: one dt = 10 step must equal two dt = 5 steps EXACTLY (both are
        // pure slot translations; a linear-rate aging regression ages people
        // at half speed at dt = 10 and breaks this equality immediately).
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = new double[Cohorts.Count],
                MortalityPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = 1000 + 137 * c;

        WorldState coarse = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)])
            .Run(PopulationExactnessTests.BucketWorld(counts), 1);
        WorldState fine = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Demographics(cfg)])
            .Run(PopulationExactnessTests.BucketWorld(counts), 2);
        // ADR-011: identical micro-sequences; the fine run reconciles integers
        // at the extra turn boundary, so ±1 per row of flooring is the exact
        // bound (a kernel divergence shows up as multi-person drift).
        for (int c = 0; c < Cohorts.Count; c++)
            Assert.True(Math.Abs(fine.Buckets[c].Count.Value - coarse.Buckets[c].Count.Value) <= 1,
                $"cohort {c}: coarse {coarse.Buckets[c].Count.Value} vs fine {fine.Buckets[c].Count.Value}");
    }

    // --- reconciliation -----------------------------------------------------

    [Fact]
    public void Reconciliation_FromLedgerAlone_PersonAndFoodExact()
    {
        // The moral bookkeeping, proven from the sources/sinks table ALONE:
        //   Population: InitialEndowment + Births − Deaths − Starvation == Σ bands
        //   Food:       InitialEndowment + Harvest − Eaten            == Σ stores
        // and NO other reason ever touches either quantity (aging is a Transfer —
        // it conserves by construction and never appears here).
        // 650 turns (T2.7b, stated): the vacuity guard below demands every
        // flow occurred, and on the honest post-ADR-011 dynamics the dev
        // world's first Malthus crash (and first starvation) lands near turn
        // 590 — the growth to carrying capacity is slower and cleaner than
        // the mortality-dodging era ever showed.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        for (int t = 1; t <= 650; t++) world = exec.Step(world);

        long popEndow = 0, births = 0, deaths = 0, starved = 0;
        long foodEndow = 0, harvest = 0, eaten = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Population)
            {
                if (row.Reason == ReasonIds.InitialEndowment) popEndow = row.TotalSourced;
                else if (row.Reason == ReasonIds.Births) births = row.TotalSourced;
                else if (row.Reason == ReasonIds.Deaths) deaths = row.TotalSunk;
                else if (row.Reason == ReasonIds.Starvation) starved = row.TotalSunk;
                else Assert.Fail($"unexpected Population flow reason {row.Reason.Value}");
                // Source-only reasons never sink and vice versa.
                if (row.Reason == ReasonIds.InitialEndowment || row.Reason == ReasonIds.Births)
                    Assert.Equal(0, row.TotalSunk);
                else
                    Assert.Equal(0, row.TotalSourced);
            }
            else if (row.Quantity == ConservedQuantityIds.Food)
            {
                if (row.Reason == ReasonIds.InitialEndowment) foodEndow = row.TotalSourced;
                else if (row.Reason == ReasonIds.Harvest) harvest = row.TotalSourced;
                else if (row.Reason == ReasonIds.Eaten) eaten = row.TotalSunk;
                else Assert.Fail($"unexpected Food flow reason {row.Reason.Value}");
            }
        }

        long popFromLedger = checked(popEndow + births - deaths - starved);
        long foodFromLedger = checked(foodEndow + harvest - eaten);
        Assert.Equal(TotalPop(world), popFromLedger);        // person-exact
        long storeTotal = 0;
        for (int i = 0; i < world.FoodStores.Count; i++) storeTotal += world.FoodStores[i].Store.Value;
        Assert.Equal(storeTotal, foodFromLedger);            // food-exact
        Assert.True(births > 0 && deaths > 0 && starved > 0 && harvest > 0 && eaten > 0,
            "reconciliation is vacuous — some flow never occurred in 650 turns");
    }

    // --- bench report -------------------------------------------------------

    private sealed class PhaseTotals : ITurnObserver
    {
        public readonly Dictionary<string, (long Ticks, long Bytes)> Totals = [];
        public void OnPhase(string phase, long ticks, long bytes)
        {
            (long t, long b) = Totals.TryGetValue(phase, out var cur) ? cur : (0L, 0L);
            Totals[phase] = (t + ticks, b + bytes);
        }
    }

    [Fact]
    public void ProductionPipeline_PerPhaseBench_Reported()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        var observer = new PhaseTotals();

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int t = 1; t <= 200; t++) world = exec.Step(world, observer);
        double totalMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                         / System.Diagnostics.Stopwatch.Frequency;

        foreach (string phase in new[] { "clone", "catchment", "farming", "consumption", "demographics", "pathbuild" })
        {
            (long ticks, long bytes) = observer.Totals[phase];
            double ms = ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Console.WriteLine($"bench {phase}: {ms:F2} ms total over 200 turns, {bytes} bytes allocated");
        }
        Console.WriteLine($"bench total: {totalMs:F1} ms for 200 founded turns (dev 256²)");
        Assert.True(totalMs < 5000, $"200 founded turns took {totalMs:F0} ms");
    }
}
