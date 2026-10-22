# Director decision docket — M6 knowledge redesign (revised 2026-10-22)

**Status: DECISION-READY. Nothing here is ruled.** Recommendations are the agent's. The Director rules by a dated line
in the named record (this docket's §8 for package-level items; `docs/d061-civics-points-supersession.md` §9 for
D-061; the M6 decision register for D-K, D-T, D-J, D-H, D-R; a dated note beside the record being amended). Older
rulings are never edited — they are superseded by dated records. This docket does not ratify anything, and neither
does the assignment that produced it.

**Ratification is staged (Director intent, 2026-10-22).** Group **A** is the architecture gate and is ruled first.
Group **B** items are ruled before the packet that needs them. Group **C** items are later balance and content choices.
Ruling group A approves **no** node, cost, yield, government form, AI strategy, UI behaviour or educational text.

Baseline and current head: package built from `edba2b5` (branch base for this work); first published at `92fe440`;
this revision is the next docs-only commit on `claude/new-session-yavghm`. `main` = `m5-exit` = `6e51a3e`. The
item-by-item check against the Director's intent is `director-intent-reconciliation.md`.

## 1. The smallest set to rule next — group A (architecture gate, 6 items)

| ID | Question | Recommendation | Folds in | Evidence |
|---|---|---|---|---|
| **A-1** | **Two separate pools.** RP and CP are separate, for distinct trees; CP is generated from its own inputs (population-led, plus institution and specialist sources), never a fraction or split of RP; an RP↔CP exchange exists **only in the turn the empire changes government** (economics open, implementation M8); single-pool rulings are superseded only through D-061 §9. Also: is a "pool" generation + per-node progress (no bank; recommended) or a bankable balance? | Ratify D-061.1 with this intent; "pool" = generation + per-node progress; exchange as an architectural slot only | KD-02; CP G-1, G-2, G-9 | `cp-economy-proposal.md` §2–§3, §5; D-061 intent note (appended 2026-10-22) |
| **A-2** | **Scope boundaries.** Staged ratification as above; M6 defines civic **eligibility** only, adoption and active effects stay M8 (D-D); regime forms are composable modules with a derived government label, never an enum a mechanism reads; legacy civic keys 1001–1007 are anchors, new civics append from 1008; no Age transition grants or requires a government form; micro/macro split kept as a reviewable design option | Approve | KD-21 (principle) | `civics-tree-proposal.md` §4a; validator V23–V26 |
| **A-3** | **Fog information model.** Per-empire UNKNOWN / VISIBLE / REMEMBERED; units are the only default sight source (no passive settlement, city or border sight); enemy formations leave a dated GHOST marker (D-039 C1–C3), neutral units nothing; remembered land shows last-observed facts only; maps never shared; AI reads the same scoped interface; difficulty reveals nothing; and **which M6 exploration option** (X1–X6) | Approve the model; **X2** (formation sight + one-time 50 km initial extent), accepting that M6 has almost no exploration and ordered colonization nearly stops until M7's scout; X4 (settlement sight with a sunset) only as a ruled fallback | KD-12, KD-16; fog §17 items 1–15 | `fog-of-war-proposal.md` §1.2, §6.7, §17, §18.0 |
| **A-4** | **The cross-pool gate.** Keep `legal_code_roman` in the research stage (Option A, with tolerances P1–P7), remove it from the stage but keep it on the university (Option B), or add an OR route (Option C)? | A, with B pre-registered as the remedy | KD-03 | `cp-economy-proposal.md` §4 (static ESTIMATE) |
| **A-5** | **Observation ≠ knowledge ≠ capability ≠ infrastructure.** Research grants eligibility only; it never creates, places, reveals or refreshes a deposit or the remembered map; seeing a surface resource needs no research; concealed deposits need prospecting; each capability carries a realization class (EXISTING / EXTENSION REQUIRED / NEW MECHANIC REQUIRED / DEFERRED / KNOWLEDGE-ONLY) | Approve | KD-19 (principle); fog §17 item 9 | `fog-of-war-proposal.md` §12.1; `capability-dependency-matrix.md` §3a |
| **A-6** | **Nuclear boundary under the frozen Spine.** The geology/nuclear chain stays a knowledge and educational sequence; weapons nodes realize only through the M10+ deterrence abstraction; any civil-nuclear runtime mechanic requires a CR against the Spine | Approve; no CR now | KD-20 (a), (b) | `technology-tree-proposal.md` §4; Spine `:137, :154` |

