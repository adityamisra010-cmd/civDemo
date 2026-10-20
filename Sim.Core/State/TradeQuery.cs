using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// R2a — THE TRADE CAPABILITY PREDICATE (Director decision 1). Pure, read-only, no state. ONE predicate, every
/// caller: TradeArbitrageSystem moves goods between a settlement pair only when <see cref="CanTrade"/> holds on
/// PREV, and AvailableActionsQuery lists the Trade capability only when <see cref="KnowsTrade"/> holds.
///
/// The gate is content: sim.json <c>trade.entity</c> names a research entity (research.json; its requirement
/// names the node), evaluated by the one knowledge evaluator against each settlement's knowledge
/// (<see cref="SettlementKnowledge.MaskOf"/> — the controller's, or a city-state's own). No node id in code.
/// A pair trades iff BOTH endpoints know trade: formal exchange needs a partner who practises it, so a polity
/// cannot export trade to a neighbour that has not learned it, and foreign trade waits on both sides.
/// Ungated (true) when the config names no entity, no research content is attached, or the world has no control
/// relation (a hand-built toy — the ConstructionQuery precedent). WITHIN-settlement consumption is not trade and is
/// never asked about here.
/// </summary>
public static class TradeQuery
{
    /// <summary>Whether a settlement's knowledge makes trade legal there.</summary>
    public static bool SettlementKnowsTrade(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        SettlementKnowledge.IsEntityEligible(world, cfg.Research, settlement, cfg.Trade.Entity);

    /// <summary>THE pair predicate TradeArbitrageSystem applies.</summary>
    public static bool CanTrade(IReadOnlyWorldState world, SimConfig cfg, SettlementId a, SettlementId b) =>
        SettlementKnowsTrade(world, cfg, a) && SettlementKnowsTrade(world, cfg, b);

    /// <summary>Whether a POLITY's own completed knowledge makes the Trade capability eligible (the action
    /// surface's reading; no entity configured or no research = always).</summary>
    public static bool KnowsTrade(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Trade.Entity is not { } id || cfg.Research is not { } research) return true;
        int entity = research.EntityIndexOf(id);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, ResearchQuery.CompletedMask(world, research, polity));
    }
}
