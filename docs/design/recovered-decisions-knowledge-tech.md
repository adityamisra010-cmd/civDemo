# RECOVERED DECISIONS — KNOWLEDGE · RESEARCH · TECHNOLOGY · BREAKTHROUGHS · CAPABILITY · DIFFUSION

**ARCHAEOLOGY, NOT DESIGN.** This document recovers decisions that already exist in the
repository. It designs nothing, implements nothing, rules nothing, and reconciles nothing.
No production code, schema, golden, corridor, quarantine or ratified/frozen document was
touched. Created under the Director's 2026-09-19 mandate, PART 3 (decision-recovery lane).

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `9f6c6ae`, working
tree clean. Every `file:line` below was re-read on that tree; where a source document cites a
line that has since moved, the drift is recorded rather than silently corrected (GOV-4: the
tree wins on facts, the ruling stands on rulings).

**Author authority:** none. The DIRECTOR IS ChatGPT. Where two documents disagree, both rows
are recorded and a CONFLICT row names the pair. Nothing here picks a side.

**Label key** (mandate labelling rule — every substantive claim in the prose carries exactly one):
RATIFIED · MEASURED · PROPOSED · INFERRED · DIRECTOR DECISION REQUIRED.
**Status key** (table column — taken from the source document, never from this author's judgement):
RATIFIED · OPEN · DEFERRED · REJECTED · DIRECTOR DECISION REQUIRED.

**Counts:** 63 decision rows (K-01 … K-63) · 11 conflict rows (C-01 … C-11).

---

## §1 THE LAW LAYER — WHAT CAPABILITY MAY NOT BE

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-01 — Law 4, no calendar gates.** *"**No calendar gates.** Capability derives from computed state; era labels are descriptive output only."* | `docs/civ-sim-architecture-v3-outline.md:22` (frozen Spine S1); restated `CLAUDE.md:19` | RATIFIED | The root constraint of this whole cluster. An era label is an OUTPUT of the knowledge system, never an input to it. Anything that reads a date, turn number or era name to decide capability is illegal by construction. |
| **K-02 — Law 2, mechanisms over modifiers.** *"coefficients inside resolution equations are fine; free-floating permanent buffs are banned."* | `CLAUDE.md:17` | RATIFIED | "Research completes → +10% yield forever" is the banned shape. A technology must change **which mechanism runs**, or a coefficient **inside** a resolution equation, never a standing buff attached to an owner. |
| **K-03 — Law 6, isolation.** *"systems never reference each other — only `State` and `Kernel`. Communication is through tables and events."* | `CLAUDE.md:21`; reaffirmed `docs/d042-empire-and-player-control-addendum.md:134-140` | RATIFIED | The reason a coordinating capability owner is illegal (K-26). Knowledge, economy, military and governance may depend on each other conceptually and must still communicate only through published state. |
| **K-04 — D-040 B3: NO TECHNOLOGY UNLOCK; Civ's research-to-unlock shape is REJECTED.** *"The director raised Civ's 'research sailing to cross to another continent' as the reference and it is **REJECTED in that form**: a tech-tree node opening sea travel is a calendar gate wearing a tree."* | `docs/d040-discovery-and-control.md:59-64` | RATIFIED (the rejection itself is the ruling) | The tech tree as an unlock graph is closed permanently. Any future "node → capability" edge reopens a frozen director ruling and needs a CR. |
| **K-05 — D-040 B3: the sanctioned form is a conjunction of computed preconditions.** *"sea travel becomes possible when the conditions for boats exist — a coastal settlement, timber, and craft capacity — in the same shape as class emergence … A landlocked polity never develops it; a coastal one does; nobody schedules either."* | `docs/d040-discovery-and-control.md:66-70` | RATIFIED | The positive half of B3. A capability is a predicate over computed world state. Prerequisites are **conjuncts**, not edges. |
| **K-06 — the "landlocked" clause is illustrative, not a conformance test.** *"**B3 does NOT require that every capability predicate exhibit permanent structural exclusion.** … it is **illustrative of what the form makes possible**, not a conformance test every predicate must pass."* | `docs/adr/cr-007-b3-exemplar-reconciliation.md:83-89` | RATIFIED — as CR-007's own resolution (*"RESOLVED WITHOUT A NEW RULING"*, `:3-4`); see **C-01** for its contested status elsewhere | Permanent exclusion is permitted and valuable, not mandatory. A designer may not be forced to invent a structural exclusion for every capability, and may not claim B3 demands one. |
| **K-07 — D-040 B1: the map is DISCOVERED, not revealed.** *"A polity's knowledge of the world is a **computed extent**, not a visibility flag over pre-known terrain. Land that has never been reached is not dark — it is **absent from what the polity can act on**."* | `docs/d040-discovery-and-control.md:37-39` | RATIFIED | Geographic knowledge is the first knowledge quantity the project ruled on, and it is a computed extent. Settlement, claim and trade operate only within known extent. |
| **K-08 — D-040 B2: discovery is D-039's reconnaissance at a different scale; do not invent a second exploration system.** *"**Those clauses govern discovery too.** Strategic discovery is that same capability walked further and returned with a map. **Do not invent a second exploration system.**"* | `docs/d040-discovery-and-control.md:41-50` | RATIFIED | Diffusion-of-geographic-knowledge reuses D-039 B1–B5. The one difference is ruled: *"reconnaissance reports a lagging position of a moving thing; discovery reports a permanent fact about the land. Once known, land stays known."* (`:51-54`) — i.e. geographic knowledge is **monotone by ruling**, unlike other knowledge. |
| **K-09 — D-039 A1: what varies across eras is KNOWLEDGE and LATENCY, not verbs.** *"D-011 §1 fixes the order verbs as constant across all eras — 'units change, verbs don't.' That stands. What changes is WHAT THE COMMANDER KNOWS and HOW FAST ORDERS ARRIVE."* | `docs/d039-command-fog-and-siege.md:6-11` | RATIFIED | Era-varying warfare is already ruled to be (a) unit DATA and (b) information quantities — not new mechanisms invented by a technology system. |
| **K-10 — D-039 A5: Law 4 binds all of command capability; era is a consequence, never an input.** *"Command capability derives from computed state — literacy, road and signal infrastructure, institutions, general competence, force size. **Era is a consequence, never an input.**"* | `docs/d039-command-fog-and-siege.md:37-40` | RATIFIED | Names four knowledge-cluster inputs by name — **literacy, infrastructure, institutions, competence** — none of which exists as a published variable today (see **K-45**, **C-07**). |
| **K-11 — D-039 A2: three separable information channels.** *"POSITION … STRENGTH … OUTCOME — what happened after an order executed. IMPROVES LITTLE ACROSS ERAS. … Model them as three quantities, not one information score."* | `docs/d039-command-fog-and-siege.md:13-20` | RATIFIED | Precedent: information/knowledge is modelled as **several quantities with different dynamics**, explicitly not one "science score". |
| **K-12 — D-039 B1: information quality is bought with whatever is scarce in that era, never with money.** *"Money does not exist before its milestone … the ancient cost is FOOD AND PEOPLE."* | `docs/d039-command-fog-and-siege.md:44-49` | RATIFIED | The costing precedent for any knowledge-production mechanism: the input is people and food (opportunity cost inside the shipped population system), not currency. |
| **K-13 — D-041: an ACCUMULATED STOCK whose input is time is legal when it is a lever.** *"**Control is COMPUTED each turn** … **Attachment is an ACCUMULATED STOCK** held by a population, **slow to build and slow to lose**"*; *"That is what makes attachment a LEVER rather than a COEFFICIENT."* | `docs/d041-attachment.md:34-36`, `:64-66`; record header *"Extends **D-040**"* `:3` | RATIFIED | The decisive precedent that accumulation as such is not what B3 bans. The fence it implies: the stock must **feed behaviour continuously** and be a lever, never a boolean grant. |

---

## §2 THE SHIPPED MECHANISM — THE D-020 PREDICATE DSL OVER PUBLISHED VARIABLES

*The capability seam in this project is not a proposal. It ships. The rows below describe it as
it actually is on `9f6c6ae`.*

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-14 — D-020 OPENED at D-018: emergence predicates need a format.** *"**D-020 opened:** emergence/threshold predicates need a format — recommendation: a tiny comparison DSL … parsed and validated at load; no scripting engine (mod platform stays deleted). Close at M2 spec."* | `docs/d018-classes-and-needs.md:70` | RATIFIED (opened; closed at K-15) | The DSL was always intended as **data**, never a scripting layer. A mod/script engine is out of scope by an earlier deletion. |
| **K-15 — D-020 CLOSED at the M2 spec.** *"**D-020 CLOSED — Emergence predicate DSL.** Comparisons (`> < >= <= ==`) and boolean ops (`&& \|\| !`) over **registered variables** that simulation systems publish per settlement … **No functions, no arithmetic in v1** (queue if needed). Parser lives in Sim.Core; predicates are data."* | `docs/m2-spec.md:8`; closure authority `docs/spine-s8-governance-freeze.md:22` (*"closing an open decision at its named spec is not an amendment"*) | RATIFIED | The grammar is **closed**. No arithmetic means a predicate cannot compute a rate or a ratio inline — any new quantity must be **published as a variable by the system that owns it**. That is the single most load-bearing shape constraint on a future knowledge system. |
| **K-16 — the grammar, as implemented.** `orExpr := andExpr ('\|\|' andExpr)* · andExpr := unary ('&&' unary)* · unary := '!' unary \| '(' orExpr ')' \| comparison · compare := operand ('>' '<' '>=' '<=' '==') operand · operand := variableName \| numberLiteral (invariant culture)`; *"No functions, no arithmetic (v1 …). Parsed ONCE at config load with loud rejection."* | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` | RATIFIED (shipped contract; code `_doc` counts as a decision record) | Determinism-safe by construction: pure tree walk, ordinal strings, invariant parsing. A future scope widening inherits this or breaks Law 5. |
| **K-17 — predicates evaluate against the PREVIOUS turn's published rows (one-turn lag).** *"The parsed tree is immutable and evaluates against a per-settlement variable reader (PREV turn's rows — one-turn lag, §3.2)."* | `Sim.Core/Systems/ClassMobility/Predicate.cs:22-25`; `Sim.Core/State/WorldState.cs:388-394` | RATIFIED (shipped contract) | Every capability answer is one turn stale by design. Any "rapid breakthrough" semantics must be stated in **turns**, with this lag counted. |
| **K-18 — the variable registry is CODE-side; names never live in sim rows; entries are delivered incrementally (D-027).** *"the CODE-side mapping between the names predicates use and the integer ids stored in VariableRow (ADR-001: names never live in sim rows). Delivered incrementally per D-027 — **an entry exists only once a system actually publishes it.**"* | `Sim.Core/State/Variables.cs:3-8` | RATIFIED (shipped contract) | A knowledge system does not "add a variable to a registry": it must **publish** the quantity each turn from a system that owns it. Registration order is the deterministic iteration order. |
| **K-19 — REGISTRY LAW: scale invariance.** *"published variables that are all RATIOS are SCALE-INVARIANT … **ANY emergence predicate that needs scale sensitivity MUST publish an absolute quantity, not another ratio.**"* Learned from a measurement: *"identical at every size (measured 3.5 ± 0.1 across all twelve)"*. | `Sim.Core/State/Variables.cs:24-32` | RATIFIED (shipped law, stated in code) | Binding on any knowledge/literacy variable. A "literacy share" alone cannot differentiate civilizations; an absolute quantity must accompany it. |
| **K-20 — the emergence LATCH: `ClassStateRow.Active` is hysteresis, serialized.** *"Inactive + emerge true → Active = 1; active + recede true → Active = 0. The latch (persistent, serialized) IS the hysteresis … **Recede absent = never recedes.**"* | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; row `Sim.Core/State/WorldState.cs:396-403` | RATIFIED (shipped contract) | The latch records **current satisfaction under hysteresis**, not history. Monotonic acquisition exists only as the special case of an omitted `recede` — a data choice, not a mechanism property. |
| **K-21 — CORRECTION (recorded, not rewritten): the latch was misdescribed in two prior records as *"records that a predicate has fired"*.** *"**The shipped latch records CURRENT satisfaction under hysteresis, not history** … Any design relying on capability outliving its preconditions must say so explicitly and justify it."* | `docs/milestone-architecture-governance.md:242-251` (§6.2b); correction notice `docs/capability-architecture-decision.md:20-23`; the misdescriptions are `docs/m5-roadmap-dependency-audit.md:220-221` and `docs/capability-architecture-decision.md:128-131` | MEASURED | A correction recorded in two documents about what two other documents say; the SHIPPED semantics are K-20, read from `ClassMobilitySystem.cs:28-33`. No ruling is cited, and one of the carrying records (`docs/capability-architecture-decision.md`) is itself under a CORRECTION NOTICE. Kills the casual argument that "a government change transforms rather than deletes acquired capability, because a latch remembers". It does not remember. |
| **K-22 — recipe availability (`requires`) is the SECOND live consumer, and it is a KNOWLEDGE GATE by name.** *"an optional D-020 availability predicate ('requires') — **a knowledge gate over published variables, never a calendar date (law 4)**"*; *"Requires is an OPTIONAL D-020 availability predicate over published variables — a knowledge gate, never a calendar date (law 4)."* | `Sim.Data/content/goods.json:3`; `Sim.Core/Systems/GoodsConfig.cs:32-35`, field `:52`; spec origin `docs/m3-spec.md:26` (*"a per-recipe era/knowledge gate expressed in the D-020 DSL"*) | RATIFIED (shipped data + code contract) | **The knowledge gate over recipes already exists.** A future technology layer widens the scope of this device; it does not invent it. |
| **K-23 — the recipe gate is PURE-DERIVED: no stored state, no hysteresis.** Evaluated per settlement per turn against PREV variables: `available[r] = _requires[r] is not { } predicate \|\| predicate.Evaluate(varId => ReadVariable(prev, settlement, varId));` | `Sim.Core/Systems/Production/ProductionSystem.cs:394-396`; parsed once at construction `:105-108`; validated at load `Sim.Core/Systems/GoodsConfig.cs:241-245` | RATIFIED (shipped, MEASURED from source) | The tree ships **two seams with different storage answers** — a serialized latch and a pure predicate. Which one a future capability uses is an open design choice (**K-32**). |
| **K-24 — the live gated recipes.** `bronze-casting` and `toolmaking`, both `"requires": "artisan_share > 0.05"`. | `Sim.Data/content/goods.json:136` + `:156`, `:159` + `:175` | RATIFIED (shipped data) | An **economic/social** predicate gates a **technological** recipe today. Anyone asserting "knowledge precedes economy" has to reckon with the shipped direction of the gate. |
| **K-25 — the live emergence predicates.** Artisans: `"emerge": "food_surplus_ratio > 1.3 && population > 520"`, `"recede": "food_surplus_ratio < 1.1"`. Merchants (T4.11): `"emerge": "trade_volume > 200 && population > 520"`, `"recede": "trade_volume < 50"`. | `Sim.Data/content/sim.json:169-171`, `:176-178` | RATIFIED (shipped data; TUNE values are LIVING under `docs/spine-s8-governance-freeze.md:20`) | The merchant `_doc` states the governing reason for the latch explicitly: *"what turns this spiky signal into a durable class is the D-020 HYSTERESIS LATCH … a single threshold would have oscillated on and off."* |

---

## §3 D-042 — THE DIRECTOR RULING THAT SETTLED THE KNOWLEDGE MODEL

*D-042 is a director design ruling (`docs/d042-empire-and-player-control-addendum.md:3`) and is
the newest and most authoritative document in this cluster. Its §15 explicitly lists what it
closes "so these are not re-asked".*

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-26 — REJECTED: a universal `CapabilitySystem` God object.** *"**Do not create a universal God system such as a `CapabilitySystem`** that owns every capability or coordinates every domain."* Repeated in the anti-pattern list: *"a universal `CapabilitySystem` God object"*. | `docs/d042-empire-and-player-control-addendum.md:141-142` (§7.3); `:196-203` (§12) | **REJECTED** | **See §5 for the full rejection record — reason, blast radius and revisit conditions.** |
| **K-27 — D-020 is the capability FOUNDATION.** *"**The existing D-020 predicate machinery is the foundation** for future capability evaluation."* | `docs/d042-empire-and-player-control-addendum.md:146-147` (§8.1); restated in the settled-questions table `:295` | RATIFIED | Closes "do we build a capability mechanism?" — we widen the one that ships. |
| **K-28 — the capability chain is ruled.** *"Capability is **distinct from** raw state, knowledge, research activity, technology, policy, institution and action"*, with the chain `State → Published Variables → Predicates → Capabilities → Available Actions`. | `docs/d042-empire-and-player-control-addendum.md:149-152` (§8.2) | RATIFIED | Eight distinct concepts, ruled non-interchangeable. Collapsing "knowledge" and "capability" into one stock is now a ruling violation, not a taste question. |
| **K-29 — capability evaluation must distinguish SCOPE.** *"**Capability evaluation must be able to distinguish SCOPE — Empire-level and Settlement-level, with unit/action scope where required.**"* | `docs/d042-empire-and-player-control-addendum.md:155-156` (§8.3) | RATIFIED | The shipped `VariableRow` is `(SettlementId, VarId, Value)` — settlement-scoped only (`Sim.Core/State/WorldState.cs:394`). Empire-scope evaluation therefore has no substrate today. **This is the cluster's hard blocker.** |
| **K-30 — capability consumes computed state, never calendar unlocks.** *"Capability definitions **consume computed state, never hard-coded calendar unlocks.**"* | `docs/d042-empire-and-player-control-addendum.md:157-158` (§8.4) | RATIFIED | Restates Law 4 inside the capability layer. |
| **K-31 — the knowledge/technology/capability system is NOT implemented by D-042.** *"**Do not implement the knowledge/technology/capability system as part of this record.**"* | `docs/d042-empire-and-player-control-addendum.md:159-160` (§8.5) | DEFERRED (explicit) | D-042 gives constraints, not a design. The design phase is still owed. |
| **K-32 — knowledge is DISTINCT from technology and capability.** *"**Knowledge is distinct from technology and capability.**"* | `docs/d042-empire-and-player-control-addendum.md:164` (§9.1) | RATIFIED | Three separate objects. A single "science points → tech" pipeline conflates two of them. |
| **K-33 — knowledge is ultimately EMPIRE-scoped.** *"Knowledge is ultimately **Empire-scoped**, while produced/applied through settlements, institutions, people and research activities."* | `docs/d042-empire-and-player-control-addendum.md:165-166` (§9.2) | RATIFIED | Production is local; the stock is Empire-level. Directly requires K-29's scope substrate. |
| **K-34 — research activities proceed in PARALLEL.** *"**Research activities proceed in PARALLEL.**"* + anti-pattern *"a rigid one-at-a-time research queue"*. | `docs/d042-empire-and-player-control-addendum.md:167` (§9.3); `:196-203` (§12); settled-questions `:293` | RATIFIED | Settles the M5 placeholder's item 8 in the affirmative. A Civ-style single queue is a ruled anti-pattern. |
| **K-35 — knowledge generation is an ALLOCATABLE FLOW.** *"**Knowledge generation is a resource/flow the player ALLOCATES among concurrent research activities.**"* | `docs/d042-empire-and-player-control-addendum.md:168-169` (§9.4) | RATIFIED | Knowledge is a **flow** the player directs — the allocation is the player's decision surface, not a completion timer. |
| **K-36 — unallocated knowledge ACCUMULATES AS A RESERVE.** *"**Unallocated knowledge ACCUMULATES AS A RESERVE rather than disappearing**, and the player may deliberately hold a non-optimal reserve."* | `docs/d042-empire-and-player-control-addendum.md:170-171` (§9.5); settled-questions `:292` | RATIFIED | **This answers the Q1 accumulation blocker YES.** CR-007's own §6 correction notice says so: *"So the accumulation question is **answered YES** and must not be re-asked"* (`docs/adr/cr-007-b3-exemplar-reconciliation.md:178-188`). |
| **K-37 — knowledge may arise through many named routes, including DIFFUSION.** *"Knowledge may arise through deliberate research, practical discovery, diffusion, external exchange, espionage, or other explicitly defined mechanisms; open borders and foreign institutions may contribute."* | `docs/d042-empire-and-player-control-addendum.md:172-174` (§9.6) | RATIFIED | Diffusion and circumstantial discovery are ruled IN as first-class sources, not as later bolt-ons. "Practical discovery" is the repository's own term for what a brief might call serendipity. |
| **K-38 — do NOT collapse these concepts into a single rigid technology tree.** *"**Do not collapse these concepts into a single rigid technology tree.**"* | `docs/d042-empire-and-player-control-addendum.md:175` (§9.7) | RATIFIED | The guard that keeps K-35/K-36's accumulation compatible with B3 (K-04). |
| **K-39 — REJECTED: calendar-date technology unlocks where computed predicates are intended.** Anti-pattern list: *"calendar-date technology unlocks where computed predicates are intended"*. | `docs/d042-empire-and-player-control-addendum.md:196-203` (§12) | **REJECTED** | Third independent statement of Law 4 inside this cluster (K-01, K-30, K-39). |
| **K-40 — government influences capability THROUGH state and computed conditions; no bonus bundles.** *"Government may influence available decisions, policies, capabilities, economic behaviour, military organisation, knowledge production and institutions — **through state and computed conditions.**"* + *"**Avoid hard-coding governments as bundles of direct bonuses.** (This aligns with Law 2…)"* | `docs/d042-empire-and-player-control-addendum.md:87-91` (§5.2, §5.3) | RATIFIED | The research/government orthogonality the M5 placeholder sketches (K-53) has a ratified mechanism-side constraint here. |
| **K-41 — a government transition PRESERVES unrelated accumulated Empire state.** *"A government transition **preserves unrelated accumulated Empire state** unless a specific mechanic explicitly changes it."* | `docs/d042-empire-and-player-control-addendum.md:92-93` (§5.4) | RATIFIED | Ruled for **state**. It does **not** rule that a capability outlives its preconditions — the latch does not provide that (K-20/K-21). The join between the two is unresolved (**G-06**). |
| **K-42 — research allocation is a PERSISTENT DIRECTIVE, not a per-turn order.** *"**Persistent directives:** research allocation, policy allocation … **A persistent directive is state describing a desired ongoing configuration — it is NOT re-issued every turn to keep working.**"* | `docs/d042-empire-and-player-control-addendum.md:99-106` (§6.2) | RATIFIED | Names the storage shape of the player's research decision: a directive row in state, carried across turns. |
| **K-43 — player and AI share ONE pathway; no separate physics.** *"**The simulation must not contain separate gameplay physics for human- and AI-controlled Empires.**"* | `docs/d042-empire-and-player-control-addendum.md:130-132` (§6.6); diagram `:112-128` | RATIFIED | AI research strategy (M5 placeholder item 19) must issue the same directives as the player. |
| **K-44 — future mid-turn control must preserve deterministic replay; CR-006 is NOT resolved.** *"Any future mechanism **must preserve deterministic replay** and be an **explicit temporal/control architecture — never an ad hoc pause inserted into individual systems.** (Consistent with the open CR-006; this record does not resolve it.)"* | `docs/d042-empire-and-player-control-addendum.md:188-194` (§11) | RATIFIED (constraint) + the underlying question OPEN | Bounds "rapid breakthrough" semantics: a discovery may not pause a system mid-turn. |

---

## §4 RATIFIED DEPENDANTS — MECHANISMS THAT ALREADY REQUIRE KNOWLEDGE QUANTITIES

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-45 — D-018: the Intelligentsia emerges on literacy and education institutions.** Class table row: *"**Intelligentsia** \| mixed \| **literacy share + education institutions > threshold** \| agitation …"* | `docs/d018-classes-and-needs.md:26`; D-018 frozen at `docs/spine-s8-governance-freeze.md:16` | RATIFIED (frozen D-decision) | A frozen class emergence predicate names two knowledge variables that **do not exist in the registry** (see MEASURED note below, and **C-07**). |
| **K-46 — D-018: the Prospects need is education-driven.** *"**Prospects** \| education access, mobility openness, growth trend — *children's outlook*"* | `docs/d018-classes-and-needs.md:42` | RATIFIED (frozen) | Education access feeds the needs/grievance engine that already ships. |
| **K-47 — D-018: rising expectations REPLACE era tables, and scale with literacy.** *"**Rising expectations (replaces era tables).** Salience of Comfort, Liberty, and Prospects scales with the bucket's **literacy, urbanization, and media exposure** — computed state (Law 5)."* | `docs/d018-classes-and-needs.md:48` | RATIFIED (frozen) | The project already **deleted an era table** in favour of a literacy-driven computed quantity. Precedent for how every other era table should die. |
| **K-48 — D-018: mobility into the Intelligentsia runs through education access.** *"…→Intelligentsia via education access; … mobility openness itself feeds the Prospects need."* | `docs/d018-classes-and-needs.md:63` | RATIFIED (frozen) | Education is a **conserved-people flow** (`Ledger.Transfer`), not a modifier. |
| **K-49 — D-021: education is a flow with a lag, and a deliberate gamble.** *"**Education is a flow with a lag:** literacy investments change bucket composition over sim-decades and — via rising expectations (frozen) — raise liberty/prospects salience. Educating your population remains a deliberate gamble; that tension is kept, not patched."* | `docs/d021-stability-doctrine.md:43`; the lever statement `:39` (*"→Intelligentsia (education access — *the* lever: school institutions raise the flow and feed the Prospects need)"*) | RATIFIED | Ruled dynamics for the education mechanism: slow, composition-changing, and destabilising before it pays. |
| **K-50 — M4 deferred the whole cluster BY RULING.** *"**DEFERRED BY RULING** — Knowledge/technology (**no tech tree, no diffusion, no era system**), money and finance, full armies and the AutoResolver, M5 politics."* | `docs/m4-exit-inventory.md:226-228` | DEFERRED | The M4 baseline contains no knowledge system at all; nothing in the cluster is owed backwards compatibility with shipped mechanics. |

> **MEASURED (this pass, tree `9f6c6ae`).** The published-variable registry ships **four**
> variables — `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`
> (`Sim.Core/State/Variables.cs:93`, ids at `:36-38`, `:60`, `:90`). **None is a literacy,
> education, institution or knowledge variable.** K-45, K-46, K-47, K-48, K-49 and K-10 are all
> ratified dependants of quantities that do not exist. `docs/capability-architecture-decision.md:224-228`
> records this gap but states the count as *"only three variables"* — that count predates the
> T4.11 `trade_volume` registration and is stale on this tree (**C-08**).

---

## §5 THE REJECTED DECISIONS — RECORDED WITH THEIR REASONS

*Mandate requirement (vi): the rejected universal CapabilitySystem is the most important
instance in the tree and is recorded here with the precise reason.*

### 5.1 REJECTED — a universal `CapabilitySystem` that owns every capability (K-26)

**What was proposed.** `docs/capability-architecture-decision.md:194-206` (§5) proposed a
capability seam shaped **`CapabilityState(scope, domain, capability)`** — *"scope generalized,
not fixed to 'civ'"*, over a relation `(scopeKind, scopeId, varId, value)`, with `domain`
carrying no mechanism so that *"no tree; domain lattice lite"* becomes expressible as parallel
independent lattices rather than one graph.

**The rejection, verbatim** (`docs/d042-empire-and-player-control-addendum.md:141-142`, §7.3):

> *"**Do not create a universal God system such as a `CapabilitySystem`** that owns every
> capability or coordinates every domain."*

and again as an anti-pattern (`:196-203`, §12): *"a universal `CapabilitySystem` God object"*.

**THE PRECISE REASON — it is Law 6 (isolation), not taste.** §7 is titled *"STATE-MEDIATED
DEPENDENCY (reaffirms Law 6)"* and gives the reasoning in the two clauses above the
prohibition (`:134-140`):

> *"**Gameplay interdependence is allowed; direct code coupling is not.** Systems communicate
> through World State and kernel contracts, never by calling sibling domain systems."*
> *"Economy may depend *conceptually* on knowledge, government, military, transport and
> institutions — **that never justifies a sibling call.**"*

A system that *owns every capability* must be called by, or must call, every domain — which is
the sibling coupling Law 6 forbids. The same record forbids the analogous Empire God object at
§3.2 (`:60-62`) and §12, so the rejection is one instance of a general anti-pattern, not a
one-off objection to this design.

**WHAT SURVIVES THE REJECTION.** D-042 §14.5 (`:276-282`) records the disposition explicitly:

> *"The seam is conformant **only** as a *shared predicate grammar consumed independently by
> each domain system* — the shape D-020 already has — and **not** as a coordinating owner."*

The author's own document carries the same constraint notice at
`docs/capability-architecture-decision.md:165-174`: *"the `CapabilityState(scope, domain,
capability)` notation below must be read as **a way of asking**, never as **a system that
answers for everyone**."* The **scope** half of the proposal was independently **ratified** by
§8.3 (K-29), and the **foundation** half by §8.1 (K-27).

**WHAT WOULD HAVE TO CHANGE FOR IT TO BE REVISITED.** *(INFERRED from the governance
procedure, not stated in any document.)* D-042 is a director design ruling of the exempt
document class (S8 §4). Reinstating a coordinating capability owner would require (a) a
Contradiction Report under `docs/spine-s8-governance-freeze.md:35-37` showing an internal,
empirical or law conflict — *"a better idea … is not a contradiction"* (`:31`) — and (b) a
director ruling amending D-042 §7.3 and §12, which would also have to dispose of Law 6
(`CLAUDE.md:21`), since §7.3 is derived from it. **No such conflict is recorded anywhere in
the tree today.** The cheaper path, and the one the tree already sanctions, is the shared
grammar plus a scope key — which needs no amendment at all.

### 5.2 The other recorded rejections in this cluster

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-51 — REJECTED: "the capability seam is new work that must be built."** *"**'§4.1 the capability seam — the anti-retrofit device' proposed as new work. It already ships** (D-020 DSL, two consumers). The work is scope widening."* | Proposal at `docs/m5-roadmap-dependency-audit.md:197-211`; rejection by its own author at `docs/capability-architecture-decision.md:356-358` (§12.2) | **REJECTED** | Revisit condition: none — it is a factual correction, and the shipped seam is at K-15/K-22. A packet proposing to "build the seam" is proposing a rebuild. |
| **K-52 — REJECTED/WITHDRAWN: "B3's own sanctioned exemplar fails B3's own test" (BLOCKING contradiction #1).** *"**That finding is withdrawn — it rested on a misreading, identified in §4.1.** The underlying measurements were correct; the inference from them was not."* | Claim at `docs/milestone-architecture-governance.md:45-79`; withdrawal at `docs/adr/cr-007-b3-exemplar-reconciliation.md:8-12`, `:95-111`; marked in place at `docs/milestone-architecture-governance.md:17-43` | **REJECTED** (withdrawn by its author) | The measurements stand (`sim.json:171` TUNE note; `Variables.cs:24-32`). What falls is the inference. Verdicts #1, #6 and #10 of the adversarial pass *"lose their principal supporting argument"* (`cr-007:272-274`). Revisit would require showing that *"it"* in B3 refers to the artisan class, which §4.1 refutes on the text. |
| **K-53 — REJECTED: Model A (stock → project → completion → unlock).** The model table marks Model A B3-compatible = **NO**, *"is one"* (a hidden tree), progress-bar = yes, circumstantial discovery = *"lost"*; *"**Reject A outright.**"* | `docs/capability-architecture-decision.md:150-159` (§4.2) | **REJECTED — but by an UNVERIFIED recommendation**, not a ruling; the same section is headed *"RECOMMENDATION UNVERIFIED"* (`:115`, `:159`) | **DIRECTOR DECISION REQUIRED** to convert into a ruling. Note the adjacent ratified statements that point the same way: K-04, K-38, and D-042 §12's ban on a one-at-a-time queue. |
| **K-54 — WITHDRAWN: "the food investigation found an emergence predicate reading a post-destruction stock."** *"**§7.3 does not say that** — it concerns *migration's* food attractiveness, a **reader**, not an emergence predicate … **Withdrawn.**"* | `docs/m5-roadmap-dependency-audit.md:360-365` (the claim); `docs/capability-architecture-decision.md:358-362` (§12.3, the withdrawal) | **REJECTED** (withdrawn) | Removes the only recorded evidence that the shipped emergence predicates read corrupted state. Any future audit of the predicates starts from zero findings, not one. |

---

## §6 PLACEMENT, SEQUENCING AND OWNERSHIP — WHAT IS STILL OPEN

| Decision | Source (file:line) | Status | Implication for future architecture |
|---|---|---|---|
| **K-55 — Spine: Knowledge & diffusion is M6, "no tree; domain lattice lite".** *"\| Knowledge & diffusion \| M6 \| T2 \| **no tree; domain lattice lite** \|"* and *"**M6 — Knowledge & divergence.** Domain lattice lite, diffusion, **computed era labels**; map shows civilizations pulling apart in time."* | `docs/civ-sim-architecture-v3-outline.md:84`, `:110` | RATIFIED (frozen Spine) but STALE by one milestone — see **C-02** | Three cluster terms are fixed here: **domain lattice lite**, **diffusion**, **computed era labels**, and the divergence goal (*"civilizations pulling apart in time"*). |
| **K-56 — D-011 §6 resequence: knowledge is M7 ("was M6"), politics M8.** *"\| M7 \| knowledge & divergence \| was M6 \|"* | `docs/d011-battle-layer-addendum.md:62-63`; used by `docs/d040-discovery-and-control.md:158-162` | RATIFIED (the operative order) | The number to use is **M7**. Any document saying M6 is reading the unpatched Spine table. |
| **K-57 — D-040 D3: map extent and discovery land at M7 (knowledge).** *"**M7 (knowledge)** — map extent and discovery mechanics."* | `docs/d040-discovery-and-control.md:158-162` | RATIFIED | Discovery is knowledge-milestone work, not transport-milestone work — except for sea travel as an **edge type**, which is the transport packet's (K-60). |
| **K-58 — GOV-2 §1c: the Spine's system inventory is STALE BY ONE MILESTONE from M6 onward, and reconciliation is REQUIRED.** Verified row: *"\| Knowledge & diffusion \| M6 (`:84`) \| M7 ('was M6', `d011:55`) \| stale by one \|"*; *"1c's reconciliation remains REQUIRED."* | `docs/m4-pre-spec-dependencies.md:143-153`, `:139-141` | OPEN | A ratified, unexecuted documentation obligation sitting directly on this cluster's milestone row. |
| **K-59 — CR-005: placing Research/Technology/Institutions at M5 conflicts with the frozen milestone order.** *"**Status: OPEN — awaiting director ruling. No frozen document has been edited.**"* Three options: A RENUMBER · B INSERT · C architecture-only packet inside M5. Recommendation: *"**Option C**, unless the director specifically wants the fiscal system to move."* | `docs/adr/cr-005-m5-research-technology-institutions-placement.md:3`, `:93-118`, `:140`; queue pointer `docs/queue.md:1187-1194` | **OPEN** | The milestone that owns this cluster is **not decided**. CR-005 itself states the real question: *"If the director's intent is that M5 must be substantively ABOUT learning and change … then Option B is the honest reading and C is a half-measure."* (`:149-152`). |
| **K-60 — the sequencing directive itself: technology must be DESIGNED before its dependants.** *"**TECHNOLOGY MUST BE DESIGNED BEFORE DOWNSTREAM SYSTEMS THAT DEPEND ON TECHNOLOGICAL CAPABILITIES.**"* — a later warfare or economic packet *"must not independently invent its own technology prerequisites"*. | `docs/m5-research-technology-institutions-placeholder.md:27-37`; recorded as the director's reason in `docs/adr/cr-005-…:53-58` | **OPEN** (the directive is recorded; its milestone expression is what CR-005 holds open) | This is the anti-retrofit rule the whole cluster exists to serve. It is a statement about **design order**, not build order (CR-005 §5). |
| **K-61 — the M5 placeholder is explicitly NOT RATIFIED and NOT a spec.** *"**THIS IS NOT A SPEC AND NOTHING HERE IS RATIFIED.** … **Milestone placement is NOT settled.** … **A packet written against this document without a ratified spec would be implementing ahead of the spec, which CLAUDE.md forbids.**"* The deliberately ABSENT list: science formulas · knowledge formulas · population exponents · research costs · technology prerequisites · government modifiers · institution bonuses · foreign-exchange coefficients · research slot counts · technology eras · military technology values · economic technology values. | `docs/m5-research-technology-institutions-placeholder.md:3-21` | **OPEN** | Everything in its §3 scope list (items 1–24) is **recorded ownership, not design**. Nothing there may be cited as a decision. |
| **K-62 — the M5 placeholder's shape claim: "THE ARCHITECTURE IS A CHAIN, NOT A TREE."** *"Explicitly **NOT** 'technology = a tree where the player unlocks bonuses'"*, with the chain population → human intellectual potential → education/institutions → science and knowledge generation → research → technologies/institutions → changed civilization capabilities → changed behaviour; and two orthogonal axes: *"**Research determines which capabilities can become POSSIBLE.** … **Government determines which institutional pathways are naturally AVAILABLE or EFFICIENT.**"* | `docs/m5-research-technology-institutions-placeholder.md:39-62` | **OPEN** (unratified document) | The two-axis split is independently supported by ratified material — D-042 §5.2 (K-40) and §9 (K-32…K-38) — but the **chain itself** has never been ruled on. Note the audit's refutation of the chain's middle link (**C-05**). |
| **K-63 — the M5 placeholder's own load-bearing open question.** *"Does technology alter **mechanisms** (law 2 compliant) or apply **modifiers** (law 2 forbids free-floating permanent buffs)? **This is the single most load-bearing question in the packet** — 'unlock a bonus' is precisely the shape law 2 bans, and §2 above is the answer's outline, not the answer."* | `docs/m5-research-technology-institutions-placeholder.md:117-137` (§5 Q6; siblings Q1–Q7) | **OPEN** | Its §5 also opens: science vs knowledge one stock or two (Q1) · institution as stock/structure/row-with-lifecycle (Q2) · does knowledge decay, and is decay a named Ledger sink (Q3) · brain drain must move people through the existing migration/`Ledger.Transfer` machinery (Q4) · diminishing returns is a curve under CR-003 §5.1's derived-vs-chosen discipline (Q5) · AI research strategy must be deterministic and free of unordered iteration, law 5 (Q7). |

---

## §7 CONFLICT ROWS — TWO SOURCES, BOTH RECORDED, NEITHER RECONCILED

*Mandate rule (ii). Each row names the two sources. No side is picked and nothing is merged.*

| # | Conflict | Source A | Source B | Recorded status |
|---|---|---|---|---|
| **C-01** | **CR-007's own status.** CR-007 declares itself *"RESOLVED WITHOUT A NEW RULING — the alleged contradiction does not exist as stated"*. The routing document lists CR-007 among *"Open change requests awaiting director ruling"*, annotated *"B3 exemplar — headline withdrawn by the author; the record stands"*. | `docs/adr/cr-007-b3-exemplar-reconciliation.md:3-4` | `docs/current-state.md:368-370` | Both recorded. A self-declared resolution by the finding's own author is not a director ruling; whether CR-007 is closed is **DIRECTOR DECISION REQUIRED**. |
| **C-02** | **Which milestone owns knowledge.** Spine: *"Knowledge & diffusion \| M6"* / *"M6 — Knowledge & divergence"*. D-011 §6: *"\| M7 \| knowledge & divergence \| was M6 \|"*. | `docs/civ-sim-architecture-v3-outline.md:84`, `:110` (frozen) | `docs/d011-battle-layer-addendum.md:62` (ratified resequence) | Both recorded. GOV-2 §1c calls the Spine row *"stale by one"* and says reconciliation *"remains REQUIRED"* (`docs/m4-pre-spec-dependencies.md:143-153`) — the reconciliation has **not** been performed on this tree. Third party in the same dispute: `docs/capability-architecture-decision.md:378` lists it as MINOR conflict #9, *"known-stale"*. |
| **C-03** | **Era gates in ratified material vs Law 4 / D-040 B3.** Frozen D-011 gates capability on era labels: *"(**era-gated** additions: bombard, air strike, dig-in)"*, *"Later eras arrive as data + a few new verbs… gunpowder… industrial… modern…"*, *"\| M11+ \| era expansions \| each adds its battle-layer units/verbs as data \|"*. D-009/D-010 calls bridges and tunnels *"expensive, **era-gated**, terrain-crossing edges"*. Law 4 and B3 forbid the shape. | `docs/d011-battle-layer-addendum.md:13`, `:45`, `:66`; `docs/d009-d010-map-population-addendum.md:12` | `CLAUDE.md:19`; `docs/d040-discovery-and-control.md:59-64` | Both recorded, by three documents. D-040 flags the D-009/D-010 instance **against itself** and declines to rule: *"**It is not amended here** — D-040 amends nothing — but the transport packet will have to face it"* (`:223-227`). CR-007 §9 narrows the live part: *"The **D-011 military instance is not yet owned** — that is the genuinely open part"* (`:255`). **A CR-009 was recommended and NEVER WRITTEN** — `docs/current-state.md:13`: *"neither has ever existed on any branch"*. Status: **DIRECTOR DECISION REQUIRED, unowned.** |
| **C-04** | **Does knowledge precede advanced military?** The roadmap audit: *"**Knowledge at M7 already precedes both.** The stated sequencing goal is, on this axis, *already satisfied by the ratified order.*"* The adversarial pass: assumption #2 **FALSIFIED** — *"the ratified gate on advanced military is **an era label, not knowledge**"*, and *"military does **not** currently descend from capability at all — it descends from an **era label**."* | `docs/m5-roadmap-dependency-audit.md:41-45` | `docs/milestone-architecture-governance.md:110-121` (#2 FALSIFIED), `:139-153`, `:282-284` | Both recorded. CR-007 §10.2 leaves the falsification standing: *"#2 advanced military is era-gated in frozen D-011 … untouched; still a genuine Law 4 tension"* (`:281-282`). |
| **C-05** | **Does knowledge depend on money/taxation through institution funding?** The audit's chain: *"Institutions need funding; funding needs money and taxation; research needs institutions. **Knowledge is genuinely downstream of the governing loop**"*. The capability record refutes the link: *"GOV-2 §1a rules M5 taxes **in kind**, so institution funding never had to wait for currency. **The shipped proof is Housing** … **Corrected chain:** institutions → **in-kind upkeep** → M5 governing loop → M7 knowledge."* | `docs/m5-roadmap-dependency-audit.md:125-146` (§2) | `docs/capability-architecture-decision.md:210-222` (§6); the in-kind ruling itself `docs/m4-pre-spec-dependencies.md:33-41` (GOV-2 §1a) | Both recorded. A third document adds a countervailing direction: *"the economy funds institutions that produce knowledge that changes the economy. **A cycle has no 'precedes.'**"* (`docs/milestone-architecture-governance.md:155-162`, #3 FALSIFIED). |
| **C-06** | **Is an accumulating knowledge/science quantity permitted?** Recorded as the blocking open question in two records — *"**Q1 ruling: is an accumulating knowledge quantity permitted, and under what fence?** … **The adversarial pass has not run.**"* and *"**This is the load-bearing question and everything else depends on it.**"* D-042 §15 rules it settled: *"**Is an accumulating knowledge/science quantity permitted?** (CR-007 §6 / the Q1 blocker) → **YES**"*. | `docs/capability-architecture-decision.md:386-391` (§14.1); `docs/m5-roadmap-dependency-audit.md:296-301` (§8 Q1); queue pointer `docs/queue.md:1213-1214` | `docs/d042-empire-and-player-control-addendum.md:290-292` (§15), `:168-171` (§9.4/9.5) | Both recorded. CR-007's §6 correction notice treats D-042 as governing — *"the accumulation question is **answered YES** and must not be re-asked"* (`:178-188`) — but the two earlier records and the queue entry still read as OPEN and were never marked. **The FENCE (under what conditions accumulation stays legal) was NOT ruled by D-042 beyond §9.7/§12: DIRECTOR DECISION REQUIRED.** |
| **C-07** | **Ratified mechanisms depend on literacy/education variables that do not exist.** D-018 and D-021 gate Intelligentsia emergence, the Prospects need, rising expectations and mobility on literacy and education institutions. The registry publishes four variables, none of them literacy or education. | `docs/d018-classes-and-needs.md:26`, `:42`, `:48`, `:63`; `docs/d021-stability-doctrine.md:39`, `:43`; `docs/d039-command-fog-and-siege.md:38` | `Sim.Core/State/Variables.cs:93` (MEASURED, this pass) | Both recorded. `docs/capability-architecture-decision.md:224-228` states the same gap: *"**The dependency is ratified; the variables to satisfy it do not exist.** That gap is real M7 work."* |
| **C-08** | **How many published variables ship.** *"the predicate registry ships only **three** variables (`Variables.cs`)"*. The file ships four: `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. | `docs/capability-architecture-decision.md:224-228` | `Sim.Core/State/Variables.cs:93`, ids `:36-38`, `:60`, `:90` (MEASURED, this pass) | Both recorded. Cause is INFERRED: the T4.11 merchant variable registered after that document was written. Related line drift, MEASURED: the capability record cites the artisan predicate at `sim.json:165-167`; on `9f6c6ae` it is at `:169-171`. Per GOV-4 the tree wins on the fact; the ruling is unaffected. |
| **C-09** | **How many meanings does "institution" carry, and which one is the knowledge-conversion mechanism?** Four meanings: *"political module (M7→M8), a need trade-off binding M5 by name (D-035), a settlement capability (D-038), and the knowledge-conversion mechanism (the M5 placeholder)."* Six meanings: adds *"argument to the control function"* (D-041 `:35`) and *"predicate operand in class emergence"* (D-018 `:25`), with *"**`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or `Sim.Tests/`.**"* | `docs/capability-architecture-decision.md:230-234` | `docs/milestone-architecture-governance.md:179-209` (§4) | Both recorded. Both say the same thing about consequence: *"**This must be ruled before anyone writes an institution packet.**"* A **CR-010** was recommended (`:299-300`) and **never written** (`docs/current-state.md:13`). Status: **DIRECTOR DECISION REQUIRED, unowned.** |
| **C-10** | **Does research have completing "projects"?** The M5 scope list: *"7. Research capacity and research throughput. 8. **Multiple simultaneous research projects — NOT a single Civ-style queue.** … 11. Technology prerequisites and **alternative technological pathways**."* And the temporal placeholder: *"When research crosses **completion**: 1. the technology becomes available at that point in simulation time…"*. Against: *"a *completing project that grants a capability* remains **prohibited** — unchanged"*, logged as conflicts #6 and #7 (*"M5 placeholder §7's research-completion event **vs** B3 (it is Model A)"*, *"M5 scope items 7–11 (projects, throughput, prerequisites) **vs** B3"*). | `docs/m5-research-technology-institutions-placeholder.md:79-84`; `docs/m5-temporal-control-and-player-agency-placeholder.md:154-169` (§7) | `docs/adr/cr-007-b3-exemplar-reconciliation.md:215`; `docs/capability-architecture-decision.md:375-376` (§13 #6, #7); `docs/milestone-architecture-governance.md:223` | Both recorded. D-042 settles **half** of it — parallelism YES (§9.3), one-at-a-time queue banned (§12) — and says nothing about whether a research activity **completes**. Note both placeholders declare themselves unratified (`:3-11` and `:3-5`). Status: **DIRECTOR DECISION REQUIRED.** |
| **C-11** | **"Education and literacy as modifiers" vs Law 2.** The M5 placeholder lists *"5. Education and literacy as **modifiers** of knowledge production"* and *"government modifiers"* among the deliberately-absent items. Flagged: *"**Law 2 hazard flagged, not a violation:** … A free-floating permanent modifier is the banned construct. **D-035's shipped shape is the legal one** — *'one institution raises one need and lowers another'*, a two-sided mechanism."* | `docs/m5-research-technology-institutions-placeholder.md:73`, `:17` | `docs/milestone-architecture-governance.md:205-209`; law at `CLAUDE.md:17` | Both recorded. The hazard is named, not ruled. The two-sided D-035 shape is offered as the legal alternative but has not been ruled to apply here. |

---

## §8 TERMINOLOGY — THE REPOSITORY'S OWN TERMS IN THIS CLUSTER

*Definitions are the documents'. Where a term has a defining line, it is cited. The design
phase must use these words; generic game-design vocabulary is drift.*

| Term | Meaning as the documents define it | Defining source |
|---|---|---|
| **D-020 predicate DSL** | The closed comparison/boolean grammar over **registered variables**, parsed once at config load, with actionable rejection. No functions, no arithmetic in v1. Predicates are **data**; the parser lives in `Sim.Core`. | `docs/m2-spec.md:8`; `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| **published variables** | Named `double` quantities that a system writes per settlement per turn into the `Variables` table, keyed by the **code-side** registry id (names never live in sim rows). An entry exists only once a system actually publishes it (D-027). Consumers read the **PREV** turn's rows. | `Sim.Core/State/Variables.cs:3-8`; `Sim.Core/State/WorldState.cs:388-394` |
| **the capability seam** | The D-020 DSL over published variables, **as consumed independently by each domain system**. It ships, with two live consumers. Never a coordinating owner. | `docs/capability-architecture-decision.md:176-178`; `docs/d042-…:276-282` |
| **two live consumers** | Class emergence (`ClassStateRow`, stored latch, hysteretic) and recipe availability (`requires`, pure derived, no state). | `docs/capability-architecture-decision.md:186-192`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396` |
| **knowledge gate** | The project's name for a `requires` predicate on a recipe: *"a knowledge gate over published variables, **never a calendar date** (law 4)"*. | `Sim.Data/content/goods.json:3`; `Sim.Core/Systems/GoodsConfig.cs:32-35` |
| **latch** (hysteresis latch) | `ClassStateRow.Active` — 1 while the emergence predicate has fired and the recession predicate has not; **current satisfaction under hysteresis, not a record that something happened**. `recede` absent = never recedes. | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction `docs/milestone-architecture-governance.md:242-251` |
| **emerge / recede** | The two predicate slots of a class definition in `sim.json`; the band between them is the hysteresis. | `Sim.Data/content/sim.json:169-171`, `:176-178` |
| **capability** | Distinct from raw state, knowledge, research activity, technology, policy, institution and action. Sits in the chain `State → Published Variables → Predicates → Capabilities → Available Actions`. | `docs/d042-…:149-152` (§8.2) |
| **knowledge** | Distinct from technology and capability; ultimately **Empire-scoped**; generated as an **allocatable flow**; unallocated remainder **accumulates as a reserve**. | `docs/d042-…:164-171` (§9.1–§9.5) |
| **scope** (Empire-level / Settlement-level) | The dimension capability evaluation must distinguish, *"with unit/action scope where required"*. The repository's word is **Empire** in D-042 and **polity** in code and older docs — D-042 §13/§14.4 rules them the **same object** and implies no rename. | `docs/d042-…:155-156` (§8.3), `:207-229` (§13), `:269-274` (§14.4) |
| **domain lattice lite** | The Spine's name for the knowledge structure — *"no tree; domain lattice lite"*. Parallel independent lattices, never one graph. | `docs/civ-sim-architecture-v3-outline.md:84`; reading at `docs/capability-architecture-decision.md:199-201` |
| **diffusion** | Knowledge moving between civilizations; one of D-042 §9.6's named sources. Rides **contact**, which the shipped trade network already provides. | `docs/d042-…:172-174`; `docs/m5-roadmap-dependency-audit.md:137` |
| **divergence** | The milestone's goal statement: *"map shows civilizations pulling apart in time"*. | `docs/civ-sim-architecture-v3-outline.md:110` |
| **computed era labels** | Era names as **descriptive output** derived from state — never an input to capability. | `docs/civ-sim-architecture-v3-outline.md:22`, `:110`; `docs/d039-command-fog-and-siege.md:37-40` |
| **practical discovery** | D-042 §9.6's term for non-deliberate knowledge arrival; the placeholder's *"circumstantial research and discovery"* (item 20) is the same idea in the unratified record. | `docs/d042-…:172-174`; `docs/m5-research-technology-institutions-placeholder.md:97` |
| **discovery** (geographic) | The D-040 sense: the world's **extent** is itself discovered; *"discovery reports a permanent fact about the land. Once known, land stays known."* Distinct from reconnaissance, which reports a lagging position of a moving thing. | `docs/d040-discovery-and-control.md:37-54` |
| **computed extent** | A polity's knowledge of the world — a computed quantity, **not** a visibility flag over pre-known terrain. | `docs/d040-discovery-and-control.md:37-39` |
| **structural exclusion** | The property of a predicate that can be **false forever** for a structurally unsuited polity (B3's coastal conjunct). *Permitted and valuable, NOT required* (CR-007 §8.3). | `docs/d040-…:66-70`; `docs/adr/cr-007-…:231-247` |
| **schedule (with jitter)** | The repository's name for the failure mode: a predicate whose conjuncts are all constant-true or monotone-in-time. *"It is not prohibited by B3, but it delivers no differentiation and should be recognised as a schedule rather than mistaken for emergence."* | `docs/adr/cr-007-…:238-240` (§8.4) |
| **REGISTRY LAW — scale invariance** | All-ratio predicate sets cannot differentiate settlements; scale sensitivity requires an **absolute** published quantity. | `Sim.Core/State/Variables.cs:24-32` |
| **extent of the market** | Smith's limit on the division of labour, the project's justification for the `population` conjunct; currently implemented as **raw population**, which D-040 F2 records as *"a proxy, not an extent … Noted, not ruled."* | `Sim.Core/State/Variables.cs:44-58`; `docs/d040-…:198-203` |
| **anti-retrofit device** | The purpose the seam serves: one uniform way for any system to ask *"can this civilization do X?"*, so later packets do not each invent their own gate. | `docs/m5-roadmap-dependency-audit.md:197-211` |
| **scope widening** | The named unit of work on the seam: *"The anti-retrofit device is not new work. The work is to widen its SCOPE."* | `docs/capability-architecture-decision.md:52` |
| **persistent directive** | Player state describing a desired ongoing configuration — research allocation is the named example — *"NOT re-issued every turn to keep working."* | `docs/d042-…:99-106` (§6.2) |
| **D-021 valves** (release valves) | The stability doctrine's machinery that the political consequences of this cluster ride on. The tree's wording is *"RELEASE valves"*, not "unrest valves" (D-040 F4). | `docs/d021-stability-doctrine.md:17`; `docs/d040-…:211-213` |
| **quarantine** (of a citation defect) | D-040 Part F's doctrine for a finding against a *reference*: *"None changes a ruling; all change what may be cited in support of one."* | `docs/d040-discovery-and-control.md:186-187` |

---

## §9 GAPS — SUBJECTS IN THIS CLUSTER ON WHICH THE REPOSITORY RECORDS NO DECISION

*Stated as questions for the Director, per the mandate. Each is a genuine absence — verified by
reading the cluster's spine documents and by grepping the corpus for the term — not a
disagreement (disagreements are in §7) and not a proposal.*

**G-01 — What is the substrate for an Empire-scoped published variable?** D-042 §8.3 requires
Empire-level capability evaluation and §9.2 makes knowledge Empire-scoped, but `VariableRow` is
`(SettlementId, VarId, Value)` and no Empire/polity-keyed variable row type exists. *(MEASURED:
`Sim.Core/State/WorldState.cs:394`.)* No document proposes a row shape, a key, or a serialization
plan. **Who owns designing it, and at which milestone?**

**G-02 — Does a research activity COMPLETE, and if so what does completion produce?** D-042
§9.3–§9.5 rule that research runs in parallel and that knowledge is an allocated flow with an
accumulating reserve, but no document states whether an activity terminates, and if it does,
whether the terminal event is a capability grant (which C-10 records as contested) or something
else. **Is "completion" a concept in this architecture at all?**

**G-03 — What is the FENCE on accumulation?** D-042 answers *whether* accumulation is permitted
(YES) and supplies three guards (no rigid tree, no one-at-a-time queue, no calendar unlocks). No
document rules on the narrower fences that two records proposed: must the accumulator be able to
**fall**; are **ordered thresholds K1 < K2 < K3 on one accumulator** tree edges in disguise; is a
monotone-in-time accumulator a schedule (CR-007 §8.4's *"Proposed"*). **Which, if any, of these
becomes a rule?**

**G-04 — May a capability predicate reference another capability's latch?** Two records propose
banning it — *"Capability A referencing capability B's *latch* is how a tree grows back — that is
the line to hold"* (`docs/capability-architecture-decision.md:203-206`), restated as *"Proposed"*
at `docs/milestone-architecture-governance.md:319-321` and `docs/adr/cr-007-…:233-234`. **No
document rules on it.** Given that the D-020 grammar has no notion of a latch operand today, is
the ban worth ratifying before the grammar is widened?

**G-05 — How many knowledge quantities are there, and what are they?** One stock, two, or a
stock and a rate; science vs knowledge; how many parallel domains; whether domains are literal
trees, graphs or predicate sets. Recorded as questions in two unratified records
(`m5-research-…:122-124`; `m5-roadmap-…:308-311`) and never answered. **Does the Director want
one knowledge quantity per domain, or one global quantity allocated across domains?**

**G-06 — Does an acquired capability outlive its preconditions, and by what mechanism?** D-042
§5.4 preserves *unrelated accumulated Empire state* across a government transition; the shipped
latch provides no monotonic acquisition (K-20/K-21). The join is unruled — recorded as *"Open"*
at `docs/milestone-architecture-governance.md:329-330`. **Should knowledge be forgettable, and
if so through what — a named Ledger sink, a recede predicate, or decay on the reserve?**

**G-07 — Does knowledge DECAY, and is decay a conserved-stock event?** Asked twice
(`m5-research-…:127-129` — *"is decay a Ledger sink with its own reason, as spoilage and granary
overflow are for grain?"*; `m5-roadmap-…:310`) and never answered. Answering it decides whether
knowledge is a Law 1 conserved `long` or a `double` observational quantity — a **law 7** typing
question, not a flavour question.

**G-08 — WHAT CALIBRATION CORRIDOR GATES KNOWLEDGE?** *"The project gates milestones on
corridors; 'science output' has no obvious historical target. A milestone that cannot be
calibrated is a milestone that cannot pass its own exit criteria."* (`m5-roadmap-…:312-314`;
repeated as *"unanswered and serious"* at `capability-architecture-decision.md:397-399`.) **No
candidate corridor is named anywhere in the tree.** This is the only gap that can block a
milestone exit rather than a packet.

**G-09 — What is the DIFFUSION mechanism?** Diffusion is named in the Spine (M6/M7 row), in
D-042 §9.6, and in the audit as *"rides the existing trade network"* — but no document states
what crosses the contact edge, at what rate, in what direction, or whether it is conserved.
**Is diffusion a flow of the knowledge quantity, or an independent predicate on contact?**

**G-10 — How does foreign contribution get gated?** The placeholder expects gating *"by future
rules involving distance, diplomacy, language, institutional capacity, literacy and wealth"* and
says *"**Those rules are not decided here.**"* (`m5-research-…:106-108`). D-042 §9.6 permits open
borders and foreign institutions to contribute but sets no conditions. **Nobody owns these rules.**

**G-11 — Is "educated population" a class, a class attribute, or new state?** Asked at
`m5-roadmap-…:305` and never answered. D-018 reserves an Intelligentsia class slot (K-45) but the
literacy quantity that gates it has no home. **Which of the three?**

**G-12 — What does BREAKTHROUGH mean under the atomic turn?** The only recorded constraint is
that *"'Rapid' is meaningful in turns, not sim-years"* (`milestone-architecture-governance.md:174-176`),
that dt falls 10 → 0.5 across eras, and that the worst case is the Neolithic where discoveries are
rarest (`capability-architecture-decision.md:306-311`). **No document defines a breakthrough as an
object.** Is it an event, a predicate flip, a chronicle entry, or nothing at all?

**G-13 — Who owns AI research strategy, and under what determinism discipline?** The AI
constitution lists *"Technological appetite"* as a personality dimension
(`docs/m5-ai-constitution.md:79`) and bans hidden map knowledge (`:212`), and describes itself as
*"Planning document for M5 … not an implementation packet"* (`:5`) with four unresolved citation
findings against the tree (`docs/queue.md:1163-1185`). The placeholder's Q7 requires a
deterministic decision procedure free of unordered iteration (law 5). **No document connects the
two.**

**G-14 — Is there a technology object at all, distinct from a capability?** D-042 §9.1 rules
knowledge distinct from technology and capability, and §8.2 lists technology as its own concept —
but **nothing in the tree says what a technology IS**: a row, a predicate, a named bundle, or
purely a label over a satisfied capability. `institution` has the same problem and at least has a
conflict record (C-09); `technology` has neither a definition nor a conflict record.

---

## §10 CAVEATS ON THE EVIDENCE

1. **Secondary evidence, declared (GOV-4).** Four of this cluster's richest sources —
   `docs/capability-architecture-decision.md`, `docs/milestone-architecture-governance.md`,
   `docs/m5-roadmap-dependency-audit.md` and `docs/adr/cr-007-b3-exemplar-reconciliation.md` —
   are **prior agents' reports by the same author**, not rulings. They are cited here as
   **records of what was concluded**, never as ratification. Three of the four carry correction
   notices falsifying parts of the others; every such correction is recorded above (K-21, K-51,
   K-52, K-54) rather than applied silently.

2. **The verification status of `capability-architecture-decision.md`, stated because it bounds
   its own content.** *"Six source audits were commissioned; **four returned (Q1–Q4) and two did
   not (Q5, Q6 — session limit)**, and **the three-lens adversarial verification of the Q1
   recommendation DID NOT RUN.** … **§4's recommendation is UNVERIFIED and must not be built
   against until it is attacked.**"* (`:29-37`). Its own CORRECTION NOTICE then records that the
   adversarial pass **has since run** and returned **SURVIVES_WITH_CONDITIONS, not a clean
   survival** (`:7-27`), and falsified two of the document's claims — §4.1's appeal to the
   shipped exemplar, and the latch description. **Both falsifications are recorded above** (the
   first at K-52 and §7 C-01, the second at K-21). RATIFIED content in that document is only
   what it **quotes** from ruled sources; its own recommendations are labelled here accordingly.

3. **Line drift is recorded, not corrected.** Where a source cites a line that has moved on
   `9f6c6ae` — `sim.json:165-167` → `:169-171`, `CLAUDE.md:16` → `:19` — both are shown (C-08).
   No frozen or ratified document was edited to fix a citation.

4. **Branch scope.** This recovery covers `claude/civdemo-work-b1z2y4` @ `9f6c6ae` only.
   `docs/current-state.md:13` records that **CR-008 (money owner) exists only on
   `m5-full-build`** and that **CR-009 and CR-010 have never existed on any branch** — MEASURED
   by that document, re-checked here against `docs/adr/` (no `cr-008`, `cr-009` or `cr-010`
   file on this branch) and against `origin/m5-full-build`'s tree listing (a `cr-008` file is
   present there). CR-009 and CR-010 are the owners of C-03 and C-09 respectively, so **two of
   this cluster's unresolved conflicts have a recommended CR that was never written.**

5. **Nothing in T4.21 touches this cluster.** The in-progress packet on this branch (famine
   semantics, bounded migration, demographic shock) publishes no knowledge variable and adds no
   predicate consumer. MEASURED: `Sim.Core/State/Variables.cs` is unchanged with respect to the
   M4 baseline's four ids.

---

**WHAT THIS DOCUMENT DID NOT DO.** No knowledge, research, capability, technology, diffusion,
institution or polity system designed. No conflict reconciled. No status changed. No ruling
made, proposed as ratified, or implied. No frozen or ratified document edited. No production
code, schema, JSON, golden, corridor or quarantine touched. No independent reviewer
participated.
