# ADR-029 — THE RESEARCH ENGINE: ONE CLP POOL, TECHNOLOGY AND CIVICS (schema v26, OrderKind 6, SystemId 24)

**Status:** PROPOSED. It is implemented on branch `research-progression-foundation`. Acceptance, and the golden
repin it carries (§15), are the Director's ruling on the pull request. This ADR records engineering; it rules
nothing. **The authority is D-044** (`docs/d044-research-progression-rulings.md`), the Director rulings of
2026-09-30 recorded verbatim. Where D-044 is silent, §13 lists every choice the implementer made. Each is labelled,
and each can be overridden by a ruling.

**Amends:** no ratified record. It **extends** the closed D-020 grammar (§11), which is why it is an ADR. It adds a
canonical schema version, an order kind and a system id, which are contract surfaces (CLAUDE.md "touched a
contract? write an ADR").

**Relates to:** D-044 (authority) · D-042 §6.2, §6.5 (directives, one order pathway) · ADR-028 §3–§4 (availability
is not construction) · ADR-019 Part 10 ("nothing spawns") · D-021 (paired feedback) · D-020 (predicate DSL) ·
ADR-005 (canonical schema) · ADR-001 (unmanaged rows, ids not strings) ·
`docs/design/civilization-progression-architecture.md` (reconciled with D-044 in the same branch) ·
`docs/research-corpus-audit.md` (generated per-node audit).

---

## §1 — WHAT THIS BUILDS, AND THE PRICE (S8 §2)

The Director's 2026-09-30 instruction orders the research foundation built now, ahead of a ruling on CR-005's
milestone placement (D-044 Part D, T1). S8 §2 prices an override of the frozen ladder as "a written ADR stating what
breaks, which tests and docs change, and the schedule price". This section is that statement. It does not rule
CR-005.

- **What breaks:**
  - Saves from v25 no longer load (D-008: no migration).
  - Every world golden moves (§15). The toy golden moves by layout alone. The founded, driven and FirstReign
    goldens move by the research tables alone; the attribution controls measure this.
  - `m5-full-build`, if it lands after this branch, renumbers its schema to v27. Its OrderKind 5 and SystemId 22
    are left free here (§3, §4).
- **Tests and documents that change:** listed in §15 and §16.
- **Schedule price:** none on the shipped physics. Research feeds nothing back into population, food, trade,
  migration or any other system. The calibration battery therefore cannot move, and §15's controls measure that.

## §2 — CONTENT CONTRACT: `Sim.Data/content/research.json`

### §2.1 The file and its generator

**The one authoritative graph (D-044 R16).** The shipped file is `Sim.Data/content/research.json`, schema
`civ-sim/research@1`, an embedded resource opened by `DataFiles.OpenResearch()`. It is **GENERATED** by
`scripts/migrate-research-corpus.py` from the Director's corpus `tech-graph-v0.6.json`, which stays at the repository
root, unchanged and read-only.

- The generator is deterministic: two runs are byte-identical. `--check` fails if either output is stale, and
  the CI `build-and-test` job runs it, so a hand edit to `research.json` or the audit, or a generator change
  without a regeneration, fails CI.
- The file records the corpus SHA-256. A test recomputes it (`Canonical_IsInSyncWithTheCorpusFileItWasMigratedFrom`),
  which catches a changed corpus. That test alone does not compare the migrated content with the corpus; the CI
  `--check` step does. (Corrected after review: the first version of this ADR claimed the SHA test alone prevented
  drift, and CI did not yet run `--check`.)
- The generator also writes `docs/research-corpus-audit.md`. It is the full audit D-044 R23 asks for: every node's
  key, tree and subtree, domain, age, depth, cost, prerequisites, Eureka mapping, family and generation, unlock count
  and historical date.

### §2.2 The node model (D-044 R6, R12)

**Identity**
- `key` — the STABLE integer id carried by rows and orders. Technologies take 1..424 in corpus order; Civics take
  1001..1006. Keys are never renumbered; new nodes append new keys.
- `id` — the corpus string id, preserved.
- `name`, `desc`.
- `age` — A1..A9, metadata only (R13).
- `frontier`.
- `emerged` — the corpus's historical date.

**Research**
- `cost` — BaseCost in CLP (§2.4).
- `prereq` — an AND / OR / nested expression over node ids (R8). NOT is refused, so availability is monotone in
  completed knowledge.
- `eurekas[]` — the corpus prose, a status, and a condition when evaluable (§7).
- `domain`, `secondaryDomains` — the research-domain classification.
- `branch` — technologies only: null for the Main Technology Tree, or one of the five subtree ids (§8).

**Unlocks**
- `capabilities`, `techniques`, `applications` — corpus text.
- `buildings`, `infrastructure`, `institutions`, `units`, `activities`, `projects` — registry entity ids. Each list
  is the exact reverse index of the entities' knowledge requirements, which are authoritative
  (`registry.meta.authority`). The `projects` list is new: it fills the gap the v0.6 audit recorded.

**Progression**
- `family`, `generation` — display lineage. They are **not** a dependency rule, because 13 corpus nodes break
  "gen N requires gen N−1" (D-044 R7).

**Effects**
- `effects.immediate` — must be empty. No immediate-effect kind is ratified, and a free-floating effect would be a
  law-2 modifier.
- Newly enabled downstream nodes are the derived `Dependents`. The corpus's `effects.enables` was verified equal to
  that reverse index and is not duplicated.

**Repeatable**
- `repeatable` — the corpus's frontier-line descriptor, kept as data. It is not implemented (§13 R-12).

### §2.3 Civics (D-044 R12)

The corpus contains no Civics nodes. The six candidates that architecture §5.7 proposed are reclassified from
registry institutions: `law_code`, `legal_code_roman`, `census`, `coined_wage`, `patent` and `joint_stock`.

- Each civic takes its institution's short id and its **knowledge** half.
- The civic's prerequisite is the institution's requirement expression. Any reference to *another* institution is
  replaced by that institution's own knowledge requirement, because an institution is established, not researched.
- The registry institution stays an institution, and its requirement becomes the civic. For example,
  `inst.law_code` requires `law_code`.
- Civics may unlock only institutions (architecture §5.4: a civic makes institutional forms eligible) and never
  carry a subtree.
- The Civics tree branches: `law_code → legal_code_roman → {patent, joint_stock}`. It crosses trees in both
  directions: civics require technologies, and the research stage requires a civic (§8).
- No other Civics content was invented (R23: do not fabricate).

### §2.4 Costs — TUNE, chosen, not derived

The corpus has no research cost. The generator writes an explicit per-node `cost`:

```
BaseCost = round_to_10(1000 × 1.12^depth)
```

`depth` is the node's longest prerequisite path, taking the union over OR alternatives. Depth rises strictly along
every edge, so a dependent always costs more than each of its prerequisites. Every value can be edited per node in
the data.

**Measured pacing on the canonical founded world (seed 42, one polity, the `--auto cheapest` measurement driver):**

| turn | sim-year | nodes complete |
|---|---|---|
| 99 | 990 | 50 |
| 196 | 1960 | 100 |
| 300 | 2750 (≈ 1250 BCE) | 139 of the 176 Main-tree nodes, plus 1 civic |
| 398 | 3240 | the research stage is reached; all five subtrees open |
| 1000 | 5500 | 295: Main 176/176, Military 23, Medicine 16, Engineering 41, Natural Science 24, Agriculture 9, Civics 6/6 |

(Re-measured after the review fixes, which made 24 fewer Eureka strings evaluable; the first measurement, at
`ced5009`, was 50 by turn 96, 100 by 191, 141 + 1 at turn 300, the stage at 388.)

The research stage was not yet reached at turn 300. These are readings, not targets. The pacing depends on the
provisional CLP function (§6).

### §2.5 Validators (D-044 R24)

The loader is `ResearchContentLoader`. It fails fast and loud: the message names the file path, the node and the
rule. Each rule has a rejection test in `ResearchContentTests`. JSON is read with `RespectNullableAnnotations`, so an
explicit `null` in a required non-nullable value is a validation failure naming its path, never a null in the model
or a `NullReferenceException` (`Rejects_ExplicitNullsInRequiredValues_…`).

| Validator | What it rejects |
|---|---|
| Duplicate ids and keys | An id repeated across *both* trees; keys that are not strictly ascending |
| Tree ids | Anything other than exactly [technology, civics] |
| Subtree ids | Anything other than the five ruled ids in order (no `industry_energy`); an unknown `branch` on a node |
| Subtree assignment | A civic with a `branch` key; a technology without one; a Main-tree technology with a subtree prerequisite, even through an OR alternative |
| Missing prerequisites | An unknown atom |
| Cyclic prerequisites | A self-reference; any cycle, reported as "prerequisite cycle: a requires d requires a" |
| NOT | NOT anywhere in a prerequisite |
| Eureka references | An unknown name or quantity; a status/condition mismatch; a **dead Eureka** — a condition that cannot hold while its node is still researchable. The check is by necessity, not by descendants: it computes everything completable *without* the node (the reachability fixpoint with the node held incomplete, the stage opening only if it can without the node) and rejects a NOT-free condition that is false there even with every comparison taken as true (`Predicate.CanHold`). An OR alternative that needs the node does not kill a condition whose other alternative can hold; a condition naming subtree knowledge on a node the stage itself needs is dead. (Corrected after review: the first check rejected every condition naming a descendant, a false positive for OR-dependents, and missed the stage-gated case.) |
| Costs | Not finite, or not greater than 0 |
| Unlock references | An unknown entity; a kind mismatch; a duplicate ("listed twice"); disagreement with the reverse index; a civic unlocking anything but an institution |
| Orphans | A node with no dependents, no entity unlocks and no capability |
| Unreachable nodes | A Main-tree node reachable only through subtree knowledge (for example through a Civic: an invalid cross-tree dependency); a research stage that knowledge available before the stage can never satisfy (a deadlock); a final every-node reachability fixpoint |
| Entity requirements | Undeclared or stale `unresolved` references; institution cycles; a bad id prefix |
| Other | A stale `depth`; an age outside A1..A9; non-empty immediate effects; tuning out of range (the CLP exponent must be in (0, 1), because linear population is forbidden); a missing required key; a `stock_<good>` quantity with no goods registry attached |

## §3 — STATE CONTRACT: schema v26

Five tables are appended after Disasters, as blocks 41–45 of `CanonicalSchema.Write`. They are **the minimum
authoritative research state** (D-044 R18). Availability, subtree opening, EffectiveCost and CLP throughput are
derived and never stored.

| Table | Row | Width | Owner | Meaning |
|---|---|---|---|---|
| ResearchTargets | (Polity, Node) | 8 | ResearchSystem | The one active target; at most one row per polity. Absence means no target |
| ResearchProgress | (Polity, Node, double Progress) | 16 | ResearchSystem | CLP invested in a node not yet complete. Created lazily, removed on completion |
| ResearchCompleted | (Polity, Node) | 8 | ResearchSystem | The knowledge base. Row presence is the fact, append-only, in completion order |
| ResearchEurekas | (Polity, Node, int Eureka) | 12 | ResearchSystem | Fired Eurekas. Row presence is the fact; a Eureka fires once |
| ResearchCostModifiers | (Polity, int UniversityType, double Factor) | 16 | **none yet** | §9's input contract. No system writes it (the ClaimRow / RecognitionRow precedent) |

- `Node` is a `ResearchNodeId` holding the content key.
- None of these rows is people, money or goods, so the Ledger is untouched (law 1). Progress is a `double` in CLP
  (law 7; the `PathProgressRow.Banked` precedent).
- **Collision note** (in `CanonicalSchema.cs`): v25 is T4.21-1's Disasters table, so the unmerged `m5-full-build`
  "v25" becomes **v27**.
- **SystemId 24.** 17 and 22 are held by unmerged branches; 19 is left alone.

## §4 — ORDER CONTRACT: OrderKind 6, `SetResearchTarget`

- **Payload:** TargetId is a node key, or −1 to clear. Amount is reserved and must be 0. ActorId is the issuing
  Empire.
- **Wire format:** unchanged; IoVersion stays 1. **Kind 5 is left unused**, because `m5-full-build` holds it.
- **At LOAD:** a key below 1 other than −1, or a non-zero Amount (NaN included), is rejected.
- **At CONSUMPTION (ResearchSystem):** the actor must be a registered polity and the node must be AVAILABLE to it on
  PREV. Otherwise the order changes nothing (the ConstructionSystem precedent).
- **Several orders for one polity in one turn:** processed in log order; the last valid one wins.
- **Delivery:** an order stamped t retargets the step t → t+1, and that step's CLP goes to the new target. This is
  pinned turn-exactly by `Selection_AnOrderStampedT_RetargetsTheStepFromT_AndThatStepsClpGoesToIt` and
  `Research_TurnExactDelivery_OnTheFoundedWorld`.
- **The UI:** it emits through `ResearchOrderFactory` and `UiSession.EmitResearchOrder`, and refuses to log an
  order the simulation would ignore.

## §5 — STEP SEMANTICS (`ResearchSystem`, last in `pipeline.json`)

For each polity in `Prev.Polities` table order (a duplicated roster row counts once):

1. **Directive.** Apply this polity's SetResearchTarget orders (§4).
2. **Eurekas.** For each AVAILABLE node in index order, each unfired evaluable Eureka whose condition holds on PREV
   fires once. It credits its own node with `eurekaCreditFraction × EffectiveCost`, capped at the remaining cost,
   whatever the target is (R10).
3. **Throughput.** `ClpPerYear × dtYears` goes to the active target, capped at its remaining cost. CLP that reaches
   no node is not stored (R20-D).
4. **Completion.** Each available node whose progress has reached EffectiveCost completes, in index order. Its
   completed row is appended, its progress row removed, and it is no longer the target. Reaching the cost sets
   progress to *exactly* the cost, so the comparison cannot miss by an ulp.
5. **Persist the directive.**

Availability is always read from PREV, so the dependents of a node completed in step t open in step t+1. The system
draws no RNG, writes no ledger flow and reads no Age (law 4). Removals preserve the relative order of the rows that
remain. With no research content attached, the system is inert.

## §6 — CLP (D-044 R2) — PROVISIONAL, CHOSEN, NOT DERIVED

```
CLP per sim-year = clpCoefficient × adults ^ clpAdultExponent        (1.0 and 0.5 shipped)
```

- **Adults** are cohorts 3..11, every class, in the settlements the polity controls. Each settlement is credited to
  its lowest-id controller.
- **The exponent** is validated in (0, 1), because architecture §8.1.2 forbids linear population.
- **Inputs not implemented:** population is the only §8.1 input that exists in simulation state. Education,
  literacy, institutions, health, specialization and connectivity are not guessed at.
- **Dimensions:** CLP is research effort. Costs and progress are in CLP. The rate is CLP per sim-year, integrated
  over `dtYears` (law 3).
- **Loops:** none is closed. Research feeds nothing back, so CLP cannot raise population. D-021 applies when a
  consumer of research closes a loop.

## §7 — EUREKA (D-044 R10)

The condition language is the ONE D-020 predicate language (§11). A condition reads:
- **node atoms** — the polity's completed knowledge;
- **`stock_<good>` quantities** — a positive stock of a shipped good in a settlement;
- **registered variables.**

A condition with any settlement-scoped operand holds if it holds in AT LEAST ONE settlement the polity controls. A
condition over node atoms only is evaluated once, for the polity. Eurekas are evaluated only for AVAILABLE nodes (§13
R-5). A fired Eureka is stored and never fires again.

**Mapping the corpus's 801 prose strings.** Only *faithful* mappings are made machine-evaluable. The generator states
each rule:

1. **Exact technology references** — the id or the name — plus four reviewed aliases: fire, sustained fire, adhesive,
   and alphabetic script.
2. **Exact good names**, including synonyms.
3. **A hand-reviewed list.** A string is mapped when it names a shipped good, **or an object shaped directly from
   one** (a stone mould, a timber frame, a clay tablet, a bronze ram). The mapped condition reads "a controlled
   settlement holds a positive stock of that good": the material is at hand. There are no object stocks, so it cannot
   check that the object itself exists; this is the stated approximation.

**Deliberately NOT mapped:**
- a **different substance** made from a good (wood ash, lime, molten or smelted metal, copper wire);
- a form that needs a **separate graph technique** (heat-treated stone → `heat_treatment_stone`, ground-stone axes →
  `ground_stone_early`);
- a negation ("not potter's clay");
- an unstated quantity ("in quantity", "at scale");
- inseparable lists (stone, papyrus, ink);
- **exchange** ("long-distance exchange reaching a tin source") — no trade-network state exists;
- **institution presence** — 79 strings; no institutions system exists;
- **contact with a civilization holding the node** — 61 strings; no contact state exists (the D-035-C carrier test).

**Implied by prerequisites — declared, not evaluated.** A knowledge circumstance that the node's own prerequisites
already guarantee (plus, for a subtree node, what the stage guarantees) would hold at every moment the node is
available: a flat 25 % cost cut, not a circumstance. The generator computes each node's must-complete set (an atom
contributes itself and its must-set; AND = union, OR = intersection) and gives such a string the status
`implied-by-prerequisites` with no condition. 21 strings are affected; every use of the four aliases is among them.
They are listed as corpus problem 9 in the audit. `Canonical_NoEvaluableKnowledgeEureka_IsGuaranteedByItsOwnNodesPrerequisites`
exhibits, for each remaining knowledge condition, a reachable state where the node is available and the condition
is false.

