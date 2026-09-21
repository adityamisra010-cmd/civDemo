# ARCH-O — ARCHITECTURAL INVARIANTS (mandate Part 20)

**DESIGN, NOT IMPLEMENTATION.** This document states invariants. It implements nothing, changes no
schema, writes no M5 code, merges nothing, and edits no existing document. Created under the
Director's 2026-09-19 mandate as OUTPUT O of the synthesis lane.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `26d12d3`, working tree
clean; `origin/main` = `dbef61a`. Every `file:line` below was read on that tree.

**Author authority: none.** The DIRECTOR IS ChatGPT. **Nothing in SECTION II is ratified**, and
this document never presents a PROPOSED invariant as an existing rule. Where a proposed invariant
is *narrower* or *broader* than the ratified material it resembles, the difference is stated
explicitly rather than smoothed over — that is the whole point of the two-section split.

**Label key:** RATIFIED (cite file:line) · MEASURED (cite record + tree) · PROPOSED · INFERRED ·
DIRECTOR DECISION REQUIRED.

---

## §0 HOW TO READ THIS DOCUMENT

**SECTION I** lists invariants that **already bind**. Every row quotes its source with a
`file:line`. A future packet that violates a Section I invariant is violating a ratified item, and
the response is the S8 §3 Contradiction-Report path, not a design discussion.

**SECTION II** lists invariants the mandate asks for **where no ratified basis exists**. Every one
is **PROPOSED**. Each states three things the mandate requires: **what it forbids**, **how a
reviewer would CHECK it**, and **what it would COST**. None of them binds anyone until the Director
rules.

**The two sections are strictly separated and nothing crosses.** Where a Section II invariant has a
*partial* ratified basis — and three of them do — the ratified part is quoted **in Section I** at
its own row, and Section II states only the **residue** that is not ratified. This is the
distinction the mandate asks for by name, and §II.1 is the case it warns about.

**One standing rule this document obeys throughout.** *"A conflict is not yours to dissolve. Do not
silently reconcile two sources … Identify both sources, state which is authoritative under this
ordering, and state whether the conflict needs a director ruling."*
(`docs/gov-4-repository-freshness.md:34-37`, RATIFIED.)

---

# SECTION I — INVARIANTS WITH EXISTING RATIFIED BASIS

Each row: the invariant, its quoted source, what it forbids, and what already checks it. **The
"enforced by" column is MEASURED** (this lane, `26d12d3`) — it records what actually runs, which
is not always what the rule says.

## I.1 The law layer

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-1** | **CONSERVATION (law 1).** | *"People, money, and goods are conserved. Every aggregation, migration, or conversion operator must conserve exactly; property-based tests enforce this in CI. Anything that cannot conserve (e.g., a lossy LOD tier) is cut, not fudged."* — `docs/civ-sim-architecture-v3-outline.md:19`. Short form: *"people/money/goods change ONLY via `Ledger.Transfer`/`Ledger.Flow`. Conserved stocks are `long`. Exact equality in tests — no epsilon."* — `CLAUDE.md:16` | Any conserved quantity that moves without naming two endpoints (`Transfer`) or a named source/sink (`Flow`). Any epsilon in a conservation test. | **Mechanical, not disciplinary.** `Conserved` is a get-only wrapper whose single mutation path `Conserved.UNSAFE_LedgerSet` is **grep-gated**; *"casual `+=` does not compile at all"* — `docs/adr/adr-004-conserved-wrapper.md:8-24`. `ConservationAuditor` checks `Σ stocks + Σ sunk − Σ sourced = 0` exactly, per quantity. |
| **I-2** | **MECHANISMS OVER MODIFIERS (law 2).** | *"Effects act on real stocks and flows. Coefficients **inside** a resolution equation (terrain multiplier in a combat equation, fertility factor in a yield function) are legal. **Free-floating permanent auras ('+10% happiness') are illegal.**"* — `docs/civ-sim-architecture-v3-outline.md:20`; `CLAUDE.md:17` | A standing buff attached to an owner. **NOT** coefficients — the Spine records that v2's absolute wording made combat self-contradictory and that this precise form closes it, so *"no coefficients at all"* is a misreading with a documented history. | **Review only.** No gate. |
| **I-3** | **dt-CORRECTNESS (law 3).** | *"All change integrates over turns at dt-correct rates."* — `:21`; *"every rate is per-sim-year; integrate with `dtYears`. Never hardcode per-turn amounts."* — `CLAUDE.md:18`; *"A system that hardcodes per-turn amounts fails review"* — `docs/m0-kernel-spec.md:70` | Any per-turn constant. dt runs 10 → 0.5 across the era table, so a per-turn constant is a different quantity in every era. | **Review**, plus **CR-001's permanent dt-continuity detonator** across the turn-250 dt 10 → 5 gate (*"This test exists forever"*). |
| **I-4** | **NO CALENDAR GATES (law 4).** | *"Capability derives from computed state; era labels are descriptive output only."* — `docs/civ-sim-architecture-v3-outline.md:22`; `CLAUDE.md:19`. Restated twice inside the capability layer: *"Capability definitions **consume computed state, never hard-coded calendar unlocks**"* and the anti-pattern *"calendar-date technology unlocks where computed predicates are intended"* — `docs/d042-empire-and-player-control-addendum.md:157-158`, `:201` | Reading a date, turn number or era name to decide what is possible. Era is an **OUTPUT**. | **Review only** — and **there is ratified material in violation**: frozen D-011's *"(era-gated additions: bombard, air strike, dig-in)"* (`:13`), *"Later eras arrive as data + a few new verbs"* (`:45`), and D-009/D-010's *"expensive, **era-gated**, terrain-crossing edges"* (`:12`). **CR-009 was recommended and has never existed on any branch.** See §I.6. |
| **I-5** | **DETERMINISM (law 5) — the banned-constructs list, FROZEN.** | *"`System.Random` · `DateTime.Now/UtcNow` in sim code · `float` · `AsParallel`/unordered `Parallel.*` · iterating `Dictionary`/`HashSet` in sim logic (use arrays or sort keys) · `GetHashCode()` as logic input · culture-sensitive parse/format (always `InvariantCulture`) · LINQ in hot paths. All randomness via `RngRegistry` streams; RNG state lives in `WorldState`."* — `CLAUDE.md:20`; identical list frozen at `docs/m0-kernel-spec.md:83-84` | Each named construct. The list is **FROZEN**: adding to it is an amendment, removing from it is a CR. | **Two complementary permanent CI gates, both required on every push**: the grep gate (`scripts/check-banned-constructs.sh`) for visible constructs, and the determinism harness for *"behavioral divergence no grep can see"* — `docs/adr/adr-006-determinism-gates.md:8-21`. **Two known holes, recorded**: dictionary-iteration order and culture-sensitive format are *"enforced by code review, not by either gate"* (`:23-32`). |
| **I-6** | **ISOLATION / STATE-MEDIATED DEPENDENCY (law 6).** | *"systems never reference each other — only `State` and `Kernel`. Communication is through tables and events."* — `CLAUDE.md:21`. Director ruling: *"**Gameplay interdependence is allowed; direct code coupling is not.** … Economy may depend *conceptually* on knowledge, government, military, transport and institutions — **that never justifies a sibling call.**"* — `docs/d042-…:135-140` | A sibling domain call, in any direction, for any reason. Conceptual dependence is **permitted**; the call is not. | **By construction** (I-8) plus review. |
| **I-7** | **TYPES (law 7).** | *"conserved stocks `long`; rates/prices/ratios `double`."* — `CLAUDE.md:22`; `float` separately banned at `:20` | A third numeric type. A conserved quantity as `double`. | The grep gate bans `float`; the rest is review. |

