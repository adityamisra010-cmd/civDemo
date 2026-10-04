# M0–M5 coherence matrix — the integrated tree

**Tree:** `m5-integration` @ `0b8cb14` plus the verification stream (`m5i-v-verify`). **Date:** 2026-10-03.
**Base:** the M0–M5 audit of `ffbb6f8` (pre-stream). This document updates its matrix (§A), gap list (§B)
and save/load table (§E) to the integrated tree. It does not re-decide anything: binding decisions are in
`docs/adr/adr-033-m5-integration-pass.md`.

**Evidence rules.** "MEASURED (V)" means the verification stream ran it on this tree. A commit is cited
for every fix; the commit's own record is secondary evidence (gov-4). Where no test was run by V the cell
says so. Evidence numbers `[E#]` are the audit's.

## 1. What each stream landed

| Stream | Branch tip (merged) | Fixed |
|---|---|---|
| Roads foundation + research-tree UI | `997824b` (frozen transport), `dad9734` | road development system (ADR-032), research UI |
| S1 governing loop | `43f38cd` → merge `69181ab` | schema v30 TaxPolicies, `SetTaxRate` (kind 5), GovernanceSystem (research-gated levy, administrative reach written into `ControlRow.Strength` [E21]), tax as an explained cause, coupling-map entries (road → reach) |
| U1 era theme | `e61e182` → `7a6ed6f` | Age-derived UI theme (ADR-033 D8), era previews |
| S2 capability layer | `8478b4d` → `1c62632` | knowledge-derived labour activities (D1), `AvailableActionsQuery` (D2), one labour predicate for load / PathBuild / UI [E1] (`ed18459`), EnqueueConstruction availability + project→entity link [E6, E8] (`0eccd42`), `AiOrders` — the AI acts through orders, CLI + UI (`b2d2902`) [E11, E12] |
| Integration UI (U2a) | `09db372` | AI tax wiring (`5e7fa35`), M1 labour path retired + emitter guards (`a6dcce9`), `--ai-empires` launch option (`ca7e2d6`) [E13 partial], capability-driven action surface (`0d5efc7`) [E2], one map-ink source |
| U3 world map | `0ec37ac` | roads drawn on the map [E10], walls from state not Age label (D-038 H5) [E33] |
| S3 institutions | `93dfcd2` → `e5cd3fa` | schema v31 Institutions + ConstructionLabor; construction capacity spent once (D10, `8ef9a50`) [E5]; universities on ADR-028's lifecycle (`941104c`); AI research goals cover the capability gates (`72c0ac4`) |
| U2b views | `ecf3aa7` | player-facing settlement/empire/institutions views, debug behind a toggle (D9) [E37]; state-derived annal events, colony names [E23, E24]; `--resume` by replay [E32]; lens text truth (audit B2) |
| S4 AI goals | `b952a9f` → `0b8cb14` | goal-sequential AI research (`a391471`): road order 141, Age 236, levy 547 on the canonical world |
| V verification | `m5i-v-verify` | mid-game colony orders replay (`7100c77`); integrated save/load battery; cross-process AI leg; previews re-rendered |

## 2. Matrix (updated)

Legend as in the audit; **FIXED** cites the commit; **REMAINS** is an open gap or an escalation (§4).

