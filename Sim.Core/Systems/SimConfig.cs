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
    [property: JsonPropertyName("catchment")] CatchmentConfig Catchment,
    [property: JsonPropertyName("consumption")] ConsumptionConfig Consumption,
    [property: JsonPropertyName("demographics")] DemographicsConfig Demographics,
    [property: JsonPropertyName("pathBuild")] PathBuildConfig PathBuild,
    [property: JsonPropertyName("founding")] FoundingConfig Founding,
    [property: JsonPropertyName("registries")] RegistriesConfig Registries,
    [property: JsonPropertyName("mobility")] MobilityConfig Mobility,
    [property: JsonPropertyName("production")] ProductionConfig Production,
    [property: JsonPropertyName("price")] PriceConfig Price,
    [property: JsonPropertyName("migration")] MigrationConfig Migration,
    // T2.6: the D-018 needs registry rides ITS OWN data file (needs.json) but
    // travels with SimConfig so system construction stays single-config —
    // attached by SimConfigLoader.Load(sim, needs), never parsed from sim.json.
    [property: JsonIgnore] NeedsConfig? Needs = null,
    // T3.2: the D-031 goods registry rides goods.json, attached the same way.
    [property: JsonIgnore] GoodsConfig? Goods = null);

/// <summary>
/// Farming tuning — Leontief production (T1.8 director-sanctioned spec
/// amendment; the T1.5 form had no labor factor and ghost-harvested in a dead
/// world): harvest/yr = min(arableKm2 × YieldPerArableKm2PerYear,
/// adults × farmShare × OutputPerFarmerPerYear). Land side: what the catchment
/// can yield at full working; labor side: what the assigned farmers can work.
/// Every leaf is [JsonRequired] (T1.5 adversarial finding): a missing or typo'd
/// key must fail the load loudly, never silently bind 0.0.
///
/// DENOMINATION (T3.2b): YieldPerArableKm2PerYear is food units per
/// FERTILITY-WEIGHTED km² per year, against
/// CatchmentSummaryRow.EffectiveArableKm2. One food unit is one person-year of
/// adult-equivalent sustenance, so the constant reads directly as "person-years
/// of food an ideal-suitability km² yields per year". Before T3.2b the key was
/// `yieldPerFarmlandPerYear` and was denominated per fertility-weighted lattice
/// NODE (256 km²) while claiming nothing about its unit at all — CR-002.
/// </summary>
public sealed record FarmingConfig(
    [property: JsonPropertyName("yieldPerArableKm2PerYear"), JsonRequired] double YieldPerArableKm2PerYear,
    [property: JsonPropertyName("outputPerFarmerPerYear"), JsonRequired] double OutputPerFarmerPerYear);

/// <summary>
/// Catchment tuning (T3.2b). A settlement's catchment is its ECONOMIC
/// HINTERLAND — the country whose produce can profitably flow in — not a
/// farmer's daily working radius.
///
/// HinterlandRadiusKm is stated as an IDEAL-GROUND radius in km and converted to
/// the pathfinder's cost budget by LatticeGeometry. What it actually buys is
/// then decided by geography: less through mountain and marsh, more along
/// rivers and built paths, which is the mechanism by which a road network grows
/// a hinterland (D-009) rather than a modifier bolted onto one.
///
/// Before T3.2b this was `CatchmentSystem.TravelBudget = 15.0` — a code
/// constant, in cost units, that no tuning pass could see. It went unexamined
/// for three milestones and ended up compensating for the yield denomination
/// error (CR-002).
/// </summary>
public sealed record CatchmentConfig(
    [property: JsonPropertyName("hinterlandRadiusKm"), JsonRequired] double HinterlandRadiusKm);

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
/// <summary>EndowmentJitter (T3.1c): amplitude of the seeded per-settlement
/// jitter on the founding endowment — each cohort count and the food store
/// scale by (1 + j·u), u ∈ [−1,1] hashed from (seed, settlement, slot).
/// At 0 every settlement founds as an identical copy (the M2 behavior that
/// produced the T2.4 same-decade class lockstep). TUNE.</summary>
public sealed record FoundingConfig(
    [property: JsonPropertyName("cohortCounts"), JsonRequired] long[] CohortCounts,
    [property: JsonPropertyName("foodStore"), JsonRequired] long FoodStore,
    [property: JsonPropertyName("endowmentJitter"), JsonRequired] double EndowmentJitter);

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
/// the fields first. T3.3 DEMOLITION (mandated, m3 spec §1): the M2 scaffolds
/// that lived here — the artisan tool-yield multiplier and the artisan
/// construction-labor weight — are DELETED. Tools are a real good consumed by
/// farmers (ProductionConfig); sector labor pools are class-blind shares of
/// the adult workforce (D-032).
/// </summary>
public sealed record MobilityConfig(
    [property: JsonPropertyName("promoteRatePerYear"), JsonRequired] double PromoteRatePerYear,
    [property: JsonPropertyName("famineDemoteRatePerYear"), JsonRequired] double FamineDemoteRatePerYear,
    [property: JsonPropertyName("targetShareSlope"), JsonRequired] double TargetShareSlope,
    [property: JsonPropertyName("targetShareCap"), JsonRequired] double TargetShareCap);

