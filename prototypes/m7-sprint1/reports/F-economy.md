# Slice F — economy audit, logistics prototype, calibration (M7 Sprint 1, implementation-spec §6)

This is an isolated, reversible prototype. Deleting `Proto.M7.Econ/`, `Proto.M7.Econ.Tests/` and `M7Econ.slnx` removes it completely. It does not decide O-14, O-30, O-35, O-9 or O-21.

## What exists

| Path | What it is |
|---|---|
| `M7Econ.slnx` | Solution with Proto.M7.Econ and Proto.M7.Econ.Tests only |
| `Proto.M7.Econ/EconWorld.cs` | The shipped configs and the canonical founded world. Founded with `WorldFounding.Found` and stepped with `TurnExecutor` without orders, which is the FOUNDED golden recipe |
| `Proto.M7.Econ/MovementFood.cs` | `MovementParams`: every number is read from sim.json, goods.json, worldgen.json or the kernel, never typed in. `MovementFood` computes route, days, rations and haul over the real lattice (`Pathfinder.FindPath`), plus days on a road class |
| `Proto.M7.Econ/LogisticsPriority.cs` | `LogisticsPriority.Resolve(free, civilianRequirement, growthHeadroomDemand, moves)`. It cuts growth first, then shortens or denies moves, and never lets a military draw come out of the civilian requirement. Pure integer code with a decision record and exact conservation |
| `Proto.M7.Econ/PolityMeasure.cs` | Per-polity snapshot read from the state tables: people, adults, soldiers, stocks, last-step produced/eaten, civilian requirement, `FoodHeadroom.Vacancy`, UnfedUnits |
| `Proto.M7.Econ/Calibration.cs`, `Program.cs` | Headless calibration command: baseline checkpoints, movement tables, allocator on measured numbers, and recruitment counterfactual twins (RecruitFormation orders under the existing §6.5 predicate) |
| `Proto.M7.Econ/MeasuredAllocation.cs` | (verification fix) Measured numbers → allocator inputs under an explicit `AllocatorReading`; used by `Program.Run` |
| `Proto.M7.Econ.Tests/` | 34 tests: allocator (11), movement (7), world/neutrality (5), verification fixes (11, `EconVerificationFixTests.cs`) |

Sim.* is referenced read-only (`ProjectReference ../../../Sim.Core`, `../../../Sim.Data`). No Sim.* file, Sim.slnx, CI file, script or doc was changed. Evidence: `git status --short` shows the 21 pre-existing governance docs and `?? prototypes/`; `git diff --stat` over Sim.*, scripts, .github and Sim.slnx is empty.

## Results

- **Build** (`dotnet build prototypes/m7-sprint1/M7Econ.slnx -c Debug|Release -nologo --disable-build-servers`): 0 warnings, 0 errors (TreatWarningsAsErrors on).
- **Tests:** `Passed! Failed: 0, Passed: 23, Skipped: 0, Total: 23`, in both Debug and Release, 1 m 44 s. The run is dominated by the 300-turn golden test.
- **Golden neutrality:**
  - `Recipe_ReproducesThePinnedFoundedGolden_Seed42Turn300` reproduces `d5ebd0b2…3501` = `SnapshotTests.FoundedGoldenHash` = ci.yml `FOUNDED_GOLDEN`.
  - `PrototypeQueries_DoNotChangeTheWorld` shows the world hash is unchanged after every query.
  - The calibration run passed through the same hash at turn 300.
- **Determinism scan** (CLAUDE.md law 5 patterns over `Proto.M7.Econ*/*.cs`): no float, Random, DateTime, Stopwatch, Dictionary or HashSet, GetHashCode or Parallel.
  - The only hits are `Environment.TickCount64` in `Calibration.cs`/`Program.cs`. They measure wall time for the report and are never fed into a world.
  - The spec §1 strict pattern set (no double either) run over `LogisticsPriority.cs` finds nothing.
  - Sim-facing code uses double only where Sim.Core stores rates as doubles (Law 7). Every stock returned is a long (ceil).
- **Mutants:** 8 of 8 killed, each by at least one semantic test. Run in a private copy at `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7s1/work/econ/mut/`. Bound: 5 × the 12 s clean filtered run, i.e. 60 s. No hang.

| Mutant | Killed by |
|---|---|
| M1: growth served before military | Stage1_GrowthIsCutBeforeAnyMoveIsTouched, Stage2, Property_Sweep, Unconstrained |
| M2: military draws from the civilian requirement | CivilianShortfall_…, Property_Sweep, Stage1/2, NotOneDayFits, Unconstrained |
| M3: shortened days rounded up | Shortened_GrantIsAtMostTheRemainder…, NotOneDayFits, TieDense |
| M4: (priority, id) tie-break dropped | TieDense_EqualPrioritiesAndDraws_OrderIsByIdRegardlessOfInputOrder |
| M5: haul ignores route cost | FromRouteCost_PinnedArithmetic, Bill_IsMonotone… |
| M6: 365.25-day ration year | FromRouteCost_PinnedArithmetic, Bill_IsMonotone… |
| M7: adults counted with elders | Snapshot_IsAnExactSumOfTheTables |
| M8: growth not zeroed after a restriction | Property_Sweep_ConservationCivilianFirstAndMonotoneInFreeGrain |

