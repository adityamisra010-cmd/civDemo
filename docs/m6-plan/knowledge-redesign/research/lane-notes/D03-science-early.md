# Lane D03: early science and mathematics (trunk), lane notes

**Status: PROPOSED.** Nothing here is ratified or runtime content. The data is in `data/proposal-parts/D03-science-early.json`.

**Labels:** MEASURED = read from content, code or the baseline runs, with a citation. RESEARCHED = corroborated by search summaries this session (pages were not opened; see the end of this document). INFERENCE = derived by the rule stated. OPEN DECISION = needs the Director.

## 1. Summary

D03 owns 24 existing trunk nodes. They cover counting and numerals, arithmetic, practical and axiomatic geometry, algebra, astronomy, calendars, timekeeping, weights, alchemy and distillation, plus five nodes that the partition assigned by their domain field: dog, horse, donkey, camel and ochre.

The lane makes six kinds of change:

1. **Research stage.** The stage's only alternative was vacuous. It now has real Greek, Indian/Islamic and Chinese mathematics routes, and the Greek route is still the cheapest.
2. **Cost scoring.** A scale-maximum scoring anomaly on the three calendar nodes is corrected.
3. **Causality and chronology.** Four edges are fixed: surveying, astronomy_geometric, distillation and standard_weights.
4. **Dating.** Practical geometry is re-dated from Thales to the Bronze Age.
5. **Classification.** Five nodes are re-filed by domain.
6. **New node.** One: `counting_rods`.

No node is split, merged, retired or moved between trees or branches. All 24 owned nodes appear once in the part file. A scratch integration and validation run (copies of the package tools, outside the repository) gives 0 errors, with D03's three V09 dead-end warnings resolved.

## 2. Dispositions

| disposition | n | nodes |
|---|---|---|
| keep | 10 | tally_notation, numeral_sexagesimal, arithmetic_babylonian, water_clock*, astronomy_records*, geometry_axiomatic*, mechanics_archimedean, abacus*†, alchemy, hindu_arabic |
| relocate | 10 | dog_domestication, ochre_processing, horse_domestication, donkey, camel (classification); surveying, standard_weights, astronomy_geometric, distillation, algebra (graph position) |
| rebalance | 3 | calendar_lunar, calendar_civil, calendar_lunisolar |
| relocate + rebalance | 1 | geometry_practical (Age A4 → A3, re-scored, cost unchanged) |
| split / merge / replace / retire | 0 | — |
| new | 1 | counting_rods |

\* capability block overridden (owner, deferral or dead-end resolution); † emerged text corrected.

**Vocabulary note.** "relocate" is used here for prerequisite rewires and domain re-files, because the vocabulary has no "rewire" label (open question 3).

## 3. Changes and reasons

### 3.1 Research stage (the brief's specific question)

**MEASURED:** the stage expression is `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman`. Its cheapest closure is 28,660 RP over 18 nodes. The geometry chain owned by D03 accounts for 11,280 RP of that (39 %): surveying 2,490, geometry_practical 1,760 and geometry_axiomatic 7,030. A script comes on top.

**Finding 1: culturally narrow (MEASURED from the graph).** `algebra` required `geometry_axiomatic`, so the alternative `(geometry_axiomatic OR algebra)` could never be taken without Greek axiomatic geometry. I computed stage closures with the OR alternatives treated as impossible when excluded (INFERENCE):

| graph | Greek route | geometry_axiomatic excluded | Indian route | Chinese route |
|---|---|---|---|---|
| current | 28,660 / 18 | **unreachable** | unreachable | unreachable |
| proposed | 28,660 / 18 (unchanged) | 33,510 / 19 | 40,300 / 21 | 33,510 / 19 |

The fix is `algebra = (hindu_arabic OR counting_rods) AND geometry_practical`. The historical basis (RESEARCHED):

