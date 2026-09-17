# CR-015 — FAMINE IS EXCEPTIONAL: the deficit is a food-balance signal, not a famine; flight and shock integration are unbounded in dt

**Status: RULED (mandate 2026-09-17) — implementation T4.21-1..6 authorized; G1 open.** The
ruling text is the director's 2026-09-17 mandate, recorded verbatim in substance in §6 (the
CR-003 §5 form); the nine explicit amendment lines N1–N9 are §6.2; the orchestrator's decisions
under the mandate's ownership grant are §6.3; **G1 (harvest-weather decade variance) is ESCALATED
in §6.4 and awaits its own director ruling** — nothing in T4.21 depends on it. Recording ADRs:
ADR-024 (food state, effective deficit, disaster shock, schema v25), ADR-025 (bounded migration),
ADR-026 (demographic shock integration, headroom growth cap). Packet spec:
`docs/t4.21-architecture.md`.

**CR number:** 015. `cr-014-craft-input-cap-ignores-the-banked-remainder.md` is the highest on
`main`; `origin/m5-full-build` adds only `cr-008`; no `cr-015..019` exists on any of the 87
branches (`git ls-tree` over every ref, 2026-09-17). No queue line reserves 015 (the 008–010
reservations that trapped CR-012 do not reach it).

**Written against** `claude/civdemo-work-b1z2y4` @ `45046eb` (= `main` `dbef61a` + the unmerged
M4 playtest build `2807155` + the forensic CLI). Every `file:line` was read on that tree.

**This CR walks through the standing fence of `docs/m4-exit-inventory.md` §12** ("DO NOT REOPEN
DURING M5 STARTUP … without a fresh ruling", `:300`). The director's 2026-09-17 mandate IS that
fresh ruling for consumption-deficit semantics, demographics and migration; §6.2 N7 records exactly
which §12 bullets are superseded and which stand, so the fence is visibly walked through rather
than silently bypassed. **CR-003 §8.5 is reopened here EXPLICITLY** (N7), not hidden.

---

## 1. FROZEN ITEMS IN CONFLICT (S8 §3 field 1)

Each item quotes the frozen text verbatim with its locus, states what the redesign needs, and names
the amendment line (§6.2) that resolves it. Items are ordered as the amendments are numbered.

### N1 — "no famine trigger, threshold or event that fires independently of the food balance"

- `docs/m3-spec.md:52` (T3.4b, directed packet, CR-003 ruling): *"Famine must EMERGE from bad
  years against thin stores: no famine trigger, threshold or event that fires independently of the
  food balance."*
- `docs/adr/cr-003.md:280-283` (§5.2(b), the ruling): *"the multiplier acts on OUTPUT, never on
  the derived 26.0; famine emerges from the food balance, never from a trigger"*.
- `Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:21-28`: *"FAMINE IS NOT TRIGGERED HERE, AND
  CANNOT BE. This system computes exactly one thing: a mean-one multiplier on realised farm output.
  It has no threshold, no event, no famine flag, and no read of population, stores or deficits. A
  bad year is a small number multiplying a harvest; whether that becomes famine is decided
  downstream by the food balance alone — thin stores plus consecutive bad years. That is the
  ruling's constraint ('no famine trigger, threshold or event that fires independently of the food
  balance') satisfied by CONSTRUCTION rather than by discipline."*
- **Conflict:** the redesign introduces (i) a derived four-state ladder — a THRESHOLD (`a`) and a
  CAUSE qualifier on the FAMINE arm — and (ii) a `DisasterSystem` whose serialized row is an EVENT.
  Both keep `d > 0` necessary, so neither fires independently of the food balance; but the T3.4b
  wording bans a "threshold or event" by name, and the honest course is to amend the clause, not
  to argue that a cause-gated threshold is not a threshold. Weather's own "FAMINE IS NOT TRIGGERED
  HERE" stays literally true.

### N2 — CR-012 §5: "No demographic architecture change of any kind is authorized"

- `docs/adr/cr-012-malthus-architecture.md:142-158`: *"RULING: KEEP. … Specifically ratified as
  established fact, not merely this ADR's argument: Deficit → fertility/mortality is structurally
  sound. The mechanism is linear and dt-robust. … NOT AUTHORIZED by this ruling: any simplification
  or replacement of Demographics or Consumption. T4.2 remains frozen and is not reopened by this
  ruling or by anything in this ADR. No demographic architecture change of any kind is
  authorized."* `:162`: *"STATUS: RULED — KEEP. CR-012 closed."*
- Code it protects: `Sim.Core/Systems/Demographics/DemographicsSystem.cs:136` `suppression =
  Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * deficit)`; `:139` `starveRate[c] =
  StarvationRate(d, c, deficit)`; `:273-278` `return d.StarvationMortalityMaxPerYear * deficit *
  multiplier;`; `:181` `if (deficit == 0.0 && unsuppressed > 0.0)` (the rebound release gate).
- **Conflict:** the redesign changes the ARGUMENT of the starvation hazard and of fertility
  suppression (`d_eff`, ADR-024 §3.2 / G3(b)) and adds a headroom fertility multiplier inside the
  micro-loop (ADR-026). Each is a demographic architecture change in CR-012's words. CR-012 was
  ruled on evidence in which `DeficitRatio` was *"IDENTICALLY 1.0 [sufficiency] … exactly 0
  everywhere"* (`docs/t4.10-review-record.md:280`); it never observed the regime §2 measures.

### N3 — the population-cap semantics "on which no ruling exists"

- `docs/t4.20-food-semantics.md:178-179`: *"`FoodSupportedPopulation` and `FoodConstrainedGrowth`
  were **not** added: both presuppose a population-cap semantics on which no ruling exists."*
- `docs/queue.md:1358-1359`: *"T4.20 — deferred: `FoodSupportedPopulation` / `FoodConstrainedGrowth`
  were NOT added. Both presuppose a population-cap semantics no director ruling establishes. Needs
  the ruling first."*
- `docs/milestones.md:324-326`: *"No food-supported population cap — the measurement found every
  shortfall to be a production shock rather than growth overshoot, so a cap would have had nothing
  to correct."*
- **Conflict:** the mandate's §28 requires that under constant conditions population approaches
  the limit set by food INFLUX asymptotically and never overshoots into deficit; that is a
  population-cap semantics, and the tree records that none has been ruled.

### N4 — ADR-012's Exit valve "bounded by the overdraw scaler alone"; T2.8's "pinned empirically"

- `docs/adr/adr-012-destination-viability.md:53-63` (D-021 Exit-valve preservation): *"flight
  desire remains source-driven (`FamineFlightFactor × deficit_source`), uncapped by the gap
  mechanism, exactly as D-021 ratified. Viability only redistributes WHERE the fleeing go — toward
  destinations that can feed them. When every reachable destination is itself non-viable, flight
  goes to zero: there is no exodus without a destination; people die at home instead of circulating
  between ruins."*
- `Sim.Core/Systems/Migration/MigrationSystem.cs:44-51`: *"famine flight stays gap-INDEPENDENT
  (D-021: starving people leave for anywhere reachable AND VIABLE) and is deliberately NOT
  gap-capped: the Exit valve is a surge by design, bounded by the overdraw scaler alone."*
- `MigrationSystem.cs:93-95` (T2.8 stabilization (a)): *"(Multiple sources can share one
  destination; with f well below 1 and the ascending-pair execution order the collective inflow
  stays inside the basin — pinned empirically by the oscillation regression tests.)"*
