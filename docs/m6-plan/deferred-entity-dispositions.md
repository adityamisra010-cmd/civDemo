# Research entity dispositions (M6 closure, track KNOW)

**Status:** RECORD, 2026-10-28. Authority: D-066 §2 **A9** (ADOPTED): *"M6 realizes only the entities the slice needs (the
law court). Every other deferred entity is classified with an owning system and a target milestone, consistent with the
M7 charters. No other tier is realized in M6."* This register supersedes the tier list of
`docs/deferred-entity-realization-plan.md` as the per-entity record (that plan carries a dated pointer here); its A–G
tiers stay as the plan's history.

**Coverage.** Every entity of the shipped `research.json` (135) appears in §2 **exactly once**;
`EntityDispositionRegisterTests.Register_ListsEveryResearchEntityExactlyOnce_WithTheAuditsRealization` checks this table
against the content and against the realizing consumers on every test run.

**Sources.** *Realized* means a content link names a consumer that a system of this build enforces through the knowledge
evaluator — the same per-consumer-type rule `scripts/research-gameplay-unlock-audit.py` applies (goods.json recipes,
projects and `founds`; sim.json road classes and `trade.entity`; research.json `sectorActivities`; and, new in this
pass, unit-families.json `realizedBy`, which RecruitmentQuery recruits — ADR-055, the uncommitted M7 pass in the base
tree). *Deferred* rows take their owning system and status from the building charter's entity register
(`docs/m7-plan/building-and-infrastructure-charter.md` §9: E existing, M7 candidate, M8+ with owner, B blocked by a
missing good or system, D design-only); the three deferred activities from the unit charter
(`docs/m7-plan/unit-catalogue-and-progression.md` §8.5) and the movement water edges (charter M7.6). The target
milestone column reads the charter's status: M7 → M7; M8+ → the milestone in its owner; B → the blocking packet's
milestone (EC-4a: M10 recommended, OD-3; EC-5: M10+; energy and space: unscheduled); D → unscheduled.

## 1. Counts

| kind | total | realized | baseline, not simulated | M6 slice (track CIV) | deferred |
|---|---|---|---|---|---|
| building | 43 | 3 | 0 | 0 | 40 |
| infrastructure | 25 | 5 | 0 | 0 | 20 |
| institution | 28 | 1 | 1 (`inst.burial`) | 1 (`inst.law_code`) | 25 |
| unit | 23 | 23 (gate wired; 9 recruit only once a planned packet good exists) | 0 | 0 | 0 |
| activity | 9 | 5 | 1 (`activity.fishing`) | 0 | 3 |
| recipe | 4 | 4 | 0 | 0 | 0 |
| project | 3 | 0 | 0 | 0 | 3 |
| **all** | **135** | **41** | **2** | **1** | **91** |

Reconciliation with the pre-pass figure "20 realized, 115 deferred": the 20 are the 18 realized non-unit entities and the
2 null-requirement baselines; the 115 are the 91 deferred above, the 23 units (now realized through `realizedBy`) and the
law court (realized by track CIV under D-066 A2 — not by this track).

## 2. Register

Columns: entity · kind · disposition · owner system (realized: the enforcing system; deferred: the owning system and the
charter's status) · target milestone · evidence (realized: the consumer and the test that pins it; deferred: the
requirement and the reason).

