# M6 scope register and milestone plan

**Status: PROPOSED (planning input, 2026-10-20, tree `dc83a67`). Not the M6 spec.** This register states what M6 is by
the authoritative record, what the Director's 2026-10-20 instruction adds, where each addition sits against the frozen
allocation, what is excluded, and what "M6 complete" means. Every allocation line cites its source; every proposal is
marked PROPOSED and, where it needs the Director, points at `m6-decision-register.md`.

## 1. The authoritative definition

- `docs/milestones.md:23` — "**M6** | **Knowledge / Research / Technology** | NEXT (large)".
- `docs/milestones.md:51-52` — "M6 establishes the authoritative Knowledge → Capability layer and its vocabulary."
- `docs/milestones.md:74-83` (roadmap level; "detailed rules belong to the M6 spec") — "the Technology and Civics
  trees; research points (RP) and progression; prerequisites; Eureka / foreign-exposure acceleration; persistent
  civilization-owned knowledge; capability acquisition; research-gated actions; realization requirements and the
  capability → realization chain; knowledge diffusion; universities and research modifiers; Age integration;
  tech-dependent economic, infrastructure and military capabilities; corpus gameplay audit; automatic propagation of
  authored consequences into legal gameplay; simulation-side rejection of illegal actions; UI exposure of legal actions;
  save/load/replay/determinism coverage; tech-dependent content and assets."
- `docs/milestones.md:85-88` — the research engine (ADR-029/030), Ages (ADR-031), the unlock pipeline and universities
  shipped in M5 as "early work, not completion … M6 owns that work and will review, reconcile and complete it."
- Added by Director rulings: fog of war, scouts and visibility persistence (D-052 §5; `milestones.md:35`); the E1
  visual milestone (D-052 §6; `milestones.md:32-34`); the one-file session export (`milestones.md:40-43`; ruled
  2026-10-20 as a size-reducing encoding beside the six files, `docs/m5-run-recording-audit.md` §9).
- Boundaries: M7 Battle Layer "CONSUMES M6's authoritative technology/capability state. No parallel tech vocabulary …
  no 'generic battle now, tech later'", and owns movement, Action Points and recruitment (`milestones.md:36, 53-58`;
  D-052 §4). M8 Politics / Diplomacy "consumes governance, knowledge and military state and FEEDS BACK"
  (`milestones.md:59-61`); the Spine places "Politics deep (institutions, regime change) … composable modules, not enum
  governments" at the politics milestone (`civ-sim-architecture-v3-outline.md:85, 134`).
- Governance of the spec: S8 §4 (written after the M5 exit gate; the four §4.1 items; `spine-s8:48-52, 72-188`;
  CLAUDE.md:28-29). The spec's base commit must be stated and it builds on unmerged M5 work until the merge.

## 2. What exists today (MEASURED on `dc83a67`) — the baseline M6 reviews, reconciles and completes

