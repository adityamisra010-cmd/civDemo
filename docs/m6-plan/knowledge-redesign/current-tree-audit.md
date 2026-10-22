# Current tree and engine audit — the MEASURED baseline for the knowledge redesign

**Status: audit of the tree as built (MEASURED), with the redesign's proposed dispositions joined in from the canonical
proposal files.** Nothing here changes code, content or rulings. Baseline: branch `claude/new-session-yavghm` at
`edba2b57b8e821cc128218216e4cbad3693839b5` (`main` = tag `m5-exit` = `6e51a3e26c6b10e179335db3042898f1251099de`; the
branch adds docs-only commits; `git diff 6e51a3e HEAD -- . ':!docs'` is empty). Latest CI on the branch head: CI run
39652196122 and ui-artifact 39652196108, both green.

Labels: **MEASURED** · **INFERENCE** · **PROPOSED** · **OPEN DECISION** · **IMPLEMENTATION DEFERRED**.

| Part | File |
|---|---|
| Complete per-node inventory with proposed disposition, changes and compatibility risk | `audit/current-tree-inventory-{trunk, military, medicine, engineering, natural_science, agriculture, civics}.md` (generated from `data/current-tree-inventory.json` + the proposal JSON) |
| Engine audit (14 questions, every claim `path:line`) | `audit/engine-audit.md` |
| Fog, map, deposits and AI-information audit (53-row leak matrix) | `audit/fog-and-ai-information-audit.md` |
| Governance constraints digest (rules, prohibitions, cost rubric, contradictions X-01…X-23) | `audit/governance-constraints-digest.md` |
| Measured progression on seeds 1, 2, 42, 7 | `measurements/baseline-progression.md` |
| Machine-readable inventory | `data/current-tree-inventory.json` (`tools/extract_current.py`) |

## 1. Headline numbers (MEASURED, `data/current-tree-inventory.json`)

