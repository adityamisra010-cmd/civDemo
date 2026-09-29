# D-043 — CIVILIZATION PROGRESSION, AGES, THE TREES AND MOBILE AGENTS

**Director design ruling, 2026-09-29.** Decision record — exempt document class under S8 §4.
Extends D-038 Part H, D-039, D-040 and D-042 on the subjects below. **This record is not an
architecture addendum. It binds no frozen document and amends none.** Where a ruling collides
with a frozen item (S8 §1), the collision is filed as **CR-017** (OPEN). Until CR-017 is ruled,
applying that ruling to the frozen item is **DIRECTOR DECISION REQUIRED**
(`spine-s8-governance-freeze.md:33`, `:37`).

**DESIGNS NO MECHANISM.** No storage, no constant, no equation, no schema. No production code,
test, JSON content, golden or frozen document was changed by this record.

**Citations verified against the tree at `096677e`** (branch `claude/civdemo-work-b1z2y4`,
LOCAL and REMOTE, **not MAIN**; 18 commits ahead of `origin/main` `de5e00e`, 0 behind).
- Conflicts with existing documents are recorded in **PART F**, NOT rewritten.
- Previously open questions this record answers are mapped in **PART G**.
- Disagreements between the dictation and the tree are in **PART H** (§7.12: the tree wins).

**Provenance note.** The Director dictated these rulings in one message on 2026-09-29. Each is
quoted **verbatim** below, and the section numbers 1–23 are the dictation's own. The labels follow
house terminology (`docs/design/m4-closure-audit.md:32-37`;
`docs/design/recovered-decisions-architecture-invariants.md:27-28`):

- **RATIFIED.** Every dictated **LOCKED** ruling is a ratified Director ruling, cited to this
  record. "LOCKED" is not house vocabulary; in the tree it is a UI node state
  (`Sim.Ui/UiContent/trees/the-trees.json:24`).
- **DEFERRED** with **DIRECTOR DECISION REQUIRED.** The dictated **IMPORTANT DIRECTOR HOLD** (§7)
  is recorded this way, with the explicit entry **DD-13** that §7 asks for.
- **DEFERRED.** The §23 "do not implement" items are recorded this way (PART E).
- **INFERRED** and **PROPOSED.** Anything the filing agent adds is labelled INFERRED (reasoned,
  not stated) or PROPOSED (a suggestion for the Director). Such additions are never RATIFIED.
- **DIRECTOR DECISION REQUIRED.** A claim that rests on an OPEN CR takes this label
  (`docs/design/ratified-label-audit.md:48-49`). The application of a ruling to a frozen item
  named in CR-017 is therefore labelled this way, even though the ruling itself is RATIFIED.

Two register ids are minted here as the explicit entries the dictation orders (§7 asks for one).
They narrow the existing DD-04(ii) (`docs/architecture/pre-m5-repository-audit.md:373`) rather
than replace it: **DD-13** (generic current-Age effects, §7) and **DD-14**
(the Age Transition Surge model, §5). **Measured: neither id, nor D-043 or CR-017, occurs on any
of the 85 local and remote refs** (`git grep` over `refs/heads` and `refs/remotes`, 2026-09-29).
The audit's §5 table is a frozen investigation record and is not edited.

The dictation opened with three instructions. They bind the whole record and are recorded
verbatim:

> DO NOT implement gameplay systems yet.
>
> Your task is documentation and architecture-record maintenance only.
>
> Use the existing progression architecture documents and current ADR/provenance conventions. Preserve prior ratified decisions. Do not silently modify existing ratified architecture.

---

## PART A — AGES, MILESTONES AND THE TREES (dictation §1–§8)

### A1. AGE STRUCTURE — **RATIFIED** (dictation §1)

> LOCKED:
>
> There are exactly 9 Ages:
>
> A1 Prehistoric / Stone Age
> A2 Neolithic / Agricultural
> A3 Bronze Age
> A4 Iron Age
> A5 Classical / Imperial
> A6 Medieval
> A7 Early Modern
> A8 Industrial
> A9 Modern / Contemporary
>
> Ages are civilization-development states, not date brackets.
>
> Different civilizations may occupy different Ages simultaneously.
>
> Age progression is irreversible.
>
> Do not reinterpret Ages as calendar eras.

*Where it touches the tree:*
- The shipped placeholder has six unnamed Ages; that divergence is F25, and the Ages test is F26.
- Several names are already taken: D-006's date bands and the "era" registers use them (term table
  F.4; F40–F41).
- A successor polity's Age is unruled: F17.
- Whether an Age may be READ by mechanisms is CR-017 §1. This clause alone does not make it an
  input.

### A2. AGE TRANSITION REQUIREMENTS — **RATIFIED** (dictation §2)

> LOCKED:
>
> An Age transition requires:
>
> A. Mandatory Core Milestones
> - all required Core milestones must be satisfied.
>
> B. Supporting Milestones
> - supporting milestones are divided into meaningful categories.
> - multiple alternative milestones exist within categories.
> - the civilization must satisfy sufficient supporting requirements with coverage across multiple categories.
> - the player cannot satisfy an Age merely by repeatedly accumulating milestones from one easy category.
> - different civilization playstyles should be able to reach the same Age through different combinations of supporting milestones.
>
> Milestones represent actual civilization conditions, not abstract generic Age points.
>
> Example principle:
> An Iron Age may have a mandatory ironworking/metallurgy core, while supporting requirements could include alternative military, economic, institutional, infrastructure, knowledge, etc. developments.
>
> Do not invent final milestone lists in this task.

*Where it touches the tree:*
- The placeholder milestone schema cannot express "sufficient", "coverage across multiple
  categories" or "multiple alternative milestones exist within categories". Its categories also
  differ from §2's examples (F27).
- The dictation says "Mandatory Core Milestones", so the shipped UI's "Mandatory" matches it.
- The final milestone lists are DEFERRED (PART E).

### A3. MILESTONES ARE PLAYER-VISIBLE — **RATIFIED** (dictation §3)

> LOCKED:
>
> Milestones are visible to the player.
>
> The UI should eventually expose:
> - Core milestones
> - Supporting milestone categories
> - achieved/unachieved status
> - alternative pathways
> - remaining requirements for the next Age
>
> Milestones are NOT an eighth Tree.
>
> The Trees remain the seven player-facing progression lenses:
>
> Knowledge
> Techniques
> Institutions
> Infrastructure
> Industry
> Military
> Applications
>
> Milestones are underlying progression objects that are surfaced in the Age/progression UI.

*Where it touches the tree:*
- The seven Trees match the shipped lenses exactly (PART H, H2).
- The shipped Age view shows Mandatory and Supporting counts. It does not yet show category
  coverage, alternative pathways or remaining requirements (F28).
- The Trees graph carries a `milestone` node type inside the INDUSTRY lens (F28).

### A4. PARTIAL AGE PROGRESS — **RATIFIED** (dictation §4)

> LOCKED:
>
> Partial completion of an Age's milestone requirements produces NO generic partial Age modifier.
>
> Example:
> 70% completion does not mean 70% of an Age bonus.
>
> The actual systems provide the benefits of the individual developments:
> research, techniques, institutions, infrastructure, industry, military, applications, etc.
>
> Age transition is a discrete state transition.

*Where it touches the tree:*
- The shipped per-Age progress percentage is a display with no effect. This ruling forbids an
  effect, not a display. What the percentage means stays DD-T6 (F29).
- Whether a discrete Age flip is an "instant transformation" is recorded as a weak tension in
  CR-017 §4.

### A5. AGE TRANSITION SURGE — **RATIFIED** (dictation §5)

> LOCKED:
>
> Every civilization receives its own Age Transition Surge when it enters a new Age.
>
> The surge is not exclusive to the first civilization reaching an Age.
>
> The surge:
> - begins immediately upon Age transition
> - ramps upward
> - reaches a civilization-specific maximum
> - remains elevated around its peak for several turns
> - then gradually tapers
> - eventually approaches approximately 50% of its maximum as a residual effect
> - primarily scales in magnitude with civilization scale/development
> - larger civilizations may take slightly longer to reach peak, but duration should not scale dramatically
> - may have Age-specific weighting
>
> Broad affected outputs include:
> - Research
> - Construction
> - Resources
> - Industry
> - Food
> - Population productivity
> - Trade/economic activity
> - Infrastructure development
> - Military production/training
>
> Initial balancing target:
> approximately +20% around peak, subject to later tuning.
>
> Do NOT implement the exact formula yet.
>
> Do NOT hardcode an arbitrary decay curve yet.
>
> The Director will define the final mathematical model after the surrounding economic/research/institutional systems exist.

**DD-14 — THE AGE TRANSITION SURGE MODEL. Status: DEFERRED. DIRECTOR DECISION REQUIRED.**
- **Ruled:** the shape (above).
- **Deferred:** the formula, the decay curve and the Age-specific weights. The dictation reserves
  them for the Director "after the surrounding economic/research/institutional systems exist".
