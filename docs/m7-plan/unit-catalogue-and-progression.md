# M7 Unit Charter: catalogue, progression, dependencies and validation (A1–A9 and endgame)

**Status: PROPOSED charter, for the Director's review.** This is a documentation and design file only.

**Nothing in this file changes:**
- runtime code, unit or family IDs or keys, content data (`unit-families.json`, `research.json`, `goods.json`, `sim.json`);
- the research and civics trees, defaults, the save schema or tests;
- `main`, tags or releases.

Every number in it is a provisional band, not a tuned value.

**Companion:** [Building and Infrastructure Charter](building-and-infrastructure-charter.md). Buildings, networks, wonders,
power, oil, nuclear and space infrastructure are specified there. This file is the authoritative home of:
- the unit taxonomy;
- the seven end-to-end dependency chains (§12);
- the dependency matrix (§13);
- the staged implementation sequence (§14);
- the balance and validation rules (§15).

These cover units and buildings alike.

**Verified base.** Branch `claude/new-session-yavghm`, head `b769933` (equal to the remote branch); `main` = `6e51a3e`.
The facts in §3 were re-read from that tree. Previous reports were treated as secondary evidence.

**Supersedes:** the 2026-10-26 proposal of the same name (commit `5382b85`). Its decisions are kept and extended:
- a culture-neutral common roster first;
- ADR-031 modernization unchanged;
- no invented goods;
- the family splits;
- the system-by-system sequence.

Its culture policy is carried into §1 and §6.10 unchanged in substance.

---

## Contents

