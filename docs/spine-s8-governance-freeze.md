# SPINE S8 — GOVERNANCE: THE FREEZE PROTOCOL
### Director directives ratified 2026-07-16. This document joins the Spine and is itself frozen at M0 exit.

**The three directives, codified:**
1. Architecture freezes at M0 exit; amendment only on discovered contradiction.
2. Documentation exists at most one milestone ahead of implementation.
3. A system proves itself in code before any later system is specified.

---

## 1. FREEZE PERIMETER

**FROZEN at M0 exit (change requires a Contradiction Report + director ADR):**
- Spine S1–S5 and this S8: Design Laws v3, Kernel Contract, Scale Charter (as amended by D-009), stack decisions.
- Kernel code contract: `ISimSystem`, `SimContext`, double-buffer model, `Ledger` API, RNG regime, snapshot/hash format, banned-constructs list.
- All CLOSED decision-log entries: D-001…D-008, D-011 (dual-resolver battle contract), D-009/D-010 (three-layer world, bucket+notables population), D-018 (class/needs frame).
- The milestone ladder order M0→M11+ and each milestone's exit-criteria *definitions*.

**LIVING (change freely, no procedure):**
- All data files and every `TUNE` parameter — tuning is play, not amendment.
- The current milestone's spec and task packets (implementation reality may reshape packets mid-milestone).
- Open decisions D-012…D-017, D-019, D-020 — **closing an open decision at its named spec is not an amendment**; it is the plan.
- UI layouts, chronicle text, names, presentation polish.

## 2. WHAT COUNTS AS A CONTRADICTION (the only key that opens the freeze)

- **Internal:** two frozen commitments provably conflict (e.g., a law vs a contract).
- **Empirical:** a frozen commitment fails in code — determinism unachievable as specified, perf gate unreachable at charter scale, conservation impossible under a mandated pattern. Evidence = failing test or bench, attached.
- **Law conflict:** an implemented system cannot satisfy a Design Law without violating another.

**What does NOT count:** a better idea, a taste change, a new feature wish, a cooler architecture read about online. These are not deleted — they are **parked**: appended to the *Post-Slice Amendment Queue* (`docs/queue.md`, one line each) and reviewed **only at the M10 Vertical Slice gate**, the single scheduled moment the project re-examines itself. Between now and the slice, the queue is write-only.

**Director override:** exists — it is your project. But it costs a written ADR stating what breaks, which tests and docs change, and the schedule price. A freeze with a free override is theater; a freeze with a priced override is governance.

## 3. AMENDMENT PROCEDURE (lightweight — one person, one template)

**Contradiction Report** (`docs/adr/cr-NNN.md`), five fields: (1) frozen items in conflict · (2) evidence (test/bench/derivation) · (3) minimal fix options, ≤3 · (4) blast radius: docs, tests, packets touched · (5) recommendation. Director rules → ADR records the ruling → blast-radius checklist executed → freeze resumes.

## 4. DOCUMENTATION CADENCE (directives 2+3, strict form)

The pipeline is sequential by design:

```
implement M(n) → exit criteria GREEN → write + ratify M(n+1) spec → cut packets → implement M(n+1)
```

- The **only** system-spec documents in existence at any moment: the one being implemented, and — after proof — the next one. Nothing beyond n+1 is ever written. (My earlier "draft one ahead during implementation" is superseded: spec-writing for n+1 begins only after M(n)'s exit gate passes, so every spec is written with the previous system's code-truth in hand.)
- **Exempt document classes:** decision records/ADRs (they are the amendment mechanism), the queue, and hotfix specs spawned by an approved Contradiction Report.
- **Proof standard** per system = its packet acceptance tests green + CI green + (from M2 onward) the autoplay soak green + any calibration hooks defined for it. Already encoded in each milestone's exit criteria; no new definition.

### 4.1 SPEC FORMAT (director ruling 2026-07-26, recorded in `docs/adr/adr-014-spec-format-foundations-audit.md` — effective from the M4 spec onward)

