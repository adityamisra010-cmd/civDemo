# M7.2 decision docket, M7.3 contract plan and dependency plan (under D-075)

**Status: ADMINISTRATIVE PLAN under D-075.** No packet is authorized by it.
- It records the consequences of the Director's decisions of 2026-11-03. It does not ask for them to be approved again.
- It authorizes no code, data, schema, test, golden, build, commit, push or merge.
- M7.1–M7.20, B7-24 and B7-25 stay unauthorized until the Director authorizes each one (REV:493; D-075 O-22).
- It edits no earlier record. It is a new file. Every supersession it relies on is the one D-075 §2 records.

**Governing records:**
- **D-075**: `docs/d075-m7-unified-warfare-direction.md`, the record of the decisions. Cited **D-075:n**.
- **ADR-060**: `docs/adr/adr-060-unified-warfare-simulation-architecture.md`, the architecture record. Cited **ADR-060:n**.
- Both files are LOCAL and untracked at the time of writing (`git status`: `?? docs/adr/adr-060-…` and `?? docs/d075-…`; also `?? docs/m7-spec.md` and the dated-note edits of A-3). They are not on REMOTE or MAIN. MAIN is `7d5defc` (tag `m6-exit`). HEAD is `7d5defc`.

**Inputs and citation forms:**
- **REV:n** is the Director's revision, "DIRECTOR'S REVISION: UNIFIED WARFARE ARCHITECTURE AND M7.1-M7.20", §A–J, lines 374–567 of the session file `…/scratchpad/m7-1/m7-spec-revised.md`.
  - The Director confirmed it as their 2026-11-03 decision (D-075:7–8).
  - Lines 1–373 of that file are the earlier agent draft and decide nothing (D-075:9).
- **v2:n** is the verified governance reconciliation v2, `…/scratchpad/m7-1/m7.2-governance-reconciliation-v2.md`, including its verifier's addendum (v2:545–587).
  - Its findings are used here, reclassified under today's authority.
  - Where v2 says "requires amendment or ratification", the item is now either SUPERSEDED by D-075, when today's decision resolves it, or OPEN, when it does not.
- Repository citations are `path:line` at `7d5defc`. Each was opened when this plan was written.

**The three categories, used throughout:**
- **DECIDED**: decided by the Director on 2026-11-03 (D-075 §1, R1–R54).
- **ADMIN**: documentation required to record a decision. It is drafted as the consequence of that decision.
- **OPEN**: a technical question the decisions leave undecided. Open items use D-075 §5's numbers (O-1 … O-37, plus O-7a; ADR-060 §4 carries the same ids and content). Plan-level open items that D-075 §5 does not carry are numbered **P-n**.
- In §4, a dependency change is labelled **consequence of D-075** or **technical finding (proposed)**. A proposed finding binds nothing until the Director rules.

---

## §1 Numbering

### 1.1 The schemes (DECIDED and ADMIN)

- **REV M7.1–M7.20 are the M7 milestone packets.** This is DECIDED by R48 (REV:495–516; D-075:147, :359).
- **B7-nn are sub-packet ids mapped onto them.** The Director's instruction 9 uses B7 ids (R53, D-075:157). The mapping below is ADMIN (D-075:360). Formal adoption of the B7 scheme beyond that use is OPEN (O-24).
- **B7-24 and B7-25 are M7 packets** by R54 (D-075:158). REV gives them no M7.n number. Whether they get one is part of O-24.
- **Old labels are superseded aliases.** They are historical and are never reused for new work (D-075:362).

### 1.2 M7.n ↔ B7-nn

| REV packet | REV line | B7 | Status |
|---|---|---|---|
| M7.1 Foundations audit and architecture reconciliation | :497 | B7-01 | DECIDED (R48). The audit draft exists as a proposal; it is not accepted (v2:285) |
| M7.2 Governance and decision rulings | :498 | B7-02 | DECIDED (R48); see §2 |
| M7.3 Unified simulation contracts | :499 | B7-03 | DECIDED (R48); see §3 |
| M7.4 Battlefield renderer foundation | :500 | B7-05 | DECIDED (R48) |
| M7.5 Terrain, structures and scenario editor | :501 | B7-06 | DECIDED (R48) |
| M7.6 Individual entities and formation model | :502 | B7-07 | DECIDED (R48) |
| M7.7 Movement and spatial interaction | :503 | B7-09 | DECIDED (R48) |
| M7.8 Detection, visibility and concealment | :504 | B7-10 | DECIDED (R48) |
| M7.9 Land warfare foundations | :505 | B7-11 | DECIDED (R48) |
| M7.10 Individual combat and Animator AI | :506 | B7-12 | DECIDED (R48) |
| M7.11 Tactical pulse and WEGO resolution | :507 | B7-15 | DECIDED (R48) |
| M7.12 Enemy AI and battle inspection | :508 | B7-16 | DECIDED (R48) |
| M7.13 Siege operations | :509 | B7-18 | DECIDED (R48) |
| M7.14 Logistics and operational warfare | :510 | B7-17 | DECIDED (R48) |
| M7.15 Naval warfare module | :511 | B7-19 | DECIDED (R48); resolver mode OPEN (O-1) |
| M7.16 Air warfare module | :512 | B7-20 | DECIDED (R48) |
| M7.17 Combined-arms interaction | :513 | B7-21 | DECIDED (R48) |
| M7.18 War Sandbox and test laboratory | :514 | B7-22 | DECIDED (R48) |
| M7.19 Campaign integration and resolver parity | :515 | B7-23 | DECIDED (R48). v2's move of the parity battery to B7-15 is OPEN (O-23) |
| M7.20 Whole-M7 integration, optimization and exit | :516 | B7-29 | DECIDED (R48) |
| — (military buildings and calibration) | none | B7-24 | DECIDED as an M7 packet (R54). No M7.n number (O-24) |
| — (Garrisoning, GA) | none | B7-25 | DECIDED as an M7 packet (R54). Design OPEN (O-36) |

**v2 B7 ids that have no REV packet.** These are proposals, not decided. Each is OPEN under O-23, and where noted under O-34 or O-35.

| B7 | v2 content (v2 line) | Status |
|---|---|---|
| B7-00 | "Record the direction" (v2:284) | **Discharged (ADMIN).** D-075 and ADR-060 record the direction. Not a packet |
| B7-04 | Supply, attrition and upkeep-consequence slice; end of soldier immortality (v2:288) | Proposal (O-23). Owner of "immortal until M7.4" is O-35 |
| B7-08 | Catalogue and combat-stat data contract (v2:292) | Proposal (O-23, O-34) |
| B7-13 | War state, minimum hostility predicate (v2:297) | Proposal (O-23) |
| B7-14 | AutoResolver, contact and Ledger casualties (v2:298) | Proposal (O-23). REV places the resolver contract in M7.19 (REV:515) |
| B7-26, B7-27, B7-28 | EC-1, EC-2, EC-3 as future packets (v2:310–312) | **Stale (technical finding, §4.3 TF-5).** EC-1–EC-3 are ratified (`docs/d067-m6-exit-gate-director-rulings.md:99–101`, R16–R18, "**RATIFIED**") and delivered in commit `f7a59b8` ("economy packets EC-1..3"), an ancestor of `7d5defc` |

### 1.3 Alias table: every old label maps to one entry