- **Performance** (4 CPUs, shared with other agents):
  - Founding a canonical world takes about 3 s. One step takes about 0.35 s up to turn 200.
  - The full 4-seed calibration took 7 m 35 s in Release: 200 turns × 4 seeds, plus 2 × 3 twins × 10 turns per seed.
  - The seed-42 run to turn 500 took 2 m 53 s.
  - A movement table (11 A* routes) is cheap compared with a step.

## Findings (numbers in `m7s1/reports/economy-prototype-and-calibration.md`)

1. The economy audit re-verified recon-economy.md against the code. Corrections: `DemographicsSystem.cs:150` (not :162), and the "no grain in identity inputs" claim, which holds only for army formations. New findings:
   - `TransportQuery.EstimateFreight` is the existing travel-time hook.
   - `FoodHeadroom.Vacancy` is public and is the growth-headroom quantity.
   - Two year lengths exist: `SimClock.YearDays` 360 and `TransportQuery.DaysPerYear` 365.25.
2. Baseline, measured on HEAD (seeds 1, 2, 42, 7):
   - Population is about 6.1–6.5 k at turn 25 and about 23–24.5 k at turn 200. Seed 42 reaches 86.6 k at turn 500.
   - There is no starvation in any measured baseline step.
   - The grain store holds about 1 year of civilian demand.
   - Food produced is 2.5–3.7 per adult-year.
3. **The marginal food cost of a soldier is about 0, except lost labour.** Measured with real RecruitFormation twins:
   - Lost harvest is 1.3–1.9 grain per soldier-year.
   - About 3.8 births are lost per soldier per century.
   - The upkeep exactly replaces what the soldier would have eaten as a civilian.
4. **Existing rules do not constrain force size.** Only "at least one adult stays" binds. The support check (1 year of upkeep against a 10-year step) never bound. The rules accepted 36–56 % of the population as soldiers, including 48,720 at turn 500. When the store empties, upkeep goes to `UnfedUnits`, which nothing consumes: 960 soldiers at turn 25 leave 33–37 k units unfed per century with no consequence.
5. Candidate mechanisms derived from these measurements (none adopted):
   - **C1:** a dt-consistent support check, i.e. the store must cover one step of upkeep. That gives 645–690 soldiers at turn 25 and about 2.5–2.6 k at turn 200. At turn 500, 50 k soldiers would need about 2.6 × the store.
   - **C2:** a consumer for `UnfedUnits` (this is O-35).
   - **C3:** labour loss, which already exists.
   - **C4:** a surplus bound. Not recommended.
6. Movement food:
   - Marches last 2–43 days and the terrain factor is 0.15–1.18. Rivers and paths make routes cheaper; slope makes them dearer.
   - The haul grows with distance², so for a 50 k column it dwarfs the rations (up to about 102 k haul against 6 k rations).
   - Only the haul is an incremental cost, because the rations are upkeep the soldier eats anyway.
   - Road classes are strictly faster in the shipped order.
7. On measured turn numbers the allocator cut growth first in all 9 cases. In 2 of them (seeds 1 and 7 at turn 25) it also shortened the 50 k move. Civilians were never short. **Superseded in part (verification F1):** these verdicts hold only under the as-shipped input reading; see "Verification and fixes" below.

## Decisions taken in this slice (prototype-local, labelled)

- Haul basis: FULL and MEAN are both computed and reported. No default; the choice is open.
- Days per year: 360, the kernel clock that integrates upkeep. The 365.25 discrepancy is recorded as open.
- Allocator period = one turn. Inputs: free = stock + last harvest (**PROTOTYPE ASSUMPTION**), R = last requirement, G = Vacancy × dt.
- A restricted period gives growth 0, and the sub-day residue stays in store.
- Moves are ordered by (Priority, Id).
- After a move is restricted, every later move is denied.

## Blockers and dependencies for integration (not resolved here)

- **Jungle/forest terrain classes are NOT-FOUND.** The "jungle dearer than open" ordering cannot be shown without a terrain layer or invented thresholds.
- **O-30 / ADR-019 §1.2:** an intra-turn economic causal chain (per-pulse food) is forbidden.
- **O-35:** the consequence of unfed soldiers.
- **O-9:** off-road movement versus the D-009 graph.
- **O-21:** siege starvation.
- **O-14:** where the framework lives.
- **The open rule under which military draw may starve civilians:** not implemented.
- Any Sim.* hook (the `FoodHeadroom` seam, a movement query or table, a support-check change) moves the founded goldens, because the founding warband eats every step. It also needs an ADR-055 amendment.
- No movement order exists (`OrderKind` 1–18). Formations are always stationed at a settlement.

