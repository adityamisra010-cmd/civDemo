# M4 Dietary Diversity — §1 RECONCILIATION GATE

**Verdict: STOP. SAME PHENOMENON.** Do not implement a second mechanism.

Reconciled against commit `79dec68` (`Merge branch 't4.20-food-legibility' into t4.19-glass-box`)
in a dedicated read-only worktree. No production code was written. All numbers below were
MEASURED on that tree with a throwaway probe against the driven founded world (seed 42, 200 turns,
`DrivenGoldenTests.RunDriven`), then deleted.

Three independent grounds for STOP, any one of which is sufficient:

1. **Same phenomenon.** Both quantities are the Herfindahl concentration of the settlement's
   ACTUAL CONSUMED Sustenance goods. They are two affine rescalings of one number.
2. **The proposed form is forbidden by name** by the ratified ruling that already owns the
   phenomenon (D-035-A, lines 28-30).
3. **Architectural conflict at the integration point.** The stated deprivation invariant cannot be
   honoured by guarding on food, and where it CAN be honoured the bonus is measurably inert.

---

## 1. D-035-A verbatim, and the existing implementation

### 1.1 `docs/d035-needs-aggregation.md`, lines 20-38, quoted verbatim

```
20  ## D-035-A — VARIETY AS SATISFACTION
21
22  Basket satisfaction depends on **diversity as well as quantity**. The same calories from grain
23  alone satisfy Sustenance **less** than the same calories spread across grain / livestock / fish.
24
25  **Implementation.** A concentration term **inside the satisfaction equation**, Herfindahl-style,
26  TUNE-weighted. The same shape applies to Comfort across pottery / cloth.
27
28  **NEVER as a bonus modifier.** This is law 2 (mechanisms over modifiers) and it is the whole
29  difference between a mechanism and a buff: a variety *bonus* added after the fact is a free-
30  floating modifier; a concentration term inside the equation changes what satisfaction *is*.
31
32  **Rationale (director).** Monotony is real deprivation, and it gives trade a purpose beyond
33  scarcity — a settlement that can feed itself entirely on grain still has a reason to import fish.
34
35  **Note for implementation:** a Herfindahl index over basket shares is `H = Σ shareᵢ²`, running
36  from `1/n` (perfectly even across n goods) to `1` (everything from one good). The satisfaction
37  equation should be *decreasing* in H at fixed quantity. State the mapping and its TUNE weight;
38  do not leave "Herfindahl-style" to interpretation.
```

Lines 28-30 are the decisive text. They do not merely prefer the multiplicative form; they name the
additive-bonus form and forbid it, as an application of constitutional law 2.

Also load-bearing: **D-035-C** (lines 72-96) — the carrier test. "Name the physical carrier — a
good, a purse, a building, a policy, a body, a season. If none exists, it is an invented modifier
and is **refused**." The carrier for dietary diversity is *the food goods*, and that carrier is
already spent: it is what the existing term reads.

### 1.2 The existing variety quantity, exactly

`Sim.Core/Systems/NeedsGrievance/NeedsAggregation.cs:57-88`, `VarietyFactor`.

| property | value |
| --- | --- |
| **computed FROM** | `obtained[]`, built in `NeedsGrievanceSystem.Satisfaction` (`NeedsGrievanceSystem.cs:305-350`). Per basket line: `obtained_i = basket_i.PerPersonYear × Fill(prev, settlement, good_i)`. |
| **`Fill`** | `NeedsGrievanceSystem.cs:377-396`: `row.LastConsumptionEatenUnits / (double)row.LastConsumptionDemandUnits`, clamped [0,1]. **Post-clamp EATEN over demanded.** |
| **formula** | `H = Σ share_i²` over obtained; `factor = 1 − varietyWeight × clamp((H − H*)/(1 − H*), 0, 1)` (line 85-87) |
| **N** | the BASKET'S DECLARED LENGTH for that (class, need) — `quantities.Length`, line 60. Documented at lines 44-51 as the load-bearing detail. For Sustenance on shipped data that is **3** for every class. |
| **H\*** | `needs.json` `varietyStandard.shares = [0.70, 0.20, 0.10]` → **H\* = 0.54** (measured: `0.5400000000000001`). A FIXED external standard, not perfect evenness. |
| **what it multiplies** | the quantity term, INSIDE the satisfaction equation: `Satisfaction = clamp(quantity × VarietyFactor(...), 0, 1)` — `NeedsGrievanceSystem.cs:346-350` |
| **range** | [0, 1]. At shipped `varietyWeight = 0.15` the realised range is **[0.85, 1.00]** |
| **zero-consumption behaviour** | `total <= 0` → returns **1.0** (line 65) — NO penalty. Documented at lines 53-55: the quantity term is already zero, so a variety penalty would double-count nothing. |
| **single-good basket** | `n <= 1` → 1.0 (line 61) |
| **result lands in** | `NeedSatisfactionRow.Value` in the `NeedSatisfactions` table |

