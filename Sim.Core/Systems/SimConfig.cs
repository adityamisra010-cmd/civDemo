using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Systems;

/// <summary>Raised on any sim-config schema violation, with an actionable message.</summary>
public sealed class SimConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Simulation tuning for the population/food loop (T1.5, cohortized at T2.1;
/// every value TUNE, D-006). Loaded from sim.json on the T0.4 loader template:
/// Sim.Core takes string/Stream, loud actionable errors. Records support `with`
/// so tests derive variants (e.g. the unfed world) from the canonical config.
/// All rates are per-sim-year and integrate against dtYears (law 3).
/// </summary>
public sealed record SimConfig(
    [property: JsonPropertyName("farming")] FarmingConfig Farming,
    [property: JsonPropertyName("consumption")] ConsumptionConfig Consumption,
    [property: JsonPropertyName("demographics")] DemographicsConfig Demographics,
    [property: JsonPropertyName("pathBuild")] PathBuildConfig PathBuild,
    [property: JsonPropertyName("founding")] FoundingConfig Founding,
    [property: JsonPropertyName("registries")] RegistriesConfig Registries,
    [property: JsonPropertyName("mobility")] MobilityConfig Mobility,
    [property: JsonPropertyName("migration")] MigrationConfig Migration);

/// <summary>
/// Farming tuning — Leontief production (T1.8 director-sanctioned spec
/// amendment; the T1.5 form had no labor factor and ghost-harvested in a dead
/// world): harvest/yr = min(farmland × YieldPerFarmlandPerYear,
/// adults × farmShare × OutputPerFarmerPerYear). Land side: what the catchment
/// can yield at full working; labor side: what the assigned farmers can work.
/// Tuned so a fresh seed-42 run is labor-limited early and land-limited at
/// equilibrium. Every leaf is [JsonRequired] (T1.5 adversarial finding):
/// a missing or typo'd key must fail the load loudly, never silently bind 0.0.
/// </summary>
public sealed record FarmingConfig(
    [property: JsonPropertyName("yieldPerFarmlandPerYear"), JsonRequired] double YieldPerFarmlandPerYear,
    [property: JsonPropertyName("outputPerFarmerPerYear"), JsonRequired] double OutputPerFarmerPerYear);

/// <summary>
/// Path-building tuning (T1.6, all TUNE, per-sim-year rates — law 3).
/// LaborPerAdultPerYear: banked build-progress per adult-year of path labor.
/// BuildCostMultiplier: segment build cost = lattice StepCost × this.
/// DirtPathSpeedFactor in (0,1]: a built edge's traversal cost = StepCost × this
/// (the fast lane — must be cheaper than walking to matter).
/// </summary>
public sealed record PathBuildConfig(
    [property: JsonPropertyName("laborPerAdultPerYear"), JsonRequired] double LaborPerAdultPerYear,
    [property: JsonPropertyName("buildCostMultiplier"), JsonRequired] double BuildCostMultiplier,
    [property: JsonPropertyName("dirtPathSpeedFactor"), JsonRequired] double DirtPathSpeedFactor);

/// <summary>
/// Per-cohort consumption weights (T2.1): food per person per sim-year for each
/// of the 16 five-year cohorts (D-015 constants, cohortized). Exactly
/// Cohorts.Count entries, validated at load.
/// </summary>
public sealed record ConsumptionConfig(
    [property: JsonPropertyName("cohortWeights"), JsonRequired] double[] CohortWeights);

/// <summary>
/// Cohort demographic profiles (T2.1, D-026), all per-sim-year, all TUNE
/// (historical retune is T2.7's packet):
///  - FertilityPerPersonPerYear[c]: births per person IN cohort c per year
///    (both sexes pooled). Newborns are credited to the cohorts a dt-window
///    of births actually spans (see DemographicsSystem).
///  - MortalityPerYear[c]: base deaths per person in cohort c per year.
///  - StarvationMortalityMaxPerYear scales with the PREVIOUS turn's
///    consumption-deficit ratio (one-turn lag, §3.2), multiplied by
///    StarvationChildMultiplier on child cohorts and StarvationElderMultiplier
///    on elder cohorts (famine age-selectivity — the acceptance criterion).
/// Aging carries no rate here: cohort width is structural (Cohorts.WidthYears)
/// and the slot-advance integration derives everything from dt (law 3).
/// T2.7 famine fertility response (all TUNE):
///  - FamineFertilitySuppressionSlope: conceptions scale by
///    max(0, 1 − slope × deficit) during a deficit — famine suppresses births
///    (amenorrhea, deferral), a coefficient INSIDE the birth equation (law 2).
///  - ReboundRecoverableFraction: the share of suppressed exact births banked
///    into the group's ReboundReservoir (the rest are conceptions permanently
///    lost to aging-out and death — magnitude of the rebound).
///  - ReboundReleaseRatePerYear: fed-turn drain rate of the reservoir back
///    into the births flow (duration of the rebound; at Neolithic dt = 10 a
///    rate ≥ 0.1 releases the bulk in the first fed decade).
/// </summary>
public sealed record DemographicsConfig(
    [property: JsonPropertyName("fertilityPerPersonPerYear"), JsonRequired] double[] FertilityPerPersonPerYear,
    [property: JsonPropertyName("mortalityPerYear"), JsonRequired] double[] MortalityPerYear,
    [property: JsonPropertyName("starvationMortalityMaxPerYear"), JsonRequired] double StarvationMortalityMaxPerYear,
    [property: JsonPropertyName("starvationChildMultiplier"), JsonRequired] double StarvationChildMultiplier,
    [property: JsonPropertyName("starvationElderMultiplier"), JsonRequired] double StarvationElderMultiplier,
    [property: JsonPropertyName("famineFertilitySuppressionSlope"), JsonRequired] double FamineFertilitySuppressionSlope,
    [property: JsonPropertyName("reboundRecoverableFraction"), JsonRequired] double ReboundRecoverableFraction,
    [property: JsonPropertyName("reboundReleaseRatePerYear"), JsonRequired] double ReboundReleaseRatePerYear);

/// <summary>
/// The founding endowment per settlement: people per cohort (exactly
/// Cohorts.Count entries — data-explicit, no apportionment code) and food
/// units. The whole founding population belongs to the FIRST registered class
/// (the always-on base class, D-027); other classes found at zero.
/// </summary>
public sealed record FoundingConfig(
    [property: JsonPropertyName("cohortCounts"), JsonRequired] long[] CohortCounts,
    [property: JsonPropertyName("foodStore"), JsonRequired] long FoodStore);

/// <summary>One registry entry: a stable id and a display name (ADR-001: names
/// live in config/registries, never in sim rows).</summary>
public sealed record RegistryEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name);

