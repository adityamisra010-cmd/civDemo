# FOOD ANOMALY — OBSERVABILITY AND ROOT-CAUSE INVESTIGATION

**Worktree pinned to `main` `87fb866`.** Investigation and observability only. No
mechanism, equation, constant, band, quarantine, golden or calibration value was
changed. **Nothing is certified and nothing is merged.**

Reported symptom: aggregate food > 0 at turn 47, **exactly 0** at turn 48, > 0
again at turn 49, with population and farming output positive throughout.

---

## §1 PHASE 1 — WHAT IS ACTUALLY LOGGED, AND WHEN

Traced by execution path, not by name.

### 1.1 The only food aggregate in the tree

`Sim.Core/Kernel/ReplayReport.cs:77-84` — and it is the **only** one. There is no
other `food` aggregate in `Sim.Cli`, the chronicle, the hash log or the bench:

```csharp
long totalFood = 0;
int grain = cfg.Goods.GrainId;
for (int i = 0; i < w.GoodStocks.Count; i++)
    if (w.GoodStocks[i].Good.Value == grain) totalFood += w.GoodStocks[i].Amount.Value;
```

**Three properties of that number, each of which matters:**

1. **It is END-OF-TURN stock.** `WriteTurn` is called with the post-step world,
   after all thirteen systems. `TurnExecutor.Step` (`TurnExecutor.cs:86-92`) runs
   the whole pipeline and returns; nothing samples state in between. Before this
   packet **no intra-turn state was observable at all** — the turn was atomic to
   every observer in the tree.
2. **It is GRAIN ONLY**, not "food". Livestock (2) and fish (3) are food goods in
   the basket and are *not* in this sum.
3. **It is a bare sum over rows.** A settlement with no grain row and a settlement
   holding zero are indistinguishable in it.

### 1.2 Every writer of grain, exhaustively

Established by grepping every `GoodStocks` holder in `SystemCatalog` and reading
each one's ledger calls. Pipeline order (`Sim.Data/content/pipeline.json`) shown;
grain is the numeraire, good id 1, conserved quantity 101.

| # | phase | touches grain? | how |
|---|---|---|---|
| 1 | `catchment` | no | |
| 2 | `harvestweather` | no | sets the yield multiplier only |
| 3 | `production` | **YES** | `Ledger.Flow(..., ReasonIds.Harvest, Source)` — `ProductionSystem.cs:244` |
| 4 | `appropriation` | **YES** | `Ledger.Transfer` settlement→settlement; **net zero in aggregate** |
| 5 | `consumption` | **YES** | `Eaten` (Sink), then `Spoilage` (Sink), then `GranaryOverflow` (Sink) |
| 6 | `price` | no | reads PREV |
| 7 | `trade` | **no** | grain short-circuited at `TradeArbitrageSystem.cs:137` (D-033 numeraire) |
| 8 | `housing` | no | timber and clay only |
| 9–13 | `classmobility`, `migration`, `demographics`, `needsgrievance`, `pathbuild` | no | read PREV |

Grain is **not an input to any recipe** (`goods.json` — no recipe lists grain),
so `InputsConsumed` never touches it. The complete grain vocabulary is therefore
**one source (`Harvest`), three sinks (`Eaten`, `Spoilage`, `GranaryOverflow`),
one internal transfer (appropriation)** — and that is what makes a closed audit
possible.

---

## §2 PHASES 2–4 — WHAT WAS BUILT

Three pieces. Two are pure observers; the third is a default-implemented
interface method that no production path reaches.

### 2.1 Intra-turn phase checkpoints — `TurnExecutor.cs`

`ITurnObserver` gains

```csharp
void OnPhaseState(string phase, IReadOnlyWorldState next) { }
```

called from the existing `Observe` local function after each phase. **The
parameter is `IReadOnlyWorldState`, so an observer cannot write.** The body is
empty by default, so the two existing implementors (`BenchObserver`,
`PhaseTotals`) are untouched. `observer` is null on every canonical, golden, CLI,
replay and UI path, so **neither this nor `OnPhase` is reached there at all** —
which is why the goldens are unmoved (§5).

The first checkpoint is `"clone"` — Next before any system has run, i.e. true
beginning-of-turn state.

### 2.2 The food conservation audit — `Sim.Core/Kernel/FoodAudit.cs` (new)

