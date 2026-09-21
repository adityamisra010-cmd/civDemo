# CR-014 — THE CRAFT INPUT CAP IGNORES THE BANKED REMAINDER, AND THE LEDGER THROWS

**Status: CLOSED / ACCEPTED (director, T4.19 finalization) — option 1 accepted as
implemented (T4.19-A); its tests are not to be modified, tuned or weakened. The cap expression
changed; nothing else in the simulation did. §8–§10 record the fix, the unit
rig and the attribution of the driven golden's movement, every number measured
on the tree it names.**

Raised under S8 §3 during T4.19 lane C. The founding-cohort correction changed
settlement 10's crafting history on the driven golden world and reached a
coincidence the old history never did. The defect is in `ProductionSystem`, not
in founding; a founding change that happens to expose it does not license lane
C to change production, so it is escalated instead.

## §1 THE FROZEN ITEMS IN CONFLICT

- **Law 1 / `OverdrawPolicy.Throw`** — the ledger refuses to sink more than a
  stock holds, and `ProductionSystem.Craft` asks it to under `Throw`.
- **Law 3, dt-correctness via banked remainders (D-004)** — fractional
  quantities are carried in `ConsumeRemainder` between turns so that whole
  units are sunk exactly.

The two are consistent only if the input cap accounts for the bank. It does not.

## §2 THE EVIDENCE

Driven golden world (seed 42, canonical, the standing `SectorAllocation`
orders), founding vector from lane C, turn **213**:

```
Sim.Core.Kernel.LedgerOverdrawException: sinking 67 exceeds available stock 66 under OverdrawPolicy.Throw.
   at ProductionSystem.Craft (Sim.Core/Systems/Production/ProductionSystem.cs:430)

recipe 'weaving'   input good 9 (fiber)   settlement 10
stock 66   perOutput 3   exactOutput 22 (= 66/3, the Leontief cap)
ConsumeRemainder 0.9999999999999929
exactIn = 22 × 3 + 0.9999999999999929 = 67.0 exactly   → WholeUnits 67   → Throw
```

Measured by lane C with a one-line probe inside `Craft`, then reverted; the tree
carries no trace of it. Re-measured independently by the lane's verifier.

## §3 THE MECHANISM (`ProductionSystem.cs:406-432`)

```csharp
exactOutput = Math.Min(exactOutput, perOutput > 0.0 ? stockAmount / perOutput : exactOutput);
...
double exactIn = exactOutput * perOutput + inStock.ConsumeRemainder;
long sunk = ConservedMath.WholeUnits(exactIn, ...);
ctx.Ledger.Flow(ref inStock.Amount, ..., ReasonIds.InputsConsumed, sunk, FlowDirection.Sink, OverdrawPolicy.Throw);
```

The cap is computed from the **stock alone**; the sink then adds the **banked
remainder** and floors. When the remainder is within a few ulps of 1.0 the sum
rounds to `stock + 1.0`, floors to `stock + 1`, and `Throw` makes it fatal. The
remainder sits that close to 1 because an earlier turn's `exactIn − sunk` was
`k − ε`. Whether any given world reaches the coincidence is a matter of its
crafting history: the old founding vector's driven world did not in 300 turns;
the new one does at turn 213. The founded no-order run, the CI ordered run and
its replay all complete — the crash is specific to the driven allocation.

## §4 OPTIONS (≤3)

1. **Cap on the bank-inclusive stock**: `Math.Min(exactOutput,
   (stockAmount − inStock.ConsumeRemainder) / perOutput)`, so the fractional
   unit already committed is inside the cap. One expression; the Leontief
   reading ("what the stock can supply") becomes exact. Moves the driven golden
   (and any world where crafting was ever input-bound with a non-zero bank).
2. **`OverdrawPolicy.ClampToAvailable` on the input sink, shortfall NOT
   banked** — the demographics convention for its sinks. Never throws; a unit
   is under-consumed on the coincidence turn. Also moves the driven golden.
3. **Both** — belt and braces; the clamp becomes unreachable and stays as a
   guard.

## §5 BLAST RADIUS

