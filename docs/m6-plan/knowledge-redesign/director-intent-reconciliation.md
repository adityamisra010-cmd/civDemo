# Director-intent reconciliation — M6 knowledge redesign (2026-10-22)

**Status: PROPOSED. Nothing here is ruled.** This report maps the Director's latest intent (the 2026-10-22 refinement
brief) onto the package, records every conflict with frozen or ratified governance and its resolution path, and ranks
the corrections. The intent is **input**; no item below is represented as already ruled. Where the intent and a
frozen record disagree, the record is unchanged and a formal path is named.

Status words: **aligned** (the package now says what the intent says) · **partially aligned** (agrees, with an open
point named) · **contradicted** (the package says otherwise) · **not yet specified**. The "before" column is the
package at `92fe440`; "now" is this revision.

## 0. Freshness (MEASURED 2026-10-22, before editing)

| Item | Value |
|---|---|
| Branch / HEAD before this revision | `claude/new-session-yavghm` @ `92fe4408be555bf5acd2a31c6ca96815c5baade9`, clean, tracking `github/claude/new-session-yavghm` (same SHA) |
| Remote `main`, tag `m5-exit` | both `6e51a3e26c6b10e179335db3042898f1251099de` |
| Ahead of `main` | 6 commits: `8702afc`, `a0b3b25` (M5 closure docs), `4190a01`, `edba2b5` (M6.1 docs), `ce6bcfd`, `92fe440` (this package) |
| Diff outside the package (`main...HEAD`) | 21 files under `docs/` from the four earlier docs commits (M6 spec, D-061, CR-023, M6 planning registers, ADR notes); preserved, not edited, except one append to `docs/d061-civics-points-supersession.md` |
| CI on `92fe440` | CI run 39779294879 success; ui-artifact run 39779294721 success |
| Baseline vs head | `edba2b5` is the commit this work started from (the M6.1 docket); `ce6bcfd` the package checkpoint; `92fe440` the first published package; this revision is the next docs-only commit. The old README called `edba2b5` "the branch baseline" without naming the head — corrected |

## 1. Intent → package mapping

### A. Separate knowledge and civics economies

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| A.1 | RP and CP are separate accumulated pools for distinct trees | partially aligned (D-061.1 proposed; "accumulated" not reconciled with the no-reserve rule) | **partially aligned** — separate pools proposed; "accumulated" read as generation + per-node progress unless the Director rules a bankable balance (conflict G-1) | `cp-economy-proposal.md` §2.6, §5; docket A-1 |
| A.2 | CP generated in part from population, larger population → more CP | not yet specified | **aligned** — population term `c_P·P_ref·(P_free/P_ref)^β_P` per sim-year (placeholders PROPOSED) | `cp-economy-proposal.md` §2.2 |
| A.3 | Other source categories (social-sciences universities, colleges, embassies, spies, artists, literary scholars, others), each classified | not yet specified | **aligned** — catalogue S1–S12: none EXISTING as a CP input; population, reach, colleges, courts, printing, institution maturity PARTLY REPRESENTED; social-sciences universities, temples, bureaucrats, scholars, artists, embassies, spies PROPOSED, with owner, milestone, dependencies and double-counting rule | `cp-economy-proposal.md` §1 |
| A.4 | Not a fixed fraction or split of RP; independent inputs; no final yields | **contradicted** — tolerance P5 "CP ≥ 0.30 × RP" and the static model's `r × RP` stand-in | **aligned** — P5 restated in CP terms, the ratio stand-in marked SUPERSEDED, equations with independent inputs, every coefficient a PROPOSED placeholder | `civics-tree-proposal.md` §6; `cp-economy-proposal.md` §2.5; `research-balance-analysis.md` §6 |
| A.5 | RP↔CP exchange only in the government-change turn; economics OPEN; no implementation | not yet specified | **aligned** — structural limit, open list (direction, ratio, limits, order, eligibility), order within the turn, determinism; IMPLEMENTATION DEFERRED to M8; needs a dated ruling (conflict G-2) | `cp-economy-proposal.md` §3; D-061 intent note |
| A.6 | Happiness penalty only an example; do not develop | — | **aligned** — named once as excluded; nothing designed | `cp-economy-proposal.md` §3; D-061 intent note; roadmap KR-5 |
| A.7 | Supersede single-pool rulings only by the append-only process; intent added to D-061 as not ratified | partially aligned | **aligned** — a non-ratified "Director intent note — 2026-10-22" appended to `docs/d061-civics-points-supersession.md` (25 lines added, 0 removed; §9 empty) | D-061 (end); conflict G-9 |

