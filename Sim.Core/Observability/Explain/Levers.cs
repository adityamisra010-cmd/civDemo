using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Observability.Explain;

/// <summary>
/// The player control that reaches a chain node, or none with the reason.
/// <see cref="Kind"/> is null for None; otherwise it is an EXISTING
/// <see cref="OrderKind"/> and <see cref="Sectors"/> names the sector ids the
/// order targets (Sectors.Farming..Construction). The Reason is a statement of
/// the mechanism path, never advice: naming the lever is the whole of what
/// this table does.
/// </summary>
public readonly record struct Lever(OrderKind? Kind, int[] Sectors, string Reason)
{
    public bool IsNone => Kind is null;

    public static Lever None(string reason) => new(null, [], reason);

    public static Lever Allocation(string reason, params int[] sectors) =>
        new(OrderKind.SectorAllocation, sectors, reason);
}

/// <summary>
/// T4.19 — THE LEVER TABLE (docs/observability-architecture.md §5, last column).
///
/// M4 has exactly ONE player policy: the five-sector labour allocation per
/// settlement (D-032, <see cref="OrderKind.SectorAllocation"/>; the legacy
/// <see cref="OrderKind.LaborAllocation"/> maps onto the same row). So every
/// lever here is that order, by sector, or None. <see cref="OrderKind.EnqueueConstruction"/>
/// (M4-D) exists but reaches no node in any need chain: a completed structure
/// feeds no need, and the queue only COMPETES for the construction pool
/// (ConstructionSystem.CapacityMeets subtracts housing's draw, never the
/// reverse) — so it is not listed as a lever for Shelter, because pulling it
/// cannot raise a dwelling count.
///
/// WHERE THERE IS NO LEVER THE TABLE SAYS SO. Weather, arable land, deposit
/// abundance and population are conditions; grain imports do not exist (grain
/// never trades, trade is autonomous). No mechanic is invented to make a
/// tooltip actionable, and no recommendation text is carried — a reason names
/// the path, nothing more.
///
/// A GAP node maps to the lever of the STORED inputs it is computed from, and
/// the reason says the node itself is transient. Every <see cref="ChainNode"/>
/// has an entry; an unknown node throws, and the lever-completeness test walks
/// the whole enum so a new node cannot land unmapped.
/// </summary>
public static class Levers
{
    public static Lever For(ChainNode node) => node switch
    {
        // --- Sustenance -------------------------------------------------
        ChainNode.SustenanceSatisfaction => Lever.Allocation(
            "Reached through the food fills: farming share (grain harvest) and herding share (livestock, fish).",
            Sectors.Farming, Sectors.Herding),
        ChainNode.FoodGoodEaten or ChainNode.FoodGoodDemanded or ChainNode.FoodGoodFill => Lever.Allocation(
            "What is eaten is bounded by what was produced and stored: grain by the farming share, livestock and fish by the herding share.",
            Sectors.Farming, Sectors.Herding),
        ChainNode.DeficitRatio => Lever.Allocation(
            "Food obtained over food required; obtained is produced by the farming and herding pools.",
            Sectors.Farming, Sectors.Herding),
        ChainNode.NutritionalDemand => Lever.None(
            "A population fact (cohort-weighted heads × basket rates); demographics and migration move it, no order does."),
        ChainNode.GrainStore or ChainNode.GrainHarvest or ChainNode.GrainEaten => Lever.Allocation(
            "Farm labour = farming share × adults is the labour side of the harvest Leontief.", Sectors.Farming),
        ChainNode.FarmingShare => Lever.Allocation("This IS the farming slider.", Sectors.Farming),
        ChainNode.ArableLand => Lever.None(
            "EffectiveArableKm2 is the catchment's fertility-weighted land — a condition of the site and its network; no M4 order changes it."),
        ChainNode.HarvestWeather => Lever.None(
            "Mean-one stochastic weather (HarvestWeatherSystem); a condition, not a control."),
        ChainNode.ToolsStock => Lever.Allocation(
            "Tools are crafted (toolmaking recipe) from bronze, which is cast from extracted ore: crafting and extraction shares.",
            Sectors.Crafting, Sectors.Extraction),
        ChainNode.ToolFactor => Lever.Allocation(
            "Transient (not recorded); its stored input is the tools stock, reached by the crafting share.", Sectors.Crafting),
        ChainNode.LandVsLabourBinding => Lever.Allocation(
            "Transient (not recorded); the labour side is the farming share — the land side has no lever.", Sectors.Farming),
        ChainNode.DepositFoodProduced => Lever.Allocation(
            "Herding pool = herding share × adults, split across food deposits ∝ abundance.", Sectors.Herding),
        ChainNode.HerdingShare => Lever.Allocation("This IS the herding slider.", Sectors.Herding),
        ChainNode.DepositAbundance => Lever.None(
            "A founding endowment (DepositRow.Abundance): a condition, no order changes it."),
        ChainNode.GrainImports => Lever.None(
            "Grain never trades (numeraire pinned at 1.0, TradeArbitrageSystem) and trade is autonomous: no player trade lever exists in M4."),

        // --- Shelter ----------------------------------------------------
        ChainNode.ShelterSatisfaction or ChainNode.HousingSufficiency => Lever.Allocation(
            "Dwellings are built by the construction pool from timber produced by the extraction pool.",
            Sectors.Construction, Sectors.Extraction),
        ChainNode.Dwellings or ChainNode.DwellingsDelta => Lever.Allocation(
            "Built = min(deficit, labour cap, timber cap): construction share for labour, extraction share for timber; upkeep timber also comes from extraction.",
            Sectors.Construction, Sectors.Extraction),
        ChainNode.Population => Lever.None(
            "Σ bucket counts; demographics and migration move it, no order does."),
        ChainNode.PersonsPerDwelling => Lever.None("A tuning constant (sim.json housing.personsPerDwelling)."),
        ChainNode.MaintenanceFraction => Lever.Allocation(
            "Upkeep is timber (and clay where the data draws it) against the stock the extraction pool produces.",
            Sectors.Extraction),
        ChainNode.ConstructionLabourUsed => Lever.Allocation(
            "Builder-years = construction share × adults × dt.", Sectors.Construction),
        ChainNode.ConstructionShare => Lever.Allocation("This IS the construction slider.", Sectors.Construction),
        ChainNode.TimberStock or ChainNode.ClayStock => Lever.Allocation(
            "A deposit good produced by the extraction pool ∝ its abundance.", Sectors.Extraction),
        ChainNode.ExtractionShare => Lever.Allocation("This IS the extraction slider.", Sectors.Extraction),
        ChainNode.BuiltDecayedSplit => Lever.Allocation(
            "Not recorded per settlement; built follows construction labour and timber, decay follows unmet timber upkeep.",
            Sectors.Construction, Sectors.Extraction),
        ChainNode.BuildBindingConstraint => Lever.Allocation(
            "Transient (not recorded); its stored inputs are construction labour and the timber stock.",
            Sectors.Construction, Sectors.Extraction),

        // --- Comfort / basket-bound -------------------------------------
        ChainNode.ComfortSatisfaction => Lever.Allocation(
            "Pottery and cloth are crafted from clay, timber and fiber produced by the extraction pool.",
            Sectors.Crafting, Sectors.Extraction),
        ChainNode.ComfortGoodEaten or ChainNode.ComfortGoodDemanded or ChainNode.ComfortGoodFill
            or ChainNode.CraftOutputProduced => Lever.Allocation(
            "Output = min(labour cap, every input cap): crafting share for labour, extraction share for the raw inputs.",
            Sectors.Crafting, Sectors.Extraction),
        ChainNode.CraftingShare => Lever.Allocation("This IS the crafting slider.", Sectors.Crafting),
        ChainNode.CraftInputStock => Lever.Allocation(
            "A deposit good produced by the extraction pool ∝ its abundance.", Sectors.Extraction),
        ChainNode.CraftInputDemand => Lever.Allocation(
            "What recipes wanted from labour alone: the crafting share sets it.", Sectors.Crafting),
        ChainNode.RecipeLabourCap => Lever.Allocation(
            "Transient (not recorded); its stored input is the crafting pool.", Sectors.Crafting),

        // --- unbound ----------------------------------------------------
        ChainNode.NotSimulated => Lever.None("Not yet simulated: nothing reaches a need that is not computed."),

        _ => throw new ArgumentOutOfRangeException(nameof(node), node, "chain node has no lever entry"),
    };
}
