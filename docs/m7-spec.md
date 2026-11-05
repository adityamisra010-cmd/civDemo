# M7 — BATTLE LAYER — MILESTONE SPECIFICATION (DRAFT)

**Status: DRAFT FOR THE DIRECTOR'S REVIEW — NOT RATIFIED. No packet beyond T7.1 is allocated. This document authorizes no gameplay code, content, schema or golden change.**

**Provenance of this draft:**
- **When and why.** It was written under S8 §4 after the M6 exit gate (D-074, 2026-11-03; `main` = tag `m6-exit` = `7d5defc`).
- **Citations.** Every citation is to that commit unless it says otherwise.
- **Structural reference.** `docs/m6-spec.md`.
- **Inputs:**
  - the closed design: `docs/d011-battle-layer-addendum.md` and `docs/d039-command-fog-and-siege.md`;
  - the charter rulings: `docs/d065-m7-charter-rulings.md`;
  - the handoff: `docs/m6-plan/m7-handoff-register.md` and `docs/m6-plan/deferred-work-register.md`;
  - the M7 planning documents under `docs/m7-plan/` (unit charter §14, building charter §12, execution-pass report §7);
  - the open change request `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md`.

**Labels:**
- RATIFIED: a ratified record.
- RULED: a Director ruling.
- MEASURED: read off the code at `7d5defc`.
- PROPOSED: this draft's proposal.
- ASSUMED: a working assumption this draft makes where no ruling exists.
- DIRECTOR DECISION REQUIRED.

**What M7 is, in one paragraph.** M7 makes armies real. Formations are already recruited, manned from real people and fed (ADR-055, on `main`). In M7 they move across the map, are seen and not seen, meet rival formations, fight, take losses that come out of real people, besiege and take settlements, and are worn down when supply fails.
- **The battle contract is frozen (D-011 §2).** There is one resolver interface with two implementations:
  - an **AutoResolver** for every AI battle and every delegated one;
  - a **TacticalResolver**, the player's watchable command battle.
- **The parity rule holds them together.** The AutoResolver's outcome matches the median of the TacticalResolver's autoplay outcomes for the same setup.
- **The battle layer consumes M6's capability state** (`docs/milestones.md:93-98`). It has no tech vocabulary of its own.

---

## §0 S8 §4.1 CONFORMANCE, REQUIREMENT BY REQUIREMENT

| S8 §4.1 requirement | where it lives | state |
|---|---|---|
| Base skeleton: §1 decisions closed · §2 scope fence · §3 system notes · §4 task packets · §5 exit criteria · §6 governance | this document, §1–§6 | DRAFTED |
| FOUNDATIONS AUDIT as packet one | §4, T7.1 (scope in §4.1) | PROPOSED: runs after the Director approves this draft for audit |
| Dimensional declaration | §3.2 | DRAFTED: provisional as authored; T7.1 is authoritative |
| Corridor independence | §3.3 | DRAFTED: refusals and named derivations |
| Coupling map | §3.4 | DRAFTED: provisional as authored; T7.1 is authoritative |
| Director decisions isolated | §7 | 21 open items, each with a recommendation |

---

## §1 DECISIONS CLOSED

| # | decision | source |
|---|---|---|
| C1 | **WEGO command battles.** 6–12 command pulses per battle, most ending by rout. Era-constant order verbs, with era additions (bombard, air strike, dig-in). | D-011 §1, §3, §5 (RULED, frozen by S8 §1) |
| C2 | **The dual-resolver contract.** `IBattleResolver.Resolve(BattleSetup) → BattleOutcome`. AutoResolver (Lanchester family) and TacticalResolver, with identical inputs and outputs. | D-011 §2 (frozen) |
| C3 | **Battle invariants:**<br>- the setup is built entirely from strategic state;<br>- casualties are debited from real people through the Ledger (Law 1);<br>- player orders are logged per pulse and replay is blow-for-blow;<br>- the parity suite;<br>- delegation goes to the general. | D-011 §2 (frozen) |
| C4 | **Battles read frozen turn-start state** (the sub-step rule). | D-011 §3; `docs/civ-sim-architecture-v3-outline.md:33` |
| C5 | **Naval battles always auto-resolve. Sieges are multi-turn strategic operations, not battles.** | D-011 §5 |
| C6 | **Command friction is DELAY, not degradation.** Position, strength and outcome are three separate information channels. Command capability grows faster than force size. Nothing is gated on era. | D-039 Part A (A2–A5) |
| C7 | **Reconnaissance is an investable capability.** The display shows the stale "ghost" of a last-seen force. Siege includes starvation. There is a campaign layer. | D-039 Parts B–E |
| C8 | **A unit family is a thing with an independent, controllable lifecycle.** Everything else is a category B–E quantity, attachment or entity. Engineers, supply and medical are category B quantities owned by the battle system: no invented buffs. | D-065 R8–R10 |
| C9 | **Air defence** is mobile or garrisoned, never counted twice. Garrisoning is a requirement. **Snipers are required** (their form is open: §7 Q10). | D-065 R11, R14 |
| C10 | **Re-equipment** is a distinct manual order; free Age modernization is kept; a paid upgrade is barred. **Aviation** is staged in Flights I–III. **EC-1, EC-2 and EC-3** are separate packets. | D-065 R1–R3 |
| C11 | **Already ratified and on `main`:**<br>- recruitment, personnel and re-equipment (ADR-055: orders 16–18, conserved personnel, grain upkeep, AI recruitment);<br>- the economy packets EC-1, EC-2 and EC-3 (ADR-056).<br>**M7 does not re-specify them.** | D-067 R15–R18; D-074 R1 |
| C12 | **Soldiers are immortal and the warband draw is as it is until the casualty packet.** "No new behaviour is to be invented to remove these limitations" before it. | D-067 R15 |
| C13 | **Assigned to M7:**<br>- movement, Action Points and right-click move;<br>- fog work that needs moving units (ghost rows and display, scouts, sighting from moving formations, siege line-of-sight);<br>- claim, trade and road routing limited to the known extent;<br>- DW-002 (the founding rule reads unseen land). | D-052 §4; D-066 R1, §3; D-067 R10 |
| C14 | **Explore's 240 km cap is kept until mobile scouts exist.** | D-068 R4 |
| C15 | **No other game is named in shipped content.** | D-069 |
| C16 | **Order kind 10 is reserved for MoveFormation.** | `Sim.Core/Kernel/OrderLog.cs:164` (MEASURED) |

---

## §2 SCOPE FENCE — WHAT M7 EXPLICITLY DOES NOT DO

- **Diplomacy beyond the minimum war state.** That covers treaties, alliances, envoys, intelligence and counter-intelligence hooks. These are M8. **ASSUMED:** M7 carries only the minimum war/peace relation a battle needs (§7 W1).
- **Laws, government adoption, the unrest ladder, rebels, partisans, migration policy and capital succession.** M8 (D-062 R3; D-067 R13).
- **Special people beyond generals.** Scientists, artists, writers and musicians are M9. Generals are in scope because D-011 §2 makes them a resolver input (see §7 Q4 for the classification conflict).
- **Energy, fuels and engines.** EC-4a is provisionally M10 (OD-3). The energy owner Q8/B5 is open (D-065 R4).
- **Aviation Flights II–III, submarines and carriers realized, cyber and space.** M11+. Flight I is in M7 only if its Age is reached in play (D-065 R3).
- **Commanded naval battle.** It stays auto-resolve (C5). **ASSUMED:** naval movement and ports (charter stage M7.6) are a late, separable packet that can be cut to M7.x without failing the exit (§7 N1).
- **No new combat buffs.** Every combat modifier sits inside the resolver equation as a coefficient on a named state quantity (Law 2).
- **No era gates in combat or movement.** Capability comes from computed state (Law 4).
- **The research pacing, the research-economy candidate and M6.24** (stone floors, richness classes, primitive toolmaking). They are carried from M6 (D-074 §1). M7 does not own them unless the Director assigns them (§7 R1, R2).

---

