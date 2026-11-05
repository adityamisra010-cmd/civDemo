# O-17 — cross-platform determinism: DetMath prototype, accuracy and golden impact

Slice O-17 of the M7 sprint 1 (implementation-spec §7). Written 2026-11-04 by the O-17 lane agent.
Tree read: `/home/user/m5w/dev`, branch `claude/new-session-yavghm`, HEAD `7d5defc`, plus the uncommitted
governance docs. Nothing outside `prototypes/m7-sprint1/` was written. No git state was changed.
Copy of this report: `m7s1/reports/o17-cross-platform.md` (scratchpad).

> **WINDOWS EQUALITY IS UNTESTED.** Every number below was measured on one Linux x64 host
> (glibc 2.39, .NET 10.0.12, SDK 10.0.112, Intel Xeon 2.1 GHz with AVX2+FMA). No Windows run was made.
> The only Windows comparison in the repository is `xplat-surveillance.yml`, which does not trigger on
> `claude/**` branches and needs a push, and this sprint may not push. **This report does not claim
> cross-platform hash equality.**

Labels: **MEASURED** (run in this session), **RE-VERIFIED** (read in the tree at the cited line in this
session), **DOC-ONLY** (stated in a repository document; not re-measured), **INFERRED**.

---

## 1. What exists now

| Path | What it is |
|---|---|
| `M7DetMath.slnx` | Solution: `Proto.DetMath` and `Proto.DetMath.Tests`. It has no `Sim.*` reference. |
| `Proto.DetMath/DetMath.cs` | Public `DetMath.Exp`, `Log`, `Pow`, `Cos`. |
| `Proto.DetMath/DoubleDouble.cs` | Error-free transformations (TwoSum, FastTwoSum, Veltkamp Split, Dekker TwoProd) and double-double add, mul and div. |
| `Proto.DetMath.Tests/DetMathCorrectnessTests.cs` | Correctness tests that do not depend on the host libm: oracle vectors, IEEE identities, special values, tie-dense midpoints, monotonicity, and a libm-free source scan. |
| `Proto.DetMath.Tests/DetMathAccuracyMeasurementTests.cs` | 11 ranges × 10^6 inputs: ulp distance to the host `Math.*`, plus ns/call. |
| `Proto.DetMath.Tests/vectors/cr-vectors.txt` | 9,180 correctly rounded reference vectors stored as bit patterns. The oracle is Python `decimal` at 70–130 digits. |
| `Proto.DetMath.Tests/vectors/gen-cr-vectors.py`, `gen-constants.py` | The generators for the vectors and for the split constants (ln 2 in 3 parts, π/2 in 4 parts). |

**Method.** Each function reduces its argument exactly, evaluates the kernel in double-double (about 106 bits), and returns the leading double.

- **Exp:** `k = round(x/ln2)`. The reduction uses a 3-part ln 2. Then `expm1(r/256)` by Taylor series, squared back 8 times. The result is scaled by 2^k built from bits. Subnormal results are rounded once on the 2^-1074 grid, with a residual correction.
- **Log:** exponent and mantissa are split by bits. A 97-entry table `invc = 128/j` is used, with `log(invc)` computed in double-double at type initialisation. The final step is `2·atanh((y-1)/(y+1))`.
- **Pow:** `exp(y·log x)` in double-double. Four exponents are special-cased to the single IEEE operation that is the correctly rounded answer: 1, 2, −1 (`x`, `x*x`, `1/x`) and 0.5 (`sqrt`). Integer exponents 3..64 use exact double-double binary powering. All C99 Annex F special cases are handled.
- **Cos:** Cody–Waite reduction with a 4-part π/2 (about 212 bits), then double-double Taylor kernels. The supported domain is |x| ≤ 2^28. Larger arguments throw rather than return an unverified value; the only campaign caller passes 2πu with u in [0,1).

Only `+ − × ÷`, `Math.Sqrt` (correctly rounded by IEEE-754; it compiles to `sqrtsd`), comparisons, integer conversions and `BitConverter` bit casts are used. There is no other `Math.*`, no FMA, no `float`, and no external table. A test scans the source to enforce this. Every table is computed at type initialisation from those same operations.

