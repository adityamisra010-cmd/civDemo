# Eureka evaluability register (M6 closure, track KNOW)

**Status:** RECORD, 2026-10-28. Authority: D-066 (the Director's M6 closure instructions; §2 A7–A9 context) and
ADR-058 §B. Every Eureka of the shipped `research.json` appears below **exactly once** (`EurekaEvaluabilityTests.
Register_ListsEveryEurekaExactlyOnce_WithTheContentsStatus` checks this table against the content on every test run).

## 1. What changed

Before this pass the content carried **86 Eurekas, all on technologies**: 14 machine-evaluable (2 over stock, 12 over
knowledge atoms) and 72 `future-system:*` conditions that never fire. ADR-058 §B adds three quantity families to the
research dialect, each grounded in state the build already computes:

| quantity | reads | source of truth |
|---|---|---|
| `deposit_<good>` | the deposit abundance at the settlement **as its holder knows it** — a concealed deposit reads 0 until the holder prospects the settlement | `DepositRow` through `DepositConcealment.KnownAbundance` (the one predicate every deposit consumer reads) |
| `site_river` | 1 when a river cell lies in the knowledge-lattice node the settlement stands on, else 0 | `TerrainSet.Rivers`; the settlement's own node is always known to its holder |
| `site_coast` | 1 when an open-water cell (water ≥ 0.5, not a river cell) lies in that node, else 0 | `TerrainSet.Water`, `TerrainSet.Rivers` |

Seven Eurekas are made evaluable with them (six `resources/terrain`, one `goods state`). **Totals now: 86 Eurekas,
21 evaluable, 65 future-system** (resources/terrain 30, world events 24, institutions 8, goods state 1, world state 1,
construction state 1). The tree-closure hashes do not cover Eurekas (`TreeClosureBaselineTests` hashes id, key, tree,
Age, prerequisite, unlocks, effects, repeatable and the entity list), so TC-1 STRUCTURE and TEXT are unchanged; Eureka
text is unchanged.

The rule for "made evaluable": every noun of the circumstance maps onto a quantity the build computes; a partial
grounding is stated in the last column. Where a circumstance needs a resource, species, climate or event the build does
not model, the Eureka stays future and names the owning system and milestone. Three are marked **GROUNDABLE NOW** or
**GROUNDABLE LATER**: the state exists but a new operand or a content decision would be needed; they were left out of
this pass to keep the dialect change to the resources/terrain packet.

## 2. Register

Columns: node · Eureka index · `system` (content) · status · condition (evaluable) or owning system and target milestone
(future) · grounding (evaluable) or the circumstance text (future).

| node | # | system | status | condition / owner — target | grounding / circumstance |
|---|---|---|---|---|---|
| `knapping_blade` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (stone is generic; obsidian/flint not distinguished) — target unscheduled | An obsidian or fine flint source is in range |
| `adhesive_natural` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (resin, bitumen, ochre) — target unscheduled | Resin, bitumen or ochre is already gathered and worked for daily binding |
| `dugout` | 0 | resources/terrain | evaluable (ADR-058) | `deposit_timber > 0 AND (site_river > 0 OR site_coast > 0)` | timber deposit known at the settlement AND a river or open water in its own lattice node ('navigable' is not modelled) |
| `fishing_hook` | 0 | resources/terrain | evaluable (ADR-058) | `deposit_fish > 0` | a fish deposit at the settlement (fish's deposit channel is water) |
| `dog_domestication` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Wolves range near human camps |
| `cereal_cultivation` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) — target unscheduled | Wild cereals grow in range |
| `rice_wet` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) — target unscheduled | Wild rice grows in wetlands in range |
| `millet` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) — target unscheduled | Wild millets grow in range |
| `maize` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) — target unscheduled | Teosinte grows in range |
| `nixtamalization` | 0 | goods state | future | resource roster (worldgen deposits + goods.json) — no such good (lime, wood ash) and food processing — target unscheduled | Wood ash or lime is used in cooking |
| `sheep_goat` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Wild sheep or goats range nearby |
| `cattle` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Aurochs range nearby |
| `pig` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Wild boar range nearby |
| `salt_extraction` | 0 | resources/terrain | evaluable (ADR-058) | `site_coast > 0` | the sea-coast alternative: open water in the settlement's own lattice node (brine springs and salt pans are not modelled) |
| `copper_smelting` | 0 | goods state | evaluable (before ADR-058) | `stock_copper_ore > 0` | knowledge or stock already evaluable |
| `irrigation_canal` | 0 | resources/terrain | future | research dialect: a site aridity operand (terrain moisture exists; the aridity threshold is an unruled content decision) — GROUNDABLE LATER — target unscheduled | An arid floodplain watered by a river |
| `terrace` | 0 | resources/terrain | future | research dialect: a site slope operand (terrain elevation exists) — GROUNDABLE LATER — target unscheduled | Arable land on slopes |
| `arsenical_bronze` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (copper-ore does not distinguish arsenical ores) — target unscheduled | Arsenic-bearing copper ores (fahlore, enargite, arsenopyrite-rich deposits) are smelted locally |
| `tin_bronze` | 0 | goods state | evaluable (before ADR-058) | `stock_tin_ore > 0` | knowledge or stock already evaluable |
| `sail_square` | 0 | resources/terrain | evaluable (ADR-058) | `site_river > 0 OR site_coast > 0` | a river or open water in the settlement's own lattice node (steady winds are not modelled) |
| `horse_domestication` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled (EC-2 horses is a herd stock, not a wild range) — target unscheduled | Wild horses roam the region (steppe) |
| `donkey` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | The wild ass ranges nearby |
| `camel` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Wild camels range in arid zones |
| `glass_glaze` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (natron / plant-ash alkali) — target unscheduled | Natron or plant-ash alkali is already used in washing, embalming or ceramic work |
| `glass_core` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (natron / plant ash) — target unscheduled | Natron or plant ash is available |
| `beekeeping` | 0 | resources/terrain | future | fauna layer (wild-species ranges) — the livestock deposit is generic, species are not modelled — target unscheduled | Wild honeybees nest in the region |
| `coinage_electrum` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (precious metals) — target unscheduled | Electrum (natural gold–silver alloy) occurs in range |
| `siege_ram` | 0 | world state | future | fortifications (building.city_wall, building charter §5.12) — target M7 | A fortified settlement stands among neighbours |
| `qanat` | 0 | resources/terrain | future | hydrology (groundwater / aquifers) — not modelled — target unscheduled | An aquifer upslope of an arid plain |
| `geometry_axiomatic` | 0 | institutions | future | institutions: inst.library (deferred; building charter §9.4 D, Research) — target unscheduled | Institution present: library |
| `water_mill` | 0 | resources/terrain | future | research dialect: a river-gradient operand (rivers and elevation exist) — GROUNDABLE LATER — target unscheduled | A river with usable fall |
| `concrete_pozzolan` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (volcanic ash) — target unscheduled | Volcanic ash deposits are in range |
| `road_paved` | 0 | construction state | future | research dialect: a built-roads operand over RoadDevelopmentSystem's TransportEdges — GROUNDABLE NOW, left out of this pass (resources/terrain scope) — target M7 candidate (with the route graph) | Built roads already exist |
| `silk` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) and fauna (silkworm) — target unscheduled | The silkworm (Bombyx mori) and mulberry are in range |
| `monsoon_sailing` | 0 | resources/terrain | future | climate: a seasonal wind channel (building charter B3 wind channel) — target unscheduled | Seasonal monsoon winds cross open sea |
| `compass_magnetic` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (lodestone) — target unscheduled | Lodestone occurs in the region |
| `gunpowder` | 0 | resources/terrain | evaluable (ADR-058) | `deposit_saltpetre > 0 OR stock_saltpetre > 0` | a saltpetre deposit known to the holder, or saltpetre held |
| `greek_fire` | 0 | resources/terrain | future | EC-4a light fuels (petroleum seeps) — target M10 recommended (EC-4a owner open, OD-3) | Naphtha from surface petroleum seeps is already used as an incendiary |
| `paper_money` | 0 | world events | future | finance: money and coin (no coin good; building charter §9 mint B) — target M8 | Metal currency is in short supply |
| `printing_press` | 0 | research (knowledge state) | evaluable (before ADR-058) | `olive_press_beam` | knowledge or stock already evaluable |
| `longitude_problem` | 0 | world events | future | movement: ocean routes and losses (water edges, charter M7.6) — target M7 | Ships lost to navigational error on ocean routes |
| `coke` | 0 | resources/terrain | evaluable (ADR-058) | `deposit_coal > 0` | a coal deposit KNOWN to the holder (coal is concealable: only once prospected) |
| `inoculation` | 0 | world events | future | health: epidemics and endemic disease — target M9 | Endemic smallpox |
| `interchangeable_parts` | 0 | world events | future | Battle Layer: war state (sustained war) — target M7 | Sustained war (musket contracts) |
| `chemistry_quantitative` | 0 | institutions | future | institutions: inst.scientific_society (charter §9.4 M8+) — target M9 | Institution present: scientific society |
| `sulphuric_acid` | 0 | goods state | evaluable (ADR-058) | `stock_sulphur > 0 AND stock_saltpetre > 0` | both inputs held (the activity itself is not modelled; its inputs are) |
| `haber_bosch` | 0 | world events | future | resource roster: natural nitrate supplies (saltpetre exists; nitrate scarcity is a market/depletion fact not modelled) — target unscheduled | Natural nitrate supplies are scarce |
| `electromagnetism` | 0 | institutions | future | institutions: inst.scientific_society (charter §9.4 M8+) — target M9 | Institution present: scientific society |
| `telegraph` | 0 | research (knowledge state) | evaluable (before ADR-058) | `railway` | knowledge or stock already evaluable |
| `petroleum_refining` | 0 | resources/terrain | future | EC-4a light fuels (oil seeps and wells) — target M10 recommended (EC-4a owner open, OD-3) | Oil seeps or wells are in range |
| `evolution` | 0 | research (knowledge state) | evaluable (before ADR-058) | `carrack OR caravel` | knowledge or stock already evaluable |
| `aircraft` | 0 | research (knowledge state) | evaluable (before ADR-058) | `bicycle` | knowledge or stock already evaluable |
| `aircraft_metal` | 0 | world events | future | Battle Layer: war state — target M7 | Sustained war between major powers |
| `container_shipping` | 0 | institutions | future | politics/diplomacy: an international standards body — target M8 | An international standards body exists |
| `radar` | 0 | world events | future | Battle Layer: rival formations sighted (formation sighting is fog work deferred to M7, D-066 R1) — target M7 | A rival fields military aircraft |
| `computer` | 0 | research (knowledge state) | evaluable (before ADR-058) | `jacquard` | knowledge or stock already evaluable |
| `quantum_mechanics` | 0 | institutions | future | research dialect: an institution operand over InstitutionRow (the university IS realized, ADR-033 D6) — GROUNDABLE NOW, left out of this pass — target M7 candidate | Institution present: research university |
| `nuclear_fission` | 0 | world events | future | Battle Layer: war state — target M7 | Sustained war between major powers |
| `antibiotic_penicillin` | 0 | world events | future | Battle Layer: war state — target M7 | Sustained war |
| `vaccine_lab` | 0 | world events | future | health: epidemics — target M9 | An epidemic is under way |
| `canning` | 0 | world events | future | Battle Layer: war state and army provisioning — target M7 | Sustained war requiring army provisioning |
| `green_revolution` | 0 | world events | future | food state: famine (FoodState/food_surplus_ratio exist, but an unpublished variable reads 0 and would fire spuriously — needs a published famine fact) — target unscheduled | Famine or acute food shortage |
| `genetics_mendel` | 0 | institutions | future | institutions: inst.statistics_vital (charter §9.4 M8+) — target M9 | Statistical record-keeping exists |
| `cyber_warfare` | 0 | world events | future | digital infrastructure of rivals (EC-5 + networks) — target M10+ (EC-5 packets) | A rival depends on networked infrastructure |
| `antiviral_drugs` | 0 | world events | future | health: epidemics — target M9 | A recognised viral epidemic drives a targeted pharmaceutical programme |
| `mrna_vaccines` | 0 | world events | future | health: pandemics — target M9 | A pandemic is under way |
| `oral_rehydration` | 0 | world events | future | health: endemic disease — target M9 | Endemic diarrhoeal disease |
| `catalytic_converter` | 0 | institutions | future | law and regulation (emission law) — target M8 | Urban photochemical smog has produced vehicle-emission law |
| `icbm` | 0 | world events | future | Battle Layer / strategic weapons: rival thermonuclear weapons — target M10 (strategic weapons, building charter §7.3) | A rival holds thermonuclear weapons |
| `stealth` | 0 | world events | future | Battle Layer: air losses to a rival's air defence — target M7 | A rival integrated radar air-defence network has inflicted aircraft losses |
| `missile_defence` | 0 | world events | future | Battle Layer: rival missile forces — target M7 | A rival fields ballistic missiles |
| `cotton_cultivation` | 0 | resources/terrain | future | flora/biome layer (wild-plant ranges) — target unscheduled | Wild cotton grows in range |
| `cupellation` | 0 | resources/terrain | future | resource roster (worldgen deposits + goods.json) — no such good (argentiferous lead ore) — target unscheduled | Argentiferous lead ore (galena) is smelted locally |
| `taxonomy` | 0 | research (knowledge state) | evaluable (before ADR-058) | `carrack OR caravel` | knowledge or stock already evaluable |
| `breech_loading_rifle` | 0 | world events | future | Battle Layer: battle encounters — target M7 | A rival's breech-loader-armed infantry has been met in battle |
| `epidemiology` | 0 | world events | future | health: epidemics — target M9 | A cholera-type epidemic has struck the polity's towns |
| `sanitary_engineering` | 0 | world events | future | health: epidemics; disasters (great fire) — target M9 | A cholera epidemic or great fire strikes a large city |
| `structural_iron` | 0 | world events | future | disasters: urban fire (DisasterSystem models famine-class shocks only) — target unscheduled | A great fire destroys a timber-floored mill or warehouse |
| `organic_chemistry` | 0 | institutions | future | building.research_laboratory (charter §9.1 D, Research) — target unscheduled | A teaching research laboratory is operating |
| `periodic_system` | 0 | research (knowledge state) | evaluable (before ADR-058) | `spectroscopy` | knowledge or stock already evaluable |
| `stratigraphy` | 0 | research (knowledge state) | evaluable (before ADR-058) | `canal_navigation` | knowledge or stock already evaluable |
| `guided_missile` | 0 | world events | future | Battle Layer: rival bombers — target M7 | A rival's long-range bombers threaten the polity's cities |
| `communications_satellite` | 0 | research (knowledge state) | evaluable (before ADR-058) | `submarine_cable` | knowledge or stock already evaluable |
| `geophysical_prospecting` | 0 | research (knowledge state) | evaluable (before ADR-058) | `petroleum_refining` | knowledge or stock already evaluable |
| `radioactivity` | 0 | research (knowledge state) | evaluable (before ADR-058) | `xray` | knowledge or stock already evaluable |
| `radiometric_prospecting` | 0 | research (knowledge state) | evaluable (before ADR-058) | `nuclear_fission` | knowledge or stock already evaluable |
