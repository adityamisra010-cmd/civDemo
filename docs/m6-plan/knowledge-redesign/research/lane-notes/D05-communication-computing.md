# D05 — Communication & computing: lane notes

**Status: PROPOSED.** Nothing here is ratified or implemented. Canonical data:
`data/proposal-parts/D05-communication-computing.json` (30 existing records, 6 new). Labels: MEASURED · RESEARCHED ·
INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED. "Static closure" figures are INFERENCE from
`tools/kr_lib.py` closures (cheapest satisfying set; no RP trajectory).

## 1. Summary

The writing, record and money core is kept intact: every Age fact (a3_writing, a4_alphabet, a5_coinage, a6_paper,
a6_finance, a7_printing, a8_telegraph, a9_radio), the research-stage atom `stamp_seal`, and every civic prerequisite
keep their ids and consumers. The lane makes four kinds of change.

1. **Causal placement.** The six A6–A7 printing and finance nodes that ADR-029 rule R1 put in Engineering only because
   of their Age (`woodblock_print`, `movable_type_ceramic`, `movable_type_metal`, `paper_money`, `bill_of_exchange`,
   `printing_press`) move to the trunk. Printing is a craft innovation and did not need organized scholarship. Every
   prerequisite of the six is already a trunk node.
2. **Two bundle splits that conserve or reduce workload.** `radio` becomes wireless telegraphy (the key is kept and the
   node is re-dated to A8), plus a new `radio_broadcasting`. This removes a causal inversion with `vacuum_tube`.
   `satellite` keeps the orbital satellite (one factor re-scored), plus a new `communications_satellite`.
3. **Four new nodes with real dependencies or owners:** `mesoamerican_script` (D03 asked for it), `automatic_switching`,
   `punched_card_tabulation` and `optical_telegraph`.
4. **Dead ends resolved:** `movable_type_metal`, `fibre_optics` and `cellular_digital`, plus D07's `jacquard`. All 30
   records also get capability blocks; the auto-filled owner for `trade` was wrong and is corrected.

The stored-program computer (535,250 RP) belongs to D06. Its split is requested with a concrete design that uses two of
D05's new nodes.

Validation (MEASURED, scratch copy of the package; package files untouched): `integrate.py --allow-incomplete` +
`validate.py --no-corpus`. With the D05 part alone the result is ERROR 0, WARN 90 (baseline 93): the knowledge-only
dead-end count (V09) falls from 91 to 87, and one V17 appears (printing_press at A7 in the trunk, a requested exception).
At the final check ten parts were present (D01–D05, D07–D10, D12). Integrated together, D05 contributes no ERROR. The 6
ERRORs come from D08 records that use the milestone value 'M11+', which is not in the validator's set.

Other lanes' requests are answered in §11. Three of them are accepted **at integration**, because they name new ids
from those lanes:

- radio ← D04's `electromagnetic_field_theory`
- hacksilver ← D02's `cupellation`
- printing_press ← D07's re-pointed `type_founding`, with a material re-score — conditional on D07 moving
  type_founding to the trunk.

## 2. Dispositions (30 existing + 6 new)

| disposition | count | nodes |
|---|---|---|
| keep | 22 | token_counting, stamp_seal, proto_writing, cuneiform, hieroglyphic, chinese_script, papyrus, abjad, alphabet_vowels, brahmi, hacksilver, coinage_electrum, paper, telegraph, submarine_cable*, telephone, television*, internet, mobile_phone*, fibre_optics†, cellular_digital†, trade‡ |
| relocate (engineering → trunk) | 6 | woodblock_print, movable_type_ceramic, movable_type_metal†, paper_money, bill_of_exchange, printing_press |
| split + rebalance | 1 | satellite → + communications_satellite |
| split + relocate (Age A9 → A8) + rebalance | 1 | radio → + radio_broadcasting |
| merge / replace / retire | 0 | — |
| new | 6 | radio_broadcasting, communications_satellite, mesoamerican_script, automatic_switching, punched_card_tabulation, optical_telegraph |

