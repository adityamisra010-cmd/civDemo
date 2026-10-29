# D-067: M6 exit-gate rulings: A1–A19 settled (confirmed or ruled), migration accepted, ADR-055 and EC-1/2/3 ratified, M6.24 deferred (Director, 2026-10-28)

**Status: RULED.** Recorded from the Director's instruction of 2026-10-28 ("Mission: Take M6 from integrated worktree to formally closure-ready", §2 "Director decisions to apply").
- **Basis of the instruction:** the Director decision brief prepared read-only from tree `c05878de` (session scratchpad `m6-director-decision-brief.md`, not a repository file).
- **What this record does:** it adds rulings. It edits no earlier record (gov-4 §6). Where it changes the meaning of an earlier record, that record gets an appended, dated pointer note.
- **Precedence:** an explicit Director ruling ranks above D-records, ADRs and the spec (gov-4 §1).

**What this record does NOT do:**
- It does not close M6. The formal exit ruling is still the Director's.
- The `CLAUDE.md` current-milestone line is unchanged.
- No commit, push, merge, tag or release is authorized.
- No gameplay behaviour is changed. Every ruling below is governance, scheduling or documentation.

Labels used below:
- **RULED:** the Director's decision.
- **CONFIRMED:** an ADOPTED default of D-066 §2 that the Director now confirms. Its D-066 text stays as written; corrections are listed here.

---

## 1. The D-066 defaults A1–A19

