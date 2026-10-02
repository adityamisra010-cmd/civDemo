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
    [property: JsonPropertyName("transport")] TransportConfig Transport,
    [property: JsonPropertyName("founding")] FoundingConfig Founding,
    [property: JsonPropertyName("registries")] RegistriesConfig Registries,
    [property: JsonPropertyName("mobility")] MobilityConfig Mobility,
    [property: JsonPropertyName("production")] ProductionConfig Production,
    [property: JsonPropertyName("price")] PriceConfig Price,
    [property: JsonPropertyName("harvestVariance")] HarvestVarianceConfig HarvestVariance,
    [property: JsonPropertyName("migration")] MigrationConfig Migration,
    [property: JsonPropertyName("trade")] TradeConfig Trade,
    [property: JsonPropertyName("housing")] HousingConfig Housing,
    // T4.21-1 (CR-015): the famine-is-exceptional classification and the
    // explicit famine-class production shock.
    [property: JsonPropertyName("foodState")] FoodStateConfig FoodState,
    [property: JsonPropertyName("disaster")] DisasterConfig Disaster,
    // ADR-032: inter-city road classes, development cost placeholder and the
    // travel-time hook. OPTIONAL: absent leaves RoadDevelopmentSystem inert (toy
    // and hand-written configs); the canonical sim.json carries it.
    [property: JsonPropertyName("roads")] RoadsConfig? Roads,
    // T2.6: the D-018 needs registry rides ITS OWN data file (needs.json) but
    // travels with SimConfig so system construction stays single-config —
    // attached by SimConfigLoader.Load(sim, needs), never parsed from sim.json.
    [property: JsonIgnore] NeedsConfig? Needs = null,
    // T3.2: the D-031 goods registry rides goods.json, attached the same way.
    [property: JsonIgnore] GoodsConfig? Goods = null,
    // ADR-029 (D-044): the research graph rides research.json, attached the same
    // way by the four-stream Load. Null leaves the ResearchSystem inert.
    [property: JsonIgnore] Research.ResearchContent? Research = null,
    // ADR-031 (D-047): the Ages (ages.json) and the unit-family graph
    // (unit-families.json), attached by the six-stream Load. Null leaves the two
    // Age systems inert and founds no formations.
    [property: JsonIgnore] Ages.AgeContent? Ages = null,
    [property: JsonIgnore] Ages.UnitFamilyContent? UnitFamilies = null,
    // ADR-033 D4 (the M5 governing loop): administrative reach, the extraction response,
    // the taxation research gate and the AI tax valve. OPTIONAL and in the DEFAULTED TAIL
    // (Roads above is positional): absent, the governing loop is INERT — GovernanceSystem
    // applies no SetTaxRate, leaves ControlRow.Strength as founding wrote it, and every
    // Governance reader returns the neutral value (no tax, extraction ×1, burden ×1). Toy and
    // hand-written configs therefore run the full catalog unchanged; the canonical sim.json
    // carries the section.
    [property: JsonPropertyName("governance")] GovernanceConfig? Governance = null);

/// <summary>
/// ADR-033 D4 — THE GOVERNING LOOP'S TUNING (ported from <c>m5-full-build</c>, whose AI constants
/// were code literals and are data here). Every number is TUNE; the two loop constants are
/// denominated against shipped quantities rather than chosen freely — see sim.json
/// <c>governance._doc</c> for the full provenance.
///
/// <c>AuthorityDecayCostUnits</c>: the e-fold of ADMINISTRATIVE REACH in SettlementDistances
/// travel-cost units — reach = exp(−travelCost / this) from the capital, the functional form and
/// the 25.0 of <c>migration.dampingDecayCostUnits</c> (D-040 C3: a distance term over the network
/// graph, travel cost and not Euclidean distance). <c>TaxExtractionResponseMax</c>: output
/// multiplier = 1 + this × effectiveRate, framed by <c>production.toolYieldBonusMax</c> (0.3); 0.0
/// disables the economic arm. <c>TaxationRequires</c>: the RESEARCH GATE of the tax edict, one
/// requires-expression over research.json node ids in the research dialect (AND / OR / nested,
/// no NOT, no comparisons), validated against research.json when the four-stream load attaches it
/// and evaluated by the existing knowledge evaluator (<see cref="State.Governance.CanLevyTax"/>).
/// <c>Ai</c>: the AI tax valve's constants (<see cref="GovernanceAiConfig"/>).
/// </summary>
public sealed record GovernanceConfig(
    [property: JsonPropertyName("authorityDecayCostUnits"), JsonRequired] double AuthorityDecayCostUnits,
    [property: JsonPropertyName("taxExtractionResponseMax"), JsonRequired] double TaxExtractionResponseMax,
    [property: JsonPropertyName("taxationRequires"), JsonRequired] string TaxationRequires,
    [property: JsonPropertyName("ai"), JsonRequired] GovernanceAiConfig Ai);

/// <summary>
/// ADR-033 D4 — THE AI TAX VALVE (D-021 valve 6, "the state acts by default"), the four constants
/// <c>m5-full-build</c>'s AiGovernance carried as literals (60 / 35 / 5 / 40). An AI Empire RAISES
/// its declared rate by <c>StepPercent</c> while its legitimacy is ≥ <c>ComfortableLegitimacy</c>,
/// LOWERS it while legitimacy is &lt; <c>TroubledLegitimacy</c>, holds in the dead band between
/// (the hysteresis that stops a one-step oscillation), and never declares more than
/// <c>MaxRatePercent</c>. Legitimacies are on the 0..100 happiness scale; rates are percentages.
/// </summary>
public sealed record GovernanceAiConfig(
    [property: JsonPropertyName("comfortableLegitimacy"), JsonRequired] double ComfortableLegitimacy,
    [property: JsonPropertyName("troubledLegitimacy"), JsonRequired] double TroubledLegitimacy,
    [property: JsonPropertyName("stepPercent"), JsonRequired] double StepPercent,
    [property: JsonPropertyName("maxRatePercent"), JsonRequired] double MaxRatePercent);

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
    [property: JsonPropertyName("hinterlandRadiusKm"), JsonRequired] double HinterlandRadiusKm,
    // T3.8 (the spec line's catchment bonus). SizeBonusMaxRatio — CHOSEN 0.10
    // within a stated frame: a fully built-out settlement's infrastructure and
    // labor density extend its effective hinterland by AT MOST a tenth (a
    // second-order effect on a 50 km radius; 0.5 would make size dominate
    // geography, 0.01 would be decorative). SizeDwellingsRef — DERIVED 70,
    // the founding-scale dwelling demand (~410 founding population /
    // 6 persons per dwelling): a settlement at its founding size sits near
    // the top tier, and the bonus saturates rather than compounding with
    // growth (law 2: a coefficient inside the budget equation, quantized to
    // five steps so the D-016 recompute gate survives).
    [property: JsonPropertyName("sizeBonusMaxRatio"), JsonRequired] double SizeBonusMaxRatio,
    [property: JsonPropertyName("sizeDwellingsRef"), JsonRequired] double SizeDwellingsRef);

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
/// Transport tuning (T4.7 — the transport packet). ONE constant:
/// RiverCostFactor, the cost of moving ALONG a river as a fraction of ideal
/// ground (movement cost 1.0), applied as a coefficient inside
/// <see cref="Pathing.TraversalLattice.Build"/>'s per-block cost aggregation
/// (law 2 — never a free-floating buff, and no second graph: TerrainSet stays
/// the authoritative geometry and no river-derived routing state is persisted).
///
/// DERIVATION — see sim.json `transport._doc` for the full text and band.
/// </summary>
public sealed record TransportConfig(
    [property: JsonPropertyName("riverCostFactor"), JsonRequired] double RiverCostFactor);

