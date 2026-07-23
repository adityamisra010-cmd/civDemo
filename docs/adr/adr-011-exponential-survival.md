# ADR-011 — Exponential survival integration for cohort demography

**Status: accepted** (director ruling on CR-001, option (a); T2.8 session, T2.7b work).

## Context

CR-001 demonstrated that explicit-Euler cohort flows (`rate × PREV count × dt`) make
the demographic tempo dt-fragile: the discretization swing across the era-pacing arc
(≈ 5.6/1000·yr between dt 10 and dt 5) is ~8× the ratified growth signal
(+0.7/1000·yr), so no single tuning can hold the [0.05, 0.1] %/yr band across eras.
Separately, Prev-sized ABSOLUTE death flows created a dodge class: people moved by an
earlier system in the same turn were never where their death flow was computed, and
ClampToAvailable silently under-killed (the migration ping-pong exploited this at
scale, inflating growth ~+10/1000·yr).

## Decision — the mechanism

1. **Mortality is a per-capita survival fraction on PRESENT counts.** A bucket with
   rate m keeps `exp(−m·dt)` of the people PRESENT when demographics runs:
   `deaths = present × (1 − exp(−m·dt))`, integer flows through the existing D-004
   remainder accumulators. Survival composes exactly across dt
   (`e^{−m·10} = (e^{−m·5})²`), so the tempo is dt-invariant by construction.
   Because the fraction applies to present counts, mortality is
   **position-independent**: being moved this turn changes WHERE you die, never
   WHETHER — the dodge class is killed by the integrator itself, not by a patch.
2. **Sequential sinks compose exactly.** Base deaths then starvation, each its own
   exponential fraction with its own ledger reason and remainder; the product is
   `e^{−(m+s)·dt}` regardless of order (exponentials commute) — the order
   (deaths first, starvation second) is pinned for flooring determinism only.
3. **Fertility integrates person-years.** Births per group =
   Σ_c fertility[c] × PY_c, with `PY_c = present_c × (1 − e^{−λ·dt}) / λ`
   (λ = the cohort's total sink rate m + s; λ → 0 limit: present × dt) — the
   standard nLx-style exposure: people who die mid-step bear children for the
   fraction of the step they lived. Famine suppression multiplies fertility BEFORE
   integration; the rebound reservoir banks suppressed EXACT births as before.
4. **Newborns face in-step mortality.** Births are spread uniformly over the step,
   so of B exact births credited to cohort j, `B × (1 − e^{−m_j·dt}) / (m_j·dt)`
   survive to the step boundary; the shortfall flows as Deaths (sourced then sunk —
   ledger-honest infant deaths). Without this factor, longer steps let newborns skip
   more infant exposure and the dt-invariance breaks by ≈ 4/1000·yr (measured).
5. **Aging: ADR-010 slot-advance stands**, sized from POST-SINK present counts (the
   people actually present age); descending-cohort processing is cascade-free
   (arrivals land only on already-processed higher cohorts). The pinned within-step
   composition order is **birth-exacts computed from pre-sink presents → base
   deaths → starvation → aging → newborn credit (source + in-step infant-death
   sink)** per settlement, in table order — newborns credit AFTER aging because
   NewbornShares already places them at their end-of-step age. The remaining
   compositional error (aging is a step-boundary jump while deaths are continuous;
   newborn in-step survival uses the credited cohort's rate) is the honest
   first-order residue — MEASURED by the dt-invariance and era-boundary continuity
   tests, not hidden.

## The law-3 reading

Law 3 mandates per-sim-year rates integrated with dtYears. Explicit Euler was an
IMPLEMENTATION of that law, not the law itself — exactly as ADR-010 ruled for aging
(where linear-rate aging was replaced by slot-advance because Euler was outside its
stable range at dt 10). Exponential survival is the same move for mortality: the
closed-form integral of the constant-rate ODE the data declares. dt-halving tests
that pinned first-order Euler CONVERGENCE are superseded by strictly stronger
dt-INVARIANCE pins.

## Consequences

- Fertility/mortality data arrays keep their per-sim-year semantics; the T2.7 bands
  re-tune once on honest dynamics and then hold across the whole era arc.
- The exactness hand-walk tests re-derive against the closed forms above (mechanical).
- Reading PRESENT counts is an integration concern, not a signal: cross-system
  SIGNALS (deficits, vitals, distances) remain Prev-read per §3.2. Sinks acting on
  present stocks is the established Ledger pattern (Consumption debits present
  FoodStores); demographics now follows it for its own stock.
- A reintroduced Prev-sized-absolute-flow regression is caught semantically by the
  position-independence test (move people mid-turn; mortality must follow them).