## §3 SYSTEM NOTES

### §3.1 Shape and invariants (PROPOSED)

| # | invariant |
|---|---|
| I-1 | **Personnel are conserved.** A formation's people leave or return to settlement cohorts only by `Ledger.Transfer` or `Ledger.Flow`. A casualty is a sink with its own reason. Tests use exact equality. |
| I-2 | **Movement is an order with turn-exact delivery.** MoveFormation (kind 10) is validated on PREV through `RouteQuery` (H7-01). A pin proves the turn it applies, so live-versus-replay equality is not the only proof. |
| I-3 | **One movement model.** Movement uses the existing lattice pathfinder (`Sim.Core/Pathing/Pathfinder.cs:86`) and the `RouteQuery` budget prefix (`Sim.Core/State/RouteQuery.cs:43`). There is no second graph. |
| I-4 | **Every rate is per sim-year × dt, or is an agency budget explicitly exempted by a CR-017 §3 ruling.** No per-turn constants otherwise. |
| I-5 | **Resolver purity.** `Resolve` is a pure function of `BattleSetup` and its RNG stream. That stream is keyed by (SystemId, RegionId) and lives in `WorldState`. |
| I-6 | **Parity.** For every setup in the parity battery, the AutoResolver's outcome lies within the stated band of the TacticalResolver autoplay median. The band and its asymmetric margins are fixed before measurement (ADR-015 §7.16). |
| I-7 | **Fog-honest AI.** AI military decisions read only `PolityKnowledge`. This is extended to rival formations (`PolityKnowledge.KnowsUnit`, `PolityKnowledge.cs:189`). |
| I-8 | **Capture is a state transfer.** A captured settlement's `ControlRow` passes to an existing polity through one query-validated path, and a core chronicle event records it. Today only revolt moves control, and only to a new polity (`RevoltSystem.cs:84-98`). |
| I-9 | **No calendar gates.** Verbs, ranges and sight come from equipment and capability state, never from the Age label. |

### §3.2 DIMENSIONAL DECLARATION (provisional as authored; T7.1 authoritative)

Existing quantities are read from the producing expression. New ones are PROPOSED.

