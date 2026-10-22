# Nuclear science and the geology of uranium: research brief and draft node chain

**Status:** PROPOSED research input for the M6 knowledge-tree redesign. Nothing here is ratified, and no
existing node, prerequisite or unlock has been changed.
**Access date for every source:** 2026-10-22.
**Companion file:** `../sources/nuclear-geology-sources.json` (250 records, `NUC-01` … `NUC-250`).
**Scope:** the full nuclear case-study agenda, plus the geology and resource-knowledge path it depends on.

---

## 0. Method and limits of the evidence

- **How sources were retrieved.** Every source was retrieved this session with WebSearch, mostly with
  queries restricted to the publisher's own domain (NobelPrize.org, APS, AIP, IAEA, NRC, DOE and its labs,
  USGS, BGS, ICRP, NEA, museums). **WebFetch failed for every domain tried (DNS resolution error)**, so no
  page was read in full. Each claim rests on the search tool's extract of the page or pages cited. Where
  extracts from different sources disagreed, the disagreement is reported (§3) and not smoothed over.
- **Search budget.** The session's web-search allowance ran out near the end. Two follow-up checks could
  not be made; they are listed in §5.
- **Wikipedia** was used only to find sources. No claim in this brief rests on it.
- **World Nuclear Association (WNA)** is the industry's trade association. It is used for fuel-cycle,
  reactor and production facts. Its statements about market share or prospects are marked *(industry)*.
- **Weapons.** Weapons are covered only at the level of dates, treaties and the facts needed for the
  scope decision in §4.6. No design information is included.

**Tags used in §2.** Each fact carries a stage tag and a confidence grade:

| Stage tag | Meaning |
|---|---|
| `OBS` | A phenomenon was observed or a material was recognised. |
| `EXPL` | A scientific explanation or theory was established. |
| `DEMO` | First engineering demonstration (a prototype or a one-off). |
| `COMM` | First commercial or industrial use. |
| `ADOPT` | Widespread adoption, or a current-state statistic. |
| `INST` | An institution, treaty, standard or prize. |

Confidence: **H** means several authoritative sources agree, or one primary or official source states it
directly. **M** means one good secondary source, minor disagreement between sources, or a figure that
came through a combined search extract. **L** means a tradition or a weakly attributed figure; do not
build mechanics on it.

---

## 1. The design facts with the most consequence

1. **Uranium ore had uses and hazards long before anyone understood it.** Joachimsthal silver miners
   threw away "Pechblende" as a nuisance mineral (reported in the district from 1565) [NUC-04 · M].
   Uranium became a glass and ceramic colourant from the 1830s [NUC-06, NUC-07 · M]. Radium extraction
   drove uranium-ore mining from 1898 until the 1930s [NUC-10, NUC-187 · M]. Ore-district miners suffered
   very high lung-disease mortality from Agricola's time onward, centuries before radon was suspected
   [NUC-148 · M]. Deposits, their value and their dangers therefore exist whatever a polity has
   researched. Research changes what a polity can *do* about them.
2. **Finding, proving and extracting are separate stages, and each takes years.** About one exploration
   project in a thousand finds an economic uranium deposit, and about one in three of those reaches
   mining [NUC-180 · M]. Cigar Lake was found in 1981 and reached commercial production in 2015
   [NUC-190 · M]. The 2026 Red Book release gives 15–20 years as the typical lead time for a uranium
   mining project [NUC-171 · M]. Official knowledge also misses deposits: the 1952 Mi Vida strike lay in
   ground the Atomic Energy Commission had judged barren [NUC-182 · M].
3. **Deposit type decides how a deposit is found and mined.** The IAEA scheme recognises 15 types and 36
   subtypes [NUC-162 · H]. Sandstone roll-front ores suit in-situ leaching, which now produces more than
   half of world uranium [NUC-97 · M]. Athabasca unconformity ores sit at the contact between sandstone
   and basement and are extremely rich (up to about 20% U) [NUC-163, NUC-164 · M]. At Olympic Dam, the
   largest known deposit, uranium is a by-product of copper
   [NUC-163 · M].
4. **Supply is concentrated geographically.** About two-thirds of Manhattan Project uranium came from one
   Congolese mine and about one-sixth from one Canadian mine [NUC-185 · M]. Today Kazakhstan alone supplies
   about 40% of world mine output [NUC-99 · M].
5. **"Reactor" is not one capability, because the moderator decides whether enrichment is needed.**
   Graphite or heavy water lets a reactor run on natural uranium; light water needs fuel enriched to about
   3–5% U-235 [NUC-80, NUC-119 · H]. A polity without enrichment can still run graphite or heavy-water
   reactors.
6. **The steps sensitive to weapons proliferation are enrichment and reprocessing, not reactors.**
   Industry and IAEA sources agree on this [NUC-117, NUC-118 · M]. Commercial fuel is LEU at about 3–5%,
   the safeguards boundary between LEU and HEU is 20%, and weapons need uranium enriched above 90% or
   plutonium [NUC-115, NUC-119 · H]. The existing `nuclear_fission` node bundles "reactor" with "nuclear
   weapon"; §4.6 proposes separating them.
7. **Electricity is an operating input to nuclear facilities, not a knowledge prerequisite for them.**
   CP-1 demonstrated a controlled chain reaction at about half a watt [NUC-53 · M]. Gaseous diffusion used
   about 2,500 kWh per SWU
   (separative work unit), against about 50 for modern centrifuges, a roughly fifty-fold difference in a
   plant input [NUC-81 · H]. A power reactor's unit size is limited to about 10% of the grid it feeds
   [NUC-221 · H]. Electricity belongs in plant recipes and grid constraints, not in research
   prerequisites.
8. **The hazards were felt before protection knowledge existed.** Edison's X-ray assistant died in 1904
   [NUC-133 · H]. Radium dial painters were poisoned in the 1920s [NUC-135 · H]. Radiation protection
   became an institution only in 1928, and dose limits have been lowered repeatedly since [NUC-138,
   NUC-140, NUC-142 · H].
9. **Radiation detection is the shared enabler.** Prospecting, protection, reactor instrumentation and
   nuclear medicine all depend on counters and scintillators [NUC-150, NUC-154, NUC-175, NUC-206 · H/M].
10. **The back end of the fuel cycle needs the geology path again.** High-level waste maps to deep
    geological disposal [NUC-125 · M]. Only a defence-waste repository operates (WIPP, since 1999), and
    the first spent-fuel repository (Onkalo) still awaited its government operating licence at the time
    of search [NUC-126, NUC-128, NUC-129 · H].
11. **Peaceful isotope use depends on small research reactors and a supply chain that cannot wait.**
    Tc-99m serves about 40 million procedures a year. Its parent Mo-99 has a 66-hour half-life and Tc-99m
    a 6-hour one, so production must be continuous [NUC-199, NUC-200 · H].

---

## 2. Fact base by agenda topic

### 2.1 Uranium as an element, pitchblende mining, and uranium glass and glazes

- `OBS` Joachimsthal (Jáchymov) grew on a silver boom from about 1500. Miners called the black,
  uranium-bearing mineral that spoiled their silver ore "Pechblende", and pitchblende is reported from the
  district as early as 1565 [NUC-04 · M].
- `EXPL` In 1789 Klaproth recognised the oxide of a new element in pitchblende and named it uranium after
  the recently discovered planet. He could not reduce it to the metal [NUC-01, NUC-02 · H].
- `EXPL` In 1841 Péligot showed that Klaproth's product was uranium dioxide, and made uranium metal by
  reducing uranium tetrachloride with potassium [NUC-01 · H].
- `COMM` Uranium became a colourant before it had any other use. Corning dates the earliest mention of
  uranium glass to 1817 and Josef Riedel's popularisation of it to 1834. ORAU puts first use in the 1830s
  [NUC-06, NUC-07 · M]. Bohemian glass and ceramics drew on Jáchymov uranium from the mid-1800s
  [NUC-03 · M]. A Joachimsthal silver works was converted to uranium-colour production in 1847, Patera's
  "uranium yellow" process followed in 1851, and a dedicated plant opened in 1854 [NUC-04 · M].
- `ADOPT` Consumer use continued into the 20th century. Homer Laughlin's Fiesta dinnerware (1936) used a
  red glaze in which up to about 14% of the glaze weight was uranium. Production stopped in WWII when the
  government took the company's uranium, and resumed in 1959 with depleted uranium [NUC-08 · M]. EPA warns
  against using these antiques for food [NUC-09 · H]. Uranium glass followed the same pause and resumption
  [NUC-07 · M].
- `COMM` After 1898, radium became the valuable product of uranium ore. The Joachimsthal factory held a
  radium monopoly from 1909 to 1913 [NUC-04 · M]. Colorado Plateau carnotite was the world's main radium
  source from 1912 to 1922, and later a vanadium source [NUC-10 · M]. Port Radium, found at Great Bear Lake
  in 1930, mined radium first, with uranium a minor by-product until the 1940s [NUC-187, NUC-188 · M]. New
  mines in the US and the Belgian Congo ended Joachimsthal's dominance [NUC-05 · M].
- `OBS` In the United States, uranium was first found in the Central City district of Colorado in 1871,
  and the 1898 discovery of radium set off a wide search for uranium minerals [NUC-10 · M].

### 2.2 The discovery and study of radioactivity

- `OBS` Röntgen discovered X-rays on 8 November 1895 and reported them on 28 December 1895. He received
  the first Nobel Prize in Physics in 1901 [NUC-13, NUC-14 · H]. X-rays prompted Becquerel's question.
- `OBS` In 1896 Becquerel tested whether phosphorescent uranium salts emitted penetrating rays after
  exposure to sunlight. Plates kept in a drawer through overcast days, and developed on 1 March, were
  strongly exposed anyway. He attributed the emission to the uranium itself [NUC-11, NUC-12 · H].
- `OBS` The Curies measured ionization currents with the piezoelectric quartz electrometer that Pierre
  and Jacques Curie had built earlier [NUC-18 · H]. Working through fractions of pitchblende, by late June
  1898 they had a substance about 300 times more active than uranium. A July 1898 paper announced
  polonium, and on 26 December 1898 they reported radium in a barium-like fraction [NUC-15, NUC-17 · H]. The raw material was Joachimsthal residue: APS
  says about a ton, supplied by the Austrian government; another account says about ten tonnes
  [NUC-17, NUC-04 · M]. Marie Curie coined the word "radioactivity" [NUC-11 · M].
- `EXPL` Rutherford reported alpha and beta rays in 1898–1899 [NUC-19 · H]. Villard observed an
  undeflected, highly penetrating radiation from radium in 1900 but did not treat it as a new kind of ray.
  Rutherford later named it gamma [NUC-22 · H].
- `EXPL` At McGill, Rutherford and Soddy developed the theory that radioactivity is the spontaneous
  disintegration of atoms, one element turning into another (first version 1902, refined 1903). Beta rays
  were shown to be electrons and alpha particles to be helium ions [NUC-19, NUC-20 · H].
