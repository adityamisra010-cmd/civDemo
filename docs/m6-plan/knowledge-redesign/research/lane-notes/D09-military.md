# D09 — Military technology: lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified or describes existing behaviour unless it
is labelled MEASURED.** Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED.
Canonical data: `data/proposal-parts/D09-military.json` (34 existing records, 6 new). Baseline: `edba2b5`.

## 1. Summary

The military tree keeps its shape. What changes is what is causally wrong or missing:

- **Eight prerequisite corrections at unchanged cost** replace a non-causal or imprecise edge with the knowledge that
  actually enabled the weapon: `cannon_early` (refined saltpetre; cast bronze OR wrought iron — not cast iron) ·
  `matchlock` (corned powder, not the crossbow) · `cannon_cast_bronze` (closed-mould casting with its alloy) · `saddle`
  (sewing) · `trebuchet` (from the new traction trebuchet, not torsion engines) · `composite_bow` (hide glue, not birch
  tar) · `tank` (rifled guns and machine guns, not cast-iron cannon) · `stealth` (now requires the aeroplane whose unit
  it grants). Four of these answer requests from D04, D02 and D07, accepted.
- **The empty A8 is filled.** Today the military subtree jumps from the flintlock (1610) to the tank (1916) [MEASURED:
  no military node at A8], while `unit-families.json` already declares "rifled firearms", "rifles", "steel guns" and
  "machine guns" for A7–A9 identities with no knowledge behind them. Four causally chained nodes fill it: `rifling` (A7) →
  `breech_loading_rifle` and `rifled_artillery` (A8) → `machine_gun` (A8) → the tank.
- **One split:** `guided_munitions` (189,240 RP) bundled 1940s–1950s guided missiles with 1970s precision-guided munitions
  and forced the laser (1960) before any guided weapon. Split into `guided_missile` + precision-guided munitions (key 403
  kept), workload conserved exactly.
- **Two merges** of variant dead ends into their survivors: `siege_tower → siege_ram`, `crossbow_repeating → crossbow`.
- **One new trunk node**, `traction_trebuchet` (A5): the real ancestor of the counterweight trebuchet and an independent
  (East Asian) route to siege artillery.
- **Dead ends** get an owner or an explicit justification; unit grants stay eligibility only (M7 battle layer). Five
  PROPOSED unit entities that link new knowledge to existing unit-family identities are an OPEN DECISION for T6.12.

Net workload: lane 693,230 → 742,040 RP (+48,810; +0.44 % of the 11.21 M tree) [MEASURED by scratch script].

## 2. Dispositions (34 owned nodes)

| disposition | count | nodes |
|---|---|---|
| keep, no field change | 11 | bow_simple, sling, chariot, cavalry, naval_ram, torsion_artillery, longbow, cannon_cast_iron, flintlock, ship_of_line, combat_drone |
| keep, capability block only (owner/milestone stated) | 8 | atlatl, stirrup, plate_armour, greek_fire, wheellock, radar, icbm, missile_defence |
| keep, prerequisite corrected (cost unchanged) | 8 | composite_bow, saddle, cannon_early, cannon_cast_bronze, matchlock, trebuchet, tank, stealth |
| keep as merge survivor | 2 | siege_ram (renamed "Battering ram and siege tower"), crossbow |
| keep, repeatable descriptor recount | 1 | frontier_military |
| rebalance | 1 | trace_italienne |
| split (+ rebalance) | 1 | guided_munitions |
| merge (key retired) | 2 | siege_tower → siege_ram, crossbow_repeating → crossbow |
| retire / relocate / replace | 0 | — |
| **new** | **6** | traction_trebuchet, rifling, breech_loading_rifle, rifled_artillery, machine_gun, guided_missile |

(11 + 8 + 8 + 2 + 1 = 30 records in `keep`; with 1 rebalance, 1 split and 2 merges = 34.)

## 3. Split, merges, rebalance — with reasons

