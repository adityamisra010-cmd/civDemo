# AGENT B — FOOD EVIDENCE: THE 40-SEED EXACT-ZERO SWEEP AND THE PROPAGATION TRACES

Worktree: `wt-sweepB` (detached at f76dd56). Tests only; no file under
`Sim.Core/`, `Sim.Data/` or `Sim.Cli/` was modified.

New test files (both untracked, both under `Sim.Tests/`):

- `Sim.Tests/Kernel/FoodZeroSweepEvidence.cs` — the 40-seed sweep.
- `Sim.Tests/Kernel/FoodPropagationTraceEvidence.cs` — the propagation traces.

Raw artifacts: `/tmp/agentB-sweep.md`, `/tmp/agentB-traces.md`.
Runs: sweep PASSED in 23 m 01 s (4 seeds in parallel, 4 cores, under contention
from another worktree's test run); traces PASSED in 4 m 32 s.

Capacity is an **estimate** everywhere below and is labelled as such. The
granary ceiling lives inside `BoundStore` and was deliberately NOT
re-implemented. It is inferred from the demand the consumption system itself
publishes — `GoodStockRow.LastConsumptionDemandUnits`, post-substitution —
as `GranaryYearsOfDemand * Σdemand / dtYears`, with
`GranaryYearsOfDemand = 1.5` read from `cfg.Consumption`. It is accurate to the
sub-unit remainder bank, i.e. of order one unit per settlement (12 settlements).

Grain stock is a `long`. **Zero means exactly 0. No epsilon is used anywhere.**

---

## 1. THE EXACT-ZERO ANSWER

**NO.** Over seeds 1..40 × 120 turns = **4,800 turn-events**, aggregate
end-of-turn grain was **never** exactly 0 with positive population.

| measurement | value |
|---|---|
| seeds with an aggregate exact zero and pop > 0 | **0 / 40** |
| turn-events with an aggregate exact zero and pop > 0 | **0 / 4800** |
| turns with **zero population** (a different fact, counted apart) | **0** |
| global minimum aggregate grain | **2602**, seed 24, turn 11 |
| store/capacity ratio range over all seeds/turns | [0.0000, 0.9998] |
| conservation (`FoodTurnAccount.Reconciles`) failures | **0 / 4800** |
| seeds that crashed | 0 / 40 |

Because the count is zero, the "full detail rows for any zero events" section is
empty — there are no rows to print. The harness would have printed seed, turn,
dt, population, settlement count, harvest, eaten, spoilage, overflow, previous
ending stock, published demand and the capacity estimate for each, and printed
none.

### The five outcomes, kept distinct

- **(a) zero grain with positive population — 0 occurrences at the aggregate.**
  It *does* occur per settlement: 87 turn-events across the 40 seeds had at
  least one of the 12 settlements holding exactly 0 grain (per-seed counts in
  the table below, max 7 on seed 16). Every settlement had a grain row at all
  times (`settlementsNoRow = 0` in every traced turn), so "no row" never
  masqueraded as "zero".
- **(b) zero population — 0 occurrences.** Every seed ended with population
  between 6,131 (seed 24) and 10,969 (seed 32).
- **(c) merely low grain — this is what the minima are.** The lowest aggregate
  store observed anywhere was 2602 against a same-turn consumption of 19,906
  units, i.e. about 13% of one turn's eating. Low relative to demand, but not
  zero and never zero.
- **(d) low harvest (harvest < that turn's eating) — exactly 1 turn per seed,
  40 turns of 4800.** In every seed this is turn 1, the founding turn, before
  production has run a full cycle. From turn 2 onward harvest exceeded
  consumption on every single turn of every single seed.
- **(e) capacity-limited grain — 120 turns of 120, in all 40 seeds
  (4800 / 4800).** Granary overflow was strictly positive on **every turn of
  every seed**. The store is not scarcity-limited at all; it is pinned against
  the granary ceiling on literally every turn measured, with the store/capacity
  ratio topping out at 0.9990–0.9998 whenever the harvest is normal.

**Conservation held everywhere**: `Residual == 0` on all 4,800 turns; the sweep
asserts it per turn and the assertion never fired.

### Per-seed table

`minGrain@turn | minPositiveGrain@turn | turns with a settlement at exactly 0 |
low-harvest turns (h<eaten) | capacity-bound turns (overflow>0) | pop at min |
end pop | settlements | store/cap range`

Note that min and min-positive coincide for every seed — a direct consequence of
the aggregate minimum never being 0.

| seed | minGrain@turn | minPositive@turn | settl.-zero turns | lowHarvest | capBound | popAtMin | endPop | settl. | store/cap range |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 4787@17 | 4787@17 | 1 | 1 | 120 | 4796 | 10480 | 12 | [0.0000, 0.9996] |
| 2 | 4806@9 | 4806@9 | 4 | 1 | 120 | 4515 | 10403 | 12 | [0.0000, 0.9997] |
| 3 | 3491@14 | 3491@14 | 4 | 1 | 120 | 3695 | 8336 | 12 | [0.0000, 0.9995] |
| 4 | 3227@9 | 3227@9 | 2 | 1 | 120 | 3755 | 8732 | 12 | [0.0000, 0.9995] |
| 5 | 2769@6 | 2769@6 | 5 | 1 | 120 | 3615 | 8514 | 12 | [0.0000, 0.9996] |
| 6 | 4665@4 | 4665@4 | 0 | 1 | 120 | 4285 | 10496 | 12 | [0.0000, 0.9997] |
| 7 | 3751@4 | 3751@4 | 0 | 1 | 120 | 4242 | 10405 | 12 | [0.0000, 0.9996] |
| 8 | 4252@4 | 4252@4 | 1 | 1 | 120 | 4021 | 9850 | 12 | [0.0000, 0.9996] |
| 9 | 2989@6 | 2989@6 | 0 | 1 | 120 | 2740 | 6603 | 12 | [0.0000, 0.9994] |
| 10 | 3325@65 | 3325@65 | 3 | 1 | 120 | 5784 | 8515 | 12 | [0.0000, 0.9996] |
| 11 | 3112@6 | 3112@6 | 2 | 1 | 120 | 3468 | 8314 | 12 | [0.0000, 0.9995] |
| 12 | 3789@6 | 3789@6 | 3 | 1 | 120 | 3949 | 9305 | 12 | [0.0000, 0.9996] |
| 13 | 4609@12 | 4609@12 | 1 | 1 | 120 | 4589 | 10565 | 12 | [0.0000, 0.9996] |
| 14 | 3742@24 | 3742@24 | 2 | 1 | 120 | 4437 | 9313 | 12 | [0.0000, 0.9996] |
| 15 | 3278@14 | 3278@14 | 3 | 1 | 120 | 3543 | 7826 | 12 | [0.0000, 0.9994] |
| 16 | 3159@25 | 3159@25 | 7 | 1 | 120 | 4746 | 9214 | 12 | [0.0000, 0.9997] |
| 17 | 4043@15 | 4043@15 | 2 | 1 | 120 | 4089 | 9087 | 12 | [0.0000, 0.9995] |
| 18 | 3768@4 | 3768@4 | 2 | 1 | 120 | 3784 | 9164 | 12 | [0.0000, 0.9995] |
| 19 | 3436@5 | 3436@5 | 2 | 1 | 120 | 3533 | 8533 | 12 | [0.0000, 0.9995] |
| 20 | 4216@4 | 4216@4 | 1 | 1 | 120 | 4016 | 9793 | 12 | [0.0000, 0.9996] |
| 21 | 4338@21 | 4338@21 | 0 | 1 | 120 | 4958 | 10616 | 12 | [0.0000, 0.9997] |
| 22 | 4774@3 | 4774@3 | 1 | 1 | 120 | 4456 | 10735 | 12 | [0.0000, 0.9998] |
| 23 | 3497@3 | 3497@3 | 2 | 1 | 120 | 3420 | 8091 | 12 | [0.0000, 0.9995] |
| 24 | **2602@11** | 2602@11 | 4 | 1 | 120 | 2693 | 6131 | 12 | [0.0000, 0.9994] |
| 25 | 3200@37 | 3200@37 | 3 | 1 | 120 | 4615 | 8540 | 12 | [0.0000, 0.9995] |
| 26 | 3374@19 | 3374@19 | 2 | 1 | 120 | 3961 | 8620 | 12 | [0.0000, 0.9995] |
| 27 | 3515@7 | 3515@7 | 3 | 1 | 120 | 3429 | 8094 | 12 | [0.0000, 0.9994] |
| 28 | 3250@4 | 3250@4 | 2 | 1 | 120 | 2979 | 7213 | 12 | [0.0000, 0.9995] |
| 29 | 3712@6 | 3712@6 | 2 | 1 | 120 | 3629 | 8375 | 12 | [0.0000, 0.9995] |
| 30 | 3136@9 | 3136@9 | 5 | 1 | 120 | 4012 | 9230 | 12 | [0.0000, 0.9996] |
| 31 | 3569@4 | 3569@4 | 2 | 1 | 120 | 3359 | 8189 | 12 | [0.0000, 0.9996] |
| 32 | 4750@5 | 4750@5 | 2 | 1 | 120 | 4525 | 10969 | 12 | [0.0000, 0.9996] |
| 33 | 3964@10 | 3964@10 | 2 | 1 | 120 | 3955 | 9228 | 12 | [0.0000, 0.9996] |
| 34 | 3430@5 | 3430@5 | 0 | 1 | 120 | 3595 | 8707 | 12 | [0.0000, 0.9996] |
| 35 | 4494@4 | 4494@4 | 2 | 1 | 120 | 4050 | 9837 | 12 | [0.0000, 0.9996] |
| 36 | 4141@4 | 4141@4 | 2 | 1 | 120 | 3732 | 9078 | 12 | [0.0000, 0.9996] |
| 37 | 3647@9 | 3647@9 | 2 | 1 | 120 | 3657 | 8540 | 12 | [0.0000, 0.9996] |
| 38 | 3674@34 | 3674@34 | 5 | 1 | 120 | 4835 | 9234 | 12 | [0.0000, 0.9995] |
| 39 | 3803@57 | 3803@57 | 5 | 1 | 120 | 6130 | 9912 | 12 | [0.0000, 0.9996] |
| 40 | 4160@4 | 4160@4 | 0 | 1 | 120 | 3737 | 9120 | 12 | [0.0000, 0.9996] |

The `0.0000` at the bottom of every store/cap range is turn 1, where the
consumption system has not yet published a demand large enough for the estimate
to be meaningful; it is an artifact of the estimator at founding, not a store
that was empty.

---

## 2. PROPAGATION TRACES

Seeds traced: **42** (the ordinary/canonical seed), **24** (holds the sweep's
global minimum, 2602 at turn 11), **16** (most per-settlement exact zeros, 7).
Every traced turn is asserted to reconcile; none failed.

All dt values in the traced windows are **10 years/turn**.

### Seed 42 — lowest aggregate food, turn 8

| turn | pop | dPop | preHarvestStore | harvest | h/eaten | eaten | spoil | overflow | end | capEst | nextCapEst | store/cap |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 7 | 4082 | +31 | 4493 | 59261 | 1.963 | 30185 | 18479 | 10589 | 4501 | 4527.8 | 4564.9 | 0.9941 |
| **8** | 4109 | +27 | 4501 | 52929 | 1.739 | 30433 | 14862 | 7781 | **4354** | 4564.9 | 4591.5 | 0.9538 |
| 9 | 4144 | +35 | 4354 | 65060 | 2.125 | 30610 | 21363 | 12855 | 4586 | 4591.5 | 4630.8 | 0.9988 |

### Seed 42 — first per-settlement exact-zero, turn 54

| turn | pop | dPop | preHarvestStore | harvest | h/eaten | eaten | spoil | overflow | end | capEst | nextCapEst | store/cap | settl.@0 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 53 | 5836 | +52 | 6364 | 61681 | 1.445 | 42685 | 13958 | 5293 | 6109 | 6402.8 | 6466.6 | 0.9541 | 0 |
| **54** | 5877 | +41 | 6109 | 52687 | 1.225 | 42997 | 8695 | 1593 | **5511** | 6466.6 | 6526.6 | 0.8522 | **1** |
| 55 | 5902 | +25 | 5511 | 102662 | 2.359 | 43511 | 35602 | 22540 | 6520 | 6526.6 | 6573.6 | 0.9990 | 0 |

Same signature: harvest 61681 → **52687** → 102662 against near-flat eating
(42685 / 42997 / 43511). One settlement empties; the aggregate does not.

### Seed 24 — global minimum aggregate grain, turn 11 (also its first per-settlement zero)

| turn | pop | dPop | preHarvestStore | harvest | h/eaten | eaten | spoil | overflow | end | capEst | nextCapEst | store/cap | settl.@0 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 10 | 2683 | +20 | 2945 | 40376 | 2.035 | 19841 | 12923 | 7714 | 2843 | 2976.2 | 2998.9 | 0.9553 | 0 |
| **11** | 2693 | +10 | 2843 | 35075 | 1.762 | 19906 | 9913 | 5497 | **2602** | 2998.9 | 3009.2 | 0.8676 | **1** |
| 12 | 2711 | +18 | 2602 | 41886 | 2.088 | 20061 | 13446 | 7977 | 3004 | 3009.2 | 3034.9 | 0.9983 | 0 |

### Seed 16 — lowest aggregate food, turn 25 (4 settlements at exactly 0)

| turn | pop | dPop | preHarvestStore | harvest | h/eaten | eaten | spoil | overflow | end | capEst | nextCapEst | store/cap | settl.@0 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 24 | 4705 | +34 | 4931 | 101255 | 2.919 | 34692 | 39362 | 26933 | 5199 | 5203.8 | 5245.4 | 0.9991 | 0 |
| **25** | 4746 | +41 | 5199 | 51566 | 1.526 | 33784 | 12651 | 7171 | **3159** | 5245.4 | 5286.6 | 0.6022 | **4** |
| 26 | 4633 | **-113** | 3159 | 77706 | 2.205 | 35244 | 25116 | 15224 | 5281 | 5286.6 | 5184.1 | 0.9989 | 0 |

Seed 16 turn 25→26 is the one traced case where a low-food turn is followed by a
real population fall: −113 people, the only negative dPop in any traced window.

### Intra-turn phase attribution (identical shape in all nine traced turns)

Grain after each of the thirteen phases, seed 24 turn 11:

```
clone            2843
catchment        2843   delta 0
harvestweather   2843   delta 0
production      37918   delta +35075     <-- the only inflow
appropriation   37918   delta 0          <-- internal transfer, world total unchanged
consumption      2602   delta -35316     <-- eaten + spoilage + overflow, all at once
price            2602   delta 0
trade            2602   delta 0
housing          2602   delta 0
classmobility    2602   delta 0
migration        2602   delta 0
demographics     2602   delta 0
needsgrievance   2602   delta 0
pathbuild        2602   delta 0
```

Every traced turn in every traced seed has exactly this shape: **two phases move
grain and eleven do not.** `appropriation` moved 0 net grain in all nine traced
turns, confirming it is a pure internal transfer. Everything after `consumption`
— price, trade, housing, classmobility, migration, demographics, needsgrievance,
pathbuild — left the world grain total bit-identical in all nine turns.

### At which point does the outcome become inevitable? — with numbers

**At `production`, within the same turn.** Not earlier, and not carried in from
the previous turn.

The evidence is the decomposition of the ending stock. Take seed 24 turn 11:

- start of turn: 2843
- after production: 37918 (harvest **35075**)
- after consumption: 2602

The turn's outcome is fixed the moment the harvest number lands, because
consumption then removes essentially everything above the granary ceiling. What
distinguishes turn 11 from its neighbours is **only** the harvest: 40376 on turn
10, **35075** on turn 11, 41886 on turn 12, against a near-constant consumption
of 19841 / 19906 / 20061. The previous turn's ending stock is almost irrelevant
— it contributes 2843 units to a turn whose throughput is 35,075.

The same holds for the other two traces:

- seed 42 turn 8: harvest drops 59261 → **52929** (−10.7%) while eating moves
  30185 → 30433 (+0.8%); ending stock 4501 → 4354.
- seed 16 turn 25: harvest drops 101255 → **51566** (−49.1%) while eating moves
  34692 → 33784 (−2.6%); ending stock 5199 → **3159**, the seed's minimum, and
  four settlements land on exactly 0.

The mechanism the phase trace exposes: the store is **capacity-bound on every
turn** (overflow > 0 on 4800 of 4800 turn-events). So on a normal turn the store
ends at ~99.9% of the granary ceiling regardless of how large the harvest was —
seed 16 turn 24 harvested 101,255 and *destroyed* 39,362 to spoilage plus 26,933
to overflow, ending at 5199, ratio 0.9991. That means **the carry-over buffer
conveys no information from a good turn into a bad one**: it is capped at ~1.5
years of demand no matter how good the preceding harvest was. When the harvest
then halves, nothing absorbs it, and the ending stock falls in one step. The
low-food outcome is therefore *created* in `production` and *realised* in
`consumption`, and it is invisible to any earlier phase — catchment and
harvestweather moved grain by 0 in all nine traced turns.

Why it nevertheless never reaches exactly 0: even on the worst traced turn the
harvest (51,566) exceeded that turn's eating (33,784) by a factor of 1.526. Over
seeds 1..40, harvest exceeded eating on 4,760 of 4,800 turns; the 40 exceptions
are each seed's turn 1. The system is not food-scarce at the aggregate at any
point in 120 turns — it is *storage*-limited.

---

## 3. THE PROCYCLICAL HYPOTHESIS, AS A MEASURED NUMBER

Hypothesis under test: population falls → demand falls → capacity falls → more
grain becomes overflow → buffer weakens.

Counted over all consecutive turn pairs in the three traced seeds
(t−1, t, t+1 with t from 1 to 118, 118 pairs per seed):

| seed | P(next-turn capacity falls \| population fell) | baseline P(capacity falls \| population rose) |
|---|---|---|
| 42 | 2/2 = **1.0000** | 0/116 = 0.0000 |
| 24 | 3/3 = **1.0000** | 0/114 = 0.0000 |
| 16 | 6/6 = **1.0000** | 0/112 = 0.0000 |
| **pooled** | **11/11 = 1.0000** | **0/342 = 0.0000** |

The conditional rate is **1.0000 (11 of 11)** and the complementary baseline is
**0.0000 (0 of 342)**: capacity fell on the turn after a population fall every
single time, and never once after a population rise. The demand→capacity link
of the chain is therefore confirmed exactly, with no exception in 353 pairs.

Two honest caveats on what this does and does not prove:

1. The sample of population falls is **small — 11 events in 354 traced turns**.
   These worlds grow almost monotonically (all 40 seeds ended above their
   minimum-turn population), so the antecedent is rare. The rate is 1.0000 on 11
   observations, not on hundreds.
2. The chain's *final* link — "buffer weakens" — is **not supported** by these
   worlds, because the buffer is already saturated: overflow is positive on
   100% of turns regardless of population direction. A falling capacity does
   convert more grain to overflow, but the store was pinned at the ceiling
   before and after, so no additional weakening of the aggregate reserve is
   observable. Seed 16's population fall of −113 at turn 26 is *preceded* by the
   low-food turn 25, i.e. the observed direction there is food → population, not
   population → food.

---

## 4. CONSERVATION

`FoodTurnAccount.Reconciles` (`Residual == 0`, `long` arithmetic, no epsilon)
was asserted on **every turn of every seed**:

- sweep: 40 seeds × 120 turns = **4,800 turns, 0 failures**;
- traces: 3 seeds × 120 turns = **360 turns, 0 failures**.

Total **5,160 turn-accounts, all residual exactly 0**. No grain moved outside
the Ledger anywhere in this evidence.
