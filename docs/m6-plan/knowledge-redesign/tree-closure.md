# Technology and civics tree closure: baseline TC-1 (PROPOSED, 2026-10-26)

**Version.** TC-1 is identified by its two closure hashes (§2), not by a commit. Any commit whose shipped `research.json`
reproduces both hashes carries TC-1. Each later change to node content or dependencies makes a TC-2, TC-3, … row in §5.

**Status.** PROPOSED. It becomes CLOSED when the Director confirms this record.

**What closes.** The technology and civics trees, as a CONTENT-DESIGN milestone: their nodes, dependencies, Age gates,
unlocks, effect declarations and educational text. This is not a claim that every progression mechanic is finished.

**What stays open.**
- The research-economy configuration (switches, pacing scale, approved cost batches). Its default adoption is a separate
  decision: `research-cost-pass/decision-docket.md`.
- The tracked follow-ups in §4.

**Rule after closure.** Node content and dependencies change only for a defect fix or an explicitly approved change. Each such
change is recorded in §5 with its new hashes. No further cost tuning or node redesign without a material defect found by
validation (Director, 2026-10-26).

## 1. Checklist (the Director's closure checklist)

| Item | Status | Evidence |
|---|---|---|
| Technology tree: nodes, costs, prerequisites, Age gates and unlocks final for this milestone | PASS; **costs follow D1** | 525 technologies. Integrity report §2: ids, references, reachability, Age order, unlock references and cost-model consistency all PASS. Costs are outside the closure hashes. The candidate moves 16 costs (b1, b3; 3 of them technologies), and the same checks PASS on it. Whether they ship is the default-adoption decision (D1). |
| Civic tree: nodes, CP costs, dependencies and Age-gate behaviour validated | PASS; **costs follow D1** | 480 civics. Same integrity checks. Age-gate behaviour: the sweep (ADR-048), plus the civics gate rule (ADR-053) proven to respect prerequisites and never starve other goals (`civics-gate-trial.md`; whole-run order invariants, §3). |
| Research economy: overflow, reserve, AI gate goals and the approved cost configuration pass the final four-seed trial | PASS (candidate, not default) | `research-cost-pass/candidate-trial.md`, seeds 1, 2, 42 and 7, 1,630 turns. With the switches, civics A7 = 1.479 and batches b1 + b3, every seed completes every Age. There are no anomaly flags, replay and save/load are exact, and the order invariants hold. b2 and b5 were excluded for measured regressions. |
| Educational content: descriptions present; future-dependent effects explicitly identified, not presented as implemented | PASS | All 1,005 nodes are named and described. The 23 future-only nodes are listed (§2, check 7b); each card shows "not simulated yet" or "arrives with M7" (`InfoQueryTests.TheGapAuditNodes_…`, `FutureEffectsTests`). The 7 nodes without an effect list are live through a gate or a live unlock (check 7, authoritative R1 test). |
| Integrity: no unintended orphan, unreachable prerequisite, duplicate id or broken unlock | PASS | §2. The 3 dead ends (integrated_alliance_command, pilgrimage, road_traffic_regulation) are intended: CR-025 declared them future-dependent (M7/M8/M9), and they have no dependents by design. |
| Regression baseline: tests, determinism, save/load and CI pass; limitations documented | See §3 | Full suites, replay, save/load, CI cross-process and allocation legs, GitHub CI on the final commit. |

## 2. Integrity report

Produced by `tools/tree_closure_check.py`, which exits 1 on any FAIL. Its teeth were proven on a deliberately corrupted copy:
a duplicate id, a broken reference, a cycle, a missing description, a broken unlock and a cost drift were each reported.

The report is reproduced in `tree-closure-integrity.md`.

**Closure hashes** (`Sim.Tests/Systems/TreeClosureBaselineTests.cs`):

| Hash | What it covers | Value |
|---|---|---|
| STRUCTURE | id, key, tree, Age, prerequisites, unlocks, effects and repeatable descriptor of every node, plus the entities | `918502844849f4378fe8a0e06c118bd1c59c476e61400909e852ed12e4b9d1d0` |
| TEXT | name and description of every node | `de341a8eb3a7770ef36ee2585342a2baadf9c131b602b93f8af19a054d1e6c39` |

Costs and tuning are deliberately outside both hashes. A third test proves the projection: a cost or tuning edit leaves the
hash unchanged, and a prerequisite edit moves it.

## 3. Regression baseline

Filled in by the final validation of this branch: see `research-cost-pass/candidate-trial.md` §validation.

## 4. Tracked follow-ups (do not reopen the trees)

| Item | Why it is not a closure blocker | Where tracked |
|---|---|---|
| Technology Age 9 throughput (about 140 turns at any cost) | A capacity limit of one-node-per-lane progression; its fix belongs in the repeatable/progression design | ADR-050 §calibrated trial, queue |
| 23 future-dependent nodes | Explicitly declared; shown "not simulated yet"; realized when their system exists | CR-025 §8, `FutureEffectsTests` |
| papyrus / telephone realized-effect proposals | Optional; would move the default game | decision docket D5 |
| Age 1 residual (not cost-bound), seed spread | Pacing, not content | pacing-recalibration.md §3, queue |
| 17 costs one rounding step (+10) above the formula | Authored precision (at most 0.07 %); cost and content_cost agree | integrity report, check 10 |

## 5. Change log after closure

| Date | Change | Kind (defect / approved) | New STRUCTURE / TEXT hash |
|---|---|---|---|
| — | — | — | — |