| # | Mechanic | State | Eligibility / progress | UI | AI | Save/load | Determinism | Notes |
|---|---|---|---|---|---|---|---|---|
| M0 | Kernel | OK | OK | OK | OK | OK | OK | order validation now defers checks for mid-game colonies (`7100c77`) |
| M1b | Settlements, catchments, distances | OK | OK | OK | OK | OK | OK | × Ages GAP [E33] FIXED (U3) |
| M1c | PathBuild | OK | FIXED [E5] (`8ef9a50`) | OK | OK | OK | OK | |
| M1d | Labour (kinds 2, 3) | OK | FIXED [E1] (`ed18459`) | FIXED [E2] (`0d5efc7`) | AI still emits no labour orders [E3] — REMAINS | OK | FIXED [E4] (V xproc leg) | |
| M1e | UI session | OK | OK | OK | FIXED [E12] | resume by replay [E32] (`73149b5`) | AI session tests (`AiSessionTests`) | |
| M2a–c | Cohorts, classes, migration | OK | OK | OK | OK | OK | OK | migration ← road distances recorded (S1 coupling map) |
| M2d | Needs / grievance | OK | REMAINS (D-021 / unrest-lite escalated) | OK | OK | OK | OK | |
| M2e | Chronicle | OK | OK | FIXED [E23, E24] (`e71c4be`) | — | by replay | OK | |
| M3a | Recipes | OK | REMAINS [E15] (not knowledge-gated) | OK | OK | OK | OK | escalated |
| M3c | Trade | OK | OK | OK | foreign classification live at aiEmpires ≥ 1 | OK | OK | |
| M3f | Harvest weather | OK | OK | invisible [E34] | OK | OK | OK | correlation on road-aware distance [E14] — REMAINS (escalated) |
| M4a | Polities, control, capitals | Strength written by governance [E21] FIXED; claims/recognitions/notables no writer [E20] REMAINS; capital never relocates [E22] REMAINS | OK | OK | OK | OK | OK | |
| M4b | Construction | OK | FIXED [E5, E6, E8] (`0eccd42`, `8ef9a50`) | FIXED (action surface) | FIXED (`b2d2902`) | MEASURED (V battery: granary + university) | OK | |
| M4c–g | Colonization, appropriation, revolt | OK | DORMANT [E16–E18] — REMAINS (escalated) | OK | OK | OK | colony orders now replay (V) | revolted settlements refuse orders — REMAINS (by design of the control predicate; escalated) |
| M4h | Disasters | OK | hazard 0.0, CR-016 OPEN | OK | OK | OK | OK | |
| R | Research | OK | AI researches (`72c0ac4`, `a391471`) | research on the status band ("research idle [K]") | FIXED [E11] | MEASURED (V: mid-research, after completion) | MEASURED (V xproc: kind 6) | ResearchCostModifiers now written by InstitutionsSystem |
| A | Ages | OK | military milestones met by the founding warband [E29] — REMAINS | OK | FIXED [E12] | MEASURED (V: before/at decision, after entry) | MEASURED (xproc kind 7) | thin mechanical meaning [E28] — REMAINS |
| U | Units | OK | OK | OK | modernize via AI Age | OK | OK | no military orders (M7 Battle Layer) |
| T | Roads | OK (frozen at `997824b`) | OK | FIXED [E10] (U3) | FIXED [E9] (`b2d2902`) | MEASURED (V: founded world, mid partial route 62%) [E39] | MEASURED (xproc kind 8) | straight geometry — REMAINS |
| G | Governing loop | FIXED (S1) | research gate | tax control (`TaxControlTests`) | AI levies (`5e7fa35`) | MEASURED (V: before issue, issued-not-in-effect, in force) | MEASURED (xproc kind 5) | D-021 tax pushback — REMAINS |
| I | Institutions (universities) | FIXED (S3, v31) | lifecycle + brake | institutions view | AI founds | MEASURED (V: mid maturation) | in-process | |

## 3. Verification evidence (MEASURED by V on this tree)

- **Mid-game colony orders.** Reproduced: `ValidateAgainstWorld` threw "settlement 12 … does not exist" on a
  log whose orders the live step applies (stranded-source rig; colony founded turn 1→2, orders delivered
  turn 2→3). Fixed in `7100c77`; `ColonyOrderValidationTests` (3 tests) and the amended
  `PathBuildTests.UnknownSettlement_RejectedBeforeTurnOne_Actionably`.
- **Integrated save/load battery.** `IntegratedSaveLoadBatteryTests` (8 tests, `suite=determinism`): dev
  world, seed 42, aiEmpires = 1, 40 turns, every player order kind; save points turn 1, mid-research (1),
  after a completion (28), mid partial road (2), mid university maturation (32), tax before issue /
  issued / in force (5, 5+, 6), Age before / at decision / entered (3, 3+, 4). Hash-identical every turn
  after load with the AI and player re-run on the loaded state; action surfaces equal before save / after load.
