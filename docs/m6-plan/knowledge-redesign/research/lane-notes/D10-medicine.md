# D10 — Medicine and public health: lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified, and nothing is runtime content.**
Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED.
Canonical data: `data/proposal-parts/D10-medicine.json`. This file explains it. Baseline: `data/current-tree-inventory.json`
(HEAD `edba2b5`).

## 1. Summary

D10 owns 27 nodes: 5 trunk (A2–A5) and 22 in the Medicine subtree (A7–A9). The tree had four problems:

1. It had **no public-health knowledge**. Nothing covered how disease distribution, transmission and treatment effects
   were established.
2. **Three sciences** (physiology, immunology, endocrinology) were missing. Their applications reached them through
   proxies or non-causal edges: genetics → contraception, anaesthesia → transfusion, germ theory → cancer chemotherapy.
3. **Eleven dead ends** had no owner.
4. A few **costs** were inconsistent with their peers.

The proposal makes these changes:

- **Keep 18 nodes.** One of them gets a corrected `emerged` date.
- **Split 1.** `anatomy_dissection` becomes anatomy plus a new `physiology` node.
- **Rebalance 8.**
  - Four are prerequisite repairs with unchanged cost.
  - Four are re-scores: two under the prerequisite convention and three for the material factor. `chemotherapy`
    counts in both groups.
- **Add 5 nodes:** `physiology` (A7), `epidemiology` (A8), `immunology`, `endocrinology` and `sulfonamides` (all A9).

Some things are not touched:

- the research-stage atom `medicine_hippocratic` and all of its consumers;
- both Age facts (`germ_theory` for A8 and `antibiotic_penicillin` for A9);
- the edges D-047 ruled on (`xray`, `anaesthesia`, `antisepsis`);
- the penicillin-closure pin;
- the `oral_rehydration` cost pin (740).

Every dead end now has an owner. Most will enter the M9 disease model (§3). The military hospital is M7.
`trepanation` stays knowledge-only, with an explicit justification.

## 2. Dispositions

| disposition | n | nodes |
|---|---|---|
| keep | 18 | trepanation (emerged corrected), medicine_recorded, medicine_hippocratic, pharmacology, surgery_instruments, inoculation, vaccination, anaesthesia, germ_theory, antisepsis, antibiotic_penicillin, antibiotic_broad, vaccine_lab, xray, organ_transplant, mrna_vaccines, oral_rehydration, frontier_medicine |
| split | 1 | anatomy_dissection → physiology |
| rebalance | 8 | antibiotic_resistance, blood_transfusion, hormonal_contraception, chemotherapy, medical_imaging, antiviral_drugs, monoclonal_antibodies, ivf |
| merge / replace / relocate / retire | 0 | — |
| new | 5 | physiology, epidemiology, immunology, endocrinology, sulfonamides |

The disposition vocabulary has no word for a prerequisite repair that leaves the factors unchanged. For those, this
lane uses `rebalance`, because `prereq_complexity` is re-evaluated (see open question 2).

### 2.1 Split

**anatomy_dissection (key 241) → anatomy_dissection + physiology.** The node bundled two pieces of knowledge, each
useful on its own:

- Vesalian structural anatomy (1543). The surgery line needs it: anaesthesia → antisepsis → transplant.
- Harvey's circulation (1628), carried as the capability string "circulation (Harvey 1628)". Transfusion and
  endocrinology need it.

The key stays with anatomy. Its consumer, prerequisites, Age, cost (2,960), family (surgery) and generation (3) are
unchanged.

The split sum is 2,960 + 8,360 = 11,320, which is 3.8 times the original. That is more than double, and the reason
is this: the circulation was an unpriced string on a node scored as an incremental refinement (novelty 0.5). It is new
priced knowledge, not a re-division of existing workload.

Old saves that completed anatomy_dissection do not hold physiology. No alias or remap seam exists; that is the
package's open decision.

### 2.2 Rebalances

