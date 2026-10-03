using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// R1 (research → gameplay unlock pipeline) — THE RECIPE READ SEAM (the ConstructionQuery precedent): pure,
/// read-only, no state, no cache. <b>ProductionSystem computes with these same statics</b>: a crafting recipe
/// runs in a settlement iff <see cref="IsRecipeAvailable"/> holds on PREV, and the labour surface lists a
/// recipe's output (LabourActivities.GoodsOf) and the action surface lists the recipe as a production
/// capability (AvailableActionsQuery) iff the same predicate holds on the world asked about — one
/// predicate, every caller.
///
/// AVAILABLE = KNOWN (the recipe's research entity, goods.json <c>recipes[].entity</c> — data, no id in
/// code — is knowledge-eligible for the settlement's CONTROLLER through ResearchQuery.IsKnowledgeEligible,
/// the one knowledge evaluator; a null requirement is the baseline and always met, ADR-028 §3) AND the
/// recipe's own D-020 <c>requires</c> predicate over the settlement's published variables holds (the
/// artisan latch — a class-domain predicate the class domain owns).
///
/// Whose knowledge: the settlement's controller (EmpireQuery.TryGetController, the lowest polity id on a
/// tie). A settlement no Empire controls in a world that HAS a control relation has no researched knowledge
/// and runs only baseline recipes. A world with no control relation at all (a hand-built toy with no
/// polity roster) and a config with no research content have no knowledge to gate on: every recipe is
/// known there (the ConstructionQuery precedent). A recipe with no entity link has no knowledge
/// requirement; an entity id the content does not define is never known (content changed under a save).
/// </summary>
public static class CraftingQuery
{
    /// <summary>The knowledge mask that gates recipes in <paramref name="settlement"/>, or null when nothing
    /// gates (no research content, or no control relation in the world). An uncontrolled settlement of a
    /// controlled world gets its own accumulated knowledge (R2a).</summary>
    public static bool[]? KnowledgeOf(IReadOnlyWorldState world, ResearchContent? research, SettlementId settlement) =>
        SettlementKnowledge.MaskOf(world, research, settlement);

    /// <summary>Whether the recipe's knowledge requirement is met by <paramref name="completed"/> (null = no gate).</summary>
    public static bool IsKnown(ResearchContent? research, RecipeEntry recipe, bool[]? completed)
    {
        if (research is null || completed is null || recipe.Entity is null) return true;
        int entity = research.EntityIndexOf(recipe.Entity);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, completed);
    }

    /// <summary>Whether a POLITY knows the recipe (its own completed knowledge; the action surface's reading).</summary>
    public static bool IsKnownBy(IReadOnlyWorldState world, ResearchContent? research, PolityId polity, RecipeEntry recipe) =>
        research is null || IsKnown(research, recipe, ResearchQuery.CompletedMask(world, research, polity));

    /// <summary>THE recipe predicate (see the header). <paramref name="requires"/> is the recipe's parsed
    /// D-020 predicate when the caller holds it pre-parsed (ProductionSystem); null parses it here.
    /// <paramref name="completed"/> is <see cref="KnowledgeOf"/> for the settlement.</summary>
    public static bool IsRecipeAvailable(
        IReadOnlyWorldState world, ResearchContent? research, SettlementId settlement, RecipeEntry recipe,
        bool[]? completed, Systems.ClassMobility.Predicate? requires = null)
    {
        if (!IsKnown(research, recipe, completed)) return false;
        if (recipe.Requires is null) return true;
        Systems.ClassMobility.Predicate predicate = requires ?? Systems.ClassMobility.Predicate.Parse(recipe.Requires);
        return predicate.Evaluate(v => Variable(world, settlement, v));
    }

    /// <summary>Convenience: <see cref="IsRecipeAvailable"/> with the settlement's knowledge looked up.</summary>
    public static bool IsRecipeAvailable(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, RecipeEntry recipe) =>
        IsRecipeAvailable(world, cfg.Research, settlement, recipe, KnowledgeOf(world, cfg.Research, settlement));

    /// <summary>A published variable of the settlement; an unpublished variable reads 0.0 (the ProductionSystem precedent).</summary>
    public static double Variable(IReadOnlyWorldState world, SettlementId settlement, int varId)
    {
        for (int i = 0; i < world.Variables.Count; i++)
            if (world.Variables[i].Settlement == settlement && world.Variables[i].VarId == varId) return world.Variables[i].Value;
        return 0.0;
    }
}
