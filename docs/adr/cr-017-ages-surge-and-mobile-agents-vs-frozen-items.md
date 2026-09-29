# CR-017 — AGES AS STATE, THE AGE TRANSITION SURGE, MOBILE AGENTS AND WAR PULSES vs THE FROZEN LAWS, D-009/D-010, D-011 AND THE KERNEL CONTRACT

**Status: OPEN — awaiting director ruling. No frozen document has been edited and no code has
been changed.** Raised under S8 §3 (`spine-s8-governance-freeze.md:37`) by D-043
(`docs/d043-civilization-progression-ages-and-mobile-agents.md`), the Director's rulings of
2026-09-29.

S8 §2 prices a Director override: it *"exists — it is your project. But it costs a written ADR
stating what breaks, which tests and docs change, and the schedule price"*
(`spine-s8-governance-freeze.md:33`). D-043 is a decision record, not that ADR. This CR prices
each collision so the Director can rule, override or re-read, one collision at a time.

**CR number:** 017.
- `cr-016-armed-disaster-fallout.md` is the highest on this branch.
- `origin/m5-full-build` adds only `cr-008`.
- 009 and 010 are reserved by reference and not reused (the `cr-012` renumbering note).
- **MEASURED:** no `cr-017` file or `CR-017` id exists on any of the 85 local and remote refs
  (`git grep` and `git log --all --name-only`, 2026-09-29).

**Written against** `claude/civdemo-work-b1z2y4` @ `096677e` (= `origin/claude/civdemo-work-b1z2y4`;
18 ahead of `origin/main` `de5e00e`, 0 behind). Every `file:line` below was read on that tree.

**Seven independent collisions.** They can be ruled separately, with three couplings:
- §2's option B needs §1 ruled first.
- §6 should be ruled together with CR-006 §1.
- §1 is the ground of the never-written CR-009, which the Director may absorb here.

**Evidence type throughout: derivation.** No code implements any of the rulings, so nothing can
fail a test yet (S8 §3 field 2 admits "test/bench/derivation").

**Labels.** Frozen texts and rulings are quoted as RATIFIED. Each option and recommendation is
the filing agent's and is **PROPOSED**. Nothing here is a ruling.

---

## §1 CONFLICT ONE — THE AGE AS AN INPUT vs LAW 4

### 1.1 Frozen items in conflict (S8 §3 field 1)

- **Spine S2 Law 4:** *"4. **No calendar gates.** Capability derives from computed state; era
  labels are descriptive output only."* (`civ-sim-architecture-v3-outline.md:22`).
- **`CLAUDE.md:19`:** *"4. **No calendar gates:** capability derives from computed state, never
  from dates or era labels."*
- **The Spine ladder** places the Age-like object as computed OUTPUT: *"computed era labels; map
  shows civilizations pulling apart in time"* (`civ-sim-architecture-v3-outline.md:110`).
- **D-018:**
  - *"**Classes emerge, they are not unlocked.** Every class has a computed emergence predicate
    (Law 4)."* (`d018-classes-and-needs.md:8`);
  - *"**No era weight tables.**"* (`:11`).
- **Adjacent: the CR-009 ground, never written.** The frozen era-gates that this collision also
  decides:
  - *"(era-gated additions: bombard, air strike, dig-in)"* (`d011-battle-layer-addendum.md:13`),
    and the era-keyed arrival of later military content (`:45`);
  - *"expensive, era-gated, terrain-crossing edges"* (`d009-d010-map-population-addendum.md:12`);
  - *"era-weighted"* needs (`d009-d010-map-population-addendum.md:34`).

Non-frozen records that rest on the same law are recorded in D-043 F8:
- D-040 B3 (`d040-discovery-and-control.md:59-64`);
- D-039 A5 (`d039-command-fog-and-siege.md:37-40`);
- D-042 §8.4 (`d042-empire-and-player-control-addendum.md:157-158`);
- CR-001's rejected option (b) (`docs/adr/cr-001-dt-fragile-demography.md:3-5`).

### 1.2 The rulings that collide (D-043, verbatim)

- §5: *"Every civilization receives its own Age Transition Surge when it enters a new Age."*
  Also: *"may have Age-specific weighting"*.
- §6: *"Current Age may change ongoing conditions such as: … institutional possibilities …
  military organization …"* Bounded by: *"these changes must operate through the underlying
  simulation"*.
- §7, the philosophy, DEFERRED: *"Age → changes possibilities and civilization conditions →
  Research / Institutions / Infrastructure / Industry / Population / Military etc. → actual
  consequences"*.
- §8:
  - *"Entering a new Age can unlock: - new buildings - new institutions - new infrastructure -
    new units - new techniques - new applications"*;
  - *"Existing obsolete military units can automatically upgrade to their appropriate successor
    unit upon Age transition."*

