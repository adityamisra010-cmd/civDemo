# H-X — CR-013 Windows/Linux determinism: the DetMath kernel experiment (lane X, M7 Phase C)

Written 2026-11-06 by the lane X agent. Base tree: development branch `claude/new-session-yavghm` at `dfdbc2d`
(`/home/user/m5w/dev`). This report is the only file lane X hands back (`prototypes/m7-sprint1/reports/H-X.md`).
**Verification round 1** (verifier findings X-R1-1 to X-R1-7, all seven reproduced and fixed) is §14; the statements it
corrected are marked *(corrected in round 1)* where they stand. **Verification round 2** (findings X-R2-1 to X-R2-6,
all six reproduced, none refuted; X-R2-1 to X-R2-4 fixed in the experiment with tests, X-R2-5 corrected in this report
and two code comments, X-R2-6 a proposal gap closed in §9 step 3) is §15; the statements it corrected are marked
*(corrected in round 2)*. The experiment head is now `823a315`.
**No `Sim.*` file is changed on `lane/X`.**

The kernel change itself is an **EXPERIMENT that stays outside the repository** (D-079 Q5 and Q15 are OPEN):
branch `lane/X-kernel-experiment` in the lane clone `/dev/shm/m7c/X`, saved as
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/patches/X-kernel-experiment.patch`
and `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/bundles/X-kernel-experiment.bundle`
(ref `refs/heads/lane/X-kernel-experiment`, based on `dfdbc2d`). It was never integrated or pushed.

> **WINDOWS EQUALITY IS UNTESTED.** Every number below was measured on one Linux x64 host: Ubuntu glibc 2.39, .NET
> runtime 10.0.12 (SDK 10.0.112), "Intel Xeon Processor @ 2.10GHz" with AVX2, FMA and AVX-512, 4 CPUs shared with
> other agents. No Windows run was made. **This report does not claim cross-platform hash equality.**

Labels: **MEASURED** (run by this lane in this session, on the commits named); **RE-VERIFIED** (read in the tree at the
cited line in this session); **DOC-ONLY** (stated in a repository document, not re-measured); **INFERRED**. Earlier
lane-X notes and logs were lost in container restarts; **every number here was re-measured on 2026-11-06.**

---

## 0. Summary

1. **The experiment.** All 48 transcendental calls in `Sim.Core` (`Math.Exp` 29, `Math.Pow` 15, `Math.Log` 3,
   `Math.Cos` 1; 47 code lines; 15 files) go through one chokepoint, `Sim.Core/Kernel/SimMath.cs`. A build property
   picks the implementation: unset (the default) compiles each `SimMath` method to the `System.Math` call it replaces;
   `-p:SimNumerics=detmath` compiles it to `DetMath`, the O-17 prototype copied into `Sim.Core/Kernel` with only its
   namespace (and one comment line) changed. `Sim.Data` and `Sim.Cli` contain no transcendental call. MEASURED/RE-VERIFIED.
2. **Switch OFF moves no golden.** The full `Sim.Tests` suite in Release passes with **zero pin edits: 3,479 test cases, 3,443 passed, 0 failed, 36 skipped** (each skip by its own `Skip` reason; the run had to be split because the container killed large test hosts, §2). The per-turn hash logs of 7 CLI legs (2,500 turn hashes:
   canonical seed 42 × 400 turns; founded seeds 42, 16, 1, 2, 7 × 300; founded seed 42 with one AI empire × 600) are
   byte-identical to the unmodified `dfdbc2d` build. MEASURED. *(Round 1, on `fb3698b`: the 7 legs again byte-identical
   to a fresh build of the `dfdbc2d` tree, and all 80 cases of the 76 moved-pin methods pass in the OFF build; the full suite was not
   re-run, §14.3.)*
3. **Switch ON moves every campaign golden, and only doubles move in the sampled states.** Under ON, `Sim.Tests` gives **3,365 passed, 78 failed, 36 skipped** of 3,479, and **every one of the 78 failures is a pinned world hash** (54 distinct old→new values, §3); no replay, twin, save/load, conservation or semantic-turn test fails. The founded
   seed-42 hash moves from `FOUNDED_GOLDEN` `d5ebd0b2…` to `86fbbc16…` (the value O-17 measured). First differing turn
   against today's numerics: seed 42 turn 9, seed 16 turn 3, seed 1 turn 19, seed 2 turn 7, seed 7 turn 17, AI leg turn
   6; the canonical (toy) world never differs. At 19 sampled (leg, turn) states, **0 of 10,069–26,399 integer and bool
   fields differ**; 1–21 double fields differ (weather and price terms first, then banked migration and produce
   remainders). MEASURED.
4. **A new finding: today's goldens depend on the CPU, even on Linux.** On this one host, turning glibc's CPU dispatch
   down (`GLIBC_TUNABLES=glibc.cpu.hwcaps=-AVX2,-FMA,-AVX512F,-AVX512DQ,-AVX512VL,-AVX`) changes 651 exp, 107 log, 662
   pow and 715 cos results per million inputs (all by 1 ulp), and the libm build's founded seed-42 hash then differs
   from `FOUNDED_GOLDEN` from turn 13. The DetMath build's 300 per-turn hashes are identical under that change, under
   `DOTNET_EnableAVX=0` (SSE-only JIT code), with tiering off, with PGO and ReadyToRun off, and with all of them at once.
   MEASURED.
5. **No FMA contraction.** The JIT emitted 0 FMA instructions in every method of a 60-turn founded run (8,175 to 8,257
   methods tiered; 2,141 to 2,156 fully optimised), OFF and ON, on an FMA-capable CPU. MEASURED.
6. **DetMath is correctly rounded on every vector tried:** the 9,180 stored oracle vectors and two held-out sets
   generated for this report with new seeds (17,930 and 175,430 vectors), 0 mismatches in 202,540. glibc was wrong on 10
   and 106 of the held-out vectors. Not a proof of correct rounding (§6). MEASURED. *(Round 1, X-R1-4:)* those log
   vectors were log-uniform or bit patterns; on the campaign's own log input (the Box–Muller u1, uniform in (0,1))
   glibc misrounds about 0.08 %. DetMath: 0 mismatches on a new 4,200-vector Box–Muller fixture and on 200,000 held-out
   uniform inputs, where glibc misrounded 164 (§6, §14).
7. **Cost.** `sim bench` (founded 42 × 300 turns, four alternating runs per build, loaded host): median 16,483 ms OFF, 19,446 ms ON, **+18.0 %**, almost all of it in `DemographicsSystem` (2,752 → 5,800 ms). Indicative; a quiet-host bench is owed (§5). MEASURED.
8. **Grep audit.** *(Corrected in round 1, X-R1-3: the audit missed one platform-variant input. The forensic
   `configDigest` and run id SHA-256 the raw bytes of the embedded content JSON, so a CRLF checkout changes them —
   measured: same 300 world hashes, different digest and run id; remedy proposed as landing step 4a. It does not touch
   the world hash.)* Apart from the 48 transcendental calls, nothing in `Sim.Core`/`Sim.Data`/`Sim.Cli` was found that
   is platform-variant on Windows/Linux x64: no other transcendental, no FMA, no `float`/`decimal`, no vector
   intrinsics, no parallelism, invariant globalisation, exact rounding modes, culture-invariant parsing (§7.3).
9. **Mutants.** 5 facade/switch mutants, bounded at 30 s (10 × the 2.7 s clean run): 4 killed by semantic tests, **1
   survivor (S-M5): a broken switch condition that silently builds libm when DetMath was asked for** passes every
   `SimMathNumericsTests` test. The experiment now kills it: commit `f8e0acc` records the raw build property in the test assembly and asserts that `Sim.Core` compiled what was asked for (it also refuses a mistyped value — at test level only; *round 1 adds the build-time refusal, X-R1-5*). The scan rule caught 6 of 6 injected spellings (§8). *(Corrected in round 1, X-R1-1/X-R1-2: the record overstated the coverage. A `Log` hole in the switch and four further spellings of a raw libm call survived every test and the scan. Commit `fb3698b` adds compiled-IL checks of the switch and of the chokepoint, campaign-distribution facade inputs, a build-time property guard and a Cos-domain test; 18 of 18 round-1 mutants are killed: 17 by semantic tests, and the mistyped property by the build itself (its guard is pinned by a test, mutant GUARD), §14.2.)* *(Corrected in round 2, X-R2-1 to X-R2-4: the round-1 checks still missed a libm call made inside a System.Numerics API (`Complex.FromPolarCoordinates`), a libm path through a private SimMath helper, a guard detached from the build, and a member access split across lines; the bash rule had no automated teeth. Commit `823a315` closes each; 29 of 29 mutants (11 new, the 18 of round 1 re-run) are killed, 28 by semantic tests and the mistyped property by the build, §15.2. The IL checks still do not follow calls INTO the BCL, §15.4.)*
10. **Recommendation for Q5 (PROPOSAL):** (a), staged — land the switch **default OFF** with the scan ban, the numerics
    id and a Windows+Linux CI leg now; flip and re-pin once only after that leg shows equality (§11). The exact landing
    plan is §9; the Windows evidence still needed, by route, is §10.

---

## 1. The experiment and its provenance

| Item | State |
|---|---|
| Earlier work | Commit `02152b5` ("route Sim.Core transcendentals through SimMath (build switch, default OFF)"), parent `fc5230c`, restored from `scratchpad/m7c/wip/X-kernel-experiment.bundle` (bundle verified) |
| Rebase check | `git diff --stat fc5230c dfdbc2d -- Sim.Core Sim.Data Sim.Cli Sim.Tests scripts Directory.Build.props` is **empty** (RE-VERIFIED), so the experiment applies unchanged |
| Experiment branch | `lane/X-kernel-experiment` = `02152b5` cherry-picked onto `dfdbc2d` → `e738f08`; the two diffs are byte-identical (md5 of `git diff` equal); then `f8e0acc`, the S-M5 fix of §8 (`Sim.Tests` only); then `fb3698b`, the verification-round-1 fixes (§14); then `823a315`, the verification-round-2 fixes (§15). The branch head is `823a315` |
| Size | 24 files, +1,042 / −58 lines for `e738f08`; with `f8e0acc`, 24 files, +1,073 / −58; with `fb3698b`, 28 files, +15,027 / −58 (13,380 of the added lines are the two oracle-vector fixture files); with `823a315`, 28 files, +15,341 / −58 |

Files of `e738f08` (all on the experiment branch only):

| File | Role |
|---|---|
| `Sim.Core/Kernel/SimMath.cs` (75 lines) | The chokepoint: `Exp`, `Log`, `Pow`, `Cos`, and `NumericsId` (`"libm"` or `"detmath-1"`). Each method is `#if SIM_NUMERICS_DETMATH` DetMath `#else` System.Math, so the OFF build contains no branch. *(Corrected in round 2, X-R2-5: true of `e738f08` only. Since `fb3698b` the file has 90 lines (94 in `823a315`, comments only) and the OFF build keeps one branch: `Cos`'s domain guard (X-R1-7), which both builds share and which runs before the `#if`. It changes OFF behaviour: for a finite \|x\| > 2^28 the libm build used to return a value (`Math.Cos(1e9)` = 0.8378871813639024, §14.1) and now throws. The only caller (2πu, u in [0,1)) never reaches it; the 7 legs and the 80 moved-pin cases are unchanged, §14.3.)* |
| `Sim.Core/Kernel/DetMath.cs` (450), `DoubleDouble.cs` (88) | The O-17 prototype (`prototypes/m7-sprint1/Proto.DetMath`); `diff` against the prototype after the namespace substitution: one doc-comment line ("no float" → "no single precision", because the D-004 `float` gate has no comment exemption). RE-VERIFIED; pinned by `TheKernelCopy_IsThePrototype_ModuloNamespaceAndOneCommentLine` |
| `Sim.Core/Sim.Core.csproj` | `SimNumerics=detmath` defines `SIM_NUMERICS_DETMATH` |
| 15 call-site files | `Math.X(` → `SimMath.X(` (48 calls; `using Sim.Core.Kernel;` added where missing) |
| `Sim.Tests/TestUtil/DemographicsReplica.cs`, `HarvestWeatherReference.cs` | The two bit-exact test replicas call `SimMath` too, so they stay exact under both builds |
| `Sim.Tests/Kernel/SimMathNumericsTests.cs` (342; 994 in `823a315`) | 13 test cases: facade identity (4 × 20,000 inputs, against the function the build names), oracle vectors (4 functions, 9,180 vectors, linked from the prototype), source scans (no raw transcendental outside `SimMath`; DetMath libm-free), provenance, C99 Annex F special values. *Round 1 (`fb3698b`): 42 test cases, listed in §14. Round 2 (`823a315`): 71, §15* |
| `Sim.Tests/Sim.Tests.csproj` | Links `cr-vectors.txt` from the prototype. *Round 1: copies of the vectors and generators in `Sim.Tests/Fixtures/detmath` instead (X-R1-6)* |
| `scripts/check-banned-constructs.sh` | New rule "raw transcendental outside SimMath (CR-013)". *Round 1: the import rule; round 2: one `CR013_NAMES` list, the System.Numerics / System.Runtime.Intrinsics rule and a perl pass for matches split across lines (§15)* |