| Old label | Source (opened at `7d5defc`) | Now |
|---|---|---|
| Charter M7.0 "Rulings first" | `docs/m7-plan/unit-catalogue-and-progression.md:1633` | M7.2 (B7-02) |
| Charter M7.1 "Ratify the family catalogue and schema" | charter:1634 | B7-08 (proposed; O-23, O-34) |
| Charter M7.2 "Movement + Action Capacity + ZOC" | charter:1635 | M7.7 (B7-09). The ZOC owner is O-34 |
| Charter M7.3 "Recruitment + personnel + upkeep" | charter:1636 | **dropped**: delivered under ADR-055 (ratified, D-067 R15, `d067:80`) |
| Charter M7.4 "AutoResolver + TacticalResolver v1 … siege … starvation path" | charter:1637 | B7-14 (proposed; O-23). If B7-14 is not adopted, its content falls to M7.11, M7.13 and M7.19 (D-075:370) |
| Charter M7.5 "Agents: reconnaissance … generals … Category B allocations" | charter:1638 | M7.8 (B7-10) for reconnaissance. Generals and category-B allocations have no owner (O-34; v2 verifier, v2:560) |
| Charter M7.6 "Water edges, ports, naval transport" | charter:1639 | M7.15 (B7-19). "(auto-resolve)" is O-1 |
| Charter M7.7 "Military buildings" | charter:1640 | B7-24 (R54) |
| Charter M7.8 GA "Garrisoning" | charter:1641 | B7-25 (R54) |
| EC-1, EC-2, EC-3 | charter:1642 | **dropped as plan stages**: delivered under ADR-056 (D-067 R16–R18; commit `f7a59b8`). D-075's alias row (D-075:375) was corrected on 2026-11-03 to the same effect (TF-5, A-7) |
| ADR-055 / D-067 "M7.4" ("Soldiers are immortal until M7.4") | `docs/adr/adr-055-m7-recruitment-personnel-reequip.md:261`; `docs/d067-m6-exit-gate-director-rulings.md:88` | The packet that ends soldier immortality. **OPEN (O-35)**: M7.14, M7.19 or v2's B7-04 |
| `current-state.md` "M7.8 proposed" (GA) | `docs/current-state.md:99` | B7-25 (R54) |
| Draft T7.1 Foundations audit + spec | REV:208 | M7.1 (B7-01) |
| Draft T7.2 Rulings and ADRs | REV:209 | M7.2 (B7-02) |
| Draft T7.3 Catalogue and combat-stat contract | REV:210 | B7-08 (proposed) |
| Draft T7.4 War state | REV:211 | B7-13 (proposed) |
| Draft T7.5 Movement | REV:212 | M7.7 (B7-09) |
| Draft T7.6 Contact + AutoResolver + casualties | REV:213 | B7-14 (proposed) |
| Draft T7.7 Supply attrition | REV:214 | B7-04 (proposed) |
| Draft T7.8 Fortification, siege, starvation, capture | REV:215 | M7.13 (B7-18) |
| Draft T7.9 Reconnaissance + ghost display | REV:216 | M7.8 (B7-10) |
| Draft T7.10 Known-extent routing | REV:217 | **superseded as a packet; unplaced.** The work stays assigned to M7 by D-067 R10 (`d067:33`, "deferred to M7"), which today's decisions do not touch. Its owner is **OPEN (P-1)** |
| Draft T7.11 TacticalResolver + parity suite | REV:218 | M7.11 (B7-15) |
| Draft T7.12 AI military behaviour | REV:219 | M7.12 (B7-16) |
| Draft T7.13 Military buildings + calibration | REV:220 | B7-24 |
| Draft T7.14 Water edges, ports, naval auto-resolve | REV:221 | M7.15 (B7-19); "auto-resolve" is O-1 |
| Draft T7.15 Garrisoning | REV:222 | B7-25 |
| Draft T7.16 Integration + Director playtest | REV:223 | M7.20 (B7-29) |

The draft's own plan (REV:202–232), including its critical path "T7.1 → T7.2 → T7.5 → T7.6 → T7.8 / T7.11 → T7.16" (REV:225), is superseded by REV §G and the Director's critical path (D-075 S59, D-075:254).

---

## §2 M7.2 docket

M7.2's deliverable is DECIDED (REV:498): "Resolve Action Capacity, off-road movement, operational/tactical pulse definitions, internal step, spatial model, individual entity persistence, naval resolver, siege boundary and required D-record/ADR amendments."
- Two items in that list are discharged:
  - "siege boundary", by R40/R41 (REV:438, :467);
  - "required D-record/ADR amendments", by D-075 and ADR-060 (D-075:461).
- What remains for M7.2 is ADMIN work in §2(a) and the Director's rulings in §2(b).

### 2(a) ADMINISTRATIVE records (consequences of D-075; not requests for approval)

| # | Record | Required by | State at writing |
|---|---|---|---|
| A-1 | **D-075**: the ruling record of the decisions, supersessions S1–S61, and the open list | Clarification items 1, 2, 4 | **Written, LOCAL and untracked.** Not committed |
| A-2 | **ADR-060**: the architecture record and the "director ADR" of `spine-s8-governance-freeze.md:13` ("change requires a Contradiction Report + director ADR"), with the Contradiction Report fields (ADR-060 §3.1) and M7's exit-definition change (S8:17; R51) | D-075 §4.2 (D-075:336–344) | **Written, LOCAL and untracked.** Not committed. See A-6 for the corrections it needs before filing |
| A-3 | **Dated notes** appended to the end of each affected file, titled "## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)". The list is the 17 files of D-075 §4.1 (D-075:312–328), with S-rows and alias-only notes | Clarification item 3; D-075 §4.1 | **Mostly appended, LOCAL and uncommitted; not written by this plan.** At the start of this work no file carried the heading. When this plan was finished, another writer in the same working tree had appended it: |
| | | | Present (heading count 1) in 14 tracked files: `cr-017`, the Spine outline, D-011, D-038, D-039, D-052, D-065, the recovered-decisions register, the E1 checklist, the M7 handoff register, the aviation and unit charters, `milestones.md`, the style bible. `git diff --numstat` shows 0 deleted lines in each, so the changes are append-only. |
| | | | Present in untracked `docs/m7-spec.md`. |
| | | | **Completed 2026-11-03 (corrections pass):** the alias-only notes for `docs/adr/adr-055-m7-recruitment-personnel-reequip.md` and `docs/d067-m6-exit-gate-director-rulings.md` are appended. Correction sections are also appended after the notes on D-011 (S4 withdrawn, O-37) and `milestones.md` (S31 withdrawn). |
| | | | The content of each note was not reviewed against D-075 §4.1 by this plan. That review is ADMIN and still pending. `docs/current-state.md` (a router, not on the §4.1 list) was also changed: 8 lines added and 1 changed |
| A-4 | **This plan**: numbering, docket, contracts, dependencies, critical path, lanes | Clarification item 8 | Written, LOCAL and untracked |
| A-5 | **Alias freeze.** Old labels (charter M7.0–M7.8, EC-1..3, T7.n, "M7.4" in ADR-055/D-067, "M7.8" in `current-state.md`) are historical. Recorded in §1.3 and D-075 §4.5. The per-file alias notes are part of A-3 | D-075:362 | Recorded here and in D-075 |
| A-6 | **Consistency corrections to ADR-060 before it is filed.** It is a new, unfiled draft, so this is a correction of a draft, not an edit of a frozen record. Found while writing this plan (line numbers below are those of the ADR-060 draft before correction): | Clarification item 3 (accuracy of the record) | **Applied 2026-11-03** ("Corrections applied — 2026-11-03", at the end of D-075) |
| | (i) ADR-060 §4's open-item numbers diverge from D-075 §5 from O-26 on. ADR-060:313–315 has O-26 "Camera cinema", O-27 "air units in sandbox", O-28 "GA's design; sniper form". D-075 has O-27 "Camera cinema", O-31 "air units", O-36 "GA design", O-32 "sniper form", and O-26 "campaign map". ADR-060 §4 lacks D-075's O-26, O-28–O-30 and O-33–O-35. | | **This plan uses D-075 §5's numbers.** |
| | (ii) ADR-060:289 has an O-7a (numeric representation of battle positions) that D-075 §5 lacks. | | Applied: O-7a is now in D-075 §5 and ADR-060 §4 with the same content; §4 of ADR-060 is a copy of D-075 §5. |
| | (iii) ADR-060:217 writes the upstream segment as "B7-00 → B7-02 → …". D-075 wrote "B7-01 → B7-02 → …". | | Applied: all records now quote v2:325 exactly, "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11", and note that B7-00 is discharged (§5). |
| | (iv) ADR-060:308 cites D-039 D5 as `d039-…:129`. The words "This runs on the FOOD system, not the battle system" are at `docs/d039-command-fog-and-siege.md:128` (the D5 entry runs :127–129). | | Applied: ADR-060 §4 now cites `d039:128`, as D-075:515 does. |
| A-7 | **Correction of D-075's EC-1..3 alias row** (D-075:375), as TF-5 records | Accuracy | **Applied 2026-11-03**: the row now reads "none: done", ratified (`d067:99–101`) and delivered (`f7a59b8`) |
| A-8 | **S8 §4.1 compliance.** The M7 spec text carries the four items (`spine-s8-governance-freeze.md:72–73`: "Every milestone spec from M4 onward carries all four. Item 1 is a task packet … items 2–4 are spec content"). Item 1 is M7.1 (R52). Items 2–4 (dimensional declaration, corridor independence, coupling map) belong in the ratified M7 spec text | Standing rule (`CLAUDE.md:29`; v2 D-21; D-075:458) | Compliance check, not a decision. The spec text that carries them does not yet exist |
| A-9 | **D-069 R1 drafting rule.** Packet text names genres, not another game (`docs/d069-originality-and-third-party-game-references.md:18`). "Total War-style" stays in the Director's decision text as a descriptor (D-075:295) | Standing rule (v2 D-20) | Drafting rule; no ruling needed |
| A-10 | **`CLAUDE.md` milestone line.** Unchanged. `CLAUDE.md:10` still reads "M7 Battle Layer — spec in preparation, NOT ratified; no active packets until its spec is ratified (S8 §4)" | D-075 §4.4 | No action |
| A-11 | **Follow-up for a future authorized packet.** Rescope the C1 comments in `Sim.Ui.Tests/ObjectLayerTests.cs` and `Sim.Ui.Tests/UnitTokenDetailTests.cs`. Both tests draw only campaign-map tokens | D-075 §4.3 | Not authorized now |
| A-12 | **Records that follow each future ruling.** Each §2(b) answer produces its record. These are drafted as consequences when the Director rules, not now. | D-075 §5 | Listed only. Examples, all conditional on the option the Director chooses: |
| | O-8 and O-9 → an ADR closing CR-017 §3/§5 | | |
| | O-4 → D-013 closed, or amended if the budget changes (REV:533) | | |
| | O-7 → D-012 closed | | |
| | O-13(b) → an ADR-009 amendment | | |
| | O-14(c) → a kernel ADR (`OrderLog.cs:5`) | | |
| | O-17(b) → CR-013 reopened and ADR-022 amended | | |
| | O-1(b) with controllable transport → an ADR-031 amendment (`d065:56`) | | |
| | O-29(b) or O-30(b) → a dated supersession note on ADR-019 §1.4 or §1.2 | | |
| | O-21(b) → a dated note on D-039 D5 | | |
| | O-10(b) → an ADR-055 amendment | | |

