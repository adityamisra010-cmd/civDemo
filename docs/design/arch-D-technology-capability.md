# ARCHITECTURE D — TECHNOLOGY / CAPABILITY

**DESIGN PHASE. NOTHING IMPLEMENTED.** No production code, schema, golden, corridor,
quarantine or ratified/frozen document was touched. No milestone spec was written or
amended. No ruling is made here. Created under the Director's 2026-09-19 mandate,
Part 6 (design lane D).

**Author authority: none. The DIRECTOR IS ChatGPT.** Where two sources disagree, both
are named and neither side is taken (§5). Where the repository records no decision, the
question is stated rather than answered (§6).

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `31d8718`,
working tree clean. Every `file:line` below was re-read on that tree by this lane.
Where a prior record's citation has drifted or been falsified by the tree, the drift is
recorded (§5), never silently corrected — GOV-4: the tree wins on facts, the ruling
stands on rulings.

**Label key** (mandate labelling rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this
phase's design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.
Most of what follows is PROPOSED. That is the honest state of this subject.

**Prior lanes read first, as the method requires:**
`docs/design/recovered-decisions-knowledge-tech.md` (K-01…K-63, C-01…C-11, G-01…G-14),
`docs/design/recovered-decisions-architecture-invariants.md` (A-42, A-50…A-52, A-58,
A-60, G-102, C-12), `docs/design/recovered-decisions-civics-institutions.md` (G-080…
G-097, G-118), `docs/design/recovered-decisions-food-disaster-needs.md` (F-169…F-172),
`docs/design/recovered-decisions-climate-env-agri.md` (E-112), and
`docs/design/m4-closure-audit.md`. This lane re-verified every RATIFIED fact it leans
on against its source file:line rather than trusting the recovery rows.

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This document answers one question: **how does a civilization come to be able to do
something it could not do before, and how does it stop being able to do it** — without
a technology tree, without a research-points economy, and without a new system that
owns capability. Its finding, stated first because everything else follows from it, is
that **no new abstraction is required**: the shipped D-020 predicate DSL over published
variables already is the mechanism, the shipped hysteresis latch already makes capability
loss representable, and the work that remains is **additive substrate** — a scope key for
Empire-level answers, published variables for quantities that ratified mechanisms already
assume, and one written choice-rule about storage — none of which is a system and none of
which coordinates anything. What this document **deliberately does not do**: it does not
define knowledge, technology or institution as objects (three of those are unruled and one
is unbuildable — §5, §6); it does not rule the fence on accumulation (G-03); it does not
settle whether a capability predicate may read another capability's latch (G-04 — surfaced
in §3.8, not settled); it does not choose a milestone; it does not propose a packet; and it
does not price anything. It is a shape, its state requirements, and an honest list of what
the shape cannot yet carry.

---

## §1 THE LAW LAYER — WHAT A CAPABILITY MAY NOT BE, VERIFIED ON THIS TREE

The mandate asks that the ban on "technology node researched = permanent civilization
bonus" be stated with citations rather than as taste. It is banned twice over, at the
level of law, and a third time by a director ruling.

| # | The law or ruling, verbatim | Source (verified on `31d8718`) | What it forbids |
|---|---|---|---|
| 1 | *"**Mechanisms over modifiers:** coefficients inside resolution equations are fine; free-floating permanent buffs are banned."* | `CLAUDE.md:17` (law 2) | The **"+ bonus forever"** half. A completed research may not attach a standing multiplier to an owner. RATIFIED. |
| 2 | *"**No calendar gates:** capability derives from computed state, never from dates or era labels."* | `CLAUDE.md:19` (law 4); frozen Spine `docs/civ-sim-architecture-v3-outline.md:22` | The **"node researched"** half, when the node's only real input is elapsed time. RATIFIED. |
| 3 | *"a tech-tree node opening sea travel is a **calendar gate wearing a tree**"* — Civ's research-to-unlock shape *"**REJECTED in that form**"* | `docs/d040-discovery-and-control.md:59-64` (B3) | The unlock **graph** itself. RATIFIED. |
| 4 | The sanctioned positive form: *"sea travel becomes possible when the conditions for boats exist — a coastal settlement, timber, and craft capacity — **in the same shape as class emergence** … A landlocked polity never develops it; a coastal one does; **nobody schedules either.**"* | `docs/d040-discovery-and-control.md:66-70` | — (this is what is permitted) RATIFIED. |
| 5 | *"**Do not collapse these concepts into a single rigid technology tree.**"* | `docs/d042-empire-and-player-control-addendum.md:175` (§9.7) | The tree again, from the newest ruling. RATIFIED. |
| 6 | Anti-pattern list: *"**calendar-date technology unlocks where computed predicates are intended**"*; *"**a rigid one-at-a-time research queue**"* | `docs/d042-…:196-203` (§12) | Both the date gate and the Civ queue. RATIFIED. |

**INFERRED (this lane, not stated in one place by any document):** laws 2 and 4 bite the
banned shape at *different* joints, and this matters for design. Law 4 kills the **input**
(a date or era label may not decide capability); law 2 kills the **output** (the
consequence may not be a free-floating permanent buff). A design can satisfy one and
violate the other. A predicate over computed state whose consequence is a standing
`+10% yield` on an Empire satisfies law 4 and violates law 2. A mechanism-changing
consequence gated on `year > 3000` satisfies law 2 and violates law 4. **A conformant
capability must pass both gates, and they are separate gates.**

**Two further RATIFIED constraints bound every capability answer, and both are usually
forgotten:**

- **Law 6, isolation:** *"systems never reference each other — only `State` and `Kernel`.
  Communication is through tables and events."* (`CLAUDE.md:21`), reaffirmed as
  *"Gameplay interdependence is allowed; direct code coupling is not … Economy may depend
  *conceptually* on knowledge, government, military, transport and institutions — **that
  never justifies a sibling call.**"* (`docs/d042-…:136-140`, §7.1–§7.2). **This is the
  reason the universal CapabilitySystem was rejected — see §2.** RATIFIED.
- **D-021's paired-feedback rule:** *"Every positive feedback loop in the design must ship
  with at least one negative feedback loop that *strengthens with amplitude*. … This rule
  is project-wide."* (`docs/d021-stability-doctrine.md:8`); the valves are Exit, Voice and
  Endurance (`:24`). RATIFIED. §3.9 states what this design owes under it.

---

## §2 THE CENTRAL OBLIGATION — THE REJECTION, RECOVERED PRECISELY

### 2.1 What was rejected, verbatim

`docs/d042-empire-and-player-control-addendum.md:141-142` (§7.3), RATIFIED — D-042 is a
director design ruling of the exempt document class (`:3`, S8 §4):

> *"**Do not create a universal God system such as a `CapabilitySystem`** that owns every
> capability or coordinates every domain."*

Repeated in the anti-pattern list at `:196-203` (§12) as *"a universal `CapabilitySystem`
God object"*.

**What was proposed and thereby rejected:** `docs/capability-architecture-decision.md:194-206`
(§5) proposed **`CapabilityState(scope, domain, capability)`** over a relation
`(scopeKind, scopeId, varId, value)`, *"scope generalized, not fixed to 'civ'"*, with
`domain` carrying no mechanism so that *"no tree; domain lattice lite"* becomes parallel
independent lattices rather than one graph. MEASURED from that document on this tree.

### 2.2 THE PRECISE REASON — it is law 6, and it is structural, not aesthetic

**RATIFIED.** The reason is on the face of the record and is recoverable without
inference: §7 is **titled** *"STATE-MEDIATED DEPENDENCY (reaffirms Law 6)"*, and the
prohibition is clause 3 of a three-clause block whose first two clauses supply the
reasoning (`docs/d042-…:136-140`):

> *"**Gameplay interdependence is allowed; direct code coupling is not.** Systems
> communicate through World State and kernel contracts, never by calling sibling domain
> systems."*
> *"Economy may depend *conceptually* on knowledge, government, military, transport and
> institutions — **that never justifies a sibling call.**"*

