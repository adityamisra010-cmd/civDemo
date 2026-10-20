using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Ages;

/// <summary>
/// ADR-031 — THE PROVISIONAL AI AGE POLICY (D-047 ruling 13: "AI civilizations make the
/// equivalent decision through AI logic"). It is NOT a system and writes no state: it
/// returns AdvanceAge ORDERS, which the session appends to the order log exactly as the
/// player's click does, so AI and player share one order pathway (ledger §2.4) and an AI
/// run replays from its log.
///
/// THE RULE, stated so nothing about it is hidden: an AI-commanded polity advances on the
/// first turn it is eligible, choosing the LOWEST surge key in content order. That is
/// deterministic and tie-free by construction (one candidate order per polity; the surge
/// is a fixed content position, never an argmax over scores). It is the dullest rule that
/// exercises the pathway; strategic delay (the option ruling 13 gives the player) is later
/// AI work. Player-commanded polities are never touched: the player is never auto-advanced.
/// </summary>
public static class AgeAdvancePolicy
{
    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, AgeContent content, PolityId polity)
    {
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        if (AgeQuery.NextAge(world, content, polity) is not { } next) return null;
        int surge = content.Surges[0].Key;
        if (AgeQuery.CheckAdvance(world, content, polity, next.Key, surge) != AdvanceRejection.None) return null;
        return AgeQuery.AdvanceOrder(world, polity, next.Key, surge);
    }

    /// <summary>Every AI polity's order for this turn, in roster order (each polity once).</summary>
    public static OrderRecord[] OrdersForAi(IReadOnlyWorldState world, AgeContent content)
    {
        var result = new List<OrderRecord>();
        var seen = new List<int>();
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityId polity = world.Polities[i].Id;
            if (seen.Contains(polity.Value)) continue;
            seen.Add(polity.Value);
            if (Decide(world, content, polity) is { } order) result.Add(order);
        }
        return [.. result];
    }
}
