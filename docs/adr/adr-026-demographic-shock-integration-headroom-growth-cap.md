# ADR-026 — DEMOGRAPHIC SHOCK INTEGRATION: effective-deficit mortality and fertility, the feedable food-influx limit, and the headroom growth cap

**Status: accepted under CR-015; implementation T4.21-3** (`FoodHeadroom` itself ships in T4.21-1 so
T4.21-2 ∥ T4.21-3 share no hunk). Ruling: `docs/adr/cr-015-famine-is-exceptional.md` §6 (mandate
2026-09-17, item 6 and §28; N2, N3; **G3(b) is the shipped argument**). Spec:
`docs/t4.21-architecture.md` §3.6, §3.7–§3.10, §6.4.

**ADR number:** 026 (see ADR-024 for the numbering evidence).

> Outside FAMINE, the exceptional demographic channels — starvation mortality and fertility
> suppression — read the EFFECTIVE deficit `d_eff` (adaptation absorbs a shortfall up to `a`);
> in FAMINE they read the whole deficit. Under constant conditions population rises asymptotically
> toward `N_lim`, the number of adult-equivalents the settlement's last-turn FEEDABLE food influx
> sustains — the fixed point of Consumption's one-directional substitution at the standing basket
> mix — via a fertility multiplier on the remaining headroom at rate `k`; it never overshoots into
> deficit. Immigration draws on the same headroom at the same rate (ADR-025). No serialized cap
> state; the cap never removes a person; decline is the deficit channel's job.

---

## §1 WHAT THIS ADR AMENDS (quoted), AND WHAT IT DOES NOT CHANGE

**Amends (CR-015 N2, N3):**

- `docs/adr/cr-012-malthus-architecture.md:157-158`: *"No demographic architecture change of any
  kind is authorized."* → AMENDED BY CR-015 in narrow scope: (a) `StarvationRate` reads `d_eff`
  (linear form and constant retained); (b) `suppression` reads `d_eff` outside FAMINE, nominal `d`
  in FAMINE — **G3(b)**; (c) the headroom fertility multiplier. One line appended after `:162`.
- `docs/t4.20-food-semantics.md:178-179` / `docs/queue.md:1358-1359` / `docs/milestones.md:324-326`:
  *"a population-cap semantics on which no ruling exists"* → RULED (CR-015 §6.2 N3); the queue lines
  are closed; `FoodSupportedPopulation`/`FoodConstrainedGrowth` become legally RECOMPUTED from
  `FoodHeadroom`.
- `docs/queue.md:66-70` (T2.8 adversarial item (2), rebound release gated on `unsuppressed > 0`):
  ANNOTATED — the release gate stays on NOMINAL `d == 0.0` (`DemographicsSystem.cs:181`,
  byte-identical); the `unsuppressed > 0` half is untouched and the item stays open.

**Does NOT change:** ADR-011 exponential survival `1 − e^{−m h}`, `1 − e^{−s h}` (`:204-209`);
`MicroStepYears` 0.5; ADR-010 aging (`:211-218`); the rebound reservoir's bank/release arithmetic
and its nominal-`d == 0.0` release gate (`:178-187`); newborns credited before the sinks
(`:191-194`); the integer reconciliation order (`:221-267`); the vitals row and `DtYears` (`:269`);
`StarvationMortalityMaxPerYear` 0.12, the child/elder multipliers 1.5/1.3,
`FamineFertilitySuppressionSlope` 3.0, `ReboundRecoverableFraction` 0.5,
`ReboundReleaseRatePerYear` 0.08, `cohortWeights`, the T4.19 mortality/fertility vectors;
Consumption and T4.2 (no line).

---

## §2 MECHANISMS

### 2.1 `FoodHeadroom` — ONE definition of the food-influx limit (`Sim.Core/State/FoodHeadroom.cs`, new; static, pure; used by Demographics, Migration and the observer)

