# D02 — Materials, metallurgy & mining: lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified or describes existing behaviour unless it
is labelled MEASURED.** Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED.
Canonical data: `data/proposal-parts/D02-materials-mining.json` (57 existing records, 3 new). Baseline: `edba2b5`
(`data/current-tree-inventory.json`). Closure figures are the package's cheapest-closure measure (`tools/kr_lib.py`,
greedy over OR — an upper bound), computed by a scratch script on the proposed graph.

## 1. Summary

The materials tree keeps its shape: 47 of 57 nodes are kept, 10 are **rewired** (prerequisites repaired for causality;
cost moves only through the prerequisite-complexity convention, on 3 of them), and 3 nodes are added.

- **Iron vs bronze — verdict.** `iron_bloomery`'s factor scores are right; its prerequisites were not. Its only
  prerequisite was `charcoal`, so bloomery smelting was reachable with no furnace knowledge at all, which is what made
  its closure (10 nodes / 14,320 RP) cheaper than the A3 CORE fact (9 / 15,250) [MEASURED]. Proposed:
  `iron_bloomery ← charcoal AND kiln_updraft`. Prerequisite complexity moves 0 → 0.5 by convention (8,360 → 11,830 RP);
  the closure becomes 14 / 26,130 RP, 1.7× the A3 fact. Copper smelting stays what the node's own Eureka says it is — an
  accelerator, "not a prerequisite: African iron may be independent".
- **Nine other causal repairs**: microliths need blade production; lost-wax casting predates closed moulds and tin bronze;
  pattern welding needs steel, not tempering; lacquer owes nothing to fermentation; hydraulic concrete needs lime, not the
  arch; cast iron needs casting, and the water-powered blast furnace needs bellows (each replaces a redundant atom, so the
  cost is unchanged); superalloys rest on scientific metallurgy; aluminium needs industrial alkali for its alumina (D04's
  request, cost unchanged).
- **Three new nodes**: `metallography` (A8, science) separates understanding metals from making them; `cupellation` (A3)
  supplies the silver that weighed-silver exchange and fire assay need; `percussion_drilling` (A6) is the extraction step
  of the brine, gas and oil chains.
- **The bronze chain is stated exactly** (§4): copper-ore and tin-ore are extracted ungated, `tin_bronze` gates the only
  metal recipe, and bronze is the only metal tools are made from. `arsenical_bronze` opens the Bronze Age without any recipe.
- Lane workload 591,150 → 612,380 RP (+21,230; +0.19 % of the 11.21 M tree) [MEASURED by scratch script].

## 2. Dispositions (57 owned nodes)

| disposition | count | nodes |
|---|---|---|
| keep, no field change | 40 | knapping_oldowan, knapping_levallois, knapping_blade, pressure_flaking, heat_treatment_stone, ground_stone_early, adhesive_natural, birch_tar, hafting, bone_tools, pottery_open_fired, kiln_updraft, copper_native, casting_open, gold_silver_native, casting_closed, sheet_metal, fired_brick, glass_glaze, glass_core, steel_carburized, quenching, tempering, glass_blown_precursor, glass_blowing, window_glass, brass_calamine, spectacles, wire_drawing, coke, glass_lead, puddling, hot_blast, bessemer, open_hearth, basic_process, portland_cement, composites, nanotechnology, asteroid_mining |
| keep, capability block only (owner, goods relation or deferred effect stated) | 6 | potters_wheel_fast, copper_smelting, arsenical_bronze, tin_bronze, mining_shaft, frontier_materials |
| keep, `emerged` corrected (text only) | 1 | wootz |
| rewire, cost unchanged | 7 | lacquer, lost_wax, pattern_welding, cast_iron, blast_furnace_water, aluminium, superalloy |
| rewire, cost moved by the prerequisite-complexity convention | 3 | iron_bloomery (+3,470), microlith (+110), concrete_pozzolan (−3,470) |
| split / merge / replace / relocate / retire | 0 | — |
| **new** | **3** | cupellation, percussion_drilling, metallography |

Totals: 40 + 6 + 1 = 47 keep; 7 + 3 = 10 rewire; 47 + 10 = 57. Every record also carries a PROPOSED `nodeKind`
(37 technique, 19 application, 1 science among the 57; the new nodes add one of each).

## 3. The MEASURED anomaly: iron smelting cheaper than bronze

