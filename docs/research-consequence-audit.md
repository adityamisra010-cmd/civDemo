# Research consequence audit (closure grade)

**Generated** by `scripts/research-consequence-audit.py` from the canonical content (research.json sha256 `9450783cf3cac4f8…`), the operand registry `Sim.Core/State/KnowledgeOperands.cs`, `scripts/research-consequence-declarations.json` and the M7 building charter §9 register. Do not edit by hand; CI runs `--check`, which fails on a stale file or on any FAIL below. M6 closure, D-066 R2 (the content/consequence audit) and A5 / A9.

## 1. Rules

| rule | name | what it requires | result |
|---|---|---|---|
| R1 | operand registered | every realized and level operand is a row of the operand registry | PASS |
| R2 | operand consumed in play | every realized and level operand is consumed by a simulation system (CR-025 recurrence guard) | PASS |
| R3 | mirror equals C# | this script's operand table equals Sim.Core/State/KnowledgeOperands.cs row for row (id, reader, consumed) | PASS |
| R4 | future effect has an owner | every effects.future entry names a system and a milestone; its operand is null or registered and NOT consumed | PASS |
| R5 | free text declared | every unlocks.techniques / unlocks.applications string is declared knowledge-only, and every declaration is still in content | PASS |
| R6 | entity classified | every entity is realized by a consumer, baseline (null requirement), or deferred with an owner and a target | PASS |
| R7 | gate resolves | every gate expression parses and names only nodes or institution short ids; every gate has an enforcing system | PASS |
| R8 | no double application | DA-1 an operand twice in one node's realized list · DA-2 twice in one level list · DA-3 a future effect on an operand the same node already realizes or levels · DA-4 one entity unlocked twice by one node · DA-5 a non-empty effects.immediate (a retired channel that would apply on top of realized) · DA-6 one consumer object linked to two different entities | PASS |
| R9 | zero unclassified | every item of every node has a class; an entity list naming a non-entity is unclassified | PASS |
| R10 | every node has a consequence | every node has a realized, level or future effect, a realized or deferred entity link, or a gate | PASS |

**Result: PASS — zero unclassified items, no rule failed.**

## 2. Classification totals

1005 nodes (Technology 525, Civics 480); 135 entities.

| item class | items |
|---|---|
| ENTITY-DEFERRED | 185 |
| ENTITY-REALIZED | 24 |
| FUTURE | 26 |
| GATE | 101 |
| KNOWLEDGE | 1488 |
| OPERAND | 1130 |

| entity class | entities |
|---|---|
| baseline | 2 |
| deferred | 114 |
| realized | 19 |

## 3. Operands → consuming system (the mirror of `KnowledgeOperands.cs`)

| operand | consumed by | in play | nodes (realized) | nodes (levels) |
|---|---|---|---|---|
| `farming.yieldPerArableKm2PerYear` | ProductionSystem | yes | 45 | 1 |
| `farming.outputPerFarmerPerYear` | ProductionSystem | yes | 26 | 0 |
| `production.outputPerHerderPerYear` | ProductionSystem | yes | 20 | 0 |
| `production.outputPerExtractorPerYear` | ProductionSystem | yes | 42 | 1 |
| `production.toolYieldBonusMax` | ProductionSystem | yes | 17 | 0 |
| `production.toolWearPerEquippedFarmerPerYear` | ProductionSystem | yes | 17 | 0 |
| `consumption.grainSpoilagePerYear` | ConsumptionSystem | yes | 12 | 0 |
| `consumption.granaryYearsOfDemand` | ConsumptionSystem | yes | 9 | 0 |
| `harvestVariance.sigmaLogYield` | HarvestWeatherSystem | yes | 24 | 1 |
| `granaries.famineReductionMax` | GranaryEffects | yes | 8 | 0 |
| `granaries.farmYieldBonusMax` | GranaryEffects | yes | 2 | 0 |
| `foodState.adaptationAbsorbableShortfall` | FoodState | yes | 33 | 0 |
| `housing.buildLaborAdultYearsPerDwelling` | HousingSystem | yes | 7 | 0 |
| `housing.buildTimberPerDwelling` | HousingSystem | yes | 7 | 1 |
| `housing.upkeepTimberPerDwellingYear` | HousingSystem | yes | 4 | 0 |
| `housing.tauYears` | HousingSystem | yes | 17 | 0 |
| `housing.personsPerDwelling` | HousingSystem and readers | yes | 10 | 0 |
| `pathBuild.laborPerAdultPerYear` | PathBuildSystem | yes | 9 | 0 |
| `pathBuild.buildCostMultiplier` | PathBuildSystem | yes | 9 | 0 |
| `pathBuild.dirtPathSpeedFactor` | PathBuildSystem | yes | 0 | 0 |
| `transport.riverCostFactor` | transport readers | yes | 7 | 0 |
| `roads.baselineKmPerDay` | TransportQuery | NO — no system uses overland travel time yet (CR-025) | 0 | 0 |
| `roads.maxRouteKm` | RoadDevelopmentQuery | yes | 9 | 0 |
| `catchment.hinterlandRadiusKm` | CatchmentSystem | yes | 23 | 0 |
| `trade.gapClosingFraction` | TradeArbitrageSystem | yes | 53 | 0 |
| `trade.costPerBulkCostUnit` | TradeArbitrageSystem | yes | 41 | 1 |
| `price.stockReleaseRatePerYear` | Price / Trade | yes | 24 | 0 |
| `demographics.mortalityPerYear` | DemographicsSystem | yes | 49 | 1 |
| `demographics.starvationMortalityMaxPerYear` | DemographicsSystem | yes | 22 | 0 |
| `demographics.starvationChildMultiplier` | DemographicsSystem | yes | 19 | 0 |
| `institutions.universities.maxMortalityReduction` | Institutions | yes | 30 | 0 |
| `institutions.universities.maxResearchCostReduction` | Institutions | yes | 27 | 0 |
| `institutions.universities.maturityTauYears` | Institutions | yes | 18 | 0 |
| `governance.authorityDecayCostUnits` | Governance | yes | 76 | 1 |
| `governance.taxExtractionResponseMax` | Governance | yes | 44 | 0 |
| `migration.baseRatePerYear` | MigrationSystem | yes | 21 | 0 |
| `migration.dampingDecayCostUnits` | MigrationSystem | yes | 23 | 0 |
| `mobility.promoteRatePerYear` | ClassMobilitySystem | yes | 49 | 0 |
| `mobility.targetShareCap` | ClassMobilitySystem | yes | 34 | 0 |
| `colonization.orderPartyFraction` | FoundingQuery | yes | 11 | 0 |
| `fog.exploreStepNodes` | DiscoveryQuery | yes | 17 | 0 |
| `fog.exploreMaxRadiusNodes` | DiscoveryQuery | yes | 21 | 1 |
| `fog.formationSightNodes` | DiscoveryQuery | yes | 15 | 1 |
| `research.civicsPointsCoefficient` | ResearchQuery (CP) | yes | 20 | 1 |
| `research.researchPointsCoefficient` | ResearchQuery (RP) | yes | 29 | 1 |
| `society.grievanceRelief` | NeedsGrievanceSystem | yes | 63 | 1 |
| `society.unrestOutputDrag` | Governance / Unrest | yes | 54 | 1 |

## 4. Gates → enforcing system

The university founding chain is the entity gate of `building.university` and `inst.university` (research.json), realized by the goods.json projects that found universities (§5).

| gate | expression | enforced by |
|---|---|---|
| sim.json governance.taxationRequires | `taxation` | GovernanceSystem — Governance.CanLevyTax (sim.json governance.taxationRequires; order 5 SetTaxRate) |
| research.json researchStage.requires | `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal` | ResearchSystem — ResearchQuery.StageReached (research.json researchStage.requires opens the subtrees; order 6) |
| sim.json discovery.assayRequires | `ore_prospecting` | DiscoverySystem — DiscoveryQuery.AssayUnlocked (sim.json discovery.assayRequires; order 15 Prospect, Amount 1) |
| goods.json recipe `iron-smelting`.knowledge | `iron_bloomery` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `coke-smelting`.knowledge | `coke` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `steel-cementation`.knowledge | `steel_carburized` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `steel-converting`.knowledge | `bessemer` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `iron-toolmaking`.knowledge | `iron_farm_tools` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `gunpowder-milling`.knowledge | `saltpetre_refining` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json recipe `explosives-making`.knowledge | `dynamite` | ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met) |
| goods.json deposit `iron-ore`.knowledge | `iron_bloomery` | ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known) |
| goods.json deposit `coal`.knowledge | `coke` | ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known) |
| goods.json herd `horses`.knowledge | `horse_domestication` | ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known) |
| goods.json deposit `saltpetre`.knowledge | `saltpetre_refining` | ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known) |
| goods.json deposit `sulphur`.knowledge | `saltpetre_refining` | ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known) |
| ages.json Age 2 milestone `a2_cultivation` (core) | `cereal_cultivation` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 2 milestone `a2_pottery` (supporting) | `pottery_open_fired` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 2 milestone `a2_herding` (supporting) | `sheep_goat` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 3 milestone `a3_bronze` (core) | `arsenical_bronze OR tin_bronze` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 3 milestone `a3_writing` (supporting) | `proto_writing OR token_counting` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 3 milestone `a3_law` (supporting) | `law_code` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 3 milestone `a3_wheel` (supporting) | `wheel_solid` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 4 milestone `a4_iron` (core) | `iron_bloomery` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 4 milestone `a4_alphabet` (supporting) | `abjad` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 4 milestone `a4_census` (supporting) | `census` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_systematic_law` (core) | `legal_code_roman` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_coinage` (core) | `coinage_electrum` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_geometry` (supporting) | `geometry_axiomatic` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_roads` (supporting) | `road_paved` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_concrete` (supporting) | `concrete_pozzolan` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 5 milestone `a5_wages` (supporting) | `coined_wage` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 6 milestone `a6_heavy_plough` (core) | `heavy_plough` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 6 milestone `a6_paper` (supporting) | `paper` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 6 milestone `a6_mills` (supporting) | `water_mill OR windmill_post` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 6 milestone `a6_finance` (supporting) | `double_entry OR bill_of_exchange` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 6 milestone `a6_numerals` (supporting) | `hindu_arabic` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_printing` (core) | `printing_press` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_ocean` (supporting) | `caravel` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_powder` (supporting) | `corned_powder` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_science` (supporting) | `mechanics_newtonian` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_patent` (supporting) | `patent` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 7 milestone `a7_company` (supporting) | `joint_stock` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 8 milestone `a8_steam` (core) | `steam_rotary` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 8 milestone `a8_iron` (supporting) | `puddling` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 8 milestone `a8_rail` (supporting) | `railway` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 8 milestone `a8_telegraph` (supporting) | `telegraph` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 8 milestone `a8_germ` (supporting) | `germ_theory` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 9 milestone `a9_electricity` (core) | `electricity_generation` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 9 milestone `a9_combustion` (core) | `internal_combustion` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 9 milestone `a9_radio` (supporting) | `radio` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 9 milestone `a9_antibiotics` (supporting) | `antibiotic_penicillin` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |
| ages.json Age 9 milestone `a9_computing` (supporting) | `computer` | AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge) |

## 5. Entities