> **A standing ambiguity that binds every citation of "law N" above.** Two statements of the laws
> exist and they are **not identical**: the frozen Spine S2 has **ten** laws
> (`docs/civ-sim-architecture-v3-outline.md:18-28`); `CLAUDE.md:15-22` has **seven**, differently
> numbered. Spine law 3 is *"no instant transformation"*; short-form law 3 is dt-correctness. Spine
> law 5 (*"computed, never assigned"*) has **no short-form counterpart**; short-form law 5 is
> determinism, which is Spine law 9. Spine laws 5, 6 (glass box), 7 (symmetry), 8 (data-driven) and
> 10 (tiered realism) are **absent from the list every agent reads first**. **No document states
> that the short form is complete, and none states that it is a subset.** Recorded as recovery-lane
> **C-01 / G-01**; **DIRECTOR DECISION REQUIRED** on which enumeration binds and what *"law N"*
> means in a future document. **This document cites both, by file, every time.**

## I.2 Kernel and ownership

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-8** | **EXPLICIT OWNERSHIP — one system per table, enforced by construction.** | *"`WorldState` is plain data: a set of tables … each owned by exactly one system … The kernel constructs each system's `SimContext` with typed writable handles to its owned tables only. **An agent cannot silently write another system's state — the reference does not exist.**"* — `docs/m0-kernel-spec.md:54-64` (FROZEN) | A second writer on a table. Ownership is a **compile-time fact**, not a convention. | `ISimSystem<TOwned>` + **one composition root**: *"Contexts are constructed in exactly one place — `SystemCatalog`, the composition root — via internal constructors"* (`docs/adr/adr-003-typed-owned-tables.md:8-13`). **MEASURED exception, sanctioned and recorded at field level**: `GoodStocks` and `Buckets` are **SANCTIONED SHARED** tables with the split stated per field (`Sim.Core/SystemCatalog.cs:31-60`). |
| **I-9** | **ONE-TURN LAG IS THE DEFAULT COUPLING; SAME-TURN EDGES ARE EXPLICIT.** | *"At turn start the kernel clones `Prev → Next` … Systems read `Prev`, write `Next`. **One-turn lag is therefore the default**; deliberate same-turn edges (later milestones) are explicit kernel-ordered handoffs listed in the interaction matrix."* — `docs/m0-kernel-spec.md:66` (FROZEN) | An undeclared same-turn dependency. | **Partially.** MEASURED: same-turn edges ship and are documented at their own sites (`ConsumptionSystem.cs:46-48`), and **the "interaction matrix" was not located on any ref** (recovery-lane **G-06**). See `arch-M-cross-system-graph.md` **X-M1**. |
| **I-10** | **SYSTEMS ARE STATELESS PURE FUNCTIONS.** | *"Systems remain stateless pure functions; cross-system communication remains Prev-reads (one-turn lag) and, later, kernel-ordered explicit handoffs."* — `docs/adr/adr-003-typed-owned-tables.md:32`; Spine: *"`step(read_only_world_prev, my_tables_rw, orders, rng_stream, dt)`"* — `:31` | State held between steps. | **Re-proved on every push** — twin runs construct every system fresh (`docs/adr/adr-006-determinism-gates.md:48`). |
| **I-11** | **STABLE INTEGER IDs ARE THE ONLY CROSS-TABLE REFERENCE; NAMES AND TEXT LIVE OUTSIDE SIM TABLES.** | *"`Table<T>` … constrains rows to `T : unmanaged` — pure value data, no object references, no managed fields."* *"Cross-table references are by typed id only … never object references"*; *"**Names, text, and variable-length data live outside sim tables**, in id-keyed registries or relational tables."* — `docs/adr/adr-001-unmanaged-table-rows.md:8-10`, `:25-28` | Storing a string, a list or a reference in simulation state. | The type constraint. It does not compile otherwise. |
| **I-12** | **NO SECOND IDENTITY ABSTRACTION — `PolityId` is the key.** | *"**Identity stays `PolityId` + `ControlRow`.** No `EmpireId`, `CivilizationId`, `SettlementOwnerId` or second ownership abstraction."* — `docs/m4-exit-inventory.md:316-317`; *"`PolityId` remains the identity key"* — `docs/d042-…:217-220`; ruled concretely by CR-011 | Minting a new identity for a new domain. What binds domains together is a **stable id**, not a container. | Review. **MEASURED as shipped**: M4 added `PolityRow`, `ControlRow`, `CapitalRow`, `CommandSource` and `EmpireQuery` *"needing no new field and no serialization change"* for the order's issuing actor (`docs/milestones.md:296-304`). |
| **I-13** | **NO GOD OBJECTS — the Empire is a relationship, not a container.** | *"**The Empire must not become a God object** containing every domain's state. Domain state stays in its own tables/systems, sharing a stable Empire identity/key."* *"The conceptual shape … is a **relationship, not an instruction to build one monolithic class.**"* — `docs/d042-…:60-65`; anti-pattern list `:198` | A monolithic Empire class. A coordinating owner of several domains. | Review, plus I-8's construction guarantee, which makes a God object **impossible to build by accident** — it can only be built by adding table after table to one registration in `SystemCatalog`, in one reviewable diff. |
| **I-14** | **CANONICAL SERIALIZATION HAS ONE EXPLICIT CHOKEPOINT; NO REFLECTION; NO RAW MEMORY.** | *"All canonical serialization lives in `Sim.Core/Kernel/CanonicalSchema.cs` … `Write`/`Read` … list every table and every field … in fixed order, plus an `ExpectedLength` … One integer `Version`, bumped on any schema change."* — `docs/adr/adr-005-canonical-schema.md:8-14`. *"raw struct memory, `MemoryMarshal`, and unsafe copies are banned in the serializer … **No reflection** … **Doubles as raw IEEE-754 bits** … with NO normalization of −0.0 or NaN"* — `:18-28` | Adding state anywhere else. Normalising a double before hashing. | *"Adding state = three edits in one file … the anti-padding test and the pinned golden hash break loudly until all three agree"* (`:29-30`). |
| **I-15** | **EVERY NEW SERIALIZED ROW TYPE SHIPS A POPULATED-TABLE TEST.** | *"Every new serialized row type ships a POPULATED-table test: exact ExpectedLength, bit-exact round-trip, hash equality. **Empty-table coverage proves nothing** (T1.1/T1.3 precedent)."* — `CLAUDE.md:38` | An empty-table round-trip standing in as coverage. | Review; the precedent is named. **Binds every row any sibling lane proposes** — a knowledge holding, a persistent directive, an institution row, a capability latch. |