- `INST` Nobel Prizes: Physics 1903 to Becquerel and the Curies; Chemistry 1908 to Rutherford; Chemistry
  1911 to Marie Curie for discovering radium and polonium and isolating radium [NUC-12, NUC-16, NUC-19 · H].

### 2.3 Atomic theory: Dalton's atoms to the neutron

- `EXPL` Dalton's *A New System of Chemical Philosophy* appeared in parts from 1808 to 1827. It treated
  each element as made of atoms of characteristic mass [NUC-23 · H]. His first table of relative atomic
  weights dates from 1803. The values stayed uncertain for decades because chemical formulas were unknown
  [NUC-24 · M]. Britannica calls this the first application of atomic theory to chemistry [NUC-25 · M].
- `EXPL` Mendeleev's 1869 periodic table left gaps whose predicted elements were found: gallium in 1875
  and germanium in 1886 [NUC-29 · H].
- `EXPL` In 1897 J. J. Thomson showed that cathode rays are particles (electrons) and measured their
  charge-to-mass ratio. His 1906 prize cited conduction of electricity in gases [NUC-26 · H].
- `EXPL` Geiger and Marsden saw rare large-angle scattering of alpha particles from gold foil in 1909.
  Rutherford explained it in 1911 with a small, dense, positive nucleus [NUC-27, NUC-28 · H].
- `EXPL` In 1913 Soddy formulated the concept of the isotope (same chemistry, different atomic weight)
  and coined the word [NUC-21 · H]. Aston's mass spectrograph (1919) found isotopes in many stable
  elements [NUC-30 · H]. Urey's group found deuterium in 1931 [NUC-33 · H].
- `EXPL` Bothe and Becker found a penetrating radiation from beryllium in 1930. In 1932 Chadwick showed it
  consisted of neutral particles of about the proton's mass: the neutron [NUC-31, NUC-32 · H].
- *Design reading.* The isotope concept underlies both enrichment (separating U-235) and heavy water
  (separating deuterium). Aston's mass spectrograph is the ancestor of the wartime calutron (§2.10).

### 2.4 Artificial radioactivity, accelerators and slow neutrons

- `OBS` The Joliot-Curies made aluminium radioactive by alpha bombardment, announcing it on
  15 January 1934. This was artificial radioactivity [NUC-34 · H].
- `OBS` Fermi produced neutron-induced radioactivity from 20 March 1934 and found that neutrons slowed by
  water or paraffin were far more effective. Slow neutrons are the physical basis of moderation
  [NUC-35 · H].
- `DEMO` Lawrence's cyclotron (dated to 1929 in Nobel material; Nobel Prize 1939) produced hundreds of
  radioisotopes [NUC-36 · H]. Berkeley's Radiation Laboratory was established in 1936 [NUC-37 · M]. In
  1937 Perrier and Segrè identified technetium, the first artificially made element, in cyclotron-irradiated
  molybdenum [NUC-38 · H].

### 2.5 Fission, the chain reaction and criticality

- `OBS` In 1934 Ida Noddack suggested that bombarded uranium might break into large fragments. Nobody
  followed the idea up, and Fermi's "transuranic" interpretation stood for four years [NUC-40 · H].
- `OBS` In December 1938 Hahn and Strassmann showed chemically that a supposed radium product was
  barium. Their paper was submitted on 22 December 1938 [NUC-40, NUC-42 · H].
- `EXPL` Meitner and Frisch interpreted the result as the uranium nucleus splitting, and borrowed the
  word "fission" from biology. Their *Nature* letter was submitted on 16 January and published on
  11 February 1939 [NUC-41 · H].
- `EXPL` In 1939 Halban, Joliot and Kowarski reported secondary neutrons from fission. Groups in Paris and
  at Columbia found two or three per fission, enough in principle for a chain reaction [NUC-45, NUC-41 · M].
- `EXPL` In February 1939 Bohr reasoned that the rare isotope U-235 causes slow-neutron fission. The
  Bohr–Wheeler theory was published on 1 September 1939, and in early 1940 Nier's separated U-235 sample
  let Dunning's group confirm it [NUC-44 · H].
- `EXPL` Szilard conceived a neutron chain reaction on 12 September 1933, before fission was known, and
  patented it in 1934. The patent covered chain reactions and the idea of a critical mass, though parts of
  its physics were wrong [NUC-46, NUC-47 · M].
- `DEMO` On 2 December 1942 Chicago Pile-1 sustained the first controlled chain reaction. It was a pile
  of graphite blocks holding uranium metal and oxide, controlled by cadmium rods, in a squash court under
  the stands of Stagg Field [NUC-50, NUC-51, NUC-52 · H]. It first ran at about 0.5 W and later at up to
  about 200 W [NUC-53 · M]. In 1943 it was rebuilt with shielding at Site A as CP-2 [NUC-51 · H].
- `DEMO` Material purity was the hidden prerequisite. CP-1's graphite had to hold boron to fractions of a
  part per million, and National Carbon's "AGOT" nuclear-grade graphite was still arriving on the day of
  criticality [NUC-54, NUC-55 · M].
- `DEMO` Scale-up came quickly. The X-10 pile at Oak Ridge (natural uranium, graphite, air-cooled, built
  by DuPont) went critical on 4 November 1943. Hanford B, a graphite reactor cooled with Columbia River
  water, went critical on 26 September 1944 [NUC-56, NUC-57 · H].
- `INST` The Fermi–Szilard "Neutronic Reactor" patent was filed on 19 December 1944 and issued in May 1955
  [NUC-49, NUC-48 · H].

### 2.6 Uranium geology and deposit types

- `EXPL` **Abundance.** The crust averages about 2.7 ppm uranium. Granites and shales often hold
  5–25 ppm, and seawater about 0.003 ppm [NUC-167, NUC-168 · M/H]. Other compilations seen in search gave
  somewhat different crustal averages, so treat 2.7 ppm as an order of magnitude. Natural uranium is
  about 99.3% U-238, 0.7% U-235 and 0.005% U-234 [NUC-169 · M].
- `INST` **Classification.** The 2013 IAEA scheme, used in the NEA/IAEA Red Book since 2014, has 15 types
  and 36 subtypes. Examples include intrusive, polymetallic hematite breccia complex, Proterozoic
  unconformity and sandstone [NUC-162 · H]. IAEA-TECDOC-1842 (2018) describes the scheme with examples
  [NUC-161 · H].
- `OBS` **Unconformity deposits.** All Saskatchewan production for 40 years has come from Athabasca
  unconformity deposits. Some ore approaches 20% uranium, richest at or just above the sandstone–basement
  contact [NUC-163 · M]. Cameco calls Cigar Lake the world's highest-grade mine, at about 100 times the
  world average grade [NUC-164 · M, company].
- `OBS` **Breccia complex.** Olympic Dam is the largest known uranium deposit, and its uranium is
  recovered only as a by-product of copper [NUC-163 · M].
- `OBS` **Sandstone deposits.** There are two main forms: tabular deposits on the Colorado Plateau, and
  roll-fronts in the Wyoming basins and south Texas [NUC-165 · H]. Tabular ore is concentrated by
  carbonaceous material; roll-fronts form at a regional oxidation–reduction front [NUC-166 · H]. Most
  economic US uranium is sandstone-hosted. Vein deposits occur in hard rock, and phosphorites yield
  by-product uranium [NUC-10 · M].
- `OBS` **Historic rich ores.** Shinkolobwe (Belgian Congo) averaged over 2% uranium [NUC-185 · M]; Port
  Radium is noted above [NUC-187 · M].
- *Design reading.* Deposit type should be a property of each world-generated deposit. It determines
  which methods can detect the deposit, which extraction route applies (ISL, open pit or underground) and
  which by-products are linked (copper, phosphate, vanadium, radium).

### 2.7 Prospecting methods

- `COMM` **Plants.** USGS botanists used selenium-indicator plants, including some *Astragalus* species,
  to map mineralised ground on the Colorado Plateau [NUC-179 · H].
- `COMM` **Radioactivity assay of samples.** Gold-leaf electroscopes measured the activity of uranium ore
  samples [NUC-155 · M].
- `COMM` **Ground radiometrics.** For several field seasons before 1949, the USGS (working for the AEC)
  prospected with car-mounted Geiger–Mueller counters. Rocks rich in uranium or thorium are surrounded by
  an abnormally strong gamma field that can be detected from a road [NUC-175 · H].
- `ADOPT` **Airborne radiometrics.** By 1958 airborne radioactivity surveys were an established guide to
  uranium exploration [NUC-176 · H]. The ARMS-I system, with six NaI(Tl) detectors, flew 1958–1961
  [NUC-177 · M]. Modern gamma-ray spectrometry maps potassium, uranium and thorium from aircraft, ground,
  vehicles, boreholes and laboratory samples [NUC-178 · H].
- `INST` **State incentives drive prospecting.** As the sole legal buyer, the AEC guaranteed prices from
  1948, paid a $10,000 bonus for each new high-grade deposit and built access roads. About 800 Colorado
  Plateau producers were active by the mid-1950s [NUC-182, NUC-183 · M].

### 2.8 Discovery versus extraction: locating, identifying, proving, extracting

- `INST` **Exploration stages.** The IAEA describes a progression from reconnaissance to advanced
  exploration to resource definition, in which spending rises as risk falls. About one project in 1,000
  finds an economic deposit, and about one in three of those reaches mining [NUC-180 · M].
- `INST` **Classification of knowledge about a deposit.** USGS Circular 831 classifies resources by
  geologic assurance and economic feasibility, separates identified from undiscovered resources, and notes
  that subeconomic resources can become reserves when prices change [NUC-172 · H]. The JORC Code grades
  resources as Inferred, Indicated or Measured. Only the part that remains viable after "modifying factors"
  (mining, processing, infrastructure, economic, legal, environmental, social and governmental) becomes a
  Probable or Proved Ore Reserve. Words like "ore" and "reserve" are not to be used for resources
  [NUC-173, NUC-174 · H].
- `OBS` **Lead times.** An older IAEA estimate gives 8–10 years from discovery to production, plus five
  or more years of exploration [NUC-181 · M]. The 2026 Red Book release gives 15–20 years for
  mining-project development [NUC-171 · M].
- `OBS` **Worked example, Cigar Lake.** Found by surface drilling in 1981, delineated 1982–1986, test-mined
  1987–1992, developed underground from 2005, flooded in 2006 and 2008, in commercial production from
  2015 [NUC-190, NUC-191 · M]. World Nuclear News reported its entry into operation [NUC-189 · M].
- `ADOPT` **Global resource base.** Identified recoverable resources were 7,934,500 tU at up to
  USD 260/kgU on 1 January 2023 [NUC-170 · H]. The 2026 edition reports more than 8.1 Mt, and 2024 world
  production of 61,924 tU [NUC-171 · M].