**Origin.** The T3.2b detour existed because the M3 spec specified new systems without auditing the
constants they would stand on. A yield constant denominated per lattice node was consumed as if it
were per km²; the catchment budget that compensated for it was a code constant no tuning pass could
see. Both were catchable on paper. Neither was in anybody's packet. The price of that omission
is the vindicating example (CR-003 ruling §6(e)): a TWO-PACKET emergency detour — T3.2b plus the
directed T3.4b it forced — and two CRs, where a foundations audit as packet one would have caught
it as planned work.

**Aim.** Fewer AVOIDABLE surprises. Reality correcting the spec — CR-001, CR-002, the ghost harvest
— is the system working, not a failure to plan; this format does not ask a spec to be omniscient
and adds no new process gate. It asks four questions that can be answered before code exists.

**Effective from the M4 spec onward.** M3 is mid-flight and is NOT retro-fitted: its remaining
packets — including T3.10, which specs new corridors — run under the pre-amendment format.

Every milestone spec from M4 onward carries all four. Item 1 is a task packet (the first one in
§4); items 2–4 are spec content. The existing spec skeleton (§1 decisions closed · §2 scope fence ·
§3 system notes · §4 task packets · §5 exit criteria · §6 governance) is otherwise unchanged, and
each requirement below names where it lives.

**1. FOUNDATIONS AUDIT — packet one.** `T(n).1` is an explicit pass over the EXISTING quantities
the milestone's new systems will stand on.

  *Scope is DEPENDENCY, not perturbation — and it is ENUMERATED FROM CODE, not asserted.* In scope:
  every existing constant AND every denominated state-table field that the milestone's §3 equations
  consume, followed transitively through each field's producer. "Constant" means any numeric
  literal entering a sim equation, whether TUNE data or code-resident — `CatchmentSystem.TravelBudget`
  lived in no config file, which is how it stayed unexamined for three milestones. A "consumption
  site" is any reader — a system, the metrics/autoplay layer, a corridor computation, a view-model —
  not only systems: CR-002 was visible only by comparing `FarmingSystem` against `AutoplayMetrics`,
  which is not a system and would never appear in a systems-only audit. The coupling map (4 below)
  is CHECKED AGAINST this enumeration; it does not define it. The distinction is load-bearing:
  a perturbation-scoped or map-scoped audit would have exempted the ruling's own origin case, since
  `TravelBudget` reached M3's systems only through `EffectiveArableKm2`.

  For each quantity in scope, four answers:

  - **(a) What it means in REAL units — and whether the value is PHYSICALLY POSSIBLE there.**
    An internal unit is not an answer: "15 cost units" only restates the number, and "…which is
    how many km?" yields 240 km of ideal-ground reach, at which point nobody defends the value.
    The second half is the check that actually killed the old yield constant, and it needs no
    disputed parameter and no corridor: restate the value in one independent unit system and ask
    if it is possible — 28.0/node re-read as sown yield failed to return a 120–200 kg/ha sowing
    rate by ~700×. PASS STATE for pure coefficients: "dimensionless reconciler between X and Y"
    (e.g. `attractivenessFoodWeight`) is a valid, closing answer to (a); such a quantity's
    remaining obligations are (b) and (c).
  - **(b) Whether its denomination matches its consumption site** — the identical question at both
    ends of the pipe: what the producer writes, what the consumer multiplies. CR-002 was two
    consumers reading one field in two denominations and only one of them converting.
  - **(c) Whether it was DERIVED or merely CHOSEN** — and if chosen, from what, by whom, when.
    WARNING SIGN — "retuned to preserve behaviour": a placeholder that is later deliberately tuned
    stops looking like a placeholder, because a value someone adjusted appears considered.
    `yieldPerFarmlandPerYear` entered bare at T1.5 and was retuned 40 → 28 at T1.6 solely to hold
    the no-order production rate constant; it then survived three milestones and a 16× error
    unquestioned. The audit must treat "was tuned at some point" as evidence of NOTHING, and ask
    only whether the value was ever DERIVED.
  - **(d) Whether it is VISIBLE TO TUNING** — TUNE data, or a code literal. A code-resident value
    cannot be examined by the process that exists to catch bad values; that visibility failure is
    why the travel budget could quietly absorb the yield fault. "Code literal" is itself a finding.

  DISPOSITIONS — fixed here so the packet can close. The audit RECORDS; it acquires no powers.
  - A (b) mismatch, or any genuine conflict with a frozen item, opens a CR under §3.
  - A physically impossible magnitude under (a) is ESCALATED to the director before any dependent
    packet builds on the value — never silently absorbed, and never silently "fixed" either.
  - The COMMON case — (c) fails but (a), (b), (d) are clean — is RECORDED as "chosen, never
    derived" and queued. It is NOT corrected in this packet. Most of `sim.json` predates any
    derivation discipline; this disposition is what keeps a clean-ish audit at a session instead
    of inheriting the whole config's provenance debt.
  - "A better way exists" goes to the queue (§2), as ever. The audit asks whether a value is
    traceable, denominated and possible — never whether it is optimal.

  ACCEPTANCE (the anti-rubber-stamp form — an audit that can pass by silence is not an audit):
  the deliverable is a WRITTEN TABLE, one row per in-scope quantity, no unexplained omissions.
  Every row answers (a)–(d) EXPLICITLY — "never derived" is a stated answer, never an omitted
  one — and every mismatch or impossible-magnitude row carries its CR number or escalation.
  The director adjudicates the table, not the presence of headings. This is the packet that would
  have caught CR-002 as planned work.

