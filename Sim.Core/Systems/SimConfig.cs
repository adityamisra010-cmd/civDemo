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
    // the tax gate (taxationRequires, its research half; taxationMinAge, its Age half, H2)
    // and the AI tax valve. OPTIONAL and in the DEFAULTED TAIL
    // (Roads above is positional): absent, the governing loop is INERT — GovernanceSystem
    // applies no SetTaxRate, leaves ControlRow.Strength as founding wrote it, and every
    // Governance reader returns the neutral value (no tax, extraction ×1, burden ×1). Toy and
    // hand-written configs therefore run the full catalog unchanged; the canonical sim.json
    // carries the section.
    [property: JsonPropertyName("governance")] GovernanceConfig? Governance = null,
    // ADR-033 D6 (institutions; docs/institutions-universities.md): the university lifecycle's tuning —
    // maturation, viability, staffing, the research-cost and health effects. OPTIONAL and in the
    // defaulted tail: absent, InstitutionsSystem is INERT (founds nothing, writes no cost factor), the
    // staffing reader returns the raw adult count and the mortality seam returns 1.0, so toy and
    // hand-written configs run the full catalog unchanged; the canonical sim.json carries the section.
    [property: JsonPropertyName("institutions")] InstitutionsConfig? Institutions = null,
    // ADR-036 (D-051 §5, the Found Settlement order): the party fraction. OPTIONAL and in the defaulted tail:
    // absent, no Found Settlement order can form a party (FoundingQuery answers NoParty) and the order is inert;
    // emergent D-037 B1 colonization never reads it. The canonical sim.json carries the section.
    [property: JsonPropertyName("colonization")] ColonizationConfig? Colonization = null,
    // D-051 §6 applied to construction (the empire purse; EmpireStockpile / ConstructionQuery.Holders; authority D-051 §6
    // and D-053 §1, the Director's 2026-10-13 instruction, item 1). OPTIONAL and in
    // the defaulted tail: absent (or empireStockpile false), a construction project draws on its own settlement's stock
    // alone — the pre-change rule, bit-exact. The canonical sim.json carries the section, ON.
    [property: JsonPropertyName("construction")] ConstructionConfig? Construction = null,
    // M5 item 5 (granaries): the two granary effects — the LOCAL farm-output factor and the EMPIRE-WIDE famine-intensity
    // factor (State.GranaryEffects). OPTIONAL and in the defaulted tail: absent (or enabled false) both factors are the
    // literal 1.0 and neither ProductionSystem nor DemographicsSystem multiplies by them, so toy and hand-written configs
    // (and the attribution control TestConfigs.PreGranaryEffects) run exactly as before. The canonical sim.json carries it.
    [property: JsonPropertyName("granaries")] GranariesConfig? Granaries = null,
    // M6 Track EX (D-062, ADR-044): concealed geological deposits and the Prospect order. OPTIONAL and in the defaulted
    // tail: absent (or enabled false) no deposit is concealed, every consumer reads DepositRow.Abundance as before and
    // the Prospect order is refused — the attribution control (TestConfigs.PreDiscovery). The canonical sim.json carries it.
    [property: JsonPropertyName("discovery")] DiscoveryConfig? Discovery = null,
    // M6 Track EX (D-062, ADR-044): the minimal fog of war (known extent, sight, settlement memory) and the Explore order.
    // OPTIONAL and in the defaulted tail: absent (or enabled false) PolityKnowledge.Of answers all-known exactly as before,
    // DiscoverySystem writes nothing and the Explore order is refused. The canonical sim.json carries it, ON.
    [property: JsonPropertyName("fog")] FogConfig? Fog = null);

/// <summary>
/// M6 Track EX (D-062; ADR-044) — CONCEALED DEPOSITS AND PROSPECTING. <c>Enabled</c>: THE SWITCH (false or absent: nothing
/// is concealed, Prospect is refused — the attribution control). <c>ConcealableGoods</c>: the goods.json deposit goods a
/// deposit of which may be concealed (geological deposits; biological and food goods are surface-evident).
/// <c>ConcealedShare</c> s in [0, 1]: per settlement, k = floor(n × s + u) of its n eligible rows are concealed
/// (State.DepositConcealment).
/// THE PROSPECT BILL (ADR-046, M6 review 2026-10-23 — the Director: grain, stone, timber and metal, no deadlock):
/// <c>ProspectCost</c> — the basic SURVEY a Prospect order (Amount 0) sinks, whole units per good, drawn like Explore's through
/// EmpireStockpile.Draw (reason Prospecting); the loader refuses a basic bill naming a concealable good or any good made
/// from one (bronze, tools — the goods the survey exists to find), so the first survey never waits on itself.
/// <c>AssayCost</c> + <c>AssayRequires</c> — the improved ASSAY (Prospect order Amount 1): offered only once the polity's
/// completed knowledge meets <c>AssayRequires</c> (ore_prospecting); its bill may name a metal (bronze), the real metal sink.
/// Both absent: no assay. Every good of a bill must be held (beyond construction's set-asides) or nothing is paid.
/// </summary>
public sealed record DiscoveryConfig(
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("concealableGoods"), JsonRequired] string[] ConcealableGoods,
    [property: JsonPropertyName("concealedShare"), JsonRequired] double ConcealedShare,
    [property: JsonPropertyName("prospectCost"), JsonRequired] DiscoveryCostItem[] ProspectCost,
    [property: JsonPropertyName("assayCost")] DiscoveryCostItem[]? AssayCost = null,
    [property: JsonPropertyName("assayRequires")] string? AssayRequires = null);

/// <summary>ADR-046: one line of a Prospect bill — a goods.json good and a whole quantity ≥ 0.</summary>
public sealed record DiscoveryCostItem(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("qty"), JsonRequired] long Qty);

/// <summary>
/// M6 Track EX (D-062; ADR-044) — THE MINIMAL FOG. Distances are in traversal-lattice NODES (16 km each; PolityKnowledge.Grid).
/// <c>Enabled</c>: THE SWITCH (false or absent: all-known, nothing written — the attribution control).
/// <c>SeedRadiusNodes</c> (3 ≈ 50 km, the hinterland constant): every step each polity knows the disc of this radius
/// around every settlement it controls (rule E2), and an Explore starts from it. <c>FormationSightNodes</c> (1): a
/// formation sees the disc of this radius around itself (rule E4). <c>ExploreStepNodes</c> (3): what one Explore adds to a
/// settlement's explored radius; <c>ExploreMaxRadiusNodes</c> (15): the cap. <c>ExploreGrain</c> (whole grain ≥ 0): the
/// provisions one Explore sinks (reason Expedition).
/// </summary>
public sealed record FogConfig(
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("seedRadiusNodes"), JsonRequired] int SeedRadiusNodes,
    [property: JsonPropertyName("formationSightNodes"), JsonRequired] int FormationSightNodes,
    [property: JsonPropertyName("exploreStepNodes"), JsonRequired] int ExploreStepNodes,
    [property: JsonPropertyName("exploreMaxRadiusNodes"), JsonRequired] int ExploreMaxRadiusNodes,
    [property: JsonPropertyName("exploreGrain"), JsonRequired] long ExploreGrain);