**It required no new bookkeeping**, and that is the point. `Ledger.Flow` already
writes a running `TotalSourced`/`TotalSunk` per `(quantity, reason)` into
`WorldState.LedgerFlows` — serialized, cloned, present in every snapshot — while
`Ledger.Transfer` writes no row because it conserves by construction. So the
identity

    stock(end) − stock(start) = Σ Δsourced − Σ Δsunk

is computable from state that already exists, by differencing two snapshots.
`FoodTurnAccount.Residual` is that identity's error term, in `long`, **with no
epsilon**. Reasons are indexed by id rather than enumerated, and a grain flow
carrying an id beyond the audit's capacity **throws** — the accounting is allowed
to fail loudly, never to lose a term quietly.

### 2.3 The harness — `Sim.Tests/Kernel/FoodAnomalyInvestigation.cs` (new)

Runs the canonical founded world (`HeadlessFounding.Found`, production
`pipeline.json`) and emits the per-turn closed accounting, per-phase grain stock
inside turns 45–51, and the per-settlement split at every phase. It **asserts
conservation** and nothing else — the rest is diagnostic output.

---

## §3 THE REQUIRED ACCOUNTING — IT RECONCILES EXACTLY

**60 turns, seed 42, residual `0` on every single turn.** No epsilon; these are
`long`s. Grain is never created or destroyed outside the Ledger, no row is
dropped, no settlement takes a store with it. `OtherSourced` and `OtherSunk` are
`0` on all 60 turns, confirming §1.2's claim that the reason vocabulary is
complete.

**So the anomaly is NOT a conservation defect.** That result is what makes the
rest of this document a statement about *mechanism* rather than about a leak.

Representative rows (`start + harvest − eaten − spoilage − granary = end`):

| turn | pop | start | +harvest | −eaten | −spoilage | −granary | = end | residual |
|---|---|---|---|---|---|---|---|---|
| 45 | 5475 | 5976 | 130710 | 40203 | 53125 | 37335 | 6023 | **0** |
| 46 | 5509 | 6023 | 90576 | 40474 | 30898 | 19161 | 6066 | **0** |
| 47 | 5564 | 6066 | 74441 | 40701 | 21916 | 11792 | 6098 | **0** |
| 48 | 5597 | 6098 | **62185** | 41091 | 14969 | 6766 | **5457** | **0** |
| 49 | 5652 | 5457 | 83930 | 41388 | 26426 | 15371 | 6202 | **0** |

---

## §4 ROOT CAUSE

### 4.1 The reported state does not occur in the canonical 12-settlement world

Seed 42 never reaches zero: end-of-turn grain sits between 4,354 and 6,712 for
all 60 turns. **The mechanism that produces the reported flicker is nonetheless
fully visible in it, and is measured below.** A world with few settlements — a
played or single-settlement session — has no cross-settlement averaging, and the
same mechanism drives the aggregate all the way to exactly 0.

### 4.2 Finding 1 — the end-of-turn food number is not a stock. It is the granary capacity.

`ConsumptionSystem.BoundStore` (`ConsumptionSystem.cs:283-296`) caps the store at

    capacity = WholeUnits(GranaryYearsOfDemand × annualGrainDemand),  GranaryYearsOfDemand = 1.5

recomputed **every turn from that turn's own demand**. Measured
`end ÷ capacity` over 60 turns:

- **47 of 60 turns: 0.999.** The store is at capacity, to rounding.
- The 13 exceptions are all *low-harvest* turns, and the shortfall is monotone in
  the harvest: turn 48 → 0.885 (harvest 62,185), turn 54 → 0.854 (52,687),
  turn 56 → 0.846 (57,902).

**Consequence:** `totalFood` is a function of current *population*, not of past
harvests. It carries no history. A settlement cannot accumulate famine insurance,
because the size of its buffer is defined by how hungry it is right now — the cap
is **procyclical**. When population falls, capacity falls with it, and the surplus
that would have fed the survivors is destroyed as overflow.

> **CORRECTION (added with §7.6, not silently rewritten).** This paragraph
> originally read "capacity falls with it **in the same turn**". That is wrong.
> Consumption reads `prev.Buckets` and sits at pipeline slot 5, while population
> is mutated at slots 10 (migration) and 11 (demographics), so capacity in turn N
> uses turn N−1's population. **The procyclical chain is real but carries two
> turns of lag, not instantaneous.** The claim is weaker than first stated; the
> loop's sign is unchanged.

