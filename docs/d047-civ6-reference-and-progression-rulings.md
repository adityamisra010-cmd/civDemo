# D-047 — CIV VI AS DEFAULT REFERENCE; CAUSAL PREREQUISITES, BASELINE CONTENT, EUREKAS, AGE ADVANCEMENT AND MILITARY PROGRESSION RULINGS

**Status:** DIRECTOR RULINGS (2026-10-02), recorded verbatim in Part A. Parts B–F are the repository's record of
what each ruling closes and how the Phase 1 content pass applied it; they decide nothing beyond the rulings.
**Branch:** `research-progression-foundation` (PR #10). **Number:** D-047 was measured free on every ref
(ledger annex R7) and is minted here.
**Supersedes nothing by rewriting.** Every earlier record keeps its text; the append-only pointer notes are listed
in Part F.

---

## PART A — THE DIRECTOR'S RULINGS (verbatim)

The block between the markers is the Director's message of 2026-10-02, title and PART 1 (rulings 1–18), byte for
byte as received. SHA-256 of the block's content (the lines between the markers, each ending in a newline):
`6713ce776eef0473fbea827ad8e1f2407c5ada891b4f1114dac3ad1c0da2281d`.

<!-- BEGIN DIRECTOR TEXT (D-047 PART 1, verbatim) -->
```text
DIRECTOR RULING + IMPLEMENTATION PASS
Civ VI default reference + make the simulation visible

The previous questions are now CLOSED.

DO NOT reopen previously settled decisions.

Use the following new Director rulings as authoritative.

==================================================
PART 1 — NEW DIRECTOR RULINGS
==================================================

1. CAUSAL PREREQUISITES

Use the strict rule:

Every prerequisite relationship must be causally and historically possible before the technology/capability it enables.

If a prerequisite chronologically follows the technology it currently gates, treat the edge as incorrect and fix/re-scope it.

Do not merely preserve a historically impossible edge because Civ VI would permit it for gameplay.

Review the approximately 10 candidates identified in the gap audit, including examples such as:

- xray -> vacuum_tube
- canning -> germ_theory

These are review candidates, not automatically all confirmed defects.

Use causal reasoning, historical chronology and the actual meaning of the nodes.

==================================================
2. FIRE
==================================================

Assume fire was already discovered before the simulation begins.

Fire itself is BASELINE.

Do not require research to unlock basic fire.

If `fire_making` currently represents the discovery of fire, re-scope it toward an advanced/improved use of controlled fire rather than deleting it blindly.

==================================================
3. HAFTING / ADHESIVE
==================================================

Use a split model:

Primitive/basic hafting and adhesive techniques = BASELINE.

Advanced composite/material/precision hafting = technology-owned.

Do not require research for the basic founding-era ability to make simple hafted tools.

==================================================
4. FISHING
==================================================

Basic fishing is BASELINE.

Fishing must not require a research node merely to exist as an activity.

Advanced fishing techniques remain technology-owned.

==================================================
5. FISH WEIRS
==================================================

Fish weirs are NOT baseline.

Treat them as technology-owned infrastructure/advanced fishing infrastructure.

Basic fishing exists without them.

==================================================
6. FISHHOOK
==================================================

Basic fishing remains baseline.

A basic fishhook should not gate the existence of fishing.

Specialized fishing equipment and improved/pelagic fishing can remain technology-owned.

==================================================
7. WATERCRAFT
==================================================

Primitive shore-hugging transport is BASELINE.

This includes basic riverine/coastal craft where physically appropriate.

Advanced seagoing/oceanic transport is technology-owned.

Use this progression concept:

primitive water transport
→ improved coastal/seagoing transport
→ oceanic navigation/transport

Research unlocks the capability/class.

Research does NOT itself construct the vessel.

The appropriate downstream industry/infrastructure/military/transport system performs realization.

==================================================
8. ROADS
==================================================

Use the realistic interpretation:

B.

Road technology nodes represent infrastructure-class improvements/unlocks.

They do NOT construct roads.

Construction/infrastructure systems realize them.

Basic paths remain baseline.

Technology may unlock improved road classes such as paved/macadamized infrastructure.

Do not use calendar Age alone as the gate.

Do not blindly delete existing road technology nodes.

Resolve their meaning as capability/class unlocks rather than construction itself.

==================================================
9. UNIVERSITIES
==================================================

Freeze the currently ratified five university types.

Do not expand the number of university types in this pass.

Use the five existing specialized university types.

Their effects remain domain-specific EffectiveCost reductions and downstream institutional effects.

Do NOT copy Civ VI's flat Science output.

==================================================
10. EUREKAS
==================================================

Accept the Civ VI-style meaningful Eureka philosophy.

Every Eureka should represent a meaningful:

- achievement
- discovery
- observation
- exposure
- institutional condition
- practical accomplishment
- historical circumstance

that plausibly accelerates understanding of the technology.

Reject trivial "you possess the raw material required to manufacture this" conditions unless the material possession itself is historically meaningful evidence for the technology.

Retain the existing civDemo 40% BaseCost ceiling.

Review and rewrite the inherited weak Eureka strings.

`arsenical_bronze` should specifically reference meaningful arsenical ore exposure/use rather than generic material possession.

==================================================
11. CIVICS UI
==================================================

YES.

The player-facing KNOWLEDGE & TECHNOLOGY lens contains:

- Technology research
- Civics research

These remain TWO separate actual research trees.

Civics is NOT a third tree.

Active/adopted civic/government effects are surfaced under INSTITUTIONS.

==================================================
12. AGE MILESTONE EVALUATION
==================================================

AGE-MS-1 is now RATIFIED.

Architecture:

Individual owning systems publish milestone facts.

Each fact belongs to one of the five Age-milestone categories:

1. Technological
2. Material-Economic
3. Institutional-Social
4. Systemic
5. Military Realization

A thin Age evaluator reads those facts.

The evaluator checks:

- mandatory core milestones
- supporting milestone requirements
- category coverage

The evaluator does NOT become a universal ProgressionSystem/God object.

==================================================
13. AGE ADVANCEMENT — IMPORTANT NEW RULE
==================================================

Age advancement is NOT automatic immediately when all milestones are satisfied.

Once a civilization has satisfied all requirements for the next Age:

the civilization becomes ELIGIBLE TO ADVANCE.

The PLAYER or AI must explicitly choose to advance.

The player can interact with the CURRENT CAPITAL CITY and see:

"ADVANCE AGE"

when the civilization is eligible.

Clicking "ADVANCE AGE":

- records the civilization's decision;
- schedules the Age transition;
- causes the next turn to begin under the new Age;
- does not retroactively alter the current turn;
- triggers the existing Age-transition surge;
- triggers the appropriate automatic unit modernization/conversion;
- makes the new Age's technology/capability environment active.

AI civilizations make the equivalent decision through AI logic.

IMPORTANT:

The player may deliberately delay Age advancement even after becoming eligible.

This is intentional.

Example:

A civilization may have fulfilled the requirements for a new Age but deliberately keep its existing military configuration for another period before advancing.

Do NOT automatically advance the civilization merely because all milestones are complete.

==================================================
14. AGE SURGE
==================================================

The player chooses the type/emphasis of the Age surge.

Use the Civ VI Golden Age / dedication concept as inspiration, but adapt it to civDemo.

The surge must remain:

- temporary
- causal
- domain-specific
- implemented through the relevant systems/equations

It must NOT become a universal floating modifier/God-system.

Do not freeze numerical coefficients yet.

Create the UI concept so the player can see/select the surge emphasis when advancing.

==================================================
15. SPECIAL PEOPLE
==================================================

The Civ VI Great People / Rock Band pattern is accepted.

Special people/cultural groups have:

- finite useful actions/charges
- ownership by the recruiting civilization
- meaningful specialized abilities
- retirement/death/disbanding after their useful lifecycle

This is already compatible with the MobileAgent architecture.

Do not reopen the finite-charge decision.

==================================================
16. RECRUITMENT
==================================================

Recruitment model:

A.

Institutions generate recruitment points/eligibility.

Civilizations compete for or recruit available special people.

Use the Civ VI Great People pattern as the gameplay reference.

Do not make every ordinary person a fully simulated special agent.

Ordinary population remains represented through the population system.

==================================================
17. ZONES OF CONTROL
==================================================

YES.

Establish Zones of Control for military movement.

Do NOT copy Civ VI's hex implementation.

civDemo uses continuous x/y positions.

Therefore:

- define a geometric/agent-based ZOC around appropriate military units/formations;
- entering or moving through ZOC should impose the appropriate movement/action restriction;
- friendly/hostile/neutral territory should be handled explicitly;
- ZOC must be deterministic;
- ZOC must be queryable by movement/pathfinding;
- do not create a universal military God object.

Use Civ VI's strategic concept as inspiration, not its hex-grid implementation.

==================================================
18. UNIT MODERNIZATION — ALREADY SETTLED
==================================================

DO NOT reopen this.

At Age transition:

units automatically convert to the appropriate next-Age version of their unit type.

This is automatic.

It is free.

Do NOT introduce Civ VI-style paid manual upgrades.

However, we need a proper unit-line architecture so that obsolete units do not remain unchanged forever.

```
<!-- END DIRECTOR TEXT -->

### A.2 — The decision hierarchy (Director text, verbatim from the Director's message of 2026-10-02)

The Director's preceding message ("DIRECTOR RULING: CIVILIZATION VI AS DEFAULT DESIGN REFERENCE"), which this
record's rulings close, set the hierarchy verbatim:

> Decision hierarchy:
>
> 1. Explicit Director-ratified civDemo decisions
> 2. Explicit repository architectural decisions/ADRs
> 3. Civ VI precedent for the closest analogous mechanic
> 4. New design only where neither 1 nor 2 nor 3 provides a suitable answer

Causal and historical possibility (ruling 1) is a level-1 Director ruling, so it binds prerequisite edges above any
Civ VI precedent. Civ VI precedent is adapted, never copied: no flat Science output, no hex-grid ZOC, no paid
manual upgrades (rulings 9, 17, 18).

*(Correction, 2026-10-02: an earlier draft of this section recorded an agent-reconstructed hierarchy because the
source text was not available to the drafting agent; the Director's text above replaces it.)*

### A.3 — Parts 2–9 of the same message (requirements summary, not rulings re-stated)

The same message orders an implementation programme. It is summarised, not ratified, here; each later phase
records its own ADR where it touches a contract.

- **Part 2 — unit progression system.** Civilization-wide generic unit FAMILIES by military role (at least: light
  infantry/scout, spear/anti-cavalry, heavy/line infantry, ranged infantry, mounted scout/light cavalry, heavy
  cavalry, ranged cavalry, siege, naval combat, naval transport, air, specialised modern roles). Per family: role,
  emergence, Age availability, generic identity, predecessor, successor, auto-modernization, branching, equipment,
  MobileAgent vs army composition, no-analogue handling. Schema and graph first; no final combat statistics.
- **Part 3 — Age transition and modernization.** On advancing, each eligible unit converts to its successor; with
  no successor it is preserved (or converted to an explicit generic successor). Location, ownership, compatible
  experience, army membership and supply are preserved. Deterministic, inspectable, tested.
- **Part 4 — visibility.** A full-screen Technology and Civics experience under KNOWLEDGE & TECHNOLOGY (two sibling
  trees generated from the real research data, with prerequisites, progress, Eureka state, base and effective
  cost); capital-city ADVANCE AGE with milestone status, surge choice and a modernization preview drawn from the
  unit-family data; world/settlement visibility of progress.
- **Part 5 — zoom levels** (world, regional, settlement, deep UI) with explicit information density.
- **Part 6 — discipline.** No universal ProgressionSystem, no VisualizationGodObject; UI read-only over simulation
  state; no mock data for the progression UI.
- **Part 7 — order.** Phase 1 (this record and the content pass: causal review, Eurekas, baseline, roads and
  watercraft); Phase 2 (Age eligibility, evaluator, explicit advancement, next-turn transition, surge plumbing);
  Phase 3 (unit families and modernization); Phase 4 (full-screen UI); Phase 5 (world visuals).
- **Parts 8–9 — quality bar and verification** (the test list, the gates, no RP overflow, no frozen decision
  reopened, no God object).

---

## PART B — WHAT EACH RULING CLOSES

| ruling | closes | record of the closure |
|---|---|---|
| 1 causal prerequisites | gap audit **G-34** (chronology candidates), **G-22** (medicine edges), **G-24** (communications edges); proposal P11 (edges part) | Part C per-edge table; `research.json`; test `ResearchContentTests.D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent` |
| 2 fire | §3 `fire_making` row (UNRESOLVED → BASELINE); **G-19** in part; matrix §5 item 3 (fire strings) | `baseline.fire`; `fire_making` re-scoped to advanced controlled-fire use, key 1 kept |
| 3 hafting/adhesive | §3 `hafting`, `adhesive_natural` rows (UNRESOLVED → split); matrix §5 item 3 (hafting strings) | `baseline.primitive_hafted_tools`; both nodes re-scoped to composite hafting; `unit.spearmen` stays on `hafting` (a trained composite-spear unit, not the founding unit — `baseline.basic_military` is unchanged) |
| 4 fishing | **G-18** (`activity.fishing`), §3 fishing rows | `activity.fishing` requires nothing; `baseline.basic_fishing` |
| 5 fish weirs | §3 `infra.fish_weir` row (UNRESOLVED → TECHNOLOGY-OWNED infrastructure) | `infra.fish_weir` keeps `requires: basketry`; recorded, no content change |
| 6 fishhook | §3 `fishing_hook` row (UNRESOLVED) | `fishing_hook` re-scoped to line and pelagic fishing; its activity link removed |
| 7 watercraft | **D-044 Part D T3** (sea part), gap audit **G-04** (sea part), §3 raft / dugout / sail_square / coastal and ocean shipping rows; matrix §5 item 4 | `baseline.primitive_water_transport`; raft and dugout re-scoped to improved craft; `sail_square` = the improved coastal/seagoing CLASS (`activity.coastal_shipping`); `caravel`, `carrack`, `polynesian_canoe` = the oceanic CLASS (`activity.ocean_shipping`). Research unlocks the class; no node builds a vessel |
| 8 roads | **D-044 Part D T3** (road part), **G-04** (road part), §3 track_road / road_paved / macadam rows; matrix BLOCKED road rows | `baseline.basic_paths`; the three road nodes are infrastructure-CLASS unlocks (strings reworded, nothing deleted); `infra.road_*` keep their requirements; construction realizes them |
| 9 universities | **G-25** (sixth type) | the five types are frozen; no content change |
| 10 Eurekas | **G-26** (a) and (b); proposal P11 (Eureka part) | Part D; test `D047_ArsenicalBronzeEureka_*` |
| 11 Civics UI | **G-08** (which lens surfaces Civics) | Technology and Civics are two trees under KNOWLEDGE & TECHNOLOGY; adopted civic effects under INSTITUTIONS. G-07 (stale architecture text) is left for P8 |
| 12 Age milestones | **G-14 — AGE-MS-1 is now RATIFIED** (ledger annex R6.3: owning systems publish facts in five categories; a thin evaluator, no God object) | implemented in Phase 2 |
| 13 Age advancement | the "automatic on milestones" reading of ledger §10 (explicit player/AI choice; next-turn effect; delay allowed) | Phase 2 |
| 14 Age surge | CR-017 §2 (surge choice; temporary, causal, domain-specific; coefficients not frozen) — recorded, CR-017 itself is UNMERGED-B and not edited | Phase 2 (plumbing), Phase 4 (UI) |
| 15, 16 special people | **G-27** (finite charges, ownership, lifecycle; recruitment model A) | recorded; not implemented in this pass |
| 17 Zones of Control | movement question of CR-017 §5 / G-28 (geometric, deterministic, queryable ZOC; no hex, no military God object) | recorded; not implemented in this pass |
| 18 modernization | nothing reopened: automatic, free, at Age transition | Phase 3 |

**Also recorded here (an earlier Director freeze, not new): completion overflow is LOST.** Conversational statement
2.2 of 2026-10-01 ("RP overflow FREEZE: lost", gap audit §4) is recorded by this D-record as the Director's freeze:
RP past the target's remaining cost, and RP with no target, is not stored and does not carry to any node. This
closes **G-01** and confirms ADR-029 R-2 as built (ledger §5.5). **G-02** (ADR-030 §2 item 3's wording) is stale
documentation only.

**M5 entry gate (ledger §28).** Item 8 (baseline capabilities explicitly classified) and item 9 (no known
research/starting-world contradiction) are met for the content named in the gap audit (G-18, G-19, G-20, G-21 and the
coastal-shipping gate), measured on this branch after the Phase 1 commits.

---

## PART C — CAUSAL PREREQUISITE REVIEW (ruling 1), PER EDGE

**Method.** Every candidate from gap audit G-34 (eleven era-marked hits plus five bare-year cases) and the edges of
G-22 and G-24 was judged on the node's meaning, the historical mechanism and the dates. A defect is fixed by
replacing the late prerequisite with the knowledge that actually enabled the dependent; no node is deleted or
renumbered, no date text is altered to make an edge pass (except `fire_making`, re-scoped by ruling 2).

**Derived fields.** `depth` is regenerated from the prerequisites (36 nodes move). Where the number of prerequisite
atoms crosses the corpus's own `prereq_complexity` convention (one atom 0.0, two 0.5, three 1.0 — 402 of 430 nodes
follow it), the factor, `magnitude` and `cost` move with it; `cost` is U × K^magnitude rounded to tens exactly as the
corpus rounds it, and `calibration_adjustment` stays 0 (ADR-029 addendum A, R-10). No other factor is touched.

| # | dependent (dated) | edge reviewed (dated) | verdict | reasoning | new prerequisite | prereq_complexity / cost |
|---|---|---|---|---|---|---|
| 1 | `bill_of_exchange` (~1150 CE) | `double_entry` (~1300 CE) | **DEFECT — inverted** | Genoese and Islamic (suftaja) bills of exchange predate double-entry bookkeeping, which grew up partly to account for them. Credit instruments need coinage and a literate merchant script | `coinage_electrum AND (abjad OR chinese_script)`; and `double_entry` now requires `hindu_arabic AND paper AND bill_of_exchange` (coinage reached through the bill). Family `finance` generations swapped: bill 1, double entry 2 | bill 0.0 → 0.5, 2490 → 3520; double entry unchanged |
| 2 | `alchemy` (~200 BCE China) | `glass_blowing` (~50 BCE) | **DEFECT** | Han and early Hellenistic alchemy worked with furnaces, metals and minerals, not blown glass; the blown-glass alembic belongs to distillation (Maria, ~100 CE). Alchemy rests on pyrometallurgy plus a literate theory tradition (Chinese or Greek) | `copper_smelting AND (chinese_script OR alphabet_vowels)`; `distillation` gains `glass_blowing` (`alchemy AND glass_blowing`) | alchemy 0.0 → 0.5, 4970 → 7030; distillation 0.0 → 0.5, 2490 → 3520 |
| 3 | `standard_weights` (~2600 BCE) | `surveying` (~2500 BCE) | **DEFECT** | Weight standards came from administered exchange and accounting (tokens, then records), not land measurement | `token_counting AND (proto_writing OR hieroglyphic)` | 0.0 → 0.5, 1240 → 1760 |
| 4 | `seed_drill` (~200 BCE) | `iron_mouldboard` (~100 BCE) | **DEFECT** | The Han multi-tube drill needs the ard and iron shares, not the later mouldboard | `plough_ard AND cast_iron` | 0.0 → 0.5, 1760 → 2490 |
| 5 | `torsion_artillery` (~340 BCE) | `mechanics_archimedean` (~250 BCE) | **DEFECT** | Macedonian torsion engines (~340 BCE) grew from the gastraphetes; the calibration formula is a later refinement, not the cause | `crossbow` | 0.5 → 0.0, 7030 → 4970 |
| 6 | `canning` (1810 CE) | `germ_theory` (1860 CE) | **DEFECT** (Director's example) | Appert's process was empirical; Pasteur explained it fifty years later (the node's own text says so) | `glass_blowing AND sheet_metal` | 1.0 → 0.5, 23650 → 16730 |
| 7 | `glass_lead` (1674 CE) | `coke` (1709 CE) | **DEFECT** | Ravenscroft's lead crystal used coal-fired, covered-crucible glasshouses, not coke; lead glass also existed earlier. No coal node exists to substitute | `glass_blowing` | 0.5 → 0.0, 9950 → 7030 |
| 8 | `cotton_gin_saw` (1793 CE) | `milling_machine` (~1818 CE) | **DEFECT** | Whitney's gin used wire hooks on a cylinder; it needed drawn wire, not the milling machine | `cotton_gin_roller AND wire_drawing` | unchanged (0.5) |
| 9 | `cannon_cast_bronze` (~1400 CE) | `corned_powder` (~1420 CE) | **DEFECT** | Cast-bronze bombards used serpentine powder; corning followed and is not their cause. Bell-founding (lost-wax/closed casting) is | `cannon_early AND lost_wax` | 1.0 → 0.5, 9950 → 7030 |
| 10 | `xray` (1895 CE) | `vacuum_tube` (1906 CE) | **DEFECT** (Director's example) | Röntgen used a Crookes/Hittorf gas-discharge (cathode-ray) tube driven by an induction coil: vacuum technique plus electromagnetism, a decade before the thermionic valve | `electromagnetism AND barometer_vacuum AND photography` | 0.5 → 1.0, 47310 → 66910 |
| 11 | `jet_engine` (1939 CE) | `superalloy` (~1940 CE) | **DEFECT** (strict rule) | The first jets ran on heat-resisting steels; Nimonic superalloys were developed for the jet engine, so the dependency runs the other way | `aircraft_metal AND steam_turbine` | 1.0 → 0.5, 16730 → 11830 |
| 12 | `genome_sequencing` (1977) | `pcr` (1983) | **DEFECT** (bare years) | Sanger sequencing (1977) predates PCR; the node's emergence is Sanger's method | `dna_structure AND computer` | unchanged (0.5) |
| 13 | `superconductivity_applied` (1911 discovery; magnets 1960s) | `quantum_mechanics` (1925) | **KEEP** | The node is the APPLICATION (1960s magnets), which relied on the quantum theory of type-II superconductors; discovery is not the node | — | — |
| 14 | `precision_agriculture` (1990s) | `gps` (1995) | **KEEP** | Within the dates' precision; GPS guidance is the defining enabler | — | — |
| 15 | `combat_drone` (1990s–2000s) | `gps` (1995) | **KEEP** | Within precision; GPS-guided UAVs (Predator 1995) | — | — |
| 16 | `cmos_vlsi` (1970s–1980s) | `microprocessor` (1971) | **KEEP** | VLSI followed and was driven by the microprocessor | — | — |
| 17 | `anaesthesia` (1846, surgery 4) | `chemistry_quantitative AND anatomy_dissection` | **KEEP** | Both precede it; surgical anaesthesia is a surgical practice (dosing to operative depth) that presupposes anatomical surgery, and its agents come from pneumatic chemistry | — | — |
| 18 | `antisepsis` (1867, surgery 5) | `germ_theory AND anaesthesia` | **KEEP** | Both precede it; Lister applied germ theory to the longer operations anaesthesia made possible | — | — |
| 19 | `vacuum_tube` (1906) | `electric_light AND radio` | **KEEP** | Fleming's valve came from the Edison effect in lamps and was built as a radio detector | — | — |
| 20 | `radio` (1895 / 1920) | `electromagnetism AND telegraph` | **KEEP** | Radio telegraphy grew out of Hertz's electromagnetism and wire telegraphy | — | — |
| 21 | `clinical_medicine` family naming (G-22) | — | **NO CHANGE** | A naming question, not an edge; out of ruling 1's scope | — | — |

**Penicillin (G-23).** `antibiotic_penicillin`'s whole prerequisite closure (AND and OR) contains no surgery-family
node; pinned by `D047_PenicillinsPrerequisiteClosure_ContainsNoSurgeryFamilyNode`.

**After the pass** the era-marker heuristic finds **0** violations (was 11) over every dependent dated after
3000 BCE; pinned by `D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent` with no exemption.

---

## PART D — EUREKAS (ruling 10)

The eight inherited Eurekas (G-26a) are rewritten as authored, node-specific circumstances. Weights stay 0.40 each
(the ceiling). `source` becomes `authored`. Only `arsenical_bronze` changes evaluability.

| node | was | now (text — kind — system) | why it accelerates |
|---|---|---|---|
| `adhesive_natural` | "circumstance: tree resin, bitumen seep, or plant gum" (generic) | "Resin, bitumen or ochre is already gathered and worked for daily binding" — exposure — resources/terrain | compound adhesives were refined where the raw binders were handled daily (Sibudu, Umm el Tlel) |
| `arsenical_bronze` | "arsenical copper ore (fahlore)", **`when: stock_copper_ore > 0`** (generic copper stock) | "Arsenic-bearing copper ores (fahlore, enargite, arsenopyrite-rich deposits) are smelted locally" — exposure — resources/terrain, **future-system** (`when: null`) | arsenical copper arose where arsenic-rich ores were smelted; generic copper-ore stock carries no arsenic signal and no state distinguishes arsenical ore yet, so the condition waits for that system rather than firing on any copper (G-26b) |
| `glass_glaze` | "natron or plant ash" | "Natron or plant-ash alkali is already used in washing, embalming or ceramic work" — exposure — resources/terrain | faience and glazes came from alkali fluxes already in daily use |
| `greek_fire` | "petroleum" | "Naphtha from surface petroleum seeps is already used as an incendiary" — exposure — resources/terrain | Callinicus refined an existing naphtha-incendiary tradition |
| `sulphuric_acid` | "saltpetre" | "Sulphur is burned with saltpetre to make acid for bleaching or metal finishing" — practical accomplishment — goods state | Ward's glass-globe process (1736) is the small-batch practice the lead chamber scaled |
| `antiviral_drugs` | "epidemic viral disease" | "A recognised viral epidemic drives a targeted pharmaceutical programme" — world event — world events | acyclovir and the AIDS-driven AZT/HAART programmes |
| `catalytic_converter` | "urban air pollution" | "Urban photochemical smog has produced vehicle-emission law" — institutional — institutions | the Clean Air Act limits set its 1975 introduction |
| `stealth` | "rival integrated air defence" | "A rival integrated radar air-defence network has inflicted aircraft losses" — world event — world events | Vietnam and 1973 losses made radar signature decisive |

**Counts after the pass:** 81 Eurekas on 80 nodes, all authored; **17** evaluable today (was 18). The one remaining
goods-stock condition on copper ore is `copper_smelting`'s, authored with its justification (§8.5 permits it).

---

## PART E — BASELINE AND CLASS CONTENT (rulings 2–8)

**Activities (G-18).** `activity.farming`, `.herding`, `.fishing`, `.logging`, `.mining` now have `requires: null`
and are removed from the crop, `sheep_goat`, `cattle`, `fishing_hook`, `ground_stone_early` and `mining_shaft`
reverse indexes (the loader's reverse-index rule). Pinned: null requirement, eligible with zero nodes.

**Baseline list (G-20, G-21).** `providedBy` corrected for `settlement_founding` (famine flight with no viable exit;
no settler or found order), `food_gathering` (no HarvestSystem; HarvestWeatherSystem supplies only the multiplier) and
`construction` (whole-project completion, no progress field; "no Builder" kept). Added: `baseline.fire`
(not simulated), `baseline.primitive_hafted_tools` (not simulated), `baseline.basic_fishing` (ProductionSystem),
`baseline.primitive_water_transport` (not simulated as vessels; river corridor coefficient), `baseline.basic_shelter`
(HousingSystem), `baseline.basic_paths` (PathBuildSystem).

**Re-scoped node strings (G-19; ids, keys and nodes kept).**

| node | change |
|---|---|
| `fire_making` (1) | "Controlled-fire pyrotechnology": heat treatment of stone, managed high-temperature hearths, planned burning; `emerged` re-dated to the re-scoped capability (~164 kya) |
| `adhesive_natural` (9) | "Compound adhesives" for load-bearing composite hafting |
| `hafting` (11) | "Composite hafting": durable composite spear, socketed axe/adze |
| `fishing_hook` (22) | "Line and pelagic fishing" |
| `raft` (20) | "Sea-crossing raft" (open water beyond sight of land) |
| `dugout` (21) | "Load-carrying dugout" (lake/estuary freight, offshore fishing) |
| `sheep_goat` (41) | "herding sector" → "selective flock breeding" |
| `ground_stone_early` (8) | resharpenable ground-edge felling axe; efficient heavy woodworking |
| `digging_stick_hoe` (39) | hafted-blade tillage of heavier soils |
| `sail_square` (94) | improved coastal and seagoing transport class; river strings reworded as improvements under sail |
| `caravel`, `carrack`, `polynesian_canoe` | description notes the oceanic transport class |
| `track_road`, `road_paved`, `macadam` | capabilities and descriptions reworded as infrastructure-class unlocks realized by construction |

**Recorded as non-owning, unchanged** (the audit §3 "record" option): `knapping_oldowan` (cutting edges, butchery),
`cordage` (snares, nets, windbreak shelter), `basketry` (fish traps), `bone_tools` (harpoons, awls, needles),
`grinding_stone` (wild-cereal harvesting, storage, drying), `shelter_hut` (an advanced framed dwelling). The baseline
forms of these exist without the nodes; the node strings describe the improved forms or historical context and gate
nothing. `infra.fish_weir` keeps `basketry` (ruling 5).

---

## PART F — APPEND-ONLY NOTES MADE BY THIS RECORD

- `docs/design/director-architecture-and-decision-ledger.md` — Part II annex **R8** (Part I untouched; its SHA-256
  `4c18b3b9…72b8a2` re-measured unchanged).
- `docs/d044-research-progression-rulings.md` — Part F note: **T3 resolved** by rulings 7 and 8.
- `docs/adr/adr-029-research-engine.md` — **Addendum B** (content pass, overflow note, goldens).
- `docs/design/research-capability-ownership.md` — §5 resolution note.
- `docs/design/director-ledger-gap-audit.md` — status section (§9).