/// <summary>
/// Production tuning (T3.3, D-032 — all TUNE, per-sim-year rates, law 3).
/// Provenance discipline per S8 §4.1(c): every value below is CHOSEN, not
/// derived, and says so — with the plausibility frame that bounds it. The
/// foundations audit of the milestone that consumes these owes them a row.
///
/// OutputPerHerderPerYear — food units (person-years of sustenance) one
///   herder/fisher produces per year at deposit abundance 1.0. CHOSEN 3.0:
///   below the farmer's 5.0 (pastoralism supports fewer people per worker
///   than valley agriculture — the reason farming displaced it on good land),
///   above 1.0 (a herder feeds more than himself or herding would not exist).
/// OutputPerExtractorPerYear — units of a raw good one extractor produces per
///   year at deposit abundance 1.0. CHOSEN 4.0: sets raw-good flow scale;
///   meaningful only relative to recipe input demands (a potter needs 2 clay +
///   0.5 timber per pot), sized so a few extractors supply a few artisans.
/// ToolsPerFarmerToEquip — tool units that fully equip one farmer. CHOSEN 1.0:
///   the natural unit (one man, one plough/sickle set).
/// ToolYieldBonusMax — labor-side yield factor at full equipment: factor =
///   1 + bonus × equipRatio. CHOSEN 0.3, carried over in MAGNITUDE from the
///   retired scaffold's cap (bronze-age tool advantage over digging-stick
///   agriculture, order tens of percent — Boserup's tool-intensity range),
///   but the MECHANISM is new: the ratio is real tools in stock per farmer,
///   not a class share.
/// ToolWearPerEquippedFarmerPerYear — tool units one EQUIPPED farmer wears out
///   per year (the Ledger sink that makes tools deplete). CHOSEN 0.1: a tool
///   set lasts ~10 working years — bronze tools were repaired and recast for
///   decades; wholly wrong values fail the plausibility question loudly
///   (1.0 = tools of wet clay; 0.001 = heirloom economy with no tool demand).
/// </summary>
public sealed record ProductionConfig(
    [property: JsonPropertyName("outputPerHerderPerYear"), JsonRequired] double OutputPerHerderPerYear,
    [property: JsonPropertyName("outputPerExtractorPerYear"), JsonRequired] double OutputPerExtractorPerYear,
    [property: JsonPropertyName("toolsPerFarmerToEquip"), JsonRequired] double ToolsPerFarmerToEquip,
    [property: JsonPropertyName("toolYieldBonusMax"), JsonRequired] double ToolYieldBonusMax,
    [property: JsonPropertyName("toolWearPerEquippedFarmerPerYear"), JsonRequired] double ToolWearPerEquippedFarmerPerYear);

