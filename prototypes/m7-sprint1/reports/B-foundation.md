# Slice B — deterministic time + event foundation (M7 Sprint 1)

Spec: `m7s1/implementation-spec.md` §0, §1, §2 (scratchpad). Repo `/home/user/m5w/dev`, branch
`claude/new-session-yavghm`, HEAD `7d5defc`. Everything is under `prototypes/m7-sprint1/`. Nothing outside the folder
was created, changed or deleted: `git status` shows the pre-existing docs changes plus `?? prototypes/` only. No git
state was changed. `Sim.slnx` was never built.

## 1. What exists

| area | files (`Proto.M7/`) | notes |
|---|---|---|
| skeleton (§0) | `../Directory.Build.props`, `../README.md`, `../M7Proto.slnx`, `../scripts/check-proto-determinism.sh`, `../fixtures/era-pacing.json` | The props are self-contained and do not import the root props. `CheckForOverflowUnderflow=true` applies to `Proto.M7` only. The fixture is a byte copy (`cmp` identical, and a test asserts it). `M7Econ.slnx` and the Econ projects belong to the Slice F agent; I did not touch them. |
| Fixed (§2.1) | `Fixed/FixedMath.cs`, `Fixed/Q32.cs` | `MulDivFloor`/`MulDivCeil` (Int128, exact; throw on overflow), `DivFloor`/`DivCeil`/`ModFloor` (correct for negative operands), `ISqrt(ulong)` and `ISqrt(UInt128)` (bitwise, exact floor). `Q32` is a `long` raw with 32 fractional bits. |
| Time (§2.2) | `Time/SimInstant.cs`, `Time/ExactNumberText.cs`, `Time/CampaignCalendar.cs`, `Time/RateIntegrator.cs`, `Time/WarPulseSchedule.cs` | `SimInstant(long Ms)`, `MsPerDay = 86_400_000`, 360-day year. `CampaignCalendar` reads the fixture with `Utf8JsonReader`. dtYears comes from the raw number-token text through exact rational arithmetic, so no IEEE type is used. It applies the same rules as the kernel `EraTableLoader`. API: `TurnStart`, `TurnEnd`, `TurnLengthMs`, `TurnOf`, `PositionInTurn`, `At(turn,num,den)`, `BandIndexAt`. |
| Events (§2.3) | `Events/EventKey.cs`, `EventHeap.cs`, `EventScheduler.cs`, `EventTrace.cs`, `TombstoneSet.cs`, `SignificantKinds.cs` | The key is `(TimeMs, Class, Priority, SourceId, Seq)`. `Seq` is global and monotonic. Ordering uses an explicit binary min-heap. Classes are 0/10/20/30/40/50/60/70/250, with the reasons in the `EventClass` doc. Liveness is checked by generation against Target **and** Source; a stale event is traced as `Dropped`. Cancellation uses a bit-array tombstone keyed by `Seq`. The SHA-256 trace hash is computed over a canonical little-endian encoding. |
| Runner (§2.4) | `Events/Runner.cs`, `Events/SubTurnOrderLog.cs` | `RunUntil(limit)` returns `ReachedLimit` or `Interrupted(at)`. `IssueOrder` works only while stopped. `CreateReplay` pre-schedules the logged orders and never stops. |
| Demo worlds | `Demo/CampaignDemoWorld.cs`, `Demo/PulseDemoWorld.cs`, `Demo/DemoKinds.cs` | Synthetic `ConstructionProcess`: no wonders exist in the repo (recon-critic A12). The demo also has units for simultaneity and invalidation, the orders Rush and StartProcess, and war pulses with a fixed-step tick and an action budget refreshed per pulse. |
| RNG (§2.6) | `Rng/CounterRng.cs` | `Draw(seed, stream, a, b, c, index)` folds its inputs through the SplitMix64 finalizer. The constants are copied from `Sim.Core/Kernel/SplitMix64.cs`, and a test pins them, including Vigna's reference output. Also provides `DrawAttempt`, unbiased `NextBelow`, and `Chance(Q32)`. This is the only file that uses `unchecked`. |
| tests | `Proto.M7.Tests/` (7 files) | 99 tests. |
| bench | `Proto.M7.Bench/Program.cs` | Measures heap, steady scheduler and the campaign demo. |

