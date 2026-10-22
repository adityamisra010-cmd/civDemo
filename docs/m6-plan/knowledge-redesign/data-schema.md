# Knowledge redesign — data schema and canonical files

**Status: PROPOSED design data. Nothing here is runtime content.** The simulation reads
`Sim.Data/content/*.json`; this package never writes there. These files are the canonical, machine-checkable
form of the proposal; the Markdown views are generated from them by `tools/render.py` and must not be edited by
hand (edit the JSON, re-render, re-validate).

## 1. Which file is canonical

| Concern | Canonical file | Generated views |
|---|---|---|
| Current tree (MEASURED baseline) | `data/current-tree-inventory.json` (from `tools/extract_current.py`, reads production content read-only) | `current-tree-audit.md`, `audit/current-tree-inventory-*.md` |
| Proposed technology graph | `technology-tree-proposal.json` | `technology-tree-proposal.md`, `proposal/technology-*.md` |
| Proposed civics graph | `civics-tree-proposal.json` | `civics-tree-proposal.md` |
| Educational corpus | `corpus/*.json` (one file per topic; `corpus/index.json` lists them) | `educational-content-corpus.md`, `corpus/*.md` |
| Source register | `sources/register.json` (merged from `sources/*-sources.json`) | `historical-source-register.md` |
| Lane working files | `data/proposal-parts/<lane>.json` (inputs to `tools/integrate.py`) | — |
| Ownership partition | `data/partition.json` (every existing node owned by exactly one design lane) | — |

## 2. Lane part files (`data/proposal-parts/<lane>.json`) — written by the design lanes

```jsonc
{
  "lane": "D02-materials-mining",
  "summary": "one paragraph: what this lane changed and why",
  "existing": [            // EVERY existing node the lane owns, exactly once
    {
      "id": "copper_smelting",                    // existing id (never renamed here; a rename is a field change)
      "disposition": ["keep"],                    // keep | rewire | rebalance | split | merge | replace | relocate | retire (combinable)
      "rationale": "why this disposition (one or two sentences)",
      "changes": "exact field changes vs current, or 'none'",
      // optional overrides, present ONLY when the field changes:
      "name": "...", "branch": "...", "age": "A4", "prereq": "a AND (b OR c)", "prereqRationale": "...",
      "factors": {"novelty":0,"difficulty":0,"material":0,"institutional":0,"breadth":0,"prereq_complexity":0},
      "costRationale": "why the re-scored factors are right (never 'to hit an Age duration')",
      "splitInto": ["new_id_a"],                  // split: this record keeps the key and describes the retained part
      "mergeInto": "survivor_id",                 // merge: this node is retired into the survivor
      "replacedBy": "new_id",                     // replace
      "retireReason": "...",                      // retire
      "compatibility": {"saves": "", "ids": "", "ui": "", "ai": "", "consumers": "", "tests": ""},  // required unless keep
      "capability": { ... }                       // optional override of the auto-filled block (see section 4)
    }
  ],
  "new": [
    {
      "id": "snake_case_unique", "name": "Display name",
      "branch": "trunk|military|medicine|engineering|natural_science|agriculture",
      "age": "A1..A9", "domain": "existing domain vocabulary", "secondaryDomains": [],
      "emerged": "short, sourced-later historical anchor or null",
      "prereq": "expression over node ids (AND / OR / parentheses; no NOT)",
      "prereqRationale": "why each edge is CAUSAL (knowledge actually needed), not chronological",
      "factors": {"novelty":0,"difficulty":0,"material":0,"institutional":0,"breadth":0,"prereq_complexity":0},
      "costAnalog": "existing id with comparable factors (calibration anchor)",
      "nodeKind": "science|application|technique|resource|infrastructure|institutional|military|medical",
      "capability": { ... },                      // section 4 — mandatory for new nodes
      "eurekas": [ {"text":"", "kind":"", "class":"", "weight":0.0, "justification":""} ],
      "family": null, "generation": null,
      "splitFrom": null,                          // existing id if this node is carved out of it
      "whyDistinct": "why this merits its own node (decision, dependency, capability or educational value)",
      "acceptanceTest": "the observable test that proves the node does what it promises"
    }
  ],
  "crossLaneRequests": [ {"targetLane": "D11-nuclear-geology", "request": "", "reason": ""} ],
  "openQuestions": [ "" ]
}
```

## 3. Canonical proposal files

`technology-tree-proposal.json` and `civics-tree-proposal.json` share one node shape (civics add the civic block):

