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