## 2. The campaign's call sites (RE-VERIFIED)

`grep -rnE 'Math\.(Exp|Log|Log10|Log2|Pow|Sin|Cos|Tan|Atan|Tanh)\b' Sim.Core` gives 48 lines. One of them is a doc comment (`PriceSystem.cs:50`), so the real count is **47 code lines with 48 calls in 15 files**: Exp 29, Pow 15, Log 3, Cos 1. Sim.Data has none, and there are no Sin/Tan/Atan/Log10/Log2 calls.

recon-determinism.md says "49 call sites in 17 files". That count is **not reproduced**. The file list and the per-function rows in its table do match these 48 calls.

| File (calls) | Fn | Argument range used for the measurement |
|---|---|---|
| HarvestWeatherSystem.cs:208, 218, 219, 300, 306 (5); :324 (Log + Cos) | Exp, Log, Cos | Exp of −dt/τ, lognormal x in about [−5, 5], −distance/range; Log(u1), u1 ∈ [1e-300, 1); Cos(2πu2) |
| DemographicsSystem.cs:184, 374, 381, 382, 423, 426 (6) | Exp | −rate·h, small to moderate negative values |
| Institutions.cs:354, 359, 364, 428, 448, 449 (6) | Exp | −dt/τ, −coverage |
| ResearchQuery.cs:340, 361, 365, 902, 921, 960 (6) | Pow | 1.2^(n−1); (1−g)^n; P^β with β about 0.7 |
| NeedsAggregation.cs:178, 182, 208, 209, 240 (5) | Pow | s^ρ with s ∈ [1e-12, 1]; acc^(1/ρ) |
| ProductionSystem.cs:534, 536, 538 (Log), 553, 555 (5) | Exp, Log | herd logistic closed form |
| MigrationSystem.cs:227, 422, 686 (3) | Exp | −k·deficit·dt, −cost/decay |
| Governance.cs:115, HousingSystem.cs:141, ConsumptionSystem.cs:556, DisasterSystem.cs:104, PriceSystem.cs:152 (5) | Exp | decay and hazard exponents; the price exponent |
| AutoplayMetrics.cs:119 (Log), ResearchContent.cs:1138, 1274 (Pow), UnitProgressionContent.cs:126, 271 (Pow) | — | off the hash path (metrics or load-time checks) |

## 3. Divergence history (DOC-ONLY, citations RE-VERIFIED in this session)

- **CR-013 §2** (`docs/adr/cr-013-cross-platform-determinism.md:29-41`). Seed 42 on the canonical world, commit `98f25c1`, Windows x64 against Linux x64. The hashes are equal at turns 0–1 and differ from turn 2 (`545ca00bd148…` vs `28e62ffb26fb…`). Population, food and settlements are equal on every turn (`:45-47`).
- **CR-013 §8.5** (`:247-259`). The first divergent field at turn 2 is `PriceTerms[43].Consumption`, −2 ulp, with `HarvestWeather` differing in 2 of 12 rows. At turn 3 it is `GoodStocks[154].ProduceRemainder`, 8,192 ulp. Every long column is equal. §8.6 (`:263-271`) classifies this as a "last-ulp transcendental divergence that has already propagated" (HarvestWeather Exp/Log/Cos and the PriceSystem Exp), not FMA. Its line numbers for HarvestWeatherSystem are stale; the current ones are in §2 above.
- **ADR-022 ACCEPTED** (`docs/adr/adr-022-reference-platform-linux-x64.md:1-5`, `:105-125`). Linux x64 is the reference. Option 2 (a deterministic Exp/Sqrt) was "rejected for now, and reopenable", with the cost given as "moving every golden once"; that cost was never measured.
- **First standing surveillance run** (`docs/t4.19-verification-record.md:166-173`, run 34462298577 on `e08111b`). Linux MATCH; Windows DIVERGED-AT-TURN-2; `GoodStocks[154].ProduceRemainder` 32,768 ulp at turn 3.
- **T4.21 cascade** (`docs/t4.21-evidence/cascade-report.md:12`). A 161-turn seed-42 playtest. There are 42 differing leaves, all `economy.goods[].price` (33) or `social.grievance[].value` (9), with a maximum relative difference of 3.67e-16. 0 of 1,932 settlement-turns differ in population.
- **D-058** (`docs/d058-…:181-185`). The Director's 68-turn win-x64 session: its "only divergence remains the ADR-022 platform one (win-x64 against Linux from turn 2)".
- **The surveillance never blocks.** `xplat-surveillance.yml:124` sets `continue-on-error: true` and `:213` ends with `exit 0`. The triggers (`:36-43`) are `main` and `t[0-9]*.*` pushes, a weekly cron and dispatch, so **`claude/**` branches are not covered**. Only seed 42, founded, 300 turns, has ever been compared across operating systems.
- **Governance state.** The Director brief (2026-11-04) says "O-17: Target cross-platform hash equality between Windows and Linux". The uncommitted governance docs still record O-17 as **OPEN**: `docs/d075-m7-unified-warfare-direction.md:447`, `:511`; `docs/adr/adr-060-unified-warfare-simulation-architecture.md:336`, `:378`. ADR-022 is ACCEPTED. Moving to DetMath therefore needs a written CR-013 reopen or an ADR-022 amendment. This lane changed none of those documents.