**No further interpretive record is required for the decided items.** D-075 and ADR-060 record all of R1–R54. In particular, "multiple specialized battle engines" read as "domain modules on one kernel" is recorded from REV's own text (D-075:61; ADR-060:90–92). It is not a new interpretation.

### 2(b) OPEN Director decisions

Nothing here is decided. Every alternative leaves today's decisions intact, and none is a reason to revisit them. The option order is not a recommendation. Alternatives and consequences follow D-075 §5 (D-075:492–531) and ADR-060 §4, which draw on v2 §E (v2:431–454).

**Group 1: named in M7.2's own deliverable (REV:498).** M7.2 cannot close without these.

| # | Question (the words that leave it open) | Alternatives → consequences | Blocks |
|---|---|---|---|
| O-8 | **Action Capacity against Law 3** (CR-017 §3). REV:536 "Resolve before movement implementation"; `d052:48–49` "must be ruled before any M7 movement packet". **Ratified baseline under every alternative:** the Director's ledger Part I §17.2, "Every MobileAgent has Action Capacity." (`docs/design/director-architecture-and-decision-ledger.md:863`), and §19, "involved military agents receive refreshed Action Capacity" (`:949`) | (a) Option A, per sim-year × dt → a dt-0.5 turn gets 1/20 of a dt-10 turn. (b) Option B, an agency budget per decision interval → one exemption ADR. (c) Option C, override Law 3 → the largest blast radius | M7.7, M7.14 |
| O-9 | **Off-road movement against the D-009/D-010 graph, including sea** (CR-017 §5). REV:536. **Ratified baseline under every alternative:** the ledger's Part I §18 (`docs/design/director-architecture-and-decision-ledger.md:910–937`): "Movement uses continuous x/y positions." (:912), "**Off-road movement is allowed.**" (:931), "Ships may move on open water/free positions." (:935) | (a) Option A, the graph is the fast route and supply object, and off-road samples terrain → one ADR. (b) Option B, off-road as the lowest network tier → the Scale Charter edge budget is re-measured. (c) Option C, amend D-009 | M7.7, M7.15 |
| O-2 | **Operational war pulse duration.** REV:463 "subject to a dimensional audit and formal ruling" | (a) An era-varying fixed duration, set after the audit. (b) A single fixed duration. Either way it integrates with dt (Law 3) | M7.14, M7.13 |
| O-3 | **The MECHANISM by which War Pulses crossing campaign-turn boundaries is reconciled with the kernel contract and the sub-step rule (CR-017 §6 with CR-006 §1).** Crossing itself is not open: the Director's ledger Part I §19 (RATIFIED) rules "War Pulses can cross strategic turn boundaries" (`docs/design/director-architecture-and-decision-ledger.md:943`) and "players retain full operational control" (`:950`); it stays binding (D-075 §3). The ledger's annex R4.10 records the collision as OPEN (`:1543–1544`) | (a) CR-017 §6.4 Option B, rule together with CR-006 §1 (`docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:528–529`), choosing CR-006 §1.4's A sub-stepping, B date-stamped presentation or C interrupt as a turn boundary (`docs/adr/cr-006-continuous-time-and-campaign-epoch.md:66–91`). (b) CR-017 §6.4 Option C, Director override of kernel §3.2/§3.4 adding a sub-turn coordinate (`cr-017:531–532`) → every golden re-pins (`cr-017:540`). (c) **Would supersede ledger §19 (a ratified Director ruling):** no crossing, pulses inside one Step, CR-017 §6.4 Option A (`cr-017:520–522`; blast radius `:538`) | M7.14, M7.15, M7.16 |
| O-29 | **Whether the operational pulse is a world-global clock.** ADR-019 §1.4: "it is not a second global clock" (`adr-019-architecture-constitution-addendum.md:81`) | (a) Local to wars and their participants → no conflict. (b) Global → ADR-019 §1.4 superseded in part | M7.14 |
| O-4 | **Tactical pulse length and count.** D-011 §1 "Battles run 6–12 pulses max" (`d011:10`); D-013 "6–12 pulses ≈ one day" (`d011:89`). REV:533 "Amend D-013 only if tactical pulse budget/duration changes" | (a) Keep 6–12 command pulses, each with internal steps → no change to D-011 §1 or D-013. (b) A different count or length → D-013 amended and the parity battery reshaped | M7.11 |
| O-5 | **Internal step definition.** REV:498 "Resolve … internal step" | (a) Fixed integer ticks. (b) An event-driven queue in integer time. (c) A hybrid. Each needs a composite key with an integer tie-break and a tie-dense test (`CLAUDE.md:48`) | M7.3, M7.11 |
| O-7 | **Spatial model (D-012).** REV:535 "Audit whether the proposed grid supports movement, LOS and projectiles" | (a) A coarse grid (`d011:88`) → cheap pathing; weak LOS and projectiles. (b) Continuous coordinates → best fit for LOS and projectiles; needs a new spatial index. (c) A hybrid → two structures to keep consistent | M7.3, M7.7, M7.8 |
| O-7a | **Numeric representation of battle positions and kinematics** (D-075 §5 and ADR-060 §4). Law 5 bans `float` (`CLAUDE.md:20`) | (a) `double` under the existing determinism regime. (b) Fixed-point integers. Interacts with O-17 | M7.3 |
| O-10 | **Entity persistence and identity; wounded and prisoners (`d011:27`).** REV:534 "Rule persistence/identity semantics" | (a) Battle-transient entities that collapse to formation counts → no conserved-table change. (b) Persistent veterans in a conserved table → schema, migration and Ledger work, plus an ADR-055 amendment. (c) A person registry → touches D-010 | M7.6, M7.19 |
| O-1 | **Naval resolution, and transport under D-065 R13.** REV:532 "Director must decide whether naval battles remain auto-only or gain TacticalResolver support" | (a) Auto-only → keeps `d011:45`'s auto-resolve; the naval module still shares the kernel contracts (I.1). (b) TacticalResolver support → naval tactical battles; an ADR-031 amendment and a save migration if transport vessels become controllable (`d065:56`) | M7.15, M7.17 |

**Group 2: block M7.3 or M7.6 (ADR-060 §5, ADR-060:377–382).**

