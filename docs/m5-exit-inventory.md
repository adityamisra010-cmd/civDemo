# M5 EXIT INVENTORY — CLOSURE REPORT AND EVIDENCE MATRIX

**Prepared 2026-10-20 for the Director's closure ruling. M5 is NOT closed.** This record is the evidence package the
ruling is taken on; it rules nothing itself. It follows the M4 precedent (`docs/m4-exit-inventory.md`,
`docs/design/m4-final-closure-audit.md`): every status is measured against the tree named below, and where something
is open, pending or deferred it says so. Labels: **VERIFIED** (measured on the tree or read from GitHub by the agent
writing this), **RECORDED** (a repository document says so), **DIRECTOR-REPORTED** (the Director's own words, not an
agent observation), **INFERRED** (reasoned, not measured).

| | |
| --- | --- |
| branch | `claude/new-session-yavghm` |
| last code / test change | `3c18a71` (Sim.Ui: resource persistence, next-turn figure) · `9330f27` (its tests) |
| evidenced builds (CI, Windows smoke) | `4d2de04` · `dc83a67` — docs-only after `9330f27`, so byte-identical in `Sim.*` |
| closure-record commit | the commit carrying this file (docs only; its SHA is in `docs/current-state.md`'s dated entry) |
| `main` | `93270cd` — an ancestor of the branch; the branch is 423 commits ahead; nothing from M5 is on `main` |
| schema | **v34**; readable set {32, 33, 34} (`Sim.Core/Kernel/CanonicalSchema.cs:181,185,195`) |
| proposed tag | **`m5-exit`** — lightweight, on the merge commit into `main`, minted by publishing a GitHub Release (the M0–M4 convention; §11) |
| M5 criteria list | `docs/d052-m5-integration-audit-rulings.md` §15 (RATIFIED). There is no `docs/m5-spec.md`; ADR-033 is M5's decision record (`docs/current-state.md:704`) |
| verdict | **READY FOR THE DIRECTOR'S REVIEW. NOT READY FOR TAGGING** until the rulings in §6 are given. Engineering evidence is complete on the candidate; what remains is acceptance, scheduling and one ratified-rule conflict (§10). |

---

## 0. Verdict in one table

Of D-052 §15's seven exit-and-merge conditions (verbatim in §2):

| D-052 §15 condition | Status | What closes it |
|---|---|---|
| 1. CR-020 §B reassessed; battery green or re-ruled | battery **PASS** (CI green on `4d2de04`, `dc83a67`); re-record **PENDING ACCEPTANCE** (D-059) | the Director accepts D-059 and rules D-054 Q8 |
| 2. the deferred D-051 items scheduled | **BLOCKED** — no destination named for §1(a), §2, §3, §6-remainder, §4 | the Director names a milestone per item (§6 R2; recommendation in §7) |
| 3. the unit-display rework (§7) | **FAIL** — not built (`d052:98`) | build it (UI only) or fold it into M6's visual milestone by a dated ruling (§6 R3) |
| 4. the Spine M5 items dispositioned (§12) | **BLOCKED** — destination unruled (`d052:154-158`; `milestones.md:37-38`) | the Director rules the destination (§6 R4; recommendation: split M6 civics / M8 regime) |
| 5. packet acceptance (§14) | **PENDING ACCEPTANCE** — 18 m5w packets + W0, ADR-035 P-F1/P-F0, D-053…D-060, `3c18a71` | one en-bloc acceptance ruling (§6 R5) |
| 6. a green CI run, Windows smoke included, on the candidate | **PASS** (VERIFIED on `4d2de04` and `dc83a67`; §3) | re-confirm on the closure-record commit after push (docs only) |
| 7. the Director's merge ruling | **PENDING** | §6 R8 |

Also open and material: human coverage of the post-`231485e` changes is partial (§4); the next-turn figure deviates from
ratified D-051 §3 (§10, Decision C). The 20-seed soak named by the S8 proof standard was dispatched on the candidate
2026-10-20 and **passed** (run 39377615261; §2 row B).

---

## 1. Repository truth (LOCAL / REMOTE / MAIN, never collapsed — gov-4)

| State | Value | Evidence |
|---|---|---|
| LOCAL (`/home/user/m5w/dev`) | `dc83a67` on `claude/new-session-yavghm`, working tree clean | `git status`, VERIFIED 2026-10-20 |
| REMOTE (GitHub `claude/new-session-yavghm`) | `dc83a67780188083734b544a3deb7151f16e72b2` | `git ls-remote`, VERIFIED 2026-10-20 |
| MAIN (GitHub `main`) | `93270cd17872eb7cd2a7750edb5126df66bc96e2` ("Merge civilization progression architecture (#9)", 2026-09-30) | `git ls-remote`; `git merge-base --is-ancestor 93270cd HEAD` → yes |
| main's governance line | `main`'s `CLAUDE.md:10` still says "Current milestone: M4" — D-052 explains: "`main`'s `CLAUDE.md` still names M4 only because the M5 branches are unmerged; it changes when they merge" (`d052:19-20`) | RECORDED |
| integration worktree | `/home/user/m5w/integration` at `de86ace`, untouched (its pre-existing uncommitted state is preserved) | VERIFIED |
| tags on GitHub | `m0-exit afb2b4b` · `m1-exit 1c6e016` · `m2-exit 042bb64` · `m3-exit 5bf4920` · `m4-exit de5e00e` — each a GitHub Release published by the Director's account; `m4-exit` is the PR #7 merge commit on `main` | VERIFIED (GitHub API) |

Commits after the last human-played build `231485e` (all pushed): `827337e` D1 launcher + M6 docs · `8abea7f`
resources view · `45a6a08` D-058 · `bd33657`→`6d1988f` D-059 · `aa79173` chronicle fix · `77d8c30` docs ·
`e2b4c54`/`e19d3ad`/`f80dce6` D-060 · `3c18a71`/`9330f27`/`4d2de04` resource persistence · `dc83a67` run-recording ruling.

---

## 2. The closure matrix

D-052 §15, verbatim (`docs/d052-m5-integration-audit-rulings.md:186-200`):

> ## 15. Playtest readiness is not M5 exit
> **Playtest readiness:** a build the Director can play, with its known issues disclosed in `docs/m5-playtest-known-issues.md`.
> **M5 exit and merge readiness** additionally need at least:
> - CR-020 §B reassessed, and the calibration battery green or re-ruled;
> - the deferred D-051 items scheduled;
> - the unit-display rework (§7);
> - the Spine M5 items dispositioned (§12);
> - packet acceptance (§14);
> - a green CI run, Windows smoke included, on the candidate;
> - the Director's merge ruling.
> **M5 is NOT closed.**

Status vocabulary: PASS · FAIL · BLOCKED (cannot be completed yet) · PENDING ACCEPTANCE (evidence exists; the authorized
decision is outstanding) · NOT APPLICABLE (informational).

| # | Criterion | Governing requirement | Status | Evidence | Remaining action | Owner | Blocks closure? |
|---|---|---|---|---|---|---|---|
| 1 | CI green, Windows smoke included, on the candidate | D-052:197; GOV-3 B6 (`gov-3:115-118`): any red in a pre-exit sweep stops | **PASS** (VERIFIED) | `dc83a67`: CI run 39373352699 — `build-and-test` (banned constructs, read isolation, research audits, Test 05:10–05:38, read-only proof) success; `determinism` success; `determinism-xproc` incl. the D-055 allocation step success; `calibration` success; `calibration-nightly` skipped (schedule/dispatch only). ui-artifact run 39373352710: `ui-artifact` success, `windows-smoke` success; artifact `sim-ui-win-x64-dc83a67` id 12242108766. `4d2de04`: CI run 39371214975 all four jobs success; run 39371214840 `windows-smoke` success; artifact id 12241855029 (82,117,526 bytes). | Re-confirm on the closure-record commit (docs only) | CI | N (met) |
| 2 | Calibration: CR-020 §B reassessed, battery green or re-ruled | D-052:192; `ci.yml:440-456` | battery **PASS**; re-record **PENDING ACCEPTANCE** | D-059 (`docs/d059-calibration-rerecord-d051-regime.md:3-11`): CR-020 §B option 1, test-only, "The Director's acceptance is still pending"; battery 7/7 (`d059:112-122`); CI `calibration` green on `6d1988f`, `f80dce6`, `4d2de04`, `dc83a67`. Residual: nightly cylinder drift quarantined and unruled (`m5-playtest-known-issues.md:293-296`; D-054 §10 Q8). The 20-seed soak was dispatched on the branch 2026-10-20 (S8 §4 proof standard, `spine-s8:53`). | Accept D-059; rule Q8; record the soak result | director; CI | **Y** |
| 3 | Deferred D-051 items scheduled | D-052:193; D-051 §1(a) `d051:18`, §2 `:40-47`, §3 `:54-58`, §4 `:70-77`, §6 `:98-102` | **BLOCKED** | Each is "deferred until after the Director's M5 playtest" (`d052:127-152`); no later record names a milestone (`m5-playtest-known-issues.md:270-272`; `queue.md:1470`). §6 is partly built by D-053 (`d053:151-160`). The 2026-10-20 "Next turn" column is a stores preview on queued orders, not §3's forecast (§10). | Name a milestone per item (§7 has a recommended disposition) | director | **Y** |
| 4 | Unit-display rework (§7) | D-052:194; `d052:80-98` "Status: NOT IMPLEMENTED" | **FAIL** (not built) | `m5-playtest-known-issues.md:148-152`; `current-state.md:317`. "Built" means one representative token per unit type with that type's formation count above it, and a combined army panel of simulation-backed figures only. Under `formationsPerPolity` 1 the count reads 1 and the panel aggregates one formation: no player-visible change until M7 recruitment (`d052:90`). | Build (UI only, S–M) or fold into M6's visual milestone by a dated ruling superseding §15 bullet 3 | director → implementation | **Y** |
| 5 | Spine M5 items dispositioned (budget, authority / bandwidth economy, laws-lite) | D-052:195; `d052:154-158`; Spine `civ-sim-architecture-v3-outline.md:132`; `milestones.md:49-50` | **BLOCKED** | "not built … deferred out of the M5 playtest build. The ruling names no destination milestone, and does not say whether the M5 exit waits for them" (`d052:156-158`); `milestones.md:37-38`. INFERRED from `m5-governing-loop-port.md:26`: "authority" exists only as a tax-capacity coefficient; no budget/treasury (CR-008 OPEN); no laws-lite. | Rule the destination (§6 R4). Recommendation: the civics tree, incl. laws-lite and authority as civic content, is M6's (`milestones.md:76` already lists "the Technology and Civics trees"); government ADOPTION and regime change stay M8 (`v3-outline:134`; `d011:60`); budget/treasury waits for CR-008. | director | **Y** |
| 6 | Packet acceptance (§14) | D-052:196; `d052:175` "merged into the local integration, without rulings" | **PENDING ACCEPTANCE** | The 18 m5w packets (`m5w-map-workstream-audit-and-plan.md:1057-1073, 1178-1181`): S1a `b8d2108` · S0 `6e9d05e` · P4 `3ca52da` · P13 `bf5f3bb` · P6 `5a44eaa` · S1b `3d8aacc` · S1c `9ff6452` · P1 `1b54ce6` · P3 `9e066b2` · P2 `7e4464f` · P7 `c58d9b0` · P8 `efef5da` · P9 `c0dc157` · P10 `888eda3` · S1d `50e94ce` · P11 `54cb603` · P12a `f495bcb` · P9b `c683259`; W0 (`m5w-w0-docs`). Contract rulings exist (ADR-036, ADR-037 RATIFIED, `d052:22-44`); ADR-035 P-F1/P-F0 unruled (`adr-035:20-22`). Also awaiting acceptance: D-053/ADR-038, D-054/ADR-039, D-055/ADR-040, D-056/ADR-041, D-057/ADR-042, D-058, D-059, D-060, the resources view, the chronicle fix, `Continue last game.cmd`, `3c18a71`/`9330f27`. | One en-bloc acceptance ruling naming what is accepted and what is not (§6 R5) | director | **Y** |
| 7 | The Director's merge ruling | D-052:198; CLAUDE.md:48-50 (the loop) | **PENDING** | No merge ruling recorded; `main` unchanged (`current-state.md:57`). Pushes to the branch were under the 2026-10-19 "full authorization" (`d059:4`). | §6 R8 | director | **Y** |
| 8 | D-058 construction carry-over | `d058:3-8` "awaits the Director's acceptance like every packet" | **PENDING ACCEPTANCE** (human Pass DIRECTOR-REPORTED) | `m5-playtest-known-issues.md:596-609`: "Construction carry-over (D-058) \| **Pass**"; build INFERRED `f80dce6`. Tests: `ContinueWithinTurnTests.TheDirectorsExample_…`, 6 mutants (`d058:38, 116-121`); the Director's 68-turn session replays identically under both rules (`d058:177-185`). | Acceptance ruling | director | **Y** (part of 6) |
| 9 | D-059 calibration re-record | `d059:10-11` | **PENDING ACCEPTANCE** | as row 2 | Acceptance ruling | director | **Y** (part of 2) |
| 10 | CR-022 / D-057 / ADR-042 replay-identity proof | `cr-022:3` "RAISED 2026-10-18 AND IMPLEMENTED UNDER DELEGATED AUTHORITY (option 2) — AWAITING THE DIRECTOR'S RULING"; `adr-042:3-4`; `d057:3-9` | **PENDING ACCEPTANCE** | Kernel files touched (`d057:206-213`; `d056:88-97`): `ReplayProof.cs` (new), `CanonicalSchema.cs` read flags, `TurnExecutor`, `ReplayGuard`, `WorldHash`, `Snapshot`, `OrderLog` kind 13, `OrderValidation`. Readable set {32,33,34} "pending the Director's acceptance" (`d057:147-150`). Tests `adr-042:80-91`. CLAUDE.md:34: the kernel is frozen without director sign-off. | Rule CR-022 (option 2 as built / 1 / 3); accept D-056/D-057 | director | **Y** (INFERRED — kernel sign-off) |
| 11 | Human testing of the post-`231485e` changes | `m5-playtest-known-issues.md:552-560` "Not yet played"; standing gate `m5-playability-gate.md:3-5` | **PARTIAL** (see §4) | DIRECTOR-REPORTED 2026-10-20 on build INFERRED `f80dce6`: rival isolation Pass; resources view/strip Pass except zero-stock; D-058 Pass; D1 Pass; ordinary play Pass (`known-issues:596-609`). DIRECTOR-REPORTED 2026-10-20 (instruction text): zero-stock persistence confirmed on the build of `4d2de04`. NOT REPORTED: PT-04–PT-11, the chronicle wording fix, checklist 21–22 (the next-turn figure), the §16 GPU-seam items, D-050 item 8 (`known-issues:611-613, 536-538, 622-623`; `d050:20-21`). | The Director plays the uncovered items or waives them in the closure block (§6 R6) | human playtest / director | Director's call (M4 precedent: a recorded waiver, `m4-exit-session.md:16-21`) |
| 12 | D-060 rival-data isolation; Decisions A/B | `d060:9`; `d060:248-261` | **PENDING ACCEPTANCE** (human Pass DIRECTOR-REPORTED; A and B RULED) | Pushed at `f80dce6`; CI 39338603701 green; Windows smoke 39338603779 green (`d060:250-253`). Tests `Sim.Ui.Tests/KnowledgeLeakTests.Rivals.cs` (five `D060_…`), hash unchanged `827d9b00…3878a0`. Decision A deferred to fog (not permanently public); Decision B intentional disclosure (`d060:255-261`). | Acceptance ruling | director | **Y** (part of 6) |
| 13 | Resource persistence at 0 and the next-turn figure (`3c18a71`, `9330f27`) | the Director's 2026-10-20 instruction; `m5-playtest-known-issues.md:614-623` | CI **PASS**; zero-stock **DIRECTOR-REPORTED PASS**; next-turn check **NOT REPORTED**; one ratified-rule conflict (**Decision C**, §10) | 11 tests in `Sim.Ui.Tests/ResourcesPersistenceTests.cs` (names in `m5-resources-view-proposal.md:195-215`); four mutants killed; 40 turns ±AI with 0 mismatches; local suites on this code: Sim.Tests 2,635/0/13, Sim.Ui.Tests 1,091/0; CI green on `4d2de04`, `dc83a67`. Human: "The user has manually tested the build associated with `4d2de04` and confirmed that resources remain visible at zero" (the Director's instruction, 2026-10-20; build identity as stated by the Director; artifact 12241855029 exists and is the only pushed build carrying the fix; window title / session files not supplied). The "with no new orders, Next turn equals the next turn's Empire figure" check: not reported. | Decision C; then the human check of checklist 22 | director; human playtest | **Y** (Decision C) |
| 14 | Save/load, replay, determinism, compatibility | — | NOT APPLICABLE (informational, §9) | — | — | — | N |
| 15 | Known defects and accepted limitations | — | NOT APPLICABLE (informational, §8) | — | — | — | N |

