# M6 — KNOWLEDGE / RESEARCH / TECHNOLOGY — MILESTONE SPECIFICATION (DRAFT)

**Status: DRAFT FOR THE DIRECTOR'S REVIEW — NOT RATIFIED. No packet beyond T6.1 is allocated; no gameplay code, content,
schema or golden changes are authorized by this document.** Written 2026-10-21 under S8 §4 after the M5 exit gate
(`m5-exit` = `main` `6e51a3e`, published 2026-10-21). Base commit for every citation: `claude/new-session-yavghm` @
`a0b3b25` (= `6e51a3e` + two docs-only closure commits). Structural reference: `docs/m4-spec.md` (the first S8 §4.1 spec).
Inputs: `docs/m6-plan/` (planning input, 2026-10-20), `docs/m6-foundations-audit.md` (T6.1, measured),
`docs/d061-civics-points-supersession.md` (PROPOSED), `docs/adr/cr-023-…md` (RAISED).

Labels: **RATIFIED** (ruling or frozen document, cited) · **RULED** (a dated Director ruling) · **DIRECTED** (the
Director's 2026-10-21 instruction, not yet a ruling in the record) · **MEASURED** (T6.1) · **PROPOSED** (this spec) ·
**DIRECTOR DECISION REQUIRED**.

---

## §0. S8 §4.1 CONFORMANCE, REQUIREMENT BY REQUIREMENT

| S8 §4.1 requirement | where it lives | state |
|---|---|---|
| **1. Foundations audit as packet one** | **§4 T6.1** and its deliverable `docs/m6-foundations-audit.md` | **RUN and WRITTEN** (99 rows across A–E; dispositions in its §F: no (b) mismatch, no impossible magnitude, "chosen never derived" recorded and queued). The Director adjudicates the table |
| **2. Dimensional declaration** | **§3.2** | **PARTIAL BY CONSTRUCTION and stated as such**: units of every existing quantity are READ OFF the producing expression and cited (anti-self-certification); the two new quantities (Civics Points, sight budget / known extent) are declared with units and the equations each packet must balance before implementation |
| **3. Corridor independence** | **§3.3** | **SATISFIED by refusal**: no knowledge corridor is proposed as an M6 exit gate; arch-PQ Q-26 is answered with a measured, non-gating baseline and the shape a future corridor must take; the two existing quarantined corridors fog will perturb are named |
| **4. Coupling map** | **§3.4** | **SATISFIED as the spec author's PROVISIONAL reading**; T6.1 is authoritative over it (living clause) |

**Stated limit.** The core equations of CP generation, sight and diffusion are not designed here; §3.2 declares their
units and the balance each packet must show, as M4 did for B-2 and the AutoResolver.

---

## §1. DECISIONS CLOSED (carried from ratified or ruled records; verified against the tree)

| # | decision | source |
|---|---|---|
| 1.1 | Two top-level trees: Technology (five subtrees) and Civics; non-linear prerequisites; causal edges | D-044 R3/R8; D-047 r1 (RATIFIED) |
| 1.2 | Completion grants ELIGIBILITY ONLY; completion builds nothing; consumers ask the query | D-044 R11/R14; ADR-029 §10 (RATIFIED) |
| 1.3 | Civic completion → governance capability → institutional eligibility → adoption as a SEPARATE act with its own conditions | civ-progression §5.4 (DIRECTOR RULING) |
| 1.4 | Policy cards / "spend points → bonus" NOT adopted; government as mechanism, not modifier | civ-progression §5.3; D-042 §5.3; law 2 (RATIFIED) |
| 1.5 | M6 defines civic prerequisites and government ELIGIBILITY; adoption and active political effects are M8; Available vs Adopted distinguished in model and presentation | D-D (RULED 2026-10-20) |
| 1.6 | Unit progression: semantics, content schema, validation, M7 contract only; no serialized per-unit state | D-E (RULED) |
| 1.7 | Training / AP: contract only; no state, order or AP-resolution change; training panel = labelled inert placeholder | D-F (RULED) |
| 1.8 | Fog of war, scouts, LIVE/GHOST/UNKNOWN, sighting memory, known-extent restriction of settle/claim/trade are M6 | D-052 §5 (RATIFIED); D-040 B1 |
| 1.9 | E1 visuals are M6; C1 (no figures) stands; D-052 §7 unit display deferred to E1 | D-052 §6; R3 (RULED) |
| 1.10 | One-file session export: size-reducing encoding, one workbook BESIDE the six files, no encryption; M6 | ruling 2026-10-20 (`m5-run-recording-audit.md` §9) |
| 1.11 | Eureka ceiling 0.40 × BaseCost retained; no overflow of credit | D-047 r10; ledger §26 items 25–26 |
| 1.12 | Age advancement is explicit (order kind 7), irreversible, never automatic; research never reads the Age; modernization automatic and free | ADR-031; D-044 R13; D-047 r13/r18; D-043 A8 |
| 1.13 | No Builder unit; no second exploration system; "a scout is a person drawn from a bucket" | D-045 §1; D-040 B2; D-039 B1 |
| 1.14 | Order kind 10 reserved (M7 MoveFormation); schema v35 reserved to S2a fog, v36 to S3, "first to land" rule | D-055 note; D-054 §5 item 13 |
| 1.15 | Laws-lite and the authority/bandwidth economy enter the M6 civic specification subject to architecture review; budget/treasury unassigned (CR-008 closed) | R4 (RULED) |
| 1.16 | Civ VI is a design REFERENCE, adapted never copied (no flat Science output, no hex ZOC, no paid upgrades) | D-047 A.2 (RULED) |
| 1.17 | **Civics Points as a separate currency; a genuinely distinct civics tree** | **DIRECTED 2026-10-21 — recorded as D-061 PROPOSED; supersedes D-I when ratified (CR-023)** |

---

## §2. SCOPE FENCE — WHAT M6 EXPLICITLY DOES NOT DO

- Movement, Action Points/Capacity state, right-click move, recruitment, headcount, battle resolution, the 23 `unit.*`
  entities, a functional training panel — **M7** (D-052 §4; D-011; `m7-handoff-register.md`).
- Government ADOPTION as an action, active-government effects, regime change, coups, elections, legitimacy as a stock,
  succession, diplomacy, the D-019 verb-freeze matrix — **M8** (D-D).
- A third currency, a general RP or CP reserve, manual percentage allocation among targets, a reusable per-turn
  exception mechanism (ledger §26 items 4, 5, 11 stay in force even under D-061).
- Builders / worker units; worker-placed improvements — never (D-045 §1).
- Encryption of session files; replacing the six files with one container.
- Any golden, canonical-hash or schema change outside an ADR with attribution controls (CLAUDE.md).
- Redesign of frozen items: the Spine, the kernel contract, D-011, the milestone order.
- Finalizing a historical civics catalogue or balance values without a design basis (R-01).

---

## §3. SYSTEM NOTES

### §3.1 The shape of the milestone, and the invariants every packet preserves

M6 turns the research engine shipped in M5 ("early work, not completion", `milestones.md`) into the authoritative
**Knowledge → Capability** layer, adds Civics as a second progression domain with its own currency (D-061), gives the
world fog and discovery, and ships the E1 visual tier and the one-file export. M7 consumes M6's capability vocabulary;
M8 consumes its eligibility state.

**Invariants (I-n) — each packet's acceptance must show they hold:**

- **I-1 Eligibility, not grants.** No node completion writes anything but its completion row. Every capability is a
  predicate answered by a query (`ResearchQuery.IsKnowledgeEligible` / `RequirementMet`, and the civics analogue). The
  orphan and unlock-reverse-index validators stay red for a node that nothing consumes unless it is marked knowledge-only.
- **I-2 Five states, four owners.** `Unresearched → Researched (= Available/Eligible) → Adopted → Active` plus
  `Realized` for entities. Research systems own the first two; an institution or regime-module table (M8 for government
  adoption; InstitutionsSystem for institutions) owns Adopted/Active; realizing systems (Construction, Production,
  Governance…) own Realized. **No state is implied by another**: "Available" is never read as "Adopted".
- **I-3 Gate on completion, never on a currency.** RP and CP appear only as terms inside rate equations and never as the
  left operand of a comparison that gates anything (arch-O II.2 fence; D-020 predicate grammar sees node names only).
- **I-4 One engine.** One graph loader, one predicate grammar, one availability query, one engine parameterized by
  (tree, pool, target). No `CivicsQuery` twin (R-02).
- **I-5 Mechanisms over modifiers.** Every authored effect names the resolution equation it enters or the entity it
  makes eligible; no free-floating buff; an effect is applied by exactly one system (law 2; R-03).
- **I-6 Age independence of research.** Availability and cost never read the Age; the Age reads research (D-044 R13).
  Completing an Age grants nothing by itself; in particular no government form.
- **I-7 Optional means optional.** A validator proves every Age's CORE milestone set and every macro-civic's
  prerequisites are reachable without any node tagged `optional`; optional branches add depth, never gates (DIRECTED).
- **I-8 Knowledge is monotone and civilization-owned.** Completion sets persist across control changes
  (`KnowledgeTransfer`, `KnowledgeMonotonicTests`); diffusion only adds.
- **I-9 Information fence.** The AI reads the world only through `PolityKnowledge` for anything fog gates; the player's
  surfaces only through `PlayerKnowledge`; `KnowledgeLeakTests*` and the metamorphic `PolityKnowledgeTests` stay green
  with the stub replaced by stored extent (Spine principle 7; ADR-037).
- **I-10 Append-only schema; populated tests; attributed goldens.** New state is appended as new sections; a POPULATED
  table test per row type; every moved golden carries a RE-PIN cause block and an attribution twin (CLAUDE.md; E4).
- **I-11 Determinism.** No `Dictionary`/`HashSet` iteration, no `float`, no `System.Random`; every argmax over doubles
  uses a composite key with an integer tie-break and ships a tie-dense test; any new RNG draw is a registered stream.
- **I-12 Turn-exact delivery.** Every new order kind ships its own stamping pin (issued t, applied t→t+1 or as stated).

### §3.2 DIMENSIONAL DECLARATION (S8 §4.1 requirement 2)

**Anti-self-certification:** for existing quantities the unit is read off the producing expression and cited (T6.1).

| quantity | unit | producing expression / status |
|---|---|---|
| **RP** (Research Points) | RP per STRATEGIC TURN (ADR-030 exception; never × dt) | EXISTING: `RP = 0.08 × pop^0.699`, `ResearchQuery.cs:384-385`; dt-invariance pinned (`ResearchEngineTests.cs:84`) |
| population (RP input) | persons (`long`, every cohort of controlled settlements) | EXISTING: `ResearchQuery.cs:361-375` |
| **BaseCost / EffectiveCost** | RP | EXISTING: `92.4 × 2^magnitude` (`research.json:37-45`); `max(0.2·Base, Base·ΠFactor)` (`ResearchQuery.cs:268-280`) |
| university cost factor | dimensionless ∈ (0,1] | EXISTING: `1 − ⅓(1 − e^(−X))`, X = Σ maturity (`Institutions.cs:233-261`) |
| institution maturity | dimensionless [0,1]; τ in sim-years, integrated with dt | EXISTING: `InstitutionsSystem.cs:62-70` |
| acceleration credit | RP, ≤ 0.4 × BaseCost per node | EXISTING: `ResearchSystem.cs:133-174` |
| **CP** (Civics Points) | **NEW — CP per strategic turn (recommended, by ADR-043 mirroring ADR-030) OR CP per sim-year × dt (law 3)** — DIRECTOR DECISION D-061.2 | PROPOSED: `CP = c_c × f(inputs)`; inputs and exponents are D-061.3; the packet shows `CP_turn` balances on both sides of the credit equation and that no CP enters a predicate |
| civic BaseCost | CP | PROPOSED: the same `U_c × K^magnitude` form; U_c calibrated by a stated turn anchor, not copied from U |
| **civics progress / target** | CP; node key | PROPOSED: `CivicsProgressRow(Polity, Node, Progress: double)`, `CivicsTargetRow(Polity, Node)` — appended sections (I-10) |
| administrative reach | dimensionless [0,1] | EXISTING: `exp(−TravelCost/25)`, `Governance.cs:94-115`; TravelCost in cost units (16 km ideal ground per unit, `LatticeGeometry.cs:36-45`) |
| legitimacy | happiness scale 0..100, derived | EXISTING: `Governance.cs:205-230` |
| effective tax rate | fraction | EXISTING: `clamp(nominal × reach)`, `Governance.cs:147-154` |
| **sight budget** | **NEW — travel-cost units** (16·b km ideal ground; ≈14.4·b km real); a per-source constant or a content field per source class (settlement, formation) | PROPOSED: computed by `Pathfinder.Isochrone(source, budget)` (`Pathfinder.cs:170-217`); the packet must show that a vantage term, if any, enters as a budget multiplier (dimensionless) not an additive km term mixed with cost units |
| **known extent** | **NEW — bits over lattice nodes** (65,536 nodes at the default = 8,192 bytes per polity; monotone OR) | PROPOSED: `KnownExtentRow(Polity, …)` bitset; the unit is "nodes known", never km² unless multiplied by `LatticeGeometry.BlockAreaKm2` |
| exposure (foreign) | RP credited per contact event, ≤ the acceleration pool | PROPOSED writer for `ResearchExposureRow` (M6.21): credit = `w_contact × BaseCost` with w ≤ 0.4 − w_eureka |
| export cell | characters (UTF-8 code points); cap 32,767 per cell (Excel) | EXISTING design: `m6-session-export/README.md:43-55` |
| unit progression | NO quantity in M6 (contract only, D-E/D-F) | fields named as an M7 contract, dimension stated per field, no writer |

Each packet completes its own declaration before implementation; T6.1 is authoritative over every EXISTING row.

### §3.3 CORRIDOR INDEPENDENCE (S8 §4.1 requirement 3) — including arch-PQ Q-26

**Q-26 ("What calibration corridor gates knowledge or diffusion? A milestone that cannot be calibrated cannot pass its
own exit") — answered by refusal and a baseline, not by a new gate.**

1. **No knowledge corridor is an M6 exit criterion.** The only candidates have self-referential shape: "Age A_k reached
   by turn N" where N is derived from the sim's own RP production, or "RP per capita per Age" whose numerator and
   denominator are both sim outputs. By the algebraic test (write bound and measurement as fractions and cancel) the
   sim-measured term disappears; by the counterfactual test the recipe gives a different bound on either side of any
   change to `rp.coefficient`. **REFUSED at spec time** (S8 §4.1 item 3).
2. **The measured baseline that exists is kept non-gating:** ADR-030's research-calibration report (seed 42, 1630
   turns, 696,745 RP = 6.3 % of the 11,059,750 RP finite workload; A7–A9 unreached; `docs/research-calibration-report.md`)
   stays a CI `--check` on the generated document, which gates staleness, not pacing. T6.3 adds the same measurement for
   CP (a CP-rate report), also non-gating.
3. **What a future knowledge corridor must look like, if the Director wants one:** its bound must be an explicit
   formula over named inputs marked *external* (e.g. a historical first-attestation date per Age, from a source outside
   the sim) and *sim-measured* (e.g. the sim-year at which the Age is entered), with NO sim-measured term in the bound's
   denominator. Even then it depends on the Age-to-dt mapping (ADR-030 §4, deferred), which must be ruled first.
4. **Existing corridors M6 perturbs** (both already QUARANTINED, `corridors.json`): `densityPerArableKm2` — the fog
   restriction of settle/claim to known extent changes settlement count and so the corridor's denominator (catchment
   arable); `migrationGrossPerDecade` — unchanged mechanism, but destinations limited by known extent could move it.
   Neither band is re-derived in M6; T6.8 measures both before/after on the 20-seed sweep and records the delta with
   attribution (not a pass/fail).