```
Limit(prev, s, cohortWeights, baskets):                 // N_lim in adult-equivalents; +∞ when absent
    D      = prev.ConsumptionDeficits[s].DemandUnits    (absent or 0 → +∞)
    dtPrev = prev.SettlementVitals[s].DtYears           (absent → +∞)
    S      = prev.GoodStocks[(s, staple)].LastProducedUnits          // staple = BasketBook's substitution target (grain)
    for each non-staple food good g (registry order):
        NS_g = prev.GoodStocks[(s, g)].LastProducedUnits              (absent → 0)
        b_g  = prev.GoodStocks[(s, g)].LastConsumptionDemandUnits / D  // realised basket share of g, [0, 1)
    // X = feedable food at the LIMIT population at the current basket mix — the fixed point of
    // Consumption's pass 1 / pass 2 evaluated at demand X:   X = S + Σ_g min(NS_g, b_g·X)
    surplus := all g; repeat (≤ |FoodGoods| times):
        X = (S + Σ_{g short} NS_g) / (1 − Σ_{g surplus} b_g)
        move every g ∈ surplus with NS_g < b_g·X to short    // monotone: X only falls; table order
    until no move
    return X / dtPrev                                       // ae-persons the influx feeds per year
Vacancy(prev, s, …) = max(0, Limit(…) − N_nutr(prev, s))    // N_nutr = Σ cohortWeights[c]·count over prev buckets
```

- **Exact mirror of one-directional substitution (F5).** At the limit population the staple
  covers its own share plus every non-staple shortfall; a non-staple covers only its own share. All
  non-staples in surplus ⇒ `X = S/b_staple` (Libur t110: `7458/0.9 = 8287`, `ρ_eff = 1.32` — the sum
  over food goods says 2.02× and is the mutant **M-DEM-SUMFOOD**); all short ⇒ `X = S + Σ NS_g`;
  mixed ⇒ the fixed point (≤ 3 iterations over ≤ 3 goods, table-ordered, no doubles sorted; the
  only ordering is the breakpoint order keyed `(X_g, GoodId)`).
- **The basket mix is the STANDING population's** (`b_g` from the realised demand that wrote prev):
  exact under a constant class mix; the class mix is a slow variable. Stated as the approximation
  it is.
- **Absolute, not ratio:** `N_lim = X/dt_prev` — right for a land-bound settlement (production does
  not scale with arrivals), conservative-then-geometric for a labour-bound one. `dt_prev` comes from
  `SettlementVitalsRow.DtYears`, so an era-boundary turn reads the dt that produced `X`. Equivalently
  `N_lim = N_nutr(t−1) × X/D` (food entries sum to 1.0 per class, `needs.json:67`).
- **Weather is inside `S`** (the realised multiplier): a bad draw is harmless to the cap (clamp
  `V ≥ 0`); a good draw over-states the limit for one turn and `k` limits how far it is chased.
  Weather-normalising `S` needs a `HarvestWeatherRow.AppliedMultiplier` field — queued for the v25
  window, not taken.
- **Identity arm:** `+∞` whenever the inputs are absent — every hand-built rig
  (`new ConsumptionDeficitRow(s, d)` defaults `DemandUnits` to 0) and every founding turn.

#### 2.1a The null arm — AMENDED T4.21-4 (RULE 2): catchment ROW ABSENCE

The null arm's meaning is **"the influx is UNMEASURED"**, and a settlement whose catchment has never
been computed could not have produced. `FoodHeadroom.Limit` therefore returns `+∞` when
`prev.CatchmentSummaries` carries **no row** for the settlement, ahead of the deficit / demand /
vitals arms.

- **Mechanism, confirmed at file:line.** `ProductionSystem.Farm` (`ProductionSystem.cs:211-216`)
  scans `prev.CatchmentSummaries` for `EffectiveArableKm2`; with no row `arableKm2` stays `0.0`, so
  `landSide = 0` and `ratePerYear = min(landSide, laborSide) = 0` (`:230-233`). `S = 0` gives the
  fixed point `X = 0` and `N_lim = 0` — a **structural unavailability of the measurement**, not a
  measured zero capacity.