**Split — `guided_munitions` (key 403) → retained "Precision-guided munitions" + new `guided_missile`.** Conflation of
guided missiles (Hs 293 rocket-boosted radio-guided anti-ship missile, 1943; radar- and infrared-homing SAMs/AAMs in the
1950s) with precision-guided munitions (laser-guided bombs at the Thanh Hoa bridge, 1972; later satellite guidance)
[RESEARCHED]. Key 403 keeps the part its consumers need (`missile_defence`, `frontier_military`: hit-to-kill and precision
strike). Prerequisites: `guided_missile` = rocket AND radar; 403 = laser AND computer AND guided_missile (rocket replaced
by guided_missile, which requires it; a laser-guided bomb has no motor). Factors 403: novelty 1.0→0.5, difficulty
3.5→3.0 (magnitude 11.0→10.0). Parts 94,620 + 94,620 = 189,240 = original (conserved).

**Merge — `siege_tower` (key 125) → `siege_ram`.** Assyrian rams were housed in wheeled towers and brought up built
ramps (Lachish, 701 BCE) [RESEARCHED]; the unit family already treats them as one identity ("Rams and siege towers").
siege_tower required siege_ram, so every holder keeps every capability. Survivor renamed "Battering ram and siege tower";
factors unchanged (−880 RP of workload).

**Merge — `crossbow_repeating` (key 160) → `crossbow`.** A magazine-fed variant (Chu tomb at Qinjiazui, 4th c. BCE;
service into the late Qing) [RESEARCHED], not a new principle; it made nothing eligible and has no successor (−1,240 RP).
Preserving the ids (S1-33 "wherever possible") was weighed and rejected for both: neither node has a consumer, a
successor or a distinct capability, and the content survives in the survivors' descriptions and capability strings.

**Rebalance — `trace_italienne`.** Missing causal knowledge added: the bastion trace re-engineered the masonry
curtain-and-tower wall (lower, thicker, earth-backed, masonry-faced walls; angled bastions removing the dead ground of
round towers) [RESEARCHED], so it requires `stone_fortification` besides `cannon_cast_bronze` and `geometry_axiomatic`.
This also yields a coherent fortification line: city wall → castle → bastion fort. Three conjuncts ⇒ prereq_complexity
0.5→1.0 (convention); cost 4,180→5,910. Its auto-filled owner was wrong: `building.bastion_fort` is deferred-entity
tier E (M7), not an M6 construction realization [MEASURED: `docs/deferred-entity-realization-plan.md:57-63`].

## 4. Prerequisite corrections on kept nodes (conjunct count and cost unchanged)