## I.3 The player/AI boundary

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-16** | **ONE ORDER PIPELINE FOR PLAYER AND AI.** | *"**Player and AI use the same downstream pathway:**"* `Human Player → Player Control` and `AI → AI Decision Engine`, both into `Orders / Directives → Validation → Simulation Systems → Next World State` — `docs/d042-…:110-128`. And: *"**The simulation must not contain separate gameplay physics for human- and AI-controlled Empires.** Player control is an **authority distinction, not a different simulation model.**"* — `:130-132` | `PlayerEmpire` / `AIEmpire` as separate simulation entities. An AI that writes state directly. Difficulty as hidden resources. | Review. Ratified ancestor is Spine law 7: *"AI actors use player-identical verbs and information class. Difficulty = information and **friction**, never hidden resources."* — `docs/civ-sim-architecture-v3-outline.md:25` |
| **I-17** | **UI CODE MUST NEVER MUTATE SIMULATION STATE.** | *"**A button represents player intent. UI code must never directly mutate simulation state, nor call domain systems to perform outcomes.**"* — `docs/d042-…:108-109`; anti-pattern `:202` | Any write from the presentation layer. | **Mechanised**: mutation through `IReadOnlyWorldState` must not compile, proven by a gate that **passes only when the build FAILS** with CS0200 and CS1061 — `scripts/check-readonly-proof.sh:2-8`. |
| **I-18** | **A PERSISTENT DIRECTIVE IS STATE, NOT A RE-ISSUED ORDER — and every order-delivery semantic gets its own turn-exact pin.** | *"**Persistent directives:** research allocation, policy allocation, storage investment, recurring trade routes. **A persistent directive is state describing a desired ongoing configuration — it is NOT re-issued every turn to keep working.**"* — `docs/d042-…:99-106`. *"Replay equality proves reproducibility, not semantics. Every order-delivery semantic … gets its own turn-exact pin — live-vs-replay comparison alone cannot see stamping drift (T1.9 precedent)."* — `CLAUDE.md:39` | Modelling a standing configuration as a per-turn order. Relying on a golden or a replay to prove *when* an order applies. | Review + the named precedent. A directive is a serialized row, so it also inherits **I-15**. |

## I.4 Observability

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-19** | **READ-ONLY OBSERVABILITY — the one rule.** | *"**The logger observes. The simulation calculates. The UI reads. The player issues orders.** Observability must not become a second simulation."* — `docs/observability-architecture.md:16-17` | An observer that computes a simulation quantity. | Review. **Freshness fact that travels with every citation of this document:** it is **not on `origin/main`** (MEASURED this lane) — see **§I.7**. |
| **I-20** | **THE FIVE-KIND FIELD TAXONOMY, AND GAP AS A CONFORMANT ANSWER.** | *"Every field in every record is therefore one of exactly five things, and the record type says which: **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED**"* — RECOMPUTED being *"a call to a PUBLIC static simulation function on stored state — never a private re-implementation"*. *"Nothing else is permitted. If a quantity would need a formula the simulation does not expose, it is a **GAP** … rather than an observer-side copy of the formula that will drift."* — `docs/observability-architecture.md:19-32` | A re-implemented formula on the observer side. **An honest GAP is conformant.** | Review. **Note: "DERIVED" is not one of the five kinds** — it is the tree's separate word for a *reading* (`SettlementHappiness.cs:6-8`). Four sibling lanes recorded the same correction to the mandate's four-name summary. |
| **I-21** | **NO HIDDEN SERIALIZED DERIVED STATE.** | *"No simulation constant moves for observability. No system reads any record. **No record is serialized into `WorldState` or the canonical schema.** No UI component computes a simulation quantity from a private formula."* — `docs/observability-architecture.md:331-337`. And: *"records are never a source of truth, so they can always be rebuilt by replaying the order log through the same observer."* — `:276-279` | Serializing anything **merely** to be observable. | Review + I-14's chokepoint, which makes any new serialized state a three-edit diff in one file. |
| **I-22** | **A DERIVED READING STAYS DERIVED — the `SettlementHappiness` precedent.** | *"**Happiness stays derived.** Not a stock, not serialized, no persisted row. The causal direction is conditions → happiness → migration pressure → movement → changed conditions → recalculated happiness. **Migration must never grant happiness directly.**"* — `docs/m4-exit-inventory.md:310-312`. In code: *"a **DERIVED READING**, never a stock … It does not accumulate, it does not decay, nothing integrates it, and it is **NOT SERIALIZED**"* — `Sim.Core/State/SettlementHappiness.cs:6-17` | Promoting a derived reading to a stock. **And the short-circuit edge**: granting the reading directly instead of moving the conditions. | Review. **MEASURED, and it is the strongest enforcement pattern in the tree**: a reading that must not act yet is fenced by an **allowlisted grep gate with one reviewable reason per entry** — `scripts/check-read-isolation.sh:1-22`, which fails CI on any sim-side reference to the Grievances or NeedSatisfactions tables outside its allowlist. |
| **I-23** | **NO MECHANIC IS INVENTED TO MAKE A TOOLTIP ACTIONABLE.** | *"Weather, arable land, deposit abundance and terrain have no M4 lever; the explanation names them as conditions, not recommendations. **No mechanic is invented to make a tooltip actionable.**"* — `docs/observability-architecture.md:247-250` | Observability creating design pressure. | Review. |