- **Cross-process.** `ci.yml` determinism-xproc AI leg, run locally: two processes, 600 turns, canonical
  world, aiEmpires = 1, player kinds 3/4/6 + AI kinds 4–8 (190 orders); hash logs and run logs
  byte-identical; replay identical; `sim inspect` "reproduction VERIFIED: 601 turns"; final hash
  `7e9161a8…`.
- **Bench.** `sim bench --founded --seed 42 --turns 300`: `997824b` 33,947 ms vs this tree 34,515 ms
  (median of 3 each, alternated) — ratio 1.017.

## 4. What remains (escalations, not fixed by this pass)

1. **Unrest-lite / D-021 tax pushback.** Taxation has no behavioural pushback beyond the measured
   pushback note (S1); grievance still drives nothing.
2. **Recipes are not knowledge-gated** [E15] — Stone-Age bronze casting and pottery before research.
   ADR-033 D1 keeps production unchanged; touches calibration.
3. **Ages' thin mechanical meaning** [E28] and **the founding warband satisfying every military
   milestone** [E29] — needs recruitment (M7 Battle Layer; was "M6" before the 2026-10-03 rebase) or a content ruling.
4. **Dormant M4 conflict loop** [E16–E19] — no colony, starvation, revolt or raid in the order-free
   reference runs; CR-016 open.
5. **Weather correlation rides road-aware distance** [E14] — whether it should use geographic distance.
6. **No military orders** — armies and mobile agents are M7 (Battle Layer; was "M6" before the 2026-10-03 roadmap rebase).
7. **Straight road geometry** — routes are drawn and costed straight (transport frozen).
8. **Capital relocation** [E22] — a revolted capital stays the capital; governance reach is anchored on it.
9. **Revolted settlements refuse orders** — once control is lost, labour/construction orders are refused
   by the control predicate; there is no reconquest or re-annexation order.
10. **`aiEmpires` defaults to 0** — the option exists (CLI, UI) but a default game has no rival.
11. **A1 yield = cereal yield (CR-006)** — the founding food activity yields at the cereal rate.
12. Also still open from the audit: AI labour decisions [E3]; claims/recognitions/notables have no
    writer [E20]; Age-eligibility table read only by the schema [E30]; harvest weather invisible [E34].

## 5. R1 — research trees drive gameplay (append-only, 2026-10-03)

**Branch:** `m5i-r1-unlock-pipeline` (cut from `m5-integration` @ `ac13c3d`; measured at `c62c594` + this record). Not merged.

- **Recipes [E15] — FIXED.** `research.json` entity kind `recipe`: `recipe.pottery_firing` requires `pottery_open_fired`,
  `recipe.bronze_casting` requires `tin_bronze`; `recipe.weaving` and `recipe.toolmaking` are declared baseline (null
  requirement — ownership matrix class A; Director decision recorded in the audit). goods.json recipes link the entity;
  `CraftingQuery.IsRecipeAvailable` (controller knowledge + the artisan latch) is the one predicate ProductionSystem,
  LabourActivities and AvailableActionsQuery call. M3a row: eligibility now research-gated.
- **Action surface.** New `Production` domain (crafts known, generic over goods.json); university founding (the
  Institutions domain, previously never rendered) joins the build block; notices announce each newly researched action.
- **Audit.** `docs/research-gameplay-unlock-audit.md` (script + CI `--check`): 431 nodes classified A–J; 115 deferred
  entities (units M7; buildings, infrastructure and institutions with no realizing system); 2 baseline-claim findings
  (weaving, toolmaking) left to the Director.
- **Goldens (MEASURED).** founded `74306d6a…` → `68c629b6…` (ci.yml FOUNDED_GOLDEN moved; two CLI processes agree);
  driven `65d53a01…` → `7aa20e40…`; FirstReign `481d3717…` → `158bdd4c…`. Recipe-knowledge attribution control: the
  content twin without the recipe links returns every old pin byte for byte.