| # | Question | Alternatives → consequences | Blocks |
|---|---|---|---|
| O-14 | **Where REV's "kernel" (R5) lives in code.** REV:408 and :412 do not say | (a) A separate battle-framework project → frozen `Sim.Core/Kernel` untouched; the project must be added to the banned-construct scan (`scripts/check-banned-constructs.sh:22` today lists `Sim.Core Sim.Data Sim.Cli`). (b) A new area inside `Sim.Core`, outside `Kernel/`. (c) Extend `Sim.Core/Kernel` → kernel sign-off; `OrderRecord` "shape … is fixed" (`Sim.Core/Kernel/OrderLog.cs:5`); every order-log consumer re-checked. This also covers the pulse scheduler against the fixed `SystemId[]` (`docs/m0-kernel-spec.md:68`) and formation-membership ownership (`:54`) | M7.3, M7.9, M7.11 |
| O-6 | **Event priority, tie-break, collision, interruption and RNG-consumption rules.** REV:469 "Define …" | Technical work for M7.3. v2's five-field key is one candidate only (v2 verifier E5, v2:573). A ruling is needed only if the Director wants to fix the shape | M7.3, M7.11 |
| O-17 | **Cross-platform determinism scope.** REV:516 "Windows/CI" is not defined | (a) Keep ADR-022's Linux x64 reference (`adr-022…:37`) → Windows is built and smoke-tested, not hash-equal. (b) Hash equality across platforms → CR-013 reopened; simulation math (transcendentals) reworked | M7.3, M7.20 |
| O-11 | **Caps and counted objects.** REV:402 "exact counted object to be defined"; REV:404 "proposed limits" | Covers the final cap values, the counted object for vehicles and mortars, and identities 306–308 (60 personnel) against the 50 cap. (a) V1: vehicles are battle-transient → no amendment. (b) V2: a conserved vehicle stock → four amendments and a migration | M7.6 |
| O-32 | **Sniper form** (D-065 R14, `d065:57` "**Open:** independent formation, or a specialist capability attached") | (a) Its own formation. (b) An attached capability | M7.6 |
| O-33 | **Category-B support (D-065 R10) against R13's entities** | (a) Quantities consumed by the battle system, with no member entities. (b) Battle-transient entities derived from the quantities, kept non-commandable (R19) | M7.6, M7.9 |
| O-15 | **Paired Lanchester inside the TacticalResolver** (`d011:37`). REV is silent | (a) Keep it at formation level, driven by entity state. (b) Entity-level resolution aggregated to outcomes (R8 allows distinct equations per module) | M7.9, M7.11 |

**Group 3: block later packets.**

| # | Question | Alternatives → consequences | Blocks |
|---|---|---|---|
| O-13 | **Host engine and renderer** (D-002; ADR-009). REV:558 lists it as an audit item only | (a) A 3D path in the current MonoGame host. (b) A new host or engine beside the simulation → ADR-009 amended. The choice follows the M7.1 audit | M7.4, M7.10 |
| O-12 | **Target hardware and performance budgets.** REV:475 "Set target hardware and measurable performance budgets before promising scale" | (a) Name a reference machine and budgets before the benchmarks. (b) Measure first → the benchmarks report without a pass line | M7.4, M7.20 (I.9) |
| O-25 | **How battlefield assets are produced** (`d038:75` F4). REV is silent | Procedural, rendered, purchased or commissioned; provenance per asset (D-069 R3) | M7.4, M7.10 |
| O-27 | **"Camera cinema"** (`d011:41`) | (a) Not in M7. (b) In M7.4 or M7.18 | M7.4 |
| O-16 | **Parity tolerance and granularity** (`d011:29`; I.12) | (a) A median-gap band pre-committed before measurement → testable. (b) Qualitative parity → untestable | M7.11/M7.19 |
| O-21 | **Siege-owned starvation state against D-039 D5** (`d039:128`) | (a) The siege owns the state and the food system computes it → no conflict. (b) The siege computes starvation itself → D5 superseded in part | M7.13 |
| O-30 | **Intra-turn supply, upkeep, attrition and siege supply against ADR-019 §1.2** (`adr-019…:51–53`, "intra-turn economic causal chains of any kind") | (a) Economic effects settle at turn granularity → no conflict. (b) Per-pulse economic effects → ADR-019 §1.2 superseded in part | M7.13, M7.14 |
| O-35 | **Which packet ends "Soldiers remain immortal until M7.4"** (`d067:88`; `adr-055:261`) | M7.14 (attrition), M7.19 (Ledger casualties), or v2's B7-04. Bound to O-10 | M7.14, M7.19 |
| O-18 | **Sandbox casualty conservation** | (a) Non-conserved and sandbox-only → no Ledger touch; must never reach a campaign. (b) Conserved → synthetic populations | M7.18 |
| O-19 | **Sandbox force legality** | (a) Legality by computed capability (`milestones.md:93`). (b) Free forces → a parallel vocabulary that needs its own record | M7.5, M7.18 |
| O-20 | **Whether editing reaches campaign-encounter terrain.** REV:487 "without changing combat rules" | (a) Sandbox scenarios only. (b) Editing on loaded encounters → D-009 interaction | M7.5, M7.19 |
| O-31 | **Air units in sandbox or test scenarios beyond the Age reached in play** (REV:513 against REV:537) | (a) Tests may instantiate any designed family; campaign play stays capability-gated. (b) Tests use only Age-reached families | M7.16, M7.17 |
| O-36 | **GA design** (`d065:54` "approved future feature requirement") | Ratify charter:1641's seven-part breakdown as B7-25's scope, or design it in its packet | B7-25 |
| O-37 | **Which resolver path sandbox AI-vs-AI and autoplay battles use; `d011:22` "Used for all AI-vs-AI battles" and `d011:23` "Player-only" as they apply to the sandbox.** REV:381, :481 and :485 do not say | (a) TacticalResolver path → `d011:22–23` superseded in part, for the sandbox only. (b) AutoResolver, as `d011:22` says → no supersession. (c) Either resolver selectable per Battle Test setup → `d011:22–23` superseded in part, for the sandbox only. Campaign battles keep `d011:22–23` under every alternative | M7.18 |
| O-34 | **Packet owners** for ZOC, generals as commanders, category-B allocations, the MobileAgent table design and the catalogue validator | Assign them to M7.6, M7.7, M7.12 or M7.19, or adopt v2's B7-08 | M7.6, M7.7, M7.19 |
| P-1 | **Owner of known-extent routing** (former T7.10; DW-001/002/006). D-067 R10 "deferred to M7" (`d067:33`) still binds. No REV packet names it, and D-075 §5 does not list it | (a) Assign it to an M7.n packet (for example M7.8, detection and knowledge). (b) A separate sub-packet outside the battle program | Planning |

**Group 4: planning and authorization.**