- `INST` **Assessing what is still undiscovered.** The USGS estimates undiscovered resources from the
  properties of known deposits of the same type, because the undiscovered ones are by definition unknown
  [NUC-250 · H]. World generation can work the same way.
- *Design reading.* The four verbs map onto distinct knowledge and activities. **Locating** needs survey
  activity on accessed ground (§2.7, §2.21). **Identifying** needs mineral and chemical analysis. **Proving**
  needs drilling, assays and an estimate of grade and tonnage. **Extracting** needs mining and milling
  capacity, and the price to justify it.

### 2.9 Mining, milling and processing

- `ADOPT` Uranium is mined in open pits or underground, or recovered by in-situ leaching (ISL)
  [NUC-98 · M]. ISL now produces more than half of world output (about 55–56% in 2022–2024) [NUC-97 · M].
  Kazakhstan produces only by ISL. It has led world production since 2009 and supplied about 39–40% of it
  in 2024–2025 [NUC-99 · M].
- `COMM` A conventional mill crushes and grinds the ore and leaches it, usually with sulphuric acid. It
  purifies the solution by solvent extraction or ion exchange, then precipitates and dries yellowcake of
  about 80–85% U3O8. Recovery is 90–95%. The tailings stay radioactive and must be confined [NUC-100,
  NUC-101 · H].
- *Design reading.* Milling creates a waste stock (tailings) and needs acid and reagents. The "radioactive
  ore processing" node in §4 also covers radium recovery, the first commercial product of these ores.

### 2.10 Conversion, enrichment and fuel fabrication

- `EXPL` Moissan isolated fluorine in 1886 by electrolysis and won the 1906 Chemistry prize. The
  electrolytic route is still the industrial method [NUC-113, NUC-114 · H/M]. Conversion depends on it.
- `COMM` **Conversion.** Yellowcake is reacted with fluorine to make uranium hexafluoride (UF6). Metropolis,
  Illinois is the only US plant, and it restarted in 2023 after an idle period [NUC-102, NUC-103 · H/M].
- `DEMO` **Electromagnetic separation.** The Y-12 calutrons separated U-235 electromagnetically, using
  about 14,700–15,000 tons of borrowed Treasury silver for magnet windings. The silver was returned by 1970
  [NUC-107, NUC-108 · H].
- `DEMO`/`COMM` **Gaseous diffusion.** K-25 at Oak Ridge, built 1943–1945, was a half-mile-long building in
  which UF6 passed through more than 3,000 stages with nickel barriers. It employed over 10,000 workers in
  1945 and fed the uranium weapon [NUC-105, NUC-106 · H].
- `ADOPT` **Centrifuges.** Centrifuge enrichment was shown to be viable by the 1960s, but engineering and
  material difficulties delayed its adoption. It now accounts for all new capacity. It uses about 50 kWh
  per SWU against about 2,500 for diffusion [NUC-81, NUC-104 · H/M].
- `INST` **Enrichment levels.** Commercial fuel is LEU at about 3–5% U-235. HALEU runs from 5 to 19.75%.
  The safeguards line between LEU (indirect-use material) and HEU (direct-use) is 20%. Weapons need
  uranium enriched above 90%, or plutonium [NUC-119, NUC-120, NUC-115, NUC-116 · H].
- `ADOPT` **Fuel fabrication.** The oxide is converted to UO2 powder, pressed and sintered into pellets,
  sealed in zirconium-alloy tubes and built into assemblies. LWR fuel is enriched up to about 4.8%
  [NUC-109, NUC-110 · M/H]. Natural zirconium carries about 2% hafnium, which absorbs neutrons. The
  Bureau of Mines produced hafnium-free zirconium for naval reactors [NUC-111 · M]. About 90% of zirconium
  metal now goes to hafnium-free reactor cladding [NUC-112 · M].

### 2.11 Reactor engineering, the reactor families and first grid power

- `EXPL` **Moderation.** Graphite or heavy water allows natural-uranium fuel; light water absorbs more
  neutrons, so light-water reactors need enriched fuel [NUC-80 · M; NUC-81 · H].
- **Which plant was "first" depends on the criterion:**
  - `DEMO` First usable electricity: EBR-I in Idaho on 20 December 1951. It lit four bulbs; DOE says
    200 W each, Argonne says 100 W. EBR-I was a liquid-metal-cooled fast breeder that made more fuel than
    it consumed [NUC-58, NUC-59 · H].
  - `DEMO` First grid connection: the 5 MWe AM-1 at Obninsk, a graphite-moderated water-cooled reactor
    (LWGR). It reached criticality on 6 May 1954 and was connected to the grid on 27 June 1954 [NUC-60,
    NUC-61 · H].
  - `COMM` First commercial-scale station: Calder Hall (four Magnox reactors), grid-connected from August
    1956 and opened by the Queen on 17 October 1956. It was conceived as a dual-purpose plant, with defence
    plutonium the first priority, and the plutonium role ended in the 1960s [NUC-62–NUC-66 · H/M].
  - `COMM` First full-scale US plant devoted to civilian power: Shippingport, a PWR. It went critical on
    2 December 1957, was synchronised with the grid on 18 December and reached full power on 23 December
    1957 [NUC-68, NUC-69, NUC-70 · H].
- `DEMO` **PWR lineage.** The submarine *Nautilus*, with a Westinghouse pressurized-water plant, went
  "underway on nuclear power" on 17 January 1955. The same Naval Reactors organisation led Shippingport
  [NUC-71, NUC-69 · H].
- `DEMO` **BWR lineage.** BORAX-I (1953) tested boiling water as coolant and moderator and was
  deliberately destroyed in a 1954 test. BORAX-III lit the town of Arco on 17 July 1955, and EBWR followed
  in 1956. GE's commercial BWR built on this work [NUC-72, NUC-73 · H]. Yankee Rowe (PWR) and Dresden-1
  (BWR) both started in 1960 [NUC-74, NUC-75 · M].
- `DEMO`/`COMM` **Heavy water.** ZEEP at Chalk River went critical on 5 September 1945, the first reactor
  outside the US [NUC-76 · H]. NPD (1962) was the CANDU prototype, and Douglas Point (1967–68) proved the
  design at scale [NUC-77, NUC-78 · M]. PHWRs hold natural-uranium fuel in pressure tubes inside a
  heavy-water calandria and refuel on power [NUC-79 · H].
- `COMM` **Gas-cooled reactors.** Magnox used natural uranium metal, graphite and CO2. The AGR, prototyped
  at Windscale in 1962, used enriched oxide in stainless steel at about 40% efficiency; 14 AGRs were built
  between 1976 and 1989 [NUC-64, NUC-67 · M].
- `OBS` **RBMK.** A graphite-moderated, boiling-water, channel-type reactor. INSAG-7 names its positive
  void coefficient and the possibility of a positive scram as key shortcomings behind the 1986 Chernobyl
  accident. Later changes reduced the void effect [NUC-82, NUC-83, NUC-84 · H/M].
- `COMM` **Fast reactors.** BN-600 was grid-connected in 1980 and was followed by BN-800. Phénix ran from
  1973 to 2009, and Superphénix (1,200 MWe) closed in 1998 [NUC-85, NUC-86, NUC-87 · M].
- `ADOPT` **Fleet today.** A PRIS snapshot (date uncertain, about 2024–25) shows about 417 units: 308 PWR,
  46 PHWR, 45 BWR, 8 GCR (the UK AGRs), 7 LWGR (Russian RBMKs) and 2 FBR [NUC-88 · M]. IAEA reported 416
  reactors with 376.3 GW(e) on 19 November 2025 [NUC-90 · M; see also NUC-89].
- `INST` **Accidents and regulation.** TMI-2 partly melted down on 28 March 1979. The NRC response
  reformed operator training, emergency planning and inspection, and the industry founded INPO
  [NUC-93 · H]. At Fukushima Daiichi in 2011, the earthquake cut off-site power and the tsunami disabled
  backup power, causing a long station blackout [NUC-94 · H].

### 2.12 Power generation and grid integration

- `ADOPT` Nuclear supplied about 9% of world electricity in 2024, down from about 17% in the mid-1990s
  *(industry)* [NUC-91 · M]. 2024 output was a record 2,667 TWh at an 83% average capacity factor
  *(industry)* [NUC-92 · M].
- `ADOPT` After the 1974 oil-shock decision, France gets about 70% of its electricity from nuclear
  [NUC-95, NUC-96 · M/H].
- `INST` **Grid constraint.** The largest single unit is practically limited to about 10% of system load,
  unless large load shedding after a trip is acceptable. The grid must also hold reserves and be able to
  supply the tripped plant [NUC-221 · H; NUC-222 · M].
- `INST` **Load following.** European utility requirements call for daily cycling between 50% and 100%
  of rated power at 3–5% per minute. In France, load following costs about 1.2% of capability factor
  [NUC-223 · H].
- `INST` **National prerequisites.** The IAEA "Milestones" approach lists 19 infrastructure issues across
  three phases, with human-resource development underpinning the rest [NUC-220 · H].

### 2.13 Spent fuel, reprocessing and waste

- `ADOPT` **Interim storage.** Spent fuel cools in pools, typically for at least five years, and can then
  move to dry casks, which frees pool space [NUC-123, NUC-124 · H].
- `COMM` **Reprocessing.** Every commercial plant uses PUREX: the fuel is dissolved in nitric acid and
  uranium and plutonium are separated by solvent extraction. La Hague has run since 1976, and THORP ran
  from 1994 to November 2018. Recovered plutonium is recycled as MOX fuel [NUC-121 · M]. No civil
  reprocessing operates in the US [NUC-122 · H]. Wartime separation chemistry was first developed at X-10
  for use at Hanford [NUC-56 · H].
- `INST` **Waste classes.** The IAEA's GSG-1 defines exempt, very short lived, very low, low,
  intermediate and high level waste. High level waste maps to deep geological disposal [NUC-125 · M].
- `ADOPT` **Repositories.** WIPP received its first shipment on 26 March 1999. It is about 2,150 feet down
  in salt and takes defence transuranic waste [NUC-126, NUC-127 · H]. For Onkalo, Posiva applied for an
  operating licence on 30 December 2021. In August 2026 the regulator STUK found no safety obstacle, but
  the government decision was still pending in the sources found [NUC-128, NUC-129 · H]. Posiva began
  drilling the first deposition hole in late August 2026, and a planned capacity of 6,500 t in 3,250
  canisters was reported [NUC-130 · L].

### 2.14 Biological hazards and protection standards

- `OBS` **Early injuries.** Clarence Dally, Edison's X-ray assistant, had lesions by 1900, underwent
  amputations and died in 1904 [NUC-133 · H]. Becquerel was burned by a radium tube he carried in a
  pocket, and Pierre Curie deliberately exposed his arm to radium for about ten hours. Their joint paper on
  these effects appeared in 1901 [NUC-134 · M].