/// <summary>
/// One class-registry entry (T2.2, D-020/D-027): id + name plus OPTIONAL
/// emergence and recession predicates in the D-020 DSL. Emerge absent = the
/// class is always active (the base class). Recede absent = once emerged,
/// never recedes. Both are parsed and validated AT LOAD (unknown variables and
/// malformed expressions reject loudly); the hysteresis SHAPE is emerge X /
/// recede Y with Y &lt; X — the band between them is the latch.
/// </summary>
public sealed record ClassEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("emerge")] string? Emerge = null,
    [property: JsonPropertyName("recede")] string? Recede = null);

/// <summary>
/// Class-mobility tuning (T2.2, all TUNE, per-sim-year rates — law 3).
/// Target artisan share = min(TargetShareCap, TargetShareSlope × (surplus − 1))
/// clamped at 0 — a capped saturating function of the published surplus ratio.
/// The live share relaxes toward the target at PromoteRatePerYear (fraction of
/// the gap closed per year); a famine (Prev deficit &gt; 0) forces demotion at
/// FamineDemoteRatePerYear regardless of predicates — artisans starve back to
/// the fields first. SCAFFOLDING (spec §1, M3 replaces): artisans contribute
/// ConstructionLaborWeight × artisanAdults to PathBuild's pool and a farming
/// tool multiplier 1 + min(ToolYieldBonusCap, ToolYieldBonusSlope × share).
/// </summary>
public sealed record MobilityConfig(
    [property: JsonPropertyName("promoteRatePerYear"), JsonRequired] double PromoteRatePerYear,
    [property: JsonPropertyName("famineDemoteRatePerYear"), JsonRequired] double FamineDemoteRatePerYear,
    [property: JsonPropertyName("targetShareSlope"), JsonRequired] double TargetShareSlope,
    [property: JsonPropertyName("targetShareCap"), JsonRequired] double TargetShareCap,
    [property: JsonPropertyName("toolYieldBonusCap"), JsonRequired] double ToolYieldBonusCap,
    [property: JsonPropertyName("toolYieldBonusSlope"), JsonRequired] double ToolYieldBonusSlope,
    [property: JsonPropertyName("constructionLaborWeight"), JsonRequired] double ConstructionLaborWeight);

