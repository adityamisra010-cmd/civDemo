# Unit and content design references (design reference, not shipped)

**Authority:** [D-069](../d069-originality-and-third-party-game-references.md) R2 (Director, 2026-11-01); rule and
release gate: [ip-originality.md](../ip-originality.md).

**Status: design reference, not shipped.** This file keeps design knowledge that used to sit inside shipped content
(`Sim.Data/content/`). D-069 R2 rules that shipped content names no other game, studio or franchise, so these notes were
moved here rather than deleted. Nothing in this file is read by the game, and nothing here may be copied back into
`Sim.Data/content/` or into any player-facing text (the guard test `Sim.Tests/Conventions/ThirdPartyReferenceGuardTests.cs`
fails if it is).

The references below record which genre convention each of our identities was *compared with* while the families were
designed (D-047 Parts 2–3, ADR-031). They are comparative research of a pattern: a military role that persists across
Ages while its realization changes. They are not a name-for-name or statistic-for-statistic mapping, and no statistic,
roster or text was copied (ADR-031; unit charter, References). Whether any of our names overlaps a third-party name too
closely is reviewed separately at the release gate (`ip-originality.md` §3 item 2; first-pass report:
[`../m6-plan/ip-name-overlap-report.md`](../m6-plan/ip-name-overlap-report.md)).

## 1. Unit identity design references (moved from `unit-families.json` `reference`)

Until 2026-11-01 each of these identities in `Sim.Data/content/unit-families.json` carried a `reference` field. The
loader (`UnitFamilyContent`) parsed it into `UnitIdentity.Reference`, which nothing read. D-069 R2 removed the field
from the content and the property from the loader. The values are kept here verbatim (source: commit `511e70f`).
Identities without a row carried no `reference`.

| Family | Identity key | Identity id | Our name | Design reference (was `reference`) |
|---|---|---|---|---|
| `light_infantry` | 102 | `skirmishers` | Skirmishers | Civ VI Skirmisher |
| `light_infantry` | 104 | `rangers` | Rangers | Civ VI Ranger |
| `light_infantry` | 105 | `special_forces` | Special forces | Civ VI Spec Ops |
| `spear_anti_cavalry` | 201 | `spearmen` | Spearmen | Civ VI Spearman; AoE II Spearman |
| `spear_anti_cavalry` | 203 | `pikemen` | Pikemen | Civ VI Pikeman; AoE II Pikeman |
| `spear_anti_cavalry` | 204 | `pike_and_shot` | Pike and shot | Civ VI Pike and Shot |
| `heavy_infantry` | 301 | `warband` | Warband | Civ VI Warrior; AoE II Militia |
| `heavy_infantry` | 302 | `bronze_swordsmen` | Bronze-armed infantry | Civ VI Swordsman |
| `heavy_infantry` | 305 | `men_at_arms` | Men-at-arms | Civ VI Man-at-Arms; AoE II Man-at-Arms |
| `heavy_infantry` | 306 | `musketeers` | Musket infantry | Civ VI Musketman |
| `heavy_infantry` | 307 | `line_infantry` | Rifle line infantry | Civ VI Line Infantry |
| `heavy_infantry` | 308 | `mechanized_infantry` | Mechanized infantry | Civ VI Infantry / Mechanized Infantry |
| `ranged_infantry` | 401 | `slingers` | Slingers | Civ VI Slinger |
| `ranged_infantry` | 402 | `archers` | Archers | Civ VI Archer; AoE II Archer |
| `ranged_infantry` | 404 | `crossbowmen` | Crossbowmen | Civ VI Crossbowman; AoE II Crossbowman |
| `ranged_infantry` | 408 | `machine_gun_teams` | Machine-gun teams | Civ VI Machine Gun |
| `light_cavalry` | 502 | `light_horse` | Light horse | Civ VI Horseman; AoE II Scout Cavalry |
| `light_cavalry` | 503 | `light_lancers` | Light lancers | AoE II Light Cavalry |
| `light_cavalry` | 504 | `hussars` | Hussars | AoE II Hussar |
| `light_cavalry` | 505 | `carbine_cavalry` | Carbine cavalry | Civ VI Cavalry |
| `heavy_cavalry` | 601 | `chariots` | Chariots | Civ VI Heavy Chariot |
| `heavy_cavalry` | 603 | `knights` | Knights | Civ VI Knight; AoE II Knight |
| `heavy_cavalry` | 604 | `cuirassiers` | Cuirassiers | Civ VI Cuirassier |
| `heavy_cavalry` | 605 | `tanks` | Tanks | Civ VI Tank |
| `ranged_cavalry` | 701 | `horse_archers` | Horse archers | AoE II Cavalry Archer |
| `siege` | 801 | `siege_rams` | Rams and siege towers | Civ VI Battering Ram; AoE II Battering Ram |
| `siege` | 802 | `torsion_engines` | Torsion engines | Civ VI Catapult |
| `siege` | 803 | `trebuchets` | Trebuchets | Civ VI Trebuchet; AoE II Trebuchet |
| `siege` | 804 | `bombards` | Bombards | Civ VI Bombard |
| `siege` | 805 | `field_artillery` | Field artillery | Civ VI Field Cannon / Artillery |
| `siege` | 806 | `rocket_artillery` | Rocket artillery | Civ VI Rocket Artillery |
| `naval_combat` | 901 | `war_galleys` | War galleys | Civ VI Galley |
| `naval_combat` | 902 | `quinqueremes` | Heavy galleys | Civ VI Quadrireme |
| `naval_combat` | 904 | `ships_of_the_line` | Ships of the line | Civ VI Frigate |
| `naval_combat` | 905 | `ironclads` | Steam warships | Civ VI Ironclad |
| `naval_combat` | 906 | `destroyers` | Destroyers | Civ VI Destroyer |
| `air` | 1101 | `military_aircraft` | Military aircraft | Civ VI Biplane / Fighter |
| `reconnaissance` | 101 | `scouts` | Scouts | Civ VI Scout |

