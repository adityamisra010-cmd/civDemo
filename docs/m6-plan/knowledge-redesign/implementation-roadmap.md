# Knowledge redesign — implementation roadmap (PROPOSED packets)

**Status: PROPOSED. Nothing in this roadmap is allocated or ratified.** M6 implementation stays blocked until the
Director ratifies `docs/m6-spec.md` and rules the gating decisions (`director-decision-docket.md`). **Ratification is
staged (Director intent, 2026-10-22):** the architecture gate (docket group A) comes first; each packet's mechanics and
content decisions (group B) are ruled before that packet; balance and content choices (group C) later. Ruling the
architecture approves no node, cost, yield, government form, AI strategy, UI behaviour or educational text.
Revised 2026-10-22 for the fog correction (unit-led sight), the CP source model and the corpus shipping gate. Packets are
small, testable and aligned with the M6 spec's T6.x lanes; where a packet extends a T6.x packet it says so. CLAUDE.md
rules apply to every packet: one packet per session and branch, cut from `main`; acceptance = the packet's criteria;
banned-constructs grep, `dotnet build`, `dotnet test`; POPULATED-table tests for new rows; turn-exact pins for new order
semantics; tie-dense tests for orderings over doubles; golden moves attributed with a switch-off twin; verification
worktrees pinned to the packet commit; merge only on the Director's ruling.

## 0. Dependency overview

```
KR-0 governance gate
  ├─> KR-1 engine fixes + measurement harness + content identity (T6.2+)
  │     ├─> KR-2 migration seam (alias/remap) ──> KR-4 technology content B (splits/merges)
  │     ├─> KR-3 technology content A (non-breaking)  ──┐
  │     └─> KR-5 civics engine (T6.3, needs D-061) ──> KR-6 civics content (T6.4/T6.5)
  ├─> KR-8 fog S2a (T6.7) ──> KR-9 fog S2b (T6.8) ──> KR-10 fog S2c (T6.9)
  │                              └─> KR-11 resource discovery contract (with T6.21 / D-051 §2(b))
  ├─> KR-7 educational corpus content (after KR-3/KR-6 shapes are fixed)
  ├─> KR-12 capability/institution consumers (T6.11)
  └─> KR-13 UI integration (T6.6)
KR-14 save/load/determinism closing (T6.17) — after every state packet
KR-15 vertical slice (T6.18) ──> KR-16 human acceptance, integration, performance, exit (T6.20)
```

Critical path: KR-0 → KR-1 → KR-3 → (KR-2 → KR-4) → KR-7 → KR-15 → KR-16. Fog (KR-8…KR-10) runs in parallel from
KR-0; civics (KR-5, KR-6) waits on D-061.

## 1. Packet table

| Packet | Extends | Scope (one line) | Blocking decisions | Size |
|---|---|---|---|---|
| KR-0 Governance gate | T6.1 | Director rulings on docket group A (architecture); group B items before their packet | — | docs |
| KR-1 Engine fixes, harness, content identity | T6.2 | AI goal closure (cheapest route + stage atoms), AI research measurement harness, calibration report refresh, content hash in session identity | KD-05, KD-07, KD-10, KD-11 | M |
| KR-2 Migration seam | new | content-declared key remap applied at load for retired/merged/split keys | KD-08 | M |
| KR-3 Technology content A | T6.4 (tech half) | new nodes, rewires, rebalances, relocations, renames, Eureka appends — no key retirement | KD-01, KD-04, KD-06, KD-09 | L |
| KR-4 Technology content B | T6.4 | splits, merges, retirements through KR-2 | KD-01, KD-08 | M |
| KR-5 Civics engine | T6.3 | CP generation from its own population-led inputs (not a fraction of RP), civic progress, ADR-043, schema bump, migration of the 7 civics, P1–P7 measurement; no RP↔CP exchange (M8) | A-1, A-4, B-4 | L |
| KR-6 Civics content | T6.4/T6.5 | 60 new civics, regime/policy entity kinds (content-only), validators, Age-fact rules | KD-02, KD-21, KD-22, KD-23 | L |
| KR-7 Educational corpus | new | node texts + sources into content and UI; ships only `source-checked` entries (C10 gate) | B-7 | M |
| KR-8 Fog S2a | T6.7 | explored extent, unit-produced VISIBLE sight (no passive settlement sight by default), last-seen memory incl. formation GHOST rows, the ruled M6 exploration option (X2 recommended), schema, PreFog twin | A-3, B-8 | L |
| KR-9 Fog S2b | T6.8 | gating, AI scoped queries, D-T, DW-002 companions, trade rule | KD-13, KD-14, KD-18 | M |
| KR-10 Fog S2c | T6.9 | UNKNOWN / VISIBLE / REMEMBERED display (dark overlay for remembered), GHOST markers with last-seen turn, no neutral-unit memory, blocked-action reasons | A-3 | M |
| KR-11 Resource discovery contract | T6.21 (D-051 §2(b)) | place-level copper/tin truth (worldgen-derived), identification, deposit UI states | KD-19 | M |
| KR-12 Capability/institution consumers | T6.11 | realize selected deferred entities through existing equations | KD-01, KD-27 | L |
| KR-13 UI integration | T6.6 | research/civics panels: lanes, cards, grants, owners, milestones, reasons | KD-01 | M |
| KR-14 Persistence closing | T6.17 | fixture resumes, cross-process legs, readable set | — | M |
| KR-15 Vertical slice | T6.18 | one playable chain end to end | KR-3, KR-8…KR-11 | M |
| KR-16 Acceptance and exit | T6.20 | human acceptance, 20-seed soak, performance, exit inventory | all | M |