/// <summary>
/// ADR-032 — inter-city road tuning. EVERY NUMBER IS TUNE, a deterministic PLACEHOLDER
/// (the Director's ruling 12: no ratified cost or speed formula exists yet). One entry per
/// road-family class, DirtPath (the free baseline) included so the travel-time hook has a
/// baseline speed and capacity. Read by RoadDevelopmentSystem (eligibility, cost, and the
/// effective performance it writes onto each route through <see cref="State.RoadPerformance"/>)
/// and by <see cref="State.TransportQuery"/> (baseline speed and capacity of the travel-time
/// hook). Pathfinding reads the WRITTEN effective performance, never this config.
///
/// <c>BaselineKmPerDay</c>: freight speed over the free baseline (DirtPath) on IDEAL ground,
/// calibrated to the Director's figure "5000 t of steel over 3000 km takes ~3 in-game months"
/// on bad roads: 3000 km / ~90 days = 33.3 km/day. <c>MaxRouteKm</c>: the longest baseline
/// pair (ideal-ground-equivalent km) treated as an inter-city route — beyond it two places are
/// not neighbours a road would join directly.
/// </summary>
public sealed record RoadsConfig(
    [property: JsonPropertyName("baselineKmPerDay"), JsonRequired] double BaselineKmPerDay,
    [property: JsonPropertyName("maxRouteKm"), JsonRequired] double MaxRouteKm,
    [property: JsonPropertyName("classes"), JsonRequired] RoadClassConfig[] Classes)
{
    /// <summary>The entry for <paramref name="edgeType"/>, or null.</summary>
    public RoadClassConfig? ClassOf(int edgeType)
    {
        for (int i = 0; i < Classes.Length; i++)
            if (Classes[i].EdgeType == edgeType) return Classes[i];
        return null;
    }
}

/// <summary>
/// ADR-032 — one road class. <c>Entity</c>: the research.json infrastructure entity whose
/// knowledge-eligibility makes the class buildable (null only for DirtPath, the baseline).
/// <c>SpeedFactor</c> in (0,1]: the class's travel cost per km as a fraction of the baseline's
/// (lower = faster; DirtPath 1). <c>CapacityTonnesPerYear</c>: freight throughput of one edge.
/// <c>MaterialsPerKm</c>: goods consumed per km to build the class from nothing; an upgrade
/// pays only the per-good DIFFERENCE to the class it replaces (never negative).
/// </summary>
public sealed record RoadClassConfig(
    [property: JsonPropertyName("edgeType"), JsonRequired] int EdgeType,
    [property: JsonPropertyName("entity")] string? Entity,
    [property: JsonPropertyName("speedFactor"), JsonRequired] double SpeedFactor,
    [property: JsonPropertyName("capacityTonnesPerYear"), JsonRequired] long CapacityTonnesPerYear,
    [property: JsonPropertyName("materialsPerKm"), JsonRequired] RoadMaterialConfig[] MaterialsPerKm);

/// <summary>ADR-032 — <c>Qty</c> units of goods.json good <c>Good</c> per km of road.</summary>
public sealed record RoadMaterialConfig(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("qty"), JsonRequired] double Qty);