| Area | Built (PRODUCTION on the branch) | Seam / placeholder | Not built |
|---|---|---|---|
| Research corpus | `research.json` (`civ-sim/research@2`): 433 nodes on one key space — Technology tree 426, **Civics tree 7** (law_code, legal_code_roman, census, coined_wage, patent, joint_stock, taxation); 135 entities (building 43, institution 28, infrastructure 25, unit 23, activity 9, recipe 4, project 3); loader validators (acyclicity, depth, orphans, Eurekas, unlock kinds, "immediate effects must be empty — law 2") | `UnlockedCapabilities` strings (display only) | civics beyond the 7 nodes; adoption of a civic (ADR-029 §14 item 4) |
| Engine | `ResearchSystem` (RP per turn = 0.08·P^0.699, ADR-030; one target; partial progress; completion = eligibility only, D-044 R11/R14); `ResearchQuery.{IsAvailable, EffectiveCost, TargetOrder, IsKnowledgeEligible, Holds, RequirementMet, CheapestAvailable}`; Eureka credit | `ResearchExposureRow` + `AccelerationSource.ForeignExposure` — no writer | diffusion (13 pathways listed in the ledger §12, "deferred"); repeat mechanics of the 10 recursive nodes (D-046 G4) |
| Unlock pipeline | one predicate, two callers: construction projects (granary, workshop, university), road classes, recipes (pottery, weaving, bronze, toolmaking), trade, wild-food activities, the tax gate (`taxationRequires`), Age facts — 51 nodes gate a live consumer | `IsKnowledgeEligible` header text stale ("no system consumes this answer yet") | 262 + 70 nodes knowledge-only; 27 of 28 `inst.*` entities unrealized; the 115 deferred entities (tiers A–G) |
| Ages | 9 ages, milestone categories incl. InstitutionalSocial (civics nodes are Age milestones: a5_systematic_law CORE = legal_code_roman); explicit `AdvanceAge` order; six surges stored; modernization conversion log | surge numeric effect (DD-13: none applied) | — |
| Institutions | universities only: `InstitutionRow`, lifecycle Locked→Available→UnderConstruction→Active→Mature, `InstitutionsSystem` writes research cost modifiers, staffing brake, `ServiceOffset` reads maturity × reach | — | every other institution type ("follow the same lifecycle later"); CR-010 "what is an institution" never written |
| Governance | tax edict as policy (`TaxPolicyRow`, kind 5, `GovernanceSystem`); authority = administrative reach (`ControlRow.Strength` = exp(−travelCost/25) from the capital); gate chain civic knowledge ∧ Age ∧ seat → `CanLevyTax`; legitimacy = derived happiness mean (no feedback by design); unrest-lite, revolt | — | budget/treasury (CR-008), authority/bandwidth economy, laws-lite, government forms, legitimacy as a stock, ruler/succession, elections, verb-freeze matrix (D-019, M8) |
| Knowledge seam | `PolityKnowledge` (ADR-037): all-known today; `Known/Visible/KnowsSettlement/KnowsUnit/ReadsInternals`; leak-gated | inert known-extent arms in `FoundingQuery`, `AiFoundingPolicy` | stored known extent (S2a, v35 reserved), discovery system, scouts, LIVE/GHOST/UNKNOWN display |
| Units | `MilitaryUnitRow(Id, Owner, Family, Identity, Location, X, Y, Experience, Army)`; 12 families / 66 identities; free Age modernization (ADR-031); `UnitCardModel` honest "not simulated" lines | `Experience` preserved 0.0 (nothing earns it); `Army` int, no table | movement, AP, recruitment, promotions, training, settler/hero units, notables with a writer |
| Visuals | the composed tier: `SettlementComposition` (P12a), `UnitEmblem` (P11), era hand tokens (P13), parchment substrate (merged `c655a6d`) | — | E1: drawn buildings/institutions with count staging, Age terrain, authored unit silhouettes |
| Session record | six plain files; forensic close record with unkeyed SHA-256 | — | the one-file workbook; the C# codebook port |
| AI | `AiResearchPolicy` goal-sequential (core Age goal, road, tax, university); `AiGovernance`; `AiFoundingPolicy` reading the whole map (accepted until known extent) | — | civics/government strategy; information access under fog |

## 3. In scope for M6 (PROPOSED, with the allocation each line rests on)