- **Keyed on ROW ABSENCE ONLY — never on `production == 0` and never on `arable == 0`.** A zero
  influx with the catchment computed is the **ABANDONED** settlement's *genuine* zero, which must
  keep `N_lim = 0` (`D_Cap_NoGrowthOnAGranary`, the abandonment semantics). `CatchmentSystem`
  (`CatchmentSystem.cs:138-140`) emits one row per settlement in `prev.Settlements` unconditionally
  on labour and sectors, so a real abandoned settlement always has its row and never reaches this
  arm; and `FoodState.IsAbandoned` (`FoodState.cs:119-129`) reads the raw sector row, nothing to do
  with the catchment.
- **Test.** `FoodHeadroomTests.H_NullArm_NoCatchmentRow_IsPositiveInfinity_ButAbandonedWithARowIsZero`
  asserts both arms of the one decision together: no row (every other input present) ⇒ `Limit` and
  `Vacancy` are `+∞`, the same rig WITH the row is finite, and a row with `arable > 0` and zero food
  production ⇒ `Limit == 0`, `Vacancy == 0`. Keying on `arable == 0` or on `S == 0` collapses the
  second case into the first and fails it.
- **Rigs.** Rigs that assumed the finite arm now carry the row they implied: `FoodHeadroomTests.Rig`
  (new `catchmentRow` parameter, default true), `DemographicShockTests.CappedRig` (arable 5000 — the
  abandoned case above), `D_Headroom_CountsArrivals`'s inline world and `MigrationBoundedTests.Vacant`.
  The migration rigs take **arable 0**, which is attractiveness-neutral by construction:
  `MigrationSystem.cs:296-299` leaves `arableKm2` at `0.0` when no row matches, so an absent row and
  a zero-arable row are the same number to attractiveness and only the `FoodHeadroom` arm moves.

See ADR-025 §2.4a for the measured scope of this arm and for what it does **not** close.

### 2.2 Kernel changes (`DemographicsSystem.cs:122-141, 162-195`)

**(i) Mortality and fertility on the effective deficit (G3(b)).** Once per settlement from PREV
beside `deficit` (`:128-135`): `state = FoodState.Of(prev, s, cfg, out _)`, `dEff =
FoodState.EffectiveDeficit(deficit, state, cfg)`. Then `:136` `suppression = max(0, 1 − slope ×
dEff)` and `:139` `starveRate[c] = StarvationRate(d, c, dEff)`. In FAMINE `dEff == deficit`, so both
channels read the whole deficit; outside FAMINE both read the adapted remainder. `:181` (release
gate `deficit == 0.0`) is byte-identical on nominal `d`. dt-invariance holds by construction:
`dEff` is a per-turn scalar and `e^{−s h}` composes. The birth full-stop sits at nominal
`d = a + (1 − a)/3 = 0.4667` outside FAMINE and at `1/3` inside it.

**(ii) Headroom growth cap.** Once per settlement per turn:

```
N_lim = FoodHeadroom.Limit(prev, s, cohortWeights, baskets)                 // +∞ ⇒ cap skipped
N_now = Σ_rows cohortWeights[c] × pop_row over the OWNED buckets at step start   // post-migration, post-colonization, post-revolt
H_0   = max(0, N_lim − N_now)                                                // ae; refugees who arrived this turn have used headroom
```

`H_0` is clamped at 0: the cap is a GROWTH limiter (unclamped, one ordinary 0.44 draw with a full
granary would cut births 73 %); a stockpile is not an influx (an abandoned settlement on its granary
has `S = 0` ⇒ `N_lim = 0` ⇒ births ≤ deaths — the "large stockpile" pin). No `d == 0` gate.

