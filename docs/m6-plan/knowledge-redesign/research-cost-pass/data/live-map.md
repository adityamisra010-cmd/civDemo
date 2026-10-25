# Research live map: live effects vs placeholders

Tree read: `/home/user/m5w/dev` at `b1dce0f` on branch `claude/new-session-yavghm` (LOCAL; the branch is 4 commits ahead of its remote). I only read files. Nothing was built or run. The machine-readable version is `live-map.json` in this folder.

**How "live" is decided.** An operand counts as live when a system in `pipeline.json` reads it, under the switches as shipped, and the value it reads can change world state. Shipped research.json switches: `tuning.knowledgeEffects` true, `repeatableLevels` true, `civicsPoints.enabled` true, `ageSweep` true. Shipped sim.json switches: `granaries.enabled`, `fog.enabled`, `discovery.enabled`, `farming.preCultivation.enabled` and `colonization.aiFounding.enabled` are all true. Readers in Observability, InfoQuery or AvailableActionsQuery do not count. An entity counts as live when a system builds, founds or crafts it, or uses its knowledge eligibility to change state.

## Headline counts

- **Operands live: 45/47.** Two are not live:
  - `roads.baselineKmPerDay`. Its only reader is `TransportQuery.EstimateFreight(shipper)`, and that method has no caller outside the tests. Even so, 17 nodes declare deltas on it (summing to 0.59).
  - `pathBuild.dirtPathSpeedFactor`. The reader at PathBuildSystem.cs:251 is wired and runs, but no node and no repeatable level declares a delta on it, so it always reads its base value.
- **Live entity ids: 15/135** (building 3/43, inst 1/28, infra 5/25, unit 0/23, activity 2/9, recipe 4/4, project 0/3). Kinds with no realizing system at all: `unit.`, `project.`.
- **Nodes: 1005.** 967 have at least one live operand effect. 18 have a live unlock. 13 are repeatable with levels.
- **Nodes with no live value: 33** when only live operands, live unlocks and the two sim.json order gates count. The figure drops to **30** when ages.json milestone facts and research-stage atoms also count as live consequences; those two extra gates are listed in `orderGates` too. Under that rule, `electricity_generation`, `internal_combustion` and `radio` stop counting as having no value, because each is an Age milestone fact.
- **futureSystem:** M8 government adoption 52 · M7 battle layer 31 · "M8 government layer (node desc)" 20 · null 902.

## Documented as effective but not actually read (or read more narrowly than documented)

1. **`roads.baselineKmPerDay`: not read by any system.** The registry gives TransportQuery as its reader, and contract §1.1 says it means "faster overland travel". 17 nodes declare it: horse_domestication, wheel_spoked, saddle, road_paved, bridge_stone, horseshoe, wheellock, internal_combustion, aluminium, motor_road, sprung_coach, integrated_alliance_command, road_houses, pilgrimage, bridge_trusts, turnpike_trusts, road_traffic_regulation. For 15 of them it is the only realized effect; the two exceptions are road_paved and wheellock. ADR-047 line 71 and queue.md line 1616 already admit that "no system consumes freight time yet", but the content still declares the effect.
2. **`pathBuild.dirtPathSpeedFactor`: no content at all.** The registry row and its reader exist, but there are 0 node deltas and 0 levels on it.
3. **`transport.riverCostFactor`: read much more narrowly than documented.** The registry and contract give the reader as "transport readers" and the meaning as "cheaper river haulage". In fact only PathBuildSystem.cs:239 reads it, and only to choose the lattice the path builder prices segments on. Pathfinder, trade and migration distances, the catchment partition and settlement siting all keep the base value. ADR-047 line 70 acknowledges this. 7 nodes declare it.
4. **`fog.formationSightNodes`: live but nearly dormant.** Formations are founded at capitals and stay there until M7 adds movement. The base is 1 node, so even the maximum gain to 3 nodes (about 48 km) rarely reaches another settlement. The repeatable frontier_military uses it as an explicit "PROXY ... before M7".
5. **`activity.herding`, `activity.logging`, `activity.mining`: label only.** research.json `sectorActivities` describes them as "replaces" (herding) or "joins" (logging, mining), and the unlock audit (`docs/research-gameplay-unlock-audit.md`) counts activities as class B, "research-gated gameplay capability". In code they only change the labour-sector label (`LabourActivities` identities; "production unchanged", ADR-033 D1). The one activity with a production effect is `activity.farming`, through `farming.preCultivation`. `activity.fishing`, `caravans`, `coastal_shipping` and `ocean_shipping` have no consumer at all.
6. **Civic descriptions claim an institution the code never establishes.** Six civics say "the institution itself is established by its owning system once this civic is complete": law_code (inst.law_code), legal_code_roman, census, coined_wage, patent and joint_stock. No system establishes any `inst.*` except inst.university. inst.library and inst.legal_code_roman matter only as requirement atoms of inst.university.
7. **`unit.*`: requirement is never checked.** unit-families.json `realizedBy` names the unit entities, but its own docs say it "gates nothing". Formations are founded at worldgen and modernized by Age transition, so research never touches them. The 31 class-F nodes appear in the audit as "load-validated".
8. **Correction to `government-packages.json`: no wrong claims found.** Every `builtIn` row marked `live:true` names an operand that is live here: governance.authorityDecayCostUnits, governance.taxExtractionResponseMax, mobility.promoteRatePerYear, trade.*, migration.*, granaries.famineReductionMax and price.stockReleaseRatePerYear.