**INFERRED (this lane; the record states the rule, not this derivation):** the rejection
follows mechanically from the definition of the thing rejected. A system that *owns every
capability* must, to answer for a domain, either read that domain's private state or be
read by that domain at its own step — so it is a system every other system has an edge to.
That is precisely the sibling coupling law 6 forbids, and the edge count is not incidental
to the design but constitutive of it: **a coordinator is, by definition, the node with an
edge to everything.** The same record forbids the structurally identical Empire God object
at `:60-62` (§3.2) — *"Domain state stays in its own tables/systems, sharing a stable
Empire identity/key"* — and at §12, so the rejection is one instance of a general
anti-pattern rather than an objection to this particular design.

**Two things were NOT rejected, and reading the rejection as wider than it is would be its
own error.** RATIFIED, from the very next section of the same record:

1. **Capability evaluation itself.** `docs/d042-…:146-147` (§8.1): *"**The existing D-020
   predicate machinery is the foundation** for future capability evaluation."*
2. **The scope half of the rejected proposal.** `docs/d042-…:155-156` (§8.3): *"**Capability
   evaluation must be able to distinguish SCOPE — Empire-level and Settlement-level, with
   unit/action scope where required.**"*

And the disposition is stated explicitly at `docs/d042-…:280` (§14.5), RATIFIED: the seam
*"is conformant **only** as a shared predicate grammar consumed independently by each
domain system … and **not** as a coordinating owner."*

**So the rejection is of an OWNER, not of an ABSTRACTION.** It rejects a location for the
answer, not the question.

### 2.3 THE VERDICT THIS LANE REACHES

**PROPOSED (this lane's conclusion, and the mandate requires it be stated plainly):**

> **No new capability abstraction is required. The D-020 predicate seam already carries
> it. What is missing is not an abstraction but SUBSTRATE — a scope key, published
> variables that do not exist, and one written rule about where an answer is stored.**

This is the honest answer the mandate asks for if the distinction cannot be drawn cleanly,
and this lane reached it not because the distinction is hard to draw but because drawing it
produces **nothing that is not already the shipped seam**. The mechanism-level reasoning:

**MEASURED, on this tree.** The seam's two live consumers already demonstrate the whole
shape, and neither goes through an owner:

| | class emergence | recipe availability |
|---|---|---|
| who parses the predicate | `ClassMobilitySystem`, at its own config load | `ProductionSystem`, at its own construction — `ClassMobility.Predicate.Parse(src)` (`Sim.Core/Systems/Production/ProductionSystem.cs:105-108`), validated at load in `Sim.Core/Systems/GoodsConfig.cs:245` |
| what it reads | PREV `VariableRow`s, one-turn lag (`Sim.Core/Systems/ClassMobility/Predicate.cs:22-25`; `Sim.Core/State/WorldState.cs:388-394`) | the same, identically — *"the D-020 gate against PREV published variables — identical wiring to ClassMobility's emergence evaluation"* (`ProductionSystem.cs:388-389`) |
| where the answer lives | `ClassStateRow(SettlementId, ClassId, int Active)` — the owning system's own row (`WorldState.cs:403`) | **nowhere** — recomputed into a `stackalloc bool[]` inside the step and discarded (`ProductionSystem.cs:390-396`) |
| who else must know | nobody | nobody |

**INFERRED (this lane).** That table is the mechanism-level distinction the mandate asks
for, and it is a distinction between the rejected design and *the tree*, not between the
rejected design and a new proposal. The rejected `CapabilityState(scope, domain,
capability)` puts **one table** holding every domain's capability answers and **one place**
that evaluates them. The shipped seam puts the parse in each consumer's constructor, the
evaluation inside each consumer's own step, and the answer — if it is stored at all — in a
row type that consumer owns. `Predicate` is reachable from `ProductionSystem` because it is
an **immutable value type with a static `Parse`**, not a system: the same standing the
project already relies on elsewhere, stated in the tree at
`Sim.Core/State/SettlementHappiness.cs:57-58` — *"This is a pure static function — not a system,
not a table — so reading it crosses no isolation boundary (law 6 governs systems
referencing systems)."* MEASURED.

**Therefore the mandate's alternative applies and this lane takes it:** the honest answer
is that no new abstraction is needed and the D-020 predicate seam already carries it. §3
is a design of the substrate, not of a mechanism.

### 2.4 The conformance test this lane offers, so the rejection is checkable rather than remembered

**PROPOSED — THE NO-OWNER TEST** (new term, defined once, marked PROPOSED). A capability
design is conformant with D-042 §7.3 if and only if both hold:

1. **Deletion independence.** Deleting any one domain system leaves every other domain
   system's capability evaluation compiling and running unchanged. *(A coordinator fails
   this by construction: delete it and nothing can evaluate.)*
2. **Single-writer answers.** No table is written by more than one system and read by a
   third as the authority on *"can X do Y"*. Each stored capability answer is written and
   read by the system that acts on it.

**INFERRED:** both clauses are grep-checkable in the style the project already uses —
`scripts/check-read-isolation.sh` is the shipped precedent for exactly this kind of gate
(an allowlist of legitimate readers of a table, enforced locally and in CI; MEASURED,
`scripts/check-read-isolation.sh:1-30`). This lane proposes the test as a **rule**; it does
**not** propose the script, which would be implementation and is out of this phase's scope.

---

## §3 THE DESIGN

### 3.1 How a capability emerges — the chain, and where each link already lives

D-042 §8.2 rules the chain (`docs/d042-…:148-152`), RATIFIED:

```
State → Published Variables → Predicates → Capabilities → Available Actions
```

and rules that capability is *"**distinct from** raw state, knowledge, research activity,
technology, policy, institution and action"* — eight non-interchangeable concepts.

The mandate asks how a capability emerges from **knowledge + resources + skills +
infrastructure + institutions + circumstances**. **PROPOSED:** every one of those six is a
*conjunct*, never an edge, and each conjunct is a **published variable owned by the system
that owns the underlying physics**. The design is therefore not a capability structure at
all; it is a **publication discipline**, and the table below is its whole content.

| ingredient | the conjunct | who must publish it | does it exist on `31d8718`? |
|---|---|---|---|
| **resources** | an absolute quantity derived from a conserved stock (deposit presence, stock on hand, extraction throughput) | the system owning that stock (Production / Storage / Extraction) | **NO** — MEASURED: the registry ships four variables (`Sim.Core/State/Variables.cs:93`) and none is a resource quantity |
| **skills** | a class-composition quantity — `artisan_share` is exactly this | `ClassMobilitySystem` | **YES** — `Variables.cs:37` (`artisan_share`), live in two recipe gates (`Sim.Data/content/goods.json:156`, `:175`) |
| **circumstances** | a terrain / climate / market quantity: coastal adjacency, moisture, `population`, `trade_volume` | the owning system | **PARTLY** — `population` (`Variables.cs:60`), `trade_volume` (`:90`); no terrain or climate variable is published |
| **infrastructure** | a network/road/housing quantity | PathBuild / Housing | **NO** |
| **institutions** | an institution quantity | **nobody — there is no institution** | **NO.** MEASURED and load-bearing: *"`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or `Sim.Tests/`"* (`docs/milestone-architecture-governance.md:181-183`, secondary evidence; re-checked by this lane on `31d8718` — the term does not occur in any of the four projects) |
| **knowledge** | an Empire-scoped knowledge quantity | a knowledge system that does not exist | **NO**, and the substrate to hold it does not exist either — §3.3 |

**MEASURED, and it is the single most important number in this document:** four of the six
ingredients the mandate names have **no publisher on this tree**, and one of them
(institutions) has no *definition* anywhere in the repository — six meanings across three
milestones, with CR-010 recommended and **never written** (`docs/current-state.md:13`:
*"open CRs include CR-009, CR-010 — **neither has ever existed on any branch**"*). A
capability architecture is therefore not blocked on architecture. **It is blocked on
publishers and on one definition.**

### 3.2 What a capability IS, in this design

**PROPOSED.** A capability is **not a stored object**. It is a **question a domain system
asks of published state, in a grammar every domain shares, at the moment it needs the
answer.** It has no id, no registry, no table and no owner. Three things exist, and only
three:

1. **The grammar** — `Sim.Core/Systems/ClassMobility/Predicate.cs`, a closed comparison and
   boolean DSL over registered variables: *"orExpr := andExpr ('||' andExpr)* · andExpr :=
   unary ('&&' unary)* · unary := '!' unary | '(' orExpr ')' | comparison · compare :=
   operand ('>' '<' '>=' '<=' '==') operand · **operand := variableName | numberLiteral**"*
   (`Predicate.cs:14-18`), *"No functions, no arithmetic (v1)"* (`:20`), parsed once at
   config load with loud rejection. RATIFIED; closed at
   `docs/m2-spec.md:8` under D-020, and closing an open decision at its named spec is not
   an amendment (`docs/spine-s8-governance-freeze.md:22`).
2. **The predicate text** — **data**, in a data file, alongside the thing it gates. RATIFIED
   (`docs/m2-spec.md:8`: *"predicates are data"*).
3. **The answer's storage, IF the domain needs it** — a row type owned by the asking system,
   or no row at all. §3.5 is the choice rule.

**PROPOSED — what this buys, stated as the reason to prefer it over any capability object:**
because a capability has no stored identity, *"capability A requires capability B"* is not
expressible. There is no operand for it (`Predicate.cs:18`: operands are a variable name or
a number, and nothing else). **The tree cannot grow back through a grammar that has no
node type.** That is a structural guarantee, not a discipline someone must maintain — and
it is exactly why §3.8 treats the one way it could be reintroduced as the sharpest open
question in this design.

### 3.3 SCOPE — the one piece of substrate that is genuinely missing

**RATIFIED:** D-042 §8.3 requires capability evaluation to distinguish Empire-level and
Settlement-level (`docs/d042-…:155-156`), and §9.2 makes knowledge *"ultimately
**Empire-scoped**, while produced/applied through settlements, institutions, people and
research activities"* (`:165-166`).

**MEASURED on `31d8718`:** the published-variable substrate is settlement-keyed and only
settlement-keyed — `public record struct VariableRow(SettlementId Settlement, int VarId,
double Value);` (`Sim.Core/State/WorldState.cs:394`), serialized at slot 20, v8
(`Sim.Core/Kernel/CanonicalSchema.cs:363-367`). **There is nowhere for an Empire-scoped
quantity to live.**

