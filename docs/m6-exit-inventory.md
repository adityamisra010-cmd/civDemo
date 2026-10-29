# M6 exit inventory: Knowledge / Research / Technology (prepared 2026-10-28 for the Director's exit gate)

**Status:** TECHNICALLY READY FOR THE EXIT GATE. M6 is NOT closed: the human and external gates in §4 are open, and
closing the milestone is the Director's ruling.

**Tree:** branch `claude/new-session-yavghm`, HEAD `0bf7626`, plus the uncommitted working tree. That working tree holds two layers:
- the M7 pass, preserved (`docs/m7-plan/m7-execution-pass-report.md`);
- the M6 closure (D-066).

**Not done:** nothing is committed, pushed or merged (D-066 R7).

**Audit method:** an independent, read-only audit of every §5 criterion and every task row against the exact tree
(2026-10-28). Its ten record defects were fixed before this inventory was written.

## 1. Exit criteria (m6-spec §5, as amended by D-062 and D-066)

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | Technology tree preserved | PASS | `TreeClosureBaselineTests` (STRUCTURE/TEXT constants unchanged by the closure); all four `research-*.py --check`; research.json changed only outside the hashes (Eureka conditions, `foreignExposure`; ADR-058) |
| 2 | Civics a separate domain | PASS | `CivicsPointsTests` (identity every step, seeds 1/2/42/7), `ShippedResearchPointLossIdentityTests`, `CivicsInvariantTests.NoCurrencyGatesAnything_…`, turn-exact civic pin; CP-rate formula in `research-calibration-report.md` |
| 3 | Civic effects, states, forms | PASS (scope A13) | `CivicState` without "Adopted"; `CivicsInvariantTests` (Age entry grants no form; I-7 optionality and its validator); `RegimeQueryTests`; forms "Available — adoption in M8" |
| 4 | Fog | PASS for the minimal fog; movement items DEFERRED (R1) | `DiscoveryExtentControlChangeTests`, `KnowledgeLeakTests` on real fog (the stub only as a control leg), `PolityKnowledgeTests` (A18), ADR-044 dated note (bench, mutant verdict) |
| 5 | E1 at the Director's visual gate | OPEN: HUMAN GATE | `e1-visual-acceptance-checklist.md`; `ObjectLayerTests`; frame budget: §3 |
| 6 | Export | PASS (opening it in Excel is a human check) | `SessionExportTests`: the Director's 68-turn session and a current-tree session, byte-identical; CI step |
| 7 | Unit progression | PASS (A11) | `UnitProgressionContentTests` (49 mutants), `UnitProgressionInertTests`, `UnitTrainingPlaceholderTests`; kind 10 reserved |
| 8 | Content validation | PASS (A13) | `research-consequence-audit.py --check` (zero unclassified items, in CI); `ActionSurfaceAcceptanceSweepTests` (UI offered = simulation accepted, every order kind) |
| 9 | Persistence / replay / determinism | PASS locally; Windows smoke and 20-seed nightly are EXTERNAL | §2; populated-table tests v35–v39; `golden-attribution-register.md`; schema v39, readable set {32…39}, an ADR per bump (054, 055) |
| 10 | Slice acceptance; handoff; §7 decisions | OPEN: HUMAN GATE (playtest) and DIRECTOR (A1–A19 confirmation) | `m6-playtest-checklist.md` (acceptance set: rows 1, 2, 3, 5, 10); `m7-handoff-register.md` (single owner per row) |

Task rows M6.1–M6.24: `m6-task-register.md` dated note 2026-10-28.

## 2. Validation evidence (final integrated tree)