- **Behaviour (MEASURED).** The order-free founded world now trades NOTHING in 300 turns (first trade was turn 28) and,
  in 650 turns, no merchant town emerges (was turn 119) — crafted goods and the trade economy now wait on research.
  Calibration battery green; autoplay 4 seeds × 650: every gated corridor in band; the two quarantined corridors move
  slightly (density breach 3.91 % → 3.62 %, migration 74.12 % → 74.16 %); final populations within 0.3 %.
- **Suites (MEASURED, Release).** Sim.Tests 1396 / 0 / 5; Sim.Ui.Tests 472 / 0 / 0; banned constructs, read isolation,
  read-only proof, research content audit, calibration report and the unlock audit `--check` exit 0; ci.yml
  determinism-xproc (incl. the aiEmpires = 1 leg: 187 orders, kinds 3–8, inspect VERIFIED 601 turns) passes locally.
- **Remains.** Weaving/toolmaking gating (Director); 115 deferred entities; order-free world has no trade until research
  (a consequence, reported for a ruling, not retuned).

## 6. R2b — governance and weather gaps (append-only, 2026-10-03)

**Branch:** `m5i-r2b-governance-fixes` (cut from `m5-integration` @ `f1fe76f`). Not merged. Director decisions 9, 10,
11 (governance and weather parts of 15/16). Status words: RATIFIED (a Director ruling or frozen record), IMPLEMENTED
(shipped here), INFERRED (an implementation inference — not a ruling), DEFERRED, OPEN.

### 6.1 Tax brake — D-021 unrest-lite (IMPLEMENTED; parameters INFERRED within measured frames)

Chain: tax edict → effective rate (declared × reach) → **Dignity** need bound with source `taxBurden`, satisfaction
= 1 − effective rate (D-035-D, RATIFIED: "taxation → Dignity is direct") → grievance stock (NeedsGrievance, dt-integrated)
→ `State.Unrest.TaxGrievance` = the stock × the share of the aggregate shortfall that lifting the levy would close
(zero where no levy is felt) → protest p = clamp((G_tax − 15)/(50 − 15), 0, 1) →
(1) output × (1 − 0.5·p·r) (the withheld levied effort; untaxed protest drags nothing, so hunger cannot feed on itself);
(2) grievance decay + 0.005·p /yr (valve 1, discharge); (3) at p = 1 RevoltSystem drops the control row (uprising).
Config `needs.json unrest` (optional; absent → inert). Read-isolation gate: `Sim.Core/State/Unrest.cs` allowlisted as
the single sim-side reader of grievance, reason recorded in the script. Valve 2 (fatigue stock), 3 (an unrest-driven
exit push) and the ignite-and-burn-out battery entry are NOT shipped (DEFERRED; a dedicated fatigue stock is a new
serialized table); the AI tax valve (valve 6) already existed; migration's happiness-weighted viability remains the only exit coupling.

**INFERRED simplification:** the levy's share is re-attributed each turn from the current shortfalls rather than
remembered from the past levy. A first cut keyed protest on the WHOLE grievance stock and was rejected on measurement:
an untaxed, fed, housed settlement (dev world, labour swing to 60 % farming, pottery not yet researched) reached
grievance 79.6 and rose at turn 17 — Comfort/variety grievance, not politics.

**Measured (founded seed-42, N = 12, one player Empire holding all, `arithmetic_babylonian` granted, levy from turn 0,
300 turns; rig `GovernancePushbackMeasurement`, skipped in the suite):**

