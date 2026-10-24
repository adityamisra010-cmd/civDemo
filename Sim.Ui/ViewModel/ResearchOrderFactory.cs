using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-029 / D-044 R17 — the research directive the UI emits. It is pure, and since ADR-033 D5 it
/// DELEGATES to the one research order builder the AI uses too (<see cref="ResearchQuery.TargetOrder"/>,
/// <see cref="ResearchQuery.ClearTargetOrder"/>): the UI never writes research state and never builds
/// the order itself; a refusal is decided by the availability ResearchSystem applies, so the UI never
/// logs an order the simulation would ignore.
/// </summary>
public static class ResearchOrderFactory
{
    /// <summary>The order that sets <paramref name="node"/> as the issuing Empire's one active target,
    /// stamped with the CURRENT turn (delivered to the next End Turn, whose RP already goes to it), or
    /// null when the node is not available to that Empire now (unknown, completed, locked by
    /// prerequisites, or in a subtree whose research stage is not reached) — exactly
    /// <see cref="ResearchQuery.TargetOrder"/>. With tuning.destinationRouting (ADR-048) the same order names any
    /// INCOMPLETE node as the lane's destination; only an unknown or completed node is refused.</summary>
    public static OrderRecord? SetTarget(
        IReadOnlyWorldState world, ResearchContent content, PolityId issuer, ResearchNodeId node) =>
        ResearchQuery.TargetOrder(world, content, issuer, node);

    /// <summary>The order that clears the target, stamped with the CURRENT turn — exactly
    /// <see cref="ResearchQuery.ClearTargetOrder"/>. Partial progress everywhere is kept (D-044 R9);
    /// while there is no target, RP reaches no node (R20-D).</summary>
    public static OrderRecord ClearTarget(IReadOnlyWorldState world, PolityId issuer) =>
        ResearchQuery.ClearTargetOrder(world, issuer);

    /// <summary>ADR-043: the order that clears ONE lane's target (−1 Technology, −2 Civics), stamped with the CURRENT
    /// turn — exactly <see cref="ResearchQuery.ClearTargetOrder(IReadOnlyWorldState, PolityId, ResearchTree)"/>.</summary>
    public static OrderRecord ClearTarget(IReadOnlyWorldState world, PolityId issuer, ResearchTree lane) =>
        ResearchQuery.ClearTargetOrder(world, issuer, lane);
}
