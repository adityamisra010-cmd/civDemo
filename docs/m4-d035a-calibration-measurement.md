# D-035-A VARIETY — CALIBRATION MEASUREMENT (GATE TWO)

**Status:** measurement only. **No production change, no golden change, no code change** is proposed
or made by this document. It is evidence for the director's fifth gate.

**Measured at** `79dec68` (`Merge branch 't4.20-food-legibility' into t4.19-glass-box`), in a
detached read-only worktree pinned to that commit (ADR-015 §6 / T2.1 precedent). The measuring
probe was a throwaway file, deleted after the run; the measured worktree ended with
`git status --porcelain` empty and nothing committed there.

**Scope, per the director's ruling.** The proposed second dietary-diversity mechanism is REJECTED as
double counting. D-035-A is the SOLE authoritative food-variety mechanism for M4. Nothing here
proposes a second diversity index, a Simpson or Shannon index, a happiness bonus, an additive
post-aggregate modifier, a multiplicative replacement, a food-only deprivation guard, a new
migration/demographic/revolt term, new food-variety state, or a new serialized field. The only
question examined is whether D-035-A's existing contribution is appropriately weighted.

---

## 0. HEADLINE

1. **The mechanism drives NO behaviour in M4.** `SettlementHappiness` deliberately does not read the
   needs tables, and a CI gate enforces that. There is no path from the variety factor to happiness,
   migration, revolt, demographics, or any other behaviour. This is confirmed at source, not
   inferred. **It is display, explanation and telemetry only.**
2. **The realised factor is, in practice, a per-class CONSTANT.** In the founded world 99.7% of
   settlement-class-turns sit at exactly one of two values fixed by the class's *declared* basket
   (peasant 0.910261, artisan 0.969543). Raising `varietyWeight` deepens a constant offset; it does
   not make the mechanism more responsive, because there is almost nothing for it to respond to.
3. **Recommendation: NO PRODUCTION CHANGE.** 0.15 is already visible where the mechanism is visible
   at all, and no candidate weight buys behavioural salience, because there is no behaviour to be
   salient in. See §8.

---

## 1. THE CURRENT WEIGHT AND STANDARD (read from the data file)

From `Sim.Data/content/needs.json` at `79dec68`:

| Parameter | Value | Location |
| --- | --- | --- |
| `varietyWeight` (Sustenance, need 1) | **0.15** | `needs[0].varietyWeight` |
| `varietyWeight` (Comfort, need 6) | **0.15** | `needs[5].varietyWeight` |
| `varietyWeight` (Shelter, need 2) | 0.0 | `needs[1].varietyWeight` |
| `varietyStandard.shares` | **[0.70, 0.20, 0.10]** | `varietyStandard.shares` |
| H\* (derived) | **0.54** (loaded as `0.5400000000000001`) | `VarietyStandardConfig.Concentration` |

**Note for the director, not previously stated in the framing:** `varietyWeight = 0.15` is carried by
**two** needs, Sustenance *and* Comfort (pottery/cloth). Any tuning of "the variety weight" must say
which of the two it means. All measurements below are for **Sustenance**, need 1.

The mechanism, re-confirmed at source in `Sim.Core/Systems/NeedsGrievance/NeedsAggregation.cs`
(`VarietyFactor`, lines 57-88):

```
H      = Σ shareᵢ²  over the basket's obtained quantities
excess = clamp((H − H*) / (1 − H*), 0, 1)
factor = clamp(1 − varietyWeight × excess, 0, 1)
```

with `n` the basket's **declared** length, `Length ≤ 1` and an empty/zero basket returning 1.0. It
multiplies the quantity term inside `Satisfaction`
(`NeedsGrievanceSystem.cs:347-350`), lands in `NeedSatisfactionRow.Value`, and thence into grievance.

---

## 2. THE REALISED DISTRIBUTION

**Datasets.** Both are seed 42, canonical worldgen (1024², N = 12), 300 turns, stepped one turn at a
time so every intermediate world is observed.

| Dataset | Turns | Settlements | Settlement-turns | Settlement-class-turn rows (Sustenance) |
| --- | --- | --- | --- | --- |
| **Founded canonical** (no orders) | 300 | 12 | **3,600** | **6,892** |
| **Driven golden** (`DrivenGoldenTests.DrivingOrders`, 60 SectorAllocation orders at turn 2) | 300 | 12 | **3,599** | **5,187** |