Shipped Sustenance `varietyWeight = 0.15`; Comfort likewise 0.15; Shelter 0.0.

---

## 2. Downstream path of the existing variety quantity

```
ConsumptionSystem (writes LastConsumptionEatenUnits / LastConsumptionDemandUnits per GoodStockRow)
   -> NeedsGrievanceSystem.Fill
   -> NeedsGrievanceSystem.Satisfaction  (quantity x VarietyFactor)
   -> WorldState.NeedSatisfactions
   -> NeedsGrievanceSystem grievance accrual -> WorldState.Grievances
   -> [STOPS]
```

**It drives no behaviour today.** Verified two ways on `79dec68`:

- `./scripts/check-read-isolation.sh` returns **OK, exit 0**: the `NeedSatisfactions` / `Grievances`
  tables and their row types are referenced only by the owning system, `WorldState` declarations,
  `CanonicalSchema`, `SystemCatalog`, founding/colonization (row CREATION at 0.0, never a read),
  `PathBuildSystem` (interface forwarding only), `ReplayReport`, `Sim.Core/Observability/`, UI and
  tests.
- `docs/d021-stability-doctrine.md:48` — "**M5 (unrest-lite):** ships valves 1, 2, 3, 6, 7 *with*
  the unrest it ships — the brakes install with the gas pedal, never after."

So the only live consumers are READ-ONLY observers: `Sim.Core/Observability/Observer.cs:560-562`,
`Sim.Core/Observability/Explain/CausalChain.cs:606-607`, `Sim.Core/Kernel/ReplayReport.cs:167-169`,
and `Sim.Ui/ViewModel/GrievanceViewModel.cs`. **The existing channel is player-FACING today (it is
displayed and explained) but player-AFFECTING only from M5.**

This matters and cuts both ways. It is why there is no *behavioural* double count in M4 today —
and it is exactly why installing a second, permanent, additive reward now would create one at M5,
in a milestone where nobody would be looking for it.

Independently confirmed: **happiness deliberately does not read the needs tables at all.**
`Sim.Core/State/SettlementHappiness.cs:27-38` states the reason — sourcing happiness from those
rows would make needs state drive behaviour in M4, the precise thing D-021 defers — and notes that
reusing the needs aggregate instead "is a DIRECTOR'S CALL because it turns on D-021, not on
engineering taste." **Happiness therefore has no variety term of any kind today.**

---

## 3. SAME OR DIFFERENT — axis by axis

| axis | D-035-A variety (shipped) | M4 Dietary Diversity (proposed) | same? |
| --- | --- | --- | --- |
| **input** | post-clamp EATEN / demanded per Sustenance good, rescaled by basket rate | post-clamp EATEN units per Sustenance good | **same signal**, one rate-weighted, one raw |
| **statistic** | `H = Σ p_i²` over consumption shares | `H = Σ p_i²` over consumption shares | **identical** |
| **mapping** | `1 − w·(H − H*)/(1 − H*)` | `(1 − H)/(1 − 1/N)` | affine rescalings of the same `H` |
| **normalisation ref.** | fixed external standard H\* = 0.54 | perfect evenness 1/N | different |
| **N** | basket declared length (3 for Sustenance, all classes) | eligible Sustenance goods (3) | **numerically identical on shipped data** |
| **zero case** | total = 0 → 1.0 (no penalty) | total <= 0 → D = 0 (no bonus) | different, and both are "no adjustment" in their own sign convention |
| **range** | [0.85, 1.00] at shipped w | [0, 5] happiness points | different |
| **arithmetic role** | multiplicative term INSIDE the satisfaction equation | ADDITIVE points AFTER an aggregate | different — and this is the forbidden difference |
| **destination** | `NeedSatisfactions` → grievance (inert until M5) | happiness (drives migration + revolt TODAY) | different |
| **player decision rewarded** | import livestock/fish so the diet is not grain-only | import livestock/fish so the diet is not grain-only | **identical** |

