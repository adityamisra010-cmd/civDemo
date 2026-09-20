# M4 FINAL CLOSURE AUDIT

**Directed packet, 2026-09-20. FINAL M4 AUDIT ONLY.** No production code, schema, test, data,
configuration, CI file, golden, corridor or specification was modified. Nothing merged. No M5 or
future-architecture work performed. The post-M4 civilization-architecture systems are explicitly out
of scope and are not designed, restructured or expanded here.

**Tree, MEASURED this pass.** Branch `claude/civdemo-work-b1z2y4` @ **`b6eeace`**, working tree
clean, synced with `origin` (0 ahead / 0 behind). `main` @ **`dbef61a`** ("Merge M4 completion
(director-certified): the M4 baseline"), untouched. `dbef61a..HEAD` = **160 commits**; the non-doc
diff is 101 files, +21,658 / −548 across `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests`, `scripts`.
Schema is **v25** on the candidate against **v24** on `main`.

**Authority used.** `docs/m4-spec.md` §6 EXIT CRITERIA and `docs/milestones.md`'s M4 exit-gate entry.
No new M4 requirement is invented anywhere in this file.

---

## SECTION A — M4 STATUS

**M4 is NOT CLOSED, and every remaining blocker is a Director action, not an agent deliverable.**

The engineering is done and green. What is missing is the closure ceremony that `m4-spec.md` §6
ratifies and that `docs/milestones.md:369` states in one sentence:

> **"M4 does not close on this entry. It closes on the director's play session against the candidate
> and his merge ruling, as M3 did."**

That sentence is the controlling text. Three of the four blockers below are named directly in it;
the fourth is §6's own closing-artefact requirement.

One further item — the density-quarantine window breach — is **conditional on the merge decision**
and is a process/instrument issue, not a gameplay defect. It is set out in full in Section C.

---

## SECTION B — M4 ACCEPTANCE CHECKLIST

### B.1 The ratified §6 exit criteria, against this tree

| § 6 criterion (`docs/m4-spec.md:375-386`) | status this pass | evidence |
| --- | --- | --- |
| All packets accepted; each merged on a director ruling | **NOT MET** | T4.1–T4.16 merged at `dbef61a`. T4.17, T4.18, T4.19, T4.20, T4.21 are unmerged on the candidate. MEASURED: `git log --oneline -1 main`. |
| Scarcity can bite; five Q-B predictions reported individually | **MET** | `docs/milestones.md:344`; re-confirmed by the green famine scenario set (`FamineScenarioTests`) this pass. |
| Determinism suites green; xproc; first-reign shape asserts standing | **MET** | MEASURED this pass: full suite exit 0 (see B.3). CI carries `determinism` and `determinism-xproc` jobs (`.github/workflows/ci.yml:39`, `:73`). |
| Goldens pinned with dated history; driven golden extended | **MET** | `docs/milestones.md:347`. No golden moved by this packet or by the design phase. |
| Calibration battery green across ≥20 seeds with proven teeth; quarantined corridors reported with measured ranges rather than silently gating | **AT RISK ON THE CANDIDATE** | Battery green, but the density quarantine's recorded window no longer matches the candidate. See Section C, blocker **B5**. |
| The nightly has been green, and someone has read it | **INSTRUMENT MET; READING IS THE DIRECTOR'S** | `docs/milestones.md:349`. The instrument gap is real and unchanged — `ci.yml:231-233`'s `gated()` short-circuits on `quarantine.active` and never reads `quarantine.window`. MEASURED this pass by reading the file. |
| Director exit session from the CI zip, replaying hash-identical, with a T3.12a replay report | **NOT MET** | `docs/milestones.md:350` — "AWAITING THE DIRECTOR — the machinery is complete and tested". |
| `milestones.md` M4 entry with its known-open list; `m4-exit` Release | **NOT MET** | Entry exists but is stale against this tree; `git tag` returns `m3-exit` only. |

### B.2 The twenty-one coverage areas

| # | area | status | evidence (MEASURED this pass unless stated) |
| --- | --- | --- | --- |
| 1 | Strategic polity / player empire architecture | **DELIVERED** | `PolityRow`, `ControlRow`, `CapitalRow`, `CommandSource`, `EmpireQuery` present; 21 references in `Sim.Core/State/WorldState.cs`. Founding instantiates the player Empire in the same operation that creates settlements. |
| 2 | Settlement control | **DELIVERED** | `ControlRow` one-or-none cardinality (D-037 A3); `RevoltSystem` removes the relation at exactly-zero happiness; `Controls` is a sanctioned shared table. |
| 3 | Player and AI common order pathway | **DELIVERED** | `Sim.Core/Kernel/OrderLog.cs`, `OrderValidation.cs`; the issuing actor is the existing `PolityId` — a projection, no second identity, no schema field. |
| 4 | Economy / resource ownership | **DELIVERED** | `Ledger`, `ConservedMath`, `ConservationAuditor`; Price, Trade, Production, Consumption systems all present and green. |
| 5 | Tax / governance foundations authorized for M4 | **CORRECTLY ABSENT** | RATIFIED exclusion: *"No money, no treasury, no taxation"* (`docs/milestones.md:329`). M5 owns it; GOV-2 §1a rules M5 taxes **in kind**. |
| 6 | Happiness | **DELIVERED** | Derived reading, never a stock, not serialized; `scripts/check-read-isolation.sh` **PASS** this pass. |
| 7 | Migration | **DELIVERED, with a quarantine** | T4.10/T4.12 plus T4.21-2 bounded flight. `migrationGrossPerDecade` quarantine active — reports rather than gates. |
| 8 | Population / demographics | **DELIVERED** | T4.18 founding transient; T4.21-3 headroom growth cap; CR-001 dt-continuity detonator intact at λ = 0. |
| 9 | Food | **DELIVERED** | T4.2 store bounding (prediction met to four decimals); T4.20 legibility, ruled observability-only; T4.21 four-state food ladder. |
| 10 | Founding conditions | **DELIVERED** | T4.18 + T4.19 founding demographic vector correction; ADR-017/ADR-018 govern variation and spacing (ADR-017's own status is open — R8). |
| 11 | Artisan mechanism | **DELIVERED** | T4.19 retired the timing window in favour of structural latch tests; live predicate `artisan_share > 0.05` in `goods.json`. |
| 12 | Density | **MEASURED BREACH ON CANDIDATE** | Seed 3 = `0.35415668759623087` vs recorded window floor `0.3685744951368359`. See Section C **B5**. |
| 13 | Determinism | **GREEN** | `scripts/check-banned-constructs.sh` **PASS**; determinism tests green in the full suite. |
| 14 | Replay / save-load | **GREEN** | `Snapshot`, `ReplayReport`, `SessionManifest`, `SessionTrace`, `WorldHash` present; round-trip tests green. |
| 15 | Schema | **v25 ON CANDIDATE, v24 ON MAIN** | `Sim.Core/Kernel/CanonicalSchema.cs:77` — Disasters table appended after Structures. A "v25" collision with the unmerged `m5-full-build` branch is stated in the code, not hidden. |
| 16 | UI / read-only observability | **GREEN** | `scripts/check-readonly-proof.sh` **PASS**; `Sim.Ui.Tests` 296/296. |
| 17 | Known quarantines | **REPORTED** | CR-003 Malthus (standing ruling, two dev seeds red by design); `migrationGrossPerDecade`; `densityPerArableKm2`. |
| 18 | Test status | **GREEN** | **MEASURED this pass:** `Sim.Tests` 885 passed / 0 failed / 4 skipped (28m25s); `Sim.Ui.Tests` 296 passed / 0 failed / 0 skipped (1m36s); exit 0. The 4 skips are pre-existing **manual measurement rigs** (`FoundingVariationItem0Tests.cs:58`, `WaterRouteCounterfactualTests.cs:88` and `:142`, `HousingBeforeColumnTests.cs:40`) — deliberate, each with a run-manually reason. |
| 19 | Documentation | **STALE IN PLACES, NON-BLOCKING EXCEPT B4** | `docs/current-state.md` carries its own staleness correction; the `milestones.md` M4 entry is stale — that one **is** blocking, as B4. |
| 20 | Cross-platform status | **RESOLVED** | CR-013 **CLOSED / ACCEPTED**; ADR-022 **ACCEPTED** — Linux x64 is the reference platform, Windows under non-blocking surveillance, no equation changed. |
| 21 | M4 governance / process requirements | **PARTIALLY MET** | S8 §4.1 conformance on all four requirements is recorded (`docs/milestones.md:287-292`). The closure ceremony — exit session, merge ruling, tag, closing edit — is outstanding. |

### B.3 Known-open items 4a and 4b are discharged

`docs/milestones.md:360` and `:361` record both as **OPEN** and skipped. **MEASURED this pass:
neither carries a `Skip` attribute any longer.** `FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes`
(`MigrationTests.cs:398`) and `MagnitudeCorridor_FedPhaseDrift_WithTeeth` (`MigrationTests.cs:666`)
are live and green in the 885, and `S_Abandonment_TriggersFamine` (`FamineScenarioTests.cs:498`)
carries the re-aimed assertion CR-015 N9 specified. The `milestones.md` entry has not been updated
to say so — which is part of blocker **B4**, not a separate defect.

---

## SECTION C — REMAINING M4 BLOCKERS

### B1 — The Director's exit session
- **blocker:** the exit play session against the candidate, replaying hash-identical, with a T3.12a replay report attached.
- **exact source:** `docs/m4-spec.md:384-385`.
- **exact M4 requirement:** *"Director exit session from the CI zip, log replaying hash-identical, with a T3.12a replay report attached — M4's exit should not make the director the measuring instrument again."*
- **observed condition:** not run. `docs/milestones.md:350` reads "AWAITING THE DIRECTOR — the machinery is complete and tested".
- **why it violates the requirement:** the criterion names an act only the Director performs.
- **minimum action:** the Director runs the session against `b6eeace` and attaches the replay report. No agent action can discharge this.

### B2 — The merge ruling for the five post-certification packets
- **blocker:** T4.17, T4.18, T4.19, T4.20, T4.21 are not merged.
- **exact source:** `docs/m4-spec.md:375`; `CLAUDE.md` Environment section.
- **exact M4 requirement:** *"All packets accepted; each merged on a director ruling."*
- **observed condition:** `main` @ `dbef61a`; 160 commits on the candidate, 101 non-doc files changed.
- **why it violates the requirement:** `CLAUDE.md` states the loop — *"the DIRECTOR RULES on acceptance, and only then the agent performs the merge to `main` on that explicit ruling — never on its own judgement."* The ruling has not been given.
- **minimum action:** a Director merge ruling. This audit does not merge and does not recommend a merge; it reports readiness.

### B3 — The `m4-exit` Release tag
- **blocker:** the tag does not exist.
- **exact source:** `docs/m4-spec.md:387`.
- **exact M4 requirement:** *"`milestones.md` M4 entry with its known-open list; `m4-exit` Release."*
- **observed condition:** MEASURED — `git tag` returns `m3-exit` only.
- **why it violates the requirement:** the Release is named in the criterion.
- **minimum action:** the Director cuts the tag at the merge. `docs/milestones.md:351` already records that *"the tag is the director's, at the merge"*.

### B4 — The `milestones.md` M4 entry cannot be the record of a closed milestone as written
- **blocker:** the closing artefact is stale against this tree.
- **exact source:** `docs/m4-spec.md:387`.
- **exact M4 requirement:** *"`milestones.md` M4 entry with its known-open list."*
- **observed condition:** the entry describes T4.21 as *"IN PROGRESS"* (`:318`); records known-open 4a and 4b as OPEN and skipped when both are now live and green (B.3 above); and predates the density-window breach entirely.
- **why it violates the requirement:** a closing entry that misdescribes the tree it closes is not the record the criterion asks for.
- **minimum action:** a closing edit to the entry, performed on a Director ruling. `milestones.md` is a living document; this audit is barred from editing it without that ruling.

### B5 — The density-quarantine window breach (CONDITIONAL ON B2)
- **blocker:** the recorded quarantine window no longer matches the candidate, and no shipped instrument can see it.
- **exact source:** `docs/m4-spec.md:379-380`.
- **exact M4 requirement:** *"Calibration battery green across ≥20 seeds with proven teeth, quarantined corridors reported with measured ranges rather than silently gating (the T3.12 mechanism)."*
- **observed condition, MEASURED and independently VERIFIED** (`docs/design/density-window-verification.md`, verdict **CONFIRMED**): canonical `densityPerArableKm2` at seed 3 = **`0.35415668759623087`** against the recorded window floor **`0.3685744951368359`** — **3.9117757 % below**. The shipped nightly gate returns **exit 0 / PASS**; an otherwise-identical control gate that consults `quarantine.window` returns **exit 1 / BREACH** on the same metrics file. `CalibrationBatteryTests` is 7 passed / 0 failed because its canonical theory runs **seeds 1 and 2 only** (`CalibrationBatteryTests.cs:426-428`), both comfortably inside the window. Attribution: **T4.21 caused it** — on the pre-T4.21 tree `45046eb` the same instrument measures 20/20 inside the window, reproducing both endpoints bit-for-bit.
- **why it violates the requirement — precisely, and with its limit stated:** the corridor is *reported*, but the range it reports is stale by 3.91 %, so "reported with measured ranges" is not satisfied on the candidate. **This is a process and instrument-accuracy issue, NOT an M4 gameplay blocker.** There is no simulation defect: seed 3's arable is bit-identical across the two arms, so the density move *is* the population move (133,750 → 128,518, **−3.9117757009345686 %** — the same number to every digit as the window shortfall), which is T4.21's intended bounded-migration and demographic effect, not a malfunction.
- **conditionality:** the §6 criterion was recorded **MET at the exit gate** against the certified baseline, and T4.21 is not in that baseline. **B5 therefore attaches to the candidate, not to certified M4, and becomes an M4 item only if B2 is ruled "merge."**
- **minimum action:** a **Director ruling**, not a repair. `docs/milestones.md:384` already states the shape — *"Needs a ruling, not a re-band — CR-002 and CR-003 both forbid fitting the instrument to the artifact."* The ruling must choose among: re-pin the window to the post-T4.21 measured envelope; accept the drift and record it; or lift the quarantine. **No `corridors.json` edit, golden re-pin, quarantine-threshold change or simulation change may precede that ruling**, and none was made here.

---

## SECTION D — OPEN ITEMS THAT DO NOT BLOCK M4

### CR-016 — the disaster rate. **DOES NOT BLOCK.**
Accepted state, unchanged by this audit: the disaster mechanism exists, is structurally validated,
ships at **λ = 0.0**, λ = 0.01 is rejected, the mechanism is unarmed. No rate was derived here, no
real-world frequency calibration implemented, no redesign performed.

**No ratified M4 requirement names the disaster hazard rate.** `m4-spec.md` §6 does not mention it;
the disaster system arrived with T4.21, a post-certification packet. The governing precedent is
ratified in the M4 entry itself: *"**No research, technology or institutions** — CR-005 places them
in M5 and **remains open without blocking M4**"* (`docs/milestones.md:329-330`). An open CR is not a
blocker by virtue of being open, and this audit does not infer one.

**Recorded as a deferred future item,** with the Director's stated future direction preserved
verbatim in intent and **not implemented**: real-world hazard frequency → geographic exposure →
settlement exposure → event probability → 10-year-turn representation; the simulation determines
consequences, vulnerability and recovery, and does not invent the underlying frequency by tuning for
acceptable gameplay.

### G1 — the harvest-weather decade variance. **DOES NOT BLOCK.**
Nothing was implemented, redesigned, introduced or tuned: no climate architecture, no rainfall
rework, no ENSO, no sigma change.

G1 is **outside the ratified M4 acceptance criteria** — §6 contains no variance criterion, and G1 is
recorded as *"escalated and open"* against T4.21 (`docs/milestones.md:318`), a packet outside the
certified baseline. Classified as directed: **POST-M4 DESIGN / FUTURE ARCHITECTURE**, and left alone.

**One honest qualifier, stated and not resolved.** G1 has two halves that should not be collapsed.
The *future climate architecture* is post-M4 and is correctly left alone. The *escalated measurement*
— `cr-015` §6.4, which computes the turn-mean OU variance factor at 0.427 for dt 10 — is an open
correctness escalation that `cr-015` itself says requires a separate Director ruling. That ruling is
owed on its own track whether or not M4 closes. It is flagged, not acted on.

### Other non-blocking open items
- Known-open 1 (density ruling), 2 (CR-003 Malthus quarantine, standing ruling), 3 (migration below corridor, quarantine active).
- Known-open 5 (ADR-017 "certification pending"), 6 (ADR-020 awaiting ruling), 7 (T4.19-E set S1–S5), 8 (grain storage-bounded, livestock and fish not — no recorded intent either way).
- Known-open 9 (ADR-019 exists only on an unmerged branch; the sequence still jumps 018 → 020), 10 (`current-state.md` staleness, self-corrected inline).
- Known-open 4a and 4b: **discharged** this pass — see B.3. Their `milestones.md` rows are stale, which is B4.

---

## SECTION E — GOVERNANCE / PROVENANCE ITEMS FOR POST-M4 REVIEW

Recorded separately as directed. **None of these is resolved here, and none blocks M4 closure under
any existing M4 acceptance criterion.** The provenance audit is complete and was not redone; no
additional bare `RATIFIED` rows were swept; the six UNVERIFIED cases were not relabelled.

| # | item | why it is post-M4 |
| --- | --- | --- |
| G-i | Do ADRs and CRs that exist **only on an unmerged branch** (`adr-022`…`adr-026`, `cr-013`…`cr-016`) carry RATIFIED status? | A governance question about the label vocabulary. No §6 criterion turns on it. If B2 is ruled "merge", it resolves itself for these nine documents. |
| G-ii | Does a director ruling attested **only in a code `_doc` comment or in `docs/queue.md`** count as a ratified record? Six sites are held at UNVERIFIED pending it. | Documentation provenance. No §6 criterion turns on it. |
| G-iii | Some **local findings-table rows carry a bare `RATIFIED` over a code-only source** — `arch-I` F26 is the proven instance. The population is **unmeasured**; the sweep was not authorized and was not performed. | Design-document hygiene, post-M4. |
| G-iv | The provenance audit's three **DIRECTOR DECISION REQUIRED** sites (overridable orchestrator decisions under CR-015 §6.3; one citation into the OPEN CR-016). | Governance vocabulary, post-M4. |
| G-v | `m4-spec.md` §6 is headed **"EXIT CRITERIA — PROPOSED"** and §4's packet list likewise. The criteria have been treated as binding throughout, including in this audit. Whether the heading is vestigial or load-bearing has never been ruled. | Surfaced, not resolved. It does not change this audit's outcome either way: every blocker in Section C is also named in `docs/milestones.md`'s own closing sentence. |

**Provenance audit result, carried forward unchanged:** RATIFIED 117 · MEASURED 77 · UNVERIFIED 6 ·
DIRECTOR DECISION REQUIRED 3.

---

## SECTION F — POST-M4 WORK QUEUE

Identified only. **Nothing below is designed, specified, restructured or implemented in this packet**,
and nothing below may begin before M4 is formally closed.

**F.1 — Closure ceremony (Director, in order).** Exit session against `b6eeace` with the T3.12a
replay report (B1) · the B5 density ruling, conditional on the merge (B5) · the merge ruling (B2) ·
the closing edit to the `milestones.md` M4 entry (B4) · the `m4-exit` Release tag (B3).

**F.2 — Rulings owed on their own track, whenever the Director reaches them.** CR-016 (the disaster
rate, mechanism to stay unarmed meanwhile) · G1's escalated measurement · R5–R14 from the earlier
closure audit (the `MagnitudeCorridor` tooth, ADR-018 §11's premise, the CR-003 teeth re-aim,
ADR-017, ADR-020, T4.19-E S1–S5, the grain/livestock storage asymmetry, the nightly's
quarantine-window blind spot, ADR-025 §2.4a RULE 2).

**F.3 — The POST-M4 CIVILIZATION ARCHITECTURE PHASE.** Its material already exists as a delivered,
unratified design corpus — 17 documents under `docs/design/`, outputs A–Q, with P-01…P-63 conflicts
and Q-01…Q-80 questions, ten of the latter marked BLOCKS M5 START. **It awaits Director ratification
and is not to be extended, revised or implemented in the interim.** Per the Director's sequence, this
phase begins only after M4 CLOSED.

**F.4 — M5 implementation.** Begins only after F.3 is ratified. Not started, not scoped here.

---

**M4 NOT CLOSED**
