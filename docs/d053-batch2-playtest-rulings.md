# D-053 — THE DIRECTOR'S 2026-10-13 POST-PLAYTEST BATCH: WHAT WAS BUILT, WHAT STOPPED, WHAT IT SUPERSEDES

**Status: RECORDED 2026-10-13/14, uncommitted, for the Director's review.**
- The instructions are the Director's own: the batch beginning "You are implementing the approved next batch of
  changes", items 1–8.
- This record quotes them and records how they were carried out.
- Lines marked **CONFIRM** are consequences found during implementation that the instruction did not state in so many
  words. They are listed in §6 for an explicit answer.
- Items 3, 4 and 6 were not implemented; CR-021 holds their impact analysis.

**Not an M5 exit record, and not authorisation to publish.** M5 stays NOT complete (D-052 §15). Nothing here is
committed, pushed, merged, tagged or packaged.

---

## §0 Source (verbatim, the Director, 2026-10-13)

> **1.** "Implement deterministic, lossless, immediate intra-empire resource access for construction and roads. A
> settlement must be able to use eligible resources held by other settlements in the same empire. Define and document
> a stable donor-selection and consumption order. Preserve resource conservation … Avoid save-format changes if
> possible … Preserve existing spoilage behavior and storage rules except for the granary effects explicitly approved
> below."
>
> **2.** "Allocate available empire food by critical need, not equal shares and not merely proportional demand. Define
> a deterministic urgency metric based on simulation-backed food needs and starvation risk, then deterministic
> tie-breaks. … never allocates more food than is available or more than the applicable demand/storage rules permit.
> … Do not introduce new demographic or mortality behavior without support from existing rules."
>
> **5.** "Famine intensity reduction is empire-wide and depends on the number of granaries in the empire. Farm-production
> bonus applies locally to the settlement containing each granary. Both effects must have diminishing returns and
> explicit upper bounds. … calibrate conservative, documented coefficients … Keep the currently approved famine-disaster
> setting unchanged. … Do not claim granaries increase storage …"

Boundaries (same message):
- no save-format change unless reported first;
- no frozen calibration reference changed;
- stop and report on a frozen-decision conflict;
- no design value invented silently.

## §1 Construction and roads draw on the empire stockpile (item 1)

- **Who draws.** A settlement that an empire controls (`EmpireQuery.TryGetController`, turn-start control) checks
  availability against, and draws from, the stocks of **every settlement that empire controls**.
  - A stateless settlement is its own stockpile (D-051 §6).
  - So is every settlement in a toy world with no `ControlRow` at all.
- **Donor order (stable, documented).** The building settlement first. Then the empire's other settlements
  nearest-first, by exact integer squared site-cell distance, with ties to the lower settlement id. This is ADR-032
  Final Ruling 1's road order with the builder pinned first.
  - A draw takes min(remaining, held) from each holder in turn, as `Ledger.Flow` sinks under the existing reasons.
  - Nothing moves between settlements first.
  - Goods stay physically in their settlements' rows.
- **Competing projects.** Queue heads are tried in ascending settlement id, each against what earlier heads left
  this step.
  - The M4-D gate is unchanged: a head is built whole or waits; capacity is still the building settlement's own labour.
  - A lower-id head that passes its gates draws first.
- **Projects and roads.** `construction` runs before `roaddevelopment` (pipeline.json). Both draw on the same
  turn-in-progress table, so no unit is consumed twice.
  - This fixed priority (buildings, then roads) is what exists until item 4 is ruled (CR-021 §B).
- **AI and the queue-freeze guard.**
  - **The AI reserves the purse within one decision.** Before ordering, it subtracts the inputs of every project
    already queued in the empire (any slot) and of every order chosen earlier in the same turn. So it never orders more
    than the purse can pay now.
  - **The obtainability guard counts only the unclaimed purse.** For a crafted input the empire cannot currently
    produce (tools before bronze, for example), the guard reads the empire's holding minus every queued project's claim
    on it. This closes the ADR-033 D3 / D-051 §4 queue-freeze trap that a shared purse would otherwise re-open (no
    cancel order exists).
  - **Raw inputs are not claimed.** They keep the pooled "any stock or deposit" reading and stay transient blockers.
  - **Pending orders count.** Orders placed earlier in the same turn count as claims in the action list, so a second
    workshop against the same tools is not offered.
  - **The claimed case is named.** When the guard refuses because the input is held but claimed, the card and the
    action line say so, for example "needs 10 tools; the empire's 10 are already claimed by projects queued at S0".
- **Information class.** The pooled figure ("needs 20 stone (empire has 6)") is shown only for a settlement the viewer
  controls. A foreign settlement shows its own stock, so nothing leaks once M6's known extent exists (ADR-037).
