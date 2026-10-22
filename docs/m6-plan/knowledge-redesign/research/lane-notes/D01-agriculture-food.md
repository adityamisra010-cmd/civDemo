# D01 — Agriculture and food: lane notes

**Status: PROPOSED.** This is a design lane's output. Nothing here is ratified and nothing describes existing
behaviour unless it is labelled MEASURED. Labels used: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION ·
IMPLEMENTATION DEFERRED.

The canonical data is `data/proposal-parts/D01-agriculture-food.json`. These notes explain it.

**Read for this lane:** the lane brief, `design-framework.md`, `data-schema.md`, `data/partition.json`, and
`data/current-tree-inventory.json`. Also `audit/engine-audit.md` (Q5–Q14 and appendices), `audit/fog-and-ai-information-audit.md`
§2, `docs/research-gameplay-unlock-audit.md` §1, `audit/governance-constraints-digest.md` (§1–§12) and
`measurements/baseline-summary.json` with its CSVs.

The following were read only, never modified: `Sim.Core/State/LabourActivities.cs`, `Sim.Core/Systems/Production/ProductionSystem.cs`,
`Sim.Core/Systems/Research/AiResearchPolicy.cs`, `Sim.Tests/Systems/ResearchContentTests.cs`, `Sim.Tests/Systems/AiPolicyTests.cs`,
`Sim.Data/content/sim.json`, `docs/deferred-entity-realization-plan.md` and `docs/m6-spec.md` (T6.11).

**Validation.** The validation was MEASURED in memory. The lane's part was integrated alone through `tools/integrate.py` and
`tools/validate.py` `check_graphs`, with their writers patched out, so no package file was written. The result:

- 0 errors, and no warning names a D01 node.
- The C# chronology heuristic (`ResearchContentTests.EarliestMarkedYear`) finds no violation.
- The marker-free `emerged` count stays 83.

## 1. Summary

**Dispositions.** D01 keeps 30 of its 46 nodes unchanged and changes 16. It splits, merges, replaces and retires
nothing, so it needs no save alias. It adds 5 new nodes.

**1. The A6 coupling is removed.** Four practitioner crafts move from the Agriculture subtree to the trunk:
`heavy_plough`, `horse_collar`, `rice_champa` and `sugar_refining`. They were in the subtree only because of an
Age-based classification rule.

- `heavy_plough` is the A6 CORE fact. It now rests on its causal European prerequisites, `plough_ard AND iron_bloomery
  AND wheel_solid`, instead of the Han cast-iron plough.
- Entering A6 therefore no longer requires the research stage or cast iron.
- The A6 gate falls from 40 nodes / 82,410 RP to 19 nodes / 27,570 RP. The old figure includes the stage closure; the
  new one is all trunk.

**2. Regional alternatives are added as OR routes.** No route adds a click: orchards, terraces, beer, pigs and sugar no
longer impose one domestication centre's sequence.

**3. Two causal gaps are closed:**
- breeding livestock now requires livestock;
- drip irrigation now requires irrigation.

**4. One cost inconsistency is fixed.** `material 2.0` on three crop nodes is re-scored to 0.0.

**5. Five new nodes are added:**
- `cotton_cultivation` (trunk);
- `agricultural_chemistry` → `chemical_fertilizer`;
- `scientific_plant_breeding`;
- `farm_machinery`.

The subtree now starts at A7. It still holds 15 nodes, 14 of them finite.

## 2. Disposition counts

| Disposition | Count | Nodes |
|---|---|---|
| keep | 30 | fishing_hook, grinding_stone, rice_wet, millet, sorghum_pearl_millet, root_crop, legume, sickle, digging_stick_hoe, nixtamalization, sheep_goat, cattle, milking, animal_traction, fermentation_fruit, oil_press, salt_extraction, flax, plough_ard, beekeeping, rotary_quern, iron_mouldboard, seed_drill, crop_rotation_norfolk, new_world_crops, refrigeration, canning, precision_agriculture, frontier_crops, frontier_climate |
| rewire, cost unchanged | 6 | orchard, pig, fermentation_grain, terrace, seed_drill_tull, green_revolution |
| rewire, cost moved by the prerequisite convention | 3 | selective_breeding, mechanised_agriculture (`emerged` also re-dated), drip_irrigation |
| rebalance | 3 | cereal_cultivation, cereal_domesticated, maize (material re-score) |
| relocate (subtree → trunk) | 4 | horse_collar, rice_champa; heavy_plough and sugar_refining also rewire |
| split / merge / replace / retire | 0 | — |
| new | 5 | cotton_cultivation, agricultural_chemistry, chemical_fertilizer, farm_machinery, scientific_plant_breeding |