| ID | Scope item | Allocation basis | Workstream |
|---|---|---|---|
| S-01 | Review, reconcile and complete the research engine, Ages, unlock pipeline, universities | `milestones.md:85-88` (RECORDED) | M6.2, M6.21, M6.22 |
| S-02 | The Technology and Civics trees as the authoritative knowledge vocabulary; prerequisites; RP progression | `milestones.md:76` | M6.2, M6.4 |
| S-03 | Civics as a separate progression domain with its own content and semantics — node identity, cost, prerequisites, partial progress, completion, unlocks, branching — on the ONE shared RP pool | D-044 R12 (`d044:360-383`, RATIFIED); D-046 G5 | M6.4, M6.5 |
| S-04 | Civic completion → governance capability → institutional eligibility → adoption as a separate act with its own conditions (Unresearched → Researched/Available → Adopted/Active) | civ-progression §5.4 (DIRECTOR RULING); arch-O II.4; D-042 §5 | M6.4, M6.6 |
| S-05 | Government forms as named bundles of composable regime modules with a DERIVED label — availability made by civics in M6; adoption/regime change per Decision D-D | Spine `:85` "composable modules, not enum governments"; D-042 §5.3; arch-FGH §2.1 (design) | M6.6 |
| S-06 | Realization of civic consequences through existing mechanisms only (institution lifecycle, construction `founds`, the governance gate chain, coefficients inside named equations) | law 2; D-040 B3; D-047 r7/8; ADR-019 §9.3 | M6.3, M6.16 |
| S-07 | Eureka / foreign-exposure acceleration; persistent civilization-owned knowledge; knowledge diffusion | `milestones.md:77-79` | M6.21 |
| S-08 | Universities and research modifiers; the deferred research entities by tier (A–D, G); the 27 unrealized `inst.*` entities' fate | `milestones.md:80`; `deferred-entity-realization-plan.md` | M6.22 |
| S-09 | Age integration (civics nodes as Age milestones already; what M6 adds is reviewed against CR-017 §1) | `milestones.md:80` | M6.3 |
| S-10 | Research-gated actions, simulation-side rejection, UI exposure of legal actions (one predicate, two callers) | `milestones.md:81-82`; `AvailableActionsQuery` rule | M6.7, M6.16 |
| S-11 | Corpus gameplay audit; automatic propagation of authored consequences; no effect applied twice | `milestones.md:81` | M6.16 |
| S-12 | Fog of war: stored known extent (S2a), discovery, scouts (D-039 B6 ruling), sighting memory, LIVE/GHOST/UNKNOWN, known-extent restriction of settle/claim/trade, ADR-037 channels | D-052 §5 (RATIFIED) | M6.13 |
| S-13 | Visual milestone E1 | D-052 §6 (RATIFIED); D-038 Part G | M6.14 |
| S-14 | One-file session export (workbook beside the six files; codebook v4; no encryption) | `milestones.md:40-43`; `m5-run-recording-audit.md` §9 | M6.15 |
| S-15 | Save/load, replay, determinism coverage for every new table; schema bumps by ADR | `milestones.md:82`; CLAUDE.md:38 | M6.17 |
| S-16 | Unit progression: DESIGN, content schema, validation and the M7 interface contract; a training-panel placeholder — no per-unit state, no AP (Decision D-E / D-F) | D-052 §4; D-011; `milestones.md:53-58` | M6.8–M6.12 |
| S-17 | AI strategy for research and civics; AI information access under fog (Spine principle 7) | `m5-ai-constitution.md`; ADR-037 §6 | M6.23 |
| S-18 | Conditional: D-051 §2 availability classes, §4 primitive toolmaking, §1(a), §6-remainder — only if closure ruling R2 assigns them | D-052 §15 bullet 2 | M6.24 |
| S-19 | Housekeeping: stale status headers, stale code comments, CR-005 close-out, arch-N numbering note | gov-4 dated-note rule | M6.1 |

## 4. The 2026-10-20 instruction's items against the frozen allocation

