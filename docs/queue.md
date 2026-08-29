- Harden Prev against downcast escape (ctx.Prev as WorldState): wrap Prev in a read-only facade object instead of an interface view of the live state. Compile-time-only guarantee is the ratified T0.2 design; revisit at M10 slice gate. (raised T0.5 verification)
- Dropped-local Conserved escape: Ledger.Transfer into a non-world stock local removes value from audited totals (used deliberately by the auditor teeth test). Consider requiring stocks to be world-table-resident. Revisit at M10 slice gate. (raised T0.6, ADR-004)
- Terrain detail-on-zoom: resample hash-noise fBm at view resolution (pure coordinate function → infinite zoom achievable); UI polish track. (raised T1.7 visual gate)
- River polyline corner smoothing (Chaikin subdivision, render-path only — sim polylines untouched); UI polish track. (raised T1.7 visual gate)
- True river breadth: derive render width from actual discharge/accumulation instead of TUNE rank falloff (render-path only); UI polish track. (noted at T1.8 setup — the T1.7 rework used rank-scaled TUNE widths, no breadth line existed yet) UNAFFECTED by the art-gate D-A3 fix (docs/art-gate-defects.md): D-A3 changed the zoom RESPONSE of the width, not where the width comes from; this item stays open.
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
  **T3.6 CONSEQUENCE (director ruling at certification): this is no longer a variety
  complaint — it is a BLOCKER on observable inter-settlement exchange, and therefore on every
  mechanism downstream of trade volume.** The T3.6 R1 measurement (docs/t3.6-review-record.md)
  found ZERO units traded of every good in 100 driven decades because the uniform mix leaves
  settlements with no comparative advantage: every abundant good's price gap sits inside its
  transport deadband, and the only gaps that open (bronze, at the band ceiling) have no
  inventory behind them. Founding uniformity is the named structural cause of D-034's silence,
  and T3.7 merchants predicate on the trade volume it suppresses. No owner assigned — that is
  the director's next ruling; the fix is not designed here.
- Map symbology art-direction (director ruling, T2.12 session): DEFERRED to
  after M4/M5 — settlement icon tiers, production/trade-route visual language,
  political vs catchment border treatment, army/conflict markers, unrest
  presence on the map, legend + map-mode system. Reasoning: symbology encodes
  what the map CONTAINS, and M3 (goods/markets), M4 (neighbors/armies), M5
  (unrest) each change that content; redrawing piecemeal in the interim is
  wasted work — placeholder symbols are CORRECT until the map's content is
  settled. The art SUBSTRATE (terrain textures, fibre overlay [renamed from
  "grain overlay" by CONV-1 — `grain` is namespaced to the SIM domain], palette, UI
  frames, typography, style bible) has no such dependency and ships separately
  at M2+. Target: after M4 or M5, against the Troy/Humankind stylized
  reference. [D-038 (docs/d038-visual-target.md) fixes the milestone: an
  inserted visual milestone after M5, before M6, absorbing this packet.]
  **D-038 PART H (composed settlement sprites) further scopes the settlement-icon-tier half of
  this entry:** settlement icons are RULED to be composed sprites assembled at draw time from
  per-institution parts, not a glyph ring around a marker; glyphs are demoted to what is
  HAPPENING to a settlement (trade status, unrest, production emphasis) rather than what it IS.
  Part H also carries the constraint that binds any part authored for it — one light angle, one
  ground plane, stricter than anywhere else in the visual layer because parts sit adjacent in a
  single image — and one open legibility question (how many parts before the sprite stops
  reading). Same milestone, same deferral reasoning; Part H does not schedule work.
- D-039 (docs/d039-command-fog-and-siege.md) — CROSS-REFERENCE POINTER ONLY.
  Director ruling closing D-014 (command friction), and extending D-011/D-012/D-013
  with reconnaissance, the stale-ghost display, siege, and a new campaign layer.
  Binds M6 (Parts A, C, D, E); Part B's reconnaissance-investment mechanism touches
  M4, which owns armies. D5 (siege starvation) is hard-blocked on M4's B-2 store
  bounding. Nothing scheduled here — the pointer exists so the queue records it.
- ADR-012 viability forward-note (T2.13 adversarial pass): the absolute food
  gate is one-way today (a store cannot rise without a harvest, so a dead ruin
  stays dead). If a future milestone adds food SHIPMENTS into settlements, a
  trickle into a ruin re-arms viability — revisit the gate when goods movement
  (M3) lands.
- ~~Worldgen: continental mask permits land at the world boundary~~ — **CLOSED by T3.1(b)**
  (edge taper). Was: measured 185 land cells on seed 42's eastern boundary column, 87 on
  the northern row; the render was verified complete, so it was a worldgen property, not
  a Sim.Ui defect. The smoothstep taper now drives boundary elevation to exactly 0 and a
  10-seed test pins that no land touches any world edge.
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
- **libm on the hashed determinism surface (T3.4b lens 1, carried not blocking):** `Math.Cos` and
  `Math.Log` enter sim code for the first time in `HarvestWeatherSystem`'s Box–Muller. Same-machine
  reproducibility is verified unaffected, but libm is not bit-guaranteed across glibc versions and
  nothing in the kernel spec or the banned-constructs gate addresses it. Bears on CI-vs-container
  golden pins, not on correctness. Raise if a golden ever diverges between environments.
  **Director note (2026-07-27):** this is the FIRST TIME A PLATFORM MATH LIBRARY SITS INSIDE
  DETERMINISM. Everything determinism rested on until now was integer PCG32 and IEEE-754 arithmetic,
  both bit-specified. libm is not: `cos`/`log`/`exp` are implementation-defined to within an ulp and
  may differ across glibc versions and CPU dispatch paths. **The property at risk is CROSS-PROCESS
  BYTE-IDENTITY** — the guarantee the whole replay/golden/CI apparatus is built on, and the one M0
  spent a milestone establishing. A 1-ulp difference survives rounding until it lands near a .5
  boundary, then diverges permanently and silently.
- **Migration test pinned to a realisation, not a property (T3.4b lens 6):** swapping `cos` for `sin`
  in Box–Muller is distribution-preserving — both are standard normal on the same uniform phase —
  yet `MigrationTests.FamineAtOneOfTwelve_…` fails its non-vacuity guard. ADR-015 §7.8 family.
- **OPEN — owner T3.4d, re-homed from T3.10 (directed packet T3.4d, 2026-07-29) — (corridor &
  measurement teeth): the √(1−ρ²) stationary-variance factor is
  unpinned at every dt (T3.4c review, test-power M6-golden-only).** The factor exists, per its own
  comment, to hold the stationary variance at σ² for every dt; at the shipped Neolithic dt = 10,
  ρ = 0.036 and the factor is 0.99935 — inert — while at dt = 1 its deletion inflates σ 1.43×. The
  one dt = 1 rig measures autocorrelation, which is invariant to innovation scale. No test measures
  realised variance at TWO different dt, so the factor's entire stated purpose has no semantic
  test. NOT cleared — a real coverage gap, not a stated T3.4c acceptance criterion.
- **OPEN — owner T3.4d, re-homed from T3.10 (directed packet T3.4d, 2026-07-29): the
  spatial-correlation test does not test its own name (T3.4c review,
  test-power F5).** `SpatialCorrelation_NeighboursShareWeather_AndDistantSettlementsDoNot` computes
  one GLOBAL mean pairwise correlation and asserts it in (0.05, 0.95); it never compares near
  against far. Mutants M5 (spatialSharedFraction made dead by forcing k = 1) and M8 (the distance
  kernel made constant, spatialRangeCostUnits dead) both land inside the band and pass — two
  shipped TUNE parameters have no semantic test. The asymmetry is the tell: pure-local dies,
  pure-regional does not. Requires a near-vs-far comparison at a rig with meaningful spacing
  spread. NOT cleared.
- **Q1 (T3.4c certification, director) — owner T4.1 (M4 FOUNDATIONS AUDIT): the canonical
  land-capacity / labour-capacity bind ratio was never measured.** The T3.4c land-capped rig used
  outputPerFarmerPerYear ×1e6 — a definitely-binds value, not a threshold — which served its
  criterion (M10/M9 discrimination) but tells nobody whether the real distance between the shipped
  world and a land-capped one is 3× or 1e5×. That distance is exactly what B-2 store bounding and
  M4 colonization aim to close. Measure the actual bind ratio per settlement on the canonical
  world in T4.1, which is the packet that will need the number. Not a defect; filed, not fixed.
- **Q2 (T3.4c certification, director) — the quarantine drift tooth's tolerance margins are
  ASYMMETRIC, recorded with the mitigation so it is not rediscovered as a surprise.** 0.75 leaves
  11% headroom on the must-pass side (largest legitimate correction ×0.836) against 40% on the
  must-fire side (disablement signature ×0.536). A legitimate substrate correction larger than
  ×0.836 would FALSE-FIRE the drift tooth. Mitigation, by construction: the per-seed pinned
  values make that loud rather than silent — the failure message names the seed, the recorded
  value and both signatures, and the correct response is a deliberate re-pin under a ruling, not
  a tolerance widen. (CalibrationBatteryTests.AssertDevMigrationQuarantine.)

## T3.5 review notes (2026-07-27)

- **M5 tuning, grievance monotonicity:** `needs.aggregation.tierACollapse = 1.0` makes grievance
  non-monotone in food when Comfort ≈ 0 and Shelter ≈ 1 — a hungrier settlement is measurably
  calmer, by ~3.4% over food fill 0.55→0.34. Verified as NOT a defect (no ruling requires
  monotonicity; d018:46 mandates the collapse) and NOT reachable in the founded world (zero
  settlement-turns in the region). `tierACollapse ≤ 0.25` removes it entirely if M5 wants
  monotonicity once grievance actually drives behaviour. TUNE only.
- **Aggregation strength is thinly pinned.** ρ's *sign* is well covered, but halving ρ's magnitude
  in both `Aggregate` and `CeilingWhenOneNeedIsZero` survives every test except `sum > ces*4.0`,
  at ~7% margin. The harmonic-mean and power-mean oracles added at T3.5 cover σ = 0.5; a
  σ-general strength pin would close the rest.
- **T3.5b golden churn, flagged ahead:** items 1 (subsistence default) and 3 (ghost-class
  grievance) both move founded-world trajectories, and item 1 moves the food economy itself.
  Expect the founded golden, the first-reign golden, the calibration battery anchors and the
  equilibrium-density invariant all to need re-measurement in that packet. Flagged now so the
  re-pin is planned rather than discovered.
- **Q2 (T3.5b certification, director) — an UNDECLARED test-power weakening, itemized here as
  ruled.** `HudViewModelTests`' Sustenance loop pinned `Assert.Equal("Sustenance: 0.85")` EXACTLY,
  every turn. T3.5b re-anchored it to: exact at turn 1, floor `value >= 0.85` for turns 2+. WHY the
  trade was necessary: the derived herding share diversifies the obtained diet from turn 2, so the
  displayed value legitimately varies by turn and no single literal is correct. WHAT IT CAN NO
  LONGER DISTINGUISH: on turns 2+, a regression that INFLATES displayed Sustenance (any value
  ≥ 0.85, including a hardcoded 1.0 from turn 2 on) passes the floor; only turn 1 retains exact
  power against fabricated defaults. Same class as T3.1's MalthusLite weakening (now a binding
  T3.10 obligation because nobody itemized it at the time) — itemized at the time, this time.
  Restoring per-turn exactness needs turn-indexed expected values or a recomputed oracle; owner
  whoever next touches the HUD pins.
- **Q3 (T3.5b certification, director) — WRITTEN as ADR-015 §7.15 (GOV-1, 2026-07-30):
  a pre-committed READING requires a DISCRIMINATING OBSERVABLE.** Root cause of T3.5b's misapplied
  reading (lens 2 F2): density = population/arable is composite, so "the food economy moves" could
  fire for a pure denominator reason while the mechanism under test did nothing. §7.13 requires
  pre-committing the readings; it does not require verifying the observable can DISCRIMINATE
  between them. Second time this shape has bitten — §7.7 was the first (a corridor insensitive to
  its own control parameter). NOT written into ADR-015 in T3.5b, deliberately: all four lenses had
  cleared, and a post-clearance governance addition is the f8a19e1 pattern, now recorded twice.
  ADR-015 §7.15 carries it with a worked example from both instances.
