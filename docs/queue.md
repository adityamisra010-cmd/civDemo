- Harden Prev against downcast escape (ctx.Prev as WorldState): wrap Prev in a read-only facade object instead of an interface view of the live state. Compile-time-only guarantee is the ratified T0.2 design; revisit at M10 slice gate. (raised T0.5 verification)
- Dropped-local Conserved escape: Ledger.Transfer into a non-world stock local removes value from audited totals (used deliberately by the auditor teeth test). Consider requiring stocks to be world-table-resident. Revisit at M10 slice gate. (raised T0.6, ADR-004)
- Terrain detail-on-zoom: resample hash-noise fBm at view resolution (pure coordinate function → infinite zoom achievable); UI polish track. (raised T1.7 visual gate)
- River polyline corner smoothing (Chaikin subdivision, render-path only — sim polylines untouched); UI polish track. (raised T1.7 visual gate)
- True river breadth: derive render width from actual discharge/accumulation instead of TUNE rank falloff (render-path only); UI polish track. (noted at T1.8 setup — the T1.7 rework used rank-scaled TUNE widths, no breadth line existed yet)
- Bind founding parameters INTO the order-log header (seed + world size recorded at save; replay refuses a mismatch with an actionable error) — the hard version of the T1.9 filename convention (orders-*-sPX.bin). Requires an OrderLog IoVersion bump + ADR. (raised T1.9 adversarial pass)
- Buckets group/cohort lookups are linear table scans (FindInGroup, BandViews). Re-benched at T2.3 as planned: N=12 1024² founded, classmobility 105.9 ms + demographics 97.3 ms per 200 turns (~0.5 ms/turn each) — visible but trivially within budget, no index added. Revisit when T2.8's autoplay batches multiply turn counts. (raised T2.1; re-benched T2.3)
- Director: world feels small at 12 settlements — revisit D-015 size / settlement count against T2.8 density-corridor results. (raised T2.4 visual gate) RESOLUTION-SO-FAR (director ruling, T2.9 session): the T2.8 density verdict stands — density is historically correct (0.30–0.36 people per fertility-weighted arable km² at year 4500); the "small map" perception is TRAVEL-SCALE (settlement count / spacing / travel budget), all TUNE data. Deferred to the M10 slice gate unless the M2 exit session makes it acute.
  **SUPERSEDED AND REFRAMED AT T3.2b (CR-002).** The T2.8 density verdict this rests
  on was measuring a denomination bug: "arable km²" was fertility-weighted lattice
  NODES scaled by the block area in one consumer and not in the other, and the
  205 km catchment radius that made the world *look* full was compensating for a
  yield constant denominated 256× too coarse. Both are fixed; the honest picture
  is the opposite of "small". At a 50 km economic hinterland the twelve
  settlements claim ~2 % of the continent, so the world is now overwhelmingly
  EMPTY — and that is the item. Reframed as an EXPANSION OPPORTUNITY rather than
  a sizing complaint: there is a large, fertile, reachable, unclaimed hinterland
  and no mechanism by which a growing population can take it. A settlement at its
  land-bound ceiling can only starve back or migrate to another full settlement;
  it cannot throw off a daughter settlement, and PathBuild can only extend the
  reach of an existing centre. Candidate mechanisms (NOT designed here, no packet
  implied): daughter-settlement founding driven by sustained land pressure plus a
  reachable unclaimed frontier; or a colonization destination in the migration
  choice set. Whichever lands, it wants the SAME land-pressure signal the Malthus
  corridor already reads, so it belongs with the M10 slice gate or a dedicated
  post-M3 packet, not bolted onto a tuning pass.
- **COLONIZATION / LAND CLEARANCE — M4-TARGETED (director ruling, CR-003 §5.2(a)).** The
  mechanism whose absence CR-003 exposed: how population converts empty land into settled, worked
  land, and therefore how the frontier eventually closes and Malthusian pressure legitimately
  emerges. Origin: cr-003 — the corrected constants leave a pre-Malthusian world because nothing
  fills the frontier, and the Malthus corridors stay quarantined until this exists. A large
  system (founding rules, site selection, clearing cost, sprawl constraints); at home in M4
  alongside expansion and borders. Design bound by D-037 (`docs/d037-emergent-polities.md`)
  Part B1 — colonization from below, migration extended to depart into UNCLAIMED land, refugee
  foundings may be stateless. See also the expansion-opportunity reframing of the "world feels
  small" entry above (same finding, ruled correct at CR-003 §6(d)).
