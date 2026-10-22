# Director decision docket — M6 knowledge redesign (2026-10-22)

**Status: DECISION-READY. Nothing here is ruled.** Recommendations are the agent's. The Director rules by a dated line
in the named record (this docket's §5 for package-level items; `docs/d061-civics-points-supersession.md` §9 for
D-061; the M6 decision register for D-K, D-T, D-J, D-H, D-R; a dated note beside the record being amended). Older
rulings are never edited — they are superseded by dated records. This docket does not ratify anything, and neither
does the assignment that produced it.

Baseline: branch `claude/new-session-yavghm` @ `edba2b5`; `main` = `m5-exit` = `6e51a3e`. Evidence for every item is in
the package files named in the "Affected" lines.

## 1. Summary — what blocks what

| ID | Question (short) | Recommendation | Blocks | Latest point |
|---|---|---|---|---|
| KD-01 | Adopt the proposed technology and civics graphs as M6's content design basis? | Adopt in principle; land through KR-3/4/6 with per-change re-validation | KR-3, KR-4, KR-6, KR-7, KR-12, KR-13 | before KR-3 |
| KD-02 | D-061.1 Civics Points, and land the 60 new civics only with CP? | Ratify D-061.1; new civics only with CP | KR-5, KR-6 | before KR-5 |
| KD-03 | Keep `legal_code_roman` as a research-stage atom? | Keep (Option A) with tolerances P1–P6; Option B pre-registered remedy | KR-5 acceptance | before KR-5 |
| KD-04 | Craft-in-trunk principle and the trunk relocations (incl. `heavy_plough`, `printing_press`)? | Approve | KR-3 | before KR-3 |
| KD-05 | Fix the AI research goal closure (cheapest route; add stage atoms)? | Approve (engine fix in KR-1) | KR-1 | before KR-1 |
| KD-06 | `research_count` Age facts (≥200, ≥280 technologies) with a larger tree? | Re-ratify as counts now; revisit after KR-3 measurement | KR-3 (A8/A9 facts) | before KR-3 |
| KD-07 | Put research content identity into session resume? | Approve | KR-1 | before KR-1 |
| KD-08 | Key-remap seam; do old saves receive split-off parts? | Approve the seam; yes, split parts are granted | KR-2, KR-4 | before KR-2 |
| KD-09 | Approve content re-scores of ratified nodes (with recorded reasons)? | Approve the listed re-scores | KR-3 | before KR-3 |
| KD-10 | How to close the RP-vs-workload gap (not by costs)? | Separate decision; measure with the AI empire as reference; rule RP inputs (D-S) with D-061.3 | none of KR-1…KR-4 (pacing report only) | before KR-16 |
| KD-11 | Overflow: keep LOST (recorded freeze) or carry (D-R)? | Rule explicitly; recommend carry capped at one node | KR-1 | before KR-1 |
| KD-12 | Fog model (D-K): geometric sight, memory as state incl. formation ghosts? | Approve | KR-8 | before KR-8 |
| KD-13 | D-T and DW-002 with the two companion founding rules? | Approve | KR-9 | before KR-9 |
| KD-14 | DW-006 trade under fog for a polity's own settlements? | Measure first; D-040 B1 literal by default | KR-9 | before KR-9 |
| KD-15 | Revolt-born polity inherits extent and memory (amends D-048 r1)? | Yes, by dated ruling | KR-8 | before KR-8 |
| KD-16 | Scouts (D-052 §5 vs D-J): M6 contract, M7 unit? | Yes, dated note on D-052 §5 | KR-8 | before KR-8 |
| KD-17 | ADR-037 dated notes (PreFog attribution switch; scoped place card)? | Approve | KR-8, KR-9 | before KR-8 |
| KD-18 | Record emergent colonization (and path targets) as named D-040 B1 deviations? | Record; measure | KR-9 | KR-9 |
| KD-19 | Resource discovery scope in M6 (worldgen-derived truth, copper/tin only, surface ores ungated)? | Approve | KR-11 | before KR-11 |
| KD-20 | Nuclear scope under the frozen Spine; keep or retire weapons nodes; optional cuts? | Keep as knowledge (deterrence M10+); no civil-nuclear mechanics without a CR; take the optional cuts | KR-3/KR-4 (D11 nodes) | before KR-3 |
| KD-21 | How regime/policy grants are represented (civics may unlock only institutions)? | Capability strings in M6; representation with T6.5/D-V | KR-6 | before KR-6 |
| KD-22 | `coined_wage` is de facto mandatory for A5 entry: add a non-civic A5 supporting fact? | Add one | KR-6 (Ages content) | before KR-6 |
| KD-23 | Do "laws-lite"/authority civics take effect in M6? | Only `provincial_administration`/`tax_farming` if the R4 review agrees; else M8 | KR-6 | before KR-6 |
| KD-24 | Energy/grid capability ownership | Knowledge-only until the M10+ industrial (energy) expansion | none (documentation) | KR-3 |
| KD-25 | Educational corpus: storage, fact-check gate, network access for verification | Separate text file; ship only checked entries; allow source hosts | KR-7 | before KR-7 |
| KD-26 | "Age of Learning" surge text promises an effect that does not exist | Remove the promise until a mechanism exists | KR-3 (ages text) | KR-3 |
| KD-27 | Realize selected deferred entities in M6 through existing equations (sanitation, irrigation, mills)? | Yes for the ones with a named equation, each by dated ruling | KR-12 | before KR-12 |
| KD-28 | Pre-fog sessions on resume; schema slot for fog (D-H) | Resume with notice; next landed version | KR-8 | before KR-8 |
| KD-29 | Who rules entity-requirement changes for non-civic entities? | Same instrument as KD-01 (content ratification) | KR-3 | before KR-3 |