## 2. Packets in detail

### KR-0 — Governance gate (docs only)
- **Scope:** the Director rules the docket items marked BLOCKING (`director-decision-docket.md` §1) and ratifies or
  amends `docs/m6-spec.md` (A1) and D-061.1 (A2). Rulings are dated lines in the named records; superseded passages are
  never edited.
- **Exclusions:** no code, content, schema or golden change.
- **Acceptance:** each BLOCKING KD item has a dated ruling; `CLAUDE.md:10` and `milestones.md` M6 row updated by the
  Director's instruction only.
- **Stop condition:** any ruling that changes a frozen Spine item goes through a CR first (S8).

### KR-1 — Research engine fixes, measurement harness, content identity (extends T6.2)
- **Scope:** (a) `AiResearchPolicy` goal closure uses the cheapest satisfying route (not the union over OR) and adds the
  research-stage atoms when a goal node sits in a closed subtree (engine audit Q11 gap; D03 finding: the university goal
  inflates to 60,440 RP); (b) a measurement harness that reports the AI polity's research directly (proposal in
  `measurements/baseline-progression.md` §6.5), outside production decision paths; (c) regenerate
  `docs/research-calibration-report.md` (its measured section is stale: seed-42 turn-1 population 5,118 vs 5,245);
  (d) content identity: the research content SHA-256 enters the session manifest and the resume check, with a clear
  refusal message on mismatch (current-tree audit K11); (e) D-R as ruled (KD-11).
- **Exclusions:** no research content change; no CP; no fog.
- **Prerequisites:** KR-0. **Blocking:** KD-05, KD-07, KD-10 (only for the report text), KD-11.
- **Files/systems:** `Sim.Core/Systems/Research/AiResearchPolicy.cs`, `ResearchSystem.cs` (if D-R changes),
  `SessionManifest.cs`, `UiSession.cs` (resume check), `Sim.Cli` (harness), `scripts/research-calibration-report.py`.
- **Acceptance:** tie-dense test for the new closure ordering; AI goal tests updated with dated pins; AI-empire goldens
  re-pinned with an attribution twin (`PreAiClosure` switch reproduces old hashes byte-for-byte); manifest round-trip and
  mismatch-refusal tests; harness output equals the measurement reconstruction on seeds 1, 2, 42, 7 (the
  `baseline-ai-node-completions.csv` figures as the control).
- **Migration/goldens:** manifest field appended (schema/manifest version per D-H); AI goldens move (attributed).
- **Stop/rollback:** any order-less golden moves ⇒ stop (the change leaked into zero-knowledge behaviour).
- **Human check:** none (headless).

### KR-2 — Content migration seam (alias/remap)
- **Scope:** a content-declared remap (`retiredKeys: [{key, successors[], rule}]`) applied deterministically at load: a
  completed retired key completes its successors per rule (merge/replace: the survivor; split: as ruled in KD-08);
  progress/Eureka/credit rows of retired keys are dropped from play exactly as today (they stay in the stream).
