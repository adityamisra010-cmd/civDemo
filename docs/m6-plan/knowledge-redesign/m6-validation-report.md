# M6 slice — validation report (evidence)

Branch `claude/new-session-yavghm`. Baseline before this work: `06e0153` (CI run 39801418809 and ui-artifact run
39801418680, both success). Implementation integrated at `1fb5fdf` (merge of `050e1ea` = Track EX + Track CP
(`5af05ad`) + Track TREE (`ad21520`), on top of the governance commits `490941d`, `4cb34db`). `git diff 050e1ea
1fb5fdf` over `Sim.*`, `.github`, `scripts` is empty (0 lines): the merge added documents only. `main` = `m5-exit` =
`6e51a3e`, untouched.

## 1. Local checks on the integrated tree (`1fb5fdf` code; run 2026-10-23)

| Command | Exit | Result |
|---|---|---|
| `dotnet build Sim.slnx -c Release` | 0 | 0 errors |
| `./scripts/check-banned-constructs.sh` | 0 | OK |
| `./scripts/check-read-isolation.sh` | 0 | OK |
| `python3 scripts/research-content-audit.py --check` | 0 | current |
| `python3 scripts/research-calibration-report.py --check` | 0 | current |
| `python3 scripts/research-gameplay-unlock-audit.py --check` | 0 | current |
| `python3 -I docs/m6-plan/knowledge-redesign/tools/implement_tranche.py --check` | 0 | up to date |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Tests | 0 | 2,707 passed, 13 skipped, 0 failed (2,720) |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Ui.Tests | 0 | 1,099 passed, 0 failed |

Run by the implementation agent on `050e1ea` (same code), from scripts extracted from the final `ci.yml`: the
cross-process determinism step (found-settlement leg agrees over 300 turns; AI-empire leg agrees over 600 turns; run
log 400 orders, kinds {3–9, 12, 13, 14, 15}) and the allocation leg (4 split-checks PASS) — exit 0 each.

## 2. What the tests pin (new test classes)

- **Civics Points:** `CivicsPointsTests` (lanes, turn-exact civic-target delivery, banking, dt scaling, flooring,
  tie-dense AI lanes, switch, the identity `balance = generated − spent` every step on seeds 1, 2, 42, 7),
  `CivicsPointsSchemaV35Tests` (populated table, exact length, bit-exact round-trip, hash, v34 migration, refusals),
  `CivicsPointsAttributionTests`.
- **Stage gate:** `ResearchContentTests` (stage holds without `legal_code_roman`); `ResearchUnlockPipelineTests` T14
  (the university still needs it).
- **Discovery:** `DiscoveryTests` (concealment reproducibility, tie-dense ranking, realized share pinned per seed,
  deposit table unchanged by concealment, Prospect and Explore turn-exact pins, ledger-exact grain sinks, refusals
  charge nothing, no repeat survey, concealed row produces nothing before and produces after, explore radius growth
  and cap, extent monotone, cylinder wrap, founding refused on unknown land, sighting memory), `DiscoveryLeakTests`
  (metamorphic: perturbing unknown land, undiscovered concealed deposits and unseen rival settlements leaves AI orders
  and UI output identical, with positive controls), `DiscoverySchemaV35Tests` (sections 63–66 populated),
  `DiscoveryOrderLogTests`, `DiscoveryLayoutAttributionTests`, `DiscoveryActionsUiTests` (Actions menu listing, costs,
  blockers, emit guards, the "last turn" refusal line; Construction still lists Build Granary).
- **Content tranche:** `ResearchContentTests` (counts, fixture identity, D-047 chronology), the `PreTreeTranche` twin.

Realized concealment share over eligible ore rows (pinned):

| Seed | Turn 0 | Turn 300 (AI world, colonies included) |
|---|---|---|
| 1 | 6/24 = 25.0 % | 9/38 = 23.7 % |
| 2 | 7/24 = 29.2 % | 10/38 = 26.3 % |
| 42 | 7/24 = 29.2 % | 10/38 = 26.3 % |
| 7 | 6/24 = 25.0 % | 10/38 = 26.3 % |

## 3. Golden re-pins and their attribution

| Golden | v34 (pre-M6) | Final | Cause |
|---|---|---|---|
| Founded t300 (= ci.yml `FOUNDED_GOLDEN`) | `0f29efc7` | `8ccbe80f` | CP account + discovery rows/effects |
| Toy t200 | `2e6d4461` | `f97432f0` | layout only (empty sections 62–66) |
| Driven t300 | `eb88663a` | `392e2806` | CP + discovery |
| First reign t40 | `f7ca60a5` | `e3a86ce6` | CP + discovery |
| AI world t300 | `25f4799d` | `776d5d0c` | CP + discovery + content tranche |

