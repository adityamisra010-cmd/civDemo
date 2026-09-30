# ADR-028 — VIABILITY AND SATURATION OF INFRASTRUCTURE AND INSTITUTIONS (DD-13, DD-14)

**Status:** RATIFIED — Director ruling. DD-13 and DD-14 were decided outside the tree and are recorded here so that the Technology Graph registry, and every later packet, can cite a file rather than a conversation.

**Amends:** nothing. This record is additive. It designs no mechanism and sets no constant.

**Relates to:** D-042 §7.3 (`:141`) and §12 (`:200`) (the ban on a universal `CapabilitySystem` God object), D-020 (the predicate seam), law 2 (mechanisms over modifiers), CR-003 §5.1 (no constant chosen to reproduce desired behaviour), ADR-019 Part 8 (cities: absorption as merge, local policy as settlement-scoped attribute).

---

## §1 — DD-13 = C: VIABILITY IS HIERARCHICAL AND TYPE-SPECIFIC

**1.1** Whether an infrastructure or institution can exist and function at a place is decided per type, from that type's own conditions. There is no single viability rule shared by all types.

**1.2** Viability is composed from four kinds of term, each declared by the type:

- **physical viability** — the thing can physically be built and run here;
- **local capacity** — the settlement or site can supply what it needs (labour, inputs, catchment population);
- **marginal returns** — what an additional instance contributes, given what already exists;
- **genuine site constraints** — a real geographic or material condition (a river with head, a coast, a deposit), never an invented one.

**1.3** Hierarchy: a type may depend on another type's presence (a lighthouse on a harbour, a dry dock on a harbour, a university on a library). The dependency is declared on the dependent type.

**1.4 FORBIDDEN:**
- a universal hardcoded maximum count of any building, infrastructure or institution;
- a universal `CapabilitySystem` or any single system that owns viability for every type (D-042 §7.3, §12).

## §2 — DD-14 = B: SATURATION IS THRESHOLDS PLUS DIMINISHING RETURNS

**2.1** Saturation is represented through **viability thresholds** (below a threshold, a type cannot be sustained at a place) and **diminishing returns** (each additional instance contributes less).

**2.2** Saturation is **derived**, never stored as a flag. It is a reading of the marginal-return curve that the consuming system owns.

**2.3 FORBIDDEN:** arbitrary hardcoded maximum counts, in any form — per settlement, per polity, per type.

**2.4** Every threshold and every curve is derived by the packet that builds the mechanism, from a stated reference class, fixed before measuring its effect (CR-003 §5.1).

## §3 — CONSEQUENCE FOR THE LIFECYCLE

The Technology Graph registry distinguishes, and this record binds:

| state | kind | source |
|---|---|---|
| LOCKED | derived | requirement predicate false |
| AVAILABLE | derived | requirement predicate true and construction possible |
| UNDER_CONSTRUCTION | stored | the existing ConstructionSystem project queue |
| ACTIVE | stored | a built instance |
| MATURE | stored | an accumulated maturity quantity on the instance |
| SATURATED / DIMINISHING | derived | §2 — never a stored flag |

**Researching a technology never creates a building.** It can make one AVAILABLE.

**Not every building requires a technology.** A type may be baseline (founding-turn), technology-unlocked, capability-, resource-, site- or settlement-condition-gated, institution-dependent, or a combination. A null technology requirement is valid and must not be filled with an invented prerequisite.

## §4 — RECORDED STATE OF THE TREE AT RATIFICATION

- `ConstructionSystem.cs:92-97` queues a project when the ordering actor controls the settlement (`:92-93`) and the project id exists (`:95`). **There is no technology or availability predicate.** LOCKED and AVAILABLE are therefore not yet expressible in the simulation. The packet that wires availability adds that predicate and must satisfy §1 and §2.
- `granary` (project 1) and `workshop` (project 2) are baseline constructibles.
- Extraction is `ProductionSystem.FromDeposits`, reading deposits with no constructed asset. A mine is not a prerequisite for extraction.
- Construction output is inert (arch-E F23, measured on `b6820e2`, re-verified on `de5e00e`): `Structures` is written by `ConstructionSystem` and serialized into the world hash, and read by no system for any effect. The granary's storage bound is the config constant `GranaryYearsOfDemand` (`SimConfig.cs:134-138`), which does not consult the structure count.

## §5 — WHAT THIS DOES NOT DO

Designs no viability function, no curve, no threshold value. Adds no code, data, test or golden. Moves no constant. Does not reopen D-042. Does not rule the institutions layer.