### §3.4 COUPLING MAP (S8 §4.1 requirement 4) — PROVISIONAL AS AUTHORED; T6.1 AUTHORITATIVE

OUTBOUND: what M6's new systems perturb.

| new system | perturbs | re-anchoring expected |
|---|---|---|
| **Civics Points pool, target, progress (D-061)** | the 7 civics nodes' funding source; `ResearchEngineTests.SharedPool_OneTarget…`; AI tax/university goals (`AiResearchPolicy.cs:137-181`); research panel; snapshot layout (new sections) | ALL goldens (layout) + behaviour where civics completion turns move (tax gate timing → extraction → stores); two conservation pins replace one; attribution twin `TestConfigs.PreCivicsPoints` reproducing the one-pool world byte-for-byte |
| **civics content (micro/macro) and validator** | `research.json` or `civics.json`; Age milestone facts for InstitutionalSocial; `ResearchContentTests` (50) | content tests; `--check` scripts regenerated; no behaviour unless costs move |
| **government-form availability (regime-module data shape, derived label)** | new read-only table or content; UI Institutions lens | layout golden only; no behaviour (M8 adopts) |
| **stored known extent + discovery (S2a)** | every snapshot (new section at v35); founding-seeded rows move all goldens; `PolityKnowledge.Of` reads the extent → every leak test's positive control; AI site valuation (D7 channel closes at S2b) | ALL goldens with attribution (`TestConfigs.PreFog` twin reproducing all-known); `PolityKnowledgeTests.KnownCellsValuations_…Quarantined` flips; `sim bench` before/after |
| **known-extent restriction of settle/claim/trade (S2b)** | `FoundingQuery` arm 2.5; `TradeQuery` mutually-known term; `RoadDevelopmentQuery.Routes` (600 km channel); colonization timing → settlement count → density corridor denominator; migration destinations | behaviour goldens; the two quarantined corridors measured, not re-derived (§3.3) |
| **LIVE/GHOST/UNKNOWN display, sighting memory (S2c)** | UI only + a persisted sighting-memory table if memory is state (D-K) | layout golden if a table; UI tests; Decision A (rival token size) revisited |
| **foreign-exposure writer, diffusion (M6.21)** | research completion timing of contacted polities → every downstream consumer; `ResearchCreditRow` | behaviour goldens with attribution; D-021-style brake if it accelerates |
| **universities / deferred entities (M6.22)** | realized `inst.*` entities acquire owners → labour, services, cost seams | goldens per realized entity; consumer tests |
| **E1 visuals** | Sim.Ui only; no sim perturbation | UI render tests; Director visual gate |
| **one-file export** | Sim.Ui/Sim.Cli writers only; no state | byte-identical round trip goldens from the prototype; no hash change pinned (as `TelemetryTests :19`) |
| **unit progression contract, training placeholder** | content schema + validator only; UI placeholder | none (inert) |
| **AI civics/fog strategy (M6.23)** | AI order streams → every AI-world golden | goldens with attribution; metamorphic tests extended to civics state |