/// <summary>
/// D-051 §6 applied to construction (authority: D-051 §6 and D-053 §1 — the Director's 2026-10-13 instruction, item 1:
/// construction and roads draw on the empire stockpile) — the construction queue's content switch. <c>EmpireStockpile</c> (true in the
/// shipped sim.json): a settlement an Empire controls builds from the goods of EVERY settlement that Empire controls,
/// drawn instantly and losslessly in a stated donor order (Sim.Core/State/EmpireStockpile.cs); a stateless settlement
/// builds from its own; the obtainability guard reads that purse UNCLAIMED (less every queued project's inputs,
/// ConstructionQuery.Claimed). False or absent: the settlement's own stock only and no claims, exactly as before — the attribution control
/// (TestConfigs.PreEmpireConstructionStockpile), never a gameplay setting. Roads already drew on the civilization's
/// purse (ADR-032) and are not switched by it.
///
/// <c>Incremental</c> (WS1, D-054 — the Director's 2026-10-14 instruction, workstream 1; true in the shipped sim.json):
/// INCREMENTAL CONSTRUCTION — a queue head progresses a little each turn as far as its builders and the next units of
/// its materials allow, the materials leaving the purse in lockstep with the work, and what has gone in is never lost
/// while it waits (ConstructionSystem's header; progress in ConstructionProgressRow, schema v33). False or absent: M4-D's
/// whole-or-nothing gate exactly as before (batch 2) — the attribution control (TestConfigs.PreIncrementalConstruction),
/// never a gameplay setting.
///
/// <c>AllocationRatios</c> (WS2, D-054 — the Director's 2026-10-14 instruction, workstream 2; true in the shipped
/// sim.json): EDITABLE BUILDING/ROADS MATERIAL SPLIT — each Empire's standing BuildingsPercent b (ResourceAllocationRow,
/// order kind 12; absent = 100, building first) guarantees building work b shares in 100 and road works the rest of each
/// eligible good the Empire's purse holds when building work starts, on a turn the Empire's road order is delivered;
/// whatever one side does not need, the other may use (State.ResourceAllocation). The LOADER REFUSES it without
/// <c>Incremental</c> and <c>EmpireStockpile</c> (a whole-or-wait head capped to a share could never build, and the split
/// is an Empire purse's), and the rule is inert under any such combination built in code. False or absent: building
/// work first, road works get what it leaves, exactly as with WS1 alone — the attribution control
/// (TestConfigs.PreAllocationRatios), never a gameplay setting.
///
/// <c>Reservations</c> (D-055 — the Director's batch-4 instruction, item 1: projects must not consume resources reserved
/// by other projects; true in the shipped sim.json): MATERIAL RESERVATIONS ACROSS TURNS — each queue head sets aside what
/// it still needs, and the set-asides are published per purse and good in the ConstructionHolds table (schema v34, section
/// 61; State.Reservations), which construction CLEARS at the start of every step whatever this switch says. The LOADER
/// REFUSES it without <c>Incremental</c> (a hold is the remaining need of a part-built head; the whole-or-wait gate has
/// none). The Empire purse is NOT required: the purse key follows the holder set (ConstructionQuery.Holders), so with
/// <c>EmpireStockpile</c> off every purse is a single settlement's. False or absent: nothing is set aside and nothing is
/// published — the batch-3 rule exactly, plus the empty section-61 count prefix — the attribution control
/// (TestConfigs.PreReservations), never a gameplay setting.
///
/// <c>ContinueWithinTurn</c> (D-058 — the Director's ruling of 2026-10-19, "Build one at a time, so that if 6 can be built
/// and an order of 9 is given, and in that turn, resources are available for 1 more and then some, it should show in the
/// next turn as 7 built, 1 under construction, 1 not started"; true in the shipped sim.json): construction stays
/// SEQUENTIAL — one project of a settlement's queue at a time, in queue order — but the settlement's building capacity of
/// ONE step is no longer capped at one project: when the head completes, what is left of that step's capacity continues
/// to the next entry, which is resolved in the same step at its own order's place in the building order, under the same
/// set-aside rules as any head (ConstructionQuery.HoldsWalk). The LOADER REFUSES it without <c>Incremental</c> (the
/// whole-or-wait gate has no partial capacity to carry). False or absent: the promoted entry is a head from the NEXT
/// step and the completing step's leftover capacity reaches PathBuild's bank only — the batch-4 rule exactly — the
/// attribution control (TestConfigs.PreContinueWithinTurn), never a gameplay setting.
/// </summary>
public sealed record ConstructionConfig(
    [property: JsonPropertyName("empireStockpile")] bool EmpireStockpile = false,
    [property: JsonPropertyName("incremental")] bool Incremental = false,
    [property: JsonPropertyName("allocationRatios")] bool AllocationRatios = false,
    [property: JsonPropertyName("reservations")] bool Reservations = false,
    [property: JsonPropertyName("continueWithinTurn")] bool ContinueWithinTurn = false);