| node | old | new | reason |
|---|---|---|---|
| cannon_early | gunpowder AND cast_iron | saltpetre_refining AND (casting_closed OR iron_bloomery) | metal: earliest guns cast bronze in China (Wuwei, contextually 1214–1227; Xanadu 1298) and forged wrought-iron staves and hoops in Europe (Mons Meg type); cast-iron guns are later, already `cannon_cast_iron` (1543) [RESEARCHED]. Propellant: the 1040s formulas (≤ ~50 % saltpetre) were incendiary; guns need high-nitrate powder (D04's request; shift RESEARCHED by D04, necessity of refining INFERENCE); saltpetre_refining implies gunpowder |
| matchlock | cannon_early AND crossbow | cannon_early AND corned_powder | corned powder went first into hand-held guns while serpentine stayed with early cannon (D04's request; RESEARCHED by D04, Hall's corning thesis, dating debated; consistent with D-047 r1 #9). The crossbow edge stood for role succession (crossbowmen → arquebusiers, already encoded by the unit family) and an unsourced trigger/stock transfer (INFERENCE) |
| cannon_cast_bronze | cannon_early AND lost_wax | cannon_early AND casting_closed | within D-047 r1 #9's own reasoning ("bell-founding (lost-wax/closed casting)", RATIFIED — a ruled edge, flagged): bronze guns were cast like bells in closed loam moulds and need the alloy; D02 re-scopes lost_wax onto open casting and cannon_early now admits wrought iron, so lost_wax no longer guaranteed tin bronze — D02's request (option b), accepted; casting_closed requires tin_bronze. lost_wax loses its only dependant (D02's to resolve) |
| saddle | cavalry AND hide_working | cavalry AND sewing | early saddles (Pazyryk) were stitched leather and felt — D07's request, accepted; sewing implies hide_working |
| trebuchet | torsion_artillery | traction_trebuchet | counterweight trebuchet = traction trebuchet with a counterweight; torsion is a different principle (the Thessalonica account contrasts the two) [RESEARCHED] |
| composite_bow | … AND birch_tar | … AND hide_working | laminated with hide or fish glue; birch *bark* was a covering [RESEARCHED] |
| tank | … AND cannon_cast_iron | … AND rifled_artillery AND machine_gun | the 1916 tank carried 6-pdr QF guns ("male") or machine guns ("female") [RESEARCHED] |
| stealth | composites AND radar AND computer | … AND aircraft | it grants `unit.stealth_aircraft` but did not require powered flight |

Agreed but not encodable here (the ids are other lanes' new ids, brief §9) — **to be applied at integration**, conjunct
counts unchanged: `radar` ← D04's `electromagnetic_field_theory` instead of `electromagnetism` (radar is the reflection of
electromagnetic waves; Hertz 1887); `guided_missile` ← D08's `liquid_fuel_rocket` instead of `rocket` (D08 re-scopes the
`rocket` key to the 1957 orbital launch vehicle; left there, guided_missile is wrong and fails the chronology test,
1957 > 1943 CE).

Kept on review: `stirrup`'s iron_bloomery (a low-impact proxy: the earliest dated pair, Feng Sufu 415 CE, is wood
sheathed in gilt bronze and iron [RESEARCHED]); D-047 Part C verdicts on torsion_artillery, cannon_cast_bronze and
combat_drone (RATIFIED) untouched.

## 5. New nodes and why

| id | Age · branch | prereq | cost | why |
|---|---|---|---|---|
| traction_trebuchet | A5 · trunk | sling AND timber_frame | 2,490 | lever-sling engine of Warring States China (Mohist texts, 4th c. BCE), dominant siege artillery of East Asia and the Mediterranean until the 12th c.; true ancestor of the counterweight trebuchet; an alternative route to `unit.siege_engine` |
| rifling | A7 · military | matchlock | 4,180 | spin-stabilised fire (~1500 CE, Vienna/Nuremberg, attribution disputed); hinge of every rifled weapon; rangers identity |
| breech_loading_rifle | A8 · military | rifling AND flintlock AND chemistry_quantitative AND interchangeable_parts | 16,730 | Dreyse 1841 → metallic-cartridge rifles 1860s; fulminate priming + machine-made breech parts; firearm generation 4; line-infantry identity |
| rifled_artillery | A8 · military | rifling AND puddling AND slide_rest_lathe | 19,890 | Armstrong 1855 (wrought-iron built-up), Krupp cast steel 1860s; cannon generation 4; field-artillery identity; the tank's gun |
| machine_gun | A8 · military | breech_loading_rifle | 5,910 | Gatling 1862, Maxim 1884 (black-powder cartridges first, reworked for smokeless 1887–1889) [RESEARCHED]; machine-gun-team identity; the tank |
| guided_missile | A9 · military | rocket AND radar | 94,620 | split part of key 403 (see §3) |

Every new node has a dependant or an entity (no new dead end). All carry era-marked `emerged` texts, so the pinned
count of 83 unmarked texts does not move [MEASURED by scratch check]. Eurekas: two, both `future-system:world events`
(breech_loading_rifle: rival breech-loaders met in battle — Königgrätz 1866; guided_missile: rival bomber threat), so
the evaluable count (17) is unchanged; the total-Eureka pin (81 on 80 nodes) moves by two.

## 6. Unit-family consistency (eligibility only; realized by the M7 battle layer)

