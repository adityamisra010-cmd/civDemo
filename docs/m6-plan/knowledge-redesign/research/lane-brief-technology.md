# Technology design lanes — common brief (D01–D11)

This is the working brief every technology design lane follows. It is part of the package so the Director can see how
the proposal was produced. Status: PROPOSED method; nothing here is ratified.

## 1. Your job

You own a set of EXISTING technology nodes (listed in `data/partition.json` under your lane id) and the design of any
NEW nodes in your domain. You produce `data/proposal-parts/<lane>.json` (schema: `data-schema.md` §2) and
`research/lane-notes/<lane>.md`. Design, decisively: a deeper, more granular tree where granularity adds historical or
educational value, a meaningful decision, a real dependency or a real capability — never click inflation.

## 2. Hard rules

- Write ONLY your two output files. Modify nothing else; nothing under `Sim.*`; no git operations; never touch
  `/home/user/m5w/integration` or `/home/user/civDemo`. No model identifiers in files.
- Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED. Never present a
  proposal as existing behaviour; never mark anything ratified.
- Do not write the 50–60-word educational descriptions (a later stage does). Do keep `name`, `emerged`, rationale and
  `whyDistinct` historically accurate; where a design choice rests on a historical claim you are not sure of, verify it
  (WebSearch/WebFetch via ToolSearch "select:WebSearch,WebFetch") or mark it INFERENCE. Never invent dates or inventors.
- Every existing node you own appears EXACTLY ONCE in `existing` (most will be `keep` with a one-line rationale).

## 3. Read first

`design-framework.md` (rules) · `data-schema.md` (part-file schema) · `data/partition.json` (your nodes) ·
`data/current-tree-inventory.json` (every node: key, Age, branch, cost, magnitude, factors, prereq, dependants, entity
consumers, Age-milestone links, unlocks, eurekas, universityRelevance, unlock-audit class and consequences) ·
`audit/engine-audit.md` Q6 (loader rules) and Q14 (impact table) · `audit/fog-and-ai-information-audit.md` §2
(resources and deposits) · `docs/research-gameplay-unlock-audit.md` §1 (how consequences are realized). If
`measurements/baseline-progression.md` and `audit/governance-constraints-digest.md` exist, read their summaries.

## 4. MEASURED context

- 426 technology nodes (trunk 177 · military 37 · medicine 22 · engineering 131 · natural_science 44 · agriculture 15)
  + 7 civics. Total cost 11.21M RP; A9 alone is 8.91M (79.5 %), A8 1.41M, A1–A7 together 0.89M.
- Measured pace (seed 42, the existing `sim research --auto cheapest` driver, 1630 turns = −4000 to 2100): the research
  stage opens at turn 1016 (≈ 1532 CE); about 200 nodes are complete by turn 1309 (≈ 1939 CE); the printing press is
  NOT reached by 2100. The whole campaign generates ≈ 0.70M RP. The cost scale was calibrated for scenario populations
  40–108M at A9; the canonical world reaches ≈ 0.5M. This is a demography/RP decision for the Director — do NOT
  re-price nodes to fix pacing (D-046 §1 forbids it). Do reduce single-node bottlenecks where a node bundles several
  capabilities (splitting a 100k–757k RP node into causally distinct parts roughly conserves workload but removes
  multi-century waits on one target).
- 91 nodes are knowledge-only dead ends (no dependant, no realizing grant). Give each one you own a reason: a later
  realization owner (capability.deferredEffect + milestone), a dependant, a merge, or an explicit justification.
- Deposits are per SETTLEMENT (`DepositRow`), immutable, rolled at founding; 14 goods; the only ores are copper-ore and
  tin-ore; extraction is ungated (research gates recipes and labels only). There is no coal, iron ore, oil, salt or
  uranium good. Resource identification/prospecting is designed by lane D11 (geology & nuclear); fog by the fog lane.
- Current graph conventions the validator enforces: no Age inversion; trunk closed under prerequisites; no technology
  prerequisite names a civic; all A7–A9 nodes sit in subtrees; subtrees open together at the research stage
  (`medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman`).

## 5. Loader rules your new or changed nodes must satisfy (MEASURED, `ResearchContent.cs`)

- Factors (quarter steps): novelty [0.5, 2] · difficulty [0, 3.5] · material [0, 2] · institutional [0, 3] · breadth
  [0, 1.5] · prereq_complexity [0, 1]. Cost = 92.4 × 2^(sum), within ±max(5 RP, 0.1 %) — the integrator computes it.
- Exactly ONE primary university per technology: give `primaryUniversity` ∈ {military, medicine, engineering,
  natural_science, agriculture}; a subtree node must use its own subtree; a trunk node may use any.
