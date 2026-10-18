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
    [property: JsonPropertyName("siting"), JsonRequired] SitingConfig Siting,

    /// <summary>
    /// M4 §11 — how many AI-commanded Empires the world is founded with, BESIDE
    /// the single human-commanded one. Exactly one Empire is ever
    /// <see cref="Sim.Core.State.CommandSource.Player"/>; this is the count of
    /// the others.
    ///
    /// THE POINT OF THE FIELD IS THAT THE NUMBER IS NOT IN THE CODE. Whether a
    /// world has three rivals or eleven is a setup decision, and hard-coding one
    /// answer now is exactly what makes it expensive to change later. Optional
    /// with a default of 0 so that a config written before this existed still
    /// loads and still describes the same world.
    ///
    /// The DEFAULT IS 0, deliberately, and it is not a claim that a world should
    /// have no rivals. It preserves the shipped world exactly — one Empire
    /// holding everything, every golden unmoved — so that turning rivals on is a
    /// deliberate act with its own measurement, rather than something that
    /// happened to every existing world the day the seam landed.
    /// </summary>
    [property: JsonPropertyName("aiEmpires")] int AiEmpires = 0,

    /// <summary>
    /// Batch 3 WS3 — THE WORLD'S EAST-WEST TOPOLOGY (worldgen.json `topology`). <c>wrapX: true</c> generates a
    /// CYLINDER: the edge taper is y-only (the poles stay ocean), the value noise is periodic in x, hydrology wraps in x
    /// with sinks on the poles only, and every x neighbour of worldgen and siting wraps. Absent or false is EXACTLY the
    /// flat world generated before batch 3, byte for byte (the PreCylinderWorldgen attribution control) — an
    /// attribution switch, not a gameplay setting; and the only supported mode for a size the lattice stride does not
    /// divide (Worldgen.Generate refuses wrapX there).
    ///
    /// READ ONLY BY <see cref="Worldgen.Generate"/> (and validated by the loader): every other consumer reads
    /// <see cref="TerrainSet.WrapX"/>, which Generate sets from it — a source guard enforces that, because a system
    /// reading the config could disagree with the terrain it is stepping (the D-052 --size precedent: a world founded at
    /// one size is stepped by a pipeline holding worldgen.json).
    /// </summary>
    [property: JsonPropertyName("topology")] TopologyConfig? Topology = null);