| Instruction item | What the record says | Proposed M6 handling | Decision |
|---|---|---|---|
| M6.4 "Civics uses Civics Points … do not reuse Research Points" | D-044 R2 (Director verbatim): "There is ONE shared Cognitive Load Point pool … Do not create a second accumulated research currency"; R12 "Do not create Culture Points"; D-046 G5 "exactly one shared RP pool"; ledger §26 prohibited reintroductions 2–4; the pool question is ★ do-not-ask-again (D-044 Part B) | Keep the one pool as built; civics nodes already cost RP; adoption is a predicate, not a price. Any second currency is a Director supersession with its own ADR | **D-I** |
| M6.6 government adoption, active government, government-specific effects, transitions | Regime change / institutions as modules = politics milestone (Spine `:85, :134` → M8 after the rebase; D-011 §6); M6 scope names the Civics TREE, not government forms | M6 builds availability (civics make a module set eligible) and the regime-module/derived-label data shape; adoption action and active effects are M8 unless the Director prices an override | **D-D** |
| M6.8–M6.11 per-unit progression, promotion trees, training, settler/hero progression | Units/formations owned by D-011 (frozen); `Experience` has no earner before M7 battle; no settler or hero unit exists (settler = instantaneous order, ADR-036; special people D-043 B3 ratified, CR-017 §7 OPEN) | Design + content schema + validator + contract; no `MilitaryUnits` writer, no new per-unit table in M6 | **D-E** |
| M6.10 repeatable training raising max Action Points | AP/Action Capacity is M7 (D-052 §4); CR-017 §3 must be ruled before any AP packet; S3b plan derives AC max from content | M6 writes the contract (dormancy, eligibility, cost, increment, cap, diminishing returns) as data and validation; M7 implements state and action | **D-F** |
| M6.12 training UI placeholder | no governing text; the `UnitCardModel` honest-UI pattern | a labelled, inert entry point | — |
| M6.13 fog | M6 by D-052 §5; storage/granularity undesigned by ruling (D-040 B5); S2a plan (bitset, v35) unratified | ratify the S2a shape in the spec | **D-K**, **D-J** (scouts) |
| M6.14 E1 | M6 by D-052 §6; H8 and the Age style register unruled | plan; gate on the slice's art | **D-L** |
| M6.15 export | M6; ruled 2026-10-20 | build to README §5 | **D-G** (xlsx dependency) |
| "the existing technology tree already exists — no replacement" | matches: two trees on one key space, loader is the authority | M6.2 names the reusable types; nothing is rewritten | — |
| "no Builders" | D-045 §1 "There is NO Builder unit … Do NOT create a Builder MobileAgent" (RATIFIED) | matches | — |
| "no encryption" | `m5-run-recording-audit.md` §9 | matches | — |

## 5. Explicit exclusions (out of M6 unless a Director ruling moves them)

- Movement, Action Points / Capacity state, right-click move, recruitment, headcount, battle resolution, the 23 `unit.*`
  entities, the functional training panel — **M7**.
- Government adoption as a political action, active-government effects, regime change, coups, elections, legitimacy
  as a stock, capital succession, diplomacy, the D-019 verb-freeze matrix — **M8** (Decision D-D may move the adoption
  action with a priced override).
- Finer population segments — **M9**. Clone architecture (ADR-020) — bites at M8/M9.
- Builders / worker units; worker-based improvement placement — never (D-045 §1).
- A second research currency, Culture Points, a general RP reserve, a second per-turn exception mechanism — not
  without superseding D-044 R2/R12 and D-046 G5 (Decision D-I).
- Encryption of session files; replacing the six files with one container — not without a threat model and an ADR
  (`m5-run-recording-audit.md` §8 option 3 refused by the 2026-10-20 ruling).
- A policy-card model or "spend points → bonus" (civ-progression §5.3; law 2).
- Any change to goldens, canonical hashes or the schema outside an ADR with attribution controls.

## 6. Existing / unfinished / new / deferred — kept apart

