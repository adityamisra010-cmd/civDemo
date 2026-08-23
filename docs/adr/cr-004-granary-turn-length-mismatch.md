# CR-004 — THE GRANARY CAP AND THE TURN LENGTH ARE DENOMINATED IN DIFFERENT UNITS

**Status: OPEN — awaiting director ruling. No code change proposed or applied.**
Raised from the food-anomaly investigation; full measurement in
`docs/food-anomaly-investigation.md`. Worktree pinned to `main` `87fb866`.

---

## §1 THE FROZEN ITEMS IN CONFLICT

1. **T4.2 B-2a store bounding** — `granaryYearsOfDemand = 1.5`, derived and
   stated in `docs/t4.2-manifest.md` before the world was run, with the reference
   class "an agrarian household carried on the order of one to a few years of its
   own consumption". Denominated in **years**.

2. **CR-003 ruling 3 / T3.4b harvest variance** — `correlationTimeYears = 3.0`,
   whose stated purpose in `sim.json` is *"THE parameter that makes MULTI-YEAR
   DROUGHTS possible, which the ruling requires: consecutive failures are what
   kill, **against stores that survive one bad year**."* Its premise is a store
   that survives one bad year. Also denominated in **years**.

3. **The era pacing table** — the canonical turn is **dt = 10 sim-years**.
   Denominated in **years per turn**.

(1) and (2) are consistent with each other in years. Neither is consistent with
(3): the *resolution* at which scarcity is evaluated is a decade, but both
storage parameters were reasoned about at the scale of a single year.

## §2 EVIDENCE

Canonical founded world, seed 42, 60 turns, production pipeline. Instrumentation
is a pure observer; residual is `long` with no epsilon.

**E1 — the buffer is 15% of what it must bridge.** A full granary holds 1.5 years
of demand; one turn consumes 10 years of demand. Measured turn 48: store 6,098
against a turn's consumption of 41,091 = **14.8%**. A completely full granary
cannot cover a sixth of one turn.

**E2 — 53.4% of all grain harvested is destroyed before it can be eaten.**
Harvest 4,549,017; spoilage + granary overflow 2,430,293. Per-turn peak 69.2%.
Spoilage alone is `1 − exp(−0.08 × 10) = 55.07%` of the store per canonical turn —
the parameter was derived as an *annual* rate against a reference class of annual
storage.

**E3 — end-of-turn food is the capacity, not a stock.** `end ÷ capacity` is
**0.999 on 47 of 60 turns**. The stock carries no history; it is re-derived from
current demand every turn. The 13 exceptions are all low-harvest turns and the
shortfall is monotone in the harvest (turn 48 → 0.885 at harvest 62,185;
turn 54 → 0.854 at 52,687; turn 56 → 0.846 at 57,902).

**E4 — the cap is procyclical.** Capacity is a multiple of *current* demand, so
when population falls the buffer shrinks in the same turn and the surplus that
would have fed the survivors is destroyed as `GranaryOverflow`. Storage cannot
act as famine insurance, which is the function the reference class in (1) was
chosen for.

**E5 — conservation is intact.** Residual exactly 0 on 60/60 turns;
`OtherSourced` and `OtherSunk` 0 throughout. **This is a mechanism conflict, not
a leak** — which is why it is a CR and not a defect report.

**Consequence.** A single bad harvest turn shows up immediately as near-zero
food, and the next normal turn restores it fully. Multi-year droughts cannot
"kill through consecutive failures" as (2) intends, because there is no store to
exhaust — the first bad turn already finds the granary holding a sixth of a
turn's food. The reported turn-47/48/49 flicker is this, at a settlement count
low enough that no cross-settlement averaging hides it.

## §3 OPTIONS (≤3, minimal, NOT recommendations to implement without a ruling)

**Option A — denominate the granary in turns, not years.**
Capacity becomes `granaryTurnsOfDemand × turnDemand`. Restores (2)'s premise
directly: a store that survives one bad *turn* is what a decade-resolution sim
needs. Blast radius: one expression in `BoundStore`; all goldens move; T4.2's
manifest reference class must be re-derived, since "1.5 years" was the derived
figure and "1.5 turns" is not the same claim.

**Option B — leave the cap and accept that storage is not a buffer at this
resolution**, recording it as an explicit modelling limit and removing the "stores
that survive one bad year" premise from (2)'s doc so the two stop contradicting.
Blast radius: documentation only; no goldens move. Costs the drought mechanism
its stated teeth.

**Option C — make the cap counter-cyclical**, sizing capacity from a smoothed or
peak historical demand rather than current demand, so a population crash does not
destroy the surplus in the same turn. Addresses E4 but not E1/E2 — the buffer is
still too small for the turn — and it adds serialized per-settlement state.

## §4 BLAST RADIUS

All four goldens re-pin under A and C (grain stock enters the world hash every
turn). Migration's food-attractiveness term and its famine-flight term read grain
stock and `LastProducedUnits`, so T4.2's migration corridor and the calibration
bands that depend on it would need re-measurement — the granary cap was already
measured as the dominant term in that corridor. Prices read stock through
`MarketScale`. Option B moves nothing.

## §5 RECOMMENDATION

**Option A, with the T4.2 reference class re-derived rather than rescaled** — the
conflict is a units error, and A is the only option that fixes the units. But
this is a ruling for the director, not a call for an agent: A re-pins every golden
and disturbs a calibration corridor, and B is a legitimate choice if the director
would rather accept the limit than pay that cost.

**No option is implemented. Awaiting ruling.**
