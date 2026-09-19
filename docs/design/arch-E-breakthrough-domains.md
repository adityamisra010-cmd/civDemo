# ARCH-E — BREAKTHROUGH ARCHITECTURE (PART 7) AND TECHNOLOGY DOMAINS (PART 8)

**DESIGN ONLY. NOTHING IMPLEMENTED.** No production code, schema, golden, corridor,
quarantine, content file or existing document was touched or edited. No table, row, field or
serialized type is designed here. No merge. No M5 code. Created under the Director's
2026-09-19 mandate, PART 7 and PART 8 (design lane E). **The DIRECTOR IS ChatGPT; this lane
rules nothing.** Where a design choice touches or contradicts something RATIFIED, the
conflict is surfaced with both sources and left unresolved — that is a required output of
this lane, not a failure.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `b6820e2`, working
tree clean at the time of reading. Every `file:line` cited below as RATIFIED or MEASURED was
re-read on that tree by this lane.

**Label key** (mandate labelling rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this
phase's design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.
Most of what follows is PROPOSED. That is expected and is stated honestly.

**Read first, and built on rather than re-decided.** This lane read, before designing:
`docs/design/recovered-decisions-knowledge-tech.md` (K-01…K-63, C-01…C-11, G-01…G-14),
`docs/design/recovered-decisions-climate-env-agri.md` (E-01…E-119, §7 WATER, §12 gaps),
`docs/design/recovered-decisions-architecture-invariants.md`,
`docs/design/recovered-decisions-food-disaster-needs.md`,
`docs/design/arch-C-knowledge.md` (sibling lane: the CORPUS / HOLDING / PRACTICE registers)
and `docs/design/arch-D-technology-capability.md` (sibling lane: capability as publication
discipline). Where this lane needs one of their PROPOSED objects it names it as **theirs**
and does not re-derive it; where it disagrees with a sibling lane it says so in §14.

**PROPOSED terms introduced here, defined once and used consistently:** **BINDING SIDE**
(§3), **ARRIVAL HAZARD** (§5), **PRESSURE TERM** (§3). Every other term in this document is
the repository's own — the Spine, D-020 predicate DSL, published variables, D-021 valves,
carrier, corridor, quarantine, latch, packet, law 1..7, Exit/Voice/Endurance, domain lattice
lite, practical discovery, schedule with jitter, structural exclusion, computed era labels.

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This architecture exists to answer one question without ever scheduling its answer: **why
does a thing become doable HERE and not THERE, NOW and not THEN, when nothing in the
simulation knows what year it is.** It separates three objects the word "breakthrough"
normally welds together — the continuous rise of a holding (a state transition), the moment a
PRACTICE predicate first evaluates true (a predicate crossing), and the sentence in the
chronicle that says so (an event) — and it puts randomness in exactly one of the three, behind
an `RngRegistry` stream, with the possibility conditions kept strictly deterministic so that a
structurally unsuited polity has an arrival rate of **exactly zero**, not a small one. It
deliberately does **not** supply a probability formula (the Director has explicitly reserved
that), does **not** define a technology object, does **not** design a table, row, field,
schema, variable id or predicate string, does **not** propose a research-points economy, a
completion event, a tech tree, an era label as an input, or a permanent civilization bonus,
and does **not** resolve a single one of the conflicts it surfaces. Its Part 8 half builds a
domain graph whose edges are grounded in the goods, recipes and equations the repository
**already ships** — `Sim.Data/content/goods.json` and `ProductionSystem`'s harvest equation —
rather than in a parallel invented economy, and every edge either names its D-035-C physical
carrier or is listed in §13 as a debt.

---

## §1 GROUND — THE FACTS THIS DESIGN STANDS ON, VERIFIED THIS PASS

Each re-read by this lane at `b6820e2`.

| # | Fact | Label | Source |
|---|---|---|---|
| **F1** | **Law 4 — no calendar gates.** *"capability derives from computed state, never from dates or era labels"*; Spine form: *"Capability derives from computed state; era labels are descriptive output only."* | RATIFIED (frozen Spine S2) | `CLAUDE.md:19`; `docs/civ-sim-architecture-v3-outline.md:22` |
| **F2** | **Law 2 — mechanisms over modifiers.** *"coefficients inside resolution equations are fine; free-floating permanent buffs are banned"*; Spine form: *"Coefficients *inside* a resolution equation … are legal. Free-floating permanent auras ('+10% happiness') are illegal."* | RATIFIED (frozen Spine S2) | `CLAUDE.md:17`; `docs/civ-sim-architecture-v3-outline.md:20` |
| **F3** | **D-040 B3 — NO TECHNOLOGY UNLOCK.** *"a tech-tree node opening sea travel is a calendar gate wearing a tree."* The sanctioned positive form: *"sea travel becomes possible when the conditions for boats exist — a coastal settlement, timber, and craft capacity — in the same shape as class emergence … A landlocked polity never develops it; a coastal one does; nobody schedules either."* | RATIFIED | `docs/d040-discovery-and-control.md:59-70` |
| **F4** | **The capability seam ships: the D-020 predicate DSL over published variables.** Closed grammar — comparisons `> < >= <= ==`, `&& \|\| !`, *"operand := variableName \| numberLiteral"*, *"No functions, no arithmetic (v1)"*, parsed once at config load with loud rejection; evaluates against the **PREV** turn's rows (one-turn lag). | RATIFIED (shipped contract; closed at `docs/m2-spec.md:8`) | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| **F5** | **Consumer two — recipe availability — is a KNOWLEDGE GATE BY NAME:** *"an optional D-020 availability predicate ('requires') - a knowledge gate over published variables, never a calendar date (law 4)"*. Pure-derived, no stored state: `available[r] = _requires[r] is not { } predicate \|\| predicate.Evaluate(varId => ReadVariable(prev, settlement, varId));` Live on two recipes, both `"requires": "artisan_share > 0.05"`. | RATIFIED (shipped data + code) | `Sim.Data/content/goods.json:3`, `:156`, `:175`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396` |
| **F6** | **The registry publishes exactly four variables** — `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. **None is a knowledge, literacy, education, institution, resource, terrain or infrastructure variable.** Names are code-side; an entry exists only once a system actually publishes it (D-027). | MEASURED (this pass, `b6820e2`) | `Sim.Core/State/Variables.cs:3-8`, `:36-37`, `:60`, `:90`, `:93` |
| **F7** | **REGISTRY LAW — scale invariance.** *"ANY emergence predicate that needs scale sensitivity MUST publish an absolute quantity, not another ratio."* Learned from a measurement (twelve settlements at `3.5 ± 0.1` regardless of size). | RATIFIED (shipped law, stated in code) | `Sim.Core/State/Variables.cs:24-32` |
| **F8** | **`VariableRow` is settlement-keyed and documented as observational:** `record struct VariableRow(SettlementId Settlement, int VarId, double Value)` — *"Observational state, not a stock."* | MEASURED | `Sim.Core/State/WorldState.cs:388-394` |
| **F9** | **The emergence LATCH is hysteresis, not history:** *"Inactive + emerge true → Active = 1; active + recede true → Active = 0 … **Recede absent = never recedes.**"* Corrected in place against two prior records that called it a record that a predicate *"has fired"*. | RATIFIED (shipped) + RATIFIED correction | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction `docs/milestone-architecture-governance.md:242-251` |
| **F10** | **`DisasterSystem` is the shipped shape of a rare stochastic arrival** — *"the second stochastic driver in the simulation, and the first EVENT"*. Hazard integrated exactly: `p = 1 − exp(−λ dt)` — *"exact integration of a per-year hazard (law 3)"*. Determinism: *"One RNG stream per settlement from the registry, keyed (SystemId × settlement id) exactly as HarvestWeather keys its own; state in WorldState"*, with **both uniforms drawn unconditionally** so RNG consumption is constant whatever the outcome. | RATIFIED (shipped, under CR-015/ADR-024) | `Sim.Core/Systems/Disaster/DisasterSystem.cs:9-11`, `:34`, `:56-60` |
| **F11** | **The RNG registry's key is ONE 32-bit integer channel beside the SystemId.** *"One named stream per (system × region), D-007"*; `ulong key = ((ulong)(uint)system.Value << 32) \| (uint)region.Value;` Systems reuse `RegionId` as a generic id channel: `ctx.Rng(new RegionId(id.Value))` in both `DisasterSystem` and `HarvestWeatherSystem`. | MEASURED | `Sim.Core/Kernel/RngRegistry.cs:43-49`, `:68-69`; `Sim.Core/Kernel/SimContext.cs:73`; `Sim.Core/Systems/Disaster/DisasterSystem.cs:109`; `Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:99` |
| **F12** | **A breakthrough-shaped EVENT already ships, observer-side and granting nothing:** `ChronicleEventType.FirstArtisans = 4`, detected by *"data-driven THRESHOLD FUNCTIONS over observable rows only"*. | MEASURED | `Sim.Core/Chronicle/ChronicleCollector.cs:6-17`, `:27-30` |
| **F13** | **The observability taxonomy:** READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED, *"Nothing else is permitted"*, with **GAP** for the rest. *"The logger observes. The simulation calculates. The UI reads."* | RATIFIED | `docs/observability-architecture.md:14-33` |
| **F14** | **A DERIVED READING is the ruled shape for "a condition holds":** SettlementHappiness *"does not accumulate, it does not decay, nothing integrates it, and it is NOT SERIALIZED … A stock can be granted … A derived reading cannot — the only way to move it is to move a condition."* And it is gated by an enforced script from reading the needs/grievance tables. | RATIFIED (director ruling, shipped) | `Sim.Core/State/SettlementHappiness.cs:6-35`; gate `scripts/check-read-isolation.sh:1-25` |
| **F15** | **A second derived reading, and the repository's only shipped answer to "is there a problem here":** `FoodState.Of(prev, s)` and `FoodState.EffectiveDeficit(d, state, cfg)` — *"Famine is a DERIVED state, never a stock"*. Consumed by `DemographicsSystem` and RECOMPUTED by the observer. | RATIFIED (ADR-024 under CR-015) | `docs/adr/adr-024-food-state-effective-deficit-disaster-shock.md:17-19`; `Sim.Core/State/FoodState.cs:73-112`; `Sim.Core/Systems/Demographics/DemographicsSystem.cs:61`, `:248` |
| **F16** | **The shipped materials→manufacturing→agriculture chain, and it is a MECHANISM not a modifier:** `harvest/yr = min(arableKm2 × YieldPerArableKm2PerYear, farmLabor × OutputPerFarmerPerYear × toolFactor)`, `toolFactor = 1 + ToolYieldBonusMax × equipRatio`, `equipRatio = min(1, toolStock / (farmLabor × ToolsPerFarmerToEquip))`; equipped farmers wear tools out through a Ledger sink *"so the bonus DEPLETES: stock raises yield, use consumes stock (law 2 — a mechanism, not a modifier)"*. | RATIFIED (shipped) | `Sim.Core/Systems/Production/ProductionSystem.cs:35-44`, `:227-233`, `:264-271` |
| **F17** | **Crafting labour splits EQUALLY among AVAILABLE recipes** (flat-price case), and output is Leontief-limited by labour and by **every** input stock. Adding or gating a recipe therefore moves the split for every other recipe in the same settlement. | RATIFIED (shipped) | `Sim.Core/Systems/Production/ProductionSystem.cs:58-72` |
| **F18** | **Construction is a GATE, NOT A RATE, with no timer:** *"The head is built this turn iff EVERY material is present in full AND the settlement's construction capacity meets the project's requirement … A cathedral is not '50 turns'; it is a project a village cannot marshal and a city can. Duration is emergent."* | RATIFIED (shipped, M4-D) | `Sim.Core/Systems/Construction/ConstructionSystem.cs:37-48` |
| **F19** | **D-035-C carrier test:** *"**Name the physical carrier — a good, a purse, a building, a policy, a body, a season.** If none exists, it is an invented modifier and is **refused**."* Seven legal paths; *"a coupling that does not fit one of them has not found an eighth path, it has failed the carrier test."* | RATIFIED | `docs/d035-needs-aggregation.md:72-96`, test at `:91` |
| **F20** | **D-021 paired-feedback rule:** *"Every positive feedback loop in the design must ship with at least one negative feedback loop that *strengthens with amplitude*."* Organizing frame: *"grievance expresses through three competing channels — **Exit, Voice, or Endurance** — and the sim always keeps at least one channel open."* | RATIFIED | `docs/d021-stability-doctrine.md:8`, `:24` |
| **F21** | **Terrain rasters are IMMUTABLE after worldgen**, excluded from the per-turn `Clone()`, hash-folded; the reserved upgrade path *"would move the mutated layers into cloned, canonically-serialized state — a director-approved ADR at that milestone"*. The six shipped layers contain **no soil, no vegetation, no pollution, no land-use layer**. | RATIFIED | `docs/adr/adr-008-static-terrain.md:8-15`, `:37-40`; `Sim.Core/Worldgen/TerrainSet.cs:20-27`; recovered at `docs/design/recovered-decisions-climate-env-agri.md:80-81` |
| **F22** | **Water is a COST COEFFICIENT, not a medium:** *"Rivers do NOT change passability; river cells are LAND. Vessels, ports and naval movement are OUT OF SCOPE."* `riverCostFactor` = 0.20, the conservative edge of a ratified band. **Irrigation appears nowhere** in `docs/`, `Sim.Core/` or `Sim.Data/` except as the reference class's stated *exclusion*. | RATIFIED | `Sim.Data/content/sim.json:21-22`; recovered at `docs/design/recovered-decisions-climate-env-agri.md:189`, `:321-324` |
| **F23** | **`StructureRow(SettlementId, int ProjectId, long Count)` is WRITTEN by `ConstructionSystem` and READ BY NO SYSTEM FOR ANY EFFECT.** The only other reference is the `IReadOnlyWorldState` adapter property in `PathBuildSystem`. The granary's storage bound comes from a config constant (`GranaryYearsOfDemand`) that does not consult the structure count. | MEASURED (this pass, `b6820e2`) | `Sim.Core/State/WorldState.cs:744-755`; `Sim.Core/Systems/Construction/ConstructionSystem.cs:115`; `Sim.Core/Systems/PathBuild/PathBuildSystem.cs:355`; bound at `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:310-322` |
| **F24** | **Which side of the Leontief `min()` binds is a first-class, derived property of the world**, and it is independent of the yield: *"land binds whenever a·s·m·outputPerFarmerPerYear > c, at every yield … the labour side has ~3.4× headroom, so the world is land-bound at equilibrium."* | RATIFIED | `docs/adr/adr-013-lattice-denomination-and-agronomic-recalibration.md:190-197` |
| **F25** | **Scarcity must EMERGE, never be hardwired:** *"**The Malthusian trap must EMERGE when land fills.** It must never be hardwired, and it must never be restored by choosing a constant that reproduces the old crash."* | RATIFIED (director ruling) | `docs/adr/cr-003.md:248-260`, recovered at `docs/design/recovered-decisions-climate-env-agri.md:150` |
| **F26** | **"Rapid" is meaningful in turns, not sim-years**, and *"the only shipped discovery chain crosses **three** turn boundaries"*. dt falls 10 → 0.5 across eras; the worst case is the Neolithic, *"where discoveries are rarest"*. | SECONDARY EVIDENCE (a prior agent's report, not a ruling) — declared under GOV-4 | `docs/milestone-architecture-governance.md:173-176`; `docs/capability-architecture-decision.md:306-311` |
| **F27** | **"Schedule with jitter" is the repository's own name for the failure mode this design must avoid:** a predicate whose conjuncts are all constant-true or monotone-in-time. *"It is not prohibited by B3, but it delivers no differentiation and should be recognised as a schedule rather than mistaken for emergence."* | SECONDARY EVIDENCE (CR-007, self-declared resolved; its status is contested — recovery-lane C-01) | `docs/adr/cr-007-b3-exemplar-reconciliation.md:238-240` |

---

# PART 7 — BREAKTHROUGH ARCHITECTURE

## §2 WHAT A BREAKTHROUGH IS: THE THREE-OBJECT ANSWER

The mandate asks whether a breakthrough is an **EVENT**, a **STATE TRANSITION**, or the
**crossing of a predicate**. **PROPOSED: all three exist, they are three different objects,
and the entire failure mode of every technology system this project has rejected is the
welding of them into one.** A tech-tree node is precisely an event, a state transition and a
predicate crossing collapsed into a single row that grants something — which is why F3 calls
it *"a calendar gate wearing a tree"*.

Kept apart, in the order they occur:

| # | object | what it is | kind of state | randomness | who may act on it |
|---|---|---|---|---|---|
| **1** | **the state transition** | a HOLDING's firmness in one corpus region rises (sibling lane ARCH-C §2.1 owns this object) | persistent, non-conserved, `double` sim state | **yes — and only here** (§6) | the system that owns holdings |
| **2** | **the predicate crossing** | a PRACTICE predicate — a D-020 conjunction over published variables — first evaluates true in some settlement | **DERIVED READING. Not stored.** The shipped `requires` shape, F5 | **none. Ever.** | each consuming domain, independently (law 6) |
| **3** | **the event** | the chronicle sentence *"the first bronze was cast at Libur"* | **observer-side detection over observable rows**, the `FirstArtisans` shape (F12) | none | nobody. It grants nothing and can be deleted without changing one bit of the simulation |

**PROPOSED — the load-bearing sentence of Part 7.**

> **The breakthrough IS the state transition. The predicate crossing is its CONSEQUENCE. The
> event is its OBSERVATION. Nothing downstream may read the event, and nothing upstream may
> be caused by it.**

Three properties follow without further machinery, and each is a mandate requirement:

- **Progress is not monotonic**, because object 2 is derived and object 1 can fall. A PRACTICE
  predicate that was true last turn is false this turn the moment a conjunct fails — the timber
  ran out, the artisans receded (F9: *"active + recede true → Active = 0"*), the workshop
  burned. Nothing has to "un-grant" anything, because nothing was granted.
- **Independent rediscovery is free.** Object 1 is per-holder. Two Empires accruing firmness in
  the same corpus region are not competing for a global flag, because there is no global flag.
  This lane notes that the repository's one monotone-by-ruling knowledge quantity is
  *geographic*: *"discovery reports a permanent fact about the land. Once known, land stays
  known"* (`docs/d040-discovery-and-control.md:51-54`, RATIFIED) — and that ruling is about
  the **map**, which is a fact about the world, not about practice, which is a fact about
  people. **INFERRED**, and flagged as a possible over-reading in §14 (X-E3).
- **"Knowing but not being able" needs no new mechanism**, because object 1 and object 2 have
  different storage and neither derives from the other (ARCH-C §2.2's separation, which this
  lane adopts rather than re-derives).

### 2.1 What this lane does NOT claim

**PROPOSED, stated as a limit.** This lane does **not** claim that object 1 must exist as
designed by ARCH-C, nor that firmness has one channel or two (ARCH-C's own Q-C7). Part 7's
architecture is indifferent to that: it requires only that **something continuous, holder-
scoped and fallible sits between the conditions and the predicate**, so that a breakthrough
has somewhere to *arrive* that is not the predicate itself. If the Director rules that no such
quantity exists, Part 7 degenerates to "a capability predicate flips when its conjuncts
happen to align" — which is legal, is D-040 B3 exactly, and loses the ability to represent
*attempted and failed* inquiry. That degenerate form is recorded as a genuine fallback, not as
a strawman, and it is **Q-E1**.

---

## §3 "A PROBLEM EXISTS" AS STATE — THE FOUR CANDIDATE READINGS, AND THE PROPOSED ONE

The mandate asks this precisely: is "a problem exists" a measured shortfall, a deficit, an
unmet need, or a bound that binds? **The repository ships all four shapes.** They are not
synonyms and picking between them is the most consequential single choice in Part 7.

| reading | what it already is on this tree | label | why it is or is not the right object |
|---|---|---|---|
| **a measured shortfall** | the food balance `d` in `FoodState` — a ratio of want to have | RATIFIED (F15) | It is a **symptom with no direction**. A settlement short of food learns nothing about *what to do*: plough more land, make more tools, trade, or leave. It cannot aim a breakthrough. |
| **a deficit after a dead zone** | `FoodState.EffectiveDeficit(d, state, cfg)` — the shortfall net of the band that ordinary variance explains | RATIFIED (F15) | Better: it distinguishes noise from a real bite. Still undirected, and still food-only. |
| **an unmet need** | `NeedSatisfactionRow` / `GrievanceRow` | RATIFIED **and ENFORCED-FORBIDDEN as a sim-side reader until M5** — `scripts/check-read-isolation.sh` fails CI on any sim-side reference outside its allowlist, because D-021 defers grievance-driven behaviour to M5 (F14) | **Structurally unavailable.** A breakthrough system that reads needs is a read-isolation violation on the current tree, and the fix is a D-021 ruling, not a packet. Recorded as a hard constraint, not an opinion. |
| **a bound that binds** | which side of a Leontief `min()` is the argmin; whether `equipRatio` has saturated at 1; whether a store is at its capacity ceiling; whether `abundanceSum <= 0.0` | RATIFIED (F24) | **PROPOSED: this is the right object.** It is the only one of the four that names *what is in the way*, which is what makes a breakthrough directional rather than generic. |

### 3.1 PROPOSED — the BINDING SIDE, and the PRESSURE TERM

**PROPOSED (term, defined once).** The **BINDING SIDE** of a resolution equation is the term
that is currently the argmin — the one whose relaxation would change the outcome. The
repository already speaks this way: *"land binds whenever a·s·m·outputPerFarmerPerYear > c"*,
*"the labour side has ~3.4× headroom, so the world is land-bound at equilibrium"* (F24).

**PROPOSED (term, defined once).** A **PRESSURE TERM** is a **published variable** (F6's
existing mechanism, no new machinery) whose value expresses **how hard a named constraint is
biting in this settlement this turn** — dimensionless, in [0, 1] by construction, computed by
the system that owns the equation, and **derived, never stored as a stock** (F14's shape).

Three properties, each of which is why this reading is proposed over the other three:

1. **It is already computed.** `ProductionSystem` must evaluate both sides of the harvest
   `min()` to take the min at all (F16). Publishing *which side bound, and by how much* costs
   the owning system one variable write — the D-027 discipline exactly: *"an entry exists only
   once a system actually publishes it"* (`Sim.Core/State/Variables.cs:3-8`). No system reaches
   into another (law 6). **PROPOSED**, and it is the cheapest thing in this document.
2. **It satisfies the REGISTRY LAW correctly.** F7 forbids differentiating on ratios alone. A
   pressure term is a **ratio** and therefore must **never travel alone**: a pressure predicate
   must conjoin an absolute quantity (`population`, a stock, an extent). **PROPOSED**, and it
   is the one place where a naive "problem → breakthrough" design would silently reproduce the
   T3.1 lockstep the registry law was written from.
3. **It relieves itself, which is the D-021 valve (§8).** A constraint that binds hard drives
   inquiry toward relieving it; relieving it lowers the pressure, which lowers the drive. The
   negative feedback is the same quantity read the other way round, and it strengthens with
   amplitude by construction.

### 3.2 The honest objection to this reading, stated because it is real

**INFERRED.** A pressure term is a *coefficient describing the world*, and a coefficient
describing the world is one refactor away from being a coefficient *acting on* the world —
which is the law-2 line (F2). The fence this lane proposes: **a pressure term may appear (i)
as an operand in a D-020 predicate, and (ii) as a term inside an arrival-hazard rate equation
(§5), and nowhere else. It may never multiply an output.** That fence is PROPOSED and is not
ruled anywhere; it is **Q-E3**.

A second objection, which this lane cannot dispose of: **F25 rules that scarcity must EMERGE
and must never be hardwired.** A pressure term is a *measurement* of emergent scarcity, not a
hardwiring of it — but a pressure term that enters a hazard rate makes scarcity **causally
productive**, and whether that crosses F25's line is not this lane's call. **X-E1**, §14.

---

## §4 THE FIVE CONDITIONS, AND THE STATE THAT EXPRESSES EACH

The Director's causal shape: *knowledge exists + problem exists + resources exist + relevant
skills exist + experimentation/institutional conditions exist → a breakthrough BECOMES
POSSIBLE.* **PROPOSED:** each of the five is a **conjunct over published variables**, never an
edge — F3's ruled form, and F5's shipped form. The table states what each is, who would have
to publish it, and whether it exists on this tree.

| # | condition | PROPOSED state that expresses it | publisher (law 6: the system owning the underlying physics) | exists at `b6820e2`? |
|---|---|---|---|---|
| **1** | **knowledge exists** | a **holding** quantity for the relevant corpus region, published as a variable (ARCH-C's object; ARCH-D §3.3's scope problem applies — knowledge is ruled Empire-scoped, `VariableRow` is settlement-keyed) | a knowledge system that does not exist | **NO.** And the substrate to hold an Empire-scoped variable does not exist either (F8; ARCH-D §3.3) |
| **2** | **a problem exists** | a **PRESSURE TERM** (§3.1) naming the binding side of a shipped resolution equation | the system owning that equation — Production, Consumption, Catchment, Housing | **NO** — but it is derivable **today** from quantities those systems already compute (F16, F24) |
| **3** | **resources exist** | an **absolute** quantity from a conserved stock or a founding endowment: stock on hand, `DepositRow.Abundance`, extraction throughput | Production / Consumption (the sanctioned shared `GoodStocks` holders) | **NO published variable** — the stocks and deposits themselves are shipped (`GoodStockRow`, `DepositRow` at `Sim.Core/State/WorldState.cs:279`) |
| **4** | **relevant skills exist** | a class-composition quantity. **`artisan_share` IS EXACTLY THIS**, and it already gates two technological recipes (F5) | `ClassMobilitySystem` | **YES** — the only one of the five with a live publisher and a live consumer |
| **5** | **experimentation / institutional conditions exist** | a venue-and-slack quantity: person-years not committed to subsistence, plus a place where work happens | **nobody.** `institution` has six recorded meanings and **zero code**; CR-010 was recommended and never written | **NO**, and it is the one condition that lacks even a *definition* |

**MEASURED, and it is the most important number in Part 7: four of the five conditions have
no publisher on this tree, and one of them has no definition anywhere in the repository.**
The breakthrough architecture is therefore **not blocked on architecture** — the conjunction
form is ruled (F3), the grammar ships (F4), the gate ships (F5). **It is blocked on
publishers, on one scope substrate, and on one definition.** This lane reaches the same
conclusion as sibling lane ARCH-D §3.1 by a different route, which is recorded as
corroboration rather than as agreement negotiated between lanes.

---

## §5 POSSIBLE VERSUS HAPPENS — THE TWO-STAGE SHAPE

This is the mandate's central structural question and the place where a formula would be easy
and wrong. **The Director has reserved the probability formula; this lane supplies only the
SHAPE, and deliberately writes no functional form for λ.**

**PROPOSED — the two stages, and the fence between them.**

```
STAGE 1  POSSIBILITY      a D-020 CONJUNCTION over published variables.
                          Deterministic. Derived. No storage. No randomness.
                          FALSE  →  the arrival hazard is EXACTLY ZERO, not small.

STAGE 2  ARRIVAL          a per-sim-year ARRIVAL HAZARD λ over the possible set,
                          integrated exactly as  p = 1 − exp(−λ·dtYears)  (F10),
                          drawn from an RngRegistry stream (§6).
                          Circumstances SCALE λ within the positive branch. They
                          NEVER move it off zero, and they never move it to one.
```

**PROPOSED (term, defined once): the ARRIVAL HAZARD** is the per-sim-year rate at which a
holding makes a discrete upward jump, given that the possibility conjunction holds. It is a
rate, it is per sim-year, it integrates with `dtYears` (law 3), and its integration form is
not a design choice — `1 − exp(−λ dt)` is the shipped, dt-exact form (F10), and the linear
form `λ·dt` is the CR-001 dt-fragility the project has already paid for once
(`Sim.Core/Systems/Consumption/ConsumptionSystem.cs:241-247`, RATIFIED).

**Why the fence is the whole design.** The separation of "exactly zero" from "small" is what
carries B3's structural exclusion (F3). A landlocked Empire does not have a *low* chance of
inventing the keel; it has **no** chance, forever, because a conjunct is false and no
circumstance multiplies its way past a conjunction. Permanent exclusion is **permitted and
valuable, not mandatory** — recovery-lane K-06, from CR-007 §8.3: *"B3 does NOT require that
every capability predicate exhibit permanent structural exclusion … it is illustrative of what
the form makes possible, not a conformance test every predicate must pass."* This lane
therefore does **not** require every breakthrough to have a structurally excluding conjunct,
and says so explicitly so that no later packet cites Part 7 as demanding one.

**What may appear in λ, PROPOSED as a closed list** (the Director rules the formula; this lane
rules only what is *eligible* to be in it):

1. **PRESSURE TERMS** (§3.1) — the problem, pushing.
2. **Absolute quantities** — people doing the work, stock on hand, extent of the market. Required
   by the registry law (F7), not optional.
3. **Holding firmness in ADJACENT corpus regions** — the cross-domain term that makes Part 8's
   graph causal rather than decorative. **And this is the sharpest hazard in the whole design**:
   it is one notation away from *"capability A requires capability B"*, which two records
   propose banning and **no document rules on** (recovery-lane G-04). See §7.3 and **Q-E5**.
4. **Allocation from a persistent directive** — *"research allocation … state describing a
   desired ongoing configuration — it is NOT re-issued every turn to keep working"*
   (`docs/d042-empire-and-player-control-addendum.md:99-106`, RATIFIED).

**What may NOT appear in λ, and this list is not negotiable under the laws:** a turn number, a
sim-year, an era label, a milestone, any quantity monotone in time by construction, any
quantity that cannot fall, `GetHashCode()`, `DateTime`, a `float`, or any per-turn (rather
than per-sim-year) amount. Laws 3, 4, 5 and 7 each independently forbid at least one of these.

**PROPOSED — what "HAPPENS" means at the turn boundary, stated because the atomic turn makes
it non-obvious.** A breakthrough does not interrupt a turn. D-042 §11 binds this: *"Any future
mechanism must preserve deterministic replay and be an explicit temporal/control architecture
— never an ad hoc pause inserted into individual systems"* (RATIFIED,
`docs/d042-empire-and-player-control-addendum.md:188-194`), and CR-006 is not resolved. So the
arrival is booked once per turn per holder, in table row order, exactly as `DisasterSystem`
books its onset (F10) — and the predicate crossing it enables is seen by consumers **one turn
later**, because predicates read PREV (F4). At dt = 10 in the Neolithic that is a decade of
latency on top of a decade of turn, which is the honest cost of the atomic turn and is
recorded rather than engineered around (F26). **Q-E7.**

---

## §6 WHERE RANDOMNESS ENTERS, AND THE EXACT KEYING

**PROPOSED: randomness enters at exactly one place — stage 2's arrival draw — and nowhere
else.** Stage 1 is a pure predicate evaluation; the event (object 3) is an observer-side
detection over rows that already exist. Neither draws.

**The keying, and a MEASURED substrate limit that a design written from memory would miss.**

Law 5 requires all randomness through `RngRegistry` streams with state in `WorldState`
(`CLAUDE.md:20`). MEASURED at `b6820e2`: the registry's key is **`(SystemId, RegionId)`, packed
as `((ulong)system << 32) | (uint)region`** (`Sim.Core/Kernel/RngRegistry.cs:68-69`), and the
only accessor is `SimContext.Rng(RegionId)` (`:73`). Systems already reuse `RegionId` as a
generic 32-bit id channel — `ctx.Rng(new RegionId(id.Value))` keys by **settlement** in both
`DisasterSystem` (`:109`) and `HarvestWeatherSystem` (`:99`).

Consequences, stated plainly:

- **Keying an arrival draw by settlement works today**, with an exact shipped precedent
  (F10/F11). **PROPOSED keying if the breakthrough holder is a settlement:**
  `(BreakthroughSystemId × settlementId)`.
- **Keying by (holder × corpus entry) does NOT fit the shipped key.** There is one 32-bit id
  channel, not two. Packing two ids into it is a stability hazard: the seed derivation is
  `SplitMix64.Mix(_world.Seed) ^ SplitMix64.Mix(key)`, so any packing scheme becomes part of
  the determinism contract and any later change to the corpus size renumbers every stream.
  **This is a real architectural fork and it is not this lane's to settle. Q-E4.**
- **PROPOSED — the draw discipline, imported from the shipped precedent rather than invented:**
  draw a **constant number of uniforms per holder per turn, unconditionally, in a fixed order**,
  whatever the hazard or the outcome. `DisasterSystem`'s reason is the one that matters: *"BOTH
  uniforms are drawn unconditionally, in that fixed order, so RNG consumption is a constant 2
  NextDouble per settlement-turn whatever the hazard or the outcome: λ = 0 leaves the
  RngStreams table bit-identical"* (`DisasterSystem.cs:56-60`, RATIFIED). This is what makes a
  λ = 0 world an **attribution control** rather than a different world, and it is the single
  most useful piece of hygiene Part 7 can inherit.
- **PROPOSED — ordering discipline.** Any argmax over `double`-valued hazards (e.g. "which line
  of inquiry arrives first") uses a composite key with a stable integer tie-break `(score, id)`
  and ships a tie-dense test (`CLAUDE.md:40`). Holders are iterated in table row order or a
  sorted array — never `Dictionary`/`HashSet` iteration (law 5). Flagged here so no packet
  discovers it late.

**PROPOSED, and offered as the option the Director may prefer: randomness is OPTIONAL to this
architecture.** Stage 2 can be made deterministic — an accumulator that crosses a threshold at
a rate set by the same λ, with no draw at all. The architecture is unchanged; only the arrival
distribution differs (a fixed crossing time instead of an exponential one). This lane does not
recommend either, because the choice turns on whether the Director wants two identical worlds
to diverge, which is a design taste question and **Q-E2**.

---

## §7 CIRCUMSTANCE WITHOUT SCHEDULE — THE CENTRAL QUESTION, AND A CONFORMANCE TEST

> *Can a breakthrough be made MORE LIKELY by circumstance without being SCHEDULED by it?*

**PROPOSED: yes, and the distinction is mechanical rather than aesthetic. A circumstance
multiplies a rate. A schedule advances a clock.** The two are distinguishable by a test that
can be run against code, and this lane's main contribution to Part 7 is that test.

### 7.1 Why the naive design fails, stated first

The obvious design — *"λ rises with population, prosperity, trade and time spent working"* —
is a **schedule with jitter** (F27), the repository's own name for it. Every one of those
quantities rises monotonically in a growing civilization, so every civilization arrives at
every breakthrough in roughly the same order at roughly the same time, differing only by
noise. That is a tech tree with a random number generator stapled to it: it satisfies the
letter of law 4 (no date is read) and defeats its purpose entirely. The Spine's goal for this
milestone is the opposite: *"map shows civilizations pulling apart in time"*
(`docs/civ-sim-architecture-v3-outline.md:110`, RATIFIED).

### 7.2 PROPOSED — THE CIRCUMSTANCE TEST (three clauses, all mechanical)

A proposed breakthrough is **circumstantial** rather than **scheduled** iff:

- **(a) FALSIFIABILITY OF EVERY TERM.** Every quantity entering λ or the possibility
  conjunction **can fall**. No term is a turn number, an era, a cumulative-work counter, or any
  accumulator without a decay or recede path. *Rationale:* this is the operational form of F1
  and it is checkable by grep plus one reading of each publisher. A term that can only rise is
  a clock wearing a variable's name.
- **(b) THE ZERO BRANCH IS REACHABLE.** There exists a **reachable** world state — not merely a
  representable one — in which λ = 0 for a whole campaign. *Rationale:* F3's landlocked polity.
  Per K-06 this is **not required of every predicate**; the test's clause (b) is satisfied
  either by exhibiting such a state **or by explicitly recording that this breakthrough has no
  structural exclusion and why**. An unexamined absence is the finding; a stated absence is not.
- **(c) THE TWO COUNTERFACTUALS.** Hold the calendar fixed and vary circumstance → arrival
  times must differ **across seeds and across worlds**. Hold circumstance fixed and vary the
  calendar (turn length, dt schedule, start year) → the arrival **distribution in sim-years**
  must be unchanged. *Rationale:* this is the corridor-independence discipline (S8 §4.1 item 3)
  turned on a mechanism instead of a band: *"a recipe that yields a different bound depending on
  which side of the change you run it on is not a derivation"*
  (`docs/spine-s8-governance-freeze.md:175`, RATIFIED). The second half is also a dt-correctness
  test in disguise and would have caught CR-001's class of defect.

**The test is PROPOSED, not ruled.** It is offered because F27's "schedule with jitter" is
currently a phrase in a contested secondary record (recovery-lane C-01) with no operational
form, and a design phase that hands the Director a checkable test is worth more than one that
hands over a principle. **Q-E6** asks whether it should become a rule.

### 7.3 The honest hazard in clause 3 of §5's eligible-terms list

**PROPOSED and flagged as the sharpest unresolved thing in Part 7.** Cross-domain terms are
what make historical plausibility possible at all: metallurgy really does precede machinery.
But *"holding firmness in an adjacent corpus region raises λ here"* is, structurally, an edge
from one knowledge quantity to another — and two records propose banning exactly this shape:
*"Capability A referencing capability B's latch is how a tree grows back — that is the line to
hold"* (`docs/capability-architecture-decision.md:203-206`, restated at
`docs/milestone-architecture-governance.md:319-321` and `cr-007:233-234`). **No document rules
on it** (recovery-lane G-04).

**PROPOSED distinction, offered for the Director to accept or reject, not asserted:**

| shape | what it is | this lane's reading |
|---|---|---|
| λ here reads **capability B's latch** (a boolean, a stored grant) | a tree edge | the banned shape. It is a node pointing at a node. |
| λ here reads **a physical quantity that domain B's practice produces** — bronze in the store, tools in use, a road on the ground | a **carrier** | **PROPOSED as legal**, because the coupling runs through a good or a building and passes D-035-C (F19) |
| λ here reads **holding firmness in region B directly** | neither, and that is the problem | **DIRECTOR DECISION REQUIRED.** It has no physical carrier — one body of knowledge does not touch another except through a person who holds both. Listed as **OWED-E1** in §13. |

**The consequence, stated because it disciplines all of Part 8:** under the middle row,
*metallurgy → machinery* is not an edge between knowledges. It is **bronze in a store**, which
a machinery PRACTICE predicate conjoins as a material term. That is why every edge in §11 is
required to name a **good, a building, a body, a policy or a season** — and why the ones that
cannot are debts rather than design.

---

## §8 THE D-021 OBLIGATION THIS DESIGN OWES

**RATIFIED (F20):** every positive feedback loop ships with at least one negative loop that
**strengthens with amplitude**, in the same milestone, and the valves are **Exit, Voice,
Endurance**.

**The positive loop Part 7 creates, stated plainly so it can be attacked (PROPOSED):**
pressure binds → inquiry aims at it → holding rises → PRACTICE predicates cross → the binding
constraint relaxes → more surplus and more practitioners → more inquiry capacity → faster
arrival.

**The paired negatives, PROPOSED, each strengthening with amplitude:**

1. **THE PRESSURE TERM IS ITS OWN BRAKE, and this is the structural one.** λ is driven by how
   hard a constraint binds; a successful breakthrough **relaxes that constraint**, which lowers
   the pressure term, which lowers λ. The brake is the same quantity as the gas and it weakens
   exactly in proportion to the loop's success. **This is the design's answer to F20 and it
   costs nothing**, because it is a property of the shape rather than an added mechanism.
2. **The binding side MOVES.** F24 is ratified: relieving the labour side of the harvest `min()`
   does not raise output once **land** binds. A civilization that solves its tool problem then
   faces a land problem, and the pressure term that was driving metallurgy falls to zero while
   a different one rises. **Progress redirects rather than compounds** — which is also F25's
   emergent-scarcity requirement doing its work.
3. **THE RATIFIED ONE, which this design adopts and adds nothing to.** D-021 already rules the
   knowledge loop's own political valve: *"Education is a flow with a lag: literacy investments
   change bucket composition over sim-decades and — via rising expectations (frozen) — raise
   liberty/prospects salience. Educating your population remains a deliberate gamble; that
   tension is kept, not patched"* (`docs/d021-stability-doctrine.md:43`, RATIFIED). Grievance
   discharges through **Exit, Voice or Endurance**.

**Carrier problem with valve 3, stated honestly and identically to sibling lane ARCH-C §4.2:**
it runs through *literacy*, and **there is no literacy variable** (F6). Recorded as **OWED-E5**
rather than assumed.

**A D-021 hazard this lane must declare against its own design.** Valve 1 assumes the
breakthrough relieves the constraint that drove it. If λ is driven by pressure but the arrival
relieves a *different* constraint, the brake never engages and the loop is unpaired. **PROPOSED
fence:** an arrival hazard may only read pressure terms for constraints that the corpus region
it advances can physically relieve. That fence is checkable at config load (the predicate and
the recipe it gates are both data) and it is **Q-E8**.

---

## §9 OBSERVABILITY PLACEMENT

**RATIFIED (F13):** every observability field is READ / SUMMED / DIFFERENCED / RESIDUAL /
RECOMPUTED, or it is a **GAP**. *"Nothing else is permitted."* And *"Nothing new is serialized
merely to be observable."*

**PROPOSED placement, object by object:**

| object | observability kind | note |
|---|---|---|
| holding firmness (object 1) | **READ** | it is a row a system wrote, if such a row exists |
| the possibility conjunction (stage 1) | **RECOMPUTED** | *"a call to a PUBLIC static simulation function on stored state — never a private re-implementation"*. `Predicate.Evaluate` is already that function. `FoodState.Of` and `SettlementHappiness.Of` are the shipped precedents (F14, F15). |
| the pressure term (§3.1) | **READ** (as a published variable) or **RECOMPUTED** | **PROPOSED: whichever the owning system already computes. It must not be re-derived observer-side** — that is the drift F13 forbids by name. |
| the arrival hazard λ | **RECOMPUTED** | the glass-box explanation of *why here and not there* is exactly λ's decomposition into its terms, and `Sim.Core/Observability/Explain/CausalChain.cs` is the shipped machinery for that shape (MEASURED: `:263-264` recomputes `FoodState.EffectiveDeficit` as a `LinkKind.Recomputed` chain link). |
| the breakthrough event (object 3) | **observer-side detection** | the `FirstArtisans` shape (F12). **PROPOSED: it is appended to `ChronicleEventType` and nothing in `Sim.Core/Systems/` may reference it.** |

**PROPOSED, stated as a rule this lane would want held:** if the chronicle event were deleted
and the whole `Sim.Core/Chronicle/` folder removed, the simulation must produce a bit-identical
world hash. If it would not, the event is load-bearing and the design has failed object 3's
separation.

---

# PART 8 — THE DOMAIN GRAPH

## §10 METHOD, AND WHY THE GRAPH STARTS FROM `goods.json`

The mandate's instruction is explicit: ground the graph in the tree the repository already
ships rather than inventing a parallel one. **MEASURED at `b6820e2`** — the shipped economy is
14 goods and 4 recipes plus 2 projects (`Sim.Data/content/goods.json`):

- **food:** grain (numeraire), livestock, fish
- **raw:** timber, stone, clay, copper-ore, tin-ore, fiber, hides
- **processed:** bronze, tools, pottery, cloth
- **recipes:** `pottery-firing` (clay 2.0 + timber 0.5 → 1 pottery) · `weaving` (fiber 3.0 → 1
  cloth) · `bronze-casting` (copper-ore 8.0 + tin-ore 1.0 + timber 2.0 → 1 bronze, `requires:
  artisan_share > 0.05`) · `toolmaking` (bronze 1.0 + timber 1.0 → **2** tools, `requires:
  artisan_share > 0.05`) — inputs and labour are **per execution**, corrected at T3.3
  (`goods.json:3`)
- **projects:** `granary` (timber 40 + stone 20, labour 2.0) · `workshop` (timber 60 + stone 40
  + **tools 10**, labour 8.0)

**Three observations that shape the whole graph, MEASURED rather than asserted:**

1. **A materials → manufacturing → agriculture chain already exists in data and in code**, and
   it terminates in a *production rate term*, not a bonus: bronze → tools → `equipRatio` →
   `toolFactor` → the labour side of the harvest `min()` (F16). **This is the template for every
   edge in §11.**
2. **A SOCIAL conjunct already gates a TECHNOLOGICAL recipe** — `artisan_share > 0.05` on both
   bronze-casting and toolmaking (F5). Anyone drawing a graph in which knowledge flows only
   from technology to society has the shipped arrow pointing the other way.
3. **Domains are not classes of thing; they are classes of BINDING CONSTRAINT.** PROPOSED, and
   it is the organizing principle of §11: a domain is worth naming when there is a distinct
   constraint it relieves, and worth *not* naming when it would relieve the same constraint as
   a domain already on the list. This is what keeps the roster from growing by analogy.

---

## §11 THE PROPOSED DOMAIN ROSTER

**PROPOSED.** Each row: the domain, the binding constraint it relieves, the shipped quantity
that constraint lives in, and whether the tree has a substrate for it today. The Spine's term
for the structure is **domain lattice lite** — *"no tree; domain lattice lite"*
(`docs/civ-sim-architecture-v3-outline.md:84`, RATIFIED, frozen) — parallel independent
lattices, never one graph. This roster is a roster of lattices, not a graph of nodes.

| # | domain | the constraint it relieves | where that constraint lives on `b6820e2` | substrate |
|---|---|---|---|---|
| 1 | **agriculture** | the land side and the labour side of `harvest = min(land, labour × toolFactor)` | `ProductionSystem.cs:227-233`; `YieldPerArableKm2PerYear` = 26.0, `OutputPerFarmerPerYear` = 5.0 (`sim.json:5-6`) | **SHIPPED** |
| 2 | **water** | (a) transport cost along rivers; (b) the moisture channel that prices deposits and fertility | `riverCostFactor` = 0.20 (`sim.json:21-22`); `HinterlandMeans` (`WorldFounding.cs:325-352`) | **PARTIAL.** Water is a cost coefficient, not a medium (F22). Irrigation exists nowhere. |
| 3 | **food preservation** | the two store sinks: spoilage `1 − exp(−0.08·dt)` and the granary capacity ceiling `1.5 × annualGrainDemand` | `ConsumptionSystem.cs:233-322`; `sim.json:43-45` | **SHIPPED as constants; NOT as a mechanism** — see §12.2 |
| 4 | **materials** | extraction throughput, `abundance²/Σ` concentration, and recipe input availability | `ProductionSystem.FromDeposits` (`:287-310`); `DepositRow` (`WorldState.cs:279`) | **SHIPPED** |
| 5 | **construction** | project material and capacity gates, whole-or-nothing | `ConstructionSystem.cs:37-48`; projects in `goods.json` | **SHIPPED as a gate; its OUTPUT is inert** (F23) |
| 6 | **energy** | nothing on this tree | — | **NO SUBSTRATE.** No fuel accounting: timber enters `pottery-firing` and `bronze-casting` as an ordinary Leontief input, not as energy. Naming it a domain now would be inventing one. **Recorded as absent.** |
| 7 | **transport** | edge cost on the traversal lattice, and therefore trade volume and market extent | `TraversalLattice.Build` cost aggregation; `trade_volume` (`Variables.cs:90`) | **SHIPPED** |
| 8 | **manufacturing** | recipe availability, labour-per-execution, and the **equal split of crafting labour among available recipes** (F17) | `ProductionSystem.cs:58-72`, `:394-396` | **SHIPPED** |
| 9 | **medicine** | mortality in the demographic micro-steps | `DemographicsSystem` (ADR-011 exponential survival) | **PARTIAL** — mortality exists; no health quantity, no disease |
| 10 | **sanitation** | nothing on this tree | — | **NO SUBSTRATE.** Density exists (`SizeTier`, housing); no health or contamination quantity for it to bind. |
| 11 | **communications** | D-039's three information quantities — POSITION, STRENGTH, OUTCOME — *"Model them as three quantities, not one information score"* (`d039:13-20`, RATIFIED) | none published | **NO SUBSTRATE**, but the *shape* is ratified and must not be re-invented as one score |
| 12 | **navigation** | D-040 B1's **computed extent** — *"not a visibility flag over pre-known terrain"* | none | **NO SUBSTRATE.** Ruled to land at M7 (`d040:158-162`). D-040 B2: *"Do not invent a second exploration system."* |
| 13 | **warfare** | D-011's battle layer | frozen D-011; **era-gated by its own text** | **OUT OF SCOPE HERE** and **in open conflict with law 4** — recovery-lane C-03, unowned, CR-009 never written |
| 14 | **environmental management** | soil, erosion, salinisation, deforestation, pollution | **nothing — and the wall is structural** | **BLOCKED BY ADR-008** (F21): the layers such a mechanism would write to are immutable, uncloned, hash-folded. The reserved route is a director-approved ADR moving *only the layers that gain writers* into cloned serialized state. |
| 15 | **information / computation** | — | — | **NOT PROPOSED AS A DOMAIN.** Its only pre-modern content is literacy and record-keeping, which this lane places in **communications** (11) rather than splitting. Stated as a choice so it can be overruled. |

**Two domains this lane ADDS, because the repository's own content implies them and the
mandate's list does not name them:**

| # | domain | why the shipped content implies it | constraint it relieves |
|---|---|---|---|
| 16 | **textiles and hides** | `fiber` → `weaving` → `cloth`, and `hides` with no recipe at all — a raw good the roster ships with **no consumer** | Comfort/Shelter basket satisfaction (D-035 shared satisfier: *"cloth → Comfort *and* Shelter bedding"*, `d035-needs-aggregation.md:81`) |
| 17 | **ceramics and containers** | `clay` + `timber` → `pottery`, a processed good whose historical function is **storage** | **PROPOSED, and it is the interesting one:** pottery is the physical carrier that would connect manufacturing to **food preservation** (domain 3) — see §12.2 |

---

## §12 THE EDGES, WITH THEIR CARRIERS — AND THREE WORKED CHAINS

Every edge is expressed the Director's way: *metallurgy → machinery → pumps → irrigation*, never
*"irrigation technology → +20% food"*. The carrier column is D-035-C's test (F19): **a good, a
purse, a building, a policy, a body, a season.** No carrier → the edge goes to §13.

| # | edge | the coupling, stated physically | D-035-C carrier | status |
|---|---|---|---|---|
| E1 | materials → manufacturing | copper-ore + tin-ore + timber are consumed by `bronze-casting`; bronze enters the store | **a good** (the ores, the bronze) | **SHIPPED** |
| E2 | manufacturing → agriculture | tools in the store raise `equipRatio`, which raises `toolFactor` on the **labour side** of the harvest `min()`; use wears them out through a Ledger sink | **a good** (tools) | **SHIPPED** — F16 |
| E3 | skills → manufacturing | `artisan_share > 0.05` gates both metallurgical recipes | **a body** (the artisans) | **SHIPPED** — F5 |
| E4 | manufacturing → construction | `workshop` requires **10 tools** as a material | **a good** (tools) | **SHIPPED** (the project gate) |
| E5 | construction → food preservation | a granary is a physical vessel of finite size; more granaries hold more grain | **a building** (the granary) | **CARRIER NAMED, MECHANISM ABSENT** — §12.2, and it is a MEASURED gap (F23) |
| E6 | ceramics → food preservation | fired pottery is the sealed container that keeps grain from moulds, insects and rodents — the three named drivers of `grainSpoilagePerYear` (`sim.json:43`) | **a good** (pottery) | **PROPOSED** — §12.2 |
| E7 | water → transport | rivers price a traversal block at `r`, lowering edge cost | **a season/a place** (the river, as a cost coefficient inside `TraversalLattice.Build`) | **SHIPPED** — F22 |
| E8 | transport → market extent → skills | cheaper edges raise realised trade; `trade_volume` is published; merchants emerge on `trade_volume > 200 && population > 520` | **a good in motion** (the traded good), **a path** | **SHIPPED** — §12.3 |
| E9 | agriculture → skills | `food_surplus_ratio` + `population` gate artisan emergence — surplus feeds a specialist, market extent buys from him | **a good** (the food), **a body** | **SHIPPED** |
| E10 | materials → construction | timber and stone are project inputs | **a good** | **SHIPPED** |
| E11 | environmental management → agriculture | clearance/erosion/salinisation would move `fertility`, which sets `EffectiveArableKm2`, which sets the **land side** of the harvest `min()` | **a season/the land itself** | **BLOCKED** — ADR-008's immutable rasters (F21). The carrier is nameable; the substrate refuses it. |
| E12 | water → agriculture (irrigation) | the Director's own exemplar chain: metallurgy → machinery → **pumps** → irrigation → the land side of the `min()` | **a good** (the pump), **a building** (the channel) | **PROPOSED, and it needs BOTH a new good and a mutable land quantity.** Irrigation appears nowhere in the repository (F22). |
| E13 | medicine/sanitation → demography | mortality rates in the 0.5-year micro-steps | **a body** | **PARTIAL** — mortality ships; no health quantity to move it |
| E14 | communications → command | D-039 A5's four named inputs: literacy, road and signal infrastructure, institutions, competence | **a body** (the messenger), **a building** (the road) | **NO PUBLISHER** — recovery-lane C-07: the dependency is ratified, the variables do not exist |
| E15 | knowledge region → knowledge region | *"metallurgy makes machinery thinkable"* with no physical intermediate | **none this lane can name** | **CARRIER OWED — OWED-E1**, §13. This is §7.3's hazard. |

### 12.1 WORKED CHAIN ONE — MATERIALS → MANUFACTURING → AGRICULTURE (shipped end to end)

**This chain is not proposed. It is MEASURED, running on this tree, and it is the template.**

```
ore geography (DepositRow.Abundance, seeded with depositSpread 0.8/0.9 — "the precondition
  for comparative advantage", goods.json:3)
    → extraction output  pool × w_g × OutputPerExtractorPerYear × abundance_g,
      concentrating as abundance²/Σ                         ProductionSystem.cs:48-54, :287-310
    → copper-ore + tin-ore stocks
    → [GATE] artisan_share > 0.05                            goods.json:156  (a D-020 predicate)
    → bronze-casting, Leontief-limited by labour AND by every input stock
    → bronze stock
    → toolmaking (bronze 1.0 + timber 1.0 → 2 tools)         goods.json:159-175
    → tools stock
    → equipRatio = min(1, toolStock / (farmLabor × 1.0))     ProductionSystem.cs:227-229
    → toolFactor = 1 + 0.3 × equipRatio                      :230
    → laborSide = farmLabor × 5.0 × toolFactor               :233
    → harvest = min(landSide, laborSide)                     :232-233
    → ToolWear Ledger sink at 0.1 per equipped farmer-year   :264-271
```

**Why this is the template, in four properties:**

- **It terminates in a production rate term, not a bonus.** `toolFactor` is a coefficient
  *inside* a resolution equation — law 2's legal form, stated in the code's own words: *"stock
  raises yield, use consumes stock (law 2 — a mechanism, not a modifier)"* (F16).
- **It is self-limiting twice over.** `equipRatio` saturates at 1 (one tool set per farmer), and
  tools wear out at `0.1/yr` through a Ledger sink. A civilization that stops making tools
  loses the gain. **Nothing is permanent.** This is the D-021 valve shape at the level of a
  single coefficient.
- **The binding side can neutralise it.** F24: the world is **land-bound** at equilibrium with
  ~3.4× labour headroom. So the entire metallurgy chain can complete and change **nothing** in
  a land-bound settlement — and that is the correct behaviour, not a bug. **This is the single
  best argument for §3's pressure-term reading:** a breakthrough architecture that does not know
  which side binds will aim inquiry at a constraint that is not binding.
- **It crosses three turn boundaries** (F26, secondary evidence): artisans latch on PREV
  variables, the recipe gate reads PREV variables, and the tool stock reaches the harvest
  equation a turn after it is crafted. At dt = 10 that is thirty sim-years from social
  precondition to first grain. **Any "rapid breakthrough" claim must be stated against this
  number.**

### 12.2 WORKED CHAIN TWO — CERAMICS / CONSTRUCTION → FOOD PRESERVATION → A STORE BOUND (the gap)

```
clay deposit (depositChannel "water", priced meanMoisture²)   WorldFounding.cs:419-431
    → pottery-firing (clay 2.0 + timber 0.5 → 1 pottery)      goods.json:103-120
    → pottery stock                                            [ TODAY: consumed by needs only ]
       ...................................................... PROPOSED EDGE E6 ......
    → grainSpoilagePerYear, currently the fixed constant 0.08
      "threshed cereal in pre-modern FIXED storage (mud-brick or pit granary, no chemical
       protection, no controlled atmosphere) loses 5-10%/yr to moulds, germination, insects
       and rodents; the midpoint is taken, not tuned. Carrier: decay."   sim.json:43
    → spoilage = 1 − exp(−rate × dt), a Ledger loss with its own reason  ConsumptionSystem.cs:296-303

timber 40 + stone 20 + capacity 2.0 → granary project        goods.json:179-196
    → StructureRow(settlement, projectId, Count)              ConstructionSystem.cs:115
       ...................................................... EDGE E5: MEASURED GAP ......
    → granaryYearsOfDemand = 1.5, a CONFIG CONSTANT that does not consult StructureRow
      "Carrier: a structure of finite size, which grows with the settlement because more
       households means more granaries."                       sim.json:43
    → capacity = 1.5 × annualGrainDemand; overflow is a Ledger loss   ConsumptionSystem.cs:310-322
```

**This chain is the document's most useful finding, and it is a MEASURED fact rather than a
design.** The granary's TUNE doc **names its carrier explicitly** — *"a structure of finite
size, which grows with the settlement"* — and the simulation ships a `StructureRow` that
counts exactly those structures, **and no system reads it** (F23). The building is built, the
row is written, and the bound it is documented to represent is a constant that scales with
population instead.

**PROPOSED, and deliberately minimal:** the food-preservation domain is the place where E5 and
E6 both land, and both have carriers already sitting in the tree — **a building** (the granary,
counted) and **a good** (pottery, stocked). Neither needs a new good, a new row or a new
predicate operand. What they need is for the owning system to read a row it already has.

**Stated as a caution, not a recommendation:** closing E5 changes a store bound, which changes
the food balance, which changes `FoodState`, which feeds `DemographicsSystem` — and CR-003's
Malthus corridors are **QUARANTINED with their bands frozen**, liftable only on *"a mechanism
legitimately restoring"* them (`docs/design/recovered-decisions-climate-env-agri.md:151`,
citing `cr-003:424`). **This lane does not propose closing it. It records that the carrier is
named, the row exists, and the edge does not — which is a finding a future foundations audit
(S8 §4.1 item 1) would have to record anyway.**

### 12.3 WORKED CHAIN THREE — WATER → TRANSPORT → MARKET EXTENT → SKILLS (shipped end to end)

```
river cells in TerrainSet (static, ADR-008)
    → nodeCost = (1-span)·blockMeanCost + span·0.20, span = min(1, riverCells/stride)  sim.json:21
    → cheaper traversal edges between settlements
    → realised trade: TradeFlowRow(From, To, Good, Quantity)      WorldState.cs:459
    → trade_volume, PUBLISHED per settlement per turn             Variables.cs:90
    → [PREDICATE] "trade_volume > 200 && population > 520"         sim.json:176-178
    → the merchant class latches Active = 1                        ClassMobilitySystem.cs:28-33
       (recede "trade_volume < 50" — the hysteresis band is what makes a spiky
        signal a durable class; a single threshold would have oscillated)
    → merchant adults change the class composition
    → which is the same kind of quantity artisan_share is
    → which gates two technological recipes                        goods.json:156, :175
```

**Why this chain matters to Part 8:** it is a complete, shipped, **geography → capability**
path with no knowledge quantity anywhere in it. A landlocked, riverless, isolated settlement
does not fail to reach merchants because it lacks knowledge — it fails because `trade_volume`
never crosses 200. **That is D-040 B3's structural exclusion, already running.** Any
breakthrough design that inserts a knowledge term *in front of* this chain is adding a
conjunct to something that already discriminates; it should say what the knowledge term adds
that geography does not.

### 12.4 A FOURTH CHAIN, RECORDED BECAUSE IT FAILS

The Director's own exemplar — **metallurgy → machinery → pumps → irrigation** — cannot be
worked end-to-end on this tree, and the honest thing is to say where it stops:

```
metallurgy   → bronze stock                              SHIPPED (chain one)
machinery    → no good, no recipe                        ABSENT from goods.json
pumps        → no good                                   ABSENT
irrigation   → would have to move the LAND side of the harvest min(), i.e. either
               EffectiveArableKm2 or the fertility raster feeding it
                                                          BLOCKED: rasters are immutable,
                                                          uncloned and hash-folded (ADR-008)
```

**MEASURED:** *"irrigation"* occurs in the repository only as the reference class's stated
**exclusion** — *"rain-fed cereal agriculture without irrigation or modern inputs"* — in the
derivation of `sigmaLogYield` (recovered at
`docs/design/recovered-decisions-climate-env-agri.md:321-324`). **INFERRED, and load-bearing:**
adding irrigation therefore does not merely add a mechanism; it moves the world **outside the
reference class from which the shipped weather variance was derived**, and `YieldPerArableKm2PerYear`
= 26.0 was derived as *"what a saturated territory can produce"* for that same rain-fed class
(`sim.json:3-5`). **This is exactly the shape S8 §4.1's foundations audit exists to catch, and
it is recorded here so that no later packet discovers it after building on it. Q-E10.**

---

## §13 CARRIERS OWED

D-035-C (F19) requires every cross-system coupling to name a physical carrier and refuses it
otherwise. The edges whose carrier this lane **could** name are in §12's table. The ones it
**could not** are debts, listed rather than hidden.

| # | proposed edge | what would have to carry it | why this lane cannot name it | blocks |
|---|---|---|---|---|
| **OWED-E1** | **knowledge region → knowledge region** (§5 term 3, §7.3, edge E15) — *"metallurgy makes machinery thinkable"* | a **body** who holds both — a person, a school, a workshop tradition | One body of knowledge does not physically touch another. The only candidate carrier is a holder who holds both, and whether that is expressible is ARCH-C's open Q-C7/§6 territory, not this lane's. Two records propose banning the shape outright (G-04); none rules. | the entire cross-domain half of the domain graph |
| **OWED-E2** | **pressure term → arrival hazard** (§3.1, §5) | a **body** — the people who feel the constraint and change what they do | A pressure term is a *reading of an equation*. The claim that a binding constraint changes behaviour needs a mechanism by which people notice it, and the shipped analogue (needs/grievance) is **enforced-forbidden** as a sim-side reader until M5 (`scripts/check-read-isolation.sh`). | §3, §5, §8 valve 1 — i.e. the core of Part 7 |
| **OWED-E3** | **experimentation / institutional conditions** (§4 condition 5) | a **building** and the **bodies** in it — an institution | *"institution"* has six recorded meanings and **zero code**; CR-010 was recommended and never written (`docs/current-state.md:13`). Naming the carrier means picking a meaning, which is a Director ruling. | §4 condition 5; §5 term 4 |
| **OWED-E4** | **Empire-scope ↔ settlement-scope** — a settlement drawing on its Empire's holding, or contributing to it | messengers, administration, an itinerant scholar | Nothing of the kind exists. The scope KEY exists (`PolityRow`, the `Polities` table); the scoped variable ROW does not (ARCH-D §3.3, MEASURED). No substrate **and** no carrier. | every Empire-scoped conjunct in §4 |
| **OWED-E5** | **knowledge → literacy → rising expectations → grievance** — D-021's ratified valve 3 (§8) | a **body** (a literate person) and a **building** (a school) | **No literacy or education variable exists** (F6), although D-018 and D-021 ratify four mechanisms that depend on one (recovery-lane C-07). Identical to sibling lane ARCH-C's OWED-2. | §8 valve 3 |
| **OWED-E6** | **diffusion across a contact edge** — a breakthrough arriving because a neighbour already has it | a **body** travelling with the goods | MEASURED: the shipped trade artefact is `TradeFlowRow(From, To, Good, Quantity)` — **goods move, people do not**. A good is not a teacher. The Spine ratifies *"diffusion"*; no document says what crosses (recovery-lane G-09). Identical to ARCH-C's OWED-1. | §11 domain 7 → all domains; the Spine's divergence goal |
| **OWED-E7** | **environmental management → agriculture** (edge E11) | the **land itself** (a season, in D-035-C's vocabulary) | The carrier is nameable; the **substrate refuses it**. ADR-008 makes the rasters immutable, uncloned and hash-folded, and the reserved route is a director-approved ADR per layer (F21). This is a substrate debt, not a carrier debt, and is listed here so it is not mistaken for a design gap. | §11 domain 14; §12.4 |

**Carriers this lane believes it CAN name, recorded so they can be attacked rather than
assumed:** E6's carrier is **pottery, a good already in the roster**; E5's is **the granary, a
building already counted in `StructureRow`**; E2's is **tools, a good whose wear already runs
through a Ledger sink**. If a reviewer judges any of the three to be an invented modifier, it
belongs in the table above.

---

## §14 CONFLICTS SURFACED

*Each row names both sources. **No side is picked, nothing is merged, and no status is
changed.** Surfacing is the required output.*

| # | conflict | source A | source B | this lane's disposition |
|---|---|---|---|---|
| **X-E1** | **Does a PRESSURE TERM driving an arrival hazard hardwire scarcity?** CR-003 §5.1 rules *"**The Malthusian trap must EMERGE when land fills.** It must never be hardwired"*. §3.1 proposes publishing how hard a constraint binds and letting it drive λ — which makes measured scarcity **causally productive**. | `docs/adr/cr-003.md:248-260` (director ruling) | this document §3.1, §5 (PROPOSED) | Both recorded. Whether "measuring emergent scarcity and letting it aim inquiry" is inside or outside F25's prohibition is **DIRECTOR DECISION REQUIRED**. This lane does not claim it is safe. |
| **X-E2** | **May a breakthrough system read a pressure term derived from needs?** The most natural expression of *"a problem exists"* for anything but food is need satisfaction. `scripts/check-read-isolation.sh` fails CI on any sim-side reference to the needs/grievance tables outside its allowlist, because D-021 defers grievance-driven behaviour to M5. | `docs/d021-stability-doctrine.md` Part 4 (landing schedule); the enforced gate `scripts/check-read-isolation.sh:1-25` | this document §3, §4 condition 2 | Both recorded. A breakthrough system landing at M7 is **after** M5, so the deferral may simply have expired by then — but nothing states that, and the allowlist is a CI gate, not a milestone clause. **DIRECTOR DECISION REQUIRED.** |
| **X-E3** | **Is knowledge monotone, like the map?** D-040 B2 rules geographic knowledge monotone: *"discovery reports a permanent fact about the land. Once known, land stays known."* This lane (§2) and ARCH-C both treat practice as **non-monotonic**, and the mandate requires losing practical access while retaining knowledge. | `docs/d040-discovery-and-control.md:51-54` (RATIFIED) | this document §2; `docs/design/arch-C-knowledge.md:466-511` (PROPOSED) | Both recorded. This lane reads D-040's ruling as scoped to the **map** and not to practice (INFERRED, §2), and flags its own reading as the thing that could be wrong. **DIRECTOR DECISION REQUIRED** if any later packet relies on the distinction. |
| **X-E4** | **Does a research activity COMPLETE?** The M5 placeholder's scope items 7–11 and the temporal placeholder's §7 assume completion (*"When research crosses completion: the technology becomes available at that point in simulation time"*). CR-007 and the capability record hold that *"a completing project that grants a capability remains prohibited"*. Part 7 is built with **no completion event** (§2, §5). | `docs/m5-research-technology-institutions-placeholder.md:79-84`; `docs/m5-temporal-control-and-player-agency-placeholder.md:154-169` (both declare themselves unratified) | `docs/adr/cr-007-b3-exemplar-reconciliation.md:215`; `docs/capability-architecture-decision.md:375-376`; recovery-lane C-10 | Both recorded. D-042 settles **half** — parallelism yes, one-at-a-time queue banned — and says nothing about completion. **DIRECTOR DECISION REQUIRED**; recovery-lane G-02. Part 7's no-completion assumption is PROPOSED and would have to be revisited if completion is ruled in. |
| **X-E5** | **May λ read another domain's holding directly?** *"Capability A referencing capability B's latch is how a tree grows back — that is the line to hold"* is proposed in two records and **ruled by none**. §5's eligible-term 3 and edge E15 need exactly that reading, or the domain graph has no cross-domain causality except through goods. | `docs/capability-architecture-decision.md:203-206`; restated `docs/milestone-architecture-governance.md:319-321`; `cr-007:233-234` — all **PROPOSED**, never ruled (recovery-lane G-04) | this document §5 term 3, §7.3, OWED-E1 | Both recorded. Note the grammar has **no latch operand today** (`Predicate.cs:18`: operands are a variable name or a number), so the ban is currently enforced by the grammar's shape rather than by a rule. **DIRECTOR DECISION REQUIRED** before the grammar is widened. |
| **X-E6** | **Warfare's era gates versus law 4.** Frozen D-011 gates capability on era labels — *"(**era-gated** additions: bombard, air strike, dig-in)"*, *"Later eras arrive as data + a few new verbs"* — and D-009/D-010 calls bridges and tunnels *"expensive, **era-gated**, terrain-crossing edges"*. Law 4 and D-040 B3 forbid the shape; D-040 flags the D-009/D-010 instance **against itself** and declines to rule. | `docs/d011-battle-layer-addendum.md:13`, `:45`, `:66`; `docs/d009-d010-map-population-addendum.md:12` | `CLAUDE.md:19`; `docs/d040-discovery-and-control.md:59-64`, `:223-227` | Both recorded, by four documents. Part 8 lists warfare (domain 13) as **out of scope here** precisely because of this. **CR-009 was recommended and never written** (`docs/current-state.md:13`). **DIRECTOR DECISION REQUIRED, unowned.** |
| **X-E7** | **Which milestone owns this work.** The frozen Spine says *"Knowledge & diffusion \| M6"*; D-011 §6 resequences to *"M7 knowledge & divergence (was M6)"*; GOV-2 §1c calls the Spine row *"stale by one"* and says reconciliation *"remains REQUIRED"* — and it has not been performed. | `docs/civ-sim-architecture-v3-outline.md:84`, `:110` (frozen) | `docs/d011-battle-layer-addendum.md:62-63`; `docs/m4-pre-spec-dependencies.md:143-153` | Both recorded (recovery-lane C-02). This lane uses **M7** where it must name a milestone, on D-011's authority, and flags that the Spine row disagrees. Not this lane's to reconcile. |
| **X-E8** | **The granary's documented carrier versus the granary's shipped mechanism.** `sim.json:43` states the capacity constant's carrier as *"a structure of finite size, which grows with the settlement"*; `StructureRow` counts exactly those structures and **no system reads it** (F23). | `Sim.Data/content/sim.json:43` (the TUNE doc) | `Sim.Core/State/WorldState.cs:744-755`; `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:310-322` (MEASURED, this pass) | Both recorded. This is a **documentation-versus-code divergence inside a shipped, quarantine-adjacent subsystem**, surfaced here and **not fixed**: CR-003's Malthus corridors are quarantined with frozen bands and this lane changes nothing. It belongs in a foundations audit's table. |

---

## §15 DIRECTOR QUESTIONS

*Stated as questions. No preferred answer is attached to any of them, and none is a proposal
in disguise.*

**Q-E1 — Must something continuous sit between the conditions and the predicate?** §2.1 states
the fallback honestly: without a holding-like quantity, Part 7 degenerates to "a capability
predicate flips when its conjuncts align", which is legal and is D-040 B3 exactly, but cannot
represent inquiry that was attempted and failed. Does the Director want attempted-and-failed
inquiry to be representable?

**Q-E2 — Should a breakthrough's arrival be stochastic at all?** §6 shows the architecture is
indifferent: a deterministic threshold crossing and an exponential arrival differ only in
whether two identical worlds diverge. Which does the Director want?

**Q-E3 — What is the fence on a PRESSURE TERM?** This lane proposes it may appear only as a
predicate operand and as a term inside an arrival-hazard rate, and may never multiply an
output. Is that the fence, a different one, or none?

**Q-E4 — What is the RNG keying for a per-(holder × corpus region) draw?** MEASURED: the
shipped registry key is `(SystemId, RegionId)` with one 32-bit id channel
(`RngRegistry.cs:68-69`), and every shipped consumer keys by settlement. A two-id draw needs
either a packing convention that becomes part of the determinism contract, or a registry
change. Which?

**Q-E5 — May an arrival hazard read holding firmness in another corpus region directly?**
Recovery-lane G-04 records that two documents propose banning the analogous shape for
capability latches and that none rules. Without it, cross-domain causality runs only through
goods and buildings. With it, the tree can grow back through a rate equation instead of
through the grammar.

**Q-E6 — Should the CIRCUMSTANCE TEST (§7.2) become a rule?** It is offered as three
mechanical clauses — every term can fall; the zero branch is reachable or its absence is
stated; the two counterfactuals. Is a test of this kind wanted, and if so does it bind at spec
time, packet time, or verification time?

**Q-E7 — What is a breakthrough's temporal granularity under the atomic turn?** The arrival is
booked once per turn; the predicate crossing is seen one turn later (F4); dt is 10 in the
Neolithic where discoveries are rarest; D-042 §11 forbids an ad-hoc mid-turn pause and CR-006
is unresolved. Is a thirty-sim-year social-precondition-to-first-grain latency (§12.1)
acceptable, or does this cluster need CR-006 answered first?

**Q-E8 — Must an arrival hazard only read pressure for constraints it can relieve?** §8's
D-021 pairing depends on it: if λ is driven by a constraint the arrival cannot relieve, the
negative valve never engages and the loop is unpaired.

**Q-E9 — Is the domain roster (§11) the right cut, and are the two additions wanted?** This
lane declined to name **energy** and **sanitation** as domains because neither has any
constraint on this tree to relieve, declined to split **information/computation** from
communications, and added **textiles/hides** and **ceramics/containers** because the shipped
goods imply them. Each of those five choices is reversible and none is ruled.

**Q-E10 — Does irrigation move the world outside its own derived reference class?**
`sigmaLogYield` = 0.2936 and `YieldPerArableKm2PerYear` = 26.0 were both derived for
**rain-fed cereal agriculture without irrigation** (§12.4, MEASURED). If irrigation is in
scope, does it re-open those derivations, and at which milestone does that audit run?

**Q-E11 — Who owns the `StructureRow` divergence (X-E8)?** A building is built, counted, and
read by nobody, while the constant it is documented to carry scales with population instead.
Is that a queue entry, a foundations-audit row for whichever milestone next touches stores, or
a CR?

**Q-E12 — What, if anything, is a TECHNOLOGY?** Recovery-lane G-14 records that D-042 §9.1
rules knowledge distinct from technology and capability, and that **nothing in the tree says
what a technology IS** — a row, a predicate, a named bundle, or purely a label over a
satisfied capability. Part 7 and Part 8 are both written without needing the object, which is
itself evidence about the answer; but the word appears in the milestone's own name.

---

## §16 CAVEATS ON THIS DOCUMENT

1. **Secondary evidence, declared (GOV-4).** `docs/capability-architecture-decision.md`,
   `docs/milestone-architecture-governance.md`, `docs/m5-roadmap-dependency-audit.md` and
   `docs/adr/cr-007-b3-exemplar-reconciliation.md` are **prior agents' reports by one author**,
   not rulings. Where this document leans on them (F26, F27) the label says SECONDARY EVIDENCE.
   `docs/capability-architecture-decision.md` additionally carries a CORRECTION NOTICE
   falsifying two of its own claims, and its adversarial pass returned
   **SURVIVES_WITH_CONDITIONS, not clean survival**, so *"§4's recommendation remains
   UNVERIFIED"* — nothing from its §4 is used here.
2. **Sibling-lane material is likewise secondary.** `docs/design/arch-C-knowledge.md`,
   `docs/design/arch-D-technology-capability.md` and the five recovered-decisions documents are
   outputs of this same 2026-09-19 mandate, committed hours before this one. They are cited as
   **records of what a sibling lane concluded**, never as ratification. Every RATIFIED or
   MEASURED fact this document leans on was re-read against its own source by this lane.
3. **Tree pin and concurrency.** All MEASURED claims are against `claude/civdemo-work-b1z2y4`
   @ `b6820e2`. Sibling lanes were committing to the same branch during this session; where a
   cited line has since moved, GOV-4 governs — the tree wins on facts, the ruling stands on
   rulings.
4. **No probability formula appears in this document**, by the Director's explicit instruction.
   §5 states the *shape* (`1 − exp(−λ dt)`, per-sim-year, dt-exact) and a closed list of what is
   **eligible** to appear in λ. It writes no functional form, no coefficient and no magnitude.
5. **No independent reviewer participated**, and no adversarial pass was run against this
   design. Everything labelled PROPOSED is one agent's unverified reasoning.
6. **What this document did not do.** No system, table, row, field, schema, variable id,
   predicate string, good, recipe or constant was designed or written. No conflict was
   reconciled. No status was changed. No ruling was made, proposed as ratified, or implied. No
   frozen or ratified document was edited. No production code, JSON, golden, corridor or
   quarantine was touched. No merge was performed.