### B. Staged ratification

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| B.1 | Architecture first; mechanics and content later gates; architecture ≠ approval of nodes, costs, yields, forms, AI, UI, text | partially aligned (one flat 29-item docket) | **aligned** — docket regrouped: A (6 architecture items) · B (per packet) · C (later choices); the disclaimer is stated in the docket, roadmap and README | `director-decision-docket.md` §1–§4; `implementation-roadmap.md` header |
| B.2 | Old rulings intact; suggestions labelled | aligned | **aligned** — no ratified text edited; KD numbers kept; superseded package wording listed (docket §4) | docket §4 |

### C. Fog of war

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| C.1 | Three per-empire states UNKNOWN / VISIBLE / REMEMBERED | partially aligned (UNKNOWN / REMEMBERED (GHOST) / LIVE per node) | **aligned** | fog §0 C1, §1.2, §5.1 |
| C.2 | VISIBLE = inside a valid unit's sight; current terrain, resources, foreign and neutral units | **contradicted** — sight from controlled settlements | **aligned** — units are the only default producers | fog §6.1 |
| C.3 | REMEMBERED shows last observation under a dark overlay; never refreshed from hidden state | partially aligned | **aligned** | fog §4.2, §13 |
| C.4 | Leaving units: area stays remembered, foreign/neutral positions vanish; ratified formation ghosts respected; no neutral memory | **contradicted** — first draft had formations LIVE-only (against D-039 C1 / D-052 §5) | **aligned** — enemy formations leave a dated GHOST marker (D-039 C1–C3), distinct, never moved, never current; neutral units leave nothing | fog §5.3, §6.5 |
| C.5 | Unit-led exploration; no silent passive settlement vision | **contradicted** — passive population/elevation settlement sight was the default (radius ≥ 12 nodes) | **aligned** — withdrawn as default, retained as option X4 (Director-only exception with a sunset) | fog §0 C3, §6.9 |
| C.6 | M6/M7 audit: formations static until M7, scout is M7's; show the consequence; offer clean options | not yet specified | **aligned** — six options X1–X6, recommendation X2, stated cost: M6 has almost no exploration and ordered colonization nearly stops until M7 | fog §6.7 |
| C.7 | LOS, elevation, sea, radius by type, passive sources as explicit options from code and precedent | partially aligned (chosen, partly fitted to founding outcomes) | **aligned** — none exists in code (MEASURED); G1–G7 options with Civ VI / Age of Empires precedent (search-corroborated); constants never fitted to sim outputs | fog §6.2, §6.3 |
| C.8 | Independent per-empire maps; no implicit sharing by allies, embassies, spies | not yet specified | **aligned** | fog §10.1, §17 item 14 |

### D. Observation, knowledge, capability, infrastructure

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| D.1 | Four distinct layers | partially aligned (one seven-step chain) | **aligned** — re-split by the source of each fact | fog §12.1; `capability-dependency-matrix.md` §1 |
| D.2 | Seeing a visible surface resource needs no research; concealed deposits by prospecting; research never creates or reveals deposits or updates the remembered map | **contradicted** in the fog proposal (identification gate on visible surface signatures) | **partially aligned** — the fog proposal withdrew the gate; **six technology grants still read `identification:deposit.<x> (exposed …)`** (`copper_smelting`, `ore_prospecting` ×2, `concrete_pozzolan`, `saltpetre_refining`, `chemistry_quantitative`). They are consistent with the intent only as *recognition knowledge* that relabels a polity's own observations and never touches unobserved land — which is fog §17 item 9's alternative, not its recommendation. Left unchanged pending A-5 (correction R-7 below) | fog §12.1, §17 item 9; docket A-5 |
| D.3 | Research may teach recognition or processing but never secretly updates the remembered map | aligned in principle | **aligned**; validator-level guarantee is a KR-11 metamorphic test | roadmap KR-11 |

### E. Strict AI information parity

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| E.1 | AI plans and values through the same per-polity observation boundary; authoritative systems may read truth | aligned | **aligned** — 53 read sites mapped to `PolityKnowledge` | fog §7.3, §10.2 |
| E.2 | No exploitation of unseen terrain, resources, units or changes | partially aligned | **aligned** — metamorphic leak invariants added for unseen enemy movement and unseen resource/world change | fog §11.1 |
| E.3 | Difficulty grants no hidden information by default | not yet specified | **aligned** — any reveal mode needs a CR against ADR-037 §1 | fog §10.1, §17 item 13 |

### F. Civic identities and structure

