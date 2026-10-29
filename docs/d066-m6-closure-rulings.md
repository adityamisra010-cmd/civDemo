# D-066: M6 closure rulings, the fog-of-war deferral, and the defaults adopted to complete M6

**Status:** RULED (§1) / ADOPTED UNDER DELEGATION (§2), 2026-10-28.
- **Source:** the Director's written instructions of 2026-10-28.
  - "M6 closure: execute the remaining work"
  - "M6 Closure: Execute All Remaining Work Except Deferred Fog-of-War" (*"Basically we want to reach a point where we
    can start playtesting m6 properly"*)
- **Precedence:** an explicit Director ruling ranks above D-records, ADRs and the spec (gov-4 §1).
- **Records left unedited:** no earlier record is edited. Each affected record gets an appended, dated pointer note
  (§4).

**Labels:**
- **RULED:** the Director's words, quoted or closely restated.
- **ADOPTED:** an open choice settled by the agent under the decision rules the Director laid down in the 2026-10-28
  instruction:
  - *"IF a choice is a routine implementation detail within scope, THEN choose the simplest deterministic, testable,
    reversible solution and record the rationale."*
  - *"IF multiple options are valid, THEN choose the lowest-risk, most reversible option that preserves compatibility
    and milestone scope."*

  Every ADOPTED item is reversible, and the Director is asked to confirm it at the exit gate. None is presented as a
  Director ruling.

---

## 1. The Director's rulings (RULED, 2026-10-28)

| # | Ruling |
|---|---|
| R1 | **Fog-of-war work that depends on movable units is deferred until movable units exist.** Any fog task needing movable-unit visibility, scouting, sighting updates or movement-dependent information is deferred, and its dependency, owner and milestone are documented. A fog component already implemented and independently testable is preserved with its tests. A component needed to keep current gameplay correct, deterministic and leak-free is completed if that can be done safely without movable-unit mechanics; otherwise its blocker is documented. The deferral does not extend to unrelated M6 work. No movement, Action Points, movable-unit mechanics or M7 battle systems may be built to complete fog. |
| R2 | **Complete all other M6 obligations:** civics and institutions (the vertical slice), knowledge systems, universities and deferred entities, unit progression contracts (contracts and inert placeholders only), E1 visuals, the C# one-file session export, the content/consequence audit, persistence and determinism, governance and the M7 handoff. Routine decisions are made autonomously under the stated rules. |
| R3 | The technology and civics trees are accepted. Their design, content, costs and hashes are not reopened without a specific, evidenced defect. |
| R4 | The research-economy candidate remains a trial and is not adopted into shipped defaults. |
| R5 | M7 follow-up work is deferred: aircraft tuning, AI material-needs research, finite-first research policy, battle mechanics and movement. The existing uncommitted M7 pass is preserved and is not extended. |
| R6 | The comprehensive M6 audit is the final step immediately before the exit gate. Human gates (visual acceptance, manual GUI acceptance, Windows smoke, soak) are never claimed unless performed. |
| R7 | No commits, pushes, merges, tags or releases without separate explicit authorization. |
| R8 | The goal is a build the Director can properly playtest as M6. |

The first instruction also names the slice: *"Use the approved vertical slice in the M6 decision records."* §2 A2
records that reading.

## 2. Open choices settled to complete M6 (ADOPTED; reversible; confirmation requested at the exit gate)

| # | Open item (where) | Adopted | Why this option |
|---|---|---|---|
| A1 | **D-M: what is an institution** (`m6-decision-register.md` D-M) | Option 1: an institution is a founded, staffed, maturing structure of a settlement or polity, following the university lifecycle (`InstitutionRow`, Locked→Mature). It is realized by construction `founds`, and its effects come only through coefficients inside named equations. Institution types become content-driven. | The register's recommendation; it is the shipped meaning; no schema change |
| A2 | **D-P: the vertical slice** (register D-P items 1–8) | The slice as written: `law_code` → law court (`inst.law_code`) → `Unrest.ServiceOffset`; the "codified law" regime module and its derived label (no adoption order); the inert promotion entry shown in the placeholder; "no non-military example" recorded honestly; rejection, UI, persistence. | The Director called it "the approved vertical slice"; it reuses four shipped systems |
| A3 | **D-L: E1 style register** | Option 1: the register is driven by REALIZED state; the Age banner is the only Age-keyed element. One Part G gate for the E1 batch, held at the playtest. | The register's recommendation; Law 4 and D-052 §6 forbid a visual that implies capability |
| A4 | **D-038 H8: parts limit** | A bounded parts count per settlement composite, with an overflow rule that folds the surplus into the count shown at high zoom. The number is set in the E1 conventions doc. | Keeps frame cost bounded; presentation-only, so trivially reversible |
| A5 | **D-Q: verb-granting civics** | Option 1: knowledge-only in M6, consumed by the owning system when it exists | The register's recommendation |
| A6 | **"Optional" (I-7) wording** (`director-directions-2026-10-24.md` §3, §4 item 2) | "Optional" means "not needed for the next Age". The validator proves no next-Age entry fact requires such a node. | It is what the Age sweep (D-063 R2, implemented) already implies |
| A7 | **Diffusion pathway** (m6-spec T6.10; ledger §12; D-046 Part D item 5) | Contact/trade-carried exposure only, written to the existing `ResearchExposureRow` under the existing credit ceiling. Conquest knowledge stays with DW-021 (M7/M8); espionage and the other pathways are later. | The spec's recommendation; uses existing seams; no schema change |
| A8 | **Research-modifier formula** (D-044 R5; D-045 Part D item 3) | The shipped ADR-033 D6 form, Factor = 1 − maxReduction·(1 − e^−X), is the M6 formula. No change. | Already implemented and tested; changing it is not in scope |
| A9 | **Deferred-entity tiers** (`deferred-entity-realization-plan.md`) | M6 realizes only the entities the slice needs (the law court). Every other deferred entity is classified with an owning system and a target milestone, consistent with the M7 charters. No other tier is realized in M6. | Lowest risk; avoids inventing effects for later systems |
| A10 | **Form civics with realized effects** (D-D vs D-063 R1) | Their coefficient effects are KNOWLEDGE effects (D-063 R1). The forms themselves are shown "Available; adoption in M8". | D-063 R1 is ruled; adoption stays M8 (D-062 R3) |
| A11 | **Exit criterion 7** | "No per-unit state" reads as "M6 packets add none". The ADR-055 state is M7 work (D-065 R7). The "v36 untouched" clause is superseded by D-062 R11, since v36 belongs to ADR-049. AP state has no reserved schema version. | Factual reading of the rulings |
| A12 | **The M6 proposal-package tools** (`docs/queue.md` 2026-10-28) | Checks that read the live corpus and live content are reconciled to the live tree. Checks of the superseded proposal graph alone are retired as a historical snapshot (kept in the repository, out of the gate). | The package was a planning input that the shipped tree superseded |
| A13 | **Criterion 8 scope** | Criterion 8 reads against the delivered scope: the slice's regime module (A2) and the promotion schema (§1 R2) are built; full government forms are M8. | Same reading D-062 gave criteria 2 and 4 |
| A14 | **D-G: export format and dependencies** (`m6-decision-register.md` D-G) | The register's recommendation: a hand-rolled minimal OOXML writer on `System.IO.Compression`, with no new package. Entries are STORED (`CompressionLevel.NoCompression`) with a fixed entry order and fixed timestamps, so the bytes are identical on every runtime and OS. | No dependency; deflate output varies with the runtime's zlib, stored entries do not |
| A15 | **Telemetry tag after the M7 pass** | The M7 pass changed the telemetry line shape (`grain.military`, `grain.fodder`, reasons 19–23, 23 goods) under the unchanged `telemetry/v4` tag, which breaks the export README §5.4 rule "one codebook per telemetry schema". The writer now emits `telemetry/v5` with codebook v5. The exporter reads v4 (the Director's 68-turn session) and v5. | Restores the one-codebook-per-schema rule; changes no simulation hash |
| A16 | **Exporter placement** | `Sim.Ui` (the first instruction allows "Sim.Ui or Sim.Cli, never Sim.Core"). It runs at session close and headless as `sim-ui --export-session <manifest>`, which needs no new project and no Sim.Cli→Sim.Ui reference. | Fits the instruction literally; the headless mode follows `--era-preview` and `--frame-cost` |
| A17 | **Continuation-cell grammar** (README §4: "marked continuation cells", undefined) | Defined in the export ADR and codebook v5: a continuation row has an empty column A and a cell that starts with a reserved marker; the decoder joins it to the row above. | The simplest unambiguous rule; tested on a synthetic world of 50+ settlements |
| A18 | **AI valuation of unseen terrain** (ADR-036 §8 (h), ADR-037 §6; quarantined test `PolityKnowledgeTests.KnownCellsValuations_…`) | Closed in M6 under R1's third rule: it is an information leak in current gameplay, and closing it needs no movable units. The AI's valuations read only known terrain, and the quarantined test is inverted to equality. | R1: "a component necessary for current gameplay to remain … free from information leaks … complete it if safe" |
| A19 | **Claim, trade and road planning against the known extent** (slice contract E8; ADR-044 §11; DW-001/DW-006 still say M6) | Stays deferred to M7, as the binding D-062 slice contract E8 ruled; recorded in §3 with dependency and owner. | E8 is a ratified contract; road and trade routing belong with the M7 movement graph |

**Not decided here (recorded, not blocking M6):**
- the `recipe.toolmaking` / `recipe.weaving` baseline claim (D-047);
- the city-state conquered-knowledge rule (`city-state-progression.md`);
- the M7 rulings D1–D10 of the M7 execution report.

## 3. The fog-of-war deferral (R1), item by item

This table is filled from the reconciliation of the live tree; `docs/m6-plan/deferred-work-register.md` carries the
same rows.

| Fog item | Disposition | Dependency | Owner / milestone |
|---|---|---|---|
| LIVE/GHOST rows for rival formations; ghost display | DEFERRED (D-062; R1) | Movable units: a ghost is a last-seen position of something that moves | M7 (movement, D-039 parts A/D/E) |
| Scouts / reconnaissance units revealing land | DEFERRED (D-062; R1) | Movable units and Action Points | M7 (H7-07) |
| Sighting updates from moving formations (sight along a path, sighting memory of units) | DEFERRED (R1) | Movement order kind 10 and per-step positions | M7 (H7-01, H7-07) |
| Claim, trade and road routing restricted to the known extent | DEFERRED (contract E8; ADR-044 §11; A19) | The M7 movement/route graph that these routings share | M7 (DW-001, DW-002, DW-006 moved) |
| Siege and line-of-sight information | DEFERRED | Battle layer | M7 (D-039, D-011) |
| Stored known extent, UNKNOWN/VISIBLE/REMEMBERED, settlement sighting memory, Explore/Prospect | DONE, PRESERVED with existing tests | — | M6 |
| Extent monotone across revolt and loss of control | COMPLETED in M6 (test pin added) | none | M6 |
| Leak gate on production fog (the `KnowledgeStubs` stub removed from the gating legs) | COMPLETED in M6 | none | M6 |
| AI valuation of unseen terrain | COMPLETED in M6 (A18) | none | M6 |
| Discovery-layer bench measurement | COMPLETED in M6 (ADR-044 dated note) | none | M6 |

## 4. Records receiving an appended pointer note

- `docs/m6-spec.md`;
- `docs/m6-plan/m6-decision-register.md` (D-L, D-M, D-P, D-Q);
- `m6-scope-register.md`, `m6-task-register.md`, `m6-risk-register.md`;
- `deferred-work-register.md`, `m7-handoff-register.md`;
- `docs/m6-plan/knowledge-redesign/director-directions-2026-10-24.md` (§4 item 2).

---
## Dated note — 2026-10-28 (M6 exit audit: §3 aligned with the registers; M6.24)

**§3 owners and reasons, one label per row**, carried identically by `deferred-work-register.md` and
`m7-handoff-register.md`:

| Fog item | Reason deferred | Dependency | Single owner |
|---|---|---|---|
| LIVE/GHOST rows; ghost display | R1: a ghost is the last-seen position of something that moves | M7 movement (kind 10) | M7, H7-07 (reconnaissance and ghost display); H7-01 is a dependency, not an owner |
| Scouts / reconnaissance | R1 | Movable units and Action Points | M7, H7-07 |
| Sighting from moving formations | R1 | Per-step positions | M7, H7-07 |
| Claim, trade **and road** routing on the known extent | A19 (slice contract E8; ADR-044 §11) | The M7 route graph | M7, DW-001/DW-002/DW-006 (route graph), owner M7 movement (H7-01) |
| Siege and line-of-sight information | R1 (D-039 part D is siege, battle layer) | The battle layer | M7, H7-04 (battle resolution) |
| Discovery-layer bench | (not deferred: COMPLETED) | — | — |

**M6.24 (D-051 §2/§4 carry-over)** is added to §2's "Not decided here" list. D-N assigned these items to M6, but DW-036
records that the Director deferred the §4 mechanism and milestone "until after the playtest", and the §2 richness classes
have no ruled mechanism either. NOT IMPLEMENTED. A Director ruling is required.
