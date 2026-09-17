# PRE-REGISTERED BEFORE ANY COUNTERFACTUAL RESULT EXISTED

Lane: pre-registration (Step 3, PREDICT BEFORE INTERPRETING). Written 2026-09-17 against
build 45046eb (Sim.Core/Sim.Data byte-identical to candidate 2807155). No replay was run by
this lane. No counterfactual output was consulted (none existed). Every number below is either
(a) read from the director's observed Windows run (seed 42, 2807155, 161 turns; the reference
metrics script is `cascade/base_reference_metrics.py`, the analytic table is
`cascade/g_analytic.py`), or (b) derived by hand from the transfer functions at the cited
file:line. Tags: CONFIRMED / PROBABLE / POSSIBLE / REJECTED / UNRESOLVED.

Comparison rule for every prediction: the counterfactual is judged against the LINUX BASE
replay (apples to apples, CR-013/ADR-022). The Windows numbers quoted here are the director's
observed run and are used only to fix a-priori thresholds; where the Linux BASE differs from
them, the thresholds below are re-expressed as the stated FRACTION of the Linux BASE value.

---

## 0. Reference values from the observed run (Windows, 2807155, seed 42) — the anchors

Shared-definition metrics over 1932 settlement-turns (12 settlements x 161 turns):

| metric | observed value |
|---|---|
| outflowFrac median / mean / p90 / p99 / max | 0.00134 / 0.00754 / 0.0115 / 0.122 / 0.7923 |
| settlement-turns with 0 < outflowFrac < 0.05 | 1077 (outflowFrac == 0: 796; >= 0.05: 59; >= 0.20: 9; >= 0.50: 2) |
| share of total outflow persons in settlement-turns with outflowFrac >= 0.05 / >= 0.20 | 58.1% / 22.9% |
| famine settlement-turns deficit > 0 / >= 0.05 / >= 0.20 | 15 / 3 / 1 |
| famine episodes (maximal runs) | 15, EVERY ONE of length 1 (there is no episode of 2+ consecutive turns in the whole run) |
| loss >= 20% events | 13 (9 of them at Libur) |
| recovery time for those events | Libur t85, t93, t102, t118, t160: 1 turn each (rebound by inflow); t89: 30; t124: 37; t123: not recovered |
| max recovery overshoot within 5 turns | 0.948 (Libur t118), 0.643 (Libur t85) |
| maxInflowFrac | 17.58 (Libur t119: 1213 into opening 69); inflowFrac >= 0.5 on 6 settlement-turns |
| oscillation episodes (loss >= 30% then gain >= 100% of trough within 3 turns) | 3 (all Libur: t85, t93, t118) |
| grossMigrationPerDecade | 0.00712 (sumOut 11335 / person-years 15,910,080 x 10); corridor band [0.001, 0.01] |
| world-population crashes (AutoplayMetrics.Crashes, 0.20) | 0 (world pop 5191..16948, monotone-ish) |
| aggregate starvation deaths, whole run | 246 (turn-level population.starvation) |
| Libur t118 deaths / opening | 103 / 809 = 0.127 |

Famine -> next-turn outflow pairs (push = prev deficit, read by MigrationSystem.cs:182-184):

| settlement, famine turn | deficit | next-turn outflowFrac | gain = outflowFrac / deficit |
|---|---|---|---|
| Zareshu (2) t56 | 0.0483 | 0.205 | 4.25 |
| Libur t84 | 0.1268 | 0.544 | 4.29 (saturating regime, see G) |
| Libur t92 | 0.0783 | 0.378 | 4.83 |
| Libur t101 | 0.0499 | 0.227 | 4.55 |
| Libur t117 | 0.2972 | 0.792 | 2.67 (saturated, see G) |
| Libur t151 | 0.0120 | 0.068 | 5.64 |
| Libur t159 | 0.0472 | 0.242 | 5.13 |

Linear-regime gain (0.01 < deficit < 0.10), mean of 5 events: **4.88 outflow-fraction per unit deficit per turn**.

