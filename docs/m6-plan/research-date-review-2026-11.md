# Research date-order review: the audit's §7 candidates (November 2026)

**Status: REVIEW (lane RT, D-084).** This review covers the 48 date-order candidates in §7 of the research-tree audit
([`research-tree-audit-2026-11-10.md`](research-tree-audit-2026-11-10.md)). It was made under D-084 reading DC1-R1(a)
([`../d084-research-tree-audit-fixes-2026-11-10.md`](../d084-research-tree-audit-fixes-2026-11-10.md)), which turns the
candidates into a reviewed list with evidence. The rules it followed:

- `Sim.Data/content/research.json` changes only where the evidence shows that an `emerged` text is wrong, and every such
  change is listed below with its source.
- A prerequisite that the evidence calls into question is written up as a **PROPOSAL** for the Director. No prerequisite
  was changed.
- There is no bulk correction.
- The D-047 causal test keeps passing.

The audit's other findings (§2–§6, §8) are answered in the lane's report, `research-tree-fixes-2026-11.md`, which sits next
to this file.

**Two passes.** The first pass of this review (commit `950d6d1`) applied five `emerged` text fixes. The lane's run was
interrupted, and on resuming the five were judged again against the rule above and against D-047 Part C ("no date text is
altered to make an edge pass"). Two were **withdrawn** and became proposals, because the evidence does not show the old
text wrong for the node as it is defined: `fire_making` (F1, a date D-047 Part E itself set for the re-scoped node) and
`knapping_blade` (F2, which turns on whether the node's "prismatic cores" covers the early African blades). Three stand:
`adhesive_natural`, `grinding_stone` and `well` (F3–F5). §4 gives each, with its sources.

## 1. Method

**The candidates.** The audit's heuristic compares the earliest marked date in a prerequisite's `emerged` text with the
earliest marked date in its dependent's. The marked dates are `BCE`, `CE`, `kya` and `Mya`, read with the same regular
expression the D-047 test uses (`ResearchContentTests.EarliestMarkedYear`). An edge is a candidate when the prerequisite's
date is the later one. The heuristic reads `kya` as years on the same axis as BCE, so for Palaeolithic dates the
2,000-year difference between "before present" and "BCE" is ignored. That difference is smaller than the uncertainty of
every Palaeolithic date below.

The candidates were re-measured from the file with
[`research-tree-fixes-2026-11/measure_audit.py`](research-tree-fixes-2026-11/measure_audit.py)
(`python3 -I docs/m6-plan/research-tree-fixes-2026-11/measure_audit.py`).

| `research.json` | Candidates | Must-hold | Alternative |
|---|---|---|---|
| Base `51ab032` (blob `f05deae6`, the audit's blob) | **48**: the audit's 48, edge for edge, REPRODUCED | 14 | 34 |
| After this review's three text fixes (F3–F5, §4) | **48** | 14 | 34 |

The count is unchanged, but not the set: one candidate is resolved (`ochre_processing ← grinding_stone`, F4) and one new
one appears (`birch_tar ← adhesive_natural`, the last row of §3, from F3's correction of the adhesive date). F5 moves the
dependent date of `well ← timber_frame` from ~8,000 to ~8,500 BCE; that row was a candidate before and stays one. (The first
pass's five fixes gave 46: its F1 and F2 also resolved `birch_tar ← fire_making` and `bone_tools ← knapping_blade`.)

**Evidence labels.**

| Label | Meaning |
|---|---|
| **FETCH** | The source page itself was read in this session. **None.** Every fetch attempt failed because the host did not resolve from this session's network: `digitalcommons.usf.edu` and `en.wikipedia.org` (first pass), `pubmed.ncbi.nlm.nih.gov` (second pass). No further site was tried. |
| **SEARCH** | A web search in this session returned the source and summarised the claim. The page itself was **not** read. The source is cited by the result's own link. |
| **KNOW** | The reviewer's own knowledge, not checked in this session. |
| **TEXT** | The verdict follows from the `emerged` texts of `research.json` as they stand, with no outside evidence. Example: the dependent's own OR group holds another prerequisite whose date is no later than the dependent's. |
| **REPO** | The repository's knowledge corpus (`docs/m6-plan/knowledge-redesign/corpus/*.json`), which is the source of the game's educational texts. It is internal and mostly not source-checked, so it is used for corroboration only. |

**Verdicts.**

| Verdict | Meaning |
|---|---|
| **no conflict** | Read as written, the texts do not put the prerequisite after the dependent. Causes: a same-group alternative is dated no later than the dependent; the flagged date is marked inferred or contested; or the date belongs to something other than the node (wild use against cultivation, weaving against one loom type). |
| **text fix** | The evidence shows that the `emerged` text is wrong, and it was corrected in `research.json` (§4). |
| **prerequisite question** | The dated evidence on both sides stands, and the order conflicts. The question is put to the Director as a PROPOSAL (§5). |

## 2. The eight priority candidates (audit §7.2)

**P1. `bow_simple ← cordage` (must-hold).** Verdict: **no conflict.**
- The bow's earliest date is inferred: "~64 kya Sibudu (inferred)". Lombard and Phillipson (2010, *Antiquity* 84) judged
  it "highly likely" that the bow and arrow were in use by 64,000 cal BP. They based this on macro-fractures and residues
  on backed quartz pieces, and later work debates the reading (SEARCH:
  [Cambridge abstract](https://www.cambridge.org/core/journals/antiquity/article/abs/indications-of-bow-and-stonetipped-arrow-use-64-000-years-ago-in-kwazulunatal-south-africa/89AF638BE5E64CEAC63363EFDD4D5E8F)).
- The cordage date is a direct find: "~50 kya Abri du Maras (direct)". Hardy et al. (2020, *Scientific Reports*) report a
  three-ply inner-bark cord, 41,000–52,000 years old (SEARCH:
  [CARTA record](https://carta.anthropogeny.org/node/321357),
  [NPR](https://www.npr.org/2020/04/10/828400733/the-oldest-string-ever-found-may-have-been-made-by-neanderthals)).
  REPO gives the same age.
- The cordage text itself adds "older inferred". A bow needs its string, so the inference that dates the bow also dates
  cordage. The heuristic is comparing an inferred date with a direct one.
- No change. The text is not wrong.

**P2. `ochre_processing ← grinding_stone` (must-hold).** Verdict: **text fix (applied) → resolved.**
- `grinding_stone` described itself as "Quern and handstone for processing seeds, tubers, pigment". Its `emerged` text,
  however, named only the plant-flour example: Bilancino, ~30 kya.
- Grindstones for pigment are far older. McBrearty and Tryon (2006) report blades, grindstones and pigment in the
  Kapthurin Formation, Kenya, before 285,000 BP (SEARCH:
  [chapter PDF](https://scholar.harvard.edu/sites/scholar.harvard.edu/files/catryon/files/mcb_tryon_2006.pdf)).
- Henshilwood et al. (2011, *Science* 334: 219–222) found two ochre-processing toolkits with grindstones at Blombos,
  dated about 100,000 years ago by OSL (SEARCH: [UOW record](https://ro.uow.edu.au/scipapers/4074)). Second pass
  (SEARCH, [EurekAlert release](https://www.eurekalert.org/news-releases/470496),
  [ScienceDaily](https://www.sciencedaily.com/releases/2011/10/111013141807.htm)): the kits held ochre, bone, charcoal,
  grindstones and hammerstones; ochre was rubbed on quartzite slabs to a fine powder.
- The text now gives these dates. The earliest moves from ~30 kya to ~285 kya.
- Why this is a correction and not a date moved to make the edge pass (D-047 Part C): the node's own description includes
  pigment, and the assemblage that dates the dependent (`ochre_processing`, "~100 kya Blombos") is the one that holds the
  grindstones.

**P3. `bone_tools ← knapping_blade` (must-hold).** Verdict: **prerequisite question (a definition)**, PROPOSAL B1 in §5. The
first pass applied a text fix here (F2); the second withdrew it (§4).
- The old text reads "~40 kya Upper Palaeolithic; earlier in Africa (~70 kya)". If the node is any systematic blade
  production, that understates African blade production:
- McBrearty and Tryon (2006) report blades in the Kapthurin Formation before 285,000 BP (SEARCH, link under P2). Second
  pass (SEARCH, [2021 *J. Paleolithic Archaeology*](https://link.springer.com/10.1007/s41982-021-00109-1)): the Kapthurin
  knapping at the Living Site and the Factory Site "systematically produced blades", and dedicated blade cores occur.
- Johnson and McBrearty (2010, *J. Hum. Evol.* 58: 193) report nearly 100 blades, with the cores they were struck from, at
  about 500,000 years (SEARCH:
  [Science News](https://sciencenews.org/?p=41474),
  [citation](https://cambridge.org/core/product/identifier/S0033589400027642/type/journal_article)).
- Howiesons Poort, roughly 65.8–59.5 ka, is the date Jacobs et al. (2008) give. Work at Diepkloof argues for a longer
  chronology (SEARCH:
  [ScienceDaily](https://sciencedaily.com/releases/2008/10/081031102630.htm),
  [Diepkloof clarification](https://digitalcommons.usf.edu/kip_articles/4132)). REPO dates Howiesons Poort to ~65 ka.
- The first pass's text used the conservative "before ~285 kya", not ~500 kya: the ~500 kya figure is one team's, and it
  would place the node before its own prerequisite `knapping_levallois` ("~300 kya").
- **Why the fix was withdrawn.** The node is defined as blades "struck from prismatic cores". Prismatic (volumetric) blade
  cores are the Upper Palaeolithic method; whether the Kapthurin cores are prismatic in that sense could not be checked (no
  FETCH; the searches did not settle it). Read strictly, the old text is right (Howiesons Poort ~65 ka is its "earlier in
  Africa (~70 kya)"), so the evidence does not show it wrong, and the date may not be moved to make the edge pass
  (D-047 Part C).
- Read strictly, the conflict is real and lies on the other side as well: `bone_tools` says it "requires groove-and-splinter
  technique", an Upper Palaeolithic method, while its earliest date is the Katanda barbed points (~90 kya), which were
  shaped by grinding (KNOW; not checked). Which side to move is a definition the Director owns (B1).

**P4. `loom_warp_weighted ← spinning_spindle` (must-hold).** Verdict: **no conflict** for the loom itself. Wording proposal
W2 in §5.
- The loom's earliest marked date is woven cloth: "~7,000 BCE Çatalhöyük (textiles)". Weaving in general is not the
  warp-weighted loom.
- The textiles are plain-weave cloth of tree bast (oak, and also elm and willow/poplar), not flax. They date to 6,700–6,500
  cal BC (SEARCH: Rast-Eicher et al. 2021, *Antiquity*,
  [NTNU record](https://ntnuopen.ntnu.no/ntnu-xmlui/handle/11250/3032773); 2023 *JAS Reports*,
  [Glasgow record](https://eprints.gla.ac.uk/293971)).
- The loom's own diagnostic evidence, loom weights at ~5,500 BCE, follows the spindle whorls at ~6,000 BCE. REPO says the
  same: whorls are common by ~6,000 BCE, and loom weights appear from the sixth millennium BCE.
- Spinning may also be much older. Perforated Natufian pebbles at Nahal Ein Gev II, ~12,000 years ago, are argued to be
  spindle whorls (2024 *PLOS ONE*; a hypothesis supported by spinning with replicas). SEARCH:
  [EurekAlert](https://www.eurekalert.org/news-releases/1064656),
  [Hebrew University](https://europeanfriends.huji.ac.il/node/3187341).
- No change. "~7,000 BCE" is a rounding of 6,700–6,500 BCE, not an error, and the order verdict does not depend on it.

**P5. `territorial_kingship ← appointed_officials` (must-hold).** Verdict: **prerequisite question.** This is a
definition, and the Director owns it. PROPOSAL K1 in §5.
- The prerequisite's earliest marked date is "~2,600 BCE Old Kingdom Egypt (royal officials)". The dependent's is
  "~3,100 BCE unified Egypt".
- Titled officials are attested from the First Dynasty onward, the same horizon as unification (SEARCH):
  - Officials served as sealers in many departments, with titles such as "Sealer of the God". One was the chancellor
    Hemaka.
  - Non-royal inscriptions at Hierakonpolis are evidence of provincial officials.
  - Sealings from Djer's reign begin to name districts.
  - First Dynasty year labels record officials' names and titles.
  - The vizier is secure only from the Third Dynasty.
  - Sources: [Macquarie thesis on Early Dynastic sealing administration](https://figshare.mq.edu.au/articles/thesis/Those_administrative_documents_are_sealed_a_study_of_the_evolution_of_the_structure_and_function_of_the_Egyptian_administration_in_the_Late_Predynastic_and_Early_Dynastic_Period_with_specific_attention_to_seals_and_seal_impressions/19428551),
    [*Ancient Egyptian Economy*, Early Dynastic chapter](https://www.cambridge.org/core/books/ancient-egyptian-economy/early-dynastic-period-c-30002686-bce/0DB61A5A16C5DC5ABEEDEAEADEE52533),
    [Kahl 2006](https://archiv.ub.uni-heidelberg.de/propylaeumdok/3706/1/Kahl_Inscriptional_evidence_2006.pdf).
- So "~2,600 BCE" understates the office-holders. Whether First Dynasty sealers and title-holders meet the node's
  definition is a question of reading, which the evidence does not settle. The node says "officials appointed and
  dismissed by the ruler govern by written order".
- No text change was made.

**P6–P8. `war_band ← kin_council`, `hunt_season_leaders ← kin_council`, `seasonal_aggregation ← kin_council` (must-hold).**
Verdict: **no conflict.** Wording proposal W1 in §5.
- `kin_council`'s text gives no emergence date: "~10 kya and earlier: councils of household heads ... (ethnographic
  inference; no direct date)". REPO says the same: "their prehistoric antiquity is inferred, not dated". The heuristic
  picks up "~10 kya", which is the only marker in the text.
- The dependents' own dates hold:
  - **Jebel Sahaba** is at least 13.4 ka old. Crevecoeur et al. (2021, *Sci. Rep.* 11: 9991) read it as recurrent episodes
    of violence rather than one war (SEARCH:
    [DOAJ](https://doaj.org/article/847fae4545ce4c20b097c22b0bc80d8a),
    [CNRS](https://www.cnrs.fr/en/jebel-sahaba-succession-violence-rather-prehistoric-war)).
  - **Amvrosievka** is a bison drive-and-kill site with a minimum of 650 animals. It dates to 19,000–18,000 radiocarbon
    years BP, uncalibrated (Krotova 2020; SEARCH: [record](https://zendy.io/title/10.37445/adiu.2020.04.10),
    [Encyclopedia of Ukraine](https://www.encyclopediaofukraine.com/pages%5CA%5CM%5CAmvrosiivkaarcheologicalsite.htm)).
    Calibrated, it is older than ~20 ka (KNOW).
  - **Conkey (1980)**, in *Current Anthropology* 21(5): 609–630, tests Lower Magdalenian Altamira as an aggregation site
    (SEARCH: [record](https://digitalcommons.usf.edu/kip_articles/2620)). The ~17 ka age of the Lower Magdalenian is
    KNOW.
- A council with no date cannot postdate a dated dependent. `gift_exchange ← kin_council` (row 35 below) is the same case.

## 3. The other 40 candidates

Each row gives the dependent's earliest marked date, then the prerequisite's earliest marked date (base → now where a
text fix changed it). Rows marked *P1–P8* are in §2.

| # | Dependent ← prerequisite | Kind | Dependent | Prerequisite | Evidence | Verdict |
|---|---|---|---|---|---|---|
| 1 | `birch_tar ← fire_making` | must | ~200 kya Campitello | ~164 kya (D-047 Part E's date; the first pass's F1 withdrawn, §4) | SEARCH (§4, F1; H1) | **prerequisite question** (PROPOSAL H2). Birch-tar making is itself controlled heating, and Campitello (minimum ~191 ka) is older than the node's ~164 kya. Whether to date the node by that, by an earlier hearth, or to drop the edge is the Director's (D-047 set this date). |
| 2 | `hafting ← adhesive_natural` | must | ~500 kya Kathu Pan (contested); secure ~200 kya | ~200 kya → **~70 kya** | SEARCH (§4, F3); SEARCH Kathu Pan: Wilkins et al. 2012 ([phys.org](https://phys.org/news/2012-11-archaeologists-spear-half-million-years.html)) | **prerequisite question** (PROPOSAL H1). The old adhesive date was wrong (§4). With it corrected, both hafting dates precede compound adhesives. |
| 3 | `hafting ← cordage` | must | as above | ~50 kya (direct; "older inferred") | SEARCH Hardy et al. 2020 (P1) | **prerequisite question** (H1). The node's binding is "sinew and glue". The secure ~200 kya hafting evidence is birch tar on flakes, not cord. |
| 4 | `bow_simple ← cordage` | must | | | | *P1*: no conflict |
| 5 | `raft ← cordage` | must | Flores ~1 Mya (contested); Sahul ≥50 kya (inferred) | ~50 kya (direct; older inferred) | TEXT | **no conflict**. The flagged date is the crossing the text itself marks contested. The uncontested inference (Sahul) is no earlier than cordage. |
| 6 | `ochre_processing ← grinding_stone` | must | | | | *P2*: text fix → resolved |
| 7 | `bone_tools ← knapping_blade` | must | | | | *P3*: prerequisite question (B1); the first pass's F2 withdrawn |
| 8 | `orchard ← root_crop` | alt | fig possibly ~9,000 BCE | ~8,000 BCE | TEXT; KNOW (Gilgal figs, Kislev et al. 2006) | **no conflict**. Same-group alternative: `cereal_cultivation`, ~11,500 BCE. |
| 9 | `well ← timber_frame` | alt | ~8,000 → **~8,500 BCE** | ~5,500 BCE | TEXT; the dependent was corrected (§4, F5) | **no conflict**. Same-group alternative: `stone_dry`, ~10,000 BCE. |
| 10–14 | `fermentation_grain ←` `cereal_domesticated`, `rice_wet`, `millet`, `maize`, `sorghum_pearl_millet` | alt | ~11,000 BCE Raqefet (contested) | ~9,500 / ~7,000 / ~8,000 / ~7,000 / ~3,000 BCE | SEARCH: Liu et al. 2018, *JAS Reports* ([Stanford](https://news.stanford.edu/stories/2018/09/crafting-beer-lead-cereal-cultivation), [phys.org](https://phys.org/news/2018-09-evidence-hypothesis-beer-cultivate-cereals.html)); KNOW (Godin Tepe residues) | **no conflict on the secure date**: Godin Tepe, ~3,500 BCE, follows all five. The contested date is brewing from wild cereals by Natufian foragers, 13,700–11,700 cal BP, so it precedes every domesticated cereal by construction. Optional PROPOSAL F-R (§5). |
| 15 | `flax ← cereal_cultivation` | must | wild flax use ~30 kya Dzudzuana | ~11,500 BCE | TEXT; KNOW (Kvavadze et al. 2009) | **no conflict**. The flagged date is wild fibre use, which the text labels as such. Cultivation, ~8,000 BCE, follows cereal cultivation. |
| 16–17 | `spinning_spindle ← wool`, `← cotton_cultivation` | alt | ~6,000 BCE | ~4,000 / ~5,500 BCE | TEXT | **no conflict**. Same-group alternative: `flax`. |
| 18 | `loom_warp_weighted ← spinning_spindle` | must | | | | *P4*: no conflict (W2) |
| 19–20 | `surveying ← chinese_script`, `← mesoamerican_script` | alt | ~2,500 BCE | ~1,250 / ~650 BCE | TEXT | **no conflict**. Alternatives: `cuneiform` (~2,900 BCE), `hieroglyphic` (~3,250 BCE). |
| 21 | `standard_weights ← chinese_script` | alt | ~2,600 BCE | ~1,250 BCE | TEXT | **no conflict**. Alternative: `proto_writing`, ~3,300 BCE. |
| 22–23 | `calendar_civil ← chinese_script`, `← mesoamerican_script` | alt | ~2,800 BCE | ~1,250 / ~650 BCE | TEXT | **no conflict**. Alternatives: `cuneiform`, `hieroglyphic`. |
| 24 | `medicine_recorded ← chinese_script` | alt | ~2,100 BCE | ~1,250 BCE | TEXT | **no conflict**. Alternative: `cuneiform`. |
| 25 | `astronomy_records ← mesoamerican_script` | alt | ~750 BCE | ~650 BCE | TEXT | **no conflict**. Alternatives: `hieroglyphic` with `calendar_lunar`, or `arithmetic_babylonian` (~1,800 BCE). This is one of the four nested expressions, now shown grouped (`research-tree-fixes-2026-11.md` §3.1). |
| 26 | `latitude_sailing ← astronomy_records` | alt | ~1,000 BCE | ~750 BCE | TEXT | **no conflict**. Alternative: `calendar_lunar`, ~8,000 BCE. |
| 27 | `trade ← camel` | alt | ~3,500 BCE | ~2,500 BCE | TEXT | **no conflict**. Alternatives: `donkey` (~5,000 BCE), `sail_square` (~5,500 BCE). |
| 28–29 | `taxation ← hieroglyphic`, `← chinese_script` | alt | ~3,300 BCE | ~3,250 / ~1,250 BCE | TEXT | **no conflict**. Alternative: `proto_writing`, ~3,300 BCE. |
| 30 | `appointed_officials ← chinese_script` | alt | ~2,600 BCE | ~1,250 BCE | TEXT | **no conflict**. Alternatives: `cuneiform`, `hieroglyphic`. |
| 31 | `royal_envoys ← chinese_script` | alt | ~2,400 BCE | ~1,250 BCE | TEXT | **no conflict**. Alternatives: `cuneiform`, `hieroglyphic`. |
| 32 | `territorial_kingship ← appointed_officials` | must | | | | *P5*: prerequisite question (K1) |
| 33 | `territorial_kingship ← succession_law` | alt | ~3,100 BCE | ~1,500 BCE | TEXT | **no conflict**. Alternative: `sacral_kingship`, ~3,100 BCE. |
| 34 | `craft_guilds ← municipal_charter` | alt | ~100 BCE Roman collegia | ~90 CE | TEXT; KNOW | **no conflict established**. The other alternative, `legal_code_roman`, has no `emerged` text at all (`null`), so its route cannot be dated. Systematic Roman law with jurists predates ~100 BCE (KNOW). A missing text is a content gap, not a wrong one: PROPOSAL G (§5). |
| 35 | `gift_exchange ← kin_council` | must | ~40 kya shell-bead exchange | "~10 kya and earlier ... no direct date" | TEXT; REPO | **no conflict**, as P6–P8 (W1). |
| 36 | `war_band ← kin_council` | must | | | | *P6*: no conflict (W1) |
| 37–38 | `scribal_schools ← chinese_script`, `← mesoamerican_script` | alt | ~2,500 BCE | ~1,250 / ~650 BCE | TEXT | **no conflict**. Alternatives: `cuneiform`, `hieroglyphic`. |
| 39–40 | `scriptural_canon ← alphabet_vowels`, `← brahmi` | alt | ~1,200 BCE | ~800 / ~500 BCE | TEXT | **no conflict**. Alternatives: `abjad` (~1,800 BCE), `chinese_script` (~1,250 BCE). |
| 41 | `public_theatre ← brahmi` | alt | ~534 BCE | ~500 BCE | TEXT | **no conflict**. Alternative: `alphabet_vowels`, ~800 BCE. |
| 42 | `state_exploration ← caravel` | alt | ~1,405 CE | ~1,430 CE | TEXT | **no conflict**. Alternative: `ocean_junk`, ~1,100 CE. |
| 43 | `state_propaganda ← radio_broadcasting` | alt | ~1,917 CE | 1920 CE | TEXT | **no conflict**. Alternative: `mass_press`, ~1,833 CE. |
| 44 | `hunt_season_leaders ← kin_council` | must | | | | *P7*: no conflict (W1) |
| 45 | `seasonal_aggregation ← kin_council` | must | | | | *P8*: no conflict (W1) |
| 46 | `island_voyaging ← dugout` | alt | ~8,500 BCE | ~8,000 BCE | TEXT | **no conflict**. Alternative: `raft`, inferred ≥50 kya. |
| 47 | `village_defences ← timber_frame` | alt | ~8,000 BCE | ~5,500 BCE | TEXT | **no conflict**. Alternative: `stone_dry`, ~10,000 BCE. |
| 48 | `tyranny ← citizen_militia` | alt | ~657 BCE | ~650 BCE | TEXT | **no conflict**. Alternative: `standing_army`, ~2,300 BCE. The two dates are also within a decade of each other. |
| new | `birch_tar ← adhesive_natural` | must | ~200 kya Campitello | **~70 kya** (corrected, §4, F3) | SEARCH (§4) | **prerequisite question** (H1). Birch tar is itself the oldest manufactured adhesive in the evidence. The tree makes it depend on compound adhesives, which are dated ~70 ka. |

**Tally of the audit's 48:**

| Verdict | Count | Candidates |
|---|---|---|
| text fix, resolved | 1 | row 6 |
| prerequisite question | 5 | rows 1, 2, 3, 7, 32 |
| no conflict | 42 | see below |

The 42 "no conflict" verdicts break down as:
- 28 alternatives with a same-group alternative dated no later than the dependent;
- 5 fermentation alternatives that conflict only with the contested date;
- 1 alternative whose other route is undated (row 34);
- 8 must-holds read as written: rows 4, 5, 15, 18, 35, 36, 44 and 45.

The new candidate is a sixth prerequisite question. (The first pass counted 3 text fixes and 3 prerequisite questions; rows 1
and 7 moved when F1 and F2 were withdrawn.)

## 4. The text fixes applied to `research.json`

Three `emerged` strings changed: F3, F4 and F5 below. Nothing else in the file changed: no prerequisite, cost, Age, name or
description (so neither tree-closure hash moves: STRUCTURE reads no text, TEXT reads names and descriptions only). Every node
keeps a marked date, so the D-047 test's dated-node count (`Nodes.Count − 85`) is unchanged. `emerged` is read by the loader
and by the D-047 test; no screen shows it (MEASURED: no `Sim.Ui` source reads `ResearchNode.Emerged`).

**F1. `fire_making`: WITHDRAWN in the second pass (text restored; PROPOSAL H2).**
- The first pass changed "~164 kya heat-treated silcrete (Pinnacle Point); routine by ~70 kya" to begin "~300 kya
  repeatedly used central hearth (Qesem Cave); ...", citing Shahack-Gross et al. 2014, *J. Archaeol. Sci.* (SEARCH:
  [TAU record](https://cris.tau.ac.il/en/publications/evidence-for-the-repeated-use-of-a-central-hearth-at-middle-pleis/),
  [Science News](https://www.sciencenews.org/article/fire-used-regularly-cooking-300000-years)): two superimposed use cycles
  of one hearth and habitual fire use across the 420–200 ka sequence.
- Why it was withdrawn: the node excludes "making, keeping and cooking with fire" (baseline, D-047 ruling 2), and a
  repeatedly used hearth is first of all evidence of keeping fire. The old text names a correct example of the node's
  advanced craft (heat-treated silcrete), so the evidence does not show it wrong. The date is also the one D-047 Part E set
  for the re-scoped node, and the change resolved an edge (`birch_tar ← fire_making`), which D-047 Part C forbids as a reason.

**F2. `knapping_blade`: WITHDRAWN in the second pass (text restored; PROPOSAL B1).**
- The first pass changed "~40 kya Upper Palaeolithic; earlier in Africa (~70 kya)" to "before ~285 kya Kapthurin Formation
  (Kenya); ~65 kya Howiesons Poort (southern Africa); ~40 kya Upper Palaeolithic (Eurasia)" (sources under P3).
- Why it was withdrawn: P3. The node is prismatic-core blade production, and whether the early African blades are that is a
  reading the evidence here does not settle.

**F3. `adhesive_natural`**
- Was: "~200 kya (bitumen, Umm el Tlel)"
- Now: "~70 kya bitumen hafting adhesive (Umm el Tlel); ~70-60 kya ochre-loaded plant gum (Sibudu)"
- Why it was wrong: the Umm el Tlel bitumen is ~70 ka (or ~40 ka in the first report), not ~200 ka. REPO's text says
  "about 71,000 years ago".
- Sources (SEARCH):
  - Boëda et al. 1996, *Nature* 380: 336, ~40 ka ([Nature](https://www.nature.com/articles/380336a0)).
  - Boëda et al. 2008, *Antiquity*, "Middle Palaeolithic bitumen use at Umm el Tlel around 70 000 BP"
    ([HAL](https://hal.parisnanterre.fr/hal-03033277)).
  - Wadley, Hodgskiss and Grant 2009, *PNAS*: compound adhesives of plant gum and ochre at Sibudu, about 70 ka
    ([PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC2700998)).
  - Second pass (SEARCH): the 2008 article's own summary — natural bitumen on stone tools at about 70,000 BP, "taking the
    practice back a further 30 000 years" from the 1996 report
    ([Cambridge Core abstract](https://resolve.cambridge.org/core/journals/antiquity/article/abs/middle-palaeolithic-bitumen-use-at-umm-el-tlel-around-70-000-bp/21D16BC1EA8A3841BB27C8108FD75C60));
    and the Sibudu compound adhesive at about 70 ka with heat-treated compound glue at about 61 ka (PNAS 106(24):
    9590–9594, [PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC2700998)).
- Why this is a correction and not a date moved to make an edge pass: the old text attaches ~200 kya to a site whose
  bitumen is ~70 ka, and the correction makes two edges worse, not better.
- **Effect.** The correct date is *later* than the old one, so the fix exposes a question rather than removing one.
  `hafting ← adhesive_natural` widens from 300 ka to 430 ka, and `birch_tar ← adhesive_natural` appears. Both are
  PROPOSAL H1.

**F4. `grinding_stone`**
- Was: "~30 kya Bilancino; routine in Natufian ~12 kya"
- Now: "before ~285 kya Kapthurin Formation (grindstones with pigment); ~100 kya Blombos (ochre-grinding slabs); ~30 kya
  Bilancino (plant flour); routine in Natufian ~12 kya"
- Why it was wrong: the node's own description includes pigment, and grindstones used with pigment date before 285 ka
  and at ~100 ka.
- Sources (SEARCH): McBrearty and Tryon 2006; Henshilwood et al. 2011, *Science* 334: 219–222. Links are under P2.
- It resolves `ochre_processing ← grinding_stone`. Why that is not the reason for the change: P2.

**F5. `well`**
- Was: "~8,000 BCE Atlit-Yam (stone-lined); ~5,200 BCE LBK oak-lined wells (Saxony)"
- Now: "~8,500 BCE western Cyprus (Kissonerga-Mylouthkia wells); ~6,900-6,300 BCE Atlit-Yam (stone-lined); ~5,200 BCE LBK
  oak-lined wells (Saxony)"
- Why it was wrong: Atlit-Yam's well is about 8,000 years *old*, not 8,000 BCE. The settlement dates to 6,900–6,300 BCE.
  The western Cypriot wells are about 1,000 years older, built as long as 10,500 years ago. REPO's text says "roughly
  10,500 to 8,300 years ago".
- Sources (SEARCH):
  - Cyprus well reports, 2009 ([phys.org](https://phys.org/news165070153.html),
    [NBC](https://www.nbcnews.com/id/wbna31525549)).
  - Atlit-Yam, 6,900–6,300 BC ([Atlas Obscura](https://www.atlasobscura.com/places/atlit-yam)).
  - Galili and Nir 1993, *The Holocene*, on the Pre-Pottery Neolithic well
    ([AGRIS record](https://agris.fao.org/search/fr/records/65de23f04c5aef494fda3803)).
- The name Kissonerga-Mylouthkia for the Cypriot wells (Peltenburg et al. 2000) is KNOW.
- Second pass (SEARCH): Galili and Nir 1993 date the Atlit-Yam well to about 7,900 BP (radiocarbon; the habitation
  8,100–7,500 BP, a later review c. 9,250–8,000 cal BP, that is about 7,300–6,050 BCE)
  ([AGRIS record](https://agris.fao.org/search/fr/records/65de23f04c5aef494fda3803)). Nothing in any source puts it at
  ~8,000 BCE. The 2009 radiocarbon reports put the Kissonerga well at 9,000–10,500 years old
  ([NBC](https://www.nbcnews.com/id/wbna31525549)); a 2024 *Scientific Reports* study assigns the remains in well 133 to
  c. 7,600–6,800 cal BCE by period, because they were not dated directly
  ([Springer](https://link.springer.com/10.1038/s41598-024-60161-z)). "~8,500 BCE" is the early end of the radiocarbon
  range, so it is an approximation, and the Director may prefer "~8,500–7,000 BCE". The earliest date moves by ~500 years
  and resolves no candidate; it corrects a before-present age written as BCE.

**What the fixes did and did not move (measured on the second pass, with F3–F5 only):**
- `research.json` SHA-256 `1591427a…` (base) → `fd4b9d34…`. The four generated reports that embed it were regenerated
  (`research-corpus-audit.md`, `research-calibration-report.md`, `research-gameplay-unlock-audit.md`,
  `research-consequence-audit.md`); against the base, each differs in that one header line only, and each `--check` exits 0.
- The research tooling's live checks (`research-tool-tests`: `validate.py --live` with 0 errors, 13 of 13 fixtures and 8
  of 8 mutants, the sourcing-queue tests with 5 of 5 mutants, `export_runtime_texts.py --check`) exit 0, and
  `knowledge-texts.json` is current: the educational texts come from the corpus, not from `emerged`.
- `tree_closure_check.py`: no blocking defect. Release `ResearchContentTests` and `TreeClosureBaselineTests`: 63 of 63 pass,
  including `D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent`. The tier runs are in the lane report.
- The research-economy candidate file (`knowledge-redesign/research-cost-pass/candidate/research.candidate.json`) is the
  shipped file plus overlays, pinned by `ResearchCandidateTests`. It now carries the same three lines (commit `7f9b1b0`).
  The first pass had left it stale, and Tier 1 caught it: `CandidateFile_DiffersFromShippedOnlyInTheTuningSwitches_…`
  failed. D-074's text change needed the same update. `ResearchCandidateTests`: 3 of 3 pass.
- `measure_audit.py`: 48 candidates (14 must-hold, 34 alternatives) before and after; the §2–§4.5 output is identical.

## 5. Proposals for the Director (nothing applied)

**H1. The adhesive and hafting cluster.**
- The trigger: `hafting ← cordage AND adhesive_natural` and `birch_tar ← fire_making AND adhesive_natural`, set against
  the corrected adhesive date (~70 ka).
- The evidence:
  - Birch-tar flakes at Campitello are at least ~191–200 ka (SEARCH: Mazza et al. 2006, as cited in
    [Cosmos](https://cosmosmagazine.com/palaeontology/lifting-the-veil-from-neanderthal-technology) and
    [PMC6825292](https://pmc.ncbi.nlm.nih.gov/articles/PMC6825292)).
  - Compound adhesives are ~70 ka.
  - Direct cordage is 41–52 ka.
- An internal inconsistency to settle first: `research.json` says hafting is "secure ~200 kya", while REPO's educational
  text says "glued hafts are secure by about 70,000 years ago".
- Options:
  - **(a)** `birch_tar ← fire_making` only (or no prerequisite at all, H2 (b)), and hafting's glue as
    `(adhesive_natural OR birch_tar)`. Birch tar becomes the old manufactured glue it is.
  - **(b)** Re-scope `hafting` to the ~70 ka glued hafts (REPO's reading), and make the ~200 kya clause context.
  - **(c)** Keep the order as a gameplay abstraction: compound adhesives as the entry point for all engineered glue. Say so
    in the texts.
- (a) and (b) change prerequisites or depths, which moves layout, calibration and possibly goldens. That is the
  Director's call.

**H2. `birch_tar ← fire_making`** (row 1; the first pass's F1, withdrawn). Birch-tar making is dry distillation, "controlled
heating without oxygen" in the node's own words, and its earliest evidence (Campitello, minimum ~191 ka; H1) is older than
`fire_making`'s ~164 kya. Options:
- **(a)** Text: date `fire_making` by earlier evidence of the advanced craft, for example the first pass's Qesem hearth
  (~300 ka, F1's source) or Campitello's tar itself. This moves a date D-047 Part E set for the re-scoped node.
- **(b)** Prerequisite: `birch_tar` without `fire_making`: basic fire is baseline (D-047 ruling 2), and tar-making is its own
  craft. Combine with H1's choice for the adhesive edge.
- **(c)** Keep the order as a gameplay abstraction and say so in the texts.

**B1. `bone_tools ← knapping_blade`** (P3, row 7; the first pass's F2, withdrawn). Options:
- **(a)** Text on the prerequisite: read "prismatic cores" broadly and date `knapping_blade` from the African Middle
  Pleistocene blades (F2's text: "before ~285 kya Kapthurin Formation (Kenya); ~65 kya Howiesons Poort ...").
- **(b)** Text on the dependent: `bone_tools` is defined by the groove-and-splinter technique, an Upper Palaeolithic method;
  mark the Katanda points (~90 ka, shaped by grinding; KNOW) as a precursor or drop them, so the node dates from ~40 kya.
- **(c)** Prerequisite: `bone_tools` from `knapping_levallois` (or with no knapping prerequisite), keeping the dates.
- **(d)** Keep the order as a gameplay abstraction.
- (a) and (b) are text changes that turn on the node's definition; (c) changes depths and the layout.

**K1. `territorial_kingship ← appointed_officials`** (P5). Options:
- **(a)** Date `appointed_officials` from the First Dynasty. This is a text change if First Dynasty title-holders count
  as "appointed officials". The candidate shrinks to about 100 years, within the precision of both dates.
- **(b)** Require `(sacral_kingship OR succession_law)` only, with appointed officials as a later strengthening of the
  realm.
- **(c)** Keep as is.

**W1. `kin_council` wording.** Reword the text so that it carries no date marker, for example "no direct date: councils
of household heads are recorded ethnographically among foragers and early farmers; their prehistoric antiquity is
inferred". This removes the four heuristic hits on rows 35, 36, 44 and 45.
- It is not applied because the text is not wrong. Removing the marker would also move the D-047 test's pinned
  dated-node count from `Nodes.Count − 85` to `− 86`, a test pin that is the Director's to move.

**W2. `loom_warp_weighted` wording.** Separate the evidence for weaving from the evidence for the loom. For example: loom
weights ~5,500 BCE, with plain-weave bast cloth earlier at Çatalhöyük, 6,700–6,500 cal BC. Any marker in the text is read
by the heuristic, so the conflict only disappears if the textile date is dropped or the spindle is dated earlier.

**F-R. `fermentation_grain` (optional).** If the contested Natufian precursor is meant to stand, two changes would make
the order consistent:
- add `cereal_cultivation` (~11,500 BCE) to the cereal group; or
- drop "Raqefet (contested)" from `emerged`.

**G. `legal_code_roman` has no `emerged` text.** It is the one undated alternative among the candidates. A dated text is
a content addition, not a correction, so it is left to the content owner.

**T. A test for OR alternatives.** The D-047 test checks only must-hold prerequisites of dependents dated after 3000 BCE
(audit §7.3). A companion rule would apply to the same post-3000 BCE window: every OR group must hold at least one
alternative dated no later than its dependent. Measured on the current file, it checks 67 OR groups (the last section of
`measure_audit.py`'s output) and reports:
- 0 violations if an undated alternative counts as satisfying the group;
- 1 violation (`craft_guilds`, row 34) if it does not.

Not implemented, because a new content rule is the Director's to make.

## 6. What could not be checked

- **No source page was read (no FETCH).** Every outside fact above is SEARCH (a search engine's summary naming the
  source) or KNOW.
- **Hosts the Director could allow.** `digitalcommons.usf.edu`, `en.wikipedia.org` (first pass) and
  `pubmed.ncbi.nlm.nih.gov` (second pass) failed to resolve from this session. Allowing them in the session's network
  settings would let a follow-up read the primary pages.
- **Second pass.** It re-ran the searches behind the three applied fixes (F3–F5) and behind P3, with the results cited in
  place; they agree with the first pass on every date used. It found nothing that settles P3's "prismatic" reading.
- **Specific gaps:**
  - Whether the Kapthurin blade cores are "prismatic" (P3).
  - Whether First Dynasty officials were appointed and dismissed by written order (P5).
  - The calibrated age of Amvrosievka and of the Lower Magdalenian (KNOW only).
  - The dates of the remaining no-conflict rows, beyond the texts themselves. Their verdict rests on the texts' own
    dates (TEXT), not on outside evidence.
- **Candidates the heuristic cannot see.** The review covers only the heuristic's candidates. A wrong date that does not
  invert an edge is invisible to it, as are the bare-year texts the D-047 test also skips.