- **Exclusions:** no new nodes; the packet ships with a synthetic test content fixture.
- **Blocking:** KD-08.
- **Acceptance:** POPULATED-table tests; v34 fixture resume with a completed key that a fixture content retires;
  remap is idempotent and order-independent (sorted application); replay identity preserved; cross-process leg green.
- **Migration/goldens:** none in shipped content until KR-4.
- **Stop:** if the remap requires rewriting existing rows (forbidden by the append premise, `CanonicalSchema.cs:197-204`),
  stop and raise a CR.

### KR-3 — Technology content A (non-breaking)
- **Scope:** append the new technology nodes (keys 427+ in the order `tools/integrate.py` states); apply rewires,
  rebalances, relocations (incl. trunk relocations of craft nodes, KD-04), display-name changes, capability strings,
  `universityRelevance`, era-marked `emerged`, Eureka appends (never reorder); recompute `depth` and
  `finite_nodes_to_exhaust`; regenerate the three CI `--check` documents.
- **Exclusions:** no split/merge/retire (KR-4), no civics, no descriptions (KR-7).
- **Prerequisites:** KR-1. **Blocking:** KD-01, KD-04, KD-06, KD-09.
- **Files:** `Sim.Data/content/research.json`, `ages.json` (only if KD-06/KD-22 change facts), the three audit docs,
  `ResearchContentTests` pins (counts, the 83-unmarked rule, positions), `Sim.Ui` preview rigs that name ids.
- **Acceptance:** loader green; `tools/validate.py` 0 ERROR on the shipped content's projection; reachability report on
  seeds 1, 2, 42, 7 (stage turn, Age facts, civic gates) within the tolerances stated in
  `research-balance-analysis.md` §7; order-less goldens unchanged (MEASURED proof) unless a new root has an evaluable
  Eureka; AI-empire pins re-pinned with attribution; allocation scenario premises (keys 12/17/2/8/52/74/53) re-checked.
- **Stop:** any old-save resume regression not covered by a ruled migration rule.
- **Human check:** the Director opens the research panel and confirms the trunk relocations read as intended.

### KR-4 — Technology content B (splits, merges, retirements)
- **Scope:** the split/merge/retire records of the proposal through the KR-2 seam.
- **Blocking:** KD-01, KD-08. **Prerequisites:** KR-2, KR-3.
- **Acceptance:** every retired key has a remap row; a v34 fixture that completed each split node resumes with the
  ruled outcome; validators green; goldens attributed.

### KR-5 — Civics engine (T6.3)
- **Scope:** as `docs/m6-spec.md` T6.3 (CP pool, civics target kind 14, progress rows, ADR-043, schema bump, migration
  of the 7 civics, two conservation pins, minimal AI civics lane) — only if D-061.1 is ratified; otherwise KR-6 lands on
  the one pool and KD-02's workload warning applies.
- **Acceptance (added by this package):** the D12 tolerances P1–P7 (`civics-tree-proposal.md` §6, restated in CP
  terms; P7 = the tax edict stays available as today) measured against the one-pool twin on seeds 1, 2, 42, 7 with both
  drivers; failure triggers the pre-registered Option B remedy (`cp-economy-proposal.md` §4). CP is generated from its
  own inputs (§2 there), never as a fraction or split of RP. **Exclusions:** the RP↔CP exchange (government-change turn
  only; M8) and any government-change consequence such as a happiness penalty (not developed, by Director instruction).

### KR-6 — Civics content (T6.4/T6.5)
- **Scope:** the 60 new civics (keys 1008+), strands as `domain` lanes, validators (macro civics are leaves, never Age
  facts; I-7 optionality; no civic in a ratified gate closure), regime/policy representation per KD-21 (content-only in
  M6), entity re-points for institutions.
- **Blocking:** KD-02, KD-21, KD-22, KD-23. **Prerequisites:** KR-5 (or KD-02 ruling the one-pool path).
- **Acceptance:** loader + validators green; UI shows Researched ≠ Adopted (the "Adopted civics" lens renamed);
  AI never values a deferred promise as a benefit (test).

