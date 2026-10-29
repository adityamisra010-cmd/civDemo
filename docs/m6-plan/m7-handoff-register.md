# M7 handoff register — what M6 provides, what M7 implements

**Status: PROPOSED (planning input, 2026-10-20, tree `dc83a67`).** M7 is the Battle Layer: it "CONSUMES M6's
authoritative technology/capability state. No parallel tech vocabulary inside the battle layer, and no 'generic battle
now, tech later'" (`docs/milestones.md:53-58`, RECORDED under the 2026-10-03 rebase). D-011 (frozen, `spine-s8:16`)
owns battle mechanics; D-052 §4 (RATIFIED) assigns movement and Action Points to M7. Ownership below is single: one
column says what M6 delivers, the other what M7 builds; nothing is shared.

| ID | Item | M6 must provide | M7 must implement | Governing sources | Rulings owed first |
|---|---|---|---|---|---|
| H7-01 | Formation movement (`MoveFormation`, order kind 10; m5w S3a/S3c) | Leave kind 10 reserved (`Sim.Core/Kernel/OrderLog.cs:157`); the fog seam's `Visible(node)` / `Known(node)` queries over stored extent (M6.13) that a move's legality and sight will read; no second position table | The order, its validation on PREV via the route query, the writer re-split of `MilitaryUnits` (`AgeTransitionSystem.cs:43-45`), right-click move UI | `d052:46-51`; `m5w-map-workstream-audit-and-plan.md:812-878`; `d011` §1 | CR-017 §5 (off-road vs D-009) |
| H7-02 | Action Points / Action Capacity (S3b; ~~schema reserved v36~~ **no reserved schema version**: v36 is ADR-049's — D-062 R11, D-066 A11) | **DELIVERED (M6.10, D-F RULED option 1):** the interface contract [training-ap-contract.md](training-ap-contract.md) and its DORMANT data (`Sim.Data/content/unit-progression.json` → `training`; validated by `UnitProgressionContentLoader` rules T1–T3; read by nothing — `UnitProgressionInertTests`): dormant = M7 action state `idle` for a whole refresh, passive gain exactly 0; explicit mode; eligibility with one rejection code per condition; cost (AC share, years as rate × dtYears); increment; cap; escalating-years diminishing returns; the carrier as a named open input (§7). Base AC max stays M7.1 unit-families content. No state, no order, no effect in M6 | AC state and its persistence (the next free schema version at landing, or an agent-generic `MobileAgentState` table), the refresh rule, the training action and its order (never kind 10) with a turn-exact delivery pin, one rejection path per code through one predicate, the dormancy test, replay coverage (training-ap-contract.md §9) | `d052:46-51`; ~~`d055:1306-1308` (v35 S2a, v36 S3)~~; `cr-017:289-321` (§3 options, rec. B); D-043 B2 `d043:390-405`; training-ap-contract.md | CR-017 §3 (Action Capacity vs Law 3) — still owed before any M7 movement packet; ~~Decision D-F~~ (RULED option 1, 2026-10-20) |
| H7-03 | Recruitment, headcount, military realization | The capability vocabulary M7 reads: which research/civic nodes gate which unit identities (the 23 `unit.*` entities stay DEFERRED, tier E), validated by the corpus audit (M6.16). Nothing else: the recruitment slice below is NOT an M6 deliverable | **Partly realized early by ADR-055** (PROPOSED; the uncommitted M7 pass under D-065 R7; preserved and not extended in M6, D-066 R5): recruitment from real cohorts (kind 16, `Ledger.Transfer`), headcount as `FormationPersonnelRow` (v39 §70), re-equipment (kind 17) and disbandment (kind 18), the card's personnel line, per-identity knowledge gates. **Still M7:** ratify and merge ADR-055 with the M7 spec; retire the `pending` notes of the Age military milestones in `ages.json` (they still say "in M5 no order or system creates a formation"); the unit-display count D-052 §7 asks for on the map; the Army MobileAgent | `milestones.md:53-58`; `adr-033:151-158` (D7 ESCALATED); `d052:89-90`; `deferred-entity-realization-plan.md:57-63`; `adr-055` | — |
| H7-04 | Battle resolution (`IBattleResolver`, Auto + Tactical, WEGO pulses, parity suite) | Nothing new; the technology state it consumes (equipment identities, Age forms) | Everything in D-011 §1–§3 and §8; D-012/D-013 close at the M7 spec | `d011:27-61, 84-86` | D-011 §8 decisions at the M7 spec |
| H7-05 | Experience and promotions becoming live | **DELIVERED (M6.8/M6.9; D-E RULED option 1 — the "if the Director scopes it" conditional is resolved):** the design note and contract [unit-progression-contract.md](unit-progression-contract.md) (the D-011 §3 fields and terms promotions may reference; the writer split), the INERT promotion content (`unit-progression.json` → `trees`: 3 trees, 12 promotions, 7 land families) and its validator (`UnitProgressionContentLoader` R1–R7, one mutant test per rule; CI step). `MilitaryUnitRow.Experience` stays the earning input that nothing earns before M7 (ADR-055 §6 only keeps or scales it on re-equipment) | The earning rule (D-011 §2 "general experience" via `BattleOutcome`), promotion state (a companion table, the next free schema version) and its writer, the D-011 §3 formation fields with one call site per contract term, refusal as order validation, the lifecycle edges (unit-progression-contract.md §5) | `d011:27, 37`; `WorldState.cs` (`MilitaryUnitRow`); `d043:300-320` (ADR-031 modernization stays automatic and free); unit-progression-contract.md | ~~Decision D-E (progression state in M6?)~~ RULED option 1 (2026-10-20); open inputs in the contract §6 block nothing in M6 |
| H7-06 | Unit training panel | **DELIVERED (M6.12):** one inert line on the unit card, "Training — planned for M7 · '<the family tree's first promotion>': earned in M7" (`Sim.Ui/ViewModel/UnitTrainingPlaceholder.cs`): text only, no control (the card's only control stays close), no order, no world read beyond the card's own family field; PlayabilityGate row `unit card training placeholder is inert (M6.12)`; distinct from ADR-055's real recruit / re-equip / disband controls on the Policy surface | The functional panel: unit inspection, eligibility, upgrade (promotion) selection, training actions, AP integration — replacing the card's line | `UnitCardModel.cs` (`NotSimulatedLines` pattern); 2026-10-20 instruction M6.12; D-066 A2 item 4 | — |
| H7-07 | Command friction, reconnaissance (pointed, era-costed), stale-GHOST display, siege incl. starvation, the campaign layer | The LIVE/GHOST/UNKNOWN **display** and persisted sighting memory moved to M6 by D-052 §5 (`d052:53-66`), scouts per D-039 B6 ruling, the stored known extent | Parts A, D, E of D-039 (command friction, siege, campaign); reconnaissance as an operational verb over M6's visibility state | `d039:7-10, 48-100, 175-181`; `adr-037:22, 43` (M7 placement superseded for GHOST) | D-039 B6 (scouts: class / flag / capability score — "M6's spec owns it") |
| H7-08 | The 23 `unit.*` research entities and castle / bastion / dry_dock / military_medicine | Listed, validated and inert in the corpus (M6.16); their prerequisites inside the tree stay consistent | Their realization (`deferred-entity-realization-plan.md` §E "owned by M7 Battle Layer (do not pull forward)") | `deferred-entity-realization-plan.md:57-63` | — |
| H7-09 | CR-017's remaining collisions (Ages as state, the transition surge, mobile agents, war pulses) | Record which of them M6's Age-integration work touches (M6.3) and raise them before any M6 packet that would | §3 and §5 before any movement packet; the rest when their packets come | `cr-017:3-6, 29-32`; `d052:48-51` | CR-017 rulings |
| H7-10 | MobileAgent (D-043 B1) as the carrier for settlers, scouts, special people | Nothing further: the statement is made — M6 has no scout class (D-J; D-062 "the scout unit and ghost rows move with movement to M7"; D-066 §3), so **M7 creates the first MobileAgent**. ADR-055's `reconnaissance` family (22, `mobile_agent`) is content only, refused for recruitment in this build (ADR-055 §5; unit charter §6.5 item 7). Settlers and special people: [settler-special-character-disposition.md](settler-special-character-disposition.md) (M6.11) | The agent-generic position/AC tables; the first MobileAgent (unit charter §14: M7.2 Army, then scouts; M7.5 reconnaissance replaces Explore); war pulses (D-043 B8); special people after the Q4 / CR-017 §7 ruling | `d043:350-405, 548-570`; `cr-017:549-631` (§7 special people, rec. "a notable with a catalogue identity"); unit charter §14, §16.4 Q4 | CR-017 §7 / charter Q4 (escalated to M7.0) |
| H7-11 | Settler progression | Nothing: a settler is an instantaneous `FoundSettlement` order (`OrderLog.cs:124-128`, ADR-036) — there is no settler unit to progress. If a settler ever becomes a MobileAgent it is an M7/M8 decision | A settler agent, if ruled, with travel on M7's movement | `adr-036`; `d043:350-388` | Director scope decision (D-E) |
| H7-12 | CR-019 revolt polity vs the T4.5 raider gate | Nothing | Options 2/3 at the military/political milestones | `cr-019:3, 44-50` (DEFERRED to M7/M8) | CR-019 |
| H7-13 | Annexation gameplay, conquest, recovery of revolted settlements; transfer of institutions on conquest | Institution/civic state defined so that transfer rules can be written (M6.4–M6.6: what a civic or government row belongs to) | The conquest path (`MergeInto` has no caller), recovery, transfer | `adr-033:258-259`; `m5-integration-coherence-matrix.md:172-183` | institution-transfer ruling (M7/M8) |
| H7-14 | **GA — garrisoning** (D-065 R11; an approved requirement, not yet designed) | Nothing | Unit charter §14 stage **M7.8 GA**, its seven parts: the state model (one capability, one state, a logged and conserved transition); eligibility and deploy/withdraw orders with turn-exact delivery; the city/installation interface; the garrison as an explicit term in local defence and battle inputs (no free bonus); persistence and replay; no double counting (never mobile and garrisoned at once); tests and manual UI validation. Air-defence garrisons stay blocked until family 14 is realizable | D-065 R11; unit charter §13–§14 (M7.8); building charter §5.16a; the 2026-10-27 dated note below | the Director confirms the M7.8 placement (D-065 Addendum 3) |

**Boundary rules this register enforces**

- M6 writes no `MilitaryUnits` row field and adds no per-unit state ~~unless the Director rules otherwise in D-E~~ (D-E RULED
  option 1; D-066 A11: "M6 packets add none"); ~~the writer stays `AgeTransitionSystem` (`SystemCatalog.cs:427-429`)~~ the
  writers are `AgeTransitionSystem` (modernization) and ADR-055's `MilitarySystem` (recruitment, disbandment, re-equipment
  — the M7 pass, D-065 R7), split by field and pipeline order (`unit-progression-contract.md` §1); M6 adds none.
- M6 does not assign order kind 10 ~~and does not take schema v36 (reserved for S3) — see Decision D-H on version
  sequencing if M6's own tables land first~~; v36 belongs to ADR-049 and AP/AC state has no reserved schema version (D-062
  R11; D-066 A11) — it takes the next free version when it lands.
