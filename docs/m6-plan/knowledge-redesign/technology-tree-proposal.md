# Technology tree proposal (Tree 1)

**Status: PROPOSED — design data for Director review; not ratified; not runtime content.** Canonical data:
`technology-tree-proposal.json` (schema: `data-schema.md`). Full per-node listings, generated from the JSON, are in
`proposal/technology-{trunk, military, medicine, engineering, natural_science, agriculture}.md`; the per-node
capability matrix in `matrix/`; the old→new mapping of every existing node in `audit/current-tree-inventory-*.md`.
Design method: `design-framework.md`; lane rationale: `research/lane-notes/`; cross-lane decisions:
`research/integration-adjudication.md`.

## 1. What changes, in numbers

| | Current (MEASURED) | Proposed |
|---|---|---|
| Technology nodes | 426 | 494 active (+70 new, −2 merged) |
| Trunk / Military / Medicine / Engineering / Natural Science / Agriculture | 177 / 37 / 22 / 131 / 44 / 15 | 205 / 44 / 30 / 139 / 61 / 15 |
| Dispositions of the 426 existing nodes | — | keep 298 (incl. keep-with-capability-restatement) · rewire 69 · rebalance 59 · relocate 34 · split 14 · merge 2 (combinable labels) |
| Total technology workload | 11,192,730 RP | 13,158,910 RP (+17.6 %; +1.6M is the optional nuclear/geology case study) |
| Most expensive node | 756,960 RP (`rocket`) | 378,470 RP |
| Knowledge-only dead ends | 91 | 13, each kept with a stated reason |
| Validator | — | 0 ERROR (`tools/validate.py --no-corpus`), 72 WARN of expected kinds |

New technology keys 427–496 are assigned by `tools/integrate.py` in (Age, branch, id) order — PROPOSED until
ratified. Retired keys (never reused): `siege_tower` → `siege_ram`, `crossbow_repeating` → `crossbow` (merges;
neither had a consumer).

## 2. Principles applied

1. **Understanding separate from application** where it matters (e.g., electromagnetism vs field theory; nuclear
   physics vs reactor families; radio telegraphy vs broadcasting).
2. **Causal prerequisites only**: 69 rewires remove chronology-only edges and add missing knowledge (iron smelting now
   needs furnace knowledge; algebra's prerequisites now give the research stage real Greek, Chinese and Indian routes;
   the heavy plough no longer waits on Chinese cast iron).
3. **Crafts stay in the trunk** (D-044 R15; docket KD-04): 34 relocations move practitioner crafts (heavy plough,
   printing press, type founding, windmills, medieval textile and mill crafts, Polynesian voyaging, commercial
   instruments) out of the subtrees, so medieval and early-modern Age facts no longer wait on the university stage.
4. **Splits only for bundled capabilities** (14): rocket, satellite, radio, electromagnetism, genome sequencing,
   electricity generation, internal combustion, computer, transistor, guided munitions, anatomy, stone fortification,
   nuclear fission, nuclear power. Each split keeps the original key on the part that preserves current consumers.
5. **Additions where a capability, decision or real dependency was missing** (70): e.g., thermodynamics, periodic
   system, cell theory, epidemiology, immunology, AC transmission, structural iron, sanitary engineering, rifling and
   industrial-era arms (military A8 was empty), cupellation, metallography, counting rods, ocean junk, cotton
   cultivation, and the geology/nuclear case study.
6. **Costs from the rubric, never for pacing** (D-046 §1): 59 rebalances each move one factor for a content reason
   (calendar institutional outliers, cereal material, medical imaging material, prerequisite-complexity convention after
   rewires) — docket KD-09.
7. **Every promise has an owner**: each node's capability block names what it makes eligible, the realizing system and
   milestone, its inputs, and behaviour when inputs or resources are missing or unknown.

## 3. Lane summaries

