# G — Phase C lane T1: deterministic time/event architecture and exact proportional carry

Status: PROTOTYPE, isolated. Built against DIR5 (2026-11-05) §2.4/§2.5 and ADR-061 §3.4–§3.10 (LOCAL-committed).
Base: branch `claude/new-session-yavghm`, HEAD `f98869a`; changes are uncommitted working-tree edits inside
`prototypes/m7-sprint1/` only. Nothing outside the prototype was touched.

Not in scope here, and not done: War Pulse sub-stepping across turns, two-action windows, naval, parity, food (later lanes).

## 1. What was built

| Piece | File | What it does |
|---|---|---|
| Exact rational | `Proto.M7/Time/ExactFraction.cs` | `long` Num/Den. Canonical form: Den > 0, gcd = 1, and `default` = 0/1. Int128 intermediates with cross-reduction. A result that does not fit in `long` is **refused** with `OverflowException`. Operations are pure. |
| Exact carry integrator | `Proto.M7/Time/RateIntegrator.cs` (`IntegrateExact`) | The carry is an exact fraction of one unit in [0, 1). Accrued = ⌊carry + rate·dt/L⌋. Atomic on refusal. |
| Proportional interval carry | `Proto.M7/Time/ProportionalCarry.cs` | e′ = e·L_new/L_old as an exact rational number of ms. Never floored. |
| Superseded rule removed | `Proto.M7/Demo/CampaignDemoWorld.cs` | Slice B's `carry = MulDivFloor(carry, newLen, oldLen)` is deleted. The carry is now an `ExactFraction` and is not rescaled when the turn length changes. The state hash encodes it as (Num, Den). |
| Decision windows | `Proto.M7/Events/Runner.cs` | `DecisionWindow(Index, InstantMs, Hits[])` with one window per instant that lists every significant event at that instant in key order. Windows are recorded whether stops are on or off and during replay. `WindowsHashHex()`. |
| Order gate | `Runner.cs`, `EventScheduler.cs`, `EventTrace.cs` | An order is delivered only at the instant of the latest closed window. Otherwise it is traced as the new outcome `TraceOutcome.Refused = 4` and changes nothing. |
| Rules, stated once | `Proto.M7/Events/EventRules.cs` | R1–R5 below, each one naming the code that enforces it. |

## 2. Design rules (delegated by DIR5 §2.5; ADR-061 §3.4–§3.9)

- **R1 Priority.** Events at one instant run in ascending class order: System 0 < Arrival 10 < Detection 20 < Fire 30 < Damage 40 < Resolve 50 < CampaignProcess 60 < Significant 70 < PlayerOrder 250. Within a class they run by the integer `Priority`. No fractional score is ever used; where a score matters it enters as (score, id).
- **R2 Tie-break.** After class and priority, ties break on `SourceId` (a stable integer, never a hash) and then on `Seq` (scheduler-global and strictly increasing). The key is therefore unique, which gives a total order. Causality: a new key must be greater than the key that is executing.
- **R3 Collisions.**
  - All Fire events at an instant act on the pre-damage world, so a mutual kill is possible.
  - A stale target or source generation causes the event to be traced as `Dropped`.
  - A `Seq` tombstone causes the event to be traced as `Cancelled`.
  - Several significant events at one instant produce **one** stop and **one** window that lists them all.
  - An order at an instant that is not a window instant is traced as `Refused`.
  - An order the world rejects is recorded as refused and changes no state.
- **R4 Interruption.**
  - A window opens at T when an executed event with class < 250 has a kind in `SignificantKinds`. The set of significant kinds is data, and the list itself is OPEN.
  - The window closes before the first key whose time is > T or whose class is ≥ 250.
  - **Orders never open windows**, so windows cannot be chained into extra actions.
  - Whether a turn splits depends on world state only. A stop consumes zero simulated time.
  - Orders are stamped T and execute at T in class 250, after every world event at T.
  - Replay never stops, but it records the same windows.
- **R5 RNG.**
  - Event randomness is counter-based: `CounterRng.Draw(seed, stream, a, b, c, index)`, keyed by stable event identity (source, instant, kind, index).
  - A draw is never keyed by `Seq`, by window index, or by any cursor that a stop or an order could advance.
  - Presentation never draws.

## 3. Carry math and proof

**Rule.** The carry is held in *units*. It is never held as ms over the turn length.