/// <summary>
/// Per-cohort consumption weights (T2.1): food per person per sim-year for each
/// of the 16 five-year cohorts (D-015 constants, cohortized). Exactly
/// Cohorts.Count entries, validated at load.
/// </summary>
public sealed record ConsumptionConfig(
    [property: JsonPropertyName("cohortWeights"), JsonRequired] double[] CohortWeights,
    // T4.2 (B-2 base layer). GrainSpoilagePerYear: the fraction of STORED grain
    // lost per sim-year to moulds, germination, insects and rodents — DERIVED
    // from pre-modern fixed-storage loss rates (5-10%/yr; midpoint taken), see
    // docs/t4.2-manifest.md 1a. Integrated dt-correctly as a survival
    // exponential, never as rate*dt (the CR-001 fragility).
    [property: JsonPropertyName("grainSpoilagePerYear"), JsonRequired] double GrainSpoilagePerYear,
    // GranaryYearsOfDemand: the per-settlement ceiling, denominated in YEARS OF
    // THE SETTLEMENT'S OWN ANNUAL GRAIN DEMAND so it scales with population and
    // introduces no per-world constant. DERIVED from agrarian household storage
    // carrying "one to a few years"; midpoint taken. Manifest 1b.
    [property: JsonPropertyName("granaryYearsOfDemand"), JsonRequired] double GranaryYearsOfDemand);

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
/// T4.21 headroom relaxation (ADR-025 §2.4, ADR-026; t4.21-architecture §3.5c/§3.6b):
///  - HeadroomRelaxationPerYear (k): the per-year rate at which the REMAINING
///    food headroom of a settlement (N_lim − N, FoodHeadroom.Vacancy) is closed
///    — by natural growth (the Demographics headroom cap) AND by immigration
///    (Migration's vacancy bound): cap = (1 − exp(−k·dt)) × V. ONE law, ONE
///    constant, consumed by both systems. TUNE, CHOSEN per S8 §4.1(c), not
///    derived: k = ln 2 / 10 within the director's frame "the gap halves per
///    decade" ((1 − e^{−k·h}) = 3.41 % of remaining headroom per half-year).
///    No null arm by design — a structural bound has no switch (law 2); the
///    identity arm is N_lim = +∞ (no deficit row / no demand), where the cap
///    is vacuous. A rate ≥ 0 is required (finite, non-negative).
/// </summary>
public sealed record DemographicsConfig(
    [property: JsonPropertyName("fertilityPerPersonPerYear"), JsonRequired] double[] FertilityPerPersonPerYear,
    [property: JsonPropertyName("mortalityPerYear"), JsonRequired] double[] MortalityPerYear,
    [property: JsonPropertyName("starvationMortalityMaxPerYear"), JsonRequired] double StarvationMortalityMaxPerYear,
    [property: JsonPropertyName("starvationChildMultiplier"), JsonRequired] double StarvationChildMultiplier,
    [property: JsonPropertyName("starvationElderMultiplier"), JsonRequired] double StarvationElderMultiplier,
    [property: JsonPropertyName("famineFertilitySuppressionSlope"), JsonRequired] double FamineFertilitySuppressionSlope,
    [property: JsonPropertyName("reboundRecoverableFraction"), JsonRequired] double ReboundRecoverableFraction,
    [property: JsonPropertyName("reboundReleaseRatePerYear"), JsonRequired] double ReboundReleaseRatePerYear,
    [property: JsonPropertyName("headroomRelaxationPerYear"), JsonRequired] double HeadroomRelaxationPerYear);

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
/// T3.4b harvest variance (CR-003 ruling §3). ALL TUNE, ALL CHOSEN, per S8
/// 4.1(c) — none of these is derived, and each states its real-world meaning.
///
/// THE MODEL. Per settlement, a log-space AR(1) with a spatially smoothed
/// innovation:
///   rho     = exp(-dtYears / CorrelationTimeYears)      (temporal memory)
///   x_i(t)  = rho * x_i(t-1) + Sigma * sqrt(1 - rho^2) * e_i
///   mult_i  = exp(x_i - Sigma^2 / 2)                    (mean EXACTLY 1)
/// where e_i is a unit-variance innovation smoothed over neighbours by an
/// exponential distance kernel (see SpatialRangeCostUnits).
///
/// WHY exp(-dt/tau) FOR rho, AND WHY sqrt(1 - rho^2) ON THE INNOVATION. Both are
/// dt-correctness (law 3) and both are the ADR-016 family — a correlation that
/// decays continuously in TIME, not per turn. exp(-dt/tau) makes the memory a
/// property of years rather than of turn count; sqrt(1 - rho^2) is exactly the
/// factor that holds the STATIONARY variance at Sigma^2 for every dt, so a
/// campaign that shrinks dt does not silently change how variable the weather
/// is. A naive constant rho would make weather calmer or wilder purely as an
/// artefact of the era table.
///
/// SigmaLogYield — DERIVED 0.2936, replacing a CHOSEN 0.18 (T3.4b, director
///   ruling). Standard deviation of LOG yield in a single year.
///   REFERENCE CLASS: rain-fed cereal agriculture without irrigation or modern
///   inputs. The best-documented proxy is medieval English demesne wheat
///   (Winchester Pipe Rolls, 13th–15th c.), whose interannual yield CV is
///   commonly placed around 0.25–0.35. Early agriculture should sit at least
///   that high — landrace seed, no systematic rotation — and this model adds a
///   specific reason to expect the upper part of the band: a ~50 km hinterland
///   is a spatially small sample, so little internal averaging smooths the draw.
///   DERIVATION: take CV = 0.30 as the central value and invert the lognormal
///   relation CV = sqrt(exp(sigma²) − 1), giving
///   sigma = sqrt(ln(1 + CV²)) = sqrt(ln 1.09) = 0.2936.
///   The prior 0.18 implied CV = 0.18 — below the entire reference band, and
///   never derived from anything.
///   MEASURED CONSEQUENCE, reported because it does not help: gross migration
///   rises 0.43 → 0.76 %/decade, i.e. the derived constant moves the migration
///   corridor FURTHER out of band, not toward it. Adopted regardless, per the
///   T3.2b precedent that out-of-band with a derived constant beats in-band
///   with a fitted one.
/// CorrelationTimeYears — CHOSEN 3.0. Never derived. The e-folding memory of
///   the weather process. THIS IS THE PARAMETER THAT MAKES MULTI-YEAR DROUGHTS
///   POSSIBLE, which the ruling requires: at tau = 3 a bad year is followed by a
///   bad year far more often than chance, so consecutive failures — the thing
///   that actually kills, against stores that survive one bad year — occur at a
///   realistic rate rather than never. Frame: drought regimes persist over a
///   few years, not a few decades.
/// SpatialSharedFraction — CHOSEN 0.6. Never derived. The share of a
///   settlement's innovation that comes from the regionally smoothed field
///   rather than its own local draw. THE RULING REQUIRES THIS BE MEANINGFUL:
///   uncorrelated rolls let trade and migration average away all risk, and
///   "regional bad years are the point". At 0.6 a bad year is mostly shared
///   with neighbours and partly local.
/// SpatialRangeCostUnits — CHOSEN 40.0 cost units. Never derived. The e-folding
///   distance of the weather field, in the same travel-cost units as
///   SettlementDistances. Settlements a short journey apart share weather
///   strongly; settlements across the map share it weakly. Frame: a weather
///   system spans a region, not a continent.
/// </summary>
public sealed record HarvestVarianceConfig(
    [property: JsonPropertyName("sigmaLogYield"), JsonRequired] double SigmaLogYield,
    [property: JsonPropertyName("correlationTimeYears"), JsonRequired] double CorrelationTimeYears,
    [property: JsonPropertyName("spatialSharedFraction"), JsonRequired] double SpatialSharedFraction,
    [property: JsonPropertyName("spatialRangeCostUnits"), JsonRequired] double SpatialRangeCostUnits);