### 4.3 Finding 2 — 53.4% of all grain ever harvested is destroyed before it can be eaten

Summed over 60 turns: harvest **4,549,017**; destroyed by spoilage + granary
overflow **2,430,293** = **53.4%**. Per-turn peak **69.2%** (turn 45).

Two independent destroyers, both correctly ledgered:

- **Spoilage** is `1 − exp(−0.08 × dt)`. At the canonical **dt = 10 sim-years**
  that is **55.07% of the store, in one turn.**
- **Granary overflow** removes everything above 1.5 years of demand.

### 4.4 Finding 3 — the store is dimensionally too small for the turn it must bridge, and this is a conflict between two ratified items

This is the root cause, and it is arithmetic, not judgement.

- The granary holds **1.5 years** of demand (T4.2 B-2, `granaryYearsOfDemand`).
- One canonical turn consumes **10 years** of demand (era dt).
- **A completely full granary therefore covers 15% of a single turn's
  consumption.** Measured on turn 48: store 6,098 against a turn's consumption of
  41,091 — **14.8%**, exactly as predicted.

Now read what the harvest-variance model states it is relying on
(`sim.json`, `harvestVariance._doc`, CR-003 ruling 3):

> "correlationTimeYears: e-folding memory (3.0 — THE parameter that makes
> MULTI-YEAR DROUGHTS possible, which the ruling requires: consecutive failures
> are what kill, **against stores that survive one bad year**)."

**CR-003 ruling 3 requires stores that survive one bad year. T4.2's granary cap
delivers stores that survive 0.15 of one turn.** The buffer that the
harvest-variance design explicitly depends on is destroyed by the storage cap
before it can be drawn on, and the two were derived against different time
units — the cap in *years*, the turn in *decades*.

That is why a single bad harvest turn appears immediately as near-zero (or, with
one settlement, exactly zero) food, and why the next normal turn restores it in
full: nothing was carried in, and nothing is carried out.

### 4.5 Classification

Against the packet's buckets, the anomaly is **not** a logging artefact alone and
**not** a conservation break:

- **Conservation (a leak):** ruled out — residual 0 on 60/60 turns.
- **Ordering/timing artefact:** partially present — the only observable was
  end-of-turn, which hides that the store is refilled and re-emptied *within*
  every turn (production takes settlement 0 from 651 → 8,172, consumption returns
  it to 651). Fixed by §2.1, but it is not the cause.
- **Mechanism defect — the operative one:** a **dimensional mismatch between the
  granary capacity (1.5 years) and the turn length (10 years)**, coupled to a
  **procyclical cap** that destroys the buffer exactly when it is needed. Both
  halves are in ratified, frozen material.

---

## §5 VERIFICATION

- `dotnet build`: clean, **0 warnings, 0 errors**.
- Conservation assertion: **passes**, 60/60 turns, residual exactly 0.
- The production diff is **one default-implemented interface method and its call
  site**, both unreachable when `observer` is null — which is every canonical,
  golden, CLI, replay and UI path.

### 5.1 ATTRIBUTION — branch vs `main`, both suites run to completion

Both suites were run to completion, **sequentially against the same starting
commit** (`main` `87fb866`), one per worktree.

| | total | passed | **failed** | skipped |
|---|---|---|---|---|
| `main` `87fb866` | 497 | 484 | **7** | 6 |
| `food-anomaly-observability` | 498 | 485 | **7** | 6 |
| delta | **+1** | **+1** | **0** | **0** |

The `+1` is `FoodAccountingReconcilesEveryTurn_AndTheAnomalyWindowIsDumped`.
**The failing set is identical, test for test:**
`ClassSystemTests.Artisans_EmergeInFedAutoplay_PlateauAtTheCap_DocumentedWindow`,
`ClassSystemTests.Famine_DrainsArtisansBeforePeasantStarvationPeaks`,
`PopulationTests.ProductionPipeline_PerPhaseBench_Reported`,
`CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed: 1, 2)`,
`CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7, 42)`.
All seven are pre-existing certified quarantine reds. **No test was altered,
weakened, re-banded or deleted, and no quarantine status was touched.**

