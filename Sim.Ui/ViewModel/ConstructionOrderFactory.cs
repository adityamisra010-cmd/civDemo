using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-033 D3 — the player's BUILD order. Pure. The order is exactly
/// <see cref="ConstructionQuery.EnqueueOrder"/> (the constructor the AI's construction policy uses too),
/// and it is built only when <see cref="ConstructionQuery.IsProjectAvailable"/> holds — THE predicate
/// ConstructionSystem applies to an EnqueueConstruction order and the action surface lists projects by
/// (one predicate, every caller): the settlement exists, the Empire controls it, goods.json defines the
/// project and its research entity is knowledge-eligible. Materials and construction labour are
/// AFFORDABILITY, not legality: a project that is short of timber is still legal to queue (the queue head
/// waits until it can be built), so the blocker is shown as information and never refuses the order.
/// </summary>
public static class ConstructionOrderFactory
{
    /// <summary>The EnqueueConstruction order for <paramref name="projectId"/> in <paramref name="settlement"/>,
    /// stamped with the CURRENT turn (delivered to the next End Turn, which queues it), or null when the
    /// simulation would refuse it.</summary>
    public static OrderRecord? Create(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId) =>
        ConstructionQuery.IsProjectAvailable(world, cfg, issuer, settlement, projectId)
            ? ConstructionQuery.EnqueueOrder(world, issuer, settlement, projectId)
            : null;
}