Observation that frames the whole pre-registration (CONFIRMED from the run, not interpretation):
by the director's own definition, NONE of the 15 famine episodes is a "sustained crisis" (all are
one 10-year turn long), and the deepest is deficit 0.297 (a 30% shortfall for one decade). Yet
two of them removed 54% and 79% of the settlement in one turn. The pathology to be tested is
therefore precisely: **outflowFrac >= 0.5 following a LENGTH-1 famine episode with deficit
< 0.35**. The observed run has 2 such settlement-turns.

---

## 1. DEFINITIONS (numeric, fixed a priori)

Let X_base be the Linux-BASE value of a metric and X_cf the counterfactual value.

**D1. "Substantially reduce"**: X_cf <= 0.5 x X_base (a factor of two) for count/extremum
metrics (maxOutflowFrac, count of settlement-turns with outflowFrac >= 0.20, oscillation
episodes, maxInflowFrac, maxOvershoot, famine settlement-turns). Rationale: the transfer
functions predict order-of-magnitude changes where a mechanism is truly on the path (e.g. M1
takes the flight sum D from ~3.5 to ~1.0, x0.29 in the linear regime, section 3), and single-seed
noise in these counts is a few units at most; a 2x bar is therefore conservative to every
hypothesis and well outside noise. For maxOutflowFrac there is an additional ABSOLUTE bar:
substantially reduced means maxOutflowFrac <= 0.40 (the director's "should not normally cause
~80% departure" translated to "no more than 40% of a settlement leaves in one decade after a
one-decade shock").

**D2. "Preserve ordinary low-level migration"** (all three must hold):
- median outflowFrac in [0.5, 2.0] x BASE median (Windows anchor 0.00134);
- count of settlement-turns with 0 < outflowFrac < 0.05 within +-20% of BASE (Windows anchor
  1077 of 1932);
- grossMigrationPerDecade inside the corridor band [0.001, 0.01] AND >= 0.4 x BASE (Windows
  anchor 0.00712 -> floor 0.0028). The 0.4 floor is derived: 58% of BASE's outflow persons sit
  in surge turns (outflowFrac >= 0.05) and the post-surge refill inflows are the other side of
  those persons; removing both cannot legitimately remove more than ~60% of gross flow.

**D3. "Without materially changing"** (tolerances for metrics a counterfactual is NOT supposed
to touch):
- HarvestWeather multiplier series: bit-identical to BASE for M1, M2, M2b, M4 (the weather RNG
  stream is keyed by (system x settlement) and drawn in table-row order before any state is read,
  HarvestWeatherSystem.cs:92-99, so population feedback cannot reach it; tolerance = 0).
- famine settlement-turns (deficit > 0 and >= 0.05): within +-3 settlement-turns of BASE for
  M1/M2/M2b/M4 (populations diverge, so demand diverges; +-3 covers the marginal cases at
  deficit < 0.005 that the observed run shows at s1, s3, s5, s7, s8);
- world population at t161: within +-10% of BASE for M1/M2/M2b/M4; within +-15% for M3/M3b
  (weather changes yield realisations everywhere; the mean multiplier is exactly 1 in both,
  HarvestWeatherSystem.cs:142,201, so any drift beyond 15% signals a population-feedback change,
  not a weather-mean change);
- linear-regime cascade gain (outflowFrac(t+1) / pushDeficit for 0.01 < pushDeficit < 0.10):
  within +-25% of BASE mean (Windows anchor 4.88) for M3/M3b — the migration code is untouched
  there, and the gain is set by D x 2.4 x mean-cohort-profile (section 3), which weather does
  not enter.

**D4. "Eliminate the pathological oscillation"**: oscillationEpisodes = 0 (Windows anchor 3),
AND no settlement-turn with loss >= 0.50 followed within 3 turns by inflowFrac >= 1.0.

**D5. "Post-crisis immigration spike"**: maxInflowFrac (Windows anchor 17.58) and maxOvershoot
(Windows anchor 0.948). "Substantially reduced" per D1 is <= 0.5x; the ABSOLUTE bars that
correspond to the director's "gradual repopulation" are maxInflowFrac <= 1.0 (nobody more than
doubles in one decade by immigration) and maxOvershoot <= 0.30.

**D6. "Sustained crisis"** (the case where large migration IS legitimate): a famine episode of
length >= 2 consecutive turns with deficit >= 0.20 in each. BASE has zero such episodes. Any
counterfactual that produces one is allowed large outflow there without penalty.