The `rewire` disposition follows the updated framework §4: prerequisites repaired for causality, with cost moving only
through the prerequisite-complexity convention.

`refrigeration` stays `keep` in this part. D01 accepted D04's request to add D04's new `thermodynamics`, but that edge
must be applied as an integration override, because a lane part may not name another lane's new id. See §13.

Capability blocks are documented on 26 existing nodes, because the auto-fill was wrong or incomplete for them:

- 6 farming-yield nodes;
- 2 herding-label nodes;
- 15 dead ends;
- `heavy_plough`, `terrace` and `salt_extraction`.

## 3. Coupling check: `heavy_plough` and the A6 Age (requested)

### What the current content does (MEASURED)

- `ages.json` names `heavy_plough` as the A6 CORE research fact. `heavy_plough` sits in the Agriculture subtree only
  because ADR-029 §8, rule R1, assigned every node of A6 or later to a subtree by domain.
- Entering A6 therefore needed the research stage: `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND
  stamp_seal AND legal_code_roman`.
- It also needed `iron_mouldboard` ← `cast_iron`, at 16,730 RP.
- Under the cheapest-first driver, `heavy_plough` never completes on seeds 1, 2, 42 or 7 (coordinator finding,
  `baseline-progression` §0 item 4).

### Is the dependency causal?

**Organized scholarship is not causal.** The heavy plough is a peasant and smith's craft.

- RESEARCHED: it was used among some Slavs by about 568 CE (linguistic evidence), and the Carolingian *carruca* named
  it.
- That is centuries before the first European universities.

**The cast-iron route is not causal either.** It is the Han Chinese plough, a separate development.

- RESEARCHED (Bray): the Han plough had a cast-iron mouldboard by about 100 BCE.
- The European plough's share and coulter were forged bloomery iron. Its mouldboard was commonly wooden, and its
  wheeled forecarriage gives the node its name.
- European cast iron came centuries later.

**The causal set is all trunk:**
- `plough_ard` (the plough, and traction through it);
- `iron_bloomery` (the share and coulter);
- `wheel_solid` (the forecarriage).

Moving `heavy_plough` to the trunk is therefore legal, and D-044 §15 asks for it: "do not force every existing
technology into a specialized branch".

### Effect on the A6 coupling

| | Nodes | RP | Includes |
|---|---|---|---|
| Old A6 core requirement | 40 | 82,410 | `heavy_plough` closure plus the stage closure; contains `cast_iron` |
| New A6 core requirement | 19 | 27,570 | trunk only; no stage; no cast iron |

The RP figures are greedy upper bounds.

- Entering A6 still requires entering A5 first. The A5 core already needs the civic `legal_code_roman` and
  `coinage_electrum`.
- A7, whose core node is `printing_press` in the engineering subtree, becomes the first Age bound to the research stage.

### Effect on the AI's Age goal

**What changes:**
- `AiResearchPolicy.CoreGoalClosure` for A6 becomes all-trunk, so every node in it can be pursued before the stage.
- The engine-audit Q11 gap no longer applies to A6. That gap is a core goal that is skipped because its last node
  waits behind the closed stage.

**Measured context (MEASURED):**
- In the four AI baselines, the stage opened at turns 702–716.
- A5 entry, at about turn 1225, was the binding constraint. A6 followed four turns later, so the stage coupling did not
  bind there.

**Expected effect (INFERENCE):** the cheapest-first driver now reaches `heavy_plough`. AI-empire goldens move.

### Other practical crafts moved with it

All four relocated nodes have trunk-only prerequisites:

| Node | Why it is not university knowledge |
|---|---|
| `horse_collar` | a harness-makers' craft |
| `rice_champa` | Song state distribution of a farmers' variety after the 1012 CE drought (RESEARCHED) |
| `sugar_refining` | an Indian crystallisation craft, about 350 CE (RESEARCHED) |

### Practitioner nodes that stay in the subtree

The A7 nodes `crop_rotation_norfolk`, `seed_drill_tull`, `selective_breeding` and `new_world_crops` are also
practitioner knowledge, but they stay in the subtree:

- the validator's V17 convention keeps every A7+ node in a subtree;
- no Age fact depends on them;
- the stage opens long before they matter in every measured run.

This is recorded as a finding, not a change.

### Counts after this lane alone

| | Before | After |
|---|---|---|
| Trunk | 177 | 182 |
| Agriculture subtree | 15 | 15 (4 moved out, 4 new) |
| Agriculture finite nodes | 14 | 14 |

`frontier_crops`' `finite_nodes_to_exhaust` therefore stays 14, but only if all four new subtree nodes are accepted.