| node | change | old → new cost | reason |
|---|---|---|---|
| antibiotic_resistance | prereq gains `epidemiology`; prereq_complexity 0.5 → 1.0 | 23,650 → 33,450 | Resistance is managed through surveillance and controlled trials. The 1948 MRC streptomycin trial exposed resistance under monotherapy and led to combination therapy. `evolution` (selection) stays. |
| blood_transfusion | `germ_theory AND anaesthesia` → `immunology AND physiology` | unchanged 16,730 | Blood groups (1901) came from serology, and transfusion rests on the circulation. Anaesthesia is not knowledge transfusion needs. Germ theory stays in the closure through immunology. |
| hormonal_contraception | `chemistry_quantitative AND genetics_mendel` → `endocrinology AND synthetic_dye` | unchanged 47,310 | The pill needs reproductive endocrinology and synthetic steroid chemistry. Mendelian genetics was an adjacency edge. |
| ivf | `hormonal_contraception` → `endocrinology` | unchanged 16,730 | IVF rests on reproductive endocrinology and embryo culture. The pill is a sibling application of the same science, and the 1978 birth came from a natural cycle. |
| monoclonal_antibodies | `dna_structure AND vaccine_lab` → `dna_structure AND immunology` | unchanged 47,310 | Hybridomas are applied immunology; laboratory vaccines were a proxy. |
| chemotherapy | `chemistry_quantitative AND germ_theory` → `sulfonamides`; material 0 → 1.5; prereq_complexity 0.5 → 0; generation 2 → 3 | 8,360 → 16,730 | See below. |
| medical_imaging | material 0 → 1.5 | 8,360 → 23,650 | CT and MRI are defined by their apparatus. Material 0.0 was below its own parent `xray` (1.5). |
| antiviral_drugs | material 0 → 1.5 | 8,360 → 23,650 | Nucleoside-analogue and HIV drugs are industrial pharmaceutical chemistry (peer `hormonal_contraception` 1.5). |

More on chemotherapy:

- It grew from Ehrlich's chemotherapy programme and from the antimetabolite concept that sulfonamide research
  produced (Woods 1940).
- Farber's antifolate remissions (1948) belong to that line.
- The lineage between the two is partly INFERENCE.
- Germ theory stays in its closure through sulfonamides.

## 3. The M9 channels used in `deferredEffect` (PROPOSED vocabulary, not a design)

Medicine has almost no owner before M9. The architecture outline's Tier 1 system table places **Health & disease**
as a T2 system: "SIR on trade network; sub-step rule" (the sub-step rule itself is Spine S3). The 2026-10-03 rebase
moved it to M9 Society. The outline's build spine also names plague as a crisis archetype.

Each deferred effect names the channel where the node's knowledge would act. These are channels, not mechanisms. M9
designs the equations.

| channel | what it is | MEASURED anchor today |
|---|---|---|
| transmission | per-contact spread in the SIR model on the trade network | none: there is no disease code |
| susceptibility | immunity moves susceptibles out of the pool | none |
| case fatality | treatment lowers deaths among the infected | none |
| cohort base mortality | non-epidemic deaths by age cohort (16 cohorts, `demographics.mortalityPerYear`) | realized today: `InstitutionEffects.MortalityMultiplier`. Medical-university maturity cuts base mortality by up to 10 %, and reads no medical knowledge. |
| fertility | DemographicsSystem fertility | realized: `fertilityPerPersonPerYear` |
| Health need | D-018 need 4 | unbound today (`needs.json`, `bound: false`) |
| M7 casualties | battle casualties, debited from cohorts through the Ledger (D-011) | M7, not built |

Every effect enters as a coefficient inside an equation, never as a flat modifier (law 2). Whether knowledge should
also enter the existing medical-university equation is open question 1.

## 4. New nodes