**D7. "Pathological single-turn cascade"** (the thing to eliminate): a settlement-turn with
outflowFrac >= 0.5 whose push deficit came from a length-1 famine episode with deficit < 0.35.
BASE anchor: 2 (Libur t85, t118). Success = 0.

**D8. "Recovery gradual"**: for any loss >= 20% event, recovery time >= 2 turns and <= 15 turns
(20-150 years). BASE anchor: five 1-turn "recoveries" that are really the immigration spike.

---

## 2. PREDICTIONS

Notation: D_i = sum_j damping(i->j) x viability(j) — the multiplier on the flight term that
MigrationSystem.cs:397-403 sums over destinations; K = BaseRatePerYear x dtYears x
FamineFlightFactor = 0.03 x 10 x 8.0 = 2.4 (per unit deficit, per unit D, per unit
CohortProfile); per-bucket flight fraction in BASE = min(1, K x profile_c x D_i x deficit_i)
(the min is the overdraw scaler at :430-431).

### A — Weather (the director's prediction A: the trigger is weather variance calibrated for
one year but applied to a decade)

Hypothesis: sigmaLogYield 0.2936 is a ONE-year SD (sim.json "harvestVariance"._doc) applied as
the stationary SD of a process whose AR(1) memory at dt=10 is rho = e^(-10/3) = 0.0357
(HarvestWeatherSystem.cs:138), so each turn draws a fresh full-variance multiplier for a whole
decade; the shocks are therefore too frequent/deep for a decade, and that is what triggers the
famines.

IF (M3, sigma -> 0.1933) THEN:
- famine settlement-turns with deficit >= 0.05: <= 0.4 x BASE (anchor 3 -> <= 1); with deficit
  >= 0.20: 0 (anchor 1). Derivation: P(mult < 0.6) falls 0.0556 -> 0.0054 (x0.10) and
  P(mult < 0.8) falls 0.270 -> 0.145 (x0.54) under the stationary lognormal with exact mean 1;
  Libur's famine threshold is around mult ~0.6 (it famines on 7 of 161 turns = 4.3%, which is
  the z = -1.71 tail, mult = exp(-1.71 x 0.2936 - 0.043) = 0.58).
- Libur t117 deficit: <= 0.10, PROBABLY 0. Derivation: the same innovation sequence gives
  x' = 0.6585 x (bit-for-bit up to rounding, because x_t = sigma x sum_k rho^k c inn_{t-k} is
  linear in sigma), so mult' = 1.00975 x mult^0.6585; the BASE t117 multiplier estimated from
  per-capita harvest (4.25 vs ~9.5 normal) is ~0.43-0.48 -> M3 0.58-0.62, i.e. production
  x1.30-1.35, which exceeds the 6588 demand given t117's obtained 4630 + 450 store.
- maxOutflowFrac: <= 0.5 x BASE ONLY BECAUSE the triggering deficits are smaller — see F: the
  gain per unit deficit is unchanged.
- grossMigrationPerDecade: 0.5-0.9 x BASE, still in band (corridors.json note: the corridor is
  weather-driven; sigma 0->0.5 moved it 3.7x).
ELSE (A rejected as a sufficient explanation) if under M3 famine settlement-turns (>= 0.05)
remain >= 0.7 x BASE, or if a residual famine with deficit in [0.05, 0.15] still yields
outflowFrac >= 0.40 (the cascade fires at normal severity regardless of sigma).

Prior belief: PROBABLE that M3 removes most famine turns on this seed; REJECTED a priori as the
"cause of the cascade" — sigma sets frequency, not the amplification (see F, G). Note the
director's governance: any weather calibration change needs an explicit ruling, and CR-003
ruling 3 wants multi-year droughts to be possible; at dt=10 the correlation time 3y is
invisible either way (rho 0.036), which is a separate frozen-item tension to record, not to fix.

### B — Migration elasticity (the director's prediction B: the flight term is the amplifier)

Hypothesis: given the same deficit, the flight response at MigrationSystem.cs:393-404,
415-431 (linear rate x dt, summed over destinations, capped only by the 100% overdraw ceiling)
converts a moderate one-decade deficit into near-total departure.

