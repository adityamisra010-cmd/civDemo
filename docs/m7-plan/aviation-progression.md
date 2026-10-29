# M7 Aviation Progression: Flight I–III, dependencies, resources and open decisions

**Status: PROPOSED, for the Director's review.** This is a design and documentation file only.

**Nothing in this file changes:**
- runtime code, content data (`research.json`, `unit-families.json`, `goods.json`, `sim.json`, `ages.json`);
- the research and civics trees, schemas, defaults, saves or tests;
- `main`, tags or releases.

Every change to content below is a **proposal** that needs its own approval. TC-1 stays **PROPOSED**.

**Verified base.** Branch `claude/new-session-yavghm`, head `0bf7626` (equal to the remote branch); `main` = `6e51a3e`.
Every finding in §2 was measured on that tree or on the recorded four-seed candidate runs (seeds 1, 2, 42, 7).

**Rulings applied** (recorded verbatim in [D-065](../d065-m7-charter-rulings.md)):
- **R1** manual re-equipment is a distinct, permitted order; ADR-031's free Age-transition modernization is unchanged.
- **R2** EC-1 (metals), EC-2 (mounts) and EC-3 (gunpowder) are separate, individually reviewed packets inside M7.
- **R3** aviation progresses in stages across Ages, not all at A9.
- **R4** energy-system ownership (unit charter Q8 / building charter B5) stays **open**.

**Companions:** [unit charter](unit-catalogue-and-progression.md) §7.17–§7.22 and §12.5;
[building charter](building-and-infrastructure-charter.md) §5.11 and §5.19;
[gap-resolution standard](gap-resolution-standard.md).

---

## Contents

