# Research corpus audit — tech-graph-v0.6 → research.json

**GENERATED** by `scripts/migrate-research-corpus.py` (do not edit by hand). Corpus SHA-256 `e1251b0987a50db9252953a0a6af89594befa7bdf6dfb82361ba180d193ca854`. Contract: `docs/adr/adr-029-research-engine.md`; rulings: D-044.

## Summary

- Technology nodes integrated: **424** (every corpus technology; none dropped).
- Civics nodes integrated: **6** (the architecture §5.7 candidates).
- Tree 1 placement (rule R1, ADR-029 §8): trunk 176, military 37, medicine 22, engineering 130, natural_science 44, agriculture 15
- Eureka strings: 801; machine-evaluable 93; by status: contact-state-absent 61, evaluable 93, implied-by-prerequisites 21, institution-state-absent 79, no-state-carrier 279, requires-authoring 268
- Eureka audit categories (Director ruling 2026-10-01 §7): A single-condition evaluable **93**, B multi-condition evaluable **0**, C requires a future system **419**, D implied by prerequisites **21**, E dead / impossible **0**, F ambiguous, requires authoring **268**
- Cost bands (technologies): M1 refinement 29, M2 notable advance 136, M3 major advance 146, M4 breakthrough 88, M5 epochal 25
- Technique candidates flagged for the Director (not moved): 38
- Technology nodes with at least one evaluable Eureka: 83
- Research stage trigger: `(medicine_hippocratic AND (geometry_axiomatic OR algebra)) AND (cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper) AND (((cuneiform AND stamp_seal) AND cuneiform) AND legal_code_roman)`
- Stage-trigger prerequisite closure: 32 nodes — 30 technologies (all forced into the trunk) and 2 civics
- Registry entities: 129; with unresolved corpus references: `inst.newspaper` (postal_imperial)
- Frontier (age F) nodes normalized to A9 + frontier: 16
- Repeatable descriptors kept as data (levels not implemented): 10

## Corpus problems found (none repaired silently)

1. **No research cost in the corpus.** BaseCost is calibrated: content band × the Age's RP unit (`scripts/research-calibration/nodes.json`; `docs/research-calibration-report.md`). Depth no longer sets cost.
2. **Eurekas are prose.** Only faithful mappings are machine-evaluable; the rest keep their text with a status.
3. **`inst.newspaper` references `postal_imperial`,** which is not a technology, civic or institution (it survives only as a `reclass` entry). Declared unresolved; `inst.newspaper`, and everything that requires it, is never knowledge-eligible.
4. **Age `F`** (16 nodes) is not one of the nine Ages; normalized to A9 with `frontier: true` (architecture §17.5).
5. **`research.repeatable`** on 10 frontier nodes conflicts with idempotent completion; the nodes complete once (D-044 Part D T6).
6. **Generation numbers are display lineage, not a dependency rule.** 13 nodes break 'gen N requires gen N-1'; not validated (D-044 R7).
7. **No Civics nodes existed.** The six architecture §5.7 candidates were reclassified; no Civics content was invented.
8. **`effects.immediate` is empty on every node.** No immediate-effect kind is ratified (law 2), so the loader requires it empty.
9. **21 Eureka circumstances name knowledge the node's own prerequisites already guarantee** (e.g. "circumstance: fire" on a node that requires fire_making). Evaluated, each would fire the moment the node became available — a flat cost cut, not a circumstance. Declared `implied-by-prerequisites`, not evaluated (ADR-029 §7): `heat_treatment_stone` (fire_making), `microlith` (adhesive_natural), `birch_tar` (fire_making), `sling` (cordage), `raft` (cordage), `dugout` (fire_making), `nixtamalization` (maize), `kiln_updraft` (mudbrick), `sledge` (cordage), `steel_carburized` (charcoal), `cast_iron` (charcoal), `iron_mouldboard` (cast_iron), `woodblock_print` (paper), `paper_money` (paper), `double_entry` (paper), `bill_of_exchange` (paper), `printing_press` (abjad OR alphabet_vowels), `cannon_cast_iron` (cast_iron), `steam_rotary` (gearing), `germ_theory` (microscope), `steam_turbine` (steam_high_pressure).
10. **0 Eureka circumstances are dead** — a faithful reading names knowledge completable only after the node itself.
11. **268 Eureka strings are ambiguous prose** that cannot be decomposed without fabricating a condition; kept verbatim as `requires-authoring` with a reason (below).

## Technique candidates (Director ruling 2026-10-01 §11 — flagged, NOT moved)

| node | age | reason |
|---|---|---|
| `pressure_flaking` | A1 | Reads as a practical working method applied to existing knapping knowledge. |
| `ochre_processing` | A1 | Practical processing method applying grinding knowledge. |
| `nixtamalization` | A2 | A food-processing procedure applying known materials rather than new knowledge. |
| `dyeing` | A2 | Reads as a craft practice applied to existing cloth. |
| `terrace` | A2 | Practical land-management method. |
| `trepanation` | A2 | A surgical procedure applying existing blade tools. |
| `sheet_metal` | A3 | Hammering/raising sheet is a practical working method applied to existing bronze. |
| `surveying` | A3 | Rope-stretching land measurement is an applied method of geometric knowledge. |
| `quenching` | A4 | Reads as a practical working method applied to steel. |
| `tempering` | A4 | A procedural method of working steel. |
| `pattern_welding` | A4 | A smithing method applying known materials. |
| `hacksilver` | A4 | A practice of using weighed metal for exchange. |
| `latitude_sailing` | A5 | Practical method of applying star knowledge to navigation. |
| `monsoon_sailing` | A5 | A navigational practice rather than new technology. |
| `sugar_refining` | A6 | A processing method (boiling to crystal) applied to a crop. |
| `saltpetre_refining` | A6 | Leaching and recrystallisation is a practical production method. |
| `wire_drawing` | A6 | Drawing metal through dies is a craft process. |
| `double_entry` | A6 | A recording method/practice rather than discovered knowledge. |
| `type_founding` | A7 | Reads as a manufacturing method (hand-mould casting). |
| `celestial_navigation` | A7 | Practical method of applying astronomy at sea. |
| `mercator` | A7 | Method of applying geometry to chart-making. |
| `corned_powder` | A7 | Granulation is a processing method. |
| `brass_calamine` | A7 | A process method (cementation). |
| `inoculation` | A7 | Medical practice applying observed immunity. |
| `crop_rotation_norfolk` | A7 | A farming method/rotation scheme. |
| `selective_breeding` | A7 | Practice of applying controlled breeding. |
| `interchangeable_parts` | A8 | Reads as a manufacturing method/system of practice rather than a discrete device. |
| `macadam` | A8 | A construction method applying known materials rather than new knowledge. |
| `antisepsis` | A8 | Sterile/antiseptic procedure is a practical method of applying germ theory. |
| `container_shipping` | A9 | Standardisation and handling practice rather than new physical knowledge. |
| `antibiotic_resistance` | A9 | Described explicitly as stewardship practice. |
| `canning` | A9 | Practical preservation method applied empirically before being understood. |
| `cyber_warfare` | A9 | Reads as a practice of applying networked computing to conflict. |
| `pcr` | A9 | A laboratory procedure for applying known enzymology. |
| `ivf` | A9 | Clinical procedure applying reproductive biology. |
| `oral_rehydration` | A9 | A practical treatment method rather than new knowledge. |
| `drip_irrigation` | A9 | Practical method of applying water to crops. |
| `precision_agriculture` | A9 | Practice of applying existing positioning tech to farming. |

## Cost calibration (every node: band, breadth, kind, cost and the reason)

cost = round(BAND_WEIGHT × AGE_UNIT[age]); BAND_WEIGHT {"M1": 1.0, "M2": 2.0, "M3": 3.5, "M4": 6.0, "M5": 10.0}; AGE_UNIT {"A1": 10.99, "A2": 9.44, "A3": 133.49, "A4": 130.08, "A5": 178.08, "A6": 261.85, "A7": 129.85, "A8": 33.74, "A9": 39.01}.

