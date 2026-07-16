# ADR-002 — SimClock stores time as integer days

**Status:** accepted (director-sanctioned, recorded at T0.4 session setup)
**Context packet:** T0.4 (clock + era table)

## Decision

`SimClock` stores time as `long SimDays` since the campaign epoch (**4000 BCE =
day 0**), with a fixed sim calendar of **`YEAR_DAYS = 360`** days per year. The
current turn length is likewise integer days (`long DtDays`). `dtYears` — the
universal rate basis of law 3 — is a *derived* `double` (`DtDays / 360.0`), as is
`WorldDateYears`; neither is stored.

## Rationale

- **Dates are position-state.** The D-004 philosophy: quantities whose exact
  equality matters are integers. A date is compared, banded, and snapshotted —
  accumulating it in floating point invites drift.
- **Drift-proof calendar.** 6,000 years of `+= 0.5` in doubles is representable but
  fragile under refactor; integer days make every date reachable exactly, forever.
- **Exact band boundaries.** Era-band edges land on exact day values, so "which
  band contains this date" has one answer — no epsilon at 1500 BCE.
- **Integer-only clock state for snapshots.** The T0.7 canonical serialization of
  the clock is three longs; nothing about time depends on double bit patterns.

## Consequences

- The era-pacing table (D-006) validates in day units: each band's dt must convert
  to a whole number of days, and each band's span must be an exact multiple of its
  dt — a campaign tick-through therefore hits every band edge exactly.
- Rate math is unchanged: systems integrate with the derived `double dtYears`
  (law 3); only the clock's *stored* state is integer.
- Calendar display (BCE/CE labels, month/day fictions) is presentation-layer only;
  the sim never parses or formats dates.