| quantity | unit | producing expression / status |
|---|---|---|
| **turn length dt** | sim-years per turn: 10 / 5 / 3 / 2 / 1 / 0.5 / 0.5 by era band | EXISTING: `Sim.Data/content/era-pacing.json`. 360-day year (`Sim.Core/Kernel/SimClock.cs:12-16`) |
| **path cost** | cost units; 1 unit = 16 km on ideal ground (median real ground ≈ 14.4 km) | EXISTING: `Sim.Core/Pathing/LatticeGeometry.cs:39-44`, `TraversalLattice.KmPerNode` |
| **formation position** | terrain px (X, Y), or a settlement (Location ≥ 0) | EXISTING: `MilitaryUnitRow`, `Sim.Core/State/WorldState.cs:1090` |
| **personnel** | people (long, conserved) | EXISTING: `FormationPersonnelRow.Count`, `WorldState.cs:1116` |
| **upkeep** | grain units (one adult's year of nutrition) per person-year × dtYears | EXISTING: `MilitarySystem.cs:104` `Count × UpkeepGrainPerPersonYear × DtYears`; sim.json military `_doc` |
| **unfed shortfall** | grain units not supplied | EXISTING: `UnfedUnits`. No consumer yet |
| **formation sight** | lattice nodes | EXISTING: `DiscoveryQuery.FormationSightNodes`, `DiscoveryQuery.cs:250`; operand `fog.formationSightNodes` |
| **march rate** | km per march-day | NEW (PROPOSED). Per identity, from equipment and mount: foot, horse, rail, motor |
| **march-days per sim-year** | days per sim-year | NEW (PROPOSED). Campaign-season share of the 360-day year |
| **movement budget per turn** | cost units per turn = march rate [km/day] × march-days [day/yr] × dtYears [yr] ÷ 16 [km/cost unit] | NEW (PROPOSED). **DIMENSIONAL WARNING: at dt = 10 the budget covers thousands of km.** A turn-level budget alone would let an A1 warband cross the map in one turn. This is why §7 W2 (war pulses or a capped operational tempo) must be ruled before T7.5 |
| **Action Capacity** | either decisions per decision interval (CR-017 §3 option B: an agency budget, exempt from Law 3) or capacity per sim-year × dt (option A) | NEW. DIRECTOR DECISION REQUIRED (§7 C17-3) |
| **strength** | people in the line (a subset of personnel) | NEW (PROPOSED). The same unit as personnel; never a score |
| **cohesion, fatigue, morale** | dimensionless, in [0, 1] | NEW (PROPOSED). Each enters the Lanchester coefficient as a multiplier inside the equation (Law 2) |
| **Lanchester attrition coefficient** | casualties per (person × pulse) | NEW (PROPOSED). Derived per identity from equipment; T7.1 records whether it was derived or chosen |
| **pulse duration** | hours of battle per pulse (D-013 recommendation: 6–12 pulses ≈ one day) | NEW. DIRECTOR DECISION REQUIRED (§7 D-013) |
| **casualties** | people (long), through the Ledger | NEW (PROPOSED). The resolver's double-valued loss is made whole with a banked remainder, as in `ConservedMath.WholeUnits` |
| **siege duration, starvation** | sim-years; grain units against the besieged settlement's store | NEW (PROPOSED). Uses the existing grain store and consumption, with no siege-only stock |
| **fortification** | dimensionless defence multiplier on the attacker's coefficient, from a built structure (city_wall, castle, bastion_fort) | NEW (PROPOSED). Its source is the building charter §12 |
| **ZOC radius** | lattice nodes | NEW (PROPOSED) |

**Balance checks to run on paper at T7.1:**
- **Movement budget.** Both sides of `min(budget, route cost)` are in cost units.
- **Lanchester.** dS/dpulse = −k × S_enemy has units people/pulse = (people per person-pulse) × people. This holds only if k is per (person × pulse), which is why k is declared that way.
- **Upkeep.** people × grain/(person·yr) × yr = grain.
- **Starvation.** It compares grain against grain; no ratio of grain to people.

### §3.3 CORRIDOR INDEPENDENCE

- **No battle-outcome corridor is proposed as an M7 exit gate** (refused at spec time). A band on casualties or on win rates measured against the sim's own battles would be a mirror.
- **The parity suite is a contract test, not a corridor** (I-6).
  - **Independent of:** the AutoResolver's own outputs. The reference is the TacticalResolver's autoplay distribution over the same frozen setups.
  - **How it could fail:** a change to either resolver's equation moves one side only, and the band reports it.
  - **Algebraic test:** the bound is the TacticalResolver median, which contains no AutoResolver term, so nothing cancels.
  - **Counterfactual test:** run the suite on the pre-change resolver pair.
- **External sanity bands (PROPOSED, report-only until ratified).** These are, for example, the ranges of historical loss shares for the defeated side in pre-gunpowder, gunpowder and industrial field battles.
  - Each band is an explicit external source, committed in T7.1 **before** any measurement (ADR-015 §7.13).
  - Each must name how a resolver change could fail it.
  - None gates exit unless the Director ratifies it.
- **Existing corridors M7 perturbs are measured, not re-derived:**
  - population and demography corridors (casualties, starvation under siege);
  - the food corridors (supply attrition);
  - `densityPerArableKm2` and `migrationGrossPerDecade` (capture moves control and people).
  
  Their bands stay as ratified. Any move is attributed with a switch-off twin, as `TestConfigs.PreMilitary` did.

### §3.4 COUPLING MAP — PROVISIONAL AS AUTHORED; T7.1 AUTHORITATIVE

| new system | perturbs | re-anchoring expected |
|---|---|---|
| **Movement (kind 10, formation X/Y, Location, Army)** | formation sight and rival visibility follow X/Y (`DiscoveryQuery.FormationNode`, `DiscoveryQuery.cs:270`), so the known extent grows; `KnowledgeLeakTests`; the map legend and unit card (`UnitCardModel.cs:85-109`); the playability-gate assertion "cannot be moved" (`PlayabilityGate.cs:2500`); export codes (`WorkbookCodes.cs:22`) | ALL goldens where a formation moves, attributed with a movement switch-off twin; leak gates re-run |
| **War state** | AI policies (recruitment, construction, discovery); trade between warring polities | AI goldens; trade tests |
| **AutoResolver + casualties** | personnel, cohorts and demography; `MilitaryAttributionTests`; the immortal-soldier limitation (C12) is lifted | demography corridor measurements; all goldens with battles, attributed |
| **Supply attrition (`UnfedUnits` consumer)** | food balance; military upkeep accounting | food tests; switch-off twin |
| **Siege and capture** | `ControlRow`, governance, tax, revolt, the chronicle (a core capture event replaces the UI-derived "CP", `StateChronicle.cs:175-190`), the institution owner (H7-13) | governance and revolt tests; chronicle codebook (`docs/m6-session-export/`); session-export golden |
| **Reconnaissance replacing Explore** | `AiDiscoveryPolicy`; Explore (kind 14) and D-068's blocker text; the mobile-agent `NoRuleSupport` guard (`RecruitmentQuery.cs:221-222`) | discovery tests; AI goldens |
| **Known-extent routing (DW-001, DW-002, DW-006)** | founding siting, trade (`TradeQuery.CanTrade`), road development, claims | founding, trade and road goldens, attributed |
| **Combat stats in content** | `unit-families.json` (today: "no combat statistics"); the research consequence and unlock audits (CI) | audit reports regenerated; charter check |
| **Schema v40** | save readability (v32–v39 readable today, `CanonicalSchema.cs:231-235`) | populated-table round-trip tests per new row type |

### §3.5 ARCHITECTURE BY DOMAIN (PROPOSED)

1. **Movement.**
   - Add `OrderKind.MoveFormation = 10` (load validation `OrderLog.cs:654-661`, world checks, export code "MF").
   - Movement runs in MilitarySystem, or in a new MovementSystem in its own pipeline slot. Today MilitaryUnits is written only by AgeTransition and Military.
   - It writes `Location`, `X`, `Y` and `Army`, moving along `RouteQuery`'s budget prefix within the turn's movement budget (§3.2).
   - Army formations move in the first step; scouts follow.
2. **War state.**
   - A relation table: polity pair, state, since-turn.
   - Declare-war and make-peace orders, validated on PREV.
   - Scope: the minimum war/peace relation only (§7 W1).
3. **Contact and battle.**
   - Contact is opposing formations within ZOC at war, detected on frozen turn-start state.
   - The player is prompted: Command or Delegate.
   - AI-versus-AI battles and delegated battles go to the AutoResolver; commanded ones go to the TacticalResolver.
4. **Casualties and supply.**
   - Losses are debited through the Ledger under a new sink reason.
   - `UnfedUnits` gets its first reader: attrition and desertion back to cohorts.
   - Category B quantities (engineers, supply, medical) enter as coefficients (C8).
5. **Fortification, siege and capture.**
   - Walls act as a coefficient.
   - A siege is a multi-turn operation that starves the besieged store.
   - Capture transfers the `ControlRow` (I-8), a core chronicle event records it, and the institutions transfer (H7-13).
6. **Reconnaissance and the ghost display.**
   - The mobile-agent guard is lifted for scout families.
   - Reconnaissance replaces Explore (charter M7.5).
   - Last-seen ghosts are stored as rows (D-039 Part C).
7. **Known-extent routing.** DW-001 (claims, trade and roads on the known extent), DW-002 (founding reads unseen land) and DW-006 (trade is mutually known).
8. **Content.**
   - Combat stats per identity, derived from equipment (T7.1 records each as derived or chosen).
   - The `unit.*` entities and castle, bastion_fort, dry_dock and military_medicine are classified (H7-08).
9. **UI.**
   - Move orders (right-click move, D-052 §4) and a route preview from `RouteQuery`.
   - A battle prompt; ghost tokens.
   - The command-battle view: a coarse grid with continuous render (D-012).

---

## §4 TASK PACKETS — PROPOSED

The packets are in dependency order. None is allocated until ratification.

| packet | scope (register workstreams) | prerequisites | blocking decisions | acceptance (summary) | size |
|---|---|---|---|---|---|
| **T7.1 FOUNDATIONS AUDIT + SPEC** (this) | every existing constant and denominated field the §3 equations consume, enumerated from code with answers (a)–(d) (§4.1); external battle sources committed before measurement; this spec revised from the findings | Director approves this draft for audit | none | audit table adjudicated; spec ratified by a dated ruling | M |
| **T7.2 RULINGS AND ADRs** (charter M7.0) | ADRs for CR-017 §3 (Action Capacity) and §5 (off-road movement); the war-tempo ruling (W2); D-012 and D-013 recorded | T7.1 | §7 C17-3, C17-5, W2, D-012, D-013 | ADRs filed; ruling record | S |
| **T7.3 CATALOGUE AND COMBAT-STAT CONTRACT** (M7.1, H7-08) | combat fields per identity in `unit-families.json` with provenance; entity classification A–E; MobileAgent table design | T7.1 | Q10, Q4 | charter check passes; every stat derived or recorded as chosen; audits regenerated | M |
| **T7.4 WAR STATE (minimum)** | relation table, declare-war and peace orders, AI posture | T7.2 | W1 | turn-exact delivery pins; populated-table round trip; AI only-legal tests | S |
| **T7.5 MOVEMENT (Army)** (M7.2, H7-01, H7-02, DW-018, DW-019) | kind 10, movement budget, ZOC, Action Capacity per the T7.2 ADR; UI right-click move and route preview | T7.2, T7.3 | C17-3, C17-5, W2 | turn-exact pin per delivery semantic; tie-dense path tests; replay; leak gates; movement switch-off twin | L |
| **T7.6 CONTACT + AUTORESOLVER + CASUALTIES** (M7.4, H7-04) | contact detection on frozen state; `IBattleResolver`; AutoResolver (Lanchester); Ledger casualties; soldiers become mortal (C12 lifted) | T7.4, T7.5 | D-013 | exact conservation; resolver purity; RNG stream pin; demography measured and attributed | L |
| **T7.7 SUPPLY ATTRITION** (M7.4, ADR-055 §10) | the `UnfedUnits` consumer; attrition and desertion; category B supply | T7.6 | none | exact conservation; food corridors measured | S |
| **T7.8 FORTIFICATION, SIEGE, STARVATION, CAPTURE** (M7.4, D-039 D, H7-13, DW-020, DW-021) | walls as a coefficient; the siege operation; capture transfers control; core chronicle event; institution transfer | T7.6 | CR-019 (M7 or M8), conquest scope | capture pins; governance and revolt tests; chronicle codebook regenerated | L |
| **T7.9 RECONNAISSANCE + GHOST DISPLAY** (M7.5, H7-07, DW-001 ghost part) | scout families recruitable; reconnaissance replaces Explore; stored ghosts; generals as resolver input | T7.5 | B6 (scouts), Q4 (generals) | leak gates; AI discovery goldens attributed; D-068 text retired | M |
| **T7.10 KNOWN-EXTENT ROUTING** (DW-001, DW-002, DW-006; D-067 R10) | claims, trade, roads and founding read the known extent | T7.1 (independent lane) | none | metamorphic fog tests; founding, trade and road goldens attributed | M |
| **T7.11 TACTICALRESOLVER + PARITY SUITE** (H7-04) | the WEGO command battle: pulses, order verbs, order log per pulse; the battle view; the parity battery | T7.6 | D-012, D-013 | blow-for-blow replay; parity within the pre-committed band; Director playtest of a command battle | L |
| **T7.12 AI MILITARY BEHAVIOUR** | move, attack, defend and siege decisions from `PolityKnowledge` only; D3 (material needs) if ruled | T7.6, T7.8, T7.9 | D3 | only-legal tests; tie-dense ordering; fog-honesty metamorphic tests; 4-seed measurement | M |
| **T7.13 MILITARY BUILDINGS + CALIBRATION** (M7.7) | barracks, depot, siege workshop and walls realized; calibration battery for battle | T7.8 | none | building charter rows realized; calibration report | M |
| **T7.14 WATER EDGES, PORTS, NAVAL AUTO-RESOLVE** (M7.6) | water movement, ports, naval transport; naval battles auto-resolve | T7.5, T7.6 | N1, D7 | (separable; may move to M7.x) | L |
| **T7.15 GARRISONING (GA)** (M7.8, H7-14) | garrisoned air defence and garrisons, counted once (C9) | T7.6 | GA placement | no double counting, proven by test | S |
| **T7.16 INTEGRATION + DIRECTOR PLAYTEST** | full validation, soak, Windows build, playtest checklist, exit inventory | all in scope | none | §5 met | M |

- **Critical path:** T7.1 → T7.2 → T7.5 → T7.6 → T7.8 / T7.11 → T7.16.
- **Independent lanes:**
  - T7.10, after T7.1;
  - T7.3, after T7.1;
  - T7.4, after T7.2;
  - T7.9, after T7.5;
  - T7.13 and T7.15, late.
- **Golden-moving packets:** T7.4, T7.5, T7.6, T7.7, T7.8, T7.9, T7.10 and T7.12. Each ships a switch-off twin and an attribution record (the T3 and ADR-055 precedent).

### §4.1 T7.1 FOUNDATIONS AUDIT — SCOPE

- **What is in scope.** Every existing constant and denominated state field the §3.2 equations consume, followed back through its producer. This includes code literals and every consumer: systems, metrics, autoplay, corridors and view-models. Known members:
  - `KmPerNode`, the lattice step cost and the movement-cost raster (`Worldgen.cs:132-144`);
  - dt per era;
  - the 360-day year;
  - `UpkeepGrainPerPersonYear`, `supportYears` and `reequipExperienceTransfer`;
  - `FormationSightNodes`;
  - the personnel draw;
  - the grain store and consumption (the siege input);
  - the control-strength reach;
  - the revolt thresholds;
  - every `unit-families.json` recruitment field.
- **The four questions per row:**
  - (a) the real-unit value, and whether it is physically possible;
  - (b) whether the producer's denomination matches the consumer's;
  - (c) whether it was derived or chosen;
  - (d) whether tuning can see it.
- **Dispositions:**
  - a (b) mismatch or a frozen-item conflict opens a CR;
  - an impossible magnitude is escalated before any dependent packet;
  - "chosen, never derived" is queued.
- **Deliverable:** `docs/m7-foundations-audit.md`, in the `m6-foundations-audit.md` format.

---

## §5 EXIT CRITERIA — PROPOSED

Nothing is complete because tasks merged or CI is green.

1. **Movement.** A formation moves along a route under its movement budget, delivered on the pinned turn. Replay and save/load mid-move are exact.
2. **Conservation.** Every casualty, desertion, attrition and capture moves people and goods only through the Ledger. The tests use exact equality, including a populated-table round trip for every new row type.
3. **Battle contract.** Both resolvers implement `IBattleResolver` with identical inputs and outputs. A commanded battle replays blow for blow from the order log.
4. **Parity.** The AutoResolver lies within the pre-committed band of the TacticalResolver autoplay median across the parity battery.
5. **Siege and capture.** A settlement can be besieged, starved and taken. Control passes to an existing polity, a core chronicle event records it, and institutions transfer as ruled.
6. **Fog.** Moving formations reveal and lose sight. Rival forces show as stale ghosts. Reconnaissance replaces Explore's stand-in. The AI reads only what it knows, proven by metamorphic tests.
7. **Known-extent routing.** DW-001, DW-002 and DW-006 are closed, with attributed golden moves.
8. **Law conformance.** Every new rate is per sim-year × dt, or exempt by a ratified ADR. There are no free-floating buffs, no era gates and no banned constructs.
9. **Determinism.** The in-process and cross-process legs and CI on the final tree are green. The Windows smoke passes.
10. **Human gates.** The Director plays the M7 slice and passes a checklist that includes at least one commanded battle, one delegated battle and one siege.

---

## §6 GOVERNANCE AND HOUSEKEEPING

- **Ratification.** A dated Director ruling on this spec (a D-record) opens T7.2. Until then, only T7.1 (audit and spec revision) may proceed: documentation only. Partial authorization is possible (the D-062 precedent).
- **Living spec.** Audit findings that move a §3 row revise this spec as dated notes. They are not CRs.
- **Records found stale at drafting:**
  - `docs/milestones.md`'s table still shows M6 as CURRENT. D-074 closed it.
  - The PROPOSED headers on ADR-047/048/049 and TC-1 are carried (D-074 §1).
  
  These are noted here, not edited (gov-4 §6).
- **Packet headers.** Every packet names its milestone, packet, governing D/ADR, spec, frozen contracts, scope and exclusions (gov-4 §8).
- **Verification.** One worktree per verifier. No fix is acted on before its verdict. Mutant runs are bounded. "Teeth are not aim" (ADR-015 §6–§7).

---

## §7 UNRESOLVED DIRECTOR DECISIONS

| group | decision | recommendation | alternatives | consequence | blocks |
|---|---|---|---|---|---|
| C17-3 | **Action Capacity vs Law 3** (CR-017 §3) | **B:** Action Capacity is an agency budget per decision interval, exempt from Law 3; every physical effect still × dt | A: capacity per sim-year × dt (a dt-0.5 turn gets 1/20 of a dt-10 turn); C: override Law 3 | B keeps the turn playable at every dt; A makes late-game turns nearly actionless | T7.5 |
| C17-5 | **Off-road movement vs the D-009/D-010 graph** (CR-017 §5) | **A:** the network is the fast route and the supply object; off-road movement samples terrain cost. The shipped lattice pathfinder already samples it | B: off-road as the network's lowest tier; C: amend D-009 | A reuses `Pathfinder` with no new graph | T7.5 |
| W2 | **War tempo:** how movement and battles fit 10-year turns (§3.2 warning) | **War pulses:** while at war, a turn is subdivided into fixed sim-time pulses (for example one campaign season); movement and contact run per pulse | a per-turn distance cap (simpler, unrealistic at long turns); battles only at turn end | Without this, an A1 army crosses the map in one turn | T7.5, T7.6 |
| W1 | **War-state scope in M7** | the minimum war/peace relation only; treaties M8 | a full diplomacy table now (scope creep into M8) | battles need a hostility predicate | T7.4 |
| D-012 | **Battle grid** | coarse deterministic grid (~48×32) + continuous render | hex or free continuous | determinism and pathing cost | T7.11 |
| D-013 | **Pulse budget** | 6–12 pulses ≈ one day of battle | fixed 8; era-varying | the parity battery's shape | T7.6, T7.11 |
| B6 | **Scouts:** a class, a flag or a capability score (D-039) | a capability score on any formation, plus dedicated scout families | a class only | reconnaissance as an investable capability (D-039 B) | T7.9 |
| Q4 | **Special people / generals** (CR-017 §7: D-043 B3 vs M4 R-1 / D-037 A1) | generals as notables in the resolver setup only; the full system M9 | a full special-people system now | the D-011 delegation input | T7.9, T7.11 |
| Q10 | **Snipers:** a formation or an attached capability | an attached capability (category B) on line formations | its own family | the D-065 R14 requirement is met either way | T7.3 |
| GA | **Garrisoning placement** | M7.8, as charted (T7.15) | M8 | D-065 R11 is a requirement | T7.15 |
| N1 | **Naval scope** | T7.14 is in M7 but separable (cut to M7.x without failing exit) | in, and exit-gating; out to M8 | a large packet | T7.14 |
| D7 | **naval_transport (family 10)** | make it implicit (ADR-031 amendment), per D-065 R13 | keep it as a family | T7.14's shape | T7.14 |
| CR-019 | **Revolt polity vs the raider gate** | rule in M7 with capture (T7.8) | defer to M8 | the capture semantics | T7.8 |
| H7-13 | **Conquest and institution transfer** | transfer with control; annexation (`MergeInto`) M8 | M8 for both | the law court and universities on capture | T7.8 |
| D1 | **RC-1 timing** (aircraft late in A9) | B, magnitude 6.0 (execution report §7) | as shipped | Flight I reachability | T7.3 |
| D2 | **A9 research throughput** (84–87% of RP lost) | a finite-first ADR | as is | late-game pacing | — (not M7 core) |
| D3 | **AI material-needs goal** (EC packets inert; the AI recruits once, at turn 243) | rule it into T7.12 | leave it | an AI army worth fighting | T7.12 |
| D5 / R1 | **Research-economy candidate and pacing** (carried from M6) | a separate ruling outside M7 | fold it into M7 | Age reach in play affects which units appear | — |
| R2 | **M6.24** (stone floors, richness classes, primitive toolmaking) | assign it to M7.x or M8 explicitly | leave it unscheduled | the toolmaking-needs-bronze observation | — |
| OD-3 / Q8 | **EC-4a timing and the energy owner** | M10, as recommended | earlier | none in M7 | — |
| D9 / D10 | **Close formally** (reconciled by the M6 closure audit) | confirm closed | — | record hygiene | — |

---

## §8 MISSING INFORMATION AND WORKING ASSUMPTIONS

**Missing:**
- **The war tempo (W2)** is the biggest gap. 10-year A1 turns make any per-turn movement unrealistic.
- **External battle-loss sources** for §3.3 are not yet chosen.
- **No combat statistics exist in content.**
- **The AI's military intent** beyond recruitment is unspecified.

**Assumed in this draft (each reversible by ruling):**
- the minimum war state only (W1);
- naval is separable (N1);
- generals only, of the special people (Q4);
- snipers as an attached capability (Q10);
- the CR-017 recommendations (§3 B, §5 A).

---

## §9 PROVENANCE

**MEASURED at `7d5defc`:**
- MilitaryUnitRow `WorldState.cs:1090`;
- FormationPersonnelRow `:1116`;
- the kind-10 reservation `OrderLog.cs:164`;
- the load rejection `:654-661`;
- Pathfinder `Pathing/Pathfinder.cs:86`;
- the RouteQuery budget prefix `State/RouteQuery.cs:43`;
- the cost-unit definition `Pathing/LatticeGeometry.cs:39-44`;
- the upkeep expression `Systems/Military/MilitarySystem.cs:104`;
- sight `State/DiscoveryQuery.cs:250,270`;
- `PolityKnowledge.KnowsUnit` `:189`;
- revolt control moves `Systems/Revolt/RevoltSystem.cs:84-98`;
- the UI-derived capture event `Sim.Ui/ViewModel/StateChronicle.cs:175-190`;
- schema v39 `Kernel/CanonicalSchema.cs:231-235`;
- the mobile-agent guard `State/RecruitmentQuery.cs:221-222`;
- the unit card `Sim.Ui/ViewModel/UnitCardModel.cs:85-109`;
- the gate assertion `Sim.Ui/Headless/PlayabilityGate.cs:2500`;
- era turn lengths `Sim.Data/content/era-pacing.json`.

**RULED or RATIFIED:**
- D-011;
- D-039;
- D-052 §4;
- D-062 R3 and R6;
- D-065;
- D-066 R1 and R5;
- D-067 R10, R13 and R15–R19;
- D-068 R4;
- D-069;
- D-074;
- ADR-055;
- ADR-056.

**PROPOSED (records not yet ratified):** unit charter §14, building charter §12, the execution-pass report §7 (D1–D10), and CR-017's recommendations.


# DIRECTOR'S REVISION: UNIFIED WARFARE ARCHITECTURE AND M7.1-M7.20

**Status: PROPOSED FOR REVIEW.** This revision records the Director's latest design direction. It supersedes conflicting proposals in the original draft only where stated. It does not silently amend binding rulings. In particular, conflicts with D-011, D-012, D-013 and CR-017 require formal rulings before implementation.

## A. Confirmed design direction

- M7 builds a reusable military simulation environment and standalone War Sandbox, not merely a campaign battle viewer.
- Campaign battles and sandbox battles use the same simulation kernel and domain modules.
- Visual direction is a high-quality Total War-style 3D battlefield. Graphics work starts during M7.
- Individual soldiers and machines exist as simulation entities. The player commands formations; subordinate entities act and position themselves under formation intent.
- **Animator AI** means visual animation/presentation only. **Enemy AI** means opposing-force decision-making. These must have separate interfaces, responsibilities, logs and tests.
- Tactical battles use fixed-duration pulses with smaller internal time steps. Both sides' orders resolve against a common evolving state. Movement, firing, charges, target acquisition, interruption and timed reinforcements can overlap within a pulse.
- Realistic spotting, line of sight, concealment and terrain-based hiding are required. Forests and other cover must have meaningful tactical effects.
- Full terrain, structure and deployment editing is required.
- Decisions must be inspectable. M7 is initially a developer/test environment. Real-world war experimentation is out of scope until the whole game is integrated and tested.
- No autonomous tactical personality layer. Enemy AI makes inspectable decisions based on its knowledge, objectives and legal orders.
- Historical authenticity is a design objective. Shared interfaces must not flatten materially different historical weapons, formations, movement, detection, logistics and combat.
- Initial proposed per-formation caps:

| Formation/entity type | Maximum |
|---|---:|
| Heavy/aggregated infantry | 50 soldiers |
| Archers and lighter infantry | 60 soldiers |
| Tanks | 5 vehicles |
| Helicopters | 3 |
| Ships | 5 |
| Fixed-wing aircraft | 1 |
| Snipers | 2 personnel |
| Mortars | 5 weapon systems/crews, exact counted object to be defined |

These are proposed limits, not measured performance guarantees. M7.1/M7.6 must define precisely what is counted for each family and validate recruitment, splitting, merging, casualties and save/load against the caps.

## B. Architecture: one simulation kernel, multiple domain modules

**Do not build disconnected battle engines.** Build one deterministic military simulation framework with domain-specific modules and a combined-arms interaction layer.

### B1. Shared kernel

The kernel owns the common simulation clock, pulse scheduler, internal steps, deterministic event ordering, entity identity/lifecycle, formation membership, order contracts, movement/spatial queries, detection/knowledge contracts, target acquisition interfaces, effects/damage application, replay, save/load, RNG ownership, inspection and event logging.

It must not become one giant collection of era-specific combat conditionals. Domain modules provide explicit capabilities and rules through stable contracts.

### B2. Domain modules

| Module | Domain responsibility |
|---|---|
| Land warfare | Infantry, cavalry, archers, artillery, ground vehicles, melee, projectiles, armour and land formations |
| Air warfare | Aircraft, helicopters, flight state, air-to-air, air-to-ground, interception and air defence interactions |
| Naval warfare | Surface vessels, maritime movement/detection, naval weapons, damage, sinking, ports and transport |
| Siege operations | Investment, blockade, siege works, bombardment, breaches, sorties, relief, starvation, assault and surrender |
| Combined-arms interaction | Cross-domain detection, target classification, engagement eligibility, weapons, interception, support and effects |

A domain module may contain distinct equations and mechanics. It must reuse the kernel's clock, event ordering, identity, inspection, persistence and replay contracts.

### B3. Combined-arms interaction

Combined arms is not another battle engine. It connects domain modules through capability contracts. Examples include aircraft attacking tanks, tanks engaging helicopters, ground air-defence engaging aircraft, ships engaging aircraft, aircraft attacking ships, and artillery engaging detected/designated ground targets.

A cross-domain engagement follows a testable sequence: detect or receive valid target information → identify/classify as required → establish engagement eligibility → select compatible weapon/effect → resolve interception/defence → apply damage/effects → update knowledge and inspection records.

Not every entity can attack every other entity. Compatibility derives from equipment, weapons, sensors, target class, range, altitude/depth, line of sight and relevant capability state. Do not add era gates where actual capability state should govern.

### B4. Siege is a persistent operation, not a separate engine

A siege advances over operational/campaign time and owns investment, blockade, supply, starvation, construction, fortification condition, bombardment, sorties, relief, surrender and capture state. Tactical assaults use the land module. Naval blockade or amphibious action uses the naval/combined-arms modules. Siege must not duplicate combat resolution.

### B5. Historical authenticity

Ancient/medieval melee, archery, cavalry and siege equipment must not be forced into modern fire-control assumptions. Modern warfare must support relevant distinctions such as armour, penetration, guided weapons, sensors, aircraft, air defence and combined-arms engagement. Shared interfaces are abstractions, not identical mechanics.

## C. Entities, formations and rendering

Keep three layers distinct:

1. **Simulation entity:** an individual soldier, vehicle, aircraft, ship, weapon team or other simulated object with type-appropriate state.
2. **Formation:** the player-commandable group, owning formation orders, membership and command state. Individual entities execute formation intent through deterministic local behaviours.
3. **Render representation:** visual models, instances, grouped representations or impostors chosen according to camera distance and performance budget.

The renderer may reduce visual detail, animation frequency or model count. It must not merge or delete simulation identities. Simulation outcomes must be invariant under camera position, render quality, frame rate and LOD.

**Animator AI** converts simulation state and formation intent into visual spacing, facing, locomotion, weapon handling, hit reactions and death animations. It must not choose targets, invent movement, decide casualties, alter morale or change combat outcomes.

**Enemy AI** selects formation-level orders using only permitted knowledge (`PolityKnowledge`/battle knowledge). It does not control visual animation.

## D. Two clocks plus internal steps

The original draft risks conflating campaign war tempo with tactical battle pulses. Separate them:

1. **Campaign turn:** existing civilization turn with era-dependent `dtYears`.
2. **Operational war pulse:** fixed interval for strategic movement, contact, reinforcement, siege progression and campaign operations. Its configured duration may be weeks in earlier eras and days in modern eras, subject to a dimensional audit and formal ruling.
3. **Tactical battle pulse:** a much shorter fixed interval for battle resolution. It must be suitable for movement, firing, charges, interception and other events during continuous resolution.
4. **Internal simulation step:** smaller deterministic time increments/event processing within a pulse.

A siege does not run as one enormous tactical battle. Investment, supply, starvation and siege works progress over operational/campaign time; an assault invokes tactical battle resolution.

During tactical pulse resolution, both sides' orders act against a common evolving state. A charge may contact a moving formation; archers may acquire and fire on a newly valid target; suppression/casualties may change subsequent actions; reinforcements may enter at configured times. Define deterministic event priority, tie-breaking, collisions, interruptions, target invalidation, simultaneous effects and RNG consumption. Do not impose artificial movement-then-fire phases where they contradict the time model.

## E. Graphics and performance

Graphics work starts early, with a separate renderer/simulation boundary. Required foundations include 3D terrain, elevation, slopes, water, vegetation, roads, structures, fortifications, deployment zones, individual models, instancing, batching, culling, LOD, animation throttling, camera, selection, tactical map, overlays, inspection panels and simulation-driven effects.

The proposed formation caps are not a maximum battle-size guarantee. Benchmark simulation and rendering separately: CPU, GPU, memory, draw calls, animation cost, simulation throughput and worst-case entity counts. Set target hardware and measurable performance budgets before promising scale.

"Fusing ten units into one representation" is permitted only as a visual batching/LOD technique. It must never merge their simulation state or identities.

## F. War Sandbox

The sandbox is a standalone mode using the same kernel and domain modules as campaign battles. Its planned modes are:

- **Quick Battle:** configure sides, forces, terrain, conditions and deployment.
- **Scenario Editor:** edit terrain, elevation, vegetation, roads, water, structures, fortifications, objectives, deployment zones and timed reinforcements.
- **Battle Test:** controlled experiments, seeded repetitions, autoplay and parameter sweeps.
- **Replay and Inspection:** inspect orders, events, visibility, target decisions, movement, damage, casualties and causal explanations.
- **Campaign Encounter:** later load a real campaign encounter into the same simulation without changing combat rules.

The first playable slice should prove a small land scenario, individual entities, formation orders, pulse resolution, replay and inspection. Graphics, terrain and simulation can proceed as parallel lanes after contracts are established.

## G. Revised M7.1-M7.20 task decomposition

These are proposed task packets, not implementation authorization. Each packet needs its own scope, governing rulings, dependencies, acceptance tests and explicit authorization.

| Task | Name | Deliverable | Dependencies |
|---|---|---|---|
| **M7.1** | Foundations audit and architecture reconciliation | Read-only audit of code, schemas, clocks, movement, spatial queries, rendering stack, persistence, replay, RNG, battle contracts and performance. Reconcile all proposals with binding rulings. | None |
| **M7.2** | Governance and decision rulings | Resolve Action Capacity, off-road movement, operational/tactical pulse definitions, internal step, spatial model, individual entity persistence, naval resolver, siege boundary and required D-record/ADR amendments. | M7.1 |
| **M7.3** | Unified simulation contracts | Kernel/domain interfaces, entity lifecycle, formation ownership, orders, deterministic event queue, time model, target/effect contracts, inspection, replay and persistence boundaries. | M7.1-M7.2 |
| **M7.4** | Battlefield renderer foundation | 3D scene, camera, terrain rendering path, entity rendering, selection, lighting, profiling and simulation/render separation. Deliver a visually compelling empty battlefield. | M7.1; aligned with M7.3 |
| **M7.5** | Terrain, structures and scenario editor | Terrain layers, elevation, vegetation, water, roads, bridges, buildings, walls, fortifications, deployment zones, objectives, editable structures and scenario persistence. | M7.3-M7.4 |
| **M7.6** | Individual entities and formation model | Individual entities, formation membership, caps, identity/lifecycle, formation-level command state and deterministic local positioning contracts. | M7.3 |
| **M7.7** | Movement and spatial interaction | Strategic and tactical movement interfaces, pathfinding, terrain costs, collision/occupancy, spacing, facing, turning, charge movement and obstacles. | M7.3, M7.5-M7.6 |
| **M7.8** | Detection, visibility and concealment | LOS, spotting, sensors, vegetation/terrain concealment, remembered/ghost contacts, confidence and fog-honest knowledge. | M7.5-M7.7 |
| **M7.9** | Land warfare foundations | Historical land combat profiles: melee, formations, archery, cavalry, projectiles, armour/defence, morale/cohesion and period-specific equipment. | M7.3, M7.6-M7.8 |
| **M7.10** | Individual combat and Animator AI | Individual actions under formation orders; visual locomotion, spacing, facing, weapon handling, firing/reloading, hit/death animation. Prove renderer independence. | M7.4, M7.6-M7.7, M7.9 |
| **M7.11** | Tactical pulse and WEGO resolution | Fixed tactical pulses, internal steps, simultaneous orders, deterministic events, interruptions, collisions, target acquisition during pulse, timed reinforcements, order log and exact replay. | M7.2-M7.10 |
| **M7.12** | Enemy AI and battle inspection | Fog-honest opposing-force decisions, formation-level orders, explainable decisions and developer inspection. No tactical personality layer. | M7.8-M7.9, M7.11 |
| **M7.13** | Siege operations | Persistent investment, blockade, supply, starvation, siege works, bombardment, breaches, sorties, relief, assault, surrender and capture. Assaults use land combat. | M7.7-M7.12; campaign contract |
| **M7.14** | Logistics and operational warfare | Operational war pulses, strategic movement, reinforcement, supply, upkeep, attrition, command delay and campaign-scale contact. | M7.2-M7.3, M7.6-M7.7 |
| **M7.15** | Naval warfare module | Naval entities, movement, detection, formations, weapons, damage, sinking, ports and transport. Tactical versus auto-only resolution decided by formal ruling. | M7.3, M7.6, M7.8, M7.11, M7.14 |
| **M7.16** | Air warfare module | Aircraft/helicopters, flight state, detection, air-to-air, air-to-ground, interception, air defence, basing and ratified era progression. | M7.3, M7.6, M7.8, M7.11, M7.14 |
| **M7.17** | Combined-arms interaction | Cross-domain detection, classification, engagement eligibility, weapons, interception, damage/effects and support. Test aircraft-tank, tank-helicopter, air-defence-aircraft, ship-aircraft interactions. | M7.9, M7.15-M7.16 |
| **M7.18** | War Sandbox and test laboratory | Quick Battle, scenario creation, force configuration, deployment, objectives, replay, inspection, seeded tests, analytics and scenario persistence. | M7.4-M7.12; integrate domain modules incrementally |
| **M7.19** | Campaign integration and resolver parity | Connect campaign formations, real personnel, Ledger casualties, recruitment, supply, war state, reconnaissance, siege/capture and battle setup. Implement AutoResolver/TacticalResolver contract and parity battery. | M7.11-M7.18 |
| **M7.20** | Whole-M7 integration, optimization and exit | Cross-era/domain validation, determinism, replay/save-load, historical plausibility, performance/memory benchmarks, large-battle stress tests, Windows/CI, Director playtest and exit report. | All required packets |

### Parallel lanes after M7.1-M7.3

- **Graphics:** M7.4 → M7.5 → visual work in M7.10/M7.18.
- **Simulation:** M7.6 → M7.7 → M7.8 → M7.9 → M7.11.
- **Campaign:** M7.14, then M7.13 and M7.19.
- **Domains:** M7.15 and M7.16 after shared contracts; M7.17 connects them.
- **AI/testing:** M7.12 and test-laboratory work in M7.18 as interfaces stabilize.

Parallelism does not authorize incompatible implementations. Every lane uses M7.3 contracts and the same deterministic kernel.

## H. Binding conflicts requiring explicit rulings

| Existing record | Existing position | New direction | Required resolution |
|---|---|---|---|
| D-011 §5 / original C5 | Naval battles always auto-resolve; sieges are multi-turn strategic operations, not battles | A naval domain module is required; siege remains a persistent operation with tactical assaults | Preserve siege as operation. Director must decide whether naval battles remain auto-only or gain TacticalResolver support. |
| D-011 §1, §3; D-013 | 6-12 command pulses, approximately one day of battle | Tactical fixed-duration pulses with internal steps; operational pulses may be days/weeks | Separate clocks. Amend D-013 only if tactical pulse budget/duration changes. |
| D-011 §2 | Formation-level command, dual resolver and parity | Individual soldiers simulated but not individually commanded | Compatible if individuals are subordinate simulation entities. Rule persistence/identity semantics. |
| D-012 | Coarse deterministic grid with continuous render proposed | Total War-style 3D, editable terrain and individual movement | Continuous rendering can coexist with deterministic spatial simulation. Audit whether the proposed grid supports movement, LOS and projectiles. |
| CR-017 §3 and §5 | Action Capacity and off-road movement unresolved | Movement and pulses central to M7 | Resolve before movement implementation. |
| D-065 R3 | Aviation staged; later aviation scope constrained | Air module required architecturally | Build generic contracts; implement only ratified era/capability scope. |
| D-065 R8-R10 | Formation is independently controllable family; support may be quantity/attachment | Individual entities exist within formations | Keep formation as player-commandable family; subordinate members are not new player-commandable families absent a ruling. |

## I. Additional M7 acceptance criteria

1. Land, naval and air modules use the same deterministic clock, event, identity, inspection, save/load and replay contracts.
2. At least one cross-domain engagement runs end-to-end through the combined-arms layer.
3. A formation is commanded as one unit while its individual entities act under its intent.
4. Camera, LOD, animation detail and frame rate cannot alter simulation results.
5. Terrain concealment demonstrably affects detection and is inspectable.
6. Events can occur during a pulse, interrupt actions and resolve deterministically under simultaneous orders.
7. Siege progresses on operational/campaign time; assault invokes land combat.
8. A saved sandbox scenario reopens and replays identically.
9. Performance reports entity counts, formation counts, frame rate, simulation throughput, CPU/GPU memory and stress results. Do not promise a maximum battle size before measurement.
10. Representative eras demonstrate that shared interfaces have not flattened historical differences.
11. Material decisions can be traced to inputs, knowledge, rules/capabilities, events and state changes.
12. Campaign resolvers preserve the ratified common contract, parity and Ledger conservation.

## J. Immediate next step

**M7.1 is a read-only foundations and architecture audit.** It must inspect:
- current rendering/UI technology and viability of a Total War-style 3D scene;
- existing entity/animation infrastructure and simulation/render separation;
- military entity, personnel, formation and order schemas;
- deterministic pathing, spatial queries, LOS and terrain representation;
- save/load, replay, RNG and event ordering;
- reuse opportunities and architectural gaps;
- performance constraints and target hardware;
- exact amendments needed to D-011, D-012, D-013 and CR-017.

M7.1 must not implement the renderer or battle simulation. It produces a measured architecture audit, coupling/dependency map, binding-conflict list and M7.2 decision docket. After review, the Director can ratify the architecture and authorize M7.2/M7.3.

---

## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)

*The text above is the Director's revision file copied exactly: lines 1–373 are the earlier agent draft, and lines 374–567 are the "DIRECTOR'S REVISION: UNIFIED WARFARE ARCHITECTURE AND M7.1-M7.20" (§A–J). Line numbers below are those of the text above, and match the REV:n citations in `docs/d075-m7-unified-warfare-direction.md`.*

**DECIDED (Director, 2026-11-03).** The Director's Revision §A–J (lines 374–567) is RULED by D-075 (`docs/d075-m7-unified-warfare-direction.md`), with the architecture recorded in ADR-060 (`docs/adr/adr-060-unified-warfare-simulation-architecture.md`). The revision's own status line (line 376, "PROPOSED FOR REVIEW") is kept as historical text and is overtaken by the Director's clarification of 2026-11-03. Where §A–J conflicts with the original draft, §A–J governs. Following line 376's own limit, each supersession below extends only as far as the words of the replacing clause; draft content that §A–J does not address stays as written, as a proposal.

**The original draft's parts that are SUPERSEDED (ADMIN: recorded as the consequence of D-075; rows S58–S61 of D-075 §2.5):**
- **§1 C1 and C5 (lines 50 and 54), only where affected.** C1's "Era-constant order verbs, with era additions (bombard, air strike …)" is superseded in part as to the air-strike mechanism: air-to-ground and air-to-air sit in the air module and interact through combined arms (§B2, §B3; D-075 R7, R9, R12). C1's "6–12 command pulses per battle" is **not** decided: it is OPEN (D-075 O-4). C5's "Sieges are multi-turn strategic operations, not battles" is superseded in part by §B4 and §D (lines 436–438, 467; D-075 R40, R41): a siege is a persistent operation over operational/campaign time, and an assault invokes tactical battle resolution through the land module. C5's "Naval battles always auto-resolve" is **not** decided: it is OPEN (D-075 O-1, line 532).
- **§2 scope fence, naval and aviation items, only where affected.** The "Commanded naval battle" item (line 76): its **ASSUMED** clause that naval movement and ports are "a late, separable packet that can be cut to M7.x without failing the exit" is SUPERSEDED, since the naval module is packet M7.15 and acceptance criterion I.1 requires naval to share the kernel contracts at exit (lines 511, 542; D-075 S61, R7, R48, R51); "It stays auto-resolve (C5)" is OPEN (O-1). The aviation item (line 75) is superseded only to the extent that the air module, including air-to-air, air-to-ground and interception, is an M7 packet (M7.16, line 512; D-075 R7, R12); the Flights II–III, submarine, carrier, cyber and space deferrals stand as written, and whether tests and the sandbox may use air units outside the Age scope reached in play is OPEN (O-31).
- **§3.5 Architecture by domain (lines 164–198), only where affected.** Superseded in part by §B (one simulation kernel with land, air and naval domain modules and a combined-arms layer, lines 406–438; D-075 R5–R12), §C (entities, formations, Animator AI, lines 444–456), §D (two clocks plus internal steps, lines 458–469) and §E (a 3D battlefield with a separate renderer/simulation boundary, lines 471–477; D-075 R21, R22). Specifically: item 5's "A siege is a multi-turn operation that starves the besieged store" yields to §B4 (whether the siege or the food system owns starvation is OPEN, O-21); item 9's "The command-battle view: a coarse grid with continuous render (D-012)" yields to the 3D battlefield of §A and §E (the spatial model itself is OPEN, O-7). Items not addressed by §A–J (for example, war state, known-extent routing and content classification) remain proposals.
- **§4 task table T7.1–T7.16 (lines 202–232), including the critical path "T7.1 → T7.2 → T7.5 → T7.6 → T7.8 / T7.11 → T7.16".** SUPERSEDED and replaced by §G, M7.1–M7.20 (lines 491–526; D-075 S59, R48, R49). The M7 critical path is the Director's: **B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29** (clarification item 9; D-075 R53). T7 labels survive only as aliases (D-075 §4.5).
- **§7 items decided today.** **GA** (line 304, "M7.8, as charted (T7.15) | M8"): decided, GA is in M7 as B7-25 (D-075 S60, R54). **N1** (line 305, naval "separable"): decided, the naval module is in M7 as M7.15 and exit-gating through I.1 (D-075 S61). **W2** (war tempo): decided in principle by §D's operational war pulse (line 463; D-075 R29); its duration is OPEN (O-2). Crossing campaign-turn boundaries is not open: the Director's ledger Part I §19 (RATIFIED) rules "War Pulses can cross strategic turn boundaries" (`docs/design/director-architecture-and-decision-ledger.md:943`), which stays binding (D-075 §3). OPEN are only the mechanism by which crossing is reconciled with the kernel contract and the sub-step rule, CR-017 §6 with CR-006 §1 (O-3), and whether the pulse is a world-global clock (O-29). Every other §7 row, including C17-3, C17-5, D-012, D-013, Q10, D7, W1, B6, Q4, CR-019 and H7-13, is **not** decided by today's decisions; those that §A–J touches are listed as open items in D-075 §5 (for example O-1, O-4, O-7, O-8, O-9, O-32, O-34).

**Not ratified for implementation.** This spec as a whole is not yet ratified for implementation, and **no M7 packet is authorized**. M7.1–M7.20 and every B7 packet wait for the Director's explicit authorization, packet by packet (line 493; D-075 O-22). No code, data, schema, test, golden, build, commit or push is authorized by this note.

**Where the revised plan lives.** The revised M7.2 decision docket, the M7.3 contracts and the dependency plan are in `docs/m7-plan/m7-2-docket-m7-3-contracts.md` (drafted from D-075 §4.6–§4.8).

**OPEN.** Every technical question today's decisions do not resolve is listed for the Director's decision in D-075 §5 (O-1 to O-37, plus O-7a; ADR-060 §4 carries the same ids). None is decided here.

## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)

Record: `docs/d076-m7-decision-reconciliation.md` (Director decisions of 2026-11-04).
- **S79:** draft C5 "Naval battles always auto-resolve" (line 54) was never a decision and is now answered the other way (D-076 N8). The REV caps (lines 391–404) are recovered verbatim and unchanged in D-076 §5.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S356** (`m7-spec.md:50`) — "6–12 command pulses per battle, most ending by rout." — IN PART: no fixed 6–12 ceiling (also D-076 N9; not annotated here); retained: WEGO, era-constant verbs. **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S357** (`m7-spec.md:54`) — "**Naval battles always auto-resolve. Sieges are multi-turn strategic operations, not battles.**" — IN PART: naval: full tactical control; sieges: persistent operations evaluated through battle pulses, max three campaign-turn equivalents; retained: multi-turn. **Replaced by** DIR5 §2.1, §2.2 (D-079 DR8, DR9). [DIRECTOR DECISION]
- **S358** (`m7-spec.md:58`) — "**Snipers are required** (their form is open: §7 Q10)." — IN PART: form decided: independent small specialist formations; retained: the requirement and C9's air-defence clause. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S359** (`m7-spec.md:61`) — "**Soldiers are immortal and the warband draw is as it is until the casualty packet.**" — IN PART: the packet is M7.14; retained: no new behaviour before it. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S360** (`m7-spec.md:71`) — "**ASSUMED:** M7 carries only the minimum war/peace relation a battle needs (§7 W1)." — IN PART: decided: M7 owns basic military records (wars, battles, participants, outcomes, casualties, leader fates), post-battle outcomes and leader execution; retained: treaties and diplomacy are M8. **Replaced by** DIR5 §4.1–§4.3 (D-079 DR36–DR38). [DIRECTOR DECISION]
- **S361** (`m7-spec.md:75`) — "Flight I is in M7 only if its Age is reached in play (D-065 R3)." — IN PART: sandbox supports all designed families regardless of era; retained for campaign. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S362** (`m7-spec.md:76`) — "**Commanded naval battle.** It stays auto-resolve (C5)." — IN FULL: naval tactical control; AutoResolver optional, never default (left OPEN by the D-075 note; not in D-076 S79). **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S363** (`m7-spec.md:115`) — "NEW. DIRECTOR DECISION REQUIRED (§7 C17-3)" — IN PART: decided: at most two whole actions per decision window, integrated with AC. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S364** (`m7-spec.md:121`) — "| **siege duration, starvation** | sim-years;" — IN PART: duration bounded at three campaign-turn equivalents; siege health per battle pulse; retained: grain against the store, no siege-only stock. **Replaced by** DIR5 §2.2 (D-079 DR9). [DIRECTOR DECISION]
- **S365** (`m7-spec.md:185`) — "A siege is a multi-turn operation that starves the besieged store." — IN PART: adds three-turn maximum, siege health and rationing; retained: multi-turn. **Replaced by** DIR5 §2.2 (D-079 DR9). [DIRECTOR DECISION]
- **S366** (`m7-spec.md:272`) — "The Windows smoke passes." — IN PART: Windows/Linux hash equality required; retained: green legs and CI. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S367** (`m7-spec.md:295`) — "| C17-3 | **Action Capacity vs Law 3** (CR-017 §3) |" — IN PART: count decided (two per window); rec. B consistent. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S368** (`m7-spec.md:298`) — "| W1 | **War-state scope in M7** |" — IN FULL: M7 basic military records; M8 political/diplomatic. **Replaced by** DIR5 §4.3 (D-079 DR38). [DIRECTOR DECISION]
- **S369** (`m7-spec.md:300`) — "| D-013 | **Pulse budget** | 6–12 pulses ≈ one day of battle |" — IN FULL (count): no ceiling (also D-076 N9); pulse length open (Q-B1). **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S370** (`m7-spec.md:303`) — "| Q10 | **Snipers:** a formation or an attached capability | an attached capability (category B) on line formations |" — IN FULL: own formations; recommendation not adopted. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S371** (`m7-spec.md:306`) — "| D7 | **naval_transport (family 10)** | make it implicit (ADR-031 amendment), per D-065 R13 |" — IN FULL: controllable transport vessels included. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S372** (`m7-spec.md:308`) — "| H7-13 | **Conquest and institution transfer** |" — IN PART: capture outcomes (sack, raze, occupy/keep) are M7 post-battle choices, design delegated; retained: institution transfer and annexation not addressed. **Replaced by** DIR5 §4.2, §4.3 (D-079 DR37, DR38). [DIRECTOR DECISION]
- **S373** (`m7-spec.md:331`) — "- snipers as an attached capability (Q10);" — IN FULL: superseded assumption. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S374** (`m7-spec.md:484`) — "edit terrain, elevation, vegetation, roads, water, structures, fortifications, objectives, deployment zones and timed reinforcements." — IN PART (READING): deployment zones bounded by the thirds rule; retained: the rest. **Replaced by** DIR5 §2.14 (D-079 DR31, DR32). [READING]