- Brahmagupta (628 CE) gave rules for zero and negative numbers and solved quadratics without Euclidean proof.
- The Chinese tradition grew from counting-rod computation: fangcheng elimination in the Nine Chapters, then Song–Yuan polynomial algebra (1247–1303 CE).
- Only al-Khwarizmi's demonstrations leaned on Euclid.

Every former holder of algebra's prerequisite still satisfies the new one, so availability only widens. `calculus` keeps `geometry_axiomatic` explicitly. The alternative routes cost more than the Greek one (+4,850 RP Chinese, +11,640 RP Indian), so the stage is broadened, not cheapened.

**Finding 2: causality and cost.**

- *Causality.* surveying → geometry_practical → geometry_axiomatic is a causal chain. Field-area computation is attested at Fara from ~2,600 BCE (RESEARCHED).
- *Cost.* geometry_axiomatic's factors match its A5 peers: novelty 2.0 (the deductive method); difficulty and institutional at the A5 prior; breadth 1.5 (university building, the stage, five dependants). It is not mispriced.
- *Binding atom.* It is the stage atom completed last in every measured run (`measurements/baseline-progression.md` finding 2: driver turns 996–1,016, MEASURED; AI 702–716, INFERENCE). Algebra does not relieve it, because algebra costs more. Whether a mathematics atom should gate all five subtrees is left as open question 4.

**Finding 3: needlessly expensive for AI polities (MEASURED policy behaviour; INFERENCE on cost).** `AiResearchPolicy` closes the university goal over the union of every OR branch (`AiResearchPolicy.cs:184-195`). The goal's union closure is 60,440 RP over 29 nodes, against a cheapest closure of 28,660 over 18.

AI telemetry for seed 42 (INFERENCE, `measurements/baseline-ai-node-completions.csv`) shows the cost of this. The AI researched abjad, brahmi and hindu_arabic (10,310 RP, turns 658–684) before geometry_axiomatic (710), and numeral_sexagesimal and arithmetic_babylonian (501/507) before surveying (522).

D03's two rewires change that union by +880 RP: counting_rods adds 3,520 and the surveying fix removes 2,640. The road-goal union falls from 30,480 to 27,840 RP. The structural fix lies in AI policy (open question 1), not in content.

**Finding 4: naming (withdrawn).** I first flagged `legal_code_roman` and `medicine_hippocratic` as single-tradition names. MEASURED from the inventory: their display names are already "Systematic law" and "Clinical medicine"; only the ids carry a tradition, and ids are never renamed. D12 and D10 confirmed this. The note is withdrawn.

### 3.2 Rebalances: the calendar anomaly

**MEASURED:** among A1–A5 nodes, only the three calendar nodes carry institutional 3.0, the loader's maximum. Elsewhere it appears on astrolabe (A6) and on A8–A9 programme-scale nodes (locomotive, rocket, nuclear_fission). The consequences:

- `calendar_lunar` (4,180) cost more than the A2 CORE fact `cereal_cultivation` (3,520).
- `calendar_civil` (5,910) cost more than every A3 script.

D-045 had classed all three as band M2, a "bounded" or "useful refinement" (`docs/research-corpus-audit-d045.md:167,195,223`). The reason for the change is the content of the institutional factor, not pacing (D-046 §1).

| node | factor change | old → new RP | content reason |
|---|---|---|---|
| calendar_lunar | I 3.0 → 0.25 | 4,180 → 620 | Its own anchor, the Warren Field pit alignment (~8,000 BCE), was built by Mesolithic hunter-gatherers, with no institution behind it; A2 prior |
| calendar_civil | I 3.0 → 1.25 | 5,910 → 1,760 | A state-decreed year justifies an above-prior institutional score, at law_code's level, not the national-programme maximum |
| calendar_lunisolar | I 3.0 → 1.0; B 0 → 0.5 | 3,520 → 1,240 | Temple scribes are already priced in astronomy_records (A4 prior). It gains a dependant (§3.3) |
| geometry_practical | Age A4 → A3; D 1.25 → 1.0; I 1.0 → 0.75; B 0.5 → 1.0 | 1,760 → 1,760 | Re-dated to Bronze Age scribal mathematics, so the A3 prior applies; it now underlies both axiomatic geometry and algebra. The two corrections cancel; no cost was targeted |

