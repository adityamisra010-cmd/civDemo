# H-S — Phase C lane S: siege and garrisons

First session 2026-11-05 (commit `c6d0ae2`); resumed 2026-11-06. This report supersedes the 2026-11-05 version (the old text is in git
history). **Verification round 1 (fix r1, 2026-11-06): §12.** Statements corrected in place by fix r1 are marked **[r1]**; §5 (Q14) was
re-measured and rewritten. **Verification round 2 (fix r2, 2026-11-06): §13.** Statements corrected in place by fix r2 are marked
**[r2]**. Clone `/dev/shm/m7c/S` (`git clone --shared` of `/home/user/m5w/dev`), branch `lane/S` cut from `fc5230c`.
ISOLATED PROTOTYPE: only `prototypes/m7-sprint1` changed. Nothing under `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui`
changed (D-079 Q5 is open; measured: 0 files outside `prototypes/` differ from `9503d2b`). No new project. Every number below was
measured in this clone unless it is marked **inferred**. Four CPUs were shared with other agents: wall times are indicative.

Authority: DIR5 §2.2 (siege) and §2.3 (garrisons); D-079 DR9, DR10, R13, R15, Q14 (OPEN), RDG-2 (READING), RDG-6 (READING);
contracts C15 and C30 of `docs/m7-plan/m7-2-m7-3-revision-2026-11-05.md`; D-080 WA1 (boundary orders apply to the pulse starting
at that instant) and WA4 (realistic per-pulse economics and non-payment); ADR-060 §2.5 (siege is a persistent operation, never
duplicates combat resolution); ADR-061 §3.11 (per-pulse economics, sub-stepping); D-039 D1-D5.

