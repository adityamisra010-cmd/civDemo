# T4.19 LANE C — THE FOUNDING DEMOGRAPHIC STATE, CORRECTED

**Packet:** T4.19 glass-box, lane C. **Ruling:** the T4.18 diagnosis
(`docs/m4-population-transient-investigation.md`) is ACCEPTED — the opening collapse
5,140 → 4,330 → 4,041 → 3,987 is `founding.cohortCounts` putting 17.5 % of the founding
population in the 60+ cohorts, where shipped mortality runs 0.107–0.305/yr. The BEHAVIOUR is
NOT accepted: the game must begin approximately within its stable demographic regime.

**Branch** `t4.19-lane-demo`, cut from `main` at `feaf218`.

**WHAT CHANGED — ONE DATA ARRAY.** `Sim.Data/content/sim.json` `founding.cohortCounts`:

```
old  [46 44 40 30 28 26 24 22 20 18 17 15 30 20 12  8]   Σ 400   child 130 / adult 200 / elder 70
new  [57 56 51 45 40 34 29 24 19 15 11  8  5  3  2  1]   Σ 400   child 164 / adult 225 / elder 11
```

plus a `_docCohortCounts` field recording the derivation, one new test class
(`Sim.Tests/Systems/FoundingDemographicsTests.cs`) that re-derives the vector from the kernel on
every run, the golden re-pins §7 attributes, and this record.

**WHAT DID NOT CHANGE (C1, measured by `git diff`):** `DemographicsSystem`, every demographic
rate, `foodStore`, `endowmentJitter`, `WorldFounding`, every equation, every corridor band,
every quarantine. No startup modifier, suppressed-death window, population bonus or
stabilisation multiplier exists anywhere in the tree. The world simply starts on the age
structure its own rates support.

---

## §1 WHAT `cohortCounts` IS — READ FROM `WorldFounding.cs`, NOT ASSUMED

`WorldFounding.Found` (`Sim.Core/Worldgen/WorldFounding.cs:71-110`) treats the 16-slot array as
**absolute per-settlement counts** for the FIRST registered class only (other classes found at
zero, D-027). Each slot is jittered by two multiplicative factors of amplitude
`endowmentJitter = 0.69` — a settlement-COMMON factor and a per-(settlement, slot) factor, both
pure SplitMix64 hashes of `(seed, settlement, slot)` — rounded to whole units and floored at 0,
then sourced through `Ledger.Flow(InitialEndowment)`. Three quantities are then derived
DIRECTLY from the realised counts: the grain endowment
(`foodStore × realisedPop / configPop × (1 + jitter/3 · u)` — so **food per capita does not
depend on the cohort vector**), the founding dwellings (`round(realisedPop / personsPerDwelling)`),
and the three `InitialEndowment` ledger rows. Nothing else reads the vector. The config total
(400) is what `popScale` divides by, so keeping Σ = 400 keeps the world total comparable:
seed 42 founds 5,140 → 5,143 people.

---

## §2 THE STABLE AGE STRUCTURE — MEASURED FROM THE KERNEL, NOT DERIVED ON PAPER

**Instrument.** A throwaway console harness in the scratchpad (not in the tree) builds a
single-settlement 16-cohort `Buckets` world exactly as `PopulationExactnessTests.BucketWorld`
does, and steps it through a **pipeline containing only `demographics`**
(`PipelineLoader.Load("{\"pipeline\":[\"demographics\"]}", SystemCatalog.All(cfg))`) on a flat
era table. With no consumption system the deficit table is never written, so the PREV-read
deficit is exactly 0 — the fed regime, no famine suppression, rebound reservoir identically 0.
This is the production `DemographicsSystem` (ADR-011 micro-kernel), untouched.

**Convergence from the OLD vector × 1000 (400,000 people, dt 10), measured:**

