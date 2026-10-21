# M6 — Director's ruling docket (2026-10-21, M6.1 ratification gate)

**Status: DECISION-READY. Nothing here is ruled. Recommendations are the agent's; the Director rules by a dated line in
the named record (D-061 §9 for G1; a dated note in `docs/m6-spec.md` §6 for the spec; the decision register for the
rest). This docket does not ratify anything and this prompt did not.** Baseline: `main` = `m5-exit` = `6e51a3e`;
branch head `4190a01` (docs only; CI 39608034320 and ui-artifact 39608034426 green).

## A. Decisions required to OPEN M6 implementation

### A1 — Ratify `docs/m6-spec.md`
1. **Question.** Is the M6 specification (S8 §4.1 items 1–4, invariants I-1…I-12, packets T6.1–T6.21, exit criteria 1–10) ratified as the allocation instrument for M6?
2. **Recommendation.** Ratify **with two corrections recorded by dated note** (this docket's §C.1 and §C.2 — the civic→technology coupling and the T6.17 packet shape). Both are appended, not rewritten.
3. **Alternatives.** (a) Ratify as-is: the two defects are carried into packets and found there (cost: a T6.3 surprise when CP pace gates the technology subtrees). (b) Return for redraft: delays every lane by one review cycle for two notes' worth of change.
4. **Affected.** Every M6 packet; `CLAUDE.md:10` ("spec in preparation" → "ratified; packets open"); `milestones.md` M6 row; the m6-plan package (superseded by dated note).
5. **Latest point.** Before T6.2 opens (nothing in §4 starts without it).

### A2 — D-061.1: supersede the single-RP-pool rulings for Civics
1. **Question.** Is the Director's 2026-10-21 direction — a separate Civics Points pool and a genuinely distinct civics tree — ratified as superseding D-044 R2/R12, D-046 G5, ledger §26 items 2–3 and the 2026-10-20 D-I ruling, **for Civics only**, per `docs/d061-civics-points-supersession.md` §3 and CR-023 option 1?
2. **Recommendation.** Ratify D-061.1.
3. **Alternatives.** CR-023 option 2 (one pool, two targets — a split, not a currency; does not meet the direction as worded); option 3 (defer — contradicts "the intended direction").
4. **Affected.** Records: D-044, D-046, ledger §26, D-I, the one-pool test `ResearchEngineTests.SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp` (`Sim.Tests/Systems/ResearchEngineTests.cs:548`), `Civics_ResearchesWithProgressCompletionAndBranching` (:532). Systems (later): `ResearchSystem`, `ResearchQuery`, `AiResearchPolicy`, research panel, snapshot sections 62+. State: new CP pool/target/progress rows.
5. **Latest point.** Before T6.3 (civics currency). T6.2 and every independent lane can run without it.

## B. Decisions that can be ruled within later packets (each with its latest point)

| id | exact question | recommendation | alternatives → material consequence | affected | latest point |
|---|---|---|---|---|---|
| **D-061.2** | Is CP per strategic turn (a second scoped law-3 exception by its own ADR-043) or per sim-year × dt? | per-turn by ADR-043, mirroring ADR-030 §2 scope exactly | per-year: civics pacing diverges from technology across era bands (ADR-030 §4: 20× between Neolithic and Modern dt) | `ResearchSystem` step; ADR-043; `ResearchEngineTests` dt-invariance twin for CP | T6.3 design, before code |
| **D-061.3** | What generates CP? | a population term with its own exponent (anchored like RP) **plus** an Active-institution term (Σ maturity of civic institutions); **no RP term, no fraction of RP** | population-only: CP mirrors RP and civics never rewards institutions; a fraction of RP: a split, refused by the 2026-10-20 instruction | `ResearchQuery` (new `CivicsPoints`), `research.json`/`civics.json` tuning block, CP-rate report, **the civic→technology gate timing (§C.1)** | T6.3, before code; must be checked against §C.1's reachability measurement |
| **D-061.4** | Separate content file/key space, or shared key space with a cross-tree fence? | **REVISED from the review package: shared key space, civics in a reserved range (today 1001–1007), with a validator fence for cross-tree edges.** Reason: a separate key space forces re-keying `ResearchProgressRow`/`ResearchCompletedRow` rows at the v34→v35 migration, which is a row REWRITE and breaks the append-only premise behind the readable set (`CanonicalSchema.cs:14-16`) | separate `civics.json`: cleaner authoring, but migration must map keys deterministically and the completed-mask readers must translate — a hidden kernel change | `ResearchContent` loader, validators, `CanonicalSchema` migration, every completed-mask reader | T6.3, before the migration is written |
| **D-061.5** | At the schema bump, carry the seven nodes' existing RP progress 1:1 as CP, or reset? | carry 1:1 (progress rows keep their key under D-061.4-revised; a `tree` read decides the pool) | reset: every resumed session loses civics progress; cheaper code | migration path, v34 fixture resume test extended, goldens attributed | T6.3 |
| **D-061.6** | Which schema version do civics tables take? | the next landed version (D-H "first to land"); fog's v35 reservation moves by dated note if civics lands first | fixed v35 fog / v36 S3: the first-finishing lane stalls | `CanonicalSchema`, D-055 note, ADRs | the first of T6.3/T6.7 to reach code |
| **D-061.7** | One civics target or several? | one (mirrors R9; one `SetCivicsTarget` kind 14) | several: manual allocation (ledger §26 item 5 forbids percentage allocation) | order kinds, AI lane, UI | T6.3 |
| **D-061.8** | Keep civics nodes as Age-milestone facts and **as atoms inside Technology gates** (§C.1)? | keep both, with two rules: no macro civic (government form) is ever an Age CORE milestone; civic atoms in technology gates are legitimate "exposure" edges **and T6.3 must measure that the research stage stays reachable at A4/A5 under the chosen CP rate** | strip civics from the stage / `inst.university` requirements: removes the coupling but rewrites D-044 R4 content and the D-047 causal review | `ages.json`, `research.json:254`, `inst.*` requires, `AgeQuery`, AI goal closures | T6.3 (measurement) and T6.4 (content rule) |
| **D-R** | Does research overflow carry to the next target of the same pool, or stay lost? | carry, capped at one node ahead | lose (today): one-turn floor per node and lost RP with no target | `ResearchSystem.cs:189-234`; `Completion_…LosesTheOverflow` (:230) superseded by a dated pin; ADR-029 R-2 note | T6.2 |
| **D-S** | Do inputs beyond population feed RP (scholars, literacy)? | not in M6 for RP (ADR-030 stands); institutions feed CP only (D-061.3) | scholars feed RP: double-counts staff already withdrawn from labour (`Institutions.cs:72-109`) | `ResearchQuery`, ADR-030 | T6.2 |
| **D-K** | Ratify S2a: per-polity bitset over the 16 km lattice (v-next), monotone OR, sight = `Pathfinder.Isochrone` from controlled settlements and owned formations with ONE TUNE budget in cost units; sighting memory as state? | yes, all four | memory derived only: lost on resume; sight from settlements only: formations invisible until M7 anyway (acceptable alternative) | `WorldState` new sections, `DiscoverySystem`, `PolityKnowledge.Of`, leak tests, `PreFog` twin | T6.7, before code |
| **D-J** | Scouts in M6: none / capability score / class? | none (M7's first MobileAgent) | score: a coefficient with no mechanism (law 2) | T6.7 | T6.7 |
| **D-T** | What does the AI assume of unseen terrain? | impassable and worthless for valuation (symmetric with `SiteAssessment` withholding) | optimistic default: re-opens the quarantined channel (`PolityKnowledgeTests.cs:311`) | `AiFoundingPolicy.cs:63-80, 312-340` | T6.8 |
| **D-M** | What is an institution (CR-010)? | a founded, hosted, maturing row with one owning system and ≥1 consumer — the university shape generalized; written as an ADR in T6.11 | bespoke per type: R-02 for institutions | `InstitutionsSystem`, `Institutions.cs`, six civic `inst.*` entities | T6.4 content needs the definition; T6.11 writes the ADR |
| **D-P** | Vertical slice = law_code → law court → `Unrest.ServiceOffset`? | yes | another slice | T6.18 | T6.11 (the slice's first realization) |
| **D-Q** | Verb-granting civics before M7: knowledge-only? | knowledge-only until M7 | seam ruling: a verb with no owner | T6.4 validator rule | T6.4 |
| **D-H** | Schema sequencing rule | first to land takes the next version; reservations re-noted | fixed slots | `CanonicalSchema`, D-055 note | first state packet |
| **D-G** | Export writer: hand-rolled minimal OOXML or a library? | hand-rolled (no new dependency; deterministic bytes) | library: licensing and nondeterminism review | T6.14 | T6.14 start |
| **D-L** | E1 Age style register driver | realized state | Age or knowledge | T6.13 | T6.13 start |
| **D-U** (new) | Commission the cited Civ VI comparison as T6.1b (docs only) before T6.4 content? | yes | fold into T6.2 | T6.4's content basis | before T6.4 |
| **D-V** (new, §C.3) | Does M6 ship the regime-module set as a serialized read-only table, or as content only until M8 writes it? | content only in M6 (no table without a writer) | a table with no writer is dead state and a layout golden move for nothing | T6.5 | T6.5 |

## C. Pre-ratification audit findings (spec vs governing records)

**C.1 — MATERIAL, a review-package defect: Technology is gated by a civic, and the spec did not say so.** MEASURED:
`legal_code_roman` (civics key 1002) is an atom of the research stage that opens all five Technology subtrees
(`Sim.Data/content/research.json:254`, read by `ResearchQuery.cs:81-94`; test
`Subtrees_AreClosedBeforeTheStage_AndAllFiveOpenTogetherWhenItIsReached`, `ResearchEngineTests.cs:348`); `inst.university`
requires `library AND legal_code_roman` (`research.json:29068`); 13 further `inst.*` entities require civics (census,
coined_wage, patent, joint_stock — `research.json:28984-29152`); `a5_systematic_law` is an A5 CORE milestone
(`ages.json:85`). Under D-061 these edges move from "the same pool, cheapest-first" to "paced by CP generation": a slow
CP rate delays the technology subtrees, universities and A5 for every polity, and the AI's university goal closure
(`AiResearchPolicy.cs:165-178`) spans both pools. **Consequence for rulings:** D-061.3 (CP inputs) and D-061.8 (Age and
gate coupling) are not independent; T6.3 must measure the turn at which `legal_code_roman` completes on the canonical
seeds before and after, and the spec's §3.4 coupling map gains a row. **Corrected by dated notes** appended to
`docs/m6-spec.md` (§3.4, §7 G1) and `docs/d061-civics-points-supersession.md` (§5) in this commit. No ruling is implied.

**C.2 — Packet shape: T6.17 "continuous" conflicts with CLAUDE.md "Execute ONE task packet".** Persistence coverage is
each state packet's acceptance (I-10, I-12), plus one closing packet (T6.17) that runs the fixture resumes, the
cross-process legs and the readable-set statement over the merged tree. Corrected by dated note to §4.

**C.3 — Dead state risk in T6.5.** "Regime-module data shape … read-only in M6" as a serialized table would be state
without a writer; recommend content-only in M6 (D-V). Not a defect in a frozen rule; a design choice surfaced.

**C.4 — Hidden dependency: T6.10 (diffusion) waits on T6.8 (contact within known extent).** If the fog lane lags,
diffusion can land first against the all-known stub with a documented re-gate when extent arrives. Noted, no change.

**C.5 — D-061.4's original recommendation (separate `civics.json`) collides with the append-only migration premise**
(`CanonicalSchema.cs:14-16`; readable set {32,33,34} relies on appended tables, never rewritten rows). Recommendation
revised in §B. Recorded in D-061 by dated note.

**C.6 — Consistency checks passed.** Frozen Spine M6 line ("Knowledge & divergence: domain lattice lite, diffusion,
computed era labels") is covered (two trees + subtrees, T6.10, Ages as computed state + E1); fog, civics currency and
export enter by rulings, not by rewriting the Spine. S8 §4: written after the exit gate; four items present; nothing
beyond n+1 specified. Kernel: no kernel file is touched by any packet except `CanonicalSchema` appends by ADR and a new
`OrderKind`, both the established path. Determinism/persistence rules restated as I-10…I-12. M6/M7/M8 boundaries match
D-052 §4, D-011, D-D, the handoff register. Registers: the task/dependency registers map onto T6.x with one new gate
(D-061). The five-state separation is explicit (I-2) and the tax gate chain is the live template (`Governance.cs:251-302`).

**C.7 — Missing or thin acceptance criteria.** T6.19 and T6.21 are one-liners; T6.13 has no asset budget (D-L owns it).
To be completed when those packets are cut; none is on the critical path.

**C.8 — Nothing makes the critical path unexecutable** once A1 and A2 are ruled. The path's first code packet (T6.2)
needs only D-R and D-S, both rule-able inside it.

## D. Readiness statement

**The specification is READY FOR RATIFICATION with the two dated corrections of C.1 and C.2 applied** (this commit). It
is NOT ready to be treated as ratified: no ruling exists yet.

## E. The exact next action required from the Director

Write two dated lines: (1) in `docs/m6-spec.md` §6: "2026-10-DD — RATIFIED (with the M6.1 docket's C.1/C.2 notes)";
(2) in `docs/d061-civics-points-supersession.md` §9: "2026-10-DD — RATIFIED D-061.1" (D-061.2–8 may follow with T6.3,
noting the revised .4 and widened .8). Optionally rule D-R and D-S now so T6.2 opens without a round-trip.

## F. The proposed first implementation packet after ratification — T6.2 Research engine reconciliation

- **Scope (one packet, from `main` on branch `t6.2-research-reconciliation`):** (a) parameterize `ResearchSystem`/
  `ResearchQuery` by (tree, pool, target) behind the existing one-pool behaviour — NO behaviour change until T6.3 (byte-
  identical goldens, proven by the existing pins); (b) implement D-R if ruled "carry" behind a TUNE switch with the
  pre-change control as the attribution twin, else record "lose" as ruled; (c) the stale code comments
  (`ResearchQuery.cs:562`, `PolityKnowledge.cs:27-28`, `UnitCardModel.cs:46,79-80`, `SessionEmitter.cs:12`) and the
  ADR-029 §3 owner line; (d) a reachability measurement harness: the turn at which `legal_code_roman`, the research stage
  and `inst.university` eligibility are reached on seeds 1, 2, 42, 7 (the C.1 baseline T6.3 will be measured against);
  (e) `UnlockedCapabilities` strings reviewed against the unlock audit.
- **Exclusions:** no CP pool, no civics target, no schema bump, no content change, no fog.
- **Acceptance:** `ResearchEngineTests`, `ResearchContentTests`, `ResearchUnlockPipelineTests` green; founded golden
  `0f29efc7…6d3c` and the 60-turn hash `827d9b00…3878a0` UNCHANGED unless D-R = carry, in which case every moved pin
  carries a RE-PIN cause block and the switch-off twin reproduces the old value byte-for-byte; the reachability report
  committed under `docs/`; banned-constructs, read-isolation, readonly-proof gates OK; full Sim.Tests and Sim.Ui.Tests;
  cross-process leg green; tie-dense test for any new ordering; mutants with at least one semantic kill each.
- **Blocking decisions:** spec ratified; D-R and D-S (rule-able at packet start).
