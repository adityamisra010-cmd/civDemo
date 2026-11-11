# Morale in the reference game: research dossier (run 1)

**What this is.** The dossier step of a research workflow for lane MOR2, written 2026-11-10. It answers questions Q1 to Q8 and G
from the evidence that four research angles collected and one check step verified between 19:18Z and 21:18Z that day. The
reference is Total War Saga: TROY (2020), which the Director named as the research reference for morale at two levels: the army's
morale on the campaign map and each unit's morale in battle (D-082 DA2, `docs/d082-morale-caps-ties-director-answers-2026-11-10.md`).
The question this pass exists for is D-083's (`docs/d083-entering-battle-costs-morale-2026-11-10.md`): does the reference charge
morale for entering a battle?

**Reference only (D-069, `docs/ip-originality.md`).** This is research in a scratch directory, not shipped content. None of the
reference's names, numbers or text below enters our design; the numbers are reported only to show what the evidence contains.
**No decisions are taken here.** Section 5 is input for lane MOR2 and nothing more.

All files named below sit under the run directory
`RUN = /tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/research/run1`.

## How to read this dossier

- **Labels** (one per claim). **CODE**: definitions or values read in the game's own data, or in a faithful extraction of it, with
  a path and a hash. **FETCH**: text read on a fetched page; this run has none. **SEARCH**: a web-search result summary with its
  URL; this is weaker evidence. **KNOW**: unverified background knowledge, used only as a hypothesis.
- **Scope** (one per claim). **reference** is the reference itself. **sibling** is another title of the same series and developer,
  named in the claim. **genre** is another developer's game.
- **Verdicts** come from the check step (`RUN/check.json`). **CONFIRMED**: the checker re-measured the claim against a re-fetched
  file with its own scripts. **WEAK**: consistent with the finder's own search log or with the data, but not independently
  confirmed. **UNSUPPORTED** and **CONTRADICTED** claims are kept apart in §6 and are not used as evidence.
- **Confidence** per answer. **ESTABLISHED** needs two independent CONFIRMED sources, or CODE. **LIKELY** needs one CONFIRMED
  source. Anything else is **UNKNOWN**. "INFERRED" marks a reading of ours.

**Two consequences hold throughout.**

1. The check step could not re-run a single web search, because the session's per-turn search budget had been used up. So no SEARCH
   claim could be CONFIRMED, and every SEARCH claim is at best WEAK. Under the rubric, every answer that rests on search summaries
   alone is UNKNOWN, however many summaries agree. The text says "reported (WEAK)" for what those summaries say.
2. Every CONFIRMED claim about the reference is CODE taken from one file: an open-source pack-file tool's schema of the reference's
   database (S-TROY, §1.5). This CODE shows that the reference's data **defines** a key or a table with the description quoted. It
   gives no value and no sign, and it does not show that the game uses the key. The file holds no values at all; one key is
   described "NOT USED"; and the army-morale family carries keys for a sibling's mechanic that this title does not have. So
   "ESTABLISHED (definition level)" means exactly that, and no more. That the descriptions are faithful to the developer's own
   data is INFERRED, from in-house typos kept in them, keys found only in this title, and differences between titles. If the schema
   is held to be a fetched page rather than a faithful extraction, its claims become FETCH from a single source, and every
   definition-level answer drops from ESTABLISHED to LIKELY.

---

## 0. Summary

- **Q1: does entering a battle cost morale?** No source shows that it does, and no source rules it out. The reference's
  battle-morale definitions (129 keys) and army-morale definitions (36 keys) contain no cost for entering a battle, for deploying,
  or for time spent in one. Morale is described as a starting point plus situational terms, moved toward the computed value each
  tick. Apart from that starting point, the only terms that apply to every unit of a side are eight difficulty modifiers
  (ESTABLISHED, definition level).
  - Every term that can lower morale without losses is conditional: surprise (timed), being unprepared for a night battle, fatigue
    states, exposed flanks, enemy numbers nearby, missile and artillery fire, a frightening enemy, the direction of first contact,
    and a dead or fled general. These are ESTABLISHED as definitions; their sizes are UNKNOWN.
  - Reported (WEAK): the campaign can make units start a battle tired, through stances, landings, one scripted battle and a paid
    pre-battle action. It also applies conditional morale effects to battles.
  - The only trigger tied to taking part in a battle sits on a commander's campaign stock. One fan wiki (WEAK) says that a hero's
    "motivation" falls when he fights in his own territory or is reinforced, and that motivation feeds the hero's own morale.
  - Whether the game charges a cost for entering: UNKNOWN. The reference neither supports nor contradicts D-083 DB1.
- **Q2: army morale on the campaign map.** No source shows an army morale number in the game.
  - The data defines a complete army-morale ("military force morale") family, inherited from a predecessor, and a table that maps a
    morale level to an effect bundle (ESTABLISHED, definition level). The family has:
    - a starting value, a minimum and a maximum;
    - "low" and "very low" thresholds;
    - a bonus for a win and a penalty for a loss, each lasting some turns;
    - a penalty scaled by casualties;
    - terms for idleness, foreign soil, being led by the faction leader, the faction leader's death, looting, raiding, released
      captives, transfers, bankruptcy and attrition.
  - Whether this title uses the family: UNKNOWN.
  - Reported (WEAK): conditional morale effects that come from heroes, traits, items, buildings, gods, agents, events and stances.
    Also three stocks that battles move: one DLC faction's per-army "Battle Glory", one faction leader's mood, and every generic
    hero's motivation.
- **Q3 to Q8.** At definition level the data covers all of the following; each is ESTABLISHED as a definition, and every size is
  UNKNOWN:
  - the battle-morale factors: casualties in three time windows, contact direction, flanks, enemy numbers, missile and artillery
    fire, the commander's aura and death, nearby routs, combats won or lost, kills, the charge, army destruction, reinforcements,
    fatigue, surprise, night, fear and terror, terrain and fortification, and difficulty;
  - seven morale states;
  - rally, post-rally and shatter rules;
  - ambush rolls by ground type;
  - seven fatigue levels, with deltas by activity;
  - the channels by which a battle's result returns to the campaign: experience, captives, loot and loyalty.

  Reported sizes (WEAK) include a post-rally window of about 60 s with no rout, a 90 s charge morale window, and hero abilities
  worth plus or minus 15 to 25 morale for 25 to 60 s.
- **The three overstated cells of the morale proposal's §3.3 table.** Each now has evidence from the reference at definition level,
  rather than the sources it originally cited:
  - rout contagion: a "friends routing" concern (data-06);
  - fatigue: three fatigue morale concerns (data-07);
  - missile fire: "Attacked by projectile" and artillery terms (data-05).

  None has a size, and only missile fire has any report from the reference beyond the data: players' word (§5.4).
- **Genre (§3).** A cost for taking part is reported in the genre. A developer diary of Europa Universalis IV, as summarised, charges
  deployed troops morale damage every day (WEAK). In Mount & Blade II: Bannerlord, each soldier's starting morale is reportedly set
  from the party's morale (WEAK).
- **Limits.** 93 hosts were tested. 90 were refused at the proxy; one (`raw.githubusercontent.com`) served files. No web page was
  read, so there is no FETCH claim. No game file with values was read, nor the Assembly Kit, nor any tooltip text, nor Reddit.

---

## 1. Method and limits

### 1.1 Inputs

| File | What it holds |
|---|---|
| `RUN/official.json` | Official angle: 42 claims from patch notes and their mirrors, developer blogs, FAQs, store texts, interviews and previews. Also sibling manuals (scope sibling) and genre developers' blogs. About 70 searches; log `RUN/work-official/notes/search-log.md`. |
| `RUN/data.json` | Data angle: 28 claims. Most come from the pack-file tool's schemas of the reference and of WARHAMMER II; the rest are mod pages seen through search. 23 searches; log `RUN/work-data/notes/search-log.json`. |
| `RUN/community.json` | Community angle: 21 claims from players, guides, reviews and mods, in English, Chinese, Polish, Korean, Russian and Japanese sources. 82 searches, 80 of them answered; log `RUN/work-community/notes/search-log.md`. |
| `RUN/targeted.json` | Targeted angle: 10 claims, Q1 and Q2 first. 24 searches before the search budget ran out. Also re-read the schema's loyalty and campaign-AI tables. |
| `RUN/hosts.json` | The first sweep's re-test of every candidate host, one try each. |
| `RUN/check.json` | Check step: 147 verdicts (101 claims, 4 not-found sets, and hosts) and 15 recorded contradictions. It re-fetched four schemas and could not re-run any search. |

### 1.2 Hosts tried and reached

Each host was tried once with `curl` through the session's proxy (UTC, 2026-11-10). After the sweep, no refused host was retried by
any agent or any tool.

| Result | Host(s) | Tested by, when | Detail |
|---|---|---|---|
| **REACHED** | `raw.githubusercontent.com` | sweep 19:18:26Z; data 19:43–19:49Z; checker 20:57–21:03Z | `HEAD /` gave 301. GET by exact path gave HTTP 200 for `schema_troy.ron` and `schema_wh2.ron` (fetched by both data and checker) and for `schema_3k.ron` and `schema_wh3.ron` (checker). `schema_pharaoh.ron` gave 404 (a guessed file name, not retried). One web-fetch of `/` returned a 301 to github.com, which was not followed. The host serves files by exact path only. |
| answered, gated | `github.com` | data 19:47–19:48Z | `HEAD /` gave 400. A GET of the schema repository page gave 403: "GitHub access to this repository is not enabled for this session". It was not retried and not routed around, and add_repo was not used. Git was not used against any outside repository (task rule). |
| answered, unused | `gitlab.com` | data 19:47:25Z | `HEAD /` gave 301. No relevant source was identified there. |
| **REFUSED** (35; sweep, `hosts.json`) | developer and publisher: `www.totalwar.com`, `wiki.totalwar.com`, `community.creative-assembly.com`, `forums.totalwar.com`, `support.sega.com`, `www.creative-assembly.com`, `www.sega.com` · stores: `store.steampowered.com`, `api.steampowered.com`, `store.epicgames.com` · community: `steamcommunity.com`, `www.reddit.com`, `old.reddit.com`, `www.twcenter.net`, `tw-modding.com` · wikis: `totalwar.fandom.com`, `en.wikipedia.org` · press and guides: `www.pcgamer.com`, `www.pcinvasion.com`, `www.rockpapershotgun.com`, `www.gamesradar.com`, `www.thegamer.com`, `gamerant.com`, `screenrant.com`, `www.dualshockers.com`, `www.ign.com`, `wccftech.com`, `primagames.com`, `gamepur.com`, `www.gamepressure.com`, `prodigygamers.com` · video: `www.youtube.com` · genre developers: `forum.paradoxplaza.com`, `www.taleworlds.com`, `battlebrothersgame.com` | 19:18:23–19:26:46Z | curl exit 56, "CONNECT tunnel failed, response 403". Where the proxy's failure list still held the entry, it recorded `connect_rejected`, "policy denial or other refusal". |
| **REFUSED** (8; data angle) | `www.nexusmods.com`, `catalogue.smods.ru`, `docs.google.com`, `gist.githubusercontent.com`, `web.archive.org`, `www.moddb.com`, `illwiki.com`, `eu4.paradoxwikis.com` | 19:47–19:55Z | Same refusal. |
| **REFUSED** (46; community angle) | `www.pcgamesn.com`, `www.redbull.com`, `www.gamewatcher.com`, `games-guides.com`, `www.gameshedge.com`, `explorminate.org`, `saveorquit.com`, `techraptor.net`, `gamingbolt.com`, `www.nme.com`, `segmentnext.com`, `www.gry-online.pl`, `en.namu.wiki`, `tvtropes.org`, `www.eurogamer.net`, `www.gamespot.com`, `gamefaqs.gamespot.com`, `www.polygon.com`, `www.vg247.com`, `www.shacknews.com`, `www.gamereactor.eu`, `forums.pcgamer.com`, `guides-factory.com`, `steamah.com`, `steamsolo.com`, `gameplay.tips`, `bbs.nga.cn`, `tieba.baidu.com`, `www.bilibili.com`, `forum.imtw.ru`, `www.playground.ru`, `stopgame.ru`, `wireframe.raspberrypi.com`, `www.gamersky.com`, `www.3dmgame.com`, `www.windowscentral.com`, `www.destructoid.com`, `www.resetera.com`, `www.gamerguides.com`, `devtrackers.gg`, `www.gamebanshee.com`, `www.vice.com`, `www.neoseeker.com`, `twdb.io`, `wiki.twcenter.net`, `www.tweaktown.com` | 20:11:22–20:12:09Z | Same refusal. The checker's live read of the proxy status at 21:07:22Z still listed `connect_rejected` for the last 19 of these. |
| **REFUSED** (1; targeted angle) | `chadvandy.github.io` (re-hosted script documentation for this title and its siblings) | 20:45:00Z | Same refusal, and the proxy recorded `connect_rejected`. Copies of its pages were not fetched from raw.githubusercontent.com, because that would route around a refused host. |
| not re-tested in this run | `r2encv2.totalwar.com` and `www.iskmogul.com` (earlier pass only) | earlier pass | r2encv2 got a proxy `connect_rejected` 403 at 10:05:00Z. For iskmogul the earlier pass's fetch tool reported it could not resolve the host. |

Totals: **93 distinct hosts were tested in this run. Three answered (one useful), and 90 were refused at the proxy.**

The tools behaved as follows:

- **Web search** runs outside the container. It answered about 197 queries: about 70 official, 23 data, 80 community and 24
  targeted. After that, the per-turn budget of 200 calls, shared by every agent, was used up. Two later targeted queries and the
  checker's only query were refused for that reason.
- The search tool itself refused two searches restricted to reddit.com: "The following domains are not accessible to our user
  agent: [reddit.com]".
- **Web fetch** was used once, on `raw.githubusercontent.com/`. There was no other reachable host with pages to read.

### 1.3 What kinds of source were and were not available

**Available.**

1. The open-source pack-file tool's schema of the reference's database. For each key-value table it holds the row keys, each with
   an in-house description, and it holds every table's field definitions. It holds **no values**.
2. The same tool's schemas for three siblings: WARHAMMER II, THREE KINGDOMS and WARHAMMER III.
3. Web-search summaries with their result URLs. These are written by a model about the result pages, and no page behind them was
   read.

**Not available.**

- Any page of the developer, the publisher, a store, a forum, the press, a wiki, a video site, a mod site or a script-documentation
  mirror. Every one was refused at the proxy.
- The title's official Assembly Kit, which holds raw data with values and the scripts (official-38, WEAK). It is served only from
  refused hosts.
- Any packed game table with values, the localisation files (so no tooltip text), the script source, and the rows of any effect
  bundle.
- Reddit, which is refused both at the proxy and by the search tool.
- Video content; only titles and short snippets came through search.

**What follows from this.**

- CODE is definitions only, and no value, sign or use of any constant in the reference is known.
- There is no FETCH claim.
- Every SEARCH claim rests on one or a few summaries of pages that could not be opened. Several summaries of one page are not
  independent sources.
- The game versions behind wikis and guides are mostly unknown, roughly 2020 to 2021.
- Absences are absences in definitions and in summaries, not proof that a mechanic does not exist.
- No game file was read in this run either. The CODE evidence is a third-party schema, and its descriptions are only INFERRED to
  come from the developer's data.