- `OBS` **Radium dial painters.** Workers who shaped their brushes with their lips ingested radium. A
  dentist raised the alarm, "radium jaw" was linked to radium by 1923, and New Jersey compensated "radium
  necrosis" [NUC-135, NUC-136 · H/M]. The episode shaped Manhattan Project safety standards [NUC-137 · M].
- `OBS` **Miners.** Agricola recorded high respiratory mortality among Erzgebirge miners. Härting and
  Hesse reported the lung-cancer hazard in 1879, and radiation was suspected from about 1921. Reports
  suggest about half of the miners died of lung cancer, usually ten or more years after first exposure
  [NUC-148, NUC-147 · M]. US compensation (RECA) covers uranium miners, millers and ore transporters who
  worked between 1942 and 1990 [NUC-149 · H].
- `INST` **Standards.** The 1925 radiology congress raised the need for a protection body. The
  International X-ray and Radium Protection Committee was founded in Stockholm in 1928, with Sievert as
  first chair, to protect medical staff [NUC-138, NUC-139 · H]. The first dose-limit recommendation came
  in 1934 [NUC-140 · H]. The body was renamed ICRP in 1950. Publication 26 (1977) introduced justification,
  optimisation and limitation, and limits were cut again in 1990 [NUC-138, NUC-140, NUC-141 · H/M].
  Publication 103 (2007) sets 20 mSv a year for workers, averaged over five years, and 1 mSv a year for the
  public [NUC-142 · H].
- `INST` **Health physics** was named at the Chicago Metallurgical Laboratory in 1942. Its first group
  moved to Oak Ridge with Ernest Wollan [NUC-143, NUC-144 · M].

### 2.15 Radiation detection

- `DEMO` **Electrometers and electroscopes** were the first quantitative instruments [NUC-18, NUC-155 · H/M].
- `DEMO` **Scintillation.** Crookes' spinthariscope (shown on 15 May 1903) displayed individual alpha
  particles as flashes on zinc sulfide [NUC-153 · H]. Geiger and Marsden counted their scattered alphas the
  same way [NUC-27 · H].
- `DEMO` **Cloud chamber.** Wilson's chamber (1911) made particle tracks visible, and won the 1927 Nobel
  Prize [NUC-152 · H].
- `DEMO`/`ADOPT` **Geiger counters.** Geiger's alpha counter (1908–1911) was followed in 1928 by the
  Geiger–Müller tube, which counts many kinds of ionizing radiation but cannot identify their type or
  energy [NUC-150, NUC-151 · H/M].
- `ADOPT` **Dosimetry.** The first true film badge was designed in the early 1930s. Wollan's
  cadmium-filtered badge followed, and pocket ion chambers were worn almost universally in the Manhattan
  Project [NUC-157, NUC-156 · M].
- `ADOPT` **NaI(Tl) scintillators** (Hofstadter, 1948) became the standard gamma detector, and are used in
  airborne survey systems and in the Anger camera [NUC-154, NUC-177, NUC-206 · M/H].

### 2.16 Shielding, containment and safe laboratory practice

- `INST` The protection principles are time, distance, shielding (lead, concrete or water) and
  containment, applied under ALARA. Doubling the distance cuts the exposure rate by a factor of four, and
  glove boxes are an engineering control [NUC-145, NUC-146 · H].
- `DEMO`/`ADOPT` **Hot cells** are shielded, exhausted enclosures worked by tongs or master–slave
  manipulators [NUC-160 · H]. Argonne's remote-handling group, formed in 1947, developed force-reflecting
  master–slave manipulators [NUC-158, NUC-159 · M].

### 2.17 Research institutions and industrial prerequisites

- `INST` The Manhattan Project was an Army programme under Groves, officially 1942–1947 [NUC-213 · H]. It
  cost about $2.2 billion by the war's end, and peak employment was about 129,000 in June 1944, mostly
  construction workers [NUC-218, NUC-219 · H].
- `INST` It relied on industrial firms (DuPont built X-10), materials suppliers (National Carbon graphite)
  and specialised plant inputs (silver-wound calutron magnets, nickel diffusion barriers) [NUC-56, NUC-55,
  NUC-107, NUC-105 · H/M].
- `OBS` **Imported ore.** About 1,200 tons of Congo ore had been shipped to Staten Island in 1940, and
  the Army bought it along with about 3,000 more tons in the Congo [NUC-184, NUC-186 · H/M]. Eldorado
  reopened in 1942 to supply the US, and the Canadian government took control [NUC-187 · M].
- `INST` **Specialised industries the nuclear chain needs:** hafnium-free zirconium [NUC-111, NUC-112],
  elemental fluorine [NUC-113, NUC-114], nuclear-grade graphite [NUC-54], and trained people (X-10 trained
  hundreds of chemists and engineers) [NUC-56].
- `INST` **International institutions.** Eisenhower's "Atoms for Peace" speech (8 December 1953) led to the
  IAEA, whose Statute took effect on 29 July 1957 [NUC-217 · H].

### 2.18 Nuclear medicine and other peaceful uses

- `COMM` **Radium therapy.** Radium was placed first in body cavities and later in implanted needles.
  Staff exposure and leaking needles were early problems [NUC-201 · M]. The Institut du Radium (1909) and
  the Fondation Curie (1920) became treatment and training centres [NUC-202 · M].
- `EXPL` **Tracers.** In 1913 Hevesy realised a radioisotope could mark an element. His first tracer
  studies date from 1923, and he won the 1943 Nobel Prize (awarded in 1944) [NUC-194, NUC-195 · H/M].
- `DEMO` **Radionuclide therapy.** On 31 March 1941 Hertz gave the first radioiodine treatment, using
  isotopes from the MIT cyclotron [NUC-204 · H].
- `COMM` **Reactor isotopes.** On 2 August 1946 Oak Ridge shipped its first reactor-made medical isotope
  (carbon-14). More than 1,000 shipments of 60 isotopes followed in the first year [NUC-196, NUC-197 · H].
- `COMM` **Cobalt-60 teletherapy** began in October–November 1951, in both London, Ontario and Saskatoon.
  Which came first is disputed (§3) [NUC-77, NUC-203 · M/H].
- `ADOPT` **Tc-99m.** Brookhaven's generator (Tucker, Richards and Greene, 1957–58) was first ordered by a
  hospital in 1961 [NUC-198 · H]. Tc-99m now serves about 40 million procedures a year, roughly 80% of
  nuclear medicine. Mo-99 has a 66-hour half-life and Tc-99m a 6-hour one; supply comes from research
  reactors and has been disrupted by their outages [NUC-199, NUC-200 · H]. Anger's scintillation camera
  (Berkeley, 1950s) became the basis of SPECT [NUC-205, NUC-206 · M/H].
- `ADOPT` **Industry.** Radiography uses Ir-192, Co-60 and Se-75 sources [NUC-209 · H]. About 200 cobalt-60
  irradiators sterilise medical devices and irradiate food [NUC-210, NUC-211 · M].
- `EXPL`/`ADOPT` **Radiocarbon dating.** Libby proposed it in 1946 and confirmed it against samples of
  known age in 1949. He won the 1960 Nobel Prize, and the method now reaches about 50,000 years [NUC-207,
  NUC-208 · H].

### 2.19 Natural fission at Oklo (an educational geology and physics fact)

- `OBS` In 1972 French analysts found Oklo (Gabon) uranium with 0.7171% U-235 instead of the usual
  0.7202% [NUC-193 · M]. Natural chain reactions had run there about 1.8 billion years ago; the IAEA gives
  1.8 billion, popular accounts say "2 billion" [NUC-192 · H; NUC-193]. U-235 then made up about 3.7% of
  uranium, groundwater acted as moderator, and about 16 reaction zones are known [NUC-193 · M].
- `EXPL` Oklo is studied as a natural analogue for repositories: the actinides largely stayed in place
  while some fission products migrated [NUC-131, NUC-132 · M].
- *Design reading (inference, not sourced).* Criticality needs only enough fissile fraction plus a
  moderator. Today's 0.72% U-235 is too low for a light-water system without enrichment (consistent with
  NUC-80), which is why Oklo cannot recur today.

### 2.20 Nuclear weapons: the minimum facts for a scope decision

- `DEMO` The first nuclear explosion, a plutonium device, was tested on 16 July 1945 [NUC-212 · H]. A
  uranium weapon was used on Hiroshima on 6 August and a plutonium weapon on Nagasaki on 9 August 1945
  [NUC-213 · H].
- `DEMO` **First tests.** USSR 29 August 1949; UK October 1952; first full-scale thermonuclear test (US)
  1 November 1952; France February 1960; China 16 October 1964 [NUC-214, NUC-215 · H].
- `INST` **NPT.** Opened for signature on 1 July 1968 and in force from 5 March 1970. A nuclear-weapon
  state is one that exploded a device before 1 January 1967. The treaty was extended indefinitely in 1995
  and has 191 parties [NUC-216 · H].
- `EXPL` **Pathways.** Fissile material comes from enrichment (HEU) or from reactors plus reprocessing
  (plutonium). The production reactors (X-10, Hanford, the dual-purpose Calder Hall) are graphite designs
  [NUC-119, NUC-56, NUC-57, NUC-65 · H/M]. Enrichment and reprocessing are the dual-use steps
  [NUC-117, NUC-118 · M].
- `OBS` **Scale.** About $2.2 billion and up to 129,000 workers [NUC-218, NUC-219 · H].

### 2.21 The geology and resource-knowledge path

- `COMM` **Assaying.** Finds at Troy II (about 2600 BC) and Cappadocian tablets imply cupellation in the
  third millennium BC [NUC-224 · M]. In cupellation, lead oxide soaks into a bone-ash cupel at about 800 °C
  and leaves gold and silver behind, making it an ancient quantitative analysis [NUC-224, NUC-225 · M].
  The touchstone compares a metal's streak against standards, with nitric acid, and is still used [NUC-226
  · H]. Pliny, citing Theophrastus, describes the "Lydian" stone [NUC-227 · M].
- `EXPL` **Early mining treatises.** Agricola was town physician at Joachimsthal from 1527 to 1533. He
  wrote *Bermannus* (1530) and *De natura fossilium*, which classified minerals, and favoured observation
  over authority [NUC-229 · H]. *De re metallica*, covering ore-body geology, mining and smelting, appeared
  posthumously in Basel in 1556; the Hoover translation followed in 1912 [NUC-228, NUC-229 · H]. His move
  to Chemnitz is dated 1533 or 1536 [NUC-230 · M].
- `EXPL` **Mineral analysis.** Cronstedt developed blowpipe analysis of ores, and Berzelius and Hausmann
  applied it to qualitative analysis [NUC-231, NUC-232 · H/M]. Bunsen and Kirchhoff's spectral analysis
  (about 1859) found caesium in 1860 and rubidium in 1860–61 [NUC-233, NUC-234 · H/M]. With the Nicol
  prism, Sorby showed in 1849 that minerals in thin section can be identified optically [NUC-235 · H].