### 1.3 Evidence (S8 §3 field 2 — derivation)

**The collision was predicted.** The pre-M5 audit registered it before the ruling existed. DD-04
asks: *"Is a Draft-1 Age … a computed LABEL that is output only, or a STATE any mechanism reads?
If read, Law 4 and Spine S2.4 must be amended by CR."*
(`docs/architecture/pre-m5-repository-audit.md:373`). §5 and §8 READ the Age:
- the surge's trigger is Age entry;
- the unlock's trigger is Age entry;
- modernization's trigger is Age transition.

**Law 4 admits capability from "computed state" and confines "era labels" to output.** The
dictation supports both readings of what an Age is.

**For "computed state":**
- *"Ages are civilization-development states, not date brackets"* (§1).
- *"Milestones represent actual civilization conditions"* (§2). An Age is then a predicate over
  computed state.

**For "era label":**
- *"Age progression is irreversible"* (§1). Once entered, the Age is a latch. It no longer tracks
  the conditions that produced it, and a polity whose conditions collapse keeps its Age (D-043
  F17).
- The Age names are era names: Bronze Age, Iron Age, Medieval, Industrial.
- D-040 B3 applied Law 4 to reject a capability keyed to a named stage: *"a tech-tree node opening
  sea travel is a calendar gate wearing a tree"* (`d040-discovery-and-control.md:60-61`).

**Why this needs a ruling.** Under the strict reading this is S8 §2's *"Law conflict"*
(`spine-s8-governance-freeze.md:29`); under the other it is no conflict at all. Choosing between
them is the Director's: *"A conflict is not yours to dissolve"* (`docs/gov-4-repository-freshness.md:34`).

### 1.4 Minimal fix options (S8 §3 field 3, ≤3; none implemented)

**Option A — RULE THE AGE COMPUTED STATE (an interpretive ADR, with no change to the text).**
- **The ruling.** An ADR records that an Age is a per-polity state computed from milestone
  predicates over published state and latched by the irreversibility ruling. "Era labels" in
  Law 4 then means date-band and display names (D-006's table; the Age's display name), which
  stay output only.
- **What it makes legal.** An Age-keyed effect is legal under Law 4 when it enters D-042 §8's
  pipeline as a predicate over the Age state (*"State → Published Variables → Predicates →
  Capabilities → Available Actions"*, `d042-empire-and-player-control-addendum.md:152`).
- **Limits.** D-018 :8 still binds: Age transitions may not unlock *classes*. D-018 :11 still
  binds needs weights; DD-13 already defers generic tables.

**Option B — KEEP LAW 4 STRICT; KEY EACH EFFECT ON ITS CONDITIONS.**
- **The ruling.** The Age stays output-only. Each §5/§8 effect keys on the same computed
  conditions (the milestones) that define the transition, never on the Age value. The UI still
  shows the Age and its transition.
- **The cost.** §1's irreversibility becomes display-only. An unlock could then recede if its
  conditions fall, and whether that matches the Director's "Age transition unlocks" is not clear
  from the dictation.

**Option C — DIRECTOR OVERRIDE: AMEND LAW 4.** A priced ADR (`spine-s8-governance-freeze.md:33`)
amends Spine S2.4 and `CLAUDE.md:19` to admit "Age state" as an input beside computed state.

### 1.5 Blast radius (S8 §3 field 4)

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none edited; one interpretive ADR (ADR-028 is the next free number) | append-only reconciliation notes on D-040 B3 and D-039 A5; CR-009 absorbed (the D-011 and D-009 era-gates read as Age-gates); the design-corpus rows in D-043 F43 re-labelled on the next audit | none today — no Age state exists in `Sim.Core`, and the `Sim.Ui` placeholder never feeds the simulation |
| B | none | none; every future unlock, modernization and surge definition must name its conditions | the same |
| C | the Spine S2.4 and `CLAUDE.md:19` texts; every quotation of Law 4 in D-018 (frozen) | D-039 A5, D-040 B3, D-042 §8.4, CR-001, the design-corpus rows A-04 and I-4; review checklists | every future review of a capability predicate |

### 1.6 Recommendation (S8 §3 field 5) — PROPOSED

**Option A.**
- It follows the dictation's own definition: conditions, not dates.
- It leaves the frozen text alone.
- It answers CR-009 in one place.

Under A, two constraints remain the operative limits on what an Age may change:
- §6's *"through the underlying simulation"*;
- §7's DEFERRED hold (DD-13).

---

## §2 CONFLICT TWO — THE SURGE'S RESIDUAL vs LAW 2

### 2.1 Frozen items in conflict

- **Spine S2 Law 2:** *"Coefficients *inside* a resolution equation (terrain multiplier in a
  combat equation, fertility factor in a yield function) are legal. Free-floating permanent auras
  ("+10% happiness") are illegal."* (`civ-sim-architecture-v3-outline.md:20`).