| # | Question | Alternatives | Blocks |
|---|---|---|---|
| O-22 | **Per-packet authorization** (REV:493 "explicit authorization") | Authorize packet by packet (for example M7.1's acceptance, then M7.2 and M7.3), or by lane | Every packet |
| O-23 | **v2's resequencing and the formal upstream path** | Adopt or reject each item of §4.3 TF-1 to TF-4 and TF-9 | Planning |
| O-24 | **Formal adoption of B7-nn** | (a) B7-nn as sub-packets under M7.n, with aliases frozen. (b) M7.n only, with B7 ids kept for R53 alone | Planning |
| O-26 | **The campaign map against D-038 C1 and A3.** Today's decisions reach only the battlefield, so C1 and A3 stay binding on the map | (a) No change. (b) A separate ruling | none in M7 |
| O-28 | **The milestone name "Battle Layer"** (`milestones.md:24`) against R1 | Keep it, or rename at a milestone gate | none |

**Not in the docket, because they are DECIDED today:**
- the 3D battlefield with individual figures (R21, R22; S9, S20–S23);
- air-to-air within the air module (R7; S12);
- the sandbox configuring its own setups (R44; S5);
- "atomic" no longer barring individually simulated members (S8);
- separate clocks and fixed intervals (R29, R30, R33);
- the naval module as an M7 packet (R7, R48);
- siege as an operation, with assaults through the land module (R40, R41);
- GA placed in M7 as B7-25 (R54);
- the critical path (R53).

---

## §3 M7.3 contract plan

**Scope of M7.3 (DECIDED, REV:499):** "Kernel/domain interfaces, entity lifecycle, formation ownership, orders, deterministic event queue, time model, target/effect contracts, inspection, replay and persistence boundaries."
- REV:526: "Every lane uses M7.3 contracts and the same deterministic kernel."
- §3.1 lists the contracts in that scope. §3.2 lists contract obligations decided elsewhere. M7.3's interfaces must accommodate them, and the named later packets deliver them.

**Placement (OPEN).** The owning project or namespace of every framework-side contract is O-14. ADR-060 decides the layers, not the projects:
- the framework kernel (R5);
- the domain modules (R7, R8);
- the combined-arms layer (R9);
- the render side, behind "a separate renderer/simulation boundary" (REV:473).

v2 writes `Sim.Battle` for the kernel (v2:415–429). That name is a proposal. The render host is O-13. The existing one-way rule, "nothing references `Sim.Ui`" (`docs/adr/adr-009-ui-stack.md:36`), is still binding (D-075:294).

**Invariants every contract carries (DECIDED):**
- One clock, event order, identity, inspection, persistence and replay for all modules (R4, R8; I.1, REV:542).
- Renderer invariance: the renderer "must not merge or delete simulation identities", and outcomes are "invariant under camera position, render quality, frame rate and LOD" (REV:452; I.4, REV:545; REV:477).
- Animator AI and Enemy AI have "separate interfaces, responsibilities, logs and tests" (REV:384).
- Events during a pulse, interruption, and deterministic simultaneity (REV:469; I.6, REV:547).
- Causal traceability (I.11, REV:552).
- Capability state, not era gates (REV:434).
- Shared interfaces are "abstractions, not identical mechanics" (REV:442).

**Standing repository rules carried (not new constraints):**
- A POPULATED-table test per new serialized row type (`CLAUDE.md:49`).
- A composite ordering key with a stable integer tie-break, plus a tie-dense test (`CLAUDE.md:48`).
- A turn-exact pin per order-delivery semantic (`CLAUDE.md:50`).
- The banned constructs (Law 5).
- Ledger-only change of people, money and goods (Law 1).

### 3.1 Contracts in M7.3's scope

| # | Contract | Layer (placement O-14) | Inputs → outputs | Invariants (source) | Tests | OPEN inside it | Used by |
|---|---|---|---|---|---|---|---|
| C1 | **Time model**: campaign turn, operational war pulse, tactical battle pulse, internal step | Kernel | configuration and current time → pulse and step boundaries | Four layers (REV:462–465); "Separate clocks" (REV:533); fixed intervals (REV:463–464); rates per sim-year × dt (Law 3) | Dimensional declaration (A-8); boundary pins: an event at a pulse edge lands in the stated pulse; replay | O-2, O-3, O-4, O-5, O-29 | M7.11, M7.13, M7.14 |
| C2 | **Deterministic event queue and ordering** | Kernel | scheduled actions → one totally ordered event stream | "deterministic event priority, tie-breaking, collisions, interruptions, target invalidation, simultaneous effects and RNG consumption" (REV:469); no artificial movement-then-fire phases (REV:469) | Tie-dense ordering test (`CLAUDE.md:48`). Simultaneity test: both sides' orders against one evolving state (REV:385). Interruption and target-invalidation tests (I.6). Fixed RNG consumption order. Replay hash equality | O-6 (key shape; v2's five-field tuple is a candidate, v2:573) | all |
| C3 | **Entity identity and lifecycle** | Kernel | spawn, damage, death and removal → stable entity ids | Entities with "type-appropriate state" (REV:448); never merged or deleted by rendering (REV:452, :477) | Identity-stability test under render-setting changes. POPULATED round-trip for any serialized entity row | O-10, O-11 | M7.6, M7.9 |
| C4 | **Formation ownership and membership** | Kernel | formation + members → command state | The formation is "the player-commandable group" (REV:449). Members are not commandable families (REV:538). One unit commanded while entities act under its intent (I.3, REV:544) | The command surface accepts formation orders only. Membership round-trip | O-11, O-32, O-33; who owns membership between ADR-055's campaign tables and the framework (O-14) | M7.6, M7.12 |
| C5 | **Order contract** (formation-level orders, logged per pulse) | Kernel | legal formation orders → order log → applied at stated pulse and step | Campaign battles keep "player battle orders append to the order log per pulse; replay reproduces the battle blow-for-blow" (`d011:28`, retained, D-075:277). Order latency is delay, not degradation (D-039 A3, retained) | A delivery pin per delivery semantic (`CLAUDE.md:50`). Exact replay (REV:507). Only-legal rejection tests | O-14 (c) if the campaign `OrderRecord` shape is touched (`OrderLog.cs:5`) | M7.11, M7.12 |
| C6 | **Movement and spatial queries** | Kernel | positions, terrain, occupancy → paths, collisions, ranges | "movement/spatial queries" are kernel-owned (REV:412). Terrain costs, collision, spacing, facing (REV:503) | Tie-dense path tests. Determinism across seeds | O-7, O-7a, O-8, O-9; O-17 for numerics | M7.7 |
| C7 | **Detection and knowledge** | Kernel, with sensor rules per domain module | observations → knowledge states (live, remembered or ghost, unknown) and confidence | Realistic spotting, LOS, concealment, cover effects (REV:386). Concealment affects detection and is inspectable (I.5, REV:546). D-039 Parts A–C retained (D-075:278) | Concealment metamorphic test (more cover → never better detection). Fog-honesty test. Ghost reverts to live on re-observation (D-039 C3) | O-7 | M7.8, M7.12 |
| C8 | **Target acquisition and effects** | Kernel interface; combined-arms layer for cross-domain | target information → classification → eligibility → weapon or effect → interception → damage → knowledge and inspection update | The REV:432 sequence. Compatibility derives from "equipment, weapons, sensors, target class, range, altitude/depth, line of sight and relevant capability state" (REV:434) | One test per sequence step. A negative test: an ineligible pair cannot engage. One cross-domain engagement end-to-end (I.2, REV:543) | O-15 | M7.9, M7.17 |
| C9 | **Domain module contract** | Kernel ↔ modules | module registration → rules and capabilities through stable contracts | A module "may contain distinct equations and mechanics. It must reuse the kernel's clock, event ordering, identity, inspection, persistence and replay contracts" (REV:426). No "giant collection of era-specific combat conditionals" (REV:414) | A conformance test per module (land, naval, air, siege). Representative-era distinctness (I.10, REV:551) | O-1 (naval resolver mode) | M7.9, M7.13, M7.15, M7.16 |
| C10 | **Inspection and causal log** | Kernel | events and decisions → trace | Material decisions trace to "inputs, knowledge, rules/capabilities, events and state changes" (I.11, REV:552). Developer environment (REV:388) | Trace completeness: every state change has a causing event | — | M7.12, M7.18 |
| C11 | **Replay and persistence boundaries** (battle state, scenario, RNG ownership) | Kernel | state, seed and orders → serialized state; reload → identical continuation | "A saved sandbox scenario reopens and replays identically" (I.8, REV:549). RNG ownership is a kernel duty (REV:412) | POPULATED round-trip with ExpectedLength and hash. Save mid-pulse then reload → identical outcome. Replay equality, plus separate delivery pins | O-6 (RNG keying where there is no `regionId`; `m0-kernel-spec.md:72`); O-14; O-18 | M7.5, M7.11, M7.18 |

### 3.2 Contract obligations decided elsewhere (M7.3 makes room; later packets deliver)

| # | Contract | Layer | Inputs → outputs | Invariants (source) | Tests | OPEN | Packet |
|---|---|---|---|---|---|---|---|
| C12 | **Render boundary** (a read-only view of simulation state) | Simulation → render, one way | state → render representation | "visual models, instances, grouped representations or impostors" (REV:450). Fusing "only as a visual batching/LOD technique" (REV:477) | **Renderer-invariance test**: run the same battle under different camera, LOD, render-quality and frame-rate settings → identical state hash (I.4) | O-13, O-12 | M7.4 |
| C13 | **Animator AI interface** | Render side | state + formation intent → spacing, facing, locomotion, weapon handling, hit and death animation | "must not choose targets, invent movement, decide casualties, alter morale or change combat outcomes" (REV:454). Its own log (REV:384) | **Separation tests:** (1) the Animator has no write path to simulation state (reference direction, as `adr-009:36–37`); (2) disabling or varying the Animator leaves the state hash unchanged; (3) its log is separate from the Enemy AI log | O-13 | M7.10 |
| C14 | **Enemy AI interface** | Kernel side | permitted knowledge (`PolityKnowledge`/battle knowledge) → formation-level orders | "using only permitted knowledge … does not control visual animation" (REV:456). "No autonomous tactical personality layer" (REV:389). Its own log (REV:384) | Only-legal orders. Fog-honesty metamorphic test (hidden facts changed → identical orders). Animator-independence (varying the animator → identical orders). Tie-dense decision ordering | — | M7.12 |
| C15 | **Siege operation contract** | Siege module, on operational and campaign time | investment, blockade, supply and works → siege state; an assault → a tactical battle through the land module | REV:438, :467. "Siege must not duplicate combat resolution" (REV:438). I.7 (REV:548). Fortification in campaign computed from buildings (D-039 D1, retained) | Siege state advances only on operational or campaign time. An assault invokes C9 (land). Exact **Ledger conservation** for capture, casualties and starvation where campaign-linked | O-21, O-30, O-2 | M7.13 |
| C16 | **Campaign resolver contract** (`BattleSetup`, `BattleOutcome`, `IBattleResolver`, two implementations) | Adapter between campaign and framework | campaign state → setup; outcome → Ledger | "identical inputs and outputs" (`d011:21`). Casualties "debited from actual population cohorts via `Ledger` — Law 1, exact" (`d011:27`). The parity rule (`d011:29`). I.12 (REV:553). M7.19 (REV:515). Retained for campaign battles (D-075:277) | **Ledger conservation** with exact equality. Parity battery. POPULATED round-trips. Delegation path | O-15, O-16; the parity battery's placement (O-23); casualty granularity (O-10); the call shape (v2's `Begin/Advance/Outcome`, v2:419, is a proposal) | M7.19 |
| C17 | **Sandbox scenario contract** | Framework | scenario file → setup; edits → persisted scenario | Standalone, same kernel and modules (REV:481). Modes (REV:483–487). Self-configured setups (S5) | Scenario round-trip with an identical replay (I.8). Seeded repetitions. Parameter sweeps reproducible | O-18, O-19, O-20, O-31, O-37 | M7.5, M7.18 |
| C18 | **Performance instrumentation** | Both sides, measured separately | runs → entity and formation counts, frame rate, simulation throughput, CPU/GPU memory, stress results | REV:475; I.9 (REV:550). "Do not promise a maximum battle size before measurement" | Reports only. Pass lines wait on O-12 | O-12, O-11 | M7.4, M7.20 |