\* The prerequisite is rewired and the cost is unchanged (the requirement count is unchanged). television and
mobile_phone move onto split or new nodes; submarine_cable moves from screw_propeller to steamboat at D08's request.
† Dead end, resolved through the capability block or a cross-lane dependant. ‡ The capability owner is corrected.

**Accepted at integration.** A lane file may name only existing ids, so these changes are recorded in the records'
`changes` but not written into the JSON:

- `radio`: `electromagnetism` → D04's new `electromagnetic_field_theory`.
- `hacksilver`: `gold_silver_native` → D02's new `cupellation`.
- `printing_press`: `casting_closed` → D07's re-pointed `type_founding`, with material 0.75 → 0.25. This one is
  conditional on D07 also moving type_founding to the trunk.

## 3. Printing-press coupling verdict (the lane's key question)

**Question.** `printing_press` is the A7 CORE fact and sits in Engineering, so entering A7 requires the research stage.
On seed 42 it is not reached by 2100.

**Verdict: printing does not causally need organized scholarship. Move it to the trunk. Make no D05-internal split.
Accept D07's type-founding precondition at integration, with a matching material re-score.**

- **Causality (RESEARCHED).** Gutenberg trained as a goldsmith. His system combined an adjustable hand mould and a
  type-metal alloy, an oil-based ink, and a press of the kind used for wine, paper and bookbinding (Mainz, c. 1450; the
  42-line Bible was printed on both paper and vellum) [historyofinformation.com #292; UWM Libraries incunabula
  exhibit]. All of these are craft knowledge. In this graph the causal set is `casting_closed` (the mould),
  `mechanics_archimedean` (the screw press, per its capability strings) and `paper`. All three are already trunk nodes,
  **so nothing else has to move.** The Engineering placement came from ADR-029 rule R1 ("stage closure + A1–A5 → trunk;
  the rest by domain"). R1 is an overridable implementer resolution; it places nodes by Age, which contradicts D-044 R4
  ("If the existing graph contains nodes that logically remain in the main trunk, keep them in the trunk") and S6 (Age is
  metadata).
- **Factors kept in this file (8.75 → 39,780 RP).** Only material changes, and only with D07's type_founding edge
  (next point).
  - Novelty 2.0. In this graph the press does not require woodblock printing; it is the independent European route,
    and for the civilization researching it, it founds typographic printing. Paper, woodblock printing, the telegraph
    and radio are all scored 2.0.
  - Difficulty and institutional are at the A7 prior.
  - Material 0.75 covers type metal and oil ink.
  - Breadth 1.5 covers books, periodicals, mass schooling, scientific societies, patent, anatomy and type founding.
  - Prerequisite complexity 1.0 for three requirements.

  It is the most expensive A7 node (next: steam_atmospheric, 33,450) for content reasons, not by error.
- **No D05-internal split, but a workload-conserving split of the A7 core through D07.**
  - Within D05 the mould, alloy, ink and press work as one system, so no part of them is carved out here.
  - D07 has re-pointed its existing `type_founding` (key 216) from `printing_press` to `casting_closed`. Casting type
    in an adjustable hand mould was the press's precondition, not its sequel.
  - D05 accepts `printing_press` = `type_founding AND mechanics_archimedean AND paper` at integration. The requirement
    count is unchanged.
  - The type-metal content now sits in type_founding, so material drops 0.75 → 0.25 (8.25 = 28,130 RP). Press plus
    type_founding is 36,490 RP against 39,780 (−8.3 %).
  - This is conditional on D07 also moving type_founding to the trunk, since a trunk node may not require a subtree
    node. That would be a second V17 exception, for a goldsmith's craft. Otherwise the acceptance lapses and the press
    keeps casting_closed.
  - With the edge, the press closure becomes 95,650 RP and patent's 103,560 RP (static closure). It is not written
    into the JSON now, because today's type_founding would make a cycle (it still requires the press) or a
    trunk-closure error.
- **Effect on A7 entry: none in practice.**
  - The CORE fact loses its hidden stage gate. Its effective requirement falls from press closure ∪ stage (29 nodes,
    107,230 RP) to the press closure alone (22 nodes, 98,940 RP), a difference of 8,290 RP. The nodes dropped are
    ground_stone_early, knapping_oldowan, law_code, legal_code_roman, medicine_hippocratic, medicine_recorded and
    stamp_seal (static closure). This removes the A7 CORE's exposure to the civic atom `legal_code_roman`, which would
    become binding under D-061 Civics Points pacing (docket C.1).
  - The earliest possible completion of the press does not move in the measured runs. The press's own closure already
    contains `geometry_axiomatic` through mechanics_archimedean, and that is the stage atom that completes last in every
    measured run, driver and AI alike (baseline-progression §0 item 2, labelled INFERENCE there).
  - Ages advance one at a time.
    - **A6 entry** becomes stage-free with D01's relocation of `heavy_plough` (in D01's part). Three supporting
      categories can then be met before the stage: technological (`paper` or `hindu_arabic`), material_economic
      (`water_mill`), and institutional_social (`bill_of_exchange`, after D05's move) or the 18-settlement systemic
      fact.
    - **A7 entry** stays stage-coupled through its supporting facts: 3 of 7 across at least 3 categories are required,
      and every technological or systemic option (caravel D08; corned_powder and mechanics_newtonian D04) is a subtree
      node, while military realization is pending M7.
  - AI polity: printing_press completes in 1598–1630 CE on seeds 1, 2 and 42 and in 1831 CE on seed 7 (INFERENCE,
    order-log reconstruction in the measurement package). The AI enters A7 only in 1924–2033 CE (MEASURED AdvanceAge
    stamps), held back by earlier Ages.
- **Effect on `patent`** (`legal_code_roman AND printing_press`).
  - The effective closure falls from 30 nodes / 111,410 RP (with the stage) to 25 nodes / 106,850 RP (static closure).
  - Under the cheapest-first driver patent stays locked (MEASURED: never completes). The press is never the cheapest
    available node, and its price is unchanged. That is a property of the measurement driver, not a content defect,
    and re-pricing is barred (D-046 §1).
  - The AI completes patent at turns 1339–1362 on seeds 1, 2 and 42 (INFERENCE from the measurement package); no change
    is expected.
- **Other effects.**
  - The AI's A7 core goal now finds an available node before the stage, which closes engine-audit gap Q11 for
    a7_printing.
  - Trunk nodes pay BaseCost, so a polity with an Engineering university loses that university's EffectiveCost
    factor on the press.
  - One V17 WARN (a trunk node at A7): an **OPEN DECISION** for the Director.
- **The same reasoning moves the East Asian printing line and the finance instruments.** Static closures with the
  stage, then without it:

| node | with the stage | without | non-causal requirement removed |
|---|---|---|---|
| woodblock_print | 20 nodes / 54,560 RP | 8 / 31,980 | 22,580 RP |
| paper_money | 29 / 80,830 | 21 / 59,660 | 21,170 RP |
| bill_of_exchange | 27 / 53,480 | 20 / 35,830 | 17,650 RP |
| joint_stock (civic, through bill_of_exchange) | 28 / 56,440 | 23 / 42,520 | 13,920 RP |

  RESEARCHED: the bill of exchange appeared between the 12th and the 14th century as a merchants' and notaries'
  instrument [J. Munro, Univ. of Toronto notes; Bordeaux abstract].
- **Compatibility of the six moves.** Keys are unchanged. Partial progress becomes active, because the moves go INTO the
  always-open trunk. The pinned tests that move are the branch counts (trunk 177 → 184 and Engineering 131 → 130 with
  D05's new nodes) and the 6 recursive Engineering descriptors' `finite_nodes_to_exhaust` (125 → 124). AI-empire
  goldens move (attribute them here). None of the pinned keys 12/17/2/8/52/74/53/2 is involved.

## 4. Splits, relocations and rebalances

### radio → `radio` (wireless telegraphy, A8) + `radio_broadcasting` (A9)

- **Conflation.** "Radio" stood both for Marconi's 1895 spark wireless telegraphy and for 1920 tube broadcasting.
- **Causal inversion.** `vacuum_tube` (D04) required that node, yet Fleming's 1904 valve was invented as a detector for
  wireless telegraphy (RESEARCHED: ETHW "Milestones: Fleming Valve, 1904"), and broadcasting needed the tube. KDKA's 1920
  transmitter was a roughly 100 W vacuum-tube set (RESEARCHED: ETHW "Milestones: Westinghouse Radio Station KDKA, 1920").
- **What keeps the key.** The key stays with wireless telegraphy, which preserves every consumer: a9_radio (described as
  "Radio communication"; precedent: a6_paper names the A5 node paper), building.radio_transmitter, vacuum_tube and
  weather_prediction.
- **Changes to the retained node.** Renamed "Wireless telegraphy (radio)". Age A9 → A8 (1895–1901; both prerequisites
  are A8). Re-scored to the A8 prior: difficulty 3.0 → 2.5, institutional 2.5 → 2.0. That gives 9.5 = 66,900 RP, the
  same factors as `telegraph`.
- **The new node.** `radio_broadcasting` = `radio AND vacuum_tube`, 9.5 = 66,900 RP. The pair totals 133,800 against
  133,810: **workload conserved**.
- **Dependants rewired.**
  - `television` → `radio_broadcasting AND photography`. The count and cost are unchanged; its generation 2 → 3 (family
    broadcasting: radio 1, broadcasting 2, television 3).
  - `mobile_phone` → `radio_broadcasting AND microprocessor AND automatic_switching` (count and cost unchanged).
- **Entity edit (IMPLEMENTATION DEFERRED).** inst.mass_media_broadcast should require radio_broadcasting instead of
  radio.
- **Effect.** The electronics gateway gets cheaper: the vacuum_tube closure falls from 817,350 to 750,440 RP (−66,910),
  and so do those of computer, transistor, laser, radar and television (static closure).
- **Saves.** Key 311 holders do not receive radio_broadcasting (no alias seam).

### satellite → `satellite` (orbital satellite) + `communications_satellite`

- **The bundle.** One node held space access (Sputnik 1, 1957), reconnaissance and relay communications (Telstar 1,
  1962; Syncom 3, the first geostationary, 1964; Intelsat I, 1965) [RESEARCHED: Wikipedia "List of communications
  satellite firsts"; US Centennial of Flight Commission Intelsat essay].
- **The retained node.** It keeps orbit insertion, tracking and telemetry, all five dependants and its prerequisites.
  The reconnaissance string is reworded as a pointed observer.
- **One re-score: material 2.0 → 1.5.** The retained Sputnik-class satellite is an 83.6 kg aluminium-alloy sphere with
  battery-powered transmitters [RESEARCHED: Sputnik 1 references]. Its launcher's materials are scored in `rocket`
  (2.0), and the long-life solar-array and transponder payload moves out. Space follow-ons elsewhere score material
  0.0; `gps` scores 2.0. The cost goes from 12.5 → 12.0, or 535,250 → 378,470 RP.
- **The new node.** `communications_satellite` = `satellite AND solar_pv`, 10.0 = 94,620 RP. A relay must run for
  years. Vanguard 1 (1958) was the first solar-powered satellite, and Telstar 1 carried 3,600 solar cells, while
  battery-only Sputnik 1 fell silent after 21 days [RESEARCHED: RenewEconomy, Vanguard 1 anniversary; Radio-Electronics,
  Nov 1962, via rfcafe].
- **Totals.** The pair is 473,090 RP, 11.6 % below the original (within the one-quarter limit; the reduction is the
  material re-score). The space-cluster closures fall by 223,690 RP (satellite: 3,341,650 → 3,117,960). This is not a
  pacing change: A9 is unreached in every measured run.
- **Recommended unlock-string edits.** Move "satellite communication" to the new node. Drop "GPS", since gps is its own
  node (D08). Reword the reconnaissance string as a POINTED orbital observer whose sight belongs to the fog lane (D08's
  request; D-039 B3, D-040 B3). The key stays on the first artificial satellite, as D08 asked. Its `rocket` atom now
  names D08's orbital launch vehicle, which needs no D05 edit.

### Relocations (§3)

Six nodes, engineering → trunk; keys, factors and prerequisites are unchanged. `radio`'s Age relocation is covered
above.

### Prerequisite corrections on kept nodes

- **`submarine_cable`: `telegraph AND screw_propeller` → `telegraph AND steamboat`** (D08's request; the count and cost
  are unchanged). The 1850 Channel cable was laid from the paddle tug Goliath and the 1851 cable from the hulk Blazer
  under tow, so the screw is not causal [RESEARCHED: atlantic-cable.com cableship pages; Wikipedia "Submarine
  Telegraph Company"].
- **television and mobile_phone:** rewired onto the radio split (§4 above).

## 5. New nodes and why

| id | Age · branch | prerequisites | RP | why it merits a node | owner / status |
|---|---|---|---|---|---|
| radio_broadcasting | A9 · eng | radio AND vacuum_tube | 66,900 | split, see §4 | inst.mass_media_broadcast (T6.11) + M9 media + M7 voice command · deferred |
| communications_satellite | A9 · eng | satellite AND solar_pv | 94,620 | split, see §4; Eureka: `submarine_cable` (cables showed intercontinental demand; TAT-1 1956, RESEARCHED: ETHW; carriers' motive INFERENCE) | M8 contact/diplomacy + M9 media · deferred |
| mesoamerican_script | A3 · trunk | calendar_lunar | 3,520 | One of the few independent inventions of writing, and the only one rooted in calendar notation (calendar dates c. 650 BCE, Zapotec San José Mogote c. 600–500 BCE, Maya San Bartolo c. 300 BCE; Cascajal c. 900 BCE disputed) [RESEARCHED: Wikipedia "Mesoamerican writing systems"; Mesoweb San Bartolo report]. D03 asked for it: it cannot express the Maya vigesimal place value with zero or the Venus tables without a script. Equal cost to its peers (same factors as hieroglyphic), and never cheaper than chinese_script: every holder of calendar_lunar already has tally_notation and bone_tools. | acts through OR groups · knowledge-only |
| automatic_switching | A8 · eng | telephone | 9,950 | Strowger's 1891 patent and the first automatic exchange (La Porte, 1892) let telephone networks scale without operators [RESEARCHED: Wikipedia "Strowger switch"]. Its relay logic is the base of cellular switching (new prerequisite of mobile_phone) and of relay computing: Zuse's Z3 (1941) and Stibitz's Bell Labs calculator (1939–40) were built from telephone relays [RESEARCHED: Computer History Museum timeline]. | M10+ network capacity; dependant mobile_phone · knowledge-only |
| punched_card_tabulation | A8 · eng | jacquard AND telegraph | 14,060 | Mechanised data processing for the 1890 US census; the 1880 count took most of a decade [RESEARCHED: ETHW "Early Punched Card Equipment"; Columbia c250]. The Jacquard loom is the conceptual precedent: Billings suggested it and Hollerith experimented with it, though the conductor's ticket punch was the more direct spark (prerequisite edge INFERENCE). Electrical contact counting comes from telegraph practice. Gives D07's `jacquard` its first dependant. Eureka: `census` (a civic id used as a condition, not as a prerequisite). | M8 administration (census capacity) + D06 computing hook · deferred |
| optical_telegraph | A7 · eng | telescope | 7,030 | Chappe's state semaphore network (Paris–Lille 1794; operators read each tower by telescope; a message took about 32 minutes; c. 534 stations over 5,000 km by the 1840s; replaced 1846–53) [RESEARCHED: Wikipedia "Chappe telegraph"; ETHW "France Builds Visual Telegraph"]. It fills the signalling gap between courier relays and the electric telegraph for the M7 command-latency owner (D-039 A3/A5: latency computed from signal infrastructure) and for M8 administrative reach. | M7/M8 · deferred; signal-line entity IMPLEMENTATION DEFERRED |

Every new node is A7+ in a subtree except the trunk script, has an era-marked `emerged` (the "83 marker-free" pin is
unchanged), and passes the chronology test on its AND-prerequisites. Each also carries the per-factor `costRationale`
and `costAnalog` the rubric requires: xray, solar_pv, hieroglyphic, telephone, jacquard and longitude_problem
respectively.

## 6. Candidates considered and REJECTED

| candidate | why rejected |
|---|---|
| Parchment (writing material) | No consumer distinguishes it: building.library already accepts papyrus OR paper, and the simulation has no regional material differentiation. An equal-cost OR member would only soften costs. |
| Mechanical calculator (Schickard, Pascal, Leibniz; the arithmometer) | Not a causal prerequisite of electronic computing, and no consumer; it would be a new knowledge-only dead end. |
| A D05-internal split of printing_press (a new type-casting node) | That half already exists as D07's type_founding. Creating a second node would duplicate it, so D07's re-pointed node is accepted at integration instead (§3). |
| Re-scoring printing_press's novelty 2.0 → 1.0 | In this graph the press is the independent European founding of typographic printing (it does not require woodblock_print); its peers score 2.0. Only material moves, and only with the type_founding edge. |
| Making printing_press require woodblock_print or movable type | No demonstrated transmission to Mainz, and it would add 14,070 RP to the A7 core closure on a weak causal claim. |
| Splitting `internet` (packet switching vs the Web) | 94,620 RP is below bottleneck scale, and no consumer distinguishes the two. |
| Creating the computer-split nodes in D05 | `computer` is D06's record (lineage and re-score must be theirs); requested instead (§11) to avoid id collisions and one-sided lineage. |
| Khipu (Andean knotted cords) as a records route to census or taxation | The consumers are D12 civics, and taxation's prerequisite set is test-pinned; D12 has recorded it as rejected for now. |
| Signal beacons | Pre-arranged binary fire signals are baseline-like (baseline.fire) and have no consumer. |
| Crossbar and electronic switching; 3G/4G; separate Web generations | Generation padding with no new capability (D-045: "do NOT manufacture generations"). |
| Postal service | An institution or civic (D12), not knowledge of a device. |
| Removing fire_making from token_counting | Many tokens were baked, so the edge is defensible; the cost effect is trivial. |
| Moving a9_radio to radio_broadcasting | Not needed: the fact's text ("Radio communication") fits the retained part. |
| Correcting printing_press's Eureka text ("screw press" against the condition olive_press_beam, a lever press) | Eureka lists are append-only by policy; raised as a question instead. |

## 7. Conflations found

- **Discovery vs application.** radio conflated wireless telegraphy with broadcasting; satellite conflated space access
  with relay communications, plus a "GPS" string that duplicates D08's gps node. Both are split.
- **Eligibility vs adoption/realization.**
  - The telegraph's "order latency collapse" and the optical telegraph act only along BUILT lines (D-039 A5: computed
    from infrastructure).
  - `paper_money` is knowledge; issuing notes is a monetary policy (M8 adoption) once money exists as an institution
    (D-030 defers money; prices are grain-equivalent).
  - The same applies to `coinage_electrum`, `hacksilver` and `bill_of_exchange`: no precious-metal good exists among
    the 14 goods (MEASURED).
- **Research vs physical production.** Paper, papyrus and printing have no paper or book good, so printing houses and
  paper mills have no product until T6.11 or M9 define one.
- **Player knowledge vs world truth.** satellite's "orbital reconnaissance" may only ever be a POINTED M7 asset launched
  through a project. It never reveals the map: D-040 B3 forbids a research unlock of reach, and D-054 keeps "satellites
  revealing the map" out of scope.
- **Tooling (MEASURED).** `integrate.py` `auto_capability` tests the needle "activity" before "trade", so `trade`
  (activity.trade → TradeArbitrageSystem) is auto-filled as "ProductionSystem (labour activity)". D05 overrides it. The
  integrator should check the trade consumer first.

## 8. Dead ends resolved

| node | resolution |
|---|---|
| movable_type_metal | Explicit justification: the independent East Asian route to cast metal type (Jikji 1377), with nothing later that causally needs it. Deferred owner: M9 literacy and culture. |
| fibre_optics | No causal dependant without breaking chronology (internet 1969 predates fibre 1970; the D-047 chronology test). Deferred owner: M10+ long-haul network capacity. |
| cellular_digital | D06 is asked to make `smartphone` require it instead of mobile_phone (the smartphone's defining capability is mobile data). Deferred owner: M10+. |
| jacquard (D07's) | Gains the dependant `punched_card_tabulation`. |

Deferred (J-class) nodes keep their T6.11 owners: papyrus, paper_money, submarine_cable, television, internet,
mobile_phone, telephone and woodblock_print. New leaves name owners: communications_satellite (M8/M9),
optical_telegraph (M7/M8) and punched_card_tabulation (M8, plus the D06 hook).

## 9. Cost findings

- **Lane workload** (integrator output): 1,090,340 → 1,062,730 RP (−27,610; −2.5 %).
  - Splits: radio is conserved; satellite falls by 62,160 RP (material re-score).
  - New non-split nodes add 34,560 RP.
- **Bottleneck relief** (static closure):
  - vacuum_tube −66,910 RP (the electronics gateway).
  - satellite and its five dependants −223,690 RP.
  - mobile_phone +9,940 RP (automatic_switching).
  - No node is re-priced for pacing. A9 is unreached and the press's timing is bound by geometry_axiomatic, so measured
    pace is unaffected.
- **The A7 core with D07's edge** (at integration, conditional): printing_press 39,780 → 28,130 RP (material
  0.75 → 0.25; the type-metal content moves to type_founding at 8,360). The pair is 36,490 RP (−8.3 %), and the press
  closure goes from 98,940 → 95,650 RP. This is content, not pacing: the press's timing is bound by geometry_axiomatic,
  and the cheapest-first driver never targets it.
- **Peer inconsistencies noted, not acted on** (other lanes): space follow-ons score material 0.0 while gps scores 2.0.
- **Engineering-university factor.** Six relocated nodes now pay BaseCost.

## 10. Alternative routes

- **Writing:** four independent script routes.
  - tokens → proto-cuneiform → cuneiform
  - tokens → hieroglyphic
  - tally + bone → Chinese
  - calendar → Mesoamerican (new)

  The costs are equal, so the cheapest closure is still chinese_script and there is no softening.
- **Printing:** the East Asian line (woodblock → ceramic → metal type) and the European press stay independent, with no
  edge between them because no transmission is demonstrated. Both reach building.printing_house (OR).
- **Computing (requested from D06):** a relay route (`automatic_switching`) OR a tube route (`vacuum_tube`) to
  programmable computation.
- **Surfaces:** papyrus OR paper for the library (unchanged).

## 11. Cross-lane requests (full text in the JSON)

- **D06:**
  - Split `computer` into a programmable-computation part, `(vacuum_tube OR automatic_switching) AND calculus`
    (optionally `AND punched_card_tabulation`), and the stored-program computer (key 315: keeps a9_computing and its
    14 dependants), at about 11.5 + 11.5 so workload is conserved.
  - Make `smartphone` require `cellular_digital` in place of mobile_phone.
  - Keep the id `solar_pv`.
- **D04:**
  - **Accepted:** radio requires `electromagnetic_field_theory` at integration.
  - Keep vacuum_tube requiring radio; do not create a cycle through radio_broadcasting.
  - **CONFLICT on double_entry:** D04 moves it to Engineering "beside bill_of_exchange", which D05 moves to the trunk.
    Recommended: double_entry → trunk, since all its prerequisites are then trunk. The alternative is to revert
    bill_of_exchange.
- **D08** (replies):
  - The satellite key stays on the first artificial satellite and keeps `rocket`, which after D08's split is the
    orbital launch vehicle (no edit).
  - The reconnaissance string is reworded as a pointed observer, and the "GPS" string is dropped.
  - **Accepted:** submarine_cable now requires `steamboat`.
- **D07** (reply):
  - **Accepted at integration:** printing_press requires `type_founding`, with material 0.75 → 0.25. This is
    **conditional on type_founding moving to the trunk**; otherwise it lapses.
  - jacquard gains the dependant punched_card_tabulation, as D07's record anticipates.
- **D02** (reply): **accepted at integration:** hacksilver requires `cupellation`.
- **D01:** acknowledged. Your heavy_plough move makes A6 entry stage-free.
- **D03:** mesoamerican_script added. Please add it to the OR groups you offered (surveying, geometry_practical,
  calendar_civil, astronomy_records) and to Maya-specific nodes; not to numeral_sexagesimal.
- **D12:**
  - No edit needed: patent and joint_stock lose their stage gate through the trunk moves, and none of the nodes D12
    named is split.
  - Optional: add mesoamerican_script to the writing OR groups.
- **D10:** optional — medicine_recorded's OR group.
- **D09:** signalling ids for command consumers.
- **Integration (entity requirements):**
  - inst.mass_media_broadcast → radio_broadcasting.
  - mesoamerican_script joins the six script entity OR groups.
  - The unlock-string edits on radio and satellite.

## 12. Open questions

1. **OPEN DECISION — the V17 exception.** Should printing_press be accepted as a trunk node at A7? If D07's
   type_founding edge is adopted, type_founding becomes a second one. Or should both stay in Engineering, accepting a
   non-causal stage gate (and civic exposure) on the A7 CORE?
2. **A7 entry stays stage-coupled** through technological and systemic supporting facts (D04, D08). Is that intended,
   or should ages.json or those lanes provide a trunk-reachable option? Corned powder and the caravel were craft
   innovations.
3. **Alias/remap seam** (the package raises it once): old saves holding radio or satellite do not receive the split-off
   nodes.
4. **AI (T6.16):** should `trade` become an AI goal? Today the AI reaches it only through the cheapest fallback, at
   1871–1994 CE in the four AI seeds (INFERENCE), although every trade_volume Age fact depends on it.
5. **mesoamerican_script's reach:** should the civic, medical and entity writing OR groups accept it? taxation's
   prerequisite set is test-pinned.
6. **printing_press Eureka 0 text:** should "A screw press is in use…" be corrected to "Wine or oil presses are in
   use", keeping the same index and condition?

## 13. Compatibility summary (for the integrator)

- **Moving pins:**
  - branch counts
  - Engineering `finite_nodes_to_exhaust` (125 → 124 from D05)
  - corpus-sync name pin (radio renamed: a 7th exception)
  - generation test (television 3)
  - Eureka pins (81 → 83 Eurekas; 17 → 19 evaluable; 15 → 17 knowledge conditions)
  - AI-empire goldens
  - the three CI `--check` docs
- **Unaffected:** the chronology test (all new texts are dated, and radio keeps 1895), TradeResearchUnlockTests (trade
  is untouched), and the allocation-scenario and fixture keys.
- **IMPLEMENTATION DEFERRED:**
  - entity-requirement and unlock-list edits (§11)
  - a signal-line entity for optical_telegraph
  - a ground-station entity for communications satellites
  - the alias/remap seam