- **`CLAUDE.md:17`:** *"coefficients inside resolution equations are fine; free-floating
  permanent buffs are banned."*
- **D-018:** *"**No era weight tables.**"* (`d018-classes-and-needs.md:11`), against "Age-specific
  weighting".

Non-frozen records that apply the same law (D-043 F9, F10, F24):
- D-035-C's carrier test (`d035-needs-aggregation.md:91-92`);
- D-040 C3 (`d040-discovery-and-control.md:105-107`);
- D-042 §5.3 (`d042-empire-and-player-control-addendum.md:90-91`) and §7.3 (`:141-142`);
- D-021's paired-feedback rule (`d021-stability-doctrine.md:8`).

### 2.2 The ruling that collides (D-043 §5, verbatim)

- *"eventually approaches approximately 50% of its maximum as a residual effect"*;
- *"Initial balancing target: approximately +20% around peak, subject to later tuning."*;
- *"may have Age-specific weighting"*;
- the affected outputs: *"Broad affected outputs include: - Research - Construction - Resources -
  Industry - Food - Population productivity - Trade/economic activity - Infrastructure
  development - Military production/training"*.

### 2.3 Evidence (derivation)

**The residual.** 0.5 × ~20 % ≈ **+10 %**, standing permanently across nine output classes and
keyed to Age entry, not to any stock. That is the magnitude of the Spine's own example of an
illegal aura, "+10% happiness".

**The rest of the surge.** The ramp, peak and taper are transient, and "permanent" does not reach
them.

**The weights.** "Age-specific weighting" is a coefficient table keyed on the Age:
- CR-001 rejected a per-era rate table (`docs/adr/cr-001-dt-fragile-demography.md:3-5`);
- D-018 bans era weight tables for needs.

**Where the line falls.** Law 2 permits a coefficient inside each domain's own resolution
equation. The open question is whether a coefficient keyed on the Age is "free-floating". The
audit's reading of the same concept:
- *"a coefficient inside a domain's own equation driven by a computed stock that correlates with
  the Age → legal, but then not an Age buff"*;
- *"nothing keyed on an Age"* (`docs/architecture/pre-m5-repository-audit.md:341`).

### 2.4 Minimal fix options (≤3; none implemented)

**Option A — THE CARRIER FORM.**
- **The ruling.** The surge acts only as a coefficient inside each domain's own resolution
  equation, driven by a named carrier: a real stock or flow the transition creates. INFERRED
  examples, not designs: capital, institutions or infrastructure built during the surge.
- **What the residual becomes.** The lasting effect of what the surge built, not a standing
  multiplier.
- **What the targets become.** "+20 %" and "~50 %" become calibration targets for emergent
  outcomes.

**Option B — A COEFFICIENT ON THE AGE STATE INSIDE EACH DOMAIN'S EQUATION (needs §1 option A or
C).**
- **The ruling.** A coefficient keyed on Age state and time since transition, in sim-years (§3),
  inside each domain's own equation is "inside a resolution equation" and therefore legal.
- **What it permits.** The residual, stated as such.

**Option C — DIRECTOR OVERRIDE OF LAW 2** for the named Age Transition Surge, by priced ADR.

Dropping the residual is not offered as an option: it would rewrite the ruling.

### 2.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none | none; DD-14's model must name its carriers | the future surge packet |
| B | none edited; one interpretive ADR reading Law 2 for this one mechanism; D-018 :11 read as needs-only | D-035-C and D-040 C3 distinguished in writing | the same |
| C | Spine S2.2 and `CLAUDE.md:17` | every carrier-test document (D-035, D-040, D-042 §5.3) | review gates for every modifier |

### 2.6 Recommendation — PROPOSED

**Rule the constraint now and leave the model deferred (DD-14): Option A, with Option B as the
fallback** if no carrier can produce the dictated shape.

Two further constraints on any option (INFERRED):
- **D-042 §7.3:** each domain applies its own share. No single system applies the surge to nine
  domains.
- **D-021's paired-feedback rule:** the model pairs the surge with a negative loop (D-043 F24).

---

## §3 CONFLICT THREE — TURN-DENOMINATED QUANTITIES vs LAW 3

### 3.1 Frozen items in conflict

- **`CLAUDE.md:18`:** *"3. **dt-correctness:** every rate is per-sim-year; integrate with
  `dtYears`. Never hardcode per-turn amounts."*
- **Spine S2 Law 3:** *"3. **No instant transformation.** All change integrates over turns at
  dt-correct rates."* (`civ-sim-architecture-v3-outline.md:21`).