| turn | pop | growth %/yr | CBR %/yr | CDR %/yr | 60+ share | shares c0…c15 |
|---:|---:|---:|---:|---:|---:|---|
| 0 | 400,000 | — | — | — | 0.1750 | .1150 .1100 .1000 .0750 .0700 .0650 .0600 .0550 .0500 .0450 .0425 .0375 .0750 .0500 .0300 .0200 |
| 1 | 331,199 | −1.8698 | 3.164 | 4.884 | 0.0709 | .1320 .1308 .1205 .1066 .0905 .0758 .0645 .0557 .0484 .0414 .0346 .0282 .0239 .0199 .0139 .0133 |
| 2 | 314,402 | −0.5191 | 3.843 | 4.350 | 0.0382 | .1404 .1370 .1250 .1123 .0985 .0842 .0702 .0576 .0469 .0377 .0296 .0225 .0162 .0106 .0061 .0053 |
| 3 | 312,607 | −0.0572 | 4.108 | 4.165 | 0.0292 | .1429 .1394 .1265 .1129 .0990 .0854 .0721 .0595 .0478 .0372 .0279 .0201 .0136 .0082 .0043 .0031 |
| 5 | 316,493 | +0.0725 | 4.170 | 4.097 | 0.0263 | .1432 .1399 .1271 .1134 .0992 .0853 .0721 .0597 .0483 .0376 .0280 .0198 .0129 .0074 .0037 .0023 |
| 8 | 323,792 | +0.0761 | 4.172 | 4.096 | 0.0263 | .1432 .1399 .1271 .1134 .0992 .0853 .0721 .0597 .0482 .0376 .0280 .0198 .0129 .0074 .0037 .0023 |
| 50 | 445,708 | +0.0761 | 4.172 | 4.096 | 0.0263 | identical to turn 8 at four decimals |
| 100 | 652,035 | +0.0761 | 4.172 | 4.096 | 0.0263 | identical |
| 200 | 1,395,433 | +0.0761 | 4.172 | 4.096 | 0.0263 | identical |
| 400 | 6,391,291 | +0.0761 | 4.172 | 4.096 | 0.0263 | identical |

The shares are stationary to four decimals from turn 8 and do not move again through turn 400.
The kernel's fed growth rate is **+0.0761 %/yr** (CBR 4.172, CDR 4.096 per 100 person-years,
the CDR including in-step infant deaths — which is why it exceeds the crude
Σ mortality × share = 3.96), the ratified "≈ 0.07 %/yr" of T2.7. **The stable 60+ share is
2.63 %**; the old vector founded at 17.5 %, 6.7× over.

**Three independent cross-checks, all agreeing to four decimals:**
- **dt 5** from the old vector × 1000: the same shares by turn 10 (turn 10 = 50 sim-years),
  the same +0.0761 %/yr at turn 100. The micro-step kernel is dt-invariant by construction
  and this measures it.
- **A uniform start** (25,000 per cohort — a shape that is not the answer): the same shares
  at turn 50 and turn 100, drift < 10⁻⁴ per cohort between them, growth 0.0761 %/yr. This is
  what `FoundingDemographicsTests.CohortCounts_AreTheLargestRemainderRoundingOfTheKernelsOwnStableShape`
  runs on every suite pass — the pin is a convergence proof, not a persistence one.
- **A double-precision replica** of the pinned micro-kernel with no integer flooring (the
  continuous limit): shares `.14323 .13993 .12711 .11337 .09922 .08533 .07212 .05971 .04823
  .03762 .02804 .01979 .01289 .00744 .00365 .00232`, growth 0.07612 %/yr. The production
  system at scale 1000 matches this at every printed digit, so integer reconciliation costs
  nothing at that scale.
- **Scale 1** (400 people, the real founding size): the shares wander by ±0.003 per cohort
  from integer flooring; the largest-remainder rounding of its turn-400 vector from the old
  start is the same 16 numbers, and from the new start at turn 50 it differs by one unit in
  six slots (57,56,51,46,39,34,29,24,20,15,11,7,6,3,1,1) — flooring noise at 400 people,
  which is why the ×1000 measurement is the one used.

**Rounding to the same total.** Largest-remainder apportionment of the turn-400 shares to
Σ = 400, lowest-index tie-break:

```
exact ×400  57.293 55.971 50.844 45.348 39.690 34.132 28.849 23.882 19.292 15.047 11.215 7.914 5.156 2.977 1.461 0.929
floors      57     55     50     45     39     34     28     23     19     15     11     7     5     2     1     0      Σ 391
remainders  .293   .971   .844   .348   .690   .132   .849   .882   .292   .047   .215   .914  .156  .977  .461  .929
9 leftover → slots 13 (.977) 1 (.971) 15 (.929) 11 (.914) 7 (.882) 6 (.849) 2 (.844) 4 (.690) 14 (.461)
result      57     56     51     45     40     34     29     24     19     15     11     8     5     3     2     1      Σ 400
```

No two remainders tie at three decimals here, so the tie-break is not exercised by this
vector — which is exactly why `Apportion_TieDense_LowestIndexWins_AndTheTotalIsExact` exercises
it on sixteen equal weights (all remainders identical, the extra units must land on the lowest
indices) and on sixteen exact halves, plus a 0..1000 total sweep for the exactness of the sum.
The rounded vector's own crude rates are CBR 4.19, CDR 3.96 %/yr on the arrays — the rounding
residual is +0.2 %/yr at turn 0 and the kernel reaches its +0.076 %/yr by turn 2 from it
(measured: 400 → 402.9 → 406.0 → 409.1 in the continuous replica; the shares at turn 1 are
already within 0.0002 of stationary).