| id | Age | prereq | factors (n/d/m/i/b/p) | cost | analog | cheapest closure | why it carries weight |
|---|---|---|---|---|---|---|---|
| physiology | A7 | anatomy_dissection | 2/2/0/1.5/1/0 | 8,360 | heliocentrism | 26 nodes / 112,900 | Function, not structure. Parent of transfusion and endocrinology. Split from anatomy_dissection. |
| epidemiology | A8 | medicine_hippocratic AND hindu_arabic | 1/2.5/0/2/1/0.5 | 11,830 | vaccination | 11 nodes / 29,710 (+ research stage) | The missing public-health pillar. Prerequisite of antibiotic_resistance. Basis for the D07 and D12 requests. One future-system Eureka (cholera epidemic). |
| immunology | A9 | vaccine_lab | 1/3/0.75/2.5/1/0 | 28,130 | vaccine_lab | 38 / 201,360 | Common science behind serum therapy, blood typing, transplant rejection and monoclonals. Replaces two proxy edges. |
| endocrinology | A9 | physiology AND chemistry_quantitative | 1/3/0.75/2.5/1/0.5 | 39,780 | genetics_mendel | 45 / 264,820 | Real parent of the reproductive line. Adds insulin (1922), which was absent until recombinant insulin (1982). |
| sulfonamides | A9 | germ_theory AND synthetic_dye | 1/3/1.5/2.5/0.5/0.5 | 47,310 | hormonal_contraception | 56 / 349,260 | First synthetic antibacterials. A genuinely different route from penicillin. Began the fall in maternal mortality. Parent of cancer chemotherapy. |

All five sit in the Medicine subtree, use the medical university as primary, carry era-marked `emerged` texts, and are
optional (on no Age or stage gate). Every `costRationale` has a per-factor note under the cost-rubric addendum: the Age
prior for difficulty and institutional, breadth as downstream scope, material not inflated for theories, and novelty
2.0 only for physiology (paradigm-founding).

Families:

- `immunology` is immunisation generation 4, after vaccine_lab at 3.
- `sulfonamides` is pharmacology generation 2. `chemotherapy` moves to 3, so the D-047 generations test holds.
- `physiology`, `epidemiology` and `endocrinology` have no family.

## 5. Candidates considered and REJECTED

| candidate | why rejected |
|---|---|
| Pre-literate herbal medicine root (A1–A2) | It would sit on the research-stage closure (stage cost and AI churn) and add no capability. `trepanation` already marks pre-literate medicine. |
| Medieval or Islamic-world hospital medicine node (bimaristan, Canon) | No distinct dependency. Hospitals are already eligible through clinical medicine plus pharmacology. Belongs in the corpus of those nodes as a parallel tradition. |
| Contagion and quarantine node | Quarantine (Ragusa 1377) rests on empirical contagion, which clinical medicine already holds. Its administration is a civic; D12's `public_health_board` covers it. |
| Nursing and hospital hygiene (Nightingale) | An institutional practice realized by `inst.hospital`. Its statistics belong to epidemiology. No separate consumer. |
| Virology and cell culture | Only `antiviral_drugs` would depend on it. `vaccine_lab`'s polio half cannot depend on it without a split, because the chronology rule fails: virology 1892 is later than vaccine_lab's 1885. A T2 SIR model is not expected to distinguish pathogen classes. Revisit at M9 (open question 5). |
| Split vaccine_lab (1885 Pasteurian vs 1955 viral vaccines) | Same reason. |
| Split penicillin (discovery vs deep-tank production) | It is the A9 Age fact and its closure is pinned. The production half's owner is M11+ industries, with no separate consumer. It is under the 100k bottleneck band. |
| Split xray (discovery vs radiography) | The first clinical radiographs came weeks after the 1895 discovery. No capability gap separates them. |
| Split medical_imaging (CT vs MRI) | MRI's NMR physics and superconducting magnets are real, but both feed one diagnostic channel at M9. Bundle noted. |
| OR route for anaesthesia (Hanaoka 1804, herbal) | D-047 ruled the edge KEEP, and the herbal route did not diffuse. Recorded as a parallel tradition only. |
| germ_theory + chemistry_quantitative | Sterilisation, culture and staining rest on microscopy, glass and heat. It would also raise the A8 fact's cost. Not knowledge strictly needed. |
| trepanation → surgery_instruments edge | The instrument set does not need cranial surgery. The edge would express family lineage, not causality (D-044 R7). |
| oral_rehydration: germ_theory → physiology | Causally better, and it keeps the count and the 740 cost. But it would move a large M9 effect onto an even earlier closure, on a Director-cited node. Deferred to M9 calibration. |
| blood_transfusion + refrigeration | Blood-bank cold storage is an infrastructure input, not knowledge. Direct transfusion (1901–1914) needed none. |
| ivf + antisepsis (laparoscopic retrieval) | Generic hospital surgery by 1978. The distinctive knowledge is endocrine timing and embryo culture. |
| Re-score organ_transplant and blood_transfusion material | The 1954 transplant was surgical technique, and transfusion apparatus is simple. Drugs and cold storage are inputs. |
| Cellular pathology, nutrition and vitamins | Cell theory is D04's domain (request (4)). Vitamins have no consumer today; `nixtamalization` (D01) already covers the pellagra case as a parallel tradition. |
| Radiotherapy, tracers, nuclear medicine, radiation protection | Lane D11's, by the brief. |

