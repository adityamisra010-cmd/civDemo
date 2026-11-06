# H-E — Phase C lane E: per-pulse war economics, the insolvency cascade, movement logistics

Date: 2026-11-05, resumed 2026-11-06 (§12). Clone `/dev/shm/m7c/E`, branch `lane/E`, base `fc5230c` (D-080), merged with
dev `444b85e` on 2026-11-06. ISOLATED PROTOTYPE: only `prototypes/m7-sprint1` changed. No file under `Sim.*` changed
(D-079 Q5 is open). No permanent project was created.

**Resumed session (2026-11-06), summary — details and measurements in §12:** the clone was rebuilt from
`scratchpad/m7c/bundles/E.bundle` (head `6dda903`; the 12:06 snapshot `wip/m7c-E.patch` is byte-identical to
`git diff fc5230c..6dda903`, so nothing was missing from the bundle); dev `444b85e` merged with no conflicts; the work
lost in the container restarts was redone: ships' companies on lane N's vessel model (sea ration, rowing exertion,
stowage, seamen's pay, bunker coal, naval powder, scurvy), occupation trade loss, contributions levied whatever the grain
want; plus one merge-integration fix (lane TR's new terrain classes were not covered by the logistics tables). 17 new
tests, 18 mutants (17 killed, 1 survivor recorded), a small bench.

Authority: DIR5 §2.4 ("Economic effects occur per operational pulse"), §2.11 (logistics, the automatic food priority,
"Audit existing economy … Do not invent arbitrary costs or duplicate existing resource models"); D-080 **WA4** — which
economic systems settle per War Pulse and what happens when a polity cannot pay are DELEGATED with the criterion "Resolve
this as realistically as possible like how it would unfold in the real world"; ADR-061 §3.11 ([DELEG pending] which
systems settle per pulse); D-079 DR26–DR28, Q3 (food ordering OPEN). Laws 1–5 bind.

**Everything in this report that chooses a mechanism, a number or a priority is a PROPOSAL under WA4's delegation or a
labelled calibration, not a decision.** The food-allocation interpretation (DR27, Q3) is NOT chosen: both orderings are
implemented with no default and measured side by side (§5).

## 1. What was built

All code is in NEW files; the only edit to a shared file is six enum members appended to `Commodity` in
`Proto.M7/War/WarLedger.cs` (lane T3 does not touch that file). `WarTheaterWorld.cs`, `Runner.cs` and
`EventScheduler.cs` are unchanged: the economy layer composes T2's world instead of editing it.
**(Superseded, as of fix round 1: the shared-file edits are now `War/WarLedger.cs` — seven `Commodity` members with
`Coal` of §12 — `Proto.M7.Bench/Program.cs` — one dispatch line, §12 — and `War/WarTheaterWorld.cs` — one read-only
`IsParticipant(warId, polity)` query, §13. The lane's tests number 35 + 17 (§12) + 17 (§13) = 69; see §11–§13.)**

| File | Role |
|---|---|
| `Proto.M7/WarEcon/WarCapability.cs` | `WarTech` flags (Coinage, Banking, Gunpowder, Magazines, Railways, MotorTransport, Sanitation) and `PolityCapability`, derived from the polity's known technologies (stand-in for the research table). No constructor from a date (Law 4). |
| `Proto.M7/WarEcon/WarEconTables.cs` | `LogisticsRates` (every per-sim-year rate, integer units, `Proposal()` = the labelled calibration of §3), `TerrainLogistics` (march cost reusing the Slice C terrain table, grazing ‰, wear factor), `RoadClass`, `CarriageMode`. |
| `Proto.M7/WarEcon/ArmyLogistics.cs` | `ArmyComposition` (+ capability validation), `Route`/`RouteSegment`, march planning (terrain, road, rail, slowest element), the supply line (carriage loss by mode × distance × ground, magazines, infrastructure and fleet throughput, motor fuel), exact gross-up / delivery. |
| `Proto.M7/WarEcon/WarFoodAllocator.cs` | The automatic food priority at a pulse start (DIR5 tiers) under a REQUIRED `FoodOrdering` (no default), (Priority, ArmyId) composite order, exact conservation, decision record. |
| `Proto.M7/WarEcon/WarEconomyWorld.cs` | `IEventWorld` that wraps T2's `WarTheaterWorld`: plans each army's pulse at `PulseStart`, settles every sub-step T2 produces (including the straddle split at a turn start), applies discrete condition updates at `PulseEnd`, and runs the campaign settlement at `TurnStart`. Records per army per pulse (`ArmyPulseRow`), allocations, campaign settlements and cascade events; state hash and army-side hash. |
| `Proto.M7.Tests/WarEconE/*.cs` | 35 tests on 2026-11-05: allocator 6, logistics 9, world 18, scenarios and calibration 2 (plus the rig `WarEconRig.cs`); 69 after §12 (+17 naval) and §13 (+17 fix round 1). |
| `reports/H-E.md` | This report. |

Event flow at one instant X (T2's order is unchanged): `PulseEnd(w)` → this layer settles w's last sub-step, then
deserters arrive home, morale/cohesion/mutiny/disbandment update → `TurnStart` → T2 splits every straddling pulse; this
layer settles those sub-steps, then the campaign settlement → `PulseStart(w)` → this layer plans pulse n+1.

## 2. The per-pulse settlement set (WA4 — PROPOSAL)

"Plan" = computed and reserved at the pulse start from state at that instant; "flow" = settled at every sub-step as the
exact pro-rata share of the pulse total (`RateIntegrator.IntegrateExact(total, dt, W)`, carries reset per pulse) or, for
pay, the exact per-year integration with a persistent carry. "Campaign" = settled at turn starts, as the turn-atomic
campaign economy does today.

| # | Item | When | Mechanism (all transfers through the ledger) | Real-world rationale | Source status |
|---|---|---|---|---|---|
| 1 | Rations, by composition | plan + flow | Base ration (the shipped upkeep, 1 grain unit per person-year) + field extra + march extra while marching; men by arm share rates | Soldiers on campaign eat more than civilians; march exertion | Engels' 3 lb (1.36 kg) grain per soldier-day, via secondary sources; **contested** (O'Connor, *Chiron* 2013) |
| 2 | Fodder | plan + flow | Horses and draft animals: hard feed (more when working) + roughage; roughage met by grazing ‰ of the ground, else as grain; fodder shortfall raises animal deaths | Horses need ~4.5 kg grain + ~5 kg hay a day; grazing depends on ground and season | Matthews (Granicus) via search result; season not modelled |
| 3 | Pay (coin) / wages (in kind) | plan + flow | Coin only with Coinage: the chest is filled at pulse start from the treasury (arrears first, then the pulse's due); due accrues per sub-step; what the chest cannot pay becomes arrears. Without Coinage: wages in kind (grain) through the food allocator, lowest tier | Monthly musters, chronic arrears in early-modern armies; pre-coinage ration-list economies paid in kind | knowledge, unverified |
| 4 | Munitions | plan + flow | Gunpowder only: drill per year + per combat day (firearm men, guns); supplied from the polity's stock to the ammunition train; shortfall lowers the supply factor for firearm units | Powder and shot by battle intensity | unverified orders of magnitude |
| 5 | Equipment wear | plan + flow | Wear per man-year, per man-1000 km × ground factor, per combat day; replaced from stock; unreplaced deficit raises disease (exposure) and lowers the supply factor | Shoes and clothing wore out on the march | unverified |
| 6 | Carriage | plan | Supply line from depot (position 0) to the army: animal carriage loses distance/radius × ground cost (pack 150 km, wagons 600 km), magazines ×0.6, water 6000 km, rail 40 000 km; motor loses no cargo but burns fuel (round trip) | Pack animals eat their load (Engels' radius); water ≫ land | unverified (Diocletian's edict ratios are not used as numbers) |
| 7 | Supply-line throughput | plan | Least of each crossed segment's capacity (sim.json `roads.classes` capacityTonnesPerYear CITED: DirtPath 100 000 t, Track 150 000 t, Built 300 000 t) and the army's own train: carriers × payload × round trips in the pulse; motor capped by fuel in stock | Armies far from magazines could not be fed by their wagons | sim.json CITED; fleet numbers PROPOSAL |
| 8 | Foraging / requisition | plan | Own locality (loyal army): only the surplus above what the next settlement will charge the locals (§13 E-V2). Enemy locality (owner a participant of the army's war, §13 E-V6): stock draws at nominal per-year rates applied once per elapsed day (§13 E-V1) — foraging 25,335 ‰/yr of its grain (40 % in 7 days), contributions 5,378 ‰/yr of its money into the pay chest (10 % in 7 days) — of its grain into the wage chest for a polity without coinage (§14 E2-7). When hungry after that: forced requisition 100,910 ‰/yr (90 % in 7 days) with a famine-risk event. Neutral locality: nothing | Armies lived off the land abroad; "war feeds itself"; magazines at home | unverified |
| 9 | Loot / plunder | plan | Mutinous army only: plunders the locality it stands in, whoever owns it (grain it needs and can carry; money at the forced rate of row 8 — 90 % in 7 days — which the soldiers keep — it reduces their arrears); own-side plunder is tallied as grain and as money (unrest inputs) if the victim is its own polity | Sack of Antwerp 1576 by unpaid Spanish mutineers in a city of the same crown | Wikipedia "Sack of Antwerp"; Parker 1973 (not read) |
| 10 | War taxation / levies | flow (latched) | Revenue rate = civilians × (base tax + war tax) + trade × (1 − blockade), accrued as a pure inflow at every polity touch | Extraordinary war taxes | — |
| 11 | Labour withdrawn | flow (latched) | Harvest rate = (civ + mob)·h − mob·1.6 grain/yr: a mobilised man removes 1.6 grain-years of harvest | **CITED (measured)**: F-economy recruitment twins, 1.3–1.9 grain per soldier-year, midpoint taken | measured |
| 12 | Trade disruption | flow (latched) | Blockade ‰ of trade income lost (input from the naval/occupation lanes) | Blockades cut customs and trade revenue | — |
| 13 | War borrowing | campaign | Banking only: at a settlement, borrow up to the gap between cash and the next turn's pay plus arrears, within a credit limit (3 × annual revenue) and the lenders' capital; interest accrues per sim-year; unpaid interest at a settlement = default: credit closes | Spanish defaults (1575) stopped the transfers that paid the Army of Flanders → mutinies | Wikipedia; Parker (not read) |
| 14 | Desertion | flow (latched rate, compounded per day on the declining strength, §14 E2-1) | ‰/yr from base + low morale + arrears duration (capped) + hunger + mutiny; deserters go to an in-transit account and reach the owner's civilians at the pulse end | Unpaid, hungry armies melted away | unverified |
| 15 | Disease and starvation | flow (latched) | base ‰/yr × (1 + 4·hunger + 1·equipment deficit), × 0.3 with Sanitation, plus starvation 3000 ‰/yr × hunger (not reduced by Sanitation); men → dead | Disease killed more soldiers than battle before modern medicine; men without food die within weeks | unverified |
| 16 | Civilian consumption, growth, famine, interest, borrowing | campaign | At turn starts (turn-atomic campaign economy); the pulse allocator protects the civilians' need until the next settlement | — | — |
| 17 | Ships' companies: sea ration (2026-11-06) | plan + flow | Company = hulls × lane N `VesselClass.Crew`; the sea ration (base + 800 gm/yr) replaces the field extra for everyone aboard | The sea ration was heavier than a soldier's (biscuit, salt meat, pease, beer) | knowledge, unverified |
| 18 | Rowing exertion (2026-11-06) | plan + flow | Oared hulls only: rowers (850 ‰ of the crew) eat +800 gm/yr while under way; sail and steam have no exertion term | A trireme had about 170 oarsmen of 200 aboard; rowing is heavy labour | knowledge, unverified (Morrison & Coates, not read) |
| 19 | Seamen's pay (2026-11-06) | plan + flow | 12 soldier-months per seaman-year in coin, 500 gm in kind without coinage; the same chest, arrears and cascade as the army | Seamen's wages were of the order of a soldier's (unverified) | knowledge, unverified |
| 20 | Bunker coal (2026-11-06) | plan + flow | Steam hulls only (SteamPower): 60 t per hull per 1000 km steamed, from the polity's coal stock to the bunker; without coal the squadron lies at anchor (movement restricted, never a starvation) | Coaling stations and colliers bound steam navies | unverified order of magnitude |
| 21 | Naval powder (2026-11-06) | plan + flow | Gun-armed hulls (every lane N naval weapon except deck archers) × 5 t per day of action, Gunpowder only | Powder and shot by action | unverified |
| 22 | Scurvy (2026-11-06) | plan (latched) + flow | Deaths begin 56 days after the last fresh food, then +400 ‰/yr per 30 days, capped at 4000 ‰/yr; a port call that yields food restarts the clock; Antiscorbutics × 50 ‰; Sanitation does not apply | Scurvy killed more seamen than battle on long voyages (Anson 1740–44); citrus issue (RN 1795, after Lind 1753) nearly ended it | knowledge, unverified |
| 23 | Occupation and blockade trade loss (2026-11-06) | latched at pulse starts / disbandment | A force standing in an ENEMY locality (its owner a participant of the force's war; a neutral foreign town is not occupied, §13 E-V6, corrected §14 E2-8) cuts the owner's trade income by that locality's `TradeSharePermille` (each locality once), plus the blockade input, capped at the whole trade income; the victim is accrued at the old rate up to the instant first | Occupied ports and market towns stop paying customs and trade revenue | knowledge, unverified |
| 8a | Contributions (changed 2026-11-06) | plan | Levied on an ENEMY locality (owner a participant of the army's war; neutral towns give nothing, §13 E-V6, corrected §14 E2-8) at every pulse start whatever the army's grain want (before: only when it wanted grain): in money into the pay chest where the polity has coinage, in kind (grain) into the wage chest otherwise (§14 E2-7, PROPOSAL) | Contribution systems (Wallenstein; French revolutionary armies) levied money independently of the army's food | knowledge, unverified |

What is NOT settled per pulse, deliberately: civilian consumption and growth (campaign economy, turn-atomic today, and
nonlinear in Sim.Core — Slice B:127), interest and borrowing (fiscal decisions keyed to the campaign settlement).

## 3. Calibration table (PROPOSAL; model outputs measured by `CalibrationTable_PerDayConsumption_ByComposition`)

Grain unit: the shipped unit (D-015, sim.json `military.upkeepGrainPerPersonYear` 1.0) is one adult-year of food. Kept
in milli-units (gm). **PROPOSED mapping: 1 gm = 0.252 kg** (0.7 kg of grain a day × 360 days), so the shipped civilian
ration is 0.700 kg/day.

Model values (measured output of the calibration test):

| quantity | model | unit |
|---|---|---|
| civilian subsistence (base ration) | 0.700 | kg grain-eq / person-day |
| soldier in camp (base + field) | 0.910 | kg / man-day |
| soldier marching | 1.330 | kg / man-day |
| horse at work, hard feed | 4.480 | kg grain / horse-day |
| horse roughage, as grain if not grazed | 2.450 | kg grain-eq / horse-day |
| draft animal at work, hard feed | 1.750 | kg / animal-day |
| pay, infantry / cavalry / gunner | 12 / 30 / 15 | soldier-months per man-year |
| truck fuel | 0.400 | L / truck-km |
| foot march (open, sustained) | 20 | km / day |

| force | men | food+fodder marching, t/day | of which fodder, t/day | march km/7 d (open, no road) | (track) |
|---|---|---|---|---|---|
| Bronze/Iron host (AncientHost) | 4200 | 12.558 | 6 | 105 | 123 |
| Early-modern army (EarlyModernArmy) | 5800 | 15.715 | 8 | 105 | 123 |
| Motorised force, 20,000 men, 1,000 trucks | 20000 | 26.600 | 0 | 350 | 1235 |

| carriage mode, 100 km of open ground | share of cargo lost |
|---|---|
| pack animals, no road | 666‰ |
| ox wagons, track | 166‰ |
| river boats | 16‰ |
| railway (Railways held) | 2500 ppm |

Reference orders of magnitude (all PROPOSAL anchors; "unverified" = general knowledge not checked this session):

| Quantity | Reference | Status |
|---|---|---|
| Soldier grain ration | ~1.36 kg (3 lb) per soldier-day (Engels' estimate for Alexander's army, built on a 3,600 kcal modern RDA) | secondary sources found by search; contested by O'Connor (*Chiron* 2013) |
| Horse feed | ~4.5 kg grain + ~5 kg hay/straw per horse-day (Matthews, Granicus) | secondary source found by search |
| Civilian subsistence | ~0.7 kg grain-equivalent a day (2,000–2,500 kcal) | unverified; it is only the PROPOSED kg mapping of the shipped unit |
| Labour withdrawn | 1.3–1.9 grain units of harvest per soldier-year | **measured** (F-economy recruitment twins); midpoint 1.6 used |
| Road throughput | DirtPath 100,000 t/yr, Track 150,000, Built 300,000 | **CITED** sim.json `roads.classes` |
| Road speed | speedFactor 1.0 / 0.85 / 0.7 | **CITED** sim.json |
| Foot march | ~20 km/day sustained; Vegetius' 20 Roman miles a march day is higher | unverified |
| Truck fuel | ~0.4 L/km for a 2.5–3 t truck | unverified |
| Pack-animal radius | an animal eating ~1/10 of its load a day exhausts it in ~10 days | unverified (Engels' argument) |

Reading the table (inferred, not measured against data): the model's marching soldier (1.33 kg/day) and working horse
(4.48 kg hard feed + roughage) sit at the secondary-source orders of magnitude (1.36 kg; ~4.5 kg + ~5 kg hay); a force's
food and fodder come to 2.7–3.0 kg per man-day including animals for the animal-drawn forces (1.3 kg for the motorised one, which burns fuel instead). Pack animals off-road lose two thirds of a cargo over
100 km, wagons on a track one sixth, boats under 2 %, rail 0.25 % — the ordering the historical record shows (water and
rail ≫ road ≫ pack); the magnitudes are PROPOSAL.

## 4. The insolvency cascade (mechanisms, measured pulse by pulse)

Cascade as mechanisms (no free-floating modifier): arrears accumulate where the chest cannot pay → morale and cohesion
fall with the arrears' DURATION (penalty ∝ months unpaid, per sim-year × pulse length) → the latched desertion rate rises
with low morale, arrears duration and hunger → cohesion below 250/1000 = mutiny: the army refuses to march and plunders
where it stands → a mutiny ends when the back pay is settled (arrears ≤ one month of pay) and the army is fed; the arrears
clock restarts → disbandment when strength falls to 10 % or cohesion stays at 0 for 7 days while mutinous (everything
returns to the polity through the ledger; fix round 1 — it was "2 pulses", §13 E-V1).

**Note (fix round 1):** the pulse-by-pulse figures of §4.1–§4.4 were measured on the tree before fix round 1. The fixes
of §13 change the stock draws (per-year rates applied per day, calibrated to the same 7-day shares), the locals' reserve
at home, the dating of arrears and disbandment timing, so individual numbers below are not current; every scenario test
named here still passes with its assertions unchanged (measured, §13.4). Ration shortfall raises disease deaths, desertion and lowers the supply factor
(`ArmyPulseRow.SupplyQ32Raw`, a Q32 in the form the tactical layer's `ConditionMath` supply input takes). The supply factor
is the minimum of fed, munitioned and equipped. **It is an output only (corrected §14 E2-10):** nothing in the prototype
passes it to a battle — there is no seam from this world to `TacticalBattle`/`AutoResolver`; the hand-off contract is
pinned by `SupplyFactor_HandOff_LowersConditionMathAttack`. **Fix round 2 (§14)** also changes the head-loss integration
(daily compounding), so desertion and death counts below are not current either.

### 4.1 Fiscal cascade to disbandment (`InsolvencyCascade_ArrearsMoraleDesertionMutinyPlunder_InThatOrder`, `…EndsInDisbandment…`)

Early-modern army (5,000 musketeers, 600 horse, 200 gunners, 1,300 animals, 150 wagons), coin pay 81,000 soldier-months a
year, halted 10 km across the border (70 km from its depot, inside its wagons' range), treasury 12,000 soldier-months, no
revenue; 7-day pulses (Early Modern band, T2's PROPOSED length). Food is ample (hunger 0 throughout), so the cascade is
purely fiscal. Selected rows (measured; day = days since the war began; morale and cohesion on 0..1000):

| pulse | day | cond | men | forage | delivered | pay due/paid | arrears | morale | cohesion | deserted | died | desertion ‰/yr |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0 | Loyal | 5800→5780 | 517,155 | 0 | 1575/1575 | 0 | 811 | 907 | 14 | 6 | 130 |
| 8 | 56 | Loyal | 5655→5639 | 251,267 | 0 | 1536/1380 | 156 | 893 | 962 | 10 | 6 | 92 |
| 9 | 63 | Loyal | 5639→5623 | 250,864 | 0 | 1531/0 | 1571 | 893 | 962 | 10 | 6 | 92 |
| 21 | 147 | Loyal | 5411→5386 | 191,969 | 53,066 | 1469/0 | 18,821 | 875 | 935 | 19 | 6 | 188 |
| 26 | 182 | Loyal | 5286→5257 | 14,928 | 226,913 | 1435/0 | 25,945 | 857 | 907 | 23 | 6 | 225 |
| 40 | 280 | Loyal | 4835→4798 | 12 | 230,295 | 1313/0 | 45,028 | 767 | 773 | 32 | 5 | 349 |
| 50 | 350 | Loyal | 4435→4392 | 0 | 220,129 | 1204/0 | 57,550 | 671 | 629 | 38 | 5 | 447 |
| 60 | 420 | Loyal | 3980→3931 | 0 | 208,584 | 1081/0 | 68,913 | 548 | 444 | 45 | 4 | 585 |
| 69 | 483 | Loyal→**Mutinous** | 3534→3483 | 0 | 197,254 | 960/0 | 78,040 | 412 | 241 | 47 | 4 | 698 |
| 70 | 490 | Mutinous | 3483→3412 | 0 (+1 plundered) | 195,975 | 946/0 | 78,978 | 396 | 216 | 67 | 4 | 1004 |
| 79 | 553 | Mutinous | 2866→2801 | 0 | 180,213 | 778/0 | 86,643 | 237 | 0 | 62 | 3 | 1127 |

Events: day 63 ArrearsBegan → day 490 MutinyBegan → plunder of the (already foraged-out) foreign town → day 567
**Disbanded** (2,737 men walk home; every store returns to the polity through the ledger). The army first strips the foreign
district by foraging (pulses 0–25), then lives on its supply line. Its pay stopped at day 56; it held together for 14 months
unpaid, losing 39 % of its strength to desertion and disease before it mutinied.

### 4.2 A mutiny settled by plundering its own town (`Mutiny_EndsWhenArrearsAreCleared`)

Same army, unpaid, garrisoned at home in its own town (which holds 5,000,000 soldier-months of private money). A loyal army
takes no contributions from its own people, so arrears grow; at pulse 68 it mutinies; at pulse 69 it sacks the town — its
grain and 90 % of its money; the soldiers pay themselves, the arrears fall from 79,489 to 941 (≤ one month's pay) and the
mutiny ends; unrest rises at home. The arrears clock restarts; with the treasury still empty the cycle repeats: second mutiny
pulse 107 (arrears 31,489), settled again at pulse 108 (655). Recurring mutinies in a chronically unpaid army are the Army
of Flanders pattern (Parker 1973, not read; the Antwerp sack of 1576 fell on a city of the same crown).

| pulse | day | cond | men | forage+seized | arrears | morale | cohesion | desertion ‰/yr |
|---|---|---|---|---|---|---|---|---|
| 67 | 469 | Loyal | 3570→3518 | 116,666+0 | 78,533 | 417 | 257 | 696 |
| 68 | 476 | Loyal→**Mutinous** | 3518→3466 | 116,667+0 | 79,489 | 401 | 233 | 703 |
| 69 | 483 | Mutinous→**Loyal** | 3466→3423 | 0+174,341 | 941 | 384 | 500 | 589 |
| 70 | 490 | Loyal | 3423→3401 | 58,992+0 | 1,871 | 384 | 500 | 296 |

### 4.3 The starvation path: a Bronze/Iron-age host beyond pack range (`AncientDeepRaid_StarvationCascade_PulseByPulse`)

4,000 foot, 200 mounted, 1,200 animals, 60 wagons, no coin (wages in kind), marching from 70 km into foreign hills and
mountains without a road; 14-day pulses (Bronze/Iron band, PROPOSED). After pulse 0 it is 191 km out: 80 km of wagon path
(13 % loss) plus 111 km of pack carriage over hills (133 %) — the whole cargo would be eaten on the way, so nothing more
arrives, wages in kind stop and arrears begin. The host strips the first foreign district, then seizes by force
(famine-risk events for the locals), then starves. Hungry, it does not march (the "km" column is distance marched in the
pulse).

| pulse | day | men | need S/M | forage+seized | delivered | hunger ppm | km | arrears (gm) | deserted | died | des ‰/yr | dis ‰/yr | supply |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0 | 4200→4169 | 347,666/182,000 | 0+0 | 382,483 | 0 | 121 | 0 | 21 | 10 | 130 | 64 | 924‰ |
| 1 | 14 | 4169→4139 | 387,983/181,218 | 568,857+0 | 0 | 0 | 73 | 86,468 | 19 | 11 | 120 | 68 | 863‰ |
| 2 | 28 | 4139→4090 | 459,713/180,460 | 120,000+162,000 | 0 | 26,866 | 0 | 172,314 | 25 | 24 | 159 | 153 | 853‰ |
| 3 | 42 | 4090→3336 | 456,609/179,107 | 7,200+9,720 | 0 | 962,944 | 0 | 257,143 | 248 | 506 | 1564 | 3186 | 37‰ |
| 6 | 84 | 2164→1735 | 317,667/120,248 | 1+2 | 0 | 999,990 | 0 | 427,010 | 150 | 279 | 1792 | 3316 | 0‰ |
| 13 | 182 | 436→345 | 158,265/55,813 | 0+0 | 0 | 1,000,000 | 0 | 568,026 | 35 | 56 | 2100 | 3360 | 0‰ |

Day 196: strength under 10 % → **Disbanded** (345 men). The supply factor handed to the battle layer falls from 924‰ to 0.

### 4.4 War borrowing and default (`WarBorrowing_ThenDefault_ClosesCredit_AndArrearsFollow`)

Banking polity, Industrial band (1-year turns, 3-day pulses), revenue ~11,800 soldier-months a year against 81,000 of pay:

| turn | borrowed | interest due / paid | debt | credit |
|---|---|---|---|---|
| 1152 | 35,319 | 0 / 0 | 35,319 | open |
| 1153 | 180 | 2,825 / 2,825 | 35,499 | open |
| 1154 | 0 | 2,840 / 66 | 38,273 | **closed (default)** |
| 1155 | 0 | 3,062 / 3,062 | 38,273 | closed |

After the default no more money is lent; the unpaid army slides into arrears (asserted).

### 4.5 Note on supply range (measured in a draft run, formula unchanged since)

At 180 km an ox-wagon train of 150 wagons (plus 300 spare draft animals as pack animals) makes 0.29 round trips in a 7-day
pulse: 118,036 gm shipped, 27.6 % eaten on the way, 85,458 delivered against ~250,000 needed — the army starves unless it
forages. This is why the fiscal scenarios station the army 70 km out. It is the model's version of the historical rule that
armies stayed within a few marches of magazines or navigable water.

## 5. The automatic food priority and the two orderings (DIR5 §2.11; Q3 evidence, NO choice)

At each pulse start, per polity, the requests of its armies in that war (depot terms, after carriage gross-up and
throughput) meet its granary: civilians' reserve first (their need until the next settlement net of the harvest still to
come), then:

- **CivilianMilitaryGrowth**: all standing rations, then all movement, then growth keeps up to its demand, then restock and
  wages in kind. Exactly DIR5's tiers: growth reduces first; movement is restricted only when growth already gets
  nothing; standing is cut only when movement gets nothing. Pinned over a full sweep of the granary
  (`CivilianMilitaryGrowth_CutOrderIsGrowthThenMovementThenStanding_OverAFullSweep`, exact thresholds) and in the world
  (`World_TierOrder…`).
- **CivilianGrowthMilitary**: growth is protected before any military request. Measured consequence: over the band
  civ + growth + standing ≤ granary < civ + growth + standing + movement, **movement is cut while growth keeps its whole
  demand** — DIR5 tier 1 does not hold for the military draw under this interpretation. In the world run (granary =
  reserve + growth + 8 M gm, no harvest) the military is first cut at pulse 21 under this ordering, where the other
  ordering instead reduced growth at pulse 21 and first cut the military at pulse 79. This is evidence for Q3, not a
  recommendation of either. (In that run the CivilianMilitaryGrowth sequence went from GrowthReduced straight to
  StandingShort at pulse 79: one pulse's draw is larger than the narrow band in which only movement is short. Every
  allocation still obeyed the tiers — the test checks each one — and the sweep test pins the MovementRestricted band
  exactly.)

At the army, the same order holds: an army whose standing need is not covered does not march at all (its movement food
feeds it instead); pinned by `Army_HungerImpliesNoMovement…` and mutant M2.

Restock and in-kind wages come after growth under both orderings (PROPOSAL: they are not food eaten this pulse).

At home a LOYAL army never takes its own civilians' reserve (`LoyalArmyAtHome_NeverSeizesTheLocalCiviliansReserve`): it
requisitions only the surplus, and if that is not enough it goes hungry — starvation falls on the army last. Only a
MUTINOUS army plunders its own side (PROPOSAL: discipline has collapsed, so this is not the polity's automatic
allocation; §9 Q-E2).

## 6. Movement: composition, distance, terrain, roads, technology

- Speed of the slowest element: ox train 15 km/day, foot 20, mounted 30, motorised 150 (all men in trucks, no animals),
  rail 400 on rail segments with Railways. Ground cost from the Slice C table (Road 700 CITED; Open 1000; Forest 1500;
  Hills 1800; Jungle 2500; Mountain 4000 PLACEHOLDER); a road removes part of the ground's excess (path 20 %, track 50 %,
  built 75 %, PROPOSAL) and applies its sim.json speedFactor (CITED). Pinned: built > track > none, open > forest > hills
  > jungle > mountain, and any road beats the same ground without (`TerrainAndRoads_OrderTheMarchExactlyAsDirected`).
- Consumption: standing need over the pulse length; movement need over the marching time only; fuel = trucks × km × 0.4
  L/km; equipment wear by km × ground factor. "Ancient infantry must not consume oil": trucks, firearms, guns and gun
  crews are refused without their technology, and a no-technology war leaves the fuel and munitions totals at exactly 0
  (`AncientArmy_ConsumesNoOilAndNoMunitions…`).
- Law 4: the same capability at two instants in different era bands (pulse length held equal) gives identical rows
  (`Capability_IsStateDerived_NotDateDerived`); rail is a capability (`Rail_OnlyWithTheRailwaysCapability`, mutant M11).
- Law 3: the standing need summed over 84 days is floor(rate × 84 d / Y) with 7-day and with 3-day pulses
  (`StandingNeed_IsDtCorrect_IndependentOfPulseLength`, independent BigInteger closed form).

## 7. Conservation, the straddle, crossing invariance

- Eight commodities (money, goods, people, grain, animals, munitions, kit, fuel); every change is a `WarLedger.Transfer`;
  sources and sinks are accounts. Totals are equal by exact `Int128` comparison at the boundary instant of a straddling
  pulse, ±1 ms, mid-pulse and at the end (`Conservation_ExactAtEveryInstant_AcrossAStraddlingPulse`), and at the end of
  every cascade scenario.
- The straddling pulse is settled in two parts; the part before the turn start is exactly floor(total × 3 d / 7 d)
  (`StraddlingPulse_PartBeforeTheBoundary_IsTheExactProRataShare`, mutant M3).
- Crossing invariance of the war side: shipped 720-day turns vs one-year turns, 150 pulses with the treasury running dry,
  → identical army-side hash (stores, condition, rows, events) although the splits differ
  (`CrossingInvariance_ArmySideIndependentOfTurnBoundaries`, mutant M10). Why it holds: every state-dependent rate is
  latched at the pulse start; discrete updates happen at pulse ends; every draw that competes for a stock is reserved at
  the pulse start; polity inflows (harvest, revenue) are pure inflows at rates latched only at pulse starts and
  settlements; deserters reach home at the pulse end.
- What is turn-keyed BY DESIGN (and so excluded from the invariance check): the campaign settlement (civilian
  consumption, famine, growth, interest, borrowing, theatre localities' harvest). With a binding granary, foraging stocks
  or borrowing, results legitimately depend on when the campaign settles. The test therefore runs without localities and
  with an ample granary.

## 8. Tests, build, scan (measured in this clone)

| Configuration | Before (base `fc5230c`, measured this session) | After (this commit) |
|---|---|---|
| Debug build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| Debug tests (`M7Proto.slnx`) | **369** passed, 0 failed (1 m 49 s) | **404** passed, 0 failed (4 m 13 s) |
| Release build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| Release tests | **369** passed, 0 failed (53 s) | **404** passed, 0 failed (1 m 45 s) |
| Determinism scan (`scripts/check-proto-determinism.sh`) | — | CLEAN: 52 files under `Proto.M7/`, 0 pattern groups with findings |

- +35 tests, all in `Proto.M7.Tests/WarEconE/`. All 369 pre-existing tests pass unchanged (the T2 golden
  `T2Golden_TraceAndStateHashes` included): **moved goldens: none.** Appending `Commodity` members does not change any
  existing account's byte, so T2's ledger hashes are unchanged.
- Durations are wall clock on four CPUs shared with about nine agents; they vary by a factor of 2–3 between runs.
- **Tie-dense tests:** `TieDense_EqualPrioritiesAndRequests_ServedByIdRegardlessOfInputOrder` (allocator: 8 armies, equal
  priority and requests, 12 permutations → identical grants, ascending id) and
  `TieDense_EqualPriorityArmies_ServedByIdRegardlessOfDeclarationOrder` (world: 3 armies sharing a locality and a granary,
  4 declaration orders → identical state hash; lower id forages first).
- **Order-delivery pins:** no order kind was added, so no new delivery semantic exists. **Populated round trips:** no
  row type is serialized (see §10).

### Mutants (bounded at 10 × the clean filtered run: 12.8 s measured → 128 s per test run; none hung)

Each mutant was applied to the clone, the test project rebuilt, the lane-E suite run, and the file restored and checked by
SHA-256. Run on the final code (after the starvation-mortality change); 11 of 11 killed by a semantic assertion.

| Mutant | Change | Killed by (semantic) |
|---|---|---|
| M1 | Growth served before movement under CivilianMilitaryGrowth | `CivilianMilitaryGrowth_CutOrder…OverAFullSweep`, `CivilianGrowthMilitary_Protects…`, `World_TierOrder…` |
| M2 | At the army, movement served before standing rations (march while starving) | `Army_HungerImpliesNoMovement…` (also 3 more) |
| M3 | A pulse's food consumed at its end instead of pro rata per sub-step | `StraddlingPulse_PartBeforeTheBoundary_IsTheExactProRataShare` |
| M4 | Deserters go to the dead instead of home | `Deserters_ReturnToPopulation_Exactly` |
| M5 | Labour withdrawal dropped from the harvest | `LabourWithdrawn_HarvestIsExactlyTheClosedForm` |
| M6 | Pack carriage loss ignores the ground | `SupplyLine_LossFractions_AreExact` |
| M7 | A loyal army seizes its own civilians' food | `LoyalArmyAtHome_NeverSeizesTheLocalCiviliansReserve` |
| M8 | Arrears duration does not erode cohesion | `InsolvencyCascade_…InThatOrder`, `…EndsInDisbandment…`, `Mutiny_EndsWhenArrearsAreCleared` |
| M9 | A disbanding army is not disbanded | `InsolvencyCascade_EndsInDisbandment…` |
| M10 | Pay due charged per full pulse at every sub-step | `CrossingInvariance_ArmySideIndependentOfTurnBoundaries` (metamorphic, not a golden) |
| M11 | Rail used without the Railways capability | `Rail_OnlyWithTheRailwaysCapability` |

Each property is one the system ought to have (ADR-015 §7.2): DIR5's tiers (M1, M2, M7), sub-stepping (M3), Ledger
conservation of people (M4), labour withdrawn (M5), "jungle and mountains increase costs" (M6), the insolvency cascade
(M8, M9), dt-correct pay across a turn boundary (M10), no calendar gates (M11). The two tests added after this mutant run
(`Capability_DerivedFromKnownTechnologies`, `Sanitation_ScalesDiseaseMortality`) were not mutated.

## 9. Proposals and open questions for the Director (nothing here is decided)

- **Q-E1 (WA4, PROPOSAL):** the settlement set and timing of §2 — per pulse: rations, fodder, pay, munitions, wear,
  carriage, throughput, foraging, contributions, plunder, desertion, disease, war-tax and trade inflows, labour withdrawn;
  per campaign settlement: civilian consumption, famine, growth, interest, borrowing.
- **Q-E2 (PROPOSAL):** only a mutinous army takes its own civilians' food; a loyal army requisitions only the surplus and
  goes hungry rather than starving its own people. Alternative: a loyal commander may also seize the reserve as the last
  resort (more historical in some periods; it would make DIR5's "starvation is the last resort" fall on civilians).
- **Q-E3 (PROPOSAL):** restock and in-kind wages are served after growth under both orderings.
- **Q-E4:** the calibration of §3 and the cascade constants (morale/cohesion penalties, mutiny threshold 250, settlement
  rule "arrears ≤ one month's pay", disband at 10 % or 7 days at zero cohesion — was 2 pulses, §13).
- **Q3 evidence (not a recommendation):** §5 — under CivilianGrowthMilitary, DIR5 tier 1 does not hold for the military
  draw.
- **Significant-event candidates for D-079 Q2** (recorded as events, NOT made significant): MutinyBegan, MutinyEnded,
  CreditDefault, Disbanded, ArrearsBegan, LocalFamine.
- **Q-E5 (PROPOSAL, 2026-11-06):** ships' companies as a composition of the same force type (hulls of a lane N vessel
  class, the company from `VesselClass.Crew`), with the sea ration, rowing exertion, stowage by propulsion, seamen's
  pay, bunker coal, naval powder and scurvy of §2 rows 17–22 and the calibration of §12.3.
- **Q-E6 (PROPOSAL):** a port call yields fresh food (and restarts the scurvy clock) in an own port always, in a foreign
  port only when food was actually taken there. Alternative: any port call, or only own/allied ports.
- **Q-E7 (PROPOSAL):** occupation and close blockade are one mechanism — a hostile force standing in a locality cuts the
  owner's trade income by that locality's trade share (each locality once; with the blockade input, at most all of it);
  it changes only at the force's pulse starts and at its disbandment. The share is a per-locality input here; in the
  campaign it would come from the trade/route model (not built).
- **Q-E8 (PROPOSAL):** contributions are levied whatever the army's grain want (changed from the 2026-11-05 draft, where
  a well-fed army levied none). A mutinous army still plunders only when it wants grain (§12.6).
- **Integration dependencies:** the campaign has no treasury (CR-008, `Sim.Core/State/Governance.cs:39`), so coin pay,
  taxes into a treasury and borrowing need a money stock that does not exist in Sim.Core; the kernel landing is Q5; the
  in-progress war-economy state (carries, latched rates, chests) must be serialized (ADR-061 §3.8) — not done here.

## 10. Limitations

- Prototype stand-ins: the home front (one granary per polity, harvest as a linear inflow, civilian consumption and growth
  at turn starts) stands in for DemographicsSystem/Consumption; localities are single settlements with one stock.
- Food, pay and the other consumption are planned at the pulse start for the men present then; men who desert or die
  during the pulse do not reduce that pulse's consumption (their share is eaten or wasted).
- Within a pulse an army moves at the pulse-average speed; supplies shipped at a pulse start arrive within that pulse;
  rates use the men at the pulse start (losses do not compound within a pulse — superseded for head losses by §14 E2-1:
  desertion, disease and animal deaths now compound per day; consumption and pay still use the men at the pulse start);
  grazing ignores season.
- The supply fleet is the army's own train; hired or requisitioned civilian carts and depot convoys are not modelled
  (that is why an ox-wagon army 180 km from its depot starves in the first draft run, §4 note).
- No orders were added, so no new order-delivery semantic exists (T2's pins cover the war's existing ones); no new row
  type is serialized, so no populated round-trip test applies.
- Battle results, casualties and the fog/observer view of economics (WA3) are other lanes' work; combat days are an
  input.
- Own-side plunder is recorded (grain and money tallies, §13 E-V9) but has no consumer yet.
- Mutants ran against the lane E suite only (the lane's code is referenced by nothing else).
- Naval (2026-11-06): see §12.6.

## 11. Files changed

- Added: `Proto.M7/WarEcon/{WarCapability,WarEconTables,ArmyLogistics,WarFoodAllocator,WarEconomyWorld}.cs`.
- Added: `Proto.M7.Tests/WarEconE/{WarEconRig,WarFoodAllocatorTests,WarLogisticsTests,WarEconomyWorldTests,WarEconScenarioTests}.cs`.
- Edited: `Proto.M7/War/WarLedger.cs` (six `Commodity` members appended; no existing value changed).
- Added: `reports/H-E.md`.
- 2026-11-06: added `Proto.M7/WarEcon/NavalLogistics.cs`, `Proto.M7.Tests/WarEconE/WarEconNavalTests.cs`,
  `Proto.M7.Bench/WarEconBench.cs`; edited the lane's own `WarCapability.cs` (Antiscorbutics flag, `Naval` capability),
  `WarEconTables.cs` (naval rates; terrain tables for all 11 classes), `ArmyLogistics.cs` (naval composition, Water
  segments), `WarEconomyWorld.cs` (naval, coal, scurvy, occupation, contributions), `WarEconRig.cs` (Coal in the totals);
  shared files: `War/WarLedger.cs` (`Commodity.Coal = 8` appended) and `Proto.M7.Bench/Program.cs` (one dispatch line).
  `WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`, `TacticalBattle*.cs` and lane N's files are unchanged.

## 12. Resumed session, 2026-11-06 (restore, merge, lost work redone)

### 12.1 Restore and merge (measured)

- `/dev/shm/m7c/E` did not exist (container restarts wipe `/dev/shm`). Rebuilt with `git clone --shared` of the dev
  repository, `lane/E` at `fc5230c`, reset to `scratchpad/m7c/bundles/E.bundle` (head `6dda903`, the committed work of
  2026-11-05). `scratchpad/m7c/wip/m7c-E.patch` (12:06 snapshot) is byte-identical to `git diff fc5230c..6dda903` (`cmp`),
  so the bundle lacked nothing. No `E-wip.bundle` existed before this session.
- Dev `claude/new-session-yavghm` at `444b85e` (T3 action windows, lanes N, TR, RAM, P) merged into `lane/E`: **no
  conflicts** (lane E's files are new; `WarLedger.cs` was touched by lane E only).
- One semantic merge gap found by reading, not by a failing test: lane TR appended `Marsh = 8`, `Ford = 9`,
  `Building = 10` to `TerrainClass` (`TerrainCostTable.ClassCount` 8 → 11) while lane E's `TerrainLogistics` grazing and
  wear tables still had 8 entries, so a route over Marsh or Ford would have thrown `IndexOutOfRangeException` in
  `GrazePermille`, and a `Building` segment (cost `Impassable` = 0) would have been crossed at zero cost. Fixed: both
  tables have 11 entries (Marsh 500 ‰ grazing / wear 1600, Ford 600 / 1300, Building 0 / 0; PROPOSAL) with a length guard,
  `Route` refuses `Building` (and `Road`, `Wall`), and `Water` is now accepted as a sea or deep-river passage (§12.2).
  Pinned by `Terrain_EveryClassCovered_MarshPassableAtItsCost_BuildingRefused` and mutant NM17.

### 12.2 What was rebuilt (the work lost after 12:06 on 2026-11-05)

All in lane E's own files except two one-line appends to shared files (`Commodity.Coal = 8`; a bench dispatch line).

- **Ships' companies on lane N's vessel model** (`NavalLogistics.cs`): a naval force is an `ArmyComposition` with a lane N
  `VesselClass` and a hull count; its company is hulls × `VesselClass.Crew` (no parallel crew table), plus embarked foot up
  to hulls × `VesselClass.Capacity`. The class must be one the polity's naval capability can man: the propulsion needs
  lane N's `NavalCapability` flag (Oars / Sails / SteamPower) and a gun-armed class (any lane N naval weapon other than
  the deck archers) needs Gunpowder. `PolityCapability` carries `Naval` (lane N's enum) beside the WarTech flags; tests
  take the class from lane N's `NavalDesign.Derive`. Nothing reads a date (Law 4).
- **Movement**: passage speed by propulsion on `Water` segments only (oar 60, sail 140, engine 300 km/day; PROPOSAL); a
  land army halts at the water's edge, a fleet at the shore (embarkation is not modelled). At a land/water boundary a force
  stands on its own side (`StandingSegment`). Supply crosses Water by boat (carriage mode Water).
- **Sea ration, rowing, stowage, pay, coal, powder, scurvy**: §2 rows 17–22.
- **Occupation trade loss** (§2 row 23) and **contributions whatever the grain want** (§2 row 8a).

Event-time semantics (each pinned turn-exactly, ±1 ms where an instant matters):

| Semantic | When | Pin |
|---|---|---|
| Occupation begins | at the occupying force's pulse start at which it stands in the enemy locality (owner a participant of its war; corrected §14 E2-8); the victim is accrued at the old rate up to that instant first | `Occupation_CutsTheTradeShare_…` (instant, −1 ms, +1 ms; treasury exact) |
| Occupation ends (moved on) | at the force's next pulse start outside the locality | same test (instant and −1 ms) |
| Occupation ends (disbanded) | at the pulse end at which the force disbands | `Occupation_CountedOncePerTown_…` (instant and −1 ms) |
| Scurvy clock restart | at a pulse start in port with fresh food | `Scurvy_RisesWithTimeAtSea_…` (PortCall at every pulse start in an own port; none at a bare foreign port) |
| Scurvy rate | latched at the pulse start from the time since fresh food | same test, pulse by pulse; crossing invariance with 720-day vs 360-day turns |

### 12.3 Naval calibration (PROPOSAL; model values, the reference each is set against)

1 gm = 0.252 kg of grain-equivalent (the §3 mapping).

| Quantity | Model | Reference | Status |
|---|---|---|---|
| Sea ration (everyone aboard) | 1,800 gm/yr = 1.26 kg/day | heavier than the soldier's camp ration (0.91 kg); RN ration of biscuit, salt meat, pease, beer | knowledge, unverified |
| Rower under way | +800 gm/yr → 2,600 gm/yr = 1.82 kg/day | heavy sustained labour | unverified |
| Rowers per oared crew | 850 ‰ (170 of a galley's 200) | trireme: about 170 oarsmen (62 thranitai, 54 zygitai, 54 thalamitai) of about 200 aboard | secondary sources found by search (Hellenic Navy, World History Encyclopedia) |
| Passage speed | oar 60, sail 140, engine 300 km/day (1.3, 3.1, 6.7 knots averaged); **engine superseded in §13 (E-V7): the lane N class's MaxSpeedMmPerS × 550 ‰** | galleys beach at night; sail averages a few knots over a passage; economical steaming | unverified |
| Stowage | oar 5, sail 120, engine 90 days of the company's ration | galleys carried very little; sailing warships victualled for months | unverified |
| Coal | 60 t per hull per 1000 km (18 t/day at 300 km/day) | order of magnitude for a mid-19th-century steam frigate | unverified |
| Naval powder | 5 t per gun-armed hull per day of action | several tonnes of powder and shot per heavy ship in a day's action | unverified |
| Seaman's pay | 12 soldier-months per man-year | of the order of a soldier's pay | unverified |
| Scurvy onset | 56 days without fresh food | sources found by search range from about 4 weeks to "a few months" | secondary sources found by search; contested range |
| Scurvy mortality | +400 ‰/yr per 30 days beyond onset, cap 4,000 ‰/yr | Anson 1740–44: about 1,300 of about 2,000 men died, mostly of scurvy | secondary sources found by search |
| Antiscorbutics | × 50 ‰ | RN mandated about ¾ oz of lemon juice a day from 1795; a 1793 voyage to Madras with lemon juice was free of scurvy | secondary sources found by search (USNI Naval History) |

Model outputs (measured by the tests): pulse k of a squadron 1,000 km out with no antiscorbutics has the latched scurvy
rate f(7k days): 0 up to pulse 8 (day 56), 93 ‰/yr at pulse 9 (day 63), rising 400 ‰/yr per 30 days; with
Antiscorbutics, floor(f × 50 / 1000). Five steam frigates steaming a full 7-day pulse burn exactly
630,000 kg of coal and make 2,100 km; with 315,000 kg exactly 1,050 km; with none, 0 km and no hunger.

### 12.4 Tests, build, scan (measured in this clone; four CPUs shared with other agents)

| Solution / config | Before: `444b85e` alone (measured this session) | Merged `bfceecd` (444b85e + lane E of 2026-11-05) | After (this commit) |
|---|---|---|---|
| `M7Proto.slnx` Debug build | 0 warnings, 0 errors | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `M7Proto.slnx` Debug tests | **775** passed, 0 failed, 0 skipped (38 s) | **810** passed, 0 failed (29 s) | **827** passed, 0 failed, 0 skipped (44 s) |
| `M7Proto.slnx` Release build | 0 warnings, 0 errors | — | 0 warnings, 0 errors |
| `M7Proto.slnx` Release tests | **775** passed, 0 failed, 0 skipped (12 s) | — | **827** passed, 0 failed, 0 skipped (14 s) |
| `M7DetMath.slnx` Debug / Release | not re-run: source identical to `444b85e` (`git diff` empty) | — | 39 / 39 passed, 0 failed, 0 skipped |
| `M7Econ.slnx` Debug / Release | not re-run: source identical to `444b85e` (`git diff` empty, Sim.Core/Sim.Data unchanged) | — | 34 / 34 passed, 0 failed, 0 skipped |
| Determinism scan (`scripts/check-proto-determinism.sh`) | — | — | CLEAN: 79 files under `Proto.M7/`, 0 pattern groups with findings |

All builds and tests ran with `nice -n 19` and `-m:1` (throttle note A); durations are wall clock on four CPUs shared
with other agents. Against the `444b85e` baseline: +52 tests (35 of 2026-11-05, 17 of 2026-11-06), none failing,
none skipped, none removed.

- New: 17 tests in `Proto.M7.Tests/WarEconE/WarEconNavalTests.cs`. All 810 tests of the merged tree (775 of `444b85e` +
  35 of lane E's 2026-11-05 work) pass unchanged. **Moved goldens: none** (no lane E test pins a hash value; T2's golden
  is untouched — appending `Commodity.Coal` changes no existing account byte).
- Populated round trips / tie-dense: no new serialized row type and no new ordering over double scores (all new
  arithmetic is integer).
- Conservation: `Conservation_ExactAcrossAStraddle_WithASteamSquadron` — every commodity total, coal included, equal by
  exact `Int128` comparison at the turn-start instant of a straddling pulse, ±1 ms, a pulse end and the end.

### 12.5 Mutants (bounded at 10 × the clean filtered run: 1.85 s measured → 18.5 s per test run; none hung)

Prioritized subset on the new code, one lane-suite run (`FullyQualifiedName~WarEconE`, 52 tests) per mutant; each mutant
applied, the test project rebuilt (build exit code checked), the suite run, the file restored and checked by SHA-256, and
the clean tree rebuilt at the end. **A first run (run 1) was void**: the runner did not check the build exit code, one
mutant (NM3) did not compile (unreachable-code warning as error) and the suite ran against the previous mutant's binary.
Run 2 (below) fixed the runner; only run 2 is evidence. 17 of 18 killed by a semantic assertion; 1 survived.

| Mutant | Change | Result (run 2) |
|---|---|---|
| NM1 | Sea ration replaced by the field extra | killed: `SeaRation_StandingNeed_IsTheClosedForm` |
| NM2 | Rowing exertion for every propulsion | killed: `Rowing_ExertionOnlyForOaredHulls_…` |
| NM3 | Steam burns no coal | killed: `Steam_BurnsCoalPerKmSteamed_…`, `Conservation_…WithASteamSquadron` |
| NM4 | Movement not limited by bunker coal | killed: `Steam_BurnsCoalPerKmSteamed_…` |
| NM5 | A port call does not restart the scurvy clock | killed: `Scurvy_RisesWithTimeAtSea_…` |
| NM6 | Antiscorbutics ignored | killed: `ScurvyRate_…`, `Scurvy_RisesWithTimeAtSea_…` |
| NM7 | Scurvy from day 0 (no onset) | killed: `ScurvyRate_…`, `Scurvy_RisesWithTimeAtSea_…` |
| NM8 | Seamen unpaid | killed: `SeamensPay_AccruesExactly_…` |
| NM9 | Occupation share counted per force, not once per town | killed: `Occupation_CountedOncePerTown_…` |
| NM10 | Victim not accrued at the old rate before the occupation change | killed: `Occupation_CutsTheTradeShare_…` (exact treasury) |
| NM11 | Occupation flag left set at disbandment | killed: `Occupation_CountedOncePerTown_…` (assertion added after this mutant survived the first draft: the revenue alone could not see it because `IsOccupied` also skips disbanded forces) |
| NM11b | Victim's trade not restored at disbandment (no relatch) | killed: `Occupation_CountedOncePerTown_…` |
| NM12 | Contributions only when grain is wanted (the 2026-11-05 behaviour) | killed: `Contributions_LeviedWhateverTheGrainWant` |
| NM13 | A land army marches across Water | killed: `Water_LandArmyHaltsAtTheShore_FleetAtTheCoast` |
| NM14 | Deck archers count as guns | killed: `NavalPowder_OnlyGunArmedHulls_…` (and 8 more, through validation) |
| NM15 | Galleys stow months of provisions | killed: `Stowage_GalleysDays_SailingShipsMonths_…` |
| NM16 | Marsh and Ford give no grazing | **SURVIVED** — the tests pin that every class has an in-range entry, not the PROPOSAL values themselves; a value pin would be a calibration golden, not a property. Recorded, not "fixed". |
| NM17 | Pre-merge 8-entry terrain tables | killed: `Terrain_EveryClassCovered_…` |

Each killed property is one the system ought to have (ADR-015 §7.2): DIR5 §2.11 composition (NM1, NM2, NM8, NM14, NM15),
"ancient units must not consume oil" applied to coal (NM3, NM4), scurvy as a mechanism of time without fresh food and
its preventive (NM5–NM7), exact accrual at the instant a rate changes (NM10), occupation counted once and ended with the
force (NM9, NM11, NM11b), the contribution semantics of Q-E8 (NM12), land/water separation (NM13), the merge fix (NM17).

### 12.6 Bench (Release, `dotnet Proto.M7.Bench.dll wareconE [armies] [squadrons] [pulses]`; Stopwatch for reporting only)

| Scenario | Rows (army-pulses) | Median of 5 runs | Per row | State hash |
|---|---|---|---|---|
| 8 early-modern armies + 2 squadrons (12 ships of the line each), 520 pulses | 1,945 | 22.0 ms (first invocation), 136.8 ms (second invocation, same build) | 11.3 / 70.3 µs | `7174560048164D2A` both invocations |
| 32 armies + 8 squadrons, 520 pulses | 7,685 | 75.4 ms | 9.8 µs | `FFD4DCA3C445A76C` |

- The 6× spread between two invocations of the same configuration is CPU contention from the other agents (the runs
  are `nice -n 19`); the per-row cost is of the order of 10 µs when the machine is quiet. The same hash in two separate
  processes is a cross-process repeatability check of this configuration (not a cross-machine one).
- Rows < forces × pulses because forces disband during the run: the treasury (5 M soldier-months) cannot pay 8–32
  armies for ten years, and the insolvency cascade (§4) ends them — the bench measures the cascade path too.

### 12.7 Limitations of the 2026-11-06 work

- Coal goes from the polity's stock to the bunker at the pulse start without crossing the supply line (no collier
  throughput or loss); banked fires in port burn nothing; steam warships cannot fall back on sail.
- The supply line victuals a fleet at sea every pulse (victualling ships); stowage only caps the restock request. Oared
  fleets were in reality bound to the shore by food and above all water; water is not a commodity here.
- A port call is binary (fresh food or not); fresh food is not a commodity; scurvy affects naval forces only (sieges and
  winter quarters also produced it on land).
- Ship losses (hulls) are the battle layer's; the company shrinks only by desertion, disease and scurvy, pro rata.
- Embarkation and landing are not modelled; a land army halts at the water's edge.
- The trade share of a locality is an input; occupation by a squadron in a foreign port is treated as a close blockade of
  that port (the same mechanism). The occupier gains nothing from the lost trade except the contributions it levies.
- A mutinous army still plunders only when it wants grain (the money it would extort from a well-supplied position is
  not taken); unchanged from 2026-11-05 to keep the measured cascade scenarios intact (Q-E8).
- NM16 survived (calibration values of Marsh/Ford grazing are not pinned by design).
- The user's request relayed with this resumption ("reduce the 75 mins thing to 15 mins") names no setting in this lane's
  brief; it was applied as a 15-minute checkpoint cadence (commits plus `E-wip.bundle` refreshed after every step).

## 13. Verification round 1 (fix round, 2026-11-06)

Clone `/dev/shm/m7c/E`, branch `lane/E`, starting at `404956e` (the verified commit). Every finding was first reproduced
against that tree; only reproduced findings were changed. Logs of every measurement below are in the clone's
`r1logs/` directory (not committed). All builds and tests ran with `nice -n 19` and `-m:1` (throttle note A).
The relayed user request "reduce the 75 mins thing to 15 mins" names no setting in this lane's brief; as in §12.7 it was
applied as the checkpoint cadence (commits plus `E-wip.bundle` refreshed after each step, well under 15 minutes apart).

### 13.1 Per-finding verdicts

| Finding | Verdict | Reproduction (measured on `404956e`) | Fix | Semantic test(s) |
|---|---|---|---|---|
| E-V1 (major) Law 3: per-pulse shares | **REPRODUCED** | Verifier probe, run in my clone: one 1,000-man army in an enemy town with 1,000,000 money over 28 days levied 190,000 / 343,900 / 651,320 / 947,662 with 21 / 7 / 3 / 1-day pulses (identical to the verifier's figures). Disbandment counted pulses (`ZeroCohesionPulses >= 2`). | New `StockDraw.Share`: foraging, forced requisition, plunder and contributions take a share of what is left at a NOMINAL per-year rate applied once per elapsed day (part-day pro rata), so n days in one pulse take exactly what n one-day pulses take. Rates calibrated so 7 days take the former 40 % / 90 % / 10 %: 25,335 / 100,910 / 5,378 ‰/yr. Disbandment: `DisbandZeroCohesionDays = 7` after the instant cohesion reached zero (interpolated on the pulse's linear path). | `StockDraws_TakeTheSameOverTheSameDays_WhateverThePulseLength` (42 days, 21/7/3/1-day pulses: contributions 468,545 and foraging 1,906,678 in all four, equal to an independent loop); `StockDraw_IsPartitionInvariant_AndCalibratedToTheFormerSevenDayShares`; `Disbandment_WaitsADurationAtZeroCohesion_NotAPulseCount` (7-day and 1-day pulses) |
| E-V2 (major) loyal army at home starves the town at the settlement | **REPRODUCED** | Verifier probe: army in its own town foraged 11,816,666 gm, 66,667 gm left 1 ms before the settlement, 2,286 LocalityFamineDeaths (0 with the army outside). | `LocalReserve` = exactly what the next settlement will charge the locals: their consumption over the whole interval since their LAST settlement, with the eating carry, before any harvest (the stand-in credits it in a lump at the settlement). | `LoyalArmyAtHome_LocalsEatInFullAtTheSettlement`: forages exactly 500,000 gm (12,000,000 − 11,500,000), 0 deaths, stock exactly spent at the settlement. The old `LoyalArmyAtHome_NeverSeizes…` still passes. |
| E-V3 (major) refusal to march, desertion by arrears duration, ration shortfall in combat effectiveness untested | **REPRODUCED** | Verifier mutants VM1, VM3, VM4 re-run in my clone (bound 19 s = 10 × 1.99 s clean): all SURVIVED, 52/52 passing (`r1logs/mutants-before.log`). | Tests only (the mechanisms existed). | `MutinousArmy_RefusesToMarch` (kills VM1), `Desertion_RisesWithTheMonthsInArrears_ClosedForm` (exact base + min(cap, 30 × months), kills VM3), `SupplyFactor_IsTheWorstOfFedMunitionedEquipped_ClosedForm` (exact Q32 per pulse, with and without an equipment stock; kills VM4) |
| E-V4 (minor) six more surviving mutants | **REPRODUCED** | VM2, VM5, VM6, VM8, VM10, VM11 SURVIVED; VM7, VM9, VM12 KILLED — the verifier's result exactly. | Tests only. | `PayDue_FollowsTheMenPresent_ClosedForm` (VM2), `DiseaseRate_IsTheClosedFormOfHungerAndEquipment` (VM5), `FodderShortfall_RaisesAnimalDeaths_ClosedForm` (VM6), `OwnTown_IsNeverOccupied_OwnTradeIntact` (VM8), `Mutiny_PersistsWhileTheArmyStarves_EvenWhenPaid` (VM10), `DrillMunitionsAndWear_AreCarried_AndIndependentOfPulseLength` (VM11) |
| E-V5 (minor) roads do not cut wagon loss | **REPRODUCED** | Verifier probe: Mountain wagons over 100 km lose 2/3 on DirtPath, Track and Built alike (march costs 3400 / 2125 / 1225). | `ArmyLogistics.GroundCost` (terrain softened by its road, the march's own cost without the motor penalty) now scales pack and wagon carriage loss too. | `SupplyLine_RoadCutsWagonLoss_OverRoughGround` (17/30 > 17/48 > 49/240, exact) |
| E-V6 (minor) neutral towns treated as enemy ground | **REPRODUCED** | Probe: a town of polity 2 (not in the war) paid 343,900 contributions, was foraged and occupied (Occupation event, polity 2's revenue rate cut from 1,000,000 to 500,000). | `Hostile(army, owner)` = another polity that is a participant of the army's war (`WarTheaterWorld.IsParticipant`, a one-line read-only query added to the shared file). Neutral: no foraging, contributions, requisition or occupation; mutineers still plunder anyone. | `NeutralTown_IsNotEnemyGround` (with the enemy-owned town as the control) |
| E-V7 (nit) steam passage speed not from lane N | **REPRODUCED** | Inspection: `PassageSpeed` switched on propulsion only; TurretWarship (15,400 mm/s) and SteamFrigate (5,700 mm/s) both 300 km/day. | Engine-driven: the class's `MaxSpeedMmPerS` × 86.4 × `PassageEngineSustainedPermille` (550 ‰, PROPOSAL) → 731,808 vs 270,864 m/day. Oar and sail keep the propulsion table (their lane N speed is a sprint / best point of sailing). | `SteamPassageSpeed_IsTheLaneNClassSpeed`; `Steam_BurnsCoalPerKmSteamed_…` re-pinned (below) |
| E-V8 (nit) per-pulse rounding of drill and wear; backdated arrears | **REPRODUCED** | Probe: drill munitions over 28 days 1,937 kg (7-day pulses) vs 1,960 kg (1-day); kit replaced 112,201 vs 112,398. Inspection: `ArrearsSinceMs = PulseStart` set at the pulse end. | Persistent exact carries for drill munitions and wear by time and by distance; new arrears date from the chest-dry instant planned at the pulse start (`PulseStart + W × chest / pulse due`). | `DrillMunitionsAndWear_AreCarried_AndIndependentOfPulseLength` (1,944 kg and 112,777 milli-kits under both cadences, attrition off); `Arrears_DateFromTheInstantTheChestRanDry` (3.4978 d under 7-day pulses, 3.5022 d under 1-day pulses, continuous 3.4978 d) |
| E-V9 (nit) conservation tests cannot fail; dead code; unrest units; stale §1 | **REPRODUCED** | Inspection: `WarLedger` exposes only `Open` (constructor only) and `Transfer`, so commodity totals are invariant by construction; all 9 surviving mutants passed every conservation assertion. `restockAtArmy` written, never read; `Unrest += grain/1000 + money`; §1 stale. | Conservation tests unchanged and now described as evidence of ledger use only — the semantics rest on the closed-form tests above. `restockAtArmy` removed; own-side plunder kept as two tallies in their own units (`OwnSidePlunderGrainOf`, `OwnSidePlunderMoneyOf`); §1 corrected in place. | `Mutiny_EndsWhenArrearsAreCleared` now asserts the grain tally equals the sum of the Plunder events exactly |

No finding was refuted. Residual measured during reproduction (not a finding, unchanged): with attrition on, drill
munitions over 28 days still differ by 3 kg of 1,937 between 1-day and 7-day pulses after the fix, because desertion and
disease are per-year rates LATCHED at pulse starts (explicit first-order integration of the men, §10): the men present
differ slightly, not the rounding. **Corrected in §14 (E2-1):** this understated the effect by far — at starvation rates the
same army kept 7.2 % fewer men under 21-day than under 1-day pulses; head losses now compound per day.

### 13.2 Pins moved (each attributed)

| Test | Before | After | Cause |
|---|---|---|---|
| `Steam_BurnsCoalPerKmSteamed_…` | 2,100 km, 630,000 kg coal, half coal = half distance | 1,896,048 m, 568,815 kg, half coal = floor(dist × 284,407 / 568,815) | E-V7 (frigate passage speed 270,864 m/day) |
| `Contributions_LeviedWhateverTheGrainWant` | floor(1,000,000 × 100 / 1000) = 100,000 | 99,996 (7 daily draws, independent loop) | E-V1 |
| `SupplyLine_LossFractions_AreExact` (Open + Track wagons) | 100/600 | 100 × 850 / (600 × 1000) | E-V5 |
| `Mutiny_EndsWhenArrearsAreCleared` (unrest) | `UnrestOf(0) > unrest0` | grain tally = Σ Plunder events; money tally > 0 | E-V9 |

No hash golden exists in the lane. Every other pre-existing WarEconE test passes with its assertions unchanged.

### 13.3 Files

- Added: `Proto.M7/WarEcon/StockDraw.cs`, `Proto.M7.Tests/WarEconE/WarEconFixR1Tests.cs` (17 tests).
- Edited (lane E's own): `WarEconomyWorld.cs`, `WarEconTables.cs` (`ForageForeignPermilleYear`, `ForageForcedPermilleYear`,
  `ContributionPermilleYear`, `DisbandZeroCohesionDays`, `PassageEngineSustainedPermille` replace the per-pulse and
  per-pulse-count parameters and the flat engine speed), `ArmyLogistics.cs` (`GroundCost`), `NavalLogistics.cs`,
  `WarEconomyWorldTests.cs` (helpers made `internal`; the unrest assertion), `WarEconNavalTests.cs`, `WarLogisticsTests.cs`.
- Shared file: `Proto.M7/War/WarTheaterWorld.cs` — one read-only method `IsParticipant(warId, polity)`, delimited by a
  comment; nothing else in the file changed.

### 13.4 Tests, build, scan (measured in this clone)

| Solution / config | Before (`404956e`) | After (this commit) |
|---|---|---|
| `M7Proto.slnx` Debug build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `M7Proto.slnx` Debug tests | **827** passed, 0 failed, 0 skipped (33 s) | **844** passed, 0 failed, 0 skipped (36 s) |
| `M7Proto.slnx` Release build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `M7Proto.slnx` Release tests | **827** passed, 0 failed, 0 skipped (13 s) | **844** passed, 0 failed, 0 skipped (13 s) |
| `M7Econ.slnx`, `M7DetMath.slnx` | not touched: neither references `Proto.M7`, and `git diff 404956e` is empty for their projects; not re-run | — |
| Determinism scan (`scripts/check-proto-determinism.sh`) | — | CLEAN: 80 files under `Proto.M7/`, 0 pattern groups with findings |

Against the `444b85e` baseline (775 in Debug and Release): +69 lane E tests (35 + 17 + 17), none failing, skipped or
removed. Lane E filter (`FullyQualifiedName~WarEconE`): 52 → 69 tests, clean run 1.98 s.
Populated round trips / tie-dense: no new serialized row type, no new ordering over double scores (all new arithmetic is
integer). Order-delivery semantics: none added (no orders).

### 13.5 Mutants (bounded at 10 × the clean filtered run: 1.98 s → 20 s per test run; none hung)

Each mutant: applied, test project rebuilt (exit code checked; a build failure is void), the lane suite run once, the
file restored and checked by SHA-256. VM1–VM12 are the verifier's mutants (VM8 and VM11 with patterns moved by the
fixes); FM1–FM9 mutate the fixes themselves. **Two first runs were void** (VM11 and FM7 did not compile: the carry field
became never-assigned, a warning-as-error); they were re-run with patterns that keep the field assigned
(`r1logs/mutants-after-rerun.log`). **21 of 21 killed**, each by at least one semantic (closed-form or behavioural) test.

| Mutant | Change | Killed by |
|---|---|---|
| VM1 | mutinous army keeps marching | `MutinousArmy_RefusesToMarch` |
| VM2 | pay charged for the initial men | `PayDue_FollowsTheMenPresent_ClosedForm` |
| VM3 | no arrears-duration desertion term | `Desertion_RisesWithTheMonthsInArrears_ClosedForm` |
| VM4 | supply factor ignores hunger | `SupplyFactor_IsTheWorstOfFedMunitionedEquipped_ClosedForm` |
| VM5 | no equipment term in disease | `DiseaseRate_IsTheClosedFormOfHungerAndEquipment` |
| VM6 | no fodder-shortfall term in animal deaths | `FodderShortfall_RaisesAnimalDeaths_ClosedForm` |
| VM7 | worn kit never replaced | `DrillMunitionsAndWear_…`, `SupplyFactor_…`, `Sanitation_…`, `World_TierOrder_…` |
| VM8 | own town counted as occupied | `OwnTown_IsNeverOccupied_…`, `NeutralTown_IsNotEnemyGround` |
| VM9 | no famine-risk event on forced requisition | `AncientDeepRaid_StarvationCascade_PulseByPulse` |
| VM10 | mutiny ends while starving | `Mutiny_PersistsWhileTheArmyStarves_EvenWhenPaid` |
| VM11 | no drill munitions | `DrillMunitionsAndWear_AreCarried_…` (re-run) |
| VM12 | plunder does not reduce arrears | `Mutiny_EndsWhenArrearsAreCleared` |
| FM1 | one day's share per pulse whatever its length | `StockDraws_…`, `StockDraw_IsPartitionInvariant_…`, `Contributions_…` |
| FM2 | disband at the first zero-cohesion pulse end | `Disbandment_WaitsADurationAtZeroCohesion_…` |
| FM3 | locals' reserve counted from now | `LoyalArmyAtHome_LocalsEatInFull…`, `LoyalArmyAtHome_NeverSeizes…` |
| FM4 | carriage loss ignores the road | `SupplyLine_RoadCutsWagonLoss_…`, `SupplyLine_LossFractions_AreExact` |
| FM5 | any foreign owner is the enemy | `NeutralTown_IsNotEnemyGround` |
| FM6 | flat 300 km/day steam | `SteamPassageSpeed_…`, `Steam_BurnsCoalPerKmSteamed_…` |
| FM7 | drill munitions rounded up per pulse | `DrillMunitionsAndWear_AreCarried_…` (re-run) |
| FM8 | arrears backdated to the pulse start | `Arrears_DateFromTheInstantTheChestRanDry` |
| FM9 | own-side plunder grain in thousandths | `Mutiny_EndsWhenArrearsAreCleared` |

Each killed property is one the system ought to have: Law 3 (FM1, FM2, FM7, FM8), DIR5 §2.11 "roads reduce costs"
(FM4), the locals' food under a loyal army at home (FM3, Q-E2), war only against the war's other side (FM5, VM8), lane
N's vessel model (FM6), and the brief's cascade items (VM1–VM6, VM9–VM12).

### 13.6 Bench (Release, `dotnet Proto.M7.Bench.dll wareconE`, default 8 armies + 2 squadrons, 520 pulses)

| Tree | Rows | Median of 5 | State hash |
|---|---|---|---|
| `404956e` (rebuilt in a temporary worktree this session) | 1,945 | 66.2 ms | `7174560048164D2A` (as §12.6) |
| this commit (two invocations) | 5,200 | 57.5 ms, 37.8 ms (7.3–11.1 µs per row) | `440EB2ACCA3E7130` both |
| this commit with only E-V5 reverted (raw terrain in carriage loss) | 1,945 | 96.4 ms | `ABE19EFE5A56E111` |

The row count changed because of E-V5, measured: the armies stand 180 km out at the end of a Hills + Track segment;
with the road now cutting their wagons' loss (ground cost 1190 instead of 1800) more food arrives, hunger is lower and
the armies stabilise above the 10 % strength threshold (army 0: 694 men at pulse 140 and after, threshold 580), where
before they starved below it (619 men at pulse 100; no rows after pulse 100–119). This also corrects §12.6: measured,
army 0 in that run had no arrears (paid in full throughout) — the forces were ended by the starvation path at the end of
the supply line, not by the treasury. The per-row cost is unchanged in order (µs; the `StockDraw` day loop is at most 21
iterations per draw); timings are on four CPUs shared with other agents.

### 13.7 Proposals for the Director (nothing here is decided)

- **Q-E9 (PROPOSAL):** stock draws as nominal per-year rates applied once per elapsed day (the foraging party's daily
  round), calibrated to the former 7-day shares. Alternative: assessed flows not proportional to the stock (e.g.
  contributions per head of the occupied population per year, foraging per forager-day), which are also Law-3 exact but
  change the scenario dynamics.
- **Q-E10 (PROPOSAL):** the locals' reserve at home is their whole charge at the next settlement, with no credit for
  the harvest to come (it arrives in a lump at the settlement in this stand-in; the polity granary, whose harvest accrues
  continuously, does net it). Alternative: net the harvest to come (the army could then empty the town's stores before
  the harvest, as the verifier noted).
- **Q-E11 (PROPOSAL):** enemy ground = localities of the war's other participants (`WarSpec` has no sides, so every
  other participant is the enemy — co-belligerents would be treated as enemies; sides are not modelled in T2). Neutral
  ground gives nothing: purchase from neutrals is not modelled; a hungry army in a neutral town goes hungry; mutineers
  still plunder it.
- **Q-E12 (PROPOSAL):** steam passage at 550 ‰ of the class's trial speed; disbandment after 7 days at zero cohesion.

### 13.8 Limitations of this round

- Draws are planned at the pulse start for the whole pulse (the levy is taken in advance, like food); over a span that
  is not a multiple of the pulse length the totals differ by the part of a pulse taken in advance (the probe over 28 days
  with 21-day pulses levies 42 days' worth by day 28). Over whole pulses the totals are cadence-independent (measured).
- A part-day of a draw is applied pro rata; T2's pulse lengths are whole days, so this path is exercised only by the
  unit test.
- Latched per-pulse rates (desertion, disease, animal deaths, morale, cohesion) remain first-order in the pulse length
  (residual measured in §13.1 — understated there; head losses fixed in §14 E2-1); the interpolated zero-cohesion
  instant assumes the pulse's linear path.
- When plunder relief clears a mutinous army's arrears, `ArrearsSinceMs` is not reset (observed while reading the code;
  not a verifier finding, unchanged — it is a defect: fixed in §14 E2-6).
- §4's pulse-by-pulse tables were not regenerated (note at the top of §4); the scenario tests behind them pass.

## 14. Verification round 2 (fix round, 2026-11-06)

Base `5551683` (the round-1 head, verified by the round-2 verifier in its own worktree `verify-E-r2`). Each finding was
reproduced in this clone before any change: the verifier's probe file was copied into this clone under another name and
run (it is not committed; its output is in the clone's `r2logs/`), and its six mutants R2M1–R2M6 were re-run against the
lane suite at `5551683` (all six SURVIVED, 69/69, as reported). No finding was refuted.

### 14.1 Per-finding verdicts

| Finding | Verdict | Reproduction (measured at `5551683`) | Fix | Semantic test(s) |
|---|---|---|---|---|
| E2-1 (major) head losses explicit Euler over the pulse | **REPRODUCED** | Probe A (100,000 starving men, enemy town, 42 days), men left / dead under 21-, 7-, 3-, 1-day pulses: 55,785 / 29,371; 58,832 / 27,262; 59,687 / 26,679; 60,117 / 26,384 (identical to the verifier's). Grain eaten 1,999,997 in all four | New `WarEcon/HeadcountLoss.cs`: survival compounded once per elapsed day (StockDraw's daily round; part-day pro rata), in 1e18 fixed point; desertion and disease share one survival product pro rata to their rates; the pulse's losses are planned at the pulse start in thousandths of a head and spread linearly over its sub-steps; the four head-loss carries now persist across pulses (a part-head was discarded at each pulse end). After: men left 60,202 / 60,107 / 60,095 / 60,089 (spread 0.19 %, was 7.2 %). The residual is the rates latched at pulse starts (hunger is read at each pulse start), not the integration: at constant rates the strength after 42 days is **3,224 men under all four cadences** | `HeadLosses_CompoundPerDay_SameWhateverThePulseCut` (first pulse exactly the exact-rational closed form per cadence; 42-day strength within 2 men across cadences, measured equal), `StarvingArmy_MenLeft_NearlyIndependentOfThePulseCut` (spread ≤ 0.25 %) |
| E2-2 (minor) disbandment duration unpinned | **REPRODUCED** | R2M1 (zero dated at the pulse end) and R2M2 (at the pulse start) both SURVIVED 69/69 | No behaviour change (the interpolation was right, only unpinned); accessor `ZeroCohesionSinceOf` | `ZeroCohesion_InstantInterpolated_DisbandAWeekAfterIt`: the zero instant equals start + w × cohesion-before / −Δ (Δ recomputed from the published rates, the pulse's hunger and arrears age) — 7-day pulses 550.5472 d, 21-day pulses 554.2915 d, both equal to the closed form — and disbandment is at the first pulse end ≥ that instant + 7 days, for both cadences |
| E2-3 (minor) morale-by-arrears and clock restart untested | **REPRODUCED** | R2M6 (morale arrears term deleted) and R2M5 (clock restart deleted) SURVIVED 69/69 | Tests only | `Morale_FallsWithTheMonthsInArrears_ClosedForm` (every fed loyal pulse in arrears moves morale by exactly −60 × 1000 × months × w / year; months 0–4 seen); `MutinySettled_RestartsTheArrearsClock` (settled at day 77 with 1,513 still owed; arrears dated from day 77) |
| E2-4 (minor) in-kind arrears dating and harvest netting untested | **REPRODUCED** | R2M3 (in-kind DryAt forced to the pulse start) and R2M4 (harvest credit removed) SURVIVED 69/69; `World_TierOrder` runs with `HarvestPerCapitaYear = 0` | Tests only | `Arrears_InKind_DateFromTheInstantTheWageChestRanDry` (wage chest 2,997 of 9,722 due → dry, and dated, at day 2.1579 exactly); `CivilianReserve_NetsTheHarvestStillToCome` (charge 563,883,334 − harvest to come 312,838,333 = reserve 251,045,001, exact) |
| E2-5 (minor) partition dependence per stock | **REPRODUCED** | Probe A foraged / seized: 1,568,320 / 431,677 (21 d), 851,046 / 1,148,951 (7 d), 541,052 / 1,458,945 (1 d), total 1,999,997; probe B contributions army 0 / 1: 415,006 / 302,542 (21 d) … 361,473 / 356,075 (1 d), total 717,548 | **PARTIAL.** The `StockDraw` doc claim is corrected (invariant per draw, not per stock; the limit stated). The TOTAL invariance is pinned. The mechanism is **not** changed: the orderly/forced split and the share between armies still depend on the pulse length (measured after the round: unchanged numbers). Fix options in §14.7 | `TwoArmiesOneTown_TotalContributionsInvariant` (717,548 under 21-, 7-, 1-day pulses) |
| E2-6 (minor) plunder does not restart the arrears clock | **REPRODUCED** | Probe D: arrears cleared by plunder on day 42, `ArrearsSince` stays day 0 through day 112; 1 ArrearsBegan, 0 ArrearsCleared | `Plunder()`: when relief clears the arrears, `ArrearsSinceMs = −1` and an `ArrearsCleared` event, exactly as back pay. After: 6 ArrearsBegan, 5 ArrearsCleared; at day 77 the clock dates from day 70 | `PlunderClearingArrears_RestartsTheClock` |
| E2-7 (minor) money contributions useless without coinage | **REPRODUCED** | Probe C (28 days): `WarTech.None` arrears 38,713 with town money 0 and with 1,000,000 (343,890 levied for nothing) | **PROPOSAL (WA4):** a polity without Coinage levies contributions IN KIND — the same rate on the town's grain, into the wage chest that pays its soldiers in kind (new `CascadeKind.ContributionInKind = 20`, grain gm); with coinage unchanged (money into the pay chest). After: `None` arrears 0, town money untouched, 661,193 gm levied in kind over 28 days; Coinage unchanged (343,890 money, arrears 0). The in-kind levy draws on the town's food after orderly foraging (famine risk rises; realistic, and a design choice) | `NoCoinage_LeviesContributionsInKind_IntoTheWageChest` (no money levy, town money unchanged, arrears below the no-contribution run 38,694, every commodity total conserved exactly) |
| E2-8 (nit) report says "foreign" | **REPRODUCED** | §2 rows 23 and 8a, §12 occupation pin row | Text corrected to "enemy locality (owner a participant of the force's war)"; row 8 and 8a also state the in-kind levy | — (text) |
| E2-9 (nit) state hash misses future-relevant state | **REPRODUCED** | Inspection: `StateHashHex` omitted `_locLastSettle`, the locality carries, the allocation rows and the in-pulse plan | `StateHashHex` adds the locality settlement clock and carries and every allocation row (decision and requests); the army part of both hashes adds the in-pulse plan (latches, totals, rates, planned head losses, `DryAtMs`), its carries and the row accumulators. No lane E golden hash is pinned (none moved); the ADR-061 §3.8 serialization is still not done | `StateHash_SeesLocalityCarriesAndThePulsePlan` (edit a locality carry or the planned desertion mid-pulse → hash differs, restore → equal; the planned desertion decides the pulse's end strength) |
| E2-10 (nit) supply factor unconsumed | **REPRODUCED** | `SupplyQ32` read nowhere outside the world and its tests; `ConditionMath` in no lane E test | Report corrected (§4 note: an output only, no seam to a battle). No wiring: it would edit the shared tactical files (out of scope) | `SupplyFactor_HandOff_LowersConditionMathAttack` (the starving row's factor, 0, gives `ConditionMath.Attack` 50 of 100 — the supply floor; the fed row's, 1, gives 100) |

### 14.2 Pins moved (each attributed, measured)

| Pin | Before → after | Cause |
|---|---|---|
| `FodderShortfall_RaisesAnimalDeaths_ClosedForm` | 39 → 37 animals (closed form rewritten: heads × (1 − (1 − rate/360,000)^7)) | E2-1 (compounding) |
| `World_TierOrder_GrowthThenMovementThenStarvation_UnderBothOrderings` | granary 75.76 M → 75.91 M gm (re-rigged) | E2-1, lost premise: the growth tier ran out with 904 gm left, less than the 10,000 gm the civilian reserve rose by in one pulse as deserters came home, so no pulse cut the military (stages 1 → 4); with 150,000 gm more the window returns (stages 1 → 3 → 4) |
| Bench `wareconE` | 5,200 rows, hash `440EB2ACCA3E7130` → 2,926 rows, hash `0EFB24C4E782B963` | Attribution by reverting one fix at a time (Release): only the persistent head-loss carries reverted → 5,200 rows (hash `499F8101AFB0295D`); all of E2-1 reverted → 5,200 rows (hash `C7050E8A30215C39`, not `440EB…` because the hash now covers more state, E2-9); E2-6 or E2-7 reverted → 2,926 rows, hash unchanged (not exercised by the bench). So the row drop is the carries. Why (inferred, not traced): the forces now lose the part-heads formerly discarded at each pulse end and reach the 10 % strength threshold sooner |

§4's pulse-by-pulse tables remain un-regenerated (now stale for two rounds; disclosed at the top of §4); every scenario
test behind them passes unchanged.

### 14.3 Files

`Proto.M7/WarEcon/HeadcountLoss.cs` (new), `Proto.M7/WarEcon/WarEconomyWorld.cs` (head-loss plan and sub-step flows,
persistent carries, plunder clock reset, in-kind contributions, hash coverage, one accessor), `Proto.M7/WarEcon/StockDraw.cs`
(doc only), `Proto.M7.Tests/WarEconE/WarEconFixR2Tests.cs` (new, 12 tests), `WarEconFixR1Tests.cs` (one moved pin),
`WarEconomyWorldTests.cs` (one re-rig), this report. Nothing outside `prototypes/m7-sprint1`; no shared file
(`WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`, `TacticalBattle*.cs`) touched.

### 14.4 Tests, build, scan (measured in this clone; four CPUs shared with other agents; `nice -n 19`, `-m:1`)

| Solution / config | Before (`5551683`) | After (this commit) |
|---|---|---|
| `M7Proto.slnx` Debug build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `M7Proto.slnx` Debug tests | 844 passed (the verifier's run on this commit; my run of the lane filter: 69 / 69) | **856** passed, 0 failed, 0 skipped (27 s) |
| `M7Proto.slnx` Release build | 0 warnings, 0 errors | 0 warnings, 0 errors |
| `M7Proto.slnx` Release tests | 844 passed (the verifier's run) | **856** passed, 0 failed, 0 skipped (14 s) |
| `M7Econ.slnx`, `M7DetMath.slnx` | not touched (no project of theirs changed); not re-run | — |
| Determinism scan (`scripts/check-proto-determinism.sh`) | — | CLEAN: 81 files under `Proto.M7/`, 0 pattern groups with findings |

Against the `444b85e` baseline (775 in Debug and Release): +81 lane E tests (69 + 12), none failing, skipped or removed.
Lane E filter: 69 → 81 tests. No new serialized row type (a new enum value only) and no ordering over double scores (all
new arithmetic is integer). Order-delivery semantics: none added.

### 14.5 Mutants (bounded at 25 s per test run, over 10 × the clean filtered run of 2.02 s; none hung)

| Mutant | Result | Killed by (semantic) |
|---|---|---|
| R2M1 zero cohesion dated at the pulse end | KILLED | `ZeroCohesion_InstantInterpolated_DisbandAWeekAfterIt` |
| R2M2 zero cohesion dated at the pulse start | KILLED | same |
| R2M3 in-kind dry instant = pulse start | KILLED | `Arrears_InKind_DateFromTheInstantTheWageChestRanDry` |
| R2M4 harvest credit removed from the civilian reserve | KILLED | `CivilianReserve_NetsTheHarvestStillToCome` |
| R2M5 arrears clock not restarted at a mutiny settlement | KILLED | `MutinySettled_RestartsTheArrearsClock` |
| R2M6 morale arrears penalty deleted | KILLED | `Morale_FallsWithTheMonthsInArrears_ClosedForm` |
| F1-euler desertion back to explicit Euler | KILLED | `HeadLosses_CompoundPerDay_…`, `StarvingArmy_MenLeft_…` |
| F1-carryreset head-loss carries reset each pulse | KILLED | `HeadLosses_CompoundPerDay_…` |
| F6-noreset plunder relief without clock reset | KILLED | `PlunderClearingArrears_RestartsTheClock` |
| F7-money money contributions without coinage | KILLED | `Arrears_InKind_…`, `NoCoinage_LeviesContributionsInKind_…` |
| F9-lochash locality carry left out of the hash | KILLED | `StateHash_SeesLocalityCarriesAndThePulsePlan` |
| F9-planhash planned desertion left out of the hashes | KILLED | same |

Every kill is by a test that asserts a property the system ought to have (a closed form, a dating instant, a
conservation, a hash coverage), not by a golden.

### 14.6 Bench (Release, `dotnet Proto.M7.Bench.dll wareconE`, default 8 armies + 2 squadrons, 520 pulses)

Two invocations of this commit: 74.3 ms and 64.7 ms median per run, 2,926 rows (22–25 µs per row), hash
`0EFB24C4E782B963` both. The attribution builds of §14.2 ran 35–94 ms on the same machine within minutes, so the
timings are too noisy (shared CPUs) to attribute a per-row cost change; the new work per army-pulse is two survival
products of at most 21 Int128 multiply-divides each (inferred small).

### 14.7 Proposals for the Director (nothing here is decided)

- **E2-7 contributions in kind (PROPOSAL under WA4):** a polity without coinage levies grain into the wage chest at the
  contribution rate; alternatives: no levy at all without coinage, or a levy in kind into the army's train (food rather
  than wages). Recommended as implemented: it is the form pre-monetary armies' exactions took (tribute in kind,
  unverified) and it pays the wages the brief says are paid in kind.
- **E2-5 (open, not fixed):** (a) one joint daily draw loop per locality per pulse across all claimants — armies in
  (Priority, Id) order, orderly before forced each day — which needs the forced-requisition decision before the draws
  (the "home supply failed" test moved ahead of foraging, decided on the projected home grant); (b) keep the per-pulse
  sequence and declare the split a decision-cadence effect (a commander re-decides forced requisition each pulse);
  (c) settle locality draws on a fixed daily sub-cadence independent of the pulse. Recommended: (a); it restructures the
  pulse plan's step order and the DIR5 tier tests depend on that order, so it is left for a round that can re-verify them.

### 14.8 Limitations of this round

- E2-5's mechanism is not fixed (above): the orderly/forced split and the share between armies sharing a town depend on
  the pulse length; totals do not.
- Rates are still latched at pulse starts (hunger, morale, arrears age): residual 0.19 % in probe A; the zero-cohesion
  instant differs between 7-day and 21-day pulses (550.55 vs 554.29 days) because the penalty inputs are latched.
- The supply factor is not consumed by any battle (no seam); the ADR-061 §3.8 serialization of this world is not done.
- §4's pulse-by-pulse tables are stale (two rounds of fixes); the scenario tests behind them pass.
- The full-suite "before" counts (844) are the verifier's measurement on `5551683`, not re-run by this agent.


## Sources

- [History Hit — How Did Alexander the Great Sustain His Army?](https://www.historyhit.com/how-did-alexander-the-great-sustain-his-army/) (Engels' figures, secondary)
- [Taking Measure: The March of Alexander the Great's Army (Macmillan)](https://digfir-published.macmillanusa.com/hunt5e/hunt5e_ch04_45.html)
- [O'Connor, *Chiron* 2013 (dainst.org)](https://publications.dainst.org/journals/chiron/article/download/483/5091/5617) (critique of Engels' grain figures)
- [Sack of Antwerp (Wikipedia)](https://en.wikipedia.org/wiki/Sack_of_Antwerp)
- [Mutiny of Hoogstraten (Wikipedia)](https://en.wikipedia.org/wiki/Mutiny_of_Hoogstraten)
- [Eighty Years' War, 1576–1579 (Wikipedia)](https://en.wikipedia.org/wiki/Eighty_Years%27_War,_1576%E2%80%931579)
- Parker, G., "Mutiny and Discontent in the Spanish Army of Flanders, 1527-1606", *Past and Present* 58 (1973) — cited by
  the search results; NOT read.
- Everything else marked "unverified" is from general knowledge and was not checked against a source in this session.
- 2026-11-06 (found by search; secondary): [USNI Naval History — Finding a cure for scurvy](https://www.usni.org/magazines/naval-history-magazine/2021/february/finding-cure-scurvy);
  [USNI Proceedings 1965 — Limes, lemons and scurvy](https://www.usni.org/magazines/proceedings/1965/february/limes-lemons-and-scurvy);
  [EBSCO — Lind discovers a cure for scurvy](https://www.ebsco.com/research-starters/history/lind-discovers-cure-scurvy);
  [Hellenic Navy — History of the ancient trireme](https://averof.hellenicnavy.gr/en/trireme-olympias/history-of-ancient-trireme/);
  [World History Encyclopedia — Trireme](https://www.worldhistory.org/Trireme/). Morrison & Coates, *The Athenian
  Trireme*, and N. A. M. Rodger, *The Wooden World*, were NOT read.