## I.5 Cross-system discipline

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-24** | **D-021 — PAIRED FEEDBACK, PROJECT-WIDE.** | *"**Every positive feedback loop in the design must ship with at least one negative feedback loop that *strengthens with amplitude*.** Grievance→riot→economic damage→grievance is legal only because riot participation itself generates exhaustion, discharge, and exit at rates that grow with the outburst. **This rule is project-wide:** it binds unrest, markets (price rises pull supply), epidemics (susceptibles deplete), and war (exhaustion) equally."* — `docs/d021-stability-doctrine.md:8`. Landing rule: *"**the brakes install with the gas pedal, never after.**"* — `:48` | An accumulating quantity with no named damping mechanism that grows with the quantity. A brake scheduled for a later milestone than its loop. | **Three corridor tests**: ignite-and-burn-out (no permanent saturation lock across any 200-turn window), no empty menus, collapse-is-a-transition — *"A doom loop discovered by these tests is a mechanism bug — **fix the mechanism, never script the outcome**"* (`:10-15`). **Ignite-and-burn-out enters the battery at M5.** |
| **I-25** | **THE VALVES ARE EXIT, VOICE, ENDURANCE, AND ONE CHANNEL IS ALWAYS OPEN.** | *"grievance expresses through three competing channels — **Exit, Voice, or Endurance** — and the sim always keeps at least one channel open."* — `docs/d021-stability-doctrine.md:24-34` | A state in which no channel is open. *"'Beyond repair' is definitionally impossible."* | The three corridor tests above. **MEASURED as live**: ADR-025's bounded-migration `ω` is recorded as *"the **Exit valve's** openness, not needs/grievance state"* (`docs/m4-exit-inventory.md:332-333`). |
| **I-26** | **THE D-035-C CARRIER TEST.** | *"**Name the physical carrier — a good, a purse, a building, a policy, a body, a season.** If none exists, it is an invented modifier and is **refused**."* And: *"the seven paths are not a taxonomy to be extended by analogy; a coupling that does not fit one of them has not found an eighth path, it has failed the carrier test."* — `docs/d035-needs-aggregation.md:91-98` | Any cross-system coupling without a named physical carrier. *"This is law 2 given an operational form."* | Review. **MEASURED as the tree's own working instrument**: ADR-021 names *"unplaced departure demand: the migration→colonization **carrier**"* for a quantity that was *"structurally absent, not discarded"* (`docs/adr/adr-021-unplaced-departure-demand.md:1`, `:22-26`). |
| **I-27** | **A SECOND CHANNEL FOR AN ALREADY-REPRESENTED SIGNAL IS DOUBLE COUNTING.** | *"Food variety is represented exclusively by D-035-A. A second dietary-diversity happiness mechanism is **rejected as double counting** of the same underlying signal. **Any future change to the salience of food variety must calibrate D-035-A rather than introduce another channel.**"* Rejected *"by name, and not to be reintroduced under another name."* — `docs/adr/adr-023-food-variety-is-d035a-only.md:3-8`, `:25` | Adding a mechanism for a phenomenon an existing mechanism already carries. | Review. The generalisable obligation: **before adding a mechanism, establish whether it and an existing mechanism are the same phenomenon.** |
| **I-28** | **NOTHING SPAWNS.** | *"**NOTHING SPAWNS.** Every actor, settlement and hostile force must originate from population already simulated. Civ-style barbarian spawning is permanently anti-scoped."* — `docs/d037-emergent-polities.md:14-16` | Any actor — army, raider, faction, institution staff, scholar — that does not come out of the shipped population model. | Review. **Binds every sibling lane's proposed actor.** |
| **I-29** | **ONE DENOMINATION CHOKEPOINT, AND NAMES CARRY UNITS.** | *"`Sim.Core/Pathing/LatticeGeometry.cs` is now the only place where lattice units and physical units meet … Three things enforce it, **because a convention nobody can fail is not a fix**: 1. **Names carry units** … A bare `farmland` or `budget` is now a review finding. 2. **A grep gate** … **Tests are not exempt**. 3. **A physical-plausibility test.**"* — `docs/adr/adr-013-…:67-97` | A bare quantity name. A hand-recomputed conversion in a test. | The three-layer pattern, *"proven RED on both fault shapes … before being accepted green, is the standard for any structural gate"* (`:269-271`). |
| **I-30** | **A CORRIDOR MAY NOT BE FITTED TO THE ARTIFACT IT MEASURES; A QUARANTINE IS A TRIPWIRE.** | *"**CR-002 and CR-003 both forbid fitting the instrument to the artifact.**"* — `docs/milestones.md:357`. *"**The shared quarantine helper asserts the precondition is still ABSENT**, so each family fails loudly the moment famine returns: **a tripwire, not a mute button. This is how a quarantine is built.**"* — `docs/adr/adr-013-…:272-274` | Re-banding a corridor a shipped mechanism cannot satisfy. A quarantine that merely silences. | The corridor-independence requirement, S8 §4.1 item 3: *"A corridor whose denominator moves with the measured quantity is SELF-REFERENTIAL and is **REFUSED at spec time**, not discovered later"* (`docs/spine-s8-governance-freeze.md:154-182`), with an **algebraic** cancellation test and a **counterfactual** test. |
| **I-31** | **ORDERING AND ARGMAX OVER DOUBLES USE A COMPOSITE KEY WITH A STABLE INTEGER TIE-BREAK.** | *"Any ordering or argmax over double-valued scores uses a composite key with a stable integer tie-break (score, id) — **and ships a tie-dense test proving it.**"* — `CLAUDE.md:37` | An unstable sort or argmax anywhere a `double` decides. | Review + the required tie-dense test. **Binds every future selection mechanism** — destination choice, target choice, allocation ranking, "which line of inquiry advances". |
| **I-32** | **TERM NAMESPACING BY DOMAIN.** | *"**NAMESPACE BY DOMAIN.** A term belongs to ONE domain. The domain that holds it keeps the bare word; every other domain must qualify or rename."* … *"**THE CLAIMANT IS THE DOMAIN WITH THE MECHANICAL DEPENDENCY** — code, data, or a serialized identifier — **not the one that used it first in prose.**"* — `docs/conv-1-term-namespacing.md:44-50` | One word meaning two things across two domains. | Review. **CONV-1 carries no architectural weight and may not be cited to justify a mechanism change** (`:5-13`) — a subordinate document class, deliberately. |

## I.6 Governance and verification hygiene (ADR-015 and the freeze protocol)