**Bands:** child 164 (41.0 %), adult 225 (56.3 %), elder 11 (2.75 %) — against the kernel's
41.0 / 56.3 / 2.63, and against corridors.json's `pyramidChildShare [0.35, 0.46]`,
`pyramidAdultShare [0.46, 0.57]`, `pyramidElderShare [0.015, 0.08]`. The founding pyramid now
sits inside the corridors the battery already checks at year 4500.

---

## §3 THE CONTROL: AT TURN 0, ONLY THE COHORT-DERIVED TABLES DIFFER

The canonical 1024² N = 12 world was founded under the old and the new config in one process
and **all 41 `WorldState` tables** were compared row by row (the same list `WorldStates.StateEquals`
covers), seeds 42, 7, 123 and 2024. Terrain content hash, `Seed` and `Clock` are equal on every
seed. Tables differing:

| seed | Buckets (of 576) | GoodStocks (of 168) | Housing (of 12) | LedgerFlows (of 3) | every other table |
|---:|---:|---:|---:|---:|---|
| 42 | 191 | 10 | 10 | 3 | IDENTICAL (37 tables) |
| 7 | 191 | 11 | 9 | 3 | IDENTICAL |
| 123 | 188 | 11 | 11 | 3 | IDENTICAL |
| 2024 | 191 | 11 | 11 | 3 | IDENTICAL |

`Settlements`, `Deposits`, `ClassStates`, `Grievances`, `Controls`, `Polities`, `Capitals`,
`NetworkMeta` and the 29 empty tables are byte-identical. The three non-bucket tables are the
three quantities §1 shows founding derives directly from the realised cohort counts, and the
per-settlement food-per-capita confirms the grain rows moved for that reason and no other —
seed 42, all twelve settlements: `17.06/17.06 17.05/17.05 16.38/16.38 17.76/17.76 18.19/18.19
11.77/11.77 15.06/15.06 17.19/17.19 12.87/12.87 16.76/16.76 14.01/14.01 17.08/17.08` (old/new,
units per person, unchanged to two decimals). World totals at turn 0: population 5,140 → 5,143;
grain 82,041 → 82,230; the +0.2 % is realised-population jitter on the new slot weights. Across
the other three seeds one settlement (seed 7, settlement 1: 760 → 768 people) reads 11.91 → 11.92
— the grain endowment is rounded to a whole unit per settlement, so per-capita can move in the
second decimal by that integer rounding and by nothing else.

**No other table differs, so this packet stops nowhere.**

---

## §4 BEFORE / AFTER, FOUR SEEDS, HEADLESS, NO ORDERS, CANONICAL WORLD

Columns: population, Δ, births, deaths, starvation (reason 7, first difference of the ledger),
migration out (world sum), grain store, mean deficit ratio, child / adult / elder counts, 60+
share. Every starvation cell in every table below is **0** and every deficit is **0.0000**.

**Seed 42**

| turn | pop | Δ | births | deaths | migOut | food | child | adult | elder | 60+ % |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 old | 5,140 | — | — | — | — | 82,041 | 1,546 | 2,736 | 858 | 16.7 |
| 1 old | 4,330 | −810 | 1,632 | 2,442 | 0 | 5,752 | 1,640 | 2,365 | 325 | 7.5 |
| 2 old | 4,041 | −289 | 1,609 | 1,898 | 523 | 4,822 | 1,613 | 2,254 | 174 | 4.3 |
| 3 old | 3,987 | −54 | 1,636 | 1,690 | 153 | 4,508 | 1,622 | 2,244 | 121 | 3.0 |
| 4 old | 3,997 | +10 | 1,659 | 1,649 | 37 | 4,451 | 1,638 | 2,248 | 111 | 2.8 |
| 10 old | 4,177 | +38 | 1,728 | 1,690 | 6 | 4,618 | 1,713 | 2,352 | 112 | 2.7 |
| 20 old | 4,513 | +37 | 1,865 | 1,828 | 19 | 4,984 | 1,850 | 2,543 | 120 | 2.7 |
| 50 old | 5,702 | +36 | 2,363 | 2,327 | 9 | 6,291 | 2,338 | 3,212 | 152 | 2.7 |
| **0 new** | **5,143** | — | — | — | — | 82,230 | 1,946 | 3,060 | 137 | 2.7 |
| **1 new** | **5,245** | **+102** | 2,157 | 2,055 | 0 | 5,832 | 2,134 | 2,943 | 168 | 3.2 |
| **2 new** | **5,193** | −52 | 2,120 | 2,172 | 608 | 5,854 | 2,117 | 2,922 | 154 | 3.0 |
| **3 new** | **5,191** | −2 | 2,139 | 2,141 | 245 | 5,800 | 2,124 | 2,922 | 145 | 2.8 |
| 4 new | 5,239 | +48 | 2,164 | 2,116 | 65 | 5,797 | 2,144 | 2,954 | 141 | 2.7 |
| 10 new | 5,477 | +39 | 2,270 | 2,231 | 43 | 6,042 | 2,248 | 3,086 | 143 | 2.6 |
| 20 new | 5,928 | +56 | 2,454 | 2,398 | 36 | 6,497 | 2,428 | 3,344 | 156 | 2.6 |
| 50 new | 7,476 | +49 | 3,093 | 3,044 | 15 | 8,175 | 3,068 | 4,215 | 193 | 2.6 |

