# ADR-038 — THE EMPIRE PURSE FOR CONSTRUCTION, INTRA-POLITY GRAIN RELIEF, AND THE GRANARY EFFECTS

**Status: PROPOSED — uncommitted, for the Director's review with D-053.**
- Written 2026-10-13/14 on the `m5w-integration` working tree (base `de86ace`). Not committed, pushed, merged or
  packaged.
- **Governing record:** `docs/d053-batch2-playtest-rulings.md` (D-053 §1–§3, §5). D-053 holds the decisions, the
  supersessions and the Director's questions. This ADR is the technical contract: data flow, invariants, switches,
  observability and the test map.
- **Touches no kernel contract and no save format.**
  - `CanonicalSchema.Version` stays 32.
  - No table, row field or `OrderKind` is added.
  - `Sim.Core/Kernel` is untouched.
  - New behaviour lives in three State helpers, four systems and their content switches.

## 1. What exists now

| Layer | New code | Systems changed | Switch (sim.json; shipped ON) | Control |
|---|---|---|---|---|
| §1 Empire purse | `State/EmpireStockpile.cs` (Holders / Available / Draw); `ConstructionQuery` claims (`Claimed`, `Unclaimed`, `ClaimedAt`, `ClaimedReason`, `QueueWithPending`, `HoldersSeenBy`) | Construction (gate + draw), AiConstructionPolicy (reservation); RoadDevelopment delegates its sum/draw (byte-identical) | `construction.empireStockpile` | `TestConfigs.PreEmpireConstructionStockpile` |
| §2 Grain relief | `Systems/Consumption/FoodRelief.cs` (pure allocator) | Consumption (three phases) | `consumption.empireFoodAllocation` | `TestConfigs.PreEmpireFoodAllocation` |
| §3 Granaries | `State/GranaryEffects.cs` (two saturating factors, bound once per config) | Production (farm), Demographics (dEff, via `FoodState.EffectiveDeficit` world overload) | `granaries.enabled` (+ four coefficients) | `TestConfigs.PreGranaryEffects` |

**The batch as a whole:**
- `TestConfigs.PreD053` turns all three layers off.
- Every older attribution twin (`PreForager` and its chain, `PreTimeAveragedHarvest`, `PreTaxAge`, `PreAiFounding`)
  strips all three, under the R4 convention.

## 2. §1 — the purse

- **Holders.**
  - A settlement's controller is resolved by `EmpireQuery.TryGetController` on PREV.
  - A controlled settlement draws from every settlement of that controller. The order is the builder first, then the
    rest nearest-first by exact integer squared site-cell distance, ties ascending `SettlementId`. This is
    `RoadDevelopmentQuery.PayingSettlements(world, polity, s, s)` with the builder pinned first; it differs only on a
    terrain-less world where every distance ties.
  - A stateless settlement, and every settlement of a world with no `ControlRow`, holds alone. Roads keep their own
    toy rule: a no-Controls world counts every settlement as the issuer's.
- **Draw.**
  - `Ledger.Flow` sinks under the existing reasons, min(remaining, held) per holder in order, `OverdrawPolicy.Throw`.
  - A pre-check refuses before any flow, and a post-condition throws if anything is left undrawn.
- **Gate.**
  - ConstructionSystem resolves heads in PREV `Settlements` row order (ascending id). Each head is checked and drawn
    on the NEXT `GoodStocks` table, so it sees what earlier heads took.
  - The M4-D whole-or-nothing gate and the builder's own labour capacity are unchanged.
  - `construction` runs before `roaddevelopment` (pipeline.json) on the same table, so no unit is spent twice.
- **Claims (the queue-freeze guard).**
  - `Claimed(good)` is the inputs of every queued project, at any slot, in the purse's settlements.
  - The obtainability guard tests a crafted input the empire cannot produce against the unclaimed holding. A raw
    input reads the pooled holding and stays transient.
  - Intake passes its NEXT queue, so an order accepted earlier in the same batch also claims.
  - The action list replays pending orders (`QueueWithPending`).
- **AI.** `AiConstructionPolicy` orders only what the purse covers after subtracting queued claims and the inputs of
  orders chosen earlier in the same decision. This applies to ordinary projects and to the university site.
- **Information class (ADR-037).**
  - Pooled figures ("empire has N") appear only where the viewer controls the settlement (`HoldersSeenBy`,
    `BlockerSeenBy`). A foreign settlement shows its own stock.
  - A `KnowledgeLeakTests` metamorphic case pins this: changing a hidden foreign settlement's stock does not change
    the text.

## 3. §2 — grain relief

**ConsumptionSystem with the switch ON:**

- **P1 — own eating.** Each settlement eats from its own store with the de86ace arithmetic, unchanged. The step records
  the nutritional requirement R and the food obtained F.