- Tool-wear dt-sensitivity (raised T3.3 adversarial pass, REFUTED as a defect but
  real as a residue). Farm-tool wear is `rate x equipped-farmers x dtYears` — a
  per-sim-year rate integrated with dtYears exactly as law 3 prescribes — but in
  the stock-limited branch that is a first-order step on a decay whose own state
  is the integrand, so cumulative wear is dt-sensitive: over 20 sim-years from a
  1000-tool endowment, dt=10 leaves 0 and dt=2.5 leaves 100, and grain differs
  ~1.3%. A reviewer proposed exp(); that was refuted because exp() embeds a
  MEMORYLESS lifetime, contradicting the TUNE doc's declared finite "a tool set
  lasts ~10 working years" and leaving an immortal tail. Choosing correctly needs
  tool VINTAGE state, which m3-spec section 2 puts out of milestone scope
  ("vintaged capital"). Bounded and pinned meanwhile by
  ProductionTests.ToolWear_DtSensitivity_IsBoundedAndByDesign_NotAccidental: the
  decay coefficient is <= 1 at the coarsest era band (so the step never
  overshoots), wear is monotone in dt, and the sensitivity is asserted to still
  exist so the test cannot go vacuous. REVISIT when vintaged capital lands.
- Lattice stride floor (raised T3.2b): the traversal lattice samples terrain at
  stride 4, so one node is 16 × 16 km and one cost unit buys 16 km on ideal
  ground. That is the RESOLUTION FLOOR on every spatial quantity derived from it.
  `catchment.hinterlandRadiusKm` = 50 is ~3 nodes, which is coarse but not
  degenerate; anything below ~32 km would be under two nodes and the isochrone
  would collapse to a handful of blocks, so the instrument simply cannot
  represent a village working radius (the classic 5 km site catchment is 0.3 of
  one node). The same floor bounds how finely settlements can be spaced before
  their catchments alias into each other. If a later milestone wants either
  village-scale catchments or dense settlement spacing, the stride — not the
  radius — is the thing that has to move, and that is a worldgen/pathfinding cost
  question (stride 2 quadruples the node count and the Dijkstra work) plus a
  golden re-pin, not a tuning change.
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
- Founding variation: D-025 equal-split endowment is provisional; consider seeded
  variance in founding population/food/composition AND in SITING (the coastal
  clustering the director observed is the same item — seeded siting jitter, not
  just endowment jitter) so settlements diverge from turn 0. Director
  observations (T2.9+T2.10 visual gate + M2 exit session): uniform founding
  produces lockstep history — 11 of 12 settlements got artisans in the same
  decade with near-identical counts — and the siting pass clusters settlements
  coastally. Confirmed for the record at the T2.13 session: BOTH observations
  are this one queue item. Candidate for M3 (with goods) or the M10 gate.
- Map symbology art-direction (director ruling, T2.12 session): DEFERRED to
  after M4/M5 — settlement icon tiers, production/trade-route visual language,
  political vs catchment border treatment, army/conflict markers, unrest
  presence on the map, legend + map-mode system. Reasoning: symbology encodes
  what the map CONTAINS, and M3 (goods/markets), M4 (neighbors/armies), M5
  (unrest) each change that content; redrawing piecemeal in the interim is
  wasted work — placeholder symbols are CORRECT until the map's content is
  settled. The art SUBSTRATE (terrain textures, grain overlay, palette, UI
  frames, typography, style bible) has no such dependency and ships separately
  at M2+. Target: after M4 or M5, against the Troy/Humankind stylized
  reference.
- ADR-012 viability forward-note (T2.13 adversarial pass): the absolute food
  gate is one-way today (a store cannot rise without a harvest, so a dead ruin
  stays dead). If a future milestone adds food SHIPMENTS into settlements, a
  trickle into a ruin re-arms viability — revisit the gate when goods movement
  (M3) lands.