INBOUND (what M6 stands on) is T6.1's scope — `docs/m6-foundations-audit.md`.

### §3.5 ARCHITECTURE BY DOMAIN (what each packet must preserve; equations belong to packets)

**A. Knowledge → eligibility → realization → adoption → active effects.**
- *Knowledge* = a completion row (`ResearchCompletedRow`; civics analogue) owned by the research engine.
- *Eligibility* = a query over completion masks plus polity-scoped predicates (reach, seat, Age exposure, institution
  facts) implemented as query functions, never as Empire-scope `VariableRow`s (arch-PQ Q-03). Owner: `ResearchQuery`
  and `Governance`-style readers. The tax gate chain (`GateOf`: Inert → NeedsKnowledge → NeedsAge → NeedsSeat → Open,
  `Governance.cs:251-302`) is the template every eligibility exposes: a reason for every locked state.
- *Realization* = an entity brought into the world by an owning system (construction `founds`, recipes, activities,
  road classes). Owner: the realizing system. Nothing realizes on completion (I-1).
- *Adoption* = a separate, explicit act with preconditions (civ-progression §5.4). For institutions: founding (exists:
  universities). For governments: **M8** (D-D); M6 ships the data shape (regime-module set per Empire, latched;
  derived GOVERNMENT LABEL never read by a mechanism — arch-FGH) and the Available/Adopted presentation with locked
  reasons, no adoption order, no active effects.