**Seeds 7, 123, 2024 — the opening four turns**

| seed | t0 old → new | t1 old → new | t2 old → new | t3 old → new | t1 births/deaths old → new | t50 old → new |
|---:|---|---|---|---|---|---|
| 7 | 5,190 → 5,258 | 4,429 (−14.7 %) → **5,394 (+2.6 %)** | 4,211 → 5,457 | 4,212 → 5,515 | 1,647/2,408 → 2,185/2,049 | 6,059 → 7,993 |
| 123 | 5,150 → 5,278 | 4,488 (−12.9 %) → **5,484 (+3.9 %)** | 4,301 → 5,542 | 4,276 → 5,583 | 1,705/2,367 → 2,256/2,050 | 6,112 → 8,003 |
| 2024 | 5,263 → 5,448 | 4,615 (−12.6 %) → **5,672 (+4.1 %)** | 4,436 → 5,748 | 4,419 → 5,787 | 1,775/2,423 → 2,353/2,129 | 6,334 → 8,367 |

**The new opening trajectory, stated numerically:** the world no longer falls. Turn 1 is
+2.0 % to +4.1 % across the four seeds, turns 2–3 are within ±1 %, and from turn 4 the
population grows at the kernel's fed rate (+0.6 to +1.0 % per 10-year turn, +0.076 %/yr).
The minimum population of a 650-turn canonical run is now at turn 1 or 2 and is the founding
size itself (seed 1: 5,613 at t2 vs 4,331 at t3 before; seed 2: 5,665 at t1 vs 4,340 at t3).
The 60+ share is 2.3–2.8 % from turn 0 and stays there; before, it fell 16.7 → 2.8 % over
four turns as 700 elders died.

**The turn-1 bump is attributed, not waved at.** With `endowmentJitter = 0` all four seeds
found 12 × 400 = 4,800 and open 4,800 → 4,920 → 4,937–4,950: +120 on turn 1, births 1,992,
deaths 1,872. The continuous kernel predicts 4,834 (+0.7 %). The 96 missing deaths are the
D-004 remainder warm-up: every settlement carries 16 death-remainder accumulators that start
at zero, so the first turn's deaths floor low by ≈ 8 per settlement (16 × 0.5), once — the
demographics-alone run at scale 1 shows the same 400 → 410 (continuous 402.9). The remaining
+0 to +100 per seed is the founding jitter drawing per-settlement shapes younger or older than
the stable one (seed 2024's realised vector has 60+ at 2.3 % and cohorts 3–4 at 668/607 against
a stable 618/540). Neither is a mechanism; both are ≤ 2 % and one-off, against a −13 to −16 %
turn 1 that was a 700-person die-off.

---

## §5 FAMINE STILL KILLS — MEASURED, NOT ASSERTED

**The driven scenario** (`DrivenGoldenTests.DrivingOrders`, seed 42, the sector batch at turn 2,
the same world the driven golden pins) starves as before. Turns 1–212 (the run's common horizon
— §6 explains 213):

| | old vector | new vector |
|---|---:|---:|
| starvation deaths, turns 1–212 | 3,913 | 5,536 |
| largest single-turn losses (turn: Δpop, starvation) | t1: −810, 0 · t55: −619, 340 · t16: −398, 222 · t39: −350, 207 | t55: −786, 432 · t39: −439, 261 · t16: −423, 250 · t76: −373, 209 |
| population at turns 100 / 150 / 200 / 212 | 408 / 188 / 131 / 139 | 786 / 384 / 216 / 210 |
| minimum population by turn 212 | 116 (t198) | 202 (t203) |

