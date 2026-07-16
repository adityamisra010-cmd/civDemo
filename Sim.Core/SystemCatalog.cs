using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Growth;
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
/// </summary>
public static class SystemCatalog
{
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

    /// <summary>All systems that exist at the current milestone.</summary>
    public static SystemRegistration[] All() => [Weather(), Growth(), Trade()];
}
