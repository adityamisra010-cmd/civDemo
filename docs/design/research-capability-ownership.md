# Research capability ownership matrix (D-046 G3)

**Status:** REQUIRED before M5 — the integration contract between the research corpus and the simulation
(D-046 G3). **Design only:** every change listed here is for the M5 integration packets; none is implemented,
because none is needed to make the research schema internally valid (G3). Nothing here rules anything: each
classification applies G3's definitions to the tree, and each "Director decision" is flagged, not taken.

**Provenance.** Built by a recon → classify → adversarial-verify workflow on `research-progression-foundation`
(three recon reports, four classifiers, a contradictions checker and a completeness checker), then every
one of the checkers' 24 findings was itself adversarially verified,
one verifier per worktree pinned to `d4ec6cd` (ADR-015 §6: no finding acted on before its verdict). §8 is that record.
**Citations.** Research content is cited by stable anchor — `research.json` `<id>` `.field` — which resolves on every
revision. Code and documents are cited as `file:line` at `d4ec6cd`: citations the classifiers took at `6baafbe` were
mapped line-exactly to it (the few inside rewritten regions are aligned to the corresponding line and marked
*(moved)*); unlabelled ones in files or regions this gate did not change resolve unchanged, and the three that had
moved were corrected by hand. "dNNN:line" and "adr-NNN:line" abbreviate `docs/dNNN-*.md` and `docs/adr/adr-NNN-*.md`.

## 1. The four classes (G3, verbatim)

- **A. BASELINE** — exists without research.
- **B. TECHNOLOGY-OWNED** — requires completion of a specific research node.
- **C. REALIZATION** — the technology/capability exists, but the actual world object requires downstream
  material, institutional, infrastructure, personnel, construction, or other realization conditions.
- **D. MIXED** — only if a capability genuinely has a split prerequisite; the record states which part is
  baseline and which belongs to research. *"Do not use mixed as a convenience category."*

## 2. How the classes are read against this simulation

- **A** — the item exists or works at founding with zero completed nodes (as a capability, an object, or both), and needs
  no research to continue.
- **B** — completing a node confers it with nothing more. **No item is B today, and that is a finding, not an omission:**
  completion builds nothing and only makes things eligible (D-044 R14; ADR-028 §3), and no system consumes eligibility
  yet (ADR-029 §10). Every research-related world object therefore needs an owning system to realize it.
- **C** — the capability exists — from a node, or from the baseline (a null requirement) — but the world object exists only
  through realization conditions: construction, materials, personnel, institutions or infrastructure. A constructible
  that needs no research (the granary, the workshop) is C as an object and baseline as knowledge; the record says so.
- **D** — used for no item. Where research content claims something the founding simulation already does (the five
  activity entities, strings such as sheep_goat's "herding sector"), the whole item is baseline and the claim is a
  content defect to correct (§5), not a split prerequisite.
- **Related research node(s)** are context unless the record says *requires*. **Exists at founding:** yes / partial
  (present but dormant — awaiting a class, a construction or an order) / no.

## 3. Summary

**179 items.** A BASELINE: **48** · B TECHNOLOGY-OWNED: **0** · C REALIZATION: **131** · D MIXED: **0**

| group | items | A | B | C | D | exist at founding (yes / partial) |
|---|---|---|---|---|---|---|
| Baseline capabilities declared in research.json | 6 | 6 | 0 | 0 | 0 | 4 / 0 |
| The research engine itself | 1 | 1 | 0 | 0 | 0 | 1 / 0 |
| Simulation capabilities, objects and mechanisms running at founding | 42 | 35 | 0 | 7 | 0 | 22 / 17 |
| Buildings (registry) | 43 | 0 | 0 | 43 | 0 | 0 / 2 |
| Units (registry) | 23 | 0 | 0 | 23 | 0 | 0 / 0 |
| Activities (registry) | 8 | 5 | 0 | 3 | 0 | 5 / 0 |
| Infrastructure (registry) | 25 | 0 | 0 | 25 | 0 | 0 / 1 |
| Institutions (registry) | 28 | 1 | 0 | 27 | 0 | 0 / 1 |
| Projects (registry) | 3 | 0 | 0 | 3 | 0 | 0 / 0 |

## 4. The contradictions G3 names — and how this matrix closes each

| G3 example | Items | How the matrix closes it |
|---|---|---|
| A granary at founding treated as a researched invention | `building.granary`, `param.granary_capacity` | The entity requires nothing (no node owns it), and the storage ceiling works from turn 1 with no granary built. The object is built by ConstructionSystem (C). Storage strings on nodes (mudbrick, basketry, pottery, refrigeration, canning…) are recorded as non-owning. Pinned by `ResearchContentTests.Baseline_GranaryAndWorkshop_AreConstructibleWithZeroTechnology` |
| A workshop both baseline and technology-owned | `building.workshop` | Requires nothing; the object needs construction and 10 tools, and tools come from artisan-gated (not research-gated) recipes. If those recipes are ever research-gated, that is a Director decision recorded on the row, never a node requirement on the entity |
| Initial agriculture equivalent to completing the agriculture branch | `sector.farming`, `activity.farming` (and herding, fishing, logging, mining) | The simulation farms, herds, fishes, logs and mines at founding with zero research: all A. The content's activity entities nonetheless require nodes (e.g. `activity.farming` requires cereal_cultivation OR …): a content defect, recorded as a required change, inert today because nothing consumes eligibility |
| Baseline military requiring a future military technology | `baseline.basic_military`, `unit.*`, `coercion.appropriation` | Basic military is baseline by ruling (D-045 §1) but not simulated, and every registry unit requires a node. Constraint: the founding unit has a null requirement and equipment no node owns (unhafted clubs or bare manpower). Research-free raiding (D-037 B3) exists; cavalry's "steppe raiding" must be non-gating |
| Initial infrastructure implying later research | `infra.dirt_path`, `transport.overland_unimproved`, `transport.river_corridor`, `infra.road_*`, `infra.railway`, `infra.bridge`, `infra.navigation_canal` | Dirt paths, overland movement and river corridors are research-free from founding. Higher road, rail, bridge and canal tiers must be new edge types, never the existing dirt path — and whether any node may open them is BLOCKED on the Director's D-044 T3 ruling |

Found beyond G3's list and closed the same way: founding dwellings are baseline (`stock.dwellings`); sea crossing, exploration and colonisation strings (raft, caravel, polynesian_canoe) are non-gating under D-040 B3; the research engine itself is a baseline capability no node owns (`research.engine`).

## 5. Required changes for M5 (consolidated; none implemented)

Nothing below is implemented (G3). Content changes touch Director-ratified content (D-046 F§1), so each is a Director decision before it is an edit.

**Content (research.json), for Director decision:**
1. `activity.farming`, `activity.herding`, `activity.fishing`, `activity.logging`, `activity.mining`: null or re-scope each `requires` and remove the entity from its nodes' `unlocks.activities` (the loader's reverse-index rule) — they must never gate the founding sectors.
2. `baseline.food_gathering`, `baseline.settlement_founding`, `baseline.construction`: correct the `providedBy` text to what the code does (no HarvestSystem exists; colonies come from famine flight with no viable exit; construction completes whole projects with no progress field). Regenerate the audit (CI `--check`).
3. Record as non-gating (or reword as additive) the node strings that claim founding capabilities: sheep_goat "herding sector"; sail_square's river strings; raft, caravel and polynesian_canoe sea/exploration/colonisation strings; cavalry "steppe raiding"; hafting/knapping_oldowan/fire_making equipment strings (for the founding unit); cart, wheel_solid, animal_traction and horse_domestication overland strings; storage and spoilage strings.
4. `activity.coastal_shipping`, `activity.ocean_shipping`: their node requirements are latent D-040 B3 conflicts — BLOCKED on the D-044 T3 ruling (options on the rows).

**Code, for the M5 integration packets (each needs its own ADR where it touches a contract):**
5. A registry-id → construction-project mapping, and an issuer of `EnqueueConstruction` (no UI or CLI factory issues it today); owning effect mechanisms for built structures (no system reads `Structures`).
6. A military/formation system whose founding tier needs no research (D-045 §1; the constraint on `baseline.basic_military`).
7. An institutions system — the writer of the university cost seam, which owes the D-021 brake (`building.university`, `inst.university`).
8. Any consumer of knowledge eligibility (`ResearchQuery.IsKnowledgeEligible` has no caller in a system today) — ADR-028 §4 leaves it to the packet that needs it.

**Blocked on Director rulings:** D-044 Part D T3 (edge tiers, sea travel); T6 and D-046 G4 (repeat mechanics, e.g. frontier_medicine); whether bronze and tool recipes ever become research-gated; CR-006 (the epoch the 26.0 grain yield assumes).

## 6. Index