/// <summary>Batch 3 WS3: worldgen.json `topology` (see <see cref="WorldgenConfig.Topology"/>).</summary>
public sealed record TopologyConfig(
    [property: JsonPropertyName("wrapX"), JsonRequired] bool WrapX);

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
        ValidateTopology(cfg);

        return cfg;
    }

    /// <summary>
    /// Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15) — A MEASUREMENT, NOT A RULE: the smallest map
    /// size (px) from which founding at the default settlement count (worldgen.json siting.settlementCount, 12)
    /// succeeded on every measured seed at every measured larger size. MEASURED with
    /// `sim run --founded --seed S --turns 1 --size N`: on seeds 42, 1 and 7 at 12–140 px (every 4 px), siting refused
    /// every size up to 88 px on all three; seed 7 founded from 92 px, seed 1 from 104 px; seed 42 founded at 116 px,
    /// was refused at 120 px ("could only place 10 of 12 sites") and founded from 124 px. On seeds 1–20 and 42 at
    /// 100–160 px, every size from 124 px founded on all 21. Its ONLY use is to decide whether the cylinder's size
    /// refusal adds its founding sentence (<see cref="ValidateTopology"/>); nothing is gated on it, and founding is
    /// decided by settlement siting alone.
    /// </summary>
    private const int FoundingMeasuredPx = 124;

    /// <summary>
    /// Batch 3 WS3: the topology rules a wrapped world needs, shared by the loader and <see cref="Worldgen.Generate"/>
    /// (every `--size` / test override reaches Generate without passing the loader). Under <c>topology.wrapX</c> the
    /// value noise must be PERIODIC in x, which it is only when every octave's lattice period —
    /// frequency × lacunarity^octave, the number of noise cells across the map — is a whole number; a fractional period
    /// leaves a visible cut at the seam, so it is refused, naming the field and the octave. Flat worlds are unconstrained.
    /// </summary>
    /// <remarks>Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15): the size refusal adds its founding
    /// sentence only when a size it names is below <see cref="FoundingMeasuredPx"/>.</remarks>
    public static void ValidateTopology(WorldgenConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (cfg.Topology is not { WrapX: true }) return;
        // The seam joins the last lattice column to the first, so the lattice must tile the map exactly: a size the
        // stride does not divide leaves orphan terrain columns between the last block and the seam (the 250-px rig
        // runs flat for exactly that reason). And a wrapped lattice narrower than MapTopology.MinWrapSize nodes makes
        // a node its own neighbour.
        int stride = Pathing.TraversalLattice.DefaultStride;
        if (cfg.SizePx % stride != 0 || cfg.SizePx / stride < Pathing.MapTopology.MinWrapSize)
        {
            // W1 fix F1: the message names only what the person launching can change — the size (`--size` on the CLI
            // and the UI). worldgen.json is an embedded resource, so "set topology.wrapX false" is not theirs to act on.
            // Fix round 1 (verifier VR-1): the sizes it names are the nearest THE CYLINDER TAKES, and it says only that —
            // founding can still refuse a tiny map for its own reason (siting: 12 px places 1 of 12 sites).
            // Final nits (verifier RC1-4, 2026-10-15): the floor is worded as the cylinder's rule, not as an instruction —
            // the imperative "Use a size that is a multiple of 4 px and at least 12 px" recommended 12 px, which founding
            // refuses at the default settlement count (MEASURED, `sim run --founded --turns 1 --size N`: siting refused
            // 12, 16, 20, 24, 32, 48 and 64 px on seeds 42, 1 and 7, and 96 px on seeds 42 and 1; 128 px founded on all
            // three).
            // Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15): the founding sentence names NO founding
            // size — the size founding needs depends on the seed and the settlement count, so no figure here could be
            // guaranteed — and it says only that siting CAN refuse such a map, with its own message. It is added only
            // when a size this message names is below FoundingMeasuredPx, the smallest size founding was measured to
            // succeed from; for a refusal whose nearest sizes are all at or above it (250 px: 248 and 252 px) the
            // sentence would warn about a refusal no measured seed gave, so it is left out. ValidateTopology also runs
            // where nothing is founded (`sim worldgen --size`, `sim hash`/`sim diff --size`): there the sentence, when
            // present, is a fact about founding at that size, not about the command.
            int floor = stride * Pathing.MapTopology.MinWrapSize;
            int below = cfg.SizePx - ((cfg.SizePx % stride) + stride) % stride;
            int above = below + stride;
            bool twoNearest = below >= floor && below != cfg.SizePx;
            string nearest = twoNearest
                ? $"the nearest sizes the cylinder takes are {below} px and {above} px"
                : $"the nearest size the cylinder takes is {Math.Max(floor, above)} px";
            int smallestNamed = twoNearest ? below : Math.Max(floor, above);
            string founding = smallestNamed < FoundingMeasuredPx
                ? " That rule is the cylinder's alone: founding also needs room to site every settlement, and at the " +
                  "default settlement count, settlement siting can refuse maps this small, with its own message."
                : "";
            throw new WorldgenConfigException(
                $"this world is a cylinder (worldgen.json topology.wrapX), which needs a map size the lattice stride " +
                $"divides into at least {Pathing.MapTopology.MinWrapSize} nodes: size {cfg.SizePx} px, stride {stride} px. " +
                $"The cylinder's own rule is a multiple of {stride} px, at least {floor} px — {nearest}." + founding);
        }
        RequireIntegerPeriods("elevation", cfg.Elevation);
        RequireIntegerPeriods("continentalMask.noise", cfg.ContinentalMask.Noise);
    }

    /// <summary>The integer x period of octave <paramref name="octave"/> of a noise field: frequency × lacunarity^octave,
    /// computed by the SAME repeated multiplication the fBm uses (so the check and the generator agree bit for bit), or
    /// −1 when it is not a whole number in [1, int.MaxValue].</summary>
    public static int OctavePeriod(NoiseConfig n, int octave)
    {
        ArgumentNullException.ThrowIfNull(n);
        double frequency = n.Frequency;
        for (int o = 0; o < octave; o++) frequency *= n.Lacunarity;
        if (!(frequency >= 1.0) || frequency > int.MaxValue || frequency != Math.Floor(frequency)) return -1;
        return (int)frequency;
    }

    private static void RequireIntegerPeriods(string name, NoiseConfig n)
    {
        for (int octave = 0; octave < n.Octaves; octave++)
        {
            if (OctavePeriod(n, octave) > 0) continue;
            double frequency = n.Frequency;
            for (int o = 0; o < octave; o++) frequency *= n.Lacunarity;
            throw new WorldgenConfigException(
                $"topology.wrapX needs x-periodic noise: {name} octave {octave} has period frequency x lacunarity^{octave} = " +
                $"{Inv(frequency)}, which is not a whole number of noise cells across the map. Use a whole-number " +
                "frequency and lacunarity, or set topology.wrapX false.");
        }
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