- `EXPL` **Stratigraphy and mapping.** Steno's *Prodromus* (1669) set out superposition and original
  horizontality [NUC-236, NUC-237 · H/M]. William Smith's 1815 map, the first geological map of a whole
  country, identified strata by their fossils and their regular order [NUC-238, NUC-239 · H].
- `INST` **Geological surveys.** The Geological Survey of Great Britain was established in 1835, after De
  la Beche's private mapping from 1832, and is the oldest national survey [NUC-240, NUC-241 · H]. The USGS
  was created on 3 March 1879 to examine "the geological structure, mineral resources, and products of the
  national domain". Its first director emphasised mining geology and mineral statistics [NUC-242,
  NUC-243 · H].
- `COMM` **Geophysics.** Locating iron ore by magnetic compass in Sweden is traditionally dated to 1640
  [NUC-248 · L]. Schmidt's field magnetometer followed in 1915 [NUC-248], and the WWII fluxgate
  magnetometer opened airborne magnetic surveying [NUC-249 · M]. Gravity: Eötvös built his torsion balance
  in 1888; the first oil survey was at Egbell in 1915, and the Nash salt dome was found in 1924 [NUC-246 ·
  H]. The balance contributed to 79 Gulf Coast field discoveries [NUC-247 · M]. The first reflection-seismic
  field test was made in Oklahoma on 4 June 1921 [NUC-244, NUC-245 · M].

---

## 3. Disputed points and claims with more than one origin

| # | Claim | What the sources say | How to treat it |
|---|---|---|---|
| 1 | First nuclear power plant | EBR-I gave the first usable electricity (1951) [NUC-58]; Obninsk the first grid connection (1954) [NUC-60, NUC-61]; Calder Hall the first commercial-scale station, on the grid from August 1956 [NUC-62, NUC-64]; Shippingport the first full-scale US plant solely for civilian power (1957) [NUC-68] | Always name the criterion. All four are true under their own definitions. |
| 2 | Discovery of fission | Hahn and Strassmann did the chemistry (December 1938) [NUC-40]; Meitner and Frisch gave the physical interpretation (1939) [NUC-41]; Noddack's 1934 suggestion was ignored [NUC-40]. The 1944 Nobel Prize went to Hahn alone and remains controversial [NUC-43] | Credit all four; the prize is not a ruling on credit. |
| 3 | Chain-reaction concept | Szilard (1933, patented 1934) predates fission, and parts of his physics were wrong [NUC-46, NUC-47]. Secondary neutrons were measured in 1939 [NUC-45, NUC-41] | Szilard had the idea; feasibility was established in 1939; CP-1 demonstrated it in 1942. |
| 4 | Gamma rays | Villard observed them in 1900; Rutherford named them later (1903 in other extracts) [NUC-22] | Observation by Villard; naming and classification by Rutherford. |
| 5 | Electron | Thomson's 1897 measurement is standard, but Kaufmann measured e/m the same year; the 1906 prize citation does not mention the electron [NUC-26] | Keep 1897 and Thomson, noting the parallel work. |
| 6 | Technetium | Perrier and Segrè (1937) hold the accepted credit [NUC-38]; a reanalysis argues the Noddacks' 1925 "masurium" was natural Tc [NUC-39], and RSC calls that debate laid to rest | Use 1937; note the dispute. |
| 7 | Curies' raw material | APS: about one ton from the Austrian government [NUC-17]; Hexagon: about ten tonnes [NUC-04] | Probably different stages of the work. Do not cite a single figure. |
| 8 | Obninsk date | PRIS and the IAEA country profile: 27 June 1954 [NUC-60, NUC-61]; an older IAEA item: 26 June | Use 27 June. |
| 9 | Hanford B criticality | DOE: 26 September 1944; OSTI history: 27 September [NUC-57] | Either; the sim needs only the year. |
| 10 | ZEEP | 5 September 1945 (NRC Canada) vs 6 September (other Canadian sources) [NUC-76] | Either. |
| 11 | EBR-I bulbs | 200 W (DOE, INL) vs 100 W (Argonne) [NUC-58, NUC-59] | Not material. |
| 12 | First cobalt-60 teletherapy | CNA: London, Ontario first, Saskatoon 11 days later [NUC-77]; Saskatoon claims the first accurately calibrated treatment [NUC-203] | Joint 1951 Canadian development. |
| 13 | Oklo age | about 1.8 Ga (IAEA) vs "2 billion years" (popular) [NUC-192, NUC-193] | Use about 1.8–2 Ga. |
| 14 | First dose limit | ICRP's history places the first recommendation at the 1934 meeting [NUC-140]; a review gives 0.2 R/day and a 1924 proposal; an NCRP summary gives 0.1 R/day [NUC-141] | Use 1934 without a number unless one is ruled on. |
| 15 | Cigar Lake start | Cameco's filings give commercial production in 2015 [NUC-190]; the date of the World Nuclear News "enters operation" report was not visible [NUC-189] | Use 2015 (commercial). An earlier first-ore date is unsourced here. |
| 16 | Shinkolobwe stockpile | 1,200 short tons vs about 1,250 metric tons, and ore grade from more than 2% average to about 65% U3O8 in hand-sorted ore [NUC-184, NUC-185] | Units and sorting explain the gap. |
| 17 | Steno's laws | Sources credit him with two (superposition, horizontality) or with four [NUC-236, NUC-237] | Credit Steno for superposition and horizontality. |
| 18 | Swedish magnetic prospecting, 1640 | One old USGS abstract, repeating a tradition [NUC-248] | Low confidence. Do not use it as a node date. |
| 19 | Rubidium date | 1860 (Science History Institute) vs 1861 (RSC, APS) [NUC-233, NUC-234] | Use 1860–61. |
| 20 | Dally as "first American radiation death" | Hedged in the sources [NUC-133] | Say "an early fatality". |
| 21 | Fiesta red withdrawal | The government took the uranium in WWII (museum page) vs ended by the 1946 Atomic Energy Act (ORAU blog) [NUC-08] | Both point to wartime and postwar federal control. |

---

## 4. Draft node chain (PROPOSED, for later integration)

### 4.1 Design rules applied

1. **Prerequisites are causal.** A prerequisite is listed only when the knowledge in it is used by the
   node, not because it came earlier in history.
2. **Research makes things eligible and creates nothing.** Every item below is "class C, realization"
   (in the language of the capability-ownership matrix): the node makes something eligible, and an owning
   system builds it. Owning systems named here are indicative.
3. **Deposits exist regardless of research.** World generation places uranium (and thorium) deposits by
   IAEA type, with grade and tonnage drawn from type models [NUC-162, NUC-250]. Their radioactivity and
   radon hazard act on miners whether or not anything has been researched [NUC-148].
4. **Locate, identify, prove, extract.** No node reveals deposits. Nodes make survey, analysis, drilling and
   extraction activities eligible. Those activities act only on territory the polity has located or can
   reach, and give probabilistic, type-dependent results (§4.7).
5. **Geology is its own path.** It is linked to the nuclear chain at radiometric prospecting, ore
   processing and geological disposal.
6. **Electricity, materials and labour are plant inputs, not prerequisites** (§1, item 7).

### 4.2 Summary table

Status: **NEW** = proposed new node; **CHG** = existing node with changed prerequisites or unlocks;
**DEF** = deferred, outside this milestone's proposal.

| # | id | Name | Age | Subtree | Prerequisites | Status |
|---|---|---|---|---|---|---|
| C01 | `assaying` | Assaying (touchstone and fire assay) | A3 | trunk | `copper_smelting AND hacksilver` | NEW |
| C02 | `mineralogy` | Systematic mineralogy and blowpipe analysis | A7 | natural_science | `assaying AND mining_shaft AND distillation` | NEW |
| C03 | `stratigraphy` | Stratigraphy and geological mapping | A8 | natural_science | `mineralogy AND surveying` | NEW |
| C04 | `geological_survey` | National geological survey | A8 | natural_science | `stratigraphy AND printing_press` | NEW |
| C05 | `geophysical_prospecting` | Geophysical prospecting (magnetic, gravity, seismic) | A9 | natural_science | `geological_survey AND electromagnetism` | NEW |
| C06 | `radioactivity` | Radioactivity and radiochemistry | A9 | natural_science | `photography AND electromagnetism` | NEW |
| C07 | `radiation_detection` | Radiation detection and counting | A9 | natural_science | `radioactivity AND vacuum_tube` | NEW |
| C08 | `radiometric_prospecting` | Radiometric prospecting | A9 | natural_science | `radiation_detection AND geological_survey` | NEW |
| C09 | `radiation_protection` | Radiation protection and health physics | A9 | medicine | `radiation_detection AND xray` | NEW |
| C10 | `radiotherapy` | Radiotherapy (radium and teletherapy) | A9 | medicine | `radioactivity AND xray` | NEW |
| C11 | `nuclear_physics` | Nuclear physics (nucleus, isotopes, neutron) | A9 | natural_science | `radiation_detection AND quantum_mechanics` | NEW |
| C12 | `nuclear_fission` | Controlled nuclear fission (critical pile) | A9 | natural_science | `nuclear_physics` *(was `quantum_mechanics AND electricity_generation`)* | CHG |
| C13 | `uranium_milling` | Radioactive ore processing (radium and uranium milling) | A9 | engineering | `radioactivity AND sulphuric_acid AND mining_shaft` | NEW |
| C14 | `uranium_enrichment` | Uranium conversion and isotope enrichment | A9 | engineering | `nuclear_fission AND uranium_milling AND milling_machine` | NEW |
| C15 | `centrifuge_enrichment` | Gas-centrifuge enrichment *(optional)* | A9 | engineering | `uranium_enrichment` (plus a materials node, open question Q6) | NEW |
| C16 | `fuel_fabrication` | Ceramic oxide fuel and zirconium cladding | A9 | engineering | `nuclear_fission AND uranium_milling AND aluminium` | NEW |
| C17 | `graphite_reactor` | Graphite-moderated reactors (production, Magnox/AGR, RBMK-type) | A9 | engineering | `nuclear_fission AND aluminium` | NEW |
| C18 | `heavy_water_reactor` | Heavy-water reactors (PHWR/CANDU) | A9 | engineering | `fuel_fabrication AND steam_high_pressure` | NEW |
| C19 | `light_water_reactor` | Light-water reactors (PWR and BWR) | A9 | engineering | `fuel_fabrication AND uranium_enrichment AND steam_high_pressure` | NEW |
| C20 | `reprocessing` | Spent-fuel reprocessing (PUREX) | A9 | engineering | `nuclear_fission AND radiation_protection` | NEW |
| C21 | `fast_breeder_reactor` | Fast neutron (breeder) reactors | A9 | engineering | `reprocessing AND fuel_fabrication` | NEW |
| C22 | `nuclear_power` | Nuclear power station | A9 | engineering | `(graphite_reactor OR heavy_water_reactor OR light_water_reactor OR fast_breeder_reactor) AND steam_turbine AND electricity_generation` *(was `nuclear_fission AND steam_turbine`)* | CHG |
| C23 | `geological_disposal` | Deep geological disposal of radioactive waste | A9 | engineering | `geological_survey AND radiation_protection AND nuclear_fission` | NEW |
| C24 | `radioisotope_production` | Radioisotope production and applications | A9 | natural_science | `nuclear_physics AND radiation_protection` | NEW |
| C25 | `nuclear_medicine` | Nuclear medicine (tracers, imaging, radionuclide therapy) | A9 | medicine | `radioisotope_production AND radiation_detection` | NEW |
| C26 | `nuclear_submarine` | Nuclear submarine | A9 | military | `light_water_reactor AND screw_propeller` *(was `nuclear_fission AND screw_propeller`)* | CHG |
| C27 | `thermonuclear` | Thermonuclear weapon | A9 | military | `fission_weapon` *(was `nuclear_fission`)*, so it stays locked while weapons are deferred | CHG (DEF) |
| D01 | `fission_weapon` | Fission weapon | A9 | military | when admitted: `nuclear_fission AND (uranium_enrichment OR (graphite_reactor AND reprocessing))` | DEF |