**Net lane workload:** −6,470 RP (11,210,860 → 11,204,390), including the new counting_rods (+3,520). The early-Age shares fall: A2 −3,560, A3 −4,150, A4 −2,280. The cheapest-first driver had postponed these nodes to turns 739–917 (MEASURED); it will now take them early. That is a consequence of the change, not its aim.

**Peers checked and left unchanged:**

- `alchemy`: the only A5 node with difficulty above the prior (2.0 against 1.5), while its breadth of 0.5 understates two independent lines (1.0). The two corrections cancel at magnitude 6.25.
- `mechanics_archimedean`: difficulty 1.0 and institutional 1.75 sum to the A5 prior.
- `hindu_arabic` and `algebra`: at the A6 prior.

### 3.3 Relocations: prerequisites and anchors

- **surveying:** `arithmetic_babylonian OR …` → `cuneiform OR hieroglyphic OR chinese_script`. The Mesopotamian branch named Old Babylonian school mathematics (~1,800 BCE in content) for a node anchored at ~2,500 BCE. RESEARCHED: Fara area tables ~2,600–2,500 BCE. This is D-047 r1 (the prerequisite was not "possible before"); the chronology test misses it because the atom sits inside an OR. Availability only widens. The capability grants are rephrased as knowledge with no tax effect, at D12's request; the runtime "tax" string is pinned by GovernanceTests.
- **astronomy_geometric:** `… AND astronomy_records` → `… AND calendar_lunisolar`. Geometric models need mean motions, which came from Babylonian period relations and long eclipse records. RESEARCHED: the Babylonian 19-year cycle was in use from ~499 BCE and was adopted by Meton in 432 BCE; Ptolemy relays Hipparchus's use of a Babylonian eclipse of 721 BCE. INFERENCE: this is the knowledge the models consume. The edge resolves calendar_lunisolar's dead end. The anchor gains Hipparchus (~150 BCE). This is the lane's only narrowing: a polity with geometry_axiomatic and astronomy_records but without calendar_lunisolar loses availability, and its partial progress stays dormant until it researches calendar_lunisolar (1,240 RP).
- **distillation:** `alchemy AND glass_blowing` → `alchemy AND (glass_blowing OR sheet_metal)`. This is a regional-material alternative. RESEARCHED: Zosimos quotes Maria on copper or bronze tubes, and glass was valued for observation and for corrosive work. Glass stays where later chemistry really needs it: chemistry_quantitative → barometer_vacuum → glass_blowing. The acid nodes' vessels are D04's call; D04's part now gives sulphuric_acid the lead-chamber route, `distillation AND saltpetre_refining`. The prerequisite complexity, and so the cost, is unchanged. The Gandhara ceramic "stills" were deliberately **not** used as evidence: recent experimental work disputes them (RESEARCHED).
- **standard_weights:** the script OR gains `chinese_script`, the Group-B generic record-keeping alternative. A Chinese-route polity no longer needs Mesopotamian proto-writing to reach hacksilver and coinage_electrum, the A5 CORE fact.
- **algebra:** see §3.1.
- **Re-dating:**
  - geometry_practical: "~600 BCE Ionia" (Thales, the prehistory of axiomatic geometry) → Moscow papyrus ~1,850 BCE and Old Babylonian ~1,800 BCE.
  - donkey: genomic domestication ~5,000 BCE, Africa; Abydos pack donkeys ~3,000 BCE (RESEARCHED).
  - abacus: the Chinese suanpan is first illustrated in 1436 CE; the ~190 CE attribution is disputed (RESEARCHED).
- **Classification:** dog_domestication, horse_domestication, donkey and camel move to domain agriculture with an Agricultural primary university, matching sheep_goat, cattle and pig (family "herd"). ochre_processing moves to materials/Engineering. This changes only display and metadata: trunk nodes always pay BaseCost, and no test pins domain. A partition regenerated from domain would hand these five nodes to D01 and D02.