38 identities carried a reference: 35 cite Civilization VI ("Civ VI"), 13 cite Age of Empires II ("AoE II"), and 10 of them cite both.

## 2. Design notes moved from shipped `_doc` strings and node descriptions

Each entry is the original text. The shipped text was rewritten under D-069 R2 to keep our own rationale and
historical content without naming another game. The three node descriptions are player-facing; their rewrite is the
approved TEXT-only change recorded in [`../m6-plan/knowledge-redesign/tree-closure.md`](../m6-plan/knowledge-redesign/tree-closure.md) §5 (TC-3).
`research-pre-tranche2.json` (the D-063 attribution twin, embedded in `Sim.Data` and read by the CLI's
`--without-tranche2`) carried the same four research texts and was cleaned the same way.

### `Sim.Data/content/unit-families.json` — `_doc[0]`

Original text (as of `511e70f`):

> ADR-031 (D-047 Parts 2-3, ruling 18; D-043 A8, B5). Civilization-wide generic UNIT FAMILIES. A family is a military ROLE that persists across Ages; each Age changes how the role is REALIZED (the identity). Civ VI upgrade lines and Age of Empires II unit lines are referenced for the PATTERN only (role persists, realization changes); no list or statistic is copied.

### `Sim.Data/content/ages.json` — `_doc[5]`

Original text (as of `511e70f`):

> surges: the player (or AI) chooses ONE emphasis when advancing (D-047 ruling 14, the Civ VI dedication pattern adapted). The choice and its start turn are stored. NO NUMERIC EFFECT IS IMPLEMENTED: the formula, magnitude and duration are deferred (ledger §10.5, §27; D-043 DD-14). The fields below are the qualitative description the UI shows.

### `Sim.Data/content/sim.json` — `colonization._doc`

Original text (as of `511e70f`):