**Chain size:** 27 nodes, made of 23 new nodes (one optional), 2 changed core nodes and 2 re-pointed
military nodes, plus one deferred weapons node that is not part of the proposal. Every prerequisite id
either exists in `Sim.Data/content/research.json` (`copper_smelting`, `hacksilver`, `mining_shaft`,
`distillation`, `surveying`, `printing_press`, `electromagnetism`, `photography`, `vacuum_tube`, `xray`,
`quantum_mechanics`, `sulphuric_acid`, `milling_machine`, `aluminium`, `steam_high_pressure`,
`steam_turbine`, `electricity_generation`, `screw_propeller`) or is defined in this table. The graph has
no cycles.

### 4.3 Dependency sketch

```
GEOLOGY PATH
 copper_smelting + hacksilver --> C01 assaying
 C01 + mining_shaft + distillation --> C02 mineralogy
 C02 + surveying --> C03 stratigraphy --(+printing_press)--> C04 geological_survey
 C04 + electromagnetism --> C05 geophysical_prospecting
 C04 + C07 --> C08 radiometric_prospecting

SCIENCE AND PROTECTION
 photography + electromagnetism --> C06 radioactivity --(+vacuum_tube)--> C07 radiation_detection
 C07 + quantum_mechanics --> C11 nuclear_physics --> C12 nuclear_fission
 C07 + xray --> C09 radiation_protection        C06 + xray --> C10 radiotherapy

FUEL CYCLE AND REACTORS
 C06 + sulphuric_acid + mining_shaft --> C13 uranium_milling
 C12 + C13 + milling_machine --> C14 uranium_enrichment --> C15 centrifuge_enrichment (optional)
 C12 + C13 + aluminium --> C16 fuel_fabrication
 C12 + aluminium --> C17 graphite_reactor
 C16 + steam_high_pressure --> C18 heavy_water_reactor
 C16 + C14 + steam_high_pressure --> C19 light_water_reactor
 C12 + C09 --> C20 reprocessing --(+C16)--> C21 fast_breeder_reactor
 (C17 | C18 | C19 | C21) + steam_turbine + electricity_generation --> C22 nuclear_power
 C04 + C09 + C12 --> C23 geological_disposal

ISOTOPES AND MEDICINE
 C11 + C09 --> C24 radioisotope_production --(+C07)--> C25 nuclear_medicine

MILITARY (repointed or deferred)
 C19 + screw_propeller --> C26 nuclear_submarine
 [DEFERRED] C12 + (C14 | (C17 + C20)) --> D01 fission_weapon --> C27 thermonuclear
```

### 4.4 Node details

The notes below use the word "eligibility" in the sense of the capability-ownership matrix: the node is
the research half, and an owning system realizes the result.

**C01 `assaying`: Assaying (touchstone and fire assay)** (A3, trunk, NEW)
- *Prerequisites.* `copper_smelting AND hacksilver`. Fire assay is a furnace operation using lead, and
  both assay methods compare samples against weighed standards of known purity (`hacksilver` brings
  standard weights).
- *Represents.* Measuring how much precious metal an ore or alloy contains.
- *Eligibility, and who realizes it.* The activity "assay a sample". The resource layer uses it to reveal
  the grade of a deposit the polity has already located; the economy can use it for assay-backed metal
  quality.
- *Inputs.* Lead, bone ash and fuel; for the touchstone, a suitable dark siliceous stone [NUC-226].
- *Why a separate node.* It splits *grading* (how rich) from *finding* and from *extracting*, and every
  metal chain can reuse it.
- *Key facts.* NUC-224, NUC-225, NUC-226, NUC-227.

**C02 `mineralogy`: Systematic mineralogy and blowpipe analysis** (A7, natural_science, NEW)
- *Prerequisites.* `assaying AND mining_shaft AND distillation`. It needs assay chemistry, field contact
  with ores, and mineral acids (the touchstone and wet tests use nitric acid [NUC-226]).
- *Represents.* Classifying minerals and identifying their chemical makeup (Agricola's classification
  [NUC-229]; blowpipe analysis [NUC-231, NUC-232]; optical identification in thin section [NUC-235]). It
  includes the analytical chemistry by which uranium was recognised in pitchblende in 1789 [NUC-01].
- *Eligibility, and who realizes it.* The activity "identify a mineral occurrence". The resource layer
  converts a located but unknown occurrence (for example, "black heavy mineral in a silver vein") into a
  typed one ("uraninite or pitchblende"). Uranium remained an unrecognised waste mineral for at least
  two centuries before this kind of analysis existed [NUC-04].
- *Inputs.* Reagents, glassware and blowpipes.
- *Why a separate node.* Identification is a different step from grading and from regional mapping.
- *Key facts.* NUC-01, NUC-04, NUC-228–NUC-235.

**C03 `stratigraphy`: Stratigraphy and geological mapping** (A8, natural_science, NEW)
- *Prerequisites.* `mineralogy AND surveying`. Mapping needs both the ability to recognise rock units and
  a surveyed base map.
- *Represents.* Superposition, the identification of strata by their fossils and order, and the
  geological map [NUC-236, NUC-238].
- *Eligibility, and who realizes it.* The activity "map accessed territory". The resource layer turns a
  mapped region into a *prospectivity* field: the likelihood of each deposit type, given the host
  formations. For example, sandstone-hosted ore follows its host units [NUC-165]. It never yields a list
  of deposits.
- *Why a separate node.* It moves knowledge from single known mines to spatial prediction.
- *Key facts.* NUC-236–NUC-239, NUC-165.

**C04 `geological_survey`: National geological survey** (A8, natural_science, NEW)
- *Prerequisites.* `stratigraphy AND printing_press`, because systematic maps have to be reproduced and
  distributed.
- *Represents.* A state institution for systematic mapping and mineral statistics [NUC-240, NUC-242,
  NUC-243].
- *Eligibility, and who realizes it.* An institution, realized by the institutions system. It raises the
  rate and coverage of mapping across the polity's own territory, and is the host for regional
  radiometric and geophysical programmes (USGS for the AEC [NUC-175, NUC-176]).
- *Why a separate node.* The institution, not individual science, is what scales knowledge of
  prospectivity to a whole country. If the institutions system can gate directly on `stratigraphy`, this
  node can be folded into it (open question Q4).
- *Key facts.* NUC-240–NUC-243, NUC-175, NUC-176.

**C05 `geophysical_prospecting`: Geophysical prospecting** (A9, natural_science, NEW)
- *Prerequisites.* `geological_survey AND electromagnetism`. Magnetometers and electrical instruments need
  electromagnetism, and interpreting the readings needs geological maps.
- *Represents.* Magnetic, gravity and seismic surveys [NUC-246, NUC-244, NUC-249].
- *Eligibility, and who realizes it.* Survey activities that can detect *concealed* bodies at depth in
  surveyed, accessed areas. These are the route to "blind" deposits. The rich unconformity ores sit at the
  sandstone–basement contact [NUC-163, NUC-164], and Cigar Lake was found by surface exploration drilling
  [NUC-190].
- *Why a separate node.* The physics and the detection reach differ from radiometrics. Gravity and seismic
  methods were developed for oil, and magnetics for iron [NUC-246, NUC-248].
- *Key facts.* NUC-244–NUC-249. The specific role of electrical and electromagnetic surveys in the
  Athabasca discoveries was not sourced (Q1).

**C06 `radioactivity`: Radioactivity and radiochemistry** (A9, natural_science, NEW)
- *Prerequisites.* `photography AND electromagnetism`. Becquerel used photographic plates and the Curies
  used electrometry; radiochemistry comes in through photography's own prerequisite,
  `chemistry_quantitative` [NUC-11, NUC-18].
- *Represents.* The knowledge from 1896 to about 1910: radioactivity, polonium and radium, the
  alpha/beta/gamma classification and transmutation [NUC-11–NUC-22].
- *Eligibility, and who realizes it.* Mostly knowledge. It feeds radium recovery (via C13), radiotherapy
  (C10), detection (C07) and the health system's understanding of exposure.
- *Why a separate node.* The radium economy (1898 to the 1930s) and early radiation medicine came four
  decades before fission [NUC-10, NUC-04]. Merging this node with fission would erase that era.
- *Key facts.* NUC-11–NUC-22.

**C07 `radiation_detection`: Radiation detection and counting** (A9, natural_science, NEW)
- *Prerequisites.* `radioactivity AND vacuum_tube`. Geiger–Müller tubes, electronic counting and
  photomultiplier-based scintillation counters all use vacuum-tube technology [NUC-150, NUC-154].
- *Represents.* Electroscopes, the spinthariscope, the cloud chamber, Geiger–Müller counters, film badges
  and NaI(Tl) scintillators [NUC-150–NUC-157].
- *Eligibility, and who realizes it.* Instrument goods, made by industry and used by four downstream
  systems: prospecting, protection, reactor instrumentation and medicine.
- *Why a separate node.* It is the shared enabler (§1, item 9). Putting it inside any single consumer
  would hide the others' dependence on it.
- *Key facts.* NUC-150–NUC-157, NUC-18.

**C08 `radiometric_prospecting`: Radiometric prospecting** (A9, natural_science, NEW)
- *Prerequisites.* `radiation_detection AND geological_survey`.
- *Represents.* Ground counters, car-borne surveys, airborne gamma surveys and gamma-ray spectrometry
  [NUC-175–NUC-178].
