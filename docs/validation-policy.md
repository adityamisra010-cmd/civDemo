# Validation policy: change-aware, tiered checks

**Status: standing repository policy (2026-10-27).** This is the one canonical description. `CLAUDE.md` links here
and does not repeat it.

**Why.** The full `Sim.Tests` suite has grown past 2,000 tests. Recorded runs:
- **28 m 07 s** (1,809 tests) and **31 m 32 s** (2,054 tests)
  ([D-055](d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md));
- **45–55 min** with parallel agents on the same machine
  ([D-054](d054-batch3-incremental-construction-allocation-cylinder.md)).

Sessions used to run the build plus both full suites after every packet. On the 4-CPU session containers, several
agents doing that at once made each run take an hour or more. Most changes need a few hundred tests, not two thousand.

**Most of the time goes to a few tests that play the game.** Measured 2026-10-27: a "targeted" selection for a
one-file granary change was 104 tests and 852 s of test time. **Four** of them (long-horizon AI-world attribution
proofs, each simulating several 300-turn worlds and hashing the whole world every turn) took **777 s, 91 %**; the
other 100 tests took 75 s together. These tests are valuable, but they belong at integration, not on every edit.

**The rule in one line.**
- Run the **smallest sufficient set** while you work (Tier 1).
- Run the **affected integration set** when a feature is coherent (Tier 2).
- Run **everything** at milestone and pre-merge checkpoints, or when the deterministic core, schema or a shared
  contract changes (Tier 3).

Never report a check you did not run as passing.

---

## 1. Entry point

```
scripts/validate.sh targeted     [--plan] [--paths P ...] [--scope CATEGORY ...] [--base REF] [--no-cache]
scripts/validate.sh integration  [same options]
scripts/validate.sh full         [--heavy] [--strict]
python3 -m unittest scripts/test_tiered_validation.py        # the selector's own tests
```

**Which changes it looks at.** Without `--paths` or `--scope`, the changed files are:
- the working tree diffed against `HEAD`, or against `--base`, plus untracked files;
- and nothing that is ignored.

**`--plan`** prints the selection and the reason for every check, and runs nothing. Use it first when unsure.

**Logs.** Every run writes `TestResults/validation/<stamp>-<tier>/`, which is gitignored:
- one log per check;
- a `.trx` file per dotnet test run, with per-test durations;
- `summary.json` and `summary.md`.

Expensive results can therefore be inspected without being rerun.

**Statuses:**

| Status | Meaning | Counts as passed? |
|---|---|---|
| `PASS` | ran and succeeded | yes |
| `PASS (cached)` | succeeded earlier on byte-identical inputs (see §5) | yes, and the report says so |
| `FAIL` | ran and failed. A dotnet filter that matched **no tests** is a FAIL, never a silent pass. | no |
| `MISSING` | a required script, file or command does not exist | no (exit 1) |
| `NOT RUN` | CI-only, manual, heavy without `--heavy`, or blocked by an earlier failure; the reason is printed | **never** |

**Exit codes:**
- 0: every check that ran passed;
- 1: any FAIL or MISSING;
- 2: usage error;
- 3: with `--strict`, something selected was NOT RUN.

Use `--strict` for milestone sign-off.

## 2. The three tiers

| Tier | When | What runs |
|---|---|---|
| **1 Targeted** | after each change or small batch | the changed area's validators and tests, chosen as described below. A build is added whenever dotnet tests are selected. Docs-only changes run doc checks and no build. |
| **2 Integration** | when a feature, subsystem change or integration batch is coherent, not after every edit | Tier 1, plus the category's integration additions (determinism suite, save/load, AI and leak tests, UI-to-sim tests), plus the banned-constructs and read-isolation gates for C# changes. Shared core, schema, config, worldgen and test-infrastructure changes **escalate to both full suites**. |
| **3 Full** | milestone completion; pre-merge or release candidate; major architectural changes; deterministic-core, shared-schema or persistence-format changes; broad cross-system changes | every guard, every content and research validator, every doc link, the selector self-test, the build, full `Sim.Tests` and full `Sim.Ui.Tests`. It also lists the checks that cannot run here (CI cross-process determinism, platform smoke, nightly calibration, multi-seed trials, bench, manual GUI acceptance) as **NOT RUN** with where they run. `--heavy` runs the multi-seed trial and the bench locally. The cache is off unless `--use-cache`. |