## 4. Root causes

**(a) MEASURED here: the host libm is not correctly rounded.** On 9,180 inputs against an independent oracle:

| Function | glibc 2.39 correctly rounded | DetMath correctly rounded |
|---|---|---|
| exp | 2012 / 2013 | 2013 / 2013 |
| log | 2508 / 2508 | 2508 / 2508 |
| pow | 2946 / 2951 | 2951 / 2951 |
| cos | 1707 / 1708 | 1708 / 1708 |

In the 10^6-input runs, DetMath and glibc disagree on between 2 and 1,352 inputs per million. I sampled 2,939 of those disagreements (up to 300 per range, all 11 ranges) and classified them against the decimal oracle: **DetMath was correctly rounded in 2,939 of 2,939, and glibc was wrong in 2,939 of 2,939**. By function: exp 837, log 602, pow 900, cos 600. A concrete case that the tests pin: pow(2^52+1, 2.5) is correctly rounded to `0x4810000000000003`, and glibc returns `…02`.

**(b) KNOWN .NET BEHAVIOUR, INFERRED (not measured here).** CoreCLR's `Math.Exp`, `Log`, `Pow` and `Cos` call the platform C runtime: the UCRT on Windows and glibc on Linux. Neither library is correctly rounded, and they make different last-ulp errors. That matches every recorded divergence signature: a 1–3 ulp difference, only in tables fed by transcendentals, then amplified through `ProduceRemainder` and price feedback. Both libraries also select code by CPU at run time (FMA/AVX2 variants), so a divergence between machines on the **same** OS is possible. That has not been measured.

**(c) Ruled out.** RE-VERIFIED or inferred from the measured CR-013 evidence: `Math.Sqrt` (IEEE correctly rounded), JSON and double parsing (managed code and correctly rounded), RNG (integer PCG32), FMA contraction (RyuJIT does not contract `a*b+c`), and `(long)` casts. Turns 0–1, which include worldgen and founding, were byte-identical across operating systems in CR-013 §8.5.

## 5. Measured DetMath accuracy (MEASURED, Linux; test `UlpDistanceToHostLibm_…`)

These figures are ulp distance to the host glibc, 10^6 inputs per range. The assertion is max ≤ 1 ulp. Timings were taken on a loaded 4-CPU host while other lanes were building, so treat them as indicative only.

| range | equal to glibc | max ulp | mean ulp | DetMath ns/call | glibc ns/call |
|---|---|---|---|---|---|
| exp campaign [−50, 5] | 0.999228 | 1 | 7.7e-4 | 469–505 | 10–15 |
| exp small \|x\| ∈ [1e-12, 1] | 0.999763 | 1 | 2.4e-4 | 305–310 | 9 |
| exp full [−745, 709.7] | 0.999219 | 1 | 7.8e-4 | 287–310 | 16 |
| log Box–Muller u ∈ (0,1) | 0.999178 | 1 | 8.2e-4 | 222–293 | 10–11 |
| log ratios [0.5, 4] | 0.999176 | 1 | 8.2e-4 | 226–231 | 8 |
| log wide [1e-300, 1e300] | 0.999998 | 1 | 2.0e-6 | 203–211 | 8–10 |
| pow CES s^ρ, ρ ∈ [−10, 10] | 0.999133 | 1 | 8.7e-4 | 515–604 | 17–18 |
| pow P^β, P ∈ [1, 1e8] | 0.999160 | 1 | 8.4e-4 | 512–563 | 13–17 |
| pow integer exponent 0..200 | 0.999195 | 1 | 8.1e-4 | 373–600 | 13–18 |
| cos Box–Muller 2πu | 0.998648 | 1 | 1.35e-3 | 258–289 | 23 |
| cos wide [−1e5, 1e5] | 0.998653 | 1 | 1.35e-3 | 261–270 | 20–23 |