## Operands (47)

S is the clamped sum of the deltas. "content" gives the number of nodes that declare the operand, the sum of their deltas, and any repeatable level that also feeds it. Every row is additionally gated by `tuning.knowledgeEffects` (true).

| operand | live | reader | extra gate | consequence |
|---|---|---|---|---|
| `farming.yieldPerArableKm2PerYear` | yes | Systems/Production/ProductionSystem.cs:288 | — | cultivated grain per arable km2 x(1+S); cap S<=3.0; content: 45 nodes sum delta 2.23 + levels(frontier_crops) |
| `farming.outputPerFarmerPerYear` | yes | Systems/Production/ProductionSystem.cs:289 | — | per-farmer labour ceiling on grain x(1+S); cap S<=3.0; content: 26 nodes sum delta 1.34 |
| `production.outputPerHerderPerYear` | yes | Systems/Production/ProductionSystem.cs:226 | — | herding/fishing output per worker x(1+S); cap S<=2.0; content: 20 nodes sum delta 0.79 |
| `production.outputPerExtractorPerYear` | yes | Systems/Production/ProductionSystem.cs:232 | — | logging/mining/quarry/fishing output per extractor x(1+S); cap S<=3.0; content: 42 nodes sum delta 1.43 + levels(frontier_energy) |
| `production.toolYieldBonusMax` | yes | Systems/Production/ProductionSystem.cs:280 | — | tool yield bonus x(1+S), scaled by equipRatio (needs tools stock); cap S<=1.0; content: 17 nodes sum delta 0.62 |
| `production.toolWearPerEquippedFarmerPerYear` | yes | Systems/Production/ProductionSystem.cs:362 | — | tool wear x(1-S); cap S<=0.8; content: 17 nodes sum delta 0.64 |
| `consumption.grainSpoilagePerYear` | yes | Systems/Consumption/ConsumptionSystem.cs:549 | — | stored-grain spoilage rate x(1-S); cap S<=0.85; content: 12 nodes sum delta 0.51 |
| `consumption.granaryYearsOfDemand` | yes | Systems/Consumption/ConsumptionSystem.cs:550 | — | grain store bound (years of demand) x(1+S); cap S<=1.0; content: 9 nodes sum delta 0.42 |
| `harvestVariance.sigmaLogYield` | yes | Systems/Harvest/HarvestWeatherSystem.cs:199 | — | harvest log-yield sigma x(1-S) (steadier harvests); cap S<=0.6; content: 24 nodes sum delta 0.562 + levels(frontier_climate) |
| `granaries.famineReductionMax` | yes | State/GranaryEffects.cs:155 (via FoodState.EffectiveDeficit <- DemographicsSystem.cs:270) | sim.json granaries.enabled (true); needs >=1 standing granary project | max famine-intensity reduction from granaries x(1+S); cap S<=1.0; content: 8 nodes sum delta 0.28 |
| `granaries.farmYieldBonusMax` | yes | State/GranaryEffects.cs:136 (via ProductionSystem.cs:343) | sim.json granaries.enabled (true); needs >=1 standing granary project | granary farm-yield bonus x(1+S) (base 0.05); cap S<=1.0; content: 2 nodes sum delta 0.07 |
| `foodState.adaptationAbsorbableShortfall` | yes | State/FoodState.cs:106 (via DemographicsSystem.cs:269-270, ConsumptionSystem.cs:399) | — | absorbable shortfall a x(1+S), result <=0.95: raises SEVERE threshold and dead zone of starvation deficit; cap S<=1.0; content: 33 nodes sum delta 0.9 |
| `housing.buildLaborAdultYearsPerDwelling` | yes | Systems/Housing/HousingSystem.cs:101 | — | labour per dwelling x(1-S); cap S<=0.8; content: 7 nodes sum delta 0.34 |
| `housing.buildTimberPerDwelling` | yes | Systems/Housing/HousingSystem.cs:102 | — | timber per dwelling x(1-S); cap S<=0.7; content: 7 nodes sum delta 0.26 + levels(frontier_materials) |
| `housing.upkeepTimberPerDwellingYear` | yes | Systems/Housing/HousingSystem.cs:98 | — | timber upkeep per dwelling-year x(1-S); cap S<=0.8; content: 4 nodes sum delta 0.13 |
| `housing.tauYears` | yes | Systems/Housing/HousingSystem.cs:99 | — | dwelling lifetime tau x(1+S); cap S<=1.5; content: 17 nodes sum delta 0.56 |
| `housing.personsPerDwelling` | yes | Systems/Housing/HousingSystem.cs:100 (+SettlementHappiness.cs:147, NeedsGrievanceSystem.cs:168) | — | persons per dwelling x(1+S): fewer dwellings needed, housing sufficiency/shelter need; cap S<=0.5; content: 10 nodes sum delta 0.39 |
| `pathBuild.laborPerAdultPerYear` | yes | Systems/PathBuild/PathBuildSystem.cs:188 | — | path-build labour accrual per builder-year x(1+S); cap S<=1.0; content: 9 nodes sum delta 0.27 |
| `pathBuild.buildCostMultiplier` | yes | Systems/PathBuild/PathBuildSystem.cs:250 | — | path segment build cost x(1-S); cap S<=0.7; content: 9 nodes sum delta 0.3 |
| `pathBuild.dirtPathSpeedFactor` | **NO** | Systems/PathBuild/PathBuildSystem.cs:251 | — | NOT LIVE: reader wired (dirt-path cost factor / (1+S)) but no node and no repeatable level declares a delta on this operand -> always base; cap S<=0.8; content: 0 nodes sum delta 0 |
| `transport.riverCostFactor` | yes | Systems/PathBuild/PathBuildSystem.cs:239 | — | river cost factor x(1-S) ONLY for the path builder's segment lattice; Pathfinder/trade/migration distances keep the base factor (registry says 'transport readers' / 'cheaper river haulage'); cap S<=0.8; content: 7 nodes sum delta 0.36 |
| `roads.baselineKmPerDay` | **NO** | State/TransportQuery.cs:213 | — | NOT LIVE: only TransportQuery.EstimateFreight(shipper) applies it, and EstimateFreight has no caller in Sim.Core/Sim.Cli/Sim.Ui (tests only) -> no state change; cap S<=3.0; content: 17 nodes sum delta 0.59 |
| `roads.maxRouteKm` | yes | State/RoadDevelopmentQuery.cs:162 (via RoadDevelopmentPolicy.cs:36/40 -> AiOrders.cs:74; Plan used by road-development orders) | — | max buildable road route length x(1+S); affects AI road-development route candidacy/plan; cap S<=3.0; content: 6 nodes sum delta 0.29 |
| `catchment.hinterlandRadiusKm` | yes | Systems/Catchment/CatchmentSystem.cs:85 | — | hinterland radius x(1+S) (staleness key line 95); cap S<=1.0; content: 20 nodes sum delta 0.61 |
| `trade.gapClosingFraction` | yes | Systems/Trade/TradeArbitrageSystem.cs:139 | — | arbitrage gap-closing fraction x(1+S), result <=0.95; cap S<=1.5; content: 53 nodes sum delta 1.349 |
| `trade.costPerBulkCostUnit` | yes | Systems/Trade/TradeArbitrageSystem.cs:140 | — | haulage cost per bulk-cost unit x(1-S); cap S<=0.8; content: 41 nodes sum delta 0.719 + levels(frontier_launch) |
| `price.stockReleaseRatePerYear` | yes | Systems/Price/PriceSystem.cs:103 (+TradeArbitrageSystem.cs:141) | — | stock release rate to market x(1+S); cap S<=1.0; content: 24 nodes sum delta 0.71 |
| `demographics.mortalityPerYear` | yes | Systems/Demographics/DemographicsSystem.cs:284 | — | baseline cohort mortality x(1-S); cap S<=0.7; content: 49 nodes sum delta 0.636 + levels(frontier_medicine) |
| `demographics.starvationMortalityMaxPerYear` | yes | Systems/Demographics/DemographicsSystem.cs:235 | — | max starvation mortality x(1-S); cap S<=0.6; content: 22 nodes sum delta 0.54 |
| `demographics.starvationChildMultiplier` | yes | Systems/Demographics/DemographicsSystem.cs:236 | — | child starvation multiplier x(1-S); cap S<=0.5; content: 19 nodes sum delta 0.45 |
| `institutions.universities.maxMortalityReduction` | yes | State/Institutions.cs:316 (via DemographicsSystem.cs:279) | needs a standing inst.university (InstitutionsSystem) | university mortality reduction x(1+S); cap S<=2.0; content: 30 nodes sum delta 0.73 |
| `institutions.universities.maxResearchCostReduction` | yes | Systems/Institutions/InstitutionsSystem.cs:103 | needs a standing inst.university | university research-cost cut x(1+S), result <0.75 -> ResearchCostModifier rows read by ResearchQuery; cap S<=0.5; content: 27 nodes sum delta 0.451 |
| `institutions.universities.maturityTauYears` | yes | Systems/Institutions/InstitutionsSystem.cs:72 | needs a standing inst.university | university maturation tau x(1-S); cap S<=0.5; content: 18 nodes sum delta 0.45 |
| `governance.authorityDecayCostUnits` | yes | State/Governance.cs:105 (via GovernanceSystem.cs:93) | — | administrative reach e-fold x(1+S) -> ControlRow.Strength; cap S<=3.0; content: 74 nodes sum delta 2.49 + levels(institutional_reform) |
| `governance.taxExtractionResponseMax` | yes | State/Governance.cs:172 (via ProductionSystem.cs:201/205) | order gate sim.json governance.taxationRequires='taxation' (+taxationMinAge 3); effective only while a tax rate > 0 | tax extraction output response x(1+S); cap S<=1.5; content: 44 nodes sum delta 1.349 |
| `migration.baseRatePerYear` | yes | Systems/Migration/MigrationSystem.cs:303 | — | migration base rate x(1+S); cap S<=1.0; content: 21 nodes sum delta 0.61 |
| `migration.dampingDecayCostUnits` | yes | Systems/Migration/MigrationSystem.cs:304 | — | migration distance damping e-fold x(1+S); cap S<=2.0; content: 23 nodes sum delta 0.73 |
| `mobility.promoteRatePerYear` | yes | Systems/ClassMobility/ClassMobilitySystem.cs:211 | — | class promotion rate x(1+S); cap S<=1.5; content: 49 nodes sum delta 1.349 |
| `mobility.targetShareCap` | yes | Systems/ClassMobility/ClassMobilitySystem.cs:212 | — | specialist-class target share cap x(1+S), result <=1; cap S<=1.0; content: 34 nodes sum delta 0.9 |
| `colonization.orderPartyFraction` | yes | State/FoundingQuery.cs:245 (via ColonizationSystem.cs:284, AiFoundingPolicy.cs:259) | needs a Found Settlement order (sim.json colonization.aiFounding.enabled true for AI) | founding party fraction x(1+S), result <=0.35; cap S<=0.5; content: 11 nodes sum delta 0.35 |
| `fog.exploreStepNodes` | yes | State/DiscoveryQuery.cs:255 (via DiscoverySystem.cs:75 NextRadius) | sim.json fog.enabled (true); needs an Explore order | radius gained per Explore order x(1+S), floored; cap S<=1.0; content: 17 nodes sum delta 0.81 |
| `fog.exploreMaxRadiusNodes` | yes | State/DiscoveryQuery.cs:259 (via DiscoverySystem.cs:73/75) | sim.json fog.enabled (true); needs an Explore order | explore radius cap x(1+S), floored; cap S<=1.0; content: 21 nodes sum delta 0.9 + levels(frontier_propulsion) |
| `fog.formationSightNodes` | yes | State/DiscoveryQuery.cs:251 (via DiscoverySystem.cs:115 VisibleBits -> SettlementSightings) | sim.json fog.enabled (true); needs owned formations (unit-families.json founds them at capitals) | formation sight radius x(1+S), floored; WEAK: formations are stationary at capitals until M7 movement, base 1 node; cap S<=2.0; content: 15 nodes sum delta 1.52 + levels(frontier_military) |
| `research.civicsPointsCoefficient` | yes | State/ResearchQuery.cs:808 (via ResearchSystem.cs:300) | research.json tuning.civicsPoints.enabled (true) | CP coefficient x(1+S); cap S<=0.3; content: 20 nodes sum delta 0.271 + levels(civic_participation) |
| `research.researchPointsCoefficient` | yes | State/ResearchQuery.cs:770 (via ResearchSystem.cs:281-282) | — | RP coefficient x(1+S) (empires and city-states); cap S<=0.3; content: 29 nodes sum delta 0.277 + levels(frontier_computation) |
| `society.grievanceRelief` | yes | Systems/NeedsGrievance/NeedsGrievanceSystem.cs:206 | — | grievance accrual (needs and levy) x(1-S); cap S<=0.6; content: 63 nodes sum delta 0.539 + levels(social_policy_research) |
| `society.unrestOutputDrag` | yes | State/Unrest.cs:236 (via Governance.OutputMultiplier <- ProductionSystem.cs:201/205) | effective only under a levy (taxation gate) with protest/rebels | share of levied output withheld by unrest x(1-S); cap S<=0.6; content: 54 nodes sum delta 0.553 + levels(frontier_cyber) |