Control: `TestConfigs.PreM6` (Civics Points off, discovery off, pre-M6 `research.json` fixture) reproduces every v34
value after `SchemaProjection` strips sections 62–66; single-layer switches (`PreTrackCp`, `PreStageGate`,
`PreTreeTranche`, discovery off) attribute each move. The stage-gate change alone moves no golden.

## 4. Measured gameplay consequences (MEASURED, AI empire, canonical seed)

| Pin | Pre-M6 | Final |
|---|---|---|
| First research targets | knapping_oldowan, ground_stone_early | grinding_stone (tech lane), kin_council (civics lane) |
| Advance to A2 | turn 221 | 111 |
| First road | 134 | 166 |
| A3 entry (first tax levy the turn after) | 422 | **579** |
| Foundings by turn 300 (settlements) | 8 (14) | 10, first at 38 (16) |
| First build | 2 | 2 |

*Corrected 2026-10-23 (Director review item 2).* An earlier version of this paragraph attributed the A3/levy delay to
the CP economy pacing `taxation`. That was wrong: the Taxation civic completes **earlier** under M6 (turn 321 vs 336),
and the levy follows Age III entry, which follows the AI's bronze node (421 → 578). Removing one layer from the shipped
world: CP lanes off → bronze 424, tree tranche off → 466, discovery off → 542. The CP layer's share comes from AI goal
ordering — in the two-lane model the Technology lane works the paved-road goal (turns 334–483) before bronze, because
goal closures union every OR branch. Trace, measurements and mechanism: `audit/taxation-gate-trace.md`. Flagged for
the Director (roadmap priority 1) and `docs/queue.md`. These are single-seed measurements of the canonical AI run, not
a balance study.

## 5. Not covered or still open

- **CI on the pushed head**: recorded in the final report once the runs finish (workflow IDs).
- `--without-discovery` remains on the D-055 allocation leg and `CliConstructionLifecycleTests` (pre-M6 measurements).
- The calibration population trace is stale (predates M6); not re-measured here.
- ~~`docs/m5-playability-gate.md` §6 not regenerated for the new gate rows.~~ Regenerated 2026-10-23 (§6 below).
- The educational corpus remains blocked from shipping (0 of 561 source-checked; source hosts unreachable).
- No Windows smoke or 20-seed soak run locally; they run in CI (`ui-artifact`, nightly) only.

## 6. Director review of the batch (2026-10-23)

The five conditions the Director set before accepting the batch, and what answers each. Branch head after this review:
the commit carrying this section (SHA in the commit log; not self-referenced). `main` = `m5-exit` = `6e51a3e`, untouched.

| # | Condition | Answer | Evidence |
|---|---|---|---|
| 1 | Test the validator adversarially | `tools/test_validate.py`: 25 fixtures run the real `validate.py` on temp copies, one planted defect each; every failing fixture fails for the intended check and no other; every valid control passes. `--mutants`: 15/15 killed | legacy civic/technology renumbered (V14, V23); new civic keyed 1000 (V23); optional civic, regime form and case-study node made mandatory through the stage, an Age CORE fact, `sim.json`'s tax gate and `research.json`'s university gate (V24); regime form in a supporting/core Age fact (V25, V07); six evidence mislabels (C03, C11); valid: unchanged copy, honest downgrade, disputed claim, page-read upgrade (1 entry ships), required node added to a gate, source added to an unverified entry (stays blocked), optional node as one OR branch |
| 2 | Resolve the taxation discrepancy | the brief was right; the code matches the proposal; the validator was reading the wrong relationships (and hard-coded the gates). The levy delay is caused by Age III bronze, not taxation | `audit/taxation-gate-trace.md`; §4 above corrected |
| 3 | Keep search-corroborated distinct from source-checked | C11 now requires **each** supported/disputed claim of a `source-checked` entry to cite a page-read or document-read source; no tool derives a status from a citation | fixtures in row 1; `corpus/unresolved-claims.md` |
| 4 | Validate the sourcing-queue tool | `tools/test_sourcing_queue.py`: idempotent, tracked outputs current, evidence byte-identical, unknown plan fields kept, per-batch queue = corpus ids, a URL or page-read flag upgrades nothing, a real source-checked entry leaves the queue; 8/8; `--mutants` 5/5 killed | |
| 5 | Integrate only after review; full validation | see 6.2 and 6.3 | |

### 6.1 Validator corrections found by the review (no content rule changed)