- Anything in M6 that would *change* how AP, movement or battle resolve is out of scope by D-052 §4 and D-011; M6 may
  define data, requirements and interface contracts, nothing more.


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- Unchanged by M6.1. The spec (`docs/m6-spec.md` §3.5 H, §4 T6.12, T6.19) binds M6 to this register's boundary rules;
  D-E/D-F are ruled (option 1). H7-10: the spec recommends no scout class in M6 (D-J), so M7 creates the first MobileAgent.

## Dated note — 2026-10-27 (D-065 Addendum 3; appended, the text above is unchanged)

- **Unit taxonomy ratified** (D-065 R8–R14): the controllable unit tree holds category A formations only. Combat
  engineers, military supply and field medical are category B allocations, consumed by the battle system. Prospecting
  is a category D action. Trade and shipping are category E. Naval transport (family 10) is under reconsideration
  (ADR-031 amendment pending). Snipers are required; their form is open (Q10). Classification:
  `docs/m7-plan/unit-catalogue-and-progression.md` §4.5.
- **New M7 work item GA, garrisoning** (approved requirement, not yet designed): unit charter §14 stage M7.8,
  building charter §5.16a. It depends on M7.2 (location, orders), M7.4 (battle, fortification term), M7.7 (city
  buildings) and save/load. Air-defence garrisons also wait on `air_defence` realization. The Director confirms the
  placement.


