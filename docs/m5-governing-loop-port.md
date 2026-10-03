# M5 governing loop — the port onto the integrated tree (ADR-033 D4)

**Status:** implemented on `m5i-s1-governing-loop` (cut from `m5-integration` @ `ffbb6f8`), awaiting
reconciliation into `m5-integration` and the Director's acceptance. Nothing here is merged to `main`.
Every number below was measured on this branch by the agent writing it (ADR-015 §6) unless it says
INFERRED.

**Source.** `origin/m5-full-build` @ `9a79d1e` ("M5B") = `dbef61a` (the M4 baseline, schema v24) + four
commits (`83c8c1a`, `3c8d271`, `6508a33`, `9a79d1e`), 234 commits (205 non-merge) behind this tree
(`git rev-list --count origin/m5-full-build..ffbb6f8`). Nothing was
cherry-picked: the mechanism was re-written by hand onto the current code (schema v29, research,
Ages, roads), and the M5B tests were re-anchored and extended.

---

## 1. What was ported, and where it lives now

| M5B element | On this tree |
|---|---|
| `TaxPolicyRow(Polity, Rate)` | `Sim.Core/State/WorldState.cs` — schema **v30**, appended after `RoadDevelopments` (12 bytes/row) |
| `OrderKind.SetTaxRate = 5` | `Sim.Core/Kernel/OrderLog.cs` — load validation (Amount in [0,100], TargetId ≥ 0); `OrderValidation.cs` — actor must equal target (+ the existing roster check) |
| `GovernanceSystem`, SystemId 22 | `Sim.Core/Systems/Governance/GovernanceSystem.cs`; `SystemCatalog.Governance`; `pipeline.json` after `revolt`, before `demographics` (22 systems) |
| `Governance` readers | `Sim.Core/State/Governance.cs` — `NominalTaxRate`, `HasPolicy`, `AdministrativeReach` (the writer's computation), `ControlStrength` (the stored reach), `EffectiveTaxRate`, `ExtractionMultiplier`, `Legitimacy`, **`CanLevyTax`** (new), **`TaxOrder`** (new, the one order constructor) |
| `TaxSufficiency` | `Sim.Core/State/SettlementHappiness.cs:180-181`, multiplied in `Of` (`:235`) |
| extraction multiplier | `Sim.Core/Systems/Production/ProductionSystem.cs:177` (computed once per settlement), applied at farming `:277`, the deposit sectors (herding/fishing and extraction) and crafting |
| `governance` config | `sim.json` `governance` + `SimConfig.Governance` (defaulted tail) — `authorityDecayCostUnits` 25.0, `taxExtractionResponseMax` 0.3 (TUNE), **`taxationRequires`** (new), **`ai`** (new: 60 / 35 / 5 / 40) |
| `AiGovernance` | `Sim.Core/Systems/Governance/AiGovernance.cs` — a pure policy, `OrdersFor(world, cfg, polity, turn)`; NOT wired (stream S2's single AI producer calls it) |
| `TaxOrderFactory`, `UiSession.EmitTaxOrder` | `Sim.Ui/ViewModel/TaxOrderFactory.cs`, `Sim.Ui/UiSession.cs`; the panel is stream U2's |
| tests | `GovernanceTests` (M5B's 25 cases re-anchored + the port's), `GovernanceTimingTests`, `GovernanceSchemaTests`, `TaxControlTests` (M5B's 8), `ForensicCitationTests`, the v30 attribution layer |
| CR-008 | imported byte-identical from `9a79d1e` (its last diff only records the Director's closing ruling) + an append-only import note |

**Not imported** (stale or superseded): `docs/progression-architecture.md` (superseded by D-044,
D-047, ADR-031), the T5.1 foundations audit (written against `dbef61a`), the implementation inventory,
the v25 schema report, M5B's v24-era attribution constants and its `HashAtSchemaV24(world, bool)`
helper, M5B's `SimUiGame` edits, the `UiSession.EndTurn` AI wiring, and CR-005's "Option C" text
(CR-005 stays OPEN; a dated note records why).

## 2. What changed against M5B, and why

1. **Schema v30, v27 unused.** `Snapshot.Load` requires an exact version and v28/v29 already exist, so
   M5B's "v25" (later reserved as v27) cannot ship. TaxPolicies is v30; the v27 reservation is recorded
   as permanently unused in `CanonicalSchema`'s version notes (appended; earlier notes untouched).
2. **Taxation is research-gated by the content** (ADR-033 D4). `sim.json`
   `governance.taxationRequires: "arithmetic_babylonian OR surveying OR standard_weights OR
   coinage_electrum"` — verified: these are exactly the four `research.json` nodes whose
   `unlocks.capabilities` name taxation ("tax assessment", "taxation by area", "taxation by weight",
   "taxation in coin"); `proto_writing` and `cuneiform` mention tax only in their descriptions. The
   expression is parsed by the research dialect's own parser
   (`ResearchContentLoader.ParseRequirement`, node ids only, no `NOT`, no comparisons), validated where
   sim.json meets research.json (the four-stream load fails on an unknown id), and evaluated by the
   existing knowledge evaluator (`ResearchQuery.RequirementMet` shares the atom reader of
   `IsKnowledgeEligible`; no second evaluator). ONE predicate, `Governance.CanLevyTax(world, cfg,
   polity)`: GovernanceSystem applies a SetTaxRate only when it holds on PREV (an order failing it
   changes nothing; the last valid order of a turn wins — the ResearchSystem pattern), the AI valve acts
   only when it holds, `UiSession.EmitTaxOrder` refuses when it does not, and the available-actions
   query (stream S2) asks the same function. No node id is named in C#
   (`GovernanceTests.TheGateIsTheCONFIGUREDExpression_NoNodeIsNamedInCode`).
3. **One fact, one place.** M5B wrote `ControlRow.Strength = reach` and never read it, while production
   and happiness recomputed reach. Now `AdministrativeReach` is called by exactly one writer
   (GovernanceSystem, on PREV) and stored in exactly one field; `EffectiveTaxRate` = declared rate ×
   `ControlRow.Strength`, and production, happiness, legitimacy and the UI read it. Pinned by
   `TheEffectiveRateReadsTheSTOREDStrength_NothingRecomputesReachBesideIt` (moving the distance alone
   moves no consumer; moving Strength moves every consumer).
4. **Inert without a governance section.** M5B's reach THREW for any rig that ran the full catalog with
   two or more controlled settlements and no `governance` section. Now: no section → GovernanceSystem
   returns at once (no tax row applied, Strength keeps what founding wrote), and every reader is neutral
   (effective rate 0, extraction ×1, burden ×1, `CanLevyTax` false). Pinned by
   `WithoutAGovernanceSection_TheFullCatalogRuns_AndTheLoopIsInert`.
5. **A capital the Empire no longer controls** (orchestrator addition: revolt drops the control row but
   never the `CapitalRow`). L6 rule, chosen as the smallest coherent reading of "the capital is the
   origin": a seat the Empire does not hold administers nothing — reach 0.0 at every settlement it still
   controls, so its levy (which still stands as policy) has no effect anywhere (no extraction, no burden)
   until it holds a capital again; with no capital at all, likewise 0.0. Total: never throws, never NaN
   (a NaN distance or Strength reads 0). Capital relocation is not implemented. Pinned by
   `AnEmpireThatNoLongerControlsItsCapital_ReachesNothing_SoItsTaxFallsNowhere`. The alternative
   ("reach from the nearest controlled settlement") was rejected because it invents a seat the content
   never designated.
6. **The AI valve's constants are data** (`governance.ai`), the valve is a pure per-polity policy
   (`OrdersFor`), and it acts only when `CanLevyTax` holds. It is not wired into `UiSession` or the CLI.
7. **Production re-anchored.** M5B multiplied after `HarvestWeatherFor`; this tree has
   `ratePerYear *= foodMultiplier` (weather × disaster). The extraction multiplier is a separate factor,
   computed once per settlement and applied once at each of M5B's three sites, in the food multiplier's
   position (after the Leontief min, before dt and the remainder). Untaxed it is exactly 1.0, so
   `x × 1.0 == x` bit for bit.
8. **Glass Box truthfulness.** `HappinessExplanation.ScopeNote` no longer says "only"; the tax burden is
   an explained cause (`HappinessExplanation.Burden` and `SocialSection.Tax`, one constructor
   `TaxBurdenReading.Of`: the declared rate, the stored reach, the effective rate and the multiplier);
   the settlement record's factor labels say the two factors are the CES provision factors and the
   burden multiplies; telemetry moves to **v4** (= v3 + `social.tax`; v2/v3 lines read the burden as
   not recorded); `SessionInspector` prints a `tax scale` column and states the multiplier; tax enters
   `PolicyHistory` as the second policy (`TaxPolicyChange`/`TaxPolicyState`, polity-keyed;
   observability-architecture.md §4 note); `ForensicSchema`'s file:line citations were re-cited to the
   moved lines (`WeightOf` :234→:258, the need-id constants :226/:227→:250/:251, `Of` :169-219→:191-236,
   plus `TaxSufficiency` :180-181) and are now CHECKED against the source by `ForensicCitationTests`
   rather than pinned as a literal.
9. **The CI pin guard.** M5B left `ci.yml` `FOUNDED_GOLDEN` on the old value and `CiPinAgreementTests`
   passed because the old value survived in an "OLD" comment (`Assert.Contains`). The founded pin is now
   the named constant `SnapshotTests.FoundedGoldenHash` and the guard compares `ci.yml` to it exactly.
10. **SystemCatalog.** M5B attached Revolt's doc comment to Governance; here each registration carries
    its own, and the class header records `Controls` as a sanctioned shared table with its field-level
    split (colonization appends, revolt removes, governance rewrites Strength only).

## 3. Timing, pinned turn-exact (`GovernanceTimingTests`)

| event | first visible |
|---|---|
| SetTaxRate stamped turn t | policy row in the state of **t+1** — its ONLY effect there (the taxed run with TaxPolicies cleared hashes equal to the untaxed twin); the derived happiness reading of t+1 already carries the burden |
| ...its effect on production (and on migration/revolt, which read happiness from PREV) | the step **t+1 → t+2** |
| save the state of t+1, load, continue | hash-for-hash equal to the uninterrupted run at every later turn (including a second edict inside the window) |
| DevelopRoads stamped t | route row in **t+1**; `SettlementDistances` in **t+2** (catchment reads PREV); `ControlRow.Strength` in **t+3** (governance reads PREV) = the reach over t+2's distances; production in the step **t+3 → t+4** (it reads PREV Strength; isolated from the road's catchment effect by stepping production on t+3 with and without the new reach) |
| colony founded by the step t → t+1 | colonization writes Strength 1.0 and governance, later in the same step, rewrites it from PREV — no route on record — as **0.0** in **t+1** and **t+2** (untaxed and unburdened: unadministered, never "perfectly administered"); its distances exist from t+2 and its reach from **t+3** |
| founded world, turn 0 → 1 | founding writes no `SettlementDistances`, so every non-capital Strength reads 0.0 in the state of turn 1 and its reach from turn 2. An edict stamped turn 0 is policy in turn 1 and therefore meets the zero rows: in the step 1 → 2 only the capital is taxed (`AFoundedWorld_ReadsEveryNonCapitalStrengthZeroInTurn1_AndItsReachFromTurn2`) |

Production with a tax in place is now tested at all three sites
(`ProductionWithATaxInPlace_RaisesFarmingDepositAndCraftOutput_ByExactlyTheExtractionMultiplier`):
grain, a herding/fishing good, an extraction good and two crafted goods each rise by exactly the
multiplier (1.15 at 50 % and full reach), and the untaxed step is bit-identical to a config with no
governance section. A levy cannot conjure craft output the materials do not support
(`ALevyCannotConjureCraftOutputTheMaterialsDoNotSupport`) — but note the measured subtlety: crafting reads
the LIVE stocks, so the levy also raises same-step extraction of craft inputs (fiber, clay, timber).

## 4. Golden movements

| golden | old (v29) | new (v30) | cause | control |
|---|---|---|---|---|
| toy, seed 42, 200 turns | `4c051fd4…` | `bbcac0469b61ff494fee410179f937e62505fa23ad183afce00d89b8c3f8333c` | one empty TaxPolicies prefix (toy pipeline, no control rows) | `GoldenHashSeed42Turn200_MovedForTheV30GovernanceTrailerAlone` |
| founded, seed 42, 300 turns (and `ci.yml` FOUNDED_GOLDEN) | `b2c0032f…` | `64820f83239f005e84ef2965a5564ff46a513434d550ad43a449a58fff5f17ce` | empty TaxPolicies prefix + the reach GovernanceSystem writes into Strength (11 of 12 rows below 1.0 at turn 300: 0.097–0.82) | `FoundedGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone` (non-vacuous) |
| FirstReign, 40 turns | `c805ca10…` | `3613dcc4aa059755fc9eb4ab8b353879c93d428d7915bac673b44f4a83366ba3` | empty TaxPolicies prefix only (the lone settlement is the capital, Strength 1.0) | `FirstReignTurn40_MovedForTheGovernanceLayerAlone` |
| driven, seed 42, 300 turns | `0460e6e9…` | `638d7a0914f0475f539354e47c99673b615ca52f4e353b03bec724c77392cb7a` | empty TaxPolicies prefix + Strength | `DrivenGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone` (non-vacuous) |

The v30 layer control clears TaxPolicies, restores every Strength to 1.0 and drops the prefix; it
returns each v29 pin byte for byte, so with no levy the governing loop changed no population, food,
migration, trade or any other state. Every older control (v22–v28) strips the governance layer too and
is UNMOVED.

## 5. Coupling map (S8 §4.1 item 4) — append-only

**A. The governing loop's own couplings.**

| from | to | evidence | with no levy |
|---|---|---|---|
| TaxPolicies × Strength | production output (all three sites) | `ProductionSystem.cs:177` | ×1.0, bit-identical |
| production output | `FoodHeadroom.Limit` (growth cap, migration vacancy bound) — reads LastProducedUnits | `FoodHeadroom.cs:166`; `DemographicsSystem.cs:186, :266` | unchanged |
| TaxPolicies × Strength | happiness (`TaxSufficiency` multiplies the reading) | `SettlementHappiness.cs:235` | ×1.0, bit-identical |
| happiness | migration destination viability (weight `attractivenessHappinessWeight`) | `MigrationSystem.cs:369-371` | unchanged |
| happiness | revolt (happiness ≤ 0) — a 100 % levy at full reach is a second zero corner | `RevoltSystem.cs:80` | unchanged |
| happiness | legitimacy → the AI valve (unwired) | `Governance.cs:169`, `AiGovernance.cs` | — |
| research completion | the tax edict's availability | `Governance.cs:206` | — |

**B. Road-aware `SettlementDistances` (ADR-032 §10.5) — every consumer, recorded together.** Since
§10.5, `CatchmentSystem` computes the pairwise `SettlementDistances` with the authoritative Pathfinder,
whose overlay includes every route row's lane at its `CostFactor` (`Pathfinder.cs:352-366`;
`CatchmentSystem.cs:95` recomputes on `RoadPerformance.NetworkRevision`, `:147-170` writes the pairs). A
road that cuts travel cost therefore moves FOUR consumers. ADR-032:187 ("the trade system is unchanged")
is about the freight travel-time hook (`TransportQuery`), not about these distances.

| # | consumer | evidence | record |
|---|---|---|---|
| 1 | **administrative reach** — D-040 C6's road–control coupling, REALIZED by this port: `ControlRow.Strength` = exp(−cost / 25) from the capital, so roads raise what a tax collects and what it costs | `Governance.cs:98-104` (`AdministrativeReach`), `GovernanceSystem.cs:91` | new (ADR-033 D4) |
| 2 | **migration damping** exp(−cost / `dampingDecayCostUnits`) | `MigrationSystem.cs:404-411` | not listed in any coupling record before this one |
| 3 | **the trade threshold** `BulkPerUnit × pathCost × CostPerBulkCostUnit` | `TradeArbitrageSystem.cs:126` (`PairCost`), `:141` (threshold), `:238-243` (`PairCost` reads the table) | listed here |
| 4 | **harvest-weather spatial correlation** — kernel exp(−cost / `spatialRangeCostUnits`), which the code itself calls "a proxy for physical proximity" | `HarvestWeatherSystem.cs:121` (`Kernel`), `:211-226` (reads the table; comment `:213`); config `SimConfig.cs:507-511` ("in the same travel-cost units as SettlementDistances") | **ESCALATED: should weather correlation use geographic distance rather than road-aware travel cost?** Recorded only — no transport or weather code changed |

## 6. The tax's pushback, measured (feeds the D-021 unrest-lite escalation)

**Rig** (`Sim.Tests/Systems/GovernancePushbackMeasurement.cs`, skipped in the suite, run manually on
this branch, 3.2 min): the founded seed-42 world (canonical 1024², N = 12, one player Empire holding all
twelve), the capability forced by completing `arithmetic_babylonian` at turn 0 (a ResearchCompleted row;
the gate itself is not bypassed), a SetTaxRate of 0 / 40 / 99 % stamped turn 0 (in force from the state
of turn 1), 300 turns of the full production pipeline. Cumulative columns sum `LastProducedUnits` and
`MigrationFlows` over every turn; "output" is every good.

| arm | turn | capital pop | empire pop | held | capital happiness | capital eff. rate | empire mean eff. rate | legitimacy | capital grain (cum.) | empire grain (cum.) | capital output (cum.) | empire output (cum.) | migrants (cum.) | capital in / out (cum.) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 % | 100 | 1113 | 10807 | 12/12 | 100.00 | 0.000 | 0.000 | 100.00 | 1202518 | 11587131 | 1552637 | 14773915 | 2616 | 254 / 212 |
| 40 % | 100 | 1114 | 10860 | 12/12 | 60.00 | 0.400 | 0.154 | 83.42 | 1351000 | 12108285 | 1744115 | 15439631 | 2545 | 244 / 220 |
| 99 % | 100 | 1075 | 10864 | 12/12 | 1.00 | 0.990 | 0.385 | 59.39 | 1540295 | 12842424 | 1988326 | 16377515 | 2691 | 231 / 239 |
| 0 % | 300 | 3994 | 40539 | 12/12 | 100.00 | 0.000 | 0.000 | 100.00 | 7459648 | 74337633 | 9572293 | 94319559 | 4805 | 343 / 406 |
| 40 % | 300 | 4026 | 40897 | 12/12 | 60.00 | 0.400 | 0.163 | 82.92 | 8414948 | 78607207 | 10797838 | 99746331 | 4633 | 333 / 413 |
| 99 % | 300 | 4004 | 40981 | 12/12 | 1.00 | 0.990 | 0.406 | 57.66 | 9501150 | 84102447 | 12191722 | 106731431 | 3935 | 304 / 343 |

**What it shows (turn 300, against the untaxed arm).**

- **The levy pays and costs nothing the simulation acts on.** At 99 % the capital's cumulative grain
  is ×1.274 and its all-goods output ×1.274, the empire's ×1.131 / ×1.132 (mean effective rate 0.406:
  reach thins the levy away from the capital). At 40 %: capital ×1.128, empire ×1.057. The multiplier
  at full reach is 1 + 0.3 × 0.99 = 1.297; why the capital's cumulative ratio is lower is INFERRED:
  the step into turn 1 is untaxed, and the arms' populations (so their labour) diverge — at turn 100
  the 99 % capital holds 1075 people against 1113 untaxed.
- **Population does not fall — it rises slightly** (empire +0.9 % at 40 %, +1.1 % at 99 %; capital
  +0.8 % / +0.25 % at turn 300). Nothing in the loop removes anyone; that the extra output is what
  feeds the extra people (through the `FoodHeadroom` growth cap) is INFERRED.
- **No revolt at any rate below 100 %**: the capital's provision reading is saturated (100) in every
  arm at both checkpoints, so its happiness reads 100 × (1 − effective rate) to the printed precision —
  60.00 at 40 %, 1.00 at 99 % — and revolt fires only at ≤ 0 (`SettlementHappiness.RevoltThreshold`).
  The tax alone reaches that only at a declared 100 % on a fully-reached settlement (pinned:
  `AFullLevyAtFullReachIsTotalExtraction_TheSecondRevoltCorner`). All twelve settlements are held in
  every arm.
- **Migration is the only behavioural response, and it is weak and inverted**: total migrants moved
  FALL (−3.6 % at 40 %, −18.1 % at 99 %). In the code, happiness enters migration only through the
  destination's viability, `material × (1 − w + w × happiness)` (`MigrationSystem.cs:369-372`, weight
  `attractivenessHappinessWeight`), which multiplies every flow term and the exit openness — so a taxed
  empire is a less viable destination everywhere (the attribution of the measured fall to this term is
  INFERRED; no arm isolates it). People are not driven OUT of the taxed capital: its cumulative outflow
  is 406 untaxed, 413 at 40 %, 343 at 99 %.