| Check | Result |
|---|---|
| Build (Release) | 0 warnings, 0 errors |
| Sim.Tests, full | **3,361 passed, 0 failed, 36 skipped** (measurement rigs), 1 h 3 min |
| Sim.Ui.Tests, full | **1,200 passed, 0 failed, 0 skipped**, 24 min |
| CI determinism harness (T0.8), local | 78/78 passed |
| CI cross-process legs (T0.9), local | PASS, whole leg in a clean directory (`m6-determinism-xproc-3.clean.log`): orderless and replay (400 turns), founded orderless + pinned golden `d5ebd0b2`, seam seed 16, founded replay, found-settlement (two processes, turn-exact delivery, replay, inspect VERIFIED, 301 turns), AI-world (351 orders, kinds 3–9 and 12–16, two processes, replay, inspect VERIFIED, 601 turns) |
| CI allocation leg (D-055), local | PASS (`m6-determinism-xproc-4.rerun.log`): the split binds; holds bind younger heads; A' = A; B = A's tail; replay, hashes and twins agree; the b = 70 order changed spending in its own step |
| 20-seed soak (`autoplay --seeds 20 --turns 650` + `corridors`), local | autoplay exit 0: all 20 seeds reach year 4500. `corridors` exit 0 (quarantined corridors do not gate, T3.12). `densityPerArableKm2` QUARANTINED: 16/20 seeds in the target band, 20/20 in the recorded window. **`migrationGrossPerDecade` QUARANTINE DRIFT:** measured 0.00018–0.00035 against window [0.0009, 0.01], 0/20 seeds in the window. Pre-existing quarantine (M4 CR-002; M5 measured 0.00043–0.00091, 1/20); it drifted further on this tree, and the cause is NOT attributed. Director ruling needed (§4) |
| Scripts | banned-constructs, read-isolation, 4 research `--check`, consequence audit, codebook `--check`, charters, tree closure, `validate.py --live`, doc links, 58 script unittests: all pass |
| NOT run | Windows smoke (CI-only, needs a push); `sim bench` on the final tree (the discovery bench is in ADR-044); manual GUI and visual acceptance |
| Pre-existing, out of the gate | `research-proposal-snapshot` (historical proposal-graph checks; D-066 A12) |

## 3. Known-open items (not defects of the delivered scope)

1. **E1 frame budget.** The self-set 2.0× budget is exceeded in 20 of 48 rows. All of them are near-empty maps, at ≤ 2.8 ms per frame; large worlds are unchanged. Director's choice (`e1-object-layer-note.md`).
2. **Contact exposure is dormant in canonical worlds** (no foreign trade before about turn 540). Director question: proximity contact (`queue.md`).
3. **The founding legality rule reads unseen land,** for player and AI alike. Moved to M7 (DW-002).
4. **M6.24 / DW-036 (D-051 §2/§4)** is not implemented. It needs a Director mechanism ruling.
5. **Six `frontier_*` repeatables** carry both a completion delta and a per-level delta on one operand. Both channels are declared; content call (AU-6).
6. **The law court reaches AI worlds only after about turn 500.** Soldiers do not die while serving (ADR-055 §4, M7.4). EC packets are rarely reached in normal play.
7. **Educational corpus:** 1,005 entries, 0 source-checked. Promotion needs page reads, and only GitHub is reachable; the exact ids are in `educational-content-corpus.md`. Not an exit item (D-062 note, 2026-10-23).
8. **Migration corridor drift.** `migrationGrossPerDecade` (quarantined since M4 CR-002) fell further, to 0.00018–0.00035, outside its recorded window on all 20 seeds. It does not gate (T3.12). The cause is not attributed; candidates are the M7 pass and the M6 closure, both on this tree.

## 4. Gates that remain (human, external, Director)

1. The Director's playtest of the slice (`m6-playtest-checklist.md`).
2. The Director's D-038 Part G visual gate (`e1-visual-acceptance-checklist.md`) and the frame-budget choice.
3. Opening an exported workbook in Excel / LibreOffice.
4. Windows smoke and the 20-seed nightly in CI. Both need **commit and push authorization**; the push also produces the Windows playtest build.
5. Confirmation of D-066 A1–A19 (ADOPTED, reversible).
6. A ruling on M6.24.
7. Ratification status of the preserved M7 pass's ADR-055/ADR-056.
8. A ruling on the migration corridor drift (§3 item 8): re-window, attribute or accept.
9. The exit ruling, and the CLAUDE.md "Current milestone" line (the Director's to change).

## 5. Deferred work and M7 handoff

- **Fog deferral:** D-066 §3 and its 2026-10-28 dated note. Each item has a reason, a dependency and a single owner, mirrored in `deferred-work-register.md`.
- **Handoff:** `m7-handoff-register.md` (H7-01…H7-14, one owner each).
- **Entities:** `deferred-entity-dispositions.md` (135 entities).
- **Eurekas:** `eureka-evaluability-register.md` (86).
