# Research → gameplay unlock audit

**Generated** by `scripts/research-gameplay-unlock-audit.py` from the canonical content (research.json sha256 `5a01689516a62713…`). Do not edit by hand; CI runs `--check`. Every row is derived from content links; the only mapping in the script is per consumer type (§1), which names no node.

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
| B | Research-gated gameplay capability | gates a production recipe, a labour activity, trade, the tax edict or an Age milestone | 51 |
| C | Knowledge-only / modifier | capability strings or immediate effects, no realizing link | 382 |
| D | Institution prerequisite | gates a REALIZED institution (a founding project) or the research stage | 7 |
| E | Infrastructure prerequisite | gates a road class RoadDevelopmentSystem builds | 4 |
| F | Military / unit unlock | gates a unit entity (load-validated; recruitment is M7 Battle Layer) | 31 |
| G | Application / realization | gates a building a construction project builds | 0 |
| H | Repeatable | a repeatable node (D-044 T6 / D-046 G4) | 10 |
| I | Content-only / descriptive | no capability string, no effect, no entity | 0 |
| J | Explicitly deferred | every consequence is an entity no system of this milestone realizes | 78 |
| | **nodes** | Technology 496 + Civics 67 | **563** |

Node→consequence links by consumer type: activity 10, age 41, building 15, deferred 245, institution 50, recipe 2, road 6, stage 4, tax 1, trade 1.

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
| deferred | `activity.ocean_shipping` | activity entity requires `carrack OR caravel OR polynesian_canoe OR ocean_junk`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
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
| deferred | `building.library` | building entity requires `cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper OR mesoamerican_script`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `building.lighthouse` | building entity requires `ashlar_masonry OR stone_fortification`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
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
| deferred | `building.school` | building entity requires `cuneiform OR hieroglyphic OR chinese_script OR abjad OR brahmi OR mesoamerican_script`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
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
| deferred | `infra.sewerage_system` | infrastructure entity requires `sanitary_engineering OR (germ_theory AND reinforced_concrete)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.submarine_cable` | infrastructure entity requires `submarine_cable`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.telegraph_line` | infrastructure entity requires `telegraph`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.telephone_network` | infrastructure entity requires `telephone`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.terraces` | infrastructure entity requires `terrace`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.urban_drainage` | infrastructure entity requires `fired_brick`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `infra.well` | infrastructure entity requires `well`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.aramaic_admin` | institution entity requires `abjad AND archive`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.archive` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND stamp_seal`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.census` | institution entity requires `census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.central_bank` | institution entity requires `joint_stock AND paper_money`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.coined_wage` | institution entity requires `coined_wage`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.guild` | institution entity requires `textile_workshop AND (legal_code_roman OR craft_guilds)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.historiography` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND library`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.hospital` | institution entity requires `medicine_hippocratic AND pharmacology`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.joint_stock` | institution entity requires `joint_stock`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.law_code` | institution entity requires `law_code`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.legal_code_roman` | institution entity requires `legal_code_roman`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.library` | institution entity requires `archive AND scribal_school`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mass_media_broadcast` | institution entity requires `radio AND television AND mass_schooling`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mass_schooling` | institution entity requires `printing_press AND newspaper AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.military_medicine` | institution entity requires `surgery_instruments AND (census OR standing_army)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.mint_institution` | institution entity requires `coinage_electrum AND (census OR appointed_officials)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.natural_philosophy` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND astronomy_records`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.newspaper` | institution entity requires `printing_press`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.patent` | institution entity requires `patent`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.quarantine` | institution entity requires `hospital AND census`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.scientific_society` | institution entity requires `university AND printing_press AND optics_ibn_haytham`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.scribal_school` | institution entity requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.standard_threads` | institution entity requires `slide_rest_lathe`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
| deferred | `inst.statistics_vital` | institution entity requires `(census AND calculus) OR (official_statistics AND epidemiology)`; no system of this milestone realizes it | DEFERRED — no owning system yet; knowledge eligibility is shown in the trees and lenses only |
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
| deferred | `unit.siege_engine` | unit entity requires `siege_ram OR torsion_artillery OR trebuchet OR traction_trebuchet`; no system of this milestone realizes it | DEFERRED — recruitment is M7 Battle Layer (ADR-033 D7) |
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
| `microlith` | tech | A1 | C | hafting AND knapping_blade | barbed arrows; sickles (later); composite knives | — | — | n/a | — |
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
| `orchard` | tech | A2 | C | cereal_cultivation OR root_crop | oil; wine (with fermentation); dried fruit | — | — | n/a | — |
| `sickle` | tech | A2 | C | microlith | efficient cereal harvest | — | — | n/a | — |
| `digging_stick_hoe` | tech | A2 | C | hafting AND ground_stone_early | hafted-blade tillage of heavier soils; faster weeding and planting | — | — | n/a | — |
| `nixtamalization` | tech | A3 | C | maize AND pottery_open_fired | maize as sole staple without deficiency | — | — | n/a | — |
| `sheep_goat` | tech | A2 | B | dog_domestication | meat on the hoof; selective flock breeding | `activity.herding` → sector `herding_fishing` → replaces<br>`age:a2_herding` → Age 2 milestone `a2_herding` (supporting) | yes | yes | Labour, Age |
| `cattle` | tech | A2 | B | sheep_goat | large-animal traction (with yoke); dairy; dung fuel | `activity.herding` → sector `herding_fishing` → replaces | yes | yes | Labour |
| `pig` | tech | A2 | C | dog_domestication | meat without pasture | — | — | n/a | — |
| `milking` | tech | A2 | C | (sheep_goat OR cattle) AND pottery_open_fired | dairy food; sustained yield without slaughter | — | — | n/a | — |
| `wool` | tech | A2 | C | sheep_goat AND cordage | wool fibre; textile without a fibre crop | — | — | n/a | — |
| `animal_traction` | tech | A2 | C | cattle | ploughing; cart haulage; threshing by treading | — | — | n/a | — |
| `pottery_open_fired` | tech | A2 | B | basketry | boiling; storage; fermentation vessel | `age:a2_pottery` → Age 2 milestone `a2_pottery` (supporting)<br>`recipe.pottery_firing` → recipe `pottery-firing` | yes | yes | Age, Production |
| `kiln_updraft` | tech | A2 | J | pottery_open_fired AND mudbrick | high-fired ceramics; reducing atmosphere; glaze (later) | `building.kiln` → no realizing system | yes | deferred | — |
| `potters_wheel_fast` | tech | A2 | C | pottery_open_fired | pottery at scale; standardised wares (bevel-rim bowls) | — | — | n/a | — |
| `mudbrick` | tech | A2 | J | — | permanent multi-room house; storehouse; wall | `building.city_wall` → no realizing system | yes | deferred | — |
| `lime_plaster` | tech | A2 | C | fire_making | waterproof floors; plastered walls; plaster statuary | — | — | n/a | — |
| `timber_frame` | tech | A2 | C | ground_stone_early AND shelter_hut | large roofed spaces; multi-family dwellings | — | — | n/a | — |
| `stone_dry` | tech | A2 | E (EJ) | ground_stone_early | durable walls; megaliths; terracing | `building.city_wall` → no realizing system<br>`building.harbour` → no realizing system<br>`infra.road_built` → road class `infra.road_built` | yes | partly (rest deferred) | Roads |
| `well` | tech | A2 | J | stone_dry OR timber_frame | settlement away from rivers; reliable water in drought | `infra.well` → no realizing system | yes | deferred | — |
| `fermentation_grain` | tech | A2 | C | (cereal_domesticated OR rice_wet OR millet OR maize OR sorghum_pearl_millet) AND pottery_open_fired | beer; grain preserved as drink; bread yeast | — | — | n/a | — |
| `fermentation_fruit` | tech | A2 | C | pottery_open_fired | wine; mead | — | — | n/a | — |
| `oil_press` | tech | A2 | J | orchard AND stone_dry | cooking oil; lamp fuel; storable calories | `building.oil_press` → no realizing system | yes | deferred | — |
| `salt_extraction` | tech | A2 | C | pottery_open_fired | salting meat and fish; salt as a trade good | — | — | n/a | — |
| `flax` | tech | A2 | C | cereal_cultivation | linen fibre | — | — | n/a | — |
| `spinning_spindle` | tech | A2 | C | cordage AND (flax OR wool OR cotton_cultivation) | thread at volume | — | — | n/a | — |
| `loom_warp_weighted` | tech | A2 | J | spinning_spindle AND timber_frame | cloth; sailcloth (later) | `inst.guild` → no realizing system via `inst.textile_workshop`<br>`inst.textile_workshop` → no realizing system | yes | deferred | — |
| `dyeing` | tech | A2 | C | spinning_spindle AND ochre_processing | coloured cloth; status signalling; dye as trade good | — | — | n/a | — |
| `copper_native` | tech | A2 | C | ground_stone_early AND fire_making | metal ornament; small copper tools | — | — | n/a | — |
| `copper_smelting` | tech | A2 | J | kiln_updraft AND (copper_native OR ochre_processing) | cast copper tools; copper as trade good | `building.smithy` → no realizing system | yes | deferred | — |
| `charcoal` | tech | A2 | C | birch_tar AND ground_stone_early | smelting fuel; higher kiln temperatures | — | — | n/a | — |
| `casting_open` | tech | A2 | C | copper_smelting | cast copper axes | — | — | n/a | — |
| `gold_silver_native` | tech | A2 | C | copper_native | prestige goods; proto-currency | — | — | n/a | — |
| `irrigation_basin` | tech | A2 | J | cereal_cultivation OR root_crop | cultivation on floodplain; predictable harvest from flood | `infra.basin_irrigation` → no realizing system | yes | deferred | — |
| `irrigation_canal` | tech | A2 | J | cereal_cultivation OR root_crop | cultivation beyond rainfall; arable extension; salinisation (long-run consequence) | `infra.canal_irrigation` → no realizing system | yes | deferred | — |
| `terrace` | tech | A2 | J | stone_dry AND (cereal_cultivation OR root_crop) | hillside cultivation; erosion control | `infra.terraces` → no realizing system | yes | deferred | — |
| `sledge` | tech | A2 | C | ground_stone_early | heavy haulage without wheels | — | — | n/a | — |
| `wheel_solid` | tech | A2 | B | animal_traction AND ground_stone_early | cart; wagon | `age:a3_wheel` → Age 3 milestone `a3_wheel` (supporting) | yes | yes | Age |
| `cart` | tech | A2 | C | wheel_solid | bulk overland transport at cart range | — | — | n/a | — |
| `track_road` | tech | A2 | E (EJ) | timber_frame | trackway infrastructure class (path tier: timber causeway and stoned track); wetland causeway class | `infra.courier_relay` → no realizing system<br>`infra.road_built` → road class `infra.road_built`<br>`infra.road_track` → road class `infra.road_track` | yes | partly (rest deferred) | Roads |
| `plough_ard` | tech | A2 | C | animal_traction AND digging_stick_hoe | extensive cultivation; labour productivity ×2-3 | — | — | n/a | — |
| `token_counting` | tech | A2 | B | fire_making AND tally_notation | record of quantity without memory | `age:a3_writing` → Age 3 milestone `a3_writing` (supporting) | yes | yes | Age |
| `stamp_seal` | tech | A2 | D (DJ) | ground_stone_early AND pottery_open_fired | marking ownership; sealing stores; administrative control | `inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`stage` → research.json researchStage.requires | yes | partly (rest deferred) | Construction, Research |
| `calendar_lunar` | tech | A2 | C | tally_notation | seasonal timing; ritual calendar | — | — | n/a | — |
| `trepanation` | tech | A2 | C | knapping_blade | cranial surgery | — | — | n/a | — |
| `arsenical_bronze` | tech | A3 | B | copper_smelting | harder tools and weapons without tin trade; arsenic poisoning of smiths | `age:a3_bronze` → Age 3 milestone `a3_bronze` (core) | yes | yes | Age |
| `tin_bronze` | tech | A3 | B | ore_prospecting | standardised alloy; long-distance tin trade dependency; Bronze weapons; Bronze armour | `age:a3_bronze` → Age 3 milestone `a3_bronze` (core)<br>`recipe.bronze_casting` → recipe `bronze-casting` | yes | yes | Age, Production |
| `casting_closed` | tech | A3 | C | casting_open AND tin_bronze | socketed tools; complex shapes | — | — | n/a | — |
| `lost_wax` | tech | A3 | C | casting_open | statuary; complex vessels; fine ornament | — | — | n/a | — |
| `sheet_metal` | tech | A3 | C | tin_bronze | bronze vessels; sheet armour | — | — | n/a | — |
| `mining_shaft` | tech | A3 | B | stone_dry AND copper_smelting | ore from depth; sustained metal supply | `activity.mining` → sector `extraction` → joins | yes | yes | Labour |
| `proto_writing` | tech | A3 | B | token_counting | administrative records beyond memory | `age:a3_writing` → Age 3 milestone `a3_writing` (supporting) | yes | yes | Age |
| `cuneiform` | tech | A3 | D (DJ) | proto_writing | law; literature; letters; history | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `hieroglyphic` | tech | A3 | D (DJ) | token_counting | monumental inscription; administration | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `chinese_script` | tech | A3 | D (DJ) | tally_notation AND bone_tools | royal divination record; continuous script tradition to today | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `papyrus` | tech | A3 | J | basketry | portable documents; archives; Egyptian export monopoly | `building.library` → no realizing system | yes | deferred | — |
| `numeral_sexagesimal` | tech | A3 | C | cuneiform | multiplication; division; fractions; tables | — | — | n/a | — |
| `arithmetic_babylonian` | tech | A3 | C | numeral_sexagesimal | land measurement; tax assessment; construction calculation | — | — | n/a | — |
| `surveying` | tech | A3 | C | cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script | land register; taxation by area; construction layout | — | — | n/a | — |
| `sail_square` | tech | A3 | J | (dugout OR raft) AND loom_warp_weighted | improved coastal and seagoing transport class (sail); upriver travel under sail instead of rowing; river and coastal trade at scale under sail | `activity.coastal_shipping` → no realizing system | yes | deferred | — |
| `plank_boat` | tech | A3 | J | dugout AND copper_smelting | cargo vessels; sea-going hulls | `building.harbour` → no realizing system | yes | deferred | — |
| `horse_domestication` | tech | A3 | J | cattle | riding; fast overland movement; horse traction | `infra.courier_relay` → no realizing system | yes | deferred | — |
| `donkey` | tech | A3 | J | cattle | pack transport; caravan trade | `activity.caravans` → no realizing system | yes | deferred | — |
| `camel` | tech | A3 | J | cattle | desert crossing; long-distance caravan | `activity.caravans` → no realizing system | yes | deferred | — |
| `wheel_spoked` | tech | A3 | C | wheel_solid AND casting_closed | fast light vehicles | — | — | n/a | — |
| `chariot` | tech | A3 | F | wheel_spoked AND horse_domestication | mobile archery platform; elite warfare | `unit.chariot` → no realizing system | yes | deferred | — |
| `composite_bow` | tech | A3 | F | bow_simple AND sheep_goat AND hide_working | mounted archery; chariot archery | `unit.horse_archers` → no realizing system | yes | deferred | — |
| `fired_brick` | tech | A3 | J | kiln_updraft AND mudbrick | foundations; drains; baths; monumental work | `infra.sewer` → no realizing system<br>`infra.urban_drainage` → no realizing system | yes | deferred | — |
| `glass_glaze` | tech | A3 | C | kiln_updraft AND salt_extraction | glazed ware; faience beads | — | — | n/a | — |
| `glass_core` | tech | A3 | C | glass_glaze | glass vessels; luxury export | — | — | n/a | — |
| `standard_weights` | tech | A3 | C | token_counting AND (proto_writing OR hieroglyphic OR chinese_script) | fair exchange; taxation by weight; silver by weight (proto-money) | — | — | n/a | — |
| `calendar_civil` | tech | A3 | C | calendar_lunar AND (cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) | administrative dating; Nile flood prediction (Sothic rising) | — | — | n/a | — |
| `water_clock` | tech | A3 | C | pottery_open_fired AND calendar_civil | night hours; temple timing | — | — | n/a | — |
| `medicine_recorded` | tech | A3 | C | (cuneiform OR hieroglyphic OR chinese_script) | transmissible treatment; surgical procedure record | — | — | n/a | — |
| `beekeeping` | tech | A3 | C | pottery_open_fired | honey; beeswax for casting and sealing | — | — | n/a | — |
| `iron_bloomery` | tech | A4 | B (BJ) | charcoal AND kiln_updraft | iron tools and weapons; metal without long-distance trade; Iron agricultural tools; Iron fastenings | `age:a4_iron` → Age 4 milestone `a4_iron` (core)<br>`building.smithy` → no realizing system | yes | partly (rest deferred) | Age |
| `bellows` | tech | A4 | C | hide_working AND kiln_updraft | hotter furnaces; iron smelting feasible | — | — | n/a | — |
| `steel_carburized` | tech | A4 | C | iron_bloomery | hard edges; blades better than bronze | — | — | n/a | — |
| `quenching` | tech | A4 | C | steel_carburized | very hard edges | — | — | n/a | — |
| `tempering` | tech | A4 | C | quenching | tough AND hard blades; Noric steel | — | — | n/a | — |
| `wootz` | tech | A4 | C | steel_carburized AND kiln_updraft | superior blades; Damascus steel (via export) | — | — | n/a | — |
| `pattern_welding` | tech | A4 | C | steel_carburized | composite blades | — | — | n/a | — |
| `abjad` | tech | A4 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) | cheap literacy; commercial records without a scribal class | `age:a4_alphabet` → Age 4 milestone `a4_alphabet` (supporting)<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system | yes | partly (rest deferred) | Age |
| `alphabet_vowels` | tech | A4 | C | abjad | unambiguous phonetic record; literature in vernacular | — | — | n/a | — |
| `brahmi` | tech | A4 | J | abjad | Indian literate tradition | `building.school` → no realizing system | yes | deferred | — |
| `hacksilver` | tech | A3 | C | standard_weights AND cupellation | exchange without barter; value store; price in shekels | — | — | n/a | — |
| `coinage_electrum` | tech | A4 | B (BJ) | hacksilver AND stamp_seal | state-guaranteed money; mercenary pay; market exchange at volume; taxation in coin | `age:a5_coinage` → Age 5 milestone `a5_coinage` (core)<br>`building.mint` → no realizing system<br>`inst.mint_institution` → no realizing system | yes | partly (rest deferred) | Age |
| `cavalry` | tech | A4 | F | horse_domestication | horse archery; mobile warfare; steppe raiding | `unit.horse_archers` → no realizing system | yes | deferred | — |
| `saddle` | tech | A4 | C | cavalry AND sewing | long-distance riding; lance use (with stirrup) | — | — | n/a | — |
| `siege_ram` | tech | A4 | F | wheel_solid AND hide_working | breach walls | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `siege_tower` | tech | A4 | C | siege_ram | assault over walls | — | — | n/a | — |
| `stone_fortification` | tech | A4 | J | ashlar_masonry AND surveying | walls resisting rams; gate defence | `building.castle` → no realizing system<br>`building.lighthouse` → no realizing system | yes | deferred | — |
| `crossbow` | tech | A4 | F | bow_simple AND casting_closed | mass conscript archery | `unit.crossbowmen` → no realizing system | yes | deferred | — |
| `naval_ram` | tech | A4 | F | plank_boat AND casting_closed | naval battle; sea control | `unit.war_galley` → no realizing system | yes | deferred | — |
| `qanat` | tech | A4 | J | well AND surveying AND mining_shaft | settlement in arid zones; irrigation without river | `infra.qanat` → no realizing system | yes | deferred | — |
| `aqueduct_tunnel` | tech | A4 | J | mining_shaft AND surveying | urban water supply under siege; settlement beyond local water | `infra.aqueduct` → no realizing system | yes | deferred | — |
| `aqueduct_channel` | tech | A4 | J | surveying AND stone_dry | long-distance urban water | `infra.aqueduct` → no realizing system | yes | deferred | — |
| `arch` | tech | A3 | C | mudbrick AND surveying | spans without lintels; gates, bridges, vaults | — | — | n/a | — |
| `astronomy_records` | tech | A4 | J | arithmetic_babylonian OR ((hieroglyphic OR chinese_script OR mesoamerican_script) AND calendar_lunar) | eclipse prediction (Saros); planetary periods; the empirical base for Greek astronomy | `inst.natural_philosophy` → no realizing system | yes | deferred | — |
| `calendar_lunisolar` | tech | A4 | C | astronomy_records | reliable religious and agricultural calendar | — | — | n/a | — |
| `geometry_practical` | tech | A3 | C | surveying AND (cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) | construction calculation; navigation by geometry | — | — | n/a | — |
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
| `concrete_pozzolan` | tech | A5 | B | lime_plaster | domes; harbour moles; vaults at scale | `age:a5_concrete` → Age 5 milestone `a5_concrete` (supporting) | yes | yes | Age |
| `vault_dome` | tech | A5 | J | arch AND (concrete_pozzolan OR fired_brick OR ashlar_masonry) | baths; basilicas; large public interiors | `building.bathhouse` → no realizing system<br>`building.castle` → no realizing system<br>`infra.sewer` → no realizing system | yes | deferred | — |
| `road_paved` | tech | A5 | B (BE) | surveying | engineered paved road class: all-weather bulk transport; engineered paved road class: military mobility | `age:a5_roads` → Age 5 milestone `a5_roads` (supporting)<br>`infra.road_paved` → road class `infra.road_paved` | yes | yes | Age, Roads |
| `bridge_stone` | tech | A5 | J | arch AND (concrete_pozzolan OR ashlar_masonry) | permanent river crossing; road network integrity | `infra.bridge` → no realizing system | yes | deferred | — |
| `aqueduct_arcade` | tech | A5 | J | aqueduct_channel AND arch | city of a million; baths; fountains | `building.bathhouse` → no realizing system<br>`infra.aqueduct` → no realizing system | yes | deferred | — |
| `glass_blowing` | tech | A5 | C | glass_blown_precursor AND iron_bloomery | mass glassware; window glass (later) | — | — | n/a | — |
| `window_glass` | tech | A5 | C | glass_blowing | glazed windows; baths, villas | — | — | n/a | — |
| `cast_iron` | tech | A5 | J | iron_bloomery AND bellows AND casting_open | mass-cast tools; cast cookware; cast ploughshares | `building.blast_furnace` → no realizing system | yes | deferred | — |
| `iron_mouldboard` | tech | A5 | C | cast_iron AND plough_ard | heavy soil cultivation; weed burial | — | — | n/a | — |
| `seed_drill` | tech | A5 | C | plough_ard AND cast_iron | seed economy; row cultivation; weeding between rows | — | — | n/a | — |
| `collar_harness` | tech | A5 | C | horse_domestication AND sewing | horse haulage | — | — | n/a | — |
| `paper` | tech | A5 | B (BJ) | cordage | cheap records; books; bureaucratic scale | `age:a6_paper` → Age 6 milestone `a6_paper` (supporting)<br>`building.library` → no realizing system<br>`building.paper_mill` → no realizing system<br>`inst.translation_movement` → no realizing system | yes | partly (rest deferred) | Age |
| `crossbow_repeating` | tech | A5 | C | crossbow | rapid fire at short range | — | — | n/a | — |
| `stirrup` | tech | A5 | F | saddle AND iron_bloomery | lance charge; heavy cavalry | `unit.heavy_cavalry` → no realizing system | yes | deferred | — |
| `silk` | tech | A3 | C | loom_warp_weighted AND orchard | silk cloth; the Silk Road export | — | — | n/a | — |
| `lacquer` | tech | A2 | C | adhesive_natural | waterproof vessels; luxury goods | — | — | n/a | — |
| `astronomy_geometric` | tech | A5 | J | geometry_axiomatic AND calendar_lunisolar | planetary prediction; star catalogue; the base for all astronomy until Kepler | `building.observatory` → no realizing system | yes | deferred | — |
| `cartography` | tech | A5 | C | astronomy_geometric | maps as knowledge; D-040 B1 computed extent made explicit | — | — | n/a | — |
| `latitude_sailing` | tech | A4 | C | astronomy_records OR calendar_lunar | open-sea voyaging by latitude | — | — | n/a | — |
| `monsoon_sailing` | tech | A5 | C | sail_square AND astronomy_records | direct Arabia–India crossing; Indian Ocean trade | — | — | n/a | — |
| `lateen` | tech | A5 | C | sail_square | windward sailing; coastal trade against wind | — | — | n/a | — |
| `mortise_hull` | tech | A3 | C | plank_boat | large cargo ships; grain fleet (Rome fed by Egypt) | — | — | n/a | — |
| `pharmacology` | tech | A5 | J | medicine_hippocratic | transmissible drug knowledge; herbal trade | `building.hospital` → no realizing system<br>`inst.hospital` → no realizing system<br>`inst.quarantine` → no realizing system via `inst.hospital` | yes | deferred | — |
| `surgery_instruments` | tech | A5 | J | medicine_hippocratic AND casting_closed | cataract surgery; trauma surgery; military medicine | `inst.military_medicine` → no realizing system | yes | deferred | — |
| `abacus` | tech | A5 | C | arithmetic_babylonian OR chinese_script | commercial arithmetic without literacy | — | — | n/a | — |
| `alchemy` | tech | A5 | C | copper_smelting AND (chinese_script OR alphabet_vowels) | distillation; mineral acids (later); gunpowder (China, later) | — | — | n/a | — |
| `distillation` | tech | A5 | C | alchemy AND (glass_blowing OR sheet_metal) | essential oils; rose water; alcohol (Islamic, ~800 CE) | — | — | n/a | — |
| `heavy_plough` | tech | A6 | B | plough_ard AND iron_bloomery AND wheel_solid | cultivation of heavy soils; urbanisation of the north | `age:a6_heavy_plough` → Age 6 milestone `a6_heavy_plough` (core) | yes | yes | Age |
| `horse_collar` | tech | A6 | C | collar_harness | horse-drawn plough; horse haulage at oxen load, twice the speed | — | — | n/a | — |
| `horseshoe` | tech | A6 | C | iron_bloomery AND horse_domestication | sustained horse use on roads | — | — | n/a | — |
| `rice_champa` | tech | A6 | C | rice_wet | two harvests per year; population from 60M to 120M | — | — | n/a | — |
| `sugar_refining` | tech | A6 | C | root_crop AND pottery_open_fired | storable sweetener; plantation economy (later) | — | — | n/a | — |
| `windmill_vertical` | tech | A6 | J | water_mill | milling in windy arid land | `building.windmill` → no realizing system | yes | deferred | — |
| `windmill_post` | tech | A6 | B (BJ) | water_mill AND sail_square | power without river head; drainage (Netherlands) | `age:a6_mills` → Age 6 milestone `a6_mills` (supporting)<br>`building.windmill` → no realizing system | yes | partly (rest deferred) | Age |
| `fulling_mill` | tech | A6 | J | trip_hammer AND loom_warp_weighted | cloth finishing at scale; relocation of textile industry to rivers | `building.fulling_mill` → no realizing system | yes | deferred | — |
| `spinning_wheel` | tech | A6 | C | spinning_spindle AND wheel_solid | thread supply keeping pace with looms | — | — | n/a | — |
| `horizontal_loom` | tech | A5 | C | loom_warp_weighted | professional weaving; guild cloth industry | — | — | n/a | — |
| `cotton_gin_roller` | tech | A6 | C | wheel_solid AND cotton_cultivation | cotton as a mass fibre | — | — | n/a | — |
| `compass_magnetic` | tech | A6 | C | iron_bloomery | navigation under cloud; dead reckoning | — | — | n/a | — |
| `stern_rudder` | tech | A6 | C | mortise_hull OR plank_boat | larger ships; precise steering | — | — | n/a | — |
| `watertight_bulkhead` | tech | A6 | C | stern_rudder | survivable ocean hulls | — | — | n/a | — |
| `portolan` | tech | A6 | C | compass_magnetic AND cartography | coastal navigation by bearing and distance | — | — | n/a | — |
| `astrolabe` | tech | A6 | J | astronomy_geometric | latitude; prayer times; surveying | `building.observatory` → no realizing system | yes | deferred | — |
| `cog` | tech | A6 | C | stern_rudder AND plank_boat | bulk grain and timber trade in the Baltic and North Sea | — | — | n/a | — |
| `gunpowder` | tech | A6 | C | alchemy | incendiaries; fire lances; bombs | — | — | n/a | — |
| `saltpetre_refining` | tech | A6 | C | gunpowder | reliable high-nitrate powder | — | — | n/a | — |
| `cannon_early` | tech | A6 | F | saltpetre_refining AND (casting_closed OR iron_bloomery) | projectile artillery (weak at first) | `unit.artillery` → no realizing system | yes | deferred | — |
| `trebuchet` | tech | A6 | F | traction_trebuchet | reduction of stone castles | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `longbow` | tech | A6 | F | bow_simple | massed infantry archery | `unit.longbowmen` → no realizing system | yes | deferred | — |
| `plate_armour` | tech | A6 | C | tempering AND sheet_metal | near-immunity to arrows and blades | — | — | n/a | — |
| `greek_fire` | tech | A6 | C | distillation | naval incendiary | — | — | n/a | — |
| `woodblock_print` | tech | A6 | J | paper AND stamp_seal | mass religious texts; paper money (Song); printed calendars | `building.printing_house` → no realizing system | yes | deferred | — |
| `movable_type_ceramic` | tech | A6 | C | woodblock_print AND kiln_updraft | reusable type (limited by character count) | — | — | n/a | — |
| `movable_type_metal` | tech | A6 | C | movable_type_ceramic AND casting_closed | durable reusable type | — | — | n/a | — |
| `paper_money` | tech | A6 | J | woodblock_print AND coinage_electrum | money not limited by metal; inflation (Yuan) | `inst.central_bank` → no realizing system | yes | deferred | — |
| `hindu_arabic` | tech | A6 | B | brahmi | written arithmetic; algebra; commercial calculation | `age:a6_numerals` → Age 6 milestone `a6_numerals` (supporting) | yes | yes | Age |
| `algebra` | tech | A6 | D (DG) | (hindu_arabic OR counting_rods) AND geometry_practical | inheritance calculation; engineering; later: all quantitative science | `building.university` → project `military university`<br>`building.university` → project `medical university`<br>`building.university` → project `engineering university`<br>`building.university` → project `natural science university`<br>`building.university` → project `agricultural university`<br>`stage` → research.json researchStage.requires | yes | yes | Construction, Research |
| `trigonometry` | tech | A6 | C | astronomy_geometric AND (hindu_arabic OR numeral_sexagesimal) | astronomical tables; navigation (later); surveying | — | — | n/a | — |
| `optics_ibn_haytham` | tech | A6 | J | geometry_axiomatic AND glass_blowing | theory of vision; experimental method as a stated procedure | `inst.scientific_society` → no realizing system | yes | deferred | — |
| `spectacles` | tech | A6 | C | optics_ibn_haytham AND window_glass | corrected vision; telescope (later) | — | — | n/a | — |
| `crank_connecting_rod` | tech | A6 | C | gearing AND water_mill | sawmills; pumps; bellows | — | — | n/a | — |
| `escapement_verge` | tech | A6 | C | gearing AND water_clock | uniform hours; the precision-mechanism craft tradition | — | — | n/a | — |
| `blast_furnace_water` | tech | A6 | J | iron_bloomery AND crank_connecting_rod AND bellows | cast iron in Europe; cannon casting | `building.blast_furnace` → no realizing system | yes | deferred | — |
| `wire_drawing` | tech | A6 | C | iron_bloomery AND crank_connecting_rod | mail armour; pins; cards for wool | — | — | n/a | — |
| `canal_lock` | tech | A6 | C | irrigation_canal AND timber_frame | navigable canals over hills; Grand Canal at full function | — | — | n/a | — |
| `double_entry` | tech | A6 | B | hindu_arabic AND paper AND bill_of_exchange | auditable accounts; capital as a measured quantity; banking (next) | `age:a6_finance` → Age 6 milestone `a6_finance` (supporting) | yes | yes | Age |
| `bill_of_exchange` | tech | A6 | B | coinage_electrum AND (abjad OR chinese_script) | long-distance payment; credit; the merchant network as infrastructure | `age:a6_finance` → Age 6 milestone `a6_finance` (supporting) | yes | yes | Age |
| `printing_press` | tech | A7 | B (BJ) | type_founding AND mechanics_archimedean AND paper | books at 1/100 the cost; the public sphere | `age:a7_printing` → Age 7 milestone `a7_printing` (core)<br>`building.printing_house` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system via `inst.mass_schooling`<br>`inst.mass_media_broadcast` → no realizing system via `inst.newspaper`<br>`inst.mass_schooling` → no realizing system<br>`inst.mass_schooling` → no realizing system via `inst.newspaper`<br>`inst.newspaper` → no realizing system<br>`inst.scientific_society` → no realizing system | yes | partly (rest deferred) | Age |
| `type_founding` | tech | A7 | C | casting_closed | standardised parts at scale | — | — | n/a | — |
| `caravel` | tech | A7 | B (BJ) | lateen AND stern_rudder AND compass_magnetic | coastal exploration against wind; Atlantic islands; West Africa | `activity.ocean_shipping` → no realizing system<br>`age:a7_ocean` → Age 7 milestone `a7_ocean` (supporting) | yes | partly (rest deferred) | Age |
| `carrack` | tech | A7 | J | cog AND lateen AND compass_magnetic | transoceanic cargo; Columbus, da Gama | `activity.ocean_shipping` → no realizing system<br>`building.dry_dock` → no realizing system | yes | deferred | — |
| `polynesian_canoe` | tech | A6 | J | dugout AND latitude_sailing | open-ocean colonisation over 3,000 km | `activity.ocean_shipping` → no realizing system | yes | deferred | — |
| `celestial_navigation` | tech | A7 | C | astrolabe AND trigonometry | open-ocean position finding | — | — | n/a | — |
| `longitude_problem` | tech | A7 | C | celestial_navigation AND escapement_verge | east-west position at sea; safe ocean routing | — | — | n/a | — |
| `mercator` | tech | A7 | C | cartography AND portolan AND trigonometry | practical ocean charts | — | — | n/a | — |
| `corned_powder` | tech | A7 | B | saltpetre_refining | reliable cannon; handguns | `age:a7_powder` → Age 7 milestone `a7_powder` (supporting) | yes | yes | Age |
| `cannon_cast_bronze` | tech | A7 | F | cannon_early AND casting_closed | breaching stone walls; end of the castle | `unit.artillery` → no realizing system | yes | deferred | — |
| `cannon_cast_iron` | tech | A7 | F | cannon_cast_bronze AND blast_furnace_water | cheap naval and siege guns | `unit.artillery` → no realizing system | yes | deferred | — |
| `matchlock` | tech | A7 | F | cannon_early AND corned_powder | massed infantry fire | `unit.musketeers` → no realizing system | yes | deferred | — |
| `wheellock` | tech | A7 | C | matchlock AND escapement_verge | pistol; mounted firearms | — | — | n/a | — |
| `flintlock` | tech | A7 | F | wheellock | universal infantry musket; bayonet infantry (with next) | `unit.musketeers` → no realizing system | yes | deferred | — |
| `trace_italienne` | tech | A7 | J | cannon_cast_bronze AND geometry_axiomatic AND stone_fortification | cannon-resistant fortification | `building.bastion_fort` → no realizing system | yes | deferred | — |
| `ship_of_line` | tech | A7 | F | carrack AND cannon_cast_iron | sea control; naval blockade | `unit.ship_of_the_line` → no realizing system | yes | deferred | — |
| `heliocentrism` | tech | A7 | C | astronomy_geometric AND trigonometry | accurate planetary prediction | — | — | n/a | — |
| `telescope` | tech | A7 | J | spectacles | Jupiter's moons; naval reconnaissance (D-039 B) | `building.observatory` → no realizing system | yes | deferred | — |
| `microscope` | tech | A7 | C | spectacles | cells; microorganisms | — | — | n/a | — |
| `mechanics_newtonian` | tech | A7 | B | heliocentrism AND algebra AND calculus | engineering from first principles; ballistics; celestial mechanics | `age:a7_science` → Age 7 milestone `a7_science` (supporting) | yes | yes | Age |
| `calculus` | tech | A7 | J | algebra AND geometry_axiomatic | physics; engineering; economics (later) | `inst.statistics_vital` → no realizing system | yes | deferred | — |
| `barometer_vacuum` | tech | A7 | C | glass_blowing AND mechanics_archimedean | weather prediction; the concept behind the atmospheric engine | — | — | n/a | — |
| `steam_atmospheric` | tech | A7 | C | barometer_vacuum AND cast_iron | mine drainage; coal mining at depth | — | — | n/a | — |
| `coke` | tech | A7 | C | blast_furnace_water | iron at industrial scale; cheap cast iron | — | — | n/a | — |
| `brass_calamine` | tech | A5 | C | copper_smelting | scientific instruments; precision parts | — | — | n/a | — |
| `inoculation` | tech | A7 | C | medicine_hippocratic | immunisation | — | — | n/a | — |
| `anatomy_dissection` | tech | A7 | C | medicine_hippocratic AND printing_press | accurate surgery | — | — | n/a | — |
| `crop_rotation_norfolk` | tech | A7 | C | legume AND cereal_domesticated | yield +50%; livestock doubled; labour freed for industry | — | — | n/a | — |
| `seed_drill_tull` | tech | A7 | C | seed_drill OR (plough_ard AND horse_collar) | seed economy; horse-hoeing | — | — | n/a | — |
| `selective_breeding` | tech | A7 | C | crop_rotation_norfolk AND (sheep_goat OR cattle) | larger animals; faster maturation | — | — | n/a | — |
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
| `interchangeable_parts` | tech | A8 | C | slide_rest_lathe | gauge-and-jig manufacture of identical parts; field repair; the American system of manufactures | — | — | n/a | — |
| `milling_machine` | tech | A8 | C | slide_rest_lathe | gun parts; gears at scale | — | — | n/a | — |
| `flying_shuttle` | tech | A8 | C | horizontal_loom | faster weaving; demand pressure on spinning | — | — | n/a | — |
| `spinning_jenny` | tech | A8 | C | spinning_wheel | thread ×8 per worker | — | — | n/a | — |
| `water_frame` | tech | A8 | J | spinning_wheel AND water_mill | cotton thread at industrial scale; the factory system | `building.textile_mill` → no realizing system | yes | deferred | — |
| `spinning_mule` | tech | A8 | C | spinning_jenny AND water_frame | fine cotton competitive with India | — | — | n/a | — |
| `power_loom` | tech | A8 | J | flying_shuttle AND (water_mill OR steam_rotary) | cloth at industrial scale | `building.textile_mill` → no realizing system | yes | deferred | — |
| `cotton_gin_saw` | tech | A8 | C | cotton_gin_roller AND wire_drawing | short-staple cotton economic | — | — | n/a | — |
| `jacquard` | tech | A8 | C | drawloom AND paper | complex patterns without a drawboy; stored program (concept) | — | — | n/a | — |
| `locomotive` | tech | A8 | C | steam_high_pressure | overland speed; bulk overland transport | — | — | n/a | — |
| `railway` | tech | A8 | B (BJ) | locomotive AND puddling AND surveying | national markets; standard time; military mobilisation by rail | `age:a8_rail` → Age 8 milestone `a8_rail` (supporting)<br>`infra.railway` → no realizing system | yes | partly (rest deferred) | Age |
| `steamboat` | tech | A8 | C | steam_rotary AND mortise_hull | river commerce against current; scheduled ocean crossing | — | — | n/a | — |
| `screw_propeller` | tech | A8 | F | steamboat AND water_screw | ocean steamships; steam warships | `unit.ironclad` → no realizing system | yes | deferred | — |
| `iron_hull` | tech | A8 | F | puddling AND steamboat | ships beyond timber limits; ironclads | `unit.ironclad` → no realizing system | yes | deferred | — |
| `macadam` | tech | A8 | E | road_paved | macadamised road class: compacted graded stone, all-weather coaching | `infra.road_macadam` → road class `infra.road_macadam` | yes | yes | Roads |
| `bicycle` | tech | A8 | C | interchangeable_parts AND steel_carburized | personal transport; the precision-parts industry that built cars and aircraft | — | — | n/a | — |
| `chemistry_quantitative` | tech | A8 | J | distillation AND barometer_vacuum | predictable chemical process; all later industrial chemistry | `building.research_laboratory` → no realizing system | yes | deferred | — |
| `sulphuric_acid` | tech | A8 | J | distillation AND saltpetre_refining | bleaching; soda; fertiliser; explosives | `building.chemical_works` → no realizing system | yes | deferred | — |
| `soda_leblanc` | tech | A8 | C | sulphuric_acid AND chemistry_quantitative | glass and soap at scale; pollution (the Alkali Act 1863) | — | — | n/a | — |
| `bleach_chlorine` | tech | A8 | C | sulphuric_acid | cotton finishing at industrial pace | — | — | n/a | — |
| `synthetic_dye` | tech | A8 | C | organic_chemistry AND coke AND dyeing | cheap colour; the corporate research laboratory | — | — | n/a | — |
| `portland_cement` | tech | A8 | J | concrete_pozzolan AND kiln_updraft AND chemistry_quantitative | concrete everywhere | `building.cement_works` → no realizing system | yes | deferred | — |
| `reinforced_concrete` | tech | A8 | J | portland_cement AND bessemer | frames, bridges, high-rise | `infra.sewerage_system` → no realizing system | yes | deferred | — |
| `haber_bosch` | tech | A8 | C | chemistry_quantitative AND open_hearth AND thermodynamics | yield ceiling raised ~2×; population beyond organic limit | — | — | n/a | — |
| `dynamite` | tech | A8 | C | sulphuric_acid AND organic_chemistry | hard-rock excavation at scale; Alpine tunnels | — | — | n/a | — |
| `electromagnetism` | tech | A8 | C | calculus | motor; generator; telegraph | — | — | n/a | — |
| `telegraph` | tech | A8 | B (BJ) | electromagnetism AND wire_drawing | near-instant long-distance messages; D-039 A3 order latency collapse | `age:a8_telegraph` → Age 8 milestone `a8_telegraph` (supporting)<br>`infra.telegraph_line` → no realizing system | yes | partly (rest deferred) | Age |
| `submarine_cable` | tech | A8 | J | telegraph AND steamboat | global information network | `infra.submarine_cable` → no realizing system | yes | deferred | — |
| `dynamo` | tech | A8 | C | electromagnetism AND steam_rotary | electricity as a produced CAPACITY | — | — | n/a | — |
| `electric_light` | tech | A8 | C | dynamo AND barometer_vacuum | night work; urban lighting demand | — | — | n/a | — |
| `electricity_generation` | tech | A8 | B (BJ) | dynamo AND electric_light AND electromagnetism | electricity as CAPACITY (ADR-019 §5); electric motors; all of the 20th century | `age:a9_electricity` → Age 9 milestone `a9_electricity` (core)<br>`building.power_station` → no realizing system<br>`infra.power_grid` → no realizing system | yes | partly (rest deferred) | Age |
| `electric_motor` | tech | A8 | C | electricity_generation | unit drive; electric traction | — | — | n/a | — |
| `telephone` | tech | A8 | J | telegraph | real-time conversation at distance | `infra.telephone_network` → no realizing system | yes | deferred | — |
| `internal_combustion` | tech | A8 | B | gas_engine AND petroleum_refining | motor vehicle; aircraft (next); tank (next) | `age:a9_combustion` → Age 9 milestone `a9_combustion` (core) | yes | yes | Age |
| `petroleum_refining` | tech | A8 | J | chemistry_quantitative | lamp oil; fuel for ICE | `building.refinery` → no realizing system | yes | deferred | — |
| `vaccination` | tech | A8 | C | inoculation | smallpox control; eradication (1980) | — | — | n/a | — |
| `anaesthesia` | tech | A8 | C | chemistry_quantitative AND anatomy_dissection | long, deliberate surgery | — | — | n/a | — |
| `germ_theory` | tech | A8 | B (BJ) | microscope | antisepsis; targeted vaccines; public health engineering | `age:a8_germ` → Age 8 milestone `a8_germ` (supporting)<br>`infra.sewerage_system` → no realizing system | yes | partly (rest deferred) | Age |
| `antisepsis` | tech | A8 | C | germ_theory AND anaesthesia | abdominal surgery; survival of wounds | — | — | n/a | — |
| `photography` | tech | A8 | C | chemistry_quantitative AND glass_lead | visual record; D-039 reconnaissance by camera (later) | — | — | n/a | — |
| `evolution` | tech | A8 | C | selective_breeding AND taxonomy AND stratigraphy | biology as a science; breeding theory | — | — | n/a | — |
| `aircraft` | tech | A9 | F (FJ) | internal_combustion AND glider | aerial reconnaissance (D-039 B: reconnaissance no longer walks); bombing; air transport | `building.airfield` → no realizing system<br>`unit.aircraft_carrier` → no realizing system<br>`unit.military_aircraft` → no realizing system | yes | deferred | — |
| `aircraft_metal` | tech | A9 | C | aircraft AND aluminium | airliners; strategic bombers | — | — | n/a | — |
| `jet_engine` | tech | A9 | C | aircraft_metal AND steam_turbine | jet aircraft; global air travel | — | — | n/a | — |
| `steam_turbine` | tech | A9 | F | steam_high_pressure AND open_hearth AND thermodynamics | efficient generation; Dreadnought speed | `unit.aircraft_carrier` → no realizing system | yes | deferred | — |
| `automobile_mass` | tech | A9 | C | internal_combustion AND interchangeable_parts AND petroleum_refining | suburb; trucking; the road economy; moving assembly line (Ford, 1913); mass production | — | — | n/a | — |
| `tank` | tech | A9 | F | internal_combustion AND basic_process AND rifled_artillery AND machine_gun AND mechanised_agriculture | mobile armoured warfare | `unit.tank` → no realizing system | yes | deferred | — |
| `diesel` | tech | A9 | C | gas_engine AND open_hearth AND thermodynamics | heavy transport; submarines; generators | — | — | n/a | — |
| `aluminium` | tech | A9 | C | electricity_generation AND soda_leblanc | light structural metal; aircraft | — | — | n/a | — |
| `superalloy` | tech | A9 | C | open_hearth AND metallography | jet engines; gas turbines | — | — | n/a | — |
| `rocket` | tech | A9 | F (FJ) | internal_combustion AND aluminium AND mechanics_newtonian | ballistic missile; satellite launch | `building.spaceport` → no realizing system<br>`project.orbital_launch` → no realizing system<br>`unit.missile` → no realizing system | yes | deferred | — |
| `satellite` | tech | A9 | C | orbital_launch_vehicle AND computer AND transistor | orbital observation (a pointed observer, never a map reveal — D-039 B3, D-040 B3); orbit insertion, tracking and telemetry | — | — | n/a | — |
| `container_shipping` | tech | A9 | C | iron_hull AND diesel | near-zero bulk transport cost by sea | — | — | n/a | — |
| `radio` | tech | A8 | B (BJ) | electromagnetic_field_theory AND telegraph | wireless command (D-039 A); ship-to-shore wireless; broadcast media (D-041 C1) | `age:a9_radio` → Age 9 milestone `a9_radio` (supporting)<br>`building.radio_transmitter` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system | yes | partly (rest deferred) | Age |
| `vacuum_tube` | tech | A9 | C | electric_light AND radio | voice radio; long-distance telephone; first computers | — | — | n/a | — |
| `radar` | tech | A9 | C | vacuum_tube AND electromagnetic_field_theory | air defence; navigation; D-039 B reconnaissance at radio range | — | — | n/a | — |
| `television` | tech | A9 | J | radio_broadcasting AND photography | mass visual media (D-041 C1) | `inst.mass_media_broadcast` → no realizing system | yes | deferred | — |
| `computer` | tech | A9 | B | programmable_computer AND radar | programmable computation; ballistics, codebreaking, then everything | `age:a9_computing` → Age 9 milestone `a9_computing` (supporting) | yes | yes | Age |
| `transistor` | tech | A9 | C | vacuum_tube AND semiconductor_physics | reliable small electronics | — | — | n/a | — |
| `integrated_circuit` | tech | A9 | J | transistor AND photography | cheap computation; the microprocessor | `building.semiconductor_fab` → no realizing system | yes | deferred | — |
| `microprocessor` | tech | A9 | C | integrated_circuit AND computer | personal computing; embedded control everywhere | — | — | n/a | — |
| `internet` | tech | A9 | J | computer AND telephone AND integrated_circuit | global instant information; D-039 A2 lag collapse; D-041 media at global scale | `infra.internet_backbone` → no realizing system | yes | deferred | — |
| `quantum_mechanics` | tech | A9 | C | electromagnetic_field_theory AND thermodynamics AND spectroscopy AND periodic_system AND radioactivity | semiconductors; nuclear physics; chemistry explained | — | — | n/a | — |
| `nuclear_fission` | tech | A9 | C | nuclear_physics | controlled self-sustaining chain reaction; critical assembly (research pile) | — | — | n/a | — |
| `nuclear_power` | tech | A9 | J | (graphite_reactor OR heavy_water_reactor OR light_water_reactor OR fast_breeder_reactor) AND steam_turbine AND electricity_generation | baseload electricity without fuel imports | `building.nuclear_power_station` → no realizing system | yes | deferred | — |
| `thermonuclear` | tech | A9 | C | fission_weapon | mutual assured destruction | — | — | n/a | — |
| `antibiotic_penicillin` | tech | A9 | B | germ_theory AND fermentation_grain | wound survival; maternal mortality collapse | `age:a9_antibiotics` → Age 9 milestone `a9_antibiotics` (supporting) | yes | yes | Age |
| `antibiotic_broad` | tech | A9 | C | antibiotic_penicillin | tuberculosis curable; routine surgery | — | — | n/a | — |
| `antibiotic_resistance` | tech | A9 | C | antibiotic_broad AND evolution AND epidemiology | sustaining antibiotic efficacy | — | — | n/a | — |
| `vaccine_lab` | tech | A9 | C | germ_theory AND vaccination | childhood mortality collapse | — | — | n/a | — |
| `blood_transfusion` | tech | A9 | C | immunology AND physiology | surgical and trauma survival | — | — | n/a | — |
| `refrigeration` | tech | A8 | C | chemistry_quantitative AND steam_rotary AND thermodynamics | meat by sea from Argentina; spoilage rate near zero | — | — | n/a | — |
| `canning` | tech | A8 | C | glass_blowing AND sheet_metal | army and navy provisions; spoilage to near zero | — | — | n/a | — |
| `green_revolution` | tech | A9 | C | scientific_plant_breeding AND chemical_fertilizer AND haber_bosch AND irrigation_canal | famine ended in Asia | — | — | n/a | — |
| `genetics_mendel` | tech | A9 | C | cell_theory AND probability_statistics | scientific breeding; molecular biology (next) | — | — | n/a | — |
| `dna_structure` | tech | A9 | C | genetics_mendel AND quantum_mechanics AND xray | genetic engineering; forensics; medicine by genome | — | — | n/a | — |
| `genetic_engineering` | tech | A9 | C | dna_structure AND antibiotic_broad | synthetic insulin; GM crops; vaccines by design | — | — | n/a | — |
| `hormonal_contraception` | tech | A9 | C | endocrinology AND synthetic_dye | fertility as a chosen variable | — | — | n/a | — |
| `xray` | tech | A9 | C | electromagnetism AND barometer_vacuum AND photography | diagnosis; crystallography (DNA) | — | — | n/a | — |
| `plastics` | tech | A9 | C | petroleum_refining AND chemistry_quantitative AND synthetic_dye | cheap durable materials; textiles without fibre crops | — | — | n/a | — |
| `electric_traction` | tech | A9 | C | electric_motor AND railway | city of 10M; commuting | — | — | n/a | — |
| `skyscraper` | tech | A9 | J | structural_iron AND bessemer AND electric_motor | density without sprawl | `building.skyscraper` → no realizing system | yes | deferred | — |
| `hydroelectric` | tech | A9 | J | ac_transmission AND reinforced_concrete AND water_turbine | cheap electricity; aluminium (energy-gated) | `building.hydroelectric_dam` → no realizing system | yes | deferred | — |
| `mechanised_agriculture` | tech | A9 | C | internal_combustion AND seed_drill_tull AND farm_machinery | farm labour share to <5% | — | — | n/a | — |
| `numerical_control` | tech | A9 | C | computer AND milling_machine | flexible automation | — | — | n/a | — |
| `mobile_phone` | tech | A9 | J | radio_broadcasting AND microprocessor AND automatic_switching | ubiquitous communication | `infra.mobile_network` → no realizing system | yes | deferred | — |
| `gps` | tech | A9 | C | satellite AND microprocessor AND atomic_clock AND relativity | D-040 B1 extent: known everywhere; precision agriculture; guided munitions | — | — | n/a | — |
| `atomic_clock` | tech | A9 | C | quantum_mechanics AND vacuum_tube | navigation; network synchronisation | — | — | n/a | — |
| `solar_pv` | tech | A9 | J | semiconductor_physics AND transistor | distributed generation; energy without fuel or river | `building.solar_farm` → no realizing system | yes | deferred | — |
| `lithium_battery` | tech | A9 | C | chemistry_quantitative AND quantum_mechanics | portable electronics; electric vehicles; grid storage | — | — | n/a | — |
| `relativity` | tech | A9 | C | electromagnetic_field_theory AND mechanics_newtonian | mass-energy equivalence; orbital timing corrections (GPS) | — | — | n/a | — |
| `laser` | tech | A9 | J | quantum_mechanics AND vacuum_tube | coherent light; precision cutting; optical communication | `project.probe_acceleration` → no realizing system | yes | deferred | — |
| `fibre_optics` | tech | A9 | C | laser AND glass_lead | high-bandwidth long-distance communication | — | — | n/a | — |
| `cmos_vlsi` | tech | A9 | C | microprocessor | cheap abundant computation; embedded control everywhere | — | — | n/a | — |
| `superconductivity_applied` | tech | A9 | C | quantum_mechanics AND refrigeration | high-field magnets; MRI; particle accelerators; fusion confinement | — | — | n/a | — |
| `composites` | tech | A9 | C | plastics | light strong structures; turbine blades; airframes | — | — | n/a | — |
| `gas_turbine_power` | tech | A9 | C | jet_engine AND electricity_generation AND superalloy | efficient dispatchable generation | — | — | n/a | — |
| `wind_turbine_modern` | tech | A9 | J | composites AND ac_transmission | generation capacity from wind | `building.wind_farm` → no realizing system | yes | deferred | — |
| `nuclear_fusion_research` | tech | A9 | C | nuclear_physics AND (superconductivity_applied OR (thermonuclear AND laser)) | plasma confinement; fusion science | — | — | n/a | — |
| `grid_storage` | tech | A9 | C | ac_transmission AND (lithium_battery OR hydroelectric) | time-shifting generation; stability with intermittent sources | — | — | n/a | — |
| `smart_grid` | tech | A9 | C | internet AND ac_transmission | demand response; distributed generation | — | — | n/a | — |
| `programming_languages` | tech | A9 | C | computer | software at scale | — | — | n/a | — |
| `operating_system` | tech | A9 | C | programming_languages | multi-user computing; software portability | — | — | n/a | — |
| `relational_database` | tech | A9 | C | programming_languages | administration at national scale; transaction systems | — | — | n/a | — |
| `personal_computer` | tech | A9 | C | cmos_vlsi AND operating_system | individual computing; office automation | — | — | n/a | — |
| `public_key_crypto` | tech | A9 | C | programming_languages | secure networks; digital signatures; e-commerce | — | — | n/a | — |
| `gpu_parallel` | tech | A9 | C | cmos_vlsi | large-scale numerical computation | — | — | n/a | — |
| `machine_learning` | tech | A9 | C | programming_languages AND probability_statistics | pattern recognition; prediction from data | — | — | n/a | — |
| `deep_learning` | tech | A9 | C | machine_learning AND gpu_parallel | machine perception; automated translation | — | — | n/a | — |
| `large_language_models` | tech | A9 | C | deep_learning AND internet | machine-generated text and code; knowledge assistance | — | — | n/a | — |
| `cloud_computing` | tech | A9 | J | internet AND relational_database | elastic computation; global services | `building.data_centre` → no realizing system | yes | deferred | — |
| `smartphone` | tech | A9 | C | cellular_digital AND personal_computer AND lithium_battery | ubiquitous computing; mobile internet | — | — | n/a | — |
| `cyber_warfare` | tech | A9 | C | internet AND public_key_crypto | network attack and defence; infrastructure sabotage | — | — | n/a | — |
| `quantum_computing` | tech | A9 | C | quantum_mechanics AND cmos_vlsi AND superconductivity_applied | quantum simulation (limited) | — | — | n/a | — |
| `cellular_digital` | tech | A9 | C | mobile_phone AND cmos_vlsi | mobile data | — | — | n/a | — |
| `crewed_spaceflight` | tech | A9 | C | satellite AND computer | human operations in orbit | — | — | n/a | — |
| `lunar_flight` | tech | A9 | C | crewed_spaceflight AND integrated_circuit | crewed deep-space operations | — | — | n/a | — |
| `space_station` | tech | A9 | C | crewed_spaceflight | permanent orbital presence; microgravity research | — | — | n/a | — |
| `space_probe` | tech | A9 | C | satellite AND computer | exploration of other worlds | — | — | n/a | — |
| `reusable_launch` | tech | A9 | C | orbital_launch_vehicle AND gps AND composites | cheap access to orbit | — | — | n/a | — |
| `space_telescope` | tech | A9 | C | satellite AND telescope | observation across the spectrum; exoplanet detection | — | — | n/a | — |
| `earth_observation` | tech | A9 | C | satellite AND photography | global mapping; crop and resource monitoring | — | — | n/a | — |
| `organ_transplant` | tech | A9 | C | antisepsis AND blood_transfusion | organ failure survivable | — | — | n/a | — |
| `chemotherapy` | tech | A9 | C | sulfonamides | cancer treatment | — | — | n/a | — |
| `medical_imaging` | tech | A9 | C | xray AND computer | non-invasive diagnosis | — | — | n/a | — |
| `antiviral_drugs` | tech | A9 | C | dna_structure AND chemistry_quantitative | viral disease treatable | — | — | n/a | — |
| `monoclonal_antibodies` | tech | A9 | C | dna_structure AND immunology | targeted therapy; precise diagnostics | — | — | n/a | — |
| `pcr` | tech | A9 | C | dna_sequencing | genetic testing; forensics | — | — | n/a | — |
| `genome_sequencing` | tech | A9 | C | pcr AND computer | genetic medicine; breeding by genome | — | — | n/a | — |
| `ivf` | tech | A9 | C | endocrinology | assisted reproduction | — | — | n/a | — |
| `crispr` | tech | A9 | C | genome_sequencing AND genetic_engineering | precise genetic modification | — | — | n/a | — |
| `mrna_vaccines` | tech | A9 | C | genetic_engineering AND vaccine_lab AND genome_sequencing | rapid vaccine design | — | — | n/a | — |
| `oral_rehydration` | tech | A9 | C | germ_theory AND chemistry_quantitative | child mortality from diarrhoea collapses | — | — | n/a | — |
| `drip_irrigation` | tech | A9 | C | plastics AND (irrigation_basin OR irrigation_canal) | crops on arid land with little water | — | — | n/a | — |
| `precision_agriculture` | tech | A9 | C | gps AND mechanised_agriculture | input efficiency per field | — | — | n/a | — |
| `desalination` | tech | A9 | J | electricity_generation AND (distillation OR plastics) | water supply independent of rivers and rain | `building.desalination_plant` → no realizing system | yes | deferred | — |
| `catalytic_converter` | tech | A9 | C | chemistry_quantitative AND automobile_mass | vehicle emission control | — | — | n/a | — |
| `weather_prediction` | tech | A9 | C | computer AND radio | harvest and disaster forecasting | — | — | n/a | — |
| `carbon_capture` | tech | A9 | C | chemistry_quantitative AND gas_turbine_power | emission removal (limited scale) | — | — | n/a | — |
| `high_speed_rail` | tech | A9 | C | electric_traction AND reinforced_concrete | intercity travel faster than air over medium distance | — | — | n/a | — |
| `jet_airliner` | tech | A9 | C | jet_engine AND aircraft_metal AND superalloy | mass international travel | — | — | n/a | — |
| `electric_vehicle` | tech | A9 | C | lithium_battery AND electric_motor AND automobile_mass | road transport without oil | — | — | n/a | — |
| `helicopter` | tech | A9 | F | aircraft | vertical lift; air mobility without runways | `unit.helicopter` → no realizing system | yes | deferred | — |
| `icbm` | tech | A9 | F | rocket AND thermonuclear AND computer | intercontinental strike | `unit.icbm` → no realizing system | yes | deferred | — |
| `nuclear_submarine` | tech | A9 | F | light_water_reactor AND submarine | undersea deterrence; sea denial | `unit.nuclear_submarine` → no realizing system | yes | deferred | — |
| `guided_munitions` | tech | A9 | C | laser AND computer AND guided_missile | precision strike | — | — | n/a | — |
| `stealth` | tech | A9 | F | composites AND radar AND computer AND aircraft | penetration of air defences | `unit.stealth_aircraft` → no realizing system | yes | deferred | — |
| `combat_drone` | tech | A9 | F | gps AND microprocessor AND aircraft | persistent surveillance; strike without aircrew | `unit.combat_drone` → no realizing system | yes | deferred | — |
| `missile_defence` | tech | A9 | C | radar AND guided_munitions | defence against ballistic and cruise missiles | — | — | n/a | — |
| `additive_manufacturing` | tech | A9 | C | numerical_control AND plastics | complex parts without tooling | — | — | n/a | — |
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
| `cotton_cultivation` | tech | A2 | C | cereal_cultivation OR root_crop | cotton fibre crop; cotton textiles (spinning, with spinning_spindle) | — | — | n/a | — |
| `ashlar_masonry` | tech | A3 | J | stone_dry AND copper_smelting | dressed-stone walls, temples and towers; stone voussoirs for arches, bridges and vaults | `building.lighthouse` → no realizing system | yes | deferred | — |
| `cupellation` | tech | A3 | C | copper_smelting AND gold_silver_native | silver separated from argentiferous lead by oxidizing the lead to litharge; fire assay of gold and silver (cupellation at small scale) | — | — | n/a | — |
| `mesoamerican_script` | tech | A3 | D (DJ) | calendar_lunar | full writing grown from calendrical notation; dated dynastic monuments; bark-paper books | `building.library` → no realizing system<br>`building.school` → no realizing system<br>`inst.aramaic_admin` → no realizing system via `inst.archive`<br>`inst.archive` → no realizing system<br>`inst.guild` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system<br>`inst.historiography` → no realizing system via `inst.archive`<br>`inst.historiography` → no realizing system via `inst.scribal_school`<br>`inst.library` → no realizing system via `inst.archive`<br>`inst.library` → no realizing system via `inst.scribal_school`<br>`inst.natural_philosophy` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.archive`<br>`inst.scientific_society` → no realizing system via `inst.scribal_school`<br>`inst.scribal_school` → no realizing system<br>`inst.textile_workshop` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.archive`<br>`inst.translation_movement` → no realizing system via `inst.scribal_school`<br>`inst.university` → project `military university` founds via `inst.archive`<br>`inst.university` → project `medical university` founds via `inst.archive`<br>`inst.university` → project `engineering university` founds via `inst.archive`<br>`inst.university` → project `natural science university` founds via `inst.archive`<br>`inst.university` → project `agricultural university` founds via `inst.archive`<br>`inst.university` → project `military university` founds via `inst.scribal_school`<br>`inst.university` → project `medical university` founds via `inst.scribal_school`<br>`inst.university` → project `engineering university` founds via `inst.scribal_school`<br>`inst.university` → project `natural science university` founds via `inst.scribal_school`<br>`inst.university` → project `agricultural university` founds via `inst.scribal_school` | yes | partly (rest deferred) | Construction |
| `ore_prospecting` | tech | A3 | C | copper_smelting | surface indications of ore (colour stains, gossans) and placer working | — | — | n/a | — |
| `counting_rods` | tech | A5 | C | chinese_script | decimal place-value calculation on a counting board (rod numerals, blank for zero); signed (red/black) numbers in calculation; root extraction and elimination for simultaneous linear problems (Nine Chapters methods) | — | — | n/a | — |
| `drawloom` | tech | A5 | C | silk AND horizontal_loom | figured (pattern-woven) silk and damask; a loom that stores a pattern in its harness | — | — | n/a | — |
| `traction_trebuchet` | tech | A5 | F | sling AND timber_frame | stone-throwing artillery by lever and sling (crew-hauled) | `unit.siege_engine` → no realizing system | yes | deferred | — |
| `ocean_junk` | tech | A6 | J | watertight_bulkhead AND compass_magnetic AND monsoon_sailing | oceanic transport class (junk); Indian Ocean crossings by compass and monsoon | `activity.ocean_shipping` → no realizing system | yes | deferred | — |
| `percussion_drilling` | tech | A6 | C | iron_bloomery AND well | cased boreholes hundreds of metres deep for brine and natural gas; drilled wells reach fluids no shaft can (later petroleum) | — | — | n/a | — |
| `rifling` | tech | A7 | C | matchlock | accurate aimed fire at long range (rifles) | — | — | n/a | — |
| `physiology` | tech | A7 | C | anatomy_dissection | circulation of the blood (Harvey 1628); experimental physiology | — | — | n/a | — |
| `optical_telegraph` | tech | A7 | C | telescope | state semaphore signal network; strategic signalling over hundreds of kilometres in hours | — | — | n/a | — |
| `mineralogy` | tech | A7 | C | ore_prospecting AND distillation | typing of unfamiliar mineral occurrences in observed land (physical properties, blowpipe and wet tests) | — | — | n/a | — |
| `probability_statistics` | tech | A7 | C | algebra | mathematics of chance and inference; statistical analysis of counts and errors | — | — | n/a | — |
| `taxonomy` | tech | A7 | C | printing_press | systematic naming and classification of species | — | — | n/a | — |
| `breech_loading_rifle` | tech | A8 | C | rifling AND flintlock AND chemistry_quantitative AND interchangeable_parts | rapid aimed fire from cover (breech-loading) | — | — | n/a | — |
| `machine_gun` | tech | A8 | C | breech_loading_rifle | sustained automatic fire | — | — | n/a | — |
| `rifled_artillery` | tech | A8 | C | rifling AND puddling AND slide_rest_lathe | accurate long-range guns firing explosive shells | — | — | n/a | — |
| `epidemiology` | tech | A8 | J | medicine_hippocratic AND probability_statistics | disease measured by rates in populations (surveillance); transmission routes identified by comparison; controlled clinical trials (method) | `inst.statistics_vital` → no realizing system | yes | deferred | — |
| `ac_transmission` | tech | A8 | C | electricity_generation | transformers and polyphase AC; high-voltage transmission over long distances | — | — | n/a | — |
| `automatic_switching` | tech | A8 | C | telephone | automatic telephone exchanges (dial service); electromechanical relay switching logic | — | — | n/a | — |
| `balloon` | tech | A8 | C | barometer_vacuum | lighter-than-air flight | — | — | n/a | — |
| `gas_engine` | tech | A8 | C | steam_rotary AND chemistry_quantitative AND milling_machine | boiler-less stationary power for workshops (gas engines on town gas); the four-stroke compression cycle | — | — | n/a | — |
| `glider` | tech | A8 | C | mechanics_newtonian | gliding flight; aerodynamic lift data and three-axis control | — | — | n/a | — |
| `metallography` | tech | A8 | C | microscope AND chemistry_quantitative | microstructure of metals read from polished and etched sections; iron-carbon constitution explains hardening, tempering and alloying | — | — | n/a | — |
| `punched_card_tabulation` | tech | A8 | C | jacquard AND telegraph | mechanised tabulation of census and administrative records | — | — | n/a | — |
| `sanitary_engineering` | tech | A8 | J | steam_rotary AND fired_brick | water-carriage sewerage and storm drainage; filtered, pressurised municipal water supply | `infra.sewerage_system` → no realizing system | yes | deferred | — |
| `structural_iron` | tech | A8 | C | coke | iron bridges; fire-resistant iron-framed mills and halls; prefabricated iron-and-glass buildings | — | — | n/a | — |
| `water_turbine` | tech | A8 | C | water_mill AND mechanics_newtonian | efficient water power at high heads (turbine-driven mills); the prime mover of hydroelectric stations | — | — | n/a | — |
| `cell_theory` | tech | A8 | C | microscope | cells as the units of life; cellular pathology | — | — | n/a | — |
| `electromagnetic_field_theory` | tech | A8 | C | electromagnetism | radio (theory); light as an electromagnetic wave | — | — | n/a | — |
| `organic_chemistry` | tech | A8 | C | chemistry_quantitative | carbon compounds analysed, synthesised and drawn as structures | — | — | n/a | — |
| `periodic_system` | tech | A8 | C | chemistry_quantitative | predictive ordering of the elements | — | — | n/a | — |
| `spectroscopy` | tech | A8 | C | telescope AND chemistry_quantitative | chemical analysis by spectral lines; composition of the stars | — | — | n/a | — |
| `stratigraphy` | tech | A8 | C | mineralogy AND surveying | geological maps and the prospectivity of host formations | — | — | n/a | — |
| `thermodynamics` | tech | A8 | C | steam_separate_condenser AND calculus | laws of energy and entropy; efficiency limits of heat engines; chemical equilibrium | — | — | n/a | — |
| `agricultural_chemistry` | tech | A8 | C | chemistry_quantitative | mineral theory of plant nutrition (N, P, K; law of the minimum); soil and manure analysis; replicated field experiments | — | — | n/a | — |
| `chemical_fertilizer` | tech | A8 | C | agricultural_chemistry AND sulphuric_acid | superphosphate and potash manufacture; mineral fertilizer application | — | — | n/a | — |
| `farm_machinery` | tech | A8 | C | (heavy_plough OR iron_mouldboard) AND horse_collar AND crank_connecting_rod | mechanical reaper and threshing machine; factory-made iron and steel ploughs | — | — | n/a | — |
| `fission_weapon` | tech | A9 | C | uranium_enrichment OR reprocessing | nuclear weapon (D-011 deterrence abstraction; knowledge only, no mechanic) | — | — | n/a | — |
| `guided_missile` | tech | A9 | C | rocket AND radar | surface-to-air, air-to-air and anti-ship missiles | — | — | n/a | — |
| `submarine` | tech | A9 | C | screw_propeller AND iron_hull AND electric_motor AND internal_combustion | submerged operation; undersea warfare | — | — | n/a | — |
| `endocrinology` | tech | A9 | C | physiology AND chemistry_quantitative | insulin therapy (diabetes survivable); hormone replacement (thyroid, cortisone) | — | — | n/a | — |
| `immunology` | tech | A9 | C | vaccine_lab | serum therapy (diphtheria and tetanus antitoxins); blood-group serology; antibody-based diagnosis | — | — | n/a | — |
| `nuclear_medicine` | tech | A9 | C | radioisotope_production | radiotracer diagnosis, scintigraphy and radionuclide therapy | — | — | n/a | — |
| `radiation_protection` | tech | A9 | C | radiation_detection AND xray | dose limits, dosimetry, time-distance-shielding-containment, ALARA | — | — | n/a | — |
| `radiotherapy` | tech | A9 | C | radioactivity AND xray | cancer treatment with sealed radium sources and teletherapy beams | — | — | n/a | — |
| `sulfonamides` | tech | A9 | C | germ_theory AND synthetic_dye | systemic antibacterial therapy (streptococcal and pneumococcal infection); puerperal sepsis treatable | — | — | n/a | — |
| `communications_satellite` | tech | A9 | C | satellite AND solar_pv | intercontinental telephone, television and data relay; satellite communication | — | — | n/a | — |
| `fast_breeder_reactor` | tech | A9 | C | reprocessing | sodium-cooled fast reactors that breed more fissile material than they consume (closed fuel cycle) | — | — | n/a | — |
| `geological_disposal` | tech | A9 | C | stratigraphy AND radiation_protection AND nuclear_fission | waste classes and long-term isolation in deep geological repositories | — | — | n/a | — |
| `graphite_reactor` | tech | A9 | C | nuclear_fission AND aluminium | graphite-moderated natural-uranium reactors - production piles (plutonium, isotopes) and power variants (Magnox; AGR with enriched fuel; RBMK-type) | — | — | n/a | — |
| `heavy_water_reactor` | tech | A9 | C | nuclear_fission AND steam_high_pressure | heavy-water-moderated natural-uranium reactors refuelled on power (PHWR/CANDU) and heavy-water production | — | — | n/a | — |
| `light_water_reactor` | tech | A9 | C | nuclear_fission AND uranium_enrichment AND steam_high_pressure | light-water reactors - pressurised (including compact naval cores) and boiling-water variants | — | — | n/a | — |
| `orbital_launch_vehicle` | tech | A9 | C | rocket | orbital-class rocket (payloads placed in orbit) | — | — | n/a | — |
| `programmable_computer` | tech | A9 | C | (vacuum_tube OR automatic_switching) AND calculus | automatic digital calculation (ballistic tables, codebreaking); programs set by plugboards, switches or punched tape | — | — | n/a | — |
| `radio_broadcasting` | tech | A9 | C | radio AND vacuum_tube | voice radio and broadcast transmission | — | — | n/a | — |
| `reprocessing` | tech | A9 | C | nuclear_fission AND radiation_protection | recovery of uranium and plutonium from spent fuel by solvent extraction (PUREX); MOX fuel - a proliferation-sensitive step | — | — | n/a | — |
| `uranium_enrichment` | tech | A9 | C | nuclear_physics AND uranium_milling | conversion to UF6 and isotope enrichment (electromagnetic, gaseous diffusion; centrifuge as a later variant) - a proliferation-sensitive step | — | — | n/a | — |
| `uranium_milling` | tech | A9 | C | radioactivity AND sulphuric_acid | radium recovery from uranium ores; uranium milling to concentrate (yellowcake), and in-situ leaching of suitable sandstone ores | — | — | n/a | — |
| `dna_sequencing` | tech | A9 | C | dna_structure | reading DNA base sequences | — | — | n/a | — |
| `geophysical_prospecting` | tech | A9 | C | stratigraphy AND electromagnetism | magnetic, electrical, gravity and seismic surveys of concealed structures | — | — | n/a | — |
| `nuclear_physics` | tech | A9 | C | radiation_detection AND quantum_mechanics AND relativity | nuclear structure, isotopes (including deuterium), the neutron, artificial radioactivity, slow neutrons; fission of uranium and the role of U-235; fusion reactions of light nuclei | — | — | n/a | — |
| `radiation_detection` | tech | A9 | C | radioactivity AND vacuum_tube | counters, cloud chambers, film badges and scintillators | — | — | n/a | — |
| `radioactivity` | tech | A9 | C | photography AND electromagnetism | radioactivity - radium and polonium, alpha, beta and gamma rays, transmutation (Rutherford and Soddy); radiation exposure harms living tissue (recognised from 1901 burns) | — | — | n/a | — |
| `radioisotope_production` | tech | A9 | C | nuclear_physics AND radiation_protection | making radioisotopes by cyclotron or reactor irradiation (tracers, Co-60, Ir-192, Mo-99/Tc-99m); industrial radiography, sterilisation and tracer methods | — | — | n/a | — |
| `radiometric_prospecting` | tech | A9 | C | radiation_detection AND stratigraphy | gamma-ray surveying - ground, car-borne and airborne mapping of uranium, thorium and potassium | — | — | n/a | — |
| `semiconductor_physics` | tech | A9 | C | quantum_mechanics AND radio | band theory of solids and the p-n junction; semiconductor rectifiers and photodiodes | — | — | n/a | — |
| `scientific_plant_breeding` | tech | A9 | C | genetics_mendel | planned crosses and selection on Mendelian lines; hybrid seed (heterosis); replicated variety trials | — | — | n/a | — |
| `law_code` | civics | A3 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) | — | `age:a3_law` → Age 3 milestone `a3_law` (supporting)<br>`inst.law_code` → no realizing system | yes | partly (rest deferred) | Age |
| `legal_code_roman` | civics | A4 | B (BDJ) | law_code | — | `age:a5_systematic_law` → Age 5 milestone `a5_systematic_law` (core)<br>`inst.guild` → no realizing system<br>`inst.legal_code_roman` → no realizing system<br>`inst.scientific_society` → no realizing system via `inst.university`<br>`inst.university` → project `military university` founds<br>`inst.university` → project `medical university` founds<br>`inst.university` → project `engineering university` founds<br>`inst.university` → project `natural science university` founds<br>`inst.university` → project `agricultural university` founds | yes | partly (rest deferred) | Age, Construction |
| `census` | civics | A4 | B (BJ) | (cuneiform OR hieroglyphic OR chinese_script) AND stamp_seal | — | `age:a4_census` → Age 4 milestone `a4_census` (supporting)<br>`inst.census` → no realizing system<br>`inst.mass_media_broadcast` → no realizing system via `inst.mass_schooling`<br>`inst.mass_schooling` → no realizing system<br>`inst.military_medicine` → no realizing system<br>`inst.mint_institution` → no realizing system<br>`inst.quarantine` → no realizing system<br>`inst.statistics_vital` → no realizing system | yes | partly (rest deferred) | Age |
| `coined_wage` | civics | A4 | B (BJ) | coinage_electrum | — | `age:a5_wages` → Age 5 milestone `a5_wages` (supporting)<br>`inst.coined_wage` → no realizing system | yes | partly (rest deferred) | Age |
| `patent` | civics | A7 | B (BJ) | legal_code_roman AND printing_press | — | `age:a7_patent` → Age 7 milestone `a7_patent` (supporting)<br>`inst.patent` → no realizing system | yes | partly (rest deferred) | Age |
| `joint_stock` | civics | A6 | B (BJ) | bill_of_exchange AND legal_code_roman | — | `age:a7_company` → Age 7 milestone `a7_company` (supporting)<br>`inst.central_bank` → no realizing system<br>`inst.joint_stock` → no realizing system | yes | partly (rest deferred) | Age |
| `taxation` | civics | A3 | B | token_counting AND stamp_seal AND (proto_writing OR hieroglyphic OR chinese_script) | the tax edict: a declared levy on the realm's production; tribute in kind assessed on the harvest; obligations recorded and owed to the state | `tax` → sim.json governance.taxationRequires | yes | yes | Governance |
| `kin_council` | civics | A1 | C | — | a council of household heads deciding common matters by consensus; disputes settled by the mediation of elders | — | — | n/a | — |
| `hereditary_rank` | civics | A2 | C | cereal_cultivation OR sheep_goat | rank and office inherited within leading lineages | — | — | n/a | — |
| `oath_alliance` | civics | A2 | C | kin_council | alliances between communities sealed by oath, gifts and intermarriage | — | — | n/a | — |
| `organized_priesthood` | civics | A2 | C | calendar_lunar AND (cereal_cultivation OR sheep_goat) | a temple priesthood keeping the ritual calendar; temple estates managed by priests | — | — | n/a | — |
| `chiefdom` | civics | A2 | C | hereditary_rank | a regional chiefdom under a paramount chief | — | — | n/a | — |
| `tribal_confederacy` | civics | A2 | C | oath_alliance | a sworn league of autonomous tribes | — | — | n/a | — |
| `appointed_officials` | civics | A3 | J | taxation AND (cuneiform OR hieroglyphic OR chinese_script) | officials appointed and removed by the ruler, acting on written orders | `inst.mint_institution` → no realizing system | yes | deferred | — |
| `corvee_labour` | civics | A3 | C | taxation | labour service owed to the state, worked in rotating gangs | — | — | n/a | — |
| `elite_council` | civics | A3 | C | kin_council AND hereditary_rank | a council of leading families deliberating on war, law and finance | — | — | n/a | — |
| `royal_envoys` | civics | A3 | C | cuneiform OR hieroglyphic OR chinese_script | accredited envoys carrying sealed letters between rulers | — | — | n/a | — |
| `sacral_kingship` | civics | A3 | C | hereditary_rank AND organized_priesthood | kingship sanctioned by the gods and upheld by temple ritual | — | — | n/a | — |
| `service_tenure` | civics | A3 | C | taxation AND law_code | land grants held in return for military service | — | — | n/a | — |
| `succession_law` | civics | A3 | C | hereditary_rank AND law_code | a fixed rule naming the ruler's heir | — | — | n/a | — |
| `sworn_treaties` | civics | A3 | C | royal_envoys AND oath_alliance | written treaties sworn before the gods and kept by both parties | — | — | n/a | — |
| `city_state_oligarchy` | civics | A3 | C | elite_council AND law_code | a city governed by a council of leading families | — | — | n/a | — |
| `territorial_kingship` | civics | A3 | C | (sacral_kingship OR succession_law) AND appointed_officials | a realm of many settlements under a hereditary king | — | — | n/a | — |
| `citizen_assembly` | civics | A4 | C | kin_council | an assembly of free members deciding by vote | — | — | n/a | — |
| `elected_magistracies` | civics | A4 | C | citizen_assembly | executive offices filled by election for fixed terms | — | — | n/a | — |
| `land_registry` | civics | A4 | C | surveying AND law_code | a public register of land, holders and dues | — | — | n/a | — |
| `provincial_administration` | civics | A4 | C | appointed_officials AND census | provinces under appointed governors answering to the capital | — | — | n/a | — |
| `standing_army` | civics | A4 | J | appointed_officials | a permanent army paid and supplied by the state | `inst.military_medicine` → no realizing system | yes | deferred | — |
| `tax_farming` | civics | A4 | C | taxation AND coinage_electrum | collection of taxes contracted to private tax farmers | — | — | n/a | — |
| `theocracy` | civics | A4 | C | organized_priesthood AND (law_code OR sacral_kingship) | government by religious office under religious law | — | — | n/a | — |
| `craft_guilds` | civics | A5 | J | legal_code_roman OR municipal_charter | chartered associations of master craftsmen controlling admission and training | `inst.guild` → no realizing system | yes | deferred | — |
| `municipal_charter` | civics | A5 | C | law_code AND (elite_council OR citizen_assembly) | town self-government under a written charter | — | — | n/a | — |
| `religious_toleration` | civics | A5 | C | organized_priesthood AND legal_code_roman | legal toleration of more than one faith | — | — | n/a | — |
| `assembly_democracy` | civics | A5 | C | citizen_assembly AND law_code | government by an assembly of all citizens | — | — | n/a | — |
| `bureaucratic_empire` | civics | A5 | C | provincial_administration AND standing_army | an empire governed through salaried officials and provinces | — | — | n/a | — |
| `classical_republic` | civics | A5 | C | elected_magistracies AND elite_council | a republic of elected magistrates, senate and assemblies | — | — | n/a | — |
| `civil_service_examinations` | civics | A6 | C | appointed_officials AND paper | recruitment of officials by competitive written examination | — | — | n/a | — |
| `commercial_law` | civics | A6 | C | legal_code_roman AND trade | merchant courts enforcing contracts between strangers | — | — | n/a | — |
| `representative_assembly` | civics | A6 | C | taxation AND (municipal_charter OR service_tenure) | representatives of estates and towns consenting to taxation | — | — | n/a | — |
| `feudal_monarchy` | civics | A6 | C | service_tenure AND (succession_law OR sacral_kingship) | a monarchy of vassals holding fiefs for service | — | — | n/a | — |
| `merchant_republic` | civics | A6 | C | commercial_law AND (elected_magistracies OR municipal_charter) | a republic governed by merchant councils | — | — | n/a | — |
| `military_rule` | civics | A6 | C | standing_army OR service_tenure | government by the armed forces | — | — | n/a | — |
| `bill_of_rights` | civics | A7 | C | legal_code_roman AND (representative_assembly OR elite_council) | rights guaranteed by law against the ruler, enforceable in court | — | — | n/a | — |
| `central_banking` | civics | A7 | C | joint_stock AND paper_money | a public bank holding state accounts and issuing notes | — | — | n/a | — |
| `civil_police` | civics | A7 | C | appointed_officials AND law_code | a salaried civil police force under public authority | — | — | n/a | — |
| `compulsory_education` | civics | A7 | C | census AND printing_press | a legal duty of school attendance for children | — | — | n/a | — |
| `law_of_nations` | civics | A7 | C | sworn_treaties AND legal_code_roman | a law among sovereign states on envoys, treaties, neutrality and war | — | — | n/a | — |
| `official_statistics` | civics | A7 | J | census AND (hindu_arabic OR counting_rods) | regular official tables of population, births and deaths | `inst.statistics_vital` → no realizing system | yes | deferred | — |
| `public_health_board` | civics | A7 | C | census AND medicine_hippocratic | a public health authority with powers of quarantine and sanitation | — | — | n/a | — |
| `resident_embassies` | civics | A7 | C | sworn_treaties AND appointed_officials | permanent ambassadors resident at foreign courts | — | — | n/a | — |
| `separation_of_powers` | civics | A7 | C | written_constitution | legislative, executive and judicial powers held by separate bodies | — | — | n/a | — |
| `written_constitution` | civics | A7 | C | legal_code_roman AND representative_assembly | a fundamental written law establishing and limiting the organs of government | — | — | n/a | — |
| `absolute_monarchy` | civics | A7 | C | standing_army AND (provincial_administration OR civil_service_examinations) | a monarchy ruling without consent of estates | — | — | n/a | — |
| `constitutional_monarchy` | civics | A7 | C | succession_law AND (written_constitution OR (bill_of_rights AND representative_assembly)) | a monarchy bound by a constitution and a legislature | — | — | n/a | — |
| `federal_republic` | civics | A7 | C | written_constitution AND (municipal_charter OR provincial_administration) | a federation dividing sovereignty between union and members | — | — | n/a | — |
| `presidential_republic` | civics | A7 | C | separation_of_powers AND elected_magistracies | a republic with a separately elected president | — | — | n/a | — |
| `abolition` | civics | A8 | C | legal_code_roman | the legal abolition of slavery and hereditary bondage | — | — | n/a | — |
| `general_staff` | civics | A8 | C | standing_army AND cartography | a permanent staff of officers planning mobilization and operations | — | — | n/a | — |
| `laws_of_war` | civics | A8 | C | law_of_nations | multilateral conventions protecting the wounded, prisoners and civilians | — | — | n/a | — |
| `mass_political_party` | civics | A8 | C | mass_suffrage AND printing_press | permanent membership parties competing in elections | — | — | n/a | — |
| `mass_suffrage` | civics | A8 | C | representative_assembly AND census | the vote for all adults by secret ballot from a public register | — | — | n/a | — |
| `ministerial_responsibility` | civics | A8 | C | representative_assembly AND appointed_officials | ministers holding office only with the confidence of the elected chamber | — | — | n/a | — |
| `universal_conscription` | civics | A8 | C | standing_army AND census | compulsory military service by registration and annual call-up | — | — | n/a | — |
| `parliamentary_government` | civics | A8 | C | ministerial_responsibility | government by a cabinet responsible to parliament | — | — | n/a | — |
| `central_planning` | civics | A9 | C | official_statistics AND telegraph | state planning of output and allocation for the whole economy | — | — | n/a | — |
| `marxist_leninist_state` | civics | A9 | C | mass_political_party AND central_planning | a communist party-state with a planned economy | — | — | n/a | — |
| `one_party_state` | civics | A9 | C | mass_political_party AND (written_constitution OR standing_army) | government by a single legal party | — | — | n/a | — |
