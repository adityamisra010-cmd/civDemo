# D-062 — M6 final Director rulings: knowledge, Civics Points, exploration and prospecting

**Status: RULED by the Director, 2026-10-22.** Recorded by the agent from the Director's written instruction of
2026-10-22 ("M6 Master Prompt: Knowledge Redesign, Governance, Exploration and Implementation"), which states: *"My
latest decisions are final and supersede any conflicting earlier rulings, frozen rules, architecture decisions, and
proposals. Record these changes through the repository's append-only governance process. Preserve historical records,
explicitly identify superseded provisions, and never silently rewrite old rulings. Do not ask me to approve decisions I
have already made."* This record is that recording. Superseded clauses are **not edited**; each superseded record
receives an appended, dated pointer note naming this record (§4). Precedence (gov-4 §1): an explicit Director ruling
ranks above D-records, ADRs and the spec.

Labels: **RULED** (the Director's words, quoted or closely restated) · **DELEGATED** (a detail the Director told the
agent to define; decided in the named ADR, changeable by ordinary design change) · **SUPERSEDED** (an earlier
provision displaced by a RULED item).

---

## 1. The Director's rulings (RULED, 2026-10-22)

| # | Ruling |
|---|---|
| R1 | M6 covers Knowledge, Research, Technology, civic **eligibility**, and a **minimal functional exploration/prospecting mechanic**. |
| R2 | M7 owns the full movement and Action Points system. |
| R3 | M8 owns civic adoption and active political effects. |
| R4 | Civics Points (CP) are separate from Research Points (RP), **accumulate in a bankable balance**, and are spent on civic progression. |
| R5 | The university research-stage gate no longer requires `legal_code_roman`; `legal_code_roman` remains a university prerequisite. |
| R6 | Resource deposits are randomly distributed across the map. |
| R7 | Approximately 70–80 % of eligible deposits are discoverable without prospecting; the remaining 20–30 % are concealed until discovered through exploration/prospecting. |
| R8 | The UI has separate **Construction** and **Actions** menus. Construction contains building orders such as Build Granary; Actions contains Explore and Prospect. |
| R9 | Some technologies may enable or improve resource discovery where causally appropriate. |
| R10 | The proposed nuclear knowledge progression is accepted as a design direction; **no new nuclear simulation mechanics in M6**. |
| R11 | Implementation is minimal, deterministic, testable and compatible with the existing architecture; where earlier architecture rules conflict with these decisions, the governance records are updated and the smallest coherent solution is implemented. |
| R12 | Implementation proceeds under these decisions without a further approval round: *"Do not stop after producing proposals when implementation can safely proceed under these final decisions."* |
| R13 | Technology and civic trees: a coherent redesign is produced and implemented — causal prerequisites, meaningful intermediate nodes, artificial dependencies removed, **costs adjusted where needed to support pacing**, stable identifiers preserved where required with any remapping documented. |

## 2. Details decided under delegation (DELEGATED; the governing ADR is named)

| Topic | Decision | ADR |
|---|---|---|
| CP generation | per sim-year (law 3): `CP = c × P^β × dtYears`, P = the polity's population as RP reads it; c, β tunable (`research.json` `tuning.civicsPoints`); institution / specialist / embassy / spy / artist / scholar sources DEFERRED and catalogued | ADR-043 |
| CP storage and accounting | one `CivicsPointsRow` per polity, integer milli-CP (`long`) balance, cumulative generated and spent; identity `balance = generated − spent` asserted every step; CP is not a conserved good (no Ledger flow) | ADR-043 |
| CP spending | one Civics target per polity (its own lane; order kind 6 selects the lane by node tree); spending takes `min(balance, remaining)`; unspent CP stays banked; RP overflow semantics unchanged (lost) | ADR-043 |
| RP↔CP exchange | not implemented (only on the government-change turn, which is M8) | ADR-043 |
| Deposit denominator | eligible = stored `DepositRow`s with abundance > 0 whose good is geological (stone, clay, copper-ore, tin-ore); share measured over those rows; deposits are per-settlement hinterland rows, which is how the model distributes them across the map | ADR-044 |
| Concealment | derived per settlement from the world seed (stateless hash, no RNG stream consumed), share 0.25; never changes deposit placement or quantity | ADR-044 |
| Prospect | order kind 15 on an own settlement, once per polity and settlement; cost sunk through the Ledger; reveals concealed deposits there (possibly none); a technology halves the cost | ADR-044 |
| Explore | order kind 14 from an own settlement: widens its explored radius; food cost sunk through the Ledger; no unit moves (M7 replaces it with mobile units) | ADR-044 |
| Fog | per polity UNKNOWN / VISIBLE / REMEMBERED; stored monotone explored extent; seed discs around controlled settlements; VISIBLE from own settlement nodes and formations; settlement sighting memory; rival formations only while visible (no ghost rows before M7 movement); AI reads only the polity seam | ADR-044 |
| Schema | one bump, v35, for the M6 slice (sections 62–66); the earlier v35/v36 reservations (S2a / S3) are superseded | ADR-043, ADR-044 |
| Tree tranche | the non-breaking part of the package's proposal is implemented; key retirements, merges and consumer-removing splits wait for the remap seam (KR-2) | ADR-045 |

## 3. What is superseded (SUPERSEDED; the texts stay as written)