| Field | Meaning |
|---|---|
| `key` | stable integer. Existing keys are kept. New technology keys are assigned by `tools/integrate.py` from 427 upward in the order (Age, branch order trunk→military→medicine→engineering→natural_science→agriculture, id); new civic keys from 1008 upward in the order (Age, micro→macro, id). PROPOSED until ratified; retired keys are never reused. |
| `status` | `existing` · `new` · `retired` (retired records stay in the file so the old→new mapping is complete) |
| `disposition` | list of keep/rebalance/split/merge/replace/relocate/retire (`new` for new nodes) |
| `lane` | the design lane that owns the record |
| `id`, `name`, `tree`, `branch`, `domain`, `secondaryDomains`, `age`, `emerged` | as in production content |
| `prereq`, `prereqRationale` | proposed expression; rationale for new/changed edges |
| `cost`, `oldCost`, `magnitude`, `oldMagnitude`, `factors`, `costRationale` | BaseCost = 92.4 × 2^magnitude rounded to 10; magnitude = sum of the six factors |
| `nodeKind` | separates scientific understanding (`science`) from practical application (`application`, `technique`, `infrastructure`, `resource`, `institutional`, `military`, `medical`) |
| `capability` | section 4 |
| `eurekas`, `family`, `generation` | as in production content |
| `splitFrom`, `splitInto`, `mergedInto`, `mergedFrom`, `replaces`, `replacedBy` | lineage |
| `whyDistinct`, `acceptanceTest`, `rationale`, `changes`, `compatibility` | design justification and migration risk |
| `derived` | computed by `tools/integrate.py`: `depthMin`, `closureCount`, `closureCost`, `dependants`, `optional` |

Civic-only fields (`civic` block): `category` (`micro` · `macro`), `concept` (the institution, principle or regime),
`states` (what Researched/Available, Adopted, Active and Realized mean for this civic), `eligibility` (what completion
makes eligible), `currentEffect`, `deferredEffect`, `ownerSystem`, `milestone`, `ageRelation`, `techLinks`,
`institutionLinks`, `aiValuation`, `required` (true only if a ratified progression gate needs it), `costUnit`
(`CP if D-061 is ratified, otherwise RP`).

## 4. The capability block (every node)

```jsonc
"capability": {
  "grants": ["production:recipe.bronze", "institution:inst.university", "identification:deposit.uranium"],
  "realizationOwner": "ProductionSystem | ConstructionSystem | InstitutionsSystem | RoadDevelopmentSystem | TradeArbitrageSystem | GovernanceSystem | AgeTransitionSystem | ResearchSystem | DiscoverySystem (proposed, T6.7) | Battle layer (M7) | Adoption (M8) | none",
  "status": "realized-today | knowledge-only | deferred",
  "milestone": "existing | M6 | M7 | M8 | M9 | M10+",
  "currentEffect": "what completing it does today (MEASURED for existing nodes)",
  "deferredEffect": "what it will do once its owner exists, or null",
  "inputs": {"resources": [], "geography": [], "infrastructure": [], "institutions": []},
  "missingInputs": "behaviour when a physical input is missing (eligible but not realizable, and why)",
  "unknownResource": "behaviour when the polity has not located/identified the resource",
  "aiValuation": "how the AI should value the node (and under fog, from what information)",
  "playerCommunication": "what the player is told, including why an action stays blocked"
}
```

Grant prefixes (PROPOSED vocabulary): `knowledge:` (knowledge-only statement), `identification:` (recognize a
deposit/resource kind in observed territory), `access:` (reach/claim/traverse), `production:` (recipe or labour
activity eligibility), `infrastructure:` (road class or structure eligibility), `institution:` (institution founding
eligibility), `unit:` (unit family eligibility, realized by the M7 battle layer), `policy:`/`regime:` (civic
adoption eligibility, M8), `trade:`, `tax:`, `stage:` (research stage atom), `age:` (Age milestone fact).

For existing nodes the block is AUTO-FILLED by `tools/integrate.py` from the CI-checked unlock audit
(`docs/research-gameplay-unlock-audit.md` §5) and the node's `unlocks`; a lane overrides it only where the auto-fill
is wrong or incomplete.

## 5. Corpus files (`corpus/<topic>.json`)

```jsonc
[{
  "id": "copper_smelting", "tree": "technology",
  "description": "50–60 words of original prose (45–65 allowed where accuracy requires)",
  "wordCount": 57,                                  // recomputed by the validator
  "sources": [ {"sourceId": "SRC-0123", "claims": ["the specific claim this source supports"]} ],
  "uncertainty": "disputed origins, date ranges, multiple centres — or null",
  "factCheck": {"status": "source-checked | search-corroborated | partially-checked | unverified",
                "checkedBy": "drafting lane | independent check",
                "method": "what was compared against what", "notes": ""},
  "mechanicLink": "how the text relates to what the node actually does in the simulation"
}]
```

## 6. Source register (`sources/register.json`)

`{"id", "title", "author_org", "published", "url", "accessed", "type", "claims_supported", "confidence", "notes",
"usedBy": [node ids]}` — `type` is one of official · archive · academic · museum · university · reference · industry ·
press · community. A citation records where a claim can be checked; it is not proof that the claim was verified
(the corpus `factCheck` block records that separately).
