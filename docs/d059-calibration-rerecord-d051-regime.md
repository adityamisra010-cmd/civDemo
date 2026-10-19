# D-059 — The dev Malthus corridor's recorded values, re-recorded under the D-051 §1(b) regime

**Status: IMPLEMENTED 2026-10-19 on branch `b5-calib` (LOCAL; not pushed, not merged).** Authority: the Director,
2026-10-19, verbatim: *"CI main test run, full authorization, resolve the issue best you can."* That instruction
supersedes the hold in D-052 §9 / CR-020 §B ("nothing is re-pinned") for the two items named below and for nothing
else. CR-020 §B's own recommendation (option 1, re-record) is the resolution adopted. Every number here was MEASURED by
the agent writing this record, on this branch's base `827337e`, .NET 10.0.112.

> **Status note — 2026-10-19 (later; supersedes the "LOCAL … not pushed, not merged" status above, which is kept as written):**
> merged into `claude/new-session-yavghm` at `6d1988f` and pushed to that branch; CI run 39270568141 is green on every
> required job. Not on `main`. The Director's acceptance is still pending.

## 1. The two red cases (reproduced)

`CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand` (`Sim.Tests/Systems/CalibrationBatteryTests.cs`), dev
preset (256 px, 4 settlements, cylinder), 1000 turns, no orders:

| seed | failing assertion | measured | frozen reference |
|---|---|---|---|
| 42 | CR-003/T3.4c migration quarantine, DOWN tooth | `dev.migrationGrossPerDecade` 8.357547346004101E-05 | 0.75 × recorded 2.486239574664698E-04 = 1.8647E-04 |
| 7 | recorded-trajectory population envelope | final population 123,950 | [71,000, 119,000] |
| 7 (masked) | the same DOWN tooth, never reached | 8.367965454628822E-05 | 0.75 × 2.8835491401298867E-04 = 2.1627E-04 |

What each protects:
- **The DOWN tooth** guards against a silent loss of the migration mechanism inside the quarantined below-floor regime
  (its calibration: ×0.536 = disablement, ×0.836 = largest legitimate correction). Its own text: "a ruled substrate
  change that must re-pin this envelope deliberately, like a golden".
- **The population envelope** pins the recorded pre-Malthusian trajectory (monotone, starvation-free) against a
  collapse, a runaway or a demographic-clock drift. Its text: "the recorded-trajectory pin, re-pinned like a golden with
  this history line"; it had been re-pinned four times ([70k,100k], [68k,119k], [82k,138k], [71k,119k]), each at ~12 %
  margins on the two measured finals.
- Neither is a `corridors.json` band. The bands (`dev.migrationGrossPerDecade` [0.001, 0.01] and the four CR-003 dev
  bands) are untouched and their immovability asserts still run.

## 2. Root cause, decomposed (layer twins, single-variable steps)

Chronological layers, each arm differing from the previous in one ruled change (`TestConfigs` twins;
the cylinder via `PreD054Worldgen`):

| arm | seed 42 mig/decade | seed 42 final | seed 7 mig/decade | seed 7 final |
|---|---|---|---|---|
| R4 recorded (2026-10-04) | 2.4862E-04 | 80,937 | 2.8835E-04 | 105,841 |
| pre-1(b) chained twin, flat world | 2.5154E-04 | 82,976 | 2.8567E-04 | 104,104 |
| + D-051 §1(b) time-averaged weather | 8.7177E-05 (×0.347) | 98,208 | 1.0083E-04 (×0.353) | 124,455 |
| + D-053 batch (relief, granaries, purse) | 8.7177E-05 (×1.000) | 98,208 | 9.8771E-05 (×0.980) | 124,483 |
| + D-054 WS3 cylinder = shipped tree | **8.3575E-05** (×0.959) | **98,684** | **8.3680E-05** (×0.847) | **123,950** |

- **Migration.** 1(b) cuts gross migrants 2,831 → 1,127 (seed 42) and 4,033 → 1,670 (seed 7) while person-years
  rise ×1.15 / ×1.17. Starvation is 0 in every arm: what disappears is *deficit flight* on single-decade weather
  shortfalls — the exact behaviour D-051 §1 ruled out ("a 4 % shortfall causes a ~6 % one-turn exodus").
- **Population.** The same shortfalls were setbacks to the pre-Malthusian exponential; removing them lifts finals
  +18 % / +20 %. Seed 42 stays inside [71k, 119k] by luck of its start; seed 7 leaves it.
- **Per-500-year series** (shipped tree vs the 1(b)-only switch-off on the same tree; migrants per window):
  - seed 42, ON: 337, 274, 66, 34, 29, 35, 21, 45, 37, 120, 92. OFF: 343, 246, 87, 59, 111, 77, 76, 56, 119, 119, 93.
  - seed 7, ON: 428, 222, 45, 31, 26, 57, 65, 67, 115, 89, 244. OFF: 435, 213, 65, 100, 91, 101, 118, 212, 145, 55, 215.
  - The first 1,000 years (founding sort-out) are unchanged. 1(b) removes the mid-horizon (years 1,500–4,500)
    weather-flight bursts, which is where the R4 recording's volume came from.