| # | Intent | Before | Now | Where |
|---|---|---|---|---|
| F.1 | 426 + 7 current; 494 + 67 proposed (7 existing, 42 micro, 18 macro) | aligned | **aligned** (MEASURED again: 561 active, 2 retired) | `validation-report.md` |
| F.2 | Preserve 1001–1007 as anchors; do not claim unchanged behaviour without checking every gate | **contradicted** — "kept 7, changed 0" | **aligned** — legacy audit table (old/proposed prerequisites, consumers, re-points, risk, tests, recommendation); "changed 0" true of record fields only | `civics-tree-proposal.md` §4a |
| F.3 | Audit `taxation` (tax edict + Age III), `legal_code_roman` (stage + university + A5), institution requirements | not yet specified | **aligned** — taxation's Age III half lives in `sim.json` (`Governance.CanLevyTax`), not in the civic; legal_code_roman gates the stage, `inst.university` and A5 CORE; 7 entity re-points listed | §4a; validator V26 |
| F.4 | New keys from 1008; never renumber | aligned | **aligned** — validator V23 | `validation-report.md` |
| F.5 | Micro vs macro as a reviewable option; macros as composable modules with a derived label | aligned | **aligned** — module sets with a derived `governmentLabel` never read by a mechanism (Spine `:85`; D-042) | §4a notes |
| F.6 | M6 eligibility only; adoption and effects M8 (D-D) | aligned | **aligned**; no form is an Age fact or Age-granted (validator V25) | §4a notes; docket A-2 |

## 2. The ten known issues

| # | Issue | Finding | Correction made |
|---|---|---|---|
| 1 | Fog recommended passive settlement sight | contradicted the unit-led intent; also already a deviation from D-040 B2 / D-039 B1, B3 | withdrawn as default; option X4 (fog §6.9) |
| 2 | M6/M7 timing | formations stand still until M7 (D-052 §4); unit-only sight gives one disc at each capital for all of M6 | six options, X2 recommended, consequences stated (fog §6.7; docket A-3) |
| 3 | Enemy ghosts vs neutral units | D-039 C1 (via D-052 §5) requires a last-known marker; it is memory, not visibility | GHOST rows: dated, distinct, never moved, cleared by observed absence; neutral units leave nothing (fog §5.3) |
| 4 | `legal_code_roman` gates the stage across pools | MEASURED: in the stage, `inst.university` and A5 CORE | Options A/B/C quantified with a static ESTIMATE on seeds 1, 2, 42, 7; A recommended, B as pre-registered remedy; choice open (`cp-economy-proposal.md` §4; docket A-4) |
| 5 | CP record lacked the exchange limit and the generators | true | D-061 intent note appended; source catalogue and exchange slot written |
| 6 | README baseline | named `edba2b5` only | README states baseline, published head and this revision separately (§0 above) |
| 7 | Sourcing could be misread | 0 entries were page-checked; "0 ERROR" read as verification | shipping gate C10 (0 of 561 may ship), evidence-agreement check C11, generated queue; 121 entries moved off `unverified`; ≈ 90 texts corrected (`educational-content-corpus.md` §0, §2) |
| 8 | Balance numbers could be read as gameplay | proposed-tree figures came from a static model | every proposed figure labelled ESTIMATE (static model); baseline MEASURED; README and docket say so; balance outputs re-run and byte-identical |
| 9 | Nuclear chain vs the Spine | knowledge chain valid; civil runtime mechanics conflict with the deterrence-only Spine (`:137, :154`) | docket A-6: knowledge/educational chain only; civil runtime needs a CR; realization classes show DEFERRED or KNOWLEDGE-ONLY |
| 10 | Stage A "preserve IDs, possibly re-parent" vs "changed 0" | the proposal re-parents **none** of the seven; it re-points 7 entity requirements away from 3 of them and changes their pool | stated per node in §4a |

## 3. Conflicts with frozen or ratified governance

Each stays an open proposal; nothing below has been ruled or edited.