## 2. Decisions in full

### KD-01 — Adopt the proposed graphs as M6's content design basis
- **Question.** Are `technology-tree-proposal.json` and `civics-tree-proposal.json` (as validated by `tools/validate.py`)
  adopted as the design basis for M6 content packets, with each packet re-validating the changes it ships?
- **Recommendation.** Adopt in principle; allow the Director to strike individual lanes or nodes (each record carries its
  lane, disposition, rationale and compatibility notes).
- **Alternatives.** (a) Adopt lane by lane; (b) adopt only additions (no rewires/re-scores) — loses the causality fixes
  (e.g., the research stage's dead OR, iron's furnace prerequisite) and keeps the measured couplings K1–K5.
- **Consequences.** Content packets KR-3/KR-4/KR-6 can be cut; CI `--check` documents, `ResearchContentTests` pins and
  AI goldens move in those packets (attributed).
- **Affected.** `Sim.Data/content/research.json`, `ages.json` (if KD-06/KD-22), tests named in `audit/engine-audit.md`
  Q13; milestones M6 (content), M7–M10+ (owners of deferred realizations).
- **Dependencies.** KD-04, KD-08, KD-09, KD-20, KD-29. **Latest point:** before KR-3. **BLOCKING** for content packets.

### KD-02 — Civics Points and where the new civics land
- **Question.** Ratify D-061.1 (separate Civics Points pool, `docs/d061-civics-points-supersession.md`) and rule that
  the 60 new civics land only with CP?
- **Recommendation.** Ratify; land new civics only with CP (D12 OD-03): under one pool they add 14–26 % to the A3–A7
  research workload and enter the AI's cheapest-anywhere fallback (`civics-tree-proposal.md` §7).
- **Alternatives.** One pool (the 2026-10-20 D-I ruling stands): keep the 7 civics only, defer the 60.
- **Affected.** `ResearchSystem`, `ResearchQuery`, `AiResearchPolicy`, schema (D-H), the one-pool test, civics UI.
- **Latest point.** Before KR-5. **BLOCKING** for KR-5/KR-6.

### KD-03 — The research-stage civic atom (D-061.8, D12 OD-01)
- **Question.** Keep `legal_code_roman` in `researchStage.requires`?
- **Recommendation.** Keep (Option A) with the PROPOSED tolerances P1–P6 measured against the one-pool twin on seeds 1,
  2, 42, 7 (`civics-tree-proposal.md` §6); pre-register Option B (move the civic requirement to the realized university)
  as the remedy if any tolerance fails.