- *Eligibility, and who realizes it.* A survey activity on accessed tiles. It reports *anomalies* (U, Th,
  K), which then need follow-up to become deposits. The airborne variant also needs aircraft (an equipment
  input, not a prerequisite). Results are probabilistic: the AEC had judged Mi Vida's ground barren
  [NUC-182], and only about 1 in 1,000 projects finds an economic deposit [NUC-180].
- *Why a separate node.* This is the method specific to uranium. It depends on detection physics, not on
  general geophysics.
- *Key facts.* NUC-175–NUC-179, NUC-182, NUC-183, NUC-180.

**C09 `radiation_protection`: Radiation protection and health physics** (A9, medicine, NEW)
- *Prerequisites.* `radiation_detection AND xray`. The protection movement grew out of radiology
  (IXRPC was founded at a radiology congress to protect medical staff [NUC-138]) and depends on
  measurement.
- *Represents.* Dose limits, dosimetry, time/distance/shielding/containment, ALARA, hot cells and remote
  handling [NUC-138–NUC-146, NUC-158–NUC-160].
- *Eligibility, and who realizes it.* Protective regimes, realized by the health and regulatory systems
  and by facility design. Exposure keeps causing harm without this node; the node gives the means to
  reduce it (mine ventilation, monitoring, shielded facilities). Hot cells are a prerequisite for
  reprocessing and isotope work.
- *Why a separate node.* The hazard exists without the knowledge (dial painters, miners) [NUC-135,
  NUC-148]. Keeping protection separate lets the simulation model harm before protection arrives.
- *Key facts.* NUC-133–NUC-149, NUC-158–NUC-160.

**C10 `radiotherapy`: Radiotherapy (radium and teletherapy)** (A9, medicine, NEW)
- *Prerequisites.* `radioactivity AND xray`. It needs clinical radiology plus knowledge of radioactive
  sources.
- *Represents.* Radium brachytherapy from about the 1900s, radium institutes, and later cobalt-60
  teletherapy [NUC-201–NUC-203].
- *Eligibility, and who realizes it.* A cancer-treatment option in the health system. Its source supply
  comes first from radium (C13) and later from reactor isotopes (C24).
- *Why a separate node.* It was the first large beneficial use of radioactivity, it drove radium demand,
  and it is distinct from imaging (`xray`).
- *Key facts.* NUC-201–NUC-203, NUC-77.

**C11 `nuclear_physics`: Nuclear physics** (A9, natural_science, NEW)
- *Prerequisites.* `radiation_detection AND quantum_mechanics`. Counters and scintillation screens
  produced the evidence (Geiger–Marsden used scintillation counting [NUC-27]); quantum theory of the atom
  is used to interpret nuclear structure and energetics. This node is the place where `quantum_mechanics`
  now enters the nuclear chain.
- *Represents.* The nucleus, isotopes, the mass spectrograph, deuterium, the neutron, artificial
  radioactivity, slow neutrons and the cyclotron [NUC-27–NUC-38].
- *Eligibility, and who realizes it.* Knowledge, plus accelerators (cyclotrons) as research equipment.
- *Why a separate node.* Isotope and neutron science is the common root of enrichment, heavy water,
  moderation and isotope production.
- *Key facts.* NUC-27–NUC-38.

**C12 `nuclear_fission` (existing, key 321): Controlled nuclear fission** (A9, natural_science, CHG)
- *Prerequisites.* Change `quantum_mechanics AND electricity_generation` to `nuclear_physics`. CP-1 was a
  roughly half-watt experiment [NUC-53], and the electricity demand of isotope plants is an operating input
  (§1, item 7).
- *Represents.* Fission and secondary neutrons (1938–39), the role of U-235, the chain-reaction concept,
  moderation and material purity, and CP-1's controlled criticality [NUC-40–NUC-55].
- *Eligibility, and who realizes it.* A research pile (zero-power critical assembly). Inputs: refined
  natural uranium, nuclear-grade graphite or heavy water, and neutron absorbers. **Remove the unlock
  "nuclear weapon (D-011 deterrence)"** and move it to D01 (§4.6). Keep "reactor" as research reactor only;
  power reactors move to C17–C19 and C21.
- *Why a separate node.* It is the knowledge that a controlled chain reaction is possible and how to build
  one.
- *Key facts.* NUC-40–NUC-55.

**C13 `uranium_milling`: Radioactive ore processing (radium and uranium milling)** (A9, engineering, NEW)
- *Prerequisites.* `radioactivity AND sulphuric_acid AND mining_shaft`. Radium recovery needs knowledge
  of radium, and both radium and yellowcake processing use acid hydrometallurgy, mining and radiometric
  assay of ore [NUC-155].
- *Represents.* Radium recovery (the first product) and uranium milling to yellowcake. Also the ISL
  variant for suitable sandstone deposits [NUC-100, NUC-101, NUC-97].
- *Eligibility, and who realizes it.* Production: a mill or ISL wellfield on a *located and accessed*
  deposit, producing U3O8 (and historically radium), with a tailings waste stock.
- *Inputs.* Ore from an accessed deposit, acid and reagents, water and labour.
- *Why a separate node.* It separates extraction from discovery. Its yield depends on grade and tonnage
  (proven through C01 and C02 and drilling).
- *Key facts.* NUC-97–NUC-101, NUC-04, NUC-10, NUC-187.

**C14 `uranium_enrichment`: Uranium conversion and isotope enrichment** (A9, engineering, NEW)
- *Prerequisites.* `nuclear_fission AND uranium_milling AND milling_machine`. It needs knowledge that U-235
  is the fissile isotope [NUC-44], the uranium chemistry for UF6 conversion (fluorine chemistry is
  bundled in this node [NUC-113, NUC-102]), and precision-made diffusion barriers [NUC-105].
- *Represents.* Conversion to UF6, then electromagnetic separation and gaseous diffusion [NUC-102–NUC-108].
- *Eligibility, and who realizes it.* Conversion and enrichment plants. Electricity per SWU is a recipe
  input (about 2,500 kWh/SWU for diffusion) [NUC-81]. The plant should be flagged as proliferation-sensitive
  for later diplomacy and safeguards systems [NUC-117].
- *Why a separate node.* Light-water reactors and AGRs need it; Magnox and CANDU do not (§1, item 5). It is
  also dual-use.
- *Key facts.* NUC-102–NUC-108, NUC-113–NUC-120.

**C15 `centrifuge_enrichment`: Gas-centrifuge enrichment** (A9, engineering, NEW, *optional*)
- *Prerequisites.* `uranium_enrichment`, plus a high-strength rotor-materials node still to be chosen
  (Q6). History attributes the centrifuge's delay to "engineering and material difficulties" [NUC-81].
- *Eligibility, and who realizes it.* A different enrichment plant recipe, at about 50 kWh/SWU instead of
  about 2,500 [NUC-81, NUC-104]. This is a mechanism-level change, not a modifier. It also has a smaller
  footprint, which affects detectability [NUC-118].
- *Why a separate node.* Its input profile and proliferation profile are both different. If the Director
  prefers fewer nodes, make it a plant variant unlocked by `uranium_enrichment` together with the materials
  node.

**C16 `fuel_fabrication`: Ceramic oxide fuel and zirconium-alloy cladding** (A9, engineering, NEW)
- *Prerequisites.* `nuclear_fission AND uranium_milling AND aluminium`. `aluminium` stands in for
  reactive-metal electrometallurgy (magnesium reductant, chlorine) behind hafnium-free zirconium
  [NUC-111, NUC-112] (Q5).
- *Represents.* UO2 pellet pressing and sintering, zirconium cladding, assemblies and criticality-safe
  handling [NUC-109, NUC-110].
- *Eligibility, and who realizes it.* A fuel fabrication plant and a zirconium sponge plant (inputs:
  zircon, magnesium, chlorine, electricity).
- *Why a separate node.* PHWRs, LWRs and fast reactors all need it. Zirconium demand is almost entirely
  nuclear [NUC-112].