---

## §4 Dependency plan

### 4.1 REV §G dependencies (DECIDED, R48; REV:497–516)

| Packet | B7 | REV dependencies (verbatim) |
|---|---|---|
| M7.1 | B7-01 | None |
| M7.2 | B7-02 | M7.1 |
| M7.3 | B7-03 | M7.1-M7.2 |
| M7.4 | B7-05 | M7.1; aligned with M7.3 |
| M7.5 | B7-06 | M7.3-M7.4 |
| M7.6 | B7-07 | M7.3 |
| M7.7 | B7-09 | M7.3, M7.5-M7.6 |
| M7.8 | B7-10 | M7.5-M7.7 |
| M7.9 | B7-11 | M7.3, M7.6-M7.8 |
| M7.10 | B7-12 | M7.4, M7.6-M7.7, M7.9 |
| M7.11 | B7-15 | M7.2-M7.10 |
| M7.12 | B7-16 | M7.8-M7.9, M7.11 |
| M7.13 | B7-18 | M7.7-M7.12; campaign contract |
| M7.14 | B7-17 | M7.2-M7.3, M7.6-M7.7 |
| M7.15 | B7-19 | M7.3, M7.6, M7.8, M7.11, M7.14 |
| M7.16 | B7-20 | M7.3, M7.6, M7.8, M7.11, M7.14 |
| M7.17 | B7-21 | M7.9, M7.15-M7.16 |
| M7.18 | B7-22 | M7.4-M7.12; integrate domain modules incrementally |
| M7.19 | B7-23 | M7.11-M7.18 |
| M7.20 | B7-29 | All required packets |

### 4.2 Dependency changes: consequences of D-075

| # | Change | Reason |
|---|---|---|
| DC-1 | **B7-24 (military buildings and calibration) is added and depends on B7-18 (M7.13).** | R54 (D-075:158); edge from v2:308; on the decided path (R53) |
| DC-2 | **B7-25 (GA) is added and depends on B7-24.** | R53 edge B7-24 → B7-25; R54 |
| DC-3 | **B7-29 (M7.20) depends on B7-25.** | R53 edge B7-25 → B7-29; REV:516 "All required packets" |
| DC-4 | **M7.7's movement implementation waits on the O-8 and O-9 rulings.** | REV:536 "Resolve before movement implementation" (DECIDED). Consistent with `d052:48–49` (retained) |
| DC-5 | **M7.15's resolver mode waits on O-1.** | REV:511 "Tactical versus auto-only resolution decided by formal ruling"; REV:532 |
| DC-6 | **M7.2's deliverable shrinks.** The siege boundary and the "required D-record/ADR amendments" are discharged. | R40, R41; D-075; ADR-060 (D-075:461) |
| DC-7 | **v2's B7-00 leaves the graph.** Recording the direction is done (A-1, A-2). | D-075:425 |
| DC-8 | **Every packet waits on its own authorization.** | REV:493 (DECIDED); O-22 |

### 4.3 Dependency changes: technical findings (proposed; each OPEN until the Director rules)

| # | Change | Source and reason | Open item |
|---|---|---|---|
| TF-1 | The renderer (M7.4) depends on the docket, not only on M7.1 | v2:316. Against REV:500 "M7.1; aligned with M7.3". O-13 (host engine) independently blocks M7.4 (ADR-060 §5) | O-23 |
| TF-2 | M7.5's terrain data contract becomes a **soft** edge on M7.4. It must still land before M7.7 | v2:290, :317, :325. REV:501 makes M7.5 depend on M7.4 outright | O-23 |
| TF-3 | The parity battery moves from M7.19 to M7.11 (B7-15) | v2:299, :349. REV:515 governs until the Director rules (D-075:428) | O-23 |
| TF-4 | Added sub-packets: B7-04 (supply and attrition; end of immortality), B7-08 (catalogue), B7-13 (war state), B7-14 (AutoResolver and contact), with v2's edges (v2:288, :292, :297–298) | v2 §D.1. No REV parent | O-23, O-34, O-35 |
| TF-5 | **EC-1–EC-3 are not future dependencies.** They are ratified (D-067 R16–R18, `d067:99–101`) and delivered (commit `f7a59b8`, an ancestor of `7d5defc`; `docs/current-state.md:89` "separately switchable packets"). v2's edge "content: B7-26, B7-28 for anti-air" (v2:305) reduces to whether those packets are switched on in the configuration used. That was **not verified** | Found while writing this plan. v2:310–312 treats them as packets; D-075:375 did too until its correction on 2026-11-03 | — (record correction, A-7) |
| TF-6 | Air content (M7.16) also depends on EC-4a, engines and fuel (`Sim.Data/content/unit-families.json:30–31`), which "stays provisionally M10" (`d065:41`) | v2:304, v2:167 | O-31 |
| TF-7 | M7.18 (sandbox) depends on the O-18, O-19, O-20 and O-37 rulings | v2:306 (DEC-8b/c); D-075 O-37 | O-18–O-20, O-37 |
| TF-8 | Known-extent routing (D-067 R10) has no packet | Former T7.10 (REV:217) | P-1 |
| TF-9 | **The upstream path has two co-critical branches.** Under REV's edges, M7.3 → M7.5 → M7.7 (via REV:501 and :503) is as long as M7.3 → M7.6 → M7.7 (via REV:502–503). v2's caveat agrees: "B7-06's terrain data contract must land before B7-09" (v2:325) | Count of dependency edges only; no durations are measured | O-23 |
| TF-10 | Blocking open items per packet: O-14, O-7, O-7a and O-17 block M7.3; O-10 and O-11 block M7.6 | ADR-060:377–382 | — |
| TF-11 | v2 verifier errata (v2:564–575) and their effect on dependencies: | v2 verifier | — |
| | E2: the critical path is corrected by the Director (§5). | | |
| | E5: the event-key tuple is a proposal and is carried as O-6, not as a dependency. | | |
| | E1, E3, E4, E6, E7: citation fixes with no dependency effect. | | |

**Standing gates carried, not new dependencies:**
- The D-069 R3 provenance gate covers assets M7.4 and M7.10 produce (`d069:20`; v2 row 26). It gates release, not packet order.
- The S8 §4.1 items (A-8) gate the ratification of the M7 spec text.

---

## §5 Critical path (DECIDED, R53; corrected per the Director's instruction 9)

**B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29**

| Step | B7 | Packet | Edge evidence (REV) | Edge evidence (v2 table) |
|---|---|---|---|---|
| 1 | B7-11 | M7.9 Land warfare foundations | — | — |
| 2 | B7-12 | M7.10 Individual combat and Animator AI | REV:506 depends on "M7.9" | v2:296 |
| 3 | B7-15 | M7.11 Tactical pulse and WEGO resolution | REV:507 "M7.2-M7.10" | v2:299 |
| 4 | B7-16 | M7.12 Enemy AI and battle inspection | REV:508 "M7.11" | v2:300 |
| 5 | B7-18 | M7.13 Siege operations | REV:509 "M7.7-M7.12" | v2:302 |
| 6 | B7-24 | (no M7.n) Military buildings and calibration; charter alias M7.7 | none in REV; by R54 | v2:308 |
| 7 | B7-25 | (no M7.n) Garrisoning, GA; charter alias M7.8 | none in REV; by R54 | v2:309 |
| 8 | B7-29 | M7.20 Whole-M7 integration, optimization and exit | REV:516 "All required packets" | v2:313 |