- **Legitimacy falls (100 → 82.9 → 57.7) and nothing on the player's side consumes it**; the AI valve
  would, but it is not wired and AI empires default to 0.

**Conclusion for the Director (no unrest was implemented):** the brake does not strengthen with
amplitude — D-021's paired-feedback rule is not met by the ported loop on its own. On the three arms
measured, raising the rate is pure gain for the player up to 99 % (more output, no fewer people, no
settlement lost), with a cliff (revolt) only at exactly 100 % on a fully-reached settlement.
D-021 Part 4's unrest-lite (valves 1, 2, 3, 6, 7 and the ignite-and-burn-out battery test) is the
missing negative loop; this measurement is the evidence for that escalation.

## 7. Decisions at L6 (minimal inference)

- **Colony first turns read Strength 0.0** (unadministered) until its route is on record — the principle
  M5B stated ("missing data must not read as perfect administration"), applied to the new-colony case.
- **A non-held capital administers nothing** (§2 item 5).
- **A repeal is a standing 0 % row**, not a deleted row ("levies 0 %" ≠ "never legislated", visible to
  the policy history).
- **Every SetTaxRate needs the gate**, including a repeal (knowledge never decays, so this only bites a
  world whose content changed under a save).
- **`CanLevyTax` parses the configured expression per call** (no cached state; validated once at load):
  the roads precedent resolves its config strings against the content at evaluation time.
- **Telemetry v4** rather than an untagged key: the vintage moves with the field set (T4.20's rule).

## 8. Escalations and open items

- **Unrest-lite (D-021 Part 4) is not implemented** — no branch implements it and no ratified M5 spec
  specifies it. §6 measures the pushback that exists without it.
- **Weather correlation over road-aware cost** (§5 B4) — ESCALATED.
- **CR-005** stays OPEN (dated note appended); **CR-008**'s money question (§1.1) stays OPEN.
- The AI valve drives nothing in a played session until stream S2 wires the single AI producer (and
  `aiEmpires` defaults to 0).

**Append (2026-10-03, R2b):** the unrest-lite escalation (§6, §8) and the weather-distance escalation (§5 B4) are
IMPLEMENTED on `m5i-r2b-governance-fixes`; record and measurements in `docs/m5-integration-coherence-matrix.md` §6.