## 4. Realization: which nodes touch ProductionSystem today, and what is deferred

**Current effects (MEASURED).** Farming yield depends on research in one place only.

- `activity.farming` requires `cereal_cultivation OR root_crop OR rice_wet OR millet OR maize OR sorghum_pearl_millet`.
- With `farming.preCultivation` enabled, the first member a settlement's knowledge holds switches its Farming sector:
  - from wild-food rates: 2.0 food per arable km² per year and 4.3 per gatherer;
  - to cultivated rates: 26.0 and 5.0.
- The code path is `LabourActivities.HarvestsWildFood` and `ProductionSystem.cs:276-288`.
- Every further member only relabels the sector.
- `activity.herding` (`sheep_goat OR cattle`) is a label only. Livestock come from deposits with or without research.
- No other D01 node changes production, consumption or construction today.
- The research.json `_doc` line "production unchanged" is wrong for Farming; this is engine-audit Doc≠code item 1. The
  capability blocks now state the real effect.

**Deferred effects (IMPLEMENTATION DEFERRED).** Each deferred effect is phrased as a mechanism, never as a free-floating
modifier (law 2). That means an input or equipment good, a stock, or a coefficient inside the land or labour side of
the Farming equation.

| Node(s) | Deferred mechanism | Owner · milestone |
|---|---|---|
| the six farming members | crop-package suitability (climate, water, soil) | ProductionSystem/CatchmentSystem · M10+ |
| sheep_goat, cattle, pig, milking | managed herds as a stock (breeding, fodder, dairy) instead of a fixed wild-deposit rate | ProductionSystem · M10+ |
| heavy_plough, crop_rotation_norfolk | heavy clay as effective arable; a lower fallow share in the land side (CR-002 assumes medium fallow) | ProductionSystem/CatchmentSystem · M10+ |
| seed_drill, seed_drill_tull | a lower seed rate (CR-002 subtracts 120 kg/ha of broadcast seed) | ProductionSystem · M10+ |
| terrace; oil_press | infra.terraces adds arable on slopes (tier C); building.oil_press (tier D) | ConstructionSystem · M6 (T6.11, PROPOSED spec) |
| farm_machinery | an implement good modelled on the existing tools mechanism; needs draught horses | ProductionSystem · M10+ |
| chemical_fertilizer | a fertilizer good applied per arable km² (a nutrient input) | ProductionSystem · M10+ |
| scientific_plant_breeding, green_revolution, frontier_crops | a seed-variety state read by the land side; it needs fertilizer and water inputs | ProductionSystem · M10+ |
| rice_champa | double cropping on watered rice land | ProductionSystem · M10+ |
| drip_irrigation | arid cells become effective arable (needs a water budget) | CatchmentSystem · M10+ |
| salt_extraction, canning, refrigeration | preserved fish and livestock can enter stores (MEASURED: only grain is stored today, at 8%/yr spoilage) | ConsumptionSystem · M10+ |
| fishing_hook | an offshore fish stock reachable by boat | ProductionSystem · M10+ |
| sugar_refining, beekeeping, cotton_cultivation | new goods: sugar, honey and wax, cultivated fibre | ProductionSystem · M10+ |
| nixtamalization | pellagra risk, if M9 models diet-related disease | M9 society/disease |
| new_world_crops | crop transfer by contact, as diffusion exposure | T6.10 (PROPOSED) · then M10+ |

## 5. Changes and reasons