The largest loss in the old run was the initialization transient (t1, starvation 0); every one
of the new run's five largest losses is a real famine with reason-7 deaths, migration flight and
a non-zero prior-turn deficit (t54 deficit 0.196 → t55 −786 and 432 starved). A world that
founds 5,143 and is driven into the ground still reaches ~200 people.

**In the no-order canonical world** the famine episodes the battery sees are unchanged in
timing and larger in the head-count they take, because there are more people to take: canonical
seed 2, 650 turns, starvation deaths by turn `t80 t100 t109 t116 t128 t144 … t574` — old
`28 12 3 91 6 34 … 232` (Σ 409), new `35 4 9 111 5 44 … 304` (Σ 514). Seed 1: t18 37 → 49,
t186 61 → 66. Deficit-driven starvation, fertility suppression and famine flight all still fire
at the same turns from the same causes.

---

## §6 A LATENT PRODUCTION DEFECT THE NEW TRAJECTORY REACHES — REPORTED, NOT FIXED

The driven world under the new founding vector **throws at turn 213**:

```
Sim.Core.Kernel.LedgerOverdrawException: sinking 67 exceeds available stock 66 under OverdrawPolicy.Throw.
   at ProductionSystem.Craft (Sim.Core/Systems/Production/ProductionSystem.cs:430)
```

Measured with a one-line probe inside `Craft` (added, run, `git checkout`-ed; the tree carries
no trace of it):

```
recipe 'weaving'  input good 9 (fiber)  settlement 10
stock 66   perOutput 3   exactOutput 22 (= 66/3, the Leontief cap)   product 66.0
ConsumeRemainder 0.9999999999999929   exactIn = 66 + 0.9999999999999929 = 67.0 exactly   sunk 67
```

**Mechanism** (`ProductionSystem.cs:406-432`): the input cap sets
`exactOutput = min(exactOutput, stockAmount / perOutput)` from the STOCK ALONE, and the sink
then floors `exactOutput × perOutput + ConsumeRemainder`. The cap ignores the row's banked
remainder. When the remainder is within a few ulps of 1 the sum rounds to `stock + 1.0`, floors
to `stock + 1`, and `OverdrawPolicy.Throw` makes it fatal. The remainder sits that close to 1
because a previous turn's `exactIn − sunk` was `k − ε`. Under the old vector this world happened
never to hit the coincidence in 300 turns; under the new one, with a different consumption
history at settlement 10, it does at turn 213. **It is a defect in the production system's
arithmetic, not in founding**, and a founding change that happens to expose it does not license
lane C to change `ProductionSystem` — that is a different packet and a director ruling. Three
options for that ruling, smallest first: (a) cap on `(stockAmount − ConsumeRemainder) /
perOutput`, so the banked fraction is inside the cap; (b) `OverdrawPolicy.ClampToAvailable` on
the input sink with the shortfall NOT banked (the demographics convention); (c) both. Each moves
the driven golden again. The founded no-order run (300 turns, seed 42), the CI ordered founded
run (`gen-sample-orders.py --labor`, 300 turns) and its replay all complete: the crash is
specific to the driven allocation's crafting history.

**Consequence in the suite:** `DrivenGolden_Seed42Turn300_MatchesPinnedConstant`,
`D1_FlowAndItsDecomposition_Measured` and
`IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne`
all run `RunDriven(300)` and **fail by this exception**. They are deliberately NOT hidden,
skipped or weakened; their constants are the pre-T4.19 values with a note saying why. The driven
golden cannot be re-pinned until the ruling.

---

## §7 EVERY GOLDEN MOVEMENT, WITH ITS CONTROL

The control for every founded pin is §3: found old and new, diff every table at turn 0, only the
cohort-derived tables differ. The **no-unrelated-movement control** is
`GoldenHash_Seed42Turn200` (synthetic genesis, no founding): UNMOVED, `eec82711…`, and its
v22-stripped control in `IntegratedPinAttributionTests` still returns `0f94b4ad…`.

