# D04 — Natural science (subtree 1.4): lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified or describes existing behaviour unless it
is labelled MEASURED.** Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED.
Canonical data: `data/proposal-parts/D04-natural-science.json` (41 existing records, 9 new). Baseline: `edba2b5`.
Educational descriptions are not written here (a later stage does that).

## 1. Summary

The natural-science subtree held the right landmarks but lacked most of the sciences that connect them: there was no
thermodynamics, spectroscopy, periodic system, organic chemistry, cell theory, classification or probability, so
quantum mechanics rested on electromagnetism alone, genetics on a bare microscope, synthetic dyes and dynamite on
Lavoisier's general chemistry, and evolution on livestock breeding. Several edges were chronology or mis-readings
(lead *glass* for lead *chambers*, the marine chronometer for the atomic clock, electricity for Haber–Bosch, mine shafts
for refining). The redesign:

- **Two splits where consumers differ.** `electromagnetism` keeps the experimental laws (Volta 1800 → Faraday 1831;
  telegraph, dynamo, generator, battery, X-ray tube) and a new `electromagnetic_field_theory` takes Maxwell and Hertz
  (radio, radar, relativity, quantum theory). `genome_sequencing` keeps genomics (1986–2003) and a new `dna_sequencing`
  takes Sanger's 1977 method — halving the subtree's largest single wait (267,620 → 133,810 RP).
- **Seven new foundation nodes, each with an in-lane dependant:** `thermodynamics`, `spectroscopy`, `periodic_system`,
  `organic_chemistry`, `cell_theory`, `taxonomy` (biological classification) and `probability_statistics`.
- **Fourteen rewires** (causal prerequisite repairs), two of them also re-scored (`astrolabe`, `quantum_mechanics`).
- **One relocation:** `double_entry` moves to the trunk beside `bill_of_exchange` (with D05).
- **Dead ends** now have a dependant (pcr, corned_powder, soda_leblanc, relativity — the last three through accepted
  requests to D09, D02 and D08) or a later realization owner (bleach_chlorine M9; dynamite, catalytic_converter M10+);
  `space_telescope` stays a justified knowledge-only leaf.
- **Workload:** lane 1,896,470 → 2,020,590 RP (+124,120; +1.1 % of the 11.21 M tree) [MEASURED, scratch integration].

Requests from D01, D02, D03, D05, D06, D07, D08, D09, D10 and D12 were read in their part files and are answered
(§10); D11 had not filed when this was written.

## 2. Dispositions (41 owned nodes)

| disposition | count | nodes |
|---|---|---|
| keep, no field change | 18 | optics_ibn_haytham, corned_powder, heliocentrism, microscope, mechanics_newtonian, calculus, barometer_vacuum, chemistry_quantitative, photography, vacuum_tube, genetic_engineering, plastics, laser, superconductivity_applied, space_probe, space_telescope, crispr, carbon_capture |
| keep, capability block only (owner, inputs, resource rule stated) | 6 | gunpowder, saltpetre_refining, telescope, soda_leblanc, bleach_chlorine, catalytic_converter |
| rewire (cost unchanged) | 9 | trigonometry, sulphuric_acid, haber_bosch, dynamite, genetics_mendel, dna_structure, atomic_clock, relativity, pcr |
| rewire (cost moves only through prereq_complexity) | 3 | evolution (+41 %), synthetic_dye (+41 %), petroleum_refining (−29 %) |
| rewire + rebalance | 2 | astrolabe, quantum_mechanics |
| split + rebalance | 1 | electromagnetism |
| split + rewire + rebalance | 1 | genome_sequencing |
| relocate | 1 | double_entry (natural_science → trunk) |
| merge / replace / retire | 0 | — |
| **new** | **9** | taxonomy, probability_statistics (A7); thermodynamics, electromagnetic_field_theory, spectroscopy, organic_chemistry, periodic_system, cell_theory (A8); dna_sequencing (A9) |

(18 + 6 = 24 `keep`; 12 + 2 = 14 rewired; 2 splits; 1 relocation → 41.)

## 3. Splits, rebalances, relocation — with reasons