| arm | turn | capital pop | empire pop | held | capital eff. rate | legitimacy | capital grain cum. | empire grain cum. | migrants cum. | capital protest | capital levy grievance |
|---|---|---|---|---|---|---|---|---|---|---|---|
| BEFORE 0 % | 300 | 3999 | 40594 | 12/12 | 0.000 | 100.00 | 7469751 | 74354301 | 4792 | — | — |
| BEFORE 40 % | 300 | 4027 | 40902 | 12/12 | 0.400 | 82.92 | 8415477 | 78526064 | 4651 | — | — |
| BEFORE 99 % | 300 | 4000 | 41014 | 12/12 | 0.990 | 57.67 | 9507973 | 84067961 | 3900 | — | — |
| AFTER 0 % | 300 | 4000 | 40625 | 12/12 | 0.000 | 100.00 | 7434015 | 73978943 | 4877 | 0.000 | 0.00 |
| AFTER 40 % | 300 | 4016 | 40779 | 12/12 | 0.400 | 82.91 | 8346336 | 77884849 | 4778 | 0.000 | 7.61 |
| AFTER 70 % | 300 | 4024 | 40857 | 12/12 | 0.700 | 70.10 | 8462257 | 80250852 | 4669 | 0.187 | 21.56 |
| AFTER 99 % | 300 | 3990 | 36537 | 11/12 | 0.000 (seat lost) | 100.00 | 7370977 | 73921183 | 4956 | 0.000 | 0.00 |

(BEFORE = `f1fe76f`; the 99 % AFTER arm reads protest 0.918 at turn 20 and has lost its seat by turn 50.) Against the
untaxed arm: 40 % — capital grain ×1.123, empire ×1.053, no protest at any checkpoint (normal tax viable); 70 % — mild
protest (p 0.15–0.19), gain shrinks (capital ×1.138, empire ×1.085); 99 % — the seat rises between turns 20 and 50, and
because a lost seat administers nothing (ADR-033 D4 §2 item 5) the levy then falls nowhere: empire population −10.1 %,
empire grain −0.1 %, all extraction gone. Before R2b the same 99 % levy was pure gain (+13.1 % empire grain, nothing lost).

### 6.2 Weather distance (IMPLEMENTED)

`HarvestWeatherSystem.Kernel` reads `GeographicDistance` (straight-line site-to-site km / KmPerCostUnit) instead of the
road-aware `SettlementDistances`; terrain-less toys keep their hand-written table. No road rule or routing code touched.
`harvestVariance.spatialDistance: "travelCost"` restores the old kernel and exists only as the attribution control.
Tests: `HarvestWeatherGeographyTests` (halving every travel cost and severing a route leaves 5 steps of weather
bit-identical; replay deterministic). Resolves §5 B4 of docs/m5-governing-loop-port.md.

### 6.3 Revolt and labour (RETAINED, documented) — capital loss (DEFERRED, total)

A revolted (or risen) settlement refuses its former ruler's labour and construction orders because every order domain
asks the D-037 control relation. This is the intended consequence (the RevoltSystem header: "a settlement at zero
happiness stops obeying"), not a world deadlock: the settlement keeps people, stocks, standing allocation and output,
and its grievance discharges (pinned: `UnrestTests.ExtremeTax_…_TheEpisodeBurnsOut_Deterministically`; turn-exact
refusal: `FoundedHarnessTests`, revolt at 97). It never returns to control: re-annexation / reconquest have no ratified
mechanism in M5 (M7 battle-layer war, M8 politics — 2026-10-03 rebase) — DEFERRED, not invented. Uncontrolled development is stream R2a's.
Capital loss: no succession or relocation semantics exist in any ratified record; the existing L6 rule stands — a seat
the Empire no longer holds administers nothing (reach 0, levy falls nowhere), total and pinned
(`AnEmpireThatNoLongerControlsItsCapital_ReachesNothing_SoItsTaxFallsNowhere`). It is now REACHABLE in play (the 99 %
uprising) — OPEN for a Director ruling on succession.

### 6.4 Construction-capacity labels (IMPLEMENTED, UI only)

The action surface now labels the builders' figures "estimated for the coming turn" (detailed layout adds that the
turn re-reads labour, housing's draw and its own dt); the displayed pool is computed over LABOUR adults (after
institution staff, the reader the model uses) so pool − housing = available. No model change.

### 6.5 Scarcity / conflict reachability (diagnosed; no retuning)

Rig `ScarcityConflictReachabilityMeasurement` (skipped): canonical founded world, AI rivals through `AiOrders.Append`.