| Node | Change | Reason | Cost |
|---|---|---|---|
| heavy_plough | relocate; prereq → `plough_ard AND iron_bloomery AND wheel_solid` | §3 above | 3,520 → 4,970 (prereq_complexity 0.5 → 1.0) |
| horse_collar | relocate | harness craft; prereq `collar_harness` is trunk | 2,090 unchanged |
| rice_champa | relocate | state dissemination of a variety, not scholarship | 1,480 unchanged |
| sugar_refining | relocate; prereq `fermentation_fruit AND distillation` → `root_crop AND pottery_open_fired` | see note (a) | 2,090 unchanged; closure 60,810 → 6,690 |
| cereal_cultivation | material 2.0 → 0.0; `emerged` names the independent centres | see note (b) | 3,520 → 880 |
| cereal_domesticated | material 2.0 → 0.0; name → "Wheat and barley domestication" | the Southwest Asian founder package, one of five regional domestications | 3,520 → 880 |
| maize | material 2.0 → 0.0 | same inconsistency | 4,970 → 1,240 |
| selective_breeding | prereq + `(sheep_goat OR cattle)` | its closure contained no animal | 2,960 → 4,180 |
| mechanised_agriculture | prereq + `farm_machinery`; `emerged` re-dated | see note (f0) | 16,730 → 23,650 |
| drip_irrigation | prereq + `(irrigation_basin OR irrigation_canal)` | an irrigation technique with no irrigation in its closure | 23,650 → 33,450 |
| orchard | prereq → `cereal_cultivation OR root_crop` | see note (c) | unchanged |
| terrace | prereq → `stone_dry AND (cereal_cultivation OR root_crop)` | its own `emerged` cites Andean (tuber) terraces | unchanged |
| pig | prereq `sheep_goat` → `dog_domestication` | see note (d) | unchanged |
| fermentation_grain | prereq → `(cereal_domesticated OR rice_wet OR millet OR maize OR sorghum_pearl_millet) AND pottery_open_fired` | brewing arose with every regional grain | unchanged |
| seed_drill_tull | European route `(crop_rotation_norfolk AND horse_collar)` → `(plough_ard AND horse_collar)` | a drill needs a furrow-opening share and horse draught, not a field rotation | unchanged |
| green_revolution | prereq `genetics_mendel` → `scientific_plant_breeding`; + `chemical_fertilizer` | see note (e) | unchanged (≥3 requirements already) |

**Notes on the reasons:**

- **(a) sugar_refining.** Crystallising sugar needs a cane crop and boiling vessels. Cane is propagated clonally from
  stem cuttings (INFERENCE for the knowledge link). Fermentation consumes sugar, and distillation is not used to
  crystallise it, so both old edges were non-causal.
- **(b) cereal_cultivation.** The node is the generic pre-domestication step before every regional cereal. Material 2.0
  is the loader maximum, above `tin_bronze` (1.5). The crop siblings all score 0.0.
- **(c) orchard.** Fruit cultivars are kept true by vegetative propagation, which is root_crop's technique (RESEARCHED).
- **(d) pig.** Pigs were domesticated independently in China, about 6,000 BCE, before sheep and goats arrived there in
  the 4th to 3rd millennium BCE (RESEARCHED).
- **(e) green_revolution.** The HYVs were bred by breeding science to respond to NPK fertilizer. Genetics alone does
  not breed a variety, and nitrogen alone does not feed it.
- **(f0) mechanised_agriculture.** The combine descends from the reaper and the thresher. The `emerged` anchor moves
  from "~1,920 CE" (mass adoption) to the node's emergence, as D09 asked: Froelich's 1892 petrol tractor and Holt's
  track-type tractor, tested on 24 November 1904 (RESEARCHED). This lets `tank` (1916 CE) require it for track
  knowledge. The chronology test still passes, because the earliest marked year is now 1892, later than every AND
  prerequisite.

**Cost rules applied.** Every re-score follows the coordinator's rubric:

- Factors stay at the Age prior unless a content reason is stated.
- `prereq_complexity` moves whenever the count of top-level requirements changes.
- Each re-scored node carries per-factor notes in `costRationale`.

## 6. New nodes

| Id | Branch · Age | Prerequisite | RP | Kind | Why it is distinct |
|---|---|---|---|---|---|
| cotton_cultivation | trunk · A2 | `cereal_cultivation OR root_crop` | 440 | technique | see note (f) |
| agricultural_chemistry | agriculture · A8 | `chemistry_quantitative` | 5,910 | science | see note (g) |
| chemical_fertilizer | agriculture · A8 | `agricultural_chemistry AND sulphuric_acid` | 23,650 | application | see note (h) |
| farm_machinery | agriculture · A8 | `(heavy_plough OR iron_mouldboard) AND horse_collar AND crank_connecting_rod` | 19,890 | application | see note (i) |
| scientific_plant_breeding | agriculture · A9 | `genetics_mendel` | 11,830 | technique | see note (j) |

**Notes on the new nodes:**

- **(f) cotton_cultivation.** Cotton was domesticated independently four times (RESEARCHED: Mehrgarh, 6th millennium
  BCE; coastal Peru). It is the fibre route the graph lacked. It also gives the cotton gin (D07) a crop to gin, and D07
  is asked to add the edges.
- **(g) agricultural_chemistry.** Understanding is separated from application:
  - 1813 CE: Davy's *Elements of Agricultural Chemistry*;
  - 1840 CE: Liebig;
  - 1843 CE: Rothamsted.

  All are RESEARCHED. This is the first agricultural knowledge that truly came from organized scholarship.
- **(h) chemical_fertilizer.** Superphosphate was patented in 1842 CE (RESEARCHED). This is a clear case of resource
  identification versus exploitation: the knowledge never reveals a deposit, and its inputs are identified by D11.
  Realization is deferred to M10+.