**Split — `electromagnetism` (key 283).** The node ("Oersted 1820, Faraday 1831, Maxwell 1865") bundled the experimental
laws of current electricity with Maxwell's field theory. Its consumers need different halves: telegraph (1837), dynamo,
electricity_generation (A9 core fact), lithium_battery and xray need only the experimental laws; radio, radar,
relativity ("On the Electrodynamics of Moving Bodies") and quantum theory need the field theory and Hertz's waves. Key
283 keeps the experimental part (renamed "Electromagnetism"; emerged "1800–1831 CE"; prereq `calculus` unchanged), so
every existing consumer and the A9 core chain are untouched. Factors: difficulty 3.0 → 2.5 (the mathematics moved out);
39,780 RP. New `electromagnetic_field_theory` (A8, ← electromagnetism): 23,650 RP. Sum 63,430 vs 56,260 (+12.7 %).
Re-pointing: quantum_mechanics and relativity (this lane); radio (D05, accepted) and radar (D09, accepted) at
integration — new ids cannot be named in other lanes' part files.

**Split — `genome_sequencing` (key 386).** "1977 Sanger; 2001 human genome" bundled the invention of reading DNA with
genome-scale genomics two decades later. Key 386 keeps genomics (its consumers crispr, mrna_vaccines,
frontier_medicine need genomes); prereq becomes `pcr AND computer` (automated sequencers and assembly need computers;
sequence-tagged-site PCR mapping became the HGP's standard physical-mapping method [RESEARCHED]); novelty 2.0 → 1.0
because the paradigm moves to the method; 133,810 RP. New `dna_sequencing` (A9, ← dna_structure; novelty 2.0):
94,620 RP. `pcr` now requires dna_sequencing (primers are designed from known sequence). Sum 228,430 vs 267,620
(−14.6 %); the largest single wait halves.

**Rewire + rebalance — `astrolabe`.** Two content errors: `gearing` is not knowledge a planispheric astrolabe needs
(geared astrolabes are rare and late; D03 raised the same point), and institutional 3.0 is the corpus maximum, used
otherwise only by A9 state/corporate programmes — no other A6 node exceeds 1.75. The astrolabe was a portable
craftsman-scholar's instrument (oldest dated survivor: Nastulus, 927/928 CE [RESEARCHED]). Prereq → `astronomy_geometric`
(pc 0.5 → 0.0); institutional → 1.25 (A6 prior). 14,070 → 2,960 RP (= optics_ibn_haytham). Not a pacing change.

**Rewire + rebalance — `quantum_mechanics`.** Prereq `electromagnetism` → `electromagnetic_field_theory AND
thermodynamics AND spectroscopy AND periodic_system`: Planck's quantum (1900) answered the black-body problem
(electromagnetic radiation in thermal equilibrium; Boltzmann's statistical entropy); Bohr (1913) and Heisenberg (1925)
worked from spectral lines; Pauli's exclusion principle (1925) explained the closing of electron groups behind the
periodic system. pc 0.0 → 1.0. Separately, institutional 3.0 → 2.5 (A9 prior): quantum mechanics was worked out by
university research groups and journals, like relativity, dna_structure and laser (all 2.5); 3.0 marks rocket,
satellite, computer, transistor, nuclear_fission. Net 94,620 → 133,810 RP (+41 %); the convention alone would have
doubled it (189,240). Its closure grows from 15 nodes / 209,410 RP to 72 / 622,790 [MEASURED, scratch].

**Rewire — `evolution`, `synthetic_dye`, `petroleum_refining` (cost through pc only).** evolution ← selective_breeding
AND taxonomy (Origin ch. 13 explains the natural system of classification; pc 0 → 0.5; 16,730 → 23,650). synthetic_dye
← organic_chemistry AND coke AND dyeing (Perkin was Hofmann's student; mauve needed mordant dyeing and alizarin
reproduced madder — D07's request; pc 0.5 → 1.0; 23,650 → 33,450). petroleum_refining ← chemistry_quantitative alone:
`mining_shaft` is extraction, not refining knowledge, and distillation is implied by chemistry_quantitative (dropped as
redundant, as D02 did for aluminium); pc 0.5 → 0.0; 33,450 → 23,650; emerged → "1853–1859 CE" (Łukasiewicz's kerosene
1853 and refinery 1856, before Drake's 1859 well [RESEARCHED]).

**Relocate — `double_entry` → trunk.** An accounting technique (Genoa/Florence c. 1300), not natural science; generation
2 of the finance family whose generation 1, `bill_of_exchange`, D05 moves to the trunk (D05 recommended the same here).
In the trunk it no longer counts toward natural-science exhaustion (frontier_climate). Prereqs unchanged; primary
university Engineering (trunk nodes pay BaseCost anyway); a6_finance unchanged. **Coupled:** without D05's relocation,
validator V04 fails (MEASURED in the D04-only scratch run).

## 4. Rewires at unchanged cost (one line each)

| node | prerequisite change | causal reason |
|---|---|---|
| trigonometry | hindu_arabic → (hindu_arabic OR numeral_sexagesimal) | D03's request: chord and Islamic sine tables were computed in sexagesimal place value, the Indian tradition in decimal — a genuine alternative route; an OR group counts once |
| sulphuric_acid | glass_lead → saltpetre_refining | the Glauber/Ward method burned sulphur with saltpetre; Roebuck (1746) moved it into lead *chambers* [RESEARCHED] — lead-crystal glass (1674) is not needed |
| haber_bosch | electricity_generation → thermodynamics | Haber's *Thermodynamik technischer Gasreaktionen* (1905) and the equilibrium dispute with Nernst [RESEARCHED]; Oppau (1913) made hydrogen from steam over glowing coke [RESEARCHED] — no electrical knowledge needed |
| dynamite | chemistry_quantitative → organic_chemistry | nitroglycerine (Sobrero 1847) is an organic nitrate ester |
| genetics_mendel | evolution AND microscope → cell_theory AND probability_statistics | Mendel's laws came from counting hybrid ratios (statistics) in experiments that did not use natural selection; the chromosome theory needs cell biology |
| dna_structure | photography → xray | D10's request: the helix was read from X-ray diffraction (Photo 51); xray already lists "crystallography (DNA)" |
| atomic_clock | longitude_problem → vacuum_tube | the caesium clock (1955) locks an electronic oscillator to an atomic transition; the chronometer edge was lineage, not knowledge |
| relativity | electromagnetism → electromagnetic_field_theory | special relativity is the electrodynamics of moving bodies |
| pcr | dna_structure → dna_sequencing | primers are designed from known sequence |

## 5. New nodes and why

| id (Age) | cost | prereq | in-lane dependants | why it carries weight |
|---|---|---|---|---|
| taxonomy — "Biological classification" (A7) | 2,960 | printing_press | evolution | Linnaeus (1735); the body of knowledge evolution explains. Eureka (exposure): ocean voyaging returns specimens (carrack OR caravel), not implied, not dead |
| probability_statistics (A7) | 8,360 | algebra | genetics_mendel | Pascal–Fermat 1654 → Bernoulli 1713 → Laplace 1812; basis of Mendel's ratios, epidemiology (D10 asked for it), statistical mechanics, machine learning. A7 mathematics must sit in a subtree |
| thermodynamics (A8) | 47,310 | steam_separate_condenser AND calculus | quantum_mechanics, haber_bosch | Carnot 1824 → Boltzmann 1877; science of the heat engine. Accepted consumers: refrigeration (D01); requested: diesel, steam_turbine (D06) |
| electromagnetic_field_theory (A8) | 23,650 | electromagnetism | quantum_mechanics, relativity | split part (§3); radio and radar re-point at integration (accepted) |
| spectroscopy (A8) | 19,890 | telescope AND chemistry_quantitative | quantum_mechanics | Fraunhofer 1814; Kirchhoff and Bunsen 1859–60 (caesium, rubidium [RESEARCHED]); the instrument QM's own cost note names as its apparatus; gives the telescope a scientific consumer |
| organic_chemistry (A8) | 14,060 | chemistry_quantitative | synthetic_dye, dynamite | Wöhler 1828 → Kekulé 1858–65; scientific base of dyes, explosives, drugs and polymers. Future-system Eureka: an operating teaching research laboratory (Giessen model) |
| periodic_system (A8) | 11,830 | chemistry_quantitative | quantum_mechanics | Mendeleev 1869; predicted gallium, scandium, germanium; Pauli's input. Eureka (precursor): spectral analysis found new elements (spectroscopy), not implied, not dead |
| cell_theory (A8) | 16,730 | microscope | genetics_mendel | Schleiden 1838, Schwann 1839, Virchow 1855–58; offered to D10 (germ_theory, chemotherapy) |
| dna_sequencing (A9) | 94,620 | dna_structure | pcr | split part (§3) |

Every new node: era-marked `emerged` (the 83 marker-free pin is unchanged), natural_science primary university, a
`capability` block (knowledge-only, owner none — completion row only), per-factor `costRationale` on the Age prior, a
`costAnalog`, `whyDistinct` and an `acceptanceTest`. Keys are assigned by `tools/integrate.py`.

## 6. Candidates considered and REJECTED

| candidate | why rejected |
|---|---|
| X-ray crystallography node (von Laue 1912, Braggs 1913) | D10's `xray` already carries "crystallography (DNA)"; a separate node would duplicate it. dna_structure ← xray fixes the causal gap at no workload. Revisit if D10 ever splits X-ray physics from radiography |
| Superconductivity discovery (1911) split from `superconductivity_applied` | both consumers (fusion research, quantum computing) need the applied cryogenic engineering; the discovery node's only dependant would be the applied node — a click, not a decision |
| Maser / stimulated emission split from `laser` | stimulated emission is part of quantum mechanics (Einstein 1917); every laser consumer needs the device |
| Old quantum theory split from `quantum_mechanics` | no consumer needs only 1900–1913 theory |
| Genetic code / molecular biology split from `dna_structure` | structure, code and replication machinery have the same consumers |
| A D04 solid-state (band) physics node | D06 created `semiconductor_physics` (natural_science, A9, ← quantum_mechanics AND radio) as the science half of its transistor split; D04 reviewed it (era-marked, A9 prior, natural_science primary, per-factor notes) and endorses it — the record stays in D06's file so the id is defined once |
| Electrostatics (Gilbert, Leyden jar, Franklin) and a separate voltaic-battery node | no downstream node needs electrostatics; the battery (Volta 1800) is folded into the retained Electromagnetism scope, which also anchors lithium_battery's lineage |
| Thermometry / precision instruments as nodes | no consumer needs them beyond thermodynamics and spectroscopy; precision machine measurement is D07's machine-tool chain |
| Separate atomic-structure, radioactivity, radiation-detection, geology nodes | D11's (brief); D04 asks for edges instead (§10) |
| Polymer science split from `plastics` | all consumers need the materials, not the theory alone |
| Climate science (Tyndall 1859 → models) before carbon_capture | motivates carbon capture but is not knowledge the amine-scrubbing technology needs |
| Smokeless powder / high explosives node | no causal consumer (D09 reached the same conclusion) |
| Merging bleach_chlorine and soda_leblanc into one "alkali trade" node | both now have owners or dependants; distinct processes and dates; ids preserved (D-044) |
| Merging space_telescope into space_probe | remote observation and in-situ exploration are distinct capabilities |
| Relocating vacuum_tube to Engineering | an application, but a five-dependant hub; relocation would only relabel it |
| OR route for nitrogen fixation (Birkeland–Eyde arc, cyanamide) on haber_bosch | the node is the ammonia process; the electric-arc route is a different, short-lived product (nitrates) |
| A Eureka 'established dyeing trade' on synthetic_dye (D07's alternative) | dyeing is an A2 craft every A8 polity holds — a near-certain flat discount, which E-12 rejects; the prerequisite was taken instead |

## 7. Conflations found

- **Discovery vs later theory:** electromagnetism (experimental laws vs field theory) — split.
- **Method vs industrial scale:** genome_sequencing (Sanger method vs genomics) — split.
- **Resource extraction vs processing:** petroleum_refining carried a mine-shaft edge and Drake's well date; it is now
  processing only. The oil chain has three owners: identification D11, extraction D02 (`percussion_drilling`),
  processing D04 — agreed with D02.
- **Imaging vs diffraction:** dna_structure depended on photography; it now depends on xray (diffraction), per D10.
- **Lead glass vs lead chambers:** sulphuric_acid ← glass_lead was a mis-reading; fixed.
- **Chronology vs causality:** atomic_clock ← longitude_problem; haber_bosch ← electricity_generation; astrolabe ←
  gearing; genetics_mendel ← evolution — removed.
- **Knowledge vs world truth (fog):** telescope's "naval reconnaissance (D-039 B)" string is knowledge text only; the
  capability block states that research never reveals territory (D-040 B3). Open question 5 asks the fog lane.
- **Discipline mis-filing:** double_entry (accounting) sat in natural science — relocated to the trunk.

## 8. Dead ends resolved (baseline V09 list for this lane: bleach_chlorine, catalytic_converter, dynamite, pcr, relativity, soda_leblanc, space_telescope)

| node | resolution |
|---|---|
| pcr | dependant genome_sequencing (in-lane) |
| relativity | dependant gps — D08 accepted (GPS clocks carry a ~38 µs/day relativistic correction); D11 asked to use it for binding energy |
| soda_leblanc | dependant aluminium — D02 accepted (Bayer process digests bauxite in caustic soda); later owner M10+ alkali good |
| corned_powder (no dependant, Age fact) | dependant matchlock — D09 accepted |
| bleach_chlorine | later owner **M9** public health: with germ_theory, hypochlorite disinfection and water chlorination (Labarraque 1820s, Semmelweis 1847, Jersey City 1908); M10+ textile finishing — IMPLEMENTATION DEFERRED |
| dynamite | later owner **M10+**: an extraction technique in D02's extraction ladder and the prerequisite of a terrain-crossing tunnel class in the transport packet DW-059 (D07's pointer; requested of D02 and D08) — IMPLEMENTATION DEFERRED |
| catalytic_converter | later owner **M10+** environment/pollution content (M9 public health for air quality) — IMPLEMENTATION DEFERRED |
| space_telescope | justified knowledge-only leaf (end of the observational lineage; no space-science consumer before M11+); the one remaining V09 warning in the all-lanes scratch run |
| telescope (near dead end) | now feeds spectroscopy and D05's optical_telegraph |

## 9. Cost findings

- **Re-scores with content reasons:** astrolabe institutional 3.0 → 1.25; quantum_mechanics institutional 3.0 → 2.5;
  genome_sequencing novelty 2.0 → 1.0 (paradigm moved to dna_sequencing); electromagnetism difficulty 3.0 → 2.5 (theory
  moved to the new node). Every re-scored and new node carries a per-factor line in `costRationale`.
- **pc convention bites when the tree deepens:** adding causal prerequisites raises cost (QM +41 % even after its
  institutional correction; evolution, synthetic_dye +41 %); removing non-causal ones lowers it (petroleum_refining,
  astrolabe). Open question 1.
- **Workload:** +124,120 RP tree-wide (new non-split nodes +121,140; splits −32,020; re-scores and pc moves +35,000).
- **Closures (nodes / RP) [MEASURED, scratch integration of all eleven part files]:** quantum_mechanics 15/209,410 →
  54/541,610 (QM now honestly sits after chemistry, steam engines and optics); dna_structure 56/600,520 → 62/851,300;
  crispr 95/2,468,240 → 101/2,630,270; haber_bosch 75/796,700 → 69/705,270; electricity_generation 67/560,960 →
  63/532,920; evolution 7/31,590 → 31/136,860; astrolabe 25/76,770 → 13/26,310. These move with other lanes' edits.
- **Age 9 core path:** D06 makes internal_combustion (A9 CORE fact) require petroleum_refining, so this lane's
  petroleum rewire touches a core closure: 70/637,580 with the old prerequisite vs 68/623,940 as proposed [MEASURED]
  — slightly cheaper, no new knowledge forced onto the core path (chemistry_quantitative was already on it).
- **Pre-existing redundant conjuncts** (counted by the syntactic pc reading, not changed here): mechanics_newtonian
  (algebra ⊂ calculus), heliocentrism (astronomy_geometric ⊂ trigonometry), plastics, catalytic_converter,
  carbon_capture (chemistry_quantitative implied), space_probe (computer ⊂ satellite), space_telescope (telescope now
  implied through satellite → transistor → quantum_mechanics → spectroscopy). D04's own new and rewired prerequisites
  contain none (checked by script). Open question 6.
- **Subtree counts:** natural_science finite nodes 43 → 51 (frontier_climate's `finite_nodes_to_exhaust` must follow);
  trunk +1 (double_entry).

## 10. Cross-lane requests and replies (state at writing; full text in the JSON)

| lane | request | state |
|---|---|---|
| D01 | refrigeration ← thermodynamics | **accepted** (D01 integration override, pc 1.0) |
| D02 | aluminium ← soda_leblanc; dynamite in the extraction ladder | aluminium **accepted**; ladder open |
| D03 | (their requests) trigonometry OR route; astrolabe gearing and institutional | **done** by D04 |
| D05 | radio ← electromagnetic_field_theory; double_entry to trunk | radio **accepted** (integration); double_entry **done** |
| D06 | diesel and steam_turbine ← thermodynamics; machine_learning ← probability_statistics; semiconductor_physics | diesel, steam_turbine **accepted** (integration; D06 adds no thermodynamics node); machine_learning open; semiconductor_physics **endorsed** (stays in D06's file) |
| D07 | (their request) synthetic_dye ← dyeing; tunnel class | **done**; tunnel class → transport packet DW-059 |
| D08 | gps ← relativity; rocket ← mechanics_newtonian; longitude_problem notice; DW-059 tunnel class ← dynamite | first three **accepted**; DW-059 open |
| D09 | radar ← electromagnetic_field_theory; matchlock ← corned_powder; cannon_early ← saltpetre_refining | **all accepted** (radar at integration) |
| D10 | epidemiology ← probability_statistics; optional germ_theory/chemotherapy ← cell_theory; dna_structure ← xray (theirs) | xray **done**; others open |
| D11 | evolution ← stratigraphy; QM ← radioactivity (extended to the 1911 nucleus); relativity in the nuclear chain; identification of oil, saltpetre, sulphur | open (D11 had not filed; ids from its research brief) |
| D12 | (theirs) keep double_entry in Technology | **done** |

Integration-time edges that name a new D04 id (radio, radar, refrigeration, diesel, steam_turbine accepted so far)
need `data/integration-overrides.json`.

## 11. Compatibility summary (full blocks in the JSON)

- Saves store keys: every retained key is kept (283, 386 for the splits). Old saves that completed electromagnetism do
  not hold electromagnetic_field_theory, so quantum_mechanics/relativity (and radio/radar) become unavailable to them
  until it is researched; partial progress there goes dormant. Recommend the alias/remap seam (OPEN DECISION).
- Lowered costs (astrolabe, electromagnetism, petroleum_refining, genome_sequencing) complete stored progress ≥ the new
  cost at the next step with no RP (engine-audit 7.3).
- Tests: corpus-name pin for key 283 (renamed); branch and finite-per-subtree counts; Eureka counts (+2 evaluable
  knowledge Eurekas, +1 future-system); AI-empire goldens; the three CI `--check` docs. The D-047 chronology test and
  the 83 marker-free pin pass on the proposed graph (MEASURED by a mirror of the test).

## 12. Open questions

1. (Director) Should deepening re-price existing hubs through the pc convention (QM, evolution, synthetic_dye +41 %), or
   should pc be frozen at the ratified graph?
2. Alias/remap seam for split parts (electromagnetism, genome_sequencing) — recommended.
3. Integration overrides for edges naming new ids — accepted ones listed in §10; pending D06 (machine_learning), D10
   (epidemiology) and D11. Who applies them, and when are the resulting costs re-checked?
4. `inst.statistics_vital` requires "census AND calculus"; vital statistics rests on probability, not calculus — change
   the entity requirement to probability_statistics?
5. Telescope "naval reconnaissance (D-039 B)": should optical instruments ever enter the fog lane's sight budget, or
   stay knowledge-only under D-040 B3?
6. Re-score pre-existing redundant conjuncts (§9) under the "independent requirements" reading, or leave them?
7. space_telescope as a justified knowledge-only leaf, or owned by a later space-science packet?

## 13. Verification performed (MEASURED, scratch copies only; the package's own files were not regenerated)

- `tools/integrate.py --allow-incomplete` and `tools/validate.py --no-corpus` on (a) D04's part alone: 1 ERROR, the
  expected V04 for double_entry ↔ D05's bill_of_exchange; (b) all eleven part files present at writing (D01–D10
  without D11, plus D12): **0 ERROR**, 70 WARN, of which D04's only V09 is space_telescope.
- Mirror checks (scratch script): D-047 chronology — no violation on D04 edges, marker-free count unchanged by D04;
  every knowledge Eureka in the tree re-checked for implied/dead (loader logic incl. the stage's must-set) — none;
  no Age inversion; no redundant conjunct in D04's new or rewired prerequisites; capability grant strings equal the
  current unlock strings except the intended move of "radio (theory)".

## 14. Sources for RESEARCHED claims (web search summaries, accessed 2026-10-22)

- Lead-chamber process, Ward and Roebuck: <https://en.wikipedia.org/wiki/Lead_chamber_process>,
  <https://en.wikipedia.org/wiki/Joshua_Ward>, <https://www.ebsco.com/research-starters/history/roebuck-develops-lead-chamber-process>
- Haber 1905 and the Nernst dispute: <https://www.nobelprize.org/prizes/chemistry/1918/haber/biographical/>,
  <https://edu.rsc.org/feature/who-really-discovered-the-haber-process/2020277.article>
- Oppau 1913 (steam over glowing coke; catalysts; nitrogen by liquefaction): <https://en.wikipedia.org/wiki/Haber_process>,
  <https://aiche.org/sites/default/files/cep/20160969.pdf>, <https://www.sciencenews.org/wp-content/uploads/2020/10/19210926_SNB_No26.pdf>,
  <https://www.basf.com/global/de/who-we-are/history/chronology/1902-1924/1913>
- Linde 1876: <https://en.wikipedia.org/wiki/Carl_von_Linde>, <https://www.150.tum.de/en/?p=4637>
- Diesel and the Carnot cycle: <https://en.wikipedia.org/wiki/Rudolf_Diesel>, <https://britannica.com/EBchecked/topic/162711/Rudolf-Diesel>
- Corned powder in handguns: <https://chemistryworld.com/opinion/june-2014/7620.article>,
  <https://www.macs.hw.ac.uk/~hwloidl/hundredyearswar.com/Books/History/Gunp0011.htm>
- Wujing Zongyao nitrate content: <https://en.wikipedia.org/wiki/History_of_gunpowder>
- STS / PCR physical mapping (1989): <https://www.genome.gov/25520328/online-education-kit-1989-sequencetagged-sites-another-marker/>
- Łukasiewicz 1853/1856: <https://en.wikipedia.org/wiki/Ignacy_%C5%81ukasiewicz>,
  <https://culture.pl/en/article/ignacy-lukasiewicz-the-generous-inventor-of-the-kerosene-lamp>
- Silliman 1855: <https://en.wikipedia.org/wiki/Benjamin_Silliman_Jr.>
- Nastulus astrolabe 927/928: <https://islamsci.mcgill.ca/RASI/BEA/Nastulus_BEA.htm>, <https://en.wikipedia.org/wiki/Nastulus>
- Kirchhoff and Bunsen 1859–61: <https://en.wikipedia.org/wiki/Gustav_Kirchhoff>, <https://kip.uni-heidelberg.de/200jahrekirchhoff/ausstellung?lang=en>

Other dates used (e.g. Maxwell 1865, Hertz 1887–88, Carnot 1824, Mendeleev 1869, Schleiden/Schwann 1838–39, Linnaeus
1735, Sanger 1977, Tsiolkovsky 1903, the GPS relativistic offset, the Bayer process) are standard textbook facts not
re-verified this session; the corpus stage should cite them. A citation records where a claim can be checked, not that
it was verified.