### 1.4 What this dossier step did itself

- Read all six inputs in full, and read the angles' search logs wherever a claim needed context.
- Re-read the local schema copies without any network access:
  - re-computed the SHA-256 of all six local copies; all match the recorded hashes;
  - listed all 129 battle-morale keys and all 36 military-force morale keys with their descriptions
    (`RUN/work-dossier/kv_morale_troy.tsv`, `RUN/work-dossier/campaign_vars_troy.tsv`, `RUN/work-dossier/kv_rules_troy.tsv`);
  - checked 56 strings by exact match: each of the 55 quoted strings was found as quoted, and the one absence check
    (`autoresolver_minimum_morale_loss` in S-TROY) found no occurrence, as expected;
  - compared the night-battle setup descriptions across all four schemas;
  - confirmed that `autoresolver_minimum_morale_loss` and `enemy_morale_penalty_*` are defined only in siblings.

  The scripts are `RUN/work-dossier/scripts/spot.py` and `RUN/work-dossier/scripts/kv.py`. They were run with `python3 -I`, from
  outside the download directories.
- Ran no web search, no fetch and no host test, and did not use git.
- Applied the confidence rubric, and used as evidence only claims whose verdict is CONFIRMED or WEAK.

### 1.5 Source abbreviations for CODE

| Abbreviation | File (Rusted PackFile Manager schema, MIT) | Bytes | SHA-256 | Fetched (UTC) |
|---|---|---:|---|---|
| **S-TROY** | `https://raw.githubusercontent.com/Frodo45127/rpfm-schemas/master/schema_troy.ron` (the reference) | 6,752,098 | `8332875a61825d0c67516ccbd155ab9c38c52f628e1ab664f91074cfbffa45e5` | 19:43:31Z (data) and 20:57:37Z (checker), byte-identical. Same hash as the earlier pass's copy (morale proposal, Annex A.1). |
| **S-WH2** | `…/schema_wh2.ron` (WARHAMMER II, 2017; sibling) | 6,067,928 | `54efb0ffb49839c2e280fdeeb1dfd0bfb209e05345334930b2985530ba313580` | 19:49:39Z and 20:57:37Z |
| **S-3K** | `…/schema_3k.ron` (THREE KINGDOMS, 2019; sibling) | 8,551,426 | `f09177fa05f981880e18f85654bd375216e3d002fc2af5cd3d21b6f124f7a614` | 21:03:53Z |
| **S-WH3** | `…/schema_wh3.ron` (WARHAMMER III, 2022; sibling) | 11,213,835 | `3766315e79dee4f1997dbe001fbb5a523f70cc250f8df4841ebd8a1d14ec132e` | 21:03:55Z |

The local copies are `RUN/work-check/dl/{troy,wh2,3k,wh3}/schema_*.ron`, `RUN/work-data/dl/rpfm-schema/schema_troy.ron` and
`RUN/work-data/dl/rpfm-schema-wh2/schema_wh2.ron`.

**Line numbers in S-TROY.**

Key lists (under patches, `lookup_hardcoded`):

| List | Line | Keys |
|---|---:|---:|
| `_kv_experience_bonuses` | 158167 | |
| `_kv_fatigue` | 158172 | 29 |
| `_kv_morale` | 158187 | 129 |
| `_kv_naval_morale` | 158192 | 69 |
| `_kv_rules` | 158202 | 207 |
| `_kv_unit_ability_scaling_rules` | 158212 | |
| `cai_task_management_system_variables` | 158238–158240 | |
| `campaign_variables` | 158283 | 1,138 |

Table definitions:

| Table | Line | Note |
|---|---:|---|
| `_kv_fatigue` | 82 | |
| `_kv_morale` | 199 | fields `key` and `value` only |
| `_kv_rules` | 316 | |
| `battle_result_types` | 35454 | |
| `cai_task_management_system_variables` | 62247 | |
| `campaign_ambush_ground_types` | 62846 | |
| `campaign_bonus_value_battle_context_*` | 63696–63945 | |
| `campaign_group_food_effects` | 68094 | |
| `campaign_group_morale_effects` | 69322 | |
| `campaign_group_pending_battle_purchasable_effects` | 69445 | |
| `campaign_group_post_battle_looted_pooled_resources` | 69739 | |
| `campaign_post_battle_captive_options` | 73689 | |
| `campaign_stance_effects_junctions` | 75695 | |
| `effect_bonus_value_loyalty_event_junctions` | 89211 | |
| `ground_type_to_stat_effects` | 101373 | |
| `land_units` | 103202 | field `morale`, I32 |
| `loyalty_event_effects` | 105677 | |
| `loyalty_traits` | 105848 | |
| `unit_fatigue_effects` | 149273 | |

The file writes an apostrophe as `\'`.

---

## 2. The questions

### Q1. Does entering a battle, or simply being in one, cost morale in the reference?

**The answer in plain words.** We found nothing showing that the reference charges morale for entering a battle or for simply being
in one, at either level, and we found nothing that rules it out.

1. **In the reference's own data definitions, battle morale is computed.** It has a starting point (`morale_base`, "Starting point
   for calculations"). On top of that sit situational terms, and the current value moves toward the computed one each tick
   (`percent_update_per_tick`, "The ideal changed morale amount"; `minimium_increment_update_per_tick`, "The minimum amount that the
   morale will be changed by per tick").
   - No battle-morale key (of 129) and no army-morale key (of 36) is a cost for entering a battle, deploying, the battle's start,
     or time spent in battle.
   - Apart from the starting point, the only terms that apply to every unit of a side whatever the situation are eight difficulty
     modifiers, one set for the player's units and one for the AI's. Their values are unknown.
   - The 69 naval morale keys contain no such cost either.
2. **Every term that can lower morale with no casualties is conditional** (table below). These are definitions, and their sizes
   and signs are unknown.
3. **The campaign can change how a battle starts.**
   - The data defines effect bundles for stances, effects bought before a battle ("pending battle purchasable effects"), and
     campaign morale bonus values that apply in battle by battle context, with a clamp found only in this title.
   - Reports (WEAK) say that units start a battle tired after march-type stances, after a landing, in one scripted battle, and after
     a paid pre-battle action.
   - Further reports (WEAK) say that agents, camp followers, gods, heroes, traits, items, buildings, event choices and the encamp
     stance give conditional morale effects for battles.
   - Whether starting tired lowers morale in this title, and by how much, is not known.
4. **The one trigger found for taking part sits on a commander's campaign stock.**
   - A fan wiki's table of hero motivation traits, as search summaries quote it, says one trait costs 2 motivation whenever the
     hero "Fights a battle in own Territory". Another trait loses motivation when the hero reinforces or is reinforced.
   - Motivation is said to feed the hero's own morale, with no size given (WEAK).
   - This is conditional. It acts on one hero's campaign quantity, not on every unit that enters a battle.
5. **One path in the data could let time in battle lower morale without losses: fatigue.**
   - Activity deltas, including melee combat (`combat`) and the "combat ready pose" (`ready`), feed fatigue levels.
   - Three of those levels carry morale concerns.
   - The signs and values of these deltas are unknown, so whether standing ready or fighting without losses tires a unit, and so
     lowers its morale, is not known.

So, on this evidence, the reference neither supports nor contradicts D-083 DB1. DB1 is our own design, as D-083 DB2 records.

**Sub-answers and confidence.**

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q1-a | The reference's data defines no morale cost for entering, deploying or time in battle, among its 129 battle-morale and 36 army-morale keys | **ESTABLISHED** (definition level) | data-01, data-14 and data.not_found, all CODE CONFIRMED. All 129 keys were re-read by the checker and by this step. |
| Q1-b | Whether the game charges morale for entering a battle or being in one | **UNKNOWN** | It is absent from the definitions and from about 197 search summaries. Values, hardcoded engine behaviour, effect-bundle rows and fatigue-effect rows are not visible. |
| Q1-c | The terms that can act without losses are all conditional | **ESTABLISHED** (definition level) | data-02 (definitions only), data-04, data-05, data-07, data-08, data-09, data-12 |
| Q1-d | Campaign structures exist that can change a battle's starting state: stance effect bundles, pre-battle purchasable effects, and battle-context bonus values with a clamp | **ESTABLISHED** (definition level) | data-20 (CONFIRMED); data-15 (WEAK, but its definitions are confirmed) |
| Q1-e | Campaign stances, a landing, a scripted battle or a pre-battle action set a unit's starting fatigue; campaign effects set conditional battle morale | **UNKNOWN** | official-01 to official-05, community-13, official-22, community-02 to community-04, official-23 (all WEAK) |
| Q1-f | Taking part moves a commander's motivation (two traits), and motivation feeds the hero's morale | **UNKNOWN** | targeted-01, targeted-02, targeted-03 (WEAK); targeted-04 (CONFIRMED CODE, but only the loyalty-event structures; that motivation is loyalty is INFERRED) |
| Q1-g | How a unit's starting morale relates to its base value | **UNKNOWN** | No key or source says. There is a per-unit `morale` field (I32) and a global `morale_base`, and the upper state thresholds are marked "(% basis)" (data-10). No source reports units deploying below base (targeted.not_found). |

**The morale terms the data defines that need no casualties.** All are conditional, and no value is known.

| Term (key: description, S-TROY) | Condition that triggers it | Evidence |
|---|---|---|
| `ume_concerned_surprised`: "Surprised"; `surprise_timeout`: "Surprise timeout" | being surprised, for a limited time. That it applies to the ambushed side is INFERRED, since no key names an ambush. | data-02 |
| `ume_concerned_night_battle_unprepared`: "Unprepared for night battle" | night battles | data-02 |
| `ume_concerned_tired`: "Vigour penalty"; `ume_concerned_very_tired`: "Unit is very tired but not exhausted"; `ume_concerned_exhausted`: "Completely knackered" | a fatigue state, which the campaign may set at the start (reports, WEAK) | data-07 |
| `ume_concerned_flanks_exposed_single` and `_multiple` ("One flank is exposed", "Both flanks are exposed"); `ume_encouraged_flanks_secure` | the line's geometry | data-04 |
| `enemy_numbers_morale_penalty_multiplier`: the penalty for enemy numbers nearby "ranges from -1 to -7 (hardcoded). A value of 0 means no penalty is applied." | enemy numbers within a range | data-04 |
| `was_attacked_in_front`, `_flank`, `_rear`: "First contact from front", "from flank", "from rear" | being engaged. Note that a term exists for frontal contact too; its sign and size are unknown. | data-04 |
| `ume_concerned_attacked_by_projectile`: "Attacked by projectile"; `ume_concerned_attacked_by_artillery`: "Unit was attacked but not hit by artillery"; `ume_concerned_under_friendly_fire` | being shot at, including near misses | data-05 |
| `ume_concerned_unit_frightened`: "The enemy is frightening"; frightened horses and elephants; `ume_concerned_panic` | a frightening enemy nearby | data-09 |
| `ume_concerned_general_dead`, `_died_recently`, `_fled_recently` (each with an AI-only variant) | the commander's fate | data-12 |
| `max_reinforcement_bonus`: "Maximum bonus that can be gained from having pending reinforcements" | reinforcements still to arrive (a bonus) | data-08 |
| `ume_encouraged_on_the_hill`, `ume_encouraged_fortification`, `ume_encouraged_settlement_plaza` | ground and works (bonuses) | data-02, data-04 |
| `difficulty_modifier_player_*` and `difficulty_modifier_ai_*`: "Morale modifier applied to human player's units on hard" (and the other levels) | **every unit of a side**, by difficulty. This is the only unconditional, side-wide term. | data-01 |

**The night-battle setup terms (campaign variables) are muddled in this title.** By key name, a night battle starts with a fatigue
penalty for the attacker and a morale bonus for the defender: `unit_battle_setup_night_battle_attacker_fatigue_penalty` and
`unit_battle_setup_night_battle_defender_morale_bonus`. The reference's own descriptions disagree with those names:

- the attacker key reads "(IN GAME) Fatigue penalty for units during autoresolve night battles. Higher number means higher penalty.";
- the defender-morale key reads "(IN GAME) Fatigue penalty for all attackers in a night battle.".

In WARHAMMER II the two descriptions are swapped. In THREE KINGDOMS and WARHAMMER III they match the key names, and the defender
key there reads "Morale bonus for units defending night battles. Result will be morale + THIS." This step re-read all four schemas.
So whether the attacker's fatigue applies to fought battles or only to auto-resolved ones is not established (data-02, data-24;
check contradiction 2). Even read by its name, the morale term is a **bonus for defenders**, not a cost.

