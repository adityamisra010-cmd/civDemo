using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Worldgen;

/// <summary>Raised on any worldgen-config schema violation, with an actionable message.</summary>
public sealed class WorldgenConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Noise parameters for one fBm field (all TUNE).</summary>
public sealed record NoiseConfig(
    [property: JsonPropertyName("octaves")] int Octaves,
    [property: JsonPropertyName("frequency")] double Frequency,
    [property: JsonPropertyName("persistence")] double Persistence,
    [property: JsonPropertyName("lacunarity")] double Lacunarity);

/// <summary>
/// Worldgen tuning (D-015/D-022; every value TUNE). Loaded from worldgen.json on
/// the T0.4 template: Sim.Core takes string/Stream, loud actionable errors.
/// Records support `with` so tests derive the 256² dev preset from the canonical
/// config (D-015).
/// </summary>
public sealed record WorldgenConfig(
    [property: JsonPropertyName("sizePx")] int SizePx,
    [property: JsonPropertyName("kmPerPx")] double KmPerPx,
    [property: JsonPropertyName("landFractionTarget")] double LandFractionTarget,
    [property: JsonPropertyName("landFractionMin")] double LandFractionMin,
    [property: JsonPropertyName("landFractionMax")] double LandFractionMax,
    [property: JsonPropertyName("continentalMask")] MaskConfig ContinentalMask,
    [property: JsonPropertyName("elevation")] NoiseConfig Elevation,
    [property: JsonPropertyName("temperature")] TemperatureConfig Temperature,
    [property: JsonPropertyName("moistureDecayPx")] double MoistureDecayPx,
    [property: JsonPropertyName("movement")] MovementConfig Movement,
    [property: JsonPropertyName("rivers")] RiversConfig Rivers);

/// <summary>River extraction tuning (T1.2, all TUNE).</summary>
public sealed record RiversConfig(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("minAccumulationFraction")] double MinAccumulationFraction,
    [property: JsonPropertyName("adjacencyRadiusPx")] int AdjacencyRadiusPx,
    [property: JsonPropertyName("fertilityBoost")] double FertilityBoost,
    [property: JsonPropertyName("cellFractionMin")] double CellFractionMin,
    [property: JsonPropertyName("cellFractionMax")] double CellFractionMax);

public sealed record MaskConfig(
    [property: JsonPropertyName("noise")] NoiseConfig Noise,
    [property: JsonPropertyName("noiseWeight")] double NoiseWeight,
    [property: JsonPropertyName("radialWeight")] double RadialWeight);

public sealed record TemperatureConfig(
    [property: JsonPropertyName("equatorC")] double EquatorC,
    [property: JsonPropertyName("poleDropC")] double PoleDropC,
    [property: JsonPropertyName("lapsePerElevC")] double LapsePerElevC,
    [property: JsonPropertyName("fertilityOptimalC")] double FertilityOptimalC,
    [property: JsonPropertyName("fertilityToleranceC")] double FertilityToleranceC);

public sealed record MovementConfig(
    [property: JsonPropertyName("baseCost")] double BaseCost,
    [property: JsonPropertyName("slopeFactor")] double SlopeFactor,
    [property: JsonPropertyName("waterCost")] double WaterCost);

public static class WorldgenConfigLoader
{
    public static WorldgenConfig Load(Stream json)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd());
    }

    public static WorldgenConfig Load(string json)
    {
        WorldgenConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<WorldgenConfig>(json);
        }
        catch (JsonException e)
        {
            throw new WorldgenConfigException($"worldgen config is not valid JSON: {e.Message}", e);
        }
        if (cfg is null) throw new WorldgenConfigException("worldgen config is empty.");

        if (cfg.SizePx < 16)
            throw new WorldgenConfigException($"sizePx must be >= 16, got {cfg.SizePx}.");
        if (cfg.KmPerPx <= 0)
            throw new WorldgenConfigException($"kmPerPx must be > 0, got {Inv(cfg.KmPerPx)}.");
        if (!(0 < cfg.LandFractionMin && cfg.LandFractionMin <= cfg.LandFractionTarget
              && cfg.LandFractionTarget <= cfg.LandFractionMax && cfg.LandFractionMax < 1))
            throw new WorldgenConfigException(
                $"land fraction bounds must satisfy 0 < min <= target <= max < 1, got " +
                $"min {Inv(cfg.LandFractionMin)}, target {Inv(cfg.LandFractionTarget)}, max {Inv(cfg.LandFractionMax)}.");
        ValidateNoise("elevation", cfg.Elevation);
        if (cfg.ContinentalMask is null)
            throw new WorldgenConfigException("continentalMask is missing.");
        ValidateNoise("continentalMask.noise", cfg.ContinentalMask.Noise);
        if (cfg.Temperature is null)
            throw new WorldgenConfigException("temperature is missing.");
        if (cfg.Temperature.FertilityToleranceC <= 0)
            throw new WorldgenConfigException(
                $"temperature.fertilityToleranceC must be > 0, got {Inv(cfg.Temperature.FertilityToleranceC)}.");
        if (cfg.MoistureDecayPx <= 0)
            throw new WorldgenConfigException($"moistureDecayPx must be > 0, got {Inv(cfg.MoistureDecayPx)}.");
        if (cfg.Movement is null)
            throw new WorldgenConfigException("movement is missing.");
        if (cfg.Movement.BaseCost <= 0 || cfg.Movement.WaterCost <= 0)
            throw new WorldgenConfigException("movement.baseCost and movement.waterCost must be > 0.");
        if (cfg.Rivers is null)
            throw new WorldgenConfigException("rivers is missing.");
        if (cfg.Rivers.Count < 1)
            throw new WorldgenConfigException($"rivers.count must be >= 1, got {cfg.Rivers.Count}.");
        if (cfg.Rivers.MinAccumulationFraction <= 0 || cfg.Rivers.MinAccumulationFraction >= 1)
            throw new WorldgenConfigException(
                $"rivers.minAccumulationFraction must be in (0,1), got {Inv(cfg.Rivers.MinAccumulationFraction)}.");
        if (cfg.Rivers.AdjacencyRadiusPx < 0)
            throw new WorldgenConfigException($"rivers.adjacencyRadiusPx must be >= 0, got {cfg.Rivers.AdjacencyRadiusPx}.");
        if (cfg.Rivers.FertilityBoost < 0)
            throw new WorldgenConfigException($"rivers.fertilityBoost must be >= 0, got {Inv(cfg.Rivers.FertilityBoost)}.");
        if (!(0 <= cfg.Rivers.CellFractionMin && cfg.Rivers.CellFractionMin <= cfg.Rivers.CellFractionMax
              && cfg.Rivers.CellFractionMax < 1))
            throw new WorldgenConfigException(
                $"rivers cell-fraction bounds must satisfy 0 <= min <= max < 1, got " +
                $"min {Inv(cfg.Rivers.CellFractionMin)}, max {Inv(cfg.Rivers.CellFractionMax)}.");

        return cfg;
    }

    private static void ValidateNoise(string name, NoiseConfig? n)
    {
        if (n is null)
            throw new WorldgenConfigException($"{name} is missing.");
        if (n.Octaves is < 1 or > 16)
            throw new WorldgenConfigException($"{name}.octaves must be in 1..16, got {n.Octaves}.");
        if (n.Frequency <= 0 || n.Persistence <= 0 || n.Lacunarity <= 0)
            throw new WorldgenConfigException(
                $"{name}: frequency, persistence, lacunarity must all be > 0.");
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