- **Q5 (T3.5b certification, director) — data-file edit convention:** edits to `needs.json` and
  `goods.json` stay MINIMAL-DIFF — no wholesale re-serialization — so scope review reads the
  change rather than the reformatting. T3.5b's 143-insertion rewrite of needs.json was accepted
  after mechanical semantic-equality proof; the proof should not have been necessary.
- **SAMPLER STATE IS IMPLICIT AND ORDER-DEPENDENT (D-A1 close-out, director ruling).** The header
  rule inherited LinearClamp from the settlement-marker batch and failed to tile; the parchment
  panel background, which also relies on uv > 1, was confirmed by the director's gate to have been
  unaffected throughout. Nothing at the call site distinguishes them — the header rule was simply
  the first element to need WRAP after the marker batch. Any future tiling element drawn in that
  position inherits the same defect, and it costs a visual-gate round each time, because no
  headless test sees sampler state; this round's composite repeat test catches it for the header
  rule only. Candidate fix: make sampler state explicit per draw rather than inherited. OPEN,
  owner: UI polish track. Not fixed here.
- **T3.6 → M4 BLOCKING (B-2 escalation, spec R2's own path):** price-driven trade CANNOT
  redistribute grain at all — the numeraire's price is pinned at 1.0 everywhere (D-033), so its
  pairwise gap is structurally zero, while B-2's ~1,240-year granaries sit untouched behind that
  pin. Any famine-relief redistribution needs a QUANTITY-driven mechanism (deficit-driven relief,
  merchants, or a granary policy), not this price-driven one. Measured at T3.6
  (docs/t3.6-review-record.md, R2); ALSO measured: under sustained maximum drive the mechanism
  drains a seller's stock to zero — the unbounded-granary interaction is live in both directions.
- **T3.6 observation (not a defect): uniform sector mixes produce no arbitrage.** In the driven
  founded world every settlement running the same 40/35/25 mix, no abundant good's price gap ever
  cleared its transport deadband in 100 decades (97 crossings, all bronze at the band ceiling with
  zero inventory). Trade volume awaits real comparative advantage — deposit-differentiated
  production orders, or T3.7 merchants. The T3.11 driven golden should drive settlements
  ASYMMETRICALLY if it wants nonzero flow on the golden horizon.
- **PATTERN (director, T3.6 certification check — fourth instance): summary prose disagreeing
  with its own measured record.** 28-vs-29 commits (T3.4c handoff), 12-vs-13 mutants and
  11-vs-10 kills (T3.5b), 1-vs-2 flips (T3.6 handback: the measurement written into the slot
  labelled "margin"). Every instance: prose written FROM RECOLLECTION alongside a document whose
  numbers were measured; every one caught downstream by the director, none by the author. The
  cheap discipline, now standing: summary prose is TRANSCRIBED from the record — open the
  record, copy the number, never re-derive or recall it — and any figure appearing in both
  places is diffed before handback.
- **TOOL DEFECT (T3.6 ruling, second occurrence): the CCR stop hook's remediation text destroyed
  commit-order evidence.** The hook's check reads committer email + signature only, but its advice
  prescribed `--amend --reset-author`, which also resets AUTHOR dates — the temporal record every
  derivation-honesty lens reads. Chosen fix: make the hook preserve author dates — the advice now
  prescribes plain `--amend --no-edit` after the git-config fix (satisfies the hook's own check;
  author identity and dates untouched), so no archive-ref rescue is needed from T3.7 on. The live
  hook is patched and `scripts/bootstrap.sh` re-applies the patch every session, because the hook
  file lives in the ephemeral container home, provisioned by the CCR launcher OUTSIDE the repo.
  The durable at-source fix (launcher config) is outside agent remit — OWNER: director, via CCR
  launcher/hook configuration. Until then bootstrap carries it.
  **STATUS UPDATE (T3.6b certification): EVIDENCED, RECURRING — no longer a candidate.** The
  launcher re-provisioned the hook and wiped the patch; the fail-loudly post-condition fired
  exactly as the T3.6 ruling required (director's observation at session start). The loud stop
  now recurs EVERY session until the launcher config is fixed at source. Owner unchanged:
  director.
- **THE STOP-HOOK PATCH MUST FAIL LOUDLY (T3.6 certification ruling) — DONE, was a silent no-op.**
  As shipped at 5a16c2f the bootstrap patch silently no-opped on a non-matching hook (grep gated
  entry; a sed miss went undetected and `|| true` swallowed even hard failures) — exactly the
  worse-than-the-defect shape the ruling names. Now: sed runs without the swallow, and a
  post-condition grep verifies no `--reset-author` remains in the hook's text; if any does, the
  bootstrap EXITS 1 with an actionable message telling the operator to re-derive the patch before
  committing anything. A launcher-shipped hook change is therefore a loud session-start stop, not
  a silent loss of the next packet's commit-order evidence.
- **THE nearMean < 0.90 CEILING IS A STRUCTURALLY WEAK TOOTH (T3.4d certification ruling) — and
  it is the SOLE M5 tooth, measured.** Clean near-correlation 0.72; M5 drives it to 0.9948; the
  ceiling sits at 0.90 — a ~5% must-fire margin that CANNOT be widened, because correlation is
  bounded at 1.0 and any mutant driving it toward 1 is compressed against every ceiling below 1.
  Raising the ceiling loses the tooth; lowering it risks false-firing on an honest world (the
  companion floor is 0.30). The director asked whether the near−far MARGIN assertion also fails
  under M5, which would make the thin ceiling acceptable by redundancy: MEASURED, IT DOES NOT —
  under M5 the first failing assert is the ceiling (test line 333); the margin assert before it
  passed, because at k=1 the distance kernel still decorrelates far pairs (measured bound from
  the passing assert: far < 0.665 under M5). So the ceiling carries M5 ALONE. The clean-world
  near-correlation value that would leave the ceiling armed with no room is 0.90 itself: any
  retune raising clean near toward 0.90 silently disarms the M5 tooth while every test stays
  green (at ≥ 0.90 it false-fires instead). OPEN, owner T3.10; §7.15 and §7.16 are now
  WRITTEN (GOV-1) and this entry is an instance of both.
- **PATTERN (third instance): ASYMMETRIC-MARGIN THRESHOLDS.** T3.4c Q2 (drift envelope, 11% on
  the must-pass side), now the M5 ceiling (5% on the must-fire side) — thresholds separating a
  measured clean case from a measured mutant, where one side's margin is structurally thin. The
  standing line, WRITTEN as ADR-015 §7.16 (GOV-1, 2026-07-30): a discriminating threshold's
  WEAKER margin is stated at the point the threshold is chosen, not discovered later.
- **T3.6b ESCALATION 1 — THE TRANSPORT DEADBAND EXCEEDS THE PRICE BAND for bulk ≥ 8 at map
  distances.** Measured (docs/t3.6b-review-record.md, Item 0(c), 5 seeds): tin-ore price gaps
  span the ENTIRE band (19.95, floor to ceiling) and still reach only 0.57–0.86 of their
  deadband; threshold = bulk × pathCost × costPerBulkCostUnit ≈ 23–35 for bulk-8 goods at the
  closest pairs, vs a maximum possible gap of BandMax − BandMin = 19.95. Ores and stone are
  STRUCTURALLY untradeable overland at ANY price divergence. With T3.6 R1 this is the sharpened
  trade-silence finding. Every surface involved (price band, costPerBulkCostUnit, bulk table)
  is ruled/frozen — DIRECTOR MATERIAL, no owner assigned here.
  **COUNTEREXAMPLE, MEASURED (T3.11 Item 1 — filed here deliberately, beside escalation 1, for
  whoever scopes the M4 transport packet): THE DEADBAND IS NOT ALWAYS THE BINDING CONSTRAINT.**
  On the driven golden, `bronze` shows a price spread of **15.17 against a deadband of 7.22** —
  a gap comfortably OVER threshold, more than twice it — and moved **zero** units. The cause is
  not transport at all: `maxStock = 0`. Nobody holds any bronze to sell. Escalation 1 says the
  threshold is unreachable for high-bulk goods; this is the opposite case, a good whose gap
  clears its threshold easily and still cannot trade because the SELLER SIDE is empty.
  Consequence for scoping: lowering transport cost — water routes, draught animals (Q-C), any
  deadband lever — would do NOTHING for bronze. A transport packet measured only on total flow
  could be judged a failure, or a success, for reasons that have nothing to do with transport.
  Whatever instrument that packet uses must decompose gap / threshold / stock separately, as
  T3.11's D1 was pre-committed to do; volume alone would have read this as a trade-solver
  defect. Evidence: `docs/t3.11-review-record.md` §D1.
- **T3.6b ESCALATION 2 — COMMON-BAND-EDGE PINNING blocks gaps for 11 of 13 non-grain goods.**
  Both sides of every pair rest on the SAME band edge (floor: oversupplied-undemanded; ceiling:
  demanded-underproduced), so gap ≡ 0 however much settlements differ. The missing demand side
  (PriceSoak's recorded edge-resting) is now measured as the direct blocker of inter-settlement
  exchange. Founding variance cannot touch it; sector reallocation is M5's governing loop.
- **FOUNDING-VARIATION ITEM — MEASURED DISCHARGED AND PINNED (T3.6b).** The lockstep predicate
  no longer holds at HEAD (emergence spreads 58–85 decades at jitter 0.25; 89–118+ at the
  ADR-017 0.69; modal decade ≤ 2 of 12, five seeds) — T3.1c's jitters discharged it and it sat
  unmeasured for two milestones; the T3.6b variance-floor pin (CV ≥ 0.22, red-proven both
  regressions) prevents a silent return. Endowment variance now sits ON its reference band
  (realised founding-pop CV ≈ 0.30–0.47, RC-1 floor); siting stands per ADR-017. The
  blocked-exchange consequence recorded at T3.6 is NOT discharged — it is re-attributed by
  measurement to the two escalations above.
- **T3.9a GATE Q1 — END TURN NEEDS A KEYBOARD SHORTCUT (spacebar or Enter).** The director had
  to scroll to reach the button; it is the single most-repeated action in the game and currently
  costs a scroll. Owner: T3.9a-b (this packet).
- **T3.9a GATE Q2 — THE FIVE SECTOR BARS CLIP THEIR LABELS VERTICALLY.** Row height sits under
  the font's line height, so labels are cut off top and bottom. Owner: T3.9a-b (this packet).
- **T3.9a GATE Q3 — TEXT SIZE INCONSISTENT IN THE SETTLEMENT PANEL.** The "food ... (last
  harvest +N)" line renders at a different size from its neighbours; style bible §3 permits a
  companion face for dense numbers, but the switch must be deliberate and consistent, not
  per-line. Owner: T3.9a-b (this packet).
- **T3.9a GATE Q4 — PANELS NEED TO BE INDIVIDUALLY COLLAPSIBLE WITH SESSION-PERSISTENT STATE.**
  Market, Graphs, Annals and the settlement HUD open together clutter and overlap; the director
  specifically wants Annals closeable for routine play. ImGui already collapses on the title-bar
  arrow — what is missing is persistence and non-overlapping layout. Owner: T3.9a-b (this packet).
- **T3.9a GATE Q5 — COMFORT IS FLOW-BOUND, AND ITS MODEL IS NOT SHELTER'S.** Pots and cloth
  are durable; zero crafting for one turn should not zero Comfort. CONFIRMED FLOW-BOUND by
  T3.8's Item 3 measurement (Hikiavur t177: pottery demand 59 eaten 0, cloth demand 88 eaten 0
  → Comfort 0.0000 both classes) and left on the flow stand-in by that packet's stated verdict,
  which is why Comfort — not Shelter — is the residual grievance accruer on the fixed tree.
  **SHARPENED (T3.9b certification ruling, director): IT IS NOT SIMPLY SHELTER AGAIN.** T3.8
  rebound Shelter to the dwelling STOCK with timber and clay as upkeep; Comfort still reads
  pottery and cloth consumed THIS PERIOD. But the two decay for different reasons and therefore
  want different models: **a dwelling degrades from lack of MAINTENANCE; a pot breaks from
  USE.** The honest model is a HOUSEHOLD-GOODS STOCK that depletes with use and is replenished
  by crafting — which changes the crafting sector's job from *supplying comfort* to *replacing
  breakage plus growth*, and gives a settlement that has ACCUMULATED goods a materially lower
  crafting requirement than one starting from nothing. That is a DIFFERENT EQUILIBRIUM, not a
  copy of housing's, and the M4 spec should not reach for T3.8's maintenance-fraction shape by
  analogy.
  **OPEN QUESTION ATTACHED (T3.9b gate, director) — COMFORT MAY SATURATE ON ALMOST ANY NONZERO
  CRAFTING, WHICH WOULD MAKE IT A NEAR-USELESS SIGNAL.** Two data points exist and BOTH ARE
  EXTREMES: 0.98 at a 21% crafting share (T3.9b gate session, Nenatul) and 0.00 at zero crafting
  (T3.8 after-column, Hikiavur t177 — pottery demand 59 eaten 0, cloth demand 88 eaten 0).
  Nothing measures the middle. If Comfort reads near 1.0 across the whole usable range, the
  need discriminates nothing over the range a player actually plays in, and the FLOW reading is
  why — a flow at any adequate rate fills the basket.
  MEASUREMENT OWED, METHOD STATED: two settlements differing ONLY in crafting share (e.g. 15%
  vs 25%), same world and same seed, Comfort compared — and decomposed per §7.15 (pottery and
  cloth fill read separately, against population, since fill is a ratio whose denominator moves
  with demand). If Comfort barely moves across that range, it STRENGTHENS the stock rework
  considerably: a stock with turnover discriminates where a flow at any adequate rate does not.
  Not answered here. Owner: M4, riding with B-2 as ruled. CONSEQUENCE FOR THE RECORD ALREADY IN THE TREE: T3.8 measured Hikiavur's grievance
  falling 227.49 → 132.46 once Shelter gained memory, and **Comfort's flow reading is why it
  landed at 132 rather than in the tens** — the healthy-mix settlement in the same run reached
  10.85. Owner: M4, riding with B-2 per the director's ruling.
- **T3.9a GATE — SHELTER IS FLOW-NOT-STOCK: DIRECTOR MATERIAL, MOTIVATING MEASUREMENT FOR
  T3.8.** Measured at the gate (2026-07-29): Hikiavur at 100% farming reads Sustenance 0.88,
  Shelter 0.00, Comfort 0.00, grievance 119.55, holding 225,026 food; Mothian at the T3.5b
  default mix (55/15/10/12/8) reads Shelter 0.88, Comfort 0.74, grievance 16.49 — the
  difference is entirely construction and crafting share. WorldState.cs:278 says
  "Construction = 4; // PathBuild's pool (housing joins at T3.8)" — there is no housing stock;
  Shelter satisfies from current-period goods, so a settlement that stops building is instantly
  and completely homeless. Houses are durable; the honest model is a STOCK WITH SLOW DECAY,
  which is exactly what T3.8 ships. Shelter's Tier A dominance (D-018 §4: gate needs override
  signature weights when unmet) amplifies a flow artifact into the largest grievance term in
  the world. Explicitly: do NOT weaken grievance, do NOT lower Shelter's weight, do NOT
  down-rank it from Tier A — all three are fitting a coefficient to hide a missing mechanism
  (law 2). Filed against T3.8 as a MOTIVATING MEASUREMENT so that packet can state its
  expected before/after.