**Result:** 93 of the 801 strings are evaluable, on 83 technology nodes: 91 good-stock conditions and 2 knowledge
conditions (`copper_smelting` ← charcoal, `windmill_post` ← gearing). Status counts: evaluable 93, no-state-carrier
547, institution-state-absent 79, contact-state-absent 61, implied-by-prerequisites 21. Every other string is kept
verbatim with its status, and the Glass Box shows why it cannot fire. (Corrected after review: the first version
made 117 strings evaluable, including three that broke its own rule — wood ash, lime or wood ash, and the tin
exchange, which duplicated `tin_bronze`'s tin-ore Eureka so that one stock fact gave 50 % credit — and 21 that were
implied by prerequisites.)

## §8 — THE RESEARCH STAGE AND THE FIVE SUBTREES (D-044 R3, R4, R15)

**The stage** (`researchStage.requires`) is derived mechanically from the registry. It is the knowledge-level reading
of "the civilization can establish a university":

- building.university's technology requirement;
- AND building.library's requirement (the university building requires a library);
- AND inst.university's requirement, with its institution references expanded into their knowledge requirements.

That yields:

```
(medicine_hippocratic AND (geometry_axiomatic OR algebra)) AND (cuneiform OR hieroglyphic OR chinese_script
OR papyrus OR paper) AND (((cuneiform AND stamp_seal) AND cuneiform) AND legal_code_roman)
```