The row count is below settlement-turns × classes because a class contributes a row only while it is
active (artisans and merchants must emerge), and the driven world's settlement-turn count is 3,599
rather than 3,600 because one settlement has no Sustenance row on its founding turn.

**Harness validity.** The probe recomputes `quantity`, `H` and `factor` from the *previous* world
using the system's own public `NeedsGrievanceSystem.Fill` and `NeedsAggregation.VarietyFactor`, then
checks `quantity × factor` against the shipped `NeedSatisfactionRow.Value`.
**Mismatches: 0 of 6,892 (founded) and 0 of 5,187 (driven), at exact equality (tolerance 1e-15).**
The offline sweep in §7 is therefore recomputation of a verified identity, not a re-simulation.

### 2.1 The factor

| Dataset | min | median | p90 | p95 | max |
| --- | --- | --- | --- | --- | --- |
| Founded | 0.850000 | 0.910261 | 0.969543 | 0.969543 | 0.974579 |
| Driven | 0.850000 | 0.910261 | 0.969543 | 0.969543 | 1.000000 |

### 2.2 The contribution — satisfaction removed versus a factor of 1.0

Contribution is `quantity × (1 − factor)`, in satisfaction units (the units of
`NeedSatisfactionRow.Value`, which is in [0,1]).

| Dataset | min | median | p90 | p95 | max | mean |
| --- | --- | --- | --- | --- | --- | --- |
| Founded | 0.024133 | 0.089739 | 0.089739 | 0.089739 | 0.150000 | 0.061523 |
| Driven | 0.000000 | 0.089739 | 0.089739 | 0.089739 | 0.150000 | 0.068775 |

**The median settlement-class-turn loses 0.0897 satisfaction units to variety** — about 9% of the
Sustenance need — and the worst case loses the full 0.15.

### 2.3 The load-bearing shape: it is nearly a constant

Founded-world factor histogram (all 6,892 rows):

| factor | rows | share | what it is |
| --- | --- | --- | --- |
| 0.910261 | 3,585 | 52.02% | **peasant declared basket**, H = 0.8152, fully supplied |
| 0.969543 | 3,289 | 47.72% | **artisan declared basket**, H = 0.6334, fully supplied |
| 0.850000 | 12 | 0.17% | turn 1 only, all 12 settlements: grain-only, H = 1, full penalty |
| 6 other values | 6 | 0.09% | transient partial fills |

**Nine distinct values across 6,892 rows, and two of them account for 99.74%.** In the undriven
canonical world the variety factor is not a dynamic signal — it is a per-class constant determined by
the *declared* basket, because `Fill` is 1.0 for all three foods on almost every turn.

The driven world is the interesting contrast: 751 distinct factor values, but the same two constants
still account for **84.01%** of rows (0.910261 at 58.18%, 0.969543 at 25.83%). Trade genuinely moves
the mechanism — and in **27 rows (0.52%)** the realised diet reaches H ≤ H\* and the factor is
**exactly 1.0**, i.e. D-035-A's "reason to trade" is fully discharged. That is the mechanism working
as designed, and it is rare.

---

## 3. THE DISTRIBUTION BY SETTLEMENT

**Founded:**

| settlement | n | min | median | max |
| --- | --- | --- | --- | --- |
| 0 | 587 | 0.850000 | 0.910261 | 0.969543 |
| 1 | 591 | 0.850000 | 0.910261 | 0.969543 |
| 2 | 557 | 0.850000 | 0.910261 | 0.973660 |
| 3 | 572 | 0.850000 | 0.910261 | 0.969543 |
| 4 | 573 | 0.850000 | 0.910261 | 0.970102 |
| 5 | 580 | 0.850000 | 0.910261 | 0.969543 |
| 6 | 570 | 0.850000 | 0.910261 | 0.969543 |
| 7 | 596 | 0.850000 | 0.910261 | 0.969543 |
| 8 | 596 | 0.850000 | 0.910261 | 0.969543 |
| 9 | 543 | 0.850000 | 0.910261 | 0.969543 |
| 10 | 573 | 0.850000 | 0.910261 | 0.974579 |
| 11 | 554 | 0.850000 | 0.910261 | 0.969543 |

**Driven:** every settlement has min 0.850000 and median 0.910261; eight of twelve reach max
1.000000 (settlements 1, 2, 4, 5, 7, 8, 10, 11), four reach only 0.969543 (0, 3, 6, 9). Settlement 2
is the only one whose median differs at all, at 0.912249.

