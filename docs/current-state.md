# CURRENT STATE — the routing document

> **2026-10-18: D-056, the Director's mandate "Fix the game", is implemented on `claude/new-session-yavghm` (the
> protected development clone; NOT merged, NOT published).** Records: `docs/d056-director-mandate-fix-the-game.md`
> (decisions and supersessions) and `docs/adr/adr-041-identity-cancel-replay-guard-same-step-edge.md` (the contract).
> - **What changed.**
>   - Cancels are order kind 13 and carry their entry's identity. Kind 11 is legacy: never written, and replayed only
>     where the session trace proves the world is the recorded one; otherwise refused before any write. Bare CLI logs
>     carrying kind 11 are refused.
>   - The road-development read of ConstructionHolds is an enforced same-step edge.
>   - The snapshot reader refuses truncated or padded files and impossible row counts.
>   - The O5 early exit is observable.
>   - `--from-save S --save S` is refused.
> - **Schema stays v34.** The batch-4 entry below describes the base this sits on; verify both against the tree
>   (gov-4).

> **2026-10-18: batch 4 (the Director's instruction "Batch 4 — Independent Audit Remediation") is applied UNCOMMITTED
> on `m5w-integration`, on top of the uncommitted batch-3 tree (`de86ace` + the batch-2 base + `b3-final-v9`).**
> - **State.** LOCAL only, in the integration worktree's working tree. It is NOT committed, NOT merged, NOT pushed, NOT
>   tagged and NOT packaged ("No commit, push, merge, tag or publication is authorised"). M5 stays NOT complete
>   (D-052 §15). LOCAL, REPORTED, REMOTE and MAIN are four different states (gov-4 §2): no batch-4 commit exists, so no
>   remote branch and not `main` carries batch 4 — nor batches 2 and 3, which are uncommitted too. This entry and
>   D-055 are REPORTED state: the "MEASURED" results below are scratch-only, and every claim is to be verified against
>   the tree.
>   - Verify before relying on it: in the working tree, `Sim.Core/Kernel/CanonicalSchema.cs` must read
>     `public const int Version = 34;` and `Sim.Core/State/Reservations.cs` must exist. If not, batch 4 is not applied
>     there and this entry describes nothing.
>   - Verify that the code is the validated code: at the root, run
>     `git ls-files -co --exclude-standard -- ':!docs' | LC_ALL=C sort -u | while IFS= read -r f; do [ -f "$f" ] && sha256sum "$f"; done | sha256sum`.
>     It must print `736862e6aaa85795df6d0fff67a874efcfbda4202251621900813e9097cc1207` (770 files outside `docs/`;
>     D-055 §8). If it does not, the code there is not the code the results below were measured on.
>   - This entry was written at the batch's integration (records stage, 2026-10-17) on a scratch clone of the merged
>     tree: the six track patches combined, work tree `6a9f6dc9…` before the records (D-055's provenance names each
>     patch and its sha256). It was updated 2026-10-18 (the integration's update stage) when T3's final revision
>     replaced the merged one: code tree `06eae6ba…`; and at the fix stage (2026-10-18), which corrected records and
>     made two comment-only source edits (D-055's provenance, "the fixed tree"); and last at the records-finalize stage
>     (2026-10-18, docs only), which added D-055 §8's result table and brought the MEASURED lines below up to date. It
>     reaches the worktree together with the code it describes, so it was written before that application and does
>     not report it.
> - **Built** (records: `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md` for the
>   decisions, supersessions, measurements, moved pins and limitations; `docs/adr/adr-040-schema-v34-construction-holds-pathfinder-surface-and-cli.md`
>   for the contract):
>   - **Item 1, material reservations across turns.** Every unreleased construction queue head SETS ASIDE its remaining
>     need from its purse, oldest order first; later heads and road works may not use it. Released only when the
>     reservation is invalid (unknown project, no labour adults, `NoConfig` viability). Switch: `sim.json`
>     `construction.reservations`, shipped ON; control `TestConfigs.PreReservations`. The player is told in one term,
>     "set aside".
>   - **Item 2, W1 F7 closed:** turn-exact attribution on the shipped cylinder of every behavioural feature previously
>     proven only on flat rigs, with seam rigs for the five distance-reading features (`CylinderAttribution*Tests`); the
>     pure layout byte strips get no cylinder twin, each with its reason (D-055 §3).
>   - **Item 3:** the ci.yml step "Allocation split, binding, cross-process (D-055)", with `sim run --from-save`,
>     `--split-report` and `sim split-check`. The check re-derives every reported step's split and walk (P11, P12)
>     instead of reading them back, and judges that a split change binds in its own delivery step against the run
>     without it (PT).
>   - **Item 4:** the persistence and lifecycle matrix, closed with recorded gaps (D-055 §5).
>   - **Item 5:** the 13.8 % finding corrected (+9.2–9.4 % against a same-day flat control, seed 42's A* search size), and
>     the byte-identical optimisations O1–O6 (60-turn bench −44 % to −54 % per seed, measured on base-b3 + T5; all of
>     batch 4 against base-b3, −36.5 % to −47.0 %, D-055 §8).
> - **Contract changes (ADR-040).** Schema **v34**: section 61 `ConstructionHolds` (16-byte rows); the readable versions
>   are {32, 33, 34}; a v33 save loads with the table empty. No order kind added. `TurnExecutor.cs` changes by comments
>   only; the construction → roaddevelopment read is order-as-data in `SystemCatalog` (D-055 §11).
> - **Moved pins** (each with a dated D-055 cause block and its old value reproduced under `PreReservations`, D-055 §7).
>   The founded golden `40115e28…` → `0f29efc7…` (and ci.yml `FOUNDED_GOLDEN`), toy `222280e5…` → `2e6d4461…`, driven
>   `fa1ce900…` → `eb88663a…`, first reign `bccc8aed…` → `f7ca60a5…`, AI `91836bcf…` → `25f4799d…`: the v34 layout alone.
>   Behaviour: three DEFECT pins, two split pins, `TwoHeadsAndARoad`, and the save/load battery's save points
>   (1, 26, 2, 26) → (1, 26, 23, 24). Text pins for the "set aside" wording.
> - **Calibration.** No frozen item touched. The two CR-020 reds keep batch 3's values exactly (seed 42 8.35755E-05; seed
>   7 123,950) in every track's full run and in the integration's (D-055 §8).
> - **MEASURED: scratch-only** (D-055 §8; gov-4 §9, a result, not a certification). None of it ran on the worktree.
>   - Each track's full Sim.Tests on its own tree: only the two CR-020 reds, plus, under host load, the wall-clock
>     `PopulationTests.ProductionPipeline_PerPhaseBench_Reported`, which passed when re-run alone.
>   - The integration validated the combined tree (`4602ed4d…`, patch `e4b607ed…`). It re-ran the code-dependent checks
>     on the fixed tree (`30fed100…`, patch `2e2353eb…`; comment-only source edits). Results: build 0 warnings; the three
>     gates and the three research `--check`s OK; FULL Sim.Tests 2,577, of which 2,562 passed, 13 were skipped and 2
>     failed, exactly the CR-020 pair; FULL Sim.Ui.Tests 1,019 / 1,019; the determinism, topology and pathfinding,
>     save/load and migration, allocation and reservation filters all passed; `--smoke --monkey 300` 0 FAIL.
>   - The ci.yml cross-process step (T0.9) and the allocation step, extracted and run locally: exit 0. With and without
>     T5, the whole run is identical. The nightly (20 seeds × 650 turns) equals batch 3's on every compared field
>     (`worldHash` excluded), and `corridors.log` is byte-identical. `sim bench`, 60 turns: 36.5–47.0 % faster per seed
>     than base-b3 (load 1.80–7.66). New failures: none.
> - **NOT measured in the repository's records:** anything on the integration worktree itself, because the worktree
>   validation after this application is reported in the batch-4 report, not in the repository; CI on any batch-4 commit
>   (none exists; the ci.yml steps ran only locally); the Windows build; the GPU seam and a person reading the holds UI
>   (manual).
> - **Open for the Director:** D-054 §10 Q1–Q13, Q15, Q18 and Q19 (Q14 is answered, Q16 corrected and Q17 resolved by
>   batch 4). Batch 4 escalated nothing; its limitations are D-055 §9.
> - **Batch-4 dated notes** were appended at the END of: D-054, ADR-039, ADR-032, ADR-005 (2026-10-16), the m5w plan,
>   `docs/queue.md`, `docs/m5-playtest-known-issues.md` and `docs/m5-playability-gate.md` (§3.14, §3.15 and the §6
>   regeneration). No earlier text was rewritten (D-054 §10 Q19 is unruled), except the gate doc's §6 generated totals
>   line (117594 → 117608 frames), regenerated by the gate's own command and recorded in that document's file-end
>   notes.
> - Verify these claims against the working tree (gov-4). They describe LOCAL state only.

> **2026-10-15: batch 3 (the Director's 2026-10-14 instruction) is implemented, LOCAL and UNCOMMITTED, on top of the
> uncommitted batch-2 tree of the `m5w-integration` worktree (`de86ace`).**
> - **State.** LOCAL only, in the integration worktree's working tree. It is NOT committed, NOT merged, NOT pushed, NOT
>   tagged and NOT packaged; committing, merging and publication wait for the Director. M5 stays NOT complete
>   (D-052 §15). LOCAL, REMOTE and MAIN are three different states: no batch-3 commit exists, so no remote branch and
>   not `main` carries batch 3.
>   - Verify before relying on it: in the working tree, `Sim.Core/Pathing/MapTopology.cs` must exist and
>     `Sim.Core/Kernel/CanonicalSchema.cs` must read `public const int Version = 33;`. If not, batch 3 is not applied
>     there.
>   - The final code is the batch-3 diff `b3-final-r3.patch` (sha256 `855a14029c906c51…`: the combine stage's tree
>     `5ab74d3e…` plus fix rounds 1 and 2 and a final nits round) plus final-round fix 1 (2026-10-15: the save/load
>     battery's part-built save point moved to world 8, and three comment-only edits) plus final-round fix 3
>     (2026-10-15: the wording of two refusals, the cylinder's size refusal and the corrupt-trailer refusal, and their
>     tests) plus final-round fix 4 (2026-10-15: one semantic worldgen test for the flat noise mapping, mutant MA2; no
>     product code; its doc comment corrected afterwards). Its non-docs diff over the batch-2 base has sha256
>     `317ec1b6…` (`fa72fa6c…` before that comment correction, `5de7149e…` before fix 4; D-054 header).
> - **Built** (records: `docs/d054-batch3-incremental-construction-allocation-cylinder.md` for the decisions,
>   supersessions, measurements, moved pins and Director questions; `docs/adr/adr-039-schema-v33-orders-11-12-and-cylinder-topology.md`
>   for the contract):
>   - **WS1, incremental construction.** It supersedes M4-D's "built whole or waits". Switch: `sim.json`
>     `construction.incremental`.
>   - **WS2, each Empire's building/roads material split** (default 100 = building first). Switch:
>     `construction.allocationRatios`.
>   - **WS3, the east-west cylinder** in worldgen, simulation and rendering. Switch: `worldgen.json` `topology.wrapX`.
>   - All three are shipped ON. The attribution pair `TestConfigs.PreD054Sim` + `PreD054Worldgen` turns the batch off.
> - **Contract changes (ADR-039).**
>   - Schema **v33**: `ConstructionProgress`, 28-byte rows; `ResourceAllocations`, 8-byte rows.
>   - Order kinds **11** `CancelConstruction` and **12** `SetResourceAllocation`. Kind 10 stays reserved for M7.
>   - An int32 snapshot topology trailer.
>   - The first explicit migration: a v32 save loads flat.
>   - **One rule for old records:** `TopologyFlags.RecordedFlat(v)` = `v < 33`. Every pre-v33 save or session is
>     rebuilt FLAT, by the UI resume, `sim inspect` and the CLI save loader; a v33 save uses the topology its trailer
>     names. A real batch-2 session (`Sim.Tests/Fixtures/v32-session-found/`) resumes into the batch-2 build's own
>     world (hash `6157dfde…` at turn 8). `sim replay` has no flat path (D-054 §4, §9).
>   - CR-021 is RESOLVED.
> - **Moved pins** (each attributed in integration R1; no re-pin since; D-054 §7).
>   - The founded golden `937577ff…` → `40115e28…`, with ci.yml `FOUNDED_GOLDEN` moving the same way (its V32
>     projection `0549049d…` is the cylinder alone). Driven `4f4e0b81…` → `fa1ce900…`. AI `b2e4f333…` → `91836bcf…`.
>     First-reign `467f2cb6…` → `bccc8aed…`. Cause: the v33 layout plus the cylinder.
>   - Toy `5994e978…` → `222280e5…`: the layout alone.
>   - Seven turn and digest pins moved for the cylinder: five in Sim.Tests and two in Sim.Ui.Tests. One of them, the
>     battery's save points, also moved for WS1.
>   - With both halves off and the v32 projection, every batch-2 reference reproduces byte for byte.
> - **Calibration.** No literal, band, envelope, hazard, spoilage or store bound was touched. The two CR-020 reds remain
>   the only Sim.Tests failures: seed 42 8.35755E-05 (was 8.71773E-05); seed 7 123,950 (was 124,483). The cylinder alone
>   moves them. The nightly corridor drift is in D-054 §6.
> - **MEASURED on the final code** by final-round fix 3 (HEAD `de86ace` + batch-2 base + the final batch-3 diff;
>   Release, .NET 10.0.112; a result, not a certification, gov-4 §9; D-054 §8):
>   - build: 0 warnings, 0 errors (also with `--no-incremental`);
>   - the three gates (banned constructs, read isolation, readonly proof) and the three research `--check`s: OK;
>   - **Sim.Tests 2,060 passed / 2 failed / 13 skipped (2,075)**, the 2 being the CR-020 seeds above;
>   - **Sim.Ui.Tests 975 / 975**;
>   - calibration battery (read from the full run) 5 passed, the same 2 failed;
>   - smoke `--monkey 300`: 13 states, 3,190 checks, 0 FAIL.
> - **Measured by final-round fix 1** on its code (the final code before fix 3's two message changes): Sim.Tests
>   2,056 / 2 / 13 (2,071) and Sim.Ui.Tests 974 / 974; every test name of those runs is in fix 3's with the same
>   outcome, except the renamed size and trailer theory cases.
> - **Measured by the nits round on `b3-final-r3.patch`** (which differs from final-round fix 1's code only in one test
>   file and three doc comments): the same counts as fix 1, and the ci.yml cross-process step rc 0, including the
>   seed-16 seam leg and the exact kind set {3..9, 11, 12}, with every hash equal to the combine stage's run.
>   Final-round fixes 1 and 3 did not re-run that step: they changed no order, hash or simulation code (fix 3 changed
>   the text of two CLI/UI refusals).
> - **Measured on the final code in the integration worktree itself** (2026-10-15, after final-round fix 3 and before
>   fix 4's test; D-054 §8): the same suite counts as above, determinism filter 48 / 48, the ci.yml cross-process step
>   rc 0 (every leg, including the seed-16 seam leg and the exact kind set {3..9, 11, 12}), nightly 20 × 650 rc 0 and
>   identical in every field to the combine stage's, corridors rc 0. Final-round fix 4 then added one Sim.Tests test:
>   full Sim.Tests 2,061 / 2 / 13 (2,076).
> - **Measured only on the combine stage's tree `5ab74d3e…`** (the later rounds changed no simulation system): bench
>   +13.8 % on the shipped config and +1.2 % on the same flat world; the six CLI hash legs with the V32 projection; the
>   cylinder-off nightly control (the calibration battery's cylinder-off control was measured on the W1 tree).
> - **NOT measured:** CI on any batch-3 commit (none exists); the Windows build and smoke; the GPU seam (the manual
>   playtest, `docs/m5-playtest-known-issues.md` §16).
> - **Open for the Director:** D-054 §10 (19 questions; Q9, old sessions, is reframed for the final code, and Q17,
>   Q18 and Q19, where batch 3's dated notes sit in frozen documents, are new).
> - **Batch-3 dated notes** were appended to: `docs/milestones.md` (M4-D), §4 below, `docs/queue.md` (T3.1(b), the
>   M4-D entry, the P1 zoom-floor entry), D-052, D-053, ADR-005, ADR-008, ADR-032 (Final Ruling 3), ADR-033 (D10),
>   ADR-034 (§4, the resume notice), ADR-038, CR-021, the m5w plan, D-009, the M1 spec (D-015), the M3 spec (T3.1(b)),
>   `docs/institutions-universities.md` and `docs/session-records.md`. Some of them sit inside the body, next to the
>   text they supersede, rather than at the end of the file; D-054 §10 Q19 lists those and asks whether gov-4 §6 allows
>   it.
> - Verify these claims against the working tree (gov-4). They describe LOCAL state only.

> **2026-10-14: batch 2 (the Director's 2026-10-13 instruction) is implemented UNCOMMITTED on `m5w-integration`, on
> top of `de86ace`.** Nothing is committed, pushed, merged, tagged or packaged; publication waits for the Director.
> - **Built:**
>   - D-053 §1, the empire purse for construction and roads;
>   - §2, intra-polity grain relief in a shortage;
>   - §3, the granary effects.
>   Each has a sim.json switch, shipped ON, and an attribution control (`TestConfigs.PreD053` turns the batch off).
> - **Records:** `docs/d053-batch2-playtest-rulings.md` (decisions, supersessions, questions for the Director) and
>   `docs/adr/adr-038-empire-stockpile-food-relief-granaries.md` (technical contract, re-pins).
> - **Not built:** items 3, 4 and 6 (incremental construction, building/road ratios, the explored-longitude wrap with
>   Satellites). `docs/adr/cr-021-batch2-items-3-4-6-vs-frozen-decisions.md` is OPEN and escalated.
> - **No save-format change** (CanonicalSchema v32), and no frozen calibration reference touched.
>   - The founded golden `937577ff…` is unchanged.
>   - The AI world moves `aa7a87a9…` → `b2e4f333…` and the driven golden `7bcdc03f…` → `4f4e0b81…`, each with
>     per-layer attribution in ADR-038 §6.
>   - The two CR-020 reds are unchanged in kind. Seed 7 now reads 124,483.
> - Verify these claims against the working tree (gov-4): they describe LOCAL state only.

> **2026-10-12: D-052 recorded, and the two playtest blockers fixed, on `m5w-integration`** (two commits on
> `f0427d2`, pushed to the new branch `claude/m5-playtest-d052` for the playtest build with the Director's authorization).
> - `claude/new-session-yavghm` is untouched at `2dc6af2`.
> - NOT merged, and M5 is NOT complete.
> - The packets' acceptance and any merge to `main` are still the Director's.
> - **The Director's 2026-10-12 rulings are recorded:** `docs/d052-m5-integration-audit-rulings.md`, with dated
>   pointers in the records it supersedes. In summary:
>   - ADR-036 and ADR-037 are RATIFIED as drafted, ADR-036 §8 (g)–(i) and the S1c re-pins included.
>   - CR-020 is RULED: §A is the guard plus the approved primitive-tools direction, mechanism deferred; under §B,
>     time-averaged weather stays on, every frozen calibration reference is preserved, and the two reds are
>     disclosed.
>   - CR-017 §3/§5 are scheduled for M7, and the CR stays OPEN.
>   - D-051 §1(a), §2, §3 and §6, and primitive toolmaking, are deferred until after the playtest.
>   - Fog and scouts go to M6; the visual milestone is placed at M6; movement and Action Points go to M7.
>   - The unit-display reading is agreed, but NOT built.
>   - The Spine's M5 items (budget, authority, laws-lite) are deferred, with destination unruled.
>   - The playtest launcher uses `--ai-empires 1`.
>   - `--size` must found correctly.
>   - Old saves from another build are refused with a clear error.
> - **Still open** (D-052 §14): ADR-035 P-F1/P-F0; acceptance of the 18 m5w packets and W0; publication (`2dc6af2`
>   is the accepted remote baseline); the Spine items' destination; G9; the viewport fit; the shortcut's deposit
>   highlight.
> - **Playtest blockers fixed (code):**
>   - `--size` founding: `WorldFounding.HinterlandMeans` indexes by the terrain's own size. It used to read the wrong
>     cells and crash End Turn for bottom-row sites; that was reproduced, for example with `--seed 1 --size 256
>     --ai-empires 1` at turn 180.
>   - Cross-build resume: `SessionIncompatibleException` gives a clear error with exit code 3 and a best-effort native
>     dialog, instead of an unhandled crash (`Sim.Ui/ResumeRefusal.cs`, `Sim.Ui/NativeErrorDialog.cs`).
>   - The canonical founded golden `937577ff…` is unchanged, and no pin moved.
> - **Playtest launcher:** `.github/workflows/ui-artifact.yml` writes `start "" "app\Sim.Ui.exe" --ai-empires 1`.
> - **Known issues for the playtest:** `docs/m5-playtest-known-issues.md`.
> - **MEASURED on this tree** (a scratch clone at `f0427d2` carrying the identical diff before it was committed, Release):
>   build 0 warnings / 0 errors; banned-constructs, read-isolation and readonly-proof gates OK; three research
>   `--check`s current; **Sim.Tests 1,703 passed / 2 failed / 13 skipped (1,718)** — the 2 failures are exactly the
>   ruled CR-020 reds (`Dev_MalthusCorridors_AllInBand` seed 42: 8.71773E-05 below 0.75 × 0.000248624; seed 7: 124,455
>   above 119,000); **Sim.Ui.Tests 845 / 845**; `--smoke --monkey 300` PASSED (12 states, 2,896 checks, 0 FAIL);
>   ci.yml cross-process legs reproduced — orderless 400, ordered + replay 400, founded 300 = `937577ff…` (the pinned
>   golden), founded replay, found-settlement leg (inspect VERIFIED 301 turns), AI leg (296 orders, kinds 3–9, inspect
>   VERIFIED 601 turns, `7f374638…`).
> - **NOT measured when this was written:** CI and the Windows smoke on the pushed commit (see its GitHub Actions runs),
>   and the native dialog on Windows.
>
> Verify against git.

> **2026-10-11: M5 map / exploration / settlement workstream, `m5w-integration` @ `6b624d45` (LOCAL, NOT pushed,
> NOT merged).**
> This entry records MEASURED facts only. It was written by packet W0 (branch `m5w-w0-docs`). It names no base for the
> workstream, because none is ruled. Record: `docs/m5w-map-workstream-audit-and-plan.md`, which holds the 2026-10-10
> audit and plan and its "2026-10-11 — status" section.
>
> - **REMOTE: `2dc6af2` is on the remote.** `m5p-integration` @ `2dc6af2` is the remote branch
>   `origin/claude/new-session-yavghm` (`git ls-remote origin`, 2026-10-11). Both CI runs on that head are green
>   (`gh run view`):
>   - CI run `38032099613`: build-and-test, determinism, determinism-xproc and calibration succeeded.
>   - ui-artifact run `38032099661`: ui-artifact and windows-smoke succeeded. The artifact `sim-ui-win-x64-2dc6af2` is
>     not expired.
>
>   This dated line supersedes, for `2dc6af2` only, the 2026-10-10 entry's "(LOCAL, NOT pushed, NOT merged …)", which
>   is kept as written. `2dc6af2` is NOT on main (`origin/main` = `93270cd`).
> - **LOCAL only: `m5p-d051-fixes` @ `82bde01` and `m5w-integration` @ `6b624d45`.** Neither is on the remote: no remote
>   ref contains either commit (`git ls-remote origin`, then `git merge-base --is-ancestor` against every remote head).
>   Neither is merged to main, and because neither is on the remote, no CI can have run on
>   either. `m5w-integration` is cut from `82bde01`, 103 commits ahead.
>   Seventeen packet branches are merged into it: S0, S1a, S1b, S1c, S1d, P1, P2, P3, P4, P6, P7, P8, P9, P10, P11, P12a
>   and P13. Their heads are in the status section. None has a Director acceptance ruling.
> - **Inherited CR-020 reds:** `Dev_MalthusCorridors_AllInBand` seeds 42 and 7 are RED on `6b624d45`'s code.
>   - MEASURED 2026-10-11 on branch `m5w-w0-docs` (Release,
>     `dotnet test Sim.Tests/Sim.Tests.csproj -c Release --no-build`): 1,699 passed, 2 failed, 13 skipped.
>   - The failures carry CR-020 §B's values. Seed 7: the dev population is 124,455, above 119,000. Seed 42: migration
>     is 8.71773E-05, below 0.75 × 0.000248624.
>   - These are the two reds CR-020 records on `m5p-d051-fixes`. They are left RED until CR-020 is ruled.
>
>   On the same tree: Sim.Ui.Tests 830/830; `Sim.Ui --smoke --monkey 200` passed (12 states, 0 FAIL); the three gate
>   scripts and the three research checks are OK.
> - **PROPOSED, not ratified:**
>   - ADR-036 (the Found Settlement order, kind 9). Its legality terms are the defaults the Director was told of, and
>     they await his confirmation.
>   - ADR-037 (the player-knowledge seam).
>
>   CR-020 and CR-017 are OPEN.
> - **Open Director rulings for this workstream.** None is recorded, and no D-052 exists. They are:
>   - the base branch: CR-020 §A/§B, approval to push `m5p-d051-fixes`, and the "cut from main" convention;
>   - ADR-036's legality terms and its §8 items (g)–(i);
>   - D-038 E1's visual milestone;
>   - fog and discovered extent: D-040 pulled forward from M6;
>   - unit movement and Action Points: CR-017 §3/§5;
>   - unit representation;
>   - institutions at close zoom: D-043 D2 and H8;
>   - Age-specific terrain and architecture;
>   - part 6's "research unlocks a resource".
>
>   Waiting on them: W0b, P5, P12b, P14, S2a, S2b, S2c, S3a, S3b and S3c. Some delivered packets follow defaults that are
>   not ruled: S1b, S1c and S1d follow ADR-036's terms; P10 follows part 6 option 1; P11 follows unit-representation
>   option 1.
>
> Verify against git.

> **2026-10-10 (later) — `m5p-d051-fixes` (LOCAL, NOT pushed, NOT merged), cut from `m5p-integration` @ `2dc6af2`.**
> Director rulings recorded (ADR-035 P-F2 + ADR-017 §7 amendment accepted; D-051 §6 deferred). D-051 §4: the Age I
> workshop trap closed by `ProjectAvailability.InputUnobtainable` (a stone-toolmaking recipe was measured to block all
> granaries and is NOT shipped). D-051 §1(b): time-averaged decade weather (`timeAveragedHarvest`, off = old goldens).
> MEASURED on `2444a5c` (Release): Sim.Tests 1568 passed / 2 failed / 12 skipped — the two failures are
> `Dev_MalthusCorridors_AllInBand` seeds 42 and 7, left RED and escalated in CR-020; Sim.Ui.Tests 583/583; all gates
> and research checks OK; ci.yml determinism-xproc leg reproduced (founded golden `937577ff…`, AI leg VERIFIED).

> **2026-10-10 — M5 PLAYTEST CANDIDATE `m5p-integration` (LOCAL, NOT pushed, NOT merged; M6 not started).** One tree =
> `m5-hardening` `81ac697` + `m5p-sim` `4ef911c` + `m5p-docs` `9814383` + `m5p-info` `9205db6` + `m5p-ui` `bf711c8`.
> Conflicts resolved: `docs/d049` (append-only, both kept); five UI files (m5p-ui layout kept, every m5p-info
> Shift+click registration ported onto the new geometry). Simulation code = `m5p-sim` (other diffs are comments,
> read-only `InfoQuery`/`Variables`, `SessionManifest` provenance). MEASURED on this tree (Release, 2026-10-10):
> Sim.Tests 1559 passed / 0 failed / 12 skipped; Sim.Ui.Tests 583/583; banned-constructs, read-isolation,
> readonly-proof and the three research `--check`s OK; ci.yml determinism-xproc leg reproduced locally (orderless,
> replay, founded = `FOUNDED_GOLDEN` `e9e089bb…`, founded replay, 600-turn AI leg with order kinds 3–8, inspect
> VERIFIED); calibration battery 7/7. ADR-035 still PROPOSED; D-051 rulings ratified, not implemented. Order-free
> starvation unchanged by ADR-035 (canonical 11/20 seeds by 650; σ_log yield 0.19 counterfactual: 0/20).

> **2026-10-05 — M5 HARDENING PASS (`m5-hardening`, NOT merged, NOT tagged; M6 not started; playable build
> `sim-ui-win-x64-6ef1596`, ui-artifact run 37336233575, build and windows-smoke jobs green).** Newer than the
> 2026-10-04 entry below. Research-screen crash fixed at its root (ImGui `RendererHasVtxOffset` never declared);
> headless `--smoke` playability gate over every drawn control (9 states), also run on the shipped Windows exe;
> Warband selectable with a card stating movement is M7; taxation is continuous per-segment pressure with no
> instant revolt, a food floor and state-capacity aggravation (D-049, schema v32); the tax edict needs Taxation
> **and** Age III; a revolt-born polity inherits its parent's Age; preview tools fixed; title `civ-sim M5`; on-screen
> turn numbering starts at 1. ADR-034 (delivery-time order validation) is PROPOSED. Forager order-free starvation
> is OPEN (shipped 4.3 / 2.0 kept). Record: `docs/m5-playtest-baseline.md` §15, `docs/d049-taxation-and-revolt-model.md`,
> `docs/m5-playability-gate.md`, `docs/m5-hardening-measurements.md`. Verify against git.
>
> **2026-10-06 — D-050, Director rulings on the hardening open items** (docs-only commits `721cb01`, `81ac697` on
> `m5-hardening`; still NOT merged, NOT tagged; M6 not started; no code change, so the playable build is still
> `sim-ui-win-x64-6ef1596`). This dated line supersedes the entry's "ADR-034 … is PROPOSED" and "Forager order-free
> starvation is OPEN", which are kept as written: (1) ADR-034 is **RATIFIED**; (2) forager food stays **4.3 per
> gatherer / 2.0 per km²** — the 5.0 / 3.0 alternative is rejected, order-free worlds may starve in bad weather, and
> a player who neglects food can starve; (3) population segments are classes, finer segments deferred to **M9
> Society**; (4) the rebel count is a deterministic expected fraction, `uprisingGrievance` = 20; (5) a last
> settlement under a permanent 100 % levy may sit in permanent revolt (food floored at the untaxed level), no relief
> mechanic; (6) `taxCapacityOffsetMax` = 0.25; (7) capital succession deferred to **M8 Politics / Diplomacy**
> (revolt-founded civilizations have no capital and cannot tax until then); (8) the 1080×640 minimum-window snap-back
> is a Director playtest item. Record: `docs/d050-m5-hardening-rulings.md`. Verify against git.

> **2026-10-04 — M5 PLAYTEST BASELINE (`m5-integration` @ `bc87ef8`; played build `sim-ui-win-x64-5c364b5`, ui-artifact
> run 37203130829).** This dated entry is newer than the R3 entry below. It supersedes R3's "forager food STOPPED …
> switch stays OFF":
> - **R4a.** The forager layer ships ON at 4.3 / 2.0 with 400 founders (`07832a4`). The ledger crash is fixed
>   (`e6b7e42`). The Dignity tax offset is in (`95982bd`). D-048 is ratified.
> - **R5.** The Civics node `taxation` (1007, A3, 1,050 RP) is the only tax gate (`9f7c82b`).
>
> Full forensic baseline and decision record: `docs/m5-playtest-baseline.md`. Verify against git.

> **2026-10-03 — Director roadmap rebase (milestone ladder).** COMPLETE: M0 Kernel · M1 Walking Skeleton · M2
> Demography / Food · M3 Production / Markets · M4 Empire / Strategic Foundation. **CURRENT: M5 Governing Gameplay —
> being finished, NOT complete** (integration unmerged, see below). NEXT: **M6 Knowledge / Research / Technology** (large;
> owns, reviews and completes the research engine, Ages, unlock pipeline and universities already built early on the
> unmerged integration branches — none of that makes M6 done). FUTURE: M7 Battle Layer (consumes M6's technology/capability
> state) · M8 Politics / Diplomacy · M9 Society · M10 Integrated Civilization Simulation · M11+ Depth & Content Expansion. Earlier orderings
> (M6 battle / M7 knowledge) in this file and elsewhere are superseded; record: `docs/milestones.md` §"Roadmap rebase 2026-10-03".

> **2026-10-03 — M5 integration R3 (final R2 reconciliation) on `m5i-r3-final-reconcile` (NOT merged).** Revolt founds
> a new AI polity with a complete copy of its parent's knowledge; annexation union operation; capital-loss invariants;
> truthful food label. Turn-1 forager food STOPPED (no founding population passes the bands; switch stays OFF).
> Record: `docs/r3-final-reconcile-record.md`, ADR-033 §R3, CR-019. Verify against git.

> **2026-10-03 — M5 integration R2c on `m5i-r2c-reconcile` (NOT merged).** R2a (Trade as research node 426, city-state
> research) and R2b (unrest-lite tax brake, geographic weather distance, Age military milestones) reconciled on one tree.
> Status, goldens and evidence: ADR-033 §R2c and `docs/m5-integration-coherence-matrix.md` §7. Verify against git.

> **STALENESS CORRECTION, M4 exit gate (2026-09-16).** The measurement below is dated 2026-08-31 and
> four of its load-bearing claims have since been overtaken by git. They are corrected inline and
> marked `CORRECTED`; nothing else in this file was rewritten, per the director's instruction that the
> remaining stale-document findings stay housekeeping. Re-derive anything you rely on, as §9 says.
>
> | claim as written | git, re-derived 2026-09-16 |
> | --- | --- |
> | `origin/main` = `070f05b`, schema v22 | **`dbef61a`**, "Merge M4 completion (director-certified): the M4 baseline", schema **v24** |
> | M4-B/C/D "CERTIFIED … not merged" | **all merged** — main's tip IS the M4 completion merge |
> | §2's milestone contradiction (`CLAUDE.md:10` reads M3) | **RESOLVED** — `CLAUDE.md` now reads M4 with `docs/m4-spec.md`. §2 is itself now the stale artifact |
> | open CRs include CR-009, CR-010 | **neither has ever existed on any branch**; CR-008 exists only on `m5-full-build` |
>
> The current M4 exit candidate is `t4.19-glass-box`. Its suite, measured in Release at the exit gate:
> Sim.Tests **722 passed / 4 failed / 6 skipped**, Sim.Ui.Tests **236/236**, three gates green — which
> supersedes the 541–572 / 6 / 6 counts recorded below.
>
> **2026-09-17 — T4.21 IN PROGRESS on `claude/civdemo-work-b1z2y4`** (= `main` `dbef61a` + the unmerged
> M4 playtest build `2807155` + the forensic CLI, `45046eb`): famine semantics, bounded migration and
> shock integration. Governance landed first (T4.21-0, branch `t4.21-0-governance`):
> `docs/adr/cr-015-famine-is-exceptional.md` RULED from the director's 2026-09-17 mandate (G1 — the
> harvest-weather decade variance — ESCALATED and open), ADR-024/025/026, spec
> `docs/t4.21-architecture.md`, evidence `docs/t4.21-evidence/`. Code packets T4.21-1..6 follow on
> their own branches; nothing is merged to `main`. Suite baseline on `45046eb` (Release): 754 passed /
> 2 failed (`Dev_MalthusCorridors` seeds 42, 7 — red by CR-003 ruling) / 6 skipped. Verify against git.
> **2026-09-18 — T4.21-4 DONE, on branch `t4.21-4-arm` (cut from `claude/civdemo-work-b1z2y4` @
> `8f7f9da`), NOT merged.** The famine-class disaster is ARMED (`sim.json disaster.hazardPerYear`
> 0.0 → 0.01). Record: `docs/t4.21-4-record.md`. **ESCALATION — READ BEFORE RELYING ON THIS TREE:**
> `docs/adr/cr-016-armed-disaster-fallout.md` is OPEN — the ruled arming breaks the CR-001 permanent
> dt-continuity detonator at the era gate (measured 2.5259 per 1000 yr against a 0.1 bar), puts
> `canonical.fedGrowthPerYear` below its band, and extinguishes the dev world (93,910 → 38 over 1000
> turns). No band, constant or derivation was moved; six tests are left RED and named in CR-016 §5.
> Suite on the packet tree, MEASURED in Release: Sim.Tests **867 passed / 6 failed / 4 skipped**
> (the 6 skips became 4: `MigrationTests.cs:378` and `:532` were LIFTED under CR-015 N9),
> Sim.Ui.Tests **294/294**, and all three gate scripts green
> (`check-banned-constructs.sh`, `check-read-isolation.sh`, `check-readonly-proof.sh`). The ONE
> inherited red on `8f7f9da` (`Dev_MalthusCorridors_AllInBand(seed: 7)`, the migration drift tooth)
> is RESOLVED — its envelope was re-pinned to the measured value — but both seeds of that theory are
> now red for the CR-003 Malthus teeth instead. Goldens moved for the arming alone, proved
> bit-exactly: every layout control returns its constant byte for byte on the λ = 0 twin.
> Verify against git.
>
> T4.21-1 merged to the integration branch at `05b23e6` (Release suite, measured by the fix lane on
> the fix tree: Sim.Tests 819 passed / 2 failed / 6 skipped in 5 m 36 s, Sim.Ui.Tests 294/294 — the
> SAME two `Dev_MalthusCorridors_AllInBand(seed: 42)` and `(seed: 7)` fail on `45046eb` in the fix
> lane's own worktree with the byte-identical message "3 starvation deaths": INHERITED from the M4
> playtest build, not caused by the packet; CI on this branch is red until T4.21-4 restores the `Cr003Quarantine` guards and re-reads the
> `Dev_MalthusCorridors` message). T4.21-1 fix lane (verifier's findings): the measured foundations
> audit `docs/t4.21-1-foundations-audit.md` (per-settlement `ρ` for seed 42 ± the director's orders and
> seeds 1–3, F4/F5), the measured mutant kill-record `docs/t4.21-1-mutants.md`, and the M-FS-ABANDON
> correction in ADR-024 §8 / FoodState.cs (the Share variant is equivalent).
>
> **2026-09-18 — T4.21-2 ∥ T4.21-3 MERGED AND FINISHED on `claude/civdemo-work-b1z2y4`.** The merge
> commit `a621c86` (T4.21-2 bounded migration, ADR-025 + T4.21-3 shock integration, CR-015/ADR-026)
> was left unfinished: a `-999` placeholder in `MerchantTests`, every golden and measured pin resolved
> to the T4.21-3 side (both parents measured against `1735d41` with the other absent, so neither
> value can hold on a tree carrying both), and a conflict of fact — T4.21-3 asserted migrants move on
> turn 2, T4.21-2's vacancy bound refuses every gap flow that turn. The merge-finish lane resolved all
> three BY MEASUREMENT on the merged tree, changing NO mechanism (the diff against `a621c86` touches
> `Sim.Tests`, `Sim.Ui.Tests`, `ci.yml`, `docs/queue.md` only): T4.21-2's form is the measured truth —
> `MigrantsMoved` 0 on turn 2, 252 on turn 3, founded and driven. Four world goldens, ci.yml's
> `FOUNDED_GOLDEN` (reproduced with two separate CLI processes, byte-identical), every
> `IntegratedPinAttribution` strip and every measured-value pin re-measured; the synthetic
> `GoldenHash_Seed42Turn200` and both its strips are UNMOVED, which is the no-unrelated-movement
> control. Suite measured on the finished tree (Release): Sim.Tests **858 passed / 1 failed / 6
> skipped** in 6 m 31 s, Sim.Ui.Tests **294/294**, three gate scripts green. The ONE red is
> `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7)` — the dev migration quarantine's
> drift tooth, red on BOTH parent branches for the same pre-existing, previously masked cause
> (measured here: seed 7 `dev.migrationGrossPerDecade` 1.0200612834541973E-04 against a recorded
> 0.000799951; seed 42 8.336943780925534E-05 against 7.21744E-05, which passes). Its re-pin is
> T4.21-4's deliverable; the `corridors.json` bands and the CR-003 quarantine were NOT touched.
> The two open T4.21-2 findings (the turn-2 vacancy refusal, the fed-world basin-cap magnitude) stay
> open in `docs/queue.md` — this lane measured and recorded, it did not act on them. Verify against git.
>
> **2026-09-18 — T4.21-4 RULE APPLICATION on `claude/civdemo-work-b1z2y4`.** The orchestrator's two
> pre-registered rules were applied to the two findings above. **RULE 1: the source basin (fan-out)
> cap STAYS as shipped** — measured on the canonical founded world (seed 42, no orders), gross
> migration per decade over turns 2..300 is 0.001021 on the merged tree against 0.001509 pre-packet
> (`1735d41`), i.e. 0.677x, clearing the 0.4x threshold (0.000604) and inside the corridor band
> [0.001, 0.01]; both conditions hold, so no mechanism changed and no test or mutant was removed.
> **RULE 2: `FoodHeadroom.Limit`'s null arm now includes catchment ROW ABSENCE** (ADR-026 §2.1a),
> keyed on row absence only — never on `production == 0`, which is the abandoned settlement's genuine
> zero — with `H_NullArm_NoCatchmentRow_IsPositiveInfinity_ButAbandonedWithARowIsZero` pinning both
> arms. **It does NOT close its queue line, and it moves no golden:** measured bit-exact on the
> canonical founded world (300-turn hash `db7c7a09…` before and after — the ci.yml `FOUNDED_GOLDEN`,
> reproduced via the ci step's own `sim run --founded --seed 42 --turns 300 --hash-log`; first
> migration turn 3 both ways) and byte-identical over every turn of the driven world. Catchment is
> pipeline entry 1 and writes into NEXT, so the turn-1 world — PREV on turn 2 — already carries a row
> for every founded settlement; the turn-2 refusal comes from that row being PRESENT alongside the
> turn-1 zero staple harvest, which the rule forbids keying on. No substitute key was designed.
> Suite on this tree (Release, sequential): Sim.Tests **859 passed / 1 failed / 6 skipped** in 5 m 33 s,
> Sim.Ui.Tests **294/294**, three gate scripts green, `dotnet build -c Release` 0 warnings 0 errors.
> The +1 pass over the merge-finish tree is the new null-arm test; the ONE red is still
> `Dev_MalthusCorridors_AllInBand(seed: 7)` at the identical value (0.000102006), untouched here —
> `corridors.json` and the CR-003 quarantine were NOT re-pinned. Where the residual pre-packet →
> merged migration cut sits (vacancy bound, bounded flight, exit-openness rewrite) is ESCALATED to the
> director per RULE 1's own wording. Records: ADR-025 §2.3a/§2.4a, ADR-026 §2.1a,
> `docs/t4.21-2-record.md` §9, `docs/queue.md`. Verify against git.

> **2026-09-18 — T4.21-7 THE DISARM-AND-SETTLE LANE, on `claude/civdemo-work-b1z2y4` (worked
> directly on the integration branch, from `ee27c17`). THE FAMINE-CLASS DISASTER SHIPS COMPLETE AND
> TESTED BUT INERT.** `sim.json disaster.hazardPerYear` **0.01 → 0.0**, the exact inverse of
> T4.21-4's arming. This implements the orchestrator's decision on
> `docs/adr/cr-016-armed-disaster-fallout.md` — recorded there in full under "ORCHESTRATOR DECISION"
> — that the MECHANISM ships and the RATE becomes the director's ruling, because a world that dies
> by construction plus a broken PERMANENT dt-invariance detonator is a worse pathology than the one
> T4.21 fixed, and because choosing a lower rate to make the world survive would be the
> tuning-to-outcome CR-015 §6.5 forbids. `durationYears`, `severityMin/Max`, the §3.3 derivation and
> `corridors.json` are UNTOUCHED — the band is not what CR-016 disputes. **CR-016 STAYS OPEN: what
> is settled is the TREE, not the question.**
>
> **Suite, MEASURED by this lane on this tree, in BOTH configurations, sequential.** Debug:
> Sim.Tests **884 passed / 0 failed / 4 skipped** (30 m 31 s), Sim.Ui.Tests **296 / 0 / 0** (1 m 35 s).
> Release (the configuration CI and the earlier T4.21 records use): Sim.Tests **884 / 0 / 4**
> (8 m 6 s), Sim.Ui.Tests **296 / 0 / 0** (33 s) — the two agree test for test. `dotnet build` and
> `dotnet build -c Release` both 0 warnings 0 errors, and all three gate scripts exit 0
> (`check-banned-constructs.sh`, `check-read-isolation.sh`, `check-readonly-proof.sh`). **THE RED SET IS EMPTY.** All six reds T4.21-4 escalated (both
> canonical fed-corridor seeds, both CR-001 dt-continuity detonators, both dev Malthus seeds) are
> RESOLVED by the disarming, which is itself evidence that the arming was their sole cause — none of
> them needed chasing. The ONE red inherited from `8f7f9da` (`Dev_MalthusCorridors_AllInBand(7)`,
> the migration drift tooth) is resolved deliberately, like a golden, by re-pinning the recorded
> envelope to the measured λ = 0 values. The 4 skips are the four manual measurement rigs
> (`FoundingVariationItem0Tests`, `WaterRouteCounterfactualTests` ×2, `HousingBeforeColumnTests`);
> CR-015 N9's two lifts are NOT re-skipped.
>
> **All four world goldens and `ci.yml`'s `FOUNDED_GOLDEN` returned to their pre-arming constants
> BYTE FOR BYTE** (`db7c7a09…`, `98ee3a7a…`; the founded one derived twice — in-test harness and the
> Release CLI reproducing the ci step). That round trip is a stronger attribution than the λ = 0
> twin controls were: everything merged since `8f7f9da` (T4.21-5's observability, the chain-link
> merge fix, T4.21-6's eight finding fixes) is now MEASURED to move no world golden. Every measured
> pin returned too — artisan latch 70 / 13, merchant latch 119, founded/driven populations
> 40,539 / 6,373 at turn 300, first trade 28 / 7, and no pin failed to return.
>
> **What the disarming COSTS is recorded, not hidden.** Two `Cr003Quarantine` guards go back to
> quarantined (the dev world starves nobody and writes no famine chronicle line at λ = 0 — measured),
> `WorldReconciliationTests`' founded-starvation and driven-dwelling-decay coverage returns to
> ABSENCE pins, and `MigrationTests.MagnitudeCorridor_FedPhaseDrift_WithTeeth`'s UPWARD rate-lever
> tooth is quarantined in place (×1.0082 at λ = 0 against ×1.88 armed — below ADR-018 §11's own dead
> signature of ×1.07); its corridor assertion and both downward teeth stay live and it is NOT
> re-skipped. Each site carries BOTH readings and names CR-016 as what decides it, and every one
> reverses with the same single data edit. `docs/queue.md` carries them as open items.
>
> **The MECHANISM is still proven, by tests that arm λ in their own rigs, never by re-arming the
> shipped value**: a disaster CAN cause famine (forced-strike rigs and the λ = 0.01 in-rig battery
> arms), an ordinary bad harvest CANNOT, deliberate abandonment CAN on the SHIPPED config (mandate
> item 1(B), reachable in play today), λ = 0 gives zero famine while still giving STRESS, and the
> three determinism legs each assert a disaster actually fired. One claim was found exercised only
> by a dead test — the forensic disaster cross-check — and was given its own hazard.
>
> Records: `docs/adr/cr-016-armed-disaster-fallout.md` (decision, options, what the director is
> asked to rule), ADR-024 §11 (the disarmed re-measurement), CR-015's T4.21-7 append-only block,
> `docs/t4.21-4-record.md` (RETAINED as CR-016's evidence, now headed as measured AT λ = 0.01),
> `docs/t4.21-architecture.md` §4 row T4.21-4, `docs/queue.md`. Verify against git.

> **2026-09-18 — T4.21-8 THE FIX LANE, on `claude/civdemo-work-b1z2y4` from `8b59bab`. Three MINOR
> verifier findings, all fixed; NOTHING about the CR-016 decision is reopened.** (1) The shipped
> `sim.json` `disaster._doc` and CR-016 §2.1 pair the discontinuity **2.5259** with the components
> **+0.4352 / −1.8220**. Those are TWO DIFFERENT RIGS — re-measured by this lane at λ = 0.01:
> `Canonical_EraBoundaryContinuity_PermanentBatteryMember` (seed **1**, windows 1600–2500 /
> 2500–3400) is +0.4352 → −1.8220, discontinuity **2.2572**;
> `EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator` (turns 1..450) is +0.7616 → −1.7643,
> discontinuity **2.5259**. Both ~23–25× the 0.1 bar, so NO conclusion changes and the detonator is
> broken on both rigs. Only NEW prose was corrected (the `_doc`, and CR-016 §D.6, which is this
> lane's own section); the four frozen T4.21-4-era records carrying the same pairing are LEFT ALONE
> per `gov-4` §6 and tabulated in §D.6. (2) `SimConfigTests`' disaster substitutions anchored on
> `"hazardPerYear": 0.0` — a PREFIX of `"hazardPerYear": 0.01`, so on a re-armed tree
> `DisasterHazard_Armed_Loads` asserted against **0.011** (measured). All five sites now anchor on
> the literal WITH ITS TRAILING COMMA and fail through `AssertAnchorMatched`, whose message says to
> move the search string and never the expected value; `HazardAnchor_IsValueExact_NotAPrefix` is the
> new guard that makes the defect visible on the SHIPPED tree, and CR-016 §D.3's cost list now
> prices all six tests the director's ruling re-aims. (3) `docs/t4.21-architecture.md`'s constants
> table carries the disarm inline, like every other rate-bearing document.
>
> **Suite, MEASURED by this lane on THIS tree (`1fa8c1d`), in BOTH configurations, sequential.**
> Release (the configuration CI uses): Sim.Tests **885 / 0 / 4** (7 m 54 s), Sim.Ui.Tests **296 / 0 / 0**
> (33 s). Debug:
> Sim.Tests **885 / 0 / 4** (28 m 3 s), Sim.Ui.Tests **296 / 0 / 0** (1 m 3 s). `dotnet build` and `dotnet build -c Release`
> both 0 warnings 0 errors; all three gate scripts exit 0. **THE RED SET IS EMPTY.** The +1 against
> T4.21-7's 884 is `HazardAnchor_IsValueExact_NotAPrefix`. The 4 skips are unchanged.
>
> Records: `docs/adr/cr-016-armed-disaster-fallout.md` §D.3 and §D.6, `docs/queue.md`,
> `docs/t4.21-architecture.md` §the disaster constants table. Verify against git.
>
> **2026-10-03 — M5 INTEGRATION ASSEMBLED on `m5-integration` (tip `0b8cb14`), NOT on `main`.** Verified on
> `m5i-v-verify` (= `0b8cb14` + the verification stream). `origin/main` is `93270cd`, 0 ahead / 113 behind
> `0b8cb14`: NOTHING below is on `main` — not the frozen transport base `997824b`, not the streams.
> Stream branches (remote, merged into `m5-integration`): `m5i-s1-governing-loop` `43f38cd`,
> `m5i-u1-era-theme` `e61e182`, `m5i-s2-capability-actions` `8478b4d`, `m5i-u2a-actions` `09db372`,
> `m5i-u3-world-map` `0ec37ac`, `m5i-s3-institutions` `93dfcd2`, `m5i-u2b-views` `ecf3aa7`,
> `m5i-s4-ai-goals` `b952a9f`. Decisions: `docs/adr/adr-033-m5-integration-pass.md`. Status of every
> mechanic, what each stream fixed and what remains: `docs/m5-integration-coherence-matrix.md`.
> Schema v31; founded golden `74306d6a574b6a680e454eb385f88e9df2d6a74c5c16fd9af64930c3cdc55c1c`
> (`SnapshotTests.FoundedGoldenHash` = `ci.yml` FOUNDED_GOLDEN; unmoved by the verification stream).
> **Suite, MEASURED on `m5i-v-verify` in Release:** Sim.Tests **1370 / 0 / 5**, Sim.Ui.Tests **469 / 0 / 0**;
> all three gate scripts and both research `--check` scripts exit 0. Bench (`sim bench --founded --seed 42
> --turns 300`, median of 3): `997824b` 33,947 ms, this tree 34,515 ms (×1.017). Verification fixed one
> defect: a mid-game colony's orders made replay/`sim inspect` throw (`7100c77`). Not on `main`, not
> ruled: everything in this entry. Verify against git.

> **2026-10-03 — R1 (research trees drive gameplay) on `m5i-r1-unlock-pipeline`, cut from `m5-integration` @ `ac13c3d`;
> NOT merged, NOT on `main`.** Crafting recipes research-gated by content-declared recipe entities (pottery firing,
> bronze casting); Production domain on the action surface; full-corpus unlock audit
> `docs/research-gameplay-unlock-audit.md` (CI `--check`). Founded golden `68c629b66badfa717514e91c6efe9aaa2f8874d272d21afcee283fe1d85ce655`.
> Suite MEASURED in Release: Sim.Tests 1396 / 0 / 5, Sim.Ui.Tests 472 / 0 / 0. Record:
> `docs/m5-integration-coherence-matrix.md` §5. Verify against git.

> **2026-10-03 — R2b (governance and weather gaps) on `m5i-r2b-governance-fixes`, cut from `m5-integration` @ `f1fe76f`;
> NOT merged, NOT on `main`.** D-021 unrest-lite tax brake (Dignity bound to the tax burden → levy grievance → protest
> drag, discharge, uprising); weather correlation over geographic distance; Age military milestones need an
> age-appropriate formation (pending on M6; *2026-10-03 rebase: recruitment is now M7 Battle Layer*). Founded golden `1368df9f…` (superseded by R4a forager layer: `02c7f9eb0d08bf0ab31e6a9b0afb64041e4283e1fc4af1d4fbc94c8fa7b10ef2`).
> Suite MEASURED in Release: Sim.Tests 1418 / 0 / 6, Sim.Ui.Tests 472 / 0 / 0. Record:
> `docs/m5-integration-coherence-matrix.md` §6. Verify against git.

**Read this first, then verify it.** This file exists so an agent entering the repository with zero
conversation context can work out what to read next. It is a ROUTER and a STATUS BOARD. It is not
the Spine, not a milestone spec, not a D-decision, not an ADR, and it never restates one — where a
fact belongs to another document, this file names the document and stops.

**It is also not evidence.** Every claim below carries its provenance. Before you rely on any of
them, re-derive it from git or from the named document (§9). A value that could not be verified from
the repository is written `UNVERIFIED` with the check that would settle it.

**Measured 2026-08-31** in worktree `wt-food` against `food-anomaly-observability` at `8bd433a`,
with `origin/main` at `070f05b` as fetched. Every git figure below was read from `git rev-list`,
`git branch` or `git log` at that moment, not recalled from a session.

---

## 1. WHERE THE PROJECT IS

> **Pointer (2026-10-05, M5 hardening H3).** This table and §2 are the 2026-08-31 measurement, kept as written.
> The current milestone is **M5 Governing Gameplay, in progress** (the dated entries at the top of this file;
> `docs/milestones.md` roadmap rebase). `CLAUDE.md`'s milestone line reads M5 (verified on `9bb7423`; changed in
> `4448350`), so §2's contradiction no longer exists.
>
> *(2026-10-06, M5 polish pass.)* The "Documents required" list below the table is the 2026-08-31 (M4) list too,
> kept as written. For M5 work read: `CLAUDE.md` · `docs/milestones.md` (the authoritative roadmap) ·
> `docs/adr/adr-033-m5-integration-pass.md` (M5's decision record; M5 has no separate spec file) ·
> `docs/d049-taxation-and-revolt-model.md` · `docs/d050-m5-hardening-rulings.md` ·
> `docs/adr/adr-034-order-validation-deferral.md` · `docs/m5-playtest-baseline.md` §15–§16 ·
> `docs/m5-playability-gate.md` · `docs/spine-s8-governance-freeze.md` · `docs/gov-4-repository-freshness.md` ·
> `docs/queue.md`.

| | |
| --- | --- |
| **Current milestone** | **M4** — see §2 for why this contradicts `CLAUDE.md` |
| **Current objective** | M4 "Empire Control Foundation" — the structural minimum for the ratified Empire model |
| **Governing architecture** | `docs/d042-empire-and-player-control-addendum.md` (D-042) |
| **Milestone spec** | `docs/m4-spec.md` (R-1, R-2, R-3 ruled 2026-08-07; packet list FINAL) |
| **Authoritative baseline** | `CORRECTED` — `origin/main` = `dbef61a` (M4 completion merge, schema v24). As written: `070f05b` (T4.4 colonization, schema v22) |
| **Active implementation branch** | `m4-empire-control-foundation`, rebased onto `origin/main` — see §3 |
| **Integration state** | T4.4 v22 + M4 v23 + capacity-floor fix + D-042 + GOV-4; four goldens re-derived and causally attributed |
| **Certification** | `CORRECTED` — M4-A, M4-B, M4-C and M4-D are ALL MERGED; `origin/main`'s tip is the M4 completion merge. As written: B/C/D certified but unmerged |
| **Schema version** | **v24** (unchanged at the M4 exit gate) — v22 T4.4's `BucketRow`; v23 M4-A's Polities/Capitals; v24 M4-D's ConstructionQueue/Structures |

**Documents required before touching current work:** `CLAUDE.md` · `docs/m4-spec.md` ·
`docs/d042-empire-and-player-control-addendum.md` · `docs/spine-s8-governance-freeze.md` ·
`docs/civ-sim-architecture-v3-outline.md` · `docs/adr/adr-015-verification-hygiene.md` ·
`docs/gov-3-execution-protocol.md` · `docs/gov-4-repository-freshness.md` · `docs/queue.md`.

---

## 2. THE MILESTONE CONTRADICTION, STATED RATHER THAN RESOLVED

`CLAUDE.md:10` reads **"Current milestone: M3 — active packets: `docs/m3-spec.md` §4"**, and adds a
director amendment about the unmerged art-substrate branch.

The repository disagrees with that line. `docs/m4-spec.md` exists on `origin/main` and declares its
packet list FINAL under a dated director ruling; the merged history on `origin/main` carries T4.3,
T4.4, T4.8, T4.14 and T4.16 merges described as director-certified; and the branch list is dominated
by `t4.*` packet branches.

**The line has not been changed.** It is the director's — `CLAUDE.md` itself says it "changes only
at a milestone exit gate", and no exit-gate ruling for M3→M4 was found in the tree
(`UNVERIFIED`: an explicit M3 exit-gate record would settle it; searching `docs/` for one returns
`docs/m3-exit-session.md`, which records the session but was not confirmed to be the gate ruling).
Treat M4 as current per §5's hierarchy — the later milestone spec and the implementation both
outrank an older line in `CLAUDE.md` — and treat the `CLAUDE.md` line as a known stale entry
awaiting a director edit.

---

## 3. BRANCH AND PUBLICATION STATE — LOCAL, REMOTE AND MAIN ARE DIFFERENT THINGS

Three facts here are load-bearing, and collapsing any of them into "the repository has it" will
mislead the next agent.

1. **Local `main` is stale.** Local `main` = `87fb866`, `origin/main` = `070f05b`: **0 ahead, 8
   behind**. The 8 are the T4.4 colonization merge and the PR#4 AI-constitution admission.
2. **The work is now rebased onto `origin/main`, on a new branch.** `m4-empire-control-foundation`
   carries T4.4 + the whole M4 body and is **0 behind `origin/main`**. The pre-rebase history is
   preserved untouched at `origin/food-anomaly-observability` = `358b9a9`, so no force-push was ever
   needed and the old provenance is recoverable. REMOTE is true for both; **MAIN is still false**.
3. **Local `main` is still stale and was deliberately not moved.** It is checked out in other
   worktrees, so fast-forwarding it would disturb them. `origin/main` is the reference to use.

Anything on that branch is therefore **LOCAL**, not **REMOTE**, and not **MAIN**. Say which one you
mean, every time.

The full branch classification (64 remote branches: 49 MERGED, 15 unmerged) is recorded in the
session report for this packet rather than duplicated here, because it goes stale the moment anyone
pushes. **Re-derive it** with `git branch -r --no-merged origin/main` and the ahead/behind counts —
that command is the record, this file is not.

Two branch facts worth carrying anyway, because they are easy to miss:

- **`adr-019-architecture-addendum`** (`a36b94d`, 1 ahead / 63 behind) holds the **only copy of
  ADR-019**, which is absent from `origin/main`. Do not treat the ADR sequence on main as complete.
- **`claude/civdemo-work-b1z2y4`** exists but is 0 ahead / 63 behind and unrelated to current work.

---

## 4. STATUS OF OPEN THREADS

**M4-D — THE SETTLEMENT CONSTRUCTION QUEUE. CERTIFIED.**

A settlement holds an ordered construction queue; only its head is eligible, and that head is either
built whole this turn or waits unchanged. No build timer, no fractional progress, no construction
currency, no second labour pool. Capacity is the existing construction sector in adult-years less
housing's published draw, so builders genuinely compete with farmers. Materials leave settlement
good stocks all-or-nothing under `ReasonIds.ConstructionMaterials`. An Empire may only build where
it rules — the first production caller of `EmpireQuery.ControlsSettlement`.

A construction project is deliberately NOT a `RecipeEntry`: a recipe must output a good, and a
structure is not one. `ConstructionProjectEntry` is the smallest parallel definition.

Schema v23 → **v24** adds `ConstructionQueue` and `Structures`, empty in every canonical world, so
all four pinned worlds moved for two zero count prefixes alone. Audited by the generalised
attribution control before any repin, then repinned on director approval.

Certification run: `Sim.Tests` **572 passed / 6 failed / 6 skipped** (584, 24m07), `Sim.Ui.Tests`
**151/151**, build 0 warnings, all three gates OK. The 6 are the unchanged mainline quarantine.

> *2026-10-15 dated note (D-054; the M4-D text above is kept as written).* "Built whole this turn or waits unchanged.
> No build timer, no fractional progress … Materials leave settlement good stocks all-or-nothing" is SUPERSEDED for the
> shipped configuration by the Director's 2026-10-14 instruction: incremental construction, with progress stored in
> schema v33 `ConstructionProgress`, materials going in as they become available, and a cancel order (kind 11). The
> queue with only its head eligible, the competition with housing's draw, "no construction currency, no second labour
> pool", and an Empire building only where it rules all stand. Record: `docs/d054-batch3-incremental-construction-allocation-cylinder.md`
> §1 and §5.

**M4-C — FOUNDING INSTANTIATES THE EMPIRE. CERTIFIED.**

`WorldFounding.Found` now creates the founded world's one player-commanded Empire in the same
operation that creates its settlements: `PolityRow(PolityId 1, CommandSource.Player)`, one
`ControlRow` per founded settlement, one `CapitalRow` on the first. So `Found` never returns a
playable world whose settlements answer to nobody. Identity remains the existing D-037 `PolityId`;
`SettlementRow` carries no ownership field and no Empire container exists.

**The id is 1 by director ruling (CR-011), not 0** — the convention governing an order's actor is
the order corpus, which has stamped `ActorId = 1` since M1 including both binary replay fixtures.
Those fixtures were preserved unmodified and now resolve naturally.

**Three founded-world goldens moved, each audited before repin.** `IntegratedPinAttributionTests`
separates M4-A's schema LAYOUT from M4-C's founding CONTENT: emptying Polities/Controls/Capitals
returns each world's pre-M4-C pin byte for byte, so the Empire rows are the whole delta and no
simulation state moved with them. `GoldenHash_Seed42Turn200` did NOT move — the
no-unrelated-movement control holding.

Certification run: `Sim.Tests` **559 passed / 6 failed / 6 skipped** (571, 19m06), `Sim.Ui.Tests`
**151/151**, build 0 warnings, all three gates OK. The 6 are the unchanged mainline quarantine.

**M4-B — STRATEGIC ACTOR IN THE ORDER SEAM. CERTIFIED at `7d1734d`.**

An order's issuing strategic actor is the **existing `PolityId`**. The kernel spec §3.9 already
defined the log as `{turn, actorId, orderPayload}` for "player/AI orders", so `ActorId` was always
the strategic actor — it merely had no type and no enforcement. `PolityId` is a one-`int` identity,
so the binding required **no new field, no second identity, and no serialization change**:
`OrderRecord.Actor` is a projection with `Actor.Value == ActorId` always. `PolityId` is the typed
in-memory form; `ActorId` is the serialized form; they are one identity.

Actor existence is enforced in `OrderValidation` (the world-dependent layer), guarded on a non-empty
roster — **dormant today**, because nothing seeds `Polities`. Player/AI symmetry is preserved: the
only production references to `CommandSource` are the state definition, the serializer, the
`EmpireQuery` readers, and an existence probe in validation that discards the value. **No simulation
system branches on it.** No golden moved.

Certification run at `7d1734d` on a verified-clean tree: `Sim.Tests` **553 passed / 6 failed /
6 skipped** (565, 26m57), `Sim.Ui.Tests` **151/151**, build 0 warnings, all three gates OK. The 6
failures are the unchanged mainline quarantine set.

**CERTIFICATION RUN, measured at `5ebc1e3` — the tip this report certifies.**
`dotnet test Sim.Tests` → **541 passed / 6 failed / 6 skipped** (553 total, 16m45).
`dotnet test Sim.Ui.Tests` → **151 passed / 0 failed**. Build: 0 warnings, 0 errors. All three
repository gates OK.

The 6 failures are exactly the mainline quarantine set, unchanged from the pre-integration run and
identical test-for-test: four `CalibrationBatteryTests` (`Canonical_FedCorridors` seeds 1 and 2,
`Dev_MalthusCorridors` seeds 7 and 42) and two `ClassSystemTests` (`Artisans_EmergeInFedAutoplay`,
`Famine_DrainsArtisansBeforePeasantStarvationPeaks`). **No new deterministic failure appeared.**
`ProductionPipeline_PerPhaseBench_Reported` passed; it is load-sensitive wall-clock and is NOT part
of the deterministic quarantine.

**Food-anomaly certification: PAUSED, NOT COMPLETE.** The investigation concluded the reported
symptom is not reproducible (0/40 seeds) and that the real defect was a granary capacity floor. The
director ruled ACCEPT on the capacity-floor fix and approved the resulting `DrivenGolden` repin
(`24d107f`). The final full-suite regression run before merge was interrupted and **has not been
re-run**. The fix and its 7-test regression suite are on the active branch; **nothing is merged.**

**M4 Empire Control Foundation: implemented, blocked at the golden boundary.** `8bd433a` adds
`PolityRow`, `CapitalRow`, `CommandSource` and `EmpireQuery`, and moves `CanonicalSchema` to v22.
Two new empty count prefixes move all four pinned world hashes. That is a schema-only change, not a
behavioural one, so **the goldens were deliberately not repinned** — the pin change needs a director
ruling, on the T4.3/T4.8 precedent.

**The schema-version collision is RESOLVED by director ruling.** T4.4's v22 (`BucketRow` gaining
`UnplacedDeparture` and `UnplacedRemainder`) is authoritative because it merged to `origin/main`
first and is certified; M4's Polities and Capitals are **v23**. Exactly one meaning of every version
number survives, and T4.4's representation was not altered or reordered.

**All three moved goldens are re-pinned on the integrated tree, with the cause MEASURED.**
`IntegratedPinAttributionTests` strips the two empty v23 count prefixes — the tables' entire
contribution, since nothing writes them — and re-hashes. `GoldenHash_Seed42Turn200` and
`FoundedGolden_Seed42Turn300` return **main's exact pins** under that control, so both moved for the
M4 schema and nothing else. `DrivenGolden_Seed42Turn300` does not, which isolates the capacity-floor
fix as its second, behavioural cause. `ci.yml`'s `FOUNDED_GOLDEN` moved with its test.

**Open change requests awaiting director ruling:** CR-005 (M5 ownership of Research/Technology/
Institutions — *2026-10-03 roadmap rebase places research/knowledge/technology at M6*) · CR-006 (temporal control and epoch) · CR-007 (B3 exemplar — headline withdrawn by
the author; the record stands) · CR-008 (money owner) · CR-009 (era gates) · CR-010 (institution
definition) · D-042 §14.1 (D-018 income-column staleness).

**Quarantined / manually-run tests:** six `Skip =` entries in `Sim.Tests`. Two are quarantined
pending M4 migration work (T4.1b/ADR-018 §11 — asserts on gross migration, the wrong observable);
four are expensive measurement rigs kept off the default path by design, each naming the review
record that holds its numbers. The historical food-investigation harnesses were removed from the
default test path in `ca0aef0` and must not be re-run casually — see `docs/gov-4-repository-freshness.md` §9.

---

## 5. SOURCE-OF-TRUTH HIERARCHY

When two sources disagree, the higher one wins. **Do not silently reconcile them** — name both
sources, name which is authoritative, and say whether the conflict needs a director ruling.

1. Explicit director rulings in the current task or session
2. Later accepted D-numbered decisions and architecture addenda
3. Accepted ADRs
4. The current milestone specification
5. The current repository implementation
6. Tests and certification records — for behavioural truth specifically
7. Older architecture documents
8. Previous Claude/agent reports
9. Conversation memory
10. Agent inference

Note what this ordering does to a common mistake: a previous agent's report (8) loses to the code
(5), and agent inference (10) loses to everything. §2 above is an application of the rule, not an
exception to it.

---

## 6. KNOWN STALE AND FROZEN DOCUMENTS

**Stale — describes a state the repository has moved past:**

- `CLAUDE.md:10` — the M3 milestone line and its art-substrate amendment (§2).
  *(2026-10-06 note, M5 polish pass: no longer the M3 line — `CLAUDE.md:10` has named M5 Governing Gameplay (in
  progress, NOT complete) with the rebased sequence since `4448350`. Still stale on `m5-hardening`, and the
  Director's to edit: line 10 names `m5-integration` as M5's branch (the candidate is `m5-hardening`, built on
  it) and keeps the M3-era art-substrate amendment; line 7 still calls `docs/m0-kernel-spec.md` the "current
  milestone spec" (M5 has no spec file; its decisions are ADR-033, D-049 and D-050).)*
- Records that number milestones on a pre-2026-10-03 ladder (M6 battle / M7 knowledge / "Ancient Vertical Slice" /
  "Era Expansions"): superseded by `docs/milestones.md` §"Roadmap rebase 2026-10-03"; they carry dated pointers,
  except pure history left as written (`docs/handoff-status.md`, `docs/m2-spec.md`, `docs/m4-exit-session.md`) and
  the read-only research corpus `tech-graph-v0.6.json` (SHA-256-pinned by `ResearchContentTests`) with its `.html`
  view (D-040's pointer covers their "M7" knowledge reading). The M5 polish pass (2026-10-06) added the pointers that were missing: D-037, D-039,
  `docs/t4.6-foreign-trade-decision.md` §6, `docs/design/civilization-progression-architecture.md` 5.6,
  `docs/design/arch-I-climate-environment.md`, `docs/design/recovered-decisions-climate-env-agri.md`,
  `docs/design/recovered-decisions-food-disaster-needs.md`, `docs/m4-blocking-material.md` (B-2 addendum) and
  `docs/m4-exit-inventory.md` §7; the Spine's system-table note was moved below the table so the table renders whole.
- `docs/handoff-status.md` — a previous, measured attempt at this same routing role, dated
  2026-07-28 against `t3.4c-variance-fix` at `719152a`. Its branch table, suite counts and milestone
  are all from M3. **It is superseded by this file for routing purposes and left untouched as a
  historical measurement.**

**Frozen — stale in places but deliberately not rewritten:** the Spine
(`docs/civ-sim-architecture-v3-outline.md`), `docs/spine-s8-governance-freeze.md`, closed
D-decisions, accepted ADRs, and every investigation and review record. A later decision superseding
part of one of these does **not** license editing it. If a frozen document creates a real ambiguity
for current work, record the ambiguity (queue entry or CR) — do not resolve it by editing.

---

## 7. QUEUED AND NEXT

`docs/queue.md` is the queue; it is not summarized here. The immediate decisions the tree is waiting
on are the CRs in §4 and the golden-repin ruling for `8bd433a`.

---

## 8. UPDATE RULE

Update this file **after an accepted implementation packet**, and only with what the repository can
show: the new accepted commit, the new current task, newly accepted decisions, newly resolved
blockers, newly discovered stale or conflicting documents, and the next queued packet.

Never update it speculatively, and never to record an intention. It represents repository truth, and
a forecast written here becomes next session's false premise.

---

## 9. PROVENANCE OF EVERYTHING ABOVE

Read from git in worktree `wt-food` on 2026-08-31: branch tips, ahead/behind counts, merged/unmerged
partitions, the local-vs-`origin/main` divergence, and commit subjects. Read from the working tree:
`CLAUDE.md:10`, `docs/m4-spec.md`, `docs/handoff-status.md`, `docs/queue.md`, the `Skip =`
attributes in `Sim.Tests`, and the presence or absence of documents on `origin/main`.

Carried from the session that produced this file, and therefore **secondary evidence** under §5:
the director's ACCEPT ruling on the capacity-floor fix, the paused state of its certification, and
the list of open CRs. Each is verifiable — the CRs as files under `docs/adr/`, the ruling and pause
from the session record — but none was re-derived from git for this file.

`UNVERIFIED` values are marked inline. There is one: the M3→M4 exit-gate ruling (§2).
