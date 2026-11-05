# G — Phase C lane T2: War Pulse sub-stepping, locality under fog, per-pulse economics, boundary order delivery

Date: 2026-11-05. Tree: `/home/user/m5w/dev`, branch `claude/new-session-yavghm`, base HEAD `f0fc281` (lane T1), with
uncommitted T2 changes. ISOLATED PROTOTYPE: only `prototypes/m7-sprint1` was changed. Nothing under `Sim.*` changed, and
no git command that changes state was run.

Authority: DIR5 §2.4 (four clocks; War Pulses era-varying, local, under fog; "Use sub-stepping"; "Economic effects
occur per operational pulse"; durations "Propose and test"). ADR-061 §3.10–§3.11 and §9. D-079 DR11–DR14, O-29 (a),
O-30 (b).

Not in scope here, and not done: two-action windows, naval, parity, food, siege, Action Capacity in the war world, and
kernel integration.

## 1. What was built

| File | Role |
|---|---|
| `Proto.M7/War/WarTheaterWorld.cs` | An `IEventWorld` with these parts: <br>• each war's own pulse clock; <br>• the straddle split at turn boundaries; <br>• per-pulse economics; <br>• per-polity knowledge and window recipients; <br>• the two boundary order semantics. <br>It also holds `WarKinds`, `WarSpec`, the record types and `WarSession` (stops only for the driving polity's windows). |
| `Proto.M7/War/WarLedger.cs` | A stand-in for the kernel Ledger. Conserved `long` accounts and an all-or-nothing `Transfer` between accounts of the same commodity. Consumption goes to sink accounts. |
| `Proto.M7/War/WarFog.cs` | Observers (units or cities) and the visibility predicate. |
| `Proto.M7.Tests/WarPulseT2Tests.cs` | 22 tests: 16 facts, plus 3 theories with 3 cases each. |

T1 code is reused unchanged:
- `WarPulseSchedule`: the start-band pulse length.
- `ExactFraction` and `ProportionalCarry.ElapsedFraction`: the split progress.
- `RateIntegrator.IntegrateExact`: the economics.
- `Runner`, `EventScheduler` and `SubTurnOrderLog`: windows, the order gate and replay.

No T1 file was edited.

## 2. Design: sub-stepping across turn boundaries

The events at one instant X all have class System. Ties between wars are broken by SourceId, which is the war id (R2).

| Priority | Event | Effect |
|---|---|---|
| 0 | `PulseEnd(w)` | Settles the last sub-step `[LastSettled, X)`. Asserts that the pulse's elapsed fraction is exactly 1. Schedules the next pulse. |
| 1 | `TurnStart` | **The straddle split.** It settles `[LastSettled, X)` for every war whose pulse began before X and ends after X. It then latches pending turn orders and snapshots the ledger. |
| 2 | `PulseStart(w)` | Latches pending pulse orders and schedules the pulse end. |

A pulse keeps the length of the band that contains its start, including when it straddles a band edge (ADR-061 §3.11).
Each war's phase is its own, set by the war's start instant.

## 3. Carry proof (time)

Take a pulse `[B, B+W)`. Its sub-step endpoints are `B = x0 < … < xk = B+W`. The interior points are exactly the turn
starts S with `B < S < B+W`.

- **Lengths.** The part lengths are integers. They sum to `xk − x0 = W`, a telescoping sum with no rounding.
- **Elapsed fraction.** The elapsed fraction after part j is `Σ(xi − xi−1)/W = (xj − B)/W`, an exact canonical fraction.
- **Runtime checks.** The code checks two things and throws if either fails:
  - at every split, the fraction must equal `ProportionalCarry.ElapsedFraction(xj − B, W)`;
  - at every pulse end, it must equal 1.
- **No gaps or overlaps.** Pulses are contiguous, so sub-steps tile `[warStart, lastEnd)`. No millisecond is lost or
  counted twice.
- **Era change.** Band edges are turn starts, so a pulse that straddles an edge is split there like at any other
  boundary. Nothing is re-denominated, so nothing can be lost.