**What is measured.** A3 CORE `a3_bronze` = `arsenical_bronze OR tin_bronze`: cheapest closure 9 nodes / 15,250 RP
(arsenical route). A4 CORE `a4_iron` = `iron_bloomery`: 10 / 14,320 [MEASURED, inventory and scratch reproduction].
The iron closure runs `charcoal ← birch_tar AND timber_frame` — adhesives, framed huts, axes — and never touches the
kiln, pottery or any smelting node. In the AI baseline the polity reaches `arsenical_bronze` at turn 421 and
`iron_bloomery` at 490 (seed 42), seventy turns apart, and `tin_bronze` only at 832 [INFERENCE in the measurement file,
reconstructed from orders].

**Diagnosis.** The node's own factors match its peers: novelty 2.0 (a new metal and hot forging), difficulty 1.25 and
institutional 1.0 (the A4 prior), material 0.75 (near-ubiquitous ore; charcoal in quantity — compare tin bronze's 1.5 for a
rare alloying metal), breadth 1.5 (Age CORE fact, nine dependants). Its own cost (8,360) already exceeds tin bronze
(7,030). What is wrong is a **missing causal edge**: bloomery smelting reduces ore with charcoal in a furnace at roughly
1,100–1,300 °C and needs an enclosed, controllable high-temperature chamber — the tree's `kiln_updraft`, whose own text
calls it "the thermal precondition for smelting" and which already gates `copper_smelting`.

**Options weighed.**

| option | causal? | D-047 chronology test | loader Eureka rules | verdict |
|---|---|---|---|---|
| `charcoal AND copper_smelting` | Near-Eastern route only | passes | **refused**: the node's Eureka `when: copper_smelting` would become *implied* (guaranteed by every path); Eurekas are append-only, so it cannot be removed | rejected |
| `charcoal AND bellows` (bellows ⊃ kiln) | yes, all centres (forced draught + furnace) | **fails**: bellows' `emerged` ~1,500 BCE post-dates iron ~1,800 BCE | ok | rejected unless D06 re-dates bellows (request sent) |
| `charcoal AND (copper_smelting OR bellows)` | two routes, but forced draught on one only | passes (OR atoms not checked) | ok, but the copper route makes the Eureka automatic | rejected |
| **`charcoal AND kiln_updraft`** | yes, every centre's furnace knowledge [INFERENCE: the kiln node stands for building an enclosed high-temperature chamber, which a smelting furnace is] | passes (kiln ~6,000 BCE) | Eureka stays a circumstance (copper smelting not guaranteed) | **adopted** |

The adopted edge keeps the sub-Saharan route open — iron smelting with clay shaft furnaces where no copper-smelting
tradition preceded it; its independence is debated [RESEARCHED: Alpern 2005 reviews the debate; early dates from the
Termit massif and Nok are contested] — exactly as the original authors intended when they wrote the copper-smelting
Eureka.

**Effect** (MEASURED on the proposed graph unless marked):

| target | baseline | D02 alone | all current lanes without D02 | all current lanes with D02 |
|---|---|---|---|---|
| A3 CORE (arsenical OR tin) | 9 / 15,250 | 9 / 15,250 | 9 / 15,250 | 9 / 15,250 |
| A4 CORE iron_bloomery | 10 / 14,320 | **14 / 26,130** | 10 / 14,320 | **14 / 26,130** |
| A5 CORE legal_code_roman · coinage_electrum | 7 / 8,660 · 18 / 28,790 | unchanged | unchanged | unchanged |
| A6 CORE heavy_plough | 28 / 59,470 | 32 / 68,490 (via cast_iron) | 19 / 27,570 (D01 relocates it onto iron_bloomery) | 23 / 39,380 |
| A7 CORE printing_press | 22 / 98,940 | unchanged | unchanged | unchanged |
| A8 CORE steam_rotary | 58 / 389,980 | +3,470 | 59 / 394,160 | 59 / 397,630 |
| A9 CORE electricity_generation · internal_combustion | 560,960 · 629,960 | +3,470 each | 541,870 · 639,480 | 545,340 · 642,950 |
| research stage | 18 / 28,660 | unchanged | unchanged | unchanged |