- **(i) farm_machinery.** This is the horse- and gin-powered mechanisation of farming:
  - 1786 CE: Meikle's thresher;
  - 1828–1834 CE: the Bell, McCormick and Hussey reapers;
  - 1837 CE: Deere's steel plough.

  It fills the century-long gap between Tull's drill and the tractor. The `iron_mouldboard` OR keeps the Chinese route,
  and the China-to-Dutch transmission it assumes is INFERENCE.
- **(j) scientific_plant_breeding.** Genetics is the science; breeding is the technique:
  - 1905 CE: Biffen;
  - 1908 CE: Shull's hybrid maize;
  - 1926 CE: hybrid seed sold;
  - 1935 → 1942: Iowa adoption rose from 6% to almost 100%.

  It is the real parent of `green_revolution`.

New keys came out as 427–431 in the lane-only run. The final keys are assigned by `integrate.py` across all lanes.

Every new `emerged` text carries an era marker, so the 83 marker-free count is unchanged. Every AND-prerequisite is
dated earlier than its dependant.

## 7. Alternative routes

None of these routes adds a node to any path:

- **Tuber farmers** can reach orchards, terraces, sugar crystallisation and cotton through `root_crop`. They are not
  forced through cereals.
- **Any domesticated grain** can be brewed: barley, rice, millet, maize or sorghum.
- **Pigs** follow the dog route: a China-like centre does not need sheep and goats first.
- **Iron ploughs** come from the European heavy plough or the Han iron mouldboard (`farm_machinery`).
- **Tull-type drills** come from the Han drill, or from the ard plus the horse collar.

**Existing alternatives kept:** the five regional cereal domestications under generic cultivation, the root-crop root
of farming, `seed_drill` versus `seed_drill_tull`, and `maize OR root_crop` in `new_world_crops`.

## 8. Candidates considered and REJECTED

| Candidate | Why rejected |
|---|---|
| `sickle` → `cereal_domesticated` edge | The sickle-selection hypothesis (Hillman and Davies 1990) is questioned by later work, and uprooting also selects. The existing description's claim should be softened in the corpus stage. `sickle` keeps a deferred owner. |
| Veterinary science node | No consumer: livestock disease is not simulated, so it would be a dead end. Rinderpest and anthrax are possible M9 material; left to D10's `vaccine_lab` scope. |
| Chemical pest control (Paris green 1867, Bordeaux mixture 1885, DDT) | No pest or crop-loss state, so it would be a knowledge-only dead end. DDT's strongest coupling is malaria (D10). |
| Separate hybrid-maize node | It is one technique with one consumer; folded into `scientific_plant_breeding`. |
| Separate improved-plough node (Rotherham 1730, Small, Ransome, Deere) | Same realization as the reaper (an implement good); folded into `farm_machinery`. |
| Splitting three-field rotation from `heavy_plough` | No consumer apart from the same deferred fallow effect; it stays a technique string. |
| Agricultural experiment station institution | A new entity kind is out of lane. The `agricultural_university` type exists. |
| Tile drainage; guano-trade node; aquaculture; chinampas and raised fields; milpa intercropping; manioc detoxification; chuño freeze-drying; scythe and hay | No distinct consumer. Each would be a knowledge-only dead end, i.e. click inflation. |
| `oil_press` ← `(orchard OR flax)` for linseed and sesame oil | The early linseed-pressing dates could not be verified. |
| `cattle` independent of `sheep_goat` (African cattle) | Contested; the edge is kept as INFERENCE. |
| `heavy_plough` breadth 0.0 → 0.5 | It would match the other tillage nodes, but it would raise the A6 core node to 7,030 RP. Noted as a cost finding only. |
| Raising `maize` difficulty for its slow domestication | Not re-scored; only the inconsistent factor was changed. |
| Relocating the A7 practitioner nodes to the trunk | V17 convention; no coupling (see §3). |
| Applying the A2 and A6 core-fact OR widening directly | Age content, not lane content; raised as OPEN DECISIONS. |
| Retiring `new_world_crops` in favour of diffusion | Would need the alias/remap seam; raised as an open question. |

## 9. Conflations found

- **Research versus production (Doc≠code).** Farming yield is gated by research, although the content documentation
  says production is unchanged. Herding is only a label. Both are now stated in the capability blocks.
- **Age entry versus subtree opening.** An Age-based classification artefact tied Age VI to the university stage.
- **One centre's chronology presented as a causal chain.** Five edges did this:
  - `heavy_plough` ← Han cast iron;
  - `pig` ← `sheep_goat`;
  - beer ← Levant cereals only;
  - sugar ← fermentation and distillation;
  - Tull ← Norfolk rotation.