**How Tier 1 chooses its tests.** It combines:
- the categories in `scripts/validation-map.json` that match the changed paths;
- **reference inference**: the test classes whose source mentions the changed C# type, or the changed fixture's name
  or Markdown path;
- for changed test files, exactly the classes they define.

A type referenced by more than 40 test classes is a shared type. At integration tier it escalates to the full suite.

**Tier 1 leaves out recorded slow tests.** `scripts/test-durations.json` lists every test measured at **15 s or more**
(wall time, from a `.trx`). Tier 1 excludes those tests from its filter, and the plan says how many and how long they
took. Exceptions:
- a slow test whose own file you changed still runs;
- `--include-slow` runs them all.

Tier 2, Tier 3 and CI always run them. If every selected test is slow, the run reports NOT RUN (deferred to
integration), not a pass.

**Refreshing the list.** After any full run, record its trx files:

```
python3 scripts/tiered_validation.py --record-durations TestResults/validation/<run>/*.trx
```

Tests now under 15 s drop off the list automatically. New tests are unmeasured, so they count as fast and run.

**How often to validate.**
- Do not validate after every edit. Batch edits, then run Tier 1 once per batch.
- A wording or comment change needs no tests (docs: a sub-second link check).
- Run Tier 2 once per coherent feature, not after each Tier 1.
- Rerun a passing tier only when something relevant changed; the cache answers the rest.

**Tier 3 is mandatory**, whatever the targeted results, at:
1. a milestone exit gate;
2. a merge to `main`, or a release or tag;
3. any change to `Sim.Core/Kernel/**`, `WorldState.cs`, the canonical schema or snapshot format, `sim.json` or
   worldgen;
4. any request by the Director or the active packet.

The CI-only rows are satisfied by CI on the pushed commit, and the report must name that run. The manual row is
satisfied by the playability checklist (`docs/m5-playability-gate.md`).

## 3. Decision rules

| If… | Then… |
|---|---|
| only documentation changed | doc-link check on the changed files (`charters` too for `docs/m7-plan/**`). No build and no tests, unless a test reads that doc or fixture: the selector finds those readers and runs them. |
| a leaf subsystem changed | its tests and validators (Tier 1). Tier 2 adds the determinism suite and snapshot tests. |
| shared infrastructure, formulas, schema, serialization, determinism or config changed | Tier 1 is a first pass only. Tier 2 runs both full suites. **Never rely on targeted tests alone for these.** |
| research or civics content changed | research audit, calibration report, unlock audit, tree closure and charters, plus the research, civics and Age-progression tests |
| unit, building, infrastructure or resource content changed | the charter checker, plus the unit, goods and construction tests of that content; Tier 2 adds determinism and snapshot tests |
| AI boundaries, fog of war, persistence, replay or save/load changed | the leak, discovery, AI, snapshot and determinism tests (in both projects) |
| UI changed | the UI tests that reference the changed type, else all of `Sim.Ui.Tests`; Tier 2 runs `Sim.Ui.Tests` in full and lists manual GUI acceptance |
| several systems changed | the union of their selections. The selector never picks one category for a mixed change. |
| a path matches no category | the conservative fallback (kernel and determinism tests) and a printed note. Add a category if it recurs. |
| a targeted test fails | diagnose and fix it before widening scope, unless the failure itself needs broader diagnosis |
| a broad test fails | classify it before acting: pre-existing (fails on the base commit too), task-caused, environmental, flaky (passes on an unchanged re-run) or genuine regression. Repair the task-caused ones. Check a single suspect test on a pristine checkout; do not rerun a whole pristine suite for one failure. |
| the baseline already fails | record the baseline failures by name, then keep using checks that can still establish the change's correctness |
| the change is small and isolated | defer the full suite to the next Tier 2 or Tier 3 checkpoint |
| the milestone or packet says full validation | run Tier 3 regardless of the targeted results |

## 4. Reporting rule (every session, every agent)

