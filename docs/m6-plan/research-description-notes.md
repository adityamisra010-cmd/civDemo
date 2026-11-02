# Research description notes — developer notes moved out of player-facing text (TC-4)

**Director, 2026-11-02:** the research panel and the info card show every node's `desc` to the player. Descriptions that carried developer language (decision and ADR ids, ruling numbers, registry and reclassification notes, entity and code ids, config keys, milestone ids, "owning system" and "-class unlock" phrasing, the old civic template, ALL-CAPS emphasis) were rewritten in plain, historically accurate language that names only effects the game implements. This page keeps what was removed, keyed by node id, so nothing of value is lost. It is a TEXT-only change under the tree closure: [tree-closure.md](knowledge-redesign/tree-closure.md) §5, TC-4; attribution `TreeClosureBaselineTests.ThePreTc4Descriptions_ReproduceTheTc3Text_Tc4IsTextOnly`; guard `Sim.Tests/Conventions/DeveloperLanguageGuardTests.cs`.

The same text change is mirrored in `Sim.Data/content/research-pre-tranche2.json` (shipped; the CLI's `--without-tranche2`) and `docs/m6-plan/knowledge-redesign/research-cost-pass/candidate/research.candidate.json` (pinned node-for-node to the shipped content by `ResearchCandidateTests`). No id, key, name, Age, prerequisite, unlock, effect, cost or Idea changed.

## 1. Descriptions rewritten whole (27)

Each entry: the developer note it carried (now here), the old text and the new text.

### `fire_making` — Controlled-fire pyrotechnology (technology, key 1)

- **Note:** D-047 ruling 2: fire itself (making, keeping, cooking) is baseline (`baseline.fire`) and is gated by no node; this node is advanced controlled-fire use. Realized effect: `foodState.adaptationAbsorbableShortfall`.
- **Old:** Advanced, deliberate use of controlled fire — sustained managed hearths and earth ovens, heat treatment of stone, planned landscape burning. Fire itself (making, keeping, cooking) is baseline and needs no research (D-047 ruling 2).
- **New:** Skilled, deliberate use of fire: managed hearths and earth ovens kept going for long periods, stone hardened by heating, and planned burning of the landscape. Making, keeping and cooking with fire need no research; this is the advanced craft. Earth-oven cooking at scale helps households ride out food shortfalls.

### `adhesive_natural` — Compound adhesives (technology, key 9)

- **Note:** D-047 ruling 3: simple gum, resin and sinew binding is baseline (`baseline.primitive_hafted_tools`). Realized effects: `production.toolWearPerEquippedFarmerPerYear`, `production.toolYieldBonusMax`.
- **Old:** Engineered binders — resin loaded with ochre or beeswax, heat-processed bitumen — for load-bearing composite hafting. Simple gum, resin and sinew binding is baseline (D-047 ruling 3).
- **New:** Engineered glues — resin mixed with ochre or beeswax, and heat-processed bitumen — strong enough to hold a tool head under load. Simple gum, resin and sinew binding needs no research. Well-bound tools last longer, and farmers gain more from them.

### `hafting` — Composite hafting (technology, key 11)

- **Note:** D-047 ruling 3: simple hafted tools are baseline (`baseline.primitive_hafted_tools`). Opens `unit.spearmen` (listed by the panel with its realization status; the description does not promise recruitment). Realized effect: `production.toolYieldBonusMax`.
- **Old:** Precision-fitted composite tools — shaped tang, socket or split haft bound with sinew and compound adhesive — that survive repeated impact. Simple hafted tools are baseline (D-047 ruling 3).
- **New:** Precision-fitted composite tools — a shaped tang, socket or split haft bound with sinew and glue — that survive repeated blows. Simple hafted tools need no research. Farmers equipped with tools gain more from them.

### `raft` — Sea-crossing raft (technology, key 20)

- **Note:** D-047 ruling 7: simple riverine and shore-hugging craft are baseline (`baseline.primitive_water_transport`); research unlocks the transport class and builds no vessel. Realized effect: `fog.exploreMaxRadiusNodes`.
- **Old:** Large lashed log or reed rafts with steering oars, built for open-water crossings beyond sight of land. Simple riverine and shore-hugging craft are baseline (D-047 ruling 7); research unlocks the class and builds no vessel.
- **New:** Large lashed log or reed rafts with steering oars, built to cross open water beyond sight of land. Simple river and shore-hugging craft need no research. Your explorers can range further.

### `dugout` — Load-carrying dugout (technology, key 21)

- **Note:** D-047 ruling 7: simple log floats and small river canoes are baseline; research unlocks the transport class and builds no vessel. Realized effect: `transport.riverCostFactor`.
- **Old:** Large log hollowed by fire and adze into a stable load-carrying hull. Simple log floats and small canoes for river crossing are baseline (D-047 ruling 7); research unlocks the class and builds no vessel.
- **New:** A large log hollowed out by fire and adze into a stable hull that can carry loads. Simple log floats and small canoes for crossing rivers need no research. Hauling goods by river becomes cheaper.

### `fishing_hook` — Line and pelagic fishing (technology, key 22)

- **Note:** D-047 rulings 4 and 6: basic fishing is baseline (`baseline.basic_fishing`) and needs no hook or node (`activity.fishing` is knowledge-eligible with zero completed nodes). Realized effect: `production.outputPerExtractorPerYear` (all extraction, not fishing alone).
- **Old:** Specialised hook-and-line gear — shell or bone hooks, weighted lines, lures — for deep-water and pelagic species. Basic fishing is baseline and needs no hook or node (D-047 rulings 4 and 6).
- **New:** Specialised hook-and-line gear — shell or bone hooks, weighted lines and lures — for deep-water and open-sea fish. Basic fishing needs no hooks and no research. Fishing, and the other work of gathering raw materials, yields more.

### `digging_stick_hoe` — Hoe (technology, key 39)

- **Note:** D-047: digging-stick cultivation is baseline. Realized effect: `production.toolYieldBonusMax`.
- **Old:** Hafted blade for tillage — the cultivation tool before the plough. Digging-stick cultivation is baseline (D-047).
- **New:** A hafted blade for working the soil — the cultivation tool before the plough. Cultivation with a digging stick needs no research. Farmers equipped with tools gain more from them.

### `track_road` — Trackway (technology, key 74)

- **Note:** D-047 ruling 8: basic dirt paths are baseline (`baseline.basic_paths`); ADR-032: an infrastructure-class unlock (path tier) that road development (the DevelopRoads order) builds. Opens `infra.road_track`; `infra.road_built` requires `track_road AND stone_dry`; `infra.courier_relay` requires `track_road AND horse_domestication`. RoadDevelopmentTests pins the phrase "not an engineered road".
- **Old:** Trackway — an IMPROVED PATH, not an engineered road: timber causeway and corduroy across marsh, cleared and stoned track (Sweet Track). An infrastructure-class unlock (path tier): road development builds the trackways, and basic dirt paths are baseline (D-047 ruling 8; ADR-032). The engineered built road additionally needs dry-stone construction (infra.road_built requires track_road AND stone_dry).
- **New:** An improved path, not an engineered road: timber causeways and corduroy tracks across marsh, and cleared, stone-laid tracks such as England's Sweet Track. Basic dirt paths need no research. Your society can build trackways, and with dry-stone construction also built stone roads.

### `arsenical_bronze` — Arsenical bronze (technology, key 80)

- **Note:** ALL-CAPS emphasis ("NEEDS NO TIN") re-cased; no other change.
- **Old:** Copper smelted from arsenic-bearing ores — harder than copper, castable, and NEEDS NO TIN. Most early "bronze" was this.
- **New:** Copper smelted from arsenic-bearing ores — harder than copper, castable, and needs no tin. Most early "bronze" was this.

### `sail_square` — Square sail (technology, key 94)

- **Note:** D-047 ruling 7: unlocks the improved coastal and seagoing transport class; primitive river and shore-hugging craft are baseline, and the realizing systems build the vessels. Opens `activity.coastal_shipping` (no consumer in this build). Realized effect: `transport.riverCostFactor`.
- **Old:** Wind propulsion — first on the Nile. Unlocks the improved coastal and seagoing transport class (D-047 ruling 7); primitive river and shore-hugging craft are baseline, and the realizing systems build the vessels.
- **New:** Wind propulsion with a square sail, first used on the Nile. Simple river and shore-hugging craft need no research. Sailing makes hauling goods by river cheaper.

### `geometry_axiomatic` — Axiomatic geometry (technology, key 140)

- **Note:** ALL-CAPS emphasis ("VERIFIED") re-cased; no other change.
- **Old:** Proof from stated axioms — Euclid. Knowledge that can be VERIFIED from the text alone.
- **New:** Proof from stated axioms — Euclid. Knowledge that can be verified from the text alone.

### `road_paved` — Engineered paved road (technology, key 150)

- **Note:** D-047 ruling 8: an infrastructure-class unlock; construction systems build the roads. Opens `infra.road_paved` (road class edgeType 4). Realized effect: `governance.authorityDecayCostUnits`.
- **Old:** Surveyed, layered, cambered, drained, paved — Via Appia. Inca qhapaq ñan independently (no wheels). An infrastructure-class unlock: construction systems build the roads (D-047 ruling 8).
- **New:** Surveyed, layered, cambered, drained and paved roads such as Rome's Via Appia; the Inca built their road network, the Qhapaq Ñan, independently and without wheels. Your society can build paved roads, and its administration reaches further.

### `caravel` — Caravel (technology, key 217)

- **Note:** D-047 ruling 7: unlocks the oceanic transport class (`activity.ocean_shipping`, no consumer in this build); research builds no vessel. Realized effect: `fog.exploreStepNodes`.
- **Old:** Lateen-rigged, shallow-draft, fast — the exploration ship of the Portuguese. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel.
- **New:** Lateen-rigged, shallow-draft and fast — the ship in which the Portuguese explored the Atlantic islands and the coast of West Africa. Each Explore order reaches further.

### `carrack` — Carrack and full-rigged ship (technology, key 218)

- **Note:** D-047 ruling 7: unlocks the oceanic transport class (`activity.ocean_shipping`); research builds no vessel. Also opens `building.dry_dock`. Realized effect: `trade.costPerBulkCostUnit`.
- **Old:** Three-masted, square and lateen, high-castled — the ocean cargo and warship. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel.
- **New:** Three-masted, square- and lateen-rigged and high-castled — the ocean cargo ship and warship of Columbus and da Gama. Hauling goods for trade becomes cheaper.

### `polynesian_canoe` — Polynesian voyaging canoe (technology, key 219)

- **Note:** D-047 ruling 7: unlocks the oceanic transport class (`activity.ocean_shipping`); research builds no vessel. Realized effect: `colonization.orderPartyFraction`.
- **Old:** Double-hulled, crab-claw sail, stellar navigation — Pacific colonisation without compass or writing. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel.
- **New:** Double-hulled canoes with crab-claw sails, steered by the stars: the Pacific islands were settled with them, without compass or writing. Your settlers can set out in larger founding parties.

### `macadam` — Macadam road (technology, key 272)

- **Note:** D-047 ruling 8: an infrastructure-class unlock; construction systems build the roads. Opens `infra.road_macadam` (edgeType 5). Realized effect: `pathBuild.buildCostMultiplier`.
- **Old:** McAdam 1820 — compacted graded stone; cheap all-weather roads. An infrastructure-class unlock: construction systems build the roads (D-047 ruling 8).
- **New:** Roads of compacted, graded broken stone, introduced by John McAdam around 1820: cheap and usable in all weather. Your society can build macadam roads, and building roads becomes cheaper.

### `frontier_military` — Military research (technology, key 422)

- **Note:** The old text promised "weapon-system effectiveness in combat resolution" and a RELATIVE level ("matters only against rivals' levels"). Neither exists in this build: the repeatable descriptor records `status: DOCUMENTED — no consuming mechanism exists yet` and `relative: true` as design intent, and its level operand is the ADR-049 PROXY `fog.formationSightNodes` (no combat is simulated before M7). The new text names only the level effect that is live.
- **Old:** Each level raises weapon-system effectiveness in combat resolution. RELATIVE: a level matters only against rivals' levels, so the arms race never ends.
- **New:** Military research without end: armies keep refining their weapons and sensors to stay ahead of their rivals. Each level lets your formations see further.

### `frontier_cyber` — Cyber research (technology, key 423)

- **Note:** The old text said "RELATIVE, like military research"; relative levels are design intent only (`relative: true`, `status: DOCUMENTED — no consuming mechanism exists yet`). Its level operand is `society.unrestOutputDrag` (ADR-049: information defence keeps unrest from costing output).
- **Old:** Each level raises network offence and defence. RELATIVE, like military research.
- **New:** Ongoing research into attacking and defending computer networks. Each level reduces the output that unrest costs your society.

### `motor_road` — Motor road (highway) (technology, key 425)

- **Note:** ADR-032: an infrastructure-class unlock (highway tier); road development builds the highways as new alignments beside the older roads. Opens `infra.road_highway` (edgeType 6: speedFactor 0.15, capacity 20,000,000 t/yr, the fastest and largest class in sim.json `roads.classes`).
- **Old:** Graded, surfaced, grade-separated road for motor traffic — Milano–Laghi autostrada 1924, Cologne–Bonn 1932, Reichsautobahn and US Interstate. An infrastructure-class unlock (highway tier): road development builds the highways as new alignments beside the older roads (ADR-032).
- **New:** Graded, surfaced, grade-separated roads for motor traffic: the Milano–Laghi autostrada of 1924, the Cologne–Bonn road of 1932, the Reichsautobahn and the US Interstate. Your society can build highways, the fastest roads, which carry far more freight than any other.

### `trade` — Trade (technology, key 426)

- **Note:** ALL-CAPS emphasis ("BETWEEN") re-cased; no other change. The D-069 sentence ("Its causal prerequisites are ...") is kept verbatim. Design basis: R2a, Director decision 1 (sim.json `trade._docEntity`): trade between settlements is a research unlock; within-settlement exchange is never gated.
- **Old:** Formal exchange between communities: goods valued against a counted standard and carried by pack animal or boat to partners beyond the settlement — the caravan and boat trade of the Uruk expansion and the Old Assyrian merchants of Kanesh. Within-settlement sharing and exchange need no research; trade BETWEEN settlements (domestic or foreign) does. Its causal prerequisites are a record of quantity (clay tokens) and a carrier for goods over distance (donkey, camel or the square sail).
- **New:** Formal exchange between communities: goods valued against a counted standard and carried by pack animal or boat to partners beyond the settlement — the caravan and boat trade of the Uruk expansion and the Old Assyrian merchants of Kanesh. Within-settlement sharing and exchange need no research; trade between settlements (domestic or foreign) does. Its causal prerequisites are a record of quantity (clay tokens) and a carrier for goods over distance (donkey, camel or the square sail).

### `law_code` — Written law (civic, key 1001)

- **Note:** Reclassified from registry institution `inst.law_code` (architecture §5.7); the institution is established by its owning system once the civic is complete. In this build that system is real: the law-court construction project (goods.json project 16) founds `inst.law_code` as institution type `law_court` (ADR-057). Realized effect: `society.grievanceRelief`.
- **Old:** Written law: the civic knowledge of this form of organization. Reclassified from registry institution inst.law_code (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** Laws set down in writing, so that judgement no longer rests on memory and custom alone, as in the law codes of Ur-Nammu and Hammurabi. Your society can found a law court, and grievance builds more slowly.

### `legal_code_roman` — Systematic law (civic, key 1002)

- **Note:** Reclassified from registry institution `inst.legal_code_roman` (architecture §5.7); the institution is established by its owning system once the civic is complete. In this build no system founds `inst.legal_code_roman`, `inst.university` or `inst.guild` (the panel lists them as not built), so the new text does not promise them. Realized effect: `trade.gapClosingFraction`.
- **Old:** Systematic law: the civic knowledge of this form of organization. Reclassified from registry institution inst.legal_code_roman (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** Law organised into one coherent system, with trained jurists, written procedure and enforceable contracts, as in Roman law and its later compilation under Justinian. Dependable contracts let trade even out prices between settlements faster.

### `census` — Census (civic, key 1003)

- **Note:** Reclassified from registry institution `inst.census` (architecture §5.7); the institution is established by its owning system once the civic is complete. No system founds `inst.census` in this build (nor the five other institutions it helps open). Realized effect: `governance.taxExtractionResponseMax`.
- **Old:** Census: the civic knowledge of this form of organization. Reclassified from registry institution inst.census (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** An official count of the people and their property, taken for taxation and service — known from ancient Egypt, Mesopotamia, China and Rome. Your taxes collect a larger share of what your society produces.

### `coined_wage` — Wage labour in coin (civic, key 1004)

- **Note:** Reclassified from registry institution `inst.coined_wage` (architecture §5.7); the institution is established by its owning system once the civic is complete. No system founds it in this build. Realized effect: `migration.baseRatePerYear`.
- **Old:** Wage labour in coin: the civic knowledge of this form of organization. Reclassified from registry institution inst.coined_wage (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** Work paid in coin rather than in rations or land, as with the paid oarsmen of Athens and the salaried soldiers of Rome. People paid in coin move more freely, so migration between settlements rises.

### `patent` — Patent system (civic, key 1005)

- **Note:** Reclassified from registry institution `inst.patent` (architecture §5.7); the institution is established by its owning system once the civic is complete. No system founds it in this build. Realized effect: `institutions.universities.maxResearchCostReduction`.
- **Old:** Patent system: the civic knowledge of this form of organization. Reclassified from registry institution inst.patent (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** A legal right that rewards an inventor with a monopoly for a limited time — Venice's patent law of 1474 and England's Statute of Monopolies of 1624. Your universities cut the cost of research further.

### `joint_stock` — Joint-stock company (civic, key 1006)

- **Note:** Reclassified from registry institution `inst.joint_stock` (architecture §5.7); the institution is established by its owning system once the civic is complete. No system founds `inst.joint_stock` or `inst.central_bank` in this build. Realized effects: `trade.costPerBulkCostUnit`, `price.stockReleaseRatePerYear`.
- **Old:** Joint-stock company: the civic knowledge of this form of organization. Reclassified from registry institution inst.joint_stock (architecture §5.7); the institution itself is established by its owning system once this civic is complete.
- **New:** A company owned in transferable shares by many investors, pooling capital for ventures too large or risky for one merchant, like the Dutch and English East India Companies. Hauling goods for trade becomes cheaper, and stored goods reach the market faster.

### `taxation` — Taxation (civic, key 1007)

- **Note:** Developer references removed: the tax edict's order (`SetTaxRate`), its age gate (sim.json `governance.taxationMinAge` = 3) and the refinement node ids (`arithmetic_babylonian`, `surveying`, `standard_weights`, `coinage_electrum`), which no longer gate the edict (sim.json `governance.taxationRequires` = `taxation`). Removed as redundant with the panel's REQUIRES list: "Its causal prerequisites are counted obligations (clay tokens), sealed stores under administrative control (stamp seal) and an administrative record beyond memory (proto-cuneiform, hieroglyphic or Chinese script)." Its evidence sentence is kept in shortened form.
- **Old:** Taxation: the levy of tribute in kind — a counted share of the harvest, herd or craft output owed to the ruler or temple, assessed against the harvest and delivered into sealed stores. Its evidence is the Uruk and Ur III grain-receipt tablets, the sealed storerooms of the Ubaid and Uruk temples, and the Egyptian harvest assessment recorded beside the Nile gauge. A community without a record of what is owed has gifts and feasting, not a levy, so no Stone Age society taxes. Researching it gives the polity the knowledge of the tax edict (SetTaxRate); the edict is operational only once the polity has entered Age III, the Bronze Age (sim.json governance.taxationMinAge). Related refinements, which no longer gate the edict: tax assessment by computation (arithmetic_babylonian), taxation by area (surveying), by weight (standard_weights) and in coin (coinage_electrum). Its causal prerequisites are counted obligations (clay tokens), sealed stores under administrative control (stamp seal) and an administrative record beyond memory (proto-cuneiform, hieroglyphic or Chinese script).
- **New:** The levy of tribute in kind — a counted share of the harvest, herd or craft output owed to the ruler or temple and delivered into sealed stores, as the grain-receipt tablets of Uruk and Ur III and Egypt's harvest assessment by the Nile gauge record. Researching it lets your society levy taxes by edict once it has entered Age III, the Bronze Age; a Stone Age community, with no record of what is owed, knows gifts and feasting but no levy. Assessment by computation, by surveyed area, by standard weights and in coin came later, and none of them is needed to levy a tax.

## 2. The government-form tail (60 civics)

Each of these civics ended with the generated tail (docs/m6-plan/knowledge-redesign/tools/implement_tranche.py `CIVIC_DESC_TAIL`):

> Knowledge only for now: adopting a form of government arrives with the government layer (M8).

It is replaced by:

> Your society cannot adopt it yet.

**Note:** adoption of a government form, and active government effects, are scheduled for M8 (D-D / D-062; sim.json `regime._doc`: "No adoption order, no stored regime state, no political effect"). The old tail's "Knowledge only" was not true: every one of the 60 has at least one realized effect that a live system reads (the info card says "Read by the simulation"), so the new tail drops it. The first sentence of each description is unchanged. The 60:

`kin_council`, `hereditary_rank`, `oath_alliance`, `organized_priesthood`, `chiefdom`, `tribal_confederacy`, `appointed_officials`, `corvee_labour`, `elite_council`, `royal_envoys`, `sacral_kingship`, `service_tenure`, `succession_law`, `sworn_treaties`, `city_state_oligarchy`, `territorial_kingship`, `citizen_assembly`, `elected_magistracies`, `land_registry`, `provincial_administration`, `standing_army`, `tax_farming`, `theocracy`, `craft_guilds`, `municipal_charter`, `religious_toleration`, `assembly_democracy`, `bureaucratic_empire`, `classical_republic`, `civil_service_examinations`, `commercial_law`, `representative_assembly`, `feudal_monarchy`, `merchant_republic`, `military_rule`, `bill_of_rights`, `central_banking`, `civil_police`, `compulsory_education`, `law_of_nations`, `official_statistics`, `public_health_board`, `resident_embassies`, `separation_of_powers`, `written_constitution`, `absolute_monarchy`, `constitutional_monarchy`, `federal_republic`, `presidential_republic`, `abolition`, `general_staff`, `laws_of_war`, `mass_political_party`, `mass_suffrage`, `ministerial_responsibility`, `universal_conscription`, `parliamentary_government`, `central_planning`, `marxist_leninist_state`, `one_party_state`.

## 3. Developer language left in place, and why

- **STRUCTURE-pinned fields.** `unlocks.capabilities` and `effects.future` are inside the tree-closure STRUCTURE hash, which this TEXT-only change does not open. The guard lists what sits there exactly (`StructurePinnedExceptions`, 21 strings: D-039/D-040/D-041/D-011/ADR-019 references in 16 capability strings of 14 technologies, `CAPACITY` in dynamo and electricity_generation, `AND` in tempering, `spinning_spindle` in cotton_cultivation, "(separate node)" in maize, and "before M7" in saddle's future-effect description). Clearing them needs an approved STRUCTURE change.
- **Future-effect `system` and `milestone`.** The info card prints them as a label by design ("Future (M7 Battle Layer - chariots): …", CR-025 ruling, pinned by InfoQueryTests); two `system` strings also cite "ADR-032 section 7" (wheel_spoked, road_traffic_regulation). Not scanned by the guard; STRUCTURE-pinned in any case.
- **"Knowledge only." in eight technology descriptions** (fission_weapon, fast_breeder_reactor, geological_disposal, graphite_reactor, heavy_water_reactor, light_water_reactor, reprocessing, uranium_enrichment). Not developer language, so not in this change, but not accurate: each has a realized effect. Left for a Director decision.
- **Outside research.json node text:** the research screen's own intro line (`Sim.Ui/Progression/ProgressionScreen.cs`, "adoption arrives in M8") and the `baseline` section's `providedBy` notes (D-ids, OrderKind numbers), which the info card shows for baseline capabilities. Not node text; not in this change.