- **Bound by:**
  - CR-017 §2 (the ~50 % residual against Law 2);
  - CR-017 §3 ("several turns" against Law 3);
  - CR-017 §1 (the surge's trigger is Age entry);
  - F9, F10 and F24 (closed-record constraints).
- Mirrored in `docs/queue.md`.

*Where it touches the tree:*
- "Surge" already names a migration phenomenon (term table F.4).
- The shipped per-Age `transitionEffect` slot is a presentation animation, not this surge (F30).

### A6. PERMANENT CIVILIZATION DEVELOPMENT — **RATIFIED** (dictation §6)

> LOCKED:
>
> Civilization development is cumulative and historical.
>
> Knowledge, techniques, infrastructure, institutions, applications, etc. do not disappear merely because a civilization enters a new Age.
>
> Example:
> Irrigation developed in an earlier Age remains available and continues affecting agriculture in later Ages unless the relevant infrastructure/capability actually degrades, is destroyed, abandoned, etc.
>
> Do not implement "old Age bonuses disappear when entering a new Age."
>
> Current Age may change ongoing conditions such as:
> - consumption patterns
> - productivity
> - resource demand
> - institutional possibilities
> - available techniques
> - military organization
> - economic structure
>
> But these changes must operate through the underlying simulation rather than pretending that earlier capabilities were erased.

*Where it touches the tree:* this clause conflicts with no frozen item (CR-017 §8). It bears on
three existing documents:
- the Trees schema's `ageRange.to` (F31);
- the M5 placeholder's obsolescence item (F31);
- the design corpus's "forgetting" proposal (F31).

"Current Age may change ongoing conditions" is an Age-as-input statement, bounded by "through the
underlying simulation"; it goes to CR-017 §1.

### A7. CURRENT AGE GENERIC EFFECTS ARE DEFERRED — **DEFERRED · DIRECTOR DECISION REQUIRED** (dictation §7)

> IMPORTANT DIRECTOR HOLD:
>
> Do NOT implement a generic table such as:
>
> Industrial Age = +20% production
> Medieval Age = +10% food
> etc.
>
> Generic Age-state effects are intentionally deferred until the surrounding systems have been developed.
>
> The intended philosophy is:
>
> Age
> → changes possibilities and civilization conditions
> → Research / Institutions / Infrastructure / Industry / Population / Military etc.
> → actual consequences
>
> Revisit generic Age effects later after the relevant systems exist.
>
> Create an explicit TODO/Director Decision Required entry so this cannot be forgotten.

**DD-13 — GENERIC CURRENT-AGE EFFECTS. Status: DEFERRED. DIRECTOR DECISION REQUIRED.** This is
the explicit TODO that §7 orders, "so this cannot be forgotten".

- **What is held.** Any generic per-Age effect table. The dictation's examples are "Industrial
  Age = +20% production" and "Medieval Age = +10% food". **No agent may implement one.**
- **The intended philosophy (RATIFIED, quoted above).** Age → changes possibilities and
  civilization conditions → Research / Institutions / Infrastructure / Industry / Population /
  Military etc. → actual consequences.
- **When to revisit.** "after the relevant systems exist" (verbatim). INFERRED, non-exhaustive
  examples: knowledge/research, institutions, infrastructure, industry, population, military, etc.
- **What the revisit must first settle.**
  - CR-017 §1: whether the Age may be an input at all.
  - CR-017 §2: whether any Age-keyed effect can pass Law 2.
- **INFERRED.** A generic table is also the shape that frozen D-018 forbids for needs
  (`d018-classes-and-needs.md:11`, "No era weight tables") and that CR-001 rejected for rates
  (`docs/adr/cr-001-dt-fragile-demography.md:3-5`).
- **Where it is recorded.** Here; `docs/queue.md` (appended entry); `docs/architecture/ages-ui.md`
  §7.

### A8. AGE TRANSITION UNLOCKS — **RATIFIED** (dictation §8)

> LOCKED:
>
> Entering a new Age can unlock:
> - new buildings
> - new institutions
> - new infrastructure
> - new units
> - new techniques
> - new applications
> - other Age-appropriate capabilities
>
> Existing obsolete military units can automatically upgrade to their appropriate successor unit upon Age transition.
>
> Example:
> Crossbowmen → Riflemen
>
> The upgrade:
> - is automatic
> - is free
> - does not require the player to manually rebuild the unit
> - should eventually use historically/causally reasonable changes in attack, health, range, etc.
> - must not be implemented as arbitrary doubling of stats merely because the unit belongs to a later Age
>
> The final military-stat model will derive from the underlying military capability architecture.
>
> Keep the distinction:
>
> Research unlock
> → capability becomes known/possible
>
> Industrial/institutional/personnel conditions
> → capability can be realized
>
> Age transition / unit modernization
> → existing obsolete formations can be automatically converted into the newly appropriate formation

*Where it touches the tree:*
- **Age-transition unlocks** are the Age-as-input case of CR-017 §1. They also run into the closed
  D-040 B3 ruling, "No era gate, no date, no unlock" (F8).
- **"Free" automatic modernization** is CR-017 §4 (Law 1, Law 3 and D-011 §2's equipment).
- **The research-unlock half** of the three-way distinction must be squared with D-040 B3's
  "NO TECHNOLOGY UNLOCK" (F8).
- **The Trees content** has no obsolete state and no successor relation (F32).

---

## PART B — MOBILE AGENTS (dictation §9–§16)

### B1. UNIVERSAL MOBILEAGENT — **RATIFIED** (dictation §9)

> LOCKED:
>
> Create a universal MobileAgent architectural concept.
>
> All MobileAgents have common state such as:
> - stable ID
> - owner
> - position
> - movement capability
> - Action Capacity
> - current action state
> - domain/type
> - capabilities
> - lifecycle/status
>
> Specific agent types provide their own actions.
>
> Examples:
> - Army
> - Battalion
> - Tank formation
> - Scientist
> - General
> - Artist
> - Cultural group
> - Rock band
> - other special people/groups
>
> Do not finalize visual representations yet.

*Where it touches the tree:*
- **MEASURED:** no MobileAgent state exists in `Sim.Core`.
- **The only per-person row is `NotableRow`,** which carries identity, place, allegiance and the
  person, and nothing else (`Sim.Core/State/WorldState.cs:630-636`).
- **The world layer:** reports notables as settlement-attached "person" agents and treats "owner"
  as a demo-only relation (F33).
- **"General"** now names four objects (term table F.4).

### B2. ACTION CAPACITY — **RATIFIED** (dictation §10)

> LOCKED:
>
> All MobileAgents have Action Capacity.
>
> The budget mechanism is universal.
>
> The action set is agent-specific.
>
> Do not create a giant universal action list.

*Where it touches the tree:*
- **The refresh interval** (§16) against Law 3 is CR-017 §3.
- **INFERRED, CR-017 §8:** the universal budget can be reconciled with the kernel's
  one-owner-per-table rule and the fixed order-record shape.

### B3. SPECIAL PEOPLE — **RATIFIED** (dictation §11)

> LOCKED:
>
> Historical special people are temporary MobileAgents.
>
> They:
> - can be recruited by any player/AI player capable of recruiting them
> - do not need to belong to their historical nationality
> - are owned by whoever recruits them
> - have predefined skills
> - have predefined abilities
> - have finite lifespans
> - eventually die/disappear/retire
> - do not require persistent relationship/allegiance simulations
>
> Example:
> Germany may recruit Gandhi.
>
> Do not implement the final historical-person catalogue yet.
>
> Future content architecture should separate:
> A. Special Person definitions
> B. Ability definitions

*Where it touches the tree:*
- **Against frozen D-010's notables and Law 1:** CR-017 §7.
- **Against the ratified M4 notables ruling R-1** and its shipped lifecycle: F14.
- **Against D-037 A1 "NOTHING SPAWNS":** F13.
- **Against D-042 §6.1 on individual people:** F15.
- **INFERRED, `CLAUDE.md:17`:** an Ability definition written as a standing output bonus would be
  the free-floating buff that Law 2 bans. Abilities must act as coefficients inside resolution
  equations.

### B4. KNOWLEDGE OWNERSHIP — **RATIFIED** (dictation §12)

> LOCKED:
>
> Knowledge belongs to the civilization/polity.
>
> Individual scientists or other agents do not permanently own civilization knowledge.
>
> Individuals affect the rate/mechanism of knowledge generation and application.
>
> If a scientist dies, civilization knowledge does not disappear merely because the individual died.

*Where it touches the tree:*
- This clause is consistent with D-042 §9.2, *"Knowledge is ultimately **Empire-scoped**"*
  (`d042-empire-and-player-control-addendum.md:165-166`).
- It conflicts with no frozen item (CR-017 §8).
- The design corpus's holder-death model rests on premises this clause overturns (F43).

### B5. ARMIES — **RATIFIED** (dictation §13)

> LOCKED:
>
> Armies are MobileAgents with internal composition.
>
> An Army may contain formations/battalions such as:
> - infantry
> - cavalry
> - tanks
> - etc.
>
> Formations may be split into separate MobileAgents.
>
> Separate formations may be merged into an Army.
>
> The world visualization may represent an Army as one visible agent while the simulation retains internal composition.

*Where it touches the tree:*
- **Consistent with frozen D-011's atomic formations:** "**Formations are atomic:** warband →
  phalanx/cohort → tercio → line battalion → armored company → air wing"
  (`d011-battle-layer-addendum.md:36`). A split happens at formation boundaries (INFERRED).
- **Not settled here:** where strategic armies, the AutoResolver and sieges are placed (DD-10,
  PART G).
- **Fog:** when one Army token hides formations observed unequally, D-039 Part C's per-formation
  fog states must still be honoured (F12, INFERRED).

### B6. CULTURAL GROUPS — **RATIFIED** (dictation §14)

> LOCKED:
>
> Cultural groups such as bands are MobileAgents.
>
> They can:
> - move
> - perform
> - generate revenue
> - generate culture
> - generate diplomatic effects
> - affect loyalty
> - potentially generate unrest
> - possess skills
> - consume Action Capacity
> - eventually disband/end

*Where it touches the tree:* each listed effect must enter through a channel the corpus already
rules. The collisions are with:
- loyalty (D-040 C3, D-037 E1, D-041 C1);
- revenue (D-042 §10.3);
- unrest (frozen D-010's riot-propensity inputs);
- culture (a bucket identity dimension in the frozen Spine);
- diplomacy (interest-computed in the frozen Spine).

These are recorded as F18 and CR-017 §8. Where the members come from is F13.

### B7. MOVEMENT — **RATIFIED** (dictation §15)

> LOCKED:
>
> MobileAgents can move off-road.
>
> Terrain and infrastructure modify movement/action cost.
>
> Examples:
> - roads are efficient
> - plains have normal cost
> - forests have higher cost
> - hills have higher cost
> - mountains have very high cost
> - deserts have appropriate movement/supply penalties
>
> Future movement can also account for:
> - transport
> - weather
> - supply
> - fatigue
> - experience
> - doctrine
> - enemy territory
>
> Movement remains continuous x/y.

*Where it touches the tree:*
- **Frozen D-009/D-010** put army movement on the network graph: CR-017 §5.
- **"Remains continuous x/y"** is not a fact about the simulation: PART H, H1.
- **The shipped movement-cost raster** has slope and water but no forest, hill class or desert
  term (F35).
- **Sea movement** under D-040 B4: F12.

### B8. WAR PULSE — **RATIFIED** (dictation §16)

> LOCKED:
>
> Normal world:
> - Action Capacity refreshes according to the strategic turn.
>
> During active war:
> - War Pulses operate inside the strategic turn.
> - Involved military agents receive Action Capacity according to the War Pulse cadence.
> - Battles, movement, retreats, territorial changes, etc. can occur within War Pulses.
> - Construction and normal civilization processes continue during war.
>
> Do not implement War Pulse mechanics in this documentation task.

*Where it touches the tree:*
- **The kernel contract, the sub-step rule and D-011:** CR-017 §6, which should be ruled with
  CR-006 §1.
- **The Action Capacity refresh interval:** CR-017 §3.
- **D-039 E3's "NOT A FINER-GRAINED TURN":** F11.
- **"Pulse"** already names D-011's battle command pulse (term table F.4).

---

## PART C — CONSTRUCTION CAPACITY AND VIABILITY (dictation §17, §20)

### C1. CONSTRUCTION CAPACITY — **RATIFIED** (dictation §17)

> LOCKED:
>
> Construction capacity is generated per turn.
>
> Unused capacity disappears.
>
> It is NOT bankable.
>
> Example:
> 10 capacity generated
> 4 used
> 6 disappear
> next turn generates fresh capacity
>
> No automatic accumulation of unused construction capacity.
>
> Player allocation/prioritization can redistribute current productive capacity.

*Where it touches the tree:*
- **Consistent with the shipped M4-D construction gate** (PART H, H3; CR-017 §8).
- **Conflicts directly with the shipped PathBuild labour bank** and its ratified M3 packet spec
  (F19).
- **Placeholder construction progress** that builds up over turns: F42.

### C2. INFRASTRUCTURE / INSTITUTION VIABILITY — **RATIFIED** (dictation §20)

> LOCKED ARCHITECTURAL PRINCIPLE:
>
> Do not use arbitrary universal hardcoded limits such as:
> "maximum 5 universities per city."
>
> Use causal viability/capacity.
>
> The model should consider, where relevant:
>
> 1. Physical viability
>    - terrain
>    - water
>    - deposits
>    - coastline
>    - river geometry
>    - etc.
>
> 2. Local capacity
>    - population
>    - skilled workforce
>    - supporting infrastructure
>    - land/facility capacity
>    - economic support
>    - demand
>
> 3. Marginal returns
>    - additional instances can have diminishing returns
>
> 4. Genuine physical/site constraints
>    - finite dam sites
>    - finite viable ports
>    - finite deposits
>    - finite practical infrastructure locations
>    - etc.
>
> The architecture should support type-specific viability rules.
>
> Do NOT create a universal CapabilitySystem that owns all of these rules.
>
> Do NOT invent final formulas yet.

*Where it touches the tree:*
- This clause restates D-042 §7.3, *"Do not create a universal God system such as a
  `CapabilitySystem`"* (`d042-empire-and-player-control-addendum.md:141-142`).
- It conflicts with no frozen item (CR-017 §8).
- Together with §19, it implies institutions are countable instances per settlement and type.
  That bears on DD-11 / CR-010 (PART G) and on D-038 H2's "capability" wording (F21).

---

## PART D — SETTLEMENTS AND WORLD VISUALS (dictation §18, §19, §21, §22)

### D1. SETTLEMENT VISUAL CLASS — **RATIFIED** (dictation §18)

> LOCKED:
>
> Settlement visual scale/class is primarily determined by population.
>
> Institutions, infrastructure, industry, etc. determine settlement contents and morphology rather than defining whether the settlement is a town/city.

*Where it touches the tree:*
- **The live world layer** stages settlements by `SizeTier`, a step computed from dwellings, and
  forbids population from staging a live settlement (F36).
- **D-017 and D-038 H3** tie the footprint to a size stock grown from population AND construction
  (F20).

### D2. BUILDING VISUALIZATION — **RATIFIED** (dictation §19)

> LOCKED:
>
> Buildings/institutions are visually embedded within the settlement representation.
>
> They are not required to exist as individually placed visible building objects on the map.
>
> Example:
> A settlement with a university displays a university visual somewhere within the settlement footprint.
>
> Morphology remains cumulative and data-defined.
>
> University example:
>
> 1 → university_small
> 10 → university_expanded
> 20 → university_campus
> 30 → university_complex
>
> At high zoom, the visual can display the number associated with the aggregated institution.
>
> Do not turn every simulation institution into an individually rendered building.

*Where it touches the tree:*
- **The shipped layer** draws up to four same-type institutions per settlement individually, each
  staged by its own reported capacity (F37).
- **The same numbers, 1/10/20/30,** are pinned in a shipped test as civilization-wide summary
  counts, not per-settlement stage thresholds (F37).
- **Answered in part:** WV-02, WV-03 and D-038 H8 (PART G).

### D3. LONG-TERM VISUAL DEVELOPMENT — **RATIFIED** (dictation §21)

> LOCKED:
>
> World visuals should evolve gradually.
>
> Individual turns may show small/negligible changes.
>
> Across approximately 10–15 turns, accumulated visual change should become clearly noticeable.
>
> This applies to:
> - settlements
> - campuses
> - industrial areas
> - infrastructure
> - other civilization morphology

*Where it touches the tree:*
- **Turn length varies twentyfold** (dt 10 → 0.5 sim-years), so "10–15 turns" is 100–150
  sim-years early in the game and 5–7.5 late.
- **The only live settlement stage** changes "a handful of times per campaign" (F38).
- **INFERRED:** a presentation target, not a simulation rate (CR-017 §8).

### D4. PROCESS ANIMATIONS — **RATIFIED** (dictation §22)

> LOCKED:
>
> Short-duration processes such as construction/training can receive procedural animations when initiated/resolved.
>
> They do not require multi-turn persistent construction animation.
>
> MobileAgent movement DOES require actual continuous movement animation.

*Where it touches the tree:*
- **Against the unratified animation convention:** it allows "three kinds, nothing else" and none
  of them is movement. It also bans sub-turn implications (F39).
- **Against the Trees amendment's steady loops:** these are "not required", which is not the same
  as forbidden (F39).
- **Against the world layer's "no movement":** F39.
- **Against D-038 C1/C3 people tokens:** F22.
- **Answered in part:** DD-T1 (PART G).

---

## PART E — WHAT THIS DOES NOT DO

> Do not implement:
> - Age formulas
> - Age generic effects
> - exact transition surge formula
> - final milestone catalogues
> - final research-point formulas
> - final university effects
> - final infrastructure viability equations
> - final special-person catalogue
> - final unit art
> - final MobileAgent art
> - War Pulse mechanics
> - combat equations
>
> This task is documentation/architecture-record maintenance only.

All twelve items are **DEFERRED**. The dictation also ruled on two of them:

| item | status | where it is held |
| --- | --- | --- |
| Age generic effects | DEFERRED · DIRECTOR DECISION REQUIRED | **DD-13** (A7) |
| exact transition surge formula | DEFERRED · DIRECTOR DECISION REQUIRED | **DD-14** (A5) |
| Age formulas; final milestone catalogues | DEFERRED | A1/A2; DD-T6 (the-trees-ui §16) |
| final research-point formulas | DEFERRED | DD-T4; DD-05 |
| final university effects; final infrastructure viability equations | DEFERRED | C2; DD-11 / CR-010 |
| final special-person catalogue | DEFERRED | B3; CR-017 §7 |
| final unit art; final MobileAgent art | DEFERRED | B1 ("Do not finalize visual representations yet"); D-038 E1/F3/H7; X-VIS-1 |
| War Pulse mechanics; combat equations | DEFERRED | B8; CR-017 §6; D-011 |

**This record also does not:**

- **amend any frozen document.** Untouched: the Spine, S8, `m0-kernel-spec.md`, D-009/D-010,
  D-011, D-018 and `CLAUDE.md`. Every collision is filed in CR-017, OPEN.
- **write an ADR.** S8 §3 puts the ADR after the ruling: *"Director rules → ADR records the
  ruling"* (`spine-s8-governance-freeze.md:37`). ADR-028 is the next free number.
- **amend any post-freeze decision record, CR, ADR or milestone spec.** Untouched: D-021, D-035,
  D-037…D-042, the M3 and M4 specs, the M4 exit inventory, CR-001…CR-016 and every ADR. Each
  conflict with them is recorded in PART F.
- **edit any register row.** This covers the audit §3/§5, the-trees-ui §16, ages-ui §6,
  world-visualization §13 and D-038 H8. Status notes are appended to the living documents instead
  (see the file list at the end).
- **place anything on the milestone ladder.** Ages, MobileAgents, War Pulses, special people,
  cultural groups and the surge all stay off it. The ladder order is frozen
  (`spine-s8-governance-freeze.md:17`); CR-005 is OPEN; and the M4 exit certified "No
  AutoResolver and no armies" (`docs/milestones.md:326`).
- **change any UI content, code or test,** including those that now diverge (PART F.3).
- **write a naming convention.** CONV-2 is the next free number. Term collisions are listed in
  F.4 only, heeding CONV-1's caution that a convention which pre-empts an open design ruling
  is worse than none (`conv-1-term-namespacing.md:84-85`).
- **choose between CR-017's options.**

---

## PART F — CONFLICTS RECORDED FOR DIRECTOR RESOLUTION, NOT REWRITTEN

**No document below was edited.** Every item carries **Status: UNRESOLVED. Owner: director.**
unless it says otherwise.

Each item is classed as one of:
- **DIRECT CONFLICT** — the two texts cannot both hold;
- **TENSION** — both can hold under a reading that needs the Director's confirmation;
- **DIVERGES** — shipped content or code differs from the ruling;
- **TERM** — the same word names different things.

The candidates came from an independent sweep of the tree at `096677e`: 197 candidates, each
checked by a separate verifier that tried to refute it. Items the verifiers refuted are omitted.
Duplicates are merged.

### F.1 Frozen items (S8 §1) — filed as CR-017, summarised here

| id | ruling | frozen item | CR-017 |
| --- | --- | --- | --- |
| F1 | §5, §6, §7, §8 (Age read by mechanisms) | Law 4, `civ-sim-architecture-v3-outline.md:22`, `CLAUDE.md:19`; `d018-classes-and-needs.md:8`, `:11` | §1 |
| F2 | §5 (the ~50 % residual; "Age-specific weighting") | Law 2, `civ-sim-architecture-v3-outline.md:20`, `CLAUDE.md:17`; `d018-classes-and-needs.md:11` | §2 |
| F3 | §5 ("several turns"), §16 (Action Capacity per strategic turn) | Law 3, `CLAUDE.md:18`, `civ-sim-architecture-v3-outline.md:21`, `m0-kernel-spec.md:70` | §3 |
| F4 | §8 (automatic, free modernization) | Law 1, `civ-sim-architecture-v3-outline.md:19`; Law 3, `:21`; D-011 §2 equipment, `d011-battle-layer-addendum.md:26-27` | §4 |
| F5 | §15 (off-road, continuous x/y) | D-009/D-010, `d009-d010-map-population-addendum.md:17`, `:19`, `:51` | §5 |
| F6 | §16 (War Pulses inside the strategic turn) | kernel §3.2/§3.4, `m0-kernel-spec.md:66`, `:70`; sub-step rule, `civ-sim-architecture-v3-outline.md:33`; `d011-battle-layer-addendum.md:34` | §6 (with CR-006 §1) |
| F7 | §11 (catalogue special people) | D-010 notables, `d009-d010-map-population-addendum.md:29`; Law 1, `CLAUDE.md:16` | §7 |

### F.2 Closed decision records, ruled CRs and ratified milestone records (not S8-frozen in the §1 sense — whether a post-freeze D-record is frozen is itself open, `docs/design/arch-PQ-conflicts-and-questions.md:854`, Q-73)

**F8 — Age-transition unlocks against D-040 B3, D-039 A5 and D-042 §8.4 (§8; also §5, §7).
DIRECT CONFLICT, conditional on CR-017 §1.**
- **The closed texts.**
  - D-040 B3 rejects the unlock shape in so many words. It collects three prior refusals: *"The
    tree already refuses this shape in three places — "Classes emerge, they are not unlocked",
    "LAW 4 FORBIDS CALENDAR GATES — a tier cannot unlock by era or date", "No era gate, no date,
    no unlock.""* (`d040-discovery-and-control.md:62-64`).
  - B3's heading is **"NO TECHNOLOGY UNLOCK"** (`:59`). That cuts against §8's "Research unlock →
    capability becomes known/possible".
  - D-039 A5: *"Era is a consequence, never an input"* (`d039-command-fog-and-siege.md:39-40`).
  - D-042 §8.4: capability definitions *"consume computed state, never hard-coded calendar
    unlocks"* (`d042-empire-and-player-control-addendum.md:157-158`).
  - The queue's Director-input item Q-D: *"a tier cannot unlock by era or date"*
    (`docs/queue.md:527-529`).
- **If CR-017 §1 rules that an Age is computed state (option A),** D-042 §8.4 is satisfied, and
  B3's rejection narrows to an unlock that does not follow from computed state.
- **Otherwise,** §8 contradicts all four texts.
- **The research half needs its own statement.** Does "known/possible" differ from B3's rejected
  "tech-tree node opening sea travel"? INFERRED: it does if realization still requires computed
  conditions, which is §8's own second step.

**F9 — The surge against D-042 §5.3, D-040 C3 and D-035-C (§5). TENSION.**
- **The closed texts.**
  - D-042 §5.3: *"Avoid hard-coding governments as bundles of direct bonuses. (This aligns with
    Law 2 …)"* (`d042-empire-and-player-control-addendum.md:90-91`). This is the closest
    analogue: a civilization-level state change must not be expressed as a bundle of direct
    bonuses.
  - D-040 C3 rejected a decaying double *"attached to a place rather than a consequence of
    anything"* (`d040-discovery-and-control.md:105-107`).
  - D-035-C: *"Name the physical carrier … If none exists, it is an invented modifier and is
    refused"* (`d035-needs-aggregation.md:91-92`).
  - CR-001 rejected a per-era rate table (`docs/adr/cr-001-dt-fragile-demography.md:3-5`).
- **Why it collides.** The surge is a direct uplift across nine outputs, keyed to Age entry. It
  names no carrier, and it has Age-specific weights.
- **Resolved with** CR-017 §2 and DD-14.

**F10 — One system applying the surge to nine domains, against D-042 §7.3 (§5; also §2). TENSION,
INFERRED.**
- **The closed text.** *"Do not create a universal God system such as a `CapabilitySystem` that
  owns every capability or coordinates every domain"*
  (`d042-empire-and-player-control-addendum.md:141-142`).
- **Why it collides.** A single "Age system" that applied the surge to Research, Construction,
  Food and the rest would be that coordinator. So would one that evaluated milestones across all
  categories.
- **The reading that avoids it.** Each domain applies its own share, reading published state.

**F11 — War Pulses against D-039 E3, D-042 §11 and the M4 exit fence (§16; also §10).
TENSION, strong: it needs an explicit Director statement.**
- **The closed texts.**
  - D-039 E3: *"THE CAMPAIGN LAYER IS STRATEGIC DECISIONS BETWEEN BATTLES, NOT A FINER-GRAINED
    TURN. Ruled explicitly: the player does not play out months of movement."*
    (`d039-command-fog-and-siege.md:143-144`).
  - D-042 §11 requires any mid-turn control to *"preserve deterministic replay"* and be *"an
    explicit temporal/control architecture"*
    (`d042-empire-and-player-control-addendum.md:190-194`).
  - The ratified M4 exit fence: *"The 10-year atomic turn stays. No annual substeps, no mid-turn
    handbacks"* (`docs/m4-exit-inventory.md:321-322`).
- **Why it collides.** §16 grants Action Capacity per pulse to agents whose actions are their own
  (§10). That is a finer-grained cadence.
- **What the ruling does settle.** D-039 E2's missing war layer (`:139-141`) now has a Director
  shape (PART G).

**F12 — Movement and fog premises in D-039 and D-040 (§15, §13). TENSION.**
- **D-039 E6** takes its dt constraint from the premise *"Armies march the same graph"*
  (`d039-command-fog-and-siege.md:162-164`). §15 removes the premise. The constraint itself
  still holds.
- **D-040 B4** rules *"Sea travel extends the network's edge types; it is not a separate movement
  mode"* (`d040-discovery-and-control.md:80`).
  - §15 speaks only of land terrain.
  - A naval MobileAgent at continuous x/y would be the separate mode that B4 rules out. It also
    falls under frozen D-009's one-network clause (`d009-d010-map-population-addendum.md:17`),
    so it belongs to CR-017 §5 as well.
  - The demo already places "a fleet at sea" at a free position
    (`docs/architecture/world-visualization.md:190`).
- **D-039 Part C** gives fog states per enemy FORMATION (`d039-command-fog-and-siege.md:80`). §13
  lets one Army token hide its formations. INFERRED: the token must not merge formations that
  are observed unequally.

**F13 — Where special people's and cultural groups' members come from, against D-037 A1 (§11,
§14). TENSION.**
- **The closed text.** *"NOTHING SPAWNS. Every actor, settlement and hostile force must
  originate from population already simulated."* (`d037-emergent-polities.md:14-15`).
- **The rulings.** A catalogue person recruitable by anyone, and a band that "eventually
  disband[s]", reconcile with A1 only if their members are drawn from simulated buckets. CR-017
  §7 option A is that reconciliation for single persons; for bands it must extract and return N
  members by Transfer (CR-017 §7).
- **The demo.** It shows a 1,200-member group and a four-member band with no carrier (WV-06).

**F14 — Special people against the ratified M4 notables ruling (R-1) and its shipped lifecycle
(§11; also §9). TENSION.**
- **The ratified texts.**
  - R-1: *"RULED: OPTION B — A NOTABLE IS A PERSON. Extracted from the bucket via
    `Ledger.Transfer`"* (`docs/m4-spec.md:413-414`).
  - T4.8's acceptance: *"a notable can be BORN, DIE and DEFECT"* (`docs/m4-spec.md:275`).
  - The retrofit fields: *"home bucket link; traits and competence; MUTABLE experience;
    lifecycle (death, defection, purchase, falling out)"* (`docs/m4-spec.md:188-190`).
  - The payment half: *"the consideration for a purchase is a separate flow that cannot exist
    until money does"* (`docs/m4-spec.md:430-433`).
- **The shipped code.**
  - `NotableRow(NotableId id, SettlementId settlement, PolityId allegiance, int cohortIdx,
    Conserved count)` (`Sim.Core/State/WorldState.cs:635-636`).
  - The lifecycle comment: *"A notable cannot be created from nothing, cannot vanish"*
    (`Sim.Core/State/NotableLifecycle.cs:28-30`).
- **Where §11 differs.**
  - It gives special people "predefined skills" where R-1 has mutable experience.
  - It says special people "do not require persistent relationship/allegiance simulations",
    where R-1 has defection and purchase.
  - Its people may "disappear/retire", which is not a lifecycle operation.
  - INFERRED, conditional: if recruitment takes a person held by another polity, it is R-1's
    purchase event, whose consideration needs money (M5). §11 itself says nothing of payment.
- **The open question.** Whether a General (§9) can be a special person decides whether frozen
  D-011's *"general experience"* (`d011-battle-layer-addendum.md:27`) applies to one.

**F15 — Recruiting and commanding individuals, against D-042 §6.1 (§9, §11). TENSION.**
- **The closed text.** *"The player acts at grand-strategy abstraction and does not manipulate
  individual people"* (`d042-empire-and-player-control-addendum.md:97-98`).
- **INFERRED reconciling reading.** Special people are strategic agents, like the army of §6.2
  (*"move an army"*, `:101`), not citizens. The Director confirms or rejects this reading.

**F16 — "Civilization" against D-042 §1.3 (§1–§5). TERM.**
- **The closed text.** *"Do not introduce a separate Civilization abstraction"*
  (`d042-empire-and-player-control-addendum.md:30-32`).
- **INFERRED reading.** "Civilization" in the dictation is D-042's Empire, which is D-037's
  polity (`PolityId`). The Director confirms.

**F17 — A successor polity's Age: a new open question (§1). UNRESOLVED.**
- **The problem.** §1 makes Age progression irreversible per civilization. Rebel and successor
  polities have no Age history of their own:
  - D-042 §2.5 lets rebellion create a new Empire
    (`d042-empire-and-player-control-addendum.md:48-51`);
  - D-021 requires collapse to resolve into *"successor states, warlord regions, village
    autarky"* (`d021-stability-doctrine.md:13`).
- **Unruled.** A new polity could inherit its parent's Age, recompute it from the milestones it
  satisfies, or start at A1. So could a polity that collapses into autarky; R1 then keeps its
  Age while the conditions its milestones stood for are gone.

**F18 — What cultural groups do, against the channels already ruled (§14). TENSION.**
- **Loyalty.**
  - D-040 C3: *"DO NOT ADD A `loyalty` FIELD"*; *"loyalty is what you observe when reach runs
    out"* (`d040-discovery-and-control.md:105`, `:110-111`).
  - D-037 E1 computes loyalty from a listed set of quantities (`d037-emergent-polities.md:164-167`).
  - D-041 C1 lists attachment's candidate drivers (`d041-attachment.md:54-62`). None of them is a
    performance.
- **Revenue.** D-042 §10.3: *"one economic ownership layer — no population wallets"*
  (`d042-empire-and-player-control-addendum.md:183-186`). A band's revenue must land in Empire
  state.
- **INFERRED.** Each effect must move a quantity these records already name. The frozen
  counterparts (D-010's riot inputs, the Spine's culture and diplomacy rows) are in CR-017 §8.

**F19 — Construction capacity is NOT bankable, against the shipped PathBuild bank (§17). DIRECT
CONFLICT with a ratified packet spec and shipped code on `main`.**
- **The ratified M3 packet spec (T3.8).** *"housing maintenance+build draw from the CONSTRUCTION
  pool; PathBuild banks the remainder"* (`docs/t3.8-spec.md:44-45`).
- **The shipped code.** *"banked labor accrues unspent; later milestones spend it on better road
  tiers"* (`Sim.Core/Systems/PathBuild/PathBuildSystem.cs:51-52`). That is "automatic
  accumulation of unused construction capacity", which §17 forbids.
- **Two related carries, INFERRED not banking (the Director confirms):**
  - Housing carries a whole-unit `BuildRemainder` (`Sim.Core/Systems/Housing/HousingSystem.cs:39-40`);
  - frozen D-004 mandates a per-entity *"remainder accumulator"* for fractional flows
    (`m0-kernel-spec.md:11`).

  Both round a fraction of a *used* flow; neither keeps unused capacity.
- **Not a conflict, noted only.** D-042 §9.5 gives the opposite policy to knowledge: *"Unallocated
  knowledge ACCUMULATES AS A RESERVE rather than disappearing"*
  (`d042-empire-and-player-control-addendum.md:170-171`). A surge (§5) on both Research and
  Construction would therefore bank in one domain and evaporate in the other.

**F20 — Settlement class by population, against D-017 and D-038 H3 (§18). TENSION.**
- **The closed texts.**
  - D-017: *"A settlement carries a **size** stock grown from population and construction, which
    drives … (render-side) footprint"* (`docs/m3-spec.md:15`).
  - D-038 H3 adopted the composed sprite because *"it extends T3.8's size tiers, which already
    vary the settlement by what it has built"* (`d038-visual-target.md:153-157`).
- **What is shipped.** The size step is dwellings only (`Sim.Core/State/WorldState.cs:91-92`).
- **The question.** "primarily determined by population" leaves room for a secondary
  input. Whether the D-017 stock or population drives the visual class is the Director's.

**F21 — "Capability", against D-038 H2 (§19, §20). TERM.**
- **The closed text.** *"a university is NOT an object at coordinates inside a settlement — it is
  a CAPABILITY THE SETTLEMENT HAS"* (`d038-visual-target.md:96-98`).
- **Consistent half.** The ruling agrees with H2's "not an object at coordinates".
- **Colliding half.** It treats universities as countable instances: 1 to 30, with "additional
  instances" that have diminishing returns. D-042 §8 uses "capability" for the output of a
  predicate.

**F22 — Continuous movement animation, against D-038 C1/C3 (§22, §9, §11). TENSION.**
- **The closed text.** *"People never render as figures with faces, limbs or animation cycles"*;
  *"WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS"* (`d038-visual-target.md:29`, `:33`).
- **INFERRED reconciling reading.** Moving a token is not a figure's animation cycle, so a
  Scientist, Artist or band moves as a token.

**F23 — Where the Ages sit on the frozen ladder (§1, §2). TENSION, not a collision: no ruling moves
a milestone.**
- **The frozen text.** The ladder places *"computed era labels"* in the knowledge milestone
  (`civ-sim-architecture-v3-outline.md:110`), which is M7 after D-011 §6.
- **Why it matters.** §2's milestones span military, institutional, infrastructure and knowledge
  conditions. Several of the systems behind them are not built by M7.
- **Recorded, not scheduled.** The placement is the Director's, with CR-005 (M5/M7 ownership).

**F24 — The surge as a positive loop, against D-021's paired-feedback rule (§5). TENSION, INFERRED.**
- **The closed text.** *"Every positive feedback loop in the design must ship with at least one
  negative feedback loop that strengthens with amplitude"* (`d021-stability-doctrine.md:8`).
- **The loop.** The surge "primarily scales in magnitude with civilization scale/development". It
  boosts the outputs that satisfy the next Age's milestones, which triggers the next surge.
- **What the model must do.** DD-14's model must name the negative loop that pairs with this one.

### F.3 Living documents, placeholder content, UI code and unratified analyses (not frozen under S8 §1, `spine-s8-governance-freeze.md:13-17`; changing them is outside this documentation-only pass, and `Sim.Core` code changes only under a packet)

**F25 — Six placeholder Ages against nine (§1). DIVERGES.**
- **The shipped content.** Six Ages, `AGE_00` to `AGE_05` (`Sim.Ui/UiContent/trees/ages.json:27-252`),
  named neutrally ("Age 00"…) (`docs/architecture/ages-ui.md:115-116`).
- **The UI record** confirms *"the six Ages `AGE_00`..`AGE_05` and their 23 milestones"*
  (`docs/architecture/the-trees-ui.md:392-393`).
- **What also keys on those ids:** every Trees node `ageRange` and every demo-state step.
- **MEASURED by parse.** 6 Ages, 23 milestones, 17 of them mandatory.

**F26 — A shipped test that forbids the ruled names (§1). DIVERGES.**
- **The test.** `PlaceholderAges_Render_WithoutHistoricalNames`
  (`Sim.Ui.Tests/TreesScreenTests.cs:147-159`) asserts that the Age screen never paints "Stone",
  "Bronze", "Iron", "Classical", "Medieval", "Industrial" or "Modern".
- **The collision.** §1's Age names contain every one of those words.
- **What it pins.** The placeholder's neutrality, which is correct for placeholder content. When
  real Ages land, the test's premise ends.
- **Not edited.** This pass changes no test.

**F27 — The milestone schema and demo against §2's rule (§2). DIVERGES.**
- **The schema** has one boolean `mandatory`, one `category` and `prerequisites`
  (`docs/architecture/the-trees-content-schema.md:161-166`;
  `Sim.Ui/UiContent/trees/ages.json:306-315`). It cannot express:
  - a "sufficient" count of Supporting milestones;
  - coverage across a minimum number of categories;
  - alternatives within a category;
  - the remaining requirements for the next Age.
- **The categories.** Four provisional ones (technological, material-economic,
  institutional-social, systemic) are applied to every milestone, mandatory ones included
  (`Sim.Ui/UiContent/trees/ages.json:5-26`; `docs/architecture/ages-ui.md:132-133`). §2
  categorises only Supporting milestones. Its examples are military, economic, institutional,
  infrastructure and knowledge.
- **The demo, MEASURED by parse of `Sim.Ui/UiContent/trees/demo-state.json`.** At step 2 the
  transition is pending at progress 1.0 (`:761-768`). All four mandatory milestones of `AGE_03`
  are complete, but its only supporting milestone, `M_03_05`, stands at 0.6 partial. A
  transition with no Supporting milestone met is what §2 forbids.

**F28 — The Age view and Trees content against §3. TENSION.**
- **§3's "eventually" list.** The Age view shows Mandatory and Supporting met/total counts
  (`docs/architecture/ages-ui.md:46-50`). It has no category-coverage view, no alternative
  pathways and no "remaining requirements for the next Age".
- **A milestone inside a Tree.** The Trees graph ships a `milestone` node type
  (`Sim.Ui/UiContent/trees/the-trees.json:84-85`), and one instance sits in the INDUSTRY lens
  (`:181-182`). §3 says milestones are not an eighth Tree. A milestone placed inside one of the
  seven blurs "underlying progression objects that are surfaced in the Age/progression UI".
- **An eighth lens.** The UI documents that *"Adding an eighth lens is a JSON edit"*
  (`docs/architecture/the-trees-ui.md:52-53`). §3 fixes seven.
- **A third milestone state.** The content has "In progress", *"A milestone that is partly met"*
  (`Sim.Ui/UiContent/trees/the-trees.json:46-47`). §3 names achieved/unachieved. Partial
  *display* is not forbidden (§4 bans only a partial *modifier*).

**F29 — The per-Age progress percentage against §4. TENSION, weak.**
- **The shipped display.** The Age view shows *"per Age (`AgeStateSnapshot.CurrentProgress`)"*
  (`docs/architecture/ages-ui.md:85-88`), for example "72 %"
  (`Sim.Ui.Tests/TreesScreenTests.cs:168`).
- **INFERRED.** It has no effect, so §4 does not forbid it. What the percentage should mean under
  a "sufficient + coverage" rule is DD-T6's to decide.

**F30 — The per-Age `transitionEffect` slot against §5. TERM.**
- **The shipped slot.** `"transitionEffect": { "animation": "age-transition", "note":
  "Presentation only. …" }` (`Sim.Ui/UiContent/trees/ages.json:54-56`) is a presentation
  animation.
- **The risk.** The §5 surge is a simulation effect. A later agent must not wire one into the
  other.

**F31 — Development that expires, against §6. TENSION.**
- **The Trees schema** lets a node end at an Age: *"`to` absent means open-ended"*
  (`docs/architecture/the-trees-content-schema.md:95`). The demo milestone node carries
  `"ageRange": { "from": "AGE_04", "to": "AGE_04" }` (`Sim.Ui/UiContent/trees/the-trees.json:182`).
- **The unratified M5 placeholder** plans *"Technologies/institutions becoming obsolete or
  transforming into successors"* (`docs/m5-research-technology-institutions-placeholder.md:89`).
- **The PROPOSED design lane** allows *"Forgetting — firmness decays to zero"*
  (`docs/design/arch-C-knowledge.md:337-338`).
- **What §6 forbids.** Loss *because an Age is entered*. INFERRED: loss by real degradation
  remains open (Q-20, `docs/design/arch-PQ-conflicts-and-questions.md:791`).

**F32 — Trees and gallery content against §8. DIVERGES and TERM.**
- **No slot for modernization.** Unit-class nodes use the realization state set, which has no
  obsolete or superseded state (`Sim.Ui/UiContent/trees/the-trees.json:58-59`). No relation kind
  says "successor". §8's automatic modernization has no slot.
- **Era tags.** Gallery units carry `"era": "Industrial"` and `"Modern"`
  (`Sim.Ui/UiContent/trees/gallery.json:58-61`). These are drawing registers, but they now read
  as §1 Age names.
- **"Unit" and "formation".** A "unit" is a unit CLASS (`the-trees.json:78-79`), while D-011's
  formation is the atomic army piece. §8 uses both words.

**F33 — MobileAgent state and ownership in the world layer (§9, §11). DIVERGES.**
- **"Owner".** The layer keeps Controller and Allegiance and marks *"Owner (demo only)"*
  (`docs/architecture/world-visualization.md:62-64`). §9 puts an owner in every MobileAgent's
  common state.
- **Live notables.** They are reported as settlement-attached "person" agents with no position,
  movement or Action Capacity (`docs/architecture/world-visualization.md:92`;
  `Sim.Ui/World/Live/LiveWorldSources.cs:230-231`).
- **The gallery.** It models General, Hero, Artist, Scientist and Engineer as generic unit
  classes (`Sim.Ui/UiContent/trees/gallery.json:64-68`). §11 separates Special Person
  definitions from Ability definitions.

**F34 — "The ruled shape for armies" (§15). TENSION. D-009's ruled shape governs until CR-017 §5 is ruled; these lines go stale only if that ruling admits off-road movement (DIRECTOR DECISION REQUIRED).**
- **Four places say land armies stand on the graph because D-009 ruled it:**
  - `docs/architecture/world-visualization.md:66-67`;
  - `docs/architecture/world-visualization.md:188-189`;
  - `docs/architecture/world-visualization.md:354`;
  - `Sim.Ui/World/WorldReports.cs:153-155`.
- **The data model already conforms.** It carries a continuous `WorldPoint` for any agent
  (`docs/architecture/world-visualization.md:65-66`).

**F35 — Terrain classes against the shipped movement-cost raster (§15). DIVERGES, a gap rather
than a contradiction.**
- **The shipped raster** is `BaseCost + SlopeFactor × slope` on land and a flat `WaterCost` on
  water (`Sim.Core/Worldgen/Worldgen.cs:132-144`).
- **What §15's examples need that it lacks:** a forest term, a hill or mountain class beyond
  slope, and a desert term.

**F36 — Settlement stages driven by `SizeTier` (§18). DIVERGES.**
- **What the world layer does.** Live settlements are staged by `sizeTier` (dwellings). *"A driver
  whose provenance is `demonstration` (the `population` thresholds) **never stages a live
  bundle**"* (`docs/architecture/world-visualization.md:101-105`).
- **The content.** It lists `sizeTier` ("provenance": "simulation") ahead of `population`
  ("provenance": "demonstration") (`Sim.Ui/UiContent/world/morphology.json:101-119`).
- **What §18 says.** Population is the primary driver.

**F37 — Institutions drawn individually and staged by capacity (§19). DIVERGES; same numbers,
different meaning.**
- **Individual drawing.** `"individualUpTo": 4` draws up to four same-type reports per settlement
  individually (`Sim.Ui/UiContent/world/morphology.json:158-160`;
  `docs/architecture/world-visualization.md:128-130`).
- **Capacity staging.** The university stages by each report's capacity:
  `"stageBy": { "input": "capacity" }`, with the stages "Main building", "Expanded wing",
  "Academic block" and "Campus" (`Sim.Ui/UiContent/world/morphology.json:171-223`).
- **§19 instead** aggregates one visual per type, staged by instance COUNT: 1 small, 10 expanded,
  20 campus, 30 complex, with the number shown at high zoom.
- **The shipped 1/10/20/30.** These are CIVILIZATION-WIDE summary counts, pinned by
  `Summary_CountsReportedUniversities` (`Sim.Ui.Tests/World/WorldMorphologyTests.cs:168-181`;
  `docs/architecture/world-visualization.md:186`). They are not §19's per-settlement stage
  thresholds.

**F38 — Gradual change over ~10–15 turns (§21). TENSION.**
- **The live stage changes rarely.** It is *"the QUANTIZED settlement-size step (0..4)"*; *"the
  tier changes a handful of times per campaign"* (`Sim.Core/State/WorldState.cs:91-95`).
- **Turn length varies twentyfold,** from 10 to 0.5 sim-years (`m0-kernel-spec.md:17-23`).
- **Visuals may not read time.** The world loader rejects time-named inputs
  (`docs/architecture/world-visualization.md:113-114`).
- **INFERRED.** §21 is a presentation target that dt-integrated state must meet.

**F39 — The animation rules against §22 (and §16). DIRECT CONFLICT with the unratified convention;
TENSION with its amendment.**
- **The unratified convention.**
  - *"§5 ANIMATION CONVENTIONS — three kinds, nothing else"*
    (`docs/architecture/pre-m5-visual-system.md:239`). None of the three is movement.
  - Its ban list includes *"any animation that implies a sub-turn event the simulation did not
    produce"* (`:256-257`). Under §16, War Pulses really do move agents inside the turn.
- **The Trees amendment.** It lets steady loops run *"only while a reported process state holds:
  … training, under construction"* (`:429-431`). The content matches: a sweep and pulses loop
  for the whole state (`Sim.Ui/UiContent/trees/animations.json:19-27`), with no one-shot keyed to
  construction or training as such (training has none; construction completion plays one only
  incidentally, as the `building-stage-up` pip-fill when maturity first appears). §22 permits
  ("can receive") one-shots at initiation/resolution and does not require the loops.
- **The world layer.** *"There is no movement, speed, pathfinding, … or Action Capacity anywhere
  in this layer"* (`docs/architecture/world-visualization.md:206-207`).

**F40 — The Age header device (§1, §8). TENSION.**
- **The specified device.** `NEOLITHIC · MID`, with Early/Mid/Late cells and a world-level Age
  beside the player's (`docs/architecture/pre-m5-visual-system.md:224-227`).
- **What §1 has instead.** Per-civilization Ages, with no world Age and no sub-tiers.
- **A premise that may fall.** The device rests on *"Both labels are computed output …
  the sim never consults it"* (`:227-228`). That holds only if CR-017 §1 does not make the Age an
  input.

**F41 — Era registers and date ranges (§1). The premise is superseded; the binding stays open.**
- **The recorded conflict.** *"Era registers read as Age names"* avoided binding
  Classical/Medieval/Industrial/Modern registers to *unnamed* Ages
  (`docs/architecture/the-trees-ui.md:423-425`). The Ages are now named, and four register names
  match (A5, A6, A8, A9). DD-T8 stays open.
- **Dates.** Every Age carries a `dateRange`, *"display only, never a gate"*
  (`docs/architecture/the-trees-content-schema.md:152`). INFERRED: this fits §1 as historical
  reference text, provided it never becomes a bracket.

**F42 — Construction progress in the placeholder, and the audit's bank plan (§17). TENSION.**
- **The placeholder.** The Trees demo reports `"constructionProgress": 0.4` rising across steps
  (`Sim.Ui/UiContent/trees/demo-state.json:335`).
- **The audit's plan.** Its proposed Stage 4 lists *"C14 (the construction bank …)"*
  (`docs/architecture/pre-m5-repository-audit.md:514-515`).
- **What §17 covers.** It forbids banking UNUSED capacity. It is silent on per-project progress
  that stores capacity which WAS used. The shipped M4-D gate has neither
  (`Sim.Core/Systems/Construction/ConstructionSystem.cs:40-41`).

**F43 — Unratified analyses and the design corpus that reject the Age-as-input and unlock shapes
(§1, §5, §8, §12). TENSION; these rule nothing.**
- **Recorded so no one reads them as standing against the ruling unexamined:**
  - `docs/capability-architecture-decision.md:135-140`: *"a threshold on a MONOTONE-IN-TIME
    accumulator IS a tech-tree node wearing different clothes"*. Also `:152`, which rejects the
    "stock → project → completion → unlock" model.
  - `docs/milestone-architecture-governance.md:141-149`: FALSIFIED #2, era-gated military
    *"in frozen D-011"*.
  - `docs/design/recovered-decisions-architecture-invariants.md:52`.
  - `docs/design/arch-O-invariants.md:56`: *"Era is an **OUTPUT**"*.
  - `docs/design/arch-D-technology-capability.md:74-75`: a standing *"+10% yield"* violates law 2.
  - `docs/design/arch-D-technology-capability.md:608-612`: *"No conjunct reads a date, a turn
    index or an era label"*.
  - `docs/design/arch-E-breakthrough-domains.md:287-290`.
  - `docs/design/arch-JKL-food-disaster-needs.md:184-186`.
  - `docs/design/arch-C-knowledge.md:217-218`, `:267`, `:339-342` (knowledge lost with its holder
    or carried by a person, against §12) and `:527`.
- **The audit's pre-registered readings,** which §1, §4, §5, §8 and §15 now select:
  - `docs/architecture/pre-m5-repository-audit.md:340`: the benign "derived reading" path;
  - `:341`: C05, "nothing keyed on an Age";
  - `:350`: C14, "a currency Ages can grant → Law 2 CONFLICT";
  - `:352`: C16, "era-/tech-gated unit types → Law 4";
  - `:355`: C19, "raster 2D position off the graph → CONFLICT (D-009)".
- **The M5 placeholders.**
  - `docs/m5-research-technology-institutions-placeholder.md:13-18`: "technology eras" held
    absent. INFERRED: keep §1's Ages distinct from "technology eras".
  - `docs/m5-temporal-control-and-player-agency-placeholder.md:25-29`: construction advances
    continuously, while §17 makes the turn its accounting unit.
  - `docs/m5-temporal-control-and-player-agency-placeholder.md:187-190`: M5 owns technology
    prerequisites, while §8 adds an Age-owned unlock path.

### F.4 Term collisions (TERM; no convention is written — CONV-2 is the next free number)

| word | existing meanings (cited) | the dictation's meaning |
| --- | --- | --- |
| milestone | the frozen build ladder M0→M11+ (`spine-s8-governance-freeze.md:17`; `CLAUDE.md:10`) | an Age-progression object (Core / Supporting) |
| Age / age | the demographic age band (`civ-sim-architecture-v3-outline.md:44`); the world loader rejects an input named `age` as time (`docs/architecture/world-visualization.md:113-114`) | a civilization-development state A1–A9 |
| era; Medieval, Early Modern, Industrial, Modern | D-006's date bands that select dt (`m0-kernel-spec.md:17-23`; `Sim.Data/content/era-pacing.json:3-9`); the Spine's "computed era labels" (`civ-sim-architecture-v3-outline.md:110`); `EraRegister` drawing styles; gallery `era` tags | the names of A6–A9 |
| pulse | D-011's battle command pulse, 6–12 per battle (`d011-battle-layer-addendum.md:10`); the audit's C21 used that definition (`docs/architecture/pre-m5-repository-audit.md:323`) | War Pulse: a war-time Action Capacity cadence inside the strategic turn |
| surge | migration surge (`docs/adr/adr-012-destination-viability.md:79-80`) | Age Transition Surge — PROPOSED: always write it in full |
| General | the M4 notable general (`docs/m4-spec.md:36`); a gallery unit class (`Sim.Ui/UiContent/trees/gallery.json:64`) | a MobileAgent type (§9); possibly a special person (§11) |
| unit / formation | unit CLASS (`Sim.Ui/UiContent/trees/the-trees.json:78-79`); atomic formation (`d011-battle-layer-addendum.md:36`) | used interchangeably in §8 |
| locked | a UI node state (`Sim.Ui/UiContent/trees/the-trees.json:24`) | the dictation's ruling status (recorded here as RATIFIED) |
| capability | D-038 H2's university-as-capability (`d038-visual-target.md:96-98`); D-042 §8's predicate output | an institution counted in instances (§19, §20) |
| civilization | D-042 §1.3 forbids a separate abstraction (`d042-empire-and-player-control-addendum.md:30-32`) | the actor that holds an Age (F16) |
| owner | Controller / Allegiance / "Owner (demo only)" (`docs/architecture/world-visualization.md:62-63`) | a common MobileAgent field; the recruiter of a special person |
| loyalty | not a field (`d040-discovery-and-control.md:105`) | something a cultural group affects |

---

## PART G — WHAT THIS RECORD SETTLES THAT WAS PREVIOUSLY OPEN

Recorded so these are not re-asked. **No register row was edited.** The mapping was checked by
the same refute-first sweep; rows the verifiers rejected are listed under "still open".

| previously open | where | now ruled | extent |
| --- | --- | --- | --- |
| **DD-04** Age: label or input? | `docs/architecture/pre-m5-repository-audit.md:373` | Ages are per-civilization, non-calendar, milestone-driven, irreversible and discrete (§1, §2, §4). §5 and §8 treat the Age as READ. The register's own text says: *"If read, Law 4 and Spine S2.4 must be amended by CR"*. That CR is CR-017 §1. | PARTIALLY — "input" is answered only as DIRECTOR DECISION REQUIRED until CR-017 §1 is ruled |
| DD-04(ii): confirm no Age-keyed buff is admissible in any form | same | **Not confirmed as asked.** §4 excludes partial modifiers, §7 defers generic effects, and §5 admits a surge (CR-017 §2) | REFRAMED |
| C02 global Age | `:304` | No world-level Age is ruled; Ages are per civilization | REFRAMED |
| C03 Early/Mid/Late | `:305` | Nine Ages, with discrete transitions and no sub-tiers ruled (F40) | PARTIALLY |
| C04 milestone-based advancement | `:306` | Core + Supporting across categories, no single-category farming (§2); "read by systems" goes to CR-017 §1 | PARTIALLY |
| C05 partial Age buffs | `:307` | Ruled out in the partial form (§4) | FULLY for the partial form |
| C06 per-civilization Ages | `:308` | Divergence is ruled (§1); whether it is stored and consulted goes to CR-017 §1 | PARTIALLY |
| **DD-T6** Age list, milestones, completion rule, progress | `docs/architecture/the-trees-ui.md:453` | The list and names: nine, A1–A9. The completion rule's shape (§2). Partial credit has no Age effect (§4). The milestone lists are DEFERRED; what the percentage means is still open | PARTIALLY |
| **DD-T2** lens list, node types, state vocabulary | `:449` | The seven lenses are confirmed (§3); node types and states are still open | PARTIALLY |
| **DD-T8** Age → visual register | `:455` | The premise of the-trees-ui §15.2 is gone (F41); the binding is still open | REFRAMED |
| **DD-T1** process-state animation rule | `:448` | Construction and training may receive one-shots at initiation/resolution ("can receive"); persistent loops are "not required"; MobileAgent movement is animated continuously (§22) | PARTIALLY |
| DD-T3 / DD-T5 availability gate; realization measures | `:450`, `:452` | §8 names three steps: research (known/possible), conditions (realized), Age transition (conversion). The measures are still open | informs, not settled |
| ages-ui §6, rows 1–4 | `docs/architecture/ages-ui.md:158-161` | Row 1: list and names answered; "dates" reframed (not brackets). Row 2: structure answered; lists DEFERRED. Row 3: the rule's shape answered. Row 4: discrete and irreversible; timing still open | PARTIALLY |
| **DD-06** War Pulse definition | `docs/architecture/pre-m5-repository-audit.md:375` | A cadence inside the strategic turn, not D-039 E3's decision-point model (F11). Mechanics DEFERRED (CR-017 §6) | PARTIALLY |
| C21 War Pulses | `:323` | A War Pulse is NOT D-011's battle command pulse (F.4) | REFRAMED |
| **DD-07** Action Capacity vs Law 3 | `:376` | A universal per-MobileAgent budget; its interval is the strategic turn, or the War Pulse in war. That is the "per-turn" branch, so CR-017 §3 | PARTIALLY |
| C18 Action Capacity | `:320` | Per agent, not per polity | REFRAMED |
| **DD-08** construction points | `:377` | (a) Capacity is NOT banked (§17); materials stay conserved goods. (b) A construction bank Ages could grant is moot, but §5's surge on Construction reopens the Law 2 half (CR-017 §2). (c) There is no bank to complete on | PARTIALLY |
| C14 construction capacity | `:316` | "Banked across turns" is rejected. Stored per-project progress is not addressed (F42) | PARTIALLY |
| DD-01 / CR-006 §1 | `:371` | War Pulses run inside the turn; CR-006's A/B/C is not chosen | REFRAMED |
| **WV-01** town or city, and by what | `docs/architecture/world-visualization.md:342` | Primarily by population (§18); thresholds and names are still open | PARTIALLY |
| **WV-02** placement inside a settlement | `:343` | Embedded in the footprint, not individually placed (§19); the slot shape is still open (F37) | PARTIALLY |
| **WV-03** and **D-038 H8** past the parts limit | `:344`; `d038-visual-target.md:201-205` | Same-type instances merge into one count-staged visual with the number shown at high zoom (§19). How many distinct types one footprint shows stays with the visual milestone | PARTIALLY |
| **WV-06** bands and cultural groups | `docs/architecture/world-visualization.md:347` | They are MobileAgents (§14); what carries their members is still open (F13) | PARTIALLY |
| **WV-07** roles for important people | `:348` | MobileAgent types with their own actions (§9, §11); the carrier goes to CR-017 §7 | PARTIALLY |
| WV-08 map scale | `:349` | The driver only (§18) | PARTIALLY |
| **WV-09** off-graph positions | `:350` | Continuous x/y off-road for MobileAgents (§15) goes to CR-017 §5; sea: F12 | PARTIALLY |
| **DD-11 / CR-010** what is an institution | `docs/architecture/pre-m5-repository-audit.md:380` | Implied: a countable instance per settlement and type, with type-specific viability and diminishing returns (§19, §20). The canonical meaning is still open | PARTIALLY, by implication |
| DD-05 the knowledge object | `:374` | Knowledge belongs to the polity (§12), consistent with D-042 §9.2; the fence is still open | PARTIALLY |
| P-18 / Q-52 capability outliving preconditions; declared irreversibility | `docs/design/arch-PQ-conflicts-and-questions.md:602`, `:828` | An Age transition is never a loss event (§6); the Age is irreversible by ruling (§1) | PARTIALLY |
| Q-53 partial capability | `:829` | No partial Age modifier (§4); capability held in degree is still open | PARTIALLY |
| Q-22 where knowledge resides | `:793` | Ownership is at the polity (§12); the residence grain is still open | PARTIALLY |
| **CR-009** era gates vs Law 4 (never written) | `docs/milestone-architecture-governance.md:296-298`; `docs/design/arch-PQ-conflicts-and-questions.md:403` | "Era-gated" can now be read as "Age-gated" (§1, §8). Whether that satisfies Law 4 is CR-017 §1's question; the Director may absorb CR-009 there | REFRAMED |

**Still open and NOT settled here:**
- DD-02 (C01's scope; dt as data);
- DD-03 / CR-005 (M5/M7 ownership);
- DD-T7 (Δt per Age, catch-up);
- DD-T8 (the register binding);
- DD-T4 (research points);
- DD-09 / DD-T9 (the veterancy carrier — §8's conversion and §13's split/merge add cases it must
  cover);
- DD-10 (where armies, the AutoResolver and sieges are placed — §13 fixes only the Army's object
  shape);
- DD-12;
- X-VIS-1 / WV-04, WV-05;
- D-042 §14.1 (D-018's income column);
- CR-006, CR-008, CR-009, CR-010;
- and, new: **CR-017**, **DD-13**, **DD-14**, and F17 (a successor polity's Age).

---

## PART H — CITATION FINDINGS (§7.12: THE TREE WINS)

These record where the dictation's statements about the tree were checked (GOV-3 B4,
`docs/gov-3-execution-protocol.md:106-109`). **None of them changes a ruling.** They change what
may be cited in support of one.

**H1 — §15, "Movement remains continuous x/y." Not a fact about the simulation.**
- **The simulation.** No real-valued position exists in it: *"Every place is an integer … **No
  real-valued position exists; nothing moves between turns**"*
  (`docs/architecture/pre-m5-repository-audit.md:121-123`, READ at `de5e00e`, whose non-UI tree
  `096677e` shares).
- **The UI layer.** Continuous coordinates exist only in `Sim.Ui`'s `WorldPoint`
  (`docs/architecture/world-visualization.md:65`). There, land armies stand ON the graph
  (`:188-189`).
- **How it is recorded.** "Remains" is the Director's intended shape, not existing state.

**H2 — §3, the seven Trees. CONFIRMED.**
- **The lenses.** `the-trees.json` holds exactly Knowledge, Techniques, Institutions,
  Infrastructure, Industry, Military and Applications (MEASURED by parse;
  `docs/architecture/the-trees-ui.md` §1).
- **Not an eighth Tree.** The Age view is already built this way: *"a separate historical
  framework around the Trees, not an eighth lens"* (`docs/architecture/ages-ui.md:12`).

**H3 — §17, "It is NOT bankable." True of M4-D; not of the whole tree.**
- **True of M4-D.** Capacity is *"construction share × adult population × dtYears"*, with *"no
  partial draw, no banked progress"*
  (`Sim.Core/Systems/Construction/ConstructionSystem.cs:50-51`, `:40-41`).
- **Not true of PathBuild.** It banks the remainder (F19).

**H4 — §19, "Morphology remains cumulative and data-defined." CONFIRMED.**
- **Cumulative.** *"Structure stages (`visualTypes[].stages`): cumulative — each stage lists only
  the parts it adds"* (`docs/architecture/world-visualization.md:108`).
- **Data-defined.** The stages live in `Sim.Ui/UiContent/world/morphology.json`.

**H5 — §16, "Action Capacity refreshes according to the strategic turn" (normal world). Nothing
refreshes today.**
- **The tree.** No Action Capacity exists: *"There is no movement … or Action Capacity anywhere
  in this layer"* (`docs/architecture/world-visualization.md:206-207`). The audit's C18 finds
  none in the simulation.
- **How it is recorded.** As design, not as existing behaviour.

**H6 — An in-tree finding (filed, not fixed).**
- **The stale range.** The world-visualization entry in the queue reads *"(WV-01..WV-08, …)"*
  (`docs/queue.md:1421`).
- **The tree.** The register holds WV-01 to WV-09; WV-09 is off-graph positions
  (`docs/architecture/world-visualization.md:350`).
- **Handling.** The entry is left as written (GOV-3 G2, *"Filed, not fixed"*), and a line
  recording the finding is appended to the queue.

---

**Files written by this record:**
- `docs/d043-civilization-progression-ages-and-mobile-agents.md` (this file);
- `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md`.

**Append-only notes, nothing above them rewritten, in:**
- `docs/queue.md`;
- `docs/current-state.md`;
- `docs/architecture/world-visualization.md` §16;
- `docs/architecture/the-trees-ui.md` §19;
- `docs/architecture/ages-ui.md` §7;
- `docs/architecture/pre-m5-visual-system.md` §12.

**HOLD FOR MERGE.** Docs only. No mechanism designed, no code written.
