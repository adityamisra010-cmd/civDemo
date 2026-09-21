# M4 PLAYTEST FOLLOW-UP — THE TURN-0→3 POPULATION AND FOOD TRANSIENT

**Worktree pinned to `98f25c1` (detached), the commit the session under investigation was played
on (`buildSha` in the manifest). DIAGNOSIS ONLY: no constant, equation, config value, data file,
golden, corridor, band or quarantine was changed. The only file added to the tree is this
document.**

Session under investigation: `session-20260909-131447.json` — seed 42, canonical size, canonical
settlement count, schema v24, 624 recorded turns.

Reported symptom, from the session's own trace:

| turn | year | population | Δpop | food | Δfood | settlements |
|-----:|-----:|-----------:|-----:|-------:|--------:|---:|
| 0 | 0 | 5,140 | — | 82,041 | — | 12 |
| 1 | 10 | 4,330 | −810 (−15.8 %) | 5,752 | −76,289 (−93.0 %) | 12 |
| 2 | 20 | 4,041 | −289 (−6.7 %) | 4,822 | −930 | 12 |
| 3 | 30 | 3,987 | −54 (−1.4 %) | 4,508 | −314 | 12 |
| 4 | 40 | 3,997 | **+10** | 4,451 | −57 | 12 |

**VERDICT: INTENDED INITIALIZATION TRANSIENT, with one part of it a structural warm-up artefact
of a documented convention rather than a designed behaviour.** Both halves are fully attributed
below, to the person and to the grain unit. Nothing in the first four turns is starvation,
housing deprivation, migration, or an unexplained loss. §7 states the one residual ambiguity and
the evidence that would close it — it is an incidental hash observation, not part of this
transient.

> **§7's residual ambiguity has since been RESOLVED and escalated.** The
> `sim inspect` "REPRODUCTION FAILED at turn 2" noted there is real, and it is
> not a replay defect: the same commit run on Linux reproduces the director's
> population, food and settlement counts to the unit while hashing differently
> from turn 2. It is a CROSS-PLATFORM divergence — Windows x64 versus Linux
> x64 — written up as `docs/adr/cr-013-cross-platform-determinism.md`, open for
> a director ruling. It does not touch any figure in this investigation: every
> number below was measured on Linux and matches the director's Windows trace
> exactly.

---

## §1 HOW EVERY NUMBER BELOW WAS OBTAINED

Three instruments, in the order the packet prescribes.

**1a. Shipped instrumentation — session inspect + replay report.**

```
./scripts/bootstrap.sh
dotnet build -c Release Sim.Cli/Sim.Cli.csproj
dotnet run -c Release --no-build --project Sim.Cli -- inspect \
  --manifest <sess>/session-20260909-131447.json --turn 2 --window 3 \
  --report-jsonl <out>/report.jsonl
```

**1b. The Ledger flow table — the attribution instrument.** `Sim.Core/Kernel/Ledger.cs` is the
only mutation channel for conserved stocks, and `LedgerFlowRow(Quantity, Reason, TotalSourced,
TotalSunk)` accumulates every movement under a reason id. The population reasons are
`Births` (5, source), `Deaths` (6, sink) and `Starvation` (7, sink); the grain reasons are
`InitialEndowment` (2), `Harvest` (3), `Eaten` (4), `Spoilage` (14) and `GranaryOverflow` (15)
(`Sim.Core/State/Ids.cs:113-158`). A grep of `ReasonIds.` over `Sim.Core/` confirms these are the
**only** writers of those quantities: population is sunk in exactly two places
(`DemographicsSystem.cs:265-266` and `NotableLifecycle.cs:100`, the latter under the same
`Deaths` id), and grain in exactly the four listed. Migration is a `Ledger.Transfer` and therefore
carries **no** flow row by construction — it is read instead from `MigrationFlowRow(Settlement,
Inflow, Outflow)`, and births/deaths are cross-checked against
`SettlementVitalsRow(Settlement, Births, Deaths, DtYears)`.

`ReplayReport` does not serialize `LedgerFlows`. Rather than expand the shipped reporter or the
public CLI, a **throwaway console harness outside the repository** was built against
`Sim.Core`/`Sim.Data`, founding with `WorldFounding.Found(worldgen.json, sim.json, seed)` and
stepping the production pipeline through `TurnExecutor` — the identical recipe
`Sim.Cli`'s `HeadlessFounding` + `Executor(orders, founded: true)` uses
(`Sim.Cli/Program.cs:83-115, 760-790`). It prints per-turn **first differences** of the flow
table plus population, cohort vector, grain stock, migration flows, deficit ratios, dwellings and
catchment rows. It reads world state and never writes it. **It lives in the scratchpad, not in the
tree, and there is nothing to delete from the repository.**