Inside the micro-loop, per step `h`, order-consistent with births-before-sinks:

```
H_rem := H_0 (local; never state)
per step h:
  PASS A, per group g (anchored on its cohort-0 row), no state mutation:
      unsuppressed_g = Σ_j f_c pop_j W(λ_c h) h                    (verbatim :167-175)
      base_g    = unsuppressed_g × suppression                     (verbatim expression of :177)
      bank_g    = ReboundRecoverableFraction × (unsuppressed_g − base_g)   // from the UNCAPPED pair
      release_g = (d == 0.0 ∧ unsuppressed_g > 0) ? (reservoir_g + bank_g) × min(1, r h) : 0
      cand_g    = base_g + release_g
  CAP (skipped entirely when H_0 = +∞):
      D_pre   = Σ_rows pop_i (1 − e^{−(m_c + s_c) h}) × w_c        // this step's deaths of the standing population, ae
      A_step  = Σ_rows pop_i e^{−(m_c+s_c) h} × (h/5) × (w_{c+1} − w_c) over rows whose aging destRow exists
      allowed = (1 − e^{−k h}) × H_rem
      bornMax = (allowed + D_pre − A_step) / (W(λ_0 h) × e^{−λ_0 h} × w_0)   // newborn survivors after this step's cohort-0 sinks, heads
      m       = Σ_g cand_g > 0 ? min(1.0, max(0.0, bornMax) / Σ_g cand_g) : 1.0
  PASS B, per group g, committing:
      reservoir_g += bank_g                                         (verbatim :179)
      rel = release_g;  if (m < 1.0) { cand_g *= m; rel *= m; }    // guarded: the fed path executes today's instruction sequence
      reservoir_g −= rel;  born_g = cand_g
      birthsExact, survivors, infant deaths, pop[cohort0] += survivors   (verbatim :190-194)
  sinks and aging                                                   (verbatim :199-218)
  H_rem −= (N_nutr_after − N_nutr_before)                           (only when capped)
```

- **Order-consistent:** the deaths this step's births may replace are a closed form of the
  pre-birth state (`D_pre`); no lag, no carried state. Newborns face the same step's sinks and age
  `h/5` into cohort 1 within the step; the aging is weight-neutral ONLY because `cohortWeights[0] ==
  cohortWeights[1]` (0.6) — the exactness of `bornMax` DEPENDS on that data fact; the packet asserts
  it (`D_CohortWeights_NewbornAgingWeightNeutral`) and the replica carries the general `(w_1 − w_0)`
  term so a future weight change degrades to a bounded, not silent, error.
- **Exactly 1.0 when headroom is large:** `min(1.0, big)` returns the literal `1.0`; the guard makes
  the fed path instruction-identical.
- **When the cap binds with `bornMax ≤ 0`:** births are 0 but `N_nutr` may still rise by aging
  drift (children maturing 0.6 → 1.0); the exact statement is `N_nutr(end) ≤ N_lim + Σ_steps max(0,
  A_step − D_pre)` while binding, and the §28 pin asserts THAT bound plus `d == 0` every turn, with an
  all-weights-1.0 twin where `N_nutr ≤ N_lim` is exact.
- **Composition:** when binding, `H_rem(end) = H_0 e^{−k dt}` exactly regardless of how dt is cut;
  across a turn boundary the cap re-reads `N_lim` from prev (CR-001 (a)); on a land-bound rig one
  dt-10 turn = two dt-5 turns within flooring (`D_Cap_DtComposes_LandBound`).
- **Reservoir: strict, deferred-not-invented.** `bank` is taken from the UNCAPPED pair —
  headroom-withheld births are not deferred conceptions and banking them would invent births under
  a sustained ceiling (§28). `release` is scaled by `m` and the unreleased part STAYS banked.

**Test replica** (`Sim.Tests/TestUtil/DemographicsReplica.cs`) gains `dEff`, `suppressionArg` and
`N_lim` parameters defaulting to (`deficit`, `deficit`, `+∞`) and mirrors PASS A / CAP / PASS B.