- The live gates outside the stage and the Age facts are read from content (`kr_lib.live_gates`: `sim.json`
  `governance.taxationRequires`; `research.json` `inst.university`, institutions expanded) instead of literals.
- An Age research fact holds when ANY listed node is complete (`Sim.Core/Systems/Ages/AgeContent.cs`): only the
  intersection of its alternatives' closures is mandatory. The earlier union marked tin-branch-only ancestors
  mandatory; the closure is 93 nodes, not 94.
- `civic.required: false` is enforced (ERROR when such a civic is mandatory); the D11 case study is optional except its
  declared trunk geology path (`ore_prospecting`, proposal §4) — the branch field is the declaration, no id allowlist.
- 205 entries above `unverified` list no per-claim results (their status rests on per-source claims); one WARN counts
  them and the inventory names them.

### 6.2 Integration audit (MEASURED on the review tree)

| Check | Result |
|---|---|
| Civic keys, runtime `research.json` vs proposal | 67 = 67, keys 1001–1067, 0 mismatches, 0 duplicates |
| Technology keys | runtime 496 = proposal 494 active + `siege_tower`, `crossbow_repeating` (merges deferred to the KR-2 remap seam, ADR-045, `implementation-status.md`); new keys 427–496; 0 mismatches |
| System ids / order kinds / ledger reason ids / ADR numbers | 27 distinct system ids (Discovery 29); order kinds unique (Explore 14, Prospect 15); 18 distinct reason ids (Prospecting 17, Expedition 18); ADR-043/044/045 unique |
| Evidence counts in prose vs corpus | 113 / 250 / 198 / 0 and 0 of 561 agree in README, docket, reconciliation, corpus doc, inventory |
| Generated documents | `integrate`, `merge_sources`, `validate`, `sourcing_queue`, `unresolved_claims`, `render`: re-run, no diff beyond this review's own changes; `research-*.py --check` ×3 current; `implement_tranche.py --check` up to date; `m5-playability-gate.md` §6 regenerated (`Sim.Ui --smoke --report`: 13 states, 3,929 checks, 0 FAIL; new rows `Explore`, `Prospect`) |
| Shared-file conflicts | the review's edits touch the package tools, generated views, this report, the roadmap, `docs/queue.md`, the gate document and one skipped measurement test; no runtime code, content, schema or golden changed |


### 6.3 Local validation of the review tree (run 2026-10-23)

| Command | Exit | Result |
|---|---|---|
| `dotnet build Sim.slnx -c Release` | 0 | 0 errors, 0 warnings |
| `./scripts/check-banned-constructs.sh` / `check-read-isolation.sh` | 0 / 0 | OK / OK |
| `research-content-audit.py`, `research-calibration-report.py`, `research-gameplay-unlock-audit.py` `--check` | 0 ×3 | current |
| `implement_tranche.py --check` | 0 | up to date |
| `tools/validate.py` | 0 | ERROR 0 · WARN 272 · INFO 18; shipping gate 0 of 561 |
| `tools/test_validate.py --mutants` / `tools/test_sourcing_queue.py --mutants` | 0 / 0 | 25/25 fixtures, 15/15 mutants killed / 8/8 tests, 5/5 mutants killed |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Tests | 0 | 2,707 passed, 15 skipped (13 + the two new measurement facts), 0 failed |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Ui.Tests | 0 | 1,099 passed, 0 failed |
| `Sim.Ui --smoke --report` | 0 | SMOKE PASSED — 13 states, 3,929 checks, 0 FAIL |

No runtime code changed in this review, so the goldens and the cross-process determinism legs are unchanged; CI on the
pushed head re-runs them (run IDs in the hand-back report).

## 7. Review batch 2 — ADR-046 (2026-10-23)

The Director's five follow-up items on head `2cd9680`. Records: `docs/adr/adr-046-m6-review-fixes.md` (decisions, tests,
goldens), `audit/taxation-gate-trace.md` §6–§7 (timing, Age IV). Branch head: the commit carrying this section (SHA in
the hand-back report, not self-referenced). `main` = `m5-exit` = `6e51a3e`, untouched.

### 7.1 Research timing (MEASURED, canonical AI world, 900 turns; re-run on the final tree, identical)

| Seed | Bronze: pre-M6 / M6 before / **with fix** | Age III | First levy | Age IV |
|---|---|---|---|---|
| 1 | 412 / 569 / **401** | 413 / 570 / **402** | 414 / 571 / **403** | 828 / 873 / — |
| 2 | 425 / 597 / **423** | 426 / 598 / **424** | 427 / 599 / **425** | 843 / — / — |
| 42 | 421 / 578 / **406** | 422 / 579 / **407** | 423 / 580 / **408** | 834 / — / — |
| 7 | 420 / 574 / **406** | 421 / 575 / **407** | 422 / 576 / **408** | 869 / — / — |