**Criteria found beyond the Director's list (all from the record):**

| # | Criterion | Status | Evidence / action |
|---|---|---|---|
| A | GOV-3 B6: no red in a pre-exit sweep | PASS on `dc83a67` | row 1 |
| B | S8 §4 proof standard: "packet acceptance tests green + CI green + (from M2 onward) the autoplay soak green + any calibration hooks" (`spine-s8:53`) | **PASS** (VERIFIED) | `calibration-nightly` runs on schedule/dispatch only (`ci.yml:463`); dispatched on the branch at `dc83a67` 2026-10-20 (CI run 39377615261): "20-seed autoplay sweep" success (05:57–06:04 UTC), "Corridor teeth across all 20 seeds (bands + quarantines from corridors.json)" success; metrics artifact uploaded. The same run's `calibration`, `determinism`, `determinism-xproc` jobs: success. |
| C | Standing playability gate (`m5-playability-gate.md:3-5`): headless smoke + the §16 manual half | smoke PASS; manual half partial | rows 1, 11 |
| D | Exit artefacts by precedent: `mN-exit-session.md`, `mN-exit-inventory.md`, `milestones.md` entry, Release | this record is the inventory; no session brief is written (the Director's playtests of 2026-10-19/20 stand in for it, by the Director's choice to close the matrix) | §12 |
| E | The frozen M5 exit-criterion definition: the Spine's "'It's a game now' checkpoint — evaluate fun honestly here" (`v3-outline:132`; frozen per `spine-s8:17`); arch-N X-N6: how it is discharged "is recorded nowhere" | **needs the Director's one-line statement** in the closure block | §6 R7 |
| F | D-052 §14 items outside §15: ADR-035 P-F1/P-F0; G9 destitution-as-revolt-cause; viewport/minimap (D-054 Q5); shortcut highlighting of realized deposits; the "cut from main" convention | open | §6 R5 (P-F1/P-F0); the rest are deferred-work items (DW-040) |
| G | D-050 item 8: the 1080×640 snap-back "the Director will check it during the personal playtest" (`d050:20-21`) | not recorded | waive or play (§6 R6) |
| H | Merge-time housekeeping: CLAUDE.md:10 "Current milestone" line, `milestones.md` M5 row/entry, `current-state.md` | prepared, not applied (§12) | at the merge, on the ruling |