- *Active effects* = coefficients inside named resolution equations or entity eligibility, applied by one system (I-5).

**B. Technology and Civics as distinct domains (D-061).** Two trees, two currencies, two targets, one engine. The
civics graph is content-distinct: its own key space (D-061.4 recommended: `civics.json`), micro nodes (age-appropriate
practices, institutions, policies; many; cheaper) and macro nodes (government forms and broad arrangements; few;
costlier), its own prerequisite grammar instance, cross-domain edges only as explicit "exposure" prerequisites (a civic
may require a technology; a technology may carry a civic Eureka, as `railway` does today). Civics unlock only
institution entities and governance capabilities (existing validator rule, `ResearchContent.cs:1240-1242`). The seven
existing civics migrate to the civics key space and CP pool with their progress carried per D-061.5.

**C. Research progression, Age integration, Eureka/foreign exposure, persistence, diffusion.**
- Progression stays per ADR-029/030: one target per pool; partial progress on every node; completion in index order;
  overflow per Director decision D-R (carry or lose; §7).
- Ages read research; research never reads Ages (I-6). Civics nodes stay Age milestone facts (InstitutionalSocial) —
  reviewed in T6.4 so that no macro civic is an Age CORE milestone unless the Director wants Age entry to require it;
  Age entry never grants a government form (I-6, DIRECTED).
- Eureka: retained (0.40 ceiling). Foreign exposure: T6.10 writes `ResearchExposureRow` from a named contact carrier
  (trade flows / routes within known extent), credited deterministically in settlement-id order, never exceeding the
  shared acceleration pool.
- Persistence: knowledge is civilization-owned and monotone (I-8); revolt copies, annexation unions (existing
  `KnowledgeTransfer`); city-state research continues at `cityStatePaceFraction`.
- Diffusion: T6.10 chooses which of the ledger §12's 13 pathways are M6 (contact and conquest recommended; the rest
  named for M8/M9), each a deterministic, bounded credit, never a completion.

**D. Universities and research modifiers.** The cost seam stays the only effect of universities on research
(`ResearchCostModifiers`); whether scholars or institutions feed RP or CP generation is D-S (§7). T6.11 realizes the
deferred entity tiers by owning systems and decides the fate of the 27 unrealized `inst.*` entities under D-M ("what is
an institution", CR-010) — the six civic institutions (`inst.law_code` …) are the first candidates, with the law court
→ `Unrest.ServiceOffset` slice (D-P) as the proving case.

**E. Research-dependent capabilities and legal-action exposure.** One predicate, two callers: the simulation refuses an
illegal order at delivery (`T15_DirectInjection_…IsRejected` pattern) and `AvailableActionsQuery` lists exactly the legal
ones with provenance; the UI never computes legality itself. T6.15 audits every authored consequence (node, form,
upgrade, capability) to a legal effect or a knowledge-only mark; CI `--check` scripts stay green.

**F. Fog of war, scouting, visibility persistence (S2a–S2c).**
- *Extent* (state, v35): per-polity bitset over the 16 km lattice, monotone OR each step; seeded from founding
  catchments; revolt copies the parent's extent; annexation unions. Owner: a new `DiscoverySystem` (pipeline position
  after Catchment, before Colonization) — no RNG for a deterministic sight flood.
- *Sight* (derived each step, never stored): `Isochrone(source, budget)` from controlled settlements and owned formations
  (D-K); budget in cost units (§3.2); no scout entity in M6 unless D-J rules otherwise (recommended: none; scouts are
  M7's first MobileAgent).
- *Gating* (S2b): `FoundingQuery` known-extent arm; `TradeQuery` mutually-known term; `RoadDevelopmentQuery` routes
  within known extent; the AI valuation channel closed (D-T decides what an AI assumes of unseen terrain).
- *Display* (S2c): LIVE / GHOST / UNKNOWN; sighting memory for settlements and formations (last seen turn, position,
  strength class) — state if persisted across sessions (D-K), owned by the discovery system; a lens-owned Fog layer plus
  a terrain/river cover; DEV panels stay world-wide (D-060); Decision A (rival token size) revisited.
- Invariants: bits never clear (incl. revolt and loss of control); turn-exact discovery pin; `PolityKnowledge.Of` reads
  the stored extent; every leak test green with the stub replaced by extent; `sim bench` within budget.

**G. E1 visuals and the one-file export.** E1 (T6.13): drawn buildings/institutions with count staging, Age-specific
terrain and architecture, authored unit silhouettes within C1–C4; the Age style register's driver per D-L (recommended:
realized state); acceptance = the Director's visual gate on the slice's art first. Export (T6.14): `session-<stamp>.xlsx`
beside the six files, eight sheets, codebook v4, exact number text, size guard and continuation cell, self-check decode,
goldens from the prototype; writer choice per D-G (recommended: hand-rolled minimal OOXML, no new dependency).