| id | age | band | breadth | kind | cost | rationale |
|---|---|---|---|---|---|---|
| `fire_making` | A1 | M5 | civilization | breakthrough | 110 | Controlled fire at will transforms diet, warmth, defence and enables all pyrotechnology; Age-defining. |
| `knapping_oldowan` | A1 | M4 | domain | breakthrough | 66 | First deliberate stone tool-making; foundational but technically simple, bounded to toolmaking. *(review: M3→M4 — Oldowan is the first stone-tool breakthrough; it is rated below Levallois (M4), which refines it.)* |
| `knapping_levallois` | A1 | M4 | domain | breakthrough | 66 | Conceptually novel planned reduction: predetermined flake form requires mental template; reshapes lithic domain. |
| `knapping_blade` | A1 | M3 | domain | advance | 38 | Efficient prismatic-core blade industry broadly expands tool forms; substantial but builds on prepared-core concept. |
| `pressure_flaking` | A1 | M2 | narrow | advance | 22 | New finishing method yielding finer points; bounded capability within lithics. |
| `heat_treatment_stone` | A1 | M3 | domain | breakthrough | 38 | First pyrotechnology altering material properties; conceptually novel though narrow in output. |
| `microlith` | A1 | M3 | domain | advance | 38 | Modular replaceable composite edges broadly reshape toolkits and weapons. |
| `ground_stone_early` | A1 | M2 | domain | advance | 22 | New shaping method giving durable resharpenable axes; bounded capability. |
| `adhesive_natural` | A1 | M2 | narrow | advance | 22 | Use of found binders enables hafting; simple collection, bounded application. |
| `birch_tar` | A1 | M3 | narrow | advance | 38 | First synthetic material via controlled anaerobic distillation; real technical difficulty, narrow use. |
| `hafting` | A1 | M4 | domain | breakthrough | 66 | Composite tool concept opens a new family (spears, axes, projectiles) transforming tool and weapon domains. |
| `cordage` | A1 | M4 | civilization | breakthrough | 66 | Fibre twisting underpins hafting, bows, nets, boats, textiles; conceptually novel, broad enabler. |
| `basketry` | A1 | M2 | domain | advance | 22 | New container craft; useful but bounded application of cordage skills. |
| `atlatl` | A1 | M2 | domain | advance | 22 | Lever device boosting dart range; clear bounded hunting/military gain. |
| `bow_simple` | A1 | M4 | domain | breakthrough | 66 | Stored-energy projectile is conceptually novel, transforming hunting and warfare. |
| `sling` | A1 | M1 | narrow | incremental | 11 | Simple cord application for throwing stones; small conceptual step. |
| `shelter_hut` | A1 | M2 | domain | advance | 22 | Durable framed dwelling enabling seasonal occupation; bounded new capability. |
| `hide_working` | A1 | M2 | domain | advance | 22 | Processing skins into leather and fur; new practice with clear bounded output. |
| `sewing` | A1 | M3 | civilization | advance | 38 | Fitted clothing enabled cold-climate expansion; broad demographic impact with fine bone-tool craft. |
| `raft` | A1 | M3 | domain | breakthrough | 38 | First water transport opens a new transport line enabling sea crossings; simple materials. |
| `dugout` | A1 | M2 | domain | advance | 22 | Sturdier hull via fire and adze; bounded improvement within water transport. |
| `fishing_hook` | A1 | M2 | narrow | advance | 22 | New line-fishing tool; clear bounded subsistence gain. |
| `grinding_stone` | A1 | M2 | domain | advance | 22 | Quern processing of seeds and pigment; bounded new food-processing tool. |
| `dog_domestication` | A1 | M4 | domain | breakthrough | 66 | First domestication; conceptually novel human-animal partnership changing hunting and later herding. |
| `ochre_processing` | A1 | M1 | narrow | incremental | 11 | Narrow application of grinding to pigment; small conceptual step. |
| `bone_tools` | A1 | M2 | domain | advance | 22 | New material class worked via groove-and-splinter; bounded expansion of tool forms. |
| `lamp` | A1 | M1 | narrow | incremental | 11 | Narrow application of fire to portable light; small step. |
| `tally_notation` | A1 | M3 | domain | breakthrough | 38 | First external record of quantity; conceptually novel seed of numeracy, though technically trivial. |
| `cereal_cultivation` | A2 | M5 | civilization | breakthrough | 94 | Deliberate cereal sowing is the origin of agriculture, transforming subsistence and enabling sedentism civilization-wide. |
| `cereal_domesticated` | A2 | M4 | domain | breakthrough | 57 | Genetic domestication yields reliable high-yield staples, changing agriculture fundamentally, though it follows from cultivation. |
| `rice_wet` | A2 | M4 | domain | breakthrough | 57 | Paddy flooding and transplanting is a distinct, demanding agricultural system that founded East and South Asian civilizations. |
| `millet` | A2 | M2 | domain | advance | 19 | Regional dry-farming cereal applying cultivation knowledge to new species; important but bounded. |
| `maize` | A2 | M3 | domain | advance | 33 | Most genetically transformed crop, millennia of selection; founding staple of the Americas. |
| `sorghum_pearl_millet` | A2 | M2 | domain | advance | 19 | Regional drought-adapted cereal domestication applying known cultivation principles. |
| `root_crop` | A2 | M3 | domain | advance | 33 | Clonal vegetative propagation is a conceptually distinct agricultural pathway supporting tropical societies. |
| `legume` | A2 | M2 | domain | advance | 19 | Adds protein and nitrogen-fixing crops alongside cereals; valuable but bounded extension. |
| `orchard` | A2 | M3 | domain | advance | 33 | Perennial tree cultivation needs multi-decade investment and grafting-era sophistication, producing oil and wine economies. |
| `sickle` | A2 | M2 | domain | advance | 19 | New harvesting tool with clear bounded capability, built from existing blade and haft knowledge. |
| `digging_stick_hoe` | A2 | M1 | narrow | incremental | 9 | Hafted tillage blade combines existing hafting and ground stone; small conceptual step. |
| `nixtamalization` | A2 | M2 | narrow | advance | 19 | Alkaline processing making maize a viable staple; a specific practical method of narrow scope. |
| `sheep_goat` | A2 | M4 | civilization | breakthrough | 57 | First herd animal domestication opens pastoralism and the whole livestock line. |
| `cattle` | A2 | M3 | domain | advance | 33 | Large, dangerous animal domestication enabling milk, traction and dung; broad but follows herding precedent. |
| `pig` | A2 | M2 | domain | advance | 19 | Additional livestock species within established domestication; bounded capability. |
| `milking` | A2 | M3 | domain | advance | 33 | Secondary-products exploitation of living animals greatly raises pastoral yield; needs processing and vessels. |
| `wool` | A2 | M2 | domain | advance | 19 | Selective breeding for fleece yields a new textile fibre; bounded within textiles and herding. |
| `animal_traction` | A2 | M4 | civilization | breakthrough | 57 | Harnessing animal power is a new energy source, prerequisite to plough and cart. |
| `pottery_open_fired` | A2 | M4 | civilization | breakthrough | 57 | First synthetic material by firing clay; boiling containers and storage transform food and craft. |
| `kiln_updraft` | A2 | M3 | domain | advance | 33 | Enclosed high-temperature controlled firing; thermal precondition for smelting with real technical difficulty. |
| `potters_wheel_fast` | A2 | M3 | domain | advance | 33 | Momentum wheel enables mass production and craft specialization; precise mechanical sophistication. |
| `mudbrick` | A2 | M3 | domain | advance | 33 | Modular standardized building unit enabling permanent multi-storey settlements and early towns. |
| `lime_plaster` | A2 | M3 | domain | advance | 33 | First chemically transformed building material via high-heat calcination; fuel-hungry pyrotechnology. |
| `timber_frame` | A2 | M2 | domain | advance | 19 | Post-and-beam structures allow large longhouses; bounded construction method from axe and hut knowledge. |
| `stone_dry` | A2 | M1 | narrow | incremental | 9 | Fitting unmortared stone is a simple refinement of existing stone handling. |
| `well` | A2 | M2 | domain | advance | 19 | Groundwater access frees settlement from surface water; clear but bounded capability. |
| `fermentation_grain` | A2 | M2 | domain | advance | 19 | New controlled biochemical process yielding storable safe drink; bounded to food domain. |
| `fermentation_fruit` | A2 | M1 | narrow | incremental | 9 | Variant of fermentation applied to fruit and honey; small conceptual step. |
| `oil_press` | A2 | M2 | domain | advance | 19 | New mechanical extraction method creating a valuable product; bounded within food/craft. |
| `salt_extraction` | A2 | M2 | domain | advance | 19 | New preservation and trade good production; important but bounded method. |
| `flax` | A2 | M2 | domain | advance | 19 | New fibre crop domestication enabling linen textiles; bounded agricultural addition. |
| `spinning_spindle` | A2 | M2 | domain | advance | 19 | Whorl spindle gives continuous thread, foundational textile tool with bounded scope. |
| `loom_warp_weighted` | A2 | M3 | domain | advance | 33 | Substantial textile capability: wide cloth and complex weaves, real mechanical sophistication. |
| `dyeing` | A2 | M2 | domain | advance | 19 | Mordant chemistry adds colour to textiles; real but bounded new practice. |
| `copper_native` | A2 | M3 | domain | breakthrough | 33 | First metal working via hammering and annealing opens metallurgy line, though limited output. |
| `copper_smelting` | A2 | M5 | civilization | breakthrough | 94 | First extractive metallurgy; defines the Chalcolithic and transforms tools, trade, and power. |
| `charcoal` | A2 | M3 | domain | advance | 33 | High-temperature fuel enabling metallurgy; controlled pyrolysis with broad industrial importance. |
| `casting_open` | A2 | M3 | domain | advance | 33 | Molten-metal casting allows mass-reproducible shapes; substantial metallurgical capability. |
| `gold_silver_native` | A2 | M1 | narrow | incremental | 9 | Applies native-metal working to other metals; narrow ornamental extension. |
| `irrigation_basin` | A2 | M3 | domain | advance | 33 | Flood-basin control greatly raises yields across floodplain agriculture. |
| `irrigation_canal` | A2 | M4 | civilization | breakthrough | 57 | Engineered water diversion with institutional maintenance; underpinned urban civilization in Mesopotamia. |
| `terrace` | A2 | M2 | domain | advance | 19 | New land-shaping method making hillsides arable; bounded agricultural capability. |
| `sledge` | A2 | M1 | narrow | incremental | 9 | Simple runner-based hauling; small step from dragging loads. |
| `wheel_solid` | A2 | M4 | civilization | breakthrough | 57 | Wheel and axle is conceptually novel, opening vehicular transport and rotary machinery lines. |
| `cart` | A2 | M2 | domain | advance | 19 | Direct application of wheel to bulk overland haulage; clear bounded capability. |
| `track_road` | A2 | M1 | narrow | incremental | 9 | Simple built paths; modest infrastructure refinement. |
| `plough_ard` | A2 | M4 | civilization | breakthrough | 57 | Animal-powered tillage multiplies area per farmer, transforming agricultural productivity and social structure. |
| `token_counting` | A2 | M3 | domain | breakthrough | 33 | Abstract commodity accounting system; conceptual precursor to writing, broad administrative use. |
| `stamp_seal` | A2 | M2 | domain | advance | 19 | New authentication/ownership device; bounded administrative capability. |
| `calendar_lunar` | A2 | M2 | domain | advance | 19 | Systematic month tracking for agricultural timing; bounded new knowledge. |
| `trepanation` | A2 | M2 | narrow | advance | 19 | Remarkable surgical procedure with survival, but narrow medical application. |
| `arsenical_bronze` | A3 | M3 | domain | advance | 467 | First deliberate copper alloy; harder castable metal from local ores, opening the bronze line without tin trade. |
| `tin_bronze` | A3 | M5 | civilization | breakthrough | 1335 | Defines the Bronze Age: reproducible superior alloy reshaping tools, weapons, and long-distance tin trade networks. |
| `casting_closed` | A3 | M2 | domain | advance | 267 | Bivalve moulds enable socketed, symmetric objects; clear but bounded improvement over open casting. |
| `lost_wax` | A3 | M3 | domain | advance | 467 | Allows arbitrary complex shapes; substantial skill and material sophistication within metallurgy and art. |
| `sheet_metal` | A3 | M2 | domain | advance | 267 | New forming method for vessels and armour; bounded within metalworking. |
| `mining_shaft` | A3 | M3 | domain | advance | 467 | Underground extraction following ore bodies greatly expands raw material supply; real engineering difficulty. |
| `proto_writing` | A3 | M4 | civilization | breakthrough | 801 | Conceptually novel externalised record-keeping opening the writing family, though limited to accounting without syntax. |
| `cuneiform` | A3 | M5 | civilization | breakthrough | 1335 | Full writing of language: epochal, Age-defining transformation of administration, law, and knowledge transmission. |
| `hieroglyphic` | A3 | M4 | civilization | breakthrough | 801 | Full writing system, but likely stimulus-diffused parallel to cuneiform; major yet not the singular epochal origin. |
| `chinese_script` | A3 | M4 | civilization | breakthrough | 801 | Independent invention of full writing, founding the enduring Chinese script lineage. |
| `papyrus` | A3 | M3 | domain | advance | 467 | Light portable writing medium substantially expands document use, but geographically narrow material. |
| `numeral_sexagesimal` | A3 | M4 | domain | breakthrough | 801 | First place-value notation: conceptually novel, transforms all subsequent computation. |
| `arithmetic_babylonian` | A3 | M3 | domain | advance | 467 | Reciprocal tables and quadratic problems give broad computational mathematics built on place value. |
| `surveying` | A3 | M2 | domain | advance | 267 | Practical measurement of fields with ropes and triangles; bounded administrative capability. |
| `sail_square` | A3 | M4 | civilization | breakthrough | 801 | First harnessing of wind for propulsion; opens sailing line and reshapes transport and trade. |
| `plank_boat` | A3 | M3 | domain | advance | 467 | Hulls beyond log size enable larger seagoing vessels; substantial woodworking sophistication. |
| `horse_domestication` | A3 | M4 | civilization | breakthrough | 801 | Transforms mobility, warfare, and steppe economies across Eurasia. |
| `donkey` | A3 | M2 | domain | advance | 267 | Pack animal domestication with clear but bounded transport benefit. |
| `camel` | A3 | M3 | domain | advance | 467 | Opens desert and steppe transport regions, enabling caravan trade; broad regional impact. |
| `wheel_spoked` | A3 | M3 | domain | advance | 467 | Light precise wheel requiring bent-wood joinery; enables fast vehicles. |
| `chariot` | A3 | M4 | civilization | breakthrough | 801 | Dominant Late Bronze Age military platform, reshaping warfare and elite power; combines horse and spoked wheel. |
| `composite_bow` | A3 | M3 | domain | advance | 467 | Laminated multi-material bow greatly increases power; sophisticated construction within military domain. |
| `fired_brick` | A3 | M2 | domain | advance | 267 | Durable standardised building material; bounded improvement over mudbrick. |
| `glass_glaze` | A3 | M2 | domain | advance | 267 | Vitreous coatings and faience; new material craft, bounded in scope. |
| `glass_core` | A3 | M2 | domain | advance | 267 | First glass vessels; new luxury material but narrow application. |
| `standard_weights` | A3 | M3 | civilization | advance | 467 | State-issued standards underpin trade, taxation, and exchange broadly; institutional sophistication. |
| `calendar_civil` | A3 | M2 | domain | advance | 267 | Administrative 365-day year; useful refinement of existing calendar knowledge. |
| `water_clock` | A3 | M2 | narrow | advance | 267 | New timekeeping device by regulated flow; narrow bounded capability. |
| `medicine_recorded` | A3 | M2 | domain | advance | 267 | Writing applied to medicine; bounded advance codifying existing practice. |
| `beekeeping` | A3 | M1 | narrow | incremental | 133 | Managed hives yield honey and wax; narrow extension of animal husbandry. |
| `iron_bloomery` | A4 | M5 | civilization | breakthrough | 1301 | Iron smelting from ubiquitous ore defines the Iron Age, democratizing metal tools and weapons across civilization. |
| `bellows` | A4 | M2 | domain | advance | 260 | New tool raising furnace temperatures; clear but bounded capability within metallurgy/energy. |
| `steel_carburized` | A4 | M3 | domain | advance | 455 | Substantial material capability: hardened steel edges from wrought iron, real technical difficulty. |
| `quenching` | A4 | M2 | narrow | advance | 260 | New heat-treatment step giving hardness but bounded and brittle without tempering. |
| `tempering` | A4 | M2 | narrow | incremental | 260 | Completes heat-treatment cycle; important practical refinement within metallurgy. |
| `wootz` | A4 | M3 | domain | advance | 455 | Crucible high-carbon steel is materially sophisticated and difficult, but regionally narrow in impact. |
| `pattern_welding` | A4 | M2 | narrow | advance | 260 | Composite blade forging method combining existing materials; bounded weapon-making advance. |
| `abjad` | A4 | M5 | civilization | breakthrough | 1301 | Alphabetic writing learnable in weeks transforms literacy and administration civilization-wide. |
| `alphabet_vowels` | A4 | M3 | domain | advance | 455 | Full phonemic writing materially extends the alphabet but builds directly on the abjad. |
| `brahmi` | A4 | M3 | domain | advance | 455 | Alphasyllabary founding the Indic script family; significant but parallel to existing writing systems. |
| `hacksilver` | A4 | M2 | domain | advance | 260 | Standardized weighed silver is a new exchange practice, bounded precursor to coinage. |
| `coinage_electrum` | A4 | M4 | civilization | breakthrough | 780 | State-guaranteed coinage changes how exchange, taxation and armies work across economies. |
| `cavalry` | A4 | M4 | domain | breakthrough | 780 | Mounted warfare transformed military organization, displacing chariots everywhere. |
| `saddle` | A4 | M1 | narrow | incremental | 130 | Padded seat is a small refinement improving rider stability. |
| `siege_ram` | A4 | M2 | narrow | advance | 260 | Purpose-built siege engine with bounded military capability against walls. |
| `siege_tower` | A4 | M2 | narrow | advance | 260 | Mobile towers and ramps extend siegecraft; bounded and labour-intensive. |
| `stone_fortification` | A4 | M3 | domain | advance | 455 | Ashlar walls with towers and gates require major masonry skill and reshape defence. |
| `crossbow` | A4 | M3 | domain | advance | 455 | Trigger-mechanism weapon with high power and low training; mechanically sophisticated, broad military effect. |
| `naval_ram` | A4 | M3 | domain | advance | 455 | Trireme ramming warship needs sophisticated hull construction and transforms naval warfare. |
| `qanat` | A4 | M4 | domain | breakthrough | 780 | Conceptually novel underground aquifer tapping opens arid plains to settlement; hard survey engineering. |
| `aqueduct_tunnel` | A4 | M3 | domain | advance | 455 | Rock-cut tunnels with surveying precision bring urban water; technically difficult. |
| `aqueduct_channel` | A4 | M2 | domain | advance | 260 | Gravity channel over distance applies known hydraulic knowledge with bounded new capability. |
| `arch` | A4 | M4 | domain | breakthrough | 780 | Voussoir compression arch is a conceptual structural breakthrough changing construction thereafter. |
| `astronomy_records` | A4 | M3 | domain | advance | 455 | Centuries-long systematic observation creates the empirical basis of astronomy; institutional sophistication. |
| `calendar_lunisolar` | A4 | M2 | domain | advance | 260 | Intercalation reconciles lunar and solar years; bounded refinement of calendrics. |
| `geometry_practical` | A4 | M3 | domain | advance | 455 | Systematized area, volume and similarity broadly underpins surveying and building. |
| `medicine_hippocratic` | A4 | M4 | domain | breakthrough | 780 | Natural causation and clinical observation changed how medicine works across multiple civilizations. |
| `glass_blown_precursor` | A4 | M2 | narrow | advance | 260 | Cast and mosaic glass extends luxury vessel production; bounded craft advance. |
| `rotary_quern` | A4 | M2 | domain | advance | 260 | Continuous rotary grinding is a genuine new mechanism improving food processing labour. |
| `olive_press_beam` | A4 | M2 | narrow | advance | 260 | Lever-and-weight press enables industrial oil production; bounded application of lever principle. |
| `geometry_axiomatic` | A5 | M5 | civilization | breakthrough | 1781 | Deductive proof from axioms defines classical knowledge; conceptually novel, foundational for all later mathematics and science. |
| `mechanics_archimedean` | A5 | M4 | domain | breakthrough | 1068 | Theoretical statics turns machine building from trial-and-error into design; opens engineering science. |
| `torsion_artillery` | A5 | M3 | domain | advance | 623 | Formula-calibrated sinew-spring engines; major, technically demanding military capability within warfare. |
| `water_screw` | A5 | M2 | domain | advance | 356 | New bounded water-lifting device for irrigation and drainage; useful but narrow. |
| `noria` | A5 | M2 | domain | advance | 356 | Bucket wheel enabling lift irrigation at scale; clear but domain-bounded advance. |
| `water_mill` | A5 | M4 | civilization | breakthrough | 1068 | First non-muscle prime mover; opens the whole line of mechanical power. |
| `trip_hammer` | A5 | M2 | domain | advance | 356 | Cam-driven application of water power to hulling, ore crushing, fulling; bounded extension of mills. |
| `gearing` | A5 | M3 | domain | advance | 623 | Precision gear trains transforming motion; broad mechanical capability with real difficulty. |
| `concrete_pozzolan` | A5 | M4 | domain | breakthrough | 1068 | Hydraulic-setting material transforms construction, enabling vaults, harbours and aqueducts. |
| `vault_dome` | A5 | M3 | domain | advance | 623 | Large compressive spans; major architectural capability building on concrete and arch. |
| `road_paved` | A5 | M3 | domain | advance | 623 | Engineered, drained road networks; broad infrastructure capability requiring surveying and state organisation. |
| `bridge_stone` | A5 | M2 | domain | advance | 356 | Applies arch masonry to multi-span river crossings; notable but bounded. |
| `aqueduct_arcade` | A5 | M2 | domain | advance | 356 | Elevated arched water channels; combines existing arch and surveying for bounded new capability. |
| `glass_blowing` | A5 | M3 | domain | advance | 623 | Transforms glass from luxury to cheap mass vessels; new method with broad material impact. |
| `window_glass` | A5 | M1 | narrow | incremental | 178 | Narrow application of existing glass working to cast panes. |
| `cast_iron` | A5 | M4 | domain | breakthrough | 1068 | Fully molten iron above 1150C changes metallurgy, enabling mass-cast tools; hard high-temperature prerequisite. |
| `iron_mouldboard` | A5 | M2 | domain | advance | 356 | Soil-turning cast-iron plough; clear agricultural productivity gain within farming. |
| `seed_drill` | A5 | M2 | domain | advance | 356 | Row sowing at depth; bounded agricultural tool improving yields. |
| `collar_harness` | A5 | M3 | domain | advance | 623 | Triples horse traction; broad transport and agricultural impact from a simple redesign. |
| `paper` | A5 | M5 | civilization | breakthrough | 1781 | Cheap abundant writing surface transforms record-keeping, administration and knowledge diffusion civilization-wide. |
| `crossbow_repeating` | A5 | M1 | narrow | incremental | 178 | Magazine variant of the existing crossbow; narrow military refinement. |
| `stirrup` | A5 | M3 | domain | advance | 623 | Simple device that reshapes mounted warfare, enabling shock cavalry. |
| `silk` | A5 | M3 | domain | advance | 623 | Domestication of silkworm plus reeling creates a major luxury fibre industry and trade. |
| `lacquer` | A5 | M1 | narrow | incremental | 178 | Narrow decorative coating craft from a regional tree sap. |
| `astronomy_geometric` | A5 | M3 | domain | advance | 623 | Predictive geometric planetary models; substantial, mathematically sophisticated advance in astronomy. |
| `cartography` | A5 | M3 | domain | advance | 623 | Coordinate grids and projection make maps mathematical; broad within geography. |
| `latitude_sailing` | A5 | M2 | domain | advance | 356 | Celestial north-south positioning; clear bounded navigation method. |
| `monsoon_sailing` | A5 | M2 | domain | advance | 356 | Exploiting seasonal winds opens Indian Ocean crossings; bounded navigation practice with trade impact. |
| `lateen` | A5 | M2 | domain | advance | 356 | Fore-and-aft rig allowing closer windward sailing; new bounded maritime capability. |
| `mortise_hull` | A5 | M2 | domain | advance | 356 | Rigid shell-first joinery enabling large cargo hulls; bounded shipbuilding advance. |
| `pharmacology` | A5 | M2 | domain | advance | 356 | Systematic drug catalogues organise medical knowledge; bounded within medicine. |
| `surgery_instruments` | A5 | M1 | narrow | incremental | 178 | Standardisation of existing tool types for surgery; narrow refinement. |
| `abacus` | A5 | M2 | domain | advance | 356 | Calculation device speeding commerce and administration; clear but bounded tool. |
| `alchemy` | A5 | M3 | domain | advance | 623 | Systematic theory-driven manipulation of substances; broad proto-chemistry with apparatus sophistication. |
| `distillation` | A5 | M3 | domain | advance | 623 | Alembic separation of liquids; new method underpinning chemistry, spirits and perfumes. |
| `heavy_plough` | A6 | M4 | civilization | breakthrough | 1571 | Opened heavy clay lowlands to farming, reshaping northern European agriculture and settlement; iron and large ox teams required. |
| `horse_collar` | A6 | M3 | domain | advance | 916 | Unlocked horse traction for ploughing and haulage, a broad agricultural and transport gain from a simple device. |
| `horseshoe` | A6 | M2 | domain | advance | 524 | New bounded tool extending horse usefulness on hard and wet ground. |
| `rice_champa` | A6 | M3 | domain | advance | 916 | Double-cropping variety drove major population growth, but a crop improvement within existing rice agriculture. |
| `sugar_refining` | A6 | M2 | domain | advance | 524 | New food-processing method creating a valuable commodity, bounded to one domain. |
| `windmill_vertical` | A6 | M3 | domain | breakthrough | 916 | First harnessing of wind for mechanical work, opening a new power source family. |
| `windmill_post` | A6 | M3 | domain | advance | 916 | Independent horizontal-axis design with yaw, substantial wind-power capability in Europe. |
| `fulling_mill` | A6 | M2 | domain | advance | 524 | Applies water power to a textile process; bounded mechanisation step. |
| `spinning_wheel` | A6 | M2 | domain | advance | 524 | Tripled thread output, a clear but bounded textile productivity gain. |
| `horizontal_loom` | A6 | M2 | domain | advance | 524 | Treadle heddles gave faster, wider weaving; bounded textile advance. |
| `cotton_gin_roller` | A6 | M1 | narrow | incremental | 262 | Narrow fibre-processing device applying known roller/crank mechanics. |
| `compass_magnetic` | A6 | M4 | civilization | breakthrough | 1571 | Conceptually novel magnetic direction-finding that transformed open-sea navigation. |
| `stern_rudder` | A6 | M3 | domain | advance | 916 | Made large hulls steerable, enabling bigger ships; substantial naval capability. |
| `watertight_bulkhead` | A6 | M2 | domain | advance | 524 | Bounded hull-safety improvement within shipbuilding. |
| `portolan` | A6 | M2 | domain | advance | 524 | Practical charting method building on the compass; bounded navigational aid. |
| `astrolabe` | A6 | M3 | domain | advance | 916 | Sophisticated analogue instrument for time, latitude and astronomy requiring precise metalwork. |
| `cog` | A6 | M2 | domain | advance | 524 | New bulk cargo hull type enabling Hanseatic trade; bounded naval design. |
| `gunpowder` | A6 | M5 | civilization | breakthrough | 2618 | Age-defining chemical energy source transforming warfare, mining and states for centuries. |
| `saltpetre_refining` | A6 | M2 | narrow | advance | 524 | Process refinement removing gunpowder's input bottleneck; bounded chemical method. |
| `cannon_early` | A6 | M4 | domain | breakthrough | 1571 | First gunpowder projectile weapons opened the firearm family, changing warfare fundamentally. |
| `trebuchet` | A6 | M3 | domain | advance | 916 | Powerful gravity siege engine, major military capability with engineering difficulty. |
| `longbow` | A6 | M2 | narrow | advance | 524 | Powerful bow variant whose impact depended on training; bounded military advance. |
| `plate_armour` | A6 | M3 | domain | advance | 916 | Culmination of armour requiring steel plate and powered hammers; sophisticated materials. |
| `greek_fire` | A6 | M2 | narrow | advance | 524 | Potent but narrow secret incendiary weapon with little lasting diffusion. |
| `woodblock_print` | A6 | M4 | civilization | breakthrough | 1571 | First mechanical reproduction of text and images, opening the printing family. |
| `movable_type_ceramic` | A6 | M3 | domain | advance | 916 | Conceptually new reusable type, but limited practical adoption. |
| `movable_type_metal` | A6 | M3 | domain | advance | 916 | Durable cast type improving movable-type printing with metallurgical sophistication. |
| `paper_money` | A6 | M4 | civilization | breakthrough | 1571 | State fiat notes changed how money works, requiring printing and strong institutions. |
| `hindu_arabic` | A6 | M5 | civilization | breakthrough | 2618 | Place-value decimal with zero transformed all calculation, commerce and science. |
| `algebra` | A6 | M4 | domain | breakthrough | 1571 | Systematic equation solving founded a new mathematical discipline. |
| `trigonometry` | A6 | M3 | domain | advance | 916 | Substantial mathematical tool for astronomy and navigation building on Greek chord tables. |
| `optics_ibn_haytham` | A6 | M4 | domain | breakthrough | 1571 | Correct theory of vision plus experimental method changed how optics and science were done. |
| `spectacles` | A6 | M2 | domain | advance | 524 | Practical lens grinding extending working lives; clear but bounded capability. |
| `crank_connecting_rod` | A6 | M3 | domain | advance | 916 | General rotary-reciprocating conversion underpinning broad machinery. |
| `escapement_verge` | A6 | M4 | civilization | breakthrough | 1571 | Mechanical regulation enabled the clock, a conceptually novel family with broad social effects. |
| `blast_furnace_water` | A6 | M4 | domain | breakthrough | 1571 | Reaching cast-iron temperatures transformed European iron production in volume and kind. |
| `wire_drawing` | A6 | M2 | narrow | advance | 524 | Bounded metalworking method producing uniform wire. |
| `canal_lock` | A6 | M3 | domain | advance | 916 | Enabled canals across gradients, substantial infrastructure capability. |
| `double_entry` | A6 | M3 | domain | advance | 916 | Auditable accounting enabling capital firms; substantial institutional method. |
| `bill_of_exchange` | A6 | M3 | domain | advance | 916 | Moved value across distance without coin, broadly enabling long-distance commerce. |
| `printing_press` | A7 | M5 | civilization | breakthrough | 1298 | Epochal: mass reproduction of text transforms literacy, science, religion and administration civilization-wide. |
| `type_founding` | A7 | M3 | domain | advance | 454 | Precision interchangeable casting from matrices; difficult and foundational to printing, but bounded to typography. |
| `caravel` | A7 | M3 | domain | advance | 454 | Fast, shallow-draft exploration hull enabling ocean reconnaissance; major within naval domain. |
| `carrack` | A7 | M4 | civilization | breakthrough | 779 | Full-rigged ocean cargo/warship opened sustained oceanic trade and empire; changed seafaring fundamentally. |
| `polynesian_canoe` | A7 | M3 | domain | advance | 454 | Double-hulled voyaging with non-instrument navigation enabled Pacific colonization; sophisticated but regional. |
| `celestial_navigation` | A7 | M3 | domain | advance | 454 | Instrumented latitude finding with declination tables makes open-ocean navigation reliable; broad naval capability. |
| `longitude_problem` | A7 | M4 | domain | breakthrough | 779 | Marine chronometer solved longitude, an extreme precision-engineering problem transforming navigation. |
| `mercator` | A7 | M2 | narrow | advance | 260 | Conformal projection is a bounded mathematical tool for charts; useful but narrow. |
| `corned_powder` | A7 | M2 | domain | incremental | 260 | Process refinement of gunpowder giving consistent, stronger, storable powder; bounded improvement. |
| `cannon_cast_bronze` | A7 | M3 | domain | advance | 454 | Large one-piece casting of siege guns; materially demanding and decisive in warfare. |
| `cannon_cast_iron` | A7 | M2 | domain | advance | 260 | Cheaper iron guns broaden artillery access; cost-driven variant of existing cannon. |
| `matchlock` | A7 | M3 | domain | advance | 454 | First practical trigger-fired infantry firearm, reshaping infantry warfare. |
| `wheellock` | A7 | M2 | narrow | advance | 260 | Self-igniting mechanism enabling pistols; precise but expensive, niche ignition variant. |
| `flintlock` | A7 | M3 | domain | advance | 454 | Cheap reliable ignition standardised the musket for two centuries; broad military impact. |
| `trace_italienne` | A7 | M3 | domain | advance | 454 | New fortification geometry countering cannon, reshaping siege warfare across the domain. |
| `ship_of_line` | A7 | M3 | domain | advance | 454 | Integrates heavy guns and full-rig hulls into line-of-battle naval doctrine; major, resource-heavy. |
| `heliocentrism` | A7 | M4 | civilization | breakthrough | 779 | Conceptual revolution in cosmology underpinning modern science; reshapes worldview. |
| `telescope` | A7 | M4 | domain | breakthrough | 779 | New instrument family opening observational astronomy and optics. |
| `microscope` | A7 | M4 | domain | breakthrough | 779 | Reveals microorganisms and cells, opening microbiology; new line of inquiry. |
| `mechanics_newtonian` | A7 | M5 | civilization | breakthrough | 1298 | Epochal unified laws of motion and gravitation define the Scientific Revolution and later engineering. |
| `calculus` | A7 | M4 | civilization | breakthrough | 779 | New mathematics of rates and accumulation, foundational across science and engineering. |
| `barometer_vacuum` | A7 | M3 | domain | advance | 454 | Demonstrates air pressure and vacuum experimentally, enabling pneumatics and steam; substantial scientific advance. |
| `steam_atmospheric` | A7 | M5 | civilization | breakthrough | 1298 | First practical heat engine converting fuel to work; opens the steam power epoch. |
| `coke` | A7 | M4 | civilization | breakthrough | 779 | Frees ironmaking from charcoal and forests, enabling industrial-scale iron. |
| `brass_calamine` | A7 | M2 | narrow | advance | 260 | Cementation process yields brass for instruments and fittings; bounded metallurgical method. |
| `inoculation` | A7 | M3 | domain | advance | 454 | First deliberate immunisation practice substantially reducing smallpox mortality. |
| `anatomy_dissection` | A7 | M3 | domain | advance | 454 | Systematic dissection-based anatomy corrects Galen and founds modern medicine's empirical basis. |
| `crop_rotation_norfolk` | A7 | M4 | civilization | breakthrough | 779 | Eliminates fallow and integrates livestock, driving the Agricultural Revolution's yield gains. |
| `seed_drill_tull` | A7 | M2 | narrow | advance | 260 | Row sowing machine improving seed efficiency; a bounded tool, long known elsewhere. |
| `selective_breeding` | A7 | M3 | domain | advance | 454 | Controlled breeding doubled livestock productivity; substantial domain-wide agricultural advance. |
| `new_world_crops` | A7 | M4 | civilization | breakthrough | 779 | Intercontinental crop transfer reshaped diets and population capacity worldwide. |
| `glass_lead` | A7 | M2 | narrow | advance | 260 | New clear refractive glass formulation useful for optics and tableware; bounded materials advance. |
| `canal_navigation` | A7 | M3 | domain | advance | 454 | Large lock-equipped freight canals cut bulk transport costs; major civil-engineering capability. |
| `steam_separate_condenser` | A8 | M4 | domain | breakthrough | 202 | Watt's condenser quadrupled engine efficiency, making steam economical beyond mine pumping; conceptually novel thermal insight that reshaped the energy domain. |
| `cylinder_boring` | A8 | M2 | narrow | advance | 67 | Adaptation of cannon-boring to engine cylinders; important enabling precision but a bounded new method within machining. |
| `steam_rotary` | A8 | M4 | civilization | breakthrough | 202 | Rotary double-acting engine turned steam into a general factory prime mover, transforming industry broadly. |
| `steam_high_pressure` | A8 | M3 | domain | advance | 118 | Compact high-pressure engines enabled portability and locomotion; substantial engineering and metallurgical difficulty within power. |
| `puddling` | A8 | M4 | domain | breakthrough | 202 | Coal-fired puddling and rolling made malleable iron at scale, changing ferrous metallurgy and supplying industrialization. |
| `hot_blast` | A8 | M2 | narrow | incremental | 67 | Preheating blast air greatly cut fuel use but refines existing blast furnace practice; bounded improvement. |
| `bessemer` | A8 | M4 | civilization | breakthrough | 202 | Mass steel in tons per minutes defined the steel age, transforming construction, rail, and machinery civilization-wide. *(review: M5→M4 — A8 has 5 M5s (about 10%). Bessemer refines puddling (M4) and is on a par with it, not Age-defining the way railways or electricity are.)* |
| `open_hearth` | A8 | M3 | domain | advance | 118 | Regenerative furnace gave controllable-quality steel and scrap recycling; major domain capability building on existing steelmaking. |
| `basic_process` | A8 | M3 | domain | advance | 118 | Basic lining unlocked vast high-phosphorus ore fields for steel; significant chemistry advance with large economic reach. |
| `slide_rest_lathe` | A8 | M3 | domain | advance | 118 | Mechanical tool holding and lead-screw threading made precision repeatable, founding modern machine tools. |
| `interchangeable_parts` | A8 | M4 | civilization | breakthrough | 202 | Gauged, jig-made components changed manufacturing organization itself, enabling mass production across industries. |
| `milling_machine` | A8 | M2 | narrow | advance | 67 | New machine tool for complex shapes without filing; clear bounded capability within machining. |
| `flying_shuttle` | A8 | M2 | narrow | advance | 67 | Simple mechanism doubling weaving speed; bounded change but triggered the spinning bottleneck. |
| `spinning_jenny` | A8 | M2 | narrow | advance | 67 | Multi-spindle hand machine multiplied spinner output; new tool but limited, hand-powered capability. |
| `water_frame` | A8 | M4 | domain | breakthrough | 202 | Powered roller spinning created the factory system, changing how textile production was organized. |
| `spinning_mule` | A8 | M3 | domain | advance | 118 | Hybrid of jenny and frame yielding fine strong thread at massive spindle counts; major domain capability. |
| `power_loom` | A8 | M3 | domain | advance | 118 | Mechanized weaving completed textile industrialization; substantial technical difficulty taking decades to become practical. |
| `cotton_gin_saw` | A8 | M2 | narrow | advance | 67 | Simple mechanism with huge throughput gain for upland cotton; bounded technical step with large economic impact. |
| `jacquard` | A8 | M3 | domain | breakthrough | 118 | Punched-card control introduced programmable machinery; conceptually novel though applied in a narrow textile domain. |
| `locomotive` | A8 | M4 | civilization | breakthrough | 202 | Steam on rails created a new mode of land transport, opening the railway family with hard power and metallurgy prerequisites. |
| `railway` | A8 | M5 | civilization | breakthrough | 337 | National rail networks transformed trade, settlement, time and state power; defining infrastructure of the industrial age. |
| `steamboat` | A8 | M3 | domain | advance | 118 | Engine-driven vessels freed river and sea transport from wind and current; major new capability in transport. |
| `screw_propeller` | A8 | M2 | narrow | advance | 67 | More efficient, protected propulsion replacing paddles; bounded improvement within steam navigation. |
| `iron_hull` | A8 | M3 | domain | advance | 118 | Wrought-iron construction enabled larger, stronger ships, changing shipbuilding materials broadly. |
| `macadam` | A8 | M1 | domain | incremental | 34 | Graded compacted stone refines existing road-building practice; cheap but conceptually small step. |
| `bicycle` | A8 | M2 | narrow | advance | 67 | Combination of chain drive, steel tube and pneumatic tyre gave personal mobility; bounded capability built on existing components. |
| `chemistry_quantitative` | A8 | M4 | domain | breakthrough | 202 | Conservation of mass and element concept reframe all chemistry as a quantitative science; conceptually novel, founding modern chemistry. |
| `sulphuric_acid` | A8 | M3 | domain | advance | 118 | Lead-chamber bulk acid production is the base of industrial chemistry; substantial, broad within chemical industry. |
| `soda_leblanc` | A8 | M3 | domain | advance | 118 | Synthetic alkali frees glass, soap and textiles from plant ash; major industrial-chemistry capability. |
| `bleach_chlorine` | A8 | M2 | narrow | advance | 67 | New chemical method speeding textile bleaching; clear but bounded application. |
| `synthetic_dye` | A8 | M3 | domain | advance | 118 | Organic synthesis from coal tar founds the modern chemical industry; real difficulty and broad industrial consequences. |
| `portland_cement` | A8 | M3 | domain | advance | 118 | Reliable hydraulic cement anywhere transforms construction materials; substantial materials sophistication. |
| `reinforced_concrete` | A8 | M3 | domain | advance | 118 | Composite tension/compression material reshapes structural engineering; major within construction. |
| `haber_bosch` | A8 | M5 | civilization | breakthrough | 337 | Nitrogen fixation from air feeds half of humanity; civilization-transforming, extreme high-pressure catalytic difficulty. |
| `dynamite` | A8 | M2 | domain | advance | 67 | Stabilised high explosive enables mining and civil works; bounded new capability building on nitroglycerine. |
| `electromagnetism` | A8 | M5 | civilization | breakthrough | 337 | Unification of electricity, magnetism and light underlies the electrical age; epochal conceptual novelty. |
| `telegraph` | A8 | M4 | civilization | breakthrough | 202 | Information decoupled from transport for the first time; changes communication across the whole society. |
| `submarine_cable` | A8 | M2 | domain | incremental | 67 | Extends telegraph across oceans; significant engineering but an application of existing telegraph knowledge. |
| `dynamo` | A8 | M4 | domain | breakthrough | 202 | Self-excited generator makes electricity a bulk mechanical product; opens practical electrical power. |
| `electric_light` | A8 | M3 | domain | advance | 118 | Practical incandescent lighting is a major application driving grid demand; vacuum and filament difficulty. |
| `electricity_generation` | A8 | M4 | civilization | breakthrough | 202 | Central stations and AC grid deliver power at a distance; defines the electrical age civilization-wide. *(review: M5→M4 — Rating it M5 counts the same breakthrough as electromagnetism (M5) twice. As an application it belongs with dynamo (M4).)* |
| `electric_motor` | A8 | M3 | domain | advance | 118 | Distributed motive power reorganises factories; major industrial capability building on dynamo principles. |
| `telephone` | A8 | M3 | domain | advance | 118 | Voice transmission by wire is a substantial new communication capability extending telegraph networks. |
| `internal_combustion` | A8 | M4 | civilization | breakthrough | 202 | Compact portable power opens a new engine family enabling vehicles and aircraft; conceptually and technically hard. |
| `petroleum_refining` | A8 | M3 | domain | advance | 118 | Fractional distillation of crude creates kerosene and fuels; substantial industrial capability. |
| `vaccination` | A8 | M3 | domain | advance | 118 | Safe smallpox immunisation is a major public-health advance, empirical rather than theory-based. |
| `anaesthesia` | A8 | M3 | domain | advance | 118 | Painless surgery enables long operations; broad within medicine, built on known chemicals. |
| `germ_theory` | A8 | M4 | domain | breakthrough | 202 | Specific microbes cause specific diseases; reframes all of medicine and hygiene. |
| `antisepsis` | A8 | M3 | domain | advance | 118 | Applying germ theory collapses surgical mortality; substantial but applied advance. |
| `photography` | A8 | M3 | domain | advance | 118 | Mechanical image record via silver chemistry and optics; substantial new capability across science and media. |
| `evolution` | A8 | M4 | domain | breakthrough | 202 | Natural selection transforms biology's explanatory framework; conceptually novel though with limited direct technical capability. |
| `aircraft` | A9 | M5 | civilization | breakthrough | 390 | Powered controlled flight opens an entirely new transport and military dimension; defines the Age alongside electricity-era technologies. |
| `aircraft_metal` | A9 | M2 | domain | advance | 78 | Stressed-skin metal construction is a genuine new generation within aviation, but bounded to airframe design and commercial scale. |
| `jet_engine` | A9 | M4 | domain | breakthrough | 234 | Gas-turbine propulsion is conceptually new, transforms aviation speed and altitude, and needs hard high-temperature materials. |
| `steam_turbine` | A9 | M3 | domain | advance | 137 | Rotary high-speed steam power broadly reshapes power generation and naval propulsion, with real precision-engineering difficulty. |
| `automobile_mass` | A9 | M4 | civilization | advance | 234 | Moving assembly line plus cheap car transforms manufacturing method and personal mobility society-wide. |
| `tank` | A9 | M2 | narrow | advance | 78 | Combines existing engine, track and armour into a new bounded military capability. |
| `diesel` | A9 | M3 | domain | advance | 137 | Compression ignition gives efficient heavy power across ships, rail and trucks; significant but within the engine family. |
| `aluminium` | A9 | M3 | domain | advance | 137 | Electrolytic reduction turns a rare metal into a bulk commodity, enabling aviation; broad materials impact. |
| `superalloy` | A9 | M2 | narrow | advance | 78 | Specialised alloy class enabling hot turbine sections; important but narrow application. |
| `rocket` | A9 | M4 | domain | breakthrough | 234 | Air-independent liquid propulsion opens ballistic missiles and spaceflight, a wholly new line with hard engineering. |
| `satellite` | A9 | M4 | civilization | breakthrough | 234 | Reaching orbit creates space-based reconnaissance, communication and navigation, changing several domains. |
| `container_shipping` | A9 | M3 | civilization | advance | 137 | Low conceptual novelty but massive logistics cost reduction drives globalised trade. |
| `radio` | A9 | M4 | civilization | breakthrough | 234 | Wireless electromagnetic communication and broadcasting changes messaging and mass media fundamentally. |
| `vacuum_tube` | A9 | M4 | domain | breakthrough | 234 | Electronic amplification and switching founds electronics as a new family. |
| `radar` | A9 | M3 | domain | advance | 137 | Radio detection beyond sight transforms air and naval warfare; builds on radio and tubes. |
| `television` | A9 | M2 | domain | advance | 78 | Broadcast moving images extend radio-era media; notable but builds directly on existing electronics. |
| `computer` | A9 | M5 | civilization | breakthrough | 390 | General-purpose stored-program computation is epochal knowledge defining the modern Age. |
| `transistor` | A9 | M4 | domain | breakthrough | 234 | Solid-state switching replaces tubes and underlies all later electronics; hard materials physics. |
| `integrated_circuit` | A9 | M4 | domain | breakthrough | 234 | Monolithic integration changes how electronics is made and scaled, enabling Moore's-law growth. |
| `microprocessor` | A9 | M3 | domain | advance | 137 | CPU-on-a-chip is an application of IC technology with broad computing impact, not a new principle. |
| `internet` | A9 | M5 | civilization | breakthrough | 390 | Global packet network of networks transforms communication, commerce and knowledge society-wide. |
| `quantum_mechanics` | A9 | M5 | civilization | breakthrough | 390 | Foundational physical theory underpinning electronics, chemistry and nuclear technology of the Age. |
| `nuclear_fission` | A9 | M5 | civilization | breakthrough | 390 | Controlled release of nuclear energy reshapes energy and geopolitics; enormous difficulty and institutional scale. |
| `nuclear_power` | A9 | M3 | domain | advance | 137 | Applies fission to grid generation; major energy capability but derivative of the fission breakthrough. |
| `thermonuclear` | A9 | M3 | narrow | advance | 137 | Fusion staging multiplies yield; extreme difficulty but narrow military application of nuclear line. |
| `antibiotic_penicillin` | A9 | M4 | civilization | breakthrough | 234 | First curative antibacterial drug opens the antibiotic family and transforms mortality. |
| `antibiotic_broad` | A9 | M3 | domain | advance | 137 | Systematic screening extends antibiotics to TB and many infections; broad medical impact within an existing line. |
| `antibiotic_resistance` | A9 | M2 | domain | advance | 78 | Stewardship practice to preserve existing drugs; bounded management capability rather than new knowledge. |
| `vaccine_lab` | A9 | M3 | domain | advance | 137 | Designing vaccines from cultured pathogens broadens immunisation substantially; builds on germ theory. |
| `blood_transfusion` | A9 | M2 | domain | advance | 78 | Typing and storage make transfusion safe; clear but bounded medical capability. |
| `refrigeration` | A9 | M3 | civilization | advance | 137 | Mechanical cold chain reshapes food distribution and diet broadly; moderate conceptual novelty. |
| `canning` | A9 | M2 | domain | advance | 78 | Sealed heat preservation gives long-term food storage; simple method with bounded impact. |
| `green_revolution` | A9 | M4 | civilization | advance | 234 | Fertiliser-responsive dwarf cereals triple yields and feed billions; transformative agricultural change. |
| `genetics_mendel` | A9 | M4 | domain | breakthrough | 234 | Laws of inheritance found a new science, reshaping biology and breeding. |
| `dna_structure` | A9 | M4 | domain | breakthrough | 234 | Molecular basis of heredity opens molecular biology, sequencing and PCR. |
| `genetic_engineering` | A9 | M4 | domain | breakthrough | 234 | Moving genes between organisms creates biotech, GM crops and engineered drugs. |
| `hormonal_contraception` | A9 | M3 | civilization | advance | 137 | Bounded pharmacological advance with large demographic and social consequences. |
| `xray` | A9 | M3 | domain | advance | 137 | Non-invasive internal imaging transforms diagnosis; novel discovery but medically bounded. |
| `plastics` | A9 | M4 | civilization | breakthrough | 234 | Designed synthetic polymers create a new materials family pervasive across industry and daily life. |
| `electric_traction` | A9 | M2 | domain | advance | 78 | Applies electric motors to rail for urban transit; notable but bounded application. |
| `skyscraper` | A9 | M2 | domain | advance | 78 | Combines steel frame and elevator to build vertically; notable urban construction advance. |
| `hydroelectric` | A9 | M2 | domain | incremental | 78 | Combines dams with turbines and generators for grid power; large-scale but conceptually incremental. |
| `mechanised_agriculture` | A9 | M4 | civilization | breakthrough | 234 | Motorised farming collapses agricultural labour needs, freeing population for industry; transforms a whole domain. |
| `numerical_control` | A9 | M3 | domain | advance | 137 | Programmed machine tools and robots substantially change manufacturing, but within industry. |
| `mobile_phone` | A9 | M3 | domain | advance | 137 | Cellular telephony brings personal mobile communication; major but within communication domain. |
| `gps` | A9 | M3 | domain | advance | 137 | Global precise positioning, demanding satellites and atomic clocks; broad in navigation. |
| `atomic_clock` | A9 | M2 | narrow | advance | 78 | New precise time standard; enabling but narrow in direct capability. |
| `solar_pv` | A9 | M3 | domain | advance | 137 | Direct conversion of light to electricity opens a new energy source, broad within energy. |
| `lithium_battery` | A9 | M3 | domain | advance | 137 | High-density storage enabling portable electronics and EVs; materially sophisticated. |
| `relativity` | A9 | M4 | domain | breakthrough | 234 | Conceptually revolutionary reframing of space, time, gravity and mass-energy across physics. |
| `laser` | A9 | M4 | domain | breakthrough | 234 | Coherent light is a novel principle spawning a whole family of applications across fields. |
| `fibre_optics` | A9 | M3 | domain | advance | 137 | Low-loss fibre becomes backbone of high-bandwidth communication; major within domain. |
| `cmos_vlsi` | A9 | M3 | civilization | breakthrough | 137 | Massive integration underpins all modern computing; extreme technical and institutional sophistication. *(review: M4→M3 — It refines integrated_circuit (M4). M3 matches microprocessor (M3).)* |
| `superconductivity_applied` | A9 | M2 | narrow | advance | 78 | High-field magnets for MRI and accelerators; bounded application of known phenomenon. |
| `composites` | A9 | M2 | domain | advance | 78 | New class of lightweight materials with clear but bounded engineering uses. |
| `gas_turbine_power` | A9 | M2 | narrow | incremental | 78 | Efficiency improvement combining existing turbine and steam cycles. |
| `wind_turbine_modern` | A9 | M2 | narrow | incremental | 78 | Refined modern version of wind power using composites; bounded new capability. |
| `nuclear_fusion_research` | A9 | M2 | narrow | advance | 78 | Hard research programme without delivered capability; limited practical impact so far. |
| `grid_storage` | A9 | M2 | domain | advance | 78 | Scaled application of batteries to grid balancing; bounded, enables renewables. |
| `smart_grid` | A9 | M1 | narrow | incremental | 39 | Applies networked control to existing grids; refinement of held knowledge. |
| `programming_languages` | A9 | M3 | domain | advance | 137 | Compilers make software development broadly feasible; major within computing. |
| `operating_system` | A9 | M3 | domain | advance | 137 | Foundational software layer for managing hardware and multitasking; broad within computing. |
| `relational_database` | A9 | M2 | domain | advance | 78 | New data model for structured query; clear bounded capability. |
| `personal_computer` | A9 | M4 | civilization | breakthrough | 234 | Puts general computation in individual hands, transforming work and society broadly. |
| `public_key_crypto` | A9 | M3 | domain | breakthrough | 137 | Conceptually novel asymmetric encryption enabling secure networked commerce; narrow footprint but deep. |
| `gpu_parallel` | A9 | M2 | domain | advance | 78 | Massively parallel hardware; architectural variant of existing chips, enabling deep learning. |
| `machine_learning` | A9 | M3 | domain | advance | 137 | Learning from data is a new paradigm for computation, broad within computing. |
| `deep_learning` | A9 | M4 | domain | breakthrough | 234 | Scaled neural networks reach human-level perception, changing how AI works. |
| `large_language_models` | A9 | M5 | civilization | breakthrough | 390 | General language capability at scale is Age-defining, potentially transforming all knowledge work. |
| `cloud_computing` | A9 | M2 | domain | incremental | 78 | Institutional/business reorganisation of existing computing as networked service. |
| `smartphone` | A9 | M4 | civilization | advance | 234 | Ubiquitous pocket networked computer reshapes daily life civilization-wide; integrates existing technologies. |
| `cyber_warfare` | A9 | M2 | domain | advance | 78 | New conflict domain applying networking knowledge; bounded capability. |
| `quantum_computing` | A9 | M2 | narrow | advance | 78 | Early noisy machines; novel principle but limited delivered capability. |
| `cellular_digital` | A9 | M2 | domain | incremental | 78 | Digital generations of existing cellular networks adding data; next-generation refinement. |
| `crewed_spaceflight` | A9 | M4 | domain | breakthrough | 234 | Humans reaching orbit opens an entirely new realm; enormous technical and institutional difficulty. |
| `lunar_flight` | A9 | M3 | narrow | advance | 137 | Extreme technical feat extending crewed spaceflight, but narrow lasting capability. |
| `space_station` | A9 | M2 | narrow | incremental | 78 | Long-duration orbital habitat extends crewed spaceflight; bounded capability. |
| `space_probe` | A9 | M2 | narrow | advance | 78 | Uncrewed planetary exploration; scientific capability within bounded domain. |
| `reusable_launch` | A9 | M3 | domain | advance | 137 | Collapsing launch costs transforms access to orbit; hard engineering. |
| `space_telescope` | A9 | M2 | narrow | incremental | 78 | Places existing telescopes above atmosphere; narrow scientific gain. |
| `earth_observation` | A9 | M2 | domain | advance | 78 | Systematic orbital imaging for mapping, weather and intelligence; bounded but useful. |
| `organ_transplant` | A9 | M3 | domain | advance | 137 | Requires surgery and immunosuppression; substantial new medical capability. |
| `chemotherapy` | A9 | M2 | narrow | advance | 78 | Drug treatment of cancer; clear but bounded medical capability. |
| `medical_imaging` | A9 | M3 | domain | advance | 137 | CT and MRI transform diagnosis, combining computing and physics; broad in medicine. |
| `antiviral_drugs` | A9 | M3 | domain | advance | 137 | Substantial new therapeutic class against viruses, broad within medicine but builds on existing pharmacology. |
| `monoclonal_antibodies` | A9 | M3 | domain | advance | 137 | Hybridoma method gives broad diagnostic and therapeutic capability; sophisticated but domain-bounded. |
| `pcr` | A9 | M3 | domain | advance | 137 | Enabling laboratory method transforming molecular biology practice; domain-wide but not civilization-defining. |
| `genome_sequencing` | A9 | M4 | domain | breakthrough | 234 | Reading whole genomes changes how biology and medicine work; hard prerequisites. |
| `ivf` | A9 | M2 | narrow | advance | 78 | New clinical procedure with clear but bounded reproductive capability. |
| `crispr` | A9 | M4 | domain | breakthrough | 234 | Programmable gene editing is conceptually novel and opens a new family of biotechnology. |
| `mrna_vaccines` | A9 | M3 | domain | advance | 137 | New rapid-design vaccine platform; major within medicine, built on established molecular biology. |
| `oral_rehydration` | A9 | M2 | domain | incremental | 78 | Simple, cheap treatment with huge mortality impact but low technical difficulty or novelty for its age. |
| `drip_irrigation` | A9 | M2 | domain | advance | 78 | New water-efficient irrigation method with bounded agricultural capability. |
| `precision_agriculture` | A9 | M2 | domain | incremental | 78 | Applies satellite positioning and sensors to farm inputs; bounded efficiency gain. |
| `desalination` | A9 | M2 | narrow | advance | 78 | Bounded water-supply capability via known membrane/distillation physics; regionally important. |
| `catalytic_converter` | A9 | M1 | narrow | incremental | 39 | Narrow application of known catalysis to vehicle exhaust. |
| `weather_prediction` | A9 | M3 | domain | advance | 137 | Computational forecasting of atmosphere is substantial new capability requiring computing and physics. |
| `carbon_capture` | A9 | M2 | domain | advance | 78 | Bounded industrial separation and storage capability using known chemistry. |
| `high_speed_rail` | A9 | M2 | domain | incremental | 78 | Refines electric rail into dedicated fast lines; notable but bounded transport gain. |
| `jet_airliner` | A9 | M3 | civilization | breakthrough | 137 | Mass pressurised jet travel reshaped global mobility and trade; hard prerequisites. *(review: M4→M3 — It applies jet_engine (M4), so it should not equal the breakthrough. A9-3 is rating applications more generously than A9-1.)* |
| `electric_vehicle` | A9 | M2 | domain | incremental | 78 | Mass battery-electric vehicles apply existing battery and motor tech; notable but bounded. |
| `helicopter` | A9 | M2 | domain | advance | 78 | New rotary-wing flight mode with clear bounded capability. |
| `icbm` | A9 | M4 | civilization | breakthrough | 234 | Intercontinental nuclear delivery transformed strategy globally; hard rocketry and guidance prerequisites. |
| `nuclear_submarine` | A9 | M3 | domain | advance | 137 | Reactor propulsion yields indefinite submergence; major naval capability with high sophistication. |
| `guided_munitions` | A9 | M3 | domain | advance | 137 | Precision guidance broadly changes weapon effectiveness; substantial within military domain. |
| `stealth` | A9 | M2 | narrow | advance | 78 | Signature reduction through shaping and materials; bounded military capability. |
| `combat_drone` | A9 | M2 | domain | advance | 78 | Uncrewed aircraft add a bounded new military capability from existing avionics and links. |
| `missile_defence` | A9 | M3 | domain | advance | 137 | Intercepting missiles is technically very difficult; significant but domain-bounded. |
| `additive_manufacturing` | A9 | M3 | domain | advance | 137 | New digital fabrication paradigm, broad within manufacturing but not civilization-defining. |
| `nanotechnology` | A9 | M4 | domain | breakthrough | 234 | Molecular-scale engineering opens a new line across materials and devices. |
| `fusion_power` | A9 | M5 | civilization | breakthrough | 390 | Net-energy commercial fusion would transform civilization's energy base, defining the age. |
| `fault_tolerant_quantum` | A9 | M4 | domain | breakthrough | 234 | Error-corrected quantum computing changes how computation works; extremely hard prerequisites. |
| `general_ai` | A9 | M5 | civilization | breakthrough | 390 | Human-breadth machine intelligence is civilization-transforming and age-defining. |
| `space_solar` | A9 | M3 | domain | advance | 137 | Orbital power beaming is technically hard but a bounded energy source extension. |
| `asteroid_mining` | A9 | M3 | domain | breakthrough | 137 | Opens off-world resource extraction, a new line requiring sophisticated space capability. *(review: M4→M3 — This speculative application is rated above reusable_launch (M3) and space_probe (M2), which it depends on. That is a different standard from A9-2.)* |
| `fusion_propulsion` | A9 | M3 | domain | breakthrough | 137 | Fusion-driven spaceflight opens fast interplanetary and interstellar travel; hard prerequisites. *(review: M4→M3 — It is derived from fusion_power. M4 is generous next to how A9-1 and A9-2 rate applications.)* |
| `frontier_launch` | A9 | M1 | narrow | incremental | 39 | Repeatable level-wise cost reduction; each level is an incremental refinement. |
| `frontier_propulsion` | A9 | M1 | narrow | incremental | 39 | Repeatable incremental velocity improvement per level. |
| `frontier_materials` | A9 | M1 | domain | incremental | 39 | Repeatable incremental reduction of material inputs per level. |
| `frontier_energy` | A9 | M1 | domain | incremental | 39 | Repeatable incremental conversion efficiency gain per level. |
| `frontier_crops` | A9 | M1 | domain | incremental | 39 | Repeatable incremental yield gain per level. |
| `frontier_medicine` | A9 | M1 | domain | incremental | 39 | Repeatable incremental mortality reduction per level. |
| `frontier_computation` | A9 | M1 | domain | incremental | 39 | Repeatable incremental computation-efficiency gain per level. |
| `frontier_military` | A9 | M1 | domain | incremental | 39 | Repeatable relative effectiveness increment per level. |
| `frontier_cyber` | A9 | M1 | narrow | incremental | 39 | Repeatable relative cyber increment per level. |
| `frontier_climate` | A9 | M1 | domain | incremental | 39 | Repeatable incremental remediation capacity per level. |
| `law_code` | A3 | M4 | civilization | breakthrough | 801 | Written law codifies norms for a whole polity; conceptually novel governance knowledge that later legal and administrative civics build on. |
| `legal_code_roman` | A4 | M3 | civilization | advance | 455 | Systematic jurisprudence refines written law into a general legal science; broad but builds on law_code. |
| `census` | A4 | M2 | domain | advance | 260 | Counting people and property for administration; a bounded new administrative method. |
| `coined_wage` | A4 | M3 | civilization | advance | 455 | Wage labour paid in coin reorganizes work and obligation economy-wide; substantial institutional sophistication. |
| `patent` | A7 | M2 | domain | advance | 260 | Legal protection of invention; a bounded mechanism within law and commerce. |
| `joint_stock` | A6 | M3 | civilization | advance | 916 | Pooled, transferable ownership enables large ventures; major organizational advance resting on law and money. |

