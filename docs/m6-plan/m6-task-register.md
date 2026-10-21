# M6 task register

**Status: PROPOSED (planning input, 2026-10-20, tree `dc83a67`). No packet is allocated by this document; the M6 spec
allocates.** Workstreams M6.1–M6.20 follow the Director's 2026-10-20 decomposition; M6.21–M6.24 add the authoritative
scope items the decomposition did not name and the conditional D-051 carry-over. Each workstream carries the same
fields. Sizes are relative (S < M < L < XL) with a one-line rationale; no calendar dates (no agreed capacity model).
Owner is the role that must act: **spec** (the M6 spec author), **impl** (implementing agent, one worktree per
packet), **director** (ruling or acceptance), **human** (the Director's playtest), **CI**. Status today is PROPOSED for
every workstream; "Designed" means a design exists in the record.

Common to every workstream (not repeated below): CLAUDE.md laws 1–7; the S8 §4.1 coupling map names what each perturbs;
no `Dictionary` iteration in sim logic; every double-valued ordering has a composite key with an integer tie-break and
a tie-dense test; every new serialized row ships a POPULATED-table test; one worktree per packet; findings are not
actionable before their verdict (ADR-015 §6); hand-back is a pointer (GOV-3 E2).

## Compact table

| ID | Workstream | Deliverable | Dependencies | Acceptance criteria (short) | Size | Owner | Status |
|---|---|---|---|---|---|---|---|
| M6.1 | Baseline, scope, governance | `docs/m6-spec.md` with S8 §4.1 items; `T6.1` foundations audit; registers finalized; housekeeping of stale headers | M5 exit gate | spec ratified; T6.1 table complete; decisions D-D…D-Q recorded | M | spec, director | PROPOSED |
| M6.2 | Existing technology & research audit | measured map of the research engine, Ages, unlock pipeline, universities; reusable-type list; stale-text fixes | M6.1 | every PRODUCTION/SEAM/NOT BUILT row verified on the base commit; no rewrite proposed | M | impl | PROPOSED (audit started 2026-10-20) |
| M6.3 | Progression architecture & capability reconciliation | the written model: knowledge → eligibility → realization → adoption; Age integration review; CR-017 touch list | M6.2 | conflicts raised before any packet; no node grants capability by itself | M | spec, director | PROPOSED |
| M6.4 | Civics model | content schema for micro/macro nodes and states; one pool/one target as ruled (D-I); adoption preconditions as predicates | M6.3; D-I, D-D | `ResearchEngineTests` stay green; adoption state defined with rejection paths | L | spec, impl | PROPOSED |
| M6.5 | Age-specific civic content & historical audit | the catalogue (ids, names, prerequisites, costs, applicability, consequence, realization, legal actions, tests) with a design basis per node | M6.4; D-Q | validator green; no node without a realizing mechanism or a knowledge-only mark | L | impl, director | PROPOSED |
| M6.6 | Macro government forms | regime-module data shape + derived label; availability via civics; adoption per D-D | M6.4, M6.5; D-D, D-M | no enum government read by a mechanism; label derived, never stored | M (option 1) / L (option 2) | spec, impl, director | PROPOSED |
| M6.7 | Civics UI & legal-action exposure | Progression › Civics and INSTITUTIONS lens: states, prerequisites, costs, locked reasons; action surface only lists legal actions | M6.4, M6.6 | UI availability == simulation legality (test); no parallel state model | M | impl | PROPOSED |
| M6.8 | Unit progression architecture | design note + M7 contract; progression schema reserved; no state (D-E) | M6.3; D-E | no `MilitaryUnits` writer added; contract reviewed by M7 handoff | S | spec | PROPOSED |
| M6.9 | Military promotion trees | content schema + validator (prerequisites, exclusivity, repeatability, referenced M7 fields), inert content | M6.8 | validator green; no stat duplicated; no effect applied | M | impl | PROPOSED |
| M6.10 | Repeatable training & AP contract | the contract document and data fields (dormant, eligibility, cost, increment, cap, diminishing returns, rejection) | M6.8; D-F | contract accepted into `m7-handoff-register.md` H7-02 | S | spec | PROPOSED |
| M6.11 | Settler / hero / special-character progression | design note: no carriers exist; what M7/M8 must create first | M6.8 | states the gap; proposes nothing unsupported | S | spec | PROPOSED |
| M6.12 | Unit training UI placeholder | one labelled, inert entry point "planned for M7" | M6.8 | no mutation, no fake buttons; smoke gate row | S | impl | PROPOSED |
| M6.13 | Fog of war, scouting, visibility persistence | S2a stored extent + `DiscoverySystem` (v35); S2b known-extent restriction; S2c LIVE/GHOST/UNKNOWN + sighting memory; ADR-037 channels closed | M6.1; D-J, D-K | monotone extent; turn-exact pin; leak tests green; AI independent of unknown cells; resume reproduces rows | L | impl, director | Designed (plan), PROPOSED |
| M6.14 | Visual milestone E1 | drawn buildings/institutions with count staging; Age terrain; authored unit silhouettes; conventions; regression tests | M6.1; M6.13 (ghost art); D-L | D-038 Part G at a Director gate; C1–C4 fence; frame cost measured | XL | impl, director (gate) | PROPOSED |
| M6.15 | One-file session export | C# codebook v4 encoder/decoder; workbook writer; size guard; self-check; goldens | M6.1; D-G | byte-identical round trips incl. the Director's 68-turn session; no new dependency unless D-G says so | M | impl | Designed (README), PROPOSED |
| M6.16 | Content corpus & consequence audit | validator over nodes/forms/upgrades/capabilities: ids exist, prerequisites valid, effects realizable, no double application; CI `--check` | M6.5, M6.6, M6.9, M6.22 | audit green in CI; every item classified | M | impl | PROPOSED |
| M6.17 | Save/load, replay, determinism | per-table populated tests; replay twins; schema ADRs; readable set; migrations | each table as it lands; D-H | goldens moved only with attribution; cross-process leg green | M (continuous) | impl, CI | PROPOSED |
| M6.18 | Vertical slice & integration | the D-P slice end to end; human session | M6.7, M6.12, M6.16, M6.17; D-P | the Director plays it; every slice item green | M | impl, human, director | PROPOSED |
| M6.19 | M7 handoff & deferred work | `m7-handoff-register.md` completed; deferred register updated | M6.8–M6.13 | single ownership per item | S | spec | PROPOSED |
| M6.20 | Milestone integration & acceptance | exit inventory in the M4/M5 form; soak; human acceptance; known-open list | all | §7 of the scope register met or dispositioned | M | director | PROPOSED |
| M6.21 | Knowledge diffusion, Eureka / foreign exposure, persistent knowledge | a `ResearchExposureRow` writer (contact/trade-carried exposure), Eureka review, knowledge persistence across control changes reviewed | M6.2, M6.3 | credit ceiling honoured; diffusion monotone; AI symmetric | L | impl | PROPOSED (seam exists) |
| M6.22 | Universities, research modifiers, deferred entities | D-045/D-046 Part D modifiers formula decision; realization of tiers A–D/G entities by owning systems; the 27 `inst.*` fate | M6.2; D-M | every realized entity has an owning system; tiers re-ruled by the Director | L | impl, director | PROPOSED |
| M6.23 | AI research & civics strategy; information access under fog | `AiResearchPolicy` extended to civics goals; AI reads only known cells; metamorphic tests | M6.4, M6.13 | `PolityKnowledgeTests` quarantine lifted; AI/player symmetry kept | M | impl | PROPOSED |
| M6.24 | Conditional: D-051 carry-over | §2 availability classes; §4 primitive toolmaking; §1(a); §6-remainder — only those R2 assigns | M6.1; R2 | calibration battery re-ruled per item; goldens attributed | L (if all) | impl, director | PROPOSED (conditional) |

## Workstream detail

### M6.1 — Baseline, scope and governance
- **Objective / player outcome:** none visible; the milestone has a ratified spec, a baseline commit and clean records.
- **Scope:** write `docs/m6-spec.md` from this package with S8 §4.1's four items (`T6.1` FOUNDATIONS AUDIT as packet
  one: the existing quantities M6's systems stand on — RP/turn, EffectiveCost factors, university maturity/decay τ,
  `authorityDecayCostUnits`, legitimacy, sight isochrone inputs, lattice node size — enumerated from code; dimensional
  declaration; corridor independence incl. arch-PQ Q-26; coupling map); finalize the scope/task/decision/risk/handoff
  registers; housekeeping: dated notes on stale headers (CR-012, CR-018, ADR-034), CR-005 close-out, stale code comments
  (`ResearchQuery.cs:562`, `PolityKnowledge.cs:27-28`, `UnitCardModel.cs:46, 79-80`, CLAUDE.md:10's `m5-integration`
  and "unmerged art-substrate branch" clauses), arch-N Ladder-B translation note. **Exclusions:** any code.
