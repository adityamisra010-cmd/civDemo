using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Systems;

/// <summary>Raised on any sim-config schema violation, with an actionable message.</summary>
public sealed class SimConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Simulation tuning for the M1 population/food loop (T1.5; every value TUNE,
/// D-006). Loaded from sim.json on the T0.4 loader template: Sim.Core takes
/// string/Stream, loud actionable errors. Records support `with` so tests derive
/// variants (e.g. the unfed world) from the canonical config.
/// All rates are per-sim-year and integrate against dtYears (law 3).
/// </summary>
public sealed record SimConfig(
    [property: JsonPropertyName("farming")] FarmingConfig Farming,
    [property: JsonPropertyName("consumption")] ConsumptionConfig Consumption,
    [property: JsonPropertyName("demographics")] DemographicsConfig Demographics,
    [property: JsonPropertyName("founding")] FoundingConfig Founding);

/// <summary>
/// Farming tuning. FarmLaborShareDefault is the share of labor on the farms this
/// packet — the LaborAllocationOrder overrides it from T1.6. YieldPerFarmlandPerYear
/// converts one unit of effective farmland (block-mean fertility, T1.4) into food
/// units per sim-year (1 food = 1 person-year, D-015 constants).
/// Every leaf is [JsonRequired] (T1.5 adversarial finding): a missing or typo'd
/// key must fail the load loudly, never silently bind as 0.0.
/// </summary>
public sealed record FarmingConfig(
    [property: JsonPropertyName("farmLaborShareDefault"), JsonRequired] double FarmLaborShareDefault,
    [property: JsonPropertyName("yieldPerFarmlandPerYear"), JsonRequired] double YieldPerFarmlandPerYear);

/// <summary>Band consumption weights, food per person per sim-year (D-015 constants).</summary>
public sealed record ConsumptionConfig(
    [property: JsonPropertyName("childWeight"), JsonRequired] double ChildWeight,
    [property: JsonPropertyName("adultWeight"), JsonRequired] double AdultWeight,
    [property: JsonPropertyName("elderWeight"), JsonRequired] double ElderWeight);

/// <summary>
/// Demographic rates, all per-sim-year. Aging rates are nominally 1/bandWidth
/// (1/15 for children→adults, 1/45 for adults→elders) — still TUNE data.
/// StarvationMortalityMaxPerYear scales linearly with the PREVIOUS turn's
/// consumption-deficit ratio (one-turn lag, §3.2).
/// </summary>
public sealed record DemographicsConfig(
    [property: JsonPropertyName("birthsPerAdultPerYear"), JsonRequired] double BirthsPerAdultPerYear,
    [property: JsonPropertyName("childMortalityPerYear"), JsonRequired] double ChildMortalityPerYear,
    [property: JsonPropertyName("adultMortalityPerYear"), JsonRequired] double AdultMortalityPerYear,
    [property: JsonPropertyName("elderMortalityPerYear"), JsonRequired] double ElderMortalityPerYear,
    [property: JsonPropertyName("agingChildToAdultPerYear"), JsonRequired] double AgingChildToAdultPerYear,
    [property: JsonPropertyName("agingAdultToElderPerYear"), JsonRequired] double AgingAdultToElderPerYear,
    [property: JsonPropertyName("starvationMortalityMaxPerYear"), JsonRequired] double StarvationMortalityMaxPerYear);

/// <summary>The founding endowment per settlement (people by band; food units).</summary>
public sealed record FoundingConfig(
    [property: JsonPropertyName("children"), JsonRequired] long Children,
    [property: JsonPropertyName("adults"), JsonRequired] long Adults,
    [property: JsonPropertyName("elders"), JsonRequired] long Elders,
    [property: JsonPropertyName("foodStore"), JsonRequired] long FoodStore);

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
        RequireRate("farming.farmLaborShareDefault", cfg.Farming.FarmLaborShareDefault);
        if (cfg.Farming.FarmLaborShareDefault > 1.0)
            throw new SimConfigException(
                $"farming.farmLaborShareDefault must be <= 1, got {Inv(cfg.Farming.FarmLaborShareDefault)}.");
        RequireRate("farming.yieldPerFarmlandPerYear", cfg.Farming.YieldPerFarmlandPerYear);

        if (cfg.Consumption is null) throw new SimConfigException("consumption is missing.");
        RequireRate("consumption.childWeight", cfg.Consumption.ChildWeight);
        RequireRate("consumption.adultWeight", cfg.Consumption.AdultWeight);
        RequireRate("consumption.elderWeight", cfg.Consumption.ElderWeight);

        if (cfg.Demographics is null) throw new SimConfigException("demographics is missing.");
        RequireRate("demographics.birthsPerAdultPerYear", cfg.Demographics.BirthsPerAdultPerYear);
        RequireRate("demographics.childMortalityPerYear", cfg.Demographics.ChildMortalityPerYear);
        RequireRate("demographics.adultMortalityPerYear", cfg.Demographics.AdultMortalityPerYear);
        RequireRate("demographics.elderMortalityPerYear", cfg.Demographics.ElderMortalityPerYear);
        RequireRate("demographics.agingChildToAdultPerYear", cfg.Demographics.AgingChildToAdultPerYear);
        RequireRate("demographics.agingAdultToElderPerYear", cfg.Demographics.AgingAdultToElderPerYear);
        RequireRate("demographics.starvationMortalityMaxPerYear", cfg.Demographics.StarvationMortalityMaxPerYear);

        if (cfg.Founding is null) throw new SimConfigException("founding is missing.");
        if (cfg.Founding.Children < 0 || cfg.Founding.Adults < 0 || cfg.Founding.Elders < 0
            || cfg.Founding.FoodStore < 0)
            throw new SimConfigException("founding values (children, adults, elders, foodStore) must be >= 0.");

        return cfg;
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