## How to run

```
dotnet build prototypes/m7-sprint1/M7Econ.slnx -c Release -nologo --disable-build-servers
dotnet test  prototypes/m7-sprint1/M7Econ.slnx -c Release --no-build -nologo
dotnet prototypes/m7-sprint1/Proto.M7.Econ/bin/Release/net10.0/Proto.M7.Econ.dll \
    --seeds 1,2,42,7 --turns 0,1,25,50,100,200 --twin-at 25,200 --twin-horizon 10 --out calibration.md
```

## Verification and fixes (fixer pass, 2026-11-04)

Each finding was reproduced in a private copy (`m7s1/work/fixF/repo`, Sim.* byte-identical to the dev tree) before anything was changed. The fixes were then copied into this folder. Sim.*, docs and every file outside `prototypes/m7-sprint1/` are unchanged.

| Id | Verdict | Evidence |
|---|---|---|
| F1 (major) | **FIXED** (reproduced) | The verifier's P7 probe re-run on the unchanged code gave the same numbers. Seed 7: shipped MilitaryRestricted 37,175 / R = grain eaten GrowthReduced 43,490 / free − spoilage MilitaryRestricted 13,490. Seeds 2 and 42 flip from GrowthReduced to MilitaryRestricted net of spoilage (19,189 and 23,846). The fix makes the input reading explicit. New `MeasuredAllocation` + `AllocatorReading` with three flags: NetOfSpoilage, GrainOnlyRequirement, HaulOnlyDraw. `Program.Run` now prints an "Allocator input sensitivity" table at every twin turn (5 readings; n/a if the world has more than one polity, because the ledger is world-level). The as-shipped row is byte-identical to the first run's rows (4 seeds, turn 25, checked). The table is in `m7s1/reports/economy-prototype-and-calibration.md` §6 ("Correction after verification"). No reading is adopted: this is an open modelling choice. Tests: `F1_AsShipped_…`, `F1_Readings_ChangeExactlyTheirOwnInput`, `F1_TheVerdictOnMeasuredNumbers_DependsOnTheReading` (3 seeds; pins the flips). |
| F2 (minor) | **FIXED** (reproduced) | Before: `F2_Rations_AreTheExactCeiling_OverAnIntegerGrid` failed with "46 mismatches; first personnel=225 cost=44 got=12 exact=11", and `F2_TheReportedCase_…` failed with Expected 11, Actual 12. Fix: in `MovementFood.FromRouteCost`, rations and haul are now evaluated in `System.Decimal`. The products are exact, there is a single final division, and the 15-significant-digit conversion of each double input is used. After: both tests pass, and the haul guard (FULL and MEAN, exact) passes. Pinned values in the older tests are unchanged (12/20/10, 1112/17792, 2223/71136). The calibration's turn-25 move draws are unchanged too. `decimal` is not in the CLAUDE.md law-5 list. It is software-implemented, so it is platform-deterministic. Only the spec §1 strict set bans it, and that set applies to `Proto.M7/`, not this sim-facing lane. |
| F3 (major) | **FIXED** (reproduced) | Before (`mutate.sh before …`): V3, V4 and V6 each gave rc=0, Passed 23/23. New semantic tests: `V3_SoldiersArePerPolity_OnATwoPolityWorld` (a real `WorldFounding.Found` with `aiEmpires 1`, where each polity must report 20, not 40). `V6_AnInfiniteVacancy_IsFlaggedUnbounded_NotAddedToTheHeadroom` (turn-0 world, where `FoodHeadroom.Vacancy` is +∞ on its null arm). `V4_ProgramRun_AllocatorRow_…` is the first test of `Program.Run` (seed 42, turn 1, 1-turn twins, about 2 s), together with the F1 input tests. The mutant table is below. |
| F4 (nit) | **FIXED** (reproduced) | `grep -n` on `MilitarySystem.cs` gives 104, 110, 113, 115 and 118. `m7s1/reports/economy-audit.md` row H2 and §"Military movement second" now cite :104, :110, :113 and :115. :118 was already correct. |

**Mutants** (private copy; each run is bounded by `timeout 300`, which is 10 × the roughly 30 s clean filtered run; no hang; source restored after each run):

