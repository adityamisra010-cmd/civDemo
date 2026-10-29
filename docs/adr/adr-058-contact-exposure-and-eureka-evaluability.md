# ADR-058 — Contact-carried foreign exposure, and Eureka evaluability over deposits and site terrain

**Status: ACCEPTED — D-067 R7 (2026-10-28) RULED the pathway: diffusion through trade contact only; conquest knowledge
(DW-021) and every other pathway (espionage, proximity, migration-borne) stay deferred. D-067 §7 records this ADR's status
as confirmed, with stale facts corrected (amendment log at the end).** First recorded as ADOPTED UNDER DELEGATION (D-066
§2 A7; the Director's instructions of 2026-10-28 to complete M6), 2026-10-28. A7 adopted the CONTACT half of the spec's
recommendation, which was contact AND conquest (m6-spec T6.10: "contact and conquest recommended"); D-066's "the spec's
recommendation" is not literal, as D-067 R7 records.
Reversible by one content switch (`research.json` `tuning.foreignExposure.enabled`) and by restoring seven Eureka
conditions; confirmed at the M6 exit gate (D-067 R7, §7).

**Touches contracts:** the ADR-029 addendum A input seam `ResearchExposureRow` gains its writer (a new system, SystemId
31); the research dialect gains three quantity families. **No schema change** (the table is schema v26 section 47; the
schema stays v39). **No tree-closure change:** Eurekas are not in the TC-1/TC-2 STRUCTURE or TEXT projection
(`TreeClosureBaselineTests.Hashes`), and no Eureka text changes.

## A. The exposure writer (D-066 A7: contact- and trade-carried exposure only)

### A.1 The rule

`DiffusionSystem` (SystemId 31, `pipeline.json` last, `Sim.Core/Systems/Diffusion/`) is the ONE writer of
`ResearchExposures`. It reads only PREV. For every Empire of the roster, in table order:

1. **Contact (the carrier).** A `TradeFlowRow` of PREV (TradeArbitrageSystem's observable of the step that produced it)
   joins a settlement the Empire controls to a settlement it does not control — another Empire's or a city state's.
   Contact is binary per partner settlement; the quantity is not read. Domestic trade carries nothing.
2. **No information leak.** The partner counts only if the Empire KNOWS that settlement through THE information-class
   seam, `PolityKnowledge.Of(world, polity, cfg).KnowsSettlement` (controlled, visible, or — fog on — sighted, rule E5).
   With the fog off every settlement is known (the pre-M6 answer). An unsighted partner exposes nothing, whatever it knows.
3. **What is carried.** The partner settlement's knowledge, `SettlementKnowledge.MaskOf` (its controller's completed
   nodes ∪ its own; a city state's own).
4. **The offer.** For every node the Empire may research NOW (`ResearchQuery.AvailableMask`, the availability the engine
   pays on) that a contact partner knows, in content index order: Offered ← max(Offered, min(c × BaseCost,
   Offered + r × dtYears × BaseCost)), with r = `tuning.foreignExposure.ratePerYear` (0.01, TUNE) and c =
   `accelerationCreditCeilingFraction` (0.40). `DiffusionQuery.NextOffered` is the one formula. Monotone by
   construction; a hand-written offer above the cap is never lowered. One row per (polity, node).
5. **The payment** is unchanged ADR-029 addendum A: ResearchSystem (reading PREV) pays the unpaid part of the cumulative
   offer from the ONE pool shared with Eureka (≤ 0.40 × BaseCost in total, ≤ the remaining EffectiveCost), provenance in
   `ResearchCreditRow` source 2.

### A.2 Dimensions and laws

- Offered and credit are RP (the node's cost unit), doubles (law 7; research is not a conserved stock, law 1 untouched).
- r is a fraction of BaseCost per SIM-YEAR, integrated with `dtYears` (law 3): ten years of contact offer 0.10 ×
  BaseCost at dt 10 (one step) and at dt 1 (ten steps) — `DiffusionExposureTests.Offer_IsPerSimYear_…`.
- Determinism (law 5): roster table order, content index order, partner settlement ids sorted ascending (integers);
  no RNG, no dictionary, no LINQ, no ordering over doubles. Isolation (law 6): the system references only State queries
  and its own table; ResearchSystem is unchanged in behaviour and reads the seam from PREV as before.
- No calendar gate (law 4): nothing reads a date or an Age.

### A.3 Delivery semantics (turn-exact)

An offer written in step t→t+1 is in state t+1 and is paid in step t+1→t+2 — the one-step lag of single-writer
ownership (the InstitutionsSystem → ResearchCostModifiers precedent). Pinned by
`DiffusionExposureTests.Contact_OffersRateTimesDtTimesBaseCost_PaidOneStepLater_UpToTheCeiling` (offered 250, 500, 750,
1000, 1000; paid 0, 250, 500, 750, 1000).

### A.4 Scope decisions (new, recorded under the D-066 decision rules)

- **Receivers are Empires; city states are carriers only.** A city state has no fog record (no sightings), so the
  leak-free contact predicate is defined for Empires; whether city states also absorb foreign knowledge belongs with the
  undecided city-state knowledge rule D-066 lists as "not decided here". Simplest, reversible.
- **Only available nodes accrue.** Exposure is a bounded advance on work the Empire can do now — never knowledge whose
  prerequisites it lacks, never a completion (the spec's "brake if accelerating": ≤ the pool, ≤ r of BaseCost a year).
- **Binary contact, no stacking across partners.** Several partners knowing the node offer at the same rate as one.
  Volume-weighted diffusion is a later refinement (queue).
- **r = 0.01 per sim-year (TUNE, CHOSEN):** the exposure share of the pool fills in about 40 years of contact — four
  10-year turns early, forty 1-year turns late. Not calibrated against a band; the playtest is the measurement.
- Conquest knowledge stays DW-021 (M7/M8); research agreements, espionage and the other ledger §12 pathways are later
  (D-066 A7).

### A.5 The switch

`tuning.foreignExposure` `{ "enabled": true, "ratePerYear": 0.01 }` ships ON (the Director's instruction to complete M6).
`enabled: false` or the block absent: the system returns at once, no row is ever written — byte-identical to the block
absent (`DiffusionExposureTests.SwitchOff_WritesNothing_AndEqualsTheBlockAbsent`). The loader refuses a rate outside
(0, 1].

### A.6 Measured: dormant in the canonical AI world until both sides know trade

The carrier is TradeArbitrageSystem's flows, and trade is research-gated at BOTH endpoints (R2a, `TradeQuery.CanTrade`).
MEASURED on the canonical AI world (seed 42, one AI Empire, 600 turns, a one-off diagnostic run by the agent writing this
section): no settlement knows trade until turn ~540; after it, the AI's 15 flows are all domestic; city states (research
at 0.25 pace) and the order-less player never learn trade by turn 600 — so no foreign flow exists and no exposure row is
written. This is the trade gate working, not a defect of the writer: exposure starts once two polities that know trade
trade with each other within sight (the player researching trade near an AI or a trading city state). Whether contact
should also be carried by something cheaper than formal trade (proximity, migration) is a design question for the
calibration pass, recorded in `docs/queue.md`.

## B. Eureka evaluability over existing computed state

### B.1 The dialect

Quantity ids of the research dialect (`EurekaQuantities`), with G goods: `stock_<good>` (0..G−1, unchanged),
`deposit_<good>` (G..2G−1), `site_river` (2G), `site_coast` (2G+1), and the `stock_`/`deposit_` names of a good its
goods.json packet removed (≥ 2G+2, always 0 — the packet-off config loads the same research.json; the sim.json
`concealableGoods` precedent). All settlement-scoped: a condition holds if it holds in one settlement the holder
controls (`ResearchQuery.EurekaHolds`, unchanged shape).

- `deposit_<good>` = `DepositConcealment.KnownAbundance(world, cfg, settlement, good)` — the abundance AS THE HOLDER
  KNOWS IT: a concealed, unsurveyed deposit reads 0 (no leak; Prospect reveals it). ResearchSystem now receives the
  step's `SimConfig` (SystemCatalog), and the research screen passes the session config, so the Glass Box's "holds now"
  never shows a concealed deposit.
- `site_river` / `site_coast` = a river cell / an open-water cell (water ≥ 0.5, not river) in the knowledge-lattice node
  the settlement stands on (its own node, always known to its holder). False without terrain.

### B.2 The seven

| node | condition | grounding |
|---|---|---|
| `dugout` | `deposit_timber > 0 AND (site_river > 0 OR site_coast > 0)` | trees and water at the site; "navigable" not modelled |
| `fishing_hook` | `deposit_fish > 0` | fish's deposit channel is water |
| `salt_extraction` | `site_coast > 0` | the sea-coast alternative (brine springs, salt pans not modelled) |
| `sail_square` | `site_river > 0 OR site_coast > 0` | winds not modelled |
| `coke` | `deposit_coal > 0` | coal is concealable: only once prospected |
| `gunpowder` | `deposit_saltpetre > 0 OR stock_saltpetre > 0` | saltpetre known or held |
| `sulphuric_acid` | `stock_sulphur > 0 AND stock_saltpetre > 0` | the inputs held (the activity itself not modelled) |

Counts: 86 Eurekas; evaluable 14 → 21; future-system 72 → 65. Every one of the 86 is classified in
`docs/m6-plan/eureka-evaluability-register.md` (status, condition or owning system and target milestone), checked
against the content by `EurekaEvaluabilityTests.Register_ListsEveryEurekaExactlyOnce_…`. The loader's dead- and
implied-Eureka validators run on load and pass (they skip quantity conditions, which no prerequisite can imply).

## C. Goldens

MEASURED (KNOW tree; `KnowAttributionTests`, `AiFoundingAttributionTests`): **only the AI-world pin moves.** The founded
(`d5ebd0b2…`), driven (`55afd498…`) and first-reign (`064ed6b7…`) pins are UNMOVED — their pin tests passed unchanged on the KNOW track's tree and are unchanged on the integrated tree: no
foreign trade with a known partner and no newly evaluable Eureka acts in those worlds by their last turn. The AI world
(seed 42, turn 300) moved `ac4554bf…` → `a519b5c9…` (economy projection `3153b7e3…` → `5edcc3e0…`), and the whole move is
§B's Eurekas: three of the seven fire in that world by turn 300, while the exposure writer writes no row by then (the
exposure-off twin `TestConfigs.PreForeignExposure` is the live world). On the integrated M6 tree, combined with D-066 A18,
the shipped AI-world pin is `5458cb3d…` (`docs/m6-plan/golden-attribution-register.md`, dated notes of 2026-10-28). The
attribution control is `TestConfigs.PreKnow` — the shipped research.json with `tuning.foreignExposure` removed and the
seven conditions restored — under which the KNOW tree's base pins return byte for byte (the inert diffusion slot writes
nothing); on the integrated tree `PreKnow` returns the A18-only ("FOG-only") pin `7aed09b9…`.
By the R4 rule every older twin strips the layer (through `PreMilitary`, the packet twins, `PreAiRecruitment` and the
shipped-JSON research twins). The measured old → new table is in the KNOW hand-back and on each pin's comment.

## D. Not done here

- Groundable-now Eurekas left out of this pass (resources/terrain scope): `road_paved` (TransportEdges), `quantum_mechanics`
  (InstitutionRow). Groundable later with a ruled threshold: `irrigation_canal` (aridity), `terrace` (slope), `water_mill`
  (river gradient). Recorded in the register.
- Exposure volume weighting; city-state receivers; every pathway other than trade contact.

## Amendment log — 2026-10-28 (D-067)

Authority: `docs/d067-m6-exit-gate-director-rulings.md` R7 (§1: "RULED: diffusion through trade contact only"; the
correction "A7 adopted the contact half") and §7 ("status CONFIRMED, with stale facts corrected"). This ADR was
uncommitted and not yet confirmed, so stale FACTS were corrected in place; no design decision is changed. Line numbers
marked "was" are those of the staged pre-amendment text; evidence was read from the live tree on 2026-10-28. D-067 is cited by ruling and section, not by line: it is untracked and was being amended concurrently.

| # | Where | Old | New | Evidence |
|---|---|---|---|---|
| 1 | Status line (was :3) | "Status: ADOPTED UNDER DELEGATION (D-066 §2 A7; the Director's instructions of 2026-10-28 to complete M6), 2026-10-28." | "Status: ACCEPTED — D-067 R7 (2026-10-28) RULED the pathway: diffusion through trade contact only; conquest knowledge (DW-021) and every other pathway … stay deferred. D-067 §7 records this ADR's status as confirmed …" The original adoption is kept as history. | D-067 §1 (R7) and §7 |
| 2 | Status paragraph (new sentence) | (none) | A7 adopted the CONTACT half of the spec's contact-and-conquest recommendation; D-066's "the spec's recommendation" is not literal (D-067 R7) | `docs/m6-spec.md:215` ("contact and conquest recommended"); D-067 §1 (R7) |
| 3 | Status paragraph (was :5) | "confirmation requested at the M6 exit gate with the other D-066 ADOPTED items." | "confirmed at the M6 exit gate (D-067 R7, §7)." | as #1 |
| 4 | §C (was :121–123) | "Both parts move every world that researches: the founded, driven, first-reign and AI-world pins. … under which every base-tree pin returns byte for byte (the inert diffusion slot writes nothing)." | Only the AI-world pin moves. Founded `d5ebd0b2…`, driven `55afd498…`, first reign `064ed6b7…` UNMOVED. AI world `ac4554bf…` → `a519b5c9…` (projection `3153b7e3…` → `5edcc3e0…`), the whole move from §B's Eurekas (three of the seven fire by turn 300; no exposure row by turn 300). Integrated with D-066 A18: `5458cb3d…`. `PreKnow` returns the KNOW tree's base pins; on the integrated tree it returns the A18-only pin `7aed09b9…`. | `Sim.Tests/Kernel/KnowAttributionTests.cs:14–20` (founded, driven, first reign "UNMOVED"; AI world "MOVES"), `:44` (0 exposure rows), `:45` (3 more Eurekas than the twin), `:48–49` (`PreForeignExposure` = live); `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:120–122` (cause: "three of the seven Eurekas … fire"), `:129` (`5458cb3d…`), `:132` (`a519b5c9…`), `:147` (`7aed09b9…`, what `PreKnow` returns); `SnapshotTests.cs:1184`, `DrivenGoldenTests.cs:118`, `FirstReignTests.cs:58`; `docs/m6-plan/golden-attribution-register.md:64, :70, :74` |

**Not changed:** §A.4's pathway scope (conquest DW-021; other pathways later) and §D already match D-067 R7. D-067 names
no separate ruling on §B (Eureka evaluability); its status rests on D-067 §7's listing of this ADR as confirmed.
