# ADR-010 — Slot-advance cohort aging (T2.1, D-026)

## Status
Accepted (T2.1). Touches the demographics resolution mechanism only; the
kernel contract is unchanged beyond the spec-mandated Buckets table (schema v7).

## Context
D-026 upgrades population to 16 five-year cohorts. The M1 aging mechanism was a
linear per-year rate (1/bandWidth) integrated as `rate × dtYears` — explicit
Euler. With five-year cohorts at the Neolithic era dt of **10 sim-years**,
`rate·dt = (1/5)·10 = 2.0`: outside Euler's stable range. The floor/clamp
machinery degrades this to "the whole cohort advances one slot per turn" —
people age 5 years per 10-year turn, i.e. at half speed, at the canonical dt
the director actually plays. That is not a small integration error; it is a
broken mechanism (law 3: dt-correctness).

## Decision
Aging integrates by **slot advance**: dt years of aging is `dt / width` cohort
slots = `k` whole slots + fraction `f`.

- `floor(f × prevCount + remainder)` people move `k+1` slots (per-row D-004
  remainder accumulator carries the fraction);
- when `k ≥ 1`, the rest of the cohort (sized from PREV) moves `k` slots;
- destinations clamp to the absorbing 75+ cohort; transfers are
  `Ledger.Transfer` under ClampToAvailable (the dead do not age), far move
  first, cohorts processed descending — all pinned;
- newborns are credited to the cohorts a dt-window of births actually spans
  (uniform ages 0..dt at turn end: dt = 10 → half into cohort 0, half into
  cohort 1), each credited cohort carrying its own BirthRemainder.

Properties: **exact** when dt is an integral multiple of the cohort width
(dt = 10, 5 — the entire pre-Medieval era table), and identical to the M1-style
linear rate when dt < width (dt = 3, 2, 1, 0.5). One dt = 10 step equals two
dt = 5 steps exactly (pinned by test). People age one year of age per sim-year
at every era dt.

## Consequences
- At dt = 10 the fractional regime never engages (f = 0); at dt < 5 aging is
  diffusive (a fraction advances each turn) — the standard first-order behavior
  the dt-halving characterization documents.
- At dt = 10 whole-cohort jumps produce parity striping in the raw 16-cohort
  histogram (a 10-year turn cannot resolve 5-year sub-structure). Band views
  and quartile aggregations are unaffected; the pyramid acceptance test
  aggregates accordingly. Finer-era pyramids resolve fully.
- Whole-model dt-halving comparisons across the dt = 5 boundary mix two
  integration regimes and do not display clean Euler convergence; the
  convergence test therefore measures the pure rate flows (deaths-only decay on
  the absorbing cohort), and slot-advance dt-correctness is pinned separately
  by exact-equality tests.