- **Reuse:** `docs/m4-spec.md` skeleton; `m4-final-closure-audit.md` form; this folder.
- **Ownership:** spec author; the Director ratifies.
- **Prerequisites:** the M5 exit gate (S8 §4). **Decisions:** D-D…D-Q collected here.
- **Deliverables:** the spec; T6.1 table; records. **Acceptance:** spec ratified by dated ruling; T6.1 "no unexplained
  omissions". **Tests:** docs checks (contradiction grep across milestones/current-state/CLAUDE.md).
- **Determinism/persistence:** none. **Size: M** — the spec is long but its inputs exist. **Risks:** R-11, R-12.
- **Parallel:** none (gate). **Exit:** packets cut. **Deferred owner:** —.

### M6.2 — Existing technology and research audit
- **Objective:** a measured, cited map of what exists so nothing is duplicated.
- **Scope:** verify on the base commit the audit of 2026-10-20 (`m6-scope-register.md` §2): corpus, loader validators,
  engine, 51 live consumers, Ages, universities, seams, AI; name reusable types for civics (`ResearchContentLoader`,
  `Predicate`, `ResearchQuery.*`, `InstitutionLifecycle`, `Governance.*`, `AvailableActionsQuery`, schema test
  templates); fix stale comments; decide whether `InstitutionRow.Type` is content-driven (slice item 8).
