# M6 knowledge redesign — ENGINE AUDIT (research / technology / civics)

Audit section of the M6 knowledge-redesign design package. It tells the designers of a deeper Technology tree, a
distinct Civics tree, new node keys, splits/retirements and cost changes exactly which code, validators, tests, goldens,
schema and UI each kind of change touches. Evidence is source code, content and tests; documents are secondary and are
cited only as such or where they disagree with code (**DOC≠CODE**).

## 0. Provenance, method, labels

- **Tree.** `/home/user/m5w/dev`, branch `claude/new-session-yavghm`, HEAD `edba2b5`; `main` = `m5-exit` = `6e51a3e`
  [M] (`git rev-parse`). Nothing under `Sim.*`, no content file and no tracked file was changed. Nothing was built,
  run or tested; the three research `--check` scripts were not run.
- **Method.** Read the research loader, system, queries, AI policy, Age content/systems, institutions, kernel schema,
  snapshot, session, replay guard, UI progression screen and the tests. Content counts were computed by a scratch Python
  parse of `Sim.Data/content/*.json` (not committed). Hard-coded ids were found by scanning every C# string literal
  under `Sim*/` (bin/obj excluded) for literals equal to — or predicate strings containing — a research node id
  (Appendix A). Fixture order logs were decoded with the OrderLog binary layout (Appendix B).
- **Labels.** **[M]** MEASURED: read at the cited lines (or computed from the cited content). **[I]** INFERENCE: follows
  from measured facts, not executed. **[OQ]** OPEN QUESTION: not decidable from the tree. Paths are repo-relative.
- **Vocabulary.** *key* = the node's stable integer (`ResearchNodeId`), the ONLY node identity persisted in saves and
  orders (`Sim.Core/State/Ids.cs:176-184`) [M]. *id* = the snake_case string atom used inside content expressions
  [M] (`Sim.Core/Systems/Research/ResearchContent.cs:391-394`). *index* = dense load-time position, technologies first
  then civics, equal to key order (`ResearchContent.cs:125-130, 374-390`) [M]. *holder* = a roster polity, or a
  settlement's local key `−1 − settlementId` (`Sim.Core/State/SettlementKnowledge.cs:24-30`) [M].

### 0.1 Content at HEAD [M]

| Item | Value | Evidence |
|---|---|---|
| Schema | `civ-sim/research@2` (loader requires exactly this) | `Sim.Data/content/research.json:2`; `ResearchContent.cs:283, 363-364` |
| Technologies | 426, keys 1–426 dense in file order; trunk 177, military 37, medicine 22, engineering 131, natural_science 44, agriculture 15 | `research.json:289-28108`; pinned `Sim.Tests/Systems/ResearchContentTests.cs:38, 84` |
| Civics | 7, keys 1001–1007: law_code, legal_code_roman, census, coined_wage, patent, joint_stock, taxation; all domain `governance`, no Eurekas, `universityRelevance: []` | `research.json:28109-28488`; pinned `ResearchContentTests.cs:65-76` |
| Last technologies | motor_road (425, appended ADR-032), trade (426, appended R2a); frontier_* recursive nodes are keys 415–424 | `research.json:27977, 28038`; `ResearchContentTests.cs:39-42` |
| Entities | 135: building 43, institution 28, infrastructure 25, unit 23, activity 9, recipe 4, project 3; null requirement: building.granary, building.workshop, inst.burial, activity.fishing, recipe.weaving, recipe.toolmaking | `research.json:28489-29435` |
| Baseline capabilities | 13 (`baseline.*`) | `research.json:49-128` |
| Eurekas | 81 on 80 nodes; 17 evaluable (2 stock, 15 knowledge); all `authored`; weights 0.4 ×79, 0.2 ×2 (railway) | content parse; pinned `ResearchContentTests.cs:171-183, 207` |
| Research sets | recursive 10 (TR tier), speculative_finite 6 (TS tier, all engineering), finite 423 | `research.json:29436-…`; pinned `ResearchContentTests.cs:323-326` |
| Research stage | `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman` (a civic atom) | `research.json:253-256` |
| File SHA-256 | `7a3bf38896f443e4cdfe582503d57f7f17abbab4f87acafe5ac4c3ef8ed9b791` — equal to the SHA embedded in the three generated docs (so they are current) | `sha256sum`; `docs/research-corpus-audit.md:3`; `docs/research-gameplay-unlock-audit.md:3`; `docs/research-calibration-report.md:3` |
| Corpus provenance | `source.corpusSha256` = SHA-256 of `tech-graph-v0.6.json` (`e1251b09…`) | `research.json:13-18`; `ResearchContentTests.cs:145-149` |

### 0.2 Engine map [M]

| File | Role |
|---|---|
| `Sim.Core/Systems/Research/ResearchContent.cs` | model types + `ResearchContentLoader` (all content validation) |
| `Sim.Core/Systems/Research/ResearchSystem.cs` | SystemId 24, `research`; owns Targets/Progress/Completed/Eurekas/Credits (`:9-14, 66-69`) |
| `Sim.Core/State/ResearchQuery.cs` | THE read seam: availability, stage, cost, RP, Eureka state, eligibility (`:6-21`) |
| `Sim.Core/Systems/Research/AiResearchPolicy.cs` | AI target choice (not a system; produces orders) |
| `Sim.Core/State/SettlementKnowledge.cs`, `KnowledgeTransfer.cs` | settlement knowledge mask; revolt/annexation knowledge union |
| `Sim.Core/State/AgeQuery.cs`, `Sim.Core/Systems/Ages/*` | Age milestone facts (research, research_count) and advance |
| `Sim.Core/Systems/Institutions/InstitutionsSystem.cs`, `State/Institutions*.cs` | universities → `ResearchCostModifiers` writer |
| `Sim.Core/Kernel/CanonicalSchema.cs`, `Snapshot.cs`, `WorldHash.cs`, `SessionManifest.cs`, `ReplayGuard.cs`, `ReplayProof.cs` | persistence and identity |
| `Sim.Ui/Progression/*`, `Sim.Core/State/InfoQuery.cs`, `AvailableActionsQuery.cs` | UI / Glass Box |
| pipeline order | `… pathbuild, research, ageeligibility, agetransition, roaddevelopment, institutions` (`Sim.Data/content/pipeline.json:20-25`) |

---

## Q1. Research Points

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| Formula | `RP = coefficient × population^exponent` when population > 0, else 0 | `Sim.Core/State/ResearchQuery.cs:384-385` | M |
| Values | coefficient 0.08, exponent 0.699, anchors {100: 2, 1000: 10}, "calibration — tunable" | `research.json:20-30` | M |
| Loader rule | coefficient > 0; exponent ∈ (0,1) ("'research = population × constant' is forbidden"); anchors a non-empty object, each within 0.1 % of the formula | `ResearchContent.cs:763-786` | M |
| Input | TOTAL population (every cohort, every class: bucket counts) of the settlements whose knowledge holder is the polity; holder = lowest-id controller, else the settlement's local key; each settlement once | `ResearchQuery.cs:357-375`; `SettlementKnowledge.cs:34-35` | M |
| Read from | PREV (population of the state the step starts from) | `ResearchSystem.cs:78, 179-184` | M |
| Pool | ONE pool per polity (`ResearchPointPool`) serves both trees and goes only to the one target | `ResearchQuery.cs:387-394`; `ResearchSystem.cs:176-187`; test `Sim.Tests/Systems/ResearchEngineTests.cs:548` | M |
| dt | NOT multiplied by dtYears — the single ADR-030 §2 per-turn site; no other research code reads dt, clock (except order stamping) or Age | `ResearchSystem.cs:56, 176-178`; `ResearchQuery.cs:211, 216`; test `ResearchEngineTests.cs:84`; `docs/adr/adr-030-per-turn-research-points.md:13-31` | M |
| Consequence of per-turn RP | research per sim-year varies with the era band's dt (ADR-030 §4) | `docs/adr/adr-030-per-turn-research-points.md:41-43` (doc); `Sim.Core/Kernel/TurnExecutor.cs:116-117` (dt from era table) | M (doc) / I |
| Military-population weighting | NOT implemented. It exists only as prose in `tuning.rpPerTurn.input`; the loader binds `Input` but never reads it; `Population` counts every person once; ADR-030 §6 leaves it undecided | `research.json:23`; `ResearchContent.cs:1403-1409` vs `760-806`; `ResearchQuery.cs:357-360`; `adr-030…:55-57` | M |
| Other inputs | none: literacy, education, universities are "FUTURE modifiers"; universities only cut cost | `ResearchQuery.cs:377-383`; `InstitutionsSystem.cs:90-103` | M |
| City-state pace | an uncontrolled settlement researches with `cityStatePaceFraction × RP(its own population)` = 0.25 ×; loader range [0,1); active only when the fraction > 0, the world has ≥1 Controls row and the settlement has no controller; its target is picked by `AiResearchPolicy.Cheapest`; it issues/receives no orders | `ResearchQuery.cs:396-400`; `research.json:46`; `ResearchContent.cs:801-803`; `SettlementKnowledge.cs:39-41`; `ResearchSystem.cs:89-91, 110-113` | M |
| City-states in shipped play | founding gives every settlement a controller (round-robin); colonies inherit the parent's controller; a revolt re-assigns the place to a new AI polity — so no shipped world produces an uncontrolled settlement; city-state research runs only in hand-built test worlds (`CityStateProgressionTests`) | `Sim.Core/Worldgen/WorldFounding.cs:359-365`; `Sim.Core/SystemCatalog.cs:90-99`; `Sim.Core/Systems/Revolt/RevoltSystem.cs:184-189` | I |
| Dead code | `ResearchQuery.Adults` (cohorts 3..11) has no caller in `Sim.*` | `ResearchQuery.cs:339-355` (grep) | M |

---