**Answer to the director's sharper question: YES, they measure the same underlying player-facing
phenomenon.** The last row is the one that decides it. Different zero cases, ranges and
destinations are real differences of *plumbing*; they are not differences of *phenomenon*. A player
looking at the screen has exactly one lever — "what mix of grain, livestock and fish does my
settlement actually eat" — and both quantities are strictly monotone functions of that one lever,
computed from the same field, over the same good set, with the same index.

Formally: on the shipped data both are functions of the single scalar `H` over the same three
shares. `D = (1 − H)/(1 − 1/3) = 1.5·(1 − H)` and `factor = 1 − 0.15·max(0,(H − 0.54)/0.46)`. For
`H >= 0.54` — which covers the entire shipped world, measured — `factor = 1.15 − 0.3261·H` and
`D = 1.5 − 1.5·H`, so `D` and `factor` are **exactly collinear**: `D = 4.6·factor − 3.79`. They are
not two signals. They are one signal in two units.

---

## 4. DOUBLE COUNTING — the decisive measurement

Measured on `79dec68`, driven founded world, seed 42, turn 200. `N = 3`
(`BasketBook.FoodGoods = [1, 2, 3]` = grain, livestock, fish).

### 4.1 The same cause, both channels, one counterfactual

The player decision is: move the settlement's actual diet from grain-only toward a varied one.

| actual consumed mix | H | existing `VarietyFactor` | existing Sustenance satisfaction | proposed D | proposed bonus |
| --- | --- | --- | --- | --- | --- |
| grain only `[1, 0, 0]` | 1.0000 | 0.8500 | **0.8500** | 0.0000 | **+0.000 pts** |
| shipped peasant basket `[0.9, 0.06, 0.04]` | 0.8152 | 0.9103 | **0.9103** | 0.2772 | **+1.386 pts** |
| the standard `[0.7, 0.2, 0.1]` | 0.5400 | 1.0000 | **1.0000** | 0.6900 | **+3.450 pts** |

One decision. Two rewards. The existing channel pays **+0.15 of Sustenance satisfaction**; the new
channel pays **+3.45 happiness points**.

### 4.2 Are they the same order of magnitude? Yes — measured.

Happiness sensitivity to food sufficiency, housing pinned at 1.0, through the real
`NeedsAggregation.Aggregate(sigma = 0.5, floor = 0.05)` and the real normalisation:

```
foodSuff=1.00 -> happiness=100.000
foodSuff=0.90 -> happiness= 94.184
foodSuff=0.75 -> happiness= 84.289
foodSuff=0.50 -> happiness= 63.702
foodSuff=0.25 -> happiness= 35.553
foodSuff=0.00 -> happiness=  4.306
```

Near the top, 1 happiness point costs about 0.0172 of food sufficiency. The proposed 5-point
maximum is therefore worth **~0.086 of food sufficiency** — against the existing variety term's own
full swing of **0.15 of Sustenance satisfaction** (measured dSat on the shipped world: **0.0897**).
The two channels are the same order of magnitude for the same cause. This is not a nuance term
sitting beside a mechanism; it is a second mechanism of comparable size.

### 4.3 Per-settlement, turn 200 (all 12 settlements)

```
settl | eaten grain/livestock/fish | D      | bonus=5D | happiness | VarietyFactor | dSat
  0   | 137/9/6                   | 0.2738 | 1.369    | 100.000   | 0.9103        | 0.0897
  1   | 149/11/7                  | 0.2968 | 1.484    |  94.349   | 0.9161        | 0.0757
  2   | 116/8/5                   | 0.2791 | 1.395    | 100.000   | 0.9103        | 0.0897
  3   | 181/15/9                  | 0.3197 | 1.599    | 100.000   | 0.9103        | 0.0897
  4   | 145/10/6                  | 0.2755 | 1.377    | 100.000   | 0.9103        | 0.0897
  5   | 186/12/8                  | 0.2698 | 1.349    | 100.000   | 0.9103        | 0.0897
  6   | 73/5/3                    | 0.2739 | 1.369    | 100.000   | 0.9103        | 0.0897
  7   | 83/5/3                    | 0.2460 | 1.230    | 100.000   | 0.9103        | 0.0897
  8   | 274/20/13                 | 0.2961 | 1.480    | 100.000   | 0.9103        | 0.0897
  9   | 83/5/3                    | 0.2460 | 1.230    | 100.000   | 0.9103        | 0.0897
 10   | 146/10/7                  | 0.2882 | 1.441    | 100.000   | 0.9103        | 0.0897
 11   | 62/4/3                    | 0.2810 | 1.405    | 100.000   | 0.9103        | 0.0897
```

