using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Roads;

namespace Sim.Core;

/// <summary>
/// ADR-033 D5 — THE ONE DETERMINISTIC AI ORDER PRODUCER. A NEW SEAM (decision ledger §29 asks for new seams to
/// be flagged): it stands where the player stands — OUTSIDE the turn, between turns — and produces each
/// AI-commanded polity's ORDERS for the coming step through the same order constructors and the same
/// domain predicates the player's actions use. It is not a system, reads only what the player could read,
/// writes no state, and has no AI-only verb or write path: everything it decides is an ordinary
/// <see cref="OrderRecord"/> that the session appends to the order log, the systems validate and apply
/// exactly as they do the player's, and a replay of the log reproduces without running this class again.
///
/// PER AI POLITY, in roster order (each polity once; an extinct Empire — no controlled settlement — has
/// nothing to decide and is skipped), in this fixed order:
/// <list type="number">
/// <item><see cref="AiResearchPolicy"/> — a research target when it has none (OrderKind 6).</item>
/// <item><see cref="AgeAdvancePolicy"/> — advance on the first eligible turn (OrderKind 7).</item>
/// <item><see cref="RoadDevelopmentPolicy"/> — develop roads once a class is known and affordable (OrderKind 8).</item>
/// <item><see cref="AiConstructionPolicy"/> — one affordable available project per idle settlement (OrderKind 4).</item>
/// </list>
/// then <see cref="Governance"/>, the reconciliation slot for the tax policy (OrderKind 5, stream S1).
///
/// Player-commanded polities are never touched. With worldgen's default <c>aiEmpires = 0</c> no polity is
/// AI-commanded and this produces nothing, so no golden can move because it is called.
/// Deterministic: roster, settlement-table and content order; every argmin is a composite key with an integer
/// tie-break (in the policies); no RNG, no clock, no dictionary, no LINQ (law 5).
/// </summary>
public static class AiOrders
{
    /// <summary>Every AI polity's orders for the step executing from <paramref name="world"/>, stamped with its turn.</summary>
    public static OrderRecord[] For(IReadOnlyWorldState world, SimConfig cfg)
    {
        var result = new List<OrderRecord>();
        var seen = new List<int>();
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityRow row = world.Polities[i];
            if (row.Source != CommandSource.Ai || seen.Contains(row.Id.Value)) continue;
            seen.Add(row.Id.Value);
            PolityId polity = row.Id;
            if (EmpireQuery.IsExtinct(world, polity)) continue;

            if (AiResearchPolicy.Decide(world, cfg, polity) is { } research) result.Add(research);
            if (cfg.Ages is { } ages && AgeAdvancePolicy.Decide(world, ages, polity) is { } advance) result.Add(advance);
            if (RoadDevelopmentPolicy.Decide(world, cfg, polity) is { } roads) result.Add(roads);
            result.AddRange(AiConstructionPolicy.Decide(world, cfg, polity));
        }
        Governance(world, cfg, result);
        return [.. result];
    }

    /// <summary>Appends <see cref="For"/>'s orders to <paramref name="log"/> (the session's order log, which the
    /// executor delivers from and the replay reads) and returns how many were appended.</summary>
    public static int Append(OrderLog log, IReadOnlyWorldState world, SimConfig cfg)
    {
        OrderRecord[] orders = For(world, cfg);
        foreach (OrderRecord order in orders) log.Append(order);
        return orders.Length;
    }

    /// <summary>Whether any registered polity is AI-commanded (the session loops run the producer only then).</summary>
    public static bool HasAiPolity(IReadOnlyWorldState world)
    {
        for (int i = 0; i < world.Polities.Count; i++)
            if (world.Polities[i].Source == CommandSource.Ai) return true;
        return false;
    }

    /// <summary>
    /// RECONCILIATION SLOT — GOVERNANCE (ADR-033 D4/D5). Produces nothing in this build: SetTaxRate (OrderKind 5)
    /// does not exist here. Stream S1 ports M5's <c>AiGovernance</c>; at reconciliation the orchestrator appends
    /// <c>AiGovernance.ChooseOrders(world, cfg, world.Clock.Turn)</c> here — it visits the AI Empires in roster
    /// order itself and emits an order only when an Empire wants to change its rate — keeping its orders after
    /// every per-polity order above.
    /// </summary>
    public static void Governance(IReadOnlyWorldState world, SimConfig cfg, List<OrderRecord> into)
    {
        _ = world;
        _ = cfg;
        _ = into;
    }
}