**It needs a Civic**, `legal_code_roman`: Civics knowledge helps open Tree 1's subtrees. **All five subtrees open
together** when the stage predicate holds, and the stage is never stored. The **institutional half** — a university
actually established — cannot be evaluated, because no institutions system exists. It is the seam: the stage is data,
and a later packet can add an institution-present atom to it without changing engine code.

**Classification, rule R1 (§13 R-8)**, which produces counts of trunk 176 · Military 37 · Medicine 22 · Engineering 130 ·
Natural Science 44 · Agriculture 15:

1. **Main Technology Tree** — the stage trigger's technology prerequisite closure, plus every node aged A1–A5. The
   closure is 32 nodes, union over OR: 30 technologies, all forced into the trunk, and 2 civics (`law_code`,
   `legal_code_roman`), which stay in Tree 2. Age is used here as content metadata, never as a runtime gate.
2. **Otherwise, the primary domain decides:**

| Primary domain | Subtree |
|---|---|
| military, naval | Military (architecture §8.4.1: "military and naval knowledge") |
| medicine | Medicine |
| science, environment | Natural Science |
| agriculture, food | Agriculture |
| engineering, materials, construction, transport, communication, infrastructure, industry, energy | Engineering. R20-F removes the Industry & Energy subtree but does not place its knowledge; folding `industry` and `energy` in here is implementer resolution §13 R-8 |