- **P2 — relief, per polity.**
  - Claimants are the polity's settlements with u = R − F > 0 and an EMPTY grain row.
  - Donors are those whose grain row still holds grain.
  - `FoodRelief.Allocate` water-fills max-min on u/R:
    - The level is found in exact `Int128` arithmetic.
    - Grants are floored.
    - Leftover units go one at a time by a composite key: remaining fraction descending (exact cross-multiplication),
      then PREV FoodState severity descending, then id ascending.
    - The result is a pure function of the claim multiset, so it is order-independent.
    - Σa = min(G, Σu), and 0 ≤ a ≤ u.
  - Each recipient, in ascending id, draws its grant nearest-first from donors (`Ledger.Transfer`) and eats it
    (`Ledger.Flow` Eaten).
  - Its deficit row and grain `LastConsumptionEatenUnits` are updated.
- **P3 — bound and spoil.** `BoundStore` (spoilage and the store bound) runs per settlement on the post-relief stocks,
  unchanged.

**Switch OFF.** The literal old interleaved loop runs, which is byte-identical to de86ace by construction.

**Limitations, stated in the code:**
- With the switch ON, a hand-built world may create first-use `LedgerFlow` rows in a different order. No grain moves,
  but the hash differs.
- A grant tops up to R = floor(exact requirement), so it can interact with the banked `ConsumeRemainder` by at most one
  unit per relief event.

**Observability (no schema change):**
- The per-settlement StoreLosses residual now carries one stated new term, intra-empire relief. It is negative at a
  recipient whose store closes at 0 and positive at its donors. The world-level identity is still exact.
- `FlowsSummary.UnattributedGrainTransfer` excludes residuals with the relief signature:
  - controlled;
  - polity sum ≥ 0;
  - closing = 0;
  - eaten > 0.
  It keeps its raid meaning (measured: 0 of 300 driven relief turns flagged; a first draft flagged 282).
- `docs/telemetry-schema.md`, the forensic schema text and the UI audit line say so.

## 4. §3 — the granary effects

- **FarmYieldFactor = 1 + B·(1 − e^(−m/1)).**
  - m is the granaries standing at the settlement, from PREV Structures; B = 0.05.
  - It is applied once in `ProductionSystem.Farm` to the cultivated grain rate, after the Leontief minimum and beside
    weather and extraction.
  - It is skipped when m = 0 or while `LabourActivities.HarvestsWildFood` is true.
- **FamineIntensityFactor = 1 − 0.25·(1 − e^(−n/2)).**
  - n is the granaries standing in the settlement's civilization, or its own count when stateless.
  - It multiplies the dead-zone effective deficit in `FoodState.EffectiveDeficit(world, s, d, state, cfg)`.
  - DemographicsSystem, Observer and CausalChain all call that overload, so the recorded effective deficit is the one
    integrated.
  - It is skipped when dEff = 0 or n = 0.
- **Shared form.** Both factors use the `InstitutionEffects.Saturation` form. Each is the literal 1.0 and skipped when
  absent; the project id is resolved once per config.
- **Loader validation.** Both maxima must be in [0, 1) and the scales finite and > 0. The project must name a
  goods.json project.
- **Diminishing returns, measured in double precision.**
  - Each further granary adds strictly less below #35 (farm) and #69 (famine). After that only rounding noise remains.
  - The cap (×1.05) and floor (×0.75) are never passed.
- **Not touched:** spoilage, the store bound, `disaster.hazardPerYear`. No storage claim is made; the UI labels that
  called the settlement's store "granaries" now say "grain store".

## 5. Determinism and conservation

- **Determinism.**
  - No banned construct; all three gate scripts pass.
  - Every ordering over doubles uses a composite key with an integer tie-break, and tie-dense tests cover the donor
    order, the relief leftovers and the purse.
  - Allocator permutation independence is property-tested (2,000 trials). A row-shuffled positioned rig pins the
    recipients' execution order.
  - The AI world gives byte-identical hash logs across two processes, and an emitted session replays hash-for-hash.
- **Conservation.**
  - Every new movement is `Ledger.Transfer` or `Ledger.Flow`, with `OverdrawPolicy.Throw`.
  - Ledger audits balance exactly in the new tests: purse draws, project-plus-road in one step, and relief.
  - World reconciliation holds on every driven turn with relief ON.

## 6. Re-pins (each measured, with every layer off, then one layer off at a time)

| Pin | Old → new | Cause (per-layer evidence) |
|---|---|---|
| `DrivenGoldenTests.Golden` | `7bcdc03f…` → `4f4e0b81…` | Relief alone: batch-off and relief-off give `7bcdc03f`; purse-off and granary-off give `4f4e0b81`. First divergence turn 5 = first relief turn. |
| AI world 300 turns (`AiFoundingAttributionTests.S1cTurn300`, `GranaryAttributionTests.GranaryAiTurn300`, CLI) | `aa7a87a9…` → `b2e4f333…` | Purse plus granaries. Batch-off gives `aa7a87a9`; purse-off `fd40bbaf…`; granary-off `f9e69db7…`; relief-off `b2e4f333` (relief never acts). Purse diverges at turn 5 (order stamped 4); granary at turn 222 (first acts 221). |
| `AiEmpireIntegrationTests` first-build turn | 9 → 4 | Purse alone. |
| Blocker text pins (TurnOne surfaces, Readability) | "(has 0)" → "(empire has 0)" | §1 wording. |
| `InfoPanelTests` granary effects text | → states the wild-food case | §3 wording. |
| `AiPolicyTests.AiConstruction_GranaryFirst…` | original assertions kept on `PreEmpireConstructionStockpile`, plus an ON arm | §1 changes the premise. |
| `CalibrationBatteryTests` seed 7 (FROZEN known red) | 124,455 → 124,483 (still red, same assertion) | Relief: one relief settlement-turn in 1,000. **No frozen literal touched.** |