## Dated note — 2026-10-28 (M6.19, D-066 R2; the rows H7-02, H7-03, H7-05, H7-06, H7-10 and the boundary rules were corrected in place, struck text kept)

M6.19 completes this register with the M6.8–M6.12 contracts. Every corrected row keeps its stale words ~~struck~~ beside
the correction, and names one owner per side (what M6 delivered; what M7 builds).
- **H7-02:** the "schema reserved v36" claim was stale — v36 is ADR-049's and AP/AC has no reserved version (D-062 R11,
  D-066 A11); the contract the row asked for now exists ([training-ap-contract.md](training-ap-contract.md)) with dormant,
  validated data. D-F is ruled (option 1).
- **H7-03:** partly realized early by ADR-055 (the uncommitted M7 pass, D-065 R7) — recorded as such; the M6 side is
  unchanged (the capability vocabulary only), the remainder is M7's.
- **H7-05:** the "if the Director scopes it into M6" conditional was stale — D-E is ruled (option 1); the schema, content
  and validator are delivered ([unit-progression-contract.md](unit-progression-contract.md)).
- **H7-06:** the placeholder is delivered (one inert card line; a PlayabilityGate row).
- **H7-10:** the M6 statement is made (no scout class in M6; M7 creates the first MobileAgent); settlers and special people
  are dispositioned in [settler-special-character-disposition.md](settler-special-character-disposition.md).