/// <summary>
/// M5 item 5 (D-053 §3) — THE GRANARY EFFECTS' CONTENT (<see cref="State.GranaryEffects"/>). Two distinct, bounded,
/// saturating effects of the goods.json project <c>Project</c> names, each the ADR-028 / InstitutionEffects curve
/// S = 1 − e^(−x):
/// <list type="bullet">
/// <item><c>Enabled</c> (true): THE SWITCH. False (or the section absent) makes both factors the literal 1.0 and skips
///   both multiplications, which reproduces the pre-granary-effects world byte for byte (TestConfigs.PreGranaryEffects,
///   GranaryAttributionTests).</item>
/// <item><c>Project</c> ("granary"): the goods.json project whose built Structures count — data, never a code literal
///   (the ages.json <c>structures</c> fact names projects the same way).</item>
/// <item><c>FarmYieldBonusMax</c> (0.05, TUNE) / <c>FarmYieldScale</c> (1.0): LOCAL. ProductionSystem.Farm multiplies the
///   settlement's REALISED CULTIVATED grain rate by 1 + max × (1 − e^(−m/scale)), m = granaries standing AT that
///   settlement — the part of its own crop a dedicated store saves from post-harvest loss; never applied while the
///   settlement still harvests wild food. 0.05 recovers at most about half of the ×0.90 storage, vermin and threshing
///   loss ADR-013 deducts inside the derived yield (threshing loss stays). The maximum is in [0, 1); the factor never
///   exceeds 1 + max.</item>
/// <item><c>FamineReductionMax</c> (0.25, TUNE) / <c>FamineReductionScale</c> (2.0, TUNE): EMPIRE-WIDE. DemographicsSystem
///   multiplies the EFFECTIVE deficit (the one input of starvation mortality and fertility suppression) by
///   1 − max × (1 − e^(−n/scale)), n = granaries standing anywhere in the settlement's civilization (its own count
///   when stateless; an absolute count, independent of the civilization's size) — organised relief capacity, NOT
///   stored grain: no grain moves. The maximum is in [0, 1), so the factor stays in [1 − max, 1]: the effective
///   deficit is never inverted and never raised.</item>
/// </list>
/// Reference classes and labels (DERIVED / IDENTITY / TUNE) in sim.json <c>granaries._doc</c>. Neither effect touches
/// the store: spoilage (consumption.grainSpoilagePerYear) and the store bound (granaryYearsOfDemand) are unchanged.
/// </summary>
public sealed record GranariesConfig(
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("project"), JsonRequired] string Project,
    [property: JsonPropertyName("farmYieldBonusMax"), JsonRequired] double FarmYieldBonusMax,
    [property: JsonPropertyName("farmYieldScale"), JsonRequired] double FarmYieldScale,
    [property: JsonPropertyName("famineReductionMax"), JsonRequired] double FamineReductionMax,
    [property: JsonPropertyName("famineReductionScale"), JsonRequired] double FamineReductionScale);

/// <summary>
/// ADR-036 (PROPOSED) — the Found Settlement order's one content number. <c>OrderPartyFraction</c> (TUNE, in (0, 1)):
/// the party is this fraction of the SOURCE settlement's population, floored to whole people and allocated per
/// bucket (FoundingQuery.AllocateParty), never emptying a non-empty bucket. The fixed five-field OrderRecord cannot
/// carry a party size (TargetId = source, Amount = site), so the size is content, not an order payload. It is read
/// ONLY on the order path: emergent D-037 B1 founding sizes its party from migration's unplaced demand and is
/// untouched (T4.4 §8 standing prohibition on any B1 threshold or TUNE). Reference class in sim.json
/// <c>colonization._doc</c>.
/// </summary>
public sealed record ColonizationConfig(
    [property: JsonPropertyName("orderPartyFraction"), JsonRequired] double OrderPartyFraction,
    // M5 S1c (ADR-036 §8): the AI founding policy's switch and trigger thresholds. OPTIONAL: absent, the policy
    // is INERT (no AI polity ever issues a Found Settlement order), so toy and hand-written configs are untouched.
    [property: JsonPropertyName("aiFounding")] AiFoundingConfig? AiFounding = null);

/// <summary>
/// M5 S1c (ADR-036 §8) — THE AI FOUNDING POLICY'S CONTENT (Sim.Core/Systems/Colonization/AiFoundingPolicy.cs).
/// Every number is TUNE; each reference class is in sim.json <c>colonization.aiFounding._doc</c>, fixed before measuring.
/// <list type="bullet">
/// <item><c>Enabled</c> (true): THE SWITCH. False reproduces every pre-S1c world byte for byte — the policy then
///   issues nothing, and nothing else reads this section (the attribution control,
///   AiFoundingAttributionTests).</item>
/// <item><c>MinSourcePopulation</c> (500 people): an AI settlement sends colonists only at or above this size —
///   DERIVED as the turn-zero founding size (founding.cohortCounts, 400) ÷ (1 − orderPartyFraction 0.2), so the
///   source keeps at least a newly founded settlement's population once the party has left.</item>
/// <item><c>MinYearsBetweenFoundings</c> (350 years): at most one new settlement per AI polity in this many years —
///   anchored to the Age ladder's own expansion milestones (a4_expansion 14, a6_towns 18), which a polity holding 6
///   of the canonical 12 at founding reaches at this pace by about the historical Iron Age. A DURATION in years
///   (law 3), measured as a LOWER bound (turns elapsed × the current turn length: the era table only ever shortens
///   turns), so it never opens early.</item>
/// </list>
/// </summary>
public sealed record AiFoundingConfig(
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("minSourcePopulation"), JsonRequired] long MinSourcePopulation,
    [property: JsonPropertyName("minYearsBetweenFoundings"), JsonRequired] double MinYearsBetweenFoundings);

/// <summary>ADR-033 D6 — the institutions section of sim.json. Today it carries the five specialized
/// universities' tuning only; other institution types follow the same lifecycle later (out of scope).</summary>
public sealed record InstitutionsConfig(
    [property: JsonPropertyName("universities"), JsonRequired] UniversitiesConfig Universities);