The tests check all of this: the exact split records, the tiling, the sum per turn equal to the turn length for every
turn of a war that runs the whole campaign, and the era-change case.

## 4. Locality and fog rule

- **Clocks.** Each war has its own pulse clock. Two wars do not affect each other's pulses, sub-steps or economics; the
  test runs a war with and without a second war and compares.
- **Visibility (`WarFog`; a prototype simplification).** Polity P sees war W at instant t when either holds:
  - P is a participant of W; or
  - an observer of P is present at t (`From ≤ t < Until`) and W's theatre cell is inside its sight disk:
    `dx² + dy² ≤ r²`, in exact Int128 arithmetic.

  A cell inside a present observer's sight is explored by construction. Not modelled: line of sight, explored-set
  memory, detection chance. A moving observer would be a chain of presence intervals.
- **Knowledge.** A polity's knowledge log receives a pulse start or end only if the polity can see the war at that
  instant.
- **Window recipients (`WindowsFor`).** Each world window is filtered, per polity, to the hits that polity can see.
  Empty windows are dropped. World windows stay a function of world state (T1 R4).
- **Stops (`WarSession`).** The session stops only at windows that the driving polity receives.
- **Order gate.** An order from a polity that did not receive the window at T is executed as a recorded refusal
  (`NotARecipient`). It changes no state.
- **C19 tests.**
  - Fog metamorphic: the views of polities 0, 1, 4 and 5 (knowledge, received windows, balances) are hash-identical with
    the distant war present or absent.
  - Isolated polity: polity 5 has no knowledge and no window.
  - The presence boundary is exact to ±1 ms, and the sight radius is checked at the edge (distance exactly r is in
    sight; radius r − 1 is not).

## 5. Per-pulse economics and the conservation proof

**Rates.** Each sub-step of length dt settles every participant in ascending id order:

- `due = IntegrateExact(ratePerYear, dt, MsPerYear, ref carry)`, where `dtYears = dt / MsPerYear` is exact;
- `paid = min(due, balance)`, moved by `Transfer` to a sink account;
- any shortfall is recorded as arrears.

The prototype has two quantities:
- upkeep: money per sim-year per war, set per turn;
- supply: goods per sim-year, set per pulse.

**Integer total over any partition.** Rates change only at pulse or turn starts, and both are sub-step boundaries.
`IntegrateExact` keeps the invariant `Σq + c = Σ r·dt / Y` with the carry c in [0, 1). Hence
`Σq = ⌊Σ r·dt / Y⌋`, whatever the partition. The carry is held in units, not milliseconds, so a change of turn length or
era leaves it unchanged (ADR-061 T3).

**Conservation.** Every movement is a same-commodity `Transfer`, so the total of each commodity is constant.

The tests check this by exact equality, against independent BigInteger closed forms:
- every half day across a straddle and an era change;
- at a mid-turn pulse end;
- with partial payment;
- over a war that lasts the whole campaign: about 212,000 pulses, with the ledger checked at the end.

## 6. Order delivery semantics (pinned)

Orders arrive only at a window instant T, in class PlayerOrder, after every world event at T.

| Semantic | Rule | Pins (offset → effect) |
|---|---|---|
| Window order | At T itself (T1, unchanged) | T1 pins |
| **War Pulse order** (`OrderSupplyRate`) | At the first pulse start B of that war with **B ≥ T** | B3−1 → pulse 3 at B3; B3 → pulse 3 at B3; B3+1 → pulse 4 at B4 |
| **Turn order** (`OrderUpkeepRate`) | At the first turn start S with **S ≥ T** | S1−1 → turn 1; S1 → turn 1; S1+1 → turn 2 at S2 |

**Why `≥` rather than `>`.** No simulated time passes between a pulse start and an order issued at that instant, so the
order can apply to the current pulse without settling anything retroactively. This also serves DIR5 §2.5: "act on
significant events before the next normal turn boundary".

