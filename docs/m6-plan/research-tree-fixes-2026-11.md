# Research-tree audit fixes: verdicts, changes and evidence (lane RT, D-084, November 2026)

**Status: lane report (lane RT, D-084).** Branch `lane/RT` (clone `/home/user/m7w/RT`), base `51ab032` on the development
branch `claude/new-session-yavghm`. Nothing is merged, tagged or released.

**Authority:**
- [D-084](../d084-research-tree-audit-fixes-2026-11-10.md): DC1 and DC2, with readings DC1-R1 (a) and DC1-R2 (a) applied.
  This lane may change `Sim.Ui` (the research screens), the research estimate in `Sim.Core/State/InfoQuery.cs`, and the
  `emerged` texts of `Sim.Data/content/research.json`, with their tests. Nothing in `Sim.Core/Kernel` changed.
- D-070 (prerequisites in orange; the bank shown wherever Civics is shown), D-061 (Civics Points), D-068, D-072, D-073,
  D-047 (the causal date rule and its test), D-069 and [ip-originality.md](../ip-originality.md).

**The audit is secondary evidence.** The audit ([research-tree-audit-2026-11-10.md](research-tree-audit-2026-11-10.md)) ran
nothing. Each of its claims was checked against the tree, by a test or a measurement, before anything was changed (§1).

**Status words:**
- **MEASURED**: run in this lane, on this branch.
- **DERIVED**: an exact argument, shown here.
- **INFERRED**: argued, not isolated.
- **PROPOSAL**: for the Director, not applied.
- **READING**: an interpretation, with the alternative stated.

**Provenance: two runs.**
- **Run 1** (10:03–11:32Z) did the following, then was interrupted before validation and reports:
  - built and tested the baseline;
  - reproduced the defects with tests that fail on the unchanged tree (`60ec80c`, tests only);
  - fixed §3, §4 and §5 (`918b5fc`, `59bac37`);
  - added the off-screen marker line and the Longbow pin (`91e6b64`);
  - wrote the date review with five text fixes (`950d6d1`).
- **Run 2** (from 13:41Z) did the following:
  - checked run 1's work against the tree;
  - added three tests (`99a0115`, `0620ab9`, `9ed6c6d`);
  - withdrew two of the five text fixes (`0a0a39e`);
  - ran the mutants and the validation tiers, and wrote the reports.
- Run 1's logs, captures and scratch harness are in the session scratchpad (`m7c/rt/`). Its numbers that this report uses
  were re-measured in run 2, except where a row says "run 1".

The lane report, with the validation tables, is
[`prototypes/m7-sprint1/reports/H-RT.md`](../../prototypes/m7-sprint1/reports/H-RT.md).

---

## 0. Summary