**What this path replaces.**
- It replaces v2 §D.1's path, "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11 → B7-15 → B7-22 → B7-29" (v2:325).
- v2's own verifier showed that path is not the longest chain (error E2, v2:566–570). B7-15 depends on B7-12; B7-22 depends on B7-16; and B7-18 → B7-24 → B7-25 → B7-29 is longer.
- The old path is superseded (D-075 §2.6, D-075:269). v2 is a working document and is not edited.

**ADMIN corroboration (not a decision).** The competing chains from B7-15, counted in dependency edges under REV §G plus DC-1 to DC-3, are all shorter:
- B7-15 → B7-19 → B7-21 → B7-23 → B7-29 (REV:511, :513, :515);
- B7-16 → B7-22 → B7-23 → B7-29 (REV:514, :515);
- B7-18 → B7-23 → B7-29.

The Director's path stands as decided whatever such counts show. No packet durations are measured, so nothing here is a schedule.

**Upstream of B7-11 (ADMIN derivation, not a Director decision; formal status O-23).**
- v2's segment, quoted exactly from v2:325, is "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11" (D-075:425). B7-00, "Record the direction" (v2:284), is discharged by D-075 and is not a packet (DC-7). The remaining nodes are M7.2 → M7.3 → M7.6 → M7.7 → M7.8 → M7.9, and REV makes M7.2 depend on M7.1 (REV:498).
- Under REV:501 and :503, the branch M7.3 → M7.5 → M7.7 is co-critical (TF-9).
- The open-decision gates sit on this segment: O-14, O-7, O-7a and O-17 at M7.3; O-10 and O-11 at M7.6; O-8 and O-9 at M7.7 (DC-4).
- **Consequence recorded:** the path places GA (B7-25) on M7's route to exit. This is R54. GA's design stays OPEN (O-36).

---

## §6 Parallel lanes (DECIDED, R49; REV:518–526)

| Lane (REV wording) | Packets | B7 |
|---|---|---|
| **Graphics:** "M7.4 → M7.5 → visual work in M7.10/M7.18." (REV:520) | M7.4, M7.5, M7.10 (visual), M7.18 (visual) | B7-05, B7-06, B7-12, B7-22 |
| **Simulation:** "M7.6 → M7.7 → M7.8 → M7.9 → M7.11." (REV:521) | M7.6, M7.7, M7.8, M7.9, M7.11 | B7-07, B7-09, B7-10, B7-11, B7-15 |
| **Campaign:** "M7.14, then M7.13 and M7.19." (REV:522) | M7.14, M7.13, M7.19 | B7-17, B7-18, B7-23 |
| **Domains:** "M7.15 and M7.16 after shared contracts; M7.17 connects them." (REV:523) | M7.15, M7.16, M7.17 | B7-19, B7-20, B7-21 |
| **AI/testing:** "M7.12 and test-laboratory work in M7.18 as interfaces stabilize." (REV:524) | M7.12, M7.18 | B7-16, B7-22 |

- The lanes start "after M7.1-M7.3" (REV:518).
- REV:526: "Parallelism does not authorize incompatible implementations. Every lane uses M7.3 contracts and the same deterministic kernel."
- **The critical path crosses lanes:**
  - Simulation: M7.9, M7.11;
  - Graphics and simulation: M7.10;
  - AI/testing: M7.12;
  - Campaign: M7.13;
  - then B7-24, B7-25, and M7.20.
- **REV assigns B7-24 and B7-25 to no lane.** By dependency they follow M7.13. Whether they belong to the Campaign lane is not decided here (O-24).
- P-1 (known-extent routing) has no lane until it has an owner.
- Parallelism authorizes nothing: each packet still needs its own authorization (O-22).

---

## §7 Not authorized by this plan

- No implementation, build, test run, commit, push, branch, tag or merge.
- This plan appends no dated note. The notes present in the tree were written by another writer (A-3).
- When this plan was first written it changed neither ADR-060 nor D-075. A-6 and A-7 were applied to them in the corrections pass of 2026-11-03 ("Corrections applied — 2026-11-03", at the end of D-075).
- No decision on any item in §2(b).

## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)

