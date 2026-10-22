# D07 — Industry, construction & infrastructure: lane notes

**Status: PROPOSED** design for Director review; nothing here is ratified or implemented. Canonical data: `data/proposal-parts/D07-industry-construction.json`. Labels: MEASURED (read from code, content or a run, cited) · RESEARCHED (external source, listed in §13) · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED.

## 1. Summary

PROPOSED. D07 reviewed its 54 nodes: 25 kept, 25 rebalanced (D07 uses 'rebalance' for prerequisite repairs; 9 of them change factors), 8 relocated (4 also rebalanced) and 1 split, with 4 new nodes. (1) Coupling: seven medieval craft nodes (fulling mill, spinning wheel, treadle loom, roller gin, crank, verge escapement, pound lock) leave the Engineering subtree for the trunk, because they waited on the research stage (measured ~1532 CE) although artisans and millwrights spread them centuries earlier; all their prerequisites are trunk. (2) Causality: chronology ladders and analogy edges are replaced by knowledge actually needed - the water-supply ladder (qanat -> tunnel -> channel), the textile ladder (shuttle -> jenny -> frame -> mule -> power loom becomes wheel -> jenny / wheel + water -> frame; jenny + frame -> mule; shuttle + power -> loom), type founding (now the press's precondition, moved to the trunk with it - D05 accepted), interchangeable parts (no type-founding analogy), the arch (mudbrick, as its own anchor says), lime plaster (Natufian, before mudbrick), the verge escapement (water clock, not crank - D03's request), the pound lock (timber gates, not hull joinery), the skyscraper (structural iron, not reinforced concrete). (3) Alternative routes by material, crop or prime mover: wells (stone OR timber), irrigation (cereals OR root crops - D01's request), vaults (concrete OR brick OR ashlar), stone bridges (concrete OR ashlar), the power loom (water OR steam), desalination (distillation OR membranes). (4) Four new nodes: ashlar_masonry (split from stone_fortification; civil stone building no longer waits on military architecture), drawloom (the pattern loom Jacquard automated; silk's first dependant), structural_iron (iron bridges and frames, the real predecessor of the steel-frame skyscraper) and sanitary_engineering (sewerage and filtered water; infra.sewerage_system no longer hangs on germ theory and reinforced concrete). (5) Realization: only stone_dry's Built road class is realized today; water, sanitation, irrigation, fishing and textile-mill entities are T6.11 (M6) candidates with named consumer equations and blockers; castles and walls are M7; bridges, canals, harbours, lighthouses and skyscrapers are M10+.

Validation (MEASURED, in a scratch copy of the package with the D01–D05, D07–D10 and D12 parts present): `tools/integrate.py` reports no problem for D07; `tools/validate.py --no-corpus` reports 0 errors. The only warning touching a D07 node is V17 for `type_founding` (a trunk node at A7, the same justified exception D05 takes for `printing_press`). A separate check with the `EarliestMarkedYear` regex of `ResearchContentTests` found no chronology violation on any must-hold edge touching a D07 node. That check included D05's accepted `printing_press ← type_founding` edge and the pending cotton edits. The trunk-closure, Age-monotonicity, cycle and cost-model checks also pass.

## 2. Dispositions

| disposition | count | nodes |
|---|---|---|
| keep | 25 | cordage, basketry, shelter_hut, hide_working, sewing, wool, mudbrick, timber_frame, stone_dry, spinning_spindle, loom_warp_weighted, qanat, olive_press_beam, water_screw, noria, gearing, silk, canal_navigation, cylinder_boring, slide_rest_lathe, milling_machine, flying_shuttle, cotton_gin_saw, reinforced_concrete, numerical_control |
| rebalance (prerequisite repair; 9 also re-score factors) | 25 | listed in §3 |
| relocate (Engineering → Main Trunk; 4 also rebalanced) | 8 | fulling_mill, spinning_wheel, horizontal_loom (also A6 → A5), cotton_gin_roller, crank_connecting_rod, escapement_verge, canal_lock, type_founding |
| split | 1 | stone_fortification → ashlar_masonry |
| merge / replace / retire | 0 | — |
| new | 4 | ashlar_masonry, drawloom, structural_iron, sanitary_engineering |

Convention: lanes differ on how they label a prerequisite edit (one lane uses `rewire`, D03 uses `relocate`). D07 uses `rebalance` for every prerequisite repair, as D10 does. Each record's `changes` states the old and new prerequisite and cost. Where the conjunct count is unchanged the factors are unchanged and so is the cost.

## 3. Every changed record (old → new)