- **Agreement with glibc:** 99.86–99.9998 % of inputs, never more than 1 ulp apart.
- **Correctness:** 0 mismatches on the 9,180 oracle vectors. The C99 special cases are bit-identical to `Math.*` (26 unary values and a 24×24 pow grid).
- **Cost:** DetMath is 20–40 times slower per call.
- **Not proven correctly rounded.** The working precision is about 2^-100. Published worst cases need about 2^-113 to 2^-118 relative accuracy (Lefèvre–Muller), so a few hard inputs may round wrongly. That would be **deterministic** but not correctly rounded. Determinism does not depend on correct rounding.

## 6. Golden impact (MEASURED in a private copy)

**Setup.**

- The private copy is `m7s1/work/o17-repo/`. It was made with tar using rsync's exclude set, because `rsync` is not installed; it is 146 MB, and 7.2 GB was free.
- An unpatched minimal copy, `m7s1/work/o17-base/` (Sim.Core, Sim.Data, Sim.Cli), has a `Sim.Core` that is `diff -r` identical to the tree.
- The patch replaces all 48 calls with `global::Proto.DetMath.DetMath.*` and adds DetMath as `Sim.Core/Kernel/O17DetMath.cs` and `O17DoubleDouble.cs`. The patch file is `m7s1/work/o17-runs/o17-sim-core-callsites.patch`.
- The command is the one in the ci.yml `determinism-xproc` job (`ci.yml:117`, command at `:166`): `dotnet Sim.Cli.dll run --founded --seed 42 --turns 300 --hash-log …`, with `FOUNDED_GOLDEN` at `ci.yml:266`.

**Results.**

| | Unpatched copy | Patched (DetMath) copy |
|---|---|---|
| final hash (turn 300) | `d5ebd0b2c642ec5c…5306f93501`, **equal to `FOUNDED_GOLDEN`** | `86fbbc1671d792d5adae881f70c4b5c304b9a8b441e589dfce76defbc9f8e82e` |
| wall time, 300 turns | 26.6 s | 28.7 s (+8 %, loaded host) |
| two processes byte-identical | (CI-proven) | **yes**: `cmp` of two 300-line hash logs |

- **Per-turn hash log.** Turns 1–8 are identical. **The first differing turn is 9.** Turns 10, 11 and 14 are identical again, and every other turn from 12 to 300 differs. 11 of 300 lines are equal.
- **First divergent fields.** Measured with `sim diff` and with a scratch tool (`m7s1/work/o17-tools/FieldDiff`) that walks every field of the canonical stream.
  - At turn 9, `HarvestWeather[6].LogDeviation` differs by +1 ulp and `[8]` by −3 ulp, and nothing else differs. This is the Box–Muller and AR(1) weather path, the same system CR-013 named.
  - At turn 12 only `HarvestWeather[9]` differs. Weather deviations are recomputed each turn, which explains why turns 10–11 are equal again.
- **Integer columns never differ.** I compared every Int32, Int64 and Bool field at turns 9, 12, 50, 100, 150, 200, 250 and 300, and **0 differ**. 1 to 6 double fields differ at each turn. At turn 300 these are `Buckets[53,246].MigrationRemainder` (−2 ulp) and `GoodStocks[161,162,185].ProduceRemainder` (−32,768 to −131,072 ulp). On this seed and horizon the Linux→DetMath move therefore has the same shape as the CR-013 Windows→Linux move: doubles only, and no population or stock change.
- **Determinism suite, patched copy** (`dotnet test Sim.Tests/Sim.Tests.csproj -c Release --filter "suite=determinism"`, 24 m 30 s): **62 passed, 16 failed, 78 total**. For comparison, the sprint baseline on the real tree was the full Sim.Tests suite, 0 failed and 3,430 passed (`m7s1/baseline/baseline-build-test.txt`). Since the copy differs only by the patch, the 16 failures are attributed to DetMath. This is INFERRED, because the unpatched determinism filter was not re-run on the copy.