| Lane | New nodes | Splits / merges | Relocations | Headline |
|---|---|---|---|---|
| D01 agriculture & food | cotton_cultivation, agricultural_chemistry, chemical_fertilizer, farm_machinery, scientific_plant_breeding | — | heavy_plough, horse_collar, rice_champa, sugar_refining → trunk | OR routes for independent domestication centres; A6 CORE no longer needs the stage |
| D02 materials & mining | cupellation, percussion_drilling, metallography | — | — | iron needs furnace knowledge (Iron Age now costlier than Bronze Age); lost-wax and pattern-welding causality |
| D03 early science | counting_rods | — | 11 domain/age re-filings | the research stage's "geometry OR algebra" becomes a real choice |
| D04 natural science | thermodynamics, field theory, spectroscopy, organic chemistry, periodic system, cell theory, taxonomy, probability & statistics, DNA sequencing | electromagnetism, genome sequencing | double_entry → trunk | science/application separation; six non-causal edges removed |
| D05 communication & computing | radio broadcasting, comms satellite, automatic switching, punched-card tabulation, optical telegraph, Mesoamerican script | radio, satellite | printing chain, paper money, bill of exchange → trunk | printing is a craft (A7 CORE loses its hidden stage gate) |
| D06 energy & engineering | AC transmission, gas engine, programmable computer, semiconductor physics, water turbine | electricity generation, internal combustion, computer, transistor | windmills → trunk | grid connection separated from generation |
| D07 industry & construction | ashlar masonry, drawloom, structural iron, sanitary engineering | stone fortification | 8 medieval crafts → trunk | sewerage no longer needs germ theory; skyscraper via structural iron |
| D08 transport & naval | orbital launch vehicle, ocean junk, balloon, glider, submarine | rocket | Polynesian canoe → trunk | navigation grants access, never map reveal (D-040) |
| D09 military | traction trebuchet, rifling, breech-loading rifle, rifled artillery, machine gun, guided missile | guided munitions; merges of siege tower and repeating crossbow | — | unit eligibility only (battle layer M7) |
| D10 medicine | physiology, epidemiology, immunology, endocrinology, sulfonamides | anatomy (physiology split out) | — | all dead ends owned (M9 disease channels) |
| D11 geology & nuclear | 21 (see §4) | nuclear fission, nuclear power | — | the case study |

## 4. Nuclear science case study (lane D11; facts in `research/nuclear-geology-research-brief.md`, 250 sources)

Chain (27 nodes: 21 new + 6 existing): **geology path** `ore_prospecting` (A3, trunk; tin and copper identification) →
`mineralogy` → `stratigraphy` (A8; carries a proposed survey institution) → `geophysical_prospecting`,
`radiometric_prospecting`; **science** `radioactivity` → `radiation_detection` → `nuclear_physics` →
`nuclear_fission` (now the controlled critical pile only); **safety** `radiation_protection`; **fuel cycle**
`uranium_milling`, `uranium_enrichment`, `reprocessing`, `geological_disposal`; **reactor families**
`graphite_reactor`, `heavy_water_reactor` (both on natural uranium — no enrichment needed), `light_water_reactor`
(needs enrichment), `fast_breeder_reactor`; `nuclear_power` renamed *Nuclear power-plant engineering* (needs any
reactor family); **peaceful uses** `radioisotope_production`, `radiotherapy`, `nuclear_medicine`; **weapons**
`fission_weapon` (via enrichment OR reactor + reprocessing — the two dual-use steps), `thermonuclear`.

Rules honoured: uranium exists physically whatever is researched; no node reveals, places or proves a deposit;
identification requires observed territory (fog lane); electricity is a plant input, not a knowledge prerequisite.
Under the frozen Spine (`docs/civ-sim-architecture-v3-outline.md:137, 154`) the nuclear layer is deterrence-only:
weapons realize only as the deterrence abstraction (M10+); civil-nuclear nodes are knowledge-only and any civil
mechanic would need a CR (docket KD-20). Workload of the chain ≈ 2.41M RP (was 0.82M); optional cuts worth ≈ 0.43M are
listed in the lane notes.

## 5. Geology and resource knowledge across chains

| Chain | Location | Identification | Prospecting/proving | Extraction | Processing | Industrial use |
|---|---|---|---|---|---|---|
| Copper + tin → bronze | fog extent | `ore_prospecting` (surface ores) | contract only (M11+) | existing ungated surface extraction | `tin_bronze` recipe (existing) | tools, bronze casting |
| Iron ore + coal → steel | fog extent | `stratigraphy` (concealed classes) | deferred | new goods deferred to their expansion | bloomery → blast furnace → steel nodes | knowledge-only until goods exist |
| Petroleum → combustion | fog extent | `geophysical_prospecting` | deferred | `percussion_drilling` knowledge | `petroleum_refining` | `internal_combustion` (A9 CORE) |
| Uranium → nuclear | fog extent | `radiometric_prospecting` | deferred | `uranium_milling` | `uranium_enrichment` | reactor families (knowledge-only) |

Detail and state ownership: `fog-of-war-proposal.md` §12.

## 6. Compatibility (summary; per node in the inventory files)

Keys are identity: no existing key is renumbered; new keys append; two keys retire with successors. Splits, merges and
retirements lose knowledge in old saves unless the remap seam lands first (docket KD-08, roadmap KR-2). Rewires leave
partial progress dormant; lowered costs complete saved progress at the next step. Content changes move AI-empire goldens,
the allocation scenario and three CI `--check` documents (roadmap §3).