| entity | class | realized by / owner |
|---|---|---|
| `activity.caravans` | deferred | Trade / transport — overland haulage on the M7 movement and route graph (alters transport cost; transport is frozen without a ruled packet) [target after M7 (route graph)] |
| `activity.coastal_shipping` | deferred | Trade / transport — sea haulage on the M7 movement and route graph [target after M7 (route graph)] |
| `activity.farming` | realized | LabourActivities: sector `farming` → replaces |
| `activity.fishing` | baseline | null requirement — available from the founding (no realizing system) |
| `activity.herding` | realized | LabourActivities: sector `herding_fishing` → replaces |
| `activity.logging` | realized | LabourActivities: sector `extraction` → joins |
| `activity.mining` | realized | LabourActivities: sector `extraction` → joins |
| `activity.ocean_shipping` | deferred | Trade / transport — sea haulage on the M7 movement and route graph [target after M7 (route graph)] |
| `activity.trade` | realized | TradeArbitrageSystem: sim.json trade.entity |
| `building.airfield` | deferred | EC-4 / EC-5; air basing [charter §9 status B] |
| `building.bastion_fort` | deferred | fortification term [charter §9 status M7] |
| `building.bathhouse` | deferred | M9 health [charter §9 status M8+] |
| `building.blast_furnace` | deferred | EC-1 [charter §9 status B] |
| `building.castle` | deferred | fortification term [charter §9 status M7] |
| `building.cement_works` | deferred | limestone and fuel goods [charter §9 status B] |
| `building.chemical_works` | deferred | sulphur and chemical goods [charter §9 status B] |
| `building.city_wall` | deferred | fortification term [charter §9 status M7] |
| `building.data_centre` | deferred | EC-5 + energy + DN [charter §9 status B] |
| `building.desalination_plant` | deferred | energy [charter §9 status B] |
| `building.dry_dock` | deferred | M7.6 (vessel repair) [charter §9 status M7] |
| `building.fulling_mill` | deferred | Production (CR-025 future-only node `fulling_mill`) [charter §9 status D] |
| `building.fusion_plant` | deferred | energy (future-only) [charter §9 status B] |
| `building.granary` | realized | ConstructionSystem: project `granary` (null requirement: from the founding) |
| `building.harbour` | deferred | M7.6 water edges [charter §9 status M7] |
| `building.hospital` | deferred | M9 health [charter §9 status M8+] |
| `building.hydroelectric_dam` | deferred | energy system [charter §9 status B] |
| `building.kiln` | deferred | Production (capacity term) [charter §9 status D] |
| `building.library` | deferred | Research [charter §9 status D] |
| `building.lighthouse` | deferred | M7.6 (coastal loss term) [charter §9 status M7] |
| `building.mint` | deferred | no coin good (then M8 finance) [charter §9 status B] |
| `building.nuclear_power_station` | deferred | EC-6 + energy [charter §9 status B] |
| `building.observatory` | deferred | Research [charter §9 status D] |
| `building.oil_press` | deferred | no vegetable-oil good [charter §9 status B] |
| `building.paper_mill` | deferred | no paper good [charter §9 status B] |
| `building.power_station` | deferred | energy system + fuel [charter §9 status B] |
| `building.printing_house` | deferred | no paper good (then Research / M9 culture) [charter §9 status B] |
| `building.radio_transmitter` | deferred | EC-5 + energy [charter §9 status B] |
| `building.refinery` | deferred | EC-4 [charter §9 status B] |
| `building.research_laboratory` | deferred | Research [charter §9 status D] |
| `building.school` | deferred | Research (RP mechanism) [charter §9 status D] |
| `building.semiconductor_fab` | deferred | EC-5 + energy [charter §9 status B] |
| `building.skyscraper` | deferred | EC-1 steel; cement [charter §9 status B] |
| `building.smithy` | deferred | Production [charter §9 status D] |
| `building.solar_farm` | deferred | energy; irradiance channel (B3) [charter §9 status B] |
| `building.spaceport` | deferred | §11 [charter §9 status B] |
| `building.steelworks` | deferred | EC-1 [charter §9 status B] |
| `building.textile_mill` | deferred | Production; power-loom variant B (EC-1 coal) [charter §9 status D] |
| `building.university` | realized | ConstructionSystem: project `military university`, ConstructionSystem: project `medical university`, ConstructionSystem: project `engineering university`, ConstructionSystem: project `natural science university`, ConstructionSystem: project `agricultural university` |
| `building.water_mill` | deferred | Production (mechanical power) [charter §9 status D] |
| `building.wind_farm` | deferred | energy; wind channel (B3) [charter §9 status B] |
| `building.windmill` | deferred | Production; wind channel (B3) [charter §9 status D] |
| `building.workshop` | realized | ConstructionSystem: project `workshop` (null requirement: from the founding) |
| `infra.aqueduct` | deferred | M9 [charter §9 status M8+] |
| `infra.basin_irrigation` | deferred | Farming (preCultivation ruling) [charter §9 status D] |
| `infra.bridge` | deferred | transport packet [charter §9 status M7] |
| `infra.canal_irrigation` | deferred | Farming [charter §9 status D] |
| `infra.courier_relay` | deferred | EC-2 mounts; command latency (M7) when unblocked [charter §9 status B] |
| `infra.fish_weir` | deferred | Fishing activity [charter §9 status D] |
| `infra.internet_backbone` | deferred | EC-5 + energy + DN [charter §9 status B] |
| `infra.mobile_network` | deferred | EC-5 + energy + DN [charter §9 status B] |
| `infra.navigation_canal` | deferred | M7.6 water edges [charter §9 status M7] |
| `infra.power_grid` | deferred | energy system [charter §9 status B] |
| `infra.qanat` | deferred | Farming [charter §9 status D] |
| `infra.railway` | deferred | EC-1 [charter §9 status B] |
| `infra.road_built` | realized | RoadDevelopmentSystem: road class `infra.road_built` |
| `infra.road_highway` | realized | RoadDevelopmentSystem: road class `infra.road_highway` |
| `infra.road_macadam` | realized | RoadDevelopmentSystem: road class `infra.road_macadam` |
| `infra.road_paved` | realized | RoadDevelopmentSystem: road class `infra.road_paved` |
| `infra.road_track` | realized | RoadDevelopmentSystem: road class `infra.road_track` |
| `infra.sewer` | deferred | M9 [charter §9 status M8+] |
| `infra.sewerage_system` | deferred | M9 [charter §9 status M8+] |
| `infra.submarine_cable` | deferred | EC-5 [charter §9 status B] |
| `infra.telegraph_line` | deferred | EC-5 (copper metal) [charter §9 status B] |
| `infra.telephone_network` | deferred | EC-5 + energy [charter §9 status B] |
| `infra.terraces` | deferred | Farming [charter §9 status D] |
| `infra.urban_drainage` | deferred | M9 [charter §9 status M8+] |
| `infra.well` | deferred | M9 health / growth [charter §9 status M8+] |
| `inst.aramaic_admin` | deferred | M8 [charter §9 status M8+] |
| `inst.archive` | deferred | Research / M8 administration [charter §9 status D] |
| `inst.burial` | baseline | null requirement — available from the founding (no realizing system) |
| `inst.census` | deferred | M8 (tax efficiency term) [charter §9 status M8+] |
| `inst.central_bank` | deferred | M8 [charter §9 status M8+] |
| `inst.coined_wage` | deferred | M8 [charter §9 status M8+] |
| `inst.guild` | deferred | Production / M9 [charter §9 status D] |
| `inst.historiography` | deferred | Research [charter §9 status D] |
| `inst.hospital` | deferred | M9 [charter §9 status M8+] |
| `inst.joint_stock` | deferred | M8 [charter §9 status M8+] |
| `inst.law_code` | realized | ConstructionSystem + InstitutionsSystem: project `law court` founds |
| `inst.legal_code_roman` | deferred | M8 [charter §9 status M8+] |
| `inst.library` | deferred | Research [charter §9 status D] |
| `inst.mass_media_broadcast` | deferred | M9 (cultural groups gate) [charter §9 status M8+] |
| `inst.mass_schooling` | deferred | M9 [charter §9 status M8+] |
| `inst.military_medicine` | deferred | field-medical family (unit charter §7.12) [charter §9 status M7] |
| `inst.mint_institution` | deferred | no coin good (then M8) [charter §9 status B] |
| `inst.natural_philosophy` | deferred | Research [charter §9 status D] |
| `inst.newspaper` | deferred | M8 / M9 (opinion) [charter §9 status M8+] |
| `inst.patent` | deferred | M8 [charter §9 status M8+] |
| `inst.quarantine` | deferred | M9 [charter §9 status M8+] |
| `inst.scientific_society` | deferred | SP recruitment points (M9) [charter §9 status M8+] |
| `inst.scribal_school` | deferred | Research [charter §9 status D] |
| `inst.standard_threads` | deferred | Production [charter §9 status D] |
| `inst.statistics_vital` | deferred | M9 [charter §9 status M8+] |
| `inst.textile_workshop` | deferred | Production [charter §9 status D] |
| `inst.translation_movement` | deferred | Research [charter §9 status D] |
| `inst.university` | realized | ConstructionSystem + InstitutionsSystem: project `military university` founds, ConstructionSystem + InstitutionsSystem: project `medical university` founds, ConstructionSystem + InstitutionsSystem: project `engineering university` founds, ConstructionSystem + InstitutionsSystem: project `natural science university` founds, ConstructionSystem + InstitutionsSystem: project `agricultural university` founds |
| `project.interstellar_probe` | deferred | §11; endgame semantics unruled [charter §9 status B] |
| `project.orbital_launch` | deferred | §11 [charter §9 status B] |
| `project.probe_acceleration` | deferred | §11 [charter §9 status B] |
| `recipe.bronze_casting` | realized | ProductionSystem: recipe `bronze-casting` |
| `recipe.pottery_firing` | realized | ProductionSystem: recipe `pottery-firing` |
| `recipe.toolmaking` | realized | ProductionSystem: recipe `toolmaking` (null requirement: from the founding) |
| `recipe.weaving` | realized | ProductionSystem: recipe `weaving` (null requirement: from the founding) |
| `unit.aircraft_carrier` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.archers` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.artillery` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.chariot` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.combat_drone` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.crossbowmen` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.heavy_cavalry` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.helicopter` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.horse_archers` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.icbm` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.ironclad` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.longbowmen` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.military_aircraft` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.missile` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.musketeers` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.nuclear_submarine` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.ship_of_the_line` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.siege_engine` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.slingers` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.spearmen` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.stealth_aircraft` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.tank` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |
| `unit.war_galley` | deferred | M7 Battle Layer — recruitment and battle (ADR-033 D7; ADR-055 recruits by unit-families.json identity, not by these entities) [target M7] |

## 6. Free text declared knowledge-only

unlocks.techniques and unlocks.applications are free text, never entity ids. Each is KNOWLEDGE-ONLY: what the civilization knows how to do, shown in the research views, with no simulation consequence in M6. The node's mechanics are carried by its realized/future effects and its entity links (D-063 R1; D-066 A5: verb-granting knowledge is knowledge-only in M6, consumed by the owning system when it exists).

| node | list | items |
|---|---|---|
| `calendar_lunar` | techniques | Solstice alignment |
| `cereal_domesticated` | techniques | Threshing and winnowing |
| `cordage` | techniques | Knots and lashing; Net making; Windbreak and hide shelter |
| `dugout` | techniques | Large-log hull hollowing |
| `fermentation_grain` | techniques | Leavened bread |
| `fire_making` | techniques | Heat treatment of silcrete and flint; Earth-oven cooking at scale; Planned landscape burning |
| `flintlock` | techniques | Socket bayonet; Drill and the printed manual |
| `grinding_stone` | techniques | Wild cereal harvesting; Storage pit and cache; Drying and smoking |
| `gunpowder` | techniques | Fire lance |
| `heavy_plough` | techniques | Three-field rotation |
| `interchangeable_parts` | techniques | Moving assembly line |
| `iron_bloomery` | techniques | Forging; Massed heavy infantry |
| `lost_wax` | techniques | Goldsmithing |
| `milking` | techniques | Cheese making |
| `mining_shaft` | techniques | Fire-setting; Mining under walls |
| `sewing` | techniques | Tailored fur clothing |
| `sheep_goat` | techniques | Transhumance; Dung as fuel |
| `standard_weights` | techniques | Balance scale |
| `taxation` | techniques | Harvest assessment; Levy in kind into sealed stores |
| `trade` | techniques | Counted consignments; Pack-animal and boat haulage of goods for exchange |
| `wheel_solid` | techniques | Wheelbarrow |
| `cuneiform` | applications | Written literature; Viticulture and wine trade |
| `gearing` | applications | Seismoscope |
| `knapping_blade` | applications | Long-distance obsidian exchange |
| `ochre_processing` | applications | Representational art; Personal ornament |
| `stone_dry` | applications | Megalithic construction |

## 7. Double application (R8) and recorded observations

| rule | subject | observation |
|---|---|---|
| R8 | `frontier_materials` | completion + level on `housing.buildTimberPerDwelling`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |
| R8 | `frontier_crops` | completion + level on `farming.yieldPerArableKm2PerYear`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |
| R8 | `frontier_medicine` | completion + level on `demographics.mortalityPerYear`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |
| R8 | `frontier_military` | completion + level on `fog.formationSightNodes`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |
| R8 | `frontier_cyber` | completion + level on `society.unrestOutputDrag`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |
| R8 | `frontier_climate` | completion + level on `harvestVariance.sigmaLogYield`: the completion delta applies once (ADR-047) and each level adds cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap (KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one |

## 8. Every node

Counts per item class (O operand, F future, K knowledge text, E entity unlock, G gate, U unclassified).

| node | O | F | K | E | G | U |
|---|---|---|---|---|---|---|
| `fire_making` | 1 | 0 | 5 | 0 | 0 | 0 |
| `knapping_oldowan` | 1 | 0 | 3 | 0 | 0 | 0 |
| `knapping_levallois` | 1 | 0 | 1 | 0 | 0 | 0 |
| `knapping_blade` | 1 | 0 | 5 | 0 | 0 | 0 |
| `pressure_flaking` | 1 | 0 | 2 | 0 | 0 | 0 |
| `heat_treatment_stone` | 2 | 0 | 1 | 0 | 0 | 0 |
| `microlith` | 1 | 0 | 3 | 0 | 0 | 0 |
| `ground_stone_early` | 1 | 0 | 2 | 1 | 0 | 0 |
| `adhesive_natural` | 2 | 0 | 1 | 0 | 0 | 0 |
| `birch_tar` | 1 | 0 | 2 | 0 | 0 | 0 |
| `hafting` | 1 | 0 | 2 | 1 | 0 | 0 |
| `cordage` | 1 | 0 | 7 | 0 | 0 | 0 |
| `basketry` | 1 | 0 | 4 | 1 | 0 | 0 |
| `atlatl` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bow_simple` | 1 | 0 | 2 | 1 | 0 | 0 |
| `sling` | 1 | 0 | 2 | 1 | 0 | 0 |
| `shelter_hut` | 1 | 0 | 2 | 0 | 0 | 0 |
| `hide_working` | 1 | 0 | 3 | 0 | 0 | 0 |
| `sewing` | 1 | 0 | 4 | 0 | 0 | 0 |
| `raft` | 1 | 0 | 1 | 0 | 0 | 0 |
| `dugout` | 1 | 0 | 3 | 0 | 0 | 0 |
| `fishing_hook` | 1 | 0 | 2 | 0 | 0 | 0 |
| `grinding_stone` | 1 | 0 | 5 | 0 | 0 | 0 |
| `dog_domestication` | 1 | 0 | 3 | 0 | 0 | 0 |
| `ochre_processing` | 1 | 0 | 4 | 0 | 0 | 0 |
| `bone_tools` | 1 | 0 | 4 | 0 | 0 | 0 |
| `lamp` | 1 | 0 | 2 | 0 | 0 | 0 |
| `tally_notation` | 1 | 0 | 2 | 0 | 0 | 0 |
| `cereal_cultivation` | 1 | 0 | 2 | 1 | 1 | 0 |
| `cereal_domesticated` | 1 | 0 | 3 | 0 | 0 | 0 |
| `rice_wet` | 2 | 0 | 2 | 1 | 0 | 0 |
| `millet` | 1 | 0 | 1 | 1 | 0 | 0 |
| `maize` | 1 | 0 | 2 | 1 | 0 | 0 |
| `sorghum_pearl_millet` | 2 | 0 | 1 | 1 | 0 | 0 |
| `root_crop` | 2 | 0 | 2 | 1 | 0 | 0 |
| `legume` | 2 | 0 | 2 | 0 | 0 | 0 |
| `orchard` | 2 | 0 | 3 | 0 | 0 | 0 |
| `sickle` | 1 | 0 | 1 | 0 | 0 | 0 |
| `digging_stick_hoe` | 1 | 0 | 2 | 0 | 0 | 0 |
| `nixtamalization` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sheep_goat` | 1 | 0 | 4 | 1 | 1 | 0 |
| `cattle` | 2 | 0 | 3 | 1 | 0 | 0 |
| `pig` | 1 | 0 | 1 | 0 | 0 | 0 |
| `milking` | 1 | 0 | 3 | 0 | 0 | 0 |
| `wool` | 1 | 0 | 2 | 0 | 0 | 0 |
| `animal_traction` | 2 | 0 | 3 | 0 | 0 | 0 |
| `pottery_open_fired` | 2 | 0 | 3 | 1 | 1 | 0 |
| `kiln_updraft` | 0 | 1 | 3 | 1 | 0 | 0 |
| `potters_wheel_fast` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mudbrick` | 1 | 0 | 3 | 1 | 0 | 0 |
| `lime_plaster` | 2 | 0 | 3 | 0 | 0 | 0 |
| `timber_frame` | 1 | 0 | 2 | 0 | 0 | 0 |
| `stone_dry` | 1 | 0 | 4 | 3 | 0 | 0 |
| `well` | 1 | 0 | 2 | 1 | 0 | 0 |
| `fermentation_grain` | 2 | 0 | 4 | 0 | 0 | 0 |
| `fermentation_fruit` | 1 | 0 | 2 | 0 | 0 | 0 |
| `oil_press` | 0 | 1 | 3 | 1 | 0 | 0 |
| `salt_extraction` | 2 | 0 | 2 | 0 | 0 | 0 |
| `flax` | 2 | 0 | 1 | 0 | 0 | 0 |
| `spinning_spindle` | 1 | 0 | 1 | 0 | 0 | 0 |
| `loom_warp_weighted` | 1 | 0 | 2 | 1 | 1 | 0 |
| `dyeing` | 1 | 0 | 3 | 0 | 0 | 0 |
| `copper_native` | 1 | 0 | 2 | 0 | 0 | 0 |
| `copper_smelting` | 1 | 0 | 2 | 1 | 0 | 0 |
| `charcoal` | 1 | 0 | 2 | 0 | 0 | 0 |
| `casting_open` | 1 | 0 | 1 | 0 | 0 | 0 |
| `gold_silver_native` | 1 | 0 | 2 | 0 | 0 | 0 |
| `irrigation_basin` | 1 | 0 | 2 | 1 | 0 | 0 |
| `irrigation_canal` | 1 | 0 | 3 | 1 | 0 | 0 |
| `terrace` | 1 | 0 | 2 | 1 | 0 | 0 |
| `sledge` | 1 | 0 | 1 | 0 | 0 | 0 |
| `wheel_solid` | 1 | 0 | 3 | 0 | 1 | 0 |
| `cart` | 1 | 0 | 1 | 0 | 0 | 0 |
| `track_road` | 0 | 0 | 2 | 3 | 0 | 0 |
| `plough_ard` | 2 | 0 | 2 | 0 | 0 | 0 |
| `token_counting` | 1 | 0 | 1 | 0 | 1 | 0 |
| `stamp_seal` | 1 | 0 | 3 | 1 | 9 | 0 |
| `calendar_lunar` | 2 | 0 | 3 | 0 | 0 | 0 |
| `trepanation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `arsenical_bronze` | 1 | 0 | 2 | 0 | 1 | 0 |
| `tin_bronze` | 1 | 0 | 4 | 1 | 1 | 0 |
| `casting_closed` | 1 | 0 | 2 | 0 | 0 | 0 |
| `lost_wax` | 1 | 0 | 4 | 0 | 0 | 0 |
| `sheet_metal` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mining_shaft` | 1 | 0 | 4 | 1 | 0 | 0 |
| `proto_writing` | 1 | 0 | 1 | 0 | 1 | 0 |
| `cuneiform` | 1 | 0 | 6 | 6 | 7 | 0 |
| `hieroglyphic` | 1 | 0 | 2 | 6 | 7 | 0 |
| `chinese_script` | 1 | 0 | 2 | 6 | 7 | 0 |
| `papyrus` | 0 | 1 | 3 | 1 | 0 | 0 |
| `numeral_sexagesimal` | 1 | 0 | 4 | 0 | 0 | 0 |
| `arithmetic_babylonian` | 1 | 0 | 3 | 0 | 0 | 0 |
| `surveying` | 1 | 0 | 3 | 0 | 0 | 0 |
| `sail_square` | 1 | 0 | 3 | 1 | 0 | 0 |
| `plank_boat` | 0 | 1 | 2 | 1 | 0 | 0 |
| `horse_domestication` | 1 | 0 | 3 | 1 | 1 | 0 |
| `donkey` | 1 | 0 | 2 | 1 | 0 | 0 |
| `camel` | 1 | 0 | 2 | 1 | 0 | 0 |
| `wheel_spoked` | 0 | 2 | 1 | 0 | 0 | 0 |
| `chariot` | 1 | 0 | 2 | 1 | 0 | 0 |
| `composite_bow` | 1 | 0 | 2 | 1 | 0 | 0 |
| `fired_brick` | 1 | 0 | 4 | 2 | 0 | 0 |
| `glass_glaze` | 1 | 0 | 2 | 0 | 0 | 0 |
| `glass_core` | 1 | 0 | 2 | 0 | 0 | 0 |
| `standard_weights` | 1 | 0 | 4 | 0 | 0 | 0 |
| `calendar_civil` | 1 | 0 | 2 | 0 | 0 | 0 |
| `water_clock` | 1 | 0 | 2 | 0 | 0 | 0 |
| `medicine_recorded` | 1 | 0 | 2 | 0 | 0 | 0 |
| `beekeeping` | 1 | 0 | 2 | 0 | 0 | 0 |
| `iron_bloomery` | 1 | 0 | 6 | 1 | 3 | 0 |
| `bellows` | 1 | 0 | 2 | 0 | 0 | 0 |
| `steel_carburized` | 1 | 0 | 2 | 0 | 1 | 0 |
| `quenching` | 1 | 0 | 1 | 0 | 0 | 0 |
| `tempering` | 2 | 0 | 2 | 0 | 0 | 0 |
| `wootz` | 1 | 0 | 2 | 0 | 0 | 0 |
| `pattern_welding` | 2 | 0 | 1 | 0 | 0 | 0 |
| `abjad` | 1 | 0 | 2 | 2 | 1 | 0 |
| `alphabet_vowels` | 1 | 0 | 2 | 0 | 0 | 0 |
| `brahmi` | 1 | 0 | 1 | 1 | 0 | 0 |
| `hacksilver` | 1 | 0 | 3 | 0 | 0 | 0 |
| `coinage_electrum` | 2 | 0 | 4 | 2 | 1 | 0 |
| `cavalry` | 1 | 0 | 3 | 1 | 0 | 0 |
| `saddle` | 0 | 1 | 2 | 0 | 0 | 0 |
| `siege_ram` | 1 | 0 | 1 | 1 | 0 | 0 |
| `siege_tower` | 1 | 0 | 1 | 0 | 0 | 0 |
| `stone_fortification` | 1 | 0 | 2 | 2 | 0 | 0 |
| `crossbow` | 1 | 0 | 1 | 1 | 0 | 0 |
| `naval_ram` | 1 | 0 | 2 | 1 | 0 | 0 |
| `qanat` | 1 | 0 | 2 | 1 | 0 | 0 |
| `aqueduct_tunnel` | 1 | 0 | 2 | 1 | 0 | 0 |
| `aqueduct_channel` | 1 | 0 | 1 | 1 | 0 | 0 |
| `arch` | 1 | 0 | 2 | 0 | 0 | 0 |
| `astronomy_records` | 1 | 0 | 3 | 1 | 0 | 0 |
| `calendar_lunisolar` | 1 | 0 | 1 | 0 | 0 | 0 |
| `geometry_practical` | 1 | 0 | 2 | 0 | 0 | 0 |
| `medicine_hippocratic` | 1 | 0 | 3 | 3 | 2 | 0 |
| `glass_blown_precursor` | 1 | 0 | 2 | 0 | 0 | 0 |
| `rotary_quern` | 1 | 0 | 1 | 0 | 0 | 0 |
| `olive_press_beam` | 1 | 0 | 1 | 0 | 0 | 0 |
| `geometry_axiomatic` | 1 | 0 | 3 | 1 | 2 | 0 |
| `mechanics_archimedean` | 1 | 0 | 4 | 0 | 0 | 0 |
| `torsion_artillery` | 1 | 0 | 2 | 1 | 0 | 0 |
| `water_screw` | 1 | 0 | 2 | 0 | 0 | 0 |
| `noria` | 1 | 0 | 2 | 0 | 0 | 0 |
| `water_mill` | 1 | 0 | 2 | 2 | 1 | 0 |
| `trip_hammer` | 1 | 0 | 2 | 0 | 0 | 0 |
| `gearing` | 1 | 0 | 4 | 0 | 0 | 0 |
| `concrete_pozzolan` | 2 | 0 | 3 | 0 | 1 | 0 |
| `vault_dome` | 1 | 0 | 3 | 3 | 0 | 0 |
| `road_paved` | 1 | 0 | 2 | 1 | 1 | 0 |
| `bridge_stone` | 1 | 0 | 2 | 1 | 0 | 0 |
| `aqueduct_arcade` | 1 | 0 | 3 | 2 | 0 | 0 |
| `glass_blowing` | 1 | 0 | 2 | 0 | 0 | 0 |
| `window_glass` | 1 | 0 | 2 | 0 | 0 | 0 |
| `cast_iron` | 1 | 0 | 3 | 1 | 0 | 0 |
| `iron_mouldboard` | 2 | 0 | 2 | 0 | 0 | 0 |
| `seed_drill` | 2 | 0 | 3 | 0 | 0 | 0 |
| `collar_harness` | 1 | 0 | 1 | 0 | 0 | 0 |
| `paper` | 1 | 0 | 3 | 3 | 1 | 0 |
| `crossbow_repeating` | 2 | 0 | 1 | 0 | 0 | 0 |
| `stirrup` | 1 | 0 | 2 | 1 | 0 | 0 |
| `silk` | 1 | 0 | 2 | 0 | 0 | 0 |
| `lacquer` | 1 | 0 | 2 | 0 | 0 | 0 |
| `astronomy_geometric` | 1 | 0 | 3 | 1 | 0 | 0 |
| `cartography` | 1 | 0 | 2 | 0 | 0 | 0 |
| `latitude_sailing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `monsoon_sailing` | 1 | 0 | 2 | 0 | 0 | 0 |
| `lateen` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mortise_hull` | 1 | 0 | 2 | 0 | 0 | 0 |
| `pharmacology` | 1 | 0 | 2 | 2 | 1 | 0 |
| `surgery_instruments` | 1 | 0 | 3 | 1 | 0 | 0 |
| `abacus` | 1 | 0 | 1 | 0 | 0 | 0 |
| `alchemy` | 1 | 0 | 3 | 0 | 0 | 0 |
| `distillation` | 1 | 0 | 3 | 0 | 0 | 0 |
| `heavy_plough` | 1 | 0 | 3 | 0 | 1 | 0 |
| `horse_collar` | 2 | 0 | 2 | 0 | 0 | 0 |
| `horseshoe` | 1 | 0 | 1 | 0 | 0 | 0 |
| `rice_champa` | 2 | 0 | 2 | 0 | 0 | 0 |
| `sugar_refining` | 2 | 0 | 2 | 0 | 0 | 0 |
| `windmill_vertical` | 1 | 0 | 1 | 1 | 0 | 0 |
| `windmill_post` | 1 | 0 | 2 | 1 | 1 | 0 |
| `fulling_mill` | 0 | 1 | 2 | 1 | 0 | 0 |
| `spinning_wheel` | 1 | 0 | 1 | 0 | 0 | 0 |
| `horizontal_loom` | 1 | 0 | 2 | 0 | 0 | 0 |
| `cotton_gin_roller` | 1 | 0 | 1 | 0 | 0 | 0 |
| `compass_magnetic` | 1 | 0 | 2 | 0 | 0 | 0 |
| `stern_rudder` | 1 | 0 | 2 | 0 | 0 | 0 |
| `watertight_bulkhead` | 1 | 0 | 1 | 0 | 0 | 0 |
| `portolan` | 1 | 0 | 1 | 0 | 0 | 0 |
| `astrolabe` | 1 | 0 | 3 | 1 | 0 | 0 |
| `cog` | 1 | 0 | 1 | 0 | 0 | 0 |
| `gunpowder` | 1 | 0 | 4 | 0 | 0 | 0 |
| `saltpetre_refining` | 1 | 0 | 1 | 0 | 3 | 0 |
| `cannon_early` | 1 | 0 | 1 | 1 | 0 | 0 |
| `trebuchet` | 1 | 0 | 1 | 1 | 0 | 0 |
| `longbow` | 1 | 0 | 1 | 1 | 0 | 0 |
| `plate_armour` | 1 | 0 | 1 | 0 | 0 | 0 |
| `greek_fire` | 1 | 0 | 1 | 0 | 0 | 0 |
| `woodblock_print` | 1 | 0 | 3 | 1 | 0 | 0 |
| `movable_type_ceramic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `movable_type_metal` | 1 | 0 | 1 | 0 | 0 | 0 |
| `paper_money` | 1 | 0 | 2 | 1 | 0 | 0 |
| `hindu_arabic` | 1 | 0 | 3 | 0 | 1 | 0 |
| `algebra` | 1 | 0 | 3 | 1 | 1 | 0 |
| `trigonometry` | 1 | 0 | 3 | 0 | 0 | 0 |
| `optics_ibn_haytham` | 1 | 0 | 2 | 1 | 0 | 0 |
| `spectacles` | 1 | 0 | 2 | 0 | 0 | 0 |
| `crank_connecting_rod` | 1 | 0 | 3 | 0 | 0 | 0 |
| `escapement_verge` | 1 | 0 | 2 | 0 | 0 | 0 |
| `blast_furnace_water` | 1 | 0 | 2 | 1 | 0 | 0 |
| `wire_drawing` | 1 | 0 | 3 | 0 | 0 | 0 |
| `canal_lock` | 1 | 0 | 2 | 0 | 0 | 0 |
| `double_entry` | 1 | 0 | 3 | 0 | 1 | 0 |
| `bill_of_exchange` | 1 | 0 | 3 | 0 | 1 | 0 |
| `printing_press` | 2 | 0 | 2 | 4 | 2 | 0 |
| `type_founding` | 1 | 0 | 1 | 0 | 0 | 0 |
| `caravel` | 1 | 0 | 3 | 1 | 1 | 0 |
| `carrack` | 1 | 0 | 2 | 2 | 0 | 0 |
| `polynesian_canoe` | 1 | 0 | 1 | 1 | 0 | 0 |
| `celestial_navigation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `longitude_problem` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mercator` | 1 | 0 | 1 | 0 | 0 | 0 |
| `corned_powder` | 1 | 0 | 2 | 0 | 1 | 0 |
| `cannon_cast_bronze` | 1 | 0 | 2 | 1 | 0 | 0 |
| `cannon_cast_iron` | 1 | 0 | 1 | 1 | 0 | 0 |
| `matchlock` | 1 | 0 | 1 | 1 | 0 | 0 |
| `wheellock` | 1 | 0 | 2 | 0 | 0 | 0 |
| `flintlock` | 1 | 0 | 4 | 1 | 0 | 0 |
| `trace_italienne` | 0 | 1 | 1 | 1 | 0 | 0 |
| `ship_of_line` | 1 | 0 | 2 | 1 | 0 | 0 |
| `heliocentrism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `telescope` | 1 | 0 | 2 | 1 | 0 | 0 |
| `microscope` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mechanics_newtonian` | 1 | 0 | 3 | 0 | 1 | 0 |
| `calculus` | 1 | 0 | 3 | 1 | 0 | 0 |
| `barometer_vacuum` | 1 | 0 | 2 | 0 | 0 | 0 |
| `steam_atmospheric` | 1 | 0 | 2 | 0 | 0 | 0 |
| `coke` | 2 | 0 | 2 | 0 | 2 | 0 |
| `brass_calamine` | 2 | 0 | 2 | 0 | 0 | 0 |
| `inoculation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `anatomy_dissection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `crop_rotation_norfolk` | 2 | 0 | 3 | 0 | 0 | 0 |
| `seed_drill_tull` | 1 | 0 | 2 | 0 | 0 | 0 |
| `selective_breeding` | 1 | 0 | 2 | 0 | 0 | 0 |
| `new_world_crops` | 2 | 0 | 2 | 0 | 0 | 0 |
| `glass_lead` | 1 | 0 | 2 | 0 | 0 | 0 |
| `canal_navigation` | 1 | 0 | 2 | 1 | 0 | 0 |
| `steam_separate_condenser` | 1 | 0 | 1 | 0 | 0 | 0 |
| `cylinder_boring` | 1 | 0 | 2 | 0 | 0 | 0 |
| `steam_rotary` | 1 | 0 | 2 | 0 | 1 | 0 |
| `steam_high_pressure` | 1 | 0 | 3 | 0 | 0 | 0 |
| `puddling` | 1 | 0 | 3 | 0 | 1 | 0 |
| `hot_blast` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bessemer` | 1 | 0 | 3 | 1 | 1 | 0 |
| `open_hearth` | 1 | 0 | 2 | 1 | 0 | 0 |
| `basic_process` | 1 | 0 | 2 | 0 | 0 | 0 |
| `slide_rest_lathe` | 1 | 0 | 3 | 1 | 0 | 0 |
| `interchangeable_parts` | 1 | 0 | 4 | 0 | 0 | 0 |
| `milling_machine` | 1 | 0 | 2 | 0 | 0 | 0 |
| `flying_shuttle` | 1 | 0 | 2 | 0 | 0 | 0 |
| `spinning_jenny` | 1 | 0 | 1 | 0 | 0 | 0 |
| `water_frame` | 1 | 0 | 2 | 1 | 0 | 0 |
| `spinning_mule` | 1 | 0 | 1 | 0 | 0 | 0 |
| `power_loom` | 1 | 0 | 1 | 1 | 0 | 0 |
| `cotton_gin_saw` | 1 | 0 | 1 | 0 | 0 | 0 |
| `jacquard` | 1 | 0 | 2 | 0 | 0 | 0 |
| `locomotive` | 1 | 0 | 2 | 0 | 0 | 0 |
| `railway` | 2 | 0 | 3 | 1 | 1 | 0 |
| `steamboat` | 1 | 0 | 2 | 0 | 0 | 0 |
| `screw_propeller` | 1 | 0 | 2 | 1 | 0 | 0 |
| `iron_hull` | 1 | 0 | 2 | 1 | 0 | 0 |
| `macadam` | 1 | 0 | 1 | 1 | 0 | 0 |
| `bicycle` | 1 | 0 | 2 | 0 | 0 | 0 |
| `chemistry_quantitative` | 1 | 0 | 2 | 1 | 0 | 0 |
| `sulphuric_acid` | 0 | 1 | 4 | 1 | 0 | 0 |
| `soda_leblanc` | 1 | 0 | 2 | 0 | 0 | 0 |
| `bleach_chlorine` | 1 | 0 | 1 | 0 | 0 | 0 |
| `synthetic_dye` | 1 | 0 | 2 | 0 | 0 | 0 |
| `portland_cement` | 1 | 0 | 1 | 1 | 0 | 0 |
| `reinforced_concrete` | 1 | 0 | 1 | 1 | 0 | 0 |
| `haber_bosch` | 1 | 0 | 2 | 0 | 0 | 0 |
| `dynamite` | 2 | 0 | 2 | 0 | 1 | 0 |
| `electromagnetism` | 1 | 0 | 3 | 0 | 0 | 0 |
| `telegraph` | 1 | 0 | 2 | 1 | 1 | 0 |
| `submarine_cable` | 1 | 0 | 1 | 1 | 0 | 0 |
| `dynamo` | 1 | 0 | 1 | 0 | 0 | 0 |
| `electric_light` | 1 | 0 | 2 | 0 | 0 | 0 |
| `electricity_generation` | 0 | 0 | 3 | 2 | 1 | 0 |
| `electric_motor` | 1 | 0 | 2 | 0 | 0 | 0 |
| `telephone` | 0 | 1 | 1 | 1 | 0 | 0 |
| `internal_combustion` | 1 | 0 | 3 | 0 | 1 | 0 |
| `petroleum_refining` | 0 | 1 | 2 | 1 | 0 | 0 |
| `vaccination` | 1 | 0 | 2 | 0 | 0 | 0 |
| `anaesthesia` | 1 | 0 | 1 | 0 | 0 | 0 |
| `germ_theory` | 2 | 0 | 3 | 1 | 1 | 0 |
| `antisepsis` | 2 | 0 | 2 | 0 | 0 | 0 |
| `photography` | 1 | 0 | 2 | 0 | 0 | 0 |
| `evolution` | 1 | 0 | 2 | 0 | 0 | 0 |
| `aircraft` | 1 | 0 | 3 | 3 | 0 | 0 |
| `aircraft_metal` | 1 | 0 | 2 | 0 | 0 | 0 |
| `jet_engine` | 1 | 0 | 2 | 0 | 0 | 0 |
| `steam_turbine` | 1 | 0 | 2 | 1 | 0 | 0 |
| `automobile_mass` | 1 | 0 | 5 | 0 | 0 | 0 |
| `tank` | 1 | 0 | 1 | 1 | 0 | 0 |
| `diesel` | 1 | 0 | 3 | 0 | 0 | 0 |
| `aluminium` | 0 | 1 | 2 | 0 | 0 | 0 |
| `superalloy` | 1 | 0 | 2 | 0 | 0 | 0 |
| `rocket` | 0 | 2 | 2 | 3 | 0 | 0 |
| `satellite` | 1 | 0 | 2 | 0 | 0 | 0 |
| `container_shipping` | 1 | 0 | 1 | 0 | 0 | 0 |
| `radio` | 0 | 0 | 3 | 2 | 1 | 0 |
| `vacuum_tube` | 1 | 0 | 3 | 0 | 0 | 0 |
| `radar` | 2 | 0 | 3 | 0 | 0 | 0 |
| `television` | 1 | 0 | 1 | 1 | 0 | 0 |
| `computer` | 2 | 0 | 2 | 0 | 1 | 0 |
| `transistor` | 1 | 0 | 1 | 0 | 0 | 0 |
| `integrated_circuit` | 0 | 1 | 2 | 1 | 0 | 0 |
| `microprocessor` | 1 | 0 | 2 | 0 | 0 | 0 |
| `internet` | 1 | 0 | 3 | 1 | 0 | 0 |
| `quantum_mechanics` | 1 | 0 | 3 | 0 | 0 | 0 |
| `nuclear_fission` | 2 | 0 | 2 | 0 | 0 | 0 |
| `nuclear_power` | 0 | 1 | 1 | 1 | 0 | 0 |
| `thermonuclear` | 1 | 0 | 1 | 0 | 0 | 0 |
| `antibiotic_penicillin` | 2 | 0 | 2 | 0 | 1 | 0 |
| `antibiotic_broad` | 2 | 0 | 2 | 0 | 0 | 0 |
| `antibiotic_resistance` | 2 | 0 | 1 | 0 | 0 | 0 |
| `vaccine_lab` | 2 | 0 | 1 | 0 | 0 | 0 |
| `blood_transfusion` | 1 | 0 | 1 | 0 | 0 | 0 |
| `refrigeration` | 2 | 0 | 2 | 0 | 0 | 0 |
| `canning` | 2 | 0 | 2 | 0 | 0 | 0 |
| `green_revolution` | 2 | 0 | 1 | 0 | 0 | 0 |
| `genetics_mendel` | 1 | 0 | 2 | 0 | 0 | 0 |
| `dna_structure` | 2 | 0 | 3 | 0 | 0 | 0 |
| `genetic_engineering` | 2 | 0 | 3 | 0 | 0 | 0 |
| `hormonal_contraception` | 1 | 0 | 1 | 0 | 0 | 0 |
| `xray` | 2 | 0 | 2 | 0 | 0 | 0 |
| `plastics` | 1 | 0 | 2 | 0 | 0 | 0 |
| `electric_traction` | 1 | 0 | 2 | 0 | 0 | 0 |
| `skyscraper` | 1 | 0 | 1 | 1 | 0 | 0 |
| `hydroelectric` | 1 | 0 | 2 | 1 | 0 | 0 |
| `mechanised_agriculture` | 1 | 0 | 1 | 0 | 0 | 0 |
| `numerical_control` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mobile_phone` | 1 | 0 | 1 | 1 | 0 | 0 |
| `gps` | 2 | 0 | 3 | 0 | 0 | 0 |
| `atomic_clock` | 1 | 0 | 2 | 0 | 0 | 0 |
| `solar_pv` | 0 | 1 | 2 | 1 | 0 | 0 |
| `lithium_battery` | 1 | 0 | 3 | 0 | 0 | 0 |
| `relativity` | 1 | 0 | 2 | 0 | 0 | 0 |
| `laser` | 1 | 0 | 3 | 1 | 0 | 0 |
| `fibre_optics` | 1 | 0 | 1 | 0 | 0 | 0 |
| `cmos_vlsi` | 1 | 0 | 2 | 0 | 0 | 0 |
| `superconductivity_applied` | 1 | 0 | 4 | 0 | 0 | 0 |
| `composites` | 1 | 0 | 3 | 0 | 0 | 0 |
| `gas_turbine_power` | 1 | 0 | 1 | 0 | 0 | 0 |
| `wind_turbine_modern` | 0 | 1 | 1 | 1 | 0 | 0 |
| `nuclear_fusion_research` | 1 | 0 | 2 | 0 | 0 | 0 |
| `grid_storage` | 1 | 0 | 2 | 0 | 0 | 0 |
| `smart_grid` | 1 | 0 | 2 | 0 | 0 | 0 |
| `programming_languages` | 1 | 0 | 1 | 0 | 0 | 0 |
| `operating_system` | 1 | 0 | 2 | 0 | 0 | 0 |
| `relational_database` | 1 | 0 | 2 | 0 | 0 | 0 |
| `personal_computer` | 1 | 0 | 2 | 0 | 0 | 0 |
| `public_key_crypto` | 1 | 0 | 3 | 0 | 0 | 0 |
| `gpu_parallel` | 1 | 0 | 1 | 0 | 0 | 0 |
| `machine_learning` | 1 | 0 | 2 | 0 | 0 | 0 |
| `deep_learning` | 1 | 0 | 2 | 0 | 0 | 0 |
| `large_language_models` | 2 | 0 | 2 | 0 | 0 | 0 |
| `cloud_computing` | 0 | 1 | 2 | 1 | 0 | 0 |
| `smartphone` | 2 | 0 | 2 | 0 | 0 | 0 |
| `cyber_warfare` | 1 | 0 | 2 | 0 | 0 | 0 |
| `quantum_computing` | 2 | 0 | 1 | 0 | 0 | 0 |
| `cellular_digital` | 1 | 0 | 1 | 0 | 0 | 0 |
| `crewed_spaceflight` | 1 | 0 | 1 | 0 | 0 | 0 |
| `lunar_flight` | 1 | 0 | 1 | 0 | 0 | 0 |
| `space_station` | 1 | 0 | 2 | 0 | 0 | 0 |
| `space_probe` | 1 | 0 | 1 | 0 | 0 | 0 |
| `reusable_launch` | 1 | 0 | 1 | 0 | 0 | 0 |
| `space_telescope` | 2 | 0 | 2 | 0 | 0 | 0 |
| `earth_observation` | 1 | 0 | 2 | 0 | 0 | 0 |
| `organ_transplant` | 2 | 0 | 1 | 0 | 0 | 0 |
| `chemotherapy` | 2 | 0 | 1 | 0 | 0 | 0 |
| `medical_imaging` | 2 | 0 | 1 | 0 | 0 | 0 |
| `antiviral_drugs` | 2 | 0 | 1 | 0 | 0 | 0 |
| `monoclonal_antibodies` | 1 | 0 | 2 | 0 | 0 | 0 |
| `pcr` | 1 | 0 | 2 | 0 | 0 | 0 |
| `genome_sequencing` | 2 | 0 | 2 | 0 | 0 | 0 |
| `ivf` | 1 | 0 | 1 | 0 | 0 | 0 |
| `crispr` | 2 | 0 | 1 | 0 | 0 | 0 |
| `mrna_vaccines` | 2 | 0 | 1 | 0 | 0 | 0 |
| `oral_rehydration` | 2 | 0 | 1 | 0 | 0 | 0 |
| `drip_irrigation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `precision_agriculture` | 2 | 0 | 1 | 0 | 0 | 0 |
| `desalination` | 0 | 1 | 1 | 1 | 0 | 0 |
| `catalytic_converter` | 1 | 0 | 1 | 0 | 0 | 0 |
| `weather_prediction` | 1 | 0 | 1 | 0 | 0 | 0 |
| `carbon_capture` | 1 | 0 | 1 | 0 | 0 | 0 |
| `high_speed_rail` | 1 | 0 | 1 | 0 | 0 | 0 |
| `jet_airliner` | 1 | 0 | 1 | 0 | 0 | 0 |
| `electric_vehicle` | 1 | 0 | 1 | 0 | 0 | 0 |
| `helicopter` | 1 | 0 | 2 | 1 | 0 | 0 |
| `icbm` | 1 | 0 | 1 | 1 | 0 | 0 |
| `nuclear_submarine` | 2 | 0 | 2 | 1 | 0 | 0 |
| `guided_munitions` | 2 | 0 | 1 | 0 | 0 | 0 |
| `stealth` | 1 | 0 | 1 | 1 | 0 | 0 |
| `combat_drone` | 2 | 0 | 2 | 1 | 0 | 0 |
| `missile_defence` | 2 | 0 | 1 | 0 | 0 | 0 |
| `additive_manufacturing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `nanotechnology` | 1 | 0 | 2 | 0 | 0 | 0 |
| `fusion_power` | 0 | 1 | 1 | 1 | 0 | 0 |
| `fault_tolerant_quantum` | 1 | 0 | 1 | 0 | 0 | 0 |
| `general_ai` | 2 | 0 | 1 | 0 | 0 | 0 |
| `space_solar` | 1 | 0 | 1 | 1 | 0 | 0 |
| `asteroid_mining` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fusion_propulsion` | 1 | 0 | 1 | 1 | 0 | 0 |
| `frontier_launch` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_propulsion` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_materials` | 3 | 0 | 1 | 0 | 0 | 0 |
| `frontier_energy` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_crops` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_medicine` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_computation` | 2 | 0 | 1 | 0 | 0 | 0 |
| `frontier_military` | 3 | 0 | 1 | 0 | 0 | 0 |
| `frontier_cyber` | 3 | 0 | 1 | 0 | 0 | 0 |
| `frontier_climate` | 2 | 0 | 1 | 0 | 0 | 0 |
| `motor_road` | 0 | 0 | 1 | 1 | 0 | 0 |
| `trade` | 0 | 0 | 5 | 1 | 0 | 0 |
| `cotton_cultivation` | 1 | 0 | 2 | 0 | 0 | 0 |
| `ashlar_masonry` | 1 | 0 | 2 | 1 | 0 | 0 |
| `cupellation` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mesoamerican_script` | 1 | 0 | 3 | 6 | 7 | 0 |
| `ore_prospecting` | 0 | 0 | 1 | 0 | 1 | 0 |
| `counting_rods` | 1 | 0 | 3 | 0 | 0 | 0 |
| `drawloom` | 1 | 0 | 2 | 0 | 0 | 0 |
| `traction_trebuchet` | 2 | 0 | 1 | 1 | 0 | 0 |
| `ocean_junk` | 1 | 0 | 2 | 1 | 0 | 0 |
| `percussion_drilling` | 1 | 0 | 2 | 0 | 0 | 0 |
| `rifling` | 1 | 0 | 1 | 0 | 0 | 0 |
| `physiology` | 2 | 0 | 2 | 0 | 0 | 0 |
| `optical_telegraph` | 1 | 0 | 2 | 0 | 0 | 0 |
| `mineralogy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `probability_statistics` | 1 | 0 | 2 | 0 | 0 | 0 |
| `taxonomy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `breech_loading_rifle` | 1 | 0 | 1 | 0 | 0 | 0 |
| `machine_gun` | 1 | 0 | 1 | 0 | 0 | 0 |
| `rifled_artillery` | 1 | 0 | 1 | 0 | 0 | 0 |
| `epidemiology` | 1 | 0 | 3 | 1 | 0 | 0 |
| `ac_transmission` | 1 | 0 | 2 | 0 | 0 | 0 |
| `automatic_switching` | 1 | 0 | 2 | 0 | 0 | 0 |
| `balloon` | 1 | 0 | 1 | 0 | 0 | 0 |
| `gas_engine` | 1 | 0 | 2 | 0 | 0 | 0 |
| `glider` | 1 | 0 | 2 | 0 | 0 | 0 |
| `metallography` | 1 | 0 | 2 | 0 | 0 | 0 |
| `punched_card_tabulation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sanitary_engineering` | 1 | 0 | 2 | 1 | 0 | 0 |
| `structural_iron` | 1 | 0 | 3 | 0 | 0 | 0 |
| `water_turbine` | 1 | 0 | 2 | 0 | 0 | 0 |
| `cell_theory` | 1 | 0 | 2 | 0 | 0 | 0 |
| `electromagnetic_field_theory` | 2 | 0 | 2 | 0 | 0 | 0 |
| `organic_chemistry` | 1 | 0 | 1 | 0 | 0 | 0 |
| `periodic_system` | 1 | 0 | 1 | 0 | 0 | 0 |
| `spectroscopy` | 1 | 0 | 2 | 0 | 0 | 0 |
| `stratigraphy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `thermodynamics` | 1 | 0 | 3 | 0 | 0 | 0 |
| `agricultural_chemistry` | 2 | 0 | 3 | 0 | 0 | 0 |
| `chemical_fertilizer` | 1 | 0 | 2 | 0 | 0 | 0 |
| `farm_machinery` | 2 | 0 | 2 | 0 | 0 | 0 |
| `fission_weapon` | 1 | 0 | 1 | 0 | 0 | 0 |
| `guided_missile` | 1 | 0 | 1 | 0 | 0 | 0 |
| `submarine` | 1 | 0 | 2 | 0 | 0 | 0 |
| `endocrinology` | 2 | 0 | 2 | 0 | 0 | 0 |
| `immunology` | 1 | 0 | 3 | 0 | 0 | 0 |
| `nuclear_medicine` | 2 | 0 | 1 | 0 | 0 | 0 |
| `radiation_protection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `radiotherapy` | 2 | 0 | 1 | 0 | 0 | 0 |
| `sulfonamides` | 2 | 0 | 2 | 0 | 0 | 0 |
| `communications_satellite` | 2 | 0 | 2 | 0 | 0 | 0 |
| `fast_breeder_reactor` | 1 | 0 | 1 | 0 | 0 | 0 |
| `geological_disposal` | 2 | 0 | 1 | 0 | 0 | 0 |
| `graphite_reactor` | 1 | 0 | 1 | 0 | 0 | 0 |
| `heavy_water_reactor` | 1 | 0 | 1 | 0 | 0 | 0 |
| `light_water_reactor` | 1 | 0 | 1 | 0 | 0 | 0 |
| `orbital_launch_vehicle` | 1 | 0 | 1 | 0 | 0 | 0 |
| `programmable_computer` | 2 | 0 | 2 | 0 | 0 | 0 |
| `radio_broadcasting` | 1 | 0 | 1 | 0 | 0 | 0 |
| `reprocessing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `uranium_enrichment` | 1 | 0 | 1 | 0 | 0 | 0 |
| `uranium_milling` | 1 | 0 | 2 | 0 | 0 | 0 |
| `dna_sequencing` | 2 | 0 | 1 | 0 | 0 | 0 |
| `geophysical_prospecting` | 2 | 0 | 1 | 0 | 0 | 0 |
| `nuclear_physics` | 1 | 0 | 2 | 0 | 0 | 0 |
| `radiation_detection` | 2 | 0 | 1 | 0 | 0 | 0 |
| `radioactivity` | 2 | 0 | 2 | 0 | 0 | 0 |
| `radioisotope_production` | 2 | 0 | 2 | 0 | 0 | 0 |
| `radiometric_prospecting` | 1 | 0 | 1 | 0 | 0 | 0 |
| `semiconductor_physics` | 1 | 0 | 2 | 0 | 0 | 0 |
| `scientific_plant_breeding` | 2 | 0 | 3 | 0 | 0 | 0 |
| `herbal_remedies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `grain_storage` | 2 | 0 | 1 | 0 | 0 | 0 |
| `reservoir_dam` | 1 | 0 | 1 | 0 | 0 | 0 |
| `signal_beacons` | 1 | 0 | 1 | 0 | 0 | 0 |
| `iron_farm_tools` | 2 | 0 | 1 | 0 | 1 | 0 |
| `lifting_crane` | 1 | 0 | 1 | 0 | 0 | 0 |
| `camel_caravan` | 1 | 0 | 1 | 0 | 0 | 0 |
| `codex` | 1 | 0 | 1 | 0 | 0 | 0 |
| `insula` | 1 | 0 | 1 | 0 | 0 | 0 |
| `herring_buss` | 2 | 0 | 1 | 0 | 0 | 0 |
| `three_field_rotation` | 2 | 0 | 1 | 0 | 0 | 0 |
| `coal_fuel` | 2 | 0 | 1 | 0 | 0 | 0 |
| `tinplate` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mine_pumping_engines` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sustained_yield_forestry` | 1 | 0 | 1 | 0 | 0 | 0 |
| `crucible_cast_steel` | 1 | 0 | 1 | 0 | 0 | 0 |
| `antiscorbutics` | 2 | 0 | 1 | 0 | 0 | 0 |
| `electrostatics` | 1 | 0 | 1 | 0 | 0 | 0 |
| `logarithms` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sprung_coach` | 1 | 0 | 1 | 0 | 0 | 0 |
| `steam_printing_press` | 1 | 0 | 1 | 0 | 0 | 0 |
| `balloon_frame` | 2 | 0 | 1 | 0 | 0 | 0 |
| `town_gas` | 1 | 0 | 1 | 0 | 0 | 0 |
| `galvanized_iron` | 2 | 0 | 1 | 0 | 0 | 0 |
| `quinine` | 2 | 0 | 1 | 0 | 0 | 0 |
| `metric_system` | 1 | 0 | 1 | 0 | 0 | 0 |
| `synoptic_meteorology` | 2 | 0 | 1 | 0 | 0 | 0 |
| `precast_housing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `aerial_reconnaissance` | 2 | 0 | 1 | 0 | 0 | 0 |
| `law_code` | 1 | 0 | 0 | 1 | 1 | 0 |
| `legal_code_roman` | 1 | 0 | 0 | 3 | 2 | 0 |
| `census` | 1 | 0 | 0 | 6 | 2 | 0 |
| `coined_wage` | 1 | 0 | 0 | 1 | 1 | 0 |
| `patent` | 1 | 0 | 0 | 1 | 1 | 0 |
| `joint_stock` | 2 | 0 | 0 | 2 | 1 | 0 |
| `taxation` | 0 | 0 | 5 | 0 | 1 | 0 |
| `kin_council` | 1 | 0 | 2 | 0 | 0 | 0 |
| `hereditary_rank` | 1 | 0 | 1 | 0 | 0 | 0 |
| `oath_alliance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `organized_priesthood` | 1 | 0 | 2 | 0 | 0 | 0 |
| `chiefdom` | 1 | 0 | 1 | 0 | 0 | 0 |
| `tribal_confederacy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `appointed_officials` | 1 | 0 | 1 | 1 | 0 | 0 |
| `corvee_labour` | 1 | 0 | 1 | 0 | 0 | 0 |
| `elite_council` | 1 | 0 | 1 | 0 | 0 | 0 |
| `royal_envoys` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sacral_kingship` | 2 | 0 | 1 | 0 | 0 | 0 |
| `service_tenure` | 1 | 0 | 1 | 0 | 0 | 0 |
| `succession_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sworn_treaties` | 1 | 0 | 1 | 0 | 0 | 0 |
| `city_state_oligarchy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `territorial_kingship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `citizen_assembly` | 1 | 0 | 1 | 0 | 0 | 0 |
| `elected_magistracies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `land_registry` | 1 | 0 | 1 | 0 | 0 | 0 |
| `provincial_administration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `standing_army` | 2 | 0 | 1 | 1 | 0 | 0 |
| `tax_farming` | 1 | 0 | 1 | 0 | 0 | 0 |
| `theocracy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `craft_guilds` | 1 | 0 | 1 | 1 | 0 | 0 |
| `municipal_charter` | 1 | 0 | 1 | 0 | 0 | 0 |
| `religious_toleration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `assembly_democracy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bureaucratic_empire` | 1 | 0 | 1 | 0 | 0 | 0 |
| `classical_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civil_service_examinations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `commercial_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `representative_assembly` | 1 | 0 | 1 | 0 | 0 | 0 |
| `feudal_monarchy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `merchant_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `military_rule` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bill_of_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `central_banking` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civil_police` | 1 | 0 | 1 | 0 | 0 | 0 |
| `compulsory_education` | 1 | 0 | 1 | 0 | 0 | 0 |
| `law_of_nations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `official_statistics` | 1 | 0 | 1 | 1 | 0 | 0 |
| `public_health_board` | 1 | 0 | 1 | 0 | 0 | 0 |
| `resident_embassies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `separation_of_powers` | 1 | 0 | 1 | 0 | 0 | 0 |
| `written_constitution` | 1 | 0 | 1 | 0 | 0 | 0 |
| `absolute_monarchy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `constitutional_monarchy` | 3 | 0 | 1 | 0 | 0 | 0 |
| `federal_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `presidential_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `abolition` | 1 | 0 | 1 | 0 | 0 | 0 |
| `general_staff` | 1 | 0 | 1 | 0 | 0 | 0 |
| `laws_of_war` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mass_political_party` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mass_suffrage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ministerial_responsibility` | 1 | 0 | 1 | 0 | 0 | 0 |
| `universal_conscription` | 1 | 0 | 1 | 0 | 0 | 0 |
| `parliamentary_government` | 1 | 0 | 1 | 0 | 0 | 0 |
| `central_planning` | 1 | 0 | 1 | 0 | 0 | 0 |
| `marxist_leninist_state` | 1 | 0 | 1 | 0 | 0 | 0 |
| `one_party_state` | 1 | 0 | 1 | 0 | 0 | 0 |
| `gift_exchange` | 1 | 0 | 1 | 0 | 0 | 0 |
| `oral_tradition` | 1 | 0 | 1 | 0 | 0 | 0 |
| `shamanic_ritual` | 1 | 0 | 1 | 0 | 0 | 0 |
| `war_band` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ancestor_veneration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `communal_feasts` | 2 | 0 | 1 | 0 | 0 | 0 |
| `court_patronage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `customs_tolls` | 1 | 0 | 1 | 0 | 0 | 0 |
| `debt_remission` | 1 | 0 | 1 | 0 | 0 | 0 |
| `dynastic_marriage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `family_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `planned_cities` | 1 | 0 | 1 | 0 | 0 | 0 |
| `royal_archives` | 1 | 0 | 1 | 0 | 0 | 0 |
| `scribal_schools` | 2 | 0 | 1 | 0 | 0 | 0 |
| `temple_economy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `trade_treaties` | 1 | 0 | 1 | 0 | 0 | 0 |
| `water_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `citizen_militia` | 1 | 0 | 1 | 0 | 0 | 0 |
| `daughter_colonies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mandate_doctrine` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_games` | 1 | 0 | 1 | 0 | 0 | 0 |
| `royal_post` | 1 | 0 | 1 | 0 | 0 | 0 |
| `scriptural_canon` | 1 | 0 | 1 | 0 | 0 | 0 |
| `state_navy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `building_regulation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `jurists` | 1 | 0 | 1 | 0 | 0 | 0 |
| `jury_courts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mercenary_contracts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `missionary_religion` | 1 | 0 | 1 | 0 | 0 | 0 |
| `monasticism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `philosophical_schools` | 1 | 0 | 1 | 0 | 0 | 0 |
| `political_philosophy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_granaries` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_theatre` | 1 | 0 | 1 | 0 | 0 | 0 |
| `state_academy` | 2 | 0 | 1 | 0 | 0 | 0 |
| `state_gazette` | 2 | 0 | 1 | 0 | 0 | 0 |
| `state_monopolies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `state_religion` | 1 | 0 | 1 | 0 | 0 | 0 |
| `statecraft_doctrine` | 1 | 0 | 1 | 0 | 0 | 0 |
| `tributary_relations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `steppe_empire` | 1 | 0 | 1 | 0 | 0 | 0 |
| `canon_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `common_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `elective_succession` | 1 | 0 | 1 | 0 | 0 | 0 |
| `forest_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `legal_estates` | 1 | 0 | 1 | 0 | 0 | 0 |
| `manorialism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `market_fairs` | 1 | 0 | 1 | 0 | 0 | 0 |
| `military_orders` | 1 | 0 | 1 | 0 | 0 | 0 |
| `systematic_theology` | 1 | 0 | 1 | 0 | 0 | 0 |
| `elective_monarchy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `arts_academies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `colonial_administration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `copyright` | 1 | 0 | 1 | 0 | 0 | 0 |
| `divine_right` | 1 | 0 | 1 | 0 | 0 | 0 |
| `enlightenment` | 2 | 0 | 1 | 0 | 0 | 0 |
| `humanism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mercantilism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `military_academies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `poor_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `press_licensing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_museums` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_opera` | 1 | 0 | 1 | 0 | 0 | 0 |
| `religious_reform` | 1 | 0 | 1 | 0 | 0 | 0 |
| `scientific_societies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `social_contract` | 1 | 0 | 1 | 0 | 0 | 0 |
| `state_exploration` | 2 | 0 | 1 | 0 | 0 | 0 |
| `stock_exchange` | 1 | 0 | 1 | 0 | 0 | 0 |
| `westphalian_sovereignty` | 1 | 0 | 1 | 0 | 0 | 0 |
| `antitrust` | 2 | 0 | 1 | 0 | 0 | 0 |
| `civil_code` | 1 | 0 | 1 | 0 | 0 | 0 |
| `classical_liberalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `conservatism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `cooperatives` | 1 | 0 | 1 | 0 | 0 | 0 |
| `customs_union` | 1 | 0 | 1 | 0 | 0 | 0 |
| `factory_inspection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `free_trade` | 1 | 0 | 1 | 0 | 0 | 0 |
| `great_power_congress` | 1 | 0 | 1 | 0 | 0 | 0 |
| `heritage_preservation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `income_tax` | 1 | 0 | 1 | 0 | 0 | 0 |
| `indirect_rule` | 1 | 0 | 1 | 0 | 0 | 0 |
| `international_organizations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `judicial_review` | 1 | 0 | 1 | 0 | 0 | 0 |
| `labour_unions` | 1 | 0 | 1 | 0 | 0 | 0 |
| `limited_liability` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mass_press` | 1 | 0 | 1 | 0 | 0 | 0 |
| `merit_civil_service` | 1 | 0 | 1 | 0 | 0 | 0 |
| `nationalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `nature_conservation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `popular_sovereignty` | 1 | 0 | 1 | 0 | 0 | 0 |
| `press_freedom` | 1 | 0 | 1 | 0 | 0 | 0 |
| `professional_sport` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_postal_service` | 1 | 0 | 1 | 0 | 0 | 0 |
| `referendum` | 1 | 0 | 1 | 0 | 0 | 0 |
| `research_university` | 2 | 0 | 1 | 0 | 0 | 0 |
| `secret_ballot` | 1 | 0 | 1 | 0 | 0 | 0 |
| `secular_state` | 1 | 0 | 1 | 0 | 0 | 0 |
| `settler_land_grants` | 2 | 0 | 1 | 0 | 0 | 0 |
| `socialism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `womens_suffrage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `revolutionary_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `arms_control` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civil_rights_law` | 2 | 0 | 1 | 0 | 0 | 0 |
| `climate_accords` | 1 | 0 | 1 | 0 | 0 | 0 |
| `collective_security` | 1 | 0 | 1 | 0 | 0 | 0 |
| `data_protection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `decolonization` | 2 | 0 | 1 | 0 | 0 | 0 |
| `environmental_regulation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fascism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `human_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `integrated_alliance_command` | 0 | 2 | 1 | 0 | 0 | 0 |
| `international_tribunals` | 1 | 0 | 1 | 0 | 0 | 0 |
| `internet_governance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `multilateral_trade_rules` | 1 | 0 | 1 | 0 | 0 | 0 |
| `nuclear_deterrence` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_broadcasting` | 1 | 0 | 1 | 0 | 0 | 0 |
| `science_policy` | 3 | 0 | 1 | 0 | 0 | 0 |
| `state_propaganda` | 1 | 0 | 1 | 0 | 0 | 0 |
| `total_mobilization` | 2 | 0 | 1 | 0 | 0 | 0 |
| `zoning` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fascist_state` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mortuary_rites` | 1 | 0 | 1 | 0 | 0 | 0 |
| `guest_right` | 1 | 0 | 1 | 0 | 0 | 0 |
| `initiation_rites` | 1 | 0 | 1 | 0 | 0 | 0 |
| `landscape_burning` | 1 | 0 | 1 | 0 | 0 | 0 |
| `seasonal_round` | 1 | 0 | 1 | 0 | 0 | 0 |
| `wayfinding_lore` | 2 | 0 | 1 | 0 | 0 | 0 |
| `division_of_labour` | 1 | 0 | 1 | 0 | 0 | 0 |
| `band_territories` | 1 | 0 | 1 | 0 | 0 | 0 |
| `customary_sanctions` | 1 | 0 | 1 | 0 | 0 | 0 |
| `prestige_leadership` | 1 | 0 | 1 | 0 | 0 | 0 |
| `hunt_season_leaders` | 1 | 0 | 1 | 0 | 0 | 0 |
| `seasonal_aggregation` | 2 | 0 | 1 | 0 | 0 | 0 |
| `food_sharing_norms` | 1 | 0 | 1 | 0 | 0 | 0 |
| `marriage_exchange` | 1 | 0 | 1 | 0 | 0 | 0 |
| `megalithic_monuments` | 1 | 0 | 1 | 0 | 0 | 0 |
| `barter_exchange` | 1 | 0 | 1 | 0 | 0 | 0 |
| `craft_specialists` | 1 | 0 | 1 | 0 | 0 | 0 |
| `stock_friendship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fishing_station_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `seed_corn_reserve` | 1 | 0 | 1 | 0 | 0 | 0 |
| `transhumance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `island_voyaging` | 1 | 0 | 1 | 0 | 0 | 0 |
| `village_budding` | 1 | 0 | 1 | 0 | 0 | 0 |
| `work_parties` | 1 | 0 | 1 | 0 | 0 | 0 |
| `blood_compensation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `clearance_usufruct` | 1 | 0 | 1 | 0 | 0 | 0 |
| `household_land_tenure` | 1 | 0 | 1 | 0 | 0 | 0 |
| `witnessed_agreements` | 1 | 0 | 1 | 0 | 0 | 0 |
| `first_settler_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `regalia` | 1 | 0 | 1 | 0 | 0 | 0 |
| `age_sets` | 1 | 0 | 1 | 0 | 0 | 0 |
| `chiefly_retinue` | 1 | 0 | 1 | 0 | 0 | 0 |
| `village_defences` | 1 | 0 | 1 | 0 | 0 | 0 |
| `big_man_polity` | 1 | 0 | 1 | 0 | 0 | 0 |
| `segmentary_lineage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sodalities` | 1 | 0 | 1 | 0 | 0 | 0 |
| `village_headship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bridewealth` | 1 | 0 | 1 | 0 | 0 | 0 |
| `communal_storage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `extended_household` | 1 | 0 | 1 | 0 | 0 | 0 |
| `epic_literature` | 1 | 0 | 1 | 0 | 0 | 0 |
| `pantheon_hierarchy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `interest_bearing_credit` | 1 | 0 | 1 | 0 | 0 | 0 |
| `naruqqum_partnership` | 1 | 0 | 1 | 0 | 0 | 0 |
| `court_upbringing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `frontier_fortresses` | 1 | 0 | 1 | 0 | 0 | 0 |
| `royal_expeditions` | 1 | 0 | 1 | 0 | 0 | 0 |
| `victory_stelae` | 1 | 0 | 1 | 0 | 0 | 0 |
| `herding_contracts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `hire_tariffs` | 1 | 0 | 1 | 0 | 0 | 0 |
| `karum_courts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `royal_judges` | 1 | 0 | 1 | 0 | 0 | 0 |
| `campaign_provisioning` | 1 | 0 | 1 | 0 | 0 | 0 |
| `road_houses` | 1 | 0 | 1 | 0 | 0 | 0 |
| `overkingship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `village_mayors` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ration_distribution` | 2 | 0 | 1 | 0 | 0 | 0 |
| `state_treasury` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ethical_religion` | 1 | 0 | 1 | 0 | 0 | 0 |
| `pilgrimage` | 0 | 1 | 1 | 0 | 0 | 0 |
| `banking_houses` | 1 | 0 | 1 | 0 | 0 | 0 |
| `letter_schools` | 1 | 0 | 1 | 0 | 0 | 0 |
| `water_turn_shares` | 1 | 0 | 1 | 0 | 0 | 0 |
| `state_resettlement` | 1 | 0 | 1 | 0 | 0 | 0 |
| `weekly_rest` | 1 | 0 | 1 | 0 | 0 | 0 |
| `succession_oaths` | 1 | 0 | 1 | 0 | 0 | 0 |
| `siege_engineers` | 1 | 0 | 1 | 0 | 0 | 0 |
| `tyranny` | 1 | 0 | 1 | 0 | 0 | 0 |
| `property_class_franchise` | 1 | 0 | 1 | 0 | 0 | 0 |
| `palace_library` | 1 | 0 | 1 | 0 | 0 | 0 |
| `household_registers` | 1 | 0 | 1 | 0 | 0 | 0 |
| `congregational_worship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `hegemonic_leagues` | 1 | 0 | 1 | 0 | 0 | 0 |
| `maritime_loans` | 1 | 0 | 1 | 0 | 0 | 0 |
| `rhetoric_schools` | 1 | 0 | 1 | 0 | 0 | 0 |
| `water_commission` | 1 | 0 | 1 | 0 | 0 | 0 |
| `veteran_colonies` | 2 | 0 | 1 | 0 | 0 | 0 |
| `citizenship_grants` | 1 | 0 | 1 | 0 | 0 | 0 |
| `adoptive_succession` | 1 | 0 | 1 | 0 | 0 | 0 |
| `imperial_cult` | 1 | 0 | 1 | 0 | 0 | 0 |
| `art_of_war` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_physicians` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sortition` | 1 | 0 | 1 | 0 | 0 | 0 |
| `alimenta` | 1 | 0 | 1 | 0 | 0 | 0 |
| `agronomic_treatises` | 1 | 0 | 1 | 0 | 0 | 0 |
| `chancery_rolls` | 1 | 0 | 1 | 0 | 0 | 0 |
| `exchequer_audit` | 1 | 0 | 1 | 0 | 0 | 0 |
| `parish_tithe` | 1 | 0 | 1 | 0 | 0 | 0 |
| `vernacular_literature` | 1 | 0 | 1 | 0 | 0 | 0 |
| `holy_war_doctrine` | 1 | 0 | 1 | 0 | 0 | 0 |
| `investiture_settlement` | 1 | 0 | 1 | 0 | 0 | 0 |
| `mendicant_orders` | 1 | 0 | 1 | 0 | 0 | 0 |
| `peace_of_god` | 1 | 0 | 1 | 0 | 0 | 0 |
| `safe_conduct` | 1 | 0 | 1 | 0 | 0 | 0 |
| `commenda` | 1 | 0 | 1 | 0 | 0 | 0 |
| `cathedral_madrasa_schools` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bridge_trusts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `water_boards` | 2 | 0 | 1 | 0 | 0 | 0 |
| `marches` | 1 | 0 | 1 | 0 | 0 | 0 |
| `printed_classics` | 1 | 0 | 1 | 0 | 0 | 0 |
| `apprenticeship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sharecropping` | 1 | 0 | 1 | 0 | 0 | 0 |
| `appellate_courts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `notaries` | 1 | 0 | 1 | 0 | 0 | 0 |
| `primogeniture` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ghulam_corps` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_hospitals` | 1 | 0 | 1 | 0 | 0 | 0 |
| `caliphate` | 1 | 0 | 1 | 0 | 0 | 0 |
| `village_bylaws` | 2 | 0 | 1 | 0 | 0 | 0 |
| `mutual_aid_fraternities` | 1 | 0 | 1 | 0 | 0 | 0 |
| `chartered_universities` | 2 | 0 | 1 | 0 | 0 | 0 |
| `geographic_compendia` | 1 | 0 | 1 | 0 | 0 | 0 |
| `translation_patronage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `provincial_intendants` | 1 | 0 | 1 | 0 | 0 | 0 |
| `confessionalization` | 1 | 0 | 1 | 0 | 0 | 0 |
| `revivalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `insurance_underwriting` | 1 | 0 | 1 | 0 | 0 | 0 |
| `humanist_colleges` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fire_brigades` | 1 | 0 | 1 | 0 | 0 | 0 |
| `turnpike_trusts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `reason_of_state` | 1 | 0 | 1 | 0 | 0 | 0 |
| `printed_newspapers` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bankruptcy_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `army_magazines` | 1 | 0 | 1 | 0 | 0 | 0 |
| `military_engineers` | 1 | 0 | 1 | 0 | 0 | 0 |
| `language_academies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civil_registration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `internal_toll_abolition` | 1 | 0 | 1 | 0 | 0 | 0 |
| `prefectoral_system` | 1 | 0 | 1 | 0 | 0 | 0 |
| `industrial_exhibitions` | 1 | 0 | 1 | 0 | 0 | 0 |
| `national_literature` | 1 | 0 | 1 | 0 | 0 | 0 |
| `missionary_societies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `social_christianity` | 1 | 0 | 1 | 0 | 0 | 0 |
| `consular_service` | 1 | 0 | 1 | 0 | 0 | 0 |
| `river_commissions` | 1 | 0 | 1 | 0 | 0 | 0 |
| `commodity_exchange` | 1 | 0 | 1 | 0 | 0 | 0 |
| `agricultural_colleges` | 1 | 0 | 1 | 0 | 0 | 0 |
| `medical_education_reform` | 1 | 0 | 1 | 0 | 0 | 0 |
| `normal_schools` | 1 | 0 | 1 | 0 | 0 | 0 |
| `technical_academies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `womens_higher_education` | 1 | 0 | 1 | 0 | 0 | 0 |
| `housing_acts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `municipal_waterworks` | 1 | 0 | 1 | 0 | 0 | 0 |
| `assisted_emigration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `coaling_stations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `geographical_societies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `responsible_colonial_government` | 1 | 0 | 1 | 0 | 0 | 0 |
| `imperialism_doctrine` | 1 | 0 | 1 | 0 | 0 | 0 |
| `progressivism` | 2 | 0 | 1 | 0 | 0 | 0 |
| `social_democracy` | 1 | 0 | 1 | 0 | 0 | 0 |
| `advertising_agencies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `telegraph_news_agencies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `industrial_arbitration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `labour_exchanges` | 1 | 0 | 1 | 0 | 0 | 0 |
| `minimum_wage` | 1 | 0 | 1 | 0 | 0 | 0 |
| `workmens_compensation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `administrative_courts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `married_womens_property` | 1 | 0 | 1 | 0 | 0 | 0 |
| `penitentiary_reform` | 1 | 0 | 1 | 0 | 0 | 0 |
| `trademark_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civic_religion` | 1 | 0 | 1 | 0 | 0 | 0 |
| `plebiscite` | 1 | 0 | 1 | 0 | 0 | 0 |
| `colonial_regiments` | 1 | 0 | 1 | 0 | 0 | 0 |
| `compulsory_vaccination` | 1 | 0 | 1 | 0 | 0 | 0 |
| `bonapartist_regime` | 1 | 0 | 1 | 0 | 0 | 0 |
| `municipal_corporations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `zemstvo_councils` | 1 | 0 | 1 | 0 | 0 | 0 |
| `friendly_societies` | 1 | 0 | 1 | 0 | 0 | 0 |
| `social_insurance` | 2 | 0 | 1 | 0 | 0 | 0 |
| `public_libraries` | 1 | 0 | 1 | 0 | 0 | 0 |
| `customs_automation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `executive_budget` | 1 | 0 | 1 | 0 | 0 | 0 |
| `fiscal_equalization` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_procurement` | 1 | 0 | 1 | 0 | 0 | 0 |
| `sample_surveys` | 1 | 0 | 1 | 0 | 0 | 0 |
| `value_added_tax` | 2 | 0 | 1 | 0 | 0 | 0 |
| `withholding_tax` | 1 | 0 | 1 | 0 | 0 | 0 |
| `arts_councils` | 2 | 0 | 1 | 0 | 0 | 0 |
| `cultural_institutes` | 1 | 0 | 1 | 0 | 0 | 0 |
| `film_industry` | 1 | 0 | 1 | 0 | 0 | 0 |
| `modernist_design` | 1 | 0 | 1 | 0 | 0 | 0 |
| `ecumenism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `liberation_theology` | 1 | 0 | 1 | 0 | 0 | 0 |
| `religious_political_movements` | 1 | 0 | 1 | 0 | 0 | 0 |
| `academic_exchange` | 2 | 0 | 1 | 0 | 0 | 0 |
| `air_navigation_agreements` | 1 | 0 | 1 | 0 | 0 | 0 |
| `development_lending` | 1 | 0 | 1 | 0 | 0 | 0 |
| `international_food_aid` | 2 | 0 | 1 | 0 | 0 | 0 |
| `international_standards` | 1 | 0 | 1 | 0 | 0 | 0 |
| `peacekeeping_operations` | 1 | 0 | 1 | 0 | 0 | 0 |
| `transboundary_water_treaties` | 1 | 0 | 1 | 0 | 0 | 0 |
| `crop_insurance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `demand_management` | 1 | 0 | 1 | 0 | 0 | 0 |
| `deposit_insurance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `managed_exchange_rates` | 1 | 0 | 1 | 0 | 0 | 0 |
| `microfinance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `price_support` | 1 | 0 | 1 | 0 | 0 | 0 |
| `special_economic_zones` | 1 | 0 | 1 | 0 | 0 | 0 |
| `distance_education` | 2 | 0 | 1 | 0 | 0 | 0 |
| `mass_higher_education` | 2 | 0 | 1 | 0 | 0 | 0 |
| `mass_literacy_campaigns` | 1 | 0 | 1 | 0 | 0 | 0 |
| `universal_secondary_education` | 1 | 0 | 1 | 0 | 0 | 0 |
| `community_forestry` | 2 | 0 | 1 | 0 | 0 | 0 |
| `fisheries_management` | 1 | 0 | 1 | 0 | 0 | 0 |
| `highway_programmes` | 1 | 0 | 1 | 0 | 0 | 0 |
| `new_towns` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_housing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_works_relief` | 1 | 0 | 1 | 0 | 0 | 0 |
| `river_basin_authority` | 2 | 0 | 1 | 0 | 0 | 0 |
| `rural_electrification` | 1 | 0 | 1 | 0 | 0 | 0 |
| `soil_conservation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `commonwealth_association` | 1 | 0 | 1 | 0 | 0 | 0 |
| `indigenous_land_rights` | 1 | 0 | 1 | 0 | 0 | 0 |
| `overseas_departments` | 1 | 0 | 1 | 0 | 0 | 0 |
| `states_reorganisation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civil_disobedience` | 1 | 0 | 1 | 0 | 0 | 0 |
| `developmentalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `liberal_internationalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `multiculturalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `neoliberalism` | 1 | 0 | 1 | 0 | 0 | 0 |
| `pan_movements` | 1 | 0 | 1 | 0 | 0 | 0 |
| `populism_modern` | 1 | 0 | 1 | 0 | 0 | 0 |
| `vanguard_party` | 1 | 0 | 1 | 0 | 0 | 0 |
| `famine_early_warning` | 1 | 0 | 1 | 0 | 0 | 0 |
| `public_opinion_polling` | 2 | 0 | 1 | 0 | 0 | 0 |
| `script_reform` | 2 | 0 | 1 | 0 | 0 | 0 |
| `social_media_platforms` | 1 | 0 | 1 | 0 | 0 | 0 |
| `universal_telephone_service` | 1 | 0 | 1 | 0 | 0 | 0 |
| `collective_bargaining` | 1 | 0 | 1 | 0 | 0 | 0 |
| `employment_guarantee` | 1 | 0 | 1 | 0 | 0 | 0 |
| `guest_worker_programmes` | 1 | 0 | 1 | 0 | 0 | 0 |
| `occupational_safety` | 1 | 0 | 1 | 0 | 0 | 0 |
| `unemployment_insurance` | 1 | 0 | 1 | 0 | 0 | 0 |
| `vocational_training` | 1 | 0 | 1 | 0 | 0 | 0 |
| `commercial_arbitration` | 1 | 0 | 1 | 0 | 0 | 0 |
| `consumer_protection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `equal_pay_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `land_reform` | 1 | 0 | 1 | 0 | 0 | 0 |
| `land_titling` | 1 | 0 | 1 | 0 | 0 | 0 |
| `legal_aid` | 1 | 0 | 1 | 0 | 0 | 0 |
| `patent_cooperation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `refugee_law` | 1 | 0 | 1 | 0 | 0 | 0 |
| `tenancy_protection` | 1 | 0 | 1 | 0 | 0 | 0 |
| `constitutional_court` | 2 | 0 | 1 | 0 | 0 | 0 |
| `electoral_commission` | 1 | 0 | 1 | 0 | 0 | 0 |
| `personality_cult` | 1 | 0 | 1 | 0 | 0 | 0 |
| `power_sharing` | 2 | 0 | 1 | 0 | 0 | 0 |
| `term_limits` | 1 | 0 | 1 | 0 | 0 | 0 |
| `transitional_justice` | 1 | 0 | 1 | 0 | 0 | 0 |
| `counterinsurgency` | 1 | 0 | 1 | 0 | 0 | 0 |
| `military_intelligence` | 1 | 0 | 1 | 0 | 0 | 0 |
| `strategic_stockpiles` | 1 | 0 | 1 | 0 | 0 | 0 |
| `veterans_benefits` | 1 | 0 | 1 | 0 | 0 | 0 |
| `wartime_rationing` | 1 | 0 | 1 | 0 | 0 | 0 |
| `administrative_procedure` | 1 | 0 | 1 | 0 | 0 | 0 |
| `agricultural_extension` | 1 | 0 | 1 | 0 | 0 | 0 |
| `anti_corruption_agency` | 1 | 0 | 1 | 0 | 0 | 0 |
| `digital_identity` | 1 | 0 | 1 | 0 | 0 | 0 |
| `disaster_management` | 2 | 0 | 1 | 0 | 0 | 0 |
| `e_government` | 1 | 0 | 1 | 0 | 0 | 0 |
| `freedom_of_information` | 1 | 0 | 1 | 0 | 0 | 0 |
| `land_consolidation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `road_traffic_regulation` | 0 | 1 | 1 | 0 | 0 | 0 |
| `veterinary_campaigns` | 1 | 0 | 1 | 0 | 0 | 0 |
| `child_nutrition_programmes` | 1 | 0 | 1 | 0 | 0 | 0 |
| `drug_regulation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `expanded_immunization` | 1 | 0 | 1 | 0 | 0 | 0 |
| `international_health_regulations` | 2 | 0 | 1 | 0 | 0 | 0 |
| `national_health_service` | 1 | 0 | 1 | 0 | 0 | 0 |
| `primary_health_care` | 2 | 0 | 1 | 0 | 0 | 0 |
| `clerical_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `dominant_party_system` | 1 | 0 | 1 | 0 | 0 | 0 |
| `personalist_dictatorship` | 1 | 0 | 1 | 0 | 0 | 0 |
| `semi_presidential_republic` | 1 | 0 | 1 | 0 | 0 | 0 |
| `supranational_union` | 2 | 0 | 1 | 0 | 0 | 0 |
| `democratic_decentralization` | 1 | 0 | 1 | 0 | 0 | 0 |
| `metropolitan_government` | 1 | 0 | 1 | 0 | 0 | 0 |
| `participatory_budgeting` | 1 | 0 | 1 | 0 | 0 | 0 |
| `proportional_representation` | 1 | 0 | 1 | 0 | 0 | 0 |
| `regional_devolution` | 1 | 0 | 1 | 0 | 0 | 0 |
| `reserved_seats` | 1 | 0 | 1 | 0 | 0 | 0 |
| `conditional_cash_transfers` | 2 | 0 | 1 | 0 | 0 | 0 |
| `school_meals` | 1 | 0 | 1 | 0 | 0 | 0 |
| `welfare_state` | 2 | 0 | 1 | 0 | 0 | 0 |
| `national_accounts` | 1 | 0 | 1 | 0 | 0 | 0 |
| `national_laboratories` | 1 | 0 | 1 | 0 | 0 | 0 |
| `open_access_science` | 1 | 0 | 1 | 0 | 0 | 0 |
| `operations_research` | 1 | 0 | 1 | 0 | 0 | 0 |
| `think_tanks` | 1 | 0 | 1 | 0 | 0 | 0 |
| `institutional_reform` | 1 | 0 | 1 | 0 | 0 | 0 |
| `social_policy_research` | 1 | 0 | 1 | 0 | 0 | 0 |
| `civic_participation` | 1 | 0 | 1 | 0 | 0 | 0 |
