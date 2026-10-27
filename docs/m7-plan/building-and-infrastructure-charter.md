# Building and Infrastructure Charter (A1–A9 and endgame)

**Status: PROPOSED charter, for the Director's review.** This is a documentation and design file only.

**Nothing in this file changes:**
- runtime code, content data (`goods.json` projects, `research.json` entities, `sim.json`), entity ids;
- the trees, defaults, the save schema or tests;
- `main`, tags or releases.

Every quantity is a provisional band or a named dependency, never a tuned number.

**Companion to:** the [M7 Unit Charter](unit-catalogue-and-progression.md). That file is authoritative for:
- the status classes (§4.1);
- the seven end-to-end chains (§12);
- the dependency matrix (§13);
- the staged sequence (§14);
- balance and validation (§15).

They are referenced here, not duplicated. This file adds:
- the building, improvement, network, strategic-asset and wonder families;
- the detailed models behind the electricity, oil and nuclear chains;
- a status register for every building, infrastructure, project and institution entity in `research.json`.

**Verified base:** branch `claude/new-session-yavghm`, head `b769933`.

---

## Contents

1. [Scope and the five kinds of built thing](#1-scope-and-the-five-kinds-of-built-thing)
2. [Binding constraints](#2-binding-constraints)
3. [What exists today (verified)](#3-what-exists-today-verified-at-b769933)
4. [Shared building model](#4-shared-building-model)
5. [Building, improvement and strategic families](#5-building-improvement-and-strategic-families)
6. [Networks](#6-networks)
7. [Chain models: electricity, oil, nuclear](#7-chain-models-electricity-oil-nuclear)
8. [Wonders and megaprojects](#8-wonders-and-megaprojects)
9. [Entity status register](#9-entity-status-register)
10. [Building-specific validation](#10-building-specific-validation)
11. [Space and orbital assets](#11-space-and-orbital-assets)
12. [Open questions](#12-open-questions)

---

## 1. Scope and the five kinds of built thing

The model separates five kinds, because they behave differently in the simulation:

| Kind | What it is | Where it lives | Example |
|---|---|---|---|
| **Building** | A persistent structure in a settlement (one settlement per place; districts are abstracted, D-009 / D-039 D2) | settlement `Structures` | granary, university, theatre |
| **Territory improvement** | A structure at a site in a settlement's hinterland, bound to a deposit or terrain | hinterland site rows (new) | quarry, mine, irrigation, offshore rig, fort |
| **Network** | Nodes and edges that connect endpoints and carry a flow or a service | network graph tables | roads, rail, power lines, pipelines, telegraph, fibre |
| **Strategic asset** | A site whose purpose is strategic (launch, deterrence, detection) or that holds a stockpile | strategic tables (M10) | silo, launch complex, radar site, warhead stockpile |
| **Wonder / megaproject** | A scarce, very large project with one specific, bounded service effect | construction + a scarcity rule | grand canal, great library, space station |

**Rules that follow from the kinds:**
- A power line is not a building that produces electricity: it is a network edge.
- An offshore rig is not a city building: it is an extraction improvement at an offshore deposit.
- A nuclear silo is not a nuclear power plant. One is a strategic asset; the other is a generation building.

---

## 2. Binding constraints

| Source | What it fixes for buildings |
|---|---|
| CLAUDE.md law 2 (mechanisms over modifiers) | Every effect is a term inside an existing resolution equation and has a **live consumer**. Free-floating "+X %" buildings are banned. Precedent: the granary's two bounded, saturating effects (`sim.json granaries`, D-053 §3). |
| Law 1 (conservation) | Construction materials, maintenance and operating inputs move through `Ledger.Flow`. Staff are people in buckets. Outputs that are goods are produced by recipes, never created. |
| Law 3 (dt) | Maintenance, operating inputs, outputs and decay are rates per sim-year × dtYears |
| D-039 D1 | **Fortification is computed from built works,** size and terrain. It is not a unit or a flag. |
| D-040 C6 | **Coverage and reach are travel cost over the network,** not Euclidean radius |
| D-053, D-054, D-055 | Construction draws on the empire stockpile. It is built a little each turn as builders and materials allow, with stored progress and no timer. It uses deterministic reservations. |
| ADR-028 / ADR-033 D6 | Institutions follow the founding → maturity lifecycle. Universities are the realized precedent. |
| D-062 R7, R9 | Some deposits are concealed until prospected. Technologies may improve discovery. |
| D-062 R10 | No nuclear mechanics in M6 |
| TC-1 (PROPOSED) | No new research nodes or entities. New building types proposed here that have no `research.json` entity (substation, pipeline, offshore rig, silo) are **gated by existing node expressions in their own content** when they are realized, so the tree hash does not move. |

---

## 3. What exists today (verified at `b769933`)

| Item | Status | Evidence |
|---|---|---|
| Granary (project 1) | **existing.** Two bounded effects: a local farm bonus and an empire famine-intensity reduction. | `goods.json projects`; `sim.json granaries`; D-053 §3 |
| Workshop (project 2) | **existing** (constructible) | `goods.json projects` |
| University × 5 types (projects 11–15) | **existing.** Builds `building.university` and founds `inst.university`, with maturity. | ADR-033 D6; `docs/institutions-universities.md` |
| Road classes: dirt path (baseline) + `infra.road_track`, `infra.road_built`, `infra.road_paved`, `infra.road_macadam`, `infra.road_highway` | **existing** (RoadDevelopmentSystem; knowledge-gated) | `sim.json roads.classes`; audit class E |
| Incremental construction, empire stockpile, reservations | **existing** | D-054, D-053, D-055 |
| 43 `building.*`, 25 `infra.*`, 3 `project.*`, 28 `inst.*` entities | **Declared.** All except the above are DEFERRED: knowledge eligibility is shown, and nothing realizes them. | `research.json`; `docs/research-gameplay-unlock-audit.md` §4 |
| Recipes | 4: pottery firing, weaving, bronze casting, toolmaking | `goods.json` |

---

## 4. Shared building model

### 4.1 Lifecycle

`queued → under construction → operating → (degraded ↔ operating) → damaged → (repaired | destroyed) → decommissioned`

- **Under construction** uses the existing `ConstructionProgress` (D-054): materials and labour go in as available, and
  what has gone in is kept.
- **Degraded** is the state under unpaid maintenance or missing operating inputs. The service falls along the bounded
  curve.
- **Damaged** comes from battle, siege, sabotage or disaster. Integrity is below threshold.
- **Decommissioned** is an explicit order. A share of the materials is recoverable (a Ledger flow back to the stock).
  Nuclear decommissioning has its own chain step (§7.3).

### 4.2 Siting predicates

Siting is a pure predicate over published state. The same predicate is used by the simulation, the UI and the AI.

| Predicate kind | Examples |
|---|---|
| Settlement attribute | size tier (T3.8), population, existing structures (prerequisite buildings) |
| Hinterland feature | deposit present and **discovered** (D-062), river, coast, slope, elevation, moisture channels (`goods.json` `depositChannel`) |
| Network | connected to a road class / rail / grid / pipeline node |
| Separation | not within travel cost X of another instance (wonders, launch sites) |
| Political | inside controlled territory (claim vs control, D-037 A3); cross-border edges need an M8 agreement |

### 4.3 Construction, maintenance and staffing

- **Construction:** a material list from existing goods (or blocked goods named), plus labour in adult-years. This is
  the shape of today's projects.
- **Maintenance:** a rate per sim-year of materials (timber/stone/tools wear), drawn from the empire stockpile.
- **Staffing:** adults allocated through LabourActivities to the building's sector. Unstaffed means no service.
- **Operating inputs:** the goods a building consumes to deliver its service. A kiln consumes timber; a power station
  consumes coal, which is blocked.

### 4.4 Outputs, services and the consumer rule

Every output names the **system and equation that consumes it**. If none exists, the building is not realizable and its
status says why. Three shapes of output:
- **goods** — through a recipe;
- **a service term** — inside an equation (famine intensity, RP, grievance, supply throughput, command latency);
- **a capacity** — recruitment slots, venue seats, berths, sorties.

### 4.5 Coverage and graph connectivity

- **Coverage** is reach by travel cost from the building's settlement over the network (D-040 C6), with a saturating
  falloff. Precedent: `governance.authorityDecayCostUnits`.
- **Networks** (§6) are node/edge tables with capacity, and a per-edge loss or cost where the flow is physical.
- **Flow solving is deterministic:** sorted node ids, integer tie-breaks, fixed iteration order, and an exact flow
  identity per step (§10).

### 4.6 Upgrades, replacement, damage and repair

- **Replacement, not paid upgrade.** A higher tier (castle replacing walls, steelworks replacing a smithy's role) is a
  new construction project at the same site. It may reuse a declared share of the old structure's materials (a Ledger
  credit). Unit modernization is ruled free (ADR-031). **Building upgrades are not covered by that ruling and remain
  ordinary construction** (question B1).
- **Damage.** Integrity is reduced by:
  - siege bombard (the unit charter's siege family);
  - sabotage (unit charter §10.3);
  - disaster (the existing Disaster system);
  - neglect (degradation).
- **Repair.** A repair project restores integrity with materials ∝ damage. It is atomic and reserved like construction.
- **Destruction.** It removes the structure. Recoverable materials go to the stock. Networks lose the edge or node, and
  the flows re-solve next step.

### 4.7 AI placement (shared rules)

- **Same predicate.** The AI uses the same siting predicate and the existing construction order (`AiConstructionPolicy`
  pattern).
- **Deterministic choice.** Candidates are scored by the service gap the building closes (for example food security
  for granaries, measured supply shortfall for depots) and chosen by `(score, site id)`, with a tie-dense test.
- **No privileged reads.** Hidden deposits are not used until discovered.

### 4.8 Educational descriptions

Each card must state:
- what the structure was;
- when and where it emerged;
- its materials and labour;
- the service it provides **in this simulation**;
- what is "not simulated yet" (the M6 card convention).

A blocked building names the good or system it waits for.

---

## 5. Building, improvement and strategic families

**How each sheet reads.**
- Every sheet uses the same twelve fields.
- Statuses use the unit charter's classes (§4.1).
- Knowledge gates are existing ids.
- "Owner" is the system/milestone that must own the mechanic (unit charter §13).

### 5.1 Housing and settlement

| Field | Content |
|---|---|
| Status | **existing** (Housing system; `dwellings` fact kind) |
| Entities | — (Housing system); `building.skyscraper` (high-rise, **blocked**: steel) |
| Progression | huts and shelters (`baseline.basic_shelter`) → timber halls (`timber_frame`) → mudbrick and fired brick (`mudbrick`, `fired_brick`) → dense masonry (`vault_dome`, `concrete_pozzolan`) → high-rise (`skyscraper`, `reinforced_concrete`) |
| Siting | inside a settlement |
| Construction | timber, stone, clay, pottery (tiles); steel and cement for high-rise (**missing**) |
| Maintenance | timber/stone wear |
| Operating inputs | — |
| Outputs / services | housing capacity (the existing Housing equation) |
| Coverage | the settlement |
| Upgrades / replacement | higher density replaces lower: same site, partial material reuse |
| Damage / repair | disaster, siege, fire; repair project |
| AI placement | existing housing logic |

### 5.2 Food storage

| Field | Content |
|---|---|
| Status | Granary **existing**. Warehouses: **design-only** (likely owner M8). Cold chain: **blocked** (energy for refrigeration). |
| Entities | `building.granary` |
| Progression | communal storage (`communal_storage`) → granary (`grain_storage`) → warehouses (design-only) → cold chain (blocked: electricity) |
| Siting | settlement; farmland hinterland |
| Construction | timber 40, stone 20, labour 2.0 (shipped project values; TUNE) |
| Maintenance | none modelled today (a proposal for M10: timber wear) |
| Operating inputs | — |
| Outputs / services | local farm bonus; empire famine-intensity reduction (existing, bounded) |
| Coverage | local + empire (existing) |
| Upgrades / replacement | warehouse tier as a separate project (design-only) |
| Damage / repair | siege target: starvation path (D-039 D5) |
| AI placement | existing `AiConstructionPolicy` |

### 5.3 Water and sanitation

| Field | Content |
|---|---|
| Status | **M8+** (M9 Society: health/disease is the consumer) |
| Entities | `infra.well`, `infra.aqueduct`, `infra.urban_drainage`, `infra.sewer`, `infra.sewerage_system`, `building.bathhouse`; `building.desalination_plant` (home §5.18, blocked: energy) |
| Progression | wells (`well`) → drains (`fired_brick`) → aqueducts (`aqueduct_channel`, `aqueduct_tunnel`, `aqueduct_arcade`) → sewers (`vault_dome` AND `fired_brick`) → sanitary water systems (`sanitary_engineering` OR (`germ_theory` AND `reinforced_concrete`); `bleach_chlorine`) → desalination |
| Siting | wells: groundwater (moisture channel). Aqueducts: a source + a gradient path (a network edge from source to settlement). Desalination: coast + grid. |
| Construction | stone, clay, timber, labour (large for aqueducts); cement and steel (late, **missing**) |
| Maintenance | stone/clay wear; aqueducts silt (a rate) |
| Operating inputs | — (desalination: electricity, **blocked**) |
| Outputs / services | water supply and sanitation terms in the M9 disease and growth equations (`deferred-entity-realization-plan` A: water-supply term) |
| Coverage | the settlement; aqueduct = an edge from source to settlement |
| Upgrades / replacement | well → aqueduct → piped systems |
| Damage / repair | siege cuts aqueducts (a starvation/thirst hook) |
| AI placement | where disease or growth pressure is highest |

### 5.4 Health

| Field | Content |
|---|---|
| Status | **M8+** (M9), except `inst.military_medicine`: **M7 candidate** (home: unit charter §7.12; register §9.4). |
| Entities | `building.hospital`, `inst.hospital`, `inst.quarantine`, `inst.military_medicine`, `inst.statistics_vital` |
| Progression | healers' houses (`medicine_recorded`) → hospitals (`medicine_hippocratic` AND `pharmacology`; `public_hospitals`) → quarantine (`inst.quarantine`) → modern hospitals and public-health institutes (`germ_theory`, `vaccination`, `medical_imaging`) |
| Siting | settlement size tier |
| Construction | stone, timber, cloth; equipment (late, **missing**: electronics) |
| Maintenance | cloth, pottery |
| Operating inputs | staff (skilled share), cloth |
| Outputs / services | mortality and disease terms (M9) |
| Coverage | travel-cost reach |
| Upgrades / replacement | tiered replacement |
| Damage / repair | standard |
| AI placement | highest disease burden |

### 5.5 Education and research

| Field | Content |
|---|---|
| Status | University: **existing**. School, library, observatory, research laboratory: **design-only** (likely owner: Research system, an M6 follow-up or M10. Needs an RP mechanism, not a flat bonus: deferred plan A). Printing house: **blocked** (no paper good). |
| Entities | `building.school`, `building.library`, `building.university`, `building.observatory`, `building.research_laboratory`, `building.printing_house`; institutions `inst.scribal_school`, `inst.archive`, `inst.library`, `inst.university`, `inst.scientific_society`, `inst.mass_schooling`, `inst.translation_movement`, `inst.natural_philosophy`, `inst.historiography` |
| Progression | scribal schools (writing systems) → libraries (`papyrus` OR `paper` …) → universities (existing) → observatories (`astronomy_geometric` OR `astrolabe` OR `telescope`) → laboratories (`chemistry_quantitative`) → national laboratories (`national_laboratories`, cA9) |
| Siting | settlement size tier; universities by the existing viability rule |
| Construction | stone, timber (the university: 1800 timber, 1200 stone, 240 adult-years, shipped TUNE values) |
| Maintenance | timber/stone wear; cloth and pottery (scribal materials as a proxy are **not** proposed: no paper good exists) |
| Operating inputs | staff |
| Outputs / services | research-stage and RP terms (existing for universities); special-people recruitment points (model A: unit charter §8.7, §9) |
| Coverage | polity-wide for knowledge (D-043 B4: knowledge is polity-owned) |
| Upgrades / replacement | tiered |
| Damage / repair | standard. Knowledge is never lost with the building (D-048). |
| AI placement | existing university policy |

### 5.6 Administration and finance

| Field | Content |
|---|---|
| Status | **M8+** (M8 Politics; the M5 governance loop is the consumer for reach and tax), except mints: `building.mint` and `inst.mint_institution` are **blocked** (no coin good). |
| Entities | `building.mint`, `inst.archive`, `inst.law_code`, `inst.legal_code_roman`, `inst.census`, `inst.mint_institution`, `inst.coined_wage`, `inst.aramaic_admin`, `inst.central_bank`, `inst.joint_stock`, `inst.patent`, `inst.standard_threads` |
| Progression | council places (`kin_council`) → archives (`royal_archives`) → courts (`jury_courts`, `appellate_courts`) → mints (`coinage_electrum`) → ministries / civil service (`appointed_officials`) → banks and exchanges (`banking_houses`, `stock_exchange`, `central_banking`) → digital public services (blocked: networks) |
| Siting | capital or regional seats |
| Construction | stone, timber |
| Maintenance | wear |
| Operating inputs | staff; mints consume bronze (coinage good: none exists, so mints are **blocked** for coin output) |
| Outputs / services | administrative reach (`governance.authorityDecayCostUnits` term), tax collection efficiency (`inst.census`: deferred plan A) |
| Coverage | travel-cost reach from the seat |
| Upgrades / replacement | tiered |
| Damage / repair | standard; capture by an enemy transfers control (D-037) |
| AI placement | the seat of the largest reach gap |

### 5.7 Craft and light production

| Field | Content |
|---|---|
| Status | Workshop: **existing**. Kiln, smithy: **design-only** (Production system: a capacity/efficiency term on the existing recipes; shortest chain, deferred plan A). Water, wind, fulling and textile mills: **design-only**. Paper mill and oil press: **blocked** (no paper good; no vegetable-oil good). The textile mill's power-loom variant is **blocked** (coal/steam). |
| Entities | `building.workshop`, `building.kiln`, `building.smithy`, `building.water_mill`, `building.windmill`, `building.fulling_mill`, `building.paper_mill`, `building.oil_press`, `building.textile_mill`; `inst.textile_workshop`, `inst.guild` (`building.printing_house`: home §5.5) |
| Progression | workshops → kilns (`kiln_updraft`) → smithies (`copper_smelting` OR `iron_bloomery`) → water and wind mills (`water_mill`, `windmill_post`, `windmill_vertical`) → guildhalls (`craft_guilds`) → machine shops (`interchangeable_parts`) → factories (blocked: energy) |
| Siting | kiln: clay and timber in the hinterland. Water mill: river. Windmill: exposure (no wind channel exists; **design question B3**). |
| Construction | stone, timber, tools |
| Maintenance | tools wear |
| Operating inputs | the recipe inputs (existing) |
| Outputs / services | recipe throughput and labour-efficiency terms on existing recipes (pottery firing, bronze casting, toolmaking, weaving) |
| Coverage | the settlement |
| Upgrades / replacement | kiln → pottery works; smithy → foundry (bronze) → blast furnace (blocked) |
| Damage / repair | standard |
| AI placement | where the recipe's labour share is highest |

### 5.8 Heavy industry

| Field | Content |
|---|---|
| Status | **blocked** (iron ore, iron, steel, coal/coke, limestone/cement, sulphur, chemicals) |
| Entities | `building.blast_furnace`, `building.steelworks`, `building.cement_works`, `building.chemical_works` (`building.semiconductor_fab`: home §5.25) |
| Progression | bloomery (`iron_bloomery`) → blast furnace (`blast_furnace_water` OR `cast_iron`) → coke iron (`coke`) → steelworks (`bessemer` OR `open_hearth`) → cement (`portland_cement`) → chemical works (`sulphuric_acid`) → motor works (vehicles, blocked: fuel) |
| Siting | ore + fuel deposits, or rail/water connection to them; water |
| Construction | stone, timber, bricks; refractory (no good) |
| Maintenance | high |
| Operating inputs | ore, coal/coke, flux (all **missing**: EC-1) |
| Outputs / services | iron and steel goods (EC-1 recipes) |
| Coverage | the settlement; output enters the stockpile |
| Upgrades / replacement | furnace generations |
| Damage / repair | strategic bombing target (A9) |
| AI placement | the deposit pair with the lowest transport cost |

### 5.9 Extraction improvements

| Field | Content |
|---|---|
| Status | Extraction labour: **existing** (LabourActivities; deposits; concealed ores, D-062). Improvements as built structures: **design-only**. Coal, iron, oil, uranium mines: **blocked**. |
| Entities | `activity.mining`, `activity.logging` (**existing**: realized as LabourActivities labels in the extraction sector, unlock audit class B); `infra.*`: none. Quarry, clay pit, copper/tin mine, deep mine, coal mine, uranium mine (new). |
| Progression | surface gathering (baseline) → quarries and clay pits → shaft mines (`mining_shaft`) → pumped mines (`mine_pumping_engines`, blocked: steam/coal) → mechanized and remote extraction (blocked) |
| Siting | a **discovered** deposit of the good in the hinterland (D-062) |
| Construction | timber, stone, tools |
| Maintenance | timber (shoring), tools |
| Operating inputs | labour (existing); pumping fuel (blocked) |
| Outputs / services | raises the deposit's extraction rate (a term in the existing extraction equation); depletion as a rate |
| Coverage | one deposit site |
| Upgrades / replacement | depth tiers |
| Damage / repair | flooding/collapse (disaster hook), sabotage |
| AI placement | the highest-value discovered deposit by travel cost to its consumer |

### 5.10 Agricultural improvements

| Field | Content |
|---|---|
| Status | **design-only** (owner: Farming. `farming.preCultivation` was committed OFF in R2a; needs its ruling.) |
| Entities | `infra.basin_irrigation`, `infra.canal_irrigation`, `infra.terraces`, `infra.qanat`, `infra.fish_weir` |
| Progression | basin irrigation (`irrigation_basin`) → canals (`irrigation_canal`) → terraces (`terrace`) → qanats (`qanat`) → reservoirs (`reservoir_dam`) → drip irrigation (`drip_irrigation`) |
| Siting | river or groundwater; slope (terraces); arid moisture (qanat) |
| Construction | labour-heavy; stone, timber |
| Maintenance | silt and repair rates |
| Operating inputs | — |
| Outputs / services | farm yield and variance terms (existing farming / harvest-variance equations) |
| Coverage | the hinterland catchment (existing Catchment) |
| Upgrades / replacement | tiered |
| Damage / repair | flood/drought disasters; deliberate breaching in war |
| AI placement | the highest food-security gap |

### 5.11 Military production and training

| Field | Content |
|---|---|
| Status | Barracks, logistics depot, siege workshop: **M7 candidate** (M7.7, only with live consumers). Stables: **blocked** (mounts, EC-2). Foundry: bronze **M7 candidate**, iron **blocked** (EC-1). Arsenal/powder works: **blocked** (EC-3). Motor works: **blocked** (EC-4/EC-1). Military academy: **M7 candidate** as a command-capability term. |
| Entities | none in `research.json` except `building.dry_dock` (§5.13). Barracks, stables, siege workshop, arsenal, foundry, motor works, depot, academy are new types (content gated by existing nodes). |
| Progression | training ground (`standing_army`, cA4) → barracks → military academy (`military_academies`, cA7) → joint command (`general_staff`, cA8; `integrated_alliance_command`, cA9) |
| Siting | settlement; the depot sits on a road/rail node |
| Construction | stone, timber, tools |
| Maintenance | wear |
| Operating inputs | staff; the depot holds a supply stock (food, ammunition) |
| Outputs / services | recruitment location and capacity (unit charter §6.5 item 3); training → starting experience band; depot → supply throughput and reach (unit charter §6.7); academy → command latency / staff capacity term (D-039 A4) |
| Coverage | depot reach by travel cost |
| Upgrades / replacement | tiered |
| Damage / repair | raid and siege target |
| AI placement | the frontier settlement nearest the planned operation (key: travel cost, then id) |

**Minimum first set (carried over):**
- the founding warband needs no building;
- one land training building only if testing shows it is needed;
- a depot only when supply exists.

### 5.12 Fortifications

| Field | Content |
|---|---|
| Status | **M7 candidate** (D-039 D1 consumer: fortification term; M7.4) |
| Entities | `building.city_wall`, `building.castle`, `building.bastion_fort`; field works (built by engineers, unit charter §7.9); frontier forts (`frontier_fortresses`, cA3) |
| Progression | palisade (`timber_frame`) → walls (`mudbrick` OR `stone_dry`) → stone fortification (`stone_fortification`) → castles (`stone_fortification` AND `vault_dome`) → bastion forts (`trace_italienne`) → trench and concrete works (`reinforced_concrete`, blocked: cement) → hardened bases (blocked) |
| Siting | settlement perimeter (walls, castles, bastions); chokepoints and frontier (forts, which anchor ZOC) |
| Construction | timber, stone, labour (large); cement (late, **missing**) |
| Maintenance | stone/timber wear; garrison staffing |
| Operating inputs | garrison (formations), supplies |
| Outputs / services | the fortification term in siege difficulty (D-039 D3) and in the resolver; ZOC anchor (forts) |
| Coverage | the settlement or fort site |
| Upgrades / replacement | walls → castle → bastion (replacement at the site, partial material reuse) |
| Damage / repair | siege breach (bombard); repair project; slighting (deliberate destruction) |
| AI placement | frontier settlements facing the largest observed threat; the capital |

### 5.13 Ports and naval facilities

| Field | Content |
|---|---|
| Status | **M7 candidate** (M7.6, with water edges and the transport packet) |
| Entities | `building.harbour`, `building.lighthouse`, `building.dry_dock`; naval yard (new) |
| Progression | landing beaches (baseline craft) → harbour (`plank_boat` AND `stone_dry`) → lighthouse (`ashlar_masonry` OR `stone_fortification`) → naval yard (`state_navy`, cA4) → dry dock (`carrack`) → steam-era docks and coaling (`coaling_stations`, cA8, blocked: coal) → container port (`container_shipping`, blocked: fuel) |
| Siting | coast or navigable river node (water edge endpoint) |
| Construction | stone, timber, labour |
| Maintenance | dredging and timber |
| Operating inputs | staff |
| Outputs / services | port node: embark/disembark, naval recruitment (unit charter §7.13–7.15), trade edge capacity, repair of vessels (dry dock); lighthouse lowers the loss rate on coastal edges |
| Coverage | the water edges attached to the node |
| Upgrades / replacement | tiered |
| Damage / repair | blockade, raid, bombard |
| AI placement | coastal settlements with the largest seaborne-trade share |

### 5.14 Trade and markets

| Field | Content |
|---|---|
| Status | Trade flows **existing**. Market buildings: **design-only** (consumer: the existing TradeArbitrage). Caravanserais: **M8+** (convoys). |
| Entities | `activity.trade` (existing), `activity.caravans` (deferred); market, caravanserai, exchange (new) |
| Progression | barter places (`barter_exchange`) → markets and fairs (`market_fairs`, cA6) → caravanserais (`road_houses`, cA3; `camel_caravan`) → exchanges (`stock_exchange`, `commodity_exchange`) → digital marketplaces (blocked: networks) |
| Siting | road or water node |
| Construction | stone, timber |
| Maintenance | wear |
| Operating inputs | staff |
| Outputs / services | a trade friction term (TradeArbitrage) or convoy capacity (M8) |
| Coverage | its trade edges |
| Upgrades / replacement | tiered |
| Damage / repair | standard |
| AI placement | the busiest trade node |

### 5.15 Culture, religion and leisure venues

| Field | Content |
|---|---|
| Status | **M8+** (M9 Society: culture and influence) |
| Entities | `building.radio_transmitter` (blocked: electronics); `inst.mass_media_broadcast`, `inst.newspaper`; gathering place, temple, theatre, museum, opera house, concert hall, stadium, broadcast centre (new) |
| Progression | gathering places and burial (`inst.burial`, `megalithic_monuments`) → temples (`temple_economy`) → theatres (`public_theatre`, cA5) → opera houses (`public_opera`, cA7) and museums (`public_museums`, cA7) → stadiums (`professional_sport`, cA8) → broadcast centres (`radio_broadcasting`, `television`, blocked: electronics) |
| Siting | settlement size tier |
| Construction | stone, timber, cloth |
| Maintenance | wear |
| Operating inputs | staff |
| Outputs / services | **venue capacity** for cultural groups (unit charter §9.6); holding works (§9.3–9.5); culture term (M9) |
| Coverage | the settlement; broadcast reach (network) |
| Upgrades / replacement | tiered |
| Damage / repair | standard; riot risk at large venues (M8 hook) |
| AI placement | M9 policy |

### 5.16 Intelligence and security

| Field | Content |
|---|---|
| Status | **M8+** (M8 counterintelligence; M10 subversion) |
| Entities | police stations, intelligence bureaus, signals-intelligence sites, cyber-defence centres (new) |
| Progression | informant networks (no node: **Q6**) → police (`civil_police`, cA7) → intelligence bureaus (`military_intelligence`, cA9) → signals intelligence (`radio`, `computer`) → cyber defence (`cyber_warfare`, blocked: networks) |
| Siting | settlement |
| Construction | stone, timber |
| Maintenance | staff |
| Operating inputs | staff |
| Outputs / services | detection probability against covert agents (unit charter §10); security term in the agitation equation |
| Coverage | travel-cost reach |
| Upgrades / replacement | tiered |
| Damage / repair | sabotage |
| AI placement | settlements with observed covert activity or high grievance |

### 5.17 Detection (radar and sensor sites)

| Field | Content |
|---|---|
| Status | **blocked** (electronics; electricity) |
| Entities | radar site, sonar station, early-warning site (new) |
| Progression | signal beacons (`signal_beacons`, A3: visual warning only, design-only) → radar (`radar`) → early warning and missile defence (`missile_defence`) |
| Siting | elevation; coast for sonar |
| Construction | steel, electronics (**missing**) |
| Maintenance | electricity, staff |
| Operating inputs | power |
| Outputs / services | detection radius for air and missiles (the input to `air_defence`) |
| Coverage | radius by line of sight (elevation) |
| Upgrades / replacement | tiered |
| Damage / repair | strike target |
| AI placement | border and coast |

### 5.18 Water resources

| Field | Content |
|---|---|
| Status | Reservoirs: **design-only** (owner: Farming/Health). Hydro dams: **blocked** (energy system). Desalination: **blocked**. |
| Entities | `building.desalination_plant`; reservoir (new); `building.hydroelectric_dam` (home §5.20, the dam as generation) |
| Progression | reservoir dams (`reservoir_dam`, A3) → hydroelectric dams (`hydroelectric`, A9) |
| Siting | a river with a gradient; flooded land is lost (a hinterland change) |
| Construction | stone, labour (huge); cement and steel (late) |
| Maintenance | silt, inspection |
| Operating inputs | — |
| Outputs / services | water storage (irrigation and drought terms); generation (§7.1) |
| Coverage | the downstream catchment; a grid node |
| Upgrades / replacement | — |
| Damage / repair | failure → flood disaster |
| AI placement | the best gradient × demand site |

### 5.19 Airports and airfields

| Field | Content |
|---|---|
| Status | **blocked** (fuel, aluminium; air basing) |
| Entities | `building.airfield`; civil airport (new) |
| Progression | airfields (`aircraft`) → air bases → civil airports (`jet_airliner`, `air_navigation_agreements`) |
| Siting | flat land (slope), settlement or field site; road connection |
| Construction | cement, steel (**missing**) |
| Maintenance | fuel stock, staff |
| Operating inputs | fuel |
| Outputs / services | air-unit basing (sortie capacity); civil air edges (M11+) |
| Coverage | sortie radius |
| Upgrades / replacement | tiered |
| Damage / repair | strike target; runway repair |
| AI placement | the frontier within sortie range of targets |

### 5.20 Power generation

| Field | Content |
|---|---|
| Status | **blocked** (no energy system; coal and oil goods missing; copper metal missing for connections) |
| Entities | `building.power_station`, `building.hydroelectric_dam`, `building.nuclear_power_station`, `building.solar_farm`, `building.wind_farm`, `building.fusion_plant` |
| Progression | local mechanical power (mills) → thermal stations (`electricity_generation`; coal or oil) → hydro (`hydroelectric`) → gas turbines (`gas_turbine_power`) → nuclear (`nuclear_power`; reactor types `light_water_reactor`, `heavy_water_reactor`, `graphite_reactor`, `fast_breeder_reactor`) → solar and wind (`solar_pv`, `wind_turbine_modern`) → fusion (`fusion_power`) |
| Siting | thermal: fuel access + water for cooling; hydro: §5.18; nuclear: water + separation from large settlements (a safety predicate); solar/wind: no irradiance or wind channel exists (**B3**) |
| Construction | steel, cement, copper (**missing**) |
| Maintenance | staff (skilled), parts |
| Operating inputs | fuel by type (coal, oil, uranium: **missing**); none for hydro, solar, wind |
| Outputs / services | generation capacity into the grid (§7.1) |
| Coverage | its grid connection |
| Upgrades / replacement | replacement only; nuclear adds decommissioning (§7.3) |
| Damage / repair | strike or sabotage; accident (nuclear: §7.3) |
| AI placement | demand gap on the grid × fuel cost |

### 5.21 Oil and fuel infrastructure

| Field | Content |
|---|---|
| Status | **blocked** (EC-4: oil deposits, crude and fuel goods) |
| Entities | `building.refinery`; onshore well, offshore rig, tank farm / fuel depot (new); pipelines (§6.6) |
| Progression | percussion-drilled wells (`percussion_drilling`, A6) → oil wells (`petroleum_refining` era) → refineries (`petroleum_refining`) → offshore rigs (`geophysical_prospecting`, A9) |
| Siting | wells: discovered oil deposit. Offshore rig: discovered offshore deposit (needs a seabed deposit channel: **B4**). Refinery: pipeline/rail/water node. |
| Construction | steel (**missing**) |
| Maintenance | high; leaks |
| Operating inputs | crude (refinery) |
| Outputs / services | crude; refined fuels (recipes) |
| Coverage | pipeline graph |
| Upgrades / replacement | — |
| Damage / repair | sabotage, strike; spill disaster |
| AI placement | the largest discovered deposit |

### 5.22 Nuclear fuel cycle

| Field | Content |
|---|---|
| Status | **blocked** (EC-6) + M10 governance for enrichment levels |
| Entities | uranium mine and mill, enrichment plant, fuel fabrication, waste repository (new); `building.nuclear_power_station` (§5.20) |
| Progression | milling (`uranium_milling`) → enrichment (`uranium_enrichment`) → reactors (§5.20) → geological disposal (`geological_disposal`) |
| Siting | discovered uranium (`radiometric_prospecting`); repository: stable geology (no channel: **B4**) |
| Construction | steel, cement (**missing**) |
| Maintenance | high, skilled staff |
| Operating inputs | ore → concentrate → enriched fuel (recipes) |
| Outputs / services | reactor fuel; weapons-grade material (§7.3, unit charter §12.4) |
| Coverage | — |
| Upgrades / replacement | — |
| Damage / repair | contamination disaster |
| AI placement | M10 policy |

### 5.23 Strategic weapons and launch

| Field | Content |
|---|---|
| Status | **blocked** + **M10** (stockpile, authority, interception, consequences) |
| Entities | missile silo, road-mobile launcher garrison, missile-defence site, weapons laboratory (new); warhead stockpile (asset) |
| Progression | rocket launch sites (`rocket`) → silos (`icbm`) → missile defence (`missile_defence`) |
| Siting | separation from settlements; hardened terrain; inside controlled territory |
| Construction | steel, cement, electronics (**missing**) |
| Maintenance | high; staff |
| Operating inputs | missiles (unit charter §7.21), warheads (asset) |
| Outputs / services | launch capacity; deterrence posture (M8/M10) |
| Coverage | missile range |
| Upgrades / replacement | — |
| Damage / repair | pre-emption target |
| AI placement | M10 policy |

### 5.24 Space launch

Pointer to §11.

### 5.25 Digital infrastructure

| Field | Content |
|---|---|
| Status | **blocked** (electronics, energy; DN row in unit charter §13) |
| Entities | `building.semiconductor_fab`, `building.data_centre` |
| Progression | computers (`computer`) → fabs (`integrated_circuit`) → data centres (`cloud_computing`) |
| Siting | grid + backbone node |
| Construction | steel, electronics (**missing**) |
| Maintenance | electricity, staff |
| Operating inputs | power |
| Outputs / services | network services (§6.7); targets for cyber agents |
| Coverage | the network |
| Upgrades / replacement | generations |
| Damage / repair | cyber, power loss |
| AI placement | M11+ |

---

## 6. Networks

All networks share one model:
- nodes are settlements, sites, substations or junctions;
- edges carry a class, a capacity, a cost or loss per unit length, an integrity, and an owner;
- cross-border edges need an M8 agreement;
- flow and service are solved deterministically each step (§10).

| # | Network | Status | Entities | Edge semantics | Construction / maintenance | Upgrades | Damage / repair |
|---|---|---|---|---|---|---|---|
| 6.1 | Roads | **existing** (5 classes + dirt path) | `infra.road_track` … `infra.road_highway` | travel-cost factor per class (existing) | labour + stone/timber (existing RoadDevelopment) | class upgrades (existing) | war damage + repair (M7) |
| 6.2 | Bridges | **M7 candidate** (transport packet; frozen at 997824b until ruled) | `infra.bridge` | a river-crossing edge with a cost reduction; engineers can build temporary bridges (unit charter §7.9) | stone, timber | timber → stone (`bridge_stone`) | demolition is a war option |
| 6.3 | Navigation canals | **M7 candidate** (M7.6 water edges) | `infra.navigation_canal` | water edges between basins (`canal_lock`, `canal_navigation`) | labour (huge), stone | locks | breach |
| 6.4 | Railways | **blocked** (iron, steel, coal; EC-1) | `infra.railway` | high-capacity, low-cost freight edges; supply throughput (unit charter §7.11) | iron/steel rails (**missing**) | electrification (`electric_traction`; energy, blocked), high speed (`high_speed_rail`) | sabotage, partisans |
| 6.5 | Power transmission, substations, interconnection | **blocked** (energy system; copper metal, steel) | `infra.power_grid` | capacity and loss per length; substations link transmission and distribution | copper, steel (**missing**) | `smart_grid`, `grid_storage` | outage propagation (§7.1) |
| 6.6 | Pipelines | **blocked** (EC-4; steel) | new | crude and fuel flow; capacity; leak rate | steel (**missing**) | — | sabotage → spill disaster |
| 6.7 | Communications | courier relays **blocked** (mounts: `infra.courier_relay` requires `horse_domestication`); beacons and optical telegraph **design-only** (command-latency term, M7); telegraph, telephone, submarine cable, radio, mobile, internet **blocked** (copper metal, electronics, energy) | `infra.courier_relay`, `infra.telegraph_line`, `infra.telephone_network`, `infra.submarine_cable`, `infra.mobile_network`, `infra.internet_backbone`; `building.radio_transmitter` | **command latency** (D-039 A3) and information-channel terms between connected nodes | per class | relay → beacon (`signal_beacons`) → optical telegraph (`optical_telegraph`) → telegraph → telephone → radio → fibre (`fibre_optics`) → mobile (`mobile_phone`) → internet | line cuts; cyber |
| 6.8 | Water supply | **M8+** (M9) | `infra.aqueduct`, `infra.sewerage_system` | source → settlement edges | §5.3 | §5.3 | siege cut |

### 6.9 Field completion for networks

| Network | Siting | Operating inputs | Coverage / graph | AI placement |
|---|---|---|---|---|
| Roads (6.1) | existing RoadDevelopment predicates | maintenance labour (existing) | settlement-to-settlement edges (existing) | existing road AI |
| Bridges (6.2) | a river crossing on an existing or planned road edge | timber/stone wear | replaces a ford edge cost on that crossing | campaign routes and trade edges with the highest crossing cost, key `(cost, edge id)` |
| Navigation canals (6.3) | between two water bodies; slope limits lock count | dredging labour | new water edges | highest trade-volume pair, key `(volume, pair id)` |
| Railways (6.4) | along a graded corridor between nodes | fuel (coal, then electricity), staff | high-capacity edges | supply and trade bottlenecks |
| Power (6.5) | generator → substation → consumer corridors | losses (in the flow identity); maintenance | grid graph (§7.1) | unserved demand × line cost |
| Pipelines (6.6) | deposit → refinery → depot corridors | pumping energy | pipeline graph | discovered deposits by distance to a refinery |
| Communications (6.7) | node-to-node; submarine cables need coast | staff; energy from telegraph onward | latency/information edges | links from capital to frontier armies first (command latency) |
| Water supply (6.8) | source → settlement with a gradient | maintenance | source edges | highest disease/growth pressure (M9) |

**Work crews.** The unit charter's design-only field work crews (§8.3) are the agents that build network edges
outside any settlement's labour reach. They come with the first such network (rail or power), not before.

---

## 7. Chain models: electricity, oil, nuclear

The step tables are in the unit charter §12. This section specifies the **state each needs**, so the owning milestone
can design it.

### 7.1 Electricity grid model (owner: energy system, row EN)

**Why electricity is not a good.** Electricity is not storable as a stock, except through explicit storage sites.
Modelling it as a good would break both Law 1's meaning and the stockpile model. Instead it is a **per-step flow on a
graph**.

**State:**
- generator rows (site, type, capacity, fuel type, availability);
- transmission edges (from, to, capacity, loss per length, integrity, owner);
- substations (node; transformation capacity);
- consumer demand per building, by priority class;
- storage sites (capacity, charge as a `long` stock).

**Dispatch:**
- each step, deterministic merit order: running cost, then generator id;
- served demand is allocated by priority class, then consumer id;
- transmission limits are enforced on a fixed solve order (sorted edge ids), with no LP library nondeterminism.

**Identity (exact, integer milli-units):**

`generated = delivered + losses + curtailed + storage_charge − storage_discharge`

It is asserted every step, in the same shape as the CP balance identity (ADR-043).

**Outages:**
- unserved demand degrades the consuming building's service (the §4.1 degraded state);
- a damaged edge or node re-solves the next step.

**Fuel:** thermal generators consume coal, oil or uranium through `Ledger.Flow`, all blocked by EC packets.

**Interconnection:** cross-polity edges are M8 agreements, and the flow across them is a recorded trade.

### 7.2 Oil and fuel model (owner: EC-4 + Production + Transport)

| Area | Specification |
|---|---|
| Goods (proposed for EC-4, not added) | crude oil (raw, deposit channel: new); refined fuels — gasoline, diesel, fuel oil, aviation fuel — aggregated or split as the EC review decides |
| Deposits | onshore and offshore, partly concealed (D-062 R7 analogue) |
| Depletion | per deposit, as a rate |
| Recipes | refinery: crude → fuels, plus a byproduct share |
| Transport | pipelines (§6.6), rail, tankers (bulk on water edges) |
| Consumption | vehicles, ships, aircraft, generators. Military fuel demand is part of supply (unit charter §6.7). |
| Conservation tests | extraction = depletion; refinery mass balance; pipeline in = out + leak |

### 7.3 Civilian nuclear model (owner: EC-6 + energy system + M9 health; weapons in M10)

| Area | Specification |
|---|---|
| Fuel cycle | ore → concentrate → enriched fuel (enrichment level is state) → reactor → spent fuel (stock) → repository |
| Reactor | capacity, type (existing reactor nodes), cooling water predicate, staffing, safety-system level (a term in the accident hazard) |
| Accident | hazard rate × dtYears from a named RNG stream. Consequences through the existing Disaster system (casualties are real Ledger debits; land contamination is a hinterland state). |
| Decommissioning | an explicit project with cost; spent fuel must go to a repository |
| Weapons separation | enrichment beyond the civilian level is an M10 decision, with its own authority and detection (unit charter §12.4) |

---

## 8. Wonders and megaprojects

**Policy:**
- **Scarce.** One per world, or one per polity for megaprojects.
- **Huge.** Very large construction through the existing incremental construction.
- **One specific, bounded service effect** with a live consumer. No unexplained multiplier to everything (Law 2).
- **Contested.** A rival's partial progress is not refunded when another polity completes a one-per-world wonder; a
  declared share of materials is recoverable.
- **Status.** All are **design-only**, scheduled no earlier than M11+ (unit charter §14), except where noted.

**Fields common to every wonder** (the per-wonder table adds what differs):

| Field | Content |
|---|---|
| Construction | very large stone/timber/labour; late wonders need missing goods (named per row) |
| Maintenance | a wear rate per sim-year like any building; an unmaintained wonder degrades its one effect |
| Operating inputs | staff (a small skilled share) |
| Damage / repair | siege and disaster as for buildings; capture transfers the effect with control (D-037) |
| AI placement | a single candidate settlement chosen by `(service gap the effect closes, settlement id)`; started only when no war is planned |

| Wonder / megaproject | Gate (existing ids) | Era | Specific effect (consumer) | Siting | Coverage | Status |
|---|---|---|---|---|
| Megalithic monument | `megalithic_monuments` (cA2) | A2 | a cohesion / legitimacy term for the founding polity (M8) | stone deposit in the hinterland | polity | design-only |
| Temple complex | `temple_economy` (cA3) | A3 | a redistribution / storage term (granary-like, bounded) | settlement size tier | travel-cost reach | design-only |
| Frontier wall | `frontier_fortresses` (cA3), `stone_fortification` | A3–A5 | a ZOC line along a frontier edge set (M7 ZOC consumer) | along a controlled frontier | the wall's edge set | design-only (M7 consumer exists once ZOC lands) |
| Palace library | `palace_library` (cA4) | A4 | special-people recruitment points (model A) | the capital | polity | design-only |
| Grand canal | `canal_navigation` | A7 | a water edge between basins at scale (transport) | between two river basins | the canal's edges | design-only |
| Great observatory | `telescope` | A7 | a research term in one discipline subtree | elevation; settlement with a university | polity (knowledge is polity-owned) | design-only |
| Transcontinental railway | `railway` | A8 | a long rail edge set (blocked: EC-1) | a corridor between distant coasts | the edge set | blocked |
| Grand dam | `hydroelectric` | A9 | grid generation + water storage | river gradient | grid + catchment | blocked |
| Space station | `space_station` | A9 | §11 | orbit (launch site needed) | polity | blocked |
| Interstellar probe (existing project entity) | `project.interstellar_probe` (`fusion_propulsion`) | A9 | an endgame project (victory semantics unruled) | launch site | — | blocked |

---

## 9. Entity status register

Every `building.*`, `infra.*`, `project.*` and `inst.*` entity in `research.json` has exactly one status. Statuses use
the unit charter's classes:

| Code | Status |
|---|---|
| E | existing |
| M7 | M7 candidate |
| M8+ | M8+ (owner in brackets) |
| B | blocked by missing goods/system |
| D | design-only |

### 9.1 Buildings (43)

| Entity | Status | Owner / blocker |
|---|---|---|
| `building.granary` | E | project 1 |
| `building.workshop` | E | project 2 |
| `building.university` | E | projects 11–15 |
| `building.kiln` | D | Production (capacity term) |
| `building.smithy` | D | Production |
| `building.blast_furnace` | B | EC-1 |
| `building.water_mill` | D | Production (mechanical power) |
| `building.windmill` | D | Production; wind channel (B3) |
| `building.fulling_mill` | D | Production (CR-025 future-only node `fulling_mill`) |
| `building.paper_mill` | B | no paper good |
| `building.oil_press` | B | no vegetable-oil good |
| `building.textile_mill` | D | Production; power-loom variant B (EC-1 coal) |
| `building.steelworks` | B | EC-1 |
| `building.chemical_works` | B | sulphur and chemical goods |
| `building.cement_works` | B | limestone and fuel goods |
| `building.refinery` | B | EC-4 |
| `building.power_station` | B | energy system + fuel |
| `building.hydroelectric_dam` | B | energy system |
| `building.nuclear_power_station` | B | EC-6 + energy |
| `building.school` | D | Research (RP mechanism) |
| `building.library` | D | Research |
| `building.hospital` | M8+ | M9 health |
| `building.observatory` | D | Research |
| `building.research_laboratory` | D | Research |
| `building.printing_house` | B | no paper good (then Research / M9 culture) |
| `building.mint` | B | no coin good (then M8 finance) |
| `building.bathhouse` | M8+ | M9 health |
| `building.city_wall` | M7 | fortification term |
| `building.castle` | M7 | fortification term |
| `building.bastion_fort` | M7 | fortification term |
| `building.harbour` | M7 | M7.6 water edges |
| `building.lighthouse` | M7 | M7.6 (coastal loss term) |
| `building.dry_dock` | M7 | M7.6 (vessel repair) |
| `building.airfield` | B | EC-4 / EC-5; air basing |
| `building.radio_transmitter` | B | EC-5 + energy |
| `building.skyscraper` | B | EC-1 steel; cement |
| `building.spaceport` | B | §11 |
| `building.semiconductor_fab` | B | EC-5 + energy |
| `building.data_centre` | B | EC-5 + energy + DN |
| `building.desalination_plant` | B | energy |
| `building.solar_farm` | B | energy; irradiance channel (B3) |
| `building.wind_farm` | B | energy; wind channel (B3) |
| `building.fusion_plant` | B | energy (future-only) |

### 9.2 Infrastructure (25)

| Entity | Status | Owner / blocker |
|---|---|---|
| `infra.road_track` | E | RoadDevelopment |
| `infra.road_built` | E | RoadDevelopment |
| `infra.road_paved` | E | RoadDevelopment |
| `infra.road_macadam` | E | RoadDevelopment |
| `infra.road_highway` | E | RoadDevelopment |
| `infra.courier_relay` | B | EC-2 mounts; command latency (M7) when unblocked |
| `infra.bridge` | M7 | transport packet |
| `infra.basin_irrigation` | D | Farming (preCultivation ruling) |
| `infra.canal_irrigation` | D | Farming |
| `infra.terraces` | D | Farming |
| `infra.qanat` | D | Farming |
| `infra.well` | M8+ | M9 health / growth |
| `infra.aqueduct` | M8+ | M9 |
| `infra.urban_drainage` | M8+ | M9 |
| `infra.sewer` | M8+ | M9 |
| `infra.sewerage_system` | M8+ | M9 |
| `infra.fish_weir` | D | Fishing activity |
| `infra.navigation_canal` | M7 | M7.6 water edges |
| `infra.railway` | B | EC-1 |
| `infra.telegraph_line` | B | EC-5 (copper metal) |
| `infra.submarine_cable` | B | EC-5 |
| `infra.telephone_network` | B | EC-5 + energy |
| `infra.power_grid` | B | energy system |
| `infra.mobile_network` | B | EC-5 + energy + DN |
| `infra.internet_backbone` | B | EC-5 + energy + DN |

### 9.3 Projects (3)

| Entity | Status | Owner / blocker |
|---|---|---|
| `project.orbital_launch` | B | §11 |
| `project.probe_acceleration` | B | §11 |
| `project.interstellar_probe` | B | §11; endgame semantics unruled |

### 9.4 Institutions (28)

| Entity | Status | Owner / blocker |
|---|---|---|
| `inst.university` | E | ADR-033 D6 |
| `inst.burial` | M8+ | M9 (culture/religion) |
| `inst.scribal_school` | D | Research |
| `inst.archive` | D | Research / M8 administration |
| `inst.library` | D | Research |
| `inst.natural_philosophy` | D | Research |
| `inst.historiography` | D | Research |
| `inst.translation_movement` | D | Research |
| `inst.scientific_society` | M8+ | SP recruitment points (M9) |
| `inst.law_code` | M8+ | M8 |
| `inst.legal_code_roman` | M8+ | M8 |
| `inst.aramaic_admin` | M8+ | M8 |
| `inst.census` | M8+ | M8 (tax efficiency term) |
| `inst.mint_institution` | B | no coin good (then M8) |
| `inst.coined_wage` | M8+ | M8 |
| `inst.central_bank` | M8+ | M8 |
| `inst.joint_stock` | M8+ | M8 |
| `inst.patent` | M8+ | M8 |
| `inst.standard_threads` | D | Production |
| `inst.textile_workshop` | D | Production |
| `inst.guild` | D | Production / M9 |
| `inst.military_medicine` | M7 | field-medical family (unit charter §7.12) |
| `inst.hospital` | M8+ | M9 |
| `inst.quarantine` | M8+ | M9 |
| `inst.statistics_vital` | M8+ | M9 |
| `inst.mass_schooling` | M8+ | M9 |
| `inst.newspaper` | M8+ | M8 / M9 (opinion) |
| `inst.mass_media_broadcast` | M8+ | M9 (cultural groups gate) |

---

## 10. Building-specific validation

In addition to unit charter §15:

1. **Consumer rule.** For every realized building type, a test proves its effect is read by the named equation: a
   twin world without the building differs exactly through that term. The granary attribution precedent applies.
2. **Siting determinism.** The siting predicate is pure. The candidate order is `(score, site id)`. Tie-dense test.
3. **Network flow identity.** For every physical network (power, pipelines, later rail freight), an exact per-step
   identity in integer milli-units: in = out + loss (+ storage delta). Edge ids are processed in sorted order.
4. **Damage and repair conservation.** Destroyed materials are either recovered to the stock (Ledger) or recorded as
   destroyed. Repair consumes ∝ damage. Both are exact.
5. **Save/load.** Every new row type (sites, edges, generators, storage) gets populated-table tests: ExpectedLength,
   bit-exact round trip, hash equality, per-field sensitivity.
6. **Replay.** Construction, repair, decommission and edge-building orders get turn-exact delivery pins.
7. **No calendar gate.** No building availability reads an Age or a date. Only knowledge expressions, siting and
   materials.
8. **AI.** Placement is legal by construction, reads only discovered deposits, and is deterministic, with a four-seed
   measurement for each new policy.

---

## 11. Space and orbital assets

| Item | Status | Gate (existing ids) | State required | Notes |
|---|---|---|---|---|
| Launch facility (spaceport) | **blocked** (EC-4/EC-5; energy) | `building.spaceport` (`rocket`); `orbital_launch_vehicle`, `reusable_launch` | site (coastal or eastward-launch predicate: B3), launch capacity | launch vehicles are consumed per launch (project) |
| Orbital launch campaign | **blocked** | `project.orbital_launch` | launch campaign project | existing declared project entity |
| Satellites (asset, not unit) | **blocked** | `satellite`, `communications_satellite`, `earth_observation`, `gps` | orbital asset rows: function, lifetime decay rate | services: communications edges (§6.7), reconnaissance (VISIBLE over territory, ADR-044 consumer), navigation (movement-cost term) |
| Ground stations | **blocked** | `communications_satellite` | network nodes | link satellites to networks |
| Space station / probes | **blocked** | `space_station`, `space_probe`, `project.probe_acceleration`, `project.interstellar_probe` | megaprojects | endgame semantics unruled (§8) |
| Anti-satellite / space defence | **design-only** | none (no node) | — | no node exists; not proposed (TC-1) |

**Fields for space assets:**

| Field | Content |
|---|---|
| Construction | launch sites: steel, cement, electronics (**missing**). Satellites: built as a project and consumed into an orbital asset on launch. |
| Maintenance | launch site: staff and parts. Satellites: no maintenance; a lifetime decay rate per sim-year. |
| Operating inputs | launch fuel per launch (EC-4) |
| Coverage | satellite service footprint: the polity's territory for reconnaissance; network nodes for communications |
| Upgrades / replacement | satellites are replaced by new launches, never upgraded in orbit |
| Damage / repair | launch failure (a named RNG stream); ground stations as strike targets; no repair in orbit |
| AI placement | M11+ policy; the launch site goes on the coastal settlement with the best launch predicate, key `(predicate score, settlement id)` |

---

## 12. Open questions

1. **B1 — Building upgrades.** Confirm that building tier replacement is ordinary construction with partial material
   reuse. ADR-031's free modernization covers units only.
2. **B2 — Hinterland sites.** Improvements need per-site rows in a settlement's hinterland. Today deposits are
   per-settlement rows (ADR-044). Should improvements attach to deposit rows (smallest change) or to positioned sites
   (needed for offshore rigs and pipelines)?
3. **B3 — Missing environmental channels.** Wind, solar irradiance, launch latitude and stable geology have no
   worldgen channel. Each is a worldgen change, not a building change.
4. **B4 — Offshore and geological deposits.** Oil (onshore and offshore) and uranium need deposit channels. Part of EC-4
   and EC-6.
5. **B5 — Energy owner.** The same as unit charter Q8: M10 or M11+.
6. **B6 — Wonders.** Confirm the one-per-world vs one-per-polity split and the no-refund rule before any wonder is
   specified further.

Also see unit charter §16 for the conflicts list (C1–C10) and questions Q1–Q9. TC-1 remains PROPOSED; this charter adds
no node or entity.