**Properties of the result:**
- No Main-tree node requires a subtree node.
- 33 subtree entry nodes depend only on the trunk.

## §9 — THE SPECIALIZED-UNIVERSITY COST SEAM (D-044 R5)

```
EffectiveCost = BaseCost × Π Factor
```

- The product runs over the polity's `ResearchCostModifier` rows whose university type serves the node's subtree,
  multiplied in table order.
- **The five types** are data, one per subtree: Military, Medical, Engineering, Natural Science and Agricultural
  University.
- Main-tree and Civics nodes have no relevant university.
- **Factor must be in (0, 1]**, because a university *reduces* cost. An unknown type or an out-of-range factor
  breaks the input contract and throws.

**The table is the narrowest contract.** Its future writer, an institutions system, folds maturity, local viability
and diminishing returns into Factor (ADR-028 DD-13, DD-14). No formula is invented (R5: the formula is not
ratified). There is no `CapabilitySystem`.

**Status:** the seam is implemented and tested, including several types at once and same-type rows that multiply.
**No production writer exists**, so in the shipped world EffectiveCost = BaseCost. **D-021:** the first writer
closes the research → university loop and owes its brake.

## §10 — COMPLETION, UNLOCKS, KNOWLEDGE ELIGIBILITY (D-044 R11, R14)