### KR-7 — Educational corpus content
- **Scope:** node texts and source references into content (recommended: a separate `research-text.json` keyed by node
  id, so text edits never change `research.json`'s SHA and the CI audits — KD-25), shown on the node card with sources.
- **Acceptance:** one text per node, 45–65 words, **only `source-checked` entries ship** (validator C10/C11; today 0 of
  561 qualify), an independent check of a stated sample, no duplicate/near-duplicate text; UI wraps text at all
  supported resolutions.
- **Blocking:** B-7. **Stop:** any entry not `source-checked` ships without text (the mechanic may ship; unsourced prose
  may not).

### KR-8 / KR-9 / KR-10 — Fog S2a, S2b, S2c (T6.7–T6.9)
- **Scope and acceptance:** `fog-of-war-proposal.md` §16 (implementation sequence), §11 (deterministic test strategy:
  metamorphic leak tests on seeds 1, 2, 42, 7 with positive controls, turn-exact discovery pins, POPULATED tables,
  PreFog twin, plus leak invariants for unseen enemy movement and unseen resource/world changes), §14 (persistence and
  migration), §6.7 (the M6 exploration option the Director rules), with the §19 resolutions as revised 2026-10-22 (units
  are the only default sight producers; formation GHOST rows per D-039 C1–C3; no neutral-unit memory; geology storage on
  the ADR-008 path; companion founding rules; sight geometry and radius are OPEN options, §6.3 — the earlier
  "radius ≥ 12 nodes" is withdrawn).
- **Blocking:** A-3 (architecture) and B-8 (packet decisions). **Stop:** a metamorphic test that cannot be given teeth
  (positive control fails) blocks the packet; no packet may add passive settlement sight unless the Director rules X4.
- **Human check (KR-10):** the Director plays 50 turns and confirms UNKNOWN / VISIBLE / REMEMBERED read correctly, a
  GHOST marker shows its age and never moves, neutral units vanish when out of sight, and a blocked founding explains
  why. Under X2 the check also confirms that M6 exploration is as limited as `fog-of-war-proposal.md` §6.7 states.

### KR-11 — Resource discovery contract (with T6.21 / D-051 §2(b))
- **Scope:** place-level deposit truth for the existing ores (copper, tin) derived from worldgen (not stored); the
  identification capability (`identification:deposit.tin_ore` via `ore_prospecting`); deposit UI states (located /
  identified); no new goods, no prospecting action (contract only).
- **Blocking:** KD-19. **Acceptance:** nuclear or any research never reveals a deposit (test); identification needs
  observed land (metamorphic test); surface-ore extraction unchanged unless KD-19 rules otherwise (golden proof).

### KR-12 — Capability and institution consumers (T6.11)
- **Scope:** realize selected deferred entities through EXISTING equations only (candidates from the lanes: sanitation
  through the per-settlement mortality multiplier, irrigation through farming output, mills through the weaving recipe
  with a capacity term) — each with an owner, a consumer test and a dated ruling.
- **Blocking:** KD-01, KD-27.

### KR-13 — UI integration (T6.6)
- **Scope:** research and civics panels show lanes (trunk, five subtrees; civics strands), node cards with the
  description, grants, realizing owner and milestone ("realized by … in M8"), and the reason an action stays blocked
  (missing prerequisite, missing input, unknown resource, unowned realization).
- **Acceptance:** UI equals simulation legality (T15/T16 patterns); read-isolation gate green.

### KR-14 — Persistence closing (T6.17)
- Fixture resumes for every readable version; cross-process legs; readable-set statement; every new row POPULATED.

### KR-15 — Vertical slice (T6.18)
- **Proposed slice:** under fog, a polity observes tin-bearing land, researches `ore_prospecting`, identifies the tin,
  casts bronze (existing recipe), and — on the civics side — completes `law_code` and founds a law court
  (`Unrest.ServiceOffset`, D-P). Playable from a fresh session; ≤ 5-item human checklist.

### KR-16 — Human acceptance, integration, performance, exit (T6.20)
- 20-seed soak; Windows smoke; bench (fog ≤ 2 % of a step, `fog-of-war-proposal.md` §9); exit inventory; the Director's
  play session.

## 3. Migration and golden implications (summary)

| Packet | Schema | Order-less goldens | AI-empire goldens | Fixtures |
|---|---|---|---|---|
| KR-1 | manifest field (+version per D-H) | unchanged (proof) | move (attributed) | v33/v34 resume unaffected |
| KR-2 | remap at load (no new table) | unchanged | unchanged | new synthetic fixture |
| KR-3 | none (content) | unchanged unless a new evaluable root Eureka | move (attributed) | allocation scenario re-checked |
| KR-4 | none (content + remap) | unchanged | move | v34 split-node fixture |
| KR-5 | bump (CP rows) | move only if civics change zero-knowledge behaviour | move | migration of 7 civics |
| KR-8 | bump (extent, memory) | move (founding seed) — PreFog twin | move | v34→v-next migration |
| KR-11 | none (worldgen-derived truth) | move if regional richness changes deposits (D-051 §2(b)) | move | — |

## Status and next priorities — 2026-10-23 (appended; packet text above unchanged)

**Delivered on `claude/new-session-yavghm` under D-062** (ADRs 043/044/045; evidence `m6-validation-report.md`):

| Packet | Delivered | Not delivered (stays in its packet) |
|---|---|---|
| KR-5 (T6.3) + stage gate | Civics Points lane, per-sim-year generation, bankable balance, spending, identity pins, AI two lanes, UI (CP balance, Stop civics), section 62; stage without `legal_code_roman` | other CP sources (institutions, specialists, embassies, spies, artists, scholars); RP↔CP exchange (M8) |
| KR-3 (+ part of KR-6) | 70 new technologies (427–496), 60 knowledge-only civics (1008–1067), 133 rewires, 61 re-scores, 22 relocations, 14 splits as new keys, 8 entity widenings | merges/retirements and consumer-removing moves (need KR-2); regime/policy entity kinds; adoption (M8) |
| KR-8/9/10-lite + KR-11-lite | concealment (ores, ≈25 %), Prospect (kind 15), Explore (kind 14), DiscoverySystem, UNKNOWN/VISIBLE/REMEMBERED, sighting memory, founding on known land, Actions menu, sections 63–66 | trade/road/claim gating by known area; GHOST rows and scouts (M7); richness classes |

**Next, in priority order (each with its acceptance criterion):**

1. **Age III pacing check (C-2). — DONE 2026-10-23 (ADR-046 §1):** the AI's cheapest-route goal closures restore bronze /
   Age III / first levy to pre-M6 timing or earlier on seeds 1, 2, 42, 7 (e.g. seed 42: 579 → 407). **Next:** Age IV is not
   reached within 900 turns under M6 (pre-M6: 828–869). TRACED (`audit/taxation-gate-trace.md` §7): it binds on the
   supporting milestones, which the AI never targets; the fix removed the accidental `abjad` (seed 1: 873 → not reached).
   Proposed: an AI goal for the cheapest research supporting milestones (Director's ruling; `docs/queue.md`).
   **DONE (batch 3, ADR-046 §6):** Age IV reached on seeds 1, 2, 42, 7 at 674 / 704 / 684 / 680; and §7: the AI builds its
   first granary before it surveys (canonical 2, 256 px 7). *Original item:* MEASURED effect: the AI's Age III entry moved from turn 422 to 579 (first levy 423 → 580). *Corrected 2026-10-23:* the cause is not `taxation` (the civic completes earlier under M6, 321 vs 336) but the bronze core: the AI's Technology lane, in the two-lane model, works the paved-road goal before bronze (union-over-OR goal closures; `audit/taxation-gate-trace.md`); the tree tranche and discovery add to it. Accept: measure seeds 1, 2, 42, 7 (player driver and AI empire); either the Director accepts the new pacing or `tuning.civicsPoints` is retuned with a recorded reason (D-062 R13) and the goldens re-pinned with attribution.
2. **Re-derive the D-055 allocation scenario under the shipped rules** and drop `--without-discovery` from the ci.yml allocation leg. Accept: the four split-checks pass without the switch.
3. **KR-2 remap seam, then KR-4** (merges, retirements, consumer-removing splits). Accept: a v34 save that completed a split/merged node loads with the successor keys granted (populated fixture).
4. **KR-1 engine fixes** (AI goal closure by cheapest route; research content identity on resume; overflow ruling D-R). Accept: as in KR-1 above.
5. **Fog gating of trade, roads and claims** by the known area (DW-006, D-T scoped to plans). Accept: metamorphic leak tests extended; corridor deltas recorded.
6. **KR-7 educational corpus** — blocked until source hosts are reachable (`educational-content-corpus.md` §0). Accept: only `source-checked` entries ship.
7. **Calibration refresh**: re-measure `scripts/research-calibration/population-trace-seed42.csv` and the calibration report (stale before M6).
8. **M7**: mobile scouts replace the Explore order; formation GHOST rows with movement.