- No orphans: every node needs a dependant, an entity unlock or a capability string (`capability.grants` with
  `knowledge:` text counts).
- `depth` is computed (longest prerequisite path); Age ≥ every prerequisite's Age; trunk nodes require only trunk
  nodes; new A7+ nodes go in a subtree.
- New ids: snake_case, unique against every id in the inventory. Never rename an existing id (tests and rigs pin ids —
  339 lines in 59 files); change `name` instead.
- `emerged`: give an era-marked anchor (BCE/CE, ranges allowed, multiple centres named) where history supports one;
  `ResearchContentTests` pins the count of nodes without one.
- Eurekas: optional; total weight ≤ 0.40; use the existing `class` vocabulary seen in the inventory; an existing node's
  Eureka list is APPEND-ONLY (saves store the Eureka's index).

## 6. Compatibility rules (MEASURED, `audit/engine-audit.md` §7.3, Q14)

- Saves store node KEYS. A split keeps the original key on the retained part; old saves that completed the original do
  NOT get the new parts (no alias/remap exists). Merge/retire/replace lose knowledge in old saves the same way. State
  this in `compatibility.saves` and recommend the alias/remap seam (an OPEN DECISION the package raises once).
- A prerequisite change leaves partial progress dormant; a lowered cost completes progress ≥ the new cost for free.
- Moving trunk → subtree: every trunk dependant must move too; the primary university must serve the new subtree;
  partial progress is dormant until the stage. Moving between trees requires re-keying — do not propose it.
- AI-empire goldens, the allocation scenario (keys 12/17/2/8/52/74/53), the v33 session fixture (key 2) and three CI
  `--check` docs move with content changes; note in `compatibility.tests` when you touch those keys.

## 7. What to look for in your domain

1. **Conflations** (separate them only where it matters): scientific understanding vs practical application ·
   resource discovery vs exploitation · eligibility vs adoption · research vs physical production · Age entry vs
   automatic awards · player knowledge vs world truth.
2. **Bundled nodes**: one node standing for several independently useful capabilities, or for a discovery plus its
   engineering. Prefer splits that create a real choice or a real dependency.
3. **Missing knowledge** that a downstream capability genuinely needs (an instrument, a measurement standard, a
   material, an organizational technique). Add it if it carries weight; otherwise note it as rejected.
4. **Alternative routes**: independent inventions and regional materials (OR), never just to soften costs.
5. **Causality**: every prerequisite edge must be knowledge actually needed. Remove chronology-only edges.
6. **Cost consistency**: compare factor scores with peers (same Age, similar novelty/difficulty). Re-score only with a
   content reason; report old/new.
7. **Dead ends**: see §4.

For every NEW node give: what it represents (`name`, `whyDistinct`), `prereq` + `prereqRationale`, `factors` +
`costAnalog`, `nodeKind`, `primaryUniversity`, `capability` (grants, realizationOwner, status, milestone,
currentEffect, deferredEffect, inputs, missingInputs, unknownResource, aiValuation, playerCommunication — terse),
whether it needs a mechanical effect (status), the systems affected, `acceptanceTest`, and `splitFrom` if carved out.
Typical scale: most owned nodes kept; new nodes a handful per lane (more only with explicit justification).

## 8. Milestone owners (framework §10)

M6 knowledge/eligibility, universities and deferred institutions (T6.11), fog/discovery contract · M7 battle layer,
unit movement, mobile agents (units are eligibility-only before M7) · M8 politics/diplomacy/adoption · M9 society
(culture, religion, opinion, disease/crisis — public-health realization) · M10 integration · M11+ depth and content
(new industries, energy/grid systems, new resources and their consumers). No owner before M11+ ⇒ knowledge-only until
then, stated in `deferredEffect`.

## 9. Lanes

D01 agriculture & food · D02 materials, metallurgy & mining · D03 early science & mathematics (trunk) · D04 natural
science (subtree) · D05 communication & computing · D06 energy & engineering · D07 industry, construction &
infrastructure · D08 transport, navigation & naval · D09 military · D10 medicine · D11 geology, resource knowledge &
nuclear science (case study) · D12 civics. Reference only EXISTING ids from other lanes; ask for anything else through
`crossLaneRequests` (target lane, request, reason).

## 10. Lane notes (`research/lane-notes/<lane>.md`)

Summary · dispositions count · each split/merge/retire/relocate/rebalance with its reason · new nodes and why ·
candidates considered and REJECTED (with why — this is required) · conflations found · dead ends resolved · cost
findings · alternative routes · cross-lane requests · open questions. Validate your JSON parses before finishing.
