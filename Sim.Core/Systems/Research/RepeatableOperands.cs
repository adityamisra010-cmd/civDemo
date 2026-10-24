namespace Sim.Core.Systems.Research;

/// <summary>One row of the tranche-2 operand registry as the repeatable-levels track reads it (ADR-049): the operand id, its
/// direction (<see cref="Higher"/>: value = base × (1 + Σ); otherwise value = base × (1 − Σ)), the cap on Σ, and the
/// player-facing meaning "when improved".</summary>
public sealed record RepeatableOperand(string Id, bool Higher, double Cap, string Meaning);

/// <summary>
/// ADR-049 — THE LEGAL OPERAND IDS OF A REPEATABLE LEVEL EFFECT, in ONE place: the 47 rows of the tranche-2 contract §1.1
/// registry (docs/m6-plan/knowledge-redesign/tranche2-contract.md), in the contract's order, with their direction, cap and
/// meaning. The loader validates every <c>repeatable.effects[].operand</c> against <see cref="IndexOf"/>; the UI reads the
/// cap and meaning to show a level's benefit.
///
/// TO BE UNIFIED AT MERGE: the parallel t2-effects track builds the canonical C# operand registry (the one every owning
/// system reads, with KnowledgeEffects.Apply and its LevelContribution seam). When both land, this list is replaced by a
/// read of that registry — the ids, directions and caps here are copied from the same contract table and must agree
/// row for row. Pure data: no system reads a value from here.
/// </summary>
public static class RepeatableOperands
{
    public static readonly IReadOnlyList<RepeatableOperand> All =
    [
        new("farming.yieldPerArableKm2PerYear", true, 3.0, "land yields more grain"),
        new("farming.outputPerFarmerPerYear", true, 3.0, "each farmer produces more"),
        new("production.outputPerHerderPerYear", true, 2.0, "herds yield more"),
        new("production.outputPerExtractorPerYear", true, 3.0, "mining, logging, quarrying and fishing yield more"),
        new("production.toolYieldBonusMax", true, 1.0, "equipped farmers gain more from tools"),
        new("production.toolWearPerEquippedFarmerPerYear", false, 0.8, "tools last longer"),
        new("consumption.grainSpoilagePerYear", false, 0.85, "less grain rots in store"),
        new("consumption.granaryYearsOfDemand", true, 1.0, "granaries hold more"),
        new("harvestVariance.sigmaLogYield", false, 0.6, "steadier harvests"),
        new("granaries.famineReductionMax", true, 1.0, "granaries relieve famine more"),
        new("granaries.farmYieldBonusMax", true, 1.0, "stored seed and fodder raise yields"),
        new("foodState.adaptationAbsorbableShortfall", true, 1.0, "households absorb shortfalls"),
        new("housing.buildLaborAdultYearsPerDwelling", false, 0.8, "homes take less labour"),
        new("housing.buildTimberPerDwelling", false, 0.7, "homes take less timber"),
        new("housing.upkeepTimberPerDwellingYear", false, 0.8, "cheaper upkeep"),
        new("housing.tauYears", true, 1.5, "buildings last longer"),
        new("housing.personsPerDwelling", true, 0.5, "denser housing"),
        new("pathBuild.laborPerAdultPerYear", true, 1.0, "more path-building per worker"),
        new("pathBuild.buildCostMultiplier", false, 0.7, "paths cheaper to build"),
        new("pathBuild.dirtPathSpeedFactor", true, 0.8, "faster unpaved paths"),
        new("transport.riverCostFactor", false, 0.8, "cheaper river haulage"),
        new("roads.baselineKmPerDay", true, 3.0, "faster overland travel"),
        new("roads.maxRouteKm", true, 3.0, "longer roads possible"),
        new("catchment.hinterlandRadiusKm", true, 1.0, "settlements draw on a wider hinterland"),
        new("trade.gapClosingFraction", true, 1.5, "trade closes price gaps faster (result ≤ 0.95)"),
        new("trade.costPerBulkCostUnit", false, 0.8, "cheaper trade haulage"),
        new("price.stockReleaseRatePerYear", true, 1.0, "stocks reach markets faster"),
        new("demographics.mortalityPerYear", false, 0.7, "lower baseline mortality (all cohorts)"),
        new("demographics.starvationMortalityMaxPerYear", false, 0.6, "fewer famine deaths"),
        new("demographics.starvationChildMultiplier", false, 0.5, "children better protected in famine"),
        new("institutions.universities.maxMortalityReduction", true, 2.0, "university medicine saves more lives"),
        new("institutions.universities.maxResearchCostReduction", true, 0.5, "universities cut research cost more (result < 0.75)"),
        new("institutions.universities.maturityTauYears", false, 0.5, "universities mature faster"),
        new("governance.authorityDecayCostUnits", true, 3.0, "administrative reach extends further"),
        new("governance.taxExtractionResponseMax", true, 1.5, "taxes extract more output"),
        new("migration.baseRatePerYear", true, 1.0, "freer movement of people"),
        new("migration.dampingDecayCostUnits", true, 2.0, "migrants travel further"),
        new("mobility.promoteRatePerYear", true, 1.5, "faster social promotion"),
        new("mobility.targetShareCap", true, 1.0, "larger specialist classes possible"),
        new("colonization.orderPartyFraction", true, 0.5, "larger founding parties (result ≤ 0.35)"),
        new("fog.exploreStepNodes", true, 1.0, "each Explore order reaches further (integer)"),
        new("fog.exploreMaxRadiusNodes", true, 1.0, "larger exploration range (integer)"),
        new("fog.formationSightNodes", true, 2.0, "formations see further (integer)"),
        new("research.civicsPointsCoefficient", true, 0.3, "more Civics Points per person"),
        new("research.researchPointsCoefficient", true, 0.3, "more Research Points per person"),
        new("society.grievanceRelief", true, 0.6, "grievance builds more slowly (engine maps to the accumulation term)"),
        new("society.unrestOutputDrag", false, 0.6, "unrest costs less output"),
    ];

    /// <summary>The registry row index of <paramref name="id"/> (ordinal), or -1. A linear scan over a fixed array — no
    /// dictionary (law 5).</summary>
    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

    /// <summary>The registry row of <paramref name="id"/>; throws for an id outside the registry.</summary>
    public static RepeatableOperand Get(string id) =>
        IndexOf(id) is int i and >= 0 ? All[i] : throw new ArgumentException($"'{id}' is not a tranche-2 operand id.", nameof(id));
}
