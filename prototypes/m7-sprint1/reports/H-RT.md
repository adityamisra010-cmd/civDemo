# H-RT — M7 lane RT: the research-tree audit's fixes (D-084)

Date 2026-11-11. Clone `/home/user/m7w/RT`, branch `lane/RT`, base `51ab032` (the development branch
`claude/new-session-yavghm` as handed to this lane). **Scoped `Sim.*` exception, D-084 DC1-R2 (a):** this lane changed
`Sim.Ui` (the research screens under `Sim.Ui/Progression`) with `Sim.Ui.Tests`, the research estimate in
`Sim.Core/State/InfoQuery.cs` with `Sim.Tests`, and three `emerged` texts in `Sim.Data/content/research.json`.
- Nothing in `Sim.Core/Kernel` changed.
- No simulation state, world hash or simulation golden moved.
- No permanent project and no CI file changed.
- No merge, tag or release.

**Provenance.** This is the second run of this lane. The first (10:03–11:32Z) was interrupted after five commits
(`60ec80c`, `918b5fc`, `91e6b64`, `59bac37`, `950d6d1`) and one uncommitted test; `RT-wip.bundle` (11:32Z) held those
commits. This run continued in the same clone. It checked every earlier change against the tree before building on it, and:
- committed the uncommitted test after showing it kills the draw-order mutant (`99a0115`);
- added two tests for branches the shipped corpus never reaches (`0620ab9`, `9ed6c6d`);
- withdrew two of the five `emerged` text fixes (`0a0a39e`; fixes report §3.6);
- carried the three remaining `emerged` lines into the research-economy candidate file (`7f9b1b0`), after Tier 1 caught
  it stale (§7);
- ran the mutants and the three validation tiers, and wrote this report and
  [research-tree-fixes-2026-11.md](../../../docs/m6-plan/research-tree-fixes-2026-11.md).

The baseline counts in §7 are the first run's measurements of the same commit in the same clone; they were not re-run.

**Status words:**
- **MEASURED**: run in this lane, on this branch.
- **DERIVED**: exact argument.
- **INFERRED**: argued, not isolated.
- **PROPOSAL**: for the Director, not applied.
- **READING**: an interpretation, with the alternative stated.

**Authority:**
- D-084 (DC1, DC2; DC1-R1 (a), DC1-R2 (a));
- D-070 R2, R3 and R4; D-061; D-068; D-072; D-073;
- D-047 (ruling 1, Parts C and E);
- D-069 and `ip-originality.md`: nothing this lane wrote names another game; no player-facing text names one.

---

## 0. Summary

| Brief item | State | Where |
|---|---|---|
| (1) Check the audit §2–§8 on the tree | **Done.** §2, §2.1, §2.2, §2.3, §3, §4, §5.1, §5.4, §6, §7, §7.3 and §8 REPRODUCED. §5's formula PARTLY: a covering bank takes 1 End Turn in the engine, not 0. One further surface carries the §4 defect (the inspector; not in scope, proposal N1) | fixes §1 |
| (2a) §4: grouped expressions | **Done**, with truth-table tests for the 4 nodes, a corpus test over 995 expressions and 2 synthetic cases | fixes §3.1 |
| (2b) §3: completion-coloured edges | **Done**, pinned and hovered, in both trees | fixes §3.2 |
| (2c) §5: one bank-aware estimate | **Done**: `InfoQuery.ResearchTurns`, used by the card, the pane and the inspector, following the engine. *(Qualified in verification round 1, RT-R1-01: for an open node; a civic not yet open now gets no figure, §10)* | fixes §3.3 |
| (3) Longbow | **Diagnosed** (headless capture), no content change; the Director's screenshot and build are needed | fixes §3.4 |
| (4) Tier gaps | **Reviewed** (the 18 edges measured); routes and depths unchanged; the off-screen marker names the prerequisites and their tier distance | fixes §3.5 |
| (5) Historical review | **Done**: [research-date-review-2026-11.md](../../../docs/m6-plan/research-date-review-2026-11.md), 3 text fixes, 6 prerequisite questions | fixes §3.6 |
| (6) Validation | Tiers 1, 2 and 3, the full suites under the shared lock; what was NOT RUN is stated. *(§7 is run 2's, for `a7b5faa`; verification round 1's runs are in §10.6)* | §7 |
| (7) Reports | This file; [research-tree-fixes-2026-11.md](../../../docs/m6-plan/research-tree-fixes-2026-11.md) | — |

---

## 1. What exists