**MEASURED — and this FALSIFIES the blocker a prior record calls BLOCKING.**
`docs/capability-architecture-decision.md:56-62` states there is *"**no `PolityRow` type, no
`Polities` table**, and **nothing anywhere in `Sim.Core`/`Sim.Cli` constructs a `PolityId`
outside deserialization**"*, and lists it as conflict #2, BLOCKING (`:371`). On this tree
all three clauses are false: `public record struct PolityRow(PolityId Id, CommandSource
Source);` (`WorldState.cs:704`), the `Polities` table (`WorldState.cs:837`, `:980`),
serialized as schema **v23** (*"v23 (M4, D-042): Polities and Capitals tables appended
after Notables"* — `CanonicalSchema.cs:91`, written at `:543-547`), and constructed at
worldgen: `var player = new PolityId(1); // CR-011 ruling` and the AI roster at
`Sim.Core/Worldgen/WorldFounding.cs:282-302`. **The scope KEY exists. Only the scoped
variable ROW does not.** Recorded as conflict §5.2 and not corrected in the source
document.

**PROPOSED — the scope design, in two candidate shapes, with the trade-off stated:**

- **S1 (additive).** A second row type — `PolityVariableRow(PolityId Polity, int VarId,
  double Value)` — in its own table, appended as a new schema version. The shipped
  settlement path is untouched; every existing golden is untouched; the precedent is exact
  (v23 appended `Polities` and `Capitals` after `Notables`). Under `CLAUDE.md:39` it ships a
  POPULATED-table test with exact `ExpectedLength`, bit-exact round-trip and hash equality —
  empty-table coverage proves nothing.
- **S2 (generalizing).** Widen `VariableRow` to `(int ScopeKind, int ScopeId, int VarId,
  double Value)`. One table, uniform. **Cost:** it rewrites the shipped row width, moves every
  golden hash, and touches the serialization contract the M0 freeze covers
  (`docs/spine-s8-governance-freeze.md:14-16` — *"snapshot/hash format"* is inside the freeze
  perimeter), which is a Contradiction Report, not a packet.

**PROPOSED: S1**, on cost alone — it is additive where S2 is a frozen-contract change, and
the project has an exact precedent for the additive move. **DIRECTOR DECISION REQUIRED** to
choose (§6, Q1); this lane states the trade-off and takes no ruling.

**PROPOSED — the scope-resolution rule, which is the part that actually needs designing.**
Under S1 a predicate could name variables of both scopes in one expression
(`literacy > 0.2 && population > 520`). Resolution stays deterministic if:

1. **A variable id belongs to exactly one scope, declared in the code-side registry.** The
   registry is already code-side and incremental — *"an entry exists only once a system
   actually publishes it"* (`Variables.cs:3-8`, D-027). Adding a scope column to a
   registration is a code constant, not a grammar change. **The D-020 grammar is not
   touched.** This matters: the grammar is closed (`m2-spec.md:8`) and reopening it is an
   amendment.
2. **The consumer supplies the context, not the predicate.** A settlement-scope consumer
   evaluating at settlement *s* resolves settlement-scoped ids at *s* and Empire-scoped ids
   at *s*'s controlling polity. **The polity is derived from `ControlRow`, which D-042 §5
   keeps as the single source of truth** — and the shipped `PolityRow` docstring says so in
   terms: *"Controlled settlements are DERIVED from `ControlRow`, which stays the single
   source of truth (D-042 §5). A `SettlementIds` field here would be a second source of
   truth"* (`WorldState.cs:699-702`). MEASURED.
3. **Unresolvable resolves false, loudly at load where it can be caught.** A settlement
   under no polity's control has no Empire-scoped reading. **PROPOSED:** an uncontrolled
   settlement's Empire-scoped conjunct evaluates false rather than throwing, because the
   alternative is a mid-turn exception on an ordinary world state. **This is a semantic
   choice with teeth and it is not ruled anywhere** (§6, Q2).

**This lane owes a carrier for clause 2 and does not have one — see §4.1.** "A settlement
can draw on its Empire's knowledge" is a cross-scope coupling, and D-035-C requires a named
physical carrier: *"Name the physical carrier — a good, a purse, a building, a policy, a
body, a season. If none exists, it is an invented modifier and is **refused**."*
(`docs/d035-needs-aggregation.md:91`). RATIFIED.

### 3.4 THE LATCH, READ CORRECTLY — and why it is what makes capability loss representable

**RATIFIED, from the shipped code.** `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`:

> *"**EMERGENCE LATCH:** for each non-base class, evaluate its emerge/recede predicates
> against PREV variables. **Inactive + emerge true → Active = 1; active + recede true →
> Active = 0.** The latch (persistent, serialized) IS the hysteresis: with emerge X /
> recede Y < X, a signal oscillating inside the (Y, X) band crosses neither threshold and
> produces at most one transition. **Recede absent = never recedes.**"*

**RATIFIED, and this lane builds on the corrected reading as the mandate directs.**
Two prior records described the latch as *"records that a predicate **has**
fired"*. That is wrong and was corrected in place:
*"**The shipped latch records CURRENT satisfaction under hysteresis, not history** …
Monotonic acquisition exists **only in the special case of an omitted `recede` clause** — a
data choice, not a property of the mechanism. Any design relying on capability outliving its
preconditions must say so explicitly and justify it."*
(`docs/milestone-architecture-governance.md:242-251`, §6.2b; correction notice at
`docs/capability-architecture-decision.md:20-23`; the misdescriptions are at
`docs/m5-roadmap-dependency-audit.md:220-221` and `docs/capability-architecture-decision.md:128-131`).

**Why this matters, and it is the load-bearing sentence of this whole document.** On the
history reading, capability loss needs new machinery: something must *un-fire* a fired
predicate, which means a loss event, a decay rule, a "forget technology" mechanism — three
things that would each need their own carrier and their own valve. On the **correct**
reading, capability loss needs **nothing**:

> **PROPOSED, following directly from the RATIFIED semantics.** A capability is lost when
> the conditions that sustained it stop holding. There is no loss event because there was
> never an acquisition event — the latch is a *reading of the present under hysteresis*,
> not a receipt. **Reversibility is the DEFAULT of the shipped mechanism, and
> irreversibility is the exception you must write down**, by omitting `recede` in the data.

**This inverts the usual burden and that inversion is the design.** In a tech tree, loss is
the special case that has to be built. Here, **persistence** is the special case that has to
be *declared* — and declared in a data file, visibly, one clause at a time, reviewable.
A civilization that loses its artisans because its food surplus collapsed below `recede`
(`"recede": "food_surplus_ratio < 1.1"`, `Sim.Data/content/sim.json:170`) has lost the
recipes those artisans gated (`"requires": "artisan_share > 0.05"`, `goods.json:156`, `:175`)
**without a single line of code about technological regression**. MEASURED: that chain is
live on this tree today; nobody built it as a regression feature and it is one.

**PROPOSED — a term, defined once: the HYSTERESIS BAND is the capability's fragility
budget.** The width of `(recede, emerge)` is exactly how far a civilization may fall before
it loses the capability, and how far it must climb to regain it. It is a TUNE value in a data
file and therefore LIVING under `docs/spine-s8-governance-freeze.md:20`. **Narrow band =
fragile capability; wide band = durable capability; absent `recede` = permanent capability.**
The shipped merchant `_doc` already reasons in exactly these terms — *"a settlement that
crosses once stays a merchant town until trade genuinely dries up, which is what the latch is
for and why a single threshold would have oscillated on and off"* (`sim.json:178`). MEASURED.

**A caution this lane must state rather than smooth over.** "Permanent capability" via an
omitted `recede` is the one shape in this design that comes closest to the banned
*"researched = permanent"* form. It is not the banned form — the acquisition is still a
computed predicate, not a date or a node, so law 4 is satisfied; and the consequence is
still whichever mechanism the domain runs, not a free-floating buff, so law 2 is satisfied.
But **whether a capability may ever be declared irreversible is not ruled** (G-06,
`docs/design/recovered-decisions-knowledge-tech.md` §9), and the tree already contains the
tension: D-042 §5.4 *"A government transition **preserves unrelated accumulated Empire
state**"* (`docs/d042-…:92-93`, RATIFIED) says nothing about whether a capability survives
its preconditions, and the latch does not supply that. §6, Q5.

### 3.5 THE STORAGE CHOICE — latch or pure-derived

**MEASURED: the tree ships two consumers with opposite answers**, and nothing rules which a
new consumer should use. Class emergence stores a serialized latch with hysteresis
(`ClassStateRow`, `WorldState.cs:403`); recipe availability stores nothing and recomputes
every turn with no hysteresis (`ProductionSystem.cs:390-396`).

**PROPOSED — the choice rule, and the criterion is mechanical rather than aesthetic:**

> **Does flipping the answer move a conserved stock?** If yes, hysteresis is **mandatory**
> and the answer is a latch owned by the flipping system. If no, a pure recomputed
> predicate is correct and storing anything is forbidden.

**The derivation, INFERRED by this lane from shipped evidence rather than inherited** (the
same rule appears in `docs/capability-architecture-decision.md:186-192`, but that document's
recommendations are UNVERIFIED by its own header at `:29-37`, so this lane re-derives it):

1. A class flip moves **people**, by `Ledger.Transfer`, under law 1. An oscillating input
   would therefore chatter conserved population back and forth every turn. The shipped
   merchant `_doc` states this consequence explicitly as the reason the latch exists
   (`sim.json:178`, quoted above). MEASURED.
2. A recipe-availability flip moves **nothing**; it changes which recipes the labour split
   runs over, within the same turn's production, and the conserved stocks move by the
   ordinary production path either way (`ProductionSystem.cs:390-400`). MEASURED.
3. **INFERRED:** the distinguishing property is therefore conserved-stock movement, not
   importance, not permanence, and not whether the thing "feels like a technology".

**And the second half of the rule, which matters more for law 2:** storing a capability
answer that nothing needs stored is exactly how a free-floating modifier gets a home. The
observability architecture states the governing principle in general form —
*"Nothing else is permitted. If a quantity would need a formula the simulation does not
expose, it is a **GAP**"*, under the one rule *"The logger observes. The simulation
calculates."* (`docs/observability-architecture.md` §0). **PROPOSED:** a capability answer is
serialized only when a conserved stock depends on its hysteresis; otherwise it is recomputed,
and if someone wants to *see* it, it is RECOMPUTED in the observability sense — a call to a
public static simulation function on stored state — never a new serialized row.

**The shipped precedent for that discipline is `SettlementHappiness`**, and it is the
project's own model for a design of this kind: *"a **DERIVED READING**, never a stock …
recomputed from the world every time it is asked. It does not accumulate, it does not decay,
nothing integrates it, and it is **NOT SERIALIZED** … A stock can be granted: something hands
you +5 and the population is happier with no change in what it eats or where it sleeps. A
derived reading cannot — the only way to move it is to move a condition. The director's
prohibition … is therefore enforced by the SHAPE of this type rather than by a rule someone
has to remember."* (`Sim.Core/State/SettlementHappiness.cs:7-31`). MEASURED. **PROPOSED:
that last sentence is the standard this design holds itself to** — a capability that cannot
be granted, because there is nothing to grant.

### 3.6 THE EIGHT PROPERTIES — what each means mechanically and what state it needs

The mandate asks, for each of eight adjectives, what it means mechanically and what state it
would need. Answered one by one. **"Ships" means present on `31d8718` and MEASURED.**

**(1) LOCAL — YES, ships, no new state.**
Mechanically: the predicate is evaluated per settlement against that settlement's own
`VariableRow`s, so the same capability text yields different answers in different places in
the same turn. State needed: **none**. Both live consumers are already local
(`ProductionSystem.cs:394-396` evaluates per settlement; `ClassStateRow` is keyed
`(SettlementId, ClassId)`). **This is the default and it takes work to make a capability
non-local, not the other way round** — which is the correct bias for a simulation whose
D-042 §4.3 rules *"**Shared ownership does NOT create a single Empire-wide inventory.** Local
stocks stay physically local"* (`docs/d042-…:72-74`, RATIFIED).

**(2) PARTIAL — the word carries two mechanically different things and they must be
separated.**
- **(2a) Partial in EXTENT** — held in some settlements and not others. This is (1). Ships.
  No new state.
- **(2b) Partial in DEGREE** — held everywhere but working badly. **This is NOT expressible
  in D-020 and must not be made so.** The grammar returns a boolean and has no arithmetic
  (`Predicate.cs:17-20`). **PROPOSED:** degree is a **published `double` consumed as a
  coefficient inside the domain's own resolution equation** — which law 2 permits in terms
  (*"coefficients inside resolution equations are fine"*, `CLAUDE.md:17`) — and it is
  **never a fractional latch**. State needed: one published variable per degree quantity,
  owned by the system that computes it, plus the coefficient's placement inside an equation
  the domain already runs. **The hazard, named:** a "degree" that multiplies an outcome from
  outside any equation is the free-floating permanent buff law 2 bans; the difference between
  a legal coefficient and an illegal modifier is **whether it sits inside a resolution
  equation or beside it**, and it is a difference a reviewer must check by reading the
  equation, not the name. D-041's shipped framing is the nearest ratified analogue — an
  accumulated stock is legal because it *"feeds behaviour continuously"* and is *"a **LEVER**
  rather than a **COEFFICIENT**"* (`docs/d041-attachment.md:64-66`), which this lane reads as
  a statement about the player's grip on it, not a licence for outcome multipliers.
  **DIRECTOR DECISION REQUIRED** (§6, Q6) — nothing in the tree rules on partial capability.

**(3) FRAGILE — YES, ships, and fragility is a property of the INPUTS, not of the capability.**
Mechanically: a capability is fragile exactly insofar as some conjunct reads a quantity a
shock can move. `food_surplus_ratio` is movable by famine, weather and labour reallocation;
`trade_volume` is movable by a closed deadband; `population` is movable by mortality. State
needed: **none new** — fragility is inherited from whatever the conjunct reads. **PROPOSED,
and this is the design consequence worth stating:** you do not design fragility into a
capability, **you choose which quantities it reads, and fragility follows.** A capability
gated only on quantities no shock can move is not robust; it is inert. CR-007 §8.4 already
names the degenerate end of this axis: *"A predicate whose conjuncts are all constant-true or
monotone-in-time is a **schedule**. It is not prohibited by B3, but it delivers no
differentiation and should be recognised as a schedule rather than mistaken for emergence."*
(`docs/adr/cr-007-b3-exemplar-reconciliation.md:237-240`, labelled *Proposed* there).

**(4) REVERSIBLE — YES, ships, and it is the DEFAULT.** §3.4 in full. Mechanically: `recede`
fires, the latch clears, the dependent mechanisms stop running. State needed: **none new**
for a latched capability; **none at all** for a pure-derived one, which reverses the turn its
input falls. **The one thing that needs writing down is irreversibility**, as an omitted
`recede` clause in data, per capability, deliberately.

**(5) INSTITUTION-DEPENDENT — NOT BUILDABLE. Blocked on a definition, not on architecture.**
Mechanically it would be a conjunct on an institution quantity, exactly like any other
conjunct — the mechanism is trivial. State needed: an institution object, a system that owns
it, and a published variable it writes. **MEASURED: none of the three exists, and the word
itself carries six meanings across three milestones** (political module M7→M8 · a need
trade-off binding M5 by name in D-035 · a settlement capability in D-038 · the
knowledge-conversion mechanism in the M5 placeholder · an argument to D-041's control
function · a predicate operand in D-018 class emergence —
`docs/capability-architecture-decision.md:230-234` and
`docs/milestone-architecture-governance.md:179-209`). Both records say the same thing about
consequence: *"This must be ruled before anyone writes an institution packet."* CR-010 was
recommended and **has never existed on any branch** (`docs/current-state.md:13`). §6, Q7.

**(6) RESOURCE-DEPENDENT — buildable, and it has the cleanest carrier in this whole design.**
Mechanically: a conjunct on an absolute quantity derived from a conserved stock — deposit
presence, units on hand, extraction throughput. **The D-035-C carrier is the good itself**
(`docs/d035-needs-aggregation.md:91` names *a good* as a carrier), and the good is already
conserved under law 1, already moved only by `Ledger`, already located. State needed: **one
published variable per resource quantity, written by the system that owns the stock.** No
new table, no new mechanism, no new coupling. **The constraint that makes this sharp, and it
is RATIFIED:** at Empire scope the conjunct may **not** read an Empire-wide resource pool,
because D-042 §4.3 rules that no such pool exists — *"**Shared ownership does NOT create a
single Empire-wide inventory.** Local stocks stay physically local; moving them between
settlements requires the appropriate transport/trade/logistics mechanism"*
(`docs/d042-…:72-75`). **So an Empire-scoped resource conjunct must be an explicit aggregate
that someone computes and publishes, with transport reachability in it, or it is a fiction.**
§4.2 records the carrier this lane owes for that aggregate.

**(7) SKILL-DEPENDENT — YES, ships, partially.** Mechanically: a conjunct on class or bucket
composition. `artisan_share` is precisely this and it already gates two recipes
(`goods.json:156`, `:175`) — **MEASURED, an economic/social quantity gating a technological
capability, in shipped data.** The carrier is **people**, moved by `Ledger.Transfer` under
law 1 (D-018 §5 mobility flows, elaborated at `docs/d021-stability-doctrine.md:39-43`).
State needed: **none new** for artisan-like skills. **For literacy, everything is missing**
— and this is a ratified dependency with no publisher: D-018 gates Intelligentsia emergence
on *"literacy share + education institutions > threshold"* (`docs/d018-classes-and-needs.md:26`,
frozen), the Prospects need on education access (`:42`), rising expectations on *"literacy,
urbanization, and media exposure"* (`:48`), mobility on education access (`:63`); D-039 A5
names *"literacy, road and signal infrastructure, institutions, general competence"* as the
inputs to command capability (`docs/d039-command-fog-and-siege.md:37-40`). **MEASURED: the
registry publishes four variables and none of them is any of these**
(`Sim.Core/State/Variables.cs:93`). §5.3.

**(8) KNOWLEDGE-DEPENDENT — blocked on substrate AND on object definition.**
Mechanically: a conjunct on a knowledge quantity, which D-042 §9.2 rules Empire-scoped
(`:165-166`). State needed: the scope substrate of §3.3 **and** a knowledge quantity **and**
a system that publishes it. **MEASURED: none exists.** And the shape of the quantity is
itself unruled — how many there are, whether science and knowledge are one stock or two,
whether domains are literal lattices or predicate sets, is recorded as unanswered in two
unratified records (G-05). D-042 settles *whether* accumulation is permitted — **YES**,
§9.4/§9.5, restated in its settled-questions table (`docs/d042-…:290-292`) — but not the
fence on it (G-03). §6, Q4.

**The summary line, MEASURED:** of the eight properties, **four ship today with no new state
at all** (local, fragile, reversible, skill-dependent-for-shipped-skills), **one needs only
published variables** (resource-dependent), **one needs a ruling on shape** (partial), and
**two are blocked on objects the repository has not defined** (institution-dependent,
knowledge-dependent). **No property on that list requires a new capability abstraction.**

### 3.7 WHAT "WIDENING THE SCOPE" MEANS CONCRETELY

`docs/capability-architecture-decision.md:52` names the unit of work — *"The anti-retrofit
device is not new work. The work is to widen its SCOPE."* — and never says what widening
consists of. **PROPOSED:** it is exactly four additive things, each separately ownable,
each independently useful, and **none of them a system**:

| # | the unit | what it adds | depends on | law 6 status |
|---|---|---|---|---|
| **W1** | **Scope substrate** — the S1 polity-keyed variable row of §3.3, plus a scope column on each registry entry, plus the resolution rule | a place for an Empire-scoped reading to live | nothing (the `PolityId` key already ships — §3.3) | no new system; a row type and a registry constant |
| **W2** | **Registry growth** — publishers for the quantities ratified mechanisms already assume: literacy, education, resource availability, infrastructure reach | conjuncts that do not exist | each variable's OWNING system, one at a time, per D-027 (*"an entry exists only once a system actually publishes it"*, `Variables.cs:3-8`) | each publisher is its own system writing its own row |
| **W3** | **The storage choice-rule written down** (§3.5), so a third consumer does not have to guess between the two shipped answers | a rule, in a document | nothing | not a code artifact |
| **W4** | **The no-owner test written down** (§2.4), so conformance with D-042 §7.3 is checkable rather than remembered | a rule, and later possibly a gate script in the `check-read-isolation.sh` style | nothing | it is the law-6 check itself |

**INFERRED: W2 is the real work and W1 is the real blocker.** W3 and W4 are documentation
and cost a paragraph each. W1 is one additive row type with a POPULATED-table test. **W2 is
open-ended and is where every downstream milestone's cost actually sits** — and it is the
part that cannot be done in advance, because a variable may only be registered by a system
that actually publishes it (D-027), and the systems that would publish literacy, education
and institutional capacity **do not exist**.

**This lane records what it did NOT design, deliberately:** it did not design a knowledge
quantity, a research allocation, a diffusion rate, an institution, a technology object, a
milestone placement or a packet. Every one of those is either owned by another lane, blocked
on a director ruling, or forbidden to this phase.

### 3.8 WHAT THIS DESIGN FORBIDS — and the one hole in the forbidding, surfaced not settled

**PROPOSED, and each is a consequence of something RATIFIED rather than a preference:**

1. **No capability id, registry or central table.** Consequence of §2.2 / D-042 §7.3.
2. **No capability grants a buff.** A satisfied predicate changes **which mechanism runs**
   or a **coefficient inside a resolution equation**, never a standing modifier attached to
   an owner. Consequence of law 2 (`CLAUDE.md:17`).
3. **No conjunct reads a date, a turn index or an era label.** Era labels are computed
   OUTPUT (`docs/civ-sim-architecture-v3-outline.md:22`, `:110`). Consequence of law 4.
4. **No ordered thresholds K1 < K2 < K3 on one accumulator.** These are tree edges with the
   edges hidden in the numbers. **PROPOSED here and PROPOSED in two prior records; RULED
   NOWHERE** — this is G-03, and this lane does not settle it (§6, Q4).
5. **No sibling call.** A domain that needs another domain's fact reads the **published
   variable**, not the other system. Consequence of law 6 (`CLAUDE.md:21`;
   `docs/d042-…:136-140`).
6. **No new serialized row merely so a capability can be observed.**
   *"Nothing else is permitted … it is a **GAP**"* (`docs/observability-architecture.md` §0).

**THE HOLE — G-04, SURFACED, NOT SETTLED.** *May a capability predicate reference another
capability's latch?*

- **What the tree says today, MEASURED:** it cannot. The grammar's operands are
  `variableName | numberLiteral` and nothing else (`Predicate.cs:18`), and latches live in
  domain-owned row types (`ClassStateRow`), not in the `Variables` table. **The ban holds by
  construction** — but **accidentally**, as a side effect of where the latch happens to be
  stored.
- **What three records propose:** *"Capability A referencing capability B's *latch* is how a
  tree grows back — that is the line to hold"* (`docs/capability-architecture-decision.md:203-206`),
  restated as *Proposed* at `docs/milestone-architecture-governance.md:319-321` and at
  `docs/adr/cr-007-b3-exemplar-reconciliation.md:233-234` (*"A predicate may reference
  published variables only. Referencing another capability's latch reconstructs tree edges.
  (Proposed.)"*).
- **What no document rules:** anything. G-04 in
  `docs/design/recovered-decisions-knowledge-tech.md` §9 records the absence.
- **The sharp form this lane can add, and it is the reason the accidental ban is not
  enough.** MEASURED: nothing stops `ClassMobilitySystem` from publishing its own latch as
  an ordinary published variable — `artisans_active` as a 0.0/1.0 `double` — on its next
  turn, using the existing registry and the existing publication path, with no grammar
  change, no schema change and no review trigger. **The back door is one variable
  registration wide, and it is open today.** Whether that is a legitimate publication (a
  system publishing a fact about itself, which is what publication IS) or the tree growing
  back through the seam is **exactly the question nobody has ruled**. §6, Q3.

### 3.9 THE D-021 OBLIGATION THIS DESIGN OWES

**RATIFIED:** *"Every positive feedback loop in the design must ship with at least one
negative feedback loop that *strengthens with amplitude*. … This rule is project-wide"*
(`docs/d021-stability-doctrine.md:8`); the valves are Exit, Voice, Endurance (`:24`).

**INFERRED (this lane):** the capability seam creates a positive loop wherever it is used —
surplus → specialists → capability → more output → more surplus. The shipped instance is
live: `food_surplus_ratio` → artisans → `artisan_share` → gated recipes (`bronze-casting`,
`toolmaking`, `goods.json:156`, `:175`) → output. **PROPOSED, stated as an obligation rather
than discharged, because this lane cannot discharge it honestly:**

- **The hysteresis band is NOT a D-021 valve.** It is a discrete brake at one threshold; it
  does not strengthen with amplitude. A capability that runs away does not run away *less*
  the further it runs. Calling the latch a valve would be exactly the smoothing D-021's rule
  exists to refuse.
- **The valve must come from the domain the capability acts in**, and it must be named by
  the packet that ships that capability, in the same milestone, per D-021. The shipped
  example is the correct shape: the artisan loop's brake is `food_surplus_ratio` itself — a
  ratio whose denominator grows with the population the loop produces, so the more
  specialists the loop makes, the harder the next one is to feed. **That brake strengthens
  with amplitude and it is physical.** MEASURED as the shape of the shipped variable
  (`Variables.cs:10-19`); **INFERRED** as satisfying D-021, which no document asserts of it.
- **Therefore: every capability proposed in any future milestone owes a named valve of that
  kind, and the seam supplies none.** This lane records the obligation and does not pretend
  the architecture discharges it.

### 3.10 OBSERVABILITY PLACEMENT

**RATIFIED, and it constrains this design more than it first appears.**
`docs/observability-architecture.md` §0 is *"THE ONE RULE — The logger observes. The
simulation calculates. The UI reads. The player issues orders. Observability must not become
a second simulation"*, and every field is exactly one of **READ · SUMMED · DIFFERENCED ·
RESIDUAL · RECOMPUTED**, with anything else a **GAP**.

**MEASURED — a correction to this lane's own brief, recorded under GOV-3 B4 (*"the tree
wins; correct it, record it, and continue"*).** The mandate text given to this lane states
the taxonomy as *"READ / RECOMPUTED / DERIVED / GAP"*. The shipped taxonomy has **five**
kinds and **DERIVED is not one of them** — the five are READ, SUMMED, DIFFERENCED, RESIDUAL,
RECOMPUTED, with GAP as the residual category for what cannot be expressed
(`docs/observability-architecture.md` §0). The substantive rule the brief invokes is
unaffected and this lane applies it as written in the tree.

**PROPOSED, applying it:** a capability answer is observable as **RECOMPUTED** — *"a call to
a PUBLIC static simulation function on stored state — never a private re-implementation"*.
A `Predicate` is already such a function: immutable, deterministic, pure tree walk over a
variable reader (`Predicate.cs:22-25`). So *"why can this settlement not cast bronze"* is
answerable by evaluating the same predicate the simulation evaluates, against the same
stored rows, and reporting which conjunct was false — **with no new serialized state and no
observer-side copy of the logic that could drift.** That is the glass-box property the
architecture is for, and it is a consequence of the grammar being closed and data-driven
rather than something that has to be built on top.

---

## §4 CARRIERS OWED

Every cross-system edge this design proposes whose D-035-C physical carrier this lane could
**not** name. The test is RATIFIED: *"Name the physical carrier — a good, a purse, a
building, a policy, a body, a season. If none exists, it is an invented modifier and is
**refused**."* (`docs/d035-needs-aggregation.md:91`), and *"a coupling that does not fit one
of them has not found an eighth path, it has failed the carrier test"* (`:96-98`).

**A carrier IS named, and is recorded here for contrast, so the owed list is not read as
covering everything:** resource-dependent capability at settlement scope (§3.6(6)) — carrier
**the good itself**, D-035-C path 1/2; skill-dependent capability for shipped skills
(§3.6(7)) — carrier **people**, moved by `Ledger.Transfer`. Those two are clean.

| # | proposed edge | why a carrier is owed | status |
|---|---|---|---|
| **4.1** | **A settlement-scope consumer reading an Empire-scoped variable** (§3.3, resolution clause 2) | "The Empire knows how to do X, therefore this settlement can do X" asserts that knowledge travels from the polity to the place. **What physically carries it?** The honest candidates are an institution, an administration, a school, a travelling craftsman — and **none of them exists on this tree** (§3.6(5)). Without one, the edge is a modifier wearing a scope key. | **OWED.** Blocks Empire-scoped capability from being conformant even after W1 ships the row. |
| **4.2** | **An Empire-scoped RESOURCE aggregate** (§3.6(6)) | D-042 §4.3 forbids a single Empire-wide inventory (`:72-75`). So "the Empire has timber" must mean "timber is reachable", and reachability's carrier is **transport** — which exists in part (paths, travel cost) but whose composition into an aggregate nobody has designed. The carrier is *nearly* nameable; the aggregate is not defined. | **OWED** (a carrier candidate exists — transport — but the quantity does not). |
| **4.3** | **Knowledge → capability** | D-042 §9.1 rules knowledge distinct from capability (`:164`) and §8.2 rules both distinct from technology (`:148-152`), but **no record names what physically carries knowledge into the ability to act.** A person who knows how? A written record? A workshop? The repository does not say. | **OWED.** This is the largest hole in this document. |
| **4.4** | **Diffusion across a contact edge** | G-09: diffusion is named in the Spine (`docs/civ-sim-architecture-v3-outline.md:84`), in D-042 §9.6 (`:172-174`) and in an audit as *"rides the existing trade network"*, but **what crosses the edge, at what rate, in which direction, and whether it is conserved, is stated nowhere.** The candidate carrier — **goods and the people who move them**, both of which exist and are conserved — is plausible and **unruled**. | **OWED.** |
| **4.5** | **Literacy → command capability** | D-039 A5 names literacy, infrastructure, institutions and competence as inputs to command capability (`docs/d039-command-fog-and-siege.md:37-40`, RATIFIED). The carrier for the first is presumably **the literate person** and for the second **the road** — but the variables do not exist (C-07) so there is nothing yet to carry. | **OWED**, though the carriers look nameable once W2 supplies publishers. |
| **4.6** | **Capability loss on institutional collapse** | §3.4 makes loss representable via `recede` on a falling input. But *"the institution that sustained this capability was destroyed"* needs the institution to be a thing that can be destroyed, and a carrier for the destruction. **Neither exists** (§3.6(5)). | **OWED**, downstream of CR-010. |

**INFERRED, and stated because the list above is otherwise easy to misread as six equal
items:** 4.1, 4.3 and 4.6 all reduce to **the same missing object** — an institution. The
carrier problem in this design is not six problems. It is largely one undefined noun,
appearing three times.

---

## §5 CONFLICTS SURFACED

Each row names both sources. **No side is taken and nothing is reconciled** — that is the
mandate's requirement and this lane's limit.

**5.1 — The observability taxonomy as given to this lane vs the shipped taxonomy.**
Source A: this lane's mandate text, *"THE OBSERVABILITY TAXONOMY (RATIFIED,
docs/observability-architecture.md §0): READ / RECOMPUTED / DERIVED / GAP"*.
Source B: `docs/observability-architecture.md` §0 on `31d8718` — **READ · SUMMED ·
DIFFERENCED · RESIDUAL · RECOMPUTED**, with GAP for what cannot be expressed; **"DERIVED" is
not a kind in that taxonomy**. Recorded under GOV-3 B4 as a correction to this lane's own
prompt, not as a repository defect. The rule invoked is unaffected. (Note separately that
"DERIVED" *is* the tree's word for `SettlementHappiness` — *"a DERIVED READING, never a
stock"*, `SettlementHappiness.cs:8` — and for `TravelCostRow` (`WorldState.cs:405-411`); the two
usages are different vocabularies that happen to share a word.)

**5.2 — `capability-architecture-decision.md`'s BLOCKING conflict #2 vs the tree.**
Source A: `docs/capability-architecture-decision.md:56-62` and `:371` — *"**A per-CIVILIZATION
capability is not representable today — this is the real blocker** … there is **no `PolityRow`
type, no `Polities` table**, and **nothing anywhere in `Sim.Core`/`Sim.Cli` constructs a
`PolityId` outside deserialization**"*, logged as BLOCKING.
Source B: MEASURED on `31d8718` — `WorldState.cs:704` (`PolityRow`), `:837`/`:980` (the
`Polities` table), `CanonicalSchema.cs:91`/`:543-547` (serialized, schema v23),
`WorldFounding.cs:282-302` (constructed at worldgen under the CR-011 ruling).
**All three clauses of Source A are false on this tree.** The document was not edited. Whether
that record's conflict #2 is now closed is **DIRECTOR DECISION REQUIRED** — this lane
measured the tree and takes no view on the record's status. *(The recovery lane records the
same row as G-102 in `docs/design/recovered-decisions-architecture-invariants.md`.)*