- **H7-14 (new): GA garrisoning**, owner M7 (stage M7.8, D-065 R11), with its placement for the Director to confirm — it
  had no row id or owner before.
- **H7-07 is left unchanged by this note** (its M6 side lists ghost display and scouts, which D-062 moved to M7; the FOG
  closure track corrects it under D-066 §3).
- Boundary rules: the writer of `MilitaryUnits` is no longer `AgeTransitionSystem` alone (ADR-055's `MilitarySystem`), and
  the v36 reservation is gone; M6 still adds no field, no writer and no state.

## Dated note — 2026-10-28 (D-066 R1, §3; M6 closure track FOG; appended, the text above is unchanged)

- **H7-07 corrected — single owner.** The "M6 must provide" column of H7-07 lists "the LIVE/GHOST/UNKNOWN **display**
  … scouts per D-039 B6 ruling" as M6's. Under D-066 R1 (fog-of-war work that depends on movable units is deferred
  until movable units exist) the **ghost (LIVE/GHOST) rows and display, scouts / reconnaissance units, and sighting
  updates from moving formations are M7 ONLY** — they are no longer split between the two milestones. M6 provides,
  and has built (ADR-044): the stored known extent, UNKNOWN / VISIBLE / REMEMBERED, settlement sighting memory, Explore
  and Prospect, and the read seam (`PolityKnowledge.Of`) M7's movement and sight will read. Owner: M7 (H7-01 movement,
  H7-07 reconnaissance); dependency: order kind 10, per-step positions, Action Points (H7-02). D-039 B6's scout-form
  question moves with it to M7.
- Claim, trade and road routing against the known extent (DW-001 / DW-002 / DW-006) are M7 with the movement / route
  graph (D-066 A19; `deferred-work-register.md` note of 2026-10-28).

## Dated note — 2026-10-28 (M6 exit audit; the text above is unchanged)

- **H7-07** is the SOLE owner of ghost display, scouts and moving sight; H7-01 (movement) is a dependency of H7-07, not an owner. This supersedes the wording "Owner: M7 (H7-01 movement, H7-07 reconnaissance)" in the 2026-10-28 FOG note.
- **H7-09 (M6 side):** the M6 closure touched no CR-017 collision. No change implements Action Points, heroes, or the CR-017 §3/§5/§7 subjects. This is verified by the M6 exit audit's check that no movement/AP/battle code was added versus the base.
- **H7-11:** "Rulings owed: D-E" is stale. D-E is RULED (2026-10-20). M6.11's disposition note is `settler-special-character-disposition.md`.
- **H7-01:** kind 10 is reserved at `Sim.Core/Kernel/OrderLog.cs:164` (was cited as `:157`).
- **Register status as of 2026-10-28:** the M6 side of every row is COMPLETE or explicitly empty. The register is the M7 input. Its header "PROPOSED (planning input, 2026-10-20)" is historical.

## Dated note — 2026-10-28 (D-067: the Director's exit-gate rulings)

The rows above are unchanged. D-067 affects M7's inheritance in five places:
- **Known-extent routing is M7 work by ruling** (D-067 R10). The known-extent restriction of claim, trade and road
  routing, and the founding predicate's reads of unseen land (DW-002), are assigned to M7. This explicitly supersedes
  DW-001's 2026-10-12 M6 assignment. It is a scheduling ruling; it authorizes no implementation now. Owner: the M7
  route graph (H7-01 movement) with the fog rows of D-066 §3.
- **ADR-055 is ratified** (D-067 R15), including the scope beyond D-065 R7's list: general Recruit and Disband orders,
  soldier upkeep, and AI recruitment on. The accepted consequences are M7 inputs:
  - soldiers have no attrition until **M7.4** (H7-04 battle resolution, and its supply attrition);
  - the founding warband draws 20 people from the capital.
- **EC-1, EC-2 and EC-3 are ratified separately** (D-067 R16–R18). They are mostly dormant until their knowledge gates
  are reached. ADR-056 §10's AI research-progression finding stays open for the M7 economy track.
- **D-051 §4 primitive toolmaking** is deferred until after the M6 playtest, with no milestone named (D-067 R19). The
  M7 economy track (EC-1 toolmaking) is a coupled alternative, **not an assignment**.
- **Migration policy is not M7 work.** M8 owns it (D-067 R13).
