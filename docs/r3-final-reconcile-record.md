# R3 — final R2 reconciliation record

Branch `m5i-r3-final-reconcile`, cut from `m5-integration` @ 75794d0. The Director's R2-final instructions are
binding. Labels used below: **RATIFIED** (a Director ruling), **IMPLEMENTED** (shipped and tested here),
**MEASURED** (observed on this branch by this stream), **INFERRED** (an agent's implementation choice, not a
ruling), **DEFERRED**, **OPEN**.

## 1. Turn-1 food (§1, §19): STOPPED. No founding population passes the ratified bands

The ruling (RATIFIED) is to find, by measurement, a smaller founding population at which forager-only turn-1 food
(`farming.preCultivation` ON) passes the ratified bands. Only the founding-population parameter may change.

**Method.**
- `founding.cohortCounts` was re-apportioned to total N, using the kernel's stable shares and largest-remainder
  rounding (the T4.19 derivation).
- `founding.foodStore` was scaled at 15 per founder, so that per-capita founding food stays the same as at 400.
- `preCultivation.enabled` was set to true.
- The battery filter was CalibrationBattery, FoundingDemographics, PopulationTests and FirstReign (33 tests),
  Release build.

**Results (MEASURED).** The table excludes golden-pin failures, which are expected to move with any change.

| N per settlement | fed growth seed 1 / 2 (band 0.0005–0.001) | dev Malthus seed 42 / 7 (tooth: 0 starvation) | founding stable regime (starvation 0) | other |
|---|---|---|---|---|
| 400 (shipped) | −0.000306 / −0.000269 | 1,248 / 1,508 deaths | 29 deaths | FedWorld 1,561→28; Unfed, MalthusLite, Reconciliation red |
| 200 | −0.000380 / −0.000139 | 672 / 822 deaths | turn-1 ratio 1.139, outside [0.95, 1.10] | Era-continuity red; Pyramid red |
| 100 | +0.0000093 / −0.000144 | **Ledger exception** (amount −1) / extinct world | 3 deaths | Pyramid 0/0/0/0; era-continuity red |
| 50 | −0.000202 / −0.0000736 | 371 deaths / extinct world | red | era-continuity red |
| 400, switch OFF (control) | in band | pass | pass | 33/33 green |

**Cause (MEASURED with a diagnostic run on canonical seed 1, N=100, which was not committed).**
- The land side is not binding. Effective arable area is about 184,000 fertility-weighted km² across 12
  settlements, which is roughly 184,000 person-years of forager food a year against about 1,400 people.
- The labour side binds instead: 2.0 per gatherer × the 0.55 Farming share × the adult share. That is a
  **per-capita** ceiling, and it sits below consumption at any population size. Shrinking the founding population
  cannot raise it.
- In the battery runs no polity issues research orders: the player has no orders and `aiEmpires` is 0. Zero nodes
  were completed in 60 turns, so Agriculture never arrives to lift the ceiling.

**Outcome.**
- The switch stays committed OFF. `cohortCounts` and `foodStore` are unchanged, and so are bands and constants.
- **OPEN for the Director.** Possible next steps are to measure the forager labour rates against the reference
  class, to re-derive the never-ordered default sector mix for a forager economy, or to make a calibration rig
  that researches. None of these is a founding-population change, so none was done here.
- **Defect found (OPEN).** At N=100, the dev seed-42 run threw `ArgumentOutOfRangeException: Ledger amounts are
  never negative (−1)` with the switch ON. It does not occur with the shipped data. It should be traced before the
  switch is ever enabled.
- Agriculture's downstream change exists in code: `ProductionSystem.Farm` uses the CR-003 26.0/5.0 rates once
  `activity.farming` is eligible. It is only observable with the switch ON, which is the configuration that
  failed above.

## 2. Revolt → new AI polity; knowledge is monotonic (§0A, §2, §4): IMPLEMENTED

**Revolt (RATIFIED, IMPLEMENTED in `RevoltSystem`).**
- A revolting settlement's control row passes to a NEW polity, `CommandSource.Ai`. Its id is one above the
  roster's maximum; when several settlements revolt at once, they are handled in settlement-table order.
- The new polity is given a complete copy of the former polity's `ResearchCompleted` rows at that instant
  (`KnowledgeTransfer.MergeInto`). The parent loses nothing.
- No link is kept, so the two research independently from then on (tests in `KnowledgeMonotonicTests`).

**Implementation choices (INFERRED).**
- **What is copied:** knowledge means completed nodes. In-progress RP, fired Eurekas and credit provenance are
  research effort, so they stay with the parent and are not copied.