- **Exclusions:** no behaviour change. **Reuse:** `scripts/research-*.py`, `docs/research-gameplay-unlock-audit.md`.
- **Ownership:** impl (read-only). **Prerequisites:** M6.1. **Decisions:** none.
- **Deliverables:** audit record; reusable-type list; stale-text fixes (comment-only commits).
- **Acceptance:** every row cites file:line on the base commit; tests unchanged and green.
- **Tests:** none new. **Determinism/persistence:** none. **Size: M.** **Risks:** R-02, R-12.
- **Parallel:** with M6.13/M6.15. **Exit:** M6.3 can start.

### M6.3 — Progression architecture and capability reconciliation
- **Objective:** one written model that every later packet obeys: knowledge (completed nodes) → eligibility
  (predicates) → realization (owning systems on computed state) → adoption (a separate act with preconditions and
  state) → active effects (coefficients inside named equations); legal actions through one predicate, two callers;
  simulation-side rejection; Age integration read-only.
- **Scope:** reconcile D-040, arch-C, civilization-progression-architecture, arch-O (Section I binding), D-044/D-046/
  D-047, ADR-029–031; resolve the "visible graph vs realized capability" tension in writing: the trees are lenses over
  computed state (CR-017 §8 reading), 332 knowledge-only nodes stay honest ("knowledge-only" label), and the model names
  the carrier test (D-035-C) for every effect; list CR-017 collisions M6 touches.
