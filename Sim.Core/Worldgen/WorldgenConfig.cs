using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Worldgen;

/// <summary>Raised on any worldgen-config schema violation, with an actionable message.</summary>
public sealed class WorldgenConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Noise parameters for one fBm field (all TUNE).</summary>
public sealed record NoiseConfig(
    [property: JsonPropertyName("octaves"), JsonRequired] int Octaves,
    [property: JsonPropertyName("frequency"), JsonRequired] double Frequency,
    [property: JsonPropertyName("persistence"), JsonRequired] double Persistence,
    [property: JsonPropertyName("lacunarity"), JsonRequired] double Lacunarity);

/// <summary>
/// Worldgen tuning (D-015/D-022; every value TUNE). Loaded from worldgen.json on
/// the T0.4 template: Sim.Core takes string/Stream, loud actionable errors.
/// Records support `with` so tests derive the 256² dev preset from the canonical
/// config (D-015).
/// </summary>
public sealed record WorldgenConfig(
    [property: JsonPropertyName("sizePx"), JsonRequired] int SizePx,
    [property: JsonPropertyName("kmPerPx"), JsonRequired] double KmPerPx,
    [property: JsonPropertyName("landFractionTarget"), JsonRequired] double LandFractionTarget,
    [property: JsonPropertyName("landFractionMin"), JsonRequired] double LandFractionMin,
    [property: JsonPropertyName("landFractionMax"), JsonRequired] double LandFractionMax,
    [property: JsonPropertyName("continentalMask"), JsonRequired] MaskConfig ContinentalMask,
    [property: JsonPropertyName("elevation"), JsonRequired] NoiseConfig Elevation,
    [property: JsonPropertyName("temperature"), JsonRequired] TemperatureConfig Temperature,
    [property: JsonPropertyName("moistureDecayPx"), JsonRequired] double MoistureDecayPx,
    [property: JsonPropertyName("movement"), JsonRequired] MovementConfig Movement,
    [property: JsonPropertyName("rivers"), JsonRequired] RiversConfig Rivers,
    [property: JsonPropertyName("siting"), JsonRequired] SitingConfig Siting);

/// <summary>
/// Settlement-siting tuning (T1.4; pluralized at T2.3 per D-025, all TUNE).
/// WaterAccessCutoffPx: BFS grid distance to water beyond which the access
/// score is 0 (score falls linearly from 1 at the shoreline to 0 at the
/// cutoff). SettlementCount: sites founded per world (canonical 12; the D-015
/// dev preset overrides to 4). MinSpacingKm: minimum separation between any two
/// accepted sites, as an IDEAL-GROUND distance in km (T3.2b denomination: it was
/// `minSpacingTravel` in the pathfinder's internal cost units, the same
/// unreadable denomination as the retired catchment travel budget). Converted to
/// cost units at the LatticeGeometry chokepoint, so difficult country separates
/// settlements at a shorter map distance than easy country does.
/// </summary>
/// <summary>ScoreJitter (T3.1c founding variation): amplitude of the seeded
/// multiplicative jitter on every cell's siting score — score × (1 + j·u),
/// u ∈ [−1,1] from a deterministic SplitMix64 hash of (seed, cell). At 0 the
/// argmax is the pure fertility×access ranking (the M2 behavior, which sited
/// twelve near-identical coastal towns); a modest j lets lower-scored inland
/// and riverine sites win sometimes, so worlds stop being twelve copies of
/// the same town. TUNE.</summary>
/// <summary>ScoreFloorPercentile (T3.1c): candidates whose UNJITTERED score
/// sits below this percentile of all positive-score land cells are ineligible
/// — the jitter varies WHICH GOOD site wins, it does not gamble founders onto
/// ground that cannot carry them. MEASURED FINDING without the floor: jitter
/// occasionally accepted sites with carrying capacity below the founding
/// population, and the colony crashed 90%+ within five turns — read by the
/// Malthus battery as firstCrashTurn 5 against its [400, 800] band. TUNE.</summary>
public sealed record SitingConfig(
    [property: JsonPropertyName("waterAccessCutoffPx"), JsonRequired] int WaterAccessCutoffPx,
    [property: JsonPropertyName("settlementCount"), JsonRequired] int SettlementCount,
    [property: JsonPropertyName("minSpacingKm"), JsonRequired] double MinSpacingKm,
    [property: JsonPropertyName("scoreJitter"), JsonRequired] double ScoreJitter,
    [property: JsonPropertyName("scoreFloorPercentile"), JsonRequired] double ScoreFloorPercentile,
    [property: JsonPropertyName("fertilityRadiusPx"), JsonRequired] int FertilityRadiusPx);