| Class | Items |
|---|---|
| Existing functionality (keep; audit) | two trees, RP engine, 51 live unlock consumers, Ages, universities, tax gate chain, knowledge seam, composed visual tier, six-file record |
| Unfinished (M5 early work M6 completes) | diffusion (seam), repeat mechanics, 27 `inst.*` entities, deferred entities A–D/G, `UnlockedCapabilities` strings, stale comments |
| New in M6 | civics adoption model and content, regime-module data shape, corpus validator for civics, fog storage/discovery/scouts/display, E1 art, the workbook export, unit-progression design and placeholder, AI strategy |
| Deferred out of M6 | everything in §5; D-051 carry-over unless R2 says otherwise; items in `deferred-work-register.md` groups 2–4 |

## 7. Milestone acceptance criteria (PROPOSED for the spec's §5; nothing is complete because tasks merged or CI is green)

1. The existing technology tree is preserved and integrated: no node, cost or prerequisite changed without a recorded
   reason; `ResearchContentTests` and the content audit scripts green; the 51 live consumers unchanged in behaviour.
2. The one RP pool and one target stand (or a dated supersession says otherwise); the two-layer civic model is content
   with validated prerequisites, costs and effects; every civic effect names its realizing mechanism.
3. Government forms exist as derived labels over module sets; availability follows civics; adoption is a separate state
   (if in M6 by D-D) with simulation-side rejection and a turn-exact delivery pin per new order kind.
4. Unit progression: content schema + validator + M7 contract delivered; no `MilitaryUnits` writer added; the training
   placeholder states "planned for M7" and mutates nothing.
5. Construction stays city-level; no Builder anywhere (source guard).
6. Fog: stored extent monotone across steps, revolt and loss of control; turn-exact discovery pin; the production
   `PolityKnowledge.Of` reads the stored extent; every `KnowledgeLeakTests*` and `PolityKnowledgeTests` green; AI
   orders independent of unknown cells; save/load and resume reproduce extent rows; cross-process leg verified.
7. E1: D-038 Part G passed at a Director visual gate; C1–C4 fence held; palette/seamlessness/regenerability tests.
8. One-file export: every exported cell decodes byte-identically; size guard and continuation cells tested; the
   Director's 68-turn session exports and re-imports; no new nondeterminism; no encryption.
9. Content validation: every authored node/form/upgrade/capability has a path from definition to legal effect or is
   marked knowledge-only; no illegal action reaches the simulation; UI availability equals simulation legality.
10. Save/load, replay, determinism: a POPULATED-table test per new row; golden moves attributed; replay twins green;
    schema bumps each by ADR; readable set stated.
11. CI green incl. Windows smoke; the 20-seed soak green; human acceptance of the vertical slice by the Director with
    documented limitations; the M7 handoff register complete.

## 8. Where the documents go

- The spec: `docs/m6-spec.md` (convention `docs/m2-spec.md`, `m3-spec.md`, `m4-spec.md`), written after the M5 exit
  gate, with §1 decisions closed · §2 scope fence · §3 system notes (dimensional declaration, coupling map, corridor
  independence) · §4 task packets (`T6.1` foundations audit first) · §5 exit criteria · §6 governance.
- Decision records stay `docs/dNNN-*.md`; ADRs/CRs in `docs/adr/`; this folder is planning input and is superseded by
  the spec on ratification (a dated note here will say so).
- `docs/milestones.md` gets the M6 entry at the M6 exit, and dated notes only before that; `docs/current-state.md`
  carries the dated entry for this package; `docs/queue.md` is for "a better way exists" lines, not for rulings.


## Dated note — 2026-10-20: destinations ruled (closure rulings R2, R4)

S-18 is no longer conditional in part: D-051 **§2** and **§4** are M6's (M6.13 coordination and the M6.22 realization lane;
M6.24 narrows to those two); **§1(a)** is M9's and **§6-remainder** M10's. Laws-lite and the authority/bandwidth economy
enter the M6 civic specification (M6.4/M6.5) subject to architecture review; budget/treasury stays unassigned pending a
definition. D-I, D-D, D-E, D-F are ruled (see `m6-decision-register.md`). Record: `../m5-exit-inventory.md` §13.
