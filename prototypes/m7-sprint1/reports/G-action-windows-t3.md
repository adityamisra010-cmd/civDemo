# G — Phase C lane T3: two-action decision windows within Action Capacity

Date 2026-11-05. Authority: DIR5 §2.8 (D-079 DR22). The prototype is isolated: `Proto.M7` / `Proto.M7.Tests` only, with no
`Sim.*` reference. Built on T1 (Runner, decision windows, order gate) and T2 (`WarTheaterWorld`: War Pulses, fog
recipients). Neither is regressed: every T1/T2 test passes unchanged, and no golden moved.

The two-action count is a **Director decision**. Everything marked **PROPOSAL** is delegated work ("You own the
detailed legal-action rules and implementation") and is put forward for Director review.

## 1. Rule text implemented

| Source (verbatim) | Implemented as |
|---|---|
| DIR5 §2.8 "Maximum two actions per decision window." | `ActionRules.MaxActionsPerWindow = 2`. This is a constant, not data. The task packet says "each polity": slots are counted **per polity per enclosing window** (see Q1). |
| DIR5 §2.8 "Integrate this with the already-ratified agent-specific Action Capacity rule. Do not replace or ignore Action Capacity." | Both constraints apply to every action: a polity slot **and** the agent's own Action Capacity (C23 item 3). |
| Ledger §17.2 `:863` "Every MobileAgent has Action Capacity." `:865` "Actions are agent-specific." D-043 B2 "The budget mechanism is universal." "The action set is agent-specific." "Do not create a giant universal action list." | `AgentProfile` (data): `Capacity`, per-kind AC costs, and `ActionMask`, the agent's own subset of 4 kinds. One mechanism, `ActionWindowBook`, serves every agent. |
| D-043 B2 "Normal world: Action Capacity refreshes according to the strategic turn." | At `TurnStart`, every agent **not** in an active war is refreshed. |
| Ledger §19 `:949` "involved military agents receive refreshed Action Capacity"; ADR-061 §3.11 "refreshes at **pulse start, not at the turn boundary**" | At `PulseStart(w)`, the agents of war w are refreshed. A `TurnStart` inside a pulse leaves them untouched. |
| Ledger §18 `:933` "Agents choose the least Action-Capacity-consuming viable route." | Move cost = \|dx\| × `MoveCostPerCell`. On the uniform 1-D line the straight route is the least-cost route. Terrain is out of scope. |
| C23 item 2 / D-079 R8 (reading RDG-10, as applied) | Event-triggered (interrupt) windows give **access** to the enclosing window. They refresh neither slots nor AC. |
| Ledger §26 item 11 (no reusable generic per-turn exception); D-079: A-15 scoped Law 3 exemption | The count moves no stock. The only stock effect is Recruit, a one-off Ledger transfer of an amount the order chooses, limited by the treasury. More windows per sim-year cannot create money. Rate economics stay per sim-year × dt (T2). Whether the per-window slot reset and AC reset fall inside the A-15 instrument's scope is for that ADR. |

"Refreshed" is read as **reset to full capacity**. Capacity never accumulates.

**Enclosing window (C23 item 1).** An agent whose war has started and not ended is in the OPERATIONAL window, which is the
current War Pulse of its war. Every other agent is in the CAMPAIGN window, which is the current strategic turn. The
polity's slots are kept per (polity, war, pulse) and per (polity, turn).

**Legality (checked in this order; the first failure is logged as the reason):**
1. fog: the issuer must be a recipient of the window at T (T2 gate, `NotARecipient`);
2. ownership (`NotOwnAgent`);
3. the kind is in the agent's set (`NotInActionSet`);
4. a slot is left (`SlotsExhausted`);
5. the same-agent matrix (`KindRepeated`, `CombinationForbidden`);
6. the hit-and-run bar (`HitAndRun`);
7. target or argument (`OutOfReach`, `BadValue`);
8. Action Capacity (`CapacityExhausted`).

A refusal is logged as `OrderRefusal(log, issuer, kind, T, reason)` and changes nothing else: no slot, no AC, no
position, no Ledger transfer and no draw. Engagement rolls are counter-RNG keyed by (attacker, defender, instant), never
by a cursor.

## 2. Combination matrix (PROPOSAL, implemented as data: `ActionRules.SameAgent`)

The table applies to the same agent within one window. The row is the earlier action and the column is the next one.
"K" is `KindRepeated`, which is checked first.

| first \ next | Move | Attack | Retreat | Recruit |
|---|---|---|---|---|
| **Move** | K | yes | no | yes |
| **Attack** | no | K | no | no |
| **Retreat** | no | no | K | no |
| **Recruit** | yes | no | yes | K |

The reasons for each rule:
- **Move→Attack** is advance-and-strike.
- **Attack→anything** is barred. This covers strike-then-reposition and strike-then-retreat (hit-and-run).
- **Retreat** ends the agent's window.
- **Move→Retreat** is barred. This stops advance-then-withdraw oscillation.
- **No Recruit with an Attack** in either order. This carries over the prototype's "no Heal/Rally with an attack" rule.

**Different agents of one polity:** every ordered pair combines. This is tested for all 16 pairs.

## 3. Anti-exploit rules (PROPOSAL)

| # | Rule | Enforced by | Test |
|---|---|---|---|
| AE1 | **No window farming.** Slots and AC refresh only at the world events that define the enclosing window (`TurnStart`, `PulseStart`). Interrupt windows (significant events, probes) refresh nothing, and orders never open windows (T1 R4). | `ActionWindowBook.OnTurnStart` / `OnPulseStart` only | `NoWindowFarming_…` (k = 1, 3, 6 interrupts plus a TurnStart inside one pulse: exactly 2 accepted) |
| AE2 | **No carry-over.** A refresh resets both slots and AC. Unused slots and unused AC are lost. | `Refresh`; new `WindowUse` per window | `NoCarryOver_…`, `Refresh_…ResetNotAdd` |
| AE3 | **No splitting.** Each kind at most once per agent per window. Move cost is linear in distance, so a split across windows is never cheaper and costs one slot per window. | `KindRepeated`; cost function | `NoSplitting_…` |
| AE4 | **Presentation independence.** The budget is identical whether windows are shown, auto-skipped or replayed. The book has no presentation input. | structural | `Budget_IsIdentical_…` |
| AE5 | **A refusal is inert.** No slot, AC, transfer or draw. | all checks run before the first write | `ThirdAction_…`, `AgentSpecificActionSet_…` |
| AE6 | **Hit-and-run across windows.** An agent that retreated from enemy E may not attack E in the same window or the next `HitAndRunCooldownWindows` = 1 window of its own, unless E has since attacked it or closed on it. | `Disengagement` records | `HitAndRun_…` (bar, attack-lift, close-lift) |
| AE7 | **Fog.** Only a recipient of the window at T may act at T. Attempts by others consume nothing. | T2 gate, before the book | `Fog_…` |

## 4. Order-delivery pins (turn-exact, ±1 ms)

An action issued at window T applies at T. It spends a slot of the window that contains T.
- **Operational, pulse start X:** at X − 1 the action uses pulse n, which is exhausted, so it is refused. At X and at X + 1 it uses pulse n + 1, which is fresh.
- **Campaign, turn start S2:** at S2 − 1 the action uses turn 1 (refused). At S2 and at S2 + 1 it uses turn 2 (fresh, and AC is refreshed).

## 5. Tests and counts (measured this session)

| Config | Before | After |
|---|---|---|
| Debug | 369 passed, 0 failed (measured before any change) | **388** passed, 0 failed |
| Release | 369 passed (measured with the T3 class filtered out) | **388** passed, 0 failed |

- Build: Debug and Release, **0 warnings and 0 errors**.
- New tests: 19, in `ActionWindowsT3Tests` (15 facts, plus 2 theories × 3 cases).

## 6. Determinism scan

`scripts/check-proto-determinism.sh` scanned 48 files: **CLEAN**. The scan covered `ActionWindows.cs`, which contains no
Dictionary/HashSet, float or Random, and no hash used as logic input.

## 7. Mutants

Each mutant ran the full Debug suite (388 tests). Each run was bounded by `timeout 390` s, which is 10× the clean
Debug suite (39 s). The wall times measured were 50–131 s. The machine was shared with other agents' dotnet jobs, so
times are inflated, but none came near the bound and **none hung**. Every mutant was killed by at least one semantic
test. Every kill below is an assertion on an outcome or a refusal reason, not on a golden alone.

| Mutant | Change | Failed (of 388) | Semantic kills (selected) |
|---|---|---|---|
| M1 | `MaxActionsPerWindow` 2 → 3 | 8 | `ThirdAction_…` (third action accepted), `TieDense_…`, `NoWindowFarming_…`, both ±1 ms pin theories, `Fog_…` |
| M2 | refresh adds capacity (`+= cap`), so unused AC carries over | 9 | `NoCarryOver_…` (cost-7 move accepted), `Refresh_…ResetNotAdd`, `CapacityExhaustion_…`, `CombinationMatrix_SameAgent_…` |
| M3 | every new action instant resets all slots (interrupt windows refresh: window farming) | 5 | `NoWindowFarming_…`, `OperationalWindow_…(−1)`, `CampaignWindow_…(−1)`, `CapacityExhaustion_…` |
| M4 | an out-of-reach Attack refusal consumes a slot | 1 | `ThirdAction_…` (an accepted Move is refused `SlotsExhausted`) |
| M5 | `TurnStart` also refreshes war agents | 5 | `Refresh_WarAgentsAtPulseStartNotTurnStart_…`, `CapacityExhaustion_…`, `HitAndRun_…`, `NoWindowFarming_…` |
| M6 | matrix Attack→Retreat allowed | 1 | `CombinationMatrix_SameAgent_EveryOrderedPair` |
| M7 | `HitAndRunCooldownWindows` 1 → 0 | 1 | `HitAndRun_…` (re-attack in the next window accepted) |
| M8 | action dispatch moved before the fog gate | 2 | `Fog_…` (non-recipient actions accepted), `Budget_IsIdentical_…` (golden refusal list) |

**Run note.** The first batch was a background job with a 10-minute limit. It was killed during M7, after M1–M6 had
completed. The kill left M7's edit in the source, and I restored it by hand and checked every mutant site. M7 and M8
were then re-run. M8's first form, `if (false)`, failed to build (CS0162 as an error) and was rewritten as a condition
the compiler cannot prove false. The final clean build and both suites (above) were run after all mutants were
restored.

## 8. Goldens

- **No golden moved.** The T1 and T2 golden tests are unchanged and pass. The action layer contributes to
  `WarTheaterWorld.StateHashHex` only when the world has agents, and the recruit sink opens only then.
- **New T3 golden** (first pin, no earlier value): state `2E4CB273…F6198` and book `1BBAA5CF…7E70F`. The golden comes
  with semantic assertions: the accepted kinds, and the refusal reasons in order.

## 9. Limitations

- The world is a 1-D line of cells with uniform cost: there is no terrain, route-finding or line of sight. Attacks
  only record an engagement and a roll. There are no casualties (C24/M7.14).
- `Levies` is a counter of money paid. It is not people.
- Only the campaign and operational layers are built. The tactical-pulse layer (M7.11) is not.
- C23's Law-3 metamorphic test (a Move's effect scales with dt and the count does not) is not built, because Move is
  instantaneous here.