Why a **build-time** switch for the experiment: it cannot leak between xUnit test classes running in parallel in one
process, it costs nothing when OFF (no branch survives compilation) *(corrected in round 2, X-R2-5: no SWITCH branch survives; since `fb3698b` `Cos`'s shared domain guard is a branch the OFF build keeps, see the `SimMath.cs` row above)*, and it needs no plumbing into the static helpers
of 15 files. Its weakness is that both numerics cannot run in one process; the landing plan (§9 step 5) handles the
attribution twin with a second CI leg rather than an in-process twin.

## 2. Switch OFF moves no golden (MEASURED)

**Full suite, Release, switch OFF** (`dotnet test Sim.Tests/Sim.Tests.csproj -c Release`, build of `e738f08` with no
property):

| Run | What happened | Result |
|---|---|---|
| Attempt 1: one test host, default parallelism (4 threads) | the container's memory cgroup OOM-killed the test host | 914 passed, 0 failed, 15 skipped of 929 reached |
| Attempt 2: one test host, `xUnit.MaxParallelThreads=2`, `DOTNET_GCConserveMemory=7`, nothing else of this lane running | OOM-killed again after 49 m 56 s (the kernel log shows other agents' processes killed in the same minutes) | 3,421 passed, 0 failed, 36 skipped of 3,457 reached |
| Targeted rerun, own test host: the 4 classes holding the 22 tests attempt 2 never reached (`ForeignTradeScopeTests`, `KnowledgeEffectsTests`, `LawCourtAiWorldAttributionTests`, `WorldgenPerfTests`) | completed | 38 passed, 0 failed (the 16 already-run siblings agree with attempt 2) |
| **Union over the 3,479 listed test cases** (`--list-tests` on this build; 3,466 baseline + the 13 `SimMathNumericsTests` cases) | every case has a result | **3,443 passed, 0 failed, 36 skipped** |

The 36 skips are the suite's own `[Fact(Skip = …)]` measurement rigs; each carries its skip reason in the trx.

**CLI legs, OFF against the unmodified `dfdbc2d` build** (`sim run … --hash-log`, Release, both builds copied out of
the tree so they ran side by side):

| Leg | Turns | Final hash (both builds) | Per-turn logs |
|---|---|---|---|
| canonical seed 42 | 400 | `ba223ffe…18767f0` | identical, 400/400 |
| founded seed 42 (the `ci.yml` `FOUNDED_GOLDEN` leg) | 300 | `d5ebd0b2…306f93501` = `FOUNDED_GOLDEN` (`ci.yml:266`) | identical, 300/300 |
| founded seed 16 (the `ci.yml` second-seed leg) | 300 | `5bb5d61e…f9857c` | identical, 300/300 |
| founded seed 1 | 300 | `542ffa8a…552273` | identical, 300/300 |
| founded seed 2 | 300 | `9acee8f0…f27f30` | identical, 300/300 |
| founded seed 7 | 300 | `d8eac10b…04b56b` | identical, 300/300 |
| founded seed 42, `--ai-empires 1` | 600 | `ce267650…5b9daf76` | identical, 600/600 |

`cmp` of each pair of logs returned equal: 2,500 per-turn hashes. This is also what the construction predicts: in the
OFF build each `SimMath` method is a single call of the `System.Math` method with the same argument, and
`TheFacade_IsExactlyTheSwitchedFunction_OnTheCampaignRanges` checks that bit for bit on 80,000 inputs. *(Corrected in
round 2, X-R2-5: true of `e738f08`, on which these runs were made. Since `fb3698b`, `Cos` first applies the domain guard
both builds share; see §1.)*

The baseline tree itself (`dfdbc2d`) was built (Release, 0 warnings) and its test list enumerated (3,466 test cases);
its full suite was not re-run, by the orchestrator's instruction to run the long suite only as often as the evidence
needs. The OFF suite above is that evidence: it runs every pin of the baseline plus the 13 new test cases.

## 3. Switch ON: which goldens move (MEASURED)

**Full suite, Release, switch ON** (`-p:SimNumerics=detmath`; `SimMath.NumericsId = detmath-1` confirmed in the built
assembly):

| Run | What happened | Result |
|---|---|---|
| One test host, `xUnit.MaxParallelThreads=2` (worktree `X-on`, built with `-p:SimNumerics=detmath`) | OOM-killed after 15 m 29 s | 1,312 passed, 22 failed, 22 skipped of 1,356 reached |
| The 177 classes not fully covered, 9 chunks of 20 classes, one test host each | chunk 1 OOM-killed; chunks 2–9 completed | — |
| The 11 classes still uncovered, 4 chunks, `MaxParallelThreads=1` | completed | — |
| **Union over the same 3,479 test cases** | every case has a result | **3,365 passed, 78 failed, 36 skipped** |

**All 78 failures are `Assert.Equal` on a SHA-256 world hash** (each trx message reads `Assert.Equal() Failure: Strings differ … Expected: "<hex>" Actual: "<hex>"`); no failure of any other kind occurred. What held under ON, among others: `AiEmpireIntegrationTests` 13/13 (the turn-exact semantic pins, such as levy and A3 turns), `DeterminismHarnessTests` 4/4, `FoundedHarnessTests` 5/5, `ResearchDeterminismTests` 3/3, `IntegratedSaveLoadBatteryTests` 22/22, `FirstReignTests` 2/2, the `D054PairAttributionTests` "toy" and "first-reign" references, and `SimMathNumericsTests` 13/13 under the DetMath build.

Named reference goldens among the moved pins: `ci.yml` `FOUNDED_GOLDEN` `d5ebd0b2…` → `86fbbc16…` (via `MilitaryAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheMilitaryLayerAlone`; equal to the CLI value below); `SnapshotTests.FoundedGolden_Seed42Turn300_MatchesPinnedConstant` `bb825342…` → `a4350c0c…`; `DrivenGoldenTests.DrivenGolden_Seed42Turn300_MatchesPinnedConstant` `f320440f…` → `4f4a27a9…`; the batch-2 founded, driven and AI references `937577ff…` → `a9ff174a…`, `4f4e0b81…` → `98fbdedc…`, `b2e4f333…` → `bc37d21d…`; the AI 1024 cylinder `25f4799d…` → `9bff4842…`. The `FOUNDED_GOLDEN` move, the three batch-2 references, the AI 1024 cylinder and the five `AiFoundingAttributionTests`/`GranaryAttributionTests` AI-world values (`aa7a87a9…`, `2e4759ac…`, `5edcc3e0…`, `5fb6aa9e…`, `55fbce7e…`) equal the old→new prefixes O-17 recorded on 2026-11-04 (comparison with O17-detmath.md §6, DOC-ONLY; every value here was re-measured). O-17 did not run `SnapshotTests` or `DrivenGoldenTests`.

**The complete list of moved pins (78 tests, 54 distinct values), as measured:**

| Old pin (12 hex) | DetMath value (12 hex) | Tests | Test(s) (`Sim.Tests.*`, namespace dropped) |
|---|---|---|---|
| `937577ffb0e7` | `a9ff174a2ef9` | 7 | `D054PairAttributionTests.WithBothHalvesOff_EveryBatch2ReferenceReproducesByteForByte_ThroughTheV32Projection(reference: "founded")`; `GranaryAttributionTests.TheFoundedGolden_IsUnmovedByTheGranaryEffects_OnOrOff`; `IncrementalConstructionAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheV33LayoutAlone`; `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheForagerLayerAlone`; `WrapAttributionTests.WithTheCylinderOff_TheFoundedGoldenIsTheBatch2Golden_AndOnItDiffersFromTurnZero`; `EmpireConstructionStockpileTests.WithTheBatchOff_TheFoundedGoldenIsDe86aceByteForByte_AndThePurseMovesItNot`; `EmpireFoodAllocationTests.Switch_Off_FoundedGolden_ByteIdenticalToPreRelief` |
| `25f4799d581d` | `9bff484250fd` | 4 | `CylinderAttributionAi1024Tests.ReliefAbsence_OnTheAi1024Cylinder_ReliefNeverActs_AndTheReliefOffWorldIsTheSameEveryTurn`; `CylinderAttributionAi1024Tests.TheSharedOnArm_IsTheShippedCylinder_AndReproducesThePinnedAiRun`; `CylinderAttributionAi1024Tests.Ws1Absence_OnTheAi1024Cylinder_NoHeadSurvivesAStep_AndTheWs1OffWorldIsTheSameEveryTurn`; `DiscoveryLayoutAttributionTests.AiWorldSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte` |
| `aa7a87a98370` | `9def8e593568` | 4 | `AiFoundingAttributionTests.WithTheSwitchOff_TheAiWorldIsThePreS1cWorldByteForByte_AndOnItDivergesExactlyAtTheFirstFoundingsDelivery`; `GranaryAttributionTests.WithTheBatchOff_TheAiWorldIsTheDe86aceWorldByteForByte_AndTheGranaryLayerAloneDivergesTheStepAfterAGranaryFirstActs`; `EmpireConstructionStockpileTests.WithTheBatchOff_TheAiWorldIsDe86aceByteForByte_AndThePurseAloneDivergesExactlyAtTheFirstDifferentConstructionOrdersDelivery`; `EmpireFoodAllocationTests.Switch_Off_AiWorld_ByteIdenticalToPreRelief` |
| `4f4e0b81a588` | `98fbdedc361a` | 3 | `D054PairAttributionTests.WithBothHalvesOff_EveryBatch2ReferenceReproducesByteForByte_ThroughTheV32Projection(reference: "driven")`; `IncrementalConstructionAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheV33LayoutAlone`; `WrapAttributionTests.WithTheCylinderOff_TheDrivenGoldenIsTheBatch2DrivenGolden` |
| `b2e4f333c25a` | `bc37d21d4828` | 3 | `D054PairAttributionTests.WithBothHalvesOff_EveryBatch2ReferenceReproducesByteForByte_ThroughTheV32Projection(reference: "ai")`; `IncrementalConstructionAttributionTests.AiWorldSeed42Turn300_MovedForTheV33LayoutAlone_TheIncrementalRuleNeverActsThere`; `WrapAttributionTests.WithTheCylinderOff_TheAiWorldIsTheBatch2AiWorld` |
| `bb9cc22dba2c` | `fbed435bc476` | 3 | `CylinderAttributionReliefAbsenceTests.ConstructionPurseAbsence_OnTheFoundedCylinder_NothingIsEverQueued_AndThePurseOffWorldIsTheSameEveryTurn`; `CylinderAttributionReliefAbsenceTests.GranaryEffectsAbsence_OnTheFoundedCylinder_NoGranaryEverStands_AndTheGranaryOffWorldIsTheSameEveryTurn`; `CylinderAttributionReliefAbsenceTests.ReliefAbsence_OnTheFoundedCylinder_ReliefNeverActs_AndTheReliefOffWorldIsTheSameEveryTurn` |
| `02500b4afbdd` | `973279fcd77f` | 2 | `Adr054AttributionTests.AiWorldSeed42Turn300_MovedForAdr054Alone`; `CivicsPointsAttributionTests.AiWorldSeed42Turn300_MovedForCivicsPointsAlone` |
| `1f9f3d4f8bfa` | `9c825873beb5` | 2 | `Adr054AttributionTests.FoundedGoldenSeed42Turn300_MovedForAdr054Alone`; `CivicsPointsAttributionTests.FoundedGoldenSeed42Turn300_MovedForCivicsPointsAlone` |
| `40115e28af11` | `b10a016394c4` | 2 | `ReservationsLayoutAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheV34LayoutAlone`; `ReservationsLayoutAttributionTests.FoundedGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33` |
| `7bcdc03ff5bf` | `d1e65e4405a4` | 2 | `EmpireFoodAllocationTests.PerLayer_Relief_TheDrivenWorldDivergesExactlyAtTheFirstRelief`; `EmpireFoodAllocationTests.Switch_Off_DrivenGolden_ByteIdenticalToPreRelief_AndOnItMoves` |
| `f23e0dcd49b0` | `cb0d89dbb3a3` | 2 | `Adr054AttributionTests.DrivenGoldenSeed42Turn300_MovedForAdr054Alone`; `CivicsPointsAttributionTests.DrivenGoldenSeed42Turn300_MovedForCivicsPointsAlone` |
| `fa1ce900ea15` | `a5fc5ce8fcf8` | 2 | `ReservationsLayoutAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheV34LayoutAlone`; `ReservationsLayoutAttributionTests.DrivenGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33` |
| `003a7e49a935` | `c4e254ae50b5` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone` |
| `03b86400c4e5` | `fc63e29ca74f` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheForagerLayerAlone` |
| `0dadbdfa26c8` | `c37202418cbe` | 1 | `MilitaryAttributionTests.ThePreM6AndPreTranche2Worlds_MovedForTheV39LayoutAlone` |
| `0f29efc73302` | `eec45a84e876` | 1 | `DiscoveryLayoutAttributionTests.FoundedGoldenSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte` |
| `14365a5c7529` | `1dfd305a7932` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone` |
| `17105b1f871e` | `40b60b4c5114` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheAgeLayerAlone` |
| `246ff3f5b619` | `0bc15de6a3b6` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone` |
| `2b4d88e0b6f2` | `6786d5295e99` | 1 | `KnowAttributionTests.AiWorldSeed42Turn300_MovedForTheKnowLayerAlone_TheTwinIsTheBaseWorld` |
| `2e4759ac494d` | `0f31a6a3ba1e` | 1 | `AiFoundingAttributionTests.AiWorldTurn300_BothBatch3SwitchesOff_IsTheE91d10bWorld` |
| `2f72ec211b38` | `bf20516b21c9` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone` |
| `392e280665e7` | `514d50b1ddfb` | 1 | `ResearchLevelsLayoutAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheV36LayoutAlone` |
| `52f480ba1c4d` | `05871d8f8810` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheAgeLayerAlone` |
| `5458cb3d2564` | `e95e50f4a2dd` | 1 | `MilitaryAttributionTests.AiWorldSeed42Turn300_MovedForTheMilitaryLayerAlone` |
| `55afd498a326` | `8267ed1ce18b` | 1 | `MilitaryAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheMilitaryLayerAlone` |
| `55fbce7ebad4` | `18f6ae1d6be9` | 1 | `AiFoundingAttributionTests.TheM6Slice_IsTheWholeMove_TheAiWorldWithTheSliceOffIsTheBatch4World` |
| `5a8c6c648daa` | `53be9c351b29` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheTimeAveragedHarvestAlone` |
| `5c9344e4d894` | `0e453f673e2a` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheM4SchemaAlone` |
| `5edcc3e08123` | `d9bca9d70fdc` | 1 | `AiFoundingAttributionTests.AiWorldTurn300_WithTheKnownTerrainValuationOff_IsThePreA18World` |
| `5fb6aa9e8349` | `411b34f2ea3e` | 1 | `AiFoundingAttributionTests.AiWorldTurn300_WithTranche2Off_IsThePreTranche2World` |
| `66bcd40e56f5` | `fe54df7bfb45` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone` |
| `7d5e626004fb` | `0db7da58e721` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheResearchLayerAlone` |
| `8ccbe80fe2be` | `bd01a6fdbb8f` | 1 | `ResearchLevelsLayoutAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheV36LayoutAlone` |
| `91836bcf92b1` | `2f253523a672` | 1 | `ReservationsLayoutAttributionTests.AiWorldSeed42Turn300_MovedForTheV34LayoutAlone` |
| `9308cd36f9a8` | `d98574db2fe1` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone` |
| `98d0e2675da9` | `ccce9a16619a` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheV32LevyLayerAlone` |
| `9c8380a27a4b` | `923f2bfeb23a` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone` |
| `ab380feba01c` | `185d40711c9f` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheR2bLayersAlone` |
| `ac38d4ea209d` | `acdec4b12c93` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheTradeKnowledgeLayerAlone` |
| `b2201d255f74` | `ffc3fbe0471a` | 1 | `ResearchLevelsLayoutAttributionTests.AiWorldSeed42Turn300_MovedForTheV36LayoutAlone` |
| `b28fb83727f5` | `09540a725b5b` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne` |
| `b4972200b36d` | `fcc701482518` | 1 | `Adr054AttributionTests.AiWorldSeed42Turn300_EachSwitchMovesItAlone` |
| `b871cefee40e` | `61cf1609893d` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone` |
| `bb825342455b` | `a4350c0c45f1` | 1 | `SnapshotTests.FoundedGolden_Seed42Turn300_MatchesPinnedConstant` |
| `d5ebd0b2c642` | `86fbbc1671d7` | 1 | `MilitaryAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheMilitaryLayerAlone` |
| `e9e089bbce9f` | `7dd83e8386cc` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheTimeAveragedHarvestAlone` |
| `eb88663ac944` | `c5b80a0e79f4` | 1 | `DiscoveryLayoutAttributionTests.DrivenGoldenSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte` |
| `ed3794e7f8db` | `4c625c5bbcad` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheTransportLayoutAlone` |
| `f14c03f81db5` | `d1040ffaf6c7` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheR2bLayersAlone` |
| `f320440fba3d` | `4f4a27a9d851` | 1 | `DrivenGoldenTests.DrivenGolden_Seed42Turn300_MatchesPinnedConstant` |
| `f9d2d8b32f9a` | `f3baa1e88aca` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheV32LevyLayerAlone` |
| `fa0493ece005` | `94811a7ae75f` | 1 | `IntegratedPinAttributionTests.FoundedGoldenSeed42Turn300_MovedForTheResearchLayerAlone` |
| `fe22abb41589` | `9f8e418f9cbb` | 1 | `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_MovedForTheTransportLayoutAlone` |

**CLI legs, ON against OFF:**

| Leg | OFF final (= today) | ON final | First differing turn | Turns equal (of total) | Equal/different runs of turns |
|---|---|---|---|---|---|
| canonical 42 × 400 | `ba223ffe…` | `ba223ffe…` | none | 400/400 | the toy pipeline calls no transcendental |
| founded 42 × 300 | `d5ebd0b2…` | `86fbbc1671d792d5adae881f70c4b5c304b9a8b441e589dfce76defbc9f8e82e` | **9** | 11/300 | =1–8 ×9 =10–11 ×12–13 =14 ×15–300 |
| founded 16 × 300 | `5bb5d61e…` | `a32badc50d28aee8aaeeb0a07e4d07d822f6132c30b1a9f63eddceb13f9ceb24` | **3** | 95/300 | 32 alternating runs; last equal turn 257, different on every turn from 258 |
| founded 1 × 300 | `542ffa8a…` | `0b31b357d61ecf5ee2dc398a3af304abe796c0d2d3a2ca5b448b25ea84f8937f` | **19** | 18/300 | =1–18 ×19–300 |
| founded 2 × 300 | `9acee8f0…` | `06c9d9d2def987857a6a3575bcb16308859eea142307832265ae3e6cb9f9da74` | **7** | 176/300 | 34 alternating runs; last equal turn 198, different on every turn from 199 |
| founded 7 × 300 | `d8eac10b…` | `75a897bad6184f35094a7628dc6d1b33a420d650f1614f49f78fc2f52f039b00` | **17** | 17/300 | =1–16 ×17–18 =19 ×20–300 |
| founded 42 + 1 AI × 600 | `ce267650…` | `90d429da17b01a444c25024071f7bb95014457e7420afef2a8aed76b26398d2b` | **6** | 9/600 | =1–5 ×6 =7–8 ×9 =10–11 ×12–600 |

Weather deviations are recomputed every turn, so an early 1-ulp weather difference can vanish the next turn; the hash
stays different for good only once a difference is banked in a remainder.

## 4. The divergence, field by field (MEASURED)

Saves were written by both builds at the turns below (`--save-at`), and a scratch tool (outside the repository) walked
**every field** of the two canonical streams with `Sim.Cli`'s `SnapshotDiff.Layout`, checking that the walk consumed
exactly the stream length. `sim diff` gave the first divergent field and the per-table rows.

| Leg, turn | Integer/bool fields differing | Double fields differing | First divergent field (`sim diff`) |
|---|---|---|---|
| f42, 9 | 0 of 10,453 | 2 of 9,543 | `HarvestWeather[6].LogDeviation` +1 ulp (and `[8]` −3 ulp) |
| f42, 12 | 0 of 10,613 | 1 | `HarvestWeather[9].LogDeviation` −2 ulp |
| f42, 15 | 0 of 10,719 | 4 | `Buckets[50,53,246,309].MigrationRemainder` −1 ulp |
| f42, 50 / 100 / 150 / 200 / 250 | 0 each | 3 / 5 / 4 / 6 / 5 | migration and produce remainders |
| f42, 300 | 0 of 13,177 | 5 of 10,282 | `Buckets[53].MigrationRemainder` 0.5826656323405414 → …411 (−2 ulp); also `[246]` −2 ulp, `GoodStocks[161,162,185].ProduceRemainder` |
| f16, 3 | 0 of 10,069 | 3 | `PriceTerms[266].Consumption` −3 ulp (`Production` −3, `Clamp` −12) |
| f16, 300 | 0 of 13,859 | 5 | `GoodStocks[138].ProduceRemainder` 0.5319722506392282 → 0.5319722506319522 |
| f1, 19 | 0 of 10,734 | 1 | `HarvestWeather[4].Multiplier` +1 ulp |
| f1, 300 | 0 of 12,976 | 11 | `Buckets[101].MigrationRemainder` −1 ulp; 8 `GoodStocks` remainders |
| f2, 7 | 0 of 10,548 | 1 | `HarvestWeather[3].LogDeviation` −4 ulp |
| f2, 300 | 0 of 13,044 | 5 | `GoodStocks` produce remainders |
| f7, 17 | 0 of 10,938 | 1 | `HarvestWeather[11].LogDeviation` −2 ulp |
| f7, 300 | 0 of 13,150 | 8 | `GoodStocks` produce remainders |
| ai42, 6 | 0 of 10,378 | 4 | `PriceTerms[254].Consumption` −2 ulp (`Production`, `StockRelease` −2, `Clamp` −32) |
| ai42, 600 | 0 of 26,399 | 21 of 19,633 | `Buckets[53].MigrationRemainder` +256 ulp; also `GoodStocks`, `Housing`, `TaxGrievance` doubles |

Two first-divergence signatures appear: the weather path (`HarvestWeather`, Box–Muller `Log`/`Cos` and `Exp`) in
four legs, and the price path (`PriceTerms`, `PriceSystem`'s `Exp`) in two. The second is the same table and the same
size (2–3 ulp) as the Windows/Linux divergence CR-013 §8.5 recorded at turn 2 (`PriceTerms[43].Consumption`, −2 ulp;
DOC-ONLY). **Integer agreement here is a sample, not a standard** (DIR5:354): it was checked at 19 states and nowhere
else, and the remainders that differ decide later integer floors, so a long enough horizon is expected to move an
integer column (INFERRED; not measured past 600 turns).

## 5. Performance cost (MEASURED; loaded shared host)

`sim bench --founded --seed 42 --turns 300 --json` (the CLI's per-phase timer; world construction excluded), the OFF
and ON builds alternated four times (OFF, ON, OFF, ON, …), each under `nice -n 19` on the shared 4-CPU host (1-minute
load average 1.75–4.10 at the start of the runs):

| Build | Run 1 | Run 2 | Run 3 | Run 4 | Median |
|---|---|---|---|---|---|
| OFF (libm) | 19,123 ms | 16,195 | 16,357 | 16,608 | **16,483 ms** |
| ON (DetMath) | 20,083 ms | 18,666 | 19,265 | 19,626 | **19,446 ms** |

**ON/OFF = 1.180 (+18.0 %)** on the median of 300 turns. The cost sits almost entirely in one system:

| Phase (median of 4) | OFF ms | ON ms | Δ ms |
|---|---|---|---|
| demographics (the 6 `Exp` calls of `DemographicsSystem`) | 2,752 | 5,800 | **+3,049** |
| harvestweather (`Exp`, `Log`, `Cos`) | 32 | 69 | +37 |
| price (`Exp`) | 71 | 93 | +22 |
| migration (`Exp`) | 190 | 203 | +13 |
| all 23 other phases | — | — | within ±0.2 s, no consistent sign (e.g. catchment −80, pathbuild −180) |

O-17 measured +8 % on a whole 300-turn CLI run including start-up (DOC-ONLY, loaded host); this bench excludes start-up
and isolates the step, hence the larger ratio. Both are loaded-host numbers. **A quiet-host `sim bench` is still owed**
(landing step 5). If +18 % is judged too much, the cost is concentrated enough to attack without touching
determinism (proposals, not done): a table-driven DetMath `Exp` (tables built at type initialisation from basic
operations, as `Log` already does), or reusing `exp(−rate·h)` across cohorts that share a rate within one sub-step.

## 6. DetMath correct rounding on the vector sets (MEASURED)

The check ran the `Sim.Core` copy of DetMath (from the ON build) through a scratch console tool outside the repository
against three files of `(fn, x, y, correctly rounded, glibc)` bit patterns. The oracle is the O-17 generator
(`prototypes/m7-sprint1/Proto.DetMath.Tests/vectors/gen-cr-vectors.py`: Python `decimal` at 70–130 digits); the two
held-out files are the same generator with only its SplitMix64 seed changed (17 → 20261106 and 20261107), so their
inputs were never seen while DetMath was written.

| Vector set | exp | log | pow | cos | DetMath mismatches | glibc not correctly rounded |
|---|---|---|---|---|---|---|
| stored (`cr-vectors.txt`, seed 17, N = 1000) | 2,013 | 2,508 | 2,951 | 1,708 | **0 / 9,180** | 1 / 0 / 5 / 1 |
| held-out A (seed 20261106, N = 2000) | 4,013 | 5,008 | 5,701 | 3,208 | **0 / 17,930** | 3 / 0 / 4 / 3 |
| held-out B (seed 20261107, N = 20000) | 40,013 | 50,008 | 55,201 | 30,208 | **0 / 175,430** | 16 / 2 / 43 / 45 |
| *round 1:* Box–Muller u1 fixture (`Sim.Tests/Fixtures/detmath/cr-vectors-boxmuller-log.txt`, seed 20261109; 4,000 draws as drawn + 200 recorded glibc-misrounded) | — | 4,200 | — | — | **0 / 4,200** | – / 203 / – / – (3 of the 4,000 as drawn) |
| *round 1:* held-out C (uniform u in [0,1) floored at 1e-300, seed 20261110; not stored) | — | 200,000 | — | — | **0 / 200,000** | – / 164 / – / – |

*(Corrected in round 1, X-R1-4.)* The "log 0 / 0 / 2" of the first three rows was a sampling artefact: their log inputs
are log-uniform or bit patterns, on which glibc and DetMath disagree about 5 times per million. On the campaign's own
log input, the Box–Muller u1 (`HarvestWeatherSystem.NextStandardNormal`), glibc misrounds about 0.08 % of inputs, and on
held-out C every one of the 164 inputs where glibc and DetMath disagree is a glibc misrounding (DetMath correctly
rounded on all 164; Python `decimal` ln at 80 digits).

The same 13-case `SimMathNumericsTests` class ran inside both full suites (§2, §3), so the stored-vector check also ran
under the test runner in both builds.

Limits, unchanged from O-17 and not closed here: DetMath's working precision is about 2^-100, while published hard
cases need 2^-113 to 2^-118, so a rare input could round the wrong way (deterministically). The vectors are random
inputs over the campaign ranges, not hard cases. The author mutant M7 (`Ln2Lo` dropped) changes about 2^-23 of exp
results and is not expected to be caught at these densities (not re-run). Determinism, which is what equality needs,
does not depend on correct rounding.

## 7. Platform-variant operations

### 7.1 CPU-feature dispatch: libm varies on one machine, DetMath does not (MEASURED)

glibc 2.39 selects FMA/AVX2 variants of `exp`, `log`, `pow` and `cos` at load time (ifunc). The tunable
`GLIBC_TUNABLES=glibc.cpu.hwcaps=-AVX2,-FMA,-AVX512F,-AVX512DQ,-AVX512VL,-AVX` turns that selection down on the same
machine.

**Function level.** A scratch Python script (Python's `math` module calls the same libm; inputs built from bit patterns
and basic operations only, so they cannot depend on the libm under test, and checked identical between the two runs)
evaluated each function on 10^6 campaign-range inputs under both dispatches:

| Function (range) | Results that differ | Max distance |
|---|---|---|
| exp (x ∈ [−50, 5]) | 651 / 10^6 | 1 ulp |
| log (u ∈ (0, 1)) | 107 / 10^6 | 1 ulp |
| pow (s ∈ [2^-40, 2^27), y ∈ [−10, 10]) | 662 / 10^6 | 1 ulp |
| cos (2πu) | 715 / 10^6 | 1 ulp |

**Simulation level** (founded seed 42, 300 turns, per-turn hash logs, compared with the same build under default dispatch):

| Build | Environment | Result |
|---|---|---|
| libm (OFF) | glibc dispatch down | **differs from turn 13**, final `3bc72eb1…f9bc35` ≠ `FOUNDED_GOLDEN`; reproduced by a second run (logs identical) |
| libm (OFF) | `DOTNET_EnableAVX=0` (the JIT emits SSE-only code: a 5-turn disassembly with this setting showed 0 `vmulsd` and 840 `mulsd`) | identical, 300/300 |
| libm (OFF) | `DOTNET_TieredCompilation=0` | identical, 300/300 |
| DetMath (ON) | glibc dispatch down | identical, 300/300 |
| DetMath (ON) | `DOTNET_EnableAVX=0` | identical, 300/300 |
| DetMath (ON) | `DOTNET_TieredCompilation=0` | identical, 300/300 |
| DetMath (ON) | `DOTNET_TieredPGO=0`, `DOTNET_ReadyToRun=0` | identical, 300/300 |
| DetMath (ON) | all of the above at once | identical, 300/300 |

Consequences:
- `FOUNDED_GOLDEN` reproduced on this host only with glibc's default dispatch (its AVX2/FMA variants); with the
  dispatch turned down it did not. INFERRED: a Linux machine without AVX2/FMA (or a VM that masks them) would fail
  today's determinism leg with no code change; no such machine was available, and the CI runner's dispatch path was
  not inspected.
- The JIT's own instruction-set choice does not change any hash, in either build: the non-transcendental arithmetic is
  already ISA-independent here.
- CR-013 §3.2 (b)'s inference that "a divergence between machines on the same OS is possible" is now measured for
  Linux. The same mechanism (CPU-dependent paths in the UCRT) is INFERRED, not measured, for Windows.

### 7.2 FMA contraction (MEASURED on one host)

`DOTNET_JitDisasm='*'` over a 60-turn founded run listed every method the JIT compiled. Counting `vfmadd`, `vfmsub`,
`vfnmadd`, `vfnmsub` and `vfmaddsub`:

| Build | Tiering | Methods listed (of which `Sim.*`) | FMA instructions |
|---|---|---|---|
| OFF | default | 8,175 (5,884) | 0 |
| OFF | `TieredCompilation=0` | 2,141 (1,364) | 0 |
| ON | default | 8,257 (5,952) | 0 |
| ON | `TieredCompilation=0` | 2,156 (1,383) | 0 |

The host supports FMA and AVX-512, and the code is VEX-encoded (1,531–1,768 `vmulsd`), so the JIT had FMA available
and did not contract `a*b+c`. That matches RyuJIT's documented behaviour (it emits FMA only for explicit
`FusedMultiplyAdd`/vector intrinsics; INFERRED from documentation, confirmed here for this runtime and these methods
only). ReadyToRun code in CoreLib that was never re-jitted is not covered by the listing.

### 7.3 Grep audit of the remaining platform-variant candidates (RE-VERIFIED in `dfdbc2d`; `Sim.Core`, `Sim.Data`, `Sim.Cli`; comment lines excluded)

| Construct | Code lines | Platform-variant? | Basis |
|---|---|---|---|
| `Math.Exp/Log/Pow/Cos` | 47 lines, 48 calls, 15 files (all in `Sim.Core`) | **Yes — the measured cause** | §7.1 (glibc dispatch), CR-013 §8.5 (UCRT vs glibc). Routed through `SimMath` by the experiment |
| Any other transcendental: `Sin`, `Tan`, `Atan`, `Atan2`, `Tanh`, `Sinh`, `Cosh`, `Asin`, `Acos`, `Cbrt`, `Hypot`, `Log2`, `Log10`, `double.*` spellings | **0** | — | grep |
| `Math.FusedMultiplyAdd`, `ScaleB`, `ILogB`, `IEEERemainder`, `BitIncrement/Decrement`, `CopySign` | **0** | — | grep |
| FMA contraction of `a*b+c` by the JIT | — | **No (measured on one host)** | §7.2: 0 FMA instructions in every method the JIT emitted, tiered and fully optimised, on an FMA-capable CPU |
| `Math.Round` | 11 (`Math.Round(x)` 9; `Math.Round(x, MidpointRounding.AwayFromZero)` 2; no `Round(x, digits)`) | No | Rounding to an integer is exact in IEEE-754; `roundsd` (seen in the disassembly) and the managed fallback give the same result. The `digits` overload, which multiplies by a power of ten, is not used |
| `Math.Sqrt` | 14 | No | IEEE-754 requires correct rounding; `sqrtsd`/`vsqrtsd` in the disassembly. DetMath also uses it |
| `Math.Floor/Ceiling/Truncate` | 38 | No | Exact operations |
| `Math.Abs/Min/Max/Clamp/Sign` | 226 | No | Exact; .NET Core 3.0+ gives IEEE 754-2019 min/max semantics on every platform |
| `(long)`/`(int)` casts of `Math.*` results | 24 | No for Windows/Linux x64 | Same JIT and the same x64 conversion on both; .NET 9+ saturates out-of-range values on x64 and Arm64 alike (INFERRED, known runtime behaviour). Arm64 is out of scope |
| `double.Parse/TryParse` | 3, all `CultureInfo.InvariantCulture` (`ResearchContent.cs:1134`, `Predicate.cs:312`, `:419`) | No | .NET Core 3.0+ parsing is IEEE-correct and managed; content JSON goes through `System.Text.Json` (same) |
| Numeric `ToString("R"/"G17"/"N0"/"F…")` | 21, all `InvariantCulture` | No hashed state | Error messages and CLI output only |
| `float`, `MathF`, `decimal`, `Half` | 0 | — | grep (D-004 gate covers `float`) |
| `System.Runtime.Intrinsics`, `Vector<T>`/`Vector128`…, `System.Numerics`, `TensorPrimitives` | 0 | — | grep. CPU-width-dependent vector reductions are a real variance source; proposal in §9 step 2 to ban them in simulation code |
| LINQ `Sum/Average/Aggregate/Min/Max` over doubles | 0 | — | grep (the 2 hits are the `NeedsAggregation.Aggregate` method) |
| `Parallel.*`, `AsParallel`, `Task.Run`, `ThreadPool` | 0 | — | grep |
| `Dictionary<`/`HashSet<` | 18 lines | No (Windows/Linux) | Enumeration order follows the insertion/removal history, not hash values, so it is the same on both OSes; iteration in logic stays banned by Law 5 (review-enforced) |
| `GetHashCode` | 14, all marked `gate:allow-gethashcode` | No | Equality plumbing only (scan-enforced) |
| Culture | — | No | `InvariantGlobalization=true` in `Directory.Build.props`: no ICU/NLS difference can reach comparisons or formatting |
| Content JSON line endings | 15 embedded files, LF in the index, no `.gitattributes` pin | **Yes for the forensic identity; no for the world hash** *(corrected in round 1, X-R1-3)* | `ForensicIdentity.ConfigResources` (`ForensicIdentity.cs:75-100`) SHA-256s the raw bytes of every embedded `Sim.Data.*` resource; that feeds `ConfigDigest` (`:106-113`) and the forensic run id (`ForensicSession.cs:60-64`). Measured: content converted to CRLF gives founded 42 × 300 a different `configDigest` (`c3be9123…` → `75a2263c…`) and run id (`e42e443b845d2ca8` → `1259e2109b8cc53d`) with all 300 world hashes identical (JSON whitespace is insignificant to the parser). A Windows checkout with `core.autocrlf=true` would do this (INFERRED). Remedy: landing step 4a. (The round-0 text said "no code hashes the raw bytes"; that was wrong.) |
| `RuntimeInformation.RuntimeIdentifier` | 2 (`Sim.Cli/Program.cs:494`, `:699`) | Provenance only | Manifest `platform` field and the platform notice; not in the canonical stream |
| `Stopwatch` | 7 | No | Timing in `TurnExecutor` observer and the CLI bench only |
| `SHA256` / `IncrementalHash` | 6 | No | Integer algorithm |
| `BinaryWriter` / `BitConverter` byte order | — | No (x64) | Little-endian on both OSes; `SaveFiles` refuses a big-endian host explicitly |
| `Sim.Ui` transcendentals | 51 calls, 17 files | Not in the hash | Presentation (camera, lens, SVG). `Sim.Ui` is outside the determinism surface by project reference (`scripts/check-banned-constructs.sh:24-28`); orders carry resolved integer targets (INFERRED from the order types; not audited line by line) |
| Battle prototypes (`Proto.M7`) | 0 transcendentals | — | Integer-only (INV-1, DIR5:165) |

**Residual risks not removable by DetMath (INFERRED, not measured):**
- **Runtime version drift.** CI installs `dotnet-version: '10.0.x'` (floating patch) and there is no `global.json`; the
  Director's build may run another 10.0 patch. RyuJIT does not change the value of IEEE basic operations, and DetMath
  uses nothing else, so a patch difference should not move a hash. A pinned SDK (`global.json`) would remove the
  question; proposal only.
- **Arm64** (not a DIR5 platform) would add conversion and codegen differences; out of scope.
- **CoreLib internals.** The grep audit found no simulation-code call into a vectorised floating-point CoreLib API (no
  LINQ reductions over doubles, no `TensorPrimitives`, no vector types). Calls not visible to a grep (for example a
  CoreLib method that vectorises internally over doubles) were not audited.

## 8. Mutants and scan teeth (MEASURED)

**Facade/switch mutants**, worktree `/dev/shm/m7c/X-mut` (detached at the experiment commit), each built and then run
against `SimMathNumericsTests` (13 cases; clean run 2.7 s; bound 30 s = 10×; no run hung):

| # | Mutation | Build | Result | Killed by (semantic property) |
|---|---|---|---|---|
| S-M1 | OFF branch of `SimMath.Exp` calls `DetMath.Exp` (the switch leaks) | OFF | killed, 1 of 13 failed | `TheFacade_IsExactlyTheSwitchedFunction_OnTheCampaignRanges("exp")`: OFF must be System.Math bit for bit |
| S-M2 | ON branch of `SimMath.Pow` calls `Math.Pow` (a hole in the switch) | ON | killed, 1 failed | the same test, `"pow"` |
| S-M3 | ON build labels itself `libm` | ON | killed, 3 failed (exp, pow, cos) | the same test: the label must name the numerics actually used. `"log"` did not fail: DetMath and glibc agree on all 20,000 log inputs of that range. *(Round 1, X-R1-1: that was a gap in the inputs, not a property of log; a `Log` hole in the switch survived every test. Closed in `fb3698b`, §14)* |
| S-M4 | one call site in `HarvestWeatherSystem.cs` reverted to raw `Math.Exp` | OFF | killed, 1 failed | `NoRawTranscendental_InSimulationCode_OutsideTheFacade`: no libm path outside the chokepoint |
| **S-M5** | `Sim.Core.csproj` condition broken (`'$(SimNumerics)' == 'detmath-typo'`): `-p:SimNumerics=detmath` silently builds libm | ON requested | **SURVIVED**, 13 of 13 passed | none: every test reads the build's own `NumericsId`, so a build that is wrong about which numerics it was asked for is self-consistent |

**Fix for S-M5: commit `f8e0acc` on the experiment branch** (`git diff --stat e738f08 f8e0acc`: `Sim.Tests/Kernel/SimMathNumericsTests.cs` +21, `Sim.Tests/Sim.Tests.csproj` +10; no `Sim.Core` change). `Sim.Tests.csproj` records the raw build property as `[assembly: AssemblyMetadata("SimNumerics", "$(SimNumerics)")]`; the new test `TheBuildsNumerics_IsTheOneTheBuildPropertyAskedFor` maps "" or `libm` to `libm` and `detmath` to `detmath-1`, refuses any other value, and asserts that `SimMath.NumericsId` matches. Measured on `f8e0acc` (targeted `SimMathNumericsTests`, now 14 cases, each run under the 30 s bound):

| Build of `f8e0acc` | Result |
|---|---|
| OFF | 14 of 14 passed ("requested '', expected libm, built libm") |
| ON (`-p:SimNumerics=detmath`) | 14 of 14 passed ("requested 'detmath', expected detmath-1, built detmath-1") |
| S-M5 re-applied, ON requested | **killed**: `TheBuildsNumerics_IsTheOneTheBuildPropertyAskedFor` failed ("built libm"); 13 others passed |
| `-p:SimNumerics=detmth` (a typo, no mutation) | **refused**: the same test failed |

The full suites of §2 and §3 ran on `e738f08`. `f8e0acc` changes no `Sim.Core` source; the md5 of `Sim.Core.dll` still differs between the two builds because the SDK embeds the commit id in the assembly's informational version (`1.0.0+e738f08e…`). The first version of this test did not compile (xUnit analyzer xUnit2000 rejects a constant in the `actual` position); it was amended before any measurement was taken, and the numbers above are from the amended commit.

**Scan teeth.** In the same worktree, each of six spellings was inserted into `PriceSystem.cs` at line 152 and
`scripts/check-banned-constructs.sh` run; each run failed with exactly one hit under the rule "raw transcendental
outside SimMath (CR-013)", and the clean tree passed:
`Math.Exp(1.0)`, `double.Exp(1.0)`, `System.Math.Pow (2.0, 0.5)`, `Math.Sin(1.0)`,
`Math.FusedMultiplyAdd(1.0, 2.0, 3.0)`, `Double.Log10(2.0)`. The xUnit twin
*(Round 1, X-R1-2: all six were direct-call spellings. `using static System.Math;` with a bare `Exp(…)`, a continuation
line starting with `*`, a method group and an alias import each passed both the bash rule and its twin, and a line
starting with a block comment passed the twin. All are now caught, and a compiled-IL check covers what no grep can see,
§14.)* `NoRawTranscendental_InSimulationCode_OutsideTheFacade` passed on the clean tree and failed on the `double.Exp`
injection, naming `PriceSystem.cs:152`. This closes, for the experiment's own scans, the O-17 verifier finding V1
("`double.Exp` and friends are not banned"); V1 against the prototype's scan is not re-adjudicated here.

## 9. Landing plan (PROPOSAL; executable only after the Director rules on D-079 Q5)

Each step names its artefact, its proof, and the governance item it needs. Nothing below has been done on any shared
branch. Steps 1 to 4 move no golden. Step 5 is the only re-pin.

| Step | What lands | Proof required in the same commit | Needs |
|---|---|---|---|
| 0 | Verdict round on DetMath (CR-013 X11): verifier findings V1 to V5, author mutants M5 and M7, and this lane's mutant record (§8; S-M5 survived and is now killed by `f8e0acc`, which itself needs an independent verdict). Add a hard-case vector file (Lefèvre–Muller worst cases for exp and log; pow cases near midpoints) next to the oracle vectors | Recorded verdicts; survivors killed or adjudicated equivalent | ADR-015 §6 |
| 1 | **The switch, default OFF.** `Sim.Core/Kernel/SimMath.cs` (the one chokepoint), `DetMath.cs`, `DoubleDouble.cs`; the 48 calls in 15 files routed through `SimMath`; the two bit-exact test replicas (`DemographicsReplica`, `HarvestWeatherReference`) routed too; `SimMathNumericsTests`. Build property `SimNumerics` (unset or `libm`: System.Math; `detmath`: DetMath), **with the independent expectation channel of `f8e0acc`** (the test assembly records the raw property and `TheBuildsNumerics_IsTheOneTheBuildPropertyAskedFor` checks what `Sim.Core` compiled; kills S-M5 and refuses mistyped values) **and the round-1 checks of `fb3698b`**: the build-time `CheckSimNumerics` guard (X-R1-5); the compiled-IL check that each `SimMath` function calls the function its build names (X-R1-1); facade inputs on the campaign's Box–Muller distribution; `SimMath.Cos`'s domain shared by both builds (X-R1-7); the oracle vectors and their generators **copied** into `Sim.Tests/Fixtures/detmath` and the kernel DetMath pinned by a recorded digest, so no permanent test reads `prototypes/` (X-R1-6); **and the round-2 checks of `823a315`** (§15): the real-Build pin of the guard's hook (X-R2-2), the IL switch check over `SimMath`'s own call graph and the narrowed chokepoint exemption (X-R2-3), the System.Numerics / System.Runtime.Intrinsics refusal (X-R2-1). This is the content of `X-kernel-experiment.patch` | Full `Sim.Tests` Release green with **zero pin edits**; the 7 CLI legs of §2 byte-identical, turn for turn, to the parent commit | Q5; a kernel ADR (proposed: "ADR-063 deterministic transcendentals") |
| 2 | **Scan ban** (same commit): the `check-banned-constructs.sh` rule "raw transcendental outside SimMath (CR-013)" over `Sim.Core`, `Sim.Data`, `Sim.Cli` (System.Math, `double.*` and `Double.*` spellings of every transcendental, `FusedMultiplyAdd`, the reciprocal estimates; since round 1 with or without a call parenthesis, only `//`-led lines exempt, and `using static`/alias imports of `System.Math`/`System.Double` banned by a second rule), plus the xUnit twin `NoRawTranscendental_InSimulationCode_OutsideTheFacade` and, since round 1, the spelling-independent `NoTranscendentalCall_InCompiledSimulationCode_OutsideTheFacade`, which reads the compiled IL of `Sim.Core`, `Sim.Data` and `Sim.Cli` (X-R1-2). Proposed addition: ban `System.Runtime.Intrinsics`, `System.Numerics.Vector*` and `TensorPrimitives` in simulation code (0 uses today; prevents a CPU-width-dependent reduction from entering). *(Corrected in round 2, X-R2-1 and X-R2-4. (i) The IL check is independent of the source spelling for DIRECT calls only: it does not follow calls into the BCL, and the proposed addition missed `System.Numerics.Complex`, whose `FromPolarCoordinates` calls libm cos inside the BCL. Since `823a315` the whole of System.Numerics\* and System.Runtime.Intrinsics\* is refused in all three layers (bash rule, twin, IL check); `Vector*` and `TensorPrimitives` are inside it. (ii) The bash rule and the twin were line-based, so a member access split across lines passed both; since `823a315` the twin matches whole files and the script has a perl pass for matches that span a line break. (iii) The gate: the IL check runs in the `Sim.Tests` step of CI and is the gate for direct calls; the bash rule is the fast pre-check, and its teeth are now pinned by tests that run the script itself on 17 injected spellings plus a clean control, and pin its name list equal to the twin's and the IL check's. (iv) Not covered by any layer: a BCL method outside those namespaces that calls libm internally under a non-transcendental name, §15.4.)* | The teeth record of §8 and §14.2 (12 injected spellings; since round 1 each caught by the bash rule, the twin and the IL check); since round 2 the automated script teeth and the mutant record of §15.2 | with step 1 |
| 3 | **Numerics version.** `SimMath.NumericsId` (`libm` now, `detmath-1` after step 5) becomes an additive session-manifest key `numerics`, written only when it is not `libm`, so every existing manifest stays byte-identical and an absent key reads as `libm` (the ADR-033 D5 additive-key pattern, `Sim.Core/Kernel/SessionManifest.cs:78-83`, `:191-193`, `:258`). `sim inspect`, `sim replay` and `UiSession.ReplayTo` refuse a hash-equality verdict across a numerics mismatch with a plain message ("recorded under libm numerics; this build computes detmath-1"), never DIVERGED (CR-013 X7; the D-056 readable-set logic). Rule: any DetMath change that alters one output bit is a new id (`detmath-2`) with its own attributed re-pin. `CanonicalSchema.Version` (39) does not move: the stream layout is unchanged. Alternative for the Director: bump it to 40 anyway to force a hard break. *(Added in round 2, X-R2-6: the manifest key alone leaves the FORENSIC RUN ID blind to the numerics. `ForensicIdentity.RunIdInputs` hashes seed, size, settlements, founded, `CanonicalSchema.Version`, `configDigest` and `ordersDigest` and nothing else, and `Sim.Cli/SessionEmitter.cs` names the session files by the run id. Measured on `fb3698b`: the libm and the detmath CLI, `run --founded --seed 42 --turns 12 --emit-session`, both emit run `e42e443b845d2ca8` with byte-identical `session-e42e443b845d2ca8.json` files, while their world hashes differ from turn 9 and their forensic and trace files differ. Options, for the Director: (a) add the numerics id to the run-id preimage only when it is not `libm` (every recorded libm id stays valid; a change to the forensic identity recipe, with ADR); (b) the 39 → 40 bump above separates the ids across the version boundary only: a libm and a detmath build of the same commit carry the same constant, so during steps 1 to 4, when both builds exist, their run ids still collide; (c) the manifest key alone, accepting colliding run ids (D-057's refusal of an existing record then refuses the second run into one directory). Proposal: (a) together with the manifest key.)* | Manifest round-trip tests (populated, exact length, hash equality); a replay-refusal test | Q5 (contract touch: manifest, ADR-022 surfaces of CR-013-reopened §6) |
| 4a | **Content line endings** (added in round 1, X-R1-3; CR-013 X6): pin `Sim.Data/content/*.json text eol=lf` in `.gitattributes`, so every checkout embeds the same bytes and the forensic `configDigest`/run id agree across operating systems. Alternative for the Director: hash line-ending-normalised bytes in `ForensicIdentity.ConfigResources` (a change to the forensic identity recipe, so with an ADR, and every recorded `configDigest` moves) | The step-4 Windows leg prints the same `configDigest` and run id as the Linux leg for the same commit | with step 4 |
| 4 | **CI legs** (DELEGATED, D-079 D12; permanent code, so not Q15): a matrix job on `ubuntu-latest` and `windows-latest`, built with `-p:SimNumerics=detmath`, running the 7 legs of §2 plus the existing replay legs and `SimMathNumericsTests`, uploading the per-turn hash logs and comparing them **across operating systems** (self-checking: no golden is needed for equality). Triggers: push to the development branch and `workflow_dispatch`. Non-blocking until its first green run | Two green runs from one commit; the logs attached | Q5 (the code must be in the tree) |
| 5 | **Flip and one attributed re-pin.** One commit: default `SimNumerics` becomes `detmath`; every moved literal (the list of §3, plus the CI shas: `ci.yml` `FOUNDED_GOLDEN` → `86fbbc16…`, the AI-leg and allocation-leg values, the session fixtures) is re-pinned in that commit and nowhere else; one class `NumericsAttributionTests` holds (libm pin, detmath-1 pin) pairs for the reference goldens (founded 42 and 16, driven, AI, first-reign, toy) and asserts the pin of the build's `NumericsId`; a libm CI leg runs only that class, which proves the flip moved nothing but the numerics. One row in the golden attribution register ("CR-013 numerics libm → detmath-1"). Re-measure the corridors and the calibration battery; record `sim bench` | Both legs green; register row; bench delta | Q5; step 4 green on Windows first (proposal) |
| 6 | **Windows leg blocking.** `xplat-surveillance.yml`: remove `continue-on-error: true` (`:124`, `:232`, `:319`) and the unconditional `exit 0` (`:213`); add the development-branch trigger; compare full per-turn logs. Make the step-4 matrix a required check on the development branch and on `main`. Update the ADR-022 surfaces listed in CR-013-reopened §6 (`SessionManifest.ReferencePlatform`, `PlatformNotice`, the workflow header) | A required, green Windows check | CR-013 §5 closure criteria |
| 7 | **Seeds.** Founded 42, 16, 1, 2, 7 (300 turns), canonical 42 (400), founded 42 with one AI empire (600), plus replays of the recorded session fixtures; after the first green, add three more founded seeds (number DELEGATED) | — | — |
| 8 | **Retire libm.** After the Windows leg has been green for an agreed number of runs, delete the `libm` branch of `SimMath` and the libm CI leg, keeping `NumericsAttributionTests` as history | — | — |

## 10. Windows evidence still needed, and how to obtain it

**No Windows run was made by this lane.** Nothing in this report is evidence of Windows/Linux equality. What the
measurements do establish is narrower: on one Linux host, DetMath's results do not depend on the CPU-feature dispatch
of the C runtime or of the JIT (§7.1), and DetMath uses only operations that IEEE-754 defines exactly. That makes
Windows equality the expected outcome; it does not make it a measured one.

| # | Evidence | Why it is needed | Route if Q5 = (a) | Route if Q5 = (b), Q15 = (a) | Route if Q5 = (b), Q15 = (b) |
|---|---|---|---|---|---|
| W1 | DetMath per-function bit equality on Windows: the 9,180 stored oracle vectors and the two held-out sets of §6 (or regenerated by the generator with the stated seeds), plus the 20,000-input facade sets | Proves the four functions return the same bits on the UCRT host as on glibc | `SimMathNumericsTests` in the step-4 matrix on `windows-latest` | A prototype workflow (not in `Sim.slnx`, not a required check) running `Proto.DetMath.Tests` from `M7DetMath.slnx` on both OSes | Not obtainable in CI; only the private-file route of W5 |
| W2 | The UCRT's correct-rounding rate on the same vectors | Characterises the existing divergence; not needed for the fix | Printed by `DetMath_ReproducesTheCorrectlyRoundedVectors_BitForBit` (the host column) | Printed by the prototype's `HostLibm_AgainstCorrectlyRounded_IsRecorded` | — |
| W3 | **The closure evidence (CR-013 §5):** the 7 legs of §2 built with `detmath` on `windows-latest` and `ubuntu-latest`, full per-turn hash logs equal; the driven, first-reign and toy goldens; replay of the recorded session fixtures | The Director's requirement is equality of the whole canonical stream on every turn | Step-4 matrix | The same prototype workflow applies `X-kernel-experiment.patch` to the runner's checkout (ephemeral; nothing is committed to `Sim.Core`), builds with `-p:SimNumerics=detmath`, runs the legs on both OSes and compares the logs. The patch file would have to live under `prototypes/` | Not obtainable in CI |
| W4 | Today's (libm) Windows/Linux divergence, per seed, over the full horizon, including the first turn on which an integer column differs | Records what the fix removes; CR:352-356 notes the integer horizon was never measured past turn 5 | `xplat-surveillance.yml` by `workflow_dispatch` (permanent code; blocked by neither Q5 nor Q15), extended to the 7 legs and to `sim diff` at the turns of §4 | same | same: possible today by dispatch |
| W5 | The Director's own machine (a real consumer CPU and an OS-updated UCRT, unlike a CI image) | A CI runner is one CPU model and one UCRT build | After step 4: run the released build | Private win-x64 route (D-078 route (b)): the portable `Sim.Cli.dll` of both builds runs on Windows under an installed .NET 10 runtime with `dotnet Sim.Cli.dll run --founded --seed 42 --turns 300 --hash-log f42.log`; compare with the Linux logs of §2 (final hashes in §3) | same private route |
| W6 | CPU-dispatch invariance on Windows: `DOTNET_EnableAVX=0` and `DOTNET_TieredCompilation=0` runs of the detmath build | Repeats §7.1 on the UCRT host | Extra matrix cells | Extra workflow cells | — |
| W7 | *(round 1, X-R1-3)* The forensic `configDigest` and run id of the same run on a `windows-latest` checkout and on Linux | Whether the CI checkout converts the content JSON to CRLF (INFERRED, not measured); closes with step 4a | Step-4 matrix (`--emit-session`) | Prototype workflow | `xplat-surveillance.yml` by dispatch with `--emit-session` (possible today) |

The cheapest complete route is Q5 (a) with step 4: W1, W2, W3 and W6 come from one dispatch. Under Q5 (b) the same
evidence needs Q15 (a) and a patch-applying prototype workflow. Under (b)/(b), only W4 (today's divergence) and W5 (a
private file) are possible.

## 11. Recommendation for D-079 Q5 (PROPOSAL; the decision is the Director's)

**Recommendation: (a), staged.** Land steps 0 to 4 now on the development branch (the switch **default OFF**, the scan
ban, the numerics id in the manifest, the cross-OS CI legs), and make step 5 (the flip and the single attributed
re-pin) conditional on the first green Windows/Linux equality run of step 4.

Rationale, each point measured in this report unless labelled:
1. **Switch OFF is provably inert.** The full `Sim.Tests` suite passes in Release with zero pin edits (§2), and 2,500
   per-turn hashes over 7 legs are byte-identical to the unmodified tree. Landing it changes no golden and no behaviour.
   *(Corrected in round 2, X-R2-5: no golden, and no behaviour on any input the simulation produces. Since `fb3698b`
   the OFF `SimMath.Cos` refuses a finite |x| > 2^28, where libm returned a value; the only caller passes 2πu, u in
   [0,1). Measured unchanged: the 7 legs and the 80 moved-pin cases, §14.3.)*
   With the kernel ADR it is a visible, documented change of frozen code, not a silent one (DIR5 §10).
2. **The Windows evidence that closes CR-013 (W3) needs the code in the tree,** or a patch-applying CI job that itself
   needs Q15 (a). Option (b) postpones every campaign-level Windows measurement until after the architecture review.
3. **The libm dependence is wider than Windows/Linux.** On this one Linux host, switching glibc's CPU dispatch off
   (AVX/FMA variants) changes 0.01-0.07 % of exp/log/pow/cos results and moves the founded seed-42 hash from turn 13
   (§7.1). Today's goldens therefore depend on the CPU features of the machine that runs them, even on Linux. DetMath's
   hashes did not move under any of the dispatch changes tried.
4. **The flip is the costly step** (every campaign golden moves once; §3) and is the one that should wait for Windows
   evidence, so that the re-pin is done once and measured on both platforms (CR-013 X10).

Alternative if the Director prefers (b): rule Q15 (a) as well, so that W1 to W3 can be obtained through a prototype
workflow without changing `Sim.Core` (§10). Under (b) with Q15 (b), the experiment stays in the scratchpad bundle and
only W4 and W5 can be obtained.

## 12. Limitations and what was not done

- **No Windows run** (§10). Every equality statement in this report is Linux-against-Linux.
- **One host.** The CPU-dispatch result (§7.1) is one Xeon with AVX-512; a machine without FMA was emulated with
  glibc tunables, not used.
- **Integer columns** were compared at 19 states only (§4), never beyond 600 turns. "0 integer fields differ" is a sample.
- **Goldens outside `Sim.Tests`** were not run under the switch: `Sim.Ui.Tests` session fixtures (including the
  win-x64 `director-68` recording), the `ci.yml` allocation-leg sha256 values, the nightly corridors and the calibration
  battery. INFERRED: every one downstream of a transcendental moves under ON; none moves under OFF (the OFF build calls
  the same functions with the same arguments).
- **The full baseline suite on `dfdbc2d` was not re-run** (only built and enumerated); the OFF suite is the evidence (§2).
- **Every single-host full-suite run was killed** by the container's memory cgroup (the shared `/dev/shm` of all lanes holds about 10 GB of a 13.7–14.3 GB limit): OFF twice, ON once. Coverage was completed with class-filtered reruns in fresh test hosts (§2, §3); every one of the 3,479 test cases has a result in each build, but no single uninterrupted run of the whole suite exists for either build. A test that ran in more than one host is counted once (the later result; where both exist they agree).
- **Timings** come from a 4-CPU host shared with other agents, under `nice -n 19` (§5); they are indicative.
- **DetMath is not proven correctly rounded** (§6); no hard-case vector set exists yet; the O-17 verifier findings
  (V2–V5, M5, M7) have not had their verdict round (CR-013 X11). This lane did not re-run the prototype's mutation set.
- **The libm code path is not removed**, and nothing in `Sim.Ui` or the prototypes was changed.
- **No numerics id is recorded anywhere yet**: §9 step 3 is a design, not code.
- **Round 1 (§14) did not re-run the full `Sim.Tests` suite** on `fb3698b` (only 2 GB of memory were free on the
  shared host, and every earlier single-host run was OOM-killed; in round 1 the memory cgroup also killed one of this
  lane's targeted test hosts). Its evidence is narrower and named in §14.3: the 7 CLI legs in both builds (OFF against a
  fresh `dfdbc2d` build, ON against §3), all 80 cases of the 76 moved-pin methods in the OFF build, and
  `SimMathNumericsTests` in Debug and Release in both builds. The ON run of the moved pins was stopped (§14.3). The line-ending remedy (step 4a) is a proposal; nothing was
  changed for it.
- **Round 2 (§15) did not re-run the full `Sim.Tests` suite either.** Its `Sim.Core` change is comment-only, and the
  evidence is that `Sim.Core.dll` built from `fb3698b` and from `823a315` is byte-identical in both numerics; `Sim.Data`
  and `Sim.Cli` are unchanged (§15.3). The memory cgroup killed processes twice during round 2 (a CLI run and a `csc`),
  each retried once successfully.
- **The IL checks do not follow calls into the BCL** (§15.4). A BCL method outside System.Numerics* and
  System.Runtime.Intrinsics* that calls libm internally under a non-transcendental name would pass every guard. Today
  the non-Sim callees of simulation code fall in 15 BCL namespaces, none of them a numerics namespace (§15.3); no such
  method is known to be called, but none was audited by following the BCL's IL.
- **The script-running tests need bash, GNU grep and perl** (the gate script's own needs). On Windows they look for Git
  for Windows' `bash.exe` and fail with a message naming it if it is absent (xUnit 2.9.3 has no runtime skip).

## 13. What ran (exact)

All builds and tests under `nice -n 19`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `-m:1 -nodeReuse:false`, Release.

| What | Where | Count / result |
|---|---|---|
| `dotnet build Sim.Tests/Sim.Tests.csproj -c Release` | `dfdbc2d` (lane/X) | 0 warnings, 0 errors |
| `dotnet test … --list-tests` | `dfdbc2d` | 3,466 test cases listed |
| `dotnet build/test prototypes/m7-sprint1/M7DetMath.slnx -c Release` | `dfdbc2d` | 39 passed, 0 failed |
| `dotnet build Sim.Tests/Sim.Tests.csproj -c Release` | `e738f08` (OFF) and `-p:SimNumerics=detmath` (ON, worktree `X-on`) | 0 warnings, 0 errors each |
| Full `Sim.Tests`, OFF, attempt 1 | `e738f08` | OOM-killed after 914 passed / 0 failed |
| Full `Sim.Tests`, OFF, attempt 2 | `e738f08` | attempt 2: 3,421 passed, 0 failed, 36 skipped of 3,457 before an OOM kill; plus the 4 classes holding the 22 unreached tests: 38 passed; **union of 3,479: 3,443 passed, 0 failed, 36 skipped** |
| Full `Sim.Tests`, ON | `e738f08` + `detmath` | one host: 1,356 reached before an OOM kill; then 177 classes in 9 chunks (one OOM-killed) and 11 classes in 4 single-thread chunks; **union of 3,479: 3,365 passed, 78 failed (all world-hash pins), 36 skipped** |
| 7 CLI legs × 3 builds (`dfdbc2d`, OFF, ON) | copies of each build's `Sim.Cli` output | 21 runs, 7,500 turn hashes |
| 19 save pairs + field walk + `sim diff` | OFF and ON | §4 |
| 8 dispatch-variant runs of founded 42 × 300 | OFF and ON | §7.1 |
| `DOTNET_JitDisasm='*'`, 60 turns × 4 | OFF and ON | §7.2 |
| libm dispatch script, 10^6 inputs × 2 | Python `math` | §7.1 |
| DetMath vector checker on 3 sets | scratch tool on the ON `Sim.Core.dll` | §6 |
| Held-out vector generation | O-17 generator, seeds 20261106 (N = 2000) and 20261107 (N = 20000) | 17,930 and 175,430 vectors |
| 5 facade mutants + S-M5 re-run, `SimMathNumericsTests` | worktree `X-mut` | §8 |
| S-M5 fix verification on `f8e0acc`: 4 builds (OFF, ON, ON with S-M5 re-applied, `detmth`) + 4 targeted `SimMathNumericsTests` runs, and an OFF build of `e738f08` for comparison | worktree `X-mut` | §8 |
| `scripts/check-banned-constructs.sh` on the experiment head `f8e0acc` | clone `X` | "OK — no banned constructs found" |
| `git apply --check X-kernel-experiment.patch` on a fresh checkout of `dfdbc2d` | scratch clone | applies cleanly |
| On the hand-back tree `lane/X` (`dfdbc2d` + this report): `Sim.Tests` Release build, `--list-tests`, `M7DetMath.slnx` build and tests, `check-banned-constructs.sh`, `check-doc-links.py` on this file | worktree of `lane/X` | 0 warnings, 0 errors; 3,466 test cases (as the baseline); 39 passed; OK; 0 broken links |
| Scan teeth: 6 injections + clean; xUnit scan clean + 1 injection | worktree `X-mut` | §8 |
| `sim bench --founded --seed 42 --turns 300 --json`, ABAB × 4 | OFF and ON copies | §5 |
| **Round 1 (§14), 2026-11-06** | | |
| Reproduction: mutants VM1, VM3, VM4, VMG, VMA, VMB, VTYPO, S-M5, VPLAIN on `f8e0acc` (build, `SimMathNumericsTests` under a 28 s bound, scan; VM1/VM3/VM4/VMG also founded legs against clean ON) | worktree `X-r1mut` | §14.1 |
| Reproduction probes: glibc vs DetMath log on 10^6 uniform u (2 seeds); oracle on 400 differing inputs; Cos domain in both builds; CRLF-content build with `--emit-session`; `prototypes/m7-sprint1` moved aside | scratch tools in `/dev/shm/m7c/X-r1tools` | §14.1 |
| Box–Muller fixture generation (`gen-boxmuller-log-vectors.py`, seed 20261109); held-out C (200,000 inputs, seed 20261110) through the ON `Sim.Core.dll` and the 80-digit oracle | `fb3698b` | §6, §14.1 |
| 18 kill mutants on `fb3698b` (55 s bound) | worktree `X-r1mut` | §14.2 |
| Bash scan teeth: 13 spellings + 2 comment lines + clean | worktree `X-r1mut` at `fb3698b` | §14.3 |
| 7 CLI legs × 3 builds (fresh `lane/X` build, OFF `fb3698b`, ON `fb3698b`) | worktrees `X-r1hand`, `X`, `X-r1on` | §14.3 |
| Moved-pin cases OFF (the ON run was stopped, §14.3); `SimMathNumericsTests` Release/Debug × OFF/ON; `Sim.slnx` Release + Debug; scans; patch apply check | `fb3698b` | §14.3 |
| Hand-back tree `lane/X` (this report only): `Sim.Tests` Release build, `--list-tests`, `M7DetMath.slnx` Debug + Release build and tests, both scans, `check-doc-links.py` | worktree `X-r1hand` | `Sim.Tests` Release: 0 warnings, 0 errors; `--list-tests`: 3,466 cases (= the `dfdbc2d` baseline); `M7DetMath.slnx` Debug and Release: 0 warnings, 0 errors, 39 of 39 passed each; `check-banned-constructs.sh` OK; `check-proto-determinism.sh` CLEAN; `check-doc-links.py` on this file: 0 broken links |
| **Round 2 (§15), 2026-11-06** | | |
| Reproduction on `fb3698b` (detmath or libm Release build, `SimMathNumericsTests` 42 cases, bash scan): M-COMPLEX (+ founded 42 × 300 ON leg against a clean ON build), M-LOCAL, M-SPLIT, M-HOOK (+ the `detmth` build of `Sim.Core`, + the clean-tree control); X-R2-6: OFF and ON CLI, founded 42 × 12 with `--emit-session` | worktree `X-r2` | §15.1 |
| Fixes: commit `823a315` (4 files, +391 / −77 against `fb3698b`) | `lane/X-kernel-experiment` | §15.1 |
| 29 mutants (11 new + the 18 of round 1), bounds 71 s (detmath) and 66 s (libm) | worktree `X-r2`, driver `X-r2tools/mut.py` | §15.2 |
| `Sim.Core.dll` byte comparison `fb3698b` vs `823a315`, both numerics; founded 42 × 300 OFF and ON; `SimMathNumericsTests` Release/Debug × OFF/ON; `Sim.slnx` Release + Debug; `Sim.Tests` with `detmath` Release + Debug; both scans; patch apply check | `823a315` | §15.3 |
| Hand-back tree `lane/X` (this report only): `M7DetMath.slnx` Release + Debug build and tests, both scans, `check-doc-links.py` on this file | clone `X` | §15.3 |

**Artefacts.**
- This report: `prototypes/m7-sprint1/reports/H-X.md` on `lane/X` (the only file `lane/X` changes), copied to
  `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/reports/H-X.md`.
- The experiment (head `823a315` since round 2): `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/patches/X-kernel-experiment.patch`
  (`git diff dfdbc2d..lane/X-kernel-experiment`) and
  `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/bundles/X-kernel-experiment.bundle`.
  To inspect: `git fetch <bundle> refs/heads/lane/X-kernel-experiment:refs/heads/x-exp` in any clone that has `dfdbc2d`.
- The scratch tools (vector checker, field walker, dispatch script, held-out generator wrapper) and raw logs lived in
  `/dev/shm/m7c/X-tools` and `/dev/shm/m7c/X-logs` (RAM, not part of the hand-back); every number they produced is in this report.
- Round 1's scratch tools (mutant driver, log/cos probe, oracle scripts, CLI-leg script) and raw logs are in
  `/dev/shm/m7c/X-r1tools` and `/dev/shm/m7c/X-r1logs` (RAM, not part of the hand-back).

---

## 14. Verification round 1 (2026-11-06)

The verifier's seven findings (X-R1-1 to X-R1-7) were each reproduced against the tree before anything was changed:
the experiment at `f8e0acc` (in a separate worktree of this clone, `/dev/shm/m7c/X-r1mut`, detached) and this report at
`5a95a58`. All seven reproduced; none was refuted. The fixes are one experiment commit, **`fb3698b`** on
`lane/X-kernel-experiment` ("verification round 1 fixes"; 9 files, +14,035 / −81 against `f8e0acc`, of which 13,380
lines are the two oracle-vector fixture files). Its `Sim.Core` change is `SimMath.Cos`'s domain guard and the
`CheckSimNumerics` build target; everything else is `Sim.Tests`, its fixtures and `scripts/check-banned-constructs.sh`.
Nothing was integrated or pushed. Every number in this section was measured in this session on the host of the header.

### 14.1 Per-finding verdicts

| Finding | Sev. | Verdict | Reproduction (on `f8e0acc` / `5a95a58`) | Fix (`fb3698b` unless stated) | Semantic test(s) that now fail on the defect |
|---|---|---|---|---|---|
| **X-R1-1** Log hole in the switch survives every test | major | **REPRODUCED** | Mutant VM1 (`SimMath.cs` ON branch `DetMath.Log` → `Math.Log`, built `detmath`): `SimMathNumericsTests` 14/14 passed (7.4 s, bound 28 s), scan OK; the ON founded leg differs from clean ON from turn 9 (seed 42: 130 of 300 turns differ; seed 16: 2 turns). glibc `Math.Log` ≠ `DetMath.Log` on 857 and 821 per 10^6 uniform u in [0,1) (SplitMix seeds 20261108, 20261107), and on 0 of the test's 20,000 log-uniform inputs (VM1's survival shows it). The §8 S-M3 note was this gap | (a) facade inputs on the campaign's own log distribution: theory case `log-u1`, 100,000 Box–Muller u1 values (53-bit uniform floored at 1e-300, as `HarvestWeatherSystem.NextStandardNormal` forms them); 95 of them discriminate DetMath from glibc on this host. (b) the facade checked on every oracle-vector input, including 200 recorded inputs on which this host's glibc log misrounds (203 discriminating log inputs). (c) **host-independent:** *(corrected in round 2, X-R2-3: as written in `fb3698b`, only one call deep; a libm call in a private `SimMath` helper passed it, §15)* `EverySimMathFunction_CompilesToTheFunctionItsBuildNames` reads the compiled IL of each public `SimMath` method: the `detmath` build must call `DetMath.<same name>` with the same parameters and no BCL transcendental; the `libm` build must call `System.Math.<same name>` and nothing in `DetMath`/`DoubleDouble` | VM1 killed by 3 tests: `…OnTheCampaignRanges(log-u1)`, `…OnTheOracleVectorInputs(log)`, the IL switch test. Same for a Cos hole (VM2) and the original S-M1/S-M2/S-M3 (§14.2) |
| **X-R1-2** scan bypassed by ordinary spellings | major | **REPRODUCED** | On `f8e0acc`, built `detmath`, each passed 14/14 tests and the bash scan printed OK: VM3 `using static System.Math;` + bare `Exp(…)` (ON seed 16 differs from turn 3); VM4 a continuation line starting with `*` (from turn 3); VMG a method group `Func<double, double> LibmExp = Math.Exp;` (from turn 3). Found in addition: VMA an alias `using M = System.Math;` + `M.Exp(…)` passed tests and scan; VMB a code line starting with `/* … */` was caught by the bash rule but not by its xUnit twin (whose `IsCommentLine` exempted `/*`-led lines) | Bash rule: matches the member access with or without `(` (word boundary), exempts only `//`-led lines (`CR013_LINE_COMMENT`), and a second rule bans `using static` and alias imports of `System.Math`/`System.Double`; `SinCosPi` and `MultiplyAddEstimate` added to the name list. The xUnit twin mirrors it (`IsLineComment` = `//` only; `MathImport` regex), with a 13-case theory of spellings. **Spelling-independent** *(corrected in round 2, X-R2-1: independent of the SOURCE spelling for a DIRECT call only. Callees are judged by name and declaring type; calls are not followed into the BCL, so `System.Numerics.Complex.FromPolarCoordinates`, which calls libm cos inside the BCL, passed it, §15)*: `NoTranscendentalCall_InCompiledSimulationCode_OutsideTheFacade` reads the IL of every method, constructor, lambda, local function and type initializer of the compiled `Sim.Core`, `Sim.Data` and `Sim.Cli` (12,774 method bodies, 70,702 call/`ldftn`/`newobj` operands, 0 unresolved) and fails on any call or method-group load of a BCL transcendental (`System.Math`, `System.Double`, the generic-math interfaces, vector/tensor types; integer-returning `Log2`s excluded) outside `SimMath` | VM3, VM4, VMG, VMA, VMB, the plain `Math.Exp` (VPLAIN) and the reverted call site (S-M4): each killed by the IL chokepoint test and the source twin, and the bash scan exits 1 on each |
| **X-R1-3** audit wrongly says no code hashes raw content bytes | minor | **REPRODUCED** | `ForensicIdentity.ConfigResources` (`Sim.Core/Observability/Forensic/ForensicIdentity.cs:75-100`) SHA-256s the raw bytes of every embedded `Sim.Data.*` resource; they feed `ConfigDigest` (`:106-113`) and the run id (`ForensicSession.cs:60-64`). `.gitattributes` pins only the two session-export paths; no workflow sets `core.autocrlf`. **Measured** (mutant worktree at `f8e0acc`, OFF build; `Sim.Data` there is identical to `dfdbc2d`): `Sim.Cli` built once with the content as committed (LF) and once with every `Sim.Data/content/*.json` converted to CRLF (109,569 CR bytes added), founded 42 × 300 with `--emit-session`: `configDigest` `c3be9123…` → `75a2263c…`, run id `e42e443b845d2ca8` → `1259e2109b8cc53d`; the 300 per-turn world hashes identical (final `d5ebd0b2…` = `FOUNDED_GOLDEN`) | **Report only** (the remedy is a landing decision, not part of the transcendental experiment): §0 item 8 and the §7.3 row corrected; landing step 4a added (§9); evidence W7 added (§10) | — (documentation finding; the measurement above is the evidence). INFERRED, not measured: whether a `windows-latest` `actions/checkout` produces CRLF. The Director's win-x64 recording `director-68` (build `231485e`) records LF byte counts and digests for all 12 content files, matching `git show 231485e:…`; that build was published on `ubuntu-latest` (`ui-artifact.yml:29`), so it says nothing about a Windows checkout |
| **X-R1-4** log oracle vectors miss the campaign distribution | minor | **REPRODUCED** | The 400 inputs (seed 20261108) on which glibc and DetMath disagree, checked against Python `decimal` ln at 80 digits: glibc correctly rounded on 0, DetMath on 400, neither 0 (the verifier's figure exactly) | New fixture `Sim.Tests/Fixtures/detmath/cr-vectors-boxmuller-log.txt` from `gen-boxmuller-log-vectors.py` (seed 20261109, same oracle as `gen-cr-vectors.py`): part A, the first 4,000 Box–Muller u1 draws as drawn (glibc misrounds 3); part B, the next 200 draws on which this host's glibc log misrounds (found in 246,844 further draws). DetMath: 0 mismatches on all 4,200. Held-out C (not stored; seed 20261110): 200,000 uniform-u inputs, **DetMath 0 mismatches, glibc misrounds 164 (0.082 %)**, and glibc ≠ DetMath on exactly those 164. §6 table extended | `DetMath_ReproducesTheCorrectlyRoundedVectors_BitForBit(log)` now covers the 4,200 (it also caught mutant DIGEST's dropped Cody–Waite term through the cos vectors) |
| **X-R1-5** mistyped `SimNumerics` refused only by the tests | minor | **REPRODUCED** | `dotnet build Sim.Tests/Sim.Tests.csproj -c Release -p:SimNumerics=detmth` on `f8e0acc`: 0 errors (libm compiled); `SimMathNumericsTests` then 13 passed, 1 failed | `Sim.Core.csproj` target `CheckSimNumerics` (`BeforeTargets="PrepareForBuild"`): any value other than unset, `libm` or `detmath` (MSBuild compares case-insensitively, as the switch condition does) fails the build with `SimNumerics='detmth' is not a known numerics …` | `TheSimCoreBuild_AcceptsOnlyTheKnownNumericsValues` runs the target in a child MSBuild (bounded at 180 s) for `detmth` (must fail with the message), `libm`, `detmath` and unset (must pass); mutant GUARD (condition disabled) is killed by its `detmth` case. *(Corrected in round 2, X-R2-2: it calls the target by name, so it could not see a guard detached from the build; `AnUnknownNumericsValue_FailsARealSimCoreBuild_BeforeCompiling` now pins the hook, §15)* On `fb3698b` the `detmth` build itself fails (mutant VTYPO, §14.2) |
| **X-R1-6** permanent test would depend on the prototype tree | minor | **REPRODUCED** | `prototypes/m7-sprint1` moved aside in the mutant worktree at `f8e0acc`: the `Sim.Tests` build fails (`MSB3030: Could not copy the file …/Proto.DetMath.Tests/vectors/cr-vectors.txt`), and `TheKernelCopy_IsThePrototype…` fails (`DirectoryNotFoundException`) | Vectors and generator **copied** into `Sim.Tests/Fixtures/detmath` (`cr-vectors.txt` byte-identical to the prototype's at `dfdbc2d`, sha256 `2e296ea4…`); `Sim.Tests.csproj` no longer references `prototypes/`; `TheKernelCopy_IsThePrototype…` replaced by `TheKernelDetMath_IsPinnedByDigest` (line-ending-normalised SHA-256 of `DetMath.cs` `65ec72ac…` and `DoubleDouble.cs` `9993b527…`, equal to `sha256sum`); `TheOracleVectorFixtures_AreTheRecordedFiles` pins both vector files. Provenance re-measured: the kernel copies equal the prototype after the namespace substitution except the one comment line | With `prototypes/m7-sprint1` moved aside at `fb3698b`: both builds 0 errors, `SimMathNumericsTests` 42/42 (OFF and ON). Mutant DIGEST (one line deleted in `DetMath.cs`) killed by the digest pin and the cos vectors |
| **X-R1-7** Cos domain differs between the switch states | nit | **REPRODUCED** | Scratch probe on the two builds of `f8e0acc`: `SimMath.Cos(1e9)` = 0.8378871813639024 (libm) vs `ArgumentOutOfRangeException` (DetMath); likewise ±nextUp(2^28), 1e300; both give NaN for ±∞ and NaN | `SimMath.Cos` refuses finite \|x\| > `CosDomainLimit` (2^28) in **both** builds with the same comparison `DetMath.Cos` makes; the only caller (`2πu`) never reaches it | `TheCosDomain_IsDetMathsDomain_InBothSwitchStates`: on 19 inputs straddling ±2^28 plus the non-finite values, `SimMath.Cos` throws exactly when `DetMath.Cos` does (8 of 19 in both builds). Mutants COSG (guard removed) and COSGE (`>=`) killed in the libm build |

### 14.2 Mutant record, round 1 (MEASURED)

Worktree `/dev/shm/m7c/X-r1mut` (this run's own), detached at `fb3698b`; driver `/dev/shm/m7c/X-r1tools/mut.py` (reverts
every file it touches and checks the tree is clean; logs `/dev/shm/m7c/X-r1logs/kill/`). Each mutant: Release build
(with `-p:SimNumerics=detmath` where marked), then `SimMathNumericsTests` (42 cases) under a **55 s bound = 10 × the
5.5 s clean run**, then `scripts/check-banned-constructs.sh`. No run hung (every test run finished in 4.9–5.8 s).

| Mutant | Build | Tests failed (of 42) | Bash scan | Verdict |
|---|---|---|---|---|
| VM1 ON `Log` → `Math.Log` (X-R1-1) | detmath | facade `log-u1`, facade oracle `log`, IL switch | OK | killed |
| VM2 ON `Cos` → `Math.Cos` | detmath | facade `cos`, facade oracle `cos`, IL switch | OK | killed |
| S-M1 OFF `Exp` → `DetMath.Exp` | libm | facade `exp`, facade oracle `exp`, IL switch | OK | killed |
| S-M2 ON `Pow` → `Math.Pow` | detmath | facade `pow`, facade oracle `pow`, IL switch | OK | killed |
| S-M3 ON build labelled `libm` | detmath | 10 (every facade case, `TheBuildsNumerics…`, IL switch) | OK | killed |
| S-M4 call site reverted to `Math.Exp` (`HarvestWeatherSystem.cs`) | libm | IL chokepoint, source twin | **FAILED** | killed |
| VM3 `using static System.Math;` + `Exp(…)` (X-R1-2 a) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VM4 continuation line `* (Math.Exp(…) - 1.0)` (X-R1-2 b) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VMG method group `= Math.Exp;` (X-R1-2 c) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VMA alias `using M = System.Math;` + `M.Exp(…)` (new) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VMB `/* … */ double raw = … Math.Exp(…)` (new) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VPLAIN `Math.Exp(exponent)` in `PriceSystem.cs` (control) | detmath | IL chokepoint, source twin | **FAILED** | killed |
| VTYPO `-p:SimNumerics=detmth`, no source change (X-R1-5) | detmth | **the build fails** (`Sim.Core.csproj(20,5): error : SimNumerics='detmth' is not a known numerics …`); the test step then ran stale binaries and is disregarded | OK | killed (by the build) |
| S-M5 switch condition broken (`detmath-typo`) | detmath | `TheBuildsNumerics_IsTheOneTheBuildPropertyAskedFor` | OK | killed |
| GUARD `CheckSimNumerics` condition disabled | libm | `TheSimCoreBuild_AcceptsOnlyTheKnownNumericsValues(detmth)` | OK | killed |
| COSG Cos guard removed | libm | `TheCosDomain_IsDetMathsDomain_InBothSwitchStates` | OK | killed |
| COSGE Cos guard `>` → `>=` | libm | Cos domain test; facade oracle `cos` (the vector set holds 2^28 exactly) | OK | killed |
| DIGEST `DetMath.Cos` third Cody–Waite term deleted | detmath | `TheKernelDetMath_IsPinnedByDigest(DetMath.cs)`, `DetMath_ReproducesTheCorrectlyRoundedVectors_BitForBit(cos)` | OK | killed |

**18 of 18 killed: 17 by at least one semantic test each, and VTYPO by the build itself** (the build guard it hits is pinned by `TheSimCoreBuild_AcceptsOnlyTheKnownNumericsValues`, which kills mutant GUARD). No kill is golden-only: no hash pin is among the 42 cases. The
reproduction runs on `f8e0acc` (logs `/dev/shm/m7c/X-r1logs/*.txt`) are §14.1's evidence that VM1, VM3, VM4, VMG, VMA
and VTYPO survived before the fix and that S-M5 and VPLAIN were already killed.

### 14.3 Evidence that round 1 moved nothing it should not (MEASURED on `fb3698b`)

| Check | Build(s) | Result |
|---|---|---|
| 7 CLI legs of §2 (2,500 per-turn hashes), OFF build of `fb3698b` against a fresh build of the hand-back tree `lane/X` (`Sim.*` = `dfdbc2d`) | Release | **byte-identical, 7 of 7 logs** (`cmp`); finals `ba223ffe…` (canonical), `d5ebd0b2…` = `FOUNDED_GOLDEN`, `5bb5d61e…`, `542ffa8a…`, `9acee8f0…`, `d8eac10b…`, `ce267650…`. Both sets also equal, file for file, the verifier's independent `dfdbc2d` and ON logs |
| The same 7 legs, ON build of `fb3698b` | Release, `detmath` | finals equal to §3's ON column, 7 of 7; the founded 42 and 16 logs equal, turn for turn, the ON logs of `f8e0acc` taken in this session (the Cos guard and the build target change no value) |
| The 78 moved-pin test cases of §3 (76 methods), OFF | Release | **80 of 80 test cases passed, 0 failed**: every case of the 76 methods (the 78 moved cases plus 2 sibling theory cases). 57 ran in one test host (`MaxParallelThreads=2`) before the shared memory cgroup OOM-killed it; the other 23 methods ran in 8 per-class single-thread hosts, each passing at its first attempt (trx files in `/dev/shm/m7c/X-r1logs/trx`) |
| The same 78 cases, ON | Release, `detmath` | **not completed.** Started after the OFF run; stopped by hand after about 7 minutes because the shared memory cgroup was at its limit and was OOM-killing another agent's process every ~36 s (this lane's first OFF test host had been killed the same minute). The ON values are evidenced instead by the 7 legs above (finals equal §3's ON column; founded 42 and 16 equal the `f8e0acc` ON logs turn for turn) |
| `SimMathNumericsTests` (42 cases) | Release OFF / Release ON / Debug OFF / Debug ON | 42 of 42 passed in each of the four runs; each prints the numerics asked for and built (`''`/`libm` and `detmath`/`detmath-1`). Clean-run wall time 5.2–5.5 s (Release) |
| Builds | `Sim.slnx` Release and Debug (OFF; includes `Sim.Ui`, `Sim.Ui.Tests`); `Sim.Tests` Release and Debug with `detmath` | 0 warnings, 0 errors each (`Sim.slnx`: all 6 projects, Release and Debug; `Sim.Tests` with `detmath`: Release and Debug) |
| `scripts/check-banned-constructs.sh` | `fb3698b` | OK. Teeth: 13 injected spellings (the 6 of §8, VM4's continuation line, a method group, a `/* */`-led line, a call followed by a trailing comment, `using static System.Math;`, an alias, `global using static global::System.Double;`) each fail it with exactly one hit; a `//` and a `///` line mentioning `Math.Exp` pass |
| `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | `fb3698b` and `lane/X` | CLEAN on both (92 files under `Proto.M7`, 0 findings) |
| `git apply --check` of `X-kernel-experiment.patch` on a fresh worktree of `dfdbc2d`; the applied tree equals `lane/X-kernel-experiment` | — | applies cleanly; equal (one whitespace warning: a trailing blank in the copied `gen-cr-vectors.py`, byte-identical to the prototype's) |

**Not re-run in round 1:** the full `Sim.Tests` suite in either build (only 2 GB of memory free on the shared host; every
single-host run of it so far was OOM-killed, §12), the bench of §5, the dispatch and FMA checks of §7.1–7.2, and
anything in `Sim.Ui.Tests`. Round 1 changed no `Sim.Core` arithmetic: the OFF `SimMath` IL is pinned to call
`System.Math.<same name>` (§14.1, X-R1-1), and the only new branch (`Cos`'s guard) is false for every input of the only
caller.

### 14.4 What round 1 changes in the landing plan and the recommendation

- Step 1's content gains the round-1 checks and the copied fixtures (§9, updated in place); step 2's proof is the
  13-spelling teeth record plus the compiled-IL check; new step 4a (content line endings) and evidence item W7.
- The recommendation of §11 is unchanged: (a), staged. Round 1 strengthens its first point (the switch's own tests now
  see a hole in any of the four functions on any host, through the IL check) and adds one Windows question (W7) that
  the step-4 matrix answers in the same dispatch. *(Corrected in round 2, X-R2-3: the IL check at `fb3698b` was one call
  deep and exempted every `SimMath` method, so a hole routed through a `SimMath` helper was seen only by the value
  tests, whose teeth depend on the host libm misrounding the stored inputs (unmeasured for the Windows UCRT). Since
  `823a315` the IL check walks `SimMath`'s own call graph and the chokepoint exempts only the libm build's direct
  `SimMath.X` → `System.Math.X` call, §15.)*

---

## 15. Verification round 2 (2026-11-06)

The verifier's six findings (X-R2-1 to X-R2-6) were each reproduced against the tree before anything was changed: the
experiment at `fb3698b` (in this run's own worktree `/dev/shm/m7c/X-r2`, on `lane/X-kernel-experiment`) and this report
at `3fb2c58`. **All six reproduced; none was refuted.** The fixes are one experiment commit, **`823a315`** ("verification
round 2 fixes"; 4 files, +391 / −77 against `fb3698b`: `Sim.Tests/Kernel/SimMathNumericsTests.cs`,
`scripts/check-banned-constructs.sh`, and comment-only edits to `Sim.Core/Kernel/SimMath.cs` and `Sim.Core/Sim.Core.csproj`).
X-R2-5 and X-R2-6 are report corrections (§1, §2, §9 step 3, §11, §14, marked *(corrected in round 2)* or *(added in
round 2)*). Nothing was integrated or pushed. Every number in this section was measured in this session on the host of
the header; the memory cgroup killed two of this lane's processes during the round (a CLI run, a `csc`), and each was
re-run once and passed.

### 15.1 Per-finding verdicts

| Finding | Sev. | Verdict | Reproduction (on `fb3698b` / `3fb2c58`) | Fix (`823a315` unless stated) | Semantic test(s) that now fail on the defect |
|---|---|---|---|---|---|
| **X-R2-1** a BCL API that calls libm internally passes every guard; three texts overclaim; step 2's ban misses `Complex` | minor | **REPRODUCED** | Mutant M-COMPLEX (`HarvestWeatherSystem.cs:324` `SimMath.Cos(2.0 * Math.PI * u2)` → `System.Numerics.Complex.FromPolarCoordinates(1.0, 2.0 * Math.PI * u2).Real`, built `detmath`): `SimMathNumericsTests` 42 of 42 passed (5.2 s), bash scan "OK". The world still changed: the founded seed-42 ON leg differs from a clean ON build from turn 12, in 235 of 300 turns (clean final `86fbbc16…`). The verifier's seed-16 figure was not re-measured. The overclaiming texts were read in the tree: the test class summary (`:24`), the chokepoint docstring (`:277`), `SimMath.cs:9`, §14.1 row X-R1-2 and §9 step 2 | IL: `Il.IsBclNumericsSurface` refuses ANY member (method, constructor, accessor) of a type in System.Numerics\* or System.Runtime.Intrinsics\*, in the chokepoint check and along `SimMath`'s own call graph. Bash: new rule "System.Numerics / System.Runtime.Intrinsics in simulation code (CR-013)" (any mention outside a `//` line), also in the multi-line pass; the twin has the same regex. Texts corrected: test class summary, chokepoint docstring, `SimMath.cs` header (now says calls are not followed into the BCL), §0 item 9, §9 step 2, §14.1. Measured context: the non-Sim callees of simulation code fall in 15 BCL namespaces, none of them a numerics namespace (§15.3) | M-COMPLEX and M-PHASE (`SimMath.Cos(new System.Numerics.Complex(1.0, 2.0 * Math.PI * u2).Phase)`: a property getter, value-changing): each killed by `NoTranscendentalCall_InCompiledSimulationCode_OutsideTheFacade` (IL, host-independent) and the source twin; the bash scan exits 1 under the new rule |
| **X-R2-2** nothing pins that the build guard is hooked into the build | minor | **REPRODUCED** | Mutant M-HOOK (`BeforeTargets="PrepareForBuild"` → `"Publish"`): `dotnet build Sim.Core/Sim.Core.csproj -c Release -p:SimNumerics=detmth` gives 0 warnings, 0 errors; the default build's `SimMathNumericsTests` 42 of 42 passed (5.2 s). Control, clean tree: the same build fails, `Sim.Core.csproj(20,5): error : SimNumerics='detmth' is not a known numerics …` | New test `AnUnknownNumericsValue_FailsARealSimCoreBuild_BeforeCompiling`: runs the real `Build` target of `Sim.Core` (Release, `SimNumerics=detmth`) with `OutDir` and `IntermediateOutputPath` in a fresh temporary directory, bounded at 180 s; asserts a non-zero exit, the guard's message, and that no `Sim.Core.dll` was produced. On the clean tree: exit 1, message present, no DLL. The value test's docstring no longer claims the hook; the csproj comment names both pins | M-HOOK and M-HOOKRM (`BeforeTargets` removed): each killed by the new test (the child build then compiles `Sim.Core` into the temporary directory; runs 13.3 s and 13.1 s). Round-1 mutant GUARD is now killed by both build tests |
| **X-R2-3** the IL switch check is one call deep and the chokepoint exempts all of `SimMath`; §14.4 overclaims | minor | **REPRODUCED** | Mutant M-LOCAL (ON `Log` → `x < 0.5 ? Tail(x) : DetMath.Log(x)`, plus `private static double Tail(double x) => Math.Log(x);`, built `detmath`): 40 of 42 passed; the only failures were `…OnTheCampaignRanges(log-u1)` and `…OnTheOracleVectorInputs(log)`, both of which depend on this host's glibc misrounding the stored inputs. Both IL tests passed; the bash scan printed "OK" (`SimMath.cs` is exempt from the transcendental rule) | `EverySimMathFunction_CompilesToTheFunctionItsBuildNames` walks `SimMath`'s own call graph from each public function: callees declared on `SimMath` or on a type nested in it (private helpers, local functions, lambdas) are followed to any depth, and every external callee is judged. The chokepoint exemption is narrowed from "every method of `SimMath`" to "in the libm build, a public `SimMath.X` calling `System.Math.X` with its own parameters"; nothing is exempt in the detmath build. §14.4 and §14.1 (c) corrected | M-LOCAL and M-LOCALFN (a `static` local function calling `Math.Log` inside `Log`): each killed by BOTH IL tests (host-independent), plus the two value tests on this host. The round-1 holes VM1, VM2 and SM2 are now also killed by the chokepoint test |
| **X-R2-4** the bash rule and its twin are line-based; no test runs the script | nit | **REPRODUCED** | (a) Mutant M-SPLIT (`PriceSystem.cs`: `SimMath.Exp(exponent)` → `Math` / `.Exp(exponent)` on two lines, built `detmath`): bash scan "OK"; the twin passed; only the IL chokepoint failed ("PriceSystem.Step(…) calls System.Math.Exp(Double)"). (b) `git grep` over `Sim.Tests`: no test executes `scripts/check-banned-constructs.sh` (two comment mentions only) | (a) The twin matches each file as ONE text with every `//`-led line blanked (line numbers kept), so whitespace in its patterns may include line breaks; the script has one `CR013_NAMES` list for its rules and a perl pass that applies the three CR-013 patterns to whole files and reports, under "CR-013 rule matched across a line break (CR-013)", the matches that span a line break (one-line matches stay with the grep rules, so each spelling is reported once). (b) `TheBannedConstructsScript_RefusesEachInjectedSpelling` (17 cases: the round-1 spellings, FMA, a trailing comment, the three import forms, two numerics forms, four split forms) runs the repository's script on a scratch tree holding only the script and the injected file and asserts exit 1, the rule's name and `file:line`; `…AcceptsCleanCodeAndCommentLines` is its control (exit 0); `TheBashRule_NamesTheSameTranscendentals_AsTheTwinAndTheIlCheck` pins the three name lists equal. The twin theory gained 9 cases (split forms, numerics forms, negatives) | M-SPLIT: killed by the IL chokepoint and the twin, and the scan exits 1. Weakenings of the script itself, which leave the clean tree passing the scan: M-WEAKPAREN (rule requires `(` after the name) killed by the method-group case; M-WEAKNAME (`Exp` dropped from `CR013_NAMES`) by 5 script cases and the names test; M-NOSPLIT (multi-line pass disabled) by the 4 split cases; M-NONUM (numerics grep rule disabled) by the 2 numerics cases |
| **X-R2-5** round-0 statements stale after `fb3698b`, not marked | nit | **REPRODUCED** | Read in `3fb2c58`: `H-X.md:86` ("(75 lines) … so the OFF build contains no branch") and `:96` ("no branch survives compilation"); found in addition `:128` (§2: "each `SimMath` method is a single call of the `System.Math` method with the same argument") and `:496` (§11: "no golden and no behaviour"). At `fb3698b`, `SimMath.cs` has 90 lines and `Cos` runs its guard (`:81`) before the `#if`; `Math.Cos(1e9)` = 0.8378871813639024 under libm (round-1 probe) while the OFF `SimMath.Cos(1e9)` throws (asserted by `TheCosDomain_IsDetMathsDomain_InBothSwitchStates`, which passes in the OFF build) | Report: each of the four statements marked *(corrected in round 2)*, naming the guard, the behaviour change for finite \|x\| > 2^28, and the evidence that the only caller never reaches it. Comment-only in `SimMath.cs`: "costs nothing when OFF (no switch branch survives compilation)" → "adds no switch branch to the OFF build (only Cos's shared guard)", and the switch summary names the guard | — (documentation finding). The guard itself stays pinned: COSG and COSGE are killed again (§15.2) |
| **X-R2-6** the forensic run id ignores the numerics | nit | **REPRODUCED (measured)** | `fb3698b`, the libm and the detmath CLI, `run --founded --seed 42 --turns 12 --emit-session <dir>`: both emitted run `e42e443b845d2ca8`; the two `session-e42e443b845d2ca8.json` files are byte-identical (`schemaVersion` 39, `buildSha` "dev", no numerics field); their forensic and trace files differ; the world hashes are equal through turn 8 and differ from turn 9 (turn 12: `71179a8c…` libm, `a8385d13…` detmath). Code read: `ForensicIdentity.cs:41-44` (`RunIdInputs`), `:134-148` (`RunId`), `SessionEmitter.cs:78-79` (`_stem = RunId`) | Report only (a proposal gap; the Director decides): §9 step 3 amended with this measurement and three options: (a) the numerics id in the run-id preimage only when it is not `libm` (every recorded id unchanged; a forensic-identity recipe change, with ADR); (b) the 39 → 40 bump, which separates ids only across the version boundary, not a libm and a detmath build of one commit; (c) the manifest key alone, accepting the collision. Proposal: (a) with the manifest key | — (no code). A step-3 implementation would need a test that a libm and a detmath run of the same inputs get different run ids while a libm run's id is unchanged |

### 15.2 Mutant record, round 2 (MEASURED)

Worktree `/dev/shm/m7c/X-r2` (this run's own) on `lane/X-kernel-experiment` at `823a315`; driver
`/dev/shm/m7c/X-r2tools/mut.py` (applies one mutant, builds Release, runs `SimMathNumericsTests` (71 cases) under the
bound, runs the bash scan, reverts the touched files with `git checkout` and checks that the tree is clean, which it was
after each of the 29). Bounds: **71 s for detmath builds (10 × the 7.1 s clean run) and 66 s for libm builds (10 × 6.6 s)**.
No run hung: every test run finished in 6.4–13.5 s. Logs: `/dev/shm/m7c/X-r2logs/kill/`.

| Mutant | Build | Tests failed (of 71) | Bash scan | Verdict |
|---|---|---|---|---|
| M-COMPLEX `Complex.FromPolarCoordinates(…).Real` for `SimMath.Cos` (X-R2-1) | detmath | IL chokepoint, twin | **FAILED** (numerics rule) | killed |
| M-PHASE `SimMath.Cos(new Complex(1, 2πu₂).Phase)` (X-R2-1) | detmath | IL chokepoint, twin | **FAILED** (numerics rule) | killed |
| M-LOCAL private `SimMath.Tail` → `Math.Log` (X-R2-3) | detmath | IL switch, IL chokepoint, facade `log-u1`, facade oracle `log` | OK | killed |
| M-LOCALFN static local function → `Math.Log` (X-R2-3) | detmath | IL switch, IL chokepoint, facade `log-u1`, facade oracle `log` | OK | killed |
| M-SPLIT `Math` / `.Exp(…)` across lines (X-R2-4 a) | detmath | IL chokepoint, twin | **FAILED** (line-break rule) | killed |
| M-HOOK `BeforeTargets="Publish"` (X-R2-2) | libm | real-Build hook test | OK | killed |
| M-HOOKRM `BeforeTargets` removed (X-R2-2) | libm | real-Build hook test | OK | killed |
| M-WEAKPAREN script rule requires `(` (X-R2-4 b) | libm | script case: method group | OK | killed |
| M-WEAKNAME `Exp` dropped from `CR013_NAMES` (X-R2-4 b) | libm | 5 script cases, names-parity test | OK | killed |
| M-NOSPLIT multi-line pass disabled (X-R2-4 b) | libm | the 4 split script cases | OK | killed |
| M-NONUM numerics grep rule disabled (X-R2-1/4) | libm | the 2 numerics script cases | OK | killed |
| VM1 ON `Log` → `Math.Log` | detmath | IL switch, IL chokepoint, facade `log-u1`, facade oracle `log` | OK | killed |
| VM2 ON `Cos` → `Math.Cos` | detmath | IL switch, IL chokepoint, facade `cos` (both sets) | OK | killed |
| S-M1 OFF `Exp` → `DetMath.Exp` | libm | IL switch, facade `exp` (both sets) | OK | killed |
| S-M2 ON `Pow` → `Math.Pow` | detmath | IL switch, IL chokepoint, facade `pow` (both sets) | OK | killed |
| S-M3 ON build labelled `libm` | detmath | 10 (IL switch, `TheBuildsNumerics…`, every facade case) | OK | killed |
| S-M4 call site reverted to `Math.Exp` | libm | IL chokepoint, twin | **FAILED** | killed |
| VM3 `using static System.Math;` + `Exp(…)` | detmath | IL chokepoint, twin | **FAILED** | killed |
| VM4 continuation line `* (Math.Exp(…) - 1.0)` | detmath | IL chokepoint, twin | **FAILED** | killed |
| VMG method group `= Math.Exp;` | detmath | IL chokepoint, twin | **FAILED** | killed |
| VMA alias `using M = System.Math;` | detmath | IL chokepoint, twin | **FAILED** | killed |
| VMB `/* … */`-led line | detmath | IL chokepoint, twin | **FAILED** | killed |
| VPLAIN `Math.Exp(exponent)` (control) | detmath | IL chokepoint, twin | **FAILED** | killed |
| VTYPO `-p:SimNumerics=detmth`, no source change | detmth | **the build fails** (`Sim.Core.csproj(22,5): error : SimNumerics='detmth' is not a known numerics …`) | OK | killed (by the build) |
| S-M5 switch condition broken (`detmath-typo`) | detmath | `TheBuildsNumerics_IsTheOneTheBuildPropertyAskedFor` | OK | killed |
| GUARD `CheckSimNumerics` condition disabled | libm | real-Build hook test, `TheSimCoreBuild_AcceptsOnlyTheKnownNumericsValues(detmth)` | OK | killed |
| COSG Cos guard removed | libm | Cos domain test | OK | killed |
| COSGE Cos guard `>` → `>=` | libm | Cos domain test, facade oracle `cos` | OK | killed |
| DIGEST third Cody–Waite term deleted from `DetMath.Cos` | detmath | digest pin, `DetMath_ReproducesTheCorrectlyRoundedVectors_BitForBit(cos)` | OK | killed |

**29 of 29 killed: 28 by at least one semantic test each, and VTYPO by the build itself** (the build's guard is pinned
by the two build tests, which kill GUARD, M-HOOK and M-HOOKRM). No kill is golden-only: no hash pin is among the 71
cases. Teeth and aim (ADR-015 §7.2): each failing test asserts a property the system ought to have — the facade calls
exactly the function its build names; no libm call outside the facade; the build refuses an unknown value; the CI gate
script refuses the banned spellings. The four script weakenings leave the clean tree passing the scan; only the new
script-running tests see them, which is X-R2-4 (b).

### 15.3 Evidence that round 2 moved nothing it should not (MEASURED on `823a315`)

| Check | Result |
|---|---|
| `git diff --stat fb3698b 823a315 -- Sim.Core Sim.Data Sim.Cli` | `SimMath.cs` and `Sim.Core.csproj` only, comment lines only (+14 / −8) |
| `Sim.Core.dll` built from `fb3698b` and from `823a315` (Release, `DebugType=none`, no source revision in the version, isolated output paths), both numerics | **byte-identical**: OFF 2,285,056 bytes, sha256 `0b290bb0…`; ON 2,285,056 bytes, sha256 `655bac76…`; OFF ≠ ON. With `Sim.Data` and `Sim.Cli` unchanged, every OFF and ON measurement of §2, §3 and §14.3 stands for `823a315` |
| Founded seed 42 × 300, final Release builds of `823a315` | OFF final `d5ebd0b2…` = `FOUNDED_GOLDEN` (`ci.yml`); ON per-turn log equal, 300 of 300, to the `fb3698b` ON log taken in this round (final `86fbbc16…`) |
| `SimMathNumericsTests` (71 cases) | Release OFF 71/71 (6.6 s; 7.1 s on the final build), Release ON 71/71 (7.1 s; 7.0 s), Debug OFF 71/71 (7.3 s), Debug ON 71/71 (7.4 s) |
| What the IL walk read | 12,774 method bodies, 70,702 call/`ldftn`/`newobj` operands, 0 unresolved; 0 `SimMath`-internal helpers, local functions or lambdas today (detmath build). Non-Sim callees in 15 namespaces: System, System.Collections, System.Collections.Concurrent, System.Collections.Generic, System.Collections.Immutable, System.Diagnostics, System.Globalization, System.IO, System.Linq, System.Reflection, System.Runtime.CompilerServices, System.Runtime.InteropServices, System.Security.Cryptography, System.Text, System.Text.Json |
| Builds | `Sim.slnx` Release and Debug (OFF; 6 projects incl. `Sim.Ui`, `Sim.Ui.Tests`): 0 warnings, 0 errors each (the first Debug attempt lost its `csc` to the memory cgroup, exit 137; the retry passed). `Sim.Tests` with `detmath`, Release and Debug: 0 warnings, 0 errors |
| Scans on `823a315` | `scripts/check-banned-constructs.sh`: OK. `prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: CLEAN (92 files) |
| `X-kernel-experiment.patch` (`git diff dfdbc2d..lane/X-kernel-experiment`) | `git apply --check` passes on a fresh worktree of `dfdbc2d`; the applied tree equals `lane/X-kernel-experiment` (28 files, +15,341 / −58; one whitespace warning: the copied generator's trailing blank, byte-identical to the prototype's) |
| Hand-back tree `lane/X` (this report only; `Sim.*` = `dfdbc2d`) | `M7DetMath.slnx` Release and Debug: 0 warnings, 0 errors, 39 of 39 passed each; `check-banned-constructs.sh` OK; `check-proto-determinism.sh` CLEAN; `check-doc-links.py` on this file: 1 file checked, 0 broken links |

**Not re-run in round 2:** the full `Sim.Tests` suite in either build, the moved-pin cases, the other six CLI legs (the
byte-identical `Sim.Core.dll` covers them), the bench, the dispatch and FMA checks, and `Sim.Ui.Tests`.

### 15.4 What round 2 changes in the landing plan, the claims and the recommendation

- **Step 1** carries the round-2 checks; **step 2** now refuses System.Numerics\* and System.Runtime.Intrinsics\* in all
  three layers, its bash rule spans line breaks and has automated teeth, and the IL check is named as the gate for
  direct calls; **step 3** gains the run-id question (X-R2-6) with options (a) to (c) and proposal (a). All updated in
  place in §9.
- **What no guard covers (stated, not fixed).** The IL checks judge each callee by its name and declaring type and do
  not follow calls into the BCL. A BCL method outside the two refused namespaces that calls libm internally under a
  non-transcendental name would pass every layer. None is known to be called today (the namespace list of §15.3), but
  the BCL's IL was not walked to prove it. Options at landing, all PROPOSALS: (i) a bounded walk of the statically bound
  calls from simulation code into the BCL's IL, failing on any reachable transcendental (it cannot resolve virtual or
  interface dispatch, so it narrows the gap without closing it); (ii) an allowlist of the BCL types simulation code may
  call, so that any new BCL type is reviewed; (iii) rely on the step-4 Windows/Linux equality legs, which are the
  end-to-end proof in any case. This lane's view: (iii) is necessary whatever else is chosen; (ii) is cheap and fails
  closed.
- **Host dependence of the value tests is unchanged:** the facade value tests discriminate a hole only where the host
  libm misrounds the stored inputs (measured on glibc 2.39; unmeasured on the Windows UCRT). The host-independent guards
  are the two IL checks, which since round 2 cover `SimMath`'s helpers too.
- **The recommendation of §11 is unchanged: (a), staged.** Round 2 adds the run-id identity to step 3's scope and
  removes overclaims; it changes no measurement the recommendation rests on (§15.3).