| item | kind | class | exists at founding | initial existence | related nodes |
|---|---|---|---|---|---|
| [`baseline.settlement_founding`](#baselinesettlement_founding) | baseline-capability | A BASELINE | yes | capability | `well` |
| [`baseline.exploration`](#baselineexploration) | baseline-capability | A BASELINE | no | none | `raft`, `dugout`, `sail_square`, `caravel`, `polynesian_canoe` |
| [`baseline.basic_military`](#baselinebasic_military) | baseline-capability | A BASELINE | no | none | `hafting`, `bow_simple`, `sling`, `fire_making`, `knapping_oldowan` |
| [`baseline.food_gathering`](#baselinefood_gathering) | baseline-capability | A BASELINE | yes | capability | `grinding_stone`, `root_crop`, `cereal_cultivation`, `cereal_domesticated`, `sheep_goat`, `cattle`, `fishing_hook`, `ground_stone_early`, `mining_shaft` |
| [`baseline.construction`](#baselineconstruction) | baseline-capability | A BASELINE | yes | capability | — |
| [`baseline.migration`](#baselinemigration) | baseline-capability | A BASELINE | yes | capability | — |
| [`research.engine`](#researchengine) | baseline-capability (research system) | A BASELINE | yes | capability | — |
| [`sector.farming`](#sectorfarming) | sector (Sectors.Farming = 0) | A BASELINE | yes | capability | `root_crop`, `cereal_cultivation`, `cereal_domesticated`, `grinding_stone`, `sickle`, `digging_stick_hoe`, `animal_traction`, `irrigation_basin` |
| [`sector.herding_fishing`](#sectorherding_fishing) | sector (Sectors.Herding = 1, herding/fishing) | A BASELINE | yes | capability | `dog_domestication`, `sheep_goat`, `cattle`, `fishing_hook`, `basketry`, `cordage`, `dugout` |
| [`sector.extraction`](#sectorextraction) | sector (Sectors.Extraction = 2) | A BASELINE | yes | capability | `ground_stone_early`, `hafting`, `copper_native`, `mining_shaft`, `stone_dry` |
| [`sector.crafting`](#sectorcrafting) | sector (Sectors.Crafting = 3) | A BASELINE | yes | capability | `pottery_open_fired`, `kiln_updraft`, `spinning_spindle`, `loom_warp_weighted`, `copper_smelting`, `tin_bronze` |
| [`sector.construction`](#sectorconstruction) | sector (Sectors.Construction = 4) | A BASELINE | yes | capability | `track_road` |
| [`good.grain`](#goodgrain) | good (food, numeraire) | A BASELINE | yes | both | `cereal_cultivation`, `cereal_domesticated`, `grinding_stone`, `root_crop` |
| [`good.livestock`](#goodlivestock) | good (food) | A BASELINE | partial | capability | `dog_domestication`, `sheep_goat`, `cattle` |
| [`good.fish`](#goodfish) | good (food) | A BASELINE | partial | capability | `fishing_hook`, `basketry`, `cordage`, `dugout` |
| [`good.timber`](#goodtimber) | good (raw) | A BASELINE | partial | capability | `ground_stone_early`, `hafting` |
| [`good.stone`](#goodstone) | good (raw) | A BASELINE | partial | capability | `knapping_oldowan`, `ground_stone_early`, `stone_dry` |
| [`good.clay`](#goodclay) | good (raw) | A BASELINE | partial | capability | `pottery_open_fired`, `mudbrick` |
| [`good.copper_ore`](#goodcopper_ore) | good (raw) | A BASELINE | partial | capability | `copper_native`, `copper_smelting`, `mining_shaft` |
| [`good.tin_ore`](#goodtin_ore) | good (raw) | A BASELINE | partial | capability | `tin_bronze`, `mining_shaft` |
| [`good.fiber`](#goodfiber) | good (raw) | A BASELINE | partial | capability | `flax`, `wool`, `cordage`, `spinning_spindle` |
| [`good.hides`](#goodhides) | good (raw) | A BASELINE | partial | capability | `hide_working`, `sewing` |
| [`good.bronze`](#goodbronze) | good (processed) | C REALIZATION | no | none | `copper_native`, `copper_smelting`, `tin_bronze`, `charcoal` |
| [`good.tools`](#goodtools) | good (processed) | C REALIZATION | no | none | `copper_native`, `copper_smelting`, `tin_bronze`, `knapping_oldowan`, `ground_stone_early`, `hafting`, `sickle`, `digging_stick_hoe` |
| [`good.pottery`](#goodpottery) | good (processed) | A BASELINE | partial | capability | `pottery_open_fired`, `kiln_updraft`, `potters_wheel_fast`, `basketry` |
| [`good.cloth`](#goodcloth) | good (processed) | A BASELINE | partial | capability | `spinning_spindle`, `loom_warp_weighted`, `flax`, `wool` |
| [`recipe.pottery_firing`](#recipepottery_firing) | recipe | A BASELINE | yes | capability | `pottery_open_fired`, `kiln_updraft` |
| [`recipe.weaving`](#recipeweaving) | recipe | A BASELINE | yes | capability | `spinning_spindle`, `loom_warp_weighted` |
| [`recipe.bronze_casting`](#recipebronze_casting) | recipe | C REALIZATION | partial | capability | `copper_smelting`, `tin_bronze`, `charcoal` |
| [`recipe.toolmaking`](#recipetoolmaking) | recipe | C REALIZATION | partial | capability | `copper_smelting`, `tin_bronze`, `copper_native` |
| [`stock.dwellings`](#stockdwellings) | conserved capital stock (HousingRow.Dwellings) | A BASELINE | yes | both | `shelter_hut`, `timber_frame`, `mudbrick`, `hide_working`, `sewing`, `cordage` |
| [`param.granary_capacity`](#paramgranary_capacity) | storage parameter (consumption.granaryYearsOfDemand + grainSpoilagePerYear) | A BASELINE | yes | capability | `mudbrick`, `pottery_open_fired`, `basketry`, `grinding_stone`, `cereal_domesticated`, `stamp_seal`, `root_crop`, `refrigeration`, `canning` |
| [`transport.river_corridor`](#transportriver_corridor) | terrain transport property (riverCostFactor) | A BASELINE | yes | capability | `raft`, `dugout`, `sail_square`, `bridge_stone` |
| [`transport.overland_unimproved`](#transportoverland_unimproved) | terrain transport property (lattice node cost = block-mean terrain MovementCost; water-majority blocks impassable) | A BASELINE | yes | capability | `cart`, `wheel_solid`, `animal_traction`, `horse_domestication` |
| [`catchment.hinterland`](#catchmenthinterland) | derived per-settlement territory (CatchmentNode, CatchmentSummary, SettlementDistance) | A BASELINE | no | capability | — |
| [`settlement.founding_set`](#settlementfounding_set) | settlement objects (12 worldgen SettlementRows) | A BASELINE | yes | object | `well` |
| [`settlement.colony`](#settlementcolony) | settlement object created in play | C REALIZATION | partial | capability | `well` |
| [`class.peasants`](#classpeasants) | social class (registry class 1) | A BASELINE | yes | object | — |
| [`class.artisans`](#classartisans) | social class (registry class 2) | C REALIZATION | partial | capability | — |
| [`class.merchants`](#classmerchants) | social class (registry class 3) | C REALIZATION | partial | capability | — |
| [`population.buckets`](#populationbuckets) | conserved stock (BucketRow: culture × religion × class × 16 cohorts) | A BASELINE | yes | object | — |
| [`population.vital_rates`](#populationvital_rates) | mechanism (the Demographics kernel) | A BASELINE | yes | capability | `frontier_medicine`, `antibiotic_penicillin`, `vaccine_lab`, `oral_rehydration`, `hormonal_contraception`, `green_revolution` |
| [`registry.culture_religion`](#registryculture_religion) | identity registries (1 culture 'Riverfolk', 1 religion 'Old Rites') | A BASELINE | yes | object | — |
| [`polity.empire_control_capital`](#polityempire_control_capital) | political objects (PolityRow, ControlRow, CapitalRow) | A BASELINE | yes | object | `stamp_seal` |
| [`deposits`](#deposits) | per-settlement resource abundance (DepositRow, double, not conserved) | A BASELINE | yes | object | `mining_shaft` |
| [`trade.arbitrage`](#tradearbitrage) | economic capability (TradeArbitrageSystem) | A BASELINE | yes | capability | `donkey`, `camel`, `standard_weights`, `sail_square` |
| [`market.prices`](#marketprices) | economic capability (PriceSystem) | A BASELINE | yes | capability | `standard_weights`, `coinage_electrum` |
| [`coercion.appropriation`](#coercionappropriation) | coercive capability (AppropriationSystem — stateless raiding) | A BASELINE | partial | capability | `sling`, `cavalry` |
| [`control.revolt`](#controlrevolt) | political capability (RevoltSystem) | A BASELINE | yes | capability | — |
| [`building.granary`](#buildinggranary) | building | C REALIZATION | partial | capability | `mudbrick`, `basketry`, `pottery_open_fired`, `cereal_domesticated`, `grinding_stone`, `stamp_seal` |
| [`building.workshop`](#buildingworkshop) | building | C REALIZATION | partial | capability | `copper_smelting`, `tin_bronze` |
| [`building.kiln`](#buildingkiln) | building | C REALIZATION | no | none | `kiln_updraft` |
| [`building.smithy`](#buildingsmithy) | building | C REALIZATION | no | none | `copper_smelting`, `iron_bloomery` |
| [`building.blast_furnace`](#buildingblast_furnace) | building | C REALIZATION | no | none | `blast_furnace_water`, `cast_iron` |
| [`building.water_mill`](#buildingwater_mill) | building | C REALIZATION | no | none | `water_mill` |
| [`building.windmill`](#buildingwindmill) | building | C REALIZATION | no | none | `windmill_post`, `windmill_vertical` |
| [`building.fulling_mill`](#buildingfulling_mill) | building | C REALIZATION | no | none | `fulling_mill` |
| [`building.paper_mill`](#buildingpaper_mill) | building | C REALIZATION | no | none | `paper`, `water_mill` |
| [`building.oil_press`](#buildingoil_press) | building | C REALIZATION | no | none | `oil_press` |
| [`building.textile_mill`](#buildingtextile_mill) | building | C REALIZATION | no | none | `water_frame`, `power_loom` |
| [`building.steelworks`](#buildingsteelworks) | building | C REALIZATION | no | none | `bessemer`, `open_hearth` |
| [`building.chemical_works`](#buildingchemical_works) | building | C REALIZATION | no | none | `sulphuric_acid` |
| [`building.cement_works`](#buildingcement_works) | building | C REALIZATION | no | none | `portland_cement` |
| [`building.refinery`](#buildingrefinery) | building | C REALIZATION | no | none | `petroleum_refining` |
| [`building.power_station`](#buildingpower_station) | building | C REALIZATION | no | none | `electricity_generation` |
| [`building.hydroelectric_dam`](#buildinghydroelectric_dam) | building | C REALIZATION | no | none | `hydroelectric` |
| [`building.nuclear_power_station`](#buildingnuclear_power_station) | building | C REALIZATION | no | none | `nuclear_power` |
| [`building.school`](#buildingschool) | building | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script`, `abjad`, `brahmi` |
| [`building.library`](#buildinglibrary) | building | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script`, `papyrus`, `paper` |
| [`building.university`](#buildinguniversity) | building | C REALIZATION | no | none | `medicine_hippocratic`, `geometry_axiomatic`, `algebra` |
| [`building.hospital`](#buildinghospital) | building | C REALIZATION | no | none | `medicine_hippocratic`, `pharmacology` |
| [`building.observatory`](#buildingobservatory) | building | C REALIZATION | no | none | `astronomy_geometric`, `astrolabe`, `telescope` |
| [`building.research_laboratory`](#buildingresearch_laboratory) | building | C REALIZATION | no | none | `chemistry_quantitative` |
| [`building.printing_house`](#buildingprinting_house) | building | C REALIZATION | no | none | `printing_press`, `woodblock_print` |
| [`building.mint`](#buildingmint) | building | C REALIZATION | no | none | `coinage_electrum` |
| [`building.bathhouse`](#buildingbathhouse) | building | C REALIZATION | no | none | `aqueduct_arcade`, `vault_dome` |
| [`building.city_wall`](#buildingcity_wall) | building | C REALIZATION | no | none | `mudbrick`, `stone_dry` |
| [`building.castle`](#buildingcastle) | building | C REALIZATION | no | none | `stone_fortification`, `vault_dome` |
| [`building.bastion_fort`](#buildingbastion_fort) | building | C REALIZATION | no | none | `trace_italienne` |
| [`building.harbour`](#buildingharbour) | building | C REALIZATION | no | none | `plank_boat`, `stone_dry` |
| [`building.lighthouse`](#buildinglighthouse) | building | C REALIZATION | no | none | `stone_fortification` |
| [`building.dry_dock`](#buildingdry_dock) | building | C REALIZATION | no | none | `canal_lock` |
| [`building.airfield`](#buildingairfield) | building | C REALIZATION | no | none | `aircraft` |
| [`building.radio_transmitter`](#buildingradio_transmitter) | building | C REALIZATION | no | none | `radio` |
| [`building.skyscraper`](#buildingskyscraper) | building | C REALIZATION | no | none | `skyscraper` |
| [`building.spaceport`](#buildingspaceport) | building | C REALIZATION | no | none | `rocket` |
| [`building.semiconductor_fab`](#buildingsemiconductor_fab) | building | C REALIZATION | no | none | `integrated_circuit` |
| [`building.data_centre`](#buildingdata_centre) | building | C REALIZATION | no | none | `cloud_computing` |
| [`building.desalination_plant`](#buildingdesalination_plant) | building | C REALIZATION | no | none | `desalination` |
| [`building.solar_farm`](#buildingsolar_farm) | building | C REALIZATION | no | none | `solar_pv` |
| [`building.wind_farm`](#buildingwind_farm) | building | C REALIZATION | no | none | `wind_turbine_modern` |
| [`building.fusion_plant`](#buildingfusion_plant) | building | C REALIZATION | no | none | `fusion_power` |
| [`unit.spearmen`](#unitspearmen) | unit | C REALIZATION | no | none | `hafting`, `cordage`, `adhesive_natural`, `fire_making` |
| [`unit.archers`](#unitarchers) | unit | C REALIZATION | no | none | `bow_simple`, `cordage` |
| [`unit.slingers`](#unitslingers) | unit | C REALIZATION | no | none | `sling`, `cordage` |
| [`unit.chariot`](#unitchariot) | unit | C REALIZATION | no | none | `chariot`, `wheel_spoked`, `horse_domestication`, `cattle`, `casting_closed` |
| [`unit.horse_archers`](#unithorse_archers) | unit | C REALIZATION | no | none | `cavalry`, `composite_bow`, `horse_domestication`, `bow_simple`, `sheep_goat`, `birch_tar` |
| [`unit.heavy_cavalry`](#unitheavy_cavalry) | unit | C REALIZATION | no | none | `stirrup`, `saddle`, `iron_bloomery`, `cavalry` |
| [`unit.crossbowmen`](#unitcrossbowmen) | unit | C REALIZATION | no | none | `crossbow`, `bow_simple`, `casting_closed`, `tin_bronze` |
| [`unit.longbowmen`](#unitlongbowmen) | unit | C REALIZATION | no | none | `longbow`, `bow_simple` |
| [`unit.siege_engine`](#unitsiege_engine) | unit | C REALIZATION | no | none | `siege_ram`, `torsion_artillery`, `trebuchet`, `wheel_solid`, `hide_working`, `crossbow`, `mechanics_archimedean` |
| [`unit.artillery`](#unitartillery) | unit | C REALIZATION | no | none | `cannon_early`, `cannon_cast_bronze`, `cannon_cast_iron`, `gunpowder`, `cast_iron` |
| [`unit.musketeers`](#unitmusketeers) | unit | C REALIZATION | no | none | `matchlock`, `flintlock`, `cannon_early`, `crossbow`, `wheellock` |
| [`unit.war_galley`](#unitwar_galley) | unit | C REALIZATION | no | none | `naval_ram`, `plank_boat`, `casting_closed`, `dugout` |
| [`unit.ship_of_the_line`](#unitship_of_the_line) | unit | C REALIZATION | no | none | `ship_of_line`, `carrack`, `cannon_cast_iron` |
| [`unit.ironclad`](#unitironclad) | unit | C REALIZATION | no | none | `screw_propeller`, `iron_hull`, `steamboat`, `water_screw`, `puddling` |
| [`unit.tank`](#unittank) | unit | C REALIZATION | no | none | `tank`, `internal_combustion`, `basic_process`, `cannon_cast_iron` |
| [`unit.military_aircraft`](#unitmilitary_aircraft) | unit | C REALIZATION | no | none | `aircraft`, `internal_combustion` |
| [`unit.missile`](#unitmissile) | unit | C REALIZATION | no | none | `rocket`, `internal_combustion`, `aluminium`, `calculus` |
| [`unit.nuclear_submarine`](#unitnuclear_submarine) | unit | C REALIZATION | no | none | `nuclear_submarine`, `nuclear_fission`, `screw_propeller` |
| [`unit.icbm`](#uniticbm) | unit | C REALIZATION | no | none | `icbm`, `rocket`, `thermonuclear`, `computer` |
| [`unit.combat_drone`](#unitcombat_drone) | unit | C REALIZATION | no | none | `combat_drone`, `gps`, `microprocessor`, `aircraft` |
| [`unit.stealth_aircraft`](#unitstealth_aircraft) | unit | C REALIZATION | no | none | `stealth`, `composites`, `radar`, `computer` |
| [`unit.aircraft_carrier`](#unitaircraft_carrier) | unit | C REALIZATION | no | none | `aircraft`, `steam_turbine`, `steam_high_pressure`, `open_hearth` |
| [`unit.helicopter`](#unithelicopter) | unit | C REALIZATION | no | none | `helicopter`, `aircraft` |
| [`activity.farming`](#activityfarming) | activity | A BASELINE | yes | capability | `cereal_cultivation`, `root_crop`, `rice_wet`, `millet`, `maize`, `sorghum_pearl_millet`, `cereal_domesticated`, `grinding_stone`, `animal_traction` |
| [`activity.herding`](#activityherding) | activity | A BASELINE | yes | capability | `sheep_goat`, `cattle`, `dog_domestication` |
| [`activity.fishing`](#activityfishing) | activity | A BASELINE | yes | capability | `fishing_hook`, `cordage`, `basketry`, `dugout` |
| [`activity.logging`](#activitylogging) | activity | A BASELINE | yes | capability | `ground_stone_early`, `knapping_oldowan`, `hafting` |
| [`activity.mining`](#activitymining) | activity | A BASELINE | yes | capability | `mining_shaft`, `stone_dry`, `copper_smelting` |
| [`activity.caravans`](#activitycaravans) | activity | C REALIZATION | no | none | `donkey`, `camel`, `cattle` |
| [`activity.coastal_shipping`](#activitycoastal_shipping) | activity | C REALIZATION | no | none | `sail_square`, `dugout`, `loom_warp_weighted`, `plank_boat` |
| [`activity.ocean_shipping`](#activityocean_shipping) | activity | C REALIZATION | no | none | `carrack`, `caravel`, `polynesian_canoe`, `dugout`, `latitude_sailing`, `stern_rudder`, `compass_magnetic` |
| [`infra.dirt_path`](#infradirt_path) | infrastructure (NetworkEdgeRow, EdgeTypes.DirtPath) | C REALIZATION | partial | capability | `track_road`, `stone_dry`, `road_paved` |
| [`infra.road_track`](#infraroad_track) | infrastructure | C REALIZATION | no | none | `track_road` |
| [`infra.road_built`](#infraroad_built) | infrastructure | C REALIZATION | no | none | `track_road`, `stone_dry` |
| [`infra.road_paved`](#infraroad_paved) | infrastructure | C REALIZATION | no | none | `road_paved` |
| [`infra.road_macadam`](#infraroad_macadam) | infrastructure | C REALIZATION | no | none | `macadam`, `road_paved` |
| [`infra.courier_relay`](#infracourier_relay) | infrastructure | C REALIZATION | no | none | `track_road`, `horse_domestication` |
| [`infra.bridge`](#infrabridge) | infrastructure | C REALIZATION | no | none | `bridge_stone` |
| [`infra.basin_irrigation`](#infrabasin_irrigation) | infrastructure | C REALIZATION | no | none | `irrigation_basin`, `cereal_cultivation` |
| [`infra.canal_irrigation`](#infracanal_irrigation) | infrastructure | C REALIZATION | no | none | `irrigation_canal`, `cereal_cultivation` |
| [`infra.terraces`](#infraterraces) | infrastructure | C REALIZATION | no | none | `terrace`, `stone_dry`, `cereal_cultivation` |
| [`infra.qanat`](#infraqanat) | infrastructure | C REALIZATION | no | none | `qanat`, `well`, `surveying`, `mining_shaft` |
| [`infra.well`](#infrawell) | infrastructure | C REALIZATION | no | none | `well`, `stone_dry` |
| [`infra.aqueduct`](#infraaqueduct) | infrastructure | C REALIZATION | no | none | `aqueduct_tunnel`, `aqueduct_channel`, `aqueduct_arcade`, `qanat`, `surveying` |
| [`infra.urban_drainage`](#infraurban_drainage) | infrastructure | C REALIZATION | no | none | `fired_brick`, `kiln_updraft`, `mudbrick` |
| [`infra.sewer`](#infrasewer) | infrastructure | C REALIZATION | no | none | `vault_dome`, `fired_brick` |
| [`infra.sewerage_system`](#infrasewerage_system) | infrastructure | C REALIZATION | no | none | `germ_theory`, `reinforced_concrete` |
| [`infra.fish_weir`](#infrafish_weir) | infrastructure | C REALIZATION | no | none | `basketry`, `cordage` |
| [`infra.navigation_canal`](#infranavigation_canal) | infrastructure | C REALIZATION | no | none | `canal_navigation`, `canal_lock`, `surveying` |
| [`infra.railway`](#infrarailway) | infrastructure | C REALIZATION | no | none | `railway`, `locomotive`, `puddling`, `surveying` |
| [`infra.telegraph_line`](#infratelegraph_line) | infrastructure | C REALIZATION | no | none | `telegraph`, `electromagnetism`, `wire_drawing` |
| [`infra.submarine_cable`](#infrasubmarine_cable) | infrastructure | C REALIZATION | no | none | `submarine_cable`, `telegraph`, `screw_propeller` |
| [`infra.telephone_network`](#infratelephone_network) | infrastructure | C REALIZATION | no | none | `telephone`, `telegraph` |
| [`infra.power_grid`](#infrapower_grid) | infrastructure | C REALIZATION | no | none | `electricity_generation`, `dynamo`, `electric_light`, `electromagnetism` |
| [`infra.mobile_network`](#inframobile_network) | infrastructure | C REALIZATION | no | none | `mobile_phone`, `radio`, `microprocessor`, `telephone` |
| [`infra.internet_backbone`](#infrainternet_backbone) | infrastructure | C REALIZATION | no | none | `internet`, `computer`, `telephone`, `integrated_circuit` |
| [`inst.burial`](#instburial) | institution | A BASELINE | partial | capability | — |
| [`inst.scribal_school`](#instscribal_school) | institution | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.law_code`](#instlaw_code) | institution | C REALIZATION | no | none | `law_code`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.archive`](#instarchive) | institution | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal` |
| [`inst.textile_workshop`](#insttextile_workshop) | institution | C REALIZATION | no | none | `loom_warp_weighted`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.aramaic_admin`](#instaramaic_admin) | institution | C REALIZATION | no | none | `abjad`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.natural_philosophy`](#instnatural_philosophy) | institution | C REALIZATION | no | none | `astronomy_records`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.library`](#instlibrary) | institution | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal` |
| [`inst.coined_wage`](#instcoined_wage) | institution | C REALIZATION | no | none | `coined_wage`, `coinage_electrum` |
| [`inst.census`](#instcensus) | institution | C REALIZATION | no | none | `census`, `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal` |
| [`inst.legal_code_roman`](#instlegal_code_roman) | institution | C REALIZATION | no | none | `legal_code_roman`, `law_code` |
| [`inst.historiography`](#insthistoriography) | institution | C REALIZATION | no | none | `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal` |
| [`inst.military_medicine`](#instmilitary_medicine) | institution | C REALIZATION | no | none | `surgery_instruments`, `census` |
| [`inst.mint_institution`](#instmint_institution) | institution | C REALIZATION | no | none | `coinage_electrum`, `census` |
| [`inst.university`](#instuniversity) | institution | C REALIZATION | no | none | `legal_code_roman`, `law_code`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.hospital`](#insthospital) | institution | C REALIZATION | no | none | `medicine_hippocratic`, `pharmacology` |
| [`inst.translation_movement`](#insttranslation_movement) | institution | C REALIZATION | no | none | `paper`, `cordage`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.guild`](#instguild) | institution | C REALIZATION | no | none | `legal_code_roman`, `law_code`, `loom_warp_weighted`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.quarantine`](#instquarantine) | institution | C REALIZATION | no | none | `medicine_hippocratic`, `pharmacology`, `census` |
| [`inst.newspaper`](#instnewspaper) | institution | C REALIZATION | no | none | `printing_press` |
| [`inst.scientific_society`](#instscientific_society) | institution | C REALIZATION | no | none | `printing_press`, `optics_ibn_haytham`, `legal_code_roman`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script` |
| [`inst.joint_stock`](#instjoint_stock) | institution | C REALIZATION | no | none | `joint_stock`, `bill_of_exchange`, `legal_code_roman` |
| [`inst.central_bank`](#instcentral_bank) | institution | C REALIZATION | no | none | `joint_stock`, `paper_money` |
| [`inst.patent`](#instpatent) | institution | C REALIZATION | no | none | `patent`, `legal_code_roman`, `printing_press` |
| [`inst.standard_threads`](#inststandard_threads) | institution | C REALIZATION | no | none | `slide_rest_lathe` |
| [`inst.statistics_vital`](#inststatistics_vital) | institution | C REALIZATION | no | none | `census`, `calculus` |
| [`inst.mass_schooling`](#instmass_schooling) | institution | C REALIZATION | no | none | `printing_press`, `census` |
| [`inst.mass_media_broadcast`](#instmass_media_broadcast) | institution | C REALIZATION | no | none | `radio`, `television`, `printing_press`, `census` |
| [`project.orbital_launch`](#projectorbital_launch) | project | C REALIZATION | no | none | `rocket` |
| [`project.probe_acceleration`](#projectprobe_acceleration) | project | C REALIZATION | no | none | `laser`, `space_solar` |
| [`project.interstellar_probe`](#projectinterstellar_probe) | project | C REALIZATION | no | none | `fusion_propulsion`, `fusion_power`, `space_probe` |

## 7. The matrix — one record per item

### 7.1 Baseline capabilities declared in research.json

#### `baseline.settlement_founding`

- **Identifier:** `baseline.settlement_founding` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ColonizationSystem (Sim.Core/Systems/Colonization/ColonizationSystem.cs:176-218 founding loop, :225-305 Found), fed by MigrationSystem's unplaced-departure demand (Sim.Core/Systems/Migration/MigrationSystem.cs:477-499, :821). The initial 12 settlements come from worldgen (Sim.Core/Worldgen/WorldFounding.cs:34-40).
- **Current baseline status:** Baseline and simulated (`research.json` `baseline.settlement_founding`  / 6baafbe:31-35, simulated:true). Research-free: Colonization reads no research table. Founding is EMERGENT ONLY: there is no player-directed settler or found-settlement order; the order kinds are 1, 2, 3, 4 and 6 only (Sim.Core/Kernel/OrderLog.cs:12,21,36,49,69).
- **Related research node(s):** `well`
- **Related matrix items:** `infra.well`
- **Realization requirements:** The capability needs no research. Each realized colony (see row settlement.colony) needs all of: (1) MigrationSystem wrote UnplacedDeparture > 0, which happens only when the source is in deficit AND has no reachable viable destination (MigrationSystem.cs:477-499); (2) grain provisions > 0 (ColonizationSystem.cs:199); (3) a frontier site that passes water-access siting (ColonizationSystem.cs:201-206; SettlementSiting.cs:71-86).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation for ownership. Fix data text: the baseline providedBy says colonies are founded 'from settlements' surplus population' (`research.json` `baseline.settlement_founding` `.providedBy`  / 6baafbe:33), but the code triggers on famine-flight demand with no viable exit, not on surplus (MigrationSystem.cs:477-499; ColonizationSystem.cs:48-54). For the Director: D-045 §1 names 'Settlers'; no player founding order exists, which is outside research scope. polynesian_canoe "open-ocean colonisation" (`research.json` `polynesian_canoe` `.unlocks.capabilities`) is non-gating of colonization; see baseline.exploration.

#### `baseline.exploration`

- **Identifier:** `baseline.exploration` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** None; not simulated. Sim.Core has no visibility, known-extent or fog state (a grep for scout|explor|fog|visib finds no system). Terrain is one global immutable raster (Sim.Core/State/WorldState.cs:987  / 6baafbe:954-958, ADR-008).
- **Current baseline status:** Baseline by Director ruling (docs/d045-research-calibration-rulings.md:41), declared simulated:false (`research.json` `baseline.exploration`). In practice the whole map is known from turn 0: colony frontier siting and path targeting both search the global terrain (ColonizationSystem.cs:156-157,201-206; PathBuildSystem.cs:184-185). D-040 B1 ('Settlement, claim and trade all operate only within known extent', docs/d040-discovery-and-control.md:37-39) is not implemented.
- **Related research node(s):** `raft`, `dugout`, `sail_square`, `caravel`, `polynesian_canoe`
- **Related matrix items:** `activity.coastal_shipping`, `activity.ocean_shipping`
- **Realization requirements:** None today, because nothing is simulated. Future, per D-039 B1 (docs/d039-command-fog-and-siege.md:45-50) and D-040 B2: discovery is paid for in food and people (a scout is a person drawn from a bucket) and yields a computed known extent. D-040 B3 (d040:59-64) rules that no research node may open a movement mode.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** No research-content change is needed for founding: no entity gates it. Content constraint (D-040 B2/B3 under the D-044 T3 reading, d044:841): raft "short sea crossing" (`research.json` `raft` `.unlocks.capabilities`), caravel "coastal exploration against wind" (`research.json` `caravel` `.unlocks.capabilities`) and polynesian_canoe "open-ocean colonisation over 3,000 km" (`research.json` `polynesian_canoe` `.unlocks.capabilities`) are recorded as non-gating of exploration, discovery, sea travel and colonization. They may only be additive, or be reworded as additive. Constraint to record: activity.coastal_shipping (requires sail_square, `research.json` `activity.coastal_shipping` `.requires`) and activity.ocean_shipping (requires carrack OR caravel OR polynesian_canoe, `research.json` `activity.ocean_shipping` `.requires`) must not be wired as gates on movement or discovery while the D-044 T3 reading stands (D-040 B3; d044:841); any data or wiring change is blocked on the Director's T3 ruling; see the activity rows. Implementing known extent is a future milestone, not a research change.

#### `baseline.basic_military`

- **Identifier:** `baseline.basic_military` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** None; not simulated. Sim.Core has no unit, soldier, army or combat table or system. A grep for military|army|soldier|warrior|clubm finds only a comment at WorldState.cs:695. The polity row deliberately carries no military state (WorldState.cs:694-697). Code states 'no war and no battle' (Sim.Core/Observability/Forensic/SessionInspector.cs:713).
- **Current baseline status:** Baseline by ruling, 'Clubmen or equivalent basic military' (docs/d045-research-calibration-rulings.md:42), declared simulated:false (`research.json` `baseline.basic_military`). The registry has NO baseline unit: every basic unit needs a node. unit.spearmen requires hafting (`research.json` `unit.spearmen` `.requires`), unit.archers requires bow_simple (`research.json` `unit.archers` `.requires`), unit.slingers requires sling (`research.json` `unit.slingers` `.requires`).
- **Related research node(s):** `hafting`, `bow_simple`, `sling`, `fire_making`, `knapping_oldowan`
- **Related matrix items:** `unit.spearmen`, `unit.archers`, `unit.slingers`
- **Realization requirements:** None today, because nothing is simulated. A future unit would be personnel drawn from buckets and realized by a military system (D-039 B1 sets that precedent for scouts). Coercion without units already exists: stateless settlements take grain (AppropriationSystem.cs:130-160).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** No simulation change now. Content constraint for the military packet: the founding unit must have a null knowledge requirement. It cannot be unit.spearmen, unit.archers or unit.slingers, which the graph owns behind hafting ('thrusting spear', `research.json` `hafting` `.unlocks.capabilities`; cheapest closure 3 nodes, 1,830 RP), bow_simple ('ranged combat', `research.json` `bow_simple` `.unlocks.capabilities`) and sling (`research.json` `sling` `.unlocks.capabilities`). fire_making's technique 'Thrown spear' (`research.json` `fire_making` `.unlocks.techniques`) must not be read as the baseline weapon. D-045 forbids a research node for clubmen (d045:57-62). Equipment constraint: the founding unit's equipment must be owned by no node. That rules out hafted axes and adzes (hafting, `research.json` `hafting` `.unlocks.capabilities`), edged stone tools (knapping_oldowan, `research.json` `knapping_oldowan` `.unlocks.capabilities`) and fire-hardened weapons (fire_making, `research.json` `fire_making` `.unlocks.techniques`). Define it as unhafted wooden clubs or bare manpower, or else record those strings as non-owning of baseline equipment (the good.timber precedent).

#### `baseline.food_gathering`

- **Identifier:** `baseline.food_gathering` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** Parent of sector.farming, sector.herding_fishing and sector.extraction (ProductionSystem.cs:165-189). HarvestWeatherSystem × the disaster multiplier supply only the food multiplier (ProductionSystem.cs:163).
- **Current baseline status:** Baseline and simulated (`research.json` `baseline.food_gathering`). All four food and raw-material pathways run from turn 1 at the default shares, with zero research (WorldState.cs:375-376). This is cereal farming, herding, fishing and extraction, not foraging: no hunting or foraging sector exists (WorldState.cs:320-325).
- **Related research node(s):** `grinding_stone`, `root_crop`, `cereal_cultivation`, `cereal_domesticated`, `sheep_goat`, `cattle`, `fishing_hook`, `ground_stone_early`, `mining_shaft`
- **Related matrix items:** `activity.farming`, `activity.herding`, `activity.fishing`, `activity.logging`, `activity.mining`
- **Realization requirements:** See the component sector rows (sector.farming, sector.herding_fishing, sector.extraction).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** `research.json` `baseline.food_gathering` `.providedBy` providedBy → "ProductionSystem — Farming, Herding/fishing and Extraction sectors produce food and goods with no research (HarvestWeatherSystem supplies only the harvest multiplier)"; regenerate docs/research-corpus-audit.md with scripts/research-content-audit.py (CI runs --check). The five activity entities must never gate the sectors (see their rows). Listed for M5 / Director (D-046 Part D item 6), not implemented.

#### `baseline.construction`

- **Identifier:** `baseline.construction` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ConstructionSystem: enqueue (ConstructionSystem.cs:77-98), head-of-queue gate and whole-project completion (:100-117), materials (:149-160), completion (:179-189). Its capacity input is the sector.construction pool (see that row).
- **Current baseline status:** Baseline and simulated (`research.json` `baseline.construction`). Neither enqueue nor resolve has a technology predicate (ConstructionSystem.cs:92-95; docs/adr/adr-028-viability-and-saturation.md:57), and the project schema has no requires field (Sim.Core/Systems/GoodsConfig.cs:81-85). There is no Builder unit.
- **Related research node(s):** none
- **Related matrix items:** `building.granary`, `building.workshop`
- **Realization requirements:** The capability is baseline. Each structure needs: an EnqueueConstruction order (OrderLog.cs:49) from the controlling Empire (ConstructionSystem.cs:92-93); the project at the head of the queue, with one completion per settlement per turn; sector.construction capacity remaining after housing ≥ laborRequired (ConstructionSystem.cs:111,145; capacity is gated, not spent); and every input in stock (:149-160). The project is built whole in one turn; there is no progress field (WorldState.cs:731-742).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** No simulation change for ownership. Fix text: the providedBy (`research.json` `baseline.construction` `.providedBy` ) and D-045 §1 both describe 'capacity -> project allocation -> progress'. In the code the queue row has no progress field by design (WorldState.cs:731-741), and labour is checked but never spent (ConstructionSystem.cs:9-11,125-146). Correct the text or record the deviation, keeping 'no Builder', which ResearchBaselineTests.cs:55 asserts. Gap, not research: no Sim.Ui or Sim.Cli factory issues EnqueueConstruction, so only an order log can reach this capability.

#### `baseline.migration`

- **Identifier:** `baseline.migration` (baseline-capability)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** MigrationSystem (Sim.Core/Systems/Migration/MigrationSystem.cs:12-40 gap and flight channels; :821 unplaced write; :882 Ledger.Transfer), pipeline position Sim.Data/content/pipeline.json:14.
- **Current baseline status:** Baseline and simulated (`research.json` `baseline.migration`). Reads no research table. Runs from turn 1.
- **Related research node(s):** none
- **Realization requirements:** Two or more settlements with finite SettlementDistances (published by Catchment) and either an attractiveness gap or a food deficit, plus destination viability > 0. People move as conserved bucket transfers.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

### 7.2 The research engine itself

#### `research.engine`

- **Identifier:** `research.engine` (baseline-capability (research system))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ResearchSystem (Sim.Core/Systems/Research/ResearchSystem.cs:16-56, :79-83) and ResearchQuery (Sim.Core/State/ResearchQuery.cs:339-372). Pipeline: Sim.Data/content/pipeline.json:20. Registration: Sim.Core/SystemCatalog.cs:320-328. Order: Sim.Core/Kernel/OrderLog.cs:54-69 (SetResearchTarget = 6).
- **Current baseline status:** Runs from turn 1 with zero completed nodes; founding writes no research row (ResearchEngineTests.cs:95-99). RP = 0.08 × population^0.699 per turn (`research.json` `tuning.rpPerTurn`; ADR-030). Main-tree and Civics nodes are available by prerequisite; the five subtrees open only at the research stage (`research.json` `researchStage`; see building.university). The Eureka credit mechanism exists, but no Eureka can fire at founding: the available roots' Eurekas have null conditions (ResearchSystem.cs:117-119, :130). The foreign-exposure seam (WorldState.cs:846-855) and the university cost seam (WorldState.cs:857-875; keys = universityTypes, `research.json` `universityTypes`) are read-only inputs with no writer.
- **Related research node(s):** none
- **Realization requirements:** A polity with controlled population, and a target set by SetResearchTarget.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None. The cost-seam writer (an institutions system that owes the D-021 brake) is listed under building.university and inst.university; the foreign-exposure writer is a future contact/diffusion system, outside G3. Research itself is owned by no node (not writing, not a library).

### 7.3 Simulation capabilities, objects and mechanisms running at founding

#### `sector.farming`

- **Identifier:** `sector.farming` (sector (Sectors.Farming = 0))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.Farm (ProductionSystem.cs:165-167 call, :196-261). Sector id at WorldState.cs:320; default share 0.55 at WorldState.cs:375-376.
- **Current baseline status:** Baseline, live from turn 1 with zero research. Output = min(arable km² × 26, farmers × 5 × tool factor) × weather × disaster (sim.json:5-6; ProductionSystem.cs:230-234,253). Same simulated pathway as corpus entity activity.farming (`research.json` `activity.farming` `.requires`); the whole activity is baseline; no part is research-owned today. Component of baseline.food_gathering.
- **Related research node(s):** `root_crop`, `cereal_cultivation`, `cereal_domesticated`, `grinding_stone`, `sickle`, `digging_stick_hoe`, `animal_traction`, `irrigation_basin`
- **Related matrix items:** `activity.farming`
- **Realization requirements:** Adults × farming share; arable km² from the previous turn's catchment, which is empty at turn 0, so turn-1 harvest = 0 (ProductionSystem.cs:211-216; a measurement probe at 6baafbe (not committed)). Tools raise the labour side by up to 30% (ProductionSystem.cs:218-230).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Research content: activity.farming requires 'cereal_cultivation OR root_crop OR rice_wet OR millet OR maize OR sorghum_pearl_millet' (`research.json` `activity.farming` `.requires`). If wired, no grain would be harvested at founding, so it must not gate Sectors.Farming: null the requirement or re-scope it to a crop-specific activity, and update the six nodes' unlocks.activities to match. Director decision (epoch, CR-006 open): the 26.0 yield is derived for domesticated rainfed cereal with threshing loss, fallow and a traction-and-manure herd (sim.json:4). The graph assigns exactly those capabilities to cereal_domesticated ('reliable yield', 'storable surplus', `research.json` `cereal_domesticated` `.unlocks.capabilities`) and animal_traction ('ploughing', `research.json` `animal_traction` `.unlocks.capabilities`). root_crop's 'no granary needed — stored in ground' (`research.json` `root_crop` `.unlocks.capabilities`) contradicts the grain-and-granary model. No simulation change.

#### `sector.herding_fishing`

- **Identifier:** `sector.herding_fishing` (sector (Sectors.Herding = 1, herding/fishing))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector true (ProductionSystem.cs:180-184, :287-331); food set livestock+fish (:373-377); WorldState.cs:321; default share 0.15 (:376); 3.0 per herder-year (sim.json:191).
- **Current baseline status:** Baseline, research-free from turn 1. A single labour pool produces both livestock and fish. Same simulated pathway as corpus entity activity.herding (`research.json` `activity.herding` `.requires`); the whole activity is baseline; no part is research-owned today. Same simulated pathway as corpus entity activity.fishing (`research.json` `activity.fishing` `.requires`); the whole activity is baseline; no part is research-owned today. Component of baseline.food_gathering.
- **Related research node(s):** `dog_domestication`, `sheep_goat`, `cattle`, `fishing_hook`, `basketry`, `cordage`, `dugout`
- **Related matrix items:** `activity.herding`, `activity.fishing`, `infra.fish_weir`
- **Realization requirements:** Herders split by livestock and fish deposit abundance; no deposit means no output (ProductionSystem.cs:303). The harvest-weather and disaster multiplier applies.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Research content: activity.herding (requires sheep_goat OR cattle, `research.json` `activity.herding` `.requires`) and activity.fishing (requires fishing_hook, `research.json` `activity.fishing` `.requires`) must not gate this sector. Reword sheep_goat's capability 'herding sector' (`research.json` `sheep_goat` `.unlocks.capabilities`), which claims the baseline sector. infra.fish_weir (requires basketry, `research.json` `infra.fish_weir` `.requires`) is consistent only as an additive structure. No simulation change.

#### `sector.extraction`

- **Identifier:** `sector.extraction` (sector (Sectors.Extraction = 2))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector false and weather 1.0 (ProductionSystem.cs:185-189, :287-331); WorldState.cs:322; default share 0.10; 4.0 per extractor-year (sim.json:192).
- **Current baseline status:** Baseline, research-free from turn 1. Produces timber, stone, clay, copper-ore, tin-ore, fiber and hides from deposits. ADR-028 §4: 'A mine is not a prerequisite for extraction'. Same simulated pathway as corpus entity activity.logging (`research.json` `activity.logging` `.requires`); the whole activity is baseline; no part is research-owned today. Same simulated pathway as corpus entity activity.mining (`research.json` `activity.mining` `.requires`); the whole activity is baseline; no part is research-owned today. Component of baseline.food_gathering.
- **Related research node(s):** `ground_stone_early`, `hafting`, `copper_native`, `mining_shaft`, `stone_dry`
- **Related matrix items:** `activity.logging`, `activity.mining`
- **Realization requirements:** Extractors are split by deposit abundance, so the player cannot target one good (ProductionSystem.cs:314). Deposit abundance > 0 is required.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Research content: activity.logging (requires ground_stone_early, `research.json` `activity.logging` `.requires`) and activity.mining (requires mining_shaft, `research.json` `activity.mining` `.requires`; A3 d5; cheapest closure 12 nodes, 19,080 RP) must not gate extraction. Reword ground_stone_early 'felling' (`research.json` `ground_stone_early` `.unlocks.capabilities`) and mining_shaft 'ore from depth' (`research.json` `mining_shaft` `.unlocks.capabilities`) as additive, not as ownership of baseline extraction. No simulation change.

#### `sector.crafting`

- **Identifier:** `sector.crafting` (sector (Sectors.Crafting = 3))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.Craft (ProductionSystem.cs:190-191 call, :381-496). Recipe availability is a standard-dialect predicate over published variables (:105-108, :390-397); labour is split equally across available recipes (:403).
- **Current baseline status:** Baseline sector, research-free; default share 0.12 (WorldState.cs:376). pottery-firing and weaving are ungated; bronze-casting and toolmaking are artisan-gated.
- **Related research node(s):** `pottery_open_fired`, `kiln_updraft`, `spinning_spindle`, `loom_warp_weighted`, `copper_smelting`, `tin_bronze`
- **Related matrix items:** `building.kiln`, `building.smithy`, `building.workshop`, `inst.textile_workshop`
- **Realization requirements:** Adults × crafting share. Each recipe is Leontief over labour and every input; nothing is produced without inputs (ProductionSystem.cs:416-467).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for the sector itself (recipe ownership is in the recipe rows). Gating any recipe on research needs code: recipe 'requires' is parsed in the standard dialect (ProductionSystem.cs:105-108; GoodsConfig.cs:241-245), and research atoms exist only in the research dialect (Predicate.cs 6baafbe:28-40).

#### `sector.construction`

- **Identifier:** `sector.construction` (sector (Sectors.Construction = 4))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** Shared construction labour pool (WorldState.cs:324; default 0.08 at :376). Consumers, in order: HousingSystem spends first and publishes LastLaborUsed (HousingSystem.cs:150,183); ConstructionSystem gates on the remainder without spending it (ConstructionSystem.cs:137-145); PathBuildSystem accrues from the same remainder (PathBuildSystem.cs:132,139-147). It feeds baseline.construction, stock.dwellings and infra.dirt_path.
- **Current baseline status:** Baseline labour pool, research-free. Housing spends from it first; path accrual and project capacity read what remains.
- **Related research node(s):** `track_road`
- **Realization requirements:** Adults × construction share × dt, minus HousingRow.LastLaborUsed for paths and projects (PathBuildSystem.cs:139-145; ConstructionSystem.cs:137-143).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

#### `good.grain`

- **Identifier:** `good.grain` (good (food, numeraire))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:5-11. Founding endowment WorldFounding.cs:146-163 (the only good endowed). Production ProductionSystem.Farm :196-261. Spoilage and granary cap ConsumptionSystem.cs:294-325.
- **Current baseline status:** Baseline object and capability: 6000 × population scale × a small per-capita wobble per settlement (sim.json:113); seed 42 total 82,230. Measured turn 1: 38,920 eaten, 23,843 spoiled, 13,635 granary overflow, 0 harvested.
- **Related research node(s):** `cereal_cultivation`, `cereal_domesticated`, `grinding_stone`, `root_crop`
- **Related matrix items:** `activity.farming`
- **Realization requirements:** Farming labour and arable land; stores are subject to spoilage (0.08/yr) and the 1.5-years-of-demand ceiling.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** both
- **Required code/data changes:** None to the simulation. Content issues are the same as sector.farming.

#### `good.livestock`

- **Identifier:** `good.livestock` (good (food))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:12-18 (depositChannel moisture); herding (ProductionSystem.cs:180-184,373-377); class baskets (needs.json:76-80).
- **Current baseline status:** Baseline. The stock row exists at 0 at founding (WorldFounding.cs:151-163). Produced from turn 1 with no research: 3,800 at t1 seed 42. Modelled as food only; no traction, dairy or wool.
- **Related research node(s):** `dog_domestication`, `sheep_goat`, `cattle`
- **Related matrix items:** `activity.herding`
- **Realization requirements:** Herding labour × livestock deposit abundance × 3.0/yr × weather.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content issues are the same as sector.herding_fishing.

#### `good.fish`

- **Identifier:** `good.fish` (good (food))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:19-25 (depositChannel water); produced in the herding/fishing sector (ProductionSystem.cs:180-184,373-377); baskets (needs.json:82-86).
- **Current baseline status:** Baseline. Stock is 0 at founding; produced from turn 1 with no research (3,077 at t1 seed 42).
- **Related research node(s):** `fishing_hook`, `basketry`, `cordage`, `dugout`
- **Related matrix items:** `activity.fishing`, `infra.fish_weir`
- **Realization requirements:** Herding labour × fish (water-channel) deposit × 3.0/yr × weather.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: activity.fishing (requires fishing_hook, `research.json` `activity.fishing` `.requires`) must not gate fish. fishing_hook 'pelagic fishing' (`research.json` `fishing_hook` `.unlocks.capabilities`) and dugout 'offshore fishing' (`research.json` `dugout` `.unlocks.capabilities`) can only be additive, since no sea fishery is modelled.

#### `good.timber`

- **Identifier:** `good.timber` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:26-32 (moisture); extraction (ProductionSystem.cs:185-189). Consumers: housing build and upkeep (sim.json:209,211; HousingSystem.cs:111-124,175), pottery-firing (goods.json:110-113), bronze-casting (:146-149), toolmaking (:165-168), projects (:184-187,:200-203).
- **Current baseline status:** Baseline, extracted from turn 1 with no research (1,342 at t1 seed 42). Stock is 0 at founding.
- **Related research node(s):** `ground_stone_early`, `hafting`
- **Related matrix items:** `activity.logging`
- **Realization requirements:** Extraction labour × moisture deposit × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: activity.logging (requires ground_stone_early, `research.json` `activity.logging` `.requires`) must not gate timber. Treat ground_stone_early 'felling' (`research.json` `ground_stone_early` `.unlocks.capabilities`) and hafting 'axe' (`research.json` `hafting` `.unlocks.capabilities`) as non-owning of baseline timber.

#### `good.stone`

- **Identifier:** `good.stone` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:33-39 (elevation); extraction (ProductionSystem.cs:185-189). The ONLY consumers are construction projects (goods.json:188-191,204-207).
- **Current baseline status:** Baseline, extracted from turn 1 with no research. Elevation deposits are small (seed 42 stone abundance 0.012–0.109, a measurement probe at 6baafbe (not committed)), so only 9 stone exist at t1. Stone binds the granary: the first settlement holds 40 timber + 20 stone at t8. No stone-tool or stone-building path exists.
- **Related research node(s):** `knapping_oldowan`, `ground_stone_early`, `stone_dry`
- **Related matrix items:** `activity.mining`
- **Realization requirements:** Extraction labour × elevation deposit × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: activity.mining (`research.json` `activity.mining` `.requires`) must not gate quarrying.

#### `good.clay`

- **Identifier:** `good.clay` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:40-46 (water channel); extraction (ProductionSystem.cs:185-189); its only consumer is pottery-firing (goods.json:106-109). Housing draws no clay (sim.json:210,212).
- **Current baseline status:** Baseline and research-free. Measured: 0 at every turn end t1–t40 seed 42, because pottery-firing consumes all of it.
- **Related research node(s):** `pottery_open_fired`, `mudbrick`
- **Realization requirements:** Extraction labour × water-channel deposit × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

#### `good.copper_ore`

- **Identifier:** `good.copper_ore` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:47-54 (elevation, depositSpread 0.8); extraction (ProductionSystem.cs:185-189); its only consumer is bronze-casting (goods.json:138-141).
- **Current baseline status:** Baseline: ore is extracted from turn 1 with no research or mine (23 at t1 seed 42).
- **Related research node(s):** `copper_native`, `copper_smelting`, `mining_shaft`
- **Related matrix items:** `activity.mining`
- **Realization requirements:** Extraction labour × elevation deposit × seeded spread × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: activity.mining (requires mining_shaft, `research.json` `activity.mining` `.requires`) must not gate ore extraction (adr-028:59).

#### `good.tin_ore`

- **Identifier:** `good.tin_ore` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:55-62 (elevation, depositSpread 0.9); extraction (ProductionSystem.cs:185-189); its only consumer is bronze-casting (goods.json:142-145).
- **Current baseline status:** Baseline, extracted from turn 1 with no research (16 at t1 seed 42).
- **Related research node(s):** `tin_bronze`, `mining_shaft`
- **Related matrix items:** `activity.mining`
- **Realization requirements:** Extraction labour × elevation deposit × seeded spread × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

#### `good.fiber`

- **Identifier:** `good.fiber` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:63-69 (moisture); extraction (ProductionSystem.cs:185-189); its only consumer is weaving (goods.json:123-128).
- **Current baseline status:** Baseline and research-free. It comes from moisture deposits, with no crop or animal source. Measured: 0 at every turn end t1–t40 seed 42 because weaving consumes it all.
- **Related research node(s):** `flax`, `wool`, `cordage`, `spinning_spindle`
- **Realization requirements:** Extraction labour × moisture deposit × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: flax 'linen fibre' (`research.json` `flax` `.unlocks.capabilities`) and wool 'wool fibre' (`research.json` `wool` `.unlocks.capabilities`) claim fiber sources the founding economy already has. Record them as non-gating.

#### `good.hides`

- **Identifier:** `good.hides` (good (raw))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:70-76 (moisture); produced by the EXTRACTION sector, not herding (ProductionSystem.cs:185-189,373-377). No consumer anywhere: it is in no recipe (goods.json:102-177), no basket (needs.json:68-158 lists only grain, livestock, fish, pottery and cloth) and no housing draw.
- **Current baseline status:** Baseline and research-free. It accumulates without a sink: 113,436 at t40 seed 42.
- **Related research node(s):** `hide_working`, `sewing`
- **Realization requirements:** Extraction labour × moisture deposit × 4.0/yr.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for ownership. Simulation gap, not research: hides have no consumer.

#### `good.bronze`

- **Identifier:** `good.bronze` (good (processed))
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** goods.json:77-82. Its only source is bronze-casting (goods.json:135-157, requires artisan_share > 0.05 at :156); its only consumer is toolmaking (:161-164).
- **Current baseline status:** Research-free today, gated only by personnel (Artisans). It cannot exist at founding: Artisans are dormant and no bronze is endowed (WorldFounding.cs:119-123,151-163). The graph claims the knowledge: copper_smelting 'cast copper tools' (`research.json` `copper_smelting` `.unlocks.capabilities`) and tin_bronze 'standardised alloy' and 'Bronze weapons' (`research.json` `tin_bronze` `.unlocks.capabilities`; A3 d5; cheapest closure 9 nodes, 21,230 RP ≈ 690 turns at the founding 30.8 RP/turn, derived, ignoring growth and credit).
- **Related research node(s):** `copper_native`, `copper_smelting`, `tin_bronze`, `charcoal`
- **Related matrix items:** `building.smithy`
- **Realization requirements:** Artisans latched active with artisan_share > 0.05 (goods.json:156; emergence needs food_surplus_ratio > 1.3 && population > 520, sim.json:169). Each unit needs copper-ore 8, tin-ore 1, timber 2 and crafting labour 0.4. Bronze is fully consumed by toolmaking (stock 0 at every turn end t1–t40 seed 42).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Director decision; the ownership conflict stays latent until availability is wired. Option (a): keep bronze baseline-eligible behind its artisan gate, and reword copper_smelting and tin_bronze so they do not own the simulation's bronze good. Option (b): add a tin_bronze knowledge gate on top of the artisan gate. That needs code, because recipe requires accepts only the standard dialect (ProductionSystem.cs:105-108; Predicate.cs 6baafbe:28-40). It would also delay tools, the farm tool bonus and the workshop, which today appear at t6 seed 42. Neither option changes turn 0.

#### `good.tools`

- **Identifier:** `good.tools` (good (processed))
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** goods.json:83-88. Its only source is toolmaking (bronze 1 + timber 1 → 2 tools; requires artisan_share > 0.05; goods.json:158-176). Consumers: the farm tool factor and wear (ProductionSystem.cs:218-230,267-278) and the workshop project (goods.json:208-211).
- **Current baseline status:** Research-free today, but tools are bronze-only (no stone, bone or copper tool path), so none exist until Artisans make bronze. 0 at founding (WorldFounding.cs:151-163); first tools at t6 seed 42.
- **Related research node(s):** `copper_native`, `copper_smelting`, `tin_bronze`, `knapping_oldowan`, `ground_stone_early`, `hafting`, `sickle`, `digging_stick_hoe`
- **Realization requirements:** Artisans active, bronze in stock, timber, and crafting labour 0.2 per execution. Farmers equip from the previous turn's tool stock (one tool set per farmer, sim.json:193).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Ownership follows the good.bronze decision. Content note: the graph's stone and copper tool nodes (knapping_oldowan 'cutting edges' `research.json` `knapping_oldowan` `.unlocks.capabilities`, ground_stone_early, copper_native 'small copper tools' `research.json` `copper_native` `.unlocks.capabilities`, sickle `research.json` `sickle` `.unlocks.capabilities`) have no counterpart in the simulation. No change is required for founding.

#### `good.pottery`

- **Identifier:** `good.pottery` (good (processed))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:89-94. Source: pottery-firing (goods.json:103-120, no requires). Consumer: the Comfort basket (needs.json:88-92 and the class equivalents).
- **Current baseline status:** Baseline, made from turn 1 with no research and no kiln (2,838 pottery sourced over turns 1–3 seed 42). It is a founding Comfort good.
- **Related research node(s):** `pottery_open_fired`, `kiln_updraft`, `potters_wheel_fast`, `basketry`
- **Related matrix items:** `building.kiln`
- **Realization requirements:** Clay 2 + timber 0.5 + 0.05 labour per unit from the crafting pool.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: pottery_open_fired ('boiling', 'storage', `research.json` `pottery_open_fired` `.unlocks.capabilities`; cheapest closure 3 nodes, 4,160 RP) and kiln_updraft/building.kiln (`research.json` `kiln_updraft` `.unlocks.capabilities`, `research.json` `building.kiln` `.requires`) describe a good the founding economy already fires. Record founding pottery as baseline. A research gate on pottery-firing would remove a founding Comfort good.

#### `good.cloth`

- **Identifier:** `good.cloth` (good (processed))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:95-100. Source: weaving (goods.json:121-134, no requires). Consumer: the Comfort basket (needs.json:94-98 and the class equivalents).
- **Current baseline status:** Baseline, woven from turn 1 with no research (2,516 cloth sourced over turns 1–3 seed 42, all consumed).
- **Related research node(s):** `spinning_spindle`, `loom_warp_weighted`, `flax`, `wool`
- **Related matrix items:** `inst.textile_workshop`
- **Realization requirements:** Fiber 3 + 0.1 labour per unit from the crafting pool.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: loom_warp_weighted lists 'cloth' as its capability (`research.json` `loom_warp_weighted` `.unlocks.capabilities`; closure 10 nodes, 7,440 RP), and spinning_spindle lists 'thread at volume' (`research.json` `spinning_spindle` `.unlocks.capabilities`). Record founding cloth as baseline. inst.textile_workshop (`research.json` `inst.textile_workshop` `.requires`) can only be an additive institution.

#### `recipe.pottery_firing`

- **Identifier:** `recipe.pottery_firing` (recipe)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:103-120; executed by ProductionSystem.Craft (ProductionSystem.cs:381-496). It has no requires, so it is always available (:394-395).
- **Current baseline status:** Baseline: available at turn 0 with no gate of any kind.
- **Related research node(s):** `pottery_open_fired`, `kiln_updraft`
- **Related matrix items:** `building.kiln`
- **Realization requirements:** Clay 2, timber 0.5 and labour 0.05 per execution; crafting labour is shared equally with the other available recipes (ProductionSystem.cs:403).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content issues are the same as good.pottery.

#### `recipe.weaving`

- **Identifier:** `recipe.weaving` (recipe)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** goods.json:121-134; ProductionSystem.Craft (ProductionSystem.cs:381-496). It has no requires (:394-395).
- **Current baseline status:** Baseline: available at turn 0 with no gate.
- **Related research node(s):** `spinning_spindle`, `loom_warp_weighted`
- **Related matrix items:** `inst.textile_workshop`
- **Realization requirements:** Fiber 3 and labour 0.1 per execution from the crafting pool.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content issues are the same as good.cloth.

#### `recipe.bronze_casting`

- **Identifier:** `recipe.bronze_casting` (recipe)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** goods.json:135-157 (requires 'artisan_share > 0.05' at :156); the gate is evaluated on the previous turn's published variables (ProductionSystem.cs:105-108,390-397).
- **Current baseline status:** Knowledge is baseline today (no research gate). It is gated by personnel and cannot execute at founding because artisan_share = 0. The graph claims the knowledge (copper_smelting, tin_bronze).
- **Related research node(s):** `copper_smelting`, `tin_bronze`, `charcoal`
- **Related matrix items:** `building.smithy`
- **Realization requirements:** Artisans emerged (food_surplus_ratio > 1.3 && population > 520, sim.json:169) with artisan_share > 0.05; copper-ore 8, tin-ore 1, timber 2 and labour 0.4 per execution. Measured seed 42: Artisans in 2 settlements at t4.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** The same Director decision as good.bronze. Option (b) needs a research-aware predicate path for recipes (code, not data). building.smithy (requires copper_smelting OR iron_bloomery, `research.json` `building.smithy` `.requires`) has no counterpart in the simulation, because casting needs no structure.

#### `recipe.toolmaking`

- **Identifier:** `recipe.toolmaking` (recipe)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** goods.json:158-176 (requires 'artisan_share > 0.05' at :175); ProductionSystem.Craft.
- **Current baseline status:** Knowledge is baseline today. It is gated by personnel and cannot execute at founding (no artisans, no bronze).
- **Related research node(s):** `copper_smelting`, `tin_bronze`, `copper_native`
- **Realization requirements:** Artisans with artisan_share > 0.05; bronze 1 + timber 1 → 2 tools; labour 0.2. Measured seed 42: first tools at t6.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Follows the good.bronze decision. Nothing else.

#### `stock.dwellings`

- **Identifier:** `stock.dwellings` (conserved capital stock (HousingRow.Dwellings))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** HousingSystem (Sim.Core/Systems/Housing/HousingSystem.cs:105-192; upkeep :107-141; build :145-185). Founding endowment WorldFounding.cs:166-189. Row at WorldState.cs:463-496.
- **Current baseline status:** Baseline object at founding: round(population/6) per settlement as InitialEndowment (857 seed 42), with no materials deducted. Built and maintained automatically with no research and no order (1,023 at t1).
- **Related research node(s):** `shelter_hut`, `timber_frame`, `mudbrick`, `hide_working`, `sewing`, `cordage`
- **Related matrix items:** `building.city_wall`
- **Realization requirements:** None for existence: the founding stock is endowed via InitialEndowment with no materials sunk (WorldFounding.cs:166-189). Conditions for growth and upkeep (research-free, automatic): Build target = population/6 × 1.2. Each dwelling costs 2 timber and 0.5 adult-years from the construction pool (sim.json:211-215; HousingSystem.cs:155-183). Upkeep is 0.05 timber per dwelling per year; unmet upkeep decays dwellings with τ = 40 yr (sim.json:208-209; HousingSystem.cs:107-141). Clay cost is 0.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** both
- **Required code/data changes:** None to the simulation. Content: shelter_hut 'multi-season occupation' (`research.json` `shelter_hut` `.unlocks.capabilities`), timber_frame 'multi-family dwellings' (`research.json` `timber_frame` `.unlocks.capabilities`), mudbrick 'permanent multi-room house' (`research.json` `mudbrick` `.unlocks.capabilities`), hide_working 'shelter covering' (`research.json` `hide_working` `.unlocks.capabilities`) and sewing 'tents' (`research.json` `sewing` `.unlocks.capabilities`) all describe founding housing, which is modelled as timber frame plus earthen walls (sim.json:207). Record founding dwellings as baseline and never infer node completion from them. mudbrick alone makes building.city_wall eligible (`research.json` `building.city_wall` `.requires`), so crediting founding housing as mudbrick knowledge would make walls eligible at turn 0.

#### `param.granary_capacity`

- **Identifier:** `param.granary_capacity` (storage parameter (consumption.granaryYearsOfDemand + grainSpoilagePerYear))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ConsumptionSystem.cs:294-325 (spoilage :294-307; capacity ceiling :309-325); SimConfig.cs:131-141; sim.json:43-45.
- **Current baseline status:** Baseline, applied unconditionally to every settlement from turn 1. It is NOT a structure and reads no Structures row. Its documented carrier is 'mud-brick or pit granary ... more households means more granaries' (sim.json:43).
- **Related research node(s):** `mudbrick`, `pottery_open_fired`, `basketry`, `grinding_stone`, `cereal_domesticated`, `stamp_seal`, `root_crop`, `refrigeration`, `canning`
- **Related matrix items:** `building.granary`
- **Realization requirements:** None. Ceiling = 1.5 × annual grain demand; spoilage = 1 − e^(−0.08·dt) of grain held. Grain only.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for baseline. Making building.granary raise storage would require Consumption to read Structures, which is a separate, unruled design change (docs/adr/adr-028-viability-and-saturation.md:60). Content: the storage capabilities listed on mudbrick 'storehouse' (`research.json` `mudbrick` `.unlocks.capabilities`), pottery_open_fired 'storage' (`research.json` `pottery_open_fired` `.unlocks.capabilities`), basketry 'storage' (`research.json` `basketry` `.unlocks.capabilities`), grinding_stone 'Storage pit and cache' (`research.json` `grinding_stone` `.unlocks.techniques`), cereal_domesticated 'storable surplus' (`research.json` `cereal_domesticated` `.unlocks.capabilities`) and stamp_seal 'sealing stores' (`research.json` `stamp_seal` `.unlocks.capabilities`) describe this founding fact. root_crop's 'no granary needed' (`research.json` `root_crop` `.unlocks.capabilities`) contradicts it. refrigeration (`research.json` `refrigeration` `.unlocks.capabilities`) and canning (`research.json` `canning` `.unlocks.capabilities`) claim near-zero spoilage. They are non-founding. If ever consumed, they act as a coefficient on grainSpoilagePerYear inside the spoilage sink (ConsumptionSystem.cs:298), realized through actual refrigeration or canning capacity (M5 integration). They never gate storage. Nothing changes at founding.

#### `transport.river_corridor`

- **Identifier:** `transport.river_corridor` (terrain transport property (riverCostFactor))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** A coefficient inside the TraversalLattice per-block cost: nodeCost = (1 − span) × blockMean + span × 0.2 (Sim.Core/Pathing/TraversalLattice.cs:26-47; sim.json:21-22). Used by Catchment (CatchmentSystem.cs:94) and PathBuild (PathBuildSystem.cs:159).
- **Current baseline status:** Baseline from turn 0: moving along a river costs 0.2 of ideal ground. The value is derived from river-versus-road FREIGHT ratios, i.e. boat economics. River cells are land, and vessels and ports are out of scope (sim.json:21).
- **Related research node(s):** `raft`, `dugout`, `sail_square`, `bridge_stone`
- **Related matrix items:** `infra.bridge`, `activity.coastal_shipping`
- **Realization requirements:** None. It is not persisted state and no object is built.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: raft 'river crossing' (`research.json` `raft` `.unlocks.capabilities`), dugout 'river and lake transport' (`research.json` `dugout` `.unlocks.capabilities`) and sail_square 'river trade at scale' (`research.json` `sail_square` `.unlocks.capabilities`) describe what the founding corridor already provides. D-040 B3 forbids any node opening a movement mode (docs/d040-discovery-and-control.md:59-64), so record them as non-gating.

#### `transport.overland_unimproved`

- **Identifier:** `transport.overland_unimproved` (terrain transport property (lattice node cost = block-mean terrain MovementCost; water-majority blocks impassable))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** TraversalLattice.cs:106-140 (:131, :137); worldgen.json:32-36; built in CatchmentSystem.cs:94, PathBuildSystem.cs:159 and ColonizationSystem.cs:156-157 (SettlementSiting); trade and migration read the published SettlementDistances.
- **Current baseline status:** Baseline from turn 0, research-free, not persisted; transport.river_corridor and infra.dirt_path overlay it.
- **Related research node(s):** `cart`, `wheel_solid`, `animal_traction`, `horse_domestication`
- **Realization requirements:** None.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: cart, horse_domestication, animal_traction and wheel_solid make overland-transport claims (cart `research.json` `cart` `.unlocks.capabilities`, horse_domestication `research.json` `horse_domestication` `.unlocks.capabilities`, animal_traction `research.json` `animal_traction` `.unlocks.capabilities`, wheel_solid `research.json` `wheel_solid` `.unlocks.capabilities`). These describe carriers over a mode the founding world already has; record them as non-gating (D-040 B3 d040:59-64; D-044 T3 d044:841). Any carrier or draught-animal effect belongs to the OPEN D-040 B4 transport conversation (d040:75-80) and is BLOCKED on a Director ruling, not planned work. No transport.sea row is added: classifying sea travel B would contradict D-040 B3.

#### `catchment.hinterland`

- **Identifier:** `catchment.hinterland` (derived per-settlement territory (CatchmentNode, CatchmentSummary, SettlementDistance))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** CatchmentSystem (Sim.Core/Systems/Catchment/CatchmentSystem.cs:88-171; stale gate :176-189; size bonus :71-72); sim.json:10-13.
- **Current baseline status:** Baseline and research-free. EMPTY at turn 0 (a measurement probe at 6baafbe (not committed): 'CatchNodes 0 CatchSum 0') and computed on the first step. Recomputed whenever the network revision changes.
- **Related research node(s):** none
- **Realization requirements:** Terrain, settlements and the network revision. Budget = 50 km ideal-ground radius × a size bonus of up to 10%.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for research. Note: because it is empty at turn 0, the turn-1 harvest is 0.

#### `settlement.founding_set`

- **Identifier:** `settlement.founding_set` (settlement objects (12 worldgen SettlementRows))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** WorldFounding.cs:34-40 via SettlementSiting.ChooseSites; worldgen.json:46.
- **Current baseline status:** Baseline objects at turn 0 (FoundedTurn 0), sited by fertility × water access, all controlled by the player polity.
- **Related research node(s):** `well`
- **Related matrix items:** `infra.well`
- **Realization requirements:** None in play (worldgen).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** none

#### `settlement.colony`

- **Identifier:** `settlement.colony` (settlement object created in play)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** ColonizationSystem.Found (ColonizationSystem.cs:225-305): SettlementRow :244, inherited control :255-258, 14 zero stock rows :261-262, deposits :264-266, class and grievance rows :268-277, people transfer :284-297, provisions :302.
- **Current baseline status:** Research-free capability from turn 1, but no colony exists at founding. No colonization occurred in 40 turns seed 42.
- **Related research node(s):** `well`
- **Related matrix items:** `infra.well`
- **Realization requirements:** UnplacedDeparture > 0 (source in deficit with no reachable viable destination, MigrationSystem.cs:477-499); grain provisions > 0; an available frontier site. Colonists arrive with no housing or sector rows (homeless; ColonizationSystem.cs:102-105).
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

#### `class.peasants`

- **Identifier:** `class.peasants` (social class (registry class 1))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** sim.json:162-165; founding population all in class index 0 (WorldFounding.cs:92-95); latch Active = 1 (:119-123).
- **Current baseline status:** Baseline object at founding: all 5,143 founders (seed 42) are Peasants.
- **Related research node(s):** none
- **Realization requirements:** None (the always-on base class).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** none

#### `class.artisans`

- **Identifier:** `class.artisans` (social class (registry class 2))
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Emerge and recede predicates at sim.json:169-170; latch at ClassMobilitySystem.cs:168-188; adult mobility for this class only (:190-196); target share min(0.2, 0.2 × (surplus − 1)) (sim.json:186-187).
- **Current baseline status:** Research-free and emergent ('Classes emerge, they are not unlocked', quoted in docs/d040-discovery-and-control.md:59-64). Dormant at founding: the latch row exists with Active 0 and zero members. Measured seed 42: active in 2 settlements at t4.
- **Related research node(s):** none
- **Related matrix items:** `inst.guild`
- **Realization requirements:** food_surplus_ratio > 1.3 && population > 520 on the previous turn's variables; recedes below 1.1. Adults move from Peasants up to the target share; famine demotes first.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None. Research must not gate class emergence. inst.guild (textile_workshop AND legal_code_roman, `research.json` `inst.guild` `.requires`) can only be additive.

#### `class.merchants`

- **Identifier:** `class.merchants` (social class (registry class 3))
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Emerge and recede predicates at sim.json:176-177; latch at ClassMobilitySystem.cs:168-188. No code moves adults into this class: mobility runs only for c == 1 (ClassMobilitySystem.cs:190-196).
- **Current baseline status:** Research-free. The latch can switch on, but the class always holds 0 people. Dormant at founding; 0 merchants in 40 turns seed 42.
- **Related research node(s):** none
- **Realization requirements:** trade_volume > 200 && population > 520 to latch on; recedes below 50. There is no mobility path to fill the class.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for research ownership. Simulation gap, not research: the class can never gain members.

#### `population.buckets`

- **Identifier:** `population.buckets` (conserved stock (BucketRow: culture × religion × class × 16 cohorts))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** WorldFounding.cs:75-115 (full cross product; endowed via Ledger InitialEndowment); Demographics, Migration, ClassMobility and Colonization move people afterwards.
- **Current baseline status:** Baseline object: 576 rows and 5,143 people at turn 0, seed 42.
- **Related research node(s):** none
- **Realization requirements:** None (founding endowment).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** none

#### `population.vital_rates`

- **Identifier:** `population.vital_rates` (mechanism (the Demographics kernel))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** DemographicsSystem.cs:247-253, 270, 309, 321-369, 391, 479 (fertility, base mortality, starvation, cohort aging, famine fertility suppression with the ReboundReservoir, and the headroom growth cap); starvation and suppression read FoodState.EffectiveDeficit with the adaptation dead zone a = 0.20 (FoodState.cs:6-11, 23-60). Pipeline: pipeline.json:17. Rates: sim.json:47-91, 229-231.
- **Current baseline status:** Baseline and research-free; runs every turn from turn 1. The founding schedule is pre-modern (infant mortality 0.056/yr) and implies no medicine node is complete.
- **Related research node(s):** `frontier_medicine`, `antibiotic_penicillin`, `vaccine_lab`, `oral_rehydration`, `hormonal_contraception`, `green_revolution`
- **Realization requirements:** None.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation for founding. M5: any medicine or fertility effect enters as a coefficient inside these rate equations (law 2), realized through computed conditions; it never replaces the founding schedule. frontier_medicine's per-level mechanic (`research.json` `frontier_medicine` `.repeatable.effect`) is BLOCKED on the Director's D-044 Part D T6 ruling (d044:844) and the deferred repeat mechanics (D-046 G4), not planned work. Related matrix item: building.hospital.

#### `registry.culture_religion`

- **Identifier:** `registry.culture_religion` (identity registries (1 culture 'Riverfolk', 1 religion 'Old Rites'))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** sim.json:149-160; instantiated into buckets at WorldFounding.cs:79-82.
- **Current baseline status:** Baseline object at founding. The Belonging/Faith need is unbound (needs.json:31-36), so religion has no institutional mechanics.
- **Related research node(s):** none
- **Related matrix items:** `inst.burial`
- **Realization requirements:** None.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** None. inst.burial has a null requirement (`research.json` `inst.burial` `.requires`), which is consistent, but no system simulates it.

#### `polity.empire_control_capital`

- **Identifier:** `polity.empire_control_capital` (political objects (PolityRow, ControlRow, CapitalRow))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** WorldFounding.FoundInitialEmpire (WorldFounding.cs:277-311); rows at WorldState.cs:608, 704, 724.
- **Current baseline status:** Baseline objects at turn 0: 1 player polity (PolityId 1), 12 control rows at strength 1.0, and capital S0 (aiEmpires defaults to 0, WorldgenConfig.cs:55).
- **Related research node(s):** `stamp_seal`
- **Realization requirements:** None (founding).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** None. stamp_seal 'administrative control' (`research.json` `stamp_seal` `.unlocks.capabilities`) can only be additive to founding control.

#### `deposits`

- **Identifier:** `deposits` (per-settlement resource abundance (DepositRow, double, not conserved))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** WorldFounding.AddDepositsForSite (WorldFounding.cs:220-221,415-437), also called for colonies (ColonizationSystem.cs:264-266); depositChannel in goods.json:12-76.
- **Current baseline status:** Baseline objects at founding: 9 deposit goods × 12 settlements = 108 rows (seed 42). Deposits scale extraction rates and are not stocks.
- **Related research node(s):** `mining_shaft`
- **Related matrix items:** `activity.mining`
- **Realization requirements:** None. Derived from terrain channel hinterland means × seeded spread.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** object
- **Required code/data changes:** None for research. Note: goods without a depositSpread share one abundance per channel, so per settlement livestock, timber, fiber and hides are identical, and so are fish and clay (WorldFounding.cs:425-433; a measurement probe at 6baafbe (not committed)).

#### `trade.arbitrage`

- **Identifier:** `trade.arbitrage` (economic capability (TradeArbitrageSystem))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** TradeArbitrageSystem.cs:12-24 (pairwise arbitrage on the existing network), :113-114 (needs ≥ 2 settlements), :126-127 (finite route), :137 (grain never moves); sim.json:217-220.
- **Current baseline status:** Baseline and research-free from turn 1. No carrier, caravan or boat is required. TradeScope classifies trade but gates nothing (TradeScope.cs:45-49). First realized flow at t28 seed 42.
- **Related research node(s):** `donkey`, `camel`, `standard_weights`, `sail_square`
- **Related matrix items:** `activity.caravans`
- **Realization requirements:** A price gap above bulk × path cost × 0.16 between connected settlements, with stock on hand.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: activity.caravans (requires donkey OR camel, `research.json` `activity.caravans` `.requires`; donkey 'caravan trade' `research.json` `donkey` `.unlocks.capabilities`) may only add a carrier tier. It must never gate TradeArbitrageSystem.

#### `market.prices`

- **Identifier:** `market.prices` (economic capability (PriceSystem))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** PriceSystem.cs:104-116 (unseeded price defaults to 1.0; grain pinned at 1.0), :131-168 (damped step); goods.json:3 (grain is the numeraire).
- **Current baseline status:** Baseline, research-free from turn 1. Prices are consumed only by Trade.
- **Related research node(s):** `standard_weights`, `coinage_electrum`
- **Related matrix items:** `building.mint`
- **Realization requirements:** None beyond the goods stocks and flows.
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None. building.mint (requires coinage_electrum, `research.json` `building.mint` `.requires`) can only be additive to grain-numeraire pricing.

#### `coercion.appropriation`

- **Identifier:** `coercion.appropriation` (coercive capability (AppropriationSystem — stateless raiding))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** AppropriationSystem.cs:10-29 (D-037 B3 design), :130-160 (raider must be stateless, herding-dominant and in deficit; takes grain from the richest other settlement by Ledger.Transfer).
- **Current baseline status:** Research-free, with no units. Unreachable at founding because every settlement is controlled; it needs Revolt or a stateless colony first.
- **Related research node(s):** `sling`, `cavalry`
- **Realization requirements:** A stateless raider whose herding dominates, with a consumption deficit > 0, and some other settlement holding grain.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Simulation: none. Content: cavalry's capability "steppe raiding" (`research.json` `cavalry` `.unlocks.capabilities`; cavalry A4, depth 4, 1,240 RP; cheapest closure 5 nodes, 4,500 RP) names the mechanism AppropriationSystem already runs with no research (D-037 B3, d037:54-60; AppropriationSystem.cs:10-29, 132-164). Record it as non-owning and non-gating (research must never gate D-037 B3 raiding), or reword the string. Any additive mounted-warfare effect is the military packet's design, and any movement or range term is subject to D-044 T3 (d044:841). Related to baseline.basic_military: when military ships, it must not double-count this unit-less coercion.

#### `control.revolt`

- **Identifier:** `control.revolt` (political capability (RevoltSystem))
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** RevoltSystem.cs:12-32 (rule), :70-98 (removes the control row of any settlement at zero happiness).
- **Current baseline status:** Baseline and research-free from turn 1. Creates no objects.
- **Related research node(s):** none
- **Realization requirements:** Derived happiness of exactly 0 (SettlementHappiness.IsRevoltReady).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** none

### 7.4 Buildings (registry)

#### `building.granary`

- **Identifier:** `building.granary` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** One object, two identifiers: research.json entity building.granary (`research.json` `building.granary`) = goods.json projects[0] id 1 "granary" (goods.json:179-194); the separate project row was merged here. ConstructionSystem project id 1 'granary' (Sim.Data/content/goods.json:179-194; Sim.Core/Systems/Construction/ConstructionSystem.cs:76-117, completion :179-189). The granary FUNCTION is separate and does not depend on the building: ConsumptionSystem's grain storage ceiling granaryYearsOfDemand 1.5 (Sim.Core/Systems/Consumption/ConsumptionSystem.cs:309-325; Sim.Data/content/sim.json:45; Sim.Core/Systems/SimConfig.cs:137-141).
- **Current baseline status:** BASELINE-ELIGIBLE. requires null (`research.json` `building.granary` `.requires`), so it is always knowledge-eligible, including at turn 0 (Sim.Core/State/ResearchQuery.cs:526-527,541-545; test Sim.Tests/Systems/ResearchContentTests.cs:246-250 (moved)). ConstructionSystem queues project 1 with no tech gate (ConstructionSystem.cs:92-95; ADR-028 §4 adr-028:57-58 'baseline constructibles'). No granary structure exists at founding. The storage ceiling applies to every settlement from turn 1 with no granary built. relatedNodes are NOT requirements: they are nodes whose capability strings claim storage that the founding sim already has.
- **Related research node(s):** `mudbrick`, `basketry`, `pottery_open_fired`, `cereal_domesticated`, `grinding_stone`, `stamp_seal`
- **Realization requirements:** An EnqueueConstruction order (OrderKind 4) from the Empire that controls the settlement (ConstructionSystem.cs:77-97). Only the queue head is eligible, and at most one completion per settlement per turn (:100-117). 40 timber and 20 stone must be present in full (goods.json:183-192; check at ConstructionSystem.cs:148-160). Construction capacity (construction share x adults x dt, minus housing LastLaborUsed) must be >= 2.0 adult-years (:125-146). Materials are sunk via Ledger.Flow ConstructionMaterials (:162-177), and the StructureRow count goes up by 1 (:179-189). It is a gate, not a rate: no progress accumulates (:38-48; WorldState.cs:737-741). At turn 0 timber and stone are 0, because founding endows grain only (Sim.Core/Worldgen/WorldFounding.cs:146-164).
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None for baseline status: keep requires null, since ADR-028 §3 forbids inventing a prerequisite (adr-028:52-54). Gaps if the building is to matter: (1) no code links 'building.granary' to project id 1, because ConstructionProjectEntry has no entity field (Sim.Core/Systems/GoodsConfig.cs:81-85) and only a test names the entity; (2) the structure is inert, because no system reads Structures and storage is the config constant (ConsumptionSystem.cs:309-325; adr-028:60). Tying capacity to granary count is a ConsumptionSystem mechanism change, and it must preserve founding storage because founding settlements hold zero granaries; this is a design decision, not a data edit; (3) Sim.Ui and Sim.Cli have no EnqueueConstruction issuer; the order is only loadable from an order log (Sim.Core/Kernel/OrderLog.cs:49,220-249). Wording note: D-045 §1 says 'construction progress' (docs/d045-research-calibration-rulings.md:49-53), but ConstructionSystem has no progress field. ADR-029 §2.6 accepted ConstructionSystem as the baseline provider (docs/adr/adr-029-research-engine.md:143-160).

#### `building.workshop`

- **Identifier:** `building.workshop` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** One object, two identifiers: research.json entity building.workshop (`research.json` `building.workshop`) = goods.json projects[1] id 2 "workshop" (goods.json:195-214); the separate project row was merged here. ConstructionSystem project id 2 'workshop' (Sim.Data/content/goods.json:195-214; Sim.Core/Systems/Construction/ConstructionSystem.cs:76-117,179-189). No system reads the completed structure, and crafting runs without it (Sim.Core/Systems/Production/ProductionSystem.cs:381-496).
- **Current baseline status:** BASELINE-ELIGIBLE. requires null (`research.json` `building.workshop` `.requires`), so it is knowledge-eligible at turn 0 (ResearchQuery.cs:526-527,541-545). It is constructible with no tech gate (adr-028:58). It cannot be realized at founding: it needs 10 tools, none are endowed (WorldFounding.cs:146-164), and tools come only from the artisan-gated bronze-to-tools chain. That chain is gated by class, not research. relatedNodes are NOT requirements: they are the graph nodes that claim the tool and bronze chain.
- **Related research node(s):** `copper_smelting`, `tin_bronze`
- **Realization requirements:** EnqueueConstruction from the controlling Empire, at the queue head (ConstructionSystem.cs:77-117). 60 timber, 40 stone and 10 tools in full (goods.json:199-212). Capacity >= 8.0 adult-years (:125-146). The tools chain: toolmaking (bronze 1 + timber 1 -> 2 tools, goods.json:158-176) and bronze-casting (copper-ore 8 + tin-ore 1 + timber 2, goods.json:135-157), both require 'artisan_share > 0.05' (goods.json:156,175). Artisans emerge when food_surplus_ratio > 1.3 && population > 520 (sim.json:169). Copper-ore and tin-ore come from deposits or trade. Farming also uses tools via the equip ratio (ProductionSystem.cs:218-233), which competes for the stock.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to keep it baseline-eligible. Gaps: (1) no entity->project link (GoodsConfig.cs:81-85); (2) it is inert: no system reads Structures, and crafting (ProductionSystem.cs:381-496) reads none, so it needs an owning effect mechanism (a mechanism, not a buff; law 2); (3) latent contradiction: the graph claims the tool chain (copper_smelting 'cast copper tools' `research.json` `copper_smelting` `.unlocks.capabilities`; tin_bronze 'standardised alloy' `research.json` `tin_bronze` `.unlocks.capabilities`), while the sim gates it only by artisan share. If the bronze or tool recipes are ever research-gated, a null-requirement workshop becomes unbuildable until A2/A3 metallurgy. Director decision, not plain work: if the bronze or tool recipes are ever research-gated, record the workshop's material dependency on research; do not add a node requirement to the null-required entity (adr-028:52-54); (4) no UI/CLI EnqueueConstruction issuer.

#### `building.kiln`

- **Identifier:** `building.kiln` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Overlapping baseline: the pottery-firing recipe (clay 2 + timber 0.5 -> 1 pottery, no requires) runs in the Crafting sector from turn 1 without a kiln (Sim.Data/content/goods.json:103-120; Sim.Core/Systems/Production/ProductionSystem.cs:388-397).
- **Current baseline status:** Not baseline. LOCKED at founding (zero completed nodes). Requires kiln_updraft (`research.json` `building.kiln` `.requires`; node `research.json` `kiln_updraft`, A2 d3, 4,180 RP; prereq pottery_open_fired AND mudbrick). Approximate cheapest closure: 5 nodes / 9,390 BaseCost RP (basketry, cordage, mudbrick, pottery_open_fired, kiln_updraft); not research-stage gated. kiln_updraft 'emerged ~6,000 BCE', which predates the 4000 BCE epoch (Sim.Core/Kernel/SimClock.cs:5; CR-006 OPEN). The fired-pottery capability itself is baseline in the sim.
- **Related research node(s):** `kiln_updraft`
- **Realization requirements:** After kiln_updraft completes: a construction project (materials and labour not yet defined) built through ConstructionSystem by the controlling Empire (queue head, materials in full, capacity >= labour; ConstructionSystem.cs:100-146). To operate: clay (a water-channel deposit good, goods.json:42-48), timber fuel, and Crafting-sector labour (default share 0.12, WorldState.cs:375-376).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC (absent for every building): (a) a goods.json project entry with reference-class-derived inputs and laborRequired (only ids 1-2 exist, goods.json:178-215); (b) an entity->project link (GoodsConfig.cs:81-85 has no such field); (c) an availability predicate at ConstructionSystem enqueue that calls ResearchQuery.IsKnowledgeEligible (today it checks only control and project existence, ConstructionSystem.cs:92-95; adr-028:57); (d) a per-type effect mechanism in the owning system that reads Structures (none does), with no universal CapabilitySystem (adr-028:24-26); (e) a UI/CLI EnqueueConstruction issuer (none). SPECIFIC: define what a kiln adds over baseline open firing ('high-fired ceramics', 'reducing atmosphere', `research.json` `kiln_updraft` `.unlocks.capabilities`) as a mechanism, for example a recipe that needs the kiln as capital, not a percentage buff. Do NOT gate the existing pottery-firing recipe, which feeds the founding Comfort basket (Sim.Data/content/needs.json:87-92). If high-fired ware is to exist, add a new good.

#### `building.smithy`

- **Identifier:** `building.smithy` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Overlapping baseline: bronze-casting (copper-ore 8 + tin-ore 1 + timber 2 -> bronze) and toolmaking (bronze 1 + timber 1 -> 2 tools) run in generic Crafting with no smithy. They are gated only by 'artisan_share > 0.05' (Sim.Data/content/goods.json:135-176; Sim.Core/Systems/Production/ProductionSystem.cs:388-397).
- **Current baseline status:** Not baseline. LOCKED at founding. Requires copper_smelting OR iron_bloomery (`research.json` `building.smithy` `.requires`). copper_smelting: `research.json` `copper_smelting`, A2 d4, 4,180 RP, prereq kiln_updraft AND (copper_native OR ochre_processing), emerged ~5,000 BCE (before the epoch). iron_bloomery: `research.json` `iron_bloomery`, A4 d4, 8,360 RP, prereq charcoal. Approximate cheapest closure: 8 nodes / 14,200 RP via copper_smelting; not stage-gated. Metalworking itself is research-free in the sim (gated by class).
- **Related research node(s):** `copper_smelting`, `iron_bloomery`
- **Realization requirements:** After either node: a construction project (undefined) via ConstructionSystem. To operate: copper-ore and tin-ore (deposit goods, goods.json:49-62), or iron ore (no iron good exists in goods.json:4-101); fuel (timber; charcoal is not a good); and Artisans as personnel (they emerge when food_surplus_ratio > 1.3 && population > 520, sim.json:169).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC (absent for every building): (a) goods.json project entry (only ids 1-2 exist, goods.json:178-215); (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) owning-system effect mechanism that reads Structures (none does); (e) UI/CLI EnqueueConstruction issuer (none). SPECIFIC: decide how the smithy relates to the existing artisan-gated bronze and tool recipes. Retro-gating them on copper_smelting or tin_bronze would take away from a zero-research civ both the farm tool-factor path (ProductionSystem.cs:218-233) and the workshop's tools input (goods.json:208-210); that needs a Director ruling. The iron_bloomery branch also needs iron-ore and iron goods, which do not exist.

#### `building.blast_furnace`

- **Identifier:** `building.blast_furnace` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. No iron, pig-iron, charcoal or coke good exists (Sim.Data/content/goods.json:4-101).
- **Current baseline status:** Not baseline. LOCKED. Requires blast_furnace_water OR cast_iron (`research.json` `building.blast_furnace` `.requires`). blast_furnace_water: `research.json` `blast_furnace_water`, A6 d15, engineering subtree, 7,030 RP, so it is research-stage gated. cast_iron: `research.json` `cast_iron`, A5 d5, 16,730 RP, prereq iron_bloomery AND bellows AND kiln_updraft. Approximate cheapest closure: 18 nodes / 43,490 RP via cast_iron (not stage-gated).
- **Related research node(s):** `blast_furnace_water`, `cast_iron`
- **Realization requirements:** A construction project. Inputs of iron ore, fuel (charcoal or coke) and flux, and a pig-iron or cast-iron output good. The water-powered variant also needs a river-with-head site; rivers, river polylines and elevation exist in Sim.Core/Worldgen/TerrainSet.cs:20-27.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry (goods.json:178-215); (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism (none exists); (e) UI/CLI issuer (none). SPECIFIC: an iron-ore deposit channel, fuel and iron goods, and a smelting recipe (ADR-019 §5.1 lists iron and coal as future PHYSICAL STOCKS, docs/adr/adr-019-architecture-constitution-addendum.md:160); a site predicate for the water-powered variant.

#### `building.water_mill`

- **Identifier:** `building.water_mill` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Grain is consumed unmilled: there is no flour good, and the grain basket is at Sim.Data/content/needs.json:70-74. There is no milling or mechanical-energy mechanism.
- **Current baseline status:** Not baseline. LOCKED. Requires water_mill (`research.json` `building.water_mill` `.requires`; node `research.json` `water_mill`, A5 d6, 11,830 RP, prereq rotary_quern). Approximate cheapest closure: 10 nodes / 20,900 RP; not stage-gated.
- **Related research node(s):** `water_mill`
- **Realization requirements:** A construction project. A genuine site constraint: a river with head, which ADR-028 §1.2 names as an example (adr-028:20). It can be derived from TerrainSet rivers, head-to-mouth river polylines and elevation (Sim.Core/Worldgen/TerrainSet.cs:20-27). A milling or power mechanism must consume its output.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry (goods.json:178-215); (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a riverside-with-head site predicate, plus a milling stage or a mechanical-power capacity resource; neither exists. The node's capabilities read 'milling without human labour' and 'the energy resource type of ADR-019 §5' (`research.json` `water_mill` `.unlocks.capabilities`), and ADR-019 treats capacity as distinct from stock (adr-019:163).

#### `building.windmill`

- **Identifier:** `building.windmill` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no milling mechanism, and TerrainSet has no wind channel (Sim.Core/Worldgen/TerrainSet.cs:20-27).
- **Current baseline status:** Not baseline. LOCKED. Requires windmill_post OR windmill_vertical (`research.json` `building.windmill` `.requires`). windmill_vertical: `research.json` `windmill_vertical`, A6 d7, engineering, 2,090 RP, prereq water_mill. windmill_post: `research.json` `windmill_post`, A6 d7, engineering, 4,970 RP, prereq water_mill AND sail_square. Both are in the engineering subtree, so the research stage is required (`research.json` `researchStage`; ResearchQuery.cs:105-111 (moved)). Approximate cheapest closure including the stage: 27 nodes / 50,610 RP.
- **Related research node(s):** `windmill_post`, `windmill_vertical`
- **Realization requirements:** A construction project, a wind-exposed site (no wind field exists), and a milling mechanism to consume the output.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a milling or power mechanism, and a wind-exposure site term derived from terrain (none exists).

#### `building.fulling_mill`

- **Identifier:** `building.fulling_mill` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Cloth is a single good made by the weaving recipe (fiber 3 -> cloth, no gate, Sim.Data/content/goods.json:121-134). There is no fulling or finishing stage.
- **Current baseline status:** Not baseline. LOCKED. Requires fulling_mill (`research.json` `building.fulling_mill` `.requires`; node `research.json` `fulling_mill`, A6 d8, engineering subtree, 4,970 RP, prereq trip_hammer AND loom_warp_weighted). Research-stage gated. Approximate cheapest closure: 33 nodes / 59,720 RP. Weaving itself is baseline in the sim.
- **Related research node(s):** `fulling_mill`
- **Realization requirements:** A construction project. A river-with-head site for water power (TerrainSet.cs:20-27). Cloth input and a finished-cloth output. Textile labour.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a cloth-finishing stage or good, and a water-power site predicate. Baseline weaving must not be gated: cloth is in the founding Comfort basket (needs.json:93-98).

#### `building.paper_mill`

- **Identifier:** `building.paper_mill` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no paper good (Sim.Data/content/goods.json:4-101) and no records or administration mechanism.
- **Current baseline status:** Not baseline. LOCKED. Requires paper AND water_mill (`research.json` `building.paper_mill` `.requires`). paper: `research.json` `paper`, A5 d1, 11,830 RP, prereq cordage. water_mill: `research.json` `water_mill`, A5 d6, 11,830 RP. Approximate cheapest closure: 12 nodes / 33,780 RP; not stage-gated.
- **Related research node(s):** `paper`, `water_mill`
- **Realization requirements:** A construction project. A river-with-head site for water power (TerrainSet.cs:20-27). Fiber input (fiber exists, goods.json:63-69) and a paper output good. A consumer of paper; none exists.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a paper good, a recipe and a site predicate. Optionally, a declared dependency on a building.water_mill being present (ADR-028 §1.3, adr-028:22); today the requirement names only knowledge nodes.

#### `building.oil_press`

- **Identifier:** `building.oil_press` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no oil good and no orchard or olive crop: the food goods are grain, livestock and fish only (Sim.Data/content/goods.json:4-101).
- **Current baseline status:** Not baseline. LOCKED. Requires oil_press (`research.json` `building.oil_press` `.requires`; node `research.json` `oil_press`, A2 d3, 620 RP, prereq orchard AND stone_dry; emerged ~4,500 BCE, before the 4000 BCE epoch). Approximate cheapest closure: 7 nodes / 6,870 RP; not stage-gated.
- **Related research node(s):** `oil_press`
- **Realization requirements:** A construction project. Olive or oilseed input from an orchard production path, an oil output good, and a consumer (food basket or lamp fuel). None of these exist.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: an orchard crop, an oil good, a pressing recipe, and a basket or consumer line.

#### `building.textile_mill`

- **Identifier:** `building.textile_mill` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Founding weaving is craft-scale (Sim.Data/content/goods.json:121-134). There is no water or steam power.
- **Current baseline status:** Not baseline. LOCKED. Requires water_frame OR power_loom (`research.json` `building.textile_mill` `.requires`). water_frame: `research.json` `water_frame`, A8 d17, engineering, 8,360 RP, prereq spinning_jenny AND water_mill. power_loom: `research.json` `power_loom`, A8 d20, engineering, 14,070 RP, prereq spinning_mule AND steam_rotary. Research-stage gated. Approximate cheapest closure: 45 nodes / 118,060 RP.
- **Related research node(s):** `water_frame`, `power_loom`
- **Realization requirements:** A construction project. A river-with-head site (water_frame) or steam power with coal. Fiber input and cloth output. Factory labour.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a power resource modelled as capacity (adr-019:163), coal, and an industrial textile recipe. Leave baseline weaving untouched.

#### `building.steelworks`

- **Identifier:** `building.steelworks` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There are no iron, coal or steel goods (Sim.Data/content/goods.json:4-101).
- **Current baseline status:** Not baseline. LOCKED. Requires bessemer OR open_hearth (`research.json` `building.steelworks` `.requires`). bessemer: `research.json` `bessemer`, A8 d21, engineering, 47,310 RP, prereq puddling. open_hearth: `research.json` `open_hearth`, A8 d22, engineering, 23,650 RP, prereq bessemer AND hot_blast. Research-stage gated. Approximate cheapest closure: 68 nodes / 479,810 RP.
- **Related research node(s):** `bessemer`, `open_hearth`
- **Realization requirements:** A construction project. Pig iron and coal or coke inputs, a steel output, and a large labour pool.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: iron, coal and steel goods (ADR-019 §5.1 names them as future physical stocks, adr-019:160) and a steelmaking recipe.

#### `building.chemical_works`

- **Identifier:** `building.chemical_works` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There are no chemical goods (Sim.Data/content/goods.json:4-101).
- **Current baseline status:** Not baseline. LOCKED. Requires sulphuric_acid (`research.json` `building.chemical_works` `.requires`; node `research.json` `sulphuric_acid`, A8 d18, natural_science subtree, 33,450 RP, prereq distillation AND glass_lead). Research-stage gated. Approximate cheapest closure: 54 nodes / 184,720 RP.
- **Related research node(s):** `sulphuric_acid`
- **Realization requirements:** A construction project. Sulphur or pyrite input, an acid output, and industrial consumers.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: chemical goods, recipes and consumers.

#### `building.cement_works`

- **Identifier:** `building.cement_works` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no lime or cement good. Stone is consumed only by the construction projects (Sim.Data/content/goods.json:188-190,204-206).
- **Current baseline status:** Not baseline. LOCKED. Requires portland_cement (`research.json` `building.cement_works` `.requires`; node `research.json` `portland_cement`, A8 d15, engineering, 33,450 RP, prereq concrete_pozzolan AND kiln_updraft AND chemistry_quantitative). Research-stage gated. Approximate cheapest closure: 42 nodes / 199,850 RP.
- **Related research node(s):** `portland_cement`
- **Realization requirements:** A construction project. Limestone and clay inputs plus fuel. A cement output consumed by construction projects.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a cement good and recipe, and project inputs that use it.

#### `building.refinery`

- **Identifier:** `building.refinery` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no petroleum deposit or good; the nine deposit goods are listed at Sim.Data/content/goods.json:12-76.
- **Current baseline status:** Not baseline. LOCKED. Requires petroleum_refining (`research.json` `building.refinery` `.requires`; node `research.json` `petroleum_refining`, A8 d10, natural_science, 33,450 RP, prereq distillation AND mining_shaft). Research-stage gated. Approximate cheapest closure: 40 nodes / 111,010 RP.
- **Related research node(s):** `petroleum_refining`
- **Realization requirements:** A construction project. A crude-oil deposit (a genuine site/resource constraint) and its extraction. Fuel outputs and consumers for them.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a petroleum deposit channel and good, a refining recipe, and fuel consumers.

#### `building.power_station`

- **Identifier:** `building.power_station` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no electricity. ADR-019 §5.1 defines electricity as generation CAPACITY, grid connectivity and demand, not a stockpile (docs/adr/adr-019-architecture-constitution-addendum.md:163-164); none of this is modelled.
- **Current baseline status:** Not baseline. LOCKED. Requires electricity_generation (`research.json` `building.power_station` `.requires`; node `research.json` `electricity_generation`, A8 d22, engineering, 47,310 RP, prereq dynamo AND electric_light AND electromagnetism). Research-stage gated. Approximate cheapest closure: 74 nodes / 575,340 RP.
- **Related research node(s):** `electricity_generation`
- **Realization requirements:** A construction project, a fuel input (coal), an electricity capacity and grid system (see infra.power_grid, `research.json` `infra.power_grid` `.requires`), and demand for the power.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: an electricity capacity system that is NOT a GoodStock (adr-019:163), and fuel goods.

#### `building.hydroelectric_dam`

- **Identifier:** `building.hydroelectric_dam` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no electricity capacity system (adr-019:163).
- **Current baseline status:** Not baseline. LOCKED. Requires hydroelectric (`research.json` `building.hydroelectric_dam` `.requires`; node `research.json` `hydroelectric`, A9 d23, engineering, 23,650 RP, prereq electricity_generation AND reinforced_concrete AND water_mill). Research-stage gated. Approximate cheapest closure: 86 nodes / 819,620 RP.
- **Related research node(s):** `hydroelectric`
- **Realization requirements:** A very large construction project. A genuine site: a river with head (adr-028:20), derivable from TerrainSet rivers and elevation (Sim.Core/Worldgen/TerrainSet.cs:20-27). An electricity capacity system and a grid.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a capacity system and a river-with-head site predicate.

#### `building.nuclear_power_station`

- **Identifier:** `building.nuclear_power_station` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no uranium good and no electricity system.
- **Current baseline status:** Not baseline. LOCKED. Requires nuclear_power (`research.json` `building.nuclear_power_station` `.requires`; node `research.json` `nuclear_power`, A9 d24, engineering, 94,620 RP, prereq nuclear_fission AND steam_turbine). Research-stage gated. Approximate cheapest closure: 84 nodes / 1,305,330 RP.
- **Related research node(s):** `nuclear_power`
- **Realization requirements:** A construction project, uranium fuel, a site with cooling water, and an electricity capacity system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a fuel good and a capacity system (adr-019:163).

#### `building.school`

- **Identifier:** `building.school` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no education or literacy state and no institutions system: ResearchCostModifiers is an input contract with no writer (Sim.Core/State/WorldState.cs:861-875). The only classes are Peasants, Artisans and Merchants (Sim.Data/content/sim.json:161-179).
- **Current baseline status:** Not baseline. LOCKED. Requires cuneiform OR hieroglyphic OR chinese_script OR abjad OR brahmi (`research.json` `building.school` `.requires`). cuneiform: `research.json` `cuneiform`, A3 d6. hieroglyphic: `research.json` `hieroglyphic`, A3 d5. chinese_script: `research.json` `chinese_script`, A3 d4. Each costs 3,520 RP. abjad: `research.json` `abjad`, A4 d7. brahmi: `research.json` `brahmi`, A4 d8. Approximate cheapest closure: 5 nodes / 4,930 RP via chinese_script (tally_notation, bone_tools, knapping_blade, knapping_levallois); not stage-gated.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`, `abjad`, `brahmi`
- **Realization requirements:** A construction project, teachers or scribes as personnel (no such class exists), and an education or literacy mechanism to consume it.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a literacy or education mechanism and its personnel. Reconcile with inst.scribal_school, which uses the same script disjunction (`research.json` `inst.scribal_school` `.requires`).

#### `building.library`

- **Identifier:** `building.library` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no records or knowledge-storage mechanism, and nothing writes the research cost seam (Sim.Core/State/WorldState.cs:861-875).
- **Current baseline status:** Not baseline. LOCKED. Requires cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper (`research.json` `building.library` `.requires`). Approximate cheapest closure: 3 nodes / 2,550 RP via papyrus (`research.json` `papyrus`, A3 d2, 880 RP, prereq basketry, which needs cordage). That makes the library knowledge-eligible with NO writing system, a registry oddity. Not stage-gated. ADR-029 §8 documents the research stage as including the library requirement (docs/adr/adr-029-research-engine.md:345-353). The proposed stage expression implies it through medicine_hippocratic -> medicine_recorded -> script.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`, `papyrus`, `paper`
- **Realization requirements:** A construction project. Written material (papyrus or paper; neither is a good). Scribes as personnel. A consuming mechanism. A library is a prerequisite for a university building (adr-028:22; adr-029:347).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: decide whether eligibility through papyrus alone is intended. Reconcile with inst.library (archive AND scribal_school, `research.json` `inst.library` `.requires`). Add a consumer mechanism.

#### `building.university`

- **Identifier:** `building.university` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet, and there is no institutions system. Its knowledge requirement is a conjunct of the research-stage predicate (`research.json` `researchStage`), which opens all five subtrees (Sim.Core/State/ResearchQuery.cs:81-111 (moved)). That needs no university object: 'the institutional half - a university actually established - cannot be evaluated' (docs/adr/adr-029-research-engine.md:358-360).
- **Current baseline status:** Not baseline. LOCKED. Requires medicine_hippocratic AND (geometry_axiomatic OR algebra) (`research.json` `building.university` `.requires`). medicine_hippocratic: `research.json` `medicine_hippocratic`, A4 d8, 1,760 RP. geometry_axiomatic: `research.json` `geometry_axiomatic`, A5 d11, 7,030 RP. algebra: `research.json` `algebra`, A6 d12, 8,360 RP. Approximate cheapest closure: 10 nodes / 18,850 RP; not stage-gated (it is itself part of the stage).
- **Related research node(s):** `medicine_hippocratic`, `geometry_axiomatic`, `algebra`
- **Realization requirements:** A construction project. A library must be present: ADR-028 §1.3 (adr-028:22) and ADR-029 §8 (adr-029:347) require this, but the requirement does not declare it. Scholars as personnel. The institution inst.university (library AND legal_code_roman, `research.json` `inst.university` `.requires`). Its effect would come through ResearchCostModifiers, which has no writer (WorldState.cs:865-873).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: declare the library dependency on the dependent type (adr-028:22). An institutions system to write ResearchCostModifiers; the formula is unratified and a D-021 brake is owed (WorldState.cs:870-873). Optionally, an institution-present atom in researchStage (adr-029:358-360).

#### `building.hospital`

- **Identifier:** `building.hospital` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. The Health need is unbound (Sim.Data/content/needs.json:25-30), and no system reads Structures, so mortality is unaffected.
- **Current baseline status:** Not baseline. LOCKED. Requires medicine_hippocratic AND pharmacology (`research.json` `building.hospital` `.requires`). pharmacology: `research.json` `pharmacology`, A5 d9, 1,760 RP, prereq medicine_hippocratic. Approximate cheapest closure: 8 nodes / 9,330 RP; not stage-gated.
- **Related research node(s):** `medicine_hippocratic`, `pharmacology`
- **Realization requirements:** A construction project. Physicians as personnel (no such class exists). A health mechanism: a bound Health need coupled to mortality in demographics.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: bind Health and add a mortality mechanism. Reconcile with the duplicate inst.hospital, which has the same requirement (`research.json` `inst.hospital` `.requires`).

#### `building.observatory`

- **Identifier:** `building.observatory` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no astronomy or calendar mechanism.
- **Current baseline status:** Not baseline. LOCKED. Requires astronomy_geometric OR astrolabe OR telescope (`research.json` `building.observatory` `.requires`). astronomy_geometric: `research.json` `astronomy_geometric`, A5 d12, 3,520 RP. astrolabe: `research.json` `astrolabe`, A6 d14, natural_science. telescope: `research.json` `telescope`, A7 d14, natural_science. Approximate cheapest closure: 11 nodes / 25,670 RP via astronomy_geometric, which is not stage-gated.
- **Related research node(s):** `astronomy_geometric`, `astrolabe`, `telescope`
- **Realization requirements:** A construction project, astronomers as personnel, and a consuming mechanism (calendar, navigation or research); none exists.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a consumer mechanism.

#### `building.research_laboratory`

- **Identifier:** `building.research_laboratory` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Research capacity is derived from population and has no building input (`research.json` `tuning` tuning.rpPerTurn). ResearchCostModifiers has no writer (Sim.Core/State/WorldState.cs:861-875).
- **Current baseline status:** Not baseline. LOCKED. Requires chemistry_quantitative (`research.json` `building.research_laboratory` `.requires`; node `research.json` `chemistry_quantitative`, A8 d14, natural_science, 56,260 RP). Research-stage gated. Approximate cheapest closure: 37 nodes / 146,870 RP.
- **Related research node(s):** `chemistry_quantitative`
- **Realization requirements:** A construction project, scientists as personnel, and a mechanism that couples it to research, for example through the cost seam (ADR-029 §9).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: an institutions or research mechanism that reads it. The first writer of the research loop owes a D-021 brake (WorldState.cs:872-873).

#### `building.printing_house`

- **Identifier:** `building.printing_house` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There are no paper or book goods and no literacy mechanism.
- **Current baseline status:** Not baseline. LOCKED. Requires printing_press OR woodblock_print (`research.json` `building.printing_house` `.requires`). woodblock_print: `research.json` `woodblock_print`, A6 d4, engineering, 14,070 RP, prereq paper AND stamp_seal. printing_press: `research.json` `printing_press`, A7 d13, engineering, 39,780 RP. Both are in the engineering subtree, so it is research-stage gated. Approximate cheapest closure: 20 nodes / 54,560 RP.
- **Related research node(s):** `printing_press`, `woodblock_print`
- **Realization requirements:** A construction project, paper input (no such good), type metal or woodblocks, and literate demand.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: paper and book goods and a consumer.

#### `building.mint`

- **Identifier:** `building.mint` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no money: grain is the numeraire and prices are grain-equivalent ratios (Sim.Data/content/goods.json:3,7-10). Money is deferred and must be built as an endogenous system, not a stock (docs/adr/adr-019-architecture-constitution-addendum.md:165-171).
- **Current baseline status:** Not baseline. LOCKED. Requires coinage_electrum (`research.json` `building.mint` `.requires`; node `research.json` `coinage_electrum`, A4 d12, 11,830 RP, prereq hacksilver AND stamp_seal). Approximate cheapest closure: 18 nodes / 31,900 RP; not stage-gated.
- **Related research node(s):** `coinage_electrum`
- **Realization requirements:** A construction project, precious-metal input (no gold, silver or electrum good exists), state authority (control rows exist), and a money system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a money system built per ADR-019 §5.1, and precious-metal goods. Reconcile with inst.mint_institution (coinage_electrum AND census, `research.json` `inst.mint_institution` `.requires`).

#### `building.bathhouse`

- **Identifier:** `building.bathhouse` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no water supply or hygiene mechanism, and the Health need is unbound (Sim.Data/content/needs.json:25-30).
- **Current baseline status:** Not baseline. LOCKED. Requires aqueduct_arcade AND vault_dome (`research.json` `building.bathhouse` `.requires`). aqueduct_arcade: `research.json` `aqueduct_arcade`, A5 d13, 4,970 RP. vault_dome: `research.json` `vault_dome`, A5 d12, 3,520 RP. Approximate cheapest closure: 29 nodes / 62,750 RP; not stage-gated.
- **Related research node(s):** `aqueduct_arcade`, `vault_dome`
- **Realization requirements:** A construction project. A water supply through aqueduct infrastructure being present (infra.aqueduct, `research.json` `infra.aqueduct` `.requires`); this hierarchical dependency is not declared. Fuel. A Health mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: declare the aqueduct dependency (adr-028:22) and bind the Health need.

#### `building.city_wall`

- **Identifier:** `building.city_wall` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There are no walls, defence, siege or military. The Safety need is unbound (Sim.Data/content/needs.json:19-24). Stateless raiding takes grain with no defence term (Sim.Core/Systems/Appropriation/AppropriationSystem.cs:10-29). Sim.Core/Pathing/Pathfinder.cs:190 says walls are D-037's business.
- **Current baseline status:** Not baseline. LOCKED at founding, but it is the cheapest building to unlock. Requires mudbrick OR stone_dry (`research.json` `building.city_wall` `.requires`). mudbrick: `research.json` `mudbrick`, A2 d0, 1,050 RP, no prereq, a root node available at turn 0. stone_dry: `research.json` `stone_dry`, A2 d2, 880 RP, prereq ground_stone_early. Closure: 1 node / 1,050 RP, about 34 turns at the 30.8 RP/turn founding rate (`research.json` `tuning`). Both nodes emerged before the 4000 BCE epoch. RISK: founding dwellings are modelled as timber frame plus earth walls, with mudbrick as a reference class (sim.json:207; Sim.Core/Systems/SimConfig.cs:587-595). mudbrick's capabilities ('permanent multi-room house', 'storehouse', 'wall', `research.json` `mudbrick` `.unlocks.capabilities`) describe founding objects. Crediting founding housing as mudbrick knowledge would make walls knowledge-eligible at turn 0.
- **Related research node(s):** `mudbrick`, `stone_dry`
- **Realization requirements:** A construction project scaled to the settlement's perimeter or size, with stone and labour. sim.json:207 treats structural earth as a non-good. A Safety or defence mechanism (raiding, siege, battle layer) must consume it.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a defence mechanism (bind Safety; Appropriation or the D-011 battle layer). Rule explicitly that mudbrick knowledge is NOT baseline, otherwise walls become eligible at turn 0.

#### `building.castle`

- **Identifier:** `building.castle` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no military or defence layer.
- **Current baseline status:** Not baseline. LOCKED. Requires stone_fortification AND vault_dome (`research.json` `building.castle` `.requires`). stone_fortification: `research.json` `stone_fortification`, A4 d10, 3,520 RP, prereq stone_dry AND surveying AND (tin_bronze OR iron_bloomery). vault_dome: `research.json` `vault_dome`, A5 d12. Approximate cheapest closure: 27 nodes / 57,530 RP; not stage-gated.
- **Related research node(s):** `stone_fortification`, `vault_dome`
- **Realization requirements:** A construction project (stone and labour), a garrison, and a military or defence mechanism; none exists.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a military and defence layer.

#### `building.bastion_fort`

- **Identifier:** `building.bastion_fort` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no military, gunpowder or siege.
- **Current baseline status:** Not baseline. LOCKED. Requires trace_italienne (`research.json` `building.bastion_fort` `.requires`; node `research.json` `trace_italienne`, A7 d13, military subtree, 4,180 RP, prereq cannon_cast_bronze AND geometry_axiomatic). Research-stage gated. Approximate cheapest closure: 49 nodes / 136,580 RP.
- **Related research node(s):** `trace_italienne`
- **Realization requirements:** A construction project, artillery and a garrison, and a defence mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a military and defence layer.

#### `building.harbour`

- **Identifier:** `building.harbour` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. sim.json:21 states 'Vessels, ports and naval movement are OUT OF SCOPE'. Water is impassable to path building, and 'boats are a later milestone' (Sim.Core/Systems/PathBuild/PathBuildSystem.cs:42-45).
- **Current baseline status:** Not baseline. LOCKED. Requires plank_boat AND stone_dry (`research.json` `building.harbour` `.requires`). plank_boat: `research.json` `plank_boat`, A3 d5, 2,960 RP, prereq dugout AND copper_smelting. stone_dry: `research.json` `stone_dry`. Approximate cheapest closure: 14 nodes / 20,650 RP; not stage-gated. D-040 B3: no node may open sea travel (docs/d040-discovery-and-control.md:59-64; adr-029:425-428).
- **Related research node(s):** `plank_boat`, `stone_dry`
- **Realization requirements:** A construction project. A genuine site constraint: a coastal settlement. A sea mask exists (Sim.Core/Worldgen/TerrainSet.cs:21), and siting already scores shoreline access (Sim.Core/Worldgen/SettlementSiting.cs:3-13). Also vessels and a maritime or port mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a coastal site predicate, and a maritime layer that does NOT route sea travel through a node unlock (D-040 B3). This is outside current scope (sim.json:21).

#### `building.lighthouse`

- **Identifier:** `building.lighthouse` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. Ports and naval movement are out of scope (Sim.Data/content/sim.json:21).
- **Current baseline status:** Not baseline. LOCKED. Requires stone_fortification (`research.json` `building.lighthouse` `.requires`; node `research.json` `stone_fortification`, A4 d10, 3,520 RP). Approximate cheapest closure: 18 nodes / 26,140 RP; not stage-gated.
- **Related research node(s):** `stone_fortification`
- **Realization requirements:** A construction project and a coastal site. A harbour must be present: ADR-028 §1.3 names 'a lighthouse on a harbour' (adr-028:22), but the requirement does not declare it. Also a maritime mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: declare the harbour dependency, and a maritime layer that respects D-040 B3.

#### `building.dry_dock`

- **Identifier:** `building.dry_dock` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There are no vessels or shipbuilding (Sim.Data/content/sim.json:21).
- **Current baseline status:** Not baseline. LOCKED. Requires canal_lock (`research.json` `building.dry_dock` `.requires`; node `research.json` `canal_lock`, A6 d7, engineering, 4,180 RP, prereq irrigation_canal AND mortise_hull). Research-stage gated. Approximate cheapest closure: 30 nodes / 53,930 RP.
- **Related research node(s):** `canal_lock`
- **Realization requirements:** A construction project and a coastal or river site. A harbour must be present: ADR-028 §1.3 names 'a dry dock on a harbour' (adr-028:22), but the requirement does not declare it. Also a shipbuilding mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: declare the harbour dependency, and a shipbuilding or maritime layer.

#### `building.airfield`

- **Identifier:** `building.airfield` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no air movement and no petroleum fuel.
- **Current baseline status:** Not baseline. LOCKED. Requires aircraft (`research.json` `building.airfield` `.requires`; node `research.json` `aircraft`, A9 d21, engineering, 47,310 RP, prereq internal_combustion). Research-stage gated. Approximate cheapest closure: 72 nodes / 690,620 RP.
- **Related research node(s):** `aircraft`
- **Realization requirements:** A construction project, aviation fuel, aircraft, and an air-movement layer.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: an air-movement layer that is not opened by a node unlock (D-040 B3 analogue, adr-029:425-428), and fuel goods.

#### `building.radio_transmitter`

- **Identifier:** `building.radio_transmitter` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no communication or information mechanism and no electricity.
- **Current baseline status:** Not baseline. LOCKED. Requires radio (`research.json` `building.radio_transmitter` `.requires`; node `research.json` `radio`, A9 d17, engineering, 133,810 RP, prereq electromagnetism AND telegraph). Research-stage gated. Approximate cheapest closure: 52 nodes / 405,090 RP.
- **Related research node(s):** `radio`
- **Realization requirements:** A construction project, electricity capacity, and a communications mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a capacity system (adr-019:163) and an information mechanism.

#### `building.skyscraper`

- **Identifier:** `building.skyscraper` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no vertical-density mechanism: housing is a dwelling count at 6 persons per dwelling (Sim.Data/content/sim.json:213; Sim.Core/Systems/Housing/HousingSystem.cs).
- **Current baseline status:** Not baseline. LOCKED. Requires skyscraper (`research.json` `building.skyscraper` `.requires`; node `research.json` `skyscraper`, A9 d24, engineering, 66,910 RP, prereq bessemer AND electric_motor AND reinforced_concrete). Research-stage gated. Approximate cheapest closure: 87 nodes / 886,530 RP.
- **Related research node(s):** `skyscraper`
- **Realization requirements:** A construction project using steel and reinforced concrete (neither is a good), electricity for elevators, and an urban-density mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: steel and concrete goods, and a density mechanism.

#### `building.spaceport`

- **Identifier:** `building.spaceport` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet.
- **Current baseline status:** Not baseline. LOCKED. Requires rocket (`research.json` `building.spaceport` `.requires`; node `research.json` `rocket`, A9 d24, engineering, 756,960 RP, prereq internal_combustion AND aluminium AND calculus). Research-stage gated. Approximate cheapest closure: 82 nodes / 1,618,560 RP. The same node also gates project.orbital_launch (`research.json` `project.orbital_launch` `.requires`) and unit.missile (`research.json` `unit.missile` `.requires`).
- **Related research node(s):** `rocket`
- **Realization requirements:** A construction project, propellant and aerospace goods, and a launch mechanism (see project.orbital_launch).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: aerospace goods and a link to the orbital-launch project.

#### `building.semiconductor_fab`

- **Identifier:** `building.semiconductor_fab` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet.
- **Current baseline status:** Not baseline. LOCKED. Requires integrated_circuit (`research.json` `building.semiconductor_fab` `.requires`; node `research.json` `integrated_circuit`, A9 d24, engineering, 267,620 RP, prereq transistor AND photography). Research-stage gated. Approximate cheapest closure: 85 nodes / 1,577,810 RP.
- **Related research node(s):** `integrated_circuit`
- **Realization requirements:** A construction project, silicon and chemical inputs, electricity capacity, a skilled workforce, and an electronics good.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: electronics goods and a capacity system.

#### `building.data_centre`

- **Identifier:** `building.data_centre` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet.
- **Current baseline status:** Not baseline. LOCKED. Requires cloud_computing (`research.json` `building.data_centre` `.requires`; node `research.json` `cloud_computing`, A9 d26, engineering, 16,730 RP, prereq internet AND relational_database). Research-stage gated. Approximate cheapest closure: 91 nodes / 2,258,020 RP.
- **Related research node(s):** `cloud_computing`
- **Realization requirements:** A construction project, electricity capacity, a network (infra.internet_backbone, `research.json` `infra.internet_backbone` `.requires`), and an information mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: capacity and network systems.

#### `building.desalination_plant`

- **Identifier:** `building.desalination_plant` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no water good. Settlement siting instead requires shoreline or river access within a cutoff (Sim.Core/Worldgen/SettlementSiting.cs:3-13).
- **Current baseline status:** Not baseline. LOCKED. Requires desalination (`research.json` `building.desalination_plant` `.requires`; node `research.json` `desalination`, A9 d23, engineering, 11,830 RP, prereq electricity_generation). Research-stage gated. Approximate cheapest closure: 75 nodes / 587,170 RP.
- **Related research node(s):** `desalination`
- **Realization requirements:** A construction project, a coastal site (sea mask, TerrainSet.cs:21), electricity capacity, and a freshwater-supply mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a water-supply mechanism and a coastal site predicate.

#### `building.solar_farm`

- **Identifier:** `building.solar_farm` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no electricity system and no insolation channel; TerrainSet has temperature but no solar field (Sim.Core/Worldgen/TerrainSet.cs:20-27).
- **Current baseline status:** Not baseline. LOCKED. Requires solar_pv (`research.json` `building.solar_farm` `.requires`; node `research.json` `solar_pv`, A9 d24, engineering, 94,620 RP, prereq transistor AND quantum_mechanics). Research-stage gated. Approximate cheapest closure: 80 nodes / 1,288,590 RP.
- **Related research node(s):** `solar_pv`
- **Realization requirements:** A construction project, a site term for insolation (none exists), and an electricity capacity system with a grid.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a capacity system (adr-019:163) and an insolation site term.

#### `building.wind_farm`

- **Identifier:** `building.wind_farm` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet. There is no electricity system and no wind channel (Sim.Core/Worldgen/TerrainSet.cs:20-27).
- **Current baseline status:** Not baseline. LOCKED. Requires wind_turbine_modern (`research.json` `building.wind_farm` `.requires`; node `research.json` `wind_turbine_modern`, A9 d23, engineering, 47,310 RP, prereq composites AND electricity_generation). Research-stage gated. Approximate cheapest closure: 84 nodes / 917,940 RP.
- **Related research node(s):** `wind_turbine_modern`
- **Realization requirements:** A construction project, a wind-exposure site term (none exists), and an electricity capacity system with a grid.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a capacity system and a wind site term.

#### `building.fusion_plant`

- **Identifier:** `building.fusion_plant` (building)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only; no simulation system realizes it yet.
- **Current baseline status:** Not baseline. LOCKED. Requires fusion_power (`research.json` `building.fusion_plant` `.requires`; node `research.json` `fusion_power`, A9 d26, engineering, 94,620 RP, prereq nuclear_fusion_research; emerged 'speculative'). Research-stage gated. Approximate cheapest closure: 83 nodes / 1,406,590 RP.
- **Related research node(s):** `fusion_power`
- **Realization requirements:** A construction project, fusion fuel, and an electricity capacity system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** GENERIC: (a) goods.json project entry; (b) entity->project link (GoodsConfig.cs:81-85); (c) availability predicate at ConstructionSystem enqueue via ResearchQuery.IsKnowledgeEligible (ConstructionSystem.cs:92-95; adr-028:57); (d) Structures-reading effect mechanism; (e) UI/CLI issuer. SPECIFIC: a capacity system (adr-019:163) and a fuel good.

### 7.5 Units (registry)

#### `unit.spearmen`

- **Identifier:** `unit.spearmen` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.spearmen` and requires hafting (`research.json` `unit.spearmen` `.requires`). Sim.Core has no unit, formation, recruitment or combat code, and no order kind for raising troops (Sim.Core/Kernel/OrderLog.cs:12,21,36,49,69 — kinds 1,2,3,4,6 only).
- **Current baseline status:** Not baseline and not simulated. The Director's baseline is 'Clubmen or equivalent basic military' (docs/d045-research-calibration-rulings.md:42). It is declared outside the graph as baseline.basic_military with simulated:false (`research.json` `baseline.basic_military`; docs/adr/adr-029-research-engine.md:151). The registry has no clubmen or warband entity, so spearmen is the cheapest melee unit and it is research-gated. It is therefore NOT the baseline 'equivalent'. It is knowledge-ineligible at founding (Sim.Core/State/ResearchQuery.cs:540-551 with zero completed nodes).
- **Related research node(s):** `hafting`, `cordage`, `adhesive_natural`, `fire_making`
- **Realization requirements:** Knowledge: hafting (A1 depth 1, 520 RP; `research.json` `hafting`; prereq cordage AND adhesive_natural). The cheapest closure is cordage + adhesive_natural + hafting = 1,830 BaseCost, about 59 turns at 30.8 RP/turn; this ignores Eureka/exposure credit, the cost floor and population growth. Realization needs an owning system that does not exist: a military/formation system that (1) raises the formation from real adult cohorts by Ledger.Transfer and debits casualties through the Ledger (docs/d011-battle-layer-addendum.md:26-27; law 1); (2) equips it from goods, although no weapon good exists — the 14 goods are goods.json:5-101 and the only processed goods are bronze, tools, pottery and cloth; (3) supplies it on the network graph (docs/d039-command-fog-and-siege.md:162-164). It also needs an availability gate that reads IsKnowledgeEligible, which today has no consumer (ResearchQuery.cs:529-532).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future milestone): a military/formation system (battle layer is M6, d011:61; M4 strategic war is AutoResolver-only and the resolver is deferred, m4-spec.md:40 and WorldState.cs:630-633), equipment goods, an order kind to recruit, and an availability predicate that consumes knowledge eligibility. Data: none for this entity. Separate obligation: the same system must provide the research-free baseline tier (baseline.basic_military, a clubmen/warband formation). That tier must come neither from lowering spearmen's requirement nor from a fake node (d045:57-62). Content note: fire_making carries the techniques 'Fire-hardening of wood' and 'Thrown spear' (`research.json` `fire_making` `.unlocks.techniques`, `research.json` `fire_making` `.unlocks.techniques`), and hafting owns 'thrusting spear' (`research.json` `hafting` `.unlocks.capabilities`). So any spear-armed 'equivalent' would be research-owned, and the baseline formation must use equipment no node owns: unhafted wooden clubs (D-045 "Clubmen", d045:42) or bare manpower. It must not use axes or adzes (hafting, `research.json` `hafting` `.unlocks.capabilities`), stone cutting edges (knapping_oldowan, `research.json` `knapping_oldowan` `.unlocks.capabilities`) or fire-hardened wood (fire_making technique, `research.json` `fire_making` `.unlocks.techniques`).

#### `unit.archers`

- **Identifier:** `unit.archers` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.archers` and requires bow_simple (`research.json` `unit.archers` `.requires`). There is no unit, combat or recruitment code in Sim.Core and no recruit order (OrderLog.cs:12,21,36,49,69).
- **Current baseline status:** Not baseline and not simulated. The baseline 'clubmen or equivalent' (d045:42) is baseline.basic_military with simulated:false (`research.json` `baseline.basic_military`). A ranged 'equivalent' would need bow_simple, so archers are not the baseline. Knowledge-ineligible at founding.
- **Related research node(s):** `bow_simple`, `cordage`
- **Realization requirements:** Knowledge: bow_simple (A1 depth 1, 370 RP; `research.json` `bow_simple`; prereq cordage; capabilities 'ranged hunting' and 'ranged combat' at `research.json` `bow_simple` `.unlocks.capabilities`). The cheapest closure is cordage + bow_simple = 1,420 BaseCost, about 46 turns at 30.8 RP/turn (approximate). Realization needs a military/formation system that does not exist: manpower drawn from cohorts and casualties debited through the Ledger (d011:26-27), bows and arrows as equipment (no weapon good: goods.json:5-101), supply on the network (d039:162-164), and an availability gate that consumes eligibility (ResearchQuery.cs:529-532).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future milestone, M6 battle layer, d011:61): the formation system, equipment goods, a recruit order and an eligibility-consuming availability gate. Data: none. 'ranged hunting' has no consumer because the sim has no hunting activity: the food goods are grain, livestock and fish only (ProductionSystem.cs:373-377). That is not a conflict.

#### `unit.slingers`

- **Identifier:** `unit.slingers` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.slingers` and requires sling (`research.json` `unit.slingers` `.requires`). There is no unit or combat code in Sim.Core and no recruit order (OrderLog.cs:12,21,36,49,69).
- **Current baseline status:** Not baseline and not simulated. The basic-military baseline (d045:42) is simulated:false (`research.json` `baseline.basic_military`). Slingers need sling, so they cannot be the baseline 'equivalent'. Knowledge-ineligible at founding.
- **Related research node(s):** `sling`, `cordage`
- **Realization requirements:** Knowledge: sling (A1 depth 1, 260 RP; `research.json` `sling`; prereq cordage; capabilities 'ranged combat' and 'herd protection' at `research.json` `sling` `.unlocks.capabilities`). The cheapest closure is cordage + sling = 1,310 BaseCost, about 43 turns (approximate). Realization needs a formation system that does not exist: cohort manpower through the Ledger (d011:26-27), slings and shot as equipment (no good), supply on the network, and an eligibility-consuming availability gate (ResearchQuery.cs:529-532).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future, M6): formation system, equipment, recruit order and availability gate. Data: none. Soft content overlap: 'herd protection' (`research.json` `sling` `.unlocks.capabilities`) touches the baseline herding sector (WorldState.cs:321, default share 0.15 at :376). No predation or guarding mechanism exists, so this is not a live conflict.

#### `unit.chariot`

- **Identifier:** `unit.chariot` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.chariot` and requires chariot (`research.json` `unit.chariot` `.requires`). There is no unit, vehicle or animal-traction code in Sim.Core.
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `chariot`, `wheel_spoked`, `horse_domestication`, `cattle`, `casting_closed`
- **Realization requirements:** Knowledge: chariot (A3 depth 8, 2,090 RP; `research.json` `chariot`; prereq wheel_spoked AND horse_domestication — `research.json` `wheel_spoked` and `research.json` `horse_domestication`). The approximate cheapest closure is about 21 nodes and about 39,030 BaseCost (greedy OR choice). Realization needs a formation system (d011:26-27) plus:
- a horse or draught-animal stock — none exists; goods.json:14 'livestock' is generic food;
- spoked-wheel vehicles from timber and bronze — bronze exists (goods.json:79, recipe :136-157), but there is no vehicle good;
- crews from cohorts;
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): the formation system, a draught/riding animal good (D-040 B4 puts draught animals in the routes conversation, d040:75-76), a vehicle/equipment good and an availability gate. Data: none.

#### `unit.horse_archers`

- **Identifier:** `unit.horse_archers` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.horse_archers` and requires cavalry AND composite_bow (`research.json` `unit.horse_archers` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `cavalry`, `composite_bow`, `horse_domestication`, `bow_simple`, `sheep_goat`, `birch_tar`
- **Realization requirements:** Knowledge, both of:
- cavalry (A4 depth 4, 1,240 RP; `research.json` `cavalry`; prereq horse_domestication);
- composite_bow (A3 depth 2, 1,760 RP; `research.json` `composite_bow`; prereq bow_simple AND sheep_goat AND birch_tar).

The approximate cheapest closure is 11 nodes and 9,360 BaseCost, about 304 turns. Realization needs:
- riding horses (no horse good);
- composite bows (no weapon good);
- mounted manpower from cohorts through the Ledger (d011:26-27);
- supply;
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, riding-animal good, equipment goods and availability gate. Data: none.

#### `unit.heavy_cavalry`

- **Identifier:** `unit.heavy_cavalry` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.heavy_cavalry` and requires stirrup (`research.json` `unit.heavy_cavalry` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `stirrup`, `saddle`, `iron_bloomery`, `cavalry`
- **Realization requirements:** Knowledge: stirrup (A5 depth 6, 2,490 RP; `research.json` `stirrup`; prereq saddle AND iron_bloomery — `research.json` `saddle` and `research.json` `iron_bloomery`). The approximate cheapest closure is about 19 nodes and about 24,210 BaseCost. Realization needs horses (no good), iron armour and lances (no iron good — goods.json:5-101), elite manpower from cohorts (d011:26-27), supply, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, horse and iron/equipment goods, availability gate. Data: none.

#### `unit.crossbowmen`

- **Identifier:** `unit.crossbowmen` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.crossbowmen` and requires crossbow (`research.json` `unit.crossbowmen` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `crossbow`, `bow_simple`, `casting_closed`, `tin_bronze`
- **Realization requirements:** Knowledge: crossbow (A4 depth 7, 4,180 RP; `research.json` `crossbow`; prereq bow_simple AND casting_closed — `research.json` `casting_closed`). The cheapest closure is 13 nodes and about 30,700 BaseCost (cordage, grinding_stone, mudbrick, basketry, bow_simple, ochre_processing, pottery_open_fired, kiln_updraft, copper_smelting, casting_open, tin_bronze, casting_closed, crossbow). Realization needs crossbows with cast trigger mechanisms (no weapon good; bronze exists at goods.json:79), manpower from cohorts (d011:26-27), supply, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, equipment good and availability gate. Data: none.

#### `unit.longbowmen`

- **Identifier:** `unit.longbowmen` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.longbowmen` and requires longbow (`research.json` `unit.longbowmen` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. longbow is a Military-subtree node (branch 'military'), so it is unavailable until the research stage is reached: ResearchQuery.cs:105-111 (moved) makes availability require Branch < 0 OR stageReached, and the stage requires medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman (`research.json` `researchStage`).
- **Related research node(s):** `longbow`, `bow_simple`
- **Realization requirements:** Knowledge: longbow (A6 depth 2, 2,090 RP; `research.json` `longbow`; branch military; prereq bow_simple) plus the research stage. The approximate cheapest closure is about 20 nodes and about 31,120 BaseCost including the stage closure. Realization needs trained archers from cohorts (d011:26-27), bows (no weapon good), supply, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, equipment and availability gate. Data/content (for Director review, not a sim conflict): a self-bow-family infantry unit sits behind the university-level research stage only because longbow is filed in the Military subtree; consider whether it belongs in the main tree. Otherwise none.

#### `unit.siege_engine`

- **Identifier:** `unit.siege_engine` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.siege_engine` and requires siege_ram OR torsion_artillery OR trebuchet (`research.json` `unit.siege_engine` `.requires`). There is no siege, wall or structure-effect code: Structures are read by no system (docs/adr/adr-028-viability-and-saturation.md:60).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `siege_ram`, `torsion_artillery`, `trebuchet`, `wheel_solid`, `hide_working`, `crossbow`, `mechanics_archimedean`
- **Realization requirements:** Knowledge, any one of:
- siege_ram (A4 depth 5, 4,970 RP; `research.json` `siege_ram`; wheel_solid AND hide_working);
- torsion_artillery (A5 depth 13, 7,030 RP; `research.json` `torsion_artillery`; crossbow AND mechanics_archimedean);
- trebuchet (A6 depth 14, 5,910 RP; `research.json` `trebuchet`; military subtree, so research-stage gated).

The cheapest closure goes through siege_ram: 10 nodes, 13,050 BaseCost. Realization needs:
- timber and hides — these goods exist (goods.json:28,72);
- an engine-building or crew mechanism drawing on cohorts (d011:26-27);
- a fortification target with effect — building.city_wall is registry-only, and the siege mechanics in d039 are M6;
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future, M6 with D-039 siege): formation/siege system, a structure effect for walls, and an availability gate. Data: none.

#### `unit.artillery`

- **Identifier:** `unit.artillery` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.artillery` and requires cannon_early OR cannon_cast_bronze OR cannon_cast_iron (`research.json` `unit.artillery` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. All three nodes are in the Military subtree and therefore research-stage gated (ResearchQuery.cs:105-111 (moved); `research.json` `researchStage`).
- **Related research node(s):** `cannon_early`, `cannon_cast_bronze`, `cannon_cast_iron`, `gunpowder`, `cast_iron`
- **Realization requirements:** Knowledge, any one of:
- cannon_early (A6 depth 10, 7,030 RP; `research.json` `cannon_early`; gunpowder AND cast_iron);
- cannon_cast_bronze (A7; `research.json` `cannon_cast_bronze`);
- cannon_cast_iron (A7; `research.json` `cannon_cast_iron`).

The stage is also required. The approximate cheapest closure is about 38 nodes and about 97,940 BaseCost. Realization needs:
- gunpowder/saltpetre goods (none);
- cast guns from iron or bronze (no iron good; bronze exists, goods.json:79);
- gun crews from cohorts (d011:26-27);
- draught animals for haulage (none);
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, powder, iron and gun goods, and an availability gate. Data: none.

#### `unit.musketeers`

- **Identifier:** `unit.musketeers` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.musketeers` and requires matchlock OR flintlock (`research.json` `unit.musketeers` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. Both nodes are Military-subtree, so research-stage gated.
- **Related research node(s):** `matchlock`, `flintlock`, `cannon_early`, `crossbow`, `wheellock`
- **Realization requirements:** Knowledge, one of:
- matchlock (A7 depth 11, 4,180 RP; `research.json` `matchlock`; cannon_early AND crossbow);
- flintlock (A7 depth 17, 2,960 RP; `research.json` `flintlock`; wheellock).

The stage is also required. The approximate cheapest closure is about 47 nodes and about 123,430 BaseCost. Realization needs firearms and powder goods (none), drilled infantry from cohorts (d011:26-27), supply, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, firearm and powder goods, availability gate. Data: none.

#### `unit.war_galley`

- **Identifier:** `unit.war_galley` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.war_galley` and requires naval_ram (`research.json` `unit.war_galley` `.requires`). Sea movement does not exist:
- lattice water nodes are impassable (Sim.Core/Pathing/TraversalLattice.cs:14-15);
- PathBuild says 'boats are a later milestone' (Sim.Core/Systems/PathBuild/PathBuildSystem.cs:42-45, :180-184);
- 'Vessels, ports and naval movement are OUT OF SCOPE' (Sim.Data/content/sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding.
- **Related research node(s):** `naval_ram`, `plank_boat`, `casting_closed`, `dugout`
- **Realization requirements:** Knowledge: naval_ram (A4 depth 7, 2,960 RP; `research.json` `naval_ram`; plank_boat AND casting_closed — `research.json` `plank_boat` and `research.json` `casting_closed`). The approximate cheapest closure is about 17 nodes and about 34,680 BaseCost. Realization needs:
- a water-movement layer and boats as the same object as water routes (d040:75-76);
- a coastal settlement, timber and craft capacity (d040:66-69);
- a harbour — building.harbour (plank_boat AND stone_dry) is registry-only;
- a bronze ram (bronze good exists, goods.json:79);
- rowers from cohorts (d011:26-27);
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): the water-route/boat system (the D-040 B4 single design conversation), a naval formation system and an availability gate. Governance: D-040 B3 forbids a node opening sea travel (d040:59-64). The tension between that and knowledge eligibility is recorded but unruled (d044:841, T3). Data: none.

#### `unit.ship_of_the_line`

- **Identifier:** `unit.ship_of_the_line` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.ship_of_the_line` and requires ship_of_line (`research.json` `unit.ship_of_the_line` `.requires`). There is no sea movement (TraversalLattice.cs:14-15; sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. ship_of_line is Military-subtree, so research-stage gated.
- **Related research node(s):** `ship_of_line`, `carrack`, `cannon_cast_iron`
- **Realization requirements:** Knowledge: ship_of_line (A7 depth 17, 4,180 RP; `research.json` `ship_of_line`; carrack AND cannon_cast_iron) plus the stage. The approximate cheapest closure is about 73 nodes and about 228,290 BaseCost. Realization needs sea movement (absent), a shipyard or dry dock (registry-only buildings), timber and naval guns (no gun or iron good), crews from cohorts (d011:26-27), and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): water-route/boat system, naval formation system, gun and iron goods, availability gate. D-040 B3 (d040:59-64) applies. Data: none.

#### `unit.ironclad`

- **Identifier:** `unit.ironclad` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.ironclad` (name 'Steam warship'; requires screw_propeller AND iron_hull at `research.json` `unit.ironclad` `.requires`). There is no sea movement (TraversalLattice.cs:14-15; sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. Both nodes are Military-subtree, so research-stage gated.
- **Related research node(s):** `screw_propeller`, `iron_hull`, `steamboat`, `water_screw`, `puddling`
- **Realization requirements:** Knowledge, both of:
- screw_propeller (A8 depth 21, 14,070 RP; `research.json` `screw_propeller`; steamboat AND water_screw);
- iron_hull (A8 depth 22, 14,070 RP; `research.json` `iron_hull`; puddling AND screw_propeller).

The stage is also required. The approximate cheapest closure is about 74 nodes and about 490,770 BaseCost. Realization needs sea movement, iron/steel, coal and steam engines (none of these goods exist), dockyards, crews from cohorts, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): water-route system, industrial goods, naval formation system, availability gate. Data: optional cosmetic fix — the id 'ironclad' does not match the name 'Steam warship'. Otherwise none.

#### `unit.tank`

- **Identifier:** `unit.tank` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.tank` and requires tank (`research.json` `unit.tank` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. tank is Military-subtree, so research-stage gated.
- **Related research node(s):** `tank`, `internal_combustion`, `basic_process`, `cannon_cast_iron`
- **Realization requirements:** Knowledge: tank (A9 depth 23, 23,650 RP; `research.json` `tank`; internal_combustion AND basic_process AND cannon_cast_iron) plus the stage. The approximate cheapest closure is about 76 nodes and about 754,240 BaseCost. Realization needs steel, fuel and engines (no such goods), factory production, crews from cohorts (d011:26-27), and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): formation system, industrial goods and production, availability gate. Data: none.

#### `unit.military_aircraft`

- **Identifier:** `unit.military_aircraft` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.military_aircraft` and requires aircraft (`research.json` `unit.military_aircraft` `.requires`). There is no air layer, airfield effect or reconnaissance/visibility state in Sim.Core.
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. aircraft is in the Engineering subtree, so research-stage gated.
- **Related research node(s):** `aircraft`, `internal_combustion`
- **Realization requirements:** Knowledge: aircraft (A9 depth 21, 47,310 RP; `research.json` `aircraft`; internal_combustion; capabilities include 'aerial reconnaissance (D-039 B: reconnaissance no longer walks)' at `research.json` `aircraft` `.unlocks.capabilities`) plus the stage. The approximate cheapest closure is about 72 nodes and about 690,620 BaseCost. Realization needs:
- an airfield — building.airfield (requires aircraft) is registry-only;
- aluminium and fuel (no goods);
- pilots from cohorts;
- an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): air/formation system, goods and an availability gate. Data: none. Note: the 'aerial reconnaissance' capability presumes a scouting/visibility mechanism. The baseline exploration capability is itself not simulated (`research.json` `baseline.exploration`), and there is no fog or visibility state.

#### `unit.missile`

- **Identifier:** `unit.missile` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.missile` (name 'Ballistic missile') and requires rocket (`research.json` `unit.missile` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. rocket is in the Engineering subtree, so research-stage gated.
- **Related research node(s):** `rocket`, `internal_combustion`, `aluminium`, `calculus`
- **Realization requirements:** Knowledge: rocket (A9 depth 24, 756,960 RP; `research.json` `rocket`; internal_combustion AND aluminium AND calculus) plus the stage. The approximate cheapest closure is about 82 nodes and about 1.62M BaseCost. Realization needs rocket production, propellant and warhead goods (none), launch sites, operators from cohorts, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): weapons system, goods and production, availability gate. Data: none.

#### `unit.nuclear_submarine`

- **Identifier:** `unit.nuclear_submarine` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.nuclear_submarine` and requires nuclear_submarine (`research.json` `unit.nuclear_submarine` `.requires`). There is no sea movement (TraversalLattice.cs:14-15; sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. The node is Military-subtree, so research-stage gated.
- **Related research node(s):** `nuclear_submarine`, `nuclear_fission`, `screw_propeller`
- **Realization requirements:** Knowledge: nuclear_submarine (A9 depth 24, 133,810 RP; `research.json` `nuclear_submarine`; nuclear_fission AND screw_propeller) plus the stage. The approximate cheapest closure is about 83 nodes and about 1.23M BaseCost. Realization needs sea movement (absent), reactor fuel (no good), shipyards, crews from cohorts, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): water-route system, naval formation system, goods, availability gate. Data: none.

#### `unit.icbm`

- **Identifier:** `unit.icbm` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.icbm` and requires icbm (`research.json` `unit.icbm` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. icbm is Military-subtree, so research-stage gated.
- **Related research node(s):** `icbm`, `rocket`, `thermonuclear`, `computer`
- **Realization requirements:** Knowledge: icbm (A9 depth 25, 189,240 RP; `research.json` `icbm`; rocket AND thermonuclear AND computer) plus the stage. The approximate cheapest closure is about 91 nodes and about 3.17M BaseCost. Realization needs missile and fissile-material production (no goods), silos and launch infrastructure, operators, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): weapons system, goods, availability gate. Data: none.

#### `unit.combat_drone`

- **Identifier:** `unit.combat_drone` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.combat_drone` and requires combat_drone (`research.json` `unit.combat_drone` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. combat_drone is Military-subtree, so research-stage gated.
- **Related research node(s):** `combat_drone`, `gps`, `microprocessor`, `aircraft`
- **Realization requirements:** Knowledge: combat_drone (A9 depth 27, 23,650 RP; `research.json` `combat_drone`; gps AND microprocessor AND aircraft) plus the stage. The approximate cheapest closure is about 116 nodes and about 4.18M BaseCost. Realization needs electronics production, a satellite constellation (gps), airfields, operators, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): air/weapons system, goods, availability gate. Data: none.

#### `unit.stealth_aircraft`

- **Identifier:** `unit.stealth_aircraft` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.stealth_aircraft` and requires stealth (`research.json` `unit.stealth_aircraft` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. stealth is Military-subtree, so research-stage gated.
- **Related research node(s):** `stealth`, `composites`, `radar`, `computer`
- **Realization requirements:** Knowledge: stealth (A9 depth 24, 133,810 RP; `research.json` `stealth`; composites AND radar AND computer) plus the stage. The approximate cheapest closure is about 89 nodes and about 1.82M BaseCost. Realization needs aircraft production, composite materials (no goods), airfields, pilots, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): air system, goods, availability gate. Data: none.

#### `unit.aircraft_carrier`

- **Identifier:** `unit.aircraft_carrier` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.aircraft_carrier` and requires aircraft AND steam_turbine (`research.json` `unit.aircraft_carrier` `.requires`). There is no sea movement (TraversalLattice.cs:14-15; sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. Both nodes are Engineering-subtree, so research-stage gated.
- **Related research node(s):** `aircraft`, `steam_turbine`, `steam_high_pressure`, `open_hearth`
- **Realization requirements:** Knowledge, both of:
- aircraft (A9 depth 21, 47,310 RP; `research.json` `aircraft`);
- steam_turbine (A9 depth 23, 47,310 RP; `research.json` `steam_turbine`; steam_high_pressure AND open_hearth).

The stage is also required. The approximate cheapest closure is about 79 nodes and about 852,890 BaseCost. Realization needs sea movement (absent), steel and shipyards, embarked aircraft units, crews, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): water-route system, naval and air formation systems, goods, availability gate. Data: none.

#### `unit.helicopter`

- **Identifier:** `unit.helicopter` (unit)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes it yet. The entity is at `research.json` `unit.helicopter` and requires helicopter (`research.json` `unit.helicopter` `.requires`).
- **Current baseline status:** Not baseline and not simulated. Knowledge-ineligible at founding. helicopter is Engineering-subtree, so research-stage gated.
- **Related research node(s):** `helicopter`, `aircraft`
- **Realization requirements:** Knowledge: helicopter (A9 depth 22, 11,830 RP; `research.json` `helicopter`; prereq aircraft) plus the stage. The approximate cheapest closure is about 73 nodes and about 702,450 BaseCost. Realization needs aircraft production and fuel (no goods), crews, and an eligibility-consuming gate.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): air system, goods, availability gate. Data: none.

### 7.6 Activities (registry)

#### `activity.farming`

- **Identifier:** `activity.farming` (activity)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.Farm (Sim.Core/Systems/Production/ProductionSystem.cs):
- :165-167 — the call, with farm labour = Farming share × adults;
- :196-279 — the body: arable land read from previous-turn CatchmentSummaries at :211-216; Leontief min(arableKm2 × 26, farmLabor × 5 × toolFactor) at :232-234; weather × disaster at :163, :253; Ledger Harvest flow at :258.

The sector id is Farming = 0 (Sim.Core/State/WorldState.cs:320). The never-ordered default share is 0.55 (WorldState.cs:375-376), used when no allocation row exists (ProductionSystem.cs:148). Yields are in Sim.Data/content/sim.json:5-6. Simulated as matrix row sector.farming (see that row).
- **Current baseline status:** BASELINE: simulated and research-free. It runs every turn from turn 1 with zero completed nodes; no system reads research state. The first non-zero harvest comes at turn 2, because arable land is read from the previous turn's CatchmentSummaries, which are empty at founding (ProductionSystem.cs:211-216). It is covered by baseline.food_gathering (`research.json` `baseline.food_gathering`), but that entry's providedBy names a non-existent 'HarvestSystem'. CONTRADICTION: the proposed entity requires cereal_cultivation OR root_crop OR rice_wet OR millet OR maize OR sorghum_pearl_millet (`research.json` `activity.farming` `.requires`), so it is knowledge-ineligible at founding (cheapest unlock root_crop, 440 RP, about 14 turns). If it were ever wired as written, a zero-research civilization would harvest nothing. Entire activity baseline; research-owned part: none (D not used: no split mechanism exists).
- **Related research node(s):** `cereal_cultivation`, `root_crop`, `rice_wet`, `millet`, `maize`, `sorghum_pearl_millet`, `cereal_domesticated`, `grinding_stone`, `animal_traction`
- **Realization requirements:** No research. Output needs all of:
- a Farming share > 0 — the default 0.55, or a SectorAllocation order (OrderLog.cs:36);
- adults in the settlement;
- EffectiveArableKm2 > 0 from the previous turn's catchment;
- the weather and disaster multipliers (absent row = 1.0).

Optional: a labour-side tool factor of up to 1.3 from tool stock (ProductionSystem.cs:220-230). Tools come only from bronze through artisan-gated recipes (goods.json:136-176).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Data:
- Set activity.farming 'requires' to null, or remove the entity and let baseline.food_gathering carry it.
- Under the loader's reverse-index rule (Sim.Core/Systems/Research/ResearchContent.cs:1087-1096), remove 'activity.farming' from unlocks.activities of cereal_cultivation (`research.json` `cereal_cultivation`), root_crop (`research.json` `root_crop`), rice_wet (`research.json` `rice_wet`), millet (`research.json` `millet`), maize (`research.json` `maize`) and sorghum_pearl_millet (`research.json` `sorghum_pearl_millet`).
- If crop nodes are to own anything, give them separately named, mechanism-backed entities (for example crop-specific land suitability), not the farming activity.
- Reword root_crop's 'no granary needed — stored in ground' (`research.json` `root_crop` `.unlocks.capabilities`). It contradicts the grain-and-granary model (ConsumptionSystem.cs:309).
- Correct baseline.food_gathering providedBy from 'HarvestSystem' to ProductionSystem, with HarvestWeatherSystem supplying only the multiplier.

Code: none. Never wire this entity into ProductionSystem as written.

#### `activity.herding`

- **Identifier:** `activity.herding` (activity)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector:true (Sim.Core/Systems/Production/ProductionSystem.cs:180-184 call, :287-331 body; InSector livestock/fish at :373-377). Output = workers × 3.0 × abundance × weather, with workers split by deposit abundance (:314, :320; sim.json:191). Livestock is goods.json:14 (depositChannel moisture). Deposits are endowed at founding (WorldFounding.cs:220-221, :415-437). Sector id Herding = 1 (WorldState.cs:321), default share 0.15 (:376). Simulated as matrix row sector.herding_fishing (see that row).
- **Current baseline status:** BASELINE: simulated and research-free from turn 1, through baseline.food_gathering (`research.json` `baseline.food_gathering`). CONTRADICTION: the proposed entity requires sheep_goat OR cattle (`research.json` `activity.herding` `.requires`), so it is ineligible at founding. sheep_goat literally lists 'herding sector' and 'meat on the hoof' as its capabilities (`research.json` `sheep_goat` `.unlocks.capabilities`). Entire activity baseline; research-owned part: none (D not used: no split mechanism exists).
- **Related research node(s):** `sheep_goat`, `cattle`, `dog_domestication`
- **Realization requirements:** No research. Output needs a Herding share > 0 (default 0.15, or a SectorAllocation order), adults, a positive livestock deposit abundance (no deposit means no output, ProductionSystem.cs:303), and the weather × disaster multiplier. Herding cannot be targeted separately from fishing: the Herding pool splits across livestock and fish in proportion to abundance (:314).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Data:
- Set 'requires' to null, or remove the entity in favour of baseline.food_gathering.
- Remove 'activity.herding' from unlocks.activities of sheep_goat (`research.json` `sheep_goat`) and cattle (`research.json` `cattle`), as the reverse-index rule requires (ResearchContent.cs:1087-1096).
- Remove or reword sheep_goat's capability string 'herding sector' (`research.json` `sheep_goat` `.unlocks.capabilities`).
- If cattle are to own traction, dairy or dung fuel (`research.json` `cattle` `.unlocks.capabilities`), those need separate mechanisms; none exist.

Code: none.

#### `activity.fishing`

- **Identifier:** `activity.fishing` (activity)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector:true (Sim.Core/Systems/Production/ProductionSystem.cs:180-184, :287-331). Fish is a food deposit good (InSector :373-377; goods.json:21, depositChannel water). Water-channel abundance = meanMoisture² over the hinterland (WorldFounding.cs:431, :434). It shares the Herding sector (WorldState.cs:321 'herding/fishing'). Simulated as matrix row sector.herding_fishing (see that row).
- **Current baseline status:** BASELINE: simulated and research-free from turn 1 (baseline.food_gathering, `research.json` `baseline.food_gathering`). CONTRADICTION: the proposed entity requires fishing_hook (`research.json` `activity.fishing` `.requires`), so it is ineligible at founding. Other node strings also describe fishing:
- fishing_hook: 'pelagic fishing' (`research.json` `fishing_hook` `.unlocks.capabilities`);
- cordage: 'nets' (`research.json` `cordage` `.unlocks.capabilities`);
- basketry: 'fish traps' (`research.json` `basketry` `.unlocks.capabilities`);
- dugout: 'offshore fishing' (`research.json` `dugout` `.unlocks.capabilities`). Entire activity baseline; research-owned part: none (D not used: no split mechanism exists).
- **Related research node(s):** `fishing_hook`, `cordage`, `basketry`, `dugout`
- **Realization requirements:** No research. Output needs a Herding/fishing share > 0, adults, a positive fish deposit abundance, and the weather × disaster multiplier. Fishing is not a separately allocatable activity: it shares the Herding pool with livestock, split by abundance (ProductionSystem.cs:314).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Data:
- Set 'requires' to null, or remove the entity.
- Remove 'activity.fishing' from fishing_hook's unlocks.activities (`research.json` `fishing_hook`), under the reverse-index rule.
- If pelagic or offshore fishing is to be research-owned, it needs a separately named entity backed by a new mechanism (for example an extended fish-deposit yield). It must not be the fishing activity.

Code: none.

#### `activity.logging`

- **Identifier:** `activity.logging` (activity)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector:false (Sim.Core/Systems/Production/ProductionSystem.cs:185-189 call, :287-331 body). Timber is goods.json:28 (depositChannel moisture). Sector id Extraction = 2 (WorldState.cs:322), default share 0.10 (:376). Output rate = workers × 4.0 × abundance (sim.json:192). Simulated as matrix row sector.extraction (see that row).
- **Current baseline status:** BASELINE: simulated and research-free from turn 1. Research-free timber consumers depend on it:
- dwelling build and upkeep (sim.json:211; HousingSystem);
- pottery fuel (goods.json:104-121);
- bronze casting (:136-157);
- the granary and workshop projects (:181-214).

CONTRADICTION: the proposed entity requires ground_stone_early (`research.json` `activity.logging` `.requires`), whose capabilities 'felling' and 'heavy woodworking' (`research.json` `ground_stone_early` `.unlocks.capabilities`) describe this activity. hafting also owns 'axe' (`research.json` `hafting` `.unlocks.capabilities`). Entire activity baseline; research-owned part: none (D not used: no split mechanism exists).
- **Related research node(s):** `ground_stone_early`, `knapping_oldowan`, `hafting`
- **Realization requirements:** No research. Output needs an Extraction share > 0, adults and a positive timber deposit abundance. The player cannot aim extraction at timber: labour splits across all seven raw deposit goods in proportion to abundance (ProductionSystem.cs:314).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Data: set 'requires' to null, or remove the entity. Remove 'activity.logging' from ground_stone_early's unlocks.activities (`research.json` `ground_stone_early`), under the reverse-index rule. Code: none.

#### `activity.mining`

- **Identifier:** `activity.mining` (activity)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** ProductionSystem.FromDeposits with foodSector:false (Sim.Core/Systems/Production/ProductionSystem.cs:185-189; the comment at :176-178 says 'EXTRACTION is ore and stone'). It extracts stone (goods.json:35, elevation), clay (:42, water), copper-ore (:49, elevation, spread 0.8) and tin-ore (:57, elevation, spread 0.9). Deposits are rates, not stocks (WorldFounding.cs:194-196), so nothing depletes. Simulated as matrix row sector.extraction (see that row).
- **Current baseline status:** BASELINE: simulated and research-free from turn 1. ADR-028 records 'Extraction is ProductionSystem.FromDeposits … A mine is not a prerequisite for extraction' (docs/adr/adr-028-viability-and-saturation.md:59). CONTRADICTION: the proposed entity requires mining_shaft (`research.json` `activity.mining` `.requires`). mining_shaft is A3 depth 5, 2,960 RP, at `research.json` `mining_shaft` (prereq stone_dry AND copper_smelting). Its cheapest closure is 12 nodes and 19,080 BaseCost, about 619 turns at 30.8 RP/turn. Entire activity baseline; research-owned part: none (D not used: no split mechanism exists).
- **Related research node(s):** `mining_shaft`, `stone_dry`, `copper_smelting`
- **Realization requirements:** No research. Output needs an Extraction share > 0, adults and positive deposit abundance for stone, clay or ore. The split follows abundance only (ProductionSystem.cs:314). Ore also feeds artisan-gated bronze, not research-gated bronze (goods.json:136-157).
- **Exists at founding:** yes
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** Data:
- Set 'requires' to null, or remove the entity.
- Remove 'activity.mining' from mining_shaft's unlocks.activities (`research.json` `mining_shaft`), under the reverse-index rule.
- mining_shaft's 'ore from depth' and 'sustained metal supply' (`research.json` `mining_shaft` `.unlocks.capabilities`) have no consumer, because deposits never deplete and carry no depth. If the node is to own them, define a new mechanism (deposit depletion or a depth tier) and a separately named entity.

Code: none now.

#### `activity.caravans`

- **Identifier:** `activity.caravans` (activity)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes caravans or any carrier. Baseline overland trade runs without carriers. It is TradeArbitrageSystem (Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:12-24): pairwise arbitrage over network distances; deadband threshold = bulkPerUnit × pathCost × CostPerBulkCostUnit at :142 (sim.json:219-220); no transit loss and no carrier (:26-30). TradeScope classifies trade and gates nothing (Sim.Core/Systems/Trade/TradeScope.cs:45-49).
- **Current baseline status:** Caravans are not baseline and do not exist. The overland exchange they would carry IS baseline and research-free. Knowledge requirement: donkey OR camel (`research.json` `activity.caravans` `.requires`). Their capability strings are donkey 'pack transport' and 'caravan trade' (`research.json` `donkey` `.unlocks.capabilities`), and camel 'desert crossing' and 'long-distance caravan' (`research.json` `camel` `.unlocks.capabilities`). Ineligible at founding.
- **Related research node(s):** `donkey`, `camel`, `cattle`
- **Realization requirements:** Knowledge: donkey (A3 depth 3, 620 RP; `research.json` `donkey`) or camel (A3 depth 3, 620 RP; `research.json` `camel`), both with prereq cattle. The cheapest closure is dog_domestication + sheep_goat + cattle + camel or donkey = 2,120 BaseCost, about 69 turns. Realization (not implemented) needs:
- a pack-animal stock — no donkey, camel or draught good exists; goods.json:5-101 has only generic livestock;
- merchants to run the caravans — the Merchants latch exists (sim.json:175-178), but ClassMobility moves adults only into Artisans (ClassMobilitySystem.cs:190-196);
- a reachable route on the network;
- a carrier term inside the trade equation, for example a pack-animal coefficient in the :142 deadband (law 2).

Under D-040 B3/B4 (d040:59-76) and the D-044 T3 reading (d044:841), any movement or transport effect must emerge from those computed conditions. No node may open it.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future; the D-040 B4 'draught animals with routes' conversation, d040:75-76): a pack-animal good, a carrier term in TradeArbitrageSystem, and an availability predicate that consumes knowledge eligibility. Build that predicate only if the Director rules on the D-044 T3 tension (d044:841) and allows a knowledge term in a transport gate. Data: none now. The entity must never gate the baseline TradeArbitrageSystem.

#### `activity.coastal_shipping`

- **Identifier:** `activity.coastal_shipping` (activity)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes shipping. Sea movement does not exist:
- lattice water nodes are impassable (Sim.Core/Pathing/TraversalLattice.cs:14-15);
- PathBuild targets only the passable component — 'boats are a later milestone' (Sim.Core/Systems/PathBuild/PathBuildSystem.cs:42-45, :180-184);
- 'Vessels, ports and naval movement are OUT OF SCOPE' (sim.json:21).

River transport is baseline, but only as a cost coefficient — riverCostFactor 0.2 inside the lattice node cost (sim.json:21-22; TraversalLattice.cs:26-42) — not as a vessel.
- **Current baseline status:** Not baseline and not simulated. Knowledge requirement: sail_square (`research.json` `activity.coastal_shipping` `.requires`). Ineligible at founding. OVERLAP: sail_square's capabilities 'upriver travel without rowing' and 'river trade at scale' (`research.json` `sail_square` `.unlocks.capabilities`) describe the river corridor the founding world already has. dugout also lists 'river and lake transport' (`research.json` `dugout` `.unlocks.capabilities`).
- **Related research node(s):** `sail_square`, `dugout`, `loom_warp_weighted`, `plank_boat`
- **Realization requirements:** Knowledge: sail_square (A3 depth 5, 3,520 RP; `research.json` `sail_square`; dugout AND loom_warp_weighted). The cheapest closure is 13 nodes and 12,530 BaseCost, about 407 turns. Realization (not implemented) needs:
- a coastal settlement, timber and craft capacity (d040:66-69);
- boats as the same object as water routes (d040:75-76), which requires a water-movement layer — water is impassable today;
- a harbour — building.harbour is registry-only;
- crews.

D-040 B3 (d040:59-64) rules that a node opening sea travel is 'a calendar gate wearing a tree'. D-044 T3 reading (d044:841): no node opens sea travel or a network edge type, and the implementation wires no node to any movement or transport mechanism. Under that reading the realization conditions are computed and research-free: a coastal settlement, timber and craft capacity (d040:66-69).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): the water-route/boat system, as one design conversation with draught animals and route improvement (d040:75-76), . BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841). The knowledge requirement sail_square (`research.json` `activity.coastal_shipping` `.requires`) is a latent D-040 B3 conflict, not ownership. It is inert only because nothing consumes eligibility (adr-029:424-427; ResearchQuery.cs:529-531 at d4ec6cd). Director options: (a) null the requirement and remove the entity from sail_square's (`research.json` `sail_square` `.unlocks.activities`) unlocks.activities, per the reverse-index rule (ResearchContent.cs:1087-1096 at d4ec6cd); (b) keep it as non-gating knowledge text; (c) rule T3 under ADR-028 §3 (adr-028:53). Do not build an eligibility-consuming availability predicate. Data: reword sail_square's river capability strings (`research.json` `sail_square` `.unlocks.capabilities`), or tie them to a new tier, so they do not claim river transport the founding world already has.

#### `activity.ocean_shipping`

- **Identifier:** `activity.ocean_shipping` (activity)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** registry only — no simulation system realizes shipping. Water is impassable (TraversalLattice.cs:14-15), boats are a later milestone (PathBuildSystem.cs:42-45), and naval movement is out of scope (sim.json:21).
- **Current baseline status:** Not baseline and not simulated. Knowledge requirement: carrack OR caravel OR polynesian_canoe (`research.json` `activity.ocean_shipping` `.requires`). All three nodes are in the Military subtree, so the research stage is also required (ResearchQuery.cs:105-111 (moved); `research.json` `researchStage`). Ineligible at founding.
- **Related research node(s):** `carrack`, `caravel`, `polynesian_canoe`, `dugout`, `latitude_sailing`, `stern_rudder`, `compass_magnetic`
- **Realization requirements:** Knowledge, one of:
- polynesian_canoe (A6 depth 11, 2,960 RP; `research.json` `polynesian_canoe`; dugout AND latitude_sailing);
- caravel (A7 depth 8, 16,730 RP; `research.json` `caravel`);
- carrack (A7 depth 9, 4,180 RP; `research.json` `carrack`).

The research stage is also required. The approximate cheapest closure is about 23 nodes and about 38,610 BaseCost, including the stage. Realization (not implemented) needs:
- a water-movement layer with boats as the same object as routes (d040:75-76);
- a coastal settlement, timber and craft capacity (d040:66-69);
- ocean-capable vessels, navigation and provisioning;
- crews.

This is the exact case D-040 B3 rejects as a tech unlock (d040:59-64).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code (future): the water-route/boat system (D-040 B4 transport conversation, d040:75-80). BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841). The knowledge requirement carrack OR caravel OR polynesian_canoe (`research.json` `activity.ocean_shipping` `.requires`) is a latent D-040 B3 conflict, not ownership. It is inert only because nothing consumes eligibility (adr-029:424-427; ResearchQuery.cs:529-531 at d4ec6cd). Director options: (a) null the requirement and remove the entity from caravel's (`research.json` `caravel` `.unlocks.activities`), carrack's (`research.json` `polynesian_canoe` `.secondaryDomains`) and polynesian_canoe's (`research.json` `celestial_navigation` `.prereq`) unlocks.activities, per the reverse-index rule (ResearchContent.cs:1087-1096 at d4ec6cd); (b) keep it as non-gating knowledge text; (c) rule T3 under ADR-028 §3 (adr-028:53). Do not build an eligibility-consuming availability predicate. Data/content (for review): the ocean-sailing nodes sit in the Military subtree (branch 'military' on `research.json` `caravel`, `research.json` `carrack`, `research.json` `polynesian_canoe`), which places ocean shipping behind the university-level research stage. Otherwise none.

### 7.7 Infrastructure (registry)

#### `infra.dirt_path`

- **Identifier:** `infra.dirt_path` (infrastructure (NetworkEdgeRow, EdgeTypes.DirtPath))
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** PathBuildSystem (Sim.Core/Systems/PathBuild/PathBuildSystem.cs:111-165 accrual; :168-225 BuildSegments; edge append :211-215; revision bump :219-220). Ids.cs:69-72 (DirtPath is the only edge type). sim.json:15-19.
- **Current baseline status:** Baseline, automatic, with no order and no research. None at turn 0; first edge at t2, 91 edges by t40 seed 42. Each edge halves traversal cost and triggers a catchment recompute.
- **Related research node(s):** `track_road`, `stone_dry`, `road_paved`
- **Related matrix items:** `infra.road_track`, `infra.road_built`
- **Realization requirements:** Accrual = 0.02 × (construction share × adults × dt − housing labour) (PathBuildSystem.cs:131-147). Each segment costs step cost × 50 (:205-207). Built toward the most fertile catchment-unreached node, within the passable component only (water is impassable, :184-185).
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None to the simulation. Content: infra.road_track (requires track_road, `research.json` `infra.road_track` `.requires`) and infra.road_built (`research.json` `infra.road_built` `.requires`) must map to NEW edge tiers, never to EdgeTypes.DirtPath. track_road's 'path improvement' (`research.json` `track_road` `.unlocks.capabilities`) is consistent only on that reading; the code itself reserves 'better road tiers' for later (PathBuildSystem.cs:50-52).

#### `infra.road_track`

- **Identifier:** `infra.road_track` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.road_track` `.requires`). The closest system is PathBuildSystem, which lays only EdgeTypes.DirtPath network edges (Sim.Core/Systems/PathBuild/PathBuildSystem.cs:211-215). DirtPath is the only edge type (Sim.Core/State/Ids.cs:69-72). Knowledge eligibility is computed by ResearchQuery.IsKnowledgeEligible (Sim.Core/State/ResearchQuery.cs:534-551); its only caller outside the research system is the CLI count (Sim.Cli/ResearchCli.cs:155).
- **Current baseline status:** Not baseline. Not knowledge-eligible at founding: track_road is A2 d3 with prereq timber_frame; the heuristic cheapest closure is 6 nodes / 3,850 BaseCost RP, about 125 turns at the founding 30.8 RP/turn. A separate, research-free baseline exists and must not be confused with this entity: automatic dirt paths built from construction-sector labour (PathBuildSystem.cs:26-39; sim.json:15-18). There are no edges at turn 0 (WorldFounding.cs:44). PathBuild explicitly reserves 'better road tiers' for later (PathBuildSystem.cs:50-52).
- **Related research node(s):** `track_road`
- **Realization requirements:** track_road only makes the trackway knowledge-eligible (ADR-028 §3). A network builder must then lay or upgrade edges. That needs: banked construction labour, as PathBuild already accrues it (PathBuildSystem.cs:26-39); timber as material (dirt paths consume no goods today); and a reason to exist. The node's capabilities, 'reliable crossing of wetland' and 'path improvement' (`research.json` `track_road` `.unlocks.capabilities`), have no carrier: the movement-cost field is slope-only on land and has no wetland class (Sim.Core/Worldgen/Worldgen.cs:132-145).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: add a new edge type (for example Trackway) beside DirtPath (Ids.cs:69-72) with its own traversal factor. That builder spends banked labour and timber, through Ledger.Flow, on the new tier. Data: TUNE parameters for speed factor, build cost and timber per segment in the sim.json pathBuild block. Invariant: DirtPath stays ungated baseline, and infra.road_track must never be mapped onto the existing dirt path. BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.road_built`

- **Identifier:** `infra.road_built` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.road_built` `.requires`). No stone-founded edge tier exists: PathBuild lays only DirtPath (PathBuildSystem.cs:211-215; Ids.cs:69-72).
- **Current baseline status:** Not baseline. Not knowledge-eligible at founding: requires track_road (A2 d3) AND stone_dry (A2 d2, pre ground_stone_early); heuristic cheapest closure 7 nodes / 4,730 RP. The only overlap is the baseline dirt path, which is a distinct, lower tier.
- **Related research node(s):** `track_road`, `stone_dry`
- **Realization requirements:** Knowledge makes it eligible; a network builder realizes it. Needs: construction labour, as in the PathBuild bank; stone, which exists and is extracted from turn 1 (goods.json:35) and today is consumed only by the two construction projects (goods.json:179-215); a traversal factor better than DirtPath's 0.5 (sim.json:18). It also depends on the presence of a trackway or path edge to upgrade (hierarchy, ADR-028 §1.3).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a further edge type and a tier-upgrade path in the network builder. Stone consumption must go through Ledger.Flow (law 1); PathBuild consumes no goods today. Data: TUNE parameters for speed factor, build cost and stone per segment. BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.road_paved`

- **Identifier:** `infra.road_paved` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.road_paved` `.requires`). There is no paved edge tier (Ids.cs:69-72).
- **Current baseline status:** Not baseline. Requires road_paved (A5 d10, pre surveying); heuristic cheapest closure 7 nodes / 17,370 RP. No founding counterpart.
- **Related research node(s):** `road_paved`
- **Realization requirements:** A network builder lays a paved tier with labour and stone, ideally upgrading lower tiers (ADR-028 §1.3). Of its capabilities (`research.json` `road_paved` `.unlocks.capabilities`), 'all-weather bulk transport' would act through edge cost in trade and catchment. 'Military mobility' has no carrier: no military code exists (the only match is a comment at WorldState.cs:695).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a paved edge type and tier upgrade. Data: TUNE parameters for speed, cost and materials. Content: 'military mobility' stays inert until a military layer exists. BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.road_macadam`

- **Identifier:** `infra.road_macadam` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.road_macadam` `.requires`). There is no macadam edge tier (Ids.cs:69-72).
- **Current baseline status:** Not baseline. Requires macadam (A8 d11, branch engineering, pre road_paved). Because macadam is a specialized-subtree node, the subtree must first be opened by the research stage (`research.json` `researchStage`). Heuristic closure 8 nodes / 25,730 RP.
- **Related research node(s):** `macadam`, `road_paved`
- **Realization requirements:** Needs a network builder with an upgrade path from the paved tier (hierarchy), crushed-stone material and labour. Its capability, 'coaching speed doubled' (`research.json` `macadam` `.unlocks.capabilities`), presumes a vehicle and coach model, which does not exist.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a macadam edge tier. Data: TUNE parameters. No goods change is strictly required, because stone exists (goods.json:35). BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.courier_relay`

- **Identifier:** `infra.courier_relay` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.courier_relay` `.requires`). Its function, message and order latency, is not simulated: control carries 'no distance-decay, communication-latency' term (Sim.Core/State/WorldState.cs:602-604), and orders carry no transit delay (Sim.Core/Kernel/OrderLog.cs:90).
- **Current baseline status:** Not baseline. Requires track_road (A2 d3) AND horse_domestication (A3 d3, pre cattle); heuristic closure 10 nodes / 7,110 RP. No founding counterpart.
- **Related research node(s):** `track_road`, `horse_domestication`
- **Realization requirements:** Needs: road edges present (it depends on the infra.road_track instance, ADR-028 §1.3); horses (livestock is the only animal good, goods.json:14, so there is no horse stock); staffed stations; and the command-latency model of D-039 Part A (docs/d039-command-fog-and-siege.md:6) for it to have any effect. horse_domestication's 'fast overland movement' is at `research.json` `horse_domestication` `.unlocks.capabilities`.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** First a D-039 latency system (its own milestone packet); then relay instances along road edges, and an eligibility read (ADR). Data: optionally a horse good or a herd composition.

#### `infra.bridge`

- **Identifier:** `infra.bridge` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.bridge` `.requires`). Rivers are land and do not change passability (sim.json:21). A river-threaded lattice block is cheaper to cross, at riverCostFactor 0.2 (sim.json:22; Sim.Core/Pathing/TraversalLattice.cs:26-47).
- **Current baseline status:** Not baseline as an object. Requires bridge_stone (A5 d12, pre arch AND concrete_pozzolan); heuristic closure 17 nodes / 39,880 RP. Its function, 'permanent river crossing' (`research.json` `bridge_stone` `.unlocks.capabilities`), is already free at founding, because no system ever charges for crossing a river.
- **Related research node(s):** `bridge_stone`
- **Realization requirements:** Needs: a river-crossing site on a network route; construction labour and stone (goods.json:35); and, above all, a crossing cost for the bridge to remove. None exists: TraversalLattice blends river cells as a discount only (TraversalLattice.cs:26-47).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Model: a perpendicular river-crossing cost term must exist before a bridge can do anything. That needs an ADR, because riverCostFactor's derivation (sim.json:21) does not include one. Then a bridge edge or instance on the network. Without these the entity is decorative. BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix. Second unruled tension: D-009 "expensive, era-gated, terrain-crossing edges" (d009:12) vs D-040 B3, reported in d040:222-226 and queue.md:551-553.

#### `infra.basin_irrigation`

- **Identifier:** `infra.basin_irrigation` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.basin_irrigation` `.requires`). No irrigation term exists in Catchment, Production or Worldgen: fertility = tempSuit × moisture × river boost (Sim.Core/Worldgen/Worldgen.cs:115-129); harvest = min(arable × 26, labour × 5 × tools) (Sim.Core/Systems/Production/ProductionSystem.cs:36-37, 215, 232-233).
- **Current baseline status:** Not baseline, and cleanly separable from the baseline. The baseline yield deliberately excludes the floodplain: ADR-013's derivation deducts 'channel/floodplain 12 ha' from the cropped share (docs/adr/adr-013-lattice-denomination-and-agronomic-recalibration.md:225). The weather-variance reference class is 'rain-fed cereal agriculture without irrigation' (Sim.Core/Systems/SimConfig.cs:374). This refutes the 'soft overlap' that the registry recon raised. Requires irrigation_basin (A2 d2, pre cereal_cultivation; emerged ~5,000 BCE, which is before the 4,000 BCE epoch); heuristic closure 3 nodes / 4,330 RP. Caveat: the closure runs through cereal_cultivation, a research-owned node whose capability founding farming already exercises (the activity.farming contradiction).
- **Related research node(s):** `irrigation_basin`, `cereal_cultivation`
- **Realization requirements:** Needs: a river floodplain inside the settlement catchment (river mask and river-adjacent fertility boost, Worldgen.cs:124-128); construction labour for dykes and basins; and an effect channel. That channel would be added floodplain arable in CatchmentSummary.EffectiveArableKm2 (ProductionSystem.cs:215) and/or lower harvest variance, matching 'predictable harvest from flood' (`research.json` `irrigation_basin` `.unlocks.capabilities`).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an irrigation-instance carrier (a site-bound structure row) and a mechanism in Catchment/Production. The mechanism must be coefficients inside the land or variance equations (law 2), derived from a stated reference class (CR-003 §5.1). It needs an eligibility read and an ADR. Content: resolve the dependency on cereal_cultivation, which inherits the C1 farming contradiction.

#### `infra.canal_irrigation`

- **Identifier:** `infra.canal_irrigation` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.canal_irrigation` `.requires`). Moisture is a pure function of distance to water (Worldgen.cs:113-114), and no system raises it.
- **Current baseline status:** Not baseline. Requires irrigation_canal (A2 d2, pre cereal_cultivation); heuristic closure 3 nodes / 5,650 RP. No baseline counterpart: the baseline yield is rainfed (SimConfig.cs:374; ADR-013:223-225).
- **Related research node(s):** `irrigation_canal`, `cereal_cultivation`
- **Realization requirements:** Needs: a river water source, a canal route and construction labour. Its effect would be 'cultivation beyond rainfall' and 'arable extension' (`research.json` `irrigation_canal` `.unlocks.capabilities`) through catchment arable land, plus a long-run salinisation consequence (`research.json` `irrigation_canal` `.unlocks.capabilities`) that needs its own stock.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a canal-irrigation instance with a route; a catchment arable-extension mechanism; a salinisation state; an eligibility read and an ADR. Data: TUNE or derived coefficients.

#### `infra.terraces`

- **Identifier:** `infra.terraces` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.terraces` `.requires`). Slope affects only movement cost (Worldgen.cs:132-145), never arable land.
- **Current baseline status:** Not baseline. The baseline yield deducts 'slope and terrace break 15 ha' (ADR-013:225), so hillside cultivation is cleanly outside the baseline. Requires terrace (A2 d3, pre stone_dry AND cereal_cultivation); heuristic closure 6 nodes / 6,430 RP. Content duplication: stone_dry also lists the capability 'terracing' (`research.json` `stone_dry` `.unlocks.capabilities`).
- **Related research node(s):** `terrace`, `stone_dry`, `cereal_cultivation`
- **Realization requirements:** Needs: sloped land in the catchment, stone and labour. Its effect would be 'hillside cultivation' and 'erosion control' (`research.json` `terrace` `.unlocks.capabilities`) through added arable land on slopes.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a terrace instance carrier; an arable-on-slope term in Catchment; an eligibility read and an ADR. Content: make stone_dry 'terracing' and the terrace node agree (drop one, or reword).

#### `infra.qanat`

- **Identifier:** `infra.qanat` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.qanat` `.requires`). Founding and colony siting both require water access within siting.waterAccessCutoffPx = 24 (Sim.Core/Worldgen/SettlementSiting.cs:75-85; Sim.Data/content/worldgen.json:49; Sim.Core/Systems/Colonization/ColonizationSystem.cs:156-157).
- **Current baseline status:** Not baseline. Requires qanat (A4 d10, pre well AND surveying AND mining_shaft); heuristic closure 20 nodes / 29,430 RP. Consistent with founding: no arid, waterless settlement can exist.
- **Related research node(s):** `qanat`, `well`, `surveying`, `mining_shaft`
- **Realization requirements:** Needs: an arid site with an aquifer and slope; tunnelling labour (mining_shaft knowledge); and an effect on siting or colonization ('settlement in arid zones', 'irrigation without river', `research.json` `qanat` `.unlocks.capabilities`) and on catchment arable land.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a siting term in SettlementSiting/Colonization that admits sites beyond the water cutoff where a qanat is realizable; a qanat instance; an eligibility read and an ADR.

#### `infra.well`

- **Identifier:** `infra.well` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.well` `.requires`). Siting scores zero beyond the water-access cutoff (SettlementSiting.cs:81-82; worldgen.json:49), and colonies use the same frontier (ColonizationSystem.cs:156-157). There is no drought or water-supply mechanism.
- **Current baseline status:** Not baseline, and consistent with founding: every founded or colonized settlement has surface or river water. Requires well (A2 d3, pre stone_dry); heuristic closure 4 nodes / 2,360 RP, about 76 founding turns. Epoch note: well emerged ~8,000 BCE (`research.json` `well`ff), before the 4,000 BCE start, yet it is research-owned because the start has zero completed nodes (D-045:28-45; the CR-006 tension).
- **Related research node(s):** `well`, `stone_dry`
- **Realization requirements:** Needs per-settlement construction (stone and labour). It only has an effect if a siting or colonization term admits well-dependent sites ('settlement away from rivers') or a drought water channel exists ('reliable water in drought', `research.json` `well` `.unlocks.capabilities`).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: siting and colonization admit sites beyond the cutoff when a well is realizable; optionally a drought-water mechanism; an eligibility read and an ADR. None of this is needed for founding correctness.

#### `infra.aqueduct`

- **Identifier:** `infra.aqueduct` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.aqueduct` `.requires`). There is no urban water demand and no disease; the Health need is unbound (Sim.Data/content/needs.json:25-30).
- **Current baseline status:** Not baseline. Requires aqueduct_channel OR aqueduct_arcade OR aqueduct_tunnel. The OR is effectively aqueduct_tunnel alone, because aqueduct_channel requires aqueduct_tunnel and aqueduct_arcade requires aqueduct_channel. aqueduct_tunnel is A4 d11, pre qanat AND surveying; heuristic closure 21 nodes / 31,190 RP.
- **Related research node(s):** `aqueduct_tunnel`, `aqueduct_channel`, `aqueduct_arcade`, `qanat`, `surveying`
- **Realization requirements:** Needs: a water source and a surveyed route to the settlement; construction (stone and labour); and an urban water-demand or Health mechanism for it to have any effect ('long-distance urban water', `research.json` `aqueduct_channel` `.unlocks.capabilities`).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a water-supply/Health mechanism (binding needs.json Health) and an aqueduct instance with a route; an eligibility read and an ADR. Content: the three-way OR is redundant and could be simplified to aqueduct_tunnel; this is optional.

#### `infra.urban_drainage`

- **Identifier:** `infra.urban_drainage` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.urban_drainage` `.requires`). Sanitation and disease are not modelled (no disease/epidemic code in Sim.Core); Health is unbound (needs.json:25-30).
- **Current baseline status:** Not baseline. Requires fired_brick (A3 d4, pre kiln_updraft AND mudbrick; 'drains', `research.json` `fired_brick` `.unlocks.capabilities`); heuristic closure 6 nodes / 13,570 RP.
- **Related research node(s):** `fired_brick`, `kiln_updraft`, `mudbrick`
- **Realization requirements:** Needs: fired brick as a material (no brick good exists among the 14 goods, goods.json:7-97); construction labour; urban density; and a Health or sanitation mechanism to act on.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a Health/sanitation mechanism; a drainage instance; an eligibility read and an ADR. Data: a brick good or recipe if materials are explicit.

#### `infra.sewer`

- **Identifier:** `infra.sewer` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.sewer` `.requires`). Health is unbound (needs.json:25-30), and there is no sanitation model.
- **Current baseline status:** Not baseline. Requires vault_dome (A5 d12) AND fired_brick (A3 d4); heuristic closure 17 nodes / 40,910 RP.
- **Related research node(s):** `vault_dome`, `fired_brick`
- **Realization requirements:** Needs: fired brick and vaulting construction, labour and city scale. Its effect needs a Health/sanitation mechanism, and it depends on drainage presence (hierarchy, ADR-028 §1.3).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a Health/sanitation mechanism and sewer instances; an eligibility read and an ADR.

#### `infra.sewerage_system`

- **Identifier:** `infra.sewerage_system` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.sewerage_system` `.requires`). There is no Health, disease or sanitation model (needs.json:25-30).
- **Current baseline status:** Not baseline. Requires germ_theory (A8 d15, medicine subtree) AND reinforced_concrete (A8 d22, engineering subtree). Both sit behind the research stage (`research.json` `researchStage`); heuristic closure 76 nodes / 685,890 RP.
- **Related research node(s):** `germ_theory`, `reinforced_concrete`
- **Realization requirements:** Needs: concrete and steel materials (no such goods, goods.json:7-97), pumping and power (no electricity, ADR-019:163), construction at city scale, and a Health mechanism ('public health engineering', `research.json` `germ_theory` `.unlocks.capabilities`).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a Health mechanism, goods and power; sewerage instances; an eligibility read and an ADR.

#### `infra.fish_weir`

- **Identifier:** `infra.fish_weir` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.fish_weir` `.requires`). Baseline fishing, which is not this entity, runs in the Herding/fishing sector from water-channel deposits: fish = pool × weight × OutputPerHerderPerYear × abundance (Sim.Core/Systems/Production/ProductionSystem.cs:47-50, 180-184; Sim.Data/content/goods.json:21-25; Sectors.Herding at Sim.Core/State/WorldState.cs:321).
- **Current baseline status:** Not baseline as an object; the related fishing activity is baseline. Requires basketry (A1 d1, pre cordage; 'fish traps', `research.json` `basketry` `.unlocks.capabilities`); heuristic closure 2 nodes / 1,670 RP, about 54 founding turns. Epoch note: basketry emerged ~27 kya, before the 4,000 BCE start (CR-006 tension), yet it is research-owned under D-045's zero-node start.
- **Related research node(s):** `basketry`, `cordage`
- **Realization requirements:** Needs: a river or shore site in the catchment; construction (timber, fiber and labour); and an effect channel. That channel would be higher fish capture per herding/fishing worker at that site, as a coefficient inside the FromDeposits equation (law 2), not a flat buff.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a weir instance and a site-bound term in Production fish yield; an eligibility read and an ADR. Invariant: it must never gate baseline fishing. The activity.fishing gate (fishing_hook) is a separate contradiction outside this row.

#### `infra.navigation_canal`

- **Identifier:** `infra.navigation_canal` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.navigation_canal` `.requires`). Water is impassable to path building, and 'boats are a later milestone' (PathBuildSystem.cs:42-45). Vessels, ports and naval movement are out of scope (sim.json:21).
- **Current baseline status:** Not baseline. Requires canal_navigation (A7 d10, engineering subtree, pre canal_lock AND surveying; 'bulk inland transport at 1/4 road cost', `research.json` `canal_navigation` `.unlocks.capabilities`); heuristic closure 24 nodes / 43,800 RP. D-040 B3 forbids any node opening a movement mode (docs/d040-discovery-and-control.md:59-64). The canal must be infrastructure used by vessels that exist by computed conditions.
- **Related research node(s):** `canal_navigation`, `canal_lock`, `surveying`
- **Realization requirements:** Needs: canal construction across land with locks and a water supply; labour; and a water-transport carrier (vessels), which does not exist.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a canal edge type usable by a vessel carrier; a boat/vessel model (its own milestone, under D-040 B3). BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.railway`

- **Identifier:** `infra.railway` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.railway` `.requires`). There is no rail edge type (Ids.cs:69-72) and no iron, steel or coal good (goods.json:7-97).
- **Current baseline status:** Not baseline. Requires railway (A8 d23, engineering subtree, pre locomotive AND puddling AND surveying); heuristic closure 65 nodes / 502,120 RP.
- **Related research node(s):** `railway`, `locomotive`, `puddling`, `surveying`
- **Realization requirements:** Needs: rails (iron and steel), locomotives, fuel, construction labour, and a rail edge tier whose cost feeds trade and catchment ('national markets', `research.json` `railway` `.unlocks.capabilities`).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a rail edge type; iron, steel and coal goods and recipes. BLOCKED on a Director ruling on D-044 Part D T3 (UNRESOLVED, owner Director; d044:833,841): under the reading in force no node opens a network edge type and no node is wired to any movement or transport mechanism (adr-029:424-427), which stands against ADR-028 §3 "technology-unlocked" (adr-028:53). The edge-tier mechanism belongs to the open transport conversation (D-040 B4, d040:75-80; queue.md:531-545), not to this matrix.

#### `infra.telegraph_line`

- **Identifier:** `infra.telegraph_line` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.telegraph_line` `.requires`). Its stated effect, 'D-039 A3 order latency collapse' (`research.json` `telegraph` `.unlocks.capabilities`), has nothing to collapse: there is no communication latency (WorldState.cs:602-604), and orders carry no transit delay (OrderLog.cs:90).
- **Current baseline status:** Not baseline. Requires telegraph (A8 d16, engineering subtree, pre electromagnetism AND wire_drawing); heuristic closure 46 nodes / 264,030 RP.
- **Related research node(s):** `telegraph`, `electromagnetism`, `wire_drawing`
- **Realization requirements:** Needs: wire (copper exists only as ore, goods.json:49; there is no metal or wire good), poles, stations, labour, and the D-039 command-latency model to act on.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a D-039 latency system; line instances; an eligibility read and an ADR. Data: metal and wire goods.

#### `infra.submarine_cable`

- **Identifier:** `infra.submarine_cable` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.submarine_cable` `.requires`). There is no latency model, and naval movement is out of scope (sim.json:21).
- **Current baseline status:** Not baseline. Requires submarine_cable (A8 d22, pre telegraph AND screw_propeller; 'global information network', `research.json` `submarine_cable` `.unlocks.capabilities`); heuristic closure 75 nodes / 638,810 RP.
- **Related research node(s):** `submarine_cable`, `telegraph`, `screw_propeller`
- **Realization requirements:** Needs: a telegraph network present (hierarchy, ADR-028 §1.3), cable-laying ships, cable material, and the latency model.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a latency system, vessels and cable instances; an eligibility read and an ADR.

#### `infra.telephone_network`

- **Identifier:** `infra.telephone_network` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.telephone_network` `.requires`). There is no communication model (WorldState.cs:602-604).
- **Current baseline status:** Not baseline. Requires telephone (A8 d17, pre telegraph; 'real-time conversation at distance', `research.json` `telephone` `.unlocks.capabilities`); heuristic closure 47 nodes / 273,980 RP.
- **Related research node(s):** `telephone`, `telegraph`
- **Realization requirements:** Needs: exchanges, lines, electricity and the latency model; it builds on the telegraph network (hierarchy).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a latency or communication system, electricity capacity and network instances; an eligibility read and an ADR.

#### `infra.power_grid`

- **Identifier:** `infra.power_grid` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.power_grid` `.requires`). No electricity exists. ADR-019 defines electricity as 'generation capacity, grid connectivity and demand, not a stockpile' (docs/adr/adr-019-architecture-constitution-addendum.md:163).
- **Current baseline status:** Not baseline. Requires electricity_generation (A8 d22, pre dynamo AND electric_light AND electromagnetism; 'electricity as CAPACITY (ADR-019 §5)', `research.json` `electricity_generation` `.unlocks.capabilities`); heuristic closure 69 nodes / 568,090 RP.
- **Related research node(s):** `electricity_generation`, `dynamo`, `electric_light`, `electromagnetism`
- **Realization requirements:** Needs: generation (a building.power_station instance, hierarchy ADR-028 §1.3), grid construction connecting settlements, and demand.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an electricity-capacity system per ADR-019 and grid instances; an eligibility read and an ADR.

#### `infra.mobile_network`

- **Identifier:** `infra.mobile_network` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.mobile_network` `.requires`). There is no communication or electricity model.
- **Current baseline status:** Not baseline. Requires mobile_phone (A9 d26, pre radio AND microprocessor AND telephone; 'ubiquitous communication', `research.json` `mobile_phone` `.unlocks.capabilities`); heuristic closure 84 nodes / 2,283,020 RP.
- **Related research node(s):** `mobile_phone`, `radio`, `microprocessor`, `telephone`
- **Realization requirements:** Needs: towers, electricity, electronics and a communication/latency model; it builds on the telephone network (hierarchy).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: communication and electricity systems and network instances; an eligibility read and an ADR.

#### `infra.internet_backbone`

- **Identifier:** `infra.internet_backbone` (infrastructure)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `infra.internet_backbone` `.requires`). There is no communication model. Its capabilities 'D-039 A2 lag collapse' and 'D-041 media at global scale' (`research.json` `internet` `.unlocks.capabilities`ff) have no carrier.
- **Current baseline status:** Not baseline. Requires internet (A9 d25, pre computer AND telephone AND integrated_circuit); heuristic closure 83 nodes / 2,210,380 RP.
- **Related research node(s):** `internet`, `computer`, `telephone`, `integrated_circuit`
- **Realization requirements:** Needs: cable and data infrastructure, electricity, and a D-039 latency and D-041 media model; it builds on the telephone and submarine-cable networks (hierarchy).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: latency, media and electricity systems and backbone instances; an eligibility read and an ADR.

### 7.8 Institutions (registry)

#### `inst.burial`

- **Identifier:** `inst.burial` (institution)
- **Ownership classification:** **A BASELINE**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.burial` `.requires`, requires null). A null requirement is always met (Sim.Core/State/ResearchQuery.cs:526-527,545-547), so it is knowledge-eligible from turn 0. That is visible only in the CLI count (Sim.Cli/ResearchCli.cs:155). No institution row, table or system exists (docs/design/arch-FGH-civics-institutions-diffusion.md:111, F38).
- **Current baseline status:** Baseline at the knowledge level: one of only 3 of 129 entities eligible at founding. It is not in the research.json baseline list (`research.json` `baseline` … `research.json` `baseline.migration`, six entries). It is not simulated: the Belonging/Faith need is unbound (Sim.Data/content/needs.json:31-36), although a founding religion exists as a population-bucket dimension (Sim.Data/content/sim.json:155-160; Sim.Core/Worldgen/WorldFounding.cs:79-91).
- **Related research node(s):** none
- **Realization requirements:** None from research. It is a practice of the founding population, with no material, construction or personnel condition. If an institutions system is ever built, burial should be present from founding for the founding culture and religion rather than adopted later. Today nothing represents it.
- **Exists at founding:** partial
- **Initial existence (capability / object / both):** capability
- **Required code/data changes:** None required now: a null requirement is valid and must not be filled with an invented prerequisite (ADR-028 §3), and nothing consumes it. When an institutions system exists (CR-010), founding seeds it as present, and binding Belonging/Faith is that system's packet. Optional data: list it in the research.json baseline array as simulated:false so the Glass Box agrees with the registry.

#### `inst.scribal_school`

- **Identifier:** `inst.scribal_school` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.scribal_school` `.requires`). There is no institutions system (arch-FGH:111 F38; civilization-progression-architecture.md:190), and no literacy or education variable (Sim.Core/State/Variables.cs:36-93 publishes four variables).
- **Current baseline status:** Not baseline. Requires cuneiform OR hieroglyphic OR chinese_script (A3 d4–d6); heuristic closure 5 nodes / 4,930 RP. At 6baafbe the requirement was cuneiform alone; the proposal widens it. Reverse-index defect: in the proposed file only cuneiform lists it, and hieroglyphic and chinese_script do not. The loader rejects that (Sim.Core/Systems/Research/ResearchContent.cs:1087-1096); the unlock reverse-index repair (ADR-029 addendum A §A.5) repairs it.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Writing knowledge only makes it eligible. An institutions system must establish it (civilization-progression-architecture.md:76 §5.4; ADR-029:101-104, 'an institution is established, not researched'). Type-specific viability applies (ADR-028:11-26): literate teachers (personnel), a pupil population, and a host building (building.school, `research.json` `building.school` `.requires`; host rule at civilization-progression-architecture.md:209).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system and an institution-instance table keyed to PolityId/SettlementId (WorldState.cs:694-697), with a CR-010 ruling and an ADR. Data: apply the unlock reverse-index repair (ADR-029 addendum A §A.5) (add inst.scribal_school to the hieroglyphic and chinese_script unlocks).

#### `inst.law_code`

- **Identifier:** `inst.law_code` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.law_code` `.requires`). The civic law_code's own description says 'the institution itself is established by its owning system once this civic is complete' (`research.json` `law_code` `.desc`). There is no owning system, and the polity row carries no government or institution state (WorldState.cs:694-697).
- **Current baseline status:** Not baseline. Requires the civic law_code (key 1001, A3 d7, prereq cuneiform OR hieroglyphic OR chinese_script); heuristic closure 6 nodes / 6,170 RP.
- **Related research node(s):** `law_code`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Civic completion grants governance capability and eligibility; adoption is a separate act with its own conditions (civilization-progression-architecture.md:76). The conditions are polity-level adoption, enforcement capacity and administrative personnel. M7 is named as the home of institutional realization (civilization-progression-architecture.md:80).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions or governance system that adopts it (CR-010; ADR). Data: none.

#### `inst.archive`

- **Identifier:** `inst.archive` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.archive` `.requires`). There is no institutions system (arch-FGH:111).
- **Current baseline status:** Not baseline. Requires (cuneiform OR hieroglyphic OR chinese_script) AND stamp_seal (A2 d3, pre ground_stone_early AND pottery_open_fired); heuristic closure 11 nodes / 11,010 RP. At 6baafbe the requirement was cuneiform AND stamp_seal. Reverse-index defect: hieroglyphic and chinese_script do not list it in the proposed file; the unlock reverse-index repair (ADR-029 addendum A §A.5) repairs this.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal`
- **Realization requirements:** Needs establishment by an institutions system, with scribes (personnel), a host building (storehouse or temple; host rule civilization-progression-architecture.md:209) and records. stamp_seal's 'sealing stores' and 'administrative control' (`research.json` `stamp_seal` `.unlocks.capabilities`) are node capabilities unlocked at completion, not the archive itself.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR). Data: apply the unlock reverse-index repair (ADR-029 addendum A §A.5) for hieroglyphic and chinese_script.

#### `inst.textile_workshop`

- **Identifier:** `inst.textile_workshop` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.textile_workshop` `.requires`). Baseline cloth production, which is not this entity, is the ungated weaving recipe: fiber 3 to cloth (Sim.Data/content/goods.json:121-134). It runs in the crafting sector from turn 1 (ProductionSystem.cs:57-60, 388-397), and cloth is in the founding Comfort basket (Sim.Data/content/needs.json:93-98).
- **Current baseline status:** Not baseline as an institution; cloth-making is baseline. Requires loom_warp_weighted (A2 d4) AND archive. archive is an institution atom, read at knowledge level as its own requirement (ResearchQuery.cs:525-526,546-547); heuristic closure 18 nodes / 16,360 RP. Content contradiction: loom_warp_weighted claims 'cloth' (`research.json` `loom_warp_weighted` `.unlocks.capabilities`), which the founding weaving recipe already produces.
- **Related research node(s):** `loom_warp_weighted`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: an archive institution actually present (presence, not knowledge; hierarchy ADR-028 §1.3); artisans (the class exists and emerges from computed state, sim.json:166-172); fiber supply; a host workshop (the workshop project exists but is inert, goods.json:195-215, ADR-028:60); and establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system with presence atoms. Its effect must be a mechanism on organized weaving throughput and must never gate the baseline weaving recipe. ADR. Content: reword loom_warp_weighted's 'cloth' capability (for example 'wide loom cloth at volume').

#### `inst.aramaic_admin`

- **Identifier:** `inst.aramaic_admin` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.aramaic_admin` `.requires`). There is no administrative or latency model: control rows are written per settlement at founding with strength 1.0 (WorldFounding.cs:289-295), and control carries no latency term (WorldState.cs:602-604).
- **Current baseline status:** Not baseline. Requires abjad (A4 d7; 'cheap literacy', `research.json` `abjad` `.unlocks.capabilities`) AND archive (an institution atom); heuristic closure 12 nodes / 14,530 RP.
- **Related research node(s):** `abjad`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: an archive present; an administrative apparatus across controlled settlements; scribes; and establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions and administration system (CR-010; ADR).

#### `inst.natural_philosophy`

- **Identifier:** `inst.natural_philosophy` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.natural_philosophy` `.requires`). Research capacity takes only population as input (ResearchQuery.cs:359 (moved)), so no institution can contribute to it.
- **Current baseline status:** Not baseline. Requires (cuneiform OR hieroglyphic OR chinese_script) AND astronomy_records (A4 d9); heuristic closure 7 nodes / 10,870 RP. At 6baafbe it was alphabet_vowels AND astronomy_records. Reverse-index defect: alphabet_vowels still lists it, and none of the three scripts does; the loader rejects this, and the unlock reverse-index repair (ADR-029 addendum A §A.5) repairs it.
- **Related research node(s):** `astronomy_records`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: scholar personnel, patronage and a host; establishment by an institutions system. Any research effect would be a future modifier (civilization-progression-architecture.md:139).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR). Data: apply the unlock reverse-index repair (ADR-029 addendum A §A.5) (remove it from alphabet_vowels; add it to cuneiform, hieroglyphic and chinese_script).

#### `inst.library`

- **Identifier:** `inst.library` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.library` `.requires`). No node lists it, which is consistent with the reverse-index rule because its requirement names only institutions.
- **Current baseline status:** Not baseline. Requires archive AND scribal_school (institution atoms only). At knowledge level this expands to scripts + stamp_seal (ResearchQuery.cs:525-526); heuristic closure 11 nodes / 11,010 RP. It duplicates building.library (`research.json` `building.library` `.requires`), whose requirement diverges: cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper, so the building is reachable through papyrus with no writing. The research stage's knowledge predicate (`research.json` `researchStage`) is built from building.library, building.university and inst.university.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal`
- **Realization requirements:** Needs: archive and scribal_school actually present (hierarchy, ADR-028:21, which names 'a university on a library'); a host building.library (civilization-progression-architecture.md:209); scribes and texts; and establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system whose presence atoms replace the knowledge-level reading of institution references (CR-010; ADR). Content: reconcile the building.library and inst.library requirements (at minimum, require writing for building.library).

#### `inst.coined_wage`

- **Identifier:** `inst.coined_wage` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.coined_wage` `.requires`). There is no money: the conserved quantities are Population and per-good stocks only (Sim.Core/State/Ids.cs:74-95), and grain is the numeraire (goods.json:3). Labour is allocated by sector shares, not wages (WorldState.cs:318-325).
- **Current baseline status:** Not baseline. Requires the civic coined_wage (key 1004, A4 d13, pre coinage_electrum; description at `research.json` `coined_wage` `.desc`); heuristic closure 19 nodes / 33,140 RP.
- **Related research node(s):** `coined_wage`, `coinage_electrum`
- **Realization requirements:** Needs: a coin money stock (a law-1 conserved quantity), a wage labour market and polity adoption. None of these exist.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a money stock and ledger reasons, a wage mechanism, an institutions system; ADRs for the new conserved quantity.

#### `inst.census`

- **Identifier:** `inst.census` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.census` `.requires`). Population is exact in state (conserved buckets) and there is no polity information or fog layer, so a census has no information function to perform.
- **Current baseline status:** Not baseline. Requires the civic census (key 1003, A4 d9, pre scripts AND stamp_seal; description at `research.json` `census` `.desc`); heuristic closure 12 nodes / 15,980 RP.
- **Related research node(s):** `census`, `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal`
- **Realization requirements:** Needs: polity adoption, administrative capacity and enumerators; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system and, for any effect, a polity-knowledge or administration model (CR-010; ADR).

#### `inst.legal_code_roman`

- **Identifier:** `inst.legal_code_roman` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.legal_code_roman` `.requires`). There is no legal or governance system (WorldState.cs:694-697).
- **Current baseline status:** Not baseline. Requires the civic legal_code_roman (key 1002, A4 d9, pre law_code; description at `research.json` `legal_code_roman` `.desc`); heuristic closure 7 nodes / 8,660 RP.
- **Related research node(s):** `legal_code_roman`, `law_code`
- **Realization requirements:** Needs: polity adoption, enforcement and jurists; establishment by an institutions system. It also probably needs inst.law_code present (hierarchy), although the requirement names only the civic.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions or governance system (CR-010; ADR).

#### `inst.historiography`

- **Identifier:** `inst.historiography` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.historiography` `.requires`).
- **Current baseline status:** Not baseline. Requires (cuneiform OR hieroglyphic OR chinese_script) AND library (an institution atom); heuristic closure 11 nodes / 11,010 RP. At 6baafbe it was alphabet_vowels AND library. Reverse-index defect: alphabet_vowels still lists it, and none of the scripts does; the unlock reverse-index repair (ADR-029 addendum A §A.5) repairs this.
- **Related research node(s):** `cuneiform`, `hieroglyphic`, `chinese_script`, `stamp_seal`
- **Realization requirements:** Needs: a library present (hierarchy), literate scholars and patronage; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR). Data: apply the unlock reverse-index repair (ADR-029 addendum A §A.5).

#### `inst.military_medicine`

- **Identifier:** `inst.military_medicine` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.military_medicine` `.requires`). There is no military: the only match for military code is a comment at WorldState.cs:695, and basic military is 'not yet simulated' (`research.json` `baseline.basic_military`). Health is unbound (needs.json:25-30).
- **Current baseline status:** Not baseline. Requires surgery_instruments (A5 d9; 'military medicine', `research.json` `surgery_instruments` `.unlocks.capabilities`) AND census (civic); heuristic closure 23 nodes / 44,790 RP.
- **Related research node(s):** `surgery_instruments`, `census`
- **Realization requirements:** Needs: an army to serve, physicians, a host hospital and a census institution present; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a military layer (D-011), a Health mechanism, an institutions system; ADRs.

#### `inst.mint_institution`

- **Identifier:** `inst.mint_institution` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.mint_institution` `.requires`). There is no money stock (Ids.cs:74-95) and no precious-metal good (goods.json:7-97).
- **Current baseline status:** Not baseline. Requires coinage_electrum (A4 d12) AND census (civic); heuristic closure 19 nodes / 36,870 RP. It duplicates building.mint (coinage_electrum alone, `research.json` `building.bathhouse`); hosting the institution in that building is consistent with civilization-progression-architecture.md:209.
- **Related research node(s):** `coinage_electrum`, `census`
- **Realization requirements:** Needs: a host building.mint, precious metal (silver, gold or electrum goods), a money stock and a census institution present; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: money and metal goods, an institutions system; ADRs.

#### `inst.university`

- **Identifier:** `inst.university` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.university` `.requires`). It is the intended writer of ResearchCostModifiers, a table that no system writes (WorldState.cs:861-875; SystemCatalog.cs:315-318).
- **Current baseline status:** Not baseline. Requires library (an institution atom) AND legal_code_roman (civic). The knowledge closure is scripts, stamp_seal, law_code and legal_code_roman: heuristic 13 nodes / 14,740 RP. It duplicates building.university (medicine_hippocratic AND (geometry_axiomatic OR algebra), `research.json` `building.university` `.requires`), with a divergent requirement. The research stage (`research.json` `researchStage`) is the knowledge-level reading of inst.university together with the two buildings. Its institutional half cannot be evaluated (ADR-029:358-360).
- **Related research node(s):** `legal_code_roman`, `law_code`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: a library present (ADR-028:21); a host building.university; faculty; maturity and type-specific viability with diminishing returns (ADR-028 §1-§2); and establishment by an institutions system. Its first writer of cost factors owes the D-021 brake (civilization-progression-architecture.md:189).
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system that writes ResearchCostModifiers, with its D-021 brake (CR-010; ADR); an institution-present atom for the research stage. Content: reconcile the building.university and inst.university requirements.

#### `inst.hospital`

- **Identifier:** `inst.hospital` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.hospital` `.requires`). Health is unbound (needs.json:25-30), and there is no disease model.
- **Current baseline status:** Not baseline. Requires medicine_hippocratic (A4 d8; 'trained physicians', `research.json` `medicine_hippocratic` `.unlocks.capabilities`) AND pharmacology (A5 d9); heuristic closure 8 nodes / 9,330 RP. building.hospital has the identical requirement (`research.json` `building.hospital` `.requires`), which is consistent with the host/function split.
- **Related research node(s):** `medicine_hippocratic`, `pharmacology`
- **Realization requirements:** Needs: a host building.hospital (civilization-progression-architecture.md:209), physicians, upkeep, and establishment by an institutions system. Its effect needs a Health mechanism.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: Health binding, an institutions system; ADRs.

#### `inst.translation_movement`

- **Identifier:** `inst.translation_movement` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.translation_movement` `.requires`). There is no foreign-knowledge or exposure mechanism with an institutional input; research credit sources are only eureka and foreign_exposure (`research.json` `tuning.accelerationCreditSources`).
- **Current baseline status:** Not baseline. Requires paper (A5 d1, pre cordage; cost 11,830) AND library (an institution atom); heuristic closure 12 nodes / 22,840 RP.
- **Related research node(s):** `paper`, `cordage`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: a library present, translators (personnel), contact with foreign corpora and patronage; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system and a contact/diffusion model; ADRs.

#### `inst.guild`

- **Identifier:** `inst.guild` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.guild` `.requires`). The related Artisans class emerges from computed state with no institution, at 'food_surplus_ratio > 1.3 && population > 520' (Sim.Data/content/sim.json:166-172; ClassMobilitySystem.cs:190-196).
- **Current baseline status:** Not baseline as an institution; artisan specialization is emergent and research-free. Requires textile_workshop (an institution atom) AND legal_code_roman (civic); heuristic closure 20 nodes / 20,090 RP.
- **Related research node(s):** `legal_code_roman`, `law_code`, `loom_warp_weighted`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: an artisan class present (the latch is active), a textile_workshop present, a legal framework, and establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR). Invariant: it must never gate artisan emergence (law 4; D-020 latches).

#### `inst.quarantine`

- **Identifier:** `inst.quarantine` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.quarantine` `.requires`). There is no disease or epidemic model anywhere in Sim.Core (grep), and Health is unbound (needs.json:25-30).
- **Current baseline status:** Not baseline. Requires hospital (an institution atom) AND census (civic); heuristic closure 15 nodes / 20,380 RP.
- **Related research node(s):** `medicine_hippocratic`, `pharmacology`, `census`
- **Realization requirements:** Needs: a hospital and a census institution present, an epidemic model to act on, and enforcement capacity; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a disease model, an institutions system; ADRs.

#### `inst.newspaper`

- **Identifier:** `inst.newspaper` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.newspaper` `.requires`). There is no literacy, media or distribution model.
- **Current baseline status:** Not baseline. Requires printing_press (A7 d13, engineering subtree, behind the research stage); heuristic closure 22 nodes / 98,940 RP. Change from 6baafbe: 6baafbe required 'printing_press AND postal_imperial' with postal_imperial declared unresolved, which reads false, so newspaper, mass_schooling and mass_media_broadcast were unreachable. The proposal drops that atom and makes all three reachable. If adopted, ADR-029:420 ('the one such reference is inst.newspaper → postal_imperial') becomes stale.
- **Related research node(s):** `printing_press`
- **Realization requirements:** Needs: printing presses (a building.printing_house host), a literate readership, a distribution network, and establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system and literacy/media models (ADRs). Docs: amend ADR-029 §10 (line 420) if the proposal removes the unresolved postal_imperial atom.

#### `inst.scientific_society`

- **Identifier:** `inst.scientific_society` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.scientific_society` `.requires`).
- **Current baseline status:** Not baseline. Requires university (an institution atom) AND printing_press AND optics_ibn_haytham (A6 d12, natural_science subtree); heuristic closure 40 nodes / 135,110 RP.
- **Related research node(s):** `printing_press`, `optics_ibn_haytham`, `legal_code_roman`, `stamp_seal`, `cuneiform`, `hieroglyphic`, `chinese_script`
- **Realization requirements:** Needs: a university present, printing, scientist personnel and a journal medium; establishment by an institutions system. Any research effect would be a future modifier.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR).

#### `inst.joint_stock`

- **Identifier:** `inst.joint_stock` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.joint_stock` `.requires`). There is no capital or money stock (Ids.cs:74-95). The Merchants class can latch on (sim.json:176-178), but only class index 1 is ever moved (ClassMobilitySystem.cs:190-196).
- **Current baseline status:** Not baseline. Requires the civic joint_stock (key 1006, A6 d15, pre bill_of_exchange AND legal_code_roman; description at `research.json` `joint_stock` `.desc`); heuristic closure 27 nodes / 67,400 RP.
- **Related research node(s):** `joint_stock`, `bill_of_exchange`, `legal_code_roman`
- **Realization requirements:** Needs: money and capital, a merchant class with people in it, a legal framework, and adoption by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: money and capital, merchant mobility, an institutions system; ADRs.

#### `inst.central_bank`

- **Identifier:** `inst.central_bank` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.central_bank` `.requires`). There is no money (Ids.cs:74-95).
- **Current baseline status:** Not baseline. Requires joint_stock (civic) AND paper_money (A6 d13; 'money not limited by metal', `research.json` `paper_money` `.unlocks.capabilities`); heuristic closure 29 nodes / 86,440 RP.
- **Related research node(s):** `joint_stock`, `paper_money`
- **Realization requirements:** Needs: a money system, a joint-stock institution present, and polity adoption.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: money, an institutions system; ADRs.

#### `inst.patent`

- **Identifier:** `inst.patent` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.patent` `.requires`).
- **Current baseline status:** Not baseline. Requires the civic patent (key 1005, A7 d14, pre legal_code_roman AND printing_press; description at `research.json` `patent` `.desc`); heuristic closure 25 nodes / 106,850 RP.
- **Related research node(s):** `patent`, `legal_code_roman`, `printing_press`
- **Realization requirements:** Needs: a legal system present, a registry office, and adoption. Any innovation effect would be a future modifier.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR).

#### `inst.standard_threads`

- **Identifier:** `inst.standard_threads` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.standard_threads` `.requires`). There is no machinery or industry model (goods.json:7-97).
- **Current baseline status:** Not baseline. Requires slide_rest_lathe (A8 d18, engineering subtree). slide_rest_lathe also declares 'standard threads' as a node capability (`research.json` `slide_rest_lathe` `.unlocks.capabilities`). That capability string is technology-owned and unlocked at completion (ResearchQuery.UnlockedCapabilities); the adopted standard, this entity, is a realization. Heuristic closure 58 nodes / 247,690 RP.
- **Related research node(s):** `slide_rest_lathe`
- **Realization requirements:** Needs: a machine-tool industry base and a standard-setting body or adoption; establishment by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: industry goods and an institutions system; ADRs. Content: optionally drop the duplicate 'standard threads' capability string.

#### `inst.statistics_vital`

- **Identifier:** `inst.statistics_vital` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.statistics_vital` `.requires`).
- **Current baseline status:** Not baseline. Requires census (civic) AND calculus (A7 d13, natural_science subtree); heuristic closure 20 nodes / 69,580 RP.
- **Related research node(s):** `census`, `calculus`
- **Realization requirements:** Needs: a census institution present, statisticians, and adoption. Any effect needs a Health or demographic-policy channel.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an institutions system (CR-010; ADR).

#### `inst.mass_schooling`

- **Identifier:** `inst.mass_schooling` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.mass_schooling` `.requires`). There is no literacy or education variable (Variables.cs:36-93), and literacy is not modelled (civilization-progression-architecture.md:190).
- **Current baseline status:** Not baseline. Requires printing_press AND newspaper (an institution atom, read as printing_press) AND census (civic); heuristic closure 26 nodes / 105,830 RP. It was unreachable at 6baafbe through newspaper's unresolved postal_imperial atom.
- **Related research node(s):** `printing_press`, `census`
- **Realization requirements:** Needs: a newspaper and a census present, teachers, schools (a building.school host), a state budget, and adoption by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: an education/literacy model and an institutions system; ADRs.

#### `inst.mass_media_broadcast`

- **Identifier:** `inst.mass_media_broadcast` (institution)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `inst.mass_media_broadcast` `.requires`). There is no media model: the D-041 C1 references in radio and television (`research.json` `radio` `.unlocks.capabilities`, `research.json` `television` `.unlocks.capabilities`) have no carrier.
- **Current baseline status:** Not baseline. Requires radio (A9 d17) AND television (A9 d23) AND mass_schooling (an institution atom); heuristic closure 82 nodes / 1,031,610 RP.
- **Related research node(s):** `radio`, `television`, `printing_press`, `census`
- **Realization requirements:** Needs: mass_schooling present, broadcast infrastructure, electricity and an audience; adoption by an institutions system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: media, electricity and institutions systems; ADRs.

### 7.9 Projects (registry)

#### `project.orbital_launch`

- **Identifier:** `project.orbital_launch` (project)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `project.orbital_launch` `.requires`). ConstructionSystem knows only the granary and workshop projects (Sim.Data/content/goods.json:179-215). The project schema has no requires field (Sim.Core/Systems/GoodsConfig.cs:81-85), and enqueueing has no availability predicate (Sim.Core/Systems/Construction/ConstructionSystem.cs:92-97; ADR-028:57).
- **Current baseline status:** Not baseline. Requires rocket (A9 d24, engineering subtree; 'satellite launch', `research.json` `rocket` `.unlocks.capabilities`); heuristic closure 77 nodes / 1,611,310 RP.
- **Related research node(s):** `rocket`
- **Realization requirements:** Needs: a launch site (building.spaceport, `research.json` `building.spaceport` `.requires`, as a hierarchy dependency); fuel, aluminium and electronics goods (none of the 14 goods); electricity capacity (ADR-019:163); and a project system with a labour and material ledger.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a large-project carrier (ConstructionSystem projects with a requirement or availability predicate, or a dedicated system); goods; electricity. ADR, because the project schema is a contract.

#### `project.probe_acceleration`

- **Identifier:** `project.probe_acceleration` (project)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `project.probe_acceleration` `.requires`). There is no project carrier beyond the granary and workshop (goods.json:179-215; GoodsConfig.cs:81-85).
- **Current baseline status:** Not baseline. Requires laser (A9 d23) AND space_solar (A9 d28, in researchSets.speculative_finite, `research.json` `researchSets`ff); heuristic closure 119 nodes / 4,807,680 RP.
- **Related research node(s):** `laser`, `space_solar`
- **Realization requirements:** Needs: orbital capability realized (project.orbital_launch, as a hierarchy dependency), space solar generation, and very large material, energy and labour inputs through a project system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a project carrier with prerequisites and goods; ADR.

#### `project.interstellar_probe`

- **Identifier:** `project.interstellar_probe` (project)
- **Ownership classification:** **C REALIZATION**
- **Current simulation source:** Registry only — no simulation system realizes it yet (`research.json` `project.interstellar_probe` `.requires`). There is no project carrier beyond the granary and workshop (goods.json:179-215; GoodsConfig.cs:81-85).
- **Current baseline status:** Not baseline. Requires fusion_propulsion (A9 d27, speculative_finite; 'interstellar probe', `research.json` `fusion_propulsion` `.unlocks.capabilities`); heuristic closure 93 nodes / 4,238,520 RP.
- **Related research node(s):** `fusion_propulsion`, `fusion_power`, `space_probe`
- **Realization requirements:** Needs: orbital launch capability present, fusion propulsion hardware, and very large material, energy and labour inputs through a project system.
- **Exists at founding:** no
- **Initial existence (capability / object / both):** none
- **Required code/data changes:** Code: a project carrier with prerequisites and goods; ADR.

## 8. Verification record

Each finding of the contradictions checker and the completeness checker was given to an adversarial verifier in its
own worktree pinned to `d4ec6cd`, told to default to REFUTED when the evidence does not hold. Only CONFIRMED and
PLAUSIBLE findings changed the matrix (PLAUSIBLE with the verifier's adjusted correction).

| finding | checker | verdict | what changed |
|---|---|---|---|
| stock.dwellings | contradictions | **CONFIRMED** | `stock.dwellings` C → A; existence and growth conditions separated |
| baseline.exploration (and baseline.settlement_founding) | contradictions | **PLAUSIBLE** | caravel, polynesian_canoe added; raft/caravel/canoe strings recorded as non-gating |
| baseline.basic_military (with unit.spearmen) | contradictions | **PLAUSIBLE** | equipment constraint on the founding unit (no node-owned equipment); knapping_oldowan added |
| recipe.bronze_casting, recipe.toolmaking | contradictions | **CONFIRMED** | initial existence none → capability on both recipes |
| activity.coastal_shipping, activity.ocean_shipping (vs baseline.exploration) | contradictions | **PLAUSIBLE** | shipping rows and `baseline.exploration`: wiring recorded as BLOCKED on D-044 T3 with the Director's options; the issue's default "null it" not adopted |
| infra.road_track (also infra.road_built, infra.road_paved, infra.road_macadam, infra.railway, infra.bridge, infra.navigation_canal) | contradictions | **PLAUSIBLE** | seven edge-tier rows: eligibility reads removed, BLOCKED on D-044 T3 added; the issue's "knowledge term inside conditions" not adopted |
| recipe.pottery_firing, good.pottery, recipe.bronze_casting, good.bronze | contradictions | **REFUTED** | nothing: the graph does not route pottery or bronze through fire_making (reachability with fire_making excluded still reaches them) |
| coercion.appropriation | contradictions | **PLAUSIBLE** | cavalry added; "steppe raiding" recorded as non-owning, non-gating |
| MISSING:population.vital_rates_and_food_state | completeness | **PLAUSIBLE** | one row `population.vital_rates` added (A), not three |
| MISSING:param.grain_spoilage | completeness | **PLAUSIBLE** | no new row: `param.granary_capacity` already carries spoilage; refrigeration and canning added to it |
| MISSING:production_coefficients_and_farm_tools | completeness | **REFUTED** | nothing: the coefficients and the tool mechanism are already in the sector, good and recipe rows |
| MISSING:transport.overland_unimproved | completeness | **PLAUSIBLE** | row `transport.overland_unimproved` added (A); the issue's optional "transport.sea = B" not adopted (it would contradict D-040 B3) |
| matrix-wide citations labelled '@HEAD' / 'HEAD:' | contradictions | **PLAUSIBLE** | content citations re-anchored by id (stable on every revision); `@HEAD` code citations mapped line-exact from 6baafbe to d4ec6cd; three moved unlabelled citations fixed by hand |
| MISSING:research.engine | completeness | **PLAUSIBLE** | row `research.engine` added (A); no separate seam rows (the issue's "Eurekas can fire at founding" was false) |
| MISSING:needs_and_grievance | completeness | **REFUTED** | nothing: demand-side state that intersects no research content (outside G3) |
| MISSING:settlement.happiness | completeness | **REFUTED** | nothing: a derived reading, not a capability, building, unit or activity; already referenced by `control.revolt` |
| MISSING:env.harvest_weather_and_disaster | completeness | **REFUTED** | nothing: exogenous drivers, not capabilities (outside G3); the mitigation items already have rows |
| DUPLICATE:project.granary~building.granary | completeness | **PLAUSIBLE** | `project.granary` merged into `building.granary`, which stays C (the issue's A not adopted) |
| DUPLICATE:project.workshop~building.workshop | completeness | **PLAUSIBLE** | `project.workshop` merged into `building.workshop`, which stays C; the bronze question recorded as a Director decision |
| DUPLICATE:baseline.settlement_founding~settlement.colony | completeness | **REFUTED** | nothing: a capability (A) and the world object it realizes (C) are not duplicates |
| DUPLICATE:activity.*~sector.* | completeness | **PLAUSIBLE** | cross-references added; all eight rows stay A; activity ids moved out of the related-node lists |
| DUPLICATE:baseline.food_gathering~sector rows | completeness | **PLAUSIBLE** | `baseline.food_gathering` made the parent of the three sector rows (kept: it is a ruled baseline item); providedBy correction listed |
| OVERLAP:baseline.construction~sector.construction | completeness | **PLAUSIBLE** | capacity described once in `sector.construction`; `baseline.construction` keeps capacity → allocation → completion by reference |
| PROVENANCE:baseline.* rows | completeness | **PLAUSIBLE** | baseline rows cite the shipped `research.json` baseline list; "the content must carry the list" not listed (already true and enforced) |