- **Integration.** total = c + ρ·dt/L. The accrued amount is q = ⌊total⌋ and the new carry is c′ = total − q.
- **Changing the turn length** touches nothing, so the in-progress fraction is preserved with zero loss.
- **Same result within a turn.** Inside one turn length the result is identical to Slice B's integer carry, because c = c_int/L.

**Interval form.** For an in-progress interval: e′ = e·L_new/L_old exactly. A round trip returns exactly e, and a chain telescopes to e·L_k/L_0.

**Denominator bound.** The carry's denominator divides the lcm of the turn lengths it has integrated over. For the shipped table that lcm is 10,800 days = 933,120,000,000 ms. The largest denominator observed in the tests was 466,560,000,000.

Proofs (all in `TimeEventsT1Tests`):

- **Round trip over all 36 shipped length pairs × 7 elapsed values (252 cases).** The exact rule is lossless in all 252. The old round-down lost time in **106 of 252**: **245 ms** in total and up to **19 ms** in a single round trip.
- **Telescoping over all 1,630 turns.** Starting from e = 311,039,999,999 ms:
  - Forward, the exact result is 311,039,999,999/20 ms = 15,551,999,999 + 19/20. The legacy rule gives 15,551,999,999.
  - Coming back, the exact rule returns e. The legacy rule returns **e − 19 ms**.
- **Process integration across every band edge.** 180 trials, each with 1–5 random sub-steps per turn and a random rate:
  - Exact: Σq + carry equals the independently summed Σρ·dt/L exactly in every trial.
  - Legacy: lost value in **81 of 180** trials, **11/11,520,000,000 unit** in total. That is below one unit, so the integer totals still agree (0 mismatches).
- **Overflow refusal.** 1/P1 + 1/P2 with P1 = 4,294,967,291 and P2 = 4,294,967,279 throws `OverflowException`. The same happens for `*` and `Scale`. `IntegrateExact` throws and leaves the carry unchanged.
- **3600 d → 1800 d band edge.** The carry (L_old − 7)/L_old survives the edge unchanged. Legacy gave 155,519,999,996/L_new, which loses 1/(2·L_new). Completion lands at turn start + 66,651,428,572 ms. Over a single edge the ceiling hides the loss; that case is proven in the report and pinned in the test.

## 4. PROPOSAL — significant events that open a decision window (for Director approval; NOT decided)

Rules for every entry:

- Each entry is an event kind. A window opens only for polities to which the event is significant **and known under fog**, and AI polities are treated the same way.
- Priority is the in-class `Priority` integer: lower values are listed first in the window.
- No content kind is significant until the Director approves the list.

