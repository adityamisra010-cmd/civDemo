# M6 decision register — what genuinely needs the Director

**Status: PROPOSED (planning input, 2026-10-20, tree `dc83a67`).** Only decisions an agent may not take: each either
touches a ratified or frozen item, re-opens a recorded ruling, or chooses between designs with different compatibility
costs. For each: the question, why it matters, the options, the recommendation, and what stays blocked until it is
decided. The M5 closure rulings R1–R9 are in `../m5-exit-inventory.md` §6 and are not repeated here except where an
M6 workstream waits on them.

| ID | Decision | Blocks |
|---|---|---|
| D-C | Next-turn figure: exact weather vs D-051 §3's expected weather (`../m5-exit-inventory.md` §10) | M5 acceptance of `3c18a71`; D-051 §3's stores half |
| D-D | Government adoption and active-government effects in M6 or M8 | M6.6 scope, M6.7, the vertical slice's "one government adoption or institutional effect" |
| D-E | Per-unit progression STATE in M6, or design/contract only | M6.8–M6.11 scope; schema plan |
| D-F | The repeatable-training / Action-Point contract: M6 writes data and contract only, M7 implements | M6.10; M7 handoff H7-02 |
| D-G | Session export: hand-rolled minimal OOXML writer vs an xlsx library dependency | M6.15 start |
| D-H | Schema version sequencing: v35 fog (reserved), v36 M7 S3 (reserved) — where civics/institution tables land | M6.17; every M6 table |
| D-I | Civics currency and targeting: one pool/one target (ratified) vs two targets vs Civics Points | M6.4 design; M6.5 costs; the slice |
| D-J | Scouts: a class, a flag, or a per-polity capability score (D-039 B6, "M6's spec owns it") | M6.13 discovery rules |
| D-K | Known-extent storage and sight budget: ratify the S2a plan (per-polity bitset over the 16 km lattice, monotone OR, v35; sight = isochrone from controlled settlements and owned formations, no new constant) | M6.13 |
| D-L | E1: the Age style register's driver (Age, realized state or knowledge — D-038 H8 open) and an asset budget / gate cadence | M6.14 |
| D-M | "What is an institution" (CR-010, recommended and never written) — needed before any non-university institution is adopted | M6.6, the slice |
| D-N | D-051 carry-over destination (= closure ruling R2) | M6.24 existence |
| D-O | Spine M5 items' destination (= closure ruling R4) | M6.4/M6.5 content for laws-lite and the authority economy |
| D-P | The vertical slice: approve the proposed slice (M6.18) or choose another | M6.18 |
| D-Q | "Levying Troops" and other verb-granting civics before M7 has an owning system: knowledge-only until M7, or an explicit seam ruling | M6.5 content; H7-03 |

## D-D — Government adoption in M6 or M8

- **Why it matters.** The 2026-10-20 instruction's M6.6 asks for "adoption action, active government state,
  government-specific laws/policies/effects, transition/replacement/persistence rules". The Spine (frozen) places
  "Politics deep (institutions, regime change) … composable modules, not enum governments" at the politics milestone —
  M8 after the 2026-10-03 rebase (`civ-sim-architecture-v3-outline.md:85, 134`; `d011:60`; `milestones.md:25, 59-61`).
  M6's scope names "the Technology and Civics trees" and the capability → realization chain, not government forms
  (`milestones.md:74-83`). D-042 §5 (RATIFIED): government "may influence available decisions, policies, capabilities …
  through state and computed conditions"; "Avoid hard-coding governments as bundles of direct bonuses". arch-FGH (design)
  gives the shape: a REGIME MODULE SET (serialized, latched, per Empire) and a GOVERNMENT LABEL that is "DERIVED …
  never read by any mechanism".
- **Options.**
  1. **M6 = availability; M8 = adoption** (recommended). M6 delivers: macro civics whose completion makes a module set
     eligible; the regime-module data shape and derived label (read-only in M6); UI that shows Available vs Adopted with
     locked-state reasons. No adoption order, no active effects in M6. No freeze conflict.
  2. **M6 = availability + adoption + minimal effects.** Needs a priced override of the Spine allocation (S8 §2:
     "a written ADR stating what breaks, which tests and docs change, and the schedule price", `spine-s8:37`), CR-010
     (D-M) first, and a legitimacy type (arch-FGH §2.4) if any effect touches it. Pulls M8's defining content forward.
  3. **All government work in M8**, M6 civics content only. Loses the Director's "Researched → Adopted" demonstration.