/// <summary>
/// ADR-033 D6 — THE UNIVERSITY LIFECYCLE'S TUNING (every number TUNE; each reference class is in
/// docs/institutions-universities.md and sim.json <c>institutions._doc</c>, fixed before measuring).
/// <list type="bullet">
/// <item><c>MaturityTauYears</c> (16): e-fold of the maturity gap while viable, M' = 1 − (1 − M)e^(−dt/τ).</item>
/// <item><c>DecayTauYears</c> (21.7): e-fold of maturity while NOT viable, M' = M e^(−dt/τ).</item>
/// <item><c>MatureAt</c> (0.95): the MATURE reading of the stored maturity (1 − e⁻³).</item>
/// <item><c>StaffShareAtMaturity</c> (0.05): the share of its market (<c>AdultsPerUniversity</c>) a fully
///   mature university withdraws from labour — σ × A_u = 100 adults per instance, so a host never loses more
///   than σ of its adults while the market term holds.</item>
/// <item><c>AdultsPerUniversity</c> (2,000): the host market each university instance needs.</item>
/// <item><c>FoundingFoodSurplusRatio</c> (1.3) / <c>SustainingFoodSurplusRatio</c> (1.1): the content's
///   specialist (Artisans) emerge / recede thresholds, applied to founding and to maturation.</item>
/// <item><c>MaxResearchCostReduction</c> (1/3): the ceiling of one specialty's cost discount on its branch,
///   factor = 1 − this × (1 − e^(−X)).</item>
/// <item><c>SaturatedAt</c> (0.95): the SATURATED reading of the effect curve, S(X) ≥ this.</item>
/// <item><c>HealthType</c> ("medical_university"): the type whose maturity heals; data, not code.</item>
/// <item><c>MaxMortalityReduction</c> (0.10): the ceiling of the base-mortality reduction.</item>
/// <item><c>HealthDecayCostUnits</c> (25.0): e-fold of medical coverage over SettlementDistances travel cost.</item>
/// </list>
/// </summary>
public sealed record UniversitiesConfig(
    [property: JsonPropertyName("maturityTauYears"), JsonRequired] double MaturityTauYears,
    [property: JsonPropertyName("decayTauYears"), JsonRequired] double DecayTauYears,
    [property: JsonPropertyName("matureAt"), JsonRequired] double MatureAt,
    [property: JsonPropertyName("staffShareAtMaturity"), JsonRequired] double StaffShareAtMaturity,
    [property: JsonPropertyName("adultsPerUniversity"), JsonRequired] long AdultsPerUniversity,
    [property: JsonPropertyName("foundingFoodSurplusRatio"), JsonRequired] double FoundingFoodSurplusRatio,
    [property: JsonPropertyName("sustainingFoodSurplusRatio"), JsonRequired] double SustainingFoodSurplusRatio,
    [property: JsonPropertyName("maxResearchCostReduction"), JsonRequired] double MaxResearchCostReduction,
    [property: JsonPropertyName("saturatedAt"), JsonRequired] double SaturatedAt,
    [property: JsonPropertyName("healthType"), JsonRequired] string HealthType,
    [property: JsonPropertyName("maxMortalityReduction"), JsonRequired] double MaxMortalityReduction,
    [property: JsonPropertyName("healthDecayCostUnits"), JsonRequired] double HealthDecayCostUnits);

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
/// <c>TaxationMinAge</c> (H2, Director 2026-10-05 §7; docs/d049-taxation-and-revolt-model.md): THE AGE HALF of
/// the tax gate — the edict is operational only for a polity whose CURRENT Age (ages.json key, computed state,
/// law 4) is at least this. Optional: absent = no Age requirement (the pre-H2 gate, knowledge only). Evaluated
/// inside <see cref="State.Governance.CanLevyTax"/>, never by a caller.
/// </summary>
public sealed record GovernanceConfig(
    [property: JsonPropertyName("authorityDecayCostUnits"), JsonRequired] double AuthorityDecayCostUnits,
    [property: JsonPropertyName("taxExtractionResponseMax"), JsonRequired] double TaxExtractionResponseMax,
    [property: JsonPropertyName("taxationRequires"), JsonRequired] string TaxationRequires,
    [property: JsonPropertyName("ai"), JsonRequired] GovernanceAiConfig Ai,
    [property: JsonPropertyName("taxationMinAge")] int? TaxationMinAge = null);

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
    [property: JsonPropertyName("outputPerFarmerPerYear"), JsonRequired] double OutputPerFarmerPerYear,
    [property: JsonPropertyName("preCultivation")] PreCultivationConfig? PreCultivation = null);

