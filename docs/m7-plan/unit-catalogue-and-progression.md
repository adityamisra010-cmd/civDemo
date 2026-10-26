# M7 Unit Catalogue and Progression Design

**Status:** Design proposal for review. Documentation only; no runtime behavior, content data, unit IDs, costs, or defaults are changed by this document.

**Design decision requested:** use one shared, culture-neutral unit roster for the initial military implementation. Add civilization-specific unit and building variants only after the culture/civilization identity system exists.

## 1. Executive decisions

1. **Common roster first.** Every polity can access the same core unit families when it satisfies the relevant knowledge, production, geography, material and institutional requirements.
2. **Culture-specific variants later.** Do not create parallel full rosters or culture-specific building trees now. Reserve a data-driven extension point for later variants that replace a standard unit/building or add a small number of distinctive options.
3. **Use the existing 12 family definitions as the starting inventory, not as an unquestionable final design.** The current file is marked PROVISIONAL and has no combat statistics. Several entries need family/role cleanup before M7 makes them recruitable.
4. **Respect the existing modernization ruling.** ADR-031 specifies automatic, free modernization of mainline formations when their polity enters a new Age; preserve owner, location, experience and army membership. Do not silently replace this with paid upgrades.
5. **Distinguish modernization from a player-selected upgrade or transformation.** Same-Age technology upgrades and branch choices need explicit M7 order semantics. They are not expressible by Age-transition modernization alone.
6. **Do not invent economic goods.** The current economy has grain, livestock, fish, timber, stone, clay, copper ore, tin ore, fiber, hides, bronze, tools, pottery and cloth. It has no distinct horse, iron, steel, coal, oil, fuel, aluminium or uranium goods. Equipment labels in unit-families.json are declarative, not stock consumed.
7. **Implement shared systems across A1–A9.** Age is a simulation dimension, not a development sequence. Work should be organized by system and unit role, not by implementing one Age at a time.

## 2. What exists today

The repository already contains a substantial proposed military roster in Sim.Data/content/unit-families.json:

- 12 role families and their per-Age identities.
- A founding warband, one per founded polity with a capital.
- Family and identity keys, role descriptions, historical emergence notes, declarative equipment, optional research entity links, and branch/line-end behavior.
- A pure modernization planner and previews, used by Age transitions.
- A MilitaryUnits table that can represent an army formation or a separate mobile agent.

However, this is not a functioning army or combat system. The current content explicitly says attack, defense, range, movement, recruitment, supply and Action Capacity are not implemented by this roster. The unit entities in research.json are capability/realization declarations; research completion does not itself recruit a unit. Current goods and project definitions do not provide a full military production economy.

Relevant current sources:

- Sim.Data/content/unit-families.json
- Sim.Core/Systems/Ages/UnitFamilyContent.cs
- Sim.Core/State/MilitaryQuery.cs
- Sim.Tests/Systems/UnitModernizationTests.cs
- docs/adr/adr-031-ages-advancement-and-unit-modernization.md
- docs/research-gameplay-unlock-audit.md
- docs/design/research-capability-ownership.md
- docs/m6-plan/knowledge-redesign/audit/current-tree-inventory-military.md
- Sim.Data/content/goods.json
- docs/milestones.md

The catalogue below is a recommended target design. It retains the useful role/identity model but flags required cleanup where a family currently groups fundamentally different battlefield roles.

## 3. Culture policy: common baseline, distinct cultures later

### 3.1 Initial rule

For the first functional roster, all civilizations share:

- the same unit families and baseline identities;
- the same general recruitment and modernization rules;
- the same common military building families;
- the same technology/capability gates;
- the same combat rules and cost framework.

Geography, resource availability, transport access, population and infrastructure should create strategic differences before culture-specific bonuses exist. An empire without access to a required material or coastline should not bypass that constraint because of a cultural flavor label.

### 3.2 Later culture layer

When culture and civilization identity are modeled, use a small data-driven overlay rather than maintaining separate catalogues:

- **Unique replacement:** one civilization's unit replaces a standard identity in the same role and comparable unlock window, with a clear benefit and trade-off.
- **Unique alternative:** a special unit adds a narrow role not present in the common roster. It must not make the generic counter or core role obsolete.
- **Building replacement:** a civilization-specific building replaces a standard building family or tier, retaining its general function while changing a small set of measurable effects.
- **Unique infrastructure:** a limited special building/improvement with an explicit purpose and an owning system that actually consumes its effect.

Each variant must declare the standard unit/building it replaces or complements, the required culture/civilization identity, its technology and material prerequisites, and the effect it changes. It must pass the same legality and deterministic tests as generic content.

Do not implement cultural variants in the first M7 slice. The milestone roadmap assigns culture/social systems to M9 and deep content expansion to M11+; the actual destination for unique unit/building content should be decided when that layer is specified. The schema should not gain an unused culture modifier system early merely to anticipate it.

### 3.3 What the Civilization VI comparison means

The useful pattern to borrow from Civilization VI is not “every civilization gets an entirely different army.” It is a common baseline plus selective identity: unique units, unique buildings/districts and civilization/leader abilities. For example, Firaxis described the Māori Toa as a unique unit and the Marae as a building replacing the Amphitheater. See the official [Māori first-look announcement](https://steamcommunity.com/games/289070/announcements/detail/1703946667370548567). Civ VI also uses explicit upgrade actions and preserves unit experience/promotions when units are upgraded; its official manual describes the Slinger-to-Archer example and an upgrade cost. See the [Civ VI 25th Anniversary manual, “Upgrading Obsolete Units”](https://cdn.cloudflare.steamstatic.com/steam/apps/289070/manuals/CIV_VI_25TH_ONLINE_MANUAL_ENG.pdf?t=1663263035).

Our current ADR-031 differs from Civ VI's paid-upgrade approach: age-transition modernization is already ruled automatic and free. The design must preserve that unless a separate decision changes it.

## 4. Target common military catalogue

A family represents a persistent battlefield role; an identity is the historical realization of that role. The names below are the current proposed identities where one already exists. A role's list is not a claim that each identity is currently recruitable.

### 4.1 Army formations

| Family | Proposed identity progression | Role and design intent |
|---|---|---|
| Heavy / line infantry | Warband (A1) → Axe warriors (A2) → Bronze-armed infantry (A3) → Iron-armed infantry (A4) → Legionary infantry (A5) → Men-at-arms (A6) → Musket infantry (A7) → Rifle line infantry (A8) → Mechanized infantry (A9) | Main close-order/line-holding formation. Preserve as the core army family. Modern mechanized infantry must not be confused with tanks. |
| Light infantry / skirmishers | Skirmishers (A3) → Light auxiliaries (A5) → Rangers (A7) → Special forces (A9) | Screening, raids, rough-terrain operations and later specialized infantry. Move the world-mobile scouting job out of this family; scouts that reveal fog need to be separate mobile agents. |
| Ranged infantry | Slingers (A1) → Archers (A2) → Composite-bow archers (A4) → Crossbowmen (A6) → Arquebusiers (A7) → Sharpshooters (A8) → Machine-gun teams (A9) | Ranged fire support. Each identity requires its own tested capability, range and counter rules when combat exists. |
| Ranged infantry branch | Longbowmen (A6 alternative) | A recruitable alternative where the longbow capability is known. It is not an automatic modernization result. The current mainline modernization rule follows crossbowmen deterministically. |
| Spear / anti-cavalry infantry | Spearmen (A2) → Phalanx (A4) → Pikemen (A6) → Pike-and-shot (A7) → role absorbed by line infantry after A7 | Defensive formation and anti-mounted counter. The line end should be explicit and explained by the replacement of pike formations by bayonet-equipped line infantry; old units follow the already-declared generic-successor rule. |
| Light cavalry | Mounted scouts (A3) → Light horse (A4) → Light lancers (A6) → Hussars (A7) → Carbine cavalry (A8) | Fast mounted raiding, pursuit and screening. Do not claim horses are a stock cost until the economy models mounts distinctly. |
| Heavy cavalry / breakthrough | Chariots (A3) → Cataphracts (A5) → Knights (A6) → Cuirassiers (A7) → Tanks (A9) | Shock and breakthrough role. Tanks are a mechanized realization of the broad breakthrough role, not a literal cavalry unit. This jump needs M7 balance review and real industrial/fuel realization. |
| Ranged cavalry | Horse archers (A4) → Mounted crossbowmen (A6) → Dragoons (A7) → role absorbed by light cavalry after A7 | Mobile ranged harassment. The generic successor must be explained in the UI; it must not silently erase a distinct unit or its experience. |
| Siege / artillery | Rams and siege towers (currently A4; date/age should be reviewed) → Torsion engines (A5) → Trebuchets (A6) → Bombards (A7) → Field artillery (A8) → Rocket artillery (A9) | Fortification breach and later indirect fire. This family needs siege targets/fortifications and artillery attack resolution; without those consumers, the equipment labels have no live effect. |

### 4.2 Mobile agents and platforms

These are separate mobile agents, not formations inside an army. They need movement and appropriate action rules rather than simply being assigned an army formation identity.

| Target family | Proposed identities/path | Design decision |
|---|---|---|
| Land reconnaissance | Scout party (A1) → mounted reconnaissance (A3) → organized reconnaissance patrol (A5/A6) → modern reconnaissance vehicle (A9) | Create a dedicated mobile-agent family for the M7 scout that replaces M6's temporary Explore action. Do not treat a scout as an infantry formation only. The intermediate labels are design placeholders until historically and technologically audited. |
| Naval surface combat | War galleys (A4) → heavy galleys (A5) → war cogs (A6) → ships of the line (A7) → ironclads/steam warships (A8) → destroyers (A9) | Surface combat and sea control. Requires water traversal, coast/port realization, crew/supply and naval battle rules. |
| Naval transport | Shore craft (A1) → plank-built boats (A3) → seagoing transports (A6) → oceanic transports (A7) → steam transports (A8) → amphibious transports (A9) | Transport is a logistics role, not a combat upgrade. Requires traversable water, embark/disembark rules, carrying capacity and transport-loss handling. |
| Submarines | Conventional/submersible path when supported → nuclear submarines (A9) | Split from the surface combat family. A submarine is not an upgrade or same-role branch of a destroyer. Nuclear propulsion waits on actual nuclear capability and material/energy rules. |
| Aircraft carriers | Carrier vessels (A9) | Separate naval platform and force-projection role. It should carry/support aircraft, not auto-modernize from a destroyer. |
| Fixed-wing aviation | Early military aircraft → fighters/bombers as separately realized roles; stealth capability later changes a supported aircraft identity | Split fighter/air-superiority, strike/bomber and transport/recon roles if the combat design needs distinct behavior. A single generic “military aircraft” is adequate as a placeholder but not as a full operational catalogue. |
| Helicopters | Rotary-wing aircraft (A9) | Separate mobility and combat role; do not model as a same-role branch of fixed-wing aircraft. |
| Unmanned aircraft | Reconnaissance drones first; combat drones only when the relevant control/strike mechanics exist | Separate role if range, autonomy, cost or risk materially differs from piloted aviation. Avoid adding a drone type just for visual variety. |
| Strategic missiles | Ballistic missile → intercontinental missile (A9 same-Age technology upgrade) | A strategic delivery system, not conventional army combat. It needs a separate launch, target, stockpile and strategic-governance model. Defer realization until M7/M8/M10 rules support it. |

### 4.3 Required family refactor before M7 makes the roster recruitable

The current 12-family file is a good seed, but its grouping should not be carried forward blindly:

1. **Split scouts from light infantry.** A unit that independently moves and reveals fog is a mobile agent; a skirmisher formation within an army is not the same operational object.
2. **Split surface combatants, submarines and carriers.** These perform different roles. A same-Age “branch” list does not create a meaningful upgrade system.
3. **Split fixed-wing aviation, helicopters and drones by operational role.** These should not be represented as if they are mutually exclusive age-specific names for one unit.
4. **Preserve branch identities as recruitable alternatives, not automatic upgrades.** Longbowmen are the immediate example. A branch should be unlocked through its own capability and be separately selectable when the recruitment system exists.
5. **Add an explicit same-Age upgrade/transform model only where needed.** Strategic missile improvement and advanced aircraft illustrate why age transition alone cannot express all progression. Define an order, prerequisites, costs, state preservation and replay semantics under a reviewed ADR.
6. **Keep family and identity IDs stable wherever possible.** If splitting a family requires changing keys, provide a deterministic migration mapping and tests; never silently reassign identities in saved games.

## 5. Progression rules

### 5.1 Eligibility is more than research

For every recruitable identity, eligibility should be the conjunction of the applicable conditions, not just “the research node was completed”:

1. The required knowledge/capability is available to that polity.
2. A recruitment or production location is legally available.
3. Required materials and adult labor can be committed without violating conservation.
4. Any necessary building, institution, geography or transport condition is satisfied.
5. The owner can support the unit under the adopted upkeep/supply policy.
6. The unit's role is supported by the live M7 rules.

Research makes an option eligible; it does not create a formation, consume goods, supply a unit, or satisfy an Age's Military Realization requirement on its own. Age tags in the family data are historical/modernization metadata, not independent calendar gates.

### 5.2 Modernization

Retain ADR-031's current rule for the first implementation:

- On an explicit Age transition, a family with a later mainline realization modernizes to that identity automatically and for free.
- Preserve unit identity/state fields required by the existing contract: owner, location, coordinates, experience and army membership.
- A family not yet present at the new Age remains unchanged until its first identity exists.
- A family whose line ends uses its declared no-successor rule; generic conversion is allowed only when explicitly configured.
- Unknown content identities loaded from a save are preserved, not destroyed.
- A modernization plan shown in the UI must be identical to the plan applied by the transition.

This rule keeps the existing deterministic contract but is intentionally coarse. Same-Age unlocks and selectable branches must use a different, explicit action; do not hide them in the Age transition.

### 5.3 Explicit upgrades and transformations (future M7 decision)

For an optional upgrade/transform order, specify and test:

- valid source identity and target identity;
- target capability and prerequisites;
- whether it is a same-role upgrade, role conversion, or new recruitment;
- resources and labor consumed, if any;
- legal location and state restrictions;
- whether partially accumulated production carries over;
- state preservation (owner, location, health, experience, promotions, orders, army assignment, supply);
- event/log entry and deterministic replay;
- cancellation/failure atomicity;
- whether the source unit remains if target realization fails.

No upgrade costs should be invented in this document. Combat balance, resource availability and production throughput must be measured first. For the first release, automatic Age modernization and ordinary recruitment are enough; same-Age upgrades should be added only where the gameplay needs them.

## 6. Shared military building catalogue

These are proposed generic building families, not claims that all currently exist in the construction simulator. Culture-specific replacements are deferred.

| Building family | Main purpose | Candidate access | Dependencies and constraints |
|---|---|---|---|
| Barracks / training ground | Recruit and train ordinary land formations; centralize basic land recruitment and experience effects | Early, with the first recruitable foot units | Needs an actual construction project, recruitment gate and system-consumed training effect. Do not make a building prerequisite for the founding warband. |
| Stable / remount yard | Enable and support mounted units | When mount technology and supply are realizable | Current livestock is generic food, not a horse stock. Do not consume livestock as though it were horse equipment. A mount availability model is required. |
| Siege workshop / arsenal | Enable siege engines and maintain artillery equipment | When the first siege capability becomes realizable | Requires a real production/stock/supply effect and appropriate target/fortification mechanics. Do not treat the existing general workshop as a military workshop without a reviewed definition. |
| Shipyard / naval yard | Construct, repair and organize naval combat/transport agents | When water traversal and port/coast rules exist | Water nodes are currently impassable and the current configuration excludes vessels/ports/naval movement. Building content alone cannot unlock ships. Existing dry-dock references require a live construction/effect consumer. |
| Foundry / ordnance works | Support metal weapon and artillery production | Bronze first; later metals only when represented in the economy | Bronze exists, but iron/steel and gunpowder are not goods in the current economy. The effect must use existing production rules or wait for an approved economy extension. |
| Motor works / vehicle depot | Produce and support tanks, mechanized infantry and armored reconnaissance | Industrial/modern capability | Requires real engines, vehicle production, upkeep and fuel/material representation. None is implemented merely by naming the building. |
| Airfield / air base | Base and operate aircraft | When aviation, movement, range, basing and air combat are live | building.airfield exists as a research registry concept, but not as a realized military system. Air units must have a basing and capacity rule. |
| Strategic launch facility | Maintain and launch strategic missiles | Modern capability, after separate strategic-weapons design | Requires stockpile, launch authority, target validation, interception/defense and M8/M10 governance integration. Do not implement as another conventional combat unit. |
| Logistics depot / supply hub | Store and distribute military supply and support campaign reach | When M7 defines supply and roads/transport integration | Avoid a cosmetic building. Its real effect must change legal reach, supply throughput, recovery or another modeled quantity. |

### 6.1 Minimum building implementation

Do not implement all nine building families in one batch. The first playable land-combat slice should choose the smallest set that actually supports the recruited units. Prefer:
- no required building for the already-existing founding warband;
- one generic land training building if testing proves it is needed;
- the smallest additional gate that has a distinct, measurable effect.

Naval yards, airfields and strategic launch facilities must be implemented with their owning movement/combat systems, not as isolated placeholders. Do not add culture-specific versions yet.

## 7. Resource and economy guardrails

The current goods list contains 14 goods: grain, livestock, fish, timber, stone, clay, copper ore, tin ore, fiber, hides, bronze, tools, pottery and cloth. Do not add horse, iron, coal, oil, fuel, aluminium or uranium by implication.

For M7:

- Costs must be tied to the existing stock and construction/production interfaces.
- Equipment notes in unit-families.json are not consumed goods.
- Do not treat generic livestock as horses, copper/tin as iron, or tools as a complete weapons/vehicle supply model.
- If a target requires an unmodeled material, keep that target future-dependent or include a separately reviewed economy change with production, extraction, stockpiling and migration.
- Avoid making the first land battle require a full metal/fuel economy. Start with a transparent, conserved recruitment/material model and add advanced-material dependence when its source and consumers exist.
- Units should not be created for free except the founding warband and the already-ruled free Age modernization.

## 8. Non-military units: separate future catalogue

Do not force civilian and diplomatic agents into the MilitaryUnits family model merely to call the roster complete. These need their own owning systems and belong in later design slices:

| Category | Candidate roles | Likely owning system |
|---|---|---|
| Settlement expansion | Pioneer/settler, migration/colonization party | Expansion/settlement system |
| Construction | Engineer, builder, surveyor | Construction/infrastructure system |
| Trade/logistics | Caravan, merchant, freight convoy | Trade and transport system |
| Diplomacy | Envoy, diplomat, negotiator | M8 Politics/Diplomacy |
| Intelligence | Spy, covert operative | M8/M9, with explicit secrecy and counter-intelligence rules |
| Medical/support | Field medic, repair/support detachment | M7 supply/repair or later Society/health system |

These are placeholders for a future unit-design pass, not M7 recruitment requirements. Each needs its own movement, legality, action, persistence and owner rules. M6's current Explore action should be replaced by the dedicated mobile scout when that movement/visibility integration is ready.

## 9. Recommended implementation sequence

The sequence is by shared capability, not by Age.

### Slice 1 — Ratify the role catalogue and schema
- Resolve the family refactors in §4.3.
- Verify each recruitable identity maps to a research/capability entity or explicitly records why it does not yet.
- Define the distinction between army formation and MobileAgent.
- Add validation for orphan identities, duplicate roles, missing prerequisite mappings and unsupported effects.
- No runtime behavior change.

### Slice 2 — Land-unit realization
- Implement recruitment eligibility, resource/labor reservation, unit creation, cancellation and persistence for the selected land formation families.
- Add the first actual combat/action resolution only with M7's battle rules; do not fake attack values in data without a consumer.
- Add deterministic order types and a UI action path.
- Check ledgers and atomicity on partial resource/labor shortages.

### Slice 3 — Movement and reconnaissance
- Implement the independent scout MobileAgent and connect it to fog/visibility.
- Validate movement, pathing, wraparound, map boundaries, action capacity, exploration, persistence and deterministic replay.
- Preserve the current Explore order until the scout path is fully tested; remove it only when the replacement is live.

### Slice 4 — Army composition and support roles
- Add explicit formation membership changes, stack/army rules and the state-preservation contract.
- Add anti-cavalry, ranged, siege and cavalry interactions only when their combat consumers exist.
- Verify free Age modernization against recruited units and multi-Age transitions.

### Slice 5 — Naval and aviation systems
- Implement water traversability, ports/shipyards, naval movement, transport capacity and naval combat before making naval identities recruitable.
- Add aircraft basing/range/sortie mechanics before aviation is recruitable.
- Keep submarine, carrier, fixed-wing, helicopter, drone and strategic missile roles distinct.

### Slice 6 — Common military buildings and tuning
- Add only buildings whose effects have a real consumer.
- Calibrate production, recruitment throughput, upkeep/supply and combat attrition over representative seeds.
- Keep the roster culture-neutral.

### Slice 7 — Culture-specific content (later)
- Only after culture identity and its gameplay rules are defined.
- Add a small number of unique replacements/alternatives per culture, plus replacement buildings where their effects are measurable.
- Test the unique variant against the generic equivalent so it introduces a trade-off or new strategic option, not a strictly superior default.

## 10. Acceptance criteria

The catalogue is ready to drive M7 implementation when:

- Every family has one clear persistent role.
- Units with different operational roles are not represented as automatic upgrades of each other.
- Every recruitable identity has a valid knowledge/capability, material, location and building path, or a clearly documented future dependency.
- Every declared building effect has a live system consumer before it is labeled implemented.
- The founding warband and the ADR-031 modernization contract remain compatible.
- Branch variants are explicitly selectable/recruitable or clearly deferred; no player can accidentally lose a variant on age transition.
- Units preserve required state across modernization, movement, save/load and replay.
- No new goods, costs or combat statistics are introduced without their owning systems and calibration evidence.
- Tests cover legality, resource conservation, migration, action logging, deterministic replay, boundaries and unit-family transitions.
- The full M7 system is designed to work across A1–A9; there is no separate “ancient-only” simulation architecture.
- Culture-specific units/buildings remain deferred until an explicit culture layer is designed.

## 11. Decision summary

Approve the **shared culture-neutral roster** for the first M7 implementation. Use the existing 12 family definitions as the inventory baseline, but do not make them recruitable unchanged: split scouting from army light infantry, split naval surface combat from submarines/carriers, and split aviation roles before those systems become live. Keep automatic free age modernization as already ruled; add explicit same-Age upgrades only when the game needs them and a reviewed order contract exists.

Add culture-specific unique units and building replacements later as a thin data-driven overlay, after the culture system has an owner. This delivers Civilization VI-style differentiation without multiplying today's technology prerequisites, resource rules, buildings and balance work by the number of cultures.

## References

- Repository: Sim.Data/content/unit-families.json
- Repository: docs/adr/adr-031-ages-advancement-and-unit-modernization.md
- Repository: docs/d047-civ6-reference-and-progression-rulings.md
- Repository: docs/research-gameplay-unlock-audit.md
- Repository: docs/design/research-capability-ownership.md
- Repository: docs/m6-plan/knowledge-redesign/audit/current-tree-inventory-military.md
- Repository: Sim.Data/content/goods.json
- Repository: docs/milestones.md
- Firaxis/2K: [Civilization VI – Kupe Leads the Māori](https://steamcommunity.com/games/289070/announcements/detail/1703946667370548567)
- Civilization VI 25th Anniversary manual: [“Upgrading Obsolete Units”](https://cdn.cloudflare.steamstatic.com/steam/apps/289070/manuals/CIV_VI_25TH_ONLINE_MANUAL_ENG.pdf?t=1663263035)