## 4. New node

| id | Age | cost | prerequisite | why |
|---|---|---|---|---|
| counting_rods ("Counting-rod arithmetic") | A5 | 3,520 (N2.0 D1.5 M0 I1.25 B0.5 P0) | chinese_script | An independent decimal place-value calculating system: Warring States rods ~400 BCE and the Tsinghua decimal multiplication table ~305 BCE, the oldest known (RESEARCHED). It is the computational base of the Nine Chapters and of Song–Yuan algebra, and the ledger names "counting rods" as an approved Group-B alternative (§6.2). Its consumer is algebra, as the Chinese route. Cost analog: numeral_sexagesimal's profile at the A5 prior |

## 5. Candidates considered and rejected

| candidate | why rejected |
|---|---|
| Split astronomy_records into observation and prediction | The predictive step (period relations) is already a node, calendar_lunisolar. It is now wired between records and geometric models, so the split is gained without a new node or a new dead end |
| Separate "mathematical (arithmetical) astronomy" node | Same reason. A new node would have left calendar_lunisolar a dead end |
| Make both astronomy_records routes require calendar_lunar | It is causally tidy, but it raises prereq_complexity (0.0 → 0.5, +730 RP) and narrows availability for no new capability |
| Logic / natural-philosophy node | Deductive proof is the content of geometry_axiomatic. A field label on the stage path would add cost without a capability. The institution `inst.natural_philosophy` already exists (deferred); its requirement belongs to the integrator |
| Gnomon / sundial node | The shadow-stick technique sits inside records and calendar practice. No consumer needs it as distinct knowledge, and a new node would add one more dead end |
| Separate standard length and capacity measures node | They were standardized together with weights, and no downstream node needs them separately. Adding the node to surveying would raise the cost of the stage path |
| Rename standard_weights to "…and measures" | It would touch a corpus-sync test for no mechanical gain; the scope is noted here instead |
| Split surveying into cadastral and levelling knowledge | It would churn five D07 infrastructure nodes for a refinement; the measuring and layout knowledge is one coherent body |
| Egyptian-arithmetic node | Group B already lets `hieroglyphic` stand for Egyptian numeration; a node would lengthen Egyptian routes without a capability |
| Broaden numeral_sexagesimal / arithmetic_babylonian | Ratified as Babylonian-specific (ledger §6.2, "do not broaden"); a test pins it |
| Separate zero node | Bundled correctly in hindu_arabic; the Maya zero needs a script node (cross-lane request) |
| Route abacus's Chinese branch through counting_rods | It is more precise, but it narrows availability (dormant progress) and overrides the ratified script alternative for a dead-end node |
| Indian alchemy route (brahmi) | Rasashastra is later and its independence is debated; the Chinese and Hellenistic ORs suffice |
| Drop glass from distillation entirely | Glass is a genuine regional material; an OR is more honest than deleting it, and keeps the cost unchanged |
| Pharmacology route into distillation | Weak evidence (Dioscorides' vapour collection); not needed |

## 6. Conflations found

- **Husbandry knowledge vs access to the animal.** horse_domestication, donkey and camel grant knowledge, but no horse, donkey or camel good exists; livestock is generic. Their capability blocks now say so, and the resource design is requested of D11 (with D01).
- **Observation vs predictive model.** astronomy_records carried "eclipse prediction (Saros)". The prediction chain is now records → period relations (calendar_lunisolar) → geometric models.
- **One tradition's justification vs a requirement.** Euclid in al-Khwarizmi's algebra (§3.1) and blown glass in Alexandrian stills (§3.3).
- **Capability text vs gates.** surveying and standard_weights still carry taxation text. R5 made the Taxation civic the sole gate. The text is kept because GovernanceTests pins a "tax" string.

## 7. Dead ends

| node | resolution |
|---|---|
| calendar_lunisolar | Gains the dependant astronomy_geometric through a causal edge (§3.3) |
| water_clock | Cross-lane request to D07: make it an AND prerequisite of escapement_verge (Yi Xing 725 CE, Su Song 1088 CE; INFERENCE, not re-checked). If declined, it is a justified knowledge-only leaf until M11+, since no simulation consumer reads hours |
| abacus | Justified knowledge-only leaf. Possible later reader: the undefined authority/bandwidth economy (R4, M6 civic spec), OPEN; no consumer is forced |

## 8. Compatibility (summary; full blocks are in the JSON)

- **Saves:**
  - Lowered costs complete saved progress at or above the new cost at the next step (the three calendars).
  - astronomy_geometric is the only narrowing: dormant partial progress until calendar_lunisolar.
  - Everything else only widens.
  - No key, id or Eureka index changes.
- **Tests:**
  - New node: technology count 426 → 427+, trunk branch count +1 (`ResearchContentTests:38, 84`).
  - The chronology test passes on the proposal, with 83 unmarked emerged texts unchanged (INFERENCE, test regex re-implemented).
  - Group B tests still hold (Babylonian nodes keep cuneiform; abacus, astronomy_records and geometry_practical are reachable without cuneiform).
  - AiResearchGoalTests:95 derives its closures from content.
  - The three research `--check` docs regenerate, and AI-empire goldens move.
  - No allocation-scenario key (12/17/2/8/52/74/53) and no v33-fixture key (2) is touched.
- **Corpus stage:** the descriptions of geometry_practical (Thales), surveying (the "3-4-5 rope-stretcher" claim is popular but unattested in Egyptian sources, INFERENCE), donkey, abacus (suanpan date) and algebra (multiple centres) need rewriting to fit the proposal.

## 9. Cross-lane requests (in the JSON)

| lane | request |
|---|---|
| D07 | water_clock as a prerequisite of escapement_verge |
| D04 | trigonometry: widen to `(hindu_arabic OR numeral_sexagesimal)`, since Greek chord tables were sexagesimal. astrolabe: re-examine `gearing` and institutional 3.0. Note astronomy_geometric's added prerequisite |
| D01 | sugar_refining requires fermentation_fruit and distillation, but crystallized sugar involves neither (D-045 itself flagged it "boiling to crystal") |
| D08 | latitude_sailing's calendar_lunar branch becomes cheap (620); cartography inherits calendar_lunisolar; D03 owns astronomy knowledge and D08 its applications |
| D05 | A Mesoamerican script node would let D03 add a Maya route to the generic-writing ORs and to astronomy_records. Note the standard_weights widening |
| D11 (+D01) | Design domesticable-animal resources, or state why not. Research must never reveal wild herds |
| D12 | Naming note withdrawn. Surveying's grants rephrased as asked. calendar_lunar is cheaper, which helps organized_priesthood. `(hindu_arabic OR counting_rods)` is defensible for official_statistics |
| D10 | Consent to `(hindu_arabic OR counting_rods)` for epidemiology if counting_rods is accepted. Naming note withdrawn |

**Received and acknowledged:**

- D01: pig now depends on dog_domestication.
- D09: trace_italienne still requires geometry_axiomatic.
- D12: organized_priesthood, land_registry and official_statistics reference calendar_lunar, surveying and hindu_arabic.

## 10. Open questions

1. **OPEN DECISION, AI policy:** should AI goal closures take the cheapest satisfying OR branch, as `tools/kr_lib.py` does, instead of the union? Today every alternative route on a goal path becomes de facto mandatory for AI polities (§3.1 finding 3).
2. `building.observatory` accepts only astronomy_geometric, astrolabe or telescope, so it has no Babylonian, Chinese or Maya route. Should calendar_lunisolar be added? This is an entity requirement, so it is the coordinator's or Director's call.
3. Disposition vocabulary: is there a "rewire" label, or is "relocate" acceptable for prerequisite-only changes?
4. **Director-level:** should a mathematics atom gate all five subtrees, or only the university building? The stage is a hand-written summary of the university entities, and no validator ties the two together (ADR-029). D03 does not change the stage expression.
5. The integrator's `derived.optional` flag uses union ancestors, so it marks counting_rods "non-optional" although no cheapest stage closure contains it. This is a tool limitation and should be read with care.

## 11. Evidence used this session

All via WebSearch: result summaries and URLs, not full pages. Page fetching is blocked in this environment (`research/corpus-brief.md` §4).

| claim | source |
|---|---|
| Tsinghua decimal multiplication table ~305 BCE | [Scientific American / Nature news 2014](https://www.scientificamerican.com/article/ancient-times-table-hidden-in-chinese-bamboo-strips); [Tsinghua Bamboo Slips](https://en.wikipedia.org/wiki/Tsinghua_Bamboo_Slips) |
| Counting rods, Warring States (Changsha Chu tomb) | [Counting rods](https://en.wikipedia.org/wiki/Counting_rods); [MAA Convergence](https://old.maa.org/press/periodicals/convergence/reflections-on-chinese-numeration-systems) |
| Donkey genomic domestication ~5,000 BCE, Africa | [Todd et al. 2022, Science (record)](https://cris.biu.ac.il/en/publications/the-genomic-history-and-global-expansion-of-domestic-donkeys/); [Sci.News](https://www.sci.news/genetics/donkey-african-origin-11184.html) |
| Abydos pack donkeys ~3,000 BCE | [Rossel et al. 2008, PNAS](https://pmc.ncbi.nlm.nih.gov/articles/PMC2268817) |
| Gandhara still disputed | [Groat, Antiquity (White Rose)](https://eprints.whiterose.ac.uk/id/eprint/223374); [Allchin 1979 listing](https://indianmedicine.eldoc.ub.rug.nl/41666/) |
| Maria / Zosimos: copper or bronze tubes; glass for observation | [Mary the Jewess](https://en.wikipedia.org/wiki/Mary_the_Jewess); [Alembic](https://en.wikipedia.org/wiki/Alembic) |
| Hipparchus, Babylonian relations, 721 BCE eclipse | [Hipparchic cycle](https://en.wikipedia.org/wiki/Hipparchic_cycle); [DIO](https://dioi.org/thr.htm) |
| Babylonian 19-year cycle from ~499 BCE; Meton 432 BCE | [Babylonian calendar](https://en.wikipedia.org/wiki/Babylonian_calendar); [Metonic cycle](https://en.wikipedia.org/wiki/Metonic_cycle) |
| Moscow papyrus ~1,850 BCE; Rhind ~1,550 BCE | [Moscow Mathematical Papyrus](https://en.wikipedia.org/wiki/Moscow_Mathematical_Papyrus); [Rhind Mathematical Papyrus](https://en.wikipedia.org/wiki/Rhind_Mathematical_Papyrus) |
| Fara (Shuruppak) area tables ~2,600–2,500 BCE | [arXiv 2011.03406](https://arxiv.org/pdf/2011.03406); [Schøyen MS 3047](https://www.schoyencollection.com/mathematics-collection/arithmetics/earliest-mathematical-text-ms-3047) |
| Suanpan first illustrated 1436 CE; ~190 CE text disputed | [Suanpan](https://en.wikipedia.org/wiki/Suanpan) |
| Brahmagupta 628 CE: zero, negatives, quadratic | [MacTutor: Brahmagupta](https://mathshistory.st-andrews.ac.uk/Biographies/Brahmagupta) |

**Not re-checked this session** (general knowledge; INFERENCE where used):

- Warren Field ~8,000 BCE (Gaffney et al. 2013)
- Babylonian Astronomical Diaries from 652 BCE
- Yi Xing 725 CE and Su Song 1088 CE escapements
- Qin standardization 221 BCE
- Song–Yuan algebra dates
- DOM2 horse origin
- Salamis tablet ~300 BCE
- Later copper and bronze stills (Islamic world, Jin–Yuan China)
