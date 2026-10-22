# Civilization VI and selected strategy games: a cited comparative-design study for the M6 knowledge redesign

**Status: RESEARCH INPUT. Not a ruling, not a spec, allocates no packet, authorizes no code.** Prepared 2026-10-22 as the
cited Civilization VI comparison contemplated by ruling-docket item D-U (unruled). Every recommendation below is
**PROPOSED**; nothing here amends a frozen document, a D-decision, an ADR or the milestone order. All sources were
accessed on 2026-10-22. The machine-readable source list is `sources/comparative-sources.json` (71 records; ids match the
bracketed citations here).

**How to read the labels** (full key in §1.3): **OBS** = sourced observation; **OBS-DATA** = read or counted from a
game's own shipped data/script files; **OBS-COMMUNITY** = supported only by community sources; **INF** = this study's
inference (the sources it rests on are cited); **UNSOURCED-INFERENCE** = no retrieved source; **REC** = recommendation.
In §3, the fields "Version", "Player decision" and "Causal loop" carry no label of their own: they summarise the
labelled observations directly above them and count as INF.

---

## 0. Executive summary

### 0.1 Top lessons

1. **Boost size is established for the base ruleset only; the famous "40%" is community-reported.**
   OBS-DATA [CIV-02, CIV-03]: in the base ruleset every one of the 112 boosts (64 technology, 48 civic) is worth 50% of
   the node's cost, and that value is identical in all 25 sampled snapshots from the 2016-10-20 release to the
   2024-07-15 update. OBS-DATA [CIV-10]: China adds 10 points; the in-game text describes 60% instead of 50%.
   OBS-DATA [CIV-20] + OBS [CIV-35]: Babylon (November 2020) turns a Eureka into the full cost while cutting Science by
   50% in every city. OBS-COMMUNITY [CIV-42, CIV-44, CIV-45]: the Rise and Fall and Gathering Storm rulesets use 40%, and
   two Rise and Fall dedications add 10%; no official patch note or expansion data file could be retrieved to confirm
   it. OBS-COMMUNITY [CIV-43] (low): a May 2020 change reportedly computes boosts on current, era-adjusted cost.
   INF: our 40%-of-base-cost cap sits at the expansion-era value; keep it.
2. **Two currencies and two trees, with tree-pure prerequisites and soft coupling.** OBS-DATA [CIV-05, CIV-16]: each
   citizen yields 0.7 Science and 0.3 Culture; Science buys technologies, Culture buys civics. OBS-DATA [CIV-18]: the
   prerequisite tables never cross trees. OBS-DATA [CIV-02, CIV-03]: the trees touch only through 9 cross-tree boosts
   and through content gates (e.g., the Great Library needs a civic [CIV-06]; the Theater Square, Neighborhood and
   Entertainment Complex districts need civics [CIV-12]). OBS [C7-02]: Civilization VII also keeps a separate
   Technology and Civics tree in every Age. INF: this supports the directed Civics Points pool and distinct tree
   (D-061, PROPOSED) and argues for expressing civic
   conditions as realization conjunctions or bounded boosts rather than as civic atoms inside technology prerequisite
   gates (relevant to CR-023 / D-061.8).
3. **Civ VI channels diffusion through discrete, contact-based boosts; other games show continuous alternatives.**
   OBS-DATA [CIV-02, CIV-03, CIV-17]: meeting a civilization boosts Writing, a trade route boosts Currency, a research
   agreement boosts Chemistry, and six critical late technologies can be boosted only by a spy or a Great Scientist.
   OBS-DATA [CIV-06, CIV-07, CIV-09, CIV-24]: the Great Library grants every Ancient and Classical boost, spies steal
   boosts from Campuses, Russia earns +1 Science per 3 technologies its trade partner leads by, and a maximum-level
   Gathering Storm research alliance shows a research bonus on shared targets. OBS-DATA [CIV-05] (absence): no
   "others already know it" discount appears among the base parameters. OBS-DATA [C4-01]: Civilization IV applies a
   continuous research modifier proportional to the share of contacted teams that know the technology; OBS [V3-01]:
   Victoria 3 spreads only technologies whose prerequisites the receiver already has, at a literacy-driven pace.
   INF: foreign exposure should accrue through contact channels, be prerequisite-gated, be deterministic, and keep
   sharing the Eureka cap.
4. **Knowledge changes perception, not the world.** OBS-DATA [CIV-04, CIV-23]: each strategic resource carries
   map-placement frequencies and a reveal technology, and a plot's resource is shown to a player only when it is
   visible to that player; the reveal technologies were retuned in February 2017. OBS-DATA [CIV-23] + OBS [CIV-29,
   CIV-30]: Gathering Storm converts access into per-player stockpiles with accumulation, imports, a cap, unit demand
   and power-plant demand. OBS-DATA [C4-01]: Civilization IV's AI values
   only revealed plots and sees a resource only if its team knows the reveal technology. INF: adopt per-polity filtered
   accessors for both the player's UI and the AI.
5. **Information is layered state.** OBS-DATA [CIV-26]: a per-player visibility object decides which plots are drawn.
   OBS [CIV-38, CIV-39]: areas a player has left are drawn as an old map (remembered, not live). OBS [CIV-16]: each civ
   holds one of five Diplomatic Visibility levels about every other civ, raised by concrete channels (trade route,
   delegation or embassy, spy listening post, alliance, researching Printing). INF: three-state cell knowledge
   (unknown / remembered / live) plus a per-polity intelligence level is a coherent target for M6 fog and M8 diplomacy.
6. **Where Civ VI uses clocks, free grants or asymmetry, we must not.** OBS-DATA [CIV-25]: the Rise and Fall and
   Gathering Storm era UI estimates the world-era end from a minimum end turn, a maximum end turn and a countdown.
   OBS-DATA [CIV-09]: high-difficulty AI players receive yield scaling and a random free technology and civic boost each
   new era. OBS-DATA [CIV-21]: Dramatic Ages uses different city-loss parameters for humans and AIs. OBS [C7-01]:
   Civilization VII's Age Progress includes a per-turn trickle; OBS [HK-01]: Humankind grants periodic catch-up stars.
   INF: these conflict with Law 2 (mechanisms over modifiers), Law 4 (no calendar gates) and AI information parity.
7. **Overflow is normally carried.** OBS-DATA [C4-01]: Civilization IV carries excess research into the next
   technology and banks a turn's research when no target is set. OBS-COMMUNITY [CIV-47]: Civilization VI carries
   overflow except around instant-yield bugs. INF: with variable `dtYears`, losing overflow makes the effective research
   rate depend on turn length, which works against dt-correctness (Law 3); this bears on D-R.
8. **Granularity: few nodes, auto-pathing, depth over breadth.** OBS-DATA [CIV-02, CIV-03]: base Civ VI ships 68
   technologies and 51 civics. OBS [CIV-16]: selecting a distant node auto-queues the quickest prerequisite route.
   OBS [C7-02, C7-03, C7-04]: Civilization VII uses per-Age trees, "Masteries" that deepen an already researched node,
   and in May 2026 removed almost all passive yield increases from its tech tree. OBS [OW-02]: Old World offers four
   random technology cards per choice. INF: with roughly 426 technology nodes and one active target, our graph needs a
   goal-directed research queue; depth should come from the existing post-research states, not more nodes; random
   offers do not fit a causal-historical tree.

### 0.2 Proposed positions for our simulation (not rulings)

| # | Position (PROPOSED) | Milestone | Basis |
|---|---|---|---|
| P1 | Keep Eureka partial credit with authored causal triggers and the 40%-of-base cap; reject free-boost channels (tribal villages, wonder lump grants, difficulty boosts, full-cost boosts) | M6 | §3.2 |
| P2 | Separate Civics Points pool and distinct civics tree; tree-pure prerequisites; cross-tree links only as realization conjunctions or bounded boosts; CP generation through a named equation | M6 (adoption M8) | §3.1 |
| P3 | Foreign exposure = per-year accrual through contact channels, gated on known prerequisites, proportional to the share of known contacts holding the node, deterministic, sharing the 40% cap | M6 | §3.8, §5.2, §5.4 |
| P4 | Carry research overflow to the next target of the same pool, bounded (never a general bank) | M6 (D-R) | §3.4 |
| P5 | Goal-directed research queue (auto-path) as an order, with turn-exact delivery pins; hide nodes whose prerequisites are unknown, identically for AI | M6 | §3.3 |
| P6 | Knowledge-gated resource recognition per polity; AI and UI read through the same filtered accessors; unknown cells worthless to AI valuation | M6 (D-T, D-K) | §3.5, §3.10, §3.14 |
| P7 | Three-state cell knowledge (unknown / remembered snapshot / live) | M6 | §3.10 |
| P8 | Historic-moment-style milestone facts feed computed Ages; no turn windows, no age modifier packages | M6 (consequences M8) | §3.13, §5.6 |
| P9 | Government forms as adoption capacity, practices adopted into it, each acting through a named mechanism; no policy-card modifiers | M6 content, M8 adoption | §3.7 |
| P10 | Defer stockpiles/power, espionage, research agreements/alliances, diplomatic intelligence levels, Great People, promotions, masteries, repeatable future nodes | M7 / M8 / M9+ | §6 |

### 0.3 What this study could not source (details in §7.3)

Official confirmation of the 40% expansion-era boost value and of the +10% dedications; the Gathering Storm strategic
resource reveal technologies; Gathering Storm nuclear accident and plant-ageing rules; the size of the Gathering
Storm research-alliance bonus; whether Civ VI's AI reads hidden map information (no developer statement found);
Civ VI's engine-side overflow rule; the exact vanilla Civilization IV value of the known-team research modifier.

---

## 1. Method and source standards

### 1.1 Questions

The brief asked how Civilization VI (base 2016, Rise and Fall 2018, Gathering Storm 2019, New Frontier Pass 2020-21,
Leader Pass 2022-23, significant patches) handles knowledge progression, civics, boosts, resource discovery,
cities/institutions, government, contact and diplomacy, fog and exploration, promotions, Great People, Ages and AI
information access, and which 3-6 other games genuinely inform tree granularity, civics-versus-technology separation,
diffusion, resource discovery, and fog/AI information. Six were selected (§5.0).

### 1.2 How evidence was retrieved in this session

