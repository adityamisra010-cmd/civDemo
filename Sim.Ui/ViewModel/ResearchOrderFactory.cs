using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-029 / D-044 R17 — the research directive the UI emits. It is pure. The UI
/// never writes research state: choosing a target builds a SetResearchTarget order
/// for the log, and the simulation applies it in the next step (the
/// LaborOrderFactory pattern, m1 spec §3). A refusal is decided HERE, from the same
/// <see cref="ResearchQuery"/> availability the system uses, so the UI never logs
/// an order the simulation would ignore.
/// </summary>
public static class ResearchOrderFactory
{
    /// <summary>The order that sets <paramref name="node"/> as the issuing Empire's
    /// one active target, or null when the node is not available to that Empire now
    /// (unknown, completed, locked by prerequisites, or in a subtree whose research
    /// stage is not reached).</summary>
    public static OrderRecord? SetTarget(
        IReadOnlyWorldState world, ResearchContent content, PolityId issuer, ResearchNodeId node)
    {
        int index = content.IndexOf(node);
        if (index < 0) return null;
        bool[] completed = ResearchQuery.CompletedMask(world, content, issuer);
        if (!ResearchQuery.IsAvailable(content, index, completed, ResearchQuery.StageReached(content, completed)))
            return null;
        // Turn semantics (§3.9): stamped with the CURRENT turn, the order is delivered to
        // the step executing FROM this turn's state — the very next End Turn press — and
        // that step's RP already goes to the new target.
        return OrderRecord.From(world.Clock.Turn, issuer, OrderKind.SetResearchTarget, node.Value, 0.0);
    }

    /// <summary>The order that clears the target. Partial progress everywhere is kept
    /// (D-044 R9). While there is no target, RP reaches no node (R20-D).</summary>
    public static OrderRecord ClearTarget(long currentTurn, PolityId issuer) =>
        OrderRecord.From(currentTurn, issuer, OrderKind.SetResearchTarget, -1, 0.0);
}