| File | Change |
|---|---|
| `Sim.Ui/Progression/PrerequisiteExpression.cs` (new) | `PrereqExpr` (the expression tree, its truth, its text), `PrereqExpr.Kinds` (And / Or / Grouped from the expression), `RequiresRows` (the pane's flat list or outline) |
| `Sim.Ui/Progression/ResearchGraph.cs` | `EdgeKind.Grouped`; edges classified by `Kinds`; `Requirement(vertex)` |
| `Sim.Ui/Progression/ResearchNodeViews.cs` | the view's kinds and `Requirement`; `BankOn`, `TurnsOf` |
| `Sim.Ui/Progression/ProgressionScreen.cs` | edge inks by completion and draw order; dotted grouped edges; legend keys; lock line; REQUIRES outline and expression in names; the bank beside the estimate; card and pane estimates through `TurnsOf`; the off-screen marker's second line |
| `Sim.Core/State/InfoQuery.cs` | `ResearchTurns`, `ResearchBank`; the "Open to research now" line uses them |
| `Sim.Data/content/research.json` | `emerged` of `adhesive_natural`, `grinding_stone`, `well` |
| `Sim.Ui.Tests/ResearchTreeAuditTests.cs` (new) | 30 test cases |
| `Sim.Tests/State/ResearchEstimateBankTests.cs` (new) | 18 test cases |
| `Sim.Ui.Tests/ProgressionScreenTests.cs`, `ResearchPointDisplayTests.cs` | 3 moved pins (§5) |
| `docs/m6-plan/research-tree-fixes-2026-11.md` (new), `research-date-review-2026-11.md` (new), `research-tree-fixes-2026-11/` (the measuring script and 6 captures) | reports and evidence |
| `docs/research-*-audit.md`, `docs/research-calibration-report.md` | regenerated: the SHA-256 header line only |
| `docs/m6-plan/knowledge-redesign/research-cost-pass/candidate/research.candidate.json` | the same three `emerged` lines (the candidate is the shipped file plus overlays; `ResearchCandidateTests`) |

Line counts are in the fixes report, §2.

## 2. Design and equations

**§4: the kinds** (M = `Predicate.MustHoldAtoms`; the research dialect is monotone, so M is exactly the set of atoms true in
every satisfying assignment):

```
And      a ∈ M
Or       a ∉ M  and  f(M) = false  and  f(M ∪ {a}) = true
Grouped  otherwise
```

DERIVED (fixes §3.1): with no Grouped atom, f = AND(M) ∧ OR(the Or atoms). So the flat labelled list is kept for those
expressions and the outline is used only where it must be: MEASURED, 4 of 995 expressions.

**§5: the estimate, in End Turns, as the engine pays a civic** (step order: directives → credits → RP → CP accrual → CP
spend on `fund[1]` → completion):

```
remaining = max(0, cost − progress);  shortfall = remaining − max(0, bank)
turns = 1 if shortfall ≤ 0;  max(1, ceil(shortfall / rate)) if rate > 0;  else none
bank = the CP balance for a civic in the two-lane model, 0 for a technology
```

**§3: the inks:**
- completed prerequisite: `Semantic.Completed`;
- outstanding: `Semantic.Pinned` (pinned, cased) or `Semantic.Prerequisite` (hovered);
- draw order: dependents → completed → outstanding.
- *(Verification round 1: hovered incoming edges are cased too (RT-R1-04); the dependents' place in the order shows nothing,
  since their routes never share a segment with incoming ones (RT-R1-03). §10.)*

## 3. Evidence (the main measurements)

| Claim | Evidence | Kind |
|---|---|---|
| The audit's structural numbers (§2), 18 long edges, 77 / 4 mixed expressions, 48 date candidates | `measure_audit.py` on the base blob `f05deae6` | MEASURED |
| The defects failed tests on the unchanged tree | Run 1: 8 of 9 audit facts and 6 of 8 rig facts failed on `51ab032` (fixes §1) | MEASURED (run 1 logs) |
| The fixes hold | the focused filters, 86 UI and 94 `Sim.Tests` cases, pass; mutants §6 | MEASURED |
| The estimate is what the engine does | the engine stepped on the rig (6 cases) and on the stepped world (5 cases); all three surfaces equal the engine | MEASURED |
| The completed ink reads on its casing | 3.95–4.70:1, ΔE 39.9–54.5 from the orange, 9 Ages | MEASURED |
| The hover inks are faint in early Ages (limitation) *(superseded for incoming edges in verification round 1: now cased, §10)* | completed 1.33–3.15:1, prerequisite 1.60–3.87:1 on the field | MEASURED (scratch probe) |
| The inspector flattens too | 10 false "one of" labels on 4 nodes, 6 on 2 entities (scratch probe) | MEASURED |
| The Longbow discrepancy is the Age-sweep list under REQUIRES | the pane's text on the stepped world | INFERRED (the in-play pane was not seen) |
| `charters` fails on the base too | identical output, `51ab032` docs against this branch | MEASURED |

## 4. Tests

**New** (all pass, Release):

| Class | Cases | Covers |
|---|---|---|
| `ResearchTreeAuditTests` | 30 | **§4:** every drawn kind and every pane label true of its expression (1,973 edges); the corpus truth table (995 expressions: `Predicate`, view, graph and pane rows agree on every assignment); the four nodes, every atom combination against the canonical expression, the flat reading shown wrong; the painted pane (groups, no label on a grouped part); the 2-of-3 and the redundant-atom cases.<br>**§3:** pinned and hovered inks by completion in both trees; the shared stub shows the outstanding ink; dash by kind; the completed ink's contrast.<br>**§2.2:** the 18 long edges; the marker's second line.<br>**§6:** the Longbow pane.<br>**§5:** card, pane and inspector against the engine, and the technology estimate unchanged by the CP bank. |
| `ResearchEstimateBankTests` | 18 | the shared function's exact values (8 cases: 600 → 1, 450 → 1, 449 → 2, 0 → 10, progress + bank → 1, zero rate → 1, paid in full → 1, a negative bank counts as none); no estimate at zero, negative or NaN rates; `ResearchBank` for civic, technology and one-pool; the inspector against the engine on a rig (6); zero rate (covering → 1, short → none); technology not shortened |

## 5. Moved pins, with measured attribution

| Test | Was | Now | Attribution (MEASURED) |
|---|---|---|---|
| `ProgressionScreenTests.Edges_AreExactlyThePrerequisites_ClassifiedAndOrFromTheExpression` | each edge And iff in `MustHoldAtoms`, else Or (counts 1,749 / 224) | And iff in `MustHoldAtoms` as before; the rest Or or Grouped; counts (1,749, 214, 10) | Mutant m02 (the old flat classification) gives (1,749, 224, 0): the 10 edges that move are the grouped parts of the 4 audited nodes |
| `ProgressionScreenTests.Hover_HighlightsExactlyThePrerequisitesAndDependents` | prerequisite ink present iff the card has any prerequisite | prerequisite ink iff an outstanding prerequisite; completed ink iff a completed one | Mutant m08 (hover ink ignores completion) fails it: the change is the §3 fix alone |
| `ResearchPointDisplayTests` (the k5 civic estimate) | "~13 turns" = ceil((cost − progress) / next accrual), the bank ignored | "~1 turns" with the stepped world's 715.213 CP bank; with the bank emptied the same test still asserts the old 13. *(Moved again in verification round 1, wording only: "~1 turn", RT-R1-06 b; §10)* | The test's own twin (bank emptied) reproduces the old value; mutant m10 (bank ignored) fails it |

No simulation golden, world hash, tree-closure hash (STRUCTURE or TEXT) or content pin moved. MEASURED: `TreeClosureBaselineTests`
pass. `emerged` is in neither closure hash. The D-047 dated-node count (`Nodes.Count − 85`) is unchanged.

## 6. Mutants

Each mutant was applied to the clone, built (Release, incremental) and run against two filters:
- the research UI filter: `ResearchTreeAuditTests`, `ProgressionScreenTests`, `ResearchPointDisplayTests` and
  `ResearchPinTests`, 86 cases;
- the `Sim.Tests` filter: `ResearchEstimateBankTests`, `ResearchContentTests` and `InfoQueryTests`, 94 cases.

Then it was restored from HEAD.

**Bound:** 120 s per test run, 10× the clean run (MEASURED through the same harness: 12 s and 11 s). None hung.

Every kill is by a semantic test, not a golden.

| # | Mutation | Result | Killed by (semantic tests; count of failing cases) |
|---|---|---|---|
| m01 | edges drawn in `Graph.Edges` order (before `59bac37`), inks unchanged | KILLED | `TheSharedStubIntoACard_…` (2: both trees) |
| m02 | every atom outside the must set is "one of" (the pre-D-084 flattening) | KILLED | 19: every-edge-kind, every-pane-label, corpus truth table, the four nodes' truth tables (4) and painted panes (4), Seed drill pane, dotted grouped edges (4), 2-of-3, redundant atom, `ProgressionScreenTests.Edges_…` (the moved pin: (1749, 224, 0)) |
| m03 | "one of" no longer requires that the required atoms alone fail | KILLED | `ARedundantPartOfAGroup_…` (1) |
| m04 | the REQUIRES rows always the flat list | KILLED | 11: corpus truth table, the four nodes' truth tables (4) and panes (4), 2-of-3, redundant atom |
| m05 | a nested group's head names the other operator | KILLED | 8: corpus truth table, the four nodes' truth tables (4), a pane, 2-of-3, redundant atom |
| m06b | same-operator parts not spliced (nested binary groups) | KILLED | `TheFourNestedExpressions_EveryAtomCombination_…` (`astronomy_records`: the text no longer reads as the content writes it), 2-of-3 (2) |
| m07 | the pinned path ignores completion (all orange) | KILLED | `IncomingEdges_…` (4), `TheSharedStub_…` (2) |
| m08 | the hovered path ignores completion | KILLED | `IncomingEdges_…` (4), `TheSharedStub_…` (2), `ProgressionScreenTests.Hover_…` (the moved pin) |
| m09 | a grouped part drawn with the "one of" dash | KILLED | `TheFourNestedExpressions_PinnedEdges_…` (4) |
| m10 | the shared estimate ignores the bank | KILLED | 13: the function's theory (5), the inspector against the engine (5), zero rate, the card/pane/inspector test, `ResearchPointDisplayTests` k5 (the moved pin) |
| m11 | a covering bank gives 0 turns (the audit's formula) | KILLED | 9: the function's theory (4), the inspector against the engine (2), zero rate, the three-surface test, k5 |
| m12 | the UI counts the CP bank for a technology | KILLED | `ATechnologyEstimate_IsNotReducedByTheCivicsBank` (1) |
| m13 | `InfoQuery` counts the CP bank for a technology | KILLED | 3: `ResearchBank_…`, `ATechnologysEstimate_…` (rig), `ATechnologyEstimate_…` (stepped world) |
| m14 | the inspector line passes no bank | KILLED | 7: the inspector against the engine (5), zero rate, the three-surface test |
| m15 | the card's state line goes back to cost / rate | KILLED | 2: the three-surface test, k5 |
| m16 | the pane's estimate goes back to cost / rate | KILLED | the three-surface test (1) |
| m17b | the lock line's "Needs a group" branch removed | KILLED | 2-of-3 (1) |
| m18 | the marker counts one tier too many | KILLED | `ThePinsOffscreenMarker_…` (1) |

**18 mutants, 18 killed, none hung.** Two first attempts (m06, m17) used a constant `if (false)`; the build refused them
(CS0162 is an error under `TreatWarningsAsErrors`), so they were rerun as m06b and m17b with a non-constant guard.

**Not mutated:**
- `max(1, ceil(shortfall / rate))`: for shortfall > 0 and rate > 0 the ceiling is already ≥ 1, so dropping `max(1, …)` is an
  equivalent mutant.
- The marker's "+N more" path: it is asserted by the marker test's 1-px room case, but not mutated separately.

**Teeth and aim (ADR-015 §7.2).** Each killing test asserts a property the screens ought to have:
- a label true of its expression;
- a truth table kept;
- an ink that follows completion;
- an estimate that equals the engine's End Turns;
- a technology not paid by Civics Points;
- a marker distance equal to the layout's columns.

No kill is a golden comparison.

## 7. Validation

All runs are on this machine, which another lane shares; builds and tests ran under `nice -n 19`.

**Baseline (`51ab032`; run 1's measurement, not re-run).**

| Check | Debug | Release |
|---|---|---|
| Build of `Sim.slnx` | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `Sim.Tests`, Tier 1 filter (57 classes, 15 slow excluded) | 787 passed, 0 failed, 7 skipped of 794 (22 m 51 s) | 787 / 0 / 7 of 794, inside `validate.sh targeted` |
| `Sim.Ui.Tests`, research filter (`Progression`, `Research`, `Civics`, `InfoPanel`, `Readability`, `EffectiveContrast`) | 169 passed | — |
| `Sim.Ui.Tests`, full | — | 1,272 passed, 0 failed, 0 skipped (19 m 40 s) |

On the base, `charters` already FAILed in `validate.sh targeted`, and `uitests-full` was NOT RUN (blocked).

**Tier 1, targeted** (`scripts/validate.sh targeted --base 51ab032`, `TestResults/validation/20261111-142359-targeted`):

| Check | Status | Time | Detail |
|---|---|---|---|
| research-tool-tests | PASS | 55s | |
| research-audit, research-calibration-report, unlock-audit, consequence-audit, tree-closure | PASS | 0s each | |
| charters | FAIL | 0s | pre-existing: identical output on `51ab032` (12 "unknown id" lines in `unit-catalogue-and-progression.md`) |
| doc-links | PASS | 0s | |
| build | PASS | 30s | |
| simtests-selected | PASS | 127s | 805 passed, 0 failed, 7 skipped of 812 (58 classes, 15 slow excluded) |
| uitests-full | NOT RUN | 0s | blocked behind `charters`; run directly at Tier 3 (below) |

An earlier Tier 1 run (`20261111-141841-targeted`) failed one test, a task-caused failure the first run had missed.
`ResearchCandidateTests.CandidateFile_DiffersFromShippedOnlyInTheTuningSwitches_…` failed (804 passed, 1 failed, 7
skipped): the research-economy candidate file had to carry the three corrected `emerged` lines. Fixed in `7f9b1b0`; Tier 1
was then re-run (the table above).

**Tier 2, integration** (`scripts/validate.sh integration --base 51ab032`, `20261111-142746-integration`):

| Check | Status | Time | Detail |
|---|---|---|---|
| banned-constructs | PASS | 0s | |
| read-isolation | PASS | 0s | |
| research-tool-tests, research-audit, research-calibration-report, unlock-audit, consequence-audit, tree-closure, doc-links | PASS (cached) | 0s | inputs unchanged since the Tier 1 run |
| charters | FAIL | 0s | pre-existing, as above |
| build | PASS | 3s | |
| gui-acceptance | NOT RUN | 0s | manual: the `docs/m5-playability-gate.md` checklist |
| simtests-selected | PASS | 1231s | 1,012 passed, 0 failed, 8 skipped of 1,020 (81 classes, slow tests and the determinism suite included) |
| uitests-full | NOT RUN | 0s | blocked behind `charters`; run directly at Tier 3 |

**Debug after the change** (this run):
- Build of `Sim.slnx`: 0 warnings, 0 errors.
- The baseline's research UI filter: 169 passed, 0 failed (the same 169 as the base).
- `ResearchTreeAuditTests`: 30 passed.
- `Sim.Tests` (`ResearchEstimateBankTests`, `ResearchContentTests`, `TreeClosure`, `ResearchCandidateTests`, `InfoQueryTests`):
  104 passed.
- NOT RE-RUN in Debug: the 794-test Tier 1 filter (23 minutes in Debug). Release ran the same filter, plus the new class,
  at Tier 1, and the full suite at Tier 3.

**Tier 3, full**, run under the shared machine lock: `flock …/m7c/fullsuite.lock`, lock held 14:53:45Z–15:43:46Z.
- `scripts/validate.sh full` (`TestResults/validation/20261111-145411-full`). Then, because the pre-existing `charters`
  failure blocks the two suites, both were run directly with validate's own commands (policy §6), one after the other.
  Each direct run was also under `validate.sh`'s machine lock (`/tmp/civdemo-full-suite.lock`). Results:
  `TestResults/validation/rt-tier3-direct/*.trx`.

| Check | Status | Time | Detail |
|---|---|---|---|
| selector-selftest | PASS | 7s | |
| banned-constructs | PASS | 0s | |
| read-isolation | PASS | 0s | |
| research-tool-tests | PASS | 51s | |
| research-audit, research-calibration-report, unlock-audit, consequence-audit | PASS | 0s each | |
| research-audit-tests | PASS | 2s | |
| tree-closure | PASS | 0s | |
| charters | FAIL | 0s | pre-existing, as above |
| doc-links-all | PASS | 0s | every Markdown file under `docs/` and the root |
| readonly-proof | PASS | 2s | |
| build | PASS | 3s | |
| simtests-full | NOT RUN in `validate.sh` (blocked) → **run directly: PASS** | 2007s | **3,448 passed, 0 failed, 36 skipped of 3,484** (33 m 23 s) |
| uitests-full | NOT RUN in `validate.sh` (blocked) → **run directly: PASS** | 914s | **1,302 passed, 0 failed, 0 skipped of 1,302** (15 m 11 s). The base had 1,272; the 30 new are `ResearchTreeAuditTests` |
| bench | NOT RUN | — | heavy, not requested. No simulation hot path changed: the changes are screen code and a display query |
| multi-seed-trial | NOT RUN | — | heavy, not requested; no research cost, tuning or prerequisite changed |
| ci-xproc-determinism, ci-platform-smoke, calibration-nightly | NOT RUN | — | CI-only. They run when the orchestrator pushes; not claimed here |
| gui-acceptance | NOT RUN | — | manual (`docs/m5-playability-gate.md`). The screens were checked with headless captures only (fixes report §3) |

**What was NOT RUN, and why:**
- A full `Sim.Tests` run on the base: run 1 measured the Tier 1 filter only. The policy forbids a pristine full run without a
  failure to classify, and no test failed.
- The Debug full suites: one full suite at a time on a shared machine; Release covers them at Tier 3.
- The heavy, CI-only and manual checks above.

**Deferred:**
- Recording the new tests' durations in `scripts/test-durations.json`: `scripts/` is outside this lane's paths. Left to
  whoever records durations next.
- CI on the pushed commit, for the CI-only rows.
- The Director's playtest of the screens, for gui-acceptance and Longbow.

## 8. Limitations

- **The hover edges are faint.** Uncased edges are under 3:1 on the field in early Ages, the completed ink most (N2). This
  was already true of the prerequisite and dependent inks. *(Superseded for incoming edges in verification round 1, RT-R1-04:
  they are cased now. The dependent edges stay uncased, 1.82–4.50:1 on the field.)*
- **Spine bundles merge.** A spine's lanes are 3 px apart, narrower than a cased pinned edge. The draw order keeps the
  outstanding ink on top, but a bundle still reads as one band (N3).
- **What the estimate leaves out.** It does not count Ideas or exposure credited later, an assist, overflow (off) or a rate
  change. For a civic that is not the one being funded, it is what happens once that civic is ordered. *(Qualified in
  verification round 1, RT-R1-01: that holds for an OPEN civic. A civic not yet open is funded only after its route, so it now
  gets no figure; a technology not yet open keeps its own turns once open, its route not counted (proposal N6).)*
- **The inspector (`InfoQuery`) still flattens** the 4 grouped expressions and 2 entity requirements (N1; out of scope).
- **Longbow** is diagnosed from the repository only; the in-play pane was not seen.
- **No source page was read** for the historical review (every FETCH failed). The evidence is SEARCH and KNOW.
- **The captures** are SVGs from the scratch harness rasterised by headless Chrome. They are layout evidence, not GPU-rendered
  frames.

## 9. Proposals for the Director (nothing applied)

N1–N5 (N6 added in verification round 1, §10) and the content proposals H1, H2, B1, K1, W1, W2, F-R, G and T: fixes report §4, and the
[date review §5](../../../docs/m6-plan/research-date-review-2026-11.md#5-proposals-for-the-director-nothing-applied).

Questions genuinely open (fixes report §5):
1. The Longbow screenshot and build.
2. Whether `InfoQuery`'s labels may be corrected under D-084.
3. The content proposals H1, H2, B1 and K1.

---

## 10. Verification round 1 (2026-11-11)

**Provenance.** The fix agent of round 1 worked in this clone on `lane/RT` from `a7b5faa` (the verifier's pinned tree,
`verify-RT-r1`). It reproduced every finding on `a7b5faa` before changing anything, with a temporary probe test class in
`Sim.Ui.Tests` (kept in the session scratchpad, `m7c/rt-fix1/probe/`, and removed from the clone) and with its own run of the
verifier's five surviving mutants. A finding was fixed only after it was reproduced. Statements this round changes are marked
in place in §0, §2, §3, §5 and §8 above and in the fixes report; nothing earlier was deleted.

### 10.1 Verdicts

| ID | Sev. | Verdict | Reproduction on `a7b5faa` (MEASURED unless marked) | Change | Test that pins it |
|---|---|---|---|---|---|
| RT-R1-01 | minor | **REPRODUCED; fixed** | Stepped world: bank 715.213 CP, 17.242 CP a turn. 476 civics not open (all locked); the shared estimate gave 15 of them "~1 turns". Gift exchange: pane "0 / 183 CP · ~1 turns", engine 2 End Turns. Others, pane against engine: War band ~1 / 46, Guest right ~1 / 3, Initiation rites ~1 / 3, Landscape burning ~1 / 37, Seasonal round ~1 / 43 | `ResearchSnapshot.TurnsOf` gives no figure for a civic of the two-lane model that is not open (`NoEstimateUntilOpen`); the pane writes "no estimate until it opens". Card, inspector and every technology figure unchanged. The overclaims in §0, §8 and fixes §0 and §3.3 are qualified in place | `ACivicNotYetOpen_LockedPartialOrAWaitingDestination_…`: no figure for all 476, the statement on the painted pane in three states (locked, partial, waiting destination), the engine's 2 End Turns for Gift exchange, an open civic keeps its figure, the 511 technologies not open keep ceil(remaining / RP) |
| RT-R1-02 | minor | **REPRODUCED; fixed** | `(seed_drill AND 1 > 0) OR (plough_ard AND horse_collar)` and the `1 > 2` variant load; `PrereqExpr.Of`, `ResearchGraph.Build`, `ResearchSnapshot.Build` and `ProgressionScreen` Refresh + Paint throw `InvalidOperationException`. Base (`51ab032`): no throwing path existed (`PrerequisiteExpression.cs` is new in this lane; DERIVED from the base files, not run) | `PrereqExpr.Of` returns null for an expression it does not model; the pane writes the content's own expression text; the doc comment states the loader's actual rule; fixes §3.1's derivation corrected in place | `AComparisonOfLiterals_WhichTheLoaderAdmits_…` (2 cases): failed on the unfixed product code with the exception (MEASURED), passes now: no tree, kinds true of the Predicate, flat rows, the painted pane shows the text |
| RT-R1-03 | minor | **REPRODUCED** for V07, V08, V13 and V24; **V06 REPRODUCED as a survivor but EQUIVALENT** | Each of V06, V07, V08, V13 and V24 survived the 86-case filter (7.3–8.3 s; bound 81 s, 10× the 8.05 s clean run). V06: no dependent route shares a segment with an incoming route at any of the 1,005 cards of both trees (incoming routes share segments with each other at 775) | Four tests and one geometric pin (below). V06: the code comment and fixes §3.2 no longer claim an order for dependents; the geometry that makes their place immaterial is pinned | V07 `SeedDrillTull_Locked_…`; V08 `ThePane_ShowsTheBank_…`; V13 `TheFourNestedExpressions_PinnedEdges_…` now asserts solid / dashed / dotted, not only agreement with `EdgeDash`; V24 `TheSharedEstimate_PaysACivicAtItsLanesCivicsRate_…` on a snapshot whose RP rate is three times the CP rate; V06 `ADependentsRoute_NeverSharesASegment_…` |
| RT-R1-04 | minor | **REPRODUCED; fixed** | On the field, nine Ages: completed 1.33–3.15:1, prerequisite 1.60–3.87:1, dependent 1.82–4.50:1; completed below prerequisite in every Age. On the dark casing: completed 3.95–4.70:1; casing on the field 5.68–13.81:1 | A hovered card's incoming edges, completed and outstanding alike, are drawn over the dark casing the pinned path uses (`CasedEdge`, the former `PinnedEdge`, at the hover width; arrowhead cased too). Rendering only: no theme token moved. Dependents stay uncased (N2 narrowed to them) | `AHoveredCardsIncomingEdges_AreCasedAsThePinnedOnes_…` (both trees): failed on the uncased path (MEASURED: "law_code <- cuneiform: an uncased incoming stroke"), passes now; it also pins casing on the field ≥ 3:1 and the completed ink on the casing ≥ 3:1 in all nine Ages |
| RT-R1-05 | nit | **REPRODUCED; fixed** (docs) | Fixes §3.2's READING gave one interpretation | Alternatives (b) R2 kept literally on the pinned path, and (c) completion followed into the orange set, each with why it was not applied | — |
| RT-R1-06 | nit | **REPRODUCED** (a), (b), (c); **fixed** | (a) 11 names hold parentheses; 13 of the 96 expression lines name one, e.g. "Lunar calendar AND (Cuneiform (full writing) OR …)". (b) 4 card state lines read "~1 turns" on the stepped world (Council of elders: "Available · ~1 turns"); capture s5 shows it. (c) The 2-of-3 outline begins "or all of:" | (a) `PrereqExpr.NameInExpression` quotes such a name in the pane's line. (b) `InfoQuery.TurnsPhrase` writes "1 turn" / "N turns" for the card, the pane and the inspector. (c) `RequiresRows.HeadPrefix`: "or " heads the alternative groups after the first; the first says "either " at the root and nothing under a "one of:" head. No shipped outline changes (none puts a group first among alternatives) | (a) `TheExpressionLine_QuotesANameWithParentheses…` (96 lines, 13 quoting; painted Civil solar calendar). (b) `OneTurn_IsWrittenSingular_…` (card, pane, inspector; plural with the bank emptied) and `TheInspectorsLine_SaysOneTurnInTheSingular_…` (rig). (c) `AnOutline_NeverBeginsWithOr_…` (3 synthetic expressions, truth tables kept) and the 2-of-3 test's heads |

### 10.2 What changed

`git diff --numstat a7b5faa..HEAD` (code and tests):

| File | + / − | What |
|---|---|---|
| `Sim.Ui/Progression/ResearchNodeViews.cs` | 14 / 2 | `NoEstimateUntilOpen`; `TurnsOf` gives none for a civic not yet open (RT-R1-01) |
| `Sim.Ui/Progression/PrerequisiteExpression.cs` | 32 / 14 | `Of` returns null for an unmodelled expression (RT-R1-02); `NameInExpression` (RT-R1-06 a); `HeadPrefix` (RT-R1-06 c) |
| `Sim.Ui/Progression/ProgressionScreen.cs` | 47 / 17 | the pane's "no estimate until it opens" (RT-R1-01); the unmodelled expression's text (RT-R1-02); hovered incoming edges cased, `PinnedEdge` renamed `CasedEdge` (RT-R1-04); quoted names and `TurnsPhrase` (RT-R1-06); the draw-order comment (RT-R1-03) |
| `Sim.Core/State/InfoQuery.cs` | 5 / 1 | `TurnsPhrase`, used by the inspector's line (RT-R1-06 b). The research estimate only; nothing in `Sim.Core/Kernel` |
| `Sim.Ui.Tests/ResearchTreeAuditTests.cs` | 426 / 5 | 14 new test cases; two strengthened (the dotted-edge theory, the 2-of-3 heads); the outline reader knows "either " |
| `Sim.Tests/State/ResearchEstimateBankTests.cs` | 13 / 0 | 1 new test |
| `Sim.Ui.Tests/ResearchPointDisplayTests.cs` | 2 / 1 | the k5 pin moved (§10.3) |

Docs: the fixes report (in-place marks, the READING's alternatives, N6, §6) and this file; three captures
(`docs/m6-plan/research-tree-fixes-2026-11/r1-*.png`).

Not changed: `research.json`, every route and the layout, every theme token, any simulation state, world hash, golden or
tree-closure hash.

### 10.3 Tests and moved pins

- `ResearchTreeAuditTests`: 30 → 44 cases. `ResearchEstimateBankTests`: 18 → 19. The research UI filter: 86 → 100 cases;
  the `Sim.Tests` filter: 94 → 95.
- **Moved pin** `ResearchPointDisplayTests.CpPerTurnShown_…` (k5): `"~1 turns"` → the line ends `"~1 turn"`. Attribution
  (MEASURED): wording only — the value is the same 1 (`InfoQuery.ResearchTurns` unchanged); mutant F06, which restores the
  plural, fails this test, `OneTurn_IsWrittenSingular_…`, `ThePane_ShowsTheBank_…` (which finds the estimate by its words) and, in `Sim.Tests`, `TheInspectorsLine_SaysOneTurnInTheSingular_…`, and nothing else in the two filters.
- **Strengthened, not moved:** the 2-of-3 test asserted three heads, now their texts (`either all of:`, `or all of:`,
  `or all of:`); the dotted-edge theory asserts the three strokes themselves.

### 10.4 Mutants

Each mutant was applied in this clone, built (Debug, incremental) and run against the research UI filter (100 cases) and, for
`Sim.Core`, the `Sim.Tests` filter (95 cases); the file was then restored from HEAD (`git checkout`), and the runner refused a
file with uncommitted changes. **Bound:** 92 s UI and 114 s `Sim.Tests`, 10× the clean runs measured just before (9.2 s and
11.4 s). None hung; test runs took 8.0–12.3 s.

| # | Mutation (pre-fix behaviour unless noted) | Result | Killed by (semantic tests; failing cases) |
|---|---|---|---|
| F01 | `NoEstimateUntilOpen` never holds: every incomplete civic gets the banked single-node figure | KILLED | `ACivicNotYetOpen_…` (1) |
| F02 | `PrereqExpr.Of` throws on a comparison again | KILLED | `AComparisonOfLiterals_…` (2) |
| F03 | the pane drops the unmodelled expression's text | KILLED | `AComparisonOfLiterals_…` (2) |
| F04 | the hovered incoming edges uncased again | KILLED | `AHoveredCardsIncomingEdges_…` (2) |
| F05 | names with parentheses not quoted | KILLED | `TheExpressionLine_QuotesANameWithParentheses…` (1) |
| F06 | `TurnsPhrase` always plural (card, pane, inspector) | KILLED | UI: `OneTurn_IsWrittenSingular_…`, `CpPerTurnShown_…` (the k5 pin), `ThePane_ShowsTheBank_…` (3); `Sim.Tests`: `TheInspectorsLine_SaysOneTurnInTheSingular_…` (1) |
| F07 | every alternative group headed "or ", the first too | KILLED | `AnOutline_NeverBeginsWithOr_…` (3), the 2-of-3 test (1) |
| F08 | the root's first alternative group loses "either " (its operator unstated) | KILLED | `AnOutline_NeverBeginsWithOr_…` (2), the 2-of-3 test (1) |
| F09 | the pane no longer says why a civic not yet open has no figure | KILLED | `ACivicNotYetOpen_…` (1) |
| V24b | the verifier's V24 on the current text: `TurnsOf` divides a civic by the RP rate | KILLED | `TheSharedEstimate_PaysACivicAtItsLanesCivicsRate_…` (1) |
| V07 | the verifier's: a missing grouped alternative no longer counts in the lock line | KILLED | `SeedDrillTull_Locked_…` (1) |
| V08 | the verifier's: the pane's "Civics Points banked" row removed | KILLED | `ThePane_ShowsTheBank_…`, `ACivicNotYetOpen_…` (2) |
| V13 | the verifier's: a grouped part drawn solid | KILLED | `TheFourNestedExpressions_PinnedEdges_…` (4) |
| V06 | the verifier's: dependents' edges drawn last | **SURVIVED: equivalent** | No visible effect: a dependent's route never shares a segment with an incoming route (MEASURED at all 1,005 cards; DERIVED from `ResearchTreeLayout.Route`: incoming routes end above the card, dependent routes start below it). Pinned by `ADependentsRoute_…`. A test of the command order would have teeth without aim (ADR-015 §7.2), so the claim was withdrawn instead |

**14 mutants: 13 killed, each by a semantic test; 1 equivalent; none hung.** Teeth and aim: each killing test asserts a property
the screens ought to have — no banked figure the engine cannot reach; no exception on content that loads; a completed edge
that reads on every Age's field; a quoted name so that every unquoted parenthesis is a group; "1 turn"; an outline that keeps
its truth table and never begins "or"; a lock line that admits another alternative; the bank beside a civic's estimate; the
three strokes; a civic paid at its lane's CP rate.

### 10.5 Captures

Rendered headlessly by a scratch harness (adapted from run 1's; `m7c/rt-fix1/capture`) on the stepped world, rasterised by
headless Chrome, cropped with PIL. The BEFORE panels are run 2's renders of the code at `a7b5faa`.
- [`r1-estimates.png`](../../../docs/m6-plan/research-tree-fixes-2026-11/r1-estimates.png): an open civic "~1 turns" → "~1 turn";
  a locked civic "no estimate until it opens".
- [`r1-hover-casing.png`](../../../docs/m6-plan/research-tree-fixes-2026-11/r1-hover-casing.png): a hovered card's incoming edge
  uncased, then cased.
- [`r1-quoted-name.png`](../../../docs/m6-plan/research-tree-fixes-2026-11/r1-quoted-name.png): Civil solar calendar's line.

The earlier capture s5's AFTER panel predates this round and reads "~1 turns" (marked in the fixes report).

### 10.6 Validation

All runs on this machine, under `nice -n 19`, after the round's last code commit (`1f9dc60`; the commits after it are docs and
captures). Logs: the session scratchpad `m7c/rt-fix1/closing/`; validator outputs under `TestResults/validation/` in this clone.

**Builds** (`dotnet build Sim.slnx -m:1 -nodeReuse:false`): Release and Debug, 0 warnings, 0 errors.

**Before the tiers:** the research UI filter (100 cases) and the `Sim.Tests` filter (95 cases) passed in Debug; the wider
research UI filter (`Progression`, `Research`, `Civics`, `InfoPanel`, `Readability`, `EffectiveContrast`) passed 213 of 213 in
Debug (the base had 169 under it, run 2 199). Scans: `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` CLEAN (109
files, 0 findings; no prototype file changed); `scripts/check-banned-constructs.sh` OK; `scripts/check-doc-links.py --all` 0
broken (re-run after the last doc edit, below).

**Tier 1, targeted** (`scripts/validate.sh targeted --base 51ab032`, `20261111-172740-targeted`): research-tool-tests,
research-audit, research-calibration-report, unlock-audit, consequence-audit, tree-closure, doc-links and build PASS;
simtests-selected PASS, **1,228 passed, 0 failed, 23 skipped of 1,251** (139 classes, 99 slow excluded; 281 s); charters FAIL
(pre-existing, below); uitests-full NOT RUN (blocked behind charters; run directly at Tier 3).

**Tier 2, integration** (`validate.sh integration --base 51ab032`, `20261111-173322-integration`): banned-constructs,
read-isolation and the research checks PASS; build PASS; simtests-selected PASS, **1,419 passed, 0 failed, 23 skipped of
1,442** (149 classes, slow tests and the determinism suite included; 1,238 s); charters FAIL (pre-existing); gui-acceptance NOT
RUN (manual); uitests-full NOT RUN (blocked; run directly).

**Tier 3, full**, under the shared machine lock (`m7c/fullsuite.lock`; held 17:55:23Z–18:42:12Z for `validate.sh full` and
the Release suites, 18:42:25Z–20:28:44Z for the Debug `Sim.Tests` suite and 20:29Z–20:59:30Z for the Debug `Sim.Ui.Tests`
suite, which was started on its own so that it fitted a tool time window; its first start was stopped within seconds), each
suite also under `validate.sh`'s own lock (`/tmp/civdemo-full-suite.lock`). One full suite ran at a time:

| Check | Status | Detail |
|---|---|---|
| `validate.sh full --base 51ab032` (`20261111-175523-full`) | selector-selftest, banned-constructs, read-isolation, research-tool-tests, research-audit, research-calibration-report, unlock-audit, consequence-audit, research-audit-tests, tree-closure, doc-links-all, readonly-proof and build PASS; charters FAIL (pre-existing) | simtests-full and uitests-full NOT RUN there (blocked behind charters), so both were run directly with its command (`dotnet test <project> -c <cfg> --no-build`, trx to `TestResults/validation/rt-r1-direct/`) |
| `Sim.Tests`, full, **Release** | **PASS** | **3,449 passed, 0 failed, 36 skipped of 3,485** (31 m 18 s). Run 2: 3,448 / 36 of 3,484; the one more is `TheInspectorsLine_SaysOneTurnInTheSingular_…` |
| `Sim.Ui.Tests`, full, **Release** | **PASS** | **1,316 passed, 0 failed, 0 skipped of 1,316** (14 m 31 s). Run 2: 1,302; the 14 more are this round's |
| `Sim.Tests`, full, **Debug** | **PASS** | **3,449 passed, 0 failed, 36 skipped of 3,485** (1 h 46 m) |
| `Sim.Ui.Tests`, full, **Debug** | **PASS** | **1,316 passed, 0 failed, 0 skipped of 1,316** (30 m 43 s) |
| bench | NOT RUN | heavy, not requested: no simulation hot path changed (screen code, a display string) |
| multi-seed-trial | NOT RUN | heavy, not requested: no research cost, tuning or prerequisite changed |
| ci-xproc-determinism, ci-platform-smoke, calibration-nightly | NOT RUN | CI-only; they run when the orchestrator pushes; not claimed here |
| gui-acceptance | NOT RUN | manual (`docs/m5-playability-gate.md`); the screens were checked with headless captures only (§10.5) |

**The charters FAIL is pre-existing (MEASURED this round):** `docs/m7-plan/tools/check_charters.py` gives byte-identical output
(12 "unknown id" lines in `unit-catalogue-and-progression.md`) on this head and on `51ab032`'s `docs` and `Sim.Data/content`
extracted with `git archive` (a scratch extract, removed afterwards). Not this lane's file (N5).

**NOT RUN, and why:** the heavy, CI-only and manual checks above; the two suites inside `validate.sh` (blocked by charters; run
directly instead). **Deferred:** recording the new tests' durations in `scripts/test-durations.json` (`scripts/` is outside
this lane's paths); CI on the pushed commit; the Director's playtest (gui-acceptance, Longbow).

### 10.7 Limitations, readings and proposals of this round

- **A technology not yet open** keeps its own turns at the RP rate once it opens; its route is not counted (as before D-084).
  A civic not yet open now gets none. A route-aware figure is proposal **N6** (fixes §4).
- **READING (RT-R1-01):** no figure for a civic not yet open. Alternative: the base's own-cost figure without the bank
  (bank-free, so too high whenever a bank remains after the route: Gift exchange would read ~11 against the engine's 2).
- **READING (RT-R1-04):** the hover path cases completed and outstanding incoming edges alike, so neither outweighs the other.
  Alternatives: case only the completed edges (then what is done stands out over what is needed), or a darker hover variant of
  the completed ink (a second "completed" colour). On the casing the outstanding prerequisite ink reads at 2.74–3.87:1 (2.74
  in the Medieval Age); the edge itself reads through its casing (5.68–13.81:1 on the field).
- **READING (RT-R1-06 c):** "either " heads the first of the root's alternative groups. Alternative: give the root its own
  "one of:" head row (it would move the two shipped ONE OF outlines one level in).
- **The probe and the captures are scratch tools**, not in the repository.
