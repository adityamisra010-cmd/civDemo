# ADR-047 — Knowledge effects: completed research changes gameplay through the owning systems

**Status: PROPOSED for the M6 tranche-2 build (engine track `t2-effects`; awaiting the Director's review).** Labels:
MEASURED · DERIVED · CHOSEN (TUNE). No new state, no new table, no schema bump, no order change.

Authority: the Director's direction of 2026-10-24 ("every node changes gameplay"), recorded as the working contract
`docs/m6-plan/knowledge-redesign/tranche2-contract.md` §1 and §1.1; governance CR-024 / D-063 (contract §9).

## 1. Context

Since ADR-029 a completed research node grants ELIGIBILITY only: an entity whose knowledge requirement it satisfies may be
built, run or ordered. Most nodes unlock nothing a live system realizes, so most of the graph changed nothing. The
Director's direction asks that every node change gameplay — without turning research into a modifier layer.

## 2. Decision

1. **Content declares, systems realize.** A node may declare `effects.realized`: a list of operand effects
   `{ "operand": id, "delta": d }`. `effects.immediate` and the loader's refusal of a non-empty immediate list are
   unchanged (contract §9). The loader refuses an unregistered operand id, a delta outside (0, 0.5], a non-finite delta
   (JSON `NaN`/`Infinity` literals are read so the refusal names them) and an operand listed twice in one node. Absent =
   the empty list.
2. **The operand registry** (`Sim.Core/State/KnowledgeOperands.cs`) is the contract §1.1 table, row for row (47 rows:
   id, direction, cap on Σ, integer flag, reader, meaning). `KnowledgeEffectsTests.TheRegistry_IsTheContractTable_RowForRow`
   parses the contract document and compares. Every operand is a parameter of a live equation in its owning system.
3. **The rule** (`Sim.Core/State/KnowledgeEffects.cs`). For a holder with completed set S: Σ = Σ delta over S (+ the
   repeatable-levels seam `LevelContribution`, 0 until the levels track lands); clamp Σ to [0, cap]; `higher` reads
   base × (1 + Σ), `lower` base × (1 − Σ); integer operands round down; Σ = 0 returns the base double unchanged. Σ is
   summed in ascending node index, so it is a function of the completed SET, never of row order. Result bounds where the
   contract states one (gapClosingFraction ≤ 0.95, maxResearchCostReduction ≤ 0.75, orderPartyFraction ≤ 0.35) and two
   equation domains (targetShareCap ≤ 1, adaptationAbsorbableShortfall ≤ 0.95); a bound never lowers a base.
4. **Holders.** A polity reads its own rows; a settlement reads `SettlementKnowledge.HolderOf` (its controller, else its
   own local key). Effects never cross holders.
5. **Cost.** Systems take one `KnowledgeEffectsSnapshot` per step (one pass over the completed rows; O(log holders) per
   lookup; arrays and a sorted composite integer key, no dictionary, no LINQ). With the switch off, with no content, or with
   no declared effect, the snapshot is the shared inert instance: every lookup returns its base without resolving a holder.
6. **The switch.** research.json `tuning.knowledgeEffects` (absent = false). Off: every operand is its base exactly, the
   world byte-identical to the pre-ADR-047 build. Attribution twin: `TestConfigs.PreKnowledgeEffects` (and every older
   research twin strips the key — the R4 rule, through `UndoAiSupporting`).
7. **Shipped content.** The switch is ON and NO node declares a realized effect, so every operand is its base and every
   golden is unchanged (the full suites; `KnowledgeEffectsTests.SwitchOff_IsByteIdentical_OnALongAiRun` pins the 300-turn
   AI world: shipped = PreKnowledgeEffects = a content declaring effects with the switch off). Content tracks add effects.

## 3. How D-044 R14 is kept

ResearchSystem still only records completion. Nothing is built, granted or modified at completion time. Each owning
system reads its own operand from its own equation through the State query (law 6: State, never another system), on
PREV, in its own step — exactly as it already reads recipe eligibility. Completion declares a capability; the owning system
realizes it.

## 4. How law 2 is kept

Every operand is a coefficient INSIDE a named resolution equation of its system (the farming Leontief, the spoilage
exponential, the housing build caps, the migration damping kernel, the RP curve …), never a free-floating buff on an
output. Each is bounded: Σ by its cap (lower operands' caps < 1, so no rate reaches zero), and by the result bounds above.

## 5. Read sites (owning system ← operand)

| Operand | Read site |
|---|---|
| farming.yieldPerArableKm2PerYear, farming.outputPerFarmerPerYear | ProductionSystem.Farm — the CULTIVATED rates (the pre-cultivation forager rates are not farming knowledge's) |
| production.outputPerHerderPerYear / outputPerExtractorPerYear | ProductionSystem.FromDeposits (herding+fishing / extraction sectors; fish is produced by the herding sector's rate in the engine) |
| production.toolYieldBonusMax / toolWearPerEquippedFarmerPerYear | ProductionSystem.Farm (tool factor; tool wear sink) |
| consumption.grainSpoilagePerYear / granaryYearsOfDemand | ConsumptionSystem.BoundStore |
| harvestVariance.sigmaLogYield | HarvestWeatherSystem — the shared weather process is unchanged; the holder's harvest follows it with sensitivity σᵢ/σ: exp(sᵢ·x − σᵢ²/2), still mean-one |
| granaries.famineReductionMax / farmYieldBonusMax | GranaryEffects world seams (the binding carries the research content) |
| foodState.adaptationAbsorbableShortfall | FoodState.Of (SEVERE threshold) and the world overloads of FoodState.EffectiveDeficit |
| housing.* (5) | HousingSystem; personsPerDwelling also NeedsGrievanceSystem (Shelter), SettlementHappiness.HousingSufficiency, the observer and causal chain. WorldFounding keeps the base (no holder at founding) |
| pathBuild.laborPerAdultPerYear / buildCostMultiplier | PathBuildSystem (accrual; segment cost) |
| pathBuild.dirtPathSpeedFactor | PathBuildSystem — stored as a COST factor, so the read divides: cost factor / (1 + Σ) (the "higher = faster" of the contract) |
| transport.riverCostFactor | PathBuildSystem: a builder whose holder's factor differs routes and prices its segments on its own lattice (one per distinct factor per step). NOT per holder: CatchmentSystem's partition (one multi-source Dijkstra over all settlements), settlement siting (ColonizationSystem / FoundingQuery frontier) and WorldFounding keep the base — there is no single holder for a shared partition |
| roads.baselineKmPerDay | TransportQuery.EstimateFreight(…, research, shipper) — the hook; no system consumes freight time yet (ADR-032 §7) |
| roads.maxRouteKm | RoadDevelopmentQuery.Routes (the polity's candidate routes) |
| catchment.hinterlandRadiusKm | CatchmentSystem per-settlement budget (and SiteAssessment's replica); the staleness revision adds the count of completions bearing this effect, so such a completion is a recompute event (0 when no node declares it) |
| trade.gapClosingFraction / costPerBulkCostUnit | TradeArbitrageSystem — a pair reads the mean of its two ends' values (the shared value exactly when they agree) |
| price.stockReleaseRatePerYear | PriceSystem and TradeArbitrageSystem's replica of the market scale |
| demographics.* (3) | DemographicsSystem (every cohort's base rate; the starvation maximum; the child multiplier) |
| institutions.universities.* (3) | InstitutionEffects.MortalityMultiplier (settlement holder); InstitutionsSystem (owner's τ; owner's cost cut) and the two display readers |
| governance.authorityDecayCostUnits / taxExtractionResponseMax | Governance.AdministrativeReach (GovernanceSystem passes the content) / Governance.ExtractionMultiplier |
| migration.baseRatePerYear / dampingDecayCostUnits | MigrationSystem.Plan and Step — the SOURCE holder's (gap channel; damping kernel). The famine-flight hazard keeps the base rate |
| mobility.promoteRatePerYear / targetShareCap | ClassMobilitySystem.MoveAdults |
| colonization.orderPartyFraction | FoundingQuery.PartyFraction(world, cfg, source) — every Departure caller |
| fog.exploreStepNodes / exploreMaxRadiusNodes / formationSightNodes | DiscoveryQuery (NextRadius, the AtCap arm, formation sight in Known/Visible); PolityKnowledge passes the content |
| research.researchPointsCoefficient / civicsPointsCoefficient | ResearchQuery.ResearchPointPool, CityStateResearchPoints, CivicsPointsMilli |
| society.grievanceRelief | NeedsGrievanceSystem — the ACCUMULATION term of both stocks (needs and levy): accrual × (1 − Σ); GrievanceExplanation recomputes the same |
| society.unrestOutputDrag | Unrest.OutputFactor — the withheld share × (1 − Σ) |

## 6. Consequences

- Every operand has a teeth test (`KnowledgeEffectsTeethTests`, one theory row per registry row): on a dev-world fixture
  the owning read site moves in the stated direction for the holder, equals the base without the completion, and — where
  no coupling crosses empires within the step — leaves the rival exactly unchanged.
- Performance: shipped content builds no snapshot work (inert). Bench numbers are in the packet report.
- The levels track implements `KnowledgeEffects.LevelContribution` / `LevelsPresent` (one seam) and extends
  `CompletionsWith` if a level can carry the hinterland operand.

## 7. What is not live

Units and most institutions are not realized by any system yet (M7/M8) and are not effects (contract §1). Live entity
unlocks remain the kinds `building`, `infrastructure`, `recipe`, `activity`, plus `inst.university`, the tax edict and
the assay. Known limits (queue.md): a change of controller alone does not re-trigger a catchment recompute (the captured
settlement takes its new holder's radius at the next recompute event); the shared-partition river factor stays the base.

## Amendment note (2026-10-26): the CR-025 ruling

Recorded from the Director's ruling of 2026-10-26 on CR-025 (D-063 R1 amendment, `docs/d063-tranche2-director-direction.md`).
The sections above are not edited; where they differ, this note governs.

1. **§2.2 holds as a loader rule now.** "Every operand is a parameter of a live equation" was false for one row:
   `roads.baselineKmPerDay`'s reader (`TransportQuery.EstimateFreight`) has no caller (ADR-032 §7). The registry marks such
   a row with a `NotConsumed` reason (`KnowledgeOperandInfo.ConsumedInPlay` false; today that one row only), and the
   loader REFUSES a realized delta (§2.1) or a repeatable level effect on it. The row, its cap, its reader and the
   contract table are unchanged; the §5 read-site row for it describes a query, not a consumer.
2. **Future effects (new, optional).** A node may declare `effects.future`: `{ "milestone": "M7".."M11" | "unscheduled",
   "system": text, "operand": registered id not consumed in play | null, "description": text }`, all four members
   required, any other member (a `delta` above all) refused, one entry per system per node. They load into
   `ResearchNode.FutureEffects`. They are never in the operand index, Σ, the snapshot or any system; the research card
   lists each as "Future (M7 …): …, not simulated in this build" and never counts it as simulated.
3. **Teeth.** The per-operand teeth cover the operands consumed in play only; the former `roads.baselineKmPerDay` row
   measured the uncalled query (teeth without aim, ADR-015 §7.2) and is removed. The guard's own tests are
   `UnconsumedOperandTests` and `FutureEffectsTests`.

No new state, table, order or schema bump. Content changes (17 nodes) are in CR-025 §7.
