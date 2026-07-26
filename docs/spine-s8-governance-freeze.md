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
see. Both were catchable on paper. Neither was in anybody's packet.

**Aim.** Fewer AVOIDABLE surprises. Reality correcting the spec — CR-001, CR-002, the ghost harvest
— is the system working, not a failure to plan; this format does not ask a spec to be omniscient
and adds no new process gate. It asks four questions that can be answered before code exists.

**Effective from the M4 spec onward.** M3 is mid-flight and is NOT retro-fitted: its remaining
packets — including T3.10, which specs new corridors — run under the pre-amendment format.

Every milestone spec from M4 onward carries all four. Item 1 is a task packet (the first one in
§4); items 2–4 are spec content. The existing spec skeleton (§1 decisions closed · §2 scope fence ·
§3 system notes · §4 task packets · §5 exit criteria · §6 governance) is otherwise unchanged, and
each requirement below names where it lives.

**1. FOUNDATIONS AUDIT — packet one.** `T(n).1` is an explicit pass over the EXISTING constants the
milestone's new systems will depend on.

  *Scope is DEPENDENCY, not perturbation:* every existing constant a new system depends on —
  directly, or through a value it consumes. The coupling map (4 below) is the starting list, not
  the limit. The distinction is load-bearing rather than pedantic: `CatchmentSystem.TravelBudget`
  sat in an existing system's equation and reached M3's new systems only through
  `EffectiveArableKm2`. A perturbation-scoped audit would have exempted the ruling's own origin
  case; a dependency-scoped one catches it.

  For each constant in scope, three answers:

  - **(a) What it means in REAL units** — persons, km², person-years, grain-equivalents/yr. An
    internal unit is not an answer: "15 cost units" only restates the number. The question
    "…which is how many km?" is the one that does the work — it yields 240 km of ideal-ground
    reach (~205 km once terrain is paid for), at which point nobody defends the value.
  - **(b) Whether its denomination matches its consumption site** — the identical question at both
    ends of the pipe: what the producer writes, what the consumer multiplies. CR-002 was two
    consumers reading one field in two denominations and only one of them converting.
  - **(c) Whether it was DERIVED or merely CHOSEN** — and if chosen, from what, by whom, when.
    `yieldPerFarmlandPerYear` traces to a bare `40.0` at T1.5 and a mechanical retune to `28.0` at
    T1.6 taken only to keep the no-order production rate unchanged. Three milestones stood on it.

Cheap when clean: the deliverable is a table, and a clean audit closes the packet in a session.

The audit RECORDS the answers; it does not acquire new powers from doing so. A finding that is a
genuine conflict opens a CR under §3. Everything else follows the existing rules unchanged — TUNE
values and data files are living and may be corrected freely (§1), and "a better way exists" goes
to the queue (§2). The audit asks whether a constant is traceable and dimensionally sound, never
whether it is optimal. This is the packet that would have caught CR-002 as planned work.

*On packet ordering:* several specs put a golden-moving change first so goldens move once (M3's
T3.1). That convention survives — an audit is cheap and produces findings, and any correction it
justifies then lands in the same early window, so audit-then-correct-then-build still moves goldens
once.

**2. DIMENSIONAL DECLARATION — §3 system notes.** Every new quantity declares its units at
introduction, and every core equation is checked for unit balance on paper before implementation.
Both sides of a `min()`, both terms of a sum, and the numerator and denominator of every ratio.

  Note what actually went wrong at T1.5, because it is the more common failure and the less
  obvious one: the yield constant's NUMERATOR was declared ("1 food = 1 person-year, D-015"). It
  was `EffectiveFarmland`, introduced a packet earlier at T1.4, that never declared a unit at all —
  so `harvest = farmland × yield` was not a check that failed, it was a check nobody could run.
  Requiring the declaration is what makes the balance check performable; the imbalance is then
  visible in one line. Units belong in identifiers too, but that is a code convention (ADR-013),
  not this document's business.

**3. CORRIDOR INDEPENDENCE — wherever the corridor is introduced (§3 or §5).** No corridor is
specced without naming (i) what it is independent OF, and (ii) how a change in the measured system
could make it fail. A corridor whose denominator moves with the measured quantity is
SELF-REFERENTIAL and is REFUSED at spec time, not discovered later.

  The refused shape has a signature worth memorising, because it is not obvious in prose and is
  algebraically fatal: deriving a bound as `P_hist / (H × s̄)` where `s̄` is the sim's own measured
  quantity cancels against `measured = P_sim / (A × s̄)`, so the bound sits at a fixed fraction of
  whatever the map reports, forever. That is the CR-002 cancellation identity — the standing
  example. Two tests for it, both from the same CR-002 refutation and neither an extra obligation:
  **algebraic** — write the bound and the measurement as fractions and cancel; if a sim-measured
  term disappears from the ratio, the corridor is a mirror. **Counterfactual** — run the same
  derivation against the world as it stood BEFORE the change; a recipe that yields a different
  bound depending on which side of the change you run it on is not a derivation.

**4. COUPLING MAP — a short table, §3.** Which existing constants, corridors and emergence tests
each new system perturbs. It has two jobs and earns its place on both: it scopes the foundations
audit up front, and it is the re-anchoring checklist when something moves. T3.2b re-verified nine
downstream failures by discovery; a coupling map turns that into a list written before the work.

**Not added:** further process gates, or pre-specification of what only measurement can settle.

## 5. CURRENT STATE DECLARATION

**Frozen baseline (activates on M0 acceptance):** `civ-sim-architecture-v3-outline.md` (as amended below) · `m0-kernel-spec.md` · `d011-battle-layer-addendum.md` · `d009-d010-map-population-addendum.md` · `d018-classes-and-needs.md` · this document. Where addenda amend the v3 outline (region-graph clause, milestone renumbering, walking-skeleton content), **the addendum governs** — append-only audit trail, no retro-editing.

**Next permitted document:** the M1 spec, written upon M0 exit. Nothing else.

## 6. CLAUDE.md PATCH (append to repo root file)

```markdown
## Governance (frozen post-M0)
- The architecture is FROZEN: Spine, kernel contract, closed D-decisions, milestone order.
- You may not redesign frozen items. If implementation reveals a genuine conflict, STOP and write
  docs/adr/cr-NNN.md (frozen items in conflict, evidence, ≤3 minimal fixes, blast radius, recommendation).
  Await director ruling. "A better way exists" is not a conflict — add one line to docs/queue.md and proceed as specified.
- Never write or modify specs for milestones beyond the current+1 rule. Never implement ahead of the ratified spec.
- Tuning data files and TUNE parameters is always allowed.
```