---

## 3. Test and CI evidence

**Local, on the code of `4d2de04` (= `dc83a67` in `Sim.*`), Linux x64, .NET 10 — VERIFIED 2026-10-20:**

| Check | Result |
|---|---|
| Release build | 0 warnings, 0 errors |
| `scripts/check-banned-constructs.sh` | OK |
| `scripts/check-read-isolation.sh` | OK (needs/grievance tables; 24 allowlisted files) |
| `scripts/check-readonly-proof.sh` | OK (mutation attempts fail to compile) |
| Sim.Tests | **2,635 passed / 0 failed / 13 skipped** (25 m 44 s) |
| Sim.Ui.Tests | **1,091 passed / 0 failed** (18 m 57 s) |
| World hash, `Sim.Cli run --founded --seed 42 --ai-empires 1 --turns 60` | `827d9b00737117e25c3087495a87f18616a1e0f560f60baff5821a855a3878a0` — unchanged since before D-060 |
| `Sim.Ui --smoke --monkey 300` | SMOKE PASSED: 13 states, 3,865 checks (3,587 pass, 278 not offered), 0 FAIL, 0 problems |
| `Sim.Ui --smoke --ai-empires 1` | SMOKE PASSED: 11 states, 3,261 checks, 0 FAIL, 0 problems |
| Mutants (resource persistence) | 4/4 killed, each by a semantic test (`m5-resources-view-proposal.md` §9) |