## Q2. Consumption (targets, orders, progress, completion)

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| Active target | at most ONE `ResearchTargetRow(Polity, Node key)` per holder, across BOTH trees; absence = no target | `Sim.Core/State/WorldState.cs:872-881`; `ResearchSystem.cs:199-212` | M |
| Order | `OrderKind.SetResearchTarget = 6`; TargetId = node KEY, or −1 to clear; Amount reserved = 0; a persistent directive | `Sim.Core/Kernel/OrderLog.cs:74-89` | M |
| Load-time validation | TargetId ≥ 1 or exactly −1; Amount == 0 (NaN fails) — no content lookup | `OrderLog.cs:434-442` | M |
| World/delivery validation | actor must be a registered Empire at turn 0 (or a deferred revolt-born actor that exists at delivery); NO check of the key against content | `Sim.Core/Kernel/OrderValidation.cs:60-68, 207-226` | M |
| Consumption-time validation | per holder, in log order: only orders whose actor = this roster polity; −1 clears; a key that exists AND is AVAILABLE on PREV sets the target; anything else changes nothing; last valid wins; local holders ignore orders | `ResearchSystem.cs:113-120` | M |
| Delivery / stamping | orders with `Turn == t` are delivered to the step t → t+1 (`BatchFor(prev.Clock.Turn)`); an order stamped t retargets that very step and its RP goes to the new target; builders stamp `world.Clock.Turn` | `TurnExecutor.cs:80-81, 130-136`; `ResearchSystem.cs:25-26`; `ResearchQuery.cs:204-216`; turn-exact pin `ResearchEngineTests.cs:105` | M |
| Order builders | `ResearchQuery.TargetOrder` returns null for an unknown or unavailable node, so UI and AI never log an order the system would ignore; UI = `ResearchOrderFactory` → same builder | `ResearchQuery.cs:196-212`; `Sim.Ui/ViewModel/ResearchOrderFactory.cs:21-29`; `Sim.Ui/UiSession.cs:526-533, 646-654` | M |
| Kept target | a target carried from PREV survives only if its key exists in content AND is available; else target = −1 and the row is removed at the end of the step | `ResearchSystem.cs:104-109, 199-212` | M |
| Progress storage | `ResearchProgressRow(Polity, Node key, double Progress)` in RP; created lazily on first credit; removed only when the node completes | `WorldState.cs:883-892`; `ResearchSystem.cs:122-131, 189-197, 223-234` | M |
| Throughput | step 3: the holder's RP is credited to the target only, capped at its remaining EffectiveCost | `ResearchSystem.cs:176-187` | M |
| Completion test | every AVAILABLE node (index order) whose progress ≥ EffectiveCost (computed on PREV, i.e. last step's university factors) completes: `ResearchCompletedRow` appended, progress row removed, target cleared if it was the target; credit-only completion allowed | `ResearchSystem.cs:39-45, 189-197`; test `Sim.Tests/Systems/ResearchEurekaCreditTests.cs:256` | M |
| Exactness | reaching the cost sets progress to exactly the cost (no ulp miss) | `ResearchSystem.cs:219-234`; test `ResearchEngineTests.cs:247` | M |
| Overflow | LOST: RP beyond the remaining cost is not stored anywhere (no bank, no carry to the next target) | `ResearchSystem.cs:34-38, 228-229`; test `ResearchEngineTests.cs:230` | M |
| No target | that turn's RP reaches no node and is not stored; Eureka/exposure credits still land on available nodes | `ResearchSystem.cs:30-33, 179`; test `ResearchEngineTests.cs:193` | M |
| Partial progress | kept on every node indefinitely; switching target never erases it; no decay | `WorldState.cs:883-891`; test `ResearchEngineTests.cs:208` | M |
| Re-targeting | any later valid order in the batch replaces the target for the same step; progress on the old target is kept | `ResearchSystem.cs:113-120` | M |
| Dependents | availability is computed from PREV's completed set, so a node's dependents become available from the NEXT step | `ResearchSystem.cs:44-45, 100-101` | M |
| One-turn floor | from the four rows above: at most one node per holder per step completes from RP; each RP-funded node costs ≥ 1 step even when RP ≫ cost; a chain of depth d needs ≥ d+1 steps; the floor binds for nodes cheaper than the per-turn RP | — | I |
| Holder order | roster polities in `Polities` table order (a doubled row once), then uncontrolled settlements in settlement-table order | `ResearchSystem.cs:85-97` | M |
| Ledger | research moves no people, money or goods; no Ledger call, no RNG | `ResearchSystem.cs:58-59` | M |

---

## Q3. Cost

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| BaseCost | the node's authored `cost`; must equal `costRationale.content_cost`; `calibration_adjustment` must be 0 | `ResearchContent.cs:838-842` | M |
| Model | `U × K^magnitude`, U = 92.4, K = 2.0; magnitude = Σ six factors | `research.json:37-45`; `ResearchContent.cs:843` | M |
| Tolerance | `abs(cost − U·K^m) ≤ max(5 RP, 0.1 % × cost)` ("rounded to tens") | `ResearchContent.cs:844-848` | M |
| Factor ranges | novelty [0.5, 2], difficulty [0, 3.5], material [0, 2], institutional [0, 3], breadth [0, 1.5], prereq_complexity [0, 1]; exactly these six; magnitude = their sum within 1e-9 | `ResearchContent.cs:808-837` | M |
| Implied cost band | magnitude ∈ [0.5, 13] ⇒ BaseCost ∈ ~[130.7, 756,941] RP; content spans 180 … 756,960. A deeper late tree cannot exceed ~757 k RP without changing the code ranges or U/K | computed from the ranges; content parse | I |
| Tiers | T1–T4, TR (exactly the recursive set), TS (exactly speculative_finite) | `ResearchContent.cs:290, 818, 919-922` | M |
| EffectiveCost | `max(effectiveCostFloorFraction × BaseCost, BaseCost × Π Factor)`; floor 0.2 | `ResearchQuery.cs:268-280`; `research.json:31`; loader range (0,1] `ResearchContent.cs:787-788` | M |
| Which nodes get modifiers | only `Tree == Technology` nodes whose `Branch` equals the university type's branch: subtree technologies of that subtree; trunk (Branch −1) and Civics ALWAYS cost BaseCost | `ResearchQuery.cs:307-321` (filter `:316`) | M |
| Modifier rows | the polity's `ResearchCostModifierRow`s multiplied in table order; an unknown type key or a factor ∉ (0,1] THROWS `InvalidOperationException` | `ResearchQuery.cs:311-335` | M |
| Modifier source | InstitutionsSystem rebuilds the table every step: per (owner, type) `Factor = 1 − maxResearchCostReduction × (1 − e^−X)`, X = maturity-weighted count; maxResearchCostReduction = 1/3 ⇒ Factor ≥ 2/3 | `InstitutionsSystem.cs:90-104`; `Sim.Core/State/Institutions.cs:242-243`; `Sim.Data/content/sim.json:290` | M / I (≥ 2/3) |
| University types | 5, keys 1–5, one per subtree (`military_university` … `agricultural_university`); loader requires each to name a subtree id but NOT one-per-subtree (a test pins that) | `research.json:257-288`; `ResearchContent.cs:1081-1100`; `ResearchContentTests.cs:90` | M |
| Civics costing | same U·K^m model and validators; university relevance optional (≤ 1 primary) and has no cost effect | `ResearchContent.cs:880-883`; `ResearchQuery.cs:316` | M |
| Eureka ceiling base | ceiling = 0.4 × BASE cost (not EffectiveCost); each credit capped at the remaining EffectiveCost | `ResearchSystem.cs:140, 162-163, 223-234`; test `ResearchEurekaCreditTests.cs:227` | M |
| No Age / depth / era term | cost is content-only; no runtime Age multiplier; not depth-derived (pinned) | `ResearchQuery.cs:279-280`; `ResearchContentTests.cs:108-131` | M |

---

## Q4. Eurekas and foreign exposure

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| Timing | ResearchSystem step 2, every step, for every AVAILABLE node (index order), conditions evaluated on PREV, independent of the target | `ResearchSystem.cs:27-33, 133-174` | M |
| Fire-once | an unfired Eureka whose condition holds appends `ResearchEurekaRow(Polity, Node key, Eureka INDEX)` and fires once even if the pool is full (then it pays 0) | `ResearchSystem.cs:145-153`; `WorldState.cs:904-910` | M |
| Dialect | the D-020 predicate grammar plus the research dialect: `AND/OR/NOT` aliases, bare atoms (node ids), caller-bound quantities `stock_<good>` (goods.json name, `-`→`_`) and registered variables | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-48`; `ResearchContent.cs:518-536` | M |
| Scope | knowledge-only conditions: once at polity scope; any settlement-scoped operand (variable or stock): true if it holds in AT LEAST ONE settlement the holder controls (table order); unpublished variable reads 0 | `ResearchQuery.cs:414-436` | M |
| NOT allowed? | yes in Eureka conditions (no `UsesNot` check; dead-check treats NOT as "can hold") | `ResearchContent.cs:572-580`; `Predicate.cs:105` | M |
| Weights | explicit on all of a node's Eurekas or on none (then ceiling ÷ N each); each ∈ (0, ceiling]; sum ≤ ceiling + 1e-12 | `ResearchContent.cs:545-586` | M |
| Pool | ONE pool per node shared by Eureka and foreign exposure: ceiling = `accelerationCreditCeilingFraction (0.4) × BaseCost`; headroom = ceiling − credited(eureka) − credited(exposure); provenance per source in `ResearchCreditRow`; nothing overflows to another node | `ResearchSystem.cs:140-173, 236-251`; `research.json:32-36`; `WorldState.cs:912-920` | M |
| Reconciliation | the LAST Eureka of a node to fire pays `EurekaTotalWeight × BaseCost − already credited`, so a full set lands exactly on the entitlement | `ResearchSystem.cs:155-161`; `ResearchContent.cs:588`; tests `ResearchEurekaCreditTests.cs:61-135` | M |
| Evaluable today (17/81) | stock: copper_smelting (`stock_copper_ore > 0`), tin_bronze (`stock_tin_ore > 0`). Knowledge: iron_bloomery←copper_smelting; paper←cuneiform OR hieroglyphic OR chinese_script; windmill_post←gearing; printing_press←olive_press_beam; steam_atmospheric←mining_shaft; railway[0]←joint_stock (a CIVIC), railway[1]←mining_shaft (0.2 each); dynamite←mining_shaft; telegraph←railway; vaccination←cattle; germ_theory←fermentation_grain; photography←optics_ibn_haytham; evolution←carrack OR caravel; aircraft←bicycle; computer←jacquard. No condition reads a registered variable | content parse; pinned `ResearchContentTests.cs:152-183, 187-208` | M |
| Not evaluable (64) | `when: null`, class `future-system:<system>`: resources/terrain 34, world events 19, institutions 7, goods state 2, world state 1, construction state 1 | content parse; loader `ResearchContent.cs:566-571` | M |
| Order-less runs | MEASURED by the attribution test: with no order, no evaluable Eureka holds on an available node within 300 turns, so order-less runs write no research row (the comment says "18", content has 17 — stale count) | `Sim.Tests/Kernel/IntegratedPinAttributionTests.cs:1063-1068`; `ResearchContentTests.cs:176` | M |
| `ResearchExposureRow` | `(Polity, Node key, double Offered)` cumulative offer — an INPUT seam; NO WRITER exists (only the deserializer and tests add rows); credited up to the shared headroom, once per increment | `WorldState.cs:922-931`; `ResearchSystem.cs:167-173`; grep of `Sim.Core`/`Sim.Ui`/`Sim.Cli` | M |

---

## Q5. Prerequisites and availability

| Rule / query | Finding | Evidence | Label |
|---|---|---|---|
| Parse | `prereq` (null = root) parsed in the research dialect over node ids of BOTH trees; unknown name → "missing or malformed prerequisite" | `ResearchContent.cs:447-460` | M |
| Operators | AND, OR, nested parentheses; NOT refused ("completing knowledge can never make a node LESS available"); no variables/quantities; self-reference refused | `ResearchContent.cs:461-468` | M |
| Cycles | iterative three-colour DFS; message names the cycle "a requires b requires … a" | `ResearchContent.cs:471-472, 1261-1304` | M |
| Depth | authored `depth` must equal the longest prerequisite path (union over OR) — derived data; any prereq change cascades to every descendant's `depth` | `ResearchContent.cs:474-484` | M / I (cascade) |
| Trunk vs subtree | a trunk technology may not require a subtree technology; every trunk technology must be reachable before the stage (prereqs may pass through Civics) | `ResearchContent.cs:491-500, 602-608` | M |
| Orphans | refused: a node with no dependent, no unlocked entity and no capability string | `ResearchContent.cs:591-600` | M |
| Reachability | every node reachable by some completion sequence (stage open) | `ResearchContent.cs:615-618` | M |
| Research stage | `researchStage.requires` parsed over node ids only, AND/OR, no NOT; must be satisfiable from pre-stage knowledge (else "deadlock") | `ResearchContent.cs:502-507, 609-611` | M |
| Stage content | `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman` — a hand-written summary of building.university + building.library + inst.university (expanded); NO validator ties it to those entity requirements; legal_code_roman (civic 1002) is required (pinned) | `research.json:253-256`; `ResearchContentTests.cs:233-243` | M |
| Stage effect | all five subtrees open together when the stage holds; trunk and Civics always open | `ResearchQuery.cs:80-94` | M |
| `IsAvailable` | not completed AND (trunk/civic OR stage reached) AND prerequisite expression holds on completed knowledge AND (non-recursive OR every FINITE node of its own subtree complete) | `ResearchQuery.cs:99-116` | M |
| Monotonicity | completed rows are never removed; a completed node is never available again | `WorldState.cs:894-902`; `ResearchQuery.cs:106` | M |
| `IsKnowledgeEligible(entity)` | null requirement ⇒ true; node atom reads completed; institution atom reads that institution's own eligibility (memoised); declared-unresolved reads false | `ResearchQuery.cs:550-586` | M |
| Atom precedence in entity requirements | a name resolves FIRST as a node id, THEN as an institution short id (`inst.<name>`), then as a declared-unresolved name — e.g. `census` → civic node 1003, `library` → `inst.library` | `ResearchContent.cs:1119-1160`; `research.json` entities | M |
| `RequirementMet(expr)` | same atom reader as entity eligibility, for requirements authored outside research.json (sim.json `taxationRequires`), parsed by `ParseRequirement`: node ids only, no NOT, non-empty | `ResearchQuery.cs:588-600`; `ResearchContent.cs:316-335` | M |
| `CheapestAvailable` | composite key (EffectiveCost, key): ascending index (= key) scan, replace only on strictly lower cost ⇒ ties go to the LOWER key; tree filter optional; a read-only helper for UI/CLI drivers | `ResearchQuery.cs:145-167`; tie-dense test `ResearchEngineTests.cs:597` | M |
| Tie consequence | technologies (keys ≤ 426) always win cost ties against civics (≥ 1001); appended nodes lose ties to older ones | from the key-order scan | I |

---

## Q6. Content loader validations (`ResearchContentLoader`; every message is prefixed `research.json: `, `ResearchContent.cs:1362-1363`)

| # | Rule | Location | Error gist |
|---|---|---|---|
| 1 | JSON shape: every `[JsonRequired]` member present, explicit null in non-nullable refused | `ResearchContent.cs:303, 343-358, 1369-1532` | "is not valid JSON or is missing required values" |
| 2 | Unknown JSON members are IGNORED (default System.Text.Json; `_doc` members load today) | `ResearchContent.cs:303`; `research.json:3, 255` | — (no error: a `retired`/`alias` field would be silently ignored) |
| 3 | schema = `civ-sim/research@2`; `source.corpusSha256` 64 lower-case hex | `:363-366` | "schema is …, expected …"; "must be the 64-character lower-case hex SHA-256" |
| 4 | tuning (see Q1/Q3/Q4): RP coefficient/exponent/anchors; floor ∈ (0,1]; ceiling ∈ (0,1]; `accelerationCreditSources` exactly `[eureka, foreign_exposure]`; U > 0; K > 1; cityStatePaceFraction ∈ [0,1) | `:760-806` | "must be in (0, 1): Research Points are sublinear…" etc. |
| 5 | trees exactly `[technology, civics]` | `:1048-1057` | "two top-level trees and no others (D-044 R3)" |
| 6 | branches exactly the five ids `military, medicine, engineering, natural_science, agriculture` in this order, keys ascending, numbers `1.1`–`1.5` (ids hard-coded in `RuledBranchIds`) | `:286, 1059-1079` | "no Industry & Energy, Naval or Science subtree" |
| 7 | universityTypes: key ≥ 1 ascending, unique id, branch must be a subtree id | `:1081-1100` | "a specialized university reduces the cost of one subtree's nodes" |
| 8 | ≥ 1 technology and ≥ 1 civic | `:379-380` | "technologies is empty" / "civics is empty" |
| 9 | key ≥ 1 and STRICTLY ASCENDING across technologies THEN civics (file order = key order) | `:383-390` | "is not strictly greater than the previous key" |
| 10 | id matches `[a-z][a-z0-9_]*`, not AND/OR/NOT; ids unique across both trees | `:391-394, 405-414` | "duplicate node id … one authoritative graph" |
| 11 | name non-empty; age ∈ A1..A9; cost finite > 0; `effects.immediate` empty | `:395-404` | "No immediate-effect kind is ratified … permanent modifier (law 2)" |
| 12 | civics carry NO `branch` property; technologies MUST carry `branch` (null = trunk or a subtree id) | `:422-445, 1483-1485` | "a Civics node carries no 'branch'" / "'branch' is required on a technology" |
| 13 | prerequisites: parse, no variables/quantities, no NOT, no self | `:447-469` | "missing or malformed prerequisite"; "NOT is not allowed in a prerequisite" |
| 14 | acyclic | `:1261-1304` | "prerequisite cycle: …" |
| 15 | depth = longest path | `:474-484` | "depth N does not match the longest prerequisite path" |
| 16 | trunk technology may not require subtree knowledge | `:491-500` | "invalid subtree assignment … move one of them" |
| 17 | stage parse (node ids, AND/OR only) | `:502-507` | "researchStage.requires is an AND / OR expression over node ids only" |
| 18 | entities: kind ∈ {building, infrastructure, institution, unit, activity, project, recipe}; id prefix `building./infra./inst./unit./activity./project./recipe.`; unique id; `requires` resolves to node ids, institution short ids or declared `unresolved`; no variables/quantities; an institution may not name itself; `unresolved` exact; institution reference graph acyclic. **NOT is NOT refused in entity requirements** | `:293-295, 1102-1216` (`:1169-1170` has no `UsesNot` check) | "invalid reference … declare a corpus reference that resolves to nothing in 'unresolved'"; "institution requirement cycle" |
| 19 | unlock lists: each listed id exists, has the list's kind, not listed twice; **a Civics node may unlock ONLY Institution entities**; lists must be the exact reverse index of entity requirements (listed ⇔ requirement names the node) | `:1218-1258` | "a Civics node makes institutional forms eligible … cannot unlock a …"; "the unlock lists are the reverse index of the entity requirements, which are authoritative" |
| 20 | Eurekas: text/kind/justification/system non-empty; source ∈ {authored, inherited}; weight rules (Q4); `evaluable_now` ⇔ `when` present; class `machine-evaluable` or `future-system:<system>`; `when` resolves (node ids, `stock_<good>`); sum ≤ ceiling | `:517-589` | "invalid Eureka reference"; "the weights sum to …, above the 0.4 acceleration ceiling" |
| 21 | orphans refused; capability strings non-empty | `:591-600` | "orphan node — nothing requires it, it unlocks no entity and declares no capability" |
| 22 | trunk reachable pre-stage; stage satisfiable pre-stage; every node reachable | `:602-618` | "Invalid cross-tree dependency"; "(a deadlock)"; "unreachable" |
| 23 | dead Eureka: a knowledge condition that cannot hold while its node is researchable | `:620-647` | "(a dead Eureka)" |
| 24 | implied Eureka: a knowledge-only condition guaranteed by every path to the node | `:649-682` | "implied Eureka … a flat cost cut, not a circumstance" |
| 25 | cost rationale (Q3) | `:814-851` | "the cost must come from the content factors, never be set independently" |
| 26 | university relevance: type exists; `name` = type name minus " University"; role primary/secondary; no duplicate; a subtree node's primary serves its own subtree; technologies exactly ONE primary; civics ≤ 1 | `:853-885` | "a technology names exactly one primary university domain" |
| 27 | researchSets: `finite` described; recursive/speculative ids exist, unique, disjoint; recursive ⇔ `repeatable` descriptor; TR ⇔ recursive; TS ⇔ speculative; a finite node may not require a recursive one; a recursive node must be a subtree technology; `repeatable.availability.subtree` = own subtree; `finite_nodes_to_exhaust` = the CURRENT count of finite nodes of that subtree (engineering 125, military 36, medicine 21, natural_science 43, agriculture 14) | `:887-945`; content parse | "disagrees with the N finite nodes of its subtree" |
| 28 | baseline: non-empty; ids `baseline.*`, unique, not node ids | `:947-968` | "a baseline capability lives outside the graph" |
| 29 | sectorActivities: exactly 5 in sector order `farming, herding_fishing, extraction, crafting, construction`; baseline identity id/name/providedBy (declared baseline ids); researched entities are ACTIVITY entities mapped once; mode replaces/joins; label or entity name | `:299, 978-1046` | "maps each of the 5 labour sectors exactly once"; "is mapped twice" |

**Cross-file validators (load fails) [M]:** goods.json recipe entities must be `recipe` entities; project entities must be building/institution/infrastructure; `founds.entity` an institution and `founds.universityType` a research.json type id (`Sim.Core/Systems/SimConfig.cs:1422-1466`); sim.json road-class entities exist (`:1548-1559`); `trade.entity` exists and is an activity (`:1592-1600`); `taxationRequires` non-empty (`:1570-1572`) and parses via `ParseRequirement` (`:1602-1615`); **F1: `taxationMinAge` (3) must be ≥ the earliest Age at which `taxationRequires` holds by the nodes' `age` TAGS** (`:1616-1628, 1633-1646`); `institutions.universities.healthType` names a university type (`:1537-1544`); ages.json research facts name existing node ids, `research` facts take no `min`, `research_count` min ≤ tree size (`Sim.Core/Systems/Ages/AgeContent.cs:258-288`); Military Realization facts may never be research (`AgeContent.cs:234-238`); unit-families `realizedBy` must be an existing entity (`Sim.Core/Systems/Ages/UnitFamilyContent.cs:220-221`).

**NOT validated (design latitude and traps) [M]:** node `domain` vocabulary (free text; the UI makes one Civics lane per distinct domain — Q12); `repeatable.availability.speculative_included` (ignored); NOT inside entity requirements and Eureka conditions (row 18, Q4); one-university-type-per-subtree (test only); agreement of `researchStage.requires` with the university entities (row Q5); `emerged` text (but a TEST requires 83 marker-free texts exactly — Q13); `family`/`generation` semantics (tests only, `ResearchContentTests.cs:541-563`).

---

## Q7. Persistence

### 7.1 Rows holding research state [M]

| Section | Table | Row fields (width) | Identifier stored | Writer | Evidence |
|---|---|---|---|---|---|
| 41 | ResearchTargets | Polity, Node (8) | node KEY | ResearchSystem | `CanonicalSchema.cs:259, 725-732`; `WorldState.cs:881` |
| 42 | ResearchProgress | Polity, Node, Progress bits (16) | node KEY | ResearchSystem | `:260, 734-742`; `WorldState.cs:892` |
| 43 | ResearchCompleted | Polity, Node (8) | node KEY | ResearchSystem; RevoltSystem appends for a polity it creates (sanctioned shared table) | `:261, 744-751`; `WorldState.cs:902`; `SystemCatalog.cs:103-110, 360-367`; `RevoltSystem.cs:11-19, 189` |
| 44 | ResearchEurekas | Polity, Node, Eureka (12) | node KEY + Eureka list **INDEX** | ResearchSystem | `:262, 753-761`; `WorldState.cs:904-910` |
| 45 | ResearchCostModifiers | Polity, UniversityType, Factor bits (16) | university-type KEY | InstitutionsSystem (rebuilt every step) | `:265, 763-771`; `WorldState.cs:933-956`; `InstitutionsSystem.cs:90-104` |
| 46 | ResearchCredits | Polity, Node, Source, Amount bits (20) | node KEY + source (1 eureka, 2 exposure) | ResearchSystem | `:263, 773-782`; `WorldState.cs:912-920` |
| 47 | ResearchExposures | Polity, Node, Offered bits (16) | node KEY | none (input seam) | `:264, 784-792`; `WorldState.cs:922-931` |
| 48–50 | AgeStates / AgeEligibility / AgeTransitions | Age keys, surge key, counts | no node identifier (eligibility rebuilt every step) | Age systems | `:794-834`; `AgeEligibilitySystem.cs:41` |
| 56 | Institutions | Id, Polity, Settlement, Type, FoundedTurn, Maturity (32) | university-type KEY (`Type`) | InstitutionsSystem | `:918-929`; `WorldState.cs:1106-1107` |

Polity may be a local holder key (negative) for city-state rows (`SettlementKnowledge.cs:24`) [M]. Nothing persists a node id string or a node INDEX; the only persisted positional index is `ResearchEurekaRow.Eureka` [M].

### 7.2 Schema, readable set, append-only premise, migrations [M]

- Version **34**; readable set **{32, 33, 34}** through the one predicate `IsReadable` (`CanonicalSchema.cs:181-195`);
  research tables are v26 (`:86-101`) and are read unconditionally for all three readable versions — the version gates
  begin after them (`:1420-1470` vs `:1569, 1584`).
- "Adding state? three edits, same file: Write, Read, ExpectedLength" (`CanonicalSchema.cs:14-16`). The APPEND-ONLY
  premise the readable set depends on is stated at `:197-214`: `BytesAppendedSince` / `WorldHash.ComputeHexAsSchema` are
  exact only "because every table a readable version lacks was APPENDED after every table it has". [I] Therefore any new
  research state (e.g. a Civics Points pool/target) must be appended as sections ≥ 62 with a version bump, and widening
  an existing research row (e.g. adding a tree field to `ResearchTargetRow`) would break the v32/v33 projections,
  `ReplayGuard.MatchesRecorded` (`ReplayGuard.cs:33-34, 67-74`) and the migrating reader. `docs/m6-foundations-audit.md:18`
  records v35 as reserved for fog (doc).
- Migrations exist only for construction tables (v32→v33→v34); there is NO research migration and no content-aware
  load hook: `Snapshot.Load(Stream, TerrainSet?)` receives no SimConfig and only asserts construction invariants
  (`Snapshot.cs:55, 130, 174-186`); `CanonicalSchema.Read` reads keys verbatim (`:1420-1470`).

### 7.3 Load behaviour when content and save disagree

| Saved row | On load | First step after load / forever | Evidence | Label |
|---|---|---|---|---|
| any research row with a key ABSENT from content (retired/merged) | loads (no content check) | — | `CanonicalSchema.cs:1420-1470`; `Snapshot.cs:55-165` | M |
| ResearchTargets → absent key | — | treated as "no target"; the row is removed at the end of the step; an AI polity orders a new target; UI shows no target, but the action surface's `hasTarget` is true with no clear-target descriptor | `ResearchSystem.cs:104-109, 199-212`; `AiResearchPolicy.cs:55-59`; `Sim.Ui/Progression/ResearchNodeViews.cs:60`; `AvailableActionsQuery.cs:280, 293-296` | M |
| ResearchProgress → absent key | — | ignored, never removed (only completion removes progress) ⇒ dormant forever, still in the canonical stream and hash | `ResearchSystem.cs:125-131, 189-197` | M |
| ResearchCompleted → absent key | — | ignored by `CompletedMask` (no eligibility/availability effect) but `IsCompleted(by key)` still answers true; copied verbatim to revolt-born polities (no content filter); never removed | `ResearchQuery.cs:30-51`; `KnowledgeTransfer.cs:29-44` | M |
| ResearchEurekas → absent key | — | ignored | `ResearchQuery.cs:404-412`; `ResearchSystem.cs:144-150` | M |
| ResearchEurekas → existing key whose Eureka list was reordered/shrunk | — | the stored INDEX re-binds to whatever Eureka now sits there (treated as already fired); an index past the end is ignored | `ResearchSystem.cs:144-150`; `WorldState.cs:904-910` | M |
| ResearchCredits → absent key | — | ignored, dormant | `ResearchQuery.cs:441-451` | M |
| ResearchCostModifiers → university type ABSENT from content | loads | **throws `InvalidOperationException`** on the first `EffectiveCost` (ResearchSystem step 2, before InstitutionsSystem rebuilds the table later in the same step) — the world cannot step | `ResearchQuery.cs:311-329`; `ResearchSystem.cs:139`; `pipeline.json:21, 25` | M / I (crash path) |
| Institutions → type absent | loads | matures forever; no modifier rebuilt for it (types iterated from content); UI shows "Institution" | `InstitutionsSystem.cs:62-104`; `InstitutionsQuery.cs:144-151` | M |
| Content has NEW keys | nothing to load | new nodes start uncompleted; available if prerequisites hold | key-only rows | M |
| Partial progress ≥ a LOWERED EffectiveCost | — | completes at the next step if available, with no RP spent | `ResearchSystem.cs:189-197` | M |

### 7.4 Is content part of identity? [M]

- **World hash:** SHA-256 over the canonical stream = seed, clock, terrain presence + terrain content hash, then the
  tables (`WorldHash.cs:14-22`; `CanonicalSchema.cs:286-299`). Research content and its hash are NOT included.
- **Snapshot header:** magic, version, seed, turn (+ topology trailer) only (`Snapshot.cs:37-46`).
- **Session manifest:** seed, size, settlements, aiEmpires, schemaVersion, buildSha/buildDate, file names, platform —
  no content hash (`SessionManifest.cs:46-89, 173-208`). Forensic records carry only the terrain content hash
  (`Sim.Core/Observability/Forensic/ForensicSession.cs:49, 98-101`). Content is an embedded resource of `Sim.Data`
  (`Sim.Data/DataFiles.cs:54, 65-69`), so the only content identity is the build identity.
- **Replay identity:** `ReplayProof` proves settlement and polity ids only; "a research node" is explicitly not an
  addressed entity (`ReplayProof.cs:107-121`). A recorded `SetResearchTarget(key)` replays as whatever that key means in
  the current content [M]; `CorpusSha256` is format-checked only (`ResearchContent.cs:365-366`).
- **UI save = order log + trace, not a snapshot** (`Sim.Ui/UiSession.cs:1250-1256, 783-792`). Resume re-founds and
  replays: same schema ⇒ trace checked EVERY turn and a divergence is refused as `SessionIncompatibleException`; other
  schema ⇒ replayed unchecked with a notice (`UiSession.cs:1048, 1063-1076`). [I] Hence a content-only change WITHOUT a
  schema bump makes every v34 session whose replay diverges (any research progress, any AI empire) unresumable; with a
  bump they resume as new worlds.

### 7.5 What moves when content changes with no state change

Content alone moves NO world hash [M, 7.4]. A pinned hash moves only when the change alters what a RUN does [I]:

| Pin | Research in the run? | Moves on a content change? | Evidence | Label |
|---|---|---|---|---|
| `ci.yml` FOUNDED_GOLDEN, SnapshotTests founded golden; DrivenGoldenTests `Golden`; FirstReignTests | no RESEARCH orders (only labour/sector orders where any), `aiEmpires = 0`, no research row (measured) | only if ZERO-knowledge behaviour changes: null-requirement entities, baseline, sector 'replaces' identities (farming yield), the trade gate, recipe gating, or a root node with an evaluable Eureka | `.github/workflows/ci.yml:211`; `Sim.Tests/Kernel/DrivenGoldenTests.cs:92, 470-492` (R1/R2a re-pins); `IntegratedPinAttributionTests.cs:1048-1068`; decoded `first-reign-orders.bin` (Appendix B) | M (precedent) / I |
| toy `GoldenHash` (SnapshotTests) | toy pipeline has no research system | never | `IntegratedPinAttributionTests.cs:1038-1046` | M |
| AI-empire pins: `AiFoundingAttributionTests` (S1cTurn300 etc.), `GranaryAttributionTests`, `IncrementalConstructionAttributionTests`, `ReservationsLayoutAttributionTests`, `D054PairAttributionTests`, `WrapAttributionTests`, `EmpireConstructionStockpileTests`, `EmpireFoodAllocationTests` | yes — `AiOrders` runs `AiResearchPolicy` every turn | yes, whenever AI choices, costs, availability or eligibility change | `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:21-76`; `Sim.Core/AiOrders.cs:67-68` | I |
| Attribution twins (`TestConfigs.PreTradeKnowledge`, `PreRecipeKnowledge`, `PreR1` chain) | built by mutating the CURRENT research.json (removes `trade`) | the "OLD" controls move with the content too | `Sim.Tests/TestUtil/TestConfigs.cs:260-281`; `IntegratedPinAttributionTests.cs:761-776` | M / I |
| Allocation scenario S42-ALLOC-H (test + CI leg + split-report fixtures) | player researches keys 12, 17, 2, 8, 52, 74, 53 | yes if any cost/prereq on that chain changes: premises "track_road completes 98→99, stone_dry 115→116" | `Sim.Tests/Kernel/AllocationCiLegTests.cs:133-139`; `AllocationScenario.cs:19-20, 91-94`; `ci.yml:335-362`; `Sim.Tests/Fixtures/split-report/*` | M |
| v33 session fixture (`v33-session-q17`) | AI ordered SetResearchTarget key 2 at turn 0; premise (i) replays the log and must equal EVERY trace hash | yes if key 2 is retired/unavailable, completes earlier, or any new Eureka/root writes a row in 6 turns | `Sim.Tests/State/ConstructionHoldsV33FixtureTests.cs:130-134, 221-237` | M / I |
| v32 session / save fixtures | no research orders | only zero-knowledge behaviour (as row 1) | `Sim.Ui.Tests/Batch2SessionResumeTests.cs:1-27`; `Sim.Tests/State/ConstructionSchemaV33Tests.cs:22` | I |
| CI `--check` docs | — | ANY byte change to research.json (embedded SHA) requires regenerating `docs/research-corpus-audit.md`, `docs/research-calibration-report.md`, `docs/research-gameplay-unlock-audit.md` | `ci.yml:22-28`; `scripts/research_lib.py:29-31` | M |
| Content pins in tests | — | yes (Q13 table) | Q13 | M |

---

## Q8. Masks and arrays

| Structure | Sized / indexed by | Persisted? | Evidence | Label |
|---|---|---|---|---|
| `CompletedMask`, `AvailableMask` | `bool[Nodes.Count]` by dense index | no (rebuilt per call) | `ResearchQuery.cs:30-41, 118-124` | M |
| `SettlementKnowledge.MaskOf` | controller mask ∪ the settlement's own local mask (controlled), own mask (uncontrolled); null when no research content or no Controls rows | no | `SettlementKnowledge.cs:43-52` | M |
| Trade gate | `TradeQuery.SettlementKnowsTrade` → `IsEntityEligible(activity.trade)` over `MaskOf`; evaluated once per settlement per step on PREV; a pair trades iff both ends know | no | `TradeQuery.cs:22-28`; `SettlementKnowledge.cs:56-63`; `TradeArbitrageSystem.cs:120-136` | M |
| Recipes / farming yield | `CraftingQuery.KnowledgeOf` = `MaskOf` (controller ∪ local); `HarvestsWildFood` reads `MaskOf` | no | `CraftingQuery.cs:32-33`; `ProductionSystem.cs:477-482`; `LabourActivities.cs:107-113` | M |
| `ResearchSystem.rowOf` | `int[n]` | no | `ResearchSystem.cs:122-131` | M |
| AI goal masks | `bool[n]` closures, `bool[Entities.Count]` expansion | no | `AiResearchPolicy.cs:140, 184-195, 215` | M |
| Predicate atom ids | node atom = node index; institution atom = `n + entityIndex`; unresolved sentinel `n + m` (`UnresolvedAtom`) | no (rebuilt at load) | `ResearchContent.cs:238, 1146-1153`; `ResearchQuery.cs:580-586` | M |
| `PrerequisiteNodes`, `Dependents`, `UnlockedEntities`, `FiniteNodesBySubtree`, sector identity `Entity`/`Baseline` | dense indices | no | `ResearchContent.cs:155-169, 225-227, 70-79` | M |
| Lookup arrays | `SortedKeys` (binary search by key), `SortedIds` + `SortedIdIndex` (ordinal id search) | no | `ResearchContent.cs:229-252` | M |
| UI | `ResearchGraph` vertices in content order + `_vertexOf[n]`; `ResearchSnapshot.Nodes` (content order), `TargetIndex` (content index); `NowUsable.Entity` (entity index, UI history rebuilt by replay) | no | `Sim.Ui/Progression/ResearchGraph.cs:34-75`; `ResearchNodeViews.cs:54-60`; `Sim.Ui/ViewModel/DiscoveryShortcuts.cs:35-38, 73-88` | M |
| `ResearchEurekaRow.Eureka` | position in the node's Eureka list | **YES** | `WorldState.cs:904-910` | M |

**Appending k technologies** shifts every civic's dense index by +k (technologies precede civics: `ResearchContent.cs:374-377`)
and every institution atom id by +k [M]. No persisted state depends on dense indices [M, 7.1]. The only tests pinning
absolute positions are `ResearchContentTests.cs:39-43` (`Nodes[424]`, `Nodes[425]`, `i < 424`) [M]; others use
`IndexOfId`/`TechnologyCount + i` [M] (`ResearchContentTests.cs:69`). Iteration order = key order everywhere (Eureka
credit, completion, cheapest scans), so new keys rank after old ones in ties [I].

---

## Q9. Ages

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| `CheckAdvance` | rejections in order: UnknownPolity, AtFinalAge, WrongTargetAge (must be current + 1), UnknownSurge, NotEligible | `Sim.Core/State/AgeQuery.cs:291-310` | M |
| Eligibility | ALL core milestones met AND ≥ `supportingRequired` supporting met AND those span ≥ `minCategories` categories | `AgeQuery.cs:118-155` | M |
| `research` fact | ANY listed node completed (count of listed keys completed ≥ 1); ids resolved to KEYS at load; takes no `min` | `AgeContent.cs:30, 258-270`; `AgeQuery.cs:176-183` | M |
| `research_count` fact | completed rows whose key ∈ the tree's sorted keys (technology / civics / any) ≥ min; min ≤ tree size at load | `AgeContent.cs:32, 272-288`; `AgeQuery.cs:184-193` | M |
| Research facts in content | listed in Appendix C; cores: A2 cereal_cultivation; A3 arsenical_bronze \| tin_bronze; A4 iron_bloomery; **A5 legal_code_roman (civic) + coinage_electrum**; **A6 heavy_plough (agriculture subtree)**; **A7 printing_press (engineering subtree)**; A8 steam_rotary (engineering); A9 electricity_generation + internal_combustion (engineering). research_count technology ≥ 200 (A8), ≥ 280 (A9) | `Sim.Data/content/ages.json:27-168` | M |
| Consequence | entering A6+ requires subtree knowledge, hence the research stage, hence the civic legal_code_roman; adding technologies makes the 200/280 thresholds a smaller share of the tree | from the rows above | I |
| AdvanceAge order | kind 7; TargetId = Age being entered (2..9); Amount = surge key; delivered to step t → t+1; first valid order of the polity wins | `OrderLog.cs:91-104`; `AgeQuery.cs:336-345`; `AgeTransitionSystem.cs:82` | M |
| AI | advances on the first eligible turn with the lowest surge key | `Sim.Core/Systems/Ages/AgeAdvancePolicy.cs:23-30` | M |
| Surges | stored on `AgeStateRow.Surge`; **no numeric effect anywhere**; "Age of Learning" (key 1, `expectedEffect: temporary acceleration of research throughput`) is NOT implemented — nothing outside the Age systems/Revolt reads `Surge` | `ages.json:177-185`; `WorldState.cs:958-971`; grep (only `AvailableActionsQuery.cs:321`, `RevoltSystem.cs:208`) | M |
| Age/calendar → research | none: research reads no Age, date or dt; node `age` tags are metadata — except the F1 load validator (tax gate) and UI labels | `ResearchContent.cs:138-139`; `ResearchSystem.cs:56`; `SimConfig.cs:1633-1646` | M |
| Age → research AI | the AI core goal is the NEXT Age's unmet core research milestones (prerequisite closure) | `AiResearchPolicy.cs:208-235` | M |
| Military Realization | must be a `formations` fact, never research | `AgeContent.cs:234-238` | M |
| Unit modernization | Age-driven (AgeTransitionSystem → `MilitaryQuery.PlanFor`), never research-driven | `AgeTransitionSystem.cs:91-101`; `Sim.Core/State/MilitaryQuery.cs:75-80` (no research reference) | M |

---

## Q10. Consumers of completion (verification of `docs/research-gameplay-unlock-audit.md` §1)

Completion builds nothing; consumers ask the evaluator [M] (`ResearchSystem.cs:54-56`; `ResearchQuery.cs:550-622`).

| Consumer | Content link | Enforcing code (on PREV) | Doc §1 claim (`docs/research-gameplay-unlock-audit.md:7-17`) | Verdict |
|---|---|---|---|---|
| Recipe | goods.json `recipes[].entity` → `recipe.*` (pottery_firing ← pottery_open_fired, bronze_casting ← tin_bronze; weaving, toolmaking null) (`goods.json:105, 124, 139, 163`) | `ProductionSystem.cs:477-482` → `CraftingQuery.IsRecipeAvailable` (`CraftingQuery.cs:36-58`) over settlement knowledge | ProductionSystem — CraftingQuery.IsRecipeAvailable | ✓ [M] |
| Activity (labour identity) | `sectorActivities` → activity.farming/herding (replaces), logging/mining (joins) | labels: `LabourActivities.cs:118-123, 162-225`; **yield**: `HarvestsWildFood` → wild-food rates in `ProductionSystem.cs:276-288` while `activity.farming` is ineligible (`sim.json:7-9` enabled) | "LabourActivities (labour identity; production unchanged)" | **partly ✗ — DOC≠CODE**: farming eligibility changes the farming YIELD (InfoQuery states it correctly, `InfoQuery.cs:2010-2016`) [M] |
| Trade | sim.json `trade.entity` = activity.trade (`sim.json:230`) ← node trade | `TradeArbitrageSystem.cs:120-136` → `TradeQuery.SettlementKnowsTrade` | TradeArbitrageSystem — TradeQuery.CanTrade | ✓ [M] |
| Tax | sim.json `taxationRequires: "taxation"`, `taxationMinAge: 3` (`sim.json:268-270`) | `GovernanceSystem.cs:69` → `Governance.CanLevyTax` (`Governance.cs:251-289`: Inert → NeedsKnowledge → NeedsAge → NeedsSeat → Open) | GovernanceSystem — Governance.CanLevyTax | ✓ [M] |
| Age | ages.json research facts | `AgeTransitionSystem` → `AgeQuery.ResolveAdvance/CheckAdvance` (`AgeQuery.cs:291-345`) | AgeTransitionSystem — AgeQuery.CheckAdvance | ✓ [M] |
| Stage | `researchStage.requires` | `ResearchSystem.cs:100-101` → `AvailableMask` → `StageReached` (`ResearchQuery.cs:80-124`) | ResearchSystem — ResearchQuery.StageReached | ✓ [M] |
| Institution (university) | goods.json university projects: `entity: building.university` + `founds.entity: inst.university` + `universityType` (`goods.json:224-305`) | `ConstructionSystem.cs:247` → `ConstructionQuery.IsProjectAvailable` → `IsKnowledgeEligible` checks BOTH entities (`ConstructionQuery.cs:136-148, 482-503`); `InstitutionsSystem.cs:72-88` founds from built Structures (no second knowledge check) | ConstructionSystem + InstitutionsSystem — IsProjectAvailable (founds) | ✓ [M] |
| Road | sim.json `roads.classes[].entity` → infra.road_track/built/paved/macadam/highway (`sim.json:256-261`) | `RoadDevelopmentSystem.cs:105-106` → `Plan` → `Routes` → **`BestKnownClass`** (`RoadDevelopmentQuery.cs:110-121, 155-159`) | RoadDevelopmentSystem — RoadDevelopmentQuery.IsClassKnown | **✗ DOC≠CODE (minor)**: `IsClassKnown` is called only by `InfoQuery.cs:1280` (and a test); same evaluator underneath [M] |
| Building | goods.json `projects[].entity` (granary, workshop: null requirement) | `ConstructionQuery.IsProjectAvailable` at ENQUEUE only (queued heads gate on capacity/materials, `ConstructionQuery.cs:30-36`) | ConstructionSystem — IsProjectAvailable | ✓ [M] |
| Unit | `unit.*` entities (23), unit-families `realizedBy` (23 identities) | none: eligibility is listed only (Lenses, InfoQuery card link `InfoQuery.cs:1318-1322`); recruitment is M7 | (not in §1; class F) | [M] |
| Revolt / annexation | completed rows | `KnowledgeTransfer.MergeInto` — union, no content filter; progress/Eurekas/credits not copied | — | [M] `KnowledgeTransfer.cs:20-44`; `RevoltSystem.cs:189` |
| AI policies | all of the above | `AiResearchPolicy`, `AiConstructionPolicy` (`:136, 257`), `RoadDevelopmentPolicy`, `AiGovernance` (`AiGovernance.cs:43`) | — | [M] |
| Glass Box / UI | all | `InfoQuery` (`:344-430, 1989-2042`), `AvailableActionsQuery` (`:276-306`), Lenses, ProgressionScreen | — | [M] |

**Universities [M].** Types: 5 (Q3). Requirement: `building.university` = `medicine_hippocratic AND (geometry_axiomatic OR algebra)`;
`inst.university` = `library AND legal_code_roman` where `library` → `inst.library` = `archive AND scribal_school`
(→ `inst.archive` = scripts AND stamp_seal; `inst.scribal_school` = scripts) (`research.json:28634` and the institution
entities). State: `InstitutionRow(Id, Polity, Settlement, Type = university-type key, FoundedTurn, Maturity)`
(`WorldState.cs:1106-1107`). Step: mature/decay on host viability → found one row per built university not yet founded
(one-turn lag) → rebuild `ResearchCostModifiers` from maturities (`InstitutionsSystem.cs:62-104`). Health: the
`medical_university` type lowers mortality (`sim.json:292`). Only the 5 subtree university types exist; no civics
institution has a realizing system (every other `inst.*` is eligibility-only) [M] (`Sim.Ui/Progression/Lenses.cs:66`).

---

## Q11. AI research (`Sim.Core/Systems/Research/AiResearchPolicy.cs`)

| Aspect | Finding | Evidence | Label |
|---|---|---|---|
| When | only AI-commanded roster polities; only when it has NO live target (a carried target that is unknown/unavailable counts as none) | `:48-60` | M |
| Output | an ordinary `SetResearchTarget` built by `ResearchQuery.TargetOrder`, appended to the order log by `AiOrders` before the step (replayable) | `:58-59`; `Sim.Core/AiOrders.cs:67-68` | M |
| Goals (ordinals) | 0 core: prerequisite-ancestor closure of the NEXT Age's unmet CORE `research` milestones; 1 road: the next unknown road class's entity (lowest class above the best known); 2 tax: atoms of `taxationRequires` while `KnowsTaxation` is false (knowledge half only); 3 university: each `founds` project's building + institution entity while not knowledge-eligible; institution atoms expanded to their own requirements | `:63, 105-181, 199-206, 208-235` | M |
| Closure | ancestors over `PrerequisiteNodes` (union over OR, node included); does NOT add the research stage's atoms for a subtree goal node | `:184-195, 227-233` | M |
| Choice | goal with the lowest (remaining closure cost Σ EffectiveCost over uncompleted closure nodes in index order, ordinal) among goals with ≥ 1 available node; then the cheapest available node in that goal (EffectiveCost, key); if no goal, the cheapest available node anywhere | `:66-101, 239-250` | M |
| Tie-breaks | strictly-lower replacement in ordinal / ascending-key order ⇒ lower ordinal, then lower key; tie-dense tests | `:81-101, 239-250`; `Sim.Tests/Systems/AiResearchGoalTests.cs:143`; `AiPolicyTests.cs:61` | M |
| Cross-tree | goals are tree-agnostic: tax goal reaches the civic `taxation` (+ technology ancestors); university goal reaches civic `legal_code_roman`; A5 core reaches `legal_code_roman`; fallback "cheapest anywhere" spans both trees (technologies win cost ties) | `:156-178, 213-235`; Q5 | M / I |
| Reads | `IReadOnlyWorldState` directly (completed, targets, roster, Age rows, controls), `cfg.Research/Ages/Roads/Governance/Institutions/Goods` — not `PolityKnowledge` | whole file | M |
| Gap | a core goal whose node sits in a closed subtree (A6 heavy_plough, A7 printing_press) has no stage atoms in its closure: once the trunk ancestors are done the goal has no available node and is skipped; the stage is reached only via the university goal or the cheapest fallback | `:97, 227-233`; Q9 | I |
| Not present | no civics strategy, no adoption logic, no use of Eurekas or of university factors beyond EffectiveCost | whole file | M |

---

## Q12. UI

| Surface | Grouping / behaviour | Hard-coded content? | Evidence | Label |
|---|---|---|---|---|
| Progression screen | two tabs `TreeTab { Technology, Civics }`; one `ResearchGraph` per tree; counts "completed / TechnologyCount", "/ CivicsCount" | tab enum (2 trees); no ids | `Sim.Ui/Progression/ProgressionScreen.cs:13, 126, 396, 616, 1413-1422` | M |
| Graph | vertices = the tree's nodes in content order + EXTERNAL anchors for other-tree prerequisites; edges = `PrerequisiteNodes`, AND/OR from `MustHoldAtoms` | none | `ResearchGraph.cs:44-75` | M |
| Layout | lanes: Technology = "Main Trunk" + the content's 5 branches; Civics = one lane per distinct node `domain` (ordinal sort; today all 7 = `governance` ⇒ one lane); tiers = content `depth` relaxed by edges; Age range shown as tier labels only; hard-coded nine Age names; barycentre rank `lane × 1000 + slot` | "Main Trunk", "From Technology/From Civics", `AgeNames[9]` | `ResearchTreeLayout.cs:115-146, 191, 444-453, 492-497` | M |
| Node state | Completed › CurrentTarget › Available › **Partial** (holds progress but not available — e.g. a subtree closed "after a content change") › Locked; lock reasons (prereqs, stage, subtree exhaustion) | none | `ResearchNodeViews.cs:11, 54-136` | M |
| Lenses | 7 fixed lenses; "Adopted civics are completed Civics nodes" (research = adoption) | lens list | `Sim.Ui/Progression/Lenses.cs:9, 62-68` | M |
| Action surface | ONE "Set research target" descriptor over all available nodes of both trees (+ clear) | none | `AvailableActionsQuery.cs:276-306` | M |
| Glass Box cards | node card classifies Simulated / ResearchOnly / KnowledgeOnly by enumerating consumers (projects, recipes, sector activities, roads, trade entity, unit `realizedBy`, institution dependents, Age milestones, tax atoms, stage atoms) | consumer types in code | `Sim.Core/State/InfoQuery.cs:344-430, 1989-2042` | M |
| Unit cards | `UnitCardModel` has no research reference; InfoQuery's unit card links the identity's `realizedBy` research entity | none | `Sim.Ui/ViewModel/UnitCardModel.cs`; `InfoQuery.cs:1300-1322` | M |
| DiscoveryShortcuts | fires on an eligibility TRANSITION of goods.json recipe entities (deposit inputs) or a sector's researched identity listed in UI content (`farming` → `fertility`); no node id in code | `discovery-shortcuts.json` sector name | `Sim.Ui/UiContent/discovery-shortcuts.json:6-12`; `DiscoveryShortcuts.cs:163-188, 195, 259` | M |
| Age panel | research milestones link to the next node toward them (`InfoQuery.NextResearchToward`) | none | `Sim.Ui/Ages/AgePanelModel.cs:121-131` | M |
| Annals | "your scholars mastered <Name>" on completion (UI history file) | none | `Sim.Ui/ViewModel/StateChronicle.cs:66-73` | M |
| Previews / headless gate | hard-coded node ids (rigs) — Appendix A rows for `Sim.Ui/Actions/*`, `Sim.Ui/Headless/PlayabilityGate.cs`; `research.Nodes[research.IndexOfId("root_crop")]` would throw on a retired id | ids | `Sim.Ui/Actions/ActionSurfacePreview.cs:96-102, 127-128`; `R2aPreview.cs:53, 72`; `PlayabilityGate.cs:342` | M / I |
| UI tests | Sim.Ui.Tests reference ids in 17 files (Appendix A); `ProgressionScreenTests` checks counts relative to content (`:81-82`) | — | Appendix A | M |

---

## Q13. Hard-coded node ids and content pins

**13.1 Production code [M].** No `Sim.Core` / `Sim.Cli` code uses a research node id or key literal as a node reference
("no node id in code" holds). Ten literal matches are coincidences with the node id `trade` (system/JSON names, not
node references): `Sim.Core/Kernel/ReplayReport.cs:269`, `Sim.Core/SystemCatalog.cs:227`, `Sim.Core/Systems/Ages/AgeContent.cs:321`,
`Sim.Core/Systems/SimConfig.cs:31`, `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:84`, `Sim.Core/Systems/Trade/TradeSystem.cs:22`,
`Sim.Ui/ViewModel/TurnAuditModel.cs:49`, `Sim.Tests/Conventions/Conv1TermNamespacingTests.cs:69, 74`, `Sim.Tests/Kernel/PipelineLoaderTests.cs:50`.
Node ids/keys used as references in `Sim.Ui` (non-test): `ActionSurfacePreview.cs:96, 102, 127-128`; `R2aPreview.cs:53, 72`
(`:50` is a comment); `PlayabilityGate.cs:342` (`:141` is display text). Content-coupled literals outside `Sim.*`:
`scripts/gen-sample-orders.py:96-97, 110, 121` (keys 12, 17, 2, 8, 52, 74, 53, 2, −1), whose output SHA-256s are pinned in
`ci.yml:359-362`; `scripts/research-content-audit.py:26-28` (Group B ids).

**13.2 Tests.** 339 source lines in 59 files name node ids — 39 `Sim.Tests`, 17 `Sim.Ui.Tests`, 3 `Sim.Ui`
(Appendix A) [M]. Most-referenced: track_road (84),
taxation (37), cereal_cultivation (36), root_crop (23), trade (21), pottery_open_fired (21), stone_dry (18) [M].

**13.3 Tests that pin counts, keys, costs, positions, text or hashes [M]:**

| Pin | Evidence |
|---|---|
| corpus has 424 technologies; node i (i < 424) has the corpus id, key i+1, the corpus name (6 D-047 renames excepted) and `frontier` ⇔ corpus age F | `ResearchContentTests.cs:28-56, 134-142` |
| TechnologyCount 426; `Nodes[424]` = motor_road key 425; `Nodes[425]` = trade key 426 | `ResearchContentTests.cs:38-42` |
| 7 civics, keys 1001+i, ids list | `ResearchContentTests.cs:62-77` |
| branch counts [177, 37, 22, 131, 44, 15]; university type b serves branch b | `ResearchContentTests.cs:84-90` |
| tuning values (0.08, 0.699, anchors, 0.2, 0.4, U 92.4, K 2) | `ResearchContentTests.cs:95-105` |
| oral_rehydration BaseCost 740 | `ResearchContentTests.cs:130` |
| `CorpusSha256` = SHA-256 of `tech-graph-v0.6.json` | `ResearchContentTests.cs:145-149` |
| 81 Eurekas on 80 nodes, 17 evaluable, 0 inherited, railway 0.2+0.2; 15 knowledge-only conditions | `ResearchContentTests.cs:171-183, 207` |
| stage needs legal_code_roman; Chinese-script path reaches the stage; Group B reachability | `ResearchContentTests.cs:233-283` |
| recursive 10, speculative 6, finite 423, finite per subtree [36, 21, 125, 43, 14] | `ResearchContentTests.cs:323-326` |
| activity requirements and names; sector identities; loader-rejection tests that MUTATE the shipped research.json TEXT by exact anchors (indent-sensitive, e.g. five-space `"baseline.primitive_hafted_tools"`) | `ResearchContentTests.cs:345-452`; anchor at `research.json:190` |
| exactly 83 nodes without an era-marked `emerged` text (`Nodes.Count − 83` dated); no AND-prerequisite emerged after its dependent | `ResearchContentTests.cs:486-525` |
| motor_road: key 425, A9, engineering, prereq string, cost 33,450 = round(U·K^8.5) | `Sim.Tests/Systems/RoadDevelopmentTests.cs:1090-1113` |
| track_road: key 74, name "Trackway", description phrases | `RoadDevelopmentTests.cs:1126-1135` |
| trade: prereq string, A3, trunk, cost 880 | `Sim.Tests/State/TradeResearchUnlockTests.cs:114-137` |
| taxation: civic, key 1007, its prerequisite set | `Sim.Tests/State/ResearchUnlockPipelineTests.cs:190-216` |
| fire_making key 1; baseline wording in four descriptions | `Sim.Tests/Systems/ResearchBaselineTests.cs:100-104` |
| allocation-scenario completion turns (track_road 99, stone_dry 116) | `Sim.Tests/Kernel/AllocationCiLegTests.cs:133-139` |
| AI-empire and driven/founded hashes | §7.5 |

---

## Q14. Redesign impact table

Columns: loader rules that bite · code · tests/goldens/docs/CI · schema · UI · old civsnap saves (v32–v34) · old UI sessions.
Unless stated, "code" means no engine change is required (content-driven) [M].

| Change | Loader rules that bite | Code | Tests / goldens / docs / CI | Schema / migration | UI | Old saves v32–v34 (civsnap) | Old UI sessions |
|---|---|---|---|---|---|---|---|
| **Append technology keys** | keys must be > 426 and < 1001 (strictly ascending technologies-then-civics: max 574 more without re-keying civics) and listed after `trade` in file order (`ResearchContent.cs:383-390`); every node rule of Q6 #9-27 (cost ±max(5, 0.1 %), one primary university, non-orphan, reverse-index unlocks, depth); a FINITE subtree node changes `finite_nodes_to_exhaust` of every recursive node of that subtree (6 engineering nodes carry 125) (`:938-940`) | none; civic dense indices shift +k in memory only | `ResearchContentTests.cs:38, 84, 325-326, 513` (new nodes need an era-marked `emerged` or the 83 changes), chronology test; AI-empire pins move if roots/choices change; order-less goldens move only if a new ROOT has an evaluable Eureka or changes zero-knowledge behaviour; regenerate 3 CI docs | none | content-driven; new lanes impossible (branches fixed) | load; new nodes uncompleted [M] | same schema: refused if replay diverges; bumped: replay unchecked [M/I] |
| **Add civics 1008+** | keys > 1007 ascending in `civics`; NO `branch`; may unlock ONLY Institution entities (`:1240-1242`); non-orphan needs a capability string, an institution unlock or a dependent (`:591-600`); ≤ 1 primary university (no cost effect) | none for content; there is NO adoption/government concept, entity kind, table or system (`ResearchContent.cs:21`; one pool/target) | `ResearchContentTests.cs:62-77`; Lenses counts; AI ties lost to every technology [I] | none | Civics lanes by `domain` (`ResearchTreeLayout.cs:125-131`) | load [M] | as above |
| **Split (old key kept for one part)** | new key appended at the END of its tree (no renumbering, `research.json:5`); move references deliberately: entity `requires` + reverse-index unlocks (`:1246-1255`), dependents' prereqs (+ depth cascade), Eureka `when`s (dead/implied re-checks `:620-682`), stage, `researchSets`, ages.json facts (`AgeContent.cs:258-270`), sim.json `taxationRequires` (F1 `SimConfig.cs:1616-1628`) | none; NO content-aware migration hook exists (`Snapshot.cs:55`) — granting the new key to holders of the old would need a new seam | corpus-sync test if the retained part is a corpus node with a changed id/name (`ResearchContentTests.cs:43-56`); counts; the ResearchContentTests text anchors | none for content; a migration would be new code | Partial/Locked states as applicable | load; the old key now means only the retained part; holders of the old key do NOT hold the split-off node ⇒ anything moved to it becomes ineligible — knowledge "decays" against R3 (`KnowledgeTransfer.cs:5-6`) [I]; progress on the old key applies to the retained part's new cost [M]; Eureka indices mis-bind if Eurekas move (`WorldState.cs:904-910`) [I] | recorded orders for the old key now target the retained part (`ReplayProof.cs:107-121`) [M] |
| **Merge** | survivor keeps its key; the absorbed node must be REMOVED (no alias/retired field; unknown members ignored) and every reference rewritten, else "missing or malformed prerequisite" / "invalid reference" / ages "is not in research.json" (`ResearchContent.cs:457-460, 1163-1168`; `AgeContent.cs:266`) | none (no alias table; `IndexOf` is key-only, `ResearchContent.cs:241-245`) | counts/positions; corpus-sync test if a corpus node (< key 425) is removed; AI pins | none | — | load; absorbed-key rows dormant forever (completed ignored, progress never removed, still hashed, copied by revolt) [M]; holders of only the absorbed node lose its unlocks [I] | orders for the absorbed key are ignored [M] |
| **Retire** | remove the node and all references (as merge); if a finite subtree node, update the recursive descriptors' counts; never reuse the key (`research.json:5`) | none | `ResearchContentTests.cs:39-56, 84, 325-326`; Appendix A tests naming it; scenario/fixture keys (Appendix B) | none | UI previews using `IndexOfId(id)` throw (`ActionSurfacePreview.cs:102`) | load; target row cleared next step; progress/completed/Eureka/credit rows dormant forever [M] (7.3) | as merge |
| **Change cost** | only via the six factors (cost must stay U·K^m within tolerance); tier constraints; cap ~757 k RP (Q3) | none | `TradeResearchUnlockTests.cs:136`, `RoadDevelopmentTests.cs:1107-1108`, `ResearchContentTests.cs:130`; allocation-scenario premises if on keys 12/17/2/8/52/74/53 (`AllocationCiLegTests.cs:137-138`, `ci.yml:342`); AI-empire pins; calibration report + 2 other CI docs | none | — | load; progress ≥ new EffectiveCost completes next step with no RP; paid Eureka credit is not revised; ceiling/headroom recomputed on the new BaseCost (negative owed pays 0, `ResearchSystem.cs:228`) [M] | diverging replays refused (same schema) [I] |
| **Change prerequisites** | depth of every descendant; cycles; NOT; trunk-not-behind-subtree; pre-stage reachability; stage deadlock; finite-not-requiring-recursive; dead/implied Eureka checks can newly fail on OTHER nodes (`ResearchContent.cs:474-500, 602-682, 923-926`) | none | chronology (`ResearchContentTests.cs:486-525`), Group B (`:245-283`), generations (`:541-563`), pinned prereq strings (`TradeResearchUnlockTests.cs:124`, `RoadDevelopmentTests.cs:1099-1101`, `ResearchUnlockPipelineTests.cs:208`); AI closures (`AiResearchPolicy.cs:184-195`) | none | lock reasons; tiers move with depth | load; completed nodes stay completed even if new prereqs are unmet (no re-validation) [M]; partial progress on a now-unavailable node is dormant (no RP, no credit, no completion: `ResearchSystem.cs:135-137, 192`) and shows as UI "Partial" (`ResearchNodeViews.cs:84`) [M]; a saved target on it is cleared [M] | refused if divergent [I] |
| **Move trunk ↔ subtree** | `branch` value; trunk dependents of a node moved INTO a subtree must move too (`:491-500`); primary university must serve the new subtree (`:875-876`); recursive descriptor counts in both subtrees; stage atoms must stay outside subtrees (`:609-611`) | none; university factors start/stop applying (`ResearchQuery.cs:316`) | `ResearchContentTests.cs:84, 326`; `RoadDevelopmentTests.cs:1098`; `TradeResearchUnlockTests.cs:135` (`IsTrunk`) | none | lane change (`ResearchTreeLayout.cs:139-141`) | load; partial progress on a node moved into a closed subtree is dormant until the stage; target cleared [M]; moving an Age core node into a subtree ties that Age to the stage and starves the AI core goal (Q11 gap) [I] | refused if divergent [I] |
| **Move between trees** | the tree IS the array a node is listed in (`ResearchContent.cs:375-377`) and keys must ascend through technologies-then-civics ⇒ a technology (≤ 426) cannot be listed among civics and vice versa without changing its KEY (only edge case: the lowest-key civic moved to the end of technologies) [M/I]; civics: no branch, institution-only unlocks; technologies: exactly one primary university | re-keying breaks every save row and recorded order (keys are the identity, `Ids.cs:176-184`) ⇒ in practice retire + new node, or a loader change [I] | as retire + append | none (or a remap migration = new seam) | tab/lenses ("Adopted civics") | as retire [M] | as retire |
| **Rename id** | every expression naming it: prereqs, Eureka `when`, entity `requires`/`unresolved`, stage, `researchSets`, ages.json, sim.json `taxationRequires`; a new id equal to an institution short id (`library`, `archive`, `scribal_school`, `hospital`, `university`, …) is resolved as the NODE first (`ResearchContent.cs:1147-1153`) and then fails the reverse-index rule (`:1246-1255`) | none | corpus-sync test (`ResearchContentTests.cs:43-46`); Appendix A literals; `scripts/research-content-audit.py:26-28` | none | rigs in `Sim.Ui/Actions`, `PlayabilityGate` | unaffected (keys) [M] | unaffected (keys) [M] |
| **Rename name / description** | name non-empty; university-type names drive the relevance `name` check (`:865-868`) | none | names pinned (`ResearchContentTests.cs:48-54, 134-142, 382-384`; `RoadDevelopmentTests.cs:1130-1133`; `ResearchBaselineTests.cs:100-104`) | none | text only; annals | unaffected [M] | unaffected [M] |
| **New entity kind** (e.g. government/policy for macro-civics) | `ResearchEntityKind` enum + `EntityKindNames`/`EntityPrefixes`/`UnlockListNames` (`ResearchContent.cs:21, 293-295`), a new `UnlocksJson` list (`:1511-1522`) and `ValidateUnlocks` list (`:1220-1224`); the civics-only-institution rule (`:1240-1242`); `ValidateProjectsAgainstContent` kind allowlist if buildable (`SimConfig.cs:1443-1448`) | loader + `InfoQuery.KindName`/`Consumers` (`InfoQuery.cs:2159-2167, 1989-2042`), Lenses (`Lenses.cs:9`), any realizing system | `ResearchContentTests` entity lists; `scripts/research-gameplay-unlock-audit.py` per-type mapping + CI check | entities are not persisted — none for the kind itself; adoption/active-government STATE would be new tables appended at ≥ 62 (7.2) [I] | new cards/lens | unaffected [M] | unaffected [I] |
| *Add / reorder / reweight Eurekas* | weight rules, dead/implied checks, class/evaluable consistency; equal-share default changes EVERY weight of the node when N changes (`ResearchContent.cs:562`) | none | `ResearchContentTests.cs:152-208`; an evaluable Eureka on a node available at founding writes rows in order-less runs ⇒ moves FOUNDED/driven/first-reign goldens [I] | none | — | fired flags are by INDEX: append-only edits keep them, reorders mis-bind [M/I] | divergence [I] |
| *Change stage / university gate* | pre-stage satisfiability; no NOT; keep `researchStage` consistent with building.university/inst.university by hand (no validator) | none | `ResearchContentTests.cs:233-252`; `ResearchUnlockPipelineTests` T11–T15 | none | stage text | subtree progress re-activates or goes dormant as the stage flips [I] | divergence [I] |
| *Change subtrees / university types* | five subtree ids hard-coded (`ResearchContent.cs:286, 1059-1079`); exactly two trees (`:15, 1048-1057`); university keys persisted in `InstitutionRow.Type` and `ResearchCostModifierRow.UniversityType` | loader constants; goods.json `founds.universityType`, sim.json `healthType` | many | a removed/re-keyed type makes a loaded save with a modifier row THROW at the first step (7.3) | lanes | load, then crash on step if a modifier row names a missing type [M/I] | replay rebuilds rows under new content [I] |
| *Separate Civics Points pool/target (D-061, PROPOSED)* | — | `ResearchTargetRow` one per polity (`WorldState.cs:872-881`), `ResearchPointPool` (`ResearchQuery.cs:387-394`), throughput (`ResearchSystem.cs:176-187`), `TargetOrder`/`ClearTargetOrder` and OrderKind 6 single TargetId (`ResearchQuery.cs:204-216`; `OrderLog.cs:74-89`), one action descriptor (`AvailableActionsQuery.cs:276-296`), AI single target (`AiResearchPolicy.cs:48-60`), UI single `TargetIndex` (`ResearchNodeViews.cs:60`); ADR-030 §3 forbids reusing the per-turn exception for a second quantity without its own ADR (`adr-030…:35-38`) | `ResearchEngineTests.cs:548` (one pool/one target) superseded; every golden re-pins for the empty count prefix(es) (layout-only precedent `DrivenGoldenTests.cs:443-448`) | new rows appended as sections ≥ 62; version bump; `IsReadable`, `BytesAppendedSince`, `ReplayGuard.ReadableNewestFirst`, `SnapshotDiff.LayoutFor` (`Sim.Cli/SnapshotDiff.cs:113-120, 165`), populated-table tests | second target UI | readable set must include 34 or saves break (D-008) [I] | schema bump ⇒ unchecked replay [M] |

---

## Doc ≠ code (and stale comments)

1. **research.json `_doc` (sectorActivities) "Presentation only: production, yields and shares are unchanged"**
   (`research.json:11`) and `ResearchContent.cs:976` "nothing here changes production" vs farming wild-food yield gated on
   `activity.farming` eligibility (`LabourActivities.cs:107-123`; `ProductionSystem.cs:276-288`; `sim.json:7-9`) [M].
   Same imprecision in `docs/research-gameplay-unlock-audit.md:10` ("production unchanged").
2. `docs/research-gameplay-unlock-audit.md:16` names `RoadDevelopmentQuery.IsClassKnown`; the system enforces through
   `BestKnownClass` (`RoadDevelopmentQuery.cs:155-159`); `IsClassKnown` is used only by `InfoQuery.cs:1280` [M].
3. `ResearchQuery.cs:150-153` "the simulation never calls it, and no system picks targets" — ResearchSystem picks
   city-state targets via `AiResearchPolicy.Cheapest` (`ResearchSystem.cs:110-112`) [M].
4. `ResearchQuery.cs:558-560` "no system consumes this answer yet" — stale (Q10 consumers) [M] (also noted in
   `docs/m6-foundations-audit.md:98`).
5. `research.json:255` stage `_doc` "no institutions system" — InstitutionsSystem exists (`pipeline.json:25`) [M].
6. `research.json:23` RP input "military population weighted below 1" — not implemented (Q1) [M].
7. `ResearchContent.cs:138` "Age relevance — METADATA ONLY: nothing gates on it" — true at runtime, but node `age` tags
   decide whether the config LOADS through the F1 tax check (`SimConfig.cs:1616-1646`) [M].
8. `IntegratedPinAttributionTests.cs:1065` "18 evaluable curated Eurekas" vs 17 (`ResearchContentTests.cs:176`) [M].
9. `UnitCardModel.cs:46` "OrderKind is 1..8" — kinds 9, 11, 12, 13 exist (`OrderLog.cs:148-203`) [M].
10. `Lenses.cs:66` equates "adopted civics" with completed Civics nodes — conflicts with the D-061 direction that
    researched / adopted / active stay distinct (`docs/d061-civics-points-supersession.md:17-27`, PROPOSED) [M].

## Open questions

- [OQ] Should a redesign that splits/merges/retires nodes ship a content-aware save migration (key remap, grant of a
  split-off key)? No hook exists (`Snapshot.cs:55`); adding one touches the frozen kernel (CLAUDE.md governance).
- [OQ] Should the loader refuse NOT in entity requirements (as it does for prerequisites and sim.json requirements)?
- [OQ] Is a schema bump intended for the content change so v34 UI sessions resume (unchecked) instead of being refused?
- [OQ] Scaling: per holder per step ResearchSystem scans the Eureka/credit/exposure/modifier tables once per available
  node (`ResearchSystem.cs:135-174`; `ResearchQuery.cs:404-463`), and `CompletedMask` is O(completed rows) per call; a
  much deeper tree raises both — not measured here (`sim bench` is the house tool).

---

## Appendix A — node-id literals in `Sim*` C# sources (per file: line + ids)

False positives excluded (see 13.1). Ids joined with `/` share a line. 339 lines in 59 files.

| File | Lines | Line → ids |
|---|---|---|
| `Sim.Tests/Kernel/AiEmpireIntegrationTests.cs` | 4 | 210 knapping_oldowan; 211 ground_stone_early; 213 cereal_cultivation; 229 track_road |
| `Sim.Tests/Kernel/AllocationCiLegTests.cs` | 3 | 137 track_road; 138 stone_dry; 139 stone_dry |
| `Sim.Tests/Kernel/AllocationScenario.cs` | 1 | 211 stone_dry/track_road |
| `Sim.Tests/Kernel/IntegratedSaveLoadBatteryTests.cs` | 3 | 127 track_road; 129 cereal_cultivation/pottery_open_fired; 130 arsenical_bronze/law_code/wheel_solid |
| `Sim.Tests/Observability/SplitProbeTests.cs` | 1 | 41 track_road |
| `Sim.Tests/State/AvailableActionsQueryTests.cs` | 8 | 113 root_crop; 128 dog_domestication/sheep_goat; 133 ground_stone_early; 139 ground_stone_early/mining_shaft; 171 grinding_stone; 186 cereal_cultivation/pottery_open_fired; 206 track_road; 214 track_road |
| `Sim.Tests/State/ConstructionFixtureContinuationTests.cs` | 1 | 181 track_road |
| `Sim.Tests/State/GovernanceActionTests.cs` | 6 | 30 taxation; 32 arithmetic_babylonian/coinage_electrum/standard_weights/surveying; 71 taxation; 81 taxation; 90 root_crop; 114 taxation |
| `Sim.Tests/State/InfoQueryTests.cs` | 23 | 97 cereal_cultivation; 98 cereal_cultivation; 100 grinding_stone; 101 cereal_cultivation; 103 grinding_stone; 106 cereal_cultivation/grinding_stone; 109 cereal_cultivation; 114 grinding_stone; 116 cereal_cultivation; 117 cereal_cultivation; 132 root_crop; 137 root_crop; 145 root_crop; 148 root_crop; 155 pressure_flaking; 199 tin_bronze; 203 tin_bronze; 256 tin_bronze; 269 taxation; 313 cereal_cultivation/pottery_open_fired/taxation/tin_bronze/track_road; 424 taxation/tin_bronze/track_road; 496 grinding_stone/root_crop; 502 cereal_cultivation |
| `Sim.Tests/State/ResearchUnlockPipelineTests.cs` | 25 | 103 cereal_cultivation; 123 pottery_open_fired; 126 pottery_open_fired; 145 tin_bronze; 174 taxation; 192 taxation; 200 arithmetic_babylonian/coinage_electrum/standard_weights/surveying; 208 proto_writing/stamp_seal/token_counting; 214 taxation; 239 track_road; 248 cuneiform/geometry_axiomatic/legal_code_roman/medicine_hippocratic/stamp_seal; 253 geometry_axiomatic/medicine_hippocratic; 275 medicine_hippocratic; 276 geometry_axiomatic; 278 medicine_hippocratic; 279 geometry_axiomatic; 280 geometry_axiomatic/medicine_hippocratic; 287 legal_code_roman; 288 cuneiform/stamp_seal; 290 legal_code_roman; 291 legal_code_roman; 302 tin_bronze; 339 pottery_open_fired/taxation/tin_bronze/track_road; 352 taxation; 439 pottery_open_fired |
| `Sim.Tests/State/SetAsideReadingTests.cs` | 2 | 339 track_road; 459 track_road |
| `Sim.Tests/State/TradeResearchUnlockTests.cs` | 14 | 87 trade; 102 trade; 116 trade; 124 sail_square/token_counting; 125 token_counting; 126 donkey; 127 camel/donkey/sail_square; 128 token_counting; 130 donkey/token_counting; 131 trade; 161 trade; 174 trade; 188 donkey/token_counting; 212 trade |
| `Sim.Tests/State/TurnOneActionSurfaceTests.cs` | 5 | 160 root_crop; 169 root_crop; 214 root_crop; 223 root_crop/track_road; 250 dog_domestication/sheep_goat |
| `Sim.Tests/Systems/AgeProgressionTests.cs` | 7 | 64 cereal_cultivation/pottery_open_fired; 221 pottery_open_fired/sheep_goat; 235 cereal_cultivation/pottery_open_fired/sheep_goat; 248 cereal_cultivation/sheep_goat; 260 bow_simple/hafting/sling; 296 cereal_cultivation; 504 bow_simple/hafting |
| `Sim.Tests/Systems/AiAgeAdvancementIntegrationTests.cs` | 1 | 116 cereal_cultivation/pottery_open_fired |
| `Sim.Tests/Systems/AiPolicyTests.cs` | 5 | 107 cereal_cultivation/grinding_stone; 119 knapping_oldowan; 128 grinding_stone; 141 cereal_cultivation; 229 track_road |
| `Sim.Tests/Systems/AiResearchGoalTests.cs` | 10 | 73 track_road; 74 track_road; 77 stone_dry/track_road; 80 taxation; 81 arithmetic_babylonian; 82 taxation; 83 taxation; 95 algebra/geometry_axiomatic/legal_code_roman/medicine_hippocratic; 96 chinese_script/cuneiform/hieroglyphic/stamp_seal; 148 grinding_stone |
| `Sim.Tests/Systems/CapabilityPredicateTests.cs` | 3 | 93 cereal_cultivation; 94 grinding_stone; 119 cereal_cultivation/pottery_open_fired |
| `Sim.Tests/Systems/ConstructionAvailabilityTests.cs` | 3 | 105 kiln_updraft; 110 kiln_updraft; 219 tin_bronze |
| `Sim.Tests/Systems/ConstructionLifecyclePersistenceTests.cs` | 1 | 653 track_road |
| `Sim.Tests/Systems/ConstructionReservationTests.cs` | 4 | 751 track_road; 1367 track_road; 1882 track_road; 2079 track_road |
| `Sim.Tests/Systems/ContinueWithinTurnTests.cs` | 1 | 399 track_road |
| `Sim.Tests/Systems/EmpireConstructionStockpileTests.cs` | 1 | 370 track_road |
| `Sim.Tests/Systems/FoodSectorLabelTests.cs` | 4 | 29 root_crop; 30 root_crop; 38 cereal_cultivation; 39 cereal_cultivation |
| `Sim.Tests/Systems/GovernanceTests.cs` | 17 | 188 taxation; 191 taxation; 201 arithmetic_babylonian; 202 surveying; 203 standard_weights; 204 coinage_electrum; 213 arithmetic_babylonian/coinage_electrum/standard_weights/surveying; 217 taxation; 234 cereal_cultivation; 235 numeral_sexagesimal; 238 taxation; 246 cereal_cultivation; 248 taxation; 250 cereal_cultivation; 764 taxation; 786 arithmetic_babylonian; 796 arithmetic_babylonian |
| `Sim.Tests/Systems/GovernanceTimingTests.cs` | 1 | 265 track_road |
| `Sim.Tests/Systems/GranaryEffectsTests.cs` | 1 | 335 root_crop |
| `Sim.Tests/Systems/IncrementalConstructionIntegrationTests.cs` | 1 | 351 root_crop |
| `Sim.Tests/Systems/IncrementalConstructionTests.cs` | 1 | 543 track_road |
| `Sim.Tests/Systems/InstitutionLifecycleTests.cs` | 1 | 51 cuneiform/legal_code_roman/stamp_seal |
| `Sim.Tests/Systems/ResearchBaselineTests.cs` | 5 | 100 fire_making; 101 fire_making; 102 fishing_hook; 103 raft; 104 hafting |
| `Sim.Tests/Systems/ResearchContentTests.cs` | 47 | 39 motor_road; 41 trade; 76 census/coined_wage/joint_stock/law_code/legal_code_roman/patent/taxation; 130 oral_rehydration; 136 fire_making; 137 adhesive_natural; 138 hafting; 139 fishing_hook; 140 raft; 141 dugout; 182 railway; 240 legal_code_roman; 249 cuneiform/hieroglyphic; 250 chinese_script; 255 law_code; 256 census; 257 geometry_practical; 258 astronomy_records; 259 abacus; 262 cuneiform; 266 numeral_sexagesimal; 267 arithmetic_babylonian; 270 cuneiform; 272 cuneiform; 293 printing_press; 296 printing_press; 330 frontier_launch; 334 jet_airliner; 337 jet_airliner; 355 cereal_cultivation/rice_wet/root_crop/sorghum_pearl_millet; 356 cereal_cultivation/maize/millet/rice_wet/root_crop/sorghum_pearl_millet; 357 cattle/sheep_goat; 358 ground_stone_early; 359 mining_shaft; 380 fishing_hook; 457 sail_square; 458 caravel/carrack/polynesian_canoe; 460 sail_square; 462 macadam/road_paved/track_road; 516 vacuum_tube/xray; 517 canning/germ_theory; 534 antibiotic_penicillin; 535 germ_theory; 561 bill_of_exchange; 562 double_entry; 568 arsenical_bronze; 583 copper_smelting |
| `Sim.Tests/Systems/ResourceAllocationTests.cs` | 3 | 46 track_road; 369 track_road; 499 track_road |
| `Sim.Tests/Systems/RoadDevelopmentTests.cs` | 59 | 161 track_road; 162 stone_dry; 163 road_paved; 164 macadam; 165 motor_road; 179 macadam/motor_road/road_paved/stone_dry/track_road; 204 track_road; 217 macadam/motor_road/road_paved/stone_dry; 261 macadam/motor_road/road_paved/stone_dry/track_road; 277 macadam/motor_road/road_paved/stone_dry/track_road; 300 track_road; 307 track_road; 318 track_road; 333 macadam/motor_road/road_paved/stone_dry/track_road; 342 stone_dry/track_road; 450 track_road; 469 track_road; 483 track_road; 485 track_road; 488 track_road; 499 track_road; 519 track_road; 535 track_road; 549 track_road; 588 stone_dry/track_road; 609 track_road; 611 stone_dry; 613 road_paved; 615 macadam; 618 motor_road; 627 track_road; 641 track_road; 666 stone_dry/track_road; 689 track_road; 690 track_road; 712 track_road; 714 track_road; 717 track_road; 727 track_road; 737 stone_dry; 847 track_road; 861 track_road; 869 track_road; 925 macadam/motor_road/road_paved/stone_dry/track_road; 969 track_road; 995 track_road; 1020 track_road; 1044 track_road; 1072 track_road; 1092 motor_road; 1099 automobile_mass/petroleum_refining/reinforced_concrete; 1101 automobile_mass/petroleum_refining/reinforced_concrete; 1103 motor_road; 1109 reinforced_concrete; 1120 carrack; 1121 carrack; 1122 canal_lock; 1128 track_road; 1135 stone_dry/track_road |
| `Sim.Tests/Systems/TaxAgeGateTests.cs` | 2 | 37 taxation; 135 arsenical_bronze/law_code/wheel_solid |
| `Sim.Tests/Systems/UnitModernizationTests.cs` | 2 | 197 arsenical_bronze/law_code/proto_writing/wheel_solid; 275 arsenical_bronze/cereal_cultivation/law_code/pottery_open_fired/proto_writing/wheel_solid |
| `Sim.Tests/TestUtil/GovernanceRigs.cs` | 1 | 16 taxation |
| `Sim.Tests/TestUtil/TestConfigs.cs` | 1 | 273 trade (removes the node to build the pre-R2a twin) |
| `Sim.Tests/TestUtil/UniversityRigs.cs` | 1 | 21 cuneiform/geometry_axiomatic/legal_code_roman/medicine_hippocratic/stamp_seal |
| `Sim.Ui.Tests/ActionEmitterGuardTests.cs` | 5 | 179 track_road; 202 cordage; 203 cordage; 210 cordage; 226 taxation |
| `Sim.Ui.Tests/ActionSurfaceAgeAndTaxTests.cs` | 1 | 73 taxation |
| `Sim.Ui.Tests/ActionSurfaceTests.cs` | 7 | 61 taxation; 222 root_crop; 266 taxation; 292 track_road; 324 root_crop; 440 taxation; 572 cordage |
| `Sim.Ui.Tests/AgeAndWorldUiTests.cs` | 1 | 248 cereal_cultivation/pottery_open_fired |
| `Sim.Ui.Tests/ConstructionHoldsUiTests.cs` | 2 | 49 track_road; 453 track_road |
| `Sim.Ui.Tests/GranaryCardTests.cs` | 1 | 36 root_crop |
| `Sim.Ui.Tests/InfoPanelTests.cs` | 11 | 125 root_crop; 129 grinding_stone; 221 cereal_cultivation; 232 cereal_cultivation; 387 root_crop; 404 root_crop; 487 taxation; 494 taxation; 574 grinding_stone; 607 cereal_cultivation; 610 pressure_flaking |
| `Sim.Ui.Tests/PlayerViewsTests.cs` | 1 | 131 taxation |
| `Sim.Ui.Tests/PreviewToolProcessTests.cs` | 1 | 286 arithmetic_babylonian |
| `Sim.Ui.Tests/ProgressionScreenTests.cs` | 2 | 71 law_code; 259 cordage |
| `Sim.Ui.Tests/ReadabilityTests.cs` | 1 | 456 taxation |
| `Sim.Ui.Tests/ResearchOrderFactoryTests.cs` | 4 | 23 cordage; 31 general_ai; 50 general_ai; 53 cordage |
| `Sim.Ui.Tests/ResearchUnlockPipelineUiTests.cs` | 6 | 139 medicine_hippocratic/pottery_open_fired/tin_bronze; 140 cuneiform/geometry_axiomatic/legal_code_roman/stamp_seal; 160 pottery_open_fired; 161 pottery_open_fired; 162 pottery_open_fired; 188 trade |
| `Sim.Ui.Tests/ResourceAllocationUiTests.cs` | 2 | 43 track_road; 729 track_road |
| `Sim.Ui.Tests/SessionRecordTests.cs` | 1 | 24 cordage |
| `Sim.Ui.Tests/TaxControlTests.cs` | 1 | 34 taxation |
| `Sim.Ui.Tests/WorldMapStateTests.cs` | 4 | 114 cereal_cultivation/pottery_open_fired/track_road; 127 stone_dry; 128 road_paved; 129 motor_road |
| `Sim.Ui/Actions/ActionSurfacePreview.cs` | 4 | 96 track_road; 102 root_crop; 127 cereal_cultivation/pottery_open_fired/tin_bronze; 128 cuneiform/geometry_axiomatic/legal_code_roman/medicine_hippocratic/stamp_seal/track_road |
| `Sim.Ui/Actions/R2aPreview.cs` | 3 | 50 taxation (comment); 53 cereal_cultivation/donkey/pottery_open_fired/tin_bronze/token_counting/track_road; 72 trade |
| `Sim.Ui/Headless/PlayabilityGate.cs` | 2 | 141 track_road (display text); 342 taxation/track_road |

## Appendix B — numeric node keys in tests, scripts and fixtures [M]

| Where | Keys | Meaning / content-dependence |
|---|---|---|
| `Sim.Tests/Kernel/AllocationScenario.cs:91-94`; `scripts/gen-sample-orders.py:96-97`; `Sim.Tests/Fixtures/split-report/orders*.bin` (decoded) | 12 cordage, 17 shelter_hut, 2 knapping_oldowan, 8 ground_stone_early, 52 timber_frame, 74 track_road, 53 stone_dry | scenario chain; completion turns pinned (Q7.5) |
| `scripts/gen-sample-orders.py:110, 121` (`--founded-mix`, CI ai/mix leg) | 2; −1 at turn 150 | CI legs compare processes/replays, no pinned hash for these |
| `Sim.Tests/Fixtures/v33-session-q17/orders-f1cc70bce302a9a0.bin` (decoded); `ConstructionHoldsV33FixtureTests.cs:133` | (turn 0, AI polity 2, kind 6, key 2) | replayed against the recorded trace (premise (i)) |
| `first-reign-orders.bin`, `t38-director-orders.bin`, `v32-fixture-orders.bin`, `v33-fixture-orders.bin`, `v32-session-found/orders-*.bin` (decoded) | no kind-6 order | research-free runs |
| `Sim.Tests/State/CityStateProgressionTests.cs:187-192` | 426, 1, 76, 81 | local-holder rows round-trip (layout) |
| `Sim.Tests/State/ResearchSchemaTests.cs:20-37, 71, 85, 108, 110, 135` | arbitrary (1004, 17, 301, 2, 4, 9, 1, 1001, 11, 18, 3, 5) | layout/round-trip only (rows are not validated against content) |
| `Sim.Tests/Kernel/ResearchOrderTests.cs:28` | 1001 | order encoding |
| `Sim.Tests/Kernel/OrderDeliveryValidationTests.cs:51, 79, 112, 150`; `RevoltPolityOrderValidationTests.cs:44, 59, 98`; `MixLegConstructionPremiseTests.cs:60`; `Sim.Ui.Tests/UiSessionReplayTests.cs:100, 106` | 2 | actor-validation placeholders (meaning not asserted) [I] |
| `Sim.Tests/Kernel/IdentityDriftGuardTests.cs:123, 126, 137, 147` | 17 | identity-proof placeholders [I] |
| `ResearchContentTests.cs:40, 42, 72`; `RoadDevelopmentTests.cs:1095, 1129`; `ResearchUnlockPipelineTests.cs:195`; `ResearchBaselineTests.cs:100` | 425, 426, 1001+i, 425, 74, 1007, 1 | content key pins |
| `Sim.Tests/Systems/AiPolicyTests.cs:79-95`, `KnowledgeMonotonicTests.cs:179` | 1–6 | synthetic rig content / table-only (content-independent) |

## Appendix C — content links into research.json from other files [M]

| Source | Link | Evidence |
|---|---|---|
| ages.json research facts | A2 core cereal_cultivation(29); supp pottery_open_fired(47), sheep_goat(41) · A3 core arsenical_bronze(80) \| tin_bronze(81); supp proto_writing(86) \| token_counting(76), law_code(1001, civic), wheel_solid(72) · A4 core iron_bloomery(110); supp abjad(117), census(1003, civic) · A5 core legal_code_roman(1002, civic), coinage_electrum(121); supp geometry_axiomatic(140), road_paved(150), concrete_pozzolan(148), coined_wage(1004, civic) · A6 core heavy_plough(175, agriculture); supp paper(159), water_mill(145) \| windmill_post(181, eng.), double_entry(213, nat.sci.) \| bill_of_exchange(214, eng.), hindu_arabic(203) · A7 core printing_press(215, eng.); supp caravel(217, mil.), corned_powder(223, nat.sci.), mechanics_newtonian(234, nat.sci.), patent(1005, civic), joint_stock(1006, civic) · A8 core steam_rotary(250, eng.); supp puddling(252), railway(268), telegraph(284) (eng.), germ_theory(295, med.), research_count technology ≥ 200 · A9 core electricity_generation(288), internal_combustion(291) (eng.); supp radio(311), computer(315) (eng.), antibiotic_penicillin(324, med.), research_count technology ≥ 280 | `ages.json:27-168` (keys/branches from research.json) |
| sim.json | `trade.entity` activity.trade; road classes infra.road_track/built/paved/macadam/highway; `taxationRequires: taxation`; `taxationMinAge: 3`; `healthType: medical_university` | `sim.json:230, 256-261, 268-270, 292` |
| goods.json | recipes → recipe.pottery_firing / weaving / bronze_casting / toolmaking; projects → building.granary, building.workshop, building.university (×5) + founds inst.university with the five university-type ids | `goods.json:105-305` |
| unit-families.json | 23 identities `realizedBy` `unit.*` entities | `Sim.Data/content/unit-families.json`; `UnitFamilyContent.cs:220-221` |
| research.json sectorActivities | activity.farming (replaces, farming), activity.herding (replaces, herding_fishing), activity.logging + activity.mining (joins, extraction) | `research.json:129-208`; `ResearchContentTests.cs:393-428` |
| UI content | `discovery-shortcuts.json` sector `farming` → layer `fertility` | `Sim.Ui/UiContent/discovery-shortcuts.json:6-12` |
| research stage / universities | stage atoms medicine_hippocratic(136), geometry_axiomatic(140), algebra(204), stamp_seal(77), legal_code_roman(1002); building.university and inst.university requirements | `research.json:253-256, 28634` |