## 6. Conflations found

- **Understanding vs application.**
  - Anatomy and physiology were bundled; now split.
  - Immunology, physiology and endocrinology were represented only by their applications; now added.
  - `xray` bundles discovery and radiography; kept, as they were simultaneous.
- **Discovery vs production.** `antibiotic_penicillin` bundles 1928 with 1943. Kept (Age fact and pin). The drug
  supply question is open question 7.
- **Eligibility vs adoption.** These capability strings name outcomes that need adoption, not knowledge: "eradication
  (1980)", "fertility as a chosen variable", "childhood mortality collapse", "maternal mortality collapse" and "child
  mortality from diarrhoea collapses". The deferred effects say that coverage and adoption (M8 policy, M9 culture and
  religion) set the reach. The wording is left to the corpus stage (open question 4).
- **Single route to sanitation.** `infra.sewerage_system` is reachable only through germ theory. Historically the
  sanitary movement came first, driven by epidemiology and miasma theory. This is a request to D07.
- **Bundles noted, not split:**
  - `vaccine_lab`: 1885 Pasteurian vaccines with 1955 cell-culture viral vaccines.
  - `medical_imaging`: CT with MRI.
  - `medicine_hippocratic`: its "pharmacopoeia" and "surgery (Sushruta)" strings overlap pharmacology and
    surgery_instruments. Kept, because it is the stage atom.

## 7. Dead ends

Today there are eleven D10 dead ends (validator V09). All are resolved; the dry run (§11) shows no warning on any D10
node.

| node | resolution |
|---|---|
| trepanation | Knowledge-only by explicit justification: the earliest surgery, kept for educational value. M9 may give it none. |
| antibiotic_resistance | M9: slows the decay of antibiotic efficacy as resistance accumulates with use. Resistance is the natural brake on the case-fatality term. |
| antiviral_drugs | M9 case fatality and chronic burden of viral infection |
| chemotherapy | M9 cancer deaths in older cohorts |
| medical_imaging | M9 diagnostic coefficient inside treatment terms |
| monoclonal_antibodies | M9 targeted therapy and diagnostics |
| mrna_vaccines | M9 epidemic response: shortens the time to vaccine availability (plague archetype) |
| oral_rehydration | M9 diarrhoeal case fatality in child cohorts 0–9 |
| organ_transplant | M9 organ-failure deaths in older cohorts |
| ivf | M9 fertility channel (small term) |
| frontier_medicine | Repeat mechanics are deferred (D-046). Once ruled, each level enters cohort base mortality. Milestone M10+. |

**New dead end created:** `hormonal_contraception`, after `ivf` was re-pointed. Its owner is the M9 fertility
channel, with adoption by culture and religion, the strongest demographic effect in the lane.

**Owners that are not M9:**

- `surgery_instruments` → `inst.military_medicine`, M7: fewer of the battle wounded die.
- `pharmacology` → `inst.hospital`, which T6.11 can found only once it has a consumer, so M9 in practice.

## 8. Cost findings

**Age prior.** All 27 nodes follow the Age prior, with three exceptions, each justified and not re-scored:

- `surgery_instruments`: institutional 1.75 against a prior of 1.25, for an organised military medical service.
- `germ_theory`: difficulty 3.0 against 2.5.
- `oral_rehydration`: difficulty 0.5 and institutional 1.0, a documented D-045 exception.

