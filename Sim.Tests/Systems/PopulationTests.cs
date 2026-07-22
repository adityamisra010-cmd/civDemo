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

    private static WorldState Founded(SimConfig cfg) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed);

    private static long TotalPop(WorldState world)
    {
        long total = 0;
        for (int i = 0; i < world.PopBands.Count; i++) total += world.PopBands[i].Count.Value;
        return total;
    }

    private static void AssertRowsSaneAndAuditExact(WorldState world, int turn)
    {
        // Never negative, never NaN — checked per turn, on every row. Remainders
        // must sit in [0,1): the documented invariant that clamp shortfalls are
        // never banked (adversarial finding — a banking regression drove a
        // remainder to 81.74 in famine, so this bound has real teeth; [0,1)
        // implies finite, so no separate IsFinite check is needed).
        for (int i = 0; i < world.PopBands.Count; i++)
        {
            PopBandRow row = world.PopBands[i];
            Assert.True(row.Count.Value >= 0, $"turn {turn}: band {row.Band} count {row.Count.Value} < 0");
            Assert.True(
                row.BirthRemainder is >= 0.0 and < 1.0 && row.DeathRemainder is >= 0.0 and < 1.0
                && row.StarvationRemainder is >= 0.0 and < 1.0 && row.AgingRemainder is >= 0.0 and < 1.0,
                $"turn {turn}: remainder outside [0,1) in band {row.Band}");
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
        WorldState world = Founded(cfg);

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
    public void MalthusLite_OvershootCorrectionCycles_MeasurableIn200Turns()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);

        var trajectory = new List<long>();
        for (int t = 1; t <= 200; t++)
        {
            world = exec.Step(world);
            AssertRowsSaneAndAuditExact(world, t); // per-turn audit across the FULL 200-turn run
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
        Console.WriteLine($"equilibrium population @ seed {Seed} (dev 256², turns 30–200): mean {mean:F0}");
    }

    [Fact]
    public void MalthusMetric_ConstantPopulation_Fails_TheMetricBinds()
    {
        // Rigged rates: no births, no deaths, no aging, no starvation — the
        // population is EXACTLY constant. The oscillation metric must report
        // zero crossings, proving the Malthus assertion cannot pass vacuously.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                BirthsPerAdultPerYear = 0.0,
                ChildMortalityPerYear = 0.0,
                AdultMortalityPerYear = 0.0,
                ElderMortalityPerYear = 0.0,
                AgingChildToAdultPerYear = 0.0,
                AgingAdultToElderPerYear = 0.0,
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

    /// <summary>Hand-built demographics-only world at 100k-person scale (integer
    /// quantization noise ≪ signal), endowed through the Ledger.</summary>
    private static WorldState DemographicsWorld()
    {
        var world = new WorldState(7);
        var settlement = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(settlement, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        Span<long> counts = [33000L, 50000L, 17000L];
        for (int band = 0; band < PopBands.Count; band++)
        {
            int row = world.PopBands.Add(new PopBandRow(
                settlement, band, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref world.PopBands.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, counts[band], FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return world;
    }

    private static long[] RunDemographics(double dtYears, int simYears)
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(dtYears), [SystemCatalog.Demographics(cfg)]);
        WorldState world = exec.Run(DemographicsWorld(), (int)(simYears / dtYears));
        return [world.PopBands[0].Count.Value, world.PopBands[1].Count.Value, world.PopBands[2].Count.Value];
    }

    [Fact]
    public void DtHalving_DemographicFlows_FirstOrderConvergence()
    {
        // Linear rate×dt integration is explicit Euler: global error is O(dt),
        // so halving dt should roughly HALVE the deviation between successive
        // refinements — that convergence ratio is the property under test, and
        // the measured deviations are the documented characterization of the
        // integration error (they are NOT small at Neolithic dt: with per-year
        // rates up to ~0.077 and dt = 10yr, rate·dt ≈ 0.77 per step).
        //
        // TOLERANCE (documented): the ratio → 2 as dt → 0; at these finite dts
        // second-order curvature and integer remainder quantization widen it,
        // so [1.4, 3.5] is asserted. Runs to the same sim-year horizon.
        const int horizon = 200; // sim-years: 20 / 40 / 80 turns
        long[] atDt10 = RunDemographics(10.0, horizon);
        long[] atDt5 = RunDemographics(5.0, horizon);
        long[] atDt25 = RunDemographics(2.5, horizon);

        long l1Coarse = 0, l1Fine = 0;
        for (int band = 0; band < PopBands.Count; band++)
        {
            l1Coarse += Math.Abs(atDt10[band] - atDt5[band]);
            l1Fine += Math.Abs(atDt5[band] - atDt25[band]);
        }

        Assert.True(l1Coarse > 0, "dt-halving produced no deviation — vacuous run");
        Assert.True(l1Fine > 0, "second halving produced no deviation — vacuous run");
        double ratio = l1Coarse / (double)l1Fine;
        Assert.True(ratio is >= 1.4 and <= 3.5,
            $"convergence ratio {ratio:F2} outside [1.4, 3.5] — not first-order behavior " +
            $"(L1 dt10→5: {l1Coarse}, dt5→2.5: {l1Fine})");

        long total10 = atDt10[0] + atDt10[1] + atDt10[2];
        long total25 = atDt25[0] + atDt25[1] + atDt25[2];
        Console.WriteLine(
            $"dt-halving @ {horizon}yr: dt10 total {total10}, dt2.5 total {total25}, " +
            $"L1(10→5) {l1Coarse}, L1(5→2.5) {l1Fine}, ratio {ratio:F2}");
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
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        for (int t = 1; t <= 200; t++) world = exec.Step(world);

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
            "reconciliation is vacuous — some flow never occurred in 200 turns");
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
