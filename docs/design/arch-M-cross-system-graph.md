# ARCH-M — THE CROSS-SYSTEM DEPENDENCY GRAPH (mandate Part 18)

**DESIGN, NOT IMPLEMENTATION.** This document draws a graph. It implements nothing, changes no
schema, writes no M5 code, merges nothing, and edits no existing document. Created under the
Director's 2026-09-19 mandate as OUTPUT M of the synthesis lane.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `26d12d3`, working tree
clean; `origin/main` = `dbef61a`. Every `file:line` below was read on that tree. Where a cited
source names a line that has since moved, the drift is recorded rather than silently corrected
(GOV-4 §1: the tree wins on facts, the ruling stands on rulings).

**Author authority: none.** The DIRECTOR IS ChatGPT. Where two sources conflict, both are named
and neither is chosen. Nothing here is a ruling, and nothing here upgrades a status.

**Label key** (mandate rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this phase's
design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.

**Vocabulary.** This document uses the repository's own terms: *the Spine*, *D-020 predicate DSL*,
*published variables*, *D-021 valves* (Exit · Voice · Endurance), *carrier* (the D-035-C carrier
test), *corridor*, *quarantine*, *latch*, *packet*, *law 1..7*, *CR-NNN / ADR-NNN / D-NNN*. New
terms are marked PROPOSED and defined once.

---

## §0 WHAT THIS DOCUMENT IS, AND WHAT IT DELIBERATELY DOES NOT DO

**It is** an explicit dependency graph over the twenty-one domains the mandate names, plus the
domains the shipped code actually has. Every edge states its relation kind, its D-035-C carrier
(or records the carrier as OWED), and whether it **EXISTS TODAY** (with the shipping system and
file) or is **PROPOSED** by one of the six sibling architecture lanes.

**It deliberately does not**: rank the edges, schedule them, choose between two lanes that
disagree, design a table, name a row type, or assert that a PROPOSED edge should be built. It
also does not re-derive the sibling lanes' designs — it places them in one graph and then does the
one thing none of them could do alone: **hunt the graph for cycles across lane boundaries.**

**The single obligation the mandate puts on this lane** is not to introduce a circular dependency
by accident. §3 states the rule that makes a cycle legal or illegal, and §7 executes the hunt and
reports what it found, including the cycles it could **not** discharge.

---

## §1 GROUND — THE FACTS THE GRAPH STANDS ON, EACH VERIFIED THIS PASS

| # | fact | source, verified on `26d12d3` |
|---|---|---|
| **F1** | **State is plain data tables, each owned by exactly one system, and read/write is enforced by construction.** *"The kernel constructs each system's `SimContext` with typed writable handles to its owned tables only. **An agent cannot silently write another system's state — the reference does not exist.**"* | `docs/m0-kernel-spec.md:54-64` (§3.1, FROZEN) — **RATIFIED** |
| **F2** | **Double buffering; the one-turn lag is the DEFAULT coupling.** *"At turn start the kernel clones `Prev → Next` … Systems read `Prev`, write `Next`. One-turn lag is therefore the default; deliberate same-turn edges (later milestones) are explicit kernel-ordered handoffs listed in the interaction matrix."* | `docs/m0-kernel-spec.md:66` (§3.2, FROZEN) — **RATIFIED** |
| **F3** | **Law 6, isolation.** *"systems never reference each other — only `State` and `Kernel`. Communication is through tables and events."* Reaffirmed as a director ruling: *"Gameplay interdependence is allowed; direct code coupling is not … Economy may depend *conceptually* on knowledge, government, military, transport and institutions — **that never justifies a sibling call.**"* | `CLAUDE.md:21`; `docs/d042-empire-and-player-control-addendum.md:135-140` (§7.1–7.2) — **RATIFIED** |
| **F4** | **The turn pipeline is a fixed ordered list loaded as data.** The shipped order is `catchment · harvestweather · disaster · production · appropriation · consumption · price · trade · housing · construction · classmobility · migration · colonization · revolt · demographics · needsgrievance · pathbuild`. | `Sim.Data/content/pipeline.json:2-20` — **MEASURED** (this lane, `26d12d3`); contract at `docs/m0-kernel-spec.md:68` |
| **F5** | **`GoodStocks` and `Buckets` are SANCTIONED SHARED WRITABLE tables, with a FIELD-LEVEL ownership split recorded in one place.** *"SANCTIONED SHARED STOCK … `GoodStocks` is handed to BOTH Production and Consumption. A stock that one system fills and another drains cannot have a single writer; both mutations go exclusively through the Ledger (law 1) … This paragraph is the reviewable record of that share, so it states the split at FIELD level."* Seven holders were counted this pass: Production, Consumption, Trade, Housing, Construction, Appropriation, Colonization. | `Sim.Core/SystemCatalog.cs:31-60`, `:168`, `:179`, `:191`, `:230`, `:247` — **MEASURED** (this lane) |
| **F6** | **There IS at least one deliberate same-turn edge shipping today, and it is documented at its site.** *"Running AFTER Production is deliberate: the clamp applies to the post-harvest store — this turn's harvest is eaten this turn. **Reading its own shared Next stock is lawful** (owned-table access); everything else reads Prev."* | `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:46-48` — **MEASURED** (this lane) |
| **F7** | **The artefact F2 says records same-turn edges — the "interaction matrix" — was not located on this tree.** The frozen Spine references it twice (*"the interaction matrix marks the few deliberate same-turn edges"*; *"relevant interaction-matrix row"* as a packet input). | `docs/civ-sim-architecture-v3-outline.md:32`, `:117`; absence recorded independently as **G-06** in `docs/design/recovered-decisions-architecture-invariants.md` — **MEASURED** |
| **F8** | **The D-035-C CARRIER TEST.** *"**Name the physical carrier — a good, a purse, a building, a policy, a body, a season.** If none exists, it is an invented modifier and is **refused**."* And: *"the seven paths are not a taxonomy to be extended by analogy; a coupling that does not fit one of them has not found an eighth path, it has failed the carrier test."* | `docs/d035-needs-aggregation.md:91-98` — **RATIFIED** |
| **F9** | **D-021, the paired-feedback rule, project-wide.** *"**Every positive feedback loop in the design must ship with at least one negative feedback loop that *strengthens with amplitude*.** … **This rule is project-wide:** it binds unrest, markets (price rises pull supply), epidemics (susceptibles deplete), and war (exhaustion) equally."* The valves are **Exit, Voice, Endurance**, with at least one channel always open. | `docs/d021-stability-doctrine.md:8`, `:24-34` — **RATIFIED** |
| **F10** | **Law 2, mechanisms over modifiers.** *"coefficients inside resolution equations are fine; free-floating permanent buffs are banned."* Spine form: *"Coefficients *inside* a resolution equation … are legal. Free-floating permanent auras ('+10% happiness') are illegal."* | `CLAUDE.md:17`; `docs/civ-sim-architecture-v3-outline.md:20` — **RATIFIED** |
| **F11** | **Law 4, no calendar gates.** *"capability derives from computed state, never from dates or era labels."* | `CLAUDE.md:19`; `docs/civ-sim-architecture-v3-outline.md:22` — **RATIFIED** |
| **F12** | **The published-variable registry ships FOUR variables, and ONE system publishes all four.** `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`, all upserted by `ClassMobilitySystem` into its own `Variables` table. Predicates read them at **PREV** (one-turn lag). | `Sim.Core/State/Variables.cs:93`; `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:144-165`; lag at `Sim.Core/Systems/ClassMobility/Predicate.cs:22-25` — **MEASURED** (this lane) |
| **F13** | **The two live consumers of the D-020 seam.** Class emergence (a serialized hysteresis **latch**) and recipe availability (`requires`, **pure derived**, no state). *"a knowledge gate over published variables, never a calendar date (law 4)"*. | `Sim.Data/content/goods.json:3`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396`; `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33` — **RATIFIED** (shipped contracts) |
| **F14** | **A universal `CapabilitySystem` is REJECTED, and the reason is law 6.** *"**Do not create a universal God system such as a `CapabilitySystem`** that owns every capability or coordinates every domain."* The permitted disposition: *"conformant **only** as a *shared predicate grammar consumed independently by each domain system* … and **not** as a coordinating owner."* | `docs/d042-empire-and-player-control-addendum.md:141-142` (§7.3), `:280-282` (§14.5) — **RATIFIED / REJECTED** |
| **F15** | **The observability taxonomy is FIVE kinds plus GAP** — READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED, *"Nothing else is permitted … it is a **GAP** … rather than an observer-side copy of the formula that will drift."* **"DERIVED" is not one of the five**; it is the tree's separate word for a *reading* (`SettlementHappiness`). | `docs/observability-architecture.md:19-32` — **MEASURED** (this lane). See **X-M6**. |
| **F16** | **`SettlementHappiness` is a DERIVED READING, never a stock**, not serialized, and is forbidden by an enforced CI gate from reading the needs/grievance tables. | `Sim.Core/State/SettlementHappiness.cs:6-17`; the gate `scripts/check-read-isolation.sh:1-22`; standing fence `docs/m4-exit-inventory.md:310-312` — **RATIFIED** |
| **F17** | **ADR-008: static terrain is immutable after worldgen, excluded from the per-turn `Clone()`, and its content hash is folded into the canonical stream.** | `docs/adr/adr-008-static-terrain.md:8-15` — **RATIFIED** |
| **F18** | **The turn is atomic and 10 sim-years at the canonical dt, with 0.5-year demographic micro-steps; dt falls to 0.5 across the era table.** dt authority: *"global dt is set by the world's *most advanced* polity's era band."* | `docs/civ-sim-architecture-v3-outline.md:34`; `docs/m4-exit-inventory.md:321-322` (the M5 startup fence: *"the 10-year atomic turn stays"*) — **RATIFIED** |

---

## §2 THE RELATION VOCABULARY — THE MANDATE'S FIVE KINDS, DEFINED AGAINST THIS CODEBASE

The mandate requires every edge to state which relation it is: **READ · WRITE · DERIVE · INFLUENCE
· CONTROL**. Those words are not in the repository's vocabulary as a set, so this section defines
each against a shipped mechanism rather than importing a generic meaning. **The definitions are
PROPOSED; the mechanisms they are defined against are RATIFIED or MEASURED.**

| relation | PROPOSED definition, in this project's terms | the shipped mechanism it names | legality constraint |
|---|---|---|---|
| **READ** | System A reads a table system B owns, from `Prev` — one turn stale. | `prev.<Table>` inside a system's `Step`. Every row of §5 that is not marked SAME-TURN is this. | Always legal. It is the **default** coupling (F2) and the only one that needs no sanction. |
| **WRITE** | System A mutates a table. | `ctx.Owned.<Table>`. | **A cross-system WRITE does not exist by construction** — *"the reference does not exist"* (F1). The one exception is a **SANCTIONED SHARED TABLE** (F5), which is a field-level split recorded in `SystemCatalog.cs`, not a second writer on one field. |
| **DERIVE** | A quantity recomputed from state every time it is asked, never stored and never serialized. | `SettlementHappiness.Of` (F16); the recipe `requires` predicate (F13); `TravelCostRow`. | Legal and preferred. *"Nothing new is serialized merely to be observable"* (`docs/observability-architecture.md:331-337`). A DERIVE edge adds **no schema**. |
| **INFLUENCE** | A quantity read from another system enters a **resolution equation as a coefficient**. | `foodMultiplier` inside the harvest rate (`ProductionSystem.cs:239`, `:306`); `toolFactor` on the labour side of the harvest `min()`. | Legal **only** inside an equation (F10). The banned shape is the same quantity attached to an owner as a standing buff. |
| **CONTROL** | A player or AI **order**, or a **persistent directive**, steers a system's behaviour through the order pipeline. | `LaborAllocationOrder` → `PathBuildSystem`'s `SectorAllocations` upsert (`PathBuildSystem.cs:17-24`). | Legal only through the common pipeline: *"Player and AI use the same downstream pathway … Orders / Directives → Validation → Simulation Systems"* (`docs/d042-…:110-128`). UI code may never mutate state (`:108-109`). |

**INFERRED, and it is the most load-bearing observation in this document.** In this codebase the
five relations are **not five mechanisms**. Four of them ride on one: a `Prev` READ of a table
another system owns. DERIVE, INFLUENCE and CONTROL describe *what the reading system does with the
number*, not a different wire. The only genuinely distinct mechanisms are (a) the `Prev` read,
(b) the **sanctioned shared table** with its field-level split, and (c) the **order batch**.

Two consequences follow, and both matter for §7:

1. **The dependency graph is acyclic in TIME by construction**, because every ordinary edge is a
   one-turn lag. A cycle in the *influence* graph is therefore never a deadlock, an evaluation
   loop or an ordering problem. It is a **dynamical** object: a feedback loop. Which is exactly
   what D-021 governs (F9). **INFERRED.**
2. **The exceptions are the place to look.** Same-turn edges through a sanctioned shared table
   (F5, F6) are *not* broken by the lag; they are broken only by the pipeline order, which is
   data (F4). And the artefact that is supposed to enumerate them does not exist (F7).

---

## §3 THE CYCLE RULE — STATED EXPLICITLY, AS THE MANDATE REQUIRES

> **PROPOSED — THE CYCLE RULE.**
>
> A cycle in the influence graph is **LEGAL** if and only if at least one of the following holds,
> and the packet that closes the cycle **says which**:
>
> **(1) THE PREV BREAK.** Every edge in the cycle is an ordinary `Prev` read, so the cycle is
> broken in time by the one-turn lag (F2). This is the shipped pattern and it discharges the
> *topological* obligation completely — but it discharges **nothing else**. A prev-broken cycle
> can still run away.
>
> **(2) THE D-021 BRAKE.** The cycle carries an explicitly **named** negative feedback that
> **strengthens with amplitude**, landing in the **same milestone** as the positive loop (F9).
> This is the obligation that (1) does not discharge, and it is the one that actually binds.
>
> A cycle that has neither is **FLAGGED** — recorded as an unresolved cycle and escalated, never
> quietly absorbed.
>
> **A cycle containing a SAME-TURN edge (F5/F6) is not covered by (1) at all.** It is broken only
> by the pipeline order in `pipeline.json`, which is data and may be reordered, so such a cycle
> must name the ordering it depends on and why that ordering is correct — the form
> `ConsumptionSystem.cs:46-48` already uses.

**Why the rule is stated in two independent clauses rather than one.** MEASURED, from the tree's
own history: the shipped graph is already saturated with prev-broken cycles (§7) and the project
has never had a topological problem. What it has had is CR-003's compensating-error corridor, the
M2 resurrection cycle (ADR-012), and the starvation-magnetism loop — every one of them a
dynamical failure inside a perfectly legal prev-broken cycle. **INFERRED:** clause (1) is cheap
and nearly always satisfied; clause (2) is the expensive one and is where review attention
belongs.

---

## §4 THE NODE ROSTER

The mandate names twenty-one domains. Each is placed below against what the tree actually has.
**MEASURED** unless marked otherwise. "Shipping system" names the file; "no substrate" means this
lane searched and found none.

| # | domain (mandate's name) | status on `26d12d3` | the shipped system, or the lane that proposes it |
|---|---|---|---|
| 1 | **Population** | **SHIPS** | `DemographicsSystem` (cohorts, 0.5-y micro-steps, ADR-011 exponential survival); `BucketRow` is a SANCTIONED SHARED table (F5) |
| 2 | **Agriculture** | **SHIPS** | `ProductionSystem` farming sector — `harvest = min(land, labour × toolFactor)` (`ProductionSystem.cs:227-233`) |
| 3 | **Food** | **SHIPS** | `ConsumptionSystem` (deficit ratio, spoilage, granary cap); `FoodState` cause qualifier (ADR-024) |
| 4 | **Water** | **PARTIAL** | a **cost coefficient** only: `transport.riverCostFactor` inside `TraversalLattice.Build`, and static `_moisture` read once at founding. **No water quantity, no irrigation** (arch-I gap 10; arch-E domain 2) |
| 5 | **Climate** | **NO SUBSTRATE** | proposed by **arch-I §3**. The tree has a static temperature/moisture field and a stochastic per-settlement AR(1) weather multiplier, *"and nothing between them"* (`docs/design/recovered-decisions-climate-env-agri.md` gap 1) |
| 6 | **Environment** | **NO SUBSTRATE** | proposed by **arch-I §7**. Spine places *"Environment & climate"* at M9 with *"degradation stocks close loops"* (`docs/civ-sim-architecture-v3-outline.md:89`) |
| 7 | **Pollution** | **NO SUBSTRATE** | proposed by **arch-I §7**. MEASURED by the recovery lane: `grep -i pollution` over `docs/`, `Sim.Core/`, `Sim.Data/` returns **zero** hits |
| 8 | **Technology** | **NO OBJECT** | proposed by **arch-D**. D-042 §8.2/§9.1 rule technology distinct from knowledge and capability, and **nothing in the tree says what a technology IS** (recovery-lane G-14) |
| 9 | **Knowledge** | **NO SUBSTRATE** | proposed by **arch-C** (CORPUS / HOLDING / PRACTICE) and **arch-E** (arrival hazard, domain lattices) |
| 10 | **Institutions** | **NO CODE, ≥6 MEANINGS** | proposed by **arch-FGH Part 14**. *"`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or `Sim.Tests/`"* — one doc-comment hit only (`WorldState.cs:696`). **CR-010 recommended, never written** |
| 11 | **Civics** | **PARTIAL** | `PolityRow` (identity + `CommandSource`), `ControlRow`, `CapitalRow`, `RecognitionRow`, `ClaimRow`, `RevoltSystem`. Proposed as a layer by **arch-FGH Part 13** |
| 12 | **Government** | **NO SUBSTRATE** | proposed by **arch-FGH §2.1–2.3**. The tree rules what governments may **not** be and never names one (recovery Q-08) |
| 13 | **Legitimacy** | **NO SUBSTRATE** | named as an input in four ratified places, defined in none (recovery Q-03). Spine: M5, *"mechanism-only, no mood auras"* (`:83`) |
| 14 | **Grievance** | **SHIPS, QUARANTINED** | `NeedsGrievanceSystem` accrues; `scripts/check-read-isolation.sh` fails CI on any sim-side read outside its allowlist. *"grievance drives no BEHAVIOUR"* until M5 |
| 15 | **Migration** | **SHIPS** | `MigrationSystem`, bounded by ADR-025 (`ω`, the Exit valve's openness); `SmoothedAttractivenessRow` |
| 16 | **Disasters** | **SHIPS, INERT** | `DisasterSystem`, one kind (crop failure); `hazardPerYear = 0.0` (`sim.json:235`). **CR-016 OPEN** |
| 17 | **Resilience** | **PARTIAL** | granary cap + spoilage + `B_eff`; proposed as an architecture by **arch-JKL Part K** |
| 18 | **Trade** | **SHIPS** | `TradeSystem` + `TradeArbitrageSystem`; `TradeFlowRow(From, To, Good, Quantity)` — **goods move, people do not** |
| 19 | **War** | **NOT AT M4** | `AppropriationSystem` (raiding) ships; armies and the AutoResolver do not. D-011 §6 puts the battle layer at **M6** |
| 20 | **Culture** | **KEY ONLY** | culture and religion are dimensions of the **bucket key** and nothing reads them. Plurality deferred to M8/M9; **R-3 bites there** (`docs/m4-spec.md:465-508`) |
| 21 | **Happiness** | **SHIPS, DERIVED** | `SettlementHappiness` — a **DERIVED READING**, never a stock, never serialized (F16) |

**Four nodes the tree has that the mandate's list does not name, added because edges terminate on
them:** **Housing/Shelter** (`HousingSystem`), **Construction** (`ConstructionSystem`,
`StructureRow`), **Prices/Markets** (`PriceSystem`, local prices only — global equilibrium is
*permanently anti-scoped*, `docs/civ-sim-architecture-v3-outline.md:98`), and **Transport/Network**
(`PathBuildSystem`, `CatchmentSystem`, the traversal lattice).

---

## §5 THE SHIPPED GRAPH — EDGES THAT EXIST TODAY

**MEASURED** (this lane, `26d12d3`), by reading each system's `prev.*` accesses and its
`SystemCatalog` registration. Relation per §2. Carrier per F8. Lag is **PREV** unless the row says
**SAME-TURN**.

| # | edge | relation | carrier (F8) | lag | shipping system / file |
|---|---|---|---|---|---|
| S1 | Climate-static (terrain, moisture) → Agriculture | INFLUENCE | **the land** (a place) | worldgen only, immutable | `Sim.Core/Worldgen/WorldFounding.cs:325-352` (`HinterlandMeans`); `EffectiveArableKm2` |
| S2 | Weather → Agriculture | INFLUENCE | **a season** | PREV | `HarvestWeatherSystem` → `ProductionSystem.cs:239`, `:306` (`ratePerYear *= foodMultiplier`) |
| S3 | Weather → Weather (neighbours) | INFLUENCE | **a season**, blended over `SettlementDistances` **travel costs** | PREV | `HarvestWeatherSystem` (spatial blend). **Recorded as physically wrong by arch-I §3.3**: improving roads shrinks the weather field's footprint |
| S4 | Disasters → Agriculture | INFLUENCE | **a season** (the crop failure) | PREV | `DisasterSystem` → `ProductionSystem` (same two food paths weather multiplies). **λ = 0.0: the edge ships inert** |
| S5 | Agriculture → Food store | WRITE (shared) | **a good** (grain) | **SAME-TURN** | `ProductionSystem` → `GoodStocks` via `Ledger`, reason `Harvest` (`SystemCatalog.cs:38-43`) |
| S6 | Food store → Population | INFLUENCE | **a good** (grain), eaten | **SAME-TURN** (Consumption reads its own shared Next stock, F6) then PREV into Demographics | `ConsumptionSystem.cs:46-48`; `ConsumptionDeficitRow` → `DemographicsSystem` (PREV) |
| S7 | Population → Agriculture | INFLUENCE | **a body** (the farmers) | PREV | `ProductionSystem` reads `prev.Buckets` and `prev.SectorAllocations` for the labour side of the `min()` |
| S8 | Population → Food demand | INFLUENCE | **a body** | PREV | `ConsumptionSystem` (per-class nutritional requirement, D-018/D-035) |
| S9 | Materials (deposits) → Production | READ + INFLUENCE | **a good** (the ores) | PREV | `ProductionSystem.FromDeposits` (`:287-310`); `DepositRow` |
| S10 | Production → Prices | READ | **a good** | PREV | `PriceSystem` reads `prev.GoodStocks`; damped local excess-demand steps |
| S11 | Prices + Transport → Trade | INFLUENCE | **a good in motion**, **a path** | PREV | `TradeArbitrageSystem` reads `prev.Prices`, `prev.SettlementDistances` |
| S12 | Trade → Class emergence (merchants) | INFLUENCE via a **published variable** | **a good in motion** | PREV (twice: publish, then predicate) | `TradeFlowRow` → `ClassMobilitySystem.cs:165` publishes `trade_volume` → `sim.json:176-178` merchant `emerge` |
| S13 | Agriculture + Population → Class emergence (artisans) | INFLUENCE via **published variables** | **a good** (food), **a body** | PREV | `ClassMobilitySystem.cs:144-152` publishes `food_surplus_ratio`, `population` → `sim.json:169-171` |
| S14 | Class emergence → Recipe availability | INFLUENCE via a **published variable** | **a body** (the artisans) | PREV | `artisan_share` → `goods.json:136,156,159,175` `"requires": "artisan_share > 0.05"` (`ProductionSystem.cs:394-396`). **This is the capability seam, live** |
| S15 | Production (tools) → Agriculture | INFLUENCE | **a good** (tools), worn through a Ledger sink | PREV | `equipRatio` → `toolFactor` on the labour side of the harvest `min()` |
| S16 | Production (timber, clay) → Housing | WRITE (shared, SINK only) | **a good** | **SAME-TURN** | `HousingSystem` owns `Amount` via `Ledger` SINK, reason `HousingMaterials`, on TIMBER and CLAY only (`SystemCatalog.cs:46-48`) |
| S17 | Housing → Migration / Catchment | READ | **a building** | PREV | `CatchmentSystem`, `MigrationSystem` read `prev.Housing` |
| S18 | Construction → `StructureRow` | WRITE | **a building** | — | `ConstructionSystem`. **MEASURED GAP: no resolution equation anywhere in `Sim.Core` reads `StructureRow`** (arch-E X-E8, arch-JKL X-02) |
| S19 | Food deficit → Migration | INFLUENCE | **a body** | PREV | `MigrationSystem` reads `prev.ConsumptionDeficits`, `prev.GoodStocks`, `prev.SmoothedAttractiveness` |
| S20 | Migration → Population distribution | WRITE (shared `Buckets`) | **a body**, `Ledger.Transfer` | — | `MigrationSystem`; bounded by ADR-025 (`ω` = **the Exit valve's openness**, `docs/m4-exit-inventory.md:332-333`) |
| S21 | Migration → Colonization | INFLUENCE | **unplaced departure demand** — *"the migration→colonization **carrier**"* | PREV | ADR-021 (`docs/adr/adr-021-unplaced-departure-demand.md:1`, `:22-26`) — the tree's own named carrier row |
| S22 | Population + Goods + Housing → Needs/Grievance | READ | **a good**, **a building**, **a body** | PREV | `NeedsGrievanceSystem` reads `prev.Buckets`, `prev.GoodStocks`, `prev.Housing`, `prev.Grievances` |
| S23 | Needs/Grievance → *nothing* | — | — | — | **QUARANTINED BY GATE.** `scripts/check-read-isolation.sh` fails CI on any sim-side reader outside the allowlist. **The edge is deliberately absent until M5** |
| S24 | Conditions → Happiness | **DERIVE** | **a world variable** | on demand | `SettlementHappiness.Of` — RECOMPUTED, never stored (F15, F16) |
| S25 | Player/AI order → Sector allocation | **CONTROL** | **a policy** | order applies one turn after it lands | `PathBuildSystem.cs:17-24`; *"an order steers yields exactly one turn after it lands"* |
| S26 | Control relation → Appropriation / Revolt | READ | **a policy** (the control relation) | PREV | `AppropriationSystem` and `ConstructionSystem` read `prev.Controls`; `RevoltSystem` can remove control |
| S27 | Transport build → Catchment → Arable | INFLUENCE | **a path** | PREV | `PathBuildSystem` → `CatchmentSystem` → `EffectiveArableKm2` (the land side of the `min()`) |

**Two structural facts about the shipped graph, MEASURED and worth stating plainly.**

1. **Every published variable is published by ONE system — `ClassMobilitySystem`** (F12). The
   capability seam's supply side is, today, a single domain system's side-job. **INFERRED:** any
   widening of the seam has to decide whether that stays true, and the answer bears directly on
   the §7.3 God-object question (F14). This lane does not answer it.
2. **`PathBuildSystem` reads every table in `WorldState`.** MEASURED this pass: 40+ distinct
   `prev.*` accessors. It is the one node with near-universal in-degree. **INFERRED, and stated as
   an observation rather than a defect:** a system with universal read access is the shape a God
   object would take if one ever formed, and the anti-pattern list names exactly that risk
   (`docs/d042-…:196-203`). It is currently reading in order to route paths over the network, not
   in order to coordinate anybody — but it is the node to watch.

---

## §6 THE PROPOSED GRAPH — EDGES THE SIX SIBLING LANES PROPOSE

Every row is **PROPOSED** by the named lane. The carrier column is D-035-C (F8): a named carrier,
or **OWED** with the debt id the proposing lane assigned it. No row here is scheduled, endorsed or
ruled.

### 6.1 Knowledge, technology and capability (arch-C, arch-D, arch-E)

| # | edge | relation | carrier | lane |
|---|---|---|---|---|
| P1 | Practice (doing the work) → HOLDING firmness | INFLUENCE | **the practitioners and the workshop** (a body, a building) — *named* | arch-C §2.1, §5 |
| P2 | HOLDING → published knowledge variable | WRITE (own table) then READ | — (publication, not a coupling) | arch-C §4 |
| P3 | published knowledge variable → PRACTICE predicate | **DERIVE** | — (the D-020 seam, F13) | arch-C §2.1; arch-D §3.2 |
| P4 | PRACTICE → recipe availability / production | INFLUENCE | **a good** (what gets made) | arch-D §3.6(6)(7) |
| P5 | INQUIRY directive → people + food | **CONTROL** (persistent directive) | **a body, a good** — `Ledger`-conserved inputs | arch-C §2.1; D-042 §6.2 RATIFIED |
| P6 | knowledge region → knowledge region (*"metallurgy makes machinery thinkable"*) | INFLUENCE | **CARRIER OWED — OWED-E1 / arch-C Q-C4** | arch-E §12 E15 |
| P7 | Empire holding ↔ settlement holding | READ both ways | **CARRIER OWED — OWED-4 / OWED-E4 / FGH-OWED-3** (messengers, administration, an itinerant scholar) | arch-C §6; arch-D §4.1 |
| P8 | knowledge → capability (the join itself) | INFLUENCE | **CARRIER OWED — arch-D §4.3**, *"the largest hole in this document"* | arch-D §4.3 |
| P9 | pressure term (a binding constraint) → arrival hazard λ | INFLUENCE | **CARRIER OWED — OWED-E2** (the people who feel the constraint) | arch-E §3.1 |
| P10 | knowledge → literacy → rising expectations → grievance | INFLUENCE | **a body** (a literate person), **a building** (a school) — **OWED-2 / OWED-E5 / FGH-OWED-6**: no literacy variable exists | arch-C §4.2 valve 3 |
| P11 | trade contact → diffusion | INFLUENCE | **DISPUTED.** arch-FGH: *"a good"* carries the **existence** of a technique. arch-C: *"A good is not a teacher"* — **OWED-1**. Both PROPOSED; neither ratified | arch-FGH §4.1 ch.2 vs arch-C OWED-1 (**X-8**) |
| P12 | literacy + roads + institutions → command capability | INFLUENCE | **a body** (the messenger), **a building** (the road) — **no publisher** (C-07) | arch-E E14; D-039 A5 RATIFIED |

### 6.2 Climate, environment, pollution (arch-I)

| # | edge | relation | carrier | lane |
|---|---|---|---|---|
| P13 | CLIMATE DRIVER → rainfall anomaly | **DERIVE** (RECOMPUTED) | **a season** | arch-I A1 |
| P14 | rainfall × static `_moisture` → effective moisture | INFLUENCE | **a season** acting on **the land** | arch-I A2 |
| P15 | effective moisture → soil-water availability | WRITE (new `double`) | **water in the root zone** | arch-I A3 |
| P16 | soil-water → FALLOW BUDGET → sown share → yield | INFLUENCE | **the fallow field, a season** | arch-I A4–A6 — **BLOCKED by §6.4's wall** (ADR-008 + the ruled meaning of 26.0) |
| P17 | climate water term → river discharge proxy | **DERIVE** | **the river** | arch-I A8 |
| P18 | production/population → pollution LOADING (air, water, soil) | WRITE | **a good**, **a body** | arch-I §7.2 |
| P19 | water loading → downstream settlement | INFLUENCE | **the river** — `TerrainSet._riverPolylines`, already immutable and hash-folded. **The cleanest carrier in that document** | arch-I §7.3 |
| P20 | discharge → assimilative capacity | INFLUENCE | **the river** | arch-I §7.3 — *"a dry decade concentrates the same effluent into less water"* |
| P21 | soil loading → agriculture | INFLUENCE | **the land, the fallow field** | arch-I B1 — **blocked by the same wall as P16** |
| P22 | water quality → production inputs | INFLUENCE (D-035-C path 3, demand coupling) | **a good** | arch-I B2 |
| P23 | air/water quality → mortality | INFLUENCE | **a body** — carrier nameable, **NO SUBSTRATE** (no health quantity; Health & disease is M8/M9) | arch-I B3, **O2** |
| P24 | environmental quality → Happiness | INFLUENCE (a new non-compensatory **Factor**) | **a world variable** (D-035-C path 4) | arch-I B4, §8 |
| P25 | AIR loading → anything | — | **CARRIER OWED — O1.** Air has no medium, no consumer, no substrate. **PROPOSED for omission, not for inclusion at zero** | arch-I O1 |

### 6.3 Civics, institutions, diffusion (arch-FGH)

| # | edge | relation | carrier | lane |
|---|---|---|---|---|
| P26 | taxation in kind → a polity-scoped stock | WRITE (`Ledger.Transfer`) | transport carries the grain; **the DESTINATION STOCK DOES NOT EXIST** — **FGH-OWED-1** | arch-FGH §5; recovery **C-03/C-06** |
| P27 | government / regime module → capability | INFLUENCE via published variables | **a policy** | arch-FGH §2.1; D-042 §5.2 RATIFIED (*"through state and computed conditions"*) |
| P28 | Clergy → legitimacy | WRITE/INFLUENCE | **CARRIER OWED — FGH-OWED-7** (a body, a building) into an undefined quantity | arch-FGH §5 |
| P29 | legitimacy + state capacity + military loyalty → overthrow propensity | INFLUENCE | **CARRIER OWED — FGH-OWED-8**; *whose* loyalty is unruled | arch-FGH §5; D-009/D-010 `:37` RATIFIED |
| P30 | conquest → knowledge, people, control | WRITE | **an army (bodies)** — **FGH-OWED-2**; `NotableLifecycle.Defects` ships **with no caller** | arch-FGH §5 |
| P31 | diplomacy → anything | — | **CARRIER OWED — FGH-OWED-4**; `RecognitionRow` carries no payload by design | arch-FGH §5 |
| P32 | education access → class mobility (Intelligentsia) | WRITE (`Ledger.Transfer`) | **the carrier SHIPS** (class mobility); **the operand is owed** — no literacy variable, no Intelligentsia class in data — **FGH-OWED-6** | arch-FGH §4.1 ch.5; D-018 `:63` RATIFIED |
| P33 | closing exits (a policy) → pressure redirected into Voice | **CONTROL** | **a policy** | arch-FGH V5; D-021 valve 3 RATIFIED. **Weakest pairing in that lane's table** (X-7) |
| P34 | geographic observation → computed extent | READ | **a body** (the scouting party) — ride D-039's existing mechanism, *"Do not invent a second exploration system"* | arch-FGH §4.1 ch.7; D-040 B1/B2 RATIFIED |

### 6.4 Food, disaster, needs (arch-JKL)

| # | edge | relation | carrier | lane |
|---|---|---|---|---|
| P35 | storage technology → spoilage rate T1 / capacity T2 | INFLUENCE | **a good** (pottery, sealed containers) / **a building** (the granary) | arch-JKL §2.2, §3; arch-E E5/E6 |
| P36 | `StructureRow` (granaries built) → capacity T2 | INFLUENCE | **a building** — **carrier named in ratified text, mechanism absent** (S18) | arch-JKL X-02; arch-E X-E8 |
| P37 | seed corn → next turn's harvest | INFLUENCE | **a good** (the reserved grain) | arch-JKL §5.4 — the **first food mechanism with memory across turns**; **D-021 brake OWED-J4** |
| P38 | drainage / levees → flood exposure and vulnerability | INFLUENCE | **a building** | arch-JKL §9.2, §9.3 |
| P39 | forecasting → response | INFLUENCE | **knowledge state** — **CARRIER OWED — OWED-K1** (belongs to arch-C's registers) | arch-JKL §9.4 |
| P40 | institutions → recovery | INFLUENCE | **an institution** — circular while CR-010 is unruled; **brake OWED-K3** | arch-JKL §9.6 |
| P41 | provision history → expectation baseline → need salience | INFLUENCE | **a good** and **a purse** — **the purse does not exist** (**OWED-L2**) | arch-JKL §13 |
| P42 | literacy + urbanization + media → rising expectations | INFLUENCE | **OWED-L1** — none of the three is published | arch-JKL §15; D-018 `:48` RATIFIED |

---

## §7 THE CYCLE HUNT

**Method, stated so it can be checked.** Every edge in §5 and §6 was written as a directed pair,
the pairs were composed, and every cycle of length ≤ 5 that the composition produced was examined
by hand. Each cycle below is then run against the §3 rule: does clause (1) discharge it, does
clause (2), or is it **FLAGGED**? **MEASURED** for the shipped cycles (the edges are read off
code); **PROPOSED/INFERRED** for the rest, as marked.

### 7.1 Cycles that SHIP, and how they are broken

| # | cycle | clause (1) PREV break? | clause (2) D-021 brake? | verdict |
|---|---|---|---|---|
| **C-1** | Population → Agriculture (S7) → Food store (S5) → Population (S6) | **PARTIAL.** S5 and S6 are **SAME-TURN** through the shared `GoodStocks` table (F5, F6); only the deficit → Demographics leg is PREV | **YES, RATIFIED and named:** this *is* the Malthusian loop. The brake is starvation mortality rising with the deficit, and it strengthens with amplitude by construction | **LEGAL, on clause (2).** Note the honest wrinkle: clause (1) does **not** fully discharge it. The cycle depends on `pipeline.json`'s ordering of `production → consumption`, which is **data** |
| **C-2** | Food deficit → Migration (S19) → Population distribution (S20) → Food deficit | **YES** — every leg PREV | **YES:** `ω`, the bounded-migration openness term (ADR-025), is recorded as *"the **Exit valve's** openness"* (`docs/m4-exit-inventory.md:332-333`) | **LEGAL.** And it is the tree's own worked example of a cycle that had to be fixed twice: the M2 **resurrection cycle** and starvation magnetism were this cycle running away inside a legal topology (ADR-012, `docs/milestones.md:43-99`) |
| **C-3** | Agriculture → `food_surplus_ratio` (S13) → artisan emergence → `artisan_share` (S14) → recipe availability → Production → Agriculture (S15, tools) | **YES** — every leg PREV, and the predicate itself evaluates against PREV rows (F12) | **PARTIAL.** The `recede` clause (`food_surplus_ratio < 1.1`) is a hysteresis brake, but it is a **threshold**, not an amplitude-strengthening term | **LEGAL on clause (1); clause (2) is thin.** INFERRED, recorded not asserted: this is the shipped capability seam closing a loop on itself, and it is the template every PROPOSED knowledge edge will copy |
| **C-4** | Trade → `trade_volume` (S12) → merchant emergence → trade (more traders) | **YES** — PREV throughout | **PARTIAL** — the `recede` clause (`trade_volume < 50`) again | **LEGAL on clause (1).** The merchant `_doc` states the reason for the latch: *"a single threshold would have oscillated on and off"* |
| **C-5** | Transport build (S27) → catchment → arable → harvest → population → more path labour → transport | **YES** — PREV throughout | **NOT NAMED.** Path build consumes labour that farming would otherwise use, which is a competition brake, but no document names it as the D-021 pairing | **LEGAL on clause (1); clause (2) UNNAMED.** Recorded, not asserted as a defect |
| **C-6** | Weather → neighbour weather (S3) → … | not a cycle in systems; a spatial coupling inside one system | n/a | **Not a cycle.** Recorded because arch-I §3.3 records the coupling as **physically wrong** (it runs over travel costs, so building roads shrinks the weather field), which is a *different* finding and belongs in §9 |

### 7.2 Cycles the PROPOSED graph would create

| # | cycle | clause (1)? | clause (2)? | verdict |
|---|---|---|---|---|
| **C-7** | practice → HOLDING (P1) → published variable (P2) → PRACTICE predicate (P3) → production (P4) → more practitioners → practice | **YES** if every leg is a PREV read of a published variable, which is what arch-C proposes | **PROPOSED, three brakes**, of which one is RATIFIED: *breadth taxes depth*; diminishing returns per corpus region; and D-021's ratified *"Education is a flow with a lag … Educating your population remains a deliberate gamble; that tension is kept, not patched"* | **LEGAL IF the brakes land in the same milestone.** **But** the ratified brake runs through **literacy**, and **no literacy variable exists** (OWED-2/E5/FGH-6). **INFERRED: the pairing is currently unsatisfiable, not merely unbuilt** |
| **C-8** | knowledge region → knowledge region (P6) → … → back | **only if** the reading is a PREV read of a published variable | **none proposed** | **FLAGGED.** This is the edge that *is* the technology tree if it is allowed, and three records propose banning it while **none rules** (recovery G-04). §7.4 below treats it separately |
| **C-9** | pressure term → arrival hazard λ (P9) → a capability that relieves the pressure → pressure falls | **YES** by construction | **THE RELIEF IS THE BRAKE**, and arch-E §8 makes it conditional: *"if λ is driven by a constraint the arrival cannot relieve, the negative valve never engages and the loop is unpaired"* (Q-E8) | **LEGAL ONLY UNDER arch-E Q-E8's condition.** **FLAGGED** until that condition is ruled, because nothing enforces it |
| **C-10** | population → pollution loading (P18) → environmental quality → happiness (P24) → migration → population | **YES** — every leg a PREV read | **PROPOSED** by arch-I §10; the brake is out-migration, which grows with the loading | **LEGAL if arch-I §10's pairing is accepted.** Note it **passes through Happiness, which is a DERIVED READING** (F16) — see §7.5 |
| **C-11** | pollution → mortality (P23) → population → pollution | **YES** | not named | **FLAGGED — and blocked before it can be flagged properly**: there is **no health substrate** (O2), so the cycle cannot exist yet |
| **C-12** | soil loading → fallow budget → yield → population → soil loading (P21, P16) | **YES** | **arch-I §10 records this loop as UNPAIRED under one of its three shapes** ("multiply output only … leaves D-021 loop 1 unpaired") | **FLAGGED, conditionally.** Which shape is chosen decides whether the loop is paired. **DIRECTOR DECISION REQUIRED** (arch-I Q4) |
| **C-13** | drainage/levees → exposure falls (P38) → denser floodplain settlement → larger loss when overtopped | **YES** | **NONE — arch-JKL records it as OWED-K2**, *"a genuine open resilience problem, not a gap in this lane's search"* | **FLAGGED.** This is the levee effect and it is a textbook unpaired positive loop |
| **C-14** | seed corn reserved (P37) → smaller harvest this turn → more reserved next turn → … | **YES** | **NONE — OWED-J4.** arch-JKL declines to assert a brake it did not demonstrate | **FLAGGED** |
| **C-15** | taxation in kind (P26) → grievance → out-migration → smaller taxable base → less exaction | **YES** | **YES, PROPOSED and well-formed** (arch-FGH V1): *"The drain grows with the rate"* | **LEGAL**, but **P26's destination stock does not exist** (FGH-OWED-1), so the cycle cannot be built at all today |
| **C-16** | provision → expectation baseline (P41) → salience → grievance → … | **YES** | **THE BRAKE IS AN M8 VALVE** while the source could land at M5 — arch-JKL **X-06**, *"whichever milestone implements rising expectations owes a brake in the same milestone, and the ratified brake is scheduled for M8"* | **FLAGGED — a milestone-alignment cycle, not a topological one.** DQ-L5 |

### 7.3 THE ONE CYCLE THAT IS NOT IN TIME AT ALL — and it is the sharpest finding of this hunt

**C-17 — THE DERIVATION CYCLE: buffer → severity → buffer.**

- **Source A, RATIFIED (shipped data):** the famine-class disaster's severity floor is derived so
  the event *"can exhaust the full EFFECTIVE buffer of a settlement at the shipped surplus ratio
  ALONE at the coarsest era dt"* — `s_min · D > (dt_max(ρ_ship − 1) + G)/ρ_ship`
  (`Sim.Data/content/sim.json:234`).
- **Source B, DIRECTOR-STATED** (relayed, **UNVERIFIED as a repository document**): the frequency
  chain is *"real-world hazard frequency → geographic exposure → settlement exposure → event
  probability → its representation in a 10-year turn"*, and the simulation *"must not invent the
  underlying FREQUENCY by tuning for acceptable gameplay"*
  (`docs/design/m4-closure-audit.md:551-555`).

**The cycle, stated as arch-JKL states it (X-03), INFERRED:** under Source A, **improving the
economy mechanically requires hazards to grow**, because the event must still exhaust a larger
buffer. Better storage raises `G`; a raised `G` raises the derived `s_min`; a raised `s_min` is a
worse disaster. **The loop closes in the DERIVATION, not in the turn pipeline** — so **neither
clause of the §3 rule reaches it.** The prev-read cannot break it, because no turn elapses inside
it; D-021 cannot brake it, because nothing is integrating.

**FLAGGED, and it is the one cycle in this document that the cycle rule as written does not
cover.** **PROPOSED addition to the rule, offered for the Director to accept or reject:

> **(3) THE DERIVATION BREAK.** A quantity may not be derived from a quantity that a shipped
> mechanism moves in response to it. A constant whose derivation reads a sim-measured term that
> the constant itself perturbs is **self-referential in the same sense S8 §4.1's corridor
> independence means**, and is refused at spec time.

**INFERRED, and it is why the addition is worth considering:** S8 §4.1 requirement 3 already
states exactly this shape for *corridors* — *"A corridor whose denominator moves with the measured
quantity is SELF-REFERENTIAL and is REFUSED at spec time, not discovered later"*
(`docs/spine-s8-governance-freeze.md:154-182`). C-17 is the same failure mode in a **derived
constant** instead of a corridor band, and the frozen rule does not currently reach it.
**DIRECTOR DECISION REQUIRED.**

### 7.4 THE LATCH-REFERENCE CYCLE — stated separately because it is how the tree grows back

**C-8 deserves its own treatment**, because three independent records identify it as the single
edge that reintroduces the banned technology tree, and **none of them rules.**

- *"Capability A referencing capability B's **latch** is how a tree grows back — **that is the
  line to hold**"* — `docs/capability-architecture-decision.md:203-206` (SECONDARY EVIDENCE, and
  that document's own recommendation is **UNVERIFIED**, `:34-37`).
- Restated *"Proposed"* at `docs/milestone-architecture-governance.md:319-321` and
  `docs/adr/cr-007-b3-exemplar-reconciliation.md:233-234`. **Recovery-lane G-04: no document
  rules on it.**

**MEASURED, and it changes the shape of the question.** The D-020 grammar has **no latch
operand** — operands are `variableName | numberLiteral` (`Sim.Core/Systems/ClassMobility/Predicate.cs:18`,
`:10-27`). So the ban is enforced today **by the accident of the grammar's closure, not by a
rule.** But **arch-D Q3** states the hole precisely: a system may publish a 0.0/1.0 variable
derived from its own latch, with **no grammar change, no schema change and no review trigger**,
and a second capability may then read it. **INFERRED: the fence is one data-shaped edit away from
being gone, and nothing would fail.**

**FLAGGED. DIRECTOR DECISION REQUIRED** — and the question is ordered in §Q of
`arch-PQ-conflicts-and-questions.md` as one that should be ruled **before** the D-020 grammar or
the published-variable registry is widened, not after.

### 7.5 A cycle that passes through a DERIVED READING — and why that is not a loophole

**C-10** routes through `SettlementHappiness`, which is a **DERIVED READING, never a stock**
(F16). **INFERRED, recorded so a future packet does not misread it:** a derived reading in a cycle
does **not** break the cycle. It removes the *storage*, not the *feedback*. The causal direction is
already ruled: *"conditions → happiness → migration pressure → movement → changed conditions →
recalculated happiness. **Migration must never grant happiness directly**"*
(`docs/m4-exit-inventory.md:310-312`, RATIFIED). That ruling is precisely a statement about this
cycle: it forbids the **short-circuit edge** (migration → happiness) while permitting the long way
round. **PROPOSED reading:** "no direct grant" is the shipped, ratified instance of the cycle rule
applied to a derived reading, and any future derived reading in a loop owes the same fence.

### 7.6 Cycle-hunt summary

| verdict | cycles |
|---|---|
| **LEGAL — prev-broken and braked** | C-1 (clause 2 only), C-2, C-15 (if P26's stock ever exists) |
| **LEGAL — prev-broken, brake thin or unnamed** | C-3, C-4, C-5 |
| **LEGAL ONLY UNDER A CONDITION NOT YET RULED** | C-7 (brake unsatisfiable today), C-9 (arch-E Q-E8), C-10, C-12 (arch-I Q4) |
| **FLAGGED — unresolved cycle** | **C-8** (latch reference), **C-13** (levee effect), **C-14** (seed corn), **C-16** (milestone misalignment), **C-17** (the derivation cycle) |
| **Cannot yet exist** | C-11 (no health substrate) |

**INFERRED, and it is the sentence this lane would put in front of the Director:** not one of the
five FLAGGED cycles is a topology problem. Every one is either an **unpaired positive loop**
(C-13, C-14), a **pairing that lands in the wrong milestone** (C-16), a **fence enforced by
accident** (C-8), or a **loop in the derivation rather than in the world** (C-17). The prev-read
discipline is doing its job; what is thin is D-021 enforcement at design time.

---

## §8 THE CARRIER LEDGER — CONSOLIDATED

Every edge in §6 whose D-035-C carrier its proposing lane could not name, de-duplicated across
lanes. **This is a debt register, not a work plan.**

| consolidated debt | lane ids | what would have to carry it | what it blocks |
|---|---|---|---|
| **CD-1 — settlement ↔ Empire, anything crossing** | arch-C OWED-4 · arch-D 4.1 · arch-E OWED-E4 · arch-FGH OWED-3 | messengers, administration, an itinerant official — **a body** | P7, P27, every Empire-scoped conjunct, and D-042 §8.3 itself |
| **CD-2 — an INSTITUTION as a carrier** | arch-C OWED-3 · arch-D 4.1/4.3/4.6 · arch-E OWED-E3 · arch-FGH OWED-5 · arch-JKL OWED-K3 | a building and the people in it | P28, P40, D-035-C paths 6 and 7, the whole of Part 14. **Circular**: D-035-C names *"an institution"* as a legal carrier while CR-010 is unwritten |
| **CD-3 — literacy / education as an operand** | arch-C OWED-2 · arch-E OWED-E5 · arch-FGH OWED-6 · arch-JKL OWED-L1 | **the carrier SHIPS** (class-mobility `Ledger.Transfer`); the **variable** does not | P10, P32, P42, C-7's ratified brake, D-018's Intelligentsia, D-039 A5's command capability |
| **CD-4 — diffusion across a contact edge** | arch-C OWED-1 · arch-D 4.4 · arch-E OWED-E6 · **vs** arch-FGH §4.1 ch.2 | a body travelling with the goods — **or**, per arch-FGH, the good itself for *existence* only | P11. **Two lanes disagree (X-8) and neither is ratified** |
| **CD-5 — knowledge → capability, the join** | arch-D 4.3 (*"the largest hole in this document"*) | a person who knows how, a written record, a workshop — the repository does not say | P8 |
| **CD-6 — a polity-scoped destination stock** | arch-FGH OWED-1 · recovery C-03/C-06 | transport carries the grain; the **stock** does not exist | P26, all M5 taxation, every "stipend"/"buy off leaders" line |
| **CD-7 — a health / contamination substrate** | arch-I O2 · arch-E domain 10 | a body — **carrier nameable, substrate absent** | P23, C-11 |
| **CD-8 — a mutable land quantity** | arch-I O5/X5 · arch-E OWED-E7 | **the land itself** — carrier nameable, **ADR-008 refuses the substrate** | P16, P21, C-12, irrigation (E12) |
| **CD-9 — espionage, diplomacy, conquest** | arch-C OWED-5 · arch-FGH OWED-2/OWED-4 | an agent, an envoy, an army | P30, P31 |
| **CD-10 — a purse** | arch-JKL OWED-L2 | money, which does not exist and is deferred past M5 | P41's *why* composition shifts |

**INFERRED, stated because the table is otherwise easy to misread as ten separate problems:**
CD-1, CD-2, CD-5 and CD-6 are four faces of **two** missing objects — *an Empire-scoped substrate*
and *a defined institution*. arch-D says it in one line and this lane confirms it across all six
lanes: *"The carrier problem in this design is not six problems. It is largely one undefined noun,
appearing three times."*

---

## §9 CONFLICTS SURFACED BY THE GRAPH ITSELF

These are conflicts that only became visible when the six lanes were composed. Both sources
named; nothing reconciled. Conflicts the individual lanes already surfaced are consolidated in
`docs/design/arch-PQ-conflicts-and-questions.md` and are not restated here.

**X-M1 — The kernel names an artefact that records same-turn edges; that artefact does not
exist, and same-turn edges ship.**
*Source A, RATIFIED (frozen):* *"deliberate same-turn edges (later milestones) are explicit
kernel-ordered handoffs **listed in the interaction matrix**"* (`docs/m0-kernel-spec.md:66`);
*"the interaction matrix marks the few deliberate same-turn edges"*
(`docs/civ-sim-architecture-v3-outline.md:32`).
*Source B, MEASURED (this lane):* at least one same-turn edge ships and is documented at its own
site instead (`ConsumptionSystem.cs:46-48`); the shared-table sanction lives in
`SystemCatalog.cs:31-60`; **no interaction-matrix artefact was located** (recovery-lane G-06).
**Recorded, not reconciled.** The information exists; it is distributed across three code
comments rather than one reviewable row-set. **INFERRED consequence for this graph:** a future
packet adding a same-turn edge has no single place to look to see what it is joining.

**X-M2 — Every published variable has one publisher, and that publisher is a domain system.**
*Source A, RATIFIED:* the seam is conformant *"only as a shared predicate grammar consumed
independently by each domain system … and **not** as a coordinating owner"* (`docs/d042-…:280-282`).
*Source B, MEASURED (this lane):* all four published variables are written by
`ClassMobilitySystem` (`:144-165`), including `trade_volume`, which is a **trade** quantity, and
`population`, which is a **demographic** one. And the shared `Predicate` type itself lives inside
`Sim.Core.Systems.ClassMobility`, reached by qualified name from `ProductionSystem`
(`ProductionSystem.cs:105-108`) — arch-D §5.9 records the same observation.
**Recorded, not reconciled.** This is conformant under law 6 (a `Predicate` is an immutable value
type), and it is also the exact centre of gravity a God object would occupy. **DIRECTOR DECISION
REQUIRED** on whether a widening seam keeps one publisher.

**X-M3 — The weather's spatial coupling runs over travel costs.**
*Source A, RATIFIED:* the shipped spatial blend over `SettlementDistances`, ratified at T3.4b/T3.4c
with *"SPATIAL CORRELATION REQUIRED"* behind it (`docs/m3-spec.md:52`).
*Source B, MEASURED/recorded:* *"improving roads shrinks the weather field's effective footprint
in map terms"* — arch-I §3.3 and recovery-lane E-32/E-40. arch-I X4 adds that
`spatialSharedFraction` and `spatialRangeCostUnits` have **no semantic test**.
**Recorded, not reconciled.** In graph terms it is an edge **Transport → Climate** that nobody
designed and that no lane proposes to keep.

**X-M4 — `StructureRow` is written, counted, and read by no equation.**
*Source A, RATIFIED (shipped data):* `sim.json:43` states the granary constant's carrier as *"a
structure of finite size, which grows with the settlement."*
*Source B, MEASURED (two independent lanes):* `StructureRow` is read by **no resolution equation**
in `Sim.Core`; the constant scales with population instead (arch-E X-E8, arch-JKL X-02).
**Recorded, not reconciled.** In graph terms, S18 is an edge that terminates nowhere.

**X-M5 — Two design lanes disagree about whether a traded good can carry a technique.**
*Source A:* arch-FGH §4.1 channel 2 — a good carries the **existence** of a technique.
*Source B:* arch-C OWED-1 — *"goods move, people do not … A good is not a teacher."*
**Both PROPOSED, neither ratified, no sibling file touched.** Surfaced so the Director sees a
disagreement rather than a silently merged position (arch-FGH **X-8**, arch-C **X-04**).

**X-M6 — The observability taxonomy as this lane's brief renders it, versus as the document
reads.** The brief says *"READ / RECOMPUTED / DERIVED / GAP"*; the ratified text says *"exactly
five things"* — **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED** — plus **GAP**, and
**"DERIVED" is not one of the five** (`docs/observability-architecture.md:19-32`, F15). Recorded
under GOV-3 B4 as a correction to this lane's own prompt, not as a repository defect; four sibling
lanes recorded the same correction independently. **A freshness fact that travels with it:**
`docs/observability-architecture.md` is **not on `origin/main`** (`dbef61a`) — arch-FGH X-5.
Anything citing it as RATIFIED should say which tree it stands on. **This document stands on
`26d12d3`.**

---

## §10 WHAT THIS GRAPH ASKS THE DIRECTOR FOR

Stated as questions, with no preferred answer. They are carried, ordered by blast radius and
de-duplicated against every lane, in `docs/design/arch-PQ-conflicts-and-questions.md`; only the
ones this lane **originated** are listed here.

- **Q-M1.** Does the Director accept the **§3 cycle rule** as a conformance obligation on future
  packets — and in that two-clause form, where clause (1) explicitly does *not* discharge the
  D-021 obligation?
- **Q-M2.** Does the rule need the **third clause** §7.3 proposes — the **derivation break** — and
  if so, is it an extension of S8 §4.1's corridor-independence test or a separate rule?
- **Q-M3.** What records **same-turn edges** now that the interaction matrix does not exist
  (X-M1)? Is the `SystemCatalog` shared-table paragraph the intended artefact, and does a new
  same-turn edge owe a row somewhere?
- **Q-M4.** Should the **single-publisher** shape of the published-variable registry (X-M2) be
  preserved, split by domain, or ruled on explicitly before the seam is widened?
- **Q-M5.** May a system **publish a variable derived from its own latch** (§7.4, arch-D Q3)? This
  is the one FLAGGED cycle that can be created with no schema change and no review trigger.

---

## §11 CAVEATS ON THIS DOCUMENT

1. **The shipped edges are read off code; the proposed edges are read off sibling documents.**
   The two halves of §5 and §6 therefore have different evidence classes and are labelled
   accordingly. A PROPOSED edge's carrier is its proposing lane's claim, re-stated here, not
   re-verified by this lane against the tree.
2. **The cycle hunt is bounded at length 5 and was done by hand.** Longer cycles exist —
   C-1 composed with C-2 composed with C-3 is one — and were not enumerated. **The hunt is
   therefore a floor, not a proof.** A cycle this document does not list is not thereby absent.
3. **No sibling lane's file was read for its conclusions and then re-decided.** Where two lanes
   disagree (X-M5), both positions stand and neither is merged.
4. **Four of the twenty-one mandate domains have no substrate at all** (Climate, Environment,
   Pollution, Technology) and three more have only a key or a coefficient (Water, Culture,
   Legitimacy). Edges touching them are necessarily more speculative than edges among the shipped
   fourteen, and §6 does not grade them differently — the reader should.
5. **This lane ran no test, no build, no bench and no measurement beyond reading the tree at
   `26d12d3`.** Every MEASURED claim above is a claim about what a file says, not about what the
   simulation does.

**WHAT THIS DOCUMENT DID NOT DO.** No system designed, no table or row proposed, no schema
touched, no conflict reconciled, no status changed, no milestone allocated, no ruling made or
implied. No production code, data file, golden, corridor, quarantine or test was touched. No
existing document was edited.