**GitHub, VERIFIED job by job 2026-10-20:**

| Commit | CI run | Jobs | Windows build |
|---|---|---|---|
| `dc83a67` | 39373352699 | build-and-test ✔ · determinism ✔ · determinism-xproc ✔ · calibration ✔ · calibration-nightly skipped | run 39373352710: ui-artifact ✔, windows-smoke ✔; artifact `sim-ui-win-x64-dc83a67` 12242108766 |
| `4d2de04` | 39371214975 | all four ✔; nightly skipped | run 39371214840: ui-artifact ✔, windows-smoke ✔; artifact `sim-ui-win-x64-4d2de04` 12241855029 |
| `f80dce6` | 39338603701 | all four ✔ | run 39338603779: windows-smoke ✔; artifact 12227387967 |
| `6d1988f` | 39270568141 | all four ✔ (Sim.Tests 2,635/0/13; Sim.Ui.Tests 1,069) | run 39270568128: windows-smoke ✔ (3,862 checks, 0 FAIL) |

The CI Test step's own count lines for `4d2de04`/`dc83a67` were not pulled; the job fails on any failure, and the local
run of the same code gives the counts above. The 20-seed soak (`calibration-nightly`, dispatched on `dc83a67`
2026-10-20, run 39377615261): **success** — the 20-seed autoplay sweep and the corridor teeth across all 20 seeds both
passed; the metrics artifact is attached to that run.