## Entity kinds

| prefix | live | consumer | note |
|---|---|---|---|
| `building.` | yes (partly) | Sim.Core/Systems/Construction/ConstructionSystem.cs | MIXED: 3/43 live (granary, workshop, university = the goods.json projects); 40 have no construction project -> placeholder (deferred-entity plan tiers A-G; castle/bastion_fort/dry_dock named M7) [3/43] |
| `inst.` | yes (partly) | Sim.Core/Systems/Institutions/InstitutionsSystem.cs | MIXED: 1/28 live (inst.university); inst.library, inst.legal_code_roman (and via them inst.archive, inst.scribal_school) are requirement-chain atoms of inst.university, so they gate university founding indirectly but are never founded themselves; inst.military_medicine named M7 [1/28] |
| `infra.` | yes (partly) | Sim.Core/Systems/Roads/RoadDevelopmentSystem.cs | MIXED: 5/25 live (the road classes infra.road_track/built/paved/macadam/highway in sim.json roads.classes); 20 placeholder (irrigation, aqueducts, railway, telegraph, ...) [5/25] |
| `unit.` | no | — | PLACEHOLDER: 0/23. unit-families.json realizedBy names them but "gates nothing"; formations are founded at worldgen and modernized by Age (AgeTransitionSystem), never by research; recruitment is M7 Battle Layer [0/23] |
| `activity.` | yes (partly) | Sim.Core/Systems/Production/ProductionSystem.cs (farming) + Sim.Core/Systems/Trade/TradeArbitrageSystem.cs (trade) | MIXED: 2/9 live (activity.farming via farming.preCultivation; activity.trade via sim.json trade.entity). activity.herding/logging/mining only change the labour-sector LABEL (LabourActivities identities; "production unchanged", ADR-033 D1); fishing/caravans/coastal_shipping/ocean_shipping have no consumer [2/9] |
| `recipe.` | yes | Sim.Core/Systems/Production/ProductionSystem.cs | LIVE: 4/4 goods.json recipes, ProductionSystem crafting (pottery_firing, bronze_casting research-gated; weaving, toolmaking requires null) [4/4] |
| `project.` | no | — | PLACEHOLDER: 0/3 (orbital_launch, probe_acceleration, interstellar_probe); no goods.json project names them [0/3] |