**What completion does:**
- Completion is the knowledge. `ResearchQuery.UnlockedCapabilities` lists the declared capabilities of completed
  nodes.
- `ResearchQuery.IsKnowledgeEligible` answers the **knowledge** half of an entity's LOCKED → AVAILABLE for every
  registry entity. It evaluates the entity's knowledge requirement over completed nodes, and an institution named
  in a requirement counts when its own knowledge requirement is met.
- A null requirement is always met (ADR-028 §3).
- A declared-unresolved corpus reference reads false. The one such reference is `inst.newspaper → postal_imperial`,
  which also blocks mass schooling and broadcast media.

**What completion does not do:**
- It builds nothing.
- **No system consumes eligibility yet.** Sites, buildings, resources, institutional presence and construction
  belong to the owning systems (ADR-028 §4). D-040 B3's concrete case therefore still holds: no node opens sea
  travel or any network edge type.

## §11 — THE D-020 DIALECT (D-044 R8, R10) — AN EXTENSION OF A CLOSED DECISION

`Predicate.Parse(string, PredicateSymbols)` enters the research dialect of the same parser, tokenizer and evaluator.
It adds exactly three things:

1. **Keyword aliases:** the upper-case `AND`, `OR` and `NOT` for `&&`, `||` and `!`.
2. **Boolean atoms:** a bare name not followed by a comparison.
3. **Caller-bound quantities:** a name that is not a registered variable.

