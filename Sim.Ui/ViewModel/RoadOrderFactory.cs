using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-032 / ADR-033 D2 — the player's ROAD-DEVELOPMENT order ("modernize X% of eligible inter-city
/// transport demand"). Pure and read-only; transport is FROZEN (997824b) and nothing here decides a road
/// rule. The order is exactly <see cref="RoadDevelopmentQuery.DevelopOrder"/> — the constructor the AI's
/// road policy uses — and what it will do is exactly <see cref="RoadDevelopmentQuery.Plan(IReadOnlyWorldState, Sim.Core.Systems.Research.ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>,
/// the selection RoadDevelopmentSystem applies. The order is built only when that plan is non-empty (some
/// route is eligible for a class the Empire knows) and the percentage is one the order log accepts at load
/// ((0, 100]); otherwise the system would change nothing and the UI logs nothing.
///
/// THE PREVIEW IS AN ESTIMATE. Cost and affordability are read on the CURRENT stocks. The system applies
/// the order in the next step after production has already run in that step — and production consumes
/// tools (and produces timber and stone) first — so a class whose materials include tools can be less
/// affordable when the order lands than the preview says (measured by the world stream). The UI labels it so.
/// </summary>
public static class RoadOrderFactory
{
    /// <summary>The order log's load-time range for DevelopRoads: a percentage in (0, 100].</summary>
    public static bool CanSubmit(double percent) => percent > 0.0 && percent <= 100.0;

    /// <summary>The steps the order would select now, in application order (empty without road, research
    /// or goods content, or when no route is eligible).</summary>
    public static RoadDevelopmentStep[] Preview(IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, double percent)
    {
        if (cfg.Roads is not { } roads || cfg.Research is not { } research || cfg.Goods is not { } goods) return [];
        if (!CanSubmit(percent)) return [];
        return RoadDevelopmentQuery.Plan(world, research, roads, goods, issuer, percent);
    }

    /// <summary>The DevelopRoads order for <paramref name="percent"/>%, stamped with the CURRENT turn, or null
    /// when the simulation would change nothing with it (see the header).</summary>
    public static OrderRecord? Create(IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, double percent) =>
        Preview(world, cfg, issuer, percent).Length > 0 ? RoadDevelopmentQuery.DevelopOrder(world, issuer, percent) : null;
}
