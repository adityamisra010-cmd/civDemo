# Tree-closure integrity report

Content: `Sim.Data/content/research.json` — 525 technologies, 480 civics, 135 entities.

| Check | Result | Detail |
|---|---|---|
| 1 duplicate ids / keys | PASS | 1005 ids unique; keys unique per tree |
| 2 prerequisite references | PASS | every prerequisite names an existing node |
| 3 reachability (no cycle, no dead prerequisite) | PASS | all 1005 reachable from 10 roots |
| 4 Age order of prerequisites | PASS | no node requires a node of a later Age |
| 5 unlock references | PASS | 128 entity unlocks all name one of 135 entities; every entity-level requirement names an existing node or entity |
| 5b entities not unlocked by a node | INFO | 6 available from the start (requires null): activity.fishing, building.granary, building.workshop, inst.burial, recipe.toolmaking, recipe.weaving; 1 gated by their own requirement: inst.library (archive AND scribal_school) |
| 6 names and descriptions | PASS | all 1005 nodes named and described |
| 7 effects: realized, declared future, a live gate or a live unlock | PASS | every node covered; 7 carry no effect list but are live through a gate or an unlock (electricity_generation [unlock], motor_road [unlock], ore_prospecting [gate], radio [unlock], taxation [gate], track_road [unlock], trade [unlock]); authoritative: Tranche2ContentTests.EveryNode_HasARealEffect_OrADeclaredFutureEffect (InfoQuery verdict) |
| 7b future-only nodes (effect waits on a future system; shown 'not simulated yet') | INFO | 23: aluminium, cloud_computing, desalination, fulling_mill, fusion_power, integrated_alliance_command, integrated_circuit, kiln_updraft, nuclear_power, oil_press, papyrus, petroleum_refining, pilgrimage, plank_boat, road_traffic_regulation, rocket, saddle, solar_pv, sulphuric_acid, telephone, trace_italienne, wheel_spoked, wind_turbine_modern |
| 9 milestone references | PASS | 41 research-milestone nodes all exist |
| 8 orphans (no dependent, unlock, realized effect or milestone) | INFO | 3 (future-declared dead ends): ['integrated_alliance_command', 'pilgrimage', 'road_traffic_regulation'] |
| 10 cost model consistency | PASS | all 1005: cost = content_cost, magnitude = the sum of its factors, cost = round(U x 2^magnitude) to tens (17 authored one rounding step (+10) above, raw value just under a 5 boundary, max 0.07 %: woodblock_print, coke, puddling, power_loom, screw_propeller, iron_hull, haber_bosch, telegraph, superalloy, xray, skyscraper, cmos_vlsi, superconductivity_applied, composites, nuclear_fusion_research, nanotechnology, space_solar) |

**Result: no blocking defect.**