| # | invariant | source (quoted) | what it forbids | enforced by (MEASURED) |
|---|---|---|---|---|
| **I-33** | **ONE WORKTREE PER VERIFYING AGENT; NO FINDING IS ACTIONABLE BEFORE ITS VERDICT RETURNS.** | *"One worktree per verifying agent, never shared — concurrent mutation of a shared tree voids findings and refutations alike. No finding is actionable before its verdict returns; applying a fix on a finder's word alone is a review bypass, and **a claim written into a commit message must have been measured by the agent writing it.**"* — `docs/adr/adr-015-verification-hygiene.md:179-185`; `CLAUDE.md:41` | Acting on an unverified finding. Writing an unmeasured claim into a commit message. | Review. Precedent named: **T3.3 — a shipped regression built on a finding later refuted.** |
| **I-34** | **VERIFICATION WORKTREES PIN TO THE PACKET COMMIT; MUTANT KILL-RECORDS NEED A SEMANTIC TEST.** | *"Verification workflows pin their worktrees to the packet commit under review; **findings against any other tree are void** (T2.1 precedent). Mutant kill-records must include at least one semantic test per mutant — **golden-only kills don't count.**"* — `CLAUDE.md:40` | A finding measured against the wrong tree. A golden hash moving counted as a kill. | Review. |
| **I-35** | **EVERY MUTANT RUN IS BOUNDED; A HANG IS ITSELF A FINDING. A VERIFY STAGE ANSWERS TWO QUESTIONS.** | *"A run that exceeds its bound is killed, not waited on. **A mutant that hangs is itself a finding** … Non-termination is a stronger result than a survived mutant, not a weaker one."* — `:193-213`. *"**A lens can have perfect teeth and bite the wrong thing.** A verify stage confirms that a test fails against a mutant; it does not confirm that the test is asserting a property the system ought to have. Both are required, and only the first is mechanical … When those two answers disagree, the second wins."* — `:215-236`; `CLAUDE.md:42` | Waiting indefinitely on a mutant. Treating teeth as aim. | Review. |
| **I-36** | **EVERY GUARD SHIPS WITH A TEST THAT FAILS WHEN IT IS REMOVED, AND THE RED IS PROVEN.** | *"Every guard, clamp, rail, floor, cap or validation ships with a test that FAILS when the guard is removed — and the red is PROVEN, not assumed. Delete the guard, run the test, watch it fail, restore it. **A guard added without that step has not been tested; it has been described.**"* — `:296-308`; *"A modified guard re-opens its red proof"* — `:872` | A guard whose test was never seen red. A guard test on a fixture so extreme that a different mechanism reaches the answer first. | Review. **The companion rule**: *"**an assertion on a saturated quantity compares limits, not behaviour.** Before comparing two values produced by a system with clamps, assert that the values are strictly inside their limits"* (`:315-332`). |
| **I-37** | **A FINDING IS A MEASUREMENT PLUS AN INTERPRETATION, AND THEY VERIFY SEPARATELY.** | Director ruling, recorded verbatim: *"In T3.5, three of four killed findings had exactly correct numbers and wrong conclusions. Independent lenses agreeing raises confidence in the measurement only — it does not corroborate the interpretation … Before relaying a finding as a defect, check that some ratified item actually REQUIRES the property claimed to be broken; 'this looks wrong' is not a conflict. **The director made the same error on the same finding.**"* — `:486-496` | Relaying "this looks wrong" as a defect. | *"The defence is **procedural** — cite the requirement — **not attentional**"* (`:538`). **This document's Section I/II split is that defence applied to invariants.** |
| **I-38** | **PRE-COMMIT THE READING OF BOTH OUTCOMES, WITH A DISCRIMINATING OBSERVABLE.** | *"**An interpretation ruled AFTER a measurement is a negotiation; ruled BEFORE, it is a result.**"* — `:651-656`; *"A pre-committed reading requires a DISCRIMINATING observable"* — `:754` | Deciding what a number means after seeing it. | Review. |
| **I-39** | **THE FREEZE PERIMETER AND THE CONTRADICTION-REPORT PATH.** | FROZEN at M0 exit: *"Spine S1–S5 and this S8 … Kernel code contract … banned-constructs list … All CLOSED decision-log entries: D-001…D-008, D-011 …, D-009/D-010 …, D-018 … The milestone ladder order M0→M11+ and each milestone's exit-criteria *definitions*."* — `docs/spine-s8-governance-freeze.md:13-17`. Only three things open it: an **internal**, an **empirical**, or a **law** contradiction (`:27-29`), via a five-field report (`:37`). *"a better idea, a taste change, a new feature wish … are **parked**"* into `docs/queue.md`, **write-only until the M10 slice gate** (`:31`). | Redesigning a frozen item. Treating "a better way exists" as a conflict. | Review + the director. *"**A freeze with a free override is theater; a freeze with a priced override is governance.**"* (`:33`) |
| **I-40** | **THE FOUR S8 §4.1 SPEC ITEMS, MANDATORY FROM M4 ONWARD.** | *"Every milestone spec from M4 onward carries all four. Item 1 is a task packet (the first one in §4); items 2–4 are spec content."* — **FOUNDATIONS AUDIT as packet one · dimensional declaration · corridor independence · coupling map** — `docs/spine-s8-governance-freeze.md:51`, `:68-71`; ADR-014 | An M5+ spec without all four. | *"**The director adjudicates the table, not the presence of headings.**"* Explicitly **not** a CI lint: *"a lint that checked for the presence of four section headings would measure compliance rather than thought"* (`docs/adr/adr-014-…:70-71`). **Its honest limit is stated too**: *"The four items reduce avoidable surprises; they do not license skipping measurement, and an audit that returns 'all clean' is evidence about paper, not about the world"* (`:87-100`). |
| **I-41** | **THE SOURCE-OF-TRUTH HIERARCHY, AND "A CONFLICT IS NOT YOURS TO DISSOLVE".** | Ten ranks, *"1. Explicit director rulings … 5. The current repository implementation … 8. Previous Claude/agent reports … 10. Agent inference."* — `docs/gov-4-repository-freshness.md:19-32`. And: *"Do not silently reconcile two sources … A conflict between two FROZEN items is the CR path in `CLAUDE.md`, not a judgement call."* — `:34-37` | Collapsing LOCAL / REPORTED / REMOTE / MAIN into *"the repository has it"* (`:41-57`). Rewriting a frozen document because a later decision supersedes part of it (`:99-112`). | Review. **This is the rule under which every conflict in this design phase was recorded rather than resolved.** |

## I.7 Two facts about Section I's own evidence, recorded because they bound it

**MEASURED (this lane, `26d12d3`).**