/// <summary>
/// T4.21-1 (CR-015 §3.1/§3.2) — the food-state classification's one constant.
/// AdaptationAbsorbableShortfall (a) — CHOSEN 0.20, a TUNE value with a stated
///   physiological frame. It is BOTH the dead-zone of the effective deficit
///   (d_eff = 0 for d ≤ a; (d − a)/(1 − a) above, exactly 1 at d = 1) AND the
///   SEVERE threshold (θ_sev := a), so the state label predicts the kernel's
///   response: STRESS ⇒ no starvation, SEVERE ⇒ starvation on the unabsorbed
///   remainder, FAMINE ⇒ starvation on the whole deficit. Frame: sustained
///   ration cuts of ~15–25% are survivable without excess mortality (WWII
///   civilian rationing; the Minnesota Starvation Experiment's ~50% cut for 24
///   weeks with no deaths is the upper bound), while 40–70% cuts (Dutch Hunger
///   Winter, post-war German rations) carried excess mortality — the band's
///   midpoint taken. The recorded alternative a = 1/3 aligns the dead zone with
///   the kernel's birth full-stop (births continue only while nobody starves);
///   it is not the default because the director's frame puts survivable cuts at
///   15–25%. The null arm a = 0 reproduces today's linear response bit for bit.
///   Validated in [0, 1): at 1 the remainder divides by zero.
/// </summary>
public sealed record FoodStateConfig(
    [property: JsonPropertyName("adaptationAbsorbableShortfall"), JsonRequired] double AdaptationAbsorbableShortfall);

/// <summary>
/// T4.21-1 (CR-015 §3.3) — the explicit FAMINE-CLASS production shock. All TUNE.
/// HazardPerYear (λ) — CHOSEN. Shipped at 0.0 by T4.21-1 (the plumbing packet:
///   its golden move is layout + RngStreams only) and armed at 0.01 by T4.21-4:
///   one famine-class local crop failure per settlement per century. Reference
///   class: pre-modern European regional famines — England 1300–1700 ≈ 5–6
///   famine-class events per 400 y (≈ 1/70 y); France by région 1500–1800
///   1/50–1/100 y; a single settlement's hinterland sees fewer than a région.
///   Band 1/50–1/150, the round central value. Onset per turn is the exact
///   integration P = 1 − exp(−λ dt) (law 3). λ = 0 is the null arm: no row is
///   ever written and the world differs from a no-disaster world by the
///   RngStreams rows alone (the attribution control).
/// DurationYears (D) — CHOSEN 5.0 inside the historical band 3–7 y (the 1315–22
///   Great Famine sequence incl. the murrain, 1601–03, 1695–97, the 1690s "seven
///   ill years", 1845–49), at the length the band derivation below requires.
/// SeverityMin / SeverityMax — DERIVED lower edge, historical upper: the fraction
///   of FOOD output lost per ACTIVE year, uniform on [0.75, 1.0]. Food output at
///   0–25% of normal in an active year (Irish potato 1846 ≈ 25% of normal;
///   1601–03 near-total locally). The LOWER EDGE is a dimensional derivation
///   against the food model's EFFECTIVE buffer at the coarsest era dt: a
///   famine-class event must be able to exhaust the full buffer of a settlement
///   at the shipped surplus ratio ALONE, i.e. s_min·D &gt; (dt_max(ρ_ship − 1) + G)/ρ_ship
///   with dt_max = 10 y, G = 1.5 y (granaryYearsOfDemand) and ρ_ship = 1.3 — the
///   DECLARED derivation input (Libur's weather-mean grain surplus ratio in the
///   director's playtest; the world mean ≈ 2.1 is inflated by sparse settlements
///   that overproduce and spoil). Threshold 3.46 production-years; D = 5 and
///   s_min = 0.75 give s·D ∈ [3.75, 5.0]. No outcome is targeted: ρ_ship enters
///   only as the size of the buffer the event must be able to beat. At s_max = 1
///   the turn multiplier 1 − s·min(D, dt)/dt stays ≥ 0; above 1 it would hand
///   Ledger.Flow a negative source under Throw, so the loader refuses it.
/// The band begins beyond the weather's practical reach at the YEAR scale (a
///   0.25 yearly multiplier is z = −4.57 under the shipped lognormal, once per
///   ≈ 400,000 settlement-years) — "not every extreme draw is a disaster" holds
///   by construction; at the DECADE scale the cause row, not the magnitude, is
///   what distinguishes them (CR-015 G1).
/// </summary>
public sealed record DisasterConfig(
    [property: JsonPropertyName("hazardPerYear"), JsonRequired] double HazardPerYear,
    [property: JsonPropertyName("durationYears"), JsonRequired] double DurationYears,
    [property: JsonPropertyName("severityMin"), JsonRequired] double SeverityMin,
    [property: JsonPropertyName("severityMax"), JsonRequired] double SeverityMax);

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
/// and attractiveness A = LandWeight × farmlandPerCapita (Prev reads, capita
/// floored at 1). T4.10 REMOVED the food term from A (director ruling,
/// Option A): T4.2's bounded store made raw grain magnitude meaningless as an
/// attractiveness signal, and its candidate replacement (1 − DeficitRatio)
/// measured identically 1.0 across the canonical world, so no coefficient for
/// it was derivable. Food reaches migration through FamineFlightFactor and
/// DestinationDeficitRepulsion instead. The deficit term is
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
    // T4.10: `attractivenessFoodWeight` REMOVED — the attractiveness food term
    // it weighted no longer exists, and a dead JsonRequired key would force
    // every config to keep carrying a number nothing reads.
    // T3.2b: denominated per FERTILITY-WEIGHTED km² of catchment (was per
    // fertility-weighted lattice node). Divided by 256 in the same commit that
    // multiplied the catchment quantity by 256 — a re-denomination, not a
    // re-tune; the product is bit-identical (both factors are powers of two).
    [property: JsonPropertyName("attractivenessLandWeight"), JsonRequired] double AttractivenessLandWeight,
    [property: JsonPropertyName("famineFlightFactor"), JsonRequired] double FamineFlightFactor,
    [property: JsonPropertyName("cohortProfile"), JsonRequired] double[] CohortProfile,
    [property: JsonPropertyName("gapClosingFraction"), JsonRequired] double GapClosingFraction,
    [property: JsonPropertyName("attractivenessSmoothingWindowYears"), JsonRequired] double AttractivenessSmoothingWindowYears,
    [property: JsonPropertyName("destinationDeficitRepulsion"), JsonRequired] double DestinationDeficitRepulsion,

    /// <summary>
    /// T4.13 (director ruling): how strongly derived settlement HAPPINESS
    /// modulates a destination's viability. Deliberately WEAK — it scales
    /// viability by (1 − w + w·happiness), so w is the maximum fraction of a
    /// destination's appeal that happiness can account for. Material survival
    /// keeps its own gates: the deficit repulsion and the absolute food gate
    /// both still zero viability outright, and happiness cannot raise a
    /// destination above what its land and food already justify.
    /// </summary>
    [property: JsonPropertyName("attractivenessHappinessWeight"), JsonRequired]
    double AttractivenessHappinessWeight);

