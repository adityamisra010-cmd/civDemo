# Research Tree Audit

**Repository:** `adityamisra010-cmd/civDemo`\
**Branch inspected:** `claude/new-session-yavghm`\
**Audit mode:** Read-only source and content audit. No repository
changes, commits, builds, or runtime tests were performed.

## Executive finding

The canonical research graph has no missing prerequisite references,
cycles, stored-depth inversions, or Age inversions. The specific
playtest problems are primarily presentation/calculation defects:

1.  The dependency graph colours every incoming prerequisite edge
    identically, even when its prerequisite is already complete.
2.  Three separate UI surfaces calculate Civics research turns without
    subtracting the banked Civics Points.
3.  Four research expressions are flattened in a way that changes their
    Boolean meaning in the graph and in the per-prerequisite
    classification.
4.  The Longbow discrepancy cannot be reproduced from the canonical
    repository content. Its sole direct prerequisite is Simple bow. The
    detail pane separately displays Age-sweep and research-stage
    blockers, which are not direct prerequisites.

There are also large but valid tier gaps, and 48 date-order candidates
requiring historical review. Those are not automatically content
defects.

## 1. Baseline and source identity

Canonical data: `Sim.Data/content/research.json`\
Schema: `civ-sim/research@2`\
Blob SHA: `f05deae681d13a39690f992e768f301deeb4ba71`

Key UI source blobs: - `Sim.Ui/Progression/ProgressionScreen.cs`:
`2f92c6b799da3e2d9c2f82c00f8c9fbcb91657e1` -
`Sim.Ui/Progression/ResearchNodeViews.cs`:
`1945d16520034f8db07ae214e2c7bc8a2705fc61` -
`Sim.Ui/Progression/ResearchGraph.cs`:
`11ad32d24610be20f0148f808a708344fdf791be` -
`Sim.Ui/Progression/ResearchTreeLayout.cs`:
`c77a295176e239b7e83ef395116ec4fd2bb50844` -
`Sim.Ui.Tests/ProgressionScreenTests.cs`:
`e84c931bc675760bbc42f0bb9c0734957cee73bb`

The same research JSON blob and key UI blobs were previously confirmed
on `main` as well as the inspected development branch. This means the
canonical source findings are not explained by a branch-specific
research-content difference.

## 2. Full graph structural audit

  Measure                              Result
  ---------------------------------- --------
  Technology nodes                        525
  Civics nodes                            480
  Total nodes                           1,005
  Direct prerequisite edges             1,973
  In-tree edges                         1,744
  Cross-tree edges                        229
  AND / must-hold edges                 1,749
  OR / alternative edges                  224
  Nodes containing OR alternatives         96
  Root nodes                               10
  Missing prerequisite references           0
  Dependency cycles                         0
  Stored-depth mismatches                   0
  In-tree tier inversions                   0
  Age inversions                            0

Cross-tree direction: - Technology → Civics: 228 edges. - Civics →
Technology: 1 edge.

The one reverse-direction edge is `forest_law` (Civics) →
`sustained_yield_forestry` (Technology). It is not a cycle. It is a
valid cross-tree dependency in the current graph.

### 2.1 Tier-order result

The layout initializes each node from its content depth, relaxes
dependent nodes downward until every in-tree prerequisite precedes it,
then compresses unused columns. External cross-tree prerequisite anchors
are placed before their earliest dependent.

I checked every edge. No prerequisite is in the same or a later tier
than its dependent. I also recomputed longest-path depth from the
prerequisite graph and found no stored-depth mismatch.

This does **not** mean the layout has no usability problem. It means the
particular claim that a prerequisite is positioned after its dependent
is not present in the canonical graph currently stored in GitHub.

### 2.2 Large tier gaps

There are 241 in-tree edges with a depth gap greater than five tiers, 98
greater than eight, 46 greater than ten, and 18 greater than twelve. The
largest gaps are:

  Prerequisite → dependent                       Depth gap
  -------------------------------------------- -----------
  Irrigation canal → Green revolution                   20
  Irrigation basin → Drip irrigation                    15
  Irrigation canal → Drip irrigation                    15
  Public games → Professional sport                     15
  Standing army → Personalist dictatorship              15
  Steel carburized → Bicycle                            14
  Glass lead → Fibre optics                             14
  Fired brick → Sanitary engineering                    14
  Brass calamine → Galvanized iron                      14
  Appointed officials → Public broadcasting             14
  Surveying → Railway                                   13
  Seed drill (Tull) → Mechanised agriculture            13
  Royal post → Public postal service                    13
  Building regulation → Zoning                          13
  Turnpike trusts → Highway programmes                  13
  Water boards → River basin authority                  13
  Land registry → Land consolidation                    13
  Civil police → Road traffic regulation                13