1. [Executive decisions](#1-executive-decisions)
2. [Binding constraints](#2-binding-constraints-what-this-charter-may-not-change)
3. [What exists today (verified)](#3-what-exists-today-verified-at-b769933)
4. [How to read this charter](#4-how-to-read-this-charter)
5. [Taxonomy and reconciliation of the 12 families](#5-taxonomy-and-reconciliation-of-the-12-existing-families)
6. [Shared unit model](#6-shared-unit-model)
7. [Military family sheets](#7-military-family-sheets)
8. [Exploration, economic and civil agents](#8-exploration-economic-and-civil-agents)
9. [Commanders and special people](#9-commanders-and-special-people)
10. [Covert action, unrest and insurgency](#10-covert-action-unrest-and-insurgency)
11. [Counter matrix](#11-counter-matrix)
12. [End-to-end dependency chains](#12-end-to-end-dependency-chains)
13. [Dependency matrix](#13-dependency-matrix)
14. [Staged implementation sequence](#14-staged-implementation-sequence)
15. [Balance and validation](#15-balance-and-validation)
16. [Conflicts with existing decisions and open questions](#16-conflicts-with-existing-decisions-and-open-questions)
17. [Acceptance criteria](#17-acceptance-criteria)

---

## 1. Executive decisions

1. **One common, culture-neutral roster first.**
   - Every polity reaches the same families when it meets the knowledge, material, location, building and institutional
     conditions.
   - Culture-specific unique units and buildings come later, as a thin data overlay, once the culture system has an owner
     (§6.10).
2. **The 12 existing families stay, with their keys and identity keys.**
   - New families are added only where a role is genuinely distinct.
   - Three splits are proposed, not applied: scouts out of light infantry, submarines and carriers out of naval combat,
     helicopters and drones out of air.
   - Each split needs a deterministic save migration (§5.3).
3. **ADR-031 is preserved exactly.**
   - Modernization is automatic and free at Age transition, follows the mainline only, and has a no-successor rule.
   - D-047 ruling 18 forbids Civ VI-style paid manual upgrades.
   - This charter introduces **no paid upgrade of any kind**.
   - What the earlier brief called "transformations" are defined in §6.6. Moving a formation into a *different role* is
     a re-equipping (a new recruitment that consumes the new role's inputs and keeps people and experience). It needs
     your ruling that it is not a disguised paid upgrade (§16, Q1).
4. **Formation-level battle stays.** D-011 is unchanged:
   - formations are atomic;
   - battles are WEGO command pulses;
   - an AutoResolver and a TacticalResolver share one contract;
   - the parity rule holds.
   Units in this charter are formations or MobileAgents. There is no individual-soldier control.
5. **Ages are computed states, not calendar gates.**
   - An identity's `age` is modernization and history metadata.
   - Recruitment eligibility is a predicate over knowledge, materials, location, buildings and institutions (§6.5).
   - No rule reads a date, an era band or a turn number.
6. **Blocked chains stay blocked.** The economy has 14 goods.
   - Missing goods: iron, steel, coal, oil, refined fuel, aluminium and uranium (the brief's list), plus
     mounts/horses, gunpowder and explosives, copper metal, electronics and composites (found by this audit).
   - Every identity or building whose realization needs one is marked **blocked by missing goods/system** until a
     separate, reviewed economic-content change adds the goods, deposits, recipes, transport, consumption and
     conservation tests (§14, EC packets).
7. **Declarations are not mechanics.**
   - Spies, unrest, cultural influence, nuclear weapons, power grids, cyber and space assets each get an owning
     milestone and the state they require (§13).
   - Writing them into data without that owner implements nothing, and this charter does not pretend otherwise.
8. **No precise balance numbers.**
   - Stats are normalized provisional bands (§4.3).
   - Each family names the matchup and calibration evidence that must exist before a number is chosen (§15.6).

---

## 2. Binding constraints (what this charter may not change)

| Source | What it fixes | How this charter complies |
|---|---|---|
| D-011 (battle layer) | Formation-level command; WEGO pulses; AutoResolver/TacticalResolver contract and parity; casualties debited from real cohorts through the Ledger; era scaling "as data + a few new verbs" | All military units are formations inside Army MobileAgents, or MobileAgents. There are no per-soldier stats. Verbs stay era-constant (move, attack, hold/entrench, screen, flank, commit reserve, withdraw; later bombard and air strike). |
| ADR-031 + D-047 ruling 18 | Automatic, free modernization at Age transition; mainline only; branches never targeted; `noSuccessor` preserve/generic; unknown identities preserved; preview equals applied; no paid manual upgrades | Unchanged. §6.6 adds only what ruling 18 permits: separate recruitment of branch identities, and re-equipping as a new recruitment (Q1). |
| D-043 B1–B8 | Universal MobileAgent (stable id, owner, position, movement capability, Action Capacity, action state, domain/type, capabilities, lifecycle). Action Capacity is universal; the action set is agent-specific. Special people are temporary MobileAgents. Armies have internal composition. Cultural groups are MobileAgents. Off-road movement with terrain/infrastructure cost. War Pulse. | §6.1 entity types; §6.3 Action Capacity (D-052 §4's "Action Points"); §9 special people; §9.6 cultural groups. |
| D-047 rulings 15–17 | Special people have finite charges, owner = recruiter, lifecycle ends. Recruitment model A (institutions generate points; civilizations compete). Zones of Control are geometric over continuous x/y, not hex. | §9; §6.4. |
| D-037 A1, E3, E4 | **Nothing spawns:** every actor originates from simulated population. Insurgency runs on population support. **Espionage accelerates; it does not create** (ratio of existing grievance; M10+, with hooks earlier). | §10 derives partisans, rioters and rebel formations from population segments. Espionage amplifies existing grievance only. |
| D-039 A, B, D | Command friction is delay, not degradation. Reconnaissance is an investable capability. Fortification is computed from built works, not a unit or flag. Starvation is a first-class siege outcome. | §6.7; siege family §7.8; fortification lives in the building charter. |
| D-040 B4, C6 | Sea travel extends the network's edge types. Distance means travel cost over the network, not Euclidean distance. | Movement budgets are path-cost budgets (§6.2). Naval movement uses water edges (§7.13). |
| D-049 | Taxation is continuous pressure. Segment revolt emerges locally. Revolt-born polities inherit the parent's Age. | Riots and revolts are population states first (§10.5). Nothing here adds a revolt switch. |
| D-052 §4, D-062 R2 | M7 owns unit movement and Action Points/Action Capacity. CR-017 §3 and §5 must be ruled before any M7 movement packet. | §14 stage M7.0 lists the rulings that come first. |
| D-062 R10 | The nuclear knowledge progression is a design direction; no nuclear mechanics in M6. | Nuclear chains are M10/M11+ and blocked (§12.3–12.4). |
| CLAUDE.md laws 1–7 | Conservation through the Ledger; mechanisms over modifiers; dt-correct rates; no calendar gates; determinism; isolation; `long` stocks. | §15 turns each law into tests. |
| TC-1 (PROPOSED) | Tree node content and dependencies are frozen except for defects or approved changes. The STRUCTURE hash includes the `entities` list. | No node or entity is added. Prerequisites are existing node ids. Gates for identities without a `realizedBy` entity are proposed in `unit-families.json` (§6.5), so the TC-1 hash does not move. |

---

## 3. What exists today (verified at `b769933`)

| Item | State | Evidence |
|---|---|---|
| 12 unit families, 66 identities (7 of them branches), all PROVISIONAL, **no combat statistics** | Data only. Read by the modernization planner. | `Sim.Data/content/unit-families.json`; ADR-031 §2.4, §3 |
| Founding warband | **Existing.** One `MilitaryUnits` row per Empire with a capital. A token: no personnel drawn, no goods consumed, no combat. | ADR-031 §2.4; `WorldFounding.FoundInitialFormations` |
| Free modernization at Age transition | **Existing** (AgeTransitionSystem; `MilitaryQuery.PlanFor`; logged in `UnitConversions`) | ADR-031 §3; `UnitModernizationTests` |
| `MilitaryUnits` row | **Existing:** `(Id, Owner, Family, Identity, Location, X, Y, Experience, Army)`. No personnel, supply, movement or Action Capacity. | ADR-031 §2.1 |
| 23 `unit.*` research entities | **Declared,** knowledge-gated, DEFERRED (no recruitment) | `research.json` entities; `docs/research-gameplay-unlock-audit.md` §4; `docs/deferred-entity-realization-plan.md` §E |
| Explore order (kind 14) | **Existing.** Widens a settlement's explored radius. No unit moves; M7 is to replace it with mobile units. | D-062 §2; ADR-044 |
| Prospect order (kind 15) | **Existing.** Reveals concealed ores at an own settlement. | D-062 §2; ADR-044 |
| Found Settlement order (kind 9) | **Existing.** Sends a population party (`colonization.orderPartyFraction`). The party is not a moving agent. | ADR-036; `sim.json colonization` |
| Notables (`NotableRow`) | **Existing:** a notable is a person extracted from a bucket (M4 R-1), with a born/die/defect lifecycle | D-043 F14; `Sim.Core/State/WorldState.cs` |
| Segment revolt | **Existing:** grievance per population segment; local tipping; revolt-born polity inherits the Age | D-049 §2–§4; `Sim.Core/Systems/Revolt` |
| Trade | **Existing** as TradeArbitrage flows between settlements. No trader agents. | `sim.json trade`; `activity.trade` |
| Fog | **Existing:** UNKNOWN / VISIBLE / REMEMBERED per polity; formations give VISIBLE | D-062 §2; ADR-044 |
| Movement, Action Capacity, ZOC, recruitment, supply, combat | **Not implemented** | ADR-031 §6; D-052 §4 |
| Goods | 14: grain, livestock, fish, timber, stone, clay, copper-ore, tin-ore, fiber, hides, bronze, tools, pottery, cloth; 4 recipes | `goods.json` |

---

## 4. How to read this charter

### 4.1 Status classes (exactly one per item)

| Status | Meaning |
|---|---|
| **existing** | Live in code or shipped content at `b769933`. Named evidence in §3. |
| **M7 candidate** | Proposed for an M7 Battle Layer slice (§14). It can be realized with existing goods and the systems M7 owns. |
| **M8+** | Owned by M8 Politics/Diplomacy, M9 Society, M10 Integration or M11+. The owner is named in each sheet. |
| **blocked** | Blocked by missing goods/system: realization needs a good or system that does not exist. It stays blocked until the named EC packet or system lands (§13), whichever milestone owns its mechanics. |
| **design-only** | Specified here so the schema leaves room for it. A likely owner may be named, but no milestone slice is scheduled. Building it needs a Director decision. |

An identity can be **existing as data** (its row in `unit-families.json`) and **blocked** for recruitment. Sheets show
both where they differ.

### 4.2 Composition types

| Type | What it is | Table owner (proposed) |
|---|---|---|
| `army_formation` | An atomic formation inside an Army MobileAgent (D-043 B5; D-011 §3) | `MilitaryUnits` (exists) |
| `mobile_agent` | A MobileAgent in its own right: fleet, air wing, strategic force, scout, convoy, diplomat | MobileAgent table (M7, new; ownership re-split per ADR-031 §2.3) |
| `special_person` | A temporary MobileAgent with finite charges (D-047 rulings 15–16) | Special-people table (owner: §13 row SP) |
| `cultural_group` | A MobileAgent group that performs (D-043 B6) | Same as `special_person` |
| `population_state` | Not a unit: a segment state (grievance, riot, revolt). It becomes a formation only by organization (§10). | Revolt / unrest systems |
| `asset` | Not a unit after deployment: a satellite, a warhead stockpile, a power line | Building charter |

### 4.3 Stat bands (normalized, provisional)

All values are **relative to the same-Age cohort**, not absolute.
- **Anchor:** within an Age, the heavy-infantry realization is the reference formation, with Integrity 50 and Melee 50.
- **Cross-Age strength:** progression between Ages comes from equipment and doctrine and is set by calibration (§15.6),
  never by a per-Age multiplier (Law 2).

| Band | Range | Reading |
|---|---|---|
| — | 0 | capability absent |
| VL | 1–15 | token |
| L | 15–35 | weak |
| M | 35–55 | reference |
| H | 55–75 | strong |
| VH | 75–95 | defining strength of the role |

**Movement** is a multiplier on the reference foot formation's path-cost budget. The budget is held per refresh and derived
from a per-sim-year rate × dtYears (§6.2):
- foot 0.9–1.1;
- slow or siege 0.4–0.8;
- mounted 1.5–2.2;
- motor 2.5–4.0, which is blocked by fuel.

**Battle range** is in battle-grid cells of D-012's coarse grid:
- short 1–2;
- medium 3–5;
- long 6–12;
- indirect, beyond line of sight.

**Action Capacity** is in whole actions per refresh: 1–3 for most agents. Specific actions declare their own cost.

These bands are placeholders that let roles be compared. §15.6 states what must be measured before any is replaced by a
number.

### 4.4 Identifier conventions

- Existing family keys 1–12 and identity keys (101…1202) are **never reused or renumbered**.
- Proposed new military families carry a **proposed, unallocated** key (13–22). Proposed identities show
  `(new)` and no key. Keys are allocated only when the reviewed content change lands, append-only.
- Reconnaissance (22) is a `MilitaryUnits` family with composition `mobile_agent`, like the naval families. The other
  agents in §8–§10 are not `MilitaryUnits` families; they get their own tables and id spaces.

---

## 5. Taxonomy and reconciliation of the 12 existing families

### 5.1 Domains, classes and owners

Every class the brief names appears below exactly once, with its home and status.

| Domain | Class / subclass | Home (family or table) | Composition | Status |
|---|---|---|---|---|
| A. Military land | Melee / line infantry | `heavy_infantry` (3) | army_formation | existing (data, warband) / M7 candidate |
| | Anti-cavalry | `spear_anti_cavalry` (2) | army_formation | M7 candidate |
| | Skirmishers / light infantry | `light_infantry` (1) | army_formation | M7 candidate |
| | Ranged infantry | `ranged_infantry` (4) | army_formation | M7 candidate |
| | Light cavalry | `light_cavalry` (5) | army_formation | blocked (mounts) |
| | Heavy cavalry → **armour** | `heavy_cavalry` (6): chariots … tanks | army_formation | blocked (mounts; tanks: steel, fuel) |
| | Ranged cavalry | `ranged_cavalry` (7) | army_formation | blocked (mounts) |
| | Siege / artillery | `siege` (8) | army_formation | M7 candidate (A4–A5); blocked A6 (iron fittings), A7+ (gunpowder) |
| | **Engineers** (combat) | `combat_engineers` (proposed 13) | army_formation | M7 candidate (A4–A7); blocked A8+ (explosives) |
| | **Air defence** | `air_defence` (proposed 14) | army_formation | blocked |
| | **Logistics** (military supply) | `military_supply` (proposed 15) | army_formation | M7 candidate after the draught ruling (Q-C); blocked A8+ |
| | **Medical** (field) | `field_medical` (proposed 16) | army_formation | M7 candidate (A5–A7); blocked A8+ |
| B. Naval | Naval surface combat | `naval_combat` (9) | mobile_agent | M7 candidate (A4–A6); blocked A7+ (cast-iron cannon) |
| | Naval transport | `naval_transport` (10) | mobile_agent | M7 candidate (A1–A7); blocked A8+ |
| | **Naval raiders** | `naval_raiders` (proposed 17) | mobile_agent | M7 candidate (A5–A6); blocked A7+ |
| | Submarines | `submarines` (proposed 18; rehomes identity 907) | mobile_agent | blocked |
| | Carriers | `carriers` (proposed 19; rehomes identity 908) | mobile_agent | blocked |
| C. Air, missile, space | Fixed-wing aviation | `air` (11): military aircraft, stealth branch | mobile_agent | blocked |
| | Rotary-wing | `rotary_wing` (proposed 20; rehomes 1102) | mobile_agent | blocked |
| | Unmanned aircraft | `unmanned_aircraft` (proposed 21; rehomes 1104) | mobile_agent | blocked |
| | Strategic missiles | `strategic_missile` (12) | mobile_agent | blocked (realization M11+; governance M10) |
| | Nuclear weapons | **not a unit**: warhead stockpile asset (§12.4) | asset | blocked + M10 |
| | Space assets | **not units**: satellites and launch campaigns (building charter §11) | asset | blocked (M11+) |
| D. Exploration and territory | Scouts | `reconnaissance` (proposed 22; rehomes identity 101) | mobile_agent | existing (Explore order) → M7 candidate |
| | Settlers | colonization party (agent table) | mobile_agent | existing (order 9) → M7 candidate (transit) |
| | Builders | settlement labour (existing); field work crews | — / mobile_agent | existing (labour) / design-only (crews) |
| | Prospectors | prospecting party (agent table) | mobile_agent | existing (order 15) → M7 candidate |
| E. Economic and support | Traders | trade flows (existing); trade convoys | mobile_agent | existing (flows) / M8+ (convoys) |
| | Diplomats | envoys and embassies | mobile_agent | M8+ (M8) |
| | Researchers | scientists (special people) and field expeditions | special_person | M8+ (row SP) |
| | Medical / logistics agents | army-attached families 15–16; civil relief teams | army_formation / mobile_agent | M7 candidate / M8+ (M9) |
| F. Command and notable people | Generals / admirals | commanders drawn from notables | special_person | M7 candidate (command); traits M8+ |
| | Heroes | age-specific legendary figures | special_person | design-only |
| | Artists, writers, musicians | special people | special_person | M8+ (M9) |
| | Rock bands and touring groups | cultural groups | cultural_group | M8+ (M9) |
| G. Covert and unrest | Spies (intelligence) | intelligence agents | mobile_agent | M8+ (M8 hooks only; operations M10, Q9) |
| | Infiltrators, saboteurs, agitators | covert operatives | mobile_agent | M8+ (M10, D-037 E4) |
| | Riots / revolts | population state; rebel formations by organization | population_state → army_formation | existing (segment revolt) / M8+ (unrest ladder) |
| | Partisans | insurgent formations from population support | army_formation | M8+ (M8; foreign support M10) |
| | Cyber agents | cyber operation teams | mobile_agent | blocked (digital networks, M11+; governance M10) |

### 5.2 Reconciliation of the 12 existing families

| # | Family | Disposition | Reason |
|---|---|---|---|
| 1 | `light_infantry` | **Keep, shrink.** Identity 101 `scouts` is rehomed to `reconnaissance` (22). The family begins at A3 with `skirmishers` (102). | A unit that moves independently and reveals fog is a MobileAgent. A skirmish screen inside an army is a formation (D-043 B5). Keeping both in one family makes modernization turn a scout agent into a formation. |
| 2 | `spear_anti_cavalry` | Keep unchanged | Line end after A7 → `heavy_infantry` is historically grounded (socket bayonet) |
| 3 | `heavy_infantry` | Keep unchanged | Complete A1–A9. Carries the founding warband. `mechanized_infantry` (308) is the mechanized realization of line infantry, not armour. |
| 4 | `ranged_infantry` | Keep. `longbowmen` (405) stays a recruitable branch, never a modernization target. | ADR-031 §3 rule 2 |
| 5 | `light_cavalry` | Keep. `horse_scouts` (501) stays a screening formation. `armoured_reconnaissance` (506) stays its A9 realization. | Distinct from the independent reconnaissance agent (22) |
| 6 | `heavy_cavalry` | Keep. It **is** the armour line: `tanks` (605) is the A9 realization of the breakthrough role. No separate armour family. | Splitting would break the existing modernization line (cuirassiers → tanks) for no new role |
| 7 | `ranged_cavalry` | Keep unchanged | Line end after A7 → `light_cavalry` |
| 8 | `siege` | Keep. Review rams at A4 against `siege_ram` (A4): consistent. | Its consumer is fortification (D-039 D1) |
| 9 | `naval_combat` | **Keep for surface combat.** Rehome branch 907 `nuclear_submarines` → `submarines` (18) and branch 908 `aircraft_carriers` → `carriers` (19). | A submarine and a carrier are not same-role branches of a destroyer |
| 10 | `naval_transport` | Keep unchanged | Logistics role, not combat |
| 11 | `air` | **Keep for fixed-wing combat aviation.** `military_aircraft` (1101) mainline, `stealth_aircraft` (1103) branch. Rehome 1102 `helicopters` → `rotary_wing` (20) and 1104 `combat_drones` → `unmanned_aircraft` (21). | Rotary-wing and unmanned aircraft differ in basing, range, cost and risk |
| 12 | `strategic_missile` | Keep. `icbm_forces` (1202) stays a same-Age branch, recruited not upgraded. | Strategic delivery, not conventional combat |

**No identity is duplicated:**
- Rehoming keeps every identity key. Only the `Family` field of a saved row changes, by migration (§5.3).
- The new families' identities have no overlap with existing ones. Where names could collide, the role difference is
  stated: `outriders` (agent) vs `horse_scouts` (formation); recon drones (unmanned) vs `armoured_reconnaissance`
  (formation).

### 5.3 Migration rule for the proposed splits (not applied)

When the reviewed content change lands, a saved `MilitaryUnits` row whose `(Family, Identity)` is rehomed is rewritten
by a pure, deterministic map applied at load.

| Identity key | Old family | New family |
|---|---|---|
| 101 | 1 | 22 |
| 907 | 9 | 18 |
| 908 | 9 | 19 |
| 1102 | 11 | 20 |
| 1104 | 11 | 21 |

**The content change must also:**
- clear the `branch` flag on 908 `aircraft_carriers` and 1102 `helicopters`, which become the mainline of their new
  one-identity families (ADR-031 never targets a branch);
- keep `branch: true` on 907 `nuclear_submarines` and 1104 `combat_drones`, each under a new mainline identity
  (submarines, reconnaissance drones);
- remove the moved identities from the parent families' `branches[]` entries (`naval_combat` atAge 9; `air` atAge 9);
- give `reconnaissance` (22) composition `mobile_agent`.

**Required tests:**
- a populated-table round trip before and after;
- a hash change attributed to the migration alone, with a switch-off twin;
- a refusal test for any unmapped pair;
- ADR-031 rule 5 holds: unknown identities are preserved.

Rehoming 101 also moves the founding scouts out of the army composition. Today no saved world holds a scout row, because
only warbands are founded. The migration is therefore expected to be a no-op on every existing save. That must be
measured, not assumed.

---

## 6. Shared unit model

### 6.1 Entity types and their common state

All MobileAgents carry D-043 B1's common state:
- stable id;
- owner;
- position (continuous x/y, see §6.2);
- movement capability;
- Action Capacity;
- current action state;
- domain/type;
- capabilities;
- lifecycle/status.

Formations live inside an Army MobileAgent and carry D-011 §3's state: strength, cohesion, fatigue, ammunition where
relevant, facing and frontage.

| Field group | Formation | Mobile agent | Special person / cultural group |
|---|---|---|---|
| Personnel (people, conserved) | yes: adults transferred from buckets (Law 1) | yes for convoys, settlers and diplomats; crews for ships and aircraft | yes: one person (a notable) or a small group |
| Strength / integrity | yes | hull/airframe integrity for vessels and aircraft | no (alive / incapacitated) |
| Cohesion / morale / fatigue / experience | yes | crew experience; morale for fleets | skill level (predefined, D-043 B3) |
| Equipment state | yes (tier from identity) | yes | no |
| Supply state | yes | yes (range, fuel where modelled) | travel funds only |
| Charges | no | no | yes: finite (D-047 ruling 15) |
| Stealth / exposure | no (scouting is a capability) | covert agents only | covert agents only |

### 6.2 Movement: path cost, not tiles

The rules:
- **A budget per refresh.** Movement is a budget of path-cost units per refresh (strategic turn, or War Pulse in war:
  D-043 B8), spent along a route.
- **Cost from terrain and networks.** The route cost comes from the movement-cost raster (slope, water) plus
  infrastructure (the road classes the RoadDevelopmentSystem builds) and D-043 B7's terrain classes (forest, hill,
  mountain, desert). The raster does not yet have the latter (D-043 F35); adding them is an M7 movement packet item.
- **Dimensions.** A route's cost is dimensionless relative to ideal ground (`transport.riverCostFactor` is already
  denominated that way). The budget is `mobility multiplier × reference budget × dtYears`-consistent, so it is rate
  per sim-year (Law 3).
- **No tiles.** No hex or square tile count is used, and Civ VI's movement points are not copied.

**Open first.** CR-017 §5 is open: D-009/D-010 put armies on the network graph, while D-043 B7 says off-road continuous
x/y. The charter is compatible with either reading:
- a path-cost budget works on a graph with off-road edges;
- it also works on a cost raster with network shortcuts.

The ruling is a precondition of slice M7.2 (§14).

**Domains:**

| Domain | Movement basis | Status |
|---|---|---|
| Land | path cost (raster + roads + terrain classes) | M7 candidate |
| Water (river, coastal, ocean) | water edges extending the network (D-040 B4); draught and hull class limit edges | M7 candidate (transport packet; Q-A, Q-C, Q-E, sea edges ruled together) |
| Air | range from a base: sortie radius, not path cost | blocked (fuel; basing system) |
| Covert | path cost, plus exposure rolls at borders and targets (stream-based RNG) | M8+ |

### 6.3 Action Capacity, separate from movement

- **Separate from movement.** Movement spends the movement budget. Actions spend Action Capacity (AC). D-052 §4 calls
  this "Action Points"; this charter uses the ratified D-043 term.
- **The coupling between them is explicit.** An action may declare that it consumes all remaining movement (entrench),
  that it requires the agent to be stationary for the refresh (siege set-up), or that it ends movement (attack from
  march).
- **Multi-turn actions** (bridge building, establishing a spy network, a performance tour) hold an action state with
  progress. Progress accrues as a rate × dtYears (Law 3), never as "N turns".
- **The universal budget, agent-specific action sets** (D-043 B2). No giant shared action list.

| Agent kind | Typical action set (illustrative, not exhaustive) |
|---|---|
| Formation (in battle) | D-011 verbs: move, attack, hold/entrench, screen, flank, commit reserve, withdraw; bombard; air strike |
| Army (strategic) | march, entrench, besiege, forage, split, merge, garrison, embark/disembark |
| Engineers | build field works, bridge, breach, clear obstacles, repair a road segment |
| Scout / reconnaissance | observe, shadow (report a lagging position, D-039 B), survey |
| Settler party | travel, found settlement (the existing order kind 9 semantics) |
| Prospector | prospect (the existing kind 15 semantics, at the agent's location) |
| Diplomat | present credentials, negotiate, gather open information |
| Covert operative | establish access, gather, sabotage, agitate (§10) |
| Special person | use a charge; cultural group: perform |

### 6.4 Zones of Control (D-047 ruling 17)

- **Geometric.** A ZOC is a region around a hostile formation or Army: a disc of path-cost radius, shaped by terrain.
- **Effect.** Entering it spends extra movement and ends the move. Leaving it costs AC. Neutral and friendly territory
  are explicit cases.
- **Queryable.** Pathfinding can ask whether a path crosses a ZOC.
- **Deterministic.** Formations are processed in roster order, so overlapping ZOCs resolve the same way every run.
- **No God object.** Each Army publishes its ZOC to a table, and movement reads that table.
- **Which units project ZOC:** combat formations only. Not scouts, convoys, special people or covert agents.

### 6.5 Recruitment eligibility and conservation

An identity is recruitable by a polity at a location **if and only if** all of these hold. One predicate is used by the
simulation, the UI and the AI.

1. **Knowledge.** The identity's `realizedBy` entity requirement holds.
   - For identities without a `realizedBy`, a proposed per-identity `requires` expression in `unit-families.json`
     holds, validated against `research.json` ids.
   - This keeps the trees closed under TC-1 (no new `unit.*` entity).
2. **Latest realizable identity.** Among the family's identities whose own predicate (items 1 and 3–7) holds, the
   recruitable one is the latest in the family's line order. Branch identities whose predicate holds are recruitable
   alongside it.
   - **The Age is not read.** That keeps recruitment free of Age-as-input (Law 4; D-039 A5); only modernization reads
     the Age, as ruled in ADR-031.
   - **Material blockers fall back, not fail.** A polity at A4 without iron recruits `bronze_swordsmen` (302), not
     nothing. Every Age therefore keeps a recruitable land force wherever materials allow.
   - **Known consequence of ADR-031.** Free modernization still converts existing formations past material blockers,
     because equipment is declarative. A 302 formation becomes `iron_infantry` at A4 with no iron. This is existing ruled
     behaviour, recorded under Q5, not changed here.
3. **Location.** An own settlement, or a recruiting building where the family requires one (building charter §5.11).
4. **Materials.** The listed goods can be drawn from the empire stockpile, as construction does under D-053. They are
   consumed through `Ledger.Flow`.
5. **Personnel.** Adults are transferred from the settlement's buckets into the formation through `Ledger.Transfer`.
   People are conserved: casualties are later debited from these people (D-011 §2).
6. **Support.** The owner can carry the upkeep under the adopted policy, or the order is refused.
7. **Rule support.** M7 rules consume the role. A family with no live consumer (for example air with no basing) is not
   recruitable.

**Atomicity.** A recruitment order either completes in full or reserves nothing, on the existing reservation pattern
(D-055).

**Nothing spawns.** The founding warband is the one existing free formation (ADR-031). It stays a token until the
personnel model lands, and at that slice it is reconciled by drawing its personnel at founding. That is a measured,
attributed golden move.

### 6.6 Modernization, branches and transformation

| Mechanism | Trigger | Cost | Status |
|---|---|---|---|
| **Modernization** | The owner's Age transition (ADR-031) | **Free.** Creates and destroys no stock. Equipment is declarative. | existing |
| **Branch recruitment** | An ordinary recruitment of a branch identity (longbowmen, stealth aircraft, ICBM forces) | Its own recruitment inputs | M7 candidate (branches blocked where their goods are) |
| **Line end** | Age transition past `lineEndsAfterAge` | Free, per `noSuccessor` | existing |
| **Re-equip (role change)** | An explicit order converting a formation to a different family, keeping its people and experience (for example spearmen → ranged infantry) | The target identity's recruitment **materials** only. People are not re-drawn. | **Not proposed until ruled (Q1).** Default: disband and recruit anew. |
| **Same-role, same-Age paid upgrade** | — | — | **Not proposed** (D-047 ruling 18) |

**The tension behind Q1.**
- Modernization consumes nothing, while recruitment of the same identity consumes goods. A polity therefore gets free
  modernized formations, which is the ruled intent.
- The charter keeps that.
- The calibration consequence: standing armies modernize for free, so upkeep, not re-equipment, is the long-run
  military cost.

**Requirements on the re-equip order, if ruled:**
- source and target identities;
- legality;
- inputs;
- location;
- state preservation (owner, location, people, experience, army membership, supply);
- atomicity and cancellation;
- replay;
- a turn-exact delivery pin.

### 6.7 Command, supply and upkeep

**Command friction (D-039 A).**
- Order latency is delay, not degradation.
- It depends on the distance to the commander or capital, and on communications: courier relays, telegraph, radio.
  These are building-charter items consumed as a latency term inside the delivery equation.

**Supply.**
- Each formation has a supply demand per sim-year. Food is grain-equivalent and comes from the existing goods.
- Supply is drawn along the network from a depot or settlement.
- Throughput is limited by route capacity, which roads and later rail raise.
- Foraging draws on the local hinterland and creates grievance in it.
- Out of supply: cohesion and strength attrition as rates × dtYears.

**Upkeep.** Food per person plus equipment wear (tools, bronze, cloth or hides at the family's equipment class), as
rates per sim-year through `Ledger.Flow`.

**Siege and starvation (D-039 D5).**
- This runs on the food system.
- It requires B-2 store bounding, which is satisfied by the M5 bounded stores.
- It must be re-verified before slice M7.4.

### 6.8 AI rules shared by every family

These rules apply to every family:
- **Single path.** The AI emits ordinary orders through the same predicates (ADR-033 D5 `AiOrders`).
- **No privileged information.** It reads only the polity knowledge seam: no omniscient reads (ADR-037; the P7 leak
  gate).
- **Deterministic choice.** Every choice is a composite-key ordering `(score, id)` with an integer tie-break, and ships
  with a tie-dense test (CLAUDE.md).
- **Role fill.** The AI fields a target composition by family role (screen : line : ranged : shock : siege : support).
  It is derived from observed threats, never from a hidden per-Age table.

### 6.9 Educational descriptions (requirements common to every unit)

Each identity's card must state:
- what the force was;
- when and where it emerged (the existing `historicalEmergence`);
- what it required: knowledge, materials, organization;
- what it was good and bad at;
- **what the simulation does and does not model for it today.**

A blocked identity must say which good or system it waits for, in the "not simulated yet" voice the M6 cards already use
(`FutureEffectsTests`). Text must be neutral and culture-neutral. No unit card claims an effect that no system consumes.

### 6.10 Culture policy (carried over)

- **Common roster first.** One shared roster and the same rules for every civilization. Geography, materials, transport,
  population and infrastructure create the differences.
- **Later overlay.** Culture-specific variants come later, as a thin overlay:
  - a unique replacement in the same role and unlock window, with a trade-off;
  - a narrow unique alternative that does not obsolete the generic counter;
  - a building replacement;
  - limited unique infrastructure with an owning consumer.
- **Overlay requirements.** Each variant declares what it replaces, its culture identity, its prerequisites and the
  measured effect it changes. It passes the same legality and determinism tests.
- **No early hooks.** No culture modifier system is added before the culture layer exists (M9 / M11+).
- **What Civilization VI is used for.** The useful pattern is a common baseline plus selective identity, and Civ VI is
  used only as that reference. Its tile movement, combat numbers and paid upgrades are not copied.

---

## 7. Military family sheets

**How each sheet reads.**
- Each sheet uses the same fields.
- Progression lists identities by Age:
  - `=` means the previous identity remains the realization (an intentional gap, per the ADR-031 gap audit);
  - `—` means the family does not exist yet.
- Prerequisites are existing node or entity ids.
- Stat bands follow §4.3.

### 7.1 Heavy / line infantry — `heavy_infantry` (key 3)

| Field | Content |
|---|---|
| Status | Warband **existing** (token). A1–A3 recruitment: **M7 candidate**. A4–A8: **blocked** (iron, steel, gunpowder). A9: **blocked** (fuel). |
| Role | Main battle line: holds ground, wins close combat, later delivers line fire |
| Progression | A1 `warband` (301) · A2 `axe_warriors` (309) · A3 `bronze_swordsmen` (302) · A4 `iron_infantry` (303) · A5 `legion` (304) · A6 `men_at_arms` (305) · A7 `musketeers` (306) · A8 `line_infantry` (307) · A9 `mechanized_infantry` (308) |
| Prerequisites | 301 baseline (`baseline.basic_military`). 309 `hafting`. 302 `arsenical_bronze` OR `tin_bronze`. 303 `iron_bloomery`. 304 `iron_bloomery` AND `standing_army`. 305 `steel_carburized` AND `plate_armour`. 306 `unit.musketeers` (`matchlock` OR `flintlock`). 307 `breech_loading_rifle`. 308 `internal_combustion`. Only 306 has a `realizedBy` today; the others need the proposed per-identity `requires` (§6.5). |
| Actions / movement | Foot (×0.9–1.1). AC 1–2. Entrench (ends movement). Projects ZOC. |
| Stats (bands) | Integrity M (anchor 50) · Melee M–H · Ranged — until A7, then M · Defence M · Sight L · Cohesion M–H (the `standing_army` institution enters the cohesion equation as a coefficient) |
| Inputs | Personnel (adults). A1–A2: timber, stone, hides. A3: bronze, timber, hides. A4+: iron/steel weapons (**missing goods**). A7+: gunpowder (**missing**). A9: vehicles and fuel (**missing**). |
| Upkeep | Food per person per year. Equipment wear in the identity's material class. |
| Counters | Beats ranged infantry in contact and skirmishers that are pinned. Holds against shock if it has entrenched. |
| Vulnerabilities | Flanking, artillery, mounted shock in the open before A7, attrition when unsupplied |
| Logistics | High food demand per formation. Slow. Needs a road network for strategic tempo. |
| AI use | The default line. Keep it at least half of the field army's line strength. Garrison the capital. |
| Modernization / branch | Mainline to A9. Receives `spear_anti_cavalry` formations at A8 by generic succession. No branches. |
| Educational text | Tie each identity to its organizational change (levy, then professional, then drilled line, then mechanized), not only to its weapon. A9 must say the mechanized infantry's vehicles are not simulated until fuel exists. |

### 7.2 Spear / anti-cavalry — `spear_anti_cavalry` (key 2)

| Field | Content |
|---|---|
| Status | A2–A3 **M7 candidate**. A4–A7 **blocked** (iron). |
| Role | Close-order infantry that stops mounted attack and anchors the line |
| Progression | A1 — · A2 `spearmen` (201) · A3 = · A4 `phalanx` (202) · A5 = · A6 `pikemen` (203) · A7 `pike_and_shot` (204) · A8 line ends → generic `heavy_infantry` |
| Prerequisites | 201 `unit.spearmen` (`hafting`). 202 `iron_bloomery`. 203 `iron_bloomery` (pike drill; no specific node exists, so no new node is proposed). 204 `matchlock` |
| Actions / movement | Foot (×0.9–1.0). AC 1–2. Brace (an anti-charge stance that costs the movement budget). Projects ZOC. |
| Stats | Integrity M · Melee M · Anti-mounted VH (a coefficient applied in the melee equation against mounted targets: Law 2's precise form) · Ranged — · Defence M–H · Mobility low |
| Inputs | Personnel. Timber, stone (A2); bronze (A3); iron (A4+, **missing**); gunpowder (A7, **missing**). |
| Upkeep | Food; low equipment wear |
| Counters | Beats light and heavy cavalry that charge into it |
| Vulnerabilities | Ranged fire, heavy infantry in melee, flanks, artillery |
| Logistics | As line infantry |
| AI use | Recruited in proportion to observed enemy mounted strength. Positioned to guard the flanks of ranged formations. |
| Modernization / branch | Line end after A7 → `heavy_infantry` at A8 (existing rule). The UI must say why: the socket bayonet. |
| Educational text | Show the formation's discipline (phalanx, pike square) as the defining technology, not the spearhead material |

### 7.3 Light infantry / skirmishers — `light_infantry` (key 1)

| Field | Content |
|---|---|
| Status | A3 **M7 candidate**. A5+ **blocked** (iron; A7 firearms; A9 radios and automatic weapons need electronics). |
| Role | Screening, harassment, rough-terrain fighting, later special operations |
| Progression (after the proposed split) | A1–A2 — (scouts rehomed to `reconnaissance`) · A3 `skirmishers` (102) · A4 = · A5 `light_auxiliaries` (103) · A6 = · A7 `rangers` (104) · A8 = · A9 `special_forces` (105) |
| Prerequisites | 102: `sling` OR `bow_simple` (javelins: no specific node). 103 `iron_bloomery`. 104 `flintlock`. 105 `radio` AND `machine_gun`. |
| Actions / movement | Foot (×1.1–1.3). Rough-terrain cost coefficient reduced (band 0.4–0.7 of the foot value). AC 2. Screen (absorbs the first contact). Ambush in cover. Weak ZOC. |
| Stats | Integrity L–M · Melee L · Ranged L–M · Defence L · Sight M · Concealment M–H in cover |
| Inputs | Personnel. Hides, timber (A3); iron (A5, **missing**); firearms (**missing**). |
| Upkeep | Low |
| Counters | Beats siege, unprotected ranged units, supply columns, and line infantry in broken terrain |
| Vulnerabilities | Cavalry in the open, line infantry in open battle |
| Logistics | Low demand; can forage |
| AI use | One screen per army. Raids on enemy supply. Hold rough terrain. |
| Modernization / branch | Mainline. No branches. |
| Educational text | Distinguish skirmishers from scouts (formation vs agent) |

### 7.4 Ranged infantry — `ranged_infantry` (key 4)

| Field | Content |
|---|---|
| Status | A1–A6 **M7 candidate**: 401, 402, 403 (horn/sinew from hides), 404 (the `crossbow` node needs only `casting_closed`: a bronze trigger), branch 405 (timber). A7+ **blocked** (gunpowder; A8–A9 steel). |
| Role | Fire support; controls approaches |
| Progression | A1 `slingers` (401) · A2 `archers` (402) · A3 = · A4 `composite_archers` (403) · A5 = · A6 `crossbowmen` (404) [branch `longbowmen` (405)] · A7 `arquebusiers` (406) · A8 `sharpshooters` (407) · A9 `machine_gun_teams` (408) |
| Prerequisites | 401 `unit.slingers` (`sling`). 402 `unit.archers` (`bow_simple`). 403 `composite_bow`. 404 `unit.crossbowmen` (`crossbow`). 405 `unit.longbowmen` (`longbow`). 406 `matchlock`. 407 `breech_loading_rifle`. 408 `machine_gun`. |
| Actions / movement | Foot (×0.9–1.1). AC 1–2. Volley (range short to medium). Cannot fire after a full move (stance cost). Weak ZOC. |
| Stats | Integrity L–M · Melee L · Ranged H–VH · Range short (A1–A6), medium (A7+) · Defence L |
| Inputs | Personnel. Stone (sling shot), fiber, timber, hides (A1–A6); bronze (crossbow triggers). Gunpowder (A7+, **missing**); steel (A8+, **missing**). |
| Upkeep | Food. Ammunition wear (cloth/fiber/timber, then gunpowder). |
| Counters | Beats slow infantry on the approach, heavy cavalry that pauses, and siege crews |
| Vulnerabilities | Melee contact, light cavalry, skirmishers in cover |
| Logistics | Ammunition throughput matters from A7 (an ammunition stock is per formation, D-011 §3) |
| AI use | One per two line formations, behind the line |
| Modernization / branch | Mainline converts 404 → 406. A 405 unit converts to the next mainline identity, 406 (ADR-031 rule 2). |
| Educational text | Explain the trade-off: training time (longbow) against mechanical draw (crossbow) |

### 7.5 Light cavalry — `light_cavalry` (key 5)

| Field | Content |
|---|---|
| Status | **Blocked**: mounts. `livestock` is a food good. It is not a horse stock, and treating it as one is forbidden. A9 also needs vehicles and fuel. |
| Role | Mounted screening, raiding, pursuit |
| Progression | A1–A2 — · A3 `horse_scouts` (501) · A4 `light_horse` (502) · A5 = · A6 `light_lancers` (503) · A7 `hussars` (504) · A8 `carbine_cavalry` (505) · A9 `armoured_reconnaissance` (506) |
| Prerequisites | 501 `horse_domestication`. 502 `cavalry`. 503 `stirrup`. 504 `flintlock`. 505 `breech_loading_rifle`. 506 `internal_combustion`. |
| Actions / movement | Mounted (×1.5–2.2). AC 2. Pursue (acts on a routed enemy). Raid supply. Screen. |
| Stats | Integrity L–M · Melee M · Ranged — (A8: L) · Defence L · Sight M–H · Mobility VH |
| Inputs | Personnel. Mounts (**missing**: EC-2). Fodder (grain/livestock as food, valid once mounts exist). Hides. |
| Upkeep | Food plus fodder (high) |
| Counters | Beats ranged infantry in the open, siege, supply columns, routed formations |
| Vulnerabilities | Spear/anti-cavalry, ranged fire when stationary |
| Logistics | Fodder is the binding constraint. Strong seasonal and terrain dependence (desert penalty, D-043 B7). |
| AI use | Screen and pursuit. Raid enemy supply when enemy anti-cavalry is absent. |
| Modernization / branch | Mainline. Receives `ranged_cavalry` at A8 by generic succession. |
| Educational text | Horse availability, not cavalry knowledge, limited mounted armies. Say so. |

### 7.6 Heavy cavalry → armour — `heavy_cavalry` (key 6)

| Field | Content |
|---|---|
| Status | **Blocked**: mounts; A9 tanks need steel and fuel. |
| Role | Shock and breakthrough (the armour role at A9) |
| Progression | A1–A2 — · A3 `chariots` (601) · A4 = · A5 `cataphracts` (602) · A6 `knights` (603) · A7 `cuirassiers` (604) · A8 = · A9 `tanks` (605) |
| Prerequisites | 601 `unit.chariot` (`chariot`). 602 `cavalry` AND `iron_bloomery`. 603 `unit.heavy_cavalry` (`stirrup`). 604 `flintlock`. 605 `unit.tank` (`tank`). |
| Actions / movement | Mounted (×1.4–1.8). Chariots are terrain-restricted (open ground). Tanks are motor (×2.5–4.0). AC 1–2. Charge: a high first-contact coefficient that decays. |
| Stats | Integrity M–H · Melee VH on the charge · Ranged — (tanks: H) · Defence H · Mobility H |
| Inputs | Personnel. Mounts (**missing**). Bronze, timber (chariots). Iron or steel armour (**missing**). Tanks: steel, engines, fuel (**missing**). |
| Upkeep | High: fodder, then fuel |
| Counters | Beats ranged and light infantry in the open, artillery crews, and broken formations |
| Vulnerabilities | Prepared anti-cavalry, rough terrain, indirect-fire artillery (805–806), aviation |
| Logistics | The heaviest demand of any land family |
| AI use | Concentrate for a decisive attack. Hold in reserve as a counter-attack. |
| Modernization / branch | Mainline. The A8 gap is intentional (cuirassiers served to 1914). |
| Educational text | The tank card must explain the role continuity (breakthrough), not a fictional horse-to-tank conversion |

### 7.7 Ranged cavalry — `ranged_cavalry` (key 7)

| Field | Content |
|---|---|
| Status | **Blocked** (mounts) |
| Role | Mobile missile harassment |
| Progression | A1–A3 — · A4 `horse_archers` (701) · A5 = · A6 `mounted_crossbowmen` (702) · A7 `dragoons` (703) · A8 line ends → generic `light_cavalry` |
| Prerequisites | 701 `unit.horse_archers` (`cavalry` AND `composite_bow`). 702 `crossbow` AND `stirrup`. 703 `flintlock`. |
| Actions / movement | Mounted (×1.6–2.0). AC 2. Fire and withdraw. Can move after firing (stance cost). |
| Stats | Integrity L–M · Melee L · Ranged H · Range short · Mobility VH |
| Inputs | Mounts (**missing**). Composite bows. Iron (A6+). Gunpowder (A7). |
| Upkeep | Fodder (high) |
| Counters | Beats slow infantry, spear formations, siege |
| Vulnerabilities | Light cavalry, ranged infantry in cover, rough terrain |
| Logistics | As light cavalry |
| AI use | Harass the slow; avoid melee |
| Modernization / branch | Line end → `light_cavalry` at A8 (existing rule). The UI explains the conversion and keeps experience. |
| Educational text | Steppe origin and its dependence on herds |

### 7.8 Siege / artillery — `siege` (key 8)

| Field | Content |
|---|---|
| Status | A4–A5 **M7 candidate** (timber engines). A6 **blocked** (803's declared equipment is iron fittings: EC-1). A7+ **blocked** (gunpowder; A8 steel; A9 vehicles and fuel). |
| Role | Breach fortification (D-039 D: fortification is computed from built works); later indirect fire |
| Progression | A1–A3 — · A4 `siege_rams` (801) · A5 `torsion_engines` (802) · A6 `trebuchets` (803) · A7 `bombards` (804) · A8 `field_artillery` (805) · A9 `rocket_artillery` (806) |
| Prerequisites | 801 `unit.siege_engine` (`siege_ram` OR …). 802 `torsion_artillery`. 803 `trebuchet`. 804 `unit.artillery` (`cannon_early` OR …). 805 `rifled_artillery`. 806 `rocket`. Siege engines are assembled at the siege (needs `siege_engineers` civic, cA4, or an engineer formation present). |
| Actions / movement | Slow (×0.4–0.7). Set-up costs AC and requires being stationary. Bombard (indirect from A8). No ZOC. |
| Stats | Integrity L · Melee VL · Bombard VH vs fortification, M vs formations (A7+) · Range medium (A5–A7), long (A8+) |
| Inputs | Personnel (crews). Timber, fiber (rope), hides (A4–A5). Iron fittings (A6, **missing**). Cast cannon and gunpowder (**missing**). |
| Upkeep | Ammunition (stones, then shot and powder, which are missing). Crews eat. |
| Counters | Beats built fortification (the reason it exists). Identities with the indirect-fire capability (805, 806) also beat static formations. |
| Vulnerabilities | Any contact. Cavalry. Skirmishers. Counter-battery from indirect-fire identities. |
| Logistics | The heaviest bulk per formation. Road-bound. |
| AI use | Recruited only when a fortified target is on the campaign plan (siege difficulty shown, D-039 D4). |
| Modernization / branch | Mainline |
| Educational text | Must say that most sieges ended by starvation or negotiation, not by breach (D-039 D5) |

### 7.9 Combat engineers — `combat_engineers` (proposed key 13, new family)

| Field | Content |
|---|---|
| Status | A4–A7 **M7 candidate**. A8+ **blocked** (explosives: dynamite; A9 armoured vehicles). |
| Role | Field fortification, bridging, breaching, obstacle clearance, field road repair |
| Progression (new identities) | A1–A3 — · A4 sappers (new) · A5 = · A6 = · A7 military engineers (new) · A8 pioneer engineers (new, demolitions) · A9 combat engineers (new, armoured) |
| Prerequisites | Sappers: `siege_engineers` (cA4). Military engineers: `military_engineers` (cA7). Pioneers: `dynamite`. Armoured: `internal_combustion`. |
| Actions / movement | Foot (×0.8–1.0). AC 2. Multi-turn actions: build field works (raises the local fortification term), bridge (temporary edge), breach (assists siege), clear, repair road segment. |
| Stats | Integrity L–M · Melee L · Ranged — · Work rate H (adult-years per year, Law 3) |
| Inputs | Personnel. Timber, stone, tools. Explosives (A8, **missing**). |
| Upkeep | Food. Tools wear. |
| Counters | Enable counters rather than fight. Defeat obstacles and rivers. Shorten sieges. |
| Vulnerabilities | Any combat formation. They must be escorted. |
| Logistics | Material throughput to the work site |
| AI use | Attach one to any army planning a river crossing or a siege |
| Modernization / branch | Mainline, automatic and free (consistent with ADR-031) |
| Educational text | Roman and early-modern engineering corps as the model |

### 7.10 Air defence — `air_defence` (proposed key 14, new family)

| Field | Content |
|---|---|
| Status | **Blocked** (steel, explosives, electronics; also no air threat exists until the `air` family is realizable) |
| Role | Deny the air over a formation or site |
| Progression | A1–A8 — (a counter cannot precede its threat: `air` begins at A9) · A9 anti-aircraft artillery (new) [branch: surface-to-air missile batteries (new)] |
| Prerequisites | AA artillery: `rifled_artillery` AND `aircraft`. SAM: `guided_missile` AND `radar`. Strategic missile defence: `missile_defence` (building-charter strategic site). |
| Actions / movement | Motor (×2.5–4.0, blocked by fuel) or emplaced. AC 1. Intercept (a reaction against strike missions on the battle map, D-011 §5). |
| Stats | Integrity M · Anti-air VH · Ground L · Range long (air) |
| Inputs | Steel, explosives, electronics (**missing**) |
| Upkeep | Ammunition, fuel (**missing**) |
| Counters | Beats strike aviation, helicopters, drones |
| Vulnerabilities | Ground assault, artillery, stealth (reduced detection) |
| Logistics | Needs radar coverage (building charter §5.17) |
| AI use | Cover high-value assets when enemy aviation is observed |
| Modernization / branch | SAM is a recruited branch, not an upgrade |
| Educational text | Detection precedes interception (radar) |

### 7.11 Military supply — `military_supply` (proposed key 15, new family)

| Field | Content |
|---|---|
| Status | A2–A7 **M7 candidate after the draught ruling** (the transport packet's open Q-C, D-040 B4). Until then carts are **blocked**: livestock is a food good, and using it as draught needs that ruling. A8 **blocked** (rail: iron, steel, coal). A9 **blocked** (motor fuel). |
| Role | Carries the army's food and ammunition between depot and front. It is the real target of raids. |
| Progression (new) | A1 — (armies forage) · A2 baggage carts (new) · A3 = · A4 supply trains (new) · A5–A6 = · A7 wagon trains with depots (new) · A8 railhead logistics (new) · A9 motor transport (new) |
| Prerequisites | Carts: `cart`. Supply trains: `standing_army` (cA4). Wagon trains: `standing_army` AND `cartography`. Railhead: `railway`. Motor: `internal_combustion`. |
| Actions / movement | At the army's pace or slower. AC 1. Resupply (moves stock from depot to formation through the Ledger). Forage. |
| Stats | Integrity L · combat VL · Capacity H (throughput per year) |
| Inputs | Personnel. Timber (carts), livestock as draught (a food good; a draught model is the transport packet's open Q-C), hides. |
| Upkeep | Food. Wear. |
| Counters | None (it is a target) |
| Vulnerabilities | Light cavalry, skirmishers, raiders, partisans |
| Logistics | It **is** the logistics. Throughput limits army size away from depots. |
| AI use | Recruit to keep supply margin positive on the planned route |
| Modernization / branch | Mainline |
| Educational text | "Armies march on their stomachs": show the capacity limit |

### 7.12 Field medical — `field_medical` (proposed key 16, new family)

| Field | Content |
|---|---|
| Status | A5–A7 **M7 candidate**. A8 ambulance corps **blocked** (draught or motor: EC-2 / EC-4). Air medevac is a `rotary_wing` mission, not an identity of this family. |
| Role | Recover wounded to strength. Lower deaths among casualties (a coefficient in the casualty split, D-011 §2). |
| Progression (new) | A1–A4 — · A5 field surgeons (new) · A6 = · A7 military hospital corps (new) · A8 ambulance corps (new) · A9 = |
| Prerequisites | Surgeons: `surgery_instruments`. Hospital corps: `inst.military_medicine` (`surgery_instruments` AND (`census` OR `standing_army`)). Ambulance: `medical_education_reform` (cA8). |
| Actions / movement | At army pace. AC 1. Treat (converts wounded back to strength over time). |
| Stats | Combat — · Recovery H |
| Inputs | Personnel (a skilled share). Cloth. Pottery. |
| Upkeep | Cloth, food |
| Counters | None |
| Vulnerabilities | Any attack |
| Logistics | Low |
| AI use | One per large army |
| Modernization / branch | Mainline |
| Educational text | Disease killed more soldiers than battle before germ theory. Honest about what is modelled. |

### 7.13 Naval surface combat — `naval_combat` (key 9)

| Field | Content |
|---|---|
| Status | A4–A6 **M7 candidate** (needs water edges and ports first, slice M7.6). A7 **blocked**: 904 needs `ship_of_line` = `carrack` AND `cannon_cast_iron`, which needs iron (EC-1) and gunpowder (EC-3). A8 **blocked** (iron, coal). A9 **blocked** (steel, fuel). |
| Role | Sea control: fight fleets, protect transports, blockade |
| Progression | A4 `war_galleys` (901) · A5 `quinqueremes` (902) · A6 `war_cogs` (903) · A7 `ships_of_the_line` (904) · A8 `ironclads` (905) · A9 `destroyers` (906). Branches 907/908 rehomed (§5.2). |
| Prerequisites | 901 `unit.war_galley` (`naval_ram`). 902 `naval_ram` AND `state_navy`. 903 `sail_square` AND `state_navy`. 904 `unit.ship_of_the_line` (`ship_of_line`). 905 `unit.ironclad` (`screw_propeller` AND `iron_hull`). 906 `steam_turbine`. Requires `building.harbour` or a naval yard (building charter §5.13). |
| Actions / movement | Water edges only (D-040 B4). Galleys: coastal and river edges. Sail: ocean edges. AC 1–2. Engage, blockade (cuts a port's trade edges), escort. |
| Stats | Integrity M–H · Ship combat H · Bombard of coasts L (A7+: M) · Sight M |
| Inputs | Crews. Timber, fiber, cloth (sails) (A4–A6); bronze (rams). Cast-iron cannon and gunpowder (A7, **missing**). Iron, coal, steel, fuel (**missing**). |
| Upkeep | Crew food. Hull wear (timber). |
| Counters | Beats transports, raiders (escort role), coastal trade |
| Vulnerabilities | Fireships and storms (a weather-system hook), submarines (A9), aviation (A9) |
| Logistics | Port-based. Range limited by provisioning. |
| AI use | Escort invasions. Blockade an enemy port that holds a large trade share. |
| Modernization / branch | Mainline. Naval command battles are post-slice; navies auto-resolve (D-011 §5). |
| Educational text | Galley vs sail: oar endurance against wind independence |

### 7.14 Naval transport — `naval_transport` (key 10)

| Field | Content |
|---|---|
| Status | A1–A7 **M7 candidate** (with water edges). A8–A9 **blocked** (coal, iron, diesel). |
| Role | Carry armies, settlers and goods over water |
| Progression | A1 `shore_craft` (1001) · A2 = · A3 `plank_boats` (1002) · A4–A5 = · A6 `seagoing_transports` (1003) · A7 `oceanic_transports` (1004) · A8 `steam_transports` (1005) · A9 `amphibious_transports` (1006) |
| Prerequisites | 1001 baseline (`baseline.primitive_water_transport`). 1002 `plank_boat`. 1003 `sail_square` AND `portolan`. 1004 `caravel` OR `carrack`. 1005 `steamboat`. 1006 `diesel`. |
| Actions / movement | Water edges. AC 1. Embark, disembark (costs the carried army's AC), carry (capacity in formations and goods bulk). |
| Stats | Integrity L–M · combat VL · Capacity H |
| Inputs | Timber, fiber, cloth (A1–A7) |
| Upkeep | Crew food |
| Counters | None (it is a target) |
| Vulnerabilities | Naval combat, raiders. Losses at sea kill the people embarked (Ledger debit, not deletion). |
| Logistics | Carries the logistics of overseas armies |
| AI use | Only for planned overseas operations or colonization |
| Modernization / branch | Mainline |
| Educational text | Shore-hugging craft cannot cross open ocean: an edge restriction, not a tech switch (D-040 B3) |

### 7.15 Naval raiders — `naval_raiders` (proposed key 17, new family)

| Field | Content |
|---|---|
| Status | A5–A6 **M7 candidate**. A7 privateers **blocked** (`cannon_cast_iron` needs `blast_furnace_water`: EC-1; gunpowder: EC-3). A8 **blocked** (coal, iron). |
| Role | Commerce raiding and coastal raids; avoids battle fleets |
| Progression (new) | A1–A4 — · A5 raiding galleys (new) · A6 = · A7 privateers (new) · A8 commerce raiders (new) · A9 line ends → generic `submarines` (proposed 18) |
| Prerequisites | Raiding galleys: `plank_boat` AND `sail_square`. Privateers: `caravel` AND `cannon_cast_iron`. Commerce raiders: `steamboat` AND `iron_hull`. A letter-of-marque civic does not exist, and none is proposed (TC-1). |
| Actions / movement | Water edges, fast. AC 2. Raid trade edge (a Ledger debit of the cargo, transferred to the raider's owner). Coastal raid. Evade. |
| Stats | Integrity L–M · Ship combat L–M · Speed H · Concealment M |
| Inputs | Crews. Timber, cloth. Cannon and gunpowder (**missing**, A7). |
| Upkeep | Low |
| Counters | Beat transports and trade |
| Vulnerabilities | Naval combat escorts |
| Logistics | Prize cargo resupplies them |
| AI use | Against a rival with high seaborne trade and weak escorts |
| Modernization / branch | Line end → submarines at A9 (`noSuccessor: generic`) |
| Educational text | Privateering as a licensed private war; its political cost (M8 diplomacy hook) |

### 7.16 Submarines — `submarines` (proposed key 18; rehomes identity 907)

| Field | Content |
|---|---|
| Status | **Blocked** (steel, diesel; nuclear: uranium + M10) |
| Role | Hidden sea denial and commerce war |
| Progression | A9 submarines (new, diesel-electric) [branch `nuclear_submarines` (907)] |
| Prerequisites | `submarine`. 907: `unit.nuclear_submarine` (`nuclear_submarine`). |
| Actions / movement | Water edges, submerged. Detection needs sonar (no node; reconnaissance capability). AC 1. |
| Stats | Integrity M · Ship combat VH (from concealment) · Stealth VH |
| Inputs, upkeep | Steel, fuel, uranium (**missing**) |
| Counters | Beat surface fleets and trade |
| Vulnerabilities | Anti-submarine escorts and aircraft (requires a detection model) |
| Logistics | Port-based; the nuclear branch has unlimited range |
| AI use | Sea denial when outmatched on the surface |
| Modernization / branch | 907 is a recruited branch |
| Educational text | Distinguish propulsion from weapon (no nuclear weapon implied) |

### 7.17 Carriers — `carriers` (proposed key 19; rehomes identity 908)

| Field | Content |
|---|---|
| Status | **Blocked** (steel, fuel, air basing) |
| Role | A mobile air base: projects aviation |
| Progression | A9 `aircraft_carriers` (908) |
| Prerequisites | `unit.aircraft_carrier` (`aircraft` AND `steam_turbine`) |
| Actions / movement | Water edges. Hosts N air wings (capacity). Launch sortie. |
| Stats | Integrity H · own combat L · Air capacity VH |
| Inputs, upkeep | Steel, fuel (**missing**) |
| Counters | Enable air strikes |
| Vulnerabilities | Submarines, missiles |
| Logistics | Fuel for embarked wings |
| AI use | Only with a sea-separated rival |
| Modernization / branch | None |
| Educational text | A carrier is a platform; its strength is its aircraft |

### 7.18 Fixed-wing aviation — `air` (key 11)

| Field | Content |
|---|---|
| Status | **Blocked** (fuel, aluminium, engines; air-basing system). Also Q3: the family begins at A9 although military aviation is historically Industrial (A8). |
| Role | Air superiority, strike, reconnaissance: strike missions on the battle map, never dogfight simulation (D-011 §5) |
| Progression | A9 `military_aircraft` (1101) [branch `stealth_aircraft` (1103)] |
| Prerequisites | 1101 `unit.military_aircraft` (`aircraft`). 1103 `unit.stealth_aircraft` (`stealth`). Requires `building.airfield` or a carrier. |
| Actions / movement | Range from base (sortie radius). AC 1 sortie per refresh. Strike, intercept, reconnoitre. |
| Stats | Integrity L–M · Strike H · Air combat H · Sight VH |
| Inputs, upkeep | Aluminium, engines, fuel (**missing**) |
| Counters | Beat armour, fleets, supply |
| Vulnerabilities | Air defence, fighters, base destruction |
| Logistics | Fuel and munitions at the base |
| AI use | Strike the highest-value targets within range |
| Modernization / branch | Stealth is a recruited branch, not an upgrade (ruling 18) |
| Educational text | Separate reconnaissance (the first military use) from bombing |

### 7.19 Rotary-wing — `rotary_wing` (proposed key 20; rehomes 1102)

| Field | Content |
|---|---|
| Status | **Blocked** (fuel, aluminium) |
| Role | Air mobility, close support, medevac |
| Progression | A9 `helicopters` (1102) |
| Prerequisites | `unit.helicopter` (`helicopter`) |
| Actions / movement | Short range from a base or forward point. AC 1–2. Air assault (moves a light formation). Attack. |
| Stats | Integrity L · Strike M–H · Mobility VH |
| Inputs, upkeep | **Missing** goods |
| Counters | Beat armour in the open, skirmishers |
| Vulnerabilities | Air defence, ground fire |
| Logistics | Fuel |
| AI use | Support offensives |
| Modernization / branch | None |
| Educational text | Basing and range distinguish it from fixed-wing |

### 7.20 Unmanned aircraft — `unmanned_aircraft` (proposed key 21; rehomes 1104)

| Field | Content |
|---|---|
| Status | **Blocked** (electronics, fuel; datalinks need communications networks) |
| Role | Persistent reconnaissance, later precision strike |
| Progression | A9 reconnaissance drones (new) [branch `combat_drones` (1104)] |
| Prerequisites | Recon drones: `aerial_reconnaissance` AND `computer`. 1104 `unit.combat_drone` (`combat_drone`). |
| Actions / movement | Range from base. Loiter (sustained VISIBLE). Strike (branch). |
| Stats | Integrity VL · Sight VH · Strike M (branch) |
| Inputs, upkeep | Electronics (**missing**) |
| Counters | Reveal concealed agents and formations |
| Vulnerabilities | Air defence, electronic warfare (cyber hook) |
| Logistics | Datalink coverage (network graph) |
| AI use | Reconnaissance first |
| Modernization / branch | None |
| Educational text | Remote operation and its dependence on communications |

### 7.21 Strategic missiles — `strategic_missile` (key 12)

| Field | Content |
|---|---|
| Status | **Blocked** (fuel, electronics; launch sites) + **M10** (strategic governance) |
| Role | Long-range strike; delivery of a separately held warhead (§12.4) |
| Progression | A9 `ballistic_missiles` (1201) [branch `icbm_forces` (1202)] |
| Prerequisites | 1201 `unit.missile` (`rocket`). 1202 `unit.icbm` (`icbm`). A launch facility (building charter §5.23). |
| Actions / movement | Fixed or road-mobile launchers. Launch (consumes the missile). Targets need validation and a launch authority (M8/M10). |
| Stats | No battle stats. Strike magnitude is a property of the payload. |
| Inputs, upkeep | **Missing** goods. Stockpile maintenance. |
| Counters | Missile defence (§7.10, building charter) |
| Vulnerabilities | Pre-emption of launch sites |
| Logistics | A stockpile, not supply |
| AI use | Deterrence posture only. A use policy needs a reviewed rule (M10). |
| Modernization / branch | ICBM is a branch |
| Educational text | Conventional and nuclear payloads are separate decisions |

---

## 8. Exploration, economic and civil agents

These are MobileAgents. Reconnaissance (§8.1) is a `MilitaryUnits` family (composition `mobile_agent`). The others are
not; each has its own table and owner.

### 8.1 Reconnaissance agents — `reconnaissance` (proposed key 22; rehomes identity 101)

| Field | Content |
|---|---|
| Status | **existing** as the Explore order (kind 14, no unit). Agent: **M7 candidate** (slice M7.5). |
| Role | Reveal and maintain knowledge of terrain, deposits and rivals: fog VISIBLE and REMEMBERED (ADR-044) |
| Progression | A1 `scouts` (101) · A2 = · A3 = (outriders, mounted scouts, are **deferred**: added to the line only with EC-2 mounts, because ADR-031 would otherwise convert scouts to a mounted identity for free) · A4–A6 = · A7 survey patrols (new: map, compass and survey) · A8–A9 = (aerial and satellite reconnaissance belong to `unmanned_aircraft` and to space assets) |
| Prerequisites | 101 baseline (`baseline.exploration`). Outriders (with EC-2): `horse_domestication`. Survey patrols: `cartography` AND `state_exploration` (cA7). |
| Actions / movement | Foot ×1.1–1.3 (mounted ×1.8–2.2 once outriders exist). AC 2. Observe (VISIBLE radius). Shadow (a lagging position report, D-039 B). Survey (extends the explored extent permanently, D-040 B). |
| Stats | Integrity VL · combat — · Sight H · Concealment M |
| Inputs | One to three adults. Food. |
| Upkeep | Food |
| Counters | It is countered by light cavalry and ZOC (it avoids ZOC rather than projecting one) |
| Vulnerabilities | Capture or death (the people are debited) |
| Logistics | Foraging range |
| AI use | Keep the frontier extent current. Shadow observed rival armies. |
| Modernization / branch | Mainline, free at Age transition |
| Educational text | Replaces the Explore button. Tells the player what is now seen versus remembered. |
| Replacement rule | The Explore order is kept until the agent path is fully tested, then removed in the same packet that makes the agent live (the existing doc's slice 3 rule) |

### 8.2 Settlers — colonization party

| Field | Content |
|---|---|
| Status | **existing** (order kind 9; the party is drawn from buckets). As a moving agent in transit: **M7 candidate**. |
| Role | Found a new settlement at a legal site (ADR-036) |
| Progression | One agent type for all Ages. Its capacity, speed and range change with transport (roads, boats, rail) rather than by identity. |
| Prerequisites | `baseline.settlement_founding`. Overseas founding requires a `naval_transport` agent. |
| Actions / movement | Path cost ×0.7–0.9 (families with goods). AC 1. Found (kind 9 semantics at the agent's position). Turn back. |
| Inputs | People (`orderPartyFraction`, existing), food and timber carried |
| Vulnerabilities | Interception. Starvation in transit (people debited, never deleted). |
| AI use | The existing `AiFoundingPolicy`, unchanged, plus the transit leg |
| Educational text | Who went and why: land pressure, as already modelled by migration |

### 8.3 Builders — settlement labour and field work crews

| Field | Content |
|---|---|
| Status | Settlement labour: **existing** (ConstructionSystem, RoadDevelopmentSystem, D-054 incremental construction, D-055 allocation). Field work crews: **design-only**. |
| Decision proposed | **No Civ-style builder unit with charges.** Construction already uses real adult labour from settlement buckets. A builder unit would double-count that labour. |
| Work crews (design-only) | Work outside any settlement's reach: a road across an empty region, a power line, a pipeline. Use a crew agent that carries labour (people transferred, Law 1) and materials to a site. Needed for long-distance networks (building charter §6), so it comes no earlier than the network that needs it. |
| Educational text | n/a until realized |

### 8.4 Prospectors and geologists

| Field | Content |
|---|---|
| Status | **existing** as the Prospect order (kind 15, at an own settlement). As an agent: **M7 candidate**. Geophysical and radiometric teams: **blocked** (instruments, electronics; uranium chain). |
| Role | Reveal concealed deposits (D-062 R7: 20–30 % concealed) away from settlements |
| Progression | A1–A2 — (surface deposits only) · A3 prospecting party (new; `ore_prospecting`) · A4–A8 = · A9 geophysical survey team (new; `geophysical_prospecting`) [branch radiometric survey (`radiometric_prospecting`), uranium chain §12.3] |
| Actions / movement | Foot. AC 1. Prospect (the kind 15 semantics at a location: cost sunk through the Ledger; reveals or finds nothing; once per site). |
| Inputs | Grain, stone, timber (as kind 15 today) |
| AI use | The existing AI prospecting rule, extended to sites within reach |
| Educational text | Distinguish knowing that ore exists from being able to use it (EC packets) |

### 8.5 Traders and trade convoys

| Field | Content |
|---|---|
| Status | Trade flows: **existing** (TradeArbitrage). Convoy agents: **M8+** (M8, with diplomacy and raiding). Caravans as an activity: deferred `activity.caravans`. |
| Decision proposed | **Trade stays a flow.** A convoy agent is created only where the game needs a *target* (raiding, escort, piracy, blockade) or a *carrier of influence* (M9). Otherwise per-convoy agents add cost without decisions. |
| Progression | A3 caravans (`donkey` OR `camel`; `camel_caravan` A4) · A5 merchant convoys (`road_paved`) · A7 sea convoys (`caravel`) · A8 rail freight (**blocked**) · A9 container freight (`container_shipping`, **blocked**: fuel) |
| Actions | Carry goods along the existing trade route (a Ledger transfer at delivery). Pay tolls. Be raided. |
| Counters / vulnerabilities | Raiders, light cavalry, partisans. Escorts protect them. |
| AI use | Escort convoys on routes with observed raiders |
| Educational text | Trade as risk and distance, not as teleporting goods |

### 8.6 Diplomats

| Field | Content |
|---|---|
| Status | **M8+** (M8 Politics/Diplomacy) |
| Role | Carry proposals, establish embassies, gather open information, raise recognition (D-037 C7) |
| Progression | A3 envoys (`royal_envoys`, cA3; `sworn_treaties`) · A7 resident ambassadors (`resident_embassies`, cA7) · A9 multilateral delegations (`peacekeeping_operations` / `integrated_alliance_command`) |
| Actions | Travel (path cost). Present credentials. Negotiate (a turn-exact delivery pin, M8). Observe (an open intelligence channel). |
| Inputs | One notable or adult and a travel budget |
| Vulnerabilities | Expulsion, detention (an M8 incident) |
| AI use | M8 policy |
| Educational text | Diplomatic immunity as an institution, not a stat |

### 8.7 Researchers

| Field | Content |
|---|---|
| Status | Scientist special people: **M8+** (owner: special-people system, §13 row SP). Field expeditions: **design-only**. |
| Rule | D-043 B4: knowledge belongs to the polity. A scientist affects the rate or mechanism of research inside the RP equation (a coefficient: Law 2). A dead scientist takes no knowledge with them. |
| Charges | Finite (D-047 ruling 15): for example, accelerate one node in progress (a share of its remaining cost), or found or upgrade a research institution |
| Recruitment | Model A: institutions (universities, existing; scientific societies, `inst.scientific_society`) generate points, and civilizations compete |
| Educational text | Name the discipline. No real-person catalogue until the content architecture separates person and ability definitions (D-043 B3). |

### 8.8 Civil relief and medical teams

| Field | Content |
|---|---|
| Status | **M8+** (M9 Society: health, disease) |
| Role | Epidemic response and disaster relief (the existing Disaster system is the consumer) |
| Prerequisites | `inst.quarantine`, `public_hospitals` (cA6) |
| Educational text | Quarantine and sanitation outcomes, not miracles |

---

## 9. Commanders and special people

All special people follow D-043 B3 and D-047 rulings 15–16:
- they are temporary MobileAgents;
- they are owned by whoever recruits them;
- they have predefined skills and abilities;
- they have finite charges and a finite lifespan;
- recruitment model A: institutions generate points.

Open conflict F14 with M4 notables ruling R-1 ("a notable is a person"). The charter's reading is that **the person
comes from the population (a notable), and the special-person record adds the abilities and charges**. That satisfies
D-037 A1 (nothing spawns) and R-1. It needs the Director's ruling (§16, Q4).

### 9.1 Generals and admirals

| Field | Content |
|---|---|
| Status | Command role: **M7 candidate** (D-011 needs a general for delegation and AutoResolver parameterization). Traits catalogue: **M8+**. |
| Role | Command an Army or fleet: command radius, staff capacity, order reliability, logistics efficiency, morale recovery. Each is a **coefficient inside the delivery, supply, cohesion or resolver equations**, never a flat strength aura (Law 2). |
| Progression | The source is unchanged across Ages: a notable is appointed. Capability grows with institutions: `prestige_leadership` (cA1) chiefs · `standing_army` (cA4) professional commanders · `military_academies` (cA7) trained officers · `general_staff` (cA8) staff system (command capability grows faster than force size, D-039 A4). Admirals: `state_navy` (cA4). |
| Actions | Command (attach to an Army). Delegate battle (D-011 §2). Inspire (a charge: cohesion recovery). |
| Counters / vulnerabilities | Capture or death in battle (a notable debited). Command friction at distance. |
| AI use | Assign the most experienced available commander to the largest army (key: experience, then id) |
| Educational text | Separate leadership from the state's command institutions |

### 9.2 Heroes

| Field | Content |
|---|---|
| Status | **design-only** |
| Proposal | Rare legendary figures as a narrative layer only. Each is drawn from the population (a notable) like every special person, and its era is history text, not an Age input: finite charges, origin text, no permanent buffs, never stronger than an army. Recommended only after special people and culture are live (M11+). Must pass the same Law 2 test as every ability. |

### 9.3 Artists

| Field | Content |
|---|---|
| Status | **M8+** (M9 Society) |
| Role | Create a work: a durable cultural asset held by a building (a museum, building charter §5.15), raising culture or influence through the M9 equation |
| Prerequisites | `court_patronage` (cA3) · `arts_academies` (cA7) |
| Charges | 1–3 works, then retirement |
| Educational text | Patronage systems and how they funded art |

### 9.4 Writers

| Field | Content |
|---|---|
| Status | **M8+** (M9) |
| Prerequisites | `epic_literature` (cA3) · `vernacular_literature` (cA6) · `national_literature` (cA8). Reach scales with `printing_press`. |
| Role | Works that raise literacy- or identity-linked culture. With printing, a share of the political-opinion channel (an M8 hook). |

### 9.5 Musicians

| Field | Content |
|---|---|
| Status | **M8+** (M9) |
| Prerequisites | `public_opera` (cA7). `public_theatre` (cA5) for performance venues. |
| Role | Compositions (works) or performances (a venue-bound charge) |

### 9.6 Rock bands and touring cultural groups (D-043 B6)

| Field | Content |
|---|---|
| Status | **M8+** (M9) |
| Ratified effects | move, perform, generate revenue, generate culture, generate diplomatic effects, affect loyalty, potentially generate unrest, possess skills, consume Action Capacity, disband (D-043 B6) |
| Progression | A5 travelling troupes (`public_theatre`) · A7 touring orchestras/opera companies (`public_opera`) · A9 rock bands (`inst.mass_media_broadcast` = `radio` AND `television` AND `inst.mass_schooling`). **No recorded- or amplified-music node exists.** None is added (TC-1); the gap is reported (§16, Q6). |
| Actions | Travel (path cost). Perform at an eligible venue (building charter §5.15). Each performance resolves an audience from venue capacity × local population interest. |
| Revenue | A Ledger transfer from local buyers to the group's owner. Bounded by local purchasing power. Not created from nothing (Law 1). |
| Culture and influence | Through M9's culture equation. Diminishing returns at a repeat venue. |
| Charges | A tour has N performances, then the group disbands |
| Vulnerabilities | Security (riot risk at large venues: an unrest hook), travel cost |
| Educational text | Mass media made touring national and global |

---

## 10. Covert action, unrest and insurgency

**The governing rule (D-037 E4).**
- Espionage **amplifies existing grievance**. It never manufactures disloyalty from zero: a ratio of existing grievance,
  not an addition.
- Counterintelligence and security reduce the amplification.
- Every operation carries exposure, incident and blowback risk.

**The owner.** Subversion is M10+, with hooks reserved earlier. Proposed for M8: the hooks only, meaning the information
channel and counterintelligence detection. Whether M8 also runs gathering operations is Q9. Partisans, riots and rebel formations come from population (D-037 A1).

### 10.1 Spies (intelligence officers)

| Field | Content |
|---|---|
| Status | **M8+** (M8 hooks only: information channel, counterintelligence detection; operations M10 unless Q9 rules otherwise) |
| Role | Establish access. Report plans, dispositions, research progress and route conditions (an information channel, D-039 A2). |
| Prerequisites | State intelligence: `statecraft_doctrine` (cA5, "run officials, revenue, spies and war by rule") or `art_of_war` (cA5, "scouting, spies, supply"). Resident networks: `resident_embassies` (cA7). Coded signals: `optical_telegraph` (A7). Modern services: `military_intelligence` (cA9), `public_key_crypto`. Counterintelligence: `civil_police` (cA7). No node exists before A5 (Q6). |
| Mission sequence | Establish access (multi-turn, AC) → gather → each refresh resolves detection on a named `RngRegistry` stream → capture (a person debited or transferred as a prisoner) or expose (an M8 incident) |
| Counters | Counterintelligence (police, bureaus: building charter §5.16) |
| AI use | M8 policy: watch the strongest rival |
| Educational text | Most intelligence came from open sources and diplomats |

### 10.2 Infiltrators

| Field | Content |
|---|---|
| Status | **M8+** (M10) |
| Role | A durable network inside a named institution or faction. It raises the success of later operations. It does not act alone. |
| Requires | M8 institutions and factions to exist as targets |

### 10.3 Saboteurs

| Field | Content |
|---|---|
| Status | **M8+** (M10). Targets that need power, rail or pipelines are **blocked** with them. |
| Role | Damage a named building, route, bridge, power line or supply asset. This reduces its integrity, which a repair project restores (building charter §4.6). |
| Rule | One target per operation. Damage is bounded by the target's integrity. Exposure risk scales with target security. |

### 10.4 Agitators

| Field | Content |
|---|---|
| Status | **M8+** (M10, on the M8 unrest ladder) |
| Role | Mobilize **existing** grievance in a named population segment: it raises the segment's organization, not its grievance |
| Rule | Bounded pressure: a saturating curve with diminishing returns per repeat. It decays when not sustained. Counterplay: concessions, services, police, legitimacy. Whether pressure becomes protest, strike, riot, mutiny or revolt is decided by the segment's real conditions (D-049 local tipping). |
| Prerequisites | `printed_newspapers` (cA7) · `mass_political_party` (cA8) · `labour_unions` (cA8) as means of organization |
| Educational text | Organization turns grievance into action. Grievance itself comes from conditions. |

### 10.5 Riots and revolts

| Field | Content |
|---|---|
| Status | Segment revolt: **existing** (D-049). The unrest ladder (protest → strike → riot → revolt) and rebel formations: **M8+** (M8; D-037 B2). |
| Rule | **Not recruitable units.** A riot is a segment state. A revolt that takes up arms forms rebel formations **from the revolting segment's own adults** (Ledger transfer). Nothing spawns. |
| Counters | Garrison presence, policing, concessions, services (each acting on the segment's grievance equation) |
| Educational text | Revolts begin locally and partially (D-049 ruling 4) |

### 10.6 Partisans

| Field | Content |
|---|---|
| Status | **M8+** (M8 insurgency; foreign support M10) |
| Role | Insurgent formations in occupied or contested territory, sustained by population support (D-037 E3) |
| Rule | Strength is drawn from the supporting segment's adults. Supply comes from that segment's food (Ledger). Occupation without legitimacy raises support. Counterinsurgency (`counterinsurgency`, cA9) and services reduce it. |
| Actions | Ambush, raid supply, sabotage (local). Disperse (concealment). |
| Counters | Garrisons, light infantry, counterinsurgency policy |

### 10.7 Cyber agents

| Field | Content |
|---|---|
| Status | **Blocked** (no digital network system: `infra.internet_backbone`, `building.data_centre` deferred; realized with digital networks, M11+). Governance rules M10. |
| Role | Attack a named digital system: a grid control system, a network node, a data centre |
| Prerequisites | `cyber_warfare`, `internet` |
| Rule | Only targets that exist as modelled networks. No generic "hack a city". |

### 10.8 Field completion for the agent sheets (§8.2–§10.7)

The §8–§10 sheets state the fields that define each agent. This table completes the remaining required fields. `n/a`
gives the reason.

**Shared defaults** (apply wherever the table is silent):
- **Stats:** no battle stats; Integrity VL (a person or small group).
- **Upkeep:** food per person per sim-year (Ledger).
- **Logistics:** foraging range or travel funds.
- **Modernization:** none, because these are not `MilitaryUnits` families; capability grows with the institutions named
  in their progression.

| Agent | Progression | Stats / inputs | Counters / vulnerabilities | AI use | Educational text |
|---|---|---|---|---|---|
| Settlers (§8.2) | one type; capacity rises with transport (roads, `naval_transport`, rail when unblocked) | n/a (stats); party people + carried food and timber (existing kind 9) | none / interception by light cavalry or raiders; starvation in transit | existing `AiFoundingPolicy` + transit leg | why the party left; who went |
| Prospectors (§8.4) | A3 party → A9 geophysical team (blocked) | n/a; grain, stone, timber per attempt (existing kind 15) | none / capture | extend the existing prospecting choice to reachable sites, key `(value, site id)` | knowing ore exists vs being able to use it |
| Trade convoys (§8.5) | caravans A3 → convoys A5 → sea convoys A7 → rail/containers (blocked) | Integrity L; the cargo is goods in transit (Ledger) | escorts / raiders, light cavalry, partisans | escort on routes with observed raiders | trade as risk and distance |
| Diplomats (§8.6) | envoys A3 → resident ambassadors A7 → delegations A9 | n/a; one notable or adult + a travel budget | counterintelligence / expulsion, detention | M8 policy | immunity as an institution |
| Scientists (§8.7) | capability grows with universities and `inst.scientific_society` | n/a; recruited by model-A points | none / death ends the charges (knowledge stays, D-043 B4) | recruit when points allow; use a charge on the lane with the most remaining cost, key `(cost, node id)` | the discipline and its method |
| Relief teams (§8.8) | quarantine (`inst.quarantine`) → public-health teams (`germ_theory`) | n/a; cloth, pottery, staff | none / epidemic exposure | M9 policy | quarantine and sanitation outcomes |
| Generals (§9.1) | command capability grows by institution (chiefs → professional → academy → staff) | the notable's skill; no inputs beyond upkeep | none / capture, death | assign by `(experience, id)` | leadership vs institutions |
| Artists, writers, musicians (§9.3–9.5) | patronage A3 → academies A7 → national/mass A8–A9 | n/a; recruited by model-A points; travel to a venue or institution | none / death or retirement ends the charges | M9 policy | patronage and audiences |
| Cultural groups (§9.6) | troupes A5 → orchestras A7 → rock bands A9 | small group; travel budget | security / riot hook at large venues; travel cost | tour venues with the highest `(audience, venue id)` | mass media and touring |
| Spies (§10.1) | state intelligence A5 → resident networks A7 → modern services A9 | n/a; one adult + funds | counterintelligence / detection, capture | M8 hooks; M10 policy | open sources and diplomats |
| Infiltrators (§10.2) | from A7 (needs M8 institutions and factions) | n/a; one adult + funds | counterintelligence / exposure | M10 policy | networks inside institutions |
| Saboteurs (§10.3) | from A5 (local sabotage) → A8+ against networks (blocked with them) | n/a; one adult + materials (explosives blocked, EC-3) | security / exposure, capture | M10 policy | target choice and blowback |
| Agitators (§10.4) | A7 newspapers → A8 parties and unions | n/a; one adult + funds | concessions, services, police / exposure | M10 policy; ratio-of-grievance test (§12.6) | organization vs conditions |
| Riots and revolts (§10.5) | population state at every Age; rebel formations by organization | formation stats of the identities they can arm (latest realizable, §6.5) | garrison, policing, concessions / as formations | n/a (not commanded until a polity forms, D-049 ruling 7) | local, partial revolt |
| Partisans (§10.6) | at every Age where occupation exists; weapons follow the latest realizable identity | Integrity L–M; Melee L–M; Concealment H; supplied by the supporting segment's food | garrisons, light infantry, `counterinsurgency` / loss of support | M8 policy for the supporting polity | insurgency rests on population support |
| Cyber agents (§10.7) | A9 only | n/a; computing capacity (blocked) | cyber defence / attribution | M10 policy | named systems only |

---

## 11. Counter matrix

`++` decisive advantage · `+` advantage · `0` even · `–` disadvantage · `––` decisive disadvantage.

These are role relationships to be realized as coefficients inside the resolver equations, and they are what the
calibration battery tests (§15.6). Rows attack; columns defend.

**Keyed to capability, never to Age.** A row that changes over history changes because an identity carries a capability
(for example indirect fire), not because of the Age (Law 4; D-039 A5). Each pair is declared per family pair in data
(§15.3). The tables below are the role defaults that data starts from.

| ↓ attacker / defender → | Line inf. | Spear | Skirm. | Ranged inf. | Light cav. | Heavy cav. | Ranged cav. | Siege | Engineers | Supply |
|---|---|---|---|---|---|---|---|---|---|---|
| Line infantry | 0 | + | + (open) / – (rough) | + | 0 | – (open) | 0 | ++ | ++ | ++ |
| Spear / anti-cav | – | 0 | 0 | + | ++ | ++ | + | ++ | + | ++ |
| Skirmishers | – (open) | 0 | 0 | + | – | –– | – | ++ | + | ++ |
| Ranged infantry | + (approach) | + | 0 | 0 | – | + (if halted) | – | ++ | + | + |
| Light cavalry | 0 | –– | + | ++ | 0 | – | + | ++ | ++ | ++ |
| Heavy cav. / armour | + | –– | + | ++ | + | 0 | + | ++ | ++ | ++ |
| Ranged cavalry | + | + | 0 | + | – | – | 0 | ++ | + | ++ |
| Siege engines (801–804) | – | – | 0 | 0 | –– | –– | –– | 0 | 0 | 0 |
| Indirect-fire artillery (805–806) | + | + | 0 | + | – | + | – | + (counter-battery) | + | + |

**Naval, air and support families** (rows attack; columns defend):

| ↓ attacker / defender → | Surface combat | Transport | Raiders | Submarines | Carriers | Fixed-wing | Rotary-wing | Unmanned | Land formations | Supply / convoys |
|---|---|---|---|---|---|---|---|---|---|---|
| Surface combat (9) | 0 | ++ | + | – (without detection) | + | – | – | 0 | + (coastal bombard) | ++ |
| Raiders (17) | – | + | 0 | – | –– | –– | – | 0 | + (coastal raid) | ++ |
| Submarines (18) | ++ | ++ | + | 0 | + | — | — | — | — | ++ |
| Carriers (19) | via embarked air | via embarked air | via embarked air | – | 0 | via embarked air | via embarked air | via embarked air | via embarked air | via embarked air |
| Fixed-wing (11) | + | ++ | ++ | 0 | + | 0 | + | + | + (armour, supply) | ++ |
| Rotary-wing (20) | – | + | + | + (with detection) | – | –– | 0 | 0 | + (armour in the open) | + |
| Unmanned (21) | 0 | + | + | 0 | 0 | – | 0 | 0 | + (reveals concealment) | + |
| Air defence (14) | — | — | — | — | — | ++ | ++ | ++ | – | — |
| Strategic missiles (12) | payload-defined (M10) | | | | | | | | | |

`—` means no engagement is possible. Combat engineers (13), military supply (15) and field medical (16) do not attack:
they are defender columns only (support), and their counters are listed in their sheets.

**Fortification** (D-039 D) multiplies the defender's terms inside the equation. Siege is the only family with a
positive term against it.

---

## 12. End-to-end dependency chains

**How to read a chain.** Each step lists, in order:
- the knowledge node(s) — all existing ids;
- the realization (building, agent or flow);
- the owning system and milestone;
- the state that must exist;
- the status.

A chain is live only when every step is.

### 12.1 Electricity

| # | Step | Knowledge | Realization | Owner | Required state | Status |
|---|---|---|---|---|---|---|
| 1 | Electrical knowledge | `dynamo`, `electricity_generation` (A8) | — | Research (M6) | node complete | existing (nodes) |
| 2 | Fuel or prime mover | `steam_turbine`, `water_turbine`; fuel = coal (EC-1) or oil (EC-4) | coal mine / hydro site | Production + EC-1 (coal) / energy system (M11+, Q8) | coal good, deposits, extraction | **blocked** (coal) / hydro **blocked** (energy system) |
| 3 | Generation | — | `building.power_station`, `building.hydroelectric_dam` | Energy system (proposed, M11+) | generator capacity rows, dispatch | **blocked** |
| 4 | Transmission | `ac_transmission` (A8) | `infra.power_grid` lines | Energy system (M11+, Q8) | line edges, capacity, losses; copper metal (missing) | **blocked** |
| 5 | Substations / transformers | `ac_transmission` | substation nodes (new) | Energy system (M11+, Q8) | node rows linking transmission and distribution | **blocked** |
| 6 | Consumers | `electric_light`, `electric_motor`, `electric_traction` | consuming buildings declare demand | Production, Housing (with the energy system, M11+) | per-building demand; outage behaviour | **blocked** |
| 7 | Dispatch and storage | `grid_storage`, `smart_grid` (A9) | storage sites | Energy system (M11+, Q8) | deterministic merit order | **blocked** |
| 8 | Interconnection | — | inter-polity tie lines | Energy + M8 diplomacy (cross-border agreements) | ownership across borders | **blocked** + M8 |

**Detail:** building charter §7.1.

### 12.2 Oil and fuel

| # | Step | Knowledge | Realization | Owner | Required state | Status |
|---|---|---|---|---|---|---|
| 1 | Find oil | `percussion_drilling` (A6), `geophysical_prospecting` (A9) | prospecting agent (§8.4) finds oil deposits | Discovery (exists) + EC-4 (M11+) | oil as a geological deposit channel | **blocked** (no oil good) |
| 2 | Extract | `percussion_drilling`, `internal_combustion` | onshore well, offshore rig (new) | Production + EC-4 (M11+) | crude good; deposit depletion | **blocked** |
| 3 | Move crude | `railway`, pipelines (no node), tankers (`steamboat`) | pipeline network, rail, ships | Transport (M11+; rail with EC-1) | bulk edges; pipeline capacity | **blocked** |
| 4 | Refine | `petroleum_refining` (A8) | `building.refinery` | Production (M11+, EC-4) | recipe crude → fuels | **blocked** |
| 5 | Distribute | `motor_road` | fuel depots (new) | Transport + M7 supply model (realized M11+, EC-4) | fuel stock per depot | **blocked** |
| 6 | Consume | `internal_combustion`, `diesel`, `jet_engine` | tanks, ships, aircraft, motor transport, generators | M7 supply / Energy | fuel demand rows (Ledger.Flow) | **blocked** |

**Detail:** building charter §7.2.

### 12.3 Civilian nuclear power

| # | Step | Knowledge | Realization | Owner | Status |
|---|---|---|---|---|---|
| 1 | Radioactivity → physics | `radioactivity` (A8), `nuclear_physics`, `nuclear_fission` | — | Research | existing (nodes) |
| 2 | Find uranium | `radiometric_prospecting` | prospecting agent | Discovery + EC-6 (M10/M11+, Q5) | **blocked** (no uranium good) |
| 3 | Mine and mill | `uranium_milling` | uranium mine and mill (new) | Production + EC-6 (M10/M11+) | **blocked** |
| 4 | Enrich / fabricate fuel | `uranium_enrichment` | enrichment plant (new) | Production + EC-6 (M10/M11+) | **blocked** |
| 5 | Reactor | `nuclear_power` | `building.nuclear_power_station` | Energy system (M11+, Q8) | **blocked** |
| 6 | Grid connection | chain 12.1 | — | Energy system (M11+, Q8) | **blocked** |
| 7 | Safety, accident, waste, decommissioning | `geological_disposal` | waste repository (new) | Energy system (M11+) + Disaster + M9 health | **blocked** |

D-062 R10: the knowledge progression is accepted; no nuclear mechanics in M6. Detail: building charter §7.3.

### 12.4 Nuclear weapons

| # | Step | Knowledge | Realization | Owner | Status |
|---|---|---|---|---|---|
| 1 | Fission weapon | `fission_weapon` | weapons programme project (new) | Construction (M10 governance) | **blocked** |
| 2 | Fissile material | `uranium_enrichment` (+ chain 12.3 steps 2–4) | enrichment / reactor | Production + EC-6 (M10) | **blocked** |
| 3 | Warhead stockpile | `thermonuclear` | **asset**, not a unit: stockpile rows | Strategic system (M10) | **blocked** + M10 |
| 4 | Delivery | `rocket`, `icbm`; aircraft | `strategic_missile` (12), `air` (11) | designed M7; realized M11+; use governed M10 | **blocked** |
| 5 | Basing | — | silos and launch facilities (building charter §5.23) | Construction + M10 | **blocked** |
| 6 | Doctrine and authority | `nuclear_deterrence` (cA9) | launch authority, target validation | M8 politics + M10 | M8+ |
| 7 | Effects | — | casualties (Ledger debit of real cohorts), destruction, fallout (Disaster) | M10 | **blocked** |

**Nothing here is implemented by declaration.** No warhead, silo or launch order exists until M10 owns the stockpile,
authority, interception and consequences.

### 12.5 Aviation

| # | Step | Knowledge | Realization | Owner | Status |
|---|---|---|---|---|---|
| 1 | Flight | `balloon` (A8), `aircraft` (A9), `aircraft_metal`, `jet_engine` | — | Research | existing (nodes) |
| 2 | Materials | aluminium (EC-5), engines, fuel (EC-4) | factories (motor works) | Production (M11+, EC-4/EC-5) | **blocked** |
| 3 | Bases | `aircraft` | `building.airfield`, carriers | Construction (M11+) | **blocked** |
| 4 | Air units | — | `air`, `rotary_wing`, `unmanned_aircraft` | designed M7; realized M11+ | **blocked** |
| 5 | Sortie and strike | — | strike missions on the battle map (D-011 §5) | M7 resolver verb | M7 candidate (verb), **blocked** (units) |
| 6 | Air defence and detection | `radar`, `guided_missile` | `air_defence`, radar sites | designed M7; realized M11+ | **blocked** |
| 7 | Civil aviation | `jet_airliner`, `air_navigation_agreements` | airports (building charter §5.19) | Transport (M11+) + M8 agreements | **blocked** |

### 12.6 Covert unrest

| # | Step | Mechanism | Owner | Required state | Status |
|---|---|---|---|---|---|
| 1 | Grievance exists | needs and tax grievance per segment | Revolt / NeedsGrievance | existing tables | existing |
| 2 | Access | spy establishes access (§10.1) | Intelligence (M8) | agent rows; detection stream | M8+ |
| 3 | Network | infiltrator in an institution or faction (§10.2) | M10 | institution and faction targets | M8+ |
| 4 | Organization | agitator raises segment organization (§10.4) | M10 + M8 unrest ladder | organization per segment (new) | M8+ |
| 5 | Escalation | protest → strike → riot → revolt by local tipping (D-049) | M8 unrest ladder; revolt exists | ladder states | existing (revolt) / M8+ |
| 6 | Armed action | rebel formations or partisans from the segment's adults | M8 | formation draw from segment | M8+ |
| 7 | Counterplay | police, concessions, services, counterintelligence, legitimacy | M8 / M9 | each acts in the grievance or organization equation | M8+ |
| 8 | Attribution | exposure → diplomatic incident, blowback | M8 diplomacy | incident records | M8+ |

The binding test: an agitator in a contented, well-served segment produces no unrest (ratio of existing grievance,
D-037 E4).

### 12.7 Cultural touring

| # | Step | Mechanism | Owner | Status |
|---|---|---|---|---|
| 1 | Cultural institutions | `public_theatre`, `public_opera`, `inst.mass_media_broadcast` | Research / Institutions | existing (nodes, entities) |
| 2 | Recruitment | institutions generate points; recruit a group (model A) | Special-people system | M8+ |
| 3 | Route | travel by path cost; transport networks shorten it | M7 movement | M7 candidate (movement) |
| 4 | Venue | eligible venue (theatre, opera, stadium: building charter §5.15) | Construction | M8+ |
| 5 | Performance | audience = capacity × interest; revenue = a Ledger transfer from local buyers | M9 culture + existing economy | M8+ |
| 6 | Influence | culture / loyalty / diplomatic effects through M9 equations, diminishing at repeat venues | M9 | M8+ |
| 7 | Risk | security, riot hook at large venues | M8 unrest | M8+ |
| 8 | End | charges spent → disband | Special-people system | M8+ |

---

## 13. Dependency matrix

**Rows** are mechanics or systems. For each, the matrix gives:
- the milestone that must own it;
- the state it needs;
- the units and buildings it gates.

**EC packets** are separate, reviewed economic-content changes: goods, deposits, recipes, transport, consumption and
conservation tests. Each needs its own approval and is not part of M7's battle scope.

| Row | Mechanic | Owner (milestone) | State required | Gates (units) | Gates (buildings, see companion) |
|---|---|---|---|---|---|
| MV | Movement by path cost; terrain classes | M7 (after CR-017 §5 ruling) | MobileAgent table; cost raster terrain terms | all agents | roads (consumer) |
| AC | Action Capacity | M7 (after CR-017 §3 ruling) | AC per agent; action state | all agents | — |
| ZC | Zones of Control | M7 | ZOC table per Army | combat formations | forts (ZOC anchor) |
| RC | Recruitment + personnel transfer | M7 | formation personnel; Ledger transfer; reservation | all formations | barracks (§5.11) |
| SU | Supply and upkeep | M7 | supply stock per formation; route capacity | all formations; 15 supply | depots |
| BR | Battle resolvers + parity | M7 (D-011) | BattleSetup/Outcome; parity battery | all combat | — |
| FT | Fortification (computed) | M7 (D-039 D) | fortification term from structures | siege, engineers | walls, castles, bastion forts |
| CF | Command friction (latency) | M7 (D-039 A) | delivery delay by distance and communications | generals | courier relays, telegraph, radio |
| NV | Water edges, ports, embark | M7 slice 6 + transport packet (Q-A/Q-C/Q-E) | water edge types; port nodes | 9, 10, 17 | harbour, lighthouse, naval yard |
| SP | Special-people system | **unassigned**. Proposed: command subset M7, full M9. | person + ability definitions; charges; model A points | generals, scientists, artists, writers, musicians, cultural groups | universities, academies, venues |
| DP | Diplomacy | M8 | proposals, incidents, recognition | diplomats, convoys, interconnection | embassies |
| IN | Intelligence and counterintelligence | M8 (gathering), M10 (subversion) | agent detection streams; information channels | spies, infiltrators, saboteurs | bureaus, police |
| UL | Unrest ladder | M8 (D-037 B2) | segment organization; ladder states | agitators, rebels, partisans | police, services |
| CU | Culture and influence | M9 | culture equation; venues; works | artists, writers, musicians, bands | theatres, museums, stadiums |
| HE | Health | M9 | disease model | relief teams | hospitals, sanitation |
| EC-1 | Metals: iron ore, iron, steel; charcoal/coal | separate EC packet before A4+ recruitment | goods, deposits, smelting recipes, transport bulk, consumption | Iron/steel: 202–204, 303–305, 307–308, 103, 105, 407–408, 506, 602–605, 803, 805–806, 904–906, 1005–1006; privateers, commerce raiders (17); 13 (A9); 14; 15 (A8 rail); 18; 19 | `building.blast_furnace`, `building.steelworks`, `infra.railway`, `building.skyscraper`; the smithy's iron mode (`building.smithy` itself is design-only) |
| EC-2 | Mounts (horses) | separate EC packet | a mount stock distinct from livestock; breeding; fodder | 501–505, 601–604, 701–703; outriders (22, deferred); 16 (A8 horse ambulance); 15 if Q-C rules draught needs mounts | stables; `infra.courier_relay` |
| EC-3 | Gunpowder and explosives (saltpetre, sulphur) | separate EC packet | goods, recipe (`saltpetre_refining`, `gunpowder`, `dynamite`) | 104–105, 204, 306–307, 406–408, 504–505, 604, 703, 804–806, 904–906; privateers, commerce raiders (17); 13 (A8); 14 | arsenal, powder works |
| EC-4 | Petroleum and fuels | separate EC packet (late) | oil deposit channel, crude, fuels, pipelines | 308, 506, 605, 806, 906, 1006, 1101, 1103; 12; 13 (A9); 14; 15 (A9); 16 (A8 motor ambulance); 18–21 | wells, offshore rigs, `building.refinery`, pipelines, fuel depots, `building.airfield`, `building.spaceport` |
| EC-5 | Light metals and electronics; copper metal | separate EC packet (late) | aluminium, copper, silicon/electronics goods | 105 (radios), 1101, 1103, 1102, 1104, 12 (guidance), 14, 21 (recon drones) | `infra.power_grid`, `infra.telegraph_line`, `infra.telephone_network`, `infra.submarine_cable`, `building.radio_transmitter`, `infra.mobile_network`, `infra.internet_backbone`, `building.semiconductor_fab`, `building.data_centre`, radar sites |
| EC-6 | Uranium fuel cycle | separate EC packet (late) + M10 | uranium deposits, milling, enrichment | 907, warheads | `building.nuclear_power_station`, enrichment, waste repository |
| EN | Energy and power system | M11+ (proposed; could be M10 if the Director wants electrified A8–A9 in the integrated validation) | generation, network flow, losses, outages | electric traction, radar | §7.1 |
| SV | Strategic weapons governance | M10 | stockpile, authority, interception, consequences | 12, warheads | silos, launch sites |
| DN | Digital networks | M11+ | network nodes and edges; services | cyber agents, drones (datalinks) | data centres, backbone, mobile |
| SA | Space | M11+ | launch campaigns, orbital assets | — | spaceport, satellites |

**Rule.** A unit or building whose row is not live stays at its status class. Nothing advances to "M7 candidate"
because a later row is assumed.

---

## 14. Staged implementation sequence

The work is organized by shared system, never by Age (`milestones.md`, "Ages are a simulation dimension"). Each stage
has an owner and an exit test.

| Stage | Content | Owner | Precondition | Exit evidence |
|---|---|---|---|---|
| **S0** (this file) | Charters proposed; nothing changes | docs | — | Director review |
| **M7.0** | Rulings first: CR-017 §3 (AC refresh vs Law 3) and §5 (graph vs off-road); D-012 grid; D-013 pulse budget; Q1 re-equip; Q2 splits; Q4 notables vs special people | Director | M7 spec drafted with the S8 §4.1 four items (foundations audit, dimensional declaration, corridor independence, coupling map) | rulings recorded |
| **M7.1** | Ratify the family catalogue and schema in data: splits with migration map; per-identity `requires`; validation (orphans, duplicate roles, missing prerequisites, unsupported effects); MobileAgent table design | M7 | M7.0 | loader validation tests; migration no-op on existing saves (measured); goldens unmoved or attributed |
| **M7.2** | Movement + Action Capacity + ZOC for one agent type (Army), then scouts | M7 | M7.1 | turn-exact delivery pins; path-cost tests; wraparound and boundaries; replay; save/load populated tables |
| **M7.3** | Recruitment + personnel + upkeep for A1–A3 land identities from existing goods (301, 309, 302, 201, 401, 402, 102); founding-warband personnel reconciliation | M7 | M7.2 | conservation (people and goods exact); atomic refusal; AI legal-only; golden moves attributed with switch-off twins |
| **M7.4** | AutoResolver + TacticalResolver v1, counters, fortification term, siege (A4–A5 timber engines), starvation path | M7 (D-011, D-039) | M7.3 | parity battery; counter-matrix tests (§15.6); siege difficulty shown = resolver input |
| **M7.5** | Agents: reconnaissance replaces Explore; prospecting party; settler transit; generals as commanders (command subset of SP); engineers; supply (after Q-C); field medical | M7 | M7.2–7.4; Q-C draught ruling for supply | Explore removal only when the agent path is green; fog tests; notable debit on death |
| **M7.6** | Water edges, ports, naval transport A1–A7, surface combat A4–A6, raiders A5–A6 (auto-resolve) | M7 + transport packet | Q-A/Q-C/Q-E/sea edges ruled together (D-040 B4) | embark/disembark conservation; loss-at-sea debits |
| **M7.7** | Military buildings with live consumers only (barracks, depot, siege workshop, harbour/naval yard); calibration over seeds 1, 2, 42, 7 | M7 | M7.3–7.6 | §15 suite green; CI green |
| **EC-1 … EC-3** | Metals, mounts, gunpowder: each a separate reviewed content change. Unblocks A4–A8 recruitment of the listed identities. | separate packets (can run in M7 or after) | Director approval each | goods conservation; deposits; recipes; transport bulk; golden attribution |
| **M8** | Diplomats; intelligence gathering and counterintelligence; unrest ladder; rebel formations; partisans; trade convoys; launch authority hooks | M8 | M7 movement live | ratio-of-grievance test (§12.6) |
| **M9** | Special-people system in full (scientists, artists, writers, musicians); cultural groups and rock bands; culture venues; health | M9 | SP rows designed | revenue conservation; diminishing returns; Law 2 audit of every ability |
| **M10** | Integrated validation across A1–A9; subversion (infiltration, sabotage, agitation); strategic weapons governance; EC-6 if approved | M10 | M8, M9 | whole-ladder seeds; no Age-specific architecture |
| **M11+** | Energy system and grids; EC-4/EC-5; aviation, submarines and carriers realized; digital networks and cyber; space; wonders and megaprojects; heroes; culture-specific variants | M11+ (sequence open) | each row of §13 | per-row tests |

---

## 15. Balance and validation

### 15.1 Deterministic ordering

- **Fixed orders.** Polities are processed in roster order. Formations and agents in table order. Orders in log order:
  first valid wins, as AdvanceAge does.
- **Composite keys.** Every argmax or ordering over a double score (AI choice, target selection, dispatch) uses
  `(score, stable integer id)`. Each ships a tie-dense test.
- **No unordered iteration.** Nothing iterates a `Dictionary` or `HashSet` in sim logic, and nothing reads
  `GetHashCode` (CLAUDE.md law 5).
- **Randomness.** Detection, exposure, storms and weather come from named `RngRegistry` streams whose state lives in
  `WorldState`.

### 15.2 Stable identifiers

- Family and identity keys are append-only. They are never reused, and renaming the display `name` is free.
- Splits use the §5.3 migration map with a refusal for unmapped pairs.
- Unknown identities in a save are preserved (ADR-031 rule 5).
- MobileAgent ids are allocated monotonically per world. A dead or disbanded id is never reissued.

### 15.3 Explicit counters

- Counters are coefficients inside the resolver equations (§11), declared per family pair in data, never a hidden rule.
- Each counter pair has a test: the pair outcome distribution over a fixed setup favours the declared side by the
  declared band. The band is measured, then pinned.

### 15.4 Movement and Action Capacity

- **Movement is path cost.** It is scaled by dtYears (Law 3). It is tested on terrain classes, roads, rivers, the map
  wrap and boundaries, and ZOC entry and exit.
- **AC is independent of movement.** A test proves that an action with no movement coupling leaves the movement budget
  unchanged, and the converse.
- **Multi-turn actions.** Progress is a rate × dtYears, and it is tested at two dt values (the D-049 §6.1 / §6.2
  pattern).

### 15.5 Replay, conservation, save/load

- **Replay.** Every order kind added (move, recruit, re-equip if ruled, operations) gets a turn-exact delivery pin as
  well as replay equality (the T1.9 precedent).
- **Conservation.**
  - People between buckets, formations, agents and casualties are exact (`long`, no epsilon).
  - Goods consumed by recruitment, upkeep, ammunition and fuel go through `Ledger.Flow` and are exact.
  - Revenue from performances or raids is a transfer, never creation.
- **Save/load.** Every new row type ships a POPULATED-table test: exact ExpectedLength, bit-exact round trip, hash
  equality, per-field hash sensitivity (the T1.1 / T1.3 precedent).
- **Golden moves.** Re-pinned only with a switch-off attribution twin returning the old hash.

### 15.6 Calibration evidence required before any number replaces a band

1. **Matchup battery.** For each counter pair in §11: fixed setups (terrain, supply, fortification) × seeds; outcome
   distributions under the AutoResolver and the TacticalResolver autoplay. Parity: AutoResolver = the median of
   TacticalResolver (D-011 §2).
2. **Composition battery.** Combined-arms armies against single-role armies of equal cost. A single-role army must not
   dominate.
3. **Economic throughput.** Recruitment and upkeep against the food and goods economy over seeds 1, 2, 42, 7. An AI at
   war must not starve its settlements beyond the bounded-store envelope.
4. **Modernization cost check.** Because modernization is free, upkeep is the long-run cost. Measure the standing-army
   share of the food and goods budget across an Age transition.
5. **Siege.** Starvation vs assault outcomes against D-039 D5. Shown difficulty equals the resolver input (D-039 D4).
6. **Covert.** Agitation in a high-grievance and a zero-grievance segment. The second must produce nothing (D-037 E4).

### 15.7 AI behaviour

- **Legal by construction.** The AI uses the same predicates as the player. An attempted illegal order is a test
  failure, not a refusal path.
- **No privileged reads.** It reads only polity knowledge (ADR-037 leak gates extended to agents).
- **Deterministic.** One candidate per decision with a stable tie-break, recorded in the order log so replay reproduces
  it.
- **Measured.** Each new policy ships a four-seed measurement and an attribution control (the ADR-052 / ADR-053
  precedent).

---

## 16. Conflicts with existing decisions and open questions

### 16.1 Conflicts found

| # | Conflict | Sources | Charter position |
|---|---|---|---|
| C1 | The brief's Age table places rifles, machine guns, ironclads and early aircraft at A7, and tanks and helicopters at A8. The repository puts muskets at A7, rifles, machine guns and ironclads at A8, and tanks and aircraft at A9. | brief vs `ages.json`, `unit-families.json`, `research.json` node ages | The charter follows the repository. The brief's labels are about one Age early. |
| C2 | "Upgrade paths and transformations" vs "no paid manual upgrades" | brief vs D-047 ruling 18 | No paid upgrades. Re-equip is defined as re-recruitment and needs a ruling (Q1). |
| C3 | Continuous off-road movement vs network-graph armies | D-043 B7 vs D-009/D-010; CR-017 §5 OPEN | Path-cost model compatible with both. Ruling needed before M7.2. |
| C4 | Special people "do not require allegiance" vs "a notable is a person" | D-043 B3 vs M4 R-1 (F14); D-037 A1 (F13) | Person from population + ability record (Q4) |
| C5 | Espionage at M10+ (including degraded information quality) vs a spy class in the first unit charter | D-037 E4; D-039 B5 | Hooks only in M8 (information channel, counterintelligence detection); operations M10. Raised as Q9, not settled here. |
| C6 | Sea travel as network edges vs naval MobileAgents at continuous x/y | D-040 B4 vs D-043 B1 (F12) | Naval agents move on water edges; one transport conversation |
| C7 | Builder units vs real settlement labour | brief vs D-054/D-055 construction | No builder charges; field crews design-only |
| C8 | "Armour" as its own class vs the existing heavy-cavalry line | brief vs `unit-families.json` | Armour = `heavy_cavalry` A9 identity; no new family |
| C9 | Air begins at A9 vs military aviation historically Industrial | gap audit vs history | Not changed (Q3) |
| C10 | Some identities are realizable only with goods beyond the brief's list (horses, gunpowder, copper metal, electronics) | `goods.json` | Marked blocked with EC-2/EC-3/EC-5 |

### 16.2 Open questions for the Director

1. **Q1 — Re-equip.** Is converting a formation to a different family, consuming the target's recruitment materials and
   keeping its people and experience, a permitted *new recruitment*, or is it barred by ruling 18?
2. **Q2 — Splits.** Approve the five rehomings (§5.3) and the new families 13–22, or keep the 12-family file and express
   the roles as branches?
3. **Q3 — Aviation Age.** Should the `air` family begin at A8 (balloon and early aircraft) instead of A9? This is a data
   change to ADR-031's gap audit.
4. **Q4 — Special people.** Is the person-from-population + ability-record reading acceptable for F13/F14? Which
   milestone owns the special-people system (proposed: command subset M7, full M9)?
5. **Q5 — EC packets.** Order and scope of EC-1 (metals) to EC-6 (uranium). Should EC-1 to EC-3 run inside M7 so that
   A4–A8 identities are recruitable, or after? Without them, §6.5 falls back to the latest realizable identity (for
   example bronze-armed infantry at A4+). One known consequence of ADR-031 should also be ruled on: free modernization
   still converts existing formations past material blockers, because equipment is declarative. Accept that, or limit
   modernization by materials? Limiting it would change ADR-031 and needs a new decision.
6. **Q6 — Tree gaps.** No node exists for informants before A5 (state intelligence begins with `statecraft_doctrine` and
   `art_of_war`, cA5), letters of marque, or recorded and amplified music. The charter uses existing proxies. Adding nodes would be a TC-1 approved change. Keep the proxies or open a
   change request?
7. **Q7 — Founding warband personnel.** Draw its personnel at founding when M7.3 lands (an attributed golden move), or
   keep it a token permanently?
8. **Q8 — Energy owner.** Should the energy system be M10, for an electrified A8–A9 in the integrated validation, or
   M11+?
9. **Q9 — Espionage milestone.** D-037 E4 places espionage at M10+, with hooks earlier. Should M8 carry only the hooks
   (the information channel and counterintelligence detection), or also intelligence-gathering operations?

### 16.3 TC-1 note

This charter adds no node and no entity. TC-1 remains **PROPOSED**; its confirmation is a separate Director decision.

---

## 17. Acceptance criteria

The charter is ready to drive M7 when:
- every family has one persistent role and a status;
- no two families share a role at the same Age;
- no identity is duplicated;
- every recruitable identity has a knowledge gate made of existing ids, a material list made of existing goods (or is
  marked blocked), a location, and a consumer in live rules;
- ADR-031's modernization contract and the founding warband remain compatible: preview equals applied, unknown
  identities are preserved;
- branches are recruited, never modernization targets;
- every special person, cultural group, covert agent and insurgent originates from the population (D-037 A1);
- every chain step in §12 has an owner and a status;
- nothing is labelled implemented without its system;
- §15's tests are listed in each implementing packet's acceptance criteria;
- culture-specific variants stay deferred.

---

## References

- `Sim.Data/content/unit-families.json`, `research.json`, `goods.json`, `sim.json`
- `docs/adr/adr-031-ages-advancement-and-unit-modernization.md`
- `docs/d011-battle-layer-addendum.md`; `docs/d039-command-fog-and-siege.md`; `docs/d040-discovery-and-control.md`
- `docs/d043-civilization-progression-ages-and-mobile-agents.md` (Part B); `docs/d047-civ6-reference-and-progression-rulings.md` (rulings 15–18)
- `docs/d037-emergent-polities.md` (A1, B2, E3, E4); `docs/d049-taxation-and-revolt-model.md`
- `docs/d052-m5-integration-audit-rulings.md` §4–§5; `docs/d062-m6-director-final-rulings.md`
- `docs/milestones.md` (authoritative sequence; Ages as a dimension)
- `docs/deferred-entity-realization-plan.md`; `docs/research-gameplay-unlock-audit.md`
- `docs/m6-plan/knowledge-redesign/tree-closure.md` (TC-1, PROPOSED)
- Companion: [`building-and-infrastructure-charter.md`](building-and-infrastructure-charter.md)
- Civilization VI was used as a pattern reference only (common baseline plus unique variants; Great People; the Rock
  Band). No movement rules, statistics or upgrade costs were copied.