/// <summary>
/// The culture/religion/class registries (T2.1, D-027 incremental delivery):
/// M2 ships one placeholder culture, one placeholder religion, and the
/// Peasants + Artisans classes. Buckets are instantiated for the full cross
/// product; entry ORDER is the deterministic iteration order everywhere.
/// </summary>
public sealed record RegistriesConfig(
    [property: JsonPropertyName("cultures"), JsonRequired] RegistryEntry[] Cultures,
    [property: JsonPropertyName("religions"), JsonRequired] RegistryEntry[] Religions,
    [property: JsonPropertyName("classes"), JsonRequired] ClassEntry[] Classes);

/// <summary>
/// Migration tuning (T2.5, all TUNE, per-sim-year — law 3).
/// Desired outflow source→dest per bucket, per year:
///   BaseRatePerYear × CohortProfile[cohort] × count × damping ×
///   (max(0, A_dest − A_src) + FamineFlightFactor × Prev deficit_src)
/// where damping = exp(−travelCost / DampingDecayCost) (∞ cost ⇒ exactly 0)
/// and attractiveness A = FoodWeight × foodPerCapita + LandWeight ×
/// farmlandPerCapita (Prev reads, capita floored at 1). The deficit term is
/// the D-021 Exit valve: gap-INDEPENDENT — starving people leave for anywhere
/// reachable, weighted by damping alone when no gap is positive.
/// CohortProfile is the young-adult-peaked migration propensity (16 entries).
/// </summary>
public sealed record MigrationConfig(
    [property: JsonPropertyName("baseRatePerYear"), JsonRequired] double BaseRatePerYear,
    [property: JsonPropertyName("dampingDecayCost"), JsonRequired] double DampingDecayCost,
    [property: JsonPropertyName("attractivenessFoodWeight"), JsonRequired] double AttractivenessFoodWeight,
    [property: JsonPropertyName("attractivenessLandWeight"), JsonRequired] double AttractivenessLandWeight,
    [property: JsonPropertyName("famineFlightFactor"), JsonRequired] double FamineFlightFactor,
    [property: JsonPropertyName("cohortProfile"), JsonRequired] double[] CohortProfile);