**Pins that move (16 tests, every one a pinned world hash):**

| Test (Sim.Tests.Kernel.…) | Old pin (prefix) | DetMath value (prefix) |
|---|---|---|
| WrapAttributionTests.WithTheCylinderOff_TheFoundedGoldenIsTheBatch2Golden…; GranaryAttributionTests.TheFoundedGolden_IsUnmovedBy…; D054PairAttributionTests…(reference: "founded") | `937577ffb0e7…` | `a9ff174a2ef9…` |
| WrapAttributionTests.WithTheCylinderOff_TheDrivenGoldenIsTheBatch2DrivenGolden; D054Pair…("driven") | `4f4e0b81a588…` | `98fbdedc361a…` |
| WrapAttributionTests.WithTheCylinderOff_TheAiWorldIsTheBatch2AiWorld; D054Pair…("ai") | `b2e4f333c25a…` | `bc37d21d4828…` |
| CylinderAttributionAi1024Tests (3 tests: ReliefAbsence, TheSharedOnArm, Ws1Absence) | `25f4799d581d…` | `9bff484250fd…` |
| AiFoundingAttributionTests.WithTheSwitchOff_…PreS1cWorld…; GranaryAttributionTests.WithTheBatchOff_…De86ace… | `aa7a87a98370…` | `9def8e593568…` |
| AiFoundingAttributionTests.AiWorldTurn300_BothBatch3SwitchesOff_IsTheE91d10bWorld | `2e4759ac494d…` | `0f31a6a3ba1e…` |
| AiFoundingAttributionTests.AiWorldTurn300_WithTheKnownTerrainValuationOff_IsThePreA18World | `5edcc3e08123…` | `d9bca9d70fdc…` |
| AiFoundingAttributionTests.AiWorldTurn300_WithTranche2Off_IsThePreTranche2World | `5fb6aa9e8349…` | `411b34f2ea3e…` |
| AiFoundingAttributionTests.TheM6Slice_IsTheWholeMove_…Batch4World | `55fbce7ebad4…` | `18f6ae1d6be9…` |

**Pins that hold (62 tests):**

- Every twin, replay and conservation harness passes: DeterminismHarnessTests (1,000 turns), FoundedHarnessTests, ResearchDeterminismTests, IntegratedSaveLoadBatteryTests (all 22) and the CivicsPoints identity for seeds 1, 2, 42 and 7.
- **All 13 AiEmpireIntegrationTests "every pin returns" tests pass.** These are turn-exact semantic pins such as levy and A3 turns, so DetMath moves no recorded semantic turn.
- `D054Pair…("toy")` and `("first-reign")` reproduce byte for byte. That the toy pipeline has no transcendental (recon) is consistent with this.

**Not run** (outside the determinism filter, so not measured): SnapshotTests (the `FOUNDED_GOLDEN` literal, which the CLI run above shows moving), DrivenGoldenTests, the other ~150 hash literals, Sim.Ui.Tests session fixtures (including the win-x64 `director-68` recording), the ci.yml allocation-leg sha256 values, and the nightly corridors. **INFERRED:** every hash pin downstream of HarvestWeather moves. The "correctly rounded leaves Linux goldens unchanged" hypothesis (recon §5) is **refuted**: glibc is wrong on about 1 in 1,000 to 1,300 calls in the campaign ranges, and the weather path makes enough calls to reach one by turn 9.

## 7. Mutation testing (MEASURED; private copy `m7s1/work/o17-mut/`; bound 5× the clean run, which took 9.1 s, so 46 s)

