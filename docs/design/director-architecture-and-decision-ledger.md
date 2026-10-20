<!--
REPOSITORY PREFACE. This block is not Director text.
Part I (the Director's ledger, verbatim) lies between the BEGIN and END DIRECTOR TEXT markers.
Part II (repository annexes R1-R7) follows the END marker.
-->

> **Canonical repository copy.** This file is the canonical in-repository record of the
> *Director Architecture and Decision Ledger*. Read it under its own §0 rule.
>
> - **Part I is the Director's text, verbatim.** It sits between the `BEGIN DIRECTOR TEXT` and
>   `END DIRECTOR TEXT` markers.
>   - It is byte-identical to the attachment the Director supplied on 2026-10-02
>     (`director-architecture-and-decision-ledger.md`, 1,335 lines, SHA-256
>     `4c18b3b9365118325f9c3a7d009bfdb38e7ca0c097c4f1745b40a9c91872b8a2`).
>   - Nothing in it was edited, re-ordered or re-formatted.
>   - Its own §30 governs every later change.
> - **Part II holds the repository annexes R1–R7.**
>   - The implementation agent wrote them on 2026-10-02 against `research-progression-foundation`
>     @ `5e83e61`.
>   - They add **no** decision. They map each Part I section to the repository's records and
>     implementation, and record what conflicts with Part I, what it supersedes, and what is still open.
>   - Where they disagree with Part I, Part I governs.
> - **Placement.**
>   - The file is in `docs/design/`, as the Director instructed on 2026-10-02.
>   - It exists on branch `research-progression-foundation` only (LOCAL and REMOTE; **not MAIN**).
>     *(2026-10-06, M5 polish pass: no longer "only" — the file is also on `m5-integration` and on the M5 candidate
>     `m5-hardening`; it is still not on MAIN.)*
>   - No copy is kept at the path an earlier instruction named,
>     `docs/architecture/director-architecture-and-decision-ledger.md` (R4.21).
> - **Gap audit.** `docs/design/director-ledger-gap-audit.md` is the read-only audit of the repository
>   against this ledger.

<!-- BEGIN DIRECTOR TEXT (verbatim; SHA-256 4c18b3b9365118325f9c3a7d009bfdb38e7ca0c097c4f1745b40a9c91872b8a2) -->
# Director Architecture and Decision Ledger

## Civilization Simulation (`civDemo`)

**Status:** RATIFIED DECISION REGISTER / IMPLEMENTATION GUARDRAIL\
**Authority:** Director rulings made during architecture, M4 closure,
progression design, and research finalization\
**Purpose:** Preserve architectural decisions across context truncation,
agent changes, future milestones, and future Claude Code sessions.

------------------------------------------------------------------------

## 0. MANDATORY READING RULE

This document is a persistent architecture guardrail.

**Any agent working on M5 or later MUST read this document before
proposing, designing, implementing, refactoring, reviewing, or testing
systems that can affect civilization progression, research, knowledge,
technology, Ages, institutions, infrastructure, industry, military,
mobile agents, economy, world state, or the simulation calendar.**

Before implementation, Claude Code MUST:

1.  Read this document completely.
2.  Read `CLAUDE.md` and repository laws.
3.  Read relevant ADRs and architecture documents.
4.  Read the current milestone specification.
5.  Inspect the current repository implementation.
6.  Distinguish RATIFIED, DEFERRED, CALIBRATION, PROPOSED, INFERRED, and
    DIRECTOR DECISION REQUIRED material.
7.  Never silently change a RATIFIED decision.
8.  Stop and report if implementation requires changing a RATIFIED
    decision.
9.  Never convert an illustrative number into an architectural constant.

### Authority order

When records conflict:

1.  Later explicit Director ruling.
2.  Ratified ADR / frozen decision record.
3.  Earlier ratified architecture.
4.  Current implementation, which is evidence of implementation rather
    than architectural authority.
5.  Proposed/inferred research material.

Never resolve a genuine architecture conflict by guessing.

------------------------------------------------------------------------

# 1. STATUS VOCABULARY

**RATIFIED:** authoritative implementation constraint.

**DEFERRED:** intentionally postponed. Do not invent an implementation.

**CALIBRATION:** tunable empirical value, not permanent architecture.

**PROPOSED:** design proposal, not authoritative.

**INFERRED:** derived interpretation that must not silently become
permanent architecture.

**DIRECTOR DECISION REQUIRED:** unresolved architectural question. Do
not guess.

------------------------------------------------------------------------

# 2. CORE ARCHITECTURE

## 2.1 Determinism

The simulation is deterministic.

Preserve:

-   stable IDs
-   stable iteration order where authoritative outcomes depend on order
-   explicit system ordering
-   deterministic predicates
-   deterministic serialization
-   deterministic tests
-   no UI mutation of simulation state
-   no hidden mutable simulation state
-   no machine-specific behavior that violates the existing determinism
    contract

ADR-022 remains authoritative for platform behavior.

## 2.2 Glass Box

The UI is read-only with respect to authoritative simulation state.

Preferred:

``` text
simulation state
    -> read-only query / adapter
    -> view model
    -> UI
```

Never make the UI a second simulation authority.

## 2.3 No God Objects

Do not introduce universal objects such as:

-   `CapabilitySystem`
-   `ProgressionSystem`
-   universal modifier engines

merely to centralize unrelated rules.

Use narrow systems, explicit predicates, and shared state.

## 2.4 Common order pathway

Player and AI actions must use the same authoritative order pathway.

UI actions must not bypass simulation rules.

## 2.5 Stable identities

Stable content IDs are part of the save/order contract.

Never renumber existing technology/content keys.

Append new nodes with new keys.

------------------------------------------------------------------------

# 3. CIVILIZATION PROGRESSION

The player-facing Civilization view is:

``` text
CIVILIZATION
├── KNOWLEDGE
├── TECHNIQUES
├── INSTITUTIONS
├── INFRASTRUCTURE
├── INDUSTRY
├── MILITARY
└── APPLICATIONS
```

These are **lenses**, not seven independent linear trees.

The underlying civilization progression is interconnected.

Actual research has exactly two top-level trees:

``` text
Tree 1: Technology
Tree 2: Civics
```

Technology has five specialized branches:

``` text
1.1 Military
1.2 Medicine
1.3 Engineering
1.4 Natural Science
1.5 Agriculture
```

Do not add an Industry & Energy research subtree simply because Industry
and Energy are civilization domains.

------------------------------------------------------------------------

# 4. TECHNOLOGY AND KNOWLEDGE

## 4.1 Research completion

For player-facing research:

``` text
technology completed
    -> knowledge/capability acquired
    -> downstream realization remains separate
```

Do NOT revert to a model where research completion merely discovers a
technology and some second research process must "realize" it.

Physical realization may still require:

-   materials
-   personnel
-   institutions
-   infrastructure
-   construction
-   manufacturing
-   logistics
-   suitable sites
-   population
-   organizational capacity

## 4.2 Knowledge ownership

Knowledge belongs to the civilization/polity.

Individuals do not permanently own civilization knowledge.

If a scientist dies, civilization knowledge remains.

------------------------------------------------------------------------

# 5. RESEARCH POINTS

## 5.1 Terminology

The authoritative name is:

**Research Points (RP)**

Use:

``` text
ResearchPoints
ResearchProgress
ResearchPointPool
```

Do not use active architectural terminology:

-   CLP
-   Cognitive Load Points
-   Cognitive Pool

Historical ADR wording may remain when required for provenance.

## 5.2 One shared pool

There is exactly one shared RP pool.

Technology and Civics use the same pool.

Do not create separate Science, Culture, Medicine, Engineering, or
Military research currencies unless separately ratified.

## 5.3 One active target

The player selects one active research target.

There is no manual percentage allocation between simultaneous research
targets.

Other technologies retain their existing progress.

## 5.4 Retained per-node progress

Example:

``` text
A = 60 / 100
switch to B
B = 30 / 80
switch back to A
A = 60 / 100
```

Progress is retained per node.

## 5.5 No general RP reserve

There is no general bank of unused RP.

Do not confuse retained per-node research progress with a global
reserve.

## 5.6 Per-turn research rate

ADR-030 governs research rate.

Current calibration:

``` text
RP/turn = 0.08 × population^0.699
```

Anchors:

``` text
100 population  -> 2 RP/turn
1000 population -> 10 RP/turn
```

Research RP is generated per strategic turn.

**Do not multiply it by `dtYears`.**

This is a research-only scoped exception. Do not create a reusable
per-turn exception mechanism.

Coefficient and exponent are calibration values.

------------------------------------------------------------------------

# 6. TECHNOLOGY GRAPH

Technology is a graph, not a linear ladder.

Prerequisites may contain AND/OR/NOT logic.

OR alternatives are genuine alternative pathways.

Do not count every OR alternative as an independent
prerequisite-complexity requirement.

## 6.1 Age does not gate research

Historical Age metadata does not directly disable a technology.

If prerequisites are satisfied, a civilization may research a later-Age
technology.

Age measures civilization development; it is not a hidden research lock.

## 6.2 Group B pathways

The canonical corpus deliberately widened several dependencies where the
true dependency is generic:

-   writing
-   law
-   numeration
-   record keeping

Chinese script, counting rods, Shang records, and other approved
alternatives can satisfy widened pathways.

The genuinely Babylonian-specific nodes remain specific.

Do not broaden those specific dependencies.

------------------------------------------------------------------------

# 7. TECHNOLOGY COSTS

## 7.1 Cost formula

Canonical methodology:

``` text
BaseCost = U × K^magnitude
```

where:

``` text
magnitude =
    novelty
  + technical difficulty
  + material sophistication
  + institutional sophistication
  + breadth of capability
  + prerequisite complexity
```

Current methodology constants:

``` text
U = 92.4
K = 2.0
```

These are content/cost-model calibration values, not a reason to
introduce CLP as an active player currency.

## 7.2 Six factors

**Novelty:** incremental refinement to paradigm-founding change.

**Technical difficulty:** difficulty of achieving the technology; Age
may be a bounded context prior.

**Material sophistication:** material requirements; theories do not
receive high material scores merely because later apparatus was required
to test them.

**Institutional sophistication:** institutional requirements; Age may be
a bounded context prior.

**Breadth of capability:** scope of what the technology unlocks; use
downstream scope, never prerequisite depth.

**Prerequisite complexity:** independent requirements; top-level AND
requirements count, OR groups count once.

## 7.3 No target-duration repricing

Do not change technology costs solely to force an Age to last a chosen
number of turns.

The current cost corpus is content-derived with no calibration
adjustment to hit a pacing target.

## 7.4 A9 population

Do not freeze approximately 108M as canonical A9 population.

It is a validation scenario for the current cost corpus.

Population assumptions must be validated by simulation and historical
plausibility.

------------------------------------------------------------------------

# 8. EFFECTIVE COST AND ACCELERATION

## 8.1 Effective cost

``` text
EffectiveCost =
    max(
        0.20 × BaseCost,
        BaseCost × active modifiers
    )
```

No modifier stack may reduce effective cost below 20% of BaseCost.

## 8.2 Shared acceleration pool

Eureka and foreign-exposure credits share one pool.

Maximum:

``` text
40% of BaseCost
```

Also capped at remaining EffectiveCost.

No overflow.

Credit provenance is retained by source for Glass Box inspection.

## 8.3 Credit-only completion

Credit-only completion is intentionally allowed.

Do NOT impose a minimum 1-RP or equivalent research-spend rule.

If acceleration credit can pay the remaining EffectiveCost, the node
completes.

## 8.4 Eureka

Eureka credit uses **BaseCost**, not discounted EffectiveCost.

Example:

``` text
BaseCost = 1000
EffectiveCost = 850
Eureka ceiling = 400
```

subject to remaining EffectiveCost.

For N equal Eureka conditions:

``` text
credit per condition = 0.40 / N × BaseCost
```

Explicit weights may override equal weighting.

Eureka is:

-   partial
-   idempotent
-   independent of active research target
-   capped
-   non-overflowing

## 8.5 Eureka authoring

Retain a Eureka only if it is a meaningful historical/gameplay
accelerator.

Do not mechanically copy construction/realization materials into Eureka
conditions.

Reachability matters:

-   if every path to a technology already implies the condition, it is
    not a useful Eureka
-   a condition requiring its own node directly or indirectly is dead

------------------------------------------------------------------------

# 9. UNIVERSITIES AND SPECIALIZATION

Specialized universities include:

-   Military
-   Medical
-   Engineering
-   Natural Science
-   Agricultural

University relevance in the research corpus is **domain classification
only**.

The intended future behavior is:

``` text
relevant specialized universities
    -> lower effective research cost of relevant technologies
```

Exact numerical formulas are NOT ratified.

Do not freeze illustrative examples such as 6 universities = 1% or 20 =
5%.

University maturity, diminishing returns, saturation, local
specialization, staffing, and institutional lifecycle belong to the
institution system.

Population can generate endogenous institutional development.

Player policy can reinforce specialization but does not necessarily stop
endogenous momentum.

------------------------------------------------------------------------

# 10. AGES

Nine Ages are ratified:

``` text
A1 Prehistoric / Stone Age
A2 Neolithic / Agricultural
A3 Bronze Age
A4 Iron Age
A5 Classical / Imperial
A6 Medieval
A7 Early Modern
A8 Industrial
A9 Modern / Contemporary
```

## 10.1 Irreversible

Civilizations never move backward in historical Age.

They can regress in:

-   population
-   infrastructure
-   institutions
-   personnel
-   capabilities
-   military strength
-   production
-   realization

but Age does not decrease.

## 10.2 Different Ages simultaneously

Different civilizations may occupy different Ages simultaneously.

## 10.3 Global turn length

At a given global Age cycle, the turn length is globally consistent.

If one civilization advances first:

-   its Age transition occurs
-   the current player cycle continues under the existing `dt`
-   the new global `dt` takes effect at the next global cycle boundary

`dt` is immutable through a complete player cycle.

Exact Age-to-dt values are not frozen.

## 10.4 Age milestones

Age transition uses:

-   mandatory core milestones
-   supporting milestones
-   category coverage

Supporting milestone categories:

1.  Technological
2.  Material-Economic
3.  Institutional-Social
4.  Systemic
5.  Military Realization

Researching a military technology does not automatically satisfy
Military Realization.

Milestones are visible to the player but are not an eighth Tree.

Partial Age progress has no direct mechanical effect.

Milestones measure realized civilization development; they are not
hidden research locks.

## 10.5 Age surge

Each civilization gets its own transition surge when entering an Age.

It:

-   starts immediately
-   ramps toward a maximum
-   later declines toward roughly half peak
-   represents transition optimization
-   affects broad outputs

Potential outputs include:

-   research
-   construction
-   resources
-   industry
-   food
-   population productivity
-   trade/economic activity
-   infrastructure
-   military production/training

The magnitude depends on civilization state.

The previously discussed +20% figure is only a tunable starting target.

Exact formula/duration is deferred.

Do not implement a generic permanent Age modifier table before the
relevant systems exist.

------------------------------------------------------------------------

# 11. AGE AND KNOWLEDGE CONTINUITY

Entering a new Age does not erase old capabilities.

Example:

``` text
irrigation acquired
-> remains available in later Ages
```

New Age capabilities are additive.

------------------------------------------------------------------------

# 12. KNOWLEDGE DIFFUSION

Foreign knowledge is not a separate progression tree.

Potential pathways:

-   trade
-   open borders
-   migration
-   scholars
-   craftsmen
-   engineers
-   students
-   military observers
-   captured equipment
-   prisoners/specialists
-   observation
-   institutional imitation
-   warfare lessons

Conceptual exposure data may include:

``` text
SourcePolity
Domain
KnowledgeLevel
ContactStrength
ContactType
ExposureDuration
Reliability
LocalAbsorption
```

Exact implementation is deferred.

Discovery, contact, understanding, adoption, and realization are
distinct concepts.

An isolated civilization does not receive catch-up knowledge from an
unknown/uncontacted civilization.

Direct contact is stronger than transitive exposure.

First-mover advantage comes from mature institutions, personnel,
production, infrastructure, and organizational capability, not exclusive
permanent knowledge ownership.

------------------------------------------------------------------------

# 13. INSTITUTIONS

Institution lifecycle:

``` text
policy / knowledge / civic conditions
    -> demand / priority
    -> endogenous investment
    -> construction
    -> founded
    -> specialization
    -> maturation
    -> production / knowledge / service flows
    -> diffusion / adoption
    -> civilization effects
    -> feedback
```

Examples:

-   universities
-   hospitals
-   engineering schools
-   factories
-   military academies
-   research institutes

Do not reduce institutions to static bonuses.

------------------------------------------------------------------------

# 14. INFRASTRUCTURE AND VIABILITY

Use hierarchical/type-specific viability domains.

No universal institution hard cap.

Examples:

``` text
university -> settlement/metropolitan viability
hospital -> catchment viability
dam -> river/site viability
mine -> deposit viability
port -> suitable water/location viability
factory -> settlement + regional inputs/logistics
```

Additional institutions use viability thresholds and diminishing
returns.

Do not implement arbitrary universal maxima such as "maximum 10
universities per city."

------------------------------------------------------------------------

# 15. CONSTRUCTION

Construction capacity is generated for the relevant period.

Unused capacity disappears rather than becoming an indefinitely bankable
stock.

Player allocation uses current available capacity.

Illustrative construction numbers are not frozen.

Construction continues during war unless a later explicit system rule
changes this.

------------------------------------------------------------------------

# 16. BUILDING VISUALIZATION

Buildings are visually aggregated inside settlements.

Do not render one persistent individual world object for every building.

At high zoom, category counts may be displayed on icons.

Visual counts must remain causally constrained.

Avoid absurd outcomes such as:

-   hundreds of dams on one river
-   impossible university counts
-   building counts unsupported by population, site, institutional, or
    production capacity

University morphology examples such as 1/10/20/30 are illustrative
visualization milestones only.

Settlement visual classification is population-based.

------------------------------------------------------------------------

# 17. MOBILE AGENTS

## 17.1 Universal model

Movable/independent agents use a common MobileAgent architecture.

Possible agents:

-   tanks
-   armies
-   battalions
-   heroes
-   scientists
-   engineers
-   artists
-   musicians
-   cultural groups
-   bands

The architecture must not assume all agents are military.

## 17.2 Action Capacity

Every MobileAgent has Action Capacity.

Actions are agent-specific.

Special people have limited actions and can age/die.

A historical artist may produce a finite number of meaningful works and
then cease to exist.

Do not make historical people immortal gameplay objects.

## 17.3 Ownership

MobileAgents belong to the player/AI that recruits or controls them.

Historical nationality does not determine gameplay ownership.

A historical person can be recruited by a non-historical civilization.

No mandatory cross-national recruitment payment is ratified.

## 17.4 Armies

Armies are MobileAgents with internal composition.

They may split and merge.

A visually represented army may show a single banner/token and an
internal personnel count.

## 17.5 Cultural groups/bands

Cultural groups/bands are real MobileAgents.

They may generate:

-   revenue
-   culture
-   diplomatic effects
-   loyalty effects
-   unrest effects

Members have lightweight lifecycle data; not every member becomes an
independent map token.

------------------------------------------------------------------------

# 18. MOVEMENT

Movement uses continuous x/y positions.

Movement profiles are agent-specific.

Movement cost may depend on:

-   terrain
-   infrastructure
-   transport
-   experience
-   doctrine
-   weather
-   supply
-   fatigue
-   enemy territory
-   agent type

Road graphs are preferred where viable.

**Off-road movement is allowed.**

Agents choose the least Action-Capacity-consuming viable route.

Ships may move on open water/free positions.

Do not restrict all agents to roads.

------------------------------------------------------------------------

# 19. WAR PULSES

War Pulses can cross strategic turn boundaries.

Construction continues during war.

During War Pulses:

-   involved military agents receive refreshed Action Capacity
-   players retain full operational control
-   multiple battles can occur
-   retreats can occur
-   territory can be conquered/ceded

Do not reintroduce a blanket rule prohibiting player operational control
during War Pulses.

------------------------------------------------------------------------

# 20. RESOURCE CONSERVATION

Strategic physical goods are conserved.

Do not model every kilogram of every commodity.

Existing precedent includes conserved farm tools that wear out.

Strategic goods may include:

-   farm tools
-   weapons
-   armor
-   vehicles
-   machines
-   other strategically meaningful equipment

Exact commodity taxonomy remains deferred.

------------------------------------------------------------------------

# 21. CAPABILITY OWNERSHIP

Before deep research/world integration, every relevant baseline
capability/building/unit/activity must be classified as:

### BASELINE

Exists without research.

### TECHNOLOGY-OWNED

Requires completion of a research node.

### REALIZATION

Knowledge/capability exists, but physical realization requires
downstream conditions.

### MIXED

Use only where a genuine split exists.

The purpose is to prevent contradictions such as:

``` text
granary exists at founding
AND
granary existence is treated as proof that its technology was researched
```

The initial world may contain baseline capabilities without implying
completion of the entire related research branch.

------------------------------------------------------------------------

# 22. RECURSIVE RESEARCH

There are 10 repeatable research nodes.

Current availability rule:

> A repeatable becomes available after finite non-repeatable research in
> its own subtree is exhausted.

Repeat mechanics are deferred.

Do not redesign:

-   repeat counts
-   repeat cost escalation
-   repeat effects
-   diminishing returns
-   Age interaction
-   persistence

Research Foundation closure must not be blocked by recursive mechanics.

------------------------------------------------------------------------

# 23. RESEARCH EFFECTS

`effects.immediate` remains empty.

Do not invent a universal immediate-effect mechanism.

Prefer:

``` text
technology
    -> declared capability/unlock
    -> domain system
    -> actual simulation effect
```

Research data should not become a God object containing every downstream
civilization rule.

------------------------------------------------------------------------

# 24. VISUALIZATION

World visualization is a read-only projection of authoritative
simulation state.

Every drawn entity should be attributable to an authoritative source.

Do not infer simulation facts merely from appearance.

Do not silently mix demo data into a live simulation view.

For MobileAgents:

``` text
one reported agent -> one visual token
```

Internal composition may appear as labels/counts.

Visualization never owns the agent.

------------------------------------------------------------------------

# 25. PROGRESSION / REALIZATION SEPARATION

Preserve the conceptual distinction:

``` text
Knowledge / Technology
        ↓
capability
        ↓
technique / practice
        ↓
institutionalization
        ↓
physical realization
        ↓
application
        ↓
civilization effect
```

Not every domain requires every layer.

Do not collapse all progression into one generic bonus system.

------------------------------------------------------------------------

# 26. PROHIBITED REINTRODUCTIONS

Future work must not silently reintroduce:

1.  A universal linear technology tree.
2.  Separate research currencies per branch.
3.  A Cognitive Pool as a second currency.
4.  A general RP reserve.
5.  Manual percentage allocation among multiple research targets.
6.  Direct Age-gated technology availability.
7.  A universal CapabilitySystem.
8.  Unrestricted individual building objects on the world map.
9.  Universal hard institution caps.
10. RP multiplied by `dtYears`.
11. A reusable generic per-turn exception mechanism.
12. Individual ownership of civilization knowledge.
13. Immortal historical people.
14. Mandatory historical-nationality recruitment.
15. Mandatory cross-national recruitment payment.
16. Identical movement profiles for all MobileAgents.
17. Road-only movement.
18. Blanket player-control prohibition during War Pulses.
19. Technology repricing solely to hit target Age duration.
20. Fake baseline research nodes.
21. A separate Technique research tree merely because techniques exist.
22. An Industry & Energy Technology branch merely because those domains
    exist.
23. Generic Age modifiers before relevant systems exist.
24. Artificial minimum RP spending to defeat credit-only completion.
25. Eureka overflow.
26. Eureka credit calculated from discounted EffectiveCost.
27. Mechanical Eureka conditions copied from realization materials
    without justification.

------------------------------------------------------------------------

# 27. INTENTIONALLY DEFERRED

These are not forgotten.

### Research

-   repeatable mechanics
-   foreign exposure implementation
-   final research calibration
-   final reference populations
-   exact university discount formula
-   final RP calibration if simulation evidence requires adjustment

### Institutions

-   exact maturity curves
-   staffing formulas
-   saturation equations
-   local specialization equations
-   endogenous investment equations

### Ages

-   exact Age-to-dt values
-   exact Age surge formula
-   exact surge duration
-   permanent generic Age effects
-   final milestone thresholds

### World systems

-   detailed capability realization
-   industrial production
-   infrastructure capacity formulas
-   military realization
-   detailed logistics
-   population/productivity coupling

Do not fill these gaps with invented constants.

------------------------------------------------------------------------

# 28. M5 ENTRY GATE

M5 starts only after the Research Foundation is accepted.

Required before M5:

1.  Canonical research corpus exists in repository.
2.  ADR-030 exists.
3.  Research terminology is standardized.
4.  Research tests pass.
5.  Determinism tests pass.
6.  Goldens are intentionally re-pinned with measured causes.
7.  Capability Ownership Matrix exists.
8.  Baseline capabilities are explicitly classified.
9.  No known research/starting-world contradiction remains unresolved.
10. Provisional calibration has not been accidentally frozen as
    architecture.
11. No unresolved Director Decision is hidden inside implementation.

M5 consumes the Research Foundation.

M5 does not reopen research architecture unless a concrete contradiction
is demonstrated.

------------------------------------------------------------------------

# 29. REQUIRED CLAUDE CODE WORKFLOW FOR M5 AND FUTURE MILESTONES

Before any implementation:

### READ

Read this document, `CLAUDE.md`, relevant ADRs, relevant architecture
documents, and the current milestone specification.

### INVENTORY

Identify:

-   authoritative state
-   existing systems
-   invariants
-   tests
-   serialization
-   Glass Box observability
-   existing content/schema

### CONFLICT CHECK

Explicitly ask:

``` text
Does this conflict with RATIFIED architecture?
Does it turn a DEFERRED question into an implicit rule?
Does it freeze a CALIBRATION value?
Does it create a new global abstraction?
Does it bypass the common order/state pathway?
Does it create hidden UI authority?
```

If yes, stop and report.

### IMPLEMENT

Implement only the milestone scope.

Do not perform unrelated cleanup.

### TEST

Run:

-   build
-   targeted tests
-   full tests
-   determinism
-   serialization
-   Glass Box validation where relevant
-   golden comparisons where relevant

### DOCUMENT

If a new architectural decision is created:

-   create/update the appropriate ADR
-   update this ledger only when the decision is explicitly ratified
-   mark unresolved ideas correctly

### REPORT

Report:

-   branch
-   commit SHA
-   PR
-   files changed
-   tests
-   determinism
-   goldens
-   architectural changes
-   deferred items
-   Director decisions required

Do not merge unless explicitly instructed.

------------------------------------------------------------------------

# 30. MAINTAINING THIS DOCUMENT

This is a living ratified decision register, not a scratchpad.

Update it only when:

1.  A Director decision changes a ratified rule.
2.  A new architecture rule is explicitly ratified.
3.  A deferred question is resolved.
4.  A rule is formally superseded.

Never silently rewrite history.

When superseding a decision:

-   identify the previous rule
-   state the new rule
-   state what changed
-   reference the relevant ADR/commit when available

If a conversational decision is not recorded in the repository, it is
**unsafe to rely on it permanently**.

------------------------------------------------------------------------

# 31. FINAL ARCHITECTURAL PRINCIPLE

The simulation should model civilization as a living, interacting system
rather than a collection of disconnected bonuses.

Prefer causal relationships:

``` text
population
    ↓
people / skills / institutions
    ↓
research
    ↓
knowledge
    ↓
capabilities
    ↓
techniques
    ↓
institutions / infrastructure / industry
    ↓
production / services / military / applications
    ↓
civilization outcomes
    ↓
new research conditions
```

When implementing a feature, prefer a causal interaction with the
existing simulation over an isolated modifier.

The objective is not merely to make a technology tree work.

The objective is a deterministic, inspectable, interconnected
civilization simulation.

------------------------------------------------------------------------

# DIRECTOR GATE

Before declaring an architectural milestone complete, ask:

> **Could a fresh implementation agent reconstruct the intended
> architecture from the repository alone, without access to the
> Director's private conversation history?**

If the answer is no, the architecture is not safely documented.

Add the missing decision to the repository before proceeding.
<!-- END DIRECTOR TEXT -->

------------------------------------------------------------------------

# PART II — REPOSITORY ANNEXES (not Director text)

These annexes reconcile Part I with the repository. They are bound by four rules:

- **They decide nothing.** They add no decision, close no OPEN item and amend no other record.
- **Part I governs.** Where an annex disagrees with Part I, Part I wins.
- **"Measured"** means the implementation agent checked the claim on 2026-10-02. It checked against
  branch `research-progression-foundation` @ `5e83e61` and the remote refs fetched that day.
- **Implementation status expires** with the tree it was measured on
  (`docs/gov-4-repository-freshness.md` §2–§3).

## R1. Sources and provenance

| source (as supplied 2026-10-02) | what it is | authority here | where it lives in the repository |
|---|---|---|---|
| `director-architecture-and-decision-ledger.md` (SHA-256 `4c18b3b9…72b8a2`) | the Director's ledger | **authoritative** for every decision it explicitly contains (Director instruction, 2026-10-02) | Part I of this file |
| `CivDemo_Director_Handoff1.txt` (September 2026; SHA-256 `4ec4783d…`) | an earlier Director-role handoff from the M4 era | **secondary.** Its decisions are classified in R5. Its repository-state section is stale (R4.20) | not committed |
| `research-corpus-audit-report.md` (SHA-256 `9853d2d9…`) | a corpus audit written before the finalization. Status: PROPOSED, NOT COMMITTED | **historical.** Superseded in part (R4.6) | the proposals that were ruled were applied through D-046 |
| `research-final-cost-report.md` (SHA-256 `fcb2b300…`) | the cost methodology and per-node costs | **CALIBRATION** evidence (Part I §7) | `Sim.Data/content/research.json` (`tuning.costModel`, per-node `costRationale`); `docs/research-corpus-audit.md`; `docs/research-calibration-report.md` (PROVISIONAL) |
| `adr-030-per-turn-research-points.md` (draft; SHA-256 `b1570b9e…`) | the Director-ruled ADR | **RATIFIED** | `docs/adr/adr-030-per-turn-research-points.md`. §1–§6 are the draft verbatim; §7, a citation check, was appended at commit |
| `claude-code-prompt-research-finalization.md` (SHA-256 `51554b91…`) | the Director's finalization instruction of 2026-10-01 | **RATIFIED,** as recorded in D-046 | `docs/d046-research-foundation-gate-rulings.md`; implemented at `1642b7d`…`5e83e61` |
| `gap-partial.txt` (two identical copies; SHA-256 `209d4337…`) | the implementation agent's own partial read-only analysis. It covers 4 of 6 areas; a container restart cut the run short | **secondary evidence.** Every citation reused from it was re-measured at `5e83e61` | superseded by `docs/design/director-ledger-gap-audit.md` |

## R2. Status vocabulary used in these annexes

**Decision status** — what the governing record says:

| label | meaning | Part I §1 equivalent |
|---|---|---|
| RATIFIED | a Director ruling, recorded in the repository or in Part I | RATIFIED |
| DEFERRED | intentionally postponed. Do not invent an implementation | DEFERRED |
| CALIBRATION | a tunable value, not architecture | CALIBRATION |
| OPEN | unresolved, with a named owner: a CR, a D-record row or a register entry | DIRECTOR DECISION REQUIRED |
| HISTORICAL | kept for provenance; no longer governs | — |
| SUPERSEDED | replaced by the later ruling named in the row | — |
| PROPOSED / INFERRED | as Part I §1 | PROPOSED / INFERRED |

**Implementation status** — what the tree at `5e83e61` does:

| label | meaning |
|---|---|
| IMPLEMENTED | code or content enforces the rule; a pinning test is named where one exists |
| PARTIAL | part of the rule is enforced; the gap is named |
| NOT YET IMPLEMENTED | no code exists, and nothing contradicts the rule |
| CONFLICT | shipped code or content contradicts the rule |
| N/A | the rule constrains future work only |

**Locations:**

| tag | meaning |
|---|---|
| **MAIN** | on `origin/main` `93270cd` |
| **PR #10** | only on `research-progression-foundation` (`5e83e61` when measured, before this pass's documentation commits; PR #10, open, not merged; then 19 commits ahead of MAIN, 0 behind) |
| **UNMERGED-B** | only on `claude/civdemo-work-b1z2y4` (`6dded01`; not an ancestor of `5e83e61`; not on MAIN) |
| **M5-BRANCH** | only on `m5-full-build` (`9a79d1e`; not merged) |

## R3. Reconciliation register — every ruling section of Part I

The "audit" column names the area in `docs/design/director-ledger-gap-audit.md` that holds the evidence.

| Part I § | decision status | repository record (location) | implementation at `5e83e61` | audit |
|---|---|---|---|---|
| §2.1 determinism | RATIFIED | CLAUDE.md law 5; ADR-022 (MAIN) | IMPLEMENTED. Banned-constructs gate and determinism suites. CI `determinism` and `determinism-xproc` green on `5e83e61` (2026-10-01) | — |
| §2.2 Glass Box | RATIFIED | T4.19 observability (MAIN); `ResearchCreditRow`, per-source credit provenance (PR #10) | IMPLEMENTED for the shipped systems | 5, 6 |
| §2.3 no God objects | RATIFIED | D-042 §7.3 (MAIN); ADR-028 (MAIN); D-043 C2 (UNMERGED-B) | IMPLEMENTED. No `CapabilitySystem` or `ProgressionSystem` type exists in `Sim.Core` (measured) | 7, 14 |
| §2.4 common order pathway | RATIFIED | D-042 §6.2 (MAIN), cited at `Sim.Core/State/WorldState.cs:801`; the `SetResearchTarget` order (PR #10) | IMPLEMENTED for research | 3 |
| §2.5 stable identities | RATIFIED | `research.json:5`: "Never renumber a key; append new nodes with new keys" (PR #10) | IMPLEMENTED (keys validated by the loader) | 30 |
| §3 seven lenses; two trees; five branches | RATIFIED | D-044 R1–R4, R12 (PR #10); architecture §3 (MAIN, and the reconciled copy on PR #10); UI lenses (UNMERGED-B) | Trees and branches: IMPLEMENTED (`ResearchContent.cs` validators). Lenses: N/A in `Sim.Core`. The architecture lists **six** lenses (STALE, R4.7). The UI's seven lenses match Part I | 10, 11, 12 |
| §4.1 completion grants knowledge and capability; realization separate | RATIFIED | D-044 R11, R14 (PR #10); ADR-028 §3 (MAIN) | IMPLEMENTED. Completion builds nothing (`ResearchSystem.cs:48-50`), and no system consumes eligibility (`ResearchQuery.cs:529-532`); only the `sim research` CLI reports it | 7, 8, 9 |
| §4.2 knowledge belongs to the polity | RATIFIED | D-042 §9.2 (MAIN); architecture §4.1; D-043 B4 (UNMERGED-B) | IMPLEMENTED. `ResearchCompletedRow(PolityId, ResearchNodeId)` (`WorldState.cs:826`); no row names an individual holder | 7 |
| §5.1 RP terminology | RATIFIED | D-046 G5 (PR #10) | IMPLEMENTED. No `CLP` or `Cognitive` identifier in `Sim.Core`, `Sim.Cli`, `Sim.Ui`, `scripts` or `.github` (measured) | 1 |
| §5.2 one shared pool | RATIFIED | D-044 R2 (PR #10) | IMPLEMENTED | 2 |
| §5.3 one active target | RATIFIED | D-044 R9 (PR #10) | IMPLEMENTED | 3 |
| §5.4 progress kept per node | RATIFIED | D-044 R20-D (PR #10) | IMPLEMENTED. Pinned by `ResearchEngineTests.Switching_KeepsPartialProgressOnEveryNode_TheSixtyOfAHundredRule` | 4 |
| §5.5 no general RP reserve | RATIFIED | D-044 R20-D; ADR-029 R-2 (PR #10) | IMPLEMENTED. No owned table holds unspent RP, and completion overflow is discarded (`ResearchSystem.cs:199-214`), pinned by `Completion_AtEffectiveCost_IsImmediate_ClearsTheTarget_AndLosesTheOverflow`. Whether completion overflow may carry to the next target is still recorded as a Director decision (D-046 Part D item 2; ADR-029 A.4 R-2) | 2 |
| §5.6 per-turn RP | RATIFIED rule; coefficient and exponent are CALIBRATION | ADR-030 (PR #10); CR-018 RESOLUTION note (PR #10) | IMPLEMENTED. One code site with no `dtYears` (`ResearchSystem.cs:158-167`), pinned by `ResearchPoints_ArePerTurn_IdenticalAcrossDifferentDtYears_TheAdr030Exception` | 1 |
| §6 graph; AND/OR/NOT | RATIFIED | D-044 R8, which names AND, OR and nested AND/OR (PR #10); ADR-029 §2.2 (`adr-029-research-engine.md:71`; PROPOSED, PR #10) | **CONFLICT on NOT.** The loader refuses NOT in a prerequisite (`ResearchContent.cs:387-389`: *"so completing knowledge can never make a node LESS available"*) and in the research-stage predicate (`:430`). OPEN — see the gap audit | 10, 30 |
| §6.1 Age does not gate research | RATIFIED | D-044 R13; architecture §12.4 | IMPLEMENTED. *"Age is never read"* (`ResearchSystem.cs:50`) | 15 |
| §6.2 Group B pathways | RATIFIED | D-046 F§1, Group B (PR #10) | IMPLEMENTED. Pinned by the Group B tests in `ResearchContentTests` | 30 |
| §7.1–§7.2 cost formula; six factors | RATIFIED method; U and K are CALIBRATION | `research.json` `tuning.costModel` and per-node `costRationale` (PR #10) | IMPLEMENTED. Costs are checked against U·K^m (`Canonical_Costs_AreContentDerived_UTimesKToTheMagnitude_NoCalibrationAdjustment_NeverDepth`) | — |
| §7.3 no target-duration repricing | RATIFIED | D-046 G1 (PR #10) | IMPLEMENTED. The loader rejects any calibration adjustment (`ResearchContent.cs:757-758`) | — |
| §7.4 A9 population not frozen | RATIFIED | D-046 G1; `docs/research-calibration-report.md` (PROVISIONAL) | IMPLEMENTED. No Age or reference-population constant exists | — |
| §8.1 EffectiveCost floor | RATIFIED | D-046 G2; ADR-029 addendum A (PR #10) | IMPLEMENTED as a seam; no modifier source is live | 6 |
| §8.2 shared acceleration pool | RATIFIED | D-046 G2 (PR #10) | IMPLEMENTED. Foreign exposure is an input seam with no writer | 6, 22 |
| §8.3 credit-only completion | RATIFIED | D-046 G2 (PR #10) | IMPLEMENTED. Pinned by `CreditOnlyCompletion_WhenCreditCoversTheRemainingEffectiveCost_TheNodeCompletesWithNoRpSpent` | 6 |
| §8.4 Eureka on BaseCost | RATIFIED | D-045 Part B row 5; D-046 (PR #10) | IMPLEMENTED. Pinned by `FullEureka_IsFortyPercentOfBASE_AUniversityStyleModifierDoesNotShrinkIt` | 5 |
| §8.5 Eureka authoring | RATIFIED | D-046 F§1: 81 curated Eurekas, 73 authored and 8 inherited (PR #10) | PARTIAL. Implied conditions are rejected (`Rejects_ImpliedEurekas_…`, `Canonical_NoEvaluableKnowledgeEureka_…`), and so are dead ones (`Rejects_InvalidEurekaReferences_UnknownNames_AndDeadConditions`). The 8 inherited Eurekas carry generic justifications | 21 |
| §9 universities | RATIFIED (relevance is classification only); formulas DEFERRED | D-044 R5 (PR #10); ADR-028 §2 (MAIN) | Content IMPLEMENTED: a classification with no numbers. No university system exists (NOT YET IMPLEMENTED) | 20 |
| §10 nine Ages | RATIFIED | D-043 A1 (UNMERGED-B); architecture §12.1 (MAIN, PR #10) | PARTIAL. Node `age` metadata uses exactly A1–A9, but no Age state exists. The UI placeholder has six Ages (UNMERGED-B) | 13 |
| §10.1–§10.2 irreversible; simultaneous | RATIFIED | D-043 A1, A6 (UNMERGED-B) | NOT YET IMPLEMENTED | 15 |
| §10.3 global turn length by Age cycle | RATIFIED rule; values DEFERRED | architecture §12.3, §19 item 7 (the mapping is OPEN); frozen Spine S3 (`civ-sim-architecture-v3-outline.md:34`) and D-006 (`m0-kernel-spec.md:13`, `:25`) | NOT YET IMPLEMENTED. dt comes from calendar bands (`TurnExecutor.cs:86`; `era-pacing.json`). Binding the frozen dt rule to Ages is OPEN (R4.14) | 16 |
| §10.4 Age milestones; five categories | RATIFIED | none before this file (R6.2) | NOT YET IMPLEMENTED. Ownership is OPEN as **AGE-MS-1** (R6.3) | 14 |
| §10.5 Age surge | RATIFIED shape; formula and duration DEFERRED; the +20% target is CALIBRATION | D-043 A5 (UNMERGED-B); CR-017 §2 OPEN (UNMERGED-B) | NOT YET IMPLEMENTED | 15 |
| §11 Age and knowledge continuity | RATIFIED | D-043 A6 (UNMERGED-B) | IMPLEMENTED for research knowledge. No code path removes a `ResearchCompletedRow` (measured) | 15 |
| §12 knowledge diffusion | RATIFIED principles; implementation DEFERRED | ADR-029 addendum A seam (PR #10) | The seam is IMPLEMENTED. `ResearchExposureRow` is read at `ResearchQuery.cs:431` and written by no system. Diffusion: NOT YET IMPLEMENTED | 22 |
| §13 institution lifecycle | RATIFIED | ADR-028 §3 lifecycle (MAIN). CR-010, the institution definition, was never written (`docs/current-state.md:13`) | NOT YET IMPLEMENTED | 26 |
| §14 viability | RATIFIED; equations DEFERRED | ADR-028 §1–§2 (MAIN); D-043 C2 (UNMERGED-B) | NOT YET IMPLEMENTED. The only viability code is migration's destination viability (ADR-012) | 25 |
| §15 construction capacity not bankable | RATIFIED | D-043 C1 (UNMERGED-B) | **CONFLICT.** `ConstructionSystem` conforms; PathBuild banks unused path labour (R4.15) | 27 |
| §16 building visualization | RATIFIED | D-043 D1, D2 (UNMERGED-B) | Not on this branch. The world layer exists only on UNMERGED-B, where D-043 F36 and F37 record divergences | — |
| §17 MobileAgents | RATIFIED | D-043 B1–B6 (UNMERGED-B); CR-017 §3 and §7 OPEN (its §8 records the INFERRED non-conflicts); D-045 §1, *"Do NOT create a Builder MobileAgent"* (`d045-research-calibration-rulings.md:55`, PR #10) | NOT YET IMPLEMENTED. No MobileAgent, Action Capacity or Army type exists in `Sim.Core` or `Sim.Ui` (measured) | 23 |
| §18 movement | RATIFIED | D-043 B7 (UNMERGED-B). It conflicts with frozen D-009/D-010 and with D-040 B4 (MAIN); that conflict is CR-017 §5, OPEN | NOT YET IMPLEMENTED for agents. Flow pathing runs on the terrain lattice, with network edges as fast lanes, so it already leaves the roads over land (`Sim.Core/Pathing/TraversalLattice.cs:8-10`). Positions are discrete, and water-majority nodes are impassable (`:14-15`) | 24 |
| §19 War Pulses | RATIFIED | D-043 B8, superseded in part (UNMERGED-B; R4.10); CR-017 §6 with CR-006 §1, OPEN | NOT YET IMPLEMENTED | 24 |
| §20 strategic goods conserved | RATIFIED; taxonomy DEFERRED | CLAUDE.md law 1; farm-tool wear through Ledger sink `ToolWear` (`Sim.Core/State/Ids.cs:145` at `5e83e61`; `:135` on MAIN) | IMPLEMENTED for the goods that exist | — |
| §21 capability ownership | RATIFIED | D-046 G3 matrix, `docs/design/research-capability-ownership.md` (PR #10) | PARTIAL. 179 items are classified (A 48 · B 0 · C 131 · D 0). Five activity entities and several node strings still claim baseline capabilities (matrix §5; not applied) | 17; gap audit §3 |
| §22 recursive research | RATIFIED rule; mechanics DEFERRED | D-046 G4; D-044 Part F T6 note (PR #10) | IMPLEMENTED. Availability is held as data, and each node completes once | 29 |
| §23 `effects.immediate` empty | RATIFIED | ADR-029 R-19 (PR #10) | IMPLEMENTED. The loader rejects a non-empty list (`ResearchContent.cs:325-326`), pinned at `ResearchContentTests.cs:672-673` | 28 |
| §24 visualization read-only | RATIFIED | Glass Box (MAIN); world layer (UNMERGED-B) | Not on this branch | — |
| §25 progression/realization separation | RATIFIED | D-044 R11, R14; ADR-028 §3 | IMPLEMENTED as far as code exists: only the research layer is built, and it collapses no layer | 7 |
| §26 prohibited reintroductions | RATIFIED | gap audit §5 checks all 27 | None reintroduced. Two pre-existing states diverge from Part I: the PathBuild bank (§15), and discrete, land-only lattice pathing (§18: no continuous positions, no open water) | §5 |
| §27 intentionally deferred | DEFERRED | D-043 Part E (UNMERGED-B); D-046 Part D (PR #10) | IMPLEMENTED in the sense that nothing deferred has been built | — |
| §28 M5 entry gate | RATIFIED | PR #10 (open, not merged) | 9 of 11 conditions MET, 1 PARTIAL (8), 1 NOT MET (9). The Research Foundation is not yet accepted | §6 |

## R4. Superseded, historical, stale and conflicting records (append-only)

**No record below was edited.** Each entry is recorded here so that it is not re-litigated or silently
relied on.

**R4.1 — Handoff knowledge stock — SUPERSEDED.**
- **The text.** The handoff summarises "D-040 B3 / D-041" as "Knowledge can accumulate as a stock …
  unused knowledge may accumulate".
- **What governs.** D-044 R20-D (PR #10) and Part I §5.5 ("There is no general bank of unused RP").

**R4.2 — D-042 §9.3–§9.5 (MAIN) — SUPERSEDED.**
- **The clauses.** Parallel allocation, and "Unallocated knowledge ACCUMULATES AS A RESERVE".
- **What governs.** D-044 R2, R9 and R20-D.
- **Status of the notes.** Append-only supersession notes already stand at
  `d042-empire-and-player-control-addendum.md:173-183`, but on PR #10 only. MAIN's copy has none
  (measured).

**R4.3 — D-044 R2's name, "Cognitive Load Points" (PR #10) — SUPERSEDED.**
- **What governs.** D-046 G5 and Part I §5.1.
- **Wording.** The historical wording stays where provenance needs it (Part I §5.1).

**R4.4 — ADR-029 §5 step 2, "eurekaCreditFraction × EffectiveCost" (PR #10) — SUPERSEDED.**
- **What governs.** ADR-029 addendum A, under its precedence clause; Part I §8.4.

**R4.5 — ADR-029 §1's measured pacing, and CR-018 option 1, per-sim-year RP (PR #10) — SUPERSEDED.**
- **What governs.** ADR-030 and Part I §5.6.
- **Notes.** CR-018 carries its own append-only RESOLUTION.

**R4.6 — Corpus audit report §C's fifth category — now RATIFIED by Part I §10.4.**
- **The proposal.** The report (PROPOSED, not committed) proposed a fifth milestone category,
  "military realization", marked DIRECTOR DECISION REQUIRED.
- **What survives as input.** The report's Age→milestone mapping table stays PROPOSED input to
  AGE-MS-1.
- **What stays deferred.** Final thresholds are DEFERRED (§27).

**R4.7 — Architecture §3.3 (MAIN and PR #10) — STALE.**
- **The text.** It names **six** lenses: Techniques, Institutions, Infrastructure, Industry, Military
  and Applications, with Technology and Civics as the two trees.
- **The difference.** Part I §3 names **seven** lenses, adding KNOWLEDGE.
- **Not a substantive contradiction.** Architecture §4.1 forbids a Knowledge *resource, domain or
  tree*, and Part I's KNOWLEDGE is a lens, which is a view.
- **Still OPEN.** Which lens surfaces Tree 2 (Civics). Part I does not say.

**R4.8 — Architecture §16.1, the seven-lens Trees UI on UNMERGED-B — PARTIALLY STALE.**
- **The text.** It calls the UI "legacy/demo and non-authoritative".
- **What changed.** Its lens list now matches Part I §3 exactly: KNOWLEDGE, TECHNIQUES, INSTITUTIONS,
  INFRASTRUCTURE, INDUSTRY, MILITARY, APPLICATIONS (measured).
- **What still holds.** Its content stays placeholder.

**R4.9 — Architecture §4 and §18, "Eight domains — DIRECTOR RULING" — NOT superseded.**
- Domains are not lenses, so the two lists can coexist. The wording should say so when the document is
  next amended.

**R4.10 — D-043 B8 (UNMERGED-B) — SUPERSEDED IN PART.**
- **The clause.** "War Pulses operate inside the strategic turn."
- **What governs.** Part I §19: "War Pulses can cross strategic turn boundaries" and "players retain
  full operational control".
- **Why Part I is later.** Part I cites ADR-030 (2026-10-01); D-043 is dated 2026-09-29.
- **Still OPEN.** The collision with the frozen kernel contract, the sub-step rule and D-011:
  CR-017 §6 together with CR-006 §1.

**R4.11 — D-043 Part B and CR-017 (UNMERGED-B) against Part I §17–§19 — OPEN.**
- **The relationship.** Part I §17–§19 restate and extend D-043 Part B.
- **Still OPEN in CR-017.** Each collision with a frozen item:
  - Action Capacity per turn against Law 3 (§3);
  - movement against D-009/D-010 (§5);
  - War Pulses against the kernel contract (§6);
  - special people against Law 1 and D-010's notables (§7).
- **What Part I does not do.** It does not name CR-017 and does not rule it.
- **Consequence.** Applying any of §17–§19 to a frozen item is DIRECTOR DECISION REQUIRED.

**R4.12 — D-040 B3 (MAIN) and D-044 Part D T3 (PR #10, UNRESOLVED) — OPEN.**
- **The texts.**
  - D-040 B3: "NO TECHNOLOGY UNLOCK".
  - D-044 Part D T3 (`d044-research-progression-rulings.md:841`): UNRESOLVED.
- **Tension.** Both stand against Part I §4.1 and §21. TECHNOLOGY-OWNED means "requires completion of
  a research node".
- **Why T3 stays OPEN.** Part I names neither B3 nor T3.
- **The implementation.** It follows T3's recorded reading: no node opens sea travel or a network edge
  type (`ResearchQuery.cs:529-532`).

**R4.13 — D-040 B4 (MAIN) and frozen D-009 — CONFLICT with Part I §18.**
- **The texts.**
  - D-040 B4: boats are "the same object as water routes … not a separate movement mode".
  - Frozen D-009: one multi-modal network (`d009-d010-map-population-addendum.md:12`, `:17`).
- **Part I §18.** Ships may move on open water and free positions, and movement may leave the roads.
- **Where it is held.** OPEN under CR-017 §5 and D-043 F12/F34 (UNMERGED-B).

**R4.14 — Frozen Spine S3 and D-006 against Part I §10.3 — OPEN.**
- **The frozen texts.**
  - Spine S3: global dt is set by "the world's *most advanced* polity's era band"
    (`civ-sim-architecture-v3-outline.md:34`).
  - D-006: calendar dt bands, with the authority rule applying "from M6 when eras diverge"
    (`m0-kernel-spec.md:13`, `:25`).
- **Part I §10.3.** Global dt is keyed to the Age cycle; the values are DEFERRED.
- **The open question.** Reading "era band" as "Age" is a reading of a frozen item.
- **Where it is held.**
  - On MAIN and PR #10: architecture §19 item 7.
  - On UNMERGED-B: DD-T7, and CR-017 §1 on Age as state.
  - No CR covers the dt binding itself. CR-019 is the next free number (measured, R7); it is not minted.

**R4.15 — The PathBuild labour bank (MAIN) — CONFLICT with Part I §15.**
- **The texts.**
  - Ratified M3 packet spec T3.8: "PathBuild banks the remainder" (`docs/t3.8-spec.md:44-45`).
  - `PathBuildSystem` (`Sim.Core/Systems/PathBuild/PathBuildSystem.cs:51-52`, `:157`, `:207`, `:216`).
- **Part I §15.** "Unused capacity disappears rather than becoming an indefinitely bankable stock."
- **Where it is held.** Recorded as D-043 F19, a DIRECT CONFLICT, on UNMERGED-B only, until this file.
- **INFERRED.** Path labour is construction capacity, because PathBuild draws the construction share.
- **Not banking (INFERRED, as D-043 F19 reads them).** Two remainders round a fraction of a *used*
  flow rather than keep unused capacity:
  - `HousingSystem`'s whole-unit `BuildRemainder`;
  - D-004's per-entity remainder accumulators.
- **Owner.** OPEN — the Director rules.

**R4.16 — DD-13 / DD-14 identifier collision — CONFLICT (documentation).**

| record | location | DD-13 means | DD-14 means |
|---|---|---|---|
| ADR-028 | MAIN | viability is hierarchical and type-specific | saturation is thresholds plus diminishing returns |
| D-043 | UNMERGED-B | generic current-Age effects | the Age Transition Surge model |

- **How it arose.** The ids were minted independently. D-043 measured both free on 2026-09-29;
  ADR-028 reached MAIN later.
- **How to cite them until resolved.** Always with their record, e.g. "ADR-028 DD-13".
- **PROPOSED, not applied.**
  - ADR-028's use stands, because it is on MAIN.
  - D-043's two ids are renumbered by an append-only note when that branch is reconciled with MAIN.

**R4.17 — Handoff, "Do NOT implement a conventional Civ-style technology tree" — HISTORICAL.**
- **Superseded in part by** D-044 R1–R4 and Part I §3–§6: two research trees over one graph.
- **What survives:**
  - no linear ladder (§6, §26 item 1);
  - no Age gate (§6.1);
  - realization stays separate (§4.1);
  - no CapabilitySystem (§2.3).

**R4.18 — Handoff, "Knowledge is M7" and the sequence M5 governing loop · M6 battle · M7 knowledge · M8 politics — NOT superseded.**
- **What it is.** The frozen-ladder reading that CR-005 questions for research. CR-005 is OPEN on MAIN and
  PR #10; only the unmerged `m5-full-build` marks it RULED (Option C) (R6.1).
- **Not ruled here.** Development-milestone placement (R6.1).
- **Superseded 2026-10-03 (Director roadmap rebase).** The sequence named in this item's heading (M6 battle, M7 knowledge) is superseded by the
  Director's roadmap rebase: M5 Governing Gameplay (current) · M6 Knowledge / Research / Technology · M7 Battle
  Layer · M8 Politics / Diplomacy · M9 Society · M10 Integrated Civilization Simulation · M11+ Depth & Content Expansion. The heading's
  "NOT superseded" status is historical as of this date. Part I's verbatim text is untouched. Record:
  `docs/milestones.md` §"Roadmap rebase 2026-10-03".

**R4.19 — Handoff, "10-year atomic turn" — HISTORICAL.**
- **What it describes.** The M4-era first dt band.
- **What governs now.**
  - The shipped kernel takes dt from calendar bands (D-006).
  - Part I §10.3 sets the target rule.
- **Still OPEN.** CR-006 (MAIN): the atomic turn against a mid-turn hand-back.

**R4.20 — Handoff repository state — STALE (measured 2026-10-02).**

| handoff says | measured |
|---|---|
| `main` = `dbef61a` | `main` = `93270cd`. `dbef61a` and `221f883` are ancestors of `main` |
| latest M4 branch `t4.19-glass-box` @ `221f883` | `t4.19-glass-box` has no remote ref |
| `m5-full-build` = `9a79d1e`, unmerged | unchanged |

**R4.21 — The ledger's path — SUPERSEDED.**
- **The earlier instruction.** It named `docs/architecture/director-architecture-and-decision-ledger.md`
  as mandatory reading.
- **What governs.** The Director's instruction of 2026-10-02 placed the canonical file at
  `docs/design/director-architecture-and-decision-ledger.md`.
- **On this branch.** `docs/architecture/` does not exist.

**R4.22 — The Director's conversational statements of 2026-10-01 — NOT RECORDED.**
- **What they are.** Rulings 2.1–2.21 and the Realization Architecture brief.
- **Status.** They are not in Part I and were not in the repository.
  - Part I §30 says relying on such statements is unsafe until they are recorded.
  - This file does not ratify them.
- **Where they are listed.** Gap audit §4, together with the OPEN items each would close.
- **Label differences from Part I.** Part I governs both (Director instruction, 2026-10-02):

  | statement | conversational wording | Part I §3 |
  |---|---|---|
  | 2.8, lens 1 | "KNOWLEDGE & TECHNOLOGY" | KNOWLEDGE |
  | 2.9, Tree 1 | "Knowledge & Technology" | Technology |

## R5. Other decisions in the attached handoff (September 2026)

These are recorded so that they are not lost. Use one only after verifying it against its own
repository record. "Not re-measured" means this pass did not check it.

| handoff statement | classification | repository record (location) |
|---|---|---|
| The economy belongs to the controlling polity; resources stay physically at settlements | RATIFIED (M4) | not re-measured |
| Tax is a policy, not an in-kind transfer: no treasury and no `Ledger.Transfer` for tax. CR-008 is CLOSED | RATIFIED; the record is on M5-BRANCH only | `docs/adr/cr-008-…md` on `m5-full-build`: "CLOSED — RULED ON BY THE DIRECTOR … REJECTED THE PREMISE" (measured). Absent from MAIN and PR #10 |
| Money is a separate future milestone | OPEN | not re-measured |
| CR-010, the institution definition | OPEN; never written | `docs/current-state.md:13` (measured) |
| CR-006, the atomic turn against a mid-turn hand-back | OPEN | `docs/adr/cr-006-…md:3` (MAIN; measured) |
| CR-014 is closed | RATIFIED | `docs/adr/cr-014-…md:3` (MAIN; measured) |
| CR-013 is closed; ADR-022 makes Linux x64 the reference platform | RATIFIED | `docs/adr/cr-013-…md:3`; `docs/adr/adr-022-…md:3` (MAIN; measured) |
| CR-003 stays quarantined; ship yield 26.0 | CALIBRATION ruling (M4) | `docs/adr/cr-003.md` (MAIN). The quarantine itself was not re-measured |
| Happiness is a derived, non-serialized query; migration influence w = 0.15 | RATIFIED (M4) | `Sim.Core/Observability/Explain/HappinessExplanation.cs` exists. The rest was not re-measured |
| Migration accepted as-is for M4; density band [0.15, 0.60] kept; founding demographics; founding food 21.5 years; artisan latch tests | CALIBRATION rulings (M4) | not re-measured |
| Colonies inherit founder control; `worldgen.aiEmpires` is configurable, default 0 | RATIFIED (M4) | not re-measured |
| The M5 governing loop is on `m5-full-build` and unmerged; D-021's valve rule binds it | RATIFIED for M5's scope; the branch is unmerged | `m5-full-build` @ `9a79d1e`, 174 commits behind MAIN (measured) |
| No God objects; the D-020 predicate DSL is real; no universal CapabilitySystem | RATIFIED | the same as Part I §2.3 |

## R6. Development milestones are not Age-progression milestones

### R6.1 Two meanings of "milestone"

D-043 F.4 records the term collision (UNMERGED-B).

**Development milestones** are the build ladder:
- The order M0→M11+ is frozen (`spine-s8-governance-freeze.md:17`).
- `CLAUDE.md:10` names the current milestone: M4.
- *2026-10-03 (Director roadmap rebase):* `CLAUDE.md:10` now names M5 Governing Gameplay as current (being finished),
  with M6 Knowledge / Research / Technology next and M7 Battle Layer after it.

**CR-005** (MAIN, OPEN) is about development milestones:
- **Its question.** Does placing a Research, Technology & Institutions architecture packet in M5 override
  the frozen ladder?
- **Its options.** A renumber, B insert, or C keep the order and add an architecture-only packet. It
  recommends C.
- **Its status.** OPEN on MAIN and PR #10. The unmerged `m5-full-build` carries a copy marked *"RULED
  2026-09-05 — OPTION C ACCEPTED"* (`cr-005-m5-research-technology-institutions-placement.md:3` there). D-044
  T1 already records that this and the research build's position cannot both land
  (`d044-research-progression-rulings.md:839`).
- **Part I §28** states when M5 may start. It does not answer CR-005's placement question.
- **CR-005 stays OPEN. This file does not close it, and it is not the Age-milestone decision.**

**Age-progression milestones** are in-game objects (Part I §10.4):
- They measure realized civilization development and drive Age transition.
- They are not development milestones.
- They are not research locks.
- They are not an eighth Tree.

### R6.2 The five categories

Part I §10.4 RATIFIES five categories:

1. Technological
2. Material-Economic
3. Institutional-Social
4. Systemic
5. Military Realization

Before this file, no repository record named them:
- **D-043 A2** (UNMERGED-B) rules the structure (core plus supporting milestones, with category
  coverage) but rules no category list. Its examples (military, economic, institutional, infrastructure,
  knowledge) do not match the five.
- **`Sim.Ui/UiContent/trees/ages.json`** (UNMERGED-B) carries four provisional categories: technological,
  material-economic, institutional-social and systemic. It has no Military Realization (measured;
  D-043 F27).
- **The corpus audit report** (PROPOSED) proposed the fifth (R4.6).

### R6.3 New identifier: AGE-MS-1

**AGE-MS-1 — AGE-MILESTONE OWNERSHIP AND EVALUATION. Status: OPEN — DIRECTOR DECISION REQUIRED.**

It is registered here for two reasons: no record holds the question, and the Director ruled on
2026-10-02 that CR-005 must not be reused for it.

**Scope:**
- **(a) Who publishes what.** Which owning systems publish the realized facts that each category's
  milestones read.
  - Part I §2.3 and §26 item 7 forbid a universal `ProgressionSystem` or `CapabilitySystem`.
  - INFERRED (D-043 F10): each domain publishes its own state.
- **(b) How coverage is judged.** How "category coverage" and a "sufficient" set of supporting
  milestones are evaluated, and by what.
- **(c) Military Realization.** How it is satisfied without counting researched military technology as
  realization (§10.4). It must be reconciled with frozen D-011, which owns units and formations.
- **(d) Age as an input.** Whether mechanisms read Age state. This is CR-017 §1 (OPEN, UNMERGED-B).

**Out of scope:**
- final milestone lists and thresholds (DEFERRED, §27);
- development-milestone placement (CR-005).

**Conversational input.** On 2026-10-01 the Director gave statement 2.21: a milestone-ownership
mapping by system, labelled "CR-005 milestone ownership" and truncated in the message as received. It
is recorded in gap audit §4 as pending. It is not applied.

## R7. Identifier register for this pass

The checks covered every remote-tracking ref and every local branch: 157 refs, measured 2026-10-02.

- **Minted:** AGE-MS-1 (R6.3). The string `AGE-MS-` occurred on no ref before this file.
- **Measured free, not minted:** D-047, ADR-031 and CR-019. The next decision record, ADR or CR takes
  these numbers unless another branch claims them first.
- **Collision found:** DD-13 and DD-14 (R4.16).
- **Not reused:**
  - CR-005, the development-milestone placement;
  - CR-009 and CR-010, reserved by reference and never written.

## R8. D-047 — the Director's rulings of 2026-10-02 (append-only pointer)

- **Record:** `docs/d047-civ6-reference-and-progression-rulings.md` holds the Director's rulings 1–18 of
  2026-10-02 verbatim (Part A), with Civilization VI as the default design reference.
- **Part I is not edited.** The Director's text between the BEGIN and END markers is unchanged; its SHA-256 was
  re-measured after this annex was added: `4c18b3b9365118325f9c3a7d009bfdb38e7ca0c097c4f1745b40a9c91872b8a2`.
- **What it settles that these annexes listed as open:** AGE-MS-1 (R6.3) is **RATIFIED** (ruling 12): owning
  systems publish milestone facts in the five categories and a thin evaluator reads them. D-044 Part D T3 is resolved
  (rulings 7 and 8: research unlocks transport and infrastructure CLASSES; it constructs nothing). Age advancement is
  an explicit player/AI choice effective from the next turn (ruling 13). Completion overflow is LOST (the 2.2 freeze,
  recorded by D-047 Part B). The full closure table is D-047 Part B; the gap audit's §9 marks each G-item.
- **R7 update:** D-047 is now minted. ADR-031 and CR-019 remain free.