| seed | arm | turns | colonies | control rows lost | stateless at end | deficit settlement-turns | appropriation-armed turns | first loss |
|---|---|---|---|---|---|---|---|---|
| 42 | no orders | 500 | 0 | 0 | 0 | 13 | 0 | — |
| 42 | aiEmpires=1 | 500 | 0 | 0 | 0 | 17 | 0 | — |
| 42 | tax 40 % | 500 | 0 | 0 | 0 | 12 | 0 | — |
| 42 | tax 99 % + aiEmpires=1 | 500 | 0 | 1 | 1 | 12 | 0 | 23 |
| 7 | no orders | 300 | 0 | 0 | 0 | 14 | 0 | — |
| 7 | aiEmpires=1 | 300 | 0 | 0 | 0 | 14 | 0 | — |
| 7 | tax 40 % | 300 | 0 | 0 | 0 | 11 | 0 | — |
| 7 | tax 99 % + aiEmpires=1 | 300 | 0 | 1 | 1 | 15 | 0 | 31 |
| 1234 | no orders | 300 | 0 | 0 | 0 | 20 | 0 | — |
| 1234 | aiEmpires=1 | 300 | 0 | 0 | 0 | 23 | 0 | — |
| 1234 | tax 40 % | 300 | 0 | 0 | 0 | 19 | 0 | — |
| 1234 | tax 99 % + aiEmpires=1 | 300 | 0 | 1 | 1 | 20 | 1 | 32 |

- **Revolt — (c) was incorrectly conditioned for the M5 governing loop, now (a).** Before R2b only the two zero corners
  fired it (total deprivation; a 100 % levy at full reach), so no plausible play reached it. With the uprising path it
  fires deterministically under sustained near-total exaction (turns 23–32 on all three seeds) and never at ≤ 40 %.
  The deprivation corner stays reachable by player error (FoundedHarness: 0 % food labour → revolt at turn 97).
- **Appropriation — (a) rare but reachable, correctly conditioned.** Its raider must be stateless and short of food
  (D-037 B3); statelessness now arises from uprisings, and the trigger armed once (seed 1234). No defect.
- **Colonization — (a) rare; correctly conditioned, unreached in plausible play.** D-037 B1 founds only from people with
  NO viable reachable destination; in the canonical frontier world (deficits on 11–23 of 3,600–6,000 settlement-turns)
  that condition never holds. Unit-tested reachable; not a defect; not retuned.
- **Scarcity itself** is rare by the CR-003 ruling (a frontier world is not Malthusian until land fills) — not changed.

### 6.6 Age military milestones (IMPLEMENTED; dependency PENDING on M7 Battle Layer)

Every Age's Military Realization milestone now counts only formations whose CURRENT identity is realized at or after the
Age being entered (`minIdentityAge`, loader-enforced ≥ that Age) and carries `pending: "M6 recruitment …"` (data string predates the 2026-10-03 rebase; recruitment is now M7 Battle Layer); the Age
panel labels it "pending". Under free modernization (ruling 18) the founding line holds Age A−1's identity on the eve
of A, so in M5 no military milestone can hold — honestly, never manufactured. Measured effect (dev world, seed 42,
`AiAgeAdvancementIntegrationTests`): player A2 eligibility turn 275 → 338, AI 345 → 385. No Age becomes unreachable:
every Age keeps ≥ minCategories non-military categories (A2: 2 required of tech/material/institutional/systemic; A3–A9
likewise) — checked against ages.json, INFERRED for A3–A9 reachability in play (not run to those Ages).

### 6.7 Goldens (MEASURED) and attribution

| golden | old (R1) | new | cause (per-layer twins) |
|---|---|---|---|
| founded seed 42 × 300 (ci.yml) | `68c629b6…` | `1368df9f…` | all three layers (each stripped alone still moves it) |
| driven seed 42 × 300 | `7aa20e40…` | `58e4c47c…` | all three layers |
| FirstReign × 40 | `158bdd4c…` | `680a20c5…` | Age-military layer alone |
| toy seed 42 × 200 | unmoved | — | no terrain, no governance |