Not decided here (and not decided by this code): the siege evaluation clock (Q14 — both are implemented as a parameter; evidence
and a labelled recommendation in §5); the food-allocation interpretation (DIR5 §2.11 — the siege's shortfall split is a parameter,
§2.6); War Pulse durations (D-079 Q1 — T2's *proposed* table is used as input); every coefficient's final value (the calibration
below is a prototype fit, not a gameplay ruling); lane E's coefficient values (used as lane E labelled them: PROPOSAL).

## 0. The 2026-11-06 resume: what was done and why

| Step | What | Evidence |
|---|---|---|
| Restore | `/dev/shm/m7c/S` did not exist (container restart). Rebuilt from `scratchpad/m7c/bundles/S.bundle` (head `c6d0ae2`, the 2026-11-05 work); no `S-wip.bundle` existed. | `git diff fc5230c..c6d0ae2` is byte-identical to `wip/m7c-S.patch` (measured: empty diff). |
| Merge | `git merge` of dev `claude/new-session-yavghm` = `9503d2b` (T3, lanes N, TR, RAM, P, E) → `dd29664`. Conflicts: `WarLedger.Commodity` (lane E took codes 2-8, lane S had 2-6) and the bench dispatch; both sides kept. | M7Proto.slnx on the merged tree: Release 931/0/0, Debug 931/0/0; `9503d2b` alone (separate worktree): 856/0/0 both. The two siege replay goldens moved only by the commodity byte in the ledger hash: with lane S's old codes mapped back, the old goldens pass unchanged (931/931, measured). |
| Reuse lane E | Siege supply, garrison upkeep, desertion and disease now run on lane E's mechanisms (§0.1) instead of parallel ones. | Tests in `Siege/SiegeLaneEReuseTests.cs` and `SiegeEquationTests.cs`. |
| One edit to lane E's code | Lane E's desertion and disease-mortality formulas moved, unchanged, from `WarEconomyWorld.OnPulseStart` into the new `WarEcon/TroopLossRates.cs`, which both worlds call (delimited `[lane S]`; 4 lines replace 8). | Lane E bench `wareconE` state hashes identical before and after on four configurations (default `0EFB24C4E782B963`; `3 1 200` `CD446CA5470F013B`; `12 0 300` `C150D4CD20350257`; `1 3 400` `2A3BD5E90EAA1764`); lane E's tests all pass. |
| New siege mechanism | Desertion from an INVESTED place is contained by the besiegers' lines (§2.3 item 5): lane E's rate is a field army's, but a garrison's deserters must cross the lines. | `InvestedGarrison_Desertion_…`; calibration with and without it (§6.3). |
| Precision fix | Converting a Q32 fraction to lane E's integer scales (hunger ‰, morale ppm) now rounds to nearest; a floor biased every non-dyadic state toward hunger by 1 ‰ (found by a test: 801 vs 800). | `Desertion_IsLaneE_Rate_…`; mutant M21. |
| Re-measure | Calibration (20 seeds, both clocks), Q14 cost table, loss breakdown, 22 mutants. | §5, §6, §8. |

Progress after 12:06 on 2026-11-05 was lost with the container; nothing of it is reconstructed from memory. The 2026-11-05 deliverables
(model, garrisons, calibration, Q14 evidence, 17 mutants) were in `c6d0ae2`; they are carried forward and re-measured where the model
changed.

### 0.1 Lane E mechanisms reused (orchestrator note C)

| Mechanism | Lane E source | How the siege uses it | Pinned by |
|---|---|---|---|
| Conserved commodities | `War/WarLedger.cs` codes `People` (2), `Grain` (3, gm: 1,000 gm = one adult's year of food, D-015), `Munitions` (5, kg) | Food is Grain, gunpowder ammunition is Munitions, people are People. Lane S appends only what lane E does not model: `Water` (9, person-days), `Missiles` (10, shooter-days of non-gunpowder fire), `Ordnance` (11, pieces: siege machines and fortress guns). | Conservation tests (every commodity, exact). |
| Rations | `LogisticsRates.RationBase` (1,000 gm/yr), `RationFieldExtra` (300) | Civilians eat the base ration; garrison and besiegers the base + field extra (guard, repair, entrenching). | `TurnStart_SplitsTheSiegePulse_…` (exact gm). |
| Pay | `LogisticsRates.PayFoot/PayMounted/PayGunCrew/PaySeaman` (soldier-months), `WageInKind*` (gm), `WarTech.Coinage` | Garrison pay by arm: coin from the controller's treasury with Coinage, otherwise grain from its home granary. Replaces lane S's single PLACEHOLDER upkeep rate (which equalled PayFoot, 12). | `Pay_IsCoinWithCoinage_AndGrainWithout_…`, `Upkeep_IsExactPerTheatrePulse_…` (BigInteger closed form). |
| Arrears clock | lane E's arrears-since instant and whole 30-day months | Arrears start at the interpolated instant the pay source ran dry; their age in months enters desertion. | `Arrears_StartAtTheInterpolatedInstantThePaySourceRanDry` (**[r1] S-V5**: the older test below had an empty treasury from the first instant, so it never exercised the interpolation), `UnpaidPay_RunsUpArrears_WhoseAgeInMonths_DrivesDesertion_…` |
| Desertion | `TroopLossRates.DesertionPermilleYear` (base + low morale × (1 − M) + arrears months, capped + hunger) | Garrison under siege (siege morale, its arrears, its hunger), besiegers (their morale and hunger), and garrisons outside a siege on the theatre clock (full morale: base + arrears). Replaces lane S's morale-threshold desertion and its unpaid-share desertion. | `Desertion_IsLaneE_Rate_…`, `BaseDesertion_ThinsAPeacetimeGarrison_…`, `InvestedGarrison_Desertion_…` |
| Disease mortality | `TroopLossRates.DiseasePermilleYear` (base 60 ‰/yr × (1 + 4 × hunger), × 300 ‰ with `WarTech.Sanitation`) | The siege keeps its crowding-driven prevalence (SIS; the brief's "disease growth with crowding and starvation"); deaths = lane E's rate × prevalence / background prevalence (5 %). At the background the siege kills exactly what lane E's army economy kills. Replaces lane S's PLACEHOLDER mortality-among-the-sick. | `DiseaseDeaths_AtTheBackgroundPrevalence_AreLaneE_…`, `Sanitation_CutsDiseaseDeaths_…` |
| Head-loss integration | `HeadcountLoss.Survival/LossMilli` (per elapsed day on the declining strength; competing flows share one survival product) | Every per-head loss in a siege (fire, thirst, starvation, disease, desertion) is a ‰/yr rate integrated on lane E's survival product, with a carry in 10⁻¹⁸ head per account and flow (**[r1] S-V1**: the carry was in thousandths of a head on a loss already floored to thousandths per pulse, so on 60-second pulses every flow below 0.001 head per pulse lost nothing; lane S now uses `SiegeEquations.LossAtto` on `HeadcountLoss.Survival`, not `LossMilli`). Replaces lane S's per-flow implicit shares x/(1 + x). | `LossAtto_IsIndependentOfThePulseLength_…` (two-sided), `HeadLosses_AreTheSameOnEveryClock_…` [r1]; `LossRates_AreBounded_…`. |
| Munitions | `LogisticsRates.MunitionsPerFirearmCombatDay` (1 kg), `MunitionsPerGunCombatDay` (400 kg); `WarTech.Gunpowder`; lane E's top-up rule (shipped from the polity stock to the need) | Gunmen and manned fortress guns fire munitions at a siege share of a combat day (100 ‰, PLACEHOLDER); bombards and modern guns at a bombardment share (1,000 ‰, PLACEHOLDER); the besiegers' park is topped up from home stock as lane E tops up an army's. Bows draw Missiles. Gunpowder engines breach only while munitions are met. | `GarrisonRifles_FireLaneE_Munitions_…`, `Bombards_BreachOnlyWhileThePowderLasts_…`, `GunpowderEngines_BreachOnlyWithMunitions_…` |
| Capability | `PolityCapability`, `ArmyComposition.Validate` (firearms and guns need Gunpowder; trucks need MotorTransport) | The world takes each polity's capability (computed state; Law 4). Firearms, artillery, fortress guns, bombards and modern guns are refused at construction without Gunpowder: the gunpowder transition has its state-derived capability as well as the engine mix. | `GunsAndFirearms_NeedTheGunpowderCapability_…` |
| Supply line | `ArmyLogistics.Evaluate` (carriage loss by mode, ground, road, Magazines; throughput by segment and train), `Gross`, `Delivered`, `Route`, `TerrainLogistics` | Optional `SiegeSpec.SupplyLine` (route + train): the besiegers' need over each sub-step, grossed up for the loss and capped by the throughput and the home granary, is shipped; what does not arrive goes to a carriage-loss sink. Without a line the flat delivered rate of the spec applies (as before). | `SupplyLine_IsLaneE_Evaluate_…` (loss and throughput equal to a direct `Evaluate` call; road feeds, trackless hills starve), `SupplyLine_TrainIsValidatedByLaneE_…` |

**Not reused, and why.** (a) Starvation: lane E's `StarvationDeathPermilleYear` (3,000 ‰/yr × the immediate ration shortfall) is a
field army's; a besieged city rations deliberately and lives on body reserves for weeks, so the siege keeps its nutrition-state model
(τ = 30 days; deaths below 0.5 nutrition). (b) Lane E's equipment-exposure term (kit deficit): the siege has no kit. (c) Lane E's
morale, cohesion and mutiny: siege morale is the will factor of siege health (DIR5 "siege-health calculation"). (d) Foraging and
requisition (`StockDraw`): the besiegers do not forage in this model (a gap, §11). (e) Lane E's back pay: this world has no revenue,
so a pay source that ran dry never refills; the path would be unreachable and was not built (§11). (f) The food allocator
(`WarFoodAllocator`): inside a siege there is no growth tier and the defenders draw on the settlement's own granary; the siege's
shortfall split stays the explicit, unchosen `ShortfallPolicy` (§2.6).

## 1. What was built

| File | Role |
|---|---|
| `Proto.M7/War/Siege/SiegeTypes.cs` | Kinds and refusal codes; enums; `SiegeCoefficients` (only what is siege-specific; every coefficient with its provenance label); force, relief, supply-line, doctrine, siege, installation, formation and raid specs; `SiegeClock`; Q32 helpers. |
| `Proto.M7/War/Siege/SiegeEquations.cs` | The per-pulse equations as pure integer functions (fortification, wall and earth damage with munitions met, mining, approach, nutrition, disease, morale, capability with missiles and munitions, assault and line power, loss RATES); the battle seam `IAssaultResolver` and its labelled stand-in. |
| `Proto.M7/War/Siege/SiegeWorld.cs` | An `IEventWorld`: sieges on the chosen evaluation clock, the theatre War Pulse clock for garrisons, sub-stepping, orders with WA1 delivery, garrisons, pay, the ledger, records, the state hash. |
| `Proto.M7/War/Siege/SiegeCodec.cs` | Canonical fixed-length rows. **[r2]** Version 4: installation 223 bytes (v4 adds the theatre settle instant, §13), formation 99, siege 1,292 (v3, fix r1). The state hash is built from them (and, since fix r2, from a running digest of every pulse record). |
| `Proto.M7/War/Siege/SiegeScenarios.cs` | Nine historical sieges (five fit, four holdout) in lane E units, with each side's capability; `SiegeHarness`. |
| `Proto.M7/WarEcon/TroopLossRates.cs` (new, shared) | Lane E's desertion, disease and starvation rate formulas, extracted unchanged so lane E and lane S call one function. |
| `Proto.M7/WarEcon/WarEconomyWorld.cs` (edited, lane E's file) | The three rate expressions replaced by calls to `TroopLossRates` (delimited `[lane S]`; lane E behaviour byte-identical, §0). |
| `Proto.M7/War/WarLedger.cs` (edited) | Lane S codes 9-11 appended after lane E's 2-8; 0-8 unchanged. |
| `Proto.M7.Tests/Siege/*.cs` | **[r2]** 136 tests (§7, §12.6, §13.7): 88 at the resume, 119 after fix r1, 136 after fix r2. |
| `Proto.M7.Bench/SiegeBench.cs`, `Program.cs` | `siege` (scenario × clock × policy table with cost and the loss breakdown), `siegeseeds` (seed battery), `siegetrace` (per-pulse trace). |

`WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs` and `TacticalBattle*.cs` are **not** edited. Event kinds 60-70 and order kinds
180-187 are disjoint from WarKinds (40-43, 140-141), T3 (150+), the tactical kinds (101-108) and the demo kinds.

## 2. The siege model

### 2.1 State (per besieged installation)

- **Ledger stocks (long, Law 1).** The settlement's own granary (Grain, gm), cistern (Water), magazine (Munitions, kg), missile store
  (Missiles), guns (Ordnance) and civilians; one account per garrison formation; the besiegers' arms (melee, ranged, cavalry,
  engineers), machines by class, field granary and park (munitions, missiles); the relief army; per-polity treasury, home granary,
  home munitions and missiles, home troops, prisoners and an "elsewhere" account for evacuees; sinks for food eaten, wages in kind,
  carriage loss, water drunk, munitions and missiles spent, wages paid, killed, starved, dead of thirst, dead of disease, deserted and
  ordnance lost; a water source ("nature"). Every movement is a `WarLedger.Transfer`; every commodity total is constant (tested exactly
  at every turn snapshot and at the end, four scenarios).
- **No siege-only food stock (RDG-6, D-039 D5).** The defenders eat from the installation's granary; starvation is the food-system
  stand-in (§2.3 item 5).
- **Fractions (Q32).** Masonry integrity W; field works E; garrison and besieger morale; disease prevalence in both camps; nutrition
  state of garrison, civilians and besiegers; mining and approach progress; the ration level; ammunition met (missiles and munitions,
  each side).

### 2.2 Clocks and event order (unchanged)

- The **theatre clock** is the war's War Pulse schedule (T2's proposed table: 21/14/10/7/3/2/1 days). Garrison orders, pay, peacetime
  desertion, unrest and raids run on it.
- Each siege has an **evaluation clock**, a parameter (`SiegeClock`): War Pulses (first pulse truncated at the siege start) or fixed
  battle pulses anchored at the siege start (tactical placeholder 60 s).
- **Sub-stepping (DIR5 §2.4).** Linear flows (rations, water inflow, corridor, the besiegers' supply line or flat resupply, the park's
  top-up, ammunition, pay) are integrated exactly with carries (`RateIntegrator.IntegrateExact`) and split at every turn start, at the
  corridor's opening instant and before any change of the people they depend on. **[r1] S-V2:** this was not true of garrison pay —
  siege pulse ends (losses, reinforcement), assaults and sallies changed the garrison without settling the theatre's pay first, so a
  whole interval was paid at the head count at settle time; they now settle it first (`GarrisonPay_IsTheIntegralOfTheHeadCount_…`,
  `GarrisonPay_IsSettledBeforeAnAssaultOrASally…`). The nonlinear siege-health calculation runs once per
  pulse, at its end, over the whole pulse.
- **[r2] S2-V2:** the theatre clock's desertion of a garrison that is not under siege, and the occupation unrest, are integrated per
  installation and split at every siege start and end on it (and so at a capture), each part at the state that held over it. Before
  fix r2 both were applied once, at the theatre pulse end, over the whole pulse at the state of that instant: a siege starting
  mid-pulse skipped the desertion of the part before it, a siege ending mid-pulse with the garrison staying charged its besieged part
  twice, and a capture counted the whole pulse as occupied (`TheatreDesertion_IsChargedOnceOverTime_…`, `ACapture_InsideATheatrePulse_…`).
- **[r2] S2-V6:** wherever garrison pay and the sieges' linear flows are settled together, pay is settled first, so on a short home
  granary wages in kind are fed before a corridor or the besiegers' supply at every settlement, whichever event settles them
  (`PayInKind_IsFedBeforeTheCorridor_…`). A priority rule of mine (§10.12).
- Order of events at one instant: theatre pulse end (0); siege pulse end (1); turn start and raid arrival (2); theatre pulse start
  (3); relief arrival (4); siege pulse start (5). Significant (class 70): `SiegeEnded`, `AssaultResolved`, `Revolt`, `Probe`.

### 2.3 The per-pulse siege-health calculation (dt = pulse length; rates per sim-year)

1. **Nutrition.** Each group eats `ration × share of its due drawn`; ν' = (ν + κ n)/(1 + κ), κ = dt/τ, τ = 30 days.
2. **Disease (both camps).** SIS on crowding and malnutrition: β = β₀ × crowding × (1 + κ_m(1 − ν)); semi-implicit step, stays in
   [floor, 1] for any dt.
3. **Fortification.** fortFactor = 1 + wallValue(kind) × W² + fieldValue × E.
4. **Fire.** Casualties per year from the besiegers' ranged arm (× missiles or munitions met, by weapon) and machines (gunpowder
   machines × munitions met), × their crews' state; the garrison's fire (bowmen × missiles met; gunmen and manned guns × munitions met)
   × hunger × health. Each becomes a per-person rate ‰/yr = casualties / (population × protection) (the fortification protects the
   garrison, the lines protect the besiegers).
5. **People move (lane E's HeadcountLoss).** Per account, the competing rates — fire, thirst (acute, on water drunk), starvation
   (μ_max × shortfall below 0.5 nutrition), disease (lane E's rate × prevalence/background, Sanitation), desertion (lane E's rate;
   for an invested garrison × (1 − 0.75 × the besiegers' works): the lines contain deserters; an open corridor lets them walk) —
   share one survival product compounded per elapsed day and split pro rata; carries in 10⁻¹⁸ head [r1]. Then evacuation and
   reinforcement through an open corridor, and the besiegers' reinforcement.
6. **Counter-battery.** Manned defender guns × munitions met destroy machines.
7. **Walls and works.** dW/dt = −d + ρL(1 − W) (repair implicit; d = Σ engines × damage × vulnerability(fort, engine), gunpowder
   engines × munitions met, all × crews); field works dE/dt = bL(1 − E) − eE; mining progress (saturating, counter-mined); approach
   works gate any assault until complete.
8. **Morale.** Relaxes toward loyalty × hunger × disease × breach × casualties × hopelessness × unpaid pay (defender) and supply ×
   disease × casualties × hopelessness (besiegers). A beaten relief has two effects: an immediate fall of morale (FailedReliefShock,
   the event) and a lasting × (1 − ReliefHopeLost) on the defender's target for the rest of the siege (the state "a relief was
   defeated"); **[r1] S-V8**: these were one coefficient doing two jobs and are now two (same value, nothing moved).
9. **Siege health = will × capability:** H = M × clamp(D/D_ref), D without the fortification; the besiegers' health likewise.

### 2.4 Rationing (DIR5 §2.2)

A ration level r ∈ [0.15, 1] (defender order or AI doctrine). Consumption is r × need; endurance S/(need × r) is strictly longer for a
lower r; the hunger factor 1 − 0.5(1 − ν) lowers defensive capability as ν settles on r ("to some extent": half rations keep ¾).
Measured in a running siege (`Rationing_LowersSiegeHealthEarly_ButTheCityHoldsStrictlyLonger`): half rations give lower health and
capability at day 26 and a strictly later fall. Under lane E's rates hunger also raises desertion and disease mortality, so rationing
now costs men as well as fighting value (both pinned in the tests of §0.1). The AI doctrine counts days of food against the NET draw
(need minus an open corridor's inflow; `Doctrine_RationsAgainstTheNetDraw_…`).

### 2.5 Termination (all pinned turn-exactly; unchanged)

| Outcome | When | Consequence |
|---|---|---|
| Stormed | an assault (pulse start) succeeds | garrison remnant → the besieger's prisoners; control passes |
| Capitulated | H ≤ 0.2 at a pulse end | unconditional: garrison → prisoners; control passes |
| GarrisonDestroyed | no garrison left at a pulse end | control passes |
| Relieved | a relief battle (pulse start ≥ arrival) succeeds | siege ends; garrison stays |
| Lifted | besieger order or AI doctrine (pulse start), or the besiegers' health ≤ 0.1 (collapse) | siege ends |
| CapCapitulation | at the maximum, defender health < besieger health | surrender **on terms**: the garrison marches out (formations mobile at the exit cell, people kept); control passes |
| CapLifted | at the maximum, defender health ≥ besieger health (a tie → the defender holds) | the besiegers withdraw |

When control passes, the old controller's pay arrears to the garrison are written off (the installation's pay state restarts).

**Maximum duration (DIR5 §2.2; READING RDG-2).** Cap = start + 3 × L, L = the length of the campaign turn containing the start
instant, fixed at the start. The pulse in progress is truncated at Cap; the siege-health calculation runs over that truncated pulse
and the siege is then adjudicated. Pinned: active at Cap − 1 ms, ended at Cap; across the Industrial→Modern band edge a start at
edge − 1 ms takes L = 360 days, at the edge and at edge + 1 ms L = 180 days; the end record carries the turn of the cap instant; the
same on the battle-pulse clock. The adjudication rule is my design under the delegated equations and is flagged for the Director (§10).

### 2.6 The shortfall policy is a parameter (DIR5 §2.11 — not chosen; unchanged)

`ShortfallPolicy` (`CiviliansFirst`, `GarrisonFirst`, `ProRata`), passed explicitly by every caller. Inside a siege (growth zero)
both D-079 readings reduce to `CiviliansFirst`; historical siege practice was usually garrison first. Measured
(`ShortfallPolicy_DecidesWhoStarves…`): garrison remaining civilians-first < pro-rata < garrison-first, civilians the reverse. In all
nine calibration scenarios the three policies give identical outcomes, durations and losses (measured, §9), because the AI doctrine
rations before the store runs short.

### 2.7 The gunpowder transition and the modern encirclement (state-derived; Law 4)

There is no era switch. Two state-derived facts carry the transition: the polity's **Gunpowder capability** (lane E flag: without it
no firearm, artillery formation, fortress gun, bombard or modern gun can exist — refused at construction) and the besiegers'
**engine mix** (wall damage = Σ engines × damage × vulnerability(fortification kind, engine class), gunpowder engines only while their
munitions are met). Measured: bombards breach medieval stone and castles in weeks where trebuchets barely outpace repair; the bastion
fort holds more than five times longer than stone (`Gunpowder_BreachesMedievalWalls_…`); modern guns breach any masonry in days, but
earthworks absorb shot (earth damage per gun close to a bombard's), so masonry stops mattering and the field works, the garrison's
firepower and the supply corridor decide — the modern encirclement regime (Leningrad). An encirclement is the same model with walls
irrelevant, a corridor (food, munitions, evacuation, reinforcement, opening at a given instant) and no assault by doctrine.
`SiegeRegime` is a label from the machines, used only in records and tests. Law 4 metamorphic: the same force with bombards at −1000
and at 1700 gives the same regime and an identical trajectory; the same date with trebuchets differs (test unchanged, passes).

### 2.8 Battles go through a seam (unchanged)

Assault, relief battle, sally and raid build `AssaultInputs` and call `IAssaultResolver`; the prototype ships the labelled
`FormationAssaultStandIn` (odds × counter-RNG fortune keyed on siege, instant and purpose; success p = (RF)²/(1 + (RF)²); bounded loss
shares). Integration replaces it with `BattleResolution.Resolve`. A spy resolver test proves every battle goes through the seam.

## 3. Garrisons (DIR5 §2.3; C30)

| Function | Implementation | Test |
|---|---|---|
| Assign, withdraw, replace, reinforce | orders 184-187; effective at the first theatre pulse start ≥ T (WA1); every check at the effective instant; refusals recorded | `Assign_…`, `Withdraw_…` (pins at B−1, B, B+1), `IllegalGarrisonOrders_…`, `Replace_OverCapacity_…`, `Reinforce_…` |
| Capacity and eligibility | capacity in people (also binds corridor reinforcement); eligibility table by installation kind × family (PLACEHOLDER) | `IllegalGarrisonOrders_…`; **[r2]** `CorridorReinforcement_StopsAtTheInstallationsCapacity_…` (the corridor clause had no test, S2-V1 R2-M3) |
| One capability, one state (D-065 R11) | Mobile / Garrisoned / Captured; only Garrisoned formations count; air defence counted once | `AirDefence_IsCountedOnce_…` |
| Pay (lane E) | per arm per sim-year: coin with Coinage from the treasury, grain without it from the home granary; exact carry per theatre sub-step, split at turn starts | `Upkeep_IsExactPerTheatrePulse_…`, `Pay_IsCoinWithCoinage_AndGrainWithout_…` |
| Non-payment (WA4, lane E) | arrears with lane E's clock; + 30 ‰/yr desertion per whole month of arrears (capped at 600); the unpaid share also lowers siege morale | `UnpaidPay_RunsUpArrears_WhoseAgeInMonths_DrivesDesertion_…` (each 60-day window loses more than the one before; conserved; it runs with no siege); **[r2]** the morale term: `UnpaidPay_LowersTheBesiegedGarrisonsMorale_…` (S2-V1 R2-M9: it had no test) |
| Base desertion (lane E) | a garrison outside a siege deserts at lane E's base rate (50 ‰/yr) plus the arrears term, on the theatre clock; **[r2]** split at a siege's start and end, so each part of a theatre pulse deserts once, on the clock that holds it | `BaseDesertion_ThinsAPeacetimeGarrison_…` (144-148 of 3,000 in 360 days, compounded daily; measured 146); **[r2]** `TheatreDesertion_IsChargedOnceOverTime_…` (besieged = never besieged at every theatre pulse end, both clocks), `GarrisonPay_IsSettledAtTheSiegeStart_…` |
| Defence in sieges | the siege's defender force is the garrison aggregate | `AGarrison_DefendsInTheSiege_…` |
| Defence against raids | raid at the first theatre pulse start ≥ arrival, through the seam; pillage moves grain to the raider; a raid on an installation under siege is not fought (recorded, no losses) | `Raid_IsResolvedAt…` (pins), `Raid_IsRepelledByAGarrison_…`; **[r2]** `ARaid_OnABesiegedInstallation_IsNotFought_…` (S2-V1 R2-M8) |
| Occupation | **[r1] S-V7**: unrest grows from the grievance input only while the installation is OCCUPIED (its controller is not its population's polity, `InstallationSpec.PopulationPolity`, default the first controller); a capture by siege therefore makes the population occupied; under its own polity unrest decays. Suppressed by garrison per 1,000 civilians (20 per 1,000 suppresses fully, DERIVED); revolt at unrest 1. **Not built:** any consequence of a revolt beyond a flag, a record and a decision-window event (§10, §11); the grievance is an input, not derived from culture or treatment. | `Occupation_…` (occupied rig; revolt instant pinned), `AnOwnCity_IsNeverRestive_…`, `Capture_MakesThePopulationOccupied_…` [r1]; **[r2]** `ACapture_InsideATheatrePulse_CountsOnlyTheOccupiedPartOfThatPulse` (S2-V2), `ALiberation_CalmsTheUnrest_EvenWithNoGarrisonToPoliceIt` (S2-V1 R2-M2) |
| Investment | no garrison entry, withdrawal or reinforcement order while ANY siege is active, with or without an open corridor (a corridor's reinforcement and evacuation are the siege's own flows; **[r1] S-V9** corrected the refusal code's doc, which said "with no open corridor") | `AnInvestedInstallation_…`, `Invested_RefusesGarrisonOrders_EvenWithAnOpenCorridor` [r1] |
| Capture | storm or capitulation → prisoners; terms at the cap → march out; control passes through one path; the old controller's arrears are written off | `Capitulation_TakesTheGarrisonPrisoner_…`, `MaximumDuration_WeakerDefender_…`; **[r2]** `ACapture_WritesOffTheOldControllersArrears_…` (S2-V1 R2-M4) |
| Conservation of personnel | every move is a ledger transfer; People total exact | the garrison tests above and the conservation tests |

Test-input note: the rig's default `LogisticsRates` is lane E's PROPOSAL with the base desertion rate off, so garrison head counts stay
exact in tests that are not about desertion; base desertion has its own test with the full PROPOSAL.

## 4. Coefficients and provenance

Siege-specific coefficients live in `SiegeCoefficients` with labels; everything reused comes from lane E's `LogisticsRates` PROPOSAL
(labelled there). **DERIVED**: water need, minimum water ration, body-reserve time (30 d), starvation onset (½ ration) and maximum
(1.5/yr), thirst, the police ratio, thrower damage as ⅕ of a bombard, the missile unit; disease deaths at the background prevalence =
lane E's camp disease (the anchor). **FITTED on the fit sieges (2026-11-05; not refitted at the resume)**: bombard damage, wall values of
castle and bastion, hunger capability penalty, morale odds term, surrender health (0.2), mining saturation, relief containment, earth
damage per gun, the AI doctrine thresholds. **PLACEHOLDER**: everything else, including the three new ones — the siege share of a
combat day's munitions for small arms and fortress guns (100 ‰) and for siege guns in battery (1,000 ‰), and the lines' containment of
an invested garrison's deserters (750 ‰, DESIGN). None is a Director value.

## 5. D-079 Q14: which clock evaluates a siege — evidence and recommendation (PROPOSAL, not decided)

**[r1] Rewritten in fix r1 (2026-11-06).** The previous version claimed "the equations are dt-correct … so the clock is a resolution
choice" and "identical outcome distributions on the measured set". Both were false as measured: per-head losses were floored to
thousandths of a head per pulse (S-V1: on 60-second pulses nobody died of background disease or most fire), and the besiegers'
reinforcements went to their largest arm whenever a pulse brought fewer than one person per arm (found while verifying S-V1: on
60-second pulses every reinforcement joined the melee). The "identical distributions" were measured on a 1-hour battle pulse only, and
the "threshold caught at finer sampling" explanation of Sevastopol at 60 s (Lifted, day 617.8) was wrong: it came from these two defects
(S-V4). Both are fixed (§12); everything below is re-measured on the fixed tree (bench copy of commit `2e245cf`, Release, shared CPUs).
**[r2] S2-V7:** `2e245cf` was a WIP commit squashed into `de47827`; it is in no branch or bundle (only in my clone's reflog). Its source
equals `de47827` (`git diff 2e245cf de47827` touches only `SiegeFixR1Tests.cs` and this report), the round-2 verifier reproduced these
measurements on `de47827`, and fix r2 re-measured them on its own tree (§13.4: identical except one 1-hour maximum, attributed).

Both clocks are implemented (`SiegeClock.WarPulse`, `SiegeClock.Battle(ms)`): rates are per sim-year; nutrition, disease, morale, walls
and works use stable implicit steps; head losses are lane E's survival product with exact carries; linear flows (food, water, ammunition,
corridor, supply, pay, reinforcements, evacuation) are integrated exactly with carries and split before every change of the people they
depend on.

**Convergence as the pulse shrinks (Sevastopol, harness seed; `siegetrace sevastopol <ms>`):**

| Pulse | War Pulse | 1 h | 30 min | 10 min | 5 min | 2 min | 60 s | 30 s |
|---|---|---|---|---|---|---|---|---|
| Outcome, day | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 | CapLifted 1,080 |
| Besieger health at the cap | 0.256 | 0.255 | 0.255 | 0.255 | 0.255 | 0.255 | 0.255 | 0.255 |
| Wall time (one run) | 0.14 s | 0.68 s | 0.88 s | 1.9 s | 3.5 s | 7.7 s | 15.2 s | 29.8 s |

Before the fix the same sweep gave Lifted at day 666.6 / 660.8 / 642.7 / 617.8 / 580.3 for 10 min / 5 min / 2 min / 60 s / 30 s
(verifier's measurement, `verify-S-r1/sevastopol-dt-sweep.txt`): no convergence. A diagnostic probe (internal state by reflection, not
kept as a test) located the second defect: with only the head-loss carry fixed, the besiegers' strength by arm at day 140 was melee
73,860 / ranged 46,043 on 1-hour pulses but 86,063 / 33,867 on 10-minute and 60-second pulses (same total), because each pulse's
reinforcement remainder went to the melee; after the per-arm carries the sweep above ends identically from 1 hour down. The rig siege
loses exactly the same people on War Pulses, 1-hour and 60-second pulses over 60 days (205 disease deaths, 22 killed, 8,904 left —
`HeadLosses_AreTheSameOnEveryClock_…`; before the fix: 0 and 0 at 60 s).

**Outcome distributions, 20 seeds (seeds 1-20, ProRata; `siegeseeds 20 <ms>`), all three clocks:**

| Siege | Outcome counts (equal on War Pulse, 1 h and 60 s at 20 seeds; [r2] see the note below) | Median days war / 1 h / 60 s | Max days war / 1 h / 60 s |
|---|---|---|---|
| Alesia | Capitulated 14, Relieved 6 | 67 / 45 / 45 | 67 / 45 / 45 |
| Constantinople | Stormed 20 | 46 / 44 / 45 (**[r2]** 44 at 60 s) | 60 / 80 / 66 (**[r2]** 58 at 60 s) |
| Vienna | Relieved 16, Capitulated 4 | 60 / 59 / 59 | 95 / 84 / 84 (**[r2]** 95 / 83 / 83) |
| Vicksburg | Capitulated 20 | 87 / 81 / 81 | 87 / 81 / 81 |
| Sevastopol | CapLifted 20 | 1,080 / 1,080 / 1,080 | 1,080 / 1,080 / 1,080 |
| Masada | Stormed 20 | 95 / 93 / 93 | 123 / 100 / 100 |
| Malta | Capitulated 18, Relieved 2 | 123 / 112 / 112 | 123 / 112 / 112 |
| Rhodes | Stormed 20 | 25 / 19 / 19 | 32 / 33 / 26 |
| Leningrad | Capitulated 20 | 109 / 106 / 106 | 109 / 106 / 106 |

The outcome counts are equal on all nine sieges on all three clocks (measured, 20 seeds each; the 60-second battery took 417 s on
shared CPUs, the 1-hour 8.3 s, the War Pulse 0.6 s). Before the fix, Sevastopol at 60 s was Lifted in all 20 seeds (S-V4).

**[r2] S2-V3 — equal counts, not identical outcomes.** The equal 20-seed counts are a coincidence of the sample. Seed by seed the relief
sieges differ between War Pulses and 1-hour pulses (measured, seeds 1-20): Alesia relieved in seeds {1, 10, 11, 14, 16, 17} against
{3, 4, 12, 13, 14, 20} (only seed 14 in common); Vienna capitulated in {1, 12, 15, 19} against {2, 3, 6, 12}; Malta relieved in {7, 11}
against {4, 12} — the relief battle falls on a different day (39 vs 38, 60 vs 59, 116 vs 112) and its fortune is keyed on its instant.
Over seeds 1-200 the counts differ: Alesia relieved 70 vs 76, Vienna 157 vs 154, Malta 18 vs 26 (measured; a two-proportion z of
about 0.6, 0.4 and 1.3, inside sampling noise — inferred, and the seeds are paired, which this test ignores). The evidence supports
"indistinguishable at these sample sizes", not "identical". (Verifier's probes `VerifyR2Probes.P2`, `P2b`, reproduced byte for byte in
my clone on `de47827` and again on the fix r2 tree.)

**Cost, single seed (harness seed), 60-second battle pulse (`siege 60000`):**

| Siege | Evaluations war | Evaluations 60 s | Ratio | Wall ms 60 s | Outcome war / 60 s | Days war / 60 s |
|---|---|---|---|---|---|---|
| Alesia | 3 | 64,616 | 21,539× | 716 | Relieved / Capitulated | 39 / 44.9 |
| Constantinople | 7 | 84,245 | 12,035× | 664 | Stormed / Stormed | 46 / 58.5 |
| Vienna | 9 | 84,960 | 9,440× | 865 | Relieved / Relieved | 60 / 59 |
| Vicksburg | 29 | 116,456 | 4,016× | 1,161 | Capitulated / Capitulated | 87 / 80.9 |
| Sevastopol | 360 | 1,555,200 | 4,320× | 14,896 | CapLifted / CapLifted | 1,080 / 1,080 |
| Masada | 7 | 133,701 | 19,100× | 916 | Stormed / Stormed | 95 / 92.9 |
| Malta | 18 | 161,281 | 8,960× | 1,385 | Capitulated / Capitulated | 123 / 112 |
| Rhodes | 4 | 37,597 | 9,399× | 347 | Stormed / Stormed | 25 / 26.1 |
| Leningrad | 55 | 152,133 | 2,766× | 1,440 | Capitulated / Capitulated | 109 / 105.6 |

**[r2]** Re-measured on the fix r2 tree (§13.4): Alesia 64,643 evaluations at 60 s (21,548×), 44.9 days; Constantinople Stormed at day
44.5 after 64,055 evaluations (9,151×; was 58.5 days / 84,245: this seed's assault realisation moved with the siege-start desertion
split, attributed); Leningrad 152,129; the other six rows' evaluations, outcomes and days unchanged; wall times 278-14,047 ms (shared
CPUs). The ratio range below becomes 2,766-21,548×.

War Pulse wall times are 0-8 ms per siege. Throughput on the 60-second clock: 1,555,200 evaluations in 14.9 s ≈ 104,000 per second
(one run). **Inferred, not measured:** a siege that runs to its cap in the Neolithic band (3 × 10-year turns) on 60-second pulses is
~15.6 million pulses, ≈ 150 s per siege at that throughput.

**What still differs between the clocks (measured):** (1) state-triggered endings (surrender, collapse, starvation) are detected at pulse
ends, so War Pulse durations run up to one pulse longer than the battle clock's (Alesia 67 vs 45 days is one 21-day Antiquity pulse
plus detection; Vicksburg 87 vs 81; Malta 123 vs 112); (2) battles happen at pulse starts and their fortune is keyed on the instant, so
a single seed's realisation moves with the clock (Alesia's harness seed: Relieved on War Pulses, Capitulated at 60 s; Constantinople
46 vs 58.5 days) while the outcome counts over 20 seeds coincide **[r2]** (and over 200 seeds differ within sampling noise, S2-V3);
(3) the first-order discretisation of the implicit steps: Sevastopol's
besieger health at the cap is 0.256 on War Pulses and 0.255 on every battle pulse from 1 hour to 30 seconds; the Constantinople
trajectory test (`ClockParameter_…`) holds wall and health within 0.03 and the garrison within 2 % between War Pulses and 1-hour pulses
at every common instant (passes on the fixed tree). **[r2]** No difference between the clocks' outcome distributions is detectable on
the measured set (20 seeds × nine sieges × three clocks; 200 seeds × three relief sieges × two clocks); that does not show there is none.

**Engineering recommendation (labelled; the Director decides):** reading (b) — evaluate siege health on the **Operational War Pulse**
of the besieging war; tactical battle pulses only inside the battles a siege spawns (assault, relief, sally). Reasons, all measured
on the fixed tree: **[r2]** outcome distributions indistinguishable at the measured sample sizes (above); convergence of the
60-second and finer clocks to the same trajectory; durations within one War Pulse plus detection; 2,766-21,539× fewer evaluations.
Its price **[r2]** (S2-V3: completed): (1) an ending is detected up to one War Pulse late; (2) every discrete action is held to a War
Pulse start: a relief fights at the first siege pulse start at or after its arrival, and every siege order (ration, assault, sally,
lift) takes effect at the first siege pulse start at or after its issue (WA1; pinned at B − 1, B, B + 1), so on War Pulses an order or a
relief can wait up to one whole pulse — 21 days in Antiquity, 1 day in the shortest band of T2's proposed table (inferred from the
delivery rule and the table; the relief days above show the effect). If the Director wants surrender and lifting decided at the instant
the threshold is crossed, the middle way is War Pulse evaluation plus an event at the exact crossing instant (not built). Reading (a)
is supported by the code as it stands; the cost table is its price.

## 6. Calibration against history (model vs history)

Inputs are in `SiegeScenarios.cs` (standard accounts; large uncertainty — garrison and army sizes, store days, durations; Caesar's and
Josephus's figures are likely inflated), now in lane E units (food in gm from "N days for these soldiers and civilians", munitions in kg
from "N days of siege fire") with each side's capability as a test input standing for computed state: Alesia and Masada Coinage; the
gunpowder sieges Coinage and Gunpowder; Vicksburg's Union and both sides at Leningrad also Sanitation; Sevastopol 1854 neither side
Sanitation. Historical durations: Alesia ≈ 40-60 d; Masada 50-120 d; Constantinople 53 d; Vienna 60 d; Vicksburg 47 d; Sevastopol
327-349 d; Malta 116 d; Rhodes ≈ 145-180 d; Leningrad 872 d.

### 6.1 War Pulse clock (T2 proposed lengths), ProRata, seeds 1-20 (final model)

| Siege | Set | History (outcome, days) | Model: matches / 20 | Model outcomes | Model days (min / median / max) |
|---|---|---|---|---|---|
| Alesia (52 BC) | fit | surrender after the relief failed, 40-60 | **14/20** | Capitulated 14, Relieved 6 | 39 / 67 / 67 |
| Constantinople (1453) | fit | stormed, 53 | **20/20** | Stormed 20 | 46 / 46 / 60 |
| Vienna (1683) | fit | relieved, 60 | **16/20** | Relieved 16, Capitulated 4 | 60 / 60 / 95 |
| Vicksburg (1863) | fit | surrender (starvation), 47 | **20/20** (outcome) | Capitulated 20 | 87 / 87 / 87 — **too long** |
| Sevastopol (1854-55) | fit | stormed (Malakoff), 327-349 | **0/20** | CapLifted 20 | 1,080 |
| Masada (73/74) | holdout | stormed after the ramp, 50-120 | **20/20** | Stormed 20 | 95 / 95 / 123 |
| Malta (1565) | holdout | relieved / withdrawal, 112-116 | **2/20** | Capitulated 18, Relieved 2 | 116 / 123 / 123 |
| Rhodes (1522) | holdout | surrender on terms, 145-180 | **0/20** | Stormed 20 | 25 / 25 / 32 |
| Leningrad (1941-44) | holdout | held, lifted, 872 (cap 540 in the model) | **0/20** | Capitulated 20 | 109 |

Fit set: 70/100 seed-runs; holdout 22/80 — the same match counts as the 2026-11-05 model. **This is not a good fit, and it is reported
as measured.** No coefficient was refitted at the resume.

### 6.2 What the lane E reuse changed (harness seed, measured)

Six of nine pinned records are unchanged. Vicksburg Capitulated 90 → 87 days; Leningrad Capitulated 133 → 109; Sevastopol (Lifted, 384)
→ (CapLifted, 1,080). Loss breakdown (both camps per sink; War Pulse, ProRata, harness seed):

| Siege | Outcome, days | Garrison / civilians / besiegers at end | Starved | Disease | Killed | Deserted |
|---|---|---|---|---|---|---|
| Alesia | Relieved, 39 | 67,081 / 0 / 58,959 | 0 | 1,866 | 14,337 | 2,216 |
| Constantinople | Stormed, 46 | 6,479 / 44,657 / 67,238 | 0 | 930 | 2,630 | 1,066 |
| Vienna | Relieved, 60 | 14,961 / 49,267 / 82,488 | 0 | 2,096 | 12,708 | 2,762 |
| Vicksburg | Capitulated, 87 | 23,830 / 3,435 / 56,315 | 1,009 | 3,939 | 2,273 | 5,199 |
| Sevastopol | CapLifted, 1,080 | 60,000 / 10,767 / 113,172 | 0 | 225,315 | 36,912 | 121,397 |
| Masada | Stormed, 95 | 325 / 591 / 7,359 | 0 | 136 | 328 | 221 |
| Malta | Capitulated, 123 | 5,636 / 7,822 / 30,386 | 0 | 1,415 | 2,958 | 1,973 |
| Rhodes | Stormed, 25 | 5,250 / 5,976 / 86,490 | 0 | 488 | 3,157 | 539 |
| Leningrad | Capitulated, 109 | 201,348 / 2,270,628 / 261,799 | 187,920 | 80,870 | 32,180 | 59,699 |

[r1] Re-measured on the fixed tree (`siege 60000`, War Pulse rows): Vicksburg and Sevastopol moved by one to 28 people (the exact
head-loss carry and the per-arm reinforcement split); the other seven rows, every outcome and every duration are unchanged.
**[r2]** Re-measured on the fix r2 tree: every outcome and duration unchanged; garrison end and deserters moved by 0-32 people in six
rows (e.g. Alesia garrison 67,081 → 67,054, deserted 2,216 → 2,244; Leningrad 201,348 → 201,317, 59,699 → 59,731) because the part of
the theatre pulse before each siege start now deserts (S2-V2, §13.4; with the split reverted the table is fix r1's); Vicksburg,
Sevastopol and Masada unchanged.

### 6.3 Sensitivity: the lines' containment of deserters (measured, 20 seeds, War Pulse)

Without containment (lane E's rate applied as for a field army) the match counts are identical (70/100, 22/80); medians with
containment vs without: Alesia 67 vs 53, Vicksburg 87 vs 84, Leningrad 109 vs 103; the other six unchanged. Containment was kept
because it is the mechanism (an invested garrison's deserters must cross the lines), not because of the fit, which it does not improve.

### 6.4 Diagnosis of each miss (model limitations, not code defects; each traced)

- **Vicksburg (87 vs 47 d):** a smooth decline (at day 48: nutrition 0.52, 12 % sick, health 0.47); the historical garrison broke while
  food remained (exhaustion, half the men sick or wounded, no relief in prospect). Store inputs are uncertain.
- **Sevastopol (CapLifted 1,080 vs Stormed 327-349):** never invested (corridor), reinforced both ways; earthworks repaired against 600
  guns keep the field-works term at 0.6-0.8, so the assault odds never reach the doctrine's 1.3; the besiegers' health stays at
  0.21-0.34 after day 200, above their lift threshold, so the cap adjudicates (the 2026-11-05 model had them lift at day 384). Missing:
  the growth of the Allied siege train (126 guns → 800+) and the saps that brought the assault to 25 m of the Malakoff.
- **Rhodes (Stormed day 25 vs surrender ~day 160):** 10,000 Ottoman miners against 100 counter-miners breach the bastions in weeks;
  historically Tadini's listening and countermining defeated most mines and repeated assaults failed for months.
- **Leningrad (Capitulated day 109 vs held):** one ration level for everyone (historical ration categories fed soldiers 2-4× the
  dependents); lane E's hunger desertion and disease mortality act on the starving garrison; nothing models a besieger who refuses
  surrender. The doctrine now rations against the net draw, but the Ladoga corridor (30 % of need from day 75) does not cover the need.
- **Malta:** Birgu capitulates about when the relief lands (123 vs relief at 112).
- **Alesia (6/20 relieved):** the relief battle succeeds in 6 seeds; historically it failed.

Holdout contamination (carried from 2026-11-05): holdout traces were looked at while mechanisms were fixed then; no coefficient was
fitted on them after the freeze. At the resume the doctrine's net-inflow rule was written from the logic of a commander who knows his
inflow (a flaw first seen on Leningrad's trace on 2026-11-05); it changes no calibration outcome (measured).

## 7. Tests (measured in this clone)

88 lane S tests in `Proto.M7.Tests/Siege/` (as xUnit counts them): equations 13, world 34, garrisons 19, codec and calibration 12, lane E
reuse 10. **[r1]** Now 119: + 32 in `SiegeFixR1Tests.cs`, − 1 removed (the reinforcement-remainder tie-break test, whose rule is gone;
§12.5). They cover: conservation (exact, every commodity, every turn snapshot and the end, four scenarios); the rationing trade-off
(equation level, strict, and in a running siege); attack monotonicity; ranged-to-melee saturation; the gunpowder breach and the
bastion; munitions met for gunpowder engines and garrison firearms; Law 4 metamorphic (date vs capability) and the Gunpowder capability
refusal; stability and convergence; loss rates bounded and integrated within the heads; lane E's disease anchor, Sanitation, desertion
(morale, arrears age, hunger), pay in coin or kind, the arrears clock, base desertion, the lines' containment; the supply line equal to
lane E's `Evaluate`, carriage loss to the gm, road vs trackless hills, train validation; the cap pin (±1 ms across a band edge, both
clocks, turn pinned, truncated last pulse); the cap rule and surrender on terms; order delivery pins at B−1, B, B+1 for ration, assault
and lift (siege pulses) and assign, withdraw and raid (theatre pulses); refusals; the turn-start split; no siege-only stock; the
shortfall policy; the doctrine's odds threshold, lift threshold and net-inflow rationing; replay goldens on both clocks (live with stops
and orders = replay; teeth: no orders → different state); tie-dense coincident sieges under four declaration orders [r2: the
reinforcement remainder's tie-break test listed here was removed in fix r1, §12.5]; POPULATED-table round trips (**[r2]** rows version 4:
exact lengths 223/99/1,292, bit-exact, hash equality,
truncation and version refusals; the populated world has a supply line with shipments, arrears with their clock, missiles and munitions
drawn, and non-zero per-pulse accumulators); pending orders in the row; the calibration record pinned; the clock trajectory at common
instants.

| Configuration | `9503d2b` (dev) | Merged (`dd29664`) | Final |
|---|---|---|---|
| M7Proto.slnx Release | 856 passed, 0 failed, 0 skipped | 931 / 0 / 0 | 944 / 0 / 0 (856 + 88); **[r1] 975 / 0 / 0 (856 + 119)**; **[r2] 992 / 0 / 0 (856 + 136)** |
| M7Proto.slnx Debug | 856 / 0 / 0 | 931 / 0 / 0 | 944 / 0 / 0; **[r1] 975 / 0 / 0**; **[r2] 992 / 0 / 0** |
| M7Econ.slnx, M7DetMath.slnx | not run: no project in them references Proto.M7 (checked) | — | — |

Builds: Debug and Release, 0 warnings, 0 errors (`TreatWarningsAsErrors`). Determinism scan (`scripts/check-proto-determinism.sh`):
CLEAN, 87 files.

Moved pins (attributed): the two replay goldens twice (merge: commodity byte only, measured; reuse: every flow changed by design); the
calibration record for Vicksburg, Sevastopol and Leningrad (§6.2); the siege row length 1,058 → 1,259 and the installation row 203 → 215
(row version 2); `Upkeep_IsExactPerTheatrePulse_…` now reads lane E's PayFoot (the same value, 12).

## 8. Mutants (resume battery)

Each mutant changed one source expression; the test project was rebuilt (Release) and the lane S suite (`FullyQualifiedName~Siege`, 88
tests) run once, bounded at **53 s = 10 × the measured clean cycle (5.3 s: rebuild + lane S suite)**. A failed build would be recorded,
never counted as a kill (none failed). Every source file was restored by copy and verified by SHA-256 after each run. **22 of 22
killed; none hung (slowest test run 2.7 s).** Every kill includes at least one semantic test; the goldens and the calibration pins also
fail where they apply but are never the only kill. Runner and output: `scratchpad/m7c/S-work/mutants-resume.py`, `mutants-resume-out.txt`.

| Mutant | Change | Failed | Semantic kills |
|---|---|---|---|
| M1 | rationing does not reduce the garrison's consumption | 4 | `Rationing_LowersSiegeHealthEarly_…` |
| M2 | cap 1 ms late | 9 | cap pins at edge −1, 0, +1; battle-clock cap; surrender on terms; conservation |
| M3 | cap keyed on the next turn's length | 1 | cap pin at the band edge |
| M4 | bombards damage like throwers | 2 | `Gunpowder_BreachesMedievalWalls_…` |
| M5 | strict siege-order latch (an order at B waits) | 9 | ration, assault and lift pins; the seam test; garrison-defence assault |
| M6 | garrison capacity not enforced | 2 | `IllegalGarrisonOrders_…`; `Replace_OverCapacity_…` |
| M7 | captured garrison not taken prisoner | 2 | `Capitulation_TakesTheGarrisonPrisoner_…`; `AssaultReliefAndSally_…` |
| M8 | GarrisonFirst falls to pro rata | 1 | `ShortfallPolicy_DecidesWhoStarves…` |
| M9 | Sanitation ignored in disease deaths | 3 | `DiseaseDeaths_…`; `Sanitation_CutsDiseaseDeaths_…` |
| M10 | peacetime desertion ignores pay arrears | 1 | `UnpaidPay_RunsUpArrears_…` |
| M11 | pay ignores Coinage | 1 | `Pay_IsCoinWithCoinage_AndGrainWithout_…` |
| M12 | head-loss carries dropped | 4 | the clock-trajectory test (hourly losses below one head vanish); populated table |
| M13 | supply-line carriage loss ignored | 2 | `SupplyLine_IsLaneE_Evaluate_…` |
| M14 | the lines do not contain deserters | 5 | `InvestedGarrison_Desertion_…` |
| M15 | besiegers' Gunpowder capability not checked | 1 | `GunsAndFirearms_NeedTheGunpowderCapability_…` |
| M16 | gunpowder engines breach without munitions | 2 | `GunpowderEngines_BreachOnlyWithMunitions_…`; `Bombards_BreachOnlyWhileThePowderLasts_…` |
| M17 | garrison firearms shoot missiles | 7 | `GarrisonRifles_FireLaneE_Munitions_…`; `AirDefence_IsCountedOnce_…` |
| M18 | doctrine ignores corridor inflow | 1 | `Doctrine_RationsAgainstTheNetDraw_…` |
| M19 | the cap never yields surrender on terms | 2 | `CapRule_…`; `MaximumDuration_WeakerDefender_…` |
| M20 | approach works do not gate the assault | 2 | `ApproachWorks_GateTheAssault_…` |
| M21 | lane E scale conversion by floor | 3 | `Desertion_IsLaneE_Rate_…` |
| M22 | disease deaths not scaled by prevalence | 4 | `DiseaseDeaths_AtTheBackgroundPrevalence_AreLaneE_…` |

Process note (measured): M16's first anchor matched two identical lines (wall and earth damage) and the runner refused it
(PATTERN-NOT-UNIQUE) instead of mutating either; it was re-anchored to the wall-damage function and run alone. Each property killed is
one the system ought to have (ADR-015 §7.2).

## 9. Bench

`dotnet Proto.M7.Bench.dll siege 60000` (Release): the §5 cost table plus the three shortfall policies on the War Pulse clock (identical
outcomes, durations and losses for all nine sieges under ProRata, CiviliansFirst and GarrisonFirst — measured) and the §6.2 loss
breakdown, in 16 s for the whole table. `siegeseeds 20 0` and `siegeseeds 20 3600000` give the §5 and §6.1 batteries (the 1-hour battery
in 8 s). `siegetrace <key> [ms]` prints the per-pulse trace (food in gm, munitions in kg, missiles). Lane E's `wareconE` bench output is
unchanged (§0).

## 10. For the Director (proposals and questions; nothing here is decided)

1. **Q14 (clock):** recommendation (b) War Pulse, evidence §5 as re-measured in fix r1 (§12.4). [r1] The earlier "threshold caveat
   (Sevastopol at 60 s)" was mostly the head-loss truncation defect S-V1 and is withdrawn; see §5 for what remains.
2. **The maximum-duration rule:** at three campaign-turn equivalents the weaker side yields; a defender yields on terms; a tie keeps the
   city. Alternatives: always lifted at the cap; always a final assault. Please confirm.
3. **Shortfall policy inside a siege (food interpretation):** both D-079 readings mean civilians first inside a siege, unlike historical
   practice. Not chosen; parameter and measured difference in §2.6.
4. **Surrender acceptance:** whether a besieger can refuse surrender (Leningrad). Not built.
5. **RDG-2** is used for the cap's measure (the start turn's length, fixed at the start).
6. **New — ration categories:** one ration level for garrison and civilians is DIR5's lever; historical sieges fed soldiers more
   (Leningrad). A second order (garrison ration vs civilian ration) would be a defender's choice, close to but distinct from the
   unchosen shortfall policy. Not built.
7. **New — lane E's field-army rates inside a siege:** desertion and hunger penalties apply to besieged garrisons through lane E's
   formula, contained by the lines (my design, PLACEHOLDER 750 ‰); garrisons also pay lane E's base desertion in peacetime. Please
   confirm or direct otherwise.
8. **New — the gunpowder-era siege needs siege-train growth and saps** (Sevastopol, Vicksburg): an ordnance reinforcement flow and
   approach trenches that reduce the field-works term at the point of assault. Proposal; not built.
9. **[r1] A human ration order is a standing order** (S-V6): once a defender's ration order has taken effect, an AI doctrine on that
   side no longer sets the ration for the rest of the siege (assault and lift precedence is per pending order, as before). This is my
   reading of "a human order takes precedence" for a standing level; the alternative (precedence for the latching pulse only) would make
   a ration order last one pulse. Please confirm.
10. **[r1] Occupation** (S-V7): grievance accrues only while the installation is held by a polity other than its population's own, so a
    capture makes a population restive and a liberation calms it. Open: what a revolt does (now a flag, a record and a decision-window
    event — no loss of control, no fighting) and whether grievance should derive from state (culture, treatment, war) rather than be an
    input. Not decided here.
11. **[r1] A beaten relief** (S-V8): the immediate shock and the lasting loss of hope are now separate coefficients (both 400 ‰,
    PLACEHOLDER). The lasting term is a term of the morale-target equation keyed on the computed state "a relief was defeated" — my
    reading of Law 2 is that this is a mechanism, not a free-floating modifier — but the verifier called it borderline. Options: keep;
    drop the lasting term (Alesia, Vienna and Malta would need re-fitting); or replace it with a "relief expected" term that raises the
    target while a relief is on its way. Please rule.
12. **[r2] Pay before siege flows on a shared home granary** (S2-V6): when a polity's home granary is short, wages in kind are fed
    before a corridor into a besieged city and before its besiegers' supply, at every settlement. This makes the priority the same on
    every clock (before fix r2 it depended on which event settled), but it is my choice, not a ruling. The split inside the settlement
    interval in which the granary runs dry still depends on the interval's length (bounded by one interval's flows); the dt-correct
    alternative is to share a short stock between all its flows pro rata to their dues within each interval, which reproduces the
    continuous-time result for constant rates (proposal, not built: it needs pay and siege flows settled over the same intervals).
13. **[r2] Q14 price, completed** (S2-V3): on War Pulses orders and reliefs wait for the next pulse start (up to 21 days in Antiquity),
    besides endings being detected up to one pulse late. The recommendation (b) stands as a proposal; the price is now stated in full.

## 11. Limitations (honest list)

- The assault, relief, sally and raid resolver is a formation-level **stand-in**, not the land module.
- No fog in the siege world (observers, recipients); any participant may order at any window (sides are checked).
- Garrison orders in peacetime use the theatre clock (no war → no theatre pulses); peacetime turn-start delivery is not built.
- Siege rows round-trip, but resuming a world mid-siege from rows (rebuilding the scheduler queue) is not built.
- Machines are Ordnance pieces; their crews are inside the arms; no siege-train growth.
- Besiegers' pay is not modelled (lane E's army economy owns it); garrison back pay is not built (no revenue in this world).
- The besiegers do not forage or requisition; munitions reach their park without carriage limit (lane E's rule).
- Lane E's coefficients are its PROPOSAL; applying field-army desertion and hunger rates to besieged garrisons is a modelling choice (§10.7).
- The disease and desertion sinks are shared by both camps (per-camp totals are not recorded separately).
- Calibration: one parameter set, nine sieges, four persistent misses (§6.4); no seasonal effects; no naval blockade.
- The AI doctrine is a calibration controller (ration thresholds, assault odds, lift health), not campaign AI.
- [r1] A revolt has no automatic consequence (flag, record, decision-window event only); the grievance is an input (§10.10).
- [r1] Raids have no serialized row (their state is hashed directly since S-V12; resuming from rows is not built anyway).
- [r1] Remaining per-pulse integer rounding, measured or bounded: the besiegers' supply line ships lane E's `ArmyLogistics.Gross` (a
  ceiling) each settlement, so it over-ships by at most one gm per settlement into the carriage-loss sink (inferred bound; about 0.3 % of
  the shipment on 60-second pulses, never a shortage, since delivered ≥ need); `FireShare` (counter-battery against machines) floors its
  per-pulse share to Q32 (relative error below 0.1 % per pulse at 60 s, inferred). Neither is carried; both are below the measured
  clock differences of §5.
- [r1] The besiegers' reinforcement split leaves fewer than four people per siege waiting in the home stock between pulses (exact per-arm
  carries); if another siege of the same polity has drained that stock, an arm gets what is there and the rest of its share is dropped,
  as the inflow itself is when the home stock is empty.
- [r2] Theatre desertion and unrest are split at siege boundaries and captures only; other changes of the garrison or civilians
  inside a theatre pulse (none happen outside a siege in this world: garrison orders and raids act at theatre pulse starts) would be
  charged at the end-of-span state, as before. The unrest step is implicit (first order), so a split changes it slightly by itself.
- [r2] A revolt crossed in the span settled at a siege's end raises its decision-window event only where the world has the scheduler
  (pulse ends and starts); after an order executed at once (an immediate lift) the record is kept but no event is raised, as for
  SiegeEnded there.
- [r2] The pulse digest in the state hash is a 64-bit SplitMix64 fold (CounterRng.Mix), a replay-comparison digest, not a
  cryptographic one; resuming a world from rows would need it in a row (resuming is not built).
- [r1] Lane E's own army economy uses `HeadcountLoss.LossMilli` with the same thousandth-of-a-head floor per pulse on War Pulses
  (1-21 days); at those lengths the floor is below one thousandth of a head per account and flow per pulse. Not changed (lane E's
  code); recorded for lane E in case its pulses ever shorten.

## 12. Verification round 1 (fix r1, 2026-11-06)

Clone `/dev/shm/m7c/S`, branch `lane/S`, starting at `be09e5c` (on merge `dd29664` of dev `9503d2b`); the verifier's twelve findings
(S-V1 … S-V12, `scratchpad/m7c/verify-S-r1`). Every finding was reproduced on `be09e5c` before anything changed: the verifier's probes
(`VerifySProbeTests.cs`) copied into a temporary test file of my own with two more probes (R7 occupation, R12 raid hash), and the
verifier's 16 mutants re-run in my clone with my own runner (bound 45 s = 10 × the 4.2 s clean cycle then; restore from an in-memory
copy, verified by SHA-256). The temporary probe file was deleted before the commit. Baseline on `be09e5c`: M7Proto.slnx Debug 944/0/0,
Release 944/0/0 (measured). Evidence files: `scratchpad/m7c/S-fix-r1/`.

