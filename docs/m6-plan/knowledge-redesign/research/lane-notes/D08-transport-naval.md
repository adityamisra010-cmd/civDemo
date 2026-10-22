# D08 — Transport, navigation and naval: lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified, and nothing describes existing behaviour
unless it is labelled MEASURED.** Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION ·
IMPLEMENTATION DEFERRED. Canonical data: `data/proposal-parts/D08-transport-naval.json` (55 existing records, 5 new).
Baseline: `edba2b5`. Every cost below is the `tools/integrate.py` model (92.4 × 2^magnitude, rounded to 10).

## 1. Summary

- **The tree's largest bottleneck is split.** `rocket` (756,960 RP, every factor at its loader maximum) bundled the
  liquid-propellant rocket engine (Goddard 1926; A-4/V-2 1942–44) with the multistage orbital launch vehicle (R-7, 1957).
  The key stays with the engine (name, date, `unit.missile`, and D09's `guided_missile` and `icbm` unchanged; 378,470 RP).
  The new `orbital_launch_vehicle` (189,240 RP) takes `building.spaceport`, `project.orbital_launch` and, with D05's
  consent, `satellite`'s launch prerequisite. One 757k wait becomes two, with missiles usable in between.
- **Fifteen prerequisite repairs** (`rewire`). Two remove inversions or anachronisms the strict D-047 ruling 1 forbids
  (`sledge` needed post-and-beam framing ~1,500 years after the first sledge runners; `carrack` descended from a caravel a
  century younger than the Genoese carraca). Four remove need-or-association edges that were not knowledge
  (`celestial_navigation`←caravel, `cartography`←road_paved, `horseshoe`←horse_collar, `iron_hull`←screw_propeller). Seven
  add knowledge the node genuinely needs (`aircraft`←gliding flight, `bicycle`←precision manufacture,
  `electric_vehicle`←the automobile, `jet_airliner`←superalloy turbine blades, `gps`←relativity, `collar_harness`←sewing,
  `rocket`←Newtonian mechanics in place of the calculus it contains). One widens a hull route (`sail_square`← dugout OR
  raft); one follows the split (`reusable_launch`).
- **Two content rebalances.** `road_paved` was scored with metallurgy-class material for a stone road; `locomotive` sat at
  the loader maxima for material and institutional, which no other A8 node uses.
- **One relocation.** `polynesian_canoe` (voyaging without writing) leaves the Military subtree for the Main trunk, so it
  no longer waits for the research stage that its own description contradicts.
- **Five new nodes**, each with a causal job: `orbital_launch_vehicle` (the split), `ocean_junk` (an independent route to
  the oceanic class), `balloon` and `glider` (the missing pre-history of flight) and `submarine` (the conventional ancestor
  D09 found missing).
- **Fog:** no D08 node reveals territory or writes extent (§5). Three reveal-reading strings are reworded.
- **Live consumers preserved:** all five road classes, the A3/A5/A7/A8 supporting facts (`wheel_solid`, `road_paved`,
  `caravel`, `railway`), the trade-gate atom (`sail_square`) and every entity id. MEASURED: the D08-specific scratch
  validator and the package's `integrate.py` + `validate.py`, run on a scratch copy with every lane part present at the time
  (D01–D07, D09, D10, D12), report no graph finding on a D08 node; no chronology, generation-locality or implied-Eureka
  violation exists in the integrated graph.

Net workload: D08's existing nodes 1,968,880 → 1,615,280 RP (−353,600); new nodes +263,040; lane net −90,560 RP
(−0.8 % of the 11.21 M tree) [MEASURED, scratch script on the integrated draft]. The reduction is the split; no cost was
chosen for pacing (D-046 §1).

## 2. Dispositions (55 owned nodes)

| disposition | count | nodes |
|---|---|---|
| keep, no field change | 14 | dugout, wheel_solid, wheel_spoked, mortise_hull, lateen, stern_rudder, watertight_bulkhead, cog, screw_propeller, macadam, electric_traction, motor_road, aircraft_metal, crewed_spaceflight |
| keep, capability block only (owner, milestone, deferred effect stated) | 20 | raft, cart, track_road, plank_boat, latitude_sailing, monsoon_sailing, compass_magnetic, portolan, caravel, longitude_problem, mercator, railway, steamboat, high_speed_rail, lunar_flight, space_station, container_shipping, fusion_propulsion, frontier_launch, frontier_propulsion |
| keep, text edit | 3 | helicopter (emerged), automobile_mass (assembly-line strings from D07), earth_observation (reveal strings) |
| rewire, cost unchanged | 6 | sledge, sail_square, collar_harness (also renamed), iron_hull, reusable_launch, gps |
| rewire, cost moves only through the prerequisite-complexity convention | 7 | carrack, celestial_navigation, cartography, bicycle, electric_vehicle, aircraft, jet_airliner |
| rewire + rebalance | 1 | horseshoe |
| split + rewire | 1 | rocket |
| rebalance | 2 | road_paved, locomotive |
| relocate | 1 | polynesian_canoe |
| merge / replace / retire | 0 | — |
| **new** | **5** | orbital_launch_vehicle, ocean_junk, balloon, glider, submarine |

## 3. Split, rewires, rebalances, relocation — with reasons

**Split — `rocket` (key 308) → retained "Liquid-fuel rocket" + new `orbital_launch_vehicle`.** The bundle stood for two
capabilities thirteen years apart: propulsion without air (Goddard, 16 March 1926; the A-4/V-2 from 1942) and reaching
orbit (the R-7's first full-range flight on 21 August 1957; Sputnik 1 on 4 October 1957) [RESEARCHED]. The key stays
with the engine because its consumers are the ones other lanes already build on: `unit.missile`, D09's new
`guided_missile` (the 1943 Hs 293 was boosted by a Walter liquid-propellant motor [RESEARCHED]) and `icbm`. Keeping the
key on the orbital part was designed first and rejected (§7, item 13). On D04's request `mechanics_newtonian` replaces
`calculus` in the engine's prerequisites — it contains calculus, and the rocket equation (Tsiolkovsky 1903) is Newtonian
mechanics — so orbital mechanics now reaches `satellite` and `space_probe` at no cost. `internal_combustion` stays as the
liquid-fuel combustion edge; D06 has redefined it as the high-speed petrol engine, which also brings `petroleum_refining`
(Goddard's gasoline). Factors: engine 2.0/3.5/2.0/2.5/1.0/1.0 (12.0, 378,470); orbital vehicle
1.0/3.5/2.0/3.0/1.5/0.0 (11.0, 189,240). Sum 567,710 against 756,960: −25.0 %,
exactly three-quarters by the model (the shipped node carried a 19-RP rounding). The bundle scored all six factors at their
maxima; no part reaches every maximum, and in an exponential model two parts at 12.0 and 11.0 sum to three-quarters of a
13.0 bundle. Entity moves: `building.spaceport` and `project.orbital_launch` → `orbital_launch_vehicle` (both tier G,
unrealized).

**Rewires (prerequisites repaired for causality).**

| node | old → new prerequisite | reason | cost |
|---|---|---|---|
| sledge | timber_frame → ground_stone_early | Mesolithic sledge runners (the node's own ~7,000 BCE anchor; Vis I, 7th–6th millennium BCE) predate LBK timber-frame houses (~5,500 BCE): a D-047 r1 inversion the chronology test skips because it starts at 3,000 BCE [RESEARCHED] | 520 = |
| sail_square | dugout AND loom → (dugout OR raft) AND loom | the oldest sail evidence sits on seagoing reed-bundle boats (H3 As-Sabiyah, ~5,500–5,300 BCE) [RESEARCHED] | 3,520 = |
| collar_harness | horse_domestication AND hide_working → … AND sewing | D07: stitched leather straps (sewing contains hide_working); also renamed "Breast-strap harness" (§8) | 2,490 = |
| iron_hull | puddling AND screw_propeller → puddling AND steamboat | the first iron steamship at sea, Aaron Manby (1821–22), was a paddle steamer [RESEARCHED] | 14,070 = |
| reusable_launch | rocket AND … → orbital_launch_vehicle AND … | reusable stages are reusable orbital launchers | 133,810 = |
| gps | + relativity | D04: the satellite clocks carry a relativistic rate correction; without it fixes drift by kilometres a day | 189,240 = (≥3 requirements already) |
| carrack | caravel AND cog → cog AND lateen AND compass_magnetic | Genoese "carraca" appear in the early 14th century, a century before the exploration caravel; Italian builders took the cog's square rig and rudder and kept a lateen mizzen [RESEARCHED]; compass = open-sea navigation of the oceanic class | 4,180 → 5,910 |
| celestial_navigation | astrolabe AND trigonometry AND caravel → astrolabe AND trigonometry | the caravel was the need (ships past the equator), not knowledge | 5,910 → 4,180 |
| cartography | astronomy_geometric AND road_paved → astronomy_geometric | Ptolemy's coordinates and Pei Xiu's grid maps need no paved roads; measured distance is surveying, already in astronomy_geometric's closure | 2,490 → 1,760 |
| bicycle | steel_carburized → interchangeable_parts AND steel_carburized | the 1885 safety bicycle needs precision mass-produced parts; alone it was researchable centuries early | 7,030 → 9,950 |
| electric_vehicle | + automobile_mass | an electric vehicle is an automobile with a battery drivetrain | 47,310 → 66,900 |
| aircraft | internal_combustion → internal_combustion AND glider | lift data and three-axis control came from gliders (Lilienthal 1891–96; Wright gliders 1900–02) [RESEARCHED] | 47,310 → 66,900 |
| jet_airliner | + superalloy | D02: Nimonic 80 (1941) became the standard British turbine-blade alloy; Nimonic 80A blades ran in the Comet's Ghost [RESEARCHED]; the 1939 jets ran on steels, so the edge sits here, not on jet_engine (D06 also put superalloy on gas_turbine_power; both stand) | 47,310 → 66,900 |
| horseshoe | iron_bloomery AND horse_collar → iron_bloomery AND horse_domestication (+ rebalance) | the collar was a co-adoption, not knowledge a farrier needs | 4,970 → 3,520 |

**Rebalances (factors re-scored on content).** `road_paved` material 1.5 → 0.75: a road of layered stone, gravel and lime
concrete is the `concrete_pozzolan` class (0.75), not the metallurgy class (brass, steels) that 1.5 marks at A4–A5; its
stone peers score 0–0.75 (9,950 → 5,910). `locomotive` material 2.0 → 1.5 and institutional 3.0 → 2.0: both were loader
maxima otherwise used only by A9 frontier nodes; an 1804–1829 locomotive is a high-pressure engine on wrought-iron wheels
from an engineer's workshop, and the network institution stays on `railway` (47,310 → 16,730). `horseshoe` institutional
1.75 → 1.25: farriery needs no institution (with the rewire, 4,970 → 3,520).

**Relocation.** `polynesian_canoe`: Military subtree → Main trunk (Age A6, cost 2,960 and key kept). Its prerequisites
(`dugout`, `latitude_sailing`) are trunk nodes. As a subtree node it waited for the research stage — writing, a law code
and a university-level stage — which contradicts the node's own description of voyaging "without compass or writing".

## 4. New nodes and why

| id | Age / branch | prerequisites | cost | why it is distinct |
|---|---|---|---|---|
| orbital_launch_vehicle | A9 engineering | rocket | 189,240 | the orbital half of the split (§3); gateway of the space line once `satellite` is re-pointed |
| ocean_junk | A6 military | watertight_bulkhead AND compass_magnetic AND monsoon_sailing | 4,180 | an independent route to the oceanic class: compartmented hull, compass navigation at sea (recorded 1111–1117) and the monsoon (Quanzhou wreck ~1275 with 13 compartments; Zheng He 1405–1433) [RESEARCHED]; extends `activity.ocean_shipping` to a fourth OR atom; gives two dead ends a causal consumer |
| balloon | A8 engineering | barometer_vacuum | 19,890 | the first human flight (Montgolfier and Charles, 1783) by buoyancy, not lift; military observation from 1794 (Fleurus) [RESEARCHED]; its use is exactly D-039 B4's vantage — the cleanest fog-compatible sight carrier in the tree |
| glider | A8 engineering | mechanics_newtonian | 9,950 | the aeroplane's missing knowledge: Cayley (1853), Lilienthal (1891–96), the Wright gliders (1900–02) [RESEARCHED]; without it the tree says an engine alone makes flight |
| submarine | A9 military | screw_propeller AND iron_hull AND electric_motor AND internal_combustion | 39,780 | the vessel class D09 found missing: USS Holland (commissioned 12 October 1900: petrol engine on the surface, battery-electric drive submerged) [RESEARCHED]; the conventional ancestor of D11's `nuclear_submarine` (re-point requested) |

Every new node has an era-marked `emerged` (the marker-free pin does not move for them), a cost analog and a per-factor
cost note in the part file. INFERENCE labels: the causal readings `balloon ← barometer_vacuum` (aerostatics presupposes air
as a weighable fluid) and `glider ← mechanics_newtonian` (Cayley's lift–drag–thrust analysis); the dates are RESEARCHED.

## 5. Navigation and fog (RATIFIED D-040; this lane's position, PROPOSED)

- **No D08 node reveals territory or writes extent.** Completion writes only the completion row. Discovery happens only by
  reaching the land (D-040 B2: reconnaissance at another scale).
- **Three reveal-reading strings are reworded:** `cartography` "D-040 B1 computed extent made explicit" → maps record the
  extent already discovered; `gps` "D-040 B1 extent: known everywhere" → position fixing within known extent, no map
  knowledge; `earth_observation` "global mapping" → orbital imaging, a proposed observer, no reveal. D05 keeps
  `satellite`'s reconnaissance string as pointed and never a reveal.
- **Navigation grants ACCESS eligibility, never reach.** Transport classes (`access:sea-edge.*`: open crossing — raft;
  coastal sail — sail_square; oceanic — caravel, carrack, polynesian_canoe, ocean_junk) are eligibility only, realized as
  network edge types (D-040 B4; ADR-032 §3 "future modes") by the transport packet where a settlement has coast, timber and
  builders (D-040 B3, D-047 r7). A landlocked polity never uses them; nobody schedules either.
- **Travel and sight parameters, owned elsewhere.** Compass, latitude sailing, celestial navigation, chronometer,
  portolan and Mercator charts become reliability or route-planning parameters of *realized* sea edges (transport packet).
  Aerial observers — a realized balloon (M7 asset), aircraft (M7), imaging satellites (M11+) — are proposed as
  dimensionless sight-budget multipliers (D-039 B4; m6-spec §3.2), pointed not global (D-039 B3), owned by the fog lane.
  OPEN: the D-054 instruction keeps satellite reveal out of scope; the orbital observer is a proposal only.
- **One fog dependency is recorded:** `Pathfinder.Isochrone` never enters water nodes [MEASURED, fog audit §4.7], so sight
  and extent must be able to follow realized voyages over water once sea edges exist (request to the fog lane).

## 6. Realization: what is live today and what is deferred

| consumer | D08 node(s) | today [MEASURED] | after this proposal |
|---|---|---|---|
| road classes (RoadDevelopmentSystem) | track_road (Trackway; Built road with stone_dry), road_paved, macadam, motor_road | live | ids, entities and prerequisites unchanged; road_paved cheaper |
| Age supporting facts | wheel_solid (a3_wheel), road_paved (a5_roads), caravel (a7_ocean), railway (a8_rail) | live | unchanged |
| trade gate (OR atom) | sail_square | live | unchanged; sail_square also reachable through raft |
| activity.coastal_shipping / activity.ocean_shipping | sail_square / caravel, carrack, polynesian_canoe | declared, no realizing system (tier C, "frozen transport") | + ocean_junk; transport packet (DW-059), M10+ |
| building.harbour, building.dry_dock, building.airfield | plank_boat (+stone_dry), carrack, aircraft | unrealized (tiers B, E, G) | unchanged |
| building.spaceport, project.orbital_launch | rocket | unrealized (tier G) | move to orbital_launch_vehicle; M11+ |
| unit.ironclad, unit.military_aircraft, unit.aircraft_carrier, unit.helicopter, unit.missile | screw_propeller + iron_hull, aircraft, helicopter, rocket | eligibility only | unchanged; M7 |
| infra.railway, infra.courier_relay | railway; track_road + horse_domestication | unrealized | transport packet; courier relay is D12's physical courier system |
| draught haulage (queue Q-C) | cart, sledge, collar_harness, horseshoe | not modelled | transport packet's computed bulk-cost lever, M10+ |

The transport packet (DW-059) is unscheduled and owes a Director ruling; until then every sea, rail, air and draught
effect is knowledge-only or deferred, stated as such in each capability block (milestone M10+; M11+ where no owner exists
before then: space capstones, electric vehicles, repeatables).

## 7. Candidates considered and rejected

1. **`wagonway` as railway's prerequisite.** `railway` already carries an append-only Eureka "Mine wagonways are in use"
   (when: `mining_shaft`) [MEASURED]; a wagonway prerequisite would make it semantically implied and, if the wagonway
   required `mining_shaft`, loader-refused as an implied Eureka (rule 24).
2. **Splitting the motor car from `automobile_mass`.** `internal_combustion` already declares "motor vehicle"
   [MEASURED], and D06 now defines it as the Daimler–Maybach and Benz high-speed petrol engine (1883–86);
   `automobile_mass` is the mass-production increment (novelty 0.5). A split would duplicate D06.
3. **`cart ← sledge`** ("the first wagons were sledges on wheels"). The reading of the Uruk wagon sign was found only in
   informal sources; INFERENCE, not added. `sledge` gets a deferred owner instead.
4. **Lunar-distance longitude** beside the chronometer. No consumer distinguishes the two solutions; revisit when the
   transport packet reads "longitude known".
5. **Hydrographic surveying.** No consumer distinguishes charted from uncharted known coast; it would only move the dead
   end from `mercator` to itself.
6. **Sextant / octant (1731).** Part of the celestial-navigation and longitude story; no separate consumer.
7. **Pack animals / caravans.** `donkey` and `camel` (D03) already carry pack transport into the trade gate;
   `activity.caravans` exists.
8. **Norse longship / keel.** Keels are a hull element of plank, mortise and cog hulls; the North Atlantic voyages founded
   no lasting oceanic class.
9. **Airship.** Its sim capabilities (air transport, bombing) belong to `aircraft`; the lighter-than-air principle is
   `balloon`'s.
10. **Splitting `gps` (Transit 1964).** Not a bottleneck relative to its closure; no capability difference in the sim.
11. **A sixth road class (tarmac).** The five classes are ratified (ADR-032 ruling 16; speedFactor ladder validated).
12. **Lighthouse or harbour nodes.** `building.harbour` already exists on `plank_boat`; no consumer.
13. **Keeping the split key on the orbital part** (designed first). D09's new `guided_missile` (1943) and `icbm` consume
    `rocket` as the engine: re-dating the key to 1957 would fail the D-047 chronology test (`ResearchContentTests`) and
    need a corpus-name exception, while D05 had already consented to re-pointing `satellite`.
14. **A de Laval nozzle edge (`steam_turbine`) on the rocket.** A further requirement for knowledge internal to the
    engine.
15. **Moving every A6 naval craft node to the trunk.** A structural question (open question 3); only the self-contradicting
    case moved.
16. **Re-dating `stern_rudder` A6 → A5** (~100 CE China). Re-dating re-prices; open question 5.
17. **Re-dating `electric_traction` A9 → A8.** Its 1881–1890 dates sit with its A9 peers (steam_turbine 1884,
    aluminium 1886).
18. **Merging `lunar_flight` / `space_station` into `crewed_spaceflight`.** Distinct achievements a decade apart; a merge
    loses keys for nothing.
19. **Adding `ocean_junk` to the `a7_ocean` fact.** Age content; open question 4.
20. **A torpedo node** (D09's suggestion). A munition, D09's domain like `guided_missile`.

## 8. Conflations found

- Engine vs orbital launch in `rocket` → split.
- Breast-strap harness vs padded collar in `collar_harness` (the collar is `horse_collar`'s, D01) → renamed "Breast-strap
  harness", emerged "~400–300 BCE China (Warring States); universal under the Han".
- Player knowledge vs world truth: three capability strings promised map knowledge from research → reworded (§5).
- Need, chronology or co-adoption vs knowledge: `celestial_navigation`/`carrack` (caravel), `horseshoe` (collar),
  `iron_hull` (screw: co-occurrence in SS Great Britain), `cartography` (Roman roads) → rewired.
- One hull tradition vs regional materials: `sail_square` (log vs reed bundle) → OR.
- Research vs physical production (D-047 r7): transport classes stay eligibility; vessels and edges are realized by the
  transport packet. Eligibility vs adoption: a vantage exists only for a realized observer.
- Checked and not conflated: the motor car's invention (`internal_combustion`) vs its mass production
  (`automobile_mass`).

## 9. Dead ends resolved

Baseline V09 list for D08 [MEASURED, `validation-report.md`]: 17 nodes, plus `longitude_problem`, which became a leaf
when D04 removed `atomic_clock`'s edge to it.

| node | resolution |
|---|---|
| raft | dependant `sail_square` (OR route) |
| monsoon_sailing, watertight_bulkhead | dependant `ocean_junk` |
| bicycle | consumer is `aircraft`'s machine-evaluable Eureka (0.4); otherwise knowledge-only, stated |
| cart, sledge, horseshoe | transport packet, draught-animal lever Q-C (M10+) |
| container_shipping, high_speed_rail, jet_airliner, mercator, longitude_problem | transport packet: sea-freight class, rail speed class, air corridors, route planning, route safety (M10+) |
| electric_vehicle | no fuel or energy system before M11+ (framework §8); knowledge-only, stated |
| earth_observation | proposed orbital observer (fog lane, OPEN) and possible remote-sensing identification (D11, PROPOSED); M11+ |
| lunar_flight, space_station | knowledge-only capstones, no space system before M11+, justified (educational weight; §7 item 18) |
| frontier_launch, frontier_propulsion | repeatables: recursive mechanics deferred (D-046 §G4); a finite node may not require a recursive one (loader rule 27) |

New leaves carry owners: `balloon` (M7 asset + fog lane), `submarine` (M7 identity + D11 re-point).

## 10. Cost findings

| node | old → new RP | why |
|---|---|---|
| rocket (split) | 756,960 → 378,470 (+ orbital_launch_vehicle 189,240) | §3 |
| locomotive | 47,310 → 16,730 | material and institutional at loader maxima, out of line with every A8 peer |
| road_paved | 9,950 → 5,910 | material scored as metallurgy |
| horseshoe | 4,970 → 3,520 | institutional without a reason |
| cartography / celestial_navigation | 2,490 → 1,760 / 5,910 → 4,180 | convention: one requirement fewer |
| carrack / bicycle | 4,180 → 5,910 / 7,030 → 9,950 | convention: one requirement more |
| aircraft / electric_vehicle / jet_airliner | 47,310 → 66,900 each | convention: one requirement more |

Closures on the integrated draft (they include the other lanes' edits) [MEASURED]: `road_paved` 17,370 → 13,330;
`railway` 494,990 → 470,580; `rocket` 1,605,210 → 1,303,690; `aircraft` 677,270 → 797,300; `gps` 4,077,540 →
3,671,880. `satellite` gains 189,240 once re-pointed. Factor notes for every re-scored and new node are in the part file.

## 11. Alternative routes (OR, independent inventions or regional materials)

- `sail_square`: log hull (`dugout`) OR reed-bundle hull (`raft`).
- The oceanic class (`activity.ocean_shipping`) now has four independent routes: Iberian caravel (lateen + rudder +
  compass), Mediterranean carrack (cog + lateen + compass), Pacific voyaging canoe (canoe + star latitude, now pre-stage),
  Chinese ocean-going junk (bulkheads + compass + monsoon).
- `latitude_sailing`: literate (`astronomy_records`) OR non-literate sky-watching (`calendar_lunar`) — kept, confirmed
  for D03.

## 12. Cross-lane requests

Sent (12, in the part file): D05 — re-point `satellite` to `orbital_launch_vehicle` (D05 consented; integrator may apply)
and `submarine_cable` ← steamboat (the 1850–51 cables were laid from a paddle tug and a towed hulk [RESEARCHED]); D11 —
re-point `nuclear_submarine` to the new `submarine`, and own any remote-sensing identification on `earth_observation`;
D09 — no edit needed for the split, `submarine` offered as a carrier for a future identity, torpedo left to D09, naval
transport identities mapped; fog lane — vantage multipliers, voyages over water, no extent writes; replies closing the
requests of D02, D03, D04, D07 and D12; FYIs to D01 and D06.

Received and answered: D01 (acknowledged), D02 (superalloy → jet_airliner: accepted; D06 also took superalloy on
gas_turbine_power, both stand), D06 (FYI on the redefined `internal_combustion`: `rocket` keeps it as liquid-fuel
combustion engineering and now inherits `petroleum_refining`), D03 (calendar_lunar stand-in:
confirmed; ownership respected), D04 (relativity → gps, mechanics_newtonian → rocket: accepted; longitude_problem:
justified leaf), D05 (orbital part named; re-point requested), D07 (sewing → collar_harness, assembly-line strings:
accepted), D09 (keep rocket and aircraft: kept; torpedo declined; submarine added), D12 (cartography for general_staff:
consistent).

## 13. Compatibility (summary; per-record detail in the part file)

- **Saves** (MEASURED, engine audit §7.3/Q14): keys never change. Old saves holding `rocket` keep `unit.missile`, lose
  eligibility for the inert spaceport and orbital-launch entities, and must research `orbital_launch_vehicle` before a
  re-pointed `satellite`; no recorded save holds `rocket` (A9 is unreached in every baseline run). Lowered costs complete
  progress already above them; new requirements leave some partial progress dormant until met (stated per record).
- **Tests and pins:** corpus-name exception list +1 (`collar_harness` renamed; `rocket` is not); branch counts trunk +1,
  military +1 net, engineering +3 (D08 alone); finite per subtree and the `finite_nodes_to_exhaust` descriptors move;
  marker-free `emerged` count 83 → 82 (`helicopter` now dated); unlock audit, corpus audit and calibration report
  regenerate; AI-empire goldens may move. Unaffected: `RoadDevelopmentTests` (road ids, prerequisites and `motor_road`
  untouched), `TradeResearchUnlockTests`, the ruling-7 assertions in `ResearchContentTests` (`ocean_shipping` still on
  caravel, carrack and polynesian_canoe), the allocation scenario keys.

## 14. Open questions

1. OPEN DECISION (raised once by the package): an alias/remap seam for splits (the `rocket` case above).
2. OPEN DECISION: schedule the transport packet (DW-059: Q-A rivers, Q-C draught animals, Q-E routes, D-040 sea
   travel) — the single realization owner of every D08 sea, rail, air and draught class.
3. Should A6 shipwright and farrier crafts (`stern_rudder`, `cog`, `watertight_bulkhead`, `horseshoe`) wait behind the
   research stage in subtrees?
4. `a7_ocean` names only `caravel` while four nodes realize the oceanic class: keep it as the early-modern Atlantic route
   or widen it to any_of?
5. `stern_rudder` is tagged A6 though its earliest centre is ~100 CE China; re-dating would re-price it.
6. Can coastal access be partly realized before the transport packet through the fog lane's coastal extent (fog audit
   open question 3)?
7. If neither D05 nor an integration override re-points `satellite`, it stays reachable from the bare engine — a causal
   gap, not a loader error.

## 15. Sources consulted (RESEARCHED claims)

- Sail on reed-bundle boats: [H3 (Kuwait)](https://en.wikipedia.org/wiki/H3_(Kuwait)); Carter 2006, *Antiquity*
  ([Durham repository](https://durham-repository.worktribe.com/OutputFile/1559539)).
- Carrack: [DHM, Carrack](https://www.dhm.de/mediathek/en/ship-types/milestones-in-the-history-of-european-shipbuilding/07-carrack/);
  [Southampton CMA, "When is a cog not a cog?"](https://cma.soton.ac.uk/when-is-a-cog-not-a-cog);
  [Mariners' Museum, Carrack](https://exploration.marinersmuseum.org/watercraft/carrack-nao).
- Harness: [Horse harness](https://en.wikipedia.org/wiki/Horse_harness); Needham 1960 papers
  ([Cambridge archive](https://archivesearch.lib.cam.ac.uk/repositories/21/archival_objects/334250)).
- Compass at sea and the junk: [History of the compass](https://en.wikipedia.org/wiki/History_of_the_compass);
  [Zhu Yu](https://en.wikipedia.org/wiki/Zhu_Yu_(author)); [Quanzhou ship](https://en.wikipedia.org/wiki/Quanzhou_ship);
  [Fujian, watertight bulkheads](https://wb.fujian.gov.cn/English/momentsinfujian/201801/t20180111_736966.htm).
- Iron hull: [Aaron Manby](https://en.wikipedia.org/wiki/Aaron_Manby).
- Rocketry: [Smithsonian, first liquid-fuel rocket](https://pioneersofflight.si.edu/node/470);
  [R-7 Semyorka](https://en.wikipedia.org/wiki/R-7_Semyorka); [Henschel Hs 293](https://en.wikipedia.org/wiki/Henschel_Hs_293);
  [Walter HWK 109-507](https://en.wikipedia.org/wiki/Walter_HWK_109-507).
- Balloons: [Science History Institute, first balloons](https://www.sciencehistory.org/stories/magazine/artificial-clouds-and-inflammable-air-science-and-spectacle-first-balloon);
  [French Aerostatic Corps](https://en.wikipedia.org/wiki/French_Aerostatic_Corps).
- Gliders: [Wright Brothers Aeroplane Co., the century before](https://wright-brothers.org/History_Wing/History_of_the_Airplane/Century_Before/Century_Before_Intro/Century_Before_Intro.htm);
  [George Cayley](https://www.madehow.com/inventorbios/74/George-Cayley.html).
- Helicopter: [Focke-Wulf Fw 61](https://en.wikipedia.org/wiki/Focke-Wulf_Fw_61).
- Submarine: [USS Holland (SS-1)](https://en.wikipedia.org/wiki/USS_Holland_(SS-1));
  [GlobalSecurity, SS-1](https://www.globalsecurity.org/military/systems/ship/ss-1.htm).
- Cable ships: [DatacenterDynamics, history of cable ships](https://www.datacenterdynamics.com/en/analysis/a-brief-history-of-cable-ships/);
  [Submarine Telegraph Company](https://en.wikipedia.org/wiki/Submarine_Telegraph_Company).
- Superalloy: [Nimonic](https://en.wikipedia.org/wiki/Nimonic);
  [INCO Triangle 1957](https://www.sudburymuseums.ca/triangle/data/INCOTriangle-19570701.pdf).
- Sledges: [Fennoscandia archaeologica (sledge runners)](https://journal.fi/fennoscandiaarchaeologica/article/download/126696/76514/265858);
  [Folia Archaeologica 27 (Vis I)](https://library.hungaricana.hu/en/view/FoliaArchaeologica_27/?pg=164);
  rejected wagon reading: [Bronocice pot](https://en.wikipedia.org/wiki/Bronocice_pot).

Citations record where a claim can be checked; the corpus stage records verification separately.
