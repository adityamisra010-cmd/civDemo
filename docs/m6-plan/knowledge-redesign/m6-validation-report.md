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