| entity | kind | disposition | owner | target | evidence |
|---|---|---|---|---|---|
| `building.granary` | building | realized | ConstructionSystem (ConstructionQuery.IsProjectAvailable) | M6 | project `granary` — ConstructionAvailabilityTests |
| `building.workshop` | building | realized | ConstructionSystem (ConstructionQuery.IsProjectAvailable) | M6 | project `workshop` — ConstructionAvailabilityTests |
| `building.kiln` | building | deferred | Production (capacity term) (charter §9 status D) | unscheduled (design-only) | requires `kiln_updraft`; no realizing consumer in this build |
| `building.smithy` | building | deferred | Production (charter §9 status D) | unscheduled (design-only) | requires `copper_smelting OR iron_bloomery`; no realizing consumer in this build |
| `building.blast_furnace` | building | deferred | EC-1 (charter §9 status B) | M7 (EC-1 metals landed in the M7 pass; the building model is the charter's) | requires `blast_furnace_water OR cast_iron`; no realizing consumer in this build |
| `building.water_mill` | building | deferred | Production (mechanical power) (charter §9 status D) | unscheduled (design-only) | requires `water_mill`; no realizing consumer in this build |
| `building.windmill` | building | deferred | Production; wind channel (B3) (charter §9 status D) | unscheduled (design-only) | requires `windmill_post OR windmill_vertical`; no realizing consumer in this build |
| `building.fulling_mill` | building | deferred | Production (CR-025 future-only node `fulling_mill`) (charter §9 status D) | unscheduled (design-only) | requires `fulling_mill`; no realizing consumer in this build |
| `building.paper_mill` | building | deferred | no paper good (charter §9 status B) | unscheduled (blocked: no paper good) | requires `paper AND water_mill`; no realizing consumer in this build |
| `building.oil_press` | building | deferred | no vegetable-oil good (charter §9 status B) | unscheduled (blocked: no vegetable-oil good) | requires `oil_press`; no realizing consumer in this build |
| `building.textile_mill` | building | deferred | Production; power-loom variant B (EC-1 coal) (charter §9 status D) | unscheduled (design-only) | requires `water_frame OR power_loom`; no realizing consumer in this build |
| `building.steelworks` | building | deferred | EC-1 (charter §9 status B) | M7 (EC-1 metals landed in the M7 pass; the building model is the charter's) | requires `bessemer OR open_hearth`; no realizing consumer in this build |
| `building.chemical_works` | building | deferred | sulphur and chemical goods (charter §9 status B) | unscheduled (blocked: sulphur and chemical goods) | requires `sulphuric_acid`; no realizing consumer in this build |
| `building.cement_works` | building | deferred | limestone and fuel goods (charter §9 status B) | unscheduled (blocked: limestone and fuel goods) | requires `portland_cement`; no realizing consumer in this build |
| `building.refinery` | building | deferred | EC-4 (charter §9 status B) | M10 recommended (EC-4a owner open, OD-3) | requires `petroleum_refining`; no realizing consumer in this build |
| `building.power_station` | building | deferred | energy system + fuel (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `electricity_generation`; no realizing consumer in this build |
| `building.hydroelectric_dam` | building | deferred | energy system (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `hydroelectric`; no realizing consumer in this build |
| `building.nuclear_power_station` | building | deferred | EC-6 + energy (charter §9 status B) | M10 (EC-6) | requires `nuclear_power`; no realizing consumer in this build |
| `building.school` | building | deferred | Research (RP mechanism) (charter §9 status D) | unscheduled (design-only) | requires `cuneiform OR hieroglyphic OR chinese_script OR abjad OR brahmi OR mesoamerican_script`; no realizing consumer in this build |
| `building.library` | building | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper OR mesoamerican_script`; no realizing consumer in this build |
| `building.university` | building | realized | ConstructionSystem (ConstructionQuery.IsProjectAvailable) | M6 | project `military university`; project `medical university`; project `engineering university`; project `natural science university`; project `agricultural university` — ConstructionAvailabilityTests |
| `building.hospital` | building | deferred | M9 health (charter §9 status M8+) | M9 | requires `medicine_hippocratic AND pharmacology`; no realizing consumer in this build |
| `building.observatory` | building | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `astronomy_geometric OR astrolabe OR telescope`; no realizing consumer in this build |
| `building.research_laboratory` | building | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `chemistry_quantitative`; no realizing consumer in this build |
| `building.printing_house` | building | deferred | no paper good (then Research / M9 culture) (charter §9 status B) | unscheduled (blocked: no paper good (then Research / M9 culture)) | requires `printing_press OR woodblock_print`; no realizing consumer in this build |
| `building.mint` | building | deferred | no coin good (then M8 finance) (charter §9 status B) | unscheduled (blocked: no coin good (then M8 finance)) | requires `coinage_electrum`; no realizing consumer in this build |
| `building.bathhouse` | building | deferred | M9 health (charter §9 status M8+) | M9 | requires `aqueduct_arcade AND vault_dome`; no realizing consumer in this build |
| `building.city_wall` | building | deferred | fortification term (charter §9 status M7) | M7 | requires `mudbrick OR stone_dry`; no realizing consumer in this build |
| `building.castle` | building | deferred | fortification term (charter §9 status M7) | M7 | requires `stone_fortification AND vault_dome`; no realizing consumer in this build |
| `building.bastion_fort` | building | deferred | fortification term (charter §9 status M7) | M7 | requires `trace_italienne`; no realizing consumer in this build |
| `building.harbour` | building | deferred | M7.6 water edges (charter §9 status M7) | M7 | requires `plank_boat AND stone_dry`; no realizing consumer in this build |
| `building.lighthouse` | building | deferred | M7.6 (coastal loss term) (charter §9 status M7) | M7 | requires `ashlar_masonry OR stone_fortification`; no realizing consumer in this build |
| `building.dry_dock` | building | deferred | M7.6 (vessel repair) (charter §9 status M7) | M7 | requires `carrack`; no realizing consumer in this build |
| `building.airfield` | building | deferred | EC-4 / EC-5; air basing (charter §9 status B) | M10 recommended (EC-4a owner open, OD-3) | requires `aircraft`; no realizing consumer in this build |
| `building.radio_transmitter` | building | deferred | EC-5 + energy (charter §9 status B) | M10+ (EC-5 packets) | requires `radio`; no realizing consumer in this build |
| `building.skyscraper` | building | deferred | EC-1 steel; cement (charter §9 status B) | M7 (EC-1 metals landed in the M7 pass; the building model is the charter's) | requires `skyscraper`; no realizing consumer in this build |
| `building.spaceport` | building | deferred | §11 (charter §9 status B) | unscheduled (charter §11 space; endgame semantics unruled) | requires `rocket`; no realizing consumer in this build |
| `building.semiconductor_fab` | building | deferred | EC-5 + energy (charter §9 status B) | M10+ (EC-5 packets) | requires `integrated_circuit`; no realizing consumer in this build |
| `building.data_centre` | building | deferred | EC-5 + energy + DN (charter §9 status B) | M10+ (EC-5 packets) | requires `cloud_computing`; no realizing consumer in this build |
| `building.desalination_plant` | building | deferred | energy (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `desalination`; no realizing consumer in this build |
| `building.solar_farm` | building | deferred | energy; irradiance channel (B3) (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `solar_pv`; no realizing consumer in this build |
| `building.wind_farm` | building | deferred | energy; wind channel (B3) (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `wind_turbine_modern`; no realizing consumer in this build |
| `building.fusion_plant` | building | deferred | energy (future-only) (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `fusion_power`; no realizing consumer in this build |
| `infra.road_track` | infrastructure | realized | RoadDevelopmentSystem (RoadDevelopmentQuery.IsClassKnown) | M6 | road class `infra.road_track` — ResearchUnlockPipelineTests.T09–T10 |
| `infra.road_built` | infrastructure | realized | RoadDevelopmentSystem (RoadDevelopmentQuery.IsClassKnown) | M6 | road class `infra.road_built` — ResearchUnlockPipelineTests.T09–T10 |
| `infra.road_paved` | infrastructure | realized | RoadDevelopmentSystem (RoadDevelopmentQuery.IsClassKnown) | M6 | road class `infra.road_paved` — ResearchUnlockPipelineTests.T09–T10 |
| `infra.road_macadam` | infrastructure | realized | RoadDevelopmentSystem (RoadDevelopmentQuery.IsClassKnown) | M6 | road class `infra.road_macadam` — ResearchUnlockPipelineTests.T09–T10 |
| `infra.road_highway` | infrastructure | realized | RoadDevelopmentSystem (RoadDevelopmentQuery.IsClassKnown) | M6 | road class `infra.road_highway` — ResearchUnlockPipelineTests.T09–T10 |
| `infra.courier_relay` | infrastructure | deferred | EC-2 mounts; command latency (M7) when unblocked (charter §9 status B) | M7 (EC-2 mounts) | requires `track_road AND horse_domestication`; no realizing consumer in this build |
| `infra.bridge` | infrastructure | deferred | transport packet (charter §9 status M7) | M7 | requires `bridge_stone`; no realizing consumer in this build |
| `infra.basin_irrigation` | infrastructure | deferred | Farming (preCultivation ruling) (charter §9 status D) | unscheduled (design-only) | requires `irrigation_basin`; no realizing consumer in this build |
| `infra.canal_irrigation` | infrastructure | deferred | Farming (charter §9 status D) | unscheduled (design-only) | requires `irrigation_canal`; no realizing consumer in this build |
| `infra.terraces` | infrastructure | deferred | Farming (charter §9 status D) | unscheduled (design-only) | requires `terrace`; no realizing consumer in this build |
| `infra.qanat` | infrastructure | deferred | Farming (charter §9 status D) | unscheduled (design-only) | requires `qanat`; no realizing consumer in this build |
| `infra.well` | infrastructure | deferred | M9 health / growth (charter §9 status M8+) | M9 | requires `well`; no realizing consumer in this build |
| `infra.aqueduct` | infrastructure | deferred | M9 (charter §9 status M8+) | M9 | requires `aqueduct_channel OR aqueduct_arcade OR aqueduct_tunnel`; no realizing consumer in this build |
| `infra.urban_drainage` | infrastructure | deferred | M9 (charter §9 status M8+) | M9 | requires `fired_brick`; no realizing consumer in this build |
| `infra.sewer` | infrastructure | deferred | M9 (charter §9 status M8+) | M9 | requires `vault_dome AND fired_brick`; no realizing consumer in this build |
| `infra.sewerage_system` | infrastructure | deferred | M9 (charter §9 status M8+) | M9 | requires `sanitary_engineering OR (germ_theory AND reinforced_concrete)`; no realizing consumer in this build |
| `infra.fish_weir` | infrastructure | deferred | Fishing activity (charter §9 status D) | unscheduled (design-only) | requires `basketry`; no realizing consumer in this build |
| `infra.navigation_canal` | infrastructure | deferred | M7.6 water edges (charter §9 status M7) | M7 | requires `canal_navigation`; no realizing consumer in this build |
| `infra.railway` | infrastructure | deferred | EC-1 (charter §9 status B) | M7 (EC-1 metals landed in the M7 pass; the building model is the charter's) | requires `railway`; no realizing consumer in this build |
| `infra.telegraph_line` | infrastructure | deferred | EC-5 (copper metal) (charter §9 status B) | M10+ (EC-5 packets) | requires `telegraph`; no realizing consumer in this build |
| `infra.submarine_cable` | infrastructure | deferred | EC-5 (charter §9 status B) | M10+ (EC-5 packets) | requires `submarine_cable`; no realizing consumer in this build |
| `infra.telephone_network` | infrastructure | deferred | EC-5 + energy (charter §9 status B) | M10+ (EC-5 packets) | requires `telephone`; no realizing consumer in this build |
| `infra.power_grid` | infrastructure | deferred | energy system (charter §9 status B) | unscheduled (energy system, charter §7.1 — no milestone yet) | requires `electricity_generation`; no realizing consumer in this build |
| `infra.mobile_network` | infrastructure | deferred | EC-5 + energy + DN (charter §9 status B) | M10+ (EC-5 packets) | requires `mobile_phone`; no realizing consumer in this build |
| `infra.internet_backbone` | infrastructure | deferred | EC-5 + energy + DN (charter §9 status B) | M10+ (EC-5 packets) | requires `internet`; no realizing consumer in this build |
| `inst.burial` | institution | baseline, not simulated | M9 (culture/religion) (charter §9 status M8+); known from the founding (null requirement) | M9 | no knowledge gate and no realizing consumer in this build |
| `inst.scribal_school` | institution | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script)`; no realizing consumer in this build |
| `inst.law_code` | institution | M6 slice (track CIV) | InstitutionsSystem + Unrest.ServiceOffset — the law court, D-066 A2; realized by track CIV in this closure, not by track KNOW | M6 | track CIV's tests (see its hand-back) |
| `inst.archive` | institution | deferred | Research / M8 administration (charter §9 status D) | unscheduled (design-only) | requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND stamp_seal`; no realizing consumer in this build |
| `inst.textile_workshop` | institution | deferred | Production (charter §9 status D) | unscheduled (design-only) | requires `loom_warp_weighted AND archive`; no realizing consumer in this build |
| `inst.aramaic_admin` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `abjad AND archive`; no realizing consumer in this build |
| `inst.natural_philosophy` | institution | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND astronomy_records`; no realizing consumer in this build |
| `inst.library` | institution | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `archive AND scribal_school`; no realizing consumer in this build |
| `inst.coined_wage` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `coined_wage`; no realizing consumer in this build |
| `inst.census` | institution | deferred | M8 (tax efficiency term) (charter §9 status M8+) | M8 | requires `census`; no realizing consumer in this build |
| `inst.legal_code_roman` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `legal_code_roman`; no realizing consumer in this build |
| `inst.historiography` | institution | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `(cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script) AND library`; no realizing consumer in this build |
| `inst.military_medicine` | institution | deferred | field-medical family (unit charter §7.12) (charter §9 status M7) | M7 | requires `surgery_instruments AND (census OR standing_army)`; no realizing consumer in this build |
| `inst.mint_institution` | institution | deferred | no coin good (then M8) (charter §9 status B) | unscheduled (blocked: no coin good (then M8)) | requires `coinage_electrum AND (census OR appointed_officials)`; no realizing consumer in this build |
| `inst.university` | institution | realized | ConstructionSystem + InstitutionsSystem (founds) | M6 | project `military university` founds; project `medical university` founds; project `engineering university` founds; project `natural science university` founds; project `agricultural university` founds — ResearchUnlockPipelineTests.T11–T15 |
| `inst.hospital` | institution | deferred | M9 (charter §9 status M8+) | M9 | requires `medicine_hippocratic AND pharmacology`; no realizing consumer in this build |
| `inst.translation_movement` | institution | deferred | Research (charter §9 status D) | unscheduled (design-only) | requires `paper AND library`; no realizing consumer in this build |
| `inst.guild` | institution | deferred | Production / M9 (charter §9 status D) | unscheduled (design-only) | requires `textile_workshop AND (legal_code_roman OR craft_guilds)`; no realizing consumer in this build |
| `inst.quarantine` | institution | deferred | M9 (charter §9 status M8+) | M9 | requires `hospital AND census`; no realizing consumer in this build |
| `inst.newspaper` | institution | deferred | M8 / M9 (opinion) (charter §9 status M8+) | M8 | requires `printing_press`; no realizing consumer in this build |
| `inst.scientific_society` | institution | deferred | SP recruitment points (M9) (charter §9 status M8+) | M9 | requires `university AND printing_press AND optics_ibn_haytham`; no realizing consumer in this build |
| `inst.joint_stock` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `joint_stock`; no realizing consumer in this build |
| `inst.central_bank` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `joint_stock AND paper_money`; no realizing consumer in this build |
| `inst.patent` | institution | deferred | M8 (charter §9 status M8+) | M8 | requires `patent`; no realizing consumer in this build |
| `inst.standard_threads` | institution | deferred | Production (charter §9 status D) | unscheduled (design-only) | requires `slide_rest_lathe`; no realizing consumer in this build |
| `inst.statistics_vital` | institution | deferred | M9 (charter §9 status M8+) | M9 | requires `(census AND calculus) OR (official_statistics AND epidemiology)`; no realizing consumer in this build |
| `inst.mass_schooling` | institution | deferred | M9 (charter §9 status M8+) | M9 | requires `printing_press AND newspaper AND census`; no realizing consumer in this build |
| `inst.mass_media_broadcast` | institution | deferred | M9 (cultural groups gate) (charter §9 status M8+) | M9 | requires `radio AND television AND mass_schooling`; no realizing consumer in this build |
| `unit.spearmen` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `spear_anti_cavalry/spearmen` — RecruitmentTests; UnitTaxonomyTests |
| `unit.archers` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `ranged_infantry/archers` — RecruitmentTests; UnitTaxonomyTests |
| `unit.slingers` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `ranged_infantry/slingers` — RecruitmentTests; UnitTaxonomyTests |
| `unit.chariot` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `heavy_cavalry/chariots` — RecruitmentTests; UnitTaxonomyTests |
| `unit.horse_archers` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `ranged_cavalry/horse_archers` — RecruitmentTests; UnitTaxonomyTests |
| `unit.heavy_cavalry` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `heavy_cavalry/knights` — RecruitmentTests; UnitTaxonomyTests |
| `unit.crossbowmen` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `ranged_infantry/crossbowmen` — RecruitmentTests; UnitTaxonomyTests |
| `unit.longbowmen` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `ranged_infantry/longbowmen` — RecruitmentTests; UnitTaxonomyTests |
| `unit.siege_engine` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `siege/siege_rams` — RecruitmentTests; UnitTaxonomyTests |
| `unit.artillery` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `siege/bombards` — RecruitmentTests; UnitTaxonomyTests |
| `unit.musketeers` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `heavy_infantry/musketeers` — RecruitmentTests; UnitTaxonomyTests |
| `unit.war_galley` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `naval_combat/war_galleys` — RecruitmentTests; UnitTaxonomyTests |
| `unit.ship_of_the_line` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `naval_combat/ships_of_the_line` — RecruitmentTests; UnitTaxonomyTests |
| `unit.ironclad` | unit | realized | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `naval_combat/ironclads` — RecruitmentTests; UnitTaxonomyTests |
| `unit.tank` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `heavy_cavalry/tanks` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until engines, fuel exist (planned packet goods) |
| `unit.military_aircraft` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `air/military_aircraft` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until engines, fuel exist (planned packet goods) |
| `unit.missile` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `strategic_missile/ballistic_missiles` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until fuel exist (planned packet goods) |
| `unit.nuclear_submarine` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `submarines/nuclear_submarines` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until electronics, uranium exist (planned packet goods) |
| `unit.icbm` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `strategic_missile/icbm_forces` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until electronics, fuel exist (planned packet goods) |
| `unit.combat_drone` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `unmanned_aircraft/combat_drones` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until aluminium, electronics, fuel exist (planned packet goods) |
| `unit.stealth_aircraft` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `air/stealth_aircraft` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until aluminium, composites, electronics, engines, fuel exist (planned packet goods) |
| `unit.aircraft_carrier` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `carriers/aircraft_carriers` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until engines, fuel exist (planned packet goods) |
| `unit.helicopter` | unit | realized (gate wired; recruitment blocked) | MilitarySystem (RecruitmentQuery.CheckRecruit; ADR-055) | M7 (recruitment realized in the M7 pass; battle use M7) | unit identity `rotary_wing/helicopters` — RecruitmentTests; UnitTaxonomyTests — recruitment blocked until aluminium, engines, fuel exist (planned packet goods) |
| `activity.farming` | activity | realized | LabourActivities (ADR-033 D1) | M6 | sector `farming` → replaces — ResearchUnlockPipelineTests.T01–T02; LabourActivityTests |
| `activity.herding` | activity | realized | LabourActivities (ADR-033 D1) | M6 | sector `herding_fishing` → replaces — ResearchUnlockPipelineTests.T01–T02; LabourActivityTests |
| `activity.fishing` | activity | baseline, not simulated | LabourActivities — fishing is done through the herding_fishing sector's baseline identity (baseline.basic_fishing); no identity of its own | — (baseline; no further realization planned) | no knowledge gate; the sector baseline is simulated (LabourActivityTests) |
| `activity.logging` | activity | realized | LabourActivities (ADR-033 D1) | M6 | sector `extraction` → joins — ResearchUnlockPipelineTests.T01–T02; LabourActivityTests |
| `activity.mining` | activity | realized | LabourActivities (ADR-033 D1) | M6 | sector `extraction` → joins — ResearchUnlockPipelineTests.T01–T02; LabourActivityTests |
| `activity.caravans` | activity | deferred | trade convoys / transport (unit charter §8.5) | M8 (convoys with diplomacy and raiding) | requires `donkey OR camel`; no realizing consumer in this build |
| `activity.coastal_shipping` | activity | deferred | M7 movement — water edges (charter M7.6) | M7 | requires `sail_square`; no realizing consumer in this build |
| `activity.ocean_shipping` | activity | deferred | M7 movement — water edges (charter M7.6) | M7 | requires `carrack OR caravel OR polynesian_canoe OR ocean_junk`; no realizing consumer in this build |
| `project.orbital_launch` | project | deferred | §11 (charter §9 status B) | unscheduled (charter §11 space; endgame semantics unruled) | requires `rocket`; no realizing consumer in this build |
| `project.probe_acceleration` | project | deferred | §11 (charter §9 status B) | unscheduled (charter §11 space; endgame semantics unruled) | requires `laser AND space_solar`; no realizing consumer in this build |
| `project.interstellar_probe` | project | deferred | §11; endgame semantics unruled (charter §9 status B) | unscheduled (charter §11 space; endgame semantics unruled) | requires `fusion_propulsion`; no realizing consumer in this build |
| `recipe.pottery_firing` | recipe | realized | ProductionSystem (CraftingQuery.IsRecipeAvailable) | M6 | recipe `pottery-firing` — ResearchUnlockPipelineTests.T03–T06 |
| `recipe.weaving` | recipe | realized | ProductionSystem (CraftingQuery.IsRecipeAvailable) | M6 | recipe `weaving` — ResearchUnlockPipelineTests.T03–T06 |
| `recipe.bronze_casting` | recipe | realized | ProductionSystem (CraftingQuery.IsRecipeAvailable) | M6 | recipe `bronze-casting` — ResearchUnlockPipelineTests.T03–T06 |
| `recipe.toolmaking` | recipe | realized | ProductionSystem (CraftingQuery.IsRecipeAvailable) | M6 | recipe `toolmaking` — ResearchUnlockPipelineTests.T03–T06 |
| `activity.trade` | activity | realized | TradeArbitrageSystem (TradeQuery.CanTrade) | M6 | sim.json trade.entity — TradeResearchUnlockTests |