**1c. Fidelity check — the harness reproduces the played session exactly.** Population and food
from the harness were compared against the director's live trace for all 231 turns replayed:

```
compared 231 turns, 0 mismatches in (population, food)
```

Every number in §2–§5 is therefore a measurement of the world the director actually played, not
of a lookalike. (The `inspect` hash comparison reports a divergence from turn 2 — §7.)

---

## §2 THE POPULATION FALL IS ENTIRELY BIRTHS MINUS DEATHS. IT IS NOT FOOD, HOUSING OR MIGRATION.

Measured first differences of the Ledger flow table, seed 42, the director's own order log:

| turn | Δpop | births (r5) | deaths (r6) | **starvation (r7)** | **migration out** | migration in | mean deficit ratio |
|-----:|-----:|------:|------:|----:|----:|----:|----:|
| 1 | −810 | 1,632 | 2,442 | **0** | **0** | 0 | 0.0000 |
| 2 | −289 | 1,609 | 1,898 | **0** | 523 | 523 | 0.0000 |
| 3 | −54 | 1,636 | 1,690 | **0** | 153 | 153 | 0.0000 |
| 4 | +10 | 1,659 | 1,649 | **0** | 37 | 37 | 0.0000 |

**Δpop = births − deaths, exactly, on every turn** (1,632 − 2,442 = −810; 1,609 − 1,898 = −289;
1,636 − 1,690 = −54; 1,659 − 1,649 = +10). The reconciliation closes with no residual, which is
the conservation law doing its job: there is no other channel for a person to leave the world.

Consequently, and each measured rather than argued:

- **Starvation deaths are exactly zero on turns 1–4.** Reason id 7 never fires. The
  `ConsumptionDeficits` mean is `0.0000` on every one of those turns, so
  `StarvationRate(d, c, deficit)` is identically zero (`DemographicsSystem.cs:139-141`;
  the deficit is a PREV read, and the pre-turn-0 world has no deficit row at all).
- **Migration moves nobody on turn 1** (out = in = 0), and on turns 2–4 it is
  **internal and exactly balanced** (523/523, 153/153, 37/37) — a `Ledger.Transfer` between
  settlements, conserving by construction. Net migration contributes **0** to the world total on
  every turn. The session chronicle agrees independently: its first migration event is dated
  year 20, i.e. turn 2, not turn 1.
- **Housing is in surplus throughout, not deprivation.** Dwellings × `personsPerDwelling` (6.0)
  over population: turn 0 = 0.999 (founded exactly housed, by design —
  `WorldFounding.cs:170-190`), turn 1 = 1.416, turn 2 = 1.517, turn 4 = 1.620. The settlements
  end turn 1 with **42 % more housing capacity than people**, because dwellings were built while
  the population shrank.

**The split is therefore 100 % demographics, 0 % food, 0 % housing, 0 % net migration.**

---

## §3 WHY DEATHS EXCEED BIRTHS: THE FOUNDING AGE STRUCTURE IS NOT THE STATIONARY ONE

The founding endowment is an **initial STOCK, not an equilibrium population.**
`WorldFounding.Found` sources `founding.cohortCounts` per settlement through
`Ledger.Flow(..., ReasonIds.InitialEndowment, ..., Source)` (`WorldFounding.cs:96-101`) — a
16-slot age vector written into the buckets once, with a per-(settlement, slot) uniform jitter of
amplitude `endowmentJitter = 0.69`. Nothing in the founding path solves for, checks against, or
converges to the stationary vector of the shipped vital rates. **It is an endowment, and the
document that ships it says so** — the `_docEndowmentJitter` note in `sim.json` describes the
intent as varying founding **size and composition**, never survival odds.

The shipped rates (`sim.json`, `demographics`) are 16-slot, 5-year cohorts:

```
mortalityPerYear   [.056 .030 .018 .024 .028 .032 .036 .041 .047 .056 .068 .083 .107 .147 .208 .305]
fertilityPerPersonPerYear
                   [ 0    0    0   .052 .1144 .119 .1037 .0778 .0412 .0105 0 0 0 0 0 0 ]
founding cohortCounts
                   [ 46   44   40   30   28   26   24   22   20   18  17 15 30 20 12  8 ]   (Σ = 400)
```