These are not ordering inversions. They are long visual dependency spans
caused by other prerequisite chains setting the dependent's depth. They
merit a visual scan/edge-routing review, especially where a prerequisite
is in another lane or branch.

### 2.3 Paper and Longbow content

Canonical `paper`: - Age A5 - Depth 1 - Expression: `cordage` - Only
direct prerequisite: Cordage, depth 0

Canonical `longbow`: - Age A6 - Depth 2 - Expression: `bow_simple` -
Only direct prerequisite: Simple bow, depth 1

Both are correctly ordered in the current graph. The Longbow direct
prerequisite is also consistent with the current corpus/inventory
documentation that says `bow_simple` gates `longbow`.

## 3. Confirmed defect: completion state is not reflected in dependency-edge colour

### Evidence

`ResearchNodeViews.cs` constructs each `PrereqView` with a `Completed`
boolean based on the completed mask.

`ProgressionScreen.cs` then uses that boolean in the detail pane: -
Completed prerequisite tick: `sm.Completed` - Incomplete prerequisite
tick: `sm.Danger`

However, the graph renderer does not consult prerequisite completion
state when drawing edges: - The pinned incoming-edge path calls
`PinnedEdge(...)`, which uses `Theme.Semantic.Pinned` for every incoming
edge. - The non-pinned incoming-edge path uses `t.Semantic.Prerequisite`
for every incoming edge. - The `GraphEdge` contains only endpoints and
AND/OR kind, not completion state.

Therefore a pinned node with three direct prerequisites will show all
three incoming lines in the same pinned orange colour even if one
prerequisite is complete and two are incomplete. The detail pane has the
completion information, but the dependency lines do not use it.

### Scope

This is a systemic graph-rendering defect, not a defect limited to one
node. It applies to incoming dependency edges throughout both trees
whenever the node's prerequisites are highlighted.

### Required behaviour

For each direct prerequisite edge: - Completed prerequisite:
completed/green semantic. - Incomplete prerequisite: prerequisite/orange
semantic. - Preserve solid versus dashed styling for required versus
alternative edges, while also preserving nested expression groups (see
§4).

A completed prerequisite should not be shown as outstanding merely
because it remains part of the dependency graph.

## 4. Confirmed defect: four prerequisite expressions are semantically flattened

The content supports Boolean prerequisite expressions. The graph and
detail view reduce them to a per-atom `AND` or `OR` classification using
`MustHoldAtoms()`.

This reduction is valid for simple expressions that can be represented
as:
`all common requirements AND at least one of the remaining alternatives`.

It is not valid for four expressions containing an OR branch with a
conjunction. I exhaustively evaluated the truth table for every atom
combination in the four affected expressions and compared the canonical
expression with the flattened representation.

### 4.1 Seed drill (Tull)

Canonical: `seed_drill OR (plough_ard AND horse_collar)`

Incorrect flattened representation: - Seed drill: one-of - Plough ard:
one-of - Horse collar: one-of

This makes the UI imply that any one of the three is sufficient. In
reality: - Simple seed drill alone is sufficient; OR - Both plough ard
and horse collar must be complete.

### 4.2 Astronomy records

Canonical:
`arithmetic_babylonian OR ((hieroglyphic OR chinese_script OR mesoamerican_script) AND calendar_lunar)`

Incorrect flattened representation: - Arithmetic Babylonian: one-of -
Hieroglyphic: one-of - Chinese script: one-of - Mesoamerican script:
one-of - Lunar calendar: one-of

The script-based path requires both a qualifying script and the lunar
calendar. The Babylonian arithmetic path is an independent alternative.

### 4.3 Nuclear fusion research

Canonical:
`nuclear_physics AND (superconductivity_applied OR (thermonuclear AND laser))`

Incorrect flattened representation: - Nuclear physics: required -
Applied superconductivity: one-of - Thermonuclear: one-of - Laser:
one-of

The second route requires Thermonuclear AND Laser together. They are not
independent alternatives.

### 4.4 Constitutional monarchy

Canonical:
`succession_law AND (written_constitution OR (bill_of_rights AND representative_assembly))`

Incorrect flattened representation: - Succession law: required - Written
constitution: one-of - Bill of rights: one-of - Representative assembly:
one-of

The second route requires Bill of rights AND Representative assembly
together.

