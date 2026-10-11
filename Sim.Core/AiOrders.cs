using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Colonization;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Governance;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;

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
/// <item><see cref="AiResearchPolicy"/> — a research target when it has none (OrderKind 6); its goals are the next
/// Age's core research and the knowledge the AI's own capability-gated actions need (ADR-033 B).</item>
/// <item><see cref="AgeAdvancePolicy"/> — advance on the first eligible turn (OrderKind 7).</item>
/// <item><see cref="RoadDevelopmentPolicy"/> — develop roads once a class is known and affordable (OrderKind 8).</item>
/// <item><see cref="AiConstructionPolicy"/> — one affordable available project per idle settlement, and at most one
/// viable, affordable university founding per polity (OrderKind 4; ADR-033 D6). The university's labour is
/// judged against the length of the turn that produced <c>world</c> (<c>world.Clock.DtYears</c>; the era table
/// only ever shortens turns, so at an era boundary a founding can wait one more turn in the queue —
/// ConstructionSystem re-applies its gate and never overspends); none is judged at the founding state.</item>
/// <item><see cref="AiFoundingPolicy"/> — M5 S1c (ADR-036 §8): at most one Found Settlement order per polity
/// (OrderKind 9), through the player's own constructor and FoundingQuery predicate, when the polity's interval has
/// passed, a fed source of the threshold size can outfit a party and an admissible site exists. The sites of the
/// whole batch are shared (<see cref="AiFoundingPolicy.Sites"/>) so a later polity spaces itself against an earlier
/// one's order exactly as ColonizationSystem will. It reads the WORLDGEN siting config (<paramref name="worldgen"/> on
/// <see cref="For"/>) — the same config the pipeline's ColonizationSystem judges the order with. sim.json
/// <c>colonization.aiFounding.enabled = false</c> makes it issue nothing (the pre-S1c producer, byte for byte).</item>
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
    /// <summary>Every AI polity's orders for the step executing from <paramref name="world"/>, stamped with its turn.
    /// <paramref name="worldgen"/> supplies the siting config Found Settlement orders are judged with — pass the
    /// config the executor's pipeline was built from (SystemCatalog.All).</summary>
    public static OrderRecord[] For(IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen)
    {
        var result = new List<OrderRecord>();
        var seen = new List<int>();
        var sites = new AiFoundingPolicy.Sites(world, cfg, worldgen);   // built only if some polity reaches the site arm
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
            result.AddRange(AiConstructionPolicy.Decide(world, cfg, polity,
                world.Clock.DtYears > 0.0 ? world.Clock.DtYears : null));
            if (AiFoundingPolicy.Decide(world, cfg, polity, sites) is { } found) result.Add(found);
        }
        Governance(world, cfg, result);
        return [.. result];
    }

    /// <summary>Appends <see cref="For"/>'s orders to <paramref name="log"/> (the session's order log, which the
    /// executor delivers from and the replay reads) and returns how many were appended.</summary>
    public static int Append(OrderLog log, IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen)
    {
        OrderRecord[] orders = For(world, cfg, worldgen);
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
    /// GOVERNANCE (ADR-033 D4/D5; wired at the S1×S2 reconciliation). The tax valve (D-021 valve 6, "the state
    /// acts by default"): each AI Empire, in roster order, asks <see cref="AiGovernance.OrdersFor"/>, which
    /// speaks only when the polity is AI-commanded, alive, can levy (<c>Governance.CanLevyTax</c>, the same
    /// gate the player's edict obeys: the Taxation knowledge AND the minimum Age, H2) and wants a different
    /// rate. Stamped with the executing turn,
    /// like every other AI order; appended after every per-polity order above.
    /// </summary>
    public static void Governance(IReadOnlyWorldState world, SimConfig cfg, List<OrderRecord> into)
    {
        var seen = new List<int>();
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityRow row = world.Polities[i];
            if (row.Source != CommandSource.Ai || seen.Contains(row.Id.Value)) continue;
            seen.Add(row.Id.Value);
            into.AddRange(AiGovernance.OrdersFor(world, cfg, row.Id, world.Clock.Turn));
        }
    }
}