---

## 4. Human playtest matrix (DIRECTOR-REPORTED only; agents observed none of this)

| Build | Item | Result | Source |
|---|---|---|---|
| `231485e` (artifact `sim-ui-win-x64-231485e`) | PT-01 install, launch, 68 turns, clean exit | PASS | `m5-playtest-known-issues.md:476-486`; files hash-verified against the forensic close record |
| `231485e` | PT-02 same-build resume at turn 68 | PASS (screenshot) | same |
| `231485e` | PT-03 construction orders, cancel the middle one, resume | PASS ("Rest all tests pass") | same |
| `231485e` | PT-04 – PT-11 | NOT RUN (the Director closed the playtest) | same |
| `f80dce6` — **INFERRED** build (the only pushed build carrying D-060; the Director did not state it) | rival-data isolation (D-060) | PASS | `known-issues:596-609` |
| `f80dce6` (INFERRED) | resources view and strip | PASS, except a resource vanishing at 0 stock (fixed in `3c18a71`) | same |
| `f80dce6` (INFERRED) | construction carry-over (D-058) | PASS | same |
| `f80dce6` (INFERRED) | `Continue last game.cmd` (D1) | PASS | same |
| `f80dce6` (INFERRED) | ordinary play | PASS | same |
| `4d2de04` — as stated by the Director | zero-stock persistence (resource stays listed and reads 0) | PASS | the Director's instruction of 2026-10-20 |
| `4d2de04` | "with no new orders, Next turn equals the next turn's Empire figure" (checklist 22) | **NOT REPORTED** | — |
| any | chronicle wording fix (`aa79173`); checklist 21 (resources totals vs table); §16 GPU-seam items 1–4, 8, 10; D-050 item 8 snap-back | **NOT REPORTED** | `known-issues:611-613, 536-538`; `d050:20-21` |

---

## 5. What stops the tag today

1. **Rulings, not code** (§6): D-051 scheduling; unit-display disposition; Spine M5 items' destination; en-bloc
   acceptance; CR-022; D-054 Q8; the merge ruling; the "it's a game now" statement.
2. **Decision C** (§10): the next-turn figure's weather semantics against ratified D-051 §3. Options are small code changes
   either way; none is made in this pass.
3. **Evidence still arriving:** CI on the closure-record commit (docs only; the soak has passed, §3).
4. **Human coverage** the Director may waive or play (§4).

Nothing in the candidate is known to be red: suites, gates, determinism legs, calibration battery, both smokes and the
Windows smoke are green on the exact code to be merged.

---

## 6. Rulings and acceptances owed — a proposed closure block for the Director to approve, amend or refuse

Written in the M4 form (`milestones.md:607-707`, "DIRECTOR FINAL CLOSURE RULING"). Each line is a proposal; the
Director's words replace it.

- **R1 — Calibration.** Accept D-059 (CR-020 §B option 1, test-only). Rule D-054 §10 Q8: ship `topology.wrapX` with
  the quarantined nightly drift (density 16/20, migration breach 80.83 %) as a disclosed quarantine, or hold. Record
  the 2026-10-20 soak result.
- **R2 — D-051 items scheduled** (recommendation in §7): §2 availability classes → M6 (depends on known extent and the
  P9/P10 overlays); §4 primitive toolmaking → M6 (tech-tree content + recipe realization, M6.16 corpus audit); §3 → per
  Decision C (stores half) and M6 UI lane (population half); §1(a) and §6-remainder → M6 economy carry-over packets,
  or M11+ if M6 is to stay knowledge-only.