| Brief item | State | Where |
|---|---|---|
| (1) Check §2–§8 on the tree | **Done.** Every structural number, call site and count REPRODUCED. §5's formula is PARTLY right: when the bank covers the cost, the engine takes 1 End Turn, not 0. A fourth surface carries the §4 defect: the inspector card | §1 |
| (2a) §4: keep the prerequisite expression | **Done.** The expression tree is kept in the graph model, the view and the detail pane. Grouped alternatives are shown as an outline; no per-atom label misstates a group. There are truth-table tests for the four nodes, a corpus-wide test (995 expressions) and two synthetic cases | §3.1 |
| (2b) §3: completion-coloured edges | **Done** on the pinned and the hovered path, with the dash kept and a draw order that keeps an outstanding prerequisite visible in shared channels | §3.2 |
| (2c) §5: one shared, bank-aware estimate | **Done.** One function, `InfoQuery.ResearchTurns`, is used by the card, the pane and the inspector. It follows the engine's spending order, and the deviation from the audit's formula is stated. *(Qualified in verification round 1, RT-R1-01: that holds for an open node; the pane had also given the banked figure to civics not yet open, which the engine cannot fund directly. They now get no figure.)* | §3.3 |
| (3) Longbow | **Diagnosed; no content change.** The Director's screenshot and build are needed | §3.4 |
| (4) Tier gaps | **Reviewed; routes and depths unchanged** (measured why). The pin's off-screen marker now names each prerequisite out of view and its tier distance | §3.5 |
| (5) Historical review | **Done:** [research-date-review-2026-11.md](research-date-review-2026-11.md). Three `emerged` texts corrected, each with its source; six prerequisite questions written as proposals | §3.6 |
| (6) Validation | **Done.** Tiers 1, 2 and 3 were run. At Tier 3, under the machine lock: `Sim.Tests` 3,448 passed, 0 failed, 36 skipped; `Sim.Ui.Tests` 1,302 passed, 0 failed. The `charters` check fails identically on the base (pre-existing), so `validate.sh` blocked the suites and they were run directly with its commands. What was NOT RUN is stated there. *(These are run 2's numbers, for `a7b5faa`; verification round 1 re-ran every tier and both full suites in Debug and Release: H-RT §10.6.)* | H-RT §7 |
| (7) Reports | This file and H-RT.md | — |

---

## 1. The audit's findings, checked one by one

| Audit § | Claim | Verdict | Evidence |
|---|---|---|---|
| §1 | The named blobs are the canonical ones | **REPRODUCED** | MEASURED (`git rev-parse 51ab032:<path>`): `research.json` `f05deae6`, `ProgressionScreen.cs` `2f92c6b7`, `ResearchNodeViews.cs` `1945d165`, `ResearchGraph.cs` `11ad32d2`, `ResearchTreeLayout.cs` `c77a2951`, `ProgressionScreenTests.cs` `e84c931b`. All six equal the audit's. |
| §2 | 525 / 480 / 1,005 nodes; 1,973 edges (1,744 in-tree, 229 cross-tree; 228 Technology → Civics, 1 Civics → Technology, `forest_law` → `sustained_yield_forestry`); 1,749 must-hold and 224 alternative edges; 96 nodes with OR; 10 roots; 0 missing references, cycles, stored-depth mismatches, tier inversions and Age inversions | **REPRODUCED**, every row | MEASURED with [`measure_audit.py`](research-tree-fixes-2026-11/measure_audit.py) on the base file. One nuance: "0 depth mismatches" holds for the longest path over all edges. Over in-tree edges alone, 463 differ, because a stored depth counts cross-tree prerequisites. |
| §2.1 | No prerequisite is in the same or a later layout tier than its dependent | **REPRODUCED** | MEASURED: `ProgressionScreenTests.Layout_IsDeterministic_CoversEveryNode_NoOverlap_PrerequisitesPrecede`, which asserts that `Placed[from].Column < Placed[to].Column` for every edge, passes. |
| §2.2 | 241 / 98 / 46 / 18 in-tree edges longer than 5 / 8 / 10 / 12 tiers; the 18 listed | **REPRODUCED** | MEASURED. The audit's names are paraphrases; the ids and gaps match, edge for edge. Pinned by `TheLongestGaps_EveryInTreeEdgeOverTwelveTiers_…`. |
| §2.3 | `paper` A5 depth 1 ← `cordage`; `longbow` A6 depth 2 ← `bow_simple` | **REPRODUCED** | MEASURED. No node has `longbow` as a prerequisite. |
| §3 | Every incoming edge is drawn in one ink, whatever the completion: the pinned orange, or the prerequisite ink when hovered | **REPRODUCED** | Run 1: `IncomingEdges_TakeTheirPrerequisitesCompletion_PinnedAndHovered` failed in all 4 cases (both trees, with and without a "one of" edge) on the unchanged tree. Run 2: mutants m07 and m08 restore each path's old ink, and the test fails again (H-RT §6). |
| §4 | 77 mixed AND/OR expressions; exactly 4 change their truth table when flattened; the graph and the pane misstate them | **REPRODUCED**, with one more surface | MEASURED: `measure_audit.py` compares the exhaustive truth table of every expression with the flat reading. The 4 are `seed_drill_tull`, `astronomy_records`, `nuclear_fusion_research` and `constitutional_monarchy`, and their 10 grouped atoms were drawn and labelled "one of". The flat reading is wrong on 2 of 8, 8 of 32, 2 of 16 and 2 of 16 completion states (MEASURED, run 2). Run 1: `EveryDrawnEdgeKind_…`, `EveryPrerequisiteLabel_…` and `SeedDrillTull_…` failed on the base. **One more surface (MEASURED, run 2):** the inspector card (`InfoQuery`, `Sim.Core`) labels the same 10 atoms "one of", and 6 atoms of two entity requirements too. Out of this lane's scope (§4, N1). |
| §5.1 | Three call sites ignore the bank: the card's state line, the pane and the inspector | **REPRODUCED** | Run 1, on the stepped world (715.213 CP banked, 17.242 CP a turn): all three said 13 turns for `kin_council` (217.877 CP remaining), and the engine completes it at the first End Turn. The UI test `ACivicsEstimate_…` failed on the base, and 6 of 8 rig tests in `ResearchEstimateBankTests` failed. Run 2: mutants m10, m14, m15 and m16 put each site back and are killed. No fourth call site exists (MEASURED: a search of `Sim.Ui` and `Sim.Core/State` for turn estimates). |
| §5.2–§5.3 | Remaining = max(0, cost − progress − bank); turns = ceil(remaining / CP a turn); so a bank of 600 against a cost of 500 is 0 turns, and 450 is 1 | **PARTLY** | MEASURED by stepping the engine (rig and stepped world; §3.3). The engine accrues the turn's CP, then spends the bank on the funded civic in the same End Turn step. A covering bank therefore completes the civic at the **first** End Turn: 1, not 0. In every other case the audit's number is the engine's (450 gives 1; 449 gives 2). At a zero rate the audit's formula divides by zero; the engine completes the civic only if the bank covers it. |
| §5.4 | The display test repeats the faulty assumption | **REPRODUCED** | Read from the base: `ResearchPointDisplayTests` checked ceil((cost − progress) / CP a turn) on a world holding 715.213 CP. |
| §6 | The Longbow mismatch is not reproducible from the repository; the pane prints stage and Age-sweep text apart from the direct prerequisites | **REPRODUCED** (the diagnosis) | MEASURED (§3.4): the graph draws one edge into Longbow and the REQUIRES list has one row, Simple bow. The same section then prints the stage gate and the Age IV sweep (60 node names on the stepped world), with no heading of their own. |
| §7 | 48 date-order candidates (14 must-hold, 34 alternative), none an Age inversion | **REPRODUCED** | MEASURED, edge for edge (`measure_audit.py`). The verdicts are in the date review. |
| §7.3 | The D-047 test checks only must-hold atoms of dependents dated after 3000 BCE, from free text | **REPRODUCED** | Read from `ResearchContentTests.D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent` and `EarliestMarkedYear`. The script uses the same regular expression. |
| §8 | The edge-kind test checks only the flat classification; there is no completion-edge test and no bank-aware test | **REPRODUCED** | Read from the base. `Edges_AreExactlyThePrerequisites_…` asserted And/Or against `MustHoldAtoms` and passed with the defect present (it is green on `51ab032`). |

---

## 2. What changed

`git diff --numstat 51ab032..HEAD`, code and data:

| File | + / − | What |
|---|---|---|
| `Sim.Ui/Progression/PrerequisiteExpression.cs` (new) | 193 / 0 | `PrereqExpr`, the expression tree; `PrereqExpr.Kinds`, each atom's kind decided from the expression; `RequiresRows`, the pane's rows |
| `Sim.Ui/Progression/ResearchGraph.cs` | 24 / 15 | `EdgeKind.Grouped`; edges classified by `Kinds`; `Requirement(vertex)` keeps each node's expression |
| `Sim.Ui/Progression/ResearchNodeViews.cs` | 15 / 6 | the view's kinds from `Kinds` and its `Requirement`; `BankOn`, `TurnsOf` (the shared estimate) |
| `Sim.Ui/Progression/ProgressionScreen.cs` | 148 / 43 | edge inks by completion and draw order; the dotted grouped dash; legend keys; lock line; the pane's outline and expression in names; the bank beside the estimate; card and pane on `TurnsOf`; the marker's second line |
| `Sim.Core/State/InfoQuery.cs` | 32 / 1 | `ResearchTurns` and `ResearchBank` (the one estimate); the inspector's "Open to research now" line uses them |
| `Sim.Data/content/research.json` | 3 / 3 | three `emerged` texts (§3.6) |
| `Sim.Ui.Tests/ResearchTreeAuditTests.cs` (new) | 849 / 0 | 30 test cases (H-RT §4) |
| `Sim.Tests/State/ResearchEstimateBankTests.cs` (new) | 165 / 0 | 18 test cases (H-RT §4) |
| `Sim.Ui.Tests/ProgressionScreenTests.cs` | 22 / 8 | two moved pins (H-RT §5) |
| `Sim.Ui.Tests/ResearchPointDisplayTests.cs` | 14 / 2 | one moved pin (H-RT §5) |

Docs:
- [research-date-review-2026-11.md](research-date-review-2026-11.md) (new);
- this file;
- `research-tree-fixes-2026-11/`: the measuring script and six before/after captures;
- the four generated research reports (their SHA-256 header line only);
- the research-economy candidate file, `knowledge-redesign/research-cost-pass/candidate/research.candidate.json`. It is the
  shipped file plus overlays, pinned by `ResearchCandidateTests`, and now carries the same three `emerged` lines. Tier 1
  caught it stale (H-RT §7); D-074 did the same for its text change;
- the lane report.

**What did not change:**
- Every route and the layout (`ResearchTreeLayout.cs` is blob `c77a2951` at the base and at the head).
- Every prerequisite, depth, cost, Age, name and description in `research.json`.
- Any simulation state, world hash or system.

---

## 3. The fixes

### 3.1 Audit §4: the expression kept and grouped alternatives shown

**The model.** `PrereqExpr` folds the content's parsed `Predicate` (`Predicate.Fold`) into an n-ary tree whose atoms are
content indices:
- a chain of one operator becomes one group, so "a AND b AND c" is all-of[a, b, c];
- a change of operator is a nested group;
- an atom repeated inside one group is kept once.

These rewrites are associativity and idempotence, so the tree has exactly the expression's truth table. MEASURED (run 2): all 995
expressions and all 4,632 assignments of their atoms agree between the `Predicate`, the view's tree, the graph's tree and
the pane's rows (`EveryExpression_ItsUiModel_ItsPaneRows_AndItsGraph_KeepItsTruthTable`).

**Each atom's kind** (`PrereqExpr.Kinds`) is decided from the expression, where M is `MustHoldAtoms` (AND is the union of
its sides, OR the intersection):

