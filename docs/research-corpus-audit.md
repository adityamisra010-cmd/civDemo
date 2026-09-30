# Research corpus audit — tech-graph-v0.6 → research.json

**GENERATED** by `scripts/migrate-research-corpus.py` (do not edit by hand). Corpus SHA-256 `e1251b0987a50db9252953a0a6af89594befa7bdf6dfb82361ba180d193ca854`. Contract: `docs/adr/adr-029-research-engine.md`; rulings: D-044.

## Summary

- Technology nodes integrated: **424** (every corpus technology; none dropped).
- Civics nodes integrated: **6** (the architecture §5.7 candidates).
- Tree 1 placement (rule R1, ADR-029 §8): trunk 176, military 37, medicine 22, engineering 130, natural_science 44, agriculture 15
- Eureka strings: 801; machine-evaluable 93; contact-state-absent 61, evaluable 93, implied-by-prerequisites 21, institution-state-absent 79, no-state-carrier 547
- Technology nodes with at least one evaluable Eureka: 83
- Research stage trigger: `(medicine_hippocratic AND (geometry_axiomatic OR algebra)) AND (cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper) AND (((cuneiform AND stamp_seal) AND cuneiform) AND legal_code_roman)`
- Stage-trigger prerequisite closure: 32 nodes — 30 technologies (all forced into the trunk) and 2 civics
- Registry entities: 129; with unresolved corpus references: `inst.newspaper` (postal_imperial)
- Frontier (age F) nodes normalized to A9 + frontier: 16
- Repeatable descriptors kept as data (levels not implemented): 10

## Corpus problems found (none repaired silently)

1. **No research cost in the corpus.** BaseCost is derived from prerequisite depth (TUNE, chosen).
2. **Eurekas are prose.** Only faithful mappings are machine-evaluable; the rest keep their text with a status.
3. **`inst.newspaper` references `postal_imperial`,** which is not a technology, civic or institution (it survives only as a `reclass` entry). Declared unresolved; `inst.newspaper`, and everything that requires it, is never knowledge-eligible.
4. **Age `F`** (16 nodes) is not one of the nine Ages; normalized to A9 with `frontier: true` (architecture §17.5).
5. **`research.repeatable`** on 10 frontier nodes conflicts with idempotent completion; the nodes complete once (D-044 Part D T6).
6. **Generation numbers are display lineage, not a dependency rule.** 13 nodes break 'gen N requires gen N-1'; not validated (D-044 R7).
7. **No Civics nodes existed.** The six architecture §5.7 candidates were reclassified; no Civics content was invented.
8. **`effects.immediate` is empty on every node.** No immediate-effect kind is ratified (law 2), so the loader requires it empty.
9. **21 Eureka circumstances name knowledge the node's own prerequisites already guarantee** (e.g. "circumstance: fire" on a node that requires fire_making). Evaluated, each would fire the moment the node became available — a flat cost cut, not a circumstance. Declared `implied-by-prerequisites`, not evaluated (ADR-029 §7): `heat_treatment_stone` (fire_making), `microlith` (adhesive_natural), `birch_tar` (fire_making), `sling` (cordage), `raft` (cordage), `dugout` (fire_making), `nixtamalization` (maize), `kiln_updraft` (mudbrick), `sledge` (cordage), `steel_carburized` (charcoal), `cast_iron` (charcoal), `iron_mouldboard` (cast_iron), `woodblock_print` (paper), `paper_money` (paper), `double_entry` (paper), `bill_of_exchange` (paper), `printing_press` (abjad OR alphabet_vowels), `cannon_cast_iron` (cast_iron), `steam_rotary` (gearing), `germ_theory` (microscope), `steam_turbine` (steam_high_pressure).

## Per-node audit

Columns: key · id · tree/branch · primary domain · age · depth · BaseCost · prerequisites · Eurekas (evaluable/total) · family/gen · entity unlocks · capabilities · emerged

