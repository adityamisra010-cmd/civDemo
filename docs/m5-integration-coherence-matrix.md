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
| U | Units | OK | OK | OK | modernize via AI Age | OK | OK | no military orders (M6) |
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
   milestone** [E29] — needs recruitment (M6) or a content ruling.
4. **Dormant M4 conflict loop** [E16–E19] — no colony, starvation, revolt or raid in the order-free
   reference runs; CR-016 open.
5. **Weather correlation rides road-aware distance** [E14] — whether it should use geographic distance.
6. **No military orders** — armies and mobile agents are M6.
7. **Straight road geometry** — routes are drawn and costed straight (transport frozen).
8. **Capital relocation** [E22] — a revolted capital stays the capital; governance reach is anchored on it.
9. **Revolted settlements refuse orders** — once control is lost, labour/construction orders are refused
   by the control predicate; there is no reconquest or re-annexation order.
10. **`aiEmpires` defaults to 0** — the option exists (CLI, UI) but a default game has no rival.
11. **A1 yield = cereal yield (CR-006)** — the founding food activity yields at the cereal rate.
12. Also still open from the audit: AI labour decisions [E3]; claims/recognitions/notables have no
    writer [E20]; Age-eligibility table read only by the schema [E30]; harvest weather invisible [E34].