| key | node | disposition | old prerequisite | new prerequisite | old RP | new RP |
|---|---|---|---|---|---|---|
| 51 | `lime_plaster` | rebalance | `mudbrick AND fire_making` | `fire_making` | 1,760 | 1,240 |
| 54 | `well` | rebalance | `stone_dry` | `stone_dry OR timber_frame` | 440 | 440 |
| 62 | `dyeing` | rebalance | `loom_warp_weighted AND ochre_processing` | `spinning_spindle AND ochre_processing` | 1,240 | 1,240 |
| 68 | `irrigation_basin` | rebalance | `cereal_cultivation` | `cereal_cultivation OR root_crop` | 440 | 440 |
| 69 | `irrigation_canal` | rebalance | `cereal_cultivation` | `cereal_cultivation OR root_crop` | 1,760 | 1,760 |
| 126 | `stone_fortification` | split+rebalance | `stone_dry AND surveying AND (tin_bronze OR iron_bloomery)` | `ashlar_masonry AND surveying` | 3,520 | 2,490 |
| 130 | `aqueduct_tunnel` | rebalance | `qanat AND surveying` | `mining_shaft AND surveying` | 1,760 | 1,760 |
| 131 | `aqueduct_channel` | rebalance | `aqueduct_tunnel AND stone_dry` | `surveying AND stone_dry` | 2,490 | 2,490 |
| 132 | `arch` | rebalance | `fired_brick AND surveying` | `mudbrick AND surveying` | 1,760 | 1,760 |
| 149 | `vault_dome` | rebalance | `arch AND concrete_pozzolan` | `arch AND (concrete_pozzolan OR fired_brick OR ashlar_masonry)` | 3,520 | 3,520 |
| 151 | `bridge_stone` | rebalance | `arch AND concrete_pozzolan` | `arch AND (concrete_pozzolan OR ashlar_masonry)` | 2,490 | 2,490 |
| 152 | `aqueduct_arcade` | rebalance | `aqueduct_channel AND arch AND surveying` | `aqueduct_channel AND arch` | 4,970 | 3,520 |
| 182 | `fulling_mill` | relocate | `trip_hammer AND loom_warp_weighted` | `trip_hammer AND loom_warp_weighted` | 4,970 | 4,970 |
| 183 | `spinning_wheel` | relocate | `spinning_spindle AND wheel_solid` | `spinning_spindle AND wheel_solid` | 4,970 | 4,970 |
| 184 | `horizontal_loom` | relocate+rebalance | `loom_warp_weighted AND gearing` | `loom_warp_weighted` | 4,970 | 2,960 |
| 185 | `cotton_gin_roller` | relocate | `wheel_solid` | `wheel_solid` | 2,090 | 2,090 |
| 208 | `crank_connecting_rod` | relocate | `gearing AND water_mill` | `gearing AND water_mill` | 4,180 | 4,180 |
| 209 | `escapement_verge` | relocate+rebalance | `gearing AND crank_connecting_rod` | `gearing AND water_clock` | 7,030 | 7,030 |
| 212 | `canal_lock` | relocate+rebalance | `irrigation_canal AND mortise_hull` | `irrigation_canal AND timber_frame` | 4,180 | 4,180 |
| 216 | `type_founding` | relocate+rebalance | `printing_press` | `casting_closed` | 8,360 | 8,360 |
| 258 | `interchangeable_parts` | rebalance | `slide_rest_lathe AND type_founding` | `slide_rest_lathe` | 16,730 | 11,830 |
| 261 | `spinning_jenny` | rebalance | `spinning_wheel AND flying_shuttle` | `spinning_wheel` | 8,360 | 5,910 |
| 262 | `water_frame` | rebalance | `spinning_jenny AND water_mill` | `spinning_wheel AND water_mill` | 8,360 | 8,360 |
| 263 | `spinning_mule` | rebalance | `water_frame` | `spinning_jenny AND water_frame` | 5,910 | 8,360 |
| 264 | `power_loom` | rebalance | `spinning_mule AND steam_rotary` | `flying_shuttle AND (water_mill OR steam_rotary)` | 14,070 | 14,070 |
| 266 | `jacquard` | rebalance | `horizontal_loom AND paper` | `drawloom AND paper` | 9,950 | 9,950 |
| 339 | `skyscraper` | rebalance | `bessemer AND electric_motor AND reinforced_concrete` | `structural_iron AND bessemer AND electric_motor` | 66,910 | 66,910 |
| 393 | `desalination` | rebalance | `electricity_generation` | `electricity_generation AND (distillation OR plastics)` | 11,830 | 16,730 |
| 407 | `additive_manufacturing` | rebalance | `numerical_control AND plastics AND computer` | `numerical_control AND plastics` | 16,730 | 11,830 |

Reasons, grouped by kind (every record carries its own rationale, `prereqRationale`, per-factor `costRationale` and `compatibility`):