**prereq_complexity.** Every node follows the ledger convention. Every changed count moved the factor.

**A cloned template.** Five A9 nodes (organ_transplant, chemotherapy, medical_imaging, antiviral_drugs and
frontier_medicine) shared one factor set: 0.5/3/0/2.5/0/0.5 = 8,360. D-045 says siblings should not be identical
unless warranted. Three were re-scored on material, each with a content reason. organ_transplant was kept, as a
surgical technique. frontier_medicine was kept, because repeat pricing is not redesigned.

**Workload.**

| | cost |
|---|---|
| new nodes | +135,410 |
| re-scores | +48,750 |
| Medicine branch | 537,090 → 721,250 |
| D10 lane, including trunk | 545,980 → 730,140 |

All of it is optional (no gate). The research stage, Age facts and all trunk costs are unchanged. Pacing is the
Director's demography and RP question (D-046 §1). No cost was chosen to move it.

**epidemiology's closure** is only `medicine_hippocratic` and `hindu_arabic`, plus the research stage. It can therefore
be taken soon after the stage, earlier than its A8 metadata. This is the same pattern as `inoculation`. Its own
cost (11,830) is the practical gate.

**xray breadth (0.5).** If D04 and D11 adopt `xray` as a prerequisite, breadth should become 1.0 (66,910 → 94,620).
That is left to the integrator once those lanes answer.

## 9. Alternative routes and parallel traditions

- **Antibacterial therapy:** `sulfonamides` (German dye chemistry, 1935) and `antibiotic_penicillin` (British and US
  fermentation, 1940–43) are independent nodes feeding the same M9 channel. This is a genuine regional and industrial
  alternative, not a cost softener.
- **Encoded as OR already:**
  - medical texts: cuneiform, hieroglyphic or Chinese script.
  - Clinical medicine names Greek, Ayurvedic and Chinese systematic traditions. Its display name is already neutral,
    as D03 asked.
- **Recorded in rationale and whyDistinct, not as OR edges**, because no different knowledge route exists in the graph
  or a ruling covers the edge:
  - Andean trepanation.
  - Variolation in China, India, the Ottoman world and West Africa.
  - Ibn al-Nafis's pulmonary transit (13th century).
  - Hanaoka's herbal anaesthesia (1804).
  - Fourteenth-century plague-contagion observations in al-Andalus.
  - Pasteur's and Koch's schools; Metchnikoff's cellular immunology against Ehrlich's humoral immunology.

## 10. Cross-lane requests and coordination

**D07 (industry and construction):** let `infra.sewerage_system` accept `(germ_theory OR epidemiology)`, and check
whether reinforced concrete is needed at all. The London main drainage of 1859–75 used brick and Portland cement. Keep
germ theory on any water-treatment part.

**D12 (civics):**

- D12 asked D10 to keep `medicine_hippocratic` for `public_health_board`. D10 agrees and keeps it.
- D10 supports OD-05's re-points for `inst.quarantine` and `inst.military_medicine`.
- One change requested: for `inst.statistics_vital`, take `epidemiology` instead of `calculus` as the technology half.
- `public_health_board` must not require epidemiology: its 1377 anchor would fail the chronology test.

**D04 (natural science):**

1. Add `xray` to `dna_structure` (X-ray diffraction). The cost does not change.
2. A probability or statistics node, if added, should feed epidemiology.
3. Informational: dependant changes on `synthetic_dye`, `genetics_mendel` and `chemistry_quantitative`.
4. Cell theory hooks, if D04 adds cell theory or cellular pathology.

**D11 (nuclear and geology):** `xray` is available as the causal parent of radioactivity research. Radiation medicine
stays with D11. The ids of `xray` and `medical_imaging` are stable.

**D03 (early science):** `epidemiology` uses `hindu_arabic`. If D03's `counting_rods` is accepted, the integrator may
widen this to an OR. D10 does not name proposed ids.

## 11. Verification (MEASURED, this session)

**Dry run.** I copied `tools/` and `data/` into a scratch directory and ran `integrate.py --allow-incomplete` and
`validate.py --no-corpus` there. The package files were not touched.