Existing grants checked against `Sim.Data/content/unit-families.json` [MEASURED] — all consistent: bow_simple→archers
(A2) · sling→slingers (A1) · chariot→chariots (A3) · cavalry+composite_bow→horse_archers (A4) · stirrup→knights (A6) ·
crossbow→crossbowmen (A6) · longbow→longbowmen (A6 branch) · siege_ram|torsion_artillery|trebuchet→siege_rams (A4) ·
cannon line→bombards (A7) · matchlock|flintlock→musketeers (A7) · naval_ram→war_galleys (A4) · ship_of_line→ships of
the line (A7) · tank→tanks (A9) · icbm→icbm_forces · stealth→stealth_aircraft · combat_drone→combat_drones.

Changed: `unit.siege_engine.requires` gains a fourth alternative — `siege_ram OR torsion_artillery OR
traction_trebuchet OR trebuchet`.

**PROPOSED inert unit entities (OPEN DECISION, T6.12):** each is the `realizedBy` of an existing identity that has none.

| entity | requires | identity (family, Age, declared equipment) |
|---|---|---|
| unit.rangers | rifling | rangers (light_infantry, A7, "rifled firearms") |
| unit.line_infantry | breech_loading_rifle | line_infantry (heavy_infantry, A8, "rifles, bayonets") |
| unit.field_artillery | rifled_artillery | field_artillery (siege, A8, "steel guns, shells") |
| unit.machine_guns | machine_gun | machine_gun_teams (ranged_infantry, A9, "machine guns") |
| unit.cuirassiers | stirrup AND plate_armour AND wheellock | cuirassiers (heavy_cavalry, A7, "horses, cuirasses, pistols") — 16th-c. three-quarter-armoured pistol cavalry [RESEARCHED] |

Non-binding further candidates for T6.12 (not proposed here): composite_archers←composite_bow; light_horse←cavalry;
arquebusiers←matchlock; sharpshooters and carbine_cavalry←breech_loading_rifle; dragoons←flintlock+cavalry. Knights are
deliberately NOT re-gated on plate (knights fought in mail for centuries before full plate).

## 7. Dead ends resolved (owned)

| node | resolution |
|---|---|
| atlatl | explicit justification: oldest mechanical launcher; its consequence (hunting) is baseline and has no system; no successor ⇒ knowledge-only until M11+ (`milestone: M10+`) |
| siege_tower | merged into siege_ram |
| crossbow_repeating | merged into crossbow |
| plate_armour | realization owner M7 via PROPOSED `unit.cuirassiers`; fallback: knowledge-only, M7 equipment candidate |
| greek_fire | explicit justification: regional state secret, formula lost, no successor; candidate M7 naval equipment (naval stays auto-resolve, D-011 §5) |
| missile_defence | explicit justification: distinct defensive capability, no unit family role exists; candidate M7 strike-resolution input (law 2: coefficient inside a named equation) |

Also resolved for another lane: `corned_powder` (D04, the A7 supporting fact) gains its first in-tree dependant
(matchlock).

## 8. Candidates considered and REJECTED

1. **Smokeless powder** (Vieille 1884): no causal dependant in this graph — the Maxim gun began on black powder and the
   rifled breech-loaders predate it (an AND edge would also fail the chronology test). Propellant chemistry sits with
   D04's gunpowder line; left to D04.
2. **Keeping both crossbow and corned_powder on matchlock** (three conjuncts, +1,730 RP by convention): the crossbow edge
   is role succession plus an unsourced transfer, so it is replaced rather than kept.
3. **Corned powder on cast-bronze cannon:** refused by D-047 r1 #9 (RATIFIED: bombards used serpentine powder).
4. **Gunpowder war rocket** (Song fire arrows; Mysore 1780s; Congreve 1804): no unit identity (the siege line goes
   bombards → field artillery → modern rocket artillery) and the liquid-fuel rocket (D08) does not descend from it — a
   new dead end.
5. **Percussion cap** and **Minié rifle-musket** as separate nodes: single-dependant stepping stones; folded into
   breech_loading_rifle (flintlock + fulminate chemistry edges) — click inflation otherwise.
6. **Sword / bronze or iron weapon / mail / scale-armour nodes:** applications of D02 metallurgy already stated in its
   capability strings (`sheet_metal` "sheet armour", `wire_drawing` "mail armour"); identities such as bronze_swordsmen
   have no `realizedBy` by design (ADR-031 addendum precedent).