Two further findings fall out of this table:

- **The bonus is inert where it is safe.** Eleven of twelve settlements are already at happiness
  **100.000**. Adding 1.23-1.60 points inside a 0..100 bound clamps to nothing. The mechanic would
  be observable on exactly one settlement in the shipped world.
- **The bonus is anti-correlated with need.** It is largest for well-supplied settlements that
  already score 100 and smallest for the deprived ones it would have to be guarded away from.

### 4.4 The verdict on double counting

**It is double counting in the director's sense.** One player decision — importing livestock and
fish — is rewarded twice, in two quantities, at comparable magnitude, through two computations of
the same index over the same field.

The honest qualification, stated because it is the strongest argument against this verdict: *in M4
as it stands today*, the grievance channel drives no behaviour, so the two rewards do not currently
compound in any simulated outcome. If "double counting" meant strictly "two behavioural effects
today", the answer would be no. That reading is rejected for three reasons:

1. The grievance channel is already **displayed** to the player (`GrievanceViewModel`, the explain
   chain). Two on-screen quantities both rising because the player bought fish is double counting a
   player can *see*, whatever the sim does with it.
2. D-021 schedules the grievance channel to acquire teeth at **M5**. The bonus would be shipped
   permanently in M4 and would become a genuine behavioural double count one milestone later, with
   no packet at that point looking for it.
3. It is moot regardless: D-035-A:28-30 forbids the additive form outright, independently of
   whether the double count is behavioural yet.

**What would change my mind.** I would withdraw the double-counting finding if any of these were
true, and none is:
- if the new mechanic read a field the existing one does not (it reads the same
  `LastConsumptionEatenUnits`);
- if it were computed over a different good set (both are the Sustenance goods, N = 3);
- if it were a different statistic (both are `Σ p²`);
- if the two were not monotone in the same direction of the same player action (they are, and on
  the shipped range they are exactly collinear — §3);
- if D-035-A had been silent on the additive form (it names and forbids it).

I would *not* withdraw it on the grounds that the destinations differ, that the zero cases differ,
or that grievance is inert in M4. Those were tested and are not sufficient.

---

## 5. If it had been DIFFERENT — the distinction that would have had to be written

It is not different, so no such distinction can honestly be written. Recording the attempt is part
of the evidence: every candidate distinction reduces to plumbing rather than phenomenon.

- *"One is a penalty for monotony, the other a reward for variety."* Same axis, opposite sign, same
  index. A monotone rescaling is not a second phenomenon.
- *"One measures against a nutritional standard, the other against perfect evenness."* Different
  reference constants for the same statistic. Tuning, not semantics.
- *"One feeds grievance, the other happiness."* Different destinations for the same signal — which
  is what double counting IS.

---

## 6. ELIGIBLE SET and N

**Yes, an authoritative definition exists, and it is the right one.**

`Sim.Core/Systems/Consumption/BasketBook.cs:77-82, 99` — `FoodGoods` is built as the distinct goods
appearing on a basket line whose `Need == SustenanceNeedId`, sorted ascending by good id:

```csharp
var food = new List<GoodId>();
for (int i = 0; i < lines.Length; i++)
    if (lines[i].Need == SustenanceNeedId && !food.Contains(lines[i].Good)) food.Add(lines[i].Good);
food.Sort(static (a, b) => a.Value.CompareTo(b.Value));
_foodGoods = [.. food];
```

Its doc comment (`BasketBook.cs:92-98`) states the property that makes it the correct denominator:
food lines are denominated in person-year-equivalents of nutrition, so "a unit of any of them feeds
the same person for the same time", which is what makes shares across them meaningful at all.

