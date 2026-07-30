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
  settled. The art SUBSTRATE (terrain textures, grain overlay, palette, UI
  frames, typography, style bible) has no such dependency and ships separately
  at M2+. Target: after M4 or M5, against the Troy/Humankind stylized
  reference.
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
- **Q3 (T3.5b certification, director) — candidate ADR-015 §7.15, owner T3.10 (alongside M5/M6):
  a pre-committed READING requires a DISCRIMINATING OBSERVABLE.** Root cause of T3.5b's misapplied
  reading (lens 2 F2): density = population/arable is composite, so "the food economy moves" could
  fire for a pure denominator reason while the mechanism under test did nothing. §7.13 requires
  pre-committing the readings; it does not require verifying the observable can DISCRIMINATE
  between them. Second time this shape has bitten — §7.7 was the first (a corridor insensitive to
  its own control parameter). NOT written into ADR-015 in T3.5b, deliberately: all four lenses had
  cleared, and a post-clearance governance addition is the f8a19e1 pattern, now recorded twice.
  T3.10 writes it with a worked example from both instances.
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
  green (at ≥ 0.90 it false-fires instead). OPEN, owner T3.10, alongside candidate §7.15.
- **PATTERN (third instance): ASYMMETRIC-MARGIN THRESHOLDS.** T3.4c Q2 (drift envelope, 11% on
  the must-pass side), now the M5 ceiling (5% on the must-fire side) — thresholds separating a
  measured clean case from a measured mutant, where one side's margin is structurally thin. The
  standing line: a discriminating threshold's WEAKER margin is stated at the point the threshold
  is chosen, not discovered later.
- **T3.6b ESCALATION 1 — THE TRANSPORT DEADBAND EXCEEDS THE PRICE BAND for bulk ≥ 8 at map
  distances.** Measured (docs/t3.6b-review-record.md, Item 0(c), 5 seeds): tin-ore price gaps
  span the ENTIRE band (19.95, floor to ceiling) and still reach only 0.57–0.86 of their
  deadband; threshold = bulk × pathCost × costPerBulkCostUnit ≈ 23–35 for bulk-8 goods at the
  closest pairs, vs a maximum possible gap of BandMax − BandMin = 19.95. Ores and stone are
  STRUCTURALLY untradeable overland at ANY price divergence. With T3.6 R1 this is the sharpened
  trade-silence finding. Every surface involved (price band, costPerBulkCostUnit, bulk table)
  is ruled/frozen — DIRECTOR MATERIAL, no owner assigned here.
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
  costs a scroll. Owner: T3.9b.
- **T3.9a GATE Q2 — THE FIVE SECTOR BARS CLIP THEIR LABELS VERTICALLY.** Row height sits under
  the font's line height, so labels are cut off top and bottom. Owner: T3.9b.
- **T3.9a GATE Q3 — TEXT SIZE INCONSISTENT IN THE SETTLEMENT PANEL.** The "food ... (last
  harvest +N)" line renders at a different size from its neighbours; style bible §3 permits a
  companion face for dense numbers, but the switch must be deliberate and consistent, not
  per-line. Owner: T3.9b.
- **T3.9a GATE Q4 — PANELS NEED TO BE INDIVIDUALLY COLLAPSIBLE WITH SESSION-PERSISTENT STATE.**
  Market, Graphs, Annals and the settlement HUD open together clutter and overlap; the director
  specifically wants Annals closeable for routine play. ImGui already collapses on the title-bar
  arrow — what is missing is persistence and non-overlapping layout. Owner: T3.9b.
- **T3.9a GATE Q5 — COMFORT MAY HAVE THE SAME FLOW-NOT-STOCK PROBLEM AS SHELTER.** Pots and
  cloth are durable; zero crafting for one turn should not zero Comfort. Investigate alongside
  T3.8's housing stock, same reasoning. Owner: T3.8.
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