### 4.5 Extent

-   77 nodes contain both AND and OR operators.
-   4 of those have nested structures for which the current flat edge
    classification changes the Boolean truth conditions.
-   The other 73 mixed expressions reduce to the current simple
    common-requirement-plus-alternatives representation without changing
    their truth table.

The UI currently prints the raw expression text in the detail pane when
OR is present, which helps, but the edge styling and the per-item
`required` / `one of` labels still misrepresent the four cases.

### Required behaviour

The graph must preserve the expression tree, not just the atom set and a
binary edge kind. The detail pane should display grouped alternatives.
For example:

`Seed drill OR (Plough ard AND Horse collar)`

The UI should make the grouped AND relationship visible, and completion
state should be applied to each atom without destroying the group
structure.

## 5. Confirmed defect: Civics turn estimates ignore the banked CP balance

The Civics lane is funded by Civics Points (CP). The engine explicitly
spends banked CP against the remaining EffectiveCost of the civic being
funded.

The UI estimate code does not include that bank.

### 5.1 Confirmed affected call sites

**A. Research tree card state line**

`Sim.Ui/Progression/ProgressionScreen.cs`, `StateLine(...)`: - Uses
`EffectiveCost - Progress`. - Divides by `PointsPerTurnOn(tree)`. - Does
not subtract `CivicsBalance` for Civics nodes.

**B. Selected-node detail pane**

`Sim.Ui/Progression/ProgressionScreen.cs`, selected-node statistics: -
Uses `EffectiveCost - Progress`. - Divides by the lane's rate. - Does
not subtract the banked CP balance.

**C. Research inspector / InfoQuery card**

`Sim.Core/State/InfoQuery.cs`: - The "Open to research now ... about N
turns" branch uses `cost - Progress`. - Divides by the lane's current
rate. - Does not subtract the CP bank.

These are three distinct display paths for the same underlying estimate
defect.

### 5.2 Example

For a civic with: - Effective cost = 500 CP - Existing progress = 0 CP -
CP generation = 50 CP/turn - Bank = 600 CP

Current display calculation: `ceil(500 / 50) = 10 turns`

Correct calculation: `ceil(max(0, 500 - 0 - 600) / 50) = 0 turns`

If the bank is 450 CP: `ceil((500 - 450) / 50) = 1 turn`

### 5.3 Correct formula

For a Civics node:
`Remaining = max(0, EffectiveCost - ExistingProgress - BankedCivicsPoints)`

`Turns = ceil(Remaining / CivicsPointsPerTurn)`

For Technology nodes, do not subtract a bank because the current RP
model has no general RP bank.

The estimate must use the correct lane's point pool. It must not
subtract Civics Points from Technology costs.

### 5.4 Test gap

`ResearchPointDisplayTests` checks the estimate against
`(EffectiveCost - Progress) / CP per turn`, but does not set up a
meaningful non-zero bank that should reduce the estimate. This test
therefore repeats the faulty assumption instead of catching it.

The engine's CP-spending implementation already demonstrates the
intended bank semantics. The defect is in the estimate displays, not in
the CP bank itself.

## 6. Longbow detail-pane discrepancy

The canonical JSON contains exactly one direct Longbow prerequisite:
`bow_simple`.

The current UI pipeline constructs: - `ResearchGraph.Edges` from
`ResearchNode.PrerequisiteNodes`. - `ResearchNodeView.Prerequisites`
from the same `ResearchNode.PrerequisiteNodes`. - The detail pane's
`REQUIRES` list from `ResearchNodeView.Prerequisites`.

Consequently, the current source has no path by which the graph can show
only Simple bow while the direct `REQUIRES` list independently invents
another direct prerequisite.

The detail pane also prints separate lock gates: - Research stage /
subtree gate. - Age sweep blockers. - Recursive subtree requirements
where applicable.

Those are not direct prerequisite edges and are displayed separately
from the direct prerequisite list. Longbow is Age A6, so its Age-sweep
gate is a plausible source of additional text in the detail pane.

### Conclusion

The reported mismatch is not reproducible from the canonical repository
source. The next diagnostic must compare the exact local runtime build
and loaded `research.json` with the canonical blob SHA above, and
distinguish the direct `REQUIRES` list from the separate Age-sweep/stage
sections.

Do not alter Longbow's canonical prerequisite on the basis of this
discrepancy alone.

## 7. Historical emergence-date audit