- **Exclusions:** no new mechanics. **Reuse:** the documents above.
- **Ownership:** spec; the Director rules conflicts. **Prerequisites:** M6.2. **Decisions:** D-D, D-M, D-Q surfaced.
- **Deliverables:** the model section of the spec (§3). **Acceptance:** no open contradiction left unraised.
- **Size: M.** **Risks:** R-03, R-16. **Parallel:** M6.21 design. **Exit:** M6.4/M6.8 start.

### M6.4 — Civics progression model
- **Objective / player outcome:** the player researches civics on the same RP they research technology (D-I option 1),
  sees them become Available, and adopts institutional forms as separate acts with stated preconditions.
- **Scope:** content schema for micro (numerous, cheaper, age-specific practices) and macro (fewer, costlier, government
  forms) nodes on `ResearchTree.Civics`; states Unresearched → Researched/Available → Adopted/Active, with Adopted/Active
  as institution or regime-module state (not a node state); adoption preconditions as polity-scoped predicates
  (reach, seat, Age exposure noted, institution facts) implemented as query functions (no Empire-scope `VariableRow`
  — arch-PQ Q-03); rejection paths; AI symmetry. Costs: BaseCost model as is; "required core set exposes macro choices,
  optional micro nodes add depth" evaluated against the Age milestone evaluator (civics are already Age milestones).
- **Exclusions:** a second currency (D-I); government adoption action if D-D option 1; policy cards.
- **Reuse:** `ResearchNode`, `Predicate`, `ResearchQuery`, `InstitutionLifecycle`, `Governance.CanLevyTax` chain,
  `TaxPolicyRow` policy-as-state template, `AvailableActionsQuery` domains Governance/Institutions.
- **Ownership:** spec + impl. **Prerequisites:** M6.3; D-I, D-D, D-M. **Decisions:** D-I, D-D, D-M.
- **Deliverables:** schema + loader rules; adoption state table design; order kinds ≥ 14 with turn-exact pins.
- **Acceptance:** `ResearchEngineTests` (39) and `ResearchContentTests` (50) green or superseded by a dated pin; new
  predicates covered by `AvailableActionsQueryTests`-style tests; T15/T16 patterns for every civics verb.
- **Determinism/persistence:** new tables → version per D-H; populated tests; AI argmax composite keys.
- **Size: L** — new state and orders. **Risks:** R-01–R-05. **Parallel:** M6.8 lane. **Exit:** M6.5/M6.6 content can land.

### M6.5 — Age-specific civic content and historical audit
- **Objective:** the catalogue, each node with stable id, name/description, prerequisites, cost, age/historical
  applicability (metadata only — law 4), capability or institutional consequence, realization requirements, legal
  actions affected, tests. Historical evidence separated from design abstraction; no universal claims.
- **Scope:** start from the 7 existing civics; add micro nodes only where a realizing mechanism exists today or in a
  named M6 packet (tax administration → the gate chain and reach; codified law → the law court (slice); census →
  `inst.census` lifecycle; civil service → reach decay constant as a coefficient); military civics ("Levying Troops")
  knowledge-only until M7 (D-Q). Macro nodes: the module sets they make eligible.
- **Exclusions:** balance values without a design basis; a finished historical catalogue.
- **Reuse:** `research.json` civics section; `scripts/research-content-audit.py`.
- **Ownership:** impl; the Director reviews content. **Prerequisites:** M6.4; D-Q. **Deliverables:** content + audit rows.
- **Acceptance:** validator green; `Canonical_CivicsAre…` pin superseded with a dated note; every node's consequence
  classified (live / knowledge-only).
- **Size: L** — content volume and review. **Risks:** R-01, R-03. **Parallel:** with M6.6 after the first nodes.

### M6.6 — Macro government forms and adoption
- **Objective:** government forms as derived labels over regime-module sets; availability via civics; adoption per D-D.
- **Scope (D-D option 1):** the regime-module set table design (serialized, latched, per Empire — arch-FGH §2.1),
  module presence from institution/civic state, the derived label for chronicle and UI, UI availability display; no
  adoption order, no active effects. **(Option 2 adds:** adoption order kind, active-module effects as coefficients,
  transition/replacement rules, D-021 brakes, legitimacy type.)