### 12.1 Verdicts

| ID | Sev. | Verdict | Reproduced on `be09e5c` (measured) | Fix | Semantic test(s) |
|---|---|---|---|---|---|
| S-V1 | blocker | **REPRODUCED** | `LossMilli` for 2,000 men at 60 ‰/yr on a 60-s pulse = 0 (1 h: 13, 1 d: 333). Rig siege, 60 days: War Pulse 205 disease deaths / 22 killed, 1 h 198 / 18, 60 s 0 / 0 (garrison + civilians exactly 9,000). | Head-loss carries in 10⁻¹⁸ head: `SiegeEquations.LossAtto` on lane E's `HeadcountLoss.Survival`, in `LoseAtRates` and `LoseAtRate` (theatre desertion). Lane E's code unchanged. Plus §12.2. | `LossAtto_IsIndependentOfThePulseLength_…_TwoSided` (the two-sided bound the verifier asked for); `HeadLosses_AreTheSameOnEveryClock_…` (205 / 22 / 8,904 on all three clocks); `TheatreDesertion_OfASmallGarrison_IsNotFlooredAway_…` |
| S-V2 | major | **REPRODUCED** | P1b (daily battle pulses, assault halves the garrison at B1 + 2 d): paid 350, exact integral 449. P1c (War Pulse split at the corridor's opening): 349 vs 449. | `SettleTheatre(now)` before `Evaluate` at every siege pulse end, and at the top of `Assault` and `Sally`. | `GarrisonPay_IsTheIntegralOfTheHeadCount_…` (battle clock 450 vs 449; War Pulse + corridor 449 = 449), `GarrisonPay_IsSettledBeforeAnAssaultOrASally…` (450 = 450; 616 = 616), `GarrisonPay_FollowsTheCorridorsReinforcementsPulseByPulse` (1,073 vs 1,072) |
| S-V3 | major | **REPRODUCED** | The verifier's 16 mutants in my clone: 2 killed by a semantic test (V9, V14), 10 only by the replay golden or the calibration record, 4 survived with 88/88 passing (V5, V8, V11, V12) — the verifier's result exactly. | 14 semantic tests (13 for these items, one for V8 / S-V5) for the uncovered items: disease with crowding (V2) and starvation (V3), relief containment (V4), sally effects (V5), mining and counter-mining (V6), rationing lowers morale (V1), breach (V15), repair labour (V7), water minimum (V11), cut water (V12), failed-relief hope (V10), assault losses by arm (V13), evacuation through the corridor (V16), arrears interpolation (V8, S-V5). | §12.3: all 16 killed, each by at least one semantic test |
| S-V4 | minor | **REPRODUCED** (report text) | §5 said "Outcome distributions, 20 seeds (War Pulse vs 1-hour battle pulse)" and gave "identical outcome distributions" as a reason, while its own cost table showed Sevastopol Lifted at day 617.8 on 60 s (read); the 60-s battery was the verifier's (Lifted 20/20). | §5 rewritten from measurements on the fixed tree, including the 60-s 20-seed battery and the pulse-length sweep. | — (measurement; the sweep and batteries are in §5) |
| S-V5 | minor | **REPRODUCED** | Mutant V8 (arrears clock not interpolated) survived, 88/88; the only test had an empty treasury from the first instant. | Test only (the code was right). | `Arrears_StartAtTheInterpolatedInstantThePaySourceRanDry` (money for half the first 700 → WarStart + 3.5 d; kills V8) |
| S-V6 | minor | **REPRODUCED** | P2: the latch says value 500 effective at B2; the ration in force after B2 is 1,000 (the doctrine overwrote it). | A human ration order that takes effect is a standing order: `RationHeld` (siege row v3); the doctrine no longer sets that siege's ration. Semantics flagged for the Director (§10.9). | `AHumanRationOrder_IsAStandingOrder_…` (500 in force from B2 to B2 + 30 d; control without the order: 1,000; kills F6) |
| S-V7 | minor | **REPRODUCED** | Probe R7: polity 0's own city with grievance 2,000 ‰/yr and no garrison revolts (1 revolt; controller unchanged). Code: no controller or capture term; a revolt sets a flag and a record only. | Grievance accrues only while OCCUPIED (controller ≠ the population's polity, new `InstallationSpec.PopulationPolity`, default the first controller); under its own polity unrest decays; so a capture makes the population restive. A revolt's consequence is **not built** — a question for the Director (§10.10), stated in §3 and §11. | `AnOwnCity_IsNeverRestive_…`; `Capture_MakesThePopulationOccupied_…` (captured day 75, revolt 26 theatre pulses later, day 257); `Occupation_…` re-rigged to an occupied city (pinned instant unchanged) |
| S-V8 | minor | **REPRODUCED** (facts); Law-2 reading not accepted, referred | Read: −0.4 immediate shock plus × 0.6 on the target while `ReliefFailed`, one coefficient for both; mutant V10 killed only by the golden. | Kept as a term of the morale-target equation keyed on computed state; given its own coefficient `ReliefHopeLostPermille` (same value: nothing moved); the options are put to the Director (§10.11). | `AFailedRelief_LowersTheDefendersMorale_AndTheirHope…` (shock exact; morale ratio 0.599 ≈ 1 − 0.4 a hundred days later; kills V10) |
| S-V9 | nit | **REPRODUCED** | `SiegeRefusal.Invested` doc: "with no open corridor"; the code refuses on any active siege. | Doc corrected to the code (and to §3). | `Invested_RefusesGarrisonOrders_EvenWithAnOpenCorridor` |
| S-V10 | nit | **REPRODUCED** | The calibration "bands" test checks only that the cap binds for Leningrad alone; `EnduranceMs` has no world caller (grep). | Both tests renamed to what they assert; `EnduranceMs` documented as a closed-form helper; a world-level endurance test added. | `Rationing_TheGranaryLastsStrictlyLonger_InARunningSiege` (granary short at day 33 at full rations, 61 at half) |
| S-V11 | nit | **REPRODUCED** | The sally was pinned only at B (grep of the test files). | Pins at B − 1, B, B + 1. | `SallyOrder_IsResolvedAtTheFirstSiegePulseStartAtOrAfterIssue_…` (3 cases) |
| S-V12 | nit | **REPRODUCED** | Probe R12: a raid that arrived at B1 − 2 d (unresolved) and one arriving at B1 + 3 d give the same state hash at B1 − 1 d. | Raid count, id, accounts, Arrived and Resolved appended to `StateHashHex` (no new row type; resuming from rows is not built). | `AnArrivedRaid_IsInTheStateHash_BeforeItIsResolved` (and equal hashes for equal raid state; kills F9) |