---

## §3 CONSTANTS

| constant | value | CHOSEN / DERIVED — frame | null / identity arm |
| --- | --- | --- | --- |
| `foodState.adaptationAbsorbableShortfall` `a` | 0.20 | ADR-024 §3 (physiology; the director's 15–25 % band) | `a = 0` reproduces today's response bit-for-bit |
| `demographics.headroomRelaxationPerYear` `k` | ln 2 / 10 ≈ 0.0693/yr | CHOSEN — the director's "the gap halves per decade"; `(1 − e^{−k h}) = 3.41 %` of remaining headroom per half-year. ALSO the vacancy relaxation of ADR-025 §2.4 — one law, one constant | none in config by design (a structural bound — law 2 forbids a switch); identity arm `N_lim = +∞` (every existing rig); removal arm is the mutant **M-DEM-CAP** |
| everything else | unchanged | | |

---

## §4 dt-CORRECTNESS, DETERMINISM, BIT-IDENTITY

- **dt (law 3):** `dEff` is a per-turn scalar composed through `e^{−s h}` (ADR-011);
  `allowed = (1 − e^{−k h}) × H_rem` composes exactly across micro-steps and turns;
  `DtInvariance_StarvationFlow_ExactAcrossDts` (dt 10 / 5 / 2.5) and `D_Cap_DtComposes_LandBound`.
- **Determinism (law 5):** no RNG; table-ordered scans; the only new ordering (`FoodHeadroom`'s
  breakpoints) is keyed `(X_g, GoodId)`; no dictionaries, no LINQ, no floats.
- **Isolation (law 6):** `FoodState` and `FoodHeadroom` are statics on `IReadOnlyWorldState`;
  Demographics reads prev tables it already reads plus `GoodStocks`/`SettlementVitals` (prev).
- **Conservation (law 1):** births and deaths still move through the existing flows; the cap
  scales a birth CANDIDATE before it is realised; no stock is created or destroyed by it.
- **Bit-identity ledger:**

| regime | identical? | why |
| --- | --- | --- |
| settlement with `d = 0` (any headroom) | YES except the cap | `dEff = 0` ⇒ `starveRate = 0`, `suppression = 1` (as today); reservoir arithmetic identical; cap `m = 1.0` literal ⇒ guarded ⇒ identical **iff `H_0` ≥ step growth**, i.e. any settlement whose last-turn feedable influx exceeds its demand by more than one step's births |
| any settlement with `d > 0` | NO (intended) | `dEff` differs from `d` wherever `d > a` outside FAMINE; suppression differs wherever `d > 0` outside FAMINE (G3(b)); FAMINE rigs at any `d` and Default-sector rigs at `d = 1` (`d_eff = (1 − 0.2)/0.8 = 1`) are unaffected in the starvation channel |
| hand rigs without a demand row (every `PopulationExactnessTests` rig) | YES | `N_lim = +∞` ⇒ cap skipped |
| `FedGrowth_DtInvariant` (yield 1e9) | YES | `H_0` huge ⇒ `m = 1.0` literal |

---

## §5 S8 §4.1 ITEMS FOR THIS PIECE

- **Audit:** `StarvationMortalityMaxPerYear` 0.12 (chosen, no `_doc` — T4.21-1 traces it; 12 %/yr
  at zero food is not physically possible in a year, a decade-average compromise — F3, information),
  `StarvationChild/ElderMultiplier`, `FamineFertilitySuppressionSlope` 3.0, the reservoir constants,
  `cohortWeights[16]` (`[0] == [1] == [2] = 0.6` — the exactness dependency), `MortalityPerYear`,
  `FertilityPerPersonPerYear`, `MicroStepYears`, `DemandUnits`, `SettlementVitalsRow.DtYears`,
  `GoodStockRow.LastProducedUnits` and `LastConsumptionDemandUnits` (F5), basket food shares.
- **Dimensional declaration:** `d_eff`, `suppression`, `m`, `b_g` dimensionless; `k`,
  `StarvationMortalityMaxPerYear`, `ReboundReleaseRatePerYear` per sim-year; `h`, `dt_prev` years;
  `S`, `NS_g`, `X`, `D` food units = person-year-equivalents (carry `dt_prev`); `N_lim`, `V`, `N_nutr`,
  `N_now`, `H_0`, `H_rem`, `D_pre`, `A_step`, `allowed` adult-equivalents; `born`, `bornMax` heads;
  `w_c`, `w_0` ae/head. Checks: `X = [food] + Σ min([food], [1]·[food])` ✓; `N_lim = [food]/[yr] =
  [person-year]/[yr] = [ae]` ✓; `allowed = (1 − e^{−[1/yr][yr]})·[ae]` ✓; `bornMax = ([ae] + [ae] −
  [ae])/([1]·[1]·[ae/head]) = [head]` ✓; `min(1, bornMax/Σcand)` [1]/[1] ✓.
- **Corridor independence:** `a` physiology, `k` the director's relaxation frame — neither fitted
  to an outcome; `θ_sev = a` so no second threshold exists to fit. `canonical.densityPerArableKm2`:
  the cap can only LOWER growth — expected unchanged at 20 seeds within noise. `dev.
  starvationRatePer1000`: the dead-zone REMOVES weather-driven starvation below `a`; disasters add it
  — pre-committed reading in CR-015 N7.
- **Coupling map:** CR-015 §4 rows 2, 3, 7.

---

## §6 TESTS THAT PIN EACH PROPERTY (spec §6.4)

`H_Limit_ExactMirrorOfSubstitution` (Default-mix rig: `X == 7458/0.9` to the ulp; livestock-short rig
`X == S + NS`; mixed rig the fixed point; absent row ⇒ +∞; kills **M-DEM-SUMFOOD**) ·
`D_Asymptote_DefaultMix_LivestockSurplusIsNotFood` (kills **M-DEM-SUMFOOD** semantically: a deficit
turn appears) · `D_Cap_Identity_LargeHeadroom` · `D_Cap_NoGrowthOnAGranary` (kills **M-DEM-CAP**) ·
`D_SmallStockpile_DeficitChannelOwnsDecline` (the cap never removes a person) ·
`D_Asymptote_NoOvershootUnderConstantConditions` (§28: `N_nutr` non-decreasing toward `N_lim`,
`d == 0` every turn, the aging-drift bound; all-1.0-weights twin exact; kills **M-DEM-CAP**) ·
`D_Cap_DtComposes_LandBound` · `D_CohortWeights_NewbornAgingWeightNeutral` ·
`D_Starvation_ReadsEffectiveDeficit` (Stress ⇒ 0; Severe `d = 0.4` ⇒ replica at `d_eff = 0.25`;
abandonment `d = 0.4` ⇒ replica at 0.4; kills **M-DEM-ADAPT**) · a fertility twin
`D_Suppression_ReadsEffectiveDeficit` (G3(b): Stress births unsuppressed; Severe `d = 0.4` ⇒
suppression `1 − 3.0 × 0.25`; FAMINE `d = 0.4` ⇒ `1 − 3.0 × 0.4`) ·
`DtInvariance_StarvationFlow_ExactAcrossDts` · `D_Rebound_StrictRelease_NeverInvents` (kills
**M-DEM-BANKCAPPED**) · `D_PostFamine_RecoversTowardLimit` · `D_Headroom_CountsArrivals` ·
`DemographyRetuneTests.Famine_MortalitySpike` phase 1 re-anchored + abandonment twin (CR-015 G2) ·
replica-vs-kernel exactness at a capped rig (verifier). Every mutant bounded at 3× the clean-suite
baseline; a hang is a finding (ADR-015 §7.1).