- **Kernel §3.4:** *"A system that hardcodes per-turn amounts fails review."*
  (`m0-kernel-spec.md:70`).
- **The dt-authority rule:** *"global dt is set by the world's *most advanced* polity's era band;
  all systems integrate rate × dt regardless (Law 3), so laggard civs simply evolve less per
  turn"* (`civ-sim-architecture-v3-outline.md:34`).
- **D-006's era table:** dt runs 10 → 5 → 3 → 2 → 1 → 0.5 sim-years (`m0-kernel-spec.md:17-23`).

### 3.2 The rulings that collide (verbatim)

- §5: *"remains elevated around its peak for several turns"*.
- §16:
  - *"Normal world: - Action Capacity refreshes according to the strategic turn."*;
  - *"Involved military agents receive Action Capacity according to the War Pulse cadence."*

### 3.3 Evidence (derivation)

**The surge's peak.** "Several turns" at peak is decades at dt 10 and a few years at dt 0.5.

**The dt-authority rule makes it unequal.** A laggard that enters an Age while a leader holds
global dt at 0.5 would get a surge far shorter, in sim-years, than the first entrant's. That runs
against §5's own *"Every civilization receives its own Age Transition Surge"* and *"duration
should not scale dramatically"*.

**Action Capacity.** A fixed budget per strategic turn buys 20× more actions per sim-year at dt 0.5
than at dt 10. That is a per-turn amount in kernel §3.4's sense. The audit registered it:
- C18, *"a per-polity count of actions per TURN — the interval is the turn, which runs 10 y →
  0.5 y"* (`docs/architecture/pre-m5-repository-audit.md:320`);
- DD-07 (`:376`).

**Not in conflict** (both in §8):
- §17: capacity is generated per turn *from a per-year rate × dt*.
- §21: a presentation target.

### 3.4 Minimal fix options (≤3; none implemented)

**Option A — READ "TURNS" AS SIM-TIME.**
- **The ruling.** An ADR records that the ruling's durations (the surge's peak and taper) are
  sim-year quantities, which the Director expressed at a reference turn length.
- **Action Capacity** accrues per sim-year × dt and is spent per turn. This is the audit's own
  EXTENSION reading (`docs/architecture/pre-m5-repository-audit.md:354`).
- **The consequence the Director must weigh.** At dt 0.5 a turn then carries 1/20th of the
  capacity of a dt-10 turn.

**Option B — ACTION CAPACITY IS AN AGENCY BUDGET, NOT A SIMULATION RATE.**
- **The ruling.** An ADR rules that Action Capacity is a budget of decisions per decision interval
  (the strategic turn, or a War Pulse). It moves no stock, so Law 3 does not reach it.
- **What still integrates with dt.** Every physical effect an action produces (distance moved,
  work done).
- **The surge.** Its durations are still sim-years, as in A.

**Option C — DIRECTOR OVERRIDE of Law 3 and kernel §3.4,** admitting turn-denominated durations
and budgets generally.

### 3.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none edited; one interpretive ADR | DD-07, C18 | future Action Capacity and surge packets |
| B | none edited; one ADR carving a named exemption from `CLAUDE.md:18` / `m0-kernel-spec.md:70` for agency budgets | a review rule defining what counts as a rate | the same |
| C | `CLAUDE.md:18`, the Spine S2.3 text, kernel §3.4 | T4.2 and CR-001's dt-correctness work reopened | every system; the largest |

### 3.6 Recommendation — PROPOSED

- **Option A for the surge**, and **Option B for Action Capacity**.
- The ruling should also say explicitly whether a War Pulse is a fixed span of sim-time. B fits
  that reading.

---

## §4 CONFLICT FOUR — FREE, AUTOMATIC MODERNIZATION vs LAW 1, LAW 3 AND D-011 §2

### 4.1 Frozen items in conflict

- **Spine S2 Law 1:** *"Every aggregation, migration, or conversion operator must conserve
  exactly"* (`civ-sim-architecture-v3-outline.md:19`).
- **`CLAUDE.md:16`:** *"people/money/goods change ONLY via `Ledger.Transfer`/`Ledger.Flow`."*
- **Spine S2 Law 3:** *"No instant transformation."* (`civ-sim-architecture-v3-outline.md:21`).
- **D-011 §2, which makes equipment a strategic-sim quantity with a history:**
  - `BattleSetup` reads *"formation rosters (real manpower from real cohorts), equipment, supply
    state"*;
  - `BattleOutcome` returns *"equipment losses"*;
  - (`d011-battle-layer-addendum.md:26-27`).

### 4.2 The ruling that collides (D-043 §8, verbatim)

- *"The upgrade: - is automatic - is free - does not require the player to manually rebuild the
  unit - should eventually use historically/causally reasonable changes in attack, health, range,
  etc. - must not be implemented as arbitrary doubling of stats"*.