- **Contact or diffusion versus invention.** `new_world_crops`.
- **Resource identification versus exploitation.** `salt_extraction` and `chemical_fertilizer`: the inputs are listed,
  identification is requested from D11, and no deposit is ever revealed.
- **Science versus application.**
  - `agricultural_chemistry` versus `chemical_fertilizer`;
  - `genetics_mendel` versus `scientific_plant_breeding` versus `green_revolution`.
- **Name versus content.** "Domesticated cereal" was in fact the Southwest Asian package.

## 10. Dead ends resolved

All 15 D01 dead-end warnings (V09) now carry a `deferredEffect` and a milestone:

| Node(s) | Milestone |
|---|---|
| fishing_hook, sickle, pig, milking, beekeeping, rice_champa, sugar_refining, new_world_crops, canning, green_revolution, drip_irrigation, precision_agriculture, frontier_crops, frontier_climate | M10+ |
| nixtamalization | M9 |

- `iron_mouldboard` lost its dependant `heavy_plough`. It gains `farm_machinery` through an OR route and is the subject
  of the A6 OPEN DECISION.
- No new dead end was created: `distillation`, `fermentation_fruit`, `crop_rotation_norfolk` and `genetics_mendel` keep
  other dependants.

## 11. Cost findings

**Workload.** The lane's workload rises from 279,950 to 352,050 RP (+72,100):

| Source of change | RP |
|---|---|
| New nodes | +61,720 |
| Re-prices by the prerequisite convention | +19,390 |
| Material re-score | −9,010 |
| **Net** | **+72,100** |

By Age, the change is A2 −8,570, A6 +1,450, A7 +1,220, A8 +49,450 and A9 +28,550. The A9 addition is about 0.3% of A9's
8.91M RP. No node approaches the 757k cap.

If the accepted D04 override on `refrigeration` is applied, it adds +9,800 RP at A8, bringing the lane's net change to
+81,900.

**Gates and closures:**

| Closure | Before | After |
|---|---|---|
| A2 gate (`cereal_cultivation`) | 3,890 | 1,250 |
| A6 gate (with the stage before) | 82,410 | 27,570 |
| `sugar_refining` | 60,810 | 6,690 |
| `seed_drill_tull` | 23,840 | 17,550 |
| `green_revolution` | 900,700 | 978,510 |

**Material finding.** Within A1–A3, `material ≥ 2.0` appeared only on the three crop nodes. At A2 the scale runs 0.75
for an engineered material or tool kit (pottery, kiln, mudbrick, sickle) and 1.5 for chemically processed materials
(lime plaster, dye). Cultivating a plant uses neither. The re-score rests on content alone, not on pacing (D-046 §1).

**Pacing consequence (Director decision).** The re-score lowers the A2 CORE node, so the AI works the A2 core before
the first road at turn 0. `AiPolicyTests` pins the opposite premise: 3,850 < 3,890.

## 12. Compatibility summary

- **Keys and saves.**
  - No key changes and no retired ids, so no alias or remap seam is needed for D01.
  - Relocation makes dormant pre-stage progress live.
  - A lowered cost completes stored progress at or above the new cost for free.
  - A prerequisite change can leave progress dormant ("Partial") until the new prerequisites are met.
- **ResearchContentTests.**
  - branch counts change: trunk 182, agriculture 15;
  - finite nodes per subtree: agriculture stays 14;
  - `TechnologyCount` and node positions change, as they do for any appended node;
  - the corpus-name sync test needs `cereal_domesticated` in its renamed-node map;
  - the 83 marker-free count and the chronology test pass, by the MEASURED heuristic.
- **AI tests and goldens.** The turn-0 goal premise in `AiPolicyTests` inverts because of the `cereal_cultivation`
  re-score. AI-empire goldens move.
- **Fixtures and goldens left unchanged.**
  - None of the allocation-scenario keys (12/17/2/8/52/74/53) or the v33 fixture key (2) is a D01 node, and none of
    their closures changes.
  - Order-less goldens are unaffected: no root gains an evaluable Eureka, and no null-requirement entity or sector
    identity changes.
- **CI.** The three CI `--check` documents regenerate on any content change.

## 13. Cross-lane requests and replies (also in the JSON)

### Requests from D01