- **Conflict:** the live form sums the flight desire over destinations (`:400-402`), so the total
  leaving a source scales with the NUMBER of reachable viable destinations (Libur: Σ damping·viability
  = 4.38, §2) and a profile-1 bucket at `d = 1` desires 2.4× its count per decade PER destination —
  bounded only by the overdraw scaler. The mandate requires bounded per-turn outflow by exact
  integration, HOW MANY separated from WHERE, and a bounded destination aggregate with a semantic.
  "Bounded by the overdraw scaler alone" and "only redistributes WHERE" cannot survive; "source-
  driven", "gap-independent" and "die at home" do. The T2.8 fan-in assumption was an empirical pin
  on N = 12, unproven in exactly the many-sources-one-destination case the mandate names.

### N5 — T4.12 §2c STOP: "a change to a ratified mechanism's strength"

- `docs/t4.12-derivation-record.md:426-428`: *"Famine flight is the D-021 Exit valve — the
  mechanism by which starving people leave. Weakening it 12.8× is a change to a ratified
  mechanism's strength, not a change of units. STOP condition 2 / Phase 1D."*
- `:654-655`: *"`FamineFlightFactor` is a ratified mechanism and no change to it is proposed."*
- `:768-773` (RULING): *"BaseRatePerYear: SEMANTICALLY UNDERSTOOD · NUMERICALLY UNDERIVED · CURRENT
  VALUE 0.03 RETAINED AS HISTORICAL TUNE · NO REPLACEMENT VALUE AUTHORIZED"*; `:775-780`: *"three
  constants in the migration rate sector (`BaseRatePerYear`, `AttractivenessLandWeight`,
  `FamineFlightFactor`) are all 'chosen, never derived', and they carry one gauge freedom between
  them."* — a director decision, not a packet's.
- **Conflict:** the redesign moves NO constant (0.03 × 8.0 = 0.24/yr stays the hazard `K`), but the
  exact form `1 − exp(−profile·K·ω·d·dt)` saturates where the Euler form did not, divides by
  destination count (ω ≤ 1 instead of Σ_j), and is a change of the mechanism's effective STRENGTH in
  T4.12 §2c's exact sense. T4.12's STOP was a packet-level stop awaiting a director decision; it
  must be discharged by name or the next verifier fires it against T4.21.

### N6 — ADR-021's readout value; T4.4 §D2 "it can be emptied, and that is ratified"

- `docs/adr/adr-021-unplaced-departure-demand.md:34`: `UnplacedDeparture` — *"this turn's
  departure demand for this bucket that found NO reachable, viable destination"*; `:70-74`: *"
  `MigrationSystem`'s equations, flows, caps, gates, EMA, attractiveness terms, pair selection,
  remainder handling, overdraw discipline and pipeline position are untouched."*; `:98-101`: *"the
  readout's condition stays binary (no reachable, viable destination at all). Broadening B1 to be
  land-driven, and reinterpreting the graded land-gap signal as partial unplaced demand, are both
  ruled out"*.
- `docs/t4.4-review-record.md:90` (§D2): *"can the source be drained below a valid state | it can
  be emptied, and that is ratified: ADR-012 says flight 'still empties a starving settlement into
  healthy neighbors' and is 'a surge by design, bounded by the overdraw scaler alone'. No new floor
  was invented"*.
- **Conflict:** the readout's VALUE is today the raw linear flight desire
  (`BaseRate·profile·count·dt·FamineFlightFactor·d`, `MigrationSystem.cs:338-341`). If flight
  changes shape and the carrier does not, colonization party sizes diverge from migration's own
  semantics. The value must follow the bounded destination-free hazard; under it `DrawParty` floors
  a value `< count`, so "it can be emptied" in one founding is no longer true. The CONDITION (binary)
  is untouched.

### N7 — CR-003 §8.5 / exit-inventory §12 / corridors.json: "not tuned", "not an M5 tuning target", the teeth

- `docs/adr/cr-003.md:508-517` (§8.5): *"Migration is provisionally accepted for M4 with the causal
  explanation open; it was not tuned, no constant was moved, and no corridor bound was changed.
  This is a superseded attribution, not a new cause: no replacement cause is asserted here."*
- `docs/m4-exit-inventory.md:302-305` (§12): *"Migration is not an M5 tuning target. The anomaly is
  an open research question. It is not permission to move migration constants or corridor bounds."*
  · *"The Malthus corridor stays quarantined. Do not loosen it, fit yield to it, alter consumption
  or demographics to satisfy it, fabricate starvation, or delete the quarantine."*
- `Sim.Data/content/corridors.json:97`: *"THE RULING IS TO ACCEPT CURRENT MIGRATION BEHAVIOUR AND
  NOT TUNE TO THIS CORRIDOR. … The corridor is retained as a RECORD of a historical expectation this
  world no longer meets, not as an acceptance gate"*; `:143`: *"THE FLOOR DOES NOT MOVE"*.
- `Sim.Tests/Systems/CalibrationBatteryTests.cs:236-239`: *"`Assert.True(value < lo, … is back
  INSIDE the corridor … the B-1b dev-preset deviation is RESOLVED for this seed. Re-measure the dev
  seed set …")`"* — the below-floor tooth; `docs/adr/cr-003.md:290-296` (§5.4): *"The Malthus
  corridors are not restored by adjusting any constant. They stay quarantined until a mechanism
  legitimately makes them true again. The derived 26.0 is not provisional and not negotiable."*;
  `corridors.json:136` (`starvationRatePer1000`): *"famine demography must EXIST (zero means the
  crash was bloodless = dodge regression)"*.