- **Exclusions:** regime change, coups, elections, succession (M8); enum governments.
- **Reuse:** `ClassStateRow.Active` latch + hysteresis precedent; `AgeStateRow` absence-is-default pattern.
- **Ownership:** spec + impl; director for D-D. **Prerequisites:** M6.4, M6.5; D-D, D-M.
- **Acceptance:** no mechanism reads the label; module presence derived from state with tests; (option 2) delivery pins.
- **Determinism/persistence:** one appended table (D-H). **Size: M / L.** **Risks:** R-06, R-03.

### M6.7 — Civics UI and legal-action exposure
- **Objective / player outcome:** the player sees micro/macro nodes, prerequisites, costs, states, and why something
  is unavailable; adoption interface only if D-D option 2.
- **Scope:** extend `ProgressionScreen` (TreeTab.Civics) and the INSTITUTIONS lens; locked-state explanations from the
  same predicates the simulation uses; the action surface lists only legal actions; D-038 C1 (no figures) holds.
- **Exclusions:** a parallel state model; any UI-side authorization.
- **Reuse:** `ProgressionScreen`, `Lenses`, `ResearchOrderFactory`, `AvailableActionsQuery`, `FrameText` tests, the
  playability gate rows.
- **Ownership:** impl. **Prerequisites:** M6.4, M6.6. **Acceptance:** `ProgressionScreenTests`, gate rows, a test that
  UI availability equals simulation legality for every civics verb; smoke gate green.
- **Size: M.** **Risks:** R-06 (UI authorizing). **Parallel:** after M6.6's shape.

### M6.8 — Unit progression architecture (design + contract)
- **Objective:** distinct progression models per class that actually exists (army formations; mobile agents when they
  exist), written against the D-011 formation fields M7 will create; no universal XP system; no Builders.
- **Scope:** design note; progression content schema reserved; the M7 interface contract (`m7-handoff-register.md`
  H7-02, H7-05); confirm construction stays city-level (source guard: no `Builder` type, no worker order kind).
- **Exclusions:** state, writers, orders (D-E option 1). **Reuse:** `unit-families.json`, `MilitaryQuery`, D-011, D-043.
- **Ownership:** spec. **Prerequisites:** M6.3; D-E. **Acceptance:** contract reviewed; no `MilitaryUnits` writer.
- **Size: S.** **Risks:** R-06, R-17. **Parallel:** units lane.

### M6.9 — Military promotion trees and upgrade definitions
- **Objective:** class-appropriate trees with smaller upgrades (armour, skills, health, attack …) as examples, not
  balance; prerequisites, mutually exclusive choices, repeatability; grounded in the named M7 fields.
- **Scope:** content schema + validator; inert content for 2–3 families; validation that every referenced field is in
  the M7 contract; no duplicate stat, no second source of truth.
- **Exclusions:** effects, state, UI beyond the placeholder. **Ownership:** impl. **Prerequisites:** M6.8.
- **Acceptance:** validator green in CI; `scripts/research-*` pattern extended. **Size: M.** **Risks:** R-17.

### M6.10 — Repeatable training and the Action-Point contract
- **Objective:** the contract M7 implements: dormant (a named M7 state), explicit vs passive (recommend explicit action),
  eligibility, cost (what capacity it consumes), increment, repeat limits / diminishing returns / escalating cost, cap,
  persistence/replay, rejection; "inactive units never improve accidentally".
- **Exclusions:** AP state, the action, movement (M7; D-052 §4; CR-017 §3). **Ownership:** spec. **Prerequisites:**
  M6.8; D-F. **Acceptance:** accepted into H7-02. **Size: S.** **Risks:** R-06.

### M6.11 — Settler, hero and special-character progression
- **Objective:** honest design note: settlers are an instantaneous order (ADR-036), heroes/special people are D-043 B3
  (ratified) with CR-017 §7 OPEN and notables have no writer; what M7/M8 must create before any progression exists;
  construction stays city-level.
