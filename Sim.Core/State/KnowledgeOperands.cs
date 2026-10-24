namespace Sim.Core.State;

/// <summary>
/// ADR-047 (tranche-2 contract §1.1) — THE KNOWLEDGE OPERANDS: every parameter of a live equation that completed
/// research may improve. The enum value is the operand's index into <see cref="KnowledgeOperands.All"/>; the id is the
/// only spelling research.json may use (<c>effects.realized[].operand</c>). The list is the contract table, in its order.
/// Adding an operand is a contract change (a new row in §1.1 and a read site in its owning system), never a content edit.
/// </summary>
public enum KnowledgeOperand
{
    FarmingYieldPerArableKm2PerYear = 0,
    FarmingOutputPerFarmerPerYear = 1,
    ProductionOutputPerHerderPerYear = 2,
    ProductionOutputPerExtractorPerYear = 3,
    ProductionToolYieldBonusMax = 4,
    ProductionToolWearPerEquippedFarmerPerYear = 5,
    ConsumptionGrainSpoilagePerYear = 6,
    ConsumptionGranaryYearsOfDemand = 7,
    HarvestVarianceSigmaLogYield = 8,
    GranariesFamineReductionMax = 9,
    GranariesFarmYieldBonusMax = 10,
    FoodStateAdaptationAbsorbableShortfall = 11,
    HousingBuildLaborAdultYearsPerDwelling = 12,
    HousingBuildTimberPerDwelling = 13,
    HousingUpkeepTimberPerDwellingYear = 14,
    HousingTauYears = 15,
    HousingPersonsPerDwelling = 16,
    PathBuildLaborPerAdultPerYear = 17,
    PathBuildBuildCostMultiplier = 18,
    PathBuildDirtPathSpeedFactor = 19,
    TransportRiverCostFactor = 20,
    RoadsBaselineKmPerDay = 21,
    RoadsMaxRouteKm = 22,
    CatchmentHinterlandRadiusKm = 23,
    TradeGapClosingFraction = 24,
    TradeCostPerBulkCostUnit = 25,
    PriceStockReleaseRatePerYear = 26,
    DemographicsMortalityPerYear = 27,
    DemographicsStarvationMortalityMaxPerYear = 28,
    DemographicsStarvationChildMultiplier = 29,
    InstitutionsUniversitiesMaxMortalityReduction = 30,
    InstitutionsUniversitiesMaxResearchCostReduction = 31,
    InstitutionsUniversitiesMaturityTauYears = 32,
    GovernanceAuthorityDecayCostUnits = 33,
    GovernanceTaxExtractionResponseMax = 34,
    MigrationBaseRatePerYear = 35,
    MigrationDampingDecayCostUnits = 36,
    MobilityPromoteRatePerYear = 37,
    MobilityTargetShareCap = 38,
    ColonizationOrderPartyFraction = 39,
    FogExploreStepNodes = 40,
    FogExploreMaxRadiusNodes = 41,
    FogFormationSightNodes = 42,
    ResearchCivicsPointsCoefficient = 43,
    ResearchResearchPointsCoefficient = 44,
    SocietyGrievanceRelief = 45,
    SocietyUnrestOutputDrag = 46,
}

/// <summary>How an IMPROVEMENT moves an operand's value: <see cref="Higher"/> scales the base by (1 + Σ),
/// <see cref="Lower"/> by (1 − Σ) (contract §1, steps 3 and 4).</summary>
public enum OperandDirection { Higher = 1, Lower = 2 }

/// <summary>
/// One registry row (contract §1.1): the operand, its content id, the direction an improvement moves it, the cap on the
/// summed improvement Σ, whether the parameter is a whole number (rounded DOWN after scaling, step 5), the bound the
/// RESULT may not pass where the contract states one (<see cref="double.PositiveInfinity"/> otherwise — see
/// <see cref="KnowledgeOperands"/> for each), the system that reads it and what an improvement means.
/// </summary>
public sealed record KnowledgeOperandInfo(
    KnowledgeOperand Operand, string Id, OperandDirection Direction, double Cap, bool Integer, double ResultMax,
    string ReadBy, string Meaning);