**What the pins check.** Each pin checks:
- the `LatchRecord`: effective instant and effective pulse or turn index;
- the exact final ledger;
- the pulse it missed, or, for turn orders, the boundary snapshot. The snapshot shows that the part before S1 was always
  settled at the old rate: the split happens before the latch.

**Replay.** Live play with three boundary orders is reproduced by replay: the trace, state and windows hashes are equal.
Without the orders, the state differs and the windows are the same (teeth).

## 7. PROPOSED War Pulse durations (for Director approval — NOT decided)

**Principle.** A pulse is about the time a field force needs for one operational move, such as crossing one region or
completing one supply cycle. That time shortens with movement technology: foot and ox-cart, then rail, then motor
vehicles and aircraft. It runs from weeks in early eras to days in modern ones (REV:463). This keeps pulses per turn
roughly level, between 90 and 180, as dt shrinks 20×.

The key is the band containing the pulse's start instant. This is a stand-in for the computed Age state (D-079 R2).

| Band (shipped table) | Turn | Proposed pulse | Ratio | Measured pulse starts per turn (min–max) | Pulses in band |
|---|---|---|---|---|---|
| Neolithic (−4000…−1500) | 3600 d | **21 d** (3 wk; seasonal foot campaigns) | 171.4 | 171–172 | 42,858 |
| Bronze/Iron (−1500…500) | 1800 d | **14 d** (2 wk; road-borne armies) | 128.6 | 128–129 | 51,428 |
| Medieval (500…1400) | 1080 d | **10 d** | 108 | 107–108 | 32,399 |
| Early Modern (1400…1800) | 720 d | **7 d** (1 wk; magazine logistics) | 102.9 | 102–103 | 20,572 |
| Industrial (1800…1920) | 360 d | **3 d** (rail and telegraph) | 120 | 119–120 | 14,399 |
| Modern (1920…2000) | 180 d | **2 d** (motor and air) | 90 | 90–90 | 14,400 |
| Information+ (2000…2100) | 180 d | **1 d** | 180 | 179–180 | 35,999 |

**How these were measured.** One war ran from campaign day 0 to the campaign end with these lengths. Each pulse start was
counted in its turn. In every interior turn of a band the count is the floor or the ceiling of the ratio. The test
asserts this. Where the minimum is below the floor, the cause is the first turn of a band: it receives a straddling
pulse that keeps the previous band's length.

**Cost.** The whole-campaign war is about 212,000 pulses and 1,630 turn starts. It runs in about 2 s in Debug, including
the test's checks.

## 8. Tests and counts (measured this session)

| Configuration | Before | After |
|---|---|---|
| Debug | 347 passed, 0 failed (measured before any change) | **369** passed, 0 failed |
| Release | 347 (T1 report). Measured again here with the T2 tests filtered out: 347 passed, 0 failed | **369** passed, 0 failed |

- Build: Debug and Release, **0 warnings and 0 errors** (`TreatWarningsAsErrors`).
- The +22 tests are all in `WarPulseT2Tests`.
- Tie-dense ordering test: 8 wars whose pulse starts coincide. At each coincident instant the newest war's start was
  scheduled first (lowest Seq), yet starts and ends execute in ascending war id. A shared treasury runs dry, and the wars
  that go short always form a suffix in war-id order. A permuted declaration order gives identical trace and state
  hashes.

## 9. Determinism scan

`scripts/check-proto-determinism.sh` reports **CLEAN**: 47 files under `Proto.M7/`, 0 pattern groups with findings.

## 10. Mutants

Each mutant was run against the full Debug suite and bounded at 400 s. That is 10× the clean suite: about 40 s wall time,
37 s test duration. The source was restored by copy and checked by SHA-256 after each run.

All four mutants were killed and none hung. Each run took 45–48 s, including the build. Every kill below is an assertion
failure, not an exception.