I compared explicit dates in each prerequisite's `emerged` text with
explicit dates in the dependent's `emerged` text. The heuristic takes
the earliest marked date in each description and flags an edge if the
prerequisite's earliest marked date is later than the dependent's
earliest marked date.

This produces 48 candidate date-order conflicts: - 14 involve an atom
classified as must-hold by the current Boolean reduction. - 34 involve
an OR alternative. - None is an Age inversion. - None establishes a
confirmed historical error by itself.

The heuristic is intentionally conservative about conclusions but broad
in candidate generation. Descriptions sometimes mix invention, first
surviving artefact, inferred earlier use, contested evidence, later
standardisation, and independent regional invention. Some candidates are
clearly caused by that mixture.

### 7.1 All 48 date-order candidates

`[must]` means the prerequisite atom is required on every Boolean path
under the current expression. `[alternative]` means it is one possible
route.

  Dependent ← prerequisite                       Classification
  ---------------------------------------------- ----------------
  `birch_tar ← fire_making`                      must
  `hafting ← cordage`                            must
  `hafting ← adhesive_natural`                   must
  `bow_simple ← cordage`                         must
  `raft ← cordage`                               must
  `ochre_processing ← grinding_stone`            must
  `bone_tools ← knapping_blade`                  must
  `orchard ← root_crop`                          alternative
  `well ← timber_frame`                          alternative
  `fermentation_grain ← cereal_domesticated`     alternative
  `fermentation_grain ← rice_wet`                alternative
  `fermentation_grain ← millet`                  alternative
  `fermentation_grain ← maize`                   alternative
  `fermentation_grain ← sorghum_pearl_millet`    alternative
  `flax ← cereal_cultivation`                    must
  `spinning_spindle ← wool`                      alternative
  `spinning_spindle ← cotton_cultivation`        alternative
  `loom_warp_weighted ← spinning_spindle`        must
  `surveying ← chinese_script`                   alternative
  `surveying ← mesoamerican_script`              alternative
  `standard_weights ← chinese_script`            alternative
  `calendar_civil ← chinese_script`              alternative
  `calendar_civil ← mesoamerican_script`         alternative
  `medicine_recorded ← chinese_script`           alternative
  `astronomy_records ← mesoamerican_script`      alternative
  `latitude_sailing ← astronomy_records`         alternative
  `trade ← camel`                                alternative
  `taxation ← hieroglyphic`                      alternative
  `taxation ← chinese_script`                    alternative
  `appointed_officials ← chinese_script`         alternative
  `royal_envoys ← chinese_script`                alternative
  `territorial_kingship ← succession_law`        alternative
  `territorial_kingship ← appointed_officials`   must
  `craft_guilds ← municipal_charter`             alternative
  `gift_exchange ← kin_council`                  must
  `war_band ← kin_council`                       must
  `scribal_schools ← chinese_script`             alternative
  `scribal_schools ← mesoamerican_script`        alternative
  `scriptural_canon ← alphabet_vowels`           alternative
  `scriptural_canon ← brahmi`                    alternative
  `public_theatre ← brahmi`                      alternative
  `state_exploration ← caravel`                  alternative
  `state_propaganda ← radio_broadcasting`        alternative
  `hunt_season_leaders ← kin_council`            must
  `seasonal_aggregation ← kin_council`           must
  `island_voyaging ← dugout`                     alternative
  `village_defences ← timber_frame`              alternative
  `tyranny ← citizen_militia`                    alternative

### 7.2 Priority historical reviews

The following deserve review first because the candidate is not merely a
later regional alternative:

-   `bow_simple ← cordage`: cordage's first direct evidence is listed at
    \~50 kya; the bow's Sibudu date is \~64 kya (inferred). The corpus
    itself distinguishes inferred and preserved evidence. This is a
    likely date/evidence inconsistency, but "older inferred" cordage
    could resolve it.
-   `ochre_processing ← grinding_stone`: grinding stone's listed example
    (\~30 kya) is later than Blombos ochre processing (\~100 kya). This
    likely reflects a definition mismatch between grinding stones
    generally and the specific Bilancino example.
-   `bone_tools ← knapping_blade`: knapping blade's listed date is \~40
    kya (with earlier Africa \~70 kya), while Katanda harpoons are \~90
    kya. The dependent's early example may predate the prerequisite as
    currently named.
-   `loom_warp_weighted ← spinning_spindle`: listed textile evidence
    (\~7,000 BCE) predates spindle whorls (\~6,000 BCE), suggesting a
    potential definition or evidence issue.