No finding was refuted. One was partly referred: S-V8's facts reproduce and are now tested, but whether the lasting term breaks Law 2 is
a ruling I did not make (§10.11).

### 12.2 Found while verifying S-V1's fix (same class; fixed)

With the head-loss carry exact, Sevastopol's 60-second and 10-minute runs agreed with each other but not with the 1-hour run. A
diagnostic probe (internal state by reflection; not kept) showed the cause: `ReinforceAttacker` floored each pulse's reinforcement over
the arms and gave the remainder to the largest arm, so with fewer than one person per arm per pulse (60 s: 0.19 people per pulse) every
reinforcement joined the melee (day 140: melee 86,063 / ranged 33,867 at 60 s against 73,860 / 46,043 at 1 h; same total). Fix: exact
per-arm carries in units of 1/(opening total) (`SiegeS.ReinfArmCarry`, siege row v3); what the floors leave (fewer than four people)
waits in the home stock. Test: `AttackerReinforcements_AreSplitInTheOpeningProportions_OnEveryClock` (floor(N × p_a / P) exactly per
arm on War Pulses and on 60-second pulses). It replaces the tie-dense remainder test (§12.5).

### 12.3 Mutants (fix r1 battery)

Runner `scratchpad/m7c/S-fix-r1/mutants_r1.py`, list `r1_mutants.json`, output `mutants-r1-out.txt` and `mutants-r1-out-F3F10.txt`.
Each mutant: one exact, unique replacement; Release rebuild of the test project; the lane S suite run once, bounded at **57 s = 10 × the
5.7 s clean cycle** (build + suite, measured); restore from an in-memory copy verified by SHA-256. None hung (slowest suite run 5.2 s);
no build failed. **26 of 26 killed, each by at least one semantic test** (goldens and the calibration record also fail where they apply,
never alone).

