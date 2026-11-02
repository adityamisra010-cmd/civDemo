# IP name-overlap report: first pass (D-069 R3)

**Generated** by `scripts/ip-name-overlap-report.py` from the shipped content (sha256 of research, unit-families, ages, unit-progression and sim.json together: `6dfb3958e68e718e…`) and the reference lists already in `docs/`. Do not edit by hand; regenerate with `python3 scripts/ip-name-overlap-report.py` (`--check` reports staleness). The classification column is the curated table in that script.

**Authority:** [D-069](../d069-originality-and-third-party-game-references.md) R3; rule and release gate: [ip-originality.md](../ip-originality.md) §3 item 2 and §4.

> **This is an input to the release-gate review, not a legal verdict.** A match says only that two names are equal or similar. It says nothing about whether a name is protectable, and a name with no match here is not thereby cleared. Common historical terms ("Bronze Working", "Feudalism", "Skirmisher") are fine under ip-originality.md §3 item 2; the review is about distinctive coined names, distinctive combinations, and the same names in the same tree structure.

## 1. Headline

- Shipped names compared: **1288** (each category below).
- Distinct third-party reference names in the repository's research: **1169**.
- **Exact matches** (case-insensitive equality): **49** rows, **37** distinct names of ours.
- **Near matches** (equal after normalization, small edit distance, or word containment): **69** rows, **47** distinct names of ours.
- Of those rows, **12** are spurious near matches (shared letters, not a shared name).
- **Flagged for review** (classified "distinctive/coined - review" or not yet classified): **0** distinct name(s) of ours. Every other match is a common historical term; §5 adds the same-role clusters the name-by-name view cannot show.

| Category | Shipped names | Exact rows | Near rows | Names flagged for review |
|---|---:|---:|---:|---:|
| Technology | 525 | 11 | 8 | 0 |
| Civic | 447 | 17 | 16 | 0 |
| Government form | 33 | 5 | 0 | 0 |
| Unit family | 20 | 0 | 3 | 0 |
| Unit identity | 80 | 3 | 26 | 0 |
| Unit (research entity) | 23 | 2 | 8 | 0 |
| Promotion | 15 | 0 | 1 | 0 |
| Age / era | 17 | 1 | 1 | 0 |
| Age surge | 6 | 0 | 0 | 0 |
| UI era theme | 9 | 1 | 0 | 0 |
| Building / structure | 68 | 7 | 5 | 0 |
| Institution | 28 | 1 | 0 | 0 |
| Project | 3 | 0 | 0 | 0 |
| Activity / recipe | 13 | 1 | 1 | 0 |
| Player-facing term | 1 | 0 | 0 | 0 |

## 2. Coverage and limits (read before relying on this)

- **Only lists already in this repository were used.** Nothing was fetched. The reference lists are:
  1. Civilization VI base-game civics: `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json` (the full civic list).
  2. Civilization VII: `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` (civics and masteries of the three Ages, unique civics, policies and traditions, governments, ideologies, and the buildings, wonders and units the civics unlock; names taken from game ids are marked "name from game id").
  3. The Civilization VI and VII analog recorded for each of our nodes: `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json`, `docs/m6-plan/knowledge-redesign/civics-tree-proposal.json` and `docs/m6-plan/knowledge-redesign/data/government-packages.json`. These list only the third-party nodes someone mapped to ours, not those games' full technology trees.
  4. Civilization VI and Age of Empires II unit names: the `reference` values moved out of `unit-families.json` by D-069 R2 (`docs/m7-plan/unit-design-references.md` §1).
  5. Capitalized names that directly follow a game's name in prose anywhere in `docs/**/*.md` (for example "Civ VI's Great People"). This is low-precision extraction; most of it is noise that matches nothing.
- **Not covered:** the full Civilization VI technology tree, unit roster, building, wonder, district and policy-card lists; Civilization VII technologies and units beyond those named above; every other game of the genre (Civilization I–V, Age of Empires I–IV, Europa Universalis, Victoria, Humankind, Old World, Millennia, Ara and others), whose lists are not in the repository. Those need a list before the release gate can rely on this check (ip-originality.md §3 item 2).
- **Not covered by name matching at all:** tree structure (§3 item 3), text (§3 item 4), art and audio (§3 item 5) and store text (§3 item 6).
- **Shipped names not in the comparison:** policy names (no policy content ships yet), place names (generated procedurally), good and need names (`goods.json`, `needs.json`), and UI labels other than the era enum and the Idea term (renamed from Eureka under D-069 R4).
- **Method.** Exact: case-insensitive equality of the trimmed names. Near: (a) equal after normalization (accents folded, lower case, punctuation removed, a leading article dropped, each word made singular by simple rules: "-men" → "-man", "-ies" → "-y", trailing "s"); or (b) edit distance ≤ 1 on normalized names of 5–7 characters and ≤ 2 on names of 8 or more (no fuzzy match below 5 characters); or (c) every word of the shorter name (at least two words, ignoring "of", "and", "the", "age", "era") occurs in the other. A one-word reference name inside a longer name of ours ("Pottery" in "Open-fired pottery") is deliberately not matched, because it would bury the list in generic words; the reviewer should keep that gap in mind.
- **Not in CI.** The generator has a `--check` mode, but no CI step runs it; whether it joins CI is the Director's call.
- **Classification** is a first-pass judgement: "common historical term" (a standard historical, technical or generic term any work on the subject would use) or "distinctive/coined - review" (a coined game term, a distinctive combination, or the same name in a closely similar role). It is a triage aid for the Director's review, not a clearance.