- **Evidence.** MEASURED one-pool baseline: the civic never bound the stage (66–67 turns of slack; `geometry_axiomatic`
  completes last). ESTIMATE: a CP rate below ≈15 % of RP would delay the stage by 79–249 turns on seed 42
  (`research-balance-analysis.md` §6).
- **Alternatives.** Option B now (rewrites the stage expression, D-044 R4 derivation); Option C (OR route) rejected.
- **Latest point.** Before KR-5. **BLOCKING** for KR-5 acceptance.

### KD-04 — Craft-in-trunk principle and relocations
- **Question.** Adopt the rule "a node belongs in a subtree only if it causally needs organized scholarship (the research
  stage) or another subtree node; practitioner crafts stay in the trunk at any Age" (D-044 R15), and approve the lanes'
  relocations (e.g., `heavy_plough`, `printing_press`, `type_founding`, both windmills, medieval textile and mill crafts,
  `polynesian_canoe`, `double_entry`, `bill_of_exchange`, `paper_money`)?
- **Recommendation.** Approve. The A6 CORE closure falls from 40 nodes / 82,410 RP (incl. the stage) to 19 / 27,570 RP;
  the A7 CORE fact loses its hidden stage and civic gate; the AI's Age goal no longer stalls on a closed subtree.
- **Consequence.** The "every A7–A9 node is in a subtree" convention (validator V17) becomes a warning with named
  exceptions. Partial progress on relocated nodes stays valid (same key).
- **Latest point.** Before KR-3. **BLOCKING** for KR-3.

### KD-05 — AI research goal closure
- **Question.** Change `AiResearchPolicy` to use the cheapest satisfying route (not the union of every OR branch) and to
  add the research-stage atoms when a goal node sits in a closed subtree?
- **Recommendation.** Approve (engine audit Q11 gap; D03: the university goal inflates to 60,440 RP).
- **Consequence.** AI-empire goldens move (attributed by a switch-off twin).
- **Latest point.** Before KR-1. **BLOCKING** for KR-1.

### KD-06 — `research_count` Age facts
- **Question.** The A8/A9 supporting facts count completed technologies (≥200, ≥280). The proposal grows Technology from
  426 to the count in `technology-tree-proposal.md`; the thresholds become a smaller share. Keep the counts, or restate as
  shares (47 % and 66 % of finite technologies today)?
- **Recommendation.** Keep the counts for KR-3 (no Ages-content change), then re-rule with the KR-3 measurement.
- **Latest point.** Before KR-3 (non-blocking if kept).

### KD-07 — Content identity on resume
- **Question.** Add the research content SHA-256 to the session manifest and the resume check (today content is not
  part of identity; a content change without a schema bump makes diverging v34 sessions unresumable — K11)?
- **Recommendation.** Approve; refuse with a clear message on mismatch, or resume with notice after a schema bump.
- **Latest point.** Before KR-1. **BLOCKING** for KR-1 and every later content packet.

### KD-08 — Key-remap seam and split parts
- **Question.** Add a content-declared remap applied at load (retired → successor keys), and rule what a save that
  completed a split node receives?
- **Recommendation.** Approve; a completed split node completes all its parts; a merged key maps to its survivor;
  progress, Eureka and credit rows of retired keys stay dormant as today.
- **Alternatives.** No seam: old saves silently lose knowledge (MEASURED load behaviour, `audit/engine-audit.md` §7.3).
- **Latest point.** Before KR-2. **BLOCKING** for KR-2/KR-4.

### KD-09 — Re-scoring ratified content
- **Question.** Several lanes re-scored factors where the original scoring was inconsistent (e.g., A2 calendar nodes with
  institutional 3.0; cereal nodes with material 2.0; medical-imaging nodes with material 0). Per-node factors are
  ratified content (digest X-08). Approve these re-scores, each with its recorded reason?
- **Recommendation.** Approve. None is a pacing change (D-046 §1): each moves one factor for a content reason; the
  package reports old and new costs per node.
- **Latest point.** Before KR-3. **BLOCKING** for the re-scored nodes only.