/// <summary>River extraction tuning (T1.2, all TUNE).</summary>
public sealed record RiversConfig(
    [property: JsonPropertyName("count"), JsonRequired] int Count,
    [property: JsonPropertyName("minAccumulationFraction"), JsonRequired] double MinAccumulationFraction,
    [property: JsonPropertyName("adjacencyRadiusPx"), JsonRequired] int AdjacencyRadiusPx,
    [property: JsonPropertyName("fertilityBoost"), JsonRequired] double FertilityBoost,
    [property: JsonPropertyName("cellFractionMin"), JsonRequired] double CellFractionMin,
    [property: JsonPropertyName("cellFractionMax"), JsonRequired] double CellFractionMax);

/// <summary>EdgeTaperPx (T3.1b): width of the smoothstep ramp crushing
/// elevation to 0 at the world boundary — the guaranteed ocean margin (the
/// radial term alone is zero only at corners, so continents truncated at the
/// map edge were possible; m3-spec §4 T3.1 closes that).</summary>
public sealed record MaskConfig(
    [property: JsonPropertyName("noise"), JsonRequired] NoiseConfig Noise,
    [property: JsonPropertyName("noiseWeight"), JsonRequired] double NoiseWeight,
    [property: JsonPropertyName("radialWeight"), JsonRequired] double RadialWeight,
    [property: JsonPropertyName("edgeTaperPx"), JsonRequired] int EdgeTaperPx);

public sealed record TemperatureConfig(
    [property: JsonPropertyName("equatorC"), JsonRequired] double EquatorC,
    [property: JsonPropertyName("poleDropC"), JsonRequired] double PoleDropC,
    [property: JsonPropertyName("lapsePerElevC"), JsonRequired] double LapsePerElevC,
    [property: JsonPropertyName("fertilityOptimalC"), JsonRequired] double FertilityOptimalC,
    [property: JsonPropertyName("fertilityToleranceC"), JsonRequired] double FertilityToleranceC);

public sealed record MovementConfig(
    [property: JsonPropertyName("baseCost"), JsonRequired] double BaseCost,
    [property: JsonPropertyName("slopeFactor"), JsonRequired] double SlopeFactor,
    [property: JsonPropertyName("waterCost"), JsonRequired] double WaterCost);

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
            // Covers both malformed JSON and [JsonRequired] misses (T1.5 defect
            // class: a missing/typo'd key must never silently bind as 0).
            throw new WorldgenConfigException(
                $"worldgen config is not valid JSON or is missing required values: {e.Message}", e);
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
        if (cfg.Siting is null)
            throw new WorldgenConfigException("siting is missing.");
        if (cfg.Siting.WaterAccessCutoffPx < 1)
            throw new WorldgenConfigException(
                $"siting.waterAccessCutoffPx must be >= 1, got {cfg.Siting.WaterAccessCutoffPx}.");
        if (cfg.Siting.SettlementCount < 1)
            throw new WorldgenConfigException(
                $"siting.settlementCount must be >= 1, got {cfg.Siting.SettlementCount}.");
        if (!(cfg.Siting.MinSpacingKm >= 0.0) || !double.IsFinite(cfg.Siting.MinSpacingKm))
            throw new WorldgenConfigException(
                $"siting.minSpacingKm must be a finite distance >= 0, got {Inv(cfg.Siting.MinSpacingKm)}.");
        if (cfg.Siting.FertilityRadiusPx is < 0 or > 32)
            throw new WorldgenConfigException(
                $"siting.fertilityRadiusPx must be in 0..32, got {cfg.Siting.FertilityRadiusPx}.");
        if (!(cfg.Siting.ScoreFloorPercentile >= 0.0 && cfg.Siting.ScoreFloorPercentile < 1.0))
            throw new WorldgenConfigException(
                $"siting.scoreFloorPercentile must be in [0,1), got {Inv(cfg.Siting.ScoreFloorPercentile)}.");
        if (!(cfg.Siting.ScoreJitter >= 0.0 && cfg.Siting.ScoreJitter < 1.0))
            throw new WorldgenConfigException(
                $"siting.scoreJitter must be in [0,1) — at 1 or above a jittered score can go negative and " +
                $"the argmax ordering loses meaning, got {Inv(cfg.Siting.ScoreJitter)}.");
        if (cfg.ContinentalMask.EdgeTaperPx < 1 || cfg.ContinentalMask.EdgeTaperPx > cfg.SizePx / 4)
            throw new WorldgenConfigException(
                $"continentalMask.edgeTaperPx must be in 1..sizePx/4 (an ocean margin, not an ocean world), " +
                $"got {cfg.ContinentalMask.EdgeTaperPx}.");

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
