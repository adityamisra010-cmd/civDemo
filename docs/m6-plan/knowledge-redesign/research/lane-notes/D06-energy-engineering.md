# D06 — Energy & engineering: lane notes

**Status: PROPOSED design input for Director review. Nothing here is ratified or describes existing behaviour unless
it is labelled MEASURED.** Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION
DEFERRED. Canonical data: `data/proposal-parts/D06-energy-engineering.json` (52 existing records, 5 new, one entity
re-point, four integration requests). Baseline: `edba2b5`. Educational descriptions are not written here (a later stage
does that). Costs quoted are the integrator's (`92.4 × 2^m`, rounded to tens).

## 1. Summary

D06 owns 52 nodes across four families: early pyrotechnology and mills (trunk), steam, electricity and combustion
(Engineering, A7–A9) and computing (Engineering, A9). The lane found one missing piece of infrastructure knowledge,
three bundled bottleneck nodes, one missing prime mover, several proxy edges and two craft nodes locked behind the
universities.

- **Grid finding.** `infra.power_grid` existed but was gated by the same node as the power station
  (`electricity_generation`, "Central power station and grid"). That node is split: the key keeps generation and the A9
  CORE fact; new `ac_transmission` (transformer 1884–85, three-phase lines 1891) gates the grid entity and the
  grid-scale generation types (hydro, wind, storage, smart grid). D11 keeps `electricity_generation` on
  `nuclear_power`; D06 offers `ac_transmission` as the precise "grid know-how" edge (Director's choice).
- **Bundles split along their own text, key on the consumer-preserving part.** `internal_combustion` ("Otto 1876
  four-stroke; Daimler/Benz 1885 vehicle") → `gas_engine` (Lenoir/Otto stationary engine, the principle) + the key as
  "High-speed petrol engine" (Age fact and every vehicle consumer; now requires petroleum refining), −22.6 %.
  `computer` ("ENIAC 1945, EDSAC 1949") → `programmable_computer` (relay OR valve routes; D05's request) + the key as
  the stored-program computer (memory from radar), workload conserved. `transistor` → `semiconductor_physics`
  (understanding) + the key as the device, −20.3 %.
- **Missing knowledge:** `water_turbine` — dams drive turbines, not water wheels.
- **Causal repairs:** the Newcomen engine no longer needs cast-iron cannon (its first cylinders were brass);
  charcoal needs felling axes, not house framing; lithium-ion needs quantum theory of solids; combined-cycle gas
  turbines need superalloys (D02); the smartphone needs digital cellular data (D05).
- **Coupling:** both windmills move to the trunk (millwrights' craft; D-044 R15).
- **`steam_rotary` is kept** — no faithful split exists (§7).
- Edges to other lanes' new ids (`thermodynamics`, `probability_statistics`, `automatic_switching`) are recorded as
  `integrationRequests` in the integrator's override format, not written into `prereq`.
- Written lane workload: 2,819,390 → 2,782,390 RP (−37,000); with the integration requests +33,450 → net −3,550.

## 2. Dispositions (52 owned nodes)

| disposition | count | nodes |
|---|---|---|
| keep, unchanged | 21 | fire_making, trip_hammer, steam_separate_condenser, steam_rotary, steam_high_pressure, dynamo, electric_light, electric_motor, jet_engine, microprocessor, cmos_vlsi, programming_languages, operating_system, relational_database, personal_computer, public_key_crypto, gpu_parallel, deep_learning, large_language_models, cyber_warfare, quantum_computing |
| keep, prerequisite edit at unchanged cost | 9 | charcoal, diesel*, hydroelectric, solar_pv, lithium_battery, wind_turbine_modern, grid_storage, smart_grid, smartphone |
| keep, capability block or integration note only | 14 | lamp, bellows, water_mill, steam_turbine*, machine_learning*, integrated_circuit, cloud_computing, weather_prediction, fault_tolerant_quantum, general_ai, space_solar, frontier_energy, frontier_computation, frontier_cyber |
| split (+ rebalance of the retained part) | 4 (3) | electricity_generation; internal_combustion, computer, transistor (also rebalanced) |
| rebalance (prerequisite convention only) | 2 | steam_atmospheric, gas_turbine_power |
| relocate (engineering → trunk) | 2 | windmill_vertical, windmill_post |
| merge / replace / retire | 0 | — |
| **new** | 5 | ac_transmission, gas_engine, programmable_computer, semiconductor_physics, water_turbine |

\* carries an integration request (§13); diesel and steam_turbine become `rebalance` when it is applied.
Totals: keep 44 (21 + 9 + 14) + split 4 + rebalance 2 + relocate 2 = 52. The JSON is authoritative.

## 3. Splits, rebalances and relocations — with reasons

**`electricity_generation` (288) — split → `ac_transmission`.** The node bundled central generation with local DC
distribution (Pearl Street and Holborn Viaduct, 1882) and high-voltage AC transmission (ZBD closed-core transformer
1884–85; Lauffen–Frankfurt three-phase line 1891; Niagara–Buffalo 1896 — RESEARCHED). ADR-019 §5.1 itself separates
"generation capacity, grid connectivity and demand". The key keeps generation, `building.power_station`, the A9 CORE fact
and its dependants (aluminium, desalination, electric_motor, gas_turbine_power); renamed "Central power station";
factors unchanged (47,310). `infra.power_grid` moves to the new node (`entityRequirementChanges`). Split sum 70,960 vs
47,310 (+50 %, below the doubling bound): the grid is new, separately priced workload on an optional branch, off the
Age 9 core path.

**`internal_combustion` (291) — split → `gas_engine`, rebalance.** Its own text bundles "Otto 1876 four-stroke;
Daimler/Benz 1885 vehicle". The stationary gas engine (Lenoir 1860; Otto's four-stroke 1876, coal gas) is the principle
and the root of Diesel's engine; the high-speed petrol engine (Daimler–Maybach and Benz, 1883–86, independently) needed
light construction and volatile liquid fuel (ligroin — RESEARCHED) and is what aircraft, cars, tanks, tractors,
rockets and the new submarine use. The key stays with the high-speed engine, which keeps the A9 CORE fact
`a9_combustion` and every consumer — no other lane must re-point (D01, D08, D09 already require it for vehicles). It is
renamed "High-speed petrol engine", now requires `gas_engine AND petroleum_refining`, novelty 2.0 → 1.0 (the paradigm is
scored once, on `gas_engine`), prereq_complexity 1.0 → 0.5: 133,810 → 47,310. `gas_engine` 56,260. Split sum 103,570
(−22.6 %). `diesel` moves from `internal_combustion` to `gas_engine`.

**`computer` (315) — split → `programmable_computer`, rebalance (D05 request, accepted).** Its text bundles "ENIAC 1945
(tube), EDSAC 1949 (stored program)". The first programmable machines (Zuse's relay Z3 1941; Colossus 1943–44; ENIAC
1945) had two independent hardware routes; the stored-program computer (Manchester Baby 1948, EDSAC 1949) is what all
fourteen dependants need. Key, `a9_computing` and dependants stay on 315; the retained part requires
`programmable_computer AND radar` (program memory came from radar: mercury delay lines and the Williams–Kilburn CRT
store — RESEARCHED; radar implies vacuum_tube). Novelty 2.0 → 1.0; both parts 267,620 (sum 535,240 vs 535,250).
`punched_card_tabulation` is deliberately not a prerequisite: it requires jacquard, which would make computer's
append-only Eureka (when: jacquard) implied — the loader refuses that.

**`transistor` (316) — split → `semiconductor_physics`, rebalance.** Understanding (band theory 1931, rectifier theory
1938–39, the p–n junction and its photovoltaic effect 1940 — RESEARCHED) versus its first great application (1947–48).
The key keeps the device and its consumers; the science also feeds `solar_pv`. Novelty 2.0 → 1.0 (paradigm scored once
on the science): 267,620 → 133,810; `semiconductor_physics` 79,560; sum 213,370 (−20.3 %). Answer to D02: the
crystal-growth/zone-refining step stays in the device's material factor (2.0) — no separate materials node.

**`steam_atmospheric` (237) — rebalance.** `cannon_cast_iron` removed: Newcomen's first cylinders (1712) were brass and
Coalbrookdale supplied cast-iron cylinders from the 1720s (RESEARCHED), i.e. iron founding (`cast_iron`), not cannon
founding; the cannon-boring lineage (Wilkinson 1774) already reaches the rotative engine through `cylinder_boring`.
prereq_complexity 1.0 → 0.5 by convention: 33,450 → 23,650 (the A8 core closure falls by the same 9,800).

**`gas_turbine_power` (354) — rebalance (D02 request, accepted).** + `superalloy`: continuous-duty turbine blades need
nickel superalloys (Nimonic 80 from 1941). Not on `jet_engine` (D-047 row 11). prereq_complexity 0.5 → 1.0:
47,310 → 66,900. D08 independently put superalloy on `jet_airliner`; consistent.

**`windmill_vertical`, `windmill_post` — relocate to the trunk.** Millwrights' craft (Sistan panemone; English and
Flemish post mills ~1180 CE) gated by the university stage (measured ~1532 CE in seed 42). Prerequisites are trunk, no
dependants, A6 (V17 not triggered), windmill_post's Eureka condition (gearing) is trunk. Cost and Age unchanged; trunk
nodes pay BaseCost. `building.windmill` and the second route to `a6_mills` become stage-free.

## 4. Prerequisite and capability edits at unchanged cost (one line each)

- `charcoal`: timber_frame → ground_stone_early (felling axe; house framing is a proxy).
- `diesel`: internal_combustion → gas_engine (Diesel's engine descends from the four-stroke stationary engine).
- `hydroelectric`: electricity_generation → ac_transmission; water_mill → water_turbine (3 requirements before and after).
- `solar_pv`: quantum_mechanics → semiconductor_physics (Ohl's 1940 junction photovoltaic effect).
- `lithium_battery`: electromagnetism → quantum_mechanics (intercalation chemistry; the battery lineage — Volta, now in
  D04's electromagnetism — stays implied). Shortest prerequisite chain was 12 nodes for a 1991 product.
- `wind_turbine_modern`, `smart_grid`: electricity_generation → ac_transmission (grid-connected; Gedser 1957 used an
  induction generator synchronised to the grid — RESEARCHED).
- `grid_storage`: → `ac_transmission AND (lithium_battery OR hydroelectric)` (pumped storage added; §11).
- `smartphone`: mobile_phone → cellular_digital (D05 request; gives cellular_digital a dependant).
- `bellows`: capability text "iron smelting feasible" (no edge carries it) → "forced draught for hotter furnaces".
- Capability blocks corrected to the right owner and milestone: water_mill, windmills, electricity_generation,
  integrated_circuit, hydroelectric, solar_pv, wind_turbine_modern, cloud_computing, space_solar (tier-G energy and
  industry entities → M10+, i.e. M11+ content), plus the dead ends in §9.

## 5. New nodes and why

| id | Age · branch | prereq | cost | why |
|---|---|---|---|---|
| `ac_transmission` | A8 · engineering | electricity_generation | 23,650 | grid connectivity ≠ generation; gates infra.power_grid and grid-scale generation (split) |
| `gas_engine` | A8 · engineering | steam_rotary AND chemistry_quantitative AND milling_machine | 56,260 | the stationary Otto-cycle engine; root of the petrol and diesel engines (split) |
| `programmable_computer` | A9 · engineering | (vacuum_tube OR telephone) AND calculus → at integration telephone → automatic_switching | 267,620 | relay or valve programmable machines, 1941–45 (split; D05) |
| `semiconductor_physics` | A9 · natural_science | quantum_mechanics AND radio | 79,560 | the science the transistor and solar cell were engineered from (split) |
| `water_turbine` | A8 · engineering | water_mill AND mechanics_newtonian | 14,060 | the prime mover of hydro stations and 19th-century mills (missing knowledge) |

Every new node has an era-marked `emerged` (the 83 marker-free pin is unchanged), a dependant, one primary university
serving its branch, and a capability block. Families: gas_engine "ice", programmable_computer "computing",
semiconductor_physics "electronics", water_turbine "mill" (no generation — keeps generation locality green);
ac_transmission "electricity" generation 4.

## 6. Grid finding (the coordinator's question)

MEASURED: `infra.power_grid` (infrastructure entity, no realizing system) required `electricity_generation`, the same
node as `building.power_station`; no transmission or distribution knowledge existed. PROPOSED: `ac_transmission`
gates it (entity re-point, reverse-index unlock moved); hydroelectric, wind_turbine_modern, grid_storage and smart_grid
require it. D11's final part keeps `electricity_generation` on `nuclear_power` ("Nuclear power-plant engineering")
and holds that no civil-nuclear station enters any energy/grid contract without a CR (frozen Spine); D06 accepts
that and offers `ac_transmission` only as the optional, more precise knowledge edge for the "grid know-how" D11's
brief names (unit size ≤ ~10 % of grid load, NUC-221) — same requirement count, so no cost change. Realization: the
electricity capacity system of ADR-019 §5.1 has no owner before M11+; all grid and generation entities stay
eligible-but-unrealized (milestone M10+ in the validator vocabulary).

## 7. Candidates considered and REJECTED

- **Splitting `steam_rotary` (A8 CORE).** (a) Carving out the centrifugal governor (1788): a later, separate invention
  not in the node's content; it cannot lower the rotative engine's own magnitude without re-scoring its novelty, which
  on an Age CORE node would read as pacing-driven re-pricing (D-046 §1). (b) Rotary conversion vs double action and
  parallel motion: one engine (1781–84), same dependants. (c) Factory mill-work: no separable consumer; D07's domain.
- **`centrifugal_governor` as a new node** (feeding electricity_generation or the grid): real and educational, but
  every prime mover already carried a governor; as a prerequisite it would be a hoop, not a decision.
- **Downstream `petrol_engine` with the key on the stationary engine.** Would have forced D01/D08/D09 to re-point four
  vehicle nodes and broken old saves' vehicle chains; the key now stays with the consumer-preserving part.
- **A voltaic-pile / electrochemistry node.** D04 re-scoped `electromagnetism` to start at Volta's battery (1800),
  which already roots the battery lineage; a separate node would duplicate it.
- **A D06 `thermodynamics` node.** D04 owns it and asked D06 not to duplicate; D06 wires diesel and steam_turbine to it.
- **Lead–acid storage battery (1859).** No consumer is modelled (early electric cars, submarine batteries, load
  levelling); folded into the battery lineage.
- **Gas lighting / coal-gas supply.** Its only consumer would be the gas engine; recorded as gas_engine's missing input.
- **A semiconductor-crystal materials node** (D02's question). It would double-count the device's material factor.
- **Splitting `integrated_circuit`** (planar process vs chip): one coherent invention; a split would only re-price it.
- **Control theory / servomechanisms (1920s–40s).** A genuine hub for guided munitions, NC machine tools and rockets,
  but those consumers belong to D07/D08/D09; flagged, not added.
- **Hollerith tabulation as computer's prerequisite** (D05/D07 suggestion): loader-refused (implied Eureka).
- **Magnetic drum memory as an OR route for the stored-program computer:** historically slightly later than the radar
  memories; not modelled.
- **`jet_engine ← superalloy`:** D-047 row 11 removed it (chronology); superalloy goes to gas_turbine_power instead.
- **Edges for `lamp`** (mining_shaft, lighthouse): torches and open fires served both; not knowledge actually needed.
- **`bellows → iron_bloomery`:** fails the chronology test at the corpus date; re-dating is left to the fact-check stage.
- **Re-dating steam_turbine (1884) and diesel (1897) to A8:** would re-price them through the Age prior (C-14); asked
  as an open question instead.
- **`dynamo ← (steam_rotary OR water_turbine)`:** the generator's knowledge does not depend on its drive; the OR would
  only change the cheapest path.
- Solar thermal, geothermal, tidal, fuel cells, HVDC: no consumer and no decision value at this granularity.

## 8. Conflations found

- Generation vs grid connectivity (`electricity_generation`) — split.
- Principle vs vehicle application, and engine vs fuel (`internal_combustion`) — split; petroleum knowledge explicit.
- Programmable calculation vs stored-program computing (`computer`) — split.
- Understanding vs application (`transistor`) — split.
- Water wheel vs water turbine (`hydroelectric`) — new node.
- Battery chemistry with no solid-state science (`lithium_battery`) — rewired.
- Military cannon casting as a proxy for engine cylinders (`steam_atmospheric`); house framing as a proxy for
  cordwood (`charcoal`) — removed.
- Craft knowledge vs organized scholarship (windmills) — relocated.
- A capability text that no edge realizes (`bellows`) — corrected.

## 9. Dead ends resolved (baseline V09 list for this lane)

| node | resolution |
|---|---|
| grid_storage, smart_grid | deferred owner: M11+ electricity capacity system (storage time-shift; demand response) |
| frontier_energy, frontier_computation, frontier_cyber | deferred owner named from their own DOCUMENTED descriptors (energy system; administration/automation; D-039 information quantities); levels stay D-044 T6 OPEN |
| weather_prediction | deferred owner M9: arch-JKL §9.4 designs forecasting as an observability readout and records its knowledge carrier as owed (OWED-K1) — this node |
| smartphone | candidate owner M8: D-041 C1 "media and information reach, once such a system exists" (no mechanism designed) |
| general_ai | candidate owner M10+: the labour-per-output coefficient documented on frontier_computation |
| lamp | explicit justification — accepted knowledge-only leaf (validator WARN remains) |
| fault_tolerant_quantum | explicit justification — speculative capstone kept per D-046 (validator WARN remains) |

## 10. Cost findings

- **Split sums** (framework §7): electricity_generation +50 % (new grid workload on an optional branch);
  internal_combustion −22.6 %; computer ±0; transistor −20.3 %. Re-scores move novelty only where the paradigm moves to
  the carved part ("scored once") and prereq_complexity only by convention (C-10/C-26).
- **Age CORE closures, D06 changes alone (MEASURED, kr_lib greedy closure):** a8_steam 389,980 → 379,040;
  a9_electricity 560,960 → 550,020; a9_combustion 629,960 → 626,070 (split −30,240; petroleum_refining with
  mining_shaft and stone_dry +37,290; Newcomen −9,800; charcoal −1,140); a4_iron 14,320 → 12,130 (charcoal). With all
  twelve lane parts and D06's integration requests: 385,210 / 532,920 / 623,940 (a4_iron 24,990 after D02's kiln
  edge; D04 drops mining_shaft from petroleum_refining).
- **Single-node waits:** largest D06 node falls from 535,250 to 267,620; internal_combustion 133,810 → 47,310 + 56,260;
  transistor 267,620 → 133,810 + 79,560.
- **Integration-time increases:** diesel 33,450 → 47,310, steam_turbine 47,310 → 66,900 (third requirement).
- **OPEN:** steam_rotary novelty 2.0 vs steam_separate_condenser 0.5; jet_engine (novelty 0.5, material 0.0 → 11,830)
  under-scored against steam_turbine/diesel; A8/A9 Age tags inconsistent (steam_turbine 1884 and diesel 1897 are A9,
  haber_bosch 1913 A8). Not re-scored here (D-046 §1; D-047 last ruled on jet_engine).
- Tier labels (no definition in the tree): internal_combustion, computer, transistor T4 → T2 by the measured
  novelty correlation; new nodes T2/T4 by novelty.

## 11. Alternative routes

- `grid_storage`: battery OR pumped-storage hydro — genuinely different technologies; pumped storage holds most installed
  storage capacity (over 94 % by IHA's count — RESEARCHED). Full closures are of the same order (856,140 vs 905,220 on
  the D06-only graph; 1,003,230 vs 885,010 with all current lane parts), so the OR is not a cost shortcut.
- `programmable_computer`: valves OR relays (Zuse's Z3 vs Colossus/ENIAC — independent inventions). The relay route is
  cheaper (540k vs 1.07M closure, D06-only) and gives a pre-electronic programmable machine; the stored-program step
  still needs radar, hence electronics.

## 12. Coupling check (craft knowledge vs organized scholarship)

Trunk and correct: fire_making, lamp, charcoal, bellows, water_mill (carries `a6_mills`), trip_hammer. Moved to the
trunk: both windmills. Subtree and causally justified: the atmospheric engine requires `barometer_vacuum`
(Torricelli–Guericke–Papin, a natural-science node), so the A8 steam line — and the A8 core — rest on scholarly physics,
not merely on the Age-based placement rule; the electricity and combustion lines require electromagnetism and
quantitative chemistry. Result: `a6_mills` is fully stage-free by either route; `a8_steam`'s stage dependence is causal.

## 13. Cross-lane requests and replies (state at writing; full text in the JSON)

- **D11:** reply to its note — OPTIONAL: `ac_transmission` is the precise "grid know-how" edge for `nuclear_power`
  (D11 keeps `electricity_generation`; both valid; D06 accepts D11's frozen-Spine position); A9 core now includes
  `petroleum_refining` (keep crude identification out of refining's prerequisites unless intended); steam_turbine
  gains thermodynamics.
- **D04:** ACCEPTED thermodynamics on diesel and steam_turbine and probability_statistics on machine_learning (both at
  integration); `semiconductor_physics` stays in D06's file (D04 endorsed it and adds no parallel node); D06 dropped its
  electrochemistry node in favour of D04's re-scoped electromagnetism.
- **D05:** ACCEPTED the computer split (with radar for the retained part) and the smartphone re-point;
  automatic_switching at integration; punched_card_tabulation not usable (implied-Eureka trap).
- **D02:** bellows date not altered (fact-check stage); superalloy on gas_turbine_power ACCEPTED; crystal step stays in
  the transistor; no mine-drainage edge on steam_atmospheric.
- **D08:** reply closed — rocket and the new submarine keep internal_combustion and inherit the fuel edge; superalloy
  on jet_airliner consistent.
- **D09:** radar gains computer as a dependant; tank's prerequisite now means the petrol engine; cannon_cast_iron loses
  steam_atmospheric.
- **D01:** mechanised_agriculture inherits petroleum_refining; refrigeration ← thermodynamics matches.
- **D07:** windmills moved on the same R15 reasoning; water_turbine available for turbine-driven mills.

`integrationRequests.overrides` (integrator format, for the coordinator-owned `data/integration-overrides.json`):
diesel and steam_turbine (+thermodynamics, prereq_complexity 1.0), machine_learning (calculus → probability_statistics),
programmable_computer (telephone → automatic_switching).

## 14. Compatibility summary (full blocks in the JSON)

- Saves: keys 288, 291, 315, 316 keep their consumers and Age facts. Holders of an old key do not receive the split-off
  key, so nodes re-pointed to new parts (diesel, hydroelectric, wind_turbine_modern, grid_storage, smart_grid, solar_pv)
  go dormant for them until it is researched — the package-wide grant-on-load seam is the OPEN DECISION. Lowered costs
  (steam_atmospheric, internal_combustion, computer, transistor) complete progress at or above the new cost at once.
- Tests: TechnologyCount +5; branch counts (trunk +2, engineering −2 +4, natural_science +1); recursive
  `finite_nodes_to_exhaust` (engineering 125 → 127 from this lane alone; frontier_climate's natural-science count +1);
  corpus-name pins for the renamed keys 288 and 291; chronology and generation tests pass (MEASURED by emulation);
  AI-empire goldens and the three CI `--check` docs move. Allocation-scenario keys and the v33 fixture key are untouched.
- Content: `infra.power_grid.requires` changes (loader rule 19 reverse index).

## 15. Open questions

1. OPEN DECISION — grant-on-load seam for the four splits (package-wide).
2. Who applies integration requests to other lanes' new ids, and re-checks their costs?
3. steam_rotary: keep at 133,810 or review its novelty against the separate condenser?
4. Age tags of steam_turbine and diesel (A9) vs haber_bosch and electricity_generation (A8): re-date (re-prices)?
5. jet_engine's factors: re-score after D-047?
6. Which milestone owns the ADR-019 §5.1 electricity capacity system (T6.11 lists tier-G entities for construction
   in M6; framework §10 puts energy systems at M11+)?
7. The Age 9 CORE closure now includes petroleum_refining (knowledge only). Acceptable, or should fuel be an
   accelerator (Eureka) instead?
8. bellows' date (earlier evidence, unsettled) — corpus fact-check stage.
9. nuclear_power's grid edge: D11's electricity_generation or D06's ac_transmission (same count, same cost)?
   (semiconductor_physics ownership is settled: it stays in D06's file, endorsed by D04.)

## 16. Verification performed (MEASURED; scratch copies only — no package file other than the two outputs was written)

- `tools/integrate.py --allow-incomplete` and `tools/validate.py --no-corpus` on (a) a copy with only D06's part and
  (b) a copy with all twelve lane parts present at writing (D01–D12) plus D06's four overrides applied: ERROR 0 in
  both, every existing node reviewed in (b); the only findings on D06 nodes are the two accepted V09 dead ends (lamp,
  fault_tolerant_quantum). No missing reference, cycle, trunk-closure, Age-monotonicity, prereq_complexity (V19),
  cost-model or university error.
- Emulated `ResearchContentTests.D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent` (same era-marker regex,
  must-hold atoms): no violation touching D06; marker-free set unchanged (25 D06 nodes before and after).
- Emulated `D047_Generations_AreLocalToTheirFamily`: no violation.
- Checked all 15 knowledge-condition Eurekas (conditions read read-only from `Sim.Data/content/research.json`) for the
  loader's implied/dead rules on both graphs: none.

## 17. Sources for RESEARCHED claims (web search summaries, accessed 2026-10-22)

- Newcomen brass cylinders; Coalbrookdale iron cylinders from the 1720s: [Thomas Newcomen](https://en.wikipedia.org/wiki/Thomas_Newcomen), [Coalbrookdale](https://en.wikipedia.org/wiki/Coalbrookdale), [Abraham Darby (Britannica)](https://www.britannica.com/biography/Abraham-Darby).
- ZBD transformer 1884–85: [Ottó Bláthy](https://en.wikipedia.org/wiki/Ott%C3%B3_Bl%C3%A1thy), [Centenary of the transformer](https://pp.bme.hu/ee/article/view/4637), [Edison Tech Center](https://edisontechcenter.org/transformers/).
- Lauffen–Frankfurt 1891 (≈175 km, 15 kV, turbine-driven generator): [IEEE/ETHW milestone](https://ethw.org/Milestones:Long_Distance_Electric_Power_Transmission_Using_Three-Phase_Alternating_Current,_1891), [Edison Tech Center](https://edisontechcenter.org/lauffen-frankfurt/).
- Niagara Adams plant 1895, Buffalo November 1896: [IEEE/ETHW milestone](https://ethw.org/Milestones:Adams_Hydroelectric_Generating_Plant,_1895), [Edison Tech Center](https://edisontechcenter.org/Niagara.htm).
- Pumped storage (Zurich 1882 / 1890s, dates vary — weak): [Hydropower in Switzerland](https://en.wikipedia.org/wiki/Hydropower_in_Switzerland); share of installed storage: [IHA, "the world's water battery"](https://www.hydropower.org/publications/the-world’s-water-battery-pumped-hydropower-storage-and-the-clean-energy-transition).
- Water turbines (Fourneyron 1827, Francis 1849, Pelton 1879): [Britannica, history of water turbines](https://britannica.com/technology/turbine/History-of-water-turbine-technology), [Water turbine](https://en.wikipedia.org/wiki/Water_turbine).
- Lenoir 1860, Otto 1876, Daimler–Maybach 1883–84, Benz 1885: [Otto engine](https://en.wikipedia.org/wiki/Otto_engine), [EBSCO research starter](https://www.ebsco.com/research-starters/history/ottos-practical-internal-combustion-engine).
- Ligroin and the Wiesloch pharmacy (1888): [Bertha Benz](https://en.wikipedia.org/wiki/Bertha_Benz), [Britannica](https://www.britannica.com/biography/Bertha-Benz).
- Diesel's Carnot-based treatise (1893): [Theory and Construction of a Rational Heat Motor](https://en.wikipedia.org/wiki/Theory_and_Construction_of_a_Rational_Heat_Motor), [Britannica](https://www.britannica.com/biography/Rudolf-Diesel).
- Colossus and Flowers (Post Office, Dollis Hill): [Tommy Flowers](https://en.wikipedia.org/wiki/Tommy_Flowers), [Codes and Ciphers](https://codesandciphers.org.uk/virtualbp/fish/colossus.htm).
- Radar-derived memories (delay lines, Williams–Kilburn tube): [Manchester Baby](https://en.wikipedia.org/wiki/Manchester_Baby), [Digital60: Williams–Kilburn tube](https://curation.cs.manchester.ac.uk/digital60/www.digital60.org/birth/how/kilburntube/history/index.html).
- Wilson 1931, Ohl 1940 (p–n junction and photovoltaic effect), Pickard 1906: [History of semiconductors (Cornell)](https://djena.engineering.cornell.edu/hws/history_of_semiconductors.pdf), [Encyclopedia.com](https://encyclopedia.com/science/science-magazines/physics-semiconductors), [Riordan & Hoddeson excerpt](https://eva.fing.edu.uy/mod/resource/view.php?id=31737).
- Gedser turbine 1957 (induction generator on the AC grid): [Gedser wind turbine](https://en.wikipedia.org/wiki/Gedser_wind_turbine).
- Bellows evidence (unsettled): [Davey 1979](https://keibi.uni-tuebingen.de/Record/KEI00094363), [Davey 2021 review](https://findanexpert.unimelb.edu.au/scholarlywork/1499154-ancient-pot-bellows--a-review-forty-years-on), [Rose et al., Abu Matar](https://cris.bgu.ac.il/en/publications/bellows-and-furnace-covers-in-the-unalloyed-copper-metallurgy-of--2/).