Research sequence: the AI's first targets are unchanged (`grinding_stone`, `kin_council`); the Taxation civic completes
on the same turn (314 / 340 / 321 / 321); the paved road comes 13–15 turns earlier; 2–7 fewer nodes are known at turn
900 (the redundant OR branches are gone). Open: Age IV (§7.6).

### 7.2 Prospecting costs

| Method | Bill | Requires | Offered |
|---|---|---|---|
| Survey (order Amount 0) | 40 grain, 20 timber, 10 stone | — | from founding; stone binds for the first turns |
| Assay (order Amount 1) | 20 grain, 10 timber, 5 stone, 2 bronze | `ore_prospecting` | listed with its blocker until then |

Validated at the preview (UI, AI) and again at delivery; a short purse pays nothing; one Prospect per settlement per turn
whatever the method; every good sunk under reason 17 and reconciled by the Observer. The loader refuses a survey bill that
names a concealable good or anything made from one (copper-ore, tin-ore, bronze, tools) — no deadlock.

### 7.3 Spatial range

Lattice node = 4 × 4 px × 4 km/px = **16 km**; Euclidean disc over node centres, wrapped east–west, clipped at the poles.
Seed disc 48 km; each Explore +48 km; cap **240 km** (709 nodes ≈ 181,000 km²) — intended, kept. The UI states km. One
documentation defect fixed (formation sight is the 4-neighbourhood, not 8).

### 7.4 Goldens and moved pins

See ADR-046 §5 for the table and controls. In short: the founded, toy, driven and first-reign goldens and `ci.yml`
`FOUNDED_GOLDEN` are **unchanged**; the AI world t300 hash moves `776d5d0c…` → `2e4759ac…` for the Prospect bill alone
(the routes-off twin gives the same hash); the canonical AI's levy and A3 move 579 → 407 for the route fix alone (the
twin returns 579), its first granary 2 → 3 for the bill alone.

### 7.5 Validation (run 2026-10-23 on the review tree)

| Command | Result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 errors, 0 warnings |
| `dotnet test Sim.slnx -c Release --no-build` | Sim.Tests 2,728 passed, 20 skipped (measurement facts), 0 failed; Sim.Ui.Tests 1,101 passed, 0 failed |
| `check-banned-constructs.sh` / `check-read-isolation.sh` | OK / OK |
| `research-*.py --check` ×3; `implement_tranche.py --check`; `export_runtime_texts.py --check` | current |
| `tools/validate.py`; `test_validate.py --mutants`; `test_sourcing_queue.py --mutants` | ERROR 0 · WARN 272; 25/25 fixtures, 15/15 killed; 8/8, 5/5 killed |
| `Sim.Ui --smoke --report` | SMOKE PASSED — 13 states, 3,941 checks, 0 FAIL (gate §6 regenerated; new row `Assay`) |
| `ci.yml` AI-empire leg, run locally (600 turns) | two processes byte-identical, replay identical, inspect VERIFIED; 394 orders, kinds unchanged |
| 20-seed soak (`sim autoplay --seeds 20 --turns 650`) + `sim corridors` | exit 0; all 20 final hashes and the metrics file **byte-identical to `2cd9680`**; the migration corridor's quarantine drift is pre-existing (same on the base). The soak runs no AI and no player orders, so it cannot see this batch's two behaviour changes — the AI tests above do. |

The first full run found 14 failures, all from this batch's intended changes, each fixed or re-pinned with its cause:
turn-one action counts (+12 Assay entries); the goods identity in one Observer test omitted the new `Discovery` sink;
the legacy union assertions moved under the switch-off twin with a new route test beside them; the Prospect order's
method contract; the forensic config list (+ `knowledge-texts.json`); two twins that broke the R4 rule (`PreTreeTranche`
copied the route switch and kept an assay naming a tranche node — both now stripped); and the moved AI pins above.

### 7.6 Open items

- **Age IV** (`audit/taxation-gate-trace.md` §7): not reached within 900 turns under M6; the fix costs seed 1 its Age IV
  (873 → —) because the old closure met the supporting milestone Alphabetic script by accident. The AI's goals never
  target supporting milestones; a goal for them is proposed (`docs/queue.md`), not implemented.
- **Survey vs granary** (ADR-046 §5): the AI surveys as soon as it can and competes with its first granaries for stone
  (canonical: first granary turn 2 → 3; 256 px world: turn 7 → 14). Proposed priority rule in `docs/queue.md`.