- **Conflict:** the redesign changes migration and demographic MECHANISMS on the director's order
  and will move `dev.starvationRatePer1000`, `migrationGrossPerDecade` and possibly `crashCount`; the
  tuning bans do not forbid a director-ordered mechanism change, but §12's blanket wording and the
  teeth will be cited against any diff unless the distinction is recorded and the expected corridor
  movement is pre-committed (ADR-015 §7.13 pattern).

### N8 — one-directional substitution; ClassMobility demote-first

- `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:30-36`: *"STAPLE SUBSTITUTION. … Unmet
  non-staple food demand falls back on the staple (grain): a household short of fish eats more bread
  rather than starving while grain sits in the store."* (one direction only; `:150-176`).
- `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:226-231`: *"if (deficit > 0.0) { //
  FAMINE DEMOTE-FIRST: overrides the relaxation entirely. movePerYear = m.FamineDemoteRatePerYear *
  deficit * managedAdults; …"*.
- **Not a conflict, but two lines the mandate requires signed:** (a) the mandate's preserve list
  says "substitution — STOP if it must change"; the pure pastoralist (`queue.md:1130-1140`, nominal
  `d = 0.94` in every non-raid turn) classifies SEVERE under the ladder and starves at
  `d_eff = 0.925` — the CR must say whether that REQUIRES a substitution change (G6). (b) ClassMobility's
  demote-first reads nominal `d` and the CR must say whether it stays there (N8b).

### N9 — exit-gate open items 4a/4b are unowned

- `docs/milestones.md:352` (4a): *"The blocking observable is famine lethality under T4.2 store
  bounding, not migration, so the recorded owner 'M4 migration' is wrong … OPEN, needs reassignment;
  currently unowned."*; `:353` (4b): *"OPEN, needs an owner to re-aim the teeth onto the gap-driven
  observable."*; `:374-375` reconciliation: *"OPEN / REQUIRES DIRECTOR RULING"*.
- `Sim.Tests/Systems/MigrationTests.cs:378, :532` `[Fact(Skip = "T4.1b/ADR-018 §11: … Lifts when
  M4's migration work re-derives the assertion. Owner: M4 migration.")]`; `docs/adr/adr-018-d025-
  minimum-spacing.md:260-262`: *"The skip lifts when M4's migration work re-derives what these teeth
  should assert — not by adjusting a threshold, which would pin the wrong observable harder."*
- **Conflict:** none in substance; the items need an owner and this packet is the migration work
  the skip reason names.

### Not in conflict — recorded so nobody reads them as breached