| Earlier provision | Where | Superseded by | Extent |
|---|---|---|---|
| D-I: one shared RP pool and one target; no separate Civics Points | `docs/m6-plan/m6-decision-register.md:230-232` | R4 | whole |
| D-044 R2 / R12 single research currency; R20-D "do not create a general research reserve" | `docs/d044-research-progression-rulings.md:82-103`, `:540`, `:558-564`, `:795`, `:816-818` | R4 | for Civics Points only; RP keeps R20-D |
| Ledger §26 items 2–4 (one currency; no general RP reserve) | `docs/design/director-architecture-and-decision-ledger.md:1109-1121` (§26 items 2–4) | R4 | for CP only |
| D-046 G5 "exactly one shared RP pool" | `docs/d046-research-foundation-gate-rulings.md:202-204` | R4 | whole |
| ADR-030 §3 (one per-turn pool) | `docs/adr/adr-030-per-turn-research-points.md:37` | R4 | RP unchanged; CP is a second pool, per sim-year |
| CR-023 (RAISED) and D-061 (PROPOSED) | `docs/adr/cr-023-…md`, `docs/d061-civics-points-supersession.md` | R4 | resolved: D-061.1 ratified; D-061.2–.7 decided as in §2 |
| ADR-029 §8: the stage predicate contains `legal_code_roman` ("It needs a Civic") | `docs/adr/adr-029-research-engine.md:341-367` | R5 | the civic leaves the stage; the university keeps it |
| D-046 F§1 (Group B stage widening) | `docs/d046-research-foundation-gate-rulings.md:293-298` | R5 | stage expression only |
| D-046 "Technology costs must NOT be changed to force a target Age duration" | `docs/d046-research-foundation-gate-rulings.md:50` | R13 | costs may be adjusted to support pacing, each change with a recorded reason and a measurement |
| D-040 B2 "Do not invent a second exploration system"; m6-spec 1.13 | `docs/d040-discovery-and-control.md:41-49`; `docs/m6-spec.md:46` | R1 | a minimal Explore order exists in M6; M7's mobile units replace it, not add to it |
| D-039 B1 (reconnaissance bought with food **and people**), B3 (pointed) | `docs/d039-command-fog-and-siege.md:50-55`, `:61-64` | R1, R11 | Explore costs food only (no people leave); it is pointed at a settlement's surroundings |
| D-052 §5 (scouts, full LIVE/GHOST display in M6) | `docs/d052-m5-integration-audit-rulings.md:53-58` | R1, R2 | fog is minimal; the scout unit and ghost rows move with movement to M7 |
| D-055 / m6-spec 1.14 schema reservations (v35 S2a, v36 S3, "first to land") | `docs/m6-spec.md:47` | R11 | v35 is the M6 slice |
| D-051 §2 availability classes "dependent on stored known extent" (scope only) | `docs/d051-m5-polish-rulings.md:132-142` | R6, R7 | concealment is the first availability class; richness classes stay as scheduled |
| Package recommendation "unit-led sight only, no settlement-based seed" (fog proposal X2/X4 discussion) | `docs/m6-plan/knowledge-redesign/fog-of-war-proposal.md` §6.7 | R1, R11 | the seed around controlled settlements is the founding knowledge; VISIBLE stays unit- and own-settlement-based |

**Not superseded (confirmed):** movement and Action Points stay M7 (D-052 §4, D-F); adoption and active political
effects stay M8 (D-D); order kind 10 stays reserved for M7; the frozen Spine's nuclear layer is untouched — R10 adds
knowledge nodes only, so no Contradiction Report is needed (`civ-sim-architecture-v3-outline.md:137, :154`).

## 4. Records receiving an appended pointer note (2026-10-22)

`docs/d044-research-progression-rulings.md`, `docs/d046-research-foundation-gate-rulings.md`,
`docs/design/director-architecture-and-decision-ledger.md`, `docs/adr/adr-029-research-engine.md`,
`docs/adr/adr-030-per-turn-research-points.md`, `docs/d039-command-fog-and-siege.md`, `docs/d040-discovery-and-control.md`,
`docs/d051-m5-polish-rulings.md`, `docs/d052-m5-integration-audit-rulings.md`, `docs/adr/cr-023-…md`,
`docs/d061-civics-points-supersession.md` (§9 ruling line), `docs/m6-plan/m6-decision-register.md`,
`docs/m6-plan/m6-scope-register.md`, `docs/m6-plan/m6-task-register.md`, `docs/m6-spec.md`, `docs/milestones.md`,
`docs/m6-plan/knowledge-redesign/director-decision-docket.md` (§8 ruling block).

## 5. Status of the M6 specification

The Director's instruction authorizes implementation of the slice in §1–§2 now (R12). `docs/m6-spec.md` receives a
dated amendment (its §1, §2, §4 and §7 as affected by this record). The repository's process ratifies a milestone spec
by "a dated Director ruling on this spec" (m6-spec §6); the Director's instruction rules the decisions above and
authorizes their implementation but does not ratify the remaining spec text, so the spec is recorded as **DRAFT,
AMENDED BY D-062; the D-062 slice AUTHORIZED FOR IMPLEMENTATION** — not "RATIFIED" as a whole.

`CLAUDE.md`'s "Current milestone" line still reads "no active packets until `docs/m6-spec.md` is ratified". The agent's
edit of that line was refused by the session's safety checks and reverted; the line is the Director's to change (by hand
or by granting the agent permission). Until then, this record is the authority for the D-062 slice.


---
**Dated note, 2026-10-23 — the Director's M6 review instruction (recorded, not a new ruling number).** Item 4: *"The game
UI should show the educational text naturally, without citations, source badges, verification labels or bibliography UI.
Keep provenance and evidence status in the repository for internal quality control. Do not block the gameplay changes on
network access or complete source verification of all 561 descriptions. Do not falsely mark the descriptions as fully
fact-checked."* This displaces, for the in-game display, the package's PROPOSED shipping gate (docket item B-7; validator
C10): the gate now measures internal QC readiness, not whether a text may appear. Items 1–3 (the AI cheapest-route fix, the
multi-good Prospect bill, Explore's units in km) are implemented under ADR-046. Text above unchanged.