- **Switch:** `sim.json` `construction.empireStockpile` (shipped ON; absent or false = the `de86ace` behaviour).

## §2 Food relief in a shortage (item 2) — intra-polity grain relief

- **When.** After every settlement has eaten from its own store, exactly as before.
- **Who claims.** A controlled settlement still short of food whose grain row is empty. A settlement holding grain is
  a donor.
- **What it gets.** Its polity's spare grain, allocated by **max-min water-filling on the unmet fraction** u/R.
  - u is the unmet nutritional need and R the requirement, so u/R is the nominal deficit d that FoodState, the
    effective deficit, starvation and fertility already read.
  - The worst-off is served first and brought down to the next-worst.
  - Whole-unit remainders follow a composite key: remaining fraction descending (compared exactly by integer
    cross-multiplication), then the previous turn's food state descending, then settlement id ascending.
- **Guarantees.**
  - Σ grants = min(spare grain, Σ unmet).
  - No grant exceeds its need.
  - The result does not depend on table order.
- **Delivery.** Each recipient, in ascending id, draws its grant from donors nearest-first (the §1 order, by
  `Ledger.Transfer`) and eats it (`Ledger.Flow` Eaten).
- **After relief.** The store bound and spoilage then run per settlement, unchanged.
- **Scope of what moves.**
  - Only grain moves (the staple every shortfall already substitutes to).
  - A stateless settlement neither gives nor receives.
- **No new demographic or mortality rule.** Starvation, fertility, flight and FoodState read the existing deficit
  ratio, which now reflects the food a settlement actually received.
- **The urgency metric is the nominal deficit.**
  - Within one food state it ranks exactly by starvation risk.
  - Across states it does not: a FAMINE-class settlement, which has no adaptation dead zone, at d = 0.25 has effective
    deficit 0.25, while a SEVERE one at d = 0.30 has 0.125.
  - The kernel's food state for the next step depends on tables other systems write in this step, so Consumption
    cannot rank by it without a cross-system read or a one-turn-lagged guess. **CONFIRM** (§6 Q1).
- **Observability.**
  - The per-settlement "store losses" residual now also carries intra-empire relief: negative at a recipient, positive
    at its donors.
  - The world-level identity is still exact.
  - The raid detector excludes the relief signature. Measured on the driven world: 0 of 300 relief turns are flagged as
    raids, where the first draft flagged 282.
- **Switch:** `sim.json` `consumption.empireFoodAllocation` (shipped ON; absent or false = the `de86ace` behaviour).

## §3 Granary effects (item 5)

- **Local farm output.** FarmYieldFactor = 1 + B·(1 − e^(−m/1)), where m is the granaries standing at that settlement.
  - It applies to the **cultivated** grain harvest only. It does not apply to pre-cultivation wild-food gathering
    ("farm-production bonus").
  - It is applied once, after the Leontief minimum.
  - While a settlement still gathers wild food, the card and the build button say it has no effect there.
  - **B = 0.05 (TUNE).** A granary recovers at most about half of the 10% storage, vermin and threshing loss already
    inside ADR-013's DERIVED yield (×0.90); threshing loss no store can save.
  - **CONFIRM** (§6 Q2).
- **Empire-wide famine intensity.** FamineIntensityFactor = 1 − 0.25·(1 − e^(−n/2)) multiplies the effective deficit,
  the one input of starvation mortality and fertility suppression.
  - n is the granaries in every settlement of the settlement's empire; a stateless settlement counts its own.
  - 0.25 and the scale 2 are TUNE: one granary per settlement of a ~6-settlement polity reaches the 1 − e^(−3) reading.
  - n is an absolute count: one granary relieves a 1-settlement and a 60-settlement civilization alike.
  - **No grain moves.** The coefficient stands for organised relief capacity, not stored food. Granaries are not
    claimed to increase storage: the store bound (1.5 years of demand) and spoilage (0.08 per year) are unchanged.
- **Bounds.** Both factors saturate and are bounded (farm ≤ ×1.05, famine ≥ ×0.75). Each is the literal 1.0 with no
  granary.
- **Unchanged.** The famine-disaster hazard (`disaster.hazardPerYear` 0.0), the Age II "Communal store" milestone and
  the Age III levy relief.
- **Switch:** `sim.json` `granaries.enabled` (shipped ON).

## §4 Items 3, 4 and 6 — NOT implemented (CR-021)