**5.3 — Ratified mechanisms depend on variables that do not exist.**
Source A: D-018 (frozen) gates Intelligentsia emergence on *"literacy share + education
institutions > threshold"* (`docs/d018-classes-and-needs.md:26`), Prospects on education
access (`:42`), rising expectations on literacy/urbanization/media (`:48`), mobility on
education access (`:63`); D-021 makes education *"a flow with a lag"* (`docs/d021-…:43`);
D-039 A5 names literacy and institutions as command-capability inputs (`:37-40`).
Source B: MEASURED — the registry ships four variables, `food_surplus_ratio`,
`artisan_share`, `population`, `trade_volume` (`Sim.Core/State/Variables.cs:93`), **none of
them a literacy, education, institution or knowledge quantity**. Recorded as C-07 by the
recovery lane. Both stand; neither is amended.

**5.4 — Era-gated capability in ratified material vs law 4 and D-040 B3.**
Source A: D-011 (frozen) *"(**era-gated** additions: bombard, air strike, dig-in)"*
(`docs/d011-battle-layer-addendum.md:13`) and D-009/D-010's bridges and tunnels as
*"expensive, **era-gated**, terrain-crossing edges"* (`docs/d009-d010-map-population-addendum.md:12`).
Source B: `CLAUDE.md:19` (law 4) and `docs/d040-discovery-and-control.md:59-64` (B3).
**D-040 flags the D-009/D-010 instance against itself and declines to rule** — *"**It is not
amended here** — D-040 amends nothing — but the transport packet will have to face it, and it
should not discover it late"* (`:225-227`). CR-009 was recommended and **has never existed on
any branch** (`docs/current-state.md:13`). **This binds this design directly:** any transport
or military capability built on the seam inherits an unresolved contradiction in its own
ratified source.