`ProductionSystem.Craft` only. `DrivenGoldenTests` turn-300 golden and the two
`IntegratedPinAttribution` driven controls re-pin once, with the cause named
here. No corridor depends on crafting throughput directly; the calibration
battery is re-run as a check, not re-banded.

## §6 RECOMMENDATION

**Option 1.** It is the smaller change in meaning: the cap was always intended to
say how much output the stock can supply, and the stock's committed fraction is
part of the answer. Option 2 hides the defect as a silent under-sink; option 3
pays for both.

## §7 STATE UNTIL RULED

On `t4.19-glass-box`, `DrivenGolden_Seed42Turn300_MatchesPinnedConstant`,
`D1_FlowAndItsDecomposition_Measured` and
`IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_…` **fail by this
exception, deliberately unhidden**: constants left at their pre-T4.19 values
with a note. They cannot be re-pinned until the ruling, because the world they
pin does not complete.

## §8 THE RULING, AND THE FIX AS SHIPPED

**Option 1, verbatim in substance:** cap the craft input against the
BANK-INCLUSIVE available stock in the existing cap expression, so the
consumable input computed for a recipe can never exceed the available
bank-inclusive quantity. No second `ClampToAvailable` at the sink; no change to
`ConsumptionSystem` or any constant; `OverdrawPolicy.Throw` stays — it is the
tooth that found this.

The one expression (`ProductionSystem.Craft`, the input-cap loop):

```csharp
double banked = inRow >= 0 ? stocks[inRow].ConsumeRemainder : 0.0;
double supply = Math.Max(0.0, stockAmount - banked);
exactOutput = Math.Min(exactOutput, perOutput > 0.0 ? supply / perOutput : exactOutput);
```

**Why it cannot throw.** Let `S` be the stock (a long, exact in a double below
2^53), `R ∈ [0, 1)` the bank, `p` the per-output input. The sink computes
`fl(fl(exactOutput × p) + R)` with `exactOutput ≤ fl(fl(S − R) / p)`. Each of
the four roundings is at most one half-ulp relative, so the sum is at most
`S × (1 + 4 × 2^-53)` — i.e. `S` plus at most `S × 2^-51` — and floors to `S`
or below for every `S < 2^51`. `ConservedMath` documents the goods ceiling at
1e14, four orders of magnitude under that. `Math.Max(0, ·)` keeps an empty row
with a positive bank at zero output: the bank is carried, never spent from
nothing. When `R = 0.0` the expression is bit-identical to the old one, which
is why every world without an input-bound craft over a non-zero bank is
unmoved.

## §9 THE UNIT RIG — THE COINCIDENCE REPRODUCED, THEN SWEPT

`ProductionTests.Crafting_InputCapIncludesTheBankedRemainder_TheRecordedCoincidence`:
one settlement, abundant crafting labour, fiber 66 with `ConsumeRemainder`
set to `0.9999999999999929` by hand — asserted bit-equal to `1 − 2^-47`, 64
ulps below 1.0, so the rig is the transcript and not something near it. Labour
wanted more than 66 fiber (`LastInputDemandUnits > 66`, measured in the test),
so the INPUT cap is what binds, exactly as at turn 213.

- **RED, against the pre-ruling cap** (measured with the fix stashed, then
  restored): `LedgerOverdrawException: sinking 67 exceeds available stock 66
  under OverdrawPolicy.Throw` — the §2 message to the character. 22 × 3 +
  (1 − 2^-47) = 67 − 2^-47, a tie between 67 − 2^-46 and 67.0 that rounds to
  even, 67.0.
- **GREEN, with the fix:** `supply = 66 − (1 − 2^-47) = 65 + 2^-47`, a tie at
  spacing 2^-46 that rounds to 65.0; cap 65/3; sink `65/3 × 3 + bank =
  66 − 2^-47`, a tie that rounds to 66.0; sunk 66 ≤ 66, stock 0, carried
  remainder exactly 0.0, cloth 21 = ⌊65/3⌋, and `left + sunk = 66` to the unit.
  Every one of those values is asserted, not described.