**Not moved:**
- the founded golden `937577ff…` (and ci.yml `FOUNDED_GOLDEN`);
- every historical attribution constant;
- `GlassBoxUiTests` T2 (10) and `CliRevoltPolityProducerTests` (37), whose rigs were re-built in this batch.

## 7. Test map

- **§1:** `EmpireConstructionStockpileTests`:
  - donor order (tie-dense);
  - sibling-funded granary with exact per-row draws;
  - stateless; no-Controls;
  - cross-empire isolation;
  - competing heads;
  - central purse with no reservation between heads;
  - project + road in one step;
  - turn-exact delivery;
  - replay;
  - AI purse and reservation;
  - claims (same batch, later turn, any slot, raw-not-claimed, claimed-reason text);
  - the card agrees with the blocker;
  - foreign card;
  - switch-off and per-layer controls.

  Also `EmpireStockpileBlockerTests` (UI) and `KnowledgeLeakTests` (foreign queue head).
- **§2:** `EmpireFoodAllocationTests`:
  - adequate supply, shortage, water-filling, equal and distinct urgency, ties, zero food;
  - never exceeds available or need;
  - conservation, stateless, cross-empire;
  - permutation independence;
  - donor tie-break; execution order; empty-row claimant; contested control;
  - replay;
  - shipped-config reachability:
    - abandonment FAMINE when the polity has no spare grain;
    - a short polity's claimant still starves on its water-filled share.

  Also `StoreLossTests`. Premise rigs re-run on `PreEmpireFoodAllocation`: FamineScenario, Migration, Inspection T4.21,
  FoundedHarness, Explain starved rigs, GlassBox starved session, and the reconciliation starvation sample.
- **§3:**
  - `GranaryEffectsTests`: factor values at 0/1/2/5/50, diminishing, cap, local-only, cultivated-only, empire-wide
    famine, stateless, never raises dEff, observer recompute.
  - `GranaryAttributionTests`.
  - `GranaryCardTests`: cultivating and wild-food texts.
- Every new behavioural test has a mutant killed by a semantic test. Two surviving mutants are recorded as equivalent:
  `ProjectTreeTarget`'s input loop reading own stock (MA1d), and the redundant `Eaten > 0` arm of the relief signature.

## 8. Limitations carried forward

- **Partial pooling.** D-051 §6 is only partly executed (D-053 §5.2). Housing, recipe inputs, tool equipping, trade,
  prices, migration's food gate and colony provisions remain per settlement.
- **Urgency metric.** Relief ranks by the nominal deficit; the ranking across food states is a question for the
  Director (D-053 §6 Q1).
- **The famine factor is rarely visible.** In 300 AI turns it never acted, and relief removes most single-settlement
  famines while the polity has spare grain.
- **Pre-existing gap.** A crafted input held in part, below the required amount, and unclaimed still reads "needs more
  knowledge" on the card summary.
- **Old sessions.** Sessions recorded by an earlier build diverge where construction waited on sibling stock, relief
  applies, or a granary acts. The D-052 resume guard refuses them.

## Dated note — 2026-10-15 (D-054, ADR-039; appended, the sections above are unchanged)

Batch 3 (the Director's 2026-10-14 instruction) supersedes three statements of this record for the shipped
configuration. Each stays true of batch 2 alone.
- **§2 "Gate":** "ConstructionSystem resolves heads in PREV `Settlements` row order (ascending id)" and "The M4-D
  whole-or-nothing gate … unchanged".
  - With `sim.json` `construction.incremental` on, heads progress incrementally (the materials-first lockstep).
  - They are served oldest order first: EnqueuedTurn, then SettlementId, then Slot.
  - The draw is still `EmpireStockpile.Draw` in this record's donor order. That order is now measured on the cylinder
    (`MapTopology.SquaredPx`).
- **The header:** "Touches no kernel contract and no save format. `CanonicalSchema.Version` stays 32." Batch 3 moves the
  schema to v33 and adds order kinds 11 and 12 (ADR-039).
- **§6:** the `AiEmpireIntegrationTests` first-build pin, "9 → 4" here, is 2 on the cylinder (D-054 §7). The AI-world
  hash `b2e4f333…` is reproduced only through the batch-off pair and the v32 projection (`D054PairAttributionTests`).
