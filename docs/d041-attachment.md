# D-041 — ATTACHMENT: WHAT MAKES A POPULATION STAY

**Director design ruling.** Extends **D-040**. Binds **M5** and **M8**. **Designs nothing** — no
mechanism, no constant, no curve.

**Citations verified against the tree at `c09cabb`.** Disagreements are recorded in **PART F**.

---

## PART A — THE GAP

**A1.** D-040 Part C rules **control** decaying with administrative distance, contested where
claims overlap. Both describe **the STATE'S REACH**.

**A2. Missing: the population's own attachment to the polity holding it.** Distance-decayed control
explains why remote provinces are **weakly governed**. **It does not explain why some remote
provinces never leave.**

**A3.** The director's cases, recorded as the reference class:

- **Kerala** — a foreign power annexing it today would face a population that **does not defect**,
  because two centuries of shared institutions built an attachment distance cannot erode.
- **French Guiana** — held initially by **military control**, later **accepted as normal**.
  Attachment **ACCUMULATED under sustained control**.
- **A newly founded settlement on a foreign border** — adjacent economic and cultural pressure, no
  accumulated attachment, **defects readily**.

**A4. All three are ONE MECHANISM at different points on its curve.**

---

## PART B — ATTACHMENT IS A STOCK, CONTROL IS A FUNCTION

**B1. THE DISTINCTION IS THE RULING.** **Control is COMPUTED each turn** from distance,
infrastructure and institutions — it responds immediately and symmetrically. **Attachment is an
ACCUMULATED STOCK** held by a population, **slow to build and slow to lose**.

**B2.** That asymmetry is why empires do not all shatter at the same radius. **Two provinces at
identical distance behave differently if one has been held for two centuries and the other for two
years.**

**B3. ATTACHMENT IS HELD BY A POPULATION, NOT BY A PLACE.** It **travels with people** — migrants
carry attachment to where they came from. **Territory conquered with its population intact is not
the same as territory settled fresh.**

**B4. DO NOT MAKE THIS A DECAY CURVE ON DISTANCE.** That is D-040's control term and it already
exists. **Attachment's inputs are TIME UNDER CONTROL and what the polity DOES; distance enters only
through control, never directly.**

---

## PART C — WHAT MOVES IT

**C1.** Director's list, **recorded as candidate drivers, none designed here**:

- **sustained control over time** — the primary accumulator;
- **shared institutions and language**;
- **directed expenditure** — what a state visibly spends on a place;
- **war, in both directions** — external threat builds attachment; defeat and levy erode it;
- **media and information reach**, once such a system exists;
- **economic integration** — a place trading with the core is more attached than one trading with a
  neighbour.

**C2. THESE ARE GOVERNED, NOT AUTOMATIC.** Directed expenditure and media are things a player
**DOES**. **That is what makes attachment a LEVER rather than a COEFFICIENT**, and it is why this
belongs with the governing loop.

**C3. ADJACENCY PRESSURE runs the other way.** A settlement near a foreign polity's settlements is
subject to that polity's cultural and economic pull. **D-040 C5 already requires contested claims
to be expressible; this is the POPULATION-SIDE counterpart.**

---

## PART D — WHERE IT LANDS

**D1. M5 — the governing loop.** Attachment as a **governed quantity**: expenditure, institutions,
the levers a player pulls. **C2 is the reason.**

**D2. M8 — politics.** Defection, secession, the periphery leaving. **D-021's release valves carry
the machinery** (`docs/d021-stability-doctrine.md:17`, *"THE RELEASE VALVES"*).

**D3. NOT M4** — M4 ships the claim/control/recognition data model. Attachment is a **fourth
quantity** and is not required for that model to be correct.

**CHECKED, AS DIRECTED — AND THE ANSWER IS YES, WITHOUT A RETROFIT.** T4.3's fence
(`docs/m4-spec.md:235-250`) already forbids the three shapes that would have blocked a
population-held stock:

> **PROHIBITED 1** — control as an owner id on the place row (*"silently forecloses overlap"*);
> **PROHIBITED 2** — recognition as a flag on the polity (cannot express asymmetry);
> **PROHIBITED 3** (D-040 C3/C7) — an owner id cannot carry a decay term either; **all three must
> be RELATIONS**, recognition keyed by `(recogniser, recognised)`.

**A relation keyed by (polity, population-or-settlement) is exactly the shape attachment needs**, so
the M4 model accommodates a fourth quantity by adding a table, not by reshaping an existing one.
**One caveat, stated rather than assumed: B3 says attachment is held by a POPULATION and travels
with MIGRANTS.** M4's population is the **bucket** (`(Settlement, Culture, Religion, Class,
CohortIdx)`), and migration moves counts between buckets. **Whether attachment rides that movement
correctly is a question for the packet that builds it, not a constraint on T4.3's schema** — but a
T4.3 that keyed a fourth relation to a PLACE rather than to a population-bearing row would create
the retrofit D3 asks about. **Recorded in T4.3's fence as a NOTE, not a fourth prohibition:** no
M4 obligation is created.

**D4. NOT SCHEDULED.** Each milestone's spec proposes its own packet.

---

## PART E — WHAT THIS DOES NOT DO

**E1.** Does not amend D-040. It adds the **population-side quantity** D-040's state-side model
leaves open.

**E2. DOES NOT AFFECT SETTLEMENT SPACING.** Recorded because it was raised as a candidate:
**attachment constrains where a polity can HOLD a settlement, not where one can be PLACED.**
Nothing about defection risk prevents settlements stacking on one river mouth inside a polity's own
core. **The constraint on placement is LAND** — two settlements sharing one catchment — **and it
belongs to colonization's own model**, which is what a future removal of `minSpacingKm` waits on.
**ADR-018 keeps the spacing floor until then.**

**E3.** Designs **no mechanism, no constant, no curve**.

---

## PART F — CITATION FINDINGS (§7.12: THE TREE WINS)

**F1. D-040's control ruling is Part C, and C5 is the contested-claims clause — both hold as
cited.** `docs/d040-discovery-and-control.md` C3 rules *"do not add a `loyalty` field"* and C5 makes
contested control the overlap D-037 already requires. **D-041 is consistent with both and amends
neither.**

**F2. "D-021's release valves" — the tree's wording, used.** Not *"unrest valves"*; the same
correction D-040 F4 already recorded. Cited correctly here rather than repeating the error.

**F3. THE D3 CHECK RETURNED A STRONGER ANSWER THAN THE PROMPT ANTICIPATED.** The prompt asks
whether the M4 shape *"can accommodate a population-held stock without a retrofit"* and provides
for the answer being no. **It is yes** — because T4.3's three prohibitions already force relations
rather than owner-ids. **No M4 obligation is created**, and the only residue is the
population-versus-place note recorded in D3.

---

**HOLD FOR MERGE.** Docs only.