**Evidence rows for Q1 (reference only).** Sibling rows are in §4 (official-29, official-30, community-15, community-16, data-27,
and the auto-resolve observation). Genre rows are in §3 (official-40, official-41, community-19, community-20).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-01 | 129 battle-morale keys describe a computed model: a starting point plus situational effects, moving toward the computed value each tick. None is an entry or time-in-battle term. The only unconditional side-wide terms are the difficulty modifiers. | CODE | reference | S-TROY l.158187 (`_kv_morale`); table def l.199 (key, value) | CONFIRMED |
| data-14 | A 36-key military-force morale family: battle won and lost (each for some turns), casualties multiplier, idle, and others. No "battle fought" key. Inherited (identical in WH2); use not established. | CODE | reference | S-TROY l.158283; `campaign_group_morale_effects` l.69322 | CONFIRMED |
| data.not_found | No entry-cost key among the 129 and 36. No values anywhere in the file. No "glory", "mood" or "motivation" string. No wound key. No ambush-detection key. | CODE | reference | S-TROY | CONFIRMED |
| data-02 | The terms that act before any loss are conditions: Surprised with its timeout, night unpreparedness, fatigue, flanks, enemy numbers, uphill, fort and plaza, pending reinforcements. Plus the night-setup variables. | CODE | reference | S-TROY l.158187, l.158283, l.63906 | WEAK (definitions confirmed; the night-setup reading rests on key names against this title's own texts; "Surprised" as the ambushed side's term is INFERRED) |
| data-04 | First contact from front, flank or rear; flanks exposed or secure; enemy numbers nearby (-1 to -7 hardcoded, times a multiplier); uphill; fort and plaza | CODE | reference | S-TROY l.158187, l.158202 | CONFIRMED |
| data-05 | Attacked by projectile; artillery near miss and hit; friendly fire | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-07 | Fatigue concerns for tired, very tired and exhausted; a fatigue-effects table that multiplies any unit stat per fatigue level | CODE | reference | S-TROY l.158187, l.149273 | CONFIRMED |
| data-17 | Fatigue deltas, including melee combat, the "combat ready pose" and idle; seven fatigue levels | CODE | reference | S-TROY l.158172 | CONFIRMED |
| data-15 | A clamp found only in this title on `morale_mod_mult_capped` basic and battle-context bonus values; battle-context junctions | CODE | reference | S-TROY l.158283; l.63696–63945 | WEAK (definitions confirmed; reading it as "the combined multiplier of all campaign morale" is the finder's) |
| data-20 | Stance effect bundles; pending-battle purchasable effects | CODE | reference | S-TROY l.75695, l.69445 | CONFIRMED |
| targeted-04 | Loyalty event effects and a loyalty-trait pool found only in this title; that motivation is loyalty is INFERRED | CODE | reference | S-TROY l.105677, l.105848, l.89211 | CONFIRMED (the mapping stays INFERRED) |
| official-01 | 1.1.0: Troy's garrison starts the Odysseus' Ruse night battle "very tired" (one scripted battle) | SEARCH | reference | mirrors of `https://totalwar.com/?p=11792`: `https://www.gamewatcher.com/news/total-war-saga-troy-1-1-0-update-patch-notes`, `https://games-guides.com/a-total-war-saga-troy-update-1-1-0-patch-notes-september-9/` | WEAK |
| official-02 | 1.5.0 fix: "negative fatigue" applied to armies attacked in Full Speed stance while standing in water | SEARCH | reference | `https://totalwar.com/?p=13265` | WEAK |
| official-03 | 1.7.0: the Griffin Hunt stance's reduced fatigue now applies | SEARCH | reference | `https://www.totalwar.com/?p=16377` | WEAK |
| official-04 | Players: avoid long marches so troops start the battle "fresh"; units exhausted after landing and being attacked | SEARCH | reference | `https://forums.pcgamer.com/threads/a-total-war-saga-troy.121430/`; `https://steamcommunity.com/app/1099410/discussions/0/521957463060216705/`; `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` | WEAK |
| official-05 | Camp followers' paid actions before battle can make all enemy units tired when it starts, and disable towers | SEARCH | reference | `https://store.steampowered.com/app/1553060/`; `https://support.sega.com/hc/en-us/articles/4417716670993-A-TOTAL-WAR-SAGA-TROY-RHESUS-MEMNON-FAQ`; `https://www.pcinvasion.com/total-war-saga-troy-memnon-guide-pharaohs-servant-and-camp-followers/2` | WEAK (sources disagree on which follower) |
| community-13 | Fatigue from a forced march persists in battle (one thread; may be a bug) | SEARCH | reference | `https://steamcommunity.com/app/1099410/discussions/0/3038229380328782252/` | WEAK |
| data-24 | Night attacks are a hero-skill option (Veil of Nyx) | SEARCH | reference | `https://www.gamepressure.com/total-war-saga-troy/aeneas/z2d7f1` | WEAK (its "the start differs by side" reading conflicts with this title's texts) |
| official-06 | 1.2.0: unit-morale effects shown as battle effects on the Character Details screen | SEARCH | reference | `https://totalwar.com/?p=11982` | WEAK |
| official-22 | Priestess actions: -5 % morale of all enemy units after a successful action against a foreign army | SEARCH | reference | `https://totalwar.fandom.com/wiki/Priestess/Skills` | WEAK |
| community-04 | Rumormonger: -5 % morale of all enemy units in the region | SEARCH | reference | `https://www.pcinvasion.com/total-war-saga-troy-memnon-guide-pharaohs-servant-and-camp-followers/2` | WEAK |
| community-02 | The encamp stance "increase[s] morale in foreign territories" (one fan guide) | SEARCH | reference | `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` | WEAK |
| targeted-01 | Motivation traits: Adventurer -2 "when Fights a battle in own Territory"; Competitive loses motivation when reinforcing or reinforced; Valorous +1 for a win, -2 for a loss | SEARCH | reference | `https://totalwar.fandom.com/wiki/Hero_(Total_War_Saga:_Troy)` | WEAK |
| targeted-02 | Motivation feeds the hero's morale (no size); no desertion even at -10; faction leaders have no motivation | SEARCH | reference | same page; NamuWiki hero page; `https://www.geeksundergrace.com/gaming/review-a-total-war-saga-troy/` | WEAK |
| targeted-03 | 1.7.0 names "motivation traits" (a fix for traits limited to one side) | SEARCH | reference | `https://www.totalwar.com/?p=16377` | WEAK |
| community-01 | In 80 community searches, no player, guide, review or video describes an entry cost | SEARCH | reference | Steam threads `…/0/4198997399065201249/` and `…/0/3808409963273518364/`; `https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/ATotalWarSagaTroy` | WEAK (provenance mis-cited: "C38" and one quote, §6) |
| official.not_found | No official source describes an entry cost | SEARCH | reference | official log | WEAK |
| community.not_found | The same for community sources. A NamuWiki line, "joining an army" lowering morale by 5 %, could not be placed. | SEARCH | reference | community log C2, C76 | WEAK |
| targeted.not_found | 24 searches in the game's interface terms found no entry cost and no unit deploying below base | SEARCH | reference | targeted log T1–T24 | WEAK |

**Contradictions bearing on Q1.**

- The flat statements that nothing was found (community-01; official.not_found, Q1) need the qualification of targeted-01 and of the
  unplaced NamuWiki line (check contradiction 4). This is not a direct contradiction: both are conditional and act on a hero's stock.
- The night-setup texts against the key names, as described above (data-02, data-24; check contradiction 2).
- A tension, not a contradiction: official-04 says an army tires "if you attack that round", but the fan guide's stance list says
  March "removes the ability to start battles" (community-13 notes). If so, an army tired by March would meet battle only when
  attacked (INFERRED).

**What remains unknown for Q1.**

- Every value: the starting point, the per-tick rate, the difficulty modifiers, and each concern's size and sign. This includes
  `was_attacked_in_front`.
- Whether a unit's morale at deployment equals its computed value or moves toward it from some other start.
- The signs of the `ready`, `combat` and `idle` fatigue deltas.
- Whether this title's fatigue-effects table has morale rows.
- Whether "Surprised" applies to an ambushed side, or to an army caught on the march, and for how long.
- The rows of stance effect bundles and of the effects bought before a battle; hardcoded engine behaviour; the tooltip text.
- Whether starting tired lowers morale here, and by how much.
- The size of the motivation-to-morale link, whether it reaches the army beyond the hero's own unit, and the full list of motivation
  traits.
- What NamuWiki's "joining an army" line refers to. The targeted angle suggests, untested, that it may be the Competitive trait's
  dislike of reinforcement.

---

### Q2. Is there an army-wide morale on the campaign map, or an equivalent stock?

**The answer in plain words.** No source shows an army-wide morale number on the reference's campaign map: no interface
description, guide, review or patch note found describes one.

**What the data defines.** The reference's data defines a complete army ("military force") morale family of 36 campaign variables:

- a starting value, a minimum and a maximum;
- "low" and "very low" thresholds;
- a bonus for winning a battle, lasting some turns and falling off with the armies' relative strength;
- a penalty for losing one, lasting some turns;
- a penalty multiplied by the army's level of casualties;
- terms for being idle, being on foreign soil, being led by the faction leader, the faction leader's death, looting, raiding,
  released captives, transfers, bankruptcy and attrition;
- a morale-based rebellion, and the predecessor's "Waaagh".

A table also maps a campaign group and a level key to an effect bundle. The whole family is inherited. It has identical keys and
texts in WARHAMMER II, is also in WARHAMMER III (36 keys) and THREE KINGDOMS (35), and nothing shows that this title uses it. The
file holds no value, no row of the level table is visible, and no source mentions such a number. Its "(IN GAME)" tag is weak evidence
of use, because it also marks the Waaagh keys and variables described as "Outdated" or "No effect" (data-14).
**Definitions ESTABLISHED; use UNKNOWN.**

**What the sources do describe.** All of the following is WEAK.

- **(a) Conditional morale effects that the campaign carries into battle.** These come from hero skills, traits and items, faction
  mechanics, buildings, divine favour, agent actions against armies, camp followers, event choices and the encamp stance. They are
  conditional on territory, situation, battle type or the hero's state. The data defines the structures for this, and these
  definitions are ESTABLISHED:
  - campaign bonus values that act in battle, filtered by battle type, culture, faction, force status (with an `is_yours` flag),
    campaign ground type, territory status and night-only;
  - a clamp on `morale_mod_mult_capped`, found only in this title;
  - stance effect bundles;
  - effects bought before a battle.
- **(b) Three stocks that battles move.**
  - The Amazons' per-army **Battle Glory** (DLC). The official blog says "each of her armies gains Battle Glory after an engagement";
    guides say it buffs the army's morale and movement.
  - Achilles' **mood**. Guides list "Losing any battle with any army or garrison" among the triggers of his Outraged mood, which two
    guides give +40 % melee attack. So a defeat can make this hero's force stronger rather than weaker.
  - Every generic hero's **motivation**. It moves with wins and losses and, for some traits, with merely fighting in his own
    territory or being reinforced. It is said to feed the hero's morale, with no size given.

  Faction leaders have no motivation, and no hero deserts "even at -10 motivation".

**How it would carry into battle, and how a battle would change it.**

- **Into battle.** If the army family is active, the plausible route is the level-to-effect-bundle table (INFERRED). The effects
  that were found act as "battle effects" (official-06) and, from 1.2.0 on, as "contextual" rather than universal morale effects
  (official-07, one summary).
- **A battle's effect.** By definition there is a win bonus and a loss penalty, each lasting turns, and a penalty scaled by
  casualties; whether these are active is unknown. Reports add Battle Glory gained, the mood moved, motivation moved, and divine
  favour gained or lost by conduct, for instance fleeing loses Ares' favour (WEAK).

**What raises and lowers it, by cause.** The middle column lists definitions, which are CODE (data-14 unless noted) and whose use
is not established. The right column lists reports, all WEAK.

| Cause | Defined in the data | Reported |
|---|---|---|
| Victory | `military_force_morale_battle_won` "Morale bonus from winning a battle"; `_battle_won_turns`; `_battle_won_relative_strength_mod` "Modifier on rate at which morale recieved from winning a battle falls off based on the relative stength of the armies involved" | Battle Glory after an engagement (official-18). Valorous +1 motivation for a win, and Adventurer +2 for a win in foreign territory (targeted-01). The trait "Widely Known", "+3/6/9 to morale of all units in Hero's aura", gained by winning many battles (targeted-09). |
| Defeat | `military_force_morale_bettle_lost` "Morale penalty for loosing a battle"; `_battle_lost_turns` | Valorous -2 motivation (targeted-01). Achilles toward Outraged (community-06). The trait "Hare-hearted" from defeats in defensive battles (targeted-09). Fleeing loses Ares' favour (official-21). |
| Casualties | `_casualties_multiplier` "Morale penalty multiplied for level of casualties with army"; `_decimated` with turns | A unit of Achilles' army destroyed pushes him toward Grieving (community-06) |
| Food and supplies | `food_shortage_system_morale_mod_cap` "Defines maximum morale mod bonus for negative food effects" (a cap only); `campaign_group_food_effects` (data-16) | No source ties food to morale in this title; food upkeep is the main limit on armies (community-05) |
| Attrition | `military_force_morale_attrition` "Morale modifier due to attrition" | Unpaid upkeep causes attrition, and army numbers "rapidly dwindle"; Hera's favour causes attrition to enemies. No morale link found (community-05). |
| Upkeep | `military_force_morale_bankruptcy` "Morale penalty while bankrupt" | as above |
| Leaders and heroes | `_kings_own` "Morale bonus from being lead by the faction leader"; `_faction_leader_death` with turns; `_chance_general_joins_rebellion` | Conditional hero, trait and item morale effects, such as +10 % for a hero's units in his own territory and an army-wide shield effect (official-23). Motivation feeds the hero's morale (targeted-02). |
| Events | none | MYTHOS dilemma outcomes, e.g. "+20% morale; fatigue reduction" (community-03) |
| Stances and location | `_idle` "Morale bonus for being idle" (sign and value unknown); `_foriegn_soil` "Morale penalty for army being on foriegn soil"; stance effect bundles (data-20) | Encamp "increase[s] morale in foreign territories" (community-02); march-type stances cause fatigue (Q1) |
| Raiding and looting | `_raiding`; `_looted_settlement_base`, `_multiplier`, `_turns` | Battle Glory also comes from razing (official-18) |
| Captives | `_released_captives_turns` | Ares' favour for killing captives (official-21); Battle Glory from killing captives (official-18) |
| Agents and others | none | Priestess actions against armies (official-22); Rumormonger (community-04) |
| Gods | none | Ares: a morale bonus for sword and axe units, lower morale for enemy or invading armies; Athena at level 3: a hero's aura gives morale (official-20, official-15) |
| Buildings | none | 1.3.0: region buildings give a morale effect instead of recruitment rank; 1.7.0: League mechanics' morale effects changed (official-24) |

**Sub-answers and confidence.**

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q2-a | The data defines an army-wide morale stock, with win and loss terms lasting turns and a level-to-effect-bundle table | **ESTABLISHED** (definition level) | data-14, data-16 (CONFIRMED); lineage in data-21 (§4) |
| Q2-b | The reference uses, or shows, an army-wide morale on the campaign map | **UNKNOWN** | No source shows one. The definitions are inherited, and inherited lists also carry dead content (data-14, data-21). |
| Q2-c | Structures exist that carry campaign morale effects into battle | **ESTABLISHED** (definition level) | data-15 (WEAK verdict, definitions confirmed), data-20, data-14 (level table) |
| Q2-d | What raises and lowers the morale-like campaign quantities actually reported | **UNKNOWN** | The table above; reports WEAK only |
| Q2-e | How a battle changes campaign morale in play | **UNKNOWN** | Definitions only (data-14); reports WEAK (official-18, official-21, community-06, targeted-01, targeted-09) |

**Evidence rows for Q2 (reference only).** Sibling rows are in §4 (data-21, data-25, data-27, official-33, official-34,
targeted-06). Genre rows are in §3 (official-41, community-19). targeted-05 is CONTRADICTED and is listed in §6 only.

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-14 | 36-key military-force morale family plus a level-to-effect-bundle table; inherited; use not established | CODE | reference | S-TROY l.158283, l.69322 | CONFIRMED |
| data-16 | Food-shortage morale-modifier cap and replenishment cap (inherited); a table of food effects | CODE | reference | S-TROY l.158283, l.68094 | CONFIRMED |
| data-15 | Morale bonus-value clamp found only in this title; battle-context junctions | CODE | reference | S-TROY l.158283, l.63696–63945 | WEAK (definitions confirmed) |
| data-20 | Stance effect bundles; pending-battle purchasable effects | CODE | reference | S-TROY l.75695, l.69445 | CONFIRMED |
| targeted-04 | Loyalty event effects and loyalty-trait pool (motivation as loyalty is INFERRED); `xp_gain_mod_from_loyalty` | CODE | reference | S-TROY l.105677, l.105848, l.158283 | CONFIRMED |
| data.not_found | No "glory", "mood" or "motivation" key; no values | CODE | reference | S-TROY | CONFIRMED |
| official-06 | Morale effects shown as battle effects (1.2.0) | SEARCH | reference | `https://totalwar.com/?p=11982` | WEAK (its INFERRED "no campaign stock" note is overtaken, §6) |
| official-07 | "Universal morale effects of 16 ancillaries and 1 trait" replaced by contextual ones (1.2.0) | SEARCH | reference | `https://games-guides.com/a-total-war-saga-troy-update-1-2-0-patch-notes-september-25/` | WEAK (one summary, plus one partial) |
| official-18 | Battle Glory: per army, gained after an engagement; guides say it buffs morale and movement | SEARCH | reference | `https://totalwar.com/?p=11899`; `https://www.pcinvasion.com/total-war-saga-troy-penthesilea-amazons-guide/2` | WEAK |
| official-19 | Achilles' mood moves with combat performance; Outraged's effects | SEARCH | reference | `https://www.pcinvasion.com/total-war-saga-troy-achilles-guide/2` | WEAK |
| community-06 | Mood triggers include "Losing any battle with any army or garrison" (toward Outraged) and a unit destroyed (toward Grieving) | SEARCH | reference | same guide; `https://www.gameshedge.com/total-war-saga-troy-achilles-guide/` | WEAK |
| official-20 | Divine favour: faction-wide battle and morale effects (Ares) | SEARCH | reference | `https://totalwar.com/?p=15330`; `https://totalwar.fandom.com/wiki/Ares` | WEAK (+20 % against +40 % conflict) |
| official-21 | Fleeing loses Ares' favour; killing captives gains it (designer interview) | SEARCH | reference | `https://wccftech.com/total-war-saga-troy-interview-with-lead-designer-todor-nikolov/2/` | WEAK |
| official-22 | Priestess actions lower enemy units' morale | SEARCH | reference | `https://totalwar.fandom.com/wiki/Priestess/Skills` | WEAK |
| official-23 | Conditional hero, trait and item morale bonuses (by territory, defence, sieges, the hero's state) | SEARCH | reference | e.g. `https://www.gamepressure.com/total-war-saga-troy/hector/z4d7f3`; `https://totalwar.fandom.com/wiki/Traits_(Total_War_Saga:_Troy)` | WEAK (its closing "none of them a stock" is contradicted, §6) |
| official-24 | Buildings and League mechanics with morale effects (1.3.0, 1.7.0) | SEARCH | reference | `https://totalwar.com/?p=12358`; `https://www.totalwar.com/?p=16377` | WEAK |
| community-02 | Encamp stance: replenish and "increase morale in foreign territories" | SEARCH | reference | `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` | WEAK (one fan guide) |
| community-03 | MYTHOS dilemma outcomes with morale effects | SEARCH | reference | `https://www.pcinvasion.com/troy-mythos-cerberus-guide-total-war/2` | WEAK |
| community-04 | Rumormonger: -5 % enemy morale in the region | SEARCH | reference | `https://www.pcinvasion.com/total-war-saga-troy-memnon-guide-pharaohs-servant-and-camp-followers/2` | WEAK |
| community-05 | Unpaid upkeep causes attrition; no morale link found | SEARCH | reference | `https://www.vice.com/en/article/why-total-war-saga-troy-is-about-the-wrong-war` | WEAK |
| targeted-01, targeted-02, targeted-03 | Motivation: battle triggers by trait; it feeds the hero's morale; motivation traits are official | SEARCH | reference | Q1 rows | WEAK |
| targeted-09 | Traits from battle records: Widely Known, Hare-hearted, Adventurer, and Reckless from a "Cadmean Victory" | SEARCH | reference | `https://totalwar.fandom.com/wiki/Traits_(Total_War_Saga:_Troy)`; `https://gamingtrend.com/feature/reviews/let-there-be-one-ruler-one-king-total-war-saga-troy-review/` | WEAK (one summary each; two follow-ups failed) |
| community-07 | **A mod, not the base game.** The AGONY overhaul adds a battle-driven campaign scale with military buffs at its middle level | SEARCH | reference (a mod of it) | `https://steamcommunity.com/sharedfiles/filedetails/changelog/2608733972` | WEAK |
| official.not_found, community.not_found | No army morale number found; the Q2 lines are valid for those angles only (check) | SEARCH | reference | logs | WEAK |

**Contradictions bearing on Q2.**

- **Three statements against several sources** (check contradiction 3). The statements are official-23's closing generalisation (on
  the campaign side morale exists "only as conditional percentage or point effects, none of them a stock"), official-06's INFERRED
  note, and official.not_found's Q2 line. Against them stand Battle Glory, Achilles' mood and motivation (all WEAK), and the army
  family defined in the data (CODE; use unknown). This dossier resolves it as follows: no source shows an army morale number;
  faction-mechanic and hero-level stocks are reported (WEAK); and an army stock is defined, but its use is unknown.
- **targeted-05 against S-3K and S-WH3** (CONTRADICTED, §6). The campaign AI's thresholds on "a forces morale" are inherited, not
  found only in this title.
- **Ares' sword and axe bonus** is +20 % in one source and +40 % in another (official-20).
- **A tension:** "no desertion, even at -10 motivation" (targeted-02) sits beside the inherited key for a "morale based rebellion"
  (data-14). This fits that part of the family being unused (INFERRED). It is not a contradiction.

**What remains unknown for Q2.**

- Whether the family is active, and its values.
- Whether any army morale is shown to the player.
- The rows of the level-to-effect-bundle table.
- Whether food shortage, attrition or bankruptcy lower morale in this title.
- The size of the motivation-to-morale link, and whether it reaches the army.
- Whether the encamp-stance morale claim holds.
- What the title's campaign script interface exposes. Its pages, on a refused host, were not returned by search before the budget
  ran out. A sibling's interface exposes a force morale with a forecast (targeted-06, §4).

---

### Q3. Unit morale in battle: factors, sizes and states

**The answer in plain words.** By the data's definitions, each unit's battle morale is a computed value with many situational terms
(table below), and seven threshold states:

> impetuous, eager, confident, steady, wavering, shaken, broken

The descriptions mark impetuous, eager, confident and the top of steady "(% basis)". A wavering unit stays wavering for a base time.
The definitions are ESTABLISHED. The threshold values and every size are UNKNOWN; the only number in the descriptions is the
hardcoded range from -1 to -7 for enemy numbers nearby.

The reports (all WEAK) are these:

- each unit type has a base morale in the tens, which the patches tune;
- flank attacks damage morale and cut defence, although one player and one mod author say the flank penalty misfires;
- a hero's death hits the whole army hard;
- mythical units cause fear;
- hero abilities move morale by fixed amounts, for a limited time, within an area;
- missile fire carries an "under fire" penalty, in players' words;
- low-tier units rout very easily, according to reviews at launch.

**Factors.** Definitions are CODE, with no sizes. Reports are WEAK.

| Factor asked | Defined in the data (S-TROY `_kv_morale` l.158187 unless noted) | Reported sizes and behaviour |
|---|---|---|
| Casualties, total and recent | three windows: "Unit lost 6%…50% hitpoints/entities in last 4 sec" (6, 10, 15, 33, 50); "…in last 60 sec" (10, 15, 33, 50, 80); "…in total" (10 to 90 in steps of 10). A "morale shock" threshold on losses in 4 s; a switch between hitpoints and entities (data-03) | A mod for this title lowers the recent and raises the total penalties (data-22) |
| Flank and rear | "First contact from front", "from flank", "from rear"; flanks exposed single or multiple; "All flanks are secure"; ranges for neighbours and open flanks; the rules multiply melee defence when attacked in flank or rear (data-04) | A preview says flank attacks "damage morale and reduce the targeted unit's defence stats" (official-10). One player says surrounded units "refuse to route until they are almost destroyed" (community-09). A mod author says the flank penalty "often applies to frontal combat" (data-22). |
| Missile fire | "Attacked by projectile"; artillery near miss and hit; friendly fire (data-05) | "the morale penalty too when under missile fire" (a player, community-08); the 1.6.0 "Serrated Edges" modifier lowers affected enemies' morale (official-13, one summary) |
| The general's presence or aura | "Base radius of the general's passive aura"; an inspire effect scaled from min to max by command stars, full inside the radius and fading out to radius × modifier; a "commanding general alive" effect; "Friendly is having an inspiring effect on you"; a flat, distance-adjusted inspire from non-command units, "NOT based on rank" (data-12, data-01) | The aura gives morale when the Cult of Athena is at level 3 (official-15, one summary) |
| A hero's death or wounding | "The general is dead, but not recently", "Very recently the general died", "The commanding general has recently fled", each with an AI-only variant; "Very recently the captain died"; **no key mentions wounding** (data-12) | "If your hero dies all of your army will suffer great morale penalties" (a developer quote whose outlet is not pinned, official-11); one unit's trait exempts it from the hero-death penalty (official-11) |
| Nearby friendly routs | a "UME_CONCERNED_FRIENDS_ROUTING" effect fed by "rout_balance (enemies_routing - friends_routing)" × weighting, with caps on the counts and front and flank distances (data-06) | none for this title |
| Beating an enemy unit | "Unit winning slightly in combat", "winning", "significantly winning"; "unit killed 5%", "7.5%", "12% of enemy in last 4 sec" (data-08) | The earlier pass's "scaling morale buff" (morale proposal Annex A.3, SEARCH; not searched again in this run) matches these terms at definition level (data-08 notes) |
| The battle's overall balance | no "winning or losing the battle" key. "The army is on the brink of destruction", set by two ratios (current to starting strength, enemy to friendly strength); a bonus for pending reinforcements up to a maximum (data-08); a side-level strength modifier of unclear use (checker note on data-08) | A battle script can switch the army-destruction morale effect off (targeted-08, one summary) |
| Fatigue | tired ("Vigour penalty"), very tired, exhausted (data-07) | none links fatigue to morale for this title (official-25, community-13) |
| Ambush or surprise | "Surprised" with "Surprise timeout" (data-02; the ambush link is INFERRED) | not found for this title (§Q6) |
| Fear | ranges for fear and terror. Terror sends a unit into a short or long "morale shock rout" if its current morale is at or below a threshold, followed by immunity to terror from the same enemy for a time. A frightening enemy; frightened horses and elephants; panic; "Savage Terror ability - morale value" (data-09) | Mythical units "cause fear" (a director quote with outlet not pinned, official-12). Savage Roar's reduction went from 30 to 10 (1.2.0, one summary, official-13). |
| Terrain | "Bonus for being up-hill of all enemies"; defending a fort, a breached fort or the central plaza; a penalty scaled by "ratio_of_buildings_destroyed"; a table that multiplies stats by ground type, with morale rows unknown (data-04) | Mud slows heavy infantry and reduces its fighting ability; no terrain morale effect found (official-25) |
| Being charged | no dedicated key. INFERRED: it is covered by the direction of first contact, losing a combat and recent casualties. The charger has a charge bonus with "the timout for morale bonus due to charging" (data-08). | The base game's charge morale window is 90 s (a mod author, two summaries of one text, targeted-07) |
| Fighting cavalry | "Base value (modded by training) against cavalry" (data-04) | none |
| Difficulty | eight difficulty modifiers (data-01) | none |
| Base value | a per-unit `morale` field (`land_units`, I32; checker note on official-08) and the global `morale_base` | 1.4.0: base morale of "all high Tier units" lowered by roughly 5 to 10 %, the Cyclops from 90 to 65; 1.3.0: Initiates from 50 to 45; 1.5.1: Hippotoxotai from 50 to 60 (official-08, effectively one summary) |
| Hero abilities | one title-only ability key carrying a morale value (data-09) | +25 morale to self and +10 % melee defence for 45 s; -25 morale to enemies within 30 m for 60 s; -15 to enemies for 25 s; "unbreakable" for 25 s (one Chinese guide, community-11). Named English skills without numbers (official-14, community-11). |
| Reception | none | "Low-tier units are simply too easy to rout"; one side "will simply crumble" (community-10) |

**Morale states.** There are seven, defined by a lower and an upper threshold each (`ums_*`, data-10). "Routing" and "shattered" are
not among the threshold states; the rout and shatter keys handle them (Q4). The threshold values are unknown. For legacy bands seen
by modders of earlier titles (siblings), see §4, community-17.

**Sub-answers and confidence.**

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q3-a | The factor list and seven states are defined as above | **ESTABLISHED** (definition level) | data-01, data-03, data-04, data-05, data-07, data-08, data-09, data-10, data-12 (CONFIRMED); data-06 (WEAK: only its friendly half is supported) |
| Q3-b | Sizes, thresholds, and what players see | **UNKNOWN** | official-08 to official-14, official-25, community-08 to community-11, data-22, data-23, targeted-07, targeted-08 (all WEAK) |

**Evidence rows for Q3 (reference only).** The CODE rows are those named in Q3-a. Siblings are in §4 (community-17, data-26).
Genre is in §3 (community-20, community-21, official-42).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-03 | Casualty windows and thresholds; shock threshold | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-04 | Contact direction, flanks, enemy numbers (-1 to -7), cavalry, uphill, fort, plaza; rules on defence direction | CODE | reference | S-TROY l.158187, l.158202, l.101373 | CONFIRMED |
| data-05 | Projectile, artillery, friendly fire | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-06 | Rout balance feeding a "friends routing" concern | CODE | reference | S-TROY l.158187 | WEAK ("enemy routs raise morale" is not supported: there is no "encouraged" counterpart) |
| data-07 | Fatigue concerns; fatigue-effects table | CODE | reference | S-TROY l.158187, l.149273 | CONFIRMED |
| data-08 | Winning and losing combat, kill bonus, charge bonus and timeout, army destruction, reinforcements | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-09 | Fear, terror, shock routs, immunity; Savage Terror key | CODE | reference | S-TROY l.158187, l.158212 | CONFIRMED |
| data-10 | Seven threshold states; waver timeout | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-12 | Aura, inspire, general dead, died or fled, captain died; no wound key | CODE | reference | S-TROY l.158187 | CONFIRMED |
| official-08 | Base morale per unit type, tuned in patches (values above) | SEARCH | reference | `https://support.sega.com/hc/en-gb/articles/360015921757-A-TOTAL-WAR-SAGA-TROY-PATCH-1-4-0` | WEAK ("in both" overstated: effectively one summary) |
| official-09 | A morale rating shown under the health bar; morale can fall much faster than health | SEARCH | reference | `https://www.pcgamer.com/total-war-saga-troy-brings-mythical-heroes-to-bronze-age-infantry-warfare/` | WEAK (one summary, not two) |
| official-10 | Flank attacks damage morale and cut defence (preview) | SEARCH | reference | same preview | WEAK |
| official-11 | A hero's death gives the army a large morale penalty | SEARCH | reference | `https://www.gamepressure.com/total-war-saga-troy/battles/z0d813`; `https://totalwar.fandom.com/wiki/Aristomachoi` | WEAK |
| official-12 | Mythical units cause fear | SEARCH | reference | `https://gamingbolt.com/?p=445133`; `https://totalwar.fandom.com/wiki/Ares` | WEAK |
| official-13 | Savage Roar 30 to 10; Prayer to Ares; Serrated Edges | SEARCH | reference | `https://totalwar.fandom.com/wiki/User_blog:Mc.abber/A_Total_War_Saga:_TROY_-_Patch_1.2.0` | WEAK (one summary) |
| official-14 | Hero abilities raise or lower morale (rage-driven command abilities) | SEARCH | reference | `https://www.gamepressure.com/total-war-saga-troy/agamemnon/z3d7f2` | WEAK |
| official-25 | Mud and heavy infantry: speed and fighting ability, not morale | SEARCH | reference | `https://www.totalwar.com/?p=8769` | WEAK |
| community-08 | Players count missile fire as a morale factor | SEARCH | reference | `https://steamcommunity.com/app/1099410/discussions/0/3808409963273518364` | WEAK |
| community-09 | One player's report against a strong flank penalty | SEARCH | reference | Steam threads behind log C7 | WEAK |
| community-10 | Low-tier units rout too easily (reception) | SEARCH | reference | `https://vapingunderground.com/threads/a-total-war-saga-troy-review.544853/` | WEAK |
| community-11 | Ability morale amounts and durations (Chinese guide) | SEARCH | reference | `https://www.3dmgame.com/gl/3815110_20.html` | WEAK (attribution to heroes conflicts) |
| data-22 | Immersive Combat mod: post-rally 60 to 20 s; flank penalty misfires; casualty penalties | SEARCH | reference | `https://catalogue.smods.ru/?p=151662` | WEAK |
| data-23 | One mod author found edits to the morale and fatigue tables had no effect | SEARCH | reference | `https://www.nexusmods.com/totalwarsagatroy/mods/47` | WEAK |
| targeted-07 | Charge morale window of 90 s (mod author) | SEARCH | reference | `https://catalogue.smods.ru/?p=151662` | WEAK |
| targeted-08 | The battle script can toggle the army-destruction morale effect | SEARCH | reference | `https://chadvandy.github.io/tw_modding_resources/Troy/battle/battle_army.html` | WEAK |

**Contradictions bearing on Q3.**

- **Flank morale** (check contradiction 5). The preview and TV Tropes say flanking is especially damaging. One player and one mod
  author say otherwise. The data's term is set by first contact, which could produce both reports (INFERRED).
- **data-06's "enemy routs raise it"** (check contradiction 14). Both weightings feed one concern, and no counterpart exists.
- **Ability attribution:** the same skills are assigned to different heroes in different guides (community-11; check contradiction
  9).
- **Whether mods can change these tables:** one author says edits "take no effect" (data-23), while another mod edits them (data-22).

**What remains unknown for Q3.**

- Every size and every threshold.
- Whether a positive rout balance, with more enemies routing, raises morale.
- Whether the fatigue-effects and ground-type tables carry morale rows.
- Whether the difficulty modifiers are non-zero.
- The in-game tooltip's list of factors.

---

### Q4. Routing, rallying and shattering

**The answer in plain words.** The data defines these rules (data-11; CODE):

- a broken unit goes on to rally after a base timeout, with a time bonus per experience level (read as a shorter wait; the
  checker notes this is a reading of the "time bonus" wording);
- a unit that has just rallied cannot rout again for a set number of seconds;
- a unit always shatters after a set number of routs;
- a unit can shatter already after its first or second rout, once a casualty threshold is passed. The formula is quoted as written,
  "[(THIS * starting men)<current men]", and its meaning is unclear;
- a flag, if set to true, makes the next rout a shatter once the unit's commander has died. The flag's value is unknown;
- terror can send a unit into a short or long "morale shock rout" (data-09);
- routing units have their own melee-defence and speed modifiers, found only in this title and without descriptions;
- pursuers' attacks on routers count as charges.

The values are unknown.

The reports (WEAK) are these:

- by default, a rallied unit cannot rout again for **about 60 s**. Four community and mod-author statements say so. Players call it
  longer than usual in the series ("In most TW titles, this is in the 10-15 second range"), and say mauled units came back and
  fought on;
- a guide says routers stop running if their hero does something exceptional, or if their side destroys an enemy unit;
- "Morale does recover over time though, and a fleeing unit might decide to return to the fight" (a summary).

Whether a rally needs the commander nearby was not found for the reference. ROME II's manual says it does, but that is a sibling
(§4).

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q4-a | The rally, post-rally, shatter and commander-death rules are defined as above | **ESTABLISHED** (definition level) | data-11, data-10, data-09 (CONFIRMED) |
| Q4-b | When and how often routers rally in play; the window's length; what makes a rout permanent in practice | **UNKNOWN** | community-12, data-22, official-27, community-01 (WEAK) |

**Evidence rows for Q4 (reference only).** Siblings are in §4 (official-28, community-17). Genre is in §3 (official-42,
community-20).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-11 | `broken_finish_base_timeout`; `broken_finish_timer_experience_bonus`; `post_rally_no_rout_timer` "Time (in seconds) for which a unit that has just rallied cannot rout again"; `shatter_after_rout_count` "After routing this number of times, units are always shattered"; the shatter thresholds; the commander-death shatter flag; the routing-unit modifiers; pursuit charges | CODE | reference | S-TROY l.158187, l.158202 | CONFIRMED |
| data-10 | `waver_base_timeout` "The base time for which a unit spends in waver" | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-09 | Terror shock routs, short and long; immunity timer | CODE | reference | S-TROY l.158187 | CONFIRMED |
| community-12 | Post-rally no-rout window of about 60 s by default; a mod cuts it to 15 s ("it's 10s in Warhammer2") | SEARCH | reference | `https://steamcommunity.com/app/1099410/discussions/0/3196990234734079048/`; `https://steamcommunity.com/sharedfiles/filedetails/?l=finnish&id=2591709028` | WEAK |
| data-22 | Immersive Combat: post-rally 60 to 20 s; units shatter more often | SEARCH | reference | `https://catalogue.smods.ru/?p=151662` | WEAK |
| official-27 | Routers cannot be controlled; they stop after a hero's exceptional feat or a destroyed enemy unit | SEARCH | reference | `https://www.gamepur.com/guides/how-troop-morale-and-routing-works-in-a-total-war-saga-troy` | WEAK |
| community-01 | Morale recovers over time; a fleeing unit may return | SEARCH | reference | `https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/ATotalWarSagaTroy` | WEAK |

**Contradictions bearing on Q4.** The series comparison of post-rally windows is mixed (check contradiction 6): "10-15 s" in most
titles and "10s in Warhammer2", against a WARHAMMER mod that shortens its equivalent from 60 to 45 s and a ROME II mod that sets the
timer to 60 s.

**What remains unknown for Q4.** All the timers and counts; whether a rally needs the commander nearby, or needs no enemy close by;
and how often units rally in a typical battle.

---

### Q5. How a won or lost battle feeds back to the campaign

**The answer in plain words.** The data defines several channels from a battle's result back to the campaign (data-19; CODE):

- **experience** by result type, plus experience bonuses for:
  - capturing a key building or the victory location;
  - being first into an enemy fort;
  - killing the enemy commander (a base amount, plus an amount per command star);
  - killing the faction leader or a family member;
- **captive options** set per campaign group, each with an effect bundle that lasts a number of turns and a resource transaction;
- **replenishment from captives**, between a minimum and a maximum share of the original army, set by difficulty;
- **loot**, as a share of a routed unit's recruitment cost, scaled by casualties and also taken from killed agents;
- **looted pooled resources**, set per campaign group;
- **a chance to steal an item**, by victory grade, with a modifier for when the item's holder is wounded;
- **interfaction loyalty** by **nine** result grades:

  | Victories | Draw | Defeats |
  |---|---|---|
  | heroic, decisive, close, pyrrhic | draw | valiant, close, decisive, crushing |

  data-19 says eight grades; the ninth, draw, was found by the checker and re-read by this step.

Further definitions: the experience gained is scaled by character loyalty (targeted-04), and the inherited army-morale family has
win and loss terms (data-14; use unknown). No key exists for hero motivation, mood or "glory".

The reports (WEAK) are these:

- a one-time payout of resources per victory, sized by the enemy defeated;
- favour effects that raise the captives taken and the loot after a battle;
- MYTHOS "Souls" earned through options after a battle;
- Ares' favour, lost by fleeing and gained by killing captives;
- Battle Glory after an engagement;
- Achilles' mood moved by battles;
- motivation moved by wins, losses and taking part, by trait;
- persistent traits earned by battle records;
- immortal heroes "wounded instead of killed".

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q5-a | The channels from a battle's result back to the campaign are defined as above | **ESTABLISHED** (definition level) | data-19 (WEAK only for its miscount and an assumed sign, both corrected here), data-14, targeted-04, data.not_found (CONFIRMED) |
| Q5-b | What each channel does in play, and its sizes | **UNKNOWN** | official-16, official-18, official-19, official-21, official-37, community-06, targeted-01, targeted-09 (WEAK) |

**Evidence rows for Q5 (reference only).** The sibling row is in §4 (official-31, THREE KINGDOMS duels).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-19 | Experience per result type and the bonuses; captive options (effect bundle, turns, resource transaction); replenishment caps; loot fractions; item theft; interfaction loyalty by result grade | CODE | reference | S-TROY l.158167, l.158283, l.35454, l.73689, l.69739 | WEAK (nine grades, not eight; "higher if the holder is wounded" assumes a sign) |
| data-14 | Army morale: battle won and lost, each for some turns; released captives; looting | CODE | reference | S-TROY l.158283 | CONFIRMED |
| targeted-04 | `xp_gain_mod_from_loyalty` "(a bonus of (character loyalty * xp_gain_mod_from_loyalty)% for all gained XP)" | CODE | reference | S-TROY l.158283 | CONFIRMED |
| official-37 | Resource payout per victory; favour effects on captives and loot; Souls | SEARCH | reference | `https://www.redbull.com/gb-en/a-total-war-saga-troy-tips-guide`; `https://primagames.com/gaming/total-war-saga-troy-divine-will-guide-gods` | WEAK |
| official-21 | Ares' favour: fleeing loses it; killing captives gains it | SEARCH | reference | `https://wccftech.com/total-war-saga-troy-interview-with-lead-designer-todor-nikolov/2/` | WEAK |
| official-18, official-19, community-06 | Battle Glory; Achilles' mood and its battle triggers | SEARCH | reference | Q2 rows | WEAK |
| targeted-01 | Motivation by trait: wins, losses, fighting in own territory, reinforcement | SEARCH | reference | Q1 row | WEAK |
| targeted-09 | Traits from battle records | SEARCH | reference | Q2 row | WEAK |
| official-16 | Immortal heroes "wounded instead of killed" (1.4.0) | SEARCH | reference | `https://support.sega.com/hc/en-gb/articles/360015921757-A-TOTAL-WAR-SAGA-TROY-PATCH-1-4-0` | WEAK |

**What remains unknown for Q5.** Every value; the effects of killing, releasing or enslaving captives outside the Amazons;
experience per result in practice; and whether any army-wide morale changes after a battle.

---

### Q6. Ambush

**The answer in plain words.**

- **The roll.** The data defines a campaign ambush as a roll: "Ambush will roll on chance of success, generated from ground type and
  bonus values". A set share of the failures is a critical failure. Each campaign ground type carries an `ambush_chance_of_success`,
  with a column default of 0; the values are unknown. Ambushing characters get their own zone-of-control colours on the map
  (data-18).
- **Detection.** No key or table for detection exists (data.not_found).
- **In battle.** The only candidate morale term for the ambushed side is "Surprised", with its timeout, and the link to ambushes is
  INFERRED (data-02).
- **Reports** (WEAK):
  - the army Ambush stance conceals the army and disables its movement; an enemy passing through is flanked and starts the battle
    surrounded (official-36);
  - two hero stances turn a battle into an ambush, and one skill improves ambush success and evasion (official-35).
- **A morale penalty for the ambushed side** was not found for the reference. Siblings have one (§4).

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q6-a | The ambush roll, the terrain chances and the display are defined; there is no detection key; "Surprised" is the only candidate battle term | **ESTABLISHED** (definition level) | data-18, data.not_found (CONFIRMED); data-02 (WEAK verdict, definitions confirmed) |
| Q6-b | How an ambush is set up and detected in play; what the ambushed side suffers, including any morale penalty | **UNKNOWN** | official-35, official-36, targeted.not_found (WEAK) |

**Evidence rows for Q6 (reference only).** Siblings are in §4 (official-29, official-32, community-18, data-28).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-18 | `ambush_critical_fail_proportion_of_fail` "(IN GAME) Ambush will roll on chance of success, generated from ground type and bonus values." `campaign_ambush_ground_types.ambush_chance_of_success`; zone-of-control colours | CODE | reference | S-TROY l.158283, l.62846 | CONFIRMED |
| data.not_found | No ambush-detection key or table | CODE | reference | S-TROY | CONFIRMED |
| data-02 | "Surprised" and "Surprise timeout" | CODE | reference | S-TROY l.158187 | WEAK (the ambush link is INFERRED) |
| official-35 | Stance: Shadow and Stalk set up ambushes; a skill adds +20 % ambush success and +15 % evasion (one wiki); a 1.3.0 fix | SEARCH | reference | `https://totalwar.com/?p=12358`; `https://www.gamepressure.com/total-war-saga-troy/sarpedon/z8d7f7` | WEAK |
| official-36 | Ambush stance: "Conceal your army but disable your movement"; an enemy passing through starts surrounded | SEARCH | reference | `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` | WEAK |
| targeted.not_found | No source for an ambush or surprise penalty at deployment | SEARCH | reference | targeted log T1, T2 | WEAK |

**What remains unknown for Q6.** The base chances per ground type; the detection rules; whether "Surprised" is applied to the
ambushed side, and for how long; and whether being caught on the march has any effect beyond fatigue.

---

### Q7. Fatigue

**The answer in plain words.**

- **Build-up.** Fatigue is a per-unit quantity changed by deltas for activities, each defined in the data (data-17):
  - walking and running, with heavy or medium cavalry and light cavalry apart;
  - charging, melee combat and shooting;
  - climbing walls and ladders, and working;
  - standing in the "combat ready pose";
  - idling, including in buildings, in heavy rain and in heavy snow;
  - with multipliers for shallow, steep and very steep slopes.
- **Levels.** Thresholds define seven levels: fresh, active, winded, tired, very tired, exhausted, and a maximum.
- **Link to morale.** Three of the levels carry morale concerns (data-07). Separately, a fatigue-effects table can multiply any
  modifiable unit stat per level, with a default of 1; whether it has morale rows is unknown.
- **Recovery.** That idling is the recovery term is INFERRED: the descriptions say only "delta".
- **Rates and the size of the morale effect** are unknown.

Reports (WEAK):

- in the base game, units tire after about 30 s of fighting, and fatigue lowers armour and damage (a mod author);
- mud slows heavy infantry and tires it;
- campaign stances, a landing, a scripted battle and a paid pre-battle action set fatigue at the start (Q1);
- one thread says fatigue from a forced march never recovered during a battle, possibly a bug;
- a modded game took "a few minutes of resting to recover one level of fatigue".

No report for this title links fatigue to morale.

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q7-a | The fatigue model (deltas, slope multipliers, seven levels), the three fatigue morale concerns and the stat table are defined | **ESTABLISHED** (definition level) | data-17, data-07 (CONFIRMED) |
| Q7-b | How fast fatigue builds and recovers; the morale effect's size; carry-over from the campaign | **UNKNOWN** | official-01 to official-05, official-25, official-26, community-13 (WEAK) |

**Evidence rows for Q7 (reference only).** Siblings are in §4 (official-30, community-15, data-26). Genre is in §3 (community-21).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-17 | 29 fatigue keys: activity deltas, slope multipliers, seven thresholds (identical in WH2) | CODE | reference | S-TROY l.158172 | CONFIRMED |
| data-07 | Fatigue morale concerns; `unit_fatigue_effects` (level × stat → value, default 1) | CODE | reference | S-TROY l.158187, l.149273 | CONFIRMED |
| data-02 | Night setup: the attacker fatigue penalty by key name ("during autoresolve night battles" in this title's text) | CODE | reference | S-TROY l.158283 | WEAK |
| official-26 | Units tire after about 30 s of fighting; fatigue lowers armour and damage (Radious mod author) | SEARCH | reference | `https://steamcommunity.com/workshop/filedetails/discussion/2593467734/3038229380331747720` | WEAK |
| official-25 | Mud: speed and fighting ability of heavy infantry; heavy units "easily exhausted" | SEARCH | reference | `https://www.totalwar.com/?p=8769`; `https://techraptor.net/gaming/previews/total-war-saga-troy-preview` | WEAK |
| official-01 to official-05 | Starting fatigue set from the campaign (Q1 rows) | SEARCH | reference | Q1 rows | WEAK |
| community-13 | Fatigue persists in battle after a forced march; recovery of a few minutes per level (modded) | SEARCH | reference | `https://steamcommunity.com/app/1099410/discussions/0/3038229380328782252/` | WEAK |

**What remains unknown for Q7.** Every threshold and delta; the sign of `ready` and of `idle`; whether there are morale rows in the
fatigue-effects table; and whether starting tired caps recovery, as a sibling does (§4).

---

### Q8. Commanders and heroes

**The answer in plain words.**

**The commander's influence.** In the data this is a passive aura (data-12):

- a base radius;
- an inspire effect scaled from a minimum to a maximum by command stars, full inside the radius and fading out to the radius times
  a modifier;
- a separate effect for units whose commanding general is alive;
- a flat, distance-adjusted inspire from non-command units.

**Death, flight and wounding.** The data defines concerns for a general dead "but not recently", one who "died recently" and one who
"has recently fled", each with an AI-only variant, and a concern for a captain who died recently. A flag, if set to true, makes a
unit's next rout a shatter once its commander has died (data-11); the flag's value is unknown. **No key mentions a wounded
commander.**

**This title's own hero mechanics.** These are not morale keys (data-13):

- **Rage** builds in combat: per second, per enemy killed and per 1 % of health lost, with multipliers (charging, fighting heroes,
  low health, losing a melee, just rallied). It decays out of combat after a delay.
- **Aristeia** points come from Rage earned or spent, and from each 1 % of the hero's army that is destroyed. A threshold unlocks
  the hero's Aristeia.
- A general cannot be killed while his unit's surviving share is above a threshold.
- One fear ability carries a morale value (data-09).

Reports (WEAK):

- a hero's death costs his whole army a large amount of morale;
- one patch made the heroes' aura give morale when the Cult of Athena is at level 3;
- hero abilities raise their own side's morale or lower the enemy's for a limited time, by fixed amounts (community-11 for the
  numbers);
- immortal heroes are wounded instead of killed;
- in Historical mode the hero's bodyguard retinue "can continue fighting even if the hero dies or is wounded";
- heroes can challenge each other to duels, but no morale effect of a duel was found for this title;
- motivation feeds the hero's morale, and the trait "Widely Known" gives "+3/6/9 to morale of all units in Hero's aura".

| # | Sub-answer | Confidence | Basis |
|---|---|---|---|
| Q8-a | The aura, inspire, death, flight, captain and shatter-flag terms, and the Rage and Aristeia rules, are defined; no wound term exists | **ESTABLISHED** (definition level) | data-12, data-13, data-11, data-09 (CONFIRMED) |
| Q8-b | Sizes; the wounding effect; abilities and duels in play | **UNKNOWN** | official-11, official-12, official-14 to official-17, community-11, community-14, targeted-02, targeted-09 (WEAK) |

**Evidence rows for Q8 (reference only).** Siblings are in §4 (official-28, official-31).

| ID | Claim (short) | Label | Scope | Source | Verdict |
|---|---|---|---|---|---|
| data-12 | `general_aura_radius` "Base radius of the general's passive aura"; inspire min and max; the general dead, died and fled concerns; captain; no wound key | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-13 | Rage and Aristeia rules (this title only); `general_auto_survive_threshold` | CODE | reference | S-TROY l.158202 | CONFIRMED |
| data-11 | `only_units_with_living_commanding_general_may_enter_non_shattered_rout_enabled` "once a unit's commander dies the next rout will be a shatter" | CODE | reference | S-TROY l.158187 | CONFIRMED |
| data-09 | `troy_savage_roar_morale_amount` "Savage Terror ability - morale value." | CODE | reference | S-TROY l.158212 | CONFIRMED |
| official-11 | Hero death: a large army-wide morale penalty | SEARCH | reference | `https://www.gamepressure.com/total-war-saga-troy/battles/z0d813` | WEAK |
| official-14 | Rage-driven command abilities; Rallying Cry; The Shout; a Menelaus ability that saps morale | SEARCH | reference | `https://www.gamepressure.com/total-war-saga-troy/hector/z4d7f3`; `https://www.gamereactor.eu/total-war-saga-troy-an-epic-interview/` | WEAK |
| official-15 | The aura gives morale when the Cult of Athena is at level 3 (1.1.0) | SEARCH | reference | `https://totalwar.fandom.com/wiki/User_blog:Mc.abber/A_Total_War_Saga:_TROY_-_Patch_1.1.0` | WEAK (one summary, not three) |
| official-16 | Immortal heroes wounded instead of killed | SEARCH | reference | `https://support.sega.com/hc/en-gb/articles/360015921757-A-TOTAL-WAR-SAGA-TROY-PATCH-1-4-0` | WEAK |
| official-17 | Divine Challenge duels | SEARCH | reference | `https://support.sega.com/hc/en-gb/articles/360009441858-A-TOTAL-WAR-SAGA-TROY-FAQ` | WEAK |
| community-11 | Ability amounts and durations | SEARCH | reference | `https://www.3dmgame.com/gl/3815110_20.html` | WEAK |
| community-14 | Historical mode: the retinue fights on after the hero dies or is wounded | SEARCH | reference | `https://www.pcinvasion.com/total-war-saga-troy-mythos-historical-mythological-game-mode/` | WEAK |
| targeted-02, targeted-09 | Motivation feeds the hero's morale; Widely Known's aura morale | SEARCH | reference | Q1 and Q2 rows | WEAK |

**Contradictions or tensions bearing on Q8.** A patch line saying the aura gives morale at one favour level (official-15) sits beside
the data's default aura inspire amounts (data-12). Whether a hero's aura gives morale without that favour level is not established.

**What remains unknown for Q8.** The radius and amounts; the penalties for a general who dies or flees; the effect of a hero's
wounding; the morale effect of a duel in this title; and whether the motivation effect reaches the army.

---

## 3. G: genre patterns (another developer's games; labelled genre; all WEAK; confidence UNKNOWN)

| Game (developer) | Campaign level | Battle level | Cost for taking part? | Evidence |
|---|---|---|---|---|
| Europa Universalis IV (Paradox) | an army morale with a maximum ("max morale"); a disorganised army cannot move until its morale recovers | Its developer diary of 24 May 2022 (1.34), as summarised: "Deployed troops take a daily morale damage of 1% of max morale. Reserves take a daily morale damage of 2% of max morale." Below a threshold a unit is disorganised. | **Yes: a cost per day of being in battle, whatever the casualties.** Before 1.33 a flat daily 0.03 applied. The patch note's wording conflicts: "of the average max morale of the enemy's troops". | official-40, SEARCH, WEAK. `https://admin-forum.paradoxplaza.com/forum/developer-diary/europa-universalis-iv-development-diary-24th-of-may-2022.1527592`; `https://devtrackers.gg/europa-universalis/p/c6b82983-1-34-combat` |
| Mount & Blade II: Bannerlord (TaleWorlds) | a party morale. High morale gives a slight speed boost and an edge in simulated battles; below a threshold troops may desert as an uncontrolled party | "At the beginning of the battle, each soldier is given a starting morale based on their party's overall morale rating." Since e1.7.0, agents regain up to half their initial morale. | Not stated. The campaign stock sets each soldier's start. | official-41, SEARCH, WEAK. `https://www.gamebanshee.com/3wppf`; `https://www.taleworlds.com/en/Games/Bannerlord/Blog/68` |
| Battle Brothers (Overhype Studios) | not covered by the evidence | Five morale states, with Steady the default and without effect; Wavering slightly reduces efficiency. Checks move characters between states. A character can rally only when no enemy is adjacent; fleeing spreads to allies. | Not stated | official-42, SEARCH, WEAK (one summary, its Breaking text garbled). `https://battlebrothersgame.com/?p=883`; `https://battlebrothersgame.com/?p=389` |
| Gary Grigsby's War in the East 2 (2by3 Games) | a persistent unit morale on the operational map. A winner may gain 1 point (a check on its leader); a loser may lose up to 3; a retreat costs 1 (2 if the leader's check fails) and a rout 1 more. National morale caps growth without combat experience. | not covered by the evidence (combat is resolved on the operational map) | **Not stated in the rules summarised: morale moves with results, not with taking part.** A player complains of "severe feedback loops from the linkage of unit morale to combat victories". | community-19, SEARCH, WEAK. `https://dornshuld.chemistry.msstate.edu/rules/wite2/23-0.html`; `https://forums.matrixgames.com/viewtopic.php?p=3616023` |
| Field of Glory II (Byzantine Games) | not covered by the evidence | Discrete cohesion levels change by tests that events trigger: shooting losses of about 5 to 10 % in a turn, a retest on further losses, -1 for artillery, -2 when Fragmented. A failed test breaks a unit; a wavering unit that is charged and fails routs at once; a general rallies on 6 or more. | No time-based drain and no cost for entering was mentioned, though neither was shown to be absent | community-20, SEARCH, WEAK. `https://forum.slitherine.com/viewtopic.php?p=858740` |
| Ultimate General: Gettysburg (Game-Labs); John Tiller's Civil War Battles (John Tiller Software) | not covered by the evidence | Gettysburg's field manual: "morale is affected by many factors including flank or rear attacks, casualties, volley shocks, artillery fire and fatigue". Tiller: fatigue runs from 0 to 900; high fatigue gives -2 to morale checks and -20 % melee; casualties trigger an end-of-phase check. | Indirectly: fatigue, built up by activity, feeds morale | community-21, SEARCH, WEAK. `https://steamcdn-a.akamaihd.net/steam/apps/306660/manuals/Field_Manual.pdf`; `https://johntillersoftware.com/CivilWarBattles/user/fatigue.html` |

**Patterns (INFERRED from the rows above).**

1. A per-time cost for being in battle is reported in the genre (Europa Universalis IV), and so is fatigue, built up by activity,
   that feeds morale (Ultimate General, Tiller).
2. A campaign stock can set each soldier's starting morale in battle (Bannerlord).
3. Campaign morale can move with results only, gated by leader quality (War in the East 2). That title carries a warning about
   feedback loops.
4. Discrete states can be changed by event-triggered tests (Battle Brothers, Field of Glory II).

An XCOM 2: War of the Chosen hypothesis (targeted-10, KNOW) is **not evidence** and is listed in §6.

---

## 4. Sibling-title evidence (same series and developer; NOT the reference)

None of these rows is the reference's. They show what the series does elsewhere, and how this title's inherited keys may have been
meant (INFERRED).

| Title | Claim | ID | Label | Verdict | Source |
|---|---|---|---|---|---|
| WARHAMMER II (2017) | Identical 129 battle-morale keys and 29 fatigue keys with identical descriptions; the rules and campaign-variable lists differ by title. So this title **kept its predecessor's battle-morale set** (keys only; values not compared). The checker adds that THREE KINGDOMS' 131 morale keys contain this title's 129, while WARHAMMER III differs. | data-21 | CODE | CONFIRMED | S-WH2 vs S-TROY |
| WARHAMMER II and III | **A morale loss independent of losses, in auto-resolve:** `autoresolver_minimum_morale_loss` "(IN GAME) Minimum morale loss in one autoresolver combat round" and `_per_second` "Minimum morale loss per second in a unit vs unit prediction". **Absent from the reference and from THREE KINGDOMS.** | checker observation (data-01 verdict note); re-read by this step | CODE | re-read and found as stated (not a finder claim) | S-WH2, S-WH3, S-3K, S-TROY |
| WARHAMMER III | `enemy_morale_penalty_combat_power_min` and `_max` ("relative difference in combat power to apply the moral penalty") and `_value_min` and `_max`: a battle-balance morale term the reference lacks | checker observation (data-08 note); re-read here | CODE | re-read and found as stated | S-WH3 |
| WARHAMMER III | Script documentation (summary): a force's current morale, a predicted next-turn value, and an IsLow flag, "true if morale is considered low (which gives penalties)" | targeted-06 | SEARCH | WEAK | `https://chadvandy.github.io/tw_modding_resources/WH3/campaign/model_hierarchy.html` |
| WARHAMMER III | A guide: an ambushed army immediately suffers -10 leadership, decaying over about a minute | official-32 | SEARCH | WEAK | `https://guides-factory.com/guides/total-war-warhammer-3-ambush-guide` |
| WARHAMMER and WARHAMMER III | Forced march: the army starts battle tired (WARHAMMER), or every unit "very tired" with "perfect vigour" applying only to "the newly set max" (WARHAMMER III); marched armies "can reinforce battles, they just can't START the battle" | community-15 | SEARCH | WEAK | `https://steamcommunity.com/app/364360/discussions/3/1728711392747829714/`; `https://steamcommunity.com/app/1142710/discussions/0/3185738755278938668/` |
| WARHAMMER III | A mod's fatigue-effects row `threshold_winded stat_morale 0.9600` (the mod's value) | data-26 | SEARCH | WEAK | Brutal Battles 3 pages (page not pinned) |
| WARHAMMER III and THREE KINGDOMS | Ambush in two stages: detection first, then a roll shown as 40 to 80 % by terrain plus bonuses; a failed ambush becomes a normal battle | community-18 | SEARCH | WEAK | `https://steamcommunity.com/app/1142710/discussions/0/3842178984942298843/` |
| WARHAMMER (2016) | Greenskin "fightiness" rises with fighting, wins and raiding, and falls when the army is inactive or defeated. That this is how the inherited army family was used is INFERRED. | data-25 | SEARCH | WEAK | `https://pcgamesn.com/total-war-warhammer/learning-the-ways-of-the-greenskins-in-total-war-warhammer` |
| WARHAMMER | Ambush success by terrain: light forest +25 %, jungle +30 %, hills +5 %, desert -5 %, grassland 0 % | data-28 | SEARCH | WEAK | `https://totalwar.fandom.com/wiki/Ambush` |
| THREE KINGDOMS (2019) | An army attacked in march stance begins the battle "tired" | official-30 | SEARCH | WEAK | `https://totalwar.fandom.com/wiki/Army_(Total_War:_Three_Kingdoms)` |
| THREE KINGDOMS | Attrition "also reduces unit morale if a battle starts while the effect is active" | data-27 | SEARCH | WEAK | `https://totalwar.fandom.com/wiki/Attrition` |
| THREE KINGDOMS | Higher supplies give morale and replenishment bonuses; falling supplies bring attrition and morale penalties | official-34 | SEARCH | WEAK | `https://totalwar.fandom.com/wiki/Food_(Total_War:_Three_Kingdoms)` |
| THREE KINGDOMS | A duel's winner boosts his army's morale and that of allied armies; the loser's army is penalised | official-31 | SEARCH | WEAK | `https://segmentnext.com/2019/05/28/total-war-three-kingdoms-duels-guide/` |
| THREE KINGDOMS | The campaign AI's thresholds on "a forces morale" (0.0 to 1.0) are defined here with the same text as in the reference | checker observation in targeted-05's verdict (the claim itself is CONTRADICTED, §6) | CODE | re-read by the checker | S-3K |
| THRONES OF BRITANNIA (2018) | An army's supplies are consumed each turn, with defections when they run out; each stance has a supply cost | official-33 | SEARCH | WEAK | `https://steamcommunity.com/app/712100/discussions/0/1696046342855184699`; `https://www.totalwar.com/?p=2933` |
| SHOGUN 2 (2011) | The encyclopedia, as a player quotes it: night battles bring a morale penalty unless the general can fight at night, and no reinforcements | community-16 | SEARCH | WEAK | `https://steamcommunity.com/app/34330/discussions/0/3109145219798183485` |
| EMPIRE and SHOGUN 2 era (2009–2011) | Modders' readings: computed morale against thresholds (wavering -5 to 1, timeout 40; steady 3 to 6); "shattered" below the broken threshold; a shock at about 20 % lost in 4 s | community-17 | SEARCH | WEAK | `https://www.twcenter.net/threads/how-works-the-morale-system-in-empire-total-war.403791/`; `https://www.twcenter.net/threads/morale-shock-explained.463698/` |
| ROME II (2013) | Official encyclopedia: "If attacked, all units in a marching army suffer reduced morale to reflect the inherent surprise element. The resultant battle will always be an ambush." A garrisoned army's units have improved morale if the settlement is attacked. | official-29 | SEARCH | WEAK | `https://r2encv2.totalwar.com/en/manual/single-player/0018_enc_page_campaign_play_military_stances/index.html` |
| ROME II | Official encyclopedia: routers "must be within the general's radius of influence for the rally to take effect"; shattered units do not respond; if the general is killed while units are routing, they shatter | official-28 | SEARCH | WEAK | `https://r2encv2.totalwar.com/en/manual/single-player/0087_enc_page_battle_play_phase_conflict_morale/index.html` |
| ROME II | A player: an ambushed army suffers a 25 % morale penalty | official-32 | SEARCH | WEAK | `https://steamcommunity.com/app/214950/discussions/0/1642039363009083869` |

**What the siblings show for Q1 (pattern only; INFERRED).** The series has charged morale at a battle's start for **a campaign
state**:

- caught on the march (ROME II);
- attrition (THREE KINGDOMS);
- night without a night-capable general (SHOGUN 2);
- ambushed, with a decay (WARHAMMER III).

It has also set **fatigue** at the start (THREE KINGDOMS, WARHAMMER, WARHAMMER III). And it has a **minimum loss per round in
auto-resolve** (WARHAMMER II and III, CODE). None of these is a charge on every unit for entering a battle, and none of them is the
reference's mechanism.

---

## 5. Input for our design (lane MOR2). No decisions are taken here.

### 5.1 D-083 DB1 (entering a battle costs morale)

**What the evidence suggests.**

- **The reference does not visibly charge every unit for entering a battle.** At definition level its unit-morale model has no such
  term; its army-level family, if it is used at all, has none; and about 197 search summaries describe none. The evidence can
  neither confirm nor exclude that the game does so in play. DB1 therefore remains our own design (D-083 DB2). For Annex A the
  honest wording is "not found; not ruled out", not "the reference does not".
- **The reference's model is described as computed, not drawn down.** A starting point plus situational terms, with the current
  value moving toward the computed value each tick. In that form a cost is naturally expressed as a **term with a condition** (a
  state, or a timed effect), not as a one-off deduction from a stock (INFERRED from the definitions).
- **Its terms that need no losses are conditional states:** surprise, timed; night unpreparedness; fatigue states; exposed flanks;
  enemy numbers nearby; being under missile or artillery fire; a frightening enemy; first contact from a given direction; a dead or
  fled general. Several are set by the campaign state at the start: fatigue from stances, the night attack, and surprise if it
  marks an ambush (INFERRED). For DB1 this is a pattern of "**entering under bad conditions costs**", not "entering costs".
- **The closest triggers for taking part found:**
  - a hero's motivation, lowered by fighting in his own territory or by being reinforced (two traits; commander level; conditional;
    WEAK);
  - fatigue deltas for melee combat and the combat-ready pose, which feed fatigue morale concerns (definitions; signs unknown);
  - in siblings, a minimum morale loss per round in auto-resolve (CODE, WARHAMMER II and III);
  - in the genre, a daily morale damage for troops deployed in a battle (EU4, WEAK).