For the AI polity the change is small [INFERENCE, not run]: it has already completed `kiln_updraft` on its A3 path
(turn 393, seed 42), so its A4 goal changes mainly by iron's own +3,470 RP before the unchanged 40 % copper-smelting
Eureka credit. For the `--auto cheapest` driver, `kiln_updraft` (turn 765) also precedes iron (1,198). The fix is
causal, not a pacing lever: it removes the possibility of smelting iron with no furnace knowledge.

## 4. The bronze chain: how D02 nodes relate to goods and recipes (MEASURED)

Sources: `docs/research-gameplay-unlock-audit.md` §1/§5 (CI-checked), `Sim.Data/content/goods.json`, `sim.json`,
fog audit §2.

| node | unlock-audit class | realized consequence today |
|---|---|---|
| `tin_bronze` | B | `recipe.bronze_casting`: 8 copper-ore + 1 tin-ore + 2 timber → 1 bronze per execution, 0.4 person-years, plus the D-020 predicate `artisan_share > 0.05`; and the A3 CORE fact. Eureka reads `stock_tin_ore > 0`. |
| `arsenical_bronze` | B | the A3 CORE fact only — **no recipe** (no arsenical-ore good; the bronze recipe needs `tin_bronze`) |
| `copper_smelting` | J | `building.smithy` (never built). No production effect of its own: copper-ore feeds only the bronze recipe, into which smelting is folded. Its Eureka reads `stock_copper_ore > 0` (the only Eureka the tests allow to read that stock). |
| `mining_shaft` | B | `activity.mining` joins the Extraction identity — a **label**; extraction stays ungated |
| `pottery_open_fired` | B | `recipe.pottery_firing` (2 clay + 0.5 timber → 1 pottery) and the A2 supporting fact |
| `iron_bloomery` | B (BJ) | the A4 CORE fact; `building.smithy` unrealized; **no iron-ore or iron good**, so iron makes no tools |
| `kiln_updraft`, `fired_brick`, `cast_iron`, `blast_furnace_water`, `bessemer`, `open_hearth`, `portland_cement` | J | buildings and infrastructure listed, none realized |
| `ground_stone_early` | B | `activity.logging` (label) |
| `hafting` | F | `unit.spearmen` (M7) |

copper-ore (good 7, depositSpread 0.8) and tin-ore (good 8, spread 0.9) are per-settlement deposits on the elevation
channel, written at founding and extracted with no research gate from turn 1. **Bronze is the only input metal of the
baseline toolmaking recipe** (1 bronze + 1 timber → 2 tools), so `tin_bronze` is the knowledge gate of all domestic tool
production: the farm tool factor (up to +30 %, `toolYieldBonusMax` 0.3), the workshop project (10 tools) and highway
roads (1 tool per km) — unless bronze or tools arrive by trade. Two consequences follow:

1. **Age entry without capability** (a conflation): a polity entering A3 by the arsenical route cannot cast bronze or make
   tools, and cannot meet the supporting fact `a3_bronze_stock ≥ 50` without `tin_bronze` or traded bronze. The AI polity
   does exactly this, ~410–450 turns before tin bronze in all four seeds [INFERENCE in the measurement file]. OPEN DECISION:
   D02 recommends keeping the historically right OR and telling the player in the Age panel.
2. **Iron has no economic effect.** "Metal without long-distance trade" — the point of the Iron Age — is text only.
   OPEN DECISION (M10+ new resources): an iron-ore deposit good (common, surface, ungated) with bloomery-smelting
   (iron-ore + timber as charcoal → iron) and iron-toolmaking recipes, owned by ProductionSystem.

## 5. If surface ores stay ungated but deep resources are gated by identification (OPEN DECISION raised by the fog lane)

Rules this lane keeps: research never creates or reveals a deposit; identification is D11's; D02 supplies extraction
and processing knowledge; basic gathering is never gated (F-46, D-045).

- **Copper and tin (surface) — unchanged.** Extraction stays baseline; `tin_bronze` stays the processing gate. If D11
  adds tin identification, `tin_bronze ← copper_smelting AND <D11 node>` (complexity 0 → 0.5, 7,030 → 9,950 RP) — D02
  accepts it in advance. Under fog the two stock Eurekas only hold for ore actually extracted, hence located; an
  identification prerequisite must never sit downstream of the node whose Eureka reads that ore (dead-Eureka trap).