**Resolution order** is fixed: a registered variable, then a quantity, then an atom.

**Introspection** comes with it: `AtomIds` and `QuantityIds` (distinct, in first-appearance order), `ReadsVariables`
and `UsesNot`, and `CanHold(atoms)` — whether a NOT-free expression can be true for some truth values of its
comparisons given the atoms (with a NOT it answers true, "undecided"). The loader uses them for cycle, reference and
dead-Eureka checks; the Glass Box uses them for per-prerequisite status.

**What is unchanged:**
- There are still **no functions and no arithmetic**.
- `Parse(string)` and `Evaluate(VariableReader)` behave exactly as before: the same results, failures and messages.
  Keywords and bare names still fail with the old messages, and `PredicateTests` plus a legacy pin in
  `PredicateResearchDialectTests` hold that. The *code* on that path is not byte-for-byte the old code — the shared
  parser gained the introspection bookkeeping. (Corrected after review: commit `72f3e18`'s class doc said
  "untouched" and D-044 T5 said "unchanged byte for byte"; both now say "behaves as before".)

**Status:** D-020 is closed. This extension is recorded here and may be ruled back.

## §12 — GLASS BOX (D-044 R17)

`Sim.Core/State/ResearchQuery.cs` holds pure static queries: completed nodes, the completed mask, stage and subtree
status, availability by tree or subtree, prerequisites with per-atom status, the active target, partial progress,
base and effective cost with a per-term breakdown, cost modifiers, adults and CLP per year, Eureka state (fired, and
holds-now), knowledge-eligible entities, unlocked capabilities, `CompletedBetween(prev, next)` (the observable
completion event) and `CheapestAvailable`.