IF (M1: EMA stress, exact hazard 1-exp(-h dt), per-turn ceiling, destination-normalised
weights so the sum over destinations is 1 instead of D) THEN, at the Libur t118-equivalent
turn (push deficit ~0.297):
- outflowFrac in [0.05, 0.40]; analytic centre 0.29 if stress = deficit with no EMA lag, 0.16
  if the EMA halves it on the first turn (computed with D_eff = 1, exact hazard, stable pyramid:
  section 3);
- cumulative outflow over the 3 turns t118-t120 divided by t118 opening <= 0.45 (so that
  smoothing has not merely deferred the same surge);
- maxOutflowFrac over the run <= 0.40 and <= 0.5 x BASE; count of settlement-turns with
  outflowFrac >= 0.20: <= 0.5 x BASE (anchor 9);
- D7 pathological single-turn cascades: 0 (anchor 2);
- oscillation episodes: <= 1 (anchor 3) — M1 alone removes the trough, which removes the
  refill; but see D for why 0 is not promised by M1 alone;
- D2 preserved: median outflowFrac, the 0<x<0.05 count and gross migration within their bounds
  (the gap channel is untouched by M1);
- D3: weather bit-identical; famine settlement-turns within +-3.
ELSE (B rejected) if under M1 the Libur t118-equivalent outflowFrac >= 0.5, or the 3-turn
cumulative >= 0.7, or maxOutflowFrac > 0.5 x BASE.

Prior belief: CONFIRMED-level from the observed run's own arithmetic (G below): two independent
events are reproduced by the BASE formula with one D; the formula's ceiling is reached by prime
adults at deficit >= 0.119 when D = 3.5. The uncertainty in B is not whether the term amplifies
but what M1's specific ceiling and EMA constants do on the first turn.

### C — Destination attraction (the director's prediction C: the post-crisis immigration spike
is the 1/P vacancy signal)

Hypothesis: instant attractiveness A_s = LandWeight x EffectiveArableKm2_s / max(P_s, 1)
(MigrationSystem.cs:188-189) reads an emptied settlement as the most desirable place in the
world; smoothed with alpha = min(1, 10/20) = 0.5 (:243) it jumps in one turn, and the gap
channel refills the settlement past its pre-crisis size.

IF (M2: denominator max(P, populationMemory), slow EMA) THEN at the t119-equivalent:
- Libur instant A_119 <= 1.25 x A_118 (vs x11.72 in BASE) provided the memory window is >= 50
  years (alpha_m <= 0.2: mem_119 >= 0.8 x 809 + 0.2 x 69 = 661, A = 1414/661 = 2.14 vs BASE
  1.748 at t118); S_119 <= 2.0 (vs 11.13 BASE); gap seen by neighbours <= 0.25 (vs 9.36);
- Libur inflow at t119 <= 0.25 x BASE (anchor 1213 -> <= ~300). Analytic centre ~150: linear
  gap desire = 0.03 x 10 x 0.504 x (sum_i P_i damping_iL) x gap ~ 0.15 x (15,000 x 0.35) x 0.19;
- maxOvershoot <= 0.30 (anchor 0.948); maxInflowFrac substantially reduced per D1 but NOTE:
  under M2 alone the t119 opening is still ~69 (the outflow is untouched), so inflowFrac =
  150/69 ~ 2.2 can still exceed 1.0; the ABSOLUTE bar maxInflowFrac <= 1.0 is predicted only
  for M4;
- outflow metrics (maxOutflowFrac, D7 count) NOT materially changed vs BASE (within +-1 event) —
  M2 does not touch the flight term;
- oscillation episodes: <= 1 (the loss still happens; the gain >= 100% of a 69-person trough
  needs only 69 immigrants, so a residual episode is POSSIBLE even at 0.25 x BASE inflow —
  which is why D4 also carries the loss >= 0.5 / inflowFrac >= 1.0 clause).
ELSE (C rejected) if under M2 the Libur t119-equivalent inflow >= 0.5 x BASE, or maxOvershoot
>= 0.6 — the refill is then carried by something other than the 1/P denominator (e.g. the gap
cap m* at :375-379, which uses raw population, not memory, and would then need its own
counterfactual).

