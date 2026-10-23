# M6 knowledge redesign — design package (PROPOSED, for Director review)

> **Update 2026-10-23.** The Director's final M6 rulings are recorded in `docs/d062-m6-director-final-rulings.md`, and
> a first slice is **implemented** on this branch: Civics Points and the stage gate (ADR-043), deposit concealment,
> Prospect/Explore, minimal fog and the Actions menu (ADR-044), and the non-breaking content tranche of this package's
> tree (ADR-045). What was built and what is deferred: `m6-slice-contract.md` (as built), `implementation-status.md`,
> `implementation-roadmap.md` (status section). Evidence: `m6-validation-report.md`. The rest of this package remains
> design input; the educational corpus is still blocked from shipping (no source page could be opened).

**Status: research and design only. Nothing here is implemented, ratified or runtime content.** No file under `Sim.*`,
no production content, schema, save format, golden or test changed. Labels: MEASURED · RESEARCHED · INFERENCE ·
PROPOSED · OPEN DECISION · IMPLEMENTATION DEFERRED · ESTIMATE (static model).

| Ref | Commit |
|---|---|
| Work started from | `edba2b5` (M6.1 ruling docket) |
| Package checkpoint / first publication | `ce6bcfd` / `92fe440` |
| This revision (Director-intent refinement, 2026-10-22) | the next docs-only commit on `claude/new-session-yavghm` (SHA in the commit log; not self-referenced) |
| `main` = `m5-exit` | `6e51a3e` (the published M5 closure; unchanged) |

**Start here:** `director-intent-reconciliation.md` (what changed against the Director's intent, conflicts, ranked
corrections), then `director-decision-docket.md` §1 (the six architecture-gate items).

## 1. Executive overview

- **Current state (MEASURED).** 433 nodes (426 technology, 7 civics), 11.2M RP of workload. A non-expanding polity
  researches ≈ 6 % of it in 1,630 turns and reaches the university stage at turn 996–1016; the AI empire reaches it at
  702–716. 91 knowledge-only dead ends; medieval Age facts wait on the stage; one `legal_code_roman` civic gates the
  stage, the university and A5; `taxation` gates the live tax edict together with an Age III rule in `sim.json`. There is
  no fog state and the AI reads world truth.
- **Proposal (PROPOSED).** 494 technologies (70 new, 14 splits, 2 merges, 69 rewires, 59 re-scores, 34 craft
  relocations) and 67 civics (7 legacy anchors, 42 micro, 18 regime forms, eligibility only in M6); a separate Civics
  Points economy generated from its own population-led inputs (`cp-economy-proposal.md`); a unit-led fog of war with
  UNKNOWN / VISIBLE / REMEMBERED states, dated ghosts for enemy formations and strict AI parity
  (`fog-of-war-proposal.md`); a realization class on every capability; and a 50–60-word educational text per node.
- **Principles.** Research grants eligibility only; it never creates or reveals deposits or updates the remembered
  map; prerequisites are causal; costs come from the six-factor rubric and are never moved to hit an Age duration;
  every promise names its owner system and milestone; player and AI see the same information.

## 2. What is measured and what is not

| Kind | What |
|---|---|
| MEASURED | current graph and costs; baseline runs on seeds 1, 2, 42, 7 (driver and AI empire); code consumers and gates; validator results; corpus counts |
| ESTIMATE (static model) | every turn or capability figure for the **proposed** graph and every Civics Points gate figure — lower bounds from measured RP trajectories, **not gameplay results**; no simulation of the proposed graph exists |
| Not verified | educational texts: **0 of 561 may ship** (0 source-checked, 113 search-corroborated, 250 partially checked, 198 unverified). Zero structural errors is not a fact-check |

## 3. Files

| # | File | Content |
|---|---|---|
| 1 | `README.md` | this overview |
| 2 | `director-intent-reconciliation.md` | intent → package mapping, the ten known issues, governance conflicts, ranked corrections |
| 3 | `director-decision-docket.md` | staged docket: A architecture (6), B per packet, C later; KD detail; record corrections; empty ruling block |
| 4 | `current-tree-audit.md` (+ `audit/`) | inventory with dispositions, engine audit, fog/AI audit, governance digest |
| 5 | `technology-tree-proposal.md` / `.json` (+ `proposal/technology-*.md`) | technology graph (canonical JSON; schema `data-schema.md`) |
| 6 | `civics-tree-proposal.md` / `.json` (+ `proposal/civics-detail.md`) | civics graph; §4a legacy-civic audit; §6 CP tolerances P1–P7 |
| 7 | `cp-economy-proposal.md` | CP source catalogue, candidate equations, government-change exchange slot, cross-pool gate options, conflicts G-1…G-9 |
| 8 | `fog-of-war-proposal.md` | fog design: three states, unit-led sight, M6 exploration options X1–X6, ghosts, AI parity, deviation register §18.0 |
| 9 | `research-balance-analysis.md` | costs, closures, ESTIMATE turns, sensitivity, CP gate summary |
| 10 | `capability-dependency-matrix.md` (+ `matrix/`) | per-node capability, inputs, owner, milestone, realization class |
| 11 | `educational-content-corpus.md` (+ `corpus/*.json`, `corpus/*.md`, `corpus/sourcing-queue.md`) | 561 texts, shipping gate, evidence status, remaining queue |
| 12 | `historical-source-register.md` (+ `sources/register.json`) | 1,153 sources (all retrieved as search results) |
| 13 | `civ-vi-comparative-research.md` | cited Civ VI and comparative study |
| 14 | `implementation-roadmap.md` | packets KR-0…KR-16, staged gates |
| — | `design-framework.md`, `data-schema.md`, `validation-report.md`, `generated-index.md`, `measurements/`, `research/`, `data/`, `tools/` | method, schema, validation, measurements, working files, tooling |

Outside the package, this revision only **appends** a non-ratified "Director intent note — 2026-10-22" to
`docs/d061-civics-points-supersession.md`.

## 4. How to reproduce (design tooling only; reads production content read-only)

```
python3 -I tools/extract_current.py     # current inventory from Sim.Data/content
python3 -I tools/integrate.py           # lane parts + overrides -> proposal JSON (deterministic)
python3 -I tools/merge_sources.py       # source register
python3 -I tools/validate.py            # graph + gate + corpus checks -> validation-report.md (exit 1 on ERROR)
python3 -I tools/sourcing_queue.py      # evidence progress -> corpus/plan.json, corpus/sourcing-queue.md
python3 -I tools/unresolved_claims.py   # per-node evidence + unresolved claims -> corpus/unresolved-claims.md, data/unresolved-claims.json
python3 -I tools/test_validate.py --mutants        # adversarial validator fixtures (temp copies; tracked files untouched)
python3 -I tools/test_sourcing_queue.py --mutants  # idempotence, evidence preservation, plan agreement, no status upgrade
python3 -I tools/render.py              # Markdown views
python3 -I tools/balance.py --graph proposed --json <out>   # static model (ESTIMATE)
python3 -I tools/cp_gate.py             # CP gate static model -> data/balance-cp-gate.json (ESTIMATE)
```

Results of the last run are in `validation-report.md` (header) and the handoff.

## 5. Next Director action

Rule docket group A (A-1 … A-6) — three real choices: how a CP "pool" is read (A-1), which M6 exploration option
(A-3; X2 recommended, with almost no exploration until M7), and the `legal_code_roman` gate (A-4; Option A
recommended). Groups B and C wait for their packets. Separately, allowing source hosts in the environment's network
settings is the only way the corpus can reach `source-checked`.