- **D-021** (`docs/d021-stability-doctrine.md:8`: *"Every positive feedback loop in the design must
  ship with at least one negative feedback loop that strengthens with amplitude."*; `:28`: *"Exit.
  … Openness of exits (free movement, available land, transport reach) governs the valve"*). The
  redesign DISCHARGES the paired-feedback rule for the famine → flight loop: the vacancy bound and the
  exit-openness term ω are negative feedbacks that strengthen with amplitude, and ω is literally
  "openness of exits". Exit-inventory §12's "D-021 stays as-is. Needs/grievance state drives no
  behaviour before M5" is about NEEDS/GRIEVANCE state and is untouched — ω does not activate D-021.
- **T4.10 Option A** (`docs/t4.10-review-record.md:432-436`: *"Food reaches migration through the
  channels it already had and that were already ratified — famine flight (source push), destination-
  deficit repulsion and the absolute food gate (both inside `viability`). No new channel was
  added"*). ω is the NORMALISATION of the existing flight channel over destinations and the vacancy
  bound is a destination-side SCALE on the total; neither is a fourth channel, and `R_i = LandWeight
  × arableKm2_i` (land-only attractiveness) is untouched — VACANCY ≠ ATTRACTIVENESS made literal.
- **CR-003 §7.3** (`cr-003.md:361-375`, transient starvation vs Malthusian crash at 0.20 drawdown):
  the four-state ladder REFINES "ordinary transient starvation"; the crash definition is untouched.
- **ADR-012's collapse teeth** (`adr-012:78-85`, T2.13 `CollapseStabilityTests`): must stay green
  AND must still bite (ADR-015 §7.2); §4 lists them.
- **Observability §0** (`docs/observability-architecture.md:14-33`): every new readout is READ or
  RECOMPUTED through a PUBLIC static; a conformance obligation, not an amendment.

---

## 2. EVIDENCE (S8 §3 field 2)

### 2.1 The director's mandate (2026-09-17), quoted in substance

Nine items. (1) Famine is EXCEPTIONAL. Four derived states — NORMAL, FOOD STRESS, SEVERE FOOD
STRESS, FAMINE; only FAMINE activates the exceptional response; ordinary variability is not famine;
(A) a famine-class natural disaster and (B) deliberate productive abandonment (farming = 0 ∧
herding = 0) are the two famine CAUSES; "shortage remains meaningful" in every state. (2) Weather
stays stochastic and unclamped; "if no explicit disaster concept exists, find the smallest
architecture-consistent representation". (3) Migration: the pipeline is stress → hazard → bounded
source outflow → destination shares → bounded destination inflow; HOW MANY is separated from WHERE;
"destination count must not multiply total quantity"; exact timestep integration; bounded per turn;
ordinary and severe-crisis migration both preserved. (4) "destination-level aggregate inflow must be
bounded with a clear semantic (not an arbitrary cap)"; VACANCY ≠ ATTRACTIVENESS; ADR-012 viability
preserved. (5) "sustained ration cuts of ~15–25 % are survivable" — the physiological frame. (6)
"a single 0.30-deficit decade still removes ~59 % of Libur" — shock integration must be redesigned
within the famine semantics; §28: under constant conditions population rises asymptotically toward
the limit set by food INFLUX and never overshoots into deficit. (7) Preserve: the derived 26.0, the
stock-based store model, one-directional substitution (STOP if it must change), founding, density,
`PolityId`/`ControlRow`, the 10-year turn, the 0.5-year micro-steps, determinism; gap tags
PRESERVE / REPAIR-MIN / AMEND-FIRST / NEW; governed-but-wrong mechanisms get the amendment BEFORE
the code; no serialized famine-stress state unless a persistent regime is demonstrated. (8) Mutation
tests: the flight cap removed → a test fails; fan-in protection removed → a test fails. (9)
Observability: read-only, non-authoritative, no God object; famine/disaster/abandonment trigger
reasons, hazard, source and destination bounds, aggregate inflow visible.

### 2.2 The Libur cascade counterfactuals — `docs/t4.21-evidence/` (committed by T4.21-0)

Provenance: commit `45046eb` + `diag-telemetry.patch`, Linux (ADR-022), seed 42, the director's 15
playtest orders, 161 turns, run id `02ee1038911d8d53`; the instrumented replay reaches the pristine
build's final hash `e234616b…b4e9` (README). By path, the numbers this CR rests on:

| finding | locus | number |
| --- | --- | --- |
| the BASE event | `cascade-report.md` §1.2 | Libur t117 deficit **0.297207** (weather multiplier 0.4427 drawn at t116, a 0.4th-percentile decade as shipped); t118 population **809 → 69** (loss 0.9147); outflow **641/809** = 0.792; t119 inflow **1213 into 69** (17.6×); t119 deaths 539/69 |
| destination count as a magnitude multiplier (N4) | §3 G, §1.4 column D | `D_Libur(t118) = Σ_j damping·viability = 4.384` with viability exactly 1.0 for all 11 neighbours; "D is a magnitude multiplier of 4.4× at Libur, contradicting ADR-012's 'only redistributes WHERE'" (CONFIRMED) |
| per-bucket saturation (N4, N5) | §3 G | with `K·d·dt = 2.4 × 0.297 = 0.71` per destination × D = 4.38, the overdraw clamp `min(1, ·)` binds for cohorts 2–7 (55.6 % of Libur); the aggregate reproduces 641 to +1.2 persons |
| flight bounded (M1 arm) | §3 B, §4 Branch B | at the identical draw, maxOutflowFrac 0.792 → **0.134**; maxPopLoss 0.915 → 0.594; maxInflowFrac 17.6 → 1.94 — "FIRST-ORDER amplifier, CONFIRMED" |
| the residual demographic trough (N2, N3, G3) | §3 D, §4 Branch D | M4 arm (migration bounded): Libur **782 → 321** at t118 = 13.3 pp outflow + **49.1 pp deaths**; births **27 vs ~325** normal (**39 pp missing births**); "lies in Demographics' decade integration, outside migration" |
| the demographic wiring is correct (E) | §3 E | deaths/opening 0.493 under M1; t118 starvation 195; the accepted finding is NOT reopened — the kernel does what its equations say; the equations' ARGUMENT is what CR-015 changes |
| weather variance (G1, escalated) | §3 A, §4 Branch A, §8 | M3 arm (σ_decade 0.1933): Libur t117 deficit **0.0786** (vs 0.297); famine ≥ 0.05 turns 3 → 1; "REJECTED as dominant" — the response at the surviving shock is still 37 % outflow, 52 % loss, 137 % refill |

Derivations from the kernel (spec §1.4, §3.2): at Libur's `d = 0.30`, `suppression = max(0, 1 −
3.0·0.30) = 0.10` removes 90 % of a decade's births (≈ 38 pp at a crude birth rate of 4.2 %/yr);
adult starvation `1 − e^{−0.12·0.30·10} = 30 %` per decade; today's kernel starves 21 % of adults per
decade at `d = 0.20` — inside the director's "survivable" band.

### 2.3 The audit findings (spec §2) — F1, F4, F5

- **F1 (G1, escalated):** `HarvestWeatherSystem.cs:42-45` holds the log-deviation's STATIONARY
  variance at σ² for every dt and `ProductionSystem.cs:239` applies `exp(x − σ²/2)` to the WHOLE
  turn's output; the turn-MEAN of an OU process has variance factor `g(dt/τ) = 2(τ/dt)[1 −
  (τ/dt)(1 − e^{−dt/τ})]` = 0.427 at dt 10, 0.948 at dt 0.5. Full derivation §6.4.
- **F4 (G8):** `ConsumptionSystem.cs:195-230` settles the whole turn's balance at once, so at dt = 10
  the EFFECTIVE buffer is `B_eff = G + dt·(ρ − 1)` years of demand (4.5 y at `ρ = 1.3`, 12.5 y at
  `ρ = 2.1`) while the granary holds 1.5 y; a production loss of `s·D` years produces a deficit at
  dt 10 iff `s·D > (dt(ρ−1) + G)/ρ` (3.46 at `ρ = 1.3`). The t4.2 record's open "dt-versus-buffer
  question" (`t4.2-review-record.md:286-289, 382-387`).
- **F5:** Σ over food goods is not feedable nutrition — substitution is INTO the staple only
  (`ConsumptionSystem.cs:150-176`); Libur t110 food produced 12 733 vs required 6291 reads 2.02× but
  the feedable limit is `7458/0.9 = 8287` = 1.32× (`cascade-report.md` §1.4 row 110).

### 2.4 The tree's own records

- `docs/milestones.md:352` (4a): *"an ordered total-harvest famine at one of twelve no longer kills
  8 % of the settlement within 40 turns"* — famine lethality is the unowned observable.
- `docs/queue.md:1130-1140` (T4.5): *"a pure pastoralist alternates deficit 0.94 / 0.00 forever"*,
  measured with the real pipeline at weather 1.0 — the G6 fact.
- `docs/t4.20-food-semantics.md:178-179`; `queue.md:1358-1359` — no population-cap semantics ruled.
- Baseline suite on this tree (Release): 754 passed / 2 failed (`Dev_MalthusCorridors_AllInBand`
  seeds 42 and 7, red by CR-003 ruling) / 6 skipped — measured for spec §7, not the stale
  722/4/6 in `docs/current-state.md`.

---

## 3. MINIMAL FIX OPTIONS (S8 §3 field 3, ≤ 3)

1. **This architecture** (`docs/t4.21-architecture.md` §3): derived `FoodState` ladder with one
   boundary `a`; `DisasterSystem` + `DisasterRow` (v25) as a production multiplier in a band derived
   against `B_eff` at the coarsest dt; effective deficit `d_eff` into starvation mortality and
   fertility suppression; exact flight hazard on the nominal deficit with exit openness ω and
   destination shares; basin caps on the gap channel; a vacancy bound on total inflow; the feedable
   food-influx limit `FoodHeadroom` and the headroom growth cap. No constant moved; no band moved.
2. **The same without `DisasterSystem`** — famine only by abandonment. REFUSED: the mandate's item
   1(A) names the disaster as a famine cause, and without it FAMINE is reachable only by player
   order.
3. **The same without the headroom cap and the vacancy bound** — flight and mortality only.
   REFUSED: the mandate's §28 asymptote and item 4 ("bounded destination inflow with a clear
   semantic") are then undelivered, and the cascade's M1 arm shows why (maxInflowFrac 1.94, a
   one-turn refill spike then back-flow).

---

## 4. BLAST RADIUS (S8 §3 field 4)

**Docs:** this CR; ADR-024/025/026; `docs/t4.21-architecture.md`; append-lines on `cr-003.md`
(§9), `cr-012-malthus-architecture.md`, `adr-012`, `adr-021`, `t4.12-derivation-record.md`,
`t4.4-review-record.md` §D2, `t4.2-review-record.md`, `m4-exit-inventory.md` §12; living edits to
`m4-spec.md` §3.4/§4, `milestones.md`, `queue.md`, `current-state.md`; `corridors.json`
`dispositionEvidence` lines only.

**Coupling map (spec §3.10, OUTBOUND):**

| new mechanism | perturbs | re-anchoring expected |
| --- | --- | --- |
| `DisasterSystem` + `DisasterRow` (v25) | every snapshot (layout + RngStreams); food production of struck settlements → stores → deficits → every deficit reader; chronicle; `Cr003Quarantine` guards (`PopulationTests.cs:282,483`, `ChronicleTests.cs:331`) | ALL FOUR GOLDENS + `ci.yml:136` (layout at T4.21-1 with λ = 0; behaviour at T4.21-4); `IntegratedPinAttribution` ladder extended; `SnapshotDiff` layout; `Assert.Equal(24, Version)` ×4 + a JSON literal; v24 UI saves unloadable (stated) |
| `FoodState` / `d_eff` | Demographics starvation and fertility flows; chronicle famine events | `PopulationExactnessTests` d > 0 rigs (replica takes `dEff`); `DemographyRetuneTests.Famine_MortalitySpike` (G2); `ClassSystemTests` famine-drain timing; `FamineAgeSelectivity` unaffected (d = 1 ⇒ `d_eff = 1`) |
| `FoodHeadroom` (`N_lim`) | growth of near-limit fed settlements (down-only); vacancy of destinations | goldens only where some canonical settlement has small `H_0` or a refugee inflow exceeds `cap_j` — the attribution ladder says whether it bit |
| bounded flight + ω + shares (nominal `d`) | `MigrationFlows`; `UnplacedDeparture` (colonization party sizes ≈ −⅓ at the rigs' deficits); `CollapseStabilityTests` trajectories; `MigrationTests.Overdraw_ScalesProportionally` (breaks by construction; re-rigged to assert shares); `FamineFlight` values; ordered-golden worlds | goldens; `ObservedWorlds`/`WorldReconciliationTests` measured-turn comments; `Founding5` party/provisions comments; every `ColonizationTests` rig re-measured |
| basin caps (gap channel) | multi-source/destination gap flows; `BifurcationConfig_NoTwoTurnOscillation` (the T2.8 empirical pin — must NOT regress) | goldens; new many-to-one / one-to-many pins |
| vacancy bound (total inflow) | refugee inflow into small or land-bound destinations after a collapse; `queue.md:65` ping-pong | goldens (behaviour); `M_Basin_FanIn_Flight_BoundedByVacancy` |
| headroom cap | `FedGrowth_DtInvariant` (yield 1e9 ⇒ identical); density corridor (down-only) | ladder step "P3 alone" |
| observability statics | telemetry field set (additive under `telemetry/v2`); `MigrationExplanation` text; UI positional ctors | no golden (`TelemetryTests :19` pins no hash change) |

**Golden ladder:** T4.21-1 moves all four goldens by LAYOUT + RngStreams only (λ = 0; the strip
control reproduces the pre-packet pins byte-for-byte); T4.21-2 and T4.21-3 each carry their own
attribution step ("P2 alone", "P3 alone"); T4.21-4 arms λ = 0.01 and is the last golden move. Golden
constants are re-MEASURED on the merged tree, never merged as digits (ADR-015 §6).

**Test dispositions:** `Famine_MortalitySpike` phase 1 re-anchored + abandonment twin (G2);
`Overdraw_ScalesProportionally` re-rigged to assert shares (aim kept); `FamineAtOneOfTwelve` and
`MagnitudeCorridor_FedPhaseDrift` skips LIFTED with re-derived assertions (N9); `Cr003Quarantine`
guards restored and `Dev_MalthusCorridors` messages re-read (their `starvedTotal == 0` tooth re-read
— dev starvation may now be non-zero by a disaster, which the corridor note calls "must EXIST");
`AssertDevMigrationQuarantine` becomes a recorded envelope (N7); `CollapseStabilityTests` teeth kept
green and re-verified to bite; every `ColonizationTests` rig re-measured before the readout cut.
Corridors this work MOVES with pre-committed readings: `dev.starvationRatePer1000` RISES from 0 iff a
disaster strikes a `ρ ≤ 1.4` dev settlement; `migrationGrossPerDecade` direction NOT pre-committed,
reported; `densityPerArableKm2` unchanged within noise (spec §3.9). No band moves.

**Packets:** T4.21-0 (this), T4.21-1..6 as `docs/t4.21-architecture.md` §4 (no T4.21-W).
`origin/m5-full-build` collides on schema v25 and on `ProductionSystem.cs:239/:306`; that branch
renumbers when it rebases (ADR-024).

---

## 5. RECOMMENDATION (S8 §3 field 5)

Adopt option 1 with the mandate as the ruling text, N1–N9 as explicit amendment lines, G2–G8
decided by the orchestrator under the mandate's ownership grant, and G1 escalated as its own
finding with the derivation attached. Implementation proceeds in the order of spec §4, governance
first (this packet), code second.

---

## 6. DIRECTOR RULING (2026-09-17, recorded from the mandate)

### 6.1 The ruling (recorded verbatim in substance)

**FAMINE IS EXCEPTIONAL.** The consumption deficit is the food balance's signal and stays so in
every state; it is not a famine. There are four derived states — NORMAL, FOOD STRESS, SEVERE FOOD
STRESS, FAMINE — and only FAMINE activates the exceptional response. There are exactly two famine
CAUSES: a famine-class natural disaster, and deliberate productive abandonment (farming = 0 and
herding = 0); a settlement that keeps either food sector alive is not in famine, whatever its
deficit. Ordinary weather variability is never famine. Weather stays stochastic and unclamped; the
disaster is represented by the smallest architecture-consistent mechanism.

**MIGRATION IS BOUNDED, AND HOW MANY IS SEPARATED FROM WHERE.** Stress → hazard → bounded source
outflow → destination shares → bounded destination inflow. The number who leave depends on the
source's stress and on how open its best exit is, never on how many exits there are; exact timestep
integration; bounded per turn. VACANCY ≠ ATTRACTIVENESS: whether a destination can accept is
viability (ADR-012, preserved); how many it can absorb is its vacancy in food terms; where people go
is attractiveness. Destination fan-in is bounded with a clear semantic, not an arbitrary cap.

**DEMOGRAPHICS FOLLOW THE EVIDENCE.** A single 0.30-deficit decade removing ~59 % of a settlement is
pathological; shock integration is redesigned within the famine semantics: sustained ration cuts of
~15–25 % are survivable and produce no excess mortality; only FAMINE starves on the whole deficit.
§28: under constant conditions population rises asymptotically toward the limit set by sustainable
food INFLUX and never overshoots into deficit.

**PRESERVE:** the derived 26.0; the stock-based store model (T4.2); one-directional substitution
(STOP if the famine semantics require changing it); founding and colonization semantics; density;
`PolityId` + `ControlRow`; the 10-year turn and the 0.5-year micro-steps; ADR-012 viability;
determinism. No serialized famine-stress state unless a persistent regime is demonstrated. Governed-
but-wrong mechanisms get the amendment BEFORE the code.

**CR-003 §8.5 MUST BE REOPENED EXPLICITLY, NOT HIDDEN.** The provisional acceptance of migration
"with the causal explanation open" is superseded by a director-ordered mechanism change, recorded
as such in CR-003 itself (appended §9) and here (N7); the tuning ban and the Malthus quarantine
stand.

### 6.2 Amendments (frozen text → amended text)

- **N1 — `m3-spec.md:52` / `cr-003.md:280-283` / `HarvestWeatherSystem.cs:21-28`.** *"no famine
  trigger, threshold or event that fires independently of the food balance"* → **"no famine
  trigger, threshold or event that fires independently of the food balance AND of an exceptional
  cause. FAMINE is a derived state requiring BOTH a food deficit (`d > 0`; the balance still
  decides) AND a cause: a famine-class disaster row applied to the harvest that produced the
  deficit, or productive abandonment in force. The disaster row is a PRODUCTION shock in weather's
  class (it multiplies food output; it never decides famine, never reads stores, population or
  deficits); the ladder's other boundary (`a`) separates adaptation from starvation and is not a
  famine threshold."** Weather's own "FAMINE IS NOT TRIGGERED HERE, AND CANNOT BE" stands
  unchanged. Recorded in ADR-024; CR-003 §9 appended.
- **N2 — `cr-012-malthus-architecture.md:157-158`.** *"No demographic architecture change of any
  kind is authorized"* → **"AMENDED BY CR-015 in narrow scope: (a) the starvation hazard reads the
  EFFECTIVE deficit `d_eff` — linear form and constants retained, argument changed; (b) fertility
  suppression reads `d_eff` outside FAMINE and nominal `d` in FAMINE (G3(b)); (c) a headroom
  fertility multiplier on the feedable food-influx limit acts inside the micro-loop. Every other
  CR-012 fact stands: ADR-011 exponential survival, `MicroStepYears`, ADR-010 aging, the rebound
  reservoir and its nominal-`d == 0` release gate, the reconciliation order, T4.2 store bounding,
  Consumption — all untouched."** Recorded in ADR-026; one line appended after `cr-012:162`.
- **N3 — `t4.20-food-semantics.md:178-179` / `queue.md:1358-1359` / `milestones.md:324-326`.**
  *"a population-cap semantics on which no ruling exists"* → **"RULED: population approaches the
  FEEDABLE food-INFLUX limit `N_lim` (the fixed point of Consumption's one-directional substitution
  at the standing basket mix, `FoodHeadroom.Limit`) asymptotically, via a fertility multiplier on the
  remaining headroom at rate `k`; immigration draws on the same headroom at the same rate; no
  serialized cap state; decline is the deficit channel's job (the cap never removes a person)."**
  `FoodSupportedPopulation`/`FoodConstrainedGrowth` become legally RECOMPUTED. Recorded in ADR-026;
  queue lines closed.
- **N4 — `adr-012:53-63` / `MigrationSystem.cs:44-51, :93-95`.** *"uncapped by the gap mechanism …
  Viability only redistributes WHERE the fleeing go"* / *"bounded by the overdraw scaler alone"* /
  *"the collective inflow stays inside the basin — pinned empirically"* → **"The Exit valve is
  bounded by EXACT INTEGRATION of a per-year hazard on the best exit (`φ = 1 − exp(−profile·K·ω·d·dt)`,
  `ω = max_j damping_ij·viability_j`) at the source and by the destination's VACANCY at the
  destination; the overdraw scaler remains the backstop. Viability GATES and, through the best exit,
  SCALES; vacancy BOUNDS; shares DISTRIBUTE. Source-driven, gap-independent and 'die at home' (ω = 0
  ⇒ φ = 0) are unchanged. Collective gap inflow AND outflow are bounded by construction by the basin
  `f·M*` (the pair physics pooled) — the empirical assumption becomes a pinned mechanism."**
  Recorded in ADR-025; one line appended to ADR-012.
- **N5 — `t4.12-derivation-record.md:426-428, :768-780`.** *"STOP condition 2 / Phase 1D"* →
  **"DISCHARGED for CR-015 by director ruling: the mechanism's strength changes by INTEGRATION FORM
  (exact, saturating), by ω (best exit instead of Σ over exits) and by the deficit argument — not by
  re-weighting. `BaseRatePerYear` 0.03, `FamineFlightFactor` 8.0, `CohortProfile`, `GapClosingFraction`
  0.25 and the three-constant gauge freedom are untouched; the provenance debt is re-recorded by
  the T4.21-1 audit, not paid."** Pointer line appended to the derivation record.
- **N6 — `adr-021:34, :70-74` / `t4.4-review-record.md:90`.** *"departure demand … `BaseRate·
  profile·count·dt·FamineFlightFactor·d`"*, *"MigrationSystem's equations … untouched"*, *"it can
  be emptied, and that is ratified"* → **"The readout's VALUE is the bounded destination-free hazard
  on the nominal deficit, `(1 − exp(−profile·K·d·dt)) × count` (ω := 1, since under B1's condition
  ω_i = 0); its CONDITION stays binary and its D-004 remainder semantics unchanged. `DrawParty` floors
  a value < count, so a source is no longer emptied in one founding. Refugees refused by vacancy do
  NOT enter the readout (G7(a)). A turn-exact pin is required (T1.9/T4.4 precedent); every
  colonization rig is re-measured before the cut."** Recorded in ADR-025; one line each appended to
  ADR-021 and the T4.4 record.
- **N7 — `cr-003.md:508-517` §8.5 / `m4-exit-inventory.md:302-305` §12 bullets 1–2 /
  `corridors.json:97, :143` / `CalibrationBatteryTests.cs:236-239` / `cr-003.md:337-341`.**
  *"provisionally accepted … not tuned, no constant moved"*; *"Migration is not an M5 tuning
  target"*; *"Do not … alter consumption or demographics to satisfy it, fabricate starvation"*;
  *"ACCEPT CURRENT MIGRATION BEHAVIOUR AND NOT TUNE TO THIS CORRIDOR"* → **"SUPERSEDED FOR THE
  MECHANISM CHANGES CR-015 ORDERS, and for those only. The TUNING ban stands: no migration constant,
  no demographic constant, no corridor band moves. The Malthus quarantine stands with its bands
  frozen; the change to starvation mortality and fertility under CR-015 is not a change made to
  satisfy or to lift it. `corridors.json:97` is re-read as 'accept as a RECORD; CR-015 will move it
  and pre-commits the reading' (spec §3.9); the below-floor tooth and the dev-quarantine drift tooth
  become recorded envelopes re-pinned under this ruling, never fitted. CR-003 §8.5's 'no replacement
  cause is asserted' stands — CR-015 changes the mechanism; it does not claim to have identified
  the pre-existing cause."** CR-003 §9 appended; exit-inventory §12 note appended.
- **N8 — `ConsumptionSystem.cs:30-36` / `ClassMobilitySystem.cs:226-231`.** (a) **One-directional
  substitution is UNTOUCHED by T4.21. The STOP condition is NOT reached (G6(a)): the famine
  semantics do not require a substitution change; the pure pastoralist's `d = 0.94` is a
  pre-existing basket fact the ladder labels SEVERE and starves exactly as today. A substitution CR
  is queued for M5.** (b) **`ClassMobility` famine demote-first stays on nominal `d` — an economic
  response to any shortfall, bit-identical.**
- **N9 — `milestones.md:352-353, :374-375` / `MigrationTests.cs:378, :532` / `adr-018:260-262`.**
  *"currently unowned" / "needs an owner"* → **"OWNED BY T4.21-4: both skips lift with re-derived
  assertions — 4a re-aimed as `S_Abandonment_TriggersFamine` (famine lethality under the ladder), 4b
  re-aimed onto the gap-driven observable; neither by adjusting a threshold."**

### 6.3 Orchestrator decisions under the mandate's ownership grant

The director's mandate grants the orchestrator ownership of decomposition. The following are
orchestrator decisions the director may override; the rationale for each is
`docs/t4.21-architecture.md` §13.

- **G2** — `DemographyRetuneTests.Famine_MortalitySpike_BirthDeficit_PostFamineRebound`: phase 1
  re-anchored as "STRESS does NOT spike" (its rig is a ~0.2-deficit Default-sector, weather-free,
  disaster-free settlement — FOOD STRESS under the ruling); an abandonment TWIN (0 %-farm order)
  asserts the spike where it is now true; phases 2 (birth deficit) and 3 (rebound) stand.
- **G3 — (b).** Fertility suppression reads the EFFECTIVE deficit outside FAMINE — one effective
  deficit for both exceptional demographic channels; FAMINE reads nominal `d` (= `d_eff` there).
  The rebound release gate stays on nominal `d == 0`. The birth full-stop sits at nominal
  `d = a + (1 − a)/3 = 0.4667` outside FAMINE and at `1/3` inside it. Evidence: the counterfactual
  decomposition (49 pp deaths + 39 pp missing births) — a 90 % birth suppression from a non-famine
  decade is the exceptional response the ladder reserves for FAMINE.
- **G4** — packet **T4.21** (post-certification M4 packet, the T4.17–T4.20 class), sub-packets
  T4.21-0..6 as spec §4; no T4.21-W.
- **G5** — proceed; the disaster multiplies BOTH food paths exactly as weather does; "herding → 0"
  is a sector-row fact; `FoodHeadroom` counts non-staples up to their basket share — all independent
  of how livestock is STORED. Recorded so the T4.20 storage ruling (`queue.md:1348-1352`), when it
  comes, cannot reopen ADR-024/026.
- **G6 — (a).** Pastoralists starve by basket design (D-035 as ratified): NOT a STOP. The mandate's
  condition reads "require"; the semantics do not REQUIRE a substitution change — they expose a
  pre-existing basket fact (`queue.md:1130-1140`, measured at weather 1.0). `S_Pastoralist_Pipeline`
  and `S_FarmerOnly_Pipeline` pin both single-sector outcomes; the substitution CR is queued for M5.
  Option (c) (label-only) refused as a label without a mechanism.
- **G7 — (a).** B1's condition unchanged (ADR-021 `:98-101` ratified it binary and value-
  independent); refugees refused by the vacancy bound stay home ("die at home", ADR-012); they do
  not enter `UnplacedDeparture`; "stranded by capacity" founding is queued as the first M5
  colonization line; `MajorEvents` "REFUGEES REFUSED" makes the case visible meanwhile.
- **G8 — (a).** The dt-vs-buffer artefact (F4; `t4.2-review-record.md:382-387`) is acknowledged as
  INHERITED; `ρ_ship = 1.3` is DECLARED as the derivation input for the disaster band (a magnitude
  condition, not a corridor fit); the dt-10 vs dt-0.5 classification of one physical event is PINNED
  AS A DIFFERENCE (`D_Classification_DtDifference_Pinned`), never asserted equal; the per-settlement
  `ρ` distribution is reported; the per-year food-balance sub-step is queued for M5. Option (b), a
  "granary loss" kind, is not authorized here.
- **Also decided:** N8b (above); the all-zero sector row classifies as ABANDONMENT (raw weights;
  reachable only from a hand-written log; "nothing is farmed" is literally true of it); the
  disaster onset-process biases (≈ 4.8 % rate bias at dt 0.5, ≈ 4.7 % of onsets truncated at dt 10)
  are ACCEPTED at λ = 0.01 with the exact renewal queued; §2's cascade evidence is cited by
  COMMITTING the report, the pre-registration, the instrumentation patch and the BASE metrics into
  `docs/t4.21-evidence/` (evidence, not code).

### 6.4 ESCALATED — requires a separate director ruling: G1, the harvest-weather decade variance (F1)

**Finding.** `HarvestWeatherSystem.cs:42-45` holds the log-deviation `x` at STATIONARY variance
`σ²` for every dt (`ρ_AR = e^{−dt/τ}`, innovation `σ√(1 − ρ²)`), and `ProductionSystem.cs:239, :306`
apply `exp(x − σ²/2)` to the WHOLE turn's food output. The STATE is dt-correct (T3.4b/T3.4c's
deliberate invariant — "the era table altering the climate" is what `√(1 − ρ²)` prevents). The
OUTPUT multiplier is not: a turn's harvest is the turn-MEAN of the yearly multipliers, and the
variance of the mean of an OU process over a window `T` is

```
Var(mean over T) = σ² · g(T/τ),   g(T/τ) = 2(τ/T)[1 − (τ/T)(1 − e^{−T/τ})]
g = 0.427 at T = 10 y (τ = 3):  σ_decade = 0.2936 × √0.427 = 0.192
g = 0.948 at T = 0.5 y:         σ_half-year = 0.286
```

so at the canonical dt = 10 the shipped decade multiplier is 1.5× too variable in σ. Consequences:
the 5th-percentile decade reads 0.59 as shipped where the yearly σ/τ imply 0.72; `P(decade
multiplier ≤ 0.57)` is **3.9 %** as shipped versus **0.25 %** corrected (`Φ((ln 0.57 + σ²/2)/σ)` at
σ = 0.2936 vs 0.192); at Libur's `ρ ≈ 1.3` the recorded t117 draw (0.4427, a 0.4th-percentile decade
as shipped) produced `d = 0.30`, and the cascade's M3 arm at the corrected σ produced 0.079
(`docs/t4.21-evidence/cascade-report.md` §3 A). Under the shipped variance an ordinary SEVERE decade
produces a larger dt-10 deficit at a `ρ = 1.3` settlement than a famine-class disaster does (≤ 0.20,
ADR-024's table), and the era table alters the decade-scale climate — the exact hazard the code
comment names.

**Why this is a genuine conflict and not applied inside T4.21.** The stationary-variance-at-every-dt
property is a deliberately ratified T3.4b/T3.4c invariant (`m3-spec.md:52-53`); the mandate says
weather is not to be modified to prevent famine ("stochastic and unclamped"); and law 3 says the
output should carry the turn-mean's variance. Two frozen items and a law disagree. S8 §3 records
such a conflict and escalates it; it does not let a packet resolve it by choosing. Applying `g`
would also be a fourth behavioural cause moving every golden and must never share an attribution
step with T4.21-1's layout-only move.

**Options.** (a) **LEAVE** — the mandate's "do not clamp" read strictly; the redesign separates
famine from variability by CAUSE and by the disaster band's yearly-tail position (`z = −4.57` for
the band's mildest active year); ordinary SEVERE decades from weather stay as lethal as spec §9 R1
states (14–32 % of adults in the unabsorbed remainder). (b) **APPLY `g`** — `multiplier =
exp(√g·x − g·σ²/2)`, `g(dt/τ)` computed in `HarvestWeatherSystem.cs:138-142`; σ, τ, the AR(1) state,
the spatial blend and the mean-one property untouched; goldens move. (c) apply `g` AND re-derive
σ — REFUSED (σ is yearly and correct; re-deriving it is tuning).

**Recommendation: (b), as its OWN packet with its OWN golden attribution step** ("W alone",
serial between T4.21-1 and T4.21-2 if ruled before T4.21-2 starts, otherwise after T4.21-6),
with `HarvestWeatherTests` moment pins re-derived at `g` and the decade variance pinned
dt-invariant. **Until ruled: (a).** T4.21 ships with weather byte-identical; every T4.21 mechanism
is specified to be correct under either ruling; spec §7 reports V2/V3 under both arms.

### 6.5 What not to do (standing constraints from the ruling)

- **No constant moved.** `BaseRatePerYear` 0.03, `FamineFlightFactor` 8.0, `CohortProfile`,
  `GapClosingFraction` 0.25, `DestinationDeficitRepulsion` 1.0, `AttractivenessLandWeight`,
  `StarvationMortalityMaxPerYear` 0.12, the child/elder multipliers, `FamineFertilitySuppressionSlope`
  3.0, the reservoir constants, `granaryYearsOfDemand` 1.5, `sigmaLogYield`, `correlationTimeYears`.
  New constants (`a`, λ, D, `s_min/max`, `k`) are new, with their frames stated.
- **No band moved.** No corridor band, no quarantine lifted or deleted; movement is pre-committed
  and recorded, never fitted.
- **26.0 untouched.** The disaster multiplies output, never land quality.
- **Weather unclamped and unmodified** (pending G1).
- **Substitution untouched** (G6(a)); a substitution change is a STOP, and it is not reached.
- **No serialized famine-stress state.** `FoodState` is derived; the only new serialized row is the
  disaster's physical duration.
- **No population floor** (ADR-012 refused it; law 2).
- **No per-head inflow ratio** (`inflow ≤ population` is a diagnostic, not a bound).
- **No Libur fit.** No threshold, constant or band is chosen to make seed 42 come out any particular
  way; spec §7 V7 pre-commits no numeric target.
- **No merged golden digits.** Golden constants are re-measured on the merged tree by the agent
  who writes them (ADR-015 §6).

### 6.6 Post-ruling records (pointers only; nothing above is altered)

- **G8 "the per-settlement `ρ` distribution is reported"** — delivered as
  `docs/t4.21-1-foundations-audit.md` (T4.21-1 fix lane, 2026-09-17): seed 42 with and without the
  director's 15 orders and seeds 1–3; every canonical settlement's time-mean `ρ` is 1.78–2.02
  (`sD*` 5.22–5.79 above the band's 5.0); Libur under the orders is the one settlement exposed at
  its mean (`ρ` 1.43, `sD*` 4.06); `ρ_ship = 1.3` lies below every time-mean and at
  Libur-under-orders' median. The band is unchanged; the director judges the declaration against
  these numbers.
- **"The all-zero sector row classifies as ABANDONMENT (raw weights …)"** stands; the accompanying
  code fact stated in spec §3.1 / §13(ii) ("`Share` would divide 0/0") does not — `Sectors.Share`
  guards the zero sum. Recorded as erratum E1 in `docs/t4.21-architecture.md`'s header block and in
  ADR-024 §8; the ruling is unaffected.