- The AI never assays in the 600-turn CI run; bronze is 0 in every polity through turn 400.

## 8. Review batch 3 — ADR-046 §6–§8 (2026-10-23)

The Director's two queued gameplay findings, each a narrowly scoped, separately switched change on head `e91d10b`
(CI on `e91d10b`: CI and ui-artifact success). Records: ADR-046 §6 (supporting-milestone goal), §7 (build first), §8
(goldens and attribution); `audit/taxation-gate-trace.md` §8–§9. The §1 route fix, the Prospect costs, Explore ranges and
the plain-prose educational UI are unchanged.

### 8.1 A — supporting milestones for Age IV (seeds 1, 2, 42, 7; 900 turns; e91d10b → batch 3)

| Seed | Alphabetic script | Ironworking | Age IV | First levy |
|---|---|---|---|---|
| 1 | — → 465 | 653 → 673 | — → **674** | 403 → 411 |
| 2 | — → 487 | 683 → 703 | — → **704** | 425 → 430 |
| 42 | — → 473 | 664 → 683 | — → **684** | 408 → 417 |
| 7 | — → 471 | 659 → 679 | — → **680** | 408 → 417 |

Pre-M6 Age IV 828 / 843 / 834 / 869, first levy 414 / 427 / 423 / 422. Age II comes 19–30 turns earlier; Age III and the
levy 5–9 later; the first road ~19 later (canonical 166 → 185).

### 8.2 B — build first (seed 42; canonical 900 turns, 256 px 600 turns)

| World | First granary order (e91d10b → batch 3) | First survey | Surveyed @100/300/600 | Cu/Sn found @600 | Assays |
|---|---|---|---|---|---|
| canonical | 3 → **2** | 1 → 9 | 8/13/17 (unchanged) | 4 of 4 (unchanged) | 0 |
| 256 px | 14 → **7** | 3 → 17 | 3/8/12 (unchanged) | 2 of 2 (unchanged) | 0 |

### 8.3 Validation (run 2026-10-23 on the batch-3 tree)

| Command | Result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 errors, 0 warnings |
| `dotnet test Sim.slnx -c Release --no-build` | Sim.Tests 2,744 passed, 24 skipped (measurement facts), 0 failed; Sim.Ui.Tests 1,101 passed, 0 failed |
| banned-constructs / read-isolation | OK / OK |
| `research-*.py --check` ×3 (regenerated: research.json SHA line only); `implement_tranche.py --check`; `export_runtime_texts.py --check` | current |
| `tools/validate.py`; `test_validate.py --mutants`; `test_sourcing_queue.py --mutants` | ERROR 0 · WARN 272; 15/15 killed; 5/5 killed |
| `Sim.Ui --smoke --report` | SMOKE PASSED — 13 states, 3,941 checks, 0 FAIL; the coverage report identical to §7's (no UI change) |
| `ci.yml` AI-empire leg, run locally (600 turns) | two processes byte-identical, replay identical, inspect VERIFIED; 391 orders, first survey at 9, kinds unchanged |
| 20-seed soak (`sim autoplay --seeds 20 --turns 650`) + `sim corridors` | exit 0 / 0; all 20 final hashes and the metrics file byte-identical to the e91d10b-tree soak (the soak runs no AI, so neither batch-3 change can reach it); the migration corridor's quarantine drift is the pre-existing one |

The first full run on this tree found 8 failures, every one an expected AI-pin move or a lost premise, each re-pinned
with its cause (ADR-046 §8): the AI world t300 hash, the canonical AI pins and their routes-off control, the CLI
continuation fixtures (back to their pre-bill premise), the CP attribution test reading the t300 constant, and the
allocation-ratios test's horizon (300 → 330: the AI's first road on the dev world moved 284 → 308). New controls:
both batch-3 switches off return every e91d10b value and hash.

### 8.4 Open items and risks

- **Generic supporting goal (§6):** it also pursues Age II/III supporting research — Age II earlier, the first road and
  the levy/A3 later (5–19 turns). A narrower variant (supporting milestones only once the core is met) is queued as a
  Director's call (`docs/queue.md`).
- **Assay unused by the AI** in every measured horizon (bronze arrives after its settlements are surveyed).
- **Strategic exception (§7)** is test-covered but did not trigger in the measured canonical and 256 px runs (no
  recipe consuming ore is known before the AI's surveys are done).
- Standing army (Age IV supporting, military) stays impossible until M7 recruitment (ages.json `pending`); Age IV is met
  without it.