| Mutant | Change | Semantic kill(s) |
|---|---|---|
| V1 | hunger term out of the defender's morale target | `Rationing_LowersTheDefendersMorale` |
| V2 | disease crowding fixed at 1 | `Crowding_RaisesTheCitysDisease_AndItsDeaths` |
| V3 | malnutrition out of disease transmission | `Starvation_RaisesDiseaseTransmission` |
| V4 | no relief containment | `ReliefBattle_FacesOnlyThePartOfTheLinesNotKeptFacingTheCity` |
| V5 | a successful sally neither burns engines nor resets mining | `ASuccessfulSally_BurnsEngines_DestroysTheMines_AndSetsBackTheApproach` |
| V6 | counter-mining ignored | `Miners_BreachTheWall_AndCounterMinersSlowThem` |
| V7 | repair labour fixed at 1 | `WallRepair_NeedsTheDefendersLabour` |
| V8 | arrears clock not interpolated | `Arrears_StartAtTheInterpolatedInstantThePaySourceRanDry` |
| V9 | cap tie goes to capitulation | `CapRule_LowerHealthYields_EqualHealthDefenderHolds` |
| V10 | lasting loss of hope removed | `AFailedRelief_LowersTheDefendersMorale_AndTheirHope…` |
| V11 | water rationed below the physiological minimum | `Water_IsNeverRationedBelowThePhysiologicalMinimum` |
| V12 | water cut ignored | `ACutWaterSupply_StopsTheInflow_AndTheCityDiesOfThirst` |
| V13 | assault losses on engineers and ranged removed | `AssaultLosses_FallOnTheCommittedMelee_TheEngineersAtHalf_AndTheRangedAtAQuarter` |
| V14 | garrison does not suppress unrest | `Occupation_AGarrisonSuppressesUnrest_ProportionallyToThePoliceRatio` |
| V15 | breach out of the morale target | `ABreach_LowersTheDefendersMoraleTarget` |
| V16 | evacuation ignores the corridor's opening | `Evacuation_StartsOnlyWhenTheCorridorOpens` |
| F1 | S-V1 reverted (thousandth floor in `LoseAtRates`) | `HeadLosses_AreTheSameOnEveryClock_InARunningSiege` |
| F2 | per-arm reinforcement carry dropped | `AttackerReinforcements_AreSplitInTheOpeningProportions_OnEveryClock`; populated table |
| F3 | no pay settlement at a siege pulse end | `GarrisonPay_FollowsTheCorridorsReinforcementsPulseByPulse` (first run: golden only — the test was added, then re-run) |
| F4 | no pay settlement before an assault | `GarrisonPay_IsSettledBeforeAnAssaultOrASallyChangesTheGarrison` |
| F5 | no pay settlement before a sally | `GarrisonPay_IsSettledBeforeAnAssaultOrASallyChangesTheGarrison` |
| F6 | the doctrine overwrites a human ration order | `AHumanRationOrder_IsAStandingOrder_TheDoctrineNoLongerOverridesIt` |
| F7 | every installation counts as occupied | `AnOwnCity_IsNeverRestive_WhateverItsGrievanceInput` |
| F8 | a capture never makes a population occupied | `Capture_MakesThePopulationOccupied_AndItsUnrestGrowsFromTheCapture` |
| F9 | raid state not hashed | `AnArrivedRaid_IsInTheStateHash_BeforeItIsResolved` |
| F10 | thousandth floor in `LoseAtRate` (theatre desertion) | `TheatreDesertion_OfASmallGarrison_IsNotFlooredAway_OnAnyPulseLength` (first run: survived — the test was added, then re-run) |

