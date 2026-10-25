# Online research: end-game repeatable research pricing (complete, 2026-10-25)

Access note: claude.ai session proxy blocks fandom, civfanatics, reddit, steam, paradoxwikis, wikipedia for direct fetch. Primary evidence below is the games' own data files read from public GitHub mirrors (git clone through proxy), plus WebSearch result summaries. Every number carries the URL of the file/page it came from.

## 1. Civilization VI (base-game data, mirror https://github.com/mrobaczyk/civ6 @ 310319f5, Aug 2024)
Source files:
- Techs: https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/Technologies.xml
- Civics: https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/Civics.xml
- Text: https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Text/en_US/Technologies_Text.xml , .../Civics_Text.xml
- Scoring: https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/Scoring.xml

Future Tech: `TECH_FUTURE_TECH Cost="2500" EraType="ERA_INFORMATION" Repeatable="true"` (Technologies.xml line 151).
Future Civic: `CIVIC_FUTURE_CIVIC Cost="3200" EraType="ERA_INFORMATION" Repeatable="true"` (Civics.xml line 119).
Reward text (both): "Can be completed multiple times, increasing your points towards the Score Victory." -> reward is SCORE ONLY, no gameplay effect.
Scoring: LINE_ITEM_TECHS / LINE_ITEM_CIVICS Multiplier="2" ScaleByCost="true" (score scales with cost).
Cost per repeat: the data row has a single fixed Cost; nothing in base data escalates it -> flat cost per repeat (base game; before map-size/speed multipliers).

Base-game tech cost by era (Technologies.xml):
| Era | tech costs |
|---|---|
| Ancient | 25 (Pottery, Animal Husbandry, Mining), 50 (Sailing, Astrology, Irrigation, Archery, Writing), 80 (Masonry, Bronze, Wheel) |
| Classical | 120, 200 |
| Medieval | 300, 390 |
| Renaissance | 540, 660 |
| Industrial | 845, 970 |
| Modern | 1140, 1250 |
| Atomic | 1410, 1580 |
| Information | 1850, 2155; Future Tech 2500 |
Ratio last real tech/first tech = 2155/25 = 86x; Future Tech / most expensive real tech = 2500/2155 = 1.16x.

Base-game civic cost by era (Civics.xml): Ancient 20 (Code of Laws) - 70; Classical 110-175; Medieval 200-385; Renaissance 400-655; Industrial 725-1060; Modern 660-1715; Atomic 1955-2415; Information 2880; Future Civic 3200 (=1.11x Social Media/Globalization 2880).
68 techs, 51 civics in base data. Every one of 64 tech boosts and 48 civic boosts is `Boost="50"` (Eureka/Inspiration = 50% of cost in base game).