- **Coupling (relocations).** MEASURED: in the baseline the research stage opens at turn ≈1016 (≈1532 CE), and every Engineering node waits for it. Seven medieval craft nodes therefore became reachable centuries after artisans and millwrights spread them: the fulling mill (~1,000 CE), spinning wheel, treadle loom, Indian roller gin (~500 CE), crank, verge escapement (1280s) and pound lock (984 CE). Their prerequisites are all trunk, so they move to the Main Trunk with Age and cost kept (horizontal_loom aside). `type_founding` follows `printing_press`, which D05 moved to the trunk, so the press can require it. No D07 node is an Age fact, so these moves create no new Age coupling. They do let the AI core goal for A8 progress before the stage, because the crank lies on steam_rotary's closure (engine-audit Q11 gap). Not relocated: A7–A9 artisan inventions (flying shuttle 1733, jenny 1764) stay in Engineering under the V17 convention. This is an open question (§12).
- **Chronology ladders → knowledge edges.** Water supply: `qanat → aqueduct_tunnel → aqueduct_channel` was a date ladder. Tunnels need mining and surveying (Siloam ~700 BCE, Eupalinos ~550 BCE); channels need surveying and masonry (Jerwan ~690 BCE). Textiles: shuttle → jenny → frame → mule → power loom was an induced-innovation ladder (each machine answered the previous bottleneck). The knowledge graph is now: wheel → jenny; wheel + water → frame (roller drafting and the flyer); jenny + frame → mule; shuttle + (water OR steam) → power loom. Workload is conserved: the jenny falls 2,450 RP and the mule rises 2,450 RP.
- **Analogy and context edges removed.** `interchangeable_parts` no longer needs `type_founding`: identical type from a matrix is an analogy, not knowledge the armouries used. `type_founding` no longer requires the press it made possible. `arch` requires mudbrick, as its own anchor says (the Ur mudbrick arch, ~2,000 BCE). `lime_plaster` drops mudbrick: Natufian lime plaster (~10,000 BCE) predates mudbrick. `dyeing` drops the loom: dyed yarn and twined fabric need no loom. `escapement_verge` drops the crank, which has no part in a verge, and takes `water_clock` instead (D03's request). `canal_lock` drops hull joinery and takes timber gates. `skyscraper` drops reinforced concrete (the first steel frames used none; it also made bessemer redundant) and takes `structural_iron`.
- **Redundant conjuncts.** `aqueduct_arcade`'s surveying is implied by aqueduct_channel, and `additive_manufacturing`'s computer by numerical_control. Under the ledger convention (C-10: independent requirements) prereq_complexity falls, so costs fall 29 % and 29 %.
- **Missing knowledge.** `desalination` gains a separation process, '(distillation OR plastics)'; electricity alone desalinates nothing.
- **Split.** `stone_fortification` bundled a construction technique with a military application, so the civil lighthouse (and implicitly every dressed-stone bridge and vault) rested on military architecture. Ashlar masonry is carved out; the retained key keeps the fortification, building.castle and D09's new trace_italienne edge.

## 4. New nodes

| id | name | branch · Age | prerequisite | RP (magnitude) | analog | why it carries weight |
|---|---|---|---|---|---|---|
| `ashlar_masonry` | Ashlar (dressed-stone) masonry | trunk · A3 | `stone_dry AND copper_smelting` | 2,960 (5.0) | fired_brick | The monumental cut-stone tradition (Egyptian pyramids and temples, Greek temples, Inca walls) is a construction capability distinct from military fortification, and civil structures need it: lighthouses, dressed-stone bridges and vaults. It was hidden inside 'Ashlar fortification', so civil building waited on military architecture. |
| `drawloom` | Pattern loom (drawloom) | trunk · A5 | `silk AND horizontal_loom` | 2,960 (5.0) | noria | It is the causal link the tree lacked. Jacquard's head automated the drawloom's figure harness (Bouchon 1725, Falcon 1728, Vaucanson 1745, Jacquard 1804), so jacquard needs it. It gives sericulture its first dependant (silk was a dead end), and it records the Han pattern looms of ~150 BCE, among the earliest machines that store a pattern. |
| `structural_iron` | Structural iron (iron-framed buildings and bridges) | engineering · A8 | `coke` | 23,650 (8.0) | electric_motor | Iron as a structural material (bridges 1779, frames 1797, prefabricated halls 1851) is the missing step between cheap cast iron (coke) and the steel-frame skyscraper. The tree jumped there through reinforced concrete, which the first skyscrapers did not use. |
| `sanitary_engineering` | Sanitary engineering (sewerage and filtered water) | engineering · A8 | `steam_rotary AND fired_brick` | 19,890 (7.75) | reinforced_concrete | Sanitation engineering, a D07 domain the tree lacked, is distinct from medicine (germ theory) and from a material (reinforced concrete). The current requirement of infra.sewerage_system ('germ_theory AND reinforced_concrete') is anachronistic for the 1829-1875 systems, which used brick, Portland cement and steam pumps under miasma theory. |

All four have era-marked anchors, so the pinned count of 83 undated nodes is unchanged, and they pass the chronology test. `ashlar_masonry` deliberately does not require `surveying`: that node's anchor (~2,500 BCE) postdates Djoser's dressed-stone pyramid (~2,650 BCE), and stone dressing needs measuring tools, not land surveying. The fortification keeps surveying for its layout. Two new nodes carry future-system Eurekas (world events: city fires and epidemics), with justifications; neither is evaluable, so no order-less golden moves.

## 5. Candidates considered and REJECTED

| candidate | why rejected |
|---|---|
| Factory system (separate node) | The organizational form arrived with water-powered spinning: `water_frame` already carries 'the factory system' and gates building.textile_mill, the first factory building. A separate node would duplicate that gate without a new decision or consumer. |
| Mass production / moving assembly line (split from interchangeable_parts) | Ford's line (1913) was developed for and with the mass-produced automobile, and `automobile_mass` (D08) already bundles it; a separate node would have that one dependant. The conflation is fixed in text instead: the 1820 node loses the 'mass production' and 'Moving assembly line' strings, offered to D08. |
| Chemical engineering / process industries | The chemical nodes (sulphuric_acid, soda_leblanc, synthetic_dye, haber_bosch, plastics) are D04's. A D07 process-engineering node would have no D07 consumer: it would be a dead end. Left to D04, which already split organic chemistry. |
| Dams and reservoirs (ancient storage dams) | No reservoir or water-storage entity exists, so the node would be a knowledge-only dead end. Modern dam engineering is bundled in `hydroelectric` (D06). It becomes a candidate once T6.11 or M10+ creates an irrigation-storage entity. |
| Urban utilities (gas lighting, district heating) | Energy domain (D06); no consumer system. |
| Safety elevator (Otis 1853) | Its only dependant would be the skyscraper, whose vertical transport `electric_motor` already carries (the electric elevator made tall buildings possible). Recorded in skyscraper's prerequisite rationale. |
| Screw press (between olive_press_beam and printing_press) | It would put olive_press_beam on every path to printing_press, turning that node's evaluable Eureka into an implied Eureka. The loader refuses that, and Eureka lists are append-only. |
| noria → water_mill Eureka | Lewis (1997) supports water-lifting wheels and water mills arising together in the 3rd-century-BCE Greek world, not a derivation; water_mill's Eureka pool is already full (0.4). |
| Numerical control split (NC tools / industrial robots) | Neither half has a consumer; a split adds a dead end. |
| Theory of structures (Galileo 1638 → Navier 1826) as a prerequisite of RC and steel frames | Science-lane (D04) knowledge; the first iron bridges and frames were designed empirically. Not requested: low weight against the click it adds. |
| Iron and suspension bridge nodes | Covered as capability text of `structural_iron`. A bridge class needs the transport packet (DW-059), not more knowledge nodes. |
| Renaming loom_warp_weighted to name the ground and backstrap looms | Names are pinned by the corpus-sync test; the multi-centre history belongs in the description (corpus stage). |
| Moving flying_shuttle and spinning_jenny to the trunk | These A8 artisan inventions would break the MEASURED A7–A9-in-subtrees convention (V17), and by A8 the stage is open anyway. Raised as an open question instead. |
| Merging type_founding into printing_press | Considered when D05 first kept the press whole. Superseded when D05 accepted the type_founding → printing_press edge with a workload-conserving re-score. |

## 6. Conflations found

- **Construction technique vs military application:** stone_fortification (split).
- **Eligibility vs adoption/motive:** infra.sewerage_system required germ_theory, a public-health theory that motivates building but is not needed to build. Sewers and filtration (1829–1875) predate germ theory. Eligibility now reads engineering knowledge (sanitary_engineering); adoption runs through D12's public_health_board; valuation is M9.
- **Invention vs organizational era:** interchangeable_parts (1820) claimed the assembly line (1913).
- **Knowledge vs economic pressure:** the textile ladder (shuttle → jenny, mule → power loom) encoded demand, not knowledge.
- **Component vs product:** type founding sat after the printing press it was part of.
- **Institution vs technical norm:** inst.standard_threads is a standard (Whitworth 1841), not an institution with founding, maturity and a consumer (D-M). D07 recommends reclassifying it in T6.11.

## 7. Dead ends (MEASURED V09 list: 8 D07 nodes)

| node | resolution |
|---|---|
| `sewing` | RESOLVED by dependants: D09 applied `saddle ← cavalry AND sewing` and D08 `collar_harness ← horse_domestication AND sewing` (stitched leather and felt). |
| `silk` | RESOLVED by a dependant: the new `drawloom` (pattern weaving was developed for silk). |
| `jacquard` | RESOLVED by a dependant: D05's new `punched_card_tabulation ← jacquard AND telegraph`. Still the evaluable Eureka condition of `computer`. |
| `dyeing` | RESOLVED by a dependant: D04 applied `synthetic_dye ← organic_chemistry AND coke AND dyeing` (the synthetic-dye industry grew inside the dyeing trade). Later owner M10+ (dyed-cloth good). |
| `olive_press_beam` | Justified: it is the MEASURED knowledge condition of printing_press's evaluable Eureka. An accelerator, not a gate (a gate would make that Eureka implied). Later owner M10+ (oil-press capacity). |
| `noria` | Justified: lift irrigation has no entity yet. Owner: the irrigation realization (T6.11 tier C lift term), else M10+. |
| `cotton_gin_saw` | Justified: the spinning machines (1764–1779) predate it, so no causal dependant exists. Its historical weight is the cotton/slavery expansion. Later owner M10+ (ginning recipe; D01's new cotton crop). |
| `additive_manufacturing` | Justified: no manufacturing-productivity owner before M11+ industries (milestone M10+). |

New leaf created on purpose: `spinning_mule` lost its economic edge to the power loom. It is justified like the others (M10+ fine-yarn variant). Every Engineering finite node also gates the six engineering repeatables through the exhaustion rule (MEASURED `ResearchQuery.cs:99-116`), so no subtree node is idle for progression.

## 8. Realization findings (per node)

Rule used for `milestone`. **existing** = realized today, or knowledge with no later owner. **M6** = T6.11 can realize it with a consumer that is a coefficient inside a LIVE equation (construction project plus a term in farming output, fishing extraction, the weaving recipe or the per-settlement health seam). **M7** = battle layer (defence). **M9** = public health and comfort with no live equation. **M10+** = transport edge types (FROZEN; the transport packet DW-059), maritime routes, urban density, new goods (validator vocabulary; the brief's M11+ content is stated in the text).

MEASURED seams the proposals rely on:
- **(a) Health seam.** `InstitutionEffects.MortalityMultiplier` (`Institutions.cs:287-301`, read once per settlement in `DemographicsSystem.cs:266-277`) is bounded and saturating, and fed today only by medical universities. Water and sanitation entities can enter it as coverage terms.
- **(b) Crafting split.** Crafting labour splits EQUALLY among available recipes (`ProductionSystem.cs:463-490`). Textile recipe variants therefore need a supersession rule first.
- **(c) Farming catchment.** Farming reads catchment arable km² (`ProductionSystem.cs:255-258`): the irrigation hook.
- **(d) Housing.** HousingSystem dwellings have no land or density limit, so a skyscraper has no consumer.
- **(e) Roads.** RoadDevelopmentSystem realizes road classes; the Built road class needs `track_road AND stone_dry`.

| node | entity (or knowledge) | status | milestone | realizing owner |
|---|---|---|---|---|
| `cordage` | — (knowledge only) | knowledge-only | existing | none |
| `basketry` | infra.fish_weir | deferred | M6 | ConstructionSystem (project) + ProductionSystem fish-yield term (PROPOSED) |
| `shelter_hut` | — (knowledge only) | knowledge-only | existing | none |
| `hide_working` | — (knowledge only) | knowledge-only | existing | none |
| `sewing` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `wool` | — (knowledge only) | knowledge-only | existing | none |
| `mudbrick` | building.city_wall | deferred | M7 | ConstructionSystem (project) + Battle layer (M7) for the defensive effect |
| `lime_plaster` | — (knowledge only) | knowledge-only | existing | none |
| `timber_frame` | — (knowledge only) | knowledge-only | existing | none |
| `stone_dry` | infra.road_built, building.city_wall, building.harbour | realized-today | existing | RoadDevelopmentSystem (Built road class); ConstructionSystem for the wall and harbour (unrealized) |
| `well` | infra.well | deferred | M6 | ConstructionSystem (project) + per-settlement health seam (PROPOSED term) |
| `spinning_spindle` | — (knowledge only) | knowledge-only | existing | none |
| `loom_warp_weighted` | inst.textile_workshop | deferred | M6 | InstitutionsSystem (T6.11, D-M) |
| `dyeing` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `irrigation_basin` | infra.basin_irrigation | deferred | M6 | ConstructionSystem (project) + Farming output term (PROPOSED) |
| `irrigation_canal` | infra.canal_irrigation | deferred | M6 | ConstructionSystem (project) + Farming output term (PROPOSED) |
| `stone_fortification` | building.castle | deferred | M7 | ConstructionSystem (project) + Battle layer (M7) |
| `qanat` | infra.qanat | deferred | M6 | ConstructionSystem (project) + Farming/water term (PROPOSED) |
| `aqueduct_tunnel` | infra.aqueduct | deferred | M6 | ConstructionSystem (project) + per-settlement health/water term (PROPOSED) |
| `aqueduct_channel` | infra.aqueduct | deferred | M6 | ConstructionSystem (project) + per-settlement health/water term (PROPOSED) |
| `arch` | — (knowledge only) | knowledge-only | existing | none |
| `olive_press_beam` | — (knowledge only) | knowledge-only | M10+ | ResearchSystem (Eureka condition of printing_press); none for production |
| `water_screw` | — (knowledge only) | knowledge-only | existing | none |
| `noria` | — (knowledge only) | knowledge-only | M10+ | none until an irrigation term exists |
| `gearing` | — (knowledge only) | knowledge-only | existing | none |
| `vault_dome` | building.bathhouse, building.castle, infra.sewer | deferred | M6 | ConstructionSystem (projects); health seam (sewer, PROPOSED); Battle layer (castle, M7); M9 (bathhouse) |
| `bridge_stone` | infra.bridge | deferred | M10+ | Transport packet (DW-059, D08's shared owner for network edge types; RoadDevelopmentSystem is the analogue) |
| `aqueduct_arcade` | building.bathhouse, infra.aqueduct | deferred | M6 | ConstructionSystem (project) + per-settlement health/water term (PROPOSED); M9 for the bathhouse |
| `silk` | — (knowledge only) | knowledge-only | existing | none |
| `fulling_mill` | building.fulling_mill | deferred | M6 | ConstructionSystem (project) + weaving-recipe capacity term (PROPOSED) |
| `spinning_wheel` | — (knowledge only) | knowledge-only | existing | none (knowledge only); ProductionSystem recipe variant later |
| `horizontal_loom` | — (knowledge only) | knowledge-only | existing | none (knowledge only); ProductionSystem recipe variant later |
| `cotton_gin_roller` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `crank_connecting_rod` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `escapement_verge` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `canal_lock` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `type_founding` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `canal_navigation` | infra.navigation_canal | deferred | M10+ | Transport packet (DW-059, D08's shared owner for network edge types): a canal edge class beside the road classes |
| `cylinder_boring` | — (knowledge only) | knowledge-only | existing | none |
| `slide_rest_lathe` | inst.standard_threads | deferred | M6 | InstitutionsSystem (T6.11, D-M) - D07 recommends reclassification |
| `interchangeable_parts` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `milling_machine` | — (knowledge only) | knowledge-only | existing | none |
| `flying_shuttle` | — (knowledge only) | knowledge-only | existing | none |
| `spinning_jenny` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `water_frame` | building.textile_mill | deferred | M6 | ConstructionSystem (project) + weaving-recipe capacity term (PROPOSED) |
| `spinning_mule` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `power_loom` | building.textile_mill | deferred | M6 | ConstructionSystem (project) + weaving-recipe capacity term (PROPOSED) |
| `cotton_gin_saw` | — (knowledge only) | knowledge-only | M10+ | none until a fibre-processing recipe exists |
| `jacquard` | — (knowledge only) | knowledge-only | existing | none (knowledge only); ResearchSystem reads it as computer's Eureka condition |
| `reinforced_concrete` | — (knowledge only) | knowledge-only | existing | none (knowledge only) |
| `skyscraper` | building.skyscraper | deferred | M10+ | ConstructionSystem (project) + housing capacity (none yet) |
| `numerical_control` | — (knowledge only) | knowledge-only | existing | none |
| `desalination` | building.desalination_plant | deferred | M6 | ConstructionSystem (project) + per-settlement water/health term (PROPOSED) |
| `additive_manufacturing` | — (knowledge only) | knowledge-only | M10+ | none until M11+ industries |
| `ashlar_masonry` | building.lighthouse | deferred | M10+ | ConstructionSystem (lighthouse project) + maritime routes (none yet) |
| `drawloom` | — (knowledge only) | knowledge-only | M10+ | none (knowledge only) |
| `structural_iron` | — (knowledge only) | knowledge-only | M10+ | none (knowledge only) |
| `sanitary_engineering` | infra.sewerage_system | deferred | M6 | ConstructionSystem (project) + per-settlement health seam (PROPOSED term) |

Realized today: **only `stone_dry`** (the Built road class). Every other D07 entity is IMPLEMENTATION DEFERRED. T6.11 candidates with a named live equation:
- well, qanat, aqueducts, sewer, sewerage and desalination plant → the health seam;
- basin and canal irrigation → farming output;
- fish weir → fishing extraction;
- fulling and textile mills → the weaving recipe; blocker: no per-building capacity term, as for kiln and smithy;
- inst.textile_workshop and inst.standard_threads → D-M.

Later owners: castle and city wall → M7; bathhouse → M9; bridge and navigation canal → transport packet, M10+; harbour and lighthouse → maritime routes, M10+; skyscraper → urban density, M10+.

## 9. Cost findings

- MEASURED baseline: the 54 D07 nodes cost 420,980 RP. Proposed: the 54 records plus 4 new nodes cost 460,530 RP (+39,550). New nodes add +49,460; re-scores net −9,910 (lime_plaster −520, aqueduct_arcade −1,450, stone_fortification −1,030, horizontal_loom −2,010, interchangeable_parts −4,900, spinning_jenny −2,450, spinning_mule +2,450, desalination +4,900, additive_manufacturing −4,900).
- The pending cotton edit adds +870 (cotton_gin_roller 2,090 → 2,960). D05's matching printing_press re-score (−11,650) is in D05's lane. Together, type founding plus the press cost 36,490 RP against 39,780 for the old press alone.
- Every re-score follows the coordinator's rubric: Age prior for difficulty and institutional; prereq_complexity by top-level conjuncts with an OR group counted once; per-factor notes. Only one re-dating: horizontal_loom A6 → A5 (difficulty 1.75 → 1.5). The stone_fortification split costs 1.55× the original (2,960 + 2,490 vs 3,520), because the carved-out technique is broader (breadth 1.0) than the fortification it was hidden in.
- No D07 node is a 100k+ RP bottleneck: the largest is the skyscraper at 66,910.
- Engineering finite count: −8 relocated, +2 new = −6. The six engineering recursive descriptors' `finite_nodes_to_exhaust` and the branch-count pins move.

## 10. Alternative routes added (OR, genuine regional, material or prime-mover alternatives; no cost softened)

- `well`: stone OR timber lining.
- `irrigation_basin` and `irrigation_canal`: cereal OR root crops (D01).
- `vault_dome`: concrete OR fired brick OR ashlar.
- `bridge_stone`: concrete OR ashlar (Zhaozhou 605 CE).
- `power_loom`: water OR steam (Lowell 1814; Lancashire).
- `desalination`: distillation OR membranes.
- Pending: `spinning_spindle` adds cotton as a fibre (D01).

Note (MEASURED, D03's finding): `AiResearchPolicy` unions OR branches in goal closures; none of these nodes is in an AI goal closure today.

## 11. Cross-lane requests and replies

- **D05-communication-computing.** Your condition is met: type_founding moves to the trunk (A7; one V17 WARN, the same exception as printing_press) with prereq 'casting_closed' and emerged '~1,440-1,450 CE'. Please apply at integration, as your record states: printing_press 'type_founding AND mechanics_archimedean AND paper' with material 0.75 -> 0.25 (39,780 -> 28,130 RP; press + type founding 36,490 RP). Thank you for punched_card_tabulation: it resolves jacquard's dead end (jacquard now requires D07's new drawloom AND paper; your edge is unaffected). *Reason:* Type casting is the causal precondition of the press, not its sequel; trunk closure requires both in the trunk; the A7 core workload is roughly conserved.
- **D09-military.** Reply: thank you for accepting saddle <- sewing. hide_working, sewing, timber_frame, interchangeable_parts and slide_rest_lathe are kept. stone_fortification keeps key 126 but is split: it now requires 'ashlar_masonry AND surveying' (cost 3,520 -> 2,490) and keeps building.castle; building.lighthouse moves to ashlar_masonry. Your trace_italienne edge is unaffected. *Reason:* Coordination of D09 dependants on D07 nodes.
- **D08-transport-naval.** Reply, no action required: thank you for applying collar_harness <- 'horse_domestication AND sewing' and for adopting 'moving assembly line (Ford, 1913)' and 'mass production' in automobile_mass (D07 removes them from the 1820 interchangeable_parts node). bridge_stone, canal_lock and canal_navigation now name your shared owner, the transport packet (DW-059), in their deferredEffect. canal_lock no longer requires mortise_hull; it uses timber_frame for the gates. bicycle's interchangeable_parts edge is fine (the node is kept; its cost falls 16,730 -> 11,830). *Reason:* Coordination: the assembly line was developed for and with the mass-produced automobile; a lock's users are boats, but its knowledge is canal hydraulics and gate carpentry.
- **D04-natural-science.** Reply, no action required: thank you for making synthetic_dye require dyeing ('organic_chemistry AND coke AND dyeing'; prereq_complexity 0.5 -> 1.0, cost 23,650 -> 33,450), which resolves D07's dyeing dead end. Agreed that a terrain-crossing tunnel class belongs to the transport packet (DW-059) and should name dynamite there; D07's aqueduct_tunnel is the ancient rock-cut water tunnel. The chemical-industry nodes stay with D04; D07 adds none. *Reason:* The synthetic-dye industry replaced natural dyestuffs inside an existing dyeing trade: Perkin's mauve (1856) needed mordant dyeing to be usable, and synthetic alizarin (1868) reproduced madder. It also gives D07's dyeing a causal dependant.
- **D01-agriculture-food.** Reply - accepted. (a) spinning_spindle 'cordage AND (flax OR wool OR cotton_cultivation)' (one OR group: cost unchanged) and cotton_gin_roller 'wheel_solid AND cotton_cultivation' (prereq_complexity 0.0 -> 0.5, cost 2,090 -> 2,960). Because cotton_cultivation is a NEW D01 id, D07 records both as pendingCrossLaneEdits for the integrator (the lane brief allows only existing ids in a lane's own records). (b) Applied now: irrigation_basin and irrigation_canal require 'cereal_cultivation OR root_crop' (cost unchanged). (c) Noted: farm_machinery's crank edge (the crank moves to the trunk, which helps your A8 node) and drip_irrigation's irrigation edge. *Reason:* A cotton gin needs a cotton crop; cotton is an independently domesticated fibre (Mehrgarh, coastal Peru); irrigation was not confined to cereal farmers.
- **D10-medicine.** Reply to your request on infra.sewerage_system: D07 removes reinforced_concrete as you suggested and re-points the entity to the new sanitary_engineering node (steam_rotary AND fired_brick). D07 does not add (germ_theory OR epidemiology): it would gate building on a public-health theory, which is adoption and valuation, not eligibility. The alternative is recorded for the Director as an OPEN option. Germ-theory water treatment (chlorination 1908) remains available to D10 as a practice. *Reason:* Sewerage preceded germ theory (Hamburg 1843, London 1859-1875) and Snow's 1854 study; filtration dates from 1829. Engineering knowledge is what eligibility should read.
- **D03-science-early.** Accepted in modified form: escapement_verge becomes 'gearing AND water_clock' (D07 drops crank_connecting_rod as non-causal, so the count and the 7,030 cost are unchanged), and the node moves to the trunk. water_clock's dead end is resolved. *Reason:* Mechanical clockwork replaced the water clock's regulator; the crank plays no part in a verge escapement.
- **D02-materials-mining.** FYI, no action required: (1) arch now requires mudbrick, not fired_brick; your concrete_pozzolan no longer requires arch, which is consistent. (2) lime_plaster no longer requires mudbrick (Natufian lime plaster predates mudbrick), so concrete_pozzolan's closure shrinks to fire_making -> lime_plaster. (3) crank_connecting_rod moves to the trunk: blast_furnace_water keeps it, and D02 could move medieval ironworking (blast_furnace_water, wire_drawing) to the trunk if it judges them craft knowledge. (4) New D07 nodes depend on D02 knowledge: ashlar_masonry on copper_smelting, structural_iron on coke. (5) Your percussion_drilling <- well edge is fine; well now accepts stone OR timber lining. *Reason:* Coordination of shared closures; the A5 supporting fact a5_concrete (concrete_pozzolan) gets cheaper.
- **D06-energy-engineering.** FYI, no action required: crank_connecting_rod (a prerequisite of steam_rotary) moves to the trunk; escapement_verge no longer requires the crank; power_loom accepts water_mill OR steam_rotary; sanitary_engineering requires steam_rotary; desalination now requires 'electricity_generation AND (distillation OR plastics)'. A noria -> water_mill Eureka was considered and rejected (water_mill's Eureka pool is full at 0.4). *Reason:* Coordination of closures that pass through D06's nodes.
- **D12-civics.** Boundary confirmations: (1) D07 supports your re-point of inst.guild to 'textile_workshop AND craft_guilds'. (2) inst.standard_threads is a technical norm; D07 recommends T6.11 reclassify it as knowledge on slide_rest_lathe - if a standards body is wanted, it is yours. (3) Your public_health_board governs adoption and funding of infra.sewerage_system; D07 adds no technology -> civic edge. *Reason:* Organizational knowledge (technology) versus institutions and regulation (civics), as the lane brief directs.

Entity-requirement changes (top-level `entityRequirementChanges`; the schema has no field for them; cf. D12's OD-05):
- `building.lighthouse`: `stone_fortification` → `ashlar_masonry`. A lighthouse is a civil tower in dressed stone (the Pharos ~280 BCE), not military architecture; the split of stone_fortification separates the technique from the fortification.
- `infra.sewerage_system`: `germ_theory AND reinforced_concrete` → `sanitary_engineering`. Eligibility is engineering knowledge. Sewerage and filtration (Chelsea 1829, Hamburg 1843, London 1859-1875) were built with brick, Portland cement and steam pumps under miasma theory, before germ theory or reinforced concrete. Public-health motive is adoption (D12 public_health_board) and M9 valuation, not eligibility.

Pending edits that depend on D01's new id `cotton_cultivation` (top-level `pendingCrossLaneEdits`). They are kept out of D07's own records because the brief allows only existing ids there:
- `spinning_spindle`: `cordage AND (flax OR wool)` → `cordage AND (flax OR wool OR cotton_cultivation)`. Cost effect: none (one OR group: prereq_complexity stays 0.5; cost 620).
- `cotton_gin_roller`: `wheel_solid` → `wheel_solid AND cotton_cultivation`. Cost effect: prereq_complexity 0.0 -> 0.5; magnitude 4.5 -> 5.0; cost 2,090 -> 2,960 (factors otherwise unchanged).

## 12. Open questions

- Alias/remap seam (package-level OPEN DECISION): the stone_fortification split and the two entity re-points remove eligibility from old holders (building.lighthouse; infra.sewerage_system). Both entities are unrealized, so there is no gameplay loss today; a remap that grants ashlar_masonry to holders of stone_fortification would close it.
- The part-file schema has no field for entity-requirement changes; D07 adds a top-level 'entityRequirementChanges' list (two re-points). The integrator needs to carry it (and the reverse-index unlock edits) or the Director rules them separately, as D12's OD-05.
- Water and sanitation realization (well, qanat, aqueducts, sewer, sanitary_engineering, desalination plant): realize them in T6.11 as bounded coverage terms in the existing per-settlement health seam (InstitutionEffects.MortalityMultiplier), or wait for the M9 disease model? D07 recommends the T6.11 path as the nearest honest equation.
- Textile realization: should cloth productivity knowledge become variants of the live weaving recipe? Crafting labour splits equally among available recipes (ProductionSystem.cs:463-490), so a supersession rule is needed first (M10+ design decision).
- inst.standard_threads: reclassify as knowledge (D07's recommendation) or keep it as a D12 standards institution under D-M?
- Convention V17 keeps every A7-A9 node in a subtree. Some 18th-century textile inventions (flying shuttle 1733, jenny 1764) were artisan knowledge too; D07 leaves them in Engineering. Should the convention bend for craft inventions?
- infra.sewerage_system: engineering-only eligibility (D07) versus 'sanitary_engineering AND (germ_theory OR epidemiology)' (D10's alternative)?
- Two accepted edits depend on D01's new cotton_cultivation and are recorded under pendingCrossLaneEdits (spinning_spindle's fibre OR group; cotton_gin_roller's crop requirement). The integrator, or a later D07 pass, must apply them once that id is ratified.

## 13. Sources (RESEARCHED this session; dates otherwise from the MEASURED inventory or standard history, marked INFERENCE where causal weight is judged)

- Laoguanshan pattern-loom models, ~150 BCE; treadle loom in East Asia a millennium before the West: <https://dx.doi.org/10.15184%2Faqy.2016.267>
- Horizontal treadle loom: China 2nd c. BCE; oldest European parts 11th c. (Gdańsk, Novgorod, Haithabu): <https://rep.vsu.by/bitstream/123456789/42943/1/314-315.pdf>
- Drawloom history and transmission: <https://www.cnch.org/?p=5659>
- Bouchon 1725, Falcon, Vaucanson, Jacquard 1804 automate the drawloom: <https://en.wikipedia.org/wiki/Jacquard_machine>
- Basile Bouchon: <https://en.wikipedia.org/wiki/Basile_Bouchon>
- Natufian lime plaster ~12,000 years ago (Nahal Ein Gev II): <https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10427327/>
- LBK oak-lined wells, timber felled 5206–5098 BCE: <https://kommunikation.uni-freiburg.de/pm-en/press-releases-2012/pm.2012-12-19.366-en>
- Huaca Prieta indigo-dyed cotton, ~6,000 years, weft-twined: <https://www.sciencenews.org/article/oldest-indigo-dyed-fabric-found>
- Zhaozhou (Anji) Bridge 595–605 CE, open-spandrel segmental stone arch: <https://en.wikipedia.org/wiki/Anji_Bridge>
- Zhaozhou Bridge landmark record: <https://www.asce.org/about-civil-engineering/history-and-heritage/historic-landmarks/zhaozhou-bridge>
- James Simpson, Chelsea slow-sand filter 1829: <https://en.wikipedia.org/wiki/James_Simpson_(engineer)>
- William Lindley, Hamburg sewers after the 1842 fire: <https://www.erih.net/how-it-started/virtual-exhibition-linking-europe/william-lindley-modern-water-management-for-continental-europe>
- Joseph Bazalgette: brick main sewers and Portland cement: <https://en.wikipedia.org/wiki/Joseph_Bazalgette>
- Ditherington flax mill 1797, first iron-framed building ('grandfather of skyscrapers'): <https://en.wikipedia.org/wiki/Shrewsbury_Flaxmill_Maltings>
- Ditherington, ICE record: <https://www.ice.org.uk/what-is-civil-engineering/what-do-civil-engineers-do/ditherington-flax-mill>
- Lewis (1997): water power in the 3rd-century-BCE Greek world for grinding and lifting water: <https://en.wikipedia.org/wiki/Watermill>

Not re-checked this session (INFERENCE): the Chinese summit canals (Huitong 1289; Song Li 1411); the Albion Mills fire (1791) as the fire-proofing motive; Pazyryk stitched saddles; D03's water-clock lineage of the escapement (Needham).