| Kind | Rule | Drawn | Pane label |
|---|---|---|---|
| `And` | the atom is in M | solid | "required" |
| `Or` | not in M; M alone does not satisfy the expression; M plus the atom does | dashed | "one of" |
| `Grouped` | otherwise: a part of a grouped alternative, or a redundant atom | dotted | none |

DERIVED: for the research dialect's monotone expressions (the loader admits no NOT and no comparison in a prerequisite),
M is exactly the set of atoms true in every satisfying assignment. *(Corrected in verification round 1, RT-R1-02: the loader
refuses a NOT and a comparison that reads a variable or a quantity, but it admits a comparison of literals such as "1 > 0".
The derivation holds for expressions of atoms, AND and OR, which every shipped prerequisite is. With a comparison of literals
M can be smaller than that set, so a kind can be weaker than it might be but never false; `PrereqExpr.Of` then builds no tree
and the screens show the kinds, the flat list and the content's own expression text.)* AND is a union because the union of two satisfying sets
of a monotone expression satisfies it. Write f for the expression and O for the "one of" atoms. When no atom is Grouped:
- If f(x) is true, x contains M, and x contains some atom of O; otherwise x would hold no atom outside M, and f(M) is false.
- If x contains M and an atom of O, f(x) is true by monotonicity.
- So "every required AND at least one 'one of'" is the expression exactly.

The flat list is therefore kept wherever no atom is Grouped. MEASURED: that is 991 of the 995 expressions; the outline is
used for exactly the 4 audited nodes.

**What the player sees:**
- **The graph:** each atom's edge in its kind's stroke; a Grouped part dotted (2 on, 3 off at the zoom scale).
- **The pane:**
  - when the expression has an OR, the expression first, in names, with its groups in parentheses, e.g. "Seed drill OR
    (Ard plough AND Padded horse collar)";
  - then the REQUIRES rows: the old labelled list, or the outline: a nested group's head ("all of:" or "one of:", prefixed
    "or " when it is an alternative of its parent), its prerequisites indented under it, each with its own completion tick.
  - *(Changed in verification round 1, RT-R1-06 and RT-R1-02.)* A name with parentheses of its own is quoted in the
    expression line: Lunar calendar AND ("Cuneiform (full writing)" OR Egyptian hieroglyphic OR ...); 13 of the 96 lines
    quote a name. In an outline, "or " heads only the alternative groups after the first; the first says "either " at the
    root and nothing under a "one of:" head, so no outline begins "or all of:" (the shipped four never put a group first).
    An expression the screens do not model (one with a comparison of literals) is written as the content writes it.
