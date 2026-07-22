using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.5 adversarial hardening: per-flow EXACTNESS coverage. The adversarial pass
// proved the behavioral suite has no teeth against wrong-but-ledgered amounts —
// an aging leak laundered through Deaths+Births flows and a dropped death
// remainder both passed all 126 tests (a birth double-count survived all but an
// incidental dt-ratio check). These tests pin every demographic/food flow to an
// independently hand-computed expectation, exact long equality, no epsilon.
public class PopulationExactnessTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One settlement, band counts 33000/50000/17000 endowed via Ledger.</summary>
    private static WorldState BandsWorld(long children = 33000, long adults = 50000, long elders = 17000)
    {
        var world = new WorldState(7);
        var settlement = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(settlement, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        Span<long> counts = [children, adults, elders];
        for (int band = 0; band < PopBands.Count; band++)
        {
            int row = world.PopBands.Add(new PopBandRow(
                settlement, band, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref world.PopBands.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, counts[band], FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return world;
    }

    private static long FlowTotal(WorldState world, ConservedQuantityId quantity, ReasonId reason, bool sunk)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == quantity && row.Reason == reason)
                return sunk ? row.TotalSunk : row.TotalSourced;
        }
        return 0;
    }

    private static long Floor(double v) => (long)Math.Floor(v);

    [Fact]
    public void Demographics_SingleStep_EveryFlowAndBand_HandComputedExact()
    {
        // One demographics step at dt = 10 with a seeded deficit of 0.25 —
        // large enough that starvation flows are nonzero, small enough that NO
        // clamp binds (clamp semantics get their own directed test below).
        // Every flow amount and final band count is re-derived here from config
        // × PREV counts — a doubled birth flow, swapped band rates, mislabeled
        // reasons, or an aging leak all break at least one exact equality.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0, deficit = 0.25;
        const long prevC = 33000, prevA = 50000, prevE = 17000;
        WorldState world = BandsWorld(prevC, prevA, prevE);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), deficit));

        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        WorldState next = exec.Step(world);

        DemographicsConfig d = cfg.Demographics;
        long births = Floor(d.BirthsPerAdultPerYear * prevA * dt);                 // 25000
        long deathsC = Floor(d.ChildMortalityPerYear * prevC * dt);                // 3300
        long deathsA = Floor(d.AdultMortalityPerYear * prevA * dt);                // 3000
        long deathsE = Floor(d.ElderMortalityPerYear * prevE * dt);                // 7650
        double starvRate = d.StarvationMortalityMaxPerYear * deficit;
        long starvC = Floor(starvRate * prevC * dt);                               // 9900
        long starvA = Floor(starvRate * prevA * dt);                               // 15000
        long starvE = Floor(starvRate * prevE * dt);                               // 5100
        long agingCA = Floor(d.AgingChildToAdultPerYear * prevC * dt);             // 22000
        long agingAE = Floor(d.AgingAdultToElderPerYear * prevA * dt);             // 11111

        // Band counts: exact (no clamp binds at these magnitudes — verified by
        // the positive expected finals).
        Assert.Equal(prevC + births - deathsC - starvC - agingCA, next.PopBands[0].Count.Value);
        Assert.Equal(prevA - deathsA - starvA + agingCA - agingAE, next.PopBands[1].Count.Value);
        Assert.Equal(prevE - deathsE - starvE + agingAE, next.PopBands[2].Count.Value);

        // Ledger rows: exact per-reason attribution — and aging appears NOWHERE
        // (it is a Transfer; a Flow-pair regression inflates Births/Deaths and
        // breaks these equalities).
        Assert.Equal(births, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Births, sunk: false));
        Assert.Equal(deathsC + deathsA + deathsE,
            FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: true));
        Assert.Equal(starvC + starvA + starvE,
            FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.Equal(0, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Births, sunk: true));
        Assert.Equal(0, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: false));

        // Remainders: exactly the sub-unit fractions of the same products —
        // every one of them, bit-exact (a dropped remainder writes 0.0 and the
        // true fraction is never exactly 0.0 at these operands).
        double birthExact = d.BirthsPerAdultPerYear * prevA * dt;
        Assert.Equal(birthExact - births, next.PopBands[0].BirthRemainder);
        double agingExact = d.AgingChildToAdultPerYear * prevC * dt;
        Assert.Equal(agingExact - agingCA, next.PopBands[0].AgingRemainder);
        Assert.Equal(d.ChildMortalityPerYear * prevC * dt - deathsC, next.PopBands[0].DeathRemainder);
        Assert.Equal(d.AdultMortalityPerYear * prevA * dt - deathsA, next.PopBands[1].DeathRemainder);
        Assert.Equal(starvRate * prevC * dt - starvC, next.PopBands[0].StarvationRemainder);
    }

    [Fact]
    public void RemainderAccumulation_SubUnitMortality_ProducesDeathsOverTime()
    {
        // The remainder-drop mutant escaped every magnitude test (single-step
        // flow amounts are unchanged when the fraction is discarded). This is
        // the semantic guard: with 10 adults at 0.006/yr and dt = 10, each
        // turn's exact mortality is 0.06 × prevAdults < 1 — deaths EXIST ONLY
        // through remainder accumulation. Hand-walked recurrence (rate applies
        // to the SHRINKING prev count; exact_t = 0.06·prevA + rem):
        //   t1 0.60→0  t2 1.20→1  t3 0.74→0  t4 1.28→1  t5 0.76→0
        //   t6 1.24→1  t7 0.66→0  t8 1.08→1  t9 0.44→0   — 4 deaths, 6 alive.
        // A dropped remainder floors every turn to zero and never kills anyone.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                BirthsPerAdultPerYear = 0.0,
                ChildMortalityPerYear = 0.0,
                ElderMortalityPerYear = 0.0,
                AgingChildToAdultPerYear = 0.0,
                AgingAdultToElderPerYear = 0.0,
                StarvationMortalityMaxPerYear = 0.0,
                // AdultMortalityPerYear stays canonical 0.006.
            },
        };
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = exec.Run(BandsWorld(children: 0, adults: 10, elders: 0), 9);

        Assert.Equal(4, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: true));
        Assert.Equal(6, world.PopBands[1].Count.Value);
    }

    [Fact]
    public void Aging_LeavesNoFlowFootprint_TotalPopulationExactlyConserved()
    {
        // Aging-only config: births, mortality, starvation all zero. Aging is a
        // Ledger.Transfer — people MOVE but never source or sink. Ten steps:
        // the total is exactly constant every step and the Births/Deaths/
        // Starvation ledger totals stay exactly zero. An aging implementation
        // booked as compensating flows (even a balanced pair) fails here.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                BirthsPerAdultPerYear = 0.0,
                ChildMortalityPerYear = 0.0,
                AdultMortalityPerYear = 0.0,
                ElderMortalityPerYear = 0.0,
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = BandsWorld();
        const long total = 33000 + 50000 + 17000;

        for (int t = 1; t <= 10; t++)
        {
            world = exec.Step(world);
            long sum = world.PopBands[0].Count.Value + world.PopBands[1].Count.Value
                       + world.PopBands[2].Count.Value;
            Assert.Equal(total, sum); // exact, every step
            foreach (ReasonId reason in new[] { ReasonIds.Births, ReasonIds.Deaths, ReasonIds.Starvation })
            {
                Assert.Equal(0, FlowTotal(world, ConservedQuantityIds.Population, reason, sunk: false));
                Assert.Equal(0, FlowTotal(world, ConservedQuantityIds.Population, reason, sunk: true));
            }
        }

        // Aging really ran: children shrank, elders grew.
        Assert.True(world.PopBands[0].Count.Value < 33000, "children never aged out");
        Assert.True(world.PopBands[2].Count.Value > 17000, "elders never aged in");
    }

    [Fact]
    public void ClampShortfall_NeverBanked_StarvedBandOwesNoFutureDeaths()
    {
        // Famine floor semantics: requested starvation deaths exceed the band —
        // the band clamps to exactly 0 and the shortfall is DISCARDED, not
        // banked. After repopulating, the next step sinks only the newly
        // computed amount. (A banking regression would carry the phase-1
        // shortfall into phase 2's flow.)
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
            },
        };
        const double dt = 10.0;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        WorldState world = BandsWorld(children: 5, adults: 0, elders: 0);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 1.0));

        // Phase 1: requested = floor(0.12 × 1.0 × 5 × 10) = 6 > 5 → clamp to 5.
        world = exec.Step(world);
        Assert.Equal(0, world.PopBands[0].Count.Value);
        Assert.Equal(5, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.True(world.PopBands[0].StarvationRemainder is >= 0.0 and < 1.0,
            $"shortfall banked: remainder {world.PopBands[0].StarvationRemainder}");

        // Phase 2: repopulate to 10, soften the deficit to 0.25 →
        // requested = floor(0.12 × 0.25 × 10 × 10) = 3. The Starvation total
        // must rise by EXACTLY 3 — no phase-1 debt executes against the living.
        var ledger = new Ledger(world.LedgerFlows);
        ledger.Flow(ref world.PopBands.Ref(0).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 10, FlowDirection.Source, OverdrawPolicy.Throw);
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), 0.25);
        world = exec.Step(world);
        Assert.Equal(5 + 3, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.Equal(7, world.PopBands[0].Count.Value);
    }

    // --- food-loop exactness + dt-correctness -------------------------------

    /// <summary>One settlement, a fixed catchment summary (farmland F), an
    /// endowed store, and static band counts 100/200/50.</summary>
    private static WorldState FoodWorld(double farmland, long store)
    {
        WorldState world = BandsWorld(children: 100, adults: 200, elders: 50);
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(0), NodeCount: 1, EffectiveFarmland: farmland,
            NetworkRevision: 0, LastRecomputeTurn: 0));
        int row = world.FoodStores.Add(new FoodStoreRow(
            new SettlementId(0), Conserved.Zero, 0.0, 0.0));
        new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(row).Store,
            ConservedQuantityIds.Food, ReasonIds.InitialEndowment, store,
            FlowDirection.Source, OverdrawPolicy.Throw);
        return world;
    }

    [Fact]
    public void FarmingAndConsumption_SingleStep_HandComputedExact()
    {
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0, farmland = 78.5;
        const long endow = 10000;
        var exec = new TurnExecutor(FlatEra(dt),
            [SystemCatalog.Farming(cfg), SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(FoodWorld(farmland, endow));

        // T1.6: farm share is the LaborAllocations row; this hand-built world
        // has none, so the never-ordered default of 1.0 applies.
        long harvest = Floor(farmland * cfg.Farming.YieldPerFarmlandPerYear * dt); // 21980 @ yield 28
        long demand = Floor((cfg.Consumption.ChildWeight * 100
                             + cfg.Consumption.AdultWeight * 200
                             + cfg.Consumption.ElderWeight * 50) * dt);            // 2950

        Assert.Equal(harvest, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Harvest, sunk: false));
        Assert.Equal(demand, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true));
        Assert.Equal(endow + harvest - demand, next.FoodStores[0].Store.Value);
        Assert.Equal(0.0, next.ConsumptionDeficits[0].DeficitRatio);
    }

    [Fact]
    public void Consumption_Clamp_StoreToExactZero_DeficitRatioExact()
    {
        // No farming in the pipeline; a small store. Demand = 2950 > 1000 →
        // eats exactly 1000, store EXACTLY 0, deficit exactly (2950−1000)/2950.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(FoodWorld(farmland: 0.0, store: 1000));

        long demand = Floor((cfg.Consumption.ChildWeight * 100
                             + cfg.Consumption.AdultWeight * 200
                             + cfg.Consumption.ElderWeight * 50) * dt);
        Assert.Equal(0, next.FoodStores[0].Store.Value);
        Assert.Equal(1000, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true));
        Assert.Equal((demand - 1000) / (double)demand, next.ConsumptionDeficits[0].DeficitRatio);
    }

    [Fact]
    public void FarmingAndConsumption_DtCorrect_EqualHorizonTotalsAgreeAcrossDt()
    {
        // Law-3 designed test (adversarial finding: every prior test ran the
        // food loop at dt = 10 exactly, so a hardcoded per-turn amount was
        // invisible). Harvest and demand are LINEAR in dt with telescoping
        // remainders: cumulative flow to the same sim-year horizon must agree
        // across dt within 1 unit (IEEE summation drift across different step
        // counts), and match rate × horizon within 1. A dropped or doubled
        // DtYears in either system shifts totals by ~2× and fails loudly.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 200;
        const double farmland = 78.5;
        double harvestPerYear = farmland * cfg.Farming.YieldPerFarmlandPerYear;    // 2198.0/yr (share 1.0 default)
        double demandPerYear = cfg.Consumption.ChildWeight * 100
                               + cfg.Consumption.AdultWeight * 200
                               + cfg.Consumption.ElderWeight * 50;                 // 295.0/yr

        var harvested = new long[3];
        var eaten = new long[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            var exec = new TurnExecutor(FlatEra(dts[i]),
                [SystemCatalog.Farming(cfg), SystemCatalog.Consumption(cfg)]);
            // Store large enough that the clamp never binds at any dt.
            WorldState world = exec.Run(FoodWorld(farmland, store: 1_000_000),
                (int)(horizonYears / dts[i]));
            harvested[i] = FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.Harvest, sunk: false);
            eaten[i] = FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true);
        }

        for (int i = 1; i < dts.Length; i++)
        {
            Assert.True(Math.Abs(harvested[i] - harvested[0]) <= 1,
                $"harvest not dt-linear: {harvested[0]} @dt10 vs {harvested[i]} @dt{dts[i]}");
            Assert.True(Math.Abs(eaten[i] - eaten[0]) <= 1,
                $"consumption not dt-linear: {eaten[0]} @dt10 vs {eaten[i]} @dt{dts[i]}");
        }
        Assert.True(Math.Abs(harvested[0] - (long)(harvestPerYear * horizonYears)) <= 1);
        Assert.True(Math.Abs(eaten[0] - (long)(demandPerYear * horizonYears)) <= 1);
    }

    // --- founding endowment pinning -----------------------------------------

    [Fact]
    public void Founding_EndowmentPinnedToConfig_StocksAndLedgerRowsExact()
    {
        // Adversarial finding: nothing pinned the founding amounts — a double
        // endowment or band swap passed everything. Exact pins, config-derived.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed: 42);

        Assert.Equal(PopBands.Count, world.PopBands.Count);
        Assert.Equal(PopBands.Children, world.PopBands[0].Band);
        Assert.Equal(cfg.Founding.Children, world.PopBands[0].Count.Value);
        Assert.Equal(cfg.Founding.Adults, world.PopBands[1].Count.Value);
        Assert.Equal(cfg.Founding.Elders, world.PopBands[2].Count.Value);
        Assert.Equal(1, world.FoodStores.Count);
        Assert.Equal(cfg.Founding.FoodStore, world.FoodStores[0].Store.Value);
        Assert.Equal(0, world.ConsumptionDeficits.Count);

        long popTotal = cfg.Founding.Children + cfg.Founding.Adults + cfg.Founding.Elders;
        Assert.Equal(popTotal,
            FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: false));
        Assert.Equal(cfg.Founding.FoodStore,
            FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.InitialEndowment, sunk: false));
        Assert.Equal(0,
            FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: true));
    }
}