| pin | old | new |
|---|---|---|
| `SnapshotTests.FoundedGolden_Seed42Turn300` | `98a89d18b014fa1726ab3ee611a8662b2982bf4fbac0b10ada00718e4eebd983` | `917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62` |
| `ci.yml FOUNDED_GOLDEN` | `98a89d18…` | `917993b2…` — the built CLI (`sim run --founded --seed 42 --turns 300`) lands on it independently; `CiPinAgreementTests` green |
| `IntegratedPinAttribution.FoundedGolden` v22-stripped | `f25c5dd3947a53827c1d9615a7e351108c05258bb0ffe0b1ab1a269e9a4626c6` | `f886efbd159f5717848534efe3af61b826fa599d742e5e244d7afafa067bce22` |
| `IntegratedPinAttribution.FoundedGolden` v23-stripped | `16a1c17150f210b90a8c4d866f16a1767bdc13f218f880304f2449437625e015` | `e48d9bcd8883204bb2efa4843923c49a30de86fae9269e7354e6d2018bf8e1f7` |
| `FirstReignTests` turn-40 golden | `7a9c3de745eac824c5c1b5783d527cf959ada9c558e7012423bea5f92a6361a3` | `5ee8119e365ad04dbdfc45f791a8962bb0fb616016ad1616c67b9c74c2d81e9a` |
| `IntegratedPinAttribution.FirstReign` v22-stripped | `a64a6cf62eb63a4e5c46297fca4e146a543e13cb0f49a53c3687b47da63001e6` | `69d6cf178fa536e0582874eacf7adec9fbcbc686c5e14a12292f935aa2694550` |
| `IntegratedPinAttribution.FirstReign` v23-stripped | `f79714f955c31cf0f25d323c045a0c1935345e92908fa78758bc8266c6b8ef0b` | `4e7d2e69e7c5ed72444501bed84c341b50a0d77c25d123ead36498ce9b280d7b` |
| `DrivenGoldenTests` turn-300 golden | `01673381e5e4b18753bf19f345e42f5424046a813a8c723a7564be34186820af` | **not re-pinnable** (§6) — constant left, annotated |
| `IntegratedPinAttribution.Driven` v22 / v23 | `611a1508…` / `e2f3c042…` | **not re-pinnable** (§6) — left, annotated |
| `HudViewModelTests` / `SelectionTests` population line | `pop 400  (child 130 / adult 200 / elder 70)` | `pop 400  (child 164 / adult 225 / elder 11)` — the bands of the new vector at jitter 0, the world line `world pop 1600` unchanged |
| `GoldenHash_Seed42Turn200` | `eec82711…` | **UNMOVED** (control) |
| `NeedsGrievanceTests.Famine_RaisesGrievance_InTheStarvationWindow` control-arm starvation (a T4.2 VALUE pin on the dev N = 1 seed-42 founding) | 28 | 38 |
| `PathBuildTests.Order_SetsAllocationRowOnItsTurn_…_BankAccrues` path-labour accrual, step 3 → 4 | 21.80 (formula) | 28.18 (formula now carries the T3.8 housing draw; see below) |

**The two semantic rigs, found on the dev 256² N = 1 seed-42 world.** Both passed at the packet
commit (re-verified by stashing the change and running them on `feaf218`) and both fail on the
new founding for a measured reason. Turn-0 control on THAT world, old vs new vector: Buckets
16/48 rows differ, the grain row, the housing row and the three InitialEndowment ledger rows;
Settlements, Deposits, ClassStates, Grievances, Controls, Polities, Capitals, NetworkMeta
identical; terrain hash equal. The settlement founds 468 / 234 adults instead of 459 / 211.
- **Grievance rig:** the control arm's starvation under T4.2's granary truncation of the 4,000
  seed is 38 instead of 28 because there are more mouths and none of them die of old age on
  turn 1. The pin is a recorded VALUE (T4.2's own words) and is re-pinned to 38; the contrast the
  test claims — famine grievance exceeds the fed control's — is still asserted and passes.
- **Path-bank rig:** the hand-computed accrual `LaborPerAdultPerYear × 0.5 × adults × dt` never
  carried T3.8's housing draw (`builderYears = max(0, builders × dt − prev.Housing.LastLaborUsed)`,
  the shipped `PathBuildSystem` contract) because it could never see it: under the old vector the
  settlement SHRANK over turns 1–3 (459 → 394 → 384 → 386), housing was in surplus and
  `LastLaborUsed` at turn-3 state was exactly 0.0. It now grows (468 → 479 → 491 → 500), one
  dwelling is built on turn 3, `LastLaborUsed = 1.0` adult-year, and the measured accrual is
  28.18 against the formula's 28.20 — 0.02 × 1.0 exactly. The expectation now subtracts the
  turn-3 housing draw, stays exact to nine places, and pins the subtraction it was blind to. This
  is a rig repair of the kind T4.7 made to the same assertion (accrual vs accrual-minus-spend),
  not a weakening; the director may reverse it, in which case the test fails by 0.02 for the
  reason above.

