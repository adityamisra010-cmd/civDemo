# R4a — M5 closure (simulation) record

Branch `m5i-r4a-closure`, cut from `m5-integration` @ 12754e3. The Director's R4 instruction (2026-10-04) is
binding. This stream covers items 1–6 and 8. Item 7 (the roadmap documents) belongs to the sibling stream R4b.
Labels: **RATIFIED**, **IMPLEMENTED**, **MEASURED** (observed on this branch by this stream), **INFERRED**,
**DEFERRED**, **OPEN**.

## 1. Turn-1 food: forager switch ON at 2.0 per km² and 4.3 per gatherer (IMPLEMENTED)

**Audit (MEASURED).**
- `ProductionSystem.Farm` takes `min(land side, labour side)`:
  - the land side is the arable km² × the yield per km²;
  - the labour side is the farm labour × the output per worker × the tool factor.
- With `farming.preCultivation` ON, a settlement whose knowledge does not make `activity.farming` eligible uses
  the forager rates. Otherwise it uses the cultivated CR-003 rates (26.0 / 5.0).
- At the R2a forager values (1.0 / 2.0), the labour side binds: 2.0 × the 0.55 never-ordered food share × the
  adult share is below consumption at every population size (the R3 finding, reproduced).

**Sweep (MEASURED).**
- Founding population: the shipped 400 per settlement.
- Battery: CalibrationBattery, FoundingDemographics, PopulationTests and FirstReign (33 tests), Release build.
- The table omits golden-pin failures and the `UnfedWorld` rig. That rig zeroed only the cultivated yield; it now
  switches the forager layer off as well.

| per gatherer | per km² | fed corridors s1/s2 | dev seed 42 | dev seed 7 | all-food rig (900 t) |
|---|---|---|---|---|---|
| 2.0 | 1.0 | red | starvation | starvation | — |
| 2.5 | 1.0 | red; era continuity red | starvation | starvation | — |
| 3.0 | 1.0 | red | starvation | starvation | — |
| 3.5 | 1.0 | 0.342 / 0.354: inside the band [0.15, 0.6], below the quarantine window | 1,976 starved | 1,813 starved | starves |
| 4.0 | 1.0 | green | 320 starved | 7,388 starved | starves |
| 4.0 | 2.0 | green | 200 starved | 323 starved | starves |
| 4.0 | 4.0 | green | 200 starved | 323 starved | 0 starved |
| 4.25 | 1.0 | green | 586 starved | 12,105 starved | starves |
| 4.25 | 2.0 | green | 0 starved; final population 79,196 (envelope) | **5 starved** | starves |
| 4.3 | 1.5 | green | 0 starved; 80,881 (envelope) | 418 starved | starves |
| **4.3** | **2.0** | **green** | **0 starved; 80,937 (envelope)** | **0 starved; migration envelope** | starves |
| 4.35 | 2.0 | green | 0 starved; migration envelope | 0 starved; migration envelope | starves |
| 4.4 | 2.0 | green | 0 starved; migration envelope | 0 starved; migration envelope | starves |
| 4.5 | 2.0 | green | 0 starved; migration envelope | 0 starved; migration envelope | starves |
| 4.5 | 4.0 | green | 0 starved; migration envelope | 0 starved; migration envelope | 0 starved |

"Envelope" marks a recorded-trajectory pin. These are re-pinned like goldens; they are not ratified bands.

**Chosen values: 4.3 per gatherer and 2.0 per km².** These are the smallest values on the measured grid at which
every ratified tooth passes: zero starvation and zero crashes in the dev worlds, and every corridor band.

- **Per gatherer, 4.3.** Spread over all adults, 0.55 × 4.3 = 2.37 person-years per adult. That is inside the
  forager producer range, where a prime-age forager produces roughly 1.7–3 times their own consumption
  (Kelly 2013, *The Lifeways of Hunting and Gathering Peoples*; Kaplan, Hill, Lancaster & Hurtado 2000,
  *Evolutionary Anthropology* 9:156).
- **Per km², 2.0.** This is the top of the R2a reference range for the richest riverine foragers, 0.5–2 per km².
  The sim's f = 1.0 km² is river-valley floor.
- **Agriculture is unchanged.** It is still researched, and nothing agricultural exists at founding. The
  cultivated rates raise the land ceiling 13× (26 vs 2) and output per worker by 16% (5.0 vs 4.3).
  `TurnOneActionSurfaceTests.RootCropComplete_…_CultivatedHarvestExceedsGathered` measures the larger harvest.

**Smaller founding population: 100 per settlement at 4.3 / 2.0 (MEASURED).** This pairing is worse. Fed corridor
density is 0.138 / 0.165, and seed 1 falls below the band floor of 0.15. Dev final populations are 21,074 /
27,768. The shipped 400 is the better pairing and stays.