Prior belief: CONFIRMED that the mechanism exists and is the sole origin of S = 11.13 (section 3
reconstructs S_119 and S_121 exactly from telemetry with R = 1414.1); PROBABLE that M2 removes
>= 75% of the spike; POSSIBLE residual through the uncapped part of the gap desire.

### D — Interaction (the director's prediction D: flight surge and vacancy pull are a loop, and
only removing both removes the oscillation)

Hypothesis: the oscillation (t85 -> t86, t118 -> t119) needs both halves: the flight surge
creates the vacancy, the vacancy signal pulls in more than left, the enlarged population is
then more exposed at the next shock (demand 12,655 vs harvest at t120 shows the refilled Libur
at 1520 on land that fed 800), so the loop can re-fire (t123-t124 losses of 22% and 34% follow
the refill).

IF (M4 = M1 + M2, current weather) THEN:
- oscillation episodes = 0; D7 count = 0; maxOutflowFrac <= 0.40; maxInflowFrac <= 1.0;
  maxOvershoot <= 0.30 (all absolute bars met simultaneously);
- for any loss >= 20% event that remains, recovery is gradual per D8 (2..15 turns), not
  1 turn;
- D2 preserved and D3 tolerances met (weather bit-identical; famine turns +-3);
- M4 is at least as good as the better of M1 and M2 on EVERY one of the above (no
  interaction penalty).
ELSE:
- if M1 ALONE already gives oscillation 0 and maxInflowFrac <= 1.0, the STRONG form of D
  ("both are needed") is REJECTED — the vacancy pull is downstream of the surge on this seed
  and M2 is a secondary fix;
- if M4 shows a pathology absent from both M1 and M2 (e.g. a settlement with loss >= 20% that
  never recovers within the run while both single fixes recover it), the interaction is
  adverse and D is REJECTED as a composition claim;
- if M4 still has oscillation >= 1, D is REJECTED as a sufficient explanation and a third
  channel is implicated (candidates from the transfer functions: the decade-long starvation
  integration in DemographicsSystem.cs:136-140,277; the deficit gate of viability at
  MigrationSystem.cs:230 restoring viability 1.0 the turn after a famine; the gap cap m* using
  raw population at :377).

Prior belief: PROBABLE. The loop is visible in the observed sequence (t118 loss 0.915 -> t119
inflowFrac 17.6 -> t120 demand +93% -> t123/t124 losses), but on THIS seed M1 alone may already
break it, so the strong form is at real risk.

### E — Trade-off: "M1 converts emigration into starvation deaths"

Hypothesis: the people M1 keeps at home at t118 face DemographicsSystem.cs:136-140, 277:
starvation rate 0.12/yr x deficit x ageMultiplier read from the SAME prev deficit scalar for
the whole 10-year turn (0.5-year microsteps). At deficit 0.2972 the decade starvation fraction
is 1 - exp(-1.2 x 0.2972) = 0.300 for adults, 0.414 for children (x1.5), 0.371 for elders (x1.3).

IF (M1) THEN at the Libur t118-equivalent:
- deaths / opening >= 0.30 (anchor 0.127; BASE had only ~168 stayers);
- Libur t118 starvation deaths >= 150 (vs BASE ~55-60: 168 stayers x ~0.35);
- run-total starvation deaths >= 1.5 x BASE (anchor 246);
- Libur closing at t118 in [350, 550] (opening ~809, minus 5-40% outflow, minus ~30-40% of
  stayers to starvation + ~25% base mortality over a decade, plus ~40% of a decade's births
  suppressed by max(0, 1 - 3.0 x 0.297) = 0.108 -> births ~ 0.11 x normal).
ELSE if M1's Libur closing >= 600 with starvation deaths < 100 the demographic wiring is not
reading the deficit the way the code says (re-open the "demographic wiring correct" finding).

Prior belief: PROBABLE. This is not a rejection of M1 — the director accepts "some mortality" —
but it must be reported alongside B, because a single one-decade deficit scalar driving a full
decade of starvation is the same dt-amplification pattern as the flight term, one system later.

### F — "M3 reduces famine frequency but does not change the per-famine cascade gain"