- **Recommendation:** option 1; the slice shows one institutional adoption (an institution, not a government) which is
  M6's by `milestones.md:80-81`.
- **Blocked until decided:** M6.6's deliverables, M6.7's adoption interface, slice item 2/3.

## D-E — Per-unit progression state in M6

- **Why it matters.** `MilitaryUnitRow.Experience` is a preserved 0.0 — "nothing in this build earns it"
  (`UnitCardModel.cs:157-160`); the only earner in any ruling is D-011 §2's `BattleOutcome` (M7). The D-011 §3 formation
  fields promotions would modify (strength, cohesion, fatigue, ammo) do not exist. `MilitaryUnits` has one writer by
  rule (`AgeTransitionSystem.cs:43-45`); widening the row breaks the appended-only premise of the readable set
  (`m7-handoff-register.md` E). Designing effects against absent fields is "a modifier without a carrier" (D-035-C test).
- **Options.**
  1. **Design + content schema + validator + M7 contract; no state** (recommended). Promotion trees are authored and
     validated for shape (prerequisites, exclusivity, repeatability, referenced M7 fields named), inert; the placeholder
     panel says so.
  2. Add a per-unit progression table (appended, new version) with promotions selectable now and effects deferred.
     Costs a schema bump, an ownership re-split ADR, populated tests, golden moves — for state nothing can change until M7.
  3. Defer all unit progression to M7.
- **Recommendation:** option 1.
- **Blocked until decided:** M6.8–M6.11 scope lines; D-H's version plan.

## D-F — The repeatable-training / AP contract

- **Why it matters.** Action Points are M7's (D-052 §4, RATIFIED); CR-017 §3 (Action Capacity vs Law 3) "must be ruled
  before any M7 movement packet" (`d052:49-50`); the S3b plan derives AC max from content, which a trained per-unit max
  contradicts. "Dormant units must not improve accidentally" needs a definition of dormant that only M7's movement state
  can give.
- **Options.** 1. M6 writes the contract only (definition of dormant as a named M7 state; eligibility; cost; increment;
  cap; diminishing returns; rejection cases) as data fields and validation (recommended). 2. M6 adds training state and
  an order — contradicts D-052 §4 without an override.
- **Recommendation:** option 1; the contract is `m7-handoff-register.md` H7-02's deliverable.
- **Blocked until decided:** M6.10.

## D-G — Session export: how the workbook is written

- **Why it matters.** The repository has four package dependencies (MonoGame, ImGui.NET, xunit, FsCheck). An xlsx
  library adds a dependency, a licence and possibly nondeterministic output (timestamps, GUIDs, zip ordering). The
  workbook is simple: text and number cells, eight sheets, no formulas.
- **Options.** 1. **A minimal OOXML writer in Sim.Cli/Sim.Ui tooling** (recommended): `System.IO.Compression` zip with
  fixed entry order and fixed timestamps, hand-written `sheet.xml`, inline strings, `InvariantCulture`; fully
  deterministic; ~300 lines; tested by re-reading with the same code and by a byte-identical golden. 2. ClosedXML /
  Open XML SDK: faster to write, adds the dependency and the determinism audit.
- **Recommendation:** option 1.
- **Blocked until decided:** M6.15's first packet.

## D-H — Schema version sequencing

- **Why it matters.** D-055 reserved v35 for S2a (known extent) and v36 for S3 (M7 Action Points)
  (`d055:1306-1308`). M6 civics/institution tables and any export-related schema (none expected) must take versions
  in commit order; the readable-set logic assumes appended-only tables. If civics lands before fog, the reservation
  is wrong in order.
- **Options.** 1. **Allocate by landing order, re-reserving in the spec**: next landed table takes the next version;
  the spec states the sequence (e.g. v35 fog, v36 civics, v37 M7 S3) and amends D-055's reservation by dated note
  (recommended). 2. Keep reservations fixed and hold civics tables until fog lands.
