# ADR-033 — THE M5 INTEGRATION PASS: CAPABILITY-GATED ACTIONS, AGE-DERIVED UI, THE GOVERNING LOOP, INSTITUTIONS

**Status:** ACCEPTED for implementation under the Director's integration directive of 2026-10-02 ("THIS IS NOW A
FULL INTEGRATION / COMPLETION PASS"). Every decision below records its level in that directive's hierarchy
(L1 ratified decision · L2 ADR/architecture · L3 implementation + tests · L4 design document/content · L5 Civ VI
default · L6 minimal deterministic inference). Items marked **ESCALATED** are implemented in their conservative form
and await a Director ruling; nothing here is merged to `main`.

**Integration branch:** `m5-integration` = `transport-roads-foundation` @ `997824b` (transport, FROZEN) merged with
`ui-research-trees` @ `5302e72` (merge `dad9734`; Release build 0 errors; Sim.Tests 1218 passed / 0 failed /
4 skipped, Sim.Ui.Tests 354 / 354, measured 2026-10-02 on `dad9734`). Every stream branch is cut from this branch and
is reconciled into it; it is the single source of truth for this pass.

**Touches the kernel contract** (listed here; each touch gets its stream's tests and is restated in the final report):
schema v29 → v30 (TaxPolicies) → v31 (Institutions); OrderKind 5 (SetTaxRate, reserved since v26); SystemId 22
(GovernanceSystem, reserved since v26) and one institutions system id; content requirement changes in
`research.json`.

---

## D1 — The turn-1 labour surface: the sectors stay baseline; the ACTIVITY each sector expresses is knowledge-derived

**Facts (measured on the integrated tree, recon 2026-10-02).** The five sectors (Farming, Herding/fishing,
Extraction, Crafting, Construction; `WorldState.cs:318-324`) are the founding economy. The Farming sector is the
only grain source; switching it off at founding drives grain to zero, fertility suppression to 1, starvation to
~0.105/yr and every calibration corridor and world golden red. CR-003 (26.0 yield), the T3.5b default mix,
the T4.19 founding-food ruling, D-045 §1 ("Basic food/resource gathering" is baseline), D-046 G3 (initial food
production is NOT completion of the agriculture branch) and the ownership matrix ("must not gate
`Sectors.Farming`", `research-capability-ownership.md:398-399`) all rest on that.

**Director, 2026-10-02 (integration directive):** "IF Farming has not been researched/acquired: do not show a
'Farming %' allocation control"; "After Agriculture/farming research is completed: the farming capability becomes
available; the corresponding action/option appears"; baseline = "hunting; gathering; basic fighting; basic
building/construction; fishing; primitive shore-hugging transport"; "Do NOT turn every baseline activity into a
research node"; "Do not solve this by hiding arbitrary controls with UI-only conditions."

**Decision (L1 directive + L3/L4 calibration and content):**
- **No sector is gated and production is unchanged.** The labour model is the five baseline sectors; the
  `SectorAllocation` order and every yield, share and calibration stay exactly as shipped.
- **Each sector is presented as the ACTIVITY it currently expresses, derived from authoritative knowledge state**
  through content data (never a UI-only condition):

  | sector | baseline activity (turn 1) | researched activity (appears when knowledge-eligible) |
  |---|---|---|
  | Farming | Gathering — `baseline.food_gathering` | Farming — `activity.farming` |
  | Herding/fishing | Hunting & fishing — `baseline.hunting` (new record; Director-listed baseline) + `baseline.basic_fishing` | Herding & fishing — `activity.herding` |
  | Extraction | Gathering wood & stone — `baseline.food_gathering` (resource gathering) | Logging — `activity.logging`; Mining — `activity.mining` |
  | Crafting | Crafts & toolmaking — `baseline.primitive_hafted_tools` | (no researched identity in this pass) |
  | Construction | Building — `baseline.construction`, `baseline.basic_shelter`, `baseline.basic_paths` | (projects appear under D2) |

- **Content.** `activity.farming` requires `cereal_cultivation OR root_crop OR rice_wet OR millet OR maize OR
  sorghum_pearl_millet`; `activity.herding` requires `sheep_goat OR cattle`; `activity.logging` requires
  `ground_stone_early`; `activity.mining` requires `mining_shaft` — the reverse indexes the content carried before
  D-047 Part E. `activity.fishing` stays `null` (D-047 ruling 4, fishing is baseline).
- **What this supersedes.** D-047 Part E / G-18 ("the five founding activities require no node") was an
  implementation closure, not Director ruling text; the Director's 2026-10-02 08:40 instruction "Do not change: …
  baseline founding activities" is honoured in substance (the founding economy, its five sectors and their output
  are unchanged and remain baseline) and superseded in presentation by the later 19:05 directive for farming.
  Herding, logging and mining follow "the same principle" (domestication and improved tools are the directive's own
  examples of research that makes new activities appear). Reverting any one of them is a one-line content change.
- **ESCALATED (with CR-006).** The turn-1 "Gathering" output is the CR-003 yield derived for domesticated rainfed
  cereal. Options for the Director: (a) keep — the food sector is calibrated as is and agriculture changes its
  identity, not its yield; (b) add a cultivation adoption mechanism (agriculture knowledge → adoption → higher
  yield), a new mechanic; (c) recalibrate a pre-agricultural founding economy, which reopens the T4.19 founding
  rulings and the corridors. This pass implements (a).

## D2 — `AvailableActionsQuery`: one read-only aggregation of domain-owned predicates

**Decision (L1 directive, L2 isolation law, L6 shape):**
- A pure static query in `Sim.Core` answers "what can this civilization do now?":
  `AvailableActionsQuery.For(IReadOnlyWorldState, SimConfig, PolityId)`. It owns no state, no cache and no system;
  it is not a CapabilitySystem and not an action manager.
- It asks each OWNING domain's existing query: labour activities (D1), research (`ResearchQuery`), Age advance
  (`AgeQuery`), construction projects (knowledge-eligible project entities + `ConstructionSystem`'s own checks),
  roads (`RoadDevelopmentQuery`), taxation (D4), institutions (D6), military (D7).
- **One predicate, two callers.** Each order's world validation calls the same predicate function the query calls,
  so the UI can never offer an order the simulation would reject, and an order the UI never offered is rejected on
  the same rule.
- **Only actions that exist are listed.** A locked future action is not listed on the action surface (it stays
  visible as future knowledge in the trees, which already show each node's unlocks). An action that is legal but not
  affordable this turn is listed with its blocker ("needs 40 timber"), which is information, not a disabled
  future control.
- Ordering is a stable composite key (domain ordinal, stable integer id); no dictionary iteration.

## D3 — `EnqueueConstruction` gains its availability predicate

**Decision (L2 ADR-028 §4, L4 content):** a construction project is available iff its research entity is
knowledge-eligible for the issuer (project ↔ entity link in content) and the existing settlement/control checks
pass. This makes ADR-028's LOCKED/AVAILABLE states expressible. The project↔entity link is data, not code.

## D4 — The M5 governing loop is ported onto the integrated tree; taxation is research-gated by the content

**Port (L1 ledger annex: "M5 governing loop is on `m5-full-build`… D-021's valve rule binds it"; L3 M5B code).**
`TaxPolicyRow(Polity, Rate)`, `SetTaxRate` = 5, `GovernanceSystem` = SystemId 22 (after `revolt`, before
`demographics`), the `Governance` readers, `AiGovernance`, the tax order factory and UI, CR-008's ruling text
("tax is a policy on flows; no treasury").
- **Schema:** TaxPolicies lands as **v30**. `Snapshot` requires an exact version match and v28/v29 already exist,
  so the v27 reservation cannot be what ships; **v27 stays permanently unused** (note appended to
  `CanonicalSchema`).
- **M5B defects fixed in the port:** the CI `FOUNDED_GOLDEN` left behind; `ControlRow.Strength` written but never
  read while production and happiness recompute reach (one fact, one place); the untested production effect; the
  unpinned order-delivery timing (turn-exact pin); the AI's constants become config; the misplaced
  `SystemCatalog` doc comment; Glass Box explanations that would go false (tax becomes an explained cause and a
  policy in the history); the stale forensic line citation.
- **Reach rides the road-aware distances** (`SettlementDistances` from the authoritative Pathfinder). That realizes
  D-040 C6's road–control coupling and is recorded in the coupling map.
- **Taxation is research-gated (L1 capability principle + L4 content).** The content already names the
  capability: "tax assessment" (`arithmetic_babylonian`), "taxation by area" (`surveying`), "taxation by weight"
  (`standard_weights`), "taxation in coin" (`coinage_electrum`). The tax edict is available iff any of them is
  completed — one requires-expression in content, evaluated by the existing knowledge evaluator, enforced by order
  validation and exposed by D2. Civ VI's Code of Laws analogy (L5) is not needed.
- **CR-005 stays OPEN.** M5B's draft "Option C" text is not imported: it contradicts D-044/D-047, ADR-031,
  ADR-032 and the directive, all of which ship research, Ages and roads live. An append-only note records this;
  the formal disposition is the Director's.
- **ESCALATED — unrest-lite.** D-021 Part 4 schedules unrest-lite with valves 1, 2, 3, 6, 7 and the
  ignite-and-burn-out battery test at M5. No branch implements unrest (grievance still drives nothing) and no
  ratified M5 spec specifies it, so it is not invented here. The ported tax's pushback is measured and reported.

## D5 — AI acts through the same orders; one deterministic producer

**Decision (L1 directive "AI uses the same path"; L3 existing policies; L6 producer):**
- `AiOrders` in `Sim.Core` produces each AI polity's orders through the same factories and validators the player
  uses: research target (new `AiResearchPolicy`: the next Age's core research first, else the cheapest available
  node; composite key (cost, key)), Age advance (`AgeAdvancePolicy`), roads (`RoadDevelopmentPolicy`), tax
  (`AiGovernance`), construction (`AiConstructionPolicy`). It is called by `UiSession.EndTurn` and by the CLI
  session loop. Ledger §29 asks for new seams to be flagged: this is one.
- **`aiEmpires` default stays 0** (M4 exit inventory: turning it on "is a measured decision, not a default").
  `Sim.Ui` gains a launch option to play against N AI empires. **ESCALATED:** the default AI count of a played game.

## D6 — Institutions: universities are real institutions on ADR-028's lifecycle

**Decision (L1 directive; L2 ADR-028, ADR-029 cost seam, D-047 ruling 9 "five university types"):** an institution
table (schema v31) and one system implement LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE (SATURATED
derived) for the five university types, founded endogenously through the construction queue. Maturity accrues per
sim-year; staffing draws real labour; effects have diminishing returns and saturation and diffuse with distance.
Engineering (and each branch type) writes `ResearchCostModifierRow` for its branch; medical improves health through a
per-settlement mortality seam that scales with maturity. The first writer of `ResearchCostModifierRow` owes D-021's
brake (ADR-029; `WorldState.cs:872`) and ships it. Every coefficient is TUNE.

## D7 — Military in this pass

**Facts.** Implemented: the founding warband, 12 unit families / 66 identities, automatic free modernization at Age
transitions (ADR-031). Absent from code: recruitment, movement, Action Capacity, Zone of Control, war pulses, armies
in motion (M6 battle layer on the frozen ladder; designed in D-043, D-047 and ADR-019). *(2026-10-03 roadmap rebase: now **M7 Battle Layer**, which follows M6 Knowledge / Research / Technology.)*
**Decision (L1 frozen ladder; directive "Maintain … Do not redesign"):** nothing is redesigned and no M6 mechanic is
built. The military surface shows the roster, family lines, Age identities and the modernization preview; "Basic
fighting" is a capability entry with no order, because no military order exists. **ESCALATED:** scheduling of the
military packet.

## D8 — The UI's visual era is DERIVED from the authoritative Age

**Decision (L1 directive):**
- `UiEra = f(AgeQuery.CurrentAge(world, cfg.Ages, player))`: a pure function, never stored, recomputed after load,
  shared by the live ImGui renderer and the headless SVG renderer.
- One token system (palette, type scale, panel material, edge treatment, ornament, density, control granularity)
  evolves across the nine Ages around a persistent visual DNA: the same layout regions, navigation, interaction
  rules and semantic colours. ParchmentPalette, ProgressionPalette and the WorldLens literals converge on it.
- Research changes WHAT is shown (actions, nodes, icons); the Age changes HOW it is shown. No theme change per
  researched node.
- The Age change is acknowledged by a restrained transition panel and the re-derived theme.
- **Supersedes** the style bible's substrate acceptance line "The medium reads identically at year −4000 and any
  later date (frame is era-invariant)" (`docs/style-bible-parchment.md:101`) by the later Director instruction; an
  append-only note is added there. D-002 (ImGui is the game UI) and D-038 (the map shows what IS) are unaffected.

## D9 — Obsolete UI abstractions and debug surfaces

**Decision (L1 directive "If it is merely an obsolete UI abstraction… remove it"):** the M1 single-slider labour
path (`LaborOrderFactory`, `UiSession.EmitLaborOrder`; no call site since T3.9b) leaves `Sim.Ui`. OrderKind 2 stays in
the kernel because shipped order logs replay it. Debug and audit surfaces (turn audit, record dumps, build footer)
move behind a developer toggle; the player surface does not need them to understand the civilization.

## D10 — Construction capacity is spent once

If measurement confirms that labour the construction queue consumes is also banked by PathBuild in the same turn,
`ConstructionSystem` publishes the labour it used and PathBuild subtracts it, exactly as it already subtracts housing
labour (directive: capacity "generated per turn; unused capacity disappears; no bank"; a genuine integration bug).

## D11 — Records

- The stale `roads.maxRouteKm` wording is corrected (documentation only; transport is frozen at `997824b`).
- D-043 and CR-017 are imported byte-identical from `origin/claude/civdemo-work-b1z2y4` @ `6dded01`, where ADR-031
  cites them. D-043's DD-13/DD-14 and ADR-028's DD-13/DD-14 are different entries with the same labels; cite them
  qualified ("D-043 DD-14", "ADR-028 DD-13"). Neither record is edited.

---

## Appendix A — Streams and file ownership (parallel execution map)

| stream | scope | owns (writes) | depends on |
|---|---|---|---|
| S1 governing loop | D4 | `CanonicalSchema`, `WorldState` (TaxPolicies), `OrderLog`/`OrderValidation` (kind 5), `SystemCatalog`, `pipeline.json`, `Governance*`, `ProductionSystem`, `SettlementHappiness`, Glass Box/forensic explanation files, `SnapshotDiff`, golden pins + `ci.yml`, `sim.json` `governance` | — |
| S2 capability layer | D1, D2, D3, D5, D9 (sim side), D10 | `research.json` activities/baseline, `goods.json` project links, `AvailableActions*`, `AiOrders`/policies, `ConstructionSystem`, `PathBuildSystem` (D10), `OrderValidation` (kind 4 predicate) | — (S1's kind 5 merged at reconciliation) |
| S3 institutions | D6 | institutions table/system (v31), `DemographicsSystem` seam, university projects | S1, S2 |
| U1 era theme | D8 | `Sim.Ui/Theme/*`, `Art/*`, `Render/*`, `Progression/*` and `Ages/*` painting, previews, style-bible note | — |
| U2 action surface | D1/D2 UI, D9 UI, tax/roads/construction/military panels | `SimUiGame` sections, `UiSession` emitters, `ViewModel/*` | S2 API, U1 API |
| U3 world | roads by class, institutions, units | `Sim.Ui/World/*` | U1 API |
| V verification | M0–M5 coherence, save/load, cross-process determinism, bench, previews | tests and records | all |

Reconciliation is deterministic: each stream lands on `m5-integration` in the order S1 → S2 → U1 → U2/U3 → S3 →
institution UI → V, conflicts resolved by the ratified design (never by keeping two implementations), and the
complete gate suite runs after each landing.

## R2c — reconciliation of R2a + R2b (appended 2026-10-03)

Branch `m5i-r2c-reconcile` = `m5-integration` @ f1fe76f + `m5i-r2a-trade-citystates` @ a529bab + `m5i-r2b-governance-fixes`
@ 93bb741. Labels: RATIFIED = a Director decision (director-locked-r2 items); IMPLEMENTED = shipped and tested on this
branch; INFERRED = an implementation choice made by an agent, not a ruling; DEFERRED / OPEN as stated.

- IMPLEMENTED (RATIFIED items 1, 13): Trade is research node key 426 (`trade`); TradeArbitrageSystem moves goods only
  between two settlements that both know `activity.trade` (TradeQuery.CanTrade). Placement/prerequisites as in R2a's record.
- IMPLEMENTED (RATIFIED items 4, 12): uncontrolled settlements research under PolityId(−1−settlement), pace 0.25 (INFERRED value, TUNE).
- IMPLEMENTED (RATIFIED item 9): geographic weather distance; unrest-lite tax brake (needs.json `unrest`, State/Unrest.cs);
  construction-capacity labels marked as estimates. Item 11: Age military milestones read minIdentityAge; M6-owned facts PENDING. *(2026-10-03 roadmap rebase: now **M7 Battle Layer**, which follows M6 Knowledge / Research / Technology.)*
- IMPLEMENTED, interaction (INFERRED, R2c): a settlement thrown off by an uprising has no controller and therefore becomes
  a city-state that researches on its own; no extra mechanism. Its local record starts EMPTY — the former ruler's knowledge
  is NOT seeded (seeding would be a knowledge-diffusion mechanic with no ruling; OPEN for the Director). Consequence: a
  revolted seat loses the ruler's crafts (e.g. pottery firing) until it re-learns them. Test:
  UnrestTests.ARevoltedSeat_BecomesACityState_AndResearchesFromAnEmptyLocalRecord.
- MEASURED interaction: Trade gate × tax brake — the founded seed-42 pushback table (0/40/70/99 %, turns 20–300) is
  byte-identical to R2b's AFTER rows; the founded run trades nothing either way. Trade × city-states: a pair trades only if
  both endpoints know Trade, a city-state included (TradeQuery, R2a tests).
- Goldens: founded 1368df9f… and first-reign 680a20c5… (R2b's pins; R2a does not move them), driven → efeec45d…; the
  attribution file strips each layer separately and jointly.
- DEFERRED (RATIFIED item 5): all 115 unrealized entities stay DEFERRED; 0 realized in R2c. Plan:
  `docs/deferred-entity-realization-plan.md` (tiers INFERRED).
- OPEN: `farming.preCultivation` committed OFF (turn-1 Gathering still labelled "makes grain" — item 8 tension, R2a record);
  capital loss leaves no tax reach (no ratified succession; DEFERRED); quarantined corridors densityPerArableKm2 and
  migrationGrossPerDecade breach their windows on 4-seed autoplay, as they already did at f1fe76f.

## R3 — final R2 reconciliation (appended 2026-10-03)

Full record: `docs/r3-final-reconcile-record.md`. Labels as above.

- RATIFIED (Director R2-final): (1) knowledge never decays; (2) a revolted settlement becomes a new AI-controlled
  polity; (3) at separation it inherits the complete knowledge of its former polity; (4) afterwards both progress
  independently; (5) annexation preserves and merges knowledge (union); (6) capital succession stays DEFERRED;
  (7) turn-1 primitive food is to use a smaller founding population rather than hidden farming; (8) Trade is a
  Bronze Age (A3) Technology unlock, 880 RP, `token_counting AND (donkey OR camel OR sail_square)`; (9) city-state
  autonomous research uses the existing research architecture.
- IMPLEMENTED: (2)–(5) in RevoltSystem + `State/KnowledgeTransfer.cs` (the R2c INFERRED "empty local record" is
  SUPERSEDED); capital-loss invariant tests; truthful food-sector label (`LabourActivities.CapabilityLabel`).
- INFERRED (R3): knowledge = completed nodes (progress/Eurekas not copied); revolted polity gets no capital; a
  single-settlement polity does not revolt from itself; revolted polity researches at the normal polity rate.
- STOPPED / OPEN: (7) — no founding population in {50, 100, 200, 400} passes the ratified bands with the forager
  yield ON (per-capita labour-side ceiling, and no research in order-free calibration runs). `preCultivation`
  stays OFF; nothing retuned. A Ledger negative-amount exception at N=100 (switch ON) is OPEN.
- OPEN: CR-019 — revolt no longer produces stateless settlements, so T4.5's raider gate has no producer in a
  founded world. Annexation has no caller (no conquest path; Battle Layer, M7 in the 2026-10-03 roadmap).
- Goldens: unmoved (founded 1368df9f…, first-reign 680a20c5…, driven efeec45d…).

## R4a / R5 — pointer (appended 2026-10-04, M5 playtest baseline)

The sections above are kept unchanged as history. Two of their statements are superseded:

- **D4's tax gate.** D4 says the edict is available iff any of `arithmetic_babylonian`, `surveying`,
  `standard_weights` or `coinage_electrum` is completed. That is **superseded** by R5 (`9f7c82b`, Director
  2026-10-04: "taxation is supposed to be a researchable node").
  - The only gate is now the Civics node `taxation`: key 1007, A3, 1,050 RP.
  - Its prerequisites are `token_counting AND stamp_seal AND (proto_writing OR hieroglyphic OR chinese_script)`.
  - It is wired through `sim.json governance.taxationRequires = "taxation"`.
  - The four technologies keep their capability text as refinements and no longer gate the edict.
  - The AI levy pin moved from 546/547 to 367/368 (`5c364b5`).
- **D1's ESCALATED option (a) and R3's "STOPPED" turn-1 food.** These are **superseded** by R4a (`07832a4`):
  - `farming.preCultivation` ships ON at 4.3 per gatherer and 2.0 per km², with the founding population
    unchanged at 400.
  - The Director ruled on 2026-10-04 that raising primitive gathering yield is a legitimate lever.
  - The R3 ledger crash is fixed in `e6b7e42`.
  - Dignity offset R4a: `95982bd`.
  - Record: `docs/r4a-m5-closure-record.md`.

Baseline record for the playtest: `docs/m5-playtest-baseline.md`.