## Per-node audit

Columns: key · id · tree/branch · primary domain · age · depth · BaseCost · prerequisites · Eurekas (evaluable/total) · family/gen · entity unlocks · capabilities · emerged

| key | id | tree / branch | domain | age | depth | cost | prerequisites | eureka | family / gen | entities | caps | emerged |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `fire_making` | technology / main | energy | A1 | 0 | 110 |  | 0/2 | fire / None | 0 | 2 | ~400 kya habitual; definite by ~50 kya |
| 2 | `knapping_oldowan` | technology / main | materials | A1 | 0 | 66 |  | 1/1 | knapping / None | 0 | 3 | ~2.6 Mya (Gona); Lomekwi ~3.3 Mya |
| 3 | `knapping_levallois` | technology / main | materials | A1 | 0 | 66 |  | 1/1 | knapping / None | 0 | 1 | ~300 kya |
| 4 | `knapping_blade` | technology / main | materials | A1 | 1 | 38 | knapping_levallois | 1/1 | knapping / None | 0 | 4 | ~40 kya Upper Palaeolithic; earlier in Africa (~70 kya) |
| 5 | `pressure_flaking` | technology / main | materials | A1 | 0 | 22 |  | 0/2 | knapping / None | 0 | 2 | ~75 kya Blombos (heat-treated silcrete); widespread ~20 kya |
| 6 | `heat_treatment_stone` | technology / main | materials | A1 | 1 | 38 | fire_making AND knapping_levallois | 0/2 |  | 0 | 1 | ~164 kya Pinnacle Point |
| 7 | `microlith` | technology / main | materials | A1 | 2 | 38 | hafting | 0/2 |  | 0 | 3 | ~65 kya Howiesons Poort; Mesolithic Europe |
| 8 | `ground_stone_early` | technology / main | materials | A1 | 1 | 22 | knapping_oldowan | 1/2 | ground_stone / None | 1 | 2 | ~35 kya Japan, ~44 kya Australia |
| 9 | `adhesive_natural` | technology / main | materials | A1 | 0 | 22 |  | 0/1 | adhesive / None | 0 | 1 | ~200 kya (bitumen, Umm el Tlel) |
| 10 | `birch_tar` | technology / main | materials | A1 | 1 | 38 | fire_making AND adhesive_natural | 0/2 | adhesive / None | 0 | 2 | ~200 kya Campitello (Neanderthal); ~50 kya widespread |
| 11 | `hafting` | technology / main | materials | A1 | 1 | 66 | cordage AND adhesive_natural | 0/2 |  | 1 | 3 | ~500 kya Kathu Pan (contested); secure ~200 kya |
| 12 | `cordage` | technology / main | industry | A1 | 0 | 66 |  | 1/1 | fibre / None | 0 | 4 | ~50 kya Abri du Maras (direct); older inferred |
| 13 | `basketry` | technology / main | industry | A1 | 1 | 22 | cordage | 0/1 | fibre / None | 1 | 4 | ~27 kya Pavlov impressions; ~10 kya Guitarrero Cave |
| 14 | `atlatl` | technology / main | military | A1 | 2 | 22 | hafting | 1/2 | projectile / None | 0 | 1 | ~30 kya Upper Palaeolithic Europe |
| 15 | `bow_simple` | technology / main | military | A1 | 1 | 66 | cordage | 2/2 | bow / None | 1 | 2 | ~64 kya Sibudu (inferred); ~10 kya Stellmoor (preserved) |
| 16 | `sling` | technology / main | military | A1 | 1 | 11 | cordage | 1/2 | projectile / None | 1 | 2 | ~10 kya inferred; preserved ~2,500 BCE |
| 17 | `shelter_hut` | technology / main | construction | A1 | 1 | 22 | cordage | 2/2 | shelter / None | 0 | 2 | ~25 kya Mezhirich, Dolní Věstonice |
| 18 | `hide_working` | technology / main | industry | A1 | 1 | 22 | knapping_levallois | 1/2 | hide / None | 0 | 3 | ~100 kya (scrapers, ochre) |
| 19 | `sewing` | technology / main | industry | A1 | 2 | 38 | hide_working AND cordage | 0/2 | hide / None | 0 | 3 | ~50 kya Denisova; ~30 kya widespread |
| 20 | `raft` | technology / main | transport | A1 | 1 | 38 | cordage | 1/2 | watercraft / None | 0 | 2 | inferred ≥50 kya (Sahul crossing); Flores ~1 Mya (contested) |
| 21 | `dugout` | technology / main | transport | A1 | 2 | 22 | fire_making AND ground_stone_early | 1/2 | watercraft / None | 0 | 2 | ~8,000 BCE Pesse (oldest preserved); older inferred |
| 22 | `fishing_hook` | technology / main | agriculture | A1 | 1 | 22 | cordage | 0/2 | fishing / None | 1 | 1 | ~42 kya Jerimalai (Timor) |
| 23 | `grinding_stone` | technology / main | agriculture | A1 | 0 | 22 |  | 1/2 |  | 0 | 2 | ~30 kya Bilancino; routine in Natufian ~12 kya |
| 24 | `dog_domestication` | technology / main | science | A1 | 0 | 66 |  | 0/1 | domestication / None | 0 | 3 | ~15–30 kya |
| 25 | `ochre_processing` | technology / main | science | A1 | 1 | 11 | grinding_stone | 0/1 |  | 0 | 2 | ~100 kya Blombos |
| 26 | `bone_tools` | technology / main | materials | A1 | 2 | 22 | knapping_blade | 0/2 |  | 0 | 4 | ~90 kya Katanda harpoons; ~40 kya widespread |
| 27 | `lamp` | technology / main | energy | A1 | 1 | 11 | fire_making | 1/2 | lighting / None | 0 | 2 | ~17 kya Lascaux |
| 28 | `tally_notation` | technology / main | science | A1 | 3 | 38 | bone_tools | 0/2 |  | 0 | 2 | ~44 kya Lebombo; ~20 kya Ishango |
| 29 | `cereal_cultivation` | technology / main | agriculture | A2 | 1 | 94 | grinding_stone | 0/2 | cereal / None | 1 | 2 | ~11,500 BCE (PPNA) |
| 30 | `cereal_domesticated` | technology / main | agriculture | A2 | 2 | 57 | cereal_cultivation | 0/1 | cereal / None | 0 | 2 | ~9,500 BCE Levant (einkorn, emmer, barley) |
| 31 | `rice_wet` | technology / main | agriculture | A2 | 2 | 57 | cereal_cultivation | 0/2 | cereal / None | 1 | 2 | ~7,000 BCE Yangtze (Kuahuqiao); domesticated ~6,000 BCE |
| 32 | `millet` | technology / main | agriculture | A2 | 2 | 19 | cereal_cultivation | 0/2 | cereal / None | 1 | 1 | ~8,000 BCE Cishan |
| 33 | `maize` | technology / main | agriculture | A2 | 2 | 33 | cereal_cultivation | 0/1 | cereal / None | 1 | 2 | ~7,000 BCE Balsas; staple by ~2,000 BCE |
| 34 | `sorghum_pearl_millet` | technology / main | agriculture | A2 | 2 | 19 | cereal_cultivation | 0/1 | cereal / None | 1 | 1 | ~3,000 BCE Sahel (later than others) |
| 35 | `root_crop` | technology / main | agriculture | A2 | 0 | 33 |  | 0/2 | tuber / None | 1 | 2 | ~7,000 BCE Kuk Swamp (taro); ~8,000 BCE Andes (potato) |
| 36 | `legume` | technology / main | agriculture | A2 | 2 | 19 | cereal_cultivation | 0/1 |  | 0 | 2 | ~9,500 BCE Levant; beans ~7,000 BCE Andes/Mesoamerica |
| 37 | `orchard` | technology / main | agriculture | A2 | 2 | 33 | cereal_cultivation | 0/2 |  | 0 | 3 | ~4,000 BCE Levant (olive); fig possibly ~9,000 BCE |
| 38 | `sickle` | technology / main | agriculture | A2 | 3 | 19 | microlith | 0/2 |  | 0 | 1 | ~12,500 BCE Natufian |
| 39 | `digging_stick_hoe` | technology / main | agriculture | A2 | 2 | 9 | hafting AND ground_stone_early | 1/2 | tillage / None | 0 | 3 | ~8,000 BCE (stone hoes, China and Levant) |
| 40 | `nixtamalization` | technology / main | food | A2 | 3 | 19 | maize AND pottery_open_fired | 0/2 |  | 0 | 1 | ~1,500 BCE Guatemala |
| 41 | `sheep_goat` | technology / main | agriculture | A2 | 1 | 57 | dog_domestication | 0/1 | herd / None | 1 | 2 | ~10,500 BCE Zagros/Taurus |
| 42 | `cattle` | technology / main | agriculture | A2 | 2 | 33 | sheep_goat | 0/1 | herd / None | 1 | 3 | ~8,500 BCE Near East; ~7,000 BCE Indus; possibly Sahara |
| 43 | `pig` | technology / main | agriculture | A2 | 2 | 19 | sheep_goat | 0/2 | herd / None | 0 | 1 | ~8,500 BCE Anatolia; ~6,000 BCE China |
| 44 | `milking` | technology / main | agriculture | A2 | 3 | 33 | (sheep_goat OR cattle) AND pottery_open_fired | 0/2 | secondary_products / None | 0 | 2 | ~7,000 BCE Anatolia (lipid residues) |
| 45 | `wool` | technology / main | industry | A2 | 2 | 19 | sheep_goat AND cordage | 0/1 | secondary_products / None | 0 | 2 | ~4,000 BCE Near East |
| 46 | `animal_traction` | technology / main | agriculture | A2 | 3 | 57 | cattle | 0/2 | secondary_products / None | 0 | 3 | ~4,000 BCE (plough marks, Uruk pictographs) |
| 47 | `pottery_open_fired` | technology / main | materials | A2 | 2 | 57 | basketry | 1/2 | ceramics / None | 0 | 3 | ~18,000 BCE Xianrendong; ~14,000 BCE Jomon; ~9,000 BCE Near East; ~9,400 BCE Mali |
| 48 | `kiln_updraft` | technology / main | materials | A2 | 3 | 33 | pottery_open_fired AND mudbrick | 1/2 | kiln / None | 1 | 3 | ~6,000 BCE Yarim Tepe (Halaf) |
| 49 | `potters_wheel_fast` | technology / main | materials | A2 | 3 | 33 | pottery_open_fired | 0/2 | wheel / None | 0 | 2 | ~3,500 BCE Uruk |
| 50 | `mudbrick` | technology / main | construction | A2 | 0 | 33 |  | 0/2 | earth_building / None | 1 | 3 | ~9,000 BCE Jericho; ~7,500 BCE Çatalhöyük |
| 51 | `lime_plaster` | technology / main | construction | A2 | 1 | 33 | mudbrick AND fire_making | 0/2 | lime / None | 0 | 3 | ~8,000 BCE PPNB (Ain Ghazal, Yiftahel) |
| 52 | `timber_frame` | technology / main | construction | A2 | 2 | 19 | ground_stone_early AND shelter_hut | 1/2 | timber_building / None | 0 | 2 | ~5,500 BCE LBK longhouses |
| 53 | `stone_dry` | technology / main | construction | A2 | 2 | 9 | ground_stone_early | 1/1 | stone_building / None | 3 | 3 | ~10,000 BCE Göbekli Tepe; ~3,000 BCE Skara Brae |
| 54 | `well` | technology / main | infrastructure | A2 | 3 | 19 | stone_dry | 2/2 | water_supply / None | 1 | 2 | ~8,000 BCE Atlit-Yam (Israel) |
| 55 | `fermentation_grain` | technology / main | food | A2 | 3 | 19 | cereal_domesticated AND pottery_open_fired | 1/2 | fermentation / None | 0 | 3 | ~11,000 BCE Raqefet (contested); ~3,500 BCE Godin Tepe (secure) |
| 56 | `fermentation_fruit` | technology / main | food | A2 | 3 | 9 | pottery_open_fired | 0/2 | fermentation / None | 0 | 2 | ~7,000 BCE Jiahu; ~6,000 BCE Georgia (grape) |
| 57 | `oil_press` | technology / main | food | A2 | 3 | 19 | orchard AND stone_dry | 0/2 |  | 1 | 3 | ~4,500 BCE Levant (olive) |
| 58 | `salt_extraction` | technology / main | food | A2 | 3 | 19 | pottery_open_fired | 0/2 | preservation / None | 0 | 2 | ~6,000 BCE Poiana Slatinei (Romania); ~4,500 BCE Hallstatt |
| 59 | `flax` | technology / main | agriculture | A2 | 2 | 19 | cereal_cultivation | 0/2 |  | 0 | 1 | ~8,000 BCE Levant (wild flax use ~30 kya Dzudzuana) |
| 60 | `spinning_spindle` | technology / main | industry | A2 | 3 | 19 | cordage AND (flax OR wool) | 1/2 | textile / None | 0 | 1 | ~6,000 BCE (spindle whorls) |
| 61 | `loom_warp_weighted` | technology / main | industry | A2 | 4 | 33 | spinning_spindle AND timber_frame | 0/2 | textile / None | 1 | 2 | ~7,000 BCE Çatalhöyük (textiles); loom weights ~5,500 BCE |
| 62 | `dyeing` | technology / main | industry | A2 | 5 | 19 | loom_warp_weighted AND ochre_processing | 0/2 |  | 0 | 3 | ~4,000 BCE (dyed textiles); indigo ~4,000 BCE Peru |
| 63 | `copper_native` | technology / main | materials | A2 | 2 | 33 | ground_stone_early AND fire_making | 1/1 | copper / None | 0 | 2 | ~8,700 BCE Çayönü; ~4,000 BCE Great Lakes |
| 64 | `copper_smelting` | technology / main | materials | A2 | 4 | 94 | kiln_updraft AND (copper_native OR ochre_processing) | 2/2 | copper / None | 1 | 2 | ~5,000 BCE Belovode (Serbia) and Anatolia |
| 65 | `charcoal` | technology / main | energy | A2 | 3 | 33 | birch_tar AND timber_frame | 0/2 | fuel / None | 0 | 2 | ~5,500 BCE with smelting |
| 66 | `casting_open` | technology / main | materials | A2 | 5 | 33 | copper_smelting | 1/2 | casting / None | 0 | 1 | ~5,000 BCE |
| 67 | `gold_silver_native` | technology / main | materials | A2 | 3 | 9 | copper_native | 0/1 |  | 0 | 2 | ~4,500 BCE Varna |
| 68 | `irrigation_basin` | technology / main | infrastructure | A2 | 2 | 33 | cereal_cultivation | 0/2 | irrigation / None | 1 | 2 | ~5,000 BCE Egypt |
| 69 | `irrigation_canal` | technology / main | infrastructure | A2 | 2 | 57 | cereal_cultivation | 0/2 | irrigation / None | 1 | 3 | ~6,000 BCE Samarra (Choga Mami) |
| 70 | `terrace` | technology / main | agriculture | A2 | 3 | 19 | stone_dry AND cereal_cultivation | 1/2 | irrigation / None | 1 | 2 | ~4,000 BCE Yemen; Andes ~2,000 BCE; widespread |
| 71 | `sledge` | technology / main | transport | A2 | 3 | 9 | timber_frame | 1/2 | vehicle / None | 0 | 1 | ~7,000 BCE Heinola (Finland) |
| 72 | `wheel_solid` | technology / main | transport | A2 | 4 | 57 | animal_traction AND ground_stone_early | 1/3 | wheel / None | 0 | 2 | ~3,500 BCE (Bronocice, Ljubljana, Maykop, Uruk — near-simultaneous) |
| 73 | `cart` | technology / main | transport | A2 | 5 | 19 | wheel_solid | 0/2 | vehicle / None | 0 | 1 | ~3,500 BCE |
| 74 | `track_road` | technology / main | transport | A2 | 3 | 9 | timber_frame | 1/2 | road / None | 3 | 2 | ~3,800 BCE Sweet Track (Somerset) |
| 75 | `plough_ard` | technology / main | agriculture | A2 | 4 | 57 | animal_traction AND digging_stick_hoe | 1/2 | tillage / None | 0 | 2 | ~4,000 BCE (plough marks under barrows; Uruk pictograph) |
| 76 | `token_counting` | technology / main | communication | A2 | 4 | 33 | fire_making AND tally_notation | 1/1 | record / None | 0 | 1 | ~8,000 BCE |
| 77 | `stamp_seal` | technology / main | communication | A2 | 3 | 19 | ground_stone_early AND pottery_open_fired | 2/2 | record / None | 1 | 3 | ~6,000 BCE Halaf |
| 78 | `calendar_lunar` | technology / main | science | A2 | 4 | 19 | tally_notation | 0/2 | calendar / None | 0 | 2 | ~8,000 BCE Warren Field (Scotland, pit alignment) |
| 79 | `trepanation` | technology / main | medicine | A2 | 2 | 19 | knapping_blade | 0/1 |  | 0 | 1 | ~7,000 BCE Ensisheim; widespread Neolithic |
| 80 | `arsenical_bronze` | technology / main | materials | A3 | 5 | 467 | copper_smelting | 1/1 | bronze / 1 | 0 | 2 | ~4,000 BCE Caucasus, Anatolia, Iran |
| 81 | `tin_bronze` | technology / main | materials | A3 | 5 | 1335 | copper_smelting | 2/3 | bronze / 2 | 0 | 4 | ~3,300 BCE Mesopotamia/Anatolia; Vinča possibly ~4,650 BCE |
| 82 | `casting_closed` | technology / main | materials | A3 | 6 | 267 | casting_open AND tin_bronze | 2/2 | casting / None | 0 | 2 | ~3,000 BCE |
| 83 | `lost_wax` | technology / main | materials | A3 | 7 | 467 | casting_closed | 1/2 | casting / None | 0 | 3 | ~3,700 BCE Nahal Mishmar; Indus ~2,500 BCE |
| 84 | `sheet_metal` | technology / main | materials | A3 | 6 | 267 | tin_bronze | 1/2 |  | 0 | 2 | ~2,500 BCE |
| 85 | `mining_shaft` | technology / main | materials | A3 | 5 | 467 | stone_dry AND copper_smelting | 1/2 | mining / None | 1 | 2 | ~4,000 BCE flint (Spiennes); ~2,000 BCE copper (Great Orme) |
| 86 | `proto_writing` | technology / main | communication | A3 | 5 | 801 | token_counting | 1/2 | writing / 1 | 0 | 1 | ~3,300 BCE Uruk IV |
| 87 | `cuneiform` | technology / main | communication | A3 | 6 | 1335 | proto_writing | 1/2 | writing / 2 | 4 | 4 | ~2,900 BCE |
| 88 | `hieroglyphic` | technology / main | communication | A3 | 5 | 801 | token_counting | 0/1 | writing / 2 | 2 | 2 | ~3,250 BCE Abydos |
| 89 | `chinese_script` | technology / main | communication | A3 | 4 | 801 | tally_notation AND bone_tools | 1/2 | writing / 2 | 2 | 2 | ~1,250 BCE Anyang |
| 90 | `papyrus` | technology / main | communication | A3 | 2 | 467 | basketry | 0/2 |  | 1 | 3 | ~3,000 BCE Egypt |
| 91 | `numeral_sexagesimal` | technology / main | science | A3 | 7 | 801 | cuneiform | 0/2 | numeral / 1 | 0 | 4 | ~2,100 BCE Ur III |
| 92 | `arithmetic_babylonian` | technology / main | science | A3 | 8 | 467 | numeral_sexagesimal | 0/1 | numeral / 2 | 0 | 3 | ~1,800 BCE Old Babylonian |
| 93 | `surveying` | technology / main | science | A3 | 9 | 267 | arithmetic_babylonian OR hieroglyphic | 0/2 |  | 0 | 3 | ~2,500 BCE Egypt, Mesopotamia |
| 94 | `sail_square` | technology / main | transport | A3 | 5 | 801 | dugout AND loom_warp_weighted | 0/2 | sail / None | 1 | 2 | ~3,500 BCE Naqada II depictions; ~5,000 BCE Kuwait (possible) |
| 95 | `plank_boat` | technology / main | transport | A3 | 5 | 467 | dugout AND copper_smelting | 1/2 | hull / None | 1 | 2 | ~2,500 BCE Khufu ship; ~2,000 BCE Ferriby |
| 96 | `horse_domestication` | technology / main | science | A3 | 3 | 801 | cattle | 0/1 | herd / None | 1 | 3 | ~3,500 BCE Botai; DOM2 ~2,200 BCE |
| 97 | `donkey` | technology / main | science | A3 | 3 | 267 | cattle | 0/1 | herd / None | 1 | 2 | ~4,000 BCE Egypt/Sudan |
| 98 | `camel` | technology / main | science | A3 | 3 | 467 | cattle | 0/1 | herd / None | 1 | 2 | ~1,000 BCE dromedary; ~2,500 BCE Bactrian |
| 99 | `wheel_spoked` | technology / main | transport | A3 | 7 | 467 | wheel_solid AND casting_closed | 2/2 | wheel / None | 0 | 1 | ~2,000 BCE Sintashta |
| 100 | `chariot` | technology / main | military | A3 | 8 | 801 | wheel_spoked AND horse_domestication | 0/2 | vehicle / None | 1 | 2 | ~2,000 BCE Sintashta |
| 101 | `composite_bow` | technology / main | military | A3 | 2 | 467 | bow_simple AND sheep_goat AND birch_tar | 0/2 | bow / None | 1 | 2 | ~2,000 BCE steppe |
| 102 | `fired_brick` | technology / main | materials | A3 | 4 | 267 | kiln_updraft AND mudbrick | 1/2 | earth_building / None | 2 | 4 | ~2,900 BCE Indus (Kot Diji); Mesopotamia ~2,500 |
| 103 | `glass_glaze` | technology / main | materials | A3 | 4 | 267 | kiln_updraft AND salt_extraction | 0/2 | glass / None | 0 | 2 | ~4,000 BCE Egypt (faience); glaze ~3,500 |
| 104 | `glass_core` | technology / main | materials | A3 | 5 | 267 | glass_glaze | 0/2 | glass / None | 0 | 2 | ~1,600 BCE Mesopotamia, Egypt |
| 105 | `standard_weights` | technology / main | science | A3 | 10 | 467 | surveying | 1/2 |  | 0 | 3 | ~2,600 BCE Indus; ~2,500 Mesopotamia |
| 106 | `calendar_civil` | technology / main | science | A3 | 6 | 267 | calendar_lunar AND hieroglyphic | 0/1 | calendar / None | 0 | 2 | ~2,800 BCE Egypt |
| 107 | `water_clock` | technology / main | science | A3 | 7 | 267 | pottery_open_fired AND calendar_civil | 0/1 | timekeeping / None | 0 | 2 | ~1,500 BCE Egypt (Karnak) |
| 108 | `medicine_recorded` | technology / main | medicine | A3 | 7 | 267 | cuneiform OR hieroglyphic | 0/1 | medicine / 1 | 0 | 2 | ~2,100 BCE Sumerian; ~1,600 BCE Edwin Smith |
| 109 | `beekeeping` | technology / main | agriculture | A3 | 3 | 133 | pottery_open_fired | 1/2 |  | 0 | 2 | ~2,400 BCE Egypt; ~900 BCE Tel Rehov apiary |
| 110 | `iron_bloomery` | technology / main | materials | A4 | 4 | 1301 | charcoal | 0/2 | iron / 1 | 1 | 4 | ~1,800 BCE Anatolia; ~1,200 BCE widespread; ~1,000 BCE Sub-Saharan; ~1,200 BCE India |
| 111 | `bellows` | technology / main | energy | A4 | 4 | 260 | hide_working AND kiln_updraft | 1/2 |  | 0 | 2 | ~1,500 BCE (depicted in Egyptian tombs) |
| 112 | `steel_carburized` | technology / main | materials | A4 | 5 | 455 | iron_bloomery | 0/2 | steel / 1 | 0 | 2 | ~1,200 BCE Cyprus, Anatolia |
| 113 | `quenching` | technology / main | materials | A4 | 6 | 260 | steel_carburized | 0/2 | steel / 2 | 0 | 1 | ~1,100 BCE Cyprus (Idalion knife) |
| 114 | `tempering` | technology / main | materials | A4 | 7 | 260 | quenching | 0/2 | steel / 3 | 0 | 2 | ~500 BCE Noricum, La Tène |
| 115 | `wootz` | technology / main | materials | A4 | 6 | 455 | steel_carburized AND kiln_updraft | 0/2 | steel / 2 | 0 | 2 | ~500 BCE South India (Kodumanal) |
| 116 | `pattern_welding` | technology / main | materials | A4 | 8 | 260 | tempering | 0/2 |  | 0 | 1 | ~300 BCE La Tène |
| 117 | `abjad` | technology / main | communication | A4 | 7 | 1301 | hieroglyphic OR cuneiform | 0/1 | writing / 3 | 2 | 2 | ~1,800 BCE Sinai; ~1,050 BCE Phoenician |
| 118 | `alphabet_vowels` | technology / main | communication | A4 | 8 | 455 | abjad | 0/1 | writing / 4 | 2 | 2 | ~800 BCE Greece |
| 119 | `brahmi` | technology / main | communication | A4 | 8 | 455 | abjad | 1/1 | writing / 3 | 1 | 1 | ~300 BCE (Ashokan edicts); possibly ~500 BCE |
| 120 | `hacksilver` | technology / main | communication | A4 | 11 | 260 | standard_weights AND gold_silver_native | 0/2 | money / 1 | 0 | 3 | ~2,000 BCE Mesopotamia; hoards ~1,200 BCE Levant |
| 121 | `coinage_electrum` | technology / main | communication | A4 | 12 | 780 | hacksilver AND stamp_seal | 1/2 | money / 2 | 2 | 4 | ~630 BCE Lydia; ~600 BCE China; ~400 BCE India |
| 122 | `cavalry` | technology / main | military | A4 | 4 | 780 | horse_domestication | 0/2 | mounted / None | 1 | 3 | ~900 BCE steppe; Assyrian cavalry ~850 BCE |
| 123 | `saddle` | technology / main | military | A4 | 5 | 130 | cavalry AND hide_working | 0/2 | mounted / None | 0 | 2 | ~700 BCE Scythian; framed saddle ~200 BCE |
| 124 | `siege_ram` | technology / main | military | A4 | 5 | 260 | wheel_solid AND hide_working | 2/3 | siege / None | 1 | 1 | ~900 BCE Assyria |
| 125 | `siege_tower` | technology / main | military | A4 | 6 | 260 | siege_ram | 1/2 | siege / None | 0 | 1 | ~700 BCE Assyria |
| 126 | `stone_fortification` | technology / main | construction | A4 | 10 | 455 | stone_dry AND surveying AND (tin_bronze OR iron_bloomery) | 1/2 | fortification / None | 2 | 2 | ~1,300 BCE Hittite, Mycenaean; ~700 BCE widespread |
| 127 | `crossbow` | technology / main | military | A4 | 7 | 455 | bow_simple AND casting_closed | 0/2 | bow / None | 1 | 1 | ~600 BCE China; ~400 BCE Syracuse |
| 128 | `naval_ram` | technology / main | military | A4 | 7 | 455 | plank_boat AND casting_closed | 1/2 | warship / None | 1 | 2 | ~700 BCE Phoenicia/Greece; trireme ~550 BCE |
| 129 | `qanat` | technology / main | infrastructure | A4 | 10 | 780 | well AND surveying AND mining_shaft | 0/2 | water_supply / None | 1 | 2 | ~800 BCE Persia |
| 130 | `aqueduct_tunnel` | technology / main | infrastructure | A4 | 11 | 455 | qanat AND surveying | 0/2 | water_supply / None | 1 | 2 | ~700 BCE Siloam; ~550 BCE Samos |
| 131 | `aqueduct_channel` | technology / main | infrastructure | A4 | 12 | 260 | aqueduct_tunnel AND stone_dry | 1/2 | water_supply / None | 1 | 1 | ~690 BCE Assyria |
| 132 | `arch` | technology / main | construction | A4 | 10 | 780 | fired_brick AND surveying | 1/2 | arch / None | 0 | 2 | ~2,000 BCE mudbrick (Ur); stone ~400 BCE Etruria |
| 133 | `astronomy_records` | technology / main | science | A4 | 9 | 455 | arithmetic_babylonian | 0/3 | astronomy / None | 1 | 3 | ~750 BCE Babylon |
| 134 | `calendar_lunisolar` | technology / main | science | A4 | 10 | 260 | astronomy_records | 0/1 | calendar / None | 0 | 1 | ~500 BCE Babylon; Meton 432 BCE |
| 135 | `geometry_practical` | technology / main | science | A4 | 10 | 455 | surveying AND alphabet_vowels | 0/1 | geometry / None | 0 | 2 | ~600 BCE Ionia |
| 136 | `medicine_hippocratic` | technology / main | medicine | A4 | 8 | 780 | medicine_recorded | 0/3 | medicine / 2 | 3 | 3 | ~500 BCE Greece; ~600 BCE India; ~200 BCE China |
| 137 | `glass_blown_precursor` | technology / main | materials | A4 | 6 | 260 | glass_core | 0/2 | glass / None | 0 | 2 | ~700 BCE Assyria, Phoenicia |
| 138 | `rotary_quern` | technology / main | agriculture | A4 | 5 | 260 | grinding_stone AND wheel_solid | 1/2 | mill / None | 0 | 1 | ~500 BCE Spain, Celtic Europe |
| 139 | `olive_press_beam` | technology / main | industry | A4 | 4 | 260 | oil_press AND stone_dry | 0/2 |  | 0 | 1 | ~800 BCE Levant (Tel Miqne-Ekron: 115 presses) |
| 140 | `geometry_axiomatic` | technology / main | science | A5 | 11 | 1781 | geometry_practical | 0/2 | geometry / None | 1 | 3 | ~300 BCE Alexandria |
| 141 | `mechanics_archimedean` | technology / main | science | A5 | 12 | 1068 | geometry_axiomatic | 0/1 | mechanics / None | 0 | 4 | ~250 BCE Syracuse |
| 142 | `torsion_artillery` | technology / main | military | A5 | 13 | 623 | crossbow AND mechanics_archimedean | 1/2 | siege / None | 1 | 2 | ~340 BCE Macedon; formula ~250 BCE |
| 143 | `water_screw` | technology / main | infrastructure | A5 | 13 | 356 | mechanics_archimedean | 1/2 | water_lift / None | 0 | 2 | ~250 BCE Egypt/Syracuse |
| 144 | `noria` | technology / main | infrastructure | A5 | 5 | 356 | wheel_solid AND irrigation_canal | 0/2 | water_lift / None | 0 | 2 | ~300 BCE Egypt (saqiya); stream-driven ~100 CE |
| 145 | `water_mill` | technology / main | energy | A5 | 6 | 1068 | rotary_quern | 0/2 | mill / None | 2 | 2 | ~300 BCE Perachora (Greece); Roman ~100 BCE; China ~100 CE independently |
| 146 | `trip_hammer` | technology / main | energy | A5 | 7 | 356 | water_mill | 0/2 | mill / None | 0 | 2 | ~40 BCE China (Han) |
| 147 | `gearing` | technology / main | industry | A5 | 13 | 623 | mechanics_archimedean AND casting_closed | 1/2 | mechanism / None | 0 | 3 | ~150 BCE Antikythera mechanism |
| 148 | `concrete_pozzolan` | technology / main | materials | A5 | 11 | 1068 | lime_plaster AND arch | 0/2 | concrete / None | 0 | 3 | ~300 BCE Campania |
| 149 | `vault_dome` | technology / main | construction | A5 | 12 | 623 | arch AND concrete_pozzolan | 0/2 | arch / None | 3 | 3 | ~100 BCE Rome; Pantheon 126 CE |
| 150 | `road_paved` | technology / main | transport | A5 | 10 | 623 | surveying | 1/3 | road / None | 1 | 2 | 312 BCE Via Appia; Inca ~1,400 CE |
| 151 | `bridge_stone` | technology / main | construction | A5 | 12 | 356 | arch AND concrete_pozzolan | 1/2 | bridge / None | 1 | 2 | ~62 BCE Rome |
| 152 | `aqueduct_arcade` | technology / main | infrastructure | A5 | 13 | 356 | aqueduct_channel AND arch AND surveying | 1/2 | water_supply / None | 2 | 3 | ~312 BCE Aqua Appia; arcades ~144 BCE |
| 153 | `glass_blowing` | technology / main | materials | A5 | 7 | 623 | glass_blown_precursor AND iron_bloomery | 0/2 | glass / None | 0 | 2 | ~50 BCE Syro-Palestine |
| 154 | `window_glass` | technology / main | materials | A5 | 8 | 178 | glass_blowing | 0/2 | glass / None | 0 | 2 | ~100 CE Rome (Pompeii) |
| 155 | `cast_iron` | technology / main | materials | A5 | 5 | 1068 | iron_bloomery AND bellows AND kiln_updraft | 0/2 | iron / 2 | 1 | 3 | ~500 BCE China |
| 156 | `iron_mouldboard` | technology / main | agriculture | A5 | 6 | 356 | cast_iron AND plough_ard | 0/2 | tillage / None | 0 | 2 | ~100 BCE Han |
| 157 | `seed_drill` | technology / main | agriculture | A5 | 7 | 356 | iron_mouldboard | 0/2 |  | 0 | 3 | ~200 BCE Han |
| 158 | `collar_harness` | technology / main | transport | A5 | 4 | 623 | horse_domestication AND hide_working | 0/2 | harness / None | 0 | 2 | ~300 BCE China (breast-strap); collar ~500 CE |
| 159 | `paper` | technology / main | communication | A5 | 1 | 1781 | cordage | 1/3 | writing_surface / 2 | 3 | 3 | ~100 BCE China (Fangmatan); standardised 105 CE |
| 160 | `crossbow_repeating` | technology / main | military | A5 | 8 | 178 | crossbow | 1/2 |  | 0 | 1 | ~400 BCE China |
| 161 | `stirrup` | technology / main | military | A5 | 6 | 623 | saddle AND iron_bloomery | 0/2 | mounted / None | 1 | 2 | ~300 CE China; Avars to Europe ~600 |
| 162 | `silk` | technology / main | industry | A5 | 5 | 623 | loom_warp_weighted AND orchard | 0/2 |  | 0 | 2 | ~3,500 BCE China (silk); industry by Han |
| 163 | `lacquer` | technology / main | materials | A5 | 4 | 178 | fermentation_fruit | 0/2 |  | 0 | 2 | ~5,000 BCE China (earliest); industry by Han |
| 164 | `astronomy_geometric` | technology / main | science | A5 | 12 | 623 | geometry_axiomatic AND astronomy_records | 0/2 | astronomy / None | 1 | 3 | ~150 CE Ptolemy |
| 165 | `cartography` | technology / main | naval | A5 | 13 | 623 | astronomy_geometric AND road_paved | 0/2 |  | 0 | 2 | ~150 CE |
| 166 | `latitude_sailing` | technology / main | naval | A5 | 10 | 356 | astronomy_records OR calendar_lunar | 0/2 | navigation / None | 0 | 1 | ~1,000 BCE Polynesia (stellar); Greek ~300 BCE |
| 167 | `monsoon_sailing` | technology / main | naval | A5 | 10 | 356 | sail_square AND astronomy_records | 0/2 |  | 0 | 2 | ~100 BCE |
| 168 | `lateen` | technology / main | naval | A5 | 6 | 356 | sail_square | 0/2 | sail / None | 0 | 2 | ~200 CE Mediterranean; Indian Ocean possibly earlier |
| 169 | `mortise_hull` | technology / main | naval | A5 | 6 | 356 | plank_boat AND iron_bloomery | 1/2 | hull / None | 0 | 2 | ~1,300 BCE Uluburun; Roman grain ships |
| 170 | `pharmacology` | technology / main | medicine | A5 | 9 | 356 | medicine_hippocratic | 0/3 | medicine / 3 | 2 | 2 | ~60 CE Dioscorides; ~200 CE Shennong |
| 171 | `surgery_instruments` | technology / main | medicine | A5 | 9 | 178 | medicine_hippocratic AND casting_closed | 1/2 |  | 1 | 3 | ~100 CE Rome (Pompeii, Bingen) |
| 172 | `abacus` | technology / main | science | A5 | 9 | 356 | arithmetic_babylonian | 0/2 |  | 0 | 1 | ~300 BCE Greece (Salamis tablet); Roman hand abacus; Chinese suanpan ~200 CE |
| 173 | `alchemy` | technology / main | science | A5 | 8 | 623 | glass_blowing | 0/3 | chemistry / None | 0 | 3 | ~100 CE Alexandria; ~200 BCE China |
| 174 | `distillation` | technology / main | science | A5 | 9 | 623 | alchemy | 1/1 | chemistry / None | 0 | 3 | ~100 CE Alexandria (Maria the Jewess) |
| 175 | `heavy_plough` | technology / agriculture | agriculture | A6 | 7 | 1571 | iron_mouldboard AND wheel_solid | 0/2 | tillage / None | 0 | 2 | ~500 CE Slavic/Germanic Europe |
| 176 | `horse_collar` | technology / agriculture | agriculture | A6 | 5 | 916 | collar_harness | 1/2 | harness / None | 0 | 2 | ~500 CE China; Europe ~900 |
| 177 | `horseshoe` | technology / engineering | transport | A6 | 6 | 524 | iron_bloomery AND horse_collar | 0/2 |  | 0 | 1 | ~900 CE Europe |
| 178 | `rice_champa` | technology / agriculture | agriculture | A6 | 3 | 916 | rice_wet | 0/2 | cereal / None | 0 | 2 | 1012 CE Song |
| 179 | `sugar_refining` | technology / agriculture | food | A6 | 10 | 524 | fermentation_fruit AND distillation | 0/2 |  | 0 | 2 | ~350 CE India; Islamic ~700 |
| 180 | `windmill_vertical` | technology / engineering | energy | A6 | 7 | 916 | water_mill | 1/2 | windmill / 1 | 1 | 1 | ~700 CE Sistan |
| 181 | `windmill_post` | technology / engineering | energy | A6 | 7 | 916 | water_mill AND sail_square | 2/2 | windmill / 1 | 1 | 2 | ~1,180 CE England, Flanders |
| 182 | `fulling_mill` | technology / engineering | industry | A6 | 8 | 524 | trip_hammer AND loom_warp_weighted | 0/2 |  | 1 | 2 | ~1,000 CE Europe |
| 183 | `spinning_wheel` | technology / engineering | industry | A6 | 5 | 524 | spinning_spindle AND wheel_solid | 0/2 | textile / None | 0 | 1 | ~1,000 CE India or China; Europe ~1,280 |
| 184 | `horizontal_loom` | technology / engineering | industry | A6 | 14 | 524 | loom_warp_weighted AND gearing | 1/2 | textile / None | 0 | 2 | ~1,000 CE (from China/Islamic world) |
| 185 | `cotton_gin_roller` | technology / engineering | industry | A6 | 5 | 262 | wheel_solid | 0/2 |  | 0 | 1 | ~500 CE India |
| 186 | `compass_magnetic` | technology / military | naval | A6 | 5 | 1571 | iron_bloomery | 0/3 | navigation / None | 0 | 2 | ~1,040 CE China (Shen Kuo); Europe ~1,190 |
| 187 | `stern_rudder` | technology / military | naval | A6 | 7 | 916 | mortise_hull OR plank_boat | 1/2 | hull / None | 0 | 2 | ~100 CE China; Europe ~1,180 |
| 188 | `watertight_bulkhead` | technology / military | naval | A6 | 8 | 524 | stern_rudder | 1/2 | hull / None | 0 | 1 | ~500 CE China |
| 189 | `portolan` | technology / military | naval | A6 | 14 | 524 | compass_magnetic AND cartography | 0/2 | navigation / None | 0 | 1 | ~1,270 CE (Carte Pisane) |
| 190 | `astrolabe` | technology / natural_science | science | A6 | 14 | 916 | astronomy_geometric AND gearing | 0/2 | instrument / None | 1 | 3 | ~150 CE (theory); ~800 CE Islamic (instrument) |
| 191 | `cog` | technology / military | naval | A6 | 8 | 524 | stern_rudder AND plank_boat | 0/2 | hull / None | 0 | 1 | ~1,000 CE Frisia; standard by 1,200 |
| 192 | `gunpowder` | technology / natural_science | science | A6 | 9 | 2618 | alchemy | 0/2 | gunpowder / 1 | 0 | 3 | ~850 CE China; formula 1044 |
| 193 | `saltpetre_refining` | technology / natural_science | science | A6 | 10 | 524 | gunpowder | 0/2 | gunpowder / 2 | 0 | 1 | ~1,000 CE China |
| 194 | `cannon_early` | technology / military | military | A6 | 10 | 1571 | gunpowder AND cast_iron | 1/2 | cannon / 1 | 1 | 1 | ~1,280 CE China; Europe 1326 |
| 195 | `trebuchet` | technology / military | military | A6 | 14 | 916 | torsion_artillery | 1/2 | siege / None | 1 | 1 | ~1,100 CE Byzantium/Islamic world |
| 196 | `longbow` | technology / military | military | A6 | 2 | 524 | bow_simple | 0/2 | bow / None | 1 | 1 | ~1,200 CE Wales; English armies 1300s |
| 197 | `plate_armour` | technology / military | military | A6 | 8 | 916 | tempering AND sheet_metal | 0/2 |  | 0 | 1 | ~1,350 CE |
| 198 | `greek_fire` | technology / military | military | A6 | 10 | 524 | distillation | 0/2 |  | 0 | 1 | ~672 CE Constantinople |
| 199 | `woodblock_print` | technology / engineering | communication | A6 | 4 | 1571 | paper AND stamp_seal | 0/2 | printing / 1 | 1 | 3 | ~700 CE China/Korea; Diamond Sutra 868 |
| 200 | `movable_type_ceramic` | technology / engineering | communication | A6 | 5 | 916 | woodblock_print AND kiln_updraft | 1/2 | printing / 2 | 0 | 1 | 1040 CE China |
| 201 | `movable_type_metal` | technology / engineering | communication | A6 | 7 | 916 | movable_type_ceramic AND casting_closed | 1/2 | printing / 3 | 0 | 1 | 1234 CE Goryeo |
| 202 | `paper_money` | technology / engineering | communication | A6 | 13 | 1571 | woodblock_print AND coinage_electrum | 0/2 | money / 4 | 1 | 2 | ~1,020 CE Song |
| 203 | `hindu_arabic` | technology / main | science | A6 | 9 | 2618 | brahmi AND numeral_sexagesimal | 0/1 | numeral / 3 | 0 | 3 | ~500 CE India; Baghdad ~825; Europe ~1,200 (Fibonacci) |
| 204 | `algebra` | technology / main | science | A6 | 12 | 1571 | hindu_arabic AND geometry_axiomatic | 0/1 |  | 1 | 3 | ~820 CE Baghdad |
| 205 | `trigonometry` | technology / natural_science | science | A6 | 13 | 916 | astronomy_geometric AND hindu_arabic | 0/2 |  | 0 | 3 | ~500 CE India (sine); ~900 Islamic (tangent, spherical) |
| 206 | `optics_ibn_haytham` | technology / natural_science | science | A6 | 12 | 1571 | geometry_axiomatic AND glass_blowing | 0/2 | optics / None | 1 | 2 | ~1,020 CE Cairo |
| 207 | `spectacles` | technology / engineering | materials | A6 | 13 | 524 | optics_ibn_haytham AND window_glass | 0/2 | optics / None | 0 | 2 | ~1,286 CE Pisa |
| 208 | `crank_connecting_rod` | technology / engineering | industry | A6 | 14 | 916 | gearing AND water_mill | 0/2 | mechanism / None | 0 | 3 | ~300 CE Hierapolis sawmill; general ~1,400 |
| 209 | `escapement_verge` | technology / engineering | industry | A6 | 15 | 1571 | gearing AND crank_connecting_rod | 0/2 | timekeeping / None | 0 | 2 | ~1,280 CE Europe; Su Song water escapement 1092 (dead end) |
| 210 | `blast_furnace_water` | technology / engineering | materials | A6 | 15 | 1571 | iron_bloomery AND crank_connecting_rod AND charcoal | 0/2 | iron / 2 | 1 | 2 | ~1,150 CE Lapphyttan; widespread 1,400 |
| 211 | `wire_drawing` | technology / engineering | materials | A6 | 15 | 524 | iron_bloomery AND crank_connecting_rod | 0/2 |  | 0 | 3 | ~1,000 CE (water-powered ~1,350) |
| 212 | `canal_lock` | technology / engineering | infrastructure | A6 | 7 | 916 | irrigation_canal AND mortise_hull | 1/2 |  | 1 | 2 | 984 CE China; ~1,400 Europe |
| 213 | `double_entry` | technology / natural_science | science | A6 | 13 | 916 | hindu_arabic AND coinage_electrum AND paper | 0/2 | finance / 1 | 0 | 3 | ~1,300 CE Genoa, Florence |
| 214 | `bill_of_exchange` | technology / engineering | communication | A6 | 14 | 916 | double_entry | 0/2 | finance / 2 | 0 | 3 | ~1,150 CE Italy |
| 215 | `printing_press` | technology / engineering | communication | A7 | 13 | 1298 | casting_closed AND mechanics_archimedean AND paper | 0/3 | printing / 4 | 4 | 2 | ~1,450 CE Mainz |
| 216 | `type_founding` | technology / engineering | industry | A7 | 14 | 454 | printing_press | 0/2 |  | 0 | 1 | ~1,450 CE |
| 217 | `caravel` | technology / military | naval | A7 | 8 | 454 | lateen AND stern_rudder AND compass_magnetic | 1/2 | ocean_ship / 1 | 1 | 3 | ~1,430 CE Portugal |
| 218 | `carrack` | technology / military | naval | A7 | 9 | 779 | caravel AND cog | 0/2 | ocean_ship / 2 | 1 | 2 | ~1,450 CE |
| 219 | `polynesian_canoe` | technology / military | naval | A7 | 11 | 454 | dugout AND latitude_sailing | 0/2 | ocean_ship / 2 | 1 | 1 | ~1,000 CE (Hawaii, NZ reached); technology older |
| 220 | `celestial_navigation` | technology / military | naval | A7 | 15 | 454 | astrolabe AND trigonometry AND caravel | 0/2 | navigation / None | 0 | 1 | ~1,480 CE Portugal |
| 221 | `longitude_problem` | technology / military | naval | A7 | 16 | 779 | celestial_navigation AND escapement_verge | 0/1 | navigation / None | 0 | 2 | 1761 CE |
| 222 | `mercator` | technology / military | naval | A7 | 15 | 260 | cartography AND portolan AND trigonometry | 0/1 | navigation / None | 0 | 1 | 1569 CE |
| 223 | `corned_powder` | technology / natural_science | science | A7 | 11 | 260 | saltpetre_refining | 0/2 | gunpowder / 3 | 0 | 2 | ~1,420 CE |
| 224 | `cannon_cast_bronze` | technology / military | military | A7 | 12 | 454 | cannon_early AND lost_wax AND corned_powder | 0/2 | cannon / 2 | 1 | 2 | ~1,400 CE |
| 225 | `cannon_cast_iron` | technology / military | military | A7 | 16 | 260 | cannon_cast_bronze AND blast_furnace_water | 0/2 | cannon / 3 | 1 | 1 | 1543 CE |
| 226 | `matchlock` | technology / military | military | A7 | 11 | 454 | cannon_early AND crossbow | 0/2 | firearm / 1 | 1 | 1 | ~1,475 CE |
| 227 | `wheellock` | technology / military | military | A7 | 16 | 260 | matchlock AND escapement_verge | 0/2 | firearm / 2 | 0 | 2 | ~1,500 CE Germany |
| 228 | `flintlock` | technology / military | military | A7 | 17 | 454 | wheellock | 0/2 | firearm / 3 | 1 | 2 | ~1,610 CE France |
| 229 | `trace_italienne` | technology / military | military | A7 | 13 | 454 | cannon_cast_bronze AND geometry_axiomatic | 0/2 | fortification / None | 1 | 1 | ~1,500 CE Italy |
| 230 | `ship_of_line` | technology / military | military | A7 | 17 | 454 | carrack AND cannon_cast_iron | 0/2 | warship / None | 1 | 2 | ~1,650 CE |
| 231 | `heliocentrism` | technology / natural_science | science | A7 | 14 | 779 | astronomy_geometric AND trigonometry | 0/2 | astronomy / None | 0 | 1 | 1543 / 1609 CE |
| 232 | `telescope` | technology / natural_science | science | A7 | 14 | 779 | spectacles | 0/2 | optics / None | 1 | 2 | 1608 CE |
| 233 | `microscope` | technology / natural_science | science | A7 | 14 | 779 | spectacles | 0/1 | optics / None | 0 | 2 | ~1,620 CE; Leeuwenhoek 1670s |
| 234 | `mechanics_newtonian` | technology / natural_science | science | A7 | 15 | 1298 | heliocentrism AND algebra AND calculus | 0/1 | mechanics / None | 0 | 3 | 1687 CE |
| 235 | `calculus` | technology / natural_science | science | A7 | 13 | 779 | algebra AND geometry_axiomatic | 0/1 |  | 1 | 3 | ~1,670 CE |
| 236 | `barometer_vacuum` | technology / natural_science | science | A7 | 13 | 454 | glass_blowing AND mechanics_archimedean | 0/2 |  | 0 | 2 | 1643 CE |
| 237 | `steam_atmospheric` | technology / engineering | energy | A7 | 17 | 1298 | barometer_vacuum AND cast_iron AND cannon_cast_iron | 0/2 | steam / 1 | 0 | 2 | 1712 CE |
| 238 | `coke` | technology / engineering | materials | A7 | 16 | 779 | blast_furnace_water | 0/2 | iron / 3 | 0 | 2 | 1709 CE Coalbrookdale |
| 239 | `brass_calamine` | technology / engineering | materials | A7 | 16 | 260 | copper_smelting AND wire_drawing | 1/2 |  | 0 | 2 | ~1,500 BCE (Roman); industrial ~1,550 CE |
| 240 | `inoculation` | technology / medicine | medicine | A7 | 9 | 454 | medicine_hippocratic | 0/1 | immunisation / 1 | 0 | 1 | ~1,500 CE China (possibly earlier) |
| 241 | `anatomy_dissection` | technology / medicine | medicine | A7 | 14 | 454 | medicine_hippocratic AND printing_press | 0/2 | medicine / 4 | 0 | 2 | 1543 CE |
| 242 | `crop_rotation_norfolk` | technology / agriculture | agriculture | A7 | 3 | 779 | legume AND cereal_domesticated | 0/2 |  | 0 | 3 | ~1,650 CE Flanders, Norfolk |
| 243 | `seed_drill_tull` | technology / agriculture | agriculture | A7 | 8 | 260 | seed_drill OR (crop_rotation_norfolk AND horse_collar) | 0/2 |  | 0 | 2 | 1701 CE |
| 244 | `selective_breeding` | technology / agriculture | agriculture | A7 | 4 | 454 | crop_rotation_norfolk | 0/2 |  | 0 | 2 | ~1,760 CE |
| 245 | `new_world_crops` | technology / agriculture | agriculture | A7 | 10 | 779 | carrack AND (maize OR root_crop) | 0/2 |  | 0 | 2 | ~1,500-1,700 CE |
| 246 | `glass_lead` | technology / engineering | materials | A7 | 17 | 260 | glass_blowing AND coke | 0/2 | glass / None | 0 | 2 | 1674 CE |
| 247 | `canal_navigation` | technology / engineering | infrastructure | A7 | 10 | 454 | canal_lock AND surveying | 0/2 |  | 1 | 2 | 1681 CE France; 1761 England |
| 248 | `steam_separate_condenser` | technology / engineering | energy | A8 | 18 | 202 | steam_atmospheric AND barometer_vacuum | 0/2 | steam / 2 | 0 | 1 | 1769 CE |
| 249 | `cylinder_boring` | technology / engineering | industry | A8 | 17 | 67 | cannon_cast_iron AND water_mill | 0/2 | machine_tool / 1 | 0 | 2 | 1774 CE |
| 250 | `steam_rotary` | technology / engineering | energy | A8 | 19 | 202 | steam_separate_condenser AND cylinder_boring AND crank_connecting_rod | 0/2 | steam / 3 | 0 | 2 | 1781 CE |
| 251 | `steam_high_pressure` | technology / engineering | energy | A8 | 21 | 118 | steam_rotary AND puddling | 0/1 | steam / 4 | 0 | 3 | 1804 CE |
| 252 | `puddling` | technology / engineering | materials | A8 | 20 | 202 | coke AND steam_rotary | 0/2 | iron / 4 | 0 | 3 | 1784 CE |
| 253 | `hot_blast` | technology / engineering | materials | A8 | 17 | 67 | coke | 0/2 |  | 0 | 1 | 1828 CE |
| 254 | `bessemer` | technology / engineering | materials | A8 | 21 | 202 | puddling | 0/2 | steel / 4 | 1 | 3 | 1856 CE |
| 255 | `open_hearth` | technology / engineering | materials | A8 | 22 | 118 | bessemer AND hot_blast | 0/2 | steel / 5 | 1 | 2 | 1865 CE |
| 256 | `basic_process` | technology / engineering | materials | A8 | 22 | 118 | bessemer | 0/1 | steel / 5 | 0 | 2 | 1878 CE |
| 257 | `slide_rest_lathe` | technology / engineering | industry | A8 | 18 | 118 | cylinder_boring AND escapement_verge | 0/2 | machine_tool / 2 | 1 | 3 | ~1,800 CE |
| 258 | `interchangeable_parts` | technology / engineering | industry | A8 | 19 | 202 | slide_rest_lathe AND type_founding | 0/2 | machine_tool / 3 | 0 | 2 | ~1,820 CE Springfield |
| 259 | `milling_machine` | technology / engineering | industry | A8 | 19 | 67 | slide_rest_lathe | 0/2 | machine_tool / 3 | 0 | 2 | ~1,818 CE (Whitney/North) |
| 260 | `flying_shuttle` | technology / engineering | industry | A8 | 15 | 67 | horizontal_loom | 0/2 | textile / None | 0 | 2 | 1733 CE |
| 261 | `spinning_jenny` | technology / engineering | industry | A8 | 16 | 67 | spinning_wheel AND flying_shuttle | 0/2 | textile / None | 0 | 1 | 1764 CE |
| 262 | `water_frame` | technology / engineering | industry | A8 | 17 | 202 | spinning_jenny AND water_mill | 0/2 | textile / None | 1 | 2 | 1769 CE Cromford |
| 263 | `spinning_mule` | technology / engineering | industry | A8 | 18 | 118 | water_frame | 0/2 | textile / None | 0 | 1 | 1779 CE |
| 264 | `power_loom` | technology / engineering | industry | A8 | 20 | 118 | spinning_mule AND steam_rotary | 0/2 | textile / None | 1 | 1 | 1785 / 1820 CE |
| 265 | `cotton_gin_saw` | technology / engineering | industry | A8 | 20 | 67 | cotton_gin_roller AND milling_machine | 0/1 |  | 0 | 1 | 1793 CE |
| 266 | `jacquard` | technology / engineering | industry | A8 | 15 | 118 | horizontal_loom AND paper | 0/2 |  | 0 | 2 | 1804 CE Lyon |
| 267 | `locomotive` | technology / engineering | transport | A8 | 22 | 202 | steam_high_pressure | 0/2 | rail / 1 | 0 | 2 | 1804 / 1829 CE |
| 268 | `railway` | technology / engineering | transport | A8 | 23 | 337 | locomotive AND puddling AND surveying | 0/3 | rail / 2 | 1 | 3 | 1830 CE |
| 269 | `steamboat` | technology / engineering | transport | A8 | 20 | 118 | steam_rotary AND mortise_hull | 0/2 | ship_powered / 1 | 0 | 2 | 1807 CE |
| 270 | `screw_propeller` | technology / military | naval | A8 | 21 | 67 | steamboat AND water_screw | 1/2 | ship_powered / 2 | 1 | 2 | 1836 CE |
| 271 | `iron_hull` | technology / military | naval | A8 | 22 | 118 | puddling AND screw_propeller | 0/2 | hull / None | 1 | 2 | 1843 CE |
| 272 | `macadam` | technology / engineering | transport | A8 | 11 | 34 | road_paved | 1/2 | road / None | 1 | 1 | ~1,820 CE |
| 273 | `bicycle` | technology / engineering | transport | A8 | 6 | 67 | steel_carburized | 0/3 |  | 0 | 2 | 1885 CE |
| 274 | `chemistry_quantitative` | technology / natural_science | science | A8 | 14 | 202 | distillation AND barometer_vacuum | 0/3 | chemistry / None | 1 | 2 | 1789 CE |
| 275 | `sulphuric_acid` | technology / natural_science | science | A8 | 18 | 118 | distillation AND glass_lead | 0/2 | industrial_chem / 1 | 1 | 4 | 1746 CE |
| 276 | `soda_leblanc` | technology / natural_science | science | A8 | 19 | 118 | sulphuric_acid AND chemistry_quantitative | 0/2 | industrial_chem / 2 | 0 | 2 | 1791 CE |
| 277 | `bleach_chlorine` | technology / natural_science | science | A8 | 19 | 67 | sulphuric_acid | 0/2 |  | 0 | 1 | 1785 CE |
| 278 | `synthetic_dye` | technology / natural_science | science | A8 | 17 | 118 | chemistry_quantitative AND coke | 0/2 |  | 0 | 2 | 1856 CE |
| 279 | `portland_cement` | technology / engineering | materials | A8 | 15 | 118 | concrete_pozzolan AND kiln_updraft AND chemistry_quantitative | 1/2 | concrete / None | 1 | 1 | 1824 / 1845 CE |
| 280 | `reinforced_concrete` | technology / engineering | construction | A8 | 22 | 118 | portland_cement AND bessemer | 0/2 | concrete / None | 1 | 1 | 1867 CE |
| 281 | `haber_bosch` | technology / natural_science | science | A8 | 23 | 337 | chemistry_quantitative AND open_hearth AND electricity_generation | 0/2 |  | 0 | 2 | 1913 CE |
| 282 | `dynamite` | technology / natural_science | science | A8 | 19 | 67 | sulphuric_acid AND chemistry_quantitative | 0/2 |  | 0 | 2 | 1867 CE |
| 283 | `electromagnetism` | technology / natural_science | science | A8 | 14 | 337 | calculus | 0/2 | electricity / 1 | 0 | 4 | 1820-1865 CE |
| 284 | `telegraph` | technology / engineering | communication | A8 | 16 | 202 | electromagnetism AND wire_drawing | 0/2 | messaging / 3 | 1 | 2 | 1837 CE |
| 285 | `submarine_cable` | technology / engineering | communication | A8 | 22 | 67 | telegraph AND screw_propeller | 0/3 |  | 1 | 1 | 1866 CE |
| 286 | `dynamo` | technology / engineering | energy | A8 | 20 | 202 | electromagnetism AND steam_rotary | 0/2 | electricity / 2 | 0 | 1 | 1867 CE |
| 287 | `electric_light` | technology / engineering | energy | A8 | 21 | 118 | dynamo AND barometer_vacuum | 0/2 |  | 0 | 2 | 1879 CE |
| 288 | `electricity_generation` | technology / engineering | energy | A8 | 22 | 202 | dynamo AND electric_light AND electromagnetism | 0/2 | electricity / 3 | 2 | 3 | 1882 CE |
| 289 | `electric_motor` | technology / engineering | energy | A8 | 23 | 118 | electricity_generation | 0/1 |  | 0 | 2 | ~1,885 CE |
| 290 | `telephone` | technology / engineering | communication | A8 | 17 | 118 | telegraph | 0/2 | messaging / 4 | 1 | 1 | 1876 CE |
| 291 | `internal_combustion` | technology / engineering | energy | A8 | 20 | 202 | milling_machine AND steam_rotary AND chemistry_quantitative | 0/2 | ice / 1 | 0 | 3 | 1876 CE |
| 292 | `petroleum_refining` | technology / natural_science | science | A8 | 10 | 118 | distillation AND mining_shaft | 0/2 |  | 1 | 2 | 1859 CE |
| 293 | `vaccination` | technology / medicine | medicine | A8 | 10 | 118 | inoculation | 0/1 | immunisation / 2 | 0 | 2 | 1796 CE |
| 294 | `anaesthesia` | technology / medicine | medicine | A8 | 15 | 118 | chemistry_quantitative AND anatomy_dissection | 0/1 |  | 0 | 1 | 1846 CE |
| 295 | `germ_theory` | technology / medicine | medicine | A8 | 15 | 202 | microscope | 0/4 | medicine / 5 | 1 | 3 | 1860-1880 CE |
| 296 | `antisepsis` | technology / medicine | medicine | A8 | 16 | 118 | germ_theory AND anaesthesia | 0/2 |  | 0 | 2 | 1867 CE |
| 297 | `photography` | technology / natural_science | science | A8 | 18 | 118 | chemistry_quantitative AND glass_lead | 0/2 |  | 0 | 2 | 1839 CE |
| 298 | `evolution` | technology / natural_science | science | A8 | 5 | 202 | selective_breeding | 0/2 |  | 0 | 2 | 1858 CE |
| 299 | `aircraft` | technology / engineering | transport | A9 | 21 | 390 | internal_combustion | 0/2 | aviation / 1 | 3 | 3 | 1903 CE |
| 300 | `aircraft_metal` | technology / engineering | transport | A9 | 24 | 78 | aircraft AND aluminium | 0/2 | aviation / 2 | 0 | 2 | 1915 / 1935 CE |
| 301 | `jet_engine` | technology / engineering | energy | A9 | 25 | 234 | aircraft_metal AND steam_turbine AND superalloy | 0/2 | aviation / 3 | 0 | 2 | 1939 CE |
| 302 | `steam_turbine` | technology / engineering | energy | A9 | 23 | 137 | steam_high_pressure AND open_hearth | 0/2 | steam / 5 | 1 | 2 | 1884 CE |
| 303 | `automobile_mass` | technology / engineering | transport | A9 | 21 | 234 | internal_combustion AND interchangeable_parts AND petroleum_refining | 0/2 | ice / 2 | 0 | 3 | 1913 CE |
| 304 | `tank` | technology / military | military | A9 | 23 | 78 | internal_combustion AND basic_process AND cannon_cast_iron | 0/2 |  | 1 | 1 | 1916 CE |
| 305 | `diesel` | technology / engineering | energy | A9 | 23 | 137 | internal_combustion AND open_hearth | 0/1 | ice / 2 | 0 | 3 | 1897 CE |
| 306 | `aluminium` | technology / engineering | materials | A9 | 23 | 137 | electricity_generation AND chemistry_quantitative | 0/2 |  | 0 | 2 | 1886 CE |
| 307 | `superalloy` | technology / engineering | materials | A9 | 23 | 78 | open_hearth AND chemistry_quantitative | 0/2 |  | 0 | 2 | ~1,940 CE |
| 308 | `rocket` | technology / engineering | transport | A9 | 24 | 234 | internal_combustion AND aluminium AND calculus | 0/2 | rocket / 1 | 3 | 2 | 1926 / 1944 CE |
| 309 | `satellite` | technology / engineering | communication | A9 | 25 | 234 | rocket AND computer AND transistor | 0/2 | rocket / 2 | 0 | 3 | 1957 CE |
| 310 | `container_shipping` | technology / engineering | transport | A9 | 24 | 137 | iron_hull AND diesel | 0/3 |  | 0 | 1 | 1956 CE |
| 311 | `radio` | technology / engineering | communication | A9 | 17 | 234 | electromagnetism AND telegraph | 0/2 | messaging / 5 | 2 | 2 | 1895 / 1920 CE |
| 312 | `vacuum_tube` | technology / natural_science | science | A9 | 22 | 234 | electric_light AND radio | 0/2 | electronics / 1 | 0 | 3 | 1906 CE |
| 313 | `radar` | technology / military | military | A9 | 23 | 137 | vacuum_tube AND electromagnetism | 0/2 |  | 0 | 3 | 1938 CE |
| 314 | `television` | technology / engineering | communication | A9 | 23 | 78 | vacuum_tube AND photography | 0/2 | messaging / 6 | 1 | 1 | 1936 CE |
| 315 | `computer` | technology / engineering | engineering | A9 | 23 | 390 | vacuum_tube AND calculus | 0/2 | computing / 1 | 0 | 2 | 1949 CE |
| 316 | `transistor` | technology / engineering | engineering | A9 | 23 | 234 | vacuum_tube AND quantum_mechanics | 0/2 | electronics / 2 | 0 | 1 | 1947 CE |
| 317 | `integrated_circuit` | technology / engineering | engineering | A9 | 24 | 234 | transistor AND photography | 0/2 | electronics / 3 | 1 | 2 | 1959 CE |
| 318 | `microprocessor` | technology / engineering | engineering | A9 | 25 | 137 | integrated_circuit AND computer | 0/1 | computing / 2 | 0 | 2 | 1971 CE |
| 319 | `internet` | technology / engineering | communication | A9 | 25 | 390 | computer AND telephone AND integrated_circuit | 0/2 | messaging / 7 | 1 | 3 | 1969 / 1983 / 1991 CE |
| 320 | `quantum_mechanics` | technology / natural_science | science | A9 | 15 | 390 | electromagnetism | 0/2 |  | 0 | 3 | 1925 CE |
| 321 | `nuclear_fission` | technology / natural_science | science | A9 | 23 | 390 | quantum_mechanics AND electricity_generation | 0/2 | nuclear / 1 | 0 | 2 | 1942 CE |
| 322 | `nuclear_power` | technology / engineering | energy | A9 | 24 | 137 | nuclear_fission AND steam_turbine | 0/2 | nuclear / 2 | 1 | 1 | 1954 CE |
| 323 | `thermonuclear` | technology / military | military | A9 | 24 | 137 | nuclear_fission | 0/2 | nuclear / 2 | 0 | 1 | 1952 CE |
| 324 | `antibiotic_penicillin` | technology / medicine | medicine | A9 | 16 | 234 | germ_theory AND fermentation_grain | 0/2 | antibiotics / 1 | 0 | 2 | 1943 CE |
| 325 | `antibiotic_broad` | technology / medicine | medicine | A9 | 17 | 137 | antibiotic_penicillin | 0/2 | antibiotics / 2 | 0 | 2 | 1943-1950 CE |
| 326 | `antibiotic_resistance` | technology / medicine | medicine | A9 | 18 | 78 | antibiotic_broad AND evolution | 0/2 | antibiotics / 3 | 0 | 1 | ~1,960 CE |
| 327 | `vaccine_lab` | technology / medicine | medicine | A9 | 16 | 137 | germ_theory AND vaccination | 0/2 | immunisation / 3 | 0 | 1 | 1885 / 1955 CE |
| 328 | `blood_transfusion` | technology / medicine | medicine | A9 | 16 | 78 | germ_theory AND anaesthesia | 0/2 |  | 0 | 1 | 1901 / 1937 CE |
| 329 | `refrigeration` | technology / agriculture | food | A9 | 20 | 137 | chemistry_quantitative AND steam_rotary | 0/2 |  | 0 | 2 | 1876 CE (Linde) |
| 330 | `canning` | technology / agriculture | food | A9 | 16 | 78 | glass_blowing AND sheet_metal AND germ_theory | 0/2 |  | 0 | 2 | 1810 CE |
| 331 | `green_revolution` | technology / agriculture | agriculture | A9 | 24 | 234 | haber_bosch AND genetics_mendel AND irrigation_canal | 0/2 | cereal / None | 0 | 1 | 1966 CE |
| 332 | `genetics_mendel` | technology / natural_science | science | A9 | 15 | 234 | evolution AND microscope | 0/2 |  | 0 | 2 | 1900 CE |
| 333 | `dna_structure` | technology / natural_science | science | A9 | 19 | 234 | genetics_mendel AND quantum_mechanics AND photography | 0/1 |  | 0 | 3 | 1953 CE |
| 334 | `genetic_engineering` | technology / natural_science | science | A9 | 20 | 234 | dna_structure AND antibiotic_broad | 0/2 |  | 0 | 3 | 1973 CE |
| 335 | `hormonal_contraception` | technology / medicine | medicine | A9 | 16 | 137 | chemistry_quantitative AND genetics_mendel | 0/1 |  | 0 | 1 | 1960 CE |
| 336 | `xray` | technology / medicine | medicine | A9 | 23 | 137 | vacuum_tube AND photography | 0/2 |  | 0 | 2 | 1895 CE |
| 337 | `plastics` | technology / natural_science | science | A9 | 18 | 234 | petroleum_refining AND chemistry_quantitative AND synthetic_dye | 0/2 |  | 0 | 2 | 1907 CE |
| 338 | `electric_traction` | technology / engineering | transport | A9 | 24 | 78 | electric_motor AND railway | 0/2 |  | 0 | 2 | 1890 CE |
| 339 | `skyscraper` | technology / engineering | construction | A9 | 24 | 78 | bessemer AND electric_motor AND reinforced_concrete | 0/2 |  | 1 | 1 | 1885 CE |
| 340 | `hydroelectric` | technology / engineering | energy | A9 | 23 | 78 | electricity_generation AND reinforced_concrete AND water_mill | 0/2 |  | 1 | 2 | 1895 CE |
| 341 | `mechanised_agriculture` | technology / agriculture | agriculture | A9 | 21 | 234 | internal_combustion AND seed_drill_tull | 0/2 |  | 0 | 1 | ~1,920 CE |
| 342 | `numerical_control` | technology / engineering | industry | A9 | 24 | 137 | computer AND milling_machine | 0/2 |  | 0 | 1 | 1952 CE |
| 343 | `mobile_phone` | technology / engineering | communication | A9 | 26 | 137 | radio AND microprocessor AND telephone | 0/2 | messaging / 8 | 1 | 1 | 1979 CE |
| 344 | `gps` | technology / military | naval | A9 | 26 | 137 | satellite AND microprocessor AND atomic_clock | 0/2 | navigation / None | 0 | 3 | 1995 CE |
| 345 | `atomic_clock` | technology / natural_science | science | A9 | 17 | 78 | quantum_mechanics AND longitude_problem | 0/2 | timekeeping / None | 0 | 2 | 1955 CE |
| 346 | `solar_pv` | technology / engineering | energy | A9 | 24 | 137 | transistor AND quantum_mechanics | 0/1 |  | 1 | 2 | 1954 CE |
| 347 | `lithium_battery` | technology / engineering | energy | A9 | 15 | 137 | chemistry_quantitative AND electromagnetism | 0/2 |  | 0 | 3 | 1991 CE |
| 348 | `relativity` | technology / natural_science | science | A9 | 16 | 234 | electromagnetism AND mechanics_newtonian | 0/2 |  | 0 | 2 | 1905 / 1915 |
| 349 | `laser` | technology / natural_science | science | A9 | 23 | 234 | quantum_mechanics AND vacuum_tube | 0/2 |  | 1 | 3 | 1960 |
| 350 | `fibre_optics` | technology / engineering | communication | A9 | 24 | 137 | laser AND glass_lead | 0/2 | messaging / None | 0 | 1 | 1970 |
| 351 | `cmos_vlsi` | technology / engineering | engineering | A9 | 26 | 137 | microprocessor | 0/2 | electronics / 4 | 0 | 2 | 1970s–1980s |
| 352 | `superconductivity_applied` | technology / natural_science | science | A9 | 21 | 78 | quantum_mechanics AND refrigeration | 0/2 |  | 0 | 4 | 1911 discovery; magnets 1960s |
| 353 | `composites` | technology / engineering | materials | A9 | 19 | 78 | plastics | 0/2 |  | 0 | 3 | 1930s fibreglass; 1960s carbon fibre |
| 354 | `gas_turbine_power` | technology / engineering | energy | A9 | 26 | 78 | jet_engine AND electricity_generation | 0/2 |  | 0 | 1 | 1950s; combined cycle 1970s |
| 355 | `wind_turbine_modern` | technology / engineering | energy | A9 | 23 | 78 | composites AND electricity_generation | 0/2 |  | 1 | 1 | 1980s Denmark |
| 356 | `nuclear_fusion_research` | technology / natural_science | science | A9 | 25 | 78 | thermonuclear AND superconductivity_applied | 0/2 |  | 0 | 2 | 1958 tokamak |
| 357 | `grid_storage` | technology / engineering | energy | A9 | 23 | 78 | lithium_battery AND electricity_generation | 0/3 |  | 0 | 2 | 2010s |
| 358 | `smart_grid` | technology / engineering | energy | A9 | 26 | 39 | internet AND electricity_generation | 0/2 |  | 0 | 2 | 2000s |
| 359 | `programming_languages` | technology / engineering | engineering | A9 | 24 | 137 | computer | 0/2 | computing / 2 | 0 | 1 | 1957 FORTRAN |
| 360 | `operating_system` | technology / engineering | engineering | A9 | 25 | 137 | programming_languages | 0/2 | computing / 3 | 0 | 2 | 1960s–1970s |
| 361 | `relational_database` | technology / engineering | engineering | A9 | 25 | 78 | programming_languages | 0/2 |  | 0 | 2 | 1970 |
| 362 | `personal_computer` | technology / engineering | engineering | A9 | 27 | 234 | cmos_vlsi AND operating_system | 0/2 | computing / 4 | 0 | 2 | 1977 |
| 363 | `public_key_crypto` | technology / engineering | engineering | A9 | 25 | 137 | programming_languages | 0/2 |  | 0 | 3 | 1976 |
| 364 | `gpu_parallel` | technology / engineering | engineering | A9 | 27 | 78 | cmos_vlsi | 0/2 | computing / 5 | 0 | 1 | 1999 GPU; 2007 general-purpose |
| 365 | `machine_learning` | technology / engineering | engineering | A9 | 25 | 137 | programming_languages AND calculus | 0/2 | ai / 1 | 0 | 2 | 1980s backpropagation |
| 366 | `deep_learning` | technology / engineering | engineering | A9 | 28 | 234 | machine_learning AND gpu_parallel | 0/2 | ai / 2 | 0 | 2 | 2012 |
| 367 | `large_language_models` | technology / engineering | engineering | A9 | 29 | 390 | deep_learning AND internet | 0/2 | ai / 3 | 0 | 2 | 2017 transformer; 2020 scale |
| 368 | `cloud_computing` | technology / engineering | engineering | A9 | 26 | 78 | internet AND relational_database | 0/2 |  | 1 | 2 | 2006 |
| 369 | `smartphone` | technology / engineering | engineering | A9 | 28 | 234 | mobile_phone AND personal_computer AND lithium_battery | 0/3 | messaging / None | 0 | 2 | 2007 |
| 370 | `cyber_warfare` | technology / engineering | engineering | A9 | 26 | 78 | internet AND public_key_crypto | 0/3 |  | 0 | 2 | 2010 Stuxnet |
| 371 | `quantum_computing` | technology / engineering | engineering | A9 | 27 | 78 | quantum_mechanics AND cmos_vlsi AND superconductivity_applied | 0/2 |  | 0 | 1 | 2019 demonstration |
| 372 | `cellular_digital` | technology / engineering | communication | A9 | 27 | 78 | mobile_phone AND cmos_vlsi | 0/2 | messaging / None | 0 | 1 | 1991 GSM; 4G 2009 |
| 373 | `crewed_spaceflight` | technology / engineering | transport | A9 | 26 | 234 | satellite AND computer | 0/3 | space / 1 | 0 | 1 | 1961 |
| 374 | `lunar_flight` | technology / engineering | transport | A9 | 27 | 137 | crewed_spaceflight AND microprocessor | 0/3 | space / 2 | 0 | 1 | 1969 |
| 375 | `space_station` | technology / engineering | transport | A9 | 27 | 78 | crewed_spaceflight | 0/2 | space / 2 | 0 | 2 | 1971 Salyut; 1998 ISS |
| 376 | `space_probe` | technology / natural_science | science | A9 | 26 | 78 | satellite AND computer | 0/2 |  | 0 | 1 | 1962 Mariner 2 |
| 377 | `reusable_launch` | technology / engineering | transport | A9 | 27 | 137 | rocket AND gps AND composites | 0/3 | rocket / 3 | 0 | 1 | 2015 |
| 378 | `space_telescope` | technology / natural_science | science | A9 | 26 | 78 | satellite AND telescope | 0/2 |  | 0 | 2 | 1990 Hubble |
| 379 | `earth_observation` | technology / military | naval | A9 | 26 | 78 | satellite AND photography | 0/3 |  | 0 | 2 | 1972 Landsat |
| 380 | `organ_transplant` | technology / medicine | medicine | A9 | 17 | 137 | antisepsis AND blood_transfusion | 0/2 |  | 0 | 1 | 1954 kidney; 1980s immunosuppression |
| 381 | `chemotherapy` | technology / medicine | medicine | A9 | 16 | 78 | chemistry_quantitative AND germ_theory | 0/2 |  | 0 | 1 | 1940s |
| 382 | `medical_imaging` | technology / medicine | medicine | A9 | 24 | 137 | xray AND computer AND superconductivity_applied | 0/2 |  | 0 | 1 | 1971 CT; 1977 MRI |
| 383 | `antiviral_drugs` | technology / medicine | medicine | A9 | 20 | 137 | dna_structure AND chemistry_quantitative | 0/3 |  | 0 | 1 | 1974 acyclovir; 1996 HIV therapy |
| 384 | `monoclonal_antibodies` | technology / medicine | medicine | A9 | 20 | 137 | dna_structure AND vaccine_lab | 0/2 |  | 0 | 2 | 1975 |
| 385 | `pcr` | technology / natural_science | science | A9 | 20 | 137 | dna_structure | 0/2 |  | 0 | 2 | 1983 |
| 386 | `genome_sequencing` | technology / natural_science | science | A9 | 24 | 234 | pcr AND computer | 0/2 |  | 0 | 2 | 1977 Sanger; 2001 human genome |
| 387 | `ivf` | technology / medicine | medicine | A9 | 17 | 78 | hormonal_contraception | 0/2 |  | 0 | 1 | 1978 |
| 388 | `crispr` | technology / natural_science | science | A9 | 25 | 234 | genome_sequencing AND genetic_engineering | 0/2 |  | 0 | 1 | 2012 |
| 389 | `mrna_vaccines` | technology / medicine | medicine | A9 | 25 | 137 | genetic_engineering AND vaccine_lab AND genome_sequencing | 0/3 | immunisation / 4 | 0 | 1 | 2020 |
| 390 | `oral_rehydration` | technology / medicine | medicine | A9 | 16 | 78 | germ_theory AND chemistry_quantitative | 0/3 |  | 0 | 1 | 1960s |
| 391 | `drip_irrigation` | technology / agriculture | agriculture | A9 | 19 | 78 | plastics AND irrigation_canal | 0/3 | irrigation / None | 0 | 1 | 1960s |
| 392 | `precision_agriculture` | technology / agriculture | agriculture | A9 | 27 | 78 | gps AND mechanised_agriculture | 0/3 |  | 0 | 1 | 1990s |
| 393 | `desalination` | technology / engineering | infrastructure | A9 | 23 | 78 | plastics AND electricity_generation | 0/3 |  | 1 | 1 | 1950s flash; 1960s reverse osmosis |
| 394 | `catalytic_converter` | technology / natural_science | science | A9 | 22 | 39 | chemistry_quantitative AND automobile_mass | 0/3 |  | 0 | 1 | 1975 |
| 395 | `weather_prediction` | technology / engineering | engineering | A9 | 24 | 137 | computer AND radio | 0/3 |  | 0 | 1 | 1950 |
| 396 | `carbon_capture` | technology / natural_science | science | A9 | 27 | 78 | chemistry_quantitative AND gas_turbine_power | 0/3 |  | 0 | 1 | 1970s industrial; scale speculative |
| 397 | `high_speed_rail` | technology / engineering | transport | A9 | 25 | 78 | electric_traction AND reinforced_concrete | 0/3 | rail / None | 0 | 1 | 1964 Shinkansen |
| 398 | `jet_airliner` | technology / engineering | transport | A9 | 26 | 137 | jet_engine AND aircraft_metal | 0/2 | aviation / 4 | 0 | 1 | 1952 Comet; 1958 707 |
| 399 | `electric_vehicle` | technology / engineering | transport | A9 | 24 | 78 | lithium_battery AND electric_motor | 0/2 |  | 0 | 1 | 2008 |
| 400 | `helicopter` | technology / engineering | transport | A9 | 22 | 78 | aircraft | 0/2 | aviation / None | 1 | 2 | 1939–1942 |
| 401 | `icbm` | technology / military | military | A9 | 25 | 234 | rocket AND thermonuclear AND computer | 0/3 | rocket / None | 1 | 1 | 1957 |
| 402 | `nuclear_submarine` | technology / military | military | A9 | 24 | 137 | nuclear_fission AND screw_propeller | 0/3 |  | 1 | 2 | 1954 |
| 403 | `guided_munitions` | technology / military | military | A9 | 25 | 137 | laser AND rocket AND computer | 0/2 |  | 0 | 1 | 1972 laser-guided bombs |
| 404 | `stealth` | technology / military | military | A9 | 24 | 78 | composites AND radar AND computer | 0/3 |  | 1 | 1 | 1981 F-117 |
| 405 | `combat_drone` | technology / military | military | A9 | 27 | 78 | gps AND microprocessor AND aircraft | 0/3 |  | 1 | 2 | 1990s–2000s |
| 406 | `missile_defence` | technology / military | military | A9 | 26 | 137 | radar AND guided_munitions | 0/3 |  | 0 | 1 | 1980s–2000s |
| 407 | `additive_manufacturing` | technology / engineering | industry | A9 | 25 | 137 | numerical_control AND plastics AND computer | 0/2 |  | 0 | 1 | 1986 |
| 408 | `nanotechnology` | technology / engineering | materials | A9 | 27 | 234 | cmos_vlsi AND quantum_mechanics | 0/2 |  | 0 | 2 | 1980s–2000s |
| 409 | `fusion_power` | technology / engineering | energy | A9 (F) | 26 | 390 | nuclear_fusion_research | 0/0 |  | 1 | 1 | speculative |
| 410 | `fault_tolerant_quantum` | technology / engineering | engineering | A9 (F) | 28 | 234 | quantum_computing | 0/0 |  | 0 | 1 | speculative |
| 411 | `general_ai` | technology / engineering | engineering | A9 (F) | 30 | 390 | large_language_models | 0/0 |  | 0 | 1 | speculative |
| 412 | `space_solar` | technology / engineering | energy | A9 (F) | 28 | 137 | solar_pv AND reusable_launch | 0/0 |  | 1 | 1 | speculative |
| 413 | `asteroid_mining` | technology / engineering | materials | A9 (F) | 28 | 137 | reusable_launch AND space_probe | 0/0 |  | 0 | 1 | speculative |
| 414 | `fusion_propulsion` | technology / engineering | transport | A9 (F) | 27 | 137 | fusion_power AND space_probe | 0/0 |  | 1 | 1 | speculative |
| 415 | `frontier_launch` | technology / engineering | transport | A9 (F) | 28 | 39 | reusable_launch | 0/0 |  | 0 | 1 | repeatable |
| 416 | `frontier_propulsion` | technology / engineering | transport | A9 (F) | 27 | 39 | space_probe | 0/0 |  | 0 | 1 | repeatable |
| 417 | `frontier_materials` | technology / engineering | materials | A9 (F) | 28 | 39 | nanotechnology | 0/0 |  | 0 | 1 | repeatable |
| 418 | `frontier_energy` | technology / engineering | energy | A9 (F) | 27 | 39 | solar_pv OR gas_turbine_power | 0/0 |  | 0 | 1 | repeatable |
| 419 | `frontier_crops` | technology / agriculture | agriculture | A9 (F) | 26 | 39 | crispr | 0/0 |  | 0 | 1 | repeatable |
| 420 | `frontier_medicine` | technology / medicine | medicine | A9 (F) | 25 | 39 | genome_sequencing | 0/0 |  | 0 | 1 | repeatable |
| 421 | `frontier_computation` | technology / engineering | engineering | A9 (F) | 27 | 39 | cmos_vlsi | 0/0 |  | 0 | 1 | repeatable |
| 422 | `frontier_military` | technology / military | military | A9 (F) | 26 | 39 | guided_munitions | 0/0 |  | 0 | 1 | repeatable |
| 423 | `frontier_cyber` | technology / engineering | engineering | A9 (F) | 27 | 39 | cyber_warfare | 0/0 |  | 0 | 1 | repeatable |
| 424 | `frontier_climate` | technology / natural_science | environment | A9 (F) | 28 | 39 | carbon_capture | 0/0 |  | 0 | 1 | repeatable |
| 1001 | `law_code` | civics | governance | A3 (derived) | 7 | 801 | cuneiform | 0/0 | | 1 | 0 | |
| 1002 | `legal_code_roman` | civics | governance | A4 (derived) | 9 | 455 | law_code AND alphabet_vowels | 0/0 | | 3 | 0 | |
| 1003 | `census` | civics | governance | A4 (derived) | 9 | 260 | (cuneiform AND stamp_seal) AND alphabet_vowels | 0/0 | | 6 | 0 | |
| 1004 | `coined_wage` | civics | governance | A4 (derived) | 13 | 455 | coinage_electrum | 0/0 | | 1 | 0 | |
| 1005 | `patent` | civics | governance | A7 (derived) | 14 | 260 | legal_code_roman AND printing_press | 0/0 | | 1 | 0 | |
| 1006 | `joint_stock` | civics | governance | A6 (derived) | 15 | 916 | bill_of_exchange AND legal_code_roman | 0/0 | | 2 | 0 | |

