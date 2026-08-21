# NOTABLES — FORWARD DESIGN INVENTORY

**Inventory only. No implementation, no schema, no mechanism decided here.** Its purpose is to stop
T4.8's structural work becoming a dead end because the generation architecture was never considered.
Nothing in this document authorizes a packet.

---

## §1 WHAT T4.8 ALREADY SHIPPED (merged, `aed73932`)

The **structural carrier and lifecycle**, and deliberately nothing else:

- `NotableRow(Id, Settlement, Allegiance, CohortIdx, Count)` — a **conserved population stock**,
  because R-1 ruled that a notable is a PERSON.
- **Born**: `Ledger.Transfer` of one person out of the bucket. **Dies**: `Ledger.Flow` sink under
  `ReasonIds.Deaths`. **Defects**: `Ledger.Transfer` to a new row, keeping the same `NotableId`.
- Identity continuity across every lifecycle event, with no bookkeeping outside the Ledger.

**What it deliberately does NOT provide:** who the notable is, how notables emerge, personality,
skills, capabilities, historical identity, trigger conditions, promotion frequency, presentation,
caps, or cultural context. Those are later gameplay/content systems and inventing them inside T4.8
was explicitly out of scope.

## §2 THE PRODUCT CONSTRAINT TO DESIGN AGAINST

> **Notables must not spam.**

Stated operating envelope: **approximately 3–4 newly generated notables per civilization per turn,
maximum**, subject to the eventual emergence mechanism and to exceptional historical events.

**This is a design constraint on the future emergence system, not a reason to modify T4.8.** It is
recorded now because a budget of this shape has to be designed in from the start — it cannot be
retrofitted onto a mechanism that already generates per-eligible-person.

## §3 THE PIPELINE THE EVENTUAL SYSTEM LIKELY NEEDS

```
eligible population → candidate generation → selection/ranking
                    → per-civilization birth budget → notable instantiation
```

The budget is a **stage**, not a post-hoc filter. Placing it after selection is what keeps the cost
bounded and the outcome deterministic.

## §4 PATHOLOGIES THE EVENTUAL SYSTEM MUST PREVENT

Each of these is a failure mode a naive implementation actually produces:

- every eligible person becoming a notable;
- repeated generation from the same population;
- infinite re-generation after death;
- one settlement dominating all notable creation;
- random rolls producing pathological bursts;
- identity duplication;
- notable immortality through accidental respawning.

## §5 QUESTIONS THE EVENTUAL PACKET MUST ANSWER

Not answered here, and not to be answered by invention:

**Identity & origin** — archetypes; identity generation; settlement association; allegiance; cohort;
historical-role generation; origin and social position.
**Lifecycle** — emergence triggers; death triggers; defection triggers; lifespan; succession.
**Capability** — attributes; special capabilities; reputation; influence; factional relationships;
motivations.
**System surface** — event hooks; player visibility; information uncertainty; notable density;
duplicate prevention; per-civilization generation limits.

## §6 THE ARCHITECTURAL DISTINCTION WORTH FIXING NOW

**A settlement may be generated dynamically** during worldgen or simulation, because it is a
spatial/world-state entity: a location, a catchment, a population container.

**A notable is not that.** A notable should have a **generated identity and a causal emergence
history** — it should not be `Matt123 + random stats`. The eventual system should support generated
individuals carrying identity, origin, social position, allegiance, attributes, motivations,
capabilities, relationships, historical context, and the conditions under which they emerged.

**But NOT historical real-person simulation** unless a later packet explicitly decides that.

## §7 WHAT ALREADY CONSTRAINS THE DESIGN, from the tree

Whoever opens the emergence packet inherits these and should not re-litigate them:

- **Names are presentation, not mechanics.** `NameRegistry` is a pure function of
  `(worldSeed, id, salt)` over data phonology with **no `RngRegistry` stream**, and per ADR-001 the
  registry lives outside sim tables. Identity generation must not put names into sim rows.
- **All randomness via `RngRegistry` streams**, with RNG state in `WorldState` (law 5). A generation
  mechanism that rolls must take a named stream.
- **The D-020 emergence pattern already exists** and is the natural fit: publish `Variables` → a
  data-driven `Predicate` (closed grammar, no arithmetic) evaluated against PREV → latch → warm-up
  guard. Artisan emergence is the worked precedent.
- **The scale-invariance law** (`Variables.cs`): any predicate needing scale sensitivity must publish
  an **absolute** quantity, never another ratio. A per-civilization budget is inherently absolute.
- **Conservation.** Notables are people. Every creation is a `Transfer` out of a bucket, so a birth
  budget is also a bound on population movement, and the law-1 audit covers it already.

## §8 STATUS

**FINDING / INVENTORY ONLY.** No packet is authorized by this document. When an emergence packet is
opened, §3 and §4 are its acceptance shape and §7 is its inherited fence.
