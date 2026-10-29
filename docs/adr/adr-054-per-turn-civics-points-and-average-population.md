# ADR-054 — Per-turn Civics Points from the Research Point base, and the average population

**Status: ACCEPTED — the Director's binding rule of 2026-10-27 (D-065 R6), which resolves CR-026.** Implemented behind two
`research.json` switches, both ON in the shipped content; with both absent the engine is the pre-ADR-054 engine byte for byte
(the attribution twin, `TestConfigs.PrePerTurnCivics`).

> "Every Research Point (RP) and Civics Point (CP) calculation is per turn and based on average population." "This is a
> binding model requirement." (D-065 R6)
>
> "do ensure that the base production rate (like without benefits of universities, buildings, buffs, penalties,
> espionage) of knowledge and civics points will be the same (since theyre population dependent)" (D-065 R5)

**Excepts:** CLAUDE.md law 3 ("every rate is per-sim-year; integrate with `dtYears`") — for Civics Points generation,
allocation and progress only, exactly as ADR-030 §2 excepts Research Points. This is the "second per-turn quantity" ADR-030
§3 says needs its own ADR. Law 3's text is unchanged, and so is ADR-030.

**Supersedes in part:** ADR-043 §3 (CP per sim-year × `dtYears`) — a dated note is appended to ADR-043; the rest of ADR-043
stands. **Resolves:** CR-026 (option 1).

## 1. The rule

1. **One base function.** `ResearchQuery.BaseKnowledgePointsPerTurn(tuning, P)` = `rpPerTurn.coefficient` ×
   P^`rpPerTurn.exponent` (0.08 × P^0.699; anchors 100 → 2, 1,000 → 10). Research Points AND Civics Points are this function
   of the same population input. Base RP = base CP **by construction** at every turn length and population — one function,
   not two coefficients kept equal by hand. The `civicsPoints` block therefore carries no coefficient or exponent of its own
   (the loader refuses `coefficientPerYear` / `exponent` beside `perTurn`).
2. **Per turn.** CP accrual is `floor(1000 × pace × c' × P^β)` milli-CP a step, **never multiplied by `dtYears`**
   (`ResearchQuery.CivicsPointsMilli`, the one code site; `dtYears` only gates a non-positive step). β = `rpPerTurn.exponent`;
   pace = 1 for an Empire, `cityStatePaceFraction` for a city state's local holder (the factor its RP carries).
3. **Benefits stay separate.** c' = `rpPerTurn.coefficient` × (1 + Σ `research.civicsPointsCoefficient`) and the RP lane's
   coefficient = `rpPerTurn.coefficient` × (1 + Σ `research.researchPointsCoefficient`) (ADR-047, `KnowledgeEffects.Apply`).
   Each lane's realized research benefit multiplies its coefficient on top of the shared base; with no benefit both lanes
   equal the base bit for bit. Universities, buildings, buffs, penalties and espionage (M10) are not part of the base.
4. **Average population** — §3.
5. **Switches.** `tuning.civicsPoints.perTurn` (true shipped; absent/false = ADR-043 §3, CP per sim-year × `dtYears`, with its
   own `coefficientPerYear` and `exponent`); `tuning.populationAverageTurns` (2 shipped; absent/1 = the turn-start population).
   Each has a single-switch twin (`TestConfigs.PrePerTurnCivicsOnly`, `PrePopulationAverageOnly`) and the pair is
   `TestConfigs.PrePerTurnCivics`.

## 2. Where it applies (exhaustive)

- `ResearchQuery.ResearchPointPool`, `CityStateResearchPoints` (and through them `HolderResearchPoints`, the ADR-051 reserve
  cap and draw) and `CivicsPointsMilli` read `KnowledgePopulation` (§3).
- `ResearchSystem.Step` step 3 (RP) and step 3b (CP accrual) are unchanged in shape: they call those functions.
- The Glass Box, the research screens and the CLI show the same functions (InfoQuery, ResearchNodeViews, ActionSurfaceModel,
  `sim research`). The CLI report names the average population and the per-turn CP; the Learning card says the points come
  from the population "averaged over the last turn".
- Nothing else. Every other system's rates stay per sim-year under law 3. Age milestones that count population
  (`AgeQuery`) read the turn-start population, as before: they are not RP or CP calculations.

## 3. The average population (STATED ASSUMPTION)

Nothing in the repository defined "average population". The implementing definition:

> A knowledge holder's average population for a step is the **trapezoid mean over the most recent turn interval**: (the
> holder's total population at the start of its PREVIOUS research step + its total population at the start of THIS step)
> / 2. On a holder's first step — no stored value — it is the current population.

- "Population" is `ResearchQuery.Population` (every person of the settlements the holder controls; a city state's own
  settlement for its local holder) — the input RP and CP read before this ADR.
- The earlier end is stored per holder in **`ResearchPopulations`** (schema v38, section 69: `Polity`, `Population`; 12
  bytes), rebuilt by ResearchSystem every step: one row per holder processed (roster Empires and city-state local holders),
  strictly ascending Polity. `ResearchQuery.KnowledgePopulation` is the ONE function both lanes read.