**What the evidence does not show.** No size for any of the above. Nothing on whether the reference's in-battle morale changes carry
back to the campaign, other than through the inherited win, loss and casualty terms, whose use is unknown. And no answer to D-083's
READINGS.

### 5.2 The READINGS and D-083 §4. Patterns only; none of them chooses.

- **DB1-R1 (what entering is).**
  - The reference defines a **bonus** for having pending reinforcements (`max_reinforcement_bonus`), and no key that charges an
    arriving reinforcement (definitions).
  - A trait makes a hero lose motivation for reinforcing or being reinforced (WEAK).
  - In siblings, armies on a forced march "can reinforce battles, they just can't START the battle" (WEAK).

  Nothing here separates (a), units placed on the field pay, from (b), every unit of the battle pays.
- **DB1-R2 (once, or with time).**
  - **Once, or at the start, with a decay:** "Surprised" with a timeout (definition); ROME II's marching army caught in an ambush;
    WARHAMMER III's ambushed army at -10, decaying over about a minute; THREE KINGDOMS' attrition at the battle's start (siblings,
    WEAK).
  - **Drain:** the fatigue path (definitions); EU4's daily morale damage for deployed troops (genre, WEAK); the auto-resolve
    minimum loss per round or per second (WARHAMMER II and III, CODE).

  Both forms exist in the series and the genre, so the evidence does not choose between (a) and (b).