## Eureka strings without a machine condition (category, status, reason)

| node | # | cat | status | corpus text | reason | future system |
|---|---|---|---|---|---|---|
| `fire_making` | 0 | C | no-state-carrier | circumstance: dry softwood for drill | Dry softwood species not a shipped good; timber too generic. | fauna/flora range |
| `fire_making` | 1 | F | requires-authoring | circumstance: tinder | Tinder is an unspecified material class. |  |
| `pressure_flaking` | 0 | F | requires-authoring | circumstance: heat-treated stone (see below) | Refers to another node's output with cross-reference; not a clean condition. |  |
| `pressure_flaking` | 1 | C | no-state-carrier | circumstance: antler or bone presser | Antler/bone not a shipped good. | non-shipped good: antler/bone |
| `heat_treatment_stone` | 0 | C | no-state-carrier | circumstance: silcrete or chert | Specific lithic deposits not modelled. | terrain/resource deposits |
| `heat_treatment_stone` | 1 | D | implied-by-prerequisites | circumstance: sustained fire | implied by the node's own prerequisites: 'fire_making' holds whenever heat_treatment_stone is available |  |
| `microlith` | 0 | F | requires-authoring | circumstance: blades | Blades as artefacts, not a stocked good; ambiguous. |  |
| `microlith` | 1 | D | implied-by-prerequisites | circumstance: adhesive | implied by the node's own prerequisites: 'adhesive_natural' holds whenever microlith is available |  |
| `ground_stone_early` | 1 | C | no-state-carrier | circumstance: abrasive sandstone | Sandstone type not distinguished from generic stone. | terrain/resource deposits |
| `adhesive_natural` | 0 | C | no-state-carrier | circumstance: tree resin, bitumen seep, or plant gum | Resin, bitumen, gum not shipped goods; disjunction. | fauna/flora range / resource deposits |
| `birch_tar` | 0 | C | no-state-carrier | circumstance: birch bark | Birch presence not modelled. | fauna/flora range |
| `birch_tar` | 1 | D | implied-by-prerequisites | circumstance: fire | implied by the node's own prerequisites: 'fire_making' holds whenever birch_tar is available |  |
| `hafting` | 0 | F | requires-authoring | circumstance: point | Point is an artefact, vague. |  |
| `hafting` | 1 | F | requires-authoring | circumstance: shaft | Shaft is an artefact, vague. |  |
| `basketry` | 0 | C | no-state-carrier | circumstance: pliable plant stems, roots, bark | Specific plant materials not a shipped good; fiber not exact. | fauna/flora range |
| `atlatl` | 1 | F | requires-authoring | circumstance: light dart shafts | Dart shafts are artefacts, not a stocked good. |  |
| `sling` | 0 | D | implied-by-prerequisites | circumstance: cordage | implied by the node's own prerequisites: 'cordage' holds whenever sling is available |  |
| `hide_working` | 1 | F | requires-authoring | circumstance: scrapers | Scrapers are tool artefacts, not exact shipped good. |  |
| `sewing` | 0 | C | no-state-carrier | circumstance: bone or ivory | Bone/ivory not shipped goods. | non-shipped good: bone/ivory |
| `sewing` | 1 | C | no-state-carrier | circumstance: sinew thread | Sinew not a shipped good. | non-shipped good: sinew |
| `raft` | 1 | D | implied-by-prerequisites | circumstance: cordage | implied by the node's own prerequisites: 'cordage' holds whenever raft is available |  |
| `dugout` | 1 | D | implied-by-prerequisites | circumstance: fire | implied by the node's own prerequisites: 'fire_making' holds whenever dugout is available |  |
| `fishing_hook` | 0 | C | no-state-carrier | circumstance: shell or bone | Shell/bone not shipped goods; disjunction. | non-shipped good: shell/bone |
| `fishing_hook` | 1 | F | requires-authoring | circumstance: line | Line is an artefact; vague. |  |
| `grinding_stone` | 1 | F | requires-authoring | circumstance: handstone | Handstone artefact, not a stocked good. |  |
| `dog_domestication` | 0 | C | no-state-carrier | circumstance: wolf population in range | Wolf ranges not modelled. | fauna/flora range |
| `ochre_processing` | 0 | C | no-state-carrier | circumstance: ochre or manganese deposit | Ochre/manganese deposits not modelled. | terrain/resource deposits |
| `bone_tools` | 0 | C | no-state-carrier | circumstance: bone, antler, ivory | Not shipped goods. | non-shipped good: bone/antler/ivory |
| `bone_tools` | 1 | F | requires-authoring | circumstance: burins | Burins are tool artefacts. |  |
| `lamp` | 1 | C | no-state-carrier | circumstance: animal fat | Fat not a shipped good. | non-shipped good: animal fat |
| `tally_notation` | 0 | C | no-state-carrier | circumstance: bone | Bone not shipped. | non-shipped good: bone |
| `tally_notation` | 1 | F | requires-authoring | circumstance: burin | Burin is a tool artefact. |  |
| `cereal_cultivation` | 0 | C | no-state-carrier | circumstance: wild cereal seed | Wild cereal presence needs flora range state. | fauna/flora range |
| `cereal_cultivation` | 1 | C | no-state-carrier | circumstance: cleared plot | Cleared plot needs land clearance state. | land use/terrain |
| `cereal_domesticated` | 0 | F | requires-authoring | circumstance: cultivated stands over many generations | Duration of cultivation over generations is unquantified. |  |
| `rice_wet` | 0 | C | no-state-carrier | circumstance: wild rice in range | Wild rice range not modeled. | fauna/flora range |
| `rice_wet` | 1 | C | no-state-carrier | circumstance: wetland or floodable land | Wetland terrain state needed. | terrain/hydrology |
| `millet` | 0 | C | no-state-carrier | circumstance: wild millet in range | Wild millet range not modeled. | fauna/flora range |
| `millet` | 1 | C | no-state-carrier | circumstance: loess soil | Soil type state needed. | terrain/soil |
| `maize` | 0 | C | no-state-carrier | circumstance: teosinte in range | Teosinte range not modeled. | fauna/flora range |
| `sorghum_pearl_millet` | 0 | C | no-state-carrier | circumstance: wild sorghum, pennisetum in range | Wild sorghum/pennisetum range not modeled. | fauna/flora range |
| `root_crop` | 0 | C | no-state-carrier | circumstance: wild tuber in range | Wild tuber range not modeled. | fauna/flora range |
| `root_crop` | 1 | F | requires-authoring | circumstance: digging stick | Digging stick is not a shipped good or tech id. |  |
| `legume` | 0 | C | no-state-carrier | circumstance: wild legume in range | Wild legume range not modeled. | fauna/flora range |
| `orchard` | 0 | C | no-state-carrier | circumstance: wild fruit tree in range | Wild fruit tree range not modeled. | fauna/flora range |
| `orchard` | 1 | C | no-state-carrier | circumstance: land held for decades | Long land holding needs tenure state. | land tenure/institutions |
| `sickle` | 0 | F | requires-authoring | circumstance: blades | Blades not a shipped good; vague. |  |
| `sickle` | 1 | F | requires-authoring | circumstance: haft | Haft not a shipped good. |  |
| `digging_stick_hoe` | 1 | F | requires-authoring | circumstance: handle | Handle is not a shipped good. |  |
| `nixtamalization` | 0 | D | implied-by-prerequisites | circumstance: maize | implied by the node's own prerequisites: 'maize' holds whenever nixtamalization is available |  |
| `nixtamalization` | 1 | C | no-state-carrier | circumstance: lime or wood ash | Lime or wood ash not shipped goods; disjunction. | non-shipped good: lime/ash |
| `sheep_goat` | 0 | C | no-state-carrier | circumstance: wild sheep/goat in range | Wild sheep/goat range not modeled. | fauna/flora range |
| `cattle` | 0 | C | no-state-carrier | circumstance: aurochs in range | Aurochs range not modeled. | fauna/flora range |
| `pig` | 0 | C | no-state-carrier | circumstance: wild boar in range | Wild boar range not modeled. | fauna/flora range |
| `pig` | 1 | C | no-state-carrier | circumstance: sedentary village | Sedentary village needs settlement state. | population/urban scale |
| `milking` | 0 | F | requires-authoring | circumstance: milking animals | Milking animals is vague, not exactly livestock stock. |  |
| `milking` | 1 | F | requires-authoring | circumstance: vessels | Vessels not exactly pottery stock. |  |
| `wool` | 0 | F | requires-authoring | circumstance: selected sheep over generations | Selection over generations unquantified. |  |
| `animal_traction` | 0 | F | requires-authoring | circumstance: trained oxen | Trained oxen not a shipped good. |  |
| `animal_traction` | 1 | F | requires-authoring | circumstance: yoke | Yoke not a shipped good. |  |
| `pottery_open_fired` | 1 | C | no-state-carrier | circumstance: temper (sand, shell, grog) | Temper materials not shipped goods. | non-shipped good: temper |
| `kiln_updraft` | 0 | D | implied-by-prerequisites | circumstance: mudbrick | implied by the node's own prerequisites: 'mudbrick' holds whenever kiln_updraft is available |  |
| `potters_wheel_fast` | 0 | F | requires-authoring | circumstance: heavy flywheel | Heavy flywheel not a shipped good. |  |
| `potters_wheel_fast` | 1 | F | requires-authoring | circumstance: well-fitted pivot | Pivot quality vague. |  |
| `mudbrick` | 0 | C | no-state-carrier | circumstance: daub subsoil (NOT potter's clay) | Subsoil type needs deposit state; negation. | terrain/resource deposits |
| `mudbrick` | 1 | F | requires-authoring | circumstance: straw temper | Straw temper not a shipped good. |  |
| `lime_plaster` | 0 | C | no-state-carrier | circumstance: limestone | Limestone not shipped (stone is generic). | terrain/resource deposits |
| `lime_plaster` | 1 | F | requires-authoring | circumstance: fuel in quantity (lime burning is fuel-hungry) | Fuel quantity unstated. |  |
| `timber_frame` | 1 | F | requires-authoring | circumstance: ground-stone axes | Ground-stone axes not exactly a shipped good or tech id. |  |
| `fermentation_grain` | 1 | C | no-state-carrier | circumstance: water | Water availability not tracked. | terrain/water sources |
| `fermentation_fruit` | 0 | C | no-state-carrier | circumstance: fruit or honey | Fruit/honey not shipped goods. | fauna/flora range |
| `fermentation_fruit` | 1 | F | requires-authoring | circumstance: sealed vessel | Sealed vessel is an artefact description, not pottery stock exactly. |  |
| `oil_press` | 0 | C | no-state-carrier | circumstance: oil crop | Oil crops not tracked. | fauna/flora range |
| `oil_press` | 1 | F | requires-authoring | circumstance: crushing basin | Crushing basin is an unmodelled installation. |  |
| `salt_extraction` | 0 | C | no-state-carrier | circumstance: brine spring, sea, or rock salt | Salt sources are geographic deposits. | terrain/resource deposits |
| `salt_extraction` | 1 | F | requires-authoring | circumstance: fuel or sun | Disjunction of fuel or sun, vague. |  |
| `flax` | 0 | C | no-state-carrier | circumstance: flax in range | Plant range not modelled. | fauna/flora range |
| `flax` | 1 | C | no-state-carrier | circumstance: retting water | Retting water not tracked. | terrain/water sources |
| `spinning_spindle` | 0 | F | requires-authoring | circumstance: spindle | Tool artefact not a stock or tech id. |  |
| `loom_warp_weighted` | 0 | F | requires-authoring | circumstance: frame | Frame is vague artefact. |  |
| `loom_warp_weighted` | 1 | F | requires-authoring | circumstance: loom weights | Loom weights are artefacts, not stock. |  |
| `dyeing` | 0 | C | no-state-carrier | circumstance: dye plant | Dye plants not tracked. | fauna/flora range |
| `dyeing` | 1 | C | no-state-carrier | circumstance: mordant (alum, iron) | Alum/iron mordants not shipped goods. | terrain/resource deposits |
| `charcoal` | 0 | F | requires-authoring | circumstance: timber in quantity | Unstated quantity of timber. |  |
| `charcoal` | 1 | F | requires-authoring | circumstance: earth cover | Earth cover not a modelled good. |  |
| `casting_open` | 0 | F | requires-authoring | circumstance: molten copper | Molten copper is a process state, not stock. |  |
| `gold_silver_native` | 0 | C | no-state-carrier | circumstance: placer gold or native silver | Gold/silver deposits not tracked. | terrain/resource deposits |
| `irrigation_basin` | 0 | C | no-state-carrier | circumstance: flood-plain | Floodplain terrain. | terrain/hydrology |
| `irrigation_basin` | 1 | F | requires-authoring | circumstance: earth bunds | Earthworks not modelled; practice description. |  |
| `irrigation_canal` | 0 | C | no-state-carrier | circumstance: river | River presence. | terrain/hydrology |
| `irrigation_canal` | 1 | F | requires-authoring | circumstance: dug channels | Dug channels are a practice/structure. |  |
| `terrace` | 0 | C | no-state-carrier | circumstance: hillside | Slope terrain. | terrain/relief |
| `sledge` | 1 | D | implied-by-prerequisites | circumstance: cordage | implied by the node's own prerequisites: 'cordage' holds whenever sledge is available |  |
| `wheel_solid` | 1 | F | requires-authoring | circumstance: axle | Axle artefact not modelled. |  |
| `wheel_solid` | 2 | F | requires-authoring | circumstance: a rotary craft tradition present | Vague tradition description. |  |
| `cart` | 0 | F | requires-authoring | circumstance: wheels | Wheels not a stock good. |  |
| `cart` | 1 | F | requires-authoring | circumstance: bed | Bed is vague artefact. |  |
| `track_road` | 1 | F | requires-authoring | circumstance: labour | Unstated labour quantity. |  |
| `plough_ard` | 0 | C | no-state-carrier | circumstance: trained oxen | Trained oxen distinct from livestock stock. | livestock training/draft animals |
| `calendar_lunar` | 0 | F | requires-authoring | circumstance: observation over years | Practice over unstated time. |  |
| `calendar_lunar` | 1 | F | requires-authoring | circumstance: marker | Vague marker. |  |
| `trepanation` | 0 | F | requires-authoring | circumstance: flint scraper or drill | Specific tool artefact not a stock good. |  |
| `tin_bronze` | 2 | C | no-state-carrier | circumstance: long-distance exchange reaching a tin source | Requires long-distance exchange reaching a tin source; no trade-route state. | trade network |
| `lost_wax` | 0 | C | no-state-carrier | circumstance: beeswax | Beeswax is not a shipped good. | non-shipped good: beeswax |
| `sheet_metal` | 1 | C | no-state-carrier | circumstance: anvil | Anvil is not a shipped good or tech. | non-shipped good: anvil |
| `mining_shaft` | 0 | C | no-state-carrier | circumstance: ore body | Ore body presence needs deposit state. | terrain/resource deposits |
| `proto_writing` | 1 | C | no-state-carrier | circumstance: reed stylus | Reed stylus is not a shipped good. | non-shipped good: reed stylus |
| `cuneiform` | 1 | C | no-state-carrier | circumstance: stylus | Stylus is not a shipped good. | non-shipped good: stylus |
| `hieroglyphic` | 0 | F | requires-authoring | circumstance: stone, papyrus, ink | Compound list mixing shipped good (stone) and non-shipped papyrus/ink; unclear conjunction. |  |
| `chinese_script` | 0 | C | no-state-carrier | circumstance: ox scapulae, turtle plastrons | Ox scapulae and turtle plastrons are not shipped goods; need fauna/material state. | fauna/flora range |
| `papyrus` | 0 | C | no-state-carrier | circumstance: papyrus reed (Nile delta only) | Papyrus reed presence in Nile delta needs flora range. | fauna/flora range |
| `papyrus` | 1 | C | no-state-carrier | circumstance: papyrus reed (Nile delta) | Papyrus reed presence needs flora range. | fauna/flora range |
| `numeral_sexagesimal` | 0 | C | institution-state-absent | institution present: Scribal school | names an institution's presence; no institutions system exists | institutions |
| `numeral_sexagesimal` | 1 | F | requires-authoring | circumstance: tablet | 'Tablet' is vague; not a shipped good or tech id. |  |
| `arithmetic_babylonian` | 0 | F | requires-authoring | circumstance: tablet | 'Tablet' is vague; not a shipped good or tech id. |  |
| `surveying` | 0 | C | no-state-carrier | circumstance: measuring rope | Measuring rope not a shipped good. | non-shipped good: measuring rope |
| `surveying` | 1 | C | no-state-carrier | circumstance: sighting instrument | Sighting instrument not a shipped good. | non-shipped good: sighting instrument |
| `sail_square` | 0 | F | requires-authoring | circumstance: hull | 'Hull' is vague; not a shipped good or exact tech id. |  |
| `sail_square` | 1 | F | requires-authoring | circumstance: mast | 'Mast' is vague; not a shipped good or tech id. |  |
| `plank_boat` | 1 | F | requires-authoring | circumstance: sewing cordage or dowels | Disjunctive 'cordage or dowels', neither exactly a shipped good. |  |
| `horse_domestication` | 0 | C | no-state-carrier | circumstance: wild horse in steppe range | Wild horse presence needs fauna range. | fauna/flora range |
| `donkey` | 0 | C | no-state-carrier | circumstance: wild ass in range | Wild ass presence needs fauna range. | fauna/flora range |
| `camel` | 0 | C | no-state-carrier | circumstance: wild camel in range | Wild camel presence needs fauna range. | fauna/flora range |
| `chariot` | 0 | F | requires-authoring | circumstance: spoked wheels | 'Spoked wheels' as goods not shipped; ambiguous vs tech wheel_spoked. |  |
| `chariot` | 1 | C | no-state-carrier | circumstance: trained horses | Trained horses as stock not a shipped good. | military/battle |
| `composite_bow` | 0 | C | no-state-carrier | circumstance: horn | Horn not a shipped good. | non-shipped good: horn |
| `composite_bow` | 1 | C | no-state-carrier | circumstance: sinew | Sinew not a shipped good. | non-shipped good: sinew |
| `fired_brick` | 1 | F | requires-authoring | circumstance: fuel at volume | Unstated quantity of fuel; vague. |  |
| `glass_glaze` | 0 | C | no-state-carrier | circumstance: quartz | Quartz not a shipped good. | non-shipped good: quartz |
| `glass_glaze` | 1 | F | requires-authoring | circumstance: natron or plant ash | Disjunction 'natron or plant ash', neither shipped. |  |
| `glass_core` | 0 | C | no-state-carrier | circumstance: silica | Silica not a shipped good. | non-shipped good: silica |
| `glass_core` | 1 | C | no-state-carrier | circumstance: soda | Soda not a shipped good. | non-shipped good: soda |
| `standard_weights` | 1 | C | no-state-carrier | circumstance: balance | Balance scale not a shipped good. | non-shipped good: balance |
| `calendar_civil` | 0 | F | requires-authoring | circumstance: records over years | 'Records over years' is vague with unstated duration. |  |
| `water_clock` | 0 | F | requires-authoring | circumstance: vessel with calibrated outlet | Calibrated vessel is a specification, not a stock good. |  |
| `medicine_recorded` | 0 | F | requires-authoring | circumstance: tablet or papyrus | Disjunction 'tablet or papyrus', neither a shipped good. |  |
| `beekeeping` | 0 | C | no-state-carrier | circumstance: bee population | Bee population needs fauna range. | fauna/flora range |
| `iron_bloomery` | 0 | C | no-state-carrier | circumstance: iron ore (near-universal) | Iron ore is not a shipped good; needs deposit state. | terrain/resource deposits |
| `iron_bloomery` | 1 | C | no-state-carrier | circumstance: charcoal at scale | Charcoal at scale requires a charcoal good and quantity. | non-shipped good: charcoal |
| `bellows` | 0 | C | no-state-carrier | circumstance: leather | Leather is not a shipped good (hides is distinct). | non-shipped good: leather |
| `steel_carburized` | 0 | C | no-state-carrier | circumstance: wrought iron | Wrought iron is not a shipped good. | non-shipped good: wrought iron |
| `steel_carburized` | 1 | D | implied-by-prerequisites | circumstance: charcoal | implied by the node's own prerequisites: 'charcoal' holds whenever steel_carburized is available |  |
| `quenching` | 0 | C | no-state-carrier | circumstance: steel | Steel is not a shipped good. | non-shipped good: steel |
| `quenching` | 1 | F | requires-authoring | circumstance: water or oil | Disjunction of water or oil; unclear carrier. |  |
| `tempering` | 0 | C | no-state-carrier | circumstance: quenched steel | Quenched steel is not a shipped good. | non-shipped good: quenched steel |
| `tempering` | 1 | F | requires-authoring | circumstance: controlled reheating | Practice description, not a state. |  |
| `wootz` | 0 | C | no-state-carrier | circumstance: wrought iron | Wrought iron is not a shipped good. | non-shipped good: wrought iron |
| `wootz` | 1 | F | requires-authoring | circumstance: organic carbon | Vague 'organic carbon' cannot map to a condition. |  |
| `pattern_welding` | 0 | C | no-state-carrier | circumstance: iron and steel strips | Iron and steel strips are not shipped goods. | non-shipped goods: iron, steel |
| `pattern_welding` | 1 | F | requires-authoring | circumstance: flux | Flux unspecified material. |  |
| `abjad` | 0 | F | requires-authoring | circumstance: any surface | 'Any surface' is vacuous. |  |
| `alphabet_vowels` | 0 | F | requires-authoring | circumstance: any surface | 'Any surface' is vacuous. |  |
| `hacksilver` | 0 | C | no-state-carrier | circumstance: silver | Silver is not a shipped good. | non-shipped good: silver |
| `hacksilver` | 1 | C | no-state-carrier | circumstance: balance | Balance is not a shipped good. | non-shipped good: balance scales |
| `coinage_electrum` | 1 | C | no-state-carrier | circumstance: dies | Dies are not a shipped good. | non-shipped good: dies |
| `cavalry` | 0 | C | no-state-carrier | circumstance: trained horses in number | Horse numbers and training not modelled; unstated quantity. | fauna/horses and military |
| `cavalry` | 1 | F | requires-authoring | circumstance: riders | 'Riders' vague practice/population descriptor. |  |
| `saddle` | 0 | C | no-state-carrier | circumstance: leather | Leather not shipped. | non-shipped good: leather |
| `saddle` | 1 | C | no-state-carrier | circumstance: felt | Felt not shipped. | non-shipped good: felt |
| `siege_ram` | 2 | C | no-state-carrier | circumstance: fortified settlement among neighbours | Neighbour fortification state does not exist. | military/settlements and contact |
| `siege_tower` | 1 | F | requires-authoring | circumstance: earth | 'Earth' is vague, effectively universal. |  |
| `stone_fortification` | 1 | F | requires-authoring | circumstance: cranes/levers | Compound 'cranes/levers' undefined. |  |
| `crossbow` | 0 | C | no-state-carrier | circumstance: bow stave | Bow stave not shipped. | non-shipped good: bow stave |
| `crossbow` | 1 | F | requires-authoring | circumstance: stock | 'Stock' is a component description. |  |
| `naval_ram` | 0 | F | requires-authoring | circumstance: keel and hull | Keel and hull describe a craft, not stock. |  |
| `qanat` | 0 | C | no-state-carrier | circumstance: aquifer | Aquifer state does not exist. | terrain/hydrology |
| `qanat` | 1 | F | requires-authoring | circumstance: vertical shafts | Shafts describe construction practice. |  |
| `aqueduct_tunnel` | 0 | C | no-state-carrier | circumstance: rock | Rock terrain not modelled (stone stock differs). | terrain/resource deposits |
| `aqueduct_tunnel` | 1 | C | no-state-carrier | circumstance: iron picks | Iron picks not a shipped good. | non-shipped good: iron tools |
| `aqueduct_channel` | 1 | C | no-state-carrier | circumstance: lime mortar | Lime mortar not shipped. | non-shipped good: lime mortar |
| `arch` | 1 | F | requires-authoring | circumstance: centring | Centring is a construction practice. |  |
| `astronomy_records` | 0 | C | institution-state-absent | institution present: State archive | names an institution's presence; no institutions system exists | institutions |
| `astronomy_records` | 1 | C | no-state-carrier | circumstance: tablets | Tablets not shipped (clay is raw). | non-shipped good: tablets |
| `astronomy_records` | 2 | C | no-state-carrier | circumstance: observers | Dedicated observers need institutional state. | institutions |
| `calendar_lunisolar` | 0 | F | requires-authoring | circumstance: records | 'Records' vague. |  |
| `geometry_practical` | 0 | F | requires-authoring | circumstance: none | 'None' is not a condition. |  |
| `medicine_hippocratic` | 0 | C | institution-state-absent | institution present: Natural philosophy | names an institution's presence; no institutions system exists | institutions |
| `medicine_hippocratic` | 1 | F | requires-authoring | circumstance: texts | 'Texts' vague. |  |
| `medicine_hippocratic` | 2 | C | no-state-carrier | circumstance: patients | Patients/disease state does not exist. | population/health |
| `glass_blown_precursor` | 0 | C | no-state-carrier | circumstance: glass | Glass not shipped. | non-shipped good: glass |
| `glass_blown_precursor` | 1 | F | requires-authoring | circumstance: moulds | Moulds undefined material. |  |
| `rotary_quern` | 1 | F | requires-authoring | circumstance: handle | 'Handle' is a component description. |  |
| `olive_press_beam` | 0 | F | requires-authoring | circumstance: beam | 'Beam' is a component; timber not stated. |  |
| `olive_press_beam` | 1 | F | requires-authoring | circumstance: weights | 'Weights' unspecified. |  |
| `geometry_axiomatic` | 0 | C | institution-state-absent | institution present: Library | names an institution's presence; no institutions system exists | institutions |
| `geometry_axiomatic` | 1 | C | no-state-carrier | circumstance: texts | Single circumstance 'texts'; no text-stock state exists. | written corpus/libraries |
| `mechanics_archimedean` | 0 | C | no-state-carrier | circumstance: texts | 'texts' has no state carrier. | written corpus/libraries |
| `torsion_artillery` | 0 | C | no-state-carrier | circumstance: sinew rope | Sinew rope is not a shipped good. | non-shipped good: sinew rope |
| `water_screw` | 1 | C | no-state-carrier | circumstance: pitch | Pitch not a shipped good. | non-shipped good: pitch |
| `noria` | 0 | F | requires-authoring | circumstance: wheel | 'wheel' is ambiguous: technology or object. |  |
| `noria` | 1 | C | no-state-carrier | circumstance: buckets | Buckets not a shipped good. | non-shipped good: buckets |
| `water_mill` | 0 | C | no-state-carrier | circumstance: river with head | River head is terrain state not modelled. | terrain/hydrology |
| `water_mill` | 1 | F | requires-authoring | circumstance: wheel | 'wheel' ambiguous object vs technology. |  |
| `trip_hammer` | 0 | F | requires-authoring | circumstance: water wheel | 'water wheel' not an exact tech id or good. |  |
| `trip_hammer` | 1 | F | requires-authoring | circumstance: cam shaft | 'cam shaft' is a component description. |  |
| `gearing` | 1 | F | requires-authoring | circumstance: precision cutting | 'precision cutting' is a practice description. |  |
| `concrete_pozzolan` | 0 | C | no-state-carrier | circumstance: lime | Lime not shipped. | non-shipped good: lime |
| `concrete_pozzolan` | 1 | C | no-state-carrier | circumstance: POZZOLAN (volcanic ash — Bay of Naples) | Volcanic ash deposit not modelled. | terrain/resource deposits |
| `vault_dome` | 0 | F | requires-authoring | circumstance: concrete | 'concrete' ambiguous: good or tech id not exact. |  |
| `vault_dome` | 1 | F | requires-authoring | circumstance: centring | 'centring' is a construction practice. |  |
| `road_paved` | 0 | C | institution-state-absent | institution present: Built road | names an institution's presence; no institutions system exists | institutions |
| `road_paved` | 2 | C | no-state-carrier | circumstance: gravel | Gravel not shipped. | non-shipped good: gravel |
| `bridge_stone` | 1 | F | requires-authoring | circumstance: cofferdams | Cofferdams are a practice/structure, not state. |  |
| `aqueduct_arcade` | 1 | F | requires-authoring | circumstance: concrete | 'concrete' not an exact good or tech id. |  |
| `glass_blowing` | 0 | C | no-state-carrier | circumstance: glass | Glass not shipped. | non-shipped good: glass |
| `glass_blowing` | 1 | C | no-state-carrier | circumstance: iron blowpipe | Iron blowpipe not a shipped good. | non-shipped good: iron tools |
| `window_glass` | 0 | C | no-state-carrier | circumstance: glass | Glass not shipped. | non-shipped good: glass |
| `window_glass` | 1 | F | requires-authoring | circumstance: flat casting | 'flat casting' is a practice. |  |
| `cast_iron` | 0 | C | no-state-carrier | circumstance: iron ore | Iron ore not a shipped good. | terrain/resource deposits |
| `cast_iron` | 1 | D | implied-by-prerequisites | circumstance: charcoal | implied by the node's own prerequisites: 'charcoal' holds whenever cast_iron is available |  |
| `iron_mouldboard` | 0 | D | implied-by-prerequisites | circumstance: cast iron | implied by the node's own prerequisites: 'cast_iron' holds whenever iron_mouldboard is available |  |
| `iron_mouldboard` | 1 | F | requires-authoring | circumstance: oxen | 'oxen' not exactly 'livestock'; inference required. |  |
| `seed_drill` | 0 | C | no-state-carrier | circumstance: iron tubes | Iron tubes not shipped. | non-shipped good: iron |
| `seed_drill` | 1 | F | requires-authoring | circumstance: frame | 'frame' vague. |  |
| `collar_harness` | 0 | F | requires-authoring | circumstance: leather | 'leather' not exactly 'hides'. |  |
| `collar_harness` | 1 | F | requires-authoring | circumstance: padding | 'padding' vague. |  |
| `paper` | 1 | C | no-state-carrier | circumstance: water | Water availability not modelled. | terrain/hydrology |
| `paper` | 2 | C | institution-state-absent | institution present: a writing tradition | names an institution's presence; no institutions system exists | institutions |
| `crossbow_repeating` | 1 | F | requires-authoring | circumstance: magazine mechanism | Mechanism description. |  |
| `stirrup` | 0 | C | no-state-carrier | circumstance: iron | Iron not shipped. | non-shipped good: iron |
| `stirrup` | 1 | F | requires-authoring | circumstance: leather | 'leather' not exactly hides. |  |
| `silk` | 0 | C | no-state-carrier | circumstance: mulberry | Mulberry range not modelled. | fauna/flora range |
| `silk` | 1 | C | no-state-carrier | circumstance: Bombyx mori | Silkworm range not modelled. | fauna/flora range |
| `lacquer` | 0 | C | no-state-carrier | circumstance: lacquer tree sap | Lacquer tree not modelled. | fauna/flora range |
| `lacquer` | 1 | F | requires-authoring | circumstance: wooden core | 'wooden core' not exactly timber stock. |  |
| `astronomy_geometric` | 0 | C | no-state-carrier | circumstance: texts | Texts not modelled. | written corpus/libraries |
| `astronomy_geometric` | 1 | F | requires-authoring | circumstance: instruments | 'instruments' vague. |  |
| `cartography` | 0 | C | no-state-carrier | circumstance: texts | Texts not modelled. | written corpus/libraries |
| `cartography` | 1 | C | no-state-carrier | circumstance: itineraries | Itineraries need route/travel records. | trade network |
| `latitude_sailing` | 0 | C | no-state-carrier | circumstance: clear sky | Sky clarity is climate state. | climate |
| `latitude_sailing` | 1 | F | requires-authoring | circumstance: star knowledge | Knowledge description. |  |
| `monsoon_sailing` | 0 | C | no-state-carrier | circumstance: ships | Ships not a state carrier. | naval/shipping |
| `monsoon_sailing` | 1 | C | no-state-carrier | circumstance: monsoon | Monsoon regime not modelled. | climate |
| `lateen` | 0 | F | requires-authoring | circumstance: sailcloth | 'sailcloth' not exactly cloth. |  |
| `lateen` | 1 | F | requires-authoring | circumstance: spar | 'spar' not exactly timber. |  |
| `mortise_hull` | 1 | F | requires-authoring | circumstance: tenons | 'tenons' component, not state. |  |
| `pharmacology` | 0 | C | institution-state-absent | institution present: Library | names an institution's presence; no institutions system exists | institutions |
| `pharmacology` | 1 | C | no-state-carrier | circumstance: texts | Texts not modelled. | written corpus/libraries |
| `pharmacology` | 2 | F | requires-authoring | circumstance: plant knowledge | Knowledge description. |  |
| `surgery_instruments` | 1 | C | no-state-carrier | circumstance: steel | Steel not shipped. | non-shipped good: steel |
| `abacus` | 0 | F | requires-authoring | circumstance: board or frame | Vague alternative 'board or frame'. |  |
| `abacus` | 1 | F | requires-authoring | circumstance: counters | 'counters' vague. |  |
| `alchemy` | 0 | C | institution-state-absent | institution present: Natural philosophy | names an institution's presence; no institutions system exists | institutions |
| `alchemy` | 1 | F | requires-authoring | circumstance: furnace | 'furnace' vague. |  |
| `alchemy` | 2 | C | no-state-carrier | circumstance: glass apparatus | Glass apparatus not shipped. | non-shipped good: glass |
| `heavy_plough` | 0 | C | no-state-carrier | circumstance: iron | Iron is not a shipped good. | non-shipped good: iron |
| `heavy_plough` | 1 | F | requires-authoring | circumstance: team of 4-8 oxen | Team size of oxen is a quantity not expressible as stock check. |  |
| `horse_collar` | 1 | F | requires-authoring | circumstance: padding | Padding is vague material description. |  |
| `horseshoe` | 0 | C | no-state-carrier | circumstance: iron | Iron not shipped. | non-shipped good: iron |
| `horseshoe` | 1 | C | no-state-carrier | circumstance: nails | Nails not a shipped good. | non-shipped good: nails |
| `rice_champa` | 0 | C | no-state-carrier | circumstance: Champa seed stock | Requires acquired seed stock via contact. | contact/diffusion |
| `rice_champa` | 1 | F | requires-authoring | circumstance: irrigation | Irrigation is a practice, not a condition. |  |
| `sugar_refining` | 0 | C | no-state-carrier | circumstance: sugarcane | Sugarcane crop availability. | fauna/flora range |
| `sugar_refining` | 1 | F | requires-authoring | circumstance: boiling vessels | Vague equipment description. |  |
| `windmill_vertical` | 1 | F | requires-authoring | circumstance: matting sails | Matting sails is a vague material. |  |
| `fulling_mill` | 0 | F | requires-authoring | circumstance: water wheel | Water wheel is a technology description not matching an exact id. |  |
| `fulling_mill` | 1 | F | requires-authoring | circumstance: hammers | Vague tool. |  |
| `spinning_wheel` | 0 | F | requires-authoring | circumstance: wheel | Vague component. |  |
| `spinning_wheel` | 1 | F | requires-authoring | circumstance: spindle | Vague component. |  |
| `horizontal_loom` | 1 | F | requires-authoring | circumstance: treadles | Vague component. |  |
| `cotton_gin_roller` | 0 | F | requires-authoring | circumstance: rollers | Vague component. |  |
| `cotton_gin_roller` | 1 | F | requires-authoring | circumstance: crank | Vague component. |  |
| `compass_magnetic` | 0 | C | no-state-carrier | circumstance: lodestone or magnetised iron | Lodestone deposit or iron; disjunctive non-shipped goods. | terrain/resource deposits |
| `compass_magnetic` | 1 | F | requires-authoring | circumstance: pivot or float | Vague disjunctive component. |  |
| `compass_magnetic` | 2 | F | requires-authoring | circumstance: open-sea voyaging | Practice description without defined state. |  |
| `stern_rudder` | 1 | C | no-state-carrier | circumstance: iron pintles | Iron fittings not shipped. | non-shipped good: iron |
| `watertight_bulkhead` | 1 | F | requires-authoring | circumstance: caulking | Caulking is a practice. |  |
| `portolan` | 0 | C | no-state-carrier | circumstance: vellum | Vellum not shipped. | non-shipped good: vellum |
| `portolan` | 1 | F | requires-authoring | circumstance: compass bearings | Knowledge description. |  |
| `astrolabe` | 0 | C | no-state-carrier | circumstance: brass | Brass not shipped. | non-shipped good: brass |
| `astrolabe` | 1 | F | requires-authoring | circumstance: engraving | Craft practice. |  |
| `cog` | 0 | C | no-state-carrier | circumstance: oak | Specific timber species not tracked. | fauna/flora range |
| `cog` | 1 | C | no-state-carrier | circumstance: iron nails | Not shipped. | non-shipped good: iron nails |
| `gunpowder` | 0 | C | no-state-carrier | circumstance: saltpetre (nitrate-rich soil, manure) | Nitrate sources not modelled. | terrain/resource deposits |
| `gunpowder` | 1 | C | no-state-carrier | circumstance: sulphur | Sulphur deposits not modelled. | terrain/resource deposits |
| `saltpetre_refining` | 0 | C | no-state-carrier | circumstance: nitrate earth | Nitrate earth not modelled. | terrain/resource deposits |
| `saltpetre_refining` | 1 | C | no-state-carrier | circumstance: wood ash | Not shipped. | non-shipped good: wood ash |
| `cannon_early` | 1 | F | requires-authoring | circumstance: powder | 'powder' is not exactly a tech id or good. |  |
| `trebuchet` | 1 | F | requires-authoring | circumstance: counterweight | Component description. |  |
| `longbow` | 0 | C | no-state-carrier | circumstance: yew (imported) | Imported yew needs trade state. | trade network |
| `longbow` | 1 | C | no-state-carrier | circumstance: trained archers from childhood | Training regime institution not modelled. | institutions |
| `plate_armour` | 0 | C | no-state-carrier | circumstance: steel plate | Steel not shipped. | non-shipped good: steel |
| `plate_armour` | 1 | F | requires-authoring | circumstance: water-powered hammer | Technology description not an exact id. |  |
| `greek_fire` | 0 | C | no-state-carrier | circumstance: petroleum | Petroleum seeps not modelled. | terrain/resource deposits |
| `greek_fire` | 1 | C | no-state-carrier | circumstance: resin | Not shipped. | non-shipped good: resin |
| `woodblock_print` | 0 | D | implied-by-prerequisites | circumstance: paper | implied by the node's own prerequisites: 'paper' holds whenever woodblock_print is available |  |
| `woodblock_print` | 1 | C | no-state-carrier | circumstance: ink | Ink not shipped. | non-shipped good: ink |
| `movable_type_ceramic` | 1 | F | requires-authoring | circumstance: frame | Vague component. |  |
| `movable_type_metal` | 1 | F | requires-authoring | circumstance: moulds | Vague component. |  |
| `paper_money` | 0 | D | implied-by-prerequisites | circumstance: paper | implied by the node's own prerequisites: 'paper' holds whenever paper_money is available |  |
| `paper_money` | 1 | F | requires-authoring | circumstance: printing | 'printing' not an exact tech id. |  |
| `hindu_arabic` | 0 | F | requires-authoring | circumstance: texts | Vague. |  |
| `algebra` | 0 | F | requires-authoring | circumstance: texts | Vague. |  |
| `trigonometry` | 0 | F | requires-authoring | circumstance: texts | Vague. |  |
| `trigonometry` | 1 | F | requires-authoring | circumstance: tables | Vague. |  |
| `optics_ibn_haytham` | 0 | F | requires-authoring | circumstance: lenses | Vague artefact. |  |
| `optics_ibn_haytham` | 1 | F | requires-authoring | circumstance: dark chamber | Vague setup. |  |
| `spectacles` | 0 | C | no-state-carrier | circumstance: clear glass | Glass not shipped. | non-shipped good: glass |
| `spectacles` | 1 | F | requires-authoring | circumstance: grinding | Practice. |  |
| `crank_connecting_rod` | 0 | C | no-state-carrier | circumstance: iron crank | Iron not shipped. | non-shipped good: iron |
| `crank_connecting_rod` | 1 | F | requires-authoring | circumstance: rod | Vague component. |  |
| `escapement_verge` | 0 | C | no-state-carrier | circumstance: iron | Iron not shipped. | non-shipped good: iron |
| `escapement_verge` | 1 | F | requires-authoring | circumstance: precision filing | Skill practice. |  |
| `blast_furnace_water` | 0 | F | requires-authoring | circumstance: tall furnace | Vague structure. |  |
| `blast_furnace_water` | 1 | F | requires-authoring | circumstance: waterwheel | Not an exact tech id. |  |
| `wire_drawing` | 0 | C | no-state-carrier | circumstance: iron or brass | Not shipped; disjunction. | non-shipped good: iron/brass |
| `wire_drawing` | 1 | F | requires-authoring | circumstance: hardened die | Vague tool. |  |
| `canal_lock` | 1 | F | requires-authoring | circumstance: masonry chamber | Structure description. |  |
| `double_entry` | 0 | F | requires-authoring | circumstance: ledgers | Vague. |  |
| `double_entry` | 1 | D | implied-by-prerequisites | circumstance: paper | implied by the node's own prerequisites: 'paper' holds whenever double_entry is available |  |
| `bill_of_exchange` | 0 | D | implied-by-prerequisites | circumstance: paper | implied by the node's own prerequisites: 'paper' holds whenever bill_of_exchange is available |  |
| `bill_of_exchange` | 1 | C | no-state-carrier | circumstance: merchant network | Merchant network state absent. | trade network |
| `printing_press` | 0 | C | no-state-carrier | circumstance: type metal | Alloy not a shipped good. | non-shipped good: type metal (lead-tin-antimony) |
| `printing_press` | 1 | F | requires-authoring | circumstance: press | 'press' is a device description, not stateable. |  |
| `printing_press` | 2 | D | implied-by-prerequisites | circumstance: alphabetic script (makes movable type economic) | implied by the node's own prerequisites: 'abjad OR alphabet_vowels' holds whenever printing_press is available |  |
| `type_founding` | 0 | C | no-state-carrier | circumstance: type metal | Alloy not shipped. | non-shipped good: type metal |
| `type_founding` | 1 | C | no-state-carrier | circumstance: steel punches | Steel punches need a steel good. | non-shipped good: steel |
| `caravel` | 1 | F | requires-authoring | circumstance: lateen sails | Rig feature, not a stock or tech id. |  |
| `carrack` | 0 | C | no-state-carrier | circumstance: oak | Specific timber species not tracked. | fauna/flora range: oak timber |
| `carrack` | 1 | F | requires-authoring | circumstance: three masts | Design attribute, not a condition. |  |
| `polynesian_canoe` | 0 | F | requires-authoring | circumstance: two hulls | Design attribute. |  |
| `polynesian_canoe` | 1 | C | no-state-carrier | circumstance: pandanus sail | Plant material not tracked. | fauna/flora range: pandanus |
| `celestial_navigation` | 0 | F | requires-authoring | circumstance: instrument | Vague 'instrument'. |  |
| `celestial_navigation` | 1 | F | requires-authoring | circumstance: tables | Knowledge artefact, unspecified. |  |
| `longitude_problem` | 0 | F | requires-authoring | circumstance: precision clockwork | Capability description, not a stock. |  |
| `mercator` | 0 | F | requires-authoring | circumstance: mathematics | Vague knowledge field. |  |
| `corned_powder` | 0 | C | no-state-carrier | circumstance: powder | Powder not shipped. | non-shipped good: gunpowder |
| `corned_powder` | 1 | F | requires-authoring | circumstance: wetting | Practice description. |  |
| `cannon_cast_bronze` | 0 | F | requires-authoring | circumstance: bronze at scale | Unstated quantity 'at scale'. |  |
| `cannon_cast_bronze` | 1 | F | requires-authoring | circumstance: casting pits | Facility/practice, not stock. |  |
| `cannon_cast_iron` | 0 | D | implied-by-prerequisites | circumstance: cast iron | implied by the node's own prerequisites: 'cast_iron' holds whenever cannon_cast_iron is available |  |
| `cannon_cast_iron` | 1 | F | requires-authoring | circumstance: boring | Practice description. |  |
| `matchlock` | 0 | F | requires-authoring | circumstance: barrel | Component, not stock. |  |
| `matchlock` | 1 | F | requires-authoring | circumstance: stock | Component, not stock. |  |
| `wheellock` | 0 | C | no-state-carrier | circumstance: precision steel spring | Steel springs not shipped. | non-shipped good: steel |
| `wheellock` | 1 | C | no-state-carrier | circumstance: pyrite | Mineral not tracked. | terrain/resource deposits: pyrite |
| `flintlock` | 0 | C | no-state-carrier | circumstance: flint | Flint not tracked. | terrain/resource deposits: flint |
| `flintlock` | 1 | C | no-state-carrier | circumstance: steel frizzen | Steel not shipped. | non-shipped good: steel |
| `trace_italienne` | 0 | F | requires-authoring | circumstance: earth | Earth is not a stock good. |  |
| `trace_italienne` | 1 | F | requires-authoring | circumstance: masonry facing | Construction practice, ambiguous. |  |
| `ship_of_line` | 0 | F | requires-authoring | circumstance: oak (thousands of trees) | Unstated quantity, species-specific timber. |  |
| `ship_of_line` | 1 | C | no-state-carrier | circumstance: iron guns | Iron/guns not shipped. | non-shipped good: iron guns |
| `heliocentrism` | 0 | F | requires-authoring | circumstance: observation records | Knowledge description. |  |
| `heliocentrism` | 1 | F | requires-authoring | circumstance: printed tables | Knowledge artefact. |  |
| `telescope` | 0 | C | no-state-carrier | circumstance: ground lenses | Glass not shipped. | non-shipped good: glass lenses |
| `telescope` | 1 | F | requires-authoring | circumstance: tube | Generic component. |  |
| `microscope` | 0 | C | no-state-carrier | circumstance: lenses | Glass not shipped. | non-shipped good: glass lenses |
| `mechanics_newtonian` | 0 | F | requires-authoring | circumstance: texts | Vague 'texts'. |  |
| `calculus` | 0 | F | requires-authoring | circumstance: texts | Vague 'texts'. |  |
| `barometer_vacuum` | 0 | C | no-state-carrier | circumstance: glass tube | Glass not shipped. | non-shipped good: glass |
| `barometer_vacuum` | 1 | C | no-state-carrier | circumstance: mercury | Mercury not tracked. | terrain/resource deposits: mercury |
| `steam_atmospheric` | 0 | C | no-state-carrier | circumstance: cast iron cylinder | Iron not shipped. | non-shipped good: cast iron |
| `steam_atmospheric` | 1 | F | requires-authoring | circumstance: boiler | Component description. |  |
| `coke` | 0 | C | no-state-carrier | circumstance: coal deposit | Coal deposits not tracked. | terrain/resource deposits: coal |
| `coke` | 1 | F | requires-authoring | circumstance: coking ovens | Facility description. |  |
| `brass_calamine` | 1 | C | no-state-carrier | circumstance: calamine | Zinc ore not tracked. | terrain/resource deposits: zinc ore (calamine) |
| `inoculation` | 0 | C | no-state-carrier | circumstance: smallpox material | Requires smallpox presence state. | disease/epidemics |
| `anatomy_dissection` | 0 | F | requires-authoring | circumstance: cadavers | Institutional/practice condition, unstateable. |  |
| `anatomy_dissection` | 1 | F | requires-authoring | circumstance: printing | Ambiguous; not an exact tech id. |  |
| `crop_rotation_norfolk` | 0 | C | no-state-carrier | circumstance: turnip and clover seed | Crop species not tracked. | fauna/flora range: turnip, clover |
| `crop_rotation_norfolk` | 1 | C | no-state-carrier | circumstance: enclosed fields | Enclosure state absent. | institutions: land tenure/enclosure |
| `seed_drill_tull` | 0 | C | no-state-carrier | circumstance: iron | Iron not shipped. | non-shipped good: iron |
| `seed_drill_tull` | 1 | C | no-state-carrier | circumstance: horse | Livestock is generic; horse not distinct. | fauna/flora range: horses |
| `selective_breeding` | 0 | F | requires-authoring | circumstance: pedigree records | Record-keeping practice. |  |
| `selective_breeding` | 1 | C | no-state-carrier | circumstance: enclosure | Enclosure state absent. | institutions: land tenure/enclosure |
| `new_world_crops` | 0 | C | no-state-carrier | circumstance: seed and tuber stock | Specific crops not tracked. | fauna/flora range: crop species |
| `new_world_crops` | 1 | C | no-state-carrier | circumstance: ocean transport | Ocean transport links absent. | trade network / contact-diffusion |
| `glass_lead` | 0 | C | no-state-carrier | circumstance: lead oxide | Not shipped. | non-shipped good: lead oxide |
| `glass_lead` | 1 | C | no-state-carrier | circumstance: silica | Not tracked. | terrain/resource deposits: silica sand |
| `canal_navigation` | 0 | F | requires-authoring | circumstance: excavation | Practice description. |  |
| `canal_navigation` | 1 | F | requires-authoring | circumstance: locks | Structure, not stock or tech id. |  |
| `steam_separate_condenser` | 0 | F | requires-authoring | circumstance: precision-bored cylinder | Machined component description, no state carrier; needs authoring. |  |
| `steam_separate_condenser` | 1 | F | requires-authoring | circumstance: condenser | Component of the invention itself; not a condition. |  |
| `cylinder_boring` | 0 | F | requires-authoring | circumstance: boring bar | Tool component; not a stocked good or tech id. |  |
| `cylinder_boring` | 1 | C | no-state-carrier | circumstance: water power | Requires hydrological/power-site state. | terrain/water power sites |
| `steam_rotary` | 0 | F | requires-authoring | circumstance: engine | Vague 'engine'; ambiguous which tech or good. |  |
| `steam_rotary` | 1 | D | implied-by-prerequisites | circumstance: gearing | implied by the node's own prerequisites: 'gearing' holds whenever steam_rotary is available |  |
| `steam_high_pressure` | 0 | C | no-state-carrier | circumstance: wrought-iron boiler | Wrought iron is not a shipped good. | non-shipped good: wrought iron |
| `puddling` | 0 | C | no-state-carrier | circumstance: pig iron | Pig iron not a shipped good. | non-shipped good: pig iron |
| `puddling` | 1 | C | no-state-carrier | circumstance: coal | Coal not a shipped good. | terrain/resource deposits: coal |
| `hot_blast` | 0 | F | requires-authoring | circumstance: heated air | Process description, not a condition. |  |
| `hot_blast` | 1 | F | requires-authoring | circumstance: stove | Equipment component; vague. |  |
| `bessemer` | 0 | C | no-state-carrier | circumstance: low-phosphorus ore (until 1878) | Ore phosphorus grade not modelled. | terrain/resource deposits: ore chemistry |
| `bessemer` | 1 | F | requires-authoring | circumstance: converter | Invention component itself. |  |
| `open_hearth` | 0 | F | requires-authoring | circumstance: regenerative furnace | Component of the invention. |  |
| `open_hearth` | 1 | C | no-state-carrier | circumstance: gas | Gas not a shipped good. | non-shipped good: fuel gas |
| `basic_process` | 0 | C | no-state-carrier | circumstance: dolomite lining | Dolomite not a shipped good. | non-shipped good: dolomite |
| `slide_rest_lathe` | 0 | F | requires-authoring | circumstance: iron lathe | Vague tool description. |  |
| `slide_rest_lathe` | 1 | F | requires-authoring | circumstance: lead screw | Component of the invention. |  |
| `interchangeable_parts` | 0 | F | requires-authoring | circumstance: gauges | Practice tool, not a state item. |  |
| `interchangeable_parts` | 1 | F | requires-authoring | circumstance: jigs | Practice tool, not a state item. |  |
| `milling_machine` | 0 | F | requires-authoring | circumstance: cutter | Component description. |  |
| `milling_machine` | 1 | F | requires-authoring | circumstance: indexed table | Component description. |  |
| `flying_shuttle` | 0 | F | requires-authoring | circumstance: shuttle race | Loom component. |  |
| `flying_shuttle` | 1 | F | requires-authoring | circumstance: cord | Vague; cord not a shipped good. |  |
| `spinning_jenny` | 0 | F | requires-authoring | circumstance: frame | Vague component. |  |
| `spinning_jenny` | 1 | F | requires-authoring | circumstance: multiple spindles | Component description. |  |
| `water_frame` | 0 | F | requires-authoring | circumstance: rollers | Component description. |  |
| `water_frame` | 1 | F | requires-authoring | circumstance: water wheel | Ambiguous: device or tech; not exactly a corpus id given. |  |
| `spinning_mule` | 0 | F | requires-authoring | circumstance: carriage | Component description. |  |
| `spinning_mule` | 1 | F | requires-authoring | circumstance: spindles | Component description. |  |
| `power_loom` | 0 | C | no-state-carrier | circumstance: iron loom | Iron machinery not a shipped good. | non-shipped good: iron |
| `power_loom` | 1 | F | requires-authoring | circumstance: power | Vague 'power'. |  |
| `cotton_gin_saw` | 0 | F | requires-authoring | circumstance: saw cylinders | Component description. |  |
| `jacquard` | 0 | F | requires-authoring | circumstance: punched cards | Component of the invention. |  |
| `jacquard` | 1 | F | requires-authoring | circumstance: hooks | Component description. |  |
| `locomotive` | 0 | F | requires-authoring | circumstance: boiler | Component description. |  |
| `locomotive` | 1 | C | no-state-carrier | circumstance: iron wheels | Iron not a shipped good. | non-shipped good: iron |
| `railway` | 0 | C | institution-state-absent | institution present: Joint-stock company | names an institution's presence; no institutions system exists | institutions |
| `railway` | 1 | C | no-state-carrier | circumstance: rails at scale | Rails at scale not modelled. | non-shipped good: iron rails / industrial scale |
| `railway` | 2 | C | no-state-carrier | circumstance: capital | Capital accumulation state not present. | institutions/capital markets |
| `steamboat` | 0 | F | requires-authoring | circumstance: engine | Vague 'engine'. |  |
| `steamboat` | 1 | F | requires-authoring | circumstance: paddle wheel | Component description. |  |
| `screw_propeller` | 1 | F | requires-authoring | circumstance: shaft | Component description. |  |
| `iron_hull` | 0 | C | no-state-carrier | circumstance: iron plate | Iron plate not a shipped good. | non-shipped good: iron plate |
| `iron_hull` | 1 | F | requires-authoring | circumstance: rivets | Component description. |  |
| `macadam` | 1 | F | requires-authoring | circumstance: camber | Design feature, not a condition. |  |
| `bicycle` | 0 | C | institution-state-absent | institution present: Standard screw threads | names an institution's presence; no institutions system exists | institutions |
| `bicycle` | 1 | C | no-state-carrier | circumstance: steel tube | Steel tube not a shipped good. | non-shipped good: steel tube |
| `bicycle` | 2 | F | requires-authoring | circumstance: chain | Component description. |  |
| `chemistry_quantitative` | 0 | C | institution-state-absent | institution present: Scientific society and journal | names an institution's presence; no institutions system exists | institutions |
| `chemistry_quantitative` | 1 | F | requires-authoring | circumstance: balance | 'balance' is a single vague instrument, not a shipped good or tech id. |  |
| `chemistry_quantitative` | 2 | F | requires-authoring | circumstance: apparatus | 'apparatus' is vague, unauthored. |  |
| `sulphuric_acid` | 0 | C | no-state-carrier | circumstance: sulphur | Sulphur is a non-shipped mineral resource. | terrain/resource deposits |
| `sulphuric_acid` | 1 | C | no-state-carrier | circumstance: saltpetre | Saltpetre not a shipped good. | non-shipped good: saltpetre |
| `soda_leblanc` | 0 | C | no-state-carrier | circumstance: salt | Salt not a shipped good. | non-shipped good: salt |
| `soda_leblanc` | 1 | C | no-state-carrier | circumstance: limestone | Limestone deposit not tracked (stone is generic). | terrain/resource deposits |
| `bleach_chlorine` | 0 | C | no-state-carrier | circumstance: chlorine | Chlorine not a shipped good. | non-shipped good: chlorine |
| `bleach_chlorine` | 1 | C | no-state-carrier | circumstance: lime | Lime not a shipped good. | non-shipped good: lime |
| `synthetic_dye` | 0 | C | no-state-carrier | circumstance: coal tar | Coal tar not a shipped good. | non-shipped good: coal tar |
| `synthetic_dye` | 1 | F | requires-authoring | circumstance: laboratory | 'laboratory' is vague institutional setting. |  |
| `portland_cement` | 0 | C | no-state-carrier | circumstance: limestone | Limestone deposit not tracked. | terrain/resource deposits |
| `reinforced_concrete` | 0 | C | no-state-carrier | circumstance: cement | Cement not a shipped good. | non-shipped good: cement |
| `reinforced_concrete` | 1 | C | no-state-carrier | circumstance: steel bar | Steel bar not a shipped good. | non-shipped good: steel |
| `haber_bosch` | 0 | C | no-state-carrier | circumstance: high-pressure steel | High-pressure steel not a shipped good. | non-shipped good: steel |
| `haber_bosch` | 1 | F | requires-authoring | circumstance: catalyst | 'catalyst' is unspecified. |  |
| `dynamite` | 0 | C | no-state-carrier | circumstance: nitroglycerine | Not a shipped good. | non-shipped good: nitroglycerine |
| `dynamite` | 1 | C | no-state-carrier | circumstance: diatomaceous earth | Diatomaceous earth deposit not tracked. | terrain/resource deposits |
| `electromagnetism` | 0 | C | institution-state-absent | institution present: Scientific society and journal | names an institution's presence; no institutions system exists | institutions |
| `electromagnetism` | 1 | F | requires-authoring | circumstance: laboratory | 'laboratory' is vague. |  |
| `telegraph` | 0 | C | no-state-carrier | circumstance: wire | Wire not a shipped good. | non-shipped good: wire |
| `telegraph` | 1 | C | no-state-carrier | circumstance: batteries | Batteries not a shipped good. | non-shipped good: batteries |
| `submarine_cable` | 0 | C | no-state-carrier | circumstance: gutta-percha insulation | Not a shipped good. | non-shipped good: gutta-percha |
| `submarine_cable` | 1 | C | no-state-carrier | circumstance: armoured cable | Not a shipped good. | non-shipped good: armoured cable |
| `submarine_cable` | 2 | F | requires-authoring | circumstance: gutta-percha or equivalent insulating polymer | Disjunction with 'or equivalent' is ambiguous. |  |
| `dynamo` | 0 | C | no-state-carrier | circumstance: iron | Iron not a shipped good. | non-shipped good: iron |
| `dynamo` | 1 | C | no-state-carrier | circumstance: copper wire | Copper wire not shipped (only copper-ore). | non-shipped good: copper wire |
| `electric_light` | 0 | C | no-state-carrier | circumstance: carbon filament | Not a shipped good. | non-shipped good: carbon filament |
| `electric_light` | 1 | C | no-state-carrier | circumstance: vacuum bulb | Not a shipped good. | non-shipped good: glass/vacuum bulb |
| `electricity_generation` | 0 | C | no-state-carrier | circumstance: generators | Generator stock not tracked. | infrastructure: generators |
| `electricity_generation` | 1 | C | no-state-carrier | circumstance: transformers (AC) | Transformers not tracked. | infrastructure: transformers |
| `electric_motor` | 0 | F | requires-authoring | circumstance: motor | 'motor' is circular/vague. |  |
| `telephone` | 0 | F | requires-authoring | circumstance: transducer | 'transducer' is vague. |  |
| `telephone` | 1 | C | no-state-carrier | circumstance: wire network | Network state not tracked. | infrastructure: telegraph/wire network |
| `internal_combustion` | 0 | F | requires-authoring | circumstance: precision cylinders | 'precision cylinders' is a capability description. |  |
| `internal_combustion` | 1 | C | no-state-carrier | circumstance: refined fuel | Not a shipped good. | non-shipped good: refined fuel |
| `petroleum_refining` | 0 | C | no-state-carrier | circumstance: crude oil deposit | Crude oil deposit not tracked. | terrain/resource deposits |
| `petroleum_refining` | 1 | F | requires-authoring | circumstance: stills | 'stills' is vague equipment. |  |
| `vaccination` | 0 | C | no-state-carrier | circumstance: cowpox lymph | Cowpox presence not tracked. | fauna/disease: cowpox |
| `anaesthesia` | 0 | F | requires-authoring | circumstance: ether or chloroform | Disjunction of non-shipped chemicals; needs authoring. |  |
| `germ_theory` | 0 | C | institution-state-absent | institution present: Scientific society and journal | names an institution's presence; no institutions system exists | institutions |
| `germ_theory` | 1 | C | institution-state-absent | institution present: Hospital | names an institution's presence; no institutions system exists | institutions |
| `germ_theory` | 2 | D | implied-by-prerequisites | circumstance: microscope | implied by the node's own prerequisites: 'microscope' holds whenever germ_theory is available |  |
| `germ_theory` | 3 | F | requires-authoring | circumstance: culture media | 'culture media' is a practice/material description. |  |
| `antisepsis` | 0 | C | no-state-carrier | circumstance: carbolic acid | Not a shipped good. | non-shipped good: carbolic acid |
| `antisepsis` | 1 | F | requires-authoring | circumstance: sterilisation | 'sterilisation' is a practice. |  |
| `photography` | 0 | C | no-state-carrier | circumstance: silver salts | Not a shipped good. | non-shipped good: silver salts |
| `photography` | 1 | C | no-state-carrier | circumstance: lenses | Not a shipped good. | non-shipped good: lenses |
| `evolution` | 0 | C | institution-state-absent | institution present: Scientific society and journal | names an institution's presence; no institutions system exists | institutions |
| `evolution` | 1 | C | no-state-carrier | circumstance: specimens from global voyages | Global exploration state not tracked. | contact/diffusion: global voyages |
| `aircraft` | 0 | C | no-state-carrier | circumstance: light engine | Single non-shipped manufactured good. | non-shipped good: light engines |
| `aircraft` | 1 | C | no-state-carrier | circumstance: spruce and fabric | Specific materials not among shipped goods; compound. | non-shipped good: spruce/fabric |
| `aircraft_metal` | 0 | C | no-state-carrier | circumstance: duralumin | Alloy not shipped. | non-shipped good: duralumin |
| `aircraft_metal` | 1 | F | requires-authoring | circumstance: riveting | Practice description, not a state condition. |  |
| `jet_engine` | 0 | C | no-state-carrier | circumstance: heat-resistant alloys | Material class not shipped. | non-shipped good: superalloys |
| `jet_engine` | 1 | F | requires-authoring | circumstance: compressor | Component, vague as a condition. |  |
| `steam_turbine` | 0 | F | requires-authoring | circumstance: precision blading | Manufacturing capability description, unquantified. |  |
| `steam_turbine` | 1 | D | implied-by-prerequisites | circumstance: high-pressure steam | implied by the node's own prerequisites: 'steam_high_pressure' holds whenever steam_turbine is available |  |
| `automobile_mass` | 0 | C | no-state-carrier | circumstance: steel | Steel is not a shipped good. | non-shipped good: steel |
| `automobile_mass` | 1 | F | requires-authoring | circumstance: assembly line | Production practice, not a stock. |  |
| `tank` | 0 | C | no-state-carrier | circumstance: armour plate | Not shipped. | non-shipped good: armour plate |
| `tank` | 1 | F | requires-authoring | circumstance: engine | Generic, unspecified engine type. |  |
| `diesel` | 0 | F | requires-authoring | circumstance: high-pressure cylinders | Engineering capability, unquantified. |  |
| `aluminium` | 0 | C | no-state-carrier | circumstance: bauxite | Bauxite ore deposit. | terrain/resource deposits |
| `aluminium` | 1 | C | no-state-carrier | circumstance: cryolite | Cryolite mineral deposit. | terrain/resource deposits |
| `superalloy` | 0 | C | no-state-carrier | circumstance: nickel | Nickel not shipped. | terrain/resource deposits |
| `superalloy` | 1 | C | no-state-carrier | circumstance: chromium | Chromium not shipped. | terrain/resource deposits |
| `rocket` | 0 | C | no-state-carrier | circumstance: liquid oxygen | Not shipped. | non-shipped good: liquid oxygen |
| `rocket` | 1 | F | requires-authoring | circumstance: pumps | Generic component. |  |
| `satellite` | 0 | F | requires-authoring | circumstance: launcher | Vague; could map to rocket tech but not stated as id. |  |
| `satellite` | 1 | F | requires-authoring | circumstance: electronics | Vague category. |  |
| `container_shipping` | 0 | C | institution-state-absent | institution present: Standard screw threads | names an institution's presence; no institutions system exists | institutions |
| `container_shipping` | 1 | F | requires-authoring | circumstance: standard container | Standard is an institutional practice. |  |
| `container_shipping` | 2 | C | no-state-carrier | circumstance: cranes | Port equipment not tracked. | non-shipped good: cranes |
| `radio` | 0 | C | no-state-carrier | circumstance: spark transmitter | Not shipped. | non-shipped good: spark transmitter |
| `radio` | 1 | F | requires-authoring | circumstance: antenna | Generic component. |  |
| `vacuum_tube` | 0 | F | requires-authoring | circumstance: vacuum bulb | Component/capability, vague. |  |
| `vacuum_tube` | 1 | F | requires-authoring | circumstance: filament | Component, vague. |  |
| `radar` | 0 | F | requires-authoring | circumstance: transmitter | Generic component. |  |
| `radar` | 1 | F | requires-authoring | circumstance: antenna | Generic component. |  |
| `television` | 0 | F | requires-authoring | circumstance: cathode-ray tube | Component, not a state. |  |
| `television` | 1 | F | requires-authoring | circumstance: camera tube | Component, not a state. |  |
| `computer` | 0 | F | requires-authoring | circumstance: thousands of tubes | Unstated quantity of non-tracked component. |  |
| `computer` | 1 | F | requires-authoring | circumstance: memory | Vague. |  |
| `transistor` | 0 | C | no-state-carrier | circumstance: germanium then silicon | Semiconductor materials not shipped; compound sequence. | non-shipped good: germanium/silicon |
| `transistor` | 1 | F | requires-authoring | circumstance: purification | Process description. |  |
| `integrated_circuit` | 0 | C | no-state-carrier | circumstance: silicon wafer | Not shipped. | non-shipped good: silicon wafer |
| `integrated_circuit` | 1 | F | requires-authoring | circumstance: photolithography | Process/technique. |  |
| `microprocessor` | 0 | F | requires-authoring | circumstance: IC fab | Facility, unclear mapping. |  |
| `internet` | 0 | F | requires-authoring | circumstance: computers | Unquantified; not a good or exact id. |  |
| `internet` | 1 | C | no-state-carrier | circumstance: telecom lines | Telecom infrastructure not modeled. | trade network |
| `quantum_mechanics` | 0 | C | institution-state-absent | institution present: Scientific society and journal | names an institution's presence; no institutions system exists | institutions |
| `quantum_mechanics` | 1 | F | requires-authoring | circumstance: none | 'none' is not a condition. |  |
| `nuclear_fission` | 0 | C | no-state-carrier | circumstance: uranium | Uranium deposit. | terrain/resource deposits |
| `nuclear_fission` | 1 | F | requires-authoring | circumstance: graphite or heavy water | Disjunction of non-shipped materials. |  |
| `nuclear_power` | 0 | F | requires-authoring | circumstance: reactor | Generic, not a stock or exact id. |  |
| `nuclear_power` | 1 | F | requires-authoring | circumstance: turbine | Generic; steam_turbine not named exactly. |  |
| `thermonuclear` | 0 | F | requires-authoring | circumstance: fission primary | Component description. |  |
| `thermonuclear` | 1 | C | no-state-carrier | circumstance: deuterium/tritium | Isotopes not shipped. | non-shipped good: deuterium/tritium |
| `antibiotic_penicillin` | 0 | C | no-state-carrier | circumstance: Penicillium | Mould organism presence. | fauna/flora range |
| `antibiotic_penicillin` | 1 | F | requires-authoring | circumstance: deep-tank fermentation | Process practice. |  |
| `antibiotic_broad` | 0 | C | no-state-carrier | circumstance: soil actinomycetes | Soil organisms. | fauna/flora range |
| `antibiotic_broad` | 1 | C | no-state-carrier | circumstance: screening programme | Research programme. | institutions |
| `antibiotic_resistance` | 0 | C | no-state-carrier | circumstance: surveillance | Surveillance system. | institutions |
| `antibiotic_resistance` | 1 | C | no-state-carrier | circumstance: new-drug pipeline | Drug development pipeline. | institutions |
| `vaccine_lab` | 0 | F | requires-authoring | circumstance: culture | Vague practice. |  |
| `vaccine_lab` | 1 | F | requires-authoring | circumstance: attenuation or inactivation | Disjunctive method description. |  |
| `blood_transfusion` | 0 | F | requires-authoring | circumstance: typing | Knowledge/practice. |  |
| `blood_transfusion` | 1 | C | no-state-carrier | circumstance: anticoagulant | Chemical not shipped. | non-shipped good: anticoagulant |
| `refrigeration` | 0 | F | requires-authoring | circumstance: compressor | Generic component. |  |
| `refrigeration` | 1 | C | no-state-carrier | circumstance: refrigerant | Not shipped. | non-shipped good: refrigerant |
| `canning` | 0 | C | no-state-carrier | circumstance: tinplate | Not shipped (tin-ore differs). | non-shipped good: tinplate |
| `canning` | 1 | F | requires-authoring | circumstance: sealing | Practice. |  |
| `green_revolution` | 0 | F | requires-authoring | circumstance: dwarf seed | Product of the node itself; circular. |  |
| `green_revolution` | 1 | C | no-state-carrier | circumstance: fertiliser | Not shipped. | non-shipped good: fertiliser |
| `genetics_mendel` | 0 | C | institution-state-absent | institution present: Vital statistics | names an institution's presence; no institutions system exists | institutions |
| `genetics_mendel` | 1 | F | requires-authoring | circumstance: breeding experiments | Practice description. |  |
| `dna_structure` | 0 | F | requires-authoring | circumstance: X-ray crystallography | Technique/knowledge, not a state. |  |
| `genetic_engineering` | 0 | F | requires-authoring | circumstance: restriction enzymes | Knowledge/reagent, unmodeled and vague. |  |
| `genetic_engineering` | 1 | F | requires-authoring | circumstance: plasmids | Biological tool, vague. |  |
| `hormonal_contraception` | 0 | C | no-state-carrier | circumstance: synthetic hormones | Not shipped. | non-shipped good: synthetic hormones |
| `xray` | 0 | F | requires-authoring | circumstance: tube | Vague component. |  |
| `xray` | 1 | F | requires-authoring | circumstance: plates | Vague component. |  |
| `plastics` | 0 | C | no-state-carrier | circumstance: petrochemical feedstock | Petroleum feedstock. | terrain/resource deposits |
| `plastics` | 1 | F | requires-authoring | circumstance: catalysts | Vague. |  |
| `electric_traction` | 0 | F | requires-authoring | circumstance: motors | Generic component. |  |
| `electric_traction` | 1 | F | requires-authoring | circumstance: third rail or catenary | Disjunction of infrastructure. |  |
| `skyscraper` | 0 | C | no-state-carrier | circumstance: structural steel | Not shipped. | non-shipped good: structural steel |
| `skyscraper` | 1 | F | requires-authoring | circumstance: safety elevator | Component, not exact id. |  |
| `hydroelectric` | 0 | C | no-state-carrier | circumstance: dam | River site/structure not modeled. | terrain/resource deposits |
| `hydroelectric` | 1 | F | requires-authoring | circumstance: turbines | Generic component. |  |
| `mechanised_agriculture` | 0 | F | requires-authoring | circumstance: tractor | Names the invention itself; not a stateable condition. |  |
| `mechanised_agriculture` | 1 | C | no-state-carrier | circumstance: fuel | Fuel supply not a shipped good. | terrain/resource deposits: petroleum fuel |
| `numerical_control` | 0 | F | requires-authoring | circumstance: servo | Component name, not a condition. |  |
| `numerical_control` | 1 | F | requires-authoring | circumstance: controller | Component name, not a condition. |  |
| `mobile_phone` | 0 | C | no-state-carrier | circumstance: cell network | Network coverage state does not exist. | communication infrastructure network |
| `mobile_phone` | 1 | F | requires-authoring | circumstance: handsets | Device name, unstated quantity. |  |
| `gps` | 0 | C | no-state-carrier | circumstance: satellite constellation | Satellite constellation state not modelled. | orbital infrastructure |
| `gps` | 1 | F | requires-authoring | circumstance: receivers | Device name, vague. |  |
| `atomic_clock` | 0 | C | no-state-carrier | circumstance: caesium | Caesium is not a shipped good. | non-shipped good: caesium |
| `atomic_clock` | 1 | F | requires-authoring | circumstance: microwave cavity | Component name, not a condition. |  |
| `solar_pv` | 0 | C | no-state-carrier | circumstance: silicon | Silicon not a shipped good. | non-shipped good: silicon |
| `lithium_battery` | 0 | C | no-state-carrier | circumstance: lithium | Lithium not a shipped good. | non-shipped good: lithium |
| `lithium_battery` | 1 | C | no-state-carrier | circumstance: cobalt | Cobalt not a shipped good. | non-shipped good: cobalt |
| `relativity` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `relativity` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `laser` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `laser` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `fibre_optics` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `fibre_optics` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `cmos_vlsi` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `cmos_vlsi` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `superconductivity_applied` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `superconductivity_applied` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `composites` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `composites` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `gas_turbine_power` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `gas_turbine_power` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `wind_turbine_modern` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `wind_turbine_modern` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `nuclear_fusion_research` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `nuclear_fusion_research` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `grid_storage` | 0 | C | no-state-carrier | circumstance: intermittent generation on the grid | Grid generation mix state does not exist. | electric grid/energy system |
| `grid_storage` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `grid_storage` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `smart_grid` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `smart_grid` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `programming_languages` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `programming_languages` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `operating_system` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `operating_system` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `relational_database` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `relational_database` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `personal_computer` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `personal_computer` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `public_key_crypto` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `public_key_crypto` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `gpu_parallel` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `gpu_parallel` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `machine_learning` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `machine_learning` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `deep_learning` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `deep_learning` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `large_language_models` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `large_language_models` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `cloud_computing` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `cloud_computing` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `smartphone` | 0 | C | no-state-carrier | circumstance: mobile network coverage | Coverage state not modelled. | communication infrastructure network |
| `smartphone` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `smartphone` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `cyber_warfare` | 0 | C | no-state-carrier | circumstance: rival networked infrastructure | Rival infrastructure state does not exist. | military/battle with contact/diffusion |
| `cyber_warfare` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `cyber_warfare` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `quantum_computing` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `quantum_computing` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `cellular_digital` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `cellular_digital` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `crewed_spaceflight` | 0 | C | no-state-carrier | circumstance: space rivalry | Space rivalry state does not exist. | institutions/diplomacy rivalry |
| `crewed_spaceflight` | 1 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `crewed_spaceflight` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `lunar_flight` | 0 | C | no-state-carrier | circumstance: space rivalry | Space rivalry state does not exist. | institutions/diplomacy rivalry |
| `lunar_flight` | 1 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `lunar_flight` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `space_station` | 0 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `space_station` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `space_probe` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `space_probe` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `reusable_launch` | 0 | C | no-state-carrier | circumstance: high launch cost limiting orbital activity | Launch cost and orbital activity state absent. | orbital infrastructure/space economy |
| `reusable_launch` | 1 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `reusable_launch` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `space_telescope` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `space_telescope` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `earth_observation` | 0 | F | requires-authoring | circumstance: need to monitor distant territory | Vague need, no measurable quantity. |  |
| `earth_observation` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `earth_observation` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `organ_transplant` | 0 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `organ_transplant` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `chemotherapy` | 0 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `chemotherapy` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `medical_imaging` | 0 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `medical_imaging` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `antiviral_drugs` | 0 | C | no-state-carrier | circumstance: epidemic viral disease | Epidemic state not modelled. | disease/epidemics |
| `antiviral_drugs` | 1 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `antiviral_drugs` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `monoclonal_antibodies` | 0 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `monoclonal_antibodies` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `pcr` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `pcr` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `genome_sequencing` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `genome_sequencing` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `ivf` | 0 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `ivf` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `crispr` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `crispr` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `mrna_vaccines` | 0 | C | no-state-carrier | circumstance: pandemic | Pandemic state not modelled. | disease/epidemics |
| `mrna_vaccines` | 1 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `mrna_vaccines` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `oral_rehydration` | 0 | C | no-state-carrier | circumstance: endemic diarrhoeal disease | Endemic disease state not modelled. | disease/epidemics |
| `oral_rehydration` | 1 | C | institution-state-absent | institution present: hospital or research laboratory | names an institution's presence; no institutions system exists | institutions |
| `oral_rehydration` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `drip_irrigation` | 0 | C | no-state-carrier | circumstance: water scarcity on farmland | Water scarcity state not modelled. | climate/water resources |
| `drip_irrigation` | 1 | C | institution-state-absent | institution present: research laboratory (agricultural) | names an institution's presence; no institutions system exists | institutions |
| `drip_irrigation` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `precision_agriculture` | 0 | C | no-state-carrier | circumstance: input costs limiting farm margins | Input cost margins not modelled. | farm economics/prices |
| `precision_agriculture` | 1 | C | institution-state-absent | institution present: research laboratory (agricultural) | names an institution's presence; no institutions system exists | institutions |
| `precision_agriculture` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `desalination` | 0 | C | no-state-carrier | circumstance: arid coastal settlement | Arid coastal settlement attributes not modelled. | climate/terrain (aridity, coast) |
| `desalination` | 1 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `desalination` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `catalytic_converter` | 0 | C | no-state-carrier | circumstance: urban air pollution | Air pollution state not modelled. | pollution/environment |
| `catalytic_converter` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `catalytic_converter` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `weather_prediction` | 0 | C | no-state-carrier | circumstance: weather-driven harvest failure | Weather-driven harvest failure needs climate system. | climate |
| `weather_prediction` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `weather_prediction` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `carbon_capture` | 0 | C | no-state-carrier | circumstance: rising atmospheric CO₂ | Atmospheric CO2 not modelled. | climate |
| `carbon_capture` | 1 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `carbon_capture` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `high_speed_rail` | 0 | C | no-state-carrier | circumstance: dense intercity corridor | Intercity corridor density not modelled. | population/urban scale and transport network |
| `high_speed_rail` | 1 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `high_speed_rail` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `jet_airliner` | 0 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `jet_airliner` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `electric_vehicle` | 0 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `electric_vehicle` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `helicopter` | 0 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `helicopter` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `icbm` | 0 | C | no-state-carrier | circumstance: a rival holds thermonuclear weapons | Rival tech holdings not a state carrier. | diplomacy/rival tech knowledge |
| `icbm` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `icbm` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `nuclear_submarine` | 0 | C | no-state-carrier | circumstance: naval rivalry | Naval rivalry not modelled. | military/naval rivalry |
| `nuclear_submarine` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `nuclear_submarine` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `guided_munitions` | 0 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `guided_munitions` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `stealth` | 0 | C | no-state-carrier | circumstance: rival integrated air defence | Rival air defence not modelled. | military/battle |
| `stealth` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `stealth` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `combat_drone` | 0 | C | no-state-carrier | circumstance: sustained conflict | Sustained conflict duration not a state carrier. | military/battle |
| `combat_drone` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `combat_drone` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `missile_defence` | 0 | C | no-state-carrier | circumstance: a rival fields ballistic missiles | Rival fielded weapons not modelled. | diplomacy/rival tech knowledge |
| `missile_defence` | 1 | C | institution-state-absent | institution present: research laboratory (military) | names an institution's presence; no institutions system exists | institutions |
| `missile_defence` | 2 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `additive_manufacturing` | 0 | C | institution-state-absent | institution present: university | names an institution's presence; no institutions system exists | institutions |
| `additive_manufacturing` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |
| `nanotechnology` | 0 | C | institution-state-absent | institution present: research laboratory | names an institution's presence; no institutions system exists | institutions |
| `nanotechnology` | 1 | C | contact-state-absent | contact with a civilization holding this | names contact with a civilization holding the knowledge; no contact state exists (D-035-C carrier test) | contact / foreign knowledge |

## Eureka mapping (every evaluable Eureka)

| node | corpus text | condition |
|---|---|---|
| `knapping_oldowan` | circumstance: knappable stone — quartzite, basalt, chert | `stock_stone > 0` |
| `knapping_levallois` | circumstance: fine-grained knappable stone | `stock_stone > 0` |
| `knapping_blade` | circumstance: fine-grained stone, ideally obsidian or flint | `stock_stone > 0` |
| `ground_stone_early` | circumstance: hard fine-grained stone | `stock_stone > 0` |
| `cordage` | circumstance: bast fibre, sinew, hair, or gut | `stock_fiber > 0` |
| `atlatl` | circumstance: antler or wood thrower | `stock_timber > 0` |
| `bow_simple` | circumstance: flexible wood (yew, elm, ash) | `stock_timber > 0` |
| `bow_simple` | circumstance: sinew or fibre string | `stock_fiber > 0` |
| `sling` | circumstance: smooth stones | `stock_stone > 0` |
| `shelter_hut` | circumstance: mammoth bone or timber | `stock_timber > 0` |
| `shelter_hut` | circumstance: hides | `stock_hides > 0` |
| `hide_working` | circumstance: hides | `stock_hides > 0` |
| `raft` | circumstance: logs or reed bundles | `stock_timber > 0` |
| `dugout` | circumstance: large straight log | `stock_timber > 0` |
| `grinding_stone` | circumstance: coarse stone slab | `stock_stone > 0` |
| `lamp` | circumstance: stone bowl | `stock_stone > 0` |
| `digging_stick_hoe` | circumstance: stone or bone blade | `stock_stone > 0` |
| `pottery_open_fired` | circumstance: levigated clay | `stock_clay > 0` |
| `kiln_updraft` | circumstance: clay | `stock_clay > 0` |
| `timber_frame` | circumstance: felled timber | `stock_timber > 0` |
| `stone_dry` | circumstance: workable stone in catchment | `stock_stone > 0` |
| `well` | circumstance: digging tools | `stock_tools > 0` |
| `well` | circumstance: stone or timber lining | `stock_stone > 0 OR stock_timber > 0` |
| `fermentation_grain` | circumstance: grain | `stock_grain > 0` |
| `spinning_spindle` | circumstance: clay or stone whorl | `stock_clay > 0 OR stock_stone > 0` |
| `copper_native` | circumstance: native copper deposit | `stock_copper_ore > 0` |
| `copper_smelting` | circumstance: copper ore (elevation channel) | `stock_copper_ore > 0` |
| `copper_smelting` | circumstance: charcoal | `charcoal` |
| `casting_open` | circumstance: carved stone mould | `stock_stone > 0` |
| `terrace` | circumstance: stone or earth retaining walls | `stock_stone > 0` |
| `sledge` | circumstance: timber runners | `stock_timber > 0` |
| `wheel_solid` | circumstance: planked timber | `stock_timber > 0` |
| `track_road` | circumstance: timber | `stock_timber > 0` |
| `plough_ard` | circumstance: timber ard | `stock_timber > 0` |
| `token_counting` | circumstance: clay | `stock_clay > 0` |
| `stamp_seal` | circumstance: carved stone | `stock_stone > 0` |
| `stamp_seal` | circumstance: clay sealings | `stock_clay > 0` |
| `arsenical_bronze` | circumstance: arsenical copper ore (fahlore) | `stock_copper_ore > 0` |
| `tin_bronze` | circumstance: copper | `stock_copper_ore > 0` |
| `tin_bronze` | circumstance: TIN — from Cornwall, Erzgebirge, Afghanistan, Anatolian Taurus, Southeast Asia | `stock_tin_ore > 0` |
| `casting_closed` | circumstance: stone or clay bivalve mould | `stock_stone > 0 OR stock_clay > 0` |
| `casting_closed` | circumstance: bronze | `stock_bronze > 0` |
| `lost_wax` | circumstance: fine clay | `stock_clay > 0` |
| `sheet_metal` | circumstance: bronze | `stock_bronze > 0` |
| `mining_shaft` | circumstance: picks (antler, bronze) | `stock_bronze > 0` |
| `proto_writing` | circumstance: clay tablet | `stock_clay > 0` |
| `cuneiform` | circumstance: clay | `stock_clay > 0` |
| `chinese_script` | circumstance: bronze stylus | `stock_bronze > 0` |
| `plank_boat` | circumstance: planked timber | `stock_timber > 0` |
| `wheel_spoked` | circumstance: bent timber | `stock_timber > 0` |
| `wheel_spoked` | circumstance: bronze fittings | `stock_bronze > 0` |
| `fired_brick` | circumstance: clay | `stock_clay > 0` |
| `standard_weights` | circumstance: stone weights | `stock_stone > 0` |
| `beekeeping` | circumstance: clay or straw hives | `stock_clay > 0` |
| `bellows` | circumstance: tuyère (clay nozzle) | `stock_clay > 0` |
| `brahmi` | circumstance: stone, birch bark, palm leaf | `stock_stone > 0` |
| `coinage_electrum` | circumstance: precious metal or bronze | `stock_bronze > 0` |
| `siege_ram` | circumstance: timber frame | `stock_timber > 0` |
| `siege_ram` | circumstance: hide covering | `stock_hides > 0` |
| `siege_tower` | circumstance: timber | `stock_timber > 0` |
| `stone_fortification` | circumstance: quarried stone | `stock_stone > 0` |
| `naval_ram` | circumstance: bronze ram | `stock_bronze > 0` |
| `aqueduct_channel` | circumstance: stone | `stock_stone > 0` |
| `arch` | circumstance: bricks or cut stone | `stock_stone > 0` |
| `rotary_quern` | circumstance: two dressed stones | `stock_stone > 0` |
| `torsion_artillery` | circumstance: timber frame | `stock_timber > 0` |
| `water_screw` | circumstance: timber | `stock_timber > 0` |
| `gearing` | circumstance: bronze | `stock_bronze > 0` |
| `road_paved` | circumstance: stone | `stock_stone > 0` |
| `bridge_stone` | circumstance: cut stone | `stock_stone > 0` |
| `aqueduct_arcade` | circumstance: stone | `stock_stone > 0` |
| `paper` | circumstance: bast fibre, rags, bark | `stock_fiber > 0` |
| `crossbow_repeating` | circumstance: wood | `stock_timber > 0` |
| `mortise_hull` | circumstance: timber | `stock_timber > 0` |
| `surgery_instruments` | circumstance: bronze | `stock_bronze > 0` |
| `distillation` | circumstance: glass or clay still | `stock_clay > 0` |
| `horse_collar` | circumstance: wood | `stock_timber > 0` |
| `windmill_vertical` | circumstance: timber | `stock_timber > 0` |
| `windmill_post` | circumstance: timber | `stock_timber > 0` |
| `windmill_post` | circumstance: gearing | `gearing` |
| `horizontal_loom` | circumstance: timber frame | `stock_timber > 0` |
| `stern_rudder` | circumstance: timber | `stock_timber > 0` |
| `watertight_bulkhead` | circumstance: timber | `stock_timber > 0` |
| `cannon_early` | circumstance: cast bronze or iron | `stock_bronze > 0` |
| `trebuchet` | circumstance: timber | `stock_timber > 0` |
| `movable_type_ceramic` | circumstance: clay type | `stock_clay > 0` |
| `movable_type_metal` | circumstance: bronze | `stock_bronze > 0` |
| `canal_lock` | circumstance: timber gates | `stock_timber > 0` |
| `caravel` | circumstance: timber | `stock_timber > 0` |
| `brass_calamine` | circumstance: copper | `stock_copper_ore > 0` |
| `screw_propeller` | circumstance: iron or bronze screw | `stock_bronze > 0` |
| `macadam` | circumstance: crushed stone | `stock_stone > 0` |
| `portland_cement` | circumstance: clay | `stock_clay > 0` |