IF (M3, M3b) THEN:
- famine settlement-turns (>= 0.05) <= 0.4 x BASE (A);
- for the famine settlement-turns that remain with 0.01 < deficit < 0.10, the gain
  outflowFrac(t+1)/deficit is within +-25% of the BASE mean (anchor 4.88); per-cohort
  saturation (moved/prevCount = 1.0 for cohorts 3-4) occurs at exactly the same deficit
  threshold as BASE (deficit >= 1/(2.4 x D) = 0.119 at D = 3.5) — because nothing in
  MigrationSystem reads sigma;
- outflowFrac conditional on deficit bin (0-0.05, 0.05-0.10, 0.10-0.20, 0.20-0.30) under M3 is
  indistinguishable from BASE within the +-25% gain band.
ELSE F is REJECTED if the conditional gain shifts by > 50% (which would mean the flight sum D —
i.e. neighbour viability or damping — moved with the weather, a real spatial-correlation
coupling worth reporting).
UNRESOLVED if M3 leaves fewer than 3 settlement-turns with deficit > 0.01 (likely on one
seed: expected Libur famine count ~1): then F cannot be tested on seed 42 and must not be
reported as confirmed or rejected.

### G — "The flight hazard's destination sum D exceeds 1 for Libur, so a 0.3 deficit alone
over-draws prime cohorts" (analytic; tested against the instrumented BASE)

Expected BASE outflow fraction (flight only, per-bucket ceiling applied) for a stable pyramid
with children 41.0% / adults 56.6% / elders 2.4% (cohort shares
[0.160,0.131,0.119,0.109,0.097,0.085,0.073,0.061,0.050,0.040,0.030,0.021,0.013,0.007,0.003,0.001],
built from the sim's own mortalityPerYear with growth r = -0.0036/yr to match Libur's 41%
children):

| D \ deficit | 0.02 | 0.05 | 0.10 | 0.15 | 0.20 | 0.25 | 0.30 |
|---|---|---|---|---|---|---|---|
| 0.5 | 0.012 | 0.030 | 0.060 | 0.091 | 0.121 | 0.151 | 0.181 |
| 1.0 | 0.024 | 0.060 | 0.121 | 0.181 | 0.242 | 0.302 | 0.363 |
| 2.0 | 0.048 | 0.121 | 0.242 | 0.363 | 0.484 | 0.564 | 0.622 |
| 3.0 | 0.073 | 0.181 | 0.363 | 0.528 | 0.622 | 0.699 | 0.756 |

Per-cohort at deficit 0.2972 (fraction of the bucket that leaves): D=1: prime adults 0.713,
cohort 2 (10-14) 0.357; D=2: cohorts 3-5 all 1.000 (ceiling), cohort 2 0.713; D=3: cohorts
2-6 at 1.000, cohort 1 (5-9) 0.642, cohort 0 0.428, elders 0.02-0.11.
Prime-adult ceiling deficit = 1/(2.4 D): 0.833 (D=0.5), 0.417 (D=1), 0.208 (D=2), 0.139 (D=3).

Solving the BASE formula for the two observed events: 79.2% at 0.2972 needs D = 3.42; 54.4% at
0.1268 needs D = 3.73 (uniform-within-group pyramid instead: 4.17 and 4.77). The five
linear-regime events give D = gain / (2.4 x 0.504) = 3.5-4.7. All seven observed events are
consistent with ONE D in [3.4, 4.8]; the ceiling is NOT what caps the 79% — it is the
cohort profile (children and elders leave less) that stops it short of 100%.

IF (instrumented BASE reports damping and viability) THEN:
- sum_j damping(Libur->j) x viability(j) at t118 in [3.0, 5.0] (with viability(j) = 1.0 for
  every neighbour: all other settlements have deficit 0 at t117 and happiness 100 ->
  material x (0.85 + 0.15 x 1) = 1.0 at :230-234); equivalently mean damping over the 11
  neighbours in [0.27, 0.45], i.e. mean travel cost 20-33 cost units (exp(-cost/25), :274);
- at t118 cohorts 3-5 show moved/prevCount = 1.000 exactly (this needs only D >= 1.40), and
  cohort 2 saturates iff D >= 2.80; at t85 cohorts 3-4 saturate iff D >= 3.28 — a sharp test
  of D between the two events;
