# CR-024: tranche 2 (effects, Age sweep, repeatable levels, pacing) vs frozen research rules

**Status: RULED by D-063** (Director direction, 2026-10-24). This record exists because CLAUDE.md requires a written
change request whenever an implementation crosses frozen items.

## 1. Frozen items in conflict

| Frozen item | What it says | What the Director asked (2026-10-24) |
|---|---|---|
| D-044 R13; ResearchSystem doc | Research never reads Age (law 4) | The Age sweep: a node of Age a ≥ 3 needs every Age a − 2 node complete |
| D-044 R11 | Completion is immediate and idempotent; a node completes once | Repeatable research in the final Age of both trees, with an incremental benefit on every completion |
| D-044 R14; the loader's refusal of `effects.immediate` (law 2 reading) | "Completion builds nothing … completion grants eligibility only" | "Every node in both trees must produce a real, identifiable gameplay effect" |
| Package rule C-23 (knowledge-redesign digest) | No cost is chosen to hit pacing | Per-Age research time of the two trees must agree within about 20–30 turns ("pincer") |
| D12 rules R-C2, R-C5 and validator V24 | Optional civics stay out of mandatory closures | Under the sweep, every node becomes mandatory two Ages later |

## 2. Evidence

- **Sweep.** The sweep cannot be expressed without reading the node's content Age. A fixpoint check on the proposed
  graph (2026-10-24) found that all nodes stay reachable and no prerequisite points to a later Age, so the sweep
  deadlocks nothing.
- **Effects.** Of 682 proposed nodes, 323 are KNOWLEDGE-ONLY and 248 DEFERRED (validator realization classes). Only 59
  have a live effect today.
- **Repeatables.** The ten `frontier_*` descriptors document levels but "complete once" (D-044 Part D T6 leaves levels
  as an open question).

## 3. Options

1. **Ratify the four directions as written.**
   - **Sweep.** One shared eligibility predicate reading the content Age label; it never reads the polity's Age or
     dates.
   - **Effects.** "Realized" operand effects, read by the owning systems, so ResearchSystem still builds nothing. They
     are coefficients inside named equations, capped per operand.
   - **Levels.** A `ResearchLevels` table, with diminishing gap-closing benefit per level.
   - **Pacing.** A per-(tree, Age) cost scale.
2. As option 1, but express the sweep as mass-generated prerequisite edges. **Rejected:** it pollutes causal
   prerequisites, and the Director asked for a shared rule.
3. As option 1, but with effects only as entity unlocks. **Rejected:** most entity kinds (units, institutions) have
   no live realizer before M7 and M8, so most nodes would stay inert.

## 4. Blast radius

- **Live rules.** ResearchQuery eligibility, ResearchSystem funding and completion, about 20 simulation systems (one
  coefficient read each), the AI research policy, and the UI research panel.
- **Schema.** One appended canonical table (`ResearchLevels`), with a save-version bump.
- **Content.** research.json nodes, costs and tuning.
- **Goldens.** Every golden that runs research content moves.
- **Isolation.** Each change sits behind its own research.json switch (`knowledgeEffects`, `ageSweep`,
  `destinationRouting`, plus the levels rules) with an attribution twin, so "switches off" reproduces the previous
  worlds byte for byte.

## 5. Recommendation

Option 1, as ruled in D-063.