- **R3 — Unit display (D-052 §7).** Either "build before the tag" (UI-only packet) or "folded into M6.14 (E1 visual
  milestone); D-052 §15 bullet 3 is discharged by this ruling". Recommendation: fold — the rework has no observable
  effect while every empire holds one formation (`d052:90`).
- **R4 — Spine M5 items.** Recommendation: "The M5 exit does not wait for budget, authority and laws-lite. Laws-lite and
  the authority economy are realized as civics content in M6's Civics tree (availability, prerequisites, civic effects
  through existing mechanisms); government adoption, regime change and legitimacy effects remain M8; budget/treasury
  waits for CR-008." (Any other split is the Director's to name.)
- **R5 — Acceptance en bloc.** Accept the 18 m5w packets and W0 (row 6 SHAs), ADR-035 P-F1 and P-F0 as shipped,
  D-053/ADR-038, D-054/ADR-039, D-055/ADR-040, D-056/ADR-041, D-057/ADR-042 with CR-022 ruled **option 2 as built**,
  D-058, D-059, D-060 (incl. the F12/session-file scope decisions), the resources view, the chronicle fix,
  `Continue last game.cmd`, and `3c18a71`/`9330f27`. Anything excluded is named here.
- **R6 — Human coverage.** "The recorded coverage (§4) is accepted for the M5 exit; PT-04–PT-11, the chronicle wording
  fix, checklist 21–22 and D-050 item 8 are waived / are to be played before the merge." (choose one)
- **R7 — The frozen checkpoint.** One sentence from the Director on "it's a game now — evaluate fun honestly here"
  (`v3-outline:132`), since no record discharges it (arch-N X-N6).
- **R8 — Merge and tag.** "Merge `claude/new-session-yavghm` at <closure-record SHA> into `main` by pull request; the
  agent performs the merge on this ruling and applies the merge-time edits (§12); the Director then publishes the
  `m5-exit` Release at the merge commit."
- **R9 — Decision C** (§10): option 1, 2 or 3.

---

## 7. Deferred work and its destination (summary; the full register is `docs/m6-plan/deferred-work-register.md`)

| Item | Ruled destination | Source | Recommendation where unruled |
|---|---|---|---|
| Fog of war: full fog, LIVE/GHOST/UNKNOWN, scouts, sighting memory, discovered-extent storage (S2a–S2c, schema v35), known-extent restriction of settle/claim/trade, ADR-037 channels | **M6** | `d052:53-66`; `milestones.md:35` | — |
| Visual milestone E1: drawn buildings/institutions with count staging, Age terrain, unit art; m5w P5/P12b/P14 | **M6** | `d052:68-78`; `milestones.md:32-34` | — |
| One-file session export + telemetry codebook | **M6** | `milestones.md:40-43`; `m5-run-recording-audit.md:107-123` | — |
| Research engine / Ages / unlock pipeline / universities — review, reconcile, complete | **M6** | `milestones.md:85-88` | — |
| Unit move orders, Action Points / Capacity, right-click move (S3a–S3c, kind 10, v36) | **M7** | `d052:46-51`; `milestones.md:36` | — |
| Recruitment, headcount, armies in motion, conquest; the 23 `unit.*` research entities | **M7** | `milestones.md:53-58`; deferred-entity plan §E | — |
| Capital succession; finer population segments | **M8**; **M9** | `d050:18-19`; `d050:12` | — |
| D-051 §1(a), §2, §3, §4, §6-remainder | **none named** | `d052:127-152` | §2, §4 → M6; §3 → Decision C + M6; §1(a), §6-remainder → M6 carry-over or M11+ |
| Budget, authority / bandwidth economy, laws-lite | **none named** | `d052:154-158` | civics content → M6; adoption/regime → M8; budget → after CR-008 |
| Unit-display rework (D-052 §7) | M5 (exit list) | `d052:194` | fold into M6.14 by ruling |
| Disaster architecture (CR-016 rate) | "post-M4", no number | `milestones.md:675-685` | a Director decision; not M6 |

---

## 8. Known defects and accepted limitations at the candidate (compact; details in `docs/m5-playtest-known-issues.md`)

- **Resources and food:** no availability classes, floor or regional richness (D-051 §2 deferred); the "now usable"
  shortcut outlines known land, not deposits; flight reads one turn's deficit (§1(a)); no primitive toolmaking (§4);
  the central stockpile covers construction/roads and grain relief only (§6 remainder) (`known-issues:68-134`).
- **Construction:** cancel refunds nothing; AI whole-cost prudence; split default "building first"; no in-turn forecast
  of D-058's carry-over (`known-issues:297-307, 389-408, 550`).
- **Units:** display differs from D-052 §7; no movement until M7; the formation card's "awaiting the Director's ruling"
  wording is stale (`known-issues:148-155`).
- **Saves and resume:** other-build sessions refused with the incompatibility dialog; v32/v33 sessions resume by replay
  with the D-008 notice; D-058 can diverge a pre-`231485e` session in which two projects could finish in one turn
  (`known-issues:157-206, 547-549`).