- **The card's lock line:** with no required prerequisite missing, it names an alternative that completes the choice on its
  own. When only parts of groups are missing, it says "Needs a group: X with others".
- **The legend:** "known" and "in a group" keys where the control row has room. They are dropped first, so the three
  existing keys keep their room.
- Captures: [`s4-seed_drill_tull-requires.png`](research-tree-fixes-2026-11/s4-seed_drill_tull-requires.png) and
  [`s4-astronomy_records-requires.png`](research-tree-fixes-2026-11/s4-astronomy_records-requires.png); verification round 1:
  [`r1-quoted-name.png`](research-tree-fixes-2026-11/r1-quoted-name.png) (a quoted name in Civil solar calendar's line).

### 3.2 Audit §3: an edge takes its prerequisite's completion

**The inks.** An incoming edge (with its arrowhead) is drawn in:

| Prerequisite | Pinned path | Hovered path |
|---|---|---|
| complete | the completed ink, `Semantic.Completed`: the "researched" gold the Director named in D-070 ("yellow means it has been researched"), over the dark casing | the completed ink; since verification round 1 (RT-R1-04), over the same dark casing |
| outstanding | the pinned orange, `Semantic.Pinned` | the prerequisite ink; since verification round 1, over the same dark casing |

- Each edge keeps the dash of its kind.
- **Draw order:** dependents first, then completed prerequisites, then outstanding ones. Edges into one card share channels:
  the spine, the row gap and the stub into the card. So where a bundle still carries an outstanding prerequisite, the shared
  run shows the outstanding ink, and each completed prerequisite shows the completed ink on its own run out of its card.
  *(Qualified in verification round 1, RT-R1-03: the dependents' place in this order shows nothing. A dependent's route never
  shares a segment with an incoming route, MEASURED at all 1,005 cards and now pinned, so only the completed-then-outstanding
  order is a property of the screen.)*
- Each edge keeps the spine lane of its place in `Graph.Edges`, so the routes are unchanged.

**READING (D-070 R2 against D-084):** D-070 R2 draws the pinned node's incoming edges in orange. D-084 adopts the audit's
§3 required behaviour, under which a completed prerequisite's edge takes the completed semantic. R2's orange edge therefore
now applies to outstanding prerequisites. The pinned set's cards keep R2's orange outline whether complete or not, so the
"orange set" (`PinnedPrerequisites`, the strip marks and the off-screen markers) is unchanged.

The alternatives (added in verification round 1, RT-R1-05; the reading above was stated without them):
- **(a) APPLIED:** as above. The edge follows completion on both paths; the orange set follows R2.
- **(b) R2 kept literally on the pinned path.** Every pinned incoming edge stays orange, complete or not, and completion is
  shown only on the hover path and by the pane's ticks. This leaves the defect where the audit first locates it ("a pinned
  node with three direct prerequisites will show all three incoming lines in the same pinned orange colour"), so it would not
  sort out §3 as D-084 DC1 asks.
- **(c) Completion followed further.** A completed prerequisite also leaves the orange set: no orange outline on its card, no
  strip mark and no off-screen marker. R2 names the pinned node's prerequisites without regard to completion, and §3 asks
  only about the edges, so this changes R2 beyond what §3 requires.

**Contrast (MEASURED):**
- The completed ink on the casing is 3.95–4.70:1 (WCAG 2.1 SC 1.4.11 asks 3:1 for a graphical object). It is ΔE*ab 39.9–54.5
  from the pinned orange, in all nine Age themes (`TheCompletedInk_ReadsOnItsCasing_…`; D-070 used ΔE 25 as the separation
  floor).
- Uncased hover edges on the tree's field, MEASURED in run 2 with a scratch probe:

| Ink | Contrast on the field |
|---|---|
| completed | 1.33–3.15:1 |
| prerequisite | 1.60–3.87:1 |
| dependent | 1.82–4.50:1 |

  The existing prerequisite and dependent inks were already under 3:1 in Ages I–IV; the completed ink is lower still. This
  is a limitation, with a proposal (N2, §4).
  *(Superseded in verification round 1, RT-R1-04: a hovered card's incoming edges, completed and outstanding, are now drawn
  over the dark casing, as pinned edges are. MEASURED: the casing reads at 5.68–13.81:1 on the field and the completed ink at
  3.95–4.70:1 on the casing, in all nine Ages. Only the dependent edges stay uncased; N2 now concerns them alone.)*

Captures: [`s3-common_law-edges.png`](research-tree-fixes-2026-11/s3-common_law-edges.png) (Written law complete: gold; the
rest orange). Verification round 1: [`r1-hover-casing.png`](research-tree-fixes-2026-11/r1-hover-casing.png) (a hovered
card's incoming edge before and after the casing).

### 3.3 Audit §5: one bank-aware estimate that says what the engine does

**How the engine spends the bank** (MEASURED from `ResearchSystem.Step`, then by stepping the engine).

The order inside one End Turn step, per holder:
1. **Directives.** An order issued this turn applies in this step; one destination per lane, and an order replaces it.
2. **Credits**: Ideas and foreign exposure.
3. **Research Points** to the Technology lane.
4. **Civics Points accrual.** The turn's CP go into the bank, uncapped.
5. **Civics Points spend.** The node the Civics lane funds this step (`fund[1]`) draws min(balance, remaining). That node is
   the Civics destination when it is available, else the current node of its route. A Civics lane with no destination may
   fund a civic on a Technology destination's route (an assist). With overflow off, as shipped, one civic is paid per step,
   and the rest stays banked.
6. **Completion**, in the same step.

**The estimate** (`InfoQuery.ResearchTurns`, the one function):

```
remaining = max(0, effectiveCost − progress)
shortfall = remaining − max(0, banked)
shortfall ≤ 0            → 1
pointsPerTurn > 0        → max(1, ceil(shortfall / pointsPerTurn))
otherwise                → no estimate
```

- `banked` is the polity's CP balance for a civic in the two-lane model (`InfoQuery.ResearchBank`, `ResearchSnapshot.BankOn`).
  It is 0 for a technology: there is no general RP bank (D-044 R20-D), and ADR-051's reserve is absent from the shipped
  tuning.
- The card's state line and the pane call it through `ResearchSnapshot.TurnsOf`. The inspector's "Open to research now"
  line calls it directly. The pane shows "Civics Points banked" beside the estimate that counts the bank (D-070 R4).

**Where this differs from the brief and the audit, and why.** Cost 500, rate 50, bank 600 gives **1** End Turn, not 0:
the bank is spent inside the first End Turn's step. MEASURED:
- the engine stepped on the rig, `TheInspectorsEstimate_IsTheEndTurnsTheEngineTakes`, with a bank 2,000 above the cost;
- the shared function's theory `ResearchTurns_IsTheNumberOfEndTurnsTheEnginePaysIn`.

Every other case equals the audit's formula: 450 gives 1, 449 gives 2, and with no bank the old ceil(cost / rate).

**Tests for the brief's cases:**
- cost 500, rate 50, bank 600 → 1 (the engine);
- bank 450 → 1;
- progress plus bank above the cost → 1;
- a technology not shortened by a CP bank of 10^6 → unchanged;
- all three surfaces agree with the engine on the stepped world, for 4 banks and a part-paid civic;
- a zero rate → 1 with a covering bank, none without.

**What the estimate still does not count** (as before):
- Ideas or exposure credited meanwhile;
- a free lane's assist;
- overflow (off);
- a change of the rate.

The "Open to research now" number is what the engine does once this civic is ordered (the order replaces the lane's
destination). If another civic is being funded first, the bank goes to that one until the order lands.

**A civic not yet open (added in verification round 1, RT-R1-01).** The statements above hold for a node its lane can fund
at the next End Turn, that is, an open one. Until this round the pane also gave the banked estimate to every civic that is
not open, and the engine cannot spend the bank on such a civic: ordered, it becomes the lane's destination, and the lane funds
the nodes of its route first, one civic per End Turn with overflow off, spending the bank there (`ResearchSystem` steps 1b
and 3b). MEASURED on the stepped world (bank 715.213 CP): 476 civics were not open, and 15 of them read "~1 turns"; stepped
in the engine, Gift exchange takes 2 End Turns and War band 46. DERIVED from the step order: no civic that is not open can
complete at the first End Turn, because what it waits for completes in step 6 of an End Turn at the earliest and the civic is
funded only from the next.
- Now `ResearchSnapshot.TurnsOf` gives no figure for a civic of the two-lane model that is not open (locked, partial, or a
  destination still waiting on its route; `NoEstimateUntilOpen`), and the pane writes "no estimate until it opens". The bank
  row stays (D-070 R4).
- The card's state line and the inspector never gave a figure for such a civic, so all three surfaces now give one under the
  same condition.
- A technology that is not open keeps the figure it always had: its own turns at the RP rate once it opens. There is no bank,
  so that is what the engine takes for the node once its lane funds it; its route is not counted (a limitation, as before
  D-084; proposal N6, §4).

Capture: [`s5-kin_council-estimate.png`](research-tree-fixes-2026-11/s5-kin_council-estimate.png) (~13 turns, bank
ignored → ~1 turn, bank shown). *(Its AFTER panel predates verification round 1 and reads "~1 turns"; the singular and the
locked civic are in [`r1-estimates.png`](research-tree-fixes-2026-11/r1-estimates.png).)*

### 3.4 Audit §6: Longbow, diagnosed only

MEASURED, on the stepped world and with Simple bow complete (`Longbow_HasOneDirectPrerequisite_…`, and the capture
[`s6-longbow-requires.png`](research-tree-fixes-2026-11/s6-longbow-requires.png)):
- **The graph** draws one edge into Longbow, from Simple bow.
- **The REQUIRES rows** list Simple bow only ("required").
- **The same section continues with no heading of its own:**
  - "Subtree closed: the research stage is not reached (medicine hippocratic AND (geometry axiomatic OR algebra) AND stamp
    seal)." These are four node ids.
  - "Age 4 sweep: 60 nodes outstanding:", then 60 node names (Bloomery iron, Carburized steel, … Crossbow, Cavalry, Saddle,
    …). A node of Age VI waits for every node of Age IV (ADR-048).
- **The card** says "Needs university", the stage gate.

INFERRED: a player reading the REQUIRES section takes those names as Longbow's requirements. That is the likeliest source of
the discrepancy seen in play. The content has one direct prerequisite (`bow_simple`). No change was made.

**What is needed from the Director to close it:**
- a screenshot of the pane as seen;
- the build that was run (the window title's build id, or the `sim-ui-win-x64-<sha>` artifact name).

A runtime difference in `research.json` would show as a different SHA-256 (`fd4b9d34…` now; `1591427a…` at the base).

PROPOSAL N4 (§4): give the stage gate and the Age sweep their own small headings under REQUIRES.

### 3.5 Audit §2.2: the long tier gaps

MEASURED with the scratch capture harness, which paints the game's own `ProgressionScreen` headlessly on the stepped
world. Run 2 re-rendered every capture on the current head: all 58 files (SVGs, pane texts, this table) are byte-identical
to run 1's "after" set, so the committed captures show the current code.

| Prerequisite → dependent | Tree | Stored gap | Drawn gap | Span at zoom 1 (px) | Spine | Same lane |
|---|---|---|---|---|---|---|
| `irrigation_canal` → `green_revolution` | Technology | 20 | 20 | 24,452 | right | no |
| `irrigation_basin` → `drip_irrigation` | Technology | 15 | 15 | 18,364 | left | no |
| `irrigation_canal` → `drip_irrigation` | Technology | 15 | 15 | 18,496 | right | no |
| `standing_army` → `personalist_dictatorship` | Civics | 15 | 15 | 18,154 | left | yes |
| `public_games` → `professional_sport` | Civics | 15 | 15 | 19,078 | right | yes |
| `fired_brick` → `sanitary_engineering` | Technology | 14 | 14 | 17,762 | left | no |
| `steel_carburized` → `bicycle` | Technology | 14 | 14 | 16,838 | left | no |
| `brass_calamine` → `galvanized_iron` | Technology | 14 | 14 | 18,158 | right | yes |
| `glass_lead` → `fibre_optics` | Technology | 14 | 14 | 16,706 | right | yes |
| `appointed_officials` → `public_broadcasting` | Civics | 14 | 14 | 18,608 | right | yes |
| `surveying` → `railway` | Technology | 13 | 13 | 13,728 | left | no |
| `seed_drill_tull` → `mechanised_agriculture` | Technology | 13 | 13 | 14,256 | right | yes |
| `land_registry` → `land_consolidation` | Civics | 13 | 13 | 17,478 | right | yes |
| `civil_police` → `road_traffic_regulation` | Civics | 13 | 13 | 17,874 | left | yes |
| `royal_post` → `public_postal_service` | Civics | 13 | 13 | 17,478 | left | yes |
| `building_regulation` → `zoning` | Civics | 13 | 13 | 15,366 | left | yes |
| `water_boards` → `river_basin_authority` | Civics | 13 | 13 | 17,082 | right | yes |
| `turnpike_trusts` → `highway_programmes` | Civics | 13 | 13 | 16,950 | right | yes |

**What the review found:**
- **The gaps are kept.** The layout keeps each gap: drawn equals stored, because every tier in between holds cards of the
  same tree. Every one of the 18 runs down a spine (a 6-point route), never a short elbow.
- **The prerequisite is never on screen with its focused dependent.** It is 13,728–24,452 px away at zoom 1: 14 to 25 heights
  of a 1,000-px window.
- **The spines bundle edges tightly.** A spine places a focus's edges 3 px apart in 5 lanes (`ResearchTreeLayout.Route`:
  `(lane % 5) × 3`), and a pinned edge is wider than 3 px with its casing, so a bundle reads as one band. That is why the
  draw order of §3.2 matters.

**Routing options considered and not applied:**
- Shortening an edge needs a depth change (excluded by the brief and the audit).
- Breaking the tier rows would undo the "prerequisites precede" layout every test pins.
- Widening the spines takes width from every card at every window size. That is a layout change for the Director (N3).

**The improvement made (rendering only).** The pin's off-screen marker keeps its first line ("▲ 4 prerequisites above").
A second line names the prerequisites out of view, nearest first, with their tier distance:
- e.g. "Synthetic ammonia (1 tier) · Scientific plant breeding (6 tiers) · Chemical fertilizer (7 tiers) · Canal irrigation
  (20 tiers)";
- then "+N more" when the line runs out of room;
- a pin carried from the other tree (D-073) has no card here, so its prerequisites get no distance.

Pinned by `ThePinsOffscreenMarker_…`. Capture:
[`s22-green_revolution-marker.png`](research-tree-fixes-2026-11/s22-green_revolution-marker.png).

### 3.6 Audit §7: the historical review

See [research-date-review-2026-11.md](research-date-review-2026-11.md).

**Verdicts on the 48 candidates:**
- 42 are no conflict as written;
- 1 is resolved by a text fix (`ochre_processing ← grinding_stone`);
- 5 are prerequisite questions, put as proposals: `birch_tar ← fire_making`, `hafting ←` `adhesive_natural` and `cordage`,
  `bone_tools ← knapping_blade`, and `territorial_kingship ← appointed_officials`;
- the corrected adhesive date adds a sixth question, `birch_tar ← adhesive_natural`.

**`research.json` changes: three `emerged` texts,** each listed there with its sources:

| Node | Was | Now | Reason |
|---|---|---|---|
| `adhesive_natural` | ~200 kya (Umm el Tlel) | ~70 kya Umm el Tlel; ~70–60 kya Sibudu | Umm el Tlel's bitumen is ~70 ka, not 200 |
| `grinding_stone` | | adds the pigment grindstones of Kapthurin (before ~285 kya) and Blombos (~100 kya) | the node's own description includes pigment |
| `well` | ~8,000 BCE Atlit-Yam | ~8,500 BCE Cyprus wells first; Atlit-Yam ~6,900–6,300 BCE | the old date was a before-present age written as BCE |

**Two first-pass changes were withdrawn**, because the evidence does not show the old texts wrong for the nodes as defined:
- `fire_making` (its date was set by D-047 Part E);
- `knapping_blade` ("prismatic cores").

**Unchanged:** no prerequisite; the D-047 causal test passes; both tree-closure hashes, because `emerged` is in neither.

**Evidence:** every outside fact is SEARCH or KNOW. Every FETCH failed: three hosts did not resolve.

---

## 4. Findings beyond the audit, and proposals for the Director

| # | Finding (MEASURED unless marked) | Proposal (nothing applied) |
|---|---|---|
| N1 | **The inspector card flattens grouped expressions too.** `InfoQuery.NodeCard`'s ENABLED BY and WHY LOCKED rows label each atom "required" or "one of" from `MustHoldAtoms`. The same holds for the "a prerequisite of …" links of non-node cards (`B.Build`) and for entity cards (`EntityAtoms`, `EntityWhyLocked`). Scratch probe on the stepped world: 10 false "one of" labels on the 4 research nodes, and 6 on 2 entity requirements (`infra.sewerage_system`: `sanitary_engineering OR (germ_theory AND reinforced_concrete)`; `inst.statistics_vital`: `(census AND calculus) OR (official_statistics AND epidemiology)`). D-084 DC1-R2 (a) opens `InfoQuery` for the research estimate only, so this lane did not change it. | Move the kind rule into `Sim.Core`, beside `Predicate.MustHoldAtoms`, as one function both the screens and `InfoQuery` call. Label a grouped part as such in the inspector. This needs the Director's word on scope (an `InfoQuery` change outside the estimate). Display only: no state, hash or golden would move. |
| N2 | **Uncased hover edges are faint.** On the tree's field, the hover (uncased) edge inks are under 3:1 in early Ages: prerequisite 1.60:1 in Age II, completed 1.33:1 (§3.2). *(Narrowed in verification round 1, RT-R1-04: a hovered card's incoming edges are now cased; only the dependent edges stay uncased, 1.82–4.50:1 on the field.)* | Draw the hover path's dependent edges over a thin casing too, or give the dependent ink a darker variant. A theme and design choice. |
| N3 | **Spine bundles.** A spine's 5 lanes are 3 px apart, narrower than a cased pinned edge, so a pin's prerequisite edges merge into one band in the spine (§3.5). | Lane spacing tied to the stroke width, with a spine wide enough for 5 cased edges. A layout change (every card's width moves slightly), so it is the Director's to approve. |
| N4 | **Longbow's REQUIRES section mixes in other text.** The stage gate and the Age sweep's 60 names sit under REQUIRES with no heading (§3.4, INFERRED the discrepancy's source). | Subheadings "Research stage" and "Age IV sweep" under REQUIRES, or move both to a "Why locked" block. Display only. |
| N5 | **The `charters` check fails on the base.** It reports 12 "unknown id" lines in `docs/m7-plan/unit-catalogue-and-progression.md`, with identical output on the base `51ab032` and this branch. Not this lane's file. It blocks `validate.sh`'s suites at every tier (H-RT §7). | The owner of `unit-catalogue-and-progression.md` fixes the 12 references, or the checker learns the cross-reference form. |
| N6 | **A node not yet open has no route-aware estimate** (verification round 1, RT-R1-01). A civic not yet open now gets no figure; a technology not yet open keeps its own turns at the RP rate, which leave out its route. MEASURED: Gift exchange 2 End Turns, War band 46, both through their routes. | A route estimate for the pane: step the lane's funding at the current rate (`ResearchRoutePlanner.Plan` each step, one civic per End Turn, the bank carried), and give no figure when the route needs the other lane. Display only, but it repeats the engine's funding rule in the screen, so it is the Director's to approve. |
| C1–C9 | **Content questions** from the historical review: H1, H2, B1, K1, W1, W2, F-R, G and T. | See [research-date-review-2026-11.md §5](research-date-review-2026-11.md#5-proposals-for-the-director-nothing-applied). Each is a game-design or content decision. |

## 5. Questions for the Director (only those genuinely open)

1. **Longbow:** a screenshot of the pane as seen in play, and the build that was run (§3.4).
2. **The inspector labels (N1):** may the same truthful labels be applied in `InfoQuery` (`Sim.Core/State`) under D-084, or
   should this wait for a later packet?
3. **The content proposals** in the date review: H1 (adhesive and hafting), H2 (birch tar and controlled fire), B1 (blades
   and bone tools), K1 (territorial kingship). Each changes a prerequisite, a node's definition or a date D-047 set, so the call is
   the Director's.

---

## 6. Verification round 1 (2026-11-11)

The verifier (`verify-RT-r1`, on `a7b5faa`) reported six findings. The fix agent reproduced each on that commit before
changing anything; the evidence, the mutants and the validation runs are in
[H-RT §10](../../prototypes/m7-sprint1/reports/H-RT.md#10-verification-round-1-2026-11-11). Statements the round changed are
marked in place above (§0, §3.1, §3.2, §3.3, §4).

| ID | Verdict | What changed | Pinned by |
|---|---|---|---|
| RT-R1-01 (minor): the pane gave the banked estimate to civics the engine cannot fund yet | REPRODUCED (15 locked civics read "~1 turns"; Gift exchange takes 2 End Turns); fixed | A civic not yet open gets no figure and the pane says "no estimate until it opens"; open nodes, the card, the inspector and technologies unchanged (§3.3) | `ACivicNotYetOpen_…` |
| RT-R1-02 (minor): a comparison of literals, which the loader admits, crashed the screens | REPRODUCED; fixed | No tree for such an expression; the pane writes it as the content does; the kinds stay true (§3.1) | `AComparisonOfLiterals_…` (failed on the unfixed code) |
| RT-R1-03 (minor): five claims unpinned (mutants V06, V07, V08, V13, V24 survived) | REPRODUCED; four pinned, V06 equivalent | Tests for the lock line, the bank row, the three strokes and the lane rate; the dependents' draw order is withdrawn as a claim (their routes never share a segment with incoming ones, pinned) | four tests and one geometric pin |
| RT-R1-04 (minor): the hovered completed edge was under 3:1 on the field | REPRODUCED; fixed | Hovered incoming edges are cased as pinned ones are (§3.2); rendering only | `AHoveredCardsIncomingEdges_…` (failed on the uncased path) |
| RT-R1-05 (nit): a READING without its alternative | REPRODUCED; fixed | The alternatives are stated (§3.2) | — |
| RT-R1-06 (nit): names with parentheses, "~1 turns", an outline beginning "or" | REPRODUCED; fixed | Quoted names, "1 turn", "either / or" heads (§3.1) | three tests |

Captures: [`r1-estimates.png`](research-tree-fixes-2026-11/r1-estimates.png),
[`r1-hover-casing.png`](research-tree-fixes-2026-11/r1-hover-casing.png),
[`r1-quoted-name.png`](research-tree-fixes-2026-11/r1-quoted-name.png).

Questions for the Director from this round: none beyond §5. Proposal N6 (a route-aware figure for a node not yet open) is in
§4; the round's three readings are listed in H-RT §10.7.