**5.5 — D-018's artisan trigger vs the shipped predicate.**
Source A: D-018 (frozen) lists the artisan trigger as *"craft specialization share >
threshold"* (`docs/d018-classes-and-needs.md:20`) and contains no food-surplus and no
market-extent condition.
Source B: the shipped predicate, `"emerge": "food_surplus_ratio > 1.3 && population > 520"`
(`Sim.Data/content/sim.json:169`). D-040 F2 records the disagreement and assigns no owner:
*"**D-018 and the shipped predicate disagree about what makes an artisan** — recorded here,
owner unassigned"* (`docs/d040-discovery-and-control.md:193-199`). **Material to this
document** because §3.1 and §3.6 use the shipped predicate as the worked example of what a
conformant capability looks like.

**5.6 — Whether the exemplar this design points at is a model or a counter-example.**
Source A: `docs/capability-architecture-decision.md:132-133` claims the shipped predicate
*"conjoins a structurally-falsifiable-forever term"*.
Source B: the same document's own correction notice at `:11-19` and
`docs/milestone-architecture-governance.md:230-238` — **falsified**: one conjunct is
constant-true (measured 3.5 ± 0.1 across all twelve settlements, `Variables.cs:24-32`) and
the other is universal delay by explicit tuning (`sim.json:171`: *"520 sits above the
~350-500 jittered founding sizes, so every settlement must GROW into its artisans"*).
*"The exemplar is a counter-example, not a model."* **Consequence for this document, stated
plainly:** §3 cites the shipped predicate as the exemplar of the *shape* (a conjunction over
computed published state, with hysteresis and a `recede` clause), which survives the
falsification; it does **not** cite it as an exemplar of *differentiation*, which does not.
CR-007 §8.4 supplies the tree's own word for what it is instead — **a schedule**
(`docs/adr/cr-007-…:237-240`).

**5.7 — Whether the choice rule of §3.5 has any standing.**
Source A: `docs/capability-architecture-decision.md:186-192` states the rule
(*"does flipping move a conserved stock?"*) as a recommendation.
Source B: that document's own header — *"§4's recommendation is **UNVERIFIED** and must not
be built against until it is attacked"* (`:34-37`) — and its correction notice recording the
adversarial pass returned **SURVIVES_WITH_CONDITIONS**, not clean survival (`:24-27`).
**This lane re-derived the rule from shipped evidence in §3.5 rather than inheriting it**,
and labels it **PROPOSED**. It is not ruled anywhere. §6, Q8.

**5.8 — CR-007's own status, which this document leans on twice.**
Source A: `docs/adr/cr-007-b3-exemplar-reconciliation.md:3-4` — *"RESOLVED WITHOUT A NEW
RULING"*.
Source B: `docs/current-state.md:368-370` lists CR-007 among *"Open change requests awaiting
director ruling"*. A self-declared resolution by the finding's own author is not a director
ruling. Recorded as C-01 by the recovery lane. **Material** because §3.6(3) and §5.6 cite
CR-007 §8.4's *"schedule"* framing, which carries whatever standing CR-007 has.

**5.9 — Where the shared grammar physically lives.**
MEASURED, offered as an observation rather than a defect: the shared type is
`Sim.Core.Systems.ClassMobility.Predicate`, and `ProductionSystem` reaches it by qualified
name — `ClassMobility.Predicate.Parse(src)` (`ProductionSystem.cs:105-108`),
`ClassMobility.PredicateFormatException` (`GoodsConfig.cs:245-247`). **Law 6 governs systems
referencing systems and a `Predicate` is an immutable value type, so this is conformant**
(the tree states the principle at `SettlementHappiness.cs:57-58`). But **the shared grammar
of the whole project lives inside one domain's namespace**, and as the seam widens, each new
consumer will reach across into `ClassMobility` for it. That is a latent conformance hazard
of **location**, not of mechanism. Recorded, not proposed as work — a relocation would be a
production change and this phase makes none.

---

## §6 DIRECTOR QUESTIONS

Stated as questions. No preferred answer is attached to any of them, and where this lane has
a design position it is in §3 under a PROPOSED label, not here.

**Q1 — Which scope substrate?** D-042 §8.3 requires Empire-level capability evaluation and
`VariableRow` is settlement-keyed only. Should the Empire-scoped reading live in a **second,
additive polity-keyed row type**, or should `VariableRow` itself be **generalized to carry a
scope discriminant** — which changes a serialization contract inside the M0 freeze perimeter
and therefore takes a Contradiction Report rather than a packet?

**Q2 — What does an Empire-scoped conjunct evaluate to for a settlement under no polity's
control?** False, or an error at evaluation, or is the situation to be made unreachable by
construction? Nothing in the tree rules on it, and the choice has turn-visible semantics.

**Q3 — May a system publish its own latch as an ordinary published variable?** This is G-04
in its operative form. The D-020 grammar has no latch operand, so a capability cannot read
another's latch today — but a system can publish a 0.0/1.0 variable derived from its own
latch with no grammar change, no schema change and no review trigger, and a second
capability can then read it. **Is that legitimate publication, or the technology tree
returning through the seam?** Three records propose banning latch references; none rules.

**Q4 — Is there a fence on accumulation, beyond the three guards D-042 already supplies?**
D-042 §15 rules that an accumulating knowledge quantity is permitted (**YES**, `:290-292`)
with no rigid tree, no one-at-a-time queue and no calendar unlocks. Two records additionally
propose that the accumulator must be able to **fall**, that **ordered thresholds K1 < K2 < K3
on one accumulator** are tree edges in disguise, and that a **monotone-in-time accumulator is
a schedule**. Which, if any, of those three becomes a rule — and does narrowing B3 require a
CR?

**Q5 — May a capability be declared irreversible, and by what authority?** The shipped
mechanism makes irreversibility a **data choice** — an omitted `recede` clause, one line in a
data file, needing no review. D-042 §5.4 preserves *"unrelated accumulated Empire state"*
across a government transition but rules nothing about a capability outliving its
preconditions. Is an omitted `recede` a tuning decision (LIVING, no procedure) or a design
decision that needs a ruling each time?

**Q6 — Is "partial capability" a concept this project has?** If a capability may be held in
degree rather than in extent, does degree enter as a **coefficient inside the domain's
resolution equation** (where law 2 permits coefficients), or is degree simply out of scope
and a capability strictly boolean-per-place?

**Q7 — What is an institution?** Six meanings across three milestones; `institution` appears
nowhere in any of the four code projects; CR-010 was recommended and has never existed on any
branch. **Three of the six carriers this document owes (§4.1, §4.3, §4.6) reduce to this one
undefined noun.** Who writes CR-010, and against which milestone?

**Q8 — Is the latch-vs-pure-derived choice a rule, and is "does flipping move a conserved
stock" the criterion?** The tree ships two consumers with opposite storage answers and no
document says which a third should copy.

**Q9 — Does the D-020 grammar stay closed as the seam takes on capability work?** It ships
with *"No functions, no arithmetic (v1 — queue if needed)"* (`Predicate.cs:20`). Every
quantity a capability needs must therefore be **published by the system that owns it**, which
is a real cost paid by real packets. Does that constraint hold, or does capability work
reopen D-020?

**Q10 — Who writes CR-009?** Era-gated capability sits inside frozen D-011 and ratified
D-009/D-010, in direct tension with law 4 and D-040 B3, flagged by D-040 against itself and
left unruled. Every transport and military capability built on this seam inherits it.

**Q11 — What calibration corridor gates a capability or knowledge milestone?** G-08 records
that no candidate is named anywhere in the tree, and that *"a milestone that cannot be
calibrated is a milestone that cannot pass its own exit criteria."* This is the only open
item in this cluster that can block a milestone exit rather than a packet.

---

## §7 CAVEATS ON THE EVIDENCE

1. **Secondary evidence, declared under GOV-4.** `docs/capability-architecture-decision.md`,
   `docs/milestone-architecture-governance.md`, `docs/m5-roadmap-dependency-audit.md` and
   `docs/adr/cr-007-b3-exemplar-reconciliation.md` are **prior agents' reports by one
   author**, not rulings. They are cited here as records of what was concluded, never as
   ratification, and three of the four carry correction notices falsifying parts of the
   others. Every such correction this document relies on is surfaced in §5 rather than
   applied silently. The one document whose *quotations* this lane treats as RATIFIED is
   D-042, because it is a director design ruling of the exempt class (`:3`).
2. **This document's own recommendations are UNVERIFIED.** No adversarial pass ran against
   §2.3, §3.3, §3.5 or §3.6. Under ADR-015 §6 — *"no finding is actionable before its verdict
   returns"* (`CLAUDE.md:41`) — **nothing here may be built against.** Its PROPOSED content is
   a design position awaiting attack, and the no-owner test of §2.4 has never been run against
   anything.
3. **No independent reviewer participated**, and no lens manifest was written.
4. **Line drift is recorded, not corrected.** Where a prior record cites a line that has
   moved on `31d8718` — the artisan predicate at `sim.json:165-167` → `:169-171`, the
   registry's *"three variables"* → four — both readings are shown (§5.2, §5.3, and C-08 in
   the recovery lane). No frozen or ratified document was edited to fix a citation.