- **Page fetches reached only GitHub-hosted content.** Fetches of civilization.com, civilization.2k.com,
  support.civilization.com, Wikipedia, Steam Community, designer-notes.com and the Internet Archive failed in this
  session (name resolution or proxy denial), as did proxy connections to CivFanatics, Fandom and PC Gamer; other
  non-GitHub hosts (e.g., Paradox) were therefore not attempted. Consequences:
  - **Game data and scripts were read directly.** Civilization VI's shipped gameplay XML/SQL, in-game text and DLC mode
    files were downloaded from a third-party GitHub mirror that keeps one commit per game update, pinned to commit
    SHAs [CIV-01]; Civ VI's shipped UI Lua scripts from the CivFanatics UI mirror [CIV-23..CIV-26]; Civilization IV's
    released SDK C++ source from a GitHub mirror [C4-01]. Counts and comparisons were computed locally from those files.
    These are the strongest evidence in this study, with one caveat: mirror fidelity was not independently verified
    (one cross-check passed: the mirror's 2020-08-27 commit matches the official patch-note date [CIV-27]).
  - **Every other source is a search-result extract.** Official pages, press, developer diaries and community pages
    were retrieved through the session's web-search tool, which returns a summary of matching pages. They are cited
    only for what the extract attributed to them, and the JSON `notes` field says so. Their confidence is capped at
    medium.
- **The search budget ran out** before every open question was searched; the remaining gaps are listed in §7.3 rather
  than filled from memory.
- **No game fact is stated from memory.** Where a fact was known but not retrieved, it was dropped or labelled
  UNSOURCED-INFERENCE.
- **Coverage gap that matters:** the data mirror contains the base ruleset and the DLC packs and modes, but **not** the
  Rise and Fall (Expansion1) or Gathering Storm (Expansion2) ruleset data. Expansion-ruleset numbers therefore rest on
  official extracts, UI scripts, or community sources, and are labelled accordingly.

### 1.3 Evidence classes and labels

| Label | Meaning | Relation to the planning package's vocabulary |
|---|---|---|
| **OBS** [id] | Observation stated by a retrieved source | RECORDED (external) |
| **OBS-DATA** [id] | Value read or counted from a game's own data, text or script files | MEASURED, on another game's files |
| **OBS-COMMUNITY** [id] | Only community sources support it (wikis, forums, Steam, Reddit) | RECORDED, low weight |
| **INF** | This study's inference or analysis; cites what it rests on | INFERRED |
| **UNSOURCED-INFERENCE** | Plausible but no retrieved source | INFERRED, unsupported |
| **REC** | ADOPT / ADAPT / REJECT / DEFER recommendation | PROPOSED |

Source types in the JSON: `official` (Firaxis/2K/Amplitude pages, patch notes, store listings, and Firaxis-authored
data/script/SDK files even when hosted by a mirror), `developer` (dev diaries, designer essays), `press`, `community`
(wikis, forums, Steam, Reddit, mirrors themselves, mods). Totals: official 40, community 15, press 9, developer 7.

### 1.4 Version discipline

Each Civ VI claim says which ruleset it describes. "Base ruleset" means the game without Rise and Fall or Gathering
Storm active; the base ruleset continued to receive patches through 2024, so "base" values are given with snapshot
dates. Rise and Fall and Gathering Storm are separate rulesets that override base values; a value seen only in base
files is never asserted for an expansion ruleset.

### 1.5 Copyright

Facts only, in original wording. No proprietary text, tables or assets are reproduced; identifiers such as table,
parameter or file names are used as references. No quotation exceeds 15 words.

### 1.6 Recommendation vocabulary

ADOPT (take the mechanism essentially as is), ADAPT (take the idea, re-express it under our laws), REJECT (do not
take it), DEFER (possibly later; name the milestone). Milestones: M6 knowledge/research/civics eligibility/fog;
M7 battle layer and mobile agents (scouts first); M8 adoption, government, diplomacy; M9+ later. "Law n" refers to the
numbered non-negotiable laws in `CLAUDE.md`; "AI parity" means AI faces the same information constraints as the player.

---

## 2. Civilization VI release map used in this study

| Content | Date | Evidence |
|---|---|---|
| Base game release | 2016-10-21 (mirror's first snapshot is labelled 2016-10-20) | OBS [CIV-50]; OBS-DATA [CIV-01] |
| Base patches sampled | 2016-11-18, 2016-12-21, 2017-02-24 (strategic reveal techs retuned), 2017-03-28 (Future Civic added), 2017-07-27, 2017-10-19, 2017-11-15 | OBS-DATA [CIV-01, CIV-03, CIV-04] |
| Rise and Fall (Expansion1) | 2018-02-08; base update the day before | OBS [CIV-49] |
| Gathering Storm (Expansion2) | 2019-02-14 | OBS [CIV-29] |
| New Frontier Pass | from 2020-05-21 (Maya and Gran Colombia, Apocalypse mode) | OBS-COMMUNITY [CIV-46] |
| Tech and Civic Shuffle mode (free update) | 2020-08-27 | OBS [CIV-27]; OBS-DATA [CIV-19] |
| Dramatic Ages mode (Byzantium and Gaul Pack) | 2020-09-24 snapshot | OBS [CIV-32]; OBS-DATA [CIV-21] |
| Babylon Pack | 2020-11-19 snapshot | OBS-DATA [CIV-20]; OBS [CIV-35] |
| Last New-Frontier-era snapshot in mirror | 2021-04-22 | OBS-DATA [CIV-01] |
| Leader Pass | 2022-11-21 to March 2023 (consoles 2023-08-29) | OBS [CIV-33] |
| December 2022 update (AI science bias fix) | 2022-12-15 | OBS [CIV-34] |
| Last data snapshot | 2024-07-15 | OBS-DATA [CIV-01] |

---

## 3. Civilization VI mechanics

### 3.1 Two yields, two trees (Science to Technology, Culture to Civics)

- **What it does.**
  - OBS [CIV-16]: Science accumulates each turn and is spent on the current technology; Culture advances a separate
    Civics tree whose nodes unlock policy cards and governments, plus some buildings, districts and diplomatic options.
  - OBS-DATA [CIV-05, CIV-16]: base per-citizen output is 0.7 Science and 0.3 Culture (parameters 70 and 30, the same
    at release and in 2024).
  - OBS-DATA [CIV-02, CIV-03]: 68 technologies; 50 civics at release, 51 since the 2017-03-28 update.
  - OBS-DATA [CIV-18]: the technology prerequisite table references only technologies and the civic prerequisite table
    only civics; there is no cross-tree prerequisite table.
  - OBS-DATA [CIV-02, CIV-03]: the trees are coupled only by boosts: completing Feudalism, Guilds, The Enlightenment or
    Globalization boosts Stirrups, Banking, Scientific Theory or Robotics; researching Construction, Astronomy, Radio,
    Nuclear Fission or Telecommunications boosts Games and Recreation, Colonialism, Mass Media, Cold War or Social Media.
  - OBS-DATA [CIV-06, CIV-11, CIV-12, CIV-13]: content gates mix the trees: the Great Library needs the Recorded History
    civic; Theater Square, Neighborhood and Entertainment Complex need civics while Campus, Encampment and Industrial
    Zone need technologies; 7 unit types need civics; all 10 governments and 103 policy cards need civics.
  - OBS [CIV-29]: Gathering Storm adds a Future Era to both trees; OBS-DATA [CIV-19]: its ruleset includes at least the
    Buttress and Refining technologies and five late governance civics absent from base.
  - OBS [C7-02, C7-05]: Civilization VII keeps separate Technology and Civics trees per Age, and some civilizations get
    their own civics tree.
- **Version.** Base 2016; no structural change was found for the expansions; Gathering Storm adds nodes [CIV-29,
  CIV-19]; Rise and Fall tree data were not available.
- **Player decision.** Two concurrent targets: what the civilization knows how to make versus how it organises itself;
  where to invest (e.g., Campus versus Theater Square).
- **Causal loop.** Population and districts produce Science and Culture; techs and civics unlock content (districts,
  buildings, policies); content raises yields.
- **Strengths.** INF: two progress tracks double the meaningful choices without a single exchange rate between
  "engineering" and "governance"; tree-pure prerequisites keep each tree legible; soft coupling lets the trees inform
  each other without deadlocks.
- **Weaknesses.** INF: one fungible "Culture" funds art, law and ideology alike; both trees advance automatically with
  population (per-citizen base yields); many civic payoffs are policy modifiers (§3.7).
- **REC: ADAPT.**
- **Rationale.** INF: both Firaxis designs separate the currencies and the trees, which supports the Director's
  directed Civics Points pool and distinct tree (D-061, PROPOSED; conflict recorded in CR-023). Civ VI's evidence is
  specifically that prerequisites stay inside a tree while cross-tree dependencies live in realization requirements and
  bounded boosts. Civ VI's flat per-population culture should not be copied: our CP inputs are an open decision (D-S)
  and must be a named equation.
- **Architectural interaction.** Law 2: CP generation is a mechanism (an equation over institutions, practices or
  people), never a card-style percentage. Law 3: CP accrues per sim-year and integrates with `dtYears`. Law 6: the
  research and civics systems exchange completion facts through tables or events; a cross-tree boost is a table row
  written by one system and read by the other. Persistence: a second pool means new serialized rows, each needing a
  populated-table test. Where the current design puts the civic node `legal_code_roman` inside the stage that opens the
  five technology subtrees, the Civ VI pattern would express that as a realization conjunction on the stage rather than
  a civic inside a technology prerequisite set; that choice belongs to D-061.8.
- **Proposed milestone.** M6 (pool, tree, eligibility); M8 (adoption effects).

### 3.2 Eurekas and Inspirations (boosts)

- **What it does.**
  - OBS [CIV-16]: a boost is an immediate partial credit toward one specific technology (Eureka) or civic
    (Inspiration), earned by doing something in the world; the tree shows each node's trigger; boosts may also come
    from Great People, research agreements and team-shared research.
  - OBS-DATA [CIV-02, CIV-03]: base ruleset: 64 technology and 48 civic boost rows, every one at 50%, unchanged across
    25 sampled snapshots from 2016-10-20 to 2024-07-15. Seven nodes have no boost: Animal Husbandry, Mining, Pottery,
    Future Tech; Code of Laws, Ideology, Future Civic.
  - OBS-DATA [CIV-10]: China adds 10 to technology and civic boosts (release and 2024); its in-game description gives
    60% instead of 50%, confirming the base value.
  - OBS-COMMUNITY [CIV-42, CIV-44, CIV-45]: Rise and Fall and Gathering Storm rulesets use 40%; the Free Inquiry and
    Pen, Brush and Voice dedications add 10% (to 50%). No official note retrieved.
  - OBS-COMMUNITY [CIV-43] (low): from May 2020 boosts are reportedly 40% of the current, era-adjusted cost (examples:
    48% for a later-era node, 32% for an earlier-era node), where Rise and Fall had used base cost. The visible
    (truncated) community change list for that update [CIV-46] does not show it.
  - OBS-DATA [CIV-20] + OBS [CIV-35]: Babylon adds 100 to the technology boost amount so a Eureka covers the full
    cost, and applies -50% Science in all its cities.
  - OBS-DATA [CIV-02]: trigger classes grew from 56 to 62; examples include settling on the coast (Sailing), finding a
    natural wonder (Astrology), meeting a civilization (Writing), maintaining a trade route (Currency), having a
    research agreement (Chemistry), building improvements, buildings or districts, and killing units of a type.
  - OBS-DATA [CIV-02, CIV-17]: Rocketry, Nuclear Fission, Satellites, Lasers, Stealth Technology and Nuclear Fusion have
    no in-world trigger; their boost comes only from a Great Scientist or a spy.
  - OBS-DATA [CIV-06, CIV-07, CIV-09, CIV-15]: other boost sources are the Great Library (all Ancient and Classical
    boosts), a spy mission that steals a boost from a Campus, tribal villages (one or two tech or civic boosts), and,
    for high-difficulty AI players only, a random tech and civic boost at each new era.
  - OBS-DATA [CIV-26]: the Gathering Storm tech tree tracks a boosted status and trigger text per node.
- **Version.** Base 50% (2016 onward); Rise and Fall 40% plus dedications (2018, community); New Frontier current-cost
  basis (2020, community, low); Babylon full cost (2020-11).
- **Player decision.** Which actions to take for which boosts; research order (boosted nodes first because they are
  cheaper).
- **Causal loop.** In-world action produces partial credit on a specific node; the node completes earlier; the new
  capability enables further actions and triggers.
- **Strengths.** OBS [CIV-36] (low), OBS [CIV-37]: the stated intent was "active research" tied to the map, so that
  paths differ between games and discoveries feel like quests. INF: partial rather than full credit keeps research
  necessary; per-node credit makes geography and contact matter.
- **Weaknesses.** INF: triggers are single, hand-authored and binary, which reads as a checklist; several channels
  (tribal villages, the Great Library, difficulty boosts) give credit with no causal story; Babylon shows that boosts
  approaching 100% replace research. INF: the reported reduction from 50% to 40% suggests 50% made research too cheap;
  no official rationale was retrieved.
- **REC: ADOPT** per-node partial credit with authored causal triggers and the 40%-of-base cap (as already ruled in
  D-047 ruling 10). **REJECT** free channels (tribal villages, wonder lump grants, difficulty boosts) and full-cost
  boosts. **DEFER** stolen boosts to espionage (M8/M9+) and era-relative boost scaling (M9+, weak evidence).
- **Rationale.** INF: our cap equals the expansion-era value; Civ VI's own move away from 50% and Babylon's need for a
  -50% Science counterweight both show the cap is a pacing lever that must stay bounded.
- **Architectural interaction.** Law 2: the boost is a coefficient inside the progress equation (credit limited to cap
  minus credit already given), not a permanent buff. Law 5: triggers are evaluated in sorted node-key order; no
  `Dictionary` iteration. Law 3: a Eureka is a one-shot credit, but anything sharing its cap (foreign exposure) accrues
  per sim-year. State: the per-node credited fraction must be serialized (populated-table test). A cross-tree boost is
  a seam between two systems and must travel through tables (Law 6).
- **Proposed milestone.** M6.

### 3.3 Tree shape, prerequisites, costs, navigation, repeatables, Tech and Civic Shuffle

- **What it does.**
  - OBS-DATA [CIV-02]: 68 technologies over 8 eras (11, 8, 7, 9, 8, 7, 8, 10), 5 roots, 90 prerequisite edges (39
    nodes with one prerequisite, 22 with two, one each with three and four); costs 25 to 2,500; the median era cost
    rises by x3.2, x2.4, x1.4, x1.7, x1.4, x1.1, x1.3 from era to era (growth decelerates).
  - OBS-DATA [CIV-03]: 51 civics, 69 prerequisite edges, at most two prerequisites; costs 20 to 3,200.
  - OBS [CIV-16]: choosing a distant node plots the quickest route and queues the missing prerequisites (both trees).
  - OBS-DATA [CIV-02, CIV-03]: Future Tech (from release) and Future Civic (from 2017-03-28) are repeatable.
  - OBS [CIV-27] + OBS-DATA [CIV-19]: the Tech and Civic Shuffle mode (2020-08-27) shuffles nodes within their eras,
    gives them random prerequisites and costs drawn from per-era candidate lists, uses one tree for all players in a
    game, and hides each node until a prerequisite is earned. OBS-DATA [CIV-26]: the tree UI has an "unrevealed" status.
  - OBS [C7-04]: Civilization VII's 2026 update makes Future Civic repeatable in all Ages with costs rising per repeat.
- **Version.** Base; Future Civic 2017; Shuffle mode 2020 (optional).
- **Player decision.** Route planning toward goals; which branch first.
- **Causal loop.** Prerequisites order capabilities; cost growth paces eras.
- **Strengths.** INF: about 120 nodes per full game plus auto-pathing keep clicks low. OBS [CIV-27]: Firaxis valued
  variety and mystery enough to ship Shuffle.
- **Weaknesses.** INF (also OBS [OW-01] on fixed trees in general): fixed trees invite memorised optimal orders.
  INF: random prerequisites break causal plausibility, which conflicts with D-047 ruling 1 (causal prerequisites).
- **REC: ADOPT** a goal-directed research queue. **ADAPT** "hidden until a prerequisite is known" as knowledge fog on
  the tree display. **REJECT** randomized prerequisites and costs. **DEFER** repeatable future nodes (M9+).
- **Rationale.** INF: our graph has roughly 426 technology nodes and one active target, about six times Civ VI's
  technology count; without a goal queue, every completion is a manual pick (click inflation).
- **Architectural interaction.** The queue is an order stream: when a queued target takes effect relative to when it
  was issued needs a turn-exact pin (T1.9 precedent). Choosing among equal-length routes is an argmax that needs a
  composite (score, id) key and a tie-dense test. The hidden-node filter is a view of per-polity knowledge state, and
  the AI's research chooser must use the same filter (AI parity).
- **Proposed milestone.** M6 (queue, hidden display); M9+ (repeatables).

### 3.4 Research overflow

- **What it does.**
  - OBS-COMMUNITY [CIV-47] (low): Civ VI keeps Science and Culture beyond a completed node as overflow that generally
    carries over; instant-yield sources and some bugs can wipe it. OBS [CIV-16]: the in-game civics text describes the
    pool as emptied on completion and does not mention overflow (simplified text or a different rule; unresolved).
  - OBS-DATA [C4-01]: Civilization IV converts research beyond a technology's cost into overflow (normalised by the
    research modifier) and applies it to the next technology; a turn with no research target banks the turn's
    research as overflow.
- **Version.** Civ VI behaviour is engine-side and not version-dated here.
- **Player decision.** Little; timing of instant yields.
- **Causal loop.** Excess output keeps its value across completions.
- **Strengths.** INF: progress is proportional to output; no wasted-turn penalty.
- **Weaknesses.** INF: an unbounded overflow buffer is a small bank; community bug reports show edge cases.
- **REC: ADAPT**: carry overflow to the next target of the same pool, bounded (for example at most one node's cost),
  never into a general bank; the no-target case is the Director's call (D-R).
- **Rationale.** INF: two Firaxis designs avoid losing excess. In our simulation turns have variable length; if
  overflow is lost, the amount lost per completion grows with `dtYears`, so research speed would depend on turn length,
  which runs against Law 3.
- **Architectural interaction.** Overflow becomes serialized state (populated-table test); the bound preserves "no
  general bank of points"; per-pool carry keeps RP and CP separate.
- **Proposed milestone.** M6 (T6.2, D-R).

### 3.5 Strategic resource discovery, Gathering Storm stockpiles and power

- **What it does.**
  - OBS-DATA [CIV-04]: base ruleset at release: Iron revealed by Bronze Working, Niter by Gunpowder, Coal by Steam
    Power, Oil by Combustion, Aluminum by Advanced Flight, Uranium by Nuclear Fission; Horses needs no technology.
    From the 2017-02-24 update: Niter by Military Engineering, Coal by Industrialization, Oil by Steel, Aluminum by
    Radio, Uranium by Combined Arms; unchanged through 2024.
  - OBS-DATA [CIV-05, CIV-13]: base ruleset: units such as Swordsman (Iron) or Tank (Oil) require a strategic resource;
    production needs 2 of it, or 1 with the relevant district; no per-turn consumption parameters exist in base data.
  - OBS-DATA [CIV-23]: Gathering Storm tracks, per player and resource, a stockpile, a stockpile cap, accumulation per
    turn, imports per turn, a bonus per turn, unit demand per turn and power demand per turn; plot tooltips show a
    resource only if it is visible to that player.
  - OBS [CIV-29]: Gathering Storm burns Coal, Oil and renewables in power plants to supply city power. OBS [CIV-30]:
    June 2019: Uranium is burned before Oil and Coal; Harbor buildings raise stockpiles; England's mines accumulate 2
    more per turn; the Cuirassier needs Iron instead of Niter. OBS [CIV-31] (low): February 2021 removed the resource
    requirement from two units. OBS [CIV-41] (low): a worked strategic tile yields a per-turn quantity.
  - OBS-DATA [CIV-02]: four technology boosts require improving a specific resource.
  - UNSOURCED: Gathering Storm reveal technologies; nuclear plant ageing and accident rules (§7.3).
- **Version.** Base: binary access with minimums; Gathering Storm: stocks and flows plus power.
- **Player decision.** Research the reveal technology; settle, claim or improve sources; trade; split stock between
  units and power.
- **Causal loop.** Research gives recognition; recognition drives claiming and extraction; stock feeds units and power;
  units and power create military and industrial capability; capability drives competition for sources.
- **Strengths.** INF: knowledge changes what a civilization can see and value, not what exists; Gathering Storm makes
  quantity matter.
- **Weaknesses.** INF: reveal is instant and binary; the base model is a toggle; the 2017 retune (e.g., Oil moved from
  Combustion to Steel) shows reveal timing used as a balance knob rather than a historical claim.
- **REC: ADOPT** knowledge-gated recognition (a polity perceives a resource only after the relevant node is Researched).
  **ADAPT** stocks and flows as Ledger flows. **DEFER** power and nuclear mechanics.
- **Rationale.** INF: matches "the world holds the resource; knowledge recognises it", and the Civ IV source shows the
  same rule enforced for the AI through a team-filtered accessor [C4-01].
- **Architectural interaction.** Law 1: any stockpile of a good is a conserved stock; a Gathering-Storm-style cap
  needs an explicit waste or spoilage flow rather than silent clipping (INF: what Civ VI does above the cap was not
  sourced). Law 3: extraction per sim-year. Law 6: the knowledge system writes a recognition table; economy and AI read
  it. Fog interaction: two filters apply, the cell must be known and the resource recognised.
- **Proposed milestone.** M6 (recognition); M7 if units consume materials; M9+ (stockpiles, power, nuclear).

### 3.6 Cities, districts, specialists and realization

- **What it does.**
  - OBS [CIV-37]: buildings move out of the city tile into district tiles with terrain bonuses (2016 announcement).
  - OBS-DATA [CIV-12, CIV-05]: each district is eligible through one technology or one civic; a
    population-per-district parameter of 3 limits how many districts a city may hold; each district building adds one
    specialist slot; a Campus specialist yields +2 Science.
  - OBS-DATA [CIV-14]: Great Person classes attach to districts; a Great Scientist can act only with a completed Campus.
- **Version.** Base.
- **Player decision.** Where and when to place districts; which buildings; how to staff them.
- **Causal loop.** Tree eligibility leads to a production investment (realization), then staffed output, then more
  Science or Culture.
- **Strengths.** INF: the cleanest available precedent for "completion grants eligibility; realization belongs to
  another system"; population thresholds cap realization.
- **Weaknesses.** INF: specialists pay twice: every citizen yields the per-citizen base Science [CIV-16] and a Campus
  specialist adds +2 [CIV-12], so a citizen moved from a tile to a slot still counts in the base. Under a labour Ledger
  that would be double counting, the very concern recorded for D-S.
- **REC: ADOPT** the eligibility/realization split (already our five-state chain). **ADAPT** population-based capacity
  as institution staffing that withdraws labour. **REJECT** flat adjacency or slot bonuses that are not terms of a
  named equation.
- **Rationale.** INF: Civ VI shows the split works at scale; its specialist accounting is not a precedent to follow.
- **Architectural interaction.** Law 1 (staff are people moved by assignment, not created); Law 2; Law 6 (realization
  by owning systems).
- **Proposed milestone.** M6 (eligibility, existing university realization); M9+ (broader institutions).

### 3.7 Governments and policy cards

- **What it does.**
  - OBS-DATA [CIV-11]: 10 governments (Chiefdom and nine others), each unlocked by a civic, each with a fixed mix of
    military, economic, diplomatic and wildcard slots; 103 policy cards (34 military, 44 economic, 13 diplomatic, 12
    great-person or wildcard), every one unlocked by a civic; same card count at release.
  - OBS-DATA [CIV-05]: a base anarchy duration parameter (2 turns) and policy-change cost parameters exist.
- **Version.** Base.
- **Player decision.** Choose a government (its slot mix) and fill slots; swap cards.
- **Causal loop.** Civics give options; slotted cards modify yields; yields fund further civics.
- **Strengths.** INF: unlocking (knowledge) is separate from adopting (slotting), a precedent for Researched versus
  Adopted; slots model limited institutional capacity.
- **Weaknesses.** INF: most cards are free-floating percentage modifiers (conflicts with Law 2); cheap swapping means
  little institutional inertia.
- **REC: ADAPT**: macro-civics (government forms) define adoption capacity; micro-civics (practices) are adopted into
  it. **REJECT** cards as modifiers; each adopted practice acts through a named mechanism, with transition costs.
- **Rationale.** INF: keeps the useful structure (capacity plus choice) without importing modifiers.
- **Architectural interaction.** Law 2; Law 4 (adoption conditions from state, never dates); adoption is M8 per the
  planning package (D-D ruled: M6 defines prerequisites and eligibility).
- **Proposed milestone.** M6 (content and eligibility); M8 (adoption and effects).

### 3.8 Trade routes, contact and knowledge transfer

- **What it does.**
  - OBS-DATA [CIV-12]: an international trade route earns +1 Science per Campus at the destination (and +1 Culture
    per Theater Square); domestic routes earn food and production.
  - OBS [CIV-16]: a trade route raises Diplomatic Visibility with that civilization by one level.
  - OBS-DATA [CIV-02, CIV-03]: contact and trade triggers: Writing (meet a civilization), Currency (trade route),
    Foreign Trade (discover another continent), Medieval Faires (trade routes), Political Philosophy (meet
    city-states), Chemistry (research agreement).
  - OBS-DATA [CIV-09]: Russia earns +1 Science or Culture from trade routes per 3 technologies or civics the partner
    is ahead.
  - OBS-DATA [CIV-08, CIV-05]: research agreements need Scientific Theory on both sides (parameter 10 percent).
  - OBS-DATA [CIV-24]: a maximum-level Gathering Storm research alliance shows a bonus when both parties research the
    same technology or one already knows the other's target; the size was not sourced.
  - OBS-DATA [CIV-05] (absence): no parameter discounts a technology because other known civilizations have it.
  - INF [CIV-08]: no technology or map trading item is registered in the base deal data.
- **Version.** Base (trade, triggers, Russia, agreements); Gathering Storm (research alliance bonus); Rise and Fall
  alliance details not established.
- **Player decision.** Route destinations (domestic versus foreign; advanced partners for Russia); agreements and
  alliances.
- **Causal loop.** Contact and trade raise visibility, yields and boosts, which speed knowledge, which opens more
  contact (sailing, embassies).
- **Strengths.** INF: diffusion is tied to concrete channels; Russia's rule scales with the knowledge gap, a mechanism
  rather than a flat buff.
- **Weaknesses.** INF: channels are scattered one-offs; there is no general rule that a neighbour's knowledge eases
  one's own, which Civ IV [C4-01] and Victoria 3 [V3-01] both model.
- **REC: ADAPT** into foreign exposure: per-node credit accrues per sim-year through contact channels (trade, shared
  borders, envoys) with polities that hold the node, scaled by channel intensity and by the share of known contacts
  holding it (Civ IV ratio), only when the receiver knows the node's prerequisites (Victoria 3 absorptive capacity),
  deterministic, and inside the shared 40% cap. **DEFER** research agreements and alliances to M8.
- **Rationale.** INF: combines Civ VI's channel discipline with a general, gap-sensitive rule.
- **Architectural interaction.** Law 3 (per-year accrual); Law 5 (iterate contacts in sorted id order; no random
  factor); Law 6 (contact facts published by trade and diplomacy, read by research). AI parity: diffusion is a physical
  process and may read true state, but any UI or AI forecast of exposure must use believed state (INF).
- **Proposed milestone.** M6.

### 3.9 Diplomacy and knowledge: Great Library, espionage, Diplomatic Visibility

- **What it does.**
  - OBS-DATA [CIV-06]: the Great Library (Recorded History civic; needs a Library; Campus-adjacent) gives +2 Science,
    +1 Great Scientist point per turn, two writing slots, and every Ancient- and Classical-era technology boost.
  - OBS [CIV-16] + OBS-DATA [CIV-07]: spies arrive in the Renaissance, 5 per civilization (6 for one leader); one
    mission steals a technology boost from a Campus (8 turns; base-probability parameter 14); another, the listening
    post, raises Diplomatic Visibility.
  - OBS-DATA [CIV-02, CIV-17]: six critical late technologies can be boosted only by a spy or Great Scientist.
  - OBS [CIV-16]: Diplomatic Visibility has five levels per pair of civilizations; each level reveals more "gossip"
    (from declarations and wonders up to opinions, government and agendas); levels come from a trade route, a
    delegation or embassy, a spy listening post or an alliance (not both), researching Printing, and one leader ability.
  - OBS [CIV-29]: Gathering Storm adds a World Congress (diplomacy, not knowledge).
- **Version.** Base; Gathering Storm adds the World Congress.
- **Player decision.** Wonder race; spy targeting; investing in visibility.
- **Causal loop.** Theft and information act as catch-up; visibility informs diplomacy.
- **Strengths.** INF: espionage is a risky diffusion channel; Diplomatic Visibility makes knowledge about rivals an
  earned, per-pair state.
- **Weaknesses.** INF: the Great Library is a lump grant (modifier-like); must-steal boosts are a gamey constraint;
  five fixed levels are coarse.
- **REC: REJECT** wonder lump grants (an archive or library institution that raises exposure capture could be
  **ADAPTed** at M9+). **DEFER** espionage (M8/M9+). **ADAPT** Diplomatic Visibility as a per-polity intelligence level
  fed by channels and read by UI and AI alike (M8; M6 fog state should leave room for it).
- **Rationale.** INF: the intelligence layer is the diplomatic counterpart of map fog.
- **Architectural interaction.** AI parity; Law 6 (channels publish facts); persistence (per-pair rows).
- **Proposed milestone.** M8 (intelligence, espionage); M9+ (archive institutions).

### 3.10 Fog of war, exploration and map knowledge

- **What it does.**
  - OBS-DATA [CIV-26]: the UI asks a per-player visibility object which plots are revealed and draws only those.
  - OBS [CIV-38, CIV-39]: fog was redesigned as an old cartographic map; areas look like that after a player's units
    leave (remembered state) and give way to the live view when seen again.
  - OBS-DATA [CIV-23]: resource visibility is per player.
  - OBS [CIV-16]: ranged attacks need line of sight: mountains block it, hills and flight see over it.
  - OBS-DATA [CIV-05, CIV-02, CIV-15]: exploration rewards: 10 XP for revealing a natural wonder; finding a natural
    wonder boosts Astrology (the row names a Scout, which a data comment calls an AI hint); tribal villages give boosts,
    a free technology or a free Scout.
  - INF [CIV-08]: no map-sharing deal item; alliances raise Diplomatic Visibility rather than sharing maps [CIV-16].
- **Version.** Base; no expansion change established.
- **Player decision.** Scouting routes; where to look; how much risk to take exploring.
- **Causal loop.** Exploration reveals extent; revealed extent enables settling, claims, boosts and XP; expansion widens
  the explored area.
- **Strengths.** INF: three information states (unknown, remembered, live) shown distinctly; discoveries feed knowledge.
- **Weaknesses.** INF: tribal-village rewards are lotteries detached from causality; whether the AI respects fog is
  undetermined (§3.14).
- **REC: ADOPT** three-state per-polity cell knowledge with remembered-snapshot semantics (the S2a plan, D-K).
  **ADAPT** discovery facts (e.g., first sighting of a natural feature) as Eureka triggers. **REJECT** village
  lotteries. **DEFER** map trading (M8).
- **Rationale.** INF: a direct, proven presentation of the state our fog design already intends.
- **Architectural interaction.** Law 5 (monotone known-extent OR is order-independent; INF: whether Civ VI ever
  un-reveals was not sourced); AI parity; Law 6 (sight sources publish to the fog table).
- **Proposed milestone.** M6 (S2a/S2b); M7 (scouts as the first mobile agents).

### 3.11 Units: promotions and experience

- **What it does.** OBS-DATA [CIV-13, CIV-05]: 16 promotion classes; combat classes, Recon included, have 7 promotions
  over 4 levels (spies 11, apostles 9); XP comes mainly from combat (at most 10 per fight), plus exploration (10 for a
  natural wonder, 5 for a tribal village); maximum level 6.
- **Version.** Base.
- **Player decision.** Which promotion path per unit.
- **Causal loop.** Combat and exploration earn XP; promotions improve performance; better units earn more XP.
- **Strengths.** INF: exploration XP rewards scouting without combat.
- **Weaknesses.** INF: most promotions are flat percentage bonuses.
- **REC: DEFER** to M7; then ADAPT: XP as a stock changed only by events, promotions as mechanism changes (movement
  cost, sight) not flat percentages.
- **Architectural interaction.** Law 2; Law 3 (no per-turn XP trickles); M7 owns units and Action Points (D-052 §4).
- **Proposed milestone.** M7.

### 3.12 Great People and institutions

- **What it does.** OBS-DATA [CIV-14]: Great Person classes attach to districts; 21 named Great Scientists, three per
  era from Classical to Information, each needing a completed Campus. OBS [CIV-16]: Great People can grant boosts.
  OBS-DATA [CIV-06]: some wonders add Great Person points.
- **Version.** Base.
- **Player decision.** Which Great People to pursue and when to use them.
- **Causal loop.** District investment yields points; a recruited individual delivers a burst (often a boost).
- **Strengths.** INF: era-bound named individuals give history a face.
- **Weaknesses.** INF: one-shot bursts bypass the progress equation.
- **REC: DEFER** (D-047 already accepts the pattern as a reference for later).
- **Architectural interaction.** People are Ledger stocks; a "Great Person" would need a mechanism, not a burst.
- **Proposed milestone.** M9+.

### 3.13 Ages: Rise and Fall, Gathering Storm, Dramatic Ages

- **What it does.**
  - OBS [CIV-28, CIV-40]: Rise and Fall adds Golden, Dark and normal Ages decided by era score earned from Historic
    Moments; a Dark Age followed by a Golden Age becomes a stronger Heroic Age; dedications chosen at each new era add
    era score for following them. OBS-COMMUNITY [CIV-44]: dedications are picked when the world enters a new era.
  - OBS [CIV-28]: loyalty per city and Governors arrive in the same expansion.
  - OBS-DATA [CIV-25]: per-player Golden and Dark thresholds are built from a baseline tied to the previous era plus
    listed adjustments; the world-era end is estimated from a minimum end turn, a maximum end turn and a countdown
    (same in the Rise and Fall and Gathering Storm scripts). INF: world eras are clock-bounded.
  - OBS [CIV-32] + OBS-DATA [CIV-21]: Dramatic Ages (2020) forces a Golden or Dark Age every era; Dark Ages can push
    cities into Free Cities; special policies replace dedications; era score also comes from completing technologies
    or civics and promoting units; the city-loss parameter differs between human and AI players at each difficulty.
- **Version.** Rise and Fall 2018 (Ages introduced); Gathering Storm same structure; Dramatic Ages mode 2020.
- **Player decision.** Chase era score through moments and dedications to avoid a Dark Age; choose a dedication.
- **Causal loop.** Achievements give era score; era score sets the Age type; the Age type changes bonuses and loyalty;
  those change future achievements.
- **Strengths.** INF: Historic Moments turn notable facts into a recorded timeline, useful for chronicle and
  observability.
- **Weaknesses.** INF: Golden and Dark effects are modifier packages; turn windows are calendar gates; thresholds
  relative to the previous era rubber-band.
- **REC: ADAPT** Historic-Moment-style milestone facts as inputs to computed Ages. **REJECT** turn windows,
  countdowns, Age-type modifier packages and Age-linked research bonuses (e.g., dedications that raise boost size).
  **DEFER** loyalty-style consequences (M8).
- **Rationale.** INF: our Ages are computed from milestone facts and never auto-grant technologies; Civ VI supplies the
  fact-recording idea but not the timing or the effects.
- **Architectural interaction.** Law 4; Law 2; the Age system reads facts from tables (Law 6).
- **Proposed milestone.** M6 (Age integration); M8 (political consequences).

### 3.14 AI information access and difficulty

- **What it does.**
  - OBS-DATA [CIV-09]: data-level difficulty handicaps for high-difficulty AI: Science, Culture, Production, Gold and
    Faith scaling, combat and XP scaling, and a random free technology and civic boost each new era; low-difficulty
    humans get combat and XP help. No handicap concerns vision or map knowledge.
  - OBS-DATA [CIV-22]: data-level AI behaviour-tree nodes expose no fog or visibility conditions; target finding runs
    in closed engine code. OBS-DATA [CIV-02]: some boost rows carry unit fields only as AI hints.
  - OBS [CIV-34]: December 2022: the AI stopped over-weighting Science at the expense of other yields.
  - OBS-DATA [CIV-21]: Dramatic Ages scales Dark Age city loss differently for humans and AIs.
  - OBS-COMMUNITY [CIV-48] (low): a player believes the AI sees through fog; no evidence offered.
  - **No credible developer statement on Civ VI AI vision was found.** Whether it reads hidden information cannot be
    determined from the available evidence.
  - OBS [AI-01, AI-02]: a Civilization III and IV designer argues that transparent, consistent rules matter because
    players misjudge probability and suspect cheating, and recalls the original Civilization's AI creating units under
    the fog.
  - OBS-DATA [C4-01]: Civilization IV's AI computes city-site values only for plots revealed to its team (others are
    set to a "no value" marker) and sees resources only through its team's reveal technologies; only starting-position
    placement reads unfiltered data.
- **REC: ADOPT** strict parity: the AI reads the world only through the same per-polity accessors as the player's UI
  (known extent, recognised resources, intelligence level), and unknown cells are worthless to its valuation (supports
  the D-T recommendation). **REJECT** difficulty handicaps that grant knowledge or apply asymmetric rules.
- **Rationale.** INF: Civ IV shows filtered accessors are practical in a shipped civilization game; Civ VI shows what
  happens without published guarantees (persistent suspicion).
- **Architectural interaction.** AI parity; Law 6 (the AI is a system reading tables); tests can assert that AI orders
  do not change when unknown cells change (the T6.8 acceptance shape).
- **Proposed milestone.** M6 (T6.8, D-T).

### 3.15 New Frontier Pass and Leader Pass: relevance summary

- Tech and Civic Shuffle: see §3.3 (REJECT randomization; ADAPT hidden display). OBS [CIV-27]; OBS-DATA [CIV-19].
- Dramatic Ages: see §3.13 (REJECT). OBS [CIV-32]; OBS-DATA [CIV-21].
- Babylon: see §3.2 (REJECT full-cost boosts). OBS-DATA [CIV-20]; OBS [CIV-35].
- Apocalypse (May 2020): not relevant to knowledge. OBS-COMMUNITY [CIV-46].
- Leader Pass: 18 leader selections in six packs, November 2022 to March 2023 [CIV-33]; the December 2022 update fixed
  the AI's Science bias and an August 2023 update improved some leader abilities [CIV-34]. No change to research,
  civics, fog or Ages was found; no recommendation.

---

## 4. Version matrix

"COMM" = community-only support; "n/e" = not established in this study; "-" = no change found.

| Mechanic | Base game 2016 + base patches | Rise and Fall 2018 | Gathering Storm 2019 | New Frontier Pass 2020-21 | Leader Pass 2022-23 and later |
|---|---|---|---|---|---|
| Two yields / two trees | 0.7 Sci, 0.3 Cul per citizen; 68 techs, 50 civics (51 from 2017-03) [CIV-02, CIV-03, CIV-05] | n/e (no data) | Future Era in both trees; Buttress, Refining, 5 late civics [CIV-29, CIV-19] | Shuffle mode reorders within eras [CIV-27, CIV-19] | - |
| Boost size | 50% on all 112 boosts, unchanged to 2024 in base ruleset; China +10 [CIV-02, CIV-03, CIV-10] | 40%; +10% via two dedications (COMM) [CIV-42, CIV-44] | 40% (COMM) [CIV-44, CIV-45] | current-cost basis (COMM, low) [CIV-43]; Babylon full cost, -50% Sci [CIV-20, CIV-35] | - |
| Boost sources | triggers; Great People; research agreements; team; villages; Great Library; spies; AI difficulty boosts [CIV-16, CIV-15, CIV-06, CIV-07, CIV-09] | dedications (COMM) [CIV-44] | research-alliance bonus at level 3 [CIV-24] | - | - |
| Cross-tree coupling | 4 civic-to-tech, 5 tech-to-civic boosts; pure prerequisite tables [CIV-02, CIV-03, CIV-18] | n/e | n/e | - | - |
| Tree navigation / repeatables | auto-path [CIV-16]; Future Tech (2016), Future Civic (2017) [CIV-02, CIV-03] | n/e | unrevealed node status in UI [CIV-26] | hidden-until-prerequisite (Shuffle) [CIV-19] | - |
| Strategic resources | reveal by tech; retuned 2017-02-24; need 2 (1 with district), no consumption [CIV-04, CIV-05, CIV-13] | n/e | stockpile, cap, accumulation, imports, unit and power demand [CIV-23, CIV-29]; 2019-06 tweaks [CIV-30] | 2021-02 requirement removals (low) [CIV-31] | - |
| Districts / specialists | population-per-district parameter 3; 1 slot per building; Campus specialist +2 Sci [CIV-05, CIV-12] | n/e | n/e | - | - |
| Governments / policies | 10 governments, 103 cards, all civic-gated [CIV-11] | Governors, loyalty [CIV-28] | n/e | Dramatic Ages special policies [CIV-32] | - |
| Trade / contact | per-district route yields; visibility +1; Russia gap rule [CIV-12, CIV-16, CIV-09] | n/e | research alliance condition [CIV-24] | - | - |
| Diplomacy and knowledge | Great Library; spies (5); 5-level visibility [CIV-06, CIV-07, CIV-16] | n/e | World Congress [CIV-29] | - | - |
| Fog / exploration | per-player revealed state; remembered map look; exploration XP and boosts [CIV-26, CIV-38, CIV-05] | - | per-player resource visibility in tooltips [CIV-23] | - | - |
| Promotions | 16 classes, 7 per combat class, 4 levels [CIV-13] | n/e | n/e | - | - |
| Great People | district-tied classes; 21 scientists [CIV-14] | n/e | n/e | - | - |
| Ages | none (Ages arrive with Rise and Fall [CIV-28]) | Golden/Dark/Heroic, era score, dedications [CIV-28, CIV-40]; turn-bounded world era [CIV-25] | same era scripts [CIV-25] | Dramatic Ages: always Golden or Dark, Free Cities [CIV-32, CIV-21] | - |
| AI information | difficulty scaling and free boosts; no vision handicap in data [CIV-09, CIV-22] | n/e | n/e | asymmetric Dark Age parameters [CIV-21] | AI Science-bias fix (2022-12) [CIV-34] |

---

## 5. Other games

### 5.0 Selection

Selected for the five themes in the brief: **Civilization VII** (granularity, civics/technology separation, Ages),
**Civilization IV** (diffusion, overflow, AI information; readable source), **Europa Universalis IV** (geographic
spread of institutions), **Victoria 3** (prerequisite-gated diffusion), **Old World** (granularity and choice), and
**Humankind** (milestone-based era advancement). Crusader Kings III and Stellaris were considered and dropped: no
official source on CK III innovation spread was retrieved, and Stellaris was not searched before the search budget ran
out.

### 5.1 Civilization VII (2025; updated 2026)

- OBS [C7-01]: at launch, three Ages (Antiquity, Exploration, Modern), each roughly 150-200 turns at standard speed,
  with a new civilization chosen at each transition; Age Progress fills a little every turn and more from Legacy Path
  milestones; a full meter ends the Age; Legacy Paths (Science, Military, Culture, Economy) give Golden or Dark Age
  legacies; Crises arrive near the end of an Age.
- OBS [C7-02]: every Age has its own Technology and Civics trees.
- OBS [C7-03]: the Test of Time update removed Legacy Paths after criticism that they were too prescriptive,
  replacing them with optional Triumphs (six attributes) whose major completions grant Dedications for the next Age;
  players may stay one civilization across Ages; a researched technology can be studied further into a Mastery.
- OBS [C7-04]: Update 1.4.0 (2026-05-19) removed almost all passive yield increases from the tech tree and reorganized
  it; Future Tech and Future Civic costs escalate per repeat.
- OBS [C7-05]: some civilizations get civ-specific civics trees with a Syncretism civic offering one of three
  adoption options.
- **Lessons.** INF: per-Age trees plus Masteries trade breadth for depth; removing passive yields from research is a
  shipped precedent for our "completion grants eligibility only" law; a per-turn Age trickle is a clock component;
  dropping prescriptive Legacy Paths warns against fixed milestone checklists.
- **REC.** ADOPT "no passive yields from research" (already law). ADAPT Masteries as depth via our post-research states
  (DEFER to M9+). REJECT the per-turn Age trickle. ADAPT culture-specific civic content (DEFER to M8).

### 5.2 Civilization IV (2005; Beyond the Sword SDK)

- OBS-DATA [C4-01]: research rate gets a bonus equal to a global define times (living teams we have met that know the
  technology) divided by (all living teams), plus a bonus per known alternative prerequisite; overflow carries into the
  next technology, normalised by that modifier; a turn without a target banks research as overflow; AI site values are
  computed only for revealed plots, and resources are visible to a team only with the reveal technology.
- OBS-COMMUNITY [C4-02, C4-03] (low): the known-team define is reported as 30 in the unmodded game; a mod's XML shows
  the define names with values 30/20 on one branch and 20/20 on another.
- **Lessons.** INF: diffusion can be continuous, contact-scoped and proportional; information filtering can be built
  into accessors (a resource query takes the asking team) so the AI cannot accidentally read hidden data.
- **REC.** ADAPT the known-share ratio into foreign exposure (M6). ADOPT team-scoped accessors (M6). Evidence for
  bounded carry in D-R (M6).

### 5.3 Europa Universalis IV (institutions, 2016 onward)

- OBS [EU4-01]: the penalty for an unembraced institution grows 1% per year. OBS-COMMUNITY [EU4-02, EU4-03]:
  institutions appear in provinces and spread; a country may embrace one once 10% of its autonomy-adjusted development
  has it, paying per remaining development, after which it spreads quickly through the country; each technology lists
  required institutions and missing ones raise its cost (tiered values in the wiki; up to 50% in patch 1.18).
  OBS [EU4-04]: a 2021 design charges 50% more per missing required institution.
- **Lessons.** INF: practices and institutions can be modelled as things that spread geographically and are then
  adopted at a threshold and a cost; the catch-up stick is a time-growing cost modifier.
- **REC.** ADAPT geographic presence plus adoption threshold for micro-civics (M8). REJECT time-growing cost penalties
  (Law 4 and Law 2).

### 5.4 Victoria 3 (2022)

- OBS [V3-01]: in each of three categories one technology is always spreading into a country, chosen automatically
  from current-era technologies already invented elsewhere; speed depends heavily on literacy and is slowed by
  restrictive speech laws; spread requires all prerequisites (developer reply); literacy also caps Innovation spent
  on active research. OBS-COMMUNITY [V3-02]: spread accumulates weekly from literacy plus a random factor.
- **Lessons.** INF: absorptive capacity (prerequisites, literacy) is the right gate for diffusion; involuntary
  background spread complements chosen research.
- **REC.** ADOPT the prerequisite gate for exposure (M6). ADAPT a capacity term if a literacy-like stock exists
  (M6/M9+). REJECT the random factor (prefer deterministic accrual; if randomness is ever needed it must come from an
  `RngRegistry` stream).

### 5.5 Old World (Mohawk Games)

- OBS [OW-01]: the designer argues fixed trees let veterans memorise one optimal order, notes that the first
  Civilization offered a random subset of technologies, and seeks hard choices with transparency about why options are
  missing. OBS [OW-02]: each available technology is a card; the player draws four, picks one, and the rest return only
  after a reshuffle. OBS [OW-03] (low): previews note waiting for a wanted card can slow the game and that some
  abilities allow redraws.
- **Lessons.** INF: fewer, weightier choices fight memorised paths but trade away agency and historical causality.
- **REC.** REJECT card draws. ADOPT the transparency principle: always show why a node is unavailable (missing
  prerequisite versus unknown) (M6 UI).

### 5.6 Humankind (Amplitude, 2021)

- OBS [HK-01, HK-02]: seven Era Stars advance an era (one in the Neolithic); each era offers 21, three per affinity
  across seven affinities, each tracking a different statistic; players may linger to collect more; Competitive Spirit
  stars are granted periodically, without Fame, to players still in an era after someone has moved on.
- **Lessons.** INF: multi-category thresholds stop one dominant route from deciding an era; time-granted catch-up stars
  are a clock.
- **REC.** ADAPT multi-category milestone-fact thresholds for Age computation (M6). REJECT periodic catch-up grants and
  player-chosen timing (our Ages are computed, not claimed).

### 5.7 Cross-game synthesis by theme

| Theme | Civ VI | Other games | Implication for us (INF) |
|---|---|---|---|
| Granularity / click inflation | ~120 nodes across two trees; auto-path [CIV-02, CIV-03, CIV-16] | per-Age trees, Masteries, no passive yields [C7-02, C7-03, C7-04]; four-card hands [OW-02] | goal queue now; depth via post-research states; no random offers |
| Civics vs technology | separate pools; pure prerequisites; 9 cross-tree boosts; content conjunctions [CIV-05, CIV-18, CIV-02, CIV-03] | separate trees per Age, civ-specific civics [C7-02, C7-05]; institutions spread and gate tech cost [EU4-02] | CP pool + distinct tree; couple through realization and bounded boosts |
| Diffusion | discrete channel boosts, spies, Russia's gap rule [CIV-02, CIV-07, CIV-09] | contact-share modifier [C4-01]; prerequisite-gated, literacy-paced spread [V3-01]; geographic spread with penalties [EU4-02] | per-year, channel-based, prerequisite-gated, deterministic, capped |
| Resource discovery | tech-gated per-player reveal; Gathering Storm stocks and flows [CIV-04, CIV-23] | team-filtered resource accessor [C4-01] | recognition table per polity; Ledger stocks later |
| Fog / AI information | per-player visibility; remembered map look; 5-level diplomatic visibility; AI vision undetermined [CIV-26, CIV-38, CIV-16, CIV-22] | AI values only revealed plots [C4-01]; transparency argument [AI-01] | three-state knowledge; same accessors for UI and AI |

---

## 6. Consolidated recommendation table

| # | Mechanic | REC | Milestone | Rationale (short) | Evidence |
|---|---|---|---|---|---|
| 1 | Separate Civics Points pool and distinct civics tree | ADAPT | M6 | both Firaxis designs separate them; CP inputs via named equation (D-S) | CIV-05, CIV-16, CIV-18, C7-02 |
| 2 | Tree-pure prerequisites; cross-tree links as realization conjunctions or bounded boosts | ADOPT | M6 | Civ VI schema and boosts; informs D-061.8 / CR-023 | CIV-18, CIV-02, CIV-03, CIV-06, CIV-12 |
| 3 | Eureka partial credit, authored causal triggers, 40%-of-base cap | ADOPT (keep) | M6 | base 50%, expansions 40% (community), cap as pacing lever | CIV-02, CIV-03, CIV-10, CIV-42 |
| 4 | Free boost channels (villages, wonder lump grants, difficulty boosts) | REJECT | - | Law 2; AI parity | CIV-06, CIV-09, CIV-15 |
| 5 | Full-cost boosts (Babylon) | REJECT | - | breaks the cap | CIV-20, CIV-35 |
| 6 | Era-relative boost scaling (New Frontier) | DEFER | M9+ | weak evidence; costs are not era-relative here | CIV-43 |
| 7 | Foreign exposure via contact channels, prerequisite-gated, deterministic, shared cap | ADAPT | M6 | Civ VI channels + Civ IV ratio + Victoria 3 gate | CIV-02, CIV-09, C4-01, V3-01 |
| 8 | Bounded overflow carry within a pool | ADAPT | M6 (D-R) | Civ IV source, Civ VI community, dt argument | C4-01, CIV-47 |
| 9 | Goal-directed research queue (auto-path) | ADOPT | M6 | click inflation with ~426 nodes | CIV-16, CIV-02 |
| 10 | Hide nodes until a prerequisite is known (same for AI) | ADAPT | M6 | knowledge fog on the tree | CIV-19, CIV-26, CIV-27 |
| 11 | Randomized prerequisites and costs (Shuffle) | REJECT | - | D-047 causal prerequisites | CIV-19, CIV-27 |
| 12 | Repeatable future nodes | DEFER | M9+ | end-of-tree content only | CIV-02, CIV-03, C7-04 |
| 13 | Knowledge-gated resource recognition per polity | ADOPT | M6 | per-player reveal; team-filtered AI | CIV-04, CIV-23, C4-01 |
| 14 | Strategic stockpiles, accumulation, unit and power demand | ADAPT | M9+ (M7 if units consume) | Ledger stocks and flows; explicit waste flow | CIV-23, CIV-29, CIV-30 |
| 15 | Power plants and nuclear accidents | DEFER | M9+ | accident rules unsourced | CIV-29, CIV-30 |
| 16 | Eligibility to realization split; staffing capacity | ADOPT / ADAPT | M6 / M9+ | district precedent; avoid specialist double counting | CIV-12, CIV-14, CIV-16 |
| 17 | Flat adjacency or slot bonuses | REJECT | - | Law 2 | CIV-12 |
| 18 | Government forms as capacity, practices adopted into it | ADAPT | M6 content, M8 adoption | capacity plus choice without modifiers | CIV-11 |
| 19 | Policy cards as modifiers, cheap swaps | REJECT | - | Law 2; no inertia | CIV-11, CIV-05 |
| 20 | Research agreements; research-alliance bonus | DEFER | M8 | diplomacy owns it | CIV-08, CIV-24 |
| 21 | Great Library-style lump grants | REJECT (archive institution later) | M9+ | modifier-like | CIV-06 |
| 22 | Espionage, stolen boosts, must-steal technologies | DEFER | M8/M9+ | needs agents and diplomacy | CIV-07, CIV-17 |
| 23 | Per-polity intelligence level fed by channels | ADAPT | M8 (M6 leaves room) | diplomatic counterpart of fog | CIV-16 |
| 24 | Three-state cell knowledge with remembered snapshots | ADOPT | M6 | per-player state, remembered look | CIV-26, CIV-38, CIV-39 |
| 25 | Discovery facts as Eureka triggers | ADAPT | M6/M7 | exploration feeds knowledge | CIV-02, CIV-05 |
| 26 | Tribal-village lotteries | REJECT | - | non-causal | CIV-15 |
| 27 | Map trading | DEFER | M8 | diplomacy | CIV-08, CIV-16 |
| 28 | Promotions and XP | DEFER (then ADAPT as mechanisms) | M7 | units belong to M7 | CIV-13, CIV-05 |
| 29 | Great People | DEFER | M9+ | burst mechanics need a mechanism | CIV-14 |
| 30 | Historic-moment-style milestone facts for computed Ages | ADAPT | M6 | fact recording without timing | CIV-28, CIV-40, CIV-25 |
| 31 | Turn windows, Age trickles, periodic catch-up grants | REJECT | - | Law 4 | CIV-25, C7-01, HK-01 |
| 32 | Golden/Dark/Dramatic Age modifier packages; Age-linked boost bonuses | REJECT (consequences DEFER) | M8 | Law 2; Ages never grant knowledge | CIV-28, CIV-32, CIV-21, CIV-44 |
| 33 | AI parity through filtered accessors; unknown cells worthless | ADOPT | M6 (D-T, T6.8) | Civ IV precedent; transparency | C4-01, CIV-23, CIV-26, AI-01 |
| 34 | Difficulty handicaps, asymmetric human/AI rules | REJECT | - | AI parity | CIV-09, CIV-21 |
| 35 | Masteries (depth per node) | DEFER (ADAPT) | M9+ | depth over breadth | C7-03 |
| 36 | Geographic spread and adoption threshold for practices | ADAPT | M8 | EU4 institutions | EU4-02 |
| 37 | Time-growing technology cost penalties | REJECT | - | Law 4, Law 2 | EU4-01, EU4-03 |
| 38 | Card-draw research | REJECT (keep transparency) | M6 UI | causality, agency | OW-01, OW-02 |
| 39 | Multi-category thresholds for Age computation | ADAPT | M6 | no single dominant route | HK-01 |
| 40 | No passive yields on research completion | ADOPT (already law) | M6 | Civilization VII 1.4.0 precedent | C7-04 |

Decisions this study informs (none decided here): D-R (row 8), D-S (rows 1, 16), D-T (row 33), D-K (row 24), D-J
(rows 25, 28; INF: Civ VI treats scouting as a unit class with exploration XP, consistent with scouts arriving as the
first mobile agents in M7), D-061 and CR-023 (rows 1-2), D-U (this document).

---

## 7. Observation versus inference

### 7.1 Discipline applied

Every observation bullet in §§0, 3 and 5 starts with a label from §1.3; in §3 the unlabelled "Version", "Player
decision" and "Causal loop" fields are summaries of the labelled observations above them and count as INF, and §5's
"Lessons" are INF. Recommendations are always INF-based and cite the observations they rest on. Community-only facts
are labelled OBS-COMMUNITY even when widely repeated. A value read in base-ruleset files is never asserted for the Rise
and Fall or Gathering Storm rulesets.

### 7.2 Principal inferences and what they rest on

| Id | Inference | Rests on | Strength |
|---|---|---|---|
| I-1 | Rise and Fall and Gathering Storm boosts are 40% | three independent community sources; base data rules out a base-ruleset change | medium |
| I-2 | The cut from 50% to 40% was meant to slow research | timing only; no official rationale | low |
| I-3 | Rise and Fall and Gathering Storm world eras are clock-bounded | UI script calls for minimum/maximum end turn and a countdown [CIV-25] | medium |
| I-4 | Civ VI has no technology or map trading | deal-item data [CIV-08] | medium |
| I-5 | Civ VI has no general "others know it" discount | absence among base parameters [CIV-05]; engine may differ | medium-low |
| I-6 | Civ VI AI fog behaviour is undeterminable from evidence | no statement found; closed engine [CIV-22] | high (that it is undetermined) |
| I-7 | Losing overflow makes research rate depend on turn length | arithmetic on our variable `dtYears` | high |
| I-8 | ~426 nodes with one target means click inflation without a queue | node counts [CIV-02] vs our graph | medium |
| I-9 | A Gathering Storm stockpile cap discards excess | UI caps the displayed total [CIV-23] | low |
| I-10 | Tribal-village turn attributes are earliest-turn gates | attribute name [CIV-15] | low |
| I-11 | The Dramatic Ages parameter is a share of cities lost | parameter name [CIV-21] | medium |
| I-12 | Civ VI specialists are paid twice (per-citizen base plus slot) | [CIV-16] + [CIV-12] combined | medium |

### 7.3 Claims that could not be sourced

1. An official (Firaxis/2K) statement of the 40% boost value in Rise and Fall and Gathering Storm, and of the +10%
   dedications. Only community sources were retrieved [CIV-42, CIV-44, CIV-45]; expansion data was not in the mirror.
2. The Gathering Storm strategic-resource reveal technologies (only base-ruleset values [CIV-04] are established).
3. Gathering Storm nuclear plant ageing, accident probability and fallout rules (no source retrieved; search budget
   exhausted).
4. The size of the Gathering Storm research-alliance bonus (only its condition [CIV-24]); Rise and Fall alliance rules.
5. Whether Civ VI's AI reads hidden map or resource information (no developer statement found; engine code closed).
6. Civ VI's engine-side overflow rule (community only [CIV-47]; in-game text silent or contradictory [CIV-16]).
7. The May 2020 boost-calculation change (single community account [CIV-43]).
8. The vanilla Civilization IV value of the known-team research define (community and mod files only [C4-02, C4-03]).
9. Crusader Kings III innovation spread and Stellaris research options (not sourced; excluded from §5).

---

## 8. References

All accessed 2026-10-22. Machine-readable list with claims and confidence notes: `sources/comparative-sources.json`.

### Civilization VI: game data, in-game text and scripts (Firaxis-authored, via mirrors)

- **[CIV-01]** mrobaczyk/civ6 - Civilization VI xml/sql/lua/txt files across updates (repository and commit history). GitHub user mrobaczyk (third-party mirror of Firaxis-authored files). n.d. <https://github.com/mrobaczyk/civ6>. Type: community; confidence: medium.
- **[CIV-02]** Civilization VI base ruleset data: Technologies.xml (release and update snapshots). Firaxis Games, via CIV-01. 2016-10-20. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Technologies.xml>. Type: official; confidence: high.
- **[CIV-03]** Civilization VI base ruleset data: Civics.xml (release and update snapshots). Firaxis Games, via CIV-01. 2016-10-20. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Civics.xml>. Type: official; confidence: high.
- **[CIV-04]** Civilization VI base ruleset data: Resources.xml (strategic resource reveal technologies). Firaxis Games, via CIV-01. 2017-02-24. <https://github.com/mrobaczyk/civ6/blob/5ad4758/Base/Assets/Gameplay/Data/Resources.xml>. Type: official; confidence: high.
- **[CIV-05]** Civilization VI base ruleset data: GlobalParameters.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/GlobalParameters.xml>. Type: official; confidence: high.
- **[CIV-06]** Civilization VI base ruleset data: Buildings.xml (Great Library). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Buildings.xml>. Type: official; confidence: high.
- **[CIV-07]** Civilization VI base ruleset data: UnitOperations.xml and Espionage_Text.xml (spy missions). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/UnitOperations.xml>. Type: official; confidence: high.
- **[CIV-08]** Civilization VI base ruleset data: DiplomaticActions.xml and DealItems.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/DiplomaticActions.xml>. Type: official; confidence: medium.
- **[CIV-09]** Civilization VI base ruleset data: Leaders.xml and Leaders_Text.xml (AI difficulty modifiers; Russia Grand Embassy). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Leaders.xml>. Type: official; confidence: high.
- **[CIV-10]** Civilization VI base ruleset data: Civilizations.xml and Civilizations_Text.xml (China, Dynastic Cycle). Firaxis Games, via CIV-01. 2016-10-20. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Civilizations.xml>. Type: official; confidence: high.
- **[CIV-11]** Civilization VI base ruleset data: Governments.xml and Policies.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Governments.xml>. Type: official; confidence: high.
- **[CIV-12]** Civilization VI base ruleset data: Districts.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/Districts.xml>. Type: official; confidence: high.
- **[CIV-13]** Civilization VI base ruleset data: UnitPromotions.xml and Units.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/UnitPromotions.xml>. Type: official; confidence: high.
- **[CIV-14]** Civilization VI base ruleset data: GreatPeople.xml and GreatPeople_Scientists.xml. Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/GreatPeople_Scientists.xml>. Type: official; confidence: high.
- **[CIV-15]** Civilization VI base ruleset data: GoodyHuts.xml (tribal villages). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/GoodyHuts.xml>. Type: official; confidence: high.
- **[CIV-16]** Civilization VI in-game Civilopedia concepts text (Civilopedia_Concepts_Text.xml). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Text/en_US/Civilopedia_Concepts_Text.xml>. Type: official; confidence: high.
- **[CIV-17]** Civilization VI in-game technology text (Technologies_Text.xml). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Text/en_US/Technologies_Text.xml>. Type: official; confidence: high.
- **[CIV-18]** Civilization VI gameplay database schema (01_GameplaySchema.sql). Firaxis Games, via CIV-01. 2020-05-21. <https://github.com/mrobaczyk/civ6/blob/ee7d534/Base/Assets/Gameplay/Data/Schema/01_GameplaySchema.sql>. Type: official; confidence: high.
- **[CIV-19]** Civilization VI Tech and Civic Shuffle game mode data and text (DLC/TreeRandomizer). Firaxis Games, via CIV-01. 2020-08-27. <https://github.com/mrobaczyk/civ6/tree/main/DLC/TreeRandomizer>. Type: official; confidence: high.
- **[CIV-20]** Civilization VI Babylon Pack data (Babylon_Civilizations.xml). Firaxis Games, via CIV-01. 2020-11-19. <https://github.com/mrobaczyk/civ6/blob/main/DLC/Babylon/Data/Babylon_Civilizations.xml>. Type: official; confidence: high.
- **[CIV-21]** Civilization VI Dramatic Ages game mode data (Byzantium_Gaul_DramaticAges_MODE.xml). Firaxis Games, via CIV-01. 2020-09-24. <https://github.com/mrobaczyk/civ6/blob/main/DLC/Byzantium_Gaul/Data/Byzantium_Gaul_DramaticAges_MODE.xml>. Type: official; confidence: high.
- **[CIV-22]** Civilization VI base AI behavior trees data (BehaviorTrees.xml). Firaxis Games, via CIV-01. 2024-07-15. <https://github.com/mrobaczyk/civ6/blob/310319f/Base/Assets/Gameplay/Data/BehaviorTrees.xml>. Type: official; confidence: medium.
- **[CIV-23]** Civ6-UIFiles: Gathering Storm UI scripts TopPanel_Expansion2.lua and PlotTooltip_Expansion2.lua. Firaxis Games, mirrored by CivFanatics. n.d. <https://github.com/civfanatics/Civ6-UIFiles/blob/master/DLC/Expansion2/Replacements/TopPanel_Expansion2.lua>. Type: official; confidence: high.
- **[CIV-24]** Civ6-UIFiles: Gathering Storm UI script AllianceResearchSupport.lua. Firaxis Games, mirrored by CivFanatics. n.d. <https://github.com/civfanatics/Civ6-UIFiles/blob/master/DLC/Expansion2/Additions/AllianceResearchSupport.lua>. Type: official; confidence: medium.
- **[CIV-25]** Civ6-UIFiles: Rise and Fall and Gathering Storm era UI scripts (EraProgressPanel.lua, EraSupport.lua). Firaxis Games, mirrored by CivFanatics. n.d. <https://github.com/civfanatics/Civ6-UIFiles/blob/master/DLC/Expansion2/Additions/EraProgressPanel.lua>. Type: official; confidence: medium.
- **[CIV-26]** Civ6-UIFiles: base UI scripts MinimapPanel.lua, FullscreenMapPopup.lua and Gathering Storm TechTree_Expansion2.lua. Firaxis Games, mirrored by CivFanatics. n.d. <https://github.com/civfanatics/Civ6-UIFiles/blob/master/MinimapPanel.lua>. Type: official; confidence: medium.

### Civilization VI: official web pages (search-result extracts)

- **[CIV-27]** Patch Notes: August 27, 2020 (Civilization VI). 2K / Firaxis Games. 2020-08-27. <https://support.civilization.com/hc/en-us/articles/37661296703507-Patch-Notes-August-27-2020>. Type: official; confidence: medium.
- **[CIV-28]** Sid Meier's Civilization VI: Rise and Fall (2K newsroom game page; civilization.com page). 2K / Firaxis Games. n.d. <https://newsroom.2k.com/games/sid-meiers-civilization-vi-rise-and-fall>. Type: official; confidence: medium.
- **[CIV-29]** Sid Meier's Civilization VI: Gathering Storm Now Available (2K newsroom) and store page. 2K / Firaxis Games. n.d. <https://newsroom.2k.com/news/sid-meiers-civilizationr-vi-gathering-storm-now-available>. Type: official; confidence: medium.
- **[CIV-30]** Patch Notes: June 18, 2019 (Civilization VI). 2K / Firaxis Games. 2019-06-18. <https://support.civilization.com/hc/en-us/articles/37662399287443-Patch-Notes-June-18-2019>. Type: official; confidence: medium.
- **[CIV-31]** Patch Notes: February 25, 2021 (Civilization VI). 2K / Firaxis Games. 2021-02-25. <https://support.civilization.com/hc/en-us/articles/37660820843667-Patch-Notes-February-25-2021>. Type: official; confidence: low.
- **[CIV-32]** Civ VI: Byzantium and Gaul Pack (official pack page) and 2K newsroom announcement. 2K / Firaxis Games. n.d. <https://civilization.2k.com/en-GB/civ-vi/new-frontier-pass/byzantium-gaul/>. Type: official; confidence: medium.
- **[CIV-33]** Civilization VI: Leader Pass To Bring 18 New Leader Selections (2K newsroom) and Leader Pass support article. 2K / Firaxis Games. n.d. <https://newsroom.2k.com/news/sid-meiers-civilizationr-vi-leader-pass-to-bring-18-new-leader-selections-to-the-critically-acclaimed-strategy-game-on-pc-mac-and-ios>. Type: official; confidence: medium.
- **[CIV-34]** Civ VI December 2022 Update (official update notes). 2K / Firaxis Games. 2022-12-15. <https://civilization.com/news/entries/civ-vi-december-2022-update/>. Type: official; confidence: medium.
- **[CIV-35]** Civilization VI - Babylon Pack (Microsoft Store listing). 2K. n.d. <https://www.xbox.com/en-US/games/store/civilization-vi-babylon-pack/9MVV6484VLW3>. Type: official; confidence: medium.

### Civilization VI: press

- **[CIV-36]** Civilization VI's lead designer wants to shake up players. Gamasutra. n.d. <https://gamasutra.com/design/-i-civilization-vi-i-s-lead-designer-wants-to-shake-up-players>. Type: press; confidence: low.
- **[CIV-37]** Civilization 6 announced and detailed. Well Played. n.d. <https://www.well-played.com.au/civilization-6-announced-detailed/>. Type: press; confidence: medium.
- **[CIV-38]** The art of Civilization VI. Digital Trends. n.d. <https://digitaltrends.com/gaming/art-civilization-vi>. Type: press; confidence: medium.
- **[CIV-39]** Six reasons Civilization VI was one of E3's most underappreciated gems. Game Informer. 2016-06-17. <https://gameinformer.com/games/sid_meiers_civilization_vi/b/pc/archive/2016/06/17/six-reasons-civilization-vi-was-one-of-e3s-most-underappreciated-gems>. Type: press; confidence: medium.
- **[CIV-40]** Everything we know about Civilization 6: Rise and Fall. PC Gamer. n.d. <https://www.pcgamer.com/uk/civilization-6-rise-and-fall-expansion>. Type: press; confidence: medium.
- **[CIV-41]** Civilization VI: Gathering Storm Review. The Sixth Axis. 2019-02-11. <https://www.thesixthaxis.com/2019/02/11/civilization-vi-gathering-storm-review/>. Type: press; confidence: low.

### Civilization VI: community

- **[CIV-42]** Rise and Fall Eurekas nerfed to 40%. CivFanatics forum. n.d. <https://forums.civfanatics.com/threads/rise-and-fall-eurkas-nerfed-to-40.627427/>. Type: community; confidence: medium.
- **[CIV-43]** [NFP] The New Eureka System. CivFanatics forum. n.d. <https://forums.civfanatics.com/threads/the-new-eureka-system.658687/>. Type: community; confidence: low.
- **[CIV-44]** Eureka (Civilization 6 Wiki) and Age (Civ6) (Civilization Wiki). Fandom. n.d. <https://civ6.fandom.com/wiki/Eureka>. Type: community; confidence: low.
- **[CIV-45]** Guide: Eurekas and Inspirations Chart. Steam Community. n.d. <https://steamcommunity.com/sharedfiles/filedetails/?id=1768497326>. Type: community; confidence: low.
- **[CIV-46]** Civilization VI May 2020 Update. Civilization Wiki (Fandom). n.d. <https://civilization.fandom.com/wiki/Civilization_VI_May_2020_Update>. Type: community; confidence: low.
- **[CIV-47]** Culture and Science bursts?? (CivFanatics) and 'Civ 6 mechanics iceberg' (Reddit, via a front-end mirror). 2024-10-22. <https://forums.civfanatics.com/threads/culture-science-bursts.634340>. Type: community; confidence: low.
- **[CIV-48]** Fog of war (Civilization VI General Discussions). Steam Community. n.d. <https://steamcommunity.com/app/289070/discussions/0/1843493219423834606/>. Type: community; confidence: low.
- **[CIV-49]** Civilization VI: Rise and Fall. Wikipedia. n.d. <https://en.wikipedia.org/wiki/Civilization_VI:_Rise_and_Fall>. Type: community; confidence: medium.
- **[CIV-50]** Sid Meier's Civilization VI released. CivFanatics (news). 2016-10-21. <https://civfanatics.com/2016/10/21/sid-meiers-civilization-vi-released/>. Type: community; confidence: medium.

### Other games

- **[C7-01]** Civilization VII Dev Diary #1: Ages. Firaxis Games / 2K. n.d. <https://civilization.2k.com/civ-vii/game-guide/dev-diary/ages>. Type: developer; confidence: medium.
- **[C7-02]** Sid Meier's Civilization VII glossary. Firaxis Games / 2K. n.d. <https://civilization.2k.com/civ-vii/glossary/>. Type: official; confidence: medium.
- **[C7-03]** Civilization VII Test of Time Update - Triumphs and Victories. Firaxis Games / 2K. n.d. <https://civilization.2k.com/civ-vii/game-guide/gameplay/triumphs/>. Type: official; confidence: medium.
- **[C7-04]** Civilization VII Update 1.4.0 and Patch Notes - May 19, 2026. Firaxis Games / 2K. 2026-05-19. <https://civilization.2k.com/civ-vii/game-update-notes/2026-may-19-patch-1-4-0/>. Type: official; confidence: medium.
- **[C7-05]** Civilization VII Test of Time Update - Time-Tested Civs. Firaxis Games / 2K. n.d. <https://civilization.2k.com/civ-vii/game-guide/gameplay/time-tested-civs/>. Type: official; confidence: medium.
- **[C4-01]** beyond-the-sword-sdk: unedited CvGameCoreDLL sources for Civilization IV: Beyond the Sword (CvPlayer.cpp, CvTeam.cpp, CvPlayerAI.cpp, CvPlot.cpp). Firaxis Games, mirrored by dguenms. n.d. <https://github.com/dguenms/beyond-the-sword-sdk>. Type: official; confidence: high.
- **[C4-02]** The Number Crunching Thread (page 5). CivFanatics forum. n.d. <https://forums.civfanatics.com/threads/the-number-crunching-thread.389702/page-5>. Type: community; confidence: low.
- **[C4-03]** Dawn of Civilization (Civ IV mod) GlobalDefines.xml. dguenms (mod). n.d. <https://github.com/dguenms/Dawn-of-Civilization/blob/master/Assets/XML/GlobalDefines.xml>. Type: community; confidence: low.
- **[EU4-01]** EU4 - Development Diary - 26th of May 2016. Paradox Development Studio. 2016-05-26. <https://forum.paradoxplaza.com/forum/developer-diary/eu4-development-diary-26th-of-may-2016.937696/>. Type: developer; confidence: medium.
- **[EU4-02]** Institutions - Europa Universalis 4 Wiki. Paradox-hosted wiki. n.d. <https://eu4.paradoxwikis.com/Institutions>. Type: community; confidence: medium.
- **[EU4-03]** Patch 1.18 - Europa Universalis 4 Wiki. Paradox-hosted wiki. n.d. <https://eu4.paradoxwikis.com/Patch_1.18>. Type: community; confidence: medium.
- **[EU4-04]** Europa Universalis IV - Development Diary 28th September 2021. Paradox Tinto. 2021-09-28. <https://forum.paradoxplaza.com/forum/threads/europa-universalis-iv-development-diary-28th-september-2021.1492744/>. Type: developer; confidence: medium.
- **[V3-01]** Victoria 3 - Dev Diary #27 - Technology. Paradox Development Studio. n.d. <https://forum.paradoxplaza.com/forum/threads/victoria-3-dev-diary-27-technology.1502428/>. Type: developer; confidence: medium.
- **[V3-02]** Technology - Victoria 3 Wiki. Paradox-hosted wiki. n.d. <https://vic3.paradoxwikis.com/Technology>. Type: community; confidence: medium.
- **[OW-01]** Old World Designer Notes #4: The Technology Deck. Soren Johnson / Mohawk Games. n.d. <https://www.designer-notes.com/?p=1703>. Type: developer; confidence: medium.
- **[OW-02]** Old World (VideoGameGeek entry, press-kit description). VideoGameGeek. n.d. <https://videogamegeek.com/videogame/309820/old-world>. Type: press; confidence: medium.
- **[OW-03]** PC Preview - Old World (Worthplaying) and Old World video preview (CogConnected). 2020-05-26. <https://worthplaying.com/article/2020/5/26/previews/119712-pc-preview-old-world/>. Type: press; confidence: low.
- **[HK-01]** Era Stars - Humankind Encyclopedia. Amplitude Studios (Games2Gether). n.d. <https://humankind-encyclopedia.games2gether.com/en-us/fame-cultures/game-content/era-stars>. Type: official; confidence: medium.
- **[HK-02]** Humankind Fame and Era Stars guide (PC Gamer) and Humankind tips (GamesRadar). n.d. <https://www.pcgamer.com/humankind-fame-era-stars/>. Type: press; confidence: medium.
- **[AI-01]** GD Column 7: Our Cheatin' Hearts (also 'Analysis: Game AI and Our Cheatin' Hearts'). Soren Johnson. 2009. <https://www.designer-notes.com/?p=132>. Type: developer; confidence: medium.
- **[AI-02]** Playing to Lose: AI and Civilization (GDC 2008). Soren Johnson. 2008. <https://www.designer-notes.com/playing-to-lose-ai-and-civilization-gdc-2008/>. Type: developer; confidence: low.