It is the set already used by `ConsumptionSystem`'s substitution, by
`ClassMobilitySystem.cs:131`, and by the T4.20 food observability surface
(`Observer.cs:411-439`, `SettlementRecord.cs:65-77`) — so using it introduces no second food set.

**N on shipped data = 3** (goods 1, 2, 3 = grain, livestock, fish) — measured, and independently
pinned by `Sim.Ui.Tests/FoodFlowUiTests.cs:64` (`Assert.Equal(3, f.FoodGoods.Length)`).

**N is DATA-DEPENDENT, not fixed.** It is derived from `needs.json` `baskets.entries` at load time.
Any future Sustenance basket line for a new good changes N and therefore silently rescales
`(1 − 1/N)`. That is a further reason not to introduce a second normalisation with its own N: the
existing term normalises against the fixed external standard H\* = 0.54, which is immune to this.

---

## 7. INPUT FIELD

**`GoodStockRow.LastConsumptionEatenUnits`** — declared at `Sim.Core/State/WorldState.cs:256`.

| requirement | status | evidence |
| --- | --- | --- |
| post-clamp EATEN, not pre-clamp demand | **CONFIRMED** | `ConsumptionSystem.cs:228-229`: `eaten` is the return of `ctx.Ledger.Flow(..., OverdrawPolicy.ClampToAvailable)`. The pre-clamp figure is the SEPARATE field `LastConsumptionDemandUnits` (`WorldState.cs:245` notes it is deliberately pre-clamp, as the price signal, T3.4/D-033). |
| per settlement and good | **CONFIRMED** | it is a field on `GoodStockRow`, keyed `(Settlement, Good)`; `GoodStockIndex.IndexOf(stores, settlement, good)` |
| zeroed each turn | **CONFIRMED** | `ConsumptionSystem.cs:113-121` zeroes `LastConsumptionDemandUnits` and `LastConsumptionEatenUnits` for **every** stock row of the settlement before writing the turn's values — explicitly including goods outside the baskets, citing the T3.3 staleness precedent. |
| serialized | **CONFIRMED** | `CanonicalSchema.cs:309` writes it; `GoodStockRowWidth` at `CanonicalSchema.cs:112` accounts for it; schema **v17** (T3.5, D-035) is the version that added it (`CanonicalSchema.cs:51`). |
| type | `long` — correct, it is a conserved-goods quantity |

It is the right field. It is also **already the field the existing variety term reads**, through
`NeedsGrievanceSystem.Fill` at `NeedsGrievanceSystem.cs:395`. That identity is the core of the
finding in §3.

---

## 8. INTEGRATION POINT — and why it is an independent STOP

### 8.1 The invariant cannot be honoured by guarding on food

Measured through `NeedsAggregation.Aggregate(sigma = 0.5, floor = 0.05)` and
`SettlementHappiness.Of`'s normalisation:

```
food = 0.0, housing = 1.0  ->  aggregate = 0.090909  ->  happiness =  4.306220   (NOT zero)
food = 0.0, housing = 0.0  ->  aggregate = 0.05      ->  happiness =  0          (exactly)
```

`SettlementHappiness.cs:186-211` documents this on purpose: the CES `satisfactionFloor` is a
mechanism parameter, the all-zero aggregate equals the floor exactly, and the scale is anchored so
that **total deprivation across every factor is the only way to reach exactly 0**. Lines 66-70 say
the same of `RevoltThreshold`: "Zero is reachable only when every factor is zero — an unfed,
unhoused population — so this is not a near-miss band."

**Consequence: there is no existing "food-deprivation condition" that is a food-only predicate.**
The only condition under which the shipped model yields exactly 0 is the aggregate one. Therefore:

- **Guarding on FOOD satisfaction is the wrong reading** and would be a behaviour change, not a
  guard. A settlement with `food = 0, housing = 1` sits at 4.306 today. A food-only guard would
  either leave the bonus applicable there (violating a food-deprivation invariant) or, if it also
  forced 0, would newly drive that settlement into `IsRevoltReady` — silently rewriting D-021's
  revolt condition from inside a diversity packet.