> ADR-036 (D-051 section 5, the player/AI Found Settlement order; PROPOSED - the legality terms await the Director's confirmation). orderPartyFraction 0.2: CHOSEN, TUNE. The party a Found Settlement order sends is this fraction of the SOURCE settlement's population, floored to whole people and allocated per bucket by a proportional floor whose remainder goes to the lowest bucket rows first (FoundingQuery.AllocateParty), never emptying a non-empty bucket (the source keeps at least one person in each). The fixed five-field order record carries the source and the site, so the size cannot ride the order. Reference class: D-051 section 5 names Civilization VI settlers as the style - a settler costs one population point and is first affordable in early cities of roughly 3-6 points (INFERRED from play, not measured), i.e. one sixth to one third of the city; 0.2 is one fifth, inside that band. At the turn-zero founding size (founding.cohortCounts, 400 people) it sends 80. The colonists carry provisions = floor(source grain x party / source population) (the existing B1 per-capita share, no new constant), so food per head at the new settlement equals the source's. Read ONLY by the order path: emergent D-037 B1 founding sizes its party from migration's unplaced demand and never reads this (T4.4 section 8 forbids any B1 TUNE). Absent section = no order can form a party (the order is inert).

### `Sim.Data/content/research.json` (and `research-pre-tranche2.json`) — `tuning._cityStatePaceFraction`

Original text (as of `511e70f`):

> R2a (docs/city-state-progression.md; Director decisions 4 and 12). TUNE, CHOSEN (L6): an UNCONTROLLED settlement researches autonomously with RP = cityStatePaceFraction x rpPerTurn(its own population). Reference: Civ VI city-states do not research at all; the Director rules ours progress slowly. 0.25 of the organized rate (before the sublinearity penalty of being one settlement) keeps a city-state substantially slower than a civilization of the same population. 0 disables city-state research.

### `Sim.Data/content/research.json` (and `research-pre-tranche2.json`) — `technologies[frontier_propulsion].desc` (key 416, player-facing)

Original text (as of `511e70f`):

> Each level raises the transit velocity of probes and expeditions — the research analogue of Civ 6's laser station, which is a project.

### `Sim.Data/content/research.json` (and `research-pre-tranche2.json`) — `technologies[trade].desc` (key 426, player-facing)

Original text (as of `511e70f`):

> Formal exchange between communities: goods valued against a counted standard and carried by pack animal or boat to partners beyond the settlement — the caravan and boat trade of the Uruk expansion and the Old Assyrian merchants of Kanesh. Within-settlement sharing and exchange need no research; trade BETWEEN settlements (domestic or foreign) does. Placement: Civ VI unlocks trade routes and the Trader with the Ancient-Era Foreign Trade civic (after Code of Laws); here the causal prerequisites are a record of quantity (clay tokens) and a carrier for goods over distance (donkey, camel or the square sail).

### `Sim.Data/content/research.json` (and `research-pre-tranche2.json`) — `civics[taxation].desc` (key 1007, player-facing)

Original text (as of `511e70f`):

> Taxation: the levy of tribute in kind — a counted share of the harvest, herd or craft output owed to the ruler or temple, assessed against the harvest and delivered into sealed stores. Its evidence is the Uruk and Ur III grain-receipt tablets, the sealed storerooms of the Ubaid and Uruk temples, and the Egyptian harvest assessment recorded beside the Nile gauge. A community without a record of what is owed has gifts and feasting, not a levy, so no Stone Age society taxes. Researching it gives the polity the knowledge of the tax edict (SetTaxRate); the edict is operational only once the polity has entered Age III, the Bronze Age (sim.json governance.taxationMinAge). Related refinements, which no longer gate the edict: tax assessment by computation (arithmetic_babylonian), taxation by area (surveying), by weight (standard_weights) and in coin (coinage_electrum). Placement: Civ VI has no tax technology — governments and their policies come through civics, starting with the first civic, Code of Laws (Chiefdom); here the causal prerequisites are counted obligations (clay tokens), sealed stores under administrative control (stamp seal) and an administrative record beyond memory (proto-cuneiform, hieroglyphic or Chinese script).