/// <summary>
/// ADR-047 — THE OPERAND REGISTRY, the one place the operand table lives (tranche-2 contract §1.1, row for row: id,
/// direction, cap Σ, integer flag, reader, meaning). The loader refuses any other id; the owning systems read their
/// operands through <see cref="KnowledgeEffects"/>.
/// <para><b>Result bounds.</b> Four come from the contract text: trade.gapClosingFraction ≤ 0.95,
/// institutions.universities.maxResearchCostReduction &lt; 0.75 (clamped at 0.75: the factor 1 − r·S stays above 0.25
/// because S &lt; 1), colonization.orderPartyFraction ≤ 0.35. Two are the equations' own domains, stated here rather than
/// left to overflow: mobility.targetShareCap ≤ 1 (a share of adults) and foodState.adaptationAbsorbableShortfall ≤ 0.95
/// (the dead-zone form (d − a)/(1 − a) needs a &lt; 1; sim.json's loader already refuses a ≥ 1 for the base).</para>
/// <para><b>Direction of pathBuild.dirtPathSpeedFactor.</b> The contract improves it "higher" (faster unpaved paths), but
/// the stored parameter is a COST factor (edge cost = step cost × factor, lower is faster). The registry keeps the
/// contract's direction and cap as the SPEED improvement: the read site divides the cost factor by (1 + Σ)
/// (PathBuildSystem) — the honest reading of "faster".</para>
/// </summary>
public static class KnowledgeOperands
{
    /// <summary>The registry, indexed by <see cref="KnowledgeOperand"/>.</summary>
    public static IReadOnlyList<KnowledgeOperandInfo> All => Rows;