## 2. Design decisions taken under delegated authority (O-6)

1. **Time quantum: 1 ms (decided), on a global instant.** A turn is the half-open interval `[TurnStart(t), TurnStart(t+1))`. Drivers run a turn with `RunUntil(TurnEnd-1)`; the limit is inclusive.
2. **Total order.** The key is lexicographic `(Time, Class, Priority, SourceId, Seq)` and `Seq` is unique, so equal keys cannot occur.
   - **Causality rule:** a newly scheduled key must be greater than the key that is executing. This makes the executed key sequence strictly increasing, so no phase inversion can happen at an instant (for example, a Fire scheduled after Damage at the same T is rejected).
   - **Reserved class:** `PlayerOrder` (250) can be reached only through the Runner.
3. **Simultaneity through classes.**
   - The order at one instant is Fire, then Damage, then Resolve.
   - A shot is not cancelled when its shooter dies at the same instant, because deaths resolve after all damage.
   - At different instants, a shot whose shooter has died is dropped (the Source generation is stale).
4. **"Significant" is a set of event kinds held as data** (`SignificantKinds`), independent of class.
   - The stop rule is: finish every event at instant T with class < 250, then stop. Several significant events at one instant produce one stop.
   - Interrupts are recorded on the runner, not in the trace, because a replay does not stop.
5. **The trace hash excludes the raw `Seq`.** Execution order is captured by position in the trace. A live run and its replay allocate `Seq` differently; a test asserts that the values really differ, yet both hashes are equal. The proof is in the `Runner` doc:
   - (i) only orders occupy class 250;
   - (ii) orders are ordered by SourceId, which is the log index;
   - (iii) the relative `Seq` order of world events is preserved under a monotone shift.
6. **RNG retries use an attempt fold instead of `index+k`.** This deviates from the spec's wording: `index+k` would alias a consumer's next index. `DrawAttempt(…, 0)` equals `Draw`.
7. **Construction completion is computed exactly:** `t = anchor + ceil((R·L − carry)/U)`, with Int128 intermediates. The sub-unit carry at a boundary between turns of different lengths is rescaled with `floor(carry·Lnew/Lold)`. That is deterministic and less than 1 unit, but it is a choice: see Open question 4.
8. **War-pulse length is looked up by the band that contains the pulse's start instant.** The band is computed from the date (Law 4), the same mechanism as kernel dt. A pulse that crosses a band keeps the length of the band it started in.

## 3. Placeholders (all labelled in code)

- War-Pulse lengths of 7 days for the pre-1920 bands and 1 day after: **PLACEHOLDER**, taken from REV:463's wording ("weeks … days"). O-2 is decided only as "era-varying".
- Internal step of 1 day: **PLACEHOLDER**.
- Action budget of 2: **PLACEHOLDER**. Whether it is 2 or 3 is OPEN.
- The construction numbers (1000 of 3000 units per turn, and so on) are synthetic test inputs, not content.
- `WarPulseSchedule` ships no default table; the caller must supply one.

## 4. Tests and results

- `dotnet build prototypes/m7-sprint1/M7Proto.slnx -c Release -nologo --disable-build-servers`: **0 warnings, 0 errors**.
- `dotnet test … --no-build`: **Passed 99, Failed 0, Skipped 0** (about 0.5–1 s).
  - Per class: FixedMath 43, Calendar 19, Runner 12, Scheduler 11, CounterRng 11, WarPulse 3. Theory rows are counted individually.