- **Proposed extraction ladder for new deep deposits (M10+):** `mining_shaft` — underground lodes above the water table
  (today a label; it would become a real eligibility gate) → `steam_atmospheric` (D06) — below the water table (its unlock
  text already says "mine drainage, coal mining at depth") → `dynamite` (D04) — hard rock at scale; fluids by
  `percussion_drilling` (new). Owner: ProductionSystem extraction, or the proposed DiscoverySystem for identification —
  the owner of the gate is part of the open decision.
- **Iron ore**: recommend no identification gate (bog iron, haematite, laterite are near-ubiquitous).
- **Coal (coke), pozzolana (concrete), argentiferous galena (cupellation), bauxite (aluminium), crude oil and brine
  (drilling)** are the identification needs of D02 nodes, all requested from D11. Their Eurekas
  (`future-system:resources/terrain`) become evaluable once that resource state exists.
- **A pre-steam drainage node is not added now** (Roman drainage wheels and screws; Agricola, 1556): it has no consumer
  until deep deposits exist, and it cannot feed `steam_atmospheric` because that node's Eureka reads `mining_shaft` — a
  drainage prerequisite (which needs `mining_shaft`) would make the Eureka implied, which the loader refuses (§15).

## 6. Rewires — each with its reason

1. **`iron_bloomery`** — `charcoal` → `charcoal AND kiln_updraft` (§3). 8,360 → 11,830 RP.
2. **`microlith`** — `hafting` → `hafting AND knapping_blade`: microliths are bladelet segments (the node's own text);
   without blade production there is nothing to segment [RESEARCHED: Howiesons Poort and Mesolithic microliths are
   bladelet industries]. 260 → 370 RP.
3. **`lost_wax`** — `casting_closed` → `casting_open`: a chronology inversion. Lost-wax casting predates two-piece
   moulds (~3,000 BCE in this tree) and tin bronze: the Mehrgarh amulet, unalloyed copper, from a site occupied
   4500–3600 BCE, is the earliest known lost-wax casting [RESEARCHED, Thoury et al. 2016], and the Nahal Mishmar hoard
   (~3,700 BCE) is copper–arsenic alloy. The CI chronology test exempts dependants dated before 3000 BCE, so the inversion
   was never caught; D-047 r1 / R-04 still asks for the fix. `emerged` gains the Mehrgarh anchor. Cost unchanged.
   Consequence: `cannon_cast_bronze` (D09) no longer inherits `tin_bronze` through it — request sent.
4. **`pattern_welding`** — `tempering` → `steel_carburized`: forge-welded strips of iron and steel need steel and the
   forge welding every bloomery smith practised, not the quench-and-temper cycle [INFERENCE from the technique].
5. **`lacquer`** — `fermentation_fruit` → `adhesive_natural`: lacquer is tree sap cured in layers; fermenting fruit is
   not knowledge it needs (also requested by D01). Its base is working natural resins and binders [INFERENCE].
6. **`concrete_pozzolan`** — `lime_plaster AND arch` → `lime_plaster`: the material does not need the arch. Roman
   hydraulic concrete was laid in harbour piers and moles with no arches — around the Bay of Naples before the late 2nd
   century BCE, at Cosa and at Caesarea (~25 BCE) [RESEARCHED, ROMACONS]; vaults and domes are `vault_dome` and
   `bridge_stone`, which require `arch AND concrete_pozzolan` themselves. 11,830 → 8,360 RP (convention consequence of
   removing a non-causal edge; it is a supporting, not a CORE, Age fact).
7. **`cast_iron`** — `iron_bloomery AND bellows AND kiln_updraft` → `iron_bloomery AND bellows AND casting_open`: cast
   iron is cast, yet no casting knowledge was required, while `kiln_updraft` was already implied by `bellows`. Records what
   Chinese cast iron built on — the bronze founders' practice [RESEARCHED via secondary summaries of D. B. Wagner].
   Cost unchanged (three requirements).
8. **`blast_furnace_water`** — `charcoal` (implied by iron) → `bellows`: the water-powered blast furnace is a furnace
   blown by water-driven bellows. Cost unchanged.
9. **`aluminium`** — `chemistry_quantitative` (implied) → `soda_leblanc` (D04's request, accepted): Hall–Héroult cells
   (1886) electrolyse alumina, and alumina at scale came from alkali processes — bauxite heated with soda (Le Chatelier,
   1855) and digested in caustic soda (Bayer, 1887–88); Deville's earlier route used sodium made from soda. Cost unchanged.
10. **`superalloy`** — `chemistry_quantitative` (implied) → `metallography`: Ni–Cr alloys strengthened with titanium and
    aluminium (from 1929; Nimonic 80, 1941, the standard British gas-turbine rotor-blade alloy) were designed with the
    physical metallurgy of precipitation hardening [RESEARCHED]. Cost unchanged.

Reviewed and kept: `puddling ← coke AND steam_rotary` (an OR with water power for Cort's rolling mill would let
`bessemer` bypass the steam blowing engines its converters needed); `mining_shaft ← stone_dry AND copper_smelting`
(read as metal-ore mining); `sheet_metal ← tin_bronze` (its capability strings are bronze-specific);
`glass_glaze ← … salt_extraction` (salt collecting stands for natron collecting — INFERENCE).

## 7. New nodes

| id | Age / branch | prerequisites | cost (magnitude) | analog | kind |
|---|---|---|---|---|---|
| `cupellation` — Silver refining by cupellation | A3 / trunk | copper_smelting AND gold_silver_native | 2,090 (4.5) | wheel_spoked | technique |
| `percussion_drilling` — Deep percussion drilling | A6 / engineering | iron_bloomery AND well | 4,970 (5.75) | spectacles | application |
| `metallography` — Scientific metallurgy (metallography) | A8 / engineering | microscope AND chemistry_quantitative | 14,060 (7.25; 92.4×2^7.25 = 14,065, integrator rounding) | puddling | science |

- **`cupellation`.** Most ancient silver came not from native silver but from argentiferous lead, refined by oxidizing
  the lead away; litharge from cupellation is dated to the 4th millennium BCE at Tepe Sialk, Habuba Kabira and
  Fatmalı-Kalecik [RESEARCHED]. It is the material basis of weighed-silver exchange and silver coinage, and fire assay is
  the same process in miniature. Consumers depend on adoption: `hacksilver` (D05) and assay (D11) — requests sent.
  Knowledge-only until then; M10+ silver good. Eureka (future-system): argentiferous lead ore smelted locally.
- **`percussion_drilling`.** A different extraction technique from shaft mining: cased boreholes reaching brine, gas and
  oil. Independent Chinese development (Sichuan brine and gas wells, advanced by the Song; the Shenhai well reached
  1,001 m in 1835) later used for the first oil wells (Drake, 1859, salt-well drillers) [RESEARCHED]. D04 made
  `petroleum_refining` processing-only and asked D11 to own drilling; extraction is this lane's remit, so D02 offers this
  node as the oil chain's extraction step (identification D11 → drilling D02 → refining D04). No current consumer:
  knowledge-only, deferred realization M10+ (fluid deposit extraction). Evaluable Eureka `when: salt_extraction`
  (deep drilling was developed by brine-salt producers); neither implied nor dead [checked].
- **`metallography`.** Separates understanding from application where the gap was largest: bloomery iron, crucible
  steel, the blast furnace and the Bessemer converter were all practised without knowing what steel is. Sorby's etched
  sections (1863–65), then Martens, Osmond, Le Chatelier and the iron–carbon diagram (Roberts-Austen, by 1900)
  [RESEARCHED] made designed alloys possible. Dependant: `superalloy`. Not required by any steelmaking node — the craft
  never needed the science.

All three carry era-marked `emerged` texts (the 83 undated texts stay 83), a full capability block, compatibility notes
and an acceptance test.

## 8. Alternative routes (OR)

- **Kept**: `copper_smelting ← kiln_updraft AND (copper_native OR ochre_processing)` — two ways of knowing the green ore
  (native copper, malachite pigment); the A3 OR (arsenical / tin); `stone_fortification`'s (tin_bronze OR iron_bloomery).
- **Encoded without an OR**: the sub-Saharan iron route (by not making copper smelting a requirement).
- **Declined**: `casting_closed ← casting_open AND (arsenical_bronze OR tin_bronze)` (closed moulds for arsenical copper
  before tin bronze — evidence found not conclusive, and it would move the A7 CORE closure); a no-bloomery route to
  `cast_iron` from bronze-foundry practice (Wagner's southern-origin view — secondary sources only); the Central Asian
  co-fusion route to crucible steel (`cast_iron` is A5, `wootz` A4 — an OR atom of a later Age breaks Age monotonicity;
  recorded in `wootz`'s `emerged` instead); iron via `(copper_smelting OR bellows)` (§3).

## 9. Candidates considered and REJECTED

| candidate | why rejected |
|---|---|
| Mine drainage and ventilation (Roman wheels and screws; Agricola 1556) | no consumer until deep deposits exist; cannot feed `steam_atmospheric` (its `mining_shaft` Eureka would become implied — loader rule 24); kept as the M10+ ladder design (§5) |
| Gunpowder blasting in mines (1627) | no consumer; `dynamite` (D04) carries hard-rock excavation; making `dynamite` require it would make its `mining_shaft` Eureka implied |
| Ore dressing / froth flotation (1905) | no consumer and no ore grades in the world model; M10+ with low-grade ores |
| Porcelain (Han proto-porcelain; Tang; Meissen 1708) | knowledge-only with no consumer and no good; high educational value — a candidate for the M10+ goods expansion and a model case for kaolin identification (D11) |
| Semiconductor-grade crystals (Teal–Little 1950; zone refining, early 1950s) | a real materials prerequisite of the integrated circuit and the solar cell, but adding it without cutting the integrated circuit's material factor double-counts workload; offered to D06's transistor/IC split |
| Splitting `composites` (glass-fibre / carbon-fibre) | its dependants do not divide by fibre; no bottleneck (66,910 RP) |
| Splitting `nanotechnology` (nanomaterials / nanofabrication) | single dependant; no bottleneck; the fabrication half overlaps D06's cmos_vlsi line |
| Separate Central Asian or Sheffield crucible-steel nodes | the same knowledge as `wootz` in other centres; recorded in `emerged` |
| Stainless and alloy steels, titanium, zinc distillation, sulphide-matte smelting, scientific optical glass | no consumer in the graph |
| `knapping_oldowan → knapping_levallois` edge | causal, but it removes a turn-0 root and touches founding and v33-fixture premises for an A1 refinement with no gameplay consequence |
| `heat_treatment_stone` as `pressure_flaking`'s prerequisite | obsidian and fine flint were pressure-flaked untreated |
| Removing redundant `mudbrick` from `fired_brick` | a package-level convention question (§15), not a one-node re-score |

## 10. Conflations found

- **Age entry vs capability**: `arsenical_bronze` grants the A3 CORE fact but no recipe (§4).
- **Research vs physical production**: the bronze recipe folds smelting, alloying and casting into one recipe gated by
  `tin_bronze`; `copper_smelting` has no production effect; `iron_bloomery` promises tools no good can carry.
- **Resource discovery vs exploitation**: `copper_smelting`'s OR group carries ore recognition inside a processing node;
  `tin_bronze` has no identification step although tin sources were the Bronze Age bottleneck (D11 request).
- **Material vs structure**: `concrete_pozzolan` required the arch (repaired).
- **Science vs craft**: superalloys hung directly on chemistry; the science of metals had no node (repaired by
  `metallography`).
- **Label vs gate**: `mining_shaft`'s capability strings ("ore from depth, sustained metal supply") are not realized —
  `activity.mining` is a label.
- **Two capabilities in one node, kept**: `nanotechnology`, `composites`, `puddling` ("puddling and rolling") — §9.

## 11. Dead ends resolved

| node | resolution |
|---|---|
| `potters_wheel_fast` | later owner: a wheel-thrown pottery recipe (a second recipe, not a modifier), ProductionSystem, M10+ |
| `frontier_materials` | its documented repeatable effect (construction material input), deferred by D-046 |
| `superalloy` | rewired onto `metallography`; causal consumer requested (D06 `gas_turbine_power`, else D08 `jet_airliner`) — not `jet_engine` (1937–39 engines predate Nimonic 80, 1941) |
| `pressure_flaking`, `heat_treatment_stone` | explicit justification: Palaeolithic craft knowledge, educational, no owner models stone-tool quality |
| `lacquer`, `pattern_welding`, `wootz`, `brass_calamine` | explicit justification: craft and regional knowledge (East Asian lacquer; La Tène and Viking blades; South Asian and Central Asian crucible steel; Roman brass) with no good or owner; none is a bottleneck |
| `asteroid_mining` | explicit justification: speculative-finite set member; no owner in any planned milestone |
| new `cupellation`, `percussion_drilling` | deferred M10+ realizations stated; consumers requested (D05, D11; D11) |

`gold_silver_native` gains a dependant (`cupellation`). The validator still WARNs on the 8 explicit-justification dead
ends (expected).

## 12. Cost findings

- Only three costs move, each by the prerequisite-complexity convention after a causal repair: iron +3,470,
  microlith +110, concrete −3,470. Four repairs replace a redundant or implied atom with the causal one and keep the
  count of top-level requirements (cast_iron, blast_furnace_water, aluminium, superalloy).
- New nodes follow the Age prior exactly (A3 1.0/0.75, A6 1.75/1.25, A8 2.5/2.0) and match a same-Age analog.
- **Redundant conjuncts inflate complexity**: `fired_brick`'s `mudbrick` (implied by `kiln_updraft`) and, before this
  proposal, `cast_iron`'s `kiln_updraft` and `blast_furnace_water`'s `charcoal`. A package rule is asked (§15).
- `metallography` rounds to 14,060 in `integrate.py` while `puddling` (same magnitude 7.25) ships 14,070; both are inside
  the loader tolerance (±max(5 RP, 0.1 %)).
- No A9 node of this lane bundles a 100k+ workload; nothing was split for pacing.

## 13. Compatibility summary

Keys unchanged; no split, merge or retire, so no lineage. Rewires leave completed nodes completed (no re-validation);
partial progress on a rewired node held without its new prerequisite is dormant until that completes; concrete's lower
cost completes saved progress ≥ 8,360 RP for free. No test pins any rewired id or cost; ResearchContentTests counts move
with the new nodes (technology count +3; trunk +1, engineering +2; engineering finite count 125 → 127, which every
engineering recursive descriptor must carry; Eureka totals +2, one evaluable knowledge condition). AI-empire and driven
goldens and the three CI `--check` docs move. Allocation-scenario keys 2 and 8 and the v33 key 2 are untouched.

## 14. Cross-lane requests

**Sent** (also in the JSON): **D11** — tin (cassiterite) identification for `tin_bronze`; coordinate copper-ore
recognition with `copper_smelting`'s OR group; assay can require `cupellation`; D11 owns crude-oil identification while
drilling (extraction) is D02's `percussion_drilling`; iron ore ungated; the identification needs of coal, pozzolana,
galena, bauxite, brine. **D06** — re-date `bellows` (pot bellows: Davey 1979, 2021; possible Chalcolithic bellows at Abu
Matar, Rose et al. 2024) or align its "iron smelting feasible" text; `superalloy` into `gas_turbine_power`; own or hand
over the semiconductor-crystal step of a transistor/IC split; do not make `steam_atmospheric` require a drainage node.
**D04** — aluminium request accepted; agreement that refining is processing-only. **D05** — `hacksilver ←
standard_weights AND cupellation`. **D09** — name `tin_bronze` in `cannon_cast_bronze`. **D08** — `superalloy` into
`jet_airliner` if D06 declines.

**Received and answered**: D01 (lacquer — done); D04 (aluminium ← soda_leblanc — accepted at no cost); D08 (rocket split
moves aluminium's dependant to `liquid_fuel_rocket` — noted); D09 (keep casting_closed, iron_bloomery, puddling,
basic_process, cast_iron, birch_tar — all kept).

## 15. Open questions

1. OPEN DECISION — keep the A3 CORE OR (arsenical / tin) although only tin bronze realizes bronze and tools? (D02: keep,
   and say so in the Age panel.)
2. OPEN DECISION (M10+) — realize iron with a surface iron-ore good and bloomery and iron-toolmaking recipes.
3. OPEN DECISION (fog lane, D11) — the extraction ladder for identification-gated deep deposits, and who owns the gate.
4. Package rule — should the validator flag redundant conjuncts and the convention count only independent requirements?
5. Historical checks requested — closed moulds for arsenical copper before tin bronze; a no-bloomery Chinese route to
   cast iron.
6. **Implied-Eureka trap** (MEASURED loader rule 24 + append-only Eurekas): a node can never take its own Eureka's
   knowledge condition as a prerequisite, and three Eurekas read `mining_shaft` (steam_atmospheric, railway, dynamite),
   which blocks any mining-depth node upstream of them. Should a lane be allowed to retire a Eureka when it promotes its
   condition to a prerequisite (a save-index rule is needed)?

## 16. Verification performed

- A scratch emulation of the loader-relevant rules on the proposed graph — Age monotonicity, trunk closure, references,
  cycles, the D-047 chronology test (earliest era-marked year, must-hold atoms, dependants after 3000 BCE) and the
  implied/dead-Eureka rules for all knowledge-condition Eurekas — reproduces the baseline exactly (no violations;
  83 undated) and finds no violation and no Eureka issue with D02's part, alone or integrated with D01, D03, D04, D05, D07,
  D08, D09, D10 and D12 as they stood at writing.
- `tools/integrate.py` and `tools/validate.py --no-corpus` run in a scratch copy of the package (no package file other than
  the two outputs was touched): 0 ERROR; D02's only rows are the 8 expected dead-end WARNs.
- Outside this lane, noticed while checking: the integrated graph has one chronology violation in D09's part
  (`guided_missile` 1943 ← `rocket` 1957 under D08's rename) — for the integrator.

## 17. Sources for RESEARCHED claims (accessed 2026-10-22)

- Bellows: Rose, Natali, Brotzu, Fabian, Goren, Archaeometry 66(3) 2024 (Abu Matar), listing
  https://cris.bgu.ac.il/en/publications/bellows-and-furnace-covers-in-the-unalloyed-copper-metallurgy-of--2/ ; EXARC
  experiment https://exarc.net/ark:/88735/10601 ; C. J. Davey, "Ancient Pot-Bellows: A Review Forty Years On", BASOR 385
  (2021), https://findanexpert.unimelb.edu.au/scholarlywork/1499154-ancient-pot-bellows--a-review-forty-years-on ;
  Davey 1979, Levant 11, https://keibi.uni-tuebingen.de/Record/KEI00094363
- Early Anatolian iron: https://link.springer.com/article/10.1007/s13632-023-00935-5 ;
  https://www.archaeology.org/news/7495-190325-anatolia-iron-smelting
- African iron debate: S. B. Alpern, History in Africa 32 (2005),
  https://www.cambridge.org/core/product/DB40377A90535C7041DC10159B43C50F ;
  https://en.wikipedia.org/wiki/Iron_metallurgy_in_Africa
- Chinese iron (Wagner, summarized): https://en.wikipedia.org/wiki/Bloomery ; https://donwagner.dk/arch-iron
- Mehrgarh lost-wax amulet (Thoury et al., Nature Communications 2016): https://cnrs.fr/en/node/2767 ;
  https://www.synchrotron-soleil.fr/en/news/novel-imaging-approach-reveals-how-ancient-amulet-was-made
- Cupellation: Pernicka et al. 1998 (Habuba Kabira), https://www.academia.edu/7001043 (the page's machine-written FAQ is
  not relied on); review incl. Tepe Sialk, https://pmc.ncbi.nlm.nih.gov/articles/PMC4667931
- Roman maritime concrete: https://web.uvic.ca/~jpoleson/ROMACONS/RomaconsWebsiteNewBiblio.htm ;
  https://www.ancientportsantiques.com/wp-content/uploads/Documents/PLACES/ItalyWest/BaiaeIulius-Brandon2008.pdf ;
  https://www.ancientportsantiques.com/wp-content/uploads/Documents/AUTHORS/Gianfrotta2011-Pilae.pdf
- Crucible steel: Srinivasan, MRS Proceedings (1997), https://www.cambridge.org/core/product/0C7F9DB996F0DD5758502D8F62201947 ;
  https://en.wikipedia.org/wiki/Wootz_steel (Merv and Akhsiket, 8th–12th c. CE)
- Metallography: Edyvean & Hammond (1997), https://hmsjournal.org/index.php/home/article/view/317 ; Nature 104 (1920),
  https://www.nature.com/articles/104537a0.pdf
- Superalloys: https://en.wikipedia.org/wiki/Nimonic ; INCO Triangle (1957),
  https://www.sudburymuseums.ca/triangle/data/INCOTriangle-19570701.pdf ; Superalloys 1984,
  https://www.tms.org/Superalloys/10.7449/1984/Superalloys_1984_399_419.pdf
- Chinese deep drilling: https://en.wikipedia.org/wiki/Zigong