- **Q-F (director, 2026-07-30): THE THREE SKIP-GATED MEASUREMENT RIGS NEED INVALIDATION
  CONDITIONS.** FoundingVariationItem0Tests (~30 min) underwrites ADR-017 (the decision NOT to
  amend D-025's siting clause); WaterRouteCounterfactualTests ×2 (lattice and pixel passes)
  underwrite the reframing of escalation 1 from a CR against frozen constants into a missing
  mechanism — both conclusions will be cited by the transport packet and by T3.10. Skipping
  them is correct (they are expensive; that is why) — what is missing is an INVALIDATION
  CONDITION per rig: a skipped rig still compiles, so it goes semantically stale silently,
  still runnable, measuring a world that has moved, with recorded numbers that quietly stop
  describing anything. REQUIRED, one line per rig IN THE RIG'S OWN HEADER where the next
  reader sees it: what change would invalidate its recorded numbers. Candidates by rig —
  Item 0: worldgen, siting, endowment, sector mix, catchment. Water counterfactuals: the
  lattice stride, the river mask, transport cost, bulk values, the price band. Name them PER
  RIG, no general rule. Then a packet touching a named surface knows to re-run before citing —
  the coupling-map discipline (S8 §4.1 item 4) applied to EVIDENCE rather than code. OPEN,
  owner: T3.11 (harness and goldens, which owns test-suite health). Not fixed here.
  **DONE (T3.11 Item 4b).** Written IN EACH RIG'S OWN HEADER, per rig, no general rule:
  `FoundingVariationItem0Tests` names worldgen, siting/jitters, initial endowment, the sector
  mix, catchment, and the price-band/bulk/cost trio (one per reading it took), plus what is
  explicitly NOT invalidating. `WaterRouteCounterfactualTests` states one condition covering
  both passes — lattice stride, water mask, transport cost and the Pathfinder step formula,
  bulk values, price band, map scale. **One sharpening the original line did not anticipate:**
  the water rig REPLICATES the shipped Pathfinder step model rather than calling it, so a
  change to that model makes it SILENTLY WRONG rather than merely stale — re-derive, do not
  re-run. That distinction is called out in the header, because "stale" and "wrong" need
  different responses from whoever cites it.
- **Q-A (T3.8 Item 0, director): RIVERS CANNOT LIVE ON THE STRIDE-4 LATTICE.** The T3.6b water
  counterfactual's lattice pass could only see the SEA — stride-4 majority-water blocks hide
  rivers — and concluded even FREE water was insufficient; that was a resolution artifact, not
  economics (the pixel pass routes the real river mask and the sites sit on it). Collides with
  the existing lattice-stride entry: one node is 16×16 km and a river is a sub-node feature. Any
  future water-transport packet must resolve this FIRST, and it is an ARCHITECTURE call. The
  three candidates, named without choosing: (1) finer stride — quadruples node count and
  Dijkstra work, re-pins every golden; (2) river edges as an OVERLAY on the network vector
  graph, which is what D-009 actually describes ("canal" is a listed edge type); (3) river
  polylines promoted to first-class edges. OPEN, owner: the water-transport packet.
- **Q-B (T3.8 Item 0, director): HYPOTHESIS — ESCALATION 2 MAY BE ANOTHER FACE OF B-2.** Almost
  every non-grain price rests on a band edge (stocked goods at the 0.05 floor, zero-stock goods
  at the 20.0 ceiling), so every gap is identically zero and nothing can trade. Candidate cause:
  nothing bounds accumulation — abundant goods ratchet up forever and peg the floor, while
  goods consumed on production peg the ceiling. If stores were bounded, stocks would CYCLE and
  prices would float off the rails. Filed as an explicit HYPOTHESIS, not a finding, with the
  test named: when B-2's store bounding lands, measure whether prices leave the band edges — if
  they do, escalation 2 and B-2 are one packet. Owner: B-2, M4 blocking material.
  **EXTENDED (T3.8 certification ruling): B-2's THIRD COSTUME — TIMBER.** T3.8's H1 overshoot
  has the same root: timber stores keep housing maintenance m = 1 for decades after a farm-100%
  order (measured: Hikiavur reads Shelter 1.0000 at t177 after 30 years of nothing but farming,
  timber store 755 covering ~16 more turns). The decay mechanism is correct; the buffer it draws
  on is fictional. Three faces of one defect: grain (the world cannot starve, ~1,240 years of
  reserve), prices (11 of 13 goods pegged at band edges), timber (the world cannot become
  homeless). The hypothesis sharpens to ONE TEST, THREE PREDICTIONS: if bounding stores makes
  stocks CYCLE, it should unpeg prices AND make Shelter decay reachable — B-2 is the packet that
  makes three separate mechanisms observable, not just a scarcity fix.
  **EXTENDED AGAIN (T3.9b certification ruling): A FOURTH PREDICTION — COMFORT-AS-STOCK.**
  Bounding stores also makes the household-goods stock of the sharpened Q5 entry MEANINGFUL:
  without bounding, an accumulating goods stock saturates at 1.0 forever and never falls, which
  is the TIMBER PROBLEM AGAIN — B-2's third costume in a fourth costume. A Comfort stock built
  on top of unbounded accumulation would reproduce exactly the defect T3.8 measured for Shelter,
  one milestone later and with a second stock's worth of machinery behind it.
  **EXTENDED AGAIN (T3.11 Item 1 certification ruling): A FIFTH PREDICTION — BAND-EDGE PINNING
  DEGRADES RED PROOFS, NOT JUST TRADE.** Measured inside T3.11's red proof: perturbation P1's
  effect on the three named non-grain goods at settlement 0 was INVISIBLE, because all three
  rest on a band edge (0.05 or 20.0) and a clamped price cannot show a perturbation that would
  otherwise move it. The proof's evidence had to change shape mid-run, from per-good values to
  the aggregate non-grain sum, to see an effect that was really there. This is the §7.5 hazard
  ("never assert on a quantity resting against its own limit") arriving UNINVITED — §7.5 is a
  rule about how to write a guard, and escalation 2 turns it into a property of the world that
  degrades guards nobody wrote carelessly. Prediction: if bounding stores unpegs prices, every
  price-sensitive guard in the tree gets sharper for free, and red proofs stop needing
  aggregate statistics to see per-good effects. That is a FIFTH mechanism made observable by
  one bounding fix, and the first one that is about the project's INSTRUMENTS rather than its
  simulation.
  **ONE TEST, FIVE PREDICTIONS.** That is what Q-B exists to establish: these are ONE M4 packet,
  not five — bound the stores once and five separate mechanisms become observable together.
- **Q-C (T3.8 Item 0, director design input): DRAUGHT ANIMALS AS A SECOND TRANSPORT LEVER.**
  Overland bulk haulage moved by ox-cart, not on backs. Pack/draught animals drawn from the
  HERDING sector (live today at 0.15 of the default mix) would cut effective transport cost for
  high-bulk goods, lowering the deadband T3.6b measured at 23–35 against a maximum possible gap
  of 19.95. Mechanism-shaped and COMPUTED, never assigned: effective bulk cost falls with
  available livestock, so a herding settlement hauls cheaper. Sits alongside water routes as a
  second lever on escalation 1. OPEN, owner: the same future transport packet. Not designed
  here.
- **Q-D (T3.8 Item 0, director design input): HOUSING TIERS ARE OUT OF T3.8 SCOPE.** The
  director wants housing to progress IN KIND over the campaign — hut to longhouse to tenement
  to apartment block — not only in quantity. Real, wanted, and NOT the T3.8 packet. The
  governing constraint, recorded now: LAW 4 FORBIDS CALENDAR GATES — a tier cannot unlock by
  era or date; it must derive from computed state (materials available, population density,
  accumulated wealth, institutions). Wants D-017's settlement sprawl model alongside it. OPEN,
  no owner yet.
- **Q-E (T3.8 Item 0, director ruling): TRADE ROUTES AS EMERGENT INFRASTRUCTURE.** The Civ-6
  trade-route model is RULED OUT in its unit half: no placed trader unit, no player-created
  route — that makes exchange happen because the player chose it rather than because goods were
  worth moving, inverting the signal the current mechanism gives (D-009 deleted the separate
  region graph precisely so trade IS the transport network; D-018 forbids unlocked classes).
  The half that IS wanted: historical trade ran on ESTABLISHED routes — a path used repeatedly,
  maintained, cheaper each time. That is the network graph improving where traffic justifies
  it — D-009's own claim, which PathBuild already half-implements for farmland reach. Candidate
  mechanism: sustained flow on a pair justifies path improvement; improved path lowers
  transport cost; lower cost opens further pairs. Computed, self-reinforcing, no unit. Merchant
  consequence noted: T3.7's class then emerges on its own terms once volume justifies full-time
  traders, rather than being stapled on. OPEN, owner: the transport packet — Q-A (water),
  Q-C (draught animals) and Q-E are ONE design conversation and should be ruled together. Not
  designed here.
  **D-040 (2026-08-08) ADDS SEA TRAVEL TO THIS CONVERSATION** (`docs/d040-discovery-and-control.md`
  B3/B4): boats are the same object as water routes — sea travel extends the network's EDGE TYPES,
  it is not a separate movement mode, and it becomes possible when the conditions for boats exist
  (coastal settlement, timber, craft capacity), never by a technology unlock. D-040 C6 further
  couples this queue to the control model: **improving a route extends administrative reach**, so
  Q-A and Q-E strengthen the hold on what is at the end of them. Still OPEN; D-040 designs nothing.
  **D-040 F-tension, for whoever takes this packet:** D-009/D-010 calls bridges and tunnels
  "expensive, era-gated, terrain-crossing edges" — the same shape D-040 B3 rejects, sitting in a
  ratified document on this subject.
- **MODIFYING A GUARD RE-OPENS ITS RED PROOF — WRITTEN as ADR-015 §7.17 (GOV-1, merged
  behind T3.8; the merge-time re-point this entry was held open for)
  (director + agent agreement, T3.8; reasoning added at the director's ruling).** The T3.5b
  bound-need-must-have-a-satisfier guard was taught the T3.8 housingStock source; a guard
  taught a NEW case can lose its teeth on the OLD one, and its prior §7.4 red proof no longer
  automatically holds. THE MECHANISM, stated fully: a guard widened to admit a new legitimate
  case can silently begin admitting an illegitimate one, BECAUSE THE WIDENING IS AUTHORED
  AGAINST A FAILING TEST — here 229 tests were red and the shortest edit turning them green is
  a WEAKER guard. Nothing in the suite would have noticed, because the original property had
  no test of its own independent of the guard's existence — which is exactly why
  HousingGuardTests had to be WRITTEN rather than found. This is §7.5's shape (do not assert
  on a quantity resting against its own limit) pointed at a GUARD rather than a measurement.
  THE RULE: every property a guard held BEFORE a widening must be re-proven red
  INDEPENDENTLY — never by re-running the suite, which is what the widening was written
  against. First instance: HousingGuardTests (original no-satisfier red, source-typo red,
  double-sourcing ambiguity red, measured post-modification). NOTE, DISCHARGED: the three rules that
  sat here as queue lines rather than numbered sections — this one, the discriminating
  observable, and the asymmetric-margin pattern (T3.4c Q2's 11% must-pass margin, T3.4d's 5%
  must-fire ceiling) — are now WRITTEN as ADR-015 §7.17, §7.15 and §7.16 respectively (GOV-1,
  director-certified). A queue line does not bind an agent the way a numbered §7.x does; all
  three now bind.

- **FALLOUT ENUMERATION IS PER-SOLUTION, NOT PER-PROJECT** (T3.8 certification fix pass,
  Item 4 — director ruling). T3.8's fallout enumeration ran Sim.Tests only; a 14th real
  fallout item (the HUD needs-block pin — "Shelter: 0.00" against a founded world that now
  arrives housed) was invisible to it and surfaced only when Sim.Ui.Tests ran at handback
  item 3. The rule: any packet changing sim state that the HUD displays enumerates fallout
  across BOTH test projects, not just Sim.Tests. OPEN. Owner: T3.11 (harness and goldens).

- **THE GATE ARTIFACT'S LAYOUT BURIES THE EXECUTABLE** (director, T3.9b gate session).
  `ui-artifact.yml` publishes `-r win-x64 --self-contained -o publish/sim-ui-win-x64` and
  uploads that folder AS the artifact root, so the zip root holds ~40 .NET runtime DLLs
  (coreclr, clrjit, System.Private.CoreLib, …) plus the natives (SDL2, openal, cimgui) mixed in
  with `Sim.Ui.exe`, `assets/` and `runs/`. The director scrolls past the runtime to find the
  executable every gate, and `runs/` — the thing a gate session produces — sorts to the bottom.
  Ergonomics, not a defect: the build is correct and the gate works.
  **THE CONSTRAINT, NOTED BEFORE CHOOSING:** .NET's self-contained layout expects its runtime
  BESIDE the executable, so this is a PUBLISH/PACKAGING change, not a post-hoc file move. A
  workflow step that shuffles files after publish would break the app.
  **CHOSEN: APP IN A SUBFOLDER, LAUNCHER AT THE ROOT** — publish the whole tree unchanged into
  `app/`, leaving the root as `Play civ-sim.cmd` + `app/` + `runs/`. Two facts measured on the
  current tree make this a ZERO-CODE-CHANGE packaging edit:
    - `UiSession.SessionLogPath` builds `Path.Combine("runs", …)` — a RELATIVE path, resolved
      against the process WORKING DIRECTORY. A launcher that starts `app\Sim.Ui.exe` with cwd
      left at the zip root therefore puts `runs/` at the root by itself.
    - `AssetManifest` resolves art at `Path.Combine(AppContext.BaseDirectory, "assets")` — the
      EXE's directory, not cwd — so `assets/` travels with the exe into `app/` and keeps working.
  **REJECTED: `PublishSingleFile`.** It is the tidier-looking answer and it carries real risk
  here: MonoGame.Framework.DesktopGL loads SDL2/openal/cimgui as native libraries, and
  single-file self-extract changes where those resolve at runtime; it would also invalidate
  `ui-artifact.yml`'s completeness assertion, which checks those four files by name beside the
  exe (the T1.7 director hardening that exists so a partial publish can never go green). A
  gate-ergonomics improvement must not put the gate build itself at risk. Revisit only if the
  launcher proves unsatisfying in use.
  **FOR WHOEVER TAKES IT:** the completeness assertion's paths move under `app/` and must move
  with it; the launcher should be plain enough to read at a glance; and confirm on Windows that
  cwd is the launcher's directory rather than `app/` (the `runs/` placement depends on it).
  **DONE (T3.11 Item 4c).** `ui-artifact.yml` publishes to `publish/sim-ui-win-x64/app` and
  writes `Play civ-sim.cmd` at the root; the zip root is now launcher + `app/` + (after a
  session) `runs/`. The completeness assertion MOVED WITH the tree rather than being relaxed,
  and gained two checks it never had: the launcher itself, and `app/assets` — a zip missing its
  root file is precisely the defect this change exists to prevent. Both path facts were
  RE-VERIFIED against the current tree, not carried from this line: `UiSession.SessionLogPath`
  is still `Path.Combine("runs", …)` (relative) and `AssetManifest` still resolves at
  `AppContext.BaseDirectory`. **ONE CONFIRMATION STILL OWED, AND IT CANNOT BE MADE HERE:** the
  launcher does `cd /d "%~dp0"` so cwd is forced to the launcher's own directory regardless of
  how Explorer invokes it — but that is reasoned from cmd.exe semantics, NOT measured, because
  this is a Linux container with no Windows to run it on. The director's next gate download is
  the measurement: if `runs/` appears at the zip root beside the launcher, it holds; if it
  appears inside `app/`, the `cd /d` did not take and the launcher needs revisiting. Flagged
  rather than assumed.

- **REFERENCE POINT FOR T3.12's EXIT SESSION — PLAYER-SET ALLOCATION BEATS THE DERIVED
  DEFAULT** (T3.9b visual gate, director's own session, 2026-08-05). Seed 42, settlement
  Nenatul, hand-set mix typed 55/46/41/46/36 and applying as **25 / 21 / 18 / 21 / 16**:
  **grievance 6, Shelter 1.00, Comfort 0.98, Sustenance 0.97, population 2,095 and rising,
  food 2,071,560.** Against T3.8's measured after-column on the canonical world — healthy-mix
  Mothian at grievance 10.85, farm-100% Hikiavur at 132.46 — this is the **FIRST EVIDENCE THAT
  A PLAYER-SET ALLOCATION OUTPERFORMS T3.5b's DERIVED 55/15/10/12/8 BASELINE**, and it is filed
  so T3.12's exit session has a KNOWN-GOOD MIX to compare against rather than re-deriving one
  under gate conditions.
  **NOT A REASON TO RETUNE THE DEFAULT.** T3.5b's mix is DERIVED from a reference class and
  CR-003 §5.1 governs it. A player finding a better mix for a particular world is the control
  working as designed — the default is the never-ordered starting point, not a claim to be
  optimal on every world.
  **TWO HONEST QUALIFICATIONS, so the reference point is not over-read:**
    - It is NOT a controlled comparison. Different settlement, different world instance,
      different horizon, and a session the director steered throughout; the T3.8 figures are
      Mothian t29 and Hikiavur t177 under a replayed order log. It is a reference POINT, not a
      measured delta.
    - It points the OPPOSITE WAY FROM R1 on population, and this packet cannot say why. T3.9b's
      R1 found a hand-set mix that raised per-capita satisfaction by HALVING the population
      (444 → 213, every production number falling); the gate session's settlement is large,
      growing, and well supplied at a LOWER farming share still. Different worlds and horizons,
      so the two are not in contradiction — but nothing here measures which factor separates
      them. Recorded as an open observation for T3.12, not resolved.
  OPEN. Owner: T3.12 (M3 exit session).

- **ADR REQUIRED — THE KERNEL CLONE-SIZE CLAIM AND THE BUCKET-CAP COLLISION, ONE RULING.
  OWNER: DIRECTOR. Due at M4 spec time (director ruling, T3.11 certification, 2026-08-06).**
  T3.11 Item 3 discharged the measurement GOV-2 §5 recorded as owed, and the result decides an
  amendment this agent may not write: `m0-kernel-spec.md` is inside the M0 freeze perimeter
  (`spine-s8-governance-freeze.md:15,202`), so the mechanism is a director-ruled ADR.
  **THE THREE MEASUREMENTS** (`docs/t3.11-review-record.md` Item 3; `sim bench` now reports
  bucket rows and clone bytes, which it did not before):
  1. **today, M3, N = 12:** 384 bucket rows, **82,096 B/turn = 0.078 MiB** — some 40× UNDER the
     §3.2 claim of "a few MB", and DENSE founding confirmed against GOV-2's code-only prediction
     of exactly 384;
  2. **scaling, measured not assumed:** 12 → 73,906 B, 24 → 147,036 B ⇒ **≈ 6,094 B per
     settlement**, essentially the whole clone;
  3. **projection:** at the Charter's 800 settlements with D-018's 12 class slots and a single
     culture/religion, **153,600 bucket rows — AT the ratified ~150k cap — and order 16 MB per
     turn**; at 4 cultures × 4 religions, ~2.46M rows and order **200 MB**.
  **THE CLAIM'S STATED RANGE IS WRONG AT THE FAR END.** §3.2 covers "M0–M9"; what it actually
  covers is "while buckets stay small", and buckets are exactly what D-018 and plural cultures
  grow. CONFIRM for the milestones reached, NARROW for the rest.
  **WHY ONE RULING AND NOT TWO (the director's reasoning, recorded):** this now joins the
  bucket-cap collision GOV-2 already carries — dense founding, a ratified ~150k world-wide cap,
  an unbuilt "automatic merge-below-threshold policy" the cap presupposes, and a clone-size claim
  that fails at the same scale. Four faces of one scaling decision, and **the largest unscheduled
  item in the project.** Cross-reference: `docs/m4-pre-spec-dependencies.md` §5;
  `civ-sim-architecture-v3-outline.md:44`; `docs/d018-classes-and-needs.md:10`. Also inbound at
  that scale: ADR-008's ~50 MB of terrain re-enters the clone for any layer that gains a writer.

- **SWEEP OUTPUT MUST BE CAPTURED WHOLE AND FILTERED AT READ TIME (director ruling, T3.12 exit,
  2026-08-06).** A sweep whose PURPOSE is to catch failures must **capture full output to disk and
  filter at READ time, never at CAPTURE time.** Piping `dotnet test` through `grep` discards the
  test name and the assertion text before anything is written, so a red becomes UNEXPLAINABLE
  after the fact. **Measured instance:** T3.12's pre-exit sweep recorded
  `Failed: 1, Passed: 150` for `Sim.Ui.Tests` in a run captured as `dotnet test … | grep -E
  "Passed!|Failed!|error"`. Three lines survived; the failing test's name and message were never
  written and could not be recovered. The final tree ran green and the director ruled the exit
  proceeds, but the observation itself is permanently lost. Owner: T4-era harness work, or
  wherever the sweep script comes to live.

- **CI PROCESS DEFECT — A NIGHTLY FAILURE MUST SURFACE WHERE SOMEONE READS IT. OWNER: CI,
  M4-era (director ruling, T3.12).** Measured from the Actions API, not inferred: the scheduled
  `calibration-nightly` job failed on **ELEVEN CONSECUTIVE RUNS**, from 2026-07-27 (run
  `30243589130`) through 2026-08-06 (`31076072204`). Last green: **2026-07-26**
  (`30190719365`). Nobody noticed until T3.12's pre-exit sweep reproduced it locally.
  **WHAT THAT COST, stated precisely: T3.5b, T3.6, T3.6b, T3.8, T3.9a, T3.9b and T3.11 all
  landed under an instrument that was already dark.** The whole second half of M3 shipped with
  its ≥20-seed calibration gate providing zero signal.
  **AND THE SHARPER VERSION — IT WAS NOT MERELY DARK, IT WAS MASKING.** The density false-red
  (a corridor the project had already quarantined, see the `quarantine` block in
  `corridors.json`) hid a GENUINE reading for those eleven nights: `migrationGrossPerDecade`
  seed 9 at 0.00098 against a 0.001 floor. Once T3.12 taught the nightly about quarantines, the
  job still failed — on the real signal, which had been sitting underneath the noise the entire
  time. **An instrument that is dark is bad; one that is dark AND masking a real signal is
  worse.** A red that nobody reads is indistinguishable from a green, and a red that is
  *expected* trains everyone to ignore the one that is not.
  This is the same shape as CLAUDE.md's false merge-loop line and the Spine's stale inventory:
  **nothing checks the checker.** The corridor fix (T3.12 item B) closes the instrument
  disagreement; it does NOT close this. Candidate mechanisms, none chosen here: fail the
  scheduled run loudly into a channel the director reads; a badge; a "days since last green
  nightly" line in the run summary; or a follow-up issue opened automatically on the first red.

- **MIGRATION CORRIDOR FLOOR MAY BE MIS-SPECIFIED FOR SMALL WORLDS (T3.12b, measured).**
  `canonical.migrationGrossPerDecade` floor 0.001 is ABSOLUTE and takes no account of world size.
  Seed 9 breaches it at 0.000980 (2 % under) — and seed 9 is the SMALLEST world in the 20-seed
  sweep (population 81,160 vs a 132,280 max). The metric is already population-normalised
  (`AutoplayMetrics.cs:139`), yet corr(migration, population) = **+0.737** and
  corr(migration, arable) = **+0.815** across seeds: bigger, more-arable worlds migrate
  PROPORTIONALLY more. So the smallest world showing the lowest migration intensity is the
  expected reading, not an anomaly, and one seed in twenty landing 2 % under an absolute floor is
  what that looks like. **Not a migration defect on this evidence.** Owner: whoever homes the
  migration-weight packet (T3.4c ruling 2). Do not re-tune the floor to silence it — that would
  be fitting the instrument to the artifact, and the reason the floor is absolute may itself be
  the thing to revisit.
- **WHAT MAKES WORLD POPULATION VARY 1.63x ACROSS SEEDS IS STILL OPEN (T3.12b).** Measured:
  81,160 to 132,280 over 20 seeds, and it is what drives the density corridor's across-seed
  spread (corr(density, population) = +0.858 vs corr(density, arable) = +0.018). Migration is
  EXCLUDED by measurement — it conserves people, and starvation deaths are ZERO in 20 of 20
  seeds, so the only channel by which it could move world population is shut. World population is
  set by births and deaths integrated over the era table, so the spread must originate in
  FOUNDING conditions compounding through the demographic integration. **ADR-017's endowment
  jitter at 0.69 is the named candidate and it is NOT bisected.** Owner: unassigned; belongs with
  M4's CR-002 packet, which already carries the density corridor.
- **CANDIDATE — ADR-015 SECTION: CONDITIONAL EXECUTION AND STANDING AUTONOMY (GOV-3).**
  Filed by `docs/gov-3-execution-protocol.md` Part D. A directed packet may pre-rule its branches
  and the agent EXECUTES the matching one; branch on the MEASUREMENT never the interpretation; cap
  the chain at THREE branch points; every branch states its else; the unpredicted case always
  stops. Plus the standing permissions (merge on a pre-given ruling, file a finding, correct a
  citation error in one's own prompt, re-run an invalid measurement) and the B6 list of what still
  always stops. **Expected to recover a MINORITY of round trips** — the record says so explicitly,
  because the round trips actually observed were unpredictable FINDINGS, which must still stop.
  **A3's evidence was verified and corrected: two of six cited instances are the claimed
  measurement-correct/interpretation-wrong shape; the other four are a stale premise, a wrong
  measurement, a mis-citation and a wrong instrument** — four further ways a branch condition can
  be false. **OWNER: DIRECTOR**, to rule at the next governance packet.
  **THREE CANDIDATES NOW SIT UNQUEUED INTO SECTIONS** (this one, the git-operation pattern at
  READY TO WRITE, and the registry-id rule). **A queue line does not bind an agent the way a
  numbered §7.x does** — GOV-1 made this same observation before §7.15–§7.17 were written.
- **FILED, NOT FIXED — T4.1's ORDER-CONDITIONAL FRAMING SURVIVES IN THIS FILE** at the entries
  around line 834 after the claim was withdrawn (T4.1d: the class-id bug; the two paths were
  bit-identical). Recorded by GOV-3 G2 under B3, which permits the filing and forbids the repair.
  **Owner: whoever takes T4.14.**
- **AUDIT — A DISCRIMINATOR PLACED BEHIND THE ASSERTION IT DISCRIMINATES CANNOT FIRE.
  OWNER: T3.11's HARNESS WORK.** Found at T4.1e (2026-08-08): `FirstReignTests`' shape asserts —
  the anti-blind-re-pin guard, whose comment says *"Never delete these as redundant with the
  golden"* — sit AFTER the world-hash assert. **On a failing run the hash throws first and the
  shape asserts never execute**, so the one run in which a reader needs to tell a RE-MINT from a
  REGRESSION is exactly the run that cannot tell them. T4.1e re-pinned the hash and re-ran so the
  shape asserts actually executed (they passed); a packet that skipped that step would have written
  "shape asserts re-verified" having never run them.
  **The audit: check every pin carrying semantic asserts for the same ordering, and put the
  semantics FIRST** — a guard should fail on what it guards, not queue behind a hash. Not fixed at
  T4.1e: out of that packet's one-change fence.
- **DEFECT — THE FOUNDED GOLDEN IS PINNED IN TWO PLACES; T4.1e MOVED ONE. `determinism-xproc` IS
  RED ON `main`. OWNER: CI.** Run history, `main`: GREEN at `3185a6b` (Aug 8) and at `d4ce188`
  (Aug 11 04:39); **FIRST RED at `7ae19c9`, the T4.1e merge** (run 31479413458, job 93740652645).
  **It is NOT a determinism regression.** The same job log, verbatim:
  `founded orderless: two processes byte-identical over 300 turns` and
  `replay: byte-identical to the ordered run over 400 turns`. What fails is a golden comparison:
  `FOUNDED RUN DIVERGED FROM PINNED GOLDEN: expected b9f93d4a… actual 63c8579a…` — the actual is
  T4.1e's correctly re-pinned value; the expected is the stale one at
  **`.github/workflows/ci.yml:136` (`FOUNDED_GOLDEN=`)**, a SECOND copy of a constant the test
  suite also holds. A local suite run cannot see it: the duplicate lives in CI yaml.
  **FIXED (2026-08-11, director ruling):** `ci.yml:136` updated to `63c8579a…`, the correct value.
  **AND THE GENERAL DEFECT IS GUARDED:** `Sim.Tests/Kernel/CiPinAgreementTests.cs` asserts the two
  copies agree. **Option chosen and why:** making ci.yml read from the suite's source removes the
  possibility, but the golden's home is a C# const inside the test that computes the hash and yaml
  cannot read C# — sharing it would move a test pin into a data file, trading one duplication for a
  worse one (a golden nobody reads beside its assert). The agreement test is cheaper, fails loudly
  in the suite before the push, and catches exactly the drift that occurred. Red-proved by drifting
  the yaml pin (FAILED) and restoring (PASSED).
  **AUDIT: ci.yml carries exactly ONE 64-hex pin** — `FOUNDED_GOLDEN`. No other duplicated
  constants found.
  **The general defect: a pinned constant duplicated across the test suite and the CI workflow has
  no single source of truth, and only one copy is covered by the suite.**
- **FIXED (2026-08-11) — `build-and-test` WAS RED ON `main` ON THE READ-ISOLATION CHECK. CAUSE:
  T3.12a, NOT A PRE-EXISTING FAILURE.** *(Dating corrected by the director: red at `3185a6b` is
  AFTER T3.12a merged, so the reporter is the cause; my earlier "pre-existing since at least
  2026-08-08" was wrong.)* `ReplayReport.cs` reads `NeedSatisfactions` and `Grievances` BY DESIGN —
  it is a reporter — and the T2.6 allowlist predates reporters. Allowlisted with the reason at the
  entry; red-proved by removing the entry (FAILED) and restoring (OK).
  **MEASURED WEAKNESS OF THE GUARD, filed not fixed:** it is a bare grep for four identifiers. It
  does NOT distinguish a WRITE from a READ, nor sim code from reporting code, nor code from PROSE —
  **two of the seven lines it flagged were DOC COMMENT text**. A path allowlist is its only lever,
  so every future reporter, exporter or debug dump trips it for the same non-reason. Same class as
  the FirstReign ordering finding: the guard's name claims more than its mechanism delivers.
- **(superseded, kept for the dating correction) `build-and-test` red on `main`.** Job step *"Read-isolation check (T2.6 — grievance read by
  nothing but UI/chronicle)"* fails at `3185a6b`, `d4ce188` and `7ae19c9`; **every later step —
  Setup, Restore, Build, Test, and the T0.2 no-compile acceptance — is SKIPPED as a result.**
  **The CI suite has therefore not run on `main` for at least three days**, and nobody read it.
  Same family as the nightly's eleven silent runs. Not diagnosed here beyond the step name.
- **PROCESS — A REQUIRED STATUS CHECK CAN BE BYPASSED SILENTLY BY THE PUSHING IDENTITY.** `git push`
  to `main` printed `remote: Bypassed rule violations for refs/heads/main: - Required status check
  "determinism-xproc" is failing.` and **the push succeeded**. The ruleset lists a bypass actor and
  the session's identity is in it, so the gate reports rather than blocks. **A gate that can be
  bypassed silently is not a gate.** Options (none taken): remove the bypass actor for `main`; or
  keep it and make the bypass loud. **Director's ruling.**
- **THE THIRD UNDECLARED COUPLING TO `minSpacingKm`, AND THE ADR SAYS SO.** Measured across
  T4.1b/T4.1e: spacing is coupled to **(1) the deposit CORRELATION LENGTH** — hypothesised,
  **REFUTED** (the moisture channel saturates at every site, so no correlation length helps);
  **(2) the deposit SAMPLING FOOTPRINT** — **REAL**, repaired at T4.1e; **(3) `landWeight`** —
  **REAL**, packing shrinks partitioned catchments, the land term shrinks, attractiveness gaps
  narrow, T2.8's gap-closing cap binds, and the migration rate lever loses its teeth. T3.4b already
  fixed this once by re-deriving `landWeight` after the catchment became a 50 km hinterland.
  **ADR-018 §7's "what breaks" enumeration is INCOMPLETE AS WRITTEN and should say so** — it lists
  seven moving tests and no couplings. **Spacing has more undeclared couplings than any packet has
  enumerated, and the count is 3 discovered in 2 packets.**
- **DEFECT — THE DEV MIGRATION QUARANTINE ENVELOPE IS STALE ON `main`, AND ITS SELF-VERIFICATION
  CLAIM IS FALSE AS IMPLEMENTED. OWNER: T3.10's MIGRATED CORRIDOR WORK IN M4.**
  Measured at T4.1b (2026-08-08) on `origin/main`, with NO packet change applied, via the real
  battery in a clean worktree:

  | seed | recorded constant | measured on main | ratio | tolerance |
  | --- | --- | --- | --- | --- |
  | 42 | 0.000931705 | 0.000887533 | ×0.953 | 0.75 |
  | 7 | 0.000799951 | 0.000644000 | **×0.805** | 0.75 |

  **Neither pin reproduces; seed 7 sits 0.055 above the drift tooth.** `CalibrationBatteryTests.cs:170-174`
  states the recorded values are *"self-verifying: the drift tooth below fails the moment either
  recorded value stops matching what the battery measures, so a stale pin cannot rot silently."*
  **That property does not hold.** The tooth compares against `recorded × 0.75`, so a pin can rot by
  up to 25 % in silence — and both have, one of them by 19.5 %.
  **This is §7.12 pointed at a GUARD: an assertion claiming a property it does not have.** Same
  family as the nightly that was red for eleven runs while nobody read it.
  **WHEN it drifted, and FROM WHAT, is unmeasured** — a bisect over the packets between T3.4c's pin
  and `main`, on one cheap metric. **Do not let a future re-pin absorb two causes** (the ruled
  spacing change and this drift) in one act; separate them or state which is being absorbed.
- **READY TO WRITE — ADR-015 SECTION: AN OPERATION THAT LOOKS LIKE IT SUCCEEDED IS NOT EVIDENCE
  THAT IT DID. OWNER: DIRECTOR, to rule at M4 spec time** (filed by director instruction,
  2026-08-06, into the same candidate register §7.15–§7.17 came from; GOV-1's precedent is that
  candidates are FILED and the director rules on writing them, so no section text is drafted
  here). **THREE INSTANCES, ALL IN THE LAST FEW PACKETS, ALL THE SAME SHAPE:** a git operation
  that *appears* to have done what was asked while doing something else.
  1. **T3.9b** — `git checkout` silently NO-OP'd on an untracked file, so both §7.4 arms were
     defeated at once and the red proof was ambiguous. Discarded and redone.
  2. **T3.11** — `git push --delete` returned **HTTP 403 for all 21 branches** while the loop
     printed `DELETED: 21`: the counter incremented on ATTEMPTS and the failures went to stderr.
     Caught only by re-listing the remote afterwards.
  3. **CONV-1** — `git checkout -- <file>` reverted the file to `main`, silently taking the
     packet's own rename along with the perturbation it was meant to undo.
  **WHAT THE THREE HAVE IN COMMON:** each was caught by CHECKING THE RESULT rather than trusting
  the command's apparent success, and in two of the three the operation's exit signal was
  actively misleading (a no-op is success; a counter is not a result).
  **THE REMEDY, FOUND INDEPENDENTLY TWICE:** **COMMIT A VERIFIED-GREEN BASELINE BEFORE A RED
  PROOF.** Then every revert returns to a KNOWN state rather than an assumed one, each arm is
  measured against that state, and a silent no-op or an over-wide revert shows up as a diff
  instead of as a clean-looking proof. Generalised: **a git command's output is not a
  measurement of the repository — re-read the state.**
  4. **T3.12** — `dotnet build … | grep -c error` returned `0` and therefore EXITED NONZERO, so
     the `&&` that followed silently skipped the entire test suite. The command reported nothing
     wrong because it never ran the thing that could report.
  5. **T3.12, MID-RED-PROOF** — `git checkout -- corridors.json`, intended to undo a
     perturbation, reverted to HEAD instead. The quarantine block being proved was still
     UNCOMMITTED, so the restore destroyed the work rather than the perturbation.
  **WHY IT BELONGS IN §7.4 RATHER THAN IN AN AGENT'S HABITS:** §7.4 already requires proving a
  guard red, and every instance is a failure of the PROOF PROCEDURE, not of the guards. Note
  instance 4 is not a git command at all — the pattern is broader than git: **any operation
  whose success signal is not the thing you care about.** A no-op is success; a counter counts
  attempts; `grep -c 0` is a failure exit; a revert restores whatever HEAD happens to hold.
  **STATUS UPGRADED TO READY TO WRITE (director ruling, 2026-08-06), and instance 5 is why.**
  Instance 5 occurred AFTER instances 1-3 were written into this entry, in the same session, and
  **the remedy this entry names is what recovered it**: the loss was caught by checking the
  result, the baseline was then COMMITTED (`1fad8e9`), both arms were re-run against it, and the
  restore was verified with `git diff --stat` returning empty. A remedy that prevents a loss in
  the same session it is written down is no longer a proposal with anecdotes behind it. The
  director rules on writing it as a numbered section at M4 spec time, alongside the other
  candidates.
  **FILED ON THE `conv-1-term-namespacing` BRANCH** because that is where instance 3 occurred and
  CONV-1 is itself a conventions record; redirect it if another home is preferred.

- **THE M4 SPEC IS WRITTEN AND AWAITS THREE RULINGS (`docs/m4-spec.md`, PROPOSED, 2026-08-06).**
  First spec under S8 §4.1; its own conformance is stated requirement by requirement in its §0.
  T4.1 is the foundations audit (packet one) carrying four named checks — Q1's bind ratio, D-037's
  three-quantity model, the notables retrofit fields, and `minSpacingKm` vs the Scale Charter.
  T4.2 is B-2 store bounding with Q-B's five predictions pre-committed. **The packet list is NOT
  final until the director rules R-1 (is a notable a person — counted or labelled), R-2 (`stock`
  and `source` namespacing, which waits on R-1), and R-3 (the bucket-cap and clone-size ADR, ONE
  ruling under S8 §2, and the largest unscheduled item in the project — it bites at M8/M9).** The
  spec also records findings against its own directing prompt (§9). **F4 was WITHDRAWN FOR A
  METHOD REASON and REPLACED** (director ruling): artisan emergence is an INSTANT, and sampling
  eleven turns of 650 cannot see it. Re-measured scanning EVERY turn, the claim's shape is
  **CORROBORATED** — 19 of 35 emergences across seeds 3/6/9 occur at EXACTLY 1, and the
  distribution is **BIMODAL with an empty gap between 3 and 26** (low mode {1,3}, high mode 26–60),
  reproducing the director's Nuhem-27 / Naethaehun-36 split on independent seeds. **The anomaly is
  the SPLIT, not the number 1.**
- **ARTISAN CLASS EXTINCTION — NEW M4 FINDING (m4-spec §9 F4b, measured every turn, seeds 3/6/9).**
  Artisans rise to the hundreds by turn 160 (12/12 settlements non-zero), peak near turn 320, then
  **COLLAPSE TO ZERO in 6 of 12 settlements by turn 650** — a class going extinct across half the
  world. Seed 3 finals: `[0, 1849, 0, 0, 0, 1805, 0, 2427, 2964, 1788, 0, 1376]`. Corroborates
  T3.12a's sample line `(Artisans, 0, 0)` with Comfort 0.00, and **feeds the bronze chain from the
  other end: no artisans → artisan_share zero → the casting gate is shut regardless of whether it
  ever opened.** OPEN QUESTION, not answered: is the collapse the emergence latch's RECEDE arm
  firing, CLASS MOBILITY moving people out, or the SETTLEMENTS THEMSELVES declining? Those are
  different defects. **Owner: T4.14**, alongside the emergence question.
- **NOT IN THE TREE — the director's M3 exit session log `orders-20260807-145349.bin`.** Absent
  from `docs/`, `runs/` and the container; never committed. T4.14's first obligation is to replay
  it once it is available; every artisan measurement recorded so far replays the shipped
  `orders-20260724-164734-held-exit.bin` on seeds 3/6/9 instead, and corroborates the SHAPE on
  independent worlds rather than reproducing his session.

- **M4 SPEC: R-1, R-2 AND R-3 RULED (director, 2026-08-07); THE PACKET LIST IS FINAL.**
  **R-1 — A NOTABLE IS A PERSON (Option B):** extracted from the bucket via `Ledger.Transfer`, a
  conserved population stock with births, deaths and a law-1 audit. Lifecycle, defection and purge
  become conservation-exact rather than each inventing its own bookkeeping, which D-021 valve 5
  already requires. Cost accepted deliberately. **T4.8 ships the conservation surface from day
  one; the audit is part of the packet, not a follow-up.**
  **R-2 — NAMESPACING TAKEN:** bare `stock` = goods inventory, housing = `dwelling stock`,
  population qualified explicitly; bare `source` = a need satisfier's binding, claims say
  `claim origin`. Both moved PROPOSED → RULED in `docs/conv-1-term-namespacing.md`, registry
  updated. R-1 is what unblocked them: the third meaning of `stock` now exists.
  **R-3 — RAISE THE CEILING AND RETHINK THE COPY ARCHITECTURE**, a fifth option beyond the
  spec's four. Raising the cap PERMITS more rows; it does nothing about every row being COPIED
  EVERY TURN, which is the actual late-game constraint. **New packet T4.16** — design and
  measurement only, producing an **ADR under S8 §2** (both `m0-kernel-spec` §3.2 and the kernel
  clone are inside the M0 freeze). Three non-negotiables: **read-isolation preserved exactly** (a
  scheme that weakens it is rejected, not traded off), **determinism absolute** (two-axis
  assertion with a vacuity guard, T3.12a precedent), and **goldens must not move** (if one does,
  that is a finding and the packet stops — it means behaviour changed, not representation).
  Candidates to MEASURE not pick: copy-on-write per table (law 6 already declares writers
  statically), lazy clone on first write, delta journal. **Framing correction recorded against the
  spec's own option list: sparse founding is COMPLEMENTARY to raising the ceiling, not an
  alternative — rows exist at ZERO population, so most of the projected 153,600 is empty slots,
  and sparse founding + a raised cap + a cheaper clone are three independent wins.** Slotted early
  and NON-BLOCKING, because the ADR's blast-radius inventory grows with every table M4 adds before
  it is written — not because it is urgent (measured 0.078 MiB today).

- **T4.1 FOUNDATIONS AUDIT FINDINGS (`docs/t4.1-review-record.md`, 2026-08-07).**
  **Q1 ANSWERED — the bind ratio nobody had measured:** LABOUR binds **12/12 settlements at every
  turn** on the canonical world; the land/labour ratio falls 213× (t100) → 36–63× (t300) →
  **9.4–19.2× (t650)**. The world converges toward land-binding across a campaign and never
  reaches it; the real distance is **~1 order of magnitude**, against T3.4c's ×1e6 rig which sat
  ~5 orders past the threshold. B-2 and colonization now have the number they were filed to need.
- **Q1.2 / A2 — THE TOOL YIELD BONUS IS EFFECTIVELY INERT (new, T4.1).** `toolFactor = 1 + 0.3 ×
  min(1, toolStock/farmLabor)`. Measured t650: tool stocks **0–49 per settlement** against
  farmLabor **2,000–4,500**, so toolFactor ≈ **1.001–1.015 against a designed 1.3** — under 5 % of
  its design range, and 0–2 tools (i.e. nothing) in 3 of 12 settlements. The chain that feeds it
  is OPEN and correctly gated; the failure is quantitative, at the last link. `toolYieldBonusMax`
  0.3 is **chosen, never derived**. Recorded per S8 §4.1's common-case disposition; NOT corrected.
  Owner: whoever revisits farm yield or the crafting sector.
- **BOTH M3 ARTISAN INHERITANCES ARE ORDER-CONDITIONAL (T4.1 discriminator, and it re-aims
  T4.14).** Same seeds 3/6/9, with and without the shipped held-exit order log:
  **WITH orders** — emergence at turns 4–137, counts bimodal {1,3} ∪ {26–60}, 19 of 35 at exactly
  1, and **6 of 12 settlements at ZERO artisans by t650**.
  **NO orders** — artisans present in **all 12 settlements from TURN 1** at founding-endowment
  levels (121–665), **never at 1**, and **ZERO collapse**: all 12 hold thousands at t650.
  So the "class going extinct across half the world" is **driven by the labour orders, not by the
  emergence mechanism**, and "emergence at exactly 1" is a property of order-driven RE-emergence,
  not of founding. §7.10: the M4-spec measurement was correct; its interpretation was missing this
  precondition. **T4.14's subject is re-aimed to the interaction between labour orders and class
  emergence/mobility.**
- **ESCALATION FROM T4.1 Q3, FOR THE DIRECTOR BEFORE T4.8 BUILDS — "PURCHASE" HAS NO CONSIDERATION
  AT M4.** R-1 ruled notables are conserved people, so born/dies/defects are all clean Ledger
  flows. **"Is bought" (D-021 valve 5) is not:** the person moves, but the PAYMENT is a second
  flow, and M4 ships no currency (money ruled M5, in kind). Options, none chosen: (i) M4 ships
  defection-without-payment, purchase waits for M5; (ii) purchase paid IN KIND from goods stocks,
  which M4 does have; (iii) purchase modelled as influence, a new non-conserved quantity.
- **T4.1 Q2 — THE TWO D-037 IMPLEMENTATIONS THAT WOULD FORECLOSE PART C.** Storing control as an
  OWNER ID ON THE PLACE ROW forecloses overlap; storing recognition as a FLAG ON THE POLITY
  forecloses asymmetric recognition. **Both are the natural first implementation**, which is
  precisely why D-037 D1 calls the omission unbuildable-later. T4.3 must ship all three as
  RELATIONS. Named so the audit's warning is actionable rather than general.

- **ARTISANS-AT-1: CONFIRMED FROM PRIMARY EVIDENCE (T4.1, director's chronicle supplied mid-packet,
  `docs/session-logs/chronicle-20260807-145349.txt`).** All twelve emergences from the director's
  own session: **ten at EXACTLY 1**, Nuhem at **27** (year 40) and Naethaehun at **36** (year 380).
  **Bimodal with nothing between 1 and 27.** The claim stands exactly as stated. It also
  corroborates T4.1's order-log measurement independently — that run gave emergence at turns 4–137
  with a high mode of 26–60, and year 40 at the early dt of 10 IS turn 4, so the two agree on both
  modes and on timing. **SIZE DOES NOT PREDICT THE MODE:** Nuhem is the largest founding (663) and
  lands high, but Mothian is second largest (583) and lands at 1, while Naethaehun is mid-pack
  (380) and lands at 36. **That is T4.14's discriminating question and it is now well-posed.**
- **Q1.2 / A2 — THE TOOL YIELD BONUS IS A SHIPPED MECHANISM THAT DOES ESSENTIALLY NOTHING. OWNER:
  T4.14 (director ruling, T4.1 certification).** Filed as its own M4 item because it is the bigger
  finding of the audit. Measured t650: tool stocks **0–49 per settlement** against farmLabor
  **2,000–4,500**, giving toolFactor **≈1.001–1.015 against a designed 1.3** — **under 5 % of
  design range**, and 0–2 tools (nothing) in 3 of 12 settlements. It sits on the LAST LINK of a
  chain T4.1 proved open end to end: artisans exist, `artisan_share` clears 0.05 in 12/12 by turn
  300, the gate opens, bronze is produced (and correctly shows stock 0 as an intermediate consumed
  by toolmaking within the turn). **DO NOT FIX, AND DO NOT RAISE ANY CONSTANT TO MAKE THE BONUS
  BITE — the cause is upstream (how few tools exist), not the coefficient.** Homed with T4.14
  because both now turn on one question: **what happens to production classes under labour
  orders.**
- **Q1 ANSWERED, for the record (T4.1):** LABOUR binds 12/12 at every turn; land/labour ratio
  213× (t100) → 36–63× (t300) → **9.4–19.2× (t650)**. The world converges toward land-binding
  across a campaign and never reaches it; the distance is **~1 order of magnitude**. **T3.4c's
  ×1e6 rig sat about FIVE ORDERS past the real threshold — which is exactly why the number was
  worth measuring.**
- **"IS BOUGHT" — RULED: M4 DOES NOT IMPLEMENT PURCHASE. A NARROWING OF R-1, NOT A DEFECT IN IT
  (director ruling, T4.1 Q3 escalation).** Born, dies and defects are clean Ledger flows; **is
  bought is not**, because the person moves and the CONSIDERATION is a second flow, and payment is
  money — **M5** per GOV-2 §1a. **R-1 stands:** the notable moves via `Ledger.Transfer` whatever
  the reason; the consideration cannot exist until money does. **D-021 valve 5's "bought" arrives
  at M5 with the fiscal system, not at M4 with generals.** **One more entry for GOV-2 §1a's
  rewrite inventory** (*"this list is the M5 spec author's rewrite inventory"*). The in-kind ruling
  makes purchase expressible IN PRINCIPLE (grain, land, office), **but designing it is M5's work
  and not a workaround to add at M4.**

- **T4.1d — THE TWO-PATH DISCREPANCY IS RESOLVED: A REPORTING ERROR, NOT A DETERMINISM FINDING
  (`docs/t4.1d-review-record.md`).** World hashes from the in-test harness and the CLI replay are
  **BIT-IDENTICAL at turns 1, 100 and 650** on seed 3 with no orders (`8fe17719…`, `9ee7b1e6…`,
  `e7b93234…`), and class counts match exactly. **Law 5 holds.**
  **CAUSE: `sim.json` has `id 1 = Peasants`, `id 2 = Artisans`, and T4.1's probe counted
  `Class.Value == 1`** — it reported PEASANT counts as artisan counts. That is exactly why the
  "no-order" arm showed 121–665 present from turn 1: peasants are endowed at founding, artisans
  are not.
  **WITHDRAWN: T4.1's "both M3 artisan inheritances are ORDER-CONDITIONAL".** Its discriminator
  compared a name-matched with-orders reading against a wrong-class no-orders reading — **two
  different classes, not two order regimes.**
  **UNAFFECTED and still standing:** T4.1's chain finding (read `Variables.ArtisanShare` and good
  stocks), T4.1's Q1 bind ratio, T4.1b's discriminator table, and the M4 spec's collapse
  measurement — all read by registry NAME or by fields other than the class id.
  **RULE TO ADD TO THE REGISTER: when a probe indexes a registry by RAW ID, print the registry's
  NAME for that id on the same line.** An off-by-one id yields a plausible series rather than an
  error — peasant counts look exactly like artisan counts — so nothing fails and the number is
  simply about a different thing. The replay reporter was immune because it matches on NAME, which
  is the only reason the disagreement surfaced at all. Third instance in the family with the
  sweep-capture and operation-looks-successful lines: **the failure is in what the instrument could
  distinguish.**
- **T4.14 — EXCLUDED CANDIDATE, DO NOT RE-TEST: FOUNDING POPULATION DOES NOT PREDICT THE
  EMERGENCE MODE.** The artisan emergence split is bimodal (ten at exactly 1; Nuhem 27,
  Naethaehun 36; nothing between 1 and 27). **The obvious hypothesis — bigger settlements emerge
  with more artisans — is REFUTED by the director's own chronicle:** Nuhem is the largest founding
  (663 souls) and lands high, but **Mothian is the SECOND largest (583) and lands at 1**, while
  **Naethaehun is mid-pack (380) and lands at 36**. Evidence:
  `docs/session-logs/chronicle-20260807-145349.txt`. **Whatever splits the two modes is not
  founding population.** Recorded as an excluded candidate so it is not re-tested.
- **METHOD LESSON (T4.1, recorded by director ruling): THE CHEAPER INSTRUMENT WAS ALREADY IN
  HAND.** Inheritance B was specified as a per-turn replay scan of the director's order log,
  because the log was assumed to be the only record of emergence. It was not: **the CHRONICLE is
  the emergence record** — it logs the first-emergence event and its count directly, which is
  exactly the quantity in question — so the reading needed no replay at all, and the `.bin` that
  blocked it was never required for this question. **Before building an instrument, check whether
  an existing artifact already records the quantity.** Same family as the sweep-capture line and
  the operation-looks-successful line: the failure is in what the method could SEE, not in the
  measurement.

- **T4.1b — SPACING DERIVED (~95 km), AND A MEASUREMENT THAT CHANGES THE RULING'S JUSTIFICATION
  (`docs/t4.1b-review-record.md`).** Direction was ruled Option B (spacing DOWN); the NUMBER was
  derived, not supplied. **M1 CONFIRMS the inconsistency:** at `minSpacingKm = 480` hex packing
  allots **199,532 km²** per settlement while the catchment works **7,854 km² — 3.94 %**, and
  ~95 % of measured habitable land (5,373,616 km², fertility ≥ 0.10) is never touched.
  **M2 derives s = r√(π/0.866) = 95.2 km** (catchments tangent gives 100 km) — **above the whole
  reference band** (Sumerian 30–50, Athens–Corinth ~80), because the 50 km catchment radius is
  itself generous; radius is out of scope. **M4 CONFIRMS the ~32 km stride floor does NOT bind**
  (derived spacing = 5.95 nodes; the floor is on RADIUS, unchanged) — the director's earlier
  belief that it blocked Option B is confirmed wrong.
  **THE FINDING: NO SINGLE FIXED VALUE SATISFIES BOTH CHARTER ENDPOINTS.** Measured saturation
  (5 seeds) — 480 km → **40/46/52**, 143.8 km → 206/254/294, 95.2 km → **398/457/582**, 88.1 km →
  458/515/650. **The shipped 480 already hits the Charter's "~50 (ancient)" at a median of 46**;
  the late target of 300–800 needs 88–144 km. Those are 2.5–4× apart and `minSpacingKm` is ONE
  constant. **The director's "a fixed spacing constant cannot be right across 6,000 years" is now
  a MEASUREMENT, not an intuition.** Derived 95.2 km implies ~457 settlements, ~9× the ancient
  target — reported and NOT rounded toward a comfortable count (CR-003 §5.1). Direction survives;
  justification changes: 480 is not wrong, it is correct **for the ancient endpoint only**. Three
  shapes offered to the director, none chosen. **No constant moved; the ADR amending D-025 comes
  after the ruling.**
- **OPEN DESIGN ITEM, NO OWNER — SPACING SHOULD DERIVE FROM COMPUTED STATE (law 4).** Population
  pressure and land quality, not a fixed constant. T4.1b's M3+M5 is its supporting measurement:
  it is not merely nicer, it is **the only shape that satisfies both Charter endpoints**.
- **PLAYABILITY COUPLING — WHY THE SPACING CHANGE SHOULD BE TAKEN IN TWO STEPS (director's
  reasoning, recorded).** The Charter's 300–800 is not reachable as a PLAY EXPERIENCE until
  **M5's delegation** exists: sector mixes are set per settlement by hand — workable at 12,
  tedious at 46, impossible at 300+. **And T4.16's clone work should land before the world grows**
  — the price solver is O(S²·G²) and buckets already collide with their ratified ~150k cap at
  Charter scale. Both argue the first step should be modest and the second taken deliberately.

- **THE DIRECTOR'S SESSION LOG REPLAYED (T4.1b; `docs/session-logs/orders-20260807-145349.bin`,
  main `3185a6b`; now GUARDED by `OrderLogFixtureTests`, which its filename resolution did not
  reach).** Four measurements:
  **1. THE BIMODAL-EMERGENCE DISCRIMINATOR IS DECISIVE — AND KILLS THE OBVIOUS HYPOTHESIS.** Only
  TWO settlements were ever ruled: **s11 = Mothian** and **s2 = Kunaetho**. **Both emerged at
  EXACTLY 1.** The two high-mode settlements — **Nuhem (42) and Naethaehun (58)** — received **no
  order at all**. So **the split is NOT a per-settlement response to being ordered.** It NARROWS
  T4.1's order-conditional finding rather than contradicting it: a no-order world still has
  artisans from turn 1 with no emergence event, so **the effect is WORLD-LEVEL** — two orders
  anywhere change the emergence regime everywhere. Remaining candidates: world-aggregate couplings
  (prices, trade, class-mobility shares). **Owner T4.14.**
  **2. THE ARTISAN COLLAPSE DOES NOT OCCUR IN THE DIRECTOR'S WORLD** — 0 of 12 settlements at zero
  artisans at t650 (finals 103–3,083). The 6-of-12 extinction measured earlier was on the **shipped
  M2-era held-exit log**, so it is specific to THAT labour schedule, not to labour orders in
  general and not to the director's play. **T4.14's subject narrows again.**
  **3. THE THIATHIARIATH OSCILLATION DOES NOT REPRODUCE.** Population share over 650 turns:
  range 4.64–7.89 %, **largest turn-to-turn swing 0.89 percentage points**. Against the reported
  13/24/24/10/10 %, this is not the same quantity — either a different observable (absolute
  population, a per-class share, a UI panel measuring something else) or a different run. Two
  independent replays now agree with each other and disagree with the reported figures.
  **Measured, not explained.**
  **4. KUNAETHO'S GRIEVANCE IS COMFORT-DRIVEN.** Grievance 10.4 → 23.6 → plateau ~20, while
  **Sustenance is pinned at 0.910 and Shelter at 1.000 for the ENTIRE campaign** and **Comfort
  falls 0.713 → 0.413, tracking grievance inversely throughout.** Kunaetho is a RULED settlement
  whose crafting share was set to 10 and never raised. **This is Q5 (Comfort is flow-bound) in a
  played session.**
  **ALSO: the log stores RAW slider values (sums 100/134/129/128/179), as D-032 intends** — raw
  weights, consumers normalise via `Sectors.Share`; replay and UI share that one path, so they
  cannot diverge. **AND: replay vs chronicle differs by ONE TURN on two emergences** (Kunaetho
  320 vs 330, Vurun 470 vs 480) **and systematically on counts** (Nuhem 42 vs 27, Naethaehun 58 vs
  36) — consistent with the chronicle counting adults only ("masters and hands") and logging at a
  different point in the turn. Unresolved; does not affect the 10/2 mode structure, which
  reproduces exactly.

- **T4.8 follow-up — three items the FUTURE NOTABLE SPAWNER must own** (raised by the independent
  certification review of `t4.8-notables`, none reachable while no system creates notables):
  1. **`NotableId` uniqueness is not enforced.** Calling `NotableLifecycle.Born` twice with the same
     id yields two LIVING rows sharing one identity. Conservation still holds (they are two distinct
     people), but `LivingRowOf` silently returns the first. Whoever ships the spawner must own id
     allocation or this becomes a silent identity collision.
  2. **Row growth is unbounded.** Every defection appends a row permanently — vacated rows are never
     reclaimed, correctly, since deleting would shift indices under a table other rows may reference.
     Over a 6,000-year run a frequently-defecting population grows the table monotonically and
     `LivingRowOf` is an O(n) scan over it.
  3. **`Dies` reuses `ReasonIds.Deaths`**, so "how many NOTABLES died" is not answerable from the
     ledger — notable and bucket deaths share one reason. Defensible (it is the same event, and the
     audit balances either way) and recorded as a design call for the director rather than inherited
     silently. Changing it later is a schema-visible flow-table change.
- **T4.5 follow-up — D-037 B3's OTHER half: worldgen does not place pastoralists.**
  T4.5 ships the non-state subsistence/appropriation mechanism and tests it, but the raid
  requires a HERDING-DOMINANT stateless settlement and the never-ordered default mix
  (`Sectors.Default`: Farming 0.55, Herding 0.15, …) is farming-dominant. Measured over
  300 turns of the canonical founded world: ZERO herding-dominant settlement-turns, so the
  appropriation path is DORMANT in live worlds today. D-037 B3 says non-state peoples are
  "M4 worldgen, present from turn zero — NOT spawned"; placing them is worldgen work that
  T4.5's authorized design did not cover (it forbids schema changes and does not mention
  worldgen). Whoever owns that should read `docs/t4.5-review-record.md` first.
- **T4.5 review finding — the raid responds to the BASKET, not to the year.**
  `needs.json` is grain-dominant (grain 0.9 / livestock 0.06 / fish 0.04) and a surplus in one
  good does not cover a shortfall in another, so a herding-dominant settlement is short by
  roughly the grain share in EVERY year while its livestock output sits far above the 0.06 it
  needs. Its `DeficitRatio` is therefore a near-constant of the basket, and appropriation
  becomes a self-correcting alternation (take, eat, be fed, take again) that owes nothing to
  the weather. Measured with the real pipeline, weather 1.0 everywhere: a pure pastoralist
  alternates deficit 0.94 / 0.00 forever. D-037 B3's "the same bad year that starves villages
  sends herders after grain" therefore holds for MIXED settlements (through farming's
  pre-existing weather multiplier) but not for pastoralists. Closing that gap needs diet
  substitution or a pastoralist grain trade — a needs/D-018 question, not an appropriation one.