Live entity ids: `building.granary`, `building.workshop`, `building.university`, `infra.road_track`, `infra.road_built`, `infra.road_paved`, `infra.road_macadam`, `infra.road_highway`, `inst.university`, `activity.farming`, `recipe.pottery_firing`, `recipe.weaving`, `recipe.bronze_casting`, `recipe.toolmaking`, `activity.trade`.

**Order gates.** `sim.json governance.taxationRequires = "taxation"` is live: GovernanceSystem.cs:69 and AiGovernance.cs:43 both go through `Governance.CanLevyTax`, which also needs taxationMinAge 3. `sim.json discovery.assayRequires = "ore_prospecting"` is live: `DiscoveryQuery.AssayUnlocked` feeds CheckProspect in DiscoverySystem and AiDiscoveryPolicy. Each of those nodes has no operand effect and no entity unlock, so its only live value is the gate.

## futureSystem rules applied

- **"M8 government adoption" (52):** the node is a form or a module in `docs/m6-plan/knowledge-redesign/data/government-packages.json`. That file has 22 forms, and every form and module id is a research.json node.
- **"M7 battle layer" (31):** 28 nodes unlock only `unit.*` entities. 2 more unlock only units plus M7-tier entities from `docs/deferred-entity-realization-plan.md` §E (building.castle, building.bastion_fort, building.dry_dock, inst.military_medicine): surgery_instruments and trace_italienne. The last node is frontier_military, whose repeatable `_effects` says "no combat is simulated before M7". Its formation-sight effect is live (as a proxy) all the same.
- **"M8 government layer (node desc)" (20):** the node is not in the packages file, but its own desc says "Knowledge only for now: adopting a form of government arrives with the government layer (M8)". Examples: kin_council, corvee_labour, royal_envoys, sworn_treaties, land_registry, tax_farming, abolition, mass_suffrage. These are counted separately so that the packages-based figure of 52 stays exact.
- **null (902):** no milestone is named. The deferred-entity plan sorts the remaining placeholders into tiers A–G but gives tiers A–D, F and G no milestone.

