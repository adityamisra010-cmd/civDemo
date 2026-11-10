# D-084: The research-tree audit's issues are to be sorted out, after the current queue (Director, 2026-11-10)

**Status: RULED (Director, 2026-11-10), with READINGS flagged.** Administrative record of the Director's third message of
2026-11-10 (cited **DC**). The Director attached an audit of the research trees and asked for its issues to be sorted out.
Categories as in D-076..D-083: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim).** Working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-10c.md`.

> Sort this issue also at the end

**The attachment**, `research-tree-audit.md`, is kept byte-identical at
[`docs/m6-plan/research-tree-audit-2026-11-10.md`](m6-plan/research-tree-audit-2026-11-10.md) (570 lines, 25,170 bytes, SHA-256
`3e31c62aad659c9d7d6e82cc1b2869fb665fc490e16d90eb2213fae7805715ec`). It is a read-only source audit of this branch; it ran no build,
test or game. It reports:
- **§3:** every incoming prerequisite edge in the research graph is drawn in one colour, whether or not the prerequisite is
  complete.
- **§4:** four prerequisite expressions whose OR branch contains a conjunction are flattened, so the graph and the detail pane
  misstate them. The four are Seed drill (Tull), Astronomy records, Nuclear fusion research and Constitutional monarchy.
- **§5:** three display paths estimate Civics research turns without subtracting the banked Civics Points.
- **§6:** a Longbow discrepancy seen in play that it could not reproduce from the repository.
- **§2.2:** long tier gaps in the layout.
- **§7:** 48 date-order candidates for historical review.
- **§8:** the test gaps behind these.

**Measured by the orchestrator (2026-11-10).** The audit's named blobs are the blobs at this branch's head `9be7c18`:

| File | Blob |
|---|---|
| `Sim.Data/content/research.json` | `f05deae6` |
| `Sim.Ui/Progression/ProgressionScreen.cs` | `2f92c6b7` |
| `Sim.Ui/Progression/ResearchNodeViews.cs` | `1945d165` |
| `Sim.Ui/Progression/ResearchGraph.cs` | `11ad32d2` |
| `Sim.Ui/Progression/ResearchTreeLayout.cs` | `c77a2951` |
| `Sim.Ui.Tests/ProgressionScreenTests.cs` | `e84c931b` |

Nothing else in the audit was re-checked for this record. Its findings are secondary evidence (`CLAUDE.md`;
`docs/gov-4-repository-freshness.md`): each one is verified against the tree before it is acted on.

## 1. Decisions

| ID | Category | Decision | Director's words (DC) |
|---|---|---|---|
| DC1 | DECIDED | The issues the research-tree audit reports are to be sorted out. | "Sort this issue also" |
| DC2 | DECIDED (order) | The work comes after the current queue. It is the last work item before the pass's full report, so the report covers it. | "at the end" |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DC1-R1 (what "sort" covers).**
  - (a) APPLIED: the audit's own correction sequence (§9, items 1–6):
    - **The three source-level defects** (§3, §4, §5) are fixed in code. Each is first reproduced by a test that fails on the
      current tree, and the fix adds the tests the audit lists as missing (§8).
    - **Longbow (§6)** is diagnosed, not changed. The audit itself says not to alter Longbow's prerequisite on this evidence.
      To go further, the Director's view of the detail pane (a screenshot) and the build that was run are needed.
    - **The tier gaps (§2.2)** get a layout and edge-routing review, with no change to content depths (the audit's own limit).
    - **The date-order candidates (§7)** become a reviewed list with evidence. Research content changes only where the evidence
      establishes an error; each change is listed with its source; there is no bulk correction.
  - (b) Only the three source-level defects are fixed; everything else stays as the audit reports it.
- **DC1-R2 (which projects may change).**
  - (a) APPLIED: for this work only, `Sim.Ui` (the research screens), `Sim.Core/State` (the research estimate in `InfoQuery`) and
    `Sim.Data/content/research.json` (only as DC1-R1 (a) allows) may change, with their tests. This is an exception to the rule
    that this pass works in `prototypes/` and `docs/` only (D-079 §6; D-082 §5; D-083 §5). Nothing in `Sim.Core/Kernel` changes.
    Validation runs at the tier `docs/validation-policy.md` requires for the files changed. Any moved golden or pin gets a
    measured attribution.
  - (b) The work produces a patch for the Director's review and lands nothing in `Sim.*` until the Director authorizes it.

## 3. Not decided here

- The fixes' design: how a grouped prerequisite expression is drawn and listed, and which semantic colours the edges use. These
  follow D-070 (prerequisites shown in orange; the bank shown wherever Civics is shown), D-072 and D-073 and are delegated.
- D-079 Q5 stays OPEN for everything outside this work.
- Everything D-083 §5 lists stays as it was.