**Attribution was not settled on pass/fail parity — the calibration NUMBERS were
compared.** Extracting every assertion message from both runs and diffing them:

```
canonical.densityPerArableKm2: 0.479659 has fallen below the recorded deviation window ...
canonical.densityPerArableKm2: 0.564256 has fallen below the recorded deviation window ...
seed 42: 6 starvation deaths — the dev world is no longer pre-Malthusian ...
seed  7: 2 starvation deaths — the dev world is no longer pre-Malthusian ...
CR-003 QUARANTINE RESOLVED — "the artisan share drained post-boom (min 0.032)" ...
```

**Identical to six significant figures in both runs.** The only differences
anywhere in the two logs are wall-clock timings, worktree paths, and .NET
reflection stub names — none of which are simulation state. The instrumentation
branch is therefore **numerically inert**, not merely pass/fail-neutral.

### 5.2 INTEGRITY — the whole production diff

```
 Sim.Core/Kernel/FoodAudit.cs      | 180 +++++   (new; referenced by NO production code)
 Sim.Core/Kernel/TurnExecutor.cs   |  17 +       (one default method + one call site)
```

`git diff --name-only 87fb866 -- Sim.Data/ .github/ '*golden*' '*.json'` is
**empty**: no data file, no CI file, no golden, no TUNE value, no band, no
quarantine, no frozen document was changed. The `TurnExecutor` call site sits
inside the `Observe` local function, which returns immediately when `observer is
null` — every canonical, golden, CLI, replay and UI path. Gates:
`check-banned-constructs` **OK**, `check-read-isolation` **OK**,
`check-readonly-proof` **OK**.

---

## §6 WHAT I DID NOT DO

Per the packet and per CLAUDE.md governance:

- No food buffer added; no consumption, farming, mortality or migration change;
  nothing done to prevent food reaching zero; no clamp, constant or smoothing
  touched.
- No golden re-pinned, no band or quarantine altered, no calibration value moved.
- **No fix applied.** §4.4 is a genuine conflict between frozen items
  (CR-003 ruling 3's stated premise vs T4.2 B-2's granary cap), which CLAUDE.md
  routes to a change request and a director ruling, not to an agent's edit. It is
  written up as `docs/adr/cr-004-granary-turn-length-mismatch.md`.
- The observability work is committed **separately** from any eventual
  behavioural fix, as instructed.
- Nothing is certified. Nothing is merged. **No independent reviewer ran.**

---

## §7 SOURCE-LEVEL REVIEW (E) — NONLINEAR INTERACTIONS