| # | D-066 item | Ruling | Corrections to the D-066 text (D-066 itself stays unedited; see its dated note) |
|---|---|---|---|
| R1 | A1, A2, A5, A10, A11, A12, A15, A16, A17, A18 | **CONFIRMED** as adopted. | none |
| R2 | A3: the E1 style register is driven by realized state | **CONFIRMED**, with the corrected statement. | "The Age banner is the only Age-keyed element" is too broad. The map's era theme (ink, paper tones, stroke style; `MapInk.For(EraTheme)`, `Sim.Ui/World/MapInk.cs:42`) is also keyed by Age. That is pre-existing presentation and implies no capability. The ruled meaning: no E1 element implies a capability the polity has not realized (Law 4; D-052 §6). |
| R3 | A4: parts limit | **CONFIRMED.** The cap of eight parts (`parts.maxParts` = 8, `Sim.Ui/UiContent/world/settlement-classes.json:36`) is **provisional until the D-038 Part G visual acceptance gate.** | The rationale "7 kinds + 1" is stale. With the law court there are eight part kinds (2 structures, 5 university types, the court), so the cap leaves no headroom and the overflow fold does not trigger with today's content. The claim that the cap "keeps frame cost bounded" is unsupported: the E1 frame-budget overrun (20 of 48 rows) comes from terrain marks, not parts. |
| R4 | A6: "optional" = not needed for the next Age | **CONFIRMED.** | The validator *checks* this at load time. It does not *prove* it, because it does not model the minimum-categories rule. Its scope is the 33 government forms; no "regional node" category exists in content. |
| R5 | A13: scope reading for the government-form criterion | **CONFIRMED.** | A13's subject (the slice's regime module; forms "Available — adoption in M8") is m6-spec §5 **criterion 3**, not criterion 8. Criterion 8 (content/consequence validation) is judged on its own evidence. Where `m6-exit-inventory.md` and ADR-057 cite "A13 (criterion 8)", read "criterion 3". |
| R6 | A14: export format | **CONFIRMED.** | The writer does not use `ZipArchive` / `CompressionLevel.NoCompression`. It is a hand-written STORED ZIP writer (`StoredZip`, `Sim.Ui/Export/OoxmlWorkbook.cs:358`), because `ZipArchive` stamps the host OS into each entry (measured, ADR-059 §3). Byte identity across operating systems is **PREDICTED**; nothing has run on Windows. |
| R7 | A7: diffusion pathway | **RULED: diffusion through trade contact only.** Conquest knowledge (DW-021) and every other pathway (espionage, proximity, migration-borne) stay deferred. | It is not literally "the spec's recommendation": the spec recommended contact *and* conquest. A7 adopted the contact half. |
| R8 | A8: research-modifier formula | **RULED: the shipped ADR-033 D6 university discount formula is retained provisionally, pending calibration.** | none |
| R9 | A9: deferred-entity tiers | **RULED: only the law court is built in M6.** Every other deferred entity is *classified*, not realized. "Universities and deferred entities" (D-066 R2) reads as "classified with an owning system". | D-066 says each entity is classified "with … a target milestone", but some rows in `deferred-entity-dispositions.md` read "unscheduled". Read those as "owning system named; milestone unscheduled". |
| R10 | A19: claim, trade and road planning against the known extent | **RULED: the known-extent restrictions on claim, trade and road routing are deferred to M7.** This explicitly **supersedes the M6 assignment in DW-001** (the Director's words of 2026-10-12: "the known-extent restriction of settle, claim and trade" were M6 work). It is a governance decision. It does **not** authorize implementing those mechanics now. | A19 cited slice contract E8 as "ratified". E8 is the coordinator's design document ("binding for the implementation agents", `docs/m6-plan/knowledge-redesign/m6-slice-contract.md:1`), not a Director ruling. This R10 is the ruling. The founding predicate's reads of unseen land (DW-002) stay assigned to M7, as D-066 §3 records. |

---

## 2. Migration (the `migrationGrossPerDecade` corridor) and future migration policy

| # | Ruling |
|---|---|
| R11 | **The lower observed migration baseline is ACCEPTED as an intentional design acceptance. It is NOT a causal explanation.** |
| R12 | **Optional causal-attribution runs are deferred and are not an M6 exit gate.** |
| R13 | **M8 (Politics / Diplomacy) owns the future migration-policy design.** |
| R14 | **Misleading node text** (e.g. `frontier_fortresses`, `manorialism`, `marches`, `refugee_law` describing movement controls the simulation does not have) is left for the M8 content/design pass. No migration control is implemented in this pass. |

**R11 in detail:**
- The historical corridor band [0.001, 0.01] and window [0.0009, 0.01] are kept unchanged as **historical records, not tuning targets**. No migration constant, band or window is tuned or re-cut to meet them.
- The corridor keeps reporting and does not gate (T3.12).

**What is MEASURED:**
- The drop happened **during M5**, between the M5 hardening reading of 2026-10-05 (0.000430–0.000914, 1/20 seeds in window; `m5-hardening-measurements.md:227`) and the D-053/D-054 batch measurements. It is already visible in batch 2 (2.0103e-4–3.4869e-4); batch 3 measured 1.7256e-4–3.5233e-4 (`d054-batch3-incremental-construction-allocation-cylinder.md:680`).
- The integrated M6 tree reads 1.8083e-4–3.5084e-4 (20 seeds, 650 turns, `TestResults/validation/m6-soak/`). That is essentially the M5-exit envelope.
- **The M6 integrated tree did not cause the drop.**
- The earlier exit-inventory wording ("fell further … candidates are the M7 pass and the M6 closure") is withdrawn; see the `m6-exit-inventory.md` dated note.

**What is HYPOTHESIS:** D-051 §1(b) (time-averaged harvest) is the leading suspect. It has not been measured on the corridor's canonical world. The M4-era decline is unattributed.

**R13 in detail:** the migration lever already has an M8 owner. D-021 valve 3 ("closing exits is a *policy choice*",
`d021-stability-doctrine.md:28`) is deferred to M8 in DW-026. The migration-policy design attaches to that valve and is
recorded as DW-077 (`deferred-work-register.md` dated note). D-021's M5 "policy hooks" (`:39`, `:50`) are
*class-mobility* levers (promotion flows), not migration controls; this ruling does not touch them.

**Design direction, NOT implemented behaviour.** M8 starts from this direction and may change it. All Age labels below are content Ages.

| Stage | Ages | What becomes possible |
|---|---|---|
| Basic | from **A3** | Entry and exit controls, where kingship first appears (`sacral_kingship` 1018, `territorial_kingship` 1023, `overkingship` 1243 are A3). |
| Richer | **A5–A7** | More policy options, consistent with the Director's intent that control deepens "around Age 5 or 6". |
| Permits | around **A8** | Entry permits. |
| Treaties | around **A9** | Treaty regimes between civilizations. |

Today, migration is free between any two settlements across polity lines in every Age. Research effects can only raise movement (`Sim.Core/State/KnowledgeEffects.cs`; operand contract `KnowledgeOperands.cs`), so restriction needs a new mechanism and an ADR in M8.

---

## 3. ADR-055: M7 recruitment, personnel and re-equipment

| # | Ruling |
|---|---|
| R15 | **ADR-055 is RATIFIED, after the documentation amendments recorded in its amendment log (2026-10-28).** |

**Scope beyond the original list.** The Director explicitly acknowledges that the integrated tree implements more than the literal D-065 R7 list, and accepts it:
- the general **Recruit** and **Disband** orders (order kinds 16 and 18, beside Re-equip, 17);
- **soldier upkeep** (grain per person-year, drawn every step);
- **AI recruitment enabled** (`aiRecruitment` true).

**Known consequences, accepted as they stand:**
- **Soldiers remain immortal until M7.4.** Formations have no attrition (ADR-055 §4).
- **The founding warband's population draw changes early history.** The warband's personnel are drawn from the capital at founding by `Ledger.Transfer`.

No new behaviour is to be invented to remove these limitations.

---

## 4. ADR-056: the M7 economy packets (D-065 R2: separate, individually reviewed)

| # | Packet | Ruling |
|---|---|---|
| R16 | **EC-1, metals and tools** | **RATIFIED**, after the ADR-056 amendments. |
| R17 | **EC-2, mounts (horses)** | **RATIFIED**, after the ADR-056 amendments. |
| R18 | **EC-3, gunpowder and explosives** | **RATIFIED**, after the ADR-056 amendments. |

**Recorded with R16–R18:**
- **Mostly dormant content:** the packets are mostly dormant until their knowledge gates are reached in play (ADR-056 §10).
- **Known limitation:** a snapshot written before the packets loads with no stock row for a packet good at its founding settlements (ADR-056 §5).
- **No research nodes are added.** The AI research-progression issue is not silently fixed here; it stays the open finding of ADR-056 §10.

---

## 5. M6.24 / DW-034 and DW-036: the D-051 §2 and §4 carry-over

| # | Ruling |
|---|---|
| R19 | **Deferred until after the Director's M6 playtest. No milestone is named.** |

**What R19 defers:**
- **D-051 §2:** the remaining common-material floors and the regional richness classes (§2(a) floor, §2(b) classes). This is DW-034.
- **D-051 §4:** primitive stone-and-wood toolmaking, and its unresolved CR-020 mechanism choice. This is DW-036.

**Relation to earlier rulings:**
- **What R19 supersedes:** for these items only, closure ruling R2 of 2026-10-20 (`m5-exit-inventory.md` §13; decision register D-N), which had placed them in M6. The 2026-10-12 phrase "until after the playtest" (D-052 §10, §11; DW-034, DW-036) referred to the M5 playtest. R2 replaced it, and R19 is a new deferral.
- **What stays in force:** D-051 §2 and §4 stay **RATIFIED**; this changes scheduling, not the design ruling. Deposit concealment (D-062 R6/R7, ADR-044) stays unchanged. The Workshop guard (D-052 §11; `ConstructionQuery` → `InputUnobtainable`) stays unchanged.

**Owner when it is scheduled:**
- **M10** is a content-mapped future possibility: R2 sent the related D-051 §6 remainder (the central stockpile) to M10 (`m5-exit-inventory.md` §13), while it placed §2 and §4 in M6. M10 is **not a commitment**.
- The **M7 economy track** (ADR-056 EC-1 toolmaking) is a coupled alternative, **not an assignment**.

**Carried open for whoever schedules the work:** D-062 R6 says deposits are "randomly distributed". D-051 §2(b) says richness is "geographically correlated … not a random site". This contradiction is unresolved.

**Consequences:**
- M6.24 / T6.21 is **DEFERRED**. It is **not an M6 exit criterion** (`m6-spec.md` §5 as amended by D-062, D-066 and this record names none of it).
- This ruling **discharges** the M6.24 Director gate (`m6-exit-inventory.md` §4 gate 6).
- The M6 playtest must disclose stone-poor settlements and the absence of a Workshop before bronze (`m6-playtest-checklist.md` §2).

---

## 6. The two-layer integrated tree

| # | Ruling |
|---|---|
| R20 | The intended commit order is **the M7 execution pass first, the M6 closure second**. No commit or push is authorized by this record. |

**The layers, verified 2026-10-28 by `git write-tree` on the live worktrees:**

| Layer | Tree | Files changed |
|---|---|---|
| Base commit | `0bf7626` | — |
| M7 pass alone | `cb2eb1db` | 217, against `0bf7626` |
| Validated combined tree, before this record | `c05878de` | a further 202, 47 of them overlapping the M7 layer |

The separation procedure and its consequences are in `docs/m6-exit-inventory.md` §6.

---

## 7. Records receiving an appended, dated pointer note, or an amendment under their own conventions

**Ruling records and the spec:**
- `docs/d066-m6-closure-rulings.md`
- `docs/d051-m5-polish-rulings.md`
- `docs/m6-spec.md`

**ADRs:**
- `docs/adr/adr-055-m7-recruitment-personnel-reequip.md`, `docs/adr/adr-056-economy-packets.md`: amended, then status RATIFIED.
- `adr-057`, `adr-058`, `adr-059`: status CONFIRMED, with stale facts corrected.

**M6 planning registers:**
- `docs/m6-plan/m6-task-register.md`
- `docs/m6-plan/deferred-work-register.md`
- `docs/m6-plan/m6-decision-register.md`
- `docs/m6-plan/m6-scope-register.md`
- `docs/m6-plan/m6-risk-register.md`
- `docs/m6-plan/m6-dependency-graph.md`
- `docs/m6-plan/golden-attribution-register.md`

**Hand-off and status records:**
- `docs/m7-plan/m7-handoff-register.md`
- `docs/m7-plan/m7-execution-pass-report.md`
- `docs/m6-exit-inventory.md`
- `docs/m6-plan/m6-playtest-checklist.md`
- `docs/current-state.md`

**Content description:** `Sim.Data/content/sim.json` (the `military` description string only; no value changes).

**M7 charter:** `docs/m7-plan/unit-catalogue-and-progression.md` (a dated note on the withdrawn family keys).

**Comment-only code corrections** (wrong ADR-056 section citations; no code token changed):
- `Sim.Core/State/Ids.cs`
- `Sim.Core/State/LabourActivities.cs`
- `Sim.Core/Systems/GoodsConfig.cs`
- `Sim.Core/Systems/Production/ProductionSystem.cs`
- `Sim.Tests/Systems/EconomyMountsTests.cs`
- `Sim.Tests/Systems/EconomyGunpowderTests.cs`
- `Sim.Tests/Systems/LawCourtAiWorldAttributionTests.cs` (its class summary's 506, labelled as the pre-integration value)

## Dated note — 2026-10-29 (D-068)

[D-068](d068-m6-playtest-fix-rulings.md) R3 supersedes R6 (A14) **for the workbook container only**. The workbook is now
deflated, so its `.xlsx` bytes are no longer a cross-runtime goal; the decoded part contents stay pinned. D-068 R5 also
moves the workbook content goldens (coded sheets). R6's text above stays as written.