- **T4.5 review finding — dt seam at the one-turn lag (law 3, bounded).**
  `AppropriationSystem` takes `DeficitRatio x DemandUnits` from PREV. `DemandUnits` is already
  dt-integrated where it is published, so the take scales linearly with dt in steady state
  (measured 3000 at dt=10 vs 1500 at dt=5, ratio exactly 2). But the PREV row was integrated
  under the PREVIOUS turn's dt, so on the single turn where era pacing steps dt (10 -> 5 -> 3
  -> 2 -> 1 -> 0.5) a raider takes old_dt/new_dt times the shortfall of the turn it is in —
  2x at the first boundary. One turn per band, non-compounding. Every fix widens a serialized
  row (publish a per-year demand rate, or carry the previous dt), which T4.5's design forbids;
  whoever next opens `ConsumptionDeficitRow` should fix it in the same edit.
- **PR #4 (AI CONSTITUTION) — four §23 citations do not resolve against the tree.**
  The document's own §23 closes with *"Before implementation, the coder must verify these references
  against the current repository. If this document conflicts with ratified architecture, the tree and
  ratified documents win and the conflict is a finding."* Verified at `87fb866`; these are the findings,
  filed here rather than by editing the author's prose:
  1. **"AI symmetry … difficulty should be information and decision quality"** — the principle exists but
     is named simply **Symmetry** (Spine principle 7, `docs/civ-sim-architecture-v3-outline.md:25`), and it
     reads *"Difficulty = information and **friction**, never hidden resources."* The document renders
     "friction" as "decision quality" in both §7 and §23. Not cosmetic: *friction* names a ratified,
     world-side, player-symmetric lever (D-039 is titled COMMAND FRICTION), whereas *decision quality* is
     the document's own §6 AI-side competence concept. If the document means to ADD competence as a
     difficulty lever, that is a proposal and needs to be labelled as one, not folded into a restatement
     of a frozen principle.
  2. **"Emergent systems"** — no principle, law or document of that name exists (0 hits). Its gloss also
     restates the Symmetry bullet two lines above it.
  3. **"Information systems"** — no item of that name exists (0 hits). The mechanisms are real but titled
     `docs/d039-command-fog-and-siege.md` and `docs/d040-discovery-and-control.md`.
  4. **"Strategic dt … the authoritative strategic timestep unless a sanctioned crisis layer exists"** —
     no "strategic dt", "strategic timestep" or "crisis layer" exists (0 hits each). The nearest ratified
     machinery is S3's **dt authority rule** and **crisis zoom** (`civ-sim-architecture-v3-outline.md:34`),
     but that rule governs WHICH polity sets the global dt — it is not a layered strategic/tactical
     timestep, which is what the bullet asserts.
  Whoever writes the M5 AI spec must reconcile §23 against the tree before implementing from it.

