# M4 CLOSURE AUDIT — output A

**Lane:** the M4 closure audit lane (PART 1 of the Director's 2026-09-19 mandate, as relayed to this
agent by the orchestrator).
**Tree read:** `claude/civdemo-work-b1z2y4` @ `9f6c6ae`, working tree clean. Every `file:line` in this
document was read on that tree.
**Certified baseline:** `origin/main` @ `dbef61a` ("Merge M4 completion (director-certified): the M4
baseline"). `dbef61a..9f6c6ae` = 138 commits (MEASURED, `git log --oneline dbef61a..HEAD | wc -l`).
**Author:** a research/implementation agent. **The Director is ChatGPT. Nothing in this file is a
ruling.** Where documents and instructions conflict, this file surfaces the conflict and presents
both readings with their textual basis. It does not choose.

## 0. WHAT THIS FILE IS, AND WHAT IT IS NOT

This is a NEW document under `docs/design/`. No existing ratified or frozen document was edited to
produce it. No production code, schema, test, data file or corridor band was touched. No merge was
performed. Merging is a Director ruling under `CLAUDE.md` ("the DIRECTOR RULES on acceptance, and
only then the agent performs the merge to `main` on that explicit ruling"), and this lane does not
perform it.

**Label key** (mandate-imposed; every substantive claim carries exactly one):

| label | meaning |
| --- | --- |
| RATIFIED | a ruling or frozen decision exists, cited `file:line` |
| MEASURED | a number someone measured, cited to its record and tree |
| PROPOSED | this phase's new design suggestion, for the Director to ratify or reject |
| INFERRED | reasoned from mechanism or document, not directly stated |
| DIRECTOR DECISION REQUIRED | a genuine open choice |
| UNVERIFIED | this lane could not verify it, with the reason stated |

**Evidence hygiene.** Under `docs/gov-4-repository-freshness.md` a prior agent's report is SECONDARY
evidence. Where this file leans on `docs/t4.21-director-report.md`, `docs/t4.21-4-record.md`,
`docs/t4.21-6-validation-record.md` or CR-016's own measurement sections, it says so and names the
tree the number was taken on. Numbers this lane measured itself are marked "MEASURED (this lane)".

**A provenance fact that governs the whole of §4.** The Director's 2026-09-19 mandate **does not
exist as a document in this repository**. MEASURED (this lane): `grep -rln "2026-09-19" docs/`
returns nothing. Its two instructions quoted in §4.3 reach this lane only through the orchestrator's
computed task text, which carries no user authority and is not a repository document. Every
comparison in §4.3 between that mandate and CR-015 is therefore a comparison between a RATIFIED
document and an UNVERIFIED relay. That asymmetry is itself part of what the Director must rule on.

---

## 1. THE FOUR LISTS — THE DELIVERABLE'S SPINE

Everything after §1 is support for these four lists.

### 1.1 REMAINING BLOCKERS

Blockers to CLOSING M4 — not blockers to the suite, which is green.

| # | blocker | label | basis |
| --- | --- | --- | --- |
| B1 | **The Director's exit session against the candidate, replaying hash-identical, with a T3.12a replay report.** Not run. | RATIFIED (criterion) | `docs/milestones.md:350` — "AWAITING THE DIRECTOR — the machinery is complete and tested" |
| B2 | **The merge ruling for the five post-certification packets.** Not given. | RATIFIED | `docs/milestones.md:344`; `CLAUDE.md` Environment section |
| B3 | **The `m4-exit` Release tag.** Does not exist. | MEASURED (this lane) | `git tag` returns `m3-exit` only |
| B4 | **`docs/milestones.md`'s M4 entry is the closing artefact and is stale in five places against this tree.** It cannot be the record of a closed milestone as written. See findings F1, F2, F3, F5, F8 (§3). Repairing it is a living-document edit on a Director ruling; this lane is barred from it. | MEASURED (this lane) + DIRECTOR DECISION REQUIRED | §3 of this file |
| B5 | **The density-quarantine window breach that no instrument can see.** `canonical.densityPerArableKm2` seed 3 = 0.354157 against the recorded window floor 0.3685744951368359, 3.9 % below; the nightly does not gate a quarantined corridor and the battery's per-seed teeth run only seeds 1 and 2. | MEASURED (lane B, SECONDARY, on the T4.21 candidate) — **NOT VERIFIED** | `docs/t4.21-director-report.md:1290-1304`; ADR-015 §6 makes it non-actionable until a verdict returns |

**B5 is the one blocker with a defect shape.** B1–B4 are Director acts and bookkeeping. B5 is a
measured breach of a quarantine's own stated lift condition, invisible to both instruments, on the
tree being proposed for merge. INFERRED: it needs a verify lane before anyone touches
`corridors.json`, and it is the exact failure mode known-open item 11 predicted (§3, item 11).

**Not blockers.** The test suite (green, §6.1), the three gate scripts (green, MEASURED this lane),
determinism, the goldens, and CR-016 itself. CR-016 being OPEN does not block a merge on the
documents: `docs/adr/cr-016-armed-disaster-fallout.md:171-200` puts the tree in the state its author
calls "the only state that is honest while the CR is open", and `docs/t4.21-director-report.md:1410`
recommends merging first and ruling CR-016 second. Whether a milestone may close over an OPEN CR is
DIRECTOR DECISION REQUIRED; INFERRED from precedent, M3 closed over a known-open list
(`docs/milestones.md:262`), so the precedent exists.

### 1.2 DEFERRED ITEMS

| # | item | label | basis |
| --- | --- | --- | --- |
| D1 | **G8 / CR-016 option 3 — the per-year food-balance sub-step.** The real fix for the dt artefact; M5-scale. | RATIFIED as queued | `docs/adr/cr-016-armed-disaster-fallout.md:305-310`; `docs/queue.md` G8(c) |
| D2 | **The exact disaster renewal process** (onset time uniform within the turn, loss spilled into the next turn, superposed onsets multiplying). Accepted biases ~4.7 % / ~4.8 % stand. | RATIFIED | `docs/t4.21-architecture.md` §3.3 (onset-process paragraph); ADR-024 §4 |
| D3 | **Spatial correlation and a weather-conditioned disaster hazard.** | RATIFIED as deferred | `docs/t4.21-architecture.md` §3.3, "Spatial correlation and a weather-conditioned hazard are DEFERRED (queue)" |
| D4 | **A second disaster kind and the per-row famine-class predicate** (`Severity·D·ρ_ship ≥ B_eff`) it would require. | RATIFIED as queued | `docs/t4.21-architecture.md` §3.3 |
| D5 | **Observability owed:** `recentStress`; `migrationPlan.inflowAeByChannel`; per-settlement / per-`FoodState` starvation in `AutoplayMetrics`; `autoplay-metrics/v2` famine and disaster counters. | MEASURED as owed (SECONDARY) | `docs/t4.21-director-report.md:1358-1372`, each row "CONFIRMED (queue.md)" |
| D6 | **CR-003 Malthus corridor** (known-open item 2) — quarantined by standing ruling; the crash model is not M4 work. | RATIFIED | `docs/milestones.md:380` "OPEN / FUTURE MILESTONE" |
| D7 | **The turn-2 structural artefact.** Root cause is the founding warm-up, out of T4.21's scope; ADR-025 §2.4a records the unmet RULE 2 acceptance criterion as a DEVIATION awaiting sign-off. | MEASURED as recorded (SECONDARY) | `docs/t4.21-director-report.md:1200`, `:1441-1444` |
| D8 | **Thin mutation coverage, M-MIG-FANOUT above all** — the RULE 1 "fan-out cap stays" ruling rests on a single tooth; adding a tooth is a code change no validation lane was authorised to make. | MEASURED (SECONDARY) | `docs/t4.21-director-report.md:1447-1450` |
| D9 | **G1 interim disposition (a) LEAVE.** A deferral of action, not a closure. See §5. | RATIFIED | `docs/adr/cr-015-famine-is-exceptional.md:564` "Until ruled: (a)" |

### 1.3 DIRECTOR DECISIONS REQUIRED

| # | decision | where it lives |
| --- | --- | --- |
| R1 | **Merge the candidate to `main`, or not.** | `CLAUDE.md`; `docs/milestones.md:344` |
| R2 | **CR-016 — the disaster rate.** Three options on record, none taken. And, newly surfaced here, whether CR-015's directive to arm stands amended or unamended. §4. | `docs/adr/cr-016-armed-disaster-fallout.md:130-155`, `:295-316` |
| R3 | **G1 — the harvest-weather decade variance.** Three options on record; a fourth path is now in play that none of them describes. §5. | `docs/adr/cr-015-famine-is-exceptional.md:524-570` |
| R4 | **Known-open item 1 / B5 — the density-quarantine window breach.** Verify lane first, then a ruling; no `corridors.json` edit before both. | §3 item 1; `docs/t4.21-director-report.md:1290-1304` |
| R5 | **Known-open item 4b — the `MagnitudeCorridor` upward rate-lever tooth**, quarantined in place at λ = 0 and restored verbatim if a non-zero rate is ruled. Conditional on R2. | §3 item 4b; `Sim.Tests/Systems/MigrationTests.cs:767-776` |
| R6 | **ADR-018 §11's premise no longer holds** — gap-driven and gross now respond almost identically to the rate lever, at both rates. "Is GROSS the wrong observable, or is the LEVER unreachable?" is open at both. | `docs/t4.21-director-report.md:1375-1381` |
| R7 | **The CR-003 Malthus teeth's cause-attributed re-aim** — recorded, deliberately not taken, because it changes the quarantine's substance. | `docs/adr/cr-016-armed-disaster-fallout.md:166-169`; `docs/t4.21-4-record.md` RULE C |
| R8 | **Known-open item 5** — ADR-017 reads "proposed (director certification pending)" while the spec cites its ruling as settled. | `docs/adr/adr-017-d025-founding-variation.md:3` |
| R9 | **Known-open item 6** — ADR-020 clone architecture awaiting a ruling. | `docs/milestones.md:385` |
| R10 | **Known-open item 7** — the T4.19-E structural set S1–S5; the Director's own list never arrived. | `docs/milestones.md:386` |
| R11 | **Known-open item 8** — grain is storage-bounded, livestock and fish are not; the repository records no intent either way. | `docs/milestones.md:387` |
| R12 | **Known-open item 9** — ADR-019 exists only on an unmerged branch; the sequence on this candidate still jumps 018 to 020. | MEASURED (this lane): `ls docs/adr/` shows `adr-018` then `adr-020` |
| R13 | **Known-open item 11** — the nightly gate never consults the quarantine window. Unchanged on this tree, and B5 is it firing. | `.github/workflows/ci.yml:232-234` |
| R14 | **ADR-025 §2.4a RULE 2 acceptance criterion**, recorded as an unmet DEVIATION awaiting sign-off. | `docs/t4.21-director-report.md:1441-1444` |
| R15 | **The `m4-exit` tag and the closing edit to `docs/milestones.md`** (B3, B4). | `docs/milestones.md:351` |

### 1.4 MERGE READINESS — ONE LINE

**Technically ready, governance-incomplete, with one unverified measured breach on the candidate.**
Full checklist in §6.

---

## 2. EXIT CRITERIA — `docs/milestones.md` §6 TABLE, RE-READ AGAINST THIS TREE

The table is `docs/milestones.md:342-351`. "Stated" is the document's own word. "Current truth" is
this tree.

| § 6 criterion | stated disposition (`milestones.md`) | current truth on `9f6c6ae` | label |
| --- | --- | --- | --- |
| All packets accepted, each merged on a Director ruling | MET for T4.1–T4.16; the four post-certification packets await the merge (`:344`) | **Still open, and the count is now FIVE, not four.** T4.17–T4.21 sit on the candidate; none is merged. The same entry's prose already says so at `:310` ("Four post-certification packets sit on the exit candidate") and `:316` ("A fifth post-certification packet, T4.21"), so the entry contradicts itself between its prose and its table | MEASURED (this lane, git) + FINDING F1 |
| Scarcity can bite | MET — starvation reachable on the dev world (`:345`) | **True, but on a different and much narrower basis than the one recorded.** At the shipped λ = 0 the dev reconciliation rig starves **0** in 900 turns (CR-016 D.5, SECONDARY). Canonical, 20 seeds × 650 turns: **451** starvation deaths in **2 of 20** seeds AFTER, against **7 085** in **20 of 20** BEFORE; at 161 and 300 turns AFTER shows zero on every seed (lane B, SECONDARY, `docs/t4.21-director-report.md:1306-1317`). FAMINE is reachable in play only by abandonment, item 1(B); zero FAMINE turns, zero disasters, zero abandonment were observed in 1 932 + 39 852 settlement-turns of play measurement (`:1341-1346`) | MEASURED (SECONDARY) + FINDING F2 |
| Determinism suites green; xproc; first-reign shape | MET — measured on the candidate, all green (`:346`) | **Holds.** Determinism is five-for-five with `ci.yml`'s `FOUNDED_GOLDEN` re-derived from the binary rather than copied (`docs/t4.21-director-report.md:1043-1069`, SECONDARY); determinism with an active `DisasterRow` is twin / replay / save-load hash-identical (CR-016 §3) | MEASURED (SECONDARY) |
| Goldens pinned with dated history; driven golden extended | MET (`:347`) | **Holds, and moved.** All four behavioural world goldens moved across T4.21 and moved BACK byte-for-byte at the disarm (`docs/t4.21-architecture.md:961`). No golden digit was merged rather than measured (ADR-015 §6 discipline recorded at CR-015 §6.5) | MEASURED (SECONDARY) |
| Calibration battery green across ≥20 seeds; quarantined corridors reported rather than silently gating | MET at the exit gate (`:348`) | **The gating half holds; the reporting half is breached and invisible.** Every canonical corridor that gates is green on 20/20 seeds and `fedGrowthPerYear` sits mid-band at 0.00075167 (`docs/t4.21-director-report.md:916-940`, SECONDARY). But seed 3's density reads 0.354157, 3.9 % BELOW the recorded quarantine window, and neither instrument can see it (B5). "Reported rather than silently gating" is exactly what is not happening | MEASURED (SECONDARY) + FINDING F3 |
| The nightly has been green, and someone has read it | INSTRUMENT MET, READING IS THE DIRECTOR'S (`:349`) | **Unchanged, and item 11 is unchanged with it.** `.github/workflows/ci.yml:232-234` still reads `def gated($c; $v): ($c.quarantine? and $c.quarantine.active) or (...)` — the window is never consulted | MEASURED (this lane) |
| Director exit session from the CI zip, replaying hash-identical, with a T3.12a replay report | AWAITING THE DIRECTOR (`:350`) | **Still awaiting.** No record of such a session exists on this branch | MEASURED (this lane) — blocker B1 |
| `milestones.md` M4 entry with its known-open list; `m4-exit` Release | this entry closes the first half; the tag is the Director's (`:351`) | **Tag absent** (`git tag` = `m3-exit` only). **The entry itself now needs the closing edit** (B4) | MEASURED (this lane) — blockers B3, B4 |

---

## 3. THE KNOWN-OPEN LIST AND ITS RECONCILIATION, ITEM BY ITEM

Two tables govern: the KNOWN-OPEN LIST at `docs/milestones.md:353-367` (ten rows, 1..10 with 4a/4b)
and the RECONCILIATION at `:373-390` (eleven rows — it adds item 11). **FINDING F0, minor:** the
known-open list has ten rows and the reconciliation has eleven; item 11 exists only in the
reconciliation, marked "(new, found by this packet)". A reader of the first table alone never sees
it. MEASURED (this lane).

### Item 1 — density out of band on 6/20 seeds, quarantine inactive

- **Stated:** list `:357` "Needs a ruling, not a re-band". Reconciliation `:379` **CLOSED / VERIFIED**
  — the quarantine was re-activated over the measured envelope; dry-run QUARANTINED, exit 0; control
  breach, exit 1; band never moved. RATIFIED.
- **Current truth:** the quarantine is active in data. MEASURED (this lane):
  `Sim.Data/content/corridors.json`, `canonical.densityPerArableKm2.quarantine.active = true`,
  `window = [0.3685744951368359, 0.7421101248166698]`.
- **FINDING F3.** The closure was true for the certified baseline. On the candidate the world has
  moved underneath the window: T4.21 moves every seed down (ratio min 0.9421, mean 0.9691) and seed 3
  lands 3.9 % below the floor. The window's own text says drifting outside the recorded envelope "is
  a NEW defect or a ruled substrate change that must re-pin this window deliberately". MEASURED
  (lane B, SECONDARY, `docs/t4.21-director-report.md:1290-1304`). **NOT VERIFIED — no verdict has
  returned, so under ADR-015 §6 it is not actionable.** This is blocker B5 and decision R4.
- **Second-order fact worth the Director's eye:** lane B established that "arable is invariant
  between arms" — a premise T4.19c's attribution relied on — is **no longer universally true**;
  arable itself moved on seeds 9 and 14 (−2.47 %, +0.04 %). MEASURED (lane B, SECONDARY). INFERRED:
  any re-pin of this window must re-derive the attribution, not just the endpoints.

### Item 2 — CR-003 Malthus corridor

- **Stated:** list `:358` "Quarantined by standing ruling; two dev seeds red by design".
  Reconciliation `:380` **OPEN / FUTURE MILESTONE**. RATIFIED.
- **Current truth:** the quarantine ruling stands. **The two reds it describes are gone.**
  `Dev_MalthusCorridors_AllInBand(42)` and `(7)` PASS on this tree; all three teeth
  (`starvedTotal == 0`, `crashes == 0`, `peak == final`) pass again at λ = 0. MEASURED (CR-016 D.5,
  `docs/adr/cr-016-armed-disaster-fallout.md:318-340`, SECONDARY; and the whole-suite 0-failed figure
  in §6.1).
- **FINDING F4.** "Two dev seeds red by design" is no longer a description of this tree. The
  disposition (quarantined, future milestone) is unaffected; the observable it cites is. INFERRED:
  the teeth are green because T4.21 removed the starvation that made them red, not because the
  Malthus question was answered. A closing edit that repeats "two dev seeds red by design" would be
  false against `9f6c6ae`.

### Item 3 — migration below the historical corridor

- **Stated:** list `:359`; reconciliation `:381` **CLOSED / VERIFIED** — accepted as measured by
  Director ruling 2026-09-04 with 20-seed evidence; the corridor is a record, not an acceptance gate.
  RATIFIED.
- **Current truth:** the acceptance stands, and the world moved further. The dev envelope was
  re-pinned at the disarm to the measured λ = 0 values 8.336943780925534E-05 (seed 42) and
  1.0200612834541973E-04 (seed 7) — "an order of magnitude BELOW the floor". MEASURED (CR-015
  T4.21-7 append block, `:700-706`, SECONDARY). `DriftTolerance` unchanged at 0.75; no band moved.
- **Note, not a finding.** `docs/t4.21-director-report.md:1268` records the canonical migration
  corridor's widening gap and seed 2's band exit as horizon-dependent and UNRESOLVED. That belongs
  with R6 rather than with this item.

### Item 4a — `FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes`

- **Stated:** list `:360` "OPEN, needs reassignment; currently unowned … **OWNED BY T4.21-4 (CR-015
  N9, 2026-09-17)**"; reconciliation `:382` **OPEN / REQUIRES DIRECTOR RULING**. RATIFIED.
- **Current truth: DISCHARGED.** The skip is lifted and the test is live. MEASURED (this lane):
  `Sim.Tests/Systems/MigrationTests.cs:397-398` carries a bare `[Fact]`, not `[Fact(Skip = …)]`;
  `grep -rn "Skip *=" Sim.Tests/` returns four hits, all of them the long-standing manual measurement
  rigs (`FoundingVariationItem0Tests.cs:58`, `WaterRouteCounterfactualTests.cs:88` and `:142`,
  `HousingBeforeColumnTests.cs:40`) and none of them this test.
- **How it was discharged.** Re-aimed from GROSS out-migration onto the FLIGHT channel by name,
  read through `MigrationSystem.Plan` — the executed quantity, not a second implementation. The 8 %
  threshold is UNCHANGED; only the observable moved, which is what ADR-018 §11 required. An
  anti-vacuity guard was added: settlement 0 must classify FAMINE/Abandonment. MEASURED
  (`docs/t4.21-4-record.md:219-227`, SECONDARY): flight crosses at turn 4 with 183.4 attributable
  flight against 139 attributable starvation; exit leads.
- **It survived the disarm.** The rig is abandonment-driven, so λ = 0 does not touch it: "UNAFFECTED,
  live and green". RATIFIED-as-recorded (CR-015 T4.21-7 block, `:707-709`).
- **FINDING F5.** `docs/milestones.md:360` and `:382` still read "OPEN / REQUIRES DIRECTOR RULING"
  for an item the tree has discharged. The document and the tree disagree. No ruling is required for
  4a any more; a closing edit is.

### Item 4b — `MagnitudeCorridor_FedPhaseDrift_WithTeeth`

- **Stated:** list `:361` "OPEN, needs an owner to re-aim the teeth onto the gap-driven observable
  … **OWNED BY T4.21-4**"; reconciliation `:383` **OPEN / REQUIRES DIRECTOR RULING**. RATIFIED.
- **Current truth: PARTIALLY DISCHARGED.** The skip is lifted — MEASURED (this lane),
  `Sim.Tests/Systems/MigrationTests.cs:665-666` is a bare `[Fact]`. The corridor assertion stays on
  GROSS against `corridors.json` and both DOWNWARD teeth are live and green at λ = 0. The re-aim onto
  `MaxGapDrivenPerDecade` was performed. MEASURED (`docs/t4.21-4-record.md:229-244`, SECONDARY,
  measured on the ARMED tree: gap-driven ×0.74 / ×1.88).
- **What is NOT discharged.** At the shipped λ = 0 the UPWARD rate-lever tooth measures **×1.0082**,
  below ADR-018 §11's own recorded DEAD signature of ×1.07 — so that one assertion is quarantined in
  place, as an inverted `Assert.False` that fires the moment the lever comes back. MEASURED
  (`Sim.Tests/Systems/MigrationTests.cs:740-776`, read this lane; the numbers are T4.21-7's,
  SECONDARY). The test was deliberately NOT re-skipped, because a skip would have taken the live
  corridor assertion and both downward teeth down with it.
- **FINDING F6, and it is the substantive one.** The packet did what N9 asked — it moved the
  observable — and the move revealed that ADR-018 §11's PREMISE is false at both rates. §11 rested on
  gap-driven responding ×3.10 to a 10× lever while gross responded ×1.07. Re-measured: armed,
  gap-driven ×0.74 / ×1.88 against gross ×0.71 / ×1.91; shipped, ×1.0082 upward on gap-driven and
  ×1.02 on gross — dead upward on BOTH observables. MEASURED (SECONDARY,
  `docs/t4.21-director-report.md:1375-1381`; "PROBABLE as to the exact lever ratios, which this lane
  did not re-measure"). So the open question is no longer "who owns the re-aim" but "is GROSS the
  wrong observable, or is the LEVER unreachable?" — which is decision R6, a different question from
  the one `milestones.md:361` records. INFERRED.

### Item 5 — ADR-017 "director certification pending"

- **Stated:** `:362` / `:384` **OPEN / REQUIRES DIRECTOR RULING**. RATIFIED.
- **Current truth: unchanged.** MEASURED (this lane):
  `docs/adr/adr-017-d025-founding-variation.md:3` reads
  `Status: proposed (director certification pending) · Packet: T3.6b · Date: 2026-07-29`. Decision R8.

### Item 6 — ADR-020 clone architecture

- **Stated:** `:363` / `:385` **OPEN / REQUIRES DIRECTOR RULING**; blocks nothing. RATIFIED.
- **Current truth: unchanged.** MEASURED (this lane): `docs/adr/adr-020-clone-architecture-r3.md`
  is present and carries no status line granting a ruling. Decision R9.

### Item 7 — T4.19-E structural test set S1–S5

- **Stated:** `:364` / `:386` **OPEN / REQUIRES DIRECTOR RULING**; "the Director's own list never
  arrived; the set is the implementer's reading". RATIFIED.
- **Current truth: unchanged.** UNVERIFIED as to whether any later packet re-examined S1–S5 — this
  lane found no record of one on this branch and did not run the set in isolation. Decision R10.

### Item 8 — grain is storage-bounded; livestock and fish are not

- **Stated:** `:365` / `:387` **OPEN / REQUIRES DIRECTOR RULING**; ADR-023 settles variety, not the
  storage asymmetry. RATIFIED.
- **Current truth: unchanged, and now load-bearing.** T4.21's severity derivation takes the granary
  `G = 1.5` years as the buffer a famine-class event must beat
  (`docs/t4.21-architecture.md` §3.3; ADR-024 §3). INFERRED: the storage asymmetry therefore now
  enters the disaster band's derivation input, so ruling item 8 has become coupled to §4's rate
  question. Decision R11.

### Item 9 — ADR-019 exists only on an unmerged branch

- **Stated:** `:366` / `:388` **OPEN / REQUIRES DIRECTOR RULING**; housekeeping. RATIFIED.
- **Current truth: unchanged.** MEASURED (this lane): `ls docs/adr/` goes
  `adr-018-d025-minimum-spacing.md` then `adr-020-clone-architecture-r3.md`. The sequence now also
  carries ADR-024/025/026 and CR-015/CR-016, all added by T4.21. Decision R12.

### Item 10 — `docs/current-state.md` stale in four load-bearing claims

- **Stated:** `:367` / `:389` **CLOSED / DOCUMENTATION ONLY** — four claims corrected inline and
  marked. RATIFIED.
- **Current truth: the four corrections are present and the file has been kept up to T4.21-8.**
  MEASURED (this lane): the staleness-correction block is `docs/current-state.md:3-15`, and the file
  carries dated T4.21-1 through T4.21-8 entries including the disarm (`:101-155`) and the
  885 / 0 / 4 figure (`:176-180`).
- **FINDING F7, minor.** The file has gone stale again in two places against `9f6c6ae`: `:17`
  still names `t4.19-glass-box` as "the current M4 exit candidate" with a 722 / 4 / 6 suite, which
  every later block in the same file supersedes; and there is no entry for the two director-report
  commits `7d33805` and `9f6c6ae`. MEASURED (this lane). Housekeeping, not a blocker — and
  `docs/gov-4-repository-freshness.md` already tells every reader to verify this file against git.

### Item 11 — the nightly gate never consults the window

- **Stated:** reconciliation `:390` only, **OPEN / REQUIRES DIRECTOR RULING** — "`gated()`
  short-circuits on `quarantine.active` alone, so a quarantined corridor drifting far outside its
  recorded envelope is caught only by the in-process battery's two seeds, never by the 20-seed
  nightly". RATIFIED.
- **Current truth: unchanged, verbatim.** MEASURED (this lane), `.github/workflows/ci.yml:232-234`:

  ```
  def gated($c; $v): ($c.quarantine? and $c.quarantine.active)
    or ($v >= $c.band[0] and $v <= $c.band[1]);
  ```

  The window is read at `:225` for the PRINTED line and nowhere in the gate.
  `Sim.Tests/TestUtil/Corridors.cs:56-65` exposes `Quarantine()` returning the window, so the data
  is available to any caller that wants it.
- **FINDING F8, and it is the connective one.** Item 11 predicted a failure mode; B5 is that failure
  mode having occurred. The quarantined density corridor has drifted below its recorded envelope on
  the candidate and no instrument reports it. Item 11 is therefore no longer hypothetical.
  INFERRED. Decision R13, and it is coupled to R4.

---

## 4. CR-016 — THE DISASTER RATE

### 4.1 The shipping value, and the mechanism's state

- **Shipping value: `hazardPerYear = 0.0`.** MEASURED (this lane):
  `Sim.Data/content/sim.json:235`. The rest of the block is `durationYears 5.0` (`:236`),
  `severityMin 0.75` (`:237`), `severityMax 1.0` (`:238`).
- **The mechanism ships complete and tested but INERT.** RATIFIED as an orchestrator decision, not a
  Director ruling: `docs/adr/cr-016-armed-disaster-fallout.md:179-198` (D.1). The CR's status line is
  `:3` — "**OPEN — ESCALATED TO THE DIRECTOR. Nothing is fixed here.**"
- **What is proven without the rate.** Forced-strike rigs, ladder tests, the onset-process test and
  all three determinism legs arm λ in their own rigs. RATIFIED-as-recorded, CR-016 `:210-236` (D.2),
  which names each test. Mandate item 1(B), deliberate abandonment, is live on the shipped config and
  is what makes famine reachable in play today (`FamineScenarioTests.S_Abandonment_TriggersFamine`).
- **One data edit away.** CR-016 `:237-241` (D.3), with the cost priced: four behavioural world
  goldens, `ci.yml`'s `FOUNDED_GOLDEN`, one recorded envelope, two CR-003 quarantines, six named
  reds, and six re-aimed `SimConfigTests`. MEASURED (T4.21-4 and T4.21-8, SECONDARY).

### 4.2 What CR-015 as RULED says about arming — quoted

CR-015's status line, `docs/adr/cr-015-famine-is-exceptional.md:3`:

> **Status: RULED (mandate 2026-09-17) — implementation T4.21-1..6 authorized; G1 open.**

The arming instruction sits in CR-015 §4, the blast-radius section adopted by §5's recommendation
and §6's ruling. `docs/adr/cr-015-famine-is-exceptional.md:336-339`:

> **Golden ladder:** T4.21-1 moves all four goldens by LAYOUT + RngStreams only (λ = 0; the strip
> control reproduces the pre-packet pins byte-for-byte); T4.21-2 and T4.21-3 each carry their own
> attribution step ("P2 alone", "P3 alone"); **T4.21-4 arms λ = 0.01 and is the last golden move.**

And `:327`, the coupling-map row for `DisasterSystem`, requires "behaviour at T4.21-4" of the
goldens — i.e. the goldens are expected to move for a LIVE hazard.

The ruling text itself, §6.1 `:369-401`, names the CAUSE and not the rate:

> There are exactly two famine CAUSES: a famine-class natural disaster, and deliberate productive
> abandonment (farming = 0 and herding = 0) … the disaster is represented by the smallest
> architecture-consistent mechanism.

The value's own governing row calls it CHOSEN. `docs/t4.21-architecture.md:461`, and identically
`docs/adr/adr-024-food-state-effective-deficit-disaster-shock.md:176`:

> `hazardPerYear` λ | 0.01 | **CHOSEN — DERIVED VALUE 0.01, SHIPPED AT 0.0.**

**Provenance check, because the row has been edited since the ruling.** MEASURED (this lane, git):
at the governance commit `eee90bc` ("T4.21-0: adopt the famine-is-exceptional architecture as the
packet spec") the same row already read `| \`hazardPerYear\` λ | 0.01 | CHOSEN: one famine-class local
crop failure per settlement per century …`. So **λ was labelled CHOSEN, not DERIVED, at the moment of
ruling.** The "SHIPPED AT 0.0" clause is the later T4.21-7 edit.

And CR-015 §6.5's "no constant moved" ban explicitly does not bind λ.
`docs/adr/cr-015-famine-is-exceptional.md:577`:

> New constants (`a`, λ, D, `s_min/max`, `k`) are new, with their frames stated.

### 4.3 What CR-016 escalates, and the measured fallout of the armed arm

CR-016 §1 (`:26-36`) names four frozen items: **A** CR-015's disaster at λ = 0.01; **B** CR-001's
permanent dt-continuity detonator ("This test exists forever"); **C** `corridors.json`'s immovable
canonical bands; **D** the G8/F4 dt artefact, accepted as inherited while λ = 0 made it invisible.
A collides with B and with C; D is the mechanism of the collision.

**The armed fallout, MEASURED by T4.21-4 on `t4.21-4-arm` (cut from `8f7f9da`) and independently
reproduced by validation lane C on `val-c-arm` @ `64a3f2f` with a single data edit** (SECONDARY on
both counts; `docs/adr/cr-016-armed-disaster-fallout.md:38-111` and
`docs/t4.21-director-report.md:1080-1096`):

| observable | armed (λ = 0.01) | shipped (λ = 0) | bar / band |
| --- | --- | --- | --- |
| CR-001 detonator, `CalibrationBatteryTests` rig, seed 1 | **2.2572** per 1000 yr (+0.4352 → −1.8220) | 0.0056 | 0.1 |
| CR-001 detonator, `DemographyRetuneTests` rig | **2.5259** (+0.7616 → −1.7643) | passes | 0.1 |
| `canonical.fedGrowthPerYear` | **0.000407442** — below floor | 0.00075167, mid-band, 20/20 | [0.0005, 0.001], immovable |
| canonical turn-300 population, cross-seed median | **0.26×** the unarmed world | — | — |
| dev world, 1000 turns, seed 42 | 93 910 → **38** | monotone growth | — |
| `dev.migrationGrossPerDecade`, seed 42 | 8.34E-05 → **0.0148**, 1.5× above the CEILING | 8.336943780925534E-05 | quarantined |
| tests left RED | **six**, named in CR-016 §5 | zero | — |
| onset process itself | 0.941061 per settlement-century, z = −0.925 inside the 99 % binomial band | — | correct |

**CR-016's own diagnosis of why**, `:78-84`: at 9.5 % of settlement-decades taking an onset and a
FAMINE turn's mortality `1 − e^{−0.12·d·10}` (38 % at d = 0.4) against a fed clock of 0.76 %/decade,
the expected drift is **−2.9 % per decade — net negative by construction**. λ and
`starvationMortalityMaxPerYear` "were derived independently and their product was never checked".

**CR-016 §3 is equally clear about what is NOT in conflict** (`:112-128`): the onset process is
correct, the ladder is correct, determinism holds with disasters live, and the arming is
bit-attributable. "The mechanism does what CR-015 says. The quarrel is with its MAGNITUDE against the
demographic kernel."

### 4.4 THE CONFLICT, SURFACED AND NOT RESOLVED

**The two instructions.**

- **(i) RATIFIED.** CR-015, status RULED, directs that λ = 0.01 be armed at T4.21-4 as the last
  golden move (`cr-015:338`), and its coupling map expects golden behaviour change there
  (`cr-015:327`).
- **(ii) UNVERIFIED RELAY.** The Director's 2026-09-19 mandate, as passed to this lane by the
  orchestrator, instructs "Do NOT ship λ=0.01" and "keep the mechanism unarmed unless an
  already-ratified requirement says otherwise". **This text exists nowhere in the repository**
  (MEASURED, this lane: no file on this branch contains the string `2026-09-19`).

**First, the fact that defuses the urgency.** The tree already ships λ = 0.0
(`Sim.Data/content/sim.json:235`, MEASURED this lane). Instruction (ii) is therefore **already
satisfied by the shipping state**, and was satisfied before it was issued, by T4.21-7's orchestrator
decision. **No edit to any data file is required to comply.** INFERRED. What is genuinely in conflict
is the RECORD — whether CR-015's directive to arm stands amended or unamended — and what closes
CR-016. Nothing about the tree.

**The question the mandate poses: does CR-015 constitute "an already-ratified requirement saying
otherwise"?** Both readings, with their textual basis, neither chosen.

**READING A — YES. CR-015 is such a requirement, so instruction (ii)'s own carve-out fires, and
arming should proceed unless CR-015 is amended.**

| basis | citation |
| --- | --- |
| CR-015's status is RULED, not proposed | `cr-015:3` |
| The arming is stated as an instruction inside the ruled CR, with a named packet and a named sequence position | `cr-015:338` — "T4.21-4 arms λ = 0.01 and is the last golden move" |
| The ruled coupling map expects the goldens to move for disaster BEHAVIOUR at T4.21-4, which only a non-zero λ produces | `cr-015:327` |
| The ruling's own famine semantics name a famine-class natural disaster as one of exactly two CAUSES. At λ = 0 that cause never fires outside a rig, so half the ruled semantics is unreachable in play | `cr-015:373-376` |
| CR-016 §3 states the mechanism does what CR-015 says; the quarrel is with magnitude, not with the ruling | `cr-016:127-128` |
| Under this reading, instruction (ii) is a CHANGE to a ruled decision. `CLAUDE.md` requires governed-but-wrong mechanisms to get the amendment BEFORE the code, and CR-015 §6.1 says so in the ruling itself | `cr-015:398-399` |

**READING B — NO. CR-015 ratified the mechanism and the derivation's frame, not the shipping of the
value, so instruction (ii) stands without amending CR-015.**

| basis | citation |
| --- | --- |
| The ruling text in §6.1 names the CAUSE and the smallest architecture-consistent mechanism. It never names a rate | `cr-015:369-401` |
| None of the nine explicit amendment lines N1–N9 fixes λ | `cr-015:403-479` |
| λ's own governing row says CHOSEN, not DERIVED — and said so at the governance commit, before the ruling (MEASURED, this lane, `git show eee90bc`) | `t4.21-architecture.md:461`; `adr-024:176` |
| §6.5's "no constant moved" ban explicitly exempts λ as a NEW constant with its frame stated | `cr-015:577` |
| `CLAUDE.md`: "Tuning data files and `TUNE` parameters is always allowed." λ is TUNE | `CLAUDE.md`, Governance |
| The rate was already placed on the Director's desk, by an orchestrator decision recorded in the OPEN CR-016. An instruction not to ship 0.01 is that awaited ruling arriving, not an override of CR-015 | `cr-016:179-198`, `:295-316` |
| CR-015's own §6.5 forbids choosing a rate to make the world survive. Declining to ship an unvalidated calibration is that constraint honoured | `cr-015:681-686` (T4.21-7 block) |

**What neither reading settles, and what only the Director can.** MEASURED (this lane, by reading
CR-016 §4 and D.4): instruction (ii) rules out the status quo ante. It does **not** select option 1,
2 or 3. The rate remains UNSET under either reading. **DIRECTOR DECISION REQUIRED** (R2), in two
parts:

1. **The record.** Does CR-015's arming directive stand as written, superseded by an append-only
   amendment, or read as never having ratified the value?
2. **The rate.** Which of CR-016's three options, and in which order. CR-016's own recommendation is
   "3 for the CAUSE, 2 for the MAGNITUDE, and NEITHER without the director" (`cr-016:150-152`,
   `:315-316`).

### 4.5 PROPOSED amendment text — NOT WRITTEN INTO CR-015 OR CR-016

**Status: PROPOSED. This lane has written nothing into either file.** Both blocks below are
append-only by construction: they go BELOW every existing block, and nothing above the line is
edited, which is the form both files already use (`cr-015:608`, `:670`; `cr-016:171`). They are
drafted for the Director to ratify, amend or reject. If Reading B is ruled, block 1's first
paragraph should be replaced with the one-line variant given after it.

**Block 1 — to be appended to `docs/adr/cr-015-famine-is-exceptional.md`, below the T4.21-7 block:**

```
## APPEND-ONLY — DIRECTOR AMENDMENT, THE DISASTER RATE IS WITHHELD (2026-09-19)

Recorded from the director's 2026-09-19 mandate. Nothing above this line is edited, including
the T4.21-4 block, which remains a true record of what was measured AT lambda = 0.01.

N10 — cr-015 §4, golden ladder (":338").  FROZEN TEXT: "T4.21-4 arms lambda = 0.01 and is the
last golden move."  AMENDED TEXT: "T4.21-4 armed lambda = 0.01, measured the fallout and
escalated it as CR-016; the arming is WITHDRAWN by this amendment. The shipped value is
lambda = 0.0 and the RATE IS WITHHELD pending the director's ruling on CR-016. The mechanism,
the reference class, the derivation, the severity band and the duration are UNCHANGED and are
not amended here; only the shipping of the VALUE is. T4.21-4's golden move stands as the last
behavioural golden move of the packet in the armed arm, and T4.21-7 returned all four goldens
to their pre-arming constants byte for byte."

WHAT THIS DOES NOT REOPEN. The four famine states, the two famine causes, the effective
deficit, bounded migration, the vacancy bound, the headroom cap and every N1-N9 amendment
stand exactly as ruled. Mandate item 1(A) remains RULED as a famine cause; what is withheld is
the FREQUENCY at which it fires unbidden, not its existence. Item 1(B), deliberate
abandonment, is live on the shipped config and is what makes famine reachable in play today.

WHY THE VALUE IS WITHHELD RATHER THAN RE-CHOSEN. Picking a lower lambda that made the world
survive would be the Libur fit §6.5 forbids. The measured fallout at 0.01 is CR-016 §2: the
CR-001 permanent dt-continuity detonator breaks by 2.2572 (battery rig, seed 1) and 2.5259
(DemographyRetune rig) per 1000 yr against a 0.1 bar, canonical.fedGrowthPerYear falls below
its immovable band, and the dev world goes 93,910 -> 38 over 1000 turns. lambda and
starvationMortalityMaxPerYear were derived against different reference classes and their
product was never checked.

THE FUTURE DERIVATION CHAIN, DIRECTOR-STATED. Any future value of lambda is to be derived
along: real-world hazard frequency -> geographic exposure -> settlement exposure -> event
probability -> its representation in a 10-year turn. The simulation determines CONSEQUENCES,
VULNERABILITY and RECOVERY; it must not invent the underlying FREQUENCY by tuning for
acceptable gameplay. Recorded here as the standing frame for the eventual ruling; §6.5's "no
Libur fit" is its M4-era statement.
```

**One-line variant of N10's first sentence, if the Director rules Reading B** (CR-015 never ratified
the value):

```
N10 - CLARIFICATION, NOT AN AMENDMENT. cr-015 §4's golden-ladder line ":338" is a SEQUENCING
note inside the blast radius, not a ratification of the value; lambda's own governing row read
CHOSEN, not DERIVED, at the governance commit eee90bc. The rate was never ruled and is now
explicitly WITHHELD. Nothing in §6 is amended.
```

**Block 2 — to be appended to `docs/adr/cr-016-armed-disaster-fallout.md`, below D.7:**

```
## DIRECTOR RULING (2026-09-19) — THE RATE IS WITHHELD; THIS CR CLOSES ON THAT BASIS

Nothing above this line is edited. §1-§5 remain T4.21-4's measurement at lambda = 0.01, and
D.1-D.7 remain the orchestrator's decision and its re-measurements.

THE RULING. lambda = 0.01 IS NOT SHIPPED. The famine-class disaster mechanism ships complete,
tested and INERT at hazardPerYear = 0.0, and the rate stays UNSET until it is derived along
the chain recorded as N10 in cr-015. Option 1 of §4 is REFUSED: the CR-001 detonator is not
wrong, it is working. Options 2 and 3 are not taken here and are not foreclosed; §4's
recommendation - 3 for the cause, 2 for the magnitude - stands as the recorded route, and
option 3 (the per-year food-balance sub-step, G8(c)) remains queued as M5-scale work.

STATUS: CLOSED - RULED. Closed on the RATE being withheld, not on the collision being
resolved. The G8/F4 dt artefact that makes the same physical event absorbed at dt 10 and
catastrophic at dt 5 is UNCHANGED and remains queued as G8(c). Re-arming at any value
re-opens every consequence in §2 and must not be done as a tuning edit; the cost is priced in
D.3 and the six SimConfigTests reds are its deliberate tripwire.

WHAT THIS LEAVES STANDING, DELIBERATELY. Three dispositions read as quarantines rather than
coverage while the rate is withheld, each reversing with the same one data edit:
PopulationTests.Reconciliation_FromLedgerAlone's starved > 0 guard; ChronicleTests.
Annals_TwinIdentical's famine line; and MigrationTests.MagnitudeCorridor_FedPhaseDrift_
WithTeeth's UPWARD rate-lever tooth (x1.0082 at lambda = 0 against x1.88 armed). All three are
recorded at their sites with both readings and are carried in docs/queue.md.
```

### 4.6 THE FUTURE DIRECTION FOR THE DISASTER RATE, AND WHAT THE CURRENT DESIGN DOES AND DOES NOT FIT

**The chain, DIRECTOR-STATED** (relayed through the orchestrator; UNVERIFIED as a repository
document, see §0): real-world hazard frequency → geographic exposure → settlement exposure → event
probability → its representation in a 10-year turn. The simulation determines consequences,
vulnerability and recovery, and **must not invent the underlying frequency by tuning for acceptable
gameplay**.

**What `DisasterSystem` as shipped already fits.**

| chain step | fit | evidence |
| --- | --- | --- |
| real-world hazard frequency | **FITS.** λ's reference class is explicit real-world frequency: England 1300–1700 ≈ 5–6 famine-class events / 400 y (≈ 1/70 y); France by région 1500–1800 (Le Roy Ladurie) 1/50–1/100 y; band 1/50–1/150, round central value | `t4.21-architecture.md:461`; `adr-024:176` — MEASURED as text, this lane |
| event probability | **FITS, and is dt-exact.** `p = 1 − exp(−λ·dt)`, the exact integration of a per-year hazard, composing across dts | `Sim.Core/Systems/Disaster/DisasterSystem.cs:33-40`; ADR-024 §4 |
| 10-year-turn representation | **FITS, with its biases stated rather than hidden.** No new onset while active plus once-per-turn booking gives ≈ 4.7 % of onsets truncated at dt 10 and a ≈ 4.8 % rate bias at dt 0.5; the exact renewal is queued | `DisasterSystem.cs:50-56`; `t4.21-architecture.md` §3.3 |
| consequences, vulnerability, recovery determined by the simulation | **FITS, and this is the design's strongest conformance.** The system multiplies food output and reads nothing else — no population, no stores, no deficits, no weather. Vulnerability is `ρ` and the granary; the ladder decides FAMINE from the food balance; recovery is the ordinary demographic and migration response | `DisasterSystem.cs:23-30`; CR-015 N1 (`cr-015:405-412`) |
| "must not invent the frequency by tuning for acceptable gameplay" | **FITS, and the repository already says it in its own words.** CR-015 §6.5's "No Libur fit"; CR-016 D.1 reason 2, "choosing a lower rate that makes the world survive would be tuning-to-outcome" | `cr-015:587-589`; `cr-016:185-188` |

**What the current design does NOT fit.**

| chain step | gap | evidence | label |
| --- | --- | --- | --- |
| **geographic exposure** | **ABSENT.** λ is one global scalar. `DisasterSystem` reads no terrain, no climate, no water, no latitude, no biome; every settlement carries the same per-year hazard. Spatial correlation and a weather-conditioned hazard are explicitly DEFERRED to the queue | `DisasterSystem.cs` step (`:33-41`) draws `uH` against a config-wide `p`; `t4.21-architecture.md` §3.3, "Spatial correlation and a weather-conditioned hazard are DEFERRED (queue)" | MEASURED (this lane) |
| **settlement exposure** | **ABSENT ON THE HAZARD SIDE, PRESENT ONLY ON THE CONSEQUENCE SIDE.** The one settlement-specific quantity in the derivation is `ρ_ship = 1.3` (Libur's weather-mean grain surplus ratio), and it enters the SEVERITY threshold, not the frequency. The reference class's own downward step — "a single settlement's hinterland sees fewer than a région" — is an un-quantified prose adjustment where the chain wants a computed exposure term | `t4.21-architecture.md:461`, `:470-486`; ADR-024 §3 | MEASURED (this lane) + INFERRED |
| **severity and duration derived against a hazard catalogue** | **HALF-FITS, and the mandate's reading is correct on the half that does not.** `s_max = 1.0` and `D = 5.0` ARE catalogue-derived (Irish potato 1846 ≈ 25 % of normal; 1601–03 near-total locally; the 3–7 y band from 1315–22, 1601–03, 1695–97, the 1690s seven ill years, 1845–49). **`s_min = 0.75` is NOT.** It is derived against the effective buffer: "a famine-class event must be able to exhaust the full buffer of a settlement at the shipped surplus ratio ALONE at the coarsest era dt", `s_min · D > (dt_max(ρ_ship − 1) + G)/ρ_ship`, threshold 3.46 production-years. The document names this honestly — "a DIMENSIONAL derivation … not a corridor fit" — but dimensional against an INTERNAL economic quantity is still not a hazard catalogue | `t4.21-architecture.md:470-486`; `adr-024:181-190` (`## §3`, the `s_min` derivation) | MEASURED (this lane) — and it is exactly what the mandate's parenthesis says |

**PROPOSED, for the Director to ratify or reject — the shape a chain-conformant rate would take.**
Offered as design direction only; no packet is proposed and no code is proposed.

1. λ becomes a **per-settlement rate computed from exposure**, not a global scalar: a base hazard
   frequency from the catalogue, modified by the settlement's own geography — the terms the Spine
   already carries (terrain, water, the catchment) rather than new state.
2. The severity band is re-derived **against the hazard catalogue's own severity distribution**, and
   `B_eff` reverts to what it physically is: the thing the event is measured AGAINST when the
   simulation computes the consequence, not the thing the event's size is chosen FROM.
3. **The kind registry already anticipated this.** `t4.21-architecture.md` §3.3 states that a second
   kind with sub-class severities would require `FoodState.struck` to read a per-row famine-class
   predicate `Severity·D·ρ_ship ≥ B_eff`. A catalogue of hazard kinds is the natural carrier of
   step 1 and step 2 together. RATIFIED as queued (D4); PROPOSED as the chain's landing site.

**A TENSION THIS LANE MUST SURFACE AND WILL NOT RESOLVE.** CR-016 option 2 is "re-derive λ against
the demographic kernel, not against the historical reference class alone" (`cr-016:137-147`). Read
against the Director-stated chain, that instruction points the wrong way: re-deriving the HAZARD
FREQUENCY against the MORTALITY KERNEL derives frequency from a desired demographic outcome, which
is the shape of the thing the chain forbids. Under the chain, the term that should move is the
VULNERABILITY or RECOVERY side — `starvationMortalityMaxPerYear`, or the dt artefact of option 3 —
not the frequency. CR-016 itself half-says this: "Either λ or `starvationMortalityMaxPerYear` is
denominated against a different reference than the other" (`cr-016:139-141`) — it names both and
then proposes moving λ. **DIRECTOR DECISION REQUIRED:** whether CR-016 option 2 is re-pointed at the
mortality kernel, kept as written, or dropped in favour of option 3 alone. INFERRED throughout this
paragraph; nothing here is a ruling.

---

## 5. G1 — THE HARVEST-WEATHER DECADE VARIANCE

### 5.1 The finding and its derivation

RATIFIED as escalated: `docs/adr/cr-015-famine-is-exceptional.md:524-570` (§6.4), headed
"**ESCALATED — requires a separate director ruling: G1, the harvest-weather decade variance (F1)**".
Restated in `docs/t4.21-director-report.md:1156-1198` (§15.2).

`HarvestWeatherSystem.cs:42-45` holds the log-deviation `x` at STATIONARY variance σ² for every dt
(`ρ_AR = e^{−dt/τ}`, innovation `σ√(1 − ρ²)`) — the deliberately ratified T3.4b/T3.4c invariant that
stops the era table altering the climate. `ProductionSystem.cs:239, :306` then apply
`exp(x − σ²/2)` to the WHOLE turn's output. A turn's harvest is the turn-MEAN of the yearly
multipliers, and the variance of the mean of an OU process over a window `T` is

```
Var(mean over T) = σ² · g(T/τ),   g(T/τ) = 2(τ/T)[1 − (τ/T)(1 − e^{−T/τ})]
g = 0.427 at T = 10 y (τ = 3):  σ_decade    = 0.2936 × √0.427 = 0.192
g = 0.948 at T = 0.5 y:         σ_half-year = 0.286
```

**Consequences as recorded.** At the canonical dt = 10 the shipped decade multiplier is **1.5× too
variable in σ**. The 5th-percentile decade reads **0.59** as shipped where the yearly σ/τ imply
**0.72**. `P(decade multiplier ≤ 0.57)` is **3.9 % as shipped against 0.25 % corrected**. At Libur's
`ρ ≈ 1.3` the recorded t117 draw (0.4427) produced `d = 0.30`; the cascade's M3 arm at the corrected
σ produced 0.079. Under the shipped variance an ordinary SEVERE decade produces a LARGER dt-10
deficit at a `ρ = 1.3` settlement than a famine-class disaster does (≤ 0.20, ADR-024's table).

**Evidence class, stated precisely, because the report states it precisely.** `g = 0.427`, the
"1.5× too variable" and the 3.9 % / 0.25 % pair are **algebra over the shipped constants, not
measurements** — the report labels them INFERRED and records that **no lane sampled the shipped
decade multiplier's variance** (`docs/t4.21-director-report.md:1178-1183`). What is CONFIRMED is the
code forms they are derived from, and that `HarvestWeatherSystem.cs` is untouched in the packet diff.

### 5.2 The three options on record

RATIFIED, `cr-015:558-570`:

- **(a) LEAVE** — the mandate's "do not clamp" read strictly; the redesign separates famine from
  variability by CAUSE and by the disaster band's yearly-tail position (`z = −4.57` for the band's
  mildest active year); ordinary SEVERE decades stay as lethal as spec §9 R1 states.
- **(b) APPLY `g`** — `multiplier = exp(√g·x − g·σ²/2)`, `g(dt/τ)` computed in
  `HarvestWeatherSystem.cs:138-142`; σ, τ, the AR(1) state, the spatial blend and the mean-one
  property untouched; goldens move.
- **(c) apply `g` AND re-derive σ — REFUSED**, because σ is yearly and correct and re-deriving it is
  tuning.

**Recommendation on record:** (b), as its OWN packet with its OWN golden attribution step, with
`HarvestWeatherTests` moment pins re-derived at `g` and the decade variance pinned dt-invariant.
**Interim disposition: (a).** `cr-015:568` — "**Until ruled: (a).**"

### 5.3 The mandate's question, answered on the ratified documents alone

**G1 STILL REQUIRES A DIRECTOR RULING. It is neither closed nor deferred in the sense of being
disposed of.** RATIFIED, on four independent statements on this tree:

1. `cr-015:3-8` — the status line: "**G1 (harvest-weather decade variance) is ESCALATED in §6.4 and
   awaits its own director ruling**".
2. `cr-015:568` — "Until ruled: (a)", which makes (a) an INTERIM state contingent on a ruling, not a
   disposition.
3. `docs/milestones.md:318-319` — the M4 entry: "G1, the harvest-weather decade variance, is
   escalated and open".
4. `docs/t4.21-director-report.md:1156` — "**UNRESOLVED; DIRECTOR ONLY**", and `:1428-1437` lists it
   THIRD in the recommended ruling order.

**No ruling document exists.** MEASURED (this lane): `grep -rn "G1" docs/queue.md` returns one entry,
`docs/queue.md:1397`, which reads "ESCALATED … its own packet with its own golden attribution step if
the director rules (b). Weather untouched until then." Nothing on this branch records a G1 ruling.

**The distinction that matters for the closure decision.** ACTION on G1 is deferred — weather ships
byte-identical and every T4.21 mechanism was specified to be correct under either ruling
(`cr-015:566-570`). The RULING is not deferred; it is outstanding. INFERRED: M4 can close over an
outstanding G1 exactly as it closes over other outstanding rulings, but a closing edit that records
G1 as "deferred" rather than "awaiting ruling" would misstate `cr-015:3`.

### 5.4 THE FOURTH PATH — surfaced, not chosen

**DIRECTOR-STATED** (relayed; UNVERIFIED as a repository document, §0). The mandate's stated future
direction for weather is: a **deterministic, bounded climate signal**; the chain **climate state →
rainfall → water → soil → agriculture → food**; **ENSO-like multi-year structure**; and **extreme
weather as climate and hazard STATES rather than unbounded noise**.

**This is a FOURTH path, and none of CR-015 §6.4's three options describes it.** MEASURED (this lane,
by reading `cr-015:558-570` in full):

- Option (a) leaves the stochastic AR(1) lognormal exactly as it is.
- Option (b) applies `g` to the same stochastic AR(1) lognormal, explicitly leaving "σ, τ, the AR(1)
  state, the spatial blend and the mean-one property untouched".
- Option (c) applies `g` and re-derives σ — of the same stochastic process. REFUSED.

**All three operate on a stochastic, unbounded, single-scale weather process.** The fourth path
replaces that process: a deterministic bounded signal with multi-year structure, and extreme weather
promoted from a tail draw to a STATE. INFERRED, from the option texts and the direction as relayed.

**Three consequences the Director should weigh, none of which this lane resolves.**

1. The fourth path would **interact with, and possibly subsume, the G1 defect**. If the decade
   multiplier comes from a bounded multi-year climate state rather than from `exp(x − σ²/2)` applied
   to a stationary AR(1), the turn-mean variance question is answered by construction rather than by
   a correction factor `g`. INFERRED.
2. It also **interacts with CR-016 and with the whole disaster design**, because "extreme weather as
   climate/hazard STATES" is the same architectural move as a hazard catalogue (§4.6). G1 and CR-016
   already bear on the same question — how often a settlement sees a decade it cannot absorb — and
   the report records that ruling one changes what the other's evidence means
   (`docs/t4.21-director-report.md:1434-1437`). The fourth path couples them harder. INFERRED.
3. It would touch **`m3-spec.md:52-53`'s ratified T3.4b/T3.4c invariant** and the mandate's own
   "weather stays stochastic and unclamped" from 2026-09-17 (`cr-015:376`). Those are frozen and
   ruled items. INFERRED: adopting the fourth path is an amendment to ratified weather semantics,
   not an option selection inside §6.4.

**Choosing among (a), (b), (c) and the fourth path is a Director act. This lane does not choose, and
records that the fourth path is not on CR-015 §6.4's list and would need to be put there — or put in
its own CR — before anyone could implement it.** DIRECTOR DECISION REQUIRED (R3).

---

## 6. MERGE READINESS

### 6.1 WHAT IS GREEN

| item | evidence | label |
| --- | --- | --- |
| **The three gate scripts** | `scripts/check-banned-constructs.sh` exit 0; `scripts/check-read-isolation.sh` exit 0; `scripts/check-readonly-proof.sh` exit 0 — run on `9f6c6ae` | **MEASURED (this lane)** |
| **The suite, re-measured on this tree** | `dotnet build -c Release`: **0 warnings, 0 errors**. `dotnet test -c Release`: Sim.Tests **885 passed / 0 failed / 4 skipped** (6 m 53 s), Sim.Ui.Tests **296 / 0 / 0** (34 s) — run on `9f6c6ae` | **MEASURED (this lane)**. Agrees test for test with T4.21-8's Release and Debug figures at `1fa8c1d` (SECONDARY, `cr-016:407-425` D.7); `1fa8c1d..9f6c6ae` and `64a3f2f..9f6c6ae` are **docs-only** (MEASURED, this lane, `git diff --name-only`) |
| **The four skips are the right four** | `grep -rn "Skip *=" Sim.Tests/` returns exactly the four long-standing manual measurement rigs; CR-015 N9's two lifts are not re-skipped | **MEASURED (this lane)** |
| **The red set is empty, and that was checked rather than assumed** | Every one of CR-016 §5's six deliberate reds passes at λ = 0, which is itself the evidence that the arming was their sole cause | MEASURED (SECONDARY, `cr-016:337-345`) |
| **Determinism** | five-for-five, with `ci.yml`'s `FOUNDED_GOLDEN` re-derived from the binary rather than copied; twin, replay-from-log and save/load-continue hash-identical with an active `DisasterRow` | MEASURED (SECONDARY, report §14; CR-016 §3) |
| **Goldens** | all four behavioural world goldens moved across the packet and returned byte for byte at the disarm; the T4.21-1 strip control reproduces pre-packet pins byte-for-byte | MEASURED (SECONDARY, `t4.21-architecture.md:961`; `cr-015:336-339`) |
| **Corridors** | every canonical corridor that GATES is green on 20/20 seeds; `fedGrowthPerYear` 0.00075167, mid-band; CR-001's permanent detonator 0.0056 against a 0.1 bar | MEASURED (SECONDARY, report §12.3–§12.4) |
| **No band, constant, window or quarantine was moved** | `corridors.json` byte-identical to `8f7f9da` through both the arming and the disarming | MEASURED (SECONDARY, `cr-016:208`, `cr-015:678-680`) |
| **Mutation battery** | 13/13 killed, zero golden-only kills, every mutant carrying at least one semantic killer (ADR-015 §7.2 satisfied) | MEASURED (SECONDARY, report §13) |

### 6.2 WHAT IS RED

**Nothing in the suite.** MEASURED (this lane): 0 failed in Sim.Tests and Sim.Ui.Tests in Release on
`9f6c6ae`, and all three gate scripts exit 0.

The red items are governance, not test results: blockers **B1** (no Director exit session), **B2**
(no merge ruling), **B3** (no `m4-exit` tag), **B4** (the closing artefact is stale).

### 6.3 WHAT IS UNRESOLVED

| # | unresolved | label |
| --- | --- | --- |
| U1 | **CR-016 — the disaster rate.** OPEN; three options, none taken; plus the CR-015 conflict surfaced in §4.4 | DIRECTOR DECISION REQUIRED |
| U2 | **G1 — the decade variance.** Escalated, interim (a), no ruling; and a fourth path now in play (§5.4) | DIRECTOR DECISION REQUIRED |
| U3 | **B5 — the density-quarantine window breach**, measured but NOT VERIFIED, invisible to both instruments | MEASURED, not actionable under ADR-015 §6 |
| U4 | **ADR-018 §11's premise**, false at both rates; the 4b upward tooth quarantined in place | MEASURED (SECONDARY) |
| U5 | **G8 / F4 — the dt-versus-buffer artefact**, the mechanism behind CR-016, accepted as inherited and queued as M5-scale | RATIFIED as queued |
| U6 | **The §28 asymptote is rig-proved but not demonstrated on a real world** — the canonical world sits at ~0.60–0.66 of its limit for 300 turns and never approaches the ceiling; the settling measurement (a constant-weather 1000-turn run) was not run | MEASURED (SECONDARY, report §15.7) |
| U7 | **The exceptional arm is unexercised in play.** Zero FAMINE turns, zero disasters, zero abandonment across 1 932 + 39 852 measured settlement-turns. Everything validated in play validates the ORDINARY arm | MEASURED (SECONDARY, report §15.6(f)) |
| U8 | **The adaptation boundary `a` is unfalsifiable on the shipped world** — `a = 0.20` and `a = 1/3` produce bit-identical worlds, because the largest deficit anywhere in 300 turns is 0.0508 | MEASURED (SECONDARY, report §15.6(c)) |
| U9 | **Known-open items 5, 6, 7, 8, 9, 11**, all still open and unchanged (§3) | RATIFIED as open |

### 6.4 WHAT THE MERGE WOULD CARRY — THE THREE-WAY DISTINCTION

**(i) The M4 CERTIFIED BASELINE — `main` @ `dbef61a`.** Director-certified, merged, tagged as the M4
baseline in its own merge commit message. RATIFIED. It carries T4.1–T4.16: the Empire Control
Foundation, store bounding, colonization, transport and the river-aware lattice, the migration
rework, comfort as a stock, the clone measurement, schema through v24. It does NOT carry the famine
semantics, the disaster mechanism, bounded migration, the headroom cap, or schema v25.

**(ii) The five post-certification packets on the candidate — T4.17 to T4.21.** MEASURED (this lane,
git): 138 commits ahead of `dbef61a`.

| packet | what it adds | state |
| --- | --- | --- |
| T4.17 | session records and `sim inspect` | on the candidate, unmerged (`milestones.md:310-315`) |
| T4.18 | the founding population transient | on the candidate, unmerged |
| T4.19 | the Glass Box; ruled CR-013 and CR-014; corrected the founding demographic vector; retired the Artisan timing window for structural latch tests | on the candidate, unmerged |
| T4.20 | food legibility, ruled observability-only after its audit established no defect | on the candidate, unmerged |
| T4.21 | famine semantics, bounded migration, demographic shock integration; `FoodState`, `DisasterSystem`, `DisasterRow`, `FoodHeadroom`, schema **v24 → v25**; CR-015 (RULED), CR-016 (OPEN), ADR-024/025/026 | **IN PROGRESS on this branch**, unmerged, and explicitly not part of the certified baseline (`milestones.md:316-324`) |

**What T4.21 specifically would carry into `main`:** schema v25 and the `DisasterRow` table; a
famine-class disaster mechanism that is **complete, tested and INERT**; the four-state `FoodState`
ladder and the effective deficit; bounded migration with exit openness ω, destination shares, basin
caps and the vacancy bound; the headroom growth cap; one **OPEN CR** (CR-016) and one **escalated,
unruled finding** (G1); and the three dispositions that read as quarantines rather than coverage
while the rate is withheld. It would also carry **B5**, the unverified density-window breach.

**(iii) What "closing M4" would mean for each.**

| | what closure means |
| --- | --- |
| **(i) the baseline** | Nothing changes. It is already certified. Closure would make it the ancestor of the tagged `m4-exit` rather than the endpoint |
| **(ii) the five packets** | Closure means the Director rules the merge (B2), the candidate becomes `main`, and the `m4-exit` tag is cut on it (B3). Each packet's own known-opens travel with it |
| **(iii) M4 as a milestone** | Closure means the `docs/milestones.md` M4 entry is brought current (B4) and the exit criteria at `:342-351` are re-read against the merged tree: the criteria "all packets accepted", "Director exit session", and "the tag" move from open to met, and the known-open list's surviving items become M5-era carry-forward. **Closure does NOT mean CR-016 or G1 are settled.** Both would travel into M5 as open governance, exactly as CR-003 and CR-005 travelled before them. RATIFIED by precedent (`docs/milestones.md:262`, M3's known-open list completed at the exit ruling) |

### 6.5 MERGE READINESS — THE VERDICT THIS LANE IS PERMITTED TO GIVE

**Technically ready.** The suite is green in Release on this tree (MEASURED, this lane) and in both
configurations at `1fa8c1d` (SECONDARY), the gate scripts pass, determinism
is five-for-five, the goldens are attributable, every gating corridor is in band, the mutation
battery has teeth, and no band, constant or quarantine was moved by anyone. MEASURED.

**Governance-incomplete.** B1, B2, B3 and B4 are all outstanding, and all four are Director acts or
consequences of one.

**With one unverified measured breach on the candidate** — B5 — which under ADR-015 §6 nobody may
act on until a verdict returns, and which no CI instrument can see (item 11).

**`docs/t4.21-director-report.md:1408-1424` recommends MERGE FIRST, then rule CR-016, then rule G1.**
That is a prior agent's recommendation and is SECONDARY evidence. **Merging is a Director ruling
under `CLAUDE.md`, and this lane does not perform it and does not recommend for or against it.**

---

## 7. FINDINGS INDEX — WHERE A DOCUMENT AND THE TREE DISAGREE

| # | finding | label |
| --- | --- | --- |
| F0 | The known-open list has ten rows; the reconciliation has eleven. Item 11 exists only in the second table | MEASURED (this lane) |
| F1 | The §6 table says "the four post-certification packets"; the same entry's prose says five. T4.21 is the fifth | MEASURED (this lane) |
| F2 | "Scarcity can bite — MET, starvation reachable on the dev world" is true on a much narrower basis on the candidate: 0 dev starvation over 900 turns at λ = 0; 451 canonical deaths in 2 of 20 seeds at 650 turns; zero at 300 turns on every seed | MEASURED (SECONDARY) |
| F3 | Known-open item 1 is recorded CLOSED / VERIFIED, and on the candidate the quarantined density corridor has drifted 3.9 % below its own recorded window on seed 3, unverified and invisible to both instruments | MEASURED (SECONDARY) — blocker B5 |
| F4 | Known-open item 2's "two dev seeds red by design" is no longer true of this tree; both `Dev_MalthusCorridors_AllInBand` cases pass at λ = 0 | MEASURED (SECONDARY + this lane's suite figure) |
| F5 | Known-open item 4a is recorded OPEN / REQUIRES DIRECTOR RULING; the tree has DISCHARGED it. The skip is lifted, the test is live and green, and it is abandonment-driven so the disarm did not touch it | MEASURED (this lane) |
| F6 | Known-open item 4b is recorded OPEN / needs an owner; the tree has PARTIALLY discharged it — corridor and both downward teeth live, upward rate-lever tooth quarantined in place — and the discharge revealed that ADR-018 §11's premise is false at BOTH rates, which is a different open question from the one recorded | MEASURED (this lane + SECONDARY) |
| F7 | `docs/current-state.md` has gone stale again in two places: `:17` still calls `t4.19-glass-box` the current candidate with a 722 / 4 / 6 suite, and there is no entry for the director-report commits | MEASURED (this lane) |
| F8 | Known-open item 11 is unchanged verbatim in `ci.yml`, and B5 is item 11's predicted failure mode having occurred | MEASURED (this lane) |
| F9 | The Director's 2026-09-19 mandate exists nowhere in the repository, so every comparison against CR-015 in §4.4 weighs a RATIFIED document against an UNVERIFIED relay | MEASURED (this lane) |
| F10 | CR-016 option 2 ("re-derive λ against the demographic kernel") points against the Director-stated derivation chain, which forbids deriving the underlying frequency from a desired outcome | INFERRED |