    private static readonly KnowledgeOperandInfo[] Rows =
    [
        new(KnowledgeOperand.FarmingYieldPerArableKm2PerYear, "farming.yieldPerArableKm2PerYear", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "ProductionSystem", "land yields more grain"),
        new(KnowledgeOperand.FarmingOutputPerFarmerPerYear, "farming.outputPerFarmerPerYear", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "ProductionSystem", "each farmer produces more"),
        new(KnowledgeOperand.ProductionOutputPerHerderPerYear, "production.outputPerHerderPerYear", OperandDirection.Higher, 2.0, false, double.PositiveInfinity, "ProductionSystem", "herds yield more"),
        new(KnowledgeOperand.ProductionOutputPerExtractorPerYear, "production.outputPerExtractorPerYear", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "ProductionSystem", "mining, logging, quarrying and fishing yield more"),
        new(KnowledgeOperand.ProductionToolYieldBonusMax, "production.toolYieldBonusMax", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "ProductionSystem", "equipped farmers gain more from tools"),
        new(KnowledgeOperand.ProductionToolWearPerEquippedFarmerPerYear, "production.toolWearPerEquippedFarmerPerYear", OperandDirection.Lower, 0.8, false, double.PositiveInfinity, "ProductionSystem", "tools last longer"),
        new(KnowledgeOperand.ConsumptionGrainSpoilagePerYear, "consumption.grainSpoilagePerYear", OperandDirection.Lower, 0.85, false, double.PositiveInfinity, "ConsumptionSystem", "less grain rots in store"),
        new(KnowledgeOperand.ConsumptionGranaryYearsOfDemand, "consumption.granaryYearsOfDemand", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "ConsumptionSystem", "granaries hold more"),
        new(KnowledgeOperand.HarvestVarianceSigmaLogYield, "harvestVariance.sigmaLogYield", OperandDirection.Lower, 0.6, false, double.PositiveInfinity, "HarvestWeatherSystem", "steadier harvests"),
        new(KnowledgeOperand.GranariesFamineReductionMax, "granaries.famineReductionMax", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "GranaryEffects", "granaries relieve famine more"),
        new(KnowledgeOperand.GranariesFarmYieldBonusMax, "granaries.farmYieldBonusMax", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "GranaryEffects", "stored seed and fodder raise yields"),
        new(KnowledgeOperand.FoodStateAdaptationAbsorbableShortfall, "foodState.adaptationAbsorbableShortfall", OperandDirection.Higher, 1.0, false, 0.95, "FoodState", "households absorb shortfalls"),
        new(KnowledgeOperand.HousingBuildLaborAdultYearsPerDwelling, "housing.buildLaborAdultYearsPerDwelling", OperandDirection.Lower, 0.8, false, double.PositiveInfinity, "HousingSystem", "homes take less labour"),
        new(KnowledgeOperand.HousingBuildTimberPerDwelling, "housing.buildTimberPerDwelling", OperandDirection.Lower, 0.7, false, double.PositiveInfinity, "HousingSystem", "homes take less timber"),
        new(KnowledgeOperand.HousingUpkeepTimberPerDwellingYear, "housing.upkeepTimberPerDwellingYear", OperandDirection.Lower, 0.8, false, double.PositiveInfinity, "HousingSystem", "cheaper upkeep"),
        new(KnowledgeOperand.HousingTauYears, "housing.tauYears", OperandDirection.Higher, 1.5, false, double.PositiveInfinity, "HousingSystem", "buildings last longer"),
        new(KnowledgeOperand.HousingPersonsPerDwelling, "housing.personsPerDwelling", OperandDirection.Higher, 0.5, false, double.PositiveInfinity, "HousingSystem and readers", "denser housing"),
        new(KnowledgeOperand.PathBuildLaborPerAdultPerYear, "pathBuild.laborPerAdultPerYear", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "PathBuildSystem", "more path-building per worker"),
        new(KnowledgeOperand.PathBuildBuildCostMultiplier, "pathBuild.buildCostMultiplier", OperandDirection.Lower, 0.7, false, double.PositiveInfinity, "PathBuildSystem", "paths cheaper to build"),
        new(KnowledgeOperand.PathBuildDirtPathSpeedFactor, "pathBuild.dirtPathSpeedFactor", OperandDirection.Higher, 0.8, false, double.PositiveInfinity, "PathBuildSystem", "faster unpaved paths"),
        new(KnowledgeOperand.TransportRiverCostFactor, "transport.riverCostFactor", OperandDirection.Lower, 0.8, false, double.PositiveInfinity, "transport readers", "cheaper river haulage"),
        new(KnowledgeOperand.RoadsBaselineKmPerDay, "roads.baselineKmPerDay", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "TransportQuery", "faster overland travel"),
        new(KnowledgeOperand.RoadsMaxRouteKm, "roads.maxRouteKm", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "RoadDevelopmentQuery", "longer roads possible"),
        new(KnowledgeOperand.CatchmentHinterlandRadiusKm, "catchment.hinterlandRadiusKm", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "CatchmentSystem", "settlements draw on a wider hinterland"),
        new(KnowledgeOperand.TradeGapClosingFraction, "trade.gapClosingFraction", OperandDirection.Higher, 1.5, false, 0.95, "TradeArbitrageSystem", "trade closes price gaps faster (result ≤ 0.95)"),
        new(KnowledgeOperand.TradeCostPerBulkCostUnit, "trade.costPerBulkCostUnit", OperandDirection.Lower, 0.8, false, double.PositiveInfinity, "TradeArbitrageSystem", "cheaper trade haulage"),
        new(KnowledgeOperand.PriceStockReleaseRatePerYear, "price.stockReleaseRatePerYear", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "Price / Trade", "stocks reach markets faster"),
        new(KnowledgeOperand.DemographicsMortalityPerYear, "demographics.mortalityPerYear", OperandDirection.Lower, 0.7, false, double.PositiveInfinity, "DemographicsSystem", "lower baseline mortality (all cohorts)"),
        new(KnowledgeOperand.DemographicsStarvationMortalityMaxPerYear, "demographics.starvationMortalityMaxPerYear", OperandDirection.Lower, 0.6, false, double.PositiveInfinity, "DemographicsSystem", "fewer famine deaths"),
        new(KnowledgeOperand.DemographicsStarvationChildMultiplier, "demographics.starvationChildMultiplier", OperandDirection.Lower, 0.5, false, double.PositiveInfinity, "DemographicsSystem", "children better protected in famine"),
        new(KnowledgeOperand.InstitutionsUniversitiesMaxMortalityReduction, "institutions.universities.maxMortalityReduction", OperandDirection.Higher, 2.0, false, double.PositiveInfinity, "Institutions", "university medicine saves more lives"),
        new(KnowledgeOperand.InstitutionsUniversitiesMaxResearchCostReduction, "institutions.universities.maxResearchCostReduction", OperandDirection.Higher, 0.5, false, 0.75, "Institutions", "universities cut research cost more (result < 0.75)"),
        new(KnowledgeOperand.InstitutionsUniversitiesMaturityTauYears, "institutions.universities.maturityTauYears", OperandDirection.Lower, 0.5, false, double.PositiveInfinity, "Institutions", "universities mature faster"),
        new(KnowledgeOperand.GovernanceAuthorityDecayCostUnits, "governance.authorityDecayCostUnits", OperandDirection.Higher, 3.0, false, double.PositiveInfinity, "Governance", "administrative reach extends further"),
        new(KnowledgeOperand.GovernanceTaxExtractionResponseMax, "governance.taxExtractionResponseMax", OperandDirection.Higher, 1.5, false, double.PositiveInfinity, "Governance", "taxes extract more output"),
        new(KnowledgeOperand.MigrationBaseRatePerYear, "migration.baseRatePerYear", OperandDirection.Higher, 1.0, false, double.PositiveInfinity, "MigrationSystem", "freer movement of people"),
        new(KnowledgeOperand.MigrationDampingDecayCostUnits, "migration.dampingDecayCostUnits", OperandDirection.Higher, 2.0, false, double.PositiveInfinity, "MigrationSystem", "migrants travel further"),
        new(KnowledgeOperand.MobilityPromoteRatePerYear, "mobility.promoteRatePerYear", OperandDirection.Higher, 1.5, false, double.PositiveInfinity, "ClassMobilitySystem", "faster social promotion"),
        new(KnowledgeOperand.MobilityTargetShareCap, "mobility.targetShareCap", OperandDirection.Higher, 1.0, false, 1.0, "ClassMobilitySystem", "larger specialist classes possible"),
        new(KnowledgeOperand.ColonizationOrderPartyFraction, "colonization.orderPartyFraction", OperandDirection.Higher, 0.5, false, 0.35, "FoundingQuery", "larger founding parties (result ≤ 0.35)"),
        new(KnowledgeOperand.FogExploreStepNodes, "fog.exploreStepNodes", OperandDirection.Higher, 1.0, true, double.PositiveInfinity, "DiscoveryQuery", "each Explore order reaches further"),
        new(KnowledgeOperand.FogExploreMaxRadiusNodes, "fog.exploreMaxRadiusNodes", OperandDirection.Higher, 1.0, true, double.PositiveInfinity, "DiscoveryQuery", "larger exploration range"),
        new(KnowledgeOperand.FogFormationSightNodes, "fog.formationSightNodes", OperandDirection.Higher, 2.0, true, double.PositiveInfinity, "DiscoveryQuery", "formations see further"),
        new(KnowledgeOperand.ResearchCivicsPointsCoefficient, "research.civicsPointsCoefficient", OperandDirection.Higher, 0.3, false, double.PositiveInfinity, "ResearchQuery (CP)", "more Civics Points per person"),
        new(KnowledgeOperand.ResearchResearchPointsCoefficient, "research.researchPointsCoefficient", OperandDirection.Higher, 0.3, false, double.PositiveInfinity, "ResearchQuery (RP)", "more Research Points per person"),
        new(KnowledgeOperand.SocietyGrievanceRelief, "society.grievanceRelief", OperandDirection.Higher, 0.6, false, double.PositiveInfinity, "NeedsGrievanceSystem", "grievance builds more slowly (engine maps to the accumulation term)"),
        new(KnowledgeOperand.SocietyUnrestOutputDrag, "society.unrestOutputDrag", OperandDirection.Lower, 0.6, false, double.PositiveInfinity, "Governance / Unrest", "unrest costs less output"),
    ];

    /// <summary>The number of operands.</summary>
    public static int Count => Rows.Length;

    /// <summary>The registry row of <paramref name="operand"/>.</summary>
    public static KnowledgeOperandInfo Of(KnowledgeOperand operand) => Rows[(int)operand];

    /// <summary>The operand a content id names, or null (ordinal comparison; a linear scan — load time only).</summary>
    public static KnowledgeOperand? FromId(string id)
    {
        for (int i = 0; i < Rows.Length; i++)
            if (string.Equals(Rows[i].Id, id, StringComparison.Ordinal)) return Rows[i].Operand;
        return null;
    }
}
