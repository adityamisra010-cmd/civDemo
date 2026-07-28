using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.2b — THE EQUILIBRIUM DENSITY INVARIANT, pinned.
///
/// <code>
///     equilibrium density = YieldPerArableKm2PerYear ÷ meanConsumptionPerPersonPerYear
/// </code>
/// in people per FERTILITY-WEIGHTED arable km². It follows in two lines from
/// the food balance: at a stationary population on a land-bound catchment,
/// harvest = arable × yield and consumption = population × meanConsumption, and
/// the two are equal, so population/arable = yield/meanConsumption.
///
/// WHAT IT SAYS, and why it is worth a test rather than a comment. The
/// equilibrium density of the world is fixed ENTIRELY by two tuning constants.
/// It does not depend on the catchment radius, the lattice stride, the block
/// area, the world size, the number of settlements, the fertility distribution,
/// or the settlement-siting rule — every one of those changes how MANY people
/// there are, none of them changes how DENSELY they sit. That is why the
/// density corridor is an instrument pointed at the yield constant and at
/// nothing else, and why the pre-T3.2b attempt to move the measured density by
/// correcting the travel budget did nothing: population simply adapted.
///
/// It is also why the CR-002 denomination bug was invisible for three
/// milestones. In the old (per-node) denomination the same identity read
/// density = yield ÷ (blockKm² × meanConsumption) — the block area appeared in
/// what should be a purely agronomic quantity, and nobody asked why a geometric
/// constant was setting how many people a hectare feeds.
///
/// PRECONDITIONS. The identity is a fixed-point statement and holds only where
/// all of these hold; each is checked or stated below:
///  1. LAND-BOUND. The Leontief min() resolves on the land side —
///     adults × farmShare × outputPerFarmer × toolMultiplier ≥ arable × yield.
///     Pinned by <see cref="Invariant_Precondition_LandSideBinds_AtThePredictedDensity"/>.
///     Below equilibrium the labour side can bind (the frontier regime); the
///     invariant describes where the trajectory ENDS, not where it travels.
///  2. STATIONARY POPULATION — births = deaths + starvation. A growing or
///     collapsing world is off the fixed point by construction.
///  3. STATIONARY STORE — the grain stock neither accumulates nor drains on
///     average, so harvest = consumption over the averaging window.
///  4. NO IMPORT OR EXPORT of grain. True until trade lands at T3.6; from then
///     on the identity is world-total, not per-settlement.
///  5. meanConsumption is the cohort-weight average over the EQUILIBRIUM age
///     structure (Σ w_c n_c / Σ n_c), not over the founding one — a younger or
///     older world eats differently per head.
///  6. Catchments PARTITION (T2.3), so summing arable across settlements
///     double-counts no land.
///  7. Whole-unit conversion. Harvest and consumption become long units through
///     D-004 remainder accumulators, so the identity is exact in the limit and
///     within one unit per turn otherwise. The balance tests below choose
///     quantities that land on whole units so they can assert exactly.
/// </summary>
public class EquilibriumInvariantTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static readonly GoodId Grain = new(1);

    /// <summary>The invariant itself, as one expression — the thing under test.</summary>
    private static double PredictedDensity(double yieldPerArableKm2PerYear, double meanConsumption) =>
        yieldPerArableKm2PerYear / meanConsumption;

    /// <summary>
    /// A world of <paramref name="scale"/> × (100 children, 200 adults, 50
    /// elders) on <paramref name="arableKm2"/> of fertility-weighted land, with
    /// a store big enough that no clamp can hide a shortfall.
    /// Cohort weights 0.6 / 1.0 / 0.7 ⇒ demand 295·scale per year and
    /// meanConsumption = 295/350 = 0.842857… per head per year.
    /// </summary>
    private static WorldState EquilibriumWorld(long scale, double arableKm2, long store)
    {
        var counts = new long[Cohorts.Count];
        counts[0] = 100 * scale; counts[5] = 200 * scale; counts[15] = 50 * scale;
        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(0), NodeCount: 1, EffectiveArableKm2: arableKm2,
            NetworkRevision: 0, LastRecomputeTurn: 0));
        int row = world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(0), Grain, Conserved.Zero, 0.0, 0.0));
        new Ledger(world.LedgerFlows).Flow(ref world.GoodStocks.Ref(row).Amount,
            ConservedQuantityIds.OfGood(Grain), ReasonIds.InitialEndowment, store,
            FlowDirection.Source, OverdrawPolicy.Throw);
        return world;
    }

    private const double MeanConsumption = 295.0 / 350.0;

    /// <summary>Yield overridden to an exact binary fraction so the balance
    /// lands on whole units and can be asserted with no epsilon. The CANONICAL
    /// yield's consequences are pinned by the calibration corridors; what is
    /// under test here is the IDENTITY, which must hold at any yield.</summary>
    private static SimConfig WithYield(double yieldPerArableKm2PerYear) =>
        TestConfigs.Sim() is { } c
            ? c with { Farming = c.Farming with { YieldPerArableKm2PerYear = yieldPerArableKm2PerYear } }
            : throw new InvalidOperationException();

    [Fact]
    public void Invariant_AtThePredictedDensity_HarvestEqualsConsumption_Exactly()
    {
        // 350 people; the invariant says they sit on arable = P / density
        // = 350 / (2.0 / 0.842857…) = 147.5 fertility-weighted km².
        const double yield = 2.0;
        double density = PredictedDensity(yield, MeanConsumption);
        Assert.Equal(2.0 / (295.0 / 350.0), density);

        const long population = 350;
        double arableKm2 = population / density;
        Assert.Equal(147.5, arableKm2, 12);

        SimConfig cfg = WithYield(yield);
        const double dt = 10.0;
        const long store = 1_000_000;                      // far from any clamp
        var exec = new TurnExecutor(FlatEra(dt),
            [SystemCatalog.Production(cfg), SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(EquilibriumWorld(scale: 1, arableKm2, store));

        long harvest = FlowTotal(next, ReasonIds.Harvest, sunk: false);
        long eaten = FlowTotal(next, ReasonIds.Eaten, sunk: true);

        // 147.5 × 2.0 = 295.0/yr harvested; 295.0/yr eaten. Over dt = 10:
        // 2950 each way, and the store is exactly where it started.
        Assert.Equal(2950, harvest);
        Assert.Equal(2950, eaten);
        Assert.Equal(store, next.GoodStocks[0].Amount.Value);
        Assert.Equal(0.0, next.ConsumptionDeficits[0].DeficitRatio);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(4L)]
    [InlineData(37L)]
    public void Invariant_IsIndependentOf_CatchmentSize_And_Population(long scale)
    {
        // The SAME density balances at any catchment size: scale the people and
        // the land together and the books still close to the unit. This is the
        // geometry-independence claim — a catchment radius, a lattice stride or
        // a block area changes how many people the world holds, never how
        // densely they sit. The pre-T3.2b formula had blockKm² in it precisely
        // because the denomination bug put a geometric constant where only
        // agronomy belongs.
        const double yield = 2.0;
        double density = PredictedDensity(yield, MeanConsumption);
        long population = 350 * scale;
        double arableKm2 = population / density;

        SimConfig cfg = WithYield(yield);
        const double dt = 10.0;
        const long store = 100_000_000;
        var exec = new TurnExecutor(FlatEra(dt),
            [SystemCatalog.Production(cfg), SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(EquilibriumWorld(scale, arableKm2, store));

        Assert.Equal(2950 * scale, FlowTotal(next, ReasonIds.Harvest, sunk: false));
        Assert.Equal(2950 * scale, FlowTotal(next, ReasonIds.Eaten, sunk: true));
        Assert.Equal(store, next.GoodStocks[0].Amount.Value);

        // And the density actually realized is the predicted one, exactly.
        Assert.Equal(density, population / arableKm2, 12);
    }

    [Fact]
    public void Invariant_Precondition_LandSideBinds_AtThePredictedDensity()
    {
        // PRECONDITION 1, with the CANONICAL constants. At the predicted
        // density the labour side must not be the binding one, or the world
        // settles somewhere else entirely and the corridor stops measuring the
        // yield constant.
        //
        // The ratio is yield-free, which is the point: at a land-bound
        // equilibrium harvest = meanConsumption × P, while labour capacity is
        // adultShare × farmShare × outputPerFarmer × toolMultiplier × P. Raising
        // the yield raises the equilibrium population, which raises BOTH sides
        // in the same proportion. So whether land binds is decided by the
        // labour parameters alone and holds at every yield — including the
        // wrong pre-T3.2b one, which is why the denomination error never showed
        // up as a regime change.
        SimConfig cfg = TestConfigs.Sim();
        const double adultShare = 200.0 / 350.0;   // the fixture's age structure
        const double farmShare = 1.0;              // never-ordered default
        const double toolMultiplier = 1.0;         // no artisans → no tool bonus
        double labourPerHead =
            adultShare * farmShare * cfg.Farming.OutputPerFarmerPerYear * toolMultiplier;

        Assert.True(labourPerHead > MeanConsumption,
            $"labour capacity per head {labourPerHead} ≤ consumption per head {MeanConsumption}: "
            + "the equilibrium is LABOUR-bound, so the density invariant does not hold and the "
            + "density corridor no longer measures farming.yieldPerArableKm2PerYear. Re-derive "
            + "before re-tuning farming.outputPerFarmerPerYear downward.");

        // Recorded, not asserted as a corridor: how much slack there is.
        // At the canonical 5.0 output the labour side has ~3.4× headroom.
        Assert.True(labourPerHead / MeanConsumption > 3.0);
    }

    [Fact]
    public void Denomination_CatchmentArable_IsBlockAreaTimesMeanFertility_NotNodeCount()
    {
        // CR-002 teeth, on a real founded world. EffectiveArableKm2 divided by
        // the RAW catchment area must be a mean fertility: a number in (0,1],
        // and specifically the mean of BlockMeanFertility over the owned nodes.
        //
        // A MISSING conversion makes the ratio meanFertility/256 ≈ 0.002; a
        // DOUBLED one makes it 256×meanFertility ≈ 135. Both fail. The bare
        // equality against the recomputed mean would also pass if BOTH the
        // system and the test were wrong in the same way, so the (0,1] bound
        // and the "settled catchments are not barren" floor are asserted
        // independently of it.
        WorldgenConfig wcfg;
        using (var stream = Sim.Data.DataFiles.OpenWorldgen())
            wcfg = WorldgenConfigLoader.Load(stream) is { } c
                ? c with { SizePx = 256, Siting = c.Siting with { SettlementCount = 4 } }
                : throw new InvalidOperationException();
        SimConfig cfg = TestConfigs.Sim();
        EraTable era;
        using (var stream = Sim.Data.DataFiles.OpenEraPacing()) era = EraTableLoader.Load(stream);

        WorldState world = new TurnExecutor(era, [SystemCatalog.Catchment(cfg)])
            .Step(WorldFounding.Found(wcfg, cfg, seed: 42));
        TraversalLattice lattice = TraversalLattice.Build(world.Terrain!);
        double blockAreaKm2 = LatticeGeometry.BlockAreaKm2(lattice);

        Assert.True(world.CatchmentSummaries.Count > 0, "no catchments — test vacuous");
        for (int s = 0; s < world.CatchmentSummaries.Count; s++)
        {
            CatchmentSummaryRow summary = world.CatchmentSummaries[s];
            double rawAreaKm2 = summary.NodeCount * blockAreaKm2;
            double impliedMeanFertility = summary.EffectiveArableKm2 / rawAreaKm2;

            Assert.InRange(impliedMeanFertility, 0.05, 1.0);

            // …and it IS the mean of the per-node index, over exactly the
            // nodes this settlement owns.
            double sum = 0.0;
            int owned = 0;
            for (int i = 0; i < world.CatchmentNodes.Count; i++)
            {
                if (world.CatchmentNodes[i].Settlement != summary.Settlement) continue;
                sum += LatticeMap.BlockMeanFertility(
                    world.Terrain!, lattice, world.CatchmentNodes[i].LatticeNode);
                owned++;
            }
            Assert.Equal(summary.NodeCount, owned);
            Assert.Equal(sum / owned, impliedMeanFertility, 9);
        }
    }

    [Fact]
    public void Denomination_TravelBudget_IsTheTuneRadius_ThroughTheOneConversion()
    {
        // The budget the pathfinder actually receives must be the TUNE radius
        // in ideal-ground km, no more and no less. A stray unit change here is
        // how the old code constant drifted from anything physical.
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wcfg;
        using (var stream = Sim.Data.DataFiles.OpenWorldgen())
            wcfg = WorldgenConfigLoader.Load(stream) is { } c
                ? c with { SizePx = 256 } : throw new InvalidOperationException();
        TraversalLattice lattice = TraversalLattice.Build(Sim.Core.Worldgen.Worldgen.Generate(wcfg, seed: 42));

        double budget = CatchmentSystem.TravelBudgetCostUnits(cfg, lattice);
        Assert.Equal(
            cfg.Catchment.HinterlandRadiusKm,
            LatticeGeometry.IdealGroundKmForCostUnits(lattice, budget), 9);

        // And the round trip is the identity, so no caller can be off by a
        // block area (the CR-002 failure mode, one dimension down).
        Assert.Equal(budget,
            LatticeGeometry.CostUnitsForIdealGroundKm(
                lattice, LatticeGeometry.IdealGroundKmForCostUnits(lattice, budget)), 12);
    }

    private static long FlowTotal(WorldState world, ReasonId reason, bool sunk)
    {
        ConservedQuantityId quantity = ConservedQuantityIds.OfGood(Grain);
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == quantity && row.Reason == reason)
                return sunk ? row.TotalSunk : row.TotalSourced;
        }
        return 0;
    }
}
