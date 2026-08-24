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
is **procyclical**. When population falls, capacity falls with it in the same
turn, and the surplus that would have fed the survivors is destroyed as overflow.

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