`ProductionTests.Crafting_InputSinkNeverExceedsStock_AcrossBankedRemainders`:
a deterministic sweep (fixed arrays, fixed order, no RNG) over stocks
{0, 1, 2, 3, 4, 5, 7, 66, 67, 100, 999, 10^9} × banks {0, 0.25, 0.5, 0.75,
1 − 1 ulp, 1 − 2 ulp, 1 − 16 ulp, 1 − 64 ulp}. Every row is a fresh one-step
rig; every row asserts `sunk ≤ stock`, `stock ≥ 0`, `left + sunk = stock`
exactly, and the carried remainder in [0, 1). Non-vacuity, asserted: at least
88 of the 96 rows are input-bound (every stock below 10^9) and fewer than all
96 are, so both sides of the `Min` are exercised. **RED against the old cap** at the first near-1 row it reaches:
`sinking 2 exceeds available stock 1` (stock 1, bank 1 − 2^-53: ⅓ × 3 rounds
to 1.0, + (1 − 2^-53) = 2 − 2^-53, a tie that rounds to 2.0).

## §10 ATTRIBUTION — THE DRIVEN GOLDEN MOVES FOR TWO CAUSES, MEASURED APART

Per-turn `WorldHash` logs on every arm (turns 0–300, or to the throw); first
differing turn found by `diff`; field-level diffs by `sim diff` on saved
snapshots. Each arm ran in its own worktree.

| arm | tree | cap | founding vector | turn-300 hash |
| --- | --- | --- | --- | --- |
| OLD | `feaf218` (pre-lane-C) | old | old | `01673381e5e4b18753bf19f345e42f5424046a813a8c723a7564be34186820af` (= the standing pin, reproduced) |
| X1 | `feaf218` + ONLY the §8 change | new | old | `01673381e5e4b18753bf19f345e42f5424046a813a8c723a7564be34186820af` (**= OLD**) |
| — | `673b95b` (lane base), unfixed | old | new | throws at turn 213 (turn 212: `35397d6f9820df24aec4c31cb67a1aa87d91f9a1d2a102f63d2056f119118e86`) |
| X2 | `673b95b` + the §8 change | new | new | `76f82629abbffbc3c0897d2cfab7933e890a5441697dfdb82a59cd64d74163a6` (**the new pin**) |

**OLD → X1 (the cap change on the old vector): first differing turn 273.**
Turns 273, 274, 275 differ; 276–300 are byte-identical, and so is 300. At
273 settlement 9's pottery-firing was timber-bound with a 0.25 bank; the new
cap lowers the recipe's output by 0.25 / 0.5 = 0.5 pots. Measured field by
field (`sim diff`, `dump-*/t273.bin`): timber `ConsumeRemainder` 0.25 → 0,
clay stock 0 → 1 with `LedgerFlows` (clay, InputsConsumed) `TotalSunk` 178143
→ 178142 — one clay unit not yet sunk — and pottery `ProduceRemainder` 0.5 →
0. At 274 the same three rows differ plus `PriceTerms` row 131 (computed from
the PREV clay stock 0 vs 1). At 275 `GoodStocks` and `LedgerFlows` are
identical again (clay total 178252 on both arms — the deferred unit was
consumed) and `PriceTerms` row 131 is the only difference; the written
`Prices` table is identical on all three turns. At 276 nothing differs
(114,881 bytes, 40 blocks). The fix's whole effect on the old vector is a
three-turn transient that leaves no trace at 300.

**X1 → X2 (the founding vector on the fixed cap): first differing turn 0**,
as lane C's turn-0 control records.

**Unfixed → X2 on the new vector: first differing turn 15**, and every turn
15–212 after it (198 of 198). At 15 settlement 1's weaving is fiber-bound with
bank 0.7499999999995453: the new cap spends it (`ConsumeRemainder` → 0) and
cloth's `ProduceRemainder` moves 0.9166… → 0.6666… (−0.25 = −0.75/3) with the
same 399 cloth produced; the ulp-level remainder difference propagates from
there. The unfixed run throws at 213 as §2 records.

**Reading:** lane C's founding vector is the whole of the pin's movement —
the cap change alone returns OLD byte for byte — and the cap change is what
lets the world be measured at 300 at all. The two are separated by
measurement, not by argument.