- elders' outflow fraction at t118 in [0.10, 0.30] while adults' is in [0.85, 0.95] (cohort
  gradient from CohortProfile), matching the observed elders 23 -> 10 alongside adults 455 -> 44.
ELSE G is REJECTED if the instrumented D_Libur < 2.0 (then the 79% needs another term — the
gap channel would have to be contributing, which the near-equal S values 1.78-1.79 at t118 make
implausible) or if prime cohorts show moved/prevCount < 0.95 at t118.

Prior belief: CONFIRMED to the extent that a purely analytic reproduction of two independent
events with one free parameter can be; PROBABLE for the exact interval.

---

## 3. ANALYTIC EXPECTATIONS per counterfactual (what the code MUST do if it does what it says)

**BASE (instrumented, Linux).**
- Libur R = AttractivenessLandWeight x EffectiveArableKm2 = 1414.1 (=> EffectiveArableKm2 ~
  18,100 km2). Reconstructed from telemetry: S_120 = 11.1313 = 0.5 x (S_119 + A_119) with
  S_119 = 1.7685 gives A_119 = 20.494; R = 20.494 x 69 = 1414.1; check A_118 = 1414.1/809 =
  1.748 -> S_119 = 0.5 x (1.7889 + 1.748) = 1.7685 (matches telemetry to 4 decimals); check
  A_120 = 1414.1/1520 = 0.930 -> S_121 = 0.5 x (11.131 + 0.930) = 6.0308 (matches 6.0308).
  CONFIRMED against the observed run. The instrumented Linux BASE must reproduce R_Libur within
  1% and the instant-A jump at t119 of x11.72 (809/69), giving S_119 in [10.9, 11.4].
- The gap seen by every neighbour at t119: 11.13 - ~1.77 = ~9.36; the resulting inflow is
  bounded by sum_i 0.25 x m*(i->Libur), m* = (R_L P_i - R_i P_L)/(R_L + R_i), estimated ~1700
  for ~15,000 persons elsewhere; observed 1213 sits below that cap, so the cap is close to but
  not fully binding — the instrumented run should show gapScale < 1 for at least the largest
  source pairs.
- The same D_Libur (section G) must also reproduce the t118 flight on Linux within the unit
  (the Linux BASE Libur trajectory is "comparable to the unit", CR-013).
- Weather: rho = 0.035674, innovationScale = 0.2936 x sqrt(1 - rho^2) = 0.2934 (factor
  0.99936); meanCorrection = 0.043100. Stationary P(mult < 0.6 / 0.7 / 0.8) = 0.0556 / 0.143 /
  0.270 per settlement-turn.

**M1.** With destination-normalised weights the flight multiplier is D_eff = 1 (or the max
weight <= 1). Exact hazard aggregate over the stable pyramid for h_c = 0.03 x profile_c x 8 x
stress per year over 10 years: stress 0.127 -> 0.138; stress 0.297 -> 0.285; if the EMA halves
first-turn stress: 0.073 and 0.159. Per-turn ceiling (if <= 0.40) is not reached by these
values. Expected D7 count 0. Weather series bit-identical to BASE. Any Libur closing at t118
above ~350 and below ~550 (E).

**M2.** A_119 = R / max(69, mem_119). With mem an EMA of population of window W years
(alpha_m = 10/W): mem_119 = (1 - alpha_m) x mem_118 + alpha_m x 69 (or reads prev, giving 809
directly). For W >= 50: A_119 <= 2.14, jump <= x1.22, S_119 <= 1.95, gap <= 0.25. Only
afterwards does vacancy slowly become desirability: mem decays toward 69 with time constant
W, so a settlement that STAYS empty regains attractiveness over ~W years — which is the
"gradual repopulation" target. The gap cap m* (:377) still uses raw population and is
unchanged. Weather bit-identical.

**M2b.** viability(j) additionally x (1 - hardshipEMA_j): Libur's own viability at t119 =
(1 - 0) x 1.0 x (1 - hardship) where hardship after one turn at 0.297 is alpha_h x 0.297
(0.149 at alpha 0.5): a further x0.85 on inflow at t119, decaying over the EMA window. Small
effect on its own; main value is preventing the refill from landing while the deficit signal
has already reset to 0 (deficit at t118 = 0 because the harvest recovered).