Teeth and aim (ADR-015 §7.2): each property is one the system ought to have — dt-independence of losses, reinforcements and pay (Law 3),
the brief's mechanisms (rationing, disease, relief, sally, mining, repair, water, breach, corridor), a human order's effect, occupation
depending on occupation, and a complete state hash.

### 12.4 Q14 re-measured

See §5 (rewritten). In short, on the fixed tree: Sevastopol ends CapLifted at day 1,080 with besieger health 0.255-0.256 on War Pulses
and on every battle pulse from 1 hour down to 30 seconds (before: Lifted between days 580 and 667 below 30 minutes); the outcome
distributions over 20 seeds are identical on War Pulses, 1-hour and 60-second pulses for all nine sieges (60-s battery: 417 s). The
recommendation (b) is unchanged and remains a PROPOSAL; the caveat it used to carry (a threshold "caught" at 60 s) is withdrawn.

### 12.5 Moved pins (attributed, measured)

- Replay goldens, both clocks: the exact head-loss carry alone (S-V1) moved them to War `724764749D8E…` / Battle `797CE664B16B…`; the
  siege row v3 (S-V6 flag, per-arm reinforcement carries), the raid state in the hash (S-V12; zero raids here) and, on the battle clock,
  pay settled before every garrison change (S-V2) gave the final War `2CC6A546…` / Battle `92E2E46C…`. Live = replay asserted first.