7. **Military aviation / fighter (synchronisation gear):** `aircraft` (D08) already grants `unit.military_aircraft`; a
   second node adds a click without a decision unless M7 distinguishes air roles.
8. **Torpedo, submarine, destroyer:** vessels are D08's domain — cross-lane request, not added here.
9. **Field fortification / entrenchment (dig-in):** no identity and no consumer; D-011's verb gating is M7's.
10. **Splitting icbm (189k) or stealth (134k):** leaves — nothing waits on them, so a split adds clicks and removes no
    bottleneck.
11. **Relocating A6 subtree nodes (longbow, greek_fire) to the trunk:** dormant-progress and branch-count costs for no
    causal gain; subtree placement is the D-044 R4 abstraction.
12. **Tank ← mechanised_agriculture** (the Holt crawler, 1904, is the tank's real ancestor [RESEARCHED]): the node is
    dated ~1,920 CE, after the tank, so the edge would fail the chronology test — cross-lane request to D01.
13. **combat_drone ← guided_munitions:** the Predator flew unarmed 1995–2001; reconnaissance drones do not need
    precision munitions.
14. **Military institutions** (standing army, conscription, academies, officer corps, drill): D12's; none added.
15. **greek_fire → petroleum_refining edge:** not causal (the formula was lost).

## 9. Conflations found

- Guided missile vs precision-guided munition (split, §3).
- One Assyrian siege-engine complex spread over two nodes; a weapon variant as its own node (merges, §3).
- Gun **knowledge** bound to a later gun **metal** (cannon_early ← cast_iron) and to the incendiary stage of gunpowder;
  an anachronistic gun bound to the tank.
- Role succession presented as knowledge (matchlock ← crossbow).
- Unit grant vs prerequisite knowledge: stealth aircraft eligible without powered flight.
- Player knowledge vs world truth: radar's "reconnaissance at radio range" string reads like a sight effect; it is
  display only — D-040 B3 forbids research unlocking reach; a radar reconnaissance *class* is an M7 candidate.
- Eligibility vs realization: auto-filled owner of `building.bastion_fort` (M6) vs deferred-entity tier E (M7).
- Eligibility vs Age-driven modernization (ADR-031): see open questions.
- Military knowledge vs institution: flintlock's "Drill and the printed manual" strings (D12's practice; kept as text).

## 10. Cost findings

- All 34 owned nodes follow the prereq_complexity convention and, with content-reasoned exceptions, the Age prior
  [MEASURED]: greek_fire institutional 1.75 (state secret); flintlock institutional 2.0; radar, stealth, missile_defence
  institutional 3.0 and icbm, guided_munitions difficulty 3.5 (state crash programmes). Kept.
- Re-scored: trace_italienne 4,180 → 5,910 (convention); guided_munitions 189,240 → 94,620 (split, conserved).
- New-node factors follow the coordinator's rubric (Age prior, convention, breadth = downstream scope, firearm-family
  material 0.0); per-factor notes are in each `costRationale`.
