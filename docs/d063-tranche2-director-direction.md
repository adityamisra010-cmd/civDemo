# D-063: Tranche 2 direction (Director, 2026-10-24)

**Status: RULED. Recorded from the Director's instructions of 2026-10-24.**

- **Requests.** The tree audit request, the "expand and rebalance research trees" request, and the follow-up on
  per-Age pacing.
- **Change request.** CR-024.
- **Implementation contract.** `docs/m6-plan/knowledge-redesign/tranche2-contract.md`.

| # | Ruling | Supersedes / extends |
|---|---|---|
| R1 | Every Technology and Civics node produces a real gameplay effect. Effects are "realized" operand effects (owning systems read the holder's completed knowledge and change a named coefficient, capped per operand) or live entity/order unlocks. No text-only nodes. | Extends D-044 R14 (completion still builds nothing; the owning systems realize). The loader's refusal of `effects.immediate` stands. |
| R2 | Age sweep: from Age 3 on, a node of Age x + 2 is eligible only when every node of Age x (both trees) is complete. It is one shared eligibility rule, kept distinct from authored prerequisites, and applies to player, AI and city states alike. | Supersedes D-044 R13 for this predicate only. The predicate reads the content Age label, never dates or the polity's Age. |
| R3 | Destination research: the player (and AI) may target any incomplete node. The engine plans the minimum-total-cost route (AND/OR, sweep, stage, shared nodes counted once, deterministic ties) and funds it step by step. | New. SetResearchTarget semantics extended behind `tuning.destinationRouting`. |
| R4 | Repeatable research in the final Age of both trees, completed repeatedly with a diminishing incremental benefit on each completion and a rising cost. | Extends D-044 R11 (resolves the open question in D-044 Part D T6). |
| R5 | The Civics tree grows to 90–100 % of the Technology tree's node count; Technology grows where gaps exist; no filler. | Extends D-062 (content tranche). |
| R6 | Pacing "pincer": at a given population, the time to research all of an Age in each tree agrees within about ±25 turns. Expansion adds breadth and makes nodes cheaper, not Ages longer. | Supersedes package rule C-23 for tranche 2. Costs = rubric magnitude × a recorded per-(tree, Age) scale. |
| R7 | Government forms: switching becomes available once the form is researched. Built-in policies are shown on the node. | Adoption itself stays M8 (D-D, D-062 R3). The packages are design data until then. |

**Not changed:** `main`, release tags, the milestone order, D-D (adoption in M8), CR-008 (no treasury), and the
conservation and determinism laws.