*On packet ordering:* several specs put a golden-moving change first so goldens move once (M3's
T3.1). That convention survives — an audit is cheap and produces findings, and any correction it
justifies then lands in the same early window, so audit-then-correct-then-build still moves goldens
once.

**2. DIMENSIONAL DECLARATION — §3 system notes.** Every new quantity declares its units at
introduction, and every core equation is checked for unit balance on paper before implementation.
Both sides of a `min()`, both terms of a sum, and the numerator and denominator of every ratio.

  This requirement's value is not catching imbalance on the page — it is making the check
  PERFORMABLE at all. What actually went wrong at T1.5: the yield constant's NUMERATOR was declared
  ("1 food = 1 person-year, D-015"). It was `EffectiveFarmland`, introduced a packet earlier at
  T1.4, that never declared a unit at all — so `harvest = farmland × yield` was not a check that
  failed, it was a check nobody could run. Once both sides carry units the imbalance is a one-line
  read.

  ANTI-SELF-CERTIFICATION: for any quantity produced by EXISTING code, the declared unit is READ
  OFF the producing expression and CITED (file + expression), never asserted from the author's
  memory. A unit the author assigns is self-certifying — "everyone believed the left operand was
  km²" is precisely how the fault survived — and reading it off the producer is what turns this
  requirement from a tautology into a check. Units belong in identifiers too, but that is a code
  convention (ADR-013), not this document's business.