- *"Age transition / unit modernization → existing obsolete formations can be automatically
  converted into the newly appropriate formation"*.

### 4.3 Evidence (derivation)

- **The people are conserved.** They are the same soldiers, so D-037 A1 holds.
- **The equipment is not, if it is a good.** D-011 §2 makes equipment a real quantity with
  losses. A "free" conversion then sources the successor's equipment (rifles for crossbowmen) with
  no Ledger flow, which breaks Law 1.
- **It is also instant.** Every obsolete formation converts at the moment of transition, which is
  Law 3's "instant transformation" at scale.
- **The discrete Age flip (§4) is weaker.** It is a latch changing state with no stock attached,
  so it is recorded here as a tension, not a conflict.
- **MEASURED.** No equipment stock exists in `Sim.Core` today. The only "equipment" is a
  production-side tool ratio (`Sim.Core/Systems/Production/ProductionSystem.cs:219`), so no code
  conflicts yet.

### 4.4 Minimal fix options (≤3; none implemented)

**Option A — "FREE" MEANS NO PLAYER ORDER AND NO PLAYER-PAID REBUILD.**
- **What stays automatic.** The conversion needs no order.
- **What is still sourced.** The successor's equipment comes through the Ledger from the owner's
  stocks or production.
- **What takes time.** The conversion integrates over sim-time: formations re-equip at a
  dt-correct rate, and their stats move as equipment arrives.
- **What the Director must confirm.** That "free" means free to the player, not free to the
  economy.

**Option B — FORMATION TYPE IS A LABEL; EQUIPMENT IS NOT A CONSERVED GOOD.**
- **The ruling.** An ADR rules that equipment is a derived formation attribute, not a Law 1 good,
  and that Law 3 does not govern a type label.
- **What it permits.** An instant relabel.

**Option C — DIRECTOR OVERRIDE** exempting Age-transition modernization from Laws 1 and 3, by
priced ADR.

### 4.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none | none | the future military packet: a re-equipment flow with its own conservation tests |
| B | D-011 §2's "equipment losses" become a derived quantity (an interpretive ADR) | battle-layer specs | battle packets |
| C | Spine S2.1 and S2.3 texts; `CLAUDE.md:16`, `:18` | — | conservation property tests must carve an exemption |

### 4.6 Recommendation — PROPOSED

**Option A.**

---

## §5 CONFLICT FIVE — OFF-ROAD CONTINUOUS MOVEMENT vs D-009/D-010

### 5.1 Frozen items in conflict

- *"**Trade = the network.** … One object, three jobs (movement, trade, military logistics)."*
  (`d009-d010-map-population-addendum.md:17`).
- *"armies and trade path on the graph (A* + caching — trivial at this scale)"* (`:19`).
- *"- **M4:** armies march and supply on the network graph (same object as trade)."* (`:51`).
- S8 §1 freezes the *"Scale Charter (as amended by D-009)"* (`spine-s8-governance-freeze.md:14`).
- **Supporting off-road cost, same record:** *"Terrain as continuous fields. … Sim systems *sample*
  the fields; nothing iterates "tiles.""* (`d009-d010-map-population-addendum.md:11`).

### 5.2 The ruling that collides (D-043 §15, verbatim)

- *"MobileAgents can move off-road."*
- *"Terrain and infrastructure modify movement/action cost."*
- *"Movement remains continuous x/y."*

### 5.3 Evidence (derivation)

**The collision was predicted.** The audit's readings ladder states: *"raster 2D position off the
graph → CONFLICT (D-009)"* (`docs/architecture/pre-m5-repository-audit.md:355`). §15 chooses
exactly that reading.

**Why it is a tension, not a contradiction.**
- None of the three lines says "only".
- ¶1 makes terrain continuous fields that systems sample.

**A premise in a closed record goes too.** D-039 E6 takes its premise from D-009: *"Armies march
the same graph"* (`d039-command-fog-and-siege.md:162-164`). §15 removes that premise.

**The tree.** A movement-cost raster already exists (`Sim.Core/Worldgen/Worldgen.cs:132-144`). It
has slope and water terms only (D-043 F35).

### 5.4 Minimal fix options (≤3; none implemented)

**Option A — THE GRAPH IS THE FAST ROUTE AND THE LOGISTICS OBJECT; OFF-ROAD SAMPLES THE TERRAIN.**
- **The ruling.** An interpretive ADR reads "on the graph" as the default route and the supply
  object.
- **Movement.** Off-road movement samples D-009 ¶1's terrain fields at terrain-modified cost.
- **Supply.** It still flows on the network.