- Siege row: version 3, 1,259 → 1,292 bytes (+ ration flag, + 4 arm carries); the populated-table test asserts both are populated.
- Calibration record (War Pulse, harness seed, nine sieges): **unchanged** (passes unmodified). War Pulse 20-seed battery: unchanged.
- §6.2 loss breakdown: Vicksburg and Sevastopol moved by 1-28 people.
- Removed: `TieDense_AttackerReinforcementRemainder_GoesToTheLargestArm_LowestIndexOnATie` (the per-pulse remainder rule and its integer
  argmax are gone; there is no ordering left to tie). Renamed: `HistoricalDurations_FitUnderTheMaximumDuration_ExceptLeningrads` (was
  `CalibrationScenarios_AreAllInsideTheirBands_…`) and `Rationing_LowersDefenceStrictly_AndTheClosedFormEnduranceGrowsStrictly` (was
  `…AndExtendsEnduranceStrictly`). Re-rigged: `Occupation_…` (occupied city; same pinned revolt instant).

### 12.6 Builds, tests, scan (counts against the `9503d2b` baseline)

| Configuration | `9503d2b` | `be09e5c` (before r1) | fix r1 |
|---|---|---|---|
| M7Proto.slnx Release | 856 / 0 / 0 | 944 / 0 / 0 | **975 / 0 / 0** |
| M7Proto.slnx Debug | 856 / 0 / 0 | 944 / 0 / 0 | **975 / 0 / 0** |
| lane S tests (`Proto.M7.Tests.Siege`) | — | 88 | **119** |

Debug and Release builds: 0 warnings, 0 errors. Determinism scan (`scripts/check-proto-determinism.sh`): CLEAN, 87 files. M7Econ.slnx
and M7DetMath.slnx: not touched and not run (no project in them references Proto.M7; checked). Lane E's files (`WarEcon/`,
`WarLedger.cs`) are unchanged in fix r1 (`git diff be09e5c` lists none). Bench: `siege 60000` table 22.5 s; batteries in §5.

### 12.7 Limits of this round

- A revolt still has no automatic consequence; grievance is an input (§10.10). The standing-order reading of S-V6 and the lasting
  relief term of S-V8 are put to the Director (§10.9, §10.11).
- Lane E's `WarEconomyWorld` keeps `LossMilli`'s thousandth floor (lane E's code; harmless at War Pulse lengths; §11).
- Two per-pulse roundings remain, bounded, not carried (lane E's `Gross` ceiling in the supply line, `FireShare`'s Q32 floor; §11).
- The convergence evidence is one scenario's sweep plus nine scenarios' 20-seed batteries on three clocks; other inputs were not swept.

## 13. Verification round 2 (fix r2, 2026-11-06)

Clone `/dev/shm/m7c/S` (it existed at the start of this round, clean at `de47827`; no restore was needed), branch `lane/S`, starting
at `de47827`; the round-2 verifier's seven findings (S2-V1 … S2-V7, `scratchpad/m7c/verify-S-r2`). Every finding was reproduced on
`de47827` before anything changed: the verifier's probe file copied into a temporary test class of mine (`TmpR2Repro`, deleted before
the commit) with three probes of my own (R5 recording flag, R6 pay against corridor, A1 the old hash format for the golden
attribution), and the verifier's ten mutants re-run in my clone with a copy of the verifier's runner (filter adapted to my temporary
class; bound 73 s). Baseline on `de47827`: M7Proto.slnx Release 975/0/0, Debug 975/0/0 (measured). Evidence:
`scratchpad/m7c/S-fix-r2/` (`repro-probes.txt`, `repro-R5-R6.txt`, `mutants-r2-repro-out.txt`, `golden-attribution.txt`,
`probes-after-fix.txt`, `mutants-r2fix-final-out.txt`, the batteries and the attribution runs).

### 13.1 Verdicts