- `scripts/check-proto-determinism.sh`: **CLEAN** (19 files, comments included). To check that the scan has teeth, I injected a file containing the floating type, `Dictionary<` and `unchecked` into a scratch copy: exit 1, 3 findings.

Coverage of every §2 item:

- **Golden table, independent of platform.** The MulDiv, ISqrt (64- and 128-bit), Q32 and CounterRng values were computed independently with Python big integers (`m7s1/work/B/goldens.py`). Property tests also check MulDiv floor and ceil against their defining inequalities, and ISqrt at k² and k²±1 and at ulong.MaxValue.
- **Calendar.**
  - 1630 turns; the dt set is {3600, 1800, 1080, 720, 360, 180}.
  - The turns tile the timeline exactly.
  - Dt is the dt of the band at turn start, at band edges.
  - A third of every band's turn is exactly 1200/600/360/240/120/60 days.
  - `TurnOf`/`PositionInTurn`/`At` round-trip at all 1630 turns.
  - Rejected inputs: exponent notation, dt 0.001 (0.36 days), dt 1.001 (truncation guard), misaligned bands, gaps.
- **Heap tie-dense test.** 6000 events with about 167 per (Time, Class, Priority, SourceId) prefix, inserted in 5 permuted orders, are checked against an independent LINQ multi-key sort.
- **Other scheduler checks:** class order at one instant; causality violations; mutual kill (trace shows Fire, Fire, Damage, Damage, Resolve, all at T); sequential shots (the dead shooter's shot is dropped); stale generation leads to Dropped; cancellation; and the trace hash covering every field except Seq.
- **Taj Mahal literal test.** In a 10-year turn, a wonder with 1000 of 3000 units left stops the runner at exactly `turnStart + L/3 = 1200 days`. The world's `Now` is that instant. The player issues an order, and it is delivered at that instant.
- **Instant-exact delivery pin.** The order's effect is stamped T. The order event runs at T in class 250. The rushed process completes at the literal `291_600_000_000` ms, which is inside turn 0. If the order were delivered at turn end or 1 ms late, that would be impossible.
- **Replay.**
  - Live run: 3 interrupts with orders in 3 places, including one refused order. The replay gives an equal state hash and an equal trace hash, while the raw Seqs differ.
  - Teeth: the same orders moved to the turn end change both hashes.
  - The live scenario's hashes are pinned as goldens. They were **measured on Linux** (x64, .NET 10). Equality on Windows is **UNTESTED**.
- **Piecewise linear integration.** Splitting a turn at the interrupt or at 200 random sets of cut points gives the same total and the same carry as one pass. An interrupted and an uninterrupted 3-turn run give identical state and trace hashes.
- **War pulse.**
  - Pulse 0 = [3598 d, 3605 d) crosses the boundary at 3600 d.
  - The trace and state hashes are identical for one span, for turn-by-turn runs, and for a calendar with entirely different (1-year) turn boundaries.
  - Each pulse starts once and ends once. Ticks fall at start + i·step. The budget is refreshed at pulse start and not at the turn boundary. Pulse length is keyed on the start band (Industrial → Modern).
- **RNG.**
  - 10k draws give the same values in 3 shuffled orders, with unrelated draws interleaved.
  - Inputs are position-sensitive.
  - Chi-square: 10 buckets × 100k draws, below 27.877 (p = 0.001).
  - `NextBelow` at the worst case, modulus 2^63+1: about half the draws are rejected and the results stay unbiased.
  - `Chance` returns true for 1, false for 0, and about 1/3 for 1/3.

## 5. Mutation testing

The mutants ran in a private copy (`m7s1/work/B/mut`; driver `m7s1/work/B/mutants.py`; logs `mutants.log` and `mutants-rerun.log`). A clean build and test takes 23 s, so the bound was 115 s (5×). No mutant hung. **14 of 14 were killed, each by at least one semantic (non-golden) test:**

| # | mutant | killing semantic test(s) |
|---|---|---|
| M1 | Fire resolves damage and death inline (no phase split) | Mutual_kill_at_the_same_instant_both_die, Replay… |
| M2 | order delivered 1 ms after T | Order_issued_at_the_interrupt…, Taj_Mahal…, Orders_can_only_be_issued… |
| M3 | MulDivFloor loses the negative floor correction | MulDiv_satisfies_the_defining_floor_and_ceil_inequalities |
| M4 | EventKey ignores Priority | Heap_extraction…tie_dense…, EventKey_is_lexicographic… |
| M5 | liveness ignores generation | Stale_generation_events_are_dropped_and_traced |
| M6 | runner stops right after the significant event | Runner_finishes_the_interrupt_instant… |
| M7 | RNG does not fold `c` | Inputs_are_position_sensitive |
| M8 | integral check removed, so values truncate | Exact_scaling_rejects_non_integral_products… |
| M9 | trace hash includes Seq | Replay_with_pre_scheduled_orders…, Trace_hash_covers_everything_but_seq |
| M10 | integrator drops the carry | Linear_rate_integration_is_split_invariant…, Order_issued… |
| M11 | cancellation tombstone ignored | Cancellation_by_id_prevents_execution_and_is_traced |
| M12 | budget refreshed only for the first pulse | Action_budget_is_refreshed_at_pulse_start… |
| M13 | ISqrt(ulong) starting bit too low | ISqrt_floor_property_at_perfect_squares_plus_minus_one |
| M14 | causality check removed | Causality_no_event_may_be_scheduled_behind… |

Notes on the mutation run:

- **M8 survived the first run.** A dt of 0.001 years truncated to 0 days and was still rejected, but by the `dtDays <= 0` guard rather than the integral check. I added `Exact_scaling_rejects_non_integral_products_instead_of_truncating` (dt 1.001 must not become 360 days), and M8 is now killed.
- **M8, M11 and M14 were re-run with conditions that compile**, because `if (false)` triggers CS0162 under TreatWarningsAsErrors.
- **What the M11 kill does and does not show.** A Rush order cancels the process's pending event, but if that cancellation is skipped the stale event is still dropped by liveness, because the process has already completed. So cancellation is load-bearing only in its direct test and in the golden.

## 6. Performance (`Proto.M7.Bench`, Release; Linux x64, 4 CPUs; median of 5)

- **Raw heap, 2,000,000 tie-dense events pushed and then popped:** 2278 ms, which is **0.88 M events/s** (1.76 M heap-ops/s). A 2M-entry heap of 88-byte structs is cache-bound, and the time includes a counter-RNG draw per event.
- **Steady-state scheduler** (4,096 self-rescheduling sources, so the heap holds about 4k entries; 2,000,000 events):
  - **4.86 M events/s** with the trace off;
  - **3.27 M events/s** with the trace on.
- **Campaign demo**, all 1630 turns × 1000 construction processes with interrupts on: **530 ms**, 865,281 events and 777 interrupts.

## 7. Dependencies and blockers for integration (each needs a Director or kernel decision)

1. **Kernel and OrderRecord.** Real mid-turn hand-back needs changes to `TurnExecutor.Step`, a sub-turn coordinate (best appended, not placed in `SimClock`), `OrderRecord` or a companion log, `OrderLog.BatchFor`, `OrderValidation`, the replay artefacts, and a schema bump, with every golden re-pinned (recon-kernel §7 items 1–13). The brief forbids changing these without authorization. `SubTurnOrderLog` is prototype-local.
2. **ADR-019 §1.2/§1.4** forbid an economic (non-war) interrupt like the Taj Mahal case. It is an equal-rank blocker (recon-critic X11).
3. **Nonlinear campaign integration is not piecewise-invariant.** Examples are demography's Exp and compounding. Sub-stepping the campaign pipeline at interrupts would move results and re-pin goldens (CR-006). The prototype keeps the campaign turn-atomic and integrates only linear processes. This is a kernel dependency.
4. **RNG integration.** The campaign requires `RngRegistry` with its state in `WorldState`. The path is a battle seed derived from a `RngRegistry` stream, followed by counter draws. It needs O-14 and kernel sign-off.
5. **O-14:** where the scheduler lives is undecided. This code is a prototype and not the framework.

## 8. Open Director questions (not answered here)

1. Which event kinds are significant (G1). Only the Taj Mahal example is given, and the code treats significance as data.
2. War-Pulse length per era (O-2 values) and the internal step length.
3. The action budget: 2 or 3; what the decision interval is (G2: pulse or interrupt span); and how it maps to ledger §17.2/§19 Action Capacity and `actionCapacityShare`.
4. Whether a carry at a turn boundary between bands should be rescaled (current choice, deterministic, less than 1 unit) or dropped. This matters only once real construction is integrated.

## 9. Verification and fixes (verify-B-foundation round, 2026-11-04)

Each finding was reproduced on `prototypes/m7-sprint1` before any change (ADR-015 §6). Evidence logs are in
`m7s1/work/fixB/` (scratchpad): `prefix-tests.log` (new tests against the unfixed code), `postfix-tests.log`,
`mutants-original-suite.log`, `mutants.log`, `scanprobe-extra.log`. The regression tests are in
`Proto.M7.Tests/VerificationFixTests.cs` (24 test cases). Every expectation in them is derived independently with
Int128 arithmetic from first principles; none is read back from the code under test.

**Before the fixes**, against the unfixed code: 123 tests, **11 failed**. These were the F6–F10a reproductions:
- the 3 invalid StartProcess rows, which threw ArgumentOutOfRange;
- the Rush overflow, which threw OverflowException;
- the 2 campaign-end war rows and the run-past-the-end pulse, which threw ArgumentOutOfRange('t');
- ModFloor, which threw OverflowException;
- classes 251 and 255, which threw nothing;
- the empty bands list, which threw ArgumentOutOfRange instead of FormatException.

The F1–F4 tests passed on the clean code, as they should: those findings are test gaps, and their "before" is the
mutant table below.

**After the fixes:** **Passed 123, Failed 0** (99 original + 24 new). All original goldens are unchanged, including the
replay state and trace hash literals.

| id | verdict | evidence | change |
|---|---|---|---|
| F1 | **FIXED** (test gap) | MD survived all 99 original tests (`mutants-original-suite.log`). | `Completion_instant_is_the_least_instant_whose_integer_accrual_reaches_R` checks 5 rows, each with R·L mod U ≠ 0 (asserted), and covers carry across a turn boundary and odd start offsets. It asserts the exact instant start + ceil(R·L/U), that accrual reaches R there, and that it is below R one ms earlier. `Completion_instant_literal_pin_for_a_non_dividing_rate` pins 44_434_285_715 ms (L/7, remainder 2). |
| F2 | **FIXED** (test gap) | MH survived the original suite. | `Carry_is_rescaled_by_floor_at_the_3600_to_1800_day_band_boundary…` derives the carry independently (L−7 → floor((L−7)/2)) and the completion instant, and asserts both. `Processes_crossing_every_band_boundary_keep_carry_below_the_turn_length` runs 40 random processes across each of the 5 band changes. No code change: the rescale rule itself is unchanged (Open question 4 still stands). |
| F3 | **FIXED** (wrong aim) | MC survived the original suite. At n = 2^63+1, x mod n is nearly uniform. | `NextBelow_is_unbiased_at_a_three_quarter_range_modulus` uses n = 3·2^62 and requires P(v < 2^62) ≈ 1/3 (the biased value is 1/2, about 27 sd away). The misleading comment in `CounterRngTests` is corrected: that test now states it is a range check only. |
| F4 | **FIXED** (test gap) | MK survived the original suite. | `War_pulses_are_contiguous_and_each_takes_the_length_of_its_start_band` uses PLACEHOLDER distinct lengths per band and a war that crosses band 0 → 1. It asserts: Index = i; Start(i) = End(i−1); and length = the table entry of the start band. |
| F5 | **FIXED** | Verifier probe tree: the old script reported `CLEAN`, exit 0. The new script reports 9 finding groups, exit 1. | See the scan changes listed after this table. |
| F6 | **FIXED** | Reproduced: ArgumentOutOfRange / OverflowException escaped `Runner.RunUntil`. | `ExecuteOrder` now validates before acting. StartProcess requires A > 0 and B ≥ 0. Rush additionally requires B ≤ long.MaxValue − UnitsPerTurn. An invalid order is recorded as `OrderEffect(Accepted=false)` at the order instant and leaves the state unchanged. Tests: `Invalid_start_process_order_is_recorded_as_refused` (3 rows), `Rush_order_whose_rate_would_overflow…`, and `Valid_start_process_order_is_still_accepted`. |
| F7 | **FIXED** | Reproduced: `BandIndexAt` threw on `Next` at CampaignEnd. | `WarPulseSchedule.HasNext(p)` = p.EndMs < CampaignEnd, documented as "no pulse starts at or after the campaign end; a pulse may run past it". `PulseDemoWorld` checks stopAfterMs and HasNext **before** calling `Next`. Tests: stopped at the end, unbounded, and a 7-day pulse that starts before the end and ends 4 d after it. |
| F8 | **FIXED** | Reproduced: OverflowException. | `ModFloor` now uses the truncating remainder plus a sign correction. b = −1 returns 0, because `long.MinValue % -1` throws in .NET. The correction r + b cannot overflow because the signs are opposite. Test: an exhaustive 13×13 grid of extreme operands against an Int128 reference, plus the three probe literals and `IsWholeDay(long.MinValue)`. |
| F9 | **FIXED** | Reproduced: `(EventClass)255` was accepted. | `Schedule` rejects every class ≥ PlayerOrder (250). Tests: 250, 251 and 255 throw and leave nothing pending; 70 and 249 are still accepted. |
| F10a | **FIXED** | Reproduced: ArgumentOutOfRange from `raw[0]`. | `Build` throws `FormatException("'bands' is empty")`. Test: `Empty_band_list_is_a_format_error`. |
| F10b | **REFUTED as a defect** (no change) | Confirmed as described. | The completion event is scheduled in class CampaignProcess (60). It is significant through the data set `SignificantKinds`, and spec §2.4 itself requires significance as data. The interrupt is pinned by the Taj Mahal and literal-pin tests. This is design decision #4. |

**Scan changes for F5.** The script `scripts/check-proto-determinism.sh` now:
- bans `System.Double/Single/Half/Decimal` and the bare BCL type names;
- matches `Math . X` with whitespace tolerance, and bans `using static System.Math`;
- bans `\bRandom\b`, `RandomNumberGenerator`, `Guid.NewGuid`, `TimeProvider`, `HashCode`, `ThreadPool` and `Task.Run`;
- bans the unordered containers by name (`Dictionary<`, `HashSet<`, `Hashtable`, Concurrent*, Frozen*, Immutable*);
- bans `ToHashSet`, `ToDictionary`, `ToLookup` and the other `To…` conversions;
- bans every LINQ ordering spelling (`OrderByDescending`, `Order`, `OrderDescending`, `ThenByDescending` and the rest);
- allows `unchecked` only at the exact path `Proto.M7/Rng/CounterRng.cs`;
- flags `CheckForOverflowUnderflow` set to anything but `true` in any csproj, props or targets file under `Proto.M7/`, and in the root `Directory.Build.targets`;
- flags any nested `Directory.Build.props` or `Directory.Build.targets` under `Proto.M7/`.

Additional teeth check (`scanprobe-extra.log`): a copy of the real code plus a file with `Half`, `Single`, `Decimal`, `new System.Random`, `HashCode.Combine`, `OrderDescending`, `ThenBy (`, `Dictionary <`, `Task.Run`, an `unchecked` block and a nested props file gives exit 1 with 8 groups. The real `Rng/CounterRng.cs` is still allowed. On the real tree the result is **CLEAN** (19 files).

**Mutant table.** The mutants ran in a private copy under `m7s1/work/fixB/mut*`, created by the driver `fixB/mutants.py`. Clean build plus test took 14–18 s, so each mutant was bounded at 5× that (72–91 s). No mutant hung. "Original" means the 99 pre-existing tests; "now" means the 123-test suite.

| id | mutation | original suite | now: killing semantic test(s) |
|---|---|---|---|
| MC | `NextBelow` returns x % n with no rejection | SURVIVED | NextBelow_is_unbiased_at_a_three_quarter_range_modulus |
| MD | completion instant uses floor(need/U) | SURVIVED | Completion_instant_is_the_least_instant… (5 rows), Completion_instant_literal_pin… |
| MH | carry not rescaled at a band change | SURVIVED | Carry_is_rescaled_by_floor_at_the_3600_to_1800…, Processes_crossing_every_band_boundary… |
| MK | next pulse starts at EndMs + 1 | SURVIVED | War_pulses_are_contiguous…, War_running_to_the_campaign_end… (2), A_pulse_started_before_the_campaign_end… |
| R6 | F6 fix reverted (StartProcess validation off) | n/a | Invalid_start_process_order_is_recorded_as_refused (3) |
| R7 | F7 fix weakened (`&&` changed to `\|\|`) | n/a | War_running_to_the_campaign_end… (2), A_pulse_started_before_the_campaign_end… |
| R8 | F8 fix reverted (ModFloor via b·DivFloor) | n/a | ModFloor_is_exact_over_extreme_operands |
| R9 | F9 fix reverted (only == 250 rejected) | n/a | World_events_cannot_use_a_class_at_or_after_PlayerOrder (251, 255) |

**Build and bench after the fixes.**
- Build of `M7Proto.slnx` (Release): **0 warnings, 0 errors**.
- Bench (Release, median of 5):
  - heap: 0.99 M events/s;
  - steady scheduler: 5.57 M events/s with the trace off, 4.14 M events/s with it on;
  - campaign demo: 424.9 ms, 865,281 events, 777 interrupts.
- These are in the same range as §6. The only change on a hot path is one byte comparison in `Schedule`.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S482** (9 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`B-foundation.md:40`) — "rescaled with `floor(carry·Lnew/Lold)`. That is deterministic and less than 1 unit, but it is a choice: see Open question 4." — in part: the status 'it is a choice' (Open question 4: rescale or drop) is closed by DIRECTOR DECISION - 'Preserve fractional time proportionally when campaign-turn duration changes. Do not discard leftover time.' Retained: the proportional rescale itself, which is the DIR5 rule. DELEGATED TECHNICAL: whether the floor residue (< 1 numerator unit, i.e. < 1 ms-equivalent of work) must be carried exactly so that nothing is discarded. **Replaced by** DIR5 §2.5 (D-079 DR15–DR18). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`B-foundation.md:47`) — "Action budget of 2: **PLACEHOLDER**. Whether it is 2 or 3 is OPEN." — in full as to '2 or 3 is OPEN' and 'PLACEHOLDER': DIRECTOR DECISION - maximum two actions per decision window. Retained: the value 2, now a Director maximum; legal combinations and Action Capacity integration are DELEGATED TECHNICAL. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`B-foundation.md:125`) — "The brief forbids changing these without authorization." — in part: whether mid-turn hand-back at non-integer time with re-pinned goldens is wanted is now a DIRECTOR DECISION (DIR5 §2.5 hybrid event-driven time and decision windows; §2.4 re-pin every golden); writing the kernel ADR (ADR-061 / A-13) is an ADMINISTRATIVE CONSEQUENCE needing no renewed approval (§1.1.5). Retained and OPEN: DIR5 §1.3 authorizes bounded, isolated prototype work and §10 forbids silent frozen-contract change; DIR5 does not say whether changing Sim.Core/Kernel (TurnExecutor, OrderRecord, schema) is authorized now or only after the architecture review. **Replaced by** DIR5 §2.5, §2.4, §1.1.5, §1.3, §10 (D-079 DR1, DR4–DR6, DR11–DR18, DR53). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE; OPEN residue]
- (`B-foundation.md:126`) — "forbid an economic (non-war) interrupt like the Taj Mahal case. It is an equal-rank blocker (recon-critic X11)." — in full as a blocker: DIRECTOR DECISION - the Taj Mahal completion one-third into a turn is DIR5's canonical decision-window example; ADR-019 §1.2/§1.4 are superseded for it (already recorded 2026-11-04, see M7-full-report:65). Retained: the historical fact that ADR-019 said so. **Replaced by** DIR5 §2.5 (canonical example), §2.4 (D-079 DR11–DR18). [DIRECTOR DECISION]
- (`B-foundation.md:127`) — "The prototype keeps the campaign turn-atomic and integrates only linear processes. This is a kernel dependency." — in part: the turn-atomic campaign stance is superseded - economic effects occur per operational War Pulse, turns can be interrupted at non-integer time, War Pulses cross turn boundaries with sub-stepping, and every golden is re-pinned (DIRECTOR DECISION). Retained: the finding that nonlinear integration (Exp, compounding) is not piecewise-invariant; how to sub-step it deterministically is DELEGATED TECHNICAL (§2.5); kernel authorization as in row :125 (OPEN). **Replaced by** DIR5 §2.4, §2.5 (D-079 DR11–DR18). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION; OPEN residue]
- (`B-foundation.md:128`) — "It needs O-14 and kernel sign-off." — in part: the O-14 dependency is narrowed - Sim.Battle is the Director's chosen direction (not created until the architecture review); RNG-consumption rules are DELEGATED TECHNICAL (§2.5). Retained: RngRegistry with state in WorldState is a frozen contract; any change needs an ADR (ADMINISTRATIVE) and, for the kernel, the OPEN authorization of row :125. **Replaced by** DIR5 §1.3, §2.5 (D-079 DR4–DR6, DR15–DR18). [ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- (`B-foundation.md:129`) — "**O-14:** where the scheduler lives is undecided." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL); the placement of the campaign-turn interrupt scheduler (which also serves non-war events) is not addressed by DIR5 and belongs in the Phase D project-boundary recommendation. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`B-foundation.md:135`) — "The action budget: 2 or 3; what the decision interval is (G2: pulse or interrupt span); and how it maps to ledger §17.2/§19 Action Capacity and `actionCapacityShare`." — in part: '2 or 3' closed by DIRECTOR DECISION (maximum two per decision window). The decision-interval definition and the mapping to Action Capacity / actionCapacityShare become DELEGATED TECHNICAL ('You own the detailed legal-action rules'), constrained by 'Do not replace or ignore Action Capacity'. **Replaced by** DIR5 §2.8, §2.5 (D-079 DR15–DR18, DR22). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`B-foundation.md:136`) — "Whether a carry at a turn boundary between bands should be rescaled (current choice, deterministic, less than 1 unit) or dropped." — in full: question closed by DIRECTOR DECISION - preserve fractional time proportionally, do not discard leftover time (rescale, never drop). DELEGATED TECHNICAL: exactness of the rescale (see :40). **Replaced by** DIR5 §2.5 (D-079 DR15–DR18). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