**Option B — OFF-ROAD IS THE NETWORK'S LOWEST MODE.**
- **The ruling.** Off-road travel runs over terrain-derived lattice edges, treated as the
  network's cheapest tier. The shipped lattice is 16 km per node
  (`docs/architecture/pre-m5-repository-audit.md:121-123`).
- **Positions** stay continuous along those edges.
- **"One object"** then holds literally.

**Option C — DIRECTOR OVERRIDE** amending the D-009 charter's movement clause, by priced ADR.

### 5.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none edited; one interpretive ADR | a premise note on D-039 E6; the audit's C19/C22 re-read; D-043 F34's stale "ruled shape" lines | the future movement packet |
| B | none edited; but the Scale Charter's "network 1k → ~30k edges" (`d009-d010-map-population-addendum.md:19`) must be re-measured against lattice-edge counts | C20 widened | the network, pathing and catchment packets |
| C | the D-009/D-010 text and the S8 §1 Scale Charter entry | D-039 E6, D-040 B4 | movement and supply |

### 5.6 Recommendation — PROPOSED

- **Option A.** Choose Option B only if a measurement shows the charter's edge budget holds.
- **Sea movement** is a separate matter. It collides only with non-frozen D-040 B4 and is recorded
  in D-043 F12.

---

## §6 CONFLICT SIX — WAR PULSES INSIDE THE STRATEGIC TURN vs THE KERNEL CONTRACT, THE SUB-STEP RULE AND D-011

### 6.1 Frozen items in conflict

- **Kernel §3.2:** *"Systems read `Prev`, write `Next`. One-turn lag is therefore the default"*
  (`m0-kernel-spec.md:66`).
- **Kernel §3.4, the clock:** a clock with one dt per turn (`m0-kernel-spec.md:70`).
- **The sub-step rule:** *"sub-stepped systems (epidemics, battles, panics) read only frozen
  turn-start state plus their own sub-state. No mid-turn reads of other systems' fresh writes."*
  (`civ-sim-architecture-v3-outline.md:33`).
- **D-011:** *"Strategic turn pauses during command; battles read frozen turn-start state per the
  sub-step rule."* (`d011-battle-layer-addendum.md:34`). It also defines command pulses (`:10`).

**Open or non-frozen ground on the same subject:**
- CR-006 §1, mid-turn hand-backs: *"Status: OPEN"* (`docs/adr/cr-006-continuous-time-and-campaign-epoch.md:3`);
- D-039 E3 (`d039-command-fog-and-siege.md:143-147`);
- D-042 §11 (`d042-empire-and-player-control-addendum.md:190-194`);
- the M4 exit fence (`docs/m4-exit-inventory.md:321-322`).

### 6.2 The ruling that collides (D-043 §16, verbatim)

- *"During active war: - War Pulses operate inside the strategic turn."*
- *"- Involved military agents receive Action Capacity according to the War Pulse cadence."*
- *"- Battles, movement, retreats, territorial changes, etc. can occur within War Pulses."*
- *"- Construction and normal civilization processes continue during war."*

### 6.3 Evidence (derivation)

**The sub-step rule already admits sub-stepped systems.** It names battles. War Pulses fit the
frozen contract if, and only if, three conditions hold:
1. they are resolved inside the military system's own step;
2. no other system reads their results before the next turn;
3. no control returns to the player between pulses.

**Conditions 1 and 2 cover most of §16.**
- *"Construction and normal civilization processes continue during war"*: they run on turn-start
  state.
- A territorial change made inside a pulse reaches other systems at the next turn, by the default
  one-turn lag.

**Condition 3 may fail.** §16 hands Action Capacity to agents *"according to the War Pulse
cadence"*, and §10 says agents spend it on their own actions. If the player or AI issues orders
per pulse:
- that is CR-006 §1's mid-turn hand-back;
- it also contradicts D-039 E3's explicit *"the player does not play out months of movement"*
  (D-043 F11).

### 6.4 Minimal fix options (≤3; none implemented)

**Option A — A WAR PULSE IS A SUB-STEP OF THE MILITARY SYSTEM.**
- **Inside one Step,** pulses resolve by AI or delegated resolution, and the resolver spends each
  pulse's Action Capacity.
- **Player orders** are persistent directives set at the turn boundary. D-042 §6.2 has the form:
  *"A persistent directive is state describing a desired ongoing configuration"*
  (`d042-empire-and-player-control-addendum.md:104-105`).
- **Battle command** stays D-011's existing pause.

**Option B — RULE TOGETHER WITH CR-006 §1.** If the player acts per pulse, CR-006's options
apply: A sub-stepping, B date-stamped presentation, or C interrupt-as-boundary.

**Option C — DIRECTOR OVERRIDE** of kernel §3.2/§3.4, adding a sub-turn coordinate. This has
CR-006 option A's blast radius.

