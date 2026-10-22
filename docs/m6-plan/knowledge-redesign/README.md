# M6 knowledge redesign — design package (PROPOSED, for Director review)

**Status: research and design only. Nothing here is implemented, ratified or runtime content.** No file under `Sim.*`,
no content, schema, golden or test changed. Baseline: branch `claude/new-session-yavghm` @ `edba2b5`; `main` =
`m5-exit` = `6e51a3e` (matches the published M5 closure). Labels: MEASURED · RESEARCHED · INFERENCE · PROPOSED · OPEN
DECISION · IMPLEMENTATION DEFERRED.

## 1. Executive overview

- **Current state (MEASURED).** 433 nodes (426 technology, 7 civics), 11.2M RP of workload, 79.5 % of it in A9. The
  canonical world researches ≈ 6 % of it in a full 1,630-turn game; the university stage opens at turn 996–1016
  (≈ 1500 CE) for a non-expanding polity and 702–716 for the AI empire. 91 knowledge-only dead ends; medieval and
  early-modern Age facts silently wait on the university stage; civics are 7 nodes equated with adoption in the UI;
  deposits are per settlement with no discovery chain and no uranium; there is no fog state and the AI reads world truth.
- **Proposal.** A causally repaired, moderately deeper technology graph (494 nodes: 70 new, 14 splits, 2 merges, 69
  rewires, 59 content re-scores, 34 craft relocations to the trunk), a distinct civics tree (67 civics: 42 micro, 18
  regime forms, 7 existing), a 27-node geology/nuclear case study, a code-grounded fog-of-war model (geometric sight,
  monotone extent, sighting memory incl. formation ghosts, scoped AI queries, metamorphic leak tests) with a resource
  discovery chain (exists → located → identified → prospected → extracted → processed → used), an original 50–60-word
  educational text for every node, and an implementation roadmap of 17 small packets.
- **Principles.** Knowledge grants eligibility only; research never creates deposits, materials, factories or people;
  prerequisites are causal; crafts stay in the trunk; costs come from the six-factor rubric and are never moved for
  pacing; every promise names its realizing system and milestone; player and AI see the same information.

## 2. Files

| # | File | Content |
|---|---|---|
| 1 | `README.md` | this overview |
| 2 | `current-tree-audit.md` (+ `audit/current-tree-inventory-*.md`, `audit/engine-audit.md`, `audit/fog-and-ai-information-audit.md`, `audit/governance-constraints-digest.md`) | complete inventory with proposed dispositions, engine audit, couplings, measured baseline |
| 3 | `technology-tree-proposal.md` (+ `proposal/technology-*.md`) | proposed technology graph |
| 4 | `technology-tree-proposal.json` | canonical technology data (schema `data-schema.md`) |
| 5 | `civics-tree-proposal.md` (+ `proposal/civics-detail.md`) | proposed civics graph |
| 6 | `civics-tree-proposal.json` | canonical civics data |
| 7 | `educational-content-corpus.md` (+ `corpus/*.json` canonical, `corpus/*.md`) | 561 node texts with sources and fact-check status |
| 8 | `historical-source-register.md` (+ `sources/register.json`) | 755 consolidated sources |
| 9 | `research-balance-analysis.md` | costs, counts, critical paths, sensitivity, Civics Points interaction |
| 10 | `capability-dependency-matrix.md` (+ `matrix/*.md`) | per-node capability, inputs, owner, milestone, AI, UI, tests, migration |
| 11 | `fog-of-war-proposal.md` | fog design (§19 resolutions) |
| 12 | `civ-vi-comparative-research.md` (+ `sources/comparative-sources.json`) | cited Civ VI and comparative study |
| 13 | `implementation-roadmap.md` | packets KR-0…KR-16 |
| 14 | `director-decision-docket.md` | 29 decisions (KD-01…KD-29) with recommendations |
| — | `design-framework.md`, `data-schema.md`, `validation-report.md`, `generated-index.md`, `measurements/`, `research/` (lane notes, briefs, adjudication), `data/`, `tools/` | method, schema, validation, measurements, working files, tooling |

Canonical data is JSON; Markdown views marked "generated" are produced by `tools/render.py` and are not edited by hand.

## 3. How to reproduce (design tooling only; reads production content read-only)

```
python3 -I tools/extract_current.py   # current inventory from Sim.Data/content
python3 -I tools/integrate.py         # lane parts + overrides -> proposal JSON (deterministic)
python3 -I tools/merge_sources.py     # source register
python3 -I tools/validate.py          # graph + corpus checks -> validation-report.md
python3 -I tools/render.py            # Markdown views
python3 -I tools/balance.py --graph proposed --json data/balance-proposed.json
```

Last run: integration 0 problems; validation **0 ERROR**, 391 WARN (319 = corpus entries awaiting sources; 53 =
deferred regime/policy grants; 13 justified dead ends; 2 deliberate A7 trunk crafts; 2 civic gates; 2 chronology
notes), 4 INFO.

## 4. Risks and scope

- Pacing cannot be fixed by costs (D-046); the RP/demography question is separate (KD-10).
- A slow Civics Points economy can stall all technology through the stage's civic atom (KD-03; tolerances P1–P6).
- Old saves lose split/merged knowledge without the remap seam (KD-08).
- The frozen Spine limits nuclear mechanics to deterrence (KD-20).
- 319 corpus entries are unverified (search allowance exhausted); none may ship before sourcing (KD-25).

## 5. Next steps

1. The Director rules the BLOCKING items in `director-decision-docket.md` (KR-0).
2. A follow-up sourcing pass for the unverified and partially-checked corpus entries.
3. KR-1 (engine fixes, harness, content identity) as the first code packet once ruled.
