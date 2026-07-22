using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Farming;
using Sim.Core.Systems.Growth;
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
                prev, new CatchmentTables(next.CatchmentNodes, next.CatchmentSummaries), rng,
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
                prev, new DemographicsTables(next.Buckets), rng, DemographicsSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
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
        [Catchment(), Farming(cfg), Consumption(cfg), Demographics(cfg), PathBuild(cfg),
         Weather(), Growth(), Trade()];
}