### 6.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none | a reconciliation note on D-039 E3 (the player decides between turns) | the future war-layer packet; replay pins for per-pulse resolution |
| B | per CR-006 | per CR-006 | per CR-006 |
| C | kernel §3.2/§3.4; the sub-step rule | D-039 E3, D-042 §11, the M4 exit fence | every golden re-pins (CR-006 §1.4) |

### 6.6 Recommendation — PROPOSED

**Rule this jointly with CR-006 §1.** Choose Option A if the Director did not intend per-pulse
player control.

---

## §7 CONFLICT SEVEN — CATALOGUE SPECIAL PEOPLE vs D-010 NOTABLES AND LAW 1

### 7.1 Frozen items in conflict

- **D-010:** *"**notables** put faces on it when it matters — the demagogue who leads the uprising
  emerges *from* the aggrieved bucket, named, with traits."* (`d009-d010-map-population-addendum.md:29`).
- **S8 §1** freezes *"D-009/D-010 (three-layer world, bucket+notables population)"*
  (`spine-s8-governance-freeze.md:16`).
- **`CLAUDE.md:16`**, and Spine S2 Law 1 (`civ-sim-architecture-v3-outline.md:19`).

**Ratified but not S8-frozen (D-043 F13 and F14):**
- M4 R-1: *"A NOTABLE IS A PERSON. Extracted from the bucket via `Ledger.Transfer`"*
  (`docs/m4-spec.md:413-414`);
- D-037 A1: *"NOTHING SPAWNS"* (`d037-emergent-polities.md:14-15`).

**Shipped:** `NotableRow` (`Sim.Core/State/WorldState.cs:635-636`) and `NotableLifecycle`
(`Sim.Core/State/NotableLifecycle.cs:28-30`).

### 7.2 The ruling that collides (D-043 §11, verbatim)

- *"Historical special people are temporary MobileAgents."*
- *"- can be recruited by any player/AI player capable of recruiting them"*.
- *"- have finite lifespans - eventually die/disappear/retire - do not require persistent
  relationship/allegiance simulations"*.
- *"Germany may recruit Gandhi."*

### 7.3 Evidence (derivation)

**Headcount.** A special person who appears, is recruited and then *"die[s]/disappear[s]/retire[s]"*
changes headcount, unless every appearance and exit is a Ledger operation.

**The shipped lifecycle** has Born (bucket → notable), Dies (a sink) and Defects. "Disappear" and
"retire" are not among them, and the code states: *"A notable cannot be created from nothing,
cannot vanish"*.

**Two kinds of named person.**
- D-010's notables *emerge from* buckets.
- §11's are authored catalogue entries.

**Recruitment across polities** ("Germany may recruit Gandhi") is the purchase event. Its
consideration *"cannot exist until money does"* (`docs/m4-spec.md:431-433`).

### 7.4 Minimal fix options (≤3; none implemented)

**Option A — A SPECIAL PERSON IS A NOTABLE WITH A CATALOGUE IDENTITY.**
- **Recruitment.** One person is extracted via `Ledger.Transfer` from a bucket of the recruiting
  polity, or of the host settlement.
- **Identity.** The person carries the catalogue's name, skills and abilities: §11's Special
  Person and Ability definitions.
- **Exits.** Retirement returns the person to a bucket by Transfer; death is the existing sink.
- **What holds.** Law 1, D-037 A1 and D-010 all hold.
- **What the Director must confirm.** R-1's allegiance lifecycle (defection, purchase, falling
  out) is simply not exercised for special people.

**Option B — A SPECIAL PERSON IS NOT A PERSON.**
- **The ruling.** An ADR rules that a catalogue special person is an abstract agent carrying no
  population, like an office. It then falls outside Law 1's "people".
- **What changes.** D-037 A1 is distinguished in writing, and the demo's person tokens change
  meaning.

**Option C — DIRECTOR OVERRIDE** of D-010's notable clause, by priced ADR.

### 7.5 Blast radius

| option | frozen docs | non-frozen docs | tests / packets |
| --- | --- | --- | --- |
| A | none | a note on R-1's lifecycle scope | the future special-person packet: a catalogue linkage on the notable row (a schema change) and its conservation tests |
| B | none edited; one interpretive ADR on Law 1's "people" | D-037 A1; the world layer's person tokens | the same packet, without Ledger |
| C | the D-010 text | M4 R-1, D-037 A1 | notable tests |

### 7.6 Recommendation — PROPOSED

**Option A.**

---

## §8 WHAT IS *NOT* IN CONFLICT — recorded so it is not re-litigated

Each item below holds under the reading stated. Where the reading is the filing agent's, it is
INFERRED, and the Director may overrule it.

- **§6, permanent development.** No frozen item forbids it. It forbids only Age-driven erasure;
  real degradation stays possible.
