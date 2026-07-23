- Harden Prev against downcast escape (ctx.Prev as WorldState): wrap Prev in a read-only facade object instead of an interface view of the live state. Compile-time-only guarantee is the ratified T0.2 design; revisit at M10 slice gate. (raised T0.5 verification)
- Dropped-local Conserved escape: Ledger.Transfer into a non-world stock local removes value from audited totals (used deliberately by the auditor teeth test). Consider requiring stocks to be world-table-resident. Revisit at M10 slice gate. (raised T0.6, ADR-004)
- Terrain detail-on-zoom: resample hash-noise fBm at view resolution (pure coordinate function → infinite zoom achievable); UI polish track. (raised T1.7 visual gate)
- River polyline corner smoothing (Chaikin subdivision, render-path only — sim polylines untouched); UI polish track. (raised T1.7 visual gate)
- True river breadth: derive render width from actual discharge/accumulation instead of TUNE rank falloff (render-path only); UI polish track. (noted at T1.8 setup — the T1.7 rework used rank-scaled TUNE widths, no breadth line existed yet)
- Bind founding parameters INTO the order-log header (seed + world size recorded at save; replay refuses a mismatch with an actionable error) — the hard version of the T1.9 filename convention (orders-*-sPX.bin). Requires an OrderLog IoVersion bump + ADR. (raised T1.9 adversarial pass)
- Buckets group/cohort lookups are linear table scans (FindInGroup, BandViews). Re-benched at T2.3 as planned: N=12 1024² founded, classmobility 105.9 ms + demographics 97.3 ms per 200 turns (~0.5 ms/turn each) — visible but trivially within budget, no index added. Revisit when T2.8's autoplay batches multiply turn counts. (raised T2.1; re-benched T2.3)
- Director: world feels small at 12 settlements — revisit D-015 size / settlement count against T2.8 density-corridor results. (raised T2.4 visual gate)
- Post-crash migration ping-pong: an emptied settlement's per-capita attractiveness (capita floor 1) turns it into a magnet, and the dev world settles into a persistent two-turn population slosh (~95% of a settlement shuttling, mostly children) after the first Malthus crash — at CANONICAL rates. Base rates ≥ 2.2× bifurcate into this attractor even pre-crash (measured T2.7 response curve in MagnitudeCorridor test). Needs an attractiveness smoothing constant or migration hysteresis (D-021 revisit) before T2.8 density corridors lean on migration flows. (raised T2.7 retune)
- T2.8 adversarial pass (minor hardening candidates, no packet conflict):
  (1) infant in-step shortfall uses the combined base+starvation hazard but is
  attributed entirely to Deaths, never Starvation — chronicle semantics only,
  conservation unaffected; (2) rebound reservoir release is gated on
  unsuppressed > 0, so a group whose fertile cohorts all die strands its bank
  (not a conserved stock); (3) micro-step/reconciliation aging correctness
  relies on "higher cohort => higher row index within a group" — holds today
  (founding + snapshot ordering), but no invariant test pins it; (4)
  dt-invariance covers dt 10/5/2.5 directly, dt 3 only via era-boundary
  continuity.