1. **`docs/observability-architecture.md` is not on `origin/main`** (`dbef61a`). Invariants **I-19,
   I-20, I-21 and I-23** are quoted from a document that exists on this branch and on
   `t4.19-glass-box`, and whose own header says *"Not merged to main"*. arch-FGH records the same
   (**X-5**). **They are cited here as RATIFIED because their content is ratified design and is
   cited as such across the tree — but a reader must know which tree they stand on.** This
   document stands on `26d12d3`.
2. **`scripts/check-read-isolation.sh`'s allowlist is a reviewable artefact, not a proof.** It
   enforces I-22's fence by grep. MEASURED: its allowlist has grown at least twice with a stated
   reason per entry (`:11-22`), which is the pattern working as designed — and it is also the
   pattern by which such a fence erodes if a reason is ever accepted without scrutiny.

---

# SECTION II — NEW PROPOSED INVARIANTS

**Everything in this section is PROPOSED. None of it binds anyone. None of it is ratified.**
Each states what it forbids, how a reviewer would **CHECK** it, and what it would **COST**.

Three of the six have a **partial** ratified basis, quoted above in Section I; each says exactly
what part is already ratified and what part is the residue this section proposes. **The mandate
warns specifically about the first one, and it is right to.**

## II.1 — NO UNIVERSAL CAPABILITY ABSTRACTION

> **PROPOSED (residue only).** No system may own capability evaluation on behalf of other domains.
> Each domain system evaluates its own predicates over published variables, inside itself,
> accepting the one-turn lag.

**WHAT IS ALREADY RATIFIED, and it is narrower than the invariant's headline.** The **rejection**
is ratified: *"**Do not create a universal God system such as a `CapabilitySystem`** that owns
every capability or coordinates every domain"* — `docs/d042-…:141-142` (§7.3), repeated in the
anti-pattern list as *"a universal `CapabilitySystem` **God object**"* (`:200`). So is the
**permitted disposition**: *"The seam is conformant **only** as a *shared predicate grammar
consumed independently by each domain system* — the shape D-020 already has — and **not** as a
coordinating owner."* — `:280-282` (§14.5).

**THE CARE THE MANDATE ASKS FOR, stated precisely, because the status is contested in three
separate ways.**

1. **The rejected PROPERTY is narrower than the rejected NAME.** §7.3 sits *inside* §7, titled
   *"STATE-MEDIATED DEPENDENCY (reaffirms Law 6)"* (`:134`), whose two preceding clauses are its
   premises. The object is named twice and both times by the same property: one that *"owns every
   capability **or** coordinates every domain"*. The recovery lane draws the conclusion and this
   lane repeats it verbatim rather than restating it: *"**The rejected property is therefore
   CROSS-DOMAIN COORDINATION AND OWNERSHIP, not capability evaluation as such**"* —
   `docs/design/recovered-decisions-architecture-invariants.md` §4a. **§8.1, in the very next
   section, ratifies that capability evaluation continues**, on the D-020 machinery. **A future
   proposal that evaluates capability without coordinating domains is therefore not obviously
   inside the rejection**, and a reviewer who cites §7.3 against it is over-reading.
2. **D-042's own frozen status is an open question.** D-042 is a director design ruling and an
   exempt document class under S8 §4 (`:3`), and it is **not** listed in the S8 §1 freeze
   perimeter, whose D-entry list names D-001…D-008, D-011, D-009/D-010 and D-018
   (`docs/spine-s8-governance-freeze.md:16`). **Whether a D-record issued after the M0 freeze is
   itself frozen, and what procedure amends one, is recovery-lane G-02 and is unanswered.**
3. **There is a contradiction candidate INSIDE the same record.** §8.3 requires capability
   evaluation to distinguish **Empire** and **Settlement** scope (`:155-156`), and the shipped
   substrate is settlement-keyed only. *"An argument that scope-distinguishing evaluation
   *requires* a central owner would be an argument between §8.3 and §7.3, both inside one record,
   and would be a contradiction to escalate rather than a design choice to take."*
   (`docs/design/recovered-decisions-architecture-invariants.md` §4a, INFERRED there, repeated
   here as such.)

**WHAT THE RESIDUE FORBIDS, then, that §7.3 does not already forbid in those words:** a single
entry point of the form `CanDo(scope, capability)` that any domain calls, **even if it holds no
state and coordinates nothing** — i.e. the *shape*, not only the *ownership*.

**HOW A REVIEWER WOULD CHECK IT.** Three mechanical questions, all answerable from a diff:
*(a)* does any type expose a method that answers a capability question **on behalf of a caller in
another domain**? *(b)* does any system hold a table whose rows are keyed by *(domain, capability)*
across more than one domain? *(c)* MEASURED as the live risk, from `arch-M` §5: **all four
published variables are published by `ClassMobilitySystem`**
(`Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:144-165`) and the shared `Predicate` type
lives in `Sim.Core.Systems.ClassMobility`, reached by qualified name from `ProductionSystem`
(`ProductionSystem.cs:105-108`). **The tree is already one refactor away from failing (c) by
accretion**, and arch-D §5.9 records the same observation as *"a latent conformance hazard of
location, not of mechanism."*

**WHAT IT WOULD COST.** Each domain pays for its own predicates, its own registrations, and
accepts the one-turn lag (I-9) — so the same capability question asked by three domains is
answered three times, with three chances to diverge. **And it forecloses the obvious answer to
§8.3's scope requirement**, which is precisely the tension in point 3. **DIRECTOR DECISION
REQUIRED** on whether the residue is wanted at all.

## II.2 — NO RESEARCH-POINTS SHORTCUT

> **PROPOSED (residue only).** No single accumulating scalar may be the left operand of a
> comparison that gates anything.

**WHAT IS ALREADY RATIFIED, and it does NOT say this.** Three guards are ratified — *"**Do not
collapse these concepts into a single rigid technology tree**"* (`docs/d042-…:175`), *"a rigid
one-at-a-time research queue"* as an anti-pattern (`:196-203`), and calendar unlocks (I-4). And
the opposite of this invariant's headline is *also* ratified: *"**Knowledge generation is a
resource/flow the player ALLOCATES**"* and *"**Unallocated knowledge ACCUMULATES AS A RESERVE**
rather than disappearing"* (`:168-171`), listed as settled at `:290-292`. **An accumulating
knowledge quantity is permitted, answered YES.**