| run | errors | warnings on D10 nodes |
|---|---|---|
| D10 part alone | 0 | none (the eleven dead-end warnings are gone) |
| D03 + D10 + D12 parts | 0 | none |

**Replicated `ResearchContentTests` pins** (scratch script over the integrated graph):

| pin | result |
|---|---|
| D-047 chronology (strict AND edges) | 0 violations; 457 dated edges |
| Undated `emerged` texts | still 83 (new nodes carry CE markers) |
| `xray` must-closure | still excludes `vacuum_tube` |
| `canning` must-closure | still excludes `germ_theory` |
| Penicillin's closure | contains `germ_theory` and no surgery-family node |
| Surgery family | 6 nodes |
| Generations local to family | no violation |

**Tests and other items that move when this ships:**

- branch counts: medicine 22 → 27;
- finite per subtree: medicine 21 → 26, and `frontier_medicine`'s `finite_nodes_to_exhaust` with it;
- Eureka count: 81 → 82 on 81 nodes, with the evaluable count unchanged at 17;
- AI-empire goldens;
- the three CI `--check` docs.

No allocation-scenario or v33-fixture key is touched (none of 12, 17, 2, 8, 52, 74 or 53).

**Sources (RESEARCHED).** WebFetch failed with a DNS error and Wikipedia is blocked from this session, so every
claim rests on WebSearch extracts, as lane D11 also found.

- Trepanation date: Lillie 1998, *Nature* 391 (nature.com/articles/36023); *Discover*, "Stone Age Surgery";
  *Archaeology* news brief 1997. Ensisheim c. 5100–4900 BCE; Vasilyevka II 7300–6220 cal BCE.
- Variolation case fatality, 0.5–2 % against 20–30 %: WHO, *Smallpox and its Eradication* (1988), ch. 6; James Lind
  Library (Nettleton 1722; Scheuchzer 1729).
- Sulfonamides and maternal mortality: Loudon 1987, *BMJ* 295:485; London Medical Officer of Health reports.
- Hanaoka 1804: Hektoen International; Izuo 2004, *Breast Cancer*.
- Ibn al-Nafis: PMC2612469; Qatar Digital Library.
- Graunt, "shop arithmetic", weekly bills from 1604: *Encyclopedia of Mathematics*.
- Woods 1940: Bristol "Molecule of the Month"; historyofmedicine.com.
- Farber 1948: James Lind Library.
- Röntgen and Becquerel: D11 brief, NUC-13 and NUC-14.

**Standard dates not re-verified this session; the corpus stage sources them:**

- Harvey 1628; Graunt 1662; Snow 1854; Ragusa 1377; Chadwick 1842; Bazalgette 1859–75; chlorination 1908.
- Behring and Kitasato 1890; Landsteiner 1901; Lower 1665.
- Bayliss and Starling 1902; insulin 1922; Marker and Djerassi (norethindrone 1951).
- Salvarsan 1910; Prontosil 1932–35; MRC streptomycin trial 1948.
- Köhler and Milstein 1975; natural-cycle IVF 1978.

## 12. Open questions

1. **OPEN DECISION (M9).** The realized medical-university mortality seam (ceiling 0.10, justified by smallpox and
   variolation) reads no medical knowledge. Should medical knowledge enter that equation as coefficients, or act only
   through the new disease model?
2. The disposition vocabulary lacks a word for a prerequisite repair; `rebalance` is used here.
3. Entity requirements cannot be expressed in a part file. The `infra.sewerage_system` and `inst.statistics_vital`
   requests join D12's OD-05 for T6.4. Who rules non-civic entity edits?
4. Should the corpus stage reword the adoption-outcome capability strings as eligibility?
5. Revisit virology and a vaccine split if M9 distinguishes pathogen classes.
6. `oral_rehydration`: should the causal repair and the size of its effect be calibrated together at M9?
7. Drug and vaccine supply: knowledge is not production. Should M9 use a production input (M11+ industries) or
   institutional coverage?
8. The alias and remap seam would grant `physiology` to old holders of `anatomy_dissection` (package-wide open
   decision).