- `ActionCapacityShare` → slot rounding (C23 item 5) is not built.
- Polity policy orders from T2 (supply rate, upkeep rate) are left **outside** the slot budget. They are idempotent
  last-write latches, so repeating one gains nothing (Q3).

## 10. Open questions for the Director

- **Q1. Slot scope.** The task packet says per polity. C23's invariant says "per agent". Per polity is implemented.
  Per agent would make the same-agent matrix the only within-window limit.
- **Q2. R8 / RDG-10.** Event windows share the enclosing window's slots (implemented). The alternative gives each event
  window two fresh slots and bounds them by AC only. The implemented rule can leave a polity with no slot at a
  significant event (DIR5 §2.5).
- **Q3. Policy orders.** Should supply and upkeep orders consume slots?
- **Q4. Values.** The cooldown (1 window) and the matrix cells.

## 11. Files changed

- Added `Proto.M7/War/ActionWindows.cs`: the kinds, profiles, rules and matrix, and `ActionWindowBook`.
- Added `Proto.M7.Tests/ActionWindowsT3Tests.cs`.
- Changed `Proto.M7/War/WarTheaterWorld.cs`:
  - action order kinds 150–153 and refusal codes 10–17;
  - an optional `ActionLayerSpec` constructor argument;
  - refresh hooks in `Begin`, `OnTurnStart` and `OnPulseStart`;
  - action dispatch after the fog gate;
  - conditional hash append.
- Added this report.
