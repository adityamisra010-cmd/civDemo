# Knowledge redesign — design framework (rules every design lane follows)

**Status: PROPOSED design method for Director review. It changes no ruling.** Where a rule below is a ratified record it
is cited; where it is this package's proposal it says PROPOSED. Evidence labels used throughout the package:
**MEASURED** (read from code, content, tests or a run, with a citation) · **RESEARCHED** (external source, in the
register) · **INFERENCE** · **PROPOSED** · **OPEN DECISION** · **IMPLEMENTATION DEFERRED**.

## 1. The chain every node must sit in

`human knowledge → scientific / civic prerequisites → geographical discovery → resource access → production and
institutions → available capabilities → realized simulation effects`

A node is a **piece of knowledge** (D-044 R1: technology = the civilization's knowledge). Completing it changes one thing
— the completion row — and makes things **eligible** (D-044 R11/R14; M6 spec I-1). It never creates a deposit, a
material, a factory, equipment, an institution or a workforce; those are realized by their owning systems, each with
its own inputs. A node that promises a capability must name the system that will realize it and the milestone that
owns that system; a node with no realizing owner is **knowledge-only** and says so.

## 2. Structural rules (RATIFIED unless marked)

| # | Rule | Source |
|---|---|---|
| S1 | Exactly two top-level trees: Technology and Civics. Technology = main trunk + five subtrees (Military, Medicine, Engineering, Natural Science, Agriculture). No other tree or numbered subtree (no Industry & Energy, Naval or Science tree). | D-044 R3 (★ settled) |
| S2 | The five subtrees open together when the university / research institutional stage is reached — not by Age. | D-044 R4 |
| S3 | One authoritative Technology graph; views may show a node in several places, never duplicate it. | D-044 R16 |
| S4 | Prerequisites are causal (knowledge actually needed), not chronology; AND / OR / nesting; no cycles. | D-044 R8; D-047 r1 |
| S5 | Completion = eligibility only; downstream realization by the relevant system. | D-044 R11, R14 |
| S6 | Age is metadata and a computed state: research never reads the Age; the Age never grants a node. | D-044 R13; law 4 |
| S7 | Costs are content-derived: BaseCost = 92.4 × 2^magnitude, magnitude = Σ six factors; no calibration adjustment; costs are never changed to hit an Age duration. | D-046 §1; ADR-029 add. A |
| S8 | Eureka + foreign exposure share one credit pool ≤ 40 % of BaseCost; no overflow. | D-044 R10; D-046 §2; D-047 r10 |
| S9 | Stable integer keys; never renumber; append new nodes with new keys. | `research.json` `_doc`; ADR-029 |
| S10 | MEASURED conventions of the current graph that this package keeps as validator rules: no Age inversion (a node's Age ≥ every prerequisite's Age); the trunk is closed under prerequisites (no trunk node requires a subtree node); no technology prerequisite names a civic; every node at A7–A9 sits in a subtree. | `tools/validate.py` V04, V05, V17 on `data/current-tree-inventory.json` |

## 3. Node kinds (PROPOSED) — separating understanding from application

| kind | what it represents | typical grants |
|---|---|---|
| `science` | understanding of how nature works (an explanation, a law, a model) | knowledge, prerequisites of applications |
| `application` | a practical capability built on understanding (a machine, a process, a device) | production, building, infrastructure |
| `technique` | a practice or method that changes how people work | production (activity), knowledge |
| `resource` | finding, recognizing, testing or proving a resource | identification, prospecting |
| `infrastructure` | knowledge of a network or structure class | infrastructure, building |
| `institutional` | knowledge of an organizational form that an institution realizes | institution |
| `military` | military knowledge whose assets the battle layer fields | unit |
| `medical` | medical knowledge whose practice society systems realize | knowledge (M9 owner) |
| `civic-micro` / `civic-macro` | Civics tree only (§9) | institution, policy, regime |

A **split** is justified when one node bundles understanding with application (e.g., discovering a phenomenon and
engineering with it), two independently useful capabilities, or two alternative routes. A split is not justified to
add clicks.

## 4. Dispositions and the key policy (PROPOSED)

`keep` · `rewire` (prerequisites repaired for causality; cost moves only through the prerequisite-complexity convention) · `rebalance` (factors re-scored with a stated reason) · `split` (the existing key stays with the part that
preserves the node's current consumers, Age facts and entity requirements; new parts get new keys) · `merge` (the
absorbed key is retired and maps to the survivor) · `replace` (the old key is retired and maps to the replacement) ·
`relocate` (branch, Age metadata or tree changes; key unchanged) · `retire` (key reserved forever, never reused).

Key ranges (PROPOSED, shared key space with a reserved civics range — the D-061.4 revised recommendation, docket C.5):
technology 1–999 (new keys from 427), civics 1000–1999 (new keys from 1008). `tools/integrate.py` assigns new keys
deterministically by (Age, branch order, id) — technology — and (Age, micro before macro, id) — civics. Keys are
PROPOSED until the graph is ratified.

## 5. Capability stages (PROPOSED vocabulary over repository terms)

| stage | meaning | owner in the repository today | status |
|---|---|---|---|
| knowledge | the node is complete for the polity | ResearchSystem (completion rows) | built |
| identification | the polity can recognize a resource kind in territory it has observed | proposed DiscoverySystem / fog memory (T6.7–T6.9) | proposed |
| access | the polity can reach, claim or traverse the place | control, catchments, pathfinder, roads | built (all-known today) |
| production | a recipe or labour activity is eligible | ProductionSystem, LabourActivities | built |
| infrastructure | a road class or structure type is eligible | RoadDevelopmentSystem, ConstructionSystem | built |
| institutional | an institution may be founded | ConstructionSystem + InstitutionsSystem | partly built (university) |
| availability | Researched/Available state shown and evaluable | ResearchQuery | built |
| adoption / activation | a civic, regime or policy is chosen and in force | M8 (Politics) | deferred |
| realization | the entity exists in the world | the owning system | per entity |

## 6. Resource discovery chain (PROPOSED; detailed in `fog-of-war-proposal.md`)

`deposit exists` (world geography, worldgen) → `location discovered` (fog: in the polity's known extent) →
`resource identified` (technology: an identification capability, applied to observed cells) → `prospected` (an action
or institution establishes grade and size) → `extractable` (access + an extraction technique + infrastructure) →
`processable` (a processing technique + a facility) → `industrial use` (application nodes and their consumers).
Research supplies only the identification, extraction, processing and use **knowledge**; it never places, reveals or
proves a deposit. Nuclear research in particular must never reveal uranium deposits.

## 7. Costs (PROPOSED method within S7)

- Kept nodes keep their cost. A `rebalance` re-scores factors with a reason grounded in the node's content (novelty,
  difficulty, material, institutional, breadth, prerequisite complexity) and is reported with old and new cost.
- New nodes are scored on the same six factors (quarter steps, 0–4) and name a `costAnalog` — an existing node of
  comparable difficulty at the same Age — so the scoring is auditable.
- A split's parts are scored independently; the package reports the sum against the original (a split that more than
  doubles the original workload, or makes it cheaper by more than a quarter, must say why).