-   `territorial_kingship ← appointed_officials`: the dependent's
    earliest listed state formations predate the listed examples for
    appointed officials. This may be a conceptual prerequisite issue,
    not merely a date-text problem.
-   `war_band ← kin_council`: Jebel Sahaba (\~13 kya) predates the
    current description's approximate \~10 kya date for kin councils.
-   `hunt_season_leaders ← kin_council`: the cited communal hunt
    evidence (\~20 kya) predates the kin-council estimate.
-   `seasonal_aggregation ← kin_council`: the cited Magdalenian
    aggregation (\~17 kya) predates the kin-council estimate.

Other candidates are often explained by a date string combining several
regional examples or by an OR alternative that represents a later
independent invention. They still need review, but should not be
bulk-corrected.

### 7.3 Existing historical validation gap

The current `D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent`
test: - Checks only must-held atoms. - Skips dependents dated at or
before 3000 BCE. - Uses the earliest marked date from free-text
descriptions. - Does not validate OR alternatives. - Does not resolve
ambiguous definitions or the difference between first evidence and
invention.

The test is useful for a bounded post-3000 BCE rule, but it is not a
complete historical audit of the whole corpus.

## 8. Test coverage gaps and false confidence

### Existing tests validate the flattened representation, not the full Boolean meaning

`ProgressionScreenTests.Edges_AreExactlyThePrerequisites_ClassifiedAndOrFromTheExpression`
checks that each graph edge's `And`/`Or` label agrees with
`MustHoldAtoms()`. It does not check that the rendered set of labels
preserves the original expression's truth table.

As a result, the test passes even when the graph is semantically wrong
for the four nested expressions.

### Missing completion-state edge test

The graph tests check highlighted prerequisite lines and edge direction.
They do not assert that a completed prerequisite's incoming edge changes
to the completed semantic colour while an incomplete prerequisite
remains orange.

### Missing bank-aware estimate tests

The research UI tests verify rates and progress, but do not test: - Cost
500, rate 50, bank 600 → 0 turns. - Cost 500, rate 50, bank 450 → 1
turn. - Existing progress plus bank jointly exceeding the cost. -
Technology estimates not subtracting the Civics bank. - All three UI
surfaces agreeing on the same bank-aware estimate.

## 9. Recommended correction sequence

1.  **Expression model/UI:** preserve nested prerequisite groups in
    graph edges and detail-pane rendering. Add truth-table tests for the
    four affected nodes.
2.  **Per-edge completion colouring:** feed completion state into edge
    rendering and test mixed completed/incomplete prerequisites on both
    trees.
3.  **Bank-aware estimates:** centralize the estimate function and use
    it in the tree card, selected-node pane, and InfoQuery. Add
    non-zero-bank tests.
4.  **Longbow runtime discrepancy:** compare the local executable's
    loaded research content and build provenance against the canonical
    JSON SHA. Identify whether the extra text is an Age sweep/stage
    blocker or stale content.
5.  **Tier-gap UX review:** inspect the 18 edges with gaps above 12
    tiers and the remaining 28 edges above 10 tiers. Do not change
    content depths simply to shorten lines.
6.  **Historical content review:** review the eight priority must-hold
    candidates first, then the remaining 40 date candidates with
    evidence and explicit definitions.

## 10. Audit limitations

This was an independent, read-only inspection of GitHub-pushed source
and canonical content. I did not: - Access the user's local working tree
or local uncommitted files. - Launch the game or reproduce the Longbow
detail pane. - Run .NET builds or tests. - Verify historical claims
against external primary/secondary sources.

Accordingly, source-level defects above are confirmed from code paths
and content. Runtime discrepancies and historical candidates remain
explicitly unresolved where source inspection alone cannot establish the
answer.

## Source references

-   [Canonical research
    content](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Data/content/research.json)
-   [Progression
    screen](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui/Progression/ProgressionScreen.cs)
-   [Research node
    views](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui/Progression/ResearchNodeViews.cs)
-   [Research
    graph](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui/Progression/ResearchGraph.cs)
-   [Research tree
    layout](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui/Progression/ResearchTreeLayout.cs)
-   [Research content
    tests](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Tests/Systems/ResearchContentTests.cs)
-   [Progression screen
    tests](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui.Tests/ProgressionScreenTests.cs)
-   [Research point display
    tests](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Ui.Tests/ResearchPointDisplayTests.cs)
-   [Research point lifecycle
    tests](https://github.com/adityamisra010-cmd/civDemo/blob/claude/new-session-yavghm/Sim.Tests/Systems/ResearchPointLifecycleTests.cs)