The founding vector puts **70 of every 400 people (17.5 %) in cohorts 12–15, ages 60–79**, where
annual mortality runs 0.107 → 0.305. Applying the shipped rates to the shipped vector — arithmetic
over two config arrays, no simulation involved:

```
config cohortCounts:  CDR = 6.038 %/yr   CBR = 3.268 %/yr   net = −2.771 %/yr   elder share 17.5 %
```

**The founding endowment is, on its own numbers, a population declining at 2.8 %/yr.** The
transient is fully predicted by the two data arrays before any code runs.

Measured cohort vectors from the replayed world confirm the prediction and show it draining
(instantaneous crude rates computed from the **measured** vector × the shipped rate arrays):

| turn | pop | 60+ | 60+ share | CDR %/yr | CBR %/yr | net %/yr |
|-----:|----:|----:|---:|---:|---:|---:|
| 0 | 5,140 | 858 | 16.7 % | 6.123 | 3.456 | **−2.668** |
| 1 | 4,330 | 325 | 7.5 % | 4.822 | 3.736 | −1.087 |
| 2 | 4,041 | 174 | 4.3 % | 4.231 | 4.047 | −0.184 |
| 3 | 3,987 | 121 | 3.0 % | 4.034 | 4.180 | **+0.145** |
| 4 | 3,997 | 111 | 2.8 % | 3.978 | 4.185 | +0.207 |
| 8 | 4,114 | 108 | 2.6 % | 3.953 | 4.189 | +0.236 |
| 40 | 5,265 | 140 | 2.7 % | 3.971 | 4.188 | +0.218 |

The elder band collapses from 858 people to 111 in four turns and then holds at ~2.7 % of the
population — **that steady share, not the founded 16.7 %, is the age structure these rates
support.** The measured net rate crosses zero at turn 3 and settles at +0.22 %/yr, which is where
the trajectory in §5 comes from.

**Per settlement**, the fall is universal, not localized (turn 0 counts from the chronicle, turn 1
from the replay report):

| settlement | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| turn 0 | 459 | 261 | 469 | 380 | 289 | 447 | 238 | 663 | 461 | 532 | 358 | 583 |
| turn 1 | 394 | 228 | 356 | 337 | 233 | 364 | 201 | 592 | 363 | 453 | 315 | 494 |
| Δ % | −14 | −13 | −24 | −11 | −19 | −19 | −16 | −11 | −21 | −15 | −12 | −15 |

Every one of the twelve falls; the spread (−11 % to −24 %) is the founding jitter drawing
different elder shares per site, not a failure at any particular settlement.

---

## §4 THE FOOD FALL: A 21-YEAR ENDOWMENT MEETING A 1.5-YEAR CEILING, PLUS A ONE-TURN ZERO HARVEST

Measured first differences of the grain flow rows, seed 42:

| turn | food (end) | Δfood | harvest (r3) | eaten (r4) | spoilage (r14) | granary overflow (r15) |
|-----:|------:|--------:|---------:|-------:|--------:|---------:|
| 1 | 5,752 | −76,289 | **0** | 38,391 | 24,029 | 13,869 |
| 2 | 4,822 | −930 | 70,598 | 32,186 | 24,313 | 15,029 |
| 3 | 4,508 | −314 | 55,495 | 30,092 | 16,637 | 9,080 |
| 4 | 4,451 | −57 | 60,347 | 29,720 | 19,343 | 11,341 |

Turn 1 reconciles to the unit, in the order `ConsumptionSystem` executes (eat, then spoil,
then cap):

```
82,041 − 38,391 (eaten)   = 43,650
43,650 − 24,029 (spoilage) = 19,621     24,029 / 43,650 = 0.55049
                                        1 − exp(−0.08 × 10) = 0.55067   (floored per settlement)
19,621 − 13,869 (overflow) =  5,752  ==  the trace's turn-1 food, exactly
```

Two distinct causes, and they are not equally interesting.

**4a. The endowment is ~21 years of demand; the granary holds 1.5.** Measured steady-state per
capita grain demand (turn 4): `29,720 eaten / 3,997 people / 10 years = 0.7436 units/person/yr`.
The shipped ceiling is `granaryYearsOfDemand = 1.5`, i.e. **1.115 units/person**. The founding
endowment is `82,041 / 5,140 = 15.96 units/person` = **21.5 years of demand, 14.3× the ceiling.**
`founding.foodStore = 6000` against `Σ cohortCounts = 400` is 15 units/person by construction, so
this is the config's stated endowment, not an emergent quantity. Granary overflow is a **loss
through the Ledger with its own reason id**, exactly as `ConsumptionSystem`'s header says it must
be — the store is not silently clamped. The endowment was always going to be destroyed down to the
ceiling on the first turn it was measured against a ceiling; **the fall is the endowment being far
above what the shipped storage rule permits, and 93 % is what 21.5 years falling to 1.1 years
looks like.**