**C17 `graphite_reactor`: Graphite-moderated reactors** (A9, engineering, NEW)
- *Prerequisites.* `nuclear_fission AND aluminium`. Natural-uranium lattices on graphite; fuel and cooling
  channels in light-metal tubes (Hanford B's 2,004 aluminium tubes [NUC-57]).
- *Represents.* Production piles (X-10, Hanford) and power variants: Magnox (natural U metal, CO2), AGR
  (needs C14) and the RBMK-type graphite/boiling-water design [NUC-56, NUC-57, NUC-64, NUC-82].
- *Eligibility, and who realizes it.* Reactor buildings. A production variant makes plutonium (separating
  it requires C20) and isotopes. Power variants become stations through C22. Each variant carries a safety
  parameter; the RBMK's positive void coefficient is the sourced example [NUC-82].
- *Why a separate node.* It is the route to sustained reactor operation without enrichment, and it is
  historically dual-use (Calder Hall [NUC-65]).

**C18 `heavy_water_reactor`: Heavy-water reactors (PHWR/CANDU)** (A9, engineering, NEW)
- *Prerequisites.* `fuel_fabrication AND steam_high_pressure`. Zirconium-clad bundles and pressure tubes
  [NUC-79]. Heavy-water production draws on deuterium knowledge, which comes in via C11 through C12.
- *Represents.* Natural-uranium power reactors, heavy-water moderated, refuelled on power [NUC-79, NUC-76,
  NUC-77].
- *Eligibility, and who realizes it.* A reactor building plus a heavy-water production plant (an
  isotope-separation industry of its own; history not sourced, Q3).
- *Why a separate node.* A different strategic route: power without enrichment, but needing a
  heavy-water industry.

**C19 `light_water_reactor`: Light-water reactors (PWR and BWR)** (A9, engineering, NEW)
- *Prerequisites.* `fuel_fabrication AND uranium_enrichment AND steam_high_pressure`. Enriched oxide fuel
  [NUC-80, NUC-119] in high-pressure vessels.
- *Represents.* The dominant family: PWR (naval origin, Shippingport) and BWR (BORAX, Dresden)
  [NUC-68–NUC-75, NUC-88].
- *Eligibility, and who realizes it.* PWR and BWR reactor variants. The naval PWR also feeds C26.
- *Why one node, not two.* PWR and BWR share the fuel cycle, the moderator and coolant, and the materials.
  Their main difference (the BWR boils its coolant in the core [NUC-72]; the PWR keeps it under pressure)
  makes them variants of one building, not separate knowledge.

**C20 `reprocessing`: Spent-fuel reprocessing (PUREX)** (A9, engineering, NEW)
- *Prerequisites.* `nuclear_fission AND radiation_protection`. Handling irradiated fuel requires hot
  cells and remote manipulators [NUC-160, NUC-158].
- *Represents.* Recovering uranium and plutonium by solvent extraction; MOX [NUC-121, NUC-122].
- *Eligibility, and who realizes it.* A reprocessing plant, flagged as proliferation-sensitive [NUC-117].
  It also produces high-level waste streams.
- *Why a separate node.* It is the most proliferation-relevant back-end step, it is optional (the US does
  none [NUC-122]), and fast reactors depend on it.

**C21 `fast_breeder_reactor`: Fast neutron (breeder) reactors** (A9, engineering, NEW)
- *Prerequisites.* `reprocessing AND fuel_fabrication`. Breeding is useful only if the bred fuel is
  recovered and refabricated.
- *Represents.* Sodium-cooled fast reactors: EBR-I, BN-600 and BN-800, Phénix and Superphénix [NUC-58,
  NUC-59, NUC-85–NUC-87].
- *Eligibility, and who realizes it.* A reactor variant with a closed fuel-cycle recipe. A breeder makes
  more fissile material than it uses [NUC-59].
- *Why a separate node.* It has a different neutron spectrum, coolant and fuel cycle.

**C22 `nuclear_power` (existing, key 322): Nuclear power station** (A9, engineering, CHG)
- *Prerequisites.* `(graphite_reactor OR heavy_water_reactor OR light_water_reactor OR fast_breeder_reactor)
  AND steam_turbine AND electricity_generation`. A station needs a power-capable reactor family, a turbine
  and grid know-how.
- *Eligibility, and who realizes it.* A power station built by the energy and infrastructure system. Grid
  rule: unit size no more than about 10% of grid load [NUC-221]. Load-following envelope: 50–100%, 3–5% per
  minute [NUC-223]. Refuelling outages and reserve needs apply [NUC-221].
- *Key facts.* NUC-58–NUC-75, NUC-88–NUC-96, NUC-220–NUC-223.

**C23 `geological_disposal`: Deep geological disposal** (A9, engineering, NEW)
- *Prerequisites.* `geological_survey AND radiation_protection AND nuclear_fission`. It needs site
  geology, protection, and knowledge of how waste decays and gives off heat.
- *Represents.* The GSG-1 waste classes, repositories (WIPP, Onkalo) and natural analogues (Oklo)
  [NUC-125–NUC-132].
- *Eligibility, and who realizes it.* A repository project. Realization needs a suitable host formation
  (WIPP uses salt [NUC-126]) and a long licensing process: Onkalo applied in 2021 and was still awaiting
  its licence in 2026 [NUC-129, NUC-128]. Until then, spent fuel stays in pools or casks [NUC-123].
- *Why a separate node.* It is the only end state for high-level waste, and it is the deliberate link back
  to the geology path.

**C24 `radioisotope_production`: Radioisotope production and applications** (A9, natural_science, NEW)
- *Prerequisites.* `nuclear_physics AND radiation_protection`. Cyclotrons can make isotopes on a small
  scale, and reactors (C12 onward) make the high-volume facility type eligible [NUC-36, NUC-196, NUC-160].
- *Represents.* Making isotopes: C-14, Co-60, Ir-192, I-131, Mo-99. Also tracer methods, radiography
  sources and sterilisation [NUC-194–NUC-197, NUC-209–NUC-211].
- *Eligibility, and who realizes it.* Isotope products by facility type. The volume a polity can make
  depends on the facility, which is a mechanism and not a modifier.
- *Why a separate node.* It is the main peaceful spillover and needs no power programme; a research reactor
  is enough.

**C25 `nuclear_medicine`: Nuclear medicine** (A9, medicine, NEW)
- *Prerequisites.* `radioisotope_production AND radiation_detection`.
- *Represents.* Radioiodine therapy (1941), Tc-99m diagnostics and Anger-camera or SPECT imaging
  [NUC-204, NUC-198–NUC-200, NUC-205, NUC-206].
- *Eligibility, and who realizes it.* Health-system procedures that consume short-lived isotopes. Supply
  decays with half-life (Mo-99 66 h, Tc-99m 6 h) [NUC-199].
- *Why a separate node.* The clinical capability is different from producing the isotopes.

**C26 `nuclear_submarine` (existing, key 402)** (CHG). Re-point to `light_water_reactor AND
screw_propeller`. *Nautilus* used a pressurized-water plant [NUC-71].

**C27 `thermonuclear` (existing, key 323)** (CHG, DEF). Re-point to the deferred `fission_weapon`. See §4.6.

### 4.5 Changes to existing nodes, collected

1. `nuclear_fission`: prerequisites become `nuclear_physics`. Drop the unlock "nuclear weapon (D-011
   deterrence)". Narrow "reactor" to research reactor. Update the description: the current one mentions
   Trinity, which belongs to D01.
2. `nuclear_power`: prerequisites as in C22.
3. `nuclear_submarine`: prerequisites as in C26.
4. `thermonuclear`: prerequisites as in C27.
5. **Knock-on effects to rule on (outside this chain):** `icbm` requires `thermonuclear`, so it is
   deferred with weapons. `nuclear_fusion_research` currently requires `thermonuclear AND
   superconductivity_applied`, which would make controlled fusion unreachable while weapons are deferred.
   Consider re-pointing it to `nuclear_physics AND superconductivity_applied`. That recommendation is a
   design judgement; this session did not research fusion history.
6. `chemistry_quantitative`: no change. The uranium colourant (glass and glazes) can be a product recipe
   it unlocks, needing accessed uranium ore. No new node is needed [NUC-06–NUC-09].

### 4.6 The weapons question (marked explicitly)

**Facts relevant to the decision.** Weapons need highly enriched uranium (above 90%) or plutonium
[NUC-119]. These come from enrichment (C14) or from a reactor plus reprocessing (C17 + C20) [NUC-56,
NUC-57, NUC-121]. Both are steps the civil fuel cycle also uses, so they are dual-use [NUC-117, NUC-118].
The first programme took about $2.2 billion and about 129,000 workers at peak [NUC-218, NUC-219]. The
nuclear-armed club grew from 1945 to 1964 [NUC-214], and the NPT froze the treaty definition at devices
exploded before 1 January 1967 [NUC-216].

**Proposal.**
- (a) **Defer the weapon itself to a later milestone (recommended).** Leave D01 unadmitted, re-point
  `thermonuclear` to it, and accept that `icbm` is deferred too. Civil chains (C12–C25) stay fully
  playable, because no civil node requires D01.
- (b) Keep the chain *weapons-ready*: flag C14, C15 and C20 facilities as proliferation-sensitive, so a
  later diplomacy and safeguards milestone can attach consequences without restructuring the tree.
- (c) The alternatives, if the Director wants deterrence in M6: (i) admit D01 with the prerequisite shown
  in §4.2; (ii) model weapons as a *project* realized by the military system, needing D01 eligibility plus
  stocks of fissile material. A research node alone should never confer a weapon.
- **Director decision required:** whether D-011 deterrence stays in scope for M6. The existing node
  bundles it into `nuclear_fission`.

### 4.7 Mechanisms the chain relies on (not research nodes)

1. **Deposit layer.** World generation places deposits by IAEA type, each with grade, tonnage, depth and
   linked by-products (Cu, V, P, Ra), drawn from type models [NUC-162, NUC-250, NUC-163, NUC-10].
   Deposits are hidden until located.
2. **Locating.** Survey activities (C03, C05, C08) applied to accessed tiles produce prospectivity values
   and anomalies with false positives. Discovery rates are low [NUC-180]. Radiometric surveys respond to
   the gamma field around radioactive rock and soil [NUC-175]; deep deposits need geophysics and drilling
   (inference: see Q2).
3. **Identifying and proving.** Mineralogy (C02) types an occurrence. Assaying (C01) and drilling upgrade
   confidence from inferred to indicated to measured. Price and costs (the modifying factors) decide
   whether a resource is also a reserve [NUC-172, NUC-173].
4. **Lead time.** Discovery to production is a multi-year pipeline: 8–10 years plus exploration (older
   estimate), 15–20 years now [NUC-181, NUC-171]. Cigar Lake took about 34 years from discovery (1981) to
   commercial production (2015) [NUC-190].
5. **Hazard exposure.** Radon and radiation exposure feed health outcomes whether or not research has
   happened. C09 practices reduce exposure [NUC-148, NUC-145].
6. **Electricity as a plant input.** Energy per SWU depends on the method [NUC-81]. Unit size is
   constrained by the grid [NUC-221].
7. **Decaying isotope stocks.** Short half-lives (Mo-99, Tc-99m) need continuous production [NUC-199].
8. **Spent fuel.** It accumulates, its heat decays, and it moves from pool to cask to repository
   [NUC-123, NUC-125].
9. **Sensitive-facility flag.** C14, C15 and C20 facilities are flagged for later diplomacy and safeguards
   systems [NUC-117].
10. **Reactor-variant safety parameter.** Each design variant carries a parameter such as the void
    coefficient, feeding an accident-risk mechanism [NUC-82, NUC-93, NUC-94].

Dimensional note: every rate in these mechanisms (survey coverage, mill throughput, SWU per year, decay)
is per simulation year and integrated with `dtYears`, as the constitution requires. Conserved stocks (ore,
U3O8, UF6, fuel, waste, isotopes) belong in the ledger.

---

## 5. Open questions

- **Q1.** What role did electrical and electromagnetic surveys play in the Athabasca discoveries? Not
  sourced, because the search budget ran out. It decides whether C05 is on the critical path for
  unconformity deposits.
- **Q2.** How deep can gamma-ray surveys "see"? The claim that only near-surface material is detected is
  an inference, not a sourced fact. Verify against IAEA-TECDOC-1363 [NUC-178] before parameterising C08.
- **Q3.** Heavy-water production history and methods (for example the Vemork plant, chemical exchange)
  were not researched. They are needed to cost C18's heavy-water plant.
- **Q4.** Does the institutions system gate on knowledge directly? If so, C04 could merge into C03.
- **Q5.** Which existing node should carry reactive-metal metallurgy (Kroll zirconium)? `aluminium` is
  proposed as a proxy (C16, C17).
- **Q6.** Which materials node represents high-strength rotor materials for C15?
- **Q7.** Should the shared geology nodes (C01–C05) be coordinated with other resource case studies
  (iron, coal, oil), which may propose the same nodes under different ids?
- **Q8.** Is radiocarbon dating worth a node? Its facts are recorded [NUC-207, NUC-208], but no current
  system consumes absolute dating, so it is left out of the chain.
- **Q9.** The deferral of weapons and the coupling between fusion and weapons (§4.5 item 5, §4.6) need a
  Director ruling.
- **Q10.** The methods behind the uranium purity needed for CP-1 and Hanford (refining to metal) are not
  sourced beyond graphite. If the sim models fuel-grade purity separately from yellowcake, a refining step
  may be needed between C13 and C12/C16.
- **Q11.** The reactor counts by type come from a PRIS snapshot of uncertain date [NUC-88]. Re-query the
  live PRIS before using them as calibration targets.
- **Q12.** Onkalo's operating licence was pending at the time of search [NUC-128, NUC-129]. Re-check
  before citing its status.
- **Q13.** Thorium (another fertile material, and a source of airborne radiometric anomalies [NUC-178])
  was not researched as a fuel path.
