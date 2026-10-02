using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Construction;

/// <summary>
/// ADR-033 D5 — THE PROVISIONAL AI CONSTRUCTION POLICY (the AgeAdvancePolicy / RoadDevelopmentPolicy shape).
/// It is NOT a system and writes no state: it returns EnqueueConstruction ORDERS built by
/// <see cref="ConstructionQuery.EnqueueOrder"/> — the constructor the player's build action uses — for
/// projects <see cref="ConstructionQuery.IsProjectAvailable"/> admits, the very predicate ConstructionSystem
/// applies, which the session appends to the order log; the AI's buildings go through ConstructionSystem
/// exactly as the player's do and an AI run replays from its log.
///
/// THE RULE, stated so nothing about it is hidden: for each settlement an AI-commanded polity may build in,
/// in settlement-table order, AT MOST ONE order per turn — and only while that settlement's queue is EMPTY
/// (one project waits at a time, so a blocked head is never buried under more). It enqueues the first project
/// in goods.json order (the granary first) that the settlement has not built yet, that is AVAILABLE to the
/// polity there, and whose every material is present IN FULL in the settlement now (affordable; the
/// construction-labour gate is applied by the system when it resolves the queue). Building each project once
/// per settlement keeps the AI from sinking every unit of timber and stone into duplicate structures that no
/// system reads beyond the Age milestones. Deterministic and tie-free: integer table and content order, no
/// ordering over doubles. Strategic construction is later AI work. Player-commanded polities are never touched.
/// </summary>
public static class AiConstructionPolicy
{
    /// <summary>The orders the AI issues for <paramref name="polity"/> this turn (at most one per settlement).</summary>
    public static OrderRecord[] Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects) return [];
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return [];

        var orders = new List<OrderRecord>();
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            if (ConstructionQuery.Queue(world, settlement).Length > 0) continue;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (ConstructionQuery.Built(world, settlement, project.Id) > 0) continue;
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;
                if (!ConstructionQuery.MaterialsAvailable(world.GoodStocks, goods, settlement, project)) continue;
                orders.Add(ConstructionQuery.EnqueueOrder(world, polity, settlement, project.Id));
                break;
            }
        }
        return [.. orders];
    }
}