**3. CORRIDOR INDEPENDENCE — wherever the corridor is introduced (§3 or §5).** No corridor is
specced without naming (i) what it is independent OF, and (ii) how a change in the measured system
could make it fail. A corridor whose denominator moves with the measured quantity is
SELF-REFERENTIAL and is REFUSED at spec time, not discovered later.

  This binds ANY corridor band derivation or RE-derivation, whenever it occurs — spec time,
  packet time, or acceptance follow-up. The 0.12 floor that produced the standing example arose in
  a T3.1 acceptance follow-up, not in any spec; a rule that binds only at spec authoring misses
  the moment the mirror was actually built. Every derived bound is written as an explicit formula
  over NAMED inputs, each marked *sim-measured* or *external* — which makes the test below
  mechanical instead of dependent on someone noticing.

  The refused shape has a signature worth memorising, because it is not obvious in prose and is
  algebraically fatal: deriving a bound as `P_hist / (H × s̄)` — historical population over
  (H = historical habitable area) × (s̄ = the sim's own measured mean suitability) — cancels
  against `measured = P_sim / (A × s̄)` (A = the sim catchment area), so the bound sits at a fixed
  fraction of whatever the map reports, forever. That is the CR-002 cancellation identity — the
  standing example. Two tests, both from the same CR-002 refutation and neither an extra
  obligation: **algebraic** — write the bound and the measurement as fractions and cancel; if a
  sim-measured term disappears from the ratio, the corridor is a mirror. **Counterfactual** — run
  the same derivation against the world as it stood BEFORE the change; a recipe that yields a
  different bound depending on which side of the change you run it on is not a derivation.

  A LITERAL band (no derivation on record) offers nothing to cancel — the algebraic test passes it
  trivially and proves nothing. Such a corridor discharges this requirement through (ii) alone:
  name the denominator's drivers and what change in the measured system moves them.
  `densityPerArableKm2` [0.15, 0.6] is the standing example of that failure mode — it passes the
  algebra and fails (ii): its denominator moved ~15× at T3.2b while its numerator barely moved.

**4. COUPLING MAP — a short table, §3.** Which existing constants, corridors and emergence tests
each new system perturbs. The map records the OUTBOUND direction (what the new systems move); the
foundations audit's scope is the INBOUND direction (what they stand on). Both are checked against
code — the audit against the enumerated consumption sites (1 above), the map against the
perturbation set — and neither is satisfied by the author's assertion alone. The map's second job
is the re-anchoring checklist when something moves: T3.2b's re-verification surfaced nine failing
tests by DISCOVERY — two deliberate golden re-pins, one sampling fix, and six test families
disarmed by a single fact (CR-003 §2.6) — where a coupling map would have been that list, written
before the work.

*Sequencing between item 1 and items 2/4:* the §3 dimensional declarations and coupling map are
the spec author's PROVISIONAL reading; `T(n).1` is AUTHORITATIVE over them. An audit finding that
moves a §3 equation or a map row is a revision of the current milestone's spec under §1's LIVING
clause, not an amendment — CRs are reserved for conflicts with FROZEN items.

**Not added:** further process gates, or pre-specification of what only measurement can settle.

## 5. CURRENT STATE DECLARATION

**Frozen baseline (activates on M0 acceptance):** `civ-sim-architecture-v3-outline.md` (as amended below) · `m0-kernel-spec.md` · `d011-battle-layer-addendum.md` · `d009-d010-map-population-addendum.md` · `d018-classes-and-needs.md` · this document. Where addenda amend the v3 outline (region-graph clause, milestone renumbering, walking-skeleton content), **the addendum governs** — append-only audit trail, no retro-editing.

**Next permitted document:** the spec for the milestone after the one named as current in `CLAUDE.md` — written only after the current milestone's exit gate passes (§4). Nothing else. Stated as a pointer, not a name: the only place a milestone name lives is the one place that is updated every milestone, so this line can no longer go stale. (It previously said "the M1 spec" three milestones after M1 closed — corrected under the ADR-014 override.)

## 6. CLAUDE.md PATCH (append to repo root file)

```markdown
## Governance (frozen post-M0)
- The architecture is FROZEN: Spine, kernel contract, closed D-decisions, milestone order.
- You may not redesign frozen items. If implementation reveals a genuine conflict, STOP and write
  docs/adr/cr-NNN.md (frozen items in conflict, evidence, ≤3 minimal fixes, blast radius, recommendation).
  Await director ruling. "A better way exists" is not a conflict — add one line to docs/queue.md and proceed as specified.
- Never write or modify specs for milestones beyond the current+1 rule. Never implement ahead of the ratified spec.
- Every milestone spec from M4 onward carries the four S8 §4.1 items — FOUNDATIONS AUDIT as packet one,
  dimensional declaration, corridor independence, coupling map. If you are writing a milestone spec,
  §4.1 is mandatory reading first.
- Tuning data files and TUNE parameters is always allowed.
```
