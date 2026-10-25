# Final-Age repeatables — proposal (D-064 review; NOTHING APPLIED)

**Status:** proposal for the Director. `research.json` is unchanged. Grounding: `online-research.md` (cited game data:
Civ IV/V/VI/VII, Stellaris 4.5.2, Old World), `data/live-map.json` (which operands are read today) and the shipped
content at commit `a37b3fd`.

## Why change them

- **Too cheap for the income they meet.** Level 1 of today's 13 repeatables costs 570–2,280 RP (technology) and 145–290 CP
  (civics) against ~25,000 RP and ~1,225 CP per turn in Age 9 (reference empire, seed 42): 2–9 % of one turn's RP and 12–24 %
  of one turn's CP.
- **Growth is far steeper than any shipped game.** Level n costs BaseCost × 1.2^(n−1) while each level closes 10 % of the
  remaining gap (benefit ×0.9 per level), so the price of a unit of benefit grows ×1.33 per level: 13× by level 10, 236× by
  level 20. Shipped games are flat (Civ IV/V/VI Future Tech) or linear (Stellaris +10 % of base per level, Old World +50 %):
  2.8–5.5× at level 10. No shipped game uses geometric growth.
- **Two repeatables compound income.** `frontier_computation` (+RP per person) and `civic_participation` (+CP per person)
  raise the income that buys further levels — the one pattern every shipped design avoids.
- **Two are inert today.** `frontier_military` reads formation sight (formations sit at their capitals until M7 movement)
  and `frontier_propulsion` reads exploration radius (the map is known long before Age 9).
- **The civics tree has 3 repeatables against 10 in technology**, and none of them for welfare, revenue, mobility or trade.

## Proposed set (both trees; every operand live today except where marked)

| Node | Tree | Status | Category | Operand (live reader) | Notes |
|---|---|---|---|---|---|
| frontier_crops | tech | keep | food | farming.yieldPerArableKm2PerYear | |
| frontier_energy | tech | keep | production | production.outputPerExtractorPerYear | |
| frontier_materials | tech | keep | housing / construction | housing.buildTimberPerDwelling | |
| frontier_medicine | tech | keep | health | demographics.mortalityPerYear | |
| frontier_climate | tech | keep | food security | harvestVariance.sigmaLogYield | |
| frontier_cyber | tech | keep | stability | society.unrestOutputDrag | |
| frontier_launch | tech | keep | trade | trade.costPerBulkCostUnit | proxy (no space programme) |
| frontier_computation | tech | **retarget** | trade / markets | trade.gapClosingFraction (result ≤ 0.95) | was +RP: compounding income |
| frontier_propulsion | tech | **retarget** | reach | catchment.hinterlandRadiusKm | was exploration radius: inert in Age 9 |
| frontier_military | tech | **hold for M7** | military strength | fog.formationSightNodes (dormant) | "stronger weapons" needs a combat operand; M7 battle layer adds it — retarget then |
| frontier_logistics | tech | **new** | food | consumption.grainSpoilagePerYear | cold chain and preservation |
| institutional_reform | civics | keep | loyalty / administration | governance.authorityDecayCostUnits | administrative reach — the nearest live loyalty lever |
| social_policy_research | civics | keep | happiness | society.grievanceRelief | |
| civic_participation | civics | **retarget** | revenue / legitimacy | governance.taxExtractionResponseMax | was +CP: compounding income |
| public_health_programs | civics | **new** | welfare | demographics.starvationMortalityMaxPerYear | famine safety net |
| education_reform | civics | **new** | social mobility | mobility.promoteRatePerYear | |
| migration_policy | civics | **new** | mobility | migration.baseRatePerYear | |
| trade_agreements | civics | **new** | trade | price.stockReleaseRatePerYear | stocks reach markets faster |
| strategic_reserves | civics | **new** | food security | granaries.famineReductionMax | |
| urban_planning | civics | **new** | housing | housing.personsPerDwelling (cap 0.5) | |

Result: 11 technology and 9 civics repeatables, covering food, production, health, stability, happiness, loyalty,
revenue, trade, housing, mobility and welfare. Military strength is recorded as an M7 dependency rather than faked with a
dormant operand.

## Proposed pricing

| Parameter | Today | Proposed | Basis |
|---|---|---|---|
| costGrowth g | 1.2 | **1.05** | online-research.md §8–9: unit-benefit price ×4.0 by level 10 (shipped range 2.8–5.5×); level 10 costs 12.6× BaseCost in total against 26.0× at g 1.2 |
| Level-1 BaseCost (technology) | 570–2,280 RP | **≈ 31,000–39,000 RP** (1.2–1.5× rocket, the costliest finite Age 9 technology, 25,789 RP) | the 'costliest final node' anchor; ≈ 1.2–1.6 turns of Age 9 RP |
| Level-1 BaseCost (civics) | 145–290 CP | **≈ 490–620 CP** (1.2–1.5× fascist_state, 411 CP) | ≈ 0.4–0.5 turns of Age 9 CP |
| Benefit per level | 10 % of the remaining gap | keep | already diminishing; the cap bounds it |

**Dependency on the overflow decision.** The research's second anchor — a first level worth 8–10 turns of income — cannot
be met under today's pacing: Age 9 content is scaled ×0.068 (technology) and ×0.0087 (civics), so even the largest
magnitude the cost formula allows (13 → 757,000 content RP → 51,500 RP) is about two turns of income. Both anchors only
agree once the pacing is recalibrated, which the overflow comparison shows is needed anyway if overflow is adopted. So:
apply the operand retargets, new nodes and g = 1.05 now if approved; set the level-1 magnitudes in the same change as the
pacing recalibration.

## What each new node needs (when approved)

A research.json node (Age 9, `repeatable.levels: true`, effects, costRationale factors), a corpus entry and knowledge text
(the KnowledgeTextsTests count), the Tranche2ContentTests repeatable assertions, and a viewer refresh. The loader already
supports levelled repeatables in both trees (ADR-049).
