# D-044 — RESEARCH AND PROGRESSION: ONE COGNITIVE LOAD POINT POOL, TWO TREES, FIVE SUBTREES

**Director design ruling.** Decision record — exempt document class under S8 §4. It records, verbatim, the
Director rulings delivered on **2026-09-30** in the instruction that opened the research-foundation
implementation. It follows the ADR-028 precedent: rulings decided outside the tree are written into a file so
that every later packet cites a file rather than a conversation.

**Provenance.** The rulings reached the tree only as session text. Before this record, `git grep` for
"Cognitive Load", `CLP` and "EffectiveCost" found **0** files on `main` (`93270cd`),
`origin/claude/civdemo-work-b1z2y4` and `origin/m5-full-build`. Part A is copied byte for byte from that
text by a script, not retyped. The instruction's own process sections — the git workflow and the final-report
list — are omitted, because they instruct the implementer and rule nothing.

**Designs no mechanism by itself.** The mechanism that implements these rulings, and every choice the
implementer made where the rulings are silent, is recorded in
`docs/adr/adr-029-research-engine.md`. That ADR does not rule; it records engineering choices that any
Director ruling may override.

**Relationship to ratified records.** This record **supersedes** named clauses of D-042 (Part C). Each
superseded clause is left **unedited** in D-042, which gains an append-only pointer note beside it (the precedent is
the D-039 note at `docs/d011-battle-layer-addendum.md:77`). Nothing in D-037, CONV-1, D-043, ADR-019 or ADR-028 is amended. DD-13 and DD-14 are not
renumbered.

**Followed by D-045 (2026-10-01).** `docs/d045-research-calibration-rulings.md` records the next rulings: zero
starting knowledge with baseline capabilities, the research-capacity anchors, calibrated costs, the final-Age pacing
target, 40 % partial Eurekas and the Eureka audit. This record is not edited by them; D-045 Part C names the
provisional choices they replace.

**Clause labels.** Part A's numbered sections are cited as **D-044 R1 … D-044 R25**. For example, "D-044 R9"
means section 9, RESEARCH PROGRESS. The "do not ask again" list at the end of Part A is cited as
**D-044 SETTLED**.

---

## PART A — THE RULINGS (verbatim)