- **DB1-R3 (DB1 together with DA1).** The inherited army family keeps **a win bonus and a loss penalty as separate terms**, each
  lasting turns. The win bonus "falls off" with the armies' relative strength (definition; use unknown). There is no term for
  taking part to set against them, so a battle won without loss would only raise morale there. This parallels our DA1 sizing by
  B ÷ (B + W), which was designed before this evidence (morale proposal §4.2). It neither supports DB1-R3 (a) nor (b). Under D-069
  it is a pattern to note, not a form to copy.
- **D-083 §4 (where the cost is charged).**
  - In the reference's computed model, a condition at the battle's start lasts while the condition holds (for instance surprise,
    until its timeout), and the update each tick moves morale toward the recomputed value. That is the analogue of D-083 §4's
    warning that a cost taken only at the battle's start can be recovered within the battle (INFERRED).
  - On the campaign side, the inherited win and loss terms last a number of turns: a pattern for a term in the settlement.
  - The evidence offers one pattern for "in the battle" (a timed state) and one for "the settlement" (a campaign term with a
    duration), not a ruling.

### 5.3 Laws (our constitution), for reading the patterns

- **Law 2.** Most of the reference's carry from campaign to battle is **conditional effects**: bonus values and effect bundles. That
  is the form our Law 2 bans as standing modifiers. The patterns that fit our laws are stocks and events: motivation-like event
  triggers, a campaign stock with win and loss terms, and timed battle states.