- **Recommendation:** option 1.
- **Blocked until decided:** M6.17's plan; D-055's note.

## D-I — Civics currency and targeting

- **Why it matters.** The 2026-10-20 instruction: "Civics uses Civics Points. Do not silently reuse Research Points or
  combine the currencies without an explicit, evidence-backed design decision." The record's evidence: D-044 R2
  (Director verbatim, `d044:79-103`): "There is ONE shared Cognitive Load Point pool … Do NOT create: Science Points,
  Culture Points … Do not create a second accumulated research currency"; R12 (`:360-383`): "Civics is Tree 2. It uses
  the SAME Cognitive Load Point pool … Do not create Culture Points … Do not copy Technology into Civics under another
  name"; D-046 G5 (`d046:202-204`): "There is exactly one shared RP pool. Technology and Civics draw from the same pool";
  the ledger's prohibited reintroductions §26 (2) separate currencies per branch, (3) a second currency, (4) a general
  RP reserve, (11) a reusable per-turn exception; ADR-030 §3: a second per-turn quantity needs its own ADR; D-044 Part B
  marks the pool question do-not-ask-again. As built, the 7 civics nodes already cost RP from the one pool, one target
  (`ResearchEngineTests.SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp`).
- **Options.**
  1. **One pool, one target — as ratified and built** (recommended for the slice). "Civics Points" is not a currency;
     civics compete with technology for the same RP, which is the ruled design. The civics layer's novelty is adoption,
     not currency.
  2. **One pool, two targets** (a technology target and a civics target with a player-set split). Supersedes D-044 R9
     (one target) only; needs a dated Director note and a second `SetResearchTarget`-like order kind; keeps conservation
     of invested RP exact.
  3. **Civics Points as a second accumulated currency.** Supersedes D-044 R2/R12, D-046 G5 and ledger §26; needs its
     own ADR for denomination (per-year × dt or a scoped per-turn exception), a non-linear population input, a D-021
     brake for anything that accelerates it, new rows (schema bump, populated test, golden re-pin), and must never be
     the left operand of a gate (arch-O II.2).
- **Recommendation:** option 1 now; option 2 if the Director wants parallel progression visible to the player. Option 3
  only by explicit supersession of the Director's own rulings.
- **Blocked until decided:** M6.4's design; M6.5's cost model; the slice.

## D-J — Scouts (D-039 B6)

- **Why it matters.** D-039 B6 (OPEN): a scout as a class, a flag on a notable, or a per-polity capability score —
  "M6's spec owns it, and it should be answered alongside GOV-2's open 'is a notable a person?' question"
  (`d039:77-82`). D-040 B2: discovery is reconnaissance at a different scale; "Do not invent a second exploration
  system". The S2a plan gives sight to controlled settlements and owned formations with no scout entity and no new
  constant.
- **Options.** 1. **S2a as planned, no scout entity in M6**: sight from settlements and formations; scouts arrive as
  the first MobileAgent in M7 with movement (recommended — D-039 B1's "a scout is a person drawn from a bucket" needs
  recruitment and movement, both M7). 2. A per-polity capability score that widens the sight isochrone (a coefficient in
  a named equation, realizable now). 3. A scout class (needs MobileAgent, M7).
- **Recommendation:** option 1, with option 2 noted as the only M6-realizable "scouting" lever.
- **Blocked until decided:** M6.13's discovery rules and the sight budget.

## D-K — Known-extent storage and sight (ratify the S2a shape)

- **Why it matters.** D-040 B5/E4 (RATIFIED) deliberately did not design storage, granularity or constants; the m5w
  plan proposes `KnownExtentRow(Holder, Word, Bits)` — a per-polity bitset over the 16 km lattice, rows sorted
  (Holder, Word), monotone OR each step, seeded from founding catchments, revolt inherits a copy, annexation takes the
  union, schema v35, `DiscoverySystem` (`m5w-map-workstream-audit-and-plan.md:725-760`).