**Calibration and corridor movements (MEASURED).**
- `dev.migrationGrossPerDecade` quarantine envelope, re-pinned:
  - seed 42: 8.34E-05 → 2.486E-04;
  - seed 7: 1.02E-04 → 2.884E-04.
  - Both are still below the floor of 0.001, so the band has not moved.
- Dev final-population envelope: [82k, 138k] → [71k, 119k], on 80,937 / 105,841. Peak equals final (monotone),
  and starvation is 0.
- The all-food 900-turn rig (`Reconciliation_FromLedgerAlone`) now reaches the forager land ceiling and starves.
  Its original vacuity guard is restored, which follows the T4.21-4 precedent. The battery quarantines of CR-003
  remain untouched and open.

**UI (IMPLEMENTED).**
- `LabourActivities.HarvestsWildFood` is the single predicate that both production's yield choice and the
  Policy panel read.
- The turn-1 Farming sector now reads "Gathering — makes wild food (grain)". Internally it is still the grain
  good, and the economy model is unchanged.
- The R2 previews have been regenerated.

## 2. Ledger crash (IMPLEMENTED)

- **Reproduction (MEASURED).** Forager switch ON at 1.0 / 2.0, 100 founders per settlement, dev seed 42. Turn 853
  throws `Ledger amounts are never negative (−1)` from `ConsumptionSystem.Consume`.
- **Violated invariant: a consumption request is never negative.**
  - Each non-staple food passes its **exact** shortfall (`exactDemand − eaten`) to the staple. That is a signed
    quantity.
  - While the non-staple banks a sub-unit fraction in its own remainder, that fraction is lent to the staple.
  - When the banked remainder later pays out a whole unit, the lend is repaid as a negative amount.
  - In a settlement whose demand had collapsed, the repayment exceeded the staple's whole request. Flooring the
    result gave −1.
- **Fix.** When a request is negative, nothing is asked of the store and no flow happens. The credit is carried
  exactly in the staple's remainder and is settled against its next request. There is no clamp, and nothing is
  dropped.
  - Every non-negative request is computed exactly as before, so no shipped world moves.
  - Measured: the golden, FirstReign, Population and Consumption filters were 63/63 green with no pin moved.
- **Regression.** `SubstitutionCreditRegressionTests` runs the crash configuration for 900 turns. It asserts:
  - a credit was carried, so the test is not vacuous;
  - only the staple ever carries a credit, and it stays above −3;
  - the conservation audit is exact.
  - Measured: the test fails with the crash when the fix is reverted.

## 3. Tax (IMPLEMENTED: a minimal change; MEASURED)

**Before (R2b, MEASURED).**
- Dignity was `1 − r`.
- The tax's grievance is the stock × the share of the shortfall that lifting the levy would close. Under CES
  complementarity, that attribution gives a well-provided seat the **whole** stock and a homeless seat **none**
  of it.
- Result: a 99% levy raised the well-provided seat at turn 6 and never raised the poorly-off seat. The offsets
  worked in reverse, and for a provided settlement 99% was a de facto deterministic revolt.

**Change.**
- Dignity = `1 − r × (1 − m × P)`.
  - P is the class's provision: the D-035-B CES aggregate of its other bound needs, which today are sustenance,
    shelter and comfort/amenities.
  - m = needs.json `unrest.taxBurdenOffsetMax` = 0.5. This value was CHOSEN.
- This is a coefficient inside the resolution equation. It is exactly `1 − r` when untaxed, when P = 0, or when
  m = 0, so untaxed worlds are bit-identical.
- Nothing else changed: no new revolt rule and no change to protest, uprising or attribution.

**Measured.** Founded seat, 80 turns; the 300-turn runs give the same peaks. G is the tax grievance; protest
onset is 15 and uprising 50.

| arm | rate | R2b: revolt turn / peak G / peak p | R4: revolt / peak G / peak p |
|---|---|---|---|
| well-provided (stocks and housing topped up) | 0 | none / 0 / 0 | none / 0 / 0 |
| | 40 | none / 13.2 / 0 | none / 5.5 / 0 |
| | 70 | none / 32.8 / 0.508 | none / 11.4 / 0 |
| | 99 | **turn 6** / 54.7 / 1.0 | **none** / 19.1 / 0.118 |
| natural seat | 40 | none / 8.5 / 0 | none / 4.6 / 0 |
| | 70 | none / 23.7 / 0.249 | none / 10.6 / 0 |
| | 99 | **turn 24** / 51.2 / 1.0 | **none** / 19.9 / 0.139 |
| poorly-off (homeless) | 40 / 70 / 99 | none / 0 / 0 | none / 0 / 0 |

Lower offsets were measured for the natural and well-provided arms at 99%:

| m | well-provided: peak G / peak p | natural seat: peak G / peak p |
|---|---|---|
| 0.3 | 33.2 / 0.52 | 30.1 / 0.43 |
| 0.2 | 43.8 / 0.82 | 38.4 / 0.67 |