**ResearchSystem computes with these same statics.** Every value is therefore RECOMPUTED by the simulation's own
function, never by a private re-implementation. `GlassBoxQueries_MutateNothing` proves hash-equality before and after.

- **CLI:** `sim research --seed S --turns N [--node ID] [--auto cheapest] [--emit-orders PATH]` prints the report.
  `--auto` is a measurement driver that issues real orders, so the log replays through `sim run --orders`.
- **UI:** the order pathway (§4).

**Not built:** a research panel, telemetry sections and chronicle events (§16).

## §13 — IMPLEMENTER RESOLUTIONS (the rulings are silent; each is overridable)

| # | Resolution | Why |
|---|---|---|
| R-1 | One active target per polity **spanning both trees** | R9 says "one active allocation target"; R12 says Civics uses the same pool; manual splits are banned |
| R-2 | CLP with no target, and CLP past a node's remaining cost at completion, is **lost** | R20-D (no general bank); R10's no-overflow principle, applied to throughput |
| R-3 | Completion **clears** the target; the player chooses again; there is no queue | R9; D-042 §12's "rigid queue" remains unbuilt |
| R-4 | CLP = coefficient × adults^exponent over controlled settlements (§6) | §8.1.2 forbids linear; population is the only input present |
| R-5 | Eurekas are evaluated only for **available** nodes; credit = fraction × EffectiveCost; fraction 0.25 (TUNE) | Causal reading; 0.25 because corpus nodes carry up to four Eurekas |
| R-6 | Only faithful Eureka mappings are machine conditions: a shipped good or an object shaped from one; a circumstance implied by the node's own prerequisites is declared, not evaluated (§7) | R10 "where applicable"; R23 "do not fabricate"; a condition that always holds is a modifier, not a circumstance (law 2) |
| R-7 | The research stage is the knowledge-level university predicate (§8) | R4 asks for capability plus institutional state; the latter does not exist |
| R-8 | Rule R1: stage closure + ages A1–A5 go to the trunk; the rest is split by primary domain, naval → Military, industry and energy → Engineering | R15 (keep the corpus); R20-F removes the Industry & Energy subtree without placing its knowledge, and the corpus domains `industry` and `energy` need a ruled subtree; Engineering's ruled coverage (materials, mechanical engineering) is the nearest. Architecture §8.4.1 now cites this resolution, not the reverse |
| R-9 | Civics = the six architecture §5.7 candidates, knowledge half only | R12, R23 |
| R-10 | BaseCost = round10(1000 × 1.12^depth) | The corpus has no cost; monotone along edges |
| R-11 | Age F → A9 + `frontier` | Architecture §12.7, §17.5 |
| R-12 | The 10 repeatable frontier nodes complete once; the descriptor is kept as data | R11 idempotence; no consumer exists |
| R-13 | University seam = the ResearchCostModifier input table, Factor in (0, 1], multiplicative in table order | R5; law 6 (state-mediated, no sibling call) |
| R-14 | Stable integer keys in data (tech 1..424, civics 1001..) | Saves and orders must not misbind when content grows |
| R-15 | Registry entities are carried for unlock validation and knowledge eligibility; dangling references are declared, not repaired | `registry.meta.authority`; R24 |
| R-16 | **No starting knowledge** at founding | Architecture §12.6 quotes "the game begins in the Stone Age"; CR-006 is open |
| R-17 | Prerequisites and the stage read PREV; dependents open the step after completion | The kernel's one-turn-lag convention |
| R-18 | Completion order within a step: polity table order, then node index order | Deterministic, explicit |
| R-19 | `effects.immediate` must be empty | No effect kind is ratified; law 2 |
| R-20 | Definitions: *orphan* = no dependents, no unlocks, no capability; *unreachable* = §2.5; *cross-tree invalid* = Main-tree knowledge that only subtree knowledge can reach | R24 names the validators without defining them |
| R-21 | An invalid target order is ignored at consumption; the UI refuses to log it | The ConstructionSystem precedent |
| R-22 | Research runs last in the pipeline, reads PREV only, draws no RNG | Its tables are read by no other system; no stream rows are added |

## §14 — UNRESOLVED (Director decision required)

