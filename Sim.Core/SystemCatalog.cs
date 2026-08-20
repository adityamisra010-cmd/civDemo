using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Harvest;
using Sim.Core.Systems.Housing;
using Sim.Core.Systems.Price;
using Sim.Core.Systems.Production;
using Sim.Core.Systems.Growth;
using Sim.Core.Systems.Migration;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Systems.Trade;
using Sim.Core.Systems.Weather;

namespace Sim.Core;

/// <summary>
/// The composition root for systems — THE single place where owned tables are
/// handed out (§3.1 ownership by construction, ADR-003). Each registration builds
/// that system's typed context with writable handles to its own Next tables and
/// nothing else; systems never see a writable WorldState. Any new system's
/// ownership claim lands here, reviewable at a glance.
/// The executor and pipeline loader consume these registrations generically.
///
/// SANCTIONED SHARED STOCK (T1.5; T3.2 the FoodStore migrated into the GRAIN
/// row of GoodStocks; RE-RECORDED at T3.3 when Farming became Production).
/// GoodStocks is handed to BOTH Production and Consumption. A stock that one
/// system fills and another drains cannot have a single writer; both mutations
/// go exclusively through the Ledger (law 1) and the per-turn audit holds the
/// pair to exactness. This paragraph is the reviewable record of that share, so
/// it states the split at FIELD level:
///
///   PRODUCTION owns, on every row it touches: Amount via Ledger (reasons
///     Harvest for grain, Produced for everything else, InputsConsumed for
///     recipe inputs, ToolWear for farm-tool depreciation), ProduceRemainder on
///     every produced row, LastProducedUnits on every row (zeroed each step),
///     and ConsumeRemainder on the TOOLS row and on every RECIPE-INPUT row.
///   CONSUMPTION owns: Amount via Ledger (reason Eaten) and ConsumeRemainder on
///     the GRAIN row.
///   HOUSING (T3.8, the FOURTH holder) owns: Amount via Ledger SINK only, reason
///     HousingMaterials, on the TIMBER and CLAY rows (build + upkeep draws) —
///     never a source, never another good, no remainder field touched.
///   TRADE (T3.6, the third holder) owns: Amount via Ledger.TRANSFER ONLY —
///     conserving cross-settlement moves within a good, never a source or
///     sink, and NO remainder field (whole units only; sub-unit intent is
///     dropped, not banked — a banked trade remainder would be new serialized
///     state the mandate does not ask for). Trade touches no other field.
///
/// THE COLLISION THIS RECORD EXISTS TO CATCH (T3.3 adversarial finding — the
/// paragraph had gone stale and still named Farming, mis-assigning
/// ConsumeRemainder wholly to Consumption): no shipped recipe consumes grain
/// today, so the two ConsumeRemainder owners never meet. THE FIRST RECIPE THAT
/// TAKES GRAIN AS AN INPUT — a T3.5 food basket, a brewing recipe, or a
/// data-only goods.json edit — puts two systems on one accumulator with two
/// different meanings, and each turn's carry would clobber the other's. Whoever
/// adds that recipe must split the field or serialize the two writers.
/// </summary>
public static class SystemCatalog
{
    public static SystemRegistration Catchment(SimConfig cfg)
    {
        var system = new CatchmentSystem(cfg);
        return new SystemRegistration(CatchmentSystem.WellKnownId, CatchmentSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<CatchmentTables>(
                prev, new CatchmentTables(next.CatchmentNodes, next.CatchmentSummaries,
                    next.SettlementDistances), rng,
                CatchmentSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Weather()
    {
        var system = new WeatherSystem();
        return new SystemRegistration(WeatherSystem.WellKnownId, WeatherSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<WeatherTables>(
                prev, new WeatherTables(next.Rainfall), rng, WeatherSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Growth()
    {
        var system = new GrowthSystem();
        return new SystemRegistration(GrowthSystem.WellKnownId, GrowthSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<GrowthTables>(
                prev, new GrowthTables(next.Biomass), rng, GrowthSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Trade()
    {
        var system = new TradeSystem();
        return new SystemRegistration(TradeSystem.WellKnownId, TradeSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<TradeTables>(
                prev, new TradeTables(next.Goods), rng, TradeSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Production(SimConfig cfg)
    {
        var system = new ProductionSystem(cfg);
        return new SystemRegistration(ProductionSystem.WellKnownId, ProductionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ProductionTables>(
                prev, new ProductionTables(next.GoodStocks), rng, ProductionSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T4.5 (D-037 B3): stateless settlements appropriate grain when their
    /// own subsistence fails. Owns no tables; moves grain between existing stock
    /// rows via Ledger.Transfer, which conserves by construction.</summary>
    public static SystemRegistration Appropriation(SimConfig cfg)
    {
        var system = new Systems.Appropriation.AppropriationSystem(cfg);
        return new SystemRegistration(
            Systems.Appropriation.AppropriationSystem.WellKnownId,
            Systems.Appropriation.AppropriationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(
                new SimContext<Systems.Appropriation.AppropriationTables>(
                    prev, new Systems.Appropriation.AppropriationTables(next.GoodStocks), rng,
                    Systems.Appropriation.AppropriationSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T4.13: household goods — Comfort's stock, worn by USE and
    /// replenished by crafting. Owns the HouseholdGoods table and SINKS from the
    /// shared GoodStocks (the materials crafting consumes).</summary>
    public static SystemRegistration HouseholdGoods(SimConfig cfg)
    {
        var system = new Systems.HouseholdGoods.HouseholdGoodsSystem(cfg);
        return new SystemRegistration(
            Systems.HouseholdGoods.HouseholdGoodsSystem.WellKnownId,
            Systems.HouseholdGoods.HouseholdGoodsSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(
                new SimContext<Systems.HouseholdGoods.HouseholdGoodsTables>(
                    prev, new Systems.HouseholdGoods.HouseholdGoodsTables(
                        next.HouseholdGoods, next.GoodStocks), rng,
                    Systems.HouseholdGoods.HouseholdGoodsSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Consumption(SimConfig cfg)
    {
        var system = new ConsumptionSystem(cfg);
        return new SystemRegistration(ConsumptionSystem.WellKnownId, ConsumptionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ConsumptionTables>(
                prev, new ConsumptionTables(next.GoodStocks, next.ConsumptionDeficits), rng,
                ConsumptionSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration HarvestWeather(SimConfig cfg)
    {
        var system = new HarvestWeatherSystem(cfg);
        return new SystemRegistration(HarvestWeatherSystem.WellKnownId, HarvestWeatherSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<HarvestWeatherTables>(
                prev, new HarvestWeatherTables(next.HarvestWeather), rng,
                HarvestWeatherSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Price(SimConfig cfg)
    {
        var system = new PriceSystem(cfg);
        return new SystemRegistration(PriceSystem.WellKnownId, PriceSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<PriceTables>(
                prev, new PriceTables(next.Prices, next.PriceTerms), rng,
                PriceSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T3.6 (D-034): the arbitrage system — name "trade"; the retired
    /// M0 toy is "toytrade" (director decision 3, 2026-07-28; the roster guard
    /// in PipelineLoader refuses any duplicate). Third holder of the GoodStocks
    /// share — see the ownership record above.</summary>
    public static SystemRegistration TradeArbitrage(SimConfig cfg)
    {
        var system = new TradeArbitrageSystem(cfg);
        return new SystemRegistration(TradeArbitrageSystem.WellKnownId, TradeArbitrageSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<TradeArbitrageTables>(
                prev, new TradeArbitrageTables(next.GoodStocks, next.TradeFlows), rng,
                TradeArbitrageSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T3.8: the housing stock — fourth holder of the GoodStocks share
    /// (see the ownership record above).</summary>
    public static SystemRegistration Housing(SimConfig cfg)
    {
        var system = new HousingSystem(cfg);
        return new SystemRegistration(HousingSystem.WellKnownId, HousingSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<HousingTables>(
                prev, new HousingTables(next.Housing, next.GoodStocks), rng,
                HousingSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Demographics(SimConfig cfg)
    {
        var system = new DemographicsSystem(cfg);
        return new SystemRegistration(DemographicsSystem.WellKnownId, DemographicsSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<DemographicsTables>(
                prev, new DemographicsTables(next.Buckets, next.SettlementVitals), rng,
                DemographicsSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// T2.6 table communication (law 6, reviewable record): NeedsGrievance
    /// reads the PREV SettlementVitals chronicle that Demographics writes (the
    /// D-021 generational-turnover input) — a single-writer table read across
    /// a turn boundary, not a shared stock; no sanction needed. Its OWN tables
    /// (NeedSatisfactions, Grievances) are read by nothing but UI/chronicle —
    /// the CI read-isolation grep enforces that with an allowlist.
    /// </summary>
    public static SystemRegistration NeedsGrievance(SimConfig cfg)
    {
        var system = new NeedsGrievanceSystem(cfg);
        return new SystemRegistration(NeedsGrievanceSystem.WellKnownId, NeedsGrievanceSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<NeedsGrievanceTables>(
                prev, new NeedsGrievanceTables(next.NeedSatisfactions, next.Grievances), rng,
                NeedsGrievanceSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// SANCTIONED SHARED STOCK (T2.2): Buckets is handed to BOTH Demographics
    /// (births/deaths/starvation/aging; owns Birth/Death/Starvation/Aging
    /// remainders) and ClassMobility (same-cohort adult class transfers; owns
    /// MobilityRemainder). Every mutation goes exclusively through the Ledger
    /// (law 1) and the per-turn audit holds the pair to exactness — the same
    /// reviewable pattern as the T1.5 FoodStores share above.
    /// </summary>
    public static SystemRegistration ClassMobility(SimConfig cfg)
    {
        var system = new ClassMobilitySystem(cfg);
        return new SystemRegistration(ClassMobilitySystem.WellKnownId, ClassMobilitySystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ClassMobilityTables>(
                prev, new ClassMobilityTables(next.Buckets, next.Variables, next.ClassStates), rng,
                ClassMobilitySystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// SANCTIONED SHARED STOCK (T2.5): Buckets is now handed to THREE systems —
    /// Demographics, ClassMobility (see above), and Migration (cross-settlement
    /// same-key Ledger.Transfers; owns MigrationRemainder). Same discipline:
    /// every mutation through the Ledger (law 1), per-turn audit exact.
    /// </summary>
    public static SystemRegistration Migration(SimConfig cfg)
    {
        var system = new MigrationSystem(cfg);
        return new SystemRegistration(MigrationSystem.WellKnownId, MigrationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<MigrationTables>(
                prev, new MigrationTables(next.Buckets, next.MigrationFlows,
                    next.SmoothedAttractiveness), rng,
                MigrationSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// T4.4 colonization. Takes the WORLDGEN config as well, because frontier
    /// siting is a terrain question and `SitingConfig` (the score floor, the
    /// jitter and ADR-018's `minSpacingKm`) lives there. It is OPTIONAL: toy and
    /// hand-built worlds have no terrain and no worldgen config, and the system
    /// no-ops for them, so `pipeline.json` stays valid everywhere.
    ///
    /// `next.Settlements` is a NEW ownership grant — before T4.4 no system could
    /// append a settlement and the table was immutable for the whole simulation.
    /// </summary>
    public static SystemRegistration Colonization(SimConfig cfg, Worldgen.WorldgenConfig? worldgen)
    {
        var system = worldgen is null ? null
            : new Systems.Colonization.ColonizationSystem(cfg, worldgen);
        return new SystemRegistration(
            Systems.Colonization.ColonizationSystem.WellKnownId,
            Systems.Colonization.ColonizationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system?.Step(
                new SimContext<Systems.Colonization.ColonizationTables>(
                    prev, new Systems.Colonization.ColonizationTables(
                        next.Settlements, next.Buckets, next.GoodStocks, next.Deposits,
                        next.ClassStates, next.Grievances, next.SmoothedAttractiveness), rng,
                    Systems.Colonization.ColonizationSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration PathBuild(SimConfig cfg)
    {
        var system = new PathBuildSystem(cfg);
        return new SystemRegistration(PathBuildSystem.WellKnownId, PathBuildSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<PathBuildTables>(
                prev, new PathBuildTables(next.SectorAllocations, next.PathProgress,
                    next.NetworkNodes, next.NetworkEdges, next.NetworkMeta), rng,
                PathBuildSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// All systems that exist at the current milestone — M1 production systems
    /// first, retired T0.x toys last (still registered: the toy preset and the
    /// kernel-invariant tests keep running them).
    /// </summary>
    public static SystemRegistration[] All(SimConfig cfg, Worldgen.WorldgenConfig? worldgen = null) =>
        [Catchment(cfg), HarvestWeather(cfg), Production(cfg), Appropriation(cfg), Consumption(cfg), HouseholdGoods(cfg), Price(cfg), TradeArbitrage(cfg),
         Housing(cfg), ClassMobility(cfg), Migration(cfg), Colonization(cfg, worldgen), Demographics(cfg), NeedsGrievance(cfg), PathBuild(cfg),
         Weather(), Growth(), Trade()];
}