## Nodes with no live value (30 broad, 33 strict)

These nodes have no live operand effect, no live unlock, no sim.json gate, no Age-milestone fact and no stage atom. Like every node, they still act as prerequisites and Age-sweep cohort members.

| node | placeholder unlocks | futureSystem |
|---|---|---|
| `kiln_updraft` | building.kiln | — |
| `oil_press` | building.oil_press | — |
| `papyrus` | building.library | — |
| `plank_boat` | building.harbour | — |
| `horse_domestication` | infra.courier_relay | — |
| `wheel_spoked` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `saddle` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `bridge_stone` | infra.bridge | — |
| `horseshoe` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `fulling_mill` | building.fulling_mill | — |
| `trace_italienne` | building.bastion_fort | M7 battle layer |
| `sulphuric_acid` | building.chemical_works | — |
| `telephone` | infra.telephone_network | — |
| `petroleum_refining` | building.refinery | — |
| `aluminium` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `rocket` | unit.missile, building.spaceport, project.orbital_launch | — |
| `integrated_circuit` | building.semiconductor_fab | — |
| `nuclear_power` | building.nuclear_power_station | — |
| `solar_pv` | building.solar_farm | — |
| `wind_turbine_modern` | building.wind_farm | — |
| `cloud_computing` | building.data_centre | — |
| `desalination` | building.desalination_plant | — |
| `fusion_power` | building.fusion_plant | — |
| `sprung_coach` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `integrated_alliance_command` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `road_houses` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `pilgrimage` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `bridge_trusts` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `turnpike_trusts` | — (only effect: roads.baselineKmPerDay, not read) | — |
| `road_traffic_regulation` | — (only effect: roads.baselineKmPerDay, not read) | — |

## Caveats

- I assessed reachability from code, not by running the game. Many live reads only matter once some state exists: a standing granary, a university, a tax rate above 0, Explore or Found orders, or tools in stock.
- `liveOperandEffects` for a repeatable node uses the string `"level: gapFraction 0.1"` as its delta, because level n adds cap×k×(1−k)^(n−1) rather than a fixed number.
- inst.university does not appear among anyone's node-atom unlocks except legal_code_roman. Its other requirement, inst.library, is an institution chain: archive + scribal_school, which in turn need writing nodes. Nodes that unlock inst.archive or inst.scribal_school are listed under `placeholderUnlocks`, even though they gate university founding indirectly.
