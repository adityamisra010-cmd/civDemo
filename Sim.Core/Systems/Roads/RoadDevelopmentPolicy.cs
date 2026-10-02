using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Roads;

/// <summary>
/// ADR-032 — THE PROVISIONAL AI ROAD POLICY (the Director's transport ruling 9: "the AI may
/// choose its own percentage/strategy, but both ultimately invoke the same domain operation").
/// It is NOT a system and writes no state: it returns DevelopRoads ORDERS built by
/// <see cref="RoadDevelopmentQuery.DevelopOrder"/> — the very constructor the player's slider
/// uses — which the session appends to the order log, so the AI's roads go through
/// RoadDevelopmentSystem and <see cref="RoadDevelopmentQuery.Plan(IReadOnlyWorldState, Research.ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>
/// exactly as the player's do, and an AI run replays from its log (the AgeAdvancePolicy shape).
///
/// THE RULE, stated so nothing is hidden: an AI-commanded polity orders development when the
/// plan is non-empty AND its first step is affordable from PREV stocks (it never issues an order
/// that would build nothing). Its percentage is <see cref="TradedPercent"/> when any candidate
/// route carries realised trade, else <see cref="UntradedPercent"/> — a fixed, deterministic,
/// tie-free choice (one candidate order per polity). Strategic road planning is later AI work.
/// Player-commanded polities are never touched.
/// </summary>
public static class RoadDevelopmentPolicy
{
    /// <summary>The AI's percentage when its routes carry trade (TUNE).</summary>
    public const double TradedPercent = 50.0;

    /// <summary>The AI's percentage when none of its routes carries trade yet (TUNE).</summary>
    public const double UntradedPercent = 25.0;

    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Roads is null || cfg.Research is null || cfg.Goods is null) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;

        RouteStatus[] ranked = RoadDevelopmentQuery.Ranked(RoadDevelopmentQuery.Routes(world, cfg.Research, cfg.Roads, polity));
        if (ranked.Length == 0) return null;
        double percent = ranked[0].Usage > 0 ? TradedPercent : UntradedPercent;

        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(world, cfg.Research, cfg.Roads, cfg.Goods, polity, percent);
        if (plan.Length == 0 || !Affordable(world, plan[0])) return null;
        return RoadDevelopmentQuery.DevelopOrder(world, polity, percent);
    }

    /// <summary>Every AI polity's order for this turn, in roster order (each polity once).</summary>
    public static OrderRecord[] OrdersForAi(IReadOnlyWorldState world, SimConfig cfg)
    {
        var result = new List<OrderRecord>();
        var seen = new List<int>();
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityId polity = world.Polities[i].Id;
            if (seen.Contains(polity.Value)) continue;
            seen.Add(polity.Value);
            if (Decide(world, cfg, polity) is { } order) result.Add(order);
        }
        return [.. result];
    }

    private static bool Affordable(IReadOnlyWorldState world, RoadDevelopmentStep step)
    {
        for (int m = 0; m < step.Cost.Length; m++)
        {
            int idx = GoodStockIndex.IndexOf(world.GoodStocks, step.Payer, step.Cost[m].Good);
            if (idx < 0 || world.GoodStocks[idx].Amount.Value < step.Cost[m].Units) return false;
        }
        return true;
    }
}
