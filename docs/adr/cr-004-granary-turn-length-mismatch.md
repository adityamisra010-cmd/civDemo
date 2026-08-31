# CR-004 — THE GRANARY CAP AND THE TURN LENGTH ARE DENOMINATED IN DIFFERENT UNITS

**Status: WITHDRAWN BY ITS OWN EVIDENCE. No code change proposed or applied.**
**Retained in full, not deleted — the record of a falsified hypothesis is the
point.** See §0 first; §1–§4 below are the ORIGINAL text and its premise no
longer holds.

## §0 WITHDRAWAL — THE CENTRAL CONFLICT IS FALSIFIED

This CR asserted (§1) a conflict between CR-003 ruling 3's stated premise —
*"stores that survive one bad year"* — and T4.2's granary cap, on the evidence
(§2 E1) that a full granary covers only 15% of one turn's consumption.

**That comparison was invalid.** It measured a **years-denominated store**
against a **turn-denominated consumption** — the same category error that
produced the already-withdrawn Option A (§2A). Measured in the unit the premise
actually uses, over 300 sim-years at every dt, three seeds:

| dt | 10 | 5 | 3 | 2 | 1 | 0.5 |
|---|---|---|---|---|---|---|
| store in **YEARS** of consumption | **1.634** | 1.659 | 1.664 | 1.666 | 1.656 | 1.664 |

Independently confirmed from the seed-42 turn table: 6,098 held against an
annual consumption of 4,109 = **1.484 years**.

**The store holds ~1.6 years. It survives one bad year. The premise is met and
there is no conflict between the two frozen items.** E1's "15%" was arithmetically
correct and analytically meaningless.

**What survives as fact, and is NOT a defect:** the store is pinned at the
granary ceiling on 4,800 of 4,800 measured turns, and ~55% of every harvest is
destroyed by spoilage plus overflow at *every* dt (composition shifts from
spoilage-dominated at dt=10 to overflow-dominated at dt=0.5; the total does not).
That is what a bounded store does when production outruns storage.

**What replaces this CR:** nothing at the design level. Two smaller items, both
recorded in `docs/food-anomaly-investigation.md` §7 and neither fixed:
a candidate **implementation defect** (the granary guard tests
`annualGrainDemand > 0` but never `capacity > 0`, so a floored capacity of 0
destroys an entire store — `ConsumptionSystem.cs:284`), and a **documentation
defect** (`ConsumptionSystem.cs:253` says `WholeUnits` rounds; it floors).

**Do not reinstate this CR's conflict claim, and do not reinstate Option A.**
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

## §2A CORRECTION — A WITHDRAWN RECOMMENDATION (recorded, not erased)

**The earlier recommendation to denominate granary capacity in TURNS was WRONG
and is WITHDRAWN. Capacity is correctly denominated in YEARS.**

What was originally hypothesised: that the granary is "dimensionally too small
for the turn it must bridge", and that Option A — recasting capacity as
`granaryTurnsOfDemand × turnDemand` — was the fix.

What falsified it: the era table does not hold dt at 10. It steps dt down
10 → 5 → 3 → 2 → 1 → 0.5 across the campaign, so capacity ÷ turn-consumption is
`1.5/dt` **turns**:

| dt (years/turn) | 10 | 5 | 3 | 2 | 1 | 0.5 |
|---|---|---|---|---|---|---|
| capacity in TURNS of consumption | 0.15 | 0.30 | 0.50 | 0.75 | **1.50** | **3.00** |

Denominating storage in turns would give Neolithic granaries holding **15 years**
of grain — for which no reference class exists — and Modern granaries holding
half a year, inverting the physical intent of T4.2's derivation. The years
denomination is the correct one, and `1.5/dt` is a *framing* of a fixed physical
store, not evidence of a behavioural defect on its own.

**What survives the correction:** the conflict in §1 is real, but it is located
at the TURN LENGTH and its interaction with sub-turn processes, not at the
granary constant. **Do not silently restore the withdrawn recommendation.**

## §3 OPTIONS (≤3, minimal, NOT recommendations to implement without a ruling)

**Option A — WITHDRAWN, see §2A.** Retained here only so the record shows what
was rejected and why. It must not be reinstated without new evidence.

**Option B — leave the cap and accept that storage is not a buffer at this
resolution**, recording it as an explicit modelling limit and removing the "stores
that survive one bad year" premise from (2)'s doc so the two stop contradicting.
Blast radius: documentation only; no goldens move. Costs the drought mechanism
its stated teeth.

**Option C — make the cap counter-cyclical**, sizing capacity from a smoothed or
peak historical demand rather than current demand, so a population crash does not
destroy the surplus in the same turn. Addresses E4 but not E1/E2 — the buffer is
still too small for the turn — and it adds serialized per-settlement state.

**Option D — reduce dt in the coarse bands so the timestep can resolve the
processes the design depends on** (dt ≤ tau = 3 years, i.e. the Neolithic band's
10 stops being 3.3× the AR(1) correlation time). This is the only option that
addresses the located cause rather than a symptom. Blast radius is the largest of
all: turn counts explode, era pacing is a frozen item, every golden re-pins, and
the 6,000-year campaign length is a Spine commitment.

## §4 BLAST RADIUS

All four goldens re-pin under C and D (grain stock enters the world hash every
turn). Migration's food-attractiveness term and its famine-flight term read grain
stock and `LastProducedUnits`, so T4.2's migration corridor and the calibration
bands that depend on it would need re-measurement — the granary cap was already
measured as the dominant term in that corridor. Prices read stock through
`MarketScale`. Option B moves nothing.

## §5 RECOMMENDATION — DEFERRED PENDING THE OUTSTANDING EXPERIMENT

**No option is recommended yet, and Option A is withdrawn (§2A).**

The dimensional analysis is clean: every rate is per-year, every dt conversion is
correct, and spoilage, `rho` and capacity are each independently dt-invariant.
**There is no units error** — the earlier claim of one is retracted. That leaves
the open question narrow and testable:

> Does changing temporal resolution ALONE change the qualitative food-storage
> behaviour, when compared on a per-sim-year basis?

A controlled experiment holding all annual parameters fixed and varying only dt
across {10, 5, 3, 2, 1, 0.5} is the discriminator. **Until it reports:**

- if per-year behaviour is invariant and only the per-turn framing moves, the
  temporal-resolution hypothesis is **killed**, this CR reduces to Option B
  (a documentation reconciliation of (2)'s premise), and the classification is
  **emergent behaviour consistent with the ratified model**;
- if per-year behaviour genuinely changes with dt, the classification is a
  **temporal discretization interaction**, and the choice is between B and D.

**No option is implemented. Awaiting the experiment, then a ruling.**

**No option is implemented. Awaiting ruling.**