A read-only review hunted for interactions that dimensional analysis cannot see.
**Every claim recorded below was re-verified by me directly against source before
being written here** (ADR-015 §6: a finding is not actionable on a finder's word).
Claims I did not independently confirm are not in this section.

### 7.1 CANDIDATE IMPLEMENTATION DEFECT — the capacity guard tests the wrong quantity

`ConsumptionSystem.cs:284-296`:

```csharp
if (cfg.GranaryYearsOfDemand > 0.0 && annualGrainDemand > 0.0)
{
    long capacity = ConservedMath.WholeUnits(
        cfg.GranaryYearsOfDemand * annualGrainDemand, ...);
    long over = row.Amount.Value - capacity;
    if (over > 0) { ...GranaryOverflow, ClampToAvailable... }
}
```

**The guard tests `annualGrainDemand > 0`, never `capacity > 0`.** And
`ConservedMath.WholeUnits` **floors** — `return (long)Math.Floor(exact);`
(`ConservedMath.cs:53`). So when `1.5 × annualGrainDemand < 1.0`, capacity is
**0**, `over` becomes the whole stock, and **the settlement's entire grain store
is destroyed as `GranaryOverflow` in one turn.**

Reachability, computed from shipped data (`needs.json` grain lines
`perPersonYear` 0.9 and 0.78; `sim.json` cohort weights 0.6–1.0):
capacity is 0 whenever cohort-weighted demand < 0.667 person-year-equivalents.
One person of class 2 in a child cohort gives `0.78 × 0.6 = 0.468`, so
`floor(1.5 × 0.468) = floor(0.702) = 0` — **the whole store goes.**

**The asymmetry is the tell:** a settlement at *exactly zero* population has
`annualGrainDemand == 0`, fails the guard, and keeps its grain indefinitely
(spoiling only); a settlement down to its *last person* is stripped completely.
A nearly-dead settlement is treated more harshly than a dead one.

This is **a distinct question from the CR-004 design conflict** and is a
candidate for classification **F (implementation defect)**. Whether it is
reachable in canonical worlds is an empirical question — canonical settlements
hold hundreds of people, so capacity there is in the hundreds and this branch is
never taken. **Not fixed, per instruction. Reported only.**

### 7.2 DOCUMENTATION DEFECT IN A RATIFIED HEADER

`ConsumptionSystem.cs:253` states: *"`WholeUnits` rounds, so a store small enough
that its annual spoilage is under half a unit does not spoil at all."*
**`WholeUnits` floors** (`ConservedMath.cs:53`). The threshold is therefore one
whole unit, not half, and the bias direction differs from the one documented:
capacity floors **down** (destroying more), spoilage floors **down** (destroying
less). T4.2's own header misdescribes its own arithmetic.

### 7.3 CONFIRMED — every grain-stock reader sees a POST-DESTRUCTION number

`TurnExecutor.cs:96` clones Prev once; every system reads that fully-committed
state. Because `BoundStore` is the last thing consumption does to the grain row,
**every `prev.GoodStocks[...].Amount` read anywhere in the pipeline is
post-Eaten, post-Spoilage, post-GranaryOverflow.**

The sharpest consequence is in migration (`MigrationSystem.cs:141`, `:154-155`):

```csharp
resources[s] = m.AttractivenessFoodWeight * food + m.AttractivenessLandWeight * arableKm2;
instant[s]   = resources[s] / Math.Max(pop, 1);
```

At steady state the store is pinned at capacity, which is proportional to
population, so the **food half of `instant` degenerates to a constant** and stops
discriminating between well-fed and badly-fed settlements. Only the land term
carries information.

### 7.4 IMPORTANT NEGATIVE — mortality is NOT contaminated by the cap

`DemographicsSystem.cs:128-132` reads **only**
`prev.ConsumptionDeficits[...].DeficitRatio` — a flow ratio computed from the
nutritional requirement, never the grain stock. **Starvation is therefore driven
by what people failed to eat, not by the capped store.** The cap does not
directly drive deaths. This matters: it rules out the most alarming version of
the anomaly.

### 7.5 CONFIRMED — capacity depends on the LIVESTOCK AND FISH supply

`ConsumptionSystem.cs:201-202` passes
`annualGrainDemand: (exactDemand[grain] + nonStapleShortfall) / dt`.
`nonStapleShortfall` is the unmet demand for the *other* food goods
(`ConsumptionSystem.cs:165`). So a settlement whose fishery fails gets a
**larger** grain granary, and one whose fishery recovers gets its granary
**shrunk with the difference destroyed as overflow.** A cross-good coupling that
no header describes.

### 7.6 THE FEEDBACK LOOP, WITH ITS SIGN AND ITS LAG

population(N−1) → grain demand(N) (`ConsumptionSystem.cs:132`, `:140`)
→ capacity(N) (`:201`, `:286`) → overflow sink(N) (`:289-295`)
→ smaller carry-in(N+1) → deficit(N+1) (`:185-187`)
→ starvation (`DemographicsSystem.cs:139`, `:207`) → smaller population.

**Sign: positive (amplifying) on the downswing** — a shrinking settlement is
permitted a shrinking buffer. **But it carries two turns of lag**: consumption
reads `prev.Buckets`, and population is mutated only later in the pipeline
(migration slot 10, demographics slot 11, against consumption at slot 5), so
capacity cannot shrink in the *same* turn a population falls. It shrinks the
turn after. The procyclical chain is therefore **real but lagged, not
instantaneous** — which is weaker than §4.4 of this document originally implied.

### 7.7 NOT FOUND, stated plainly

- No grain consumer is sequenced after `BoundStore` — appropriation is pipeline
  slot 4, before consumption at slot 5, so raided grain is edible the same turn.
- No same-turn population change ahead of the capacity computation (see 7.6).
- No dt-boundary defect in `ConsumeRemainder`/`ProduceRemainder` (sub-unit
  fractions, not rates), in the harvest AR(1) state (stationary at σ² for every
  dt), or in migration's smoothing window (denominated in years).
- **No units bug anywhere.** This is the third independent pass to reach that
  conclusion.

---

## §8 THE 40-SEED EXACT-ZERO SWEEP (B) — THE REPORTED STATE IS NOT REPRODUCIBLE

Seeds 1–40 × 120 turns = **4,800 turn-events**. Grain is a `long`; zero means
exactly 0 and **no epsilon is used anywhere**. Raw data:
`docs/food-evidence-40seed-sweep.md`.

| measurement | value |
|---|---|
| seeds with aggregate grain exactly 0 while population > 0 | **0 / 40** |
| turn-events with aggregate grain exactly 0 and pop > 0 | **0 / 4800** |
| turns with zero population (a different fact, counted apart) | **0** |
| global minimum aggregate grain | **2602** (seed 24, turn 11) |
| conservation failures | **0 / 4800** |

**The reported symptom — aggregate food exactly 0 at one turn with positive
population — does not occur in any canonical world in 4,800 measured turns.**

### 8.1 The five outcomes, kept distinct as required

- **(a) zero grain with positive population — 0 at the aggregate**, but it *does*
  occur **per settlement**: 87 turn-events had at least one of 12 settlements at
  exactly 0 (max 7, seed 16). Every settlement had a grain row at all times, so
  "no row" never masqueraded as "zero".
- **(b) zero population — 0 occurrences.** End populations 6,131–10,969.
- **(c) merely low grain** — the minimum, 2602, against a same-turn consumption
  of 19,906: about 13% of one turn's eating. Low, but never zero.
- **(d) low harvest (harvest < eating) — 40 of 4800 turns**, and in every seed it
  is **turn 1 only**, the founding turn. From turn 2 on, harvest exceeded
  consumption on every turn of every seed.
- **(e) capacity-limited — 4800 of 4800.** Granary overflow was strictly positive
  on **every turn of every seed**. The store is **not scarcity-limited at any
  point**; it is pinned against the granary ceiling on literally every measured
  turn.

**(e) is the finding.** The world is storage-limited, not food-limited.

## §9 THE CONTROLLED dt EXPERIMENT (C) — THE TEMPORAL-RESOLUTION HYPOTHESIS IS LARGELY KILLED

300 sim-years at **identical sim-year horizon** for every dt, seeds 42/7/13,
only dt varied. Conservation residual exactly 0 at every dt. Raw data:
`docs/food-evidence-dt-experiment.md`.

**Spoilage is exactly dt-invariant, proven arithmetically:** `survival^(1/dt)` =
`0.923116346387` at every dt ∈ {10, 5, 3, 2, 1, 0.5}, matching `exp(−0.08)` to
twelve digits. **Spoilage is not a bug.**

| dt | 10 | 5 | 3 | 2 | 1 | 0.5 |
|---|---|---|---|---|---|---|
| store in **YEARS** of consumption | 1.634 | 1.659 | 1.664 | 1.666 | 1.656 | 1.664 |
| store in **turns** of consumption | 0.163 | 0.332 | 0.555 | 0.833 | 1.656 | 3.328 |
| destroyed ÷ harvest (per turn) | 0.546 | 0.553 | 0.550 | 0.557 | 0.562 | 0.555 |
| **population at year 300** | **5116** | **5122** | **5141** | **5152** | **5165** | **5151** |
| grain stock at year 300 | 5584 | 5671 | 5699 | 5709 | 5693 | — |
| deficit events per settlement-**year** | 0.00074 | 0.00028 | **0** | **0** | **0** | **0** |
| exact-zero turns | 0 | 0 | 0 | 0 | 0 | 0 |

**Answer to "does changing dt alone change qualitative food-storage behaviour?"
— essentially NO.** Store in years, grain stock at equal sim-years, destroyed
fraction of harvest, and **population trajectory (1% spread)** are all
dt-invariant. The store-in-turns row is the mechanical `1.5/dt` framing and
carries no behavioural content.

**The one genuinely dt-sensitive quantity is deficit incidence**, which falls
from 0.00074 per settlement-year at dt=10 to 0 at dt ≤ 3. That is a real
discretization effect — at dt=10 one weather draw governs a decade, so a bad
draw records a decade-long shortfall as one event — but **it does not propagate
to population**, which is invariant. It is a rare-event count (4/360, 1/360,
3/360 at dt=10), not a change in the storage regime.

> **CORRECTION — I reported the opposite from a pilot run, and it was wrong.**
> A 50-sim-year pilot showed population 4472 at dt=10 rising to 5300 at dt=0.5
> (+18.5%) and I reported that as a material dt-sensitivity. **The 300-year run
> refutes it**: 5116 → 5151, a 1% spread with no monotone trend. 50 sim-years is
> only **5 turns** at dt=10, far too short to support a population claim. The
> earlier figure was a short-horizon artefact and is withdrawn.

**Refining dt would not rescue the buffer**: the destroyed fraction of harvest is
~0.55 at *every* dt. Only its composition shifts — spoilage-dominated at dt=10
(739,454 vs 457,123 overflow), overflow-dominated at dt=0.5 (159,917 vs
1,051,357). The grain is destroyed either way.

## §10 PROPAGATION TRACE (D) — WHERE THE OUTCOME BECOMES INEVITABLE

Intra-turn phase attribution, identical in all nine traced turns (seed 24, t11):

```
clone            2843
production      37918   delta +35075   <-- the ONLY inflow
appropriation   37918   delta 0        <-- pure internal transfer, confirmed
consumption      2602   delta -35316   <-- eaten + spoilage + overflow at once
price..pathbuild 2602   delta 0        <-- eight phases, grain bit-identical
```

**Two phases move grain; eleven do not.** The low-food outcome is **created in
`production` and realised in `consumption`, within the same turn** — not carried
in from the previous turn. What distinguishes a low turn is *only* the harvest:

| seed | turn | harvest before → at → after | eating (near-flat) | end stock |
|---|---|---|---|---|
| 42 | 8 | 59,261 → **52,929** → 65,060 | 30,185 / 30,433 / 30,610 | 4501 → **4354** → 4586 |
| 24 | 11 | 40,376 → **35,075** → 41,886 | 19,841 / 19,906 / 20,061 | 2843 → **2602** → 3004 |
| 16 | 25 | 101,255 → **51,566** → 77,706 | 34,692 / 33,784 / 35,244 | 5199 → **3159** → 5281 |

The previous turn's stock is nearly irrelevant — it contributes 2,843 units to a
turn whose throughput is 35,075. **The carry-over conveys no information from a
good turn into a bad one**, because the store ends at ~99.9% of the ceiling
regardless of how large the preceding harvest was (seed 16 turn 24 harvested
101,255 and destroyed 39,362 to spoilage plus 26,933 to overflow, ending at
0.9991 of capacity).

**Why it never reaches exactly zero:** even on the worst traced turn, harvest
(51,566) exceeded eating (33,784) by 1.53×.

### 10.1 The procyclical chain, measured

| | P(next-turn capacity falls \| population fell) | baseline P(… \| population rose) |
|---|---|---|
| pooled, 3 seeds | **11/11 = 1.0000** | **0/342 = 0.0000** |

The **demand → capacity** link is confirmed exactly, with no exception in 353
pairs. **But two honest limits:** the antecedent is rare (11 events in 354
turns — these worlds grow almost monotonically), and the chain's *final* link,
"buffer weakens", is **not supported**: the buffer is already saturated
(overflow positive on 100% of turns), so no additional weakening is observable.
In seed 16 the observed direction is **food → population** (the −113 fall at
turn 26 *follows* the low-food turn 25), not population → food.

---

## §11 FINAL CLASSIFICATION — AND A CORRECTION THAT FALSIFIES §4.4

### 11.1 THE CORRECTION: CR-004's CENTRAL CONFLICT IS FALSIFIED

**§4.4 of this document claimed a conflict between two frozen items** — that
CR-003 ruling 3 requires "stores that survive one bad year" while T4.2's cap
delivers "0.15 of one turn". **That claim is WRONG and is withdrawn.**

It compared a **years-denominated store** against a **turn-denominated
consumption** — the identical category error that produced the already-withdrawn
"denominate the granary in turns" recommendation. Measured in the unit the
premise actually uses:

| dt | 10 | 5 | 3 | 2 | 1 | 0.5 |
|---|---|---|---|---|---|---|
| store in **YEARS** of consumption | **1.634** | 1.659 | 1.664 | 1.666 | 1.656 | 1.664 |

Independently confirmed from the seed-42 per-turn table in §3: turn 48 holds
6,098 against an annual consumption of 41,091/10 = 4,109, i.e. **1.484 years**.

**The store survives one bad year — it holds roughly 1.6 of them, at every dt.
The design premise is SATISFIED, not violated. There is no conflict between
CR-003 ruling 3 and T4.2's granary cap.** CR-004's §1 is therefore falsified and
the CR is withdrawn on its stated grounds (see the CR itself, which records this
rather than deleting it).

What survives from §4.2–4.3 is descriptive and remains true: the store is pinned
at the granary ceiling on **4,800 of 4,800** measured turns, and ~55% of every
harvest is destroyed by spoilage plus overflow at **every** dt. But "the buffer
holds 1.6 years and production routinely exceeds what 1.6 years can hold" is
**not a defect** — it is what a bounded store does when production outruns
storage. It required measurement to tell those apart, and the measurement went
against my earlier reading.

### 11.2 CLASSIFICATION AGAINST THE REQUIRED CATEGORIES

| # | category | verdict |
|---|---|---|
| A | Accounting bug | **NO** — 5,160 turn-accounts, residual exactly 0, no epsilon |
| B | Dimensional/unit bug | **NO** — four independent passes; every rate per-year, every dt conversion correct |
| C | Temporal discretization interaction | **MARGINAL** — only deficit incidence is dt-sensitive (0.00074 → 0 per settlement-year); store, stock, destroyed fraction and **population are all dt-invariant** |
| D | Emergent behaviour consistent with the ratified model | **YES — this is the primary verdict** for the observed food dynamics |
| E | Ratified design conflict | **NO — falsified in §11.1** |
| F | Genuine implementation bug | **YES, one, unrelated to the reported symptom** — §7.1, the capacity guard |
| G | Still unresolved | **YES, for the REPORTED OBSERVATION itself** — 0/4800, not reproduced |

### 11.3 WHAT IS PROVEN

1. Grain accounting closes exactly — 5,160 turn-accounts, residual 0, `long`, no epsilon.
2. The reported state (aggregate grain exactly 0, population > 0) **does not occur** in 4,800 canonical turn-events. Global minimum 2,602.
3. The world is **storage-limited, never food-limited**: overflow positive on 4,800/4,800 turns; harvest exceeded eating on 4,760/4,800 (the 40 exceptions are each seed's founding turn).
4. Spoilage is exactly dt-invariant (`survival^(1/dt)` = 0.923116346387 at every dt) — **not a bug**.
5. The store holds **~1.6 years** of consumption at every dt — the design premise is met.
6. Low-food turns are created in `production` and realised in `consumption`, within one turn; eleven of thirteen phases move no grain.
7. The demand → capacity link is exact (11/11, baseline 0/342), but its downstream "buffer weakens" link is **not** supported — the buffer is already saturated.
8. **One implementation defect** (§7.1) and **one documentation defect** (§7.2), both reported, neither fixed.

### 11.4 WHAT IS NOT PROVEN / REMAINS UNCERTAIN

1. **The origin of the reported turn-47/48/49 observation is unknown.** It is not reproducible in any canonical world. Candidate explanations, none confirmed: a world with far fewer settlements (no cross-settlement averaging); a different aggregate than `ReplayReport.totalFood`; or the §7.1 capacity-floor defect in a settlement down to its last person. **Resolving it requires the seed, settlement count and log source of the original run.**
2. Whether the §7.1 defect is reachable in any *played* world — it is unreachable in canonical ones (capacity there is in the hundreds).
3. Whether ~55% harvest destruction is the intended equilibrium. It is consistent with the mechanisms as ratified, but no ratified document states a target.
4. The dt-sensitivity of deficit incidence rests on rare events (4/360, 1/360, 3/360 at dt=10).

### 11.5 RECOMMENDED NEXT ACTION

1. **Obtain the original run's seed, settlement count and log source.** Without it the reported anomaly cannot be closed, and every canonical measurement says it did not happen.
2. **Director ruling on the §7.1 capacity guard** — a one-line guard change (`capacity > 0`) is the candidate remedy, but it is a production equation and is out of an investigating agent's authority.
3. **Correct the `ConsumptionSystem.cs:253` header** ("rounds" → "floors") — documentation only.
4. **No change to the granary constant, the spoilage rate, dt, harvest variance, any band or any golden is warranted by this evidence.**