**No settlement differs materially from the others in either world.** The medians are identical to
six decimals across all twelve in both datasets. The variation in this mechanism is **between
classes**, not between settlements:

| class | founded min / median / max | driven min / median / max |
| --- | --- | --- |
| 1 (peasant) | 0.850000 / 0.910261 / 0.913676 | 0.850000 / 0.910261 / 1.000000 |
| 2 (artisan) | 0.969543 / 0.969543 / 0.974579 | 0.969543 / 0.969543 / 1.000000 |

In the driven world the split by max is the SectorAllocation grouping: the four settlements capped at
0.969543 are exactly `settlementIndex % 3 == 0` (the granary group, 0/3/6/9), which produces food
surplus and thin non-food stocks and therefore never diversifies its own diet off grain. The eight
that reach 1.0 are the quarry and workshop groups, which import. That is a sensible, legible result
and it is the only settlement-level structure the measurement found.

---

## 4. RELATIONSHIP TO FOOD COMPOSITION

| correlation | founded | driven |
| --- | --- | --- |
| factor vs **H** | **−1.000000** (r² = 1.0000) | −0.999734 (r² = 0.9995) |
| factor vs **eaten-share of the staple** | −0.999886 (r² = 0.9998) | −0.984301 (r² = 0.9688) |

**Essentially all of the factor's variance is composition — 100.00% in the founded world and 99.95%
in the driven world.** This is not an empirical surprise; it is arithmetic. Over the unclamped range
the factor is an exact affine function of H, `factor = 1 − w·(H − H*)/(1 − H*)`, so r² = 1 is the
expected result and its appearance is a check that the probe is measuring the right quantity. The
driven world's 0.05% shortfall from unity is entirely the clamp at `factor = 1.0` (the 27 rows where
H fell below H\*).

Against the staple share the relationship is slightly looser (r² = 0.9688 driven) because H depends
on the full three-way split, not the staple share alone — a diet of 60% grain / 40% fish and one of
60% grain / 20% livestock / 20% fish share a staple share and differ in H.

**There is nothing else in the factor.** It is a pure function of realised food composition, and the
measurement confirms it behaves as one.

---

## 5. RELATIONSHIP TO HAPPINESS — CONFIRMED: THERE IS NO PATH

**The expectation in the framing is CORRECT and I confirm it at source.**

`Sim.Core/State/SettlementHappiness.cs` computes happiness from exactly two factors:

- `FoodSufficiency` — reads `world.ConsumptionDeficits[i].DeficitRatio`, returns `1 − deficit`.
- `HousingSufficiency` — reads `world.Buckets` and `world.Housing`.

It aggregates them with `NeedsAggregation.Aggregate` (D-035-B CES) using weights pulled from the
needs *registry*, normalises against the satisfaction floor, and scales to 0..100. **It never reads
`NeedSatisfactions` or `Grievances`.** The type's own header states the reason explicitly:

> "THIS TYPE DELIBERATELY DOES NOT READ THEM. D-021 rules that grievance drives no BEHAVIOUR until
> M5, and `scripts/check-read-isolation.sh` enforces that on the needs tables as well as the
> grievance table. Since happiness feeds migration (a behaviour), sourcing it from those rows would
> make needs state drive behaviour in M4 — the precise thing D-021 defers."

Note what this means mechanically: happiness's food factor reuses the *same primary signals* by a
*duplicated* calculation that deliberately **omits the variety term**. The variety factor is applied
inside `NeedsGrievanceSystem.Satisfaction` and nowhere else. `SettlementHappiness.FoodSufficiency` is
a pure deficit reading with no composition term at all.

**Measured, as a cross-check on the reading:**

| correlation | founded | driven |
| --- | --- | --- |
| factor vs happiness | **+0.020142** | **−0.035208** |

Both are negligible, and — critically — **they have opposite signs in the two worlds**, which is what
incidental co-variation with no causal channel looks like. If a path existed the sign would be stable
and positive. It is not a weak relationship; it is no relationship.

**This is a central finding for the calibration question and I state it plainly: changing
`varietyWeight` cannot move happiness by any amount, in any world, at any weight.** The `+5 happiness
points` figure attached to the rejected mechanism is not merely non-authoritative — it is
unreachable by this mechanism at any weight whatsoever, including 1.0.

---

