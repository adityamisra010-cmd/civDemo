# DIRECTOR LEDGER GAP AUDIT — the repository measured against the Director Architecture and Decision Ledger

**Status:** READ-ONLY AUDIT, 2026-10-02.
- It designs nothing and implements nothing.
- It changes no code, content, test, golden or ruling.
- Every change it proposes is a proposal for the Director. None is an instruction, and none was applied.

**Authority:** `docs/design/director-architecture-and-decision-ledger.md`, Part I — "the ledger" below.
- The ledger is authoritative for every decision it explicitly contains (Director instruction, 2026-10-02).
- Where the ledger is silent, the ratified repository record governs (ledger §0).

**Tree:** `research-progression-foundation` @ `5e83e61`.
- Every `file:line` below was read on that tree unless it is marked otherwise.
- Records marked **UNMERGED-B** were read on `origin/claude/civdemo-work-b1z2y4` @ `6dded01`.
- The location tags (MAIN, PR #10, UNMERGED-B, M5-BRANCH) are defined in ledger annex R2.

**Evidence rule** (`docs/gov-4-repository-freshness.md` §2):
- The implementation agent's earlier partial analysis (`gap-partial.txt`, ledger R1) is secondary
  evidence. A claim from it appears here only after it was re-measured.
- Test results are dated, because they expire with the tree.

---

## 0. Repository state (gov-4 §3)

| branch | HEAD | upstream | merge-base with `main` | ahead / behind `main` | last commit | classification |
|---|---|---|---|---|---|---|
| `research-progression-foundation` | `5e83e61` | `origin/research-progression-foundation`, in sync | `93270cd` | +19 / −0, measured at `5e83e61` before this pass's documentation commits | 2026-10-01 | **ACTIVE.** PR #10 is open and not merged. CI is green on `5e83e61`: `build-and-test`, `determinism`, `determinism-xproc` and `calibration` succeeded on 2026-10-01; `calibration-nightly` was skipped |
| `main` | `93270cd` | — | — | — | 2026-09-30 | accepted truth |
| `claude/civdemo-work-b1z2y4` | `6dded01` | origin | `de5e00e` | +22 / −6 | 2026-09-30 | **ACTIVE, UNMERGED.** It holds D-043, CR-017 and the Trees, Ages and world UI. It is not an ancestor of `5e83e61` |
| `m5-full-build` | `9a79d1e` | origin | `dbef61a` | +4 / −174 | 2026-09-05 | **STALE against `main`.** It holds the unmerged M5 governing loop, CR-008, and a copy of CR-005 marked RULED (Option C) |
| `t4.19-glass-box` | `2807155` (local only; no remote ref) | — | — | — | 2026-09-16 | **MERGED.** Its tip `2807155`, and the handoff's `221f883`, are ancestors of `main` |

- **Working tree.** It was clean at `5e83e61` before this pass. This pass adds two documents: the ledger and this audit.
- **The ledger.** No canonical ledger existed on any of the 157 local and remote refs before this pass (measured).

## 1. Classification key (Step 6 of the Director's instruction)

| class | meaning |
|---|---|
| **RATIFIED DIRECTOR DECISION** | the ledger or a ratified record rules it; the repository must follow |
| **STALE DOCUMENTATION** | a document still states what a later ruling changed. Fix it by an append-only note, never by rewriting a frozen or historical record |
| **IMPLEMENTATION GAP** | the rule stands, but the code or content does not implement it yet |
| **IMPLEMENTATION CONFLICT** | shipped code or content contradicts a ratified rule |
| **OPEN DECISION** | the Director must rule. It is not silently resolved here |
| **DEFERRED** | intentionally postponed (ledger §27). Do not invent an implementation |
| **HISTORICAL / SUPERSEDED** | kept for provenance; a named later ruling governs |

- **CONFORMS** marks an area with no gap.
- **CONTENT REVIEW** marks a content question the Director must judge before any edit. `research.json` is
  Director-ratified content (D-046 F§1, `docs/d046-research-foundation-gate-rulings.md:367`), and the
  capability matrix requires *"a Director decision before it is an edit"*
  (`docs/design/research-capability-ownership.md:73`).

Each finding carries an id, **G-nn**. §8 collects every proposed change, giving its file, system, reason,
supporting decision, dependency, and whether it falls **NOW** or **LATER**:
- **NOW** — eligible for the next pre-M5 normalization packet, once the Director approves it;
- **LATER** — waits for a ruling, or for the milestone or system that owns it.

---

## 2. The thirty areas

### Area 1 — RP terminology and per-turn generation — **CONFORMS**

**Ledger:** §5.1 and §5.6.

**Implementation:**
- **Names.** The ruled names are used: `ResearchQuery.ResearchPoints` (`Sim.Core/State/ResearchQuery.cs:362-363`),
  `ResearchPointPool` (`:371-372`), and the `ResearchProgress` table.
- **No stale terms in code.** No `CLP` or `Cognitive` identifier occurs in `Sim.Core`, `Sim.Cli`, `Sim.Ui`, `scripts`
  or `.github` (measured).
- **Content.** The rate is RP/turn = 0.08 × P^0.699 (`research.json` `tuning.rpPerTurn`, from `:19`).
- **One code site, with no `dtYears`.** `ResearchSystem.cs:158-167`, commented with ADR-030 §2.

**Tests:**
- `ResearchEngineTests.ResearchPoints_TheAnchors_Population100Gives2_And1000Gives10_ToTolerance` (`:30`);
- `ResearchPoints_ArePerTurn_IdenticalAcrossDifferentDtYears_TheAdr030Exception` (`:84`).

**Records:** ADR-030 is RATIFIED. CR-018 carries an append-only RESOLUTION. D-044 R2's earlier name is
HISTORICAL (ledger R4.3).

**Note.** Two test comments inside historical golden re-pin blocks still say "CLP"
(`Sim.Tests/Kernel/DrivenGoldenTests.cs:385`, `Sim.Tests/Kernel/SnapshotTests.cs:861`). The ledger §5.1
permits historical wording, so no change is proposed.

### Area 2 — the research pool and RP overflow — **CONFORMS in code · OPEN on the record**

**Ledger:** §5.2, §5.5 and §8.2.

**Implementation:**
- **One pool per polity,** spent only on the target.
- **`Credit()` caps at the remaining EffectiveCost,** and *"The surplus goes nowhere"* (`ResearchSystem.cs:199-214`).
- **No owned table holds unspent RP.**

**Tests:**
- `Completion_AtEffectiveCost_IsImmediate_ClearsTheTarget_AndLosesTheOverflow` (`ResearchEngineTests.cs:229-244`);
- `NoTarget_RpReachesNoNode_AndNothingIsBanked` (`:193`).

**Findings:**

**G-01 — OPEN DECISION.** Whether completion overflow may carry to the next target is still recorded as a
Director decision:
- in D-046 Part D item 2 (`d046-research-foundation-gate-rulings.md:390-391`);
- in ADR-029 addendum A, row R-2 (`adr-029-research-engine.md:650`).

The code implements "lost", which is consistent with ledger §5.5. The ledger does not, however, state the
completion case in so many words. The Director's conversational statement 2.2 (FREEZE: "Unspent RP after
completing the active technology is LOST") would close G-01. It is pending recording (§4).

**G-02 — STALE DOCUMENTATION (minor).** ADR-030 §2 item 3 says *"overflow into the next target"*
(`adr-030-per-turn-research-points.md:31`), which reads as a carry. ADR-030's own §7 disclaims that reading
(`:87-89`). Resolve it together with G-01.

### Area 3 — one active target — **CONFORMS**

**Ledger:** §5.3.

**Implementation:**
- One target row per polity spans both trees.
- The target is set only by the `SetResearchTarget` order (`Sim.Core/State/WorldState.cs:801`).

**Test:** `SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp` (`ResearchEngineTests.cs:548`).

**Records:** D-044 R9. The D-042 §9.3 note is HISTORICAL (ledger R4.2).

### Area 4 — progress retained per node — **CONFORMS**

**Ledger:** §5.4.

**Implementation:**
- Progress is held in `ResearchProgressRow(PolityId, ResearchNodeId, double Progress)` (`WorldState.cs:816`).
- Completion removes only the completed node's row.

**Test:** `Switching_KeepsPartialProgressOnEveryNode_TheSixtyOfAHundredRule` (`ResearchEngineTests.cs:207-225`).
It pins a scaled equivalent of the ledger's 60/100 example.

### Area 5 — Eureka rules — **CONFORMS**

**Ledger:** §8.4.

**Implementation:**
- **Credit** is weight × BaseCost.
- **Weights.** The default is 0.40 ÷ N; explicit weights are allowed but must be given for all of a node's
  Eurekas or none. A node's total must not exceed the ceiling.
- **Target-independent.** Credits run for every AVAILABLE node before throughput, whatever the target.
- **Once only.** Each condition fires once.
- **Reconciliation.** The last condition to fire reconciles to the exact entitlement (ADR-029 A.4 R-23).

**Tests:** `FullEureka_IsFortyPercentOfBASE_AUniversityStyleModifierDoesNotShrinkIt`
(`ResearchEurekaCreditTests.cs:227`), and the equal-share, explicit-weight and idempotence tests in the same file.

**G-03 — HISTORICAL / SUPERSEDED.** ADR-029 §5 step 2 still reads *"eurekaCreditFraction × EffectiveCost"*.
Addendum A supersedes it under its own precedence clause, so no change is needed (ledger R4.4).

### Area 6 — EffectiveCost and the Eureka ceiling — **CONFORMS**

**Ledger:** §8.1–§8.3.

**Implementation:**
- **EffectiveCost** = max(0.20 × BaseCost, BaseCost × Π modifiers). The floor and ceiling fractions live in
  `research.json` `tuning`.
- **One pool, 0.40 × BaseCost,** shared by Eureka and foreign exposure, and capped at the remaining EffectiveCost.
- **Provenance per source** is kept in `ResearchCreditRow(Polity, Node, Source, Amount)` (`WorldState.cs:844`).
- **No modifier source exists yet.** The university modifier is DEFERRED (§9, §27).

**Test:** `CreditOnlyCompletion_WhenCreditCoversTheRemainingEffectiveCost_TheNodeCompletesWithNoRpSpent`
(`ResearchEurekaCreditTests.cs:256`).

### Area 7 — the Research → Knowledge → Capability chain — **CONFORMS for what exists**

**Ledger:** §4.1, §21 and §25.

**Implementation:**
- **Knowledge.** Completion appends `ResearchCompletedRow(PolityId, ResearchNodeId)` (`WorldState.cs:826`).
- **Capabilities** are read-only queries.
- **Eligibility is not availability.** `IsKnowledgeEligible` says so, and *"no system consumes this answer yet"*
  (`ResearchQuery.cs:526-532`).
- **The matrix** has zero B (TECHNOLOGY-OWNED) items by design (`research-capability-ownership.md:31`, `:45`).

**G-39 — OPEN DECISION (labels only).** The Director's conversational Realization Architecture brief
(2026-10-01) and the ledger §25 name the stages differently:

| source | stages |
|---|---|
| Realization brief | Research → Knowledge → Capability → Realization → Adoption → Widespread Adoption → Civilization-wide effects |
| ledger §25 | Knowledge/Technology → capability → technique/practice → institutionalization → physical realization → application → civilization effect |

- The two are not contradictory. Ledger §25 adds *"Not every domain requires every layer."*
- The ledger governs. The brief is pending recording (§4).
- Nothing downstream of capability is built, which is DEFERRED (§27: "detailed capability realization").

### Area 8 — research completion does not construct — **CONFORMS**

**Ledger:** §4.1.

**Implementation:** *"Completion builds nothing (D-044 R14)"* (`ResearchSystem.cs:48-50`).

**Test:** `KnowledgeEligibility_FollowsTheRequirement_AndUnlocksNothingPhysical` (`ResearchEngineTests.cs:613`)
asserts zero `Structures` and zero `ConstructionQueue` rows after completion (`:622-623`).

### Area 9 — infrastructure realization boundaries — **OPEN**

**Ledger:** §4.1, §14, §21 and §25.

**Implementation:**
- **Content.** Entity requirements (`infra.road_*`, `building.harbour`, `infra.fish_weir`, …) are declarations only.
- **No simulation system** consumes knowledge eligibility. Only the `sim research` CLI reports it (`Sim.Cli/ResearchCli.cs:155`).
- **Construction** accepts any known project, with no technology predicate (`ConstructionSystem.cs:92-97`).
- **Paths.** PathBuild lays only `DirtPath` edges, and water nodes are impassable
  (`Sim.Core/Pathing/TraversalLattice.cs:14-15`).

**Records:**
- ADR-028 §3 (MAIN): the lifecycle LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE →
  SATURATED/DIMINISHING, and "technology-unlocked" as a valid gate.
- D-044 R11 and R14.

**Findings:**

**G-04 — OPEN DECISION.** D-044 Part D T3 is UNRESOLVED (`d044-research-progression-rulings.md:841`). It asks
whether a node may make a transport or infrastructure *class* available, against D-040 B3, "NO TECHNOLOGY
UNLOCK" (MAIN).
- **The ledger supports the "available" reading.** §4.1 makes completion grant capability, and §21 defines
  TECHNOLOGY-OWNED. It names neither T3 nor B3, however (ledger R4.12).
- **What is blocked on it.** The matrix rows for roads, bridges, canals, railways and coastal and ocean shipping
  (`research-capability-ownership.md:79`, `:87`).
- **What would close it.** Conversational statement 2.6 ("Technology MAY unlock a new CLASS …"), pending
  recording (§4).

**G-05 — OPEN DECISION.** Frozen D-009 calls bridges and tunnels *"expensive, era-gated, terrain-crossing
edges"* (`d009-d010-map-population-addendum.md:12`). That wording stands against Law 4.
- CR-009, which would have addressed it, was never written (`docs/current-state.md:13`).
- Resolving G-04 narrows this tension but does not repair the frozen wording.

**G-06 — IMPLEMENTATION GAP, DEFERRED.** No availability path consumes knowledge eligibility
(matrix §5 item 8, `research-capability-ownership.md:85`). It belongs to the first packet that needs one
(ADR-028 §4).

### Area 10 — two research trees — **CONFORMS**

**Ledger:** §3: "Tree 1: Technology · Tree 2: Civics".

**Implementation:**
- `research.json:84-94` names the trees "Technology" and "Civics".
- The loader requires exactly `[technology, civics]`, pinned by `Rejects_InvalidTreeIds`.

**Note.** Conversational statement 2.9 named Tree 1 "Knowledge & Technology". The ledger, the later and
authoritative text, says "Technology", and the repository already matches it. No change is proposed
(ledger R4.22).

### Area 11 — five technology branches — **CONFORMS**

**Ledger:** §3.

**Implementation:**
- `research.json` `branches` lists exactly military, medicine, engineering, natural_science and agriculture, numbered 1.1–1.5.
- The loader requires them in that order.
- Node counts (measured) are below. The trunk is D-044 R3's Main Technology graph, and the five subtrees open
  at the research stage (D-044 R4).

| trunk | military | medicine | engineering | natural science | agriculture | civics |
|---|---|---|---|---|---|---|
| 176 | 37 | 22 | 130 | 44 | 15 | 6 |

**Notes:**
- No Industry & Energy branch exists (§3, §26 item 22).
- The ledger is silent on the trunk and research-stage split, which does not conflict with it.

### Area 12 — seven lenses — **STALE DOCUMENTATION · OPEN**

**Ledger:** §3: KNOWLEDGE, TECHNIQUES, INSTITUTIONS, INFRASTRUCTURE, INDUSTRY, MILITARY and APPLICATIONS, as
lenses and not trees.

**Implementation:**
- Lenses are views; `Sim.Core` has no lens concept.
- The UI content on UNMERGED-B holds exactly these seven lenses (`Sim.Ui/UiContent/trees/the-trees.json`,
  measured).

**Findings:**

**G-07 — STALE DOCUMENTATION.** Architecture §3.3 lists six lenses, with Technology and Civics as trees
(`docs/design/civilization-progression-architecture.md:45`).
- §16.1 calls the seven-lens UI "non-authoritative" (`:278`).
- §4 and §18, "Eight domains", are not lenses and may stand (ledger R4.7–R4.9).
- The architecture says there is no Knowledge *domain, resource or tree* (§4.1). That fits the ledger, whose
  KNOWLEDGE is a lens.

**G-08 — OPEN DECISION.** Which lens surfaces Tree 2 (Civics)? The ledger does not say. Conversational
statement 2.8 named lens 1 "KNOWLEDGE & TECHNOLOGY", and the ledger governs (KNOWLEDGE).

### Area 13 — Age structure — **RATIFIED · IMPLEMENTATION GAP**

**Ledger:** §10: nine Ages, A1–A9.

**Implementation:** node `age` metadata uses exactly A1–A9. No node carries `F` (measured). The distribution
over all 430 nodes:

| A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| 28 | 51 | 36 | 32 | 32 | 42 | 32 | 53 | 124 |

**Findings:**

**G-09 — IMPLEMENTATION GAP, DEFERRED.** No Age state exists: *"Age is never read"* (`ResearchSystem.cs:50`).

**G-10 — IMPLEMENTATION CONFLICT (UNMERGED-B).** `Sim.Ui/UiContent/trees/ages.json` holds six placeholder Ages,
`AGE_00` to `AGE_05` (measured; D-043 F25).

**G-11 — STALE DOCUMENTATION.** Architecture §12.7 records frontier nodes in an age `F` as a defect
(`civilization-progression-architecture.md:243`). The migrated corpus has no `F`.

### Area 14 — the five Age milestone categories — **RATIFIED · OPEN**

**Ledger:** §10.4: Technological, Material-Economic, Institutional-Social, Systemic and Military Realization.

**G-12 — RATIFIED DIRECTOR DECISION.** It is now recorded in the repository, in the ledger itself.
- Before this pass, no file on any ref named the five categories (ledger R6.2).
- D-043 A2 (UNMERGED-B) rules the structure (core plus supporting milestones, with category coverage). It gives
  example categories (military, economic, institutional, infrastructure, knowledge;
  `d043-civilization-progression-ages-and-mobile-agents.md:97-107` and F27 `:1063-1064` there) but rules no list,
  and its examples do not match the five.

**G-13 — IMPLEMENTATION CONFLICT (UNMERGED-B).**
- **Categories.** `ages.json` has four provisional categories and no Military Realization.
- **Schema.** The milestone schema has one boolean `mandatory`, one `category` and `prerequisites`. It cannot
  express coverage or a sufficient count (D-043 F27).

**G-14 — OPEN DECISION: AGE-MS-1.** Who owns and evaluates Age milestones (ledger R6.3).
- **Distinct from CR-005.** CR-005 is the development-milestone placement question. It is OPEN on MAIN and
  PR #10 and is not touched. The unmerged `m5-full-build` carries a copy marked *"RULED 2026-09-05 — OPTION C
  ACCEPTED"* (`cr-005-m5-research-technology-institutions-placement.md:3` there), which D-044 T1 already records
  (`d044-research-progression-rulings.md:839`).
- **Conversational input.** Statement 2.21, headed "CR-005 milestone ownership", maps milestone ownership by
  system. It is AGE-MS-1 input, pending recording.
- **DEFERRED.** Milestone lists and thresholds (§27).

### Area 15 — Age transition rules — **RATIFIED · NOT YET IMPLEMENTED · OPEN**

**Ledger:** §6.1, §10.1, §10.2, §10.4 (partial progress has no effect), §10.5 and §11.

**Implementation:**
- **Age is not a gate:** research never reads Age (`ResearchSystem.cs:50`; D-044 R13).
- **Continuity:** no code removes a completed-knowledge row (measured).
- **Not built:** irreversibility, simultaneity, transition and surge, because no Age state exists.

**G-15 — OPEN DECISION.** CR-017 (UNMERGED-B, OPEN) holds the frozen-law collisions:

| CR-017 § | collision |
|---|---|
| §1 | Age as an input, against Law 4 |
| §2 | the surge's residual, against Law 2 |
| §4 | automatic free modernization in D-043 A8, against Law 1, Law 3 and D-011 |

- **DEFERRED:** the surge formula and duration (§10.5, §27). The +20% figure is only a CALIBRATION starting target.
- **The ledger is silent on D-043 A8's modernization,** which stays with CR-017 §4.

### Area 16 — global dt rules — **OPEN · IMPLEMENTATION GAP**

**Ledger:** §10.3.
- dt is globally consistent within a global Age cycle.
- A new dt takes effect at the next global cycle boundary.
- dt is immutable through a player cycle.
- The values are DEFERRED.

**Implementation:**
- **One dt per step,** keyed by the calendar: `_eraTable.DtDaysAt(prev.Clock.SimDays)` (`Sim.Core/Kernel/TurnExecutor.cs:86`).
- **Seven calendar bands,** 10 / 5 / 3 / 2 / 1 / 0.5 / 0.5 years (`Sim.Data/content/era-pacing.json`).
- **Immutable within a cycle.** This holds trivially, because one step processes every polity under one dt.

**Findings:**

**G-16 — OPEN DECISION.** Binding the frozen dt rules to Ages is a frozen-item question (ledger R4.14). The
frozen texts are Spine S3 (`civ-sim-architecture-v3-outline.md:34`) and D-006 (`m0-kernel-spec.md:13`, `:25`).
- Spine S3 keys global dt to the most advanced polity's **era band**. D-006 indexes on world date until M6.
- **Where it is held.** Architecture §19 item 7 (OPEN; MAIN and PR #10), and DD-T7 on UNMERGED-B.
- **A CR is required before any code.** No CR covers the binding; CR-019 is the next free number (not minted).

**G-17 — HISTORICAL / SUPERSEDED IN PART.** ADR-019 §1.3 records *"dt CHANGES TAKE EFFECT IMMEDIATELY … RECORDED
AS A GAP"* — the Director's earlier preference for a change "at once with an explicit player notification".
- Ledger §10.3 now rules the timing: the next global cycle boundary, with dt immutable through a cycle.
- The notification is not ruled.
- An append-only note is owed on ADR-019.

### Area 17 — baseline capabilities — **IMPLEMENTATION CONFLICT (content) · CONTENT GAP**

**Ledger:** §21, §26 item 20, and §28 items 8–9.

**Implementation:**
- **Six baseline entries:** `research.json:46-83` lists settlement_founding, exploration, basic_military,
  food_gathering, construction and migration.
- **Granary and workshop** have null requirements and are constructible with zero completed nodes (pinned by
  `ResearchContentTests.Baseline_GranaryAndWorkshop_AreConstructibleWithZeroTechnology`, `:270`).
- **Sectors run research-free:** farming, herding and fishing, extraction, crafting and construction.
- **The matrix** classifies 48 items as A BASELINE (`research-capability-ownership.md:45`).

**Findings:**

**G-18 — IMPLEMENTATION CONFLICT (content).** Five activity entities gate founding activities behind nodes:

| entity | line | requires |
|---|---|---|
| `activity.farming` | `research.json:29001-29004` | six crop nodes |
| `activity.herding` | `:29008-29011` | `sheep_goat OR cattle` |
| `activity.fishing` | `:29015-29018` | `fishing_hook` |
| `activity.logging` | `:29022-29025` | `ground_stone_early` |
| `activity.mining` | `:29029-29032` | `mining_shaft` |

- **They are inert,** because nothing consumes eligibility.
- **They still contradict** ledger §21, the matrix's own A classification of these five activities (§3 table)
  and M5 gate condition 9.
- **The fix needs a Director decision** (matrix §5 item 1, `:76`). Conversational statement 2.4 would decide
  it; it is pending (§4).

**G-19 — IMPLEMENTATION CONFLICT (content).** Node capability and technique strings claim baseline
capabilities (matrix §5 item 3, `:78`). The node-by-node review is in §3.

**G-20 — STALE DOCUMENTATION (content text).** Baseline `providedBy` texts misdescribe the code (matrix §5 item 2, `:77`).
- `baseline.food_gathering` names a `HarvestSystem` that does not exist.
- The settlement-founding and construction texts are also wrong.

**G-21 — CONTENT GAP.** The baseline list has no entry for shelter, basic tools and implements, basic watercraft,
or basic paths and transport.
- D-045 §1 permits "other minimum capabilities".
- `ResearchBaselineTests` uses a contains-check, so additions would break nothing.
- This is optional, and the Director decides.

### Area 18 — medicine families — **CONTENT REVIEW · TEST GAP**

**Ledger:** silent on families.
- **Governing records:** D-044 R7 (generations are local, and none are connected automatically) and D-046
  Group A (*"medicine and messaging restructured"*).
- **Pending:** conversational statement 2.11.

**Implementation (measured):**
- **Penicillin.** `antibiotic_penicillin` (1943 CE) requires `germ_theory AND fermentation_grain`. Its whole
  prerequisite closure contains no surgery-family node.
- **Families.** The local family sequences are surgery 1–6, antibiotics 1–3, diagnostics, and so on.

**Findings:**

**G-22 — CONTENT REVIEW.** Edges to judge:
- `anaesthesia` (1846, surgery 4) requires `chemistry_quantitative AND anatomy_dissection`.
- `antisepsis` (1867, surgery 5) requires `germ_theory AND anaesthesia`. The surgery-family links read as a ladder.
- `xray` (1895) requires `vacuum_tube` (1906), a later invention (§2, area 30, G-34).
- `clinical_medicine` is a generic family name: `medicine_recorded` (gen 1) and `medicine_hippocratic` (gen 2).

Any edit to a prerequisite moves `depth`, possibly the cost rationale, the generated audit and the goldens.

**G-23 — TEST GAP.** Nothing pins "no surgery-family node in `antibiotic_penicillin`'s prerequisite closure".
The data conforms today.

### Area 19 — messaging and communications families — **CONTENT REVIEW**

**Ledger:** silent.
- **Governing records:** D-044 R7 and D-046 Group A, which dismantled the v0.6 single "messaging" chain.
- **Pending:** conversational statement 2.12.

**G-24 — CONTENT REVIEW, not established as defects.** Two edges to review:
- `vacuum_tube` (1906) requires `electric_light AND radio`.
- `radio` (1895 / 1920) requires `electromagnetism AND telegraph`.

Both edges are defensible on causal grounds: Fleming's valve was built as a radio detector, and radio
telegraphy grew out of telegraphy. Only the Director can rule them out. The `computer` ← `vacuum_tube` edge is
justified in the corpus and stays.

### Area 20 — university relevance — **CONFORMS · one pending question**

**Ledger:** §9: relevance is a domain classification only, and the formula is not ratified.

**Implementation (measured):**
- **One primary each.** Every technology has exactly one primary relevance (424); there are 181 secondary
  relevances.
- **Civics** have none.
- **Five types,** bound one-to-one to the five branches.
- **Validation.** The loader checks type, name and role and rejects duplicates (`ResearchContent.cs:783-790`).
- **No number** appears anywhere.
- **No university system exists.** D-044 T4 makes the first packet that writes a university modifier owe the
  D-021 brake.

**G-25 — OPEN DECISION.** Conversational statement 2.17 says relevance must not be restricted to the five
labels. The ledger's "Specialized universities **include**" permits more, but the loader binds each type to a
branch. Nothing conflicts until a sixth type is wanted.

### Area 21 — Eureka authoring status — **CONFORMS · CONTENT GAP**

**Ledger:** §8.5 and §26 item 27.

**Implementation (measured):**
- **81 Eurekas on 80 nodes.** `railway` has two, at 0.2 each.
- **Source:** 73 authored and 8 inherited.
- **Evaluable today:** 18. Fifteen read knowledge state; three read goods stocks (`copper_smelting` and
  `arsenical_bronze`: `stock_copper_ore > 0`; `tin_bronze`: `stock_tin_ore > 0`). The other 63 are future-system
  conditions, which are DEFERRED by design:

  | future system | Eurekas |
  |---|---|
  | resources/terrain | 34 |
  | world events | 20 |
  | institutions | 6 |
  | goods state | 1 |
  | world state | 1 |
  | construction state | 1 |

- **Kinds:**

  | kind | count |
  |---|---|
  | environmental | 38 |
  | world event | 21 |
  | precursor technology | 8 |
  | institutional | 6 |
  | precursor demand | 4 |
  | construction | 1 |
  | institutional precursor | 1 |
  | environmental exposure | 1 |
  | exposure | 1 |

- **Reachability is enforced:**
  - implied conditions: `Rejects_ImpliedEurekas_AConditionEveryPathToTheNodeAlreadySatisfies` (`ResearchContentTests.cs:476`)
    and `Canonical_NoEvaluableKnowledgeEureka_IsGuaranteedByItsOwnNodesPrerequisites` (`:156`);
  - dead conditions: `Rejects_InvalidEurekaReferences_UnknownNames_AndDeadConditions` (`:445`).

**G-26 — CONTENT GAP.**
- **(a) Generic justifications.** The 8 inherited Eurekas carry boilerplate rather than node-specific
  accelerator logic: *"inherited; the resource is distinctive …"* or *"inherited event condition …"*. They are on
  adhesive_natural, arsenical_bronze, glass_glaze, greek_fire, sulphuric_acid, antiviral_drugs,
  catalytic_converter and stealth. §8.5 asks for review.
- **(b) Goods-stock conditions.** Three evaluable conditions read a realization material, which §8.5 and §26
  item 27 allow only with a justification. `copper_smelting` and `tin_bronze` are authored with one.
  `arsenical_bronze`'s condition, `stock_copper_ore > 0`, is inherited, carries the generic justification, and
  is broader than its text ("arsenical copper ore (fahlore)"). It is a review case.
- **Not a finding.** The one `construction` Eureka (`road_paved`, "Built roads already exist") is authored with a
  justification, which §8.5 permits.

### Area 22 — knowledge diffusion — **DEFERRED (the seam conforms)**

**Ledger:** §12. The implementation is DEFERRED (§27).

**Implementation:**
- **The seam** is `ResearchExposureRow(Polity, Node, Offered)` (`WorldState.cs:855`), schema v26.
- **Read** at `ResearchQuery.cs:431`; **written by no system** (measured).
- **Credits** share the 40% pool.
- **The isolation rule holds trivially,** because nothing writes exposure.

**Not built:** pathways, contact, the exposure-data fields of §12, and the distinction between discovery,
contact, understanding, adoption and realization. These are DEFERRED.

### Area 23 — MobileAgent — **NOT YET IMPLEMENTED · OPEN**

**Ledger:** §17.

**Implementation:** no MobileAgent, Action Capacity, Army or War Pulse type exists in `Sim.Core` or `Sim.Ui`
(measured).

**G-27 — OPEN DECISION, plus a record gap.** Apart from D-045 §1's *"Do NOT create a Builder MobileAgent"*
(`d045-research-calibration-rulings.md:55`, PR #10), the MobileAgent rulings exist in the repository only as
D-043 B1–B6 and CR-017, both on UNMERGED-B. CR-017 holds these collisions open:

| CR-017 § | collision |
|---|---|
| §3 | Action Capacity per turn, against Law 3 |
| §7 | special people, against Law 1 and D-010's notables |

The ledger now records the rulings themselves. Bringing D-043 and CR-017 onto MAIN is a branch decision for
the Director.

**G-36 — REFERENCE ISSUE (content text).** `research.json:56` says `baseline.exploration` is *"not yet
simulated — no mobile agents exist (D-043)"*. It cites a record absent from this branch.

### Area 24 — military movement and War Pulses — **OPEN · NOT YET IMPLEMENTED**

**Ledger:** §18 and §19.

**Implementation:**
- **No armies.** The M4 exit certified *"No AutoResolver and no armies"* (`docs/milestones.md:326`).
- **Lattice pathing, land only.** Flow pathfinding (catchments, distances, PathBuild routing) runs on the
  terrain lattice, with network edges as fast lanes (`Sim.Core/Pathing/TraversalLattice.cs:8-10`;
  `Sim.Core/Pathing/Pathfinder.cs:43`, `:313-329`). It already leaves the roads over land. Positions are discrete
  lattice nodes, water-majority nodes are impassable, and boats are a later milestone (`PathBuildSystem.cs:42-45`).
- **No War Pulse** exists.

**Findings:**

**G-28 — OPEN DECISION.** Two parts of the ledger collide with frozen items. Both stay OPEN.

| ledger | collides with | where it is held |
|---|---|---|
| §18: off-road movement at continuous x/y, ships on open water | frozen D-009/D-010 (one network) and D-040 B4 (MAIN) | CR-017 §5 |
| §19: War Pulses that cross turn boundaries, with full operational control | the kernel contract §3.2/§3.4, the sub-step rule and D-011 | CR-017 §6, ruled together with CR-006 §1 |

The shipped lattice already allows off-road paths over land. So for today's code, the live §18 divergences are
continuous x/y positions and open-water movement, not road-only movement. The frozen D-009/D-010 collision
concerns MobileAgent movement on the network graph (CR-017 §5).

**G-29 — HISTORICAL / SUPERSEDED IN PART.** Two older texts give way to ledger §19, the later Director
ruling:
- D-043 B8's "inside the strategic turn" (ledger R4.10).
- D-039 E3's *"the player does not play out months of movement"* (MAIN, `d039-command-fog-and-siege.md:143-144`),
  as applied to War Pulses. Ledger §19 and §26 item 18 forbid a blanket prohibition of operational control during
  War Pulses.

An append-only note is owed on D-039. If the Director treats post-freeze D-records as frozen (an open
question, Q-73), a CR is owed instead.

### Area 25 — infrastructure viability and saturation — **NOT YET IMPLEMENTED · CONFORMS**

**Ledger:** §14. ADR-028 §1–§2 is RATIFIED on MAIN.

**Implementation:**
- No infrastructure or institution viability code exists. The only viability code is migration's destination
  viability (ADR-012).
- No universal cap exists anywhere, so nothing contradicts §14.

**G-30 — STALE DOCUMENTATION: an identifier collision.**

| record | location | DD-13 means | DD-14 means |
|---|---|---|---|
| ADR-028 | MAIN | type-specific viability | saturation |
| D-043 | UNMERGED-B | generic Age effects | the Age surge model |

Ledger R4.16 records the collision and a proposal; nothing was renumbered.

### Area 26 — institution lifecycle — **NOT YET IMPLEMENTED · OPEN**

**Ledger:** §13.

**Implementation:**
- **No institution system** exists.
- **The research stage is a knowledge proxy.** It is a predicate over knowledge, whose `_doc` says
  institutional presence is not evaluable (`research.json:128-131`).
- **Institution entities** in `research.json` are declarations. The matrix counts 28 institutions: 27 C and 1 A.

**Records:**
- ADR-028 §3's lifecycle is consistent with ledger §13's pipeline. The ledger's "construction → founded →
  specialization → maturation" maps onto ADR-028's UNDER_CONSTRUCTION → ACTIVE → MATURE (INFERRED).
- `docs/design/arch-FGH-civics-institutions-diffusion.md` is design input.

**G-31 — OPEN DECISION.**
- CR-010, the definition of an institution, was never written (`docs/current-state.md:13`).
- The D-021 brake is owed by the first packet that writes a university modifier (D-044 T4).

### Area 27 — construction capacity is not banked — **CONFORMS (M4-D) · IMPLEMENTATION CONFLICT (PathBuild)**

**Ledger:** §15.

**Implementation:**
- **`ConstructionSystem` conforms.** Capacity is construction share × adults × `dtYears`
  (`ConstructionSystem.cs:51`, `:137`), with *"no partial draw, no banked progress"* (`:40-41`).
- **`PathBuildSystem` banks.** Unspent path labour accrues in `PathProgressRow.Banked`
  (`WorldState.cs:381-385`; `PathBuildSystem.cs:51-52`, `:157`, `:207`, `:216`). The ratified M3 packet spec
  says *"PathBuild banks the remainder"* (`docs/t3.8-spec.md:44-45`).
- **Not banking (INFERRED, as D-043 F19 reads them).** `HousingSystem`'s whole-unit `BuildRemainder` and D-004's
  remainders round a *used* flow.

**G-32 — IMPLEMENTATION CONFLICT, pending an OPEN DECISION.** D-043 F19 (UNMERGED-B) records it as a DIRECT
CONFLICT. The Director must choose between two readings:
- **(a)** The path-labour accumulator is banked construction capacity, which §15 forbids. The fix is a code,
  schema and golden change that needs its own packet and ADR, because T3.8 is a ratified spec.
- **(b)** It is per-project progress toward the next segment. Ledger §15 and D-043 Part G (F42) leave that case
  unaddressed.

### Area 28 — research-node immediate effects — **CONFORMS**

**Ledger:** §23.

**Implementation:** the loader rejects a non-empty `effects.immediate` (`ResearchContent.cs:325-326`).

**Test:** `ResearchContentTests.cs:672-673`.

### Area 29 — repeatables — **CONFORMS · DEFERRED**

**Ledger:** §22.

**Implementation:**
- **Ten nodes** are in `researchSets.recursive`. Each holds its availability as data: the subtree, and the
  number of finite nodes to exhaust.
- **The loader checks the set,** and availability requires the subtree to be exhausted (`ResearchQuery.cs:99-116`).
- **Each completes once.** Levels are not implemented (`ResearchContent.cs:137-142`).

**Test:** `Canonical_RecursiveSet_TheTenRepeatables_WaitForTheirOwnSubtreesFiniteResearch` (`ResearchContentTests.cs:283`; the counts are asserted at `:292-295`).

**G-38 — latent; DEFERRED.** `frontier_medicine` waits on all 21 finite medicine nodes, including ones that are
not causal prerequisites of biomedical research. This binds only once repeat mechanics exist (D-046 G4).

### Area 30 — known content, prerequisite and reference issues

**G-33 — IMPLEMENTATION CONFLICT, pending an OPEN DECISION.** The ledger and the loader disagree on NOT.
- **The ledger.** §6 says *"Prerequisites may contain AND/OR/NOT logic."*
- **The loader** refuses NOT in a prerequisite: *"so completing knowledge can never make a node LESS available"*
  (`ResearchContent.cs:387-389`). It refuses NOT in the research-stage predicate too (`:430`).
- **The records.** D-044 R8 names AND, OR and nested AND/OR only (`d044-research-progression-rulings.md:262-282`).
  ADR-029 §2.2 (`:71`; PROPOSED) refuses NOT so that availability stays monotone.
- **Where NOT is already accepted.** Eureka conditions and entity requirements.
- **The Director's choice:**
  - **(a)** NOT is allowed in prerequisites. That needs an ADR, because availability could then decrease.
  - **(b)** §6's NOT refers to the predicate dialect generally.

**G-34 — CONTENT REVIEW: chronology candidates.** These are measured with a heuristic and are not established
defects.
- **The test.** A required (AND) prerequisite whose own recorded emergence is later than its dependent's,
  among dependents dated after 3000 BCE. Each node's date is the earliest date in its `emerged` text. Only
  dates with an explicit era marker (CE, BCE, kya or Mya) count; the 83 nodes whose text has none were not tested.
- **The result.** 11 hits. One is negligible: `jet_engine` 1939 ← `superalloy` ~1940.
- **Why they matter.** Architecture §6.2 (RATIFIED) makes prerequisites causal, never chronological. A
  prerequisite recorded as later cannot have been the historical cause, unless the date or the edge is wrong.

The ten:

| dependent | key | dated | requires | dated |
|---|---|---|---|---|
| `bill_of_exchange` | 214 | ~1150 CE | `double_entry` | ~1300 CE |
| `alchemy` | 173 | ~200 BCE China | `glass_blowing` | ~50 BCE |
| `standard_weights` | 105 | ~2600 BCE | `surveying` | ~2500 BCE |
| `seed_drill` | 157 | ~200 BCE | `iron_mouldboard` | ~100 BCE |
| `torsion_artillery` | 142 | ~340 BCE | `mechanics_archimedean` | ~250 BCE |
| `canning` | 330 | 1810 CE | `germ_theory` | 1860 CE |
| `glass_lead` | 246 | 1674 CE | `coke` | 1709 CE |
| `cotton_gin_saw` | 265 | 1793 CE | `milling_machine` | ~1818 CE |
| `cannon_cast_bronze` | 224 | ~1400 CE | `corned_powder` | ~1420 CE |
| `xray` | 336 | 1895 CE | `vacuum_tube` | 1906 CE |

**Read with bare years as CE,** five more appear:
- `genome_sequencing` (1977) ← `pcr` (1983);
- `superconductivity_applied` (1911) ← `quantum_mechanics` (1925);
- `precision_agriculture` (1990s) ← `gps` (1995);
- `combat_drone` (1990s) ← `gps` (1995);
- `cmos_vlsi` (1970s) ← `microprocessor` (1971).

`genome_sequencing` ← `pcr` is the same kind of candidate as `xray` ← `vacuum_tube`. The three decade-dated
texts sit within their own date precision.

**G-35 — STALE DOCUMENTATION.** The matrix row at `research-capability-ownership.md:1921` mis-anchors two unlock
lists:
- carrack's is anchored as polynesian_canoe's `.secondaryDomains`;
- polynesian_canoe's is anchored as celestial_navigation's `.prereq`.

The correct anchors are `research.json:14366` and `:14430`.

**G-36** is under area 23.

**G-37 — STALE DOCUMENTATION (comment).** `ResearchQuery.cs:531-532` justifies "no node opens sea travel" by
D-040 B3. That is correct while T3 is OPEN. Re-word it only after G-04 is ruled.

**Group B pathways (§6.2) — CONFORMS.**
- `GroupB_AWorldWithOnlyChineseScript_ReachesTheResearchStage` (`ResearchContentTests.cs:214`);
- `GroupB_CuneiformFreePaths_ReachTheGenericRecordKeepingNodes` (`:229`);
- `GroupB_TheBabylonianNodes_StillRequireCuneiform_SpecificDependenciesKept` (`:237`).

---

## 3. Baseline content review (Step 7)

**Scope:** the farming, herding, fishing, logging and mining gates; founding-era tools; basic watercraft;
coastal shipping; and the road-tier nodes.

**How to read the table:**
- **Claim class** — what the node's strings or entity links claim:
  - **BASELINE CAPABILITY** — it exists at founding with zero research (simulated or matrix class A);
  - **ADVANCED IMPROVEMENT** — an improvement over a baseline capability;
  - **TECHNOLOGY-OWNED CAPABILITY** — a new capability that legitimately requires the node;
  - **UNRESOLVED** — the Director must classify it.
- **Handling** is PROPOSED and not applied. It is a Director content decision (D-046 F§1).
- **No node is deleted.**
  - Keys are stable (`research.json:5`).
  - The finite and subtree counts are pinned (`ResearchContentTests.cs:292-295`).
  - A node left with no strings and no dependents is rejected by the loader as an orphan.

| node or entity (key) | line | claims | claim class | handling (PROPOSED) |
|---|---|---|---|---|
| **farming** — `activity.farming` | `:29001-29004` | requires one of six crop nodes | the activity is **BASELINE** (sector `farming`, matrix A) | Null `requires`, and remove the entity from the six nodes' `unlocks.activities` (the loader's reverse-index rule) |
| `cereal_cultivation` (29) | `:1988` | "predictable harvest location", "larger stands" | **ADVANCED IMPROVEMENT** | Keep. Remove only the activity link |
| `root_crop` (35), `rice_wet` (31), `millet` (32), `maize` (33), `sorghum_pearl_millet` (34) | `:2415`, `:2127`, `:2206`, `:2277`, `:2356` | crop-specific staples | **ADVANCED IMPROVEMENT**: crop packages | Keep. Remove only the activity link |
| **herding** — `activity.herding` | `:29008-29011` | requires `sheep_goat OR cattle` | the activity is **BASELINE** (sector `herding/fishing`, matrix A) | Null `requires`; remove the reverse-index entries |
| `sheep_goat` (41) | `:2798` | "herding sector" (`:2828`), "meat on the hoof" (`:2827`); transhumance; dung fuel | "herding sector" is a **BASELINE** claim; the rest is **ADVANCED IMPROVEMENT** | Re-scope "herding sector" (5 dependents, so it is no orphan) |
| `cattle` (42) | `:2880` | traction, dairy, dung fuel | **TECHNOLOGY-OWNED CAPABILITY** | Keep. Remove only the activity link |
| **fishing** — `activity.fishing` | `:29015-29018` | requires `fishing_hook` | the activity is **BASELINE** | Null `requires`; remove the reverse-index entry (`:1551`) |
| `fishing_hook` (22) | `:1517` | "pelagic fishing" (`:1544`); the name and description are the hook itself | the capability is an **ADVANCED IMPROVEMENT**; whether the hook is basic equipment is **UNRESOLVED** | Keep. Re-scope the name and description to line or pelagic fishing. It has 0 dependents, so it survives only through its capability string |
| `cordage` (12): "nets", "snares", "Net making"; `basketry` (13): "fish traps"; `bone_tools` (26): "harpoons" | `:886`, `:952`, `:1803` | basic fishing and hunting equipment | **BASELINE** claims | Record as non-owning, or re-scope |
| `infra.fish_weir` | `:28581` | requires `basketry` | **UNRESOLVED**: is a weir basic fishing or improved infrastructure? The matrix says it must never gate baseline fishing | Director classifies |
| **logging** — `activity.logging` | `:29022-29025` | requires `ground_stone_early` | the activity is **BASELINE** (sector `extraction`; ADR-028 §4, `adr-028-viability-and-saturation.md:59`) | Null `requires`; remove `:639` |
| `ground_stone_early` (8) | `:614` | "felling", "heavy woodworking" (`:631-632`) | as written, **BASELINE** claims; the node is **ADVANCED IMPROVEMENT** (ground, resharpenable axes) | Re-scope the two strings (7 dependents) |
| **mining** — `activity.mining` | `:29029-29032` | requires `mining_shaft` | basic extraction is **BASELINE** (*"A mine is not a prerequisite for extraction"*, `adr-028:59`) | Null `requires`; remove `:5710` |
| `mining_shaft` (85) | `:5685` | "ore from depth", "sustained metal supply" | **TECHNOLOGY-OWNED CAPABILITY** (shaft mining) | Keep. Remove only the activity link |
| **tools** — `knapping_oldowan` (2) | `:232` | "cutting edges", "butchery", "bone-breaking for marrow" | **BASELINE** claims (stone tools) | Record as non-owning. Whether A1 nodes stay researchable is tied to CR-006 and D-044 T7 (OPEN) |
| `fire_making` (1) | `:167` | fire on demand; fire-hardening of wood, roasting and pit cooking, the thrown spear (`:194-197`) | **UNRESOLVED**: is fire baseline? Baseline cooking and wooden implements presuppose it | Director decides. If fire is baseline, re-scope the node to improved pyrotechnology (7 dependents) |
| `adhesive_natural` (9) | `:676` | "hafting" | **UNRESOLVED** (with `hafting`) | Director |
| `hafting` (11) | `:817` | "thrusting spear", "axe", "adze" (`:835-837`); gates `unit.spearmen` (`:28840`) | **UNRESOLVED**: are hafted tools basic, or improved composite tools? The founding unit's equipment depends on the answer (matrix `baseline.basic_military`) | Director |
| `bone_tools` (26) | `:1803` | "harpoons", "awls", "needles", "points" | **BASELINE** claims (basic implements) | Record as non-owning, or re-scope (2 dependents) |
| `digging_stick_hoe` (39) | `:2656` | "tillage", "weeding", "planting" | **BASELINE** claims (basic agricultural implements); the node is **ADVANCED IMPROVEMENT** (the hafted hoe before the ard) | Re-scope. `plough_ard` depends on it |
| `sickle` (38) | `:2592` | "efficient cereal harvest" | **ADVANCED IMPROVEMENT** (already efficiency-scoped) | none |
| `grinding_stone` (23) | `:1588` | "flour from wild grain", "processed tubers"; "Wild cereal harvesting", "Storage pit and cache", "Drying and smoking" | wild-cereal harvesting and basic food preparation are **BASELINE** claims; quern processing is an **ADVANCED IMPROVEMENT** | Re-scope, or record as non-owning |
| `copper_native` (63), `copper_smelting` (64) | `:4259`, `:4317` | small copper tools; cast copper tools; gates `building.smithy` | **TECHNOLOGY-OWNED CAPABILITY** (metal) | none |
| `arsenical_bronze` (80), `tin_bronze` (81) | `:5360`, `:5430` | bronze alloying, weapons and armour | **TECHNOLOGY-OWNED CAPABILITY** | Keep. Separately, the simulation's `bronze-casting` recipe has no knowledge gate (`goods.json:136-157`; it requires only `artisan_share > 0.05`), so whether recipes gain per-recipe knowledge gates is **OPEN** (matrix `:87`). No blanket recipe gate |
| `shelter_hut` (17) | `:1193` | "multi-season occupation", "storage inside" | **ADVANCED IMPROVEMENT** (a durable framed dwelling). Basic shelter is **BASELINE** (HousingSystem; matrix `stock.dwellings`) | Keep. Record cordage's "Windbreak and hide shelter" (`:917`) as non-owning |
| **watercraft** — `raft` (20) | `:1373` | "river crossing", "short sea crossing" (`:1390-1391`) | **BASELINE** claims (basic river craft) | Re-scope; do not empty (0 dependents and 0 entities, so the orphan rule applies) |
| `dugout` (21) | `:1438` | "river and lake transport" (`:1467`), "offshore fishing" (`:1468`), "Paddle" | river and lake transport is a **BASELINE** claim; offshore fishing is an **ADVANCED IMPROVEMENT** | Re-scope (3 dependents) |
| `plank_boat` (95) | `:6345` | "cargo vessels", "sea-going hulls"; gates `building.harbour` (`:28385`) | **TECHNOLOGY-OWNED CAPABILITY** | none |
| **coastal shipping** — `activity.coastal_shipping` | `:29043-29046` | requires `sail_square` | a gate on basic coastal craft: **UNRESOLVED** (D-044 T3, D-040 B3/B4, CR-017 §5; matrix options a/b/c, BLOCKED) | Director rules on T3 first |
| `sail_square` (94) | `:6265` | "upriver travel without rowing", "river trade at scale" (`:6295-6296`) | **TECHNOLOGY-OWNED CAPABILITY** (sail power). The river strings overlap the baseline river corridor | Re-word the river strings as improvements (3 dependents) |
| `activity.ocean_shipping` | `:29050-29053` | requires `carrack OR caravel OR polynesian_canoe`, all in the military branch | **TECHNOLOGY-OWNED CAPABILITY** (oceanic voyaging), formally **UNRESOLVED** under T3 and B3 | Director. Whether the ocean nodes belong in the military subtree is a separate review |
| **roads** — basic paths (`DirtPath`, PathBuild) | — | built from construction labour; reads no research | **BASELINE** (matrix `transport.overland_unimproved` A; `infra.dirt_path` C) | Optional test pin (§8) |
| `track_road` (74) | `:4982` | "reliable crossing of wetland", "path improvement" (`:5000`); unlocks `infra.road_track` (`:28476`), `infra.road_built` (`:28483`, with `stone_dry`) and `infra.courier_relay` (`:28504`) | "path improvement" is an incremental **ADVANCED IMPROVEMENT**; the wetland trackway is **UNRESOLVED** (T3) | Director |
| `road_paved` (150) | `:9969` | "all-weather bulk transport", "military mobility"; `infra.road_paved` (`:28490`) | **TECHNOLOGY-OWNED CAPABILITY**: an engineered road class, subject to T3 | Keep |
| `macadam` (272) | `:17734` | "coaching speed doubled" (`:17751`); `infra.road_macadam` (`:28497`) | an incremental-tier **ADVANCED IMPROVEMENT**, **UNRESOLVED**. Statement 2.6, "do NOT turn every incremental road improvement into a technology node", is pending recording | Director. Retiring the node would touch stable keys, the generated audit, pacing and the goldens, so it is not proposed |

---

## 4. The Director's conversational statements of 2026-10-01 — not yet recorded

**Status of every row: PENDING RECORDING. None is ratified by the ledger or applied by this pass.**
- **Why.** Ledger §30: a conversational decision is *"unsafe to rely on … permanently"* until it is recorded.
- **What may close.** The OPEN items listed below close only when the Director records the statement.
- **Source.** The Director's message of 2026-10-01, sections 2.1–2.21 and "CLAUDE CODE TASK: REALIZATION
  ARCHITECTURE FOUNDATION", as received (the message may have been truncated after 2.21).

| statement | relation to the ledger | repository items it would close or change |
|---|---|---|
| 2.1 RP terminology, per-turn RP, one pool, one target, 60/100, no general bank | contained in §5 | none (conforms) |
| 2.2 RP overflow FREEZE: lost | consistent with §5.5; the completion case is not stated in the ledger | **G-01**, G-02 |
| 2.3 completion grants knowledge and capability, not realization or adoption | contained in §4.1 | none (conforms) |
| 2.4 baseline activities, shelter, food preparation, hunting and gathering, implements | consistent with §21; not itemised in the ledger | **G-18**, **G-19**, G-20, G-21; matrix §5 items 1 and 3 |
| 2.5 basic tools baseline; bronze and later metallurgy technology-owned; no blanket recipe gate | not itemised in the ledger | §3 rows on tools; matrix "Blocked" item on recipes (`:87`) |
| 2.6 technology MAY unlock a transport or infrastructure CLASS; no incremental road nodes | not in the ledger; resolves D-044 T3 | **G-04**; `track_road` and `macadam` rows; matrix BLOCKED rows |
| 2.7 basic watercraft baseline; ships are MobileAgents | the ledger has §18 (ships on open water), not the baseline craft | `activity.coastal_shipping`, `raft`, `dugout`; G-28 |
| 2.8 seven lenses, with lens 1 "KNOWLEDGE & TECHNOLOGY" | **differs:** the ledger's lens 1 is KNOWLEDGE, and the ledger governs | G-07, G-08 |
| 2.9 two trees, with Tree 1 "Knowledge & Technology" | **differs:** the ledger's Tree 1 is "Technology", and the ledger governs | none (the repository matches the ledger) |
| 2.10 no Techniques tree; 38 candidates deferred | contained in §26 item 21 | none (conforms: D-045 §11) |
| 2.11 medicine families; surgery must not precede penicillin | not in the ledger | G-22, G-23 |
| 2.12 communications families | not in the ledger | G-24 |
| 2.13 generations local to a family | not in the ledger (D-044 R7 governs) | optional test (§8) |
| 2.14 Eureka 40% of BASE; target-independent; credit-only completion | contained in §8.2–§8.4 | none (conforms) |
| 2.15 multi-condition Eurekas | contained in §8.4 | none |
| 2.16 Eureka authoring rule | contained in §8.5 | G-26 |
| 2.17 university relevance not limited to five labels; no numbers | partly contained in §9 ("include") | G-25 |
| 2.18 no Age population ratified | contained in §7.4 | none |
| 2.19 Age → dt: values deferred; dt changes at the global cycle boundary | contained in §10.3 | G-16, G-17 |
| 2.20 repeatables deferred | contained in §22 | none |
| 2.21 "CR-005 milestone ownership": a mapping of milestone owners | **re-labelled** by the Director's 2026-10-02 instruction (CR-005 is not the Age-milestone decision). The content is **AGE-MS-1** input | **G-14**; CR-005 stays OPEN |
| Realization Architecture brief: Research → … → Widespread Adoption → effects; no single `TechnologyUnlocked` boolean | not in the ledger; §25 states a different chain of layers | G-39; not implemented (Step 5) |

---

## 5. Ledger §26 — the prohibited reintroductions (27-item check at `5e83e61`)

**None is present.** Each item was checked against the code, the content and the records.

| # | item | status |
|---|---|---|
| 1 | universal linear tree | absent: a graph with AND/OR prerequisites |
| 2 | separate research currencies per branch | absent: one pool |
| 3 | Cognitive Pool as a second currency | absent |
| 4 | general RP reserve | absent: no table holds unspent RP |
| 5 | manual percentage allocation | absent: one target |
| 6 | Age-gated technology availability | absent: Age never read |
| 7 | universal CapabilitySystem | absent (measured) |
| 8 | individual building objects on the map | not on this branch. The world layer on UNMERGED-B draws up to four reports per visual type per settlement individually (D-043 F37) |
| 9 | universal institution caps | absent |
| 10 | RP × `dtYears` | absent (ADR-030 code site) |
| 11 | reusable per-turn mechanism | absent (ADR-030 §3) |
| 12 | individual ownership of knowledge | absent: polity-keyed rows |
| 13 | immortal historical people | no MobileAgents exist. M4 notables are not historical catalogue people |
| 14 | mandatory nationality recruitment | absent |
| 15 | mandatory recruitment payment | absent |
| 16 | identical movement profiles | no MobileAgents |
| 17 | road-only movement | absent. No MobileAgents exist, and flow pathing already leaves the roads on the terrain lattice, with roads as fast lanes (area 24) |
| 18 | blanket no-control rule during War Pulses | no War Pulses exist. D-039 E3 is superseded in part (G-29) |
| 19 | repricing to hit Age durations | absent: the loader rejects calibration adjustments (`ResearchContent.cs:757-758`) |
| 20 | fake baseline research nodes | absent: the loader rejects a baseline id that is also a node |
| 21 | separate Technique tree | absent |
| 22 | Industry & Energy branch | absent |
| 23 | generic Age modifiers | absent |
| 24 | minimum RP spend | absent: credit-only completion is pinned |
| 25 | Eureka overflow | absent (capped) |
| 26 | Eureka from EffectiveCost | absent in code; ADR-029 §5 text is superseded (G-03) |
| 27 | mechanical Eurekas from realization materials | the authored set is justified. One inherited condition is under review (G-26b) |

## 6. Ledger §28 — the M5 entry gate, measured at `5e83e61`

**Precondition: NOT MET.** "M5 starts only after the Research Foundation is accepted." PR #10 is open; the
Director has not ruled acceptance.

| # | condition | status |
|---|---|---|
| 1 | canonical research corpus in the repository | **MET** on PR #10 (LOCAL and REMOTE; not MAIN) |
| 2 | ADR-030 exists | **MET** (PR #10) |
| 3 | research terminology standardized | **MET** (area 1) |
| 4 | research tests pass | **MET.** CI `build-and-test` is green on `5e83e61` (2026-10-01). Local run at `d4ec6cd` (same code; `5e83e61` added documents only): Sim.Tests 1069 passed / 0 failed / 4 skipped; Sim.Ui.Tests 298/298 |
| 5 | determinism tests pass | **MET.** CI `determinism` and `determinism-xproc` green on `5e83e61` |
| 6 | goldens re-pinned with measured causes | **MET** (`d4ec6cd`; ADR-029 addendum A.8) |
| 7 | Capability Ownership Matrix exists | **MET** (PR #10) |
| 8 | baseline capabilities explicitly classified | **PARTIAL.** The matrix classifies them (48 A), but `research.json`'s baseline list and node strings disagree (G-19, G-20, G-21) |
| 9 | no known research/starting-world contradiction unresolved | **NOT MET.** G-18 (five activity gates), G-19, and the coastal-shipping gate |
| 10 | provisional calibration not frozen as architecture | **MET.** The calibration report is PROVISIONAL, U, K and the RP constants are tuning data, and calibration adjustments are refused |
| 11 | no unresolved Director decision hidden in implementation | **MET by record.** The implementation's readings of T3, R-2 and T7 are recorded OPEN. The PathBuild bank (G-32) was recorded only on UNMERGED-B until the ledger's R4.15. None of these is ruled |

## 7. Conflict register (Step 6, consolidated)

| class | findings |
|---|---|
| RATIFIED DIRECTOR DECISION (newly recorded in the repository) | G-12, the five categories. More generally, ledger Part I as a whole |
| STALE DOCUMENTATION | G-02, G-07, G-11, G-20 (content text), G-30, G-35, G-36 (reference), G-37 |
| IMPLEMENTATION GAP | G-06, G-09, G-21 (content), G-23 (test), G-26 (content), G-27 (code absent) |
| IMPLEMENTATION CONFLICT | G-10 and G-13 (both UNMERGED-B UI), G-18, G-19, G-32 (PathBuild), G-33 (NOT) |
| OPEN DECISION | G-01, G-04, G-05, G-08, G-14 (**AGE-MS-1**), G-15, G-16, G-22, G-24, G-25, G-27, G-28, G-31, G-32, G-33, G-34, G-39; and **CR-005**, untouched |
| DEFERRED | G-06, G-09, G-38; area 22; §27 in full |
| HISTORICAL / SUPERSEDED | G-03, G-17, G-29; ledger R4.1–R4.6, R4.10, R4.17, R4.19–R4.21 |

## 8. Proposed changes

None of these changes is applied.
- **NOW** — eligible for the next pre-M5 normalization packet once the Director approves it. "Docs" means
  append-only notes. Every original stays.
- **LATER** — waits for a ruling, or for the system or milestone that owns it.

| # | change | file(s) | system | reason | supporting decision | dependency | when |
|---|---|---|---|---|---|---|---|
| P1 | Record the Director's rulings on the pending statements (§4) that the Director confirms, in a new D-record (D-047 is free) with ledger §30 updates | new `docs/d047-….md`; ledger annex | none | Ledger §30: conversational decisions are unsafe until recorded | Director confirmation | the Director confirms each statement | **NOW** |
| P2 | Append resolution notes for completion overflow | `docs/adr/adr-029-research-engine.md` (addendum), `docs/d046-…md` Part D item 2, `docs/d045-…md` Part D item 2, `docs/adr/adr-030-…md` §7, `docs/queue.md` | none | G-01, G-02 | 2.2, once recorded (P1); ledger §5.5 | P1 | **NOW** |
| P3 | Null the five activity requirements, remove their reverse-index entries, regenerate `docs/research-corpus-audit.md` (CI `--check`) and pin "null requirement, eligible at zero nodes" | `Sim.Data/content/research.json`; `docs/research-corpus-audit.md`; `Sim.Tests/Systems/ResearchContentTests.cs` | research content | G-18; M5 gate 9 | ledger §21; matrix §5 item 1; 2.4 once recorded | P1 | **NOW** |
| P4 | Re-scope, or record as non-owning, the baseline-claiming strings in §3; correct the `providedBy` texts | `research.json`; matrix §5 notes | research content | G-19, G-20; M5 gate 8 | ledger §21; matrix §5 items 2–3; 2.4/2.5/2.7 once recorded | P1, and Director rulings on the **UNRESOLVED** rows (fire, hafting, fish weir) | **NOW** |
| P5 | Optional baseline entries: shelter, basic tools and implements, watercraft (not simulated), basic paths and transport | `research.json` `baseline`; matrix §7.1 and §3 counts | research content | G-21 | D-045 §1 | P1 | **NOW** (optional) |
| P6 | Test pins: penicillin's closure excludes surgery; zero-research run has `ResearchCompleted` = 0 and at least one `DirtPath` edge; generation local to family (non-null ⇒ family, ≥ 1, no same-family prerequisite ≥ the dependent's) | `Sim.Tests/Systems/ResearchContentTests.cs`, `ResearchBaselineTests.cs` | tests only | G-23; area 13; "basic paths are baseline" | D-044 R7; ledger §21 | none | **NOW** |
| P7 | Rule T3; then append notes to D-044 Part F, ADR-029 §10/§14, `queue.md`, and pointer notes after D-040 B3 and on the matrix BLOCKED rows | `docs/d044-…md`, `docs/adr/adr-029-…md`, `docs/d040-…md`, `docs/queue.md`, `docs/design/research-capability-ownership.md` | none | G-04 | 2.6 once recorded, or a direct ruling | Director ruling | **NOW** (records) / **LATER** (content for road tiers and shipping) |
| P8 | Append notes reconciling the lenses and §12.7 | `docs/design/civilization-progression-architecture.md` §3.3, §12.7, §16.1 | none | G-07, G-11 | ledger §3 | G-08 answered for the Civics lens | **NOW** |
| P9 | Fix the matrix anchors at `:1921` | `docs/design/research-capability-ownership.md` | none | G-35 | gov-4 citation hygiene | none | **NOW** |
| P10 | Rule NOT in prerequisites; if allowed, an ADR plus a loader change with non-monotone availability semantics and tests | `Sim.Core/Systems/Research/ResearchContent.cs:387-389`; new ADR (ADR-031 is free) | research loader | G-33 | ledger §6 against D-044 R8 and ADR-029 §2.2 | Director ruling | **LATER** |
| P11 | Content review of the flagged medicine, communications and chronology edges (G-22, G-24, G-34) and the 8 inherited Eurekas (G-26) | `research.json` (moves depth, cost rationale, the audit and possibly the goldens) | research content | causal-edge rule (architecture §6.2); ledger §8.5 | D-046 F§1 approval | Director review | **LATER** |
| P12 | Rule the PathBuild bank (reading a or b); if (a), a packet with an ADR, a code and schema change to `PathProgressRow`, and golden re-pins | `Sim.Core/Systems/PathBuild/PathBuildSystem.cs`, `Sim.Core/State/WorldState.cs`, `Sim.Core/Kernel/CanonicalSchema.cs` | PathBuild | G-32 | ledger §15 against T3.8 | Director ruling; CR if the Director treats T3.8 as frozen | **LATER** |
| P13 | File a CR for the Age-keyed dt binding (CR-019 is free) | new `docs/adr/cr-019-….md` | kernel (TurnExecutor, era table) | G-16 | ledger §10.3 against frozen Spine S3 and D-006 | an Age state exists, or is designed | **LATER** |
| P14 | Reconcile UNMERGED-B with MAIN: D-043, CR-017, and the UI's six Ages and four categories; resolve the DD-13/DD-14 collision | `claude/civdemo-work-b1z2y4` documents and `Sim.Ui/UiContent/trees/ages.json` | UI content | G-10, G-13, G-27, G-30 | ledger §10, §10.4; R4.16 | a Director decision on the branch merge | **LATER** |
| P15 | Rule CR-017 §1–§7 (Ages as state, the surge, Action Capacity, modernization, movement, War Pulses, special people) | `docs/adr/cr-017-….md` (UNMERGED-B) | — | G-15, G-27, G-28 | ledger §10, §17–§19 | P14 for the record to reach MAIN | **LATER** |
| P16 | Rule AGE-MS-1 (Age-milestone ownership) | ledger annex R6.3; a future D-record | the future Age system | G-14 | ledger §10.4, §2.3 | P15 §1 (Age as an input) | **LATER** |
| P17 | Rule CR-005 (development-milestone placement): OPEN on MAIN and PR #10; marked RULED (Option C) only on the unmerged `m5-full-build` | `docs/adr/cr-005-….md` | — | the M5 entry | CR-005 options A/B/C | Director | **LATER** (Director's timing) |
| P18 | Append notes on ADR-019 §1.3 (dt timing) and D-039 E3 (War Pulses) | `docs/adr/adr-019-….md`; `docs/d039-…md` | none | G-17, G-29 | ledger §10.3, §19 | Q-73 (whether post-freeze D-records are frozen) for D-039 | **NOW** (ADR-019) / **LATER** (D-039) |
