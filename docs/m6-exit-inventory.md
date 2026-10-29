# M6 exit inventory: Knowledge / Research / Technology (prepared 2026-10-28 for the Director's exit gate)

**Status (updated 2026-10-28, D-067 pass):** TECHNICALLY READY FOR THE EXIT GATE. M6 is NOT closed. The Director's
rulings (D-067) are recorded, and the final tree is re-validated (§8). The human and external gates in §4 / §8 are still
open, and closing the milestone is the Director's ruling. Nothing is committed, pushed or merged.

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
| 20-seed soak (`autoplay --seeds 20 --turns 650` + `corridors`), local | autoplay exit 0: all 20 seeds reach year 4500. `corridors` exit 0 (quarantined corridors do not gate, T3.12). `densityPerArableKm2` QUARANTINED: 16/20 seeds in the target band, 20/20 in the recorded window. **`migrationGrossPerDecade` QUARANTINE DRIFT:** measured 0.00018–0.00035 against window [0.0009, 0.01], 0/20 seeds in the window. Pre-existing quarantine (M4 CR-002). ~~M5 measured 0.00043–0.00091, 1/20; it drifted further on this tree~~ **Corrected 2026-10-28:** 0.00043–0.00091 was the M5 *hardening* reading; the M5-exit (D-054 batch 3) range was 1.7256e-4–3.5233e-4, so this tree is unchanged from M5 exit. The cause of the M5 drop is NOT attributed. **Accepted as design by the Director (D-067 R11)** |
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
8. **Migration corridor.** `migrationGrossPerDecade` (quarantined since M4 CR-002) reads 0.00018–0.00035, outside its recorded window on all 20 seeds. It does not gate (T3.12). ~~It fell further; the candidates are the M7 pass and the M6 closure.~~ **Corrected 2026-10-28:** the drop happened during M5 (D-054 batch 3: 1.7256e-4–3.5233e-4); this tree did not cause it. The cause is not attributed. **ACCEPTED as design, not as a causal explanation (D-067 R11–R12).**

## 4. Gates that remain (human, external, Director)