Confirmation over the full session: `food / population` is **15.96 at turn 0 and then 1.33, 1.19,
1.13, 1.11, 1.11 …** — 1.10 ± 0.01 at turns 20, 50, 100, 200, 300, 400 and 500, against a
predicted ceiling of 1.115. The store sits at its cap for 600 turns.

**4b. Turn 1 harvests exactly zero, and that is a warm-up artefact of the Prev-read convention.**
`ProductionSystem.Farm` reads arable land from **`prev.CatchmentSummaries`**
(`ProductionSystem.cs:200-206`), and `WorldFounding` never writes that table — a grep of
`CatchmentSummaries` over `Sim.Core/Worldgen/` returns nothing, and the harness measures
`CatchmentSummaries.Count = 0` at turn 0 and `12` from turn 1 onward. With `arableKm2 = 0` the
Leontief `min(landSide, laborSide)` is 0, so `ratePerYear = 0` and the turn-1 harvest is zero at
every settlement, on every seed tested. `CatchmentSystem` fills the table **during** turn 1, so
turn 2 harvests normally (70,598).

This is the documented one-turn signal lag (`§3.2`, "signals are Prev-read") applied to a table
that has no founded value — the world eats one full decade before it farms for the first time. It
cost 38,391 units of the 76,289 lost on turn 1 (50 %), against 24,029 spoiled (32 %) and 13,869
capped (18 %). It is a real interaction, it is visible only at founding, and **it caused no
deprivation whatsoever**: the deficit ratio is 0.0000 through it, because the endowment was 21
years deep. Had the endowment been sized near the ceiling instead, a zero-harvest first turn would
have been a famine.

---

## §5 THE TRANSIENT IS SEED-INDEPENDENT, AND THE TRAJECTORY IS STABLE AFTER IT

**Four seeds, headless, no orders** (`seed`, 8 turns each). Turn-1 row:

| seed | pop t0 | pop t1 | Δ % | births | deaths | **starv** | **migOut** | harvest t1 | food t0 | food t1 | food/cap t0 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 42 | 5,140 | 4,330 | −15.8 | 1,632 | 2,442 | **0** | **0** | **0** | 82,041 | 5,752 | 15.96 |
| 7 | 5,190 | 4,429 | −14.7 | 1,647 | 2,408 | **0** | **0** | **0** | 73,942 | 5,687 | 14.25 |
| 123 | 5,150 | 4,488 | −12.9 | 1,705 | 2,367 | **0** | **0** | **0** | 70,918 | 5,667 | 13.77 |
| 2024 | 5,263 | 4,615 | −12.6 | 1,775 | 2,423 | **0** | **0** | **0** | 79,542 | 5,816 | 15.11 |

The transient is present at every seed with the same signature: deaths ≈ 2,400, births ≈ 1,700,
starvation exactly zero, migration exactly zero, harvest exactly zero, food landing within 2 % of
5,700 regardless of where it started. Trough and recovery follow the same shape (seed 7 turns
positive at turn 3; 123 and 2024 at turn 4). Seed 42 with and without the director's order log is
identical for the first eight turns — **the orders are not implicated.**

**After the transient, over the director's full 624 turns:** the minimum population of the whole
session is **3,987, at turn 3** — the trough of this transient is the lowest the world ever goes.
Population then rises monotonically in trend to **32,070 at turn 623** (year 4,365), with food
tracking population at 1.0–1.1 units/capita throughout. The world never returns to the founding
food level, and is not supposed to.

**The later crises are a different phenomenon, and the flow table distinguishes them cleanly.**
The seven largest post-transient drops (turns 85, 127, 151–152, 169, 176, 196, 221; −4 % to −8 %)
all carry the signature turn 1 lacks:

| turn | Δpop | births | deaths | **starvation** | **migOut** | deficit |
|-----:|-----:|------:|------:|-----:|-----:|---:|
| 1 | −810 | 1,632 | 2,442 | **0** | **0** | 0.0000 |
| 85 | −377 | 2,656 | 2,851 | 182 | 793 | 0.0000 (0.0270 prev turn) |
| 127 | −582 | 2,836 | 3,145 | 273 | 1,161 | 0.0233 prev |
| 152 | −407 | 3,110 | 3,290 | 227 | 890 | 0.0365 prev |
| 169 | −678 | 3,115 | 3,479 | **314** | **1,265** | 0.0258 prev |
| 221 | −401 | 3,685 | 3,866 | 220 | 1,295 | 0.0157 prev |

Those are genuine famines: a non-zero consumption deficit on the preceding turn drives non-zero
starvation deaths (reason 7), fertility suppression, and a migration surge out of the hit
settlements — the mechanism working. **Turn 1 has none of those markers.** It is not a famine; it
is an age pyramid correcting itself.

---

## §6 CONCLUSION

**INTENDED INITIALIZATION TRANSIENT.** Both observed movements are the shipped configuration
being integrated for the first time, and neither is a defect in the equations:

1. **Population, turn 0 → 3 (−1,153 people, −22.4 %): an age-structure transient, 100 %
   attributable to births minus deaths, with starvation, net migration and housing deprivation all
   measured at exactly zero.** `founding.cohortCounts` places 17.5 % of the world in the 60+
   cohorts, where the shipped mortality schedule runs 0.107–0.305/yr. That vector implies
   −2.77 %/yr before any code runs; the measured world realises −2.67 %/yr and crosses to
   +0.15 %/yr by turn 3 as the elder band drains from 16.7 % to 3.0 %. The founding population is
   an **initial stock, not an equilibrium population**, and `WorldFounding` neither claims nor
   attempts otherwise.

2. **Food, turn 0 → 1 (−76,289 units, −93 %): a 21.5-year endowment meeting a 1.5-year granary
   ceiling, compounded by a structurally zero first harvest.** Attributed exactly: 38,391 eaten
   (50 %), 24,029 spoiled (32 %), 13,869 granary overflow (18 %), reconciling to the unit. The
   turn-0 endowment is **14.3× above what the economy sustains**; food/capita then holds within
   1 % of the predicted ceiling of 1.115 for 600 turns. This is `granaryYearsOfDemand = 1.5`
   doing exactly what its manifest predicted before it was measured.

**The one component that is an implementation interaction rather than a design intent** is the
zero turn-1 harvest (§4b): `ProductionSystem` reads arable land from PREV `CatchmentSummaries`, and
founding leaves that table empty, so the world eats for ten years before it farms once. It is
**benign under the shipped configuration** — the deficit ratio is 0.0000 through it — and it is
invisible after turn 1. It is recorded here as a measured property of the founding sequence, not
as a defect requiring action.

**Is follow-up warranted?** Not as a fix, and this workstream proposes none. Two lines for
`docs/queue.md`, no more:

- The founding cohort vector is ~6× over-weighted in the 60+ band relative to the stationary
  vector of its own mortality schedule (17.5 % vs a measured 2.7 %). Anyone who wants turn 0 to be
  a playable steady state rather than a stock would start there. It is a **tuning-data** question
  (always permitted) and it changes goldens, so it is a director call, not an agent's.
- The turn-1 zero harvest is safe only because the food endowment is 21 years deep. Any future
  change that brings the endowment near the granary ceiling turns founding into a famine. That
  coupling deserves to be written down before it is discovered by a retune.

---

## §7 THE ONE THING NOT ATTRIBUTED — STATED, NOT GUESSED

`sim inspect` reports, against this session:

```
REPRODUCTION FAILED at turn 2: the session recorded 545ca00bd148…, this replay computed
28e62ffb26fb…. Every turn before it matches, so that turn is where the two diverge.
```

Turn 0 and turn 1 hash identically; from turn 2 the world hash differs. **This does not touch any
number in this document**: the replayed population and food agree with the director's live trace
on all 231 turns compared, exactly, including the entire transient — so whatever differs lives in
a hashed table that is neither population nor grain stock. The played session ran in `Sim.Ui`, the
replay in `Sim.Cli`; the two founding recipes are pinned equivalent by the founding-equivalence
test, and turn 0 matching is consistent with that.

**This investigation did not chase it and does not speculate about it.** The evidence that would
close it: replay to turn 2 in both apps and diff `WorldHash`'s component tables row by row to name
the first disagreeing table, then bisect that table's writing system. That is a separate packet,
and it is a live-vs-replay semantic question of exactly the kind the T1.9 precedent says needs its
own turn-exact pin.