| key | id | tree / branch | domain | age | depth | cost | prerequisites | eureka | family / gen | entities | caps | emerged |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `fire_making` | technology / main | energy | A1 | 0 | 1000 |  | 0/2 | fire / None | 0 | 2 | ~400 kya habitual; definite by ~50 kya |
| 2 | `knapping_oldowan` | technology / main | materials | A1 | 0 | 1000 |  | 1/1 | knapping / None | 0 | 3 | ~2.6 Mya (Gona); Lomekwi ~3.3 Mya |
| 3 | `knapping_levallois` | technology / main | materials | A1 | 0 | 1000 |  | 1/1 | knapping / None | 0 | 1 | ~300 kya |
| 4 | `knapping_blade` | technology / main | materials | A1 | 1 | 1120 | knapping_levallois | 1/1 | knapping / None | 0 | 4 | ~40 kya Upper Palaeolithic; earlier in Africa (~70 kya) |
| 5 | `pressure_flaking` | technology / main | materials | A1 | 0 | 1000 |  | 0/2 | knapping / None | 0 | 2 | ~75 kya Blombos (heat-treated silcrete); widespread ~20 kya |
| 6 | `heat_treatment_stone` | technology / main | materials | A1 | 1 | 1120 | fire_making AND knapping_levallois | 0/2 |  | 0 | 1 | ~164 kya Pinnacle Point |
| 7 | `microlith` | technology / main | materials | A1 | 2 | 1250 | hafting | 0/2 |  | 0 | 3 | ~65 kya Howiesons Poort; Mesolithic Europe |
| 8 | `ground_stone_early` | technology / main | materials | A1 | 1 | 1120 | knapping_oldowan | 1/2 | ground_stone / None | 1 | 2 | ~35 kya Japan, ~44 kya Australia |
| 9 | `adhesive_natural` | technology / main | materials | A1 | 0 | 1000 |  | 0/1 | adhesive / None | 0 | 1 | ~200 kya (bitumen, Umm el Tlel) |
| 10 | `birch_tar` | technology / main | materials | A1 | 1 | 1120 | fire_making AND adhesive_natural | 0/2 | adhesive / None | 0 | 2 | ~200 kya Campitello (Neanderthal); ~50 kya widespread |
| 11 | `hafting` | technology / main | materials | A1 | 1 | 1120 | cordage AND adhesive_natural | 0/2 |  | 1 | 3 | ~500 kya Kathu Pan (contested); secure ~200 kya |
| 12 | `cordage` | technology / main | industry | A1 | 0 | 1000 |  | 1/1 | fibre / None | 0 | 4 | ~50 kya Abri du Maras (direct); older inferred |
| 13 | `basketry` | technology / main | industry | A1 | 1 | 1120 | cordage | 0/1 | fibre / None | 1 | 4 | ~27 kya Pavlov impressions; ~10 kya Guitarrero Cave |
| 14 | `atlatl` | technology / main | military | A1 | 2 | 1250 | hafting | 1/2 | projectile / None | 0 | 1 | ~30 kya Upper Palaeolithic Europe |
| 15 | `bow_simple` | technology / main | military | A1 | 1 | 1120 | cordage | 2/2 | bow / None | 1 | 2 | ~64 kya Sibudu (inferred); ~10 kya Stellmoor (preserved) |
| 16 | `sling` | technology / main | military | A1 | 1 | 1120 | cordage | 1/2 | projectile / None | 1 | 2 | ~10 kya inferred; preserved ~2,500 BCE |
| 17 | `shelter_hut` | technology / main | construction | A1 | 1 | 1120 | cordage | 2/2 | shelter / None | 0 | 2 | ~25 kya Mezhirich, Dolní Věstonice |
| 18 | `hide_working` | technology / main | industry | A1 | 1 | 1120 | knapping_levallois | 1/2 | hide / None | 0 | 3 | ~100 kya (scrapers, ochre) |
| 19 | `sewing` | technology / main | industry | A1 | 2 | 1250 | hide_working AND cordage | 0/2 | hide / None | 0 | 3 | ~50 kya Denisova; ~30 kya widespread |
| 20 | `raft` | technology / main | transport | A1 | 1 | 1120 | cordage | 1/2 | watercraft / None | 0 | 2 | inferred ≥50 kya (Sahul crossing); Flores ~1 Mya (contested) |
| 21 | `dugout` | technology / main | transport | A1 | 2 | 1250 | fire_making AND ground_stone_early | 1/2 | watercraft / None | 0 | 2 | ~8,000 BCE Pesse (oldest preserved); older inferred |
| 22 | `fishing_hook` | technology / main | agriculture | A1 | 1 | 1120 | cordage | 0/2 | fishing / None | 1 | 1 | ~42 kya Jerimalai (Timor) |
| 23 | `grinding_stone` | technology / main | agriculture | A1 | 0 | 1000 |  | 1/2 |  | 0 | 2 | ~30 kya Bilancino; routine in Natufian ~12 kya |
| 24 | `dog_domestication` | technology / main | science | A1 | 0 | 1000 |  | 0/1 | domestication / None | 0 | 3 | ~15–30 kya |
| 25 | `ochre_processing` | technology / main | science | A1 | 1 | 1120 | grinding_stone | 0/1 |  | 0 | 2 | ~100 kya Blombos |
| 26 | `bone_tools` | technology / main | materials | A1 | 2 | 1250 | knapping_blade | 0/2 |  | 0 | 4 | ~90 kya Katanda harpoons; ~40 kya widespread |
| 27 | `lamp` | technology / main | energy | A1 | 1 | 1120 | fire_making | 1/2 | lighting / None | 0 | 2 | ~17 kya Lascaux |
| 28 | `tally_notation` | technology / main | science | A1 | 3 | 1400 | bone_tools | 0/2 |  | 0 | 2 | ~44 kya Lebombo; ~20 kya Ishango |
| 29 | `cereal_cultivation` | technology / main | agriculture | A2 | 1 | 1120 | grinding_stone | 0/2 | cereal / None | 1 | 2 | ~11,500 BCE (PPNA) |
| 30 | `cereal_domesticated` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/1 | cereal / None | 0 | 2 | ~9,500 BCE Levant (einkorn, emmer, barley) |
| 31 | `rice_wet` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/2 | cereal / None | 1 | 2 | ~7,000 BCE Yangtze (Kuahuqiao); domesticated ~6,000 BCE |
| 32 | `millet` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/2 | cereal / None | 1 | 1 | ~8,000 BCE Cishan |
| 33 | `maize` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/1 | cereal / None | 1 | 2 | ~7,000 BCE Balsas; staple by ~2,000 BCE |
| 34 | `sorghum_pearl_millet` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/1 | cereal / None | 1 | 1 | ~3,000 BCE Sahel (later than others) |
| 35 | `root_crop` | technology / main | agriculture | A2 | 0 | 1000 |  | 0/2 | tuber / None | 1 | 2 | ~7,000 BCE Kuk Swamp (taro); ~8,000 BCE Andes (potato) |
| 36 | `legume` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/1 |  | 0 | 2 | ~9,500 BCE Levant; beans ~7,000 BCE Andes/Mesoamerica |
| 37 | `orchard` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/2 |  | 0 | 3 | ~4,000 BCE Levant (olive); fig possibly ~9,000 BCE |
| 38 | `sickle` | technology / main | agriculture | A2 | 3 | 1400 | microlith | 0/2 |  | 0 | 1 | ~12,500 BCE Natufian |
| 39 | `digging_stick_hoe` | technology / main | agriculture | A2 | 2 | 1250 | hafting AND ground_stone_early | 1/2 | tillage / None | 0 | 3 | ~8,000 BCE (stone hoes, China and Levant) |
| 40 | `nixtamalization` | technology / main | food | A2 | 3 | 1400 | maize AND pottery_open_fired | 0/2 |  | 0 | 1 | ~1,500 BCE Guatemala |
| 41 | `sheep_goat` | technology / main | agriculture | A2 | 1 | 1120 | dog_domestication | 0/1 | herd / None | 1 | 2 | ~10,500 BCE Zagros/Taurus |
| 42 | `cattle` | technology / main | agriculture | A2 | 2 | 1250 | sheep_goat | 0/1 | herd / None | 1 | 3 | ~8,500 BCE Near East; ~7,000 BCE Indus; possibly Sahara |
| 43 | `pig` | technology / main | agriculture | A2 | 2 | 1250 | sheep_goat | 0/2 | herd / None | 0 | 1 | ~8,500 BCE Anatolia; ~6,000 BCE China |
| 44 | `milking` | technology / main | agriculture | A2 | 3 | 1400 | (sheep_goat OR cattle) AND pottery_open_fired | 0/2 | secondary_products / None | 0 | 2 | ~7,000 BCE Anatolia (lipid residues) |
| 45 | `wool` | technology / main | industry | A2 | 2 | 1250 | sheep_goat AND cordage | 0/1 | secondary_products / None | 0 | 2 | ~4,000 BCE Near East |
| 46 | `animal_traction` | technology / main | agriculture | A2 | 3 | 1400 | cattle | 0/2 | secondary_products / None | 0 | 3 | ~4,000 BCE (plough marks, Uruk pictographs) |
| 47 | `pottery_open_fired` | technology / main | materials | A2 | 2 | 1250 | basketry | 1/2 | ceramics / None | 0 | 3 | ~18,000 BCE Xianrendong; ~14,000 BCE Jomon; ~9,000 BCE Near East; ~9,400 BCE Mali |
| 48 | `kiln_updraft` | technology / main | materials | A2 | 3 | 1400 | pottery_open_fired AND mudbrick | 1/2 | kiln / None | 1 | 3 | ~6,000 BCE Yarim Tepe (Halaf) |
| 49 | `potters_wheel_fast` | technology / main | materials | A2 | 3 | 1400 | pottery_open_fired | 0/2 | wheel / None | 0 | 2 | ~3,500 BCE Uruk |
| 50 | `mudbrick` | technology / main | construction | A2 | 0 | 1000 |  | 0/2 | earth_building / None | 1 | 3 | ~9,000 BCE Jericho; ~7,500 BCE Çatalhöyük |
| 51 | `lime_plaster` | technology / main | construction | A2 | 1 | 1120 | mudbrick AND fire_making | 0/2 | lime / None | 0 | 3 | ~8,000 BCE PPNB (Ain Ghazal, Yiftahel) |
| 52 | `timber_frame` | technology / main | construction | A2 | 2 | 1250 | ground_stone_early AND shelter_hut | 1/2 | timber_building / None | 0 | 2 | ~5,500 BCE LBK longhouses |
| 53 | `stone_dry` | technology / main | construction | A2 | 2 | 1250 | ground_stone_early | 1/1 | stone_building / None | 3 | 3 | ~10,000 BCE Göbekli Tepe; ~3,000 BCE Skara Brae |
| 54 | `well` | technology / main | infrastructure | A2 | 3 | 1400 | stone_dry | 2/2 | water_supply / None | 1 | 2 | ~8,000 BCE Atlit-Yam (Israel) |
| 55 | `fermentation_grain` | technology / main | food | A2 | 3 | 1400 | cereal_domesticated AND pottery_open_fired | 1/2 | fermentation / None | 0 | 3 | ~11,000 BCE Raqefet (contested); ~3,500 BCE Godin Tepe (secure) |
| 56 | `fermentation_fruit` | technology / main | food | A2 | 3 | 1400 | pottery_open_fired | 0/2 | fermentation / None | 0 | 2 | ~7,000 BCE Jiahu; ~6,000 BCE Georgia (grape) |
| 57 | `oil_press` | technology / main | food | A2 | 3 | 1400 | orchard AND stone_dry | 0/2 |  | 1 | 3 | ~4,500 BCE Levant (olive) |
| 58 | `salt_extraction` | technology / main | food | A2 | 3 | 1400 | pottery_open_fired | 0/2 | preservation / None | 0 | 2 | ~6,000 BCE Poiana Slatinei (Romania); ~4,500 BCE Hallstatt |
| 59 | `flax` | technology / main | agriculture | A2 | 2 | 1250 | cereal_cultivation | 0/2 |  | 0 | 1 | ~8,000 BCE Levant (wild flax use ~30 kya Dzudzuana) |
| 60 | `spinning_spindle` | technology / main | industry | A2 | 3 | 1400 | cordage AND (flax OR wool) | 1/2 | textile / None | 0 | 1 | ~6,000 BCE (spindle whorls) |
| 61 | `loom_warp_weighted` | technology / main | industry | A2 | 4 | 1570 | spinning_spindle AND timber_frame | 0/2 | textile / None | 1 | 2 | ~7,000 BCE Çatalhöyük (textiles); loom weights ~5,500 BCE |
| 62 | `dyeing` | technology / main | industry | A2 | 5 | 1760 | loom_warp_weighted AND ochre_processing | 0/2 |  | 0 | 3 | ~4,000 BCE (dyed textiles); indigo ~4,000 BCE Peru |
| 63 | `copper_native` | technology / main | materials | A2 | 2 | 1250 | ground_stone_early AND fire_making | 1/1 | copper / None | 0 | 2 | ~8,700 BCE Çayönü; ~4,000 BCE Great Lakes |
| 64 | `copper_smelting` | technology / main | materials | A2 | 4 | 1570 | kiln_updraft AND (copper_native OR ochre_processing) | 2/2 | copper / None | 1 | 2 | ~5,000 BCE Belovode (Serbia) and Anatolia |
| 65 | `charcoal` | technology / main | energy | A2 | 3 | 1400 | birch_tar AND timber_frame | 0/2 | fuel / None | 0 | 2 | ~5,500 BCE with smelting |
| 66 | `casting_open` | technology / main | materials | A2 | 5 | 1760 | copper_smelting | 1/2 | casting / None | 0 | 1 | ~5,000 BCE |
| 67 | `gold_silver_native` | technology / main | materials | A2 | 3 | 1400 | copper_native | 0/1 |  | 0 | 2 | ~4,500 BCE Varna |
| 68 | `irrigation_basin` | technology / main | infrastructure | A2 | 2 | 1250 | cereal_cultivation | 0/2 | irrigation / None | 1 | 2 | ~5,000 BCE Egypt |
| 69 | `irrigation_canal` | technology / main | infrastructure | A2 | 2 | 1250 | cereal_cultivation | 0/2 | irrigation / None | 1 | 3 | ~6,000 BCE Samarra (Choga Mami) |
| 70 | `terrace` | technology / main | agriculture | A2 | 3 | 1400 | stone_dry AND cereal_cultivation | 1/2 | irrigation / None | 1 | 2 | ~4,000 BCE Yemen; Andes ~2,000 BCE; widespread |
| 71 | `sledge` | technology / main | transport | A2 | 3 | 1400 | timber_frame | 1/2 | vehicle / None | 0 | 1 | ~7,000 BCE Heinola (Finland) |
| 72 | `wheel_solid` | technology / main | transport | A2 | 4 | 1570 | animal_traction AND ground_stone_early | 1/3 | wheel / None | 0 | 2 | ~3,500 BCE (Bronocice, Ljubljana, Maykop, Uruk — near-simultaneous) |
| 73 | `cart` | technology / main | transport | A2 | 5 | 1760 | wheel_solid | 0/2 | vehicle / None | 0 | 1 | ~3,500 BCE |
| 74 | `track_road` | technology / main | transport | A2 | 3 | 1400 | timber_frame | 1/2 | road / None | 3 | 2 | ~3,800 BCE Sweet Track (Somerset) |
| 75 | `plough_ard` | technology / main | agriculture | A2 | 4 | 1570 | animal_traction AND digging_stick_hoe | 1/2 | tillage / None | 0 | 2 | ~4,000 BCE (plough marks under barrows; Uruk pictograph) |
| 76 | `token_counting` | technology / main | communication | A2 | 4 | 1570 | fire_making AND tally_notation | 1/1 | record / None | 0 | 1 | ~8,000 BCE |
| 77 | `stamp_seal` | technology / main | communication | A2 | 3 | 1400 | ground_stone_early AND pottery_open_fired | 2/2 | record / None | 1 | 3 | ~6,000 BCE Halaf |
| 78 | `calendar_lunar` | technology / main | science | A2 | 4 | 1570 | tally_notation | 0/2 | calendar / None | 0 | 2 | ~8,000 BCE Warren Field (Scotland, pit alignment) |
| 79 | `trepanation` | technology / main | medicine | A2 | 2 | 1250 | knapping_blade | 0/1 |  | 0 | 1 | ~7,000 BCE Ensisheim; widespread Neolithic |
| 80 | `arsenical_bronze` | technology / main | materials | A3 | 5 | 1760 | copper_smelting | 1/1 | bronze / 1 | 0 | 2 | ~4,000 BCE Caucasus, Anatolia, Iran |
| 81 | `tin_bronze` | technology / main | materials | A3 | 5 | 1760 | copper_smelting | 2/3 | bronze / 2 | 0 | 4 | ~3,300 BCE Mesopotamia/Anatolia; Vinča possibly ~4,650 BCE |
| 82 | `casting_closed` | technology / main | materials | A3 | 6 | 1970 | casting_open AND tin_bronze | 2/2 | casting / None | 0 | 2 | ~3,000 BCE |
| 83 | `lost_wax` | technology / main | materials | A3 | 7 | 2210 | casting_closed | 1/2 | casting / None | 0 | 3 | ~3,700 BCE Nahal Mishmar; Indus ~2,500 BCE |
| 84 | `sheet_metal` | technology / main | materials | A3 | 6 | 1970 | tin_bronze | 1/2 |  | 0 | 2 | ~2,500 BCE |
| 85 | `mining_shaft` | technology / main | materials | A3 | 5 | 1760 | stone_dry AND copper_smelting | 1/2 | mining / None | 1 | 2 | ~4,000 BCE flint (Spiennes); ~2,000 BCE copper (Great Orme) |
| 86 | `proto_writing` | technology / main | communication | A3 | 5 | 1760 | token_counting | 1/2 | writing / 1 | 0 | 1 | ~3,300 BCE Uruk IV |
| 87 | `cuneiform` | technology / main | communication | A3 | 6 | 1970 | proto_writing | 1/2 | writing / 2 | 4 | 4 | ~2,900 BCE |
| 88 | `hieroglyphic` | technology / main | communication | A3 | 5 | 1760 | token_counting | 0/1 | writing / 2 | 2 | 2 | ~3,250 BCE Abydos |
| 89 | `chinese_script` | technology / main | communication | A3 | 4 | 1570 | tally_notation AND bone_tools | 1/2 | writing / 2 | 2 | 2 | ~1,250 BCE Anyang |
| 90 | `papyrus` | technology / main | communication | A3 | 2 | 1250 | basketry | 0/2 |  | 1 | 3 | ~3,000 BCE Egypt |
| 91 | `numeral_sexagesimal` | technology / main | science | A3 | 7 | 2210 | cuneiform | 0/2 | numeral / 1 | 0 | 4 | ~2,100 BCE Ur III |
| 92 | `arithmetic_babylonian` | technology / main | science | A3 | 8 | 2480 | numeral_sexagesimal | 0/1 | numeral / 2 | 0 | 3 | ~1,800 BCE Old Babylonian |
| 93 | `surveying` | technology / main | science | A3 | 9 | 2770 | arithmetic_babylonian OR hieroglyphic | 0/2 |  | 0 | 3 | ~2,500 BCE Egypt, Mesopotamia |
| 94 | `sail_square` | technology / main | transport | A3 | 5 | 1760 | dugout AND loom_warp_weighted | 0/2 | sail / None | 1 | 2 | ~3,500 BCE Naqada II depictions; ~5,000 BCE Kuwait (possible) |
| 95 | `plank_boat` | technology / main | transport | A3 | 5 | 1760 | dugout AND copper_smelting | 1/2 | hull / None | 1 | 2 | ~2,500 BCE Khufu ship; ~2,000 BCE Ferriby |
| 96 | `horse_domestication` | technology / main | science | A3 | 3 | 1400 | cattle | 0/1 | herd / None | 1 | 3 | ~3,500 BCE Botai; DOM2 ~2,200 BCE |
| 97 | `donkey` | technology / main | science | A3 | 3 | 1400 | cattle | 0/1 | herd / None | 1 | 2 | ~4,000 BCE Egypt/Sudan |
| 98 | `camel` | technology / main | science | A3 | 3 | 1400 | cattle | 0/1 | herd / None | 1 | 2 | ~1,000 BCE dromedary; ~2,500 BCE Bactrian |
| 99 | `wheel_spoked` | technology / main | transport | A3 | 7 | 2210 | wheel_solid AND casting_closed | 2/2 | wheel / None | 0 | 1 | ~2,000 BCE Sintashta |
| 100 | `chariot` | technology / main | military | A3 | 8 | 2480 | wheel_spoked AND horse_domestication | 0/2 | vehicle / None | 1 | 2 | ~2,000 BCE Sintashta |
| 101 | `composite_bow` | technology / main | military | A3 | 2 | 1250 | bow_simple AND sheep_goat AND birch_tar | 0/2 | bow / None | 1 | 2 | ~2,000 BCE steppe |
| 102 | `fired_brick` | technology / main | materials | A3 | 4 | 1570 | kiln_updraft AND mudbrick | 1/2 | earth_building / None | 2 | 4 | ~2,900 BCE Indus (Kot Diji); Mesopotamia ~2,500 |
| 103 | `glass_glaze` | technology / main | materials | A3 | 4 | 1570 | kiln_updraft AND salt_extraction | 0/2 | glass / None | 0 | 2 | ~4,000 BCE Egypt (faience); glaze ~3,500 |
| 104 | `glass_core` | technology / main | materials | A3 | 5 | 1760 | glass_glaze | 0/2 | glass / None | 0 | 2 | ~1,600 BCE Mesopotamia, Egypt |
| 105 | `standard_weights` | technology / main | science | A3 | 10 | 3110 | surveying | 1/2 |  | 0 | 3 | ~2,600 BCE Indus; ~2,500 Mesopotamia |
| 106 | `calendar_civil` | technology / main | science | A3 | 6 | 1970 | calendar_lunar AND hieroglyphic | 0/1 | calendar / None | 0 | 2 | ~2,800 BCE Egypt |
| 107 | `water_clock` | technology / main | science | A3 | 7 | 2210 | pottery_open_fired AND calendar_civil | 0/1 | timekeeping / None | 0 | 2 | ~1,500 BCE Egypt (Karnak) |
| 108 | `medicine_recorded` | technology / main | medicine | A3 | 7 | 2210 | cuneiform OR hieroglyphic | 0/1 | medicine / 1 | 0 | 2 | ~2,100 BCE Sumerian; ~1,600 BCE Edwin Smith |
| 109 | `beekeeping` | technology / main | agriculture | A3 | 3 | 1400 | pottery_open_fired | 1/2 |  | 0 | 2 | ~2,400 BCE Egypt; ~900 BCE Tel Rehov apiary |
| 110 | `iron_bloomery` | technology / main | materials | A4 | 4 | 1570 | charcoal | 0/2 | iron / 1 | 1 | 4 | ~1,800 BCE Anatolia; ~1,200 BCE widespread; ~1,000 BCE Sub-Saharan; ~1,200 BCE India |
| 111 | `bellows` | technology / main | energy | A4 | 4 | 1570 | hide_working AND kiln_updraft | 1/2 |  | 0 | 2 | ~1,500 BCE (depicted in Egyptian tombs) |
| 112 | `steel_carburized` | technology / main | materials | A4 | 5 | 1760 | iron_bloomery | 0/2 | steel / 1 | 0 | 2 | ~1,200 BCE Cyprus, Anatolia |
| 113 | `quenching` | technology / main | materials | A4 | 6 | 1970 | steel_carburized | 0/2 | steel / 2 | 0 | 1 | ~1,100 BCE Cyprus (Idalion knife) |
| 114 | `tempering` | technology / main | materials | A4 | 7 | 2210 | quenching | 0/2 | steel / 3 | 0 | 2 | ~500 BCE Noricum, La Tène |
| 115 | `wootz` | technology / main | materials | A4 | 6 | 1970 | steel_carburized AND kiln_updraft | 0/2 | steel / 2 | 0 | 2 | ~500 BCE South India (Kodumanal) |
| 116 | `pattern_welding` | technology / main | materials | A4 | 8 | 2480 | tempering | 0/2 |  | 0 | 1 | ~300 BCE La Tène |
| 117 | `abjad` | technology / main | communication | A4 | 7 | 2210 | hieroglyphic OR cuneiform | 0/1 | writing / 3 | 2 | 2 | ~1,800 BCE Sinai; ~1,050 BCE Phoenician |
| 118 | `alphabet_vowels` | technology / main | communication | A4 | 8 | 2480 | abjad | 0/1 | writing / 4 | 2 | 2 | ~800 BCE Greece |
| 119 | `brahmi` | technology / main | communication | A4 | 8 | 2480 | abjad | 1/1 | writing / 3 | 1 | 1 | ~300 BCE (Ashokan edicts); possibly ~500 BCE |
| 120 | `hacksilver` | technology / main | communication | A4 | 11 | 3480 | standard_weights AND gold_silver_native | 0/2 | money / 1 | 0 | 3 | ~2,000 BCE Mesopotamia; hoards ~1,200 BCE Levant |
| 121 | `coinage_electrum` | technology / main | communication | A4 | 12 | 3900 | hacksilver AND stamp_seal | 1/2 | money / 2 | 2 | 4 | ~630 BCE Lydia; ~600 BCE China; ~400 BCE India |
| 122 | `cavalry` | technology / main | military | A4 | 4 | 1570 | horse_domestication | 0/2 | mounted / None | 1 | 3 | ~900 BCE steppe; Assyrian cavalry ~850 BCE |
| 123 | `saddle` | technology / main | military | A4 | 5 | 1760 | cavalry AND hide_working | 0/2 | mounted / None | 0 | 2 | ~700 BCE Scythian; framed saddle ~200 BCE |
| 124 | `siege_ram` | technology / main | military | A4 | 5 | 1760 | wheel_solid AND hide_working | 2/3 | siege / None | 1 | 1 | ~900 BCE Assyria |
| 125 | `siege_tower` | technology / main | military | A4 | 6 | 1970 | siege_ram | 1/2 | siege / None | 0 | 1 | ~700 BCE Assyria |
| 126 | `stone_fortification` | technology / main | construction | A4 | 10 | 3110 | stone_dry AND surveying AND (tin_bronze OR iron_bloomery) | 1/2 | fortification / None | 2 | 2 | ~1,300 BCE Hittite, Mycenaean; ~700 BCE widespread |
| 127 | `crossbow` | technology / main | military | A4 | 7 | 2210 | bow_simple AND casting_closed | 0/2 | bow / None | 1 | 1 | ~600 BCE China; ~400 BCE Syracuse |
| 128 | `naval_ram` | technology / main | military | A4 | 7 | 2210 | plank_boat AND casting_closed | 1/2 | warship / None | 1 | 2 | ~700 BCE Phoenicia/Greece; trireme ~550 BCE |
| 129 | `qanat` | technology / main | infrastructure | A4 | 10 | 3110 | well AND surveying AND mining_shaft | 0/2 | water_supply / None | 1 | 2 | ~800 BCE Persia |
| 130 | `aqueduct_tunnel` | technology / main | infrastructure | A4 | 11 | 3480 | qanat AND surveying | 0/2 | water_supply / None | 1 | 2 | ~700 BCE Siloam; ~550 BCE Samos |
| 131 | `aqueduct_channel` | technology / main | infrastructure | A4 | 12 | 3900 | aqueduct_tunnel AND stone_dry | 1/2 | water_supply / None | 1 | 1 | ~690 BCE Assyria |
| 132 | `arch` | technology / main | construction | A4 | 10 | 3110 | fired_brick AND surveying | 1/2 | arch / None | 0 | 2 | ~2,000 BCE mudbrick (Ur); stone ~400 BCE Etruria |
| 133 | `astronomy_records` | technology / main | science | A4 | 9 | 2770 | arithmetic_babylonian | 0/3 | astronomy / None | 1 | 3 | ~750 BCE Babylon |
| 134 | `calendar_lunisolar` | technology / main | science | A4 | 10 | 3110 | astronomy_records | 0/1 | calendar / None | 0 | 1 | ~500 BCE Babylon; Meton 432 BCE |
| 135 | `geometry_practical` | technology / main | science | A4 | 10 | 3110 | surveying AND alphabet_vowels | 0/1 | geometry / None | 0 | 2 | ~600 BCE Ionia |
| 136 | `medicine_hippocratic` | technology / main | medicine | A4 | 8 | 2480 | medicine_recorded | 0/3 | medicine / 2 | 3 | 3 | ~500 BCE Greece; ~600 BCE India; ~200 BCE China |
| 137 | `glass_blown_precursor` | technology / main | materials | A4 | 6 | 1970 | glass_core | 0/2 | glass / None | 0 | 2 | ~700 BCE Assyria, Phoenicia |
| 138 | `rotary_quern` | technology / main | agriculture | A4 | 5 | 1760 | grinding_stone AND wheel_solid | 1/2 | mill / None | 0 | 1 | ~500 BCE Spain, Celtic Europe |
| 139 | `olive_press_beam` | technology / main | industry | A4 | 4 | 1570 | oil_press AND stone_dry | 0/2 |  | 0 | 1 | ~800 BCE Levant (Tel Miqne-Ekron: 115 presses) |
| 140 | `geometry_axiomatic` | technology / main | science | A5 | 11 | 3480 | geometry_practical | 0/2 | geometry / None | 1 | 3 | ~300 BCE Alexandria |
| 141 | `mechanics_archimedean` | technology / main | science | A5 | 12 | 3900 | geometry_axiomatic | 0/1 | mechanics / None | 0 | 4 | ~250 BCE Syracuse |
| 142 | `torsion_artillery` | technology / main | military | A5 | 13 | 4360 | crossbow AND mechanics_archimedean | 1/2 | siege / None | 1 | 2 | ~340 BCE Macedon; formula ~250 BCE |
| 143 | `water_screw` | technology / main | infrastructure | A5 | 13 | 4360 | mechanics_archimedean | 1/2 | water_lift / None | 0 | 2 | ~250 BCE Egypt/Syracuse |
| 144 | `noria` | technology / main | infrastructure | A5 | 5 | 1760 | wheel_solid AND irrigation_canal | 0/2 | water_lift / None | 0 | 2 | ~300 BCE Egypt (saqiya); stream-driven ~100 CE |
| 145 | `water_mill` | technology / main | energy | A5 | 6 | 1970 | rotary_quern | 0/2 | mill / None | 2 | 2 | ~300 BCE Perachora (Greece); Roman ~100 BCE; China ~100 CE independently |
| 146 | `trip_hammer` | technology / main | energy | A5 | 7 | 2210 | water_mill | 0/2 | mill / None | 0 | 2 | ~40 BCE China (Han) |
| 147 | `gearing` | technology / main | industry | A5 | 13 | 4360 | mechanics_archimedean AND casting_closed | 1/2 | mechanism / None | 0 | 3 | ~150 BCE Antikythera mechanism |
| 148 | `concrete_pozzolan` | technology / main | materials | A5 | 11 | 3480 | lime_plaster AND arch | 0/2 | concrete / None | 0 | 3 | ~300 BCE Campania |
| 149 | `vault_dome` | technology / main | construction | A5 | 12 | 3900 | arch AND concrete_pozzolan | 0/2 | arch / None | 3 | 3 | ~100 BCE Rome; Pantheon 126 CE |
| 150 | `road_paved` | technology / main | transport | A5 | 10 | 3110 | surveying | 1/3 | road / None | 1 | 2 | 312 BCE Via Appia; Inca ~1,400 CE |
| 151 | `bridge_stone` | technology / main | construction | A5 | 12 | 3900 | arch AND concrete_pozzolan | 1/2 | bridge / None | 1 | 2 | ~62 BCE Rome |
| 152 | `aqueduct_arcade` | technology / main | infrastructure | A5 | 13 | 4360 | aqueduct_channel AND arch AND surveying | 1/2 | water_supply / None | 2 | 3 | ~312 BCE Aqua Appia; arcades ~144 BCE |
| 153 | `glass_blowing` | technology / main | materials | A5 | 7 | 2210 | glass_blown_precursor AND iron_bloomery | 0/2 | glass / None | 0 | 2 | ~50 BCE Syro-Palestine |
| 154 | `window_glass` | technology / main | materials | A5 | 8 | 2480 | glass_blowing | 0/2 | glass / None | 0 | 2 | ~100 CE Rome (Pompeii) |
| 155 | `cast_iron` | technology / main | materials | A5 | 5 | 1760 | iron_bloomery AND bellows AND kiln_updraft | 0/2 | iron / 2 | 1 | 3 | ~500 BCE China |
| 156 | `iron_mouldboard` | technology / main | agriculture | A5 | 6 | 1970 | cast_iron AND plough_ard | 0/2 | tillage / None | 0 | 2 | ~100 BCE Han |
| 157 | `seed_drill` | technology / main | agriculture | A5 | 7 | 2210 | iron_mouldboard | 0/2 |  | 0 | 3 | ~200 BCE Han |
| 158 | `collar_harness` | technology / main | transport | A5 | 4 | 1570 | horse_domestication AND hide_working | 0/2 | harness / None | 0 | 2 | ~300 BCE China (breast-strap); collar ~500 CE |
| 159 | `paper` | technology / main | communication | A5 | 1 | 1120 | cordage | 1/3 | writing_surface / 2 | 3 | 3 | ~100 BCE China (Fangmatan); standardised 105 CE |
| 160 | `crossbow_repeating` | technology / main | military | A5 | 8 | 2480 | crossbow | 1/2 |  | 0 | 1 | ~400 BCE China |
| 161 | `stirrup` | technology / main | military | A5 | 6 | 1970 | saddle AND iron_bloomery | 0/2 | mounted / None | 1 | 2 | ~300 CE China; Avars to Europe ~600 |
| 162 | `silk` | technology / main | industry | A5 | 5 | 1760 | loom_warp_weighted AND orchard | 0/2 |  | 0 | 2 | ~3,500 BCE China (silk); industry by Han |
| 163 | `lacquer` | technology / main | materials | A5 | 4 | 1570 | fermentation_fruit | 0/2 |  | 0 | 2 | ~5,000 BCE China (earliest); industry by Han |
| 164 | `astronomy_geometric` | technology / main | science | A5 | 12 | 3900 | geometry_axiomatic AND astronomy_records | 0/2 | astronomy / None | 1 | 3 | ~150 CE Ptolemy |
| 165 | `cartography` | technology / main | naval | A5 | 13 | 4360 | astronomy_geometric AND road_paved | 0/2 |  | 0 | 2 | ~150 CE |
| 166 | `latitude_sailing` | technology / main | naval | A5 | 10 | 3110 | astronomy_records OR calendar_lunar | 0/2 | navigation / None | 0 | 1 | ~1,000 BCE Polynesia (stellar); Greek ~300 BCE |
| 167 | `monsoon_sailing` | technology / main | naval | A5 | 10 | 3110 | sail_square AND astronomy_records | 0/2 |  | 0 | 2 | ~100 BCE |
| 168 | `lateen` | technology / main | naval | A5 | 6 | 1970 | sail_square | 0/2 | sail / None | 0 | 2 | ~200 CE Mediterranean; Indian Ocean possibly earlier |
| 169 | `mortise_hull` | technology / main | naval | A5 | 6 | 1970 | plank_boat AND iron_bloomery | 1/2 | hull / None | 0 | 2 | ~1,300 BCE Uluburun; Roman grain ships |
| 170 | `pharmacology` | technology / main | medicine | A5 | 9 | 2770 | medicine_hippocratic | 0/3 | medicine / 3 | 2 | 2 | ~60 CE Dioscorides; ~200 CE Shennong |
| 171 | `surgery_instruments` | technology / main | medicine | A5 | 9 | 2770 | medicine_hippocratic AND casting_closed | 1/2 |  | 1 | 3 | ~100 CE Rome (Pompeii, Bingen) |
| 172 | `abacus` | technology / main | science | A5 | 9 | 2770 | arithmetic_babylonian | 0/2 |  | 0 | 1 | ~300 BCE Greece (Salamis tablet); Roman hand abacus; Chinese suanpan ~200 CE |
| 173 | `alchemy` | technology / main | science | A5 | 8 | 2480 | glass_blowing | 0/3 | chemistry / None | 0 | 3 | ~100 CE Alexandria; ~200 BCE China |
| 174 | `distillation` | technology / main | science | A5 | 9 | 2770 | alchemy | 1/1 | chemistry / None | 0 | 3 | ~100 CE Alexandria (Maria the Jewess) |
| 175 | `heavy_plough` | technology / agriculture | agriculture | A6 | 7 | 2210 | iron_mouldboard AND wheel_solid | 0/2 | tillage / None | 0 | 2 | ~500 CE Slavic/Germanic Europe |
| 176 | `horse_collar` | technology / agriculture | agriculture | A6 | 5 | 1760 | collar_harness | 1/2 | harness / None | 0 | 2 | ~500 CE China; Europe ~900 |
| 177 | `horseshoe` | technology / engineering | transport | A6 | 6 | 1970 | iron_bloomery AND horse_collar | 0/2 |  | 0 | 1 | ~900 CE Europe |
| 178 | `rice_champa` | technology / agriculture | agriculture | A6 | 3 | 1400 | rice_wet | 0/2 | cereal / None | 0 | 2 | 1012 CE Song |
| 179 | `sugar_refining` | technology / agriculture | food | A6 | 10 | 3110 | fermentation_fruit AND distillation | 0/2 |  | 0 | 2 | ~350 CE India; Islamic ~700 |
| 180 | `windmill_vertical` | technology / engineering | energy | A6 | 7 | 2210 | water_mill | 1/2 | windmill / 1 | 1 | 1 | ~700 CE Sistan |
| 181 | `windmill_post` | technology / engineering | energy | A6 | 7 | 2210 | water_mill AND sail_square | 2/2 | windmill / 1 | 1 | 2 | ~1,180 CE England, Flanders |
| 182 | `fulling_mill` | technology / engineering | industry | A6 | 8 | 2480 | trip_hammer AND loom_warp_weighted | 0/2 |  | 1 | 2 | ~1,000 CE Europe |
| 183 | `spinning_wheel` | technology / engineering | industry | A6 | 5 | 1760 | spinning_spindle AND wheel_solid | 0/2 | textile / None | 0 | 1 | ~1,000 CE India or China; Europe ~1,280 |
| 184 | `horizontal_loom` | technology / engineering | industry | A6 | 14 | 4890 | loom_warp_weighted AND gearing | 1/2 | textile / None | 0 | 2 | ~1,000 CE (from China/Islamic world) |
| 185 | `cotton_gin_roller` | technology / engineering | industry | A6 | 5 | 1760 | wheel_solid | 0/2 |  | 0 | 1 | ~500 CE India |
| 186 | `compass_magnetic` | technology / military | naval | A6 | 5 | 1760 | iron_bloomery | 0/3 | navigation / None | 0 | 2 | ~1,040 CE China (Shen Kuo); Europe ~1,190 |
| 187 | `stern_rudder` | technology / military | naval | A6 | 7 | 2210 | mortise_hull OR plank_boat | 1/2 | hull / None | 0 | 2 | ~100 CE China; Europe ~1,180 |
| 188 | `watertight_bulkhead` | technology / military | naval | A6 | 8 | 2480 | stern_rudder | 1/2 | hull / None | 0 | 1 | ~500 CE China |
| 189 | `portolan` | technology / military | naval | A6 | 14 | 4890 | compass_magnetic AND cartography | 0/2 | navigation / None | 0 | 1 | ~1,270 CE (Carte Pisane) |
| 190 | `astrolabe` | technology / natural_science | science | A6 | 14 | 4890 | astronomy_geometric AND gearing | 0/2 | instrument / None | 1 | 3 | ~150 CE (theory); ~800 CE Islamic (instrument) |
| 191 | `cog` | technology / military | naval | A6 | 8 | 2480 | stern_rudder AND plank_boat | 0/2 | hull / None | 0 | 1 | ~1,000 CE Frisia; standard by 1,200 |
| 192 | `gunpowder` | technology / natural_science | science | A6 | 9 | 2770 | alchemy | 0/2 | gunpowder / 1 | 0 | 3 | ~850 CE China; formula 1044 |
| 193 | `saltpetre_refining` | technology / natural_science | science | A6 | 10 | 3110 | gunpowder | 0/2 | gunpowder / 2 | 0 | 1 | ~1,000 CE China |
| 194 | `cannon_early` | technology / military | military | A6 | 10 | 3110 | gunpowder AND cast_iron | 1/2 | cannon / 1 | 1 | 1 | ~1,280 CE China; Europe 1326 |
| 195 | `trebuchet` | technology / military | military | A6 | 14 | 4890 | torsion_artillery | 1/2 | siege / None | 1 | 1 | ~1,100 CE Byzantium/Islamic world |
| 196 | `longbow` | technology / military | military | A6 | 2 | 1250 | bow_simple | 0/2 | bow / None | 1 | 1 | ~1,200 CE Wales; English armies 1300s |
| 197 | `plate_armour` | technology / military | military | A6 | 8 | 2480 | tempering AND sheet_metal | 0/2 |  | 0 | 1 | ~1,350 CE |
| 198 | `greek_fire` | technology / military | military | A6 | 10 | 3110 | distillation | 0/2 |  | 0 | 1 | ~672 CE Constantinople |
| 199 | `woodblock_print` | technology / engineering | communication | A6 | 4 | 1570 | paper AND stamp_seal | 0/2 | printing / 1 | 1 | 3 | ~700 CE China/Korea; Diamond Sutra 868 |
| 200 | `movable_type_ceramic` | technology / engineering | communication | A6 | 5 | 1760 | woodblock_print AND kiln_updraft | 1/2 | printing / 2 | 0 | 1 | 1040 CE China |
| 201 | `movable_type_metal` | technology / engineering | communication | A6 | 7 | 2210 | movable_type_ceramic AND casting_closed | 1/2 | printing / 3 | 0 | 1 | 1234 CE Goryeo |
| 202 | `paper_money` | technology / engineering | communication | A6 | 13 | 4360 | woodblock_print AND coinage_electrum | 0/2 | money / 4 | 1 | 2 | ~1,020 CE Song |
| 203 | `hindu_arabic` | technology / main | science | A6 | 9 | 2770 | brahmi AND numeral_sexagesimal | 0/1 | numeral / 3 | 0 | 3 | ~500 CE India; Baghdad ~825; Europe ~1,200 (Fibonacci) |
| 204 | `algebra` | technology / main | science | A6 | 12 | 3900 | hindu_arabic AND geometry_axiomatic | 0/1 |  | 1 | 3 | ~820 CE Baghdad |
| 205 | `trigonometry` | technology / natural_science | science | A6 | 13 | 4360 | astronomy_geometric AND hindu_arabic | 0/2 |  | 0 | 3 | ~500 CE India (sine); ~900 Islamic (tangent, spherical) |
| 206 | `optics_ibn_haytham` | technology / natural_science | science | A6 | 12 | 3900 | geometry_axiomatic AND glass_blowing | 0/2 | optics / None | 1 | 2 | ~1,020 CE Cairo |
| 207 | `spectacles` | technology / engineering | materials | A6 | 13 | 4360 | optics_ibn_haytham AND window_glass | 0/2 | optics / None | 0 | 2 | ~1,286 CE Pisa |
| 208 | `crank_connecting_rod` | technology / engineering | industry | A6 | 14 | 4890 | gearing AND water_mill | 0/2 | mechanism / None | 0 | 3 | ~300 CE Hierapolis sawmill; general ~1,400 |
| 209 | `escapement_verge` | technology / engineering | industry | A6 | 15 | 5470 | gearing AND crank_connecting_rod | 0/2 | timekeeping / None | 0 | 2 | ~1,280 CE Europe; Su Song water escapement 1092 (dead end) |
| 210 | `blast_furnace_water` | technology / engineering | materials | A6 | 15 | 5470 | iron_bloomery AND crank_connecting_rod AND charcoal | 0/2 | iron / 2 | 1 | 2 | ~1,150 CE Lapphyttan; widespread 1,400 |
| 211 | `wire_drawing` | technology / engineering | materials | A6 | 15 | 5470 | iron_bloomery AND crank_connecting_rod | 0/2 |  | 0 | 3 | ~1,000 CE (water-powered ~1,350) |
| 212 | `canal_lock` | technology / engineering | infrastructure | A6 | 7 | 2210 | irrigation_canal AND mortise_hull | 1/2 |  | 1 | 2 | 984 CE China; ~1,400 Europe |
| 213 | `double_entry` | technology / natural_science | science | A6 | 13 | 4360 | hindu_arabic AND coinage_electrum AND paper | 0/2 | finance / 1 | 0 | 3 | ~1,300 CE Genoa, Florence |
| 214 | `bill_of_exchange` | technology / engineering | communication | A6 | 14 | 4890 | double_entry | 0/2 | finance / 2 | 0 | 3 | ~1,150 CE Italy |
| 215 | `printing_press` | technology / engineering | communication | A7 | 13 | 4360 | casting_closed AND mechanics_archimedean AND paper | 0/3 | printing / 4 | 4 | 2 | ~1,450 CE Mainz |
| 216 | `type_founding` | technology / engineering | industry | A7 | 14 | 4890 | printing_press | 0/2 |  | 0 | 1 | ~1,450 CE |
| 217 | `caravel` | technology / military | naval | A7 | 8 | 2480 | lateen AND stern_rudder AND compass_magnetic | 1/2 | ocean_ship / 1 | 1 | 3 | ~1,430 CE Portugal |
| 218 | `carrack` | technology / military | naval | A7 | 9 | 2770 | caravel AND cog | 0/2 | ocean_ship / 2 | 1 | 2 | ~1,450 CE |
| 219 | `polynesian_canoe` | technology / military | naval | A7 | 11 | 3480 | dugout AND latitude_sailing | 0/2 | ocean_ship / 2 | 1 | 1 | ~1,000 CE (Hawaii, NZ reached); technology older |
| 220 | `celestial_navigation` | technology / military | naval | A7 | 15 | 5470 | astrolabe AND trigonometry AND caravel | 0/2 | navigation / None | 0 | 1 | ~1,480 CE Portugal |
| 221 | `longitude_problem` | technology / military | naval | A7 | 16 | 6130 | celestial_navigation AND escapement_verge | 0/1 | navigation / None | 0 | 2 | 1761 CE |
| 222 | `mercator` | technology / military | naval | A7 | 15 | 5470 | cartography AND portolan AND trigonometry | 0/1 | navigation / None | 0 | 1 | 1569 CE |
| 223 | `corned_powder` | technology / natural_science | science | A7 | 11 | 3480 | saltpetre_refining | 0/2 | gunpowder / 3 | 0 | 2 | ~1,420 CE |
| 224 | `cannon_cast_bronze` | technology / military | military | A7 | 12 | 3900 | cannon_early AND lost_wax AND corned_powder | 0/2 | cannon / 2 | 1 | 2 | ~1,400 CE |
| 225 | `cannon_cast_iron` | technology / military | military | A7 | 16 | 6130 | cannon_cast_bronze AND blast_furnace_water | 0/2 | cannon / 3 | 1 | 1 | 1543 CE |
| 226 | `matchlock` | technology / military | military | A7 | 11 | 3480 | cannon_early AND crossbow | 0/2 | firearm / 1 | 1 | 1 | ~1,475 CE |
| 227 | `wheellock` | technology / military | military | A7 | 16 | 6130 | matchlock AND escapement_verge | 0/2 | firearm / 2 | 0 | 2 | ~1,500 CE Germany |
| 228 | `flintlock` | technology / military | military | A7 | 17 | 6870 | wheellock | 0/2 | firearm / 3 | 1 | 2 | ~1,610 CE France |
| 229 | `trace_italienne` | technology / military | military | A7 | 13 | 4360 | cannon_cast_bronze AND geometry_axiomatic | 0/2 | fortification / None | 1 | 1 | ~1,500 CE Italy |
| 230 | `ship_of_line` | technology / military | military | A7 | 17 | 6870 | carrack AND cannon_cast_iron | 0/2 | warship / None | 1 | 2 | ~1,650 CE |
| 231 | `heliocentrism` | technology / natural_science | science | A7 | 14 | 4890 | astronomy_geometric AND trigonometry | 0/2 | astronomy / None | 0 | 1 | 1543 / 1609 CE |
| 232 | `telescope` | technology / natural_science | science | A7 | 14 | 4890 | spectacles | 0/2 | optics / None | 1 | 2 | 1608 CE |
| 233 | `microscope` | technology / natural_science | science | A7 | 14 | 4890 | spectacles | 0/1 | optics / None | 0 | 2 | ~1,620 CE; Leeuwenhoek 1670s |
| 234 | `mechanics_newtonian` | technology / natural_science | science | A7 | 15 | 5470 | heliocentrism AND algebra AND calculus | 0/1 | mechanics / None | 0 | 3 | 1687 CE |
| 235 | `calculus` | technology / natural_science | science | A7 | 13 | 4360 | algebra AND geometry_axiomatic | 0/1 |  | 1 | 3 | ~1,670 CE |
| 236 | `barometer_vacuum` | technology / natural_science | science | A7 | 13 | 4360 | glass_blowing AND mechanics_archimedean | 0/2 |  | 0 | 2 | 1643 CE |
| 237 | `steam_atmospheric` | technology / engineering | energy | A7 | 17 | 6870 | barometer_vacuum AND cast_iron AND cannon_cast_iron | 0/2 | steam / 1 | 0 | 2 | 1712 CE |
| 238 | `coke` | technology / engineering | materials | A7 | 16 | 6130 | blast_furnace_water | 0/2 | iron / 3 | 0 | 2 | 1709 CE Coalbrookdale |
| 239 | `brass_calamine` | technology / engineering | materials | A7 | 16 | 6130 | copper_smelting AND wire_drawing | 1/2 |  | 0 | 2 | ~1,500 BCE (Roman); industrial ~1,550 CE |
| 240 | `inoculation` | technology / medicine | medicine | A7 | 9 | 2770 | medicine_hippocratic | 0/1 | immunisation / 1 | 0 | 1 | ~1,500 CE China (possibly earlier) |
| 241 | `anatomy_dissection` | technology / medicine | medicine | A7 | 14 | 4890 | medicine_hippocratic AND printing_press | 0/2 | medicine / 4 | 0 | 2 | 1543 CE |
| 242 | `crop_rotation_norfolk` | technology / agriculture | agriculture | A7 | 3 | 1400 | legume AND cereal_domesticated | 0/2 |  | 0 | 3 | ~1,650 CE Flanders, Norfolk |
| 243 | `seed_drill_tull` | technology / agriculture | agriculture | A7 | 8 | 2480 | seed_drill OR (crop_rotation_norfolk AND horse_collar) | 0/2 |  | 0 | 2 | 1701 CE |
| 244 | `selective_breeding` | technology / agriculture | agriculture | A7 | 4 | 1570 | crop_rotation_norfolk | 0/2 |  | 0 | 2 | ~1,760 CE |
| 245 | `new_world_crops` | technology / agriculture | agriculture | A7 | 10 | 3110 | carrack AND (maize OR root_crop) | 0/2 |  | 0 | 2 | ~1,500-1,700 CE |
| 246 | `glass_lead` | technology / engineering | materials | A7 | 17 | 6870 | glass_blowing AND coke | 0/2 | glass / None | 0 | 2 | 1674 CE |
| 247 | `canal_navigation` | technology / engineering | infrastructure | A7 | 10 | 3110 | canal_lock AND surveying | 0/2 |  | 1 | 2 | 1681 CE France; 1761 England |
| 248 | `steam_separate_condenser` | technology / engineering | energy | A8 | 18 | 7690 | steam_atmospheric AND barometer_vacuum | 0/2 | steam / 2 | 0 | 1 | 1769 CE |
| 249 | `cylinder_boring` | technology / engineering | industry | A8 | 17 | 6870 | cannon_cast_iron AND water_mill | 0/2 | machine_tool / 1 | 0 | 2 | 1774 CE |
| 250 | `steam_rotary` | technology / engineering | energy | A8 | 19 | 8610 | steam_separate_condenser AND cylinder_boring AND crank_connecting_rod | 0/2 | steam / 3 | 0 | 2 | 1781 CE |
| 251 | `steam_high_pressure` | technology / engineering | energy | A8 | 21 | 10800 | steam_rotary AND puddling | 0/1 | steam / 4 | 0 | 3 | 1804 CE |
| 252 | `puddling` | technology / engineering | materials | A8 | 20 | 9650 | coke AND steam_rotary | 0/2 | iron / 4 | 0 | 3 | 1784 CE |
| 253 | `hot_blast` | technology / engineering | materials | A8 | 17 | 6870 | coke | 0/2 |  | 0 | 1 | 1828 CE |
| 254 | `bessemer` | technology / engineering | materials | A8 | 21 | 10800 | puddling | 0/2 | steel / 4 | 1 | 3 | 1856 CE |
| 255 | `open_hearth` | technology / engineering | materials | A8 | 22 | 12100 | bessemer AND hot_blast | 0/2 | steel / 5 | 1 | 2 | 1865 CE |
| 256 | `basic_process` | technology / engineering | materials | A8 | 22 | 12100 | bessemer | 0/1 | steel / 5 | 0 | 2 | 1878 CE |
| 257 | `slide_rest_lathe` | technology / engineering | industry | A8 | 18 | 7690 | cylinder_boring AND escapement_verge | 0/2 | machine_tool / 2 | 1 | 3 | ~1,800 CE |
| 258 | `interchangeable_parts` | technology / engineering | industry | A8 | 19 | 8610 | slide_rest_lathe AND type_founding | 0/2 | machine_tool / 3 | 0 | 2 | ~1,820 CE Springfield |
| 259 | `milling_machine` | technology / engineering | industry | A8 | 19 | 8610 | slide_rest_lathe | 0/2 | machine_tool / 3 | 0 | 2 | ~1,818 CE (Whitney/North) |
| 260 | `flying_shuttle` | technology / engineering | industry | A8 | 15 | 5470 | horizontal_loom | 0/2 | textile / None | 0 | 2 | 1733 CE |
| 261 | `spinning_jenny` | technology / engineering | industry | A8 | 16 | 6130 | spinning_wheel AND flying_shuttle | 0/2 | textile / None | 0 | 1 | 1764 CE |
| 262 | `water_frame` | technology / engineering | industry | A8 | 17 | 6870 | spinning_jenny AND water_mill | 0/2 | textile / None | 1 | 2 | 1769 CE Cromford |
| 263 | `spinning_mule` | technology / engineering | industry | A8 | 18 | 7690 | water_frame | 0/2 | textile / None | 0 | 1 | 1779 CE |
| 264 | `power_loom` | technology / engineering | industry | A8 | 20 | 9650 | spinning_mule AND steam_rotary | 0/2 | textile / None | 1 | 1 | 1785 / 1820 CE |
| 265 | `cotton_gin_saw` | technology / engineering | industry | A8 | 20 | 9650 | cotton_gin_roller AND milling_machine | 0/1 |  | 0 | 1 | 1793 CE |
| 266 | `jacquard` | technology / engineering | industry | A8 | 15 | 5470 | horizontal_loom AND paper | 0/2 |  | 0 | 2 | 1804 CE Lyon |
| 267 | `locomotive` | technology / engineering | transport | A8 | 22 | 12100 | steam_high_pressure | 0/2 | rail / 1 | 0 | 2 | 1804 / 1829 CE |
| 268 | `railway` | technology / engineering | transport | A8 | 23 | 13550 | locomotive AND puddling AND surveying | 0/3 | rail / 2 | 1 | 3 | 1830 CE |
| 269 | `steamboat` | technology / engineering | transport | A8 | 20 | 9650 | steam_rotary AND mortise_hull | 0/2 | ship_powered / 1 | 0 | 2 | 1807 CE |
| 270 | `screw_propeller` | technology / military | naval | A8 | 21 | 10800 | steamboat AND water_screw | 1/2 | ship_powered / 2 | 1 | 2 | 1836 CE |
| 271 | `iron_hull` | technology / military | naval | A8 | 22 | 12100 | puddling AND screw_propeller | 0/2 | hull / None | 1 | 2 | 1843 CE |
| 272 | `macadam` | technology / engineering | transport | A8 | 11 | 3480 | road_paved | 1/2 | road / None | 1 | 1 | ~1,820 CE |
| 273 | `bicycle` | technology / engineering | transport | A8 | 6 | 1970 | steel_carburized | 0/3 |  | 0 | 2 | 1885 CE |
| 274 | `chemistry_quantitative` | technology / natural_science | science | A8 | 14 | 4890 | distillation AND barometer_vacuum | 0/3 | chemistry / None | 1 | 2 | 1789 CE |
| 275 | `sulphuric_acid` | technology / natural_science | science | A8 | 18 | 7690 | distillation AND glass_lead | 0/2 | industrial_chem / 1 | 1 | 4 | 1746 CE |
| 276 | `soda_leblanc` | technology / natural_science | science | A8 | 19 | 8610 | sulphuric_acid AND chemistry_quantitative | 0/2 | industrial_chem / 2 | 0 | 2 | 1791 CE |
| 277 | `bleach_chlorine` | technology / natural_science | science | A8 | 19 | 8610 | sulphuric_acid | 0/2 |  | 0 | 1 | 1785 CE |
| 278 | `synthetic_dye` | technology / natural_science | science | A8 | 17 | 6870 | chemistry_quantitative AND coke | 0/2 |  | 0 | 2 | 1856 CE |
| 279 | `portland_cement` | technology / engineering | materials | A8 | 15 | 5470 | concrete_pozzolan AND kiln_updraft AND chemistry_quantitative | 1/2 | concrete / None | 1 | 1 | 1824 / 1845 CE |
| 280 | `reinforced_concrete` | technology / engineering | construction | A8 | 22 | 12100 | portland_cement AND bessemer | 0/2 | concrete / None | 1 | 1 | 1867 CE |
| 281 | `haber_bosch` | technology / natural_science | science | A8 | 23 | 13550 | chemistry_quantitative AND open_hearth AND electricity_generation | 0/2 |  | 0 | 2 | 1913 CE |
| 282 | `dynamite` | technology / natural_science | science | A8 | 19 | 8610 | sulphuric_acid AND chemistry_quantitative | 0/2 |  | 0 | 2 | 1867 CE |
| 283 | `electromagnetism` | technology / natural_science | science | A8 | 14 | 4890 | calculus | 0/2 | electricity / 1 | 0 | 4 | 1820-1865 CE |
| 284 | `telegraph` | technology / engineering | communication | A8 | 16 | 6130 | electromagnetism AND wire_drawing | 0/2 | messaging / 3 | 1 | 2 | 1837 CE |
| 285 | `submarine_cable` | technology / engineering | communication | A8 | 22 | 12100 | telegraph AND screw_propeller | 0/3 |  | 1 | 1 | 1866 CE |
| 286 | `dynamo` | technology / engineering | energy | A8 | 20 | 9650 | electromagnetism AND steam_rotary | 0/2 | electricity / 2 | 0 | 1 | 1867 CE |
| 287 | `electric_light` | technology / engineering | energy | A8 | 21 | 10800 | dynamo AND barometer_vacuum | 0/2 |  | 0 | 2 | 1879 CE |
| 288 | `electricity_generation` | technology / engineering | energy | A8 | 22 | 12100 | dynamo AND electric_light AND electromagnetism | 0/2 | electricity / 3 | 2 | 3 | 1882 CE |
| 289 | `electric_motor` | technology / engineering | energy | A8 | 23 | 13550 | electricity_generation | 0/1 |  | 0 | 2 | ~1,885 CE |
| 290 | `telephone` | technology / engineering | communication | A8 | 17 | 6870 | telegraph | 0/2 | messaging / 4 | 1 | 1 | 1876 CE |
| 291 | `internal_combustion` | technology / engineering | energy | A8 | 20 | 9650 | milling_machine AND steam_rotary AND chemistry_quantitative | 0/2 | ice / 1 | 0 | 3 | 1876 CE |
| 292 | `petroleum_refining` | technology / natural_science | science | A8 | 10 | 3110 | distillation AND mining_shaft | 0/2 |  | 1 | 2 | 1859 CE |
| 293 | `vaccination` | technology / medicine | medicine | A8 | 10 | 3110 | inoculation | 0/1 | immunisation / 2 | 0 | 2 | 1796 CE |
| 294 | `anaesthesia` | technology / medicine | medicine | A8 | 15 | 5470 | chemistry_quantitative AND anatomy_dissection | 0/1 |  | 0 | 1 | 1846 CE |
| 295 | `germ_theory` | technology / medicine | medicine | A8 | 15 | 5470 | microscope | 0/4 | medicine / 5 | 1 | 3 | 1860-1880 CE |
| 296 | `antisepsis` | technology / medicine | medicine | A8 | 16 | 6130 | germ_theory AND anaesthesia | 0/2 |  | 0 | 2 | 1867 CE |
| 297 | `photography` | technology / natural_science | science | A8 | 18 | 7690 | chemistry_quantitative AND glass_lead | 0/2 |  | 0 | 2 | 1839 CE |
| 298 | `evolution` | technology / natural_science | science | A8 | 5 | 1760 | selective_breeding | 0/2 |  | 0 | 2 | 1858 CE |
| 299 | `aircraft` | technology / engineering | transport | A9 | 21 | 10800 | internal_combustion | 0/2 | aviation / 1 | 3 | 3 | 1903 CE |
| 300 | `aircraft_metal` | technology / engineering | transport | A9 | 24 | 15180 | aircraft AND aluminium | 0/2 | aviation / 2 | 0 | 2 | 1915 / 1935 CE |
| 301 | `jet_engine` | technology / engineering | energy | A9 | 25 | 17000 | aircraft_metal AND steam_turbine AND superalloy | 0/2 | aviation / 3 | 0 | 2 | 1939 CE |
| 302 | `steam_turbine` | technology / engineering | energy | A9 | 23 | 13550 | steam_high_pressure AND open_hearth | 0/2 | steam / 5 | 1 | 2 | 1884 CE |
| 303 | `automobile_mass` | technology / engineering | transport | A9 | 21 | 10800 | internal_combustion AND interchangeable_parts AND petroleum_refining | 0/2 | ice / 2 | 0 | 3 | 1913 CE |
| 304 | `tank` | technology / military | military | A9 | 23 | 13550 | internal_combustion AND basic_process AND cannon_cast_iron | 0/2 |  | 1 | 1 | 1916 CE |
| 305 | `diesel` | technology / engineering | energy | A9 | 23 | 13550 | internal_combustion AND open_hearth | 0/1 | ice / 2 | 0 | 3 | 1897 CE |
| 306 | `aluminium` | technology / engineering | materials | A9 | 23 | 13550 | electricity_generation AND chemistry_quantitative | 0/2 |  | 0 | 2 | 1886 CE |
| 307 | `superalloy` | technology / engineering | materials | A9 | 23 | 13550 | open_hearth AND chemistry_quantitative | 0/2 |  | 0 | 2 | ~1,940 CE |
| 308 | `rocket` | technology / engineering | transport | A9 | 24 | 15180 | internal_combustion AND aluminium AND calculus | 0/2 | rocket / 1 | 3 | 2 | 1926 / 1944 CE |
| 309 | `satellite` | technology / engineering | communication | A9 | 25 | 17000 | rocket AND computer AND transistor | 0/2 | rocket / 2 | 0 | 3 | 1957 CE |
| 310 | `container_shipping` | technology / engineering | transport | A9 | 24 | 15180 | iron_hull AND diesel | 0/3 |  | 0 | 1 | 1956 CE |
| 311 | `radio` | technology / engineering | communication | A9 | 17 | 6870 | electromagnetism AND telegraph | 0/2 | messaging / 5 | 2 | 2 | 1895 / 1920 CE |
| 312 | `vacuum_tube` | technology / natural_science | science | A9 | 22 | 12100 | electric_light AND radio | 0/2 | electronics / 1 | 0 | 3 | 1906 CE |
| 313 | `radar` | technology / military | military | A9 | 23 | 13550 | vacuum_tube AND electromagnetism | 0/2 |  | 0 | 3 | 1938 CE |
| 314 | `television` | technology / engineering | communication | A9 | 23 | 13550 | vacuum_tube AND photography | 0/2 | messaging / 6 | 1 | 1 | 1936 CE |
| 315 | `computer` | technology / engineering | engineering | A9 | 23 | 13550 | vacuum_tube AND calculus | 0/2 | computing / 1 | 0 | 2 | 1949 CE |
| 316 | `transistor` | technology / engineering | engineering | A9 | 23 | 13550 | vacuum_tube AND quantum_mechanics | 0/2 | electronics / 2 | 0 | 1 | 1947 CE |
| 317 | `integrated_circuit` | technology / engineering | engineering | A9 | 24 | 15180 | transistor AND photography | 0/2 | electronics / 3 | 1 | 2 | 1959 CE |
| 318 | `microprocessor` | technology / engineering | engineering | A9 | 25 | 17000 | integrated_circuit AND computer | 0/1 | computing / 2 | 0 | 2 | 1971 CE |
| 319 | `internet` | technology / engineering | communication | A9 | 25 | 17000 | computer AND telephone AND integrated_circuit | 0/2 | messaging / 7 | 1 | 3 | 1969 / 1983 / 1991 CE |
| 320 | `quantum_mechanics` | technology / natural_science | science | A9 | 15 | 5470 | electromagnetism | 0/2 |  | 0 | 3 | 1925 CE |
| 321 | `nuclear_fission` | technology / natural_science | science | A9 | 23 | 13550 | quantum_mechanics AND electricity_generation | 0/2 | nuclear / 1 | 0 | 2 | 1942 CE |
| 322 | `nuclear_power` | technology / engineering | energy | A9 | 24 | 15180 | nuclear_fission AND steam_turbine | 0/2 | nuclear / 2 | 1 | 1 | 1954 CE |
| 323 | `thermonuclear` | technology / military | military | A9 | 24 | 15180 | nuclear_fission | 0/2 | nuclear / 2 | 0 | 1 | 1952 CE |
| 324 | `antibiotic_penicillin` | technology / medicine | medicine | A9 | 16 | 6130 | germ_theory AND fermentation_grain | 0/2 | antibiotics / 1 | 0 | 2 | 1943 CE |
| 325 | `antibiotic_broad` | technology / medicine | medicine | A9 | 17 | 6870 | antibiotic_penicillin | 0/2 | antibiotics / 2 | 0 | 2 | 1943-1950 CE |
| 326 | `antibiotic_resistance` | technology / medicine | medicine | A9 | 18 | 7690 | antibiotic_broad AND evolution | 0/2 | antibiotics / 3 | 0 | 1 | ~1,960 CE |
| 327 | `vaccine_lab` | technology / medicine | medicine | A9 | 16 | 6130 | germ_theory AND vaccination | 0/2 | immunisation / 3 | 0 | 1 | 1885 / 1955 CE |
| 328 | `blood_transfusion` | technology / medicine | medicine | A9 | 16 | 6130 | germ_theory AND anaesthesia | 0/2 |  | 0 | 1 | 1901 / 1937 CE |
| 329 | `refrigeration` | technology / agriculture | food | A9 | 20 | 9650 | chemistry_quantitative AND steam_rotary | 0/2 |  | 0 | 2 | 1876 CE (Linde) |
| 330 | `canning` | technology / agriculture | food | A9 | 16 | 6130 | glass_blowing AND sheet_metal AND germ_theory | 0/2 |  | 0 | 2 | 1810 CE |
| 331 | `green_revolution` | technology / agriculture | agriculture | A9 | 24 | 15180 | haber_bosch AND genetics_mendel AND irrigation_canal | 0/2 | cereal / None | 0 | 1 | 1966 CE |
| 332 | `genetics_mendel` | technology / natural_science | science | A9 | 15 | 5470 | evolution AND microscope | 0/2 |  | 0 | 2 | 1900 CE |
| 333 | `dna_structure` | technology / natural_science | science | A9 | 19 | 8610 | genetics_mendel AND quantum_mechanics AND photography | 0/1 |  | 0 | 3 | 1953 CE |
| 334 | `genetic_engineering` | technology / natural_science | science | A9 | 20 | 9650 | dna_structure AND antibiotic_broad | 0/2 |  | 0 | 3 | 1973 CE |
| 335 | `hormonal_contraception` | technology / medicine | medicine | A9 | 16 | 6130 | chemistry_quantitative AND genetics_mendel | 0/1 |  | 0 | 1 | 1960 CE |
| 336 | `xray` | technology / medicine | medicine | A9 | 23 | 13550 | vacuum_tube AND photography | 0/2 |  | 0 | 2 | 1895 CE |
| 337 | `plastics` | technology / natural_science | science | A9 | 18 | 7690 | petroleum_refining AND chemistry_quantitative AND synthetic_dye | 0/2 |  | 0 | 2 | 1907 CE |
| 338 | `electric_traction` | technology / engineering | transport | A9 | 24 | 15180 | electric_motor AND railway | 0/2 |  | 0 | 2 | 1890 CE |
| 339 | `skyscraper` | technology / engineering | construction | A9 | 24 | 15180 | bessemer AND electric_motor AND reinforced_concrete | 0/2 |  | 1 | 1 | 1885 CE |
| 340 | `hydroelectric` | technology / engineering | energy | A9 | 23 | 13550 | electricity_generation AND reinforced_concrete AND water_mill | 0/2 |  | 1 | 2 | 1895 CE |
| 341 | `mechanised_agriculture` | technology / agriculture | agriculture | A9 | 21 | 10800 | internal_combustion AND seed_drill_tull | 0/2 |  | 0 | 1 | ~1,920 CE |
| 342 | `numerical_control` | technology / engineering | industry | A9 | 24 | 15180 | computer AND milling_machine | 0/2 |  | 0 | 1 | 1952 CE |
| 343 | `mobile_phone` | technology / engineering | communication | A9 | 26 | 19040 | radio AND microprocessor AND telephone | 0/2 | messaging / 8 | 1 | 1 | 1979 CE |
| 344 | `gps` | technology / military | naval | A9 | 26 | 19040 | satellite AND microprocessor AND atomic_clock | 0/2 | navigation / None | 0 | 3 | 1995 CE |
| 345 | `atomic_clock` | technology / natural_science | science | A9 | 17 | 6870 | quantum_mechanics AND longitude_problem | 0/2 | timekeeping / None | 0 | 2 | 1955 CE |
| 346 | `solar_pv` | technology / engineering | energy | A9 | 24 | 15180 | transistor AND quantum_mechanics | 0/1 |  | 1 | 2 | 1954 CE |
| 347 | `lithium_battery` | technology / engineering | energy | A9 | 15 | 5470 | chemistry_quantitative AND electromagnetism | 0/2 |  | 0 | 3 | 1991 CE |
| 348 | `relativity` | technology / natural_science | science | A9 | 16 | 6130 | electromagnetism AND mechanics_newtonian | 0/2 |  | 0 | 2 | 1905 / 1915 |
| 349 | `laser` | technology / natural_science | science | A9 | 23 | 13550 | quantum_mechanics AND vacuum_tube | 0/2 |  | 1 | 3 | 1960 |
| 350 | `fibre_optics` | technology / engineering | communication | A9 | 24 | 15180 | laser AND glass_lead | 0/2 | messaging / None | 0 | 1 | 1970 |
| 351 | `cmos_vlsi` | technology / engineering | engineering | A9 | 26 | 19040 | microprocessor | 0/2 | electronics / 4 | 0 | 2 | 1970s–1980s |
| 352 | `superconductivity_applied` | technology / natural_science | science | A9 | 21 | 10800 | quantum_mechanics AND refrigeration | 0/2 |  | 0 | 4 | 1911 discovery; magnets 1960s |
| 353 | `composites` | technology / engineering | materials | A9 | 19 | 8610 | plastics | 0/2 |  | 0 | 3 | 1930s fibreglass; 1960s carbon fibre |
| 354 | `gas_turbine_power` | technology / engineering | energy | A9 | 26 | 19040 | jet_engine AND electricity_generation | 0/2 |  | 0 | 1 | 1950s; combined cycle 1970s |
| 355 | `wind_turbine_modern` | technology / engineering | energy | A9 | 23 | 13550 | composites AND electricity_generation | 0/2 |  | 1 | 1 | 1980s Denmark |
| 356 | `nuclear_fusion_research` | technology / natural_science | science | A9 | 25 | 17000 | thermonuclear AND superconductivity_applied | 0/2 |  | 0 | 2 | 1958 tokamak |
| 357 | `grid_storage` | technology / engineering | energy | A9 | 23 | 13550 | lithium_battery AND electricity_generation | 0/3 |  | 0 | 2 | 2010s |
| 358 | `smart_grid` | technology / engineering | energy | A9 | 26 | 19040 | internet AND electricity_generation | 0/2 |  | 0 | 2 | 2000s |
| 359 | `programming_languages` | technology / engineering | engineering | A9 | 24 | 15180 | computer | 0/2 | computing / 2 | 0 | 1 | 1957 FORTRAN |
| 360 | `operating_system` | technology / engineering | engineering | A9 | 25 | 17000 | programming_languages | 0/2 | computing / 3 | 0 | 2 | 1960s–1970s |
| 361 | `relational_database` | technology / engineering | engineering | A9 | 25 | 17000 | programming_languages | 0/2 |  | 0 | 2 | 1970 |
| 362 | `personal_computer` | technology / engineering | engineering | A9 | 27 | 21320 | cmos_vlsi AND operating_system | 0/2 | computing / 4 | 0 | 2 | 1977 |
| 363 | `public_key_crypto` | technology / engineering | engineering | A9 | 25 | 17000 | programming_languages | 0/2 |  | 0 | 3 | 1976 |
| 364 | `gpu_parallel` | technology / engineering | engineering | A9 | 27 | 21320 | cmos_vlsi | 0/2 | computing / 5 | 0 | 1 | 1999 GPU; 2007 general-purpose |
| 365 | `machine_learning` | technology / engineering | engineering | A9 | 25 | 17000 | programming_languages AND calculus | 0/2 | ai / 1 | 0 | 2 | 1980s backpropagation |
| 366 | `deep_learning` | technology / engineering | engineering | A9 | 28 | 23880 | machine_learning AND gpu_parallel | 0/2 | ai / 2 | 0 | 2 | 2012 |
| 367 | `large_language_models` | technology / engineering | engineering | A9 | 29 | 26750 | deep_learning AND internet | 0/2 | ai / 3 | 0 | 2 | 2017 transformer; 2020 scale |
| 368 | `cloud_computing` | technology / engineering | engineering | A9 | 26 | 19040 | internet AND relational_database | 0/2 |  | 1 | 2 | 2006 |
| 369 | `smartphone` | technology / engineering | engineering | A9 | 28 | 23880 | mobile_phone AND personal_computer AND lithium_battery | 0/3 | messaging / None | 0 | 2 | 2007 |
| 370 | `cyber_warfare` | technology / engineering | engineering | A9 | 26 | 19040 | internet AND public_key_crypto | 0/3 |  | 0 | 2 | 2010 Stuxnet |
| 371 | `quantum_computing` | technology / engineering | engineering | A9 | 27 | 21320 | quantum_mechanics AND cmos_vlsi AND superconductivity_applied | 0/2 |  | 0 | 1 | 2019 demonstration |
| 372 | `cellular_digital` | technology / engineering | communication | A9 | 27 | 21320 | mobile_phone AND cmos_vlsi | 0/2 | messaging / None | 0 | 1 | 1991 GSM; 4G 2009 |
| 373 | `crewed_spaceflight` | technology / engineering | transport | A9 | 26 | 19040 | satellite AND computer | 0/3 | space / 1 | 0 | 1 | 1961 |
| 374 | `lunar_flight` | technology / engineering | transport | A9 | 27 | 21320 | crewed_spaceflight AND microprocessor | 0/3 | space / 2 | 0 | 1 | 1969 |
| 375 | `space_station` | technology / engineering | transport | A9 | 27 | 21320 | crewed_spaceflight | 0/2 | space / 2 | 0 | 2 | 1971 Salyut; 1998 ISS |
| 376 | `space_probe` | technology / natural_science | science | A9 | 26 | 19040 | satellite AND computer | 0/2 |  | 0 | 1 | 1962 Mariner 2 |
| 377 | `reusable_launch` | technology / engineering | transport | A9 | 27 | 21320 | rocket AND gps AND composites | 0/3 | rocket / 3 | 0 | 1 | 2015 |
| 378 | `space_telescope` | technology / natural_science | science | A9 | 26 | 19040 | satellite AND telescope | 0/2 |  | 0 | 2 | 1990 Hubble |
| 379 | `earth_observation` | technology / military | naval | A9 | 26 | 19040 | satellite AND photography | 0/3 |  | 0 | 2 | 1972 Landsat |
| 380 | `organ_transplant` | technology / medicine | medicine | A9 | 17 | 6870 | antisepsis AND blood_transfusion | 0/2 |  | 0 | 1 | 1954 kidney; 1980s immunosuppression |
| 381 | `chemotherapy` | technology / medicine | medicine | A9 | 16 | 6130 | chemistry_quantitative AND germ_theory | 0/2 |  | 0 | 1 | 1940s |
| 382 | `medical_imaging` | technology / medicine | medicine | A9 | 24 | 15180 | xray AND computer AND superconductivity_applied | 0/2 |  | 0 | 1 | 1971 CT; 1977 MRI |
| 383 | `antiviral_drugs` | technology / medicine | medicine | A9 | 20 | 9650 | dna_structure AND chemistry_quantitative | 0/3 |  | 0 | 1 | 1974 acyclovir; 1996 HIV therapy |
| 384 | `monoclonal_antibodies` | technology / medicine | medicine | A9 | 20 | 9650 | dna_structure AND vaccine_lab | 0/2 |  | 0 | 2 | 1975 |
| 385 | `pcr` | technology / natural_science | science | A9 | 20 | 9650 | dna_structure | 0/2 |  | 0 | 2 | 1983 |
| 386 | `genome_sequencing` | technology / natural_science | science | A9 | 24 | 15180 | pcr AND computer | 0/2 |  | 0 | 2 | 1977 Sanger; 2001 human genome |
| 387 | `ivf` | technology / medicine | medicine | A9 | 17 | 6870 | hormonal_contraception | 0/2 |  | 0 | 1 | 1978 |
| 388 | `crispr` | technology / natural_science | science | A9 | 25 | 17000 | genome_sequencing AND genetic_engineering | 0/2 |  | 0 | 1 | 2012 |
| 389 | `mrna_vaccines` | technology / medicine | medicine | A9 | 25 | 17000 | genetic_engineering AND vaccine_lab AND genome_sequencing | 0/3 | immunisation / 4 | 0 | 1 | 2020 |
| 390 | `oral_rehydration` | technology / medicine | medicine | A9 | 16 | 6130 | germ_theory AND chemistry_quantitative | 0/3 |  | 0 | 1 | 1960s |
| 391 | `drip_irrigation` | technology / agriculture | agriculture | A9 | 19 | 8610 | plastics AND irrigation_canal | 0/3 | irrigation / None | 0 | 1 | 1960s |
| 392 | `precision_agriculture` | technology / agriculture | agriculture | A9 | 27 | 21320 | gps AND mechanised_agriculture | 0/3 |  | 0 | 1 | 1990s |
| 393 | `desalination` | technology / engineering | infrastructure | A9 | 23 | 13550 | plastics AND electricity_generation | 0/3 |  | 1 | 1 | 1950s flash; 1960s reverse osmosis |
| 394 | `catalytic_converter` | technology / natural_science | science | A9 | 22 | 12100 | chemistry_quantitative AND automobile_mass | 0/3 |  | 0 | 1 | 1975 |
| 395 | `weather_prediction` | technology / engineering | engineering | A9 | 24 | 15180 | computer AND radio | 0/3 |  | 0 | 1 | 1950 |
| 396 | `carbon_capture` | technology / natural_science | science | A9 | 27 | 21320 | chemistry_quantitative AND gas_turbine_power | 0/3 |  | 0 | 1 | 1970s industrial; scale speculative |
| 397 | `high_speed_rail` | technology / engineering | transport | A9 | 25 | 17000 | electric_traction AND reinforced_concrete | 0/3 | rail / None | 0 | 1 | 1964 Shinkansen |
| 398 | `jet_airliner` | technology / engineering | transport | A9 | 26 | 19040 | jet_engine AND aircraft_metal | 0/2 | aviation / 4 | 0 | 1 | 1952 Comet; 1958 707 |
| 399 | `electric_vehicle` | technology / engineering | transport | A9 | 24 | 15180 | lithium_battery AND electric_motor | 0/2 |  | 0 | 1 | 2008 |
| 400 | `helicopter` | technology / engineering | transport | A9 | 22 | 12100 | aircraft | 0/2 | aviation / None | 1 | 2 | 1939–1942 |
| 401 | `icbm` | technology / military | military | A9 | 25 | 17000 | rocket AND thermonuclear AND computer | 0/3 | rocket / None | 1 | 1 | 1957 |
| 402 | `nuclear_submarine` | technology / military | military | A9 | 24 | 15180 | nuclear_fission AND screw_propeller | 0/3 |  | 1 | 2 | 1954 |
| 403 | `guided_munitions` | technology / military | military | A9 | 25 | 17000 | laser AND rocket AND computer | 0/2 |  | 0 | 1 | 1972 laser-guided bombs |
| 404 | `stealth` | technology / military | military | A9 | 24 | 15180 | composites AND radar AND computer | 0/3 |  | 1 | 1 | 1981 F-117 |
| 405 | `combat_drone` | technology / military | military | A9 | 27 | 21320 | gps AND microprocessor AND aircraft | 0/3 |  | 1 | 2 | 1990s–2000s |
| 406 | `missile_defence` | technology / military | military | A9 | 26 | 19040 | radar AND guided_munitions | 0/3 |  | 0 | 1 | 1980s–2000s |
| 407 | `additive_manufacturing` | technology / engineering | industry | A9 | 25 | 17000 | numerical_control AND plastics AND computer | 0/2 |  | 0 | 1 | 1986 |
| 408 | `nanotechnology` | technology / engineering | materials | A9 | 27 | 21320 | cmos_vlsi AND quantum_mechanics | 0/2 |  | 0 | 2 | 1980s–2000s |
| 409 | `fusion_power` | technology / engineering | energy | A9 (F) | 26 | 19040 | nuclear_fusion_research | 0/0 |  | 1 | 1 | speculative |
| 410 | `fault_tolerant_quantum` | technology / engineering | engineering | A9 (F) | 28 | 23880 | quantum_computing | 0/0 |  | 0 | 1 | speculative |
| 411 | `general_ai` | technology / engineering | engineering | A9 (F) | 30 | 29960 | large_language_models | 0/0 |  | 0 | 1 | speculative |
| 412 | `space_solar` | technology / engineering | energy | A9 (F) | 28 | 23880 | solar_pv AND reusable_launch | 0/0 |  | 1 | 1 | speculative |
| 413 | `asteroid_mining` | technology / engineering | materials | A9 (F) | 28 | 23880 | reusable_launch AND space_probe | 0/0 |  | 0 | 1 | speculative |
| 414 | `fusion_propulsion` | technology / engineering | transport | A9 (F) | 27 | 21320 | fusion_power AND space_probe | 0/0 |  | 1 | 1 | speculative |
| 415 | `frontier_launch` | technology / engineering | transport | A9 (F) | 28 | 23880 | reusable_launch | 0/0 |  | 0 | 1 | repeatable |
| 416 | `frontier_propulsion` | technology / engineering | transport | A9 (F) | 27 | 21320 | space_probe | 0/0 |  | 0 | 1 | repeatable |
| 417 | `frontier_materials` | technology / engineering | materials | A9 (F) | 28 | 23880 | nanotechnology | 0/0 |  | 0 | 1 | repeatable |
| 418 | `frontier_energy` | technology / engineering | energy | A9 (F) | 27 | 21320 | solar_pv OR gas_turbine_power | 0/0 |  | 0 | 1 | repeatable |
| 419 | `frontier_crops` | technology / agriculture | agriculture | A9 (F) | 26 | 19040 | crispr | 0/0 |  | 0 | 1 | repeatable |
| 420 | `frontier_medicine` | technology / medicine | medicine | A9 (F) | 25 | 17000 | genome_sequencing | 0/0 |  | 0 | 1 | repeatable |
| 421 | `frontier_computation` | technology / engineering | engineering | A9 (F) | 27 | 21320 | cmos_vlsi | 0/0 |  | 0 | 1 | repeatable |
| 422 | `frontier_military` | technology / military | military | A9 (F) | 26 | 19040 | guided_munitions | 0/0 |  | 0 | 1 | repeatable |
| 423 | `frontier_cyber` | technology / engineering | engineering | A9 (F) | 27 | 21320 | cyber_warfare | 0/0 |  | 0 | 1 | repeatable |
| 424 | `frontier_climate` | technology / natural_science | environment | A9 (F) | 28 | 23880 | carbon_capture | 0/0 |  | 0 | 1 | repeatable |
| 1001 | `law_code` | civics | governance | A3 (derived) | 7 | 2210 | cuneiform | 0/0 | | 1 | 0 | |
| 1002 | `legal_code_roman` | civics | governance | A4 (derived) | 9 | 2770 | law_code AND alphabet_vowels | 0/0 | | 3 | 0 | |
| 1003 | `census` | civics | governance | A4 (derived) | 9 | 2770 | (cuneiform AND stamp_seal) AND alphabet_vowels | 0/0 | | 6 | 0 | |
| 1004 | `coined_wage` | civics | governance | A4 (derived) | 13 | 4360 | coinage_electrum | 0/0 | | 1 | 0 | |
| 1005 | `patent` | civics | governance | A7 (derived) | 14 | 4890 | legal_code_roman AND printing_press | 0/0 | | 1 | 0 | |
| 1006 | `joint_stock` | civics | governance | A6 (derived) | 15 | 5470 | bill_of_exchange AND legal_code_roman | 0/0 | | 2 | 0 | |

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