- **Law 3.** The reference's battle terms run per tick or per second, and its campaign terms per turn. Any use of ours is a rate
  per sim-year times the exact pulse length.

### 5.4 The Annex A rewrite and the three overstated cells (what each row can now cite)

**The three cells of the morale proposal's §3.3 "Pattern seen" column.**

| Cell | Cited before | What it can cite now | Confidence |
|---|---|---|---|
| **Rout contagion**: "the reference and the genre: a friendly rout nearby lowers morale (Annex A.3, A.5)" | Annex A.3 has no rout-contagion row. A.5's Bannerlord row says "losses and fleeing nearby lower it" (SEARCH, earlier pass); this pass did not find it again. The "demoralised by allies routing" wording that searches attach to the series comes from a WARHAMMER fan wiki, not from ROME II's manual (official-28 notes). | **Reference (CODE, definition):** a "friends routing" concern fed by `rout_balance (enemies_routing - friends_routing)` × weighting, within front and flank distances, with caps on the counts (data-06, WEAK: the friendly half is supported; that enemy routs raise morale is not). No reference report of contagion. **Genre:** one developer blog says fleeing spreads to allies (official-42, WEAK, one summary). | The concern is defined: ESTABLISHED. That it acts, and by how much: UNKNOWN. |
| **Fatigue**: "fatigue lowers morale (Annex A.4)" | A.4 (ROME II's manual) has no fatigue content | **Reference (CODE, definition):** three fatigue morale concerns ("Vigour penalty", "Unit is very tired but not exhausted", "Completely knackered") and a table of stat multipliers per fatigue level, morale rows unknown (data-07, CONFIRMED); fatigue built by activity deltas (data-17, CONFIRMED). No reference report links fatigue to morale (official-25, community-13). **Sibling:** a WARHAMMER III mod's morale row (data-26, WEAK). **Genre:** Ultimate General: Gettysburg; Tiller (community-21, WEAK). | The concerns are defined: ESTABLISHED. Their effect: UNKNOWN. |
| **Missile fire / fire without casualties**: "missile fire lowers morale (genre)" | no source named | **Reference (CODE, definition):** "Attacked by projectile"; "Unit was attacked but not hit by artillery"; "Unit was hit and damaged by artillery"; friendly fire (data-05, CONFIRMED). These can act before any loss (checker). **Reference reports:** players' "morale penalty too when under missile fire" (community-08, WEAK); a ranged modifier, "Serrated Edges", lowers enemy morale (official-13, WEAK). **Genre:** Ultimate General: Gettysburg (artillery fire, volley shocks; community-21, WEAK). Field of Glory II's tests are triggered by shooting **losses**, not by fire alone (community-20, WEAK). | The terms are defined: ESTABLISHED. Size: UNKNOWN. (This cell's "READING M (b) … OPEN" is superseded by D-083, per the proposal's dated note.) |

**The other rows of Annex A and §2.**

| Row (earlier pass) | What the evidence now says | Evidence | Confidence |
|---|---|---|---|
| A.1: "[CODE]: none … table and field NAMES only, no values … labelled [SEARCH]" | The same file (same SHA-256) carries `lookup_hardcoded` key lists with in-house descriptions: 129 battle-morale, 29 fatigue, 207 rules and 1,138 campaign-variable keys. These are definitions with no values: CODE under this pass's labels, or FETCH if the file is held to be a page. | data-01; check contradiction 15 | ESTABLISHED (definition level) |
| A.1 host list | 93 hosts tested in this run; 90 refused at the proxy (CONNECT 403); the earlier "could not resolve" hosts (primagames.com, www.pcinvasion.com, www.gamepressure.com, wiki.totalwar.com) are among those refused by curl; `www.iskmogul.com` and `r2encv2.totalwar.com` were not re-tested | §1.2 | MEASURED (host logs; not a claim about the game) |
| A.2: "Troops have no morale number outside the battlefield"; §2: "On the campaign map there is no army morale number" | Not established either way. No source shows one; the data defines an inherited 36-key family whose use is not established. | data-14; check contradiction 3 | Definition ESTABLISHED; use UNKNOWN |
| A.2: army-wide percentage modifiers | Still reported (WEAK). The structure is defined: battle-context bonus values with a clamp found only in this title. | official-23, community-03, community-04, official-22; data-15 | Reports UNKNOWN; structure ESTABLISHED |
| A.2: Battle Glory | The same in substance. The official wording comes through one summary; the morale buff comes from guides only; there is no "glory" string in the schema. | official-18; data.not_found | UNKNOWN |
| A.2: motivation, "exact effects not found" | Now reported to feed the hero's morale, rage, local happiness and movement, with no desertion even at -10. Faction leaders are exempt. Battle triggers per trait include fighting in his own territory. | targeted-01, targeted-02, targeted-03 (WEAK); targeted-04 (structures) | UNKNOWN |
| A.2: Achilles' mood | Battle triggers added: losing any battle pushes him toward Outraged | community-06, official-19 | UNKNOWN |
| A.2: battle results (XP, loot, captives, the morale-effects table) | Definitions now read: the experience, captive, loot, theft and loyalty channels (nine grades); the morale-effects table maps a group and a level to an effect bundle | data-19, data-14 | ESTABLISHED (definition level) |
| A.2: "Whether a won or lost battle changes any persistent army-wide morale: not found" | Defined (battle won and lost, each for some turns); use not established. Hero and faction stocks that battles move are reported. | data-14; community-06, targeted-01 | Definition ESTABLISHED; use UNKNOWN |
| A.3: losses, flanking, commander death; the "scaling morale buff"; routing and rally | Matched at definition level by the casualty windows, contact direction, the general-died concerns, winning combat and the kill bonus, and the rally and shatter timers | data-03, data-04, data-08, data-11, data-12 | ESTABLISHED (definition level); sizes UNKNOWN |
| A.3: the mod changelog's 90 s and 60 s | 60 s comes from four community and mod statements; 90 s from two summaries of one mod's text | community-12, data-22, targeted-07 | UNKNOWN |
| A.3: "The database keeps morale constants … names only; the keys and values were not available" | The keys and descriptions are available (129); the values are not | data-01 | ESTABLISHED |
| A.3: "an ambushed army's morale penalty in this game: not found" | Still not found for the reference. The candidate term is "Surprised" (the link is INFERRED). Siblings have such a penalty. | data-02, data-18; §4 | UNKNOWN |
| A.3: "The morale states … the exact tooltip factors: not verified [KNOW]" | Seven states and the full factor list are defined; the tooltip text was not read | data-10, data-01 | ESTABLISHED (definition level) |
| A.4: ROME II | Keep it as sibling. Add the marching-army morale and the garrison morale (official-29). Do not attribute the chain-reaction wording to it. | official-28, official-29 | sibling |
| A.5: EU4 | Add the daily morale damage for deployed troops and reserves, and the wording conflict | official-40 | UNKNOWN (genre) |
| A.5: Bannerlord | Party morale, starting morale per soldier, desertion, and the e1.7.0 regain are reported. "Losses and fleeing nearby lower it, enemies killed nearby raise it" was not re-found. | official-41 | UNKNOWN (genre) |
| A.5: Battle Brothers | Add: rally only when no enemy is adjacent; fleeing spreads to allies (one summary) | official-42 | UNKNOWN (genre) |
| A.5: possible new rows | War in the East 2; Field of Glory II; Ultimate General: Gettysburg; Tiller | community-19, community-20, community-21 | UNKNOWN (genre) |
| A.6: "Not taken: the reference's campaign design (no stock, …)" | The "no stock" premise is not established: hero and faction stocks are reported, and an army stock is defined | data-14; official-18, official-19, targeted-01, targeted-02 | — |

### 5.5 Calibration warnings in the evidence (INFERRED from WEAK reports; for whoever calibrates)

- **Low base morale with stepped casualty penalties can end a fight in one clash.** Reviews of the reference at launch said low-tier
  units "are simply too easy to rout" (community-10). A patch later lowered high-tier base morale (official-08).
- **A long window with no rout after a rally made mauled units fight on.** This is how players describe the reference's default of
  about 60 s (community-12).
- **Feedback loops.** A genre player complains of "severe feedback loops from the linkage of unit morale to combat victories"
  (community-19). This bears on DA1 together with DB1.

### 5.6 Originality (D-069)

Every name, number and text from the reference, and from its siblings and the genre, stays out of our design and out of anything
player-facing. Each row above is a pattern to read, not content to take.

---

## 6. UNSUPPORTED and CONTRADICTED claims, and corrections to WEAK claims (kept so they are not lost)

**UNSUPPORTED (not used as evidence).**

| ID | Claim | Label | Scope | Why |
|---|---|---|---|---|
| official-39 | A hypothesis: the series recomputes battle morale from a base plus active modifiers, rather than drawing it down from a stock. If that holds here, entering a battle has no cost of its own. | KNOW | reference | Only partly consistent at definition level (`morale_base` "Starting point for calculations"; `percent_update_per_tick`), and with sibling modders' readings. No value, formula or engine code was read. Both "recomputed rather than drawn down" and "entering has no cost of its own" remain hypotheses. |
| targeted-10 | XCOM 2: War of the Chosen. A soldier's Will falls during a mission even without wounds; low Will leaves the soldier Tired, needing rest. That would make it a genre case of taking part costing morale. | KNOW | genre | Not searched, because the budget was used up. No evidence; it stays a hypothesis. |

**CONTRADICTED (not used as evidence).**

| ID | Claim | Label | Scope | Contradiction |
|---|---|---|---|---|
| targeted-05 | The campaign AI's task-management table, "found only in this title's schema", sets thresholds on "a forces morale" (0.0 to 1.0). It would be the first sign that a per-army campaign morale is "configured for this title rather than only inherited". | CODE | reference | THREE KINGDOMS (S-3K) and WARHAMMER III (S-WH3) both define the table. S-3K holds all 10 of its keys, and both thresholds read identically there. The table is absent only from WARHAMMER II. It is inherited from the 2019 sibling's AI, so as evidence that this title uses an army morale it adds nothing beyond data-14. The definitions themselves are real (§4). |

**Corrections inside WEAK claims (from `RUN/check.json`; the claims remain usable as WEAK, read with these corrections).**

| Claim | Correction |
|---|---|
| official-06 (note) and official-23 (closing generalisation), official.not_found (Q2) | "No campaign stock; morale exists only as conditional effects" is overtaken. Battle Glory, Achilles' mood and motivation are reported stocks (WEAK), and an army stock is defined (data-14). |
| official-07 | One summary has the exact wording; one more (S20) partly corroborates it |
| official-08 | "In both (S1 and S20)" is not supported by the log: effectively one summary |
| official-09 | One summary (S6), not two |
| official-15 | One summary (S1), not three; an exact-phrase check failed |
| official-22 and official.not_found | The 1.6.0 "Oration of Dread" line came back in two summaries (S1, S20), not one |
| official-38 | "The game's own morale constants and effect definitions are there" (the Assembly Kit) is an unverified expectation |
| data-02 and data-24 | The night-battle reading (attacker fatigue, defender morale bonus) rests on key names against this title's own descriptions, and drops the "autoresolve" limit |
| data-06 | "Enemy routs nearby raise it" is not supported: both weightings feed one concern, with no counterpart |
| data-15 | "The combined multiplier of all campaign morale effects is clamped" is the finder's reading; the descriptions clamp one bonus value, `morale_mod_mult_capped` |
| data-19 | Nine result grades for interfaction loyalty, not eight (the ninth is draw); "higher if the holder is wounded" assumes the modifier's sign |
| community-01 and community-09 | They cite "C3 and C38 (TV Tropes)", but C38 has no TV Tropes content, and the quote "in particular destroys morale in Troy" is recorded nowhere in the log. C3 has "flanking especially damaging to morale"; C76 has "flanking destroys morale". |
| community-07 | Scope "reference" means a mod of the reference: it is a mod's design, not the base game's |
| targeted-01 | T5 calls these motivation "classes", not traits. The label "trait" rests on the 1.7.0 "motivation traits" line (one summary) and on the loyalty-trait pool (an INFERRED mapping). |
| Earlier pass, Annex A.1 and A.3 | It held the same schema file (same SHA-256) and called it names only. The 129 morale keys with descriptions were in it. |

---

## 7. Claim index (every input claim, with label, scope, verdict and the section that uses it)

(Generated from the input files by `RUN/work-dossier/scripts/index.py`.)

"Cited in" counts exact mentions of an ID. A range such as "official-01 to official-05" counts only at its end points. For the UNSUPPORTED and CONTRADICTED claims, a citation is a mention and not use as evidence (§6).

| ID | Label | Scope | Questions (finder) | Verdict | Cited in |
|---|---|---|---|---|---|
| official-01 | SEARCH | reference | Q1 Q7 | WEAK | Q1, Q7 |
| official-02 | SEARCH | reference | Q1 Q7 | WEAK | Q1 |
| official-03 | SEARCH | reference | Q1 Q7 | WEAK | Q1 |
| official-04 | SEARCH | reference | Q1 Q7 | WEAK | Q1 |
| official-05 | SEARCH | reference | Q1 | WEAK | Q1, Q7 |
| official-06 | SEARCH | reference | Q1 Q2 | WEAK | Q1, Q2, §6 |
| official-07 | SEARCH | reference | Q2 | WEAK | Q2, §6 |
| official-08 | SEARCH | reference | Q3 | WEAK | Q3, §5.5, §6 |
| official-09 | SEARCH | reference | Q3 | WEAK | Q3, §6 |
| official-10 | SEARCH | reference | Q3 | WEAK | Q3 |
| official-11 | SEARCH | reference | Q3 Q8 | WEAK | Q3, Q8 |
| official-12 | SEARCH | reference | Q3 | WEAK | Q3, Q8 |
| official-13 | SEARCH | reference | Q3 | WEAK | Q3, §5.4 |
| official-14 | SEARCH | reference | Q3 Q8 | WEAK | Q3, Q8 |
| official-15 | SEARCH | reference | Q8 | WEAK | Q2, Q3, Q8, §6 |
| official-16 | SEARCH | reference | Q8 Q5 | WEAK | Q5, Q8 |
| official-17 | SEARCH | reference | Q8 | WEAK | Q8 |
| official-18 | SEARCH | reference | Q2 Q5 | WEAK | Q2, Q5, §5.4 |
| official-19 | SEARCH | reference | Q2 Q5 | WEAK | Q2, Q5, §5.4 |
| official-20 | SEARCH | reference | Q2 Q3 | WEAK | Q2 |
| official-21 | SEARCH | reference | Q5 Q2 | WEAK | Q2, Q5 |
| official-22 | SEARCH | reference | Q2 | WEAK | Q1, Q2, §5.4, §6 |
| official-23 | SEARCH | reference | Q2 | WEAK | Q1, Q2, §5.4, §6 |
| official-24 | SEARCH | reference | Q2 | WEAK | Q2 |
| official-25 | SEARCH | reference | Q3 Q7 | WEAK | Q3, Q7, §5.4 |
| official-26 | SEARCH | reference | Q7 | WEAK | Q7 |
| official-27 | SEARCH | reference | Q4 | WEAK | Q4 |
| official-28 | SEARCH | sibling | Q4 Q8 | WEAK | Q4, Q8, §4, §5.4 |
| official-29 | SEARCH | sibling | Q1 Q6 | WEAK | Q1, Q6, §4, §5.4 |
| official-30 | SEARCH | sibling | Q1 Q7 | WEAK | Q1, Q7, §4 |
| official-31 | SEARCH | sibling | Q8 Q5 | WEAK | Q5, Q8, §4 |
| official-32 | SEARCH | sibling | Q6 | WEAK | Q6, §4 |
| official-33 | SEARCH | sibling | Q2 | WEAK | Q2, §4 |
| official-34 | SEARCH | sibling | Q2 | WEAK | Q2, §4 |
| official-35 | SEARCH | reference | Q6 | WEAK | Q6 |
| official-36 | SEARCH | reference | Q6 | WEAK | Q6 |
| official-37 | SEARCH | reference | Q5 | WEAK | Q5 |
| official-38 | SEARCH | reference | Q1 Q2 Q3 Q7 | WEAK | §1.3, §6 |
| official-39 | KNOW | reference | Q1 | UNSUPPORTED | §6 |
| official-40 | SEARCH | genre | G Q1 | WEAK | Q1, §3, §5.4 |
| official-41 | SEARCH | genre | G Q2 Q1 | WEAK | Q1, Q2, §3, §5.4 |
| official-42 | SEARCH | genre | G Q3 Q4 | WEAK | Q3, Q4, §3, §5.4 |
| data-01 | CODE | reference | Q1 Q3 | CONFIRMED | Q1, Q3, §4, §5.4 |
| data-02 | CODE | reference | Q1 Q6 Q7 | WEAK | Q1, Q3, Q6, Q7, §5.4, §6 |
| data-03 | CODE | reference | Q3 | CONFIRMED | Q3, §5.4 |
| data-04 | CODE | reference | Q3 | CONFIRMED | Q1, Q3, §5.4 |
| data-05 | CODE | reference | Q3 | CONFIRMED | §0, Q1, Q3, §5.4 |
| data-06 | CODE | reference | Q3 | WEAK | §0, Q3, §5.4, §6 |
| data-07 | CODE | reference | Q3 Q7 | CONFIRMED | §0, Q1, Q3, Q7, §5.4 |
| data-08 | CODE | reference | Q3 | CONFIRMED | Q1, Q3, §4, §5.4 |
| data-09 | CODE | reference | Q3 Q8 | CONFIRMED | Q1, Q3, Q4, Q8 |
| data-10 | CODE | reference | Q3 Q4 | CONFIRMED | Q1, Q3, Q4, §5.4 |
| data-11 | CODE | reference | Q4 | CONFIRMED | Q4, Q8, §5.4 |
| data-12 | CODE | reference | Q8 Q3 | CONFIRMED | Q1, Q3, Q8, §5.4 |
| data-13 | CODE | reference | Q8 | CONFIRMED | Q8 |
| data-14 | CODE | reference | Q2 Q5 | CONFIRMED | Q1, Q2, Q5, §5.4, §6 |
| data-15 | CODE | reference | Q2 Q1 | WEAK | Q1, Q2, §5.4, §6 |
| data-16 | CODE | reference | Q2 | CONFIRMED | Q2 |
| data-17 | CODE | reference | Q7 | CONFIRMED | Q1, Q7, §5.4 |
| data-18 | CODE | reference | Q6 | CONFIRMED | Q6, §5.4 |
| data-19 | CODE | reference | Q5 | WEAK | Q5, §5.4, §6 |
| data-20 | CODE | reference | Q1 Q2 | CONFIRMED | Q1, Q2 |
| data-21 | CODE | sibling | Q1 Q2 Q3 Q4 Q7 | CONFIRMED | Q2, §4 |
| data-22 | SEARCH | reference | Q3 Q4 | WEAK | Q3, Q4, §5.4 |
| data-23 | SEARCH | reference | Q3 Q7 | WEAK | Q3 |
| data-24 | SEARCH | reference | Q1 Q8 | WEAK | Q1, §6 |
| data-25 | SEARCH | sibling | Q2 | WEAK | Q2, §4 |
| data-26 | SEARCH | sibling | Q7 Q3 | WEAK | Q3, Q7, §4, §5.4 |
| data-27 | SEARCH | sibling | Q1 Q2 | WEAK | Q1, Q2, §4 |
| data-28 | SEARCH | sibling | Q6 | WEAK | Q6, §4 |
| community-01 | SEARCH | reference | Q1 Q3 | WEAK | Q1, Q4, §6 |
| community-02 | SEARCH | reference | Q2 Q1 | WEAK | Q1, Q2 |
| community-03 | SEARCH | reference | Q2 | WEAK | Q2, §5.4 |
| community-04 | SEARCH | reference | Q2 | WEAK | Q1, Q2, §5.4 |
| community-05 | SEARCH | reference | Q2 | WEAK | Q2 |
| community-06 | SEARCH | reference | Q5 Q2 | WEAK | Q2, Q5, §5.4 |
| community-07 | SEARCH | reference | Q2 Q5 | WEAK | Q2, §6 |
| community-08 | SEARCH | reference | Q3 | WEAK | Q3, §5.4 |
| community-09 | SEARCH | reference | Q3 | WEAK | Q3, §6 |
| community-10 | SEARCH | reference | Q3 | WEAK | Q3, §5.5 |
| community-11 | SEARCH | reference | Q8 Q3 | WEAK | Q3, Q8 |
| community-12 | SEARCH | reference | Q4 | WEAK | Q4, §5.4, §5.5 |
| community-13 | SEARCH | reference | Q7 Q1 | WEAK | Q1, Q3, Q7, §5.4 |
| community-14 | SEARCH | reference | Q8 | WEAK | Q8 |
| community-15 | SEARCH | sibling | Q1 Q7 | WEAK | Q1, Q7, §4 |
| community-16 | SEARCH | sibling | Q1 Q3 | WEAK | Q1, §4 |
| community-17 | SEARCH | sibling | Q3 Q4 Q1 | WEAK | Q3, Q4, §4 |
| community-18 | SEARCH | sibling | Q6 | WEAK | Q6, §4 |
| community-19 | SEARCH | genre | G Q2 Q5 Q1 | WEAK | Q1, Q2, §3, §5.4, §5.5 |
| community-20 | SEARCH | genre | G Q3 Q4 | WEAK | Q1, Q3, Q4, §3, §5.4 |
| community-21 | SEARCH | genre | G Q3 Q7 | WEAK | Q3, Q7, §3, §5.4 |
| targeted-01 | SEARCH | reference | Q1 Q2 Q5 | WEAK | Q1, Q2, Q5, §5.4, §6 |
| targeted-02 | SEARCH | reference | Q2 Q8 Q1 | WEAK | Q1, Q2, Q8, §5.4 |
| targeted-03 | SEARCH | reference | Q2 Q5 | WEAK | Q1, Q2, §5.4 |
| targeted-04 | CODE | reference | Q2 Q5 Q1 | CONFIRMED | Q1, Q2, Q5, §5.4 |
| targeted-05 | CODE | reference | Q2 | CONTRADICTED | Q2, §4, §6 |
| targeted-06 | SEARCH | sibling | Q2 | WEAK | Q2, §4 |
| targeted-07 | SEARCH | reference | Q3 | WEAK | Q3, §5.4 |
| targeted-08 | SEARCH | reference | Q3 Q8 | WEAK | Q3 |
| targeted-09 | SEARCH | reference | Q5 Q8 | WEAK | Q2, Q5, Q8 |
| targeted-10 | KNOW | genre | G Q1 | UNSUPPORTED | §3, §6 |

Totals: 101 claims; CONFIRMED 18, CONTRADICTED 1, UNSUPPORTED 2, WEAK 80. Not-found sets: official, community and targeted WEAK; data CONFIRMED. Every claim above is cited at least once in the body unless marked NOT CITED.

---

*End of dossier. Reference only (D-069). No decisions are taken here; §5 is input for lane MOR2.*