/// <summary>
/// T3.4 price solver tuning (D-033). ALL TUNE, and the rate is per-sim-year
/// (law 3) — the mandate's "per-turn relative change" is realised as a per-YEAR
/// cap integrated with dtYears, because a literal per-turn constant would bind
/// differently at dt = 10 and dt = 1 and is exactly what law 3 forbids. The
/// clamp is a safety rail on the STEP, and a rail whose height depends on how
/// long the turn is is the only version that means the same thing at every dt.
///
/// The step, per settlement, per good, once per turn, no global solve ever:
///   excess = consumptionDemand + inputDemand − production − stockRelease
///   scale  = max(production + stockRelease, MarketScaleFloorPerYear * dtYears)
///   p += Lambda × p × (excess / scale) × dtYears
/// then clamped to MaxRelativeChangePerYear × dtYears, then to the band.
/// excess/scale is a RATIO of per-turn quantities: both numerator and
/// denominator scale with dt, so the ratio is dimensionless and dt-invariant,
/// and the single explicit × dtYears is the whole dt dependence.
///
/// S8 §4.1(c) — DERIVED OR MERELY CHOSEN, stated explicitly per value:
/// Lambda — CHOSEN 0.04 per year. Never derived. The price-adjustment speed:
///   at a 100% relative excess sustained for one year, price moves 4% (~25-year
///   e-folding). Plausibility frame: a pre-modern market with no telegraph and
///   seasonal caravans reprices over years, not turns. SIZED AGAINST THE
///   COARSEST SHIPPED dt — the Neolithic band steps 10 sim-years at once, so
///   Lambda × dt must stay well under 1 or a single turn overshoots the
///   equilibrium it is seeking. An earlier 0.35 gave Lambda × dt = 3.5 and
///   drove the same 100-year horizon to OPPOSITE ENDS of the band at dt = 10
///   and dt = 1. That was caught by the dt-invariance test, not by inspection,
///   which is the argument for the test existing.
/// MarketScaleFloorPerYear — CHOSEN 0.1 units per year. Never derived. A
///   divide-by-zero guard with a real meaning: a market with no supply at all
///   still has a finite reference size, so a single unit of unmet demand cannot
///   produce an unbounded relative excess. PER YEAR, integrated with dtYears,
///   for the reason the rail is: it sits in the DENOMINATOR of excess/scale
///   whose numerator scales with dt, so a dt-independent constant makes the
///   ratio dt-DEPENDENT exactly when the floor binds — the one place the
///   system's own "dimensionless and dt-invariant" claim was false. Found by
///   the T3.4 dt-determinism lens, confirmed on the pinned tree. At 0.1/yr it
///   reproduces the previous 1.0 exactly at dt = 10, so the Neolithic band is
///   unchanged; finer bands now scale with the turn instead of being ten times
///   too coarse.
/// StockReleaseRatePerYear — CHOSEN 0.5 per year. Never derived. The fraction
///   of a held stock offered to the market per year. Plausibility frame: half a
///   granary comes to market within the year, the rest is held as seed corn and
///   insurance. This is the term that lets a full warehouse damp a price spike.
/// BandMin / BandMax — CHOSEN 0.05 / 20.0 grain units. Never derived. The hard
///   bound on any price relative to the numeraire. Frame: nothing in a
///   neolithic-to-iron-age economy is worth less than 1/20 of a grain unit or
///   more than 20 of them per unit; the band is deliberately WIDE, because its
///   job is to stop divergence, not to express economics. A price sitting on a
///   band edge for long stretches is a signal the band is doing work it should
///   not be — the soak reports it.
/// MaxRelativeChangePerYear — CHOSEN 0.03 per year. Never derived. The per-step
///   rail: a price may not move more than 3% of itself per sim-year however
///   large the measured excess — 30% in a 10-year Neolithic turn, 3% in a
///   1-year turn. This is the D-033 "maximum per-turn relative change",
///   dt-corrected. Sized so the rail BINDS at the coarsest dt instead of being
///   decorative there: at an earlier 0.5 it permitted a 500% move in a single
///   dt = 10 turn, which is not a rail. It also makes the step
///   positivity-preserving without help from the band — a maximum downward move
///   of 0.3p can never reach zero — so the band is left free to do only its own
///   job. It is the term that makes a shock ramp rather than jump, and the
///   first thing to look at if the soak oscillates.
/// </summary>
public sealed record PriceConfig(
    [property: JsonPropertyName("lambda"), JsonRequired] double Lambda,
    [property: JsonPropertyName("marketScaleFloorPerYear"), JsonRequired] double MarketScaleFloorPerYear,
    [property: JsonPropertyName("stockReleaseRatePerYear"), JsonRequired] double StockReleaseRatePerYear,
    [property: JsonPropertyName("bandMin"), JsonRequired] double BandMin,
    [property: JsonPropertyName("bandMax"), JsonRequired] double BandMax,
    [property: JsonPropertyName("maxRelativeChangePerYear"), JsonRequired] double MaxRelativeChangePerYear);

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
/// T2.8 stabilization (director ruling — D-021 paired-feedback):
///  - GapClosingFraction f ∈ (0,1): the gap-DRIVEN flow on a pair is capped
///    at f × the flow that would EQUALIZE per-capita attractiveness
///    (closed form; see MigrationSystem) — overshoot is structurally
///    impossible below 1. Famine flight is deliberately NOT capped by it
///    (the Exit valve is a surge by design; the overdraw scaler bounds it).
///  - AttractivenessSmoothingWindowYears τ > 0: the EMA time constant of the
///    smoothed attractiveness that DRIVES desire — a one-turn emptying
///    cannot mint a one-turn magnet.
/// </summary>
public sealed record MigrationConfig(
    [property: JsonPropertyName("baseRatePerYear"), JsonRequired] double BaseRatePerYear,
    // T3.2b: renamed to state its denomination. This one STAYS in the
    // pathfinder's cost units rather than moving to km like the catchment
    // radius and the siting spacing, because it damps travel EFFORT, not map
    // distance: 400 km of river valley and 400 km of mountain should not damp
    // migration equally, and the cost field is exactly what encodes that.
    [property: JsonPropertyName("dampingDecayCostUnits"), JsonRequired] double DampingDecayCostUnits,
    [property: JsonPropertyName("attractivenessFoodWeight"), JsonRequired] double AttractivenessFoodWeight,
    // T3.2b: denominated per FERTILITY-WEIGHTED km² of catchment (was per
    // fertility-weighted lattice node). Divided by 256 in the same commit that
    // multiplied the catchment quantity by 256 — a re-denomination, not a
    // re-tune; the product is bit-identical (both factors are powers of two).
    [property: JsonPropertyName("attractivenessLandWeight"), JsonRequired] double AttractivenessLandWeight,
    [property: JsonPropertyName("famineFlightFactor"), JsonRequired] double FamineFlightFactor,
    [property: JsonPropertyName("cohortProfile"), JsonRequired] double[] CohortProfile,
    [property: JsonPropertyName("gapClosingFraction"), JsonRequired] double GapClosingFraction,
    [property: JsonPropertyName("attractivenessSmoothingWindowYears"), JsonRequired] double AttractivenessSmoothingWindowYears,
    [property: JsonPropertyName("destinationDeficitRepulsion"), JsonRequired] double DestinationDeficitRepulsion);