- **Consequences of the definition** (each pinned, §5): a population jump between two turn starts shows HALF its effect on
  the next step and its FULL effect on the step after; an annexation averages the Empire across it; a holder absent from
  the previous step (a city state just released, a newly founded Empire, a save from before v38) starts afresh from its
  current population; a dormant city state (controlled) has no row.
- **Why the trapezoid over two turn starts.** It is the smallest window that is an average over the turn just lived, it needs
  one stored number per holder, it is exact for linear growth within the turn, and it never reads the future. A longer window
  (`populationAverageTurns` > 2) would need more stored samples and is refused by the loader.
- Integer storage; the mean is (double)prev + (double)now, × 0.5 — exact for any population below 2^53.

## 4. Schema v38

`ResearchPopulationRow(PolityId Polity, long Population)`, section 69, appended after ResearchReserves; no existing row changed
width. Readable set {32, …, 38} (`CanonicalSchema.IsReadable`, `BytesAppendedSince`, `ReplayGuard`, `Snapshot.Load`,
`SaveFiles.MigrationNote`, `SnapshotDiff.LayoutFor`). A v37 stream stops after section 68 and loads with an EMPTY table: each
holder's first step after the load reads its current population (the first-step rule). `Snapshot.Load` refuses a table no
rule writes (unsorted or duplicate Polity, a negative population; `ResearchQuery.PopulationsInvariantViolation`). With
`populationAverageTurns` off no row is ever written, so a world is its v37 stream plus one empty count prefix;
`SchemaProjection.V37` (strip section 69, assert it empty) returns every v37 pin.

## 5. Enforcement

| Test | What it proves |
|---|---|
| `PerTurnCivicsAndAveragePopulationTests.ShippedContent_BaseRpEqualsBaseCp_ExactlyAtEveryTurnLengthAndPopulation` (24 cases) | dtYears 10, 5, 3, 2, 1, 0.5 × populations 100, 1,000, 10,000, 1,000,000: base RP == base CP == `BaseKnowledgePointsPerTurn` (exact `==`), the holder pool == the base, and the step's CP milli == floor(1000 × base RP), identical at every dtYears |
| `…_CpAccrual_IsPerTurn_IdenticalAcrossDtYears_AndEqualsTheRpOfTheSameStep` (6 dt) | the ADR-030 §5 pattern extended to CP: the engine accrues the same CP at every turn length, equal to the step's RP |
| `…_TheTwin_PerTurnAbsent_IsAdr043sPerSimYearAccrual_StillReachable` | the switch off is ADR-043 §3 exactly |
| `…_APopulationJump_ShowsHalfItsEffectNextStep_AndItsFullEffectTheStepAfter_InBothLanes` | the average, turn-exact, in both lanes; first-step rule; the stored row |
| `…_WithTheAverageOff_TheJumpShowsInFullAtOnce_AndNoRowIsWritten` | the average switch's twin |
| `…_TheTableIsOneRowPerHolder_StrictlyAscending…`, `…_ACityState_Averages…`, `…_ControlChange_…` | holders, city states (pace in both lanes), annexation and release |
| `…_SaveLoad_MidRun_BetweenAJumpAndItsSecondStep_…` | save/load between the two halves of a jump continues bit-identically; replay equal |
| `…_Loader_PerTurnRefusesASecondCoefficient_AndTheWindowIsOneOrTwo` | one base by construction; the window is 1 or 2 |
| `ResearchPopulationsSchemaV38Tests` (4) | the populated table: exact ExpectedLength, bit-exact round trip, hash equality; v37 migration; refusals |
| `CivicsPointsTests` (re-rigged to the per-turn form; values unchanged at dt 10) and `CivicsPointsTests.ShippedContent_…` (4 seeds) | every two-lane property kept under the new rule; every accrual is the formula |
| `Adr054AttributionTests` (8) | the golden re-pins: the switch-off twin (both switches off AND the pre-ADR-054 civics scale) → the old pin through `SchemaProjection.V37`; each switch alone moves the AI world; the toy world moves for the layout alone |
| `AiEmpireIntegrationTests.OneAiEmpire_WithAdr054Off_EveryPinReturnsItsPreAdr054Value` | the AI trajectory pins' attribution |
| `TreeClosureBaselineTests.AircraftAgeA9_ReproducesTheTc1Structure_RC1IsTheOnlyStructuralChange` | RC-1 (§8) is the only structural content change |

## 6. Consequences (stated, accepted)

- **CP per sim-year varies with dt**, as RP does (ADR-030 §4): with equal population CP per turn is now 10 ÷ dtYears times
  the ADR-043 §3 amount in every band shorter than 10 years (×2 at dt 5 … ×20 at dt 0.5). The civics `pacingScale` is
  therefore recalibrated by the D-063 R6 procedure for the shipped content and for the research-economy candidate:
  `docs/m6-plan/knowledge-redesign/research-cost-pass/per-turn-cp-recalibration.md`.
- **The average lags growth by half a turn.** A growing holder's RP and CP are slightly below the turn-start value; a
  shrinking holder's slightly above. A conquest or a famine shows in research over two steps.
