# CR-001 — The demographic tempo, the era-pacing arc, and explicit-Euler cohort integration are mutually unsatisfiable

**Status: CLOSED — director ruled OPTION (a), exact-exponential cohort integration**
(T2.8 session, on this branch). (b) rejected: calendar-gated rates violate law 4 and
tune outcomes, not mechanisms. (c) rejected: forfeits the 6,000-year premise. The
mechanism is recorded in **docs/adr/adr-011-exponential-survival.md**; the
implementation is the directed T2.7b work (commit 2 on t2.8-calibration), including
the dt-invariance acceptance and the permanent era-boundary continuity test.

*(Original CR text below, kept verbatim for the record.)*

## 1. Frozen items in conflict

1. **The T2.7 demographic tempo** (ratified packet acceptance): pre-modern crude rates
   with long-run fed growth in **[0.05, 0.1] %/yr**, measured and accepted.
2. **The era-pacing arc** (D-006 data, Spine-frozen pacing design): dt shrinks
   10 → 5 → 3 → 2 → 1 → 0.5 sim-years across the 6,000-year run.
3. **The explicit-Euler / slot-advance cohort integration** (T2.1, D-026, ADR-010 —
   accepted, pinned by the exactness hand-walks and dt-halving tests): per-turn flows
   are `rate × PREV count × dt`, first-order accurate.

The M2 exit criteria that hang on all three simultaneously: Malthus boom–crash cycles
persisting at the new tempo (T2.7 acceptance), and T2.8's calibration corridors
(long-run growth, boom-crash presence, density) measured from canonical autoplay.

## 2. Evidence

Measured in a **fully fed** rig (yield unconstrained — pure vital rates), canonical
data, dev world, seed 42, this branch:

| era band | dt | CBR /1000·yr | CDR /1000·yr | net growth |
|---|---|---|---|---|
| Neolithic | 10 | 37.2 | 36.5 | **+0.7/1000** (in band) |
| Bronze/Iron | 5 | 35.0 | 38.4 | **−3.4/1000** (decline) |

Identical data, identical mechanism — the only change is dt. The discretization swing
(≈ −5.6/1000: Euler mortality kills more per coarse step; fertile exposure composes
differently under 2-slot vs 1-slot advance) is ~8× larger than the tuned signal
(+0.7/1000). Consequences, measured on canonical autoplay:

- The canonical world grows to ~7,300 by turn 250, crosses into Bronze (dt 5), then
  **declines monotonically to extinction by turn ~1250 with zero starvation ever**.
- No Malthus overshoot occurs at any point (capacity-touch and the dt flip coincide);
  `MalthusLite_OvershootCorrectionCycles` measures 1 down-crossing, 0 up.
- Failing tests left in place as this CR's evidence (deliberately NOT reworked):
  `MalthusLite_OvershootCorrectionCycles_MeasurableIn1000Turns`,
  `Reconciliation_FromLedgerAlone` (vacuity guard: starvation never occurs),
  `Artisans_EmergeInFedAutoplay` (post-boom drain arm: the crash it waits for is gone).

No tuning escape exists: keeping growth ≥ 0 at dt 5 forces ≈ +0.56 %/yr at dt 10 —
6–11× outside the ratified band. **Why this surfaced only now:** the old migration
ping-pong let populations dodge Prev-sized death sinks (shuttling people are never
where the death flow was computed; ClampToAvailable silently under-kills), inflating
effective growth by roughly +10/1000 and masking the dt gap. The mandated T2.8
stabilization removed that artifact; the honest demography underneath is dt-fragile.
(The dt-halving pins never caught this because they assert first-order CONVERGENCE —
O(dt) error is exactly what they permit; the tuned signal is smaller than that error.)

## 3. Minimal fix options (≤ 3)

**(a) Exact-exponential integration for cohort sinks** — per-turn death fraction
`1 − exp(−m·dt)` (and famine-suppression/decay factors likewise); births exposed at
midpoint-surviving counts. Survival then composes exactly across dt
(`e^{−10m} = (e^{−5m})²`): the tempo becomes dt-invariant by construction and one
tuning holds across the whole arc.
*Blast radius:* DemographicsSystem's sink/birth equations; the exactness hand-walks
re-derive (mechanical); the dt-halving mortality test inverts into a dt-INVARIANCE
pin (stronger); full T2.7 re-tune of the rate arrays (CBR/CDR shift several points);
all goldens. Kernel untouched; schema untouched.

**(b) Per-era rate tables** — fertility/mortality arrays per era band in sim.json,
each tuned to the band at its own dt.
*Blast radius:* data + loader only — but it hard-codes rates against era labels
(law 4, "no calendar gates," in spirit: the data would encode the integrator's dt
error, not history), and every future rate retune multiplies by six bands.

**(c) Re-scope the M2 dynamical claims to the Neolithic band** — corridors and
Malthus criteria measured over turns ≤ 250 (dt 10) only; accept post-Neolithic
decline as a known defect deferred to a later milestone.
*Blast radius:* test windows and battery definitions only — but the canonical world
still dies by year +2250, every future milestone inherits it, and the 6,000-year
premise is silently forfeit.

## 4. Recommendation

**(a).** The Euler dt-sensitivity is an integration artifact, not a mechanism —
mechanisms-over-modifiers (law 2) says fix the integrator, not paper over it with
per-era data (b) or scoped-down claims (c). The re-tune cost is bounded (T2.7's
measurement harness and acceptance tests exist and re-run); the exactness tests
re-derive against the same closed forms they already mirror. If ruled (a), the
natural sequencing is a directed packet (T2.7b) before T2.8's battery lands its
growth/boom-crash corridors.

## 5. What proceeded despite the STOP

The mandated migration stabilization (commit 1) is complete and independent of this
conflict: gap-closing flow cap + EMA smoothing, bifurcation regression tests,
occupancy-concentration bound, corridor re-anchored on the stabilized response curve,
all T2.5 teeth re-verified, goldens re-pinned. T2.8's autoplay runner and calibration
battery are NOT started — their growth/boom-crash/density corridors are exactly what
this ruling decides.