**Re-pinned, each re-measured through its own helper on `X2`:**

| constant | old | new |
| --- | --- | --- |
| `DrivenGoldenTests` golden | `01673381…` | `76f82629abbffbc3c0897d2cfab7933e890a5441697dfdb82a59cd64d74163a6` |
| `IntegratedPinAttribution.CapacityFloorFixAtSchemaV22` (`HashAtSchemaV22`) | `611a1508…` | `60bd5b208696a25f93469d58d4a4284d8ae8467107aeee3545d3d0f82b61ba14` |
| driven `beforeM4C` (`HashAtSchemaV23`) | `e2f3c042…` | `cf93e0fed26a3e28e9e971498f8240c1534a5a8aa97391fe030adeaf76d4fa75` |

`Assert.NotEqual(mainPinBeforeTheFix, atV22)` still holds (`5b204b45…` ≠
`60bd5b20…`). **Unmoved, run rather than assumed:** `GoldenHash_Seed42Turn200`
(synthetic), `FoundedGolden`, both `FirstReign` pins and the founded /
FirstReign / M5-layer `IntegratedPinAttribution` constants — no founding world
reaches an input-bound craft over a non-zero bank differently under this cap.

**D1 (`D1_FlowAndItsDecomposition_Measured`), the printout, not an assertion —
OLD arm (feaf218) vs X2:**

```
OLD  T311_D1 totalFlow=3   rows=1  minPathCost=4.9012  pairs=132   cloth moved=3
     pottery GAP>DEADBAND(!)   timber, bronze GapUnderDeadband   the rest GapZero
X2   T311_D1 totalFlow=20  rows=5  minPathCost=4.0070  pairs=132   cloth moved=20
     timber, copper-ore, fiber, bronze, pottery GAP>DEADBAND(!)   tools GapUnderDeadband
```

The OLD reading is the same with or without the cap change (X1 = OLD at 300),
so the D1 movement is the founding vector's too.

## §11 THE SUITE — MEASURED, RELEASE

First full run with the fix and the three re-pins above: **691 passed / 6
failed / 6 skipped** (703 = the 681 + 14 + 6 of the verification record §5
plus the two `ProductionTests`). Eight of the nine CR-014 failures collapsed
outright. The ninth,
`WorldReconciliationTests.Driven_Seed42_300Turns_EveryQuantityReconcilesEveryTurn`,
now reconciles every quantity on all 300 turns — the property — and failed
only its non-vacuity sample `Dwellings.Decayed > 0` on turn 25, a turn read
on the pre-lane-C founding vector.

That sample was re-measured with a probe over `ObservedWorlds.DrivenRun`
(per-turn starvation, trade units, dwellings decayed), three arms:

| arm | founding vector | cap | first decay turn | turn 7 |
| --- | --- | --- | --- | --- |
| lane-obs `8f59166` (fresh detached worktree) | old | old | 25 (decayed 4; trade 510) | starvation 68, trade 9 |
| lane base, unfixed (probed to 212) | new | old | **48** (decayed 3; starvation 44, trade 240) | starvation 93, trade 12 |
| this tree | new | new | **48**, identical readings | starvation 93, trade 12 |

The unfixed arm is the control: the cap change moves nothing in that sample,
so the founding vector is its whole cause. The assertion now reads turn 48
with this record beside it; the identities were never weakened.

**Final, on the committed tree, Release: Sim.Tests 692 passed / 5 failed /
6 skipped (703).** All nine CR-014 tests pass by name, the two `ProductionTests`
pass, and `GoldenHash_Seed42Turn200`, `FoundedGolden`, both `FirstReign`
controls and the founded / FirstReign `IntegratedPinAttribution` constants pass
unchanged. The five failures are exactly the ones the ruling names —
`Dev_MalthusCorridors` seeds 42 and 7 (CR-003) and lane C's three readings
(`Canonical_FedCorridors` seeds 1 and 2, `Artisans_EmergeInFedAutoplay`) —
untouched here. `Sim.Ui.Tests` 227 / 227. Build 0 warnings; banned-constructs,
read-isolation and readonly-proof gates OK.

