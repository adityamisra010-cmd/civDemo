# CR-014 — THE CRAFT INPUT CAP IGNORES THE BANKED REMAINDER, AND THE LEDGER THROWS

**Status: OPEN — awaiting director ruling. No simulation expression, constant,
golden, corridor or quarantine was changed. This file is the whole change.**

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