1. [Summary](#1-summary)
2. [Findings on the live tree](#2-findings-on-the-live-tree)
3. [The three Flight stages](#3-the-three-flight-stages)
4. [Aviation dependency matrix](#4-aviation-dependency-matrix)
5. [Distinct roles, not one generic aircraft](#5-distinct-roles-not-one-generic-aircraft)
6. [Which systems realize what](#6-which-systems-realize-what)
7. [Flight I industry: implementation and measurement](#7-flight-i-industry-implementation-and-measurement)
8. [Forward airfields](#8-forward-airfields)
9. [Reconciliation: carriers, reconnaissance, identities and migration](#9-reconciliation-carriers-reconnaissance-identities-and-migration)
10. [Proposed research-content changes](#10-proposed-research-content-changes)
11. [Proposed resource packets](#11-proposed-resource-packets)
12. [Milestone ownership](#12-milestone-ownership)
13. [Open decisions and the evidence each needs](#13-open-decisions-and-the-evidence-each-needs)

---

## 1. Summary

1. **The live tree gates Flight I behind A9, but not through a prerequisite.** `aircraft` is tagged A9 while both its
   prerequisites (`internal_combustion`, `glider`) are A8. Under the Age sweep (D-063 R2, `tuning.ageSweep` = true),
   an A9 node needs every A7 node of both trees. That keeps `aircraft` unresearchable for 89–121 turns of A8 in every
   measured seed (§2.1).
2. **The smallest causally valid fix is one Age tag:** retag `aircraft` A9 → A8 and re-score it with A8 priors. No
   prerequisite, no new node (§10, change RC-1). It is a research-content and TC-1 STRUCTURE change, so it is proposed,
   not applied.
3. **The tag alone does not stage the family.** Under the charter's recruitment rule (§6.5: knowledge gates, the Age is
   not read), `military_aircraft` (1101) is gated by `aircraft` alone, so today one identity covers all powered flight.
   The stages come from knowledge gates:
   - Flight I = `aircraft`;
   - Flight II = `aircraft_metal`, `jet_engine`, `helicopter`, `radar`, `aerial_reconnaissance`;
   - Flight III = `stealth`, `combat_drone`, `guided_missile`, `computer`.
4. **Bombers get their own family** (`strike_aviation`, proposed key 23). As branches of `air` they would be converted
   into fighters at A9 (ADR-031 §3 rule 2: a branch converts to the next mainline identity).
5. **Inside A9 there is no later Age**, so Flight II → Flight III happens only by recruiting branch identities or by the
   re-equipment order that ruling R1 permits. ADR-031's free modernization does no work inside A9.
6. **Every Flight is blocked by missing goods.**
   - Flight I needs fuel and engines.
   - Flight II also needs aluminium, whose smelting needs electricity. That ties Flight II to the open energy question
     (R4).
   - Flight III also needs electronics and composites.
   §11 proposes the packets as proposals. It authorizes nothing.

---

## 2. Findings on the live tree

### 2.1 The Age sweep is the actual A9 gate

`aircraft` is A9, `glider` and `internal_combustion` are A8. D-063 R2 makes a node of Age *a* eligible only when every
non-repeatable node of Age *a* − 2 in **both** trees is complete (`ResearchQuery.SweepSatisfied`).

Measured on the candidate configuration (`research-cost-pass/candidate`), turn numbers:

| Seed | A8 entry | A6 cohort done (both trees) | A7 cohort done (both trees) | A9 entry | `internal_combustion` | `glider` | `aircraft` |
|---|---|---|---|---|---|---|---|
| 1 | 1127 | 1098 | 1238 | 1262 | 1221 | 1291 | 1292 |
| 2 | 1201 | 1160 | 1322 | 1346 | 1297 | 1375 | 1376 |
| 42 | 1087 | 1056 | 1176 | 1193 | 1162 | 1217 | 1218 |
| 7 | 1094 | 1058 | 1192 | 1212 | 1177 | 1241 | 1242 |

**Reading:**
- **As tagged (A9):** `aircraft` is ineligible for the first 111, 121, 89 and 98 turns of A8 (seeds 1, 2, 42, 7). It
  becomes eligible only 24, 24, 17 and 20 turns before A9 entry.
- **Retagged (A8):** its sweep cohort is A6, which every seed completes before entering A8. The binding constraint
  becomes the knowledge itself: `internal_combustion` and `glider`.
- **The AI's own path:** even an AI that researches `internal_combustion` as an A9 core goal waits a further 17, 25, 14
  and 15 turns for the A7 sweep.

The same holds across all 179 recorded runs of this session's pacing passes and cost batches (scratch data, not
committed): `aircraft` never completes before A9 entry. In 4 runs A9 was never entered, and in 5 `aircraft` was never
completed.

### 2.2 The AI never aims at aviation before A9

`AiResearchPolicy` works goal closures:
- the next Age's core milestones;
- the capability-gated actions it uses (roads, taxation, universities);
- supporting milestones.

No goal contains `glider` or `aircraft` before A9. A9's core is `electricity_generation` and `internal_combustion`, and
the `a9_formation` supporting milestone is pending recruitment.

That is why `glider` completes exactly one turn before `aircraft` in every run: it is reached as part of the `aircraft`
closure, after A9 entry. **The retag fixes eligibility, not AI intent.** An AI that fields Flight I in late A8 needs a
recruitment-driven goal, covered in §6, row "AI".

### 2.3 Effective cost moves the other way

BaseCost = content cost × `pacingScale[tree][Age − 1]`. The technology scales are A8 = 0.775796 and A9 = 0.068141.

| Case | Content cost | Scale | Effective RP |
|---|---|---|---|
| `aircraft` as tagged (A9) | 66,900 | 0.068141 | ≈ 4,559 |
| `glider` (A8, for comparison) | 9,950 | 0.775796 | ≈ 7,719 |
| `internal_combustion` (A8, for comparison) | 47,310 | 0.775796 | ≈ 36,703 |
| `aircraft` retagged, factors unchanged | 66,900 | 0.775796 | ≈ 51,901 |
| `aircraft` retagged and re-scored with A8 priors (difficulty 3.0 → 2.5, institutional 2.5 → 2.0; magnitude 9.5 → 8.5) | 33,450 | 0.775796 | ≈ 25,950 |

The A9 tag makes `aircraft` cheaper than `glider`, which is an artefact of the A9 scale. Retagging without the ADR-045
re-score would make it dearer than `internal_combustion`. **RC-1 therefore includes the re-score.** Its pacing effect
must be measured, not assumed (§10, evidence E1).

### 2.4 Other live facts the design must respect

- `building.airfield` requires `aircraft`; it moves with RC-1.
- `unit.aircraft_carrier` requires `aircraft` AND `steam_turbine`. With RC-1, both are A8 knowledge. The identity 908 is
  A9.
  - Under §6.5, carriers would be knowledge-realizable in late A8, which is consistent with HMS Argus (1918).
  - Materials block them in any case.
- `aerial_reconnaissance` (A9) requires `aircraft` AND `photography`. With RC-1, both are A8; it stays A9 (Flight II).
- `helicopter`, `stealth` and `combat_drone` each require `aircraft` plus later knowledge; none needs a change.
- `ages.json` `a8_formation` / `a9_formation` count formations whose identity Age is ≥ 8 / ≥ 9.
  - Retagging identity 1101 to A8 (§9.3) moves it from the A9 count to the A8 count.
  - The new A9 identities (§9.3) carry the A9 count.
- `goods.json` has 14 goods: no fuel, engine, aluminium, electronics or composite good exists. Timber, cloth and tools
  exist.

---

## 3. The three Flight stages

The stage label is design vocabulary. **Nothing reads it.** Each identity is gated by knowledge, materials, location,
buildings and support (unit charter §6.5). The Age is read only by ADR-031 modernization.

### Flight I: Powered flight (late A8, once RC-1 is approved)

| Item | Content |
|---|---|
| Knowledge | `glider`, `internal_combustion` → `aircraft` (retagged A8) |
| Identities | `military_aircraft` (1101, retagged A8): reconnaissance, observation and air combat, with a light-attack action. Light bombers (new, `strike_aviation` A8 mainline). Anti-aircraft artillery (new, `air_defence` A8 mainline: `rifled_artillery` AND `aircraft`). |
| Buildings | Landing strip: `building.airfield` tier 1 at an eligible controlled settlement. Aircraft works: a recruitment location for air formations, gated by `aircraft` (building charter §5.11). |
| Capabilities | Visual reconnaissance sortie (VISIBLE around the target for the turn). Light bombing on the battle map (D-011 §5 strike mission). Spotting for artillery. |
| Inputs | Recruitment: timber, cloth and tools (all existing goods), engines (**missing**). Operation: fuel (**missing**). No aluminium. |
| Status | **blocked** (engines, fuel). Design is M7; realization waits on packet EC-4a (§11). |

### Flight II: Advanced aviation (A9)

| Item | Content |
|---|---|
| Knowledge | `aircraft_metal` (needs `aluminium`), `aerial_reconnaissance`, `helicopter`, `radar`, `jet_engine` (needs `aircraft_metal` AND `steam_turbine`) |
| Identities | Advanced piston aircraft (new, `air` A9 mainline, `aircraft_metal`). Strategic bombers (new, `strike_aviation` A9 mainline, `aircraft_metal`). Early jet fighters (new, `air` A9 **branch**, `jet_engine`). Helicopters (1102, `rotary_wing`). Early aircraft carriers (908, `carriers`). Radar-directed AA: a capability of the A8 AA identity once `radar` is known (sensor coverage, not a new identity). |
| Buildings | Air base: `building.airfield` tier 2 (paved runway, hangars, fuel store; `aircraft_metal`). Forward airfields outside the settlement network (§8). Radar sites (building charter §5.17). |
| Capabilities | Photographic reconnaissance: mapping that extends the REMEMBERED extent (D-040 B). Strategic bombing of production and infrastructure. Air mobility. Carrier-based sorties for piston aircraft. |
| Inputs | Aluminium (**missing**; its smelting needs electricity, see R4), engines, fuel in larger amounts, steel for runways and carriers (EC-1). |
| Status | **blocked** (aluminium, fuel, engines; carriers also need steel). |

The jet is a **branch** because jets did not replace piston aircraft at once. Making it the mainline would also be
meaningless: A9 has no later Age to modernize into.

### Flight III: Networked and stealth aviation (later A9)

| Item | Content |
|---|---|
| Knowledge | `stealth` (`composites` AND `radar` AND `computer` AND `aircraft`), `combat_drone` (`gps` AND `microprocessor` AND `aircraft`), `aerial_reconnaissance` AND `computer` (reconnaissance drones), `guided_missile` (SAM), `jet_engine` AND `steam_turbine` (jet-capable carriers) |
| Identities | Stealth fighters (`stealth_aircraft` 1103, `air` branch). Stealth bombers (new, `strike_aviation` branch). Reconnaissance drones (new, `unmanned_aircraft` mainline). Combat drones (1104, `unmanned_aircraft` branch). Jet-capable fleet carriers (new, `carriers` branch). Carrier-capable jets: the carrier attribute on the jet branch (§9.1). SAM batteries (new, `air_defence` branch). |
| Buildings | Airbases with hardened shelters (`building.airfield` tier 3). Datalink coverage (network graph; building charter §5.25). Civil airports are a separate, civil family. |
| Capabilities | Penetration of air defences (lower detection inside the equation, not a flat bonus). Persistent loiter reconnaissance. Strike without aircrew. |
| Inputs | Electronics and composites (**missing**), aluminium, fuel. |
| Status | **blocked** (electronics, composites, aluminium, fuel; digital networks for datalinks, M11+). |

**Historical anchors** (for orientation only, never as gates; Law 4):
- Wright Flyer, 1903 → Flight I;
- HMS Argus, the first full-deck carrier, 1918 → early carriers, Flight II, knowledge-reachable at the Flight I/II
  boundary;
- the first jet flight, 1939 → Flight II branch;
- jet carrier operations (angled deck, steam catapult), the 1950s → Flight III.

**Deviation from the brief, recorded.** The brief placed aircraft carriers in Flight III. Here early carriers are Flight
II and jet-capable carriers are Flight III. Reasons:
- the chronology (Argus 1918 predates the jet by two decades);
- the live entity gate (`aircraft` AND `steam_turbine`).

This is a routine design choice. The Director may override it without other changes.

---

## 4. Aviation dependency matrix

**Columns:**
- **Age** is the identity's or building's proposed Age (`*` = needs RC-1).
- **Prerequisites** are existing node ids.
- **Goods** names missing goods in bold.
- **Owner** is the milestone that owns the realizing mechanics.
- **Blockers** are the rows of unit charter §13 that must be live.

| Node / item | Age | Prerequisites | Unlocks (capability → realization) | Goods | Consumers (system) | Owner | Blockers |
|---|---|---|---|---|---|---|---|
| `glider` | A8 | `mechanics_newtonian` | three-axis control data → none (knowledge) | — | Research | M6 (existing) | none |
| `aircraft` | A8* (now A9) | `internal_combustion` AND `glider` | powered flight → 1101; light bombers; AA artillery; landing strip; aircraft works | timber, cloth, tools; **engines**, **fuel** | Recruitment (RC), Construction, Production, Supply (SU), Battle (BR: strike verb), Fog (sortie) | M7 design; realization with EC-4a | RC-1; EC-4a; RC, SU, BR live |
| `aerial_reconnaissance` | A9 | `aircraft` AND `photography` | photographic mapping → sortie extends REMEMBERED extent | **fuel** | Fog/Discovery (ADR-044, D-040 B) | M7 design; realization with EC-4a | EC-4a |
| `aluminium` | A9 | `electricity_generation` AND `soda_leblanc` | light structural metal → aluminium smelting recipe | **bauxite/alumina**, **aluminium**, **electricity input** | Production | EC-5a (§11) | R4 (energy owner) or a capacity proxy |
| `aircraft_metal` | A9 | `aircraft` AND `aluminium` | stressed-skin airframes → advanced piston aircraft; strategic bombers; air base tier 2 | **aluminium**, **engines**, **fuel** | Recruitment, Construction, Battle | M7 design; realization M11+ | EC-5a, EC-4a, EC-1 |
| `helicopter` | A9 | `aircraft` | rotary flight → 1102 (`rotary_wing`) | **aluminium**, **engines**, **fuel** | Recruitment, Battle (air assault) | M7 design; M11+ | EC-5a, EC-4a |
| `radar` | A9 | `vacuum_tube` AND `electromagnetic_field_theory` | detection → radar sites; radar-directed AA | **electronics** | Fog (detection channel), Battle | M7 design; M11+ | EC-5b |
| `steam_turbine` + `aircraft` | A8 knowledge, A9 identity | (existing entity gate) | early carrier → 908 | **steel**, **fuel** | Recruitment, naval movement (NV), Battle (sorties) | M7 design; M11+ | EC-1, EC-4a, NV |
| `jet_engine` | A9 | `aircraft_metal` AND `steam_turbine` | jet propulsion → jet fighter branch | **aluminium**, **engines**, **fuel** (heavier) | Recruitment, Re-equip (R1), Battle | M7 design; M11+ | EC-5a, EC-4b |
| `jet_engine` AND `steam_turbine` | A9 | (as named) | catapult/angled-deck operations → jet-capable carrier branch | **steel**, **fuel** | Recruitment, Re-equip, NV, Battle | M11+ | EC-1, EC-4b |
| `guided_missile` AND `radar` | A9 | (as named) | SAM → `air_defence` branch | **electronics**, **explosives**, **fuel** | Recruitment, Battle (intercept) | M11+ | EC-5b, EC-3, EC-4b |
| `stealth` | A9 | `composites` AND `radar` AND `computer` AND `aircraft` | low observability → 1103; stealth bombers | **composites**, **electronics**, **aluminium**, **fuel** | Recruitment, Re-equip, Battle (detection term) | M11+ | EC-5b, EC-5c, EC-5a |
| `aerial_reconnaissance` AND `computer` | A9 | (as named) | remote observation → reconnaissance drones | **electronics**, **fuel** | Fog (loiter VISIBLE), datalinks | M11+ | EC-5b; DN |
| `combat_drone` | A9 | `gps` AND `microprocessor` AND `aircraft` | strike without aircrew → 1104 | **electronics**, **explosives** | Battle, datalinks | M11+ | EC-5b, EC-3; DN |
| Forward airfield | A9 (with tier 2) | `aircraft_metal` (tier 2 gate) | basing outside settlements (§8) | timber, tools, stone; **fuel** stock | Construction (positioned sites), SU, occupation/capture (BR) | M7 design; M11+ | B2 positioned sites; EC-4a; M8 for allied access |
| Civil airport | A9 | `jet_airliner`, `air_navigation_agreements` | civil air edges | **fuel**, **steel**, **cement** | Transport (M11+), M8 agreements | M11+ | EC-1, EC-4b; M8 |

---

## 5. Distinct roles, not one generic aircraft

| Role | Identity (family) | First Flight | Gate | What makes it distinct in the rules |
|---|---|---|---|---|
| Reconnaissance / light aircraft | `military_aircraft` 1101 (`air`) | I | `aircraft` | Sortie radius from base; VISIBLE at the target; the light-attack action is a weak strike band; carries no strategic payload |
| Air superiority | 1101 → advanced piston (new) → jet branch (new) → `stealth_aircraft` 1103 | I → III | `aircraft` → `aircraft_metal` → `jet_engine` → `stealth` | Intercept action; air-combat band; escorts strike missions |
| Light bombing (tactical) | light bombers (new, `strike_aviation`) | I | `aircraft` | Strike on battle-map formations and fortifications within a short radius; it predates jets |
| Strategic bombing | strategic bombers (new, `strike_aviation` mainline A9) | II | `aircraft_metal` | Strike on production, infrastructure and depots at long radius. Damage is a construction-state debit (building charter §4.6), never a flat output penalty. |
| Stealth bombing | stealth bombers (new, `strike_aviation` branch) | III | `stealth` AND `aircraft_metal` | Lower detection probability inside the interception equation |
| Jets | early jet fighters (new, `air` branch) | II | `jet_engine` | Speed term in the intercept equation; heavier fuel demand; needs air base tier 2 |
| Carrier aviation | carrier attribute on identities (§9.1); 908 and the jet-capable carrier branch (`carriers`) | II (piston), III (jet) | `aircraft` AND `steam_turbine`; `jet_engine` AND `steam_turbine` | Basing on a moving water-edge agent. The sortie radius is measured from the carrier's position. |
| Rotary-wing | 1102 (`rotary_wing`) | II | `helicopter` | No runway; short radius; air-assault action moves a light formation |
| Drones | reconnaissance drones (new), 1104 (`unmanned_aircraft`) | III | `aerial_reconnaissance` AND `computer`; `combat_drone` | Loiter (sustained VISIBLE); datalink coverage required; no aircrew personnel at risk |
| Air defence | AA artillery (new, A8), SAM branch (new) (`air_defence`) | I, III | `rifled_artillery` AND `aircraft`; `guided_missile` AND `radar` | Reaction against strike missions; a counter cannot precede its threat, so the family begins with Flight I |

**Why bombers are a family, not branches of `air`.** ADR-031 §3 rule 2 converts a branch formation to the next mainline
identity at Age transition. A light bomber that is a branch of `air` would become an advanced piston fighter at A9. A
family of its own keeps the role:
- A8 light bombers modernize to A9 strategic bombers;
- stealth bombers stay a recruited branch.

This is the charter's split test (§5.2 of the unit charter): a persistent, distinct role.

---

## 6. Which systems realize what

**Research completion builds nothing** (D-044 R14). Each capability below has a named consumer. An item without a live
consumer stays at its status class.

| Realization | System (owner) | What it does | Status |
|---|---|---|---|
| Recruit an air formation | Recruitment (M7, row RC) | §6.5 predicate; draws aircrew adults (Ledger.Transfer) and materials (Ledger.Flow); location = aircraft works or air base | **blocked** (engines, fuel) |
| Re-equip (R1) | Recruitment (M7) | 1101 → jet branch, piston → stealth branch, and so on (§9.2; unit charter §6.6) | **blocked** (as targets) |
| Build a landing strip, air base or forward airfield | Construction (existing; positioned sites for forward airfields, B2) | Construction progress; materials through the empire stockpile (D-053) | landing strip: buildable from existing goods, but **no consumer** until air units exist, so **blocked** |
| Produce engines | Production (EC-4a recipe) | Engines from steel (EC-1) and tools in motor works | **blocked** (EC-1 in M7; EC-4a unscheduled) |
| Produce fuel | Production (EC-4a refinery recipe; `building.refinery`) | Crude → light fuel | **blocked** (EC-4a) |
| Maintain aircraft | Upkeep (M7, row SU) | Per-year fuel and spares draw × dtYears (Law 3) from the base stock; an unsupplied formation is grounded, not deleted | **blocked** (fuel) |
| Supply a base | Supply (M7, row SU) | Base stock refilled along the travel-cost path from a depot or settlement | M7 candidate (mechanism) / **blocked** (goods) |
| Sortie and strike | Battle resolvers (M7, rows BR, D-011 §5) | Strike mission on the battle map; intercept reaction | M7 candidate (verb) / **blocked** (units) |
| Reconnaissance sortie | Fog (ADR-044) + Discovery (D-040 B) | VISIBLE at the target this turn; with `aerial_reconnaissance`, extends the REMEMBERED extent | **blocked** (units) |
| UI | Unit card, base panel, sortie radius overlay | Shows only what the predicate says; refusal reasons name the missing good or system | follows the units |
| AI | AI recruitment and research policies (M7) | Recruits only legal identities; a research goal = the requirement of the next unknown identity of a family it fields (the road-class pattern, ADR-033 B) | M7 candidate (policy) |
| Tests / audit | §15 of the unit charter + §13 evidence | Conservation, delivery pins, populated tables, tie-dense ordering, attribution twins | with each packet |

**Deferred because they have no consumer:**
- civil air edges (Transport M11+);
- carrier combat as its own battle mode: naval battle stays auto-resolve (D-011 §5); carrier sorties feed the ordinary
  strike verb;
- air-to-air "dogfight" simulation, which is barred by D-011 §5.

---

## 7. Flight I industry: implementation and measurement

**No global multiplier.** A "Flight I industry bonus" applied as a permanent percentage to production would be a
free-floating modifier (Law 2) with no consumer. It is rejected.

### 7.1 What Flight I realizes in industry

1. **A recruitment location with capacity: aircraft works** (new type, gated by `aircraft`; building charter §5.11).
   - It is the only place an air formation can be recruited.
   - Its capacity limits how many air formations a polity can recruit per year (a rate × dtYears).
   - Its consumer is the recruitment predicate (§6.5 item 3).
2. **The engines recipe** in motor works (EC-4a). Its output has named consumers:
   - air recruitment;
   - later, tanks, motor transport and generators.
   - It draws labour through the existing production model, so the labour cost is visible in the ledger.
3. **The existing realized research effect.** `aircraft` already carries `fog.formationSightNodes` +0.5 (ADR-047).
   - It is not duplicated.
   - The reconnaissance sortie is a separate, unit-level mechanism.
   - The validation below proves the two are not counted twice.

### 7.2 How it is measured

**Attribution twins** (the ADR-052 / ADR-053 precedent). Two worlds with identical seeds:
- one with the aircraft-works capacity term on;
- one with it off (the switch-off twin returns the old hash byte for byte).

The packet reports, per polity and per year:
- air formations fielded;
- engines produced and consumed;
- fuel consumed;
- labour moved into motor works.

**Exact conservation:**
- engines produced = engines consumed + engines in stock (`long`, no epsilon);
- the same for fuel and for aircrew people.

**Spillover check:**
- every other good's output may change only by the labour and material reallocation the ledger explains;
- an unexplained change in grain or tools output fails the packet.

**"No inert operand" check:** the content validator refuses any operand or building output with no reading system.

**Optional, needs a ruling.** If the Director wants Flight I to raise civilian productivity, for example precision
manufacturing spilling into tools, the only lawful form is a coefficient inside a named recipe equation, read by
Production. It would have its own twin and measurement. It is not proposed here.

---

## 8. Forward airfields

A forward airfield is a positioned site outside the settlement network. It is a construction, not a unit.

| Question | Rule |
|---|---|
| **Who builds** | An army formation present at the site, using its **engineer allocation**. Engineers are category B, abstract military support, not a unit family (D-065 R10). The formation spends Action Capacity. Construction progress is a rate × dtYears (Law 3), and materials come from the formation's supply allocation through `Ledger.Flow`. |
| **Where (territorial permission)** | Legal at a position that is (a) in the builder's controlled territory, or (b) occupied by the builder's army in wartime (D-040 control is computed, not declared), or (c) in an ally's territory under an access agreement. Agreements are M8 diplomacy, so (c) is illegal until M8. Neutral territory is never legal. |
| **Siting** | Slope and terrain predicates (building charter §4.2); not inside an enemy ZOC (unit charter §6.4) |
| **Supply** | The site holds a stock (fuel, munitions, spares) refilled along the travel-cost path from a depot or settlement within supply reach (unit charter §6.7). Sorties draw from that stock. An empty stock grounds the based formations. |
| **Access** | The owner's formations. An ally's formations only under an M8 access agreement. |
| **Ownership** | The building polity. Building it does not change territorial control: control is computed from presence and administration (D-040). |
| **Capture** | If, at the end of the military phase, an enemy formation occupies the site and no defending formation is present, ownership transfers. The site's stock moves to the captor by `Ledger.Transfer` (conserved). Based formations first relocate to the owner's nearest base within sortie range (key: travel cost, then base id). With none, they are grounded at the site and resolved through the ordinary BattleOutcome (prisoners and equipment losses debited through the Ledger). |
| **Abandonment** | By order: the stock is withdrawn first, and anything not withdrawn is an explicit loss flow, never a silent deletion. By neglect: the site's integrity decays at a per-year rate while it is unsupplied and unstaffed. At zero the site is removed by the same explicit-loss rule. |
| **Determinism** | Sites are processed in id order; ids are allocated monotonically and never reissued; capture and relocation use composite keys with stable ties |
| **Owner** | Design: M7 (military). Positioned-site rows: Construction (B2 ruling). Allied access: M8. Realization: with the air families, so **blocked** (fuel). |

---

## 9. Reconciliation: carriers, reconnaissance, identities and migration

### 9.1 Carriers and the naval and mobile-agent proposals

- **Carriers stay their own family**, `carriers` (proposed key 19). The existing identity `aircraft_carriers` (908) is
  rehomed from `naval_combat` by the unit charter's §5.3 migration map (908: 9 → 19). Its key does not change.
- **A carrier is a MobileAgent on water edges** (D-040 B4; unit charter §7.13 and §7.17), with an air-capacity
  attribute. Embarked air formations are carried, like an army in naval transport.
  - Their sortie radius is measured from the carrier's current node.
  - Their fuel comes from the carrier's stock.
- **Carrier-capable is an identity attribute, not a family.**
  - It is true for 1101, the A9 advanced piston aircraft and the light bombers (piston aircraft flew from Argus).
  - It is false for strategic bombers.
  - The jet branch is carrier-capable only aboard the jet-capable carrier branch.
  The rule is one predicate: carrier class × identity.
- **Carrier combat is not M7 battle content.**
  - Naval battle stays auto-resolve (D-011 §5).
  - Carrier sorties use the ordinary strike verb.
  - A naval command battle is a post-M7 candidate.

### 9.2 Reconnaissance aircraft vs scouts, formations and agents

| Thing | What it is | What it reveals | Owner |
|---|---|---|---|
| Scouts (`reconnaissance`, proposed 22; identity 101) | A ground MobileAgent | VISIBLE around itself; lagging position reports by shadowing (D-039 B); permanent extent by survey (D-040 B) | M7.5 |
| Formation sight | A property of every formation | VISIBLE around the formation (`fog.formationSightNodes`) | existing (fog) |
| Reconnaissance sortie | An action of an air formation at its base | VISIBLE at a target within sortie radius for one turn | M7 design; blocked |
| Photographic reconnaissance | The same sortie with `aerial_reconnaissance` | Also extends REMEMBERED extent (mapping) | M7 design; blocked |
| Reconnaissance drones | `unmanned_aircraft` | Sustained VISIBLE while loitering and supplied by datalink | M11+ |
| Satellites | Space assets (building charter §11) | A pointed observer, never a map reveal (D-039 B3, D-040 B3) | M11+ |

**Scouts are not converted into aircraft.**
- Family 22 keeps its line; A8–A9 scouts stay ground agents (unit charter §8.1).
- Aerial reconnaissance is an air-formation action, so ADR-031 never turns a scout agent into an aircraft.

### 9.3 Identity and migration requirements (proposals; nothing is applied)

| Change | Kind | Migration / save effect | Test |
|---|---|---|---|
| `military_aircraft` (1101): identity Age A9 → A8 | `unit-families.json` data | No saved row can hold 1101 today (no recruitment exists). Modernization planning changes only for future rows. Measured, not assumed. | ADR-031 preview-equals-applied on a populated fixture; `a8_formation` / `a9_formation` counts |
| Advanced piston aircraft (new) as the `air` A9 mainline, gated by a per-identity `requires`: `aircraft_metal` | data, appended key | none | recruitment-predicate tests; ADR-031 A8 → A9 converts 1101 → this identity |
| Jet fighters (new) as an `air` A9 branch: `jet_engine` | data, appended key | none | never a modernization target; re-equip legality (unit charter §6.6) |
| `strike_aviation` family (proposed key 23): light bombers A8 mainline, strategic bombers A9 mainline (`aircraft_metal`), stealth bombers A9 branch (`stealth` AND `aircraft_metal`) | data, new family | none | modernization keeps bombers bombers; populated-table round trip |
| `air_defence` (proposed 14): AA artillery at A8 (`rifled_artillery` AND `aircraft`), SAM branch (`guided_missile` AND `radar`) | data, new family (already proposed) | none | counter can never precede its threat (validator) |
| `carriers` (19): 908 rehomed (§5.3); jet-capable fleet carriers as a branch (`jet_engine` AND `steam_turbine`) | data + migration (already in §5.3) | 908: family 9 → 19; branch flag cleared (already listed) | §5.3 tests |
| 1102 → `rotary_wing` (20), 1104 → `unmanned_aircraft` (21) | migration (already in §5.3) | as listed | as listed |

**Key rules** (unit charter §4.4):
- Keys are allocated only when the reviewed content change lands.
- Keys are append-only and never reused.
- The identity names above are working names; the display `name` is free to change.

---

## 10. Proposed research-content changes

These are **proposals**. They are not applied, and they need the TC-1 process (an approved change: STRUCTURE hash
re-pin with attribution).

| # | Change | Why | Size | Evidence required before adoption |
|---|---|---|---|---|
| **RC-1** | `aircraft`: `age` A9 → A8; ADR-045 re-score with A8 priors (difficulty 3.0 → 2.5, institutional 2.5 → 2.0; magnitude 9.5 → 8.5; content cost 66,900 → 33,450). No prerequisite change. | §2.1: the Age sweep blocks Flight I for most of A8. §2.3: the re-score keeps the effective cost between `glider` and `internal_combustion`. | One node. The STRUCTURE hash moves (content Age is in it). TEXT is unchanged, and costs are outside both hashes (`TreeClosureBaselineTests`). | **E1** four-seed rerun (1, 2, 42, 7) on the shipped and candidate configurations: A8/A9 entry turns, A8 cohort completion, and the D-063 R6 pincer for A8 within ±25 turns. **E2** `aircraft` completion turn vs A9 entry. **E3** TC-1 STRUCTURE re-pin with a single attributed cause. |
| RC-1a (follow-on, only if E1 shows A8 completion slips) | Recalibrate `pacingScale.technology[7]` (A8) by the D-063 R6 procedure | An added A8 node adds A8 workload | Tuning only | the same E1 battery |

**Rejected alternatives:**
- **A new A8 `powered_flight` node in front of `aircraft`.** Larger: a new node, a new key, a new STRUCTURE row and new
  text, for the same eligibility result.
- **Gating Flight I on `internal_combustion` AND `glider` through a per-identity `requires`, bypassing `aircraft`.**
  Causally wrong: powered flight is the `aircraft` node. It would also leave `building.airfield` at A9.
- **Leaving the tag and accepting the gate.** It contradicts ruling R3 (Flight I in late A8) for 89–121 turns per seed.
- **Moving `glider` earlier.** It is not the binding constraint for eligibility (§2.1); the AI's late `glider` is a goal
  artefact (§2.2).

**Not a graph change, but needed for the AI (§2.2).** The M7 AI recruitment policy adds one goal: the requirement of
the next unknown identity of a family the AI fields or wants. It follows the road-class pattern of ADR-033 B. This is AI
policy, owned by M7, and has its own four-seed measurement and attribution control.

**No other research change is needed.** `aircraft_metal`, `jet_engine`, `helicopter`, `aerial_reconnaissance`,
`stealth` and `combat_drone` already have causally valid prerequisites and stay A9.

---

## 11. Proposed resource packets

These are proposals. Each is a separate, reviewed economic-content change: goods, deposits, recipes, transport bulk,
consumption, and conservation tests. **No good is added to any recipe by this file.**

| Packet | Minimum content | Proposed owner | Blocks |
|---|---|---|---|
| **EC-1** metals (ruled into M7 by R2) | iron ore, iron, steel; charcoal/coal | M7, separate packet | engines (via EC-4a), carriers, air bases, runways |
| **EC-3** gunpowder and explosives (ruled into M7 by R2) | saltpetre, sulphur, gunpowder, explosives | M7, separate packet | bombs and munitions for strike missions, AA artillery ammunition |
| **EC-4a** light fuels and engines (**new split of EC-4**) | crude oil as a deposit channel; a refinery recipe crude → light fuel; an engines recipe (steel + tools → engines) in motor works; fuel stock rows at depots and bases | **open** (OD-3): earliest M10, otherwise M11+ | Flight I in full; tanks and motor transport (with EC-1) |
| **EC-4b** heavy and aviation fuels | the refinery's heavier fractions; pipeline and tanker bulk | M11+ | jets, SAM, civil aviation |
| **EC-5a** aluminium (**split of EC-5**) | bauxite/alumina deposit; electrolytic smelting recipe with an electricity input | M11+; its electricity input waits on **R4** (Q8/B5) | Flight II in full |
| **EC-5b** electronics; copper metal | copper metal, an electronics good | M11+ | radar sites, SAM, drones, stealth |
| **EC-5c** composites (**new**, materials) | a composites good (`plastics`-derived) | M11+ | stealth |

**Why EC-4 and EC-5 are split.**
- The smallest packet that unblocks a whole Flight is EC-4a, for Flight I.
- Aluminium is the Flight II gate, and it carries the energy dependency. Electronics does not need to wait for it.
- **Engines ride with fuel** because an engine has no consumer without fuel (the consumer rule).

---

## 12. Milestone ownership

| Milestone | Aviation scope | Not in scope |
|---|---|---|
| M6 (knowledge) | The nodes, closed under TC-1; RC-1 is a TC-1 change | anything that builds |
| **M7** (battle and military realization) | The air, strike, air-defence, carrier and rotary/unmanned family **designs**; the strike and intercept verbs; recruitment, upkeep and supply mechanisms; forward-airfield rules; re-equipment (R1); the AI goal; EC-1 to EC-3 as separate packets | carrier combat as a battle mode; any fuel, aluminium or electronics good; espionage operations; energy |
| M8 (politics) | Allied access agreements (forward airfields, overflight); `air_navigation_agreements` consequences | — |
| M9 (society) | Health effects of bombing on populations, through M9 equations | — |
| M10 (integration) | Whole-ladder validation; EC-4a at the earliest, if OD-3 rules so | new aviation mechanics |
| M11+ (depth) | EC-4b, EC-5a–c; realization of Flights II and III; civil aviation; digital datalinks | — |

---

## 13. Open decisions and the evidence each needs

| # | Decision | Why it is the Director's | Options | Recommendation | Evidence needed |
|---|---|---|---|---|---|
| **OD-1** | Approve RC-1 (`aircraft` → A8 with the re-score) | It is a TC-1 tree change | approve / reject / approve with RC-1a | approve, with E1–E3 run first | E1 four-seed pacing battery; E2 completion vs A9 entry; E3 attributed STRUCTURE re-pin |
| **OD-2** | Same-line "catch-up" re-equipment: a formation recruited as an older mainline identity, because of a material fallback, re-equipping to the current mainline identity within the same Age | It sits on the line between R1 (re-equip permitted) and D-047 ruling 18 (no Civ VI-style paid upgrade). Neither ruling covers it explicitly. | (a) barred: wait for the free Age-transition modernization; (b) permitted as a re-equip order | (a), the conservative reading. Cross-family and branch targets are covered by R1 and need no further ruling. | none beyond the ruling; if (b), a measurement of how often material fallback occurs over seeds 1, 2, 42, 7 |
| **OD-3** | When EC-4a (light fuels and engines) lands | A material scope and cost trade-off: without it, no A8–A9 military mechanised or air identity is realizable in M10's integrated validation | M7 (widens M7) / M10 / M11+ | M10, so the integrated validation covers mechanised and air militaries; not M7 | a sized packet plan; the list of identities it unblocks (§4) |
| **OD-4** = R4 (Q8 / B5) | Who owns the energy system | **Open by ruling.** Not answered here. | M10 / M11+ | none made | a measured list of mechanics that read electricity (aluminium smelting, electric traction, radar, grids); whether ADR-019 §5's electricity-as-capacity is enough for EC-5a; the M10 scope cost |
| OD-5 | Carriers in Flight II (here) vs Flight III (the brief) | A routine choice, recorded because it deviates from the brief | II / III | II for 908, III for jet carriers | none (chronology and the live entity gate, §3) |