public static class SimConfigLoader
{
    public static SimConfig Load(Stream json)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd());
    }

    public static SimConfig Load(string json)
    {
        SimConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<SimConfig>(json);
        }
        catch (JsonException e)
        {
            // Covers both malformed JSON and [JsonRequired] misses — the inner
            // message names the missing properties, so the error stays actionable.
            throw new SimConfigException(
                $"sim config is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (cfg is null) throw new SimConfigException("sim config is empty.");

        if (cfg.Farming is null) throw new SimConfigException("farming is missing.");
        RequireRate("farming.yieldPerFarmlandPerYear", cfg.Farming.YieldPerFarmlandPerYear);
        RequireRate("farming.outputPerFarmerPerYear", cfg.Farming.OutputPerFarmerPerYear);

        if (cfg.Consumption is null) throw new SimConfigException("consumption is missing.");
        RequireCohortArray("consumption.cohortWeights", cfg.Consumption.CohortWeights);

        if (cfg.Demographics is null) throw new SimConfigException("demographics is missing.");
        RequireCohortArray("demographics.fertilityPerPersonPerYear", cfg.Demographics.FertilityPerPersonPerYear);
        RequireCohortArray("demographics.mortalityPerYear", cfg.Demographics.MortalityPerYear);
        RequireRate("demographics.starvationMortalityMaxPerYear", cfg.Demographics.StarvationMortalityMaxPerYear);
        RequireRate("demographics.starvationChildMultiplier", cfg.Demographics.StarvationChildMultiplier);
        RequireRate("demographics.starvationElderMultiplier", cfg.Demographics.StarvationElderMultiplier);
        RequireRate("demographics.famineFertilitySuppressionSlope", cfg.Demographics.FamineFertilitySuppressionSlope);
        RequireRate("demographics.reboundReleaseRatePerYear", cfg.Demographics.ReboundReleaseRatePerYear);
        if (!(cfg.Demographics.ReboundRecoverableFraction >= 0.0 && cfg.Demographics.ReboundRecoverableFraction <= 1.0))
            throw new SimConfigException(
                $"demographics.reboundRecoverableFraction must be in [0,1] (a fraction of suppressed conceptions" +
                $" cannot exceed what was suppressed), got {Inv(cfg.Demographics.ReboundRecoverableFraction)}.");

        if (cfg.PathBuild is null) throw new SimConfigException("pathBuild is missing.");
        RequireRate("pathBuild.laborPerAdultPerYear", cfg.PathBuild.LaborPerAdultPerYear);
        if (cfg.PathBuild.BuildCostMultiplier <= 0 || !double.IsFinite(cfg.PathBuild.BuildCostMultiplier))
            throw new SimConfigException(
                $"pathBuild.buildCostMultiplier must be a finite value > 0, got {Inv(cfg.PathBuild.BuildCostMultiplier)}.");
        if (!(cfg.PathBuild.DirtPathSpeedFactor > 0.0 && cfg.PathBuild.DirtPathSpeedFactor <= 1.0))
            throw new SimConfigException(
                $"pathBuild.dirtPathSpeedFactor must be in (0,1], got {Inv(cfg.PathBuild.DirtPathSpeedFactor)}.");

        if (cfg.Founding is null) throw new SimConfigException("founding is missing.");
        if (cfg.Founding.CohortCounts is null || cfg.Founding.CohortCounts.Length != State.Cohorts.Count)
            throw new SimConfigException(
                $"founding.cohortCounts must have exactly {State.Cohorts.Count} entries, got " +
                $"{cfg.Founding.CohortCounts?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}.");
        foreach (long c in cfg.Founding.CohortCounts)
            if (c < 0) throw new SimConfigException("founding.cohortCounts entries must be >= 0.");
        if (cfg.Founding.FoodStore < 0)
            throw new SimConfigException("founding.foodStore must be >= 0.");

        if (cfg.Registries is null) throw new SimConfigException("registries is missing.");
        ValidateRegistry("registries.cultures", cfg.Registries.Cultures);
        ValidateRegistry("registries.religions", cfg.Registries.Religions);
        ValidateClasses("registries.classes", cfg.Registries.Classes);

        if (cfg.Mobility is null) throw new SimConfigException("mobility is missing.");
        RequireRate("mobility.promoteRatePerYear", cfg.Mobility.PromoteRatePerYear);
        RequireRate("mobility.famineDemoteRatePerYear", cfg.Mobility.FamineDemoteRatePerYear);
        RequireRate("mobility.targetShareSlope", cfg.Mobility.TargetShareSlope);
        RequireRate("mobility.toolYieldBonusCap", cfg.Mobility.ToolYieldBonusCap);
        RequireRate("mobility.toolYieldBonusSlope", cfg.Mobility.ToolYieldBonusSlope);
        RequireRate("mobility.constructionLaborWeight", cfg.Mobility.ConstructionLaborWeight);
        if (!(cfg.Mobility.TargetShareCap >= 0.0 && cfg.Mobility.TargetShareCap < 1.0))
            throw new SimConfigException(
                $"mobility.targetShareCap must be in [0,1), got {Inv(cfg.Mobility.TargetShareCap)}.");

        if (cfg.Migration is null) throw new SimConfigException("migration is missing.");
        RequireRate("migration.baseRatePerYear", cfg.Migration.BaseRatePerYear);
        RequireRate("migration.attractivenessFoodWeight", cfg.Migration.AttractivenessFoodWeight);
        RequireRate("migration.attractivenessLandWeight", cfg.Migration.AttractivenessLandWeight);
        RequireRate("migration.famineFlightFactor", cfg.Migration.FamineFlightFactor);
        if (!(cfg.Migration.DampingDecayCost > 0.0) || !double.IsFinite(cfg.Migration.DampingDecayCost))
            throw new SimConfigException(
                $"migration.dampingDecayCost must be a finite value > 0, got {Inv(cfg.Migration.DampingDecayCost)}.");
        RequireCohortArray("migration.cohortProfile", cfg.Migration.CohortProfile);

        return cfg;
    }

    private static void ValidateClasses(string name, ClassEntry[]? entries)
    {
        if (entries is null || entries.Length == 0)
            throw new SimConfigException($"{name} must have at least one entry.");
        for (int i = 0; i < entries.Length; i++)
        {
            ClassEntry e = entries[i];
            if (e is null) throw new SimConfigException($"{name}[{i}] is null.");
            if (e.Id <= 0)
                throw new SimConfigException($"{name}[{i}].id must be > 0, got {e.Id}.");
            if (string.IsNullOrWhiteSpace(e.Name))
                throw new SimConfigException($"{name}[{i}].name must be non-empty.");
            if (i > 0 && entries[i].Id <= entries[i - 1].Id)
                throw new SimConfigException(
                    $"{name} ids must be strictly ascending: [{i - 1}].id {entries[i - 1].Id} >= [{i}].id {entries[i].Id}.");
            // D-020 load-time validation: predicates parse HERE, loudly.
            foreach ((string? src, string leaf) in new[] { (e.Emerge, "emerge"), (e.Recede, "recede") })
            {
                if (src is null) continue;
                try
                {
                    _ = ClassMobility.Predicate.Parse(src);
                }
                catch (ClassMobility.PredicateFormatException ex)
                {
                    throw new SimConfigException($"{name}[{i}] ({e.Name}).{leaf}: {ex.Message}", ex);
                }
            }
        }
        if (entries[0].Emerge is not null)
            throw new SimConfigException(
                $"{name}[0] ({entries[0].Name}) is the base class and must have no emerge predicate.");
    }

    private static void ValidateRegistry(string name, RegistryEntry[]? entries)
    {
        if (entries is null || entries.Length == 0)
            throw new SimConfigException($"{name} must have at least one entry.");
        for (int i = 0; i < entries.Length; i++)
        {
            RegistryEntry e = entries[i];
            if (e is null) throw new SimConfigException($"{name}[{i}] is null.");
            if (e.Id <= 0)
                throw new SimConfigException($"{name}[{i}].id must be > 0, got {e.Id}.");
            if (string.IsNullOrWhiteSpace(e.Name))
                throw new SimConfigException($"{name}[{i}].name must be non-empty.");
            // Strictly ascending ids: uniqueness AND a stable deterministic order
            // in one check (entry order is the iteration order everywhere).
            if (i > 0 && entries[i].Id <= entries[i - 1].Id)
                throw new SimConfigException(
                    $"{name} ids must be strictly ascending: [{i - 1}].id {entries[i - 1].Id} >= [{i}].id {entries[i].Id}.");
        }
    }

    private static void RequireCohortArray(string name, double[]? values)
    {
        if (values is null || values.Length != State.Cohorts.Count)
            throw new SimConfigException(
                $"{name} must have exactly {State.Cohorts.Count} entries (one per five-year cohort), got " +
                $"{values?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}.");
        for (int i = 0; i < values.Length; i++)
        {
            if (double.IsNaN(values[i]) || double.IsInfinity(values[i]) || values[i] < 0.0)
                throw new SimConfigException($"{name}[{i}] must be a finite value >= 0, got {Inv(values[i])}.");
        }
    }

    private static void RequireRate(string name, double value)
    {
        // Finite and non-negative: NaN/Infinity in a rate poisons every stock it
        // touches ("never NaN" is an acceptance criterion, so it is a load error).
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            throw new SimConfigException($"{name} must be a finite value >= 0, got {Inv(value)}.");
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