| Mutant | Result | Killed by (semantic test first) |
|---|---|---|
| M1 integer-power path disabled | killed | `Pow_ExactMidpointResults_TieDense_RoundToEven` (semantic: an exact midpoint must round to even) |
| M2 pow sign: `negate = yIsInt` | killed | `Pow_BasicOperationIdentities_AreExact` (semantic: an even exponent drops the sign), plus the special-value grid |
| M3 cos quadrant-1 sign dropped | killed | `UnaryFunctions_SpecialValues…` (IEEE values), the ulp ≤ 1 bound, the oracle vectors |
| M4 TwoProd drops `al*bl` | killed | the ulp ≤ 1 bound, the special-value grid, the oracle vectors for all 4 functions |
| M5 subnormal-exp residual correction removed | killed | **only** the oracle vectors for exp. These are correctly rounded values from an independent arbitrary-precision oracle, not values recorded from DetMath. No oracle-free semantic test catches M5. Recorded as a weak kill. |
| M6 log series 8 → 2 terms | killed | the ulp ≤ 1 bound, the special-value grid, the oracle vectors |
| M7 exp drops the Ln2Lo term | **SURVIVED** | The term contributes about 2^-76 relative, so it changes rounding on about 2^-23 of inputs, and the suite's 1–2×10^6 exp inputs per range do not hit one. It is not equivalent, but it is invisible at this density. Recorded as a test-coverage gap. |

No mutant failed to terminate.

## 8. Plan (proposal; each step needs the governance steps named)

1. **Now, zero golden impact:** keep `Proto.DetMath` isolated, as in this sprint.
2. **Windows evidence before any ruling.** Run `Proto.DetMath.Tests` on a `windows-latest` runner, through a dispatch of a new small workflow or a `t*` branch push. The oracle vectors, the identities and the source scan do not depend on the platform, so a green run there **proves per-function bit equality** between Windows and Linux for the tested inputs. Also record the host-libm correct-rounding counts that `HostLibm_AgainstCorrectlyRounded_IsRecorded` prints, which measure the UCRT error rate. This step needs a push or dispatch, which the brief forbids in this sprint.
3. **Governance record.** Reopen CR-013 or amend ADR-022, according to the Director's O-17 ruling, which is recorded as "target equality" in the brief and "OPEN" in D-075/ADR-060. Move DetMath into `Sim.Core/Kernel` behind a switch that defaults to off, with a byte-identity test for the off state. Extend `scripts/check-banned-constructs.sh` to ban `Math.(Exp|Log|Pow|Cos|…)` outside DetMath.cs.
4. **Flip and re-pin once, with one attribution.** Use a single "DetMath re-pin" attribution test whose off twin uses libm. Bump the session or numerics version (contract touch: CanonicalSchema/manifest ADR, D-056 readable-set logic) so that old libm-era sessions are refused clearly rather than failing replay. Re-measure the corridors with the calibration battery. Run `sim bench`: the measured +8 % on the founded CLI run needs confirming on an unloaded host.
5. **Prove equality across operating systems.** Make `windows-founded` blocking (remove `continue-on-error`), extend it to several seeds and the AI leg, and add `claude/**` or dispatch triggers. Only after that run passes can Windows/Linux hash equality be claimed.
6. **Open technical items:**
   - Close the M7 gap with a hard-case vector set (Lefèvre–Muller worst cases).
   - Decide whether a correctly-rounded-by-construction implementation (a CORE-MATH port) is wanted, or whether "deterministic, about 0.5 ulp" is enough. Equality needs only determinism.
   - Cos beyond 2^28 needs Payne–Hanek reduction if a future caller needs it.
   - New M7 battle code is integer-only (O-7a) and needs no DetMath.

## 9. Tests and results

- `dotnet build prototypes/m7-sprint1/M7DetMath.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test prototypes/m7-sprint1/M7DetMath.slnx -c Release --no-build`: **39 passed, 0 failed** (about 4–6 s).
- The `Proto.M7` determinism scan does not apply. DetMath uses `double` by design, because it replaces campaign `double` math; it is not battle code. Its own source scan (no `Math.*` except Sqrt, no `float`, `decimal`, FMA, intrinsics, Random or DateTime) passes.

## 10. Blockers and dependencies