/// <summary>
/// T3.6 trade tuning (D-034). Both TUNE, per S8 §4.1(c) provenance stated:
///
/// GapClosingFraction f — CHOSEN 0.25. Never derived. The fraction of the
///   gap-closing quantity a pair moves per turn. The mandate fixes only
///   f &lt; 1 (structural no-overshoot: the flow can never move more than the
///   quantity that would close the gap, so trade is a damped step toward
///   parity, never past it — the same shape as migration's
///   gapClosingFraction, T2.8). 0.25 sits well inside the stable region and
///   leaves the transport-cost deadband, not the cap, as the normally
///   binding throttle. VALIDATED in (0,1) at load — at 1 or above the
///   structural guarantee is gone, which is a config fault, not a tuning
///   choice.
///
/// CostPerBulkCostUnit — DERIVED 0.16 grain-value units per (bulk × travel
///   cost unit). The conversion that turns bulkPerUnit × path cost into the
///   same unit as a price gap (grain value per unit of good). ANCHOR: the
///   classic result the bulk table is already reviewed against — carting
///   GRAIN roughly DOUBLES its cost within about 100 km overland. Grain has
///   bulk 1.0 and price ≡ 1.0 (numeraire), so "doubles at ~100 km" means
///   transport cost ≈ 1.0 grain-value over 100 km. One travel cost unit is
///   KmPerNode = 16 km of ideal ground (LatticeGeometry: worldgen kmPerPx
///   4.0 × lattice stride 4), so 100 km ≈ 6.25 cost units, and
///   1.0 / (1.0 bulk × 6.25 cost units) = 0.16. Real terrain costs more per
///   km than ideal ground, so effective per-km cost is HIGHER than the
///   anchor on rough routes — the right direction (overland trade harder in
///   the mountains, easier along rivers/paths where edges are cheap).
/// </summary>
public sealed record TradeConfig(
    [property: JsonPropertyName("gapClosingFraction"), JsonRequired] double GapClosingFraction,
    [property: JsonPropertyName("costPerBulkCostUnit"), JsonRequired] double CostPerBulkCostUnit);