### KD-10 — The pacing gap (a separate decision; costs are not the lever)
- **Question.** The measured driver researches ≈ 6 % of the workload in 1,630 turns (234–238 nodes; the stage at turn
  996–1016); the AI empire, with 3.5–10.3M people, completes 263–344 nodes and reaches the stage at turn 702–716. How
  should the knowledge economy be paced?
- **Recommendation.** Treat the AI empire's trajectory as the reference player profile; rule RP inputs beyond population
  (D-S) together with D-061.3; keep costs content-derived (D-046 §1). Do not change the RP formula in M6 content work.
- **Alternatives.** Demography calibration; the Age→dt mapping; accept partial trees as a design stance (a game
  researches a fraction; Ages need only their facts).
- **Latest point.** Before KR-16 (exit). Non-blocking for content packets.

### KD-11 — Overflow (D-R) against a recorded freeze
- **Question.** Overflow is LOST under a recorded Director freeze (D-047 Part B §2.2; digest X-02); the M6.1 docket
  recommended "carry" without citing that freeze. Rule explicitly.
- **Recommendation.** Carry, capped at one node ahead, if the Director reverses the freeze: finer granularity raises the
  loss (6.5–6.7 % of all RP today, MEASURED) and makes research speed depend on node size. Otherwise keep LOST.
- **Latest point.** Before KR-1. **BLOCKING** for KR-1.

### KD-12 … KD-18, KD-28 — Fog of war
Full questions, alternatives and latest points: `fog-of-war-proposal.md` §17 (items 1–18) with the principal
designer's resolutions in §19. Summary recommendations: D-K ruled with geometric integer-disc sight (population band +
vantage, sea included, no lanes), Known = stored ∪ LIVE, sighting memory as state including formation ghost rows
(D-039 C1 via D-052 §5); D-T plus the two companion founding rules (a site's spacing neighbourhood must be known; a
refusal records the obstruction) and DW-002; DW-006 measured before ruling; revolt-born polities inherit extent and
memory by a ruling beside D-048 r1; scouts = M6 mobile-producer contract, M7 unit; ADR-037 notes for the PreFog switch;
emergent colonization recorded as a D-040 B1 deviation; pre-fog sessions resume with notice; schema by D-H.
**BLOCKING** for KR-8/KR-9.

### KD-19 — Resource discovery scope in M6
- **Question.** Adopt: place-level deposit truth derived from worldgen (ADR-008 path, not stored) with D-051 §2(b)
  regional richness of copper and tin as its first M6 consumer; identification via `ore_prospecting`; surface ores stay
  ungated; new minerals (coal, iron ore, petroleum, uranium) deferred to the expansion that ships their first consumer?
- **Recommendation.** Adopt. Research never places, reveals or proves a deposit.
- **Latest point.** Before KR-11. **BLOCKING** for KR-11.

### KD-20 — Nuclear scope under the frozen Spine
- **Question.** The frozen Spine limits the nuclear layer to "deterrence-abstraction only" in the modern-era expansion
  (`docs/civ-sim-architecture-v3-outline.md:137, 154`). The proposal keeps a 27-node geology/nuclear case study as
  knowledge. Rule: (a) keep `fission_weapon`/`thermonuclear` as knowledge realized only by the deterrence abstraction
  (M10+), or retire them; (b) civil-nuclear nodes knowledge-only, any civil mechanic needing a CR against the Spine;
  (c) take the lane's optional cuts (≈426k RP of late nodes).
- **Recommendation.** (a) keep; (b) yes; (c) take the cuts unless the Director wants the full fuel-cycle detail.
- **Latest point.** Before KR-3/KR-4 for the D11 nodes.

### KD-21 … KD-23 — Civics representation and Ages content
- **KD-21** (D12 OD-04; digest X-07): civics may unlock only institutions in the loader; recommend capability strings
  in M6 and the regime/policy representation decided with T6.5/D-V. **BLOCKING** for KR-6.
- **KD-22** (D12 OD-02): `a5_wages` makes `coined_wage` de facto mandatory for A5 (military facts pend until M7, so the
  only institutional supporting fact is the civic); recommend adding a non-civic A5 supporting fact. Before KR-6.