- **Exclusions:** new classes, mechanics, actions. **Ownership:** spec. **Prerequisites:** M6.8. **Size: S.**

### M6.12 — Unit training UI placeholder
- **Objective:** a reserved panel location, "Training — planned for the Battle Layer (M7)", following `UnitCardModel`'s
  "not simulated" lines; no buttons that mutate; no provisional logic.
- **Reuse:** `UnitCardModel`, `PanelLayout`, the smoke gate. **Prerequisites:** M6.8. **Acceptance:** gate row; a
  source test that no order is emitted from the panel. **Size: S.**

### M6.13 — Fog of war, scouting and visibility persistence
- **Objective / player outcome:** the world's extent is discovered (D-040 A4); unknown land, settlements and formations
  are unknown; known-but-unseen ones show as GHOSTs with remembered positions; settle/claim/trade respect known extent;
  the AI plays by the same information.
- **Scope:** S2a — `KnownExtentRow` bitset per polity over the 16 km lattice, `DiscoverySystem` ORs the visible set
  each step (sight = isochrone from controlled settlements and owned formations), seeding from founding catchments,
  revolt copy / annexation union, schema v35, `PolityKnowledge.Of` reads it (D-K); S2b — known-extent restriction of
  settle/claim/trade and the placement verdict (DW-002), `TradeQuery` mutually-known term (DW-006); S2c — LIVE / GHOST /
  UNKNOWN display with persisted sighting memory, Decision A revisit (token size), ADR-037 §6 channels closed (AI site
  valuation, routes within 600 km, terrain bake), DEV panels stay world-wide. Scouts per D-J.
- **Exclusions:** reconnaissance as an operational verb, command friction, siege (M7, D-039 A/D/E).
- **Reuse:** `PolityKnowledge`, `PlayerKnowledge`, `KnowledgeStubs`, `KnowledgeLeakTests*`, `PolityKnowledgeTests`,
  `check-read-isolation.sh`, the S2a plan (`m5w-map-workstream-audit-and-plan.md:725-810`).
- **Ownership:** impl; director for D-J/D-K. **Prerequisites:** M6.1; D-J, D-K. **Deliverables:** three packets.
- **Acceptance:** bits never clear across steps incl. revolt; turn-exact discovery pin; populated-table test (16
  bytes/row); snapshot and UI resume reproduce extent rows; cross-process leg; every leak test green with the stub
  replaced by real extent; AI orders independent of unknown cells (quarantine lifted); `sim bench` before/after.
- **Determinism/persistence:** v35 + ADR; goldens move (founding-seeded rows) with attribution controls.
- **Size: L** — three packets, one schema bump, UI states. **Risks:** R-07, R-14, R-05. **Parallel:** fog lane.
- **Exit:** D-060 §9 "Fog (S2a) is unbuilt" superseded by a dated note.

### M6.14 — Visual milestone E1
- **Objective / player outcome:** "a settlement's size is legible at a glance without reading a number; an army reads
  as an army; terrain has relief; the whole reads as one illustrated map" (D-038 G2).
- **Scope:** drawn buildings and institutions with count staging 1/10/20/30 (D-043 D2/H8); Age-specific terrain and
  architecture (style register per D-L); authored unit silhouettes (C3; no anatomy, C1); asset conventions
  (`Sim.Ui/Art` manifest), rendering integration (`WorldLens`, `SettlementComposition`), frame-cost measurement
  (`MapFrameCost`), visual regression (palette exactness, seamlessness, regenerability) and smoke rows; m5w P5, P12b,
  P14; the D-052 §7 unit-display rework if closure ruling R3 folds it here.
- **Exclusions:** any simulation effect of visuals; figures in tokens.
- **Ownership:** impl; the Director's visual gate (D-023). **Prerequisites:** M6.1; M6.13 for ghost art; D-L.
- **Acceptance:** D-038 Part G at a gate session; tests above; no frame-cost regression beyond a measured budget.
- **Size: XL** — asset production across ages and families. **Risks:** R-08. **Parallel:** visual lane.