- **No capital:** the new polity has no capital row. M4-A makes a capital-less empire representable, and §5 rules
  out inventing one.
- **Single-settlement polities:** a settlement whose controller holds no other place does not revolt. Without this
  guard, a grievance still above the uprising line would split the same settlement again every turn.
- **Research pace:** the new polity researches as a normal polity, on its own population's RP curve. Being one
  settlement already makes it slower. The 0.25 city-state pace applies only to uncontrolled settlements (local
  holders).

**Annexation (RATIFIED, IMPLEMENTED as a domain operation).**
- The operation is `KnowledgeTransfer.MergeInto(table, annexed, owner)`, a union that is idempotent and never
  removes a row. It is tested directly.
- No annexation or conquest path exists in the simulation, so nothing calls it yet. Conquest is DEFERRED to the
  Battle Layer (M7 in the 2026-10-03 roadmap) and later politics.

**Consequence (OPEN): CR-019.** Revolt no longer produces stateless settlements, which removes the only producer
for T4.5's raider gate in founded worlds.

**R2a city-states (unchanged).** An uncontrolled settlement still researches under its local key at quarter pace.

## 3. Capital loss (§5): DEFERRED succession, invariants IMPLEMENTED as tests

Two tests in `UnrestTests` cover a 99 % levy that raises the capital while the ruler researches:

- `CapitalLoss_ErasesNoKnowledge_ResetsNoResearch_AndLeavesNoTaxSource` checks that:
  - the completed set never shrinks, on any turn;
  - per-node progress never falls;
  - the ruler is not extinct;
  - no successor capital is assigned;
  - no remaining settlement is taxed.
- `CapitalLoss_SaveLoadAndReplay_AreExact` checks twin-run hash equality and an exact Snapshot round trip at the
  turn the capital falls.

**OPEN / DEFERRED:** capital succession and post-capital-loss government continuity.

## 4. Food-sector label (§15): IMPLEMENTED

`LabourActivities.CapabilityLabel` names a sector by the completed nodes that make its researched identity
eligible, or by the baseline identity when there are none. For example, a settlement that knows only root crops
reads "Root crop …", not "Farming". The R2 city-state preview uses it. Tests are in `FoodSectorLabelTests`. The
economy is untouched.

## 5. Items confirmed unchanged (RATIFIED as accepted)

- **Tax / unrest brake:** unchanged.
- **Weather × roads independence:** `HarvestWeatherGeographyTests.ChangingTheRoadTopology_…` is unchanged.
- **Military milestones:** stay PENDING (`AgeProgressionTests`). Recruitment belongs to the Battle Layer, M7 in the
  2026-10-03 roadmap.
- **Trade:** node 426 `trade`, Technology trunk, A3, 880 RP, prereq `token_counting AND (donkey OR camel OR sail_square)` (clay-token counting AND (donkey OR camel OR square sail)). Gated through `TradeQuery.CanTrade`.
- **Deferred entities:** all 115 stay DEFERRED (`docs/deferred-entity-realization-plan.md`).
- **Quarantined corridors:** `densityPerArableKm2` and `migrationGrossPerDecade` are pre-existing quarantines and
  were not retuned.

## 6. Goldens

None moved. The founded pin 1368df9f…, first-reign pin 680a20c5… and driven pin efeec45d… all hold on this
branch. No revolt fires in the pinned runs. The founded ordered twin does revolt at turn 97, and its turn-exact
assertion now expects the new AI polity.

## 7. Verification (MEASURED, Release, this branch at the commit that adds this section)

- **Sim.Tests:** 1449 passed, 0 failed, 6 skipped (1455 total). The full CalibrationBattery is included and is
  green, with the forager switch OFF.
- **Sim.Ui.Tests:** 473 passed out of 473.
- **Gates:** all green:
  - banned-constructs
  - read-isolation
  - research content audit, calibration report and gameplay-unlock audit (`--check`)
  - read-only proof
- **Cross-process determinism:** the `ci.yml` T0.9 step was run locally. It passed on every leg: orderless 400,
  ordered vs. replay 400, founded, and the AI-empire leg over 600 turns.
- **Bench:** `sim bench --seed 42 --turns 300 --founded` took 34,859 ms in total; the revolt phase took 58.9 ms.
- **R2 previews:** regenerated with `docs/architecture/r2-previews/render-previews.sh`. Only the city-state card
  changed, and it now reads "Food sector: Root and tuber cultivation". The turn-1 card still reads
  "Gathering · makes grain", because the forager switch stays OFF (§1, OPEN).
