# D-046 — Research corpus finalization and the research-foundation gate (2026-10-01)

**Status:** Director rulings, recorded. Implemented on branch `research-progression-foundation` (PR #10, NOT merged).
**Relationship:** follows **D-044** and **D-045**, neither of which is rewritten. The finalization rulings are applied
by **ADR-029 addendum A** and **ADR-030**; CR-018 is resolved by ADR-030. M5 is NOT begun (gate ruling 7).

**Labels:** as in D-045 — **RATIFIED** (a Director ruling or frozen document), **PROVISIONAL CALIBRATION** (numbers
replaceable without a ruling), **FUTURE / OPEN** (a Director decision is required).

**Clause labels:** the gate rulings (Part A.1) are cited as **D-046 G1 … G7**; the finalization prompt (Part A.2) as
**D-046 F§1 … F§6**.

---

## PART A — THE RULINGS (verbatim)

### A.1 — The research-foundation gate

Attachments named by the Director with this message: `research-final-cost-report.csv`, `research-final-cost-report.md`,
`research-final-proposal.json`, `adr-030-per-turn-research-points.md` and the finalization prompt in A.2.

````
CLAUDE CODE — FINAL RESEARCH FOUNDATION GATE

We are closing the research corpus and preparing to move into M5.

Do NOT start M5.

Do NOT redesign the research architecture.

Do NOT merge anything.

Apply the following Director rulings exactly.

==================================================
1. A9 REFERENCE POPULATION
==================================================

DO NOT ratify ~108M as the canonical A9 population.

The ~108M figure is a calibration/validation scenario showing where the current content-derived cost corpus produces approximately 200 final-Age turns.

It is NOT an architecture constant.

Keep:
- A1 = 5,000 as the existing U calibration anchor.
- The other Age populations as provisional validation scenarios only.
- The 40M / 108M / other A9 populations as scenario inputs, not frozen constants.

Technology costs must NOT be changed to force a target Age duration.

The cost model remains content-derived:

BaseCost = U × K^magnitude

No calibration adjustment is permitted merely to hit an Age-duration target.

==================================================
2. CREDIT-ONLY COMPLETION
==================================================

ALLOW credit-only completion.

Do NOT introduce a minimum research-spend requirement.

EffectiveCost remains:

EffectiveCost =
    max(
        0.20 × BaseCost,
        BaseCost × active modifiers
    )

Acceleration credits:

- shared Eureka + foreign-exposure pool
- maximum 40% of BaseCost
- maximum remaining EffectiveCost
- no overflow
- provenance retained by source

If remaining EffectiveCost is less than or equal to available acceleration credit, the node may complete without additional RP expenditure.

This is intentional.

Do NOT add an artificial minimum of 1 RP or any equivalent rule.

Add/retain a regression test for this edge case.

==================================================
3. CAPABILITY OWNERSHIP MATRIX
==================================================

Before research integration is considered complete, create:

docs/design/research-capability-ownership.md

This is REQUIRED before M5.

The document must classify every existing baseline capability/building/unit/activity that intersects the research corpus into exactly one of:

A. BASELINE
   Exists without research.

B. TECHNOLOGY-OWNED
   Requires completion of a specific research node.

C. REALIZATION
   The technology/capability exists, but the actual world object requires
   downstream material, institutional, infrastructure, personnel, construction,
   or other realization conditions.

D. MIXED
   Only if a capability genuinely has a split prerequisite.
   In that case explicitly document which part is baseline and which part
   belongs to research.

Do not use "mixed" as a convenience category.

The purpose is to prevent contradictions such as:

- a granary existing at founding while also being treated as a researched invention
- a workshop being both baseline and technology-owned
- initial agriculture being implicitly equivalent to completion of the full
  agriculture research branch
- baseline military capability accidentally requiring a future military technology
- initial infrastructure accidentally implying later research completion

For every item, record:

- identifier
- current simulation source
- current baseline status
- related research node(s)
- ownership classification
- realization requirements
- whether the item exists at founding
- whether its initial existence represents a capability, an actual object,
  or both
- required code/data changes, if any

Do NOT implement those changes yet unless they are required to make the
research schema internally valid.

The document is an integration contract for M5.

==================================================
4. REPEATABLE RESEARCH
==================================================

DEFER recursive/repeat mechanics.

Keep the 10 repeatable nodes.

Keep the rule:

A repeatable becomes available after finite non-repeatable research in its
own subtree is exhausted.

Do NOT redesign:

- repeat cost escalation
- repeat effects
- repeat-count persistence
- diminishing returns
- repeatable interaction with Age
- repeatable balancing

If the current implementation cannot support actual repetition without
introducing architecture, leave the data representation intact and report
the deferred implementation.

Do NOT block research-foundation closure on recursive mechanics.

==================================================
5. TERMINOLOGY
==================================================

STANDARDIZE on:

Research Points (RP)

Code naming:

ResearchPoints
ResearchProgress
ResearchPointPool

Do NOT use:

CLP
Cognitive Load Points
Cognitive Pool

as active architectural terminology.

Where legacy references exist:

- preserve historical ADR wording when required for provenance
- otherwise update active architecture/code terminology to RP

There is exactly one shared RP pool.

Technology and Civics draw from the same pool.

==================================================
6. FINAL VALIDATION
==================================================

After applying the above:

Run the complete research validation suite specified in the previous
finalization prompt.

Verify:

- schema
- prerequisites
- Group B
- Chinese-script pathway
- Babylonian-specific dependencies
- baseline capabilities
- per-turn RP
- RP anchors
- Eureka
- Eureka idempotence
- foreign exposure credit seam
- shared 40% credit ceiling
- no overflow
- effective-cost floor
- implied Eureka detection
- dead Eureka detection
- determinism
- golden outputs

Recalculate:

- finite research workload by Age
- final-Age workload
- provisional population pacing scenarios
- Eureka counts
- repeatable counts
- Group B reachable-node counts

Do NOT freeze the provisional population calibration.

==================================================
7. STOP CONDITION
==================================================

When complete, report:

1. Branch SHA
2. PR
3. Files changed
4. Capability Ownership Matrix location
5. Research terminology changes
6. Tests added/changed
7. Full test result
8. Golden changes
9. Any remaining implementation blockers
10. Any remaining Director decisions

Then STOP.

Do NOT merge.

Do NOT begin M5.

M5 begins only after I review this final research-foundation state.
````

### A.2 — The finalization prompt the gate applies ("the previous finalization prompt")

````
# CLAUDE CODE — RESEARCH CORPUS FINALIZATION (branch `research-progression-foundation`)

Implement the Director's finalization rulings on the research corpus. **Update the branch and its PR. DO NOT MERGE.**

## Inputs (attached)

- `research-final-proposal.json` — the key `researchJson` is the complete proposed `Sim.Data/content/research.json`
- `research-final-cost-report.md` and `research-final-cost-report.csv` — the cost methodology and per-node costs
- `adr-030-per-turn-research-points.md` — draft ADR
- `research-canonical-proposal.md` — the change report, Group A and Group B

## Before writing

1. Confirm the branch is current with `main` (it was 0 behind at `93270cd`). If `main` has moved, merge it in first and report.
2. Confirm `docs/adr/adr-030-*` does not already exist on any branch.
3. Read ADR-029 §2 (content contract), §5–§7 and §13 in full. The rulings override §13's R-4, R-5, R-10 and R-12.

## 1. Content — `Sim.Data/content/research.json`

Replace it with `researchJson`. It carries:

- **Group B, Director-approved:** 11 prerequisite edges, 4 registry requirements and the research-stage predicate widened where the real dependency is generic writing, law, numeration or record-keeping. Chinese script, counting rods and Shang records are alternative pathways. The two Babylonian nodes stay specific.
- **Group A:** 122 filler Eurekas removed; 6 prerequisite and 11 Age corrections; 1 emergence date; family and generation corrections (medicine and messaging restructured); `inst.newspaper` repaired; research-stage predicate simplified (truth-table proven identical).
- **Content-driven costs.** `BaseCost = U × K^magnitude` with per-node factors in `costRationale`. **Calibration adjustments: none.** Do not reintroduce ADR-029 R-10.
- **81 curated Eurekas on 80 nodes** (73 authored, 8 inherited; 18 evaluable today). The abjad's condition was removed after a reachability test showed it implied by its own prerequisite under Group B. Each Eureka has `kind`, `justification`, `source`, `weight` and `when`, and every node's weights sum to at most 0.40.
- `tuning`: `rpPerTurn`, `effectiveCostFloorFraction` 0.20, `accelerationCreditCeilingFraction` 0.40, `accelerationCreditSources` [eureka, foreign_exposure], `costModel`. The old `clpCoefficient`, `clpAdultExponent` and `eurekaCreditFraction` are removed.
- `researchSets`: `finite`, `recursive` (the 10 repeatables) and `speculative_finite`.
- University relevance per node: **domain classification only, no numbers.**

Bump the content schema version. Update the loader and validator for every new field. Keep strict-null rejection and duplicate-unlock rejection.

## 2. Engine

- **Per-turn RP (ADR-030).** RP/turn = 0.08 × population^log10(5). **Not multiplied by `dtYears`.** Exactly one code site, commented with ADR-030 §2. No reusable "per-turn" mechanism.
- **Effective cost:** `EffectiveCost = max(0.20 × BaseCost, BaseCost × modifiers)`. Modifiers are a seam only: no university system, no maturity, no staffing, no numbers. With no modifiers, EffectiveCost = BaseCost.
- **One acceleration pool.** Eureka and foreign-exposure credits share a ceiling of **40% of BaseCost** per node, also capped at remaining EffectiveCost. No overflow. Record credit **per source** for the Glass Box. Foreign exposure is a **passive seam**: an entry point that accepts credit, with no source implemented.
- **Eureka credit:** each condition credits `weight × BaseCost` once; the default weight is 0.40 ÷ N. Partial credit as conditions fire. Idempotent. Independent of the active target (D-044 R10).
- **Recursive research:** excluded from finite exhaustion and from pacing. Encode the per-subtree availability rule as data. **Do not redesign recursive research**; if repeat-count mechanics are not trivially supported, defer them and report.
- `effects.immediate` stays empty and validated.

## 3. Documents (append-only)

- Commit `docs/adr/adr-030-per-turn-research-points.md` from the draft. Verify its citations: CLAUDE.md law 3, ADR-029 §6 and R-4.
- ADR-029: append an addendum recording the overrides — R-4 (per-turn RP), R-5 (40% of base, shared pool), R-10 (content-driven cost) and R-12 (recursive set) — and marking §1's measured pacing obsolete.
- D-044 Part D T6: append the resolution note. Do not rewrite either record.

## 4. Tests (ruling 13) — extend the existing files; do not weaken any test

| test | file |
|---|---|
| Group B: a world with only Chinese script reaches the research stage; cuneiform-free paths reach `law_code`, `census`, `geometry_practical`, `astronomy_records`, `abacus` | `ResearchContentTests` |
| Group B: `numeral_sexagesimal` and `arithmetic_babylonian` still require cuneiform (specific dependencies kept) | `ResearchContentTests` |
| Per-turn exception: identical RP at equal population across different `dtYears` | `ResearchEngineTests` |
| RP 100 → 2 and 1,000 → 10 (exact to tolerance); sublinear (RP(10P) < 10 × RP(P)) | `ResearchEngineTests` |
| 0 completed research nodes at founding | `ResearchEngineTests` |
| Baseline capabilities: `granary` and `workshop` constructible with zero technology | `ResearchContentTests` |
| Full Eureka = 40% of BaseCost; a university-style modifier does not shrink it | `ResearchEngineTests` |
| Partial multi-condition Eureka (0.40 ÷ N per condition, and explicit weights) | `ResearchEngineTests` |
| Eureka idempotence: a condition never credits twice | `ResearchEngineTests` |
| Eureka + foreign exposure: combined credit ≤ 40% of BaseCost; provenance per source kept | `ResearchEngineTests` |
| Effective-cost floor: no modifier stack goes below 20% of BaseCost | `ResearchEngineTests` |
| No Eureka overflow past remaining cost | `ResearchEngineTests` |
| Implied-by-prerequisite invariant: no knowledge Eureka is satisfied by every path to its node | `ResearchContentTests` |
| Dead-Eureka detection: no condition requires its own node | `ResearchContentTests` |
| Null rejection and duplicate-unlock rejection | `ResearchSchemaTests` |
| Determinism unchanged | `ResearchDeterminismTests` |

## 5. Goldens

Costs and per-turn RP move the four world goldens and the research pins. Re-pin **once**, itemised, with the measured cause. Re-measure the pacing table.

## 6. Report back

Branch, SHAs and PR link; files changed; the exact cost methodology; cost distribution by Age; the final-Age finite workload; optimised and lower-development turn estimates; Eureka counts; Group B changes; new tests and their results; golden re-pins; anything deferred; and remaining Director decisions.

**Do not describe provisional calibration as frozen architecture. Do not merge.**
````

---

## PART B — HOW EACH RULING IS IMPLEMENTED (and its label)

| Ruling | Implementation | Label |
|---|---|---|
| G1 — A9 reference population | No population constant exists in code or content. `docs/research-calibration-report.md` lists 40M, 80M, 107.9M, 113.3M and 160M as scenario inputs and prints the 150 / 200 / 250-turn readings as readings. A1 = 5,000 is checked there as the U anchor (RP(5,000) = 30.808, a magnitude-0 node takes 3.0 turns). No cost carries a calibration adjustment; the loader rejects a non-zero one | RATIFIED rule; every population PROVISIONAL |
| G2 — credit-only completion | `ResearchSystem` step 4 completes any available node whose progress has reached its EffectiveCost, whatever paid for it; no minimum RP spend. EffectiveCost = max(0.20 × BaseCost, BaseCost × Π modifiers). One acceleration pool per node (Eureka + foreign exposure): ≤ 0.40 × BaseCost, ≤ the remaining EffectiveCost, no overflow, provenance per source in `ResearchCredits`. Regression test `ResearchEurekaCreditTests.CreditOnlyCompletion_WhenCreditCoversTheRemainingEffectiveCost_TheNodeCompletesWithNoRpSpent` | RATIFIED |
| G3 — capability ownership matrix | `docs/design/research-capability-ownership.md` — every intersecting item classified A / B / C / D with the nine fields G3 lists. No change it names is implemented (none is needed for schema validity) | RATIFIED; the changes it lists are M5 integration work |
| G4 — repeatable research | The 10 repeatables stay (`researchSets.recursive`). Availability: own prerequisites AND every finite node of the own subtree complete — the rule is data (`repeatable.availability`) and the loader checks `finite_nodes_to_exhaust` against the graph; `ResearchQuery.IsAvailable` applies it. Repeat levels are NOT implemented: a repeatable completes once (D-044 R11). DEFERRED | RATIFIED rule; mechanics DEFERRED |
| G5 — terminology | Research Points (RP) in code, content, CLI and active documents: `ResearchQuery.ResearchPoints`, `ResearchPointPool`, `ResearchProgress`. "CLP" remains only in verbatim historical records (D-044, D-045 Part A, ADR-029's original text, CR-018's evidence) | RATIFIED |
| G6 — final validation | See the PR description and ADR-029 addendum A: the full suites, the content audit (`docs/research-corpus-audit.md`) and the calibration report, both checked in CI | — |
| G7 — stop | Reported; not merged; M5 not begun | RATIFIED |
| F§1 — content | `Sim.Data/content/research.json` = the proposal's `researchJson` (schema `civ-sim/research@2`) with only the loader-required repairs: five stale `depth` fields (derived data) and the unlock reverse index regenerated from the entity requirements; the D-045 `baseline` list kept | RATIFIED content |
| F§2 — engine | ADR-030 per-turn RP (one code site); the floor; the shared pool; the foreign-exposure seam (`ResearchExposures`, no writer); Eureka credit weight × BaseCost once; recursive availability; `effects.immediate` empty | RATIFIED; coefficients PROVISIONAL |
| F§3 — documents | ADR-030 committed from the draft (§7 verifies its citations); ADR-029 addendum A; D-044 Part F (T6 note); CR-018 resolution; D-045 Part E note. Nothing rewritten | — |
| F§4 — tests | The table's tests, in the named files (ADR-029 addendum A lists them) | — |
| F§5 — goldens | Re-pinned once, itemised with the measured cause (ADR-029 addendum A) | Director ruling on the PR |

---

## PART C — EARLIER CHOICES THIS RECORD REPLACES (originals not rewritten)

| Earlier choice | Where recorded | Replaced by |
|---|---|---|
| RP(P) yield of a 10-year reference turn, × dtYears / 10 (CR-018 option 1) | ADR-029 §6; CR-018 | ADR-030 (per turn, never × dt) |
| 40 % of BASE per full Eureka by condition share, its own cap (R-5 as amended) | ADR-029 §7, §13 R-5; D-045 §5–§6 | F§2: explicit weights, one pool shared with foreign exposure |
| BaseCost = band weight × Age unit (R-10 as amended) | ADR-029 §2.4; D-045 §3–§4 | F§1: BaseCost = U × K^magnitude, content-derived |
| 801 corpus strings audited into A–F; one Eureka per node | D-045 §7; `docs/research-corpus-audit-d045.md` | F§1: 81 curated Eurekas on 80 nodes |
| The generated corpus (`scripts/migrate-research-corpus.py`) | ADR-029 §2.1 | F§1: authored content; the generator is retired |

---

## PART D — OPEN (Director decision required; nothing below was decided silently)

1. **The A9 reference population and every non-anchor Age population** — scenario inputs only (G1).
2. **Completion overflow (ADR-029 R-2).** RP past a completion, and RP with no target, is not stored. ADR-030 §2 puts
   "overflow into the next target" inside the per-turn scope without ruling that it carries.
3. **The Age → dt mapping** (architecture §19 item 7), now decisive for calendar pacing (ADR-030 §4).
4. **Repeat mechanics** (G4): cost escalation, effects, repeat-count persistence.
5. **Foreign-exposure and modifier sources:** both seams exist with no writer.
6. **The changes the capability ownership matrix lists** for M5 integration.
7. **The 38 technique candidates** (D-045 §11) — carried forward in the audit, not moved.
8. **Milestone placement (CR-005)** — unchanged from D-044 Part D T1.