| Pri | Event kind | Rationale |
|---|---|---|
| 0 | Own city captured, or own last city threatened | Irreversible; losing all cities means the leader is executed (DIR5). |
| 1 | War declared on us, or an observed hostile force enters our territory | A response is needed before the turn boundary. Respects fog (DIR5 §2.4). |
| 2 | Battle begins involving our formations | Hands off to the tactical layer; the player must be able to choose engagement. |
| 3 | Battle concluded involving our formations | Post-battle decisions (pursuit, retreat, garrison). |
| 4 | Ruler death, succession or regency | Changes agency and Action Capacity. |
| 5 | Revolt, secession or civil-war outbreak in our territory | Time-critical internal threat. |
| 6 | Wonder-class construction completed | The canonical DIR5 example (Taj Mahal). |
| 7 | Famine onset (a settlement's food stock reaches 0) or epidemic outbreak | Fast-moving welfare loss. |
| 8 | Peace offer, ultimatum or call to arms that carries an in-turn deadline | Only deadline-bearing items; the broader diplomacy belongs to M8. |
| 9 | First contact with a new polity | Opens new verbs. |

Proposed **not** significant (they resolve at the turn boundary): routine builds, unit training, research or civic completion, population thresholds, trade changes, per-pulse economic settlements, and any unobserved distant war.

## 5. Tests and counts (measured this session)

- **Debug** (before → after): 335 → **347** passed, 0 failed.
  - +12 new tests in `TimeEventsT1Tests`.
  - 2 existing tests rewritten and 1 existing test renamed in `VerificationFixTests` (no change in count).
- **Release**: **347** passed, 0 failed. I did not measure a Release baseline before the changes.
- **Builds**: Debug and Release both finished with 0 warnings and 0 errors (`M7Proto.slnx`, Bench included).
- **Determinism scan**: `scripts/check-proto-determinism.sh` scanned 44 files under Proto.M7/. Result: **CLEAN**, 0 finding groups.

New pins:

- **Window order delivered at T** (1200 days = 103,680,000,000 ms):
  - It runs after every world event at T and before the next instant.
  - The rushed process completes at **291,600,000,000** ms.
  - The same order replayed 1 ms late or 1 ms early is **Refused**, and the state equals that of a run with no order.
- **Tie-dense window.** 72 events at one instant, 57 of them significant, at least 40 exact ties. Two scheduling permutations give the same (class, priority, source) order, and the exact ties resolve by `Seq`. Windows and trace are identical with stops on and off.
- **RNG.** Draws are identical whether the window is shown, stops are off, or the run is replayed. Draws for a process unrelated to an order are unchanged by that order. Negative control: draws keyed by `Seq` **do** differ between live and replay, which is why R5 bans that key.
- **T1 golden** (one band edge, 4 windows at 77,552,640,000,000 / 77,630,400,000,000 / 77,740,560,000,000 / 77,826,651,428,572 ms, 3 orders, one of them refused by the world). Replay reproduces every window, and the trace and state.
  - trace `E8EA92A0…E979`
  - state `A74B4B47…2FD8`
  - windows `60428AC0…9DB7`
  - log `A6781058…D2E4`
  - These are Linux x64 measurements.

Mutants were run against the filtered classes (Runner, VerificationFix, T1). Each run was bounded by a 300 s timeout, about 9× the 33 s full-suite baseline, and finished in about 8 s. All three were killed by a semantic test:

- **MF** (restore the floor rescale): killed by the carry-preservation test and by the golden.
- **MG** (gate always delivers): killed by both off-by-1-ms refusal tests.
- **MW** (record windows only when stopping): killed by 6 tests, including replay and RNG.

## 6. Moved goldens

`RunnerTests.Live_interrupted_scenario_hashes_are_pinned`:

- **State hash** moved from `41191E3543CE36CD9C4CF36BD7FA912E09711C201AA9AD17DD4252CD7B7BC0F2` to `2E3D9E5BED96B0FE1353E592B0B899EDCFD9559217676CB09A062745FB7EE4B7`.
- **Cause:** the encoding change only. The carry is now written as (Num, Den) instead of an integer remainder over L.
- **Proof:** the test re-encodes the new final state in Slice B's format and asserts the **old** golden exactly. Turns 0–3 have no band change, so the conversion is exact.
- **Unchanged:** the trace hash `10BBF4EE…6E48` and the log hash `8617BDA7…5864`.

No other golden moved.

## 7. Limitations / open items

- The runner accepts only window orders. Boundary orders and War Pulse orders, which are the other two delivery semantics in ADR-061 §3.7, are not implemented or pinned here.
- Windows are world-level. Per-polity, fog-filtered window recipients (ADR-061 §3.6) are not modelled.
- The exact carry covers linear per-turn rates. The campaign pipeline stays turn-atomic, and nonlinear integrations and locality (C21) are untouched.
- ADR-061 T4 specifies a fixed denominator D_T. This lane uses a *reduced* rational per the lane brief. The values are equal, and the denominator stays ≤ D_T. A table whose lcm does not fit in `long` is refused at runtime with `OverflowException`, not by the loader.
- `Replay_comparison_has_teeth_an_order_moved_to_the_turn_end_changes_the_hashes` still passes, but now because the moved orders are **Refused** by the gate. Before this change they were applied late.

## 8. Files changed

- **Added:**
  - `Proto.M7/Time/ExactFraction.cs`
  - `Proto.M7/Time/ProportionalCarry.cs`
  - `Proto.M7/Events/EventRules.cs`
  - `Proto.M7.Tests/TimeEventsT1Tests.cs`
  - `reports/G-time-events-t1.md`
- **Modified:**
  - `Proto.M7/Time/RateIntegrator.cs`
  - `Proto.M7/Demo/CampaignDemoWorld.cs`
  - `Proto.M7/Events/Runner.cs`
  - `Proto.M7/Events/EventScheduler.cs`
  - `Proto.M7/Events/EventTrace.cs`
  - `Proto.M7.Tests/RunnerTests.cs`
  - `Proto.M7.Tests/VerificationFixTests.cs`