## 3. Matches by category

Columns: our category, our id, our name, the match kind, the matched reference name (game; kind), where the reference name was found, and the classification.

### Technology

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `alchemy` | Alchemy | exact | Alchemy (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | common historical term |
| `siege_ram` | Battering ram | exact | Battering Ram (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `siege_ram` | Battering ram | exact | Battering Ram (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `cavalry` | Cavalry | exact | Cavalry (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `coinage_electrum` | Coinage | exact | Coinage (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `frontier_computation` | Computation | exact | Computation (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `gunpowder` | Gunpowder | exact | Gunpowder (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `gunpowder` | Gunpowder | exact | Gunpowder (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `machine_gun` | Machine gun | exact | Machine Gun (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `tank` | Tank | exact | Tank (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `water_mill` | Water mill | exact | Water Mill (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `steam_atmospheric` | Atmospheric steam engine | near: every word of the shorter name is in the other | Steam Engine (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `celestial_navigation` | Celestial navigation with instruments | near: every word of the shorter name is in the other | Celestial Navigation (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `nuclear_fission` | Controlled nuclear fission | near: every word of the shorter name is in the other | Nuclear Fission (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `nuclear_fission` | Controlled nuclear fission | near: every word of the shorter name is in the other | Nuclear Fission (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `laser` | Laser | near: same after normalization | Lasers (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `steam_rotary` | Rotary steam engine | near: every word of the shorter name is in the other | Steam Engine (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `silk` | Sericulture | near: edit distance <= 2 | Agriculture (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |
| `stirrup` | Stirrup | near: same after normalization | Stirrups (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |

### Civic

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `common_law` | Common law | exact | Common Law (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `enlightenment` | Enlightenment | exact | Enlightenment (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `fascism` | Fascism | exact | Fascism (Civilization VI; civics analog, government) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json`, `docs/m6-plan/knowledge-redesign/civics-tree-proposal.json` (+1 more) | common historical term |
| `fascism` | Fascism | exact | Fascism (Civilization VII; civic, ideology, civics analog, government) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+2 more) | common historical term |
| `humanism` | Humanism | exact | Humanism (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `humanism` | Humanism | exact | Humanism (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | common historical term |
| `classical_liberalism` | Liberalism | exact | Liberalism (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `mercantilism` | Mercantilism | exact | Mercantilism (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `mercantilism` | Mercantilism | exact | Mercantilism (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `municipal_charter` | Municipal charters | exact | Municipal Charters (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | common historical term |
| `nationalism` | Nationalism | exact | Nationalism (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `nationalism` | Nationalism | exact | Nationalism (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `political_philosophy` | Political philosophy | exact | Political Philosophy (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `progressivism` | Progressivism | exact | Progressivism (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `social_media_platforms` | Social media | exact | Social Media (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `socialism` | Socialism | exact | Socialism (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `stock_exchange` | Stock exchange | exact | Stock Exchange (Civilization VI; civics analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `civil_code` | Civil code | near: every word of the shorter name is in the other | Code Civil (Civilization VII; civics analog) | `docs/m6-plan/knowledge-redesign/civics-tree-proposal.json` | common historical term |
| `civil_code` | Civil code | near: every word of the shorter name is in the other | Code Civil des Français (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `civil_service_examinations` | Civil service examinations | near: every word of the shorter name is in the other | Civil Service (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `conservatism` | Conservatism | near: edit distance <= 2 | Conservation (Civilization VI; civic, technology analog, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | no real overlap (spurious near match) |
| `military_engineers` | Corps of engineers | near: every word of the shorter name is in the other | Army Corps of Engineers (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `divine_right` | Divine right of kings | near: every word of the shorter name is in the other | Divine Right (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `divine_right` | Divine right of kings | near: every word of the shorter name is in the other | Divine Right (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `enlightenment` | Enlightenment | near: same after normalization | The Enlightenment (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `holy_war_doctrine` | Holy war | near: edit distance <= 2 | Cold War (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | no real overlap (spurious near match) |
| `public_hospitals` | Hospitals | near: same after normalization | Hospital (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `merit_civil_service` | Merit civil service | near: every word of the shorter name is in the other | Civil Service (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `nationalism` | Nationalism | near: edit distance <= 2 | Rationalism (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | no real overlap (spurious near match) |
| `professional_sport` | Professional sport | near: same after normalization | Professional Sports (Civilization VI; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ6-civics-base.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `public_works_relief` | Public works relief | near: every word of the shorter name is in the other | Public Works (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `think_tanks` | Think tanks | near: same after normalization | Think-tanks (Civilization VII; policy / tradition, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `war_band` | War bands | near: edit distance <= 2 | Mār-banê (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | no real overlap (spurious near match) |

### Government form

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `chiefdom` | Chiefdom | exact | Chiefdom (Civilization VI; civics analog, government) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json`, `docs/m6-plan/knowledge-redesign/data/government-packages.json` | common historical term |
| `chiefdom` | Chiefdom | exact | Chiefdom (Civilization VII; civic, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `constitutional_monarchy` | Constitutional monarchy | exact | Constitutional Monarchy (Civilization VII; government, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `feudal_monarchy` | Feudal monarchy | exact | Feudal Monarchy (Civilization VII; government, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+1 more) | common historical term |
| `revolutionary_republic` | Revolutionary republic | exact | Revolutionary Republic (Civilization VII; government, civics analog) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json`, `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` (+2 more) | common historical term |

### Unit family

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `heavy_infantry` | Heavy Infantry / Line Infantry | near: every word of the shorter name is in the other | Line Infantry (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `light_cavalry` | Mounted Scout / Light Cavalry | near: every word of the shorter name is in the other | Light Cavalry (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `light_cavalry` | Mounted Scout / Light Cavalry | near: every word of the shorter name is in the other | Scout Cavalry (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |

### Unit identity

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `mechanized_infantry` | Mechanized infantry | exact | Mechanized Infantry (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `pike_and_shot` | Pike and shot | exact | Pike and Shot (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `rocket_artillery` | Rocket artillery | exact | Rocket Artillery (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `archers` | Archers | near: same after normalization | Archer (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `archers` | Archers | near: same after normalization | Archer (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `archers` | Archers | near: edit distance <= 1 | Archery (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |
| `bombards` | Bombards | near: same after normalization | Bombard (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `crossbowmen` | Crossbowmen | near: same after normalization | Crossbowman (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `crossbowmen` | Crossbowmen | near: same after normalization | Crossbowman (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `cuirassiers` | Cuirassiers | near: same after normalization | Cuirassier (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `destroyers` | Destroyers | near: same after normalization | Destroyer (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `hussars` | Hussars | near: same after normalization | Hussar (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `knights` | Knights | near: same after normalization | Knight (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `knights` | Knights | near: same after normalization | Knight (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `machine_gun_teams` | Machine-gun teams | near: every word of the shorter name is in the other | Machine Gun (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `men_at_arms` | Men-at-arms | near: edit distance <= 2 | Man-at-Arms (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `men_at_arms` | Men-at-arms | near: edit distance <= 2 | Man-at-Arms (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `pikemen` | Pikemen | near: same after normalization | Pikeman (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `pikemen` | Pikemen | near: same after normalization | Pikeman (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `rangers` | Rangers | near: same after normalization | Ranger (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `line_infantry` | Rifle line infantry | near: every word of the shorter name is in the other | Line Infantry (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `scouts` | Scouts | near: same after normalization | Scout (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `skirmishers` | Skirmishers | near: same after normalization | Skirmisher (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `slingers` | Slingers | near: same after normalization | Slinger (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `spearmen` | Spearmen | near: same after normalization | Spearman (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `spearmen` | Spearmen | near: same after normalization | Spearman (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `tanks` | Tanks | near: same after normalization | Tank (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `trebuchets` | Trebuchets | near: same after normalization | Trebuchet (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `trebuchets` | Trebuchets | near: same after normalization | Trebuchet (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |

### Unit (research entity)

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `unit.artillery` | Artillery | exact | Artillery (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.tank` | Tank | exact | Tank (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.archers` | Archers | near: same after normalization | Archer (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.archers` | Archers | near: same after normalization | Archer (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.archers` | Archers | near: edit distance <= 1 | Archery (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |
| `unit.crossbowmen` | Crossbowmen | near: same after normalization | Crossbowman (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.crossbowmen` | Crossbowmen | near: same after normalization | Crossbowman (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.slingers` | Slingers | near: same after normalization | Slinger (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.spearmen` | Spearmen | near: same after normalization | Spearman (Age of Empires II; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |
| `unit.spearmen` | Spearmen | near: same after normalization | Spearman (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |

### Promotion

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `tree.line_infantry` | Line infantry drill | near: every word of the shorter name is in the other | Line Infantry (Civilization VI; unit) | `docs/m7-plan/unit-design-references.md` | common historical term |

### Age / era

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `modern` | Modern | exact | Modern (Civilization VII; civics analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `neolithic` | Agricultural | near: edit distance <= 2 | Agriculture (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |

### UI era theme

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `Modern` | Modern | exact | Modern (Civilization VII; civics analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |

### Building / structure

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `infra.aqueduct` | Aqueduct | exact | Aqueduct (Civilization VI; technology analog, civics analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.granary` | Granary | exact | Granary (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.granary` | Granary | exact | Granary (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.hospital` | Hospital | exact | Hospital (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.printing_house` | Printing house | exact | Printing House (Civilization VII; building (name from game id)) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | common historical term |
| `building.water_mill` | Water mill | exact | Water Mill (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.workshop` | Workshop | exact | Workshop (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.castle` | Castle | near: edit distance <= 1 | Cattle (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |
| `building.castle` | Castle | near: edit distance <= 1 | Castes (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | no real overlap (spurious near match) |
| `building.castle` | Castle | near: same after normalization | Castles (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `building.paper_mill` | Paper mill | near: edit distance <= 2 | Water Mill (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | no real overlap (spurious near match) |
| `building.power_station` | Power station | near: every word of the shorter name is in the other | Battersea Power Station (Civilization VII; wonder (name from game id)) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | no real overlap (spurious near match) |

### Institution

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `inst.hospital` | Hospital | exact | Hospital (Civilization VII; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |

### Activity / recipe

| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |
|---|---|---|---|---|---|
| `activity.mining` | Mining | exact | Mining (Civilization VI; technology analog) | `docs/m6-plan/knowledge-redesign/data/tranche2/node-audit.json` | common historical term |
| `recipe.toolmaking` | Toolmaking | near: edit distance <= 2 | Tool Making (Civilization VII; policy / tradition) | `docs/m6-plan/knowledge-redesign/data/civ7-civics.json` | common historical term |

## 4. Names flagged for review

None.

## 5. Same-role clusters (input to ip-originality.md §3 items 2 and 3)

Each name above may be common, yet the gate also asks whether the *same names in the same tree structure* recur (§3 item 2) and whether a tree mirrors another game's position for position (§3 item 3). Name matching cannot answer item 3; these clusters show where to look first.

- **Civics vs Civilization VI civics:** 10 of our 480 names match a civic name of Civilization VI: Civil service examinations ~ Civil Service; Divine right of kings ~ Divine Right; Enlightenment ~ The Enlightenment; Humanism; Mercantilism; Merit civil service ~ Civil Service; Nationalism; Political philosophy; Professional sport ~ Professional Sports; Social media. All are standard terms of political, legal or social history. Our civics tree has 480 nodes against that game's 50-odd, so the overlap is a small share of ours, but these nodes should be checked for position-for-position placement (§3 item 3).
- **Government forms vs Civilization VII governments:** 4 of our 480 names match a government name of Civilization VII: Constitutional monarchy; Fascism; Feudal monarchy; Revolutionary republic. Standard constitutional terms; the set of 33 forms is our own (tranche-2 regime-forms lane).
- **Unit identities and unit entities vs Civilization VI units:** 22 of our 103 names match a unit name of Civilization VI: Archers ~ Archer; Artillery; Bombards ~ Bombard; Crossbowmen ~ Crossbowman; Cuirassiers ~ Cuirassier; Destroyers ~ Destroyer; Knights ~ Knight; Machine-gun teams ~ Machine Gun; Mechanized infantry; Men-at-arms ~ Man-at-Arms; Pike and shot; Pikemen ~ Pikeman; Rangers ~ Ranger; Rifle line infantry ~ Line Infantry; Rocket artillery; Scouts ~ Scout; Skirmishers ~ Skirmisher; Slingers ~ Slinger; Spearmen ~ Spearman; Tank; Tanks ~ Tank; Trebuchets ~ Trebuchet. Standard military-history terms. Several of our upgrade lines follow the same historical sequence as that game's (spear to pike to pike-and-shot; knight to cuirassier to tank); the sequence is history, but the line-by-line correspondence is the place to apply §3 item 3. The former per-identity references are in `docs/m7-plan/unit-design-references.md`.
- **Unit identities and unit entities vs Age of Empires II units:** 8 of our 103 names match a unit name of Age of Empires II: Archers ~ Archer; Crossbowmen ~ Crossbowman; Hussars ~ Hussar; Knights ~ Knight; Men-at-arms ~ Man-at-Arms; Pikemen ~ Pikeman; Spearmen ~ Spearman; Trebuchets ~ Trebuchet. Standard military-history terms.
