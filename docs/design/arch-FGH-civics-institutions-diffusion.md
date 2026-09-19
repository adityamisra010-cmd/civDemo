# ARCH-F/G/H — CIVICS · INSTITUTIONS · DIFFUSION

**DESIGN PHASE. NOTHING IMPLEMENTED.** No production code, schema, data file, golden, corridor,
quarantine or ratified/frozen document was touched. No milestone spec was written or amended. No
existing document was edited. No ruling is made here. Created under the Director's 2026-09-19
mandate, Parts 13 (civics), 14 (institutions) and 15 (diffusion).

**Author authority: none. The DIRECTOR IS ChatGPT.** Where two sources disagree, both are named
and neither side is taken (§6). Where the repository records no decision, the question is stated
rather than answered (§7).

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `b6820e2`, working tree
clean; `origin/main` = `dbef61a`. Every `file:line` below was re-read on that tree by this lane.
Where a prior record's claim has drifted against the tree, the drift is recorded (§6), never
silently corrected — GOV-4: the tree wins on facts, the ruling stands on rulings.
**Concurrent sibling-lane commit, recorded rather than absorbed:** this document was committed onto
`f373014` (*"DESIGN LANE E: breakthrough architecture (Part 7) and the technology domain graph
(Part 8)"*), which lands after the pin. MEASURED: it adds exactly one file,
`docs/design/arch-E-breakthrough-domains.md`, and changes no source, schema, data file or document
this lane read, so every citation above remains valid on the committed tree. Lane E's content was
**not** read by this lane and nothing here depends on it.

**Label key** (mandate labelling rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this phase's
design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.
Most of what follows is PROPOSED. That is the honest state of these three subjects.

**Prior lanes read first, as the method requires:**
`docs/design/recovered-decisions-civics-institutions.md` (G-001…G-200, C-01…C-14, Q-01…Q-19),
`docs/design/recovered-decisions-knowledge-tech.md` (K-01…K-63, C-01…C-11, G-01…G-14),
`docs/design/recovered-decisions-architecture-invariants.md` (A-06, A-61…A-65, §4a, G-05),
`docs/design/arch-C-knowledge.md` (the three registers; OWED-1…OWED-6),
`docs/design/arch-D-technology-capability.md` (§2 the rejection; §3.3 scope; §3.4 the latch).
This lane re-verified every RATIFIED fact it leans on against its own source `file:line` rather
than trusting a recovery row. Two recovery claims failed re-verification and are recorded in §6.

**New terms introduced here, each marked PROPOSED and defined once:** *regime module set* (§2.1),
*government label* (§2.1), *the enum-government test* (§2.2), *the God-object test* (§3.4),
*contact edge* (§4.1), *artefact diffusion* (§4.1). Everything else uses the repository's own
vocabulary — the Spine, the D-020 predicate DSL, published variables, the D-021 valves, the
D-035-C carrier test, corridor, quarantine, latch, packet, law 1..7, Exit/Voice/Endurance.

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This document is a conceptual architecture for three adjacent subjects the repository has ruled
on repeatedly and built almost none of: how a government comes to be what it is and how it can
be reformed, weakened, transformed and lost (Part 13); what kind of object an *institution* could
be, laid out as candidate meanings rather than chosen (Part 14); and how knowledge and practical
technique move between places and between Empires without a discovery ever becoming
civilization-wide by default (Part 15). Its purpose is to give the Director a design that is
checkable against the frozen laws before any packet is written, and to make every place where it
is standing on nothing visible as a carrier debt, a conflict or a question.

It deliberately does **not**: pick a meaning for "institution" (that is CR-010, recommended at
`docs/milestone-architecture-governance.md:299-300` and never written —
`docs/current-state.md:13`); resolve any conflict it finds; name a government form or a roster of
forms; propose a schema, a row type, a constant, a threshold, a curve, an RNG stream or a TUNE
value; schedule anything into a milestone; or resurrect the rejected universal `CapabilitySystem`
under a different name. It also does not design a linear government tree, a research-points
economy, or any "unlock → permanent bonus" shape, because laws 2 and 4 forbid them
(`CLAUDE.md:17`, `:19` — RATIFIED) and Part 4 of the mandate forbids them again.

---

## §1 GROUND — THE FACTS THIS DESIGN STANDS ON, EACH VERIFIED THIS PASS

Each row was read on `b6820e2` by this lane. F-numbers are this document's own; they are not
decision ids.

| # | fact | source read this pass | label |
|---|---|---|---|
| **F1** | Law 2: coefficients inside resolution equations are legal; free-floating permanent buffs are banned. Spine's precise form names *"+10% happiness"* as the illegal shape. | `CLAUDE.md:17`; `docs/civ-sim-architecture-v3-outline.md:20` | RATIFIED |
| **F2** | Law 4: capability derives from computed state, never from dates or era labels; era labels are *"descriptive output only"*. | `CLAUDE.md:19`; `docs/civ-sim-architecture-v3-outline.md:22` | RATIFIED |
| **F3** | Law 6: systems never reference each other, only `State` and `Kernel`; communication is through tables and events. | `CLAUDE.md:21` | RATIFIED |
| **F4** | Law 1: people/money/goods change only via `Ledger.Transfer`/`Ledger.Flow`; conserved stocks are `long`. Law 3: rates are per sim-year, integrated with `dtYears`. Law 7: conserved stocks `long`, rates/ratios `double`. | `CLAUDE.md:16`, `:18`, `:22` | RATIFIED |
| **F5** | Spine law 8 names **institutions** as a *content type*: *"All content (goods, crops, doctrines, institutions, units, events, phonologies) lives in validated data files; code implements mechanisms only."* | `docs/civ-sim-architecture-v3-outline.md:26` | RATIFIED |
| **F6** | The fixed execution order already contains a civics slot: *"culture/religion/opinion → politics/legitimacy → diplomacy"*, on a default one-turn lag. Marked *"draft; finalize in S3"* by its own text. | `docs/civ-sim-architecture-v3-outline.md:32` | RATIFIED, with the document's own draft qualifier |
| **F7** | Government shape, twice refused: *"composable modules, not enum governments"* and *"Avoid hard-coding governments as bundles of direct bonuses. (This aligns with Law 2 …)"*. | `docs/civ-sim-architecture-v3-outline.md:85`; `docs/d042-empire-and-player-control-addendum.md:90-91` | RATIFIED |
| **F8** | Government influence is mediated: it *"may influence available decisions, policies, capabilities, economic behaviour, military organisation, knowledge production and institutions — through state and computed conditions."* A transition *"preserves unrelated accumulated Empire state"*, and *"a government change does not destroy or replace Empire identity."* | `docs/d042-empire-and-player-control-addendum.md:84-93` | RATIFIED |
| **F9** | The universal `CapabilitySystem` is REJECTED, under a section headed *"STATE-MEDIATED DEPENDENCY (reaffirms Law 6)"*: *"Do not create a universal God system such as a `CapabilitySystem` that owns every capability or coordinates every domain."* Repeated in the §12 anti-pattern list. The precise reason is the two clauses above it: gameplay interdependence is allowed, direct code coupling is not; conceptual dependence *"never justifies a sibling call."* | `docs/d042-empire-and-player-control-addendum.md:134-142`, `:196-203` | RATIFIED |
| **F10** | Capability is *"distinct from raw state, knowledge, research activity, technology, policy, institution and action"*, on the chain `State → Published Variables → Predicates → Capabilities → Available Actions`; evaluation *"must be able to distinguish SCOPE — Empire-level and Settlement-level"*; definitions *"consume computed state, never hard-coded calendar unlocks."* | `docs/d042-empire-and-player-control-addendum.md:146-158` | RATIFIED |
| **F11** | Knowledge is Empire-scoped but *"produced/applied through settlements, institutions, people and research activities"*; research runs in parallel; generation is an allocatable flow; the unallocated remainder accumulates as a reserve; *"Knowledge may arise through deliberate research, practical discovery, diffusion, external exchange, espionage, or other explicitly defined mechanisms; open borders and foreign institutions may contribute."* Do not collapse into one rigid tree. | `docs/d042-empire-and-player-control-addendum.md:164-175` | RATIFIED |
| **F12** | The D-021 paired-feedback rule: *"Every positive feedback loop in the design must ship with at least one negative feedback loop that strengthens with amplitude"*, project-wide. | `docs/d021-stability-doctrine.md:8` | RATIFIED |
| **F13** | The organizing frame: grievance expresses through **Exit, Voice, or Endurance**, and *"the sim always keeps at least one channel open."* The eight **release valves** are enumerated. | `docs/d021-stability-doctrine.md:24`, `:26-33` | RATIFIED |
| **F14** | The landing schedule: **M5 (unrest-lite) ships valves 1, 2, 3, 6, 7 *with* the unrest it ships — "the brakes install with the gas pedal, never after"**; the ignite-and-burn-out test enters the battery at M5; M8 gets valves 4, 5, 8 plus movements, the D-019 matrix and the collapse-as-transition test. | `docs/d021-stability-doctrine.md:47-50` | RATIFIED |
| **F15** | The three corridor tests: *ignite-and-burn-out*, *no empty menus* (≥1 remediation option in every legitimacy-crisis state, under every regime, always), *collapse is a transition, not a trap*. A doom loop found by them is a mechanism bug — *"fix the mechanism, never script the outcome."* | `docs/d021-stability-doctrine.md:10-15` | RATIFIED |
| **F16** | The D-035-C carrier test: *"Name the physical carrier — a good, a purse, a building, a policy, a body, a season. If none exists, it is an invented modifier and is refused."* A coupling outside the seven paths *"has not found an eighth path, it has failed the carrier test."* | `docs/d035-needs-aggregation.md:89-95` | RATIFIED |
| **F17** | Path 6, the **institutional trade-off**: *"one institution raises one need and lowers another — garrison buys Safety with Dignity; conscription buys Safety with Sustenance. M5 must build Safety-vs-Liberty this way and no other way."* Carrier: *a policy, an institution*. Path 7, **grievance-level buffering**: faith and community absorb grievance without changing satisfaction; *"Explicitly legal."* | `docs/d035-needs-aggregation.md:86-87` | RATIFIED |
| **F18** | D-035-D: heavy or arbitrary exaction *"injures Dignity directly"*, with the tax rate/instrument as the input; routing tax pain through consumption would be the forbidden satisfaction→satisfaction link *"wearing an economic costume."* Lands M5. | `docs/d035-needs-aggregation.md:100-108` | RATIFIED |
| **F19** | `SettlementHappiness` is a **DERIVED READING, never a stock**, not serialized: *"A stock can be granted … A derived reading cannot — the only way to move it is to move a condition."* It deliberately does not read the needs/grievance tables because D-021 defers grievance-driven behaviour to M5, and `scripts/check-read-isolation.sh` enforces that. | `Sim.Core/State/SettlementHappiness.cs:6-38`; gate at `scripts/check-read-isolation.sh` | RATIFIED (shipped, director ruling) |
| **F20** | Control vs attachment, the ruling: *"Control is COMPUTED each turn from distance, infrastructure and institutions … Attachment is an ACCUMULATED STOCK held by a population, slow to build and slow to lose."* Attachment travels with people; its inputs are time under control and what the polity does; *"distance enters only through control, never directly."* What makes it a lever and not a coefficient is that a player DOES the things that move it. | `docs/d041-attachment.md:34-48`, `:64-66` | RATIFIED |
| **F21** | D-040 C3: **do not add a `loyalty` field** — *"a number attached to a place rather than a consequence of anything."* Instead control carries a distance term and the political systems read control: *"The mechanism is administrative reach; loyalty is what you observe when reach runs out."* C6: distance means travel cost over the network graph, so the transport packet and the control model are COUPLED. | `docs/d040-discovery-and-control.md:105-111`, `:130-135` | RATIFIED |
| **F22** | D-037 B2: *"A settlement ceasing to obey … is CONTROL failing while CLAIM persists. The D-010 government-paralysis mechanic is this same failure seen from the capital; secession is the provincial view."* E1: loyalty is computed from eight named inputs, and the levers on it are *"ORDINARY GOVERNANCE read at provincial scale"* — invest, integrate institutionally, tax less, extend the franchise, garrison, repress — *"repression must be able to CREATE the grievance it suppresses."* E6: autonomy, federation, devolution and power-sharing RAISE LOYALTY WITHOUT CEDING CLAIM. | `docs/d037-emergent-polities.md:48-52`, `:163-174`, `:203-206` | RATIFIED |
| **F23** | D-009/D-010: the unrest ladder is escalation, not events — `discontent → protest → riot → uprising (parallel authority, tax loss) → revolution/coup attempt`. **Government paralysis**: when overthrow risk crosses threshold a *legitimacy crisis* opens, a defined subset of verbs FREEZES and *"the authority budget collapses to crisis-only actions"*, with a six-option remediation menu — concede reforms, repress, buy off leaders, scapegoat/purge ministers, call elections/council (institution-dependent), emergency powers (legitimacy debt). Failure remains playable. | `docs/d009-d010-map-population-addendum.md:39-43` | RATIFIED (frozen) |
| **F24** | D-018: a class earns its slot only if it differs in income source, needs signature, political weight basis and **what it does when angry** — *"Anger must route through a different system per class … never a generic unrest bar."* Four of the nine class emergence conditions take an institution as an operand. **The Clergy *produce* legitimacy.** *Influence = f(class wealth share, institutional enfranchisement under current regime, organization level, armed capacity)*, and *"every regime module (Part 15) publishes its enfranchisement vector."* | `docs/d018-classes-and-needs.md:9`, `:23-27`, `:62` | RATIFIED (frozen) |
| **F25** | Rising expectations replaces era weight tables: salience of Comfort, Liberty and Prospects scales with the bucket's **literacy, urbanization and media exposure**, and the ruled consequence is that *"development raises unrest potential before satisfying it; revolutions cluster in modernizing societies."* Education is *"a flow with a lag"* and *"a deliberate gamble; that tension is kept, not patched."* | `docs/d018-classes-and-needs.md:48`; `docs/d021-stability-doctrine.md:43` | RATIFIED (frozen) |
| **F26** | The D-020 predicate DSL is closed: comparisons and boolean ops over **registered variables** published per settlement; *"No functions, no arithmetic in v1"*; predicates are data; parsed once at load; evaluated against the PREV turn's rows (one-turn lag). | `docs/m2-spec.md:8`; `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` | RATIFIED (shipped) |
| **F27** | The latch: *"Inactive + emerge true → Active = 1; active + recede true → Active = 0 … Recede absent = never recedes."* It records **current satisfaction under hysteresis, not history**. | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction `docs/milestone-architecture-governance.md:242-251` | RATIFIED (shipped) |
| **F28** | Recipe availability is the second live consumer and is named a **knowledge gate**: an optional D-020 `requires` predicate over published variables, *"never a calendar date (law 4)"*, evaluated **per settlement per turn** against PREV variables, pure-derived with no stored state. Live gated recipes: `bronze-casting`, `toolmaking`, both `"artisan_share > 0.05"`. | `Sim.Data/content/goods.json:3`, `:136`, `:156`, `:159`, `:175`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396` | RATIFIED (shipped) |
| **F29** | **MEASURED, this pass:** the published-variable registry holds four entries — `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. None is a literacy, education, institution, legitimacy or knowledge variable. `VariableRow` is keyed `(SettlementId, VarId, Value)` — settlement-scoped only. | `Sim.Core/State/Variables.cs:36-93`; `Sim.Core/State/WorldState.cs:394` | MEASURED |
| **F30** | **MEASURED, this pass:** the shipped class data holds three classes — Peasants (no predicate), Artisans, Merchants. There is no Clergy, Soldiers, Bureaucrat, Intelligentsia or Aristocracy row. | `Sim.Data/content/sim.json:161-180` | MEASURED |
| **F31** | Migration: *"people are Ledger.Transfers of buckets between settlements — migrants keep their FULL bucket key."* **MEASURED:** the bucket key is `(Settlement, Culture, Religion, Class, CohortIdx)` with a conserved `long` count; the row carries no skill, literacy or knowledge field. Bounded at T4.21-2 under ADR-025 (exit openness, basin caps, vacancy). | `Sim.Core/Systems/Migration/MigrationSystem.cs:20-21`; `Sim.Core/State/WorldState.cs:110-125`; `docs/adr/adr-025-bounded-migration-exit-openness-basin-caps-vacancy.md:1-10` | RATIFIED (shipped) + MEASURED |
| **F32** | Trade: per **connected** settlement pair, per good, once per turn, goods flow when the price gap exceeds the transport-cost **deadband**; distances are the catchment system's `SettlementDistances` over **lattice + built paths**; an unreachable pair moves exactly zero forever; **NO TRANSIT LOSS**; the artefact is `TradeFlowRow(From, To, Good, Quantity)`. | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:12-33`; `Sim.Core/State/WorldState.cs:459` | RATIFIED (shipped) |
| **F33** | `TradeScope { Unruled, Domestic, Foreign }` classifies whether a trade crosses a polity boundary, and is **DERIVED, NEVER STORED** — computed from the row's two endpoints and the `ControlRow` relation. *"this classifies; it does not gate, price, tax or forbid."* | `Sim.Core/Systems/Trade/TradeScope.cs:6-45` | RATIFIED (shipped) |
| **F34** | `NotableLifecycle` ships three operations, each a Ledger flow: **Born** (one person extracted from a bucket), **Dies**, **Defects(toSettlement, toAllegiance)** — a conserved person crossing both settlement and allegiance. *"IS BOUGHT is not — the person moves, but the consideration is a SECOND flow, and payment is money, which is M5."* **MEASURED: it has no caller** — *"Nothing in M4 decides WHEN a notable should emerge."* | `Sim.Core/State/NotableLifecycle.cs:5-33`, `:115-134`; `Sim.Core/State/WorldState.cs:620-634` | RATIFIED (shipped) + MEASURED |
| **F35** | `RevoltSystem`: a settlement whose derived happiness is **exactly zero** loses its control relation — *"statelessness is now something a world can ARRIVE at, by governing a place so badly that it stops being governed."* Zero is total deprivation, deliberately **not** a tunable low-happiness band, because *"a band would be a policy knob inviting tuning, while the ruled condition is a corner of the state space."* It does not transfer control, create a rebel polity or fight. | `Sim.Core/Systems/Revolt/RevoltSystem.cs:16-45` | RATIFIED (shipped) |
| **F36** | The Empire is not a God object: *"Domain state stays in its own tables/systems, sharing a stable Empire identity/key"*, and the conceptual shape is *"a relationship, not an instruction to build one monolithic class."* Shipped literally — `PolityRow` is identity plus command source and nothing else, and *"government, economy, knowledge, military and institution state are SEPARATE domains that key themselves to this id."* | `docs/d042-empire-and-player-control-addendum.md:60-65`; `Sim.Core/State/WorldState.cs:686-704` | RATIFIED (shipped) |
| **F37** | The observability taxonomy, read at source: every field is exactly one of **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED**, and anything needing a formula the simulation does not expose is a **GAP**, named as such. *"The logger observes. The simulation calculates. The UI reads. The player issues orders."* No record is serialized into `WorldState` or the canonical schema. | `docs/observability-architecture.md:16-32`, `:331-337` | RATIFIED — with the freshness caveat in §6 X-5 |
| **F38** | **MEASURED, this pass:** `institution` appears in the code tree exactly once, in a doc comment (`WorldState.cs:696`, quoting D-042 §3.2). There is no institution row, table, system, config field or data file. The recovery records' phrasing *"appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or `Sim.Tests/`"* is now one doc-comment stale; the substantive claim — **zero mechanical institution** — stands. | grep over `Sim.Core/ Sim.Data/ Sim.Cli/ Sim.Tests/`; prior claim at `docs/milestone-architecture-governance.md:181-183` | MEASURED |

---

# PART 13 — CIVICS

## §2.1 THE SHAPE — THREE OBJECTS, NOT ONE, AND NOT A TREE

**PROPOSED.** A government in this project is not one thing that an Empire *is*. It is three
objects with three different kinds of state, and keeping them apart is the whole design. The
split is not invented for convenience: it is the same split D-041 B1 already ruled between a
quantity computed every turn and a quantity accumulated over time (F20), and the same split
`SettlementHappiness` already ships between a reading and a stock (F19).

| object | PROPOSED name | what it is | kind of state | precedent it copies |
|---|---|---|---|---|
| the arrangements actually in force | **REGIME MODULE SET** | the set of composable arrangements an Empire is currently governed by — a franchise arrangement, a tax instrument, a delegation arrangement, an autonomy grant to a province | **serialized, latched, per Empire.** A module is present while its own D-020 `emerge` predicate has fired and its `recede` has not — the shipped latch shape (F27) | class emergence (`ClassStateRow`), F27 |
| what it is called | **GOVERNMENT LABEL** | a name for the current module set — "chiefdom", "city-state", "bureaucratic monarchy" — for the chronicle and the UI | **DERIVED READING. Not stored, not serialized, and never read by any mechanism** | *computed era labels* (F2) and `SettlementHappiness` (F19) |
| how well it governs | **THE GOVERNING READINGS** | legitimacy, state capacity, administrative reach — the quantities the political systems consume | undecided by the repository; three candidate shapes are laid out in §2.3 and put to the Director in §7 | — |

**The term is the repository's own.** *"Regime module"* is D-018's word — *"every regime module
(Part 15) publishes its enfranchisement vector"* (F24) — and the Spine's fence is *"composable
modules, not enum governments"* (F7). This design proposes only that the module set is the
**state**, and that the form is the **label**, never the reverse.

**What a regime module IS mechanically, stated so it cannot become a bonus bundle.** PROPOSED —
a module is data (F5) carrying exactly three kinds of content, and nothing else:

1. **An enfranchisement vector.** The per-class weighting of institutional enfranchisement under
   this arrangement. This is not a proposal — D-018 already *requires* every regime module to
   publish one (F24). It is the module's whole contribution to political weight.
2. **A verb disposition.** Which player verbs this arrangement makes available, and which it
   freezes. This is the D-019 verb-freeze matrix's home. D-019 is **OPEN and closes at M8**
   (`docs/d009-d010-map-population-addendum.md:62`; `docs/spine-s8-governance-freeze.md:22` —
   RATIFIED), so nothing here closes it.
3. **Coefficients inside named resolution equations**, and only inside them (F1). A module may
   change the *rate* at which an existing mechanism runs — a mobility flow, a tax instrument's
   exaction, an administrative-reach term. It may never carry a standing effect that is not an
   argument to an equation someone else owns. Under D-035-C path 6 (F17), where a module touches
   needs at all it **raises one need and lowers another** — that is the ratified shape, and it is
   M5's only permitted route to Safety-versus-Liberty.

**What makes the set losable rather than a ladder.** PROPOSED. Because a module's presence is a
latch over a predicate, and the latch *"records CURRENT satisfaction under hysteresis, not
history"* (F27), **a module whose preconditions fail recedes.** Government forms are therefore
losable by construction rather than by a bespoke collapse mechanic. The one caveat is exact and
must not be glossed: monotonic retention exists only in the special case of an omitted `recede`
clause — *a data choice, not a property of the mechanism* (F27). Whether an acquired governance
arrangement should be allowed to outlive its preconditions is **DIRECTOR DECISION REQUIRED**
(§7 Q-G6; it is the civics face of recovery-lane G-06 and conflict C-10).

## §2.2 WHY NOT A GOVERNMENT ENUM — STATED AS A CHECKABLE TEST

The tree refuses the enum twice (F7) but gives no test. **PROPOSED — the enum-government test**,
offered so the fence is checkable rather than remembered:

> Delete the government's identifier from state. If every mechanical consequence can still be
> recomputed from the regime module set and the published variables, the design is conformant.
> If any mechanism reads the identifier itself, the design has an enum government, and
> `docs/civ-sim-architecture-v3-outline.md:85` refuses it.

The corollary is the reason the **government label** is a derived reading and not a field: a label
that is stored is an identifier, and an identifier that is stored will eventually be read. Law 4
(F2) already rules exactly this shape for era labels — *"descriptive output only"* — and this
design does nothing more than apply the same disposition one level across.

**A second test, for the bonus-bundle half** (PROPOSED): for each effect a module claims, name the
resolution equation the coefficient sits inside and the system that owns that equation. An effect
with no owning equation is a free-floating buff and law 2 refuses it (F1).

## §2.3 THE ELEVEN AXES OF GOVERNANCE EVOLUTION

The mandate names eleven investigation axes. Each is set out below as: what it would be
mechanically, what already exists, and what it is standing on.

| axis | PROPOSED mechanical form | what already ships (MEASURED) | what it needs that does not exist |
|---|---|---|---|
| **population scale** | an absolute published variable conjoined into module predicates. The **registry law** binds here: *"ANY emergence predicate that needs scale sensitivity MUST publish an absolute quantity, not another ratio"* (`Sim.Core/State/Variables.cs:24-32`, RATIFIED) — a "bureaucrat share" alone cannot differentiate Empires | `population` is published and is the registry's only scale-dependent term (F29) | Empire-scope aggregation — see §2.5 and X-3 |
| **administrative capacity** | the consumer of **administrative reach**: reach is travel cost over the network graph (F21), capacity is what reach plus staffing lets an Empire actually do at a distance | `SettlementDistances` over lattice + built paths ships and is already the trade system's distance source (F32); `PathBuildSystem` ships | *state capacity* is named in four ratified places and **defined in none** (recovery Q-06). Bureaucrats do not exist as a class in data (F30) |
| **legitimacy** | the quantity a legitimacy crisis reads (F23) and that the Clergy *produce* (F24) | nothing | **Undefined — recovery Q-03.** Its type, scope, dynamics, and whether it is a stock or a derived reading are all unstated. Three candidate shapes in §2.4 |
| **taxation** | in kind at M5 by ruling (`docs/m4-pre-spec-dependencies.md:23-41`, RATIFIED); injures Dignity **directly**, with the instrument as the input (F18) | nothing — *"TAXATION does not exist on this tree in any form"* (F19's own text) | a polity-scoped conserved destination stock. Law 1's `Transfer` needs two endpoints and `PolityRow` holds none (F36) — recovery C-03 |
| **law** | *"laws-lite"* is named once, in the frozen Spine M5 line, and nowhere else | nothing | a definition. Is a law a policy, a persistent directive, a regime module, or a fourth thing? Recovery Q-05 |
| **communication** | latency and reach over the same network object; D-040 C6 rules the transport packet and the control model **COUPLED**, so *"improving a route strengthens the hold on what is at the end of it"* (F21) | the network object ships and already does three jobs | a latency quantity distinct from cost. D-039 A2 already rules information is *three* quantities, not one score (`docs/d039-command-fog-and-siege.md:13-20`, RATIFIED) |
| **military organization** | the Soldiers class (emergence: *"standing professional force exists"*), whose anger routes to *"desertion, mutiny, coup — the praetorian threat"* (F24), plus notables with allegiance (F34) | `NotableRow` with `Allegiance` and a conserved person; `NotableLifecycle.Defects` (F34) | *military loyalty* is an overthrow-propensity input and a repression check and is **never defined** — whose is it (recovery Q-07)? No Soldiers class in data (F30) |
| **economic structure** | the strongest shipped axis: classes emerge from computed surplus and market extent, and recipes are gated by a social predicate | `artisan_share`, `trade_volume`, the two live emergence predicates and the two gated recipes (F28, F29, F30) | nothing structural. This axis is the proof the whole approach works |
| **political pressure** | grievance and need satisfaction per (settlement, class) | `GrievanceRow` and `NeedSatisfactionRow` ship — and are **quarantined**: `scripts/check-read-isolation.sh` enforces that nothing reads them, because D-021 defers grievance-driven behaviour to M5 (F19) | nothing new. **The quarantine lifting IS the M5 civics event**, and it should be treated as a packet in its own right rather than a side effect |
| **institutional development** | blocked | nothing (F38) | CR-010. See Part 14 |
| **cultural norms** | the bucket key already carries `CultureId` and `ReligionId`, so cultural distance from a ruling polity's core (an F22 loyalty input) is expressible in principle | the key ships (F31) | plurality is M8/M9 and is gated on R-3, the clone-cost ruling; D-037 C4 forbids building a divergence mechanic at all |

**PROPOSED reading of that table, stated because it is the useful conclusion.** Of the eleven
axes, exactly one — economic structure — is fully expressible today, and it is expressible because
it was built in the shape this design proposes for everything else: publish a quantity, write a
predicate, latch the consequence. Three axes (legitimacy, state capacity, military loyalty) are
named repeatedly in ratified documents and defined nowhere, and they are precisely the three that
the frozen D-009/D-010 overthrow propensity takes as inputs (F23). **INFERRED:** any M5 governing
loop must define at least those three before it can compute the mechanic the Director already
ruled.

## §2.4 LEGITIMACY, STATE CAPACITY AND MILITARY LOYALTY — THREE SHAPES, NONE CHOSEN

These three are put as shapes rather than answers because the repository rules nothing about them
and this lane has no authority to.

**Shape A — a DERIVED READING** (the `SettlementHappiness` precedent, F19). Recomputed from
conditions every time it is asked; not serialized; cannot be granted. *What it buys:* the D-040 C3
prohibition (F21) is satisfied by construction — there is no number attached to a place — and the
Clergy "producing" legitimacy becomes *the conditions the Clergy create*, not a deposit anyone
makes. *What it costs:* no memory. A regime cannot be living off a reputation earned a century ago.

**Shape B — an ACCUMULATED STOCK held by a population** (the D-041 attachment precedent, F20).
Legal in this project when the quantity is a **lever** — something a player DOES things to — and
D-041 C2 says exactly that is what distinguishes a lever from a coefficient. *What it buys:*
memory, hysteresis, and the historically right asymmetry (slow to build, fast to spend). *What it
costs:* it must not become D-040 C3's forbidden field, which means its inputs must be *time under
the arrangement and what the polity does*, never distance, and never a decay curve.

**Shape C — split, as D-041 B1 already split control from attachment** (F20): a computed reading
for the current governing capacity, and a separate accumulated stock for the reputational
residue. *What it buys:* both properties. *What it costs:* two quantities where the tree names one
word, and a real risk that the second is the first wearing a different name.

**DIRECTOR DECISION REQUIRED.** All three are consistent with something ratified, which is exactly
why the repository's silence is load-bearing rather than incidental.

## §2.5 EMERGE, REFORM, TRANSFORM, WEAKEN, LOSE — WHERE EACH ONE ALREADY HAS A MECHANISM

| verb | PROPOSED mechanism | grounding |
|---|---|---|
| **emerge** | a module's `emerge` predicate fires over published variables. No date, no era, no unlock (F2, F26) | the shipped class-emergence path (F27) |
| **reform** | a player act that moves the *conditions* a module's predicate reads, or that exchanges one module for another at a stated cost. Reform is a **persistent directive** in D-042's sense — *"state describing a desired ongoing configuration — it is NOT re-issued every turn to keep working"* (`docs/d042-…:99-106`, RATIFIED) | F8; the six remediation verbs of F23 |
| **transform** | the module set changes membership while `PolityId` survives — *"a government change does not destroy or replace Empire identity"* (F8). No new nation on revolution | F8, F36 |
| **weaken** | control carries a distance term, so an over-extended Empire fails at its edges first: *"empires lose their periphery first"* (`docs/d040-discovery-and-control.md:113`, RATIFIED). Weakening is not a status effect; it is reach running out | F21 |
| **lose** | **already shipped, once.** `RevoltSystem` removes the control relation at derived happiness exactly zero, and statelessness is a first-class world state (F35) | F35 |

**The identity that must not be broken.** D-037 B2 rules that government paralysis and secession
are **one mechanism seen at two scales** (F22). PROPOSED consequence, and it is a hard constraint
on any M5/M8 packet: paralysis and secession must read the *same* control quantity, differing only
in the scope at which the failure is read — the capital's view and the province's view. A design
that gives them separate state has re-scoped a ruling and owes a Contradiction Report, not a
refactor.

**What the tree refuses, and this design refuses with it:** a `disputed` flag (D-037 C5), a
resolution timer or arbitration that exists to clear a dispute from the board (D-037 E0), a
divergence mechanic (D-037 C4), a `loyalty` field (F21), a generic unrest bar (F24), and a
tunable low-happiness revolt band (F35). Each of those is a ratified prohibition, not a taste.

## §2.6 THE D-021 OBLIGATION — EVERY GRIEVANCE SOURCE WITH ITS VALVE, IN THE SAME MILESTONE

This is the mandate's hard constraint and the section that binds everything above. F12 requires
that every positive feedback loop ship with a negative loop **that strengthens with amplitude**;
F14 requires the brakes to install with the gas pedal, and names which valves exist at which
milestone: **M5 has valves 1 (expression), 2 (fatigue), 3 (exit), 6 (the state acts by default),
7 (the economy self-heals)**; M8 adds 4 (endurance), 5 (organization decay), 8 (generational
decay).

Every grievance-producing mechanism this design proposes is listed with its valve, its
amplitude-strengthening negative loop, and the milestone in which both would have to land.

| # | PROPOSED grievance source | channel (F13) | valve(s), with milestone | the negative loop that strengthens with amplitude |
|---|---|---|---|---|
| **V1** | **Taxation in kind** injures Dignity directly, with the instrument as the input (F18 — RATIFIED, lands M5) | Exit, Voice | 1 expression · 3 exit · 7 economy self-heals — **all M5** | heavier exaction → more out-migration along the real network (the shipped, bounded channel, F31) → smaller taxable base → less exaction possible. The drain grows with the rate |
| **V2** | **Conscription** buys Safety with Sustenance (F17 — RATIFIED) | Exit, Voice | 1 · 3 · 7 — **all M5** | conscription removes producers → output falls → Sustenance falls → the conscribable base shrinks. Self-limiting at exactly the rate it is pushed |
| **V3** | **Garrison** buys Safety with Dignity (F17) | Voice, Endurance | 1 · 2 fatigue — **M5**; 4 endurance is **M8** | garrisoning consumes upkeep in kind from the settlement it garrisons, so the cost rises with the size of the imposition. **Note:** the Endurance half of this pairing is an M8 valve, so an M5 garrison mechanism must not depend on it |
| **V4** | **Repression** as a remediation verb (F23) | Voice | 1 · 2 — **M5** | RATIFIED and already the required shape: *"repression must be able to CREATE the grievance it suppresses"* (F22). The backfire IS the amplitude-strengthening loop; it is not this lane's invention |
| **V5** | **Closing exits** as a policy (F13 valve 3: *"closing exits is a policy choice that redirects pressure into Voice"*) | forced from Exit into Voice | 1 · 2 · 6 — **M5** | pressure redirected into Voice raises expression, which discharges the stock (valve 1). **This is the weakest pairing in the table and §7 Q-G4 puts it to the Director**, because at M5 Voice reaches only protest and riot: the movements and organization machinery that makes Voice a real alternative is M8 (F14) |
| **V6** | **Franchise withdrawal / closed mobility** — mobility openness feeds the Prospects need, and *"closed societies choke it and pay in grievance"* (`docs/d018-classes-and-needs.md:63`, RATIFIED) | Exit, Voice | 3 · 1 — **M5** | the closed class is also the class the Empire needs (bureaucrats, artisans); choking mobility starves the institutions the regime runs on. Grows with the tightness |
| **V7** | **Administrative reach failure** in the periphery (F21) | Endurance, Exit | **6, the state acts by default — M5.** *"governors, garrisons, and local elites respond with their own competence per the delegation doctrine — order gets restored badly rather than not at all"* (F13) | badly-restored order is cheaper than well-restored order, so the Empire's cost does not diverge as reach fails; it degrades. Valve 6 is the ratified brake and it is already an M5 valve |
| **V8** | **Occupation without legitimacy** generates grievance (D-037 E3 — RATIFIED, lands M6 battle layer) | Exit, Voice | 1 · 3 · 6 · 7 — M5 valves, **available by M6** | **Surfaced rather than asserted:** the M5 valves are available by M6 *if exit is open from an occupied settlement*. An occupation that garrisons the exits closes valve 3 and falls back on V5's weak pairing. §7 Q-G5 |
| **V9** | **Rising expectations** — development raises unrest potential before satisfying it (F25) | Voice | this is itself a **negative loop on development**, not a grievance source needing a valve | RATIFIED, and the tree explicitly declines to patch it: *"Educating your population remains a deliberate gamble; that tension is kept, not patched"* (F25). It is the paired-feedback partner for the whole knowledge/diffusion loop — see §4.5 |

**PROPOSED conformance obligation for any civics packet**, stated so it can be checked rather than
remembered: a packet that adds a row to the left column and cannot fill the right two columns from
mechanisms landing **in its own milestone** has violated F14, and the correct response is to defer
the grievance source, not to ship it and schedule the brake.

**The three corridor tests (F15) bind this design too.** *Ignite-and-burn-out* enters the battery
at M5, so the first packet that ships unrest owes it. *No empty menus* is a structural guarantee
about the D-019 matrix, which is OPEN until M8 — **PROPOSED consequence:** any M5 mechanism that
can freeze a verb must guarantee ≥1 available remediation *by construction* rather than by
consulting a matrix that does not exist. *Collapse is a transition, not a trap* is already
half-shipped: `RevoltSystem` produces statelessness, and statelessness is a legal world state
(F35) — what is missing is who picks the place up afterwards (recovery Q-11).

---

# PART 14 — INSTITUTIONS

## §3.0 THE CONFLICT, SURFACED FIRST, BEFORE ANY DESIGN

The mandate requires this, and it is the honest order. **Two records disagree about how many
things "institution" means, and neither is a ruling:**

- **Source A — four meanings:** *"political module (M7→M8), a need trade-off binding M5 by name
  (D-035), a settlement capability (D-038), and the knowledge-conversion mechanism (the M5
  placeholder)"* — `docs/capability-architecture-decision.md:230-234`.
- **Source B — six meanings:** adds *"argument to the control function"* (D-041) and *"predicate
  operand in class emergence"* (D-018), and states that **two are load-bearing and mutually
  incompatible in kind**: (5) and (6) make an institution a *published scalar*, (1) makes it a
  *composable module*, (3) makes it a *built structure* —
  `docs/milestone-architecture-governance.md:179-198`.

Both reach the same consequence in the same words: *"This must be ruled before anyone writes an
institution packet."* **CR-010 was recommended** (`docs/milestone-architecture-governance.md:299-300`)
**and never written** — `docs/current-state.md:13` records that it *"[has] never existed on any
branch"*. **MEASURED (F38): there is zero mechanical institution in the code tree.** Every
institution decision in the repository is a decision about a thing that does not exist.

**This lane's own finding, and it widens the conflict rather than closing it.** The mandate's
candidate list (organizational actor / knowledge holder / capability holder / governance mechanism
/ infrastructure / some combination) is **not the same list** as either record's. Cross-mapped:

| mandate candidate | repository meaning it corresponds to | status |
|---|---|---|
| governance mechanism | (1) composable political module — Spine `:85`, `:111` | recorded in both records |
| infrastructure | (3) settlement structure — D-038 `:170` (*"an institution's structure"*, a composited draw-time part) | recorded in both |
| capability holder | (5) argument to the control function (D-041 `:35`) and (6) predicate operand in class emergence (D-018 `:25`) — both make it a **published scalar** | recorded; these are the two the governance record calls mutually incompatible in kind with (1) |
| knowledge holder | **not the same as** (4) the knowledge-**conversion** mechanism. A *holder* is where knowing resides; a *converter* is a flow mechanism. Sibling lane C's design separates exactly these (`docs/design/arch-C-knowledge.md:103-121`, PROPOSED there) | **the mandate splits a repository meaning in two** |
| organizational actor | **no repository meaning corresponds.** Nothing in the tree records an institution that *acts* | **new meaning, not previously recorded** |
| — | (2) need trade-off binding M5 by name — D-035 `:86` (F17) | **present in the repository, absent from the mandate's list** |

**INFERRED:** the union of the two lists is at least **eight** distinct meanings, not six and not
four. That is a widening of the CR-010 problem, and it is reported as such. — **DIRECTOR DECISION
REQUIRED.**

## §3.1 THE CANDIDATE MEANINGS — WHAT EACH IMPLIES MECHANICALLY AND WHAT STATE EACH NEEDS

Laid out precisely, none chosen. For each: the mechanical consequence, the state it requires, what
it can and cannot do, and which law bites hardest.

| meaning | mechanically, it would be… | state it needs | what it can do | what it cannot do | the law that bites |
|---|---|---|---|---|---|
| **M-1 organizational actor** | a thing that consumes inputs and produces effects — staff (conserved people), in-kind upkeep, a degradation path under unmet maintenance | a row with identity, settlement, staffing and upkeep state; a Ledger sink for its consumption; **a system that steps it** | be genuinely costly, decay, fail, and be starved — the shipped **Housing** pattern (in-kind upkeep, degradation under unmet maintenance) is the working precedent, and the governance record notes *"Institutions never needed money"* (`:201-204`) | act on another domain's behalf. A single system stepping every institution for every domain is the God object F9 rejects | **law 6** (F3), and D-042 §3.2's God-object clause (F36) |
| **M-2 composable political module** | the regime-module content of Part 13 §2.1 — an enfranchisement vector, a verb disposition, coefficients inside named equations | data (F5) plus a per-Empire latch over a D-020 predicate | make government composable rather than enumerated, exactly as Spine `:85` requires (F7); carry D-037 E6's autonomy/federation/devolution (F22) | be a published scalar at the same time. This is one half of the recorded incompatibility | **law 2** (F1) — a module that carries effects not inside an equation is a bonus bundle |
| **M-3 published scalar / predicate operand** | a `double` a system publishes per settlement, which other systems' D-020 predicates read — the shape *"administrative institutions > threshold"* and *"education institutions > threshold"* already assume (F24) | **nothing new at all.** `VariableRow` and the registry already support it (F26, F29) | be consumed by every domain independently, with **no coordinating owner** — which is precisely the disposition D-042 §14.5 says the capability seam survives under | carry a lifecycle, staffing, cost or failure. A scalar cannot be starved | **none** — this is the only meaning that is fully expressible today with zero new substrate |
| **M-4 built structure / infrastructure** | a thing in a place, composited into the settlement's drawn form (D-038 `:170`) and, if it is more than art, occupying land and consuming construction materials | a settlement-keyed row; `ConstructionSystem` and `HousingRow` are the shipped analogues | make institutional presence visible and physically costly, and give an institution a **location** — which every carrier argument in Part 15 needs | be Empire-scoped. A building is somewhere | **law 1** (F4) if it consumes conserved materials |
| **M-5 knowledge-conversion mechanism** | a flow that turns population and time into knowing — the unratified M5 placeholder's schools/colleges/universities/academies/libraries | whatever the knowledge architecture decides a holding is (sibling lane C's HOLDING register, PROPOSED there) | give inquiry a venue — sibling lane C records the absence of one as its **OWED-3** | exist before the knowledge architecture is ruled | **law 4** (F2) if conversion is ever rate-gated by era |
| **M-6 knowledge holder** | the residence of knowing, distinct from its conversion — an institution that *holds* what no living person currently practises | a (holder, corpus) relation with a non-person holder kind | make "the monastery still has the book though nobody can read it" expressible — the mandate's *lose practical access while retaining knowledge* | be derived from M-5. Converting and holding are different jobs | **law 7** (F4) — is the holding a conserved `long` or an observational `double`? Recovery G-07 asks this and it is unanswered |
| **M-7 need trade-off** | the D-035-C path-6 shape: one institution raises one need and lowers another (F17) | none beyond the needs tables that already ship | be the **only ratified mechanical shape for an institution anywhere in the tree**, and it binds M5 by name | be a general definition. It describes what an institution *does to needs*, not what one *is* | **F16's carrier test** — path 6's own carrier is *"a policy, an institution"*, which is circular until this question is ruled |
| **M-8 argument to the control function** | an input term in D-041 B1's *"Control is COMPUTED each turn from distance, infrastructure and institutions"* (F20) | a published quantity per settlement — i.e. M-3 again, viewed from control's side | let institutional density extend administrative reach, which is D-040 C6's road argument applied to administration (F21) | be a module and a scalar at once — the same recorded incompatibility | **law 2** — a control term that is an unearned constant is a modifier |

## §3.2 THE MANDATE'S TEN EXAMPLES, MAPPED

| example | fits which meaning(s) | what the repository already says | this lane's note |
|---|---|---|---|
| **bureaucracy** | M-3 (the Bureaucrats emergence operand: *"administrative institutions > threshold"*) and M-1 | the Bureaucrats class and its anger repertoire are **frozen** — *"obstruction (state capacity silently drops), corruption, defection"* (F24) | the frozen class definition already *assumes* M-3 exists. No Bureaucrats row ships (F30) |
| **courts** | M-2 or M-1 | **nothing.** The only near-hit in the whole tree is D-037 E2's *"third-party arbitration where an institution exists to arbitrate"* (F22) | a genuine blank. §7 Q-I4 |
| **universities** | M-5 and M-6 | named only in the **unratified** M5 placeholder; the Intelligentsia emergence operand *"education institutions"* is frozen (F24) | recovery Q-09: no milestone owns education, no literacy variable is published (F29) |
| **guilds** | M-2 as *policy*, not as an object | D-021 names *"guild openness"* as a **mobility policy hook**, and *"training institutions"* alongside it (`docs/d021-stability-doctrine.md:39`, RATIFIED) | the tree already treats guilds as a **lever on a flow**, not as an entity |
| **markets** | **none — and this is the important row** | markets ship as a **mechanism**: `PriceSystem` and `TradeArbitrageSystem` with a transport-cost deadband over the real network (F32) | **PROPOSED finding:** an institution abstraction that tries to absorb "market" would re-abstract a shipped system. See the God-object test, condition (c) |
| **military organizations** | M-2 and M-1 | the Soldiers class emerges on *"standing professional force exists"* (F24); `NotableRow` carries `Allegiance` and defection is a conserved Ledger move (F34) | the *organizational* half has a shipped conservation surface and no driver (F34) |
| **scientific institutions** | M-5 and M-6 | nothing ratified; sibling lane C records inquiry's venue as **OWED-3** | blocked behind the knowledge architecture and behind CR-010 simultaneously |
| **religious organizations** | M-7 and M-2 | **the most ratified of the ten.** D-021 valve 4 Endurance: *"Faith access, community institutions, and habituation absorb grievance into acquiescence"* (F13); D-035-C path 7 confirms it operates after aggregation and is *"Explicitly legal"* (F17); the Clergy *produce* legitimacy (F24) | the one example whose mechanical job is already ruled, in three places |
| **public works organizations** | **none, in the same way as markets** | public works ship as mechanisms: `PathBuildSystem`, `ConstructionSystem`, `HousingRow` with in-kind upkeep | a second instance of the absorption hazard |
| **regulatory institutions** | M-2 as *policy* | D-021 rules union legality as a **policy** — *"Legality policies (ban/tolerate/recognize unions) move it — banning lowers organization but raises grievance"* (`:41`, RATIFIED) | regulation is already a policy shape with a ratified paired consequence |

**PROPOSED reading.** Three of the ten (markets, public works, guilds/regulation) already exist in
the tree as mechanisms or policies. One (religious organizations) already has a ratified mechanical
job. One (bureaucracy) is already assumed to be a published scalar by a frozen class predicate.
The remaining five are blank or blocked. **INFERRED:** whatever CR-010 rules, the ruling should be
tested against the five that already work rather than against the five that are empty, because the
absorption hazard is the live risk and the empty ones cost nothing to leave empty.

## §3.3 AVOIDING ANOTHER GOD OBJECT — THE TEST, MADE CHECKABLE

The mandate asks explicitly for a checkable test. **PROPOSED — the God-object test.** A proposed
institution abstraction is a God object if **any one** of these is true:

**(a) Ownership breadth.** Any single table or system holds state that more than one domain must
both read *and* write. *Grounding:* D-042 §3.2 — *"Domain state stays in its own tables/systems,
sharing a stable Empire identity/key"* (F36). Checkable by listing writers per table.

**(b) Call topology.** Any domain system would have to call, or be called by, the institution
system to get its answer. *Grounding:* law 6 (F3) and the precise reason the `CapabilitySystem`
was rejected — *"Gameplay interdependence is allowed; direct code coupling is not"* (F9).
Checkable by drawing the call graph: if it is a star with the institution at the centre, it fails.

**(c) Verb absorption.** The abstraction takes over a mechanism that already ships — markets,
public works, housing upkeep, path building. *Grounding:* this is a re-implementation wearing an
abstraction, and the shipped system becomes a second source of truth. Checkable against §3.2's
table.

**(d) Meaning count.** One row type carries two of the recorded meanings the governance record
calls *"mutually incompatible in kind"* — published scalar, composable module, built structure.
*Grounding:* `docs/milestone-architecture-governance.md:196-198`. Checkable by reading the row's
fields. **If a proposed row needs all three, the object is the conflict, not a resolution of it.**

**The positive half, so the test is not only a refusal.** PROPOSED: whatever an institution turns
out to be, two things are already ruled about it and constrain any answer. It is **data** — Spine
law 8 names institutions as a content type (F5) — and consumers reach it through **published
variables and the D-020 grammar, consumed independently by each domain**, which is the disposition
D-042 §14.5 says the capability seam survives under (F9's surviving half). An institution design
that satisfies (a)–(d) and speaks only through published state needs no coordinator and therefore
never tempts one.

**This lane does not choose a meaning.** The choice is CR-010's and CR-010 is the Director's.

---

# PART 15 — DIFFUSION

## §4.0 THE THREE SEPARATIONS THIS PART STANDS ON

1. **Knowledge ≠ technology ≠ capability.** RATIFIED (F10, F11). A single "science → tech"
   pipeline collapses concepts ruled non-interchangeable.
2. **Knowing ≠ being able.** Sibling lane C proposes this as the HOLDING/PRACTICE split
   (`docs/design/arch-C-knowledge.md:103-128`). That is a **sibling lane's PROPOSED design, not a
   decision**, and it is cited here as such. This lane adopts the distinction because Part 15's
   own mandate requires it (*"a civilization may KNOW something without the material, institutional
   or skilled capacity to deploy it"*), and marks it PROPOSED accordingly.
3. **No carrier, no coupling.** RATIFIED (F16). Every channel below either names a physical
   carrier from D-035-C's own list — *a good, a purse, a building, a policy, a body, a season* —
   or is marked **CARRIER OWED**. There is no proximity term, no distance-decayed osmosis and no
   "neighbours gradually learn" anywhere in this design, because each of those is the invented
   modifier F16 refuses.

**PROPOSED, defined once — CONTACT EDGE.** An ordered pair of settlements between which something
physically moved this turn. It is not new state: the shipped `TradeFlowRow(From, To, Good,
Quantity)` *is* a contact edge, and `TradeScope` already classifies each one Domestic, Foreign or
Unruled, **derived and never stored** (F32, F33). Diffusion keyed on contact therefore needs no new
classification substrate — only an answer to what crosses.

## §4.1 THE EIGHT CHANNELS

| # | channel | what physically crosses | shipped carrier? | what it CAN carry | what it CANNOT carry |
|---|---|---|---|---|---|
| **1** | **migration** | a person, as a conserved bucket transfer between settlements, keeping the **full bucket key** (F31) | **YES — a body, shipped and bounded.** `MigrationSystem` moves buckets by `Ledger.Transfer`; ADR-025 bounds it | **culture, religion, class membership, age cohort** — every dimension of the key | **anything not in the key.** MEASURED: `BucketRow` has no skill, literacy or knowledge field (F31). A migrating artisan carries the *class* Artisan and nothing about what they know |
| **2** | **trade** | a good, over connected pairs, across a transport-cost deadband, with **no transit loss** (F32) | **YES for the good — and the contact edge is shipped and derived** (F33). **NO for a person**: `TradeFlowRow` moves goods, and nobody travels with them | an **artefact**, and therefore the *existence* of the thing (see §4.2). It also makes contact itself observable, Domestic/Foreign/Unruled, at zero new cost | **a teacher.** A bronze ingot is not a lesson in casting. Sibling lane C records this as its **OWED-1** |
| **3** | **conquest** | control, people, goods | **PARTIAL.** Control is a shipped relation; `RevoltSystem` shows control can be *removed* (F35); `AppropriationSystem` shows goods can be taken by `Ledger.Transfer` with no new machinery. **`NotableLifecycle.Defects(toSettlement, toAllegiance)` ships a conserved person crossing both settlement and allegiance** (F34) | in principle: control, seized goods, and named people. D-041 B3 already rules that *"territory conquered with its population intact is not the same as territory settled fresh"* (F20) | anything, today. **MEASURED: there is no conquest mechanism, and `NotableLifecycle` has no caller** — the vehicle ships, the driver is owed (F34). **CARRIER OWED** for knowledge specifically |
| **4** | **diplomacy** | envoys, treaty clauses, recognition | **NO.** `RecognitionRow` ships with **no payload** — row presence is the whole fact — and D-037 C7's consequences are explicitly a later packet | nothing today | everything. **CARRIER OWED**, and the mandate names it as such |
| **5** | **education** | a person changing class inside one settlement | **YES, in kind — the carrier is shipped.** Class mobility is a conserved `Ledger.Transfer`, and D-018 rules *"→Intelligentsia via education access"* as one of those flows (F24) | the movement of people between classes at a bounded per-year rate — which is exactly D-021's *"education is a flow with a lag"* (F25) | the thing it is supposed to be gated on. **MEASURED: no literacy or education variable is published (F29), and no Intelligentsia class exists in data (F30).** The carrier is real; the operand is owed |
| **6** | **institutions** | — | **BLOCKED.** D-035-C's own carrier list names *"an institution"* as a legal carrier (F16, F17), which is circular while CR-010 is unruled (Part 14) | — | **CANNOT BE DESIGNED** until Part 14's question is ruled. Stated plainly rather than worked around |
| **7** | **observation** | a scouting party, for geography | **YES for geography, by ruling.** D-040 B1/B2: the map is a **computed extent**, discovery is D-039's reconnaissance at another scale, and *"Do not invent a second exploration system"* (`docs/d040-discovery-and-control.md:37-50`, RATIFIED) | geographic knowledge, which is ruled **monotone**: *"Once known, land stays known"* | observation of another Empire's *practice*. **CARRIER OWED** — and D-040 B2's prohibition means the right answer is to ride the same bodies, not to add a sensing system |
| **8** | **imitation** | — | **NOT A CHANNEL, PROPOSED.** Imitation is what a holder *does* after a carrier has arrived — it is the local response, not the transport | — | making imitation its own channel reintroduces proximity osmosis by the back door, which F16 refuses. This is a design commitment of this lane, PROPOSED, and it is the place a reviewer should attack first |

**PROPOSED, defined once — ARTEFACT DIFFUSION, and this lane knowingly differs from a sibling
lane.** D-035-C's carrier list includes *"a good"* (F16). PROPOSED: a traded good is therefore a
legal carrier for the weaker of the two things that can cross — the *existence* of a technique, not
the *practice* of it. A polity that receives bronze ingots learns that bronze exists and what it
does; it learns nothing about casting. Under sibling lane C's register split, that is a change to
HOLDING with no change to PRACTICE. **Sibling lane C reaches the opposite verdict** and records
trade-borne diffusion as OWED-1 on the ground that *"A good is not a teacher"*
(`docs/design/arch-C-knowledge.md:583`). Both positions are PROPOSED and neither is ratified. The
divergence is surfaced in §6 X-8 rather than resolved, and this lane does **not** claim its reading
is the correct one.

## §4.2 WHY A DISCOVERY DOES NOT BECOME CIVILIZATION-WIDE

Three structural reasons, in decreasing order of how firmly they are grounded.

1. **It is not currently expressible.** MEASURED: `VariableRow` is `(SettlementId, VarId, Value)`
   — settlement-scoped only (F29). D-042 §8.3 *requires* capability evaluation to distinguish
   Empire and Settlement scope (F10), and no Empire-scope substrate exists. "Civilization-wide"
   has no substrate to be wide across. This is the same blocker sibling lane D names at §3.3 and
   the recovery lanes record as C-09 / G-01 / G-05. It blocks diffusion exactly as hard as it
   blocks knowledge. — §6 X-3.
2. **The shipped gate is already local.** The recipe `requires` predicate is evaluated **per
   settlement per turn** against that settlement's PREV variables (F28). Availability is local by
   construction today, in production code, for two live recipes. **MEASURED:** this is not a design
   aspiration; it is the behaviour of the tree.
3. **Conjunction with material and labour.** D-040 B3's sanctioned form is *"a conjunction of
   computed preconditions"* — *"a coastal settlement, timber, and craft capacity"*
   (`docs/d040-discovery-and-control.md:66-70`, RATIFIED). A settlement that knows and lacks the
   deposit has a false conjunct. Knowing is one term among several, never a grant.

## §4.3 KNOWING WITHOUT BEING ABLE — THE FOUR FAILURE MODES

| failure mode | PROPOSED mechanism | status |
|---|---|---|
| **no material** | the conjunct over a deposit or an input good is false | **SHIPPED** — `DepositRow`, and production from deposits already gates on presence |
| **no skilled people** | the conjunct over a class is false — a settlement with no artisans cannot craft, and `artisan_share` already gates two recipes (F28) | **SHIPPED** |
| **no institution** | the conjunct over an institutional operand is false — the shape four frozen class predicates already assume (F24) | **BLOCKED on CR-010** (Part 14) |
| **no reach** | the capital knows and the province does not, because nothing carried it there | **BLOCKED twice** — on Empire scope (§4.2.1) and on the carrier between settlement and Empire (sibling lane C's OWED-4; this lane's FGH-OWED-3) |

## §4.4 NON-MONOTONICITY — LOSING PRACTICAL ACCESS WHILE RETAINING KNOWLEDGE

**PROPOSED, with MEASURED support, and this is the cleanest result in this part.** The asymmetry
the mandate asks for falls out of two shipped mechanisms without inventing anything:

- **Loss of practice is already expressible.** The D-020 latch recedes: *"active + recede true →
  Active = 0"* (F27). A settlement whose artisans fall below the threshold stops being able to
  craft, and the shipped merchant `_doc` explains why hysteresis rather than a single threshold —
  *"a settlement that crosses once stays a merchant town until trade genuinely dries up"*
  (`Sim.Data/content/sim.json:178`). Practical capability is **losable today**.
- **Loss of knowing is not expressible**, because no holding state exists (F29, and sibling lane
  C's register is PROPOSED, not built).

**INFERRED:** that asymmetry is exactly the mandate's target shape — practice lost, knowledge
retained — and it exists because the two live D-020 consumers made *different storage choices*: a
serialized latch for class emergence, a pure-derived predicate for recipe availability (F27, F28).
The tree already ships both storage answers. Which one a knowledge holding should use is a design
choice sibling lane D surfaces at its §3.5 and this lane does not re-decide.

**Independent rediscovery** needs nothing new: if holding is per (holder, corpus entry) and
practice is a local predicate, two Empires reaching the same conjunction independently is the
default, not a special case. **Non-monotonic progress** likewise: a recede is a first-class
outcome, not a failure path.

## §4.5 THE D-021 OBLIGATION FOR DIFFUSION

Diffusion is a positive feedback loop — knowledge raises capability, capability raises output,
output funds more knowing — so F12 requires a negative loop that strengthens with amplitude, in the
same milestone. **PROPOSED, and both candidates are RATIFIED rather than invented:**

1. **Rising expectations** (F25). *"Development raises unrest potential before satisfying it;
   revolutions cluster in modernizing societies, not stagnant ones"*, with salience scaling on
   literacy, urbanization and media exposure. The faster a civilization advances, the more its
   political demands outrun its provision. This strengthens with amplitude by construction, and
   D-021 explicitly refuses to patch it: *"that tension is kept, not patched"* (F25).
2. **Brain drain through valve 3, Exit** (F13). Knowledge concentrates where prospects are best;
   the people who carry it are exactly the people the shipped migration channel moves, keeping
   their full bucket key (F31). The larger the gradient, the larger the outflow. The unratified
   placeholder already requires brain drain to run through the existing migration machinery rather
   than a bespoke one (`docs/m5-research-technology-institutions-placeholder.md` Q4, unratified).

**PROPOSED conformance obligation:** a diffusion packet that ships a knowledge-raises-capability
loop and neither of these brakes has violated F12, and the correct response is to defer the loop.

---

## §5 CARRIERS OWED

D-035-C (F16) requires every cross-system coupling to name a physical carrier — *a good, a purse, a
building, a policy, a body, a season* — and refuses it otherwise. Edges whose carrier this lane
**could** name are recorded inline above. The edges below are the ones it **could not**, listed as
debts rather than hidden. Numbered `FGH-OWED-n` to avoid collision with sibling lane C's
`OWED-1..6`; overlaps are named.

| # | proposed edge | what would have to carry it | why this lane cannot name it | blocks |
|---|---|---|---|---|
| **FGH-OWED-1** | **taxation in kind → a polity-scoped destination** | the transport network carries the grain; the **destination stock does not exist** | Law 1's `Transfer` needs two endpoints and `PolityRow` is *"identity plus command source and nothing else"* (F36). A `Flow` sink would be confiscation-as-annihilation. Recovery **C-03** | Part 13 §2.3 taxation; V1 in §2.6; every "buy off leaders" and "stipend" line in the frozen corpus |
| **FGH-OWED-2** | **conquest → knowledge, people and control** | an army (bodies) and a conquest mechanism | no conquest system ships. The nearest vehicle, `NotableLifecycle.Defects`, ships **with no caller** (F34) | §4.1 channel 3; D-037 E3 |
| **FGH-OWED-3** | **settlement ↔ Empire** — anything crossing between the provinces and the capital | messengers, administration, an itinerant official — a body | nothing of the kind exists, and there is no Empire-scope substrate for it to write into (§4.2.1). **Same hole as sibling lane C's OWED-4** | §4.3 "no reach"; Part 13's Empire-level capability; X-3 |
| **FGH-OWED-4** | **diplomacy → any crossing at all** | an envoy (a body), a treaty (a policy) | no diplomacy system; `RecognitionRow` carries no payload by design | §4.1 channel 4 |
| **FGH-OWED-5** | **institution as a carrier** | a building and the people in it | **circular.** D-035-C path 6 and path 7 both name *"an institution"* as the carrier (F17), and CR-010 has never been written (§3.0). **Overlaps sibling lane C's OWED-3** | Part 14 entire; §4.1 channel 6; V3 in §2.6 |
| **FGH-OWED-6** | **education → the flow's operand** | the mobility `Ledger.Transfer` is the carrier and it **ships**; what is missing is the *quantity it reads* | no literacy or education published variable (F29); no Intelligentsia class in data (F30). Recovery **Q-09**, **C-07**. **Overlaps sibling lane C's OWED-2** | §4.1 channel 5; F25's rising-expectations loop |
| **FGH-OWED-7** | **the Clergy producing legitimacy** | a body (the clergy) and a building (the temple) | D-018 `:23` is the tree's only statement of where legitimacy comes from, and legitimacy has no defined type, scope or dynamics (recovery **Q-03**). A production edge into an undefined quantity cannot name its carrier | Part 13 §2.4; F23's overthrow propensity |
| **FGH-OWED-8** | **military loyalty → the repression check** | the Soldiers class, a notable, an army, or the Empire — the tree does not say whose it is | recovery **Q-07**. F23 makes it an overthrow-propensity input *and* the check repression runs, with no owner | V4 in §2.6; F23's remediation menu |
| **FGH-OWED-9** | **the enfranchisement vector → political weight** | a regime module (a policy) publishing it | D-018 `:62` **requires** every regime module to publish one (F24); no module, no vector, and no definition of what enfranchisement IS (recovery **Q-13**) | Part 13 §2.1; V6 in §2.6 |
| **FGH-OWED-10** | **observation of another Empire's practice** | the same bodies that carry reconnaissance | D-040 B2 forbids inventing a second exploration system, so the carrier must be the existing one — but reconnaissance reports positions, not practices | §4.1 channel 7 |
| **FGH-OWED-11** | **who picks up a stateless settlement** | a polity's control relation being written by something | `RevoltSystem` deliberately declines and names *"M5's politics and M6's war"* as the owners (F35). Recovery **Q-11** | Part 13 §2.5 "lose"; F15's collapse-as-transition test |

**One carrier this lane believes it CAN name, recorded so it can be attacked rather than assumed:**
the carrier for **artefact diffusion** (§4.1) is **the traded good itself**, which is the first
entry on D-035-C's own list. If a reviewer judges that a good cannot carry even the *existence* of
a technique, then §4.1 channel 2 collapses to sibling lane C's OWED-1 and belongs in the table
above.

---

## §6 CONFLICTS SURFACED

Both sources named. Nothing resolved, nothing sided with.

**X-1 — Where institutions live: M5, M7 or M8.**
*Source A:* D-035-C path 6 binds institutions to M5 **by name** — *"M5 must build Safety-vs-Liberty
this way and no other way"*, with an institution as the carrier (`docs/d035-needs-aggregation.md:86`).
*Source B:* the Spine places *"Politics deep (institutions, regime change)"* at **M7**
(`docs/civ-sim-architecture-v3-outline.md:85`, `:111`), and D-011 §6 moves politics and diplomacy to
**M8** (`docs/d011-battle-layer-addendum.md:63`). *Third party:* CR-005 would place institutions at
**M5** and is **OPEN — awaiting director ruling**
(`docs/adr/cr-005-m5-research-technology-institutions-placement.md:3`). Compatible only if
"institution" means two different things in the two places — which is X-2. **Recorded, not
reconciled.**

**X-2 — How many meanings "institution" carries.**
*Source A:* four (`docs/capability-architecture-decision.md:230-234`). *Source B:* six, two of them
*"load-bearing and mutually incompatible in kind"* (`docs/milestone-architecture-governance.md:179-198`).
*This lane's addition, §3.0:* the mandate's own candidate list contains a meaning neither record
records (**organizational actor**), splits one record's meaning in two (**holder** vs
**converter**), and omits one the repository has (**need trade-off**). The union is at least eight.
**CR-010 was recommended and never written** (`docs/current-state.md:13`). — **DIRECTOR DECISION
REQUIRED, unowned.**

**X-3 — Empire-scope capability is required; the substrate is settlement-only.**
*Source A:* *"Capability evaluation must be able to distinguish SCOPE — Empire-level and
Settlement-level"* (`docs/d042-empire-and-player-control-addendum.md:155-156`).
*Source B:* MEASURED — `VariableRow` is `(SettlementId, VarId, Value)`
(`Sim.Core/State/WorldState.cs:394`). The coordinator that would naturally supply Empire scope is
the **rejected** universal `CapabilitySystem` (F9). This blocks Part 13's Empire-level governance
capability and Part 15's entire "civilization-wide" question. — **DIRECTOR DECISION REQUIRED.**

**X-4 — Per-class income versus the ban on individual economic ownership.**
*Source A:* D-018 lists per-class income — Clergy *"stipend/tithe"*, Soldiers *"stipend"*,
Bureaucrats *"stipend"* (`docs/d018-classes-and-needs.md:23-25`), frozen.
*Source B:* D-042 §4.4/§4.6 forbid household wallets, individual citizen wealth and wages
(`:75-80`), and D-042 §14.1 records the finding as *"UNRESOLVED. Owner: director"* (`:237-249`).
Every civics line about stipends, buying off leaders, funding opposition and coup finance sits on
the answer. **Recorded, not reconciled.**

**X-5 — The observability taxonomy, as the brief renders it versus as the document reads.**
*Source A (the mandate's framing):* *"READ / RECOMPUTED / DERIVED / GAP"*.
*Source B (the document, read this pass):* *"READ · SUMMED · DIFFERENCED · RESIDUAL ·
RECOMPUTED"*, with **GAP** as the disposition for anything needing a formula the simulation does not
expose (`docs/observability-architecture.md:19-32`). **DERIVED READING** is a separate concept
belonging to `SettlementHappiness` (F19), not a taxonomy kind. — MEASURED. **And a freshness fact
that matters:** `docs/observability-architecture.md` is **not on `origin/main`** (`dbef61a`) — it
exists on `t4.19-glass-box` and on this branch, and its own header says *"Not merged to main"*.
Anything leaning on it as RATIFIED should say which tree it is standing on.

**X-6 — "institution appears nowhere in the code."**
*Source A:* `docs/milestone-architecture-governance.md:181-183`.
*Source B:* MEASURED this pass — it appears once, in a doc comment at
`Sim.Core/State/WorldState.cs:696`, quoting D-042 §3.2. The substantive claim (zero mechanical
institution) stands; the phrasing has drifted. Recorded per GOV-4, not corrected.

**X-7 — Exit-closing is a ratified policy lever whose counterweight is a later milestone.**
*Source A:* D-021 valve 3 — *"closing exits is a policy choice that redirects pressure into Voice,
with everything that implies"* (`docs/d021-stability-doctrine.md:28`), and M5 ships valve 3
(`:48`).
*Source B:* Voice's escalation machinery — movements, organization, leadership, the D-019 matrix —
is **M8** (`:49`), and D-019 is OPEN until then. *Also MEASURED:* "exit openness" exists today as a
computed term `ω` inside the migration hazard (ADR-025), **not** as a player policy, so the lever
D-021 names does not yet exist in either form. **Recorded, not reconciled.** See §7 Q-G4.

**X-8 — This lane differs from a sibling design lane on trade-borne diffusion.**
*Source A:* `docs/design/arch-C-knowledge.md:583` records trade-borne diffusion as **OWED-1** —
*"goods move, people do not … A good is not a teacher."*
*Source B:* this lane's §4.1, which proposes that a good is a legal carrier for the *existence* of a
technique (D-035-C's list opens with *"a good"*), while agreeing it cannot carry practice. **Both
are PROPOSED; neither is ratified; no sibling lane's file was touched.** Surfaced so the Director
sees the disagreement rather than a silently merged position.

**X-9 — Grievance sources whose valves land in a later milestone.**
*Source A:* F14's landing schedule — M5 gets valves 1, 2, 3, 6, 7; endurance, organization decay and
generational decay are M8 (`docs/d021-stability-doctrine.md:47-50`).
*Source B:* D-037 E3 places occupation-without-legitimacy grievance at the M6 battle layer
(`docs/d037-emergent-polities.md:182-186`), and D-035-C path 7's Endurance buffering — the natural
brake on a garrisoned population — is an M8 valve (F17, F14). The M5 valves are available by M6, so
this is a **tension, not a contradiction**, and it becomes a contradiction only if the occupying
mechanism closes exit. **Recorded as stated, not reconciled.** See §7 Q-G5.

---

## §7 DIRECTOR QUESTIONS

Stated as questions. No preferred answer is attached to any of them.

**Part 13 — civics**

- **Q-G1.** What is **legitimacy**: a derived reading, an accumulated stock held by a population, or
  a split pair (§2.4)? What is its scope — Empire, settlement, or both? *(Recovery Q-03; named as an
  input in four ratified places and defined in none.)*
- **Q-G2.** What is **state capacity**, and what is **military loyalty** a property of — the Soldiers
  class bucket, a notable, an army, or the Empire? *(Recovery Q-06, Q-07; both are inputs to the
  frozen overthrow propensity.)*
- **Q-G3.** Is there a **roster of government forms**, and if not, does the Director accept that a
  form is a derived *label* over a regime module set and never an input to any mechanism (§2.1,
  §2.2)? *(Recovery Q-08: the tree rules what governments may not be and never names one.)*
- **Q-G4.** Is **closing exits** admissible as a policy at M5, given that D-021 rules it redirects
  pressure into Voice while Voice's escalation machinery is M8? *(X-7.)*
- **Q-G5.** When a grievance source lands at M6 (occupation) and the natural brake is an M8 valve,
  does F14's "brakes install with the gas pedal" rule mean the source defers, or that the M5 valves
  suffice? *(X-9.)*
- **Q-G6.** Should a **governance arrangement outlive its preconditions**? D-042 §5.4 preserves
  unrelated accumulated Empire state across a transition (F8); the shipped latch provides no
  monotonic acquisition (F27). *(Recovery C-10 / G-06, the civics face.)*
- **Q-G7.** What is **"laws-lite"**, and what is the **"authority/bandwidth economy"**? Each is named
  once in the frozen Spine M5 line and nowhere else, and the second is given a behaviour only inside
  the government-paralysis mechanic. *(Recovery Q-04, Q-05.)*
- **Q-G8.** What is the **franchise**, mechanically — what changes it, what an election resolves, and
  what publishes the enfranchisement vector D-018 requires of every regime module? *(Recovery Q-13;
  FGH-OWED-9.)*
- **Q-G9.** **Which packet lifts the grievance quarantine?** `scripts/check-read-isolation.sh`
  enforces that nothing reads the needs and grievance tables until M5 (F19). Is that lift a packet
  of its own, and does `SettlementHappiness` then reuse the needs aggregate — which D-021 makes a
  Director's call rather than an engineering one (F19)?

**Part 14 — institutions**

- **Q-I1.** **CR-010: what is an institution?** Which of the eight candidate meanings in §3.1 is the
  canonical one, or is it several, and if several, how do they co-exist without one row carrying two
  meanings the governance record calls mutually incompatible in kind? *(X-2. This lane deliberately
  does not answer.)*
- **Q-I2.** Does the Director accept the **God-object test** (§3.3) as a checkable conformance gate
  for any future institution packet, in the form stated — or in some other form?
- **Q-I3.** Given that **markets, public works and union regulation already ship as mechanisms or
  policies** (§3.2), should an institution abstraction be explicitly forbidden from absorbing an
  already-shipped mechanism?
- **Q-I4.** **Courts** appear nowhere in the repository, and the only near-hit is D-037 E2's
  *"third-party arbitration where an institution exists to arbitrate"*. Is adjudication a subject the
  project intends to model at all?
- **Q-I5.** Which milestone owns **institutions**, given that D-035-C binds them to M5 by name while
  the Spine and D-011 §6 place them at M7/M8 and CR-005 is open? *(X-1.)*
- **Q-I6.** If an institution ends up being a **published scalar** (M-3), does the Director want it to
  be one variable per institution kind per settlement, or an aggregate — bearing in mind the registry
  law that *"ANY emergence predicate that needs scale sensitivity MUST publish an absolute quantity,
  not another ratio"* (`Sim.Core/State/Variables.cs:24-32`)?

**Part 15 — diffusion**

- **Q-H1.** **What is the Empire-scope substrate?** A second scoped variable table, a scope column on
  the existing one, or something else? Until this is answered, "civilization-wide" is not
  expressible and diffusion cannot be specified. *(X-3; recovery G-01 / G-05.)*
- **Q-H2.** Can a **traded good** carry the existence of a technique (§4.1, artefact diffusion), or
  is trade-borne diffusion strictly carrier-owed? Two design lanes disagree. *(X-8.)*
- **Q-H3.** Is **class** an acceptable proxy for know-how in migration? The bucket key carries class
  and nothing about knowing (F31); using class as the proxy risks collapsing two of the eight
  concepts D-042 §8.2 rules non-interchangeable.
- **Q-H4.** Is **imitation** a channel in its own right, or — as §4.1 proposes — the local response
  after a carrier has arrived? A separate imitation channel would need a carrier that is not
  proximity.
- **Q-H5.** **Does knowledge decay, and is decay a conserved-stock event?** This decides whether a
  holding is a law-1 conserved `long` with a named Ledger sink or a `double` observational quantity —
  a law-7 typing question, not a flavour one. *(Recovery G-07.)*
- **Q-H6.** **How is foreign contribution gated?** D-042 §9.6 permits open borders and foreign
  institutions to contribute and sets no conditions; the unratified placeholder expects gating by
  *"distance, diplomacy, language, institutional capacity, literacy and wealth"* and says those rules
  are not decided. Nobody owns them. *(Recovery G-10.)*
- **Q-H7.** **What calibration corridor gates diffusion?** The project gates milestones on corridors,
  and no candidate corridor for knowledge or diffusion is named anywhere in the tree. A milestone that
  cannot be calibrated cannot pass its own exit criteria. *(Recovery G-08.)*
- **Q-H8.** Who owns **education**, and is literacy a bucket property or a settlement-published
  variable? The carrier ships; the operand does not. *(Recovery Q-09; FGH-OWED-6.)*

---

## §8 CAVEATS ON THIS DOCUMENT

1. **Authority.** Nothing here is a ruling, a spec, or a packet. No status was changed, no conflict
   closed, no milestone assigned. Every PROPOSED item is this lane's design and carries no weight
   against a ratified document.

2. **Secondary evidence, declared (GOV-4).** `docs/capability-architecture-decision.md`,
   `docs/milestone-architecture-governance.md`, `docs/m5-roadmap-dependency-audit.md` and the
   `docs/design/recovered-decisions-*.md` lanes are **prior agents' reports, not rulings**. They are
   cited as records of what was concluded. The first of those carries a **CORRECTION NOTICE**
   falsifying two of its own claims, and its adversarial pass returned **SURVIVES_WITH_CONDITIONS,
   not clean survival** (`:7-37`) — so its §4 recommendation is treated as **UNVERIFIED** throughout
   and is never leaned on here.

3. **What was re-verified and what was not.** Every RATIFIED row in §1 was read at its own
   `file:line` on `b6820e2` by this lane. Two recovery claims failed re-verification and are recorded
   as X-5 and X-6 rather than repeated. Claims about branches other than this one
   (`m5-full-build`'s CR-008) are taken from `docs/current-state.md:13` and are **not** re-derived
   here.

4. **Freshness.** `origin/main` is `dbef61a`; this branch is `b6820e2`. `docs/observability-architecture.md`
   and the entire T4.19 observability contract are **not on `main`** (X-5). Anything built on this
   design must re-derive that state rather than inherit this sentence.

5. **No independent reviewer participated.** Under ADR-015 §6, no finding in this document is
   actionable before a verdict returns, and this document contains no finding against code — only
   design, conflicts already recorded elsewhere, and two measured drifts.

6. **What this document did not do.** No civics, institution, diffusion, knowledge, capability or
   polity system was implemented. No schema, row type, constant, threshold, curve, RNG stream, TUNE
   value or data file was proposed for addition. No existing document was edited. No milestone spec
   was written or amended. No sibling lane's file was touched. CR-010 was not answered.