- **§12, knowledge belongs to the polity.** Consistent with D-042 §9.2, *"Knowledge is ultimately
  **Empire-scoped**"* (`d042-empire-and-player-control-addendum.md:165-166`).
- **§17, construction capacity is not bankable.** Consistent with Law 3 as shipped:
  - capacity is *"construction share × adult population × dtYears"*
    (`Sim.Core/Systems/Construction/ConstructionSystem.cs:50-51`, computed at `:137`);
  - there is *"no partial draw, no banked progress"* (`:40-41`).

  Two related points:
  - The shipped PathBuild bank diverges from §17, but it is not frozen (D-043 F19).
  - D-004's *"remainder accumulator"* (`m0-kernel-spec.md:11`) carries sub-unit fractions of flows
    that were used. INFERRED: it is not unused capacity.
- **§19, buildings embedded in the settlement.** Consistent with *"Districts inside remain
  abstracted"* (`d009-d010-map-population-addendum.md:15`) and D-038 H2's "not an object at
  coordinates".
- **§20, causal viability.** It restates D-042 §7.3 (`d042-empire-and-player-control-addendum.md:141-142`)
  and is consistent with Spine Law 5 (`civ-sim-architecture-v3-outline.md:23`).
- **§3, "The Trees", against the Spine's knowledge row "no tree"** (`civ-sim-architecture-v3-outline.md:84`).
  Consistent while the Trees stay player-facing lenses over computed state, with no node granting
  capability by itself (D-040 B3).
- **§10, a universal Action Capacity with agent-specific actions.** It can be squared with two
  frozen items:
  - the kernel's one-owner-per-table rule (`m0-kernel-spec.md:54`);
  - the fixed order record, *"OrderRecord shape {Turn, ActorId, Kind, TargetId, Amount} is
    fixed"* (`Sim.Core/Kernel/OrderLog.cs:4-5`).

  INFERRED: one system owns each agent table; actions enter as orders; the `OrderKind`
  vocabulary may grow per agent type. §10 bans an action list shared by every agent, not an
  enumeration of kinds.
- **§14, cultural groups, against frozen texts.** Their unrest, culture and diplomacy effects
  touch:
  - D-010's riot-propensity inputs, *"f(grievance, density, unemployment, food price shock,
    agitator notables present, policing)"* (`d009-d010-map-population-addendum.md:36`);
  - culture as a bucket dimension (`civ-sim-architecture-v3-outline.md:44`);
  - interest-computed diplomacy (`:86`).

  INFERRED: reconcilable if each effect enters through a listed input, such as grievance, an
  agitator notable or cultural distance (D-043 F18).
- **§5 "begins immediately upon Age transition", against the double buffer**
  (`m0-kernel-spec.md:66`). Consistent if "immediately" means the first turn after the transition
  is recorded. A same-turn start needs a declared kernel-ordered edge (INFERRED).
- **§21 and §22.** Presentation targets. S8 §1 lists *"UI layouts, chronicle text, names,
  presentation polish"* as LIVING (`spine-s8-governance-freeze.md:23`).
- **§1's names against D-006's era-band names** (`m0-kernel-spec.md:17-23`). A term collision, not
  a law conflict (D-043 F.4).
- **"Milestone" in §2/§3 against the frozen ladder** (`spine-s8-governance-freeze.md:17`).
  Terminology only. No ruling moves a ladder milestone (D-043 F23).

---

## §9 RECOMMENDATION SUMMARY (S8 §3 field 5) — PROPOSED

**The order in which to rule:**
1. **§1 first.** §2's option B depends on it, and CR-009 can be absorbed there.
2. **§3** before any Action Capacity or surge packet.
3. **§6 together with CR-006 §1.**
4. **§4, §5 and §7** before their packets.

**Per collision:**

| § | collision | recommendation |
| --- | --- | --- |
| 1 | Age as an input vs Law 4 | A — rule the Age computed state |
| 2 | surge residual vs Law 2 | A — the carrier form (B as fallback) |
| 3 | turn-denominated quantities vs Law 3 | A for the surge; B for Action Capacity |
| 4 | free modernization vs Laws 1/3, D-011 §2 | A — free to the player, sourced and dt-integrated |
| 5 | off-road movement vs D-009/D-010 | A — graph for speed and supply; terrain sampled off it |
| 6 | War Pulses vs kernel / sub-step rule / D-011 | rule with CR-006 §1; A if no per-pulse player control |
| 7 | special people vs D-010, Law 1 | A — a notable with a catalogue identity |

**After the ruling** (`spine-s8-governance-freeze.md:37`):
1. ADR-028, the next free number, records the ruling.
2. The blast-radius checklist is executed.
3. The freeze resumes.

**No collision is resolved here. Nothing is implemented. Awaiting ruling.**