**M3.** Factor verified: rho_1y = e^(-1/3) = 0.716531; T + 2 x sum_{k=1..9} (T-k) rho^k =
43.3565; sqrt(43.3565 / 100) = 0.658456; sigma' = 0.2936 x 0.658456 = 0.19332. (This is the
SD of the 10-year arithmetic mean of a stationary AR(1) log-process with 1-year SD 0.2936 and
tau = 3 y — the statistic a decade-long turn should draw from; NOTE it is the SD of the mean
of the LOG yield, so it is a lower bound on the SD of the log of the mean yield by Jensen —
acceptable as the diagnostic's stated target, and the director must rule on any such change.)
Log-deviation series x' = 0.658456 x x_BASE at every settlement-turn (linearity of the AR(1)
in sigma; same RNG consumption). Multiplier relation: mult' = exp(0.658456 x (ln m +
0.043100) - 0.018682) = 1.00975 x m^0.658456. BASE-min examples: 0.35 -> 0.506, 0.40 ->
0.552, 0.45 -> 0.597, 0.50 -> 0.640, 0.60 -> 0.721. Tail probabilities: P(mult < 0.6 / 0.7 /
0.8) = 0.0054 / 0.040 / 0.145. Expected Libur famine turns ~1 (vs 7). The ranking of
weather turns is preserved, so every M3 famine settlement-turn before the first population
divergence is a subset of BASE's; after divergence, compare distributions only.

**M3b.** mult' = 1.0108 x m^0.5 (0.45 -> 0.678); P(mult < 0.6 / 0.7 / 0.8) = 0.0003 / 0.0092
/ 0.074. Famine essentially absent on this seed; useful only as the "flattened model" foil the
calibration principle warns against.

**M4.** Both of the above; the t118 trough is ~350-550 rather than 69, so even the residual
A jump is x1.5-2.3 in the RAW BASE denominator and ~x1.1 under memory; inflow at t119 tens of
persons; inflowFrac <= 0.2; maxInflowFrac over the run <= 1.0.

---

## 4. DISCONFIRMING SIGNALS (the single cleanest kill for each)

- **A (weather is the trigger that matters):** under M3, a settlement-turn with push deficit in
  [0.05, 0.15] still produces outflowFrac >= 0.40. That shows the pathology fires at normal
  severity, so sigma only changes how often it fires — A is then demoted to "frequency
  control", not the target.
- **B (flight elasticity is the amplifier):** under M1, the Libur t118-equivalent outflowFrac
  >= 0.50, or the 3-turn cumulative >= 0.70 (the surge was deferred, not bounded). Either kills
  B as stated.
- **C (1/P vacancy pull is the spike):** under M2, Libur t119-equivalent inflow >= 0.5 x BASE
  (>= ~600 into ~69). The spike then rides on the gap cap or the flight term, not on the
  instant denominator.
- **D (the two halves form a loop that must be cut together):** M1 alone yields oscillation 0
  and maxInflowFrac <= 1.0 (strong form dead: M2 is downstream), OR M4 has oscillation >= 1
  (a third channel exists).
- **E:** M1 Libur t118 starvation deaths < 100 with closing >= 600.
- **F:** conditional gain under M3 differs from BASE by > 50% (or UNRESOLVED if < 3 famine
  turns).
- **G:** instrumented D_Libur < 2.0 at t118, or prime-cohort moved/prevCount < 0.95 at t118.

---

## 5. What this lane recommends the analysis lanes NOT do

- Do not read a reduction of Libur's 79% as success (calibration principle, Step 12); the
  pass/fail is D7 = 0 together with D2 preserved and D8 gradual.
- Do not compare Windows numbers to Linux counterfactuals; all seven predictions are stated as
  fractions of the Linux BASE.
- Do not treat M3's disappearance of famines as evidence about the cascade; F is the only
  M3 statement about the cascade, and it is likely UNRESOLVED on a single seed.
- Record, do not fix: at dt = 10 the correlation time 3 y has no effect (rho = 0.036), so the
  "multi-year drought" purpose in CR-003 ruling 3 is unreachable at the canonical turn length
  whatever sigma is — a frozen-item tension for the director, outside this investigation.