Three of these are real choices (A-1's pool reading, A-3's exploration option, A-4's option); the others confirm
boundaries the Director has already stated.

## 2. Group B — rule before the named packet

| ID | Packet | Items | Recommendation (detail in §5 below) |
|---|---|---|---|
| B-1 | KR-1 engine fixes | KD-05 AI goal closure; KD-07 content identity on resume; KD-11 overflow (a recorded freeze); KD-10 report wording only | approve KD-05, KD-07; rule KD-11 explicitly |
| B-2 | KR-2 / KR-4 | KD-08 key-remap seam and split parts | approve |
| B-3 | KR-3 technology content | KD-01 graphs as design basis; KD-04 craft-in-trunk; KD-09 content re-scores; KD-29 entity re-points; KD-06 `research_count` facts; KD-26 surge text | approve; KD-06 keep counts, revisit |
| B-4 | KR-5 civics engine | CP equation shape and placeholders (D-061.3); per-sim-year vs per-turn (G-4); social-sciences source as a cost factor or a staff yield (G-3); reach weighting; tolerances P1–P7 | `cp-economy-proposal.md` §2, §6 |
| B-5 | KR-6 civics content | KD-21 representation; KD-22 non-civic A5 fact; KD-23 laws-lite; OD-05 institution re-points | as §5 |
| B-6 | KR-12 | KD-27 realizing deferred entities through named equations | yes, per entity |
| B-7 | KR-7 corpus | KD-25: only `source-checked` text ships; network access for source hosts | approve |
| B-8 | KR-8 … KR-10 fog | fog §17 items 16–25 (KD-13 scoped D-T, KD-14 DW-006, KD-15 revolt-born seed, KD-17 ADR-037 notes, KD-18 emergent colonization, KD-28 pre-fog sessions), seed scope details, ghost expiry with M7 | as fog §17 |
| B-9 | KR-11 | KD-19 discovery scope (copper/tin, worldgen-derived truth) | approve |

## 3. Group C — later balance and content choices (no packet blocked now)

| ID | Item | When |
|---|---|---|
| C-1 | KD-10 pacing gap (RP inputs, demography, Age→dt) — costs are never the lever (D-046 §1) | before KR-16 |
| C-2 | CP coefficients and source yields (all PROPOSED placeholders) | with KR-5 measurement |
| C-3 | RP↔CP exchange economics: direction, ratio, limits, order, eligibility | M8 |
| C-4 | Sight radii and vantage values (sourced, never fitted) | T6.7 / M7 |
| C-5 | KD-20 (c) optional nuclear cuts; KD-24 energy ownership | KR-3 |
| C-6 | Which sourced corpus texts ship first | KR-7 |

## 4. Superseded within this docket (2026-10-22)

- KD-03's "CP rate below ≈ 15 % of RP" framing — withdrawn (CP is not a fraction of RP).
- KD-12's "geometric integer-disc sight (population band + vantage, sea included)" around settlements and the ≥ 12-node
  radius — withdrawn as the default; retained as option X4.
- KD-25's "ship entries at least `search-corroborated`" — replaced by "ship only `source-checked`".

## 5. Item detail (KD numbers kept as stable references)

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

### KD-02 — Civics Points (now part of A-1)
- **Question.** Ratify D-061.1 (separate Civics Points pool) and rule that the 60 new civics land only with CP?
- **Recommendation (revised 2026-10-22).** Ratify through A-1; CP is generated from its **own** inputs — population-led
  plus institution/specialist sources (`cp-economy-proposal.md` §1–§2) — never a fraction or split of RP. New civics
  land only with CP: under one pool they add 14–26 % to the A3–A7 research workload (`civics-tree-proposal.md` §7).
- **Alternatives.** One pool (the 2026-10-20 D-I ruling stands): keep the 7 civics only, defer the 60.
- **Latest point.** Before KR-5. **BLOCKING** (architecture gate, A-1).

### KD-03 — The research-stage civic atom (now A-4)
- **Question.** Keep `legal_code_roman` in `researchStage.requires` (it also gates `inst.university` and the A5 CORE
  fact, so CP pace sets Technology eligibility)?
- **Options (ESTIMATE, static model, seeds 1, 2, 42, 7; `cp-economy-proposal.md` §4).** **A** keep, with liveness
  tolerances P1–P7 as T6.3 acceptance: no delay against the one-pool twin when the placeholder CP scale is ≥ 0.5 per turn
  (≥ 1 per sim-year) even after 10,000 CP spent elsewhere; slow CP plus that detour delays the stage 9–581 turns, and in
  17 of 160 grid cells the civic never completes. **B** remove the civic from the stage expression, keep it on the
  realized university: stage 16–17 turns earlier in every case, but university eligibility stays exactly as
  CP-dependent; changes ratified stage content (D-046 F§1) — dated content ruling. **C** an OR route through
  civil-service examinations: no gain in any case.
- **Recommendation.** A, with B pre-registered as the remedy if a tolerance fails. The choice is the Director's.
- **Superseded wording.** The earlier "a CP rate below ≈ 15 % of RP" framing expressed CP as a fraction of RP and is
  withdrawn (contrary to D-061.3 and the Director's intent).
- **Latest point.** Architecture gate (A-4).

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

### KD-12 … KD-18, KD-28 — Fog of war (revised 2026-10-22; architecture items now A-3, packet items B-8)
Full questions: `fog-of-war-proposal.md` §17 (25 items, each marked A or L). **The first draft's default — passive
population/elevation-based settlement sight with a ≥ 12-node radius — is withdrawn** as contrary to the Director's
unit-led intent; it survives only as option X4 (§6.9). Revised recommendation: per-empire UNKNOWN / VISIBLE /
REMEMBERED from a stored monotone explored extent, derived current sight and a stored last-seen memory; **units are the
only default sight source**; an enemy formation leaves a dated GHOST marker (D-039 C1–C3), never a current unit; neutral
units leave nothing; contact and controller in remembered land come from the last observation; maps are not shared;
the AI reads the same scoped interface and difficulty reveals nothing; M6 exploration option **X2** (fog on, formation
sight, one-time initial extent from the 50 km hinterland constant) with its stated cost — ordered colonization nearly
stops until M7's scout. KD-13 (D-T, now scoped to plans, never movement), KD-14 (DW-006; architecture-level under X2),
KD-15 (revolt-born polities: seed over their own places, not a copy of the parent), KD-16 (scouts: dated clarification
of D-052 §5), KD-17 (ADR-037 notes), KD-18 (emergent colonization as a D-040 B1 deviation), KD-28 (pre-fog sessions;
D-H) keep their numbers. **BLOCKING** for KR-8/KR-9.

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

### KD-25 — Educational corpus adoption (now B-7; revised 2026-10-22)
- **Question.** Where the node texts live, what gate they pass, and whether the environment may reach source hosts.
- **Recommendation (revised).** A separate text file keyed by node id; **ship only `source-checked` entries** (validator
  C10/C11) — `search-corroborated` is not enough; a node's mechanic may ship without its text; allow the source hosts
  in the environment's network settings so entries can be raised to `source-checked`. Today 0 of 561 qualify
  (113 search-corroborated, 250 partially-checked, 198 unverified). **BLOCKING** for KR-7.

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

## 6. Sub-dockets (detail lives with the design)

- Fog: `fog-of-war-proposal.md` §17 (25 items, A/L marked), §18.0 (deviation register) and §19 (resolutions).
- Civics: `civics-tree-proposal.md` §4a (legacy audit) and §16 (OD-01…OD-11).
- Civics Points: `cp-economy-proposal.md` §5 (G-1…G-9) and §6.
- Director-intent check, conflicts and ranked corrections: `director-intent-reconciliation.md`.
- Cross-lane adjudication and lane open questions: `research/integration-adjudication.md`.
- Measured couplings: `current-tree-audit.md` §5 (K1–K11).

## 7. Record corrections surfaced (dated corrections; nothing edited)

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

## 8. Ruling block (append-only; the Director writes here)

> _[empty — awaiting the Director. Suggested form: "2026-10-DD — KD-01 ADOPTED; KD-04 APPROVED; KD-11 …". Until a line
> is written here, nothing in this package is in force.]_