- **Windows equality is UNTESTED.** It needs a push or dispatch, which this sprint forbids.
- The Director must rule on the O-17 governance conflict: the brief says "target equality", while the D-075/ADR-060 docs say OPEN and ADR-022 is ACCEPTED.
- Adopting DetMath moves every hash golden and touches a contract (the session/numerics version). That needs kernel sign-off and an ADR.
- The ns/call figures and the +8 % come from a loaded host, so `sim bench` on a quiet host is needed.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S487** (8 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`O17-detmath.md:11`) — "and this sprint may not push." — in part: DIRECTOR DECISION (DIR5 §1.3) - commits and pushes to the development branch are authorized (never main, no tags, no releases; report commit scope and files first). Retained: the Windows run itself is still to be done and is now required (§3.2 / §6.5); trigger or build path is DELEGATED TECHNICAL; the surveillance workflow still does not trigger on `claude/**`. **Replaced by** DIR5 §1.3, §3.2 (D-079 DR4–DR6, DR35). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`O17-detmath.md:62`) — "Linux x64 is the reference." — in part: DIRECTOR DECISION - Windows is the primary gameplay platform, Linux is a determinism verification platform (not a release target), and Windows/Linux hash equality is required; fix divergence rather than declare Linux unsupported. Retained: the historical description of ADR-022 and of its 'reopenable' Option 2, now reopened through CR-013. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- (`O17-detmath.md:67`) — "Moving to DetMath therefore needs a written CR-013 reopen or an ADR-022 amendment." — in part: CR-013 is reopened by the Director (DIRECTOR DECISION; already D-076 N22); the written record is an ADMINISTRATIVE CONSEQUENCE (§1.1.5). Retained: that the docs were not yet updated when the report was written. **Replaced by** DIR5 §3.2, §1.1.5 (D-079 DR1, DR35). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- (`O17-detmath.md:172`) — "This step needs a push or dispatch, which the brief forbids in this sprint." — in part: DIRECTOR DECISION (DIR5 §1.3) - commits and pushes to the development branch are authorized (never main, no tags, no releases; report commit scope and files first). Retained: the Windows run itself is still to be done and is now required (§3.2 / §6.5); trigger or build path is DELEGATED TECHNICAL. **Replaced by** DIR5 §1.3, §3.2 (D-079 DR4–DR6, DR35). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`O17-detmath.md:173`) — "Reopen CR-013 or amend ADR-022, according to the Director's O-17 ruling, which is recorded as "target equality" in the brief and "OPEN" in D-075/ADR-060." — in part: the 'or' is resolved - reopen CR-013 (DIRECTOR DECISION). Retained: the migration plan (switch defaulting off, off-state byte-identity test, banned-construct extension, one attributed re-pin) as DELEGATED TECHNICAL, subject to the OPEN kernel-change authorization (row :192). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`O17-detmath.md:190`) — "It needs a push or dispatch, which this sprint forbids." — in part: DIRECTOR DECISION (DIR5 §1.3) - commits and pushes to the development branch are authorized (never main, no tags, no releases; report commit scope and files first). Retained: the Windows run itself is still to be done and is now required (§3.2 / §6.5); trigger or build path is DELEGATED TECHNICAL. **Replaced by** DIR5 §1.3, §3.2 (D-079 DR4–DR6, DR35). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`O17-detmath.md:191`) — "The Director must rule on the O-17 governance conflict" — in full: ruled (DIRECTOR DECISION) - reopen CR-013; Windows primary; Linux a verification platform; Windows/Linux hash equality required; investigate and fix divergence; do not dismiss divergence because integer outputs match in a limited sample. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- (`O17-detmath.md:192`) — "That needs kernel sign-off and an ADR." — in part: fixing the divergence is the Director's direction ('Investigate and fix existing divergence'); the ADR is ADMINISTRATIVE (§1.1.5). Retained and OPEN: whether DIR5's bounded authorization (§1.3 prototypes isolated; §10 no silent frozen-contract change) covers moving DetMath into Sim.Core/Kernel and re-pinning every campaign golden now, or only after the architecture review. **Replaced by** DIR5 §3.2, §1.1.5, §1.3, §10 (D-079 DR1, DR4–DR6, DR35, DR53). [ADMINISTRATIVE CONSEQUENCE; OPEN residue]