- **Guarding on the AGGREGATE is the correct reading** of "the existing food-deprivation condition
  is active". The exact predicate already exists:
  `SettlementHappiness.IsRevoltReady(world, settlement, cfg)`, defined at
  `SettlementHappiness.cs:220-222` as `Of(...) <= RevoltThreshold`, with
  **`SettlementHappiness.RevoltThreshold = 0.0`** (`SettlementHappiness.cs:70`) — value verified on
  this tree. Any guard must use that constant, not a literal and not a new one.

### 8.2 Can adding points after the aggregate violate the invariant when food satisfaction is zero but consumption is non-zero?

Traced, and the answer is **no, but only by luck, and the luck is not load-bearing enough to rely
on**. `FoodSufficiency = 1 − DeficitRatio` (`SettlementHappiness.cs:118-129`), and
`DeficitRatio = unmet demand / integer demand` over the whole basket (`WorldState.cs:283`). Food
sufficiency reaches exactly 0 only at `DeficitRatio = 1` — nothing eaten at all — in which case
total actual consumption is 0 and the proposed rule already yields `D = 0`. So the two zeros
coincide today.

But they coincide *through a coupling nobody declared*: `DeficitRatio` is computed over ALL basket
goods, not food alone. It is a coincidence of the current basket composition, not a property. A
future non-food basket line, or any change to how the deficit is aggregated, breaks it silently. An
invariant that holds by coincidence is exactly the kind of thing the constitution's
POPULATED-table/semantic-pin culture exists to refuse.

### 8.3 The correct shape, if the director still wants diversity to reach happiness

**Do not add points after the aggregate.** Put the term inside the equation, where D-035-A requires
it and where the invariant becomes structural rather than guarded:

```
SettlementHappiness.FoodSufficiency :=
    clamp(1 − DeficitRatio, 0, 1)
      × NeedsAggregation.VarietyFactor(eatenPerFoodGood, varietyWeight, H*)
```

This shape:

- **cannot** violate the deprivation invariant: food sufficiency 0 × anything = 0, and all-factors-
  zero still normalises to exactly 0. **No guard is needed at all**, so no guard can be wrong.
- creates **no second happiness system** and **no second diversity index** — it calls the existing
  `NeedsAggregation.VarietyFactor`, the same function `NeedsGrievanceSystem.Satisfaction` calls.
- obeys D-035-A:25-30 (a concentration term inside the satisfaction equation, never a bonus).
- respects the `SettlementHappiness.cs:27-38` isolation rule, because it reads the PRIMARY
  `GoodStocks` rows — not `NeedSatisfactions` — so `check-read-isolation.sh` stays green and D-021
  is untouched.
- is bounded by construction, so no 0..100 clamp interacts with it.

**This still requires a director ruling and an ADR.** It changes a happiness reading that feeds
migration and revolt, and `SettlementHappiness.cs:37-38` states in terms that the question of what
happiness may read "is a DIRECTOR'S CALL because it turns on D-021, not on engineering taste."

### 8.4 The smallest design change of all

If the intent is simply that dietary diversity should matter *more* and be *visible*: it already
exists and it is already tunable. Raise `needs.json` `needs[Sustenance].varietyWeight` above 0.15,
and/or retune `varietyStandard.shares`, and surface the existing factor in the UI as "dietary
diversity". Tuning data files is always allowed and needs no ruling. This costs zero new mechanism,
zero new index, zero new field, and zero risk to the deprivation invariant.

---

## 9. Recommendation

1. **STOP** the Dietary Diversity packet as specified. Do not implement `DiversityBonus = 5.0 × D`
   as an additive happiness contribution.
2. Report to the director: the phenomenon is **already implemented**, at
   `NeedsAggregation.VarietyFactor`, reading the same field, over the same good set, with the same
   index — and the proposed additive form is forbidden by name at `d035-needs-aggregation.md:28-30`.
3. If more effect is wanted: **tune** (§8.4). No ruling required.
4. If diversity must reach happiness: **ruling + ADR**, and the shape is §8.3 — multiplicative
   inside `FoodSufficiency`, calling the existing function, with no guard.

## Method note

Read-only worktree pinned to `79dec68` (T2.1 precedent), separate from this documentation worktree.
No production code written; the measurement probe was a single throwaway xunit fact under
`Sim.Tests/`, deleted after the run, with `git status --porcelain` left empty. The branch
`m4-diversity-draft` was neither read nor consulted; this verdict was reached independently.