public static class SimConfigLoader
{
    public static SimConfig Load(Stream json)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd());
    }

    /// <summary>T2.6: canonical two-file load — sim.json plus the D-018 needs
    /// registry (needs.json), attached as SimConfig.Needs. Systems that bind
    /// needs (NeedsGrievance) refuse construction without it.</summary>
    public static SimConfig Load(Stream simJson, Stream needsJson) =>
        Load(simJson) with { Needs = NeedsConfigLoader.Load(needsJson) };

    /// <summary>T3.2: canonical three-file load — sim.json + needs.json +
    /// goods.json (the D-031 registry, attached as SimConfig.Goods). Systems
    /// that bind goods (Farming, Consumption at M3) refuse construction
    /// without it.</summary>
    public static SimConfig Load(Stream simJson, Stream needsJson, Stream goodsJson) =>
        Load(simJson) with
        {
            Needs = NeedsConfigLoader.Load(needsJson),
            Goods = GoodsConfigLoader.Load(goodsJson),
        };

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
        RequireRate("farming.yieldPerArableKm2PerYear", cfg.Farming.YieldPerArableKm2PerYear);
        RequireRate("farming.outputPerFarmerPerYear", cfg.Farming.OutputPerFarmerPerYear);

        if (cfg.Catchment is null) throw new SimConfigException("catchment is missing.");
        if (!(cfg.Catchment.HinterlandRadiusKm > 0.0)
            || double.IsNaN(cfg.Catchment.HinterlandRadiusKm)
            || double.IsInfinity(cfg.Catchment.HinterlandRadiusKm))
            throw new SimConfigException(
                "catchment.hinterlandRadiusKm must be a finite positive distance in km, got "
                + $"{Inv(cfg.Catchment.HinterlandRadiusKm)}.");

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
        if (!(cfg.Founding.EndowmentJitter >= 0.0 && cfg.Founding.EndowmentJitter < 1.0))
            throw new SimConfigException(
                $"founding.endowmentJitter must be in [0,1) — at 1 or above a settlement can found empty, " +
                $"got {Inv(cfg.Founding.EndowmentJitter)}.");

        if (cfg.Registries is null) throw new SimConfigException("registries is missing.");
        ValidateRegistry("registries.cultures", cfg.Registries.Cultures);
        ValidateRegistry("registries.religions", cfg.Registries.Religions);
        ValidateClasses("registries.classes", cfg.Registries.Classes);

        if (cfg.Mobility is null) throw new SimConfigException("mobility is missing.");
        RequireRate("mobility.promoteRatePerYear", cfg.Mobility.PromoteRatePerYear);
        RequireRate("mobility.famineDemoteRatePerYear", cfg.Mobility.FamineDemoteRatePerYear);
        RequireRate("mobility.targetShareSlope", cfg.Mobility.TargetShareSlope);
        if (cfg.Production is null) throw new SimConfigException("production is missing.");
        RequireRate("production.outputPerHerderPerYear", cfg.Production.OutputPerHerderPerYear);
        RequireRate("production.outputPerExtractorPerYear", cfg.Production.OutputPerExtractorPerYear);
        RequireRate("production.toolsPerFarmerToEquip", cfg.Production.ToolsPerFarmerToEquip);
        RequireRate("production.toolYieldBonusMax", cfg.Production.ToolYieldBonusMax);
        RequireRate("production.toolWearPerEquippedFarmerPerYear", cfg.Production.ToolWearPerEquippedFarmerPerYear);
        if (!(cfg.Mobility.TargetShareCap >= 0.0 && cfg.Mobility.TargetShareCap < 1.0))
            throw new SimConfigException(
                $"mobility.targetShareCap must be in [0,1), got {Inv(cfg.Mobility.TargetShareCap)}.");

        if (cfg.Migration is null) throw new SimConfigException("migration is missing.");
        RequireRate("migration.baseRatePerYear", cfg.Migration.BaseRatePerYear);
        RequireRate("migration.attractivenessFoodWeight", cfg.Migration.AttractivenessFoodWeight);
        RequireRate("migration.attractivenessLandWeight", cfg.Migration.AttractivenessLandWeight);
        RequireRate("migration.famineFlightFactor", cfg.Migration.FamineFlightFactor);
        if (!(cfg.Migration.DampingDecayCostUnits > 0.0) || !double.IsFinite(cfg.Migration.DampingDecayCostUnits))
            throw new SimConfigException(
                "migration.dampingDecayCostUnits must be a finite value > 0, got "
                + $"{Inv(cfg.Migration.DampingDecayCostUnits)}.");
        RequireCohortArray("migration.cohortProfile", cfg.Migration.CohortProfile);
        if (!(cfg.Migration.GapClosingFraction > 0.0 && cfg.Migration.GapClosingFraction < 1.0))
            throw new SimConfigException(
                $"migration.gapClosingFraction must be in (0,1) — at 1 or above the damped-flow cap no longer " +
                $"prevents overshoot structurally, got {Inv(cfg.Migration.GapClosingFraction)}.");
        if (!(cfg.Migration.AttractivenessSmoothingWindowYears > 0.0)
            || !double.IsFinite(cfg.Migration.AttractivenessSmoothingWindowYears))
            throw new SimConfigException(
                $"migration.attractivenessSmoothingWindowYears must be a finite value > 0, " +
                $"got {Inv(cfg.Migration.AttractivenessSmoothingWindowYears)}.");
        if (!(cfg.Migration.DestinationDeficitRepulsion >= 1.0)
            || !double.IsFinite(cfg.Migration.DestinationDeficitRepulsion))
            throw new SimConfigException(
                $"migration.destinationDeficitRepulsion must be a finite value >= 1 — below 1 a fully " +
                $"starving destination (deficit 1.0) would still RECEIVE migrants, which is the T2.13 " +
                $"starvation-magnetism defect this parameter exists to kill; got " +
                $"{Inv(cfg.Migration.DestinationDeficitRepulsion)}.");

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