| Mutant | Change | Failed (of 369) | Semantic kills (selected) |
|---|---|---|---|
| **M1** | Drop the straddle split. The crossing guard is removed too, so the mutant is coherent: the whole pulse settles at its end and is charged to its start turn. | 9 | <ul><li>`StraddlingPulse…`: 4 sub-steps expected.</li><li>`SubSteps_Tile…`: a sub-step lies outside its turn.</li><li>`TurnOrder…` ×3: the snapshot at S1 should hold 30.</li><li>`EraChangeMidWar…`, the whole-campaign tiling and the golden.</li></ul> |
| **M2** | Economics per turn instead of per pulse. Time is accrued and integrated only at turn-boundary splits. | 14 | <ul><li>`Economics_SettleAtEachPulseEnd_ObservableMidTurn`: the ledger at a mid-turn pulse end.</li><li>`Conservation_HoldsExactly…`.</li><li>`PulseOrder…` ×3.</li><li>`TurnOrder…` ×3.</li><li>`TieDense…`, `EraChange…`, crossing invariance, whole campaign and the golden.</li></ul> |
| **M3** | Fog off: every polity sees every war. | 6 | <ul><li>`FogMetamorphic…`: a non-participant's view changes.</li><li>`ObserverPresence…` ×3.</li><li>`WindowRecipients…`.</li><li>The golden.</li></ul> |
| **M4** | Strict pulse latch: an order issued at the pulse-start instant waits for the next pulse. | 3 | <ul><li>`PulseOrder…(offset 0)`: the effective pulse.</li><li>`WindowRecipients…`: the immediate rate.</li><li>The golden.</li></ul> |

Each property these tests assert is one the system ought to have (ADR-015 §7.2):
- a turn boundary is a settlement point for every pulse that straddles it (DIR5 "sub-stepping");
- economics settle per pulse (DIR5 §2.4);
- a distant war cannot be detected by anyone who cannot see it (DIR5 fog);
- the `≥` delivery rule (§6).

The M1 failure of the crossing-invariance test is a structural assertion (sub-step counts differ). It is not counted as
a semantic kill.

## 11. Goldens

- **Moved goldens: none.** All 347 pre-existing tests pass unchanged, including the T1 golden and the
  `PulseDemoWorld`/`CampaignDemoWorld` pins. No existing world was edited.
- **New golden** (`T2Golden_TraceAndStateHashes`; 50 events, 24 sub-steps, two wars across the Neolithic → Bronze/Iron
  edge, fog, 3 boundary orders):
  - trace `39BFB0B9278DB2EAD5A8610E4729461DB312B7CCA06C9235631F67B720FA3961`
  - state `10F9370F7046BE080BF41096B89441EF1C131035862660045497667D1F2818D2`
- DIR5's "every golden replay must be re-pinned" applies at kernel integration. The split moves war economics to the
  boundary instant, so every campaign golden with an active war will move then.

## 12. Limitations and open questions

**Open questions for the Director:**
1. The proposed durations (§7), or the principle behind them.
2. The delivery rule `≥` (an order issued exactly at a pulse or turn start applies to that pulse or turn) versus a
   strict `>`.
3. Era change: a straddling pulse keeps its start-band length (ADR-061 §3.11). The alternative is to re-denominate the
   remainder proportionally to the new band's length (`ProportionalCarry` would keep that exact too).
4. Whether observers learn only pulse starts and ends, which is the case here, or also battle results and economics.
5. Which systems settle per pulse (ADR-061 DELEG pending), and the rule for a shortfall. Here the payment is capped and
   the rest is recorded as arrears.

**Limitations:**
- Static fog with observer presence intervals; at most 64 polities (recipient bitmask).
- Order operands are packed into `A` (`issuer·65536 + war`); the kernel `OrderRecord` is untouched.
- No Action Capacity in the war world (the refresh at pulse start is covered by T1's `PulseDemoWorld`).
- Only linear rates. The real campaign pipeline stays turn-atomic.
- No bench was run.

## 13. Files changed

- Added: `Proto.M7/War/WarTheaterWorld.cs`, `Proto.M7/War/WarLedger.cs`, `Proto.M7/War/WarFog.cs`.
- Added: `Proto.M7.Tests/WarPulseT2Tests.cs`.
- Added: `reports/G-war-pulse-t2.md`.