### M6.15 — One-file session export
- **Objective / player outcome:** one `session-<stamp>.xlsx` beside the six files; upload one file to an AI assistant.
- **Scope:** C# port of codebook v4 (`docs/m6-session-export/codebook-telemetry-v4.tsv`; `spec.py` as the table's
  source); sheets README, Manifest, Orders (decoded), Trace, Chronicle, Telemetry (one coded cell per turn), Codebook,
  Forensic; README §5: size guard with marked continuation cells (overflow at ≈49 settlements), self-check decode of
  every cell (named error, no raw fallback), exact double text, `InvariantCulture`, codebook versioning per telemetry
  schema, goldens from the prototype's measured runs; writer per D-G; written at session close and by `sim inspect
  --export`; the six files untouched (resume/replay unchanged).
- **Exclusions:** encryption; replacing the record; making the workbook a replay source.
- **Reuse:** `TelemetryWriter`, `SessionManifest`, `SessionTrace`, `ForensicSession`, `OrderLog.Load`, the prototype.
- **Ownership:** impl. **Prerequisites:** M6.1; D-G. **Acceptance:** byte-identical round trips on the three measured
  runs and the Director's session; adversarial cases (the prototype's 98); workbook opens in Excel (human); CI check.
- **Determinism/persistence:** none in sim; the export itself deterministic (fixed zip order/timestamps).
- **Size: M.** **Risks:** R-09. **Parallel:** export lane.

### M6.16 — Content corpus and consequence audit
- **Objective:** every authored node, form, upgrade and capability has a coherent path from definition to legal effect
  or an explicit knowledge-only mark; no illegal action reaches the simulation; UI availability equals legality; no
  effect applied twice.
- **Scope:** extend `scripts/research-content-audit.py` and `research-gameplay-unlock-audit.py` (CI `--check`) to
  civics, modules, promotion content; a C# validator for shape rules at load; tests that inject every gated order kind
  and assert rejection (`T15` pattern); a double-application audit (one predicate, two callers).
- **Ownership:** impl. **Prerequisites:** M6.5, M6.6, M6.9, M6.22. **Acceptance:** audits green in CI; the
  classification table regenerated. **Size: M.** **Risks:** R-03.

### M6.17 — Save/load, replay and determinism
- **Objective:** coverage for every new M6 state as it lands: civics/adoption rows, regime modules, known extent,
  sighting memory, export versions/decoding, AI/human action legality.
- **Scope:** per table — Write/Read/ExpectedLength, POPULATED test, snapshot round trip, clone independence,
  `StateEquals`, `SnapshotDiff` layout; replay twins (ADR-006); turn-exact delivery pins per new order kind; schema
  ADR per bump (D-H) with readable-set statement and migration tests (v33/v34 fixtures still resume); golden moves with
  attribution controls; cross-process leg; `sim bench` before/after.
- **Ownership:** impl, CI. **Prerequisites:** each table. **Acceptance:** harness green; no unexplained golden move.
- **Size: M (continuous).** **Risks:** R-04, R-05.

### M6.18 — Vertical slice and integration
- **Objective / player outcome:** the Director plays completion → eligibility → establishment → maturation → effect once,
  end to end (D-P): `law_code` → the law court institution → `ServiceOffset`; the regime module "codified law" and its
  derived label; an inert promotion entry in the placeholder; simulation-side rejection; UI states; persistence.
- **Ownership:** impl, human, director. **Prerequisites:** M6.7, M6.12, M6.16, M6.17; D-P. **Acceptance:** every slice
  item green; one human session with a ≤5-item checklist; Director acceptance recorded.
- **Size: M.** **Risks:** R-15. **Exit:** the catalogue work (M6.5 at volume) proceeds only after the slice is accepted.

### M6.19 — M7 handoff and deferred work
- **Deliverable:** `m7-handoff-register.md` completed with contracts from M6.8–M6.10 and M6.13; `deferred-work-register.md`
  updated; single ownership per item. **Prerequisites:** M6.8–M6.13. **Size: S.**

### M6.20 — Milestone integration and acceptance
- **Deliverable:** `docs/m6-exit-inventory.md` in the M4/M5 form; the 20-seed soak; human acceptance; known-open list;
  the `m6-exit` Release by the Director. **Acceptance:** `m6-scope-register.md` §7 met or dispositioned by ruling.
  **Size: M.** Not complete because tasks merged or CI is green.

### M6.21 — Knowledge diffusion, Eureka / foreign exposure, persistent knowledge
- **Objective:** knowledge moves between civilizations by contact, not only by revolt/annexation; Eureka reviewed;
  persistence across control changes stated.
- **Scope:** a `ResearchExposureRow` writer (trade/route contact as the carrier; credit ≤ 0.40 × BaseCost already
  enforced); which of the ledger §12's 13 pathways are M6 (the rest named for later); `KnowledgeTransfer` review;
  city-state knowledge rule (`docs/city-state-progression.md` §4 OPEN); monotonicity kept (`KnowledgeMonotonicTests`).
- **Ownership:** impl; director for pathway choice. **Prerequisites:** M6.2, M6.3. **Acceptance:** exposure credited
  deterministically; AI symmetric; D-021 brake if it accelerates. **Size: L.** **Risks:** R-04, R-13.

### M6.22 — Universities, research modifiers and deferred research entities
- **Objective:** the research-modifier formula decision (D-045/D-046 Part D), the fate of the 27 unrealized `inst.*`
  entities, and realization of deferred entity tiers A–D and G by owning systems (per-building capacity mechanism first
  — `deferred-entity-realization-plan.md`); tier membership re-ruled by the Director.
- **Ownership:** impl, director. **Prerequisites:** M6.2; D-M. **Acceptance:** each realized entity has an owning
  system and a consumer test; tiers recorded. **Size: L.** **Risks:** R-03.

### M6.23 — AI research and civics strategy; information access under fog
- **Scope:** `AiResearchPolicy` goals for civics (the `TaxGoal` template); AI adoption choices (if D-D option 2) with
  composite keys; AI site valuation, roads and founding under known extent (ADR-037 §6 channels); metamorphic tests
  (`PolityKnowledgeTests`). **Prerequisites:** M6.4, M6.13. **Size: M.** **Risks:** R-14, R-12.

### M6.24 — Conditional: D-051 carry-over
- **Scope (only what closure ruling R2 assigns):** §2 availability classes / floor / regional richness (with P9/P10
  overlay data), §4 primitive toolmaking (CR-020 §A option), §1(a) smoothed flight deficit, §6-remainder central
  stockpile. Each is a sim change with calibration impact: battery re-ruled per item, goldens attributed, an
  independent lane. **Size: L if all.** **Risks:** R-18.


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- **M6.1 status: deliverables DRAFTED, awaiting ratification** — `docs/m6-spec.md`, `docs/m6-foundations-audit.md`,
  D-061/CR-023, this package's dated notes, housekeeping notes on CR-012/CR-018/ADR-034/ADR-029 (appended, not rewritten).
  Stale CODE comments listed in M6.1's scope are NOT changed by M6.1 (the Director's hard constraint "no gameplay code
  changes"); they move to the first code packet (T6.2). M6.1's acceptance ("spec ratified by dated ruling; T6.1 no
  unexplained omissions") is NOT yet met.
- **M6.4** "on the same RP (D-I option 1)" is superseded in reading by D-061 (PROPOSED). The packet mapping
  M6.x → T6.x is in the spec §4 (M6.4 → T6.3 currency/graph + T6.4 content; M6.13 → T6.7/T6.8/T6.9; M6.2+M6.3 → T6.2).
- The comprehensive Civ VI comparison does not exist in the tree; proposed as T6.1b (documentation only).