- No cost is chosen to hit an Age duration (D-046 §1). Pacing is analysed, not targeted (`research-balance-analysis.md`).

## 8. Optionality and alternative routes

- Every Age CORE fact and the research stage must remain reachable; the validator lists their cheapest closures.
- Optional branches (alternative routes, specialisms, knowledge-only nodes) must not become mandatory for progression.
- Alternative routes use OR where history shows genuinely different paths (independent inventions, regional
  materials), not to soften costs.

## 9. Civics (PROPOSED; the currency question is D-061, OPEN)

- Micro-civics: practices and institutions (census, coinage, courts, civil service, treaties, commercial law, public
  education, sanitation administration, property records, local government, succession procedures, standing armies).
- Macro-civics: regime and institutional forms (chiefdom, centralized kingship, feudal/decentralized monarchy,
  republic, bureaucratic empire, constitutional monarchy, federation, parliamentary system, one-party state, communist
  system, and other historically defensible forms).
- States (M6 spec I-2): Unresearched → Researched/Available → Adopted → Active → Realized. Completion makes a civic
  **eligible**; adoption and political effects belong to M8 (D-D). No Age grants a government form; no macro civic is an
  Age CORE fact.
- Costs are stated in points of the civics pool: CP if D-061 is ratified, RP from the shared pool otherwise.
- Cross-tree edges: a civic may require technology knowledge (writing before written law). A technology requiring a civic
  is a fenced "exposure" edge that must be listed and justified (today: none in prerequisites; the research stage and
  `inst.*` requirements carry civic atoms — docket C.1).

## 10. Milestone ownership (from `docs/milestones.md` and the M6 spec)

M6: knowledge, eligibility, civics eligibility, fog S2a–S2c, diffusion, universities/deferred entities (T6.11), unit
progression contract (inert). M7: battle layer, unit movement and Action Points, mobile agents (scouts are the first
candidate). M8: politics/diplomacy — adoption, regimes, policies, treaties. M9: society — culture, religion, social
structure, opinion, disease/crisis. M10: integrated simulation across A1–A9. M11+: depth and content (new industries,
energy systems, new resources, applications). A node whose realization has no owner before M11+ is knowledge-only until
then and says so.

## 11. Validation (what `tools/validate.py` enforces)

Graph: unique ids/keys and key ranges; complete old→new mapping with unchanged keys; resolved prerequisites; no
cycles; trunk closure; Age monotonicity; research stage and Age CORE facts resolvable (civic atoms warned, macro civics
refused); cost = model within tolerance; quarter-step factors; capability block present with status, owner and
milestone; grant vocabulary; dead-end knowledge-only nodes warned; Eureka ceiling; the seven existing civics present;
lineage (split/merge/replace) consistent. Corpus: one entry per active node; 50–60 words (45–65 allowed); sources
present and registered; fact-check status present; duplicate and near-duplicate text; inevitability wording;
game-mechanic wording; quotation length.
