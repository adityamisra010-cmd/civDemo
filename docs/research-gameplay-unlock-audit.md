# Research → gameplay unlock audit

**Generated** by `scripts/research-gameplay-unlock-audit.py` from the canonical content (research.json sha256 `678acbaaf54791e3…`). Do not edit by hand; CI runs `--check`. Every row is derived from content links; the only mapping in the script is per consumer type (§1), which names no node.

## 1. How a node's consequence is realized (per consumer type)

| consumer type | class | enforcing system — predicate | order kind | UI surface (AvailableActionsQuery) | tests |
|---|---|---|---|---|---|
| recipe | B | ProductionSystem — CraftingQuery.IsRecipeAvailable | none (the Crafting labour share runs it) | Production — crafts we know | ResearchUnlockPipelineTests.T03–T06 |
| activity | B | LabourActivities (labour identity; ADR-033 D1 — production unchanged) | 3 SectorAllocation (sector baseline) | Labour — the sector's activity label | ResearchUnlockPipelineTests.T01–T02 |
| trade | B | TradeArbitrageSystem — TradeQuery.CanTrade (both endpoints' settlement knowledge; SettlementKnowledge.MaskOf) | none (automatic arbitrage on connected routes once legal; a hand-built SetResearchTarget for an unavailable node is ignored) | Trade — standing capability (people do on their own) | TradeResearchUnlockTests.T01–T08 |
| tax | B | GovernanceSystem — Governance.CanLevyTax | 5 SetTaxRate | Governance — the tax edict | ResearchUnlockPipelineTests.T07, T08, T15 |
| age | B | AgeTransitionSystem — AgeQuery.CheckAdvance (milestone fact) | 7 AdvanceAge | Age — advance | AgeProgressionTests; ResearchUnlockPipelineTests.T15 |
| stage | D | ResearchSystem — ResearchQuery.StageReached (research stage opens the subtrees) | 6 SetResearchTarget | Research — available nodes | ResearchEngineTests |
| institution | D | ConstructionSystem + InstitutionsSystem — ConstructionQuery.IsProjectAvailable (founds) | 4 EnqueueConstruction | Construction — found a university | ResearchUnlockPipelineTests.T11–T15 |
| road | E | RoadDevelopmentSystem — RoadDevelopmentQuery.IsClassKnown | 8 DevelopRoads | Roads — develop | ResearchUnlockPipelineTests.T09–T10, T15 |
| building | G | ConstructionSystem — ConstructionQuery.IsProjectAvailable | 4 EnqueueConstruction | Construction — build | ConstructionAvailabilityTests; ResearchUnlockPipelineUiTests (synthetic node) |

A consequence is CANONICAL when content declares it (an entity requirement, a consumer's entity link, a requirement expression). ENFORCED means the realizing system calls the knowledge evaluator (`ResearchQuery.IsKnowledgeEligible` / `RequirementMet`) through the predicate named above; the UI lists the action only when the query does (one predicate, every caller). Unrealized entities are DEFERRED (§4).

## 2. Counts per class

Each node is counted once, under its primary class (precedence H, B, E, D, G, F, J, C, I); A counts the baseline rows of §3.

| class | name | rule | count |
|---|---|---|---|
| A | Baseline | exists with zero research (declared in content: a baseline record or a null requirement) | 25 |
| B | Research-gated gameplay capability | gates a production recipe, a labour activity, trade, the tax edict or an Age milestone | 53 |
| C | Knowledge-only / modifier | capability strings or immediate effects, no realizing link | 259 |
| D | Institution prerequisite | gates a REALIZED institution (a founding project) or the research stage | 6 |
| E | Infrastructure prerequisite | gates a road class RoadDevelopmentSystem builds | 4 |
| F | Military / unit unlock | gates a unit entity (load-validated; recruitment is M7 Battle Layer) | 30 |
| G | Application / realization | gates a building a construction project builds | 0 |
| H | Repeatable | a repeatable node (D-044 T6 / D-046 G4) | 10 |
| I | Content-only / descriptive | no capability string, no effect, no entity | 0 |
| J | Explicitly deferred | every consequence is an entity no system of this milestone realizes | 70 |
| | **nodes** | Technology 426 + Civics 6 | **432** |

Node→consequence links by consumer type: activity 10, age 41, building 15, deferred 219, institution 40, recipe 2, road 6, stage 5, tax 4, trade 1.

## 3. Baseline (class A)

| id | name | declared as | realized |
|---|---|---|---|
| `baseline.settlement_founding` | Settlement founding | baseline record | simulated |
| `baseline.exploration` | Scouting and exploration | baseline record | not simulated |
| `baseline.basic_military` | Basic military (clubmen or equivalent) | baseline record | not simulated |
| `baseline.food_gathering` | Basic food and resource gathering | baseline record | simulated |
| `baseline.construction` | Basic internal construction | baseline record | simulated |
| `baseline.migration` | Migration and resettlement | baseline record | simulated |
| `baseline.fire` | Fire: making, keeping and cooking | baseline record | not simulated |
| `baseline.primitive_hafted_tools` | Primitive tools and simple hafting | baseline record | not simulated |
| `baseline.basic_fishing` | Basic fishing (shore, net, trap and spear) | baseline record | simulated |
| `baseline.primitive_water_transport` | Primitive riverine and shore-hugging craft | baseline record | not simulated |
| `baseline.basic_shelter` | Basic shelter | baseline record | simulated |
| `baseline.basic_paths` | Basic paths and overland movement | baseline record | simulated |
| `baseline.hunting` | Hunting | baseline record | simulated |
| `sector.farming.gathering` | Gathering | sector baseline identity | simulated (LabourActivities) |
| `sector.herding_fishing.hunting_fishing` | Hunting & fishing | sector baseline identity | simulated (LabourActivities) |
| `sector.extraction.gathering_wood_stone` | Gathering wood & stone | sector baseline identity | simulated (LabourActivities) |
| `sector.crafting.crafts_toolmaking` | Crafts & toolmaking | sector baseline identity | simulated (LabourActivities) |
| `sector.construction.building` | Building | sector baseline identity | simulated (LabourActivities) |
| `building.granary` | Granary | null-requirement entity | project `granary` |
| `building.workshop` | Workshop | null-requirement entity | project `workshop` |
| `inst.burial` | Deliberate burial | null-requirement entity | no realizing system |
| `activity.fishing` | Fishing | null-requirement entity | no realizing system |
| `recipe.weaving` | Weaving | null-requirement entity | recipe `weaving` |
| `recipe.toolmaking` | Toolmaking | null-requirement entity | recipe `toolmaking` |
| `road class (no entity)` | the free baseline road class (DirtPath) | sim.json roads.classes | simulated |

## 4. Discrepancies

| type | subject | finding | resolution |
|---|---|---|---|
| fixed | `ActionSurface` | university-founding descriptors (Institutions domain) were never rendered by the action surface | FIXED (R1): they join the construction block (same EnqueueConstruction button) |
| fixed | `goods.json recipes` | E15: crafting recipes ran with no knowledge requirement — pottery and bronze before research | FIXED (R1): recipe entities + CraftingQuery; ProductionSystem enforces |
| fixed | `sim.json trade` | R2a: trade between settlements was an M4 system with no research node | FIXED (R2a): node `trade` → entity `activity.trade` → sim.json trade.entity; TradeArbitrageSystem enforces TradeQuery.CanTrade; LIVE |
| baseline-claim | `recipe.toolmaking` | declared BASELINE, but node capability strings mention `tools`: `copper_native`, `copper_smelting`, `arsenical_bronze`, `casting_closed`, `iron_bloomery`, `cast_iron` | KEPT BASELINE — ownership matrix classifies it A; D-047 left recipe gating to the Director ("no blanket gate"); the directive named pottery and bronze casting only. Director decision. |
| baseline-claim | `recipe.weaving` | declared BASELINE, but node capability strings mention `cloth`: `hide_working`, `sewing`, `loom_warp_weighted`, `dyeing`, `silk`, `fulling_mill`, `horizontal_loom`, `power_loom` | KEPT BASELINE — ownership matrix classifies it A; D-047 left recipe gating to the Director ("no blanket gate"); the directive named pottery and bronze casting only. Director decision. |
| deferred | `activity.caravans` | activity entity requires `donkey OR camel`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `activity.coastal_shipping` | activity entity requires `sail_square`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `activity.ocean_shipping` | activity entity requires `carrack OR caravel OR polynesian_canoe`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.airfield` | building entity requires `aircraft`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.bastion_fort` | building entity requires `trace_italienne`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.bathhouse` | building entity requires `aqueduct_arcade AND vault_dome`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.blast_furnace` | building entity requires `blast_furnace_water OR cast_iron`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.castle` | building entity requires `stone_fortification AND vault_dome`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.cement_works` | building entity requires `portland_cement`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.chemical_works` | building entity requires `sulphuric_acid`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.city_wall` | building entity requires `mudbrick OR stone_dry`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.data_centre` | building entity requires `cloud_computing`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.desalination_plant` | building entity requires `desalination`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.dry_dock` | building entity requires `carrack`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.fulling_mill` | building entity requires `fulling_mill`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.fusion_plant` | building entity requires `fusion_power`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.harbour` | building entity requires `plank_boat AND stone_dry`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.hospital` | building entity requires `medicine_hippocratic AND pharmacology`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.hydroelectric_dam` | building entity requires `hydroelectric`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.kiln` | building entity requires `kiln_updraft`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.library` | building entity requires `cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.lighthouse` | building entity requires `stone_fortification`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.mint` | building entity requires `coinage_electrum`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.nuclear_power_station` | building entity requires `nuclear_power`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.observatory` | building entity requires `astronomy_geometric OR astrolabe OR telescope`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.oil_press` | building entity requires `oil_press`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.paper_mill` | building entity requires `paper AND water_mill`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.power_station` | building entity requires `electricity_generation`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.printing_house` | building entity requires `printing_press OR woodblock_print`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.radio_transmitter` | building entity requires `radio`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.refinery` | building entity requires `petroleum_refining`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.research_laboratory` | building entity requires `chemistry_quantitative`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.school` | building entity requires `cuneiform OR hieroglyphic OR chinese_script OR abjad OR brahmi`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.semiconductor_fab` | building entity requires `integrated_circuit`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.skyscraper` | building entity requires `skyscraper`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.smithy` | building entity requires `copper_smelting OR iron_bloomery`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.solar_farm` | building entity requires `solar_pv`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.spaceport` | building entity requires `rocket`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.steelworks` | building entity requires `bessemer OR open_hearth`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.textile_mill` | building entity requires `water_frame OR power_loom`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.water_mill` | building entity requires `water_mill`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.wind_farm` | building entity requires `wind_turbine_modern`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.windmill` | building entity requires `windmill_post OR windmill_vertical`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.aqueduct` | infrastructure entity requires `aqueduct_channel OR aqueduct_arcade OR aqueduct_tunnel`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.basin_irrigation` | infrastructure entity requires `irrigation_basin`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.bridge` | infrastructure entity requires `bridge_stone`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.canal_irrigation` | infrastructure entity requires `irrigation_canal`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.courier_relay` | infrastructure entity requires `track_road AND horse_domestication`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.fish_weir` | infrastructure entity requires `basketry`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.internet_backbone` | infrastructure entity requires `internet`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.mobile_network` | infrastructure entity requires `mobile_phone`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.navigation_canal` | infrastructure entity requires `canal_navigation`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.power_grid` | infrastructure entity requires `electricity_generation`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.qanat` | infrastructure entity requires `qanat`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.railway` | infrastructure entity requires `railway`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.sewer` | infrastructure entity requires `vault_dome AND fired_brick`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.sewerage_system` | infrastructure entity requires `germ_theory AND reinforced_concrete`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.submarine_cable` | infrastructure entity requires `submarine_cable`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.telegraph_line` | infrastructure entity requires `telegraph`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.telephone_network` | infrastructure entity requires `telephone`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.terraces` | infrastructure entity requires `terrace`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.urban_drainage` | infrastructure entity requires `fired_brick`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.well` | infrastructure entity requires `well`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.aramaic_admin` | institution entity requires `abjad AND archive`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.archive` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script) AND stamp_seal`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.census` | institution entity requires `census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.central_bank` | institution entity requires `joint_stock AND paper_money`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.coined_wage` | institution entity requires `coined_wage`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.guild` | institution entity requires `textile_workshop AND legal_code_roman`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.historiography` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script) AND library`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.hospital` | institution entity requires `medicine_hippocratic AND pharmacology`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.joint_stock` | institution entity requires `joint_stock`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.law_code` | institution entity requires `law_code`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.legal_code_roman` | institution entity requires `legal_code_roman`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.library` | institution entity requires `archive AND scribal_school`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mass_media_broadcast` | institution entity requires `radio AND television AND mass_schooling`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mass_schooling` | institution entity requires `printing_press AND newspaper AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.military_medicine` | institution entity requires `surgery_instruments AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mint_institution` | institution entity requires `coinage_electrum AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.natural_philosophy` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script) AND astronomy_records`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.newspaper` | institution entity requires `printing_press`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.patent` | institution entity requires `patent`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.quarantine` | institution entity requires `hospital AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.scientific_society` | institution entity requires `university AND printing_press AND optics_ibn_haytham`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.scribal_school` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.standard_threads` | institution entity requires `slide_rest_lathe`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.statistics_vital` | institution entity requires `census AND calculus`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.textile_workshop` | institution entity requires `loom_warp_weighted AND archive`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.translation_movement` | institution entity requires `paper AND library`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `project.interstellar_probe` | project entity requires `fusion_propulsion`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `project.orbital_launch` | project entity requires `rocket`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `project.probe_acceleration` | project entity requires `laser AND space_solar`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `unit.aircraft_carrier` | unit entity requires `aircraft AND steam_turbine`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.archers` | unit entity requires `bow_simple`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.artillery` | unit entity requires `cannon_early OR cannon_cast_bronze OR cannon_cast_iron`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.chariot` | unit entity requires `chariot`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.combat_drone` | unit entity requires `combat_drone`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.crossbowmen` | unit entity requires `crossbow`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.heavy_cavalry` | unit entity requires `stirrup`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.helicopter` | unit entity requires `helicopter`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.horse_archers` | unit entity requires `cavalry AND composite_bow`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.icbm` | unit entity requires `icbm`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.ironclad` | unit entity requires `screw_propeller AND iron_hull`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.longbowmen` | unit entity requires `longbow`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.military_aircraft` | unit entity requires `aircraft`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.missile` | unit entity requires `rocket`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.musketeers` | unit entity requires `matchlock OR flintlock`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.nuclear_submarine` | unit entity requires `nuclear_submarine`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.ship_of_the_line` | unit entity requires `ship_of_line`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.siege_engine` | unit entity requires `siege_ram OR torsion_artillery OR trebuchet`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.slingers` | unit entity requires `sling`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.spearmen` | unit entity requires `hafting`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.stealth_aircraft` | unit entity requires `stealth`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.tank` | unit entity requires `tank`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
| deferred | `unit.war_galley` | unit entity requires `naval_ram`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |

## 5. Every node

Columns: class (primary, then all), prerequisites, knowledge (capability strings), consequences (entity or requirement → consumer; `via inst.x` when the node acts through an institution's requirement), canonical / enforced / UI.

| node | tree | age | class | prereq | knowledge | consequences | canonical | enforced | UI |
|---|---|---|---|---|---|---|---|---|---|
| `fire_making` | tech | A1 | C | — | sustained high-temperature managed hearths; heat treatment of stone and wood | — | — | n/a | — |
| `knapping_oldowan` | tech | A1 | C | — | cutting edges; butchery; bone-breaking for marrow | — | — | n/a | — |
| `knapping_levallois` | tech | A1 | C | — | standardised points for hafting | — | — | n/a | — |
| `knapping_blade` | tech | A1 | C | knapping_levallois | burins; endscrapers; backed blades; microliths | — | — | n/a | — |
| `pressure_flaking` | tech | A1 | C | — | Solutrean laurel leaves; Clovis points | — | — | n/a | — |
| `heat_treatment_stone` | tech | A1 | C | fire_making AND knapping_levallois | workable stone from poor raw material | — | — | n/a | — |
| `microlith` | tech | A1 | C | hafting | barbed arrows; sickles (later); composite knives | — | — | n/a | — |
| `ground_stone_early` | tech | A1 | B | knapping_oldowan | resharpenable ground-edge felling axe; efficient heavy woodworking | `activity.logging` → sector `extraction` → joins | yes | yes | Labour |
| `adhesive_natural` | tech | A1 | C | — | load-bearing compound adhesive for composite hafting | — | — | n/a | — |
| `birch_tar` | tech | A1 | C | fire_making AND adhesive_natural | reliable hafting adhesive; waterproofing | — | — | n/a | — |
| `hafting` | tech | A1 | F | cordage AND adhesive_natural | durable composite thrusting spear; socketed composite axe and adze | `unit.spearmen` → no realizing system | yes | deferred | — |
| `cordage` | tech | A1 | C | — | binding; snares; nets; carrying | — | — | n/a | — |
| `basketry` | tech | A1 | J | cordage | carrying; storage; sieving; fish traps | `infra.fish_weir` → no realizing system | yes | deferred | — |
| `atlatl` | tech | A1 | C | hafting | ranged hunting of large game | — | — | n/a | — |
| `bow_simple` | tech | A1 | F | cordage | ranged hunting; ranged combat | `unit.archers` → no realizing system | yes | deferred | — |
| `sling` | tech | A1 | F | cordage | ranged combat; herd protection | `unit.slingers` → no realizing system | yes | deferred | — |
| `shelter_hut` | tech | A1 | C | cordage | multi-season occupation; storage inside | — | — | n/a | — |
| `hide_working` | tech | A1 | C | knapping_levallois | clothing; containers; shelter covering | — | — | n/a | — |
| `sewing` | tech | A1 | C | hide_working AND cordage | fitted clothing; tents; bags | — | — | n/a | — |
| `raft` | tech | A1 | C | cordage | open-water crossing beyond sight of land | — | — | n/a | — |
| `dugout` | tech | A1 | C | fire_making AND ground_stone_early | lake and estuary freight by dugout; offshore fishing | — | — | n/a | — |
| `fishing_hook` | tech | A1 | C | cordage | pelagic fishing; deep-water line fishing | — | — | n/a | — |
| `grinding_stone` | tech | A1 | C | — | flour from wild grain; processed tubers | — | — | n/a | — |
| `dog_domestication` | tech | A1 | C | — | hunting assistance; guarding; later herding, traction, sled | — | — | n/a | — |
| `ochre_processing` | tech | A1 | C | grinding_stone | symbolic marking; hide treatment | — | — | n/a | — |
| `bone_tools` | tech | A1 | C | knapping_blade | harpoons; awls; needles; points | — | — | n/a | — |
| `lamp` | tech | A1 | C | fire_making | deep cave use; night work | — | — | n/a | — |
| `tally_notation` | tech | A1 | C | bone_tools | counting beyond memory; lunar reckoning (disputed) | — | — | n/a | — |
| `cereal_cultivation` | tech | A2 | B | grinding_stone | predictable harvest location; larger stands | `activity.farming` → sector `farming` → replaces<br>`age:a2_cultivation` → Age 2 milestone `a2_cultivation` (core) | yes | yes | Labour, Age |
| `cereal_domesticated` | tech | A2 | C | cereal_cultivation | reliable yield; storable surplus | — | — | n/a | — |
| `rice_wet` | tech | A2 | B | cereal_cultivation | highest yield per area of any pre-modern cereal; dense population | `activity.farming` → sector `farming` → replaces | yes | yes | Labour |
| `millet` | tech | A2 | B | cereal_cultivation | dryland agriculture without irrigation | `activity.farming` → sector `farming` → replaces | yes | yes | Labour |
| `maize` | tech | A2 | B | cereal_cultivation | New World staple; nixtamalization required for nutrition (separate node) | `activity.farming` → sector `farming` → replaces | yes | yes | Labour |
| `sorghum_pearl_millet` | tech | A2 | B | cereal_cultivation | drought-tolerant staple | `activity.farming` → sector `farming` → replaces | yes | yes | Labour |
| `root_crop` | tech | A2 | B | — | staple without cereal; no granary needed — stored in ground | `activity.farming` → sector `farming` → replaces | yes | yes | Labour |
| `legume` | tech | A2 | C | cereal_cultivation | dietary protein without meat; soil fertility (rotation, later) | — | — | n/a | — |
| `orchard` | tech | A2 | C | cereal_cultivation | oil; wine (with fermentation); dried fruit | — | — | n/a | — |
| `sickle` | tech | A2 | C | microlith | efficient cereal harvest | — | — | n/a | — |
| `digging_stick_hoe` | tech | A2 | C | hafting AND ground_stone_early | hafted-blade tillage of heavier soils; faster weeding and planting | — | — | n/a | — |
| `nixtamalization` | tech | A3 | C | maize AND pottery_open_fired | maize as sole staple without deficiency | — | — | n/a | — |
| `sheep_goat` | tech | A2 | B | dog_domestication | meat on the hoof; selective flock breeding | `activity.herding` → sector `herding_fishing` → replaces<br>`age:a2_herding` → Age 2 milestone `a2_herding` (supporting) | yes | yes | Labour, Age |
| `cattle` | tech | A2 | B | sheep_goat | large-animal traction (with yoke); dairy; dung fuel | `activity.herding` → sector `herding_fishing` → replaces | yes | yes | Labour |
| `pig` | tech | A2 | C | sheep_goat | meat without pasture | — | — | n/a | — |
| `milking` | tech | A2 | C | (sheep_goat OR cattle) AND pottery_open_fired | dairy food; sustained yield without slaughter | — | — | n/a | — |
| `wool` | tech | A2 | C | sheep_goat AND cordage | wool fibre; textile without a fibre crop | — | — | n/a | — |
| `animal_traction` | tech | A2 | C | cattle | ploughing; cart haulage; threshing by treading | — | — | n/a | — |
| `pottery_open_fired` | tech | A2 | B | basketry | boiling; storage; fermentation vessel | `age:a2_pottery` → Age 2 milestone `a2_pottery` (supporting)<br>`recipe.pottery_firing` → recipe `pottery-firing` | yes | yes | Age, Production |
| `kiln_updraft` | tech | A2 | J | pottery_open_fired AND mudbrick | high-fired ceramics; reducing atmosphere; glaze (later) | `building.kiln` → no realizing system | yes | deferred | — |
| `potters_wheel_fast` | tech | A2 | C | pottery_open_fired | pottery at scale; standardised wares (bevel-rim bowls) | — | — | n/a | — |
| `mudbrick` | tech | A2 | J | — | permanent multi-room house; storehouse; wall | `building.city_wall` → no realizing system | yes | deferred | — |
| `lime_plaster` | tech | A2 | C | mudbrick AND fire_making | waterproof floors; plastered walls; plaster statuary | — | — | n/a | — |
| `timber_frame` | tech | A2 | C | ground_stone_early AND shelter_hut | large roofed spaces; multi-family dwellings | — | — | n/a | — |
| `stone_dry` | tech | A2 | E (EJ) | ground_stone_early | durable walls; megaliths; terracing | `building.city_wall` → no realizing system<br>`building.harbour` → no realizing system<br>`infra.road_built` → road class `infra.road_built` | yes | partly (rest deferred) | Roads |
| `well` | tech | A2 | J | stone_dry | settlement away from rivers; reliable water in drought | `infra.well` → no realizing system | yes | deferred | — |
| `fermentation_grain` | tech | A2 | C | cereal_domesticated AND pottery_open_fired | beer; grain preserved as drink; bread yeast | — | — | n/a | — |
| `fermentation_fruit` | tech | A2 | C | pottery_open_fired | wine; mead | — | — | n/a | — |
| `oil_press` | tech | A2 | J | orchard AND stone_dry | cooking oil; lamp fuel; storable calories | `building.oil_press` → no realizing system | yes | deferred | — |
| `salt_extraction` | tech | A2 | C | pottery_open_fired | salting meat and fish; salt as a trade good | — | — | n/a | — |
| `flax` | tech | A2 | C | cereal_cultivation | linen fibre | — | — | n/a | — |
| `spinning_spindle` | tech | A2 | C | cordage AND (flax OR wool) | thread at volume | — | — | n/a | — |
| `loom_warp_weighted` | tech | A2 | J | spinning_spindle AND timber_frame | cloth; sailcloth (later) | `inst.guild` → no realizing system via `inst.textile_workshop`<br>`inst.textile_workshop` → no realizing system | yes | deferred | — |
| `dyeing` | tech | A2 | C | loom_warp_weighted AND ochre_processing | coloured cloth; status signalling; dye as trade good | — | — | n/a | — |
| `copper_native` | tech | A2 | C | ground_stone_early AND fire_making | metal ornament; small copper tools | — | — | n/a | — |
| `copper_smelting` | tech | A2 | J | kiln_updraft AND (copper_native OR ochre_processing) | cast copper tools; copper as trade good | `building.smithy` → no realizing system | yes | deferred | — |
| `charcoal` | tech | A2 | C | birch_tar AND timber_frame | smelting fuel; higher kiln temperatures | — | — | n/a | — |
| `casting_open` | tech | A2 | C | copper_smelting | cast copper axes | — | — | n/a | — |
| `gold_silver_native` | tech | A2 | C | copper_native | prestige goods; proto-currency | — | — | n/a | — |
| `irrigation_basin` | tech | A2 | J | cereal_cultivation | cultivation on floodplain; predictable harvest from flood | `infra.basin_irrigation` → no realizing system | yes | deferred | — |
| `irrigation_canal` | tech | A2 | J | cereal_cultivation | cultivation beyond rainfall; arable extension; salinisation (long-run consequence) | `infra.canal_irrigation` → no realizing system | yes | deferred | — |
| `terrace` | tech | A2 | J | stone_dry AND cereal_cultivation | hillside cultivation; erosion control | `infra.terraces` → no realizing system | yes | deferred | — |
| `sledge` | tech | A2 | C | timber_frame | heavy haulage without wheels | — | — | n/a | — |
| `wheel_solid` | tech | A2 | B | animal_traction AND ground_stone_early | cart; wagon | `age:a3_wheel` → Age 3 milestone `a3_wheel` (supporting) | yes | yes | Age |
| `cart` | tech | A2 | C | wheel_solid | bulk overland transport at cart range | — | — | n/a | — |
| `track_road` | tech | A2 | E (EJ) | timber_frame | trackway infrastructure class (path tier: timber causeway and stoned track); wetland causeway class | `infra.courier_relay` → no realizing system<br>`infra.road_built` → road class `infra.road_built`<br>`infra.road_track` → road class `infra.road_track` | yes | partly (rest deferred) | Roads |
| `plough_ard` | tech | A2 | C | animal_traction AND digging_stick_hoe | extensive cultivation; labour productivity ×2-3 | — | — | n/a | — |
| `token_counting` | tech | A2 | B | fire_making AND tally_notation | record of quantity without memory | `age:a3_writing` → Age 3 milestone `a3_writing` (supporting) | yes | yes | Age |
| `stamp_seal` | tech | A2 | D (DJ) | ground_stone_early AND pottery_open_fired | marking ownership; sealing stores; administrative control | `inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`stage` → research.json researchStage.requires | yes | partly (rest deferred) | Construction, Research |
| `calendar_lunar` | tech | A2 | C | tally_notation | seasonal timing; ritual calendar | — | — | n/a | — |
| `trepanation` | tech | A2 | C | knapping_blade | cranial surgery | — | — | n/a | — |
| `arsenical_bronze` | tech | A3 | B | copper_smelting | harder tools and weapons without tin trade; arsenic poisoning of smiths | `age:a3_bronze` → Age 3 milestone `a3_bronze` (core) | yes | yes | Age |
| `tin_bronze` | tech | A3 | B | copper_smelting | standardised alloy; long-distance tin trade dependency; Bronze weapons; Bronze armour | `age:a3_bronze` → Age 3 milestone `a3_bronze` (core)<br>`recipe.bronze_casting` → recipe `bronze-casting` | yes | yes | Age, Production |
| `casting_closed` | tech | A3 | C | casting_open AND tin_bronze | socketed tools; complex shapes | — | — | n/a | — |
| `lost_wax` | tech | A3 | C | casting_closed | statuary; complex vessels; fine ornament | — | — | n/a | — |
| `sheet_metal` | tech | A3 | C | tin_bronze | bronze vessels; sheet armour | — | — | n/a | — |
| `mining_shaft` | tech | A3 | B | stone_dry AND copper_smelting | ore from depth; sustained metal supply | `activity.mining` → sector `extraction` → joins | yes | yes | Labour |
| `proto_writing` | tech | A3 | B | token_counting | administrative records beyond memory | `age:a3_writing` → Age 3 milestone `a3_writing` (supporting) | yes | yes | Age |
| `cuneiform` | tech | A3 | D (DJ) | proto_writing | law; literature; letters; history | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `hieroglyphic` | tech | A3 | D (DJ) | token_counting | monumental inscription; administration | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `chinese_script` | tech | A3 | D (DJ) | tally_notation AND bone_tools | royal divination record; continuous script tradition to today | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `papyrus` | tech | A3 | J | basketry | portable documents; archives; Egyptian export monopoly | `building.library` → no realizing system | yes | deferred | — |
| `numeral_sexagesimal` | tech | A3 | C | cuneiform | multiplication; division; fractions; tables | — | — | n/a | — |
| `arithmetic_babylonian` | tech | A3 | B | numeral_sexagesimal | land measurement; tax assessment; construction calculation | `tax` → sim.json governance.taxationRequires | yes | yes | Governance |
| `surveying` | tech | A3 | B | arithmetic_babylonian OR hieroglyphic OR chinese_script | land register; taxation by area; construction layout | `tax` → sim.json governance.taxationRequires | yes | yes | Governance |
| `sail_square` | tech | A3 | J | dugout AND loom_warp_weighted | improved coastal and seagoing transport class (sail); upriver travel under sail instead of rowing; river and coastal trade at scale under sail | `activity.coastal_shipping` → no realizing system | yes | deferred | — |
| `plank_boat` | tech | A3 | J | dugout AND copper_smelting | cargo vessels; sea-going hulls | `building.harbour` → no realizing system | yes | deferred | — |
| `horse_domestication` | tech | A3 | J | cattle | riding; fast overland movement; horse traction | `infra.courier_relay` → no realizing system | yes | deferred | — |
| `donkey` | tech | A3 | J | cattle | pack transport; caravan trade | `activity.caravans` → no realizing system | yes | deferred | — |
| `camel` | tech | A3 | J | cattle | desert crossing; long-distance caravan | `activity.caravans` → no realizing system | yes | deferred | — |
| `wheel_spoked` | tech | A3 | C | wheel_solid AND casting_closed | fast light vehicles | — | — | n/a | — |
| `chariot` | tech | A3 | F | wheel_spoked AND horse_domestication | mobile archery platform; elite warfare | `unit.chariot` → no realizing system | yes | deferred | — |
| `composite_bow` | tech | A3 | F | bow_simple AND sheep_goat AND birch_tar | mounted archery; chariot archery | `unit.horse_archers` → no realizing system | yes | deferred | — |
| `fired_brick` | tech | A3 | J | kiln_updraft AND mudbrick | foundations; drains; baths; monumental work | `infra.sewer` → no realizing system<br>`infra.urban_drainage` → no realizing system | yes | deferred | — |
| `glass_glaze` | tech | A3 | C | kiln_updraft AND salt_extraction | glazed ware; faience beads | — | — | n/a | — |
| `glass_core` | tech | A3 | C | glass_glaze | glass vessels; luxury export | — | — | n/a | — |
| `standard_weights` | tech | A3 | B | token_counting AND (proto_writing OR hieroglyphic) | fair exchange; taxation by weight; silver by weight (proto-money) | `tax` → sim.json governance.taxationRequires | yes | yes | Governance |
| `calendar_civil` | tech | A3 | C | calendar_lunar AND (cuneiform OR hieroglyphic OR chinese_script) | administrative dating; Nile flood prediction (Sothic rising) | — | — | n/a | — |
| `water_clock` | tech | A3 | C | pottery_open_fired AND calendar_civil | night hours; temple timing | — | — | n/a | — |
| `medicine_recorded` | tech | A3 | C | (cuneiform OR hieroglyphic OR chinese_script) | transmissible treatment; surgical procedure record | — | — | n/a | — |
| `beekeeping` | tech | A3 | C | pottery_open_fired | honey; beeswax for casting and sealing | — | — | n/a | — |
| `iron_bloomery` | tech | A4 | B (BJ) | charcoal | iron tools and weapons; metal without long-distance trade; Iron agricultural tools; Iron fastenings | `age:a4_iron` → Age 4 milestone `a4_iron` (core)<br>`building.smithy` → no realizing system | yes | partly (rest deferred) | Age |
| `bellows` | tech | A4 | C | hide_working AND kiln_updraft | hotter furnaces; iron smelting feasible | — | — | n/a | — |
| `steel_carburized` | tech | A4 | C | iron_bloomery | hard edges; blades better than bronze | — | — | n/a | — |
| `quenching` | tech | A4 | C | steel_carburized | very hard edges | — | — | n/a | — |
| `tempering` | tech | A4 | C | quenching | tough AND hard blades; Noric steel | — | — | n/a | — |
| `wootz` | tech | A4 | C | steel_carburized AND kiln_updraft | superior blades; Damascus steel (via export) | — | — | n/a | — |
| `pattern_welding` | tech | A4 | C | tempering | composite blades | — | — | n/a | — |
| `abjad` | tech | A4 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) | cheap literacy; commercial records without a scribal class | `age:a4_alphabet` → Age 4 milestone `a4_alphabet` (supporting)<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system | yes | partly (rest deferred) | Age |
| `alphabet_vowels` | tech | A4 | C | abjad | unambiguous phonetic record; literature in vernacular | — | — | n/a | — |
| `brahmi` | tech | A4 | J | abjad | Indian literate tradition | `building.school` → no realizing system | yes | deferred | — |
| `hacksilver` | tech | A3 | C | standard_weights AND gold_silver_native | exchange without barter; value store; price in shekels | — | — | n/a | — |
| `coinage_electrum` | tech | A4 | B (BJ) | hacksilver AND stamp_seal | state-guaranteed money; mercenary pay; market exchange at volume; taxation in coin | `age:a5_coinage` → Age 5 milestone `a5_coinage` (core)<br>`building.mint` → no realizing system<br>`inst.mint_institution` → no realizing system<br>`tax` → sim.json governance.taxationRequires | yes | partly (rest deferred) | Age, Governance |
| `cavalry` | tech | A4 | F | horse_domestication | horse archery; mobile warfare; steppe raiding | `unit.horse_archers` → no realizing system | yes | deferred | — |
| `saddle` | tech | A4 | C | cavalry AND hide_working | long-distance riding; lance use (with stirrup) | — | — | n/a | — |
| `siege_ram` | tech | A4 | F | wheel_solid AND hide_working | breach walls | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `siege_tower` | tech | A4 | C | siege_ram | assault over walls | — | — | n/a | — |
| `stone_fortification` | tech | A4 | J | stone_dry AND surveying AND (tin_bronze OR iron_bloomery) | walls resisting rams; gate defence | `building.castle` → no realizing system<br>`building.lighthouse` → no realizing system | yes | deferred | — |
| `crossbow` | tech | A4 | F | bow_simple AND casting_closed | mass conscript archery | `unit.crossbowmen` → no realizing system | yes | deferred | — |
| `naval_ram` | tech | A4 | F | plank_boat AND casting_closed | naval battle; sea control | `unit.war_galley` → no realizing system | yes | deferred | — |
| `qanat` | tech | A4 | J | well AND surveying AND mining_shaft | settlement in arid zones; irrigation without river | `infra.qanat` → no realizing system | yes | deferred | — |
| `aqueduct_tunnel` | tech | A4 | J | qanat AND surveying | urban water supply under siege; settlement beyond local water | `infra.aqueduct` → no realizing system | yes | deferred | — |
| `aqueduct_channel` | tech | A4 | J | aqueduct_tunnel AND stone_dry | long-distance urban water | `infra.aqueduct` → no realizing system | yes | deferred | — |
| `arch` | tech | A3 | C | fired_brick AND surveying | spans without lintels; gates, bridges, vaults | — | — | n/a | — |
| `astronomy_records` | tech | A4 | J | arithmetic_babylonian OR ((hieroglyphic OR chinese_script) AND calendar_lunar) | eclipse prediction (Saros); planetary periods; the empirical base for Greek astronomy | `inst.natural_philosophy` → no realizing system | yes | deferred | — |
| `calendar_lunisolar` | tech | A4 | C | astronomy_records | reliable religious and agricultural calendar | — | — | n/a | — |
| `geometry_practical` | tech | A4 | C | surveying AND (cuneiform OR hieroglyphic OR chinese_script) | construction calculation; navigation by geometry | — | — | n/a | — |
| `medicine_hippocratic` | tech | A4 | D (DGJ) | medicine_recorded | trained physicians; surgery (Sushruta); pharmacopoeia | `building.hospital` → no realizing system<br>`building.university` → project `military university`<br>`building.university` → project `medical university`<br>`building.university` → project `engineering university`<br>`building.university` → project `natural science university`<br>`building.university` → project `agricultural university`<br>`inst.hospital` → no realizing system<br>`inst.quarantine` → no realizing system via `inst.hospital`<br>`stage` → research.json researchStage.requires | yes | partly (rest deferred) | Construction, Research |
| `glass_blown_precursor` | tech | A4 | C | glass_core | cast bowls; mosaic glass | — | — | n/a | — |
| `rotary_quern` | tech | A4 | C | grinding_stone AND wheel_solid | milling productivity ×3 | — | — | n/a | — |
| `olive_press_beam` | tech | A4 | C | oil_press AND stone_dry | oil at export scale | — | — | n/a | — |
| `geometry_axiomatic` | tech | A5 | B (BDG) | geometry_practical | proof; rigorous engineering calculation; transmissible without a teacher | `age:a5_geometry` → Age 5 milestone `a5_geometry` (supporting)<br>`building.university` → project `military university`<br>`building.university` → project `medical university`<br>`building.university` → project `engineering university`<br>`building.university` → project `natural science university`<br>`building.university` → project `agricultural university`<br>`stage` → research.json researchStage.requires | yes | yes | Age, Construction, Research |
| `mechanics_archimedean` | tech | A5 | C | geometry_axiomatic | compound pulley; screw press; water screw; calculated siege engines | — | — | n/a | — |
| `torsion_artillery` | tech | A5 | F | crossbow | long-range bombardment; siege by machine | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `water_screw` | tech | A5 | C | mechanics_archimedean | lift irrigation; mine drainage | — | — | n/a | — |
| `noria` | tech | A5 | C | wheel_solid AND irrigation_canal | irrigation above river level; arable extension | — | — | n/a | — |
| `water_mill` | tech | A5 | B (BJ) | rotary_quern | milling without human labour; the energy resource type of ADR-019 §5 | `age:a6_mills` → Age 6 milestone `a6_mills` (supporting)<br>`building.paper_mill` → no realizing system<br>`building.water_mill` → no realizing system | yes | partly (rest deferred) | Age |
| `trip_hammer` | tech | A5 | C | water_mill | mechanised processing; ore crushing | — | — | n/a | — |
| `gearing` | tech | A5 | C | mechanics_archimedean AND casting_closed | astronomical calculators; mill gearing; clocks (later) | — | — | n/a | — |
| `concrete_pozzolan` | tech | A5 | B | lime_plaster AND arch | domes; harbour moles; vaults at scale | `age:a5_concrete` → Age 5 milestone `a5_concrete` (supporting) | yes | yes | Age |
| `vault_dome` | tech | A5 | J | arch AND concrete_pozzolan | baths; basilicas; large public interiors | `building.bathhouse` → no realizing system<br>`building.castle` → no realizing system<br>`infra.sewer` → no realizing system | yes | deferred | — |
| `road_paved` | tech | A5 | B (BE) | surveying | engineered paved road class: all-weather bulk transport; engineered paved road class: military mobility | `age:a5_roads` → Age 5 milestone `a5_roads` (supporting)<br>`infra.road_paved` → road class `infra.road_paved` | yes | yes | Age, Roads |
| `bridge_stone` | tech | A5 | J | arch AND concrete_pozzolan | permanent river crossing; road network integrity | `infra.bridge` → no realizing system | yes | deferred | — |
| `aqueduct_arcade` | tech | A5 | J | aqueduct_channel AND arch AND surveying | city of a million; baths; fountains | `building.bathhouse` → no realizing system<br>`infra.aqueduct` → no realizing system | yes | deferred | — |
| `glass_blowing` | tech | A5 | C | glass_blown_precursor AND iron_bloomery | mass glassware; window glass (later) | — | — | n/a | — |
| `window_glass` | tech | A5 | C | glass_blowing | glazed windows; baths, villas | — | — | n/a | — |
| `cast_iron` | tech | A5 | J | iron_bloomery AND bellows AND kiln_updraft | mass-cast tools; cast cookware; cast ploughshares | `building.blast_furnace` → no realizing system | yes | deferred | — |
| `iron_mouldboard` | tech | A5 | C | cast_iron AND plough_ard | heavy soil cultivation; weed burial | — | — | n/a | — |
| `seed_drill` | tech | A5 | C | plough_ard AND cast_iron | seed economy; row cultivation; weeding between rows | — | — | n/a | — |
| `collar_harness` | tech | A5 | C | horse_domestication AND hide_working | horse haulage; horse ploughing (later) | — | — | n/a | — |
| `paper` | tech | A5 | B (BJ) | cordage | cheap records; books; bureaucratic scale | `age:a6_paper` → Age 6 milestone `a6_paper` (supporting)<br>`building.library` → no realizing system<br>`building.paper_mill` → no realizing system<br>`inst.translation_movement` → no realizing system | yes | partly (rest deferred) | Age |
| `crossbow_repeating` | tech | A5 | C | crossbow | rapid fire at short range | — | — | n/a | — |
| `stirrup` | tech | A5 | F | saddle AND iron_bloomery | lance charge; heavy cavalry | `unit.heavy_cavalry` → no realizing system | yes | deferred | — |
| `silk` | tech | A3 | C | loom_warp_weighted AND orchard | silk cloth; the Silk Road export | — | — | n/a | — |
| `lacquer` | tech | A2 | C | fermentation_fruit | waterproof vessels; luxury goods | — | — | n/a | — |
| `astronomy_geometric` | tech | A5 | J | geometry_axiomatic AND astronomy_records | planetary prediction; star catalogue; the base for all astronomy until Kepler | `building.observatory` → no realizing system | yes | deferred | — |
| `cartography` | tech | A5 | C | astronomy_geometric AND road_paved | maps as knowledge; D-040 B1 computed extent made explicit | — | — | n/a | — |
| `latitude_sailing` | tech | A4 | C | astronomy_records OR calendar_lunar | open-sea voyaging by latitude | — | — | n/a | — |
| `monsoon_sailing` | tech | A5 | C | sail_square AND astronomy_records | direct Arabia–India crossing; Indian Ocean trade | — | — | n/a | — |
| `lateen` | tech | A5 | C | sail_square | windward sailing; coastal trade against wind | — | — | n/a | — |
| `mortise_hull` | tech | A3 | C | plank_boat | large cargo ships; grain fleet (Rome fed by Egypt) | — | — | n/a | — |
| `pharmacology` | tech | A5 | J | medicine_hippocratic | transmissible drug knowledge; herbal trade | `building.hospital` → no realizing system<br>`inst.hospital` → no realizing system<br>`inst.quarantine` → no realizing system via `inst.hospital` | yes | deferred | — |
| `surgery_instruments` | tech | A5 | J | medicine_hippocratic AND casting_closed | cataract surgery; trauma surgery; military medicine | `inst.military_medicine` → no realizing system | yes | deferred | — |
| `abacus` | tech | A5 | C | arithmetic_babylonian OR chinese_script | commercial arithmetic without literacy | — | — | n/a | — |
| `alchemy` | tech | A5 | C | copper_smelting AND (chinese_script OR alphabet_vowels) | distillation; mineral acids (later); gunpowder (China, later) | — | — | n/a | — |
| `distillation` | tech | A5 | C | alchemy AND glass_blowing | essential oils; rose water; alcohol (Islamic, ~800 CE) | — | — | n/a | — |
| `heavy_plough` | tech | A6 | B | iron_mouldboard AND wheel_solid | cultivation of heavy soils; urbanisation of the north | `age:a6_heavy_plough` → Age 6 milestone `a6_heavy_plough` (core) | yes | yes | Age |
| `horse_collar` | tech | A6 | C | collar_harness | horse-drawn plough; horse haulage at oxen load, twice the speed | — | — | n/a | — |
| `horseshoe` | tech | A6 | C | iron_bloomery AND horse_collar | sustained horse use on roads | — | — | n/a | — |
| `rice_champa` | tech | A6 | C | rice_wet | two harvests per year; population from 60M to 120M | — | — | n/a | — |
| `sugar_refining` | tech | A6 | C | fermentation_fruit AND distillation | storable sweetener; plantation economy (later) | — | — | n/a | — |
| `windmill_vertical` | tech | A6 | J | water_mill | milling in windy arid land | `building.windmill` → no realizing system | yes | deferred | — |
| `windmill_post` | tech | A6 | B (BJ) | water_mill AND sail_square | power without river head; drainage (Netherlands) | `age:a6_mills` → Age 6 milestone `a6_mills` (supporting)<br>`building.windmill` → no realizing system | yes | partly (rest deferred) | Age |
| `fulling_mill` | tech | A6 | J | trip_hammer AND loom_warp_weighted | cloth finishing at scale; relocation of textile industry to rivers | `building.fulling_mill` → no realizing system | yes | deferred | — |
| `spinning_wheel` | tech | A6 | C | spinning_spindle AND wheel_solid | thread supply keeping pace with looms | — | — | n/a | — |
| `horizontal_loom` | tech | A6 | C | loom_warp_weighted AND gearing | professional weaving; guild cloth industry | — | — | n/a | — |
| `cotton_gin_roller` | tech | A6 | C | wheel_solid | cotton as a mass fibre | — | — | n/a | — |
| `compass_magnetic` | tech | A6 | C | iron_bloomery | navigation under cloud; dead reckoning | — | — | n/a | — |
| `stern_rudder` | tech | A6 | C | mortise_hull OR plank_boat | larger ships; precise steering | — | — | n/a | — |
| `watertight_bulkhead` | tech | A6 | C | stern_rudder | survivable ocean hulls | — | — | n/a | — |
| `portolan` | tech | A6 | C | compass_magnetic AND cartography | coastal navigation by bearing and distance | — | — | n/a | — |
| `astrolabe` | tech | A6 | J | astronomy_geometric AND gearing | latitude; prayer times; surveying | `building.observatory` → no realizing system | yes | deferred | — |
| `cog` | tech | A6 | C | stern_rudder AND plank_boat | bulk grain and timber trade in the Baltic and North Sea | — | — | n/a | — |
| `gunpowder` | tech | A6 | C | alchemy | incendiaries; fire lances; bombs | — | — | n/a | — |
| `saltpetre_refining` | tech | A6 | C | gunpowder | reliable high-nitrate powder | — | — | n/a | — |
| `cannon_early` | tech | A6 | F | gunpowder AND cast_iron | projectile artillery (weak at first) | `unit.artillery` → no realizing system | yes | deferred | — |
| `trebuchet` | tech | A6 | F | torsion_artillery | reduction of stone castles | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `longbow` | tech | A6 | F | bow_simple | massed infantry archery | `unit.longbowmen` → no realizing system | yes | deferred | — |
| `plate_armour` | tech | A6 | C | tempering AND sheet_metal | near-immunity to arrows and blades | — | — | n/a | — |
| `greek_fire` | tech | A6 | C | distillation | naval incendiary | — | — | n/a | — |
| `woodblock_print` | tech | A6 | J | paper AND stamp_seal | mass religious texts; paper money (Song); printed calendars | `building.printing_house` → no realizing system | yes | deferred | — |
| `movable_type_ceramic` | tech | A6 | C | woodblock_print AND kiln_updraft | reusable type (limited by character count) | — | — | n/a | — |
| `movable_type_metal` | tech | A6 | C | movable_type_ceramic AND casting_closed | durable reusable type | — | — | n/a | — |
| `paper_money` | tech | A6 | J | woodblock_print AND coinage_electrum | money not limited by metal; inflation (Yuan) | `inst.central_bank` → no realizing system | yes | deferred | — |
| `hindu_arabic` | tech | A6 | B | brahmi | written arithmetic; algebra; commercial calculation | `age:a6_numerals` → Age 6 milestone `a6_numerals` (supporting) | yes | yes | Age |
| `algebra` | tech | A6 | D (DG) | hindu_arabic AND geometry_axiomatic | inheritance calculation; engineering; later: all quantitative science | `building.university` → project `military university`<br>`building.university` → project `medical university`<br>`building.university` → project `engineering university`<br>`building.university` → project `natural science university`<br>`building.university` → project `agricultural university`<br>`stage` → research.json researchStage.requires | yes | yes | Construction, Research |
| `trigonometry` | tech | A6 | C | astronomy_geometric AND hindu_arabic | astronomical tables; navigation (later); surveying | — | — | n/a | — |
| `optics_ibn_haytham` | tech | A6 | J | geometry_axiomatic AND glass_blowing | theory of vision; experimental method as a stated procedure | `inst.scientific_society` → no realizing system | yes | deferred | — |
| `spectacles` | tech | A6 | C | optics_ibn_haytham AND window_glass | corrected vision; telescope (later) | — | — | n/a | — |
| `crank_connecting_rod` | tech | A6 | C | gearing AND water_mill | sawmills; pumps; bellows | — | — | n/a | — |
| `escapement_verge` | tech | A6 | C | gearing AND crank_connecting_rod | uniform hours; the precision-mechanism craft tradition | — | — | n/a | — |
| `blast_furnace_water` | tech | A6 | J | iron_bloomery AND crank_connecting_rod AND charcoal | cast iron in Europe; cannon casting | `building.blast_furnace` → no realizing system | yes | deferred | — |
| `wire_drawing` | tech | A6 | C | iron_bloomery AND crank_connecting_rod | mail armour; pins; cards for wool | — | — | n/a | — |
| `canal_lock` | tech | A6 | C | irrigation_canal AND mortise_hull | navigable canals over hills; Grand Canal at full function | — | — | n/a | — |
| `double_entry` | tech | A6 | B | hindu_arabic AND paper AND bill_of_exchange | auditable accounts; capital as a measured quantity; banking (next) | `age:a6_finance` → Age 6 milestone `a6_finance` (supporting) | yes | yes | Age |
| `bill_of_exchange` | tech | A6 | B | coinage_electrum AND (abjad OR chinese_script) | long-distance payment; credit; the merchant network as infrastructure | `age:a6_finance` → Age 6 milestone `a6_finance` (supporting) | yes | yes | Age |
| `printing_press` | tech | A7 | B (BJ) | casting_closed AND mechanics_archimedean AND paper | books at 1/100 the cost; the public sphere | `age:a7_printing` → Age 7 milestone `a7_printing` (core)<br>`building.printing_house` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system via `inst.mass_schooling`<br>`inst.mass_media_broadcast` → no realizing system via `inst.newspaper`<br>`inst.mass_schooling` → no realizing system<br>`inst.mass_schooling` → no realizing system via `inst.newspaper`<br>`inst.newspaper` → no realizing system<br>`inst.scientific_society` → no realizing system | yes | partly (rest deferred) | Age |
| `type_founding` | tech | A7 | C | printing_press | standardised parts at scale | — | — | n/a | — |
| `caravel` | tech | A7 | B (BJ) | lateen AND stern_rudder AND compass_magnetic | coastal exploration against wind; Atlantic islands; West Africa | `activity.ocean_shipping` → no realizing system<br>`age:a7_ocean` → Age 7 milestone `a7_ocean` (supporting) | yes | partly (rest deferred) | Age |
| `carrack` | tech | A7 | J | caravel AND cog | transoceanic cargo; Columbus, da Gama | `activity.ocean_shipping` → no realizing system<br>`building.dry_dock` → no realizing system | yes | deferred | — |
| `polynesian_canoe` | tech | A6 | J | dugout AND latitude_sailing | open-ocean colonisation over 3,000 km | `activity.ocean_shipping` → no realizing system | yes | deferred | — |
| `celestial_navigation` | tech | A7 | C | astrolabe AND trigonometry AND caravel | open-ocean position finding | — | — | n/a | — |
| `longitude_problem` | tech | A7 | C | celestial_navigation AND escapement_verge | east-west position at sea; safe ocean routing | — | — | n/a | — |
| `mercator` | tech | A7 | C | cartography AND portolan AND trigonometry | practical ocean charts | — | — | n/a | — |
| `corned_powder` | tech | A7 | B | saltpetre_refining | reliable cannon; handguns | `age:a7_powder` → Age 7 milestone `a7_powder` (supporting) | yes | yes | Age |
| `cannon_cast_bronze` | tech | A7 | F | cannon_early AND lost_wax | breaching stone walls; end of the castle | `unit.artillery` → no realizing system | yes | deferred | — |
| `cannon_cast_iron` | tech | A7 | F | cannon_cast_bronze AND blast_furnace_water | cheap naval and siege guns | `unit.artillery` → no realizing system | yes | deferred | — |
| `matchlock` | tech | A7 | F | cannon_early AND crossbow | massed infantry fire | `unit.musketeers` → no realizing system | yes | deferred | — |
| `wheellock` | tech | A7 | C | matchlock AND escapement_verge | pistol; mounted firearms | — | — | n/a | — |
| `flintlock` | tech | A7 | F | wheellock | universal infantry musket; bayonet infantry (with next) | `unit.musketeers` → no realizing system | yes | deferred | — |
| `trace_italienne` | tech | A7 | J | cannon_cast_bronze AND geometry_axiomatic | cannon-resistant fortification | `building.bastion_fort` → no realizing system | yes | deferred | — |
| `ship_of_line` | tech | A7 | F | carrack AND cannon_cast_iron | sea control; naval blockade | `unit.ship_of_the_line` → no realizing system | yes | deferred | — |
| `heliocentrism` | tech | A7 | C | astronomy_geometric AND trigonometry | accurate planetary prediction | — | — | n/a | — |
| `telescope` | tech | A7 | J | spectacles | Jupiter's moons; naval reconnaissance (D-039 B) | `building.observatory` → no realizing system | yes | deferred | — |
| `microscope` | tech | A7 | C | spectacles | cells; microorganisms | — | — | n/a | — |
| `mechanics_newtonian` | tech | A7 | B | heliocentrism AND algebra AND calculus | engineering from first principles; ballistics; celestial mechanics | `age:a7_science` → Age 7 milestone `a7_science` (supporting) | yes | yes | Age |
| `calculus` | tech | A7 | J | algebra AND geometry_axiomatic | physics; engineering; economics (later) | `inst.statistics_vital` → no realizing system | yes | deferred | — |
| `barometer_vacuum` | tech | A7 | C | glass_blowing AND mechanics_archimedean | weather prediction; the concept behind the atmospheric engine | — | — | n/a | — |
| `steam_atmospheric` | tech | A7 | C | barometer_vacuum AND cast_iron AND cannon_cast_iron | mine drainage; coal mining at depth | — | — | n/a | — |
| `coke` | tech | A7 | C | blast_furnace_water | iron at industrial scale; cheap cast iron | — | — | n/a | — |
| `brass_calamine` | tech | A5 | C | copper_smelting | scientific instruments; precision parts | — | — | n/a | — |
| `inoculation` | tech | A7 | C | medicine_hippocratic | immunisation | — | — | n/a | — |
| `anatomy_dissection` | tech | A7 | C | medicine_hippocratic AND printing_press | accurate surgery; circulation (Harvey 1628) | — | — | n/a | — |
| `crop_rotation_norfolk` | tech | A7 | C | legume AND cereal_domesticated | yield +50%; livestock doubled; labour freed for industry | — | — | n/a | — |
| `seed_drill_tull` | tech | A7 | C | seed_drill OR (crop_rotation_norfolk AND horse_collar) | seed economy; horse-hoeing | — | — | n/a | — |
| `selective_breeding` | tech | A7 | C | crop_rotation_norfolk | larger animals; faster maturation | — | — | n/a | — |
| `new_world_crops` | tech | A7 | C | carrack AND (maize OR root_crop) | European population doubling (potato); Chinese highland farming (maize, sweet potato) | — | — | n/a | — |
| `glass_lead` | tech | A7 | C | glass_blowing | optical glass; fine tableware | — | — | n/a | — |
| `canal_navigation` | tech | A7 | J | canal_lock AND surveying | bulk inland transport at 1/4 road cost; coal to cities | `infra.navigation_canal` → no realizing system | yes | deferred | — |
| `steam_separate_condenser` | tech | A8 | C | steam_atmospheric AND barometer_vacuum | economic steam power away from coalfields | — | — | n/a | — |
| `cylinder_boring` | tech | A8 | C | cannon_cast_iron AND water_mill | steam cylinders that seal; the machine-tool tradition | — | — | n/a | — |
| `steam_rotary` | tech | A8 | B | steam_separate_condenser AND cylinder_boring AND crank_connecting_rod | factory power anywhere; mills off the rivers | `age:a8_steam` → Age 8 milestone `a8_steam` (core) | yes | yes | Age |
| `steam_high_pressure` | tech | A8 | C | steam_rotary AND puddling | locomotive; steamboat; portable engine | — | — | n/a | — |
| `puddling` | tech | A8 | B | coke AND steam_rotary | wrought iron ×15 cheaper; rails; structural iron | `age:a8_iron` → Age 8 milestone `a8_iron` (supporting) | yes | yes | Age |
| `hot_blast` | tech | A8 | C | coke | iron on poor coal | — | — | n/a | — |
| `bessemer` | tech | A8 | J | puddling | steel rails; structural steel; steel ships | `building.steelworks` → no realizing system | yes | deferred | — |
| `open_hearth` | tech | A8 | J | bessemer AND hot_blast | quality steel; scrap recycling | `building.steelworks` → no realizing system | yes | deferred | — |
| `basic_process` | tech | A8 | C | bessemer | steel from any ore; German industrial rise | — | — | n/a | — |
| `slide_rest_lathe` | tech | A8 | J | cylinder_boring AND escapement_verge | precise screws; standard threads; every later machine | `inst.standard_threads` → no realizing system | yes | deferred | — |
| `interchangeable_parts` | tech | A8 | C | slide_rest_lathe AND type_founding | mass production; field repair | — | — | n/a | — |
| `milling_machine` | tech | A8 | C | slide_rest_lathe | gun parts; gears at scale | — | — | n/a | — |
| `flying_shuttle` | tech | A8 | C | horizontal_loom | faster weaving; demand pressure on spinning | — | — | n/a | — |
| `spinning_jenny` | tech | A8 | C | spinning_wheel AND flying_shuttle | thread ×8 per worker | — | — | n/a | — |
| `water_frame` | tech | A8 | J | spinning_jenny AND water_mill | cotton thread at industrial scale; the factory system | `building.textile_mill` → no realizing system | yes | deferred | — |
| `spinning_mule` | tech | A8 | C | water_frame | fine cotton competitive with India | — | — | n/a | — |
| `power_loom` | tech | A8 | J | spinning_mule AND steam_rotary | cloth at industrial scale | `building.textile_mill` → no realizing system | yes | deferred | — |
| `cotton_gin_saw` | tech | A8 | C | cotton_gin_roller AND wire_drawing | short-staple cotton economic | — | — | n/a | — |
| `jacquard` | tech | A8 | C | horizontal_loom AND paper | complex patterns without a drawboy; stored program (concept) | — | — | n/a | — |
| `locomotive` | tech | A8 | C | steam_high_pressure | overland speed; bulk overland transport | — | — | n/a | — |
| `railway` | tech | A8 | B (BJ) | locomotive AND puddling AND surveying | national markets; standard time; military mobilisation by rail | `age:a8_rail` → Age 8 milestone `a8_rail` (supporting)<br>`infra.railway` → no realizing system | yes | partly (rest deferred) | Age |
| `steamboat` | tech | A8 | C | steam_rotary AND mortise_hull | river commerce against current; scheduled ocean crossing | — | — | n/a | — |
| `screw_propeller` | tech | A8 | F | steamboat AND water_screw | ocean steamships; steam warships | `unit.ironclad` → no realizing system | yes | deferred | — |
| `iron_hull` | tech | A8 | F | puddling AND screw_propeller | ships beyond timber limits; ironclads | `unit.ironclad` → no realizing system | yes | deferred | — |
| `macadam` | tech | A8 | E | road_paved | macadamised road class: compacted graded stone, all-weather coaching | `infra.road_macadam` → road class `infra.road_macadam` | yes | yes | Roads |
| `bicycle` | tech | A8 | C | steel_carburized | personal transport; the precision-parts industry that built cars and aircraft | — | — | n/a | — |
| `chemistry_quantitative` | tech | A8 | J | distillation AND barometer_vacuum | predictable chemical process; all later industrial chemistry | `building.research_laboratory` → no realizing system | yes | deferred | — |
| `sulphuric_acid` | tech | A8 | J | distillation AND glass_lead | bleaching; soda; fertiliser; explosives | `building.chemical_works` → no realizing system | yes | deferred | — |
| `soda_leblanc` | tech | A8 | C | sulphuric_acid AND chemistry_quantitative | glass and soap at scale; pollution (the Alkali Act 1863) | — | — | n/a | — |
| `bleach_chlorine` | tech | A8 | C | sulphuric_acid | cotton finishing at industrial pace | — | — | n/a | — |
| `synthetic_dye` | tech | A8 | C | chemistry_quantitative AND coke | cheap colour; the corporate research laboratory | — | — | n/a | — |
| `portland_cement` | tech | A8 | J | concrete_pozzolan AND kiln_updraft AND chemistry_quantitative | concrete everywhere | `building.cement_works` → no realizing system | yes | deferred | — |
| `reinforced_concrete` | tech | A8 | J | portland_cement AND bessemer | frames, bridges, high-rise | `infra.sewerage_system` → no realizing system | yes | deferred | — |
| `haber_bosch` | tech | A8 | C | chemistry_quantitative AND open_hearth AND electricity_generation | yield ceiling raised ~2×; population beyond organic limit | — | — | n/a | — |
| `dynamite` | tech | A8 | C | sulphuric_acid AND chemistry_quantitative | hard-rock excavation at scale; Alpine tunnels | — | — | n/a | — |
| `electromagnetism` | tech | A8 | C | calculus | motor; generator; telegraph; radio (theory) | — | — | n/a | — |
| `telegraph` | tech | A8 | B (BJ) | electromagnetism AND wire_drawing | near-instant long-distance messages; D-039 A3 order latency collapse | `age:a8_telegraph` → Age 8 milestone `a8_telegraph` (supporting)<br>`infra.telegraph_line` → no realizing system | yes | partly (rest deferred) | Age |
| `submarine_cable` | tech | A8 | J | telegraph AND screw_propeller | global information network | `infra.submarine_cable` → no realizing system | yes | deferred | — |
| `dynamo` | tech | A8 | C | electromagnetism AND steam_rotary | electricity as a produced CAPACITY | — | — | n/a | — |
| `electric_light` | tech | A8 | C | dynamo AND barometer_vacuum | night work; urban lighting demand | — | — | n/a | — |
| `electricity_generation` | tech | A8 | B (BJ) | dynamo AND electric_light AND electromagnetism | electricity as CAPACITY (ADR-019 §5); electric motors; all of the 20th century | `age:a9_electricity` → Age 9 milestone `a9_electricity` (core)<br>`building.power_station` → no realizing system<br>`infra.power_grid` → no realizing system | yes | partly (rest deferred) | Age |
| `electric_motor` | tech | A8 | C | electricity_generation | unit drive; electric traction | — | — | n/a | — |
| `telephone` | tech | A8 | J | telegraph | real-time conversation at distance | `infra.telephone_network` → no realizing system | yes | deferred | — |
| `internal_combustion` | tech | A8 | B | milling_machine AND steam_rotary AND chemistry_quantitative | motor vehicle; aircraft (next); tank (next) | `age:a9_combustion` → Age 9 milestone `a9_combustion` (core) | yes | yes | Age |
| `petroleum_refining` | tech | A8 | J | distillation AND mining_shaft | lamp oil; fuel for ICE | `building.refinery` → no realizing system | yes | deferred | — |
| `vaccination` | tech | A8 | C | inoculation | smallpox control; eradication (1980) | — | — | n/a | — |
| `anaesthesia` | tech | A8 | C | chemistry_quantitative AND anatomy_dissection | long, deliberate surgery | — | — | n/a | — |
| `germ_theory` | tech | A8 | B (BJ) | microscope | antisepsis; targeted vaccines; public health engineering | `age:a8_germ` → Age 8 milestone `a8_germ` (supporting)<br>`infra.sewerage_system` → no realizing system | yes | partly (rest deferred) | Age |
| `antisepsis` | tech | A8 | C | germ_theory AND anaesthesia | abdominal surgery; survival of wounds | — | — | n/a | — |
| `photography` | tech | A8 | C | chemistry_quantitative AND glass_lead | visual record; D-039 reconnaissance by camera (later) | — | — | n/a | — |
| `evolution` | tech | A8 | C | selective_breeding | biology as a science; breeding theory | — | — | n/a | — |
| `aircraft` | tech | A9 | F (FJ) | internal_combustion | aerial reconnaissance (D-039 B: reconnaissance no longer walks); bombing; air transport | `building.airfield` → no realizing system<br>`unit.aircraft_carrier` → no realizing system<br>`unit.military_aircraft` → no realizing system | yes | deferred | — |
| `aircraft_metal` | tech | A9 | C | aircraft AND aluminium | airliners; strategic bombers | — | — | n/a | — |
| `jet_engine` | tech | A9 | C | aircraft_metal AND steam_turbine | jet aircraft; global air travel | — | — | n/a | — |
| `steam_turbine` | tech | A9 | F | steam_high_pressure AND open_hearth | efficient generation; Dreadnought speed | `unit.aircraft_carrier` → no realizing system | yes | deferred | — |
| `automobile_mass` | tech | A9 | C | internal_combustion AND interchangeable_parts AND petroleum_refining | suburb; trucking; the road economy | — | — | n/a | — |
| `tank` | tech | A9 | F | internal_combustion AND basic_process AND cannon_cast_iron | mobile armoured warfare | `unit.tank` → no realizing system | yes | deferred | — |
| `diesel` | tech | A9 | C | internal_combustion AND open_hearth | heavy transport; submarines; generators | — | — | n/a | — |
| `aluminium` | tech | A9 | C | electricity_generation AND chemistry_quantitative | light structural metal; aircraft | — | — | n/a | — |
| `superalloy` | tech | A9 | C | open_hearth AND chemistry_quantitative | jet engines; gas turbines | — | — | n/a | — |
| `rocket` | tech | A9 | F (FJ) | internal_combustion AND aluminium AND calculus | ballistic missile; satellite launch | `building.spaceport` → no realizing system<br>`project.orbital_launch` → no realizing system<br>`unit.missile` → no realizing system | yes | deferred | — |
| `satellite` | tech | A9 | C | rocket AND computer AND transistor | orbital reconnaissance (D-039 B: pointed becomes GLOBAL); satellite communication; GPS | — | — | n/a | — |
| `container_shipping` | tech | A9 | C | iron_hull AND diesel | near-zero bulk transport cost by sea | — | — | n/a | — |
| `radio` | tech | A9 | B (BJ) | electromagnetism AND telegraph | wireless command (D-039 A); broadcast media (D-041 C1) | `age:a9_radio` → Age 9 milestone `a9_radio` (supporting)<br>`building.radio_transmitter` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system | yes | partly (rest deferred) | Age |
| `vacuum_tube` | tech | A9 | C | electric_light AND radio | voice radio; long-distance telephone; first computers | — | — | n/a | — |
| `radar` | tech | A9 | C | vacuum_tube AND electromagnetism | air defence; navigation; D-039 B reconnaissance at radio range | — | — | n/a | — |
| `television` | tech | A9 | J | vacuum_tube AND photography | mass visual media (D-041 C1) | `inst.mass_media_broadcast` → no realizing system | yes | deferred | — |
| `computer` | tech | A9 | B | vacuum_tube AND calculus | programmable computation; ballistics, codebreaking, then everything | `age:a9_computing` → Age 9 milestone `a9_computing` (supporting) | yes | yes | Age |
| `transistor` | tech | A9 | C | vacuum_tube AND quantum_mechanics | reliable small electronics | — | — | n/a | — |
| `integrated_circuit` | tech | A9 | J | transistor AND photography | cheap computation; the microprocessor | `building.semiconductor_fab` → no realizing system | yes | deferred | — |
| `microprocessor` | tech | A9 | C | integrated_circuit AND computer | personal computing; embedded control everywhere | — | — | n/a | — |
| `internet` | tech | A9 | J | computer AND telephone AND integrated_circuit | global instant information; D-039 A2 lag collapse; D-041 media at global scale | `infra.internet_backbone` → no realizing system | yes | deferred | — |
| `quantum_mechanics` | tech | A9 | C | electromagnetism | semiconductors; nuclear physics; chemistry explained | — | — | n/a | — |
| `nuclear_fission` | tech | A9 | C | quantum_mechanics AND electricity_generation | nuclear weapon (D-011 deterrence); reactor | — | — | n/a | — |
| `nuclear_power` | tech | A9 | J | nuclear_fission AND steam_turbine | baseload electricity without fuel imports | `building.nuclear_power_station` → no realizing system | yes | deferred | — |
| `thermonuclear` | tech | A9 | C | nuclear_fission | mutual assured destruction | — | — | n/a | — |
| `antibiotic_penicillin` | tech | A9 | B | germ_theory AND fermentation_grain | wound survival; maternal mortality collapse | `age:a9_antibiotics` → Age 9 milestone `a9_antibiotics` (supporting) | yes | yes | Age |
| `antibiotic_broad` | tech | A9 | C | antibiotic_penicillin | tuberculosis curable; routine surgery | — | — | n/a | — |
| `antibiotic_resistance` | tech | A9 | C | antibiotic_broad AND evolution | sustaining antibiotic efficacy | — | — | n/a | — |
| `vaccine_lab` | tech | A9 | C | germ_theory AND vaccination | childhood mortality collapse | — | — | n/a | — |
| `blood_transfusion` | tech | A9 | C | germ_theory AND anaesthesia | surgical and trauma survival | — | — | n/a | — |
| `refrigeration` | tech | A8 | C | chemistry_quantitative AND steam_rotary | meat by sea from Argentina; spoilage rate near zero | — | — | n/a | — |
| `canning` | tech | A8 | C | glass_blowing AND sheet_metal | army and navy provisions; spoilage to near zero | — | — | n/a | — |
| `green_revolution` | tech | A9 | C | haber_bosch AND genetics_mendel AND irrigation_canal | famine ended in Asia | — | — | n/a | — |
| `genetics_mendel` | tech | A9 | C | evolution AND microscope | scientific breeding; molecular biology (next) | — | — | n/a | — |
| `dna_structure` | tech | A9 | C | genetics_mendel AND quantum_mechanics AND photography | genetic engineering; forensics; medicine by genome | — | — | n/a | — |
| `genetic_engineering` | tech | A9 | C | dna_structure AND antibiotic_broad | synthetic insulin; GM crops; vaccines by design | — | — | n/a | — |
| `hormonal_contraception` | tech | A9 | C | chemistry_quantitative AND genetics_mendel | fertility as a chosen variable | — | — | n/a | — |
| `xray` | tech | A9 | C | electromagnetism AND barometer_vacuum AND photography | diagnosis; crystallography (DNA) | — | — | n/a | — |
| `plastics` | tech | A9 | C | petroleum_refining AND chemistry_quantitative AND synthetic_dye | cheap durable materials; textiles without fibre crops | — | — | n/a | — |
| `electric_traction` | tech | A9 | C | electric_motor AND railway | city of 10M; commuting | — | — | n/a | — |
| `skyscraper` | tech | A9 | J | bessemer AND electric_motor AND reinforced_concrete | density without sprawl | `building.skyscraper` → no realizing system | yes | deferred | — |
| `hydroelectric` | tech | A9 | J | electricity_generation AND reinforced_concrete AND water_mill | cheap electricity; aluminium (energy-gated) | `building.hydroelectric_dam` → no realizing system | yes | deferred | — |
| `mechanised_agriculture` | tech | A9 | C | internal_combustion AND seed_drill_tull | farm labour share to <5% | — | — | n/a | — |
| `numerical_control` | tech | A9 | C | computer AND milling_machine | flexible automation | — | — | n/a | — |
| `mobile_phone` | tech | A9 | J | radio AND microprocessor AND telephone | ubiquitous communication | `infra.mobile_network` → no realizing system | yes | deferred | — |
| `gps` | tech | A9 | C | satellite AND microprocessor AND atomic_clock | D-040 B1 extent: known everywhere; precision agriculture; guided munitions | — | — | n/a | — |
| `atomic_clock` | tech | A9 | C | quantum_mechanics AND longitude_problem | navigation; network synchronisation | — | — | n/a | — |
| `solar_pv` | tech | A9 | J | transistor AND quantum_mechanics | distributed generation; energy without fuel or river | `building.solar_farm` → no realizing system | yes | deferred | — |
| `lithium_battery` | tech | A9 | C | chemistry_quantitative AND electromagnetism | portable electronics; electric vehicles; grid storage | — | — | n/a | — |
| `relativity` | tech | A9 | C | electromagnetism AND mechanics_newtonian | mass-energy equivalence; orbital timing corrections (GPS) | — | — | n/a | — |
| `laser` | tech | A9 | J | quantum_mechanics AND vacuum_tube | coherent light; precision cutting; optical communication | `project.probe_acceleration` → no realizing system | yes | deferred | — |
| `fibre_optics` | tech | A9 | C | laser AND glass_lead | high-bandwidth long-distance communication | — | — | n/a | — |
| `cmos_vlsi` | tech | A9 | C | microprocessor | cheap abundant computation; embedded control everywhere | — | — | n/a | — |
| `superconductivity_applied` | tech | A9 | C | quantum_mechanics AND refrigeration | high-field magnets; MRI; particle accelerators; fusion confinement | — | — | n/a | — |
| `composites` | tech | A9 | C | plastics | light strong structures; turbine blades; airframes | — | — | n/a | — |
| `gas_turbine_power` | tech | A9 | C | jet_engine AND electricity_generation | efficient dispatchable generation | — | — | n/a | — |
| `wind_turbine_modern` | tech | A9 | J | composites AND electricity_generation | generation capacity from wind | `building.wind_farm` → no realizing system | yes | deferred | — |
| `nuclear_fusion_research` | tech | A9 | C | thermonuclear AND superconductivity_applied | plasma confinement; fusion science | — | — | n/a | — |
| `grid_storage` | tech | A9 | C | lithium_battery AND electricity_generation | time-shifting generation; stability with intermittent sources | — | — | n/a | — |
| `smart_grid` | tech | A9 | C | internet AND electricity_generation | demand response; distributed generation | — | — | n/a | — |
| `programming_languages` | tech | A9 | C | computer | software at scale | — | — | n/a | — |
| `operating_system` | tech | A9 | C | programming_languages | multi-user computing; software portability | — | — | n/a | — |
| `relational_database` | tech | A9 | C | programming_languages | administration at national scale; transaction systems | — | — | n/a | — |
| `personal_computer` | tech | A9 | C | cmos_vlsi AND operating_system | individual computing; office automation | — | — | n/a | — |
| `public_key_crypto` | tech | A9 | C | programming_languages | secure networks; digital signatures; e-commerce | — | — | n/a | — |
| `gpu_parallel` | tech | A9 | C | cmos_vlsi | large-scale numerical computation | — | — | n/a | — |
| `machine_learning` | tech | A9 | C | programming_languages AND calculus | pattern recognition; prediction from data | — | — | n/a | — |
| `deep_learning` | tech | A9 | C | machine_learning AND gpu_parallel | machine perception; automated translation | — | — | n/a | — |
| `large_language_models` | tech | A9 | C | deep_learning AND internet | machine-generated text and code; knowledge assistance | — | — | n/a | — |
| `cloud_computing` | tech | A9 | J | internet AND relational_database | elastic computation; global services | `building.data_centre` → no realizing system | yes | deferred | — |
| `smartphone` | tech | A9 | C | mobile_phone AND personal_computer AND lithium_battery | ubiquitous computing; mobile internet | — | — | n/a | — |
| `cyber_warfare` | tech | A9 | C | internet AND public_key_crypto | network attack and defence; infrastructure sabotage | — | — | n/a | — |
| `quantum_computing` | tech | A9 | C | quantum_mechanics AND cmos_vlsi AND superconductivity_applied | quantum simulation (limited) | — | — | n/a | — |
| `cellular_digital` | tech | A9 | C | mobile_phone AND cmos_vlsi | mobile data | — | — | n/a | — |
| `crewed_spaceflight` | tech | A9 | C | satellite AND computer | human operations in orbit | — | — | n/a | — |
| `lunar_flight` | tech | A9 | C | crewed_spaceflight AND integrated_circuit | crewed deep-space operations | — | — | n/a | — |
| `space_station` | tech | A9 | C | crewed_spaceflight | permanent orbital presence; microgravity research | — | — | n/a | — |
| `space_probe` | tech | A9 | C | satellite AND computer | exploration of other worlds | — | — | n/a | — |
| `reusable_launch` | tech | A9 | C | rocket AND gps AND composites | cheap access to orbit | — | — | n/a | — |
| `space_telescope` | tech | A9 | C | satellite AND telescope | observation across the spectrum; exoplanet detection | — | — | n/a | — |
| `earth_observation` | tech | A9 | C | satellite AND photography | global mapping; crop and resource monitoring | — | — | n/a | — |
| `organ_transplant` | tech | A9 | C | antisepsis AND blood_transfusion | organ failure survivable | — | — | n/a | — |
| `chemotherapy` | tech | A9 | C | chemistry_quantitative AND germ_theory | cancer treatment | — | — | n/a | — |
| `medical_imaging` | tech | A9 | C | xray AND computer | non-invasive diagnosis | — | — | n/a | — |
| `antiviral_drugs` | tech | A9 | C | dna_structure AND chemistry_quantitative | viral disease treatable | — | — | n/a | — |
| `monoclonal_antibodies` | tech | A9 | C | dna_structure AND vaccine_lab | targeted therapy; precise diagnostics | — | — | n/a | — |
| `pcr` | tech | A9 | C | dna_structure | genetic testing; forensics | — | — | n/a | — |
| `genome_sequencing` | tech | A9 | C | dna_structure AND computer | genetic medicine; breeding by genome | — | — | n/a | — |
| `ivf` | tech | A9 | C | hormonal_contraception | assisted reproduction | — | — | n/a | — |
| `crispr` | tech | A9 | C | genome_sequencing AND genetic_engineering | precise genetic modification | — | — | n/a | — |
| `mrna_vaccines` | tech | A9 | C | genetic_engineering AND vaccine_lab AND genome_sequencing | rapid vaccine design | — | — | n/a | — |
| `oral_rehydration` | tech | A9 | C | germ_theory AND chemistry_quantitative | child mortality from diarrhoea collapses | — | — | n/a | — |
| `drip_irrigation` | tech | A9 | C | plastics | crops on arid land with little water | — | — | n/a | — |
| `precision_agriculture` | tech | A9 | C | gps AND mechanised_agriculture | input efficiency per field | — | — | n/a | — |
| `desalination` | tech | A9 | J | electricity_generation | water supply independent of rivers and rain | `building.desalination_plant` → no realizing system | yes | deferred | — |
| `catalytic_converter` | tech | A9 | C | chemistry_quantitative AND automobile_mass | vehicle emission control | — | — | n/a | — |
| `weather_prediction` | tech | A9 | C | computer AND radio | harvest and disaster forecasting | — | — | n/a | — |
| `carbon_capture` | tech | A9 | C | chemistry_quantitative AND gas_turbine_power | emission removal (limited scale) | — | — | n/a | — |
| `high_speed_rail` | tech | A9 | C | electric_traction AND reinforced_concrete | intercity travel faster than air over medium distance | — | — | n/a | — |
| `jet_airliner` | tech | A9 | C | jet_engine AND aircraft_metal | mass international travel | — | — | n/a | — |
| `electric_vehicle` | tech | A9 | C | lithium_battery AND electric_motor | road transport without oil | — | — | n/a | — |
| `helicopter` | tech | A9 | F | aircraft | vertical lift; air mobility without runways | `unit.helicopter` → no realizing system | yes | deferred | — |
| `icbm` | tech | A9 | F | rocket AND thermonuclear AND computer | intercontinental strike | `unit.icbm` → no realizing system | yes | deferred | — |
| `nuclear_submarine` | tech | A9 | F | nuclear_fission AND screw_propeller | undersea deterrence; sea denial | `unit.nuclear_submarine` → no realizing system | yes | deferred | — |
| `guided_munitions` | tech | A9 | C | laser AND rocket AND computer | precision strike | — | — | n/a | — |
| `stealth` | tech | A9 | F | composites AND radar AND computer | penetration of air defences | `unit.stealth_aircraft` → no realizing system | yes | deferred | — |
| `combat_drone` | tech | A9 | F | gps AND microprocessor AND aircraft | persistent surveillance; strike without aircrew | `unit.combat_drone` → no realizing system | yes | deferred | — |
| `missile_defence` | tech | A9 | C | radar AND guided_munitions | defence against ballistic and cruise missiles | — | — | n/a | — |
| `additive_manufacturing` | tech | A9 | C | numerical_control AND plastics AND computer | complex parts without tooling | — | — | n/a | — |
| `nanotechnology` | tech | A9 | C | cmos_vlsi AND quantum_mechanics | nanomaterials; sub-100 nm fabrication | — | — | n/a | — |
| `fusion_power` | tech | A9 | J | nuclear_fusion_research | baseload generation without fission fuel | `building.fusion_plant` → no realizing system | yes | deferred | — |
| `fault_tolerant_quantum` | tech | A9 | C | quantum_computing | quantum simulation of chemistry and materials | — | — | n/a | — |
| `general_ai` | tech | A9 | C | large_language_models | automation of cognitive work | — | — | n/a | — |
| `space_solar` | tech | A9 | J | solar_pv AND reusable_launch | generation without land or night | `project.probe_acceleration` → no realizing system | yes | deferred | — |
| `asteroid_mining` | tech | A9 | C | reusable_launch AND space_probe | off-world resources | — | — | n/a | — |
| `fusion_propulsion` | tech | A9 | J | fusion_power AND space_probe | interstellar probe | `project.interstellar_probe` → no realizing system | yes | deferred | — |
| `frontier_launch` | tech | A9 | H | reusable_launch | cheaper space projects per level | — | — | n/a | — |
| `frontier_propulsion` | tech | A9 | H | space_probe | faster interplanetary and interstellar transit | — | — | n/a | — |
| `frontier_materials` | tech | A9 | H | nanotechnology | lighter, cheaper structures | — | — | n/a | — |
| `frontier_energy` | tech | A9 | H | solar_pv OR gas_turbine_power | more generation per unit of fuel or sunlight | — | — | n/a | — |
| `frontier_crops` | tech | A9 | H | crispr | higher yield per km² | — | — | n/a | — |
| `frontier_medicine` | tech | A9 | H | genome_sequencing | longer lives per level | — | — | n/a | — |
| `frontier_computation` | tech | A9 | H | cmos_vlsi | administration and automation efficiency | — | — | n/a | — |
| `frontier_military` | tech | A9 | H | guided_munitions | combat effectiveness relative to rivals | — | — | n/a | — |
| `frontier_cyber` | tech | A9 | H | cyber_warfare | information offence and defence relative to rivals | — | — | n/a | — |
| `frontier_climate` | tech | A9 | H | carbon_capture | environmental remediation | — | — | n/a | — |
| `motor_road` | tech | A9 | E | automobile_mass AND reinforced_concrete AND petroleum_refining | highway infrastructure class: motor road, the bulk road-freight corridor | `infra.road_highway` → road class `infra.road_highway` | yes | yes | Roads |
| `trade` | tech | A3 | B | token_counting AND (donkey OR camel OR sail_square) | formal exchange between settlements; commercial arbitrage along connected routes; foreign trade across polity boundaries | `activity.trade` → sim.json trade.entity (trade between settlements) | yes | yes | Trade |
| `law_code` | civics | A3 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) | — | `age:a3_law` → Age 3 milestone `a3_law` (supporting)<br>`inst.law_code` → no realizing system | yes | partly (rest deferred) | Age |
| `legal_code_roman` | civics | A4 | B (BDJ) | law_code | — | `age:a5_systematic_law` → Age 5 milestone `a5_systematic_law` (core)<br>`inst.guild` → no realizing system<br>`inst.legal_code_roman` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.university`<br>`inst.university` → project `military university` founds<br>`inst.university` → project `medical university` founds<br>`inst.university` → project `engineering university` founds<br>`inst.university` → project `natural science university` founds<br>`inst.university` → project `agricultural university` founds<br>`stage` → research.json researchStage.requires | yes | partly (rest deferred) | Age, Construction, Research |
| `census` | civics | A4 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) AND stamp_seal | — | `age:a4_census` → Age 4 milestone `a4_census` (supporting)<br>`inst.census` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system via `inst.mass_schooling`<br>`inst.mass_schooling` → no realizing system<br>`inst.military_medicine` → no realizing system<br>`inst.mint_institution` → no realizing system<br>`inst.quarantine` → no realizing system<br>`inst.statistics_vital` → no realizing system | yes | partly (rest deferred) | Age |
| `coined_wage` | civics | A4 | B (BJ) | coinage_electrum | — | `age:a5_wages` → Age 5 milestone `a5_wages` (supporting)<br>`inst.coined_wage` → no realizing system | yes | partly (rest deferred) | Age |
| `patent` | civics | A7 | B (BJ) | legal_code_roman AND printing_press | — | `age:a7_patent` → Age 7 milestone `a7_patent` (supporting)<br>`inst.patent` → no realizing system | yes | partly (rest deferred) | Age |
| `joint_stock` | civics | A6 | B (BJ) | bill_of_exchange AND legal_code_roman | — | `age:a7_company` → Age 7 milestone `a7_company` (supporting)<br>`inst.central_bank` → no realizing system<br>`inst.joint_stock` → no realizing system | yes | partly (rest deferred) | Age |