The old values were all re-derived by the harness with the OLD vector passed explicitly on this
tree (no `Sim.Core` file differs from `feaf218`, so that is the parent's world) and are
bit-identical to the six old constants; the same harness with the new vector lands on the six
new ones. The instrument is faithful in both directions. FirstReign's shape asserts pass on the new trajectory and were measured: the lone
settlement founds at 468 (was 459), runs 479, 491, 500, 508 before the director's 0 %-farm order
lands, is extinct at turn 15 (was 13; band (5, 25]), peaks at 620 food (was 495; the ghost
mountain stays absent) and stays dead through turn 40. FoundedGolden's world ends turn 300 at
41,131 people (was 31,374): a +31 % level shift, which is the removed transient compounding
nothing — the fed growth rate is unchanged (§8).

`SessionRecordTests` and `UiSessionReplayTests` pin no hashes; `Sim.Ui.Tests` is 185/185 after
the two string pins above.

---

## §8 CALIBRATION STATUS CHANGES — REPORTED WITH NUMBERS, NOTHING RE-BANDED

Measured with `AutoplayCollector` on the battery's own recipe, old vector vs new, same seeds,
same horizons. **Every RATE corridor is unchanged to three decimals**; two LEVEL readings and
one documented window move because the founding population is 30 % larger from turn 3 onward
and the fed growth rate compounds the same from a higher base.

| metric (canonical, 650 turns) | seed 1 old → new | seed 2 old → new | band |
|---|---|---|---|
| fedGrowthPerYear [800, 2400] | 0.000759 → 0.000758 | 0.000746 → 0.000748 | [0.0005, 0.001] in |
| crudeBirthRatePer1000 | 41.399 → 41.401 | 41.398 → 41.400 | [37, 46] in |
| crudeDeathRatePer1000 | 40.644 → 40.647 | 40.651 → 40.653 | [36, 45] in |
| pyramid child / adult / elder | .4103/.5634/.0263 → .4103/.5635/.0263 | unchanged | in |
| final population (year 4500) | 128,727 → 167,409 (+30.0 %) | 127,014 → 167,174 (+31.6 %) | — |
| arable km² (denominator) | 267,827.9 → 267,827.9 | 225,268.5 → 225,268.5 | — |
| **densityPerArableKm2** | **0.4806 → 0.6251** | **0.5638 → 0.7421** | [0.15, 0.6] → **OUT, both seeds** |
| migrationGrossPerDecade | 0.000294 → 0.000239 | 0.000586 → 0.000519 | [0.001, 0.01] out both before and after (accepted as measured, M4 completion ruling) |
| starvation deaths (whole run) | 98 → 115 | 409 → 514 | — |

**`Canonical_FedCorridors_AllInBand` seeds 1 and 2: PASS → FAIL on `densityPerArableKm2`
alone** (0.625062 and 0.742110 against a 0.6 ceiling). The ratio 167,409 / 128,727 = 1.300 is
the ratio of the two worlds' turn-3 populations (5,613 / 4,331 = 1.296): this is the level of
a transient that no longer happens, propagated 6,500 years at an unchanged rate, divided by an
unchanged denominator. The density corridor was lifted at M4 completion on 20/20 seeds in band
(max 0.565); the two battery seeds now sit 4 % and 24 % over its ceiling. **NOT re-banded here**:
whether the corridor's ceiling was derived on a world that quietly started 22 % smaller than it
founded is a question for the director and a 20-seed re-measurement, not for this lane.

**`Dev_MalthusCorridors_AllInBand` seeds 7 and 42: FAIL → FAIL**, the same CR-003 tooth
("N starvation deaths — the dev world is no longer pre-Malthusian in the strict sense"),
seed 7: 4 → 3, seed 42: 3 → 3. Status unchanged. Dev density moves the same way (1.624 → 2.144,
1.187 → 1.536) but is not asserted there.

**`ClassSystemTests.Artisans_EmergeInFedAutoplay_PlateauAtTheCap_DocumentedWindow`: PASS →
FAIL.** Emergence at settlement 0 of the dev world moves from turn 22 to turn 4, outside the
documented window [10, 95]. The cause is the test's own stated one — emergence is
population-gated at `population > 520` — read against the old opening: settlement 0 founded 459,
fell to 394 on turn 1 and grew back past 520 at turn 22; it now founds 468, reaches 528 on turn
2, and the Prev-read latch fires on turn 4. Boom peak 0.194 → 0.199, post-boom minimum
0.035 → 0.028 (both inside the test's other asserts). The window was measured on the collapsing
opening; re-deriving it is the same kind of decision as the corridor above and is left to the
director. NOT re-windowed here.

`Canonical_EraBoundaryContinuity` and `Corridors_AllBandsTwoSided_AndOrdered` pass unchanged.

---

## §9 THE FOOD ENDOWMENT FINDING ITS CAP — OBSERVED, UNCHANGED, NOT FIXED

T4.18 §4 attributed the turn-1 food fall to a 21.5-year endowment meeting a 1.5-year granary
ceiling, compounded by a structurally zero first harvest. **This packet changes none of it and
re-measures it on the new founding:** seed 42, grain 82,230 → 5,832 on turn 1 (−92.9 %; before
82,041 → 5,752), harvest exactly 0 on turn 1 at every seed (`CatchmentSummaries` is still empty
at turn 0), food per capita 15.99 → 1.11 and then 1.10 ± 0.01. The deficit ratio is 0.0000
through it, so the opening is still not a famine. `founding.foodStore` was NOT moved: §1 shows
the food endowment scales with the realised population, so the cohort change could not have
made it necessary, and the measured per-capita endowment is unchanged to the unit. Whether a
21-year endowment should be founded only to be destroyed on turn 1 — 38,391 eaten, 24,029
spoiled, 13,869 overflowed — is the separate question T4.18 raised and it is left exactly where
T4.18 left it, with one sharper edge: now that the population no longer dips, the
overflow-to-cap is the ONLY large first-turn movement a player will see.

---

## §10 DELIBERATELY LEFT ALONE

- `DemographicsSystem`, `WorldFounding`, `ConsumptionSystem`, `ProductionSystem` (§6 included),
  every rate, every equation.
- `founding.foodStore` (§9), `founding.endowmentJitter`.
- `corridors.json` bands and quarantine windows; the `Artisans` documented window; the CR-003
  Malthus quarantine (§8).
- The `DrivenGolden` and `IntegratedPinAttribution.Driven` constants (§6) — annotated, not moved.
- The turn-1 zero harvest (§9) and the D-004 first-turn remainder warm-up (§4) — both measured,
  both benign, neither a founding-vector question.

## §11 GATES AND SUITES — MEASURED ON THE COMMITTED TREE

`dotnet build -c Release Sim.slnx`: 0 warnings, 0 errors. `check-banned-constructs`,
`check-read-isolation`, `check-readonly-proof`: OK. `Sim.Ui.Tests`: 185 / 185.

**`Sim.Tests`, Release, full suite: 636 passed / 8 failed / 6 skipped (650), 16 m 11 s.** The
six skips are the pre-existing skip set. The eight failures, each against its status on the
parent `feaf218` — measured by running the same tests in a detached worktree at that commit
(21 tests: 19 passed / 2 failed), not inferred from the M4-D certification line:

| test | at `feaf218` | this tree | cause |
|---|---|---|---|
| `DrivenGoldenTests.DrivenGolden_Seed42Turn300_MatchesPinnedConstant` | PASS | FAIL (LedgerOverdrawException, turn 213) | §6 |
| `DrivenGoldenTests.D1_FlowAndItsDecomposition_Measured` | PASS | FAIL (same exception) | §6 |
| `IntegratedPinAttributionTests.DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne` | PASS | FAIL (same exception) | §6 |
| `ClassSystemTests.Artisans_EmergeInFedAutoplay_PlateauAtTheCap_DocumentedWindow` | PASS | FAIL (emergence turn 4, window [10, 95]) | §8 |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed: 1)` | PASS | FAIL (`densityPerArableKm2` 0.625062 ∉ [0.15, 0.6]) | §8 |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed: 2)` | PASS | FAIL (`densityPerArableKm2` 0.74211 ∉ [0.15, 0.6]) | §8 |
| `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 42)` | FAIL (3 starvation deaths) | FAIL (3) | CR-003 quarantine, unchanged |
| `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7)` | FAIL (4) | FAIL (3) | CR-003 quarantine, unchanged |

Every re-pinned golden (`FoundedGolden_Seed42Turn300`, `FirstReignTests` turn 40, both
`IntegratedPinAttribution` founded/FirstReign strips, `CiPinAgreementTests`,
`NeedsGrievanceTests.Famine_RaisesGrievance_InTheStarvationWindow`,
`PathBuildTests.Order_SetsAllocationRowOnItsTurn_…`) passes on this tree and passed at
`feaf218` with its old constant. `GoldenHash_Seed42Turn200` passes on both — the
no-unrelated-movement control. The three new `FoundingDemographicsTests` pass. `CalibrationBatteryTests.cs`
was not edited: no assertion, band, quarantine or recorded value in it was touched.

The CI founded sequence was reproduced on the built CLI from this tree: two `sim run --founded
--seed 42 --turns 300` processes byte-identical over 300 hash lines, the last line equal to
`ci.yml`'s `FOUNDED_GOLDEN` (`917993b2…`), and the `--labor` ordered run and its replay
byte-identical over 300 turns.