- **Goldens move** — every world that runs research with population change: the switch-off twin returns the old pin through
  `SchemaProjection.V37` (`Adr054AttributionTests`):

  | Pin | Old → new | Cause | Control |
  |---|---|---|---|
  | `SnapshotTests.FoundedGoldenHash` (= ci.yml `FOUNDED_GOLDEN`) | `8abc32b0…` → `1f9f3d4f…` | value + layout | twin → old (V37) |
  | `DrivenGoldenTests.Golden` | `af5be6a1…` → `f23e0dcd…` | value + layout | twin → old |
  | `FirstReignTests.PostR1Golden` | `6b9571cd…` → `389e0526…` | value + layout | twin → old |
  | `AiFoundingAttributionTests.S1cTurn300` | `2a6a61d3…` → `02500b4a…` | value + layout | twin → old |
  | `SnapshotTests.ToyGoldenHash` | `1e215573…` → `175f55be…` | layout alone (no research) | V37 projection → old |
  | `AiFoundingAttributionTests.PreTranche2Turn300` | `b8646444…` → `51b33ea0…` | layout alone (older content) | V37 projection → old |
  | `DiscoveryLayoutAttributionTests.PreM6FoundedGolden` | `9ec11ace…` → `0dadbdfa…` | layout alone | V37 projection → old |
  | `DiscoveryLayoutAttributionTests.PreM6AiTurn300` | `b4f6616c…` → `5258fe16…` | layout alone | V37 projection → old |
  | `AiEmpireIntegrationTests` (road, levy, A3) | 132/133, 376/377, A3 341 → 134/135, 379/380, A3 343 | value | twin → old values |
  | `ConstructionHoldsV33FixtureTests.PremiseIV` (`sim inspect` of the v33 session) | matches through turn 2, differs from 3 → through 1, differs from 2 | value (the average acts from a holder's second step) | premises I–III on the R4 twin |
  | schema pins (version 37 → 38, trailer +4 bytes, readable set, diff layout, migration notes) | — | layout | `ResearchPopulationsSchemaV38Tests` |
- **Saves.** v37 saves load (migrated: empty `ResearchPopulations`); a recorded UI session from an earlier build replays into a
  different world and is refused by the existing trace check (the candidate-trial §E precedent).

## 7. What this does not decide

The pacing targets (D-063 R6), any non-population input to RP or CP (architecture §19.5), the military-population weight
(ADR-030 §6), and whether the benefit operands should be re-tuned.

## 8. RC-1's consequence for `aircraft` — a DIRECTOR DECISION (measured; no option chosen)

RC-1 (Director-authorized 2026-10-27, `docs/m7-plan/aviation-progression.md` §10; TC-2 in `tree-closure.md`) retagged
`aircraft` A9 → A8 and re-scored it with the A8 priors (content cost 66,900 → 33,450). It was shipped with this change.

**Measured consequence.** Aircraft now completes **80–107 turns after Age 9 entry** (shipped and candidate, seeds 1, 2, 42,
7; before RC-1: 31 turns after, one turn after `glider`). The A8 tag removes the Age-sweep wait, but the A8 pacing scale
makes it **5.7–6.8× dearer** in effective RP — 33,450 × 0.775796 = 25,950 RP (shipped) or × 0.926249 = 30,983 RP
(candidate), against 66,900 × 0.068141 = 4,559 RP under the A9 scale — and no AI goal asks for it, so the AI's
cheapest-first order defers it 50–82 turns past `glider`. RC-1's aim (Flight I in late A8) is not met by the retag alone.

**Options, with what was measured:**

| Option | Measured | Result |
|---|---|---|
| A. Keep RC-1 as shipped | the calibrated runs (`per-turn-cp-recalibration.md` §6) | aircraft +80 … +107 turns after A9 entry; Age 8/9 pacing within targets as reported |
| B. Per-node cost: aircraft magnitude 8.5 → 6.0 (content 5,910; A8 effective ≈ 4,585 RP, the pre-RC-1 effective cost) | shipped s3 + `data/per-turn-cp/scales/aircraft-m25.json`, four seeds | aircraft completes ONE turn after `glider` again, i.e. +31 … +35 turns after A9 entry (as before RC-1); technology A8/A9 within ±1 turn; pincer unchanged. Below the ADR-045 A8 priors (it needs breadth 0, novelty 1.0): a rubric exception or a `calibration_adjustment`. `glider` itself (+30 … +34 after A9 entry) then binds. |
| C. AI goal: the M7 recruitment goal (aviation-progression.md §10, "not a graph change") or a milestone naming `aircraft` (and `glider`) | not measurable on this tree (no such goal exists) | expected to pull both nodes forward; its own ADR, switch and four-seed measurement |
| D. Revert RC-1 (A9 tag, 66,900) | the archived pre-RC-1 runs (before ADR-054) | +31 after A9 entry; the sweep keeps aircraft ineligible for 89–121 turns of A8 |

B was measured on this tree (ADR-054 on, shipped scale); D is from the archived pre-RC-1, pre-ADR-054 runs. C is the aviation plan's own follow-on and is not implemented.