- **Options.** 1. **Ratify the plan as the M6 spec's S2a packet** (recommended; it is the only designed shape, it adds
  no source of truth for the visible set, and the seam already consumes it). 2. Per-settlement extent (D-040 B5 left
  it open) — more rows, no identified benefit. 3. Coarser granularity than the lattice — conflicts with D-040 B5's
  "one node is 16 × 16 km".
- **Recommendation:** option 1.
- **Blocked until decided:** M6.13.

## D-L — E1: style-register driver and gate cadence

- **Why it matters.** D-038 H8 and "what drives the Age style register (Age, realized state or knowledge)" are unruled
  (`d038:208`; `m5w:938`). Law 4 forbids a visual that implies capability; D-052 §6 says visual Age representation must
  not grant anything. The visual gate is the Director's (D-023) and cannot be automated.
- **Options.** 1. **Style register driven by REALIZED state** (structures present, institutions active) with the Age
  banner as the only Age-keyed element (recommended: a drawing of what exists cannot be a calendar gate). 2. Driven by
  Age (simple; a label drives the look). 3. Driven by knowledge (shows what could be built — misleading).
- **Recommendation:** option 1; gate the slice's art first, then one gate per asset batch.
- **Blocked until decided:** M6.14 conventions.

## D-M — "What is an institution" (CR-010)

- **Why it matters.** arch-N `:196`: "Three ratified placements (M5 by D-035-C, M7 by the Spine, M8 by D-011) and ≥6
  meanings with zero code … CR-010 was recommended and never written." Today exactly one institution type exists
  (universities) with a lifecycle, staffing brake and effects. Civics unlock 28 `inst.*` entities; 27 have no
  realization; "Adopted" for them is unspecified. Any civic adoption in M6 needs the answer.
- **Options.** 1. **Rule it in the M6 spec as "an institution is a founded, staffed, maturing structure of a settlement
  or polity, modelled by the university lifecycle (`InstitutionRow`, Locked→Mature), realized by construction `founds`
  or by a civic's adoption act, with effects only through coefficients inside named equations"** (recommended — it is
  the shipped meaning). 2. Write CR-010 as a separate Director record first. 3. Leave undefined and keep civics
  knowledge-only in M6.
- **Recommendation:** option 1, recorded as a D-record cited by the spec.
- **Blocked until decided:** M6.6, the slice's institutional adoption.

## D-N, D-O — closure rulings R2 and R4

See `../m5-exit-inventory.md` §6 and §7. M6.24 exists only if R2 names M6; M6.4/M6.5's laws-lite and authority
content exist only if R4 places them in M6's civics.

## D-P — The proposed vertical slice (M6.18)

Chosen for existing implementation and architectural fit; every element reuses a shipped mechanism.

1. **One civic micro node:** `law_code` (exists: key 1001, A3, 1240 RP, prerequisite a script) — reviewed, not
   rewritten; its effect text names the mechanism below.
2. **One step toward a macro option:** the regime-module data shape with one module, "codified law", present when the
   law-code institution is Active, and the derived label it changes (e.g. "chiefdom" → "law-giving chiefdom"); no
   adoption order (D-D option 1).
3. **One institutional effect:** a second institution type beyond universities — the law court (`inst.law_code`) —
   established through the existing lifecycle (construction `founds` like `building.university`; Locked→Available on
   civic completion; Active/Mature by the same maturity closed forms) and realized through an EXISTING equation:
   `Unrest.ServiceOffset` already reads institution maturity × reach; the court's maturity enters it (a coefficient
   inside a named equation, not a bonus). D-021 pairing: the staffing brake already applies.
4. **One military progression upgrade:** a promotion-tree content entry validated by the M6.9 schema and shown inert
   in the placeholder with "earned in M7" (D-E option 1) — no state.
5. **One non-military progression example:** none exists to progress; the slice records this honestly (settler =
   order; heroes = CR-017 §7 OPEN) rather than inventing one.
6. **Simulation-side validation:** the construction project for the court is refused when the civic is not completed
   (`ProjectAvailability.NotKnowledgeEligible` path); an injected order is rejected (the `T15` pattern).
7. **UI:** Progression › Civics tab and the INSTITUTIONS lens show Unresearched → Available → Active for the court,
   with the locked-state reason; the action surface lists the project only when legal (one predicate, two callers).
