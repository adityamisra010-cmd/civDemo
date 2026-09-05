# M5 — Schema migration v24 → v25 (report before acceptance)

The M5 packet (§22, §26) requires that a schema change be reported with its exact
reason and blast radius rather than applied silently. This is that report. The
change is implemented on `m5-full-build` and is **not merged to `main`**.

## The change

One appended table, written last in the canonical stream:

```csharp
public record struct TaxPolicyRow(PolityId Polity, double Rate);   // 4 + 8 = 12 bytes
```

`CanonicalSchema.Version` 24 → 25. Nothing else in the stream moves: the table is
appended, so every earlier block keeps its offset and its width.

## Why a table and not a derived value

The packet rules out the alternatives explicitly. Tax is a **policy**, not a
transfer: there is no treasury, no recipient, no `TaxReceiptRow`, no in-kind
shipment. A policy is a standing legislative act that must survive a save and be
identical on replay, so it is persistent state and it is the polity's own — one
row per polity, keyed by `PolityId` (D-042: sole strategic identity). It cannot be
derived, because nothing else in the world records what an Empire has legislated.

A polity with no row is untaxed; `NominalTaxRate` returns 0.0 for it. Nothing
writes the table without a `SetTaxRate` order, so on an ungoverned world the whole
contribution to the stream is one four-byte zero count prefix.

## Blast radius — measured, not asserted

Four pinned goldens are reached. Each movement is decomposed by a control arm and
recorded at the pin itself.

| Pinned world | Moves | Causes |
|---|---|---|
| `GoldenHash_Seed42Turn200` (synthetic, terrain-less) | yes | **layout only** — the empty count prefix. Stripping it returns the old pin byte for byte. |
| `FirstReign` turn 40 | yes | **layout only** — one settlement, and it is the capital, so reach is exactly 1.0. Stripping the prefix returns the old pin. |
| `FoundedGolden_Seed42Turn300` | yes | layout **+ control strength**, and nothing else |
| Driven golden turn 300 | yes | the same two |

Two causes, and only two:

1. **Layout** — the empty `TaxPolicies` count prefix.
2. **Control strength** — `GovernanceSystem` writes `ControlRow.Strength =
   AdministrativeReach`. Founding and colonization wrote a placeholder `1.0`; the
   capital still reaches 1.0 and outlying settlements now decay with travel cost.

`IntegratedPinAttributionTests`' M5 layer strips both from the founded world and
returns the pre-M5 pin `98a89d18…` **byte for byte** — nothing left over. Because
that control closes exactly, the M4-layer attribution constants beside it keep
their original values rather than being re-measured under M5.

**Happiness does not move any of them, and that took a correction.** The first
implementation made taxation a third CES factor. Measured consequence: every world
with settlements moved even when untaxed, *and* an unfed, unhoused, untaxed
settlement scored 2.31 instead of 0 — which silently disarmed the ruled revolt
condition D-021 requires M5 to ship working. Taxation now **multiplies** the
aggregate: an untaxed realm multiplies by exactly 1.0 (bit-exact, pinned), a taxed
one is scaled down at every level of provision, and total deprivation lands on
exactly 0 at every rate. That is a coefficient inside a resolution equation, which
law 2 permits — not `happiness -= taxRate`, which the packet forbids.

**Not an extraction effect either.** With no tax policy the extraction multiplier
is `1 + response × 0` = exactly 1.0, so production output is bit-identical. No
pinned world is taxed.

## Compatibility

Saves written at v24 cannot be read at v25 and are not silently upgraded — the
version check rejects them, as it has at every prior bump. No fixture in the repo
is a v24 binary save; the replay fixtures are order logs, which are unaffected
because `SetTaxRate` is an appended `OrderKind` (5) and older logs contain no such
records.

## What the director is being asked to rule on

Whether v25 as specified above is accepted. If it is not, the governing loop needs
a different home for legislated policy, and the mechanism work above stands or
falls with it.