A completion report lists:
- **what ran**: tier, command, outcome with counts;
- **what was selected but NOT RUN, and why**;
- **what was deliberately deferred** to a later tier, and to which checkpoint.

Copying the summary table from `TestResults/validation/<run>/summary.md` satisfies this rule. A NOT RUN, deferred or
cached result is never described as "passing". A cached pass is described as cached.

## 5. Reuse without staleness

**Build once.** The entry point builds incrementally once, then runs every dotnet test with `--no-build`. It never
runs `dotnet test Sim.slnx` twice in one validation.

**Cached passes.** Only passes are cached, never failures. A cached pass is reused only when its key is unchanged.
The key is:
- the check id and its exact command or filter;
- the .NET SDK version;
- a SHA-256 over **every tracked and untracked, non-ignored file** in the working tree.

For dotnet tests, Markdown files under `docs/` that no test source names are left out of that hash. So editing prose
does not invalidate test results; editing code, data, fixtures, a doc that tests read, or the tests themselves does.

`--no-cache` forces fresh runs. Tier 3 ignores the cache unless `--use-cache` is given.

**Cheap first.** Independent script validators run in parallel (`--jobs`, default min(4, CPUs)). Expensive checks
(full suites, heavy runs) are not started when a cheaper check has already failed; they are reported as NOT RUN
(blocked).

**One full suite at a time per machine.** Parallel agents share the same CPUs. The entry point enforces this:
full-suite and heavy checks take a machine-wide lock (`civdemo-full-suite.lock` in the system temp directory) and
wait for it, printing that they are waiting.
- Agents iterate with Tier 1.
- Each runs a full suite at most once, at the end.
- The orchestrator runs Tier 3 once on the integrated tree.
- No agent runs a separate "pristine baseline" full suite to classify one failure. It checks that test alone on the
  base commit.

**Heavy experiments go last.** Multi-seed trials run only after the structural and correctness checks pass, one at a
time, and their CSV and summary outputs are kept so they need not be rerun.

## 6. Maintaining the map

`scripts/validation-map.json` is the change-to-check mapping and the source of truth. Edit it when:
- a new subsystem folder, content file or validator appears;
- the selector falls back to `unknown` for a recurring path.

`scripts/test_tiered_validation.py` proves that:
- every check named in the map is defined;
- every required script exists;
- every test pattern matches at least one test class;
- the docs-only, isolated, shared-core, cross-system, unknown, missing-command and full-mode rules behave as stated.

CI runs it, so a stale mapping fails fast.

**Known failures on the base (2026-10-28).** `research-tool-tests` runs the two research tool test scripts
(`docs/m6-plan/knowledge-redesign/tools/test_validate.py` and `test_sourcing_queue.py`). They are scripts, not unittest
classes, so the check calls them directly. The original map entry used `unittest discover`, which ran 0 tests, and the
selector correctly reported that as FAIL. Run correctly, both scripts fail on `0bf7626` as well:
- `test_validate.py`: 0 of 25 fixtures behave as intended;
- `test_sourcing_queue.py`: `t8_unresolved_inventory` fails;
- `validate.py`: 341 errors.

The M6 proposal package these tools check has drifted from the shipped `research.json`. This is pre-existing and is
recorded in `docs/queue.md`. A Tier 3 run reports the check as FAIL until the package is reconciled or the tools are
retired. Never report it as a pass. Because a failed cheap check blocks the full suites, run those two suites directly
with the same commands, and say so in the report.

Test patterns are dotnet `FullyQualifiedName~` substrings, which are case-insensitive:
- prefer `.ClassPrefix` (for example `.Age`) to a bare word that occurs inside unrelated names (Storage, Maintain);
- `trait:suite=determinism` selects by trait;
- `cls:Namespace.Class` selects one class exactly.

## 7. CI

CI (`.github/workflows/ci.yml`) is the Tier 3 executor for every push and PR:
- guards and research validators;
- the full build and both full suites;
- determinism and cross-process legs;
- the calibration battery;
- the scheduled nightly sweep.

It also runs the selector self-test and the repository-wide doc-link check. Local Tier 1 and Tier 2 never replace CI;
they keep sessions from spending an hour of a shared machine on every edit.