/// <summary>
/// T3.8 housing tuning (director ruling: maintenance, not abstract decay).
/// Provenance per S8 §4.1(c), stated per value; reference classes committed in
/// docs/t3.8-spec.md BEFORE any measurement.
///
/// TauYears — DERIVED 40 (band 25–60): the zero-maintenance habitable life of
///   a Neolithic dwelling (LBK timber-post rebuild cycles ~25–50 y; mudbrick
///   50–100 with continual replastering). The e-fold of unmaintained decay.
/// UpkeepTimberPerDwellingYear / UpkeepClayPerDwellingYear — DERIVED 0.05 /
///   0.025: annual upkeep ≈ replacement cost / lifetime = (2.0 timber +
///   1.0 clay) / 40, RC-H2's 2.5%/yr central (band 2–5%).
/// BuildTimberPerDwelling / BuildClayPerDwelling — 2.0 / 1.0: the material MIX
///   is the reference statement (timber frame + earth walls); absolute scale
///   checked against shipped production in the spec.
/// PersonsPerDwelling — DERIVED 6 (band 5–8, nuclear-to-extended household).
/// BuildLaborAdultYearsPerDwelling — CHOSEN 0.5 within a stated frame (a
///   household raises a house in a season with help; 5.0 would be a cathedral,
///   0.05 a tent).
/// SurplusCapRatio — CHOSEN 1.2, mechanism shape not tuning: houses are built
///   for households, not stockpiled; the cap stops construction labor from
///   compounding into an unbounded dwelling mountain (the B-2 shape) while
///   allowing a modest vacancy margin.
/// </summary>
public sealed record HousingConfig(
    [property: JsonPropertyName("tauYears"), JsonRequired] double TauYears,
    [property: JsonPropertyName("upkeepTimberPerDwellingYear"), JsonRequired] double UpkeepTimberPerDwellingYear,
    [property: JsonPropertyName("upkeepClayPerDwellingYear"), JsonRequired] double UpkeepClayPerDwellingYear,
    [property: JsonPropertyName("buildTimberPerDwelling"), JsonRequired] double BuildTimberPerDwelling,
    [property: JsonPropertyName("buildClayPerDwelling"), JsonRequired] double BuildClayPerDwelling,
    [property: JsonPropertyName("personsPerDwelling"), JsonRequired] double PersonsPerDwelling,
    [property: JsonPropertyName("buildLaborAdultYearsPerDwelling"), JsonRequired] double BuildLaborAdultYearsPerDwelling,
    [property: JsonPropertyName("surplusCapRatio"), JsonRequired] double SurplusCapRatio);

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
        ValidateNeedsAgainstRegistries(Load(simJson) with { Needs = NeedsConfigLoader.Load(needsJson) });

    /// <summary>T3.2: canonical three-file load — sim.json + needs.json +
    /// goods.json (the D-031 registry, attached as SimConfig.Goods). Systems
    /// that bind goods (Farming, Consumption at M3) refuse construction
    /// without it.</summary>
    public static SimConfig Load(Stream simJson, Stream needsJson, Stream goodsJson) =>
        ValidateNeedsAgainstRegistries(Load(simJson) with
        {
            Needs = NeedsConfigLoader.Load(needsJson),
            Goods = GoodsConfigLoader.Load(goodsJson),
        });

    /// <summary>ADR-029: canonical four-file load — the three-file load plus
    /// research.json (the Technology and Civics graph, D-044), attached as
    /// SimConfig.Research. The research content is validated against the goods
    /// registry it names in Eureka conditions (stock_&lt;good&gt;).</summary>
    public static SimConfig Load(Stream simJson, Stream needsJson, Stream goodsJson, Stream researchJson)
    {
        SimConfig cfg = Load(simJson, needsJson, goodsJson);
        cfg = cfg with { Research = Systems.Research.ResearchContentLoader.Load(researchJson, cfg.Goods) };
        ValidateRoadsAgainstContent(cfg);
        ValidateGovernanceAgainstContent(cfg);
        return cfg;
    }

    /// <summary>ADR-031: canonical six-file load — the four-file load plus ages.json and
    /// unit-families.json, attached as SimConfig.Ages and SimConfig.UnitFamilies. Unit
    /// families are validated against research.json's unit entities; every milestone fact
    /// against the research, goods, class and family content it names.</summary>
    public static SimConfig Load(Stream simJson, Stream needsJson, Stream goodsJson, Stream researchJson,
        Stream agesJson, Stream unitFamiliesJson)
    {
        SimConfig cfg = Load(simJson, needsJson, goodsJson, researchJson);
        return WithProgression(cfg, agesJson, unitFamiliesJson);
    }

    /// <summary>ADR-031: attaches ages.json and unit-families.json to an already-loaded config
    /// (validated against its research, goods and class registries).</summary>
    public static SimConfig WithProgression(SimConfig cfg, Stream agesJson, Stream unitFamiliesJson)
    {
        Ages.UnitFamilyContent families = Ages.UnitFamilyContentLoader.Load(unitFamiliesJson, cfg.Research);
        Ages.AgeContent ages = Ages.AgeContentLoader.Load(agesJson, cfg.Research, cfg.Goods, cfg.Registries, families);
        return cfg with { Ages = ages, UnitFamilies = families };
    }

    /// <summary>
    /// T3.5b item 4 — the two cross-file guards, at the only point where
    /// needs.json and sim.json's class registry meet. Silently-inert configs
    /// are the deepest failure class this project has (deepsea.png, the
    /// untested SectorAllocation path, the rail deletable with 333 green), so
    /// both REFUSE the load with an actionable message — a guard that logged
    /// and continued would be the fault it exists to fix.
    ///
    /// (a) A basket entry naming a class id absent from sim.json's class
    ///     registry loads clean today and renders the need fully inert for
    ///     that entry — T3.5's inertness lens drove a bound need at weight 1e9
    ///     through this path to a bit-identical world hash. With two classes
    ///     shipped, a stray 3 is a realistic typo.
    /// (b) A PER-CLASS hole in a bound need's basket is silent and inverts
    ///     the mechanism: dropping artisans' Shelter lines made artisans LESS
    ///     aggrieved under total roof collapse (measured 0.7075 vs 0.7236),
    ///     because dropping the need drops its weight from the accrual scale.
    ///     The guard is (class, need)-scoped: every bound need that any class
    ///     baskets must be basketed by EVERY registry class.
    /// </summary>
    private static SimConfig ValidateNeedsAgainstRegistries(SimConfig cfg)
    {
        if (cfg.Needs is null) return cfg;
        ClassEntry[] classes = cfg.Registries.Classes;
        BasketEntry[] entries = cfg.Needs.Baskets.Entries;

        for (int i = 0; i < entries.Length; i++)
        {
            bool known = false;
            for (int c = 0; c < classes.Length; c++)
                if (classes[c].Id == entries[i].Class) { known = true; break; }
            if (!known)
            {
                var valid = new System.Text.StringBuilder();
                for (int c = 0; c < classes.Length; c++)
                {
                    if (c > 0) valid.Append(", ");
                    valid.Append(classes[c].Id).Append(" (").Append(classes[c].Name).Append(')');
                }
                throw new NeedsConfigException(
                    $"needs.json baskets.entries[{i}] names class id {entries[i].Class}, which is not in "
                    + $"sim.json's class registry (valid ids: {valid}). A basket for an unknown class is "
                    + "SILENTLY INERT — the need never binds for anyone, whatever its weight says. Fix the "
                    + "class id, or add the class to sim.json registries.classes.");
            }
        }

        for (int n = 0; n < cfg.Needs.Needs.Length; n++)
        {
            NeedEntry need = cfg.Needs.Needs[n];
            if (!need.Bound) continue;
            bool anyClassBasketsThis = false;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].Need == need.Id) { anyClassBasketsThis = true; break; }
            if (!anyClassBasketsThis) continue;   // a bound need nobody baskets is T2.6's zero-effect gate, not a hole

            for (int c = 0; c < classes.Length; c++)
            {
                bool has = false;
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].Need == need.Id && entries[i].Class == classes[c].Id) { has = true; break; }
                if (!has)
                {
                    throw new NeedsConfigException(
                        $"needs.json: bound need {need.Id} ({need.Name}) has basket entries for some classes "
                        + $"but NONE for class {classes[c].Id} ({classes[c].Name}). A per-class hole is "
                        + "silently inverted mechanism: the class with the hole becomes LESS aggrieved under "
                        + "total deprivation, because the missing need's weight drops out of its accrual "
                        + $"scale. Add at least one baskets.entries line with class {classes[c].Id} and need "
                        + $"{need.Id}, or unbind the need.");
                }
            }
        }
        return cfg;
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
        RequireRate("farming.yieldPerArableKm2PerYear", cfg.Farming.YieldPerArableKm2PerYear);
        RequireRate("farming.outputPerFarmerPerYear", cfg.Farming.OutputPerFarmerPerYear);

        if (cfg.Catchment is null) throw new SimConfigException("catchment is missing.");
        if (!(cfg.Catchment.HinterlandRadiusKm > 0.0)
            || double.IsNaN(cfg.Catchment.HinterlandRadiusKm)
            || double.IsInfinity(cfg.Catchment.HinterlandRadiusKm))
            throw new SimConfigException(
                "catchment.hinterlandRadiusKm must be a finite positive distance in km, got "
                + $"{Inv(cfg.Catchment.HinterlandRadiusKm)}.");
        RequireRate("catchment.sizeBonusMaxRatio", cfg.Catchment.SizeBonusMaxRatio);
        if (!(cfg.Catchment.SizeDwellingsRef > 0.0) || !double.IsFinite(cfg.Catchment.SizeDwellingsRef))
            throw new SimConfigException(
                $"catchment.sizeDwellingsRef must be a finite value > 0 (it divides the dwelling stock " +
                $"into size tiers), got {Inv(cfg.Catchment.SizeDwellingsRef)}.");

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
        RequireRate("demographics.headroomRelaxationPerYear", cfg.Demographics.HeadroomRelaxationPerYear);
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

        if (cfg.Transport is null) throw new SimConfigException("transport is missing.");
        if (!(cfg.Transport.RiverCostFactor > 0.0 && cfg.Transport.RiverCostFactor <= 1.0))
            throw new SimConfigException(
                $"transport.riverCostFactor must be in (0,1] (a river must be cheaper than ideal "
                + $"ground to be a corridor, and cannot be free), got {Inv(cfg.Transport.RiverCostFactor)}.");

        if (cfg.Roads is not null) ValidateRoads(cfg.Roads);
        if (cfg.Governance is not null) ValidateGovernance(cfg.Governance);

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
        if (!(cfg.Migration.AttractivenessHappinessWeight >= 0.0
              && cfg.Migration.AttractivenessHappinessWeight <= 1.0))
        {
            throw new SimConfigException(
                "migration.attractivenessHappinessWeight must be in [0,1] — it is the FRACTION "
                + "of destination viability that happiness may account for, and a value above 1 "
                + "would let happiness alone drive viability negative. Got "
                + cfg.Migration.AttractivenessHappinessWeight.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        if (!(cfg.Migration.DestinationDeficitRepulsion >= 1.0)
            || !double.IsFinite(cfg.Migration.DestinationDeficitRepulsion))
            throw new SimConfigException(
                $"migration.destinationDeficitRepulsion must be a finite value >= 1 — below 1 a fully " +
                $"starving destination (deficit 1.0) would still RECEIVE migrants, which is the T2.13 " +
                $"starvation-magnetism defect this parameter exists to kill; got " +
                $"{Inv(cfg.Migration.DestinationDeficitRepulsion)}.");

        if (cfg.Housing is null) throw new SimConfigException("housing is missing.");
        if (!(cfg.Housing.TauYears > 0.0) || !double.IsFinite(cfg.Housing.TauYears))
            throw new SimConfigException(
                $"housing.tauYears must be a finite value > 0 — at 0 or below every dwelling dies the " +
                $"instant maintenance lapses and the stock the mechanism exists to give memory to has " +
                $"none; got {Inv(cfg.Housing.TauYears)}.");
        RequireRate("housing.upkeepTimberPerDwellingYear", cfg.Housing.UpkeepTimberPerDwellingYear);
        RequireRate("housing.upkeepClayPerDwellingYear", cfg.Housing.UpkeepClayPerDwellingYear);
        RequireRate("housing.buildTimberPerDwelling", cfg.Housing.BuildTimberPerDwelling);
        RequireRate("housing.buildClayPerDwelling", cfg.Housing.BuildClayPerDwelling);
        if (!(cfg.Housing.PersonsPerDwelling > 0.0) || !double.IsFinite(cfg.Housing.PersonsPerDwelling))
            throw new SimConfigException(
                $"housing.personsPerDwelling must be a finite value > 0 (it divides population into " +
                $"dwelling demand), got {Inv(cfg.Housing.PersonsPerDwelling)}.");
        RequireRate("housing.buildLaborAdultYearsPerDwelling", cfg.Housing.BuildLaborAdultYearsPerDwelling);
        if (!(cfg.Housing.SurplusCapRatio >= 1.0) || !double.IsFinite(cfg.Housing.SurplusCapRatio))
            throw new SimConfigException(
                $"housing.surplusCapRatio must be a finite value >= 1 — below 1 the cap forbids housing " +
                $"the population it exists to house; got {Inv(cfg.Housing.SurplusCapRatio)}.");

        if (cfg.Trade is null) throw new SimConfigException("trade is missing.");
        if (!(cfg.Trade.GapClosingFraction > 0.0 && cfg.Trade.GapClosingFraction < 1.0))
            throw new SimConfigException(
                $"trade.gapClosingFraction must be in (0,1) — the D-034 mandate fixes f < 1: at 1 or " +
                $"above the flow can overshoot the gap it is closing and the structural no-overshoot " +
                $"guarantee is gone; at 0 or below trade is silently inert. Got " +
                $"{Inv(cfg.Trade.GapClosingFraction)}.");
        if (!(cfg.Trade.CostPerBulkCostUnit > 0.0) || !double.IsFinite(cfg.Trade.CostPerBulkCostUnit))
            throw new SimConfigException(
                $"trade.costPerBulkCostUnit must be a finite value > 0 — at 0 the transport-cost deadband " +
                $"vanishes and every price gap trades regardless of distance, got " +
                $"{Inv(cfg.Trade.CostPerBulkCostUnit)}.");

        // T4.21-1 (CR-015): the food-state classification and the disaster shock.
        if (cfg.FoodState is null) throw new SimConfigException("foodState is missing.");
        if (!(cfg.FoodState.AdaptationAbsorbableShortfall >= 0.0
              && cfg.FoodState.AdaptationAbsorbableShortfall < 1.0))
            throw new SimConfigException(
                $"foodState.adaptationAbsorbableShortfall must be in [0,1) — it is the dead-zone of " +
                $"the effective deficit AND the SEVERE threshold; at 1 the unabsorbed remainder " +
                $"(d − a)/(1 − a) divides by zero, and a negative value would starve a fed settlement. " +
                $"Got {Inv(cfg.FoodState.AdaptationAbsorbableShortfall)}.");

        if (cfg.Disaster is null) throw new SimConfigException("disaster is missing.");
        RequireRate("disaster.hazardPerYear", cfg.Disaster.HazardPerYear);
        if (!(cfg.Disaster.DurationYears > 0.0) || !double.IsFinite(cfg.Disaster.DurationYears))
            throw new SimConfigException(
                $"disaster.durationYears must be a finite value > 0 — it is the years a crop failure " +
                $"lasts, and at 0 or below the event is a no-op that still writes rows; got " +
                $"{Inv(cfg.Disaster.DurationYears)}.");
        if (!(cfg.Disaster.SeverityMin >= 0.0 && cfg.Disaster.SeverityMin <= 1.0)
            || !(cfg.Disaster.SeverityMax >= 0.0 && cfg.Disaster.SeverityMax <= 1.0)
            || !(cfg.Disaster.SeverityMin <= cfg.Disaster.SeverityMax))
            throw new SimConfigException(
                $"disaster.severityMin/severityMax must satisfy 0 <= min <= max <= 1 — severity is the " +
                $"fraction of food output lost per active year, and above 1 the production multiplier " +
                $"goes negative and Ledger.Flow throws on a negative source; got min " +
                $"{Inv(cfg.Disaster.SeverityMin)}, max {Inv(cfg.Disaster.SeverityMax)}.");

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

    /// <summary>ADR-032: every road class's research entity exists in research.json and every
    /// material names a goods.json good — checked where the three files meet.</summary>
    private static void ValidateRoadsAgainstContent(SimConfig cfg)
    {
        if (cfg.Roads is null || cfg.Research is null || cfg.Goods is null) return;
        foreach (RoadClassConfig c in cfg.Roads.Classes)
        {
            if (c.Entity is not null && cfg.Research.EntityIndexOf(c.Entity) < 0)
                throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} names entity '{c.Entity}', which research.json does not define.");
            foreach (RoadMaterialConfig m in c.MaterialsPerKm)
                if (cfg.Goods.IdOf(m.Good) < 0)
                    throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} names good '{m.Good}', which goods.json does not define.");
        }
    }

    /// <summary>ADR-033 D4: the governing loop's tuning — a positive finite reach e-fold, a finite
    /// non-negative extraction response, a non-empty taxation requirement, and the AI valve's
    /// constants on their scales with a real dead band (troubled strictly below comfortable).</summary>
    private static void ValidateGovernance(GovernanceConfig g)
    {
        if (!(g.AuthorityDecayCostUnits > 0.0) || !double.IsFinite(g.AuthorityDecayCostUnits))
            throw new SimConfigException(
                $"governance.authorityDecayCostUnits must be a finite value > 0 (it is the e-fold of administrative reach), got {Inv(g.AuthorityDecayCostUnits)}.");
        RequireRate("governance.taxExtractionResponseMax", g.TaxExtractionResponseMax);
        if (string.IsNullOrWhiteSpace(g.TaxationRequires))
            throw new SimConfigException(
                "governance.taxationRequires is empty — the tax edict is research-gated (ADR-033 D4); name the research.json node ids that make it available.");
        GovernanceAiConfig? ai = g.Ai;
        if (ai is null) throw new SimConfigException("governance.ai is missing.");
        if (!(ai.ComfortableLegitimacy >= 0.0 && ai.ComfortableLegitimacy <= 100.0))
            throw new SimConfigException($"governance.ai.comfortableLegitimacy must be in [0,100], got {Inv(ai.ComfortableLegitimacy)}.");
        if (!(ai.TroubledLegitimacy >= 0.0 && ai.TroubledLegitimacy < ai.ComfortableLegitimacy))
            throw new SimConfigException(
                $"governance.ai.troubledLegitimacy must be in [0, comfortableLegitimacy) — the dead band between the two is what stops the AI oscillating — got {Inv(ai.TroubledLegitimacy)}.");
        if (!(ai.StepPercent > 0.0 && ai.StepPercent <= 100.0))
            throw new SimConfigException($"governance.ai.stepPercent must be in (0,100], got {Inv(ai.StepPercent)}.");
        if (!(ai.MaxRatePercent >= 0.0 && ai.MaxRatePercent <= 100.0))
            throw new SimConfigException($"governance.ai.maxRatePercent must be in [0,100] (a legal SetTaxRate percentage), got {Inv(ai.MaxRatePercent)}.");
    }

    /// <summary>ADR-033 D4: the taxation requirement names real research.json nodes and is a valid
    /// knowledge expression — parsed by the research dialect's own parser where sim.json and
    /// research.json meet, so a typo fails the load instead of silently never unlocking taxation.</summary>
    private static void ValidateGovernanceAgainstContent(SimConfig cfg)
    {
        if (cfg.Governance is null || cfg.Research is null) return;
        try
        {
            Systems.Research.ResearchContentLoader.ParseRequirement(
                cfg.Research, cfg.Governance.TaxationRequires, "governance.taxationRequires");
        }
        catch (Systems.Research.ResearchContentException e)
        {
            throw new SimConfigException($"sim.json {e.Message}", e);
        }
    }

    /// <summary>ADR-032: every road class once, DirtPath present with no entity, factors in (0,1],
    /// capacities and material quantities non-negative, finite speeds.</summary>
    private static void ValidateRoads(RoadsConfig roads)
    {
        if (!(roads.BaselineKmPerDay > 0.0) || !double.IsFinite(roads.BaselineKmPerDay))
            throw new SimConfigException($"roads.baselineKmPerDay must be a finite value > 0, got {Inv(roads.BaselineKmPerDay)}.");
        if (!(roads.MaxRouteKm > 0.0) || !double.IsFinite(roads.MaxRouteKm))
            throw new SimConfigException($"roads.maxRouteKm must be a finite value > 0, got {Inv(roads.MaxRouteKm)}.");
        if (roads.Classes is null || roads.Classes.Length == 0) throw new SimConfigException("roads.classes is missing.");
        foreach (int t in State.EdgeTypes.RoadClasses)
        {
            int n = 0;
            foreach (RoadClassConfig c in roads.Classes) if (c.EdgeType == t) n++;
            if (n != 1) throw new SimConfigException($"roads.classes must define edgeType {t} exactly once, found {n}.");
        }
        foreach (RoadClassConfig c in roads.Classes)
        {
            if (!State.EdgeTypes.IsRoadClass(c.EdgeType))
                throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} is not a road class.");
            if ((c.EdgeType == State.EdgeTypes.DirtPath) != (c.Entity is null))
                throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} — only DirtPath (the free baseline) has no research entity.");
            if (!(c.SpeedFactor > 0.0 && c.SpeedFactor <= 1.0))
                throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} speedFactor must be in (0,1], got {Inv(c.SpeedFactor)}.");
            if (c.CapacityTonnesPerYear <= 0)
                throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} capacityTonnesPerYear must be > 0.");
            if (c.MaterialsPerKm is null) throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} materialsPerKm is missing.");
            foreach (RoadMaterialConfig m in c.MaterialsPerKm)
                if (m.Good is null || !(m.Qty >= 0.0) || !double.IsFinite(m.Qty))
                    throw new SimConfigException($"roads.classes: edgeType {c.EdgeType} has an invalid material entry.");
            if (c.EdgeType == State.EdgeTypes.DirtPath && c.MaterialsPerKm.Length != 0)
                throw new SimConfigException("roads.classes: DirtPath is the FREE baseline — it must cost nothing.");
        }
        // The Director's ruling 22: DirtPath < Trackway < BuiltRoad < PavedRoad < MacadamRoad <
        // Highway in travel performance — speedFactor (a cost per km) STRICTLY decreasing.
        for (int i = 1; i < State.EdgeTypes.RoadClasses.Length; i++)
        {
            RoadClassConfig lo = roads.ClassOf(State.EdgeTypes.RoadClasses[i - 1])!, hi = roads.ClassOf(State.EdgeTypes.RoadClasses[i])!;
            if (!(hi.SpeedFactor < lo.SpeedFactor))
                throw new SimConfigException($"roads.classes: edgeType {hi.EdgeType} must be strictly faster (lower speedFactor) than edgeType {lo.EdgeType}.");
        }
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