- **Fog and AI:** no fog — "known" is all-known; the AI ranks every cell (accepted until M6's known extent); DEV panels
  and session files are world-wide by decision (`d052:27-29`; `d060:66-100`).
- **Rival isolation residuals, ruled:** token size deferred to fog (Decision A); pre-capture prices intentional
  (Decision B) (`d060:255-261`).
- **Resources view:** next-turn figure excludes AI orders; trends start at the first played turn; see also §10.
- **Map and performance:** whole world never on screen; minimap queued, not built; LOD keyed on window width; seam judged
  by eye; no GPU timing (`known-issues:214-241`).
- **Calibration:** quarantined nightly cylinder drift (D-054 Q8); wall-clock bench test can redden under host load.
- **Run recording:** six plain files; no compression or encryption; unkeyed SHA-256 in the close record is never checked
  in production; the one-file export is M6 design (`m5-run-recording-audit.md`).
- **Governance residue:** ADR-022 Windows/Linux divergence from turn 2 (the determinism promise is Linux x64);
  D-054 §10 Q1–Q13, Q15, Q18, Q19 unruled; CR-017 OPEN (M7); CR-006 OPEN; stale status headers on CR-012, CR-018, ADR-034.

---

## 9. Save/load, replay, determinism and compatibility

The candidate writes canonical schema **v34** and reads {32, 33, 34} (`CanonicalSchema.cs:181,185,195`); v33 (ADR-039)
and v34 (ADR-040) are appended-only over v32, which is what lets `ReplayGuard` compare hashes across versions
(`WorldHash.ComputeHexAsSchema`; `CanonicalSchema.BytesAppendedSince`). The readable set's standing is "pending the
Director's acceptance" (`d057:147-150`). The determinism promise is scoped to Linux x64 (ADR-022:37-45); a Windows
session reproduces on its own machine and is held to the within-platform standard (`:60-68`; `d058:184-185`). Resume
rules (D-057 §2): a v34 session of this build whose trace checks turn by turn resumes unchanged; a v32/v33 session
resumes by replay with the four-line D-008 notice; refused before any write (exit 2) are an order naming a settlement or
polity founded after the replay first differs, a differing founding, a missing trace with named settlements, and a
legacy kind-11 cancel in a drifted world (D-056); a same-schema session from another build that does not replay
identically is refused with exit 3 and the native dialog (D-052 §13). Measured: the 300-turn `de86ace` v32 session and
the batch-2/batch-3 fixtures resume or inspect (`d057:56-61`); the Director's 68-turn `231485e` session replays
identically on all 68 turns under D-058 (`d058:177-185`). D-059, the chronicle fix, the resources view, D-060 and
`3c18a71` moved no hash, order, save or schema; the seed-42 one-AI 60-turn hash is `827d9b00…3878a0` before and after.
INFERRED (not measured): the Director's `231485e` session therefore resumes on `dc83a67`.

---

## 10. Verified finding — the "Next turn" figure and ratified D-051 §3 (Decision C)

**The rule.** D-051 §3 (`docs/d051-m5-polish-rulings.md:52-61`, RATIFIED, implementation deferred by D-052 §10):
"The player-facing 'next turn' food and population forecast is computed by the authoritative simulation, stepping a
private copy of the world, with **expected weather** (and no new random shock onset). It never reveals the actual next
draw. ADR-019 §9.2/9.3 … stands." and "No next-turn forecast ships in the playtest build."

**What was built** (`3c18a71`, on the Director's 2026-10-20 instruction "show the expected next-turn amount …
Calculate the projection using the game's authoritative accounting rules"): `UiSession.PreviewNextWorld`
(`Sim.Ui/UiSession.cs:204-216`) steps a copy of the current world through the production pipeline with the player's
queued orders; `EmpireStores` sums the result. That is §3's mechanism ("stepping a private copy of the world") — with
one difference.

**The difference, verified on the tree.** The step's farm output is multiplied by the harvest-weather and disaster
rows of the world it steps FROM (`Sim.Core/Systems/Production/ProductionSystem.cs:176, 427-434`: `HarvestWeatherFor(prev,
…)`, `DisasterFor(prev, …)`). Those rows were drawn during the previous step; they are already in state when the player
looks at the figure, and they are the multipliers that WILL apply at the coming harvest. The settlement view shows only
the multiplier that applied to the LAST harvest ("The weather was … harvest x1.02", `Sim.Ui/ViewModel/PlayerViews.cs:
263-266`), never the pending one. So the grain line of the next-turn figure is exact precisely because it carries the
pending weather multiplier — which is "the actual next draw" in §3's sense (not the draw the preview itself consumes:
those affect the turn after and reach no figure shown). Non-farm goods are unaffected. The pending disaster multiplier
is already shown by the UI ("A crop failure will strike next turn's harvest", `PlayerViews.cs:268-270`), and the hazard
is 0.0 in `sim.json`, so disasters add nothing new.

**Why it needs the Director.** Two Director texts pull apart: the ratified §3 (expected weather) and the 2026-10-20
instruction whose stated acceptance check is "with no new orders, the Next Turn projection matches the actual Empire
resource totals after ending the turn" — a check only exact weather can pass for grain. Options:

1. **Keep exact weather** (as built). Needs a dated note on D-051 §3 narrowing it: the RESOURCES "Next turn" stores
   figure is an exact one-step projection under the pipeline's already-drawn weather; §3's expected-weather
   food-and-population forecast remains a separate, deferred surface. Cost: a player who compares the figure with the
   current store learns the coming harvest's weather one turn early (no other surface shows it).
2. **Expected weather** (recommended — conforms to the ratified rule and ADR-019 §9.3). In the preview's private copy,
   replace the player's own settlements' pending `HarvestWeather` multipliers by 1.0 (the mean-one expectation,
   `HarvestWeatherSystem.cs:61-62`) before stepping; label the column "Next turn (expected weather)"; the acceptance
   check becomes equality for every non-farm good, and for farm goods equality up to the weather multiplier — pinned by a
   test that rigs a known multiplier and asserts the exact difference. UI-only change, no hash or save impact.
3. **Blank the farm goods** in the next-turn column until §3 ships. Smallest change; least useful.

Nothing is changed in this pass. Whichever option is ruled is a small Sim.Ui change with tests; options 2 and 3 also
discharge D-051 §3's stores half.

---

## 11. Proposed tag, commit and release notes

- **Tag name:** `m5-exit` (convention `m0-exit` … `m4-exit`; `m4-exit-inventory.md:353`).
- **Commit:** the **merge commit** of `claude/new-session-yavghm` (at the closure-record SHA) into `main` — the M4
  precedent (`de5e00e`, PR #7); a tag on the branch tip would not be on `main`. The branch tip carries this record and
  is docs-identical in code to `4d2de04`/`dc83a67`, whose CI and Windows smoke are green.
- **How it is minted:** by publishing a GitHub Release named `m5-exit` targeting `main` after the merge; the
  `ui-artifact.yml` `release: published` trigger builds the win-x64 zip from the tagged commit and attaches it to the
  Release (`ui-artifact.yml:24-25, 129-139`). The Director publishes it (M0–M3 practice; `milestones.md:157-160`).
- **Release notes, draft** (fill the SHA; strike what the ruling refuses):

> # M5 Exit — Governing Gameplay
> *(DRAFT release notes — true only once the Director has ruled and the merge is done; nothing below is a statement
> about today.)*
> M5 closes on the Director's closure rulings of <date> (`docs/m5-exit-inventory.md` §6) and is merged into `main`.
> ## Final commit
> `<merge SHA>`
> ## Validation (on the merged code)
> * Sim.Tests: 2,635 passed, 0 failed, 13 skipped · Sim.Ui.Tests: 1,091 passed, 0 failed
> * Repository gates: banned constructs, read isolation, read-only proof — passed
> * Release build: 0 warnings, 0 errors · Schema: v34 (reads v32–v34)
> * CI: build-and-test, determinism, cross-process determinism (incl. the D-055 allocation leg), calibration — passed;
>   Windows smoke on the shipped exe — passed; 20-seed soak (corridor teeth across all 20 seeds) — passed
> * World hash (seed 42, one AI, 60 turns): `827d9b00…3878a0`
> ## What M5 delivered
> The governing loop as built (taxation research-gated, GovernanceSystem, unrest-lite), the M5 map workstream (18 packets),
> the central stockpile for construction/roads and grain relief (D-053), incremental construction, cylinder topology
> and the found-settlement order (D-054), set-asides and the allocation split (D-055), identity-safe resume and the
> replay-identity proof (D-056/D-057), construction carry-over (D-058), the resources view and strip, rival-data
> isolation (D-060), resource persistence at zero and the next-turn figure, and the Director's playtests of 2026-10-19/20.
> ## Deferred by ruling
> Budget, authority and laws-lite (→ <per R4>); D-051 §1(a), §2, §3, §4, §6-remainder (→ <per R2>); the unit-display
> rework (→ <per R3>); fog of war, E1 visuals and the one-file session export (M6); movement, Action Points and
> recruitment (M7). Known issues: `docs/m5-playtest-known-issues.md`.
> M6 (Knowledge / Research / Technology) begins with its spec; the planning package is `docs/m6-plan/`.

---

## 12. The closure ceremony, in order (mirrors `m4-final-closure-audit.md` F.1)

1. The Director reads this record and `docs/m6-plan/README.md` (the transition note).
2. The Director gives the §6 rulings R1–R9 (a message suffices; the agent records them verbatim in a dated closure
   block appended to `docs/milestones.md`'s M5 entry and here).
3. If R3 = build, or R9 ≠ option 1, the agent makes the ruled UI-only change with tests, pushes, and CI re-runs.
4. On R8 the agent opens the pull request `claude/new-session-yavghm → main` carrying the merge-time edits, verifies
   the required status checks, and merges it (CLAUDE.md:48-50; the M4 one-time authorization precedent).
   **Merge-time edits, prepared, not applied:** `CLAUDE.md:10` → "Current milestone: M6 Knowledge / Research /
   Technology — spec in preparation, NOT ratified; no active packets until `docs/m6-spec.md` is ratified (S8 §4)";
   `docs/milestones.md` status rows M5 → "COMPLETE (closed at its exit gate, `m5-exit`)", M6 → "CURRENT — spec in
   preparation"; the dated closure block; `docs/current-state.md` top entry.
5. The Director publishes the `m5-exit` Release at the merge commit (the tag is minted by the publish; the zip attaches).
6. The agent records the Release, the tag SHA and the attached asset in the closure block, and M6 planning proceeds
   from `docs/m6-plan/` to the M6 spec.