- **BINDING for T3.10** — MalthusLite test power was WEAKENED at T3.1 (was: the
  population trajectory must cross its long-run mean from above AND below ≥2
  times each; now ≥1 each). Cause: the worldgen refresh raised carrying
  capacity world-wide, so one overshoot–correction arc now spans most of the
  1000-turn campaign (first crash moved ~t255 → ~t820). This is a REAL LOSS OF
  POWER: one crossing pair cannot distinguish an oscillating system from a
  single overshoot settling into equilibrium — exactly the distinction the
  Malthus corridor exists to make. T3.10 MUST restore it, by either a longer
  horizon that contains ≥2 full cycles, or a rigged higher-pressure config
  (lower yield / higher fertility / smaller catchment) that produces multiple
  cycles inside a practical horizon. Do not close T3.10 with the ≥1 bar standing.
- Extent-of-market: the artisan population threshold (T3.1, `population > 520`
  in the D-020 emergence predicate) is a LOCAL-DEMAND proxy for market extent.
  Once trade exists (T3.6), generalize it to trade-connected demand — a
  settlement joined by cheap transport to neighbours has a larger effective
  market than its own population implies, which is the actual Smithian claim.
- Under-utilisation is invisible to conservation tests. `Crafting_ThreeRecipesShareOneScarceInput`
  passed under an implementation that stranded 15 of 60 timber (ADR-015 §4): the books closed on
  what was consumed, and consuming too little is not a conservation break. Whenever a scarce
  input is expected to be fully drawn down, pin the DRAWDOWN as well as the balance. Candidates
  to audit for the same blind spot: consumption clamping, migration overdraw, PathBuild banking.
- Intra-turn recipe-chain ordering (bronze-casting before toolmaking) is load-bearing behaviour
  that lives only in the goods.json array order and was declared nowhere until ADR-015. T3.4
  should state it as a contract when prices start weighting recipes, since a price-driven
  ordering could silently break the chain the same way proportional rationing did.
- T3.4 residue, price solver: PriceSystem is O(S^2 * G^2) per turn — FindPrice and the
  GoodStocks scan are both linear scans nested inside the settlement x good loop. 56 rows today,
  but ~7.8M row-visits per turn at 200 settlements. Not a law violation and out of T3.4 scope;
  revisit when settlement counts grow (raised by the T3.4 no-global-solve lens, not raised as a
  finding).
- [CLOSED by ADR-016 — D-033 amended to exact integration; spread 21% -> 5.8e-16]
  T3.4 residue, Euler under-integration of the price step: over 100 sim-years, dt 10/5/2.5/1
  give 7.439/8.225/8.694/9.006 — monotone, converging from below, 21% spread. The mandated
  D-033 form is Euler on a compounding process; exact integration (p *= exp(...)) would remove
  it and ADR-011 is precedent for making that change deliberately. Bounded by
  Price_DtSensitivity_IsEulerDiscretization_BoundedAndConverging, flagged for director ruling.
- T3.4 watch item: the shipped config has Lambda (0.04) > MaxRelativeChangePerYear (0.03), which
  means the per-step rail saturates before the market-scale floor's arithmetic can matter. A
  future retune that lowers Lambda below the rail would expose the floor-binding regime to
  direct scrutiny for the first time. Noted by the no-global-solve lens as a corner it chased
  and deliberately did not raise.
- [PROMOTED to docs/m4-blocking-material.md B-1 — the M4 spec cannot ship without answering it]
  M4 COLONIZATION / spacing collision (T3.4b finding): minSpacingKm 480 caps the dev continent at
  NINE settlements — `settlement siting could only place 9 of 12 sites`. Colonization means
  founding new settlements and there is nowhere to put the tenth. Either minSpacingKm becomes a
  colonization-aware founding rule rather than a worldgen-only one, or the continent grows, or
  expansion saturates at 9 and the Malthusian transition arrives by MAP EXHAUSTION rather than by
  land filling — which would be the trap hardwired by geometry, exactly what CR-003 forbids.
  Must be settled before or within the M4 colonization packet.
- D-035 was RULED but never FILED until 2026-07-27 (docs/d035-needs-aggregation.md). The four
  rulings — variety-as-satisfaction, non-compensatory CES aggregation, the seven legal coupling
  paths, taxation→Dignity — existed only in the director's notes, so the implementing agent
  correctly refused to proceed on them (ADR-016 §4 standing note). PROCESS ITEM: a ruling that
  changes a frozen D-decision must be filed at the time it is made, not at the time a packet
  needs it. Two packets (T3.5 by a session, T3.4b's evidence by a round-trip) were delayed by
  unfiled rulings; both refusals were correct and both cost a round-trip that filing would have
  saved.