---

## 8. WHAT THIS LANE COULD NOT VERIFY

| # | item | why |
| --- | --- | --- |
| UV1 | Every armed-arm number in §4.3 | Reproducing them requires arming `hazardPerYear`, which is a data change this phase prohibits. They are MEASURED by T4.21-4 and reproduced by validation lane C, both SECONDARY under GOV-4 |
| UV2 | The 6 / 31 `SimConfigTests` split on an armed tree | Same reason. The report itself labels it PROBABLE and records that no validation lane re-measured it (`report:1147-1152`) |
| UV3 | Lane B's 20-seed density and starvation sweeps, and lane A's playtest decomposition | Multi-hour measurement rigs; out of this lane's scope. All SECONDARY |
| UV4 | Whether the T4.19-E structural set S1–S5 has been re-examined since (item 7) | No record found on this branch; the set was not run in isolation here |
| UV5 | The content and authority of the 2026-09-19 mandate | It is not a repository document (F9). This lane treats its instructions as DIRECTOR-STATED-BY-RELAY, never as ratified text |
| UV6 | The exact G1 numbers (`g = 0.427`, 1.5×, 3.9 % / 0.25 %) | They are algebra over the shipped constants, not measurements. No lane sampled the shipped decade multiplier's variance (`report:1178-1183`). This lane did not sample it either |

---

**END OF OUTPUT A.** Nothing in this document is a ruling. The four lists in §1 are the deliverable;
§2 to §6 are their support; §7 is where the documents and the tree disagree; §8 is what this lane
could not see.
