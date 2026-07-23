using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Farming;
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
/// SANCTIONED SHARED STOCK (T1.5): FoodStores is handed to BOTH Farming (credits
/// via Ledger reason Harvest; owns HarvestRemainder) and Consumption (debits via
/// reason Eaten; owns EatenRemainder). A stock that one system fills and another
/// drains cannot have a single writer; both mutations go exclusively through the
/// Ledger (law 1) and the per-turn audit holds the pair to exactness. This
/// paragraph is the reviewable record of that share.
/// </summary>
public static class SystemCatalog
{
    public static SystemRegistration Catchment()
    {
        var system = new CatchmentSystem();
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

    public static SystemRegistration Farming(SimConfig cfg)
    {
        var system = new FarmingSystem(cfg);
        return new SystemRegistration(FarmingSystem.WellKnownId, FarmingSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<FarmingTables>(
                prev, new FarmingTables(next.FoodStores), rng, FarmingSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Consumption(SimConfig cfg)
    {
        var system = new ConsumptionSystem(cfg);
        return new SystemRegistration(ConsumptionSystem.WellKnownId, ConsumptionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ConsumptionTables>(
                prev, new ConsumptionTables(next.FoodStores, next.ConsumptionDeficits), rng,
                ConsumptionSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
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

    public static SystemRegistration PathBuild(SimConfig cfg)
    {
        var system = new PathBuildSystem(cfg);
        return new SystemRegistration(PathBuildSystem.WellKnownId, PathBuildSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<PathBuildTables>(
                prev, new PathBuildTables(next.LaborAllocations, next.PathProgress,
                    next.NetworkNodes, next.NetworkEdges, next.NetworkMeta), rng,
                PathBuildSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// All systems that exist at the current milestone — M1 production systems
    /// first, retired T0.x toys last (still registered: the toy preset and the
    /// kernel-invariant tests keep running them).
    /// </summary>
    public static SystemRegistration[] All(SimConfig cfg) =>
        [Catchment(), Farming(cfg), Consumption(cfg), ClassMobility(cfg), Migration(cfg),
         Demographics(cfg), NeedsGrievance(cfg), PathBuild(cfg), Weather(), Growth(), Trade()];
}