### Civ VI Gathering Storm (Future Era) values
Source: Archipelago Civ6 world data extracted from GS game files, https://github.com/hesto2/civilization_vi_apworld/blob/main/data/existing_tech.json and .../data/existing_civics.json
- TECH_FUTURE_TECH Cost 2600, ERA_FUTURE (other Future-era techs 2200; Offworld Mission 2500). Future Tech = 1.18x a normal Future-era tech.
- CIVIC_FUTURE_CIVIC Cost 3500, ERA_FUTURE (other Future-era civics 3200). = 1.09x.
- GS tech cost range by era: Ancient 25/50/80; Classical 120/200; Medieval 300/390; Renaissance 600/730; Industrial 930/1070; Modern 1250/1370; Atomic 1480/1660; Information 1850/2155; Future 2200/2500/2600. (77 techs, 61 civics.)
- GS civic range: Ancient 20-70; Classical 110-175; Medieval 220-420; Renaissance 440-720; Industrial 800-1210; Modern 1540-1715; Atomic 1955-2415; Information 2880-3100; Future 3200/3500.
- WebSearch summary of civilopedia/fandom (https://www.civilopedia.net/en-US/gathering-storm/technologies/tech_future_tech/ , https://www.civilopedia.net/en-US/gathering-storm/civics/civic_future_civic/ , https://civilization.fandom.com/wiki/Future_Tech_(Civ6) , https://civilization.fandom.com/wiki/Future_Civic_(Civ6)): GS Future Tech also gives +5% Production toward city projects per completion (stacking); GS Future Civic gives +1 Governor title and +50 Diplomatic Favor per completion; both add score. [Secondary — search-engine summary of pages I could not fetch directly; costs 2600/3500 match the data file above.]
- Cost per repeat stays flat (single Cost value; no escalation field in data). Effective cost is scaled by game speed: GameSpeeds.xml CostMultiplier Marathon 300 / Epic 150 / Standard 100 / Quick 67 / Online 50 (https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/GameSpeeds.xml). Standard speed = 500 turns (sum of TurnsPerIncrement), Marathon 1500, Epic 750, Quick 330.

### Civ VI Eureka/Inspiration share
- Base game: all 64 tech boosts and 48 civic boosts are Boost="50" -> 50% of cost (Technologies.xml / Civics.xml above).
- Rise & Fall onward: 40% (fandom summary via WebSearch: "Players gain 50% (40% after Rise and Fall) of the Science or Culture required" https://civilization.fandom.com/wiki/File:Masonry_Eureka_moment_(Civ6).jpg ; also https://forums.civfanatics.com/threads/tech-and-civic-boosts.645259/ ). [secondary]
- 64/68 base techs and 48/51 base civics have a boost (~94%).

### Civ VI early-game pace (derived from data)
- Start income: Palace +2 Science, +1 Culture (Buildings.xml https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/Buildings.xml lines 344/381); +0.7 Science and +0.3 Culture per citizen (GlobalParameters.xml SCIENCE_PERCENTAGE_YIELD_PER_POP=70, CULTURE_PERCENTAGE_YIELD_PER_POP=30, https://github.com/mrobaczyk/civ6/blob/main/Base/Assets/Gameplay/Data/GlobalParameters.xml).
- => turn-1 capital ~2.7 Science/turn: a 25-cost first-tier tech takes ~9-10 turns; a 50-cost tech ~18 turns unboosted, ~9 boosted (50%). First civic Code of Laws 20 culture at ~1.3 culture/turn ~= 15 turns.
- 3 opening techs available at 25 (Pottery, Animal Husbandry, Mining) + Astrology/Sailing/Archery/Irrigation/Writing reachable soon: ~3 meaningful parallel choices at turn 1 widening to ~6-8 in the first 20-30 turns.

### Civ VI overflow
- Normal per-turn science overflow carries into the next tech; one-time instant grants (Eureka, Great Scientist, looting) can overwrite the overflow pool (community-documented bug). Sources (WebSearch summaries): https://steamcommunity.com/sharedfiles/filedetails/?id=2772516161 ("Science/Culture Overflow Bug Fix" mod), https://forums.civfanatics.com/threads/lost-overflow.615518/ . No official Firaxis statement found. Culture/civics behave the same per the same mod (title "Science/Culture Overflow").

## 2. Civilization VII (all secondary: WebSearch summaries; game data not mirrored on reachable hosts)
- Future Tech cost per Age (fandom list page https://civilization.fandom.com/wiki/List_of_technologies_in_Civ7 , https://civilization.fandom.com/wiki/Future_Tech_(Civ7) ; Exploration confirmed by https://civ7.wiki.fextralife.com/Future_Tech_-_Exploration_Technology ):
  - Antiquity 1255 Science (prereqs Iron Working + Mathematics), reward includes 2 Innovation
  - Exploration 4250 Science (prereqs Gunpowder + Urban Planning), 3 Innovation
  - Modern 10000 Science (prereqs Rocketry + Nuclear Fission, which cost 8500 each -> Future Tech = 1.18x the last real tech), 3 Innovation
- Future Civic Modern: 8000 Culture (prereqs Capitalism/Hegemony/Militarism) per https://civilization.fandom.com/wiki/List_of_civics_in_Civ7 [single source].
- Reward per completion: +1 attribute point (Wildcard at launch; changed to "random attribute point" in July 22 2025 patch because "these repeatable rewards were causing the overall impact of the attribute system to be greater than intended"), +10 Age progress, boost for a starting tech of next Age; Future Civic in Modern also +1 Artifact. Sources: https://support.civilization.com/hc/en-us/articles/42883941758867-Civilization-VII-Patch-Notes-July-22-2025 , https://steamcommunity.com/sharedfiles/filedetails/?id=3427558392
- Escalation: Patch 1.0.1 (Feb 10 2025): "Future Civic is now repeatable in all Ages. The cost of Future Tech and Future Civic will now increase more when repeated." — magnitude NOT published. https://civilization.2k.com/civ-vii/game-update-notes/2025-feb-10-patch-1-0-1-2/ , https://www.shacknews.com/article/143053/civliation-7-update-101-patch-2-notes
  -> design lesson: Firaxis had to (a) raise per-repeat escalation and (b) weaken per-repeat reward after launch because flat-ish repeatables with real gameplay rewards became too strong.
- Overflow: "Any overflow Science is immediately carried over into the next Technology." https://civilization.fandom.com/wiki/Technology_(Civ7) [secondary]

## 3. Stellaris (primary: parsed vanilla v4.5.2 data, https://github.com/sopleb/StellarisTechnology/blob/main/site/data/vanilla.json , generator "stellaris-tech 0.1.0", 679 techs; files referenced are common/technology/00_{eng,phys,soc}_tech_repeatable.txt)
- Base cost of every standard repeatable: cost = @repeatableTechBaseCost = 50000; cost_per_level = 5000 (linear/additive: level n costs 50000 + 5000*(n-1) -> +10% of base per level; L10 = 95000 = 1.9x, L20 = 145000 = 2.9x). Before empire-size cost modifiers.
- Comparison: non-repeatable tier costs in the same file: T1 median 1250 (500-6000), T2 median 2500, T3 median 5000 (3000-16000), T4 median 10000 (8000-20000), T5 median 30000 (10000-300000). First repeatable = 5x a median T4 tech, 1.67x a median T5.
- History: v1.x (2016) mirror https://github.com/Lej/stellaris/blob/master/src/static/common/technology/00_eng_tech_repeatable.txt : @repeatableTechBaseCost = 1500, @repeatableTechLevelCost = 480 (+32% of base per level), tier 2, weight 25 x 0.5. Player threads cite intermediate 50000/+10000 and 50000/+5000 pairs (WebSearch summary of https://steamcommunity.com/app/281990/discussions/0/1638661595053577279/ and https://steamcommunity.com/app/281990/discussions/0/596283752196347091/ ). Always linear in level, never geometric.
- Global cost pressure instead: empire size raises ALL tech cost ("Impact of Empire Size doubled" in a 2024 patch; player report ~+40% tech cost at 8 planets/200 pops) https://steamcommunity.com/app/281990/discussions/0/4357871761804585109 [anecdotal].
- 25 standard repeatables in v4.5.2 (levels -1 = unlimited unless noted), benefit per level is a flat additive % (linear, no cap):
  | Category (area) | Repeatable | per level | cap |
  |---|---|---|---|
  | Economy - minerals (eng/industry) | improved_tile_mineral_output | Minerals from Jobs +5% | none |
  | Economy - energy (phys) | improved_tile_energy_output | Energy from Jobs +5% | none |
  | Economy - food (soc/biology) | improved_tile_food_output | Food from Jobs +5% | none |
  | Unity/culture (soc/new_worlds) | improved_core_system_cap | Monthly Unity +5% | none |
  | Build speed (eng) | reduced_building_time | Infrastructure Build Speed +10% | 5 levels |
  | Build cost (phys) | reduced_building_cost | Building Cost -5% | 5 levels |
  | Weapons damage (eng/phys) | weapon_type_{kinetic,explosive,energy}_damage | +5% | none |
  | Weapons fire rate | weapon_type_{kinetic,explosive,energy}_fire_rate | +5% | none |
  | Strike craft | strike_craft_fire_damage / fire_rate | +10% / +10% | none |
  | Armor / Shields | improved_armor_output / improved_shield_output | +5% HP | none |
  | Defense platforms | military_station_damage / _health | +10% | none |
  | Ground army | improved_army_damage / army_health | +10% | none |
  | Leaders | improved_leader_life_span | +5 years | none |
  | Fleet capacity | naval_cap / command_limit | +20 / +20 | 5 levels |
  | Starbases | improved_starbase_capacity | +1 | 5 levels |
  Special: tech_cosmogenesis_thesis 75000 + 70000/level (Machine Age); L-Gate Insight 2000 + 5000/level.
- NOT present in v4.5.2 as repeatables: research speed, pop growth, stability/happiness, edict cost. Earlier vanilla (Jan 2022 build, https://github.com/IslayTzash/stellaris-tech-tree/blob/master/public/vanilla/techs.json ) had the same 50000 base and also: Edict Duration +10%/level (statecraft), Administrative Capacity +15/level (the core_system_cap key, now Monthly Unity +5%), Fleet Command Limit +10/level (now +20, capped 5). Research speed and pop growth were never vanilla repeatables in either build; governance repeatables were converted to output %. i.e. Paradox keeps repeatables on economy %, unity %, and military, and avoids self-compounding research-speed repeatables.

## 4a. Civilization IV: BtS (primary: AdvCiv mod XML + DLL source, https://github.com/f1rpo/AdvCiv ; AdvCiv keeps BtS tech XML costs to my knowledge — e.g. Mysticism iCost 50 matches the BtS block quoted at https://forums.civfanatics.com/threads/trait-specific-techs.138643/ — but treat as "BtS via AdvCiv")
- TECH_FUTURE_TECH: iCost 10000, ERA_FUTURE, bRepeat=1, iHealth=1, iHappiness=1 (each completion = +1 health and +1 happiness in every city). https://github.com/f1rpo/AdvCiv/blob/master/Assets/XML/Technologies/CIV4TechInfos.xml
- Ratio vs top real techs: Robotics 8000, Fusion 8000, Stealth 8000 -> Future Tech = 1.25x the most expensive real tech.
- Tech cost range: Ancient 40 (Hunting, Fishing) - 120; Classical 210-400; Medieval 500-1000; Renaissance 1200-2800; Industrial 2400-6500; Modern 4000-8000; Future 7000-10000. Last/first = 8000/40 = 200x.
- No per-repeat escalation: CvTeam::getResearchCost (https://github.com/f1rpo/AdvCiv/blob/master/CvGameCoreDLL/CvTeam.cpp ~line 2176) multiplies the XML cost by handicap, era, world size, game speed, start era, team size — no term in the number of times the repeat tech was taken.
- Game speed iResearchPercent: Marathon 250 (1250 turns), Epic 150 (750), Normal 100 (500), Quick 67 (335). https://github.com/f1rpo/AdvCiv/blob/master/Assets/XML/GameInfo/CIV4GameSpeedInfo.xml
- Overflow: kept — CvPlayer holds m_iOverflowResearch and subtracts it from the next tech's remaining cost in getResearchTurnsLeftTimes100 (https://github.com/f1rpo/AdvCiv/blob/master/CvGameCoreDLL/CvPlayer.cpp lines ~371, 6560-6596, 7971).
- Design note: Civ IV is the one Civ whose future tech had a real per-level mechanical benefit (+1 health/+1 happy), and it was flat cost + linear benefit, uncapped; happiness/health being city-capped resources made it effectively a diminishing-returns sink.

## 4b. Civilization V
- Future Tech: cost 9500 Science (fandom, via WebSearch summary https://civilization.fandom.com/wiki/Future_Tech_(Civ5) ; "slightly under 10,000" per https://forums.civfanatics.com/threads/future-tech-does-something-beyond-score-increasing-mod.543338/ ). Repeatable; +10 score per completion, no other benefit (same sources). [secondary]
- No per-repeat escalation: Civ V DLL CvTeamTechs::GetResearchCost multiplies XML cost by handicap, handicap-per-era, world size, game speed, start era, team size only (Community Patch fork of the released Firaxis DLL, https://github.com/LoneGazebo/Community-Patch-DLL/blob/master/CvGameCoreDLL_Expansion2/CvTechClasses.cpp lines 2492-2545).
- Global cost pressure: per-city tech cost penalty, cost *= 1 + base% x N cities (BNW/Community Patch linear; Vox Populi adds 0.2% x C(N,2)); code comment example "base=5% ... 6 cities ... 30% + 0.2%*15 = 33%" (same file lines ~1740-1755). BNW reviews cite +5%/city (https://sullla.com/Civ5/bnwreview.html ) while a fandom page cites 2% (https://civilization.fandom.com/wiki/Science_victory_(Civ5) ) — the value is map-size dependent (WorldInfo NumCitiesTechCostMod).
- Overflow: kept — "Overflow is the leftover research from the previous research. It is automatically rolled into the main progress value" (CvTechClasses.cpp lines 1795-1798; SetResearchProgressTimes100 lines 2406-2449 hands back iOverflow via changeOverflowResearchTimes100).

## 4c. Other games (all secondary — search-engine summaries; primary pages blocked by proxy)
- Old World (Mohawk): deck system — draw 4 tech cards, pick 1, others discarded until reshuffle (Soren Johnson, https://www.designer-notes.com/old-world-designer-notes-4-the-technology-deck/ ; mechanics also https://wiki.hoodedhorse.com/Old_World/Technology ). Tier costs: T1 80, T2 120, T3 200 Science (hoodedhorse wiki); T8 2000 (https://www.oldworldwiki.org/index.php/Technology ). Tier-8 techs are repeatable; each repeat originally +500 (=25% of base), raised in Update #132 (Feb 2025) to "+1000 ... 50% of the original cost each time" (https://mohawkgames.com/2025/02/19/old-world-update-132/ ). Linear escalation. Reward: 1 Victory Point per repeat (player guide https://steamcommunity.com/app/597180/discussions/0/3361398061433714863/ ). Last tier / first tier = 25x.
- Humankind (Amplitude): no repeatable end tech; completing the tree ends the scored game. Players repeatedly asked for one ("fame each time", "+1 combat strength", "cost would rise each time"): https://steamcommunity.com/app/1124300/discussions/0/3047235828267953445/ , https://community.amplitude-studios.com/amplitude-studios/humankind/forums/169-game-design/threads/40529-an-end-game-repeatable-technology-plus-religious-shenanigans
- Endless Space 2 (Amplitude): no repeatable techs found; tech cost = era base x (1 + ~0.16 x techs already researched), additive (https://endless-space-2.fandom.com/wiki/Technology ; https://community.amplitude-studios.com/amplitude-studios/endless-space-2/forums/66-game-design/threads/35901-why-is-the-cost-in-science-of-technologies-increasing ). Global inflation rather than a sink.
- Galactic Civilizations II/III/IV (Stardock): research-cost inflation — every tech researched raises the cost of all unresearched techs, weapons techs more ("more techs you research, the more expensive all future techs will become. Weapon-techs are especially affected", https://forums.sinsofasolarempire2.com/341730/research-cost-inflation ; https://galciv.fandom.com/wiki/Research ). GalCiv IV forum report of tech-count multipliers non-weapon/weapon 1x/2x -> 1.4x/2x -> 2.1x/4.5x -> 2.8x/6.1x (https://forums.galciv4.com/381365/ta-tech-costs-different-civ-same-tech-different-cost/page/1#2607371 , unverified). No vanilla repeatable sink confirmed; GalCiv IV players complain of "Late-game ... Research over-spill waste" (https://www.galciv4.com/article/522627/questions-and-qol-suggestions-regarding-late-game-manufacturing-research-ov ).
- Millennia (C Prompt/Paradox): 10 Ages, "a handful of Techs" each; advance by researching a minimum number of current-Age techs then the next Age (https://www.paradoxinteractive.com/games/millennia/news/ages-part-one ). No repeatable end-tech found.

## 5. Design guidance (designers, postmortems, community analysis)
### 5.1 Endgame research sinks / unspent points
- Every Civ since IV ships a repeatable terminal node so per-turn science is never wasted: Civ IV (+1 health/+1 happy per level), Civ V (+10 score), Civ VI (score; GS adds +5% project production / +1 governor title +50 favor), Civ VII (attribute point + Age progress). Sources in sections 1-4.
- When the sink is absent players complain explicitly: Humankind threads asking for a repeatable end tech whose "cost would rise each time it's researched, so it can't be spammed" (https://community.amplitude-studios.com/amplitude-studios/humankind/forums/169-game-design/threads/40529-an-end-game-repeatable-technology-plus-religious-shenanigans ); GalCiv IV "Late-game ... Research over-spill waste" (https://www.galciv4.com/article/522627/questions-and-qol-suggestions-regarding-late-game-manufacturing-research-ov ).
- Stellaris Dev Diary #11 (2016): developers added "improvement technologies" so long games stay interesting after scripted techs run out (https://admin-forum.paradoxplaza.com/forum/developer-diary/stellaris-dev-diary-11-research-technology.893377/?prdxDevPosts=1 ). Players describe repeatables as "giving your researchers something to do late game" (WebSearch summary of Steam threads).
- Too-strong sinks get nerfed: Civ VII's Feb 2025 patch raised repeat escalation and the July 2025 patch replaced Wildcard with random attribute points because the repeatable rewards made the attribute system's impact "greater than intended" (section 2). Old World doubled its per-repeat increment from +500 to +1000 (Update #132). Paradox removed research-speed sources and "+20% Research Field" techs in the 3.11 tech rebalance (Dev Diary #325 -> #329) because "the speed at which we're able to burn through the entire technology tree is much higher than is healthy" and repeatables were reached too early (https://forum.paradoxplaza.com/forum/developer-diary/stellaris-dev-diary-325-3-10-3-pyxis-released-d2aa-further-beta-plans.1615735/page-7 , https://forum.paradoxplaza.com/forum/threads/stellaris-dev-diary-329-technology-beta-wrapup.1622766/page-5 ). Lesson: never put research-speed (self-compounding) effects on a repeatable; keep the first repeatable expensive enough that it arrives late.
### 5.2 Diminishing returns vs cost growth (what the shipped games actually do)
- No shipped game uses geometric (g^n) cost growth on a repeatable. Civ IV/V/VI: flat cost per repeat; Stellaris: linear +10% of base per level (+32% in 1.x); Old World: linear +25% then +50% of base per level; Civ VII: "increase more when repeated", magnitude undisclosed.
- Benefits are flat per level (Stellaris +5%/+10% additive, Civ IV +1/+1, score/VP), and diminishing returns arise implicitly (additive % on a growing base: Stellaris weapon L1->L2 is +5% relative, L10->L11 is +3.4%) or via hard caps (Stellaris build speed/cost, naval cap, command limit, starbase capacity capped at 5 levels).
- Third-party summary of Schreiber & Romero, Game Balance (CRC 2021, https://www.routledge.com/Game-Balance/Schreiber-Romero/p/book/9781498799577 ): exponential curves give cheap early levels and expensive later ones, but uncapped exponentials make "numbers get so big they lose all sense of relativity"; designers switch to linear at some point (https://blog.userwise.io/blog/the-mathematics-of-game-balance ). [secondary]
- Arithmetic for our engine (computed here, not sourced). Benefit of level n = 0.1 x 0.9^(n-1) x gap (cumulative 1-0.9^n: L5 41%, L10 65%, L20 88%, L30 96%). Price per unit of benefit relative to L1 = g^(n-1)/0.9^(n-1):
  | level | g=1.00 | g=1.05 | g=1.10 | g=1.20 | Stellaris (linear cost, additive % benefit) | Old World (linear +50%, flat VP) |
  |---|---|---|---|---|---|---|
  | 5 | 1.5x | 1.9x | 2.2x | 3.2x | 1.7x | 3.0x |
  | 10 | 2.6x | 4.0x | 6.1x | 13.3x | 2.8x | 5.5x |
  | 20 | 7.4x | 18.7x | 45.3x | 236x | 5.7x | 10.5x |
  | 30 | 21x | 87x | 337x | 4200x | 9.6x | 15.5x |
  Cumulative cost to reach L10 / L20 in units of BaseCost: g=1.0 10/20; g=1.05 12.6/33.1; g=1.10 15.9/57.3; g=1.20 26.0/186.7.
  => Our "10% of remaining gap" benefit ALREADY supplies the diminishing return (x1.11 per level). Stacking g=1.2 on top makes marginal price grow x1.33 per level (doubling every ~2.4 levels) — 2-4x steeper than any shipped reference by L10 and >20x steeper by L20; in practice players stop at L8-12 and the sink stops absorbing points, re-creating the unspent-points problem.
### 5.3 Early-game pacing
- Sid Meier: a game is "a series of interesting decisions" (GDC 2012 "Interesting Decisions", https://www.gamedeveloper.com/design/video-sid-meier-explores-interesting-decisions-in-gameplay ).
- Soren Johnson (Old World): draw 4 cards, choose 1, discard rest -> "no longer a golden path through the tech tree, each individual choice is more meaningful" (https://www.designer-notes.com/old-world-designer-notes-4-the-technology-deck/ ); on Civ IV's tree: veterans could "remember the exact order of the ten techs" to Gunpowder (https://civfanatics.com/2021/08/18/soren-johnsons-designer-notes-about-the-tech-tree/ ). [secondary summaries]
- Measured openings: Civ VI first-tier tech 25 at ~2.7 Science/turn = ~9-10 turns, 50-cost tier ~18 turns unboosted / ~9 boosted, first civic ~15 turns; 3 techs available at turn 1 (section 1). Civ IV first techs 40-60 beakers (section 4a). Old World tier 1 = 80 science, 4 cards offered. Civ VI Standard = 500 turns for 68-77 techs (~6.5-7.4 turns per tech average); Civ IV Normal = 500 turns for 92 techs (~5.4 turns per tech average).
- Cost ratio first->last real tech: Civ VI 86x (25->2155); Civ IV 200x (40->8000); Old World 25x (80->2000); Stellaris ~60x (T0 500 -> T5 median 30000). Terminal repeatable vs last real node: Civ VI 1.16-1.18x, Civ IV 1.25x, Civ VII 1.18x, Old World 1.0x (then +50%/repeat), Stellaris 1.67x median T5 (5x median T4).

## Comparison table
| Game | Repeatable node(s) | First-level cost (vs last real node) | Cost growth per repeat | Benefit per level | Categories |
|---|---|---|---|---|---|
| Civ IV BtS | Future Tech | 10000 (1.25x Robotics/Fusion 8000) | none (flat) | +1 health, +1 happiness in every city; linear, uncapped | health, happiness |
| Civ V BNW | Future Tech | 9500 [secondary] | none (flat); global +%/city tech cost | +10 score | score only |
| Civ VI base | Future Tech / Future Civic | 2500 (1.16x) / 3200 (1.11x) | none (flat) | score (ScaleByCost, x2) | score only |
| Civ VI GS | Future Tech / Future Civic | 2600 (1.18x of 2200) / 3500 (1.09x) | none (flat) | Tech: +5% city-project production; Civic: +1 governor title, +50 Diplomatic Favor; + score [secondary] | production, governance/loyalty, diplomacy |
| Civ VII | Future Tech (all Ages) / Future Civic | Antiquity 1255, Exploration 4250, Modern 10000 (1.18x of 8500); Modern Civic 8000 [secondary] | "increase more when repeated" (undisclosed) | +1 random attribute point, +10 Age progress, next-Age tech boost; Civic +1 Artifact (Modern) | leader attributes, age progress |
| Stellaris 4.5.2 | 25 repeatables | 50000 (1.67x median T5, 5x median T4) | linear +5000 (+10% of base) | +5% (economy, weapons, armor, shields, unity) / +10% (strike craft, platforms, army, build speed) / +20 cap; some capped at 5 levels | minerals, energy (trade), food, unity (culture), build speed/cost, weapon damage & fire rate, armor, shields, army, platforms, naval cap, command limit, starbases, leader lifespan |
| Old World | Tier-8 techs | 2000 (1.0x; tier 1 = 80) | linear +1000 (+50% of base; was +500) | 1 Victory Point [secondary] | score |
| Humankind | none | — | — | — (players request one) | — |
| Endless Space 2 | none found | — | global +~16%/tech researched, additive | — | — |
| GalCiv II-IV | none (vanilla) | — | global inflation per tech researched; weapons x2 | — | — |
| Millennia | none found | — | — | — | — |
| **Ours (current)** | final-Age repeatables | BaseCost | geometric g=1.2 | 10% of remaining gap to operand cap | — |

## Recommendations for our engine
1. costGrowth g: lower from 1.2 to **g = 1.05** (acceptable band 1.00-1.08). Reason: the "10% of remaining gap" benefit already decays x0.9 per level, so price-per-benefit grows (g/0.9) per level. At g=1.2 that is x1.33/level -> 13x the L1 price at L10 and 236x at L20, versus 2.8x/5.7x (Stellaris) and 5.5x/10.5x (Old World) in shipped games; at g=1.05 it is 4.0x at L10 and 18.7x at L20 — inside/just above the shipped band through L10, and cumulative cost to L10 is 12.6 x BaseCost (vs 26.0 at g=1.2). No shipped 4X uses geometric cost on repeatables at all; all are flat or linear. If a non-geometric form is allowed in the data, linear `cost_n = BaseCost x (1 + 0.10 x (n-1))` (Stellaris shape) is the closest reference match.
2. Benefit shape: keep the asymptotic "close 10% of remaining gap to cap" — it is a stronger, safer version of what references do with level caps (Stellaris caps 5 of 25 at 5 levels) and it can never exceed the operand cap (no runaway). Do NOT make any repeatable feed Research/Civics Points income (Stellaris removed research-speed sources for burning through the tree too fast; Civ VII nerfed attribute rewards).
3. First-level cost: **BaseCost ≈ 1.2-1.5x the most expensive non-repeatable node of the final Age** (Civ IV 1.25x, Civ VI 1.16-1.18x, Civ VII 1.18x; Stellaris 1.67x is the high end). In turns of income: roughly 1.2-1.5x the turns a final-Age node takes at the moment the Age opens; with Civ-like averages of ~6.5-7.4 turns per tech over a 500-turn Standard game that is ~8-10 turns of income for level 1, falling as income grows.
4. Benefit categories players expect (each maps to a shipped precedent): stability/happiness (Civ IV +happy), health (Civ IV +health), loyalty/governance (Civ VI GS governor title; Stellaris historical edict duration/admin capacity), culture/unity (Stellaris Monthly Unity +5%), production/build speed (Civ VI GS project production +5%; Stellaris build speed +10%, building cost -5%), food (Stellaris +5%), trade/gold (Stellaris energy credits +5%; Civ VI favor), military strength/weapons (Stellaris damage, fire rate, armor, shields, army damage/health — the largest family, 15 of 25), science (only as a non-compounding effect, or omit — no reference ships a research-speed repeatable today).
5. Opening pacing target: first Technology node ≈ **8-10 turns** of turn-1 Research income (Civ VI 25 cost / ~2.7 per turn ≈ 9-10), halved by a boost if we have one (Civ VI boosts 50% base, 40% from R&F); first Civics node ≈ 10-15 turns of turn-1 Civics income (Civ VI Code of Laws ~15). **3-4 meaningful choices open at turn 1** (Civ VI 3 cost-25 techs; Old World 4 cards), widening to ~6-8 within the first 20-30 turns. Real-node cost ratio first->last of ~60-90x (Civ VI 86x, Stellaris ~60x; Civ IV 200x and Old World 25x are the extremes).
6. Overflow: carry per-turn overflow into the next node (Civ IV, V, VI, VII all do — Civ IV/V verified in DLL code). Ensure one-shot grants add to, not overwrite, the overflow pool (Civ VI's community-documented bug). With banked Civics Points the repeatable must always be selectable, so points are never stranded.