| ID | Sev. | Verdict | Reproduced on `de47827` (measured) | Fix | Semantic test(s) |
|---|---|---|---|---|---|
| S2-V1 | major | **REPRODUCED** | The verifier's ten mutants in my clone gave the verifier's result exactly: R2-M2, M3, M4, M5, M8, M9 survived (119/119 lane S tests pass); R2-M1 killed only by the replay golden; R2-M10 only by the calibration record; M6 and M7 by semantic tests. Probe P3: the immediate ration path behaves correctly (0.600 on [0, 5 d), then 1.000, held) — the defect was the missing tests. | Tests for every uncovered behaviour; R2-M5's property (a besieged garrison does not also desert on the theatre clock) is also part of the S2-V2 fix. No code defect in R2-M1 … M4, M8 … M10. | R2-M1 `AHumanRationOrder_UnderADoctrine_Stands_WhetherItLatchedOrActedAtOnce` (issued at B1 − 1, B1, B1 + 1); M2 `ALiberation_CalmsTheUnrest_EvenWithNoGarrisonToPoliceIt`; M3 `CorridorReinforcement_StopsAtTheInstallationsCapacity_…`; M4 `ACapture_WritesOffTheOldControllersArrears_…`; M5 `TheatreDesertion_IsChargedOnceOverTime_…`; M8 `ARaid_OnABesiegedInstallation_IsNotFought_…`; M9 `UnpaidPay_LowersTheBesiegedGarrisonsMorale_…`; M10 `CorridorSupplies_FlowOnlyFromTheOpeningInstant_…` |
| S2-V2 | major | **REPRODUCED** | Probe P1c (desertion only, 1,000 ‰/yr; lines contain nobody): garrison at start − 2 d / B1 / B2 on War Pulses 2,777 / 2,740 / 2,687 against never besieged 2,777 / 2,723 / 2,670 (17 heads of the part before the siege start never deserted); 1-day clock, lifted on day 8: 2,777 / 2,740 / 2,664 (the besieged part of [B1, B2) charged a second time). P1: desertion at the theatre pulse end B2 = 53 against ≈ 30.2 for the 4 days outside the siege. Unrest: the same code path (read); the r2 test shows the first increment after a capture is now the occupied part only. | `SettleTheatreLosses` (new): the theatre clock's desertion of an unbesieged garrison and the occupation unrest step, per installation over [settled, upTo) at the state that held; called at the theatre pulse end, at a siege's start (after settling pay to that instant, S-V2's rule) and at its end (before the installation stops being besieged and before control passes). Installation row v4 carries the settle instant. | `TheatreDesertion_IsChargedOnceOverTime_AcrossASiegesStartAndEnd_OnEveryClock` (besieged = never besieged exactly at five theatre pulse ends, both clocks: 2,777 / 2,723 / 2,670 / 2,619 / 2,568); `ACapture_InsideATheatrePulse_CountsOnlyTheOccupiedPartOfThatPulse` (captured day 70 on the 1-day clock, 5 d before the pulse end: unrest = OverDt(2,000 ‰, 5 d) exactly; revolt 26 pulses later); `GarrisonPay_IsSettledAtTheSiegeStart_BeforeThatInstantsTheatreDesertion` (found by mutant N11, §13.5) |
| S2-V3 | minor | **REPRODUCED** (report text) | Probes P2, P2b gave the verifier's per-seed outcomes, relief days and 200-seed counts byte for byte, on `de47827` and again on the fix r2 tree. The price omitted the latency of orders and reliefs (read; the delivery rule is pinned). | §5 corrected [r2]: "indistinguishable at these sample sizes", the per-seed and 200-seed evidence, and the price completed with order and relief latency (up to one War Pulse, 21 days in Antiquity). Recommendation (b) unchanged, a proposal (§10.1, §10.13). | — (measurement) |
| S2-V4 | nit | **REPRODUCED** | §1: "version 2 … siege 1,259", "88 tests"; §7: the removed tie-break test and "rows version 2: exact lengths 215/99/1,259" (read; the code was v3, 1,292). | Corrected in place [r2]; the rows are now v4: 223 / 99 / 1,292. | — |
| S2-V5 | nit | **REPRODUCED** (measured; the verifier had inferred it) | Probe R5: two identical runs (equal ledgers, 8 evaluations each, equal garrisons) hashed `5A348C89…` with records kept and `AD75484C…` without. | A running 64-bit digest of every pulse record (`CounterRng.Mix` per field), folded whether or not the record is kept, replaces the kept list in `StateHashHex`. After: both `115278180CDC…` (probe). | `TheStateHash_DoesNotDependOnKeepingPulseRecords_ButCoversEveryPulse` (equal hashes; the digest equals the fold of the kept records on both worlds; a different trajectory gives a different digest and hash; changing only the digest changes the hash) |
| S2-V6 | nit | **REPRODUCED** (measured; the verifier had inferred it) | Probe R6 (no Coinage: wages in kind; ten days of wages in the home granary; a corridor wanting 100 × the wages): War Pulses pay the first settlement's wages in full (29,166 gm), arrears from day 7; on 1-day pulses the corridor took all 41,666 gm first, wages 0, arrears from day 0. | Pay is settled before the sieges' flows at the siege pulse end, the turn start and the raid (the theatre pulse end already did): one priority on every clock. After: 1-day 4,166 gm = the first day's due, arrears from day 1. The raid site is provably a no-op (N7, §13.5). The priority is my choice (§10.12). | `PayInKind_IsFedBeforeTheCorridor_AtEverySettlement_OnEveryClock` (War Pulses: theatre pulse end; 1-day pulses: siege pulse end; a turn start inside the first War Pulse) |
| S2-V7 | nit | **REPRODUCED** | `git branch -a --contains 2e245cf`: none; present only in my clone's reflog; `git diff 2e245cf de47827 --stat`: `SiegeFixR1Tests.cs` and `H-S.md` only. | §5's attribution corrected [r2]; every §5 battery re-measured on the fix r2 tree (§13.4). | — |

No finding was refuted.

### 13.2 Code changes (all in `Proto.M7/War/Siege/`; nothing outside lane S's files)

- `SiegeWorld.cs`: `Inst.TheatreLossSettledMs`; `SettleTheatreLosses` (the former inline body of the theatre pulse end, now per span);
  calls at the theatre pulse end, in `BeginSiege` (after `SettleTheatre`) and at the top of `EndSiege`; `EndSiege`, `Evaluate` and
  `Adjudicate` take the scheduler so a revolt crossed in the settled span raises its event where one can be raised; pay first at the
  siege pulse end, turn start and raid; `_pulseDigest` with `FoldPulse` (public, for the test) and `PulseDigest`; `StateHashHex` hashes
  the digest instead of the kept list.
- `SiegeCodec.cs`: version 4, installation row + `TheatreLossSettledMs` (215 → 223 bytes).

### 13.3 Tests (17 new in `Proto.M7.Tests/Siege/SiegeFixR2Tests.cs`; one assertion block updated)

The 17 are listed in §13.1 (two `TheatreDesertion` cases, three ration-order offsets, three pay-first sites, nine single tests).
`PopulatedTables_RoundTripBitExact_WithExactLengths` now asserts the v4 field populated (the settle instant = the current theatre pulse
start, non-zero) and the 223-byte installation row.

### 13.4 Q14 and calibration re-measured on the fix r2 tree (Release bench, shared CPUs)

| Measurement | Result against fix r1 | Attribution (measured) |
|---|---|---|
| War Pulse battery, 20 seeds (`siegeseeds 20 0`, §6.1) | **byte-identical** | — |
| Calibration record (harness seed, War Pulses) | **unchanged** (`CalibrationRecord_IsPinned` passes unmodified) | — |
| 1-hour battery, 20 seeds | identical except Vienna's maximum 84 → 83 days | the fix r2 tree with only the siege-start split reverted gives fix r1's output byte for byte (Debug bench): the harness war starts 45 days before each siege, so the part of the theatre pulse before the siege start now deserts at lane E's base rate (a few men), and one seed capitulates a day earlier |
| 60-second battery, 20 seeds | outcome counts unchanged; Constantinople median 45 → 44, maximum 66 → 58; Vienna maximum 84 → 83 | the same revert gives fix r1's whole 60-second battery byte for byte (Release bench): Constantinople's and Vienna's rows move with the siege-start desertion through the assault and relief realisations, which are keyed on their instants |
| Sevastopol sweep (War Pulse, 1 h … 30 s) | identical outcomes and healths (CapLifted day 1,080; besieger health 0.256 War, 0.255 every battle pulse) | — |
| `siege 60000` table | every outcome and day on War Pulses unchanged; garrisons and deserters moved by 0-32 people (§6.2); on 60-second pulses Constantinople's harness seed is Stormed at day 44.5 after 64,055 evaluations (was 58.5 / 84,245), Alesia 44.89 days (was 44.87) | with both splits (start and end) reverted the table equals fix r1's in every column but wall time (Debug bench); with only the start split reverted, all but one cell (Vienna 60 s deserters 2,943 against 2,957: the end split, which removes the double count after the relief) |
| 200 seeds, three relief sieges, War vs 1 h (probe P2b) | byte-identical to the verifier's `de47827` output | — |

So the Q14 evidence of §5 stands as corrected: equal 20-seed counts, 200-seed counts within sampling noise, the same convergence, the
recommendation (b) as a proposal with its full price. Times: War battery 0.7 s, 1-hour 6.8 s, 60-second 434 s, table 21 s.

### 13.5 Mutants (fix r2 battery, final tree)

Runner `scratchpad/m7c/S-fix-r2/mutants_r2fix.py` (adapted from the verifier's), list `r2_mutants.json`, output
`mutants-r2fix-final-out.txt`. Each mutant: one exact, unique replacement; Release rebuild of the test project; the lane S suite (136
tests) run once, bounded at **88 s = 10 × the 8.8 s clean cycle** (build 5.1 s + suite 3.7 s, measured on the 135 tests before the 136th was added); restore from an in-memory copy
verified by SHA-256 (21 of 21 verified). No build failed, none hung (slowest suite run 4.4 s). **20 of 21 killed, each by at least one
semantic test; one equivalent survivor (N7).** The goldens and the calibration record also fail where they apply, never alone.

| Mutant | Change | Semantic kill(s) |
|---|---|---|
| R2-M1 | immediate ration order not held against the doctrine | `AHumanRationOrder_UnderADoctrine_Stands_…` (offset 0) |
| R2-M2 | own-polity unrest decays only when policed | `ALiberation_CalmsTheUnrest_…` |
| R2-M3 | corridor reinforcement ignores capacity | `CorridorReinforcement_StopsAtTheInstallationsCapacity_…` |
| R2-M4 | arrears not written off at a capture | `ACapture_WritesOffTheOldControllersArrears_…` |
| R2-M5 | besieged garrisons also desert on the theatre clock | `TheatreDesertion_IsChargedOnceOverTime_…` |
| R2-M6 | competing loss flows not split | `LossAtto_…_TwoSided`, `Sanitation_CutsDiseaseDeaths_…`, two cap tests |
| R2-M7 | hunger never lowers capability | `Rationing_LowersDefenceStrictly_…` |
| R2-M8 | a raid on a besieged installation is fought | `ARaid_OnABesiegedInstallation_IsNotFought_…` |
| R2-M9 | unpaid pay out of the morale target | `UnpaidPay_LowersTheBesiegedGarrisonsMorale_…` |
| R2-M10 | corridor food before the opening | `CorridorSupplies_FlowOnlyFromTheOpeningInstant_…` |
| N1 | no theatre-loss split at the siege start | `TheatreDesertion_IsChargedOnceOverTime_…`, `GarrisonPay_IsSettledAtTheSiegeStart_…` |
| N2 | no theatre-loss split at the siege end | `TheatreDesertion_…` (1-day), `ACapture_InsideATheatrePulse_…` |
| N3 | end split after the installation is marked free | `TheatreDesertion_…` (1-day) |
| N4 | siege flows before pay at the siege pulse end | `PayInKind_IsFedBeforeTheCorridor_…` (1-day) |
| N5 | siege flows before pay at the turn start | `PayInKind_IsFedBeforeTheCorridor_…` (turn start) |
| N6 | siege flows before pay at the theatre pulse end | `PayInKind_IsFedBeforeTheCorridor_…` (War Pulses) |
| N8 | digest folds only kept records | `TheStateHash_DoesNotDependOnKeepingPulseRecords_…` |
| N9 | the state hash omits the digest | `TheStateHash_DoesNotDependOnKeepingPulseRecords_…` |
| N11 | no pay settlement at the siege start before its theatre desertion | `GarrisonPay_IsSettledAtTheSiegeStart_…` (first run: survived — the test was written, then re-run) |
| N12 | the settle instant not reset at a theatre pulse start | `BaseDesertion_…`, `TheatreDesertion_OfASmallGarrison_…`, `Occupation_…`, and seven more |
| N7 | pay order flipped in the raid resolution | **survived — equivalent**: a raid is resolved only at a theatre pulse start (`OnTheatrePulseStart`, or an arrival at that instant), where `_tpLastSettled` already equals the instant, so `SettleTheatre` there settles nothing and its order cannot matter |

Teeth and aim (ADR-015 §7.2): each killed property is one the system ought to have — the report's stated behaviours (§3, §2.5, §10.10),
desertion and unrest charged once over time on every clock (Law 3), one feeding priority on every clock, a state hash that is a
function of the simulation and covers every pulse.

### 13.6 Moved pins (attributed, measured)

- Replay goldens, both clocks: `2CC6A546…` / `92E2E46C…` → War `8EF1425A…` / Battle `71DB1CDD…`, by the hash FORMAT only: the fix r1
  hash format recomputed on the fixed tree (installation rows cut to their old 215 bytes, the kept pulse-record list in place of the
  digest) gave `2CC6A546…` / `92E2E46C…` exactly (probe A1, `golden-attribution.txt`; measured before the digest's mixing was moved to
  `CounterRng.Mix`, a change of the digest value only). The golden runs have base desertion off, no grievance and pay in coin, so the
  behaviour changes of fix r2 do not reach them. Live = replay is asserted first.
- Installation row: version 4, 215 → 223 bytes; the populated-table test asserts the new field populated.
- Calibration record and War Pulse battery: unchanged. 1-hour and 60-second batteries, `siege 60000` table: §13.4 (attributed).
- §6.2's loss breakdown (War Pulse rows): outcomes and days unchanged; garrison end and deserters moved by 0-32 people (Alesia
  67,081 → 67,054 garrison, 2,216 → 2,244 deserted; Leningrad 201,348 → 201,317, 59,699 → 59,731; Vicksburg, Sevastopol, Masada
  unchanged) — the part of the theatre pulse before each siege start now deserts.
- No existing test was re-rigged or re-pinned except the goldens and the populated-table row length. `Capture_MakesThePopulationOccupied_…`
  passes unchanged: on War Pulses its capture falls on a theatre pulse boundary, so the occupied part is a whole pulse either way.

### 13.7 Builds, tests, scan (counts against the `9503d2b` baseline)

| Configuration | `9503d2b` | `de47827` (fix r1, before r2) | fix r2 |
|---|---|---|---|
| M7Proto.slnx Release | 856 / 0 / 0 | 975 / 0 / 0 | **992 / 0 / 0** |
| M7Proto.slnx Debug | 856 / 0 / 0 | 975 / 0 / 0 | **992 / 0 / 0** |
| lane S tests (`Proto.M7.Tests.Siege`) | — | 119 | **136** |

Debug and Release builds: 0 warnings, 0 errors. Determinism scan (`scripts/check-proto-determinism.sh`): CLEAN, 87 files (it first
FAILED on fix r2's own `unchecked` digest mixing, which was then replaced by `CounterRng.Mix`, the allow-listed mixer). M7Econ.slnx and
M7DetMath.slnx: not touched and not run (no project in them references Proto.M7; checked at the resume). Lane E's files (`WarEcon/`,
`WarLedger.cs`) are unchanged in fix r2.

### 13.8 Limits of this round

- The pay-first priority (S2-V6) makes the feeding order the same on every clock; the share of a short granary inside the interval in
  which it runs dry still depends on the interval's length (bounded by one interval's flows). A pro-rata share per interval would be
  the dt-correct rule; proposed (§10.12), not built.
- Theatre desertion and unrest are split at siege boundaries only (the only changes of their state inside a theatre pulse in this
  world); the unrest step is first order, so a split changes it slightly by itself.
- Mutant N7 is equivalent by argument (the code path), not by a test.
- The batteries' attribution reverted the siege-start split alone (1-hour and 60-second batteries: then byte-identical to fix r1) and both
  splits for the `siege 60000` table (then identical to fix r1 except wall times); the pay-first order and the digest moved nothing in
  them (inferred from those two results: nothing else differs).
