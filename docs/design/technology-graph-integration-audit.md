# TECHNOLOGY GRAPH INTEGRATION AUDIT

**Status: AUDIT ONLY. Design document class (`docs/design/`). Rules nothing.** It changes no code,
data, schema, frozen or ratified document, and it does not touch the technology graph. Where it
finds a conflict it records the conflict and names the owner; it resolves none.

**Written against:** `claude/civdemo-work-b1z2y4` @ `fae0add` ("Add files via upload", the
Director's commit adding the graph on top of `55aa15c`). This branch is LOCAL and REMOTE, **not
MAIN**. Every `file:line` below was read on that tree.

**Label key** (`docs/design/m4-closure-audit.md:32-37`). Every substantive conclusion carries
exactly one of these labels:
- **RATIFIED** — a ruling or frozen decision exists, cited `file:line`;
- **MEASURED** — a command or parse was run on this tree and its output is reported;
- **PROPOSED** — the auditor's suggestion, for the Director to ratify or reject;
- **INFERRED** — reasoned from mechanism or document, not directly stated;
- **DIRECTOR DECISION REQUIRED** — a genuine open choice, including anything that rests on an
  OPEN CR (`docs/design/ratified-label-audit.md:48-49`);
- **UNVERIFIED** — the claim could not be checked here; the reason is stated.

**Classification key** (the task's). Every graph node and manifestation reference is classed as:
- **A** — already supported by the repository;
- **B** — supported by existing architecture, but the substrate is not yet implemented;
- **C** — requires a new architecture decision;
- **D** — data or content only;
- **E** — UI or visualization only;
- **F** — unresolved, or conflicting with an existing ruling.

**The Director's architectural rules for this task.** They are recorded as given, and they rank
first under GOV-4 §1 (`docs/gov-4-repository-freshness.md:21-37`, "explicit director rulings in
the current task"):
- Technology completion is player-facing completion of the technology or knowledge. It may unlock
  downstream capabilities and applications. It is not merely "research activity".
- Researching a technology does not itself create a building. A building becomes AVAILABLE when
  its requirement predicate is satisfied; construction creates the built instance.
- There is no universal CapabilitySystem, and no universal technology system that owns every
  downstream consequence. Law 6 and state-mediated communication between systems are preserved.
- The bridge is: Technology → Capability → Application → Simulation system/state → Observable
  world state → Visualization. Not every technology needs every layer.

§11 records where these rules meet earlier rulings.

---

## 1. Authoritative graph

- **The files.** The Director's commit `fae0add` added exactly two files at the repository root:
  `tech-graph-v0.6.json` (42,620 lines, 832,776 bytes) and `tech-graph-v0.6.html` (214 lines,
  646,209 bytes). No other file changed (`git show --stat fae0add`). **MEASURED.**
- **The authoritative file is `tech-graph-v0.6.json`.** Its top-level `"version"` is `0.6`, and
  its `registry.meta.version` is also `0.6`. **MEASURED.**
- **The HTML is a viewer over identical data.** It embeds the same data in two
  `<script type="application/json">` blocks (`id="data"` and `id="registry"`). Both decode to
  exactly the JSON file's `technologies` array and `registry` object (Python equality: True for
  both). **MEASURED.** So there is one graph, not two. The HTML is class **E** (UI only).
- **A second, placeholder graph already exists.** `Sim.Ui/UiContent/trees/the-trees.json` holds
  the demonstration Trees graph: 31 nodes, seven lenses (`docs/architecture/the-trees-ui.md:392-394`).
  - The Director's instruction "Do NOT create a second technology graph" meets it. It predates
    the technology graph and is marked demonstration content.
  - Which of the two the Trees UI must read is **DIRECTOR DECISION REQUIRED** (§11, F1).
  - This audit creates neither graph and edits neither.
- **The graph's own authority rule:** *"building.requirements.technologies is authoritative;
  technology.unlocks.* is a generated reverse index"* (`registry.meta.authority`). **MEASURED**
  (quoted from the file).

## 2. Graph structure

### 2.1 Top level (question 2) — MEASURED

The root is `{ version, technologies[424], registry{...} }`. The registry holds:

| registry key | entries | record fields |
| --- | --- | --- |
| `meta` | 11 keys | `version, authority, states, rule, vocab, tag_rule, references, sim_gate, null_requirement, unresolved, frontier` |
| `buildings` | 43 | `id, name, kind, tags, requirements{technologies, buildings, institutions, resources, site, settlement}, effects[], maturation, world{layer, manifestation}, sim_link, notes, availability[]` (+ `construction` on the two baseline entries) |
| `infrastructure` | 24 | `id, name, kind, tags, requirements{technologies, site}, effects[], world, sim_link, notes, availability[]` |
| `institutions` | 28 | `id, name, kind, tags, requirements{expression}, hosted_by, world, notes` |
| `units` | 23 | `id, name, kind, tags, requirements{technologies}, world, notes` |
| `activities` | 8 | `id, requires, world` |
| `projects` | 3 | `id, name, kind, repeatable, requirements{technologies, buildings}, effects[], world, notes` |

Every technology record has the same 13 fields:
- `id, name, desc, age`;
- `research{prereq, eureka[], edge_notes[], repeatable?}`;
- `unlocks{capabilities, units, buildings, institutions, techniques, infrastructure, applications}`;
- `progression{family, gen}`;
- `effects{immediate, enables}`;
- `evidence{emerged, region, pathways, basis, sources, confidence, notes}`;
- `sim{materials, labour, channel, carriers, substrate, lattice}`;
- `tags{primary_domains, secondary_domains, application_domains, bonus_categories, scope}`;
- `world{activities}`;
- `legacy{infrastructure_text}`.

**MEASURED** (all 424 share this key set; 10 add `research.repeatable`).

### 2.2 Node categories and types (question 3) — MEASURED

- **Node kinds.** One `technology` kind (424 nodes), plus six registry kinds: `building`,
  `infrastructure`, `institution`, `unit`, activity (no `kind` field) and `project`.
- **Ages.**

  | age | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | **F** |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | techs | 28 | 51 | 30 | 30 | 35 | 40 | 33 | 51 | 110 | **16** |

  A1–A9 match D-043's nine Age ids (`docs/d043-civilization-progression-ages-and-mobile-agents.md:60`).
  **`F` is a tenth band** holding six speculative and ten frontier technologies (§11, F4).
- **Families.** 93 `progression.family` values. 113 technologies carry a generation `gen` (1–7).
- **`tags.primary_domains`** use 15 of the 23 values in `vocab.domains`. The top five are
  science 67, materials 57, agriculture 37, military 36 and transport 34.
- **`tags.scope`** is `civilization` on all 424 technologies.
- **`sim.lattice`** uses 22 knowledge-lattice domains, for example manufacturing 63, transport 55
  and energy 51.
- **No technology carries a Trees lens.** The seven D-043 lenses are Knowledge, Techniques,
  Institutions, Infrastructure, Industry, Military and Applications
  (`docs/d043-civilization-progression-ages-and-mobile-agents.md:118`). The graph routes by
  `tags` domains instead (§11, F5).

### 2.3 Prerequisites (question 4) — MEASURED

- **The field.** `research.prereq` is a boolean expression string over technology ids, using
  `AND`, `OR` and parentheses (300 `AND`, 12 `OR`, 6 parenthesised expressions).
- **Roots.** 10 technologies have `null` prereq: `fire_making`, `knapping_oldowan`,
  `knapping_levallois`, `pressure_flaking`, `adhesive_natural`, `cordage`, `grinding_stone`,
  `dog_domestication`, `root_crop` and `mudbrick`.
- **Integrity.** Zero dangling prerequisite tokens.
- **The forward view.** `effects.enables` lists every technology whose prereq names this one. It
  is exactly the reverse of `prereq`, with 0 mismatches and no non-technology id.
- **`edge_notes`** records dropped or corrected edges in free text, for example *"dropped
  knapping_acheulean: …"*. It is class **D** content.

### 2.4 Eurekas (question 5) — MEASURED

- **The field.** `research.eureka` is a list of free-text strings. 408 technologies have 1–4;
  16 have none.
- **Three prefixes (801 strings):**
  - `circumstance:` — 661, e.g. *"circumstance: dry softwood for drill"*;
  - `institution present:` — 79;
  - `contact with a civilization holding this` — 61.
- **What the graph does not define.** It never states what a Eureka *does*: no magnitude, no
  owning system, no effect field. `registry.meta` has no Eureka key. **MEASURED.** What a Eureka
  does is therefore **DIRECTOR DECISION REQUIRED** (§11, F9).

### 2.5 Unlocks (question 6) — MEASURED

`technology.unlocks` has seven lists. Four hold registry ids; three hold free text.

| list | entries | distinct | representation |
| --- | --- | --- | --- |
| `buildings` | 65 | 41 | registry ids (`building.*`); all resolve |
| `infrastructure` | 30 | 24 | registry ids (`infra.*`); all resolve |
| `institutions` | 28 | 23 | registry ids (`inst.*`); all resolve |
| `units` | 31 | 23 | registry ids (`unit.*`); all resolve |
| `capabilities` | 821 | 804 | **free text** (e.g. "fire on demand", "cutting edges") |
| `techniques` | 30 | 30 | free text |
| `applications` | 7 | 7 | free text |

**The reverse index agrees with the authoritative side.** Rebuilding it from each registry entry's
`requirements.technologies` (and each institution's `requirements.expression`) reproduces
`technology.unlocks.{buildings, infrastructure, institutions, units}` with 0 mismatches.
**MEASURED.**

Three further links, all **MEASURED**:
- `effects.immediate` is empty on all 424 technologies. The graph declares no direct technology
  effect, which is consistent with its `tag_rule` and `frontier.forbidden`.
- `world.activities` names 17 technology→activity links. They agree exactly with the 8 activity
  records' `requires` expressions.
- The 10 `research.repeatable` frontier technologies each name one coefficient in one named
  system, and each says *"DOCUMENTED — no consuming mechanism exists yet"*.
  `frontier_climate` says *"BLOCKED — ADR-008: terrain and environment layers are immutable"*.

### 2.6 Manifestation references (question 7) — MEASURED

`world.manifestation` values across the registry:

| manifestation | count |
| --- | --- |
| building `site_entity` | 24 |
| building `settlement_part` | 19 |
| unit `token` | 23 |
| infrastructure `network` | 16 |
| infrastructure `site_entity` | 4 |
| infrastructure `settlement_part` | 4 |
| institution `none — no independent map entity` | 20 |
| institution `via host building` | 8 |
| activity `activity` | 8 |
| project `site_entity` / `site_entity activity` / `null` | 1 each |

**Requirements that point outside the technology graph:**
- **`resources`** on 8 buildings: `fuel` ×2, `iron ore` ×2, `charcoal`, `charcoal or coke`,
  `coal`, `limestone`, `clay`, `crude oil` and `uranium`.
- **`site`** on 18 buildings and infrastructure entries: `coastal` ×4, `river with head in
  catchment` ×3, `exposed windy site` ×2, `river` ×2, `water power OR steam power`, `river with
  head OR steam power`, `river with head`, `floodplain`, `hillside`, `aquifer upslope`, and
  `river, lake or tidal flat`.
- **`settlement`** on one building (the university): *"literate population above a stated
  threshold"*.
- **`hosted_by`** on 8 institutions, naming a host building.
- **`sim_link`** on 6 entries: projects 1 and 2 in `Sim.Data/content/goods.json`, and `PathBuild`
  on four road entries.

**Graph-internal integrity: one defect.** `inst.newspaper`'s expression `printing_press AND
postal_imperial` names `postal_imperial`, which is neither a technology id nor an institution id.
It is the only dangling reference in the registry. **MEASURED.** Class **F**, as a data defect.

### 2.7 Where each of the task's 23 questions is answered

| questions | section |
| --- | --- |
| 1 filename and version | §1 |
| 2–7 structure, types, prerequisites, Eurekas, unlocks, references | §2.1–§2.6 |
| 8–9 existing / missing entities | §3.1, §3.2, §5 |
| 10 relationships representable today | §4 (chain shapes; "A" rows in §3.2) |
| 11 relationships needing new substrate | §6, §7, §10 |
| 12 purely player-facing relationships | §8 (first bullet); §3.3 (D + E) |
| 13 world / map manifestations | §2.6, §5, §7, §8 |
| 14 new observable natural / world substrate | §7, §10 |
| 15 new state or quantities | §6 items 1, 4, 5, 10; §10 |
| 16 new systems | §6 item 8 |
| 17 new data registries | §6 item 12 |
| 18 construction / availability gates | §6 items 2–3; §11 F2 |
| 19 institution lifecycle, viability / saturation | §6 items 5–7; §9; §11 F3 |
| 20 MobileAgent / unit behaviour | §6 item 9; §11 F8 |
| 21 resource production, extraction, transformation, consumption | §10 |
| 22 knowledge diffusion, contact, trade, migration | §6 item 11; §11 F9; activity.caravans and shipping in §3.2 |
| 23 Age transition / milestone interactions | §11 F4, F7, F8 |

## 3. Repository compatibility

### 3.1 What the repository holds today (questions 8–9) — MEASURED

| repository substrate | where | what the graph needs from it |
| --- | --- | --- |
| 14 goods: grain, livestock, fish, timber, stone, clay, copper-ore, tin-ore, fiber, hides, bronze, tools, pottery, cloth | `Sim.Data/content/goods.json` | 8 of the graph's resource words are **not** goods: fuel, charcoal, coke, iron ore, coal, limestone, crude oil, uranium ("clay" exists) |
| 4 recipes: pottery-firing, weaving, bronze-casting, toolmaking | `goods.json` `recipes` | kiln, smithy, chemical works, oil press, paper mill and semiconductor fab imply new recipes and goods |
| 2 constructible projects: granary (1), workshop (2) | `goods.json` `projects` | the graph's `building.granary` / `building.workshop` `sim_link` resolve to these exactly |
| construction queue and whole-or-nothing gate; **no availability gate** | `Sim.Core/Systems/Construction/ConstructionSystem.cs:94-97` (any project id that exists is queued) | the graph's LOCKED / AVAILABLE states; the graph says so itself (`registry.meta.sim_gate`) |
| built instances: `StructureRow(Settlement, ProjectId, Count)`, read by no system | `Sim.Core/State/WorldState.cs:755`; `docs/design/arch-E-breakthrough-domains.md:88` (F23) | ACTIVE / MATURE; every building effect |
| five labour sectors: Farming, Herding (herding/fishing), Extraction, Crafting, Construction | `Sim.Core/State/WorldState.cs:320-325` | activities farming, herding, fishing, logging and mining |
| deposits: `DepositRow(Settlement, Good, Abundance)`, doubles, per settlement, derived at founding from a terrain channel (fish: water; timber, livestock, fiber, hides: moisture; stone, copper-ore, tin-ore: elevation; clay: water) | `WorldState.cs:279`; `Sim.Core/Worldgen/WorldFounding.cs:410-434`; `goods.json` `depositChannel` | mining, fishing, logging, resource-gated buildings |
| terrain rasters (immutable): elevation, water, temperature, moisture, fertility, movement cost, rivers + river polylines | `Sim.Core/Worldgen/TerrainSet.cs:32-41`; ADR-008 `docs/adr/adr-008-static-terrain.md:8-11` | site predicates (river, coastal, floodplain, hillside, aquifer, windy); `frontier_climate` |
| effective arable area per catchment | `WorldState.cs:88-90` (`EffectiveArableKm2`) | irrigation, terraces, agriculture frontier |
| network graph: nodes, edges with an `EdgeType` int; only `EdgeTypes.DirtPath = 1` defined; PathBuild builds dirt paths and banks labour | `WorldState.cs:54-61`; `Sim.Core/State/Ids.cs:71`; `Sim.Core/Systems/PathBuild/PathBuildSystem.cs:51-52` | roads, canals, railway, bridges, sea edges |
| trade flows between settlements | `WorldState.cs:459` (`TradeFlowRow`) | caravans, shipping |
| published variables and a hysteresis latch (D-020 predicate DSL; classes emerge) | `WorldState.cs:388-402` (`VariableRow`, `ClassStateRow`) | the D-042 §8 capability pipeline; institution emergence |
| buckets keyed by settlement × culture × religion × class × cohort | `WorldState.cs:110-112` | culture, literacy (absent), practitioners as knowledge carriers |
| one per-person row: `NotableRow` (generals) | `WorldState.cs:635-636` | units, special people |
| Trees/Ages UI (placeholder) and world view (demo) | `Sim.Ui/UiContent/trees/*.json`; `Sim.Ui/World/*`; `docs/architecture/the-trees-ui.md`, `world-visualization.md` | visualization layer |
| systems present | `Sim.Core/Systems/`: Appropriation, Catchment, ClassMobility, Colonization, Construction, Consumption, Demographics, Disaster, Growth, Harvest, Housing, Migration, NeedsGrievance, PathBuild, Price, Production, Revolt, Trade, Weather | **no** Research/Knowledge, Education, Health, Water, Energy, Communication, Military/Army, Institution, Money or Space system |

### 3.2 Classification of every registry entry

The 129 entries break down as **A 6 · B 36 · C 82 · F 5**. **MEASURED** (the rule applied to each
row is stated in the row). The **technology→availability edge** of every technology-gated entry
is classed once, globally, in §11 (F2). It is not repeated per row.

| id | kind | world manifestation | class | reason (cite) |
|---|---|---|---|---|
| `building.granary` | building | settlement_part | **A** | shipped project (goods.json projects id 1); effect inert (arch-E F23) |
| `building.workshop` | building | settlement_part | **A** | shipped project (goods.json projects id 2); effect inert (arch-E F23) |
| `building.kiln` | building | settlement_part | **C** | resources absent as goods: fuel |
| `building.smithy` | building | settlement_part | **C** | resources absent as goods: charcoal |
| `building.blast_furnace` | building | site_entity | **C** | resources absent as goods: iron ore, charcoal or coke; site predicate absent: water power OR steam power |
| `building.water_mill` | building | site_entity | **C** | site predicate absent: river with head in catchment |
| `building.windmill` | building | site_entity | **C** | site predicate absent: exposed windy site |
| `building.fulling_mill` | building | site_entity | **C** | site predicate absent: river with head in catchment |
| `building.paper_mill` | building | site_entity | **C** | site predicate absent: river with head in catchment |
| `building.oil_press` | building | settlement_part | **C** | output (oil) is no good in goods.json; ProductionSystem has 4 recipes (goods.json recipes) |
| `building.textile_mill` | building | site_entity | **C** | site predicate absent: river with head OR steam power |
| `building.steelworks` | building | site_entity | **C** | resources absent as goods: iron ore, coal |
| `building.chemical_works` | building | site_entity | **C** | outputs (acids, alkalis) are no goods; no chemical recipes |
| `building.cement_works` | building | site_entity | **C** | resources absent as goods: limestone |
| `building.refinery` | building | site_entity | **C** | resources absent as goods: crude oil |
| `building.power_station` | building | site_entity | **C** | resources absent as goods: fuel; no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `building.hydroelectric_dam` | building | site_entity | **C** | site predicate absent: river with head; no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `building.nuclear_power_station` | building | site_entity | **C** | resources absent as goods: uranium; no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `building.school` | building | settlement_part | **C** | no education system; no literacy publisher (DD-11) |
| `building.library` | building | settlement_part | **C** | no knowledge/research system (CR-005 OPEN) |
| `building.university` | building | settlement_part | **C** | no education system; no literacy publisher (DD-11); no knowledge/research system (CR-005 OPEN) |
| `building.hospital` | building | settlement_part | **C** | DemographicsSystem exists but has no cause-of-death / treatable-condition substrate |
| `building.observatory` | building | settlement_part | **C** | no knowledge/research system (CR-005 OPEN) |
| `building.research_laboratory` | building | settlement_part | **C** | no knowledge/research system (CR-005 OPEN) |
| `building.printing_house` | building | settlement_part | **C** | no knowledge/research system (CR-005 OPEN) |
| `building.mint` | building | settlement_part | **F** | money deferred (GOV-2 §1a); CR-008 open |
| `building.bathhouse` | building | settlement_part | **B** | NeedsGrievanceSystem exists (needs.json); hygiene need mapping undesigned |
| `building.city_wall` | building | settlement_part | **B** | D-039 D1 ("fortification is computed from what a settlement has built", d039:98) ruled; no fortification state |
| `building.castle` | building | settlement_part | **B** | D-039 D1 ruled (d039:98); no fortification state |
| `building.bastion_fort` | building | settlement_part | **B** | D-039 D1 ruled (d039:98); no fortification state |
| `building.harbour` | building | settlement_part | **C** | site predicate absent: coastal |
| `building.lighthouse` | building | site_entity | **C** | site predicate absent: coastal |
| `building.dry_dock` | building | site_entity | **C** | site predicate absent: coastal; no military/army state (milestones.md:326) |
| `building.airfield` | building | site_entity | **C** | no air movement or air transport substrate |
| `building.radio_transmitter` | building | site_entity | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `building.skyscraper` | building | settlement_part | **B** | HousingSystem exists; density ceiling is a housing coefficient |
| `building.spaceport` | building | site_entity | **C** | no space system |
| `building.semiconductor_fab` | building | site_entity | **C** | output (chips) is no good |
| `building.data_centre` | building | site_entity | **C** | no information system |
| `building.desalination_plant` | building | site_entity | **C** | site predicate absent: coastal; no water system |
| `building.solar_farm` | building | site_entity | **C** | no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `building.wind_farm` | building | site_entity | **C** | site predicate absent: exposed windy site; no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `building.fusion_plant` | building | site_entity | **C** | no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `infra.road_track` | infrastructur | network | **A** | PathBuild builds `EdgeTypes.DirtPath` (Ids.cs:71); no tech gate in sim |
| `infra.road_built` | infrastructur | network | **B** | only `EdgeTypes.DirtPath` exists (Ids.cs:71); PathBuild "later milestones spend it on better road tiers" (PathBuildSystem.cs:51-52) |
| `infra.road_paved` | infrastructur | network | **B** | only `EdgeTypes.DirtPath` exists (Ids.cs:71); PathBuild "later milestones spend it on better road tiers" (PathBuildSystem.cs:51-52) |
| `infra.road_macadam` | infrastructur | network | **B** | only `EdgeTypes.DirtPath` exists (Ids.cs:71); PathBuild "later milestones spend it on better road tiers" (PathBuildSystem.cs:51-52) |
| `infra.courier_relay` | infrastructur | network | **B** | D-039 A3 order latency ruled; unimplemented |
| `infra.bridge` | infrastructur | network | **F** | D-009 "era-gated" edges vs Law 4 (CR-009 ground; CR-017 §1) |
| `infra.basin_irrigation` | infrastructur | site_entity | **C** | site predicate absent: floodplain |
| `infra.canal_irrigation` | infrastructur | network | **C** | site predicate absent: river |
| `infra.terraces` | infrastructur | site_entity | **C** | site predicate absent: hillside |
| `infra.qanat` | infrastructur | site_entity | **C** | site predicate absent: aquifer upslope; no water system |
| `infra.well` | infrastructur | settlement_part | **C** | no water system |
| `infra.aqueduct` | infrastructur | network | **C** | no water system |
| `infra.urban_drainage` | infrastructur | settlement_part | **C** | no health/disease system |
| `infra.sewer` | infrastructur | settlement_part | **C** | no health/disease system |
| `infra.sewerage_system` | infrastructur | settlement_part | **C** | no health/disease system |
| `infra.fish_weir` | infrastructur | site_entity | **B** | fish deposit exists (water channel); rivers exist; no weir/lake/tidal substrate |
| `infra.navigation_canal` | infrastructur | network | **B** | network edge has an `EdgeType` int (WorldState.cs:61); only DirtPath defined |
| `infra.railway` | infrastructur | network | **B** | network edge has an `EdgeType` int (WorldState.cs:61); only DirtPath defined |
| `infra.telegraph_line` | infrastructur | network | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `infra.submarine_cable` | infrastructur | network | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `infra.telephone_network` | infrastructur | network | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `infra.power_grid` | infrastructur | network | **C** | no energy system (ADR-019 §5 exists only on unmerged `adr-019-architecture-addendum`) |
| `infra.mobile_network` | infrastructur | network | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `infra.internet_backbone` | infrastructur | network | **C** | no communication/information system (D-039 A3 ruled, unimplemented) |
| `inst.burial` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.scribal_school` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.law_code` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.archive` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.textile_workshop` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.aramaic_admin` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.natural_philosophy` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.library` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.coined_wage` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.census` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.legal_code_roman` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.historiography` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.military_medicine` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.mint_institution` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.university` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.hospital` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.translation_movement` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.guild` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.quarantine` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.newspaper` | institution | via host building | **F** | expression names `postal_imperial`, which is no technology or institution id (dangling) |
| `inst.scientific_society` | institution | via host building | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.joint_stock` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.central_bank` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.patent` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.standard_threads` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.statistics_vital` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.mass_schooling` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `inst.mass_media_broadcast` | institution | none — no independent map entity | **C** | "institution" undefined (CR-010 / DD-11); no institution state |
| `unit.spearmen` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.archers` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.slingers` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.chariot` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.horse_archers` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.heavy_cavalry` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.crossbowmen` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.longbowmen` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.siege_engine` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.artillery` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.musketeers` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.war_galley` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.ship_of_the_line` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.ironclad` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.tank` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.military_aircraft` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.missile` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.nuclear_submarine` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.icbm` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.combat_drone` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.stealth_aircraft` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.aircraft_carrier` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `unit.helicopter` | unit | token | **B** | D-011 owns units; no army/formation state (milestones.md:326); unlock edge: see §11 F2 |
| `activity.farming` | activity | activity | **F** | Farming sector shipped from founding with no tech; graph gates it on A2 crops (§11 F6) |
| `activity.herding` | activity | activity | **F** | Herding sector shipped from founding with no tech; graph gates it (§11 F6) |
| `activity.fishing` | activity | activity | **A** | abstract: Herding sector + `fish` deposit on water channel; no habitat |
| `activity.logging` | activity | activity | **A** | abstract: Extraction sector + `timber` deposit on moisture channel; no forest layer |
| `activity.mining` | activity | activity | **A** | abstract: `ProductionSystem.FromDeposits`; only copper-ore, tin-ore, stone, clay |
| `activity.caravans` | activity | activity | **B** | Trade system ships flows (TradeFlowRow); no caravan MobileAgent |
| `activity.coastal_shipping` | activity | activity | **C** | no sea edges; D-040 B4 ruled sea as edge types, unimplemented |
| `activity.ocean_shipping` | activity | activity | **C** | no sea edges; D-040 B4; CR-017 §5 (naval x/y) |
| `project.orbital_launch` | project | site_entity activity | **C** | no space-program system; repeatable projects not in ConstructionSystem |
| `project.probe_acceleration` | project | site_entity | **C** | no space-program system; repeatable projects not in ConstructionSystem |
| `project.interstellar_probe` | project | None | **C** | no space-program system; repeatable projects not in ConstructionSystem |

### 3.3 Classification of the technology nodes (424)

Classed by what each node's fields ask of the repository:

- **Node identity, name, desc, age, family/gen, evidence, tags and edge_notes: D + E.** These
  are content and display. Tags are explicitly effect-free by the graph's own rule: *"Tags classify
  and route. They generate no effect."* (`registry.meta.tag_rule`). **MEASURED.**
- **Research state — whether a civilization has completed a technology: C.**
  - No research or knowledge system and no per-polity technology state exist
    (`Sim.Core/Systems/`, MEASURED).
  - Its placement is CR-005, OPEN (`docs/adr/cr-005-m5-research-technology-institutions-placement.md:3`).
  - Its constraints are RATIFIED in D-042 §9 (`docs/d042-empire-and-player-control-addendum.md:162-175`):
    parallel research, allocatable flow, a reserve, and no rigid tree.
- **Prerequisite expressions: A as data, C as mechanism.** The shipped D-020 predicate DSL
  evaluates boolean predicates over published variables (`WorldState.cs:388-402`). A technology
  prerequisite is a predicate over technology state, which does not exist yet. **INFERRED.**
- **`sim.substrate` is the graph's own claim.** It says `shipped` for 27 nodes, `partial` for 100
  and `conceptual` for 297 (MEASURED counts).
  - Sampled and **confirmed** as naming a shipped quantity:
    - `track_road` → PathBuild;
    - `copper_smelting` → the copper-ore elevation deposit;
    - `tin_bronze` → the bronze-casting recipe;
    - `haber_bosch` → `OutputPerFarmerPerYear`;
    - `refrigeration` → spoilage (referenced in `Sim.Core/Kernel/FoodAudit.cs`).

    In each case the *quantity* exists; the *technology-conditioned coefficient* does not. That
    makes these nodes class **B** at best.
  - The remaining "shipped" claims: **UNVERIFIED** (not individually re-measured).
- **Frontier (10 repeatable): C.** Each names a coefficient in a system that does not exist yet
  (space, energy, information, AutoResolver), or in one that does (`YieldPerArableKm2PerYear`,
  age-specific mortality). None has a consumer. **MEASURED** (the graph's own
  `status: DOCUMENTED — no consuming mechanism exists yet`).
- **`frontier_climate`: F.** ADR-008 makes terrain immutable (`docs/adr/adr-008-static-terrain.md:8-11`).
- **Capabilities (804 distinct), techniques (30) and applications (7): D.** These are free text,
  not ids, so nothing can reference them mechanically. Turning any of them into D-042 §8
  capability predicates is **C** (§4).

## 4. Technology → capability → application chains

The bridge the Director gave is Technology → Capability → Application → Simulation system/state →
Observable world state → Visualization. The graph realises it in four shapes. **INFERRED**
(from the field structure in §2); **MEASURED** counts.

| chain shape | graph fields | example | layers present in the repository |
| --- | --- | --- | --- |
| **1. Tech → registry entry (availability)** | `registry.*.requirements.technologies` | `kiln_updraft` → `building.kiln` (AVAILABLE) → construction → `StructureRow` | Technology: none. Availability gate: **absent** (`ConstructionSystem.cs:94-97`). Construction: **A** (projects 1–2). Built instance: **A** (`StructureRow`). Effect: **absent** (F23). Visualization: **E**, demo only (`docs/architecture/world-visualization.md`) |
| **2. Tech → activity** | `world.activities`; `registry.activities[].requires` | `fishing_hook` → `activity.fishing` | the activity exists in the simulation as a labour sector *without* a technology gate (§11, F6) |
| **3. Tech → free-text capability** | `unlocks.capabilities` | "fire on demand" | none: there is no capability id, predicate or owner. D-042 §8's pipeline (`d042:152`) and D-020's DSL are the RATIFIED shape; the capabilities themselves are **D** until each is written as a predicate over published state (C) |
| **4. Tech (frontier) → coefficient** | `research.repeatable.effect` | `frontier_crops` → `YieldPerArableKm2PerYear` | the coefficient exists for crops, but no system reads a technology level; the other nine name absent systems |

**The graph already honours the Director's bridge rules:**
- **Researching never builds.** `registry.meta.rule`: *"Researching a technology never creates a
  building. It makes one AVAILABLE."* **MEASURED** (quoted).
- **Technologies carry no direct effect.** `effects.immediate` is empty on all 424. **MEASURED.**
- **Effects name an owning system.** Every registry effect names a `system` (production 19,
  transport 10, energy 7, communication 7, knowledge 5 and others), so no single technology system
  owns consequences. That is consistent with D-042 §7.3, *"Do not create a universal God system
  such as a `CapabilitySystem`"* (`d042-empire-and-player-control-addendum.md:141-142`).
  **RATIFIED** (the rule); **MEASURED** (the conformance).
- **Saturation is derived, never stored.** `SATURATED` is *"derived — marginal contribution below
  a curve owned by the consuming system; never a stored flag"* (`registry.meta.states`). That is
  consistent with D-043 C2's causal viability (`docs/d043-civilization-progression-ages-and-mobile-agents.md:600`).
  **INFERRED.**

**Where the chain breaks today** (in order along the chain):
1. There is no per-civilization technology state.
2. There is no availability gate.
3. There are no capability ids.
4. Built instances are inert.
5. Most effect systems do not exist.

**MEASURED** (§3.1).

## 5. Existing manifestations

**Natural and world manifestations the task named.** Status 1 = already represented,
2 = represented only abstractly, 3 = partially represented, 4 = absent. All rows **MEASURED**
against the cited lines.

| manifestation | status | what exists | cite |
| --- | --- | --- | --- |
| fish populations / habitats | **2** | a per-settlement `fish` deposit (Abundance double) derived from the water channel at founding; no population, habitat or depletion | `WorldState.cs:279`; `WorldFounding.cs:410-434`; `goods.json` (fish `depositChannel: water`) |
| forests | **2** | a `timber` deposit derived from moisture; no forest or vegetation raster (D-009 names one, `d009-d010-map-population-addendum.md:11`; not in `TerrainSet`) | `TerrainSet.cs:32-41` |
| deposits | **3** | `DepositRow` for 6 extractive goods (stone, clay, copper-ore, tin-ore, plus fish and timber); per settlement, not spatial; none for iron, coal, oil, uranium or limestone | `WorldState.cs:279`; `goods.json` |
| fertile land | **1** | `Fertility` raster; `EffectiveArableKm2` per catchment | `TerrainSet.cs:36`; `WorldState.cs:88-90` |
| rivers | **1** | `Rivers` raster, polylines, river crossing cost; no "head" (hydraulic drop) quantity | `TerrainSet.cs:38-41`; `Sim.Core/Worldgen/Hydrology.cs` |
| lakes | **4 / UNVERIFIED** | inland water may exist in the `Water` raster; no lake entity or predicate (`grep -i lake Sim.Core` → nothing) | `TerrainSet.cs:33` |
| coasts | **3** | derivable from the `Water` raster; siting considers coasts; no coastal predicate or row | `Sim.Core/Worldgen/SettlementSiting.cs:13` |
| fishing activity | **2** | pooled in the Herding sector ("herding/fishing — the food-from-deposits sector") | `WorldState.cs:321` |
| mining activity | **2** | Extraction sector via `ProductionSystem.FromDeposits`; no mine entity (the graph itself removed `building.mine`, `registry.meta.unresolved`) | `Sim.Core/Systems/Production/ProductionSystem.cs:176-180` |
| agriculture | **1** | Farming sector, harvest, weather, arable area | `WorldState.cs:320`; `Sim.Core/Systems/Harvest/` |
| ports | **4** | no port, harbour or sea edge; D-040 B4 rules sea travel as network edge types (`d040-discovery-and-control.md:80`), unimplemented | — |
| roads | **3** | PathBuild builds `DirtPath` edges only | `Ids.cs:71`; `PathBuildSystem.cs:51-52` |
| industrial activity | **3** | Crafting sector and 4 recipes; `StructureRow` counts are inert | `goods.json` recipes; arch-E F23 |
| institutions | **4** | no institution state; the class latch is the closest shape; "institution" is undefined (CR-010) | `WorldState.cs:397-403`; `docs/architecture/pre-m5-repository-audit.md:380` |
| armies / units | **4** | "No AutoResolver and no armies" certified at M4 exit; `NotableRow` generals only | `docs/milestones.md:326`; `WorldState.cs:635-636` |
| special people | **3** | `NotableRow` persons (allegiance, cohort); no catalogue, skills or position | `WorldState.cs:635-636`; D-043 B3 |
| cultural groups | **2** | culture is a bucket key only; no group entity | `WorldState.cs:110-112`; D-043 B6 |

**Graph manifestations that already exist in the repository (class A):**
- `building.granary`, `building.workshop` — projects 1 and 2;
- `infra.road_track` — DirtPath;
- `activity.fishing`, `activity.logging`, `activity.mining` — abstract sectors.

**MEASURED.** Farming and herding also exist, but ungated (§11, F6).

## 6. Missing simulation substrate

Each item cites the evidence of absence. **MEASURED** unless labelled.

1. **Per-civilization technology state and completion** (questions 15, 16). No research system
   and no technology table exist (`Sim.Core/Systems/`, `WorldState.cs`). Needed by all 424 nodes.
   Placement: CR-005 OPEN. Constraints: D-042 §9 (RATIFIED, `d042:162-175`). **C.**
2. **An availability gate on construction orders** (question 18). Absent:
   `ConstructionSystem.cs:94-97` queues any existing project id. The graph itself says *"wiring
   this registry requires that gate first"* (`registry.meta.sim_gate`). **B.** D-042 §8 is the
   RATIFIED shape; its predicate DSL exists (`WorldState.cs:388-402`).
3. **Construction projects beyond 2** (question 18). 41 more buildings have no `goods.json`
   project. **D** as data. Each also needs its effect (item 4).
4. **Effects of built instances** (question 15). `StructureRow` is read by no system (arch-E F23,
   `docs/design/arch-E-breakthrough-domains.md:88`). **B.**
5. **Maturation** (question 19). `MATURE` is *"not yet in the sim"* (`registry.meta.states`), for
   6 buildings with `maturation.matures: true`: school, library, university, hospital,
   observatory and research laboratory. The carrier question is DD-T9, OPEN
   (`docs/architecture/the-trees-ui.md:456`). **C.**
6. **Saturation and viability** (question 19). Derived by each consuming system (graph rule;
   D-043 C2, RATIFIED). The graph cites *"docs/adr/adr-028-viability-and-saturation.md"*, which
   **does not exist on any ref** (§11, F3). **C.**
7. **Institution state and lifecycle** (question 19). 28 institutions, with requirement
   expressions that may reference other institutions and 8 host buildings. There is no
   institution substrate, and "institution" is undefined: CR-010 is unwritten (DD-11,
   `docs/architecture/pre-m5-repository-audit.md:380`). The graph's own note: *"Institutions-layer
   ruling owed."* **C.**
8. **New systems named by effects** (question 16), each absent from `Sim.Core/Systems/`:
   - energy (7 effects; capacity and grid);
   - knowledge (5);
   - education (2);
   - health (3);
   - water (4);
   - communication (7) and information (1);
   - space program (4);
   - economy / money (1 — deferred, GOV-2 §1a, CR-008);
   - military (1).

   ADR-019 §5, cited by `building.power_station`, exists only on the unmerged
   `origin/adr-019-architecture-addendum` (`a36b94d`). **C.**
9. **Units, formations and armies** (question 20). 23 units, all *"Provisional. D-011 owns unit
   definitions"*. No army or formation state (`docs/milestones.md:326`). MobileAgent state is
   ruled but not implemented (D-043 B1, B5). **B.** Their technology-gated unlock edge is F2.
10. **Literacy** (question 15). The university's settlement condition, *"literate population above
    a stated threshold"*, needs a published literacy quantity. None exists (DD-11 includes "who
    publishes literacy/education"). **C.**
11. **Knowledge carriers and channels** (question 22). `sim.channel` is tacit 210 / articulated
    166 / both 48. `sim.carriers` names practitioners, artefacts and institutions. These are the
    diffusion-model inputs of the design corpus (arch-C, PROPOSED). No diffusion system exists.
    D-043 B4 rules knowledge polity-owned (RATIFIED). **C.**
12. **New data registries** (question 17). The graph *is* the registry for buildings,
    infrastructure, institutions, units, activities and projects. Nothing in `Sim.Data` holds it,
    and the simulation's ids are integers (`goods.json` projects `1`, `2`; ADR-001 bans strings in
    rows). Still needed:
    - a string-id → integer-id mapping;
    - resource and site vocabularies (§2.6);
    - a capability-id registry, if the free-text capabilities are to be referenced.

    **D** for the data; **C** for the capability ids (§4, shape 3). **INFERRED.**

## 7. Missing world substrate

Questions 13 and 14. **MEASURED** unless labelled.

- **Site predicates** (18 entries). Needed: `coastal`, `river with head`, `floodplain`,
  `hillside`, `aquifer upslope`, `exposed windy site`, and `river, lake or tidal flat`.
  - What exists: rivers, water, elevation, moisture and fertility rasters (`TerrainSet.cs:32-41`).
  - What does not: none of these is a derived per-settlement or per-site predicate; there is no
    wind, aquifer or hydraulic-head layer; "lake" is unconfirmed.
  - Class **C**: which predicates are derived from the immutable rasters and which need new ones
    is a decision. New rasters meet ADR-008.
- **Sites as places.** 24 buildings, 4 infrastructure entries and 2 projects are `site_entity`: a
  map entity outside the settlement footprint (dams, mills, mines, power stations). The world view
  places structures only *inside* a settlement's composed sprite, with no world coordinates
  (`docs/architecture/world-visualization.md:65-66`, WV-02). D-043 D2 rules institutions
  embedded; it says nothing on site entities. **C.**
- **Sea network.** Harbour, lighthouse, dry dock and coastal/ocean shipping need sea edges. D-040
  B4 is RATIFIED as the shape (`d040:80`); none are implemented. Continuous naval movement is
  CR-017 §5 (OPEN). **B / F.**
- **Road tiers, railway and canals.** `EdgeType` is an int, but only `DirtPath` is defined
  (`Ids.cs:71`). **B.**
- **Bridges** as computed edge types. The graph's note: *"d009-d010 calls bridges era-gated — a
  law-4 violation with an ADR owed"*. This is CR-009 ground, absorbed candidate in CR-017 §1
  (`docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:36`). **F.**
- **Environmental change.** `frontier_climate` (remediation) and the land effects of
  irrigation, terraces and drainage. Terrain is immutable (ADR-008). Irrigation can act through
  `EffectiveArableKm2` without mutating terrain (**INFERRED**). **C / F** (`frontier_climate` F).
- **Natural resources as stocks.** Fish populations, forests and new deposits: §10.

## 8. Missing visualization substrate

Questions 12 and 13. The visualization layer exists as a read-only, placeholder layer
(`docs/architecture/world-visualization.md`, `the-trees-ui.md`, `ages-ui.md`). **MEASURED.**

- **Purely player-facing (E).** These need no simulation:
  - the HTML viewer;
  - `evidence`, `desc`, `edge_notes`, `legacy.infrastructure_text` and `progression.family/gen`
    display;
  - Eureka hint text;
  - tags as filters;
  - the Age band layout.

  **INFERRED.**
- **Trees UI binding.** The shipped Trees UI reads `the-trees.json` (31 demo nodes, seven
  lenses). Two things are undecided:
  - there is no loader for `tech-graph-v0.6.json`;
  - there is no lens mapping, because the graph has domains, not lenses (§11, F5).

  **C.**
- **Registry visuals.** `morphology.json` has visual types for a university and other demo
  structures, but no mapping from the 43 building ids. `settlement_part` fits the composed
  sprite (WV-02). `site_entity`, `network` (road tiers, rail, canal, cable) and `activity`
  (visible fishing, farming, mining, shipping) have no view slot beyond the demo agent tokens
  and dirt edges. **C.**
- **Age bands.** The graph's `F` band has no D-043 Age (§11, F4). The Ages view has six
  placeholder Ages (D-043 F25). **F.**
- **Missing state.** There is no LOCKED/AVAILABLE/MATURE/SATURATED state for the UI to report
  until §6 items 2–6 exist. The Trees state vocabulary (`the-trees.json` state sets) has no
  SATURATED state. **B.**

## 9. Missing institutions/infrastructure/industry substrate

- **Institutions (28).** All class C: no substrate; "institution" undefined (CR-010 unwritten;
  DD-11). **MEASURED.**
  - 20 have no map entity. 8 are hosted by a building (school, library, university, hospital,
    mint, printing house, research laboratory).
  - Requirement expressions chain institutions (e.g. `inst.university` ← `library AND
    legal_code_roman`), and the graph notes these are *"not validated against the technology
    graph"*. Institution-requires-institution is a new relationship kind. **C.**
  - Hosting (`hosted_by`) makes an institution depend on a built instance. **C.**
  - One dangling reference (`postal_imperial`). **F.**
- **Infrastructure (24).**
  - Roads: 1 A (dirt track) and 3 B (tiers).
  - Canal and railway: B.
  - Courier relay: B (D-039 A3 order latency).
  - Water, health and communication networks: C.
  - Bridge: F.

  **MEASURED.**
- **Industry.**
  - Kiln, smithy, blast furnace, steelworks, cement works, refinery, chemical works, oil press,
    paper mill and semiconductor fab need new goods and recipes, and in several cases new
    resource deposits. **C.**
  - Textile and fulling mills need site predicates. **C.**
  - The generic `building.workshop` is flagged by the graph itself: *"relation to the crafting
    sector and artisan emergence is undesigned"*. **DIRECTOR DECISION REQUIRED.**

## 10. Missing resource/natural-world substrate

Question 21. **MEASURED** unless labelled.

- **Production, extraction and transformation already present:**
  - 14 goods and 4 recipes;
  - deposits for 6 extractive goods;
  - the Farming, Herding/fishing, Extraction and Crafting sectors;
  - `tools` as a Ledger good that wears out (`Sim.Core/Systems/Production/ProductionSystem.cs:36-44`).
- **Named by the graph and absent as goods or deposits:**
  - fuel, charcoal, coke, coal, iron ore, limestone, crude oil, uranium;
  - implied outputs: oil, paper, acids, steel, cement, chips and electricity. Electricity is a
    capacity, not a good (ADR-019 §5 on the unmerged branch).
  - Class **C**: whether each is a Law 1 conserved good, a deposit-scaled rate or a capacity is a
    design decision.
- **Renewable natural stocks.** Fish populations and forests exist only as static
  per-settlement abundance doubles fixed at founding (§5). Depletion, regrowth and habitat need
  a new stock class. **C**, and it meets ADR-008 if spatial.
- **`sim.materials` and `sim.labour`** on technologies are free text: 424 entries, e.g. "loom
  weights" and "weaver". Class **D**; nothing references them mechanically.
- **Consumption.** Consumption, needs and spoilage exist (`ConsumptionSystem`, `needs.json`).
  Technology-driven changes to them (e.g. `refrigeration` → spoilage) need a technology
  coefficient. **B.**

## 11. Conflicts and unresolved architecture questions

**No document below was edited.** Each item carries **Status: UNRESOLVED. Owner: director.**

**F1 — Two graphs.** `tech-graph-v0.6.json` (424 technologies) and the placeholder
`Sim.Ui/UiContent/trees/the-trees.json` (31 demo nodes) both describe a technology graph.
- The instruction "Do NOT create a second technology graph" is honoured by this audit.
- The existing placeholder is a second graph in effect. **DIRECTOR DECISION REQUIRED:** whether
  the Trees UI is re-pointed at the v0.6 graph, and what becomes of the placeholder (D-043 F25
  already records its six Ages as divergent).

**F2 — Technology → availability against D-040 B3.**
- **The graph's rule:** *"Researching a technology … makes one AVAILABLE"*. The Director's rule
  for this task is the same.
- **D-040 B3 (RATIFIED):** *"NO TECHNOLOGY UNLOCK. LAW 4 BINDS."* It quotes *"No era gate, no
  date, no unlock."* (`d040-discovery-and-control.md:59-64`).
- **D-043 A8 ratified** *"Research unlock → capability becomes known/possible"* (`docs/d043-civilization-progression-ages-and-mobile-agents.md:329`),
  and D-043 F8 already records the collision with B3 (`:827`).
- The graph's form keeps realization separate (resources, site, settlement, construction), which
  is the reading D-043 F8 calls reconcilable (INFERRED there). **DIRECTOR DECISION REQUIRED**
  before any availability gate is built. It affects the 118 technology-gated registry entries.
- **Additionally:** 23 units unlock by technology. D-011's *"era-gated additions"*
  (`d011-battle-layer-addendum.md:13`) is the CR-009 ground (CR-017 §1).

**F3 — DD-13 / DD-14 collide, and ADR-028 does not exist.**
- **The graph:** `registry.meta.references` defines *"DD-13 … viability is hierarchical and
  type-specific"* and *"DD-14 … saturation via viability thresholds and diminishing returns"*,
  both in `docs/adr/adr-028-viability-and-saturation.md`. The frontier forms cite "(DD-14)"
  ten times.
- **The repository:**
  - D-043 minted **DD-13 = generic current-Age effects** and **DD-14 = the Age Transition Surge
    model** (`docs/d043-civilization-progression-ages-and-mobile-agents.md:281`, `:210`).
  - `adr-028-viability-and-saturation.md` exists on no ref (`ls docs/adr`,
    `git log --all -- docs/adr/adr-028*`: empty). ADR-028 is the next free number.
- **MEASURED.** **DIRECTOR DECISION REQUIRED:** which meaning keeps DD-13/DD-14, and whether a
  viability/saturation ADR-028 is to be written. Not renumbered here.

**F4 — A tenth Age band `F`.** 16 technologies carry `age: "F"`.
- D-043 A1 RATIFIED *"exactly 9 Ages"* (`docs/d043-civilization-progression-ages-and-mobile-agents.md:60`).
- Whether `F` is a display band beyond A9, a placeholder, or a tenth Age is **DIRECTOR DECISION
  REQUIRED**.

**F5 — Lenses against domains.**
- D-043 A3 RATIFIED seven Trees (lenses). The graph classifies by 23 `vocab.domains` and 22
  lattice domains, and has no lens field.
- The mapping, and whether a technology may appear in several lenses, is **DIRECTOR DECISION
  REQUIRED**.
- The Spine's knowledge row says *"no tree; domain lattice lite"*
  (`civ-sim-architecture-v3-outline.md:84`, frozen). CR-017 §8 records that "The Trees" is
  consistent while the lenses stay player-facing over computed state (INFERRED there).

**F6 — Gated activities against a shipped ungated start.**
- **The graph:** `activity.farming` requires cereal/root/rice crops (A2 technologies);
  `activity.herding` requires `sheep_goat OR cattle` (A2).
- **The simulation:** it starts at 4000 BCE, in the Neolithic era band, with Farming and Herding
  sectors active from founding and no technology state (`WorldState.cs:320-321`;
  `m0-kernel-spec.md:17`).
- Wiring these gates would switch off the shipped food economy unless starting civilizations
  hold those technologies. **DIRECTOR DECISION REQUIRED:** the starting technology set per
  founded civilization.

**F7 — Ages as a technology property against Ages as development states.**
- Each technology carries one `age`. D-043 A1/A2 rule Ages as civilization development states
  entered through Core and Supporting milestones.
- The graph contains **no milestones** (no milestone field or registry). **MEASURED.**
- Whether a technology's `age` is descriptive placement only, or feeds milestones, is **DIRECTOR
  DECISION REQUIRED**. If it gates anything, it is CR-017 §1 ground.

**F8 — Unit modernization has no representation.**
- D-043 A8 rules the automatic upgrade of obsolete units (*"Crossbowmen → Riflemen"*,
  `docs/d043-civilization-progression-ages-and-mobile-agents.md:316`).
- The graph has `unit.crossbowmen` but no riflemen unit and no successor field on units.
  `progression.family/gen` exists on technologies only. **MEASURED.**
- **DIRECTOR DECISION REQUIRED:** where successor relations live (the graph or D-011's unit
  definitions). CR-017 §4 governs the conversion itself.

**F9 — Eureka semantics are undefined.**
- 801 Eureka strings, with no effect, magnitude or owner (§2.4).
- A Civ-style research-cost reduction would be a technology-level modifier. Whether it is
  admissible under Law 2 (`CLAUDE.md:17`) is not stated.
- `contact with a civilization holding this` implies diffusion (D-040 / C07–C10, the audit's
  CONFLICT class, `docs/architecture/pre-m5-repository-audit.md:309-312`).
- `institution present` implies institution state (item 7 in §6).
- **DIRECTOR DECISION REQUIRED.**

**F10 — Money-dependent entries.** `building.mint`, `inst.coined_wage`, `inst.mint_institution`,
`inst.joint_stock`, `inst.central_bank` and `bill_of_exchange` / `paper_money` all depend on money.
Money is deferred (GOV-2 §1a), and its owning milestone is CR-008. **UNRESOLVED.**

**F11 — Frontier lines against static terrain.** `frontier_climate` is *"BLOCKED — ADR-008"*
(graph); `docs/adr/adr-008-static-terrain.md:8-11`. **MEASURED.**

**F12 — `bonus_categories` wording.**
- Every technology and building carries `bonus_categories`. The graph rules that tags *"generate
  no effect"*, which is consistent with Law 2 (`CLAUDE.md:17`) and with DD-13's hold on generic
  effects.
- **PROPOSED:** keep the word "bonus" out of any runtime name. A later reader could take it as a
  bonus table. Not a conflict today.

**F13 — `inst.newspaper` references `postal_imperial`**, which does not exist in the graph (§2.6).
**MEASURED.**

**F14 — Graph effects cite unmerged or missing documents.**
- ADR-019 §5 (`building.power_station`) exists only on `origin/adr-019-architecture-addendum`.
- ADR-028: F3.
- **MEASURED.**

## 12. Implementation dependency graph

**PROPOSED** (an ordering of the missing substrate, not a design). An arrow means "is needed
before".

```
Director rulings: F1 (which graph), F2 (tech→availability vs D-040 B3), F3 (DD ids / ADR-028),
                  F4 (Age F), F6 (starting tech set), CR-005 (research placement), CR-010 (institution)
      │
      ▼
Graph loader + validation (data only; reads tech-graph-v0.6.json, rejects dangling ids)
      │
      ▼
Per-civilization technology state + completion  ──►  Trees UI binding (lens mapping F5)
      │                                                        │
      ▼                                                        ▼
Availability predicate (D-042 §8 pipeline, D-020 DSL) ──► LOCKED/AVAILABLE shown in UI
      │
      ▼
Construction gate in ConstructionSystem + projects for registry buildings (goods.json data)
      │
      ├─► Built-instance effects (StructureRow read by owning systems — F23)
      │        ├─► maturation carrier (DD-T9)  ─► MATURE
      │        └─► per-system viability/saturation (D-043 C2; F3) ─► SATURATED
      │
      ├─► New goods/recipes/deposits (fuel, iron ore, coal …) ─► resource-gated buildings
      ├─► Site predicates from terrain (coastal, river head, floodplain …) ─► site-gated entries
      ├─► Network edge types (road tiers, canal, rail; sea per D-040 B4) ─► transport entries
      ├─► Institution substrate (CR-010) ─► 28 institutions, hosted_by
      ├─► New systems (energy, knowledge, education, health, water, communication, space)
      └─► MobileAgent / army substrate (D-043 B1/B5; CR-017) ─► 23 units, activities as agents
```

## 13. Recommended implementation order

**PROPOSED.** Each step waits for the rulings it names. Nothing here is authorised by this audit.

1. **Director rulings first:** F1, F2, F3, F4, F6, and CR-005 placement. Without F2 the gate
   cannot be built; without F6 wiring activity gates breaks the shipped economy.
2. **Graph loader and validator (data and tests only).** Load `tech-graph-v0.6.json` read-only;
   pin the integrity measurements in §2 (0 dangling prerequisite tokens, reverse index exact),
   plus the one known defect (F13).
3. **Per-civilization technology state and completion.** Its milestone placement is CR-005.
4. **Availability predicate over technology state** (D-042 §8), consumed by a construction
   availability gate. Add registry buildings as `goods.json` projects only after that.
5. **Built-instance effects,** one owning system at a time, starting where the system exists
   (production, housing, consumption). This closes arch-E F23.
6. **Trees UI bound to the graph,** reading state through the existing read-only interfaces.
7. **Then, in parallel, each behind its own ruling:** goods and deposits; site predicates;
   network edge types; institutions (CR-010); maturation (DD-T9); viability and saturation (F3);
   new systems; MobileAgents and units (CR-017).

## 14. Items explicitly deferred

**DEFERRED by this audit** (not examined for design, per the task):
- every mechanic: research formulas and costs; Eureka effects; the availability gate's code;
  effect magnitudes (the graph itself says *"magnitude: unspecified"*);
- the frontier coefficients' curves (the graph cites DD-14 for them; see F3);
- implementation of any absent natural or world item in §5 (the task: "Do NOT propose
  implementation details for absent items yet");
- unit statistics and modernization (D-011; CR-017 §4);
- space-program projects and their victory relevance (the graph's own note: *"a Director
  decision"*);
- money-dependent entries (CR-008);
- climate and terrain mutation (ADR-008).

**Summary**

| Area | Existing | Missing | Dependency | Status |
| --- | --- | --- | --- | --- |
| Graph file | `tech-graph-v0.6.json` v0.6; HTML viewer embeds identical data | a loader; the `postal_imperial` defect | F1 | MEASURED |
| Technology state / completion | none | per-civ technology state, research flow | CR-005; D-042 §9 | DIRECTOR DECISION REQUIRED |
| Prerequisites | predicate DSL (D-020) for other predicates | technology-state operands | technology state | MEASURED |
| Eurekas | none | semantics, owner | F9 | DIRECTOR DECISION REQUIRED |
| Availability (LOCKED/AVAILABLE) | none (`ConstructionSystem.cs:94-97`) | the gate | F2; D-042 §8 | DIRECTOR DECISION REQUIRED |
| Construction / built instances | 2 projects; `StructureRow` | 41 projects; effects (F23) | the gate | MEASURED |
| Maturation / saturation | none | carrier (DD-T9); per-system curves | F3; D-043 C2 | DIRECTOR DECISION REQUIRED |
| Institutions (28) | class latch only | institution substrate, hosting, chaining | CR-010 | DIRECTOR DECISION REQUIRED |
| Infrastructure (24) | DirtPath edges | road tiers, rail, canal, sea, water/health/comms networks | edge types; D-040 B4; CR-017 §5 | MEASURED |
| Industry | Crafting sector, 4 recipes | ~10 industrial buildings' goods and recipes | goods decisions | INFERRED |
| Resources / natural world | 14 goods; 6 deposit goods; immutable rasters | fuel/iron/coal/oil/uranium/limestone; fish and forest stocks; site predicates | ADR-008; goods decisions | MEASURED |
| Activities (8) | farming, herding, fishing, logging, mining (abstract, ungated) | shipping; caravans as agents | F6; sea edges | DIRECTOR DECISION REQUIRED |
| Units (23) / MobileAgents | `NotableRow` generals | army/formation/MobileAgent state; successors | D-011; D-043 B1/B5; CR-017; F8 | DIRECTOR DECISION REQUIRED |
| Knowledge diffusion / contact | trade flows; migration | diffusion system; carriers | DD-05; D-040 | DIRECTOR DECISION REQUIRED |
| Ages / milestones | D-043 rulings; UI placeholder (6 Ages) | milestones in graph; band F | F4; F7; CR-017 §1 | DIRECTOR DECISION REQUIRED |
| Visualization | Trees UI (demo graph), world view (demo) | graph binding; lens map; site/network/activity visuals | F1; F5 | INFERRED |
| New systems | 19 shipped systems | energy, knowledge, education, health, water, communication, space, money | per-system rulings | MEASURED |

---

**Audit report**

- **Exact graph filename:** `tech-graph-v0.6.json`, at the repository root. The viewer
  `tech-graph-v0.6.html` embeds identical data.
- **Graph version:** `0.6` (root `version` and `registry.meta.version`).
- **Git branch:** `claude/civdemo-work-b1z2y4`.
- **Git HEAD:** `fae0add` (audit written against it; the audit document itself is uncommitted).
- **Files inspected:**
  - the graph: `tech-graph-v0.6.json`, `tech-graph-v0.6.html`;
  - simulation data: `Sim.Data/content/goods.json`, `worldgen.json`;
  - simulation code:
    - `Sim.Core/State/WorldState.cs`, `Ids.cs`, `Variables.cs`;
    - `Sim.Core/Systems/` (directory listing);
    - `Sim.Core/Systems/Construction/ConstructionSystem.cs`;
    - `Sim.Core/Systems/PathBuild/PathBuildSystem.cs`;
    - `Sim.Core/Systems/Production/ProductionSystem.cs`;
    - `Sim.Core/Worldgen/TerrainSet.cs`, `WorldFounding.cs`, `Hydrology.cs`, `SettlementSiting.cs`,
      `Worldgen.cs`;
  - UI content: `Sim.Ui/UiContent/trees/the-trees.json`;
  - decision records and CRs:
    - `docs/d043-civilization-progression-ages-and-mobile-agents.md`;
    - `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md`;
    - `docs/adr/adr-008-static-terrain.md`;
    - `docs/adr/cr-005-m5-research-technology-institutions-placement.md`;
    - `docs/d040-discovery-and-control.md`, `d042-empire-and-player-control-addendum.md`,
      `d039-command-fog-and-siege.md`, `d011-battle-layer-addendum.md`,
      `d009-d010-map-population-addendum.md`;
  - other docs:
    - `docs/design/arch-E-breakthrough-domains.md`, `arch-PQ-conflicts-and-questions.md`;
    - `docs/architecture/pre-m5-repository-audit.md`, `the-trees-ui.md`, `world-visualization.md`;
    - `docs/milestones.md`, `docs/m0-kernel-spec.md`, `CLAUDE.md`.
- **Files changed:** `docs/design/technology-graph-integration-audit.md` (new). No other file
  changed.
- **Tests run:** none. This is a documentation-only audit that touches no code. Measurements were
  Python parses of the graph and repository content files, `grep`, and `git` commands, reported
  inline.