| Item | Why it stops | Smallest safe plan |
|---|---|---|
| 3 Incremental construction | M4-D (CERTIFIED): "built whole this turn or waits unchanged … no fractional progress"; `ConstructionQueueRow` deliberately has no progress field. Partial progress needs a stored field, so schema v33 (D-052 §5). | CR-021 §A |
| 4 Editable buildings/roads ratios | A standing per-empire setting needs a new table (v33) and a new order kind (frozen kernel; kind 10 is earmarked for M7). Its meaning also needs a ruling, because roads are a one-turn action. | CR-021 §B |
| 6 East-west wrap gated by exploration | There is no explored-extent state (D-052 §5 puts it in M6, v33). A true cylinder reopens ADR-032 Final Ruling 3 ("flat square raster (no wrap)"), D-015 and the T3.1(b) ocean ring. A render-only wrap is the substitute the Director ruled out. | CR-021 §C |

From item 3, **"no unit consumed by both a project and a road"** is delivered by §1.

## §5 What this supersedes, and what it does not

1. **D-052 §11** and the dated notes under **D-051 §6** (2026-10-10, 2026-10-12: "no partial pooling and no
   relief-pooling substitute") are superseded by the Director's 2026-10-13 instruction, for §1 and §2 of this record
   only.
2. **D-051 §6** (one central stockpile) stays RATIFIED and OPEN for everything this batch does not pool:
   - housing materials;
   - production recipe inputs;
   - tool equipping;
   - trade;
   - prices;
   - migration's food gate;
   - colony provisions;
   - livestock and fish;
   - spoilage and the store bound "applied to the central stockpile".
3. **D-042 §4.3** ("Shared ownership does NOT create a single Empire-wide inventory … moving them between settlements
   requires the appropriate transport/trade/logistics mechanism") and **§10.1** ("Empire-level control does not imply
   instantaneous empire-wide access").
   - D-051 §6 (ratified 2026-10-07, later than D-042) reversed both in substance but did not name them. They are named
     here, for §1 and §2.
   - Physical location is kept: goods still sit in their settlements' rows, and spoilage and the bound are per
     settlement.
   - **CONFIRM** (§6 Q3).
4. **M4-D:** "Materials leave settlement good stocks all-or-nothing" now reads "leave the empire's settlements' good
   stocks, all-or-nothing". The whole-or-nothing gate, the queue and the capacity rule are unchanged.
5. **CR-015 / F-138 (RATIFIED):** "deliberate abandonment … is what makes famine reachable in play today".
   - For a controlled settlement, FAMINE (and its starvation and revolt consequences) now needs its polity to have no
     spare grain: relief feeds an abandoned settlement first.
   - A stateless settlement, or a polity short overall, still reaches it, and tests pin both.
   - D-051 §6 already supersedes CR-015's per-settlement semantics for controlled settlements. **CONFIRM** (§6 Q4).
6. **Not touched:**
   - CanonicalSchema v32: no table, row field or order kind added;
   - every CR-020 / D-052 §9 frozen calibration reference;
   - the famine-disaster hazard;
   - spoilage and the store bound;
   - ADR-032's road order.

## §6 Questions for the Director

1. **Food urgency metric.** Keep the nominal deficit, or rank by the effective deficit? The latter would need a ruled
   timing for "the food state the kernel will apply".
2. **Granary farm bonus.** Cap at 5%, derived against ADR-013's in-chain loss, or the 10% first proposed?
3. **D-042 §4.3 / §10.1.** Confirm they are superseded by D-051 §6 for construction, roads and shortage food.
4. **F-138.** Confirm that a controlled settlement's deliberate abandonment no longer produces famine while its empire
   has spare grain.
5. **CR-021.** The rulings it lists for items 3, 4 and 6.

## §7 Measured effects on the shipped worlds (combined tree; ADR-038 has the method)

- **Founded seed-42 world** (no orders): unchanged, `937577ff…`. Nothing is built, no settlement claims relief, and no
  granary stands.
- **AI world** (`--ai-empires 1`, seed 42, 300 turns): `aa7a87a9…` → `b2e4f333…`.
  - The purse moves the AI's first granary order from turn 9 to turn 4.
  - The granaries first act on cultivated grain at turn 221.
  - Relief never acts in this world.
  - The famine factor never acted either: 0 settlement-steps had an effective deficit in a civilization with a
    granary.
- **Driven world** (the order-driven golden): `7bcdc03f…` → `4f4e0b81…`, by relief alone.
  - Relief happens on 282 of 300 turns.
  - Starvation falls to 0.
  - Population at turn 300 rises from 8,332 to 16,451.
- **Calibration:**
  - Seed 42: CR-020 red unchanged, same message.
  - Seed 7: population 124,455 → 124,483 (one relief settlement-turn in 1,000), still red at the same assertion.
  - Every canonical metric is bit-identical.
  - No new red.