- **The R4 values are no longer reachable by undoing 1(b) alone.** The 1(b)-only switch-off on the shipped tree reads
  1.2460E-04 / 1.2298E-04 (×0.50 / ×0.43 of R4), because the cylinder regenerated the dev terrain. So the stale
  references carry two causes, both ruled: 1(b) (dominant) and the D-054 cylinder.
- **20-seed context** (shipped tree, dev, 1000 turns, seeds 1–20): migration 6.12E-05 … 1.45E-04 on all 20 (all
  below the 0.001 floor: the quarantined regime, unchanged in kind). Finals 59,123 … 127,207. Seeds 42 and 7 are
  typical.

## 3. Decision

**(b) Re-record the two per-seed recorded migration values and the population envelope under the D-051 regime.**

- `QuarantineRecordedSeed42` 2.486239574664698E-04 → **8.357547346004101E-05**.
- `QuarantineRecordedSeed7` 2.8835491401298867E-04 → **8.367965454628822E-05**.
- Population envelope [71,000, 119,000] → **[86,000, 139,000]**: the established rule (~12 % margins on the two
  measured finals, rounded outward to the thousand: 98,684 × 0.88 = 86,842; 123,950 × 1.12 = 138,824). No margin
  was added beyond the rule.
- **Unchanged:**
  - the 0.75 drift tolerance;
  - every `corridors.json` band and their immovability asserts;
  - the starvation-zero, no-crash and monotone-growth teeth;
  - the band-first ordering and its non-overlap premise (recorded / 0.75 = 1.114E-04 / 1.116E-04, still below 0.001,
    asserted by `T4216_…`).
- Nothing is skipped, quarantined, waived or deleted.
- **The teeth keep aim (MEASURED mutant):** switching 1(b) off in `sim.json` (the change these pins now record) fails
  BOTH cases.
  - seed 42: population 81,631 is below 86,000;
  - seed 7: migration 1.22976E-04 is above 1.33 × 8.36797E-05 (the UP-DRIFT tooth).

**Rejected — (a), a TUNE/data change.**
- The movement is the ruled purpose of D-051 §1(b). Restoring flight volume means reintroducing weather-driven
  exodus, or inventing a non-weather migration driver tuned to a test envelope. The second is the "tune to this
  corridor" move the M4 completion ruling (§5/§22) forbids.
- Damping growth to pull seed 7 back would move every world golden and the canonical fed-growth corridor to fit an
  in-test snapshot.
- And per §2, even a full 1(b) revert would not restore the R4 references on the cylinder world.

**Rejected — (c), a new CR.** CR-020 already is that CR. Its option 1 is this resolution, and the Director has now
authorised resolving it.

## 4. Hashes and blast radius

- The change touches only `Sim.Tests/Systems/CalibrationBatteryTests.cs` (3 literals and history comments) and docs.
  No `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Ui` or `ci.yml` byte changed, so **no world hash, golden or CI pin can
  move**. The full suites pass with every golden untouched (§6).
- Dev calibration world hashes at turn 1000 on this tree: seed 42 `1cd1425d544c95d5…`, seed 7 `714a7066bb00de4f…`.

## 5. Robustness to the parallel construction change (leftover capacity continues down the queue)

- **Structural.** The battery steps a bare `TurnExecutor` with no order log, so no construction order is ever issued
  (`AiConstructionPolicy` runs only through `AiOrders.Append`, from the session/CLI). Construction queues stay empty,
  and a rule about how capacity flows *down a queue* has nothing to act on.
- **Measured.** Switching off incremental construction, reservations or the construction purse leaves both dev worlds
  bit-identical to the shipped one (same final hash, same metrics). Of the post-R4 layers, only 1(b), the food relief
  (seed 7) and the cylinder move this world.
- **Check after the change lands.** Re-run `Dev_MalthusCorridors_AllInBand` and compare the turn-1000 dev world
  hashes above. Equal hashes mean these pins hold exactly. If they differ, the construction change reached a
  no-order world. That is a finding about the change, not about calibration, and is to be attributed by its own
  switch-off twin before any re-pin.
- Slack if it did reach: the 0.75 tooth leaves ±25–33 % on migration, and the envelope ~12 % on population.

## 6. Results (MEASURED on this branch, Release, .NET 10.0.112)

| check | result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 warnings, 0 errors |
| `CalibrationBatteryTests` (all 7: canonical 1, 2; dev 42, 7; era continuity; corridors file; T4.21-6 teeth) | 7 / 7 passed |
| Sim.Tests, full (`xUnit.MaxParallelThreads=2`) | 2,612 passed / 0 failed / 13 skipped (2,625), 53 m 57 s |
| Sim.Ui.Tests, full | 1,040 / 1,040 passed, 23 m 22 s |
| `check-banned-constructs.sh` / `check-read-isolation.sh` / `check-readonly-proof.sh` | OK / OK / OK |
| mutant: `sim.json harvestVariance.timeAveragedHarvest` = false | both dev cases FAIL (seed 42 population 81,631 < 86,000; seed 7 migration 1.22976E-04 > 1.33 × recorded) |

- A first full Sim.Tests run at default parallelism was killed by the shared container's memory cgroup (OOM, `dmesg`)
  after 1,592 passed / 0 failed.
- That kill was an environment limit, not a test result. The full re-run above is the result.