`TestConfigs.PreR2b` (weather travelCost + Dignity unbound/no unrest + relaxed formations facts) returns each R1 pin byte
for byte; every older layer control now runs on PreR1 = PreRecipeKnowledge(PreR2b(Sim())) and is unmoved. Re-pinned test
values (MEASURED): ClassSystem artisan latch 71 → 70; Merchant first-active 119 → 124 (both untaxed: INFERRED weather).

### 6.8 Calibration and suites (MEASURED, Release, this tree)

- Calibration battery (inside Sim.Tests) green. `sim autoplay --seeds 4 --turns 650` + `sim corridors`: exit 0, every
  gated corridor in band; the two QUARANTINED corridors drift slightly further (density window breach 3.62 % → 4.30 %,
  migration 74.16 % → 74.21 % below their windows) — report-only by T3.12, no ratified gate fails, nothing retuned.
- Sim.Tests 1418 / 0 / 6 (new skip: the scarcity rig); Sim.Ui.Tests 472 / 0 / 0. Gates: banned constructs, read
  isolation, read-only proof, research content audit, calibration report and unlock audit `--check` all exit 0.
  ci.yml determinism-xproc legs run locally: orderless and ordered/replay toy legs, founded xproc (last hash =
  FOUNDED_GOLDEN), founded replay, aiEmpires = 1 leg (186 orders, kinds 3–8, replay equal, inspect VERIFIED).
- Not run here: `sim bench` (orchestrator benches once).

## 7. R2c — merged R2a + R2b (append-only, 2026-10-03)

| item | status | evidence (MEASURED on `m5i-r2c-reconcile`) |
|---|---|---|
| Trade research unlock (node 426) | IMPLEMENTED | TradeResearchUnlockTests; audit `--check` green |
| City-state research (local holder) | IMPLEMENTED | CityStateProgressionTests; preview `r2-previews/city-state-card.png` |
| Unrest-lite tax brake | IMPLEMENTED | UnrestTests; pushback table identical to §6 AFTER |
| Weather over geographic distance | IMPLEMENTED | HarvestWeatherGeographyTests |
| Uprising → city-state research, empty start | IMPLEMENTED / INFERRED | UnrestTests.ARevoltedSeat_… |
| Seeding a revolted settlement with its ruler's knowledge | OPEN | no ruling |
| 115 deferred entities | DEFERRED (0 realized) | docs/deferred-entity-realization-plan.md |
| Capital succession / tax reach after capital loss | DEFERRED | no ratified mechanism |
| Quarantined corridor window breaches (density, migration) | OPEN (pre-existing at f1fe76f) | autoplay 4×650 both trees |

Suites: Sim.Tests 1436 passed / 0 failed / 6 skipped (before the R2c test), Sim.Ui.Tests 473/473. Bench founded seed 42
300 turns, 3 alternated runs: f1fe76f median 34430.70 ms, R2c median 34789.20 ms, ratio 1.010.

## 8. R3 — final R2 reconciliation (append-only, 2026-10-03)

| row | status | evidence |
|---|---|---|
| Revolt → new AI polity, complete knowledge copy | IMPLEMENTED (RATIFIED §2) | KnowledgeMonotonicTests, RevoltTests, UnrestTests |
| Parent / child diverge independently | IMPLEMENTED | KnowledgeMonotonicTests.AfterSeparation_… |
| Annexation = union | IMPLEMENTED as domain operation; no caller (no conquest path) | KnowledgeMonotonicTests.Annexation_… |
| Capital loss corrupts nothing | IMPLEMENTED (tests); succession DEFERRED | UnrestTests.CapitalLoss_… |
| Food-sector label from capability | IMPLEMENTED | FoodSectorLabelTests |
| Turn-1 forager food via smaller founding population | STOPPED — measured fail at N=50/100/200/400 | r3 record §1 |
| T4.5 raider reachability | OPEN — CR-019 | RevoltTests.RevoltNoLongerProducesStatelessness_… |