| Mutant | Before fix | After fix: killed by (semantic) |
|---|---|---|
| V3: drop the owner filter on soldiers | survived (23/23) | `V3_SoldiersArePerPolity_OnATwoPolityWorld` |
| V4: free = grain stock only (now in `MeasuredAllocation.Inputs`) | survived (23/23) | `F1_AsShipped_…`, `F1_Readings_…`, `V4_ProgramRun_…` |
| V4b: `Program.Run` resolves under a non-shipped reading | n/a (new code) | `V4_ProgramRun_…` |
| V6: never set unbounded (+∞ added to headroom) | survived (23/23) | `V6_AnInfiniteVacancy_…` |
| MF2: rations back to the double chain | n/a (new code) | `F2_Rations_…`, `F2_TheReportedCase_…` |
| MR1: NetOfSpoilage ignored | n/a (new code) | `F1_Readings_ChangeExactlyTheirOwnInput` |
| MR2: HaulOnlyDraw ignored | n/a (new code) | `F1_Readings_ChangeExactlyTheirOwnInput` |

Logs: `m7s1/work/fixF/mut/*.test.txt`. Script: `m7s1/work/fixF/tools/mutate.sh`.

**Re-run on this folder after the fixes:**
- `dotnet build prototypes/m7-sprint1/M7Econ.slnx -c Release|Debug -nologo --disable-build-servers`: 0 warnings, 0 errors.
- `dotnet test prototypes/m7-sprint1/M7Econ.slnx -c Release --no-build -nologo`: Passed 34, Failed 0, Total 34, 1 m 36 s.
- `scripts/check-proto-determinism.sh`: CLEAN (it scans `Proto.M7/` only).
- The law-5 grep over `Proto.M7.Econ*` has the same hits as before: only `Environment.TickCount64` wall time in Calibration and Program.
- The 4-seed turn-25 calibration with 1-turn twins took 23 s.

**Still open (not decided here):**
- Which allocator reading is right. Free net of spoilage and/or overflow? An all-food or grain-only R? Should rations count toward the draw?
- `MeasuredAllocation.Inputs` gives an unbounded headroom a growth demand of 0. That behaviour is unchanged from the first pass, is not tested as correct, and is a question for the same modelling decision.
- The world-ledger readings are valid only on single-polity worlds.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S486** (6 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`F-economy.md:23`) — "`Passed! Failed: 0, Passed: 23, Skipped: 0, Total: 23`, in both Debug and Release" — in part (stale count, not a ruling): DIR5 §2.11 requires reconciling this 23 with the consolidated 34/34. 23 is the first-pass, pre-verification count (Debug 09:36, Release 09:44, 2026-11-04); the verification fix stage added `EconVerificationFixTests.cs` (11 cases), and this report's later sections (:16, :136) record 34, run in Release only. Phase B measured the current count on 2026-11-05: **34 passed in Debug and 34 in each of two Release runs**, identical outcomes (`…/scratchpad/m7gov/work/phaseB-build-audit.md` §4, §6). Retained: 23 as the historical first-pass count. **Replaced by** DIR5 §2.11 (last paragraph), §8 Phase B (D-079 DR26–DR28, DR51). [ADMINISTRATIVE CONSEQUENCE]
- (`F-economy.md:69`) — "**C2:** a consumer for `UnfedUnits` (this is O-35)." — in full as to O-35: DIRECTOR DECISION (DIR5 §2.9) - 'Soldier attrition and mortality belong to M7.14'. Retained: C2 as a candidate design input for M7.14 (not adopted), under the automatic priority of §2.11. **Replaced by** DIR5 §2.9, §2.11 (D-079 DR23, DR26–DR28). [DIRECTOR DECISION]
- (`F-economy.md:83`) — "Allocator period = one turn." — in part: DIRECTOR DECISION (DIR5 §2.4) - economic effects occur per operational War Pulse, not exclusively at campaign-turn boundaries, so a one-turn allocation period cannot be the integrated design. Retained: the prototype-local assumption as history; the per-pulse allocator is DELEGATED TECHNICAL; the food-allocation ordering stays unselected (§2.11). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`F-economy.md:91`) — "**O-30 / ADR-019 §1.2:** an intra-turn economic causal chain (per-pulse food) is forbidden." — in full: O-30 closed as option (b) - 'Economic effects occur per operational pulse, not exclusively at campaign-turn boundaries' (DIRECTOR DECISION, DIR5 §2.4); ADR-019 §1.2 is superseded in part (ADMINISTRATIVE, ADR lane). Retained: the historical citation. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- (`F-economy.md:92`) — "**O-35:** the consequence of unfed soldiers." — in part: the packet is decided (attrition and mortality in M7.14) and the automatic priority is fixed (growth reduced first, movement restricted next, starvation last resort; not player-controlled). Retained: the exact UnfedUnits consequence is DELEGATED design inside M7.14, informed by the food-allocation comparison the Director has not decided. **Replaced by** DIR5 §2.9, §2.11 (D-079 DR23, DR26–DR28). [DELEGATED TECHNICAL DECISION]
- (`F-economy.md:95`) — "**O-14:** where the framework lives." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