- **Age-core closures** [MEASURED, scratch run of `kr_lib` on the current graph with D09's part applied]: A7
  printing_press 98,940 and A6 heavy_plough 59,470 unchanged; the cannon line lies on the A8 core path (steam_rotary ←
  cylinder_boring ← cannon_cast_iron ← cannon_cast_bronze ← cannon_early), so the saltpetre_refining edge (+4,180) and the
  lost_wax removal (−1,480) move every core closure containing steam_rotary by +2,700 RP — A8 steam_rotary 389,980 →
  392,680; A9 electricity_generation 560,960 → 563,660 and internal_combustion 629,960 → 632,660 (+0.4–0.7 %).
  Reachability is unchanged; cast_iron stays in those closures through steam_atmospheric.
- **Cheapest closures** [MEASURED, same run]: cannon_early 70,950 → 54,820 · cannon_cast_bronze 91,410 → 73,800 ·
  matchlock 91,630 → 61,090 · trebuchet 41,580 → 11,890 · trace_italienne 106,870 → 95,390 · saddle 7,400 → 8,710 ·
  composite_bow 5,740 → 5,200 · tank 740,890 → 879,190 · stealth 1,806,380 → 2,036,590 · guided_munitions 2,917,260 →
  2,943,610 · missile_defence 2,957,640 → 2,960,340.
- **Pacing** [MEASURED, `measurements/baseline-node-completions.csv`]: in the average runs the D09 trunk and A6 nodes
  complete (trebuchet at turns ~1,120–1,145; corned_powder ~1,190 on seed 42), cannon_early and every A7+ node never do.
  traction_trebuchet (2,490) will be taken by the cheapest-first driver where siege_tower and crossbow_repeating were.

## 11. Alternative routes

- Siege artillery: the lever-sling route (traction_trebuchet) beside rams and torsion engines — an independent invention.
- Gun metal: cast bronze (China) OR forged wrought iron (Europe) for the first cannon — regional materials.
- No OR was added to soften a cost.

## 12. Compatibility summary

- **Saves:** merged keys 125 and 160 leave dormant rows (no capability lost: their holders hold the survivors); key 403
  keeps its retained part and old holders do not receive `guided_missile` (its only consumer is 403); progress on 403
  ≥ 94,620 completes next step with no RP spent; prerequisite changes make partial progress dormant until the new
  prerequisites hold. The package-level alias/remap seam is the OPEN DECISION.
- **Tests and CI:** corpus sync (key gaps < 425), TechnologyCount and branch counts, the corpus-name pin (siege_ram
  rename), finite-per-subtree pin (military 36 → 41 + other lanes), Eureka count pin, AI-empire goldens, the three CI
  `--check` docs. Not touched: allocation-scenario keys, v33 fixture key 2, `AgeProgressionTests` ids (bow_simple, sling).
  If the proposed entities are ratified: entity lists, unlock-audit class counts, `unit-families.json` `realizedBy`.
- **Schema:** none.
- **Integration dry-run:** `tools/integrate.py` on a scratch mirror reported no D09 problem, alone (provisional keys
  427–432) and with the ten lane parts present at the time. A chronology/Age check over that integrated graph found one
  D09 issue — guided_missile (1943 CE) ← the re-scoped rocket (1957 CE) — and none once the two agreed integration
  substitutions (§4) are applied.

## 13. Cross-lane requests (also in the JSON) and requests received

Sent: D02 (option (b) accepted; lost_wax loses its only dependant; edges changed) · D07 (saddle accepted; keep hide_working,
sewing, timber_frame, stone_fortification, interchangeable_parts, slide_rest_lathe) · D08 (re-point guided_missile's
rocket atom at integration; keep aircraft; consider torpedo and diesel-electric submarine for the destroyer identity) · D01 (re-date
mechanised_agriculture to the 1904 crawler so the tank can require it) · D11 (keep thermonuclear; icbm's milestone
follows D11's nuclear recommendation) · D04 (keep the propellant and physics ids; apply the radar substitution at
integration).

Received and dispositioned: **D04** — matchlock ← corned_powder: ACCEPTED (replaces crossbow); cannon_early ←
saltpetre_refining: ACCEPTED (replaces gunpowder); radar ← electromagnetic_field_theory: AGREED, to be applied at
integration. **D02** — name the alloy in cannon_cast_bronze: ACCEPTED as option (b), lost_wax → casting_closed (lost_wax
loses its only dependant; D02's to resolve). **D07** — saddle ← sewing: ACCEPTED. **D08** — re-point the rocket edge to
liquid_fuel_rocket: ACCEPTED in substance (after the split the edge sits on guided_missile; to be applied at
integration); icbm stays on rocket; unit.missile's move noted. **D05** — signalling ids (optical_telegraph, telegraph,
radio, radio_broadcasting): noted; D09 has no command-latency consumer (D-039 A3 is M7's). **D12** — no technology nodes
for standing armies, conscription, general staffs or land-for-service and no civic-named technology prerequisite:
complied.

## 14. Open questions

1. **OPEN DECISION (T6.12):** add the five inert unit entities of §6 in M6 content, or defer to M7. Recommendation: add
   them inert, like the 23 — M7 consumes M6's capability state and should not invent the knowledge mapping.
2. **OPEN DECISION (M7 contract):** ADR-031 modernization is knowledge-blind; state whether recruitment follows the
   entity's knowledge requirement or the Age realization.
3. **OPEN QUESTION (T6.12):** no air- or missile-defence role exists; radar, guided_missile and missile_defence stay
   knowledge-only until one is added or M7 reads them inside resolution equations.
4. **OPEN QUESTION (M7):** D-011's era-gated verbs (bombard, air strike, dig-in) must be capability-gated (D-039 A5);
   candidate gates are siege engines/rifled_artillery and aircraft/guided_munitions; dig-in has no knowledge node.
5. **Package OPEN DECISION:** alias/remap seam for merged and split keys.
6. **INTEGRATION NOTE:** apply the two agreed substitutions of §4 (radar → electromagnetic_field_theory; guided_missile →
   liquid_fuel_rocket); without the second, guided_missile fails the chronology test once D08's split lands.

## 15. Sources (RESEARCHED claims)

Direct page fetches were blocked by the session proxy; the claims below rest on search-result summaries of the cited
pages (fact-check status: partially-checked). A citation records where a claim can be checked, not proof. The corning
and saltpetre claims behind the accepted D04 requests are D04's research.

- Traction/counterweight trebuchet: https://en.wikipedia.org/wiki/Mangonel · https://en.wikipedia.org/wiki/Trebuchet ·
  https://deremilitari.org/?p=2041
- Composite bow glue and coverings: https://primitiveways.com/pt-composite_bow.html · https://archeryhistorian.com/the-horn-bow/
- Rifling: https://en.wikipedia.org/wiki/Rifling · https://en.wikipedia.org/wiki/Gaspard_Kollner
- Breech-loaders: https://en.wikipedia.org/wiki/Dreyse_needle_gun · https://www.forgottenweapons.com/hall-model-1819-a-rifle-to-change-the-industrial-world/
- Rifled artillery: https://en.wikipedia.org/wiki/Armstrong_gun · https://wikipedia.com/wiki/Krupp_gun
- Machine gun: https://www.ebsco.com/research-starters/history/maxim-patents-his-machine-gun · https://www.imfdb.org/wiki/Maxim_machine_gun
- Early cannon: https://en.wikipedia.org/wiki/Xanadu_Gun · https://en.wikipedia.org/wiki/Huochong · https://en.wikipedia.org/wiki/Mons_Meg · https://en.wikipedia.org/wiki/Bombard_(weapon)
- Bastion fort: https://en.wikipedia.org/wiki/Bastion_fort · https://www.cabinet.ox.ac.uk/node/7317
- Assyrian siege engines: https://en.wikipedia.org/wiki/Siege_of_Lachish · https://en.huji.ac.il/news/siege-ramps-and-breached-walls-ancient-warfare-and-assyrian-conquest-lachish
- Repeating crossbow: https://en.wikipedia.org/wiki/Repeating_crossbow
- Stirrup: https://en.wikipedia.org/wiki/Stirrup
- Cuirassiers: https://en.wikipedia.org/wiki/Cuirassier · https://museumhameln.de/dauerausstellung_engl_fnz32/
- Matchlock and trigger history: https://en.wikipedia.org/wiki/Matchlock · https://api.rockislandauction.com/riac-blog/trigger-mechanism
- Corning (D04's source; not re-verified here): https://www.press.jhu.edu/books/title/3016/weapons-and-warfare-renaissance-europe
- Tank: https://en.wikipedia.org/wiki/Tanks_in_World_War_I · https://en.wikipedia.org/wiki/British_heavy_tanks_of_the_First_World_War
- Guided weapons: https://en.wikipedia.org/wiki/Henschel_Hs_293 · https://en.wikipedia.org/wiki/Thanh_H%C3%B3a_Bridge · https://en.wikipedia.org/wiki/Laser-guided_bomb