**H. Unit progression and the M7 handoff (D-E/D-F ruled).** T6.12 delivers: the promotion-tree and upgrade content
schema with a validator (effects reference NAMED M7 fields as a contract, validated for shape, inert); the repeatable
training / AP contract (dormancy, eligibility, cost, increment, cap, diminishing returns) as data; the settler/hero
design note (no unit objects exist; a settler is an instantaneous order); the training-panel placeholder ("planned for
M7", mutates nothing). `MilitaryUnits` keeps its single writer; kind 10 and v36 stay reserved (`m7-handoff-register.md`).

**I. AI research and civics behaviour under fog (T6.16).** A civics goal lane in `AiResearchPolicy` (the `TaxGoal`
template) targeting CP with composite keys; AI founding, roads and trade under known extent; `AiGovernance` and
`AiResearchPolicy` routed through `PolityKnowledge` for anything extent-gated; metamorphic tests extended: AI orders
independent of unknown cells and of any other polity's civics state; the tax-valve isolation asserted explicitly (C9).

**J. Save/load, replay, determinism, validation, integration, human acceptance (T6.17, T6.18, T6.20).** One ADR per
schema bump; readable set stated; POPULATED test per row; cross-process leg per new order kind; replay twins; the
60-turn and founded goldens moved only with attribution; content validators extended (civics, promotions, optional-branch
reachability); the vertical slice (D-P) is the one mandatory human session with a ≤5-item checklist per packet.

---

## §4. TASK PACKETS — PROPOSED (dependency-ordered; none allocated until ratification)

| packet | scope (register workstreams) | prerequisites | blocking decisions | acceptance (summary) | size |
|---|---|---|---|---|---|
| **T6.1 FOUNDATIONS AUDIT + SPEC** (this) | `m6-foundations-audit.md`; this spec; D-061/CR-023; register updates | M5 exit (done) | spec ratification; D-061.1 | audit table adjudicated; spec ratified by dated ruling; registers truthful | M (done, pending ratification) |
| **T6.2 Research engine reconciliation** (M6.2, M6.3) | one-engine parameterization design; overflow rule (D-R) implemented; capability vocabulary reconciled; stale comments and ADR-029 §3 owner note; `UnlockedCapabilities` strings reviewed | T6.1 ratified | D-R (overflow), D-S (RP inputs) | `ResearchEngineTests`/`ResearchContentTests` green or superseded by dated pins; no golden moves unless D-R changes behaviour (attributed) | M |
| **T6.3 Civics currency and graph** (M6.4 core) | CP pool, civics target (kind 14), civics progress rows; ADR-043; schema bump (D-H); migration of the 7 nodes (D-061.5); two conservation pins; AI civics lane minimal; CP-rate report | T6.2; **D-061 ratified** | D-061.2–7, D-H | populated-table tests; turn-exact pin for kind 14; `PreCivicsPoints` attribution twin; xproc leg extended; goldens attributed | L |
| **T6.4 Civics content and validator** (M6.5) | micro/macro schema; optional-branch reachability validator (I-7); Age-milestone review (I-6); the civics content pass with a design basis per node | T6.3 | D-061.4/8, D-M, D-Q | validators green; every node names its realizing mechanism or is knowledge-only; no macro form granted by Age entry (test) | L |
| **T6.5 Government-form availability** (M6.6) | regime-module data shape; derived label; Available vs Adopted (no adoption) | T6.4 | D-D (ruled), D-M | read-only; layout golden only; UI shows locked reasons | M |
| **T6.6 Civics UI and legal-action exposure** (M6.7) | civics panel/target, CP figure; action surface entries for civic verbs via `AvailableActionsQuery` | T6.3, T6.5 | — | UI equals simulation legality (T15/T16 patterns per verb) | M |
| **T6.7 Fog S2a — extent and discovery** (M6.13a) | `KnownExtentRow`, `DiscoverySystem`, sight isochrone, seeding, revolt/annex rules, v35 + ADR, `PolityKnowledge.Of` on extent | T6.1 ratified | D-K, D-J | bits never clear; turn-exact discovery pin; populated test; `PreFog` twin; leak tests green on extent; bench | L |
| **T6.8 Fog S2b — known-extent gating** (M6.13b) | settle/claim/trade/roads restricted; AI channels closed (D7); corridor deltas measured | T6.7 | D-T | quarantined test flips; AI orders independent of unknown cells; corridor delta recorded with attribution | M |
| **T6.9 Fog S2c — display and memory** (M6.13c) | LIVE/GHOST/UNKNOWN; sighting memory; Fog layer; terrain/river cover; Decision A | T6.7 | D-K (memory as state?) | UI resume reproduces memory; leak tests; Director visual check | M |
| **T6.10 Diffusion and foreign exposure** (M6.21) | `ResearchExposureRow` writer; pathway choice; city-state knowledge rule | T6.2; T6.8 (contact within known extent) | pathway ruling | deterministic credit; AI symmetric; brake if accelerating; goldens attributed | L |
| **T6.11 Universities and deferred entities** (M6.22) | D-M; realization of tiers A–D, G; the 27 `inst.*`; law-court slice | T6.2 | D-M, D-P | each realized entity has an owner and a consumer test | L |
| **T6.12 Unit progression contract** (M6.8–M6.12) | promotion schema + validator (inert); training/AP contract data; settler/hero note; training placeholder | T6.2 | D-E/D-F (ruled) | no `MilitaryUnits` writer; placeholder mutates nothing; schema validated | M |
| **T6.13 E1 visuals** (M6.14) | buildings/institutions with staging; Age terrain; unit silhouettes; style register per D-L | T6.1; T6.9 for GHOST art only | D-L | D-038 Part G at a Director gate; C1–C4 fence; palette/seamlessness tests | L |
| **T6.14 One-file export** (M6.15) | workbook writer; codebook port; size guard; self-check; goldens | T6.1 | D-G | byte-identical decode; Director's 68-turn session exports; no hash change | M |
| **T6.15 Corpus and consequence audit** (M6.16) | every node/form/upgrade/capability → legal effect or knowledge-only; no effect applied twice | T6.4, T6.5, T6.11, T6.12 | — | validator + report; CI `--check` | M |
| **T6.16 AI strategy under fog and civics** (M6.23) | civics goal lane; AI under known extent; metamorphic tests incl. tax valve | T6.3, T6.8 | — | `PolityKnowledgeTests` extended; goldens attributed | M |
| **T6.17 Save/load, replay, determinism** (M6.17) | continuous: ADR per bump; populated tests; replay twins; xproc legs; readable set | each state packet | D-H | all CI legs green on every head | M (spread) |
| **T6.18 Vertical slice and human acceptance** (M6.18) | the D-P slice (law_code → law court → `ServiceOffset`) playable; ≤5-item checklist | T6.6, T6.12, T6.15, T6.17 | D-P | Director plays it; limitations documented | M |
| **T6.19 M7 handoff and deferred work** (M6.19) | `m7-handoff-register.md` complete; deferred register updated | T6.12, T6.7–9 | — | register items each with an owner | S |
| **T6.20 Integration and exit** (M6.20) | merge candidate; 20-seed soak; Windows smoke; exit inventory | all | merge ruling | §5 criteria evidenced | M |
| **T6.21 D-051 carry-over lane** (M6.24: §2 availability classes, §4 primitive toolmaking) | independent lane with its own attribution controls | T6.8 for §2 (overlay data) | CR-020 §A option | battery re-ruled per item | L |

**Critical path:** T6.1 → [ratify spec + D-061] → T6.2 → T6.3 → T6.4 → T6.5 → T6.6 → T6.15 → T6.17 → T6.18 → T6.20.
**Independent lanes (start after ratification, in parallel):** fog (T6.7 → T6.8/T6.9 → T6.16), export (T6.14), E1
(T6.13), units (T6.12), diffusion (T6.10 after T6.2), universities (T6.11 after T6.2), carry-over (T6.21).
**Golden-moving packets** (move once each, early in their lane, with attribution): T6.3, T6.7, T6.8, T6.10, T6.11, T6.16.

---

## §5. EXIT CRITERIA — PROPOSED (nothing is complete because tasks merged or CI is green)

1. The Technology tree is preserved: no node, cost or prerequisite changed without a recorded reason; the 51 live
   consumers unchanged in behaviour; content tests and `--check` scripts green.
2. Civics is a separate domain on its own currency and graph (D-061 ratified and built): two conservation pins; a
   turn-exact civics-target pin; migration of the seven nodes recorded; CP-rate report generated; no currency gates
   anything (I-3 test).
3. Every civic effect names its realizing mechanism; the five-state model is explicit in code and presentation;
   government forms are Available only (M8 adopts); Age entry grants no form; optional branches are provably optional.
4. Fog: stored extent monotone across steps, revolt and loss of control; turn-exact discovery pin; `PolityKnowledge.Of`
   reads the extent; every leak and metamorphic test green with the stub removed; settle/claim/trade respect extent;
   save/load/resume reproduce extent and memory; cross-process leg green; bench within budget.
5. E1: D-038 Part G passed at a Director visual gate; C1–C4 held.
6. Export: every cell decodes byte-identically; the Director's 68-turn session exports; no nondeterminism; no encryption.
7. Unit progression: schema + validator + contract delivered; no per-unit state; placeholder inert; kind 10 and v36
   untouched.
8. Content validation: every authored consequence has a path to a legal effect or is knowledge-only; no illegal action
   reaches the simulation; UI availability equals simulation legality.
9. Save/load, replay, determinism: a populated test per new row type; every golden move attributed; ADR per bump;
   readable set stated; CI incl. Windows smoke and the 20-seed soak green on the candidate.
10. The Director's human acceptance of the vertical slice with documented limitations; the M7 handoff register
    complete; this spec's §7 decisions all RULED.

---

## §6. GOVERNANCE AND HOUSEKEEPING

- Ratification: a dated Director ruling on this spec and on D-061 (CR-023) opens T6.2. Until then: documentation only.
- Living clause: T6.1 findings may move §3.4 rows as LIVING revisions; everything else changes by dated note.
- Housekeeping owned by M6.1 (dated notes, never rewrites): CR-012 header (closed at `:162`), CR-018 header (resolved
  by ADR-030), ADR-034 header (RATIFIED at `:7`) — notes appended by this packet; ADR-029 §3 owner line (noted here, a
  note appended to ADR-029). Stale CODE comments (`ResearchQuery.cs:562`, `PolityKnowledge.cs:27-28`,
  `UnitCardModel.cs:46, 79-80`, `SessionEmitter.cs:12`) are listed for T6.2's first commit — this packet changes no
  `Sim.*` file by the Director's hard constraints.
- Planning package: superseded by this spec on ratification (dated note in `m6-plan/README.md`).

---

## §7. UNRESOLVED DIRECTOR DECISIONS — consolidated, with recommendations

| group | decision | recommendation | alternatives | consequence of each | blocks |
|---|---|---|---|---|---|
| **G1 Civics currency (D-061)** | D-061.1 supersede D-I; .2 CP per-turn by ADR-043; .3 CP inputs (population term with its own exponent + Active-institution term; no RP fraction); .4 separate `civics.json` key space; .5 carry existing progress 1:1; .6 civics takes the next landed schema version (fog's v35 moves by note if later); .7 one civics target; .8 keep civics as Age milestone facts but never CORE for a macro form | as listed (CR-023 option 1) | .2 per-year × dt; .3 a split of RP (refused by the 2026-10-20 instruction); .4 shared key space with a cross-tree fence; .5 reset; .7 multiple targets | per-year CP paces unlike RP across era bands; a split is not a currency; shared key space blurs "genuinely distinct"; reset loses 7 nodes' progress on every resumed session | T6.3–T6.6, T6.16 |
| **G2 Engine semantics** | D-R overflow: carry to the next target of the same pool, capped at one node | lose (today) | carry removes the one-turn-per-node floor and the "RP with no target is lost" surprise; lose keeps ADR-029 R-2 | T6.2 |
| | D-S RP/CP inputs beyond population | M6: none for RP (keep ADR-030); institutions feed CP only | scholars feed RP | scholars-feed-RP double-counts (staff are withdrawn from labour but still counted in RP) | T6.2, T6.3 |
| **G3 Fog** | D-K ratify S2a (bitset v35, monotone OR, sight = isochrone from settlements/formations, budget as one TUNE constant in cost units; sighting memory as state) | memory derived only | derived memory cannot survive a resume; state costs a table | T6.7, T6.9 |
| | D-J scouts: none in M6 (M7's first MobileAgent) | per-polity capability score widening sight | a score is a coefficient with no mechanism behind it (law 2 risk) | T6.7 |
| | D-T AI assumption of unseen terrain: treat unknown as impassable and worthless for valuation (symmetric with the player's `SiteAssessment` withholding) | optimistic default | optimism re-opens the D7 channel | T6.8 |
| **G4 Institutions** | D-M "what is an institution": a founded, hosted, maturing row with one owning system and ≥1 consumer (the university shape generalized); write CR-010 as an ADR in T6.11 | per-type bespoke systems | bespoke systems recreate R-02 for institutions | T6.4, T6.5, T6.11, T6.18 |
| | D-P vertical slice: law_code → law court → `ServiceOffset` | another slice | — | T6.18 |
| | D-Q verb-granting civics before M7: knowledge-only until M7 | seam ruling | a verb with no owner violates the action-surface rule | T6.4 |
| **G5 Delivery** | D-H schema sequencing: "first to land takes the next version", re-reserved by dated note | fixed slots | fixed slots stall the first lane to finish | T6.3, T6.7, T6.17 |
| | D-G export writer: hand-rolled minimal OOXML | library dependency | a NuGet adds licensing/nondeterminism review | T6.14 |
| | D-L E1 style driver: realized state | Age or knowledge | Age-driven art contradicts I-6's spirit for visuals | T6.13 |
| **G6 Research deliverable** | commission the comprehensive Civ VI comparison as a documentation sub-packet T6.1b (no code), inputs: base game vs Rise and Fall vs Gathering Storm, with sources | fold into T6.2 | folding delays the engine packet behind reading | T6.4 content basis |

---

## §8. COMPARATIVE CIVILIZATION VI RESEARCH — STATUS AND A BOUNDED PROVISIONAL TABLE

**Status (MEASURED): no comprehensive Civ VI comparison deliverable exists in the repository.** The only Civ VI material
is D-047 (the Director's rulings using Civ VI as default reference; no side-by-side analysis; no external citations),
`r2a-trade-citystates-record.md:24-31` (the sole cited Civ VI sources, on Foreign Trade and the Trader), and scattered
rejections (policy cards §5.3; Congress §13.4; hex ZOC r17; paid upgrades r18; 3D world D-038 A3). **The commissioned
comparison is therefore an OPEN DEPENDENCY** — proposed as T6.1b (documentation only, §7 G6). Nothing below claims
to be that comparison; it is the spec author's provisional reading from general knowledge, uncited, labelled INFERENCE,
and it changes no scope (any scope change is explicit in §4).

| Civ VI mechanic (base = B; Rise and Fall = RF; Gathering Storm = GS) | causal gameplay role | civDemo disposition (PROPOSED) |
|---|---|---|
| Science and Culture as separate yields feeding separate trees, two active targets (B) | parallel, competing progression with distinct pacing | **ADOPT the shape** (D-061): two currencies, two targets, one engine; NOT yield-from-tiles — CP and RP are population/institution functions |
| Eurekas and Inspirations (B) | reward play that matches the technology; shortcut cost | ADOPTED already for technology (0.40 ceiling, D-047 r10); **ADAPT** Inspirations as civic Eurekas on the civics graph, same pool rule |
| Civics → policy cards slotted into a government (B) | governments as card slots; flat bonuses | **REJECT** (civ-progression §5.3; law 2); civics make institutions and regime modules ELIGIBLE |
| Government types with inherent bonuses; Legacy bonuses (B) | strategic identity; switching cost | **ADAPT**: government = derived label over a latched module set (M8 adopts); no inherent bonuses; switching cost is an M8 mechanism |
| Governors (RF) | localized administration; loyalty | **DEFER** to M8/M9 (notables with political roles); reach already localizes administration |
| Loyalty and Dark/Golden/Heroic Ages, Dedications (RF) | era-score pressure; flip of cities | **ADAPT** partially: the surge (ages.json) is the dedication analogue with NO numeric effect yet; loyalty ≈ reach + unrest; no city flips in M6 |
| Era Score and Historic Moments (RF) | pace of achievement feeds Ages | **REJECT as a scalar** (I-3: no accumulator gates); Age eligibility stays milestone-based (ADR-031) |
| Districts and adjacency; builders (B) | spatial city planning; worker units | **REJECT builders** (D-045); districts out of scope (construction is city-level) |
| Trade routes unlocked by Foreign Trade civic (B) | civics gate economy | ADOPTED already (`r2a`, trade knowledge gate) |
| Diplomatic Favor, World Congress (GS) | diplomacy currency; global votes | **REJECT** (§13.4); diplomacy is M8 |
| Climate, disasters, power (GS) | environmental pressure | partially exists (DisasterSystem, weather); power/climate change **DEFER** (M10+) |
| Great People (B) | recruitable notables with one-off effects | ADOPTED as pattern (D-047 r15/r16: special people = notables), **DEFER** to M7/M8 |
| Unit promotions and upgrades for gold (B) | military progression; paid modernization | promotions: **ADAPT** as content + M7 state (D-E); paid upgrades **REJECTED** (r18: automatic, free) |
| Fog of war, exploration, scouts with sight/promotions (B) | information asymmetry; early-game exploration | **ADAPT**: extent + sight isochrone (S2a); scouts deferred to M7 (D-J); AI plays under the same fog (Spine 7) — unlike Civ VI's AI, which does not |
| Research agreements, tech trading, espionage (B/GS) | knowledge flows between polities | **ADAPT** as diffusion pathways (T6.10: contact credit); agreements/espionage **DEFER** (M8) |

---

## §9. PROVENANCE — per claim

Every §1 line cites its record; every MEASURED figure in §3 cites `docs/m6-foundations-audit.md` row ids (A1–E8) and
the file:line there; the D-061/CR-023 passages are quoted verbatim with line numbers in those files; the planning
package's citations were pinned to `dc83a67` and have drifted in `docs/milestones.md` (e.g. the M7 "CONSUMES" sentence
is now `:87`; the M6 scope paragraph `:110-116`) — this spec cites the current tree.


## Dated note — 2026-10-21 (M6.1 docket; appended, the text above is unchanged)

- **§3.4 coupling map — row added (defect C.1 of `docs/m6-plan/m6-ruling-docket.md`):** *Civics Points pool* ALSO perturbs
  **Technology eligibility**: `legal_code_roman` is an atom of the research stage that opens all five Technology subtrees
  (`Sim.Data/content/research.json:254`; `ResearchQuery.cs:81-94`; `ResearchEngineTests.cs:348`), of `inst.university`
  (`research.json:29068`) and of the A5 CORE milestone (`ages.json:85`); 13 further `inst.*` entities require civics
  (`research.json:28984-29152`). Under D-061 those edges are paced by CP generation. Re-anchoring: T6.3 measures the
  completion turn of `legal_code_roman`, the stage and `inst.university` eligibility on seeds 1, 2, 42, 7 before and after,
  and the AI university goal closure (`AiResearchPolicy.cs:165-178`) spans both pools.
- **§7 G1 revised:** D-061.4 recommendation changes to a SHARED key space with a reserved civics range and a cross-tree
  validator fence (a separate file would require re-keying existing progress/completed rows at migration — a row rewrite
  the append-only readable-set premise forbids, `CanonicalSchema.cs:14-16`); D-061.8 widens to "civic atoms inside
  Technology gates" with the reachability measurement as its condition; D-061.3 and D-061.8 are ruled together.
- **§4 T6.17 (defect C.2):** not "continuous". Persistence coverage is each state packet's own acceptance (I-10, I-12);
  T6.17 is ONE closing packet (fixture resumes, cross-process legs, readable-set statement on the merged tree).
- **§4 T6.5 (D-V, new):** the regime-module shape is CONTENT in M6, not a serialized table, unless the Director rules
  otherwise — a table with no writer is dead state.
- **§7 new decisions:** D-U (commission the cited Civ VI comparison as T6.1b), D-V (above). Full docket:
  `docs/m6-plan/m6-ruling-docket.md`. Nothing in this note is a ruling.


## Dated amendment — 2026-10-22 (D-062; appended, the text above is unchanged)

**Status line.** This spec is **DRAFT, AMENDED BY D-062**. The Director's ruling of 2026-10-22
(`docs/d062-m6-director-final-rulings.md`) authorizes implementation of the slice below; the rest of the spec is not
ratified by it.

**§1 additions (RULED).** 1.18 CP separate from RP, **bankable balance**, spent on civic progression (supersedes 1.17's
PROPOSED status and §2's "no general CP reserve"). 1.19 The research stage is `medicine_hippocratic AND
(geometry_axiomatic OR algebra) AND stamp_seal`; `inst.university` keeps `legal_code_roman`. 1.20 Deposits: ≈ 25 % of
eligible geological deposits are concealed until prospected; placement and quantities unchanged. 1.21 A minimal
Explore/Prospect mechanic is in M6 with separate Construction and Actions menus (supersedes 1.13 in part). 1.22
Technologies may enable or improve discovery. 1.23 Nuclear knowledge progression accepted as a design direction;
no nuclear mechanics in M6. 1.24 Costs may be adjusted to support pacing, each change with a recorded reason (R13).
1.5 (adoption M8) and 1.7 (training/AP contract only; movement M7) are confirmed. 1.14's schema reservations are
superseded: v35 is the M6 slice.

**§2 amendments.** Struck from the fence: "a general RP or CP reserve" — for CP only (RP keeps no reserve). Added to the
fence: an RP↔CP exchange (only on the government-change turn; M8), formation ghost rows and scouts (M7, with
movement), nuclear mechanics, key retirement/merge/consumer-removing splits until the remap seam (KR-2).

**§4 packets authorized now** (contract: `docs/m6-plan/knowledge-redesign/m6-slice-contract.md`):

| Packet | Scope | Acceptance (summary) | ADR |
|---|---|---|---|
| T6.2a + T6.3 | stage gate; Civics Points lane, generation, banking, spending; section 62 | stage holds without the civic and the university still needs it; CP identity `balance = generated − spent` every step on seeds 1, 2, 42, 7; dt-correct accrual; turn-exact civic-target pin; populated-table test; attribution twin; AI two lanes with tie-dense test; UI shows both targets and CP | ADR-043 |
| T6.7-lite + T6.21-lite | deposit concealment (derived); Prospect (kind 15) and Explore (kind 14); DiscoverySystem; fog UNKNOWN/VISIBLE/REMEMBERED; sections 63–66; Actions menu | share of concealed eligible deposits in [20 %, 30 %] on seeds 1, 2, 42, 7; deposit table unchanged; turn-exact pins; ledger-exact costs; no repeat exploit; founding only on known land; metamorphic leak tests; populated-table tests; v34 migration; UI lists Explore/Prospect under Actions and Build Granary under Construction | ADR-044 |
| KR-3 | non-breaking content tranche of the redesigned tree | loader and content tests green; three `--check` docs regenerated; per-change accepted/revised/deferred/rejected table | ADR-045 |

**§5 exit criteria** 2 and 4 are read against the minimal slice: criterion 2's "two conservation pins" become the CP
identity and the RP-loss identity; criterion 4's extent, memory and leak requirements hold for the minimal fog; GHOST
display and scouts move to M7.


## Dated note — 2026-10-28 (D-066; appended, the text above is unchanged)

`docs/d066-m6-closure-rulings.md` records:
- the Director's 2026-10-28 rulings: fog deferral (R1) and the completion of all other M6 obligations (R2);
- the defaults ADOPTED to complete M6.

§5 exit criteria:
- **Criterion 4:** read against the minimal fog, with movement-dependent items deferred (R1).
- **Criteria 7 and 8:** read per D-066 A11 and A13.
- **Criterion 9's** Windows smoke and 20-seed nightly are CI-only.
- **Criterion 5's** visual gate and criterion 10's slice acceptance are the Director's.

Evidence: `docs/m6-exit-inventory.md`.