5. **Branch scope.** Every measurement is against `claude/civdemo-work-b1z2y4` @ `31d8718`.
   LOCAL, REMOTE and MAIN are three different states; nothing here was checked against `main`
   or against any other branch, and `docs/current-state.md:13` records that CR-008 exists only
   on `m5-full-build` while CR-009 and CR-010 have never existed anywhere.

6. **A concurrent sibling lane committed to this branch while this document was being
   written, and the pin is stated against the tree that was measured, not the tree this
   commit sits on.** MEASURED: this document's commit has parent `dfe92d7` (*"DESIGN
   (output C): knowledge architecture"*), which is itself a child of the pinned `31d8718`.
   `dfe92d7` adds exactly one file, `docs/design/arch-C-knowledge.md`, and touches no code,
   no data file and no ratified or frozen document — so every `file:line` citation above
   holds unchanged on the commit this document ships in. Recorded rather than smoothed,
   because LOCAL, REMOTE and MAIN are three different states and a pin that quietly moved
   is a pin that proves nothing. **Also recorded: this lane and lane C shared one working
   tree.** ADR-015 §6 requires one worktree per *verifying* agent; these are design lanes
   and no verification was performed by either, but the shared tree is stated so a reader
   can price it.

---

**WHAT THIS DOCUMENT DID NOT DO.** No system, table, row, schema, predicate, variable,
knowledge quantity, institution, technology object, diffusion mechanism or packet was
designed or implemented. No conflict was reconciled. No status was changed. No open question
was closed. No ruling was made, proposed as ratified, or implied. No frozen or ratified
document was edited. No production code, schema, JSON, golden, corridor or quarantine was
touched. No milestone spec was written or amended.