```text
============================================================
AUTHORITATIVE LATEST DIRECTOR RULINGS
============================================================

These decisions supersede earlier ambiguous/proposed wording in the progression architecture.

------------------------------------------------------------
1. TECHNOLOGY = KNOWLEDGE
------------------------------------------------------------

Technology and knowledge are the same player-facing progression concept.

A Technology node represents a discrete body of civilization knowledge.

Researching/completing a Technology node means:

Technology completed
→ civilization now possesses that knowledge
→ declared capabilities/unlocks become available.

There is NOT:

Technology
+
separate Knowledge resource
+
separate Knowledge Tree.

Do not create those.

The civilization Knowledge Base is simply the accumulated set of completed Technology/Civics knowledge nodes.

Individual people do not own persistent civilization knowledge.

If a scientist dies, knowledge already acquired by the civilization remains.

------------------------------------------------------------
2. RESEARCH RESOURCE
------------------------------------------------------------

There is ONE shared Cognitive Load Point pool.

Everything that is researchable consumes the same pool:

Technology
Technology subtrees
Civics

Do NOT create:

Science Points
Culture Points
Military Research Points
Medical Research Points
Engineering Research Points
Knowledge Points as a second resource

The term "Cognitive Load Points" is the authoritative resource name.

The pool represents research/progression throughput.

Do not create a second accumulated research currency.

------------------------------------------------------------
3. TREE STRUCTURE
------------------------------------------------------------

There are exactly TWO top-level research trees:

TREE 1 — TECHNOLOGY

TREE 2 — CIVICS

Tree 1 contains exactly FIVE specialized subtrees:

TREE 1
├── Main Technology Tree
├── 1.1 Military
├── 1.2 Medicine
├── 1.3 Engineering
├── 1.4 Natural Science
└── 1.5 Agriculture

TREE 2
└── Civics

That is the entire player-facing tree hierarchy.

Do NOT introduce:
1.3.1
1.3.2
Tree 3
Tree 4
Industry & Energy tree
separate Naval tree
separate Science tree

Internal prerequisite branches inside a tree are allowed, but they are not additional numbered trees.

------------------------------------------------------------
4. WHEN TREE 1 SPLITS
------------------------------------------------------------

The specialized branches become available when the civilization reaches the university/research institutional stage.

The trigger is NOT simply:

Age == X

The trigger is the relevant education/university/research capability and institutional state.

However, do NOT over-engineer progressive branch opening.

Once the university/research stage exists, the five specialized branches become available as research branches.

The player can then research military, medicine, engineering, natural science and agriculture technologies using the SAME Cognitive Load Point pool.

This is specifically intended to support specialized universities.

------------------------------------------------------------
5. SPECIALIZED UNIVERSITIES
------------------------------------------------------------

Specialized universities exist as institutions/specializations.

Examples:

Medical University
Engineering University
Military University
Natural Science University
Agricultural University

Their research effect is:

relevant specialized university
→ reduces effective research cost of relevant Technology nodes.

It does NOT directly create a separate research currency.

It does NOT simply add flat global Science.

It does NOT create a separate research queue.

Use:

BaseCost
→ relevant modifiers
→ EffectiveCost

The exact final numerical scaling formula is NOT ratified.

Therefore implement the modifier architecture and deterministic calculation seam, but do not invent a final balance formula.

The system must be capable of supporting diminishing returns, university maturity and local viability later.

If existing university/institution code provides relevant maturity/viability information, integrate with it.

If it does not exist yet, create the narrowest interface/data contract necessary.

Do NOT create a universal CapabilitySystem.

------------------------------------------------------------
6. TECHNOLOGY NODE MODEL
------------------------------------------------------------

Every Technology node should support:

Identity:
- Technology ID
- Name
- Description
- Historical period / Age relevance

Research:
- Cognitive Load Point cost
- prerequisites
- Eureka conditions
- research-domain classification

Unlocks:
- capabilities
- units
- buildings
- institutions
- techniques
- infrastructure
- applications

Progression:
- Technology family
- Generation

Effects:
- immediate civilization effects
- newly enabled downstream systems

Use stable IDs.

Do not encode the technology graph directly in C#.

Technology content must remain data-driven.

------------------------------------------------------------
7. TECHNOLOGY FAMILIES / GENERATIONS
------------------------------------------------------------

Technology families may have generations.

Example:

Antibiotics I
Antibiotics II
Antibiotics III
...
Antibiotics VI

But there is no universal requirement that every technology family have six generations.

Generations are meaningful advancements, not arbitrary repeated levels.

Do not automatically connect generations unless the actual prerequisite graph says they are prerequisites.

------------------------------------------------------------
8. PREREQUISITE GRAPH
------------------------------------------------------------

The graph is not a linear technology tree.

It must support:

AND
OR
nested AND/OR prerequisite expressions.

Example:

(A AND B)
OR
(C AND D)

Prerequisite edges represent causal requirements, not merely historical chronology.

Reject circular prerequisite graphs.

Dependent technologies are unavailable until their prerequisites are complete.

------------------------------------------------------------
9. RESEARCH PROGRESS
------------------------------------------------------------

The player can change the current research target.

Progress already invested in a node remains.

Example:

Technology A = 60 / 100

Switch to Technology B.

Technology A remains 60 / 100.

Return to A.

Technology A remains 60 / 100.

Do not erase partial progress.

There is one active allocation target at a time for the current implementation.

Multiple nodes can retain partial progress.

Do NOT invent manual 60/20/20 allocation between technologies.

The player selects where the current Cognitive Load Point throughput is directed.

------------------------------------------------------------
10. EUREKA
------------------------------------------------------------

Eureka conditions can accelerate a Technology independently of whether it is the currently selected research target.

Eureka progress is applied to that Technology.

Eureka credit cannot exceed the remaining cost.

No Eureka overflow to another Technology.

Eureka conditions should use the existing deterministic predicate architecture where applicable.

Do not invent a second condition language.

------------------------------------------------------------
11. TECHNOLOGY COMPLETION
------------------------------------------------------------

When research reaches the effective cost:

1. Mark the Technology completed.
2. Add it to the civilization's completed knowledge.
3. Unlock its declared capabilities.
4. Unlock its declared downstream eligibility.
5. Apply declared immediate effects.
6. Make newly eligible dependent technologies available.
7. Emit existing observable/event information if the architecture requires it.

Completion must be idempotent.

Do NOT create a separate delayed "knowledge realization" phase for the Technology itself.

Downstream physical realization is still handled by the relevant system:

Technology
→ capability/unlock
→ construction/recruitment/establishment/etc.

------------------------------------------------------------
12. CIVICS
------------------------------------------------------------

Civics is Tree 2.

It uses the SAME Cognitive Load Point pool.

Civics must support:

- node identity
- cost
- prerequisites
- partial progress
- completion
- unlocks
- effects
- branching

Do not create Culture Points.

Do not copy Technology into Civics under another name.

Civics is a separate progression domain with its own content and semantics.

------------------------------------------------------------
13. AGE
------------------------------------------------------------

Do not change the Age system as part of this task.

Age progression is separate from Technology research.

Technology completion does not automatically advance Age.

Age does not directly unlock a Technology merely because the calendar entered that Age.

Age relevance can remain metadata/context.

Do not implement the generic Age transition modifier system unless an existing implementation already requires it.

------------------------------------------------------------
14. DOWNSTREAM REALIZATION
------------------------------------------------------------

Technology completion does NOT directly spawn:

buildings
institutions
units
infrastructure
etc.

It makes them available to their relevant systems.

Example:

Engineering Technology completed
→ engineering capability available
→ factory/construction/vehicle/etc. may become eligible
→ actual realization occurs through the relevant system.

Keep this distinction.

------------------------------------------------------------
15. EXISTING TECHNOLOGY CORPUS
------------------------------------------------------------

The repository already contains a researched Technology graph/corpus.

This is important.

Do NOT discard it.

Do NOT replace it with ten sample technologies.

Audit the existing corpus and migrate/normalize it into the final data model.

Preserve existing stable IDs wherever possible.

Use the existing researched content as the foundation for:

- Main Technology Tree
- Military subtree
- Medicine subtree
- Engineering subtree
- Natural Science subtree
- Agriculture subtree

Classify existing nodes into these views based on their research-domain metadata/content.

If the existing graph contains nodes that logically remain in the main trunk, keep them in the trunk.

Do not force every existing technology into a specialized branch.

The five branches are views/subtrees of the Technology graph.

------------------------------------------------------------
16. TREE VIEWS VS GRAPH
------------------------------------------------------------

Do not duplicate nodes between trees.

There is ONE authoritative Technology graph.

The UI can display the same node in different filtered views where appropriate.

Tree 1 = Technology graph viewed through the main Technology/subtree structure.

Tree 2 = Civics graph.

Do not create duplicate Technology records just because a node appears in multiple views.

------------------------------------------------------------
17. GLASS BOX
------------------------------------------------------------

Research state must be observable without mutating simulation state.

Expose read-only queries for at least:

- active research
- completed technologies
- partial progress
- base cost
- effective cost
- cost modifiers
- prerequisites
- prerequisite satisfaction
- Eureka state
- available technologies
- available civics
- completed civics
- specialized university modifiers
- civilization knowledge/completed-node state

Use existing Glass Box/read-only patterns.

Do not allow UI to directly mutate simulation state.

Research selection must enter through the normal order pathway where applicable.

------------------------------------------------------------
18. SERIALIZATION
------------------------------------------------------------

Persist the minimum authoritative research state required for deterministic save/load:

- completed nodes
- partial research progress
- active target
- Eureka progress if persistent
- persistent research modifiers if authoritative

Derived availability should be recomputable.

Do not serialize unnecessary derived state.

Follow existing serialization conventions.

------------------------------------------------------------
19. DETERMINISM
------------------------------------------------------------

All of the following must be deterministic:

- prerequisite evaluation
- eligibility
- research progress
- cost modifiers
- Eureka evaluation
- completion ordering
- unlock application
- serialization
- available-node enumeration

Never rely on unordered dictionary iteration.

Use stable IDs and explicit ordering.

------------------------------------------------------------
20. DOCUMENT CONFLICTS TO RESOLVE
------------------------------------------------------------

The current civilization-progression-architecture.md contains older wording that must now be reconciled.

At minimum inspect and correct:

A. §3.2 / §3.4:
older wording about Science allocation / Culture allocation.

Replace with the single shared Cognitive Load Point model.

B. §4:
Technology / Knowledge wording must reflect that Technology is the civilization's knowledge progression.

C. §8:
old Cognitive Pool allocation model must be reconciled with the final shared CLP model.

D. §8.2:
the earlier accumulation/reserve conflict must be resolved in favor of the latest Director ruling:
there is no general bank of unused research points.

Do not create a general research reserve.

Partial progress is stored PER NODE, not as a general bank.

E. §8.3:
older language suggesting unresolved parallel research must be reconciled with the final model:
one current research allocation target, multiple nodes may retain partial progress.

Do not invent multi-allocation percentages.

F. §8.4:
replace the older SIX-branch model with exactly FIVE:

Military
Medicine
Engineering
Natural Science
Agriculture

Remove Industry & Energy as a Tree-1 subtree.

G. §8.4.2:
replace progressive faculty-by-faculty opening with the final ruling:
the five specialized subtrees become available when the university/research institutional stage is reached.

H. §8.5:
replace old wording about universities adding generic cognitive output with:
specialized universities reduce effective research cost for relevant technologies.

I. Any text saying specialized universities cannot yet be built because literacy does not exist:
do NOT silently invent literacy.

Instead distinguish:
- architecture dependency
- implementation dependency
- systems not yet implemented.

If literacy is genuinely required by an existing ratified system, preserve that dependency, but do not let an outdated architecture sentence prevent implementation of the research system where the required primitives can be cleanly isolated.

J. Any statement that Technology completion does not immediately grant the knowledge:
replace with the latest ruling:
Technology completion = civilization knowledge acquired and its declared capabilities become unlocked.

K. Tree 2 Civics:
must use the SAME Cognitive Load Point pool.

Do not preserve an obsolete Culture allocation model.

------------------------------------------------------------
21. DO NOT RESOLVE UNRELATED GOVERNANCE ISSUES
------------------------------------------------------------

Do not renumber DD-13/DD-14.

Do not modify unrelated ADRs.

Do not rewrite D-042/D-037/CONV-1/D-043 unless absolutely required to reconcile a direct contradiction created by these rulings.

Do not expand into Age implementation.

Do not implement military combat.

Do not implement university construction if its underlying institutional system is not ready.

Do not create fake downstream systems simply to make tests pass.

------------------------------------------------------------
22. BUILD WHAT IS ACTUALLY POSSIBLE
------------------------------------------------------------

This is NOT a documentation-only task.

After reconciling the documents, implement all currently supported pieces:

REQUIRED:

1. Final research-tree model.
2. Technology node model.
3. Civics node model.
4. Shared Cognitive Load Point research resource.
5. Active research selection.
6. Per-node partial progress.
7. Technology prerequisites.
8. AND/OR prerequisite evaluation.
9. Eureka system.
10. Technology completion.
11. Civilization completed-knowledge state.
12. Unlock/capability declarations.
13. Specialized subtree availability.
14. Specialized university research-cost modifier seam.
15. Civics research using the same CLP pool.
16. Serialization.
17. Glass Box queries.
18. Deterministic validation.
19. Tests.
20. Integration with the existing Technology corpus.

If an underlying system does not yet exist, implement the narrowest stable seam needed and explicitly report it.

Do not create speculative simulation mechanics.

------------------------------------------------------------
23. CONTENT WORK
------------------------------------------------------------

Use the existing technology graph.

Perform a full audit.

For every existing Technology node determine:

- stable ID
- tree
- branch
- research domain
- cost
- prerequisites
- Eureka
- generation/family
- unlocks
- historical relevance

Do not leave the majority of the graph disconnected just because it is inconvenient.

The objective is a functioning large Technology graph, not a toy graph.

If the existing graph already has most of this information, migrate it rather than rewriting it.

If content is missing, identify exactly what is missing and create the smallest reasonable data-driven completion consistent with the existing corpus.

Do not fabricate historical technologies merely to hit a node count.

------------------------------------------------------------
24. VALIDATION
------------------------------------------------------------

Add validators for:

- duplicate IDs
- invalid tree IDs
- invalid branch IDs
- missing prerequisites
- circular prerequisites
- invalid Eureka references
- invalid costs
- invalid unlock references
- orphan technologies
- unreachable technologies
- invalid cross-tree dependencies
- invalid branch assignment

Validation failures must be explicit.

------------------------------------------------------------
25. TESTS
------------------------------------------------------------

At minimum add tests for:

- CLP generation/input
- active research selection
- partial progress
- switching research
- completion
- completion idempotence
- prerequisites
- AND
- OR
- nested AND/OR
- Eureka
- Eureka on inactive technology
- Eureka cap
- university cost reduction
- multiple university types
- branch availability
- all five branches
- Civics
- shared CLP between Technology and Civics
- serialization
- deterministic replay
- deterministic enumeration
- invalid graph detection

Use the existing test conventions.

------------------------------------------------------------
Check out the repo for info about the trees and ages and build all you can accordingly. Take your time. decisions in this prompt are already ratified Director decisions.

Do not ask me again:

- how many branches

whether Civics shares the research

resource

whether universities reduce cost

- when branches appear

- whether technology equals knowledge

whether partial progress is retained

whether Technology is linear

- whether there are five specialized

branches

These have already been decided.

If you encounter a genuinely NEW architectural decision that is not covered here or by the repository, document it as unresolved and continue all independent work.
```