- **M5 ownership of Research, Technology & Institutions — CR-005 OPEN, not a queue item.**
  Recorded here only as a pointer so the queue reader is not surprised. The director directed
  that M5 own a Research/Technology/Institutions architecture packet; that conflicts with the
  frozen milestone order (Spine: M5 governing loop, M6 knowledge & diffusion, M7 institutions)
  and with `m4-spec.md`'s five by-name deferrals of money to M5. Conflict and three options are
  in `docs/adr/cr-005-m5-research-technology-institutions-placement.md`; scope is recorded in
  `docs/m5-research-technology-institutions-placeholder.md`. **Awaiting director ruling — this is
  NOT parked to the M10 slice gate.**

- **M5 temporal control / player agency — CR-006 OPEN, not a queue item.**
  Pointer only. The agreed model (continuous simulation under turn-shaped player control,
  projections, policies, calculated hand-backs, player triggers) is recorded in
  `docs/m5-temporal-control-and-player-agency-placeholder.md`. It raises TWO conflicts with
  frozen material, both in `docs/adr/cr-006-continuous-time-and-campaign-epoch.md`: mid-turn
  hand-backs vs the atomic turn of kernel contract §3.2-3.4, and a 10,000 BCE epoch vs ADR-002
  (4000 BCE = day 0) and CLAUDE.md's "spanning 6,000 years". **Awaiting director ruling — NOT
  parked to the M10 slice gate.** Variable turn duration by era is ALREADY SATISFIED by
  `EraTable` and needs no change.