- **KD-23** (D12 OD-07): R4 assigns "laws-lite" and the authority economy to the M6 civic spec without definitions;
  recommend only `provincial_administration` and `tax_farming` as M6 candidates, subject to the R4 review. Before KR-6.

### KD-24 — Energy and grid ownership
Energy, grid and fuel capabilities have no owning system before the M10+ industrial (energy) era expansion (Spine
`:137`). Recommend: knowledge-only with named deferred effects. Non-blocking.

### KD-25 — Educational corpus adoption
- **Question.** Where the node texts live, what verification gate they pass, and whether the environment may reach
  source hosts for full-text checking (this session could only read search summaries; page fetches were blocked by the
  network policy).
- **Recommendation.** A separate text file keyed by node id (text edits then never touch `research.json`'s SHA and the
  CI audits); ship only entries at least `search-corroborated`, with an independent check of a stated sample; allow the
  source hosts so entries can be raised to `source-checked`. **BLOCKING** for KR-7.

### KD-26 — The "Age of Learning" surge
The surge promises "temporary acceleration of research throughput" but no code reads it (current-tree audit K9).
Recommend removing the promise from the text until a mechanism is designed (generic Age modifiers are prohibited,
ledger §26 item 23). Non-blocking.

### KD-27 — Realizing selected deferred entities in M6 (T6.11)
Lanes name existing equations that could realize some deferred entities (sanitation through the per-settlement
mortality multiplier; irrigation through farming output; textile and fulling mills through the weaving recipe with a
capacity term). Recommend: yes for each with a named equation, by dated ruling per entity; otherwise wait for M9.
Before KR-12.

### KD-29 — Entity-requirement changes for non-civic entities
Lanes re-point some entity requirements (e.g., `infra.power_grid` to `ac_transmission`; sewerage to
`sanitary_engineering`). Recommend ruling them with KD-01 (they are research content). Before KR-3.

## 3. Sub-dockets (detail lives with the design)

- Fog: `fog-of-war-proposal.md` §17 (18 items) and §19 (resolutions).
- Civics: `civics-tree-proposal.md` §16 (OD-01…OD-11).
- Cross-lane adjudication and lane open questions: `research/integration-adjudication.md`.
- Measured couplings: `current-tree-audit.md` §5 (K1–K11).

## 4. Record corrections surfaced (dated corrections; nothing edited)

| # | Record | Correction | Evidence |
|---|---|---|---|
| RC-1 | `docs/m6-plan/m6-ruling-docket.md` (D-061.4) and D-061 dated note | the append-only premise is at `CanonicalSchema.cs:197-204`, not `:14-16` | digest X-16 |
| RC-2 | M6.1 records on D-R | overflow LOST is a recorded Director freeze (D-047 Part B §2.2); the "carry" recommendation did not cite it | digest X-02 |
| RC-3 | `docs/research-calibration-report.md` §5 | measured section stale for this build (seed-42 turn-1 population 5,118 vs 5,245; cumulative 709,153 vs 696,745) | `measurements/baseline-progression.md` §0 item 8 |
| RC-4 | `docs/m6-spec.md` §3.5 F | extent "seeded from founding catchments" — no catchment exists at turn 0 | fog audit §12.4 |
| RC-5 | `docs/m6-spec.md` §3.2/§3.5 F; docket D-K | `Isochrone` sight contradicts the m5w applied correction and D-039 B4 | fog proposal §18 R16 |
| RC-6 | `research.json:11` `_doc` | labour activities are not presentation-only: farming eligibility changes the yield | engine audit Q10 |
| RC-7 | `docs/research-gameplay-unlock-audit.md` §1 | roads are enforced through `BestKnownClass`, not `IsClassKnown` | engine audit Q10 |
| RC-8 | `nuclear_fission` capability text | cites D-011 for deterrence; the governing record is the frozen Spine (`:154`) | D11 lane notes |

## 5. Ruling block (append-only; the Director writes here)

> _[empty — awaiting the Director. Suggested form: "2026-10-DD — KD-01 ADOPTED; KD-04 APPROVED; KD-11 …". Until a line
> is written here, nothing in this package is in force.]_