| Lane | Request |
|---|---|
| D11 | Identification knowledge for phosphate rock and guano, potash salts, nitrate (caliche) and salt (brine, rock salt). These are the inputs of `chemical_fertilizer` and `salt_extraction`. |
| D07 | Add `cotton_cultivation` to `spinning_spindle`'s fibre OR, and as a prerequisite of `cotton_gin_roller`. Consider `cereal_cultivation OR root_crop` for the irrigation nodes. FYI: `crank_connecting_rod` is now used by `farm_machinery`, and the irrigation nodes now feed `drip_irrigation`. |
| D02 | `lacquer` ← `fermentation_fruit` is non-causal: urushi is cured tree sap. |
| D10 | Name `nixtamalization` (and `canning`/`refrigeration`) as consumers if M9 models diet-deficiency disease. Veterinary medicine is left to D10. |

### Replies to requests other lanes sent to D01

These replies were read from the other lanes' parts at integration time.

| Lane | Their request | D01 reply |
|---|---|---|
| D04 | Make `refrigeration` require their new `thermodynamics`. | **Accepted, as an integration override.** Set `refrigeration` to `chemistry_quantitative AND steam_rotary AND thermodynamics`, `prereq_complexity` 1.0, `dispositionAdd` rewire. Cost moves 23,650 → 33,450 (V19). `thermodynamics` is A8 with earliest year 1824 CE, so the Age and chronology checks pass. Also: keep the Ages of `chemistry_quantitative`, `sulphuric_acid` and `genetics_mendel`, which gain new D01 dependants. |
| D09 | Re-date `mechanised_agriculture` so that `tank` can require it (Holt's crawler). | **Done** (note f0). D01 agrees to the edge; D09 or the integrator adds it to `tank`. Both nodes are A9. |
| D03 | Re-examine `sugar_refining`'s fermentation and distillation prerequisites. | **Done independently**: the prerequisite is now `root_crop AND pottery_open_fired`. FYI: `pig` now depends on `dog_domestication`. |
| D08 | FYI: `horseshoe` no longer needs `horse_collar`; `collar_harness` is renamed "Breast-strap harness". | **Acknowledged.** This resolves both points D01 would have raised: `horseshoe` into the trunk, and the collar being claimed twice. |
| D12 | If `cereal_cultivation` or `sheep_goat` is split, keep the keys on the domestication parts. | **Acknowledged: no split.** Both keep their keys and content. |
| D05 | Acknowledged that the `heavy_plough` relocation removes the stage from the A6 core. | Nothing further. |

## 14. Open questions

1. **OPEN DECISION — A6 core fact.** Should `a6_heavy_plough` accept regional equivalents?
   - `heavy_plough OR iron_mouldboard` (heavy-soil mouldboard ploughs);
   - possibly `rice_champa` as well.

   The AI would then work the union closure, starting from its cheapest available member.
2. **OPEN DECISION — A2 core fact.** Should `a2_cultivation` be `cereal_cultivation OR root_crop`, for the New Guinea,
   Andes and West Africa vegeculture centres? `root_crop` is a 440-RP root node, so AI A2 entry would come much earlier
   and the AI tests and goldens would move.
3. **Director decision — material re-score.** The re-score of three crop nodes changes ratified content (digest X-08)
   and moves the A2 core cost. It is applied here as PROPOSED.
4. **Crop transfer.** Should crop transfer become diffusion exposure under T6.10? If so, should `new_world_crops` stay
   as acclimatisation knowledge, or be retired, which needs the alias seam?
5. **Crop-package suitability.** Which owner and milestone should make the regional crop packages differ mechanically?
6. **Agricultural goods and states.** Fertilizer, implements, seed varieties, salt, sugar and preserved food carry most
   of the deferred effects in this lane. Which milestone owns them? Framework §10 says M11+ for new goods, while the
   PROPOSED T6.11 realizes tiers C–D.

## 15. Sources for RESEARCHED claims (accessed 2026-10-22)

- **Heavy plough:**
  - [Carruca (Wikipedia)](https://en.wikipedia.org/wiki/Carruca)
  - [Plough (Wikipedia)](https://en.wikipedia.org/wiki/Plough)
- **Han iron mouldboard and the Rotherham plough:**
  - Bray, in *Tools & Tillage* ([Heidelberg digitisation](https://digi.ub.uni-heidelberg.de/diglit/tools_tillage1976_1979/0241))
  - [History of the plough](https://ploughmen.co.uk/about-us/history-of-the-plough)
- **Pig domestication:**
  - [Larson et al. 2010, East Asian pig domestication (FAO AGRIS record)](https://agris.fao.org/search/ar/records/65df6a62b766d82b18019a20)
  - [sciencedaily 2010](https://sciencedaily.com/releases/2010/04/100419150947.htm)
- **Sheep in China:**
  - [Brunson, Taosi/Zhoujiazhuang (NSF BCS-1249600)](https://grantome.com/grant/NSF/BCS-1249600)
  - [ACLS project summary](https://acls.org/?p=10577)
- **Sickle hypothesis questioned:**
  - [Shattering (agriculture)](https://en.wikipedia.org/wiki/Shattering_(agriculture))
  - [UCL QSR 2016](https://discovery-pp.ucl.ac.uk/id/eprint/1502468/1/1-s2.0-S0277379116301780-main.pdf)
- **Fruit-tree vegetative propagation:**
  - [Zohary and Spiegel-Roy revisited](https://cris.biu.ac.il/en/publications/beginnings-of-fruit-growing-in-the-old-world-two-generations-late-3/)
  - [Frontiers in Plant Science 2022](https://www.frontiersin.org/journals/plant-science/articles/10.3389/fpls.2022.719406/epub)
- **Champa rice, 1012 CE:**
  - [Champa rice (Wikipedia)](https://en.wikipedia.org/wiki/Champa_rice)
  - [EBSCO research starter](https://www.ebsco.com/research-starters/history/rice-introduced-china/)
- **Sugar crystallisation (Gupta, about 350 CE) and New Guinea sugarcane:**
  - [History of sugar](https://en.wikipedia.org/wiki/History_of_sugar)
  - [PMC review](https://pmc.ncbi.nlm.nih.gov/articles/PMC9483297/)
- **Cotton:**
  - [Mehrgarh cotton (harappa.com)](https://www.harappa.com/content/first-evidence-cotton-neolithic-mehrgarh-pakistan)
  - [Huaca Prieta](https://en.wikipedia.org/wiki/Huaca_Prieta)
  - [Viot and Wendel 2023 (HAL)](https://hal.inrae.fr/hal-03976374)
  - [Cotton (Wikipedia)](https://en.wikipedia.org/wiki/Cotton)
- **Davy 1813 and Liebig 1840:**
  - [Davy, *Elements of Agricultural Chemistry*](https://www.archive.org/download/elementsofagricu00davy/elementsofagricu00davy_bw.pdf)
  - [UCL Discovery](https://discovery.ucl.ac.uk/id/eprint/1472128/)
  - [Düsseldorf (Liebig 1840)](https://lessing.ub.uni-duesseldorf.de/download/pdf/2396265.pdf)
- **Lawes' superphosphate (1842) and Rothamsted (1843):**
  - [Harpenden history](https://www.harpenden-history.org.uk/harpenden-history/topics-cms/farms-and-farming/a_brief_history_of_rothamsted_experimental_)
  - [MERL, Reading](https://www.reading.ac.uk/adlib/Details/archive/110167853)
- **Meikle (1786; patent 1788):**
  - [Britannica](https://www.britannica.com/biography/Andrew-Meikle)
- **Reapers: Bell, McCormick, Hussey:**
  - [Reaper (Wikipedia)](https://en.wikipedia.org/wiki/Reaper)
  - [Science Museum Group](https://collection.sciencemuseumgroup.org.uk/objects/co38745)
- **Biffen (1905) and the Plant Breeding Institute:**
  - [Cambridge Core](https://resolve.cambridge.org/core/journals/journal-of-agricultural-science/article/mendels-laws-of-inheritance-and-wheat-breeding/F25D57623D2060215A4293AC0F414FDE)
  - [Plant Breeding Institute](https://en.wikipedia.org/wiki/Plant_Breeding_Institute)
- **Shull 1908 and hybrid-maize adoption:**
  - [Iowa State farm reports](https://www.iastatedigitalpress.com/farmreports/article/id/4151/)
  - [Wallace Hi-Bred (Farm Progress)](https://www.farmprogress.com/corn/how-henry-wallace-ushered-in-hybrid-seed-corn-)
- **Froelich's petrol tractor (1892) and Holt's track-type tractor (24 November 1904):**
  - [John Froelich (Wikipedia)](https://en.wikipedia.org/wiki/John_Froelich)
  - [Caterpillar, "It All Began on Thanksgiving Day 1904"](https://www.caterpillar.com/en/news/caterpillarNews/history/thanksgiving-2020-ttt.html)
  - [Fordson centenary, 2017 (Modern Farmer)](https://modernfarmer.com/2017/12/happy-100th-birthday-mass-produced-tractor/)
- **Deere's steel plough (1837):**
  - [National Inventors Hall of Fame](https://invent.org/inductees/john-deere)
  - [Lemelson-MIT](https://lemelson.mit.edu/resources/john-deere)

  John Lane of Lockport, Illinois is credited with a steel plough in 1833. The `emerged` text therefore does not claim
  Deere's plough was the first.