1. The Director's playtest of the slice (`m6-playtest-checklist.md`).
2. The Director's D-038 Part G visual gate (`e1-visual-acceptance-checklist.md`) and the frame-budget choice.
3. Opening an exported workbook in Excel / LibreOffice.
4. Windows smoke and the 20-seed nightly in CI. Both need **commit and push authorization**; the push also produces the Windows playtest build.
5. ~~Confirmation of D-066 A1–A19 (ADOPTED, reversible).~~ **DONE: RULED, D-067 R1–R10.**
6. ~~A ruling on M6.24.~~ **DONE: DEFERRED until after the Director's M6 playtest, no milestone named (D-067 R19); not an exit criterion.**
7. ~~Ratification status of the preserved M7 pass's ADR-055/ADR-056.~~ **DONE: ADR-055 RATIFIED (D-067 R15); EC-1, EC-2 and EC-3 RATIFIED separately (R16–R18).**
8. ~~A ruling on the migration corridor drift (§3 item 8): re-window, attribute or accept.~~ **DONE: ACCEPTED as design, not as cause (D-067 R11–R12).**
9. The exit ruling, and the CLAUDE.md "Current milestone" line (the Director's to change).

## 5. Deferred work and M7 handoff

- **Fog deferral:** D-066 §3 and its 2026-10-28 dated note. Each item has a reason, a dependency and a single owner, mirrored in `deferred-work-register.md`.
- **Handoff:** `m7-handoff-register.md` (H7-01…H7-14, one owner each).
- **Entities:** `deferred-entity-dispositions.md` (135 entities).
- **Eurekas:** `eureka-evaluability-register.md` (86).

## 6. The two-layer tree: commit separation (prepared 2026-10-28, D-067 R20; NOT performed, no commit authorized)

**Verified layer trees** (`git write-tree`; the M7-only tree also from the dev worktree as it stood before the M6 copy):

| Layer | Tree | Contents | Validation of exactly this layer |
|---|---|---|---|
| base | `0bf7626` (commit) | the last pushed commit | CI on GitHub |
| M7 execution pass | `cb2eb1db` | 217 files against `0bf7626` | Tier 3 recorded in `m7-plan/m7-execution-pass-report.md` (Sim.Tests 3,219 passed / 0 failed / 36 skipped; Sim.Ui.Tests 1,126/1,126). That report records no tree hash, so its identity with `cb2eb1db` rests on the dev worktree being untouched since; it is **not** re-run in this pass. |
| M6 closure as validated | `c05878de` | a further 202 files (47 overlap the M7 layer) | Tier 3 + CI legs + soak, 2026-10-28 (§2). |
| D-067 exit-governance pass | the final tree (§8) | docs, `sim.json` description strings, and comment-only code/test edits | §8 (re-validated in this pass). |

**Feasibility.** The layers can be committed separately **without touching any worktree file**, because every layer
already exists as a tree object; no reconstruction or re-application of patches is needed. The procedure below runs in the
branch checkout `/home/user/m5w/dev` (branch `claude/new-session-yavghm`, HEAD `0bf7626`, nothing staged), whose working
tree must first equal the final tree (verified by a temporary-index `git write-tree`).

```bash
cd /home/user/m5w/dev
test "$(git rev-parse HEAD)" = 0bf76260f6156f917019fe1fb1aafa04845fbe53      # precondition
git diff --cached --quiet                                                  # precondition: nothing staged
T=$(mktemp -u); cp "$(git rev-parse --git-dir)/index" $T
GIT_INDEX_FILE=$T git add -A; FINAL=$(GIT_INDEX_FILE=$T git write-tree); rm -f $T
test "$FINAL" = "<the final tree hash stated in the hand-back of the D-067 pass>"   # the tree that was validated
C1=$(git commit-tree cb2eb1db6aeb977ac78c90fed27bac73f49eec75 -p 0bf7626 -F msg-1-m7.txt)
C2=$(git commit-tree c05878deb9cb0d109b825ba08eb7073ba38c786a -p "$C1" -F msg-2-m6-closure.txt)
C3=$(git commit-tree "$FINAL" -p "$C2" -F msg-3-m6-exit-governance.txt)
git update-ref refs/heads/claude/new-session-yavghm "$C3" 0bf76260f6156f917019fe1fb1aafa04845fbe53   # compare-and-swap
git read-tree HEAD          # index := HEAD; the working tree already equals HEAD, so nothing is lost
git status --short          # expect: clean
git push -u github claude/new-session-yavghm                               # the remote is named "github"
```

- **Consequences.**
  - Nothing is rewritten: `0bf7626` stays the parent, and the branch moves forward by fast-forward.
  - CI runs only on the pushed head (`C3`). The intermediate commits `C1` and `C2` are validated only by their recorded
    local runs. Re-running Tier 3 on `cb2eb1db` (about 1.5 h) would make `C1`'s evidence fresh; it is optional.
- **Alternative (two commits).** Use `C1` as above, then `C2' = commit-tree "$FINAL" -p "$C1"`. This folds the governance
  pass into the closure commit. The three-commit form keeps the validated closure tree `c05878de` as its own provenance
  point.
- **Remote naming.** The remote named `origin` (`refs/remotes/origin/…` at `93270cd`) is stale. The live remote is
  `github` (`refs/remotes/github/claude/new-session-yavghm` = `0bf7626`).
- **Status:** prepared only. No `commit-tree`, `update-ref`, stage or push has been run.

## 7. The Director's M6 playtest: executable plan (not held; nothing here is a result)

- **Build:**
  - after push authorization (§6), the CI artifact `sim-ui-win-x64-<sha>` (`ui-artifact` workflow, with its
    `windows-smoke` job); or
  - from source on the final tree: `dotnet run --project Sim.Ui -c Release -- --seed 42 --ai-empires 1` (Linux or
    Windows with .NET 10).
- **Seeds:** 42 is the default and the one every pin is measured on. Use one more seed (e.g. `--seed 7`) for a second
  impression.
- **Saves:** sessions from earlier builds are refused on resume. Start a new game.
- **Duration:** about 1–2 hours. Quit normally at the end, so the session workbook is written.

**Acceptance set (T6.18: at most five; rows of [m6-playtest-checklist.md](m6-plan/m6-playtest-checklist.md) §1):**

| Row | Scenario | PASS when | FAIL when |
|---|---|---|---|
| 1 | Research and civics points | RP and CP are shown per turn in two lanes, CP banks with no civic target, and base RP = base CP at every turn length (ADR-054) | either lane missing, CP lost with no target, or base RP ≠ base CP |
| 2 | Civic states | completed civics read "Researched", and forms read "Available — adoption in M8" | anything reads "Adopted", or a form can be adopted |
| 3 | The law court (vertical slice) | not offered before `law_code`; offered after; builds, is founded the step after completion and matures; the lens walks every state; the label becomes "law-giving chiefdom" | offered or accepted before `law_code`; never founded; the label never changes |
| 5 | Explore / Prospect / fog | Explore widens the known extent at a food cost; Prospect reveals concealed deposits; unknown land stays unknown; rivals are seen only where visible | a concealed deposit or an unseen rival is revealed without discovery |
| 10 | Session export | quitting writes `session-<stamp>.xlsx`; **Excel** opens it with eight sheets and no repair prompt | no file, a repair prompt, or missing sheets |

**Measurements to record (not acceptance criteria):**
- the turn `law_code` completes and the turn the court is founded (the early-court claim is unmeasured);
- the eight-part cap: is a settlement with every building kind legible at settlement zoom (D-067 R3)?
- frame feel on near-empty versus busy maps (the E1 budget);
- any information leak (seeing what fog should hide).

**Disclosures before play.** Judge these; don't report them as new. All are in the checklist §2.
- Movement-dependent fog is deferred to M7.
- The founding predicate reads unseen land (M7).
- **Stone-poor settlements** (D-051 §2 deferred, D-067 R19).
- **No Workshop before bronze** (D-051 §4 deferred, D-067 R19).
- Contact exposure is dormant in ordinary play.
- The AI builds courts only after about turn 500.
- Soldiers are immortal until M7.4.
- The founding warband draws 20 people from the capital.
- The EC packets are dormant in ordinary play.
- Migration is free across all borders, at the accepted lower rate.
- The E1 frame budget is exceeded on near-empty maps.
- Exporting at quit takes about 25 s for 600 turns.

**Record:** OK, a defect (with a screenshot), or a design note, per row. Attach the session folder.

**Verdict rule:** the slice is accepted when rows 1, 2, 3, 5 and 10 are PASS. Any FAIL is triaged as an M6 defect or a
deferred item before the exit ruling.

## 8. Re-validation of the final tree and the exit matrix (2026-10-28, D-067 pass)

**Tree under test:** worktree `/home/user/m5w/wt-m6`, `git write-tree` of the full working tree = `e14f5d96`
(`TestResults/validation/exit/tree-under-test.txt`). That tree already holds every code, test, content and ADR change of this
pass. After the tests ran, only these documents changed, and no test reads them (checked: the only document-reading test,
`UnitProgressionInertTests`, reads the unit-progression contract, untouched):
- `m6-exit-inventory.md`;
- `current-state.md`;
- `d067`.

**What changed against the previously validated `c05878de`:**
- documentation;
- `sim.json` `_doc` strings (structurally verified: no value changed);
- comment-only edits in four `Sim.Core` and three `Sim.Tests` files.

All logs are under `TestResults/validation/exit/` (git-ignored).

### 8.1 Results (commands from `.github/workflows/ci.yml`, run locally on Linux, .NET 10.0.112)

| Check (exact command) | Result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 warnings, 0 errors |
| CI script steps 1–13 | all exit 0 |
| └ steps 1–2 | banned constructs; read isolation |
| └ steps 3–6 | 4 research `--check` / audit (consequence audit: every rule PASS, zero unclassified) |
| └ step 7 | audit mutant tests |
| └ step 8 | tree closure: no blocking defect |
| └ step 9 | charters |
| └ step 10 | `validate.py --live` + fixtures/mutants; report unchanged |
| └ step 11 | codebooks v4, v5 current |
| └ step 12 | selector self-test |
| └ step 13 | doc links: 430 files, 0 broken |
| `dotnet test Sim.Tests --filter FullyQualifiedName~UnitProgression` (step 17) | 60 passed, 0 failed |
| `dotnet test Sim.Ui.Tests --filter FullyQualifiedName~SessionExportTests` (step 18) | 23 passed, 0 failed |
| `./scripts/check-readonly-proof.sh` (step 20) | OK (CS0200, CS1061) |
| `dotnet test Sim.Tests --no-build -c Release` | **3,361 passed, 0 failed, 36 skipped** (measurement rigs; not counted as passes), 59 min |
| `dotnet test Sim.Ui.Tests --no-build -c Release` | **1,200 passed, 0 failed, 0 skipped**, 22 min |
| Determinism harness T0.8 (`--filter suite=determinism`) | **78/78 passed** |
| Cross-process T0.9 (whole step, clean directory) | PASS: orderless + replay 400 turns; founded + pinned golden `d5ebd0b2`; seam seed 16; founded replay; found-settlement (inspect VERIFIED, 301 turns); AI world (351 orders, kinds 3–9 and 12–16, inspect VERIFIED, 601 turns) |
| Allocation D-055 (whole step) | PASS |
| Calibration battery (`--filter CalibrationBatteryTests`) | 7 passed, 0 failed |
| 20-seed soak (`autoplay --seeds 20 --turns 650` + `corridors`) | autoplay exit 0, **20/20 seeds reach year 4500** (per-seed hashes identical to the earlier soak). `corridors` exit 0: density QUARANTINED (16/20 in band, 20/20 in window); migration 1.8083e-4–3.5084e-4, outside its window — accepted (D-067 R11) |
| `sim bench --founded --seed 42 --turns 300 --json` × 3 | totals 28,060 / 28,381 / 28,204 ms (median 28,204 ms; ADR-044's track-tree median was 31,971 ms under higher load — indicative) |
| `sim-ui --smoke --ai-empires 1 --monkey 200` under `xvfb-run` (virtual display, software GL) | **SMOKE PASSED**: 11 states, 3,425 checks, 0 FAIL, 110,108 frames, 2,200 monkey actions. Automated; **not** a manual or visual acceptance |
| Session workbook | `sim run --founded --seed 42 --turns 150 --ai-empires 1 --emit-session` + `sim-ui --export-session` → `.xlsx` (self-check passed), **opened in LibreOffice Calc 24.2.7.2 headless**: all eight sheets read, 4,587 cells identical to the written XML, 0 mismatches. **Excel itself is NOT available here and was NOT run** |
| `sim-ui --e1-preview --quick` (xvfb) | 19 PNG/SVG frames rendered for the visual gate (session scratchpad). Rendering only; no visual judgement claimed |
| `validate.py` proposal-graph snapshot (out of the gate, D-066 A12 / D-067 R1) | FAILS on the base `0bf7626` (341 errors) and on this tree (357 errors): pre-existing, by construction |

**Process note.** The first soak attempt was run from a symlinked scratch directory. `dotnet run` rebuilt through the
symlink path and failed to build (CS0006). That attempt produced no result. After a clean rebuild at the real path, the
soak was rerun from the repository root, which is how CI runs it.

### 8.2 Exit matrix (m6-spec §5 as amended by D-062, D-066 and D-067)

| # | Criterion | Status | Evidence / owner |
|---|---|---|---|
| 1 | Technology tree preserved | **PASS** | 8.1 research checks and tree closure; TC-1 constants unchanged |
| 2 | Civics a separate domain | **PASS** | full Sim.Tests (`CivicsPointsTests`, `ShippedResearchPointLossIdentityTests`, …) |
| 3 | Civic effects, states, forms | **PASS** (scope: D-067 R5) | `CivicsInvariantTests`, `RegimeQueryTests`; forms "Available — adoption in M8" |
| 4 | Fog | **PASS** for the minimal fog; movement-dependent items **DEFERRED** (D-066 R1); claim/trade/road routing **DEFERRED** to M7 (D-067 R10) | `KnowledgeLeakTests`, `DiscoveryExtentControlChangeTests`, `PolityKnowledgeTests` |
| 5 | E1 at the visual gate | **NOT RUN — REQUIRES DIRECTOR** (Part G visual gate; the eight-part cap is provisional until it, D-067 R3; frame-budget choice) | `ObjectLayerTests` pass; preview frames rendered |
| 6 | Export | **PASS** (automated + LibreOffice open); **Excel open NOT RUN** | `SessionExportTests` 23/23; LibreOffice 4,587/4,587 cells |
| 7 | Unit progression | **PASS** | step 17: 60/60 |
| 8 | Content validation | **PASS** | consequence audit; `ActionSurfaceAcceptanceSweepTests` |
| 9 | Persistence / replay / determinism | **PASS locally**; Windows smoke + CI nightly **NOT RUN** (need a push) | T0.8 78/78; T0.9; D-055; populated-table tests |
| 10 | Slice acceptance; handoff; decisions | handoff **PASS**; decisions **RULED** (D-067); playtest **NOT RUN — REQUIRES DIRECTOR** (§7) | `m7-handoff-register.md`; §7 plan |
| — | M6.24 / D-051 §2, §4 | **DEFERRED** by ruling (D-067 R19); not a criterion | — |
| — | Migration corridor | **ACCEPTED** by ruling (D-067 R11); report-only | 8.1 soak row |

**FAIL rows:** none inside the gate. The only failing check is the proposal-graph snapshot, which is pre-existing and
outside the gate.

**Genuine M6 code blockers found:** none.