At m ≤ 0.3 the well-provided seat still carries **more** tax grievance than the natural seat. That is the
attribution share working against the offset. m = 0.5 is the smallest measured value at which the provided seat
carries less (19.1 vs 19.9).

**Verdict.** At 99% the levy is extremely burdensome: happiness reads 1/100, and protest drags output. It is no
longer a deterministic revolt. The R2b reading is kept as the test control (`NoOffset`).

**OPEN for the Director:** a poorly-off settlement never protests the levy. R2b's attribution assigns its
grievance to its other shortfalls. Correcting that needs the attribution itself redesigned, which was not done.
Under the shipped values no measured arm rises from tax alone within 300 turns.

## 4–6. Rulings and deferrals

- **D-048.** The knowledge-persistence rulings are recorded as RATIFIED in `docs/d048-knowledge-persistence-rulings.md`,
  with a pointer note appended to the R3 record.
  - Ruling 5 now also holds when every place of a ruler rises on the same turn: the capital is kept, or else the
    first place in table order. Tests: `KnowledgeMonotonicTests.*_D048`.
- **CR-019.** Status note appended: DEFERRED to the military and political milestones. No uncontrolled
  settlement is manufactured.
- **Not implemented (DEFERRED):** conquest, full annexation gameplay, capital succession, revolt recovery, the 115
  deferred entities, later-Age military, full Trade, the Battle Layer and the M6 research redesign.

## 7. Goldens (MEASURED)

| pin | OLD (R3) | NEW | cause | control |
|---|---|---|---|---|
| founded s42 t300 | 1368df9f… | 02c7f9eb0d08bf0ab31e6a9b0afb64041e4283e1fc4af1d4fbc94c8fa7b10ef2 | forager layer | `TestConfigs.PreForager` returns OLD |
| first reign t40 | 680a20c5… | 74a97abc39d1191061a2c39748c870faf62d308a8d2cc7b3d28feff8b35e4419 | forager layer | same |
| driven s42 t300 | efeec45d… | 3a9f007aeeb6dcd9c28efd3c47492b66d19a863057f5cfcba80c3d0c52ca12dd | forager layer | same |

- The ledger fix and the tax offset move none of these pins: there is no negative staple request, and no run is
  taxed.
- `ci.yml` FOUNDED_GOLDEN has been moved. Every older layer twin also strips the forager layer.

## 8. Other measured movements caused by the forager layer (re-pinned with history comments)

**Re-pinned values (MEASURED):**
- **Founded ordered twin** (`FoundedHarnessTests`): settlement 0 now revolts at turn 58, where it revolted at 97.
  The 30% labour swing of turn 33 starves it on wild food. The later orders are refused.
- **AI empire** (`AiEmpireIntegrationTests`):
  - Age decided and in force: 235/236 → 238/239;
  - road: 141/142 → 143/144;
  - levy: 537/538 → 546/547. This is still inside the ci.yml 600-turn leg.
- **Dev A2 eligibility** (`AiAgeAdvancementIntegrationTests`): AI 385 → 394, player 338 → 346.
- **Artisan latch** (`ClassSystemTests`): 70 → 71.
- **Glass Box emptying turn** (`GlassBoxUiTests`): 11 → 7.
- **Driven world dwelling decay** (`WorldReconciliationTests`): decay returns, first on turn 162, with 85 dwellings
  over 22 turns. The positive sample is restored.
- **Store-loss vacuity floor** (`StoreLossTests`): 2,500 → 2,000; 2,463 was measured.

**Rigs that hand-compute from, or were calibrated on, the cultivated rates.** These now run on
`TestConfigs.PreForager`:
- the land-capped weather rig;
- the PathBuild order rig;
- the DemographyRetune rigs;
- InstitutionEffects and InstitutionBrake;
- the NeedsGrievance famine contrast;
- the Unfed world.

## 9. Validation (MEASURED, Release, at 9648f7b)

- **Sim.Tests:** 1458 passed, 0 failed, 6 skipped (1464 total). This includes the CalibrationBattery, the
  save/load battery, the replay tests and the attribution controls.
- **Sim.Ui.Tests:** 473 passed out of 473.
- **Gates, all OK:**
  - banned-constructs;
  - read-isolation;
  - readonly-proof;
  - research content audit, calibration report and gameplay-unlock audit (`--check`).
- **ci.yml determinism legs, run locally:**
  - orderless, 2 processes over 400 turns;
  - ordered vs. replay over 400 turns;
  - founded, 2 processes over 300 turns, matching the pinned golden 02c7f9eb…;
  - founded ordered vs. replay over 300 turns;
  - AI-empire leg over 600 turns: 2 processes, replay and inspect (`reproduction VERIFIED`, 193 orders, kinds 3–8).
- **Bench:** `sim bench --seed 42 --turns 300 --founded` took 29,860.75 ms, against 29,167.07 ms at 12754e3
  (+2.4%). The two runs shared the machine with other agents.