**THE CONFLICT, SURFACED AND NOT RESOLVED.** arch-C records it as **X-01**: the mandate's Part 5
ban (*"do not invent a 'research points' abstraction … If you find yourself proposing a single
scalar that accumulates and unlocks things, stop"*) against D-042 §9.4–9.5. **Both are recorded;
neither is chosen here.** Note also that two earlier records and `docs/queue.md:1213-1214` still
read the accumulation question as **OPEN** while D-042 §15 rules it **settled** (recovery
knowledge-lane **C-06**).

**WHAT THE RESIDUE FORBIDS.** Not accumulation — **gating on an accumulator**. arch-C §2.3 states
the fence and labels it PROPOSED: the reserve *"may appear **only** as a term inside a rate
equation, never as the left operand of a D-020 comparison that gates anything."* Under that fence
the reserve is a law-2-legal coefficient (I-2) and *"is structurally incapable of being research
points, because the grammar never sees it."*

**HOW A REVIEWER WOULD CHECK IT.** **This one is genuinely mechanical, and that is its strongest
argument.** D-020 predicates are **data**, parsed at load, with a closed grammar whose operands are
`variableName | numberLiteral` (`Sim.Core/Systems/ClassMobility/Predicate.cs:10-27`). A reviewer
checks that the accumulator's name is **not in the published-variable registry**
(`Sim.Core/State/Variables.cs`). If it is not published, no predicate can read it, and the check is
a grep over one file. **Two things would defeat the check**, and both are recorded: a system may
publish a variable *derived* from the accumulator with no grammar or schema change (arch-D **Q3**);
and **ordered thresholds K1 < K2 < K3 on one accumulator** are proposed by two records to be *"tree
edges in disguise"* and are **ruled by none** (recovery **G-03**).

**WHAT IT WOULD COST.** Every gate must then be a **conjunction over several published quantities**
rather than one number, which is more data, more registry entries, and more publishers. It also
removes the simplest possible player-facing readout ("you have N knowledge"). **DIRECTOR DECISION
REQUIRED** — recovery **G-03**, arch-C **Q-C1**, arch-D **Q4**.

## II.3 — KNOWLEDGE ≠ TECHNOLOGY ≠ CAPABILITY

> **PROPOSED (residue only).** The three may not share one stored quantity, and none may be
> computed as a pure function of another.

**WHAT IS ALREADY RATIFIED — and the distinction itself IS ratified, unusually cleanly.**
*"Capability is **distinct from** raw state, knowledge, research activity, technology, policy,
institution and action"*, with the chain `State → Published Variables → Predicates → Capabilities →
Available Actions` — `docs/d042-…:149-152` (§8.2). And *"**Knowledge is distinct from technology
and capability.**"* — `:164` (§9.1). **Eight concepts ruled non-interchangeable; collapsing any two
is a ruling violation, not a taste question.**

**WHAT THE RESIDUE ADDS.** The ruling says the concepts are distinct. It does **not** say they may
not share a representation, and it does not say one may not be derived from another. The residue
is the **storage** half.

**A MEASURED fact that makes the residue harder than it sounds.** *"nothing in the tree says what a
technology IS"* — a row, a predicate, a named bundle, or purely a label over a satisfied capability
(recovery knowledge-lane **G-14**). **Both arch-D and arch-E were written without needing the
object at all**, which arch-E records as *"itself evidence about the answer"* (**Q-E12**). An
invariant separating three things, one of which has no definition, is an invariant that cannot yet
be checked.

**HOW A REVIEWER WOULD CHECK IT.** *(a)* Count the serialized row types: three distinct concepts
should not collapse into one table without a stated reason. *(b)* Check that no PRACTICE/capability
predicate reads **only** knowledge terms — arch-C §2.3 states the shape: *"A holding never grants
anything. It is published as a variable; a PRACTICE predicate may *conjoin* that variable with
material and labour terms."* A predicate whose only conjunct is a knowledge variable **is** the
banned "knowledge → capability" identity wearing a predicate. *(c)* The check is **not available
for technology** until **G-14** is answered.

**WHAT IT WOULD COST.** At least two serialized concepts where one would do, and a schema bump
(I-14) plus a POPULATED-table test (I-15) for each. **It also costs a public commitment that the
player-facing readout is at least two numbers, not one.**

## II.4 — DISCOVERY ≠ ADOPTION

> **PROPOSED (fully — no ratified basis was located for the separation as a rule).** That a thing
> is known must not, by itself, make it done. Institutional adoption is a separate mechanism with
> its own state and its own preconditions.

**WHAT IS ALREADY RATIFIED, and it is adjacent rather than identical.** D-040 B3 rejects the unlock
shape: *"a tech-tree node opening sea travel is **a calendar gate wearing a tree**"*, and rules the
sanctioned form — *"sea travel becomes possible when the conditions for boats exist — a coastal
settlement, timber, and craft capacity — **in the same shape as class emergence**"*
(`docs/d040-discovery-and-control.md:59-70`). That forbids **node → capability**. It does not, in
those words, require a separate **adoption** object.

**MEASURED, and it cuts the other way for one case.** Geographic knowledge is ruled **monotone**:
*"discovery reports a permanent fact about the land. **Once known, land stays known.**"*
(`docs/d040-discovery-and-control.md:51-54`). arch-E reads that ruling as **scoped to the map and
not to practice** and flags its own reading as the thing that could be wrong (**X-E3**). **DIRECTOR
DECISION REQUIRED** if any packet relies on the distinction.

**WHAT IT FORBIDS.** A discovery event that changes what a settlement does without anything else
changing. Concretely: a completion that grants. **And this is contested**: two unratified
placeholders assume research **completes** and the technology *"becomes available at that point in
simulation time"*, while CR-007 and the capability record hold that *"a completing project that
grants a capability remains **prohibited**"*. **D-042 settles half** — parallelism yes, one-at-a-time
queue banned — **and says nothing about completion** (recovery **C-10 / G-02**; arch-C **X-08**,
arch-E **X-E4**).

**HOW A REVIEWER WOULD CHECK IT.** Ask of every proposed knowledge edge: **name the carrier of
adoption** (I-26). If the answer to *"what physically changed in the settlement"* is *"the
knowledge did"*, the edge is a modifier. arch-FGH §4.3 states the checkable version — four failure
modes of knowing-without-being-able — and arch-C §3(13) states the separation as two objects:
*"HOLDING is a proposition held by a named holder. PRACTICE is a predicate that is true here, now.
They are different objects with different storage, and **neither can be derived from the other**."*

**WHAT IT WOULD COST.** Adoption needs its own state and its own carrier, and **the carrier is
owed**: CD-2 (an institution), CD-3 (literacy), CD-5 (the knowledge → capability join itself) in
`arch-M-cross-system-graph.md` §8. **It also costs the player the satisfying moment**: nothing ever
"completes". arch-E **Q-E1** puts the honest cost of the alternative: without a continuous quantity
between conditions and predicate, *"Part 7 degenerates to 'a capability predicate flips when its
conjuncts align' … but cannot represent inquiry that was attempted and failed."*

## II.5 — AN ENVIRONMENTAL EFFECT IS NOT AN ARBITRARY MODIFIER

> **PROPOSED (residue only).** An environmental quantity may enter the world only through a named
> medium with a named sink, and only at a site where a physical mechanism already reads a
> comparable quantity.

**WHAT IS ALREADY RATIFIED.** I-2 (coefficients inside equations, auras banned) and I-26 (the
carrier test) together already forbid a free-floating pollution penalty. **The residue is the
*medium and sink* clause**, which neither states.

**WHY the residue is worth stating separately, MEASURED.** `grep -rn -i "pollution"` over `docs/`,
`Sim.Core/` and `Sim.Data/` returns **zero hits** at HEAD (recovery climate-lane, gap 14). The only
ratified guidance is the Spine's M9 row, *"degradation stocks close loops"*
(`docs/civ-sim-architecture-v3-outline.md:89`) — five words, and the milestone that row names is
itself **AMBIGUOUS** after D-011 §6 (GOV-2 §1c). **An entire domain with one ratified sentence is
exactly where an arbitrary modifier would enter unchallenged.**

**HOW A REVIEWER WOULD CHECK IT.** Four questions, each answerable: *(a)* **what medium** — arch-I
§7.3 proposes three that behave differently (air: local, no transport; water: **travels downstream
along `TerrainSet._riverPolylines`, which are already immutable and hash-folded**; soil:
accumulates in the hinterland, does not travel). *(b)* **what sink** — a decay rate per sim-year
integrated with `dtYears` (I-3), plus dilution for water. *(c)* **what reads it** — and the
honest answer for air today is **nothing**, which is why arch-I proposes air *"for omission, not
for inclusion at zero"* (**O1**). *(d)* **is the quality a reading or a stock** — arch-I §7.4
proposes **ENVIRONMENTAL QUALITY is a DERIVED READING, never a stock**, on the I-22
`SettlementHappiness` precedent.

**WHAT IT WOULD COST.** It forbids the cheap version outright. **And it collides with a ratified
substrate**: the layers a soil or land-quality mechanism would write to are immutable, uncloned and
hash-folded (I-14's cousin, ADR-008), and *"the carrier is nameable; the substrate refuses it"*
(arch-E **OWED-E7**, arch-I **O5/X5**). **Three ratified items point three ways** — D-009/D-010
names soil and vegetation as intended raster layers, D-022 anti-scopes erosion, ADR-008 makes the
rasters immutable — and **nothing reconciles them** (arch-I **X5**). **DIRECTOR DECISION
REQUIRED** — arch-I **Q4, Q5**.

## II.6 — TECHNOLOGY CHANGES CAUSAL RELATIONSHIPS, IT DOES NOT ADD BONUSES

> **PROPOSED (residue only).** A technology must change **which mechanism runs**, or a coefficient
> **inside** a named resolution equation, and must be expressible as an edit at an identified
> equation site. It may not attach a standing multiplier to an owner.

**WHAT IS ALREADY RATIFIED.** This is **law 2** (I-2) applied to technology, and law 2 is
ratified. The residue is the **site-identification** clause: *"name the equation and the term."*

**MEASURED, and it is why the residue is cheap to adopt: the template already ships.** The one
shipped technology-like term is **tools**: tools in the store raise `equipRatio`, which raises
`toolFactor` on the **labour side** of `harvest = min(land, labour × toolFactor)`, and use wears
them out through a **Ledger sink** (`ProductionSystem.cs:227-233`; arch-E **E2**, arch-JKL §2.4).
**Every property the invariant wants is present**: a named good as the carrier, a named coefficient
inside a named equation, a conserved stock that depletes, and a brake that is the depletion itself.
arch-JKL §2.2 does the same thing for storage by enumerating the **five store terms T1…T5** as the
only places a preservation technology could enter.

**HOW A REVIEWER WOULD CHECK IT.** Ask the packet to fill one row: **`good/holder → published
variable → the equation it enters → the term it is → the sink that bounds it`.** A packet that
cannot fill the last two columns is proposing a bonus. **This is checkable at spec time**, which is
where S8 §4.1's dimensional declaration and coupling map already sit (I-40).

**WHAT IT WOULD COST.** It means a technology cannot be added without opening the equation it
affects, which is more work per technology and **more foundations-audit surface** (I-40, and S8
§4.1's scope is *dependency, not perturbation*). **It also has a measured bite that is not
theoretical**: arch-JKL §5.3 records that a physically honest preservation arc *"makes the food
economy worse first"*, and that the world is storage-limited on 4,800/4,800 turns with ~55% of
every harvest destroyed — so any T1/T2 edit **moves the demographic outcome directly and meets the
quarantined Malthus corridors** (**X-01** there). Under I-30 that is a finding about the mechanism,
never a reason to move a band.

## II.7 Summary of Section II's status

| # | proposed invariant | ratified basis | residue is | biggest obstacle to adopting it |
|---|---|---|---|---|
| II.1 | no universal capability abstraction | **partial** — the rejection is ratified; its *property* is narrower than its *name* | the **shape**, not only the ownership | it may collide with D-042 §8.3 **inside one record** |
| II.2 | no research-points shortcut | **partial, and pointing the other way** — accumulation is ratified YES | gating on an accumulator | mechanically checkable, but two named escapes exist (**G-03**, arch-D **Q3**) |
| II.3 | knowledge ≠ technology ≠ capability | **the distinction IS ratified** (§8.2, §9.1) | the **storage** half | *technology* has no definition (**G-14**) |
| II.4 | discovery ≠ adoption | **none located for the separation as a rule** | the whole thing | whether research **completes** is unruled (**C-10/G-02**) |
| II.5 | environmental effect ≠ arbitrary modifier | **partial** — law 2 + the carrier test | the medium-and-sink clause | ADR-008 refuses the substrate (**X5**) |
| II.6 | technology changes causal relationships | **law 2** | the site-identification clause | audit surface; and the storage case meets a **quarantined corridor** |

---

## §III WHAT THIS DOCUMENT DID NOT DO

No invariant was ratified, adopted, enforced or written into any gate. No conflict was reconciled
and no side was taken — including on the three places where a Section II proposal sits against
ratified material pointing the other way (II.1 point 3, II.2, II.4). No status was upgraded: every
Section I row carries its source's own status, and every Section II row is PROPOSED. No CR was
opened. No production code, schema, data file, golden, corridor, quarantine, test or gate script
was touched. No existing document was edited. This lane ran no build, no test and no measurement
beyond reading the tree at `26d12d3`; every MEASURED claim is a claim about what a file says.