## 6. RELATIONSHIP TO FOOD SUFFICIENCY, AND DOWNSTREAM GATES

### 6.1 Food sufficiency

| correlation | founded | driven |
| --- | --- | --- |
| factor vs `FoodSufficiency` | −0.001231 | −0.038626 |
| factor vs `quantity` (the need's own quantity term) | −0.002492 | −0.169218 |

**The variety factor is orthogonal to food sufficiency.** This is by design and is the point of
D-035-A: it is a *composition* term, not a *quantity* term, and the substitution rule in
`Satisfaction` (unmet non-staple demand falls back on the staple) is precisely what decouples them —
a fishery failure costs nutrition only if the grain store cannot cover it, and costs variety either
way. The mild negative correlation in the driven world (−0.17 against quantity) is that rule visibly
at work: settlements short of non-staples fall back on grain, which raises H and lowers the factor
while the quantity term also falls.

### 6.2 Every consumer of `NeedSatisfactions` and `Grievances`

Enumerated from the tree at `79dec68` (excluding `Sim.Tests`/`Sim.Ui.Tests`):

| Consumer | Reads or writes | M4 role |
| --- | --- | --- |
| `Systems/NeedsGrievance/NeedsGrievanceSystem.cs` | writes both; reads `prev.Grievances` for its own Euler step | **owner** |
| `State/WorldState.cs` | table/row declarations | plumbing |
| `Kernel/CanonicalSchema.cs` | serialization | plumbing |
| `SystemCatalog.cs` | ownership handout | plumbing |
| `Worldgen/WorldFounding.cs` | **writes** `GrievanceRow(…, 0.0)` at founding | seeding, never reads |
| `Systems/Colonization/ColonizationSystem.cs` | **writes** `GrievanceRow(newId, cls, 0.0)` for a new settlement (line 269-278) | seeding, never reads |
| `Systems/PathBuild/PathBuildSystem.cs` | forwards `prev` tables to satisfy the `NetworkOverlayView` interface (lines 340-341) | **interface completeness only**; routes over network tables |
| `Kernel/ReplayReport.cs` | reads both, writes JSONL | **observation-only**, outside the determinism surface (ADR-009) |
| `Observability/Observer.cs`, `Explain/CausalChain.cs`, `Explain/GrievanceExplanation.cs`, `Explain/ExplainRows.cs` | read both | **observation-only** (T4.19 glass-box) |
| `Sim.Ui/ViewModel/HudModel.cs`, `NeedsPanelModel.cs`, `HistoryBuffer.cs` | read both | **display-only** |

**Behaviour-driving consumers: none.** The only two sim systems that touch these tables outside the
owner — Colonization and PathBuild — **write a zero row** and **forward a reference** respectively.
Neither reads a value.

### 6.3 D-021: does grievance have teeth before M5?

**No, and it is enforced mechanically rather than by convention.**
`scripts/check-read-isolation.sh` greps `Sim.Core`, `Sim.Data` and `Sim.Cli` for `Grievances`,
`NeedSatisfactions`, `GrievanceRow`, `NeedSatisfactionRow` and **fails CI** on any hit outside a
documented path allowlist, on the stated ground that "grievance drives no behavior until M5
(D-021)". It runs locally and as a CI gate on every push.

`docs/m4-exit-inventory.md:313` states the same in the director's own settled list:
**"D-021 stays as-is. Needs/grievance state drives no behaviour before M5. Happiness's deliberate
duplication of primary-signal calculations, instead of reading `NeedSatisfaction`, is accepted for
exactly this reason. Do not activate D-021 as a side effect of M5 startup."**

### 6.4 Does the factor materially change any downstream gate?

The only gate the variety factor can reach at all is the **Tier A gate inside the needs system
itself** (`NeedsAggregation.ApplyTierAGate`, floor 0.5): a gate need below 0.5 satisfaction has its
weight scaled superlinearly and collapses upper-need weights. Its output feeds aggregate satisfaction
→ grievance accrual → the `Grievances` table — **which drives nothing** (§6.2, §6.3).

Measured at the shipped weight:

| Dataset | settlement-class-turns with Sustenance satisfaction < 0.5 | caused by variety alone |
| --- | --- | --- |
| Founded | **0** of 6,892 | 0 |
| Driven | **4** of 5,187 (0.08%) | **0** — all four have `quantity < 0.5` already |

**At `varietyWeight = 0.15` there is not one settlement-turn in either world where the variety factor
pushes a need across a gate it would not otherwise have crossed.** The founded world's minimum
Sustenance satisfaction is 0.8500, far above the floor. The driven world's four sub-floor rows are
famine rows whose quantity term is already below 0.5; variety deepens them but does not create them.

**The mechanism drives NO behaviour in M4.** What it does drive, quantified:

- **Display.** `HudModel` and `NeedsPanelModel` render `NeedSatisfactionRow.Value` as `{v:F2}` — two
  decimal places. A player reading the settlement HUD sees `Sustenance: 0.91` for peasants and
  `Sustenance: 0.97` for artisans instead of `1.00`.
- **Explanation.** `GrievanceExplanation` and `CausalChain` reproduce the satisfaction and its
  contribution to grievance by calling the simulation's own public functions, so the variety term is
  attributable in the glass-box explain output.
- **Telemetry.** `ReplayReport` writes per-class `satisfaction` and `grievance` to JSONL;
  `Observer`/`TelemetryWriter` and `HistoryBuffer` carry the same values.

---

## 7. THE OFFLINE SWEEP

Recomputed from the recorded per-settlement-turn `H` and `quantity` under each candidate weight. **No
production code and no golden was modified, and the simulation was not re-run.** This is sound
because the sweep only re-evaluates `factor = 1 − w·excess(H)` on recorded H, and the identity
`satRow == quantity × factor` was verified exactly on every one of the 12,079 recorded rows (§2).

**A stated limitation of the method, and the reason it is nevertheless the right one.** Recomputation
holds `H` and `quantity` fixed at their shipped-weight trajectories. It therefore does not capture any
feedback from a changed weight back into the world. In M4 there is no such feedback — §5 and §6
establish that the factor reaches no behaviour, so the trajectory is provably invariant under
`varietyWeight` and the recomputation is **exact, not approximate**. This would stop being true at M5
when D-021 activates, and a sweep done then would have to re-simulate.

### 7.1 Founded canonical world (n = 6,892)

| w | fac.min | fac.med | fac.p90 | fac.p95 | fac.max | removed.med | removed.p95 | removed.max | removed.mean |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **0.15** (current) | 0.8500 | 0.9103 | 0.9695 | 0.9695 | 0.9746 | 0.0897 | 0.0897 | 0.1500 | 0.0615 |
| 0.20 | 0.8000 | 0.8803 | 0.9594 | 0.9594 | 0.9661 | 0.1197 | 0.1197 | 0.2000 | 0.0820 |
| 0.25 | 0.7500 | 0.8504 | 0.9492 | 0.9492 | 0.9576 | 0.1496 | 0.1496 | 0.2500 | 0.1025 |
| 0.30 | 0.7000 | 0.8205 | 0.9391 | 0.9391 | 0.9492 | 0.1795 | 0.1795 | 0.3000 | 0.1230 |
| 0.40 | 0.6000 | 0.7607 | 0.9188 | 0.9188 | 0.9322 | 0.2393 | 0.2393 | 0.4000 | 0.1641 |
| 0.50 | 0.5000 | 0.7009 | 0.8985 | 0.8985 | 0.9153 | 0.2991 | 0.2991 | 0.5000 | 0.2051 |

### 7.2 Driven golden world (n = 5,187)

| w | fac.min | fac.med | fac.p90 | fac.p95 | fac.max | removed.med | removed.p95 | removed.max | removed.mean | minSat | rows < 0.5 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **0.15** (current) | 0.8500 | 0.9103 | 0.9695 | 0.9695 | 1.0000 | 0.0897 | 0.0897 | 0.1500 | 0.0688 | 0.1000 | 4 |
| 0.20 | 0.8000 | 0.8803 | 0.9594 | 0.9594 | 1.0000 | 0.1197 | 0.1197 | 0.2000 | 0.0917 | 0.1000 | 4 |
| 0.25 | 0.7500 | 0.8504 | 0.9492 | 0.9492 | 1.0000 | 0.1496 | 0.1496 | 0.2500 | 0.1146 | 0.1000 | 4 |
| 0.30 | 0.7000 | 0.8205 | 0.9391 | 0.9391 | 1.0000 | 0.1795 | 0.1795 | 0.3000 | 0.1375 | 0.1000 | 5 |
| 0.40 | 0.6000 | 0.7607 | 0.9188 | 0.9188 | 1.0000 | 0.2393 | 0.2393 | 0.4000 | 0.1834 | 0.1000 | 10 |
| 0.50 | 0.5000 | 0.7009 | 0.8985 | 0.8985 | 1.0000 | 0.2991 | 0.2991 | 0.5000 | 0.2292 | 0.1000 | 19 |

The 27 rows at `factor = 1.0` are **invariant across the whole sweep** — a diet at or below H\* takes
no penalty at any weight, so the "fully diversified" end of the scale is fixed and only the penalised
end stretches. The `max` column in the founded world moves (0.9746 → 0.9153) because no founded row
ever reaches H ≤ H\*.

### 7.3 New Tier A gate activations introduced by the calibration alone

Rows with Sustenance satisfaction < 0.5 at the candidate weight that were ≥ 0.5 at w = 0.15
(driven world; the founded world has none at any candidate):

| w | new sub-floor rows | rows where variety ALONE crosses the floor |
| --- | --- | --- |
| 0.20 | **0** | 0 |
| 0.25 | **0** | 0 |
| 0.30 | 1 (turn 283, settlement 11, class 1: 0.5237 → 0.4974) | 1 |
| 0.40 | 6 | 6 |
| 0.50 | 15 | 15 |

**0.20 and 0.25 introduce no new gate activation anywhere in either world. 0.30 and above begin to
create sub-floor settlement-turns that the shipped weight does not create** — and to do so on rows
whose *quantity* term is above 0.5, i.e. the variety term alone becomes the cause. Those crossings
still reach no behaviour (§6.4), but they are a real change in the mechanism's internal character:
above roughly 0.25, composition alone starts to register as a Tier A gate failure, which is a claim
the mechanism was not designed to make.

### 7.4 What a player would actually SEE at each weight

Since the mechanism is display-only, "perceptibly meaningful" is a question about the rendered
number. `Sim.Ui` renders satisfaction as `{v:F2}` — two decimals, 0.00-1.00.

| w | peasant reads | artisan reads | grain-only reads | peasant delta vs an unpenalised 1.00 |
| --- | --- | --- | --- | --- |
| **0.15** (current) | **0.91** | **0.97** | 0.85 | −0.09 |
| 0.20 | 0.88 | 0.96 | 0.80 | −0.12 |
| 0.25 | 0.85 | 0.95 | 0.75 | −0.15 |
| 0.30 | 0.82 | 0.94 | 0.70 | −0.18 |
| 0.40 | 0.76 | 0.92 | 0.60 | −0.24 |
| 0.50 | 0.70 | 0.90 | 0.50 | −0.30 |

At the shipped weight the effect is **already nine display points wide** on the peasant reading and
six points of class separation between peasant and artisan — both far above the 0.01 resolution of
the display. The mechanism is not invisible at 0.15. It is visible, and it is visibly
class-differentiated, which is the T3.5 class-differentiation requirement showing through.

---

## 8. RECOMMENDATION — THE DIRECTOR'S FIFTH GATE

### **RECOMMENDATION: NO PRODUCTION CHANGE. `varietyWeight` should remain 0.15.**

I am recommending **no change**, on four grounds, in descending order of weight.

**1. "Perceptibly meaningful" is already satisfied, and the framing anticipated this correctly.**
Because the mechanism drives no behaviour in M4 (§5, §6), perceptibility is entirely a question of
what a player sees. A player sees `Sustenance: 0.91` against a ceiling of `1.00`, with artisans
visibly better fed at `0.97`. Nine display points of penalty and six points of class separation, on a
two-decimal readout, is a perceptible effect by any reasonable reading. There is no measured deficit
in salience to correct.

**2. Raising the weight does not increase the mechanism's responsiveness — only its offset.** This is
the finding I would most want the director to weigh. In the founded world 99.74% of settlement-class-
turns sit at exactly two factor values fixed by the *declared* basket (§2.3); even in the driven
world with full sector specialisation and trade, 84.01% do. The realised diet almost always equals
the declared diet, because `Fill` is 1.0 for all three foods on almost every turn. Raising
`varietyWeight` therefore deepens a near-constant per-class offset and does almost nothing to the
*variance* — it makes the number bigger, not more responsive, and it would be easy to mistake the
first for the second. If the director's underlying concern is that variety "does not seem to do
much", the cause is the near-absence of realised composition variation, **not** the coefficient. That
is a content and trade question (how often can a settlement actually fail to get livestock and fish),
not a tuning one, and it is out of scope for this gate.

**3. Above 0.25 the mechanism starts making a claim it was not designed to make.** At w ≥ 0.30 the
variety term alone begins pushing settlement-turns below the Tier A gate floor on rows whose quantity
term is comfortably above it (§7.3) — 1 row at 0.30, 6 at 0.40, 15 at 0.50. Tier A exists for
"a starving intelligentsia riots over bread"; a *monotonous but adequate* diet registering as a gate
failure inverts that reading. This is currently harmless because nothing downstream consumes it, but
it would be an unpleasant surprise waiting at M5 when D-021 activates and grievance acquires teeth.
Calibrating a display-only term to a value that misbehaves the moment it becomes load-bearing is the
wrong trade.

**4. The shipped value is derived, not fitted, and nothing in the measurement contradicts its
derivation.** H\* = 0.54 comes from a diet-history reconstruction anchored to ADR-013's own
"cereals 75% of calories" chain (`docs/t3.5b-derivations.md` §2), and the resulting 0.9103 cap on a
fully supplied frontier grain economy is documented as the deliberate, honest reading. The measurement
reproduces exactly that number. Changing 0.15 without new evidence would replace a derived
coefficient with a fitted one, against the standing preference for the former.

**If the director nevertheless rules that the weight must rise, 0.20 is the smallest defensible
step** and 0.25 the largest safe one: both leave every invariant in §9 untouched, introduce zero new
gate activations in either world, and move the peasant display from 0.91 to 0.88 or 0.85. I do not
recommend either, for reasons 1-4. **I recommend 0.15 stands.**

**On `varietyStandard.shares`: no change, and no measurement supports one.** The shares are not
shown to be incorrectly specified by anything measured here — the realised H values (peasant 0.8152,
artisan 0.6334) sit above H\* = 0.54 exactly as the derivation says they should, and 27 driven rows
demonstrate that H ≤ H\* is reachable through trade, so the standard is neither unreachable nor
trivially met. Per the director's explicit instruction I note that changing the shares would in any
case be a proxy for raising variety salience and is forbidden as such.

---

## 9. THIRD GATE — WHAT EACH CANDIDATE WEIGHT WOULD AND WOULD NOT TOUCH

The method for each row is stated explicitly, per the director's instruction.

| Property | Effect of any candidate weight (0.20-0.50) | How determined |
| --- | --- | --- |
| **Happiness stays bounded 0..100** | **UNTOUCHED — happiness does not move at all.** `SettlementHappiness.Of` reads only `ConsumptionDeficits` and `Housing`, clamps to `[0, Max]` on return. No input of it is a function of `varietyWeight`. | **Code** (`SettlementHappiness.cs`), corroborated by **measurement** (factor-vs-happiness r = +0.020 founded, −0.035 driven — opposite signs, no channel) |
| **Total deprivation behaviour unchanged** | **UNCHANGED.** The all-zero-factor → 0 anchoring in `SettlementHappiness.Of` is computed from the deficit and housing factors and the D-035-B floor; `varietyWeight` appears nowhere in it. Measured happiness minimum: 87.68 (founded), 4.31 (driven); no settlement-turn at 0 in either world at any weight, because the value is weight-independent. | **Code** + **measurement** |
| **Any food-only deprivation rule appears** | **NO.** None is proposed and none would arise: the weight is a coefficient inside an existing multiplication, adding no branch, threshold or rule. The only threshold in the vicinity is the pre-existing Tier A floor (see next row). | **Code** |
| **Revolt eligibility newly created by the calibration alone** | **IMPOSSIBLE.** `RevoltSystem` gates on `SettlementHappiness.IsRevoltReady` → `Of(...) <= 0.0`, which is weight-independent by the first row. There is no other revolt trigger. | **Code** (`RevoltSystem.cs:77`), corroborated by **measurement** (no happiness variation with the factor) |
| **Tier A gate (inside needs, observation-only)** | **THE ONE THING THAT MOVES.** 0.20 and 0.25: **zero** new activations. 0.30: 1 new. 0.40: 6 new. 0.50: 15 new — all in the driven world, none in the founded world. Reaches no behaviour (grievance is read-isolated), but see §8 ground 3. | **Measurement** (§7.3) + **code** (`ApplyTierAGate`, `SeverityOf` uses `s >= floor → 0`, so the exact-0.50 minimum at w = 0.50 does *not* activate it) |
| **Migration** | **UNTOUCHED.** `MigrationSystem` multiplies viability by `(1 − w_mig + w_mig·happiness01)`, w_mig = 0.15, and happiness is weight-independent. `MigrationTables` grants Buckets, MigrationFlows and SmoothedAttractiveness — no needs table. | **Code** (`MigrationSystem.cs:213-234`, `SystemCatalog.cs`) |
| **Artisan (class emergence/mobility)** | **UNTOUCHED.** `ClassMobilitySystem` is granted Buckets, Variables and ClassStates; it holds no reference to `NeedSatisfactions` or `Grievances` (read-isolation grep). | **Code** |
| **Founding demographics** | **UNTOUCHED.** `WorldFounding` *writes* `GrievanceRow(…, 0.0)` and reads nothing from these tables. | **Code** |
| **Density** | **UNTOUCHED.** No density term reads needs state; the read-isolation allowlist has no density entry. | **Code** |
| **CR-003 (sector allocation governance)** | **UNTOUCHED.** Sector shares come from orders and `Sectors.Share`; no needs input. | **Code** |
| **Food production** | **UNTOUCHED.** `HarvestSystem`/`ProductionSystem` do not reference needs tables. | **Code** |
| **Consumption** | **UNTOUCHED.** `ConsumptionSystem` reads the same `BasketBook` but computes its own demand and eaten/demand bookkeeping; `varietyWeight` is read only by `NeedsGrievanceSystem`. Consumption runs *before* needs in the pipeline and its outputs are the needs system's inputs, never the reverse. | **Code** |
| **Preservation (spoilage/granary)** | **UNTOUCHED.** Driven by `grainSpoilagePerYear` / `granaryYearsOfDemand`; no needs input. | **Code** |
| **Appropriation** | **UNTOUCHED.** No needs-table reference. | **Code** |
| **Trade** | **UNTOUCHED.** `TradeSystem` prices and routes on stocks and prices; no needs input. Note the asymmetry worth stating: variety gives a *narrative* reason to trade, but no code path makes trade respond to the variety factor. | **Code** |
| **Ownership (PolityId / ControlRow)** | **UNTOUCHED.** Control changes only via Colonization (inheritance) and Revolt (happiness), both weight-independent. | **Code** |
| **Schema** | **UNTOUCHED.** `varietyWeight` is a value in `needs.json`, not a field. `NeedSatisfactionRow` and `GrievanceRow` are unchanged in shape; `CanonicalSchema` widths and the schema version are untouched. **No new serialized field.** | **Code** (`CanonicalSchema.cs`) |
| **Goldens** | **WOULD MOVE.** `NeedSatisfactionRow.Value` and `GrievanceRow.Value` are serialized, so any weight change re-mints every world hash and requires a re-pin with a single stated cause. **This is the one real cost of a tuning change and it is not zero** — it is the ordinary price of a TUNE change to a serialized derived value, but it should be weighed against a benefit that §8 argues is absent. | **Code** (`CanonicalSchema.cs:405-420`) |

**Determined by measurement:** the Tier A crossing counts, the factor and contribution distributions,
the per-settlement and per-class breakdowns, the composition correlations, and the absence of any
factor-happiness relationship (as corroboration of the code reading).
**Determined by reading the code:** every "UNTOUCHED" verdict above, the absence of a path from the
factor to happiness/migration/revolt, the schema invariance, and the golden sensitivity.

---

## 10. REPRODUCTION

The probe was a temporary `Sim.Tests` fixture at `79dec68`, deleted after the run. It:

1. `WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42)`;
2. for the driven arm, `DrivenGoldenTests.DrivingOrders(world.Settlements.Count)` validated with
   `OrderValidation.ValidateAgainstWorld`;
3. stepped `TurnExecutor.Run(prev, 1)` 300 times, and at each turn, for every Sustenance
   `NeedSatisfactionRow`, recomputed the obtained-quantity vector from `prev` using the system's own
   public `NeedsGrievanceSystem.Fill` and the staple-substitution rule, then `H`, `factor` via
   `NeedsAggregation.VarietyFactor`, `quantity`, and `SettlementHappiness.Of` / `.FoodSufficiency`
   from `next`;
4. asserted `quantity × factor == satRow` exactly — **0 mismatches in 12,079 rows**;
5. emitted CSV, over which §2-§7 were computed offline.

No production file, data file, golden or test was modified in the measured worktree, and nothing was
committed there.