/// <summary>
/// R2a (Director decision 8) — THE PRE-CULTIVATION FOOD YIELD, an ISOLATED CONTENT SWITCH. When
/// <see cref="Enabled"/>, a settlement whose knowledge (SettlementKnowledge.MaskOf) does not make the Farming
/// sector's researched identity (research.json sectorActivities: activity.farming, mode "replaces") eligible
/// harvests WILD FOOD at these rates instead of the CR-003 domesticated-cereal yield; the cultivated yield applies
/// once the farming capability exists for the settlement. Values are DERIVED from a forager reference class
/// stated in sim.json before any measurement (CR-003 §5.1). Disabled (or absent) = the shipped founding economy.
/// </summary>
public sealed record PreCultivationConfig(
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("yieldPerArableKm2PerYear"), JsonRequired] double YieldPerArableKm2PerYear,
    [property: JsonPropertyName("outputPerGathererPerYear"), JsonRequired] double OutputPerGathererPerYear);

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
    [property: JsonPropertyName("granaryYearsOfDemand"), JsonRequired] double GranaryYearsOfDemand,
    // INTRA-POLITY GRAIN RELIEF IN A SHORTAGE — D-053 §2 (the Director's 2026-10-13 instruction, item 2;
    // D-053 §5: it supersedes D-052 §11's "no relief-pooling substitute" for this item), a partial step toward
    // D-051 §6's ratified central stockpile, NOT that stockpile. true (shipped) = a settlement a polity controls
    // whose grain row is EMPTY after its own eating and whose requirement is still unmet draws on the grain its
    // polity's other settlements still hold, divided among the claimants by max-min water-filling on the
    // NOMINAL deficit u/R (FoodRelief; not the effective deficit — ConsumptionSystem's header states why) and
    // drawn nearest-first (ADR-032's order), BEFORE spoilage and the granary bound, which stay per settlement.
    // IDENTITY, not TUNE: the rule has no coefficient. false or ABSENT = the per-settlement store, byte for byte
    // — RETAINED as the golden-attribution control (TestConfigs.PreEmpireFoodAllocation; EmpireFoodAllocationTests).
    [property: JsonPropertyName("empireFoodAllocation")] bool EmpireFoodAllocation = false);

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
///   distance of the weather field, in IDEAL-GROUND travel-cost units applied to the
///   GEOGRAPHIC straight-line distance between site cells (km / KmPerCostUnit — M5 R2b:
///   weather does not follow roads, so the road-aware SettlementDistances are no longer
///   read; a terrain-less toy keeps its hand-written table). Settlements a short journey apart share weather
///   strongly; settlements across the map share it weakly. Frame: a weather
///   system spans a region, not a continent.
/// </summary>
public sealed record HarvestVarianceConfig(
    [property: JsonPropertyName("sigmaLogYield"), JsonRequired] double SigmaLogYield,
    [property: JsonPropertyName("correlationTimeYears"), JsonRequired] double CorrelationTimeYears,
    [property: JsonPropertyName("spatialSharedFraction"), JsonRequired] double SpatialSharedFraction,
    [property: JsonPropertyName("spatialRangeCostUnits"), JsonRequired] double SpatialRangeCostUnits,
    // M5 R2b: the distance the spatial kernel reads. Absent / "geographic" (shipped) = straight-line
    // site-to-site distance (GeographicDistance). "travelCost" = the pre-R2b road-aware SettlementDistances,
    // RETAINED ONLY as the attribution control (IntegratedPinAttributionTests strips the R2b layer with
    // it; the T4.21-4 content-twin precedent). Any other value fails the load.
    [property: JsonPropertyName("spatialDistance")] string? SpatialDistance = null,
    // D-051 ruling 1(b) (2026-10-10): true (shipped, and the default when absent) = a turn's harvest multiplier is
    // the TIME-AVERAGE of the yearly weather process over the turn's s = max(1, round(dt)) yearly sub-steps, and a
    // settlement with no weather row starts at a stationary draw. false = the pre-1(b) single point draw per turn
    // (x = 0 start), bit-exact, RETAINED ONLY as the golden-attribution control (TestConfigs.PreTimeAveragedHarvest;
    // IntegratedPinAttributionTests proves it returns the pre-1(b) pins byte for byte).
    [property: JsonPropertyName("timeAveragedHarvest")] bool TimeAveragedHarvest = true)
{
    /// <summary>True when the kernel reads the legacy road-aware travel costs (attribution control only).</summary>
    [JsonIgnore] public bool LegacyTravelCostKernel => string.Equals(SpatialDistance, "travelCost", StringComparison.Ordinal);
}

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
/// R2a — <c>Entity</c>: the research entity (research.json, kind activity) whose knowledge eligibility makes
/// trade BETWEEN settlements legal (State.TradeQuery.CanTrade; Director decision 1). Data, not code: no node id
/// appears in C#. Null = ungated (the pre-R2a behaviour, kept for content without it).
public sealed record TradeConfig(
    [property: JsonPropertyName("gapClosingFraction"), JsonRequired] double GapClosingFraction,
    [property: JsonPropertyName("costPerBulkCostUnit"), JsonRequired] double CostPerBulkCostUnit,
    [property: JsonPropertyName("entity")] string? Entity = null);

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
    public static SimConfig Load(Stream simJson, Stream needsJson, Stream goodsJson)
    {
        SimConfig cfg = ValidateNeedsAgainstRegistries(Load(simJson) with
        {
            Needs = NeedsConfigLoader.Load(needsJson),
            Goods = GoodsConfigLoader.Load(goodsJson),
        });
        ValidateGranariesAgainstContent(cfg);
        ValidateDiscoveryAgainstContent(cfg);
        return cfg;
    }

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
        ValidateTradeAgainstContent(cfg);
        ValidateProjectsAgainstContent(cfg.Goods, cfg.Research);
        ValidateInstitutionsAgainstContent(cfg);
        ValidateDiscoveryAgainstResearch(cfg);
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

        if (cfg.HarvestVariance is { SpatialDistance: { } spatial } && spatial != "geographic" && spatial != "travelCost")
            throw new SimConfigException(
                $"harvestVariance.spatialDistance must be \"geographic\" (shipped) or \"travelCost\" (the pre-R2b attribution control), got \"{spatial}\".");

        if (cfg.Farming is null) throw new SimConfigException("farming is missing.");
        RequireRate("farming.yieldPerArableKm2PerYear", cfg.Farming.YieldPerArableKm2PerYear);
        RequireRate("farming.outputPerFarmerPerYear", cfg.Farming.OutputPerFarmerPerYear);
        if (cfg.Farming.PreCultivation is { } pre)
        {
            RequireRate("farming.preCultivation.yieldPerArableKm2PerYear", pre.YieldPerArableKm2PerYear);
            RequireRate("farming.preCultivation.outputPerGathererPerYear", pre.OutputPerGathererPerYear);
        }

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
        if (cfg.Institutions is not null) ValidateInstitutions(cfg.Institutions);
        if (cfg.Granaries is not null) ValidateGranaries(cfg.Granaries);
        if (cfg.Construction is { AllocationRatios: true } construction && !(construction.Incremental && construction.EmpireStockpile))
            throw new SimConfigException(
                "construction.allocationRatios requires construction.incremental and construction.empireStockpile to be " +
                "true (D-054 WS2): the building/roads split caps a head's share of the Empire's purse, so it needs " +
                $"incremental construction (a whole-or-wait head capped below its cost could never build) and the Empire " +
                $"purse (the split is an Empire's), got incremental {(construction.Incremental ? "true" : "false")}, " +
                $"empireStockpile {(construction.EmpireStockpile ? "true" : "false")}.");
        if (cfg.Construction is { Reservations: true, Incremental: false })
            throw new SimConfigException(
                "construction.reservations requires construction.incremental to be true (D-055): a reservation sets aside " +
                "the remaining need of a part-built queue head, and the whole-or-wait rule (incremental false) has no " +
                "part-built head to hold for, got incremental false.");
        if (cfg.Construction is { ContinueWithinTurn: true, Incremental: false })
            throw new SimConfigException(
                "construction.continueWithinTurn requires construction.incremental to be true (D-058): it carries the " +
                "capacity a completing queue head leaves to the next entry in the same turn, and the whole-or-wait rule " +
                "(incremental false) has no partial capacity to carry, got incremental false.");
        if (cfg.Discovery is { } discovery) ValidateDiscovery(discovery);
        if (cfg.Fog is { } fog) ValidateFog(fog);
        if (cfg.Colonization is { } colonization
            && !(colonization.OrderPartyFraction > 0.0 && colonization.OrderPartyFraction < 1.0))
            throw new SimConfigException(
                "colonization.orderPartyFraction must be in (0,1) — a party is some of the source's people and " +
                $"never all of them (ADR-036), got {Inv(colonization.OrderPartyFraction)}.");
        if (cfg.Colonization?.AiFounding is { } aiFounding)
        {
            if (aiFounding.MinSourcePopulation < 1)
                throw new SimConfigException(
                    "colonization.aiFounding.minSourcePopulation must be >= 1 person (ADR-036 §8), got " +
                    $"{aiFounding.MinSourcePopulation.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            if (!(aiFounding.MinYearsBetweenFoundings >= 0.0) || !double.IsFinite(aiFounding.MinYearsBetweenFoundings))
                throw new SimConfigException(
                    "colonization.aiFounding.minYearsBetweenFoundings must be a finite number of years >= 0 " +
                    $"(ADR-036 §8), got {Inv(aiFounding.MinYearsBetweenFoundings)}.");
        }

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

    /// <summary>
    /// ADR-033 D3: every construction project's research entity (goods.json projects[].entity) exists in
    /// research.json and is something a construction queue can build — a building, an institution or
    /// infrastructure (a unit is recruited and an activity is labour, never queued). Checked where goods.json
    /// and research.json meet; a project with no entity has no knowledge requirement. Public so the canonical
    /// content can be checked from a config assembled without the four-file load.
    /// </summary>
    public static void ValidateProjectsAgainstContent(GoodsConfig? goods, Research.ResearchContent? research)
    {
        if (goods is null || research is null) return;
        // R1: a recipe's entity link names a research.json entity of kind recipe (CraftingQuery gates on it).
        foreach (RecipeEntry r in goods.Recipes)
        {
            if (r.Entity is null) continue;
            int e = research.EntityIndexOf(r.Entity);
            if (e < 0)
                throw new SimConfigException($"goods.json recipes: '{r.Name}' names entity '{r.Entity}', which research.json does not define.");
            if (research.Entities[e].Kind != Research.ResearchEntityKind.Recipe)
                throw new SimConfigException($"goods.json recipes: '{r.Name}' names entity '{r.Entity}' of kind {research.Entities[e].Kind} — " +
                                             "a recipe's knowledge entity is a recipe.");
        }
        if (goods.Projects is null) return;
        foreach (ConstructionProjectEntry p in goods.Projects)
        {
            if (p.Entity is null) continue;
            int e = research.EntityIndexOf(p.Entity);
            if (e < 0)
                throw new SimConfigException($"goods.json projects: '{p.Name}' names entity '{p.Entity}', which research.json does not define.");
            Research.ResearchEntityKind kind = research.Entities[e].Kind;
            if (kind is not (Research.ResearchEntityKind.Building or Research.ResearchEntityKind.Institution
                or Research.ResearchEntityKind.Infrastructure))
                throw new SimConfigException($"goods.json projects: '{p.Name}' names entity '{p.Entity}' of kind {kind} — a " +
                                             "construction project realizes a building, an institution or infrastructure.");
        }
        // ADR-033 D6: a project that FOUNDS an institution names an institution entity and a university type
        // research.json defines (one project per type is checked by the goods loader).
        foreach (ConstructionProjectEntry p in goods.Projects)
        {
            if (p.Founds is not { } founds) continue;
            int e = research.EntityIndexOf(founds.Entity);
            if (e < 0)
                throw new SimConfigException($"goods.json projects: '{p.Name}' founds entity '{founds.Entity}', which research.json does not define.");
            if (research.Entities[e].Kind != Research.ResearchEntityKind.Institution)
                throw new SimConfigException($"goods.json projects: '{p.Name}' founds entity '{founds.Entity}' of kind {research.Entities[e].Kind} — " +
                                             "what a project founds is an institution.");
            bool known = false;
            foreach (Research.UniversityType u in research.UniversityTypes)
                if (string.Equals(u.Id, founds.UniversityType, StringComparison.Ordinal)) { known = true; break; }
            if (!known)
                throw new SimConfigException($"goods.json projects: '{p.Name}' founds university type '{founds.UniversityType}', which " +
                                             "research.json universityTypes does not define.");
        }
    }

    /// <summary>ADR-033 D6: the university tuning — positive finite time constants, fractions in their
    /// ranges (a cost reduction strictly below 1 so every factor stays in (0, 1], ADR-029 §9), the
    /// sustaining surplus threshold not above the founding one (the hysteresis), a positive market and a
    /// positive finite diffusion e-fold.</summary>
    private static void ValidateInstitutions(InstitutionsConfig cfg)
    {
        UniversitiesConfig? u = cfg.Universities;
        if (u is null) throw new SimConfigException("institutions.universities is missing.");
        const string p = "institutions.universities.";
        if (!(u.MaturityTauYears > 0.0) || !double.IsFinite(u.MaturityTauYears))
            throw new SimConfigException($"{p}maturityTauYears must be a finite value > 0, got {Inv(u.MaturityTauYears)}.");
        if (!(u.DecayTauYears > 0.0) || !double.IsFinite(u.DecayTauYears))
            throw new SimConfigException($"{p}decayTauYears must be a finite value > 0, got {Inv(u.DecayTauYears)}.");
        if (!(u.MatureAt > 0.0 && u.MatureAt < 1.0))
            throw new SimConfigException($"{p}matureAt must be in (0,1) — maturity approaches 1 asymptotically, got {Inv(u.MatureAt)}.");
        if (!(u.StaffShareAtMaturity >= 0.0 && u.StaffShareAtMaturity <= 1.0))
            throw new SimConfigException($"{p}staffShareAtMaturity must be in [0,1], got {Inv(u.StaffShareAtMaturity)}.");
        if (u.AdultsPerUniversity <= 0)
            throw new SimConfigException($"{p}adultsPerUniversity must be > 0 (the host market of one university).");
        if (!(u.SustainingFoodSurplusRatio >= 0.0) || !double.IsFinite(u.SustainingFoodSurplusRatio))
            throw new SimConfigException($"{p}sustainingFoodSurplusRatio must be a finite value >= 0, got {Inv(u.SustainingFoodSurplusRatio)}.");
        if (!(u.FoundingFoodSurplusRatio >= u.SustainingFoodSurplusRatio) || !double.IsFinite(u.FoundingFoodSurplusRatio))
            throw new SimConfigException($"{p}foundingFoodSurplusRatio must be finite and >= sustainingFoodSurplusRatio (founding " +
                                         $"is the harder test), got {Inv(u.FoundingFoodSurplusRatio)}.");
        if (!(u.MaxResearchCostReduction >= 0.0 && u.MaxResearchCostReduction < 1.0))
            throw new SimConfigException($"{p}maxResearchCostReduction must be in [0,1) so every factor stays in (0,1] " +
                                         $"(ADR-029 §9), got {Inv(u.MaxResearchCostReduction)}.");
        if (!(u.SaturatedAt > 0.0 && u.SaturatedAt < 1.0))
            throw new SimConfigException($"{p}saturatedAt must be in (0,1), got {Inv(u.SaturatedAt)}.");
        if (string.IsNullOrWhiteSpace(u.HealthType))
            throw new SimConfigException($"{p}healthType must name a research.json university type id.");
        if (!(u.MaxMortalityReduction >= 0.0 && u.MaxMortalityReduction < 1.0))
            throw new SimConfigException($"{p}maxMortalityReduction must be in [0,1), got {Inv(u.MaxMortalityReduction)}.");
        if (!(u.HealthDecayCostUnits > 0.0) || !double.IsFinite(u.HealthDecayCostUnits))
            throw new SimConfigException($"{p}healthDecayCostUnits must be a finite value > 0, got {Inv(u.HealthDecayCostUnits)}.");
    }

    /// <summary>M5 item 5: the granary effects' tuning — a named project, both ceilings in [0, 1) (the famine factor
    /// 1 − max × S stays in (0, 1], so the effective deficit is never inverted and never raised; the farm factor
    /// 1 + max × S stays below 2), and positive finite e-fold scales.</summary>
    private static void ValidateGranaries(GranariesConfig g)
    {
        const string p = "granaries.";
        if (string.IsNullOrWhiteSpace(g.Project))
            throw new SimConfigException($"{p}project must name a goods.json construction project.");
        if (!(g.FarmYieldBonusMax >= 0.0 && g.FarmYieldBonusMax < 1.0))
            throw new SimConfigException($"{p}farmYieldBonusMax must be in [0,1) — the share of the delivered harvest a " +
                                         $"granary can save from post-harvest loss, got {Inv(g.FarmYieldBonusMax)}.");
        if (!(g.FarmYieldScale > 0.0) || !double.IsFinite(g.FarmYieldScale))
            throw new SimConfigException($"{p}farmYieldScale must be a finite value > 0, got {Inv(g.FarmYieldScale)}.");
        if (!(g.FamineReductionMax >= 0.0 && g.FamineReductionMax < 1.0))
            throw new SimConfigException($"{p}famineReductionMax must be in [0,1) so the famine factor stays in (0,1] and " +
                                         $"the effective deficit is never inverted, got {Inv(g.FamineReductionMax)}.");
        if (!(g.FamineReductionScale > 0.0) || !double.IsFinite(g.FamineReductionScale))
            throw new SimConfigException($"{p}famineReductionScale must be a finite value > 0, got {Inv(g.FamineReductionScale)}.");
    }

    /// <summary>M6 Track EX (ADR-044): the discovery section's ranges (the goods names are checked against goods.json by
    /// <see cref="ValidateDiscoveryAgainstContent"/>).</summary>
    private static void ValidateDiscovery(DiscoveryConfig d)
    {
        if (d.ConcealableGoods is null)
            throw new SimConfigException("discovery.concealableGoods is missing — name the goods.json deposit goods that may be concealed.");
        if (!(d.ConcealedShare >= 0.0 && d.ConcealedShare <= 1.0))
            throw new SimConfigException($"discovery.concealedShare must be in [0,1] (a share of a settlement's eligible deposits), got {Inv(d.ConcealedShare)}.");
        ValidateBill(d.ProspectCost, "discovery.prospectCost");
        if (d.ProspectCost.Length == 0)
            throw new SimConfigException("discovery.prospectCost is empty — a survey party carries provisions and materials.");
        if ((d.AssayCost is null) != (d.AssayRequires is null))
            throw new SimConfigException("discovery.assayCost and discovery.assayRequires come together: an assay is offered " +
                                         "only behind its technology, and a technology gate needs a bill.");
        if (d.AssayCost is { } assay) ValidateBill(assay, "discovery.assayCost");
        if (d.AssayRequires is { } req && string.IsNullOrWhiteSpace(req))
            throw new SimConfigException("discovery.assayRequires is empty — name a research.json requirement or omit the assay.");
    }

    private static void ValidateBill(DiscoveryCostItem[]? bill, string where)
    {
        if (bill is null) throw new SimConfigException($"{where} is missing.");
        for (int i = 0; i < bill.Length; i++)
        {
            if (bill[i] is null || string.IsNullOrWhiteSpace(bill[i].Good))
                throw new SimConfigException($"{where}[{i}] names no good.");
            if (bill[i].Qty < 0)
                throw new SimConfigException($"{where}[{i}] ({bill[i].Good}) must be a whole quantity >= 0, got " +
                    bill[i].Qty.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            for (int j = 0; j < i; j++)
                if (string.Equals(bill[j].Good, bill[i].Good, StringComparison.Ordinal))
                    throw new SimConfigException($"{where} names '{bill[i].Good}' twice.");
        }
    }

    /// <summary>M6 Track EX (ADR-044): the fog section's ranges.</summary>
    private static void ValidateFog(FogConfig f)
    {
        if (f.SeedRadiusNodes < 0 || f.FormationSightNodes < 0)
            throw new SimConfigException("fog.seedRadiusNodes and fog.formationSightNodes must be >= 0 lattice nodes.");
        if (f.ExploreStepNodes < 1)
            throw new SimConfigException("fog.exploreStepNodes must be >= 1 lattice node (an Explore that reveals nothing is not an action).");
        if (f.ExploreMaxRadiusNodes < f.SeedRadiusNodes || f.ExploreMaxRadiusNodes > 128)
            throw new SimConfigException("fog.exploreMaxRadiusNodes must be in [fog.seedRadiusNodes, 128] lattice nodes.");
        if (f.ExploreGrain < 0)
            throw new SimConfigException("fog.exploreGrain must be a whole number of grain >= 0.");
    }

    /// <summary>M6 Track EX (ADR-044): every discovery.concealableGoods name is a goods.json DEPOSIT good (it has a
    /// depositChannel); the prospect and explore provisions are the numeraire grain — checked
    /// where sim.json and goods.json meet. Public so a config assembled without the canonical load can be checked.</summary>
    public static void ValidateDiscoveryAgainstContent(SimConfig cfg)
    {
        if (cfg.Discovery is not { } d || cfg.Goods is not { } goods) return;
        foreach (string name in d.ConcealableGoods)
        {
            int id = goods.IdOf(name);
            if (id < 0 || goods.ById(id).DepositChannel is null)
                throw new SimConfigException($"discovery.concealableGoods names '{name}', which is not a goods.json deposit good (one with a depositChannel).");
        }
        foreach (DiscoveryCostItem item in d.ProspectCost)
            if (goods.IdOf(item.Good) < 0)
                throw new SimConfigException($"discovery.prospectCost names '{item.Good}', which is not a goods.json good.");
        foreach (DiscoveryCostItem item in d.AssayCost ?? [])
            if (goods.IdOf(item.Good) < 0)
                throw new SimConfigException($"discovery.assayCost names '{item.Good}', which is not a goods.json good.");
        // ADR-046 — NO DEADLOCK: the basic survey may not cost what only a survey can find — a concealable good, or a
        // good a recipe makes (directly or through other recipes) from one.
        bool[] concealedLine = ConcealedLine(goods, d.ConcealableGoods);
        foreach (DiscoveryCostItem item in d.ProspectCost)
            if (item.Qty > 0 && concealedLine[goods.IdOf(item.Good)])
                throw new SimConfigException($"discovery.prospectCost names '{item.Good}', which is (or is made from) a concealable " +
                    "good — the basic survey would wait on the deposits it exists to find. Put it in discovery.assayCost.");
    }

    /// <summary>ADR-046: the goods that are concealable or made, through any chain of goods.json recipes, from one —
    /// indexed by good id (fixed-point over the recipes in file order).</summary>
    public static bool[] ConcealedLine(GoodsConfig goods, string[] concealable)
    {
        int max = 0;
        foreach (GoodEntry g in goods.Goods) max = Math.Max(max, g.Id);
        var line = new bool[max + 1];
        foreach (string name in concealable) { int id = goods.IdOf(name); if (id >= 0) line[id] = true; }
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (RecipeEntry r in goods.Recipes)
            {
                int output = goods.IdOf(r.Output.Good);
                if (output < 0 || line[output]) continue;
                foreach (RecipeInput input in r.Inputs)
                {
                    int i = goods.IdOf(input.Good);
                    if (i >= 0 && line[i]) { line[output] = true; changed = true; break; }
                }
            }
        }
        return line;
    }

    /// <summary>ADR-046: discovery.assayRequires parses against research.json.</summary>
    private static void ValidateDiscoveryAgainstResearch(SimConfig cfg)
    {
        if (cfg.Discovery?.AssayRequires is not { } req || cfg.Research is not { } research) return;
        Research.ResearchContentLoader.ParseRequirement(research, req, "sim.json discovery.assayRequires");
    }

    /// <summary>M5 item 5: the granaries section names a goods.json construction project — checked where sim.json and
    /// goods.json meet. Public so a config assembled without the canonical load can be checked.</summary>
    public static void ValidateGranariesAgainstContent(SimConfig cfg)
    {
        if (cfg.Granaries is not { } g || cfg.Goods is null) return;
        foreach (ConstructionProjectEntry p in cfg.Goods.Projects ?? [])
            if (string.Equals(p.Name, g.Project, StringComparison.Ordinal)) return;
        throw new SimConfigException($"granaries.project '{g.Project}' is not a goods.json projects name.");
    }

    /// <summary>ADR-033 D6: the health type names a research.json university type.</summary>
    private static void ValidateInstitutionsAgainstContent(SimConfig cfg)
    {
        if (cfg.Institutions is null || cfg.Research is null) return;
        string health = cfg.Institutions.Universities.HealthType;
        foreach (Research.UniversityType u in cfg.Research.UniversityTypes)
            if (string.Equals(u.Id, health, StringComparison.Ordinal)) return;
        throw new SimConfigException($"institutions.universities.healthType '{health}' is not a research.json universityTypes id.");
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
        if (g.TaxationMinAge is { } minAge && !(minAge >= 1 && minAge <= Ages.AgeContent.AgeCount))
            throw new SimConfigException(
                $"governance.taxationMinAge must be an ages.json key in [1,{Ages.AgeContent.AgeCount}] (the Age from which the tax edict is operational), got {minAge}.");
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
    private static void ValidateTradeAgainstContent(SimConfig cfg)
    {
        if (cfg.Trade.Entity is not { } entity || cfg.Research is null) return;
        int e = cfg.Research.EntityIndexOf(entity);
        if (e < 0)
            throw new SimConfigException($"sim.json trade.entity '{entity}' is not a research.json entity — trade would never become legal.");
        if (cfg.Research.Entities[e].Kind != Systems.Research.ResearchEntityKind.Activity)
            throw new SimConfigException($"sim.json trade.entity '{entity}' is a {cfg.Research.Entities[e].Kind}, not an activity.");
    }

    private static void ValidateGovernanceAgainstContent(SimConfig cfg)
    {
        if (cfg.Governance is null || cfg.Research is null) return;
        ClassMobility.Predicate requirement;
        try
        {
            requirement = Systems.Research.ResearchContentLoader.ParseRequirement(
                cfg.Research, cfg.Governance.TaxationRequires, "governance.taxationRequires");
        }
        catch (Systems.Research.ResearchContentException e)
        {
            throw new SimConfigException($"sim.json {e.Message}", e);
        }
        // F1 (2026-10-05, d049 §15): the Age half of the tax gate (sim.json governance.taxationMinAge) and the Age the
        // gate's research nodes are tagged with (research.json "age") are two statements of one fact (Director §7:
        // Taxation is an A3 capability). They must not disagree in the direction that matters: the edict can never be
        // declared operational in an Age EARLIER than the earliest Age at which its knowledge requirement can be met.
        if (cfg.Governance.TaxationMinAge is { } minAge)
        {
            int earliest = EarliestRequirementAge(cfg.Research, requirement);
            if (minAge < earliest)
                throw new SimConfigException(
                    $"sim.json governance.taxationMinAge = {minAge} is earlier than the Age of the research its gate needs "
                    + $"(governance.taxationRequires '{cfg.Governance.TaxationRequires}' is satisfiable from A{earliest} by research.json's "
                    + "node Age tags); the tax edict cannot be operational before the knowledge that makes it exists.");
        }
    }

    /// <summary>F1: the earliest Age (1-based) at which <paramref name="requirement"/> can hold if every research node
    /// tagged with that Age or earlier is known — AND takes the latest of its sides, OR the earliest (State.ResearchQuery.RequirementMet
    /// over the Age tags). A node whose tag is not "A&lt;n&gt;" counts as Age 1. Unsatisfiable → int.MaxValue.</summary>
    public static int EarliestRequirementAge(Systems.Research.ResearchContent research, ClassMobility.Predicate requirement)
    {
        ArgumentNullException.ThrowIfNull(research);
        ArgumentNullException.ThrowIfNull(requirement);
        var known = new bool[research.Nodes.Count];
        for (int age = 1; age <= Ages.AgeContent.AgeCount; age++)
        {
            for (int n = 0; n < known.Length; n++) known[n] = NodeAge(research.Nodes[n].Age) <= age;
            // The existing knowledge evaluator (node atoms AND entity atoms), as the live gate evaluates it.
            if (State.ResearchQuery.RequirementMet(research, requirement, known)) return age;
        }
        return int.MaxValue;
    }

    private static int NodeAge(string tag) =>
        tag.Length > 1 && tag[0] == 'A'
        && int.TryParse(tag.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
            ? n : 1;

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