8. **Persistence and replay:** the new institution type uses the existing `InstitutionRow.Type` (no schema change if
   the type enum is content-driven — to be measured in M6.2); if a new table is needed, D-H applies; populated test;
   replay twin; golden attribution.

**Why this slice:** it exercises completion → eligibility → establishment → maturation → effect through four shipped
systems (research, construction, institutions, unrest) and one existing civics node, touches no frozen item, needs
only D-M and D-I(1), and is playable by the Director in one session. **Approve, amend or replace.**

## D-Q — Verb-granting civics before M7

- **Why it matters.** "Levying Troops" would, by completion, make a levy verb legal. D-040 B3 / law 4: a node that
  opens a verb is a calendar gate wearing a tree unless an owning system realizes it on computed state (D-047 r7/8
  resolved this for class unlocks). Recruitment has no owning system before M7; `AvailableActionsQuery` lists only
  available actions.
- **Options.** 1. **Such civics are knowledge-only in M6** (like the 27 `inst.*` entities): authored, validated,
  shown in the tree, consumed by M7's recruitment when it exists (recommended; it is exactly "M7 consumes M6's capability
  state"). 2. An explicit seam ruling allowing a levy order in M6 with no resolution — refused by the action-surface rule.
- **Recommendation:** option 1.
- **Blocked until decided:** M6.5's content rules for military civics.


## Dated note — 2026-10-20: D-I, D-D, D-E and D-F RULED by the Director

- **D-I — RULED:** one shared Research Points pool and one target. Civics is a distinct progression graph on
  `ResearchTree.Civics` with its own node costs, prerequisites and adoption preconditions. No separate Civics Points
  currency. (Consistent with D-044 R2/R12 and D-046 G5; nothing superseded.)
- **D-D — RULED:** M6 defines civic prerequisites and government eligibility; government adoption and active political
  effects belong to M8. Available and Adopted are distinguished in the model and the presentation without implementing
  political transitions in M6.
- **D-E — RULED, option 1:** M6 specifies unit-progression semantics, content schema, validation and the M7 contract. No
  serialized per-unit progression state in M6.
- **D-F — RULED, option 1:** M6 defines the training/AP contract only. No executable training state, training order or
  AP-resolution change in M6. The training panel remains a labelled, non-functional placeholder.
- **D-N / D-O:** ruled with the closure (R2, R4): D-051 §2 and §4 → M6 (M6.13 coordination; M6.22 lane); §1(a) → M9;
  §6-remainder → M10; laws-lite and the authority/bandwidth economy → the M6 civic specification subject to architecture
  review; budget/treasury unassigned pending a definition (CR-008 closed with no treasury requirement).
- **D-C — RULED:** expected weather (option 2); implemented UI-only with tests — see `../m5-exit-inventory.md` §13.
Record of the rulings: `docs/m5-exit-inventory.md` §13.


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- **D-I — SUPERSESSION PROPOSED (not yet ruled):** the Director's 2026-10-21 M6.1 instruction directs "a separate
  Civics Points pool, distinct from Research Points" and "a genuinely distinct tree". The conflict with the 2026-10-20
  D-I ruling and with D-044 R2/R12, D-046 G5 and ledger §26 is recorded in `docs/adr/cr-023-civics-points-vs-single-pool-rulings.md`;
  the proposed supersession record with sub-decisions D-061.1–D-061.8 is `docs/d061-civics-points-supersession.md`.
  The D-I ruling text above is NOT edited. Status: **AWAITING THE DIRECTOR**.
- New decisions surfaced by T6.1 and the spec (`docs/m6-spec.md` §7): **D-R** overflow carry/lose; **D-S** RP/CP inputs
  beyond population; **D-T** what the AI assumes of unseen terrain. Recommendations are in the spec; none is ruled.
- D-N/D-O: ruled 2026-10-20 (closure R2/R4) — unchanged. D-D, D-E, D-F: ruled — unchanged. D-G, D-H, D-J, D-K, D-L,
  D-M, D-P, D-Q: still open; consolidated with recommendations in the spec §7.