- **Milestone roadmap dependency audit — pointer only, decisions belong to the director.**
  `docs/m5-roadmap-dependency-audit.md`. Two ratified findings reframe the reordering question:
  D-011 §6 already resequenced the ladder (battle layer inserted as M6, knowledge to M7, politics
  to M8) and D-040 B3 already ruled "NO TECHNOLOGY UNLOCK — LAW 4 BINDS", rejecting the Civ
  research-to-unlock shape in favour of capability emerging from computed preconditions. The audit
  recommends keeping the ratified order, landing a small capability-predicate SEAM early as the
  anti-retrofit device, and re-scoping M7 from "knowledge & divergence" to the capability layer.
  **Awaiting director ruling on Q1 (whether an accumulating science stock is compatible with
  D-040 B3 at all) — that question blocks the rest.** NOT parked to the M10 slice gate.

- **Capability architecture decision record — pointer only; five director decisions inside.**
  `docs/capability-architecture-decision.md`. Supersedes parts of
  `docs/m5-roadmap-dependency-audit.md` (see its §12, which records three errors in that document
  rather than silently fixing them). Headlines: the capability seam ALREADY SHIPS (D-020 predicate
  DSL, two live consumers — class emergence and recipe `requires`, documented in goods.json:3 as
  "a knowledge gate over published variables, never a calendar date"); a per-CIVILIZATION
  capability is NOT representable (no PolityRow, no table, no constructor outside deserialization);
  and GOV-2 §1a rules money is NOT at M5, so money is currently UNOWNED and "money is M5" in
  m4-spec is transcription drift. **The Q1 adversarial verification DID NOT RUN (session limit) —
  §4's recommendation is UNVERIFIED under ADR-015 §6.** NOT parked to the M10 slice gate.