Record: `docs/d076-m7-decision-reconciliation.md` (Director decisions of 2026-11-04).
- **S84, S85:** this 2026-11-03 plan is revised by `docs/m7-plan/m7-2-m7-3-revision-2026-11-04.md` (docket §2(b), contract open columns, TF-10, gates). B7-nn are sub-packets under M7.n (O-24 closed). Where the revision is silent, this plan stands.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S375** (`m7-2-docket-m7-3-contracts.md:37`) — "REV gives them no M7.n number. Whether they get one is part of O-24." — IN FULL: mapped as sub-packets under an existing M7.n (ADMIN). **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [ADMINISTRATIVE CONSEQUENCE]
- **S376** (`m7-2-docket-m7-3-contracts.md:62`) — "v2's move of the parity battery to B7-15 is OPEN (O-23)" — IN FULL: not adopted; REV:515 placement stands; parity improvement is authorized foundational work now. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S377** (`m7-2-docket-m7-3-contracts.md:64`) — "No M7.n number (O-24)" — IN FULL: B7-24 mapped under an M7.n parent (ADMIN). **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [ADMINISTRATIVE CONSEQUENCE]
- **S378** (`m7-2-docket-m7-3-contracts.md:65`) — "Design OPEN (O-36)" — IN PART: GA scope set; mechanics designed in B7-25. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S379** (`m7-2-docket-m7-3-contracts.md:67`) — "These are proposals, not decided. Each is OPEN under O-23, and where noted under O-34 or O-35." — IN PART: mapped under M7.n (ADMIN); O-35 decided. **Replaced by** DIR5 §1.2, §2.9 (D-079 DR2, DR3, DR23). [ADMINISTRATIVE CONSEQUENCE]
- **S380** (`m7-2-docket-m7-3-contracts.md:72`) — "Owner of "immortal until M7.4" is O-35" — IN FULL: M7.14; B7-04 under M7.14. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S381** (`m7-2-docket-m7-3-contracts.md:92`) — "**OPEN (O-35)**: M7.14, M7.19 or v2's B7-04" — IN FULL: M7.14. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S382** (`m7-2-docket-m7-3-contracts.md:151`) — "O-14(c) → a kernel ADR (`OrderLog.cs:5`)" — IN PART: O-14(c) not chosen; the kernel ADR arises from N2/N4 (A-13/ADR-061), not O-14; letter "(c)" mismatches D-075 §5's "(b)". **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [ADMINISTRATIVE CONSEQUENCE]
- **S383** (`m7-2-docket-m7-3-contracts.md:152`) — "O-17(b) → CR-013 reopened and ADR-022 amended" — in full (condition met): the CR-013 reopening and the ADR-022 amendment are now owed as ADMINISTRATIVE consequences (A-33). **Replaced by** DIR5 §3.2, §1.1(5) (D-079 DR1, DR35). [ADMINISTRATIVE CONSEQUENCE]
- **S384** (`m7-2-docket-m7-3-contracts.md:153`) — "O-1(b) with controllable transport → an ADR-031 amendment (`d065:56`)" — IN FULL (condition met): the ADR-031 amendment and save migration are now owed (ADMIN). **Replaced by** DIR5 §2.1 (D-079 DR8). [ADMINISTRATIVE CONSEQUENCE]
- **S385** (`m7-2-docket-m7-3-contracts.md:154`) — "O-29(b) or O-30(b) → a dated supersession note on ADR-019 §1.4 or §1.2" — IN PART: O-30(b) -> ADR-019 §1.2 note owed; O-29(a) -> no §1.4 note. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [ADMINISTRATIVE CONSEQUENCE]
- **S386** (`m7-2-docket-m7-3-contracts.md:168`) — "| O-8 | **Action Capacity against Law 3** (CR-017 §3)." — IN PART: two actions max per decision window, integrated with AC. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S387** (`m7-2-docket-m7-3-contracts.md:172`) — "| O-29 | **Whether the operational pulse is a world-global clock.**" — IN FULL: (a) local to wars. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S388** (`m7-2-docket-m7-3-contracts.md:178`) — "an ADR-031 amendment and a save migration if transport vessels become controllable" — IN FULL (transport): controllable transport vessels included. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S389** (`m7-2-docket-m7-3-contracts.md:184`) — "| O-14 | **Where REV's "kernel" (R5) lives in code.**" — IN PART: (a) chosen as direction; (b) and (c) not chosen; creation awaits review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S390** (`m7-2-docket-m7-3-contracts.md:186`) — "| O-17 | **Cross-platform determinism scope.**" — in full: (b), strengthened to required equality; the existing divergence must be fixed. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S391** (`m7-2-docket-m7-3-contracts.md:187`) — "| O-11 | **Caps and counted objects.**" — IN PART: values preserved; V1 transient vehicles; CAP-1..5 resolution. **Replaced by** DIR5 §2.10, §2.9 (D-079 DR23–DR25). [DIRECTOR DECISION]
- **S392** (`m7-2-docket-m7-3-contracts.md:188`) — "| O-32 | **Sniper form**" — IN FULL: (a). **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S393** (`m7-2-docket-m7-3-contracts.md:189`) — "| O-33 | **Category-B support (D-065 R10) against R13's entities**" — IN FULL: (a). **Replaced by** DIR5 §2.12 (D-079 DR29). [DIRECTOR DECISION]
- **S394** (`m7-2-docket-m7-3-contracts.md:197`) — "| O-12 | **Target hardware and performance budgets.**" — IN FULL (procedure): (b) measure first; budgets after Victus measurement. **Replaced by** DIR5 §6.5, §7 (D-079 DR48, DR50). [DIRECTOR DECISION]
- **S395** (`m7-2-docket-m7-3-contracts.md:201`) — "| O-21 | **Siege-owned starvation state against D-039 D5**" — IN PART (READING): (b) excluded by "do not duplicate existing resource models". **Replaced by** DIR5 §2.2, §2.11 (D-079 DR9, DR26–DR28). [READING]
- **S396** (`m7-2-docket-m7-3-contracts.md:202`) — "| O-30 | **Intra-turn supply, upkeep, attrition and siege supply against ADR-019 §1.2**" — IN FULL: (b). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S397** (`m7-2-docket-m7-3-contracts.md:203`) — "| O-35 | **Which packet ends "Soldiers remain immortal until M7.4"**" — IN FULL: M7.14. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S398** (`m7-2-docket-m7-3-contracts.md:204`) — "| O-18 | **Sandbox casualty conservation**" — IN PART: isolation decided. **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S399** (`m7-2-docket-m7-3-contracts.md:205`) — "| O-19 | **Sandbox force legality**" — IN FULL: (b). **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S400** (`m7-2-docket-m7-3-contracts.md:206`) — "| O-20 | **Whether editing reaches campaign-encounter terrain.**" — IN FULL: (a). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S401** (`m7-2-docket-m7-3-contracts.md:207`) — "| O-31 | **Air units in sandbox or test scenarios beyond the Age reached in play**" — IN FULL: (a). **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S402** (`m7-2-docket-m7-3-contracts.md:208`) — "| O-36 | **GA design**" — IN PART: scope set. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S403** (`m7-2-docket-m7-3-contracts.md:209`) — "| O-37 | **Which resolver path sandbox AI-vs-AI and autoplay battles use;" — IN PART: (b) for AI-vs-AI; parity autoplay READING. **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S404** (`m7-2-docket-m7-3-contracts.md:210`) — "| O-34 | **Packet owners**" — IN PART: ADMIN mapping; Q4 open. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [ADMINISTRATIVE CONSEQUENCE]
- **S405** (`m7-2-docket-m7-3-contracts.md:218`) — "| O-23 | **v2's resequencing and the formal upstream path**" — IN FULL (resequencing): not adopted wholesale. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S406** (`m7-2-docket-m7-3-contracts.md:242`) — "**Placement (OPEN).** The owning project or namespace of every framework-side contract is O-14." — IN PART: Sim.Battle direction; creation awaits review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S407** (`m7-2-docket-m7-3-contracts.md:248`) — "v2 writes `Sim.Battle` for the kernel (v2:415–429). That name is a proposal." — IN FULL (status): Sim.Battle is the Director's chosen direction; retained: not yet created. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S408** (`m7-2-docket-m7-3-contracts.md:273`) — "who owns membership between ADR-055's campaign tables and the framework (O-14)" — IN PART: an M7.3 contract C4 design item, no longer O-14. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [ADMINISTRATIVE CONSEQUENCE]
- **S409** (`m7-2-docket-m7-3-contracts.md:274`) — "O-14 (c) if the campaign `OrderRecord` shape is touched (`OrderLog.cs:5`)" — IN PART: campaign OrderRecord stamp is A-13 (kernel ADR), not O-14. **Replaced by** DIR5 §1.3, §2.5 (D-079 DR4–DR6, DR15–DR18). [ADMINISTRATIVE CONSEQUENCE]
- **S410** (`m7-2-docket-m7-3-contracts.md:290`) — "the parity battery's placement (O-23)" — IN FULL: stays M7.19 (REV:515). **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S411** (`m7-2-docket-m7-3-contracts.md:291`) — "O-18, O-19, O-20, O-31, O-37 | M7.5, M7.18 |" — IN PART: O-19, O-20, O-31 decided; O-18, O-37 partly. **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S412** (`m7-2-docket-m7-3-contracts.md:340`) — "| TF-1 | The renderer (M7.4) depends on the docket, not only on M7.1 |" — IN FULL (READING): not adopted: REV:500 edge stands ("respect the established dependency graph"). **Replaced by** DIR5 §1.2, §8 (D-079 DR2, DR3, DR51). [READING]
- **S413** (`m7-2-docket-m7-3-contracts.md:341`) — "| TF-2 | M7.5's terrain data contract becomes a **soft** edge on M7.4." — IN FULL (READING): not adopted: REV:501 edge stands. **Replaced by** DIR5 §1.2, §8 (D-079 DR2, DR3, DR51). [READING]
- **S414** (`m7-2-docket-m7-3-contracts.md:342`) — "| TF-3 | The parity battery moves from M7.19 to M7.11 (B7-15) |" — IN FULL: not adopted. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S415** (`m7-2-docket-m7-3-contracts.md:343`) — "| TF-4 | Added sub-packets: B7-04 (supply and attrition; end of immortality), B7-08 (catalogue), B7-13 (war state), B7-14 (AutoResolver and contact), with v2's edges" — IN PART: adopted only as sub-packets under M7.n parents; v2 edges that reorder REV §G not adopted. **Replaced by** DIR5 §1.2, §2.9 (D-079 DR2, DR3, DR23). [ADMINISTRATIVE CONSEQUENCE]
- **S416** (`m7-2-docket-m7-3-contracts.md:345`) — "| TF-6 | Air content (M7.16) also depends on EC-4a" — IN PART: sandbox/tests use any designed family regardless of EC-4a; retained for campaign realization. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S417** (`m7-2-docket-m7-3-contracts.md:346`) — "| TF-7 | M7.18 (sandbox) depends on the O-18, O-19, O-20 and O-37 rulings |" — IN PART: rulings now exist (O-18 isolation only). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S418** (`m7-2-docket-m7-3-contracts.md:349`) — "O-14, O-7, O-7a and O-17 block M7.3" — in part: O-7 and O-7a (D-076 N6/N7, DIR5 §2.7) and O-17 (DIR5 §3.2) are closed; O-14 is decided as direction (Sim.Battle), so M7.3 contract text is no longer blocked by it; project creation waits for the architecture review. Retained: O-10/O-11 gates at M7.6 as narrowed by DIR5 §2.9/§2.10. **Replaced by** DIR5 §1.3, §2.7, §3.2 (D-079 DR4–DR6, DR21, DR35). [DIRECTOR DECISION]
- **S419** (`m7-2-docket-m7-3-contracts.md:391`) — "O-14, O-7, O-7a and O-17 at M7.3" — in part: as docket:349; O-8 is decided in substance (two actions per decision window), O-9 stays OPEN at M7.7. Retained: the segment itself. **Replaced by** DIR5 §1.3, §2.7, §2.8, §3.2 (D-079 DR4–DR6, DR21, DR22, DR35). [DIRECTOR DECISION]
- **S420** (`m7-2-docket-m7-3-contracts.md:414`) — "Whether they belong to the Campaign lane is not decided here (O-24)." — IN FULL: lane follows the M7.n parent chosen in the mapping (ADMIN). **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [ADMINISTRATIVE CONSEQUENCE]