## Dated note — 2026-10-22 (D-062; appended, the text above is unchanged)

D-062 (`docs/d062-m6-director-final-rulings.md`) records the Director's final M6 rulings of 2026-10-22: D-I SUPERSEDED (separate, bankable Civics Points); the research stage loses `legal_code_roman`; deposits partly concealed and revealed by prospecting; minimal Explore/Prospect in M6 with separate Construction and Actions menus; movement/AP stays M7 (D-F confirmed); adoption stays M8 (D-D confirmed); nuclear knowledge only. Open letters resolved by it: D-H (schema: v35 is the M6 slice), D-K/D-J/D-T in their minimal form (ADR-044). D-R (overflow) unchanged for RP; CP never overflows (banked). Record: `docs/d062-m6-director-final-rulings.md`.

## Dated note — 2026-10-28 (D-066; appended, the text above is unchanged)

D-L, D-M, D-P and D-Q were settled by ADOPTION under the Director's 2026-10-28 decision rules (D-066 §2 A3, A1, A2, A5), as were D-G (A14–A17) and the "optional" wording (A6). Each is reversible and listed for the Director's confirmation at the exit gate. They are not recorded as Director rulings.

## Dated note — 2026-10-28 (D-067 exit-gate rulings; appended, the text above is unchanged)

The Director ruled at the M6 exit gate ([D-067](../d067-m6-exit-gate-director-rulings.md)). The D-066 note above records
D-L, D-M, D-P and D-Q as settled by ADOPTION, not by ruling; that status is now changed as follows.

- **D-M — CONFIRMED** (D-067 R1; D-066 A1): an institution follows the university lifecycle, realized by construction
  `founds`.
- **D-P — CONFIRMED** (D-067 R1; D-066 A2): the slice as written.
- **D-Q — CONFIRMED** (D-067 R1; D-066 A5): verb-granting civics are knowledge-only in M6.
- **D-L — CONFIRMED with a correction** (D-067 R2; D-066 A3): the E1 style register is driven by realized state, but
  "the Age banner is the only Age-keyed element" is too broad. The map's era theme (`MapInk.For(EraTheme)`) is also
  Age-keyed, as pre-existing presentation that implies no capability. The ruled meaning: no E1 element implies a
  capability the polity has not realized. Related: the D-038 H8 parts cap of eight (D-066 A4) is **provisional until the
  D-038 Part G visual gate** (D-067 R3).
- **D-G — CONFIRMED** (D-067 R6 for A14, with the correction that the writer is the hand-written STORED ZIP writer
  `StoredZip`, not `ZipArchive`; cross-OS byte identity is PREDICTED; D-067 R1 for A15–A17).
- **D-T** (what the AI assumes of unseen terrain): the A18 closure is **CONFIRMED** (D-067 R1).
- **D-N — RE-RULED for D-051 §2 and §4 (D-067 R19).** The 2026-10-20 destination "§2 and §4 → M6 (M6.13 coordination;
  M6.22 lane)" (closure ruling R2) is superseded for these items. D-051 §2's remainder (§2(a) floor, §2(b) regional
  richness classes) and §4 (primitive toolmaking, with its unresolved CR-020 §A mechanism choice) are deferred until
  after the Director's M6 playtest, with **no milestone named**. M10 is a content-mapped future possibility, not a
  commitment; the M7 economy track (ADR-056 EC-1) is a coupled alternative, not an assignment. Concealment, §2's first
  availability class, stays delivered (D-062 R6/R7, ADR-044). §1(a) → M9 and §6-remainder → M10 are unchanged. D-051 §2
  and §4 stay RATIFIED. Carried open: D-062 R6 ("randomly distributed") versus D-051 §2(b) ("geographically correlated").
- **Items without a lettered entry here, ruled by D-067:** the diffusion pathway (D-066 A7 → R7: trade contact only),
  the research-modifier formula (A8 → R8: ADR-033 D6 retained provisionally, pending calibration), the deferred-entity
  tiers (A9 → R9: only the law court built; the rest classified), and claim/trade/road routing on the known extent (A19 →
  R10: M7, superseding DW-001's M6 assignment; D-K's stored-extent shape is unaffected).