---

## PART B — WHAT THE RULINGS SETTLE

Every row below is answered by a Part A ruling; the instruction states that its decisions "are already ratified
Director decisions". Rows marked **★** are also on the **D-044 SETTLED** "do not ask again" list and must not be
re-asked. The unmarked rows are ratified rulings that an implementer does not re-open, but the Director did not
put them on the do-not-ask list, so they are not closed against the Director's own later revision. Each row
points at the controlling section, and keeps that section's scope.

| question | ruled | section |
|---|---|---|
| ★ How many top-level trees? | Two: Tree 1 Technology, Tree 2 Civics | R3 |
| ★ How many specialized subtrees of Tree 1? | Exactly five: Military, Medicine, Engineering, Natural Science, Agriculture. No Industry & Energy, no Naval, no Science tree, no 1.3.1 | R3, R20-F |
| ★ Is Technology linear? | No. AND / OR / nested prerequisite expressions; causal edges; cycles rejected | R8 |
| ★ Is there a separate Knowledge resource or tree? | No. Technology is the civilization's knowledge; the knowledge base is the set of completed Technology and Civics nodes | R1 |
| ★ What drives research? | One shared Cognitive Load Point (CLP) pool for Technology, its subtrees and Civics. No Science, Culture, per-branch or second accumulated currency | R2, R12 |
| Parallel or single target? | One active allocation target at a time **for the current implementation** (R9's own scope). Many nodes may hold partial progress. No manual percentage splits | R9, R20-E |
| Does unused research accumulate? | No general bank or reserve. Partial progress is stored per node (★ for "partial progress is retained") | R20-D |
| ★ When do the subtrees open? | All five together, when the civilization reaches the university/research institutional stage. Not on an Age. Not faculty by faculty | R4, R20-G |
| ★ What do specialized universities do? | Reduce the effective research cost of relevant nodes: BaseCost → modifiers → EffectiveCost. No flat science, no second currency, no second queue. The numerical formula is NOT ratified | R5, R20-H |
| Does completion grant the knowledge at once? | Yes. Completion = knowledge acquired + declared capabilities unlocked. No delayed knowledge-realization phase | R11, R20-J |
| Does completion build things? | No. It makes buildings, institutions, units and infrastructure available; the owning system realizes them | R11, R14 |
| Do Eurekas depend on the active target? | No. A Eureka credits its own node, is capped at that node's remaining cost, never overflows, and uses the existing predicate architecture | R10 |
| Is Age a research gate? | No. Completion never advances an Age; an Age never unlocks a node; Age is metadata | R13 |
| Is the v0.6 corpus kept? | Yes. It is migrated and normalized, not replaced; stable ids are preserved | R15, R23 |

---

## PART C — CLAUSES THIS RECORD SUPERSEDES (append-only; the originals are not rewritten)

D-042 is a Director ruling record. Its superseded clauses stay in place, unedited. Each gains an append-only
pointer note, written as its own block after the clause.

| superseded clause | original text (D-042, verbatim) | controlling ruling | what survives |
|---|---|---|---|
| **§9.1** | "Knowledge is distinct from technology and capability." | R1 | The **capability** half survives: completing a node makes things *eligible*; it is not the realized capability (R11, R14). Only "knowledge is distinct from technology" is superseded |
| **§9.3** | "Research activities proceed in PARALLEL." | R9 | Many nodes may hold partial progress at once, but throughput goes to one active target |
| **§9.4** | "Knowledge generation is a resource/flow the player ALLOCATES among concurrent research activities." | R2, R9 | Research throughput is a flow (the CLP pool). The player *directs* it by choosing one target; there is no allocation among concurrent activities |
| **§9.5** | "Unallocated knowledge ACCUMULATES AS A RESERVE rather than disappearing, and the player may deliberately hold a non-optimal reserve." | R20-D | Nothing. Throughput that reaches no node is not banked. Progress already invested in a node is kept |
| **§12**, the bullet "a rigid one-at-a-time research queue" | (anti-pattern list) | R9 | Superseded only to the extent it would forbid a single active target. There is still no queue, and switching never erases progress, so the "rigid queue" the bullet describes is still not introduced |
| **§15**, row "Is an accumulating knowledge/science quantity permitted?" | "YES — §9.4/§9.5 …" | R2, R20-D | Superseded. Per-node partial progress is the only retained quantity |
| **§15**, row "Can research proceed in parallel?" | "YES — §9.3, and §12 bans a one-at-a-time queue" | R9 | Superseded as above |

**Not superseded, and consistent with the rulings:** D-042 §9.2 (knowledge is Empire-scoped — the research state
is keyed by `PolityId`), §9.6 (other knowledge sources — Eureka is one; diffusion, exchange and espionage are not
foreclosed), §9.7 (no single rigid tree — the graph is an AND/OR network viewed through two trees, R8 and R16),
§6.2 (research direction is a persistent directive — the active target is stored state, set by an order), §6.3–6.6
(orders express intent; UI never mutates state; one pathway for player and AI), §7.3 and §12's ban on a universal
`CapabilitySystem` (R5 repeats it).

**Design documents reconciled in place under R20.** `docs/design/civilization-progression-architecture.md` is a
design-class document. R20 instructs that its sections §3.2, §3.4, §4, §8, §8.2, §8.3, §8.4, §8.4.2, §8.5 and the
literacy, completion and Civics wording be *corrected*. They are rewritten in place. The replaced original wording
is preserved verbatim in that document's new §20, so no text is lost.

---

## PART D — TENSIONS WITH FROZEN OR RATIFIED RECORDS THAT THIS RECORD DOES NOT RESOLVE

**Status of every row: UNRESOLVED. Owner: director.** The implementation proceeds on the stated reading, per the
instruction "If you encounter a genuinely NEW architectural decision … document it as unresolved and continue all
independent work."

| id | tension | reading the implementation takes | why it is not resolved here |
|---|---|---|---|
| T1 | **Milestone placement.** The frozen ladder puts knowledge at **M7** and politics/institutions at **M8**: the Spine outline numbered them M6 and M7 (`:82-85`, `:109-111`), and D-011 §6 inserted the Battle Layer as M6 and moved both down one (S8 §5: on renumbering, the addendum governs over the outline). CR-005 is **OPEN** on `main`; `origin/m5-full-build` records an unmerged "Option C … DO NOT ACTIVATE M6/M7 GAMEPLAY IN M5". `CLAUDE.md:10` still names M4 | The 2026-09-30 instruction orders the research foundation to be built now and not merged without a Director ruling. ADR-029 prices the override in the S8 §2 form (what breaks, which tests and documents change) | Only the Director rules CR-005 and edits `CLAUDE.md:10`. The two recorded positions (this instruction, and the unmerged Option C) cannot both land |
| T2 | **Spine `:84`** "Knowledge & diffusion \| M6 \| T2 \| no tree; domain lattice lite" (frozen) | R16: one authoritative graph; the two trees and five subtrees are *views* of it, not independent structures. This is the reading the architecture document already takes (§2.4, §3.1) | If the Director reads the Spine row as forbidding player-facing trees at all, a CR is owed under S8 §3 |
| T3 | **D-040 B3** "NO TECHNOLOGY UNLOCK. LAW 4 BINDS … a tech-tree node opening sea travel is a calendar gate wearing a tree" | R11 and R14: completion makes things *available*; realization still needs computed conditions in the owning system. ADR-028 §3 already ratified "technology-unlocked" availability. B3's concrete case still binds: no node opens sea travel or a network edge type, and the implementation wires no node to any movement or transport mechanism | The wording tension between B3 and ADR-028 §3 predates this record; recorded, not ruled |
| T4 | **D-021** paired-feedback rule for "research → universities → cheaper research" | R5's seam ships with no live source: no institutions system exists, so no university modifier is ever written and the loop is not closed in this tree | The brake is owed by the packet that first writes a university modifier, in that packet's milestone |
| T5 | **D-020 closed grammar** ("No functions, no arithmetic in v1 (queue if needed)", `docs/m2-spec.md:8`) | R10 requires "the existing deterministic predicate architecture". Prerequisites (R8) need AND / OR over completed nodes. The grammar is extended, not replaced: keyword aliases, boolean atoms and caller-bound names. There are still no functions and no arithmetic, and the shipped `Parse(string)` path **behaves as before** — the same results, failures and messages, pinned by `PredicateResearchDialectTests`. Its code is not byte-for-byte the old code: the parser gained the introspection bookkeeping (atom, quantity, variable and NOT tracking) that the dialect shares. ADR-029 §11 records it | An extension of a closed decision is recorded by ADR, and the Director may rule it back |
| T6 | **Repeatable frontier lines** — 10 corpus nodes carry `research.repeatable` ("Research never runs out") | R11: completion is idempotent. The ten nodes complete once. Their repeatable descriptor is kept as data, and no level mechanic is built — every one of them names a consuming mechanism that does not exist | Whether a completed node may be re-researched at a higher level is a new decision |
| T7 | **Starting holdings** (architecture §12.6; CR-006) | No node is complete at founding. This is the reading of the Director's "the game begins in the Stone Age" quoted in architecture §12.6 | CR-006 (campaign epoch) is OPEN |

---

## PART E — WHERE THE RULINGS ARE SILENT

The implementation had to decide some points the rulings do not address. For example: whether one target spans
both trees, what happens to throughput when no target is set, the provisional CLP formula, what the
"institutional stage" is in state, how Eureka prose becomes a condition, and how nodes are placed in subtrees.
Each of these is recorded in `docs/adr/adr-029-research-engine.md` §13 as an **implementer resolution**. They are
not Director rulings. Each one is overridable, and none amends a ratified record.

---

## PART F — RESOLUTION NOTES (append-only, 2026-10-01; Parts A–E above are not rewritten)

**Part D T6 — repeatable frontier lines.** The research corpus finalization (D-046 F§2) and the research-foundation
gate (D-046 G4) settle T6 as follows. The ten repeatables are kept and named as the **recursive** set
(`researchSets.recursive` in `research.json`). Each becomes available only when its own prerequisites hold **and every
finite node of its own subtree is complete**; the rule is carried as data (`repeatable.availability`) and the engine
applies it (ADR-029 addendum A). Recursive research is excluded from finite exhaustion and from pacing. The **repeat
mechanics** — re-research at a higher level, cost escalation, effects, repeat-count persistence — are **DEFERRED** by
the Director (G4): a repeatable still completes once, as R11 requires. T6's question, whether a completed node may be
re-researched at a higher level, therefore remains open as a deferred implementation, not as an unruled tension.