D-044 Part D lists T1–T7: CR-005 placement, the Spine "no tree" reading, D-040 B3, the D-021 brake, the D-020
extension, repeatables and starting holdings. In addition:
1. The CLP function beyond population (architecture §19.5).
2. The institutional half of the research stage, once an institutions system exists (CR-010 unwritten).
3. The Eureka carriers for institution presence and contact.
4. Civics content beyond the six candidates (architecture §19.9).
5. Whether the Trees UI on `claude/civdemo-work-b1z2y4` is re-pointed at this graph (architecture §19.13).

## §15 — GOLDENS AND PINS (measured, not asserted)

| Pin | Old (main 93270cd) | New (measured on this branch) | Cause | Control (returns OLD byte for byte) |
|---|---|---|---|---|
| Toy, turn 200 | `b6df7edd362e15de908526c6343f50f920f3a344b7dac703aaad7671c41adaa1` | `1ba352429d018fa6ce3998f3115d9f8fa58cff59eebeeb3a1a092f99b8cdc4c4` | Five empty v26 count prefixes (the toy pipeline runs no research) | `GoldenHashSeed42Turn200_MovedForTheV26ResearchTrailerAlone` |
| Founded, turn 300 (+ `ci.yml`) | `db7c7a0907ad43353b1a44f1a957a407c0ce89ecbb2bf105b170b4b316cc82d9` | `740799216ebd1c30f2ababc0a729237bc78a6e720d00278e1cb913fb132fe5a6` | Five prefixes + ResearchSystem's own rows. With no order there is no target and no CLP lands, but three stone-mapped Eurekas fire on available roots: knapping_oldowan, knapping_levallois and grinding_stone, each at 250 / 1000 at turn 300 (read with `sim research`) | `FoundedGoldenSeed42Turn300_MovedForTheResearchLayerAlone` (asserts the strip is not vacuous) |
| Driven, turn 300 | `98ee3a7acdcad9a9cb93870ec3d66d80c4559f8c430ce9d5329b251f010f5cdb` | `e0f16b1cb418db82b264897fe4fdd535cd89b6ed253260e6aa8c571b61f546ba` | As founded | `DrivenGoldenSeed42Turn300_MovedForTheResearchLayerAlone` |
| FirstReign, turn 40 | `dacf3c34824a866726861be64480da4b7fe913a8a80bd4a72f51bc282ec1fe3e` | `37fd4ba7d3d53bd01125bb57d2daa1395b67c679e1a0a86c6e3a1af1046dda58` | As founded; the shape asserts are unchanged | `FirstReignTurn40_MovedForTheResearchLayerAlone` |

- **How the values were derived:** the toy and founded values are **derived twice**: the in-test harness, and the
  built CLI (`sim run --seed 42 --turns 200`; `sim run --founded --seed 42 --turns 300 --hash-log`, two processes
  with byte-identical logs). The driven and FirstReign values were read from the harness's own worlds.
- **Why the repin is a Director matter:** the repin moves world goldens. That is a Director ruling (gov-4 §5; the
  M4-D precedent), so it lands in its own commit.
- **Other pin changes:** every older control in `IntegratedPinAttributionTests` strips the research layer and drops
  five more prefixes, and its constants are unmoved. Version pins move from 25 to 26. The forensic resource count
  moves from 9 to 10. The pipeline pins move from 17 to 18, with `research` last.
- **Behavioural statement:** the strips returning main's pins byte for byte is the measurement that no population,
  food, trade, migration or other state moved.

## §16 — WHAT WAS NOT BUILT (and why)

| Not built | Why |
|---|---|
| A research UI panel | The Trees UI lives on an unmerged branch; the order pathway and the queries are the seam |
| Telemetry sections | Would move `telemetry/v3` → v4; a later observability packet |
| Chronicle "technology completed" event | Needs a civilization-level event shape; `CompletedBetween` is the observable |
| Institutions, universities, university construction | R21 |
| Literacy, education | Not invented, R20-I |
| Starting holdings, repeatable levels | §14 |
| Wiring eligibility into ConstructionSystem | Both shipped projects have null requirements, so it would be a no-op; ADR-028 §4 leaves it to the packet that needs it |
| AI target selection | No AI decision code exists; D-042 symmetry means AI would use this same order |