| # | Record (section) | Conflict | Resolution path | Gate |
|---|---|---|---|---|
| G-1 | D-044 R20-D; ledger §26 item 4 | "accumulated pool" vs no bank or reserve | read "pool" as generation + per-node progress (ordinary); a bankable balance needs a dated ruling | A-1 |
| G-2 | D-061 §3; ADR-030 §2–§3; ledger §26 items 5, 11 | RP↔CP exchange vs two separate currencies | dated ruling (D-061 §9 or successor) + ADR-043 scope; M8 | A-1 (slot) · M8 (economics) |
| G-3 | D-044 R5, R20-H | social-sciences universities as a CP yield vs "universities reduce cost" | civic cost factor (no ruling) or a dated note on R5's scope | B-4 |
| G-4 | CLAUDE.md law 3; D-061 §4.1 | CP per sim-year vs D-061.2's per-turn recommendation | open decision; per-turn needs ADR-043 | B-4 |
| G-6 | D-046 F§1; ADR-029 §8 | Option B changes ratified stage content | dated content ruling + dated notes | A-4 (only if B) |
| G-7 | D-018 §1–§2 | artists/scholars as classes vs closed roster | specialists (staff, notables), or a D-018 amendment | B-4 / M9 |
| G-8 | D-D; m6-spec §2, §8 | embassies and spies before diplomacy | M8 sources only | C |
| G-9 | D-044 R2/R12, D-046 G5, ledger §26 items 2–3, D-I, ADR-030 §3 | single-pool rulings | superseded only by D-061 §9 | A-1 |
| F-1 | D-052 §5; `docs/milestones.md:35`; D-039 pointer | "scouts in M6" vs movement in M7 | dated Director clarification on D-052 §5 | A-3 |
| F-2 | D-052 §5 | X1 (fog dark in M6) breaks "full fog in M6" | X1 only by CR | A-3 |
| F-3 | D-052 §2(i) | `a4_expansion` reading moves under X2 | dated note re-recording the reading | A-3 |
| F-4 | D-052 §4 | X6 (M6 scout) breaks "movement in M7" | X6 only by CR | A-3 |
| F-5 | D-040 B2; D-039 B1, B3 | formation sight and the founding seed are not bought or pointed; X4 is a larger deviation | ADR (T6.7) naming the deviation; X4 only by Director ruling | A-3 |
| F-6 | D-040 B2 | X5 is a second exploration system | X5 only by CR | A-3 |
| F-7 | D-040 A3/B1; D-051 §5 | emergent colonies settle unknown land | dated note recording the deviation | B-8 |
| F-8 | D-040 B1 | domestic trade between scattered holdings stops under X2 | measure; dated amendment for own pairs | B-8 (A under X2) |
| F-9 | D-048 r1, r4 | revolt-born polities have no unit, so no sight | dated clarification beside D-048 r1 (seed over own places) | A-3 |
| F-10 | ADR-037 §1, §7 | PreFog switch; memory as second input; place card | dated notes + fog ADR | A-3 |
| F-11 | ADR-036 | founding predicate gains knowledge arms | ADR addendum | B-8 |
| F-12 | Spine principle 7 | "Difficulty = information" | ordinary reading (no reveal); any reveal by CR | A-3 |
| S-1 | Spine `:137, :154` (FROZEN) | civil-nuclear runtime mechanics vs deterrence-only nuclear layer | CR before any civil mechanic; none proposed | A-6 |

## 4. Ranked corrections

| Rank | Correction | Blocks | State |
|---|---|---|---|
| R-1 | Unit-led fog default; X4 demoted; M6 exploration option stated | architecture (A-3) | **done** in the proposal; Director choice open |
| R-2 | Formation GHOST rows; no neutral memory | architecture (A-3) | **done** |
| R-3 | CP from independent inputs; fraction-of-RP framing removed | architecture (A-1) | **done** in canonical data and docs |
| R-4 | Exchange limited to the government-change turn; penalty not developed | architecture (A-1) | **done** (slot only) |
| R-5 | Cross-pool gate options quantified (static ESTIMATE) | architecture (A-4) | **done**; choice open |
| R-6 | Staged docket A/B/C | architecture | **done** |
| R-7 | Six `identification:… (exposed)` grants vs "no research to see a surface resource" | architecture (A-5) and KR-11 | **open**: if A-5 rules fog §17 item 9 as recommended, re-word them as recognition knowledge with no map effect in KR-3; if the alternative is ruled, they stay |
| R-8 | Legacy civic audit; "changed 0" reconciled; `taxation` and the university now count as live gates in `derived.optional` and validator V24 | KR-5 / KR-6 | **done** |
| R-9 | Shipping gate and evidence checks; sourcing queue | KR-7 | **done**; 561 entries remain blocked |
| R-10 | Realization class on every capability (EXISTING 59, EXTENSION REQUIRED 48, NEW MECHANIC REQUIRED 4, DEFERRED 127, KNOWLEDGE-ONLY 323) | KR-3 / KR-12 | **done** (validator V27) |
| R-11 | `ore_prospecting` (case-study node) is mandatory: it is `tin_bronze`'s prerequisite, so it sits in the A3 CORE closure | KR-3 | **open** — intended by lane D02/D11 (identifying tin ore precedes tin bronze); the Director confirms with B-3 or it becomes an OR |
| R-12 | Roadmap KR-5/KR-7/KR-8/KR-10 rewritten for the above; "sight radius ≥ 12 nodes" withdrawn | packets | **done** |
| R-13 | `CanonicalSchema.cs` citation in the anchor rule corrected to `:197-204` (RC-1) | none | **done** |
| R-14 | Measured calibration report stale (RC-3) | none | **open** — refresh is KR-1 work (code run), not this docs pass |

## 5. What remains MEASURED, ESTIMATED or unverified

- MEASURED: the current graph, baseline driver and AI-empire runs (seeds 1, 2, 42, 7), code consumers and gates,
  validator outputs.
- ESTIMATE (static model): every turn figure for the proposed graph and every CP-gate figure. No simulation of the
  proposed graph exists; no runtime test was run or claimed.
- Unverified: 198 corpus entries; 250 partially checked; 113 search-corroborated; 0 page-checked.