| Measure | Value |
|---|---|
| Nodes | **433** = 426 Technology (trunk 177 · Military 37 · Medicine 22 · Engineering 131 · Natural Science 44 · Agriculture 15) + 7 Civics (keys 1001–1007) |
| Sets | finite 417 · recursive (repeatable frontier) 10 · speculative finite 6 |
| Nodes by Age | A1 28 · A2 51 · A3 38 · A4 32 · A5 32 · A6 42 · A7 32 · A8 53 · A9 125 |
| Total cost | 11,210,860 RP (finite 11,059,750). By Age: A1 11,080 · A2 60,360 · A3 81,950 · A4 95,050 · A5 157,140 · A6 194,040 · A7 289,770 · A8 1,407,020 · **A9 8,914,450 (79.5 %)** |
| Cost range | 180 – 756,960 RP (magnitude 1.0 – 13.0; the loader's factor ranges cap a node at ≈757k RP) |
| Unlock-audit classes | B gameplay 51 · C knowledge-only 262 · D institution/stage 6 · E infrastructure 4 · F unit 30 · H repeatable 10 · J deferred entity 70 |
| Knowledge-only dead ends | **91** nodes with no dependant and no realizing grant (`tools/validate.py` V09 on the current graph) |
| Eurekas | 81 on 80 nodes; 17 evaluable today (2 stock, 15 knowledge); 64 wait for future systems |
| Existing descriptions | mean **11.5 words** (min 3, max 191) — every node needs a new educational text |
| Entities | 135 (building 43 · institution 28 · infrastructure 25 · unit 23 · activity 9 · recipe 4 · project 3) |
| Structural conventions | no Age inversion; trunk closed under prerequisites; no technology prerequisite names a civic; every A7–A9 node is in a subtree; trunk nodes span A1–A6, subtree nodes A5–A9 |

## 2. How research works today (MEASURED; condensed from `audit/engine-audit.md` Q1–Q12)

- **Research Points.** RP per turn = 0.08 × population^0.699, per strategic turn, never × dt (ADR-030); input is the
  total population of the polity's settlements, read from PREV; one pool per polity serves both trees and goes only to
  the one target (`ResearchQuery.cs:357-394`; `ResearchSystem.cs:176-187`). Literacy, education and institutions do not
  feed RP; universities only cut cost.
- **Targets and progress.** One `ResearchTargetRow` per polity across both trees (order kind 6, a persistent directive
  delivered the same step); per-node progress rows kept indefinitely; overflow is LOST; at most one RP-funded completion
  per step (a chain of depth d needs ≥ d+1 steps); dependants become available the next step
  (`ResearchSystem.cs:34-45, 113-131, 189-234`).
- **Cost.** BaseCost = 92.4 × 2^magnitude (six factors; loader ranges; tolerance max(5 RP, 0.1 %)); EffectiveCost =
  max(0.2 × Base, Base × Π university factors) for subtree technologies only (factor ≥ 2/3 from
  `maxResearchCostReduction` 1/3); trunk and Civics always pay BaseCost (`ResearchQuery.cs:268-335`).
- **Eurekas.** Evaluated every step on PREV for every available node; one credit pool per node (Eureka + foreign
  exposure) ≤ 0.4 × BaseCost; no overflow; `ResearchExposureRow` has no writer (`ResearchSystem.cs:133-174`).
- **Prerequisites and the stage.** AND/OR (no NOT); `depth` = longest prerequisite path; no orphans; trunk closed;
  the stage `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman` opens all five
  subtrees together (`ResearchQuery.cs:80-124`).
- **Persistence.** Rows store node KEYS (never ids or positions) except `ResearchEurekaRow.Eureka`, an index into the
  node's Eureka list; schema v34, readable {32, 33, 34}; research content is not hashed and has no content-aware load
  hook; rows for a key absent from content load and lie dormant forever (`audit/engine-audit.md` §7).
- **Ages.** An Age needs all CORE facts and enough supporting facts; `research` facts = any listed node complete;
  `research_count` technology ≥ 200 (A8) and ≥ 280 (A9); surges have no numeric effect; unit modernization is Age-driven
  and free (`AgeQuery.cs:118-193`; `AgeTransitionSystem.cs:91-101`).
- **AI.** `AiResearchPolicy` picks among goals (next Age's CORE closure · next road class · the tax civic · university
  entities) by lowest remaining closure cost, then the cheapest available node; it reads raw world state, not
  `PolityKnowledge` (`AiResearchPolicy.cs:48-250`).
- **UI.** Two tabs (Technology, Civics); lanes = trunk + five subtrees, civics lanes by `domain`; the "Adopted civics"
  lens equates research with adoption (`Sim.Ui/Progression/Lenses.cs:62-68`).

## 3. Dependency map — research node → realized effect (MEASURED)

`research node → prerequisites → knowledge state → query → available action / entity → realized effect`

| Consumer | Example chain (ids) | Knowledge query | Action / entity | Realizing system → effect | Status |
|---|---|---|---|---|---|
| Recipe | `tin_bronze` → `recipe.bronze_casting` | `CraftingQuery.IsRecipeAvailable` over settlement knowledge | Crafting labour share | ProductionSystem → bronze output from copper-ore + tin-ore stocks | live |
| Labour activity | `cereal_cultivation` (via `activity.farming`) | `LabourActivities` eligibility | SectorAllocation (kind 3) | ProductionSystem → cultivated (not wild) farming YIELD (`ProductionSystem.cs:276-288`) | live (doc says "label only" — doc≠code) |
| Trade | `trade` → `activity.trade` | `TradeQuery.SettlementKnowsTrade` (both endpoints) | automatic arbitrage on connected routes | TradeArbitrageSystem → goods flows | live |
| Tax | `taxation` (civic) + Age ≥ III | `Governance.CanLevyTax` (knowledge → Age → seat) | SetTaxRate (kind 5) | GovernanceSystem → tax policy on flows | live |
| Road class | `road_paved` → `infra.road_paved` | `RoadDevelopmentQuery.BestKnownClass` | DevelopRoads (kind 8) | RoadDevelopmentSystem → road edges | live |
| University | stage atoms + `library AND legal_code_roman` → `building.university`, `inst.university` | `ConstructionQuery.IsProjectAvailable` → `IsKnowledgeEligible` | EnqueueConstruction (kind 4) | ConstructionSystem → InstitutionsSystem → cost modifiers on one subtree; medical university lowers mortality | live |
| Research stage | the four stage atoms | `ResearchQuery.StageReached` | SetResearchTarget on subtree nodes | ResearchSystem → five subtrees open | live |
| Age | e.g., `iron_bloomery` (A4 CORE) | `AgeQuery.CheckAdvance` | AdvanceAge (kind 7) | AgeTransitionSystem → Age state, free unit modernization | live |
| Unit | e.g., military nodes → `unit.*` | listed only | none | Battle layer (M7) | deferred |
| Other buildings/institutions (class J) | e.g., `inst.census`, `inst.quarantine` | `IsKnowledgeEligible` (listed) | none | no realizing system yet | deferred |
| Knowledge-only (class C) | e.g., `knapping_oldowan` (capability strings) | none | none | none — counts toward `research_count` facts and dependants only | knowledge-only |

## 4. Conflations found (MEASURED evidence; classification is INFERENCE)

| Conflation | Where | Evidence | Assessment |
|---|---|---|---|
| Knowledge ↔ capability | "Nuclear power station" (`nuclear_power`) names a realized plant as knowledge; several A9 nodes name devices rather than know-how | node names/capabilities in `research.json` | rename/split in the proposal (D11 lane) |
| Resource discovery ↔ exploitation | deposits exist per settlement from founding and are worked from turn 1; there is nothing to find, identify or prove | `WorldState.cs:270-279`; `ProductionSystem.cs:51-60` | the discovery chain is new design (`fog-of-war-proposal.md` §12) |
| Eligibility ↔ adoption | UI "Adopted civics are completed Civics nodes" | `Lenses.cs:62-68` | Researched ≠ Adopted (M6 spec I-2); fix with the civics UI (T6.6) |
| Research ↔ physical production | farming yield switches with research (activity eligibility) while the content `_doc` calls activities presentation-only | `ProductionSystem.cs:276-288` vs `research.json:11` | intentional effect, mis-documented |
| Age entry ↔ automatic awards | Age entry modernizes units for free (D-047 r18); no Age grants a node | `AgeTransitionSystem.cs:91-101` | intentional; no change |
| Player knowledge ↔ world truth | `PolityKnowledge.Of` is all-known; 4 of 6 AI policies read raw state | `PolityKnowledge.cs:81-85`; leak matrix | the fog proposal closes it |
| Civic ↔ technology pacing | a civic (`legal_code_roman`) is an atom of the research stage | `research.json:254` | see §5 |

## 5. Hidden gates and couplings (classification INFERENCE unless marked)

| # | Coupling | Evidence (MEASURED) | Class | Consequence | Handling |
|---|---|---|---|---|---|
| K1 | Civic `legal_code_roman` gates all five subtrees (stage atom) | `research.json:254`; test `ResearchEngineTests.cs:348` | intentional (summary of `inst.university`'s requirement), consequence under Civics Points unexamined | under a separate CP pool a slow CP rate delays every subtree; ESTIMATE: with CP = 5 % / 10 % of RP the stage slips ≈ 249 / 79 turns on seed 42; ≥ 15 % no delay (`research-balance-analysis.md` §6) | docket D-K1 |
| K2 | Civic `legal_code_roman` is the A5 CORE fact | `ages.json:85` | intentional | CP pacing gates A5 entry (ESTIMATE ≈ 200 / 30 turns at 5 % / 10 %) | docket (D-061.8) |
| K3 | 13 `inst.*` entities require civics (census, coined_wage, patent, joint_stock, legal_code_roman) | `research.json:28984-29152` | intentional | institution eligibility paced by CP | keep (institutions are civic forms) |
| K4 | A6 CORE `heavy_plough` and A7 CORE `printing_press` sit in subtrees | `ages.json:105, 123`; branches in `research.json` | accidental (placement by Age, not by need for scholarship) | Ages A6+ require the research stage; the AI's Age goal closure omits the stage atoms, so the goal stalls once trunk ancestors are done (`AiResearchPolicy.cs:97, 227-233`) | proposal relocations (lanes D01/D05) + AI note |
| K5 | `research_count` technology ≥ 200 (A8), ≥ 280 (A9) | `ages.json:149, 168` | accidental with respect to tree size | adding nodes makes the thresholds a smaller share of the tree (200/426 = 47 % today) | docket: restate as a share or re-ratify |
| K6 | Railway Eureka reads the civic `joint_stock` | content parse (`railway[0]`) | intentional (authored, historically argued) | CP pacing changes a technology's credit | keep, listed as a cross-tree edge |
| K7 | Taxation: civic knowledge + Age ≥ III + seat | `Governance.cs:251-289`; `sim.json:268-270` | intentional (R5, H2) | the one live Age-gated realization (X-06) | keep |
| K8 | AI tie-breaks favour technologies over civics on equal cost (key order) | `ResearchQuery.cs:145-167` | accidental | with two pools it disappears; with one pool civics lose ties | note for T6.3 |
| K9 | Surge "Age of Learning" promises research acceleration with no effect | `ages.json:177-185`; grep | accidental (unimplemented) | misleading UI text | docket (ledger P-23 forbids generic Age modifiers) |
| K10 | Medical university lowers mortality | `sim.json:292` | intentional | a university type with a non-research effect | keep |
| K11 | Content change without schema bump makes diverging v34 UI sessions unresumable; content is not part of identity | `UiSession.cs:1048, 1063-1076`; `WorldHash.cs:14-22` | structural | every content packet needs a content-identity rule | docket (content identity) |

## 6. Measured baseline progression (MEASURED; details, method and seeds in `measurements/baseline-progression.md`)

See `measurements/baseline-progression.md` for the four-seed tables (stage turn, civic completion turns, Age research
facts, node counts, RP totals, longest waits, AI comparison). Headline (seed 42, `sim research --auto cheapest`,
1,630 turns = −4000 to 2100): the research stage opens at turn 1016 (sim-year 5532 ≈ 1532 CE); 200 nodes are complete
by turn 1309; the run ends researching `woodblock_print` with `printing_press` still locked; the whole campaign
generates ≈ 0.70M RP against an 11.06M finite workload (6.3 %). Static lower bounds (cheapest closure only, no
Eureka; ESTIMATE, `tools/balance.py`): stage ≈ turn 335–343, A8 CORE ≈ 1181–1204, A9 CORE unreachable within 1,630
turns on all four seeds.

## 7. Constraints any redesign must carry (MEASURED, `audit/engine-audit.md` §7, Q13, Q14)

1. Keys are identity: never renumber; new technology keys only in 427–1000 (all technologies precede all civics in key
   order); civics stay ≥ 1001; moving a node between trees means re-keying (in practice retire + new).
2. Split, merge, retire and replace lose knowledge in old saves (no alias/remap exists): an alias/remap migration seam is
   an OPEN DECISION the proposal raises once.
3. A cost lowered below saved progress completes for free at the next step; a prerequisite change leaves progress
   dormant; Eureka lists of existing nodes are append-only.
4. Every content packet regenerates three CI `--check` documents and moves the AI-empire pins, the allocation scenario
   (keys 12/17/2/8/52/74/53) and the v33 fixture (key 2) when it touches them; order-less goldens move only if
   zero-knowledge behaviour changes.
5. Loader rules for new nodes: factor ranges, cost tolerance, exactly one primary university per technology, no orphans,
   longest-path `depth`, `finite_nodes_to_exhaust` per subtree recomputed, civics without branch and with
   institution-only entity unlocks (so government forms need a new entity kind — X-07).

## 8. What the audit means for the redesign

- The tree is already large; depth is not the problem. The problems are **conflation** (devices named as knowledge,
  resources with no discovery chain), **91 dead ends**, **single-node bottlenecks** at A8–A9 (up to 757k RP on one
  target), **accidental couplings** (K1, K4, K5, K11) and **thin civics** (7 nodes, one lane, research equated with
  adoption).
- Pacing is a demography/RP question, not a cost question (D-046 §1; X-09): the canonical world researches ≈ 6 % of the
  workload. The proposal reports the effect of every change on workload and critical paths and puts the RP question to
  the Director separately (`director-decision-docket.md`).
- The proposed per-node dispositions are in the inventory files above and in `technology-tree-proposal.md` /
  `civics-tree-proposal.md`.
