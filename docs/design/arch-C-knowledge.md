# ARCH-C — KNOWLEDGE ARCHITECTURE

**DESIGN ONLY. NOTHING IMPLEMENTED.** No production code, schema, golden, corridor,
quarantine, content file or existing document was touched or edited. No table is designed
here. No merge. No M5 code. Created under the Director's 2026-09-19 mandate, PART 5
(design lane C). **The DIRECTOR IS ChatGPT; this lane rules nothing.** Where a design
choice touches something RATIFIED, the conflict is surfaced with both sources and left
unresolved — that is a required output of this lane, not a failure.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `31d8718`,
working tree clean. Every `file:line` cited as RATIFIED or MEASURED below was re-read on
that tree by this lane.

**Label key** (mandate labelling rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this
phase's design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.
Most of what follows is PROPOSED. That is expected and is stated honestly.

**Read first, and built on rather than re-decided:**
`docs/design/recovered-decisions-knowledge-tech.md` (K-01…K-63, C-01…C-11, G-01…G-14),
`docs/design/recovered-decisions-architecture-invariants.md` (A-43…A-58, C-06, C-12,
G-05), `docs/design/recovered-decisions-civics-institutions.md` (C-02, Q-01),
`docs/design/m4-closure-audit.md`.

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This architecture exists to make one distinction structural rather than remembered: the
difference between a civilization **KNOWING** that a thing can be done and that
civilization **BEING ABLE** to do it here, now, with these people and these materials. It
is built so that a civilization can discover, know, fail to reproduce, reproduce locally,
institutionalize, diffuse, lose practical access while retaining the proposition, and
independently rediscover — and so that progress is nowhere assumed monotonic. It
deliberately does **not** introduce a technology tree, a research queue, a completion
event that grants a capability, a global "someone has discovered X" flag, a permanent
civilization bonus, or a single scalar that accumulates and unlocks things. It also
deliberately does **not** design any table, row, field or schema: the one place where the
architecture requires storage that does not exist is stated as a blocking structural fact
(§6) and handed to the Director, because a substrate for Empire-scoped published state is
a schema change and an M5+ implementation decision, not this lane's to make. And it does
not build anything new where the repository already ships the mechanism: the capability
seam is the **D-020 predicate DSL over published variables**, it has two live consumers,
and the work on it is **scope widening**, not construction.

---

## §1 GROUND — THE FACTS THIS DESIGN STANDS ON, VERIFIED THIS PASS

Each verified by this lane against its source at `31d8718`.

| # | Fact | Label | Source |
|---|---|---|---|
| F1 | **The capability seam ships.** It is the D-020 predicate DSL over published variables: a CLOSED grammar — comparisons `> < >= <= ==` and `&& \|\| !` over registered variable names and number literals, *"No functions, no arithmetic (v1)"*, parsed once at config load, pure tree walk, ordinal strings, invariant parsing. | RATIFIED (shipped contract) | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| F2 | **Consumer one — class emergence,** with a serialized hysteresis latch: *"Inactive + emerge true → Active = 1; active + recede true → Active = 0 … Recede absent = never recedes."* The latch records **current satisfaction under hysteresis, not history.** | RATIFIED (shipped) | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; row `Sim.Core/State/WorldState.cs:403` |
| F3 | **Consumer two — recipe availability,** and it is a **knowledge gate by name**: *"an optional D-020 availability predicate ('requires') - a knowledge gate over published variables, never a calendar date (law 4)."* Implemented pure-derived, no stored state: `available[r] = _requires[r] is not { } predicate \|\| predicate.Evaluate(varId => ReadVariable(prev, settlement, varId));` | RATIFIED (shipped data + code) | `Sim.Data/content/goods.json:3`; `Sim.Core/Systems/Production/ProductionSystem.cs:388-396`; live gates `goods.json:156`, `:175` (`"requires": "artisan_share > 0.05"`) |
| F4 | **The registry publishes exactly four variables** — `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. **None is a literacy, education, institution or knowledge variable.** Names are code-side; names never live in sim rows; an entry exists only once a system actually publishes it. | MEASURED (this pass, `31d8718`) | `Sim.Core/State/Variables.cs:3-8`, `:36`, `:37`, `:60`, `:90`, `:93` |
| F5 | **REGISTRY LAW — scale invariance.** *"ANY emergence predicate that needs scale sensitivity MUST publish an absolute quantity, not another ratio."* | RATIFIED (shipped law, stated in code) | `Sim.Core/State/Variables.cs:24-32` |
| F6 | **Predicates read the PREVIOUS turn's rows — a one-turn lag, by design.** | RATIFIED (shipped) | `Sim.Core/Systems/ClassMobility/Predicate.cs:22-25`; `Sim.Core/State/WorldState.cs:388-394` |
| F7 | **`VariableRow` is `(SettlementId, VarId, Value)` — settlement-keyed, and documented as "Observational state, not a stock."** | MEASURED (this pass) | `Sim.Core/State/WorldState.cs:388-394` |
| F8 | **A universal `CapabilitySystem` is REJECTED.** *"Do not create a universal God system such as a `CapabilitySystem` that owns every capability or coordinates every domain."* The section is headed *"STATE-MEDIATED DEPENDENCY (reaffirms Law 6)"*, and the reason is given in the two clauses above it: *"Gameplay interdependence is allowed; direct code coupling is not"* and *"Economy may depend conceptually on knowledge … that never justifies a sibling call."* | RATIFIED (rejection) | `docs/d042-empire-and-player-control-addendum.md:134-142` (§7.1-7.3); anti-pattern list `:196-203` |
| F9 | **D-020 is the capability FOUNDATION; capability is distinct from seven other things; the chain is `State → Published Variables → Predicates → Capabilities → Available Actions`; capability evaluation must distinguish Empire and Settlement SCOPE; capability consumes computed state, never calendar unlocks; and the knowledge/technology/capability system is NOT implemented by D-042.** | RATIFIED | `docs/d042-…:146-160` (§8.1-8.5) |
| F10 | **Knowledge is distinct from technology and capability; knowledge is ultimately Empire-scoped while produced/applied through settlements, institutions, people and research activities; research proceeds in PARALLEL; knowledge generation is a resource/flow the player ALLOCATES; unallocated knowledge ACCUMULATES AS A RESERVE; knowledge may arise through deliberate research, practical discovery, diffusion, external exchange, espionage; do not collapse these into a single rigid technology tree.** | RATIFIED | `docs/d042-…:164-175` (§9.1-9.7); settled-list `:290-296` (§15) |
| F11 | **D-040 B3 — NO TECHNOLOGY UNLOCK.** *"a tech-tree node opening sea travel is a calendar gate wearing a tree."* The sanctioned positive form: *"sea travel becomes possible when the conditions for boats exist — a coastal settlement, timber, and craft capacity — in the same shape as class emergence … A landlocked polity never develops it; a coastal one does; nobody schedules either."* Prerequisites are **conjuncts**, not edges. | RATIFIED | `docs/d040-discovery-and-control.md:59-70` |
| F12 | **D-040 B1/B2 — the map is DISCOVERED, not revealed:** a polity's knowledge of the world is a *"computed extent"*, not a visibility flag; unreached land is *"absent from what the polity can act on"*. Discovery reuses D-039 reconnaissance — *"Do not invent a second exploration system."* And the one difference: *"discovery reports a permanent fact about the land. Once known, land stays known."* | RATIFIED | `docs/d040-…:35-54` |
| F13 | **D-039 B1 — information is bought with whatever is scarce in that era, never with money; the ancient cost is FOOD AND PEOPLE.** D-039 A2 models information as **three quantities with different dynamics, not one information score**. D-039 A5: *"Era is a consequence, never an input."* | RATIFIED | `docs/d039-command-fog-and-siege.md:13-20`, `:37-40`, `:44-49` |
| F14 | **Law 4 — no calendar gates:** *"Capability derives from computed state; era labels are descriptive output only."* **Law 2 — mechanisms over modifiers:** *"Free-floating permanent auras ('+10% happiness') are illegal"*, coefficients inside a resolution equation are legal. | RATIFIED (frozen Spine S2) | `docs/civ-sim-architecture-v3-outline.md:20`, `:22`; short form `CLAUDE.md:17`, `:19` |
| F15 | **D-021 paired-feedback rule:** *"Every positive feedback loop in the design must ship with at least one negative feedback loop that strengthens with amplitude."* The organizing frame is **Exit, Voice, or Endurance**, *"and the sim always keeps at least one channel open."* | RATIFIED | `docs/d021-stability-doctrine.md` Part 1, Part 2 |
| F16 | **D-021 already rules the knowledge loop's own valve:** *"Education is a flow with a lag: literacy investments change bucket composition over sim-decades and — via rising expectations (frozen) — raise liberty/prospects salience. Educating your population remains a deliberate gamble; that tension is kept, not patched."* | RATIFIED | `docs/d021-stability-doctrine.md` Part 3, last bullet |
| F17 | **D-035-C carrier test:** *"Name the physical carrier — a good, a purse, a building, a policy, a body, a season. If none exists, it is an invented modifier and is refused."* Exactly one thing is forbidden: `satisfaction_A` feeding `satisfaction_B` with an invented weight. Seven legal paths. *"a coupling that does not fit one of them has not found an eighth path, it has failed the carrier test."* | RATIFIED | `docs/d035-needs-aggregation.md:72-96` |
| F18 | **The observability taxonomy — READ / SUMMED / DIFFERENCED / RESIDUAL / RECOMPUTED, with GAP for the rest.** *"The logger observes. The simulation calculates. The UI reads."* *"If a quantity would need a formula the simulation does not expose, it is a GAP … rather than an observer-side copy of the formula that will drift."* | RATIFIED | `docs/observability-architecture.md:18-36` (§0) |
| F19 | **SettlementHappiness is a DERIVED READING, never a stock:** *"It does not accumulate, it does not decay, nothing integrates it, and it is NOT SERIALIZED … A stock can be granted: something hands you +5 and the population is happier with no change in what it eats or where it sleeps. A derived reading cannot — the only way to move it is to move a condition."* And the enforced gate: it *"DELIBERATELY DOES NOT READ"* the needs/grievance rows, because `scripts/check-read-isolation.sh` enforces D-021's deferral on those tables. | RATIFIED (director ruling, shipped) | `Sim.Core/State/SettlementHappiness.cs:6-35` |
| F20 | **A shipped precedent for non-conserved, serialized, `double`-valued ACCUMULATED state:** `SmoothedAttractivenessRow(SettlementId Settlement, double Value)` — *"Persistent sim state (a filter carries memory); rows appear lazily the first turn a settlement is seen."* | MEASURED (this pass) | `Sim.Core/State/WorldState.cs:423-432` |
| F21 | **D-041 — the ruled precedent that a time-integrated stock is legal when it is a lever:** *"Control is COMPUTED each turn … Attachment is an ACCUMULATED STOCK held by a population, slow to build and slow to lose."* | RATIFIED | `docs/d041-attachment.md:34-36` (as recovered at K-13 / G-046) |
| F22 | **The Empire identity already exists and is deliberately lightweight.** `PolityRow(PolityId Id, CommandSource Source)`, with the `_doc`: *"Government, economy, knowledge, military and institution state are SEPARATE domains that key themselves to this id — the Empire must not become a God object holding every domain's state."* Settlement membership is DERIVED from `ControlRow`. | MEASURED (this pass) | `Sim.Core/State/WorldState.cs:690-704` |
| F23 | **Serialization discipline:** all canonical serialization is one chokepoint; *"Adding state = three edits in one file (Write, Read, ExpectedLength); the anti-padding test and the pinned golden hash break loudly until all three agree."* No reflection, no raw struct memory, doubles as raw IEEE-754 bits. | RATIFIED | `docs/adr/adr-005-canonical-schema.md` |
| F24 | **M4 deferred the entire cluster BY RULING:** *"Knowledge/technology (no tech tree, no diffusion, no era system), money and finance, full armies and the AutoResolver, M5 politics. The 10-year atomic turn, the 0.5-year demographic microsteps and the deterministic fixed pipeline are unchanged."* | RATIFIED | `docs/m4-exit-inventory.md:226-229` |
| F25 | **The Spine's own vocabulary for this milestone:** *"Knowledge & diffusion \| M6 \| T2 \| no tree; domain lattice lite"* and *"M6 — Knowledge & divergence. Domain lattice lite, diffusion, computed era labels; map shows civilizations pulling apart in time."* (D-011 §6 resequences it to M7 — see CONFLICT X-07.) | RATIFIED (frozen Spine) | `docs/civ-sim-architecture-v3-outline.md:84`, `:110` |

**The status of `docs/capability-architecture-decision.md`, stated because this lane leans
on its *quotations* and not on its recommendations.** MEASURED (this pass): that document
carries a CORRECTION NOTICE falsifying two of its own claims — its §4.1 appeal to the
shipped exemplar, and its description of the latch — and records that its adversarial pass
returned **SURVIVES_WITH_CONDITIONS, not clean survival**, so *"§4's recommendation remains
UNVERIFIED"* (`docs/capability-architecture-decision.md:7-37`). Nothing in §4 of that
document (the five-model comparison, the "Recommended: E floored by D") is treated here as
decided. What is used from it is only what it **quotes** from ruled sources, plus its
correctly-measured statement that the seam ships and the work is scope widening
(`:52`, `:42-52`).

---

## §2 THE DESIGN — THREE REGISTERS, DELIBERATELY NOT UNIFIED

**PROPOSED.** Knowledge in this project is not one object and must not be given one
storage answer. It is three registers with three different kinds of state, and the
architecture's whole value is that they are *kept apart*. This is not an invention of
convenience: D-042 §8.2 already rules capability *"distinct from raw state, knowledge,
research activity, technology, policy, institution and action"* (F9), and §9.1 rules
knowledge distinct from technology and capability (F10). A single knowledge stock would
collapse concepts the Director has already ruled non-interchangeable.

### 2.1 The three registers, defined once

| register | PROPOSED name | what it is | kind of state | who owns it |
|---|---|---|---|---|
| content | **CORPUS** | the catalogue of what there is to know — named bodies of practice-relevant understanding (`bronze-alloying`, `crop-rotation`, `keel-construction`). No owner, no value, no edges. | **content data**, in `Sim.Data`, exactly as goods and recipes are data (F3) | nobody; it is a roster |
| residence | **HOLDING** | a relation: *this holder holds this corpus entry, this firmly*. Sparse — a row exists only where something is held. | **serialized, non-conserved, `double`-valued accumulated sim state**, the `SmoothedAttractivenessRow` shape (F20) | exactly one system |
| capability | **PRACTICE** | *this thing can actually be done in this settlement this turn* — a D-020 predicate over published variables, conjoining a holding term with material and labour terms | **DERIVED READING — not stored, not serialized**, the `SettlementHappiness` shape (F19) and the shipped `requires` shape (F3) | each consuming domain, independently |

Two flows move between them:

- **INQUIRY** — PROPOSED: the deliberate allocation of scarce real inputs (people and
  food — F13's ratified costing precedent) to a named line of work, raising the rate at
  which firmness accrues in a region of the corpus. It is a **persistent directive**, the
  repository's own term and shape: *"research allocation … state describing a desired
  ongoing configuration — it is NOT re-issued every turn to keep working"*
  (`docs/d042-…:99-106`, RATIFIED). Lines of inquiry run in PARALLEL (F10 §9.3).
- **TRANSMISSION** — PROPOSED: firmness moving from one holder to another, and *only*
  along a named physical carrier (F17). No carrier, no transmission. §5 lists the carriers
  this lane could name and the ones it could not.

### 2.2 The load-bearing sentence

**PROPOSED.** *HOLDING is a proposition held by a named holder. PRACTICE is a predicate
that is true here, now. They are different objects with different storage, and neither can
be derived from the other.* Every one of the mandate's items 7, 8, 9 and 13 is a
consequence of that separation; §4 works them through.

### 2.3 Why there is no "research points" scalar, stated against the explicit ban

**PROPOSED, and answered head-on.** The mandate bans inventing a single scalar that
accumulates and unlocks things. This design contains no such scalar, for four structural
reasons:

1. **Firmness is per (holder, corpus entry), never global.** There is no civilization
   "science" number. A civilization that is superb at metallurgy and ignorant of sailing
   is the normal case, not a special one, and the state says so directly.
2. **Nothing "unlocks".** A holding never grants anything. It is published as a variable;
   a PRACTICE predicate may *conjoin* that variable with material and labour terms. The
   grant shape — predicate true → permanent capability owned forever — is exactly what
   D-040 B3 rejects (F11) and what the shipped latch does **not** provide, because the
   latch *"records CURRENT satisfaction under hysteresis, not history"* (F2).
3. **The dominant source of knowledge in this design is not allocated at all.** It is
   PRACTICAL DISCOVERY (D-042 §9.6's own term, F10): a settlement that actually casts
   bronze, turn after turn, accrues firmness in `bronze-alloying` because people are doing
   it. No budget is spent; the carrier is the workshop and the practitioners in it. A
   civilization with no research institutions whatsoever still learns, slowly, by working.
4. **The one accumulating quantity in the design is RATIFIED, not invented, and is fenced.**
   D-042 §9.4/§9.5 (F10) rule that knowledge generation is an allocatable flow and that
   *"unallocated knowledge ACCUMULATES AS A RESERVE."* This lane does not get to delete
   that. **PROPOSED fence:** the reserve is *unallocated inquiry capacity* — person-years
   and food set aside for work not yet directed — and it **may appear only as a term inside
   a rate equation, never as the left operand of a D-020 comparison that gates anything.**
   Under that fence the reserve is a law-2-legal coefficient inside a resolution equation
   (F14), and it is structurally incapable of being research points, because the grammar
   never sees it. **The fence itself is NOT RULED** — it is recovery-lane gap G-03
   (`docs/design/recovered-decisions-knowledge-tech.md:274-279`) — and is put to the
   Director in §8. See also CONFLICT X-01.

---

## §3 THE THIRTEEN QUESTIONS

### (1) What exactly IS knowledge?

**PROPOSED.** Knowledge is a **relation between a holder and a corpus entry, carrying a
firmness** — not a quantity, not a score, not a flag, not an event. "Firmness" is PROPOSED
and is defined once: *how securely this holder can reproduce this body of understanding
without external help.* It is a `double` on [0, 1] (law 7: rates, prices and ratios are
`double`; `CLAUDE.md:22`).

What it is **not**, each with a reason that is a law or a ruling rather than a preference:

- **Not a conserved stock.** Teaching does not deplete the teacher. Law 1 governs *"people/money/goods"*
  and conserved stocks are `long` (`CLAUDE.md:16`, `:22`); knowledge has no conservation
  identity to enforce, so routing it through `Ledger.Transfer`/`Ledger.Flow` would assert a
  physics that is false. **INFERRED** — no document states that knowledge is unconserved;
  the non-depletion property is a physical fact about knowledge, and the typing consequence
  follows from law 7. The tree asks the question and has never answered it (G-07;
  `docs/m5-research-technology-institutions-placeholder.md:127-129` — *"is decay a Ledger
  sink with its own reason, as spoilage and granary overflow are for grain?"*). **Put to the
  Director in §8, Q-C6.**
- **Not a capability.** F9 rules them distinct. §3(13) is the whole answer.
- **Not a technology.** F10 §9.1 rules them distinct — and **nothing in the tree says what a
  technology IS** (recovery-lane G-14). This design therefore names no technology object at
  all, and §8 Q-C10 asks for one.
- **Not a global fact.** §3(10).

**Where it sits in the ratified chain.** In `State → Published Variables → Predicates →
Capabilities → Available Actions` (F9), HOLDING is **State**, its firmness is published as
a **Published Variable**, PRACTICE is the **Predicate**/**Capability** junction, and the
verbs a domain then permits are **Available Actions**. The design adds no new link to that
chain; it says which register occupies which link.

### (2) Where does it reside?

**PROPOSED.** In **holders**, and nowhere else. There is no world-level knowledge store,
no "discovered technologies" list, no era register that anything reads.

PROPOSED holder kinds, chosen so that each is something that already exists in the sim and
can already **die**:

| holder kind | why it is a holder | status of the substrate |
|---|---|---|
| **settlement** | where the workshop, the field and the shipyard physically are | **exists** — `SettlementId` keys the shipped `VariableRow` (F7) |
| **class bucket within a settlement** (the practitioners) | tacit craft understanding lives in the people who do the work, and those people are already counted, already age, already migrate and already die | **exists as people** (`BucketRow`, class/cohort buckets, `docs/observability-architecture.md:58-61`); **no per-bucket published-variable substrate** |
| **Empire** | D-042 §9.2 rules knowledge *"ultimately Empire-scoped"* (F10) | **identity exists** (`PolityRow`, F22); **published-variable substrate does NOT exist** — §6, the blocker |
| **institution** | D-042 §9.2 names institutions among the places knowledge is produced and applied (F10) | **NOT USABLE** — *"institution"* has six recorded meanings, zero code, and CR-010 was recommended and never written (recovery lane C-02/Q-01; `docs/design/recovered-decisions-civics-institutions.md:402`, `:441`). This lane cannot make institutions holders without picking one of six meanings, which is a Director ruling. **§8 Q-C12.** |

**INFERRED, and load-bearing:** making the holder set out of things that can die is what
makes loss (§3(8)) and survival-of-collapse (§3(9)) free rather than bolted on. A row keyed
to a holder that no longer exists is simply a row that no longer exists.

### (3) Who can possess it?

**PROPOSED.** Exactly the holders in §3(2), and them only. Individual people are not
holders — the project has no individuals and D-042 §4.4 REJECTS individual citizen wallets
as a shape (`docs/d042-…:75-80`, recovered at A-53), so a per-person knowledge model would
be the same rejected shape in a different domain. The practitioner **bucket** is the finest
grain, which is the same grain the needs, mobility and grievance machinery already uses
(`NeedSatisfactionRow(Settlement, Class, NeedId, Value)`, `GrievanceRow(Settlement, Class,
Value)` — `docs/observability-architecture.md:58-61`).

**Not a possessor: the world.** There is no omniscient register. This is the precondition
for §3(10).

### (4) How is it generated?

**PROPOSED.** Three generative routes, all of which are D-042 §9.6-named (F10), plus the
D-040 geographic case which is already ruled:

1. **PRACTICAL DISCOVERY — the default, and the largest source.** Firmness accrues in a
   corpus entry as a function of the volume of the corresponding activity actually
   performed in that settlement, integrated with `dtYears` (law 3, `CLAUDE.md:18`). A
   settlement that executes the `bronze-casting` recipe every turn gets better at bronze.
   **Carrier: the practitioners and the workshop — a body and a building** (F17). This is
   the mechanism that makes the design not need research points: a civilization with no
   deliberate inquiry still learns from what it does.
2. **INQUIRY — deliberate.** Person-years and food allocated away from other uses into a
   named line, raising the firmness rate in a corpus region, with diminishing returns
   (D-039 B2's ratified diminishing-returns shape, F13, imported rather than reinvented).
   The allocation is a persistent directive (§2.1). Lines run in parallel (F10 §9.3). **The
   cost carrier is named — food and person-years; the VENUE is not** (§5, OWED-3).
3. **ACQUISITION FROM ANOTHER HOLDER** — §3(5) and §3(6).

And the one case the tree has already ruled: **geographic knowledge is generated by going
there.** D-040 B1/B2 (F12) make a polity's knowledge of the world a *computed extent*
produced by reconnaissance walked further, and explicitly forbid a second exploration
system. **PROPOSED:** geographic extent is *not* a corpus entry and does not enter the
HOLDING register — it is its own ruled quantity with its own ruled dynamics, and this
design takes care not to swallow it. **INFERRED:** it is also the one knowledge quantity in
the project that is **monotone by ruling** — *"Once known, land stays known"* (F12) — which
is precisely why it must be kept out of a register whose defining property is that it can
decay (§3(7)).

**Not a generative route: time.** Nothing accrues because a turn passed. Law 4 (F14).

### (5) How is it acquired?

**PROPOSED.** Acquisition is distinct from generation — the mandate's Part 4 insists on it
and D-042 §9.6 lists the routes (F10). A holder acquires firmness it did not generate in
exactly three ways, each of which must name a carrier or be refused:

| route | what physically crosses | carrier status |
|---|---|---|
| **a practitioner arrives** | a person who already holds it, moving through the shipped migration / class-mobility `Ledger.Transfer` machinery | **NAMED — a body.** The mover already exists and already conserves. |
| **a practitioner is taught** | contact between a bucket that holds and a bucket that does not, inside one settlement | **NAMED — bodies in one place.** |
| **an articulated record is read** | a written record, and a holder able to read it | **OWED** — neither a record artefact nor a literacy variable exists (F4, and conflict X-05) |

**Espionage** is named by D-042 §9.6 as a permitted source (F10). This lane can name no
carrier for it and no mechanism exists: **OWED-5, §5.**

### (6) How is it transmitted?

**PROPOSED, and this is the design's sharpest commitment: KNOWLEDGE ONLY MOVES WHERE
SOMETHING PHYSICALLY MOVES.** There is no ambient diffusion by proximity, no
"neighbouring civilizations gradually learn", no distance-decayed osmosis. D-035-C is
explicit that a coupling without a carrier *"has not found an eighth path, it has failed
the carrier test"* (F17), and a proximity term is exactly the invented modifier it refuses.

PROPOSED transmission channels, each with its carrier:

1. **Migration between settlements** — carrier: the migrant. The shipped migration system
   already moves conserved people along the real network.
2. **Class mobility inside a settlement** — carrier: the person changing trade.
3. **Colonization** — carrier: the colonizing party. (MEASURED: colonization parties move
   people via `Ledger.Transfer` and are *"recorded nowhere per settlement"* —
   `docs/observability-architecture.md:63-67` — which is an observability gap this design
   would inherit.)
4. **Travelling people who accompany goods** — carrier: **OWED-1.** The Spine names
   *diffusion* (F25) and an audit says it *"rides the existing trade network"*
   (`docs/m5-roadmap-dependency-audit.md:137`, secondary evidence), but **MEASURED** on
   this tree the shipped trade artefact is `TradeFlowRow(From, To, Good, Quantity)`
   (`docs/observability-architecture.md:58-61`) — it moves **goods, not people**. A bronze
   ingot does not teach anyone to cast bronze. **This lane cannot name the carrier for
   trade-borne diffusion.** See CONFLICT X-04.
5. **Reconnaissance and discovery parties** — carrier: the scouts (F12). Geographic only.

**PROPOSED consequence, stated because it is the interesting one:** a civilization whose
borders are closed and whose people do not move does not receive knowledge, however close
its neighbours are and however much they know. Isolation is a *structural* state in this
design, not a coefficient.

### (7) Can it decay?

**PROPOSED: yes — but never on a timer.** Firmness decays at a rate determined by the
*absence of the thing that sustains it*, integrated with `dtYears` (law 3). Two sustainers,
two decay behaviours — which is §3(13)'s two channels:

- **TACIT firmness** is sustained by *doing*. Its decay rate rises as practice volume falls
  toward zero. Carrier of the decay: **cohort replacement** — the practitioners who knew it
  age out and the cohorts behind them never did the work. This rides machinery that ships
  (0.5-year demographic micro-steps, F24) and reuses D-021's ratified generational-decay
  idea — *"children inherit a fraction of their parents' grudges"* (`docs/d021-stability-doctrine.md`
  Part 2 valve 8) — applied to skill instead of grievance.
- **ARTICULATED firmness** is sustained by *record and readership*. Its decay rate rises as
  the holders able to read the record disappear. Carrier: **OWED-2** (no record artefact,
  no literacy variable — F4, X-05).

**Not decay:** a per-turn subtraction. **Not decay:** a "knowledge half-life" constant that
runs regardless of state. Both would be schedules wearing a rate, and the repository has a
name for that failure — *"a schedule rather than … emergence"*
(`docs/adr/cr-007-b3-exemplar-reconciliation.md:238-240`, secondary evidence, the term
recorded at recovery lane §8).

**DIRECTOR DECISION REQUIRED:** whether decay is a named `Ledger` sink with its own reason,
as spoilage and granary overflow are for grain. The tree asks this twice and answers it
never (G-07). This lane's PROPOSED answer is no — because knowledge is not conserved, a
`Ledger` sink would assert a conservation identity that does not exist — but the question
is the Director's. §8 Q-C6.

### (8) Can it be lost?

**PROPOSED: yes, by two structurally different routes, and the difference matters.**

1. **Forgetting** — firmness decays to zero while the holder still exists (§3(7)). Gradual,
   visible in the published variable, reversible by re-learning.
2. **Holder death** — the settlement is destroyed, or the practitioner bucket empties. The
   holding rows keyed to that holder cease to exist. Abrupt, total, and requires **no new
   mechanism at all**: this is the design's chief reason for choosing holders that can die
   (§3(2)).

**PROPOSED, and it is the honest consequence:** because PRACTICE is a derived reading with
no memory (F19's shape), loss of practical access is *immediate* the turn its preconditions
fail, while loss of the underlying holding is *slow*. That asymmetry is the whole of item 7
vs item 8, and it falls out of the storage choice rather than being modelled separately.

### (9) Can it survive institutional collapse?

**PROPOSED: yes, iff a surviving holder holds it — and that is the only mechanism.** There
is no "knowledge is preserved because it is important" rule and no archive that survives by
fiat.

Three cases, all representable without new machinery:

- Held in one place; that place falls → **lost** (rediscoverable, §3(10)).
- Held in five settlements; the Empire dissolves → **survives**, distributed, with no
  Empire to coordinate it. The settlements go on casting bronze. This is the case the
  design exists to make expressible.
- Held only as an Empire-level articulated record with no settlement-level tacit holding →
  **survives as text and dies as practice**: articulated firmness persists, tacit firmness
  is zero, and PRACTICE is false everywhere until someone re-learns by doing.

**RATIFIED constraint this must not be read against:** D-042 §5.4 rules that *"a government
transition preserves unrelated accumulated Empire state unless a specific mechanic
explicitly changes it"* (`docs/d042-…:92-93`). **INFERRED:** that rules for *state*; it does
not rule that a *capability* outlives its preconditions, and the shipped latch provides no
such memory (F2). The recovery lanes record the join as unruled (G-06; architecture lane
C-12). This design's answer is that the join is dissolved rather than resolved: HOLDING is
state and is preserved; PRACTICE is a reading and is not preserved by anything, because
there is nothing there to preserve. **§8 Q-C5** asks whether the Director accepts that
reading of §5.4.

**Also RATIFIED and relevant:** a collapse must resolve into *"functioning successor
configurations"* and *"'Beyond repair' is definitionally impossible: repair may take a
century; an unplayable black hole may never exist"* (`docs/d021-stability-doctrine.md`
Part 1). **INFERRED:** a knowledge architecture in which total loss is possible must
therefore also make **rediscovery** possible from any state — which §3(10) provides, and
which is why no loss in this design is ever marked as permanent.

### (10) Can multiple civilizations independently discover the same thing?

**PROPOSED: yes, necessarily, and it requires no mechanism — it is a consequence of the
storage shape.** Because a holding is keyed by holder and a corpus entry is *content* with
no state of its own, there is nowhere for a global "X has been discovered" flag to live.
Two Empires each accrue their own firmness in `keel-construction`, at their own rates, from
their own practice, and neither is aware of the other unless a carrier connects them
(§3(6)).

**PROPOSED bans that follow, stated as design commitments so they are checkable:**

- **No first-discoverer event object.** An event that fires once, globally, for the first
  holder to reach a threshold would reintroduce the global flag through the back door.
- **No shared corpus state.** The corpus roster is immutable content; nothing writes to it.
- **No "this is already known" suppression** of another civilization's inquiry.

**RATIFIED goal this serves:** the Spine's statement of what the milestone is *for* —
*"map shows civilizations pulling apart in time"* (F25). Divergence is only achievable if
the same corpus entry can stand at five different firmness values in five different places.

### (11) Can domains combine?

**PROPOSED: yes — as CONJUNCTION in a predicate, never as an edge in a graph.** This is
not a new idea; it is D-040 B3's ruled positive form applied to knowledge: *"sea travel
becomes possible when the conditions for boats exist — a coastal settlement, timber, and
craft capacity"* (F11). Prerequisites are conjuncts.

So a practice that requires understanding drawn from several domains is a predicate that
conjoins several published holding variables, together with its material and labour terms.
`bronze-alloying` firmness AND `furnace-building` firmness AND ore on hand AND artisans
present — one predicate, four conjuncts, no edges anywhere. Domains stay parallel and
independent, which is what the Spine's ratified phrase **"domain lattice lite — no tree"**
(F25) asks for: parallel independent lattices, not one graph.

**PROPOSED fence, and it is the line that keeps the tree from growing back:** a PRACTICE
predicate reads **HOLDING variables and material/labour variables**, and never another
practice's stored latch or another capability's result. **This is not ruled.** Two prior
records propose exactly this ban — *"Capability A referencing capability B's latch is how a
tree grows back — that is the line to hold"* (`docs/capability-architecture-decision.md:203-206`,
secondary evidence) — and the recovery lane records that **no document rules on it** (G-04).
**§8 Q-C4.** MEASURED, and relevant to the decision: the shipped D-020 grammar has no latch
operand and no functions at all (F1), so the ban is currently enforced by the grammar's
closure rather than by a rule — and would need ratifying *before* the grammar is widened,
not after.

### (12) How does experimentation work, conceptually?

**PROPOSED.** A line of inquiry is **a standing allocation, not a project with a
completion bar.** Conceptually:

- The player (or an AI Empire — *one pathway, no separate physics*, `docs/d042-…:130-132`,
  RATIFIED) directs some of the Empire's scarce person-years and food at a named region of
  the corpus. That direction is a **persistent directive** (§2.1) and is carried across
  turns without re-issue.
- While it stands, it raises the *rate* at which firmness accrues in that region for the
  holders doing the work. It does not fill a bar.
- **Outcome is uncertain and is the only place randomness enters.** Determinism is
  preserved exactly as the project requires: any draw comes from an `RngRegistry` stream
  whose state lives in `WorldState` and which is keyed by (SystemId × holder id), never
  `System.Random` (law 5, `CLAUDE.md:20`; the stream mechanism ships —
  `Sim.Core/Kernel/RngRegistry.cs`, PCG32 with state in a `WorldState` table row,
  MEASURED this pass).
- **There is no completion event, and nothing is "finished".** What changes is that
  firmness rises until PRACTICE predicates that conjoin it start evaluating true where the
  material conditions also hold — in some settlements and not others, which is the point.
- **Failure is the normal case and costs real inputs.** Person-years spent on an inquiry
  that raises little firmness are person-years not spent producing. This is what makes
  inquiry a genuine decision rather than a tax.
- **A "breakthrough"**, if the concept survives at all, is PROPOSED as *an observation*:
  the turn in which a PRACTICE predicate first evaluates true somewhere — a chronicle
  entry, computed, never a stored event that grants anything. **The tree defines
  breakthrough nowhere** (G-12) and the only recorded constraint is that *"'Rapid' is
  meaningful in turns, not sim-years"* — with the one-turn predicate lag (F6) and the
  10-year atomic turn (F24) both counting against it. **§8 Q-C9.**

**DIRECTOR DECISION REQUIRED — and this lane must not answer it.** Whether a research
activity *completes at all* is recovery-lane gap G-02, and it is contested: the M5
placeholder's scope items 7–11 and the temporal placeholder's §7 both assume completion
(*"When research crosses completion: the technology becomes available at that point in
simulation time"*), while B3 and CR-007 record that *"a completing project that grants a
capability remains prohibited"* (recovery lane C-10). Both placeholders declare themselves
unratified. This design assumes no completion; that assumption is PROPOSED and is put to
the Director as **§8 Q-C3**.

### (13) What distinguishes THEORETICAL KNOWLEDGE from PRACTICAL CAPABILITY?

**PROPOSED — the answer the whole architecture is built around.**

> **KNOWING is a HOLDING: a proposition held by a named holder, stored, slow, and
> transmissible.
> BEING ABLE is a PRACTICE: a predicate true here, now, with these people and these
> materials — derived every turn, stored nowhere, and immediately false when any conjunct
> fails.**

A PRACTICE predicate conjoins at minimum:

- a **holding** term (somebody here knows how), and
- **material** terms (the inputs physically exist here), and
- **labour** terms (people who can do it are present in the right class and number).

This is the shipped shape, widened. The live recipe gate is `"requires":
"artisan_share > 0.05"` (F3): a *social* conjunct already gates a *technological* recipe,
today, on this tree. And it is D-040 B3's exemplar exactly — *"a coastal settlement,
timber, and craft capacity"* (F11): a landlocked Empire can hold `keel-construction` at
firmness 1.0 for two thousand years and never build a ship, because the coastal conjunct is
false forever. **That is mandate item 13, and it costs nothing to build because it is how
the seam already works.**

**PROPOSED — the two firmness channels, which is what items 7–9 need and one number cannot
give.** A holding carries two firmness values, not one:

| channel | sustained by | lost when | the historical thing it is |
|---|---|---|---|
| **TACIT** | doing the work | the practitioners die or stop | the craft tradition — the smith's hands |
| **ARTICULATED** | record plus readership | the record is destroyed or nobody can read it | the treatise, the manual, the copied text |

The four loss modes the mandate names then fall out directly:

| mode | tacit | articulated | what it looks like |
|---|---|---|---|
| knowledge retained, capability lost | any | any | firmness intact; PRACTICE false because a material or labour conjunct failed — the timber is gone, the artisans receded |
| **capability retained by practice, theory lost** | high | zero | the craft survives in the doing; nobody can say why it works; one bad generation loses it with no warning |
| **theory retained, practice lost** | zero | high | the manuscript survives; re-learning is possible and slow, seeded from the record rather than from nothing |
| both lost | zero | zero | genuinely lost — and **rediscoverable**, §3(10) |
| **preserved in one holder while every practitioner dies** | zero in that settlement | high somewhere | exactly the third row, localized: the Empire-level record outlives the last workshop |

**Honest statement of cost:** two channels is a real storage decision with a real schema
consequence, and this lane does **not** design it. Whether firmness is one value or two is
**§8 Q-C7**. What this lane does claim is that with one value, "capability retained by
practice, theory lost" is **not representable**, and the mandate asks for it explicitly.

---

## §4 WHAT KIND OF STATE EACH PIECE IS — CHECKED AGAINST THE LAWS AND THE SERIALIZATION RULES

The mandate asks this explicitly. **PROPOSED**, piece by piece.

| piece | kind of state | serialized? | conserved (law 1)? | type (law 7) |
|---|---|---|---|---|
| CORPUS roster | **content data**, `Sim.Data` | no — it is config, parsed and validated at load, as goods/recipes are (F3) | n/a | n/a |
| HOLDING firmness | **persistent, non-conserved accumulated sim state** — the `SmoothedAttractivenessRow` shape (F20) and D-041's ruled *"accumulated stock … slow to build and slow to lose"* precedent (F21) | **yes** — and therefore owes ADR-005's three edits in one file, a POPULATED-table test with exact `ExpectedLength`, bit-exact round-trip and hash equality (F23; `CLAUDE.md` workflow rule) | **no** — teaching does not deplete the teacher | `double` |
| published knowledge variable | **observational** — the existing `VariableRow` mechanism, *"Observational state, not a stock"* (F7) | yes, as `VariableRow` already is — at **settlement scope only** (§6) | no | `double` |
| PRACTICE | **DERIVED READING** — recomputed every turn from published variables, the `SettlementHappiness` and `requires` shapes (F19, F3) | **NO. Never.** Nothing is serialized to make a capability durable, and *"Nothing new is serialized merely to be observable"* (F18) | n/a | `bool` from a `double` predicate |
| INQUIRY allocation | **persistent directive** — *"state describing a desired ongoing configuration"* (`docs/d042-…:99-106`, RATIFIED) | yes | the **inputs** it consumes (people, food) are conserved and move only via `Ledger` | allocation shares `double`; the people and food they claim stay `long` |
| unallocated reserve | **accumulated capacity**, fenced to rate equations (§2.3) | yes | **DIRECTOR DECISION REQUIRED** — if the reserve is literally banked person-years it is conserved and must be `long` and must move via `Ledger`; if it is a readiness ratio it is `double` and is not. **§8 Q-C2.** |  |
| era label | **derived reading, output only** — *"era labels are descriptive output only"* (F14) | no | n/a | a name |

### 4.1 Law-by-law

| law | how this design satisfies it | residual risk |
|---|---|---|
| **1 — conservation** | Knowledge is not a conserved stock and never moves through `Ledger`. Everything conserved that the design touches — the people and food inquiry consumes, the practitioners who carry knowledge when they migrate — moves **only** through the shipped `Ledger.Transfer`/`Ledger.Flow` and the shipped migration/mobility machinery. No parallel mover is introduced (the M5 placeholder's own Q4 warns against exactly that). | The reserve's typing, **Q-C2**. |
| **2 — mechanisms over modifiers** | A holding never grants anything. It appears as a **conjunct in a predicate** (F11's ruled shape) or as a **term inside a rate equation** (F14 permits coefficients inside resolution equations). The banned shape — *"research completes → permanent civilization bonus"* — has nowhere to attach, because there is no owner-scoped buff field and no completion event. | The fence on the reserve is PROPOSED, not ruled: **Q-C1/G-03**. |
| **3 — dt-correctness** | Every rate in the design — firmness gain from practice, firmness gain from inquiry, both decay rates, transmission rate — is per-sim-year and integrates with `dtYears`. No per-turn amount is named anywhere in this document, deliberately. | The 10-year atomic turn (F24) makes slow knowledge dynamics coarse; see **Q-C9**. |
| **4 — no calendar gates** | Nothing in the design reads a turn number, a year, a date or an era label. **Era labels are produced** by this architecture (the Spine's *"computed era labels"*, F25) and consumed by nothing inside it. | **CONFLICT X-06** — frozen D-011 *consumes* era labels as gates. Not this lane's to resolve. |
| **5 — determinism** | Holdings are iterated in a fixed key order (sorted arrays, never `Dictionary`/`HashSet` iteration). Predicate evaluation is already a deterministic pure tree walk with ordinal strings and invariant parsing (F1). All randomness is `RngRegistry` streams keyed by (SystemId × holder id) with state in `WorldState`. No `float`, no `DateTime`, no `GetHashCode()` as logic input, no LINQ in the hot path. | Any ordering or argmax over `double` firmness — e.g. "which line of inquiry advances" — needs a **composite key with a stable integer tie-break (score, id) and a tie-dense test** (`CLAUDE.md` workflow rule). Flagged now so no packet discovers it late. |
| **6 — isolation** | The knowledge system **publishes variables and owns one table**. It calls nobody and nobody calls it. Each consuming domain writes and evaluates **its own** predicate over the published variables — which is exactly the disposition D-042 §14.5 sanctions for the rejected seam: *"conformant only as a shared predicate grammar consumed independently by each domain system … and not as a coordinating owner."* | **CONFLICT X-02** — whether a single system owning the holding relation for every domain is the rejected God object under another name. Surfaced, not resolved. |
| **7 — types** | Firmness, rates and allocation shares are `double`. Conserved people and food stay `long` and stay in the `Ledger`. | **Q-C2.** |

### 4.2 D-021 — the paired feedback, because the design ships a positive loop

**RATIFIED obligation (F15):** every positive feedback loop ships with a negative one that
**strengthens with amplitude**, in the same milestone, and the valves are **Exit, Voice,
Endurance**.

**The positive loop this design creates (PROPOSED, stated plainly so it can be attacked):**
practice → firmness → PRACTICE predicates true → better/more production → more
practitioners and more surplus → more practice and more inquiry capacity → more firmness.

**The paired negatives, PROPOSED, each strengthening with amplitude:**

1. **Breadth taxes depth.** Practice-time is finite. The more corpus entries a holder holds
   at high tacit firmness, the less practice each receives, and the tacit decay rate rises
   with the count of held-but-under-practised entries. A civilization that knows everything
   is worse at each thing — the brake grows exactly as the loop runs.
2. **Diminishing returns on inquiry per corpus region** — imported from D-039 B2 rather
   than invented (F13).
3. **THE RATIFIED ONE, and the reason this design does not have to invent a political
   valve.** D-021 already rules it: *"Education is a flow with a lag … via rising
   expectations (frozen) — raise liberty/prospects salience. Educating your population
   remains a deliberate gamble; that tension is kept, not patched"* (F16). Knowledge raises
   literacy; literacy raises the **salience** of Liberty and Prospects (D-018's ratified
   rising-expectations mechanism, which explicitly *replaced* era tables); unmet salient
   needs raise grievance; grievance discharges through **Exit, Voice or Endurance**. The
   amplitude-strengthening property is already ruled, and the valves are already named.
   **This design adopts it and adds nothing.**

**Carrier problem with valve 3, stated honestly:** it runs through *literacy*, and there is
no literacy variable (F4). See **X-05** and **OWED-2**.

---

## §5 CARRIERS OWED

D-035-C (F17) requires every cross-system coupling to name a **physical carrier — a good, a
purse, a building, a policy, a body, a season** — and refuses it otherwise. The proposed
edges whose carrier **this lane could name** are recorded inline in §3. The edges below are
the ones it **could not**, listed as debts rather than hidden.

| # | proposed edge | what would have to carry it | why this lane cannot name it | blocks |
|---|---|---|---|---|
| **OWED-1** | **trade-borne diffusion** — knowledge crossing a contact edge between Empires | a travelling person accompanying goods | MEASURED: the shipped trade artefact is `TradeFlowRow(From, To, Good, Quantity)` — **goods move, people do not** (`docs/observability-architecture.md:58-61`). A good is not a teacher. The Spine ratifies *"diffusion"* (F25) but no document says what crosses (G-09). | §3(6) channel 4; the Spine's divergence goal |
| **OWED-2** | **articulated firmness ↔ literacy** — record-and-readership sustaining the theoretical channel | a written artefact (a good or a building) **and** a literate holder (a body) | Neither exists. There is no record good, no archive, and **no literacy or education variable in the registry** (F4). This is the same hole as X-05. | §3(5) route 3; §3(7); §3(13); §4.2 valve 3 |
| **OWED-3** | **inquiry's venue** — where deliberate work physically happens | a building and the people in it — i.e. an **institution** | *"institution"* has **six recorded meanings, zero code**, and CR-010 was recommended and never written (`docs/design/recovered-decisions-civics-institutions.md:402`, `:441`). Naming the carrier requires picking a meaning, which is a Director ruling. | §3(2) holder kinds; §3(4) route 2 |
| **OWED-4** | **settlement holdings → Empire holding**, and back down | messengers, administration, an itinerant scholar — something that physically carries knowing between the capital and the provinces | Nothing of the kind exists. This is the same hole as §6's blocker seen from the carrier side: there is no substrate *and* no carrier. | §6; §3(2) Empire holder; F10 §9.2 |
| **OWED-5** | **espionage** — D-042 §9.6 names it as a permitted source (F10) | an agent (a body) | No mechanism, no actor, no carrier anywhere in the tree. | §3(5) |
| **OWED-6** | **foreign contribution gating** — open borders and foreign institutions contributing (F10 §9.6) | a policy (the border), a body (the traveller) | The placeholder expects gating *"by distance, diplomacy, language, institutional capacity, literacy and wealth"* and says *"Those rules are not decided here"* (`docs/m5-research-technology-institutions-placeholder.md:106-108`); **nobody owns them** (G-10). | §3(5), §3(6) |

**One carrier this lane believes it CAN name, recorded so it can be attacked rather than
assumed:** practical discovery's carrier is **the practitioners and the workshop — a body
and a building** (D-035-C's own vocabulary), and the decay-by-generation carrier is
**cohort replacement**, riding the shipped 0.5-year demographic micro-steps. If a reviewer
judges either to be an invented modifier, both belong in the table above.

---

## §6 SCOPE AND RESIDENCE — THE BLOCKING STRUCTURAL FACT

**This is the part the Director asked to be stated plainly, so it is stated without
softening.**

**MEASURED (this pass, `31d8718`):** `public record struct VariableRow(SettlementId
Settlement, int VarId, double Value)` — `Sim.Core/State/WorldState.cs:394`. The published
variable table is **settlement-keyed**. There is no Empire-keyed, polity-keyed,
bucket-keyed or any-other-keyed published variable row type anywhere in `Sim.Core/State/`.

**RATIFIED (F9, F10):** D-042 §8.3 — *"Capability evaluation must be able to distinguish
SCOPE — Empire-level and Settlement-level, with unit/action scope where required."* D-042
§9.2 — *"Knowledge is ultimately **Empire-scoped**, while produced/applied through
settlements, institutions, people and research activities."*

**Therefore: KNOWLEDGE THAT BELONGS TO A CIVILIZATION HAS NOWHERE TO LIVE TODAY.** This is
recovery-lane gap **G-01** (`docs/design/recovered-decisions-knowledge-tech.md:262-266`),
independently recorded as **G-05** by the architecture lane
(`docs/design/recovered-decisions-architecture-invariants.md:423`), and it is a blocking
structural fact, not a design preference.

**Three sharpenings this lane can add, each of which narrows what the Director is being
asked for:**

1. **The blocker is in the READING, not only in the STORING.** Even if Empire-level
   knowledge were *derived* — computed on demand from the settlement holdings of the
   settlements an Empire controls, needing no new stored stock — **a D-020 predicate can
   only read a published variable** (F1: no functions, no arithmetic, operands are
   `variableName | numberLiteral`). A derived Empire quantity that is not *published* is
   invisible to every predicate in the project. So the substrate is required whichever way
   the storage question is answered.
2. **The Empire IDENTITY is not the missing piece — it ships.** `PolityRow(PolityId Id,
   CommandSource Source)` exists, and its `_doc` says exactly the right thing for this
   design: *"Government, economy, knowledge, military and institution state are SEPARATE
   domains that key themselves to this id — the Empire must not become a God object holding
   every domain's state"* (F22). The key exists; the **scoped published-variable substrate**
   does not.
3. **What would have to exist, named without designing it.** A way for a system to publish a
   named `double` **against a non-settlement key** and for a D-020 predicate to read it at
   that scope. In the repository's own terms that is a **scope dimension on published
   variables**. Whether that is a second scoped table, a scope discriminator on the existing
   relation, or something else is a **schema change and an M5+ implementation decision**,
   and this lane **does not design it** — per the mandate, and per `CLAUDE.md`'s rule that a
   milestone's specs are not written ahead of ratification.

**Consequences for this design if the substrate is never built (INFERRED, stated so the
cost of deferral is visible):** the Empire holder kind in §3(2) cannot exist; the Empire's
articulated record in §3(9) cannot exist, so *"knowledge preserved in one holder while every
practitioner dies"* collapses to the settlement case only; any PRACTICE predicate that needs
an Empire-level conjunct cannot be written; OWED-4 stays owed; and D-042 §8.3 remains
unsatisfiable. The architecture still stands at settlement scope — which is where all four
shipped variables and both live consumers are — but the word *civilization* in "what a
civilization knows" has no referent in state.

**Related and unresolved, recorded because it sits on the same substrate:** the
architecture lane records **C-06** — M5 taxation under law 1 needs a *"polity-scoped stock
that does not exist"* (`docs/design/recovered-decisions-architecture-invariants.md:364`).
Knowledge and taxation are asking for Empire-scoped state for different reasons at roughly
the same time. **INFERRED:** whoever designs one should know the other is waiting. Stated as
an observation; the sequencing is the Director's.

---

## §7 CONFLICTS SURFACED

*Mandate rule: where this design would touch or contradict something RATIFIED, surface it
with both sources and resolve nothing. Neither side is picked below.*

| # | conflict | source A | source B | this lane's position |
|---|---|---|---|---|
| **X-01** | **Is D-042's accumulating reserve the "research points" the mandate bans?** The mandate (Part 5, explicit ban): *"do not invent a 'research points' abstraction … If you find yourself proposing a single scalar that accumulates and unlocks things, stop."* | the Director's 2026-09-19 mandate, Part 5, lane C | **RATIFIED:** *"Knowledge generation is a resource/flow the player ALLOCATES among concurrent research activities"* and *"Unallocated knowledge ACCUMULATES AS A RESERVE rather than disappearing"* — `docs/d042-…:168-171` (§9.4-9.5), listed as settled at `:290-292` (§15) | **Both recorded, unreconciled.** This lane's §2.3 proposes a fence (the reserve may enter rate equations only, never a gating comparison) under which the two are compatible — but **the fence is PROPOSED and unruled** (G-03), and whether the fence is sufficient is not this lane's call. Note the recovery lane also records that two earlier documents and `docs/queue.md:1213-1214` still read the accumulation question as OPEN while D-042 §15 rules it settled (its C-06). |
| **X-02** | **Does a single system owning the HOLDING relation for every domain resurrect the REJECTED universal `CapabilitySystem`?** | **RATIFIED rejection:** *"Do not create a universal God system such as a `CapabilitySystem` that owns every capability or coordinates every domain"* — `docs/d042-…:141-142`, anti-pattern `:196-203`; the reason is Law 6, §7.1-7.2 | **RATIFIED disposition:** the seam is conformant *"only as a shared predicate grammar consumed independently by each domain system … and not as a coordinating owner"* — `docs/d042-…:276-282` (§14.5) | **Surfaced, not resolved.** This lane's §4.1 argues the design is on the permitted side — the knowledge system publishes variables and answers nothing for anybody; each domain writes its own predicate; there is no `CanDo(empire, capability)` entry point anywhere. An adversary can reasonably reply that a system owning *the* knowledge table for every domain is the same centre of gravity under another name. **§8 Q-C11** puts it to the Director rather than settling it here. |
| **X-03** | **Sub-settlement (practitioner-bucket) residence vs "knowledge is ultimately Empire-scoped."** This design's tacit channel resides in class buckets — finer than settlement, let alone Empire. | this design, §3(2)/§3(13) — **PROPOSED** | **RATIFIED:** *"Knowledge is ultimately Empire-scoped, while produced/applied through settlements, institutions, people and research activities"* — `docs/d042-…:165-166` (§9.2) | **Both recorded.** A reading on which they agree is available — §9.2's own clause *"produced/applied through … people"* arguably sanctions practitioner-level residence, with "ultimately Empire-scoped" describing the *aggregate* — but that is an interpretation of a ruling and **this lane does not get to make it**. **§8 Q-C8.** |
| **X-04** | **"Diffusion" is RATIFIED; its carrier is not nameable on this tree.** | **RATIFIED:** *"Knowledge & diffusion"* in the frozen Spine (`docs/civ-sim-architecture-v3-outline.md:84`, `:110`); *"diffusion"* as a named source at `docs/d042-…:172-174` | **RATIFIED test:** D-035-C — *"Name the physical carrier … If none exists, it is an invented modifier and is refused"* (`docs/d035-needs-aggregation.md:91-96`). **MEASURED:** the shipped trade artefact moves goods, not people (`docs/observability-architecture.md:58-61`) | **Both recorded.** Either diffusion gets a carrier that does not exist yet (a travelling-persons channel), or diffusion is exempt from the carrier test because knowledge is not conserved. **This lane cannot choose.** OWED-1; **§8 Q-C13**. |
| **X-05** | **Ratified mechanisms already depend on literacy and education variables that do not exist — and this design depends on them too.** | **RATIFIED (frozen D-018/D-021):** Intelligentsia emerges on *"literacy share + education institutions > threshold"* (`docs/d018-classes-and-needs.md:26`); the **Prospects** need is *"education access, mobility openness, growth trend"* (`:42`); rising expectations *"scales with the bucket's literacy, urbanization, and media exposure"* (`:48`); *"Education is a flow with a lag"* (`docs/d021-stability-doctrine.md` Part 3) | **MEASURED (this pass):** the registry publishes four variables and **none is literacy, education, institution or knowledge** — `Sim.Core/State/Variables.cs:93` | **Both recorded; pre-existing, not created by this design.** The recovery lane logs it as **C-07** and the capability record states it the same way: *"The dependency is ratified; the variables to satisfy it do not exist"* (`docs/capability-architecture-decision.md:224-228`, secondary evidence). This design **inherits** the gap (OWED-2, §4.2 valve 3) and does not close it. |
| **X-06** | **This architecture PRODUCES era labels; frozen D-011 CONSUMES them as capability gates.** | **RATIFIED (frozen Spine / D-040 B3 / law 4):** *"era labels are descriptive output only"* (`docs/civ-sim-architecture-v3-outline.md:22`); *"a tech-tree node opening sea travel is a calendar gate wearing a tree"* (`docs/d040-…:59-64`); the Spine's M6/M7 deliverable is *"computed era labels"* (`:110`) | **RATIFIED (frozen D-011):** *"(era-gated additions: bombard, air strike, dig-in)"*, *"Later eras arrive as data + a few new verbs"*, *"\| M11+ \| era expansions \|"* (`docs/d011-battle-layer-addendum.md:13`, `:45`, `:66`); and D-009/D-010's *"expensive, era-gated, terrain-crossing edges"* | **Both recorded; pre-existing.** Recovery-lane **C-03**: D-040 flags it against itself and declines to rule — *"It is not amended here — D-040 amends nothing — but the transport packet will have to face it"* — and **CR-009 was recommended and never written on any branch** (`docs/current-state.md:13`). **Unowned. DIRECTOR DECISION REQUIRED.** This design makes the collision more concrete, because it is the thing that would compute the labels D-011 gates on. |
| **X-07** | **Which milestone owns this work — and therefore when any of this may be specified.** | **RATIFIED (frozen Spine):** *"Knowledge & diffusion \| M6"* (`docs/civ-sim-architecture-v3-outline.md:84`, `:110`) | **RATIFIED (D-011 §6 resequence):** *"\| M7 \| knowledge & divergence \| was M6 \|"* (`docs/d011-battle-layer-addendum.md:62`); and **CR-005 is OPEN** on whether Research/Technology/Institutions sits at M5 at all | **Both recorded.** GOV-2 §1c calls the Spine row *"stale by one"* and says reconciliation *"remains REQUIRED"* — unperformed on this tree. Recovery-lane **C-02**. **This lane takes no position and notes that `CLAUDE.md` forbids writing specs beyond the current milestone + 1, so the milestone answer bounds what may be written next.** |
| **X-08** | **Does a line of inquiry complete?** This design says no. | this design, §3(12) — **PROPOSED**; supported by *"a completing project that grants a capability remains prohibited"* (`docs/adr/cr-007-…:215`, secondary evidence) and by D-040 B3 (F11) | **Unratified but explicit:** *"When research crosses completion: 1. the technology becomes available at that point in simulation time"* (`docs/m5-temporal-control-and-player-agency-placeholder.md:154-169`); M5 scope items 7–11 (`docs/m5-research-…-placeholder.md:79-84`) | **Both recorded.** Both placeholders declare themselves unratified and not specs. D-042 settles *half* — parallelism yes, one-at-a-time queue banned — and says nothing about completion. Recovery-lane **C-10** / **G-02**. **§8 Q-C3.** |
| **X-09** | **"Institution" is named by a RATIFIED ruling as a place knowledge is produced and applied, and is undefined.** | **RATIFIED:** *"produced/applied through settlements, **institutions**, people and research activities"* (`docs/d042-…:165-166`); D-035-C path 6 binds an institution carrier to M5 *"and no other way"* (`docs/d035-needs-aggregation.md:86`) | **MEASURED/recorded:** six meanings, zero code — *"`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or `Sim.Tests/`"*; **CR-010 recommended, never written** (`docs/design/recovered-decisions-civics-institutions.md:402`, `:441`) | **Both recorded.** This design consequently **cannot** make institutions holders (§3(2)) or name inquiry's venue (OWED-3). **DIRECTOR DECISION REQUIRED, unowned.** |
| **X-10** | **The two-channel firmness proposal vs the ban on schema design in this phase.** | the mandate: *"NO schema changes … DO NOT design the table"* | this design, §3(13) — **PROPOSED**: one firmness value cannot represent *"capability retained by practice, theory lost"*, which the mandate asks for by name | **Surfaced deliberately.** This lane describes the two channels **conceptually** and designs no row, no field, no width and no serialization. It records that the choice has a schema consequence and hands it over as **§8 Q-C7** rather than resolving it. |

---

## §8 DIRECTOR QUESTIONS

*Stated as questions. No preferred answer is attached to any of them.*

**Q-C1 — What is the fence on accumulation?** D-042 §15 rules that an accumulating
knowledge quantity is permitted. It does not rule the conditions under which accumulation
stays legal. Must the accumulator be able to **fall**? Are **ordered thresholds
K1 < K2 < K3 on one accumulator** tree edges in disguise? Is a monotone-in-time accumulator
a schedule? *(Recovery-lane G-03; two prior records proposed fences and none was ruled.)*

**Q-C2 — Is the D-042 §9.5 reserve a conserved quantity?** If it is banked person-years and
food, law 1 and law 7 make it `long` and it must move through the `Ledger`. If it is a
readiness ratio, it is `double` and is not conserved. The two answers have different
schemas, different tests and different failure modes.

**Q-C3 — Does a line of inquiry COMPLETE, and if so what does completion produce?**
*(Recovery-lane G-02; conflict C-10 / X-08. Two unratified placeholders assume yes; B3 and
CR-007 record that a completing project granting a capability is prohibited.)*

**Q-C4 — May a PRACTICE predicate reference another practice's stored latch, or any
capability's stored result?** Two records propose banning it and **no document rules on it**
*(recovery-lane G-04)*. The shipped grammar has no latch operand, so the ban is currently
enforced by accident of closure. Should it be ratified **before** the grammar is widened?

**Q-C5 — Does D-042 §5.4's preservation of *"unrelated accumulated Empire state"* across a
government transition say anything about CAPABILITY?** The shipped latch records current
satisfaction, not history, so it provides no such memory. *(Recovery-lane G-06; architecture
lane C-12.)* Is the join dissolved — state preserved, reading not preserved because there is
nothing to preserve — or is something more intended?

**Q-C6 — Does knowledge decay, and is decay a named `Ledger` sink with its own reason, as
spoilage and granary overflow are for grain?** *(Recovery-lane G-07. Asked twice in the
tree, answered never. The answer decides whether knowledge is a law-1 conserved `long` or a
law-7 `double`.)*

**Q-C7 — Is a holding's firmness one value or two?** With one, *"capability retained by
practice, theory lost"* is not representable. With two — a tacit channel sustained by doing
and an articulated channel sustained by record and readership — it is, at the cost of a
schema decision this lane did not make.

**Q-C8 — At what grain does knowledge RESIDE?** D-042 §9.2 says *"ultimately Empire-scoped,
while produced/applied through settlements, institutions, people and research activities."*
Does that permit residence in a practitioner **class bucket** (finer than a settlement), or
does it require that everything below the Empire be an aggregation input rather than a
holder? *(Conflict X-03.)*

**Q-C9 — What does a BREAKTHROUGH mean under the 10-year atomic turn?** The tree defines it
nowhere *(recovery-lane G-12)*; the only recorded constraint is that *"'Rapid' is meaningful
in turns, not sim-years"*, and the D-020 seam carries a one-turn evaluation lag by design.
Is a breakthrough an event, a chronicle entry, a predicate flip, or nothing at all?

**Q-C10 — Is there a TECHNOLOGY object at all, distinct from a capability?** D-042 §9.1 and
§8.2 rule technology distinct from both knowledge and capability, and **nothing in the tree
says what a technology IS** *(recovery-lane G-14)*. This design names no technology object.
Should it have one?

**Q-C11 — Does a single system owning the holding relation for every domain fall on the
wrong side of the §7.3 rejection?** *(Conflict X-02.)* If it does, what is the alternative
shape — one knowledge table per consuming domain, or something else?

**Q-C12 — What IS an institution, mechanically?** Six recorded meanings, zero code, CR-010
recommended and never written *(recovery-lane C-02/Q-01)*. Until it is ruled, institutions
cannot be holders and deliberate inquiry has no named venue (OWED-3).

**Q-C13 — What crosses a contact edge in DIFFUSION, at what rate, in which direction, and
is it conserved?** *(Recovery-lane G-09.)* And, since the shipped trade table moves goods
and not people: **does the D-035-C carrier test apply to knowledge transmission at all, or
is knowledge exempt because it is not a conserved stock?** *(Conflict X-04. The answer sets
whether §3(6)'s "no carrier, no transmission" commitment stands or falls.)*

**Q-C14 — One knowledge quantity per domain, or one global quantity allocated across
domains?** *(Recovery-lane G-05. This design assumes per (holder, corpus entry) and has no
global quantity; the question has never been ruled.)*

**Q-C15 — What is the substrate for an Empire-scoped published variable, who owns designing
it, and at which milestone?** *(Recovery-lane G-01 / architecture-lane G-05 — the blocker of
§6. Note that M5 taxation is recorded as waiting on Empire-scoped state for an independent
reason, architecture-lane C-06.)*

**Q-C16 — WHAT CALIBRATION CORRIDOR GATES KNOWLEDGE?** *"The project gates milestones on
corridors; 'science output' has no obvious historical target. A milestone that cannot be
calibrated is a milestone that cannot pass its own exit criteria."* **No candidate corridor
is named anywhere in the tree** *(recovery-lane G-08)*. This is the only open item in the
cluster that can block a **milestone exit** rather than a packet.

**Q-C17 — Is "educated population" a class, a class attribute, or new state?**
*(Recovery-lane G-11. D-018 reserves an Intelligentsia class slot; the literacy quantity
that gates it has no home — X-05.)*

**Q-C18 — Which milestone owns this cluster?** The frozen Spine says M6; D-011 §6 says M7;
CR-005 is open on whether an architecture-only packet sits inside M5. *(Conflict X-07;
GOV-2 §1c's reconciliation remains required and unperformed.)* The answer bounds what may
be specified next, because `CLAUDE.md` forbids writing specs beyond the current milestone
plus one.

---

## §9 CAVEATS ON THIS DOCUMENT

1. **Authority: none.** This lane rules nothing, resolves nothing and closes nothing. Every
   PROPOSED item is a design proposal awaiting a Director ruling. No status anywhere in the
   repository is changed by this document.
2. **Secondary evidence is declared.** `docs/capability-architecture-decision.md`,
   `docs/milestone-architecture-governance.md`, `docs/m5-roadmap-dependency-audit.md`,
   `docs/adr/cr-007-b3-exemplar-reconciliation.md` and the `docs/design/recovered-*.md`
   documents are **prior agents' reports, not rulings** (GOV-4). They are cited here as
   records of what was concluded or recovered. Where this document leans on a RATIFIED
   fact, it cites the **ruled source** and this lane re-read that source at `31d8718`.
3. **The `capability-architecture-decision.md` §4 recommendation is treated as UNVERIFIED**
   throughout, per its own correction notice and its adversarial pass returning
   SURVIVES_WITH_CONDITIONS (`:7-37`). Nothing in this design is justified by it.
4. **Nothing here is a spec.** No packet, corridor, band, acceptance criterion, `TUNE`
   parameter or test is defined. No table, row, field, width or serialization is designed —
   including the Empire-scope substrate of §6 and the two firmness channels of §3(13),
   both of which are described conceptually and handed to the Director.
5. **No independent reviewer participated.** Under ADR-015 §6, no finding in this document
   is actionable before a verdict returns on it.
6. **Scope of verification.** Facts F1–F25 were re-read on `claude/civdemo-work-b1z2y4` @
   `31d8718`. No other branch, worktree or tree state was consulted, and this lane
   performed no build, no test run and no measurement of running code.
