using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// R2a — THE KNOWLEDGE OF A SETTLEMENT (docs/city-state-progression.md). Pure, read-only, no state, no cache.
///
/// Knowledge lives in the research relation (<c>ResearchCompleted</c> and its sibling tables), keyed by a
/// KNOWLEDGE HOLDER. A holder is a roster polity (its rows are its civilization's knowledge, as since ADR-029)
/// or — for a settlement no Empire controls — the reserved, derived key <see cref="LocalHolder"/> = PolityId(−1 − s).
/// A local holder is a key of the research relation and nothing else: it is never on the Polities roster, never
/// in Controls, issues no orders and owns nothing, so the control relation stays the one answer to "who rules
/// this place" (D-037). Real polity ids are ≥ 0, so the bands never meet.
///
/// <see cref="MaskOf"/> is THE settlement knowledge every settlement-scoped capability predicate reads (recipes,
/// the labour surface, the Trade gate, the pre-cultivation food yield): the controller's completed knowledge ∪ the
/// settlement's own accumulated knowledge when controlled (a craft tradition survives annexation; the CONTROLLER
/// is not granted it), its own accumulated knowledge when uncontrolled. Null when nothing gates: no research
/// content, or a world with no control relation at all (a hand-built toy — the ConstructionQuery precedent).
/// </summary>
public static class SettlementKnowledge
{
    /// <summary>The reserved knowledge-holder key of a settlement's OWN knowledge.</summary>
    public static PolityId LocalHolder(SettlementId settlement) => new(-1 - settlement.Value);

    /// <summary>Whether a research-relation key is a settlement's own (local) holder.</summary>
    public static bool IsLocalHolder(PolityId holder) => holder.Value < 0;

    /// <summary>The settlement a local holder key belongs to.</summary>
    public static SettlementId SettlementOf(PolityId localHolder) => new(-1 - localHolder.Value);

    /// <summary>The holder a settlement's people and Eureka conditions count toward NOW: its controller (lowest
    /// id, EmpireQuery.TryGetController), else its own local holder.</summary>
    public static PolityId HolderOf(IReadOnlyWorldState world, SettlementId settlement) =>
        EmpireQuery.TryGetController(world, settlement, out PolityId controller) ? controller : LocalHolder(settlement);

    /// <summary>Whether the settlement researches on its own this turn: the world has a control relation, nobody
    /// controls the settlement, and the content enables city-state progression.</summary>
    public static bool ResearchesLocally(IReadOnlyWorldState world, ResearchContent content, SettlementId settlement) =>
        content.Tuning.CityStatePaceFraction > 0.0 && world.Controls.Count > 0
        && !EmpireQuery.TryGetController(world, settlement, out _);

    /// <summary>THE settlement knowledge mask (see the header), or null when nothing gates.</summary>
    public static bool[]? MaskOf(IReadOnlyWorldState world, ResearchContent? research, SettlementId settlement)
    {
        if (research is null || world.Controls.Count == 0) return null;
        bool[] own = ResearchQuery.CompletedMask(world, research, LocalHolder(settlement));
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId controller)) return own;
        bool[] mask = ResearchQuery.CompletedMask(world, research, controller);
        for (int i = 0; i < mask.Length; i++) mask[i] |= own[i];
        return mask;
    }

    /// <summary>Whether a research entity (by id) is knowledge-eligible in the settlement. A null entity id, no
    /// research content or no control relation: no gate (true). An id the content does not define: never.</summary>
    public static bool IsEntityEligible(IReadOnlyWorldState world, ResearchContent? research, SettlementId settlement, string? entityId)
    {
        if (entityId is null || research is null) return true;
        bool[]? mask = MaskOf(world, research, settlement);
        if (mask is null) return true;
        int entity = research.EntityIndexOf(entityId);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, mask);
    }
}
