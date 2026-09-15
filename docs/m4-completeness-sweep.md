# M4 COMPLETENESS SWEEP — "IS M4 ENOUGH DONE BY NOW?"

**Read-only inventory. No production file was changed by this packet; this document is its only
artifact.** Every figure below was measured on the candidate tree in a detached worktree pinned to
`79dec68`, or read from a file in that tree. Where a claim comes from another document rather than
from measurement, it says so — a previous agent's report is secondary evidence (GOV-4 §1).

**Candidate under review:** `origin/t4.19-glass-box` = `79dec68`
("Merge branch 't4.20-food-legibility' into t4.19-glass-box").
**Swept:** 2026-09-15.

---

## §0 THE ANSWER, FIRST

**NO — not quite. Bucket A is NOT empty, but everything in it is documentation and
version-string work, not simulation work.** The simulation, its gates and its suite are in the
state M4 asked for. What is missing is the *exit paperwork* that `docs/m4-spec.md` §6 names as
exit criteria, plus one calibration question the record shows was ruled on for the in-process
battery and — on the evidence in the tree — never checked against the nightly CI gate.

There are **four A items**. Three are docs-only and cost an hour. The fourth is a ruling the
director has already half-made and may simply need to extend.

Everything else that looks open is either a standing ruled exception (B), a question genuinely put
and not answered (C), explicitly deferred to M5+ (D), housekeeping (E), or recorded as needing an
owner nobody has assigned (F).

---

## §1 GIT STATE, RE-DERIVED

Verified with `git fetch --all` then `git rev-parse` in `/home/user/civDemo`, not carried from a
report:

| ref | sha | note |
| --- | --- | --- |
| `origin/main` | `dbef61a` | "Merge M4 completion (director-certified): the M4 baseline" |
| `origin/t4.19-glass-box` | `79dec68` | **the candidate** — T4.17, T4.18, T4.19(A–E), T4.20 on top of `dbef61a` |
| `origin/m5-full-build` | `9a79d1e` | M5 work, untouched by this sweep |
| `origin/m4-diversity-draft` | `e25ebb8` | UNVERIFIED and contradicted — not treated as authority anywhere below |
| `origin/m4-diversity-reconciliation` | `92e02fa` | exists; not on the candidate's ancestry |
| tags on origin | `m0-exit`, `m1-exit`, `m2-exit`, `m3-exit` | **no `m4-exit`** — see A3 |

`main`'s tip is the M4 completion merge, so **every M4 spec packet T4.1–T4.16 is already merged and
director-certified**. The candidate adds four post-certification packets (T4.17 session records,
T4.18 population transient + sliders, T4.19 the glass box, T4.20 food legibility). The candidate is
therefore not "M4 being finished" — it is M4 plus an observability layer that *also moved goldens*
(T4.19 lane C's founding-cohort correction), which is why it needs its own certification and is why
A4 below exists at all.

---

## §2 MEASURED SUITE RESULTS — RUN HERE, NOT RECALLED

Release, on `79dec68`, in an isolated worktree.

| check | result |
| --- | --- |
| `dotnet build -c Release` | **0 warnings, 0 errors** |
| `dotnet test Sim.Tests -c Release --no-build` | **722 passed · 4 failed · 6 skipped** (732 total, 8 m 10 s) |
| `dotnet test Sim.Ui.Tests -c Release --no-build` | **236 passed · 0 failed** (236 total, 26 s) |
| `scripts/check-banned-constructs.sh` | **OK** — no banned constructs found |
| `scripts/check-read-isolation.sh` | **OK** — needs/grievance tables read only by owner, serialization, UI, tests |
| `scripts/check-readonly-proof.sh` | **OK** — mutation attempts fail to compile (CS0200, CS1061) |

### The four failures, verbatim — and they are **exactly** the expected standing reds, no more

```
Failed  CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed: 2)
        canonical.densityPerArableKm2: 0.74211 outside [0.15, 0.6]

Failed  CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed: 1)
        canonical.densityPerArableKm2: 0.625062 outside [0.15, 0.6]

Failed  CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 42)
        seed 42: 3 starvation deaths — the dev world is no longer pre-Malthusian in the strict
        sense this tooth asserts. … cr-003.md §7.5 … §7.6 anticipates this exact message and
        records it as NOT ACTIONED. … only a director ruling on CR-003 changes either.

Failed  CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7)
        seed 7: 3 starvation deaths — [same message]
```

**No other failure appeared.** The two `ClassSystemTests` reds that `docs/current-state.md` still
records are gone: T4.19-E retired the `[10, 95]` artisan window as an unsupported calibration
instrument (`docs/t4.19-verification-record.md` §10), and the famine/demote-first test was closed as
unsupported at M4 completion (`docs/m4-exit-inventory.md` §6). The nine CR-014 reds recorded in
`docs/t4.19-verification-record.md` §2 are gone too — CR-014 was ruled and shipped at T4.19-A.

The two density values reproduce the record's own numbers to six digits
(`docs/m4-founding-demographics-correction.md` §8: 0.4806 → 0.6251 seed 1, 0.5638 → 0.7421 seed 2),
so the record and the tree agree.

### The six skipped tests, each with its stated reason

| test | reason (verbatim from `Skip=`) | class |
| --- | --- | --- |
| `MigrationTests.cs:378` | "T4.1b/ADR-018 §11: asserts on GROSS migration, which T4.1g measured as the wrong observable for the rate lever (gap-driven x3.10 vs gross x1.07). **Lifts when M4's migration work re-derives the assertion. Owner: M4 migration.**" | **F** |
| `MigrationTests.cs:532` | same string | **F** |
| `FoundingVariationItem0Tests.cs:58` | "T3.6b Item 0 measurement rig (~30 min…) — run manually to reproduce `docs/t3.6b-review-record.md`" | **B** |
| `WaterRouteCounterfactualTests.cs:88` | "T3.6b→T3.7 seed measurement rig (~20 s…) — run manually" | **B** |
| `WaterRouteCounterfactualTests.cs:142` | "T3.6b→T3.7 seed measurement rig (pixel Dijkstra, ~35 s) — run manually" | **B** |
| `HousingBeforeColumnTests.cs:40` | "T3.8 before/after-column measurement rig (~2 min…) — run manually" | **B** |

Four are expensive measurement rigs kept off the default path by design, each naming the review
record that holds its numbers — standing ruled exceptions. The two migration skips name **"Owner:
M4 migration"** and M4's migration work (T4.10/T4.12) shipped without re-deriving them; see F1.

### Code hygiene grep — clean

Across `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Ui`: **zero** `TODO`, **zero** `FIXME`, **zero**
`HACK`/`XXX` as word-boundary matches. Every hit for `M5`, `deferred`, `out of scope`, `awaiting`
and `not recorded` is a deliberate scope-fence or observability comment naming its ruling — e.g.
`NotableLifecycle.cs:13` ("payment is money, which is M5"),
`AppropriationSystem.cs:108` ("RESISTANCE IS OUT OF SCOPE (T4.8 owns conflict)"),
`SettlementHappiness.cs:30` (D-021: grievance drives no behaviour until M5). There is no
half-finished code marker anywhere in the simulation.

---

## §3 M4-SPEC PACKET-BY-PACKET STATUS, FROM THE TREE

`docs/m4-spec.md` §4 defines T4.1–T4.16. All sixteen are merged into `origin/main` at `dbef61a`.
Evidence is the merged history plus the per-packet review record in `docs/`, not a report.

| packet | status | tree evidence |
| --- | --- | --- |
| T4.1 foundations audit (+1b–1g) | **COMPLETE** | `docs/t4.1-manifest.md`, `t4.1-review-record.md`, and six sub-packet records; its findings promoted to T4.3's three named prohibitions in `m4-spec.md` §4 |
| T4.2 B-2 store bounding | **COMPLETE** | `docs/t4.2-manifest.md`, `t4.2-review-record.md`; `ConsumptionSystem.BoundStore` live; `m4-exit-inventory.md` §1: "LIVE — starvation is reachable" |
| T4.3 claim / control / recognition | **COMPLETE, DORMANT BY DESIGN** | three relations shipped; `m4-exit-inventory.md` class **B** "schema-only by design; no writer, no reader" |
| T4.4 colonization / land clearance | **COMPLETE** | `docs/t4.4-review-record.md`; ADR-021 ACCEPTED; class **A** |
| T4.5 non-state peoples | **COMPLETE** | `docs/t4.5-review-record.md`; `AppropriationSystem` live, raider precondition reachable; class **A** |
| T4.6 trade & foreign trade | **COMPLETE; foreign-trade arm dormant** | `docs/t4.6-foreign-trade-decision.md`, `t4.6-…` branch merged; class **B** — one polity controls everything founded, `aiEmpires > 0` is the activating seam |
| T4.7 transport | **COMPLETE** | `docs/t4.7-review-record.md`; river-aware traversal live; measured cause of the density corridor's return |
| T4.8 war + AutoResolver + notables | **PARTIAL BY RULING — notables only** | `docs/t4.8-review-record.md` line 4: "The AutoResolver is NOT in this packet. m4-spec §1.6 keeps strategic war at the resolver only, and the resolver itself is deferred to M6 by director ruling." Notables' conservation surface and three-of-four lifecycle shipped; table always empty (no production driver). See **B1** |
| T4.9 lattice stride | **RULED, docs-only** | `docs/t4.9-review-record.md` — stride stays 4; rivers reassigned to T4.7; **village catchments left unowned** (see F2) |
| T4.10 T3.10 migrated work | **COMPLETE** | `docs/t4.10-review-record.md`; merged at `34f7ad4`/`e6cf705` |
| T4.11 merchants | **COMPLETE** | `m4-exit-inventory.md` §5; "verified to emerge in a real 650-turn canonical run" |
| T4.12 migration-weight | **COMPLETE** | `docs/t4.12-derivation-record.md`; merged with T4.10 |
| T4.13 comfort-as-stock → happiness | **COMPLETE** | derived 0..100, pure query, not a stock, not serialized; class **A** |
| T4.14 three M3 observations | **COMPLETE** | `docs/t4.14-review-record.md` |
| T4.15 M4 exit artifact | **PARTIAL — see A1/A2/A3** | `docs/m4-exit-inventory.md` exists and is thorough; but the packet's own scope line reads "version strings, README, sweep incl. nightly, milestones entry, session brief", and **the version strings, the README, the `milestones.md` entry and the `m4-exit` tag are all still absent from the tree** |
| T4.16 clone architecture | **DESIGN COMPLETE, RULING PENDING** | `docs/adr/adr-020-clone-architecture-r3.md` — "**STATUS: DESIGN AND MEASUREMENT COMPLETE. No implementation. Awaiting director ruling on which candidate, if any, is scheduled and when.**" See **C1** |

**Post-certification packets on the candidate only:** T4.17 (`docs/session-records.md`), T4.18
(`docs/m4-population-transient-investigation.md`), T4.19 (`docs/t4.19-verification-record.md`,
`docs/observability-architecture.md`, `docs/m4-observability-gaps.md`,
`docs/m4-founding-demographics-correction.md`, `docs/t4.19c-remeasurement.md`,
`docs/m4-player-information-ui.md`, `docs/m4-control-audit-and-screen.md`,
`docs/explain-queries.md`), T4.20 (`docs/t4.20-food-semantics.md`). None is an `m4-spec` §4 packet;
each was a director fix packet layered on the certified baseline.

### `m4-spec.md` §6 exit criteria, one by one

| criterion | state | evidence |
| --- | --- | --- |
| All packets accepted; each merged on a director ruling | **MET** for T4.1–T4.16 | `main` = `dbef61a`, the M4 completion merge |
| Scarcity can bite: starvation reachable, five Q-B predictions reported individually | **MET** | `m4-exit-inventory.md` §1 and §0A; measured here — 3 starvation deaths on dev seeds 7 and 42 in this very run |
| Determinism suites green on the M4 world; xproc; first-reign shape asserts standing | **MET** | measured above: replay, save/load, schema and every golden/pin test green; xproc closed as CR-013/ADR-022 |
| Goldens pinned with dated history lines; driven golden extended | **MET** | `m4-exit-inventory.md` §10; `t4.19-verification-record.md` §3 and §9 |
| Calibration battery green across ≥20 seeds with proven teeth, quarantined corridors reported with measured ranges | **PARTIALLY MET — see A4** | density quarantine LIFTED at M4 completion on 20/20 in band; but the candidate's founding correction pushes it back OUT and the quarantine was deliberately left inactive |
| The nightly has been green, and someone has read it — the M3 CI defect closed | **NOT DEMONSTRABLE FROM THE TREE — see A4 and F3** | `.github/workflows/ci.yml` is quarantine-aware, which closes the *instrument* defect; but no record in the tree shows a green M4 nightly having been read, and on the candidate the gate would breach |
| Director exit session from the CI zip, log replaying hash-identical, with a T3.12a replay report attached | **NOT YET — this is the playtest** | the machinery is complete and tested (`docs/session-records.md`, `sim inspect`, `SessionRecordTests`); the session itself is the director's to play |
| `milestones.md` M4 entry with its known-open list; `m4-exit` Release | **NOT MET — see A2, A3** | `docs/milestones.md` has M1, M2 and M3 sections and **no M4 section**; `git ls-remote --tags origin` returns m0/m1/m2/m3-exit only |

### S8 §4.1's four mandatory items — does `m4-spec` satisfy them?

`m4-spec.md` §0 answers this requirement by requirement, and the tree bears it out:

1. **Foundations audit as packet one** — SATISFIED. T4.1 is §4's first packet with four named
   checks, and it ran: `docs/t4.1-manifest.md` plus six sub-packets, and its findings were promoted
   into T4.3's prohibitions.
2. **Dimensional declaration** — SATISFIED AS DECLARED. §3.2 declares units for what the spec
   commits to; §0 states honestly that M4's core equations did not exist at spec time and that each
   packet completes its own declaration. This is a limit the spec *states* rather than papers over,
   which is what §4.1 asks for.
3. **Corridor independence** — SATISFIED. §3.3, with the `densityPerArableKm2` failure case carried
   as the project's standing example of a self-referential corridor (CR-002's cancellation-identity
   refutation).
4. **Coupling map** — SATISFIED AS PROVISIONAL, then revised. §3.4 is marked "REVISED BY T4.1 UNDER
   THE LIVING CLAUSE", which is exactly the mechanism §4.1 specifies (T4.1 authoritative over the
   map).

**Verdict: `m4-spec.md` conforms to S8 §4.1.** No finding here.

### What `docs/civ-sim-architecture-v3-outline.md` (the Spine) says M4 owes

Three lines mention M4, and one is a real divergence worth naming:

- Spine line 80: "Trade & logistics | M4 | region-graph routing only" — **delivered** (T4.6/T4.7).
- Spine line 81: "Conflict v1 (attrition war) | M4 | manpower from real cohorts from day one", and
  line 108: "attrition-model war drawing manpower from real cohorts" — **NOT delivered.** M4 ships
  no armies and no AutoResolver.
- Spine line 14: "**Open values question (must be answered before M4):** do battles need to be
  fought/watched, or only resolved and reported?"

This is **not** an M4 blocker, and the reason is in the hierarchy rather than in anyone's
judgement: `docs/d011-battle-layer-addendum.md` §6 is a later accepted D-decision (rank 2) that
resequences the battle layer into M6 and answers the Spine's open values question, and it outranks
the older architecture outline (rank 7) under `current-state.md` §5 / GOV-4 §1. See **B1**.

---

## §4 EVERY ADR AND CR, STATUS LINE VERBATIM

Read from `docs/adr/` on `79dec68`.

| file | status line, verbatim |
| --- | --- |
| adr-001-unmanaged-table-rows | "**Status:** accepted (director-sanctioned, recorded at T0.3 session setup)" |
| adr-002-integer-day-clock | "**Status:** accepted (director-sanctioned, recorded at T0.4 session setup)" |
| adr-003-typed-owned-tables | "**Status:** accepted (recorded during T0.5; refines the §3.1 sketch)" |
| adr-004-conserved-wrapper | "**Status:** accepted (director-sanctioned mechanism, recorded at T0.6)" |
| adr-005-canonical-schema | "**Status:** accepted (director-sanctioned constraints, recorded at T0.7)" |
| adr-006-determinism-gates | "**Status:** accepted (recorded at T0.8)" |
| adr-007-turn-observer | "**Status:** accepted (director-sanctioned, recorded at M0 closure)" |
| adr-008-static-terrain | "**Status:** accepted (pre-authorized by D-024, m1-walking-skeleton-spec §1)" |
| adr-009-ui-stack | "**Status:** accepted (director-sanctioned D-003 amendment, T1.7 session order)." |
| adr-010-slot-advance-aging | "## Status / Accepted (T2.1). Touches the demographics resolution mechanism only…" |
| adr-011-exponential-survival | "**Status: accepted** (director ruling on CR-001, option (a); T2.8 session, T2.7b work)." |
| adr-012-destination-viability | "**Status: accepted** (T2.13, director packet — M2 exit held on the defect this fixes)." |
| adr-013-lattice-denomination… | "**Status:** ACCEPTED (directed packet T3.2b; packet accepted IN FULL by the CR-003 ruling, 2026-07-26)" |
| adr-014-spec-format-foundations-audit | "**Status:** ACCEPTED — director ruling 2026-07-26" |
| adr-015-verification-hygiene | "Status: **RATIFIED** (director ruling, 2026-07-26 — §6 accepted as proposed; §7 added under the…" |
| adr-016-exact-price-integration | "Status: **RATIFIED** — directed amendment to D-033 by the mandate's author, 2026-07-26" |
| **adr-017-d025-founding-variation** | "Status: **proposed (director certification pending)** · Packet: T3.6b · Date: 2026-07-29" — see **C2** |
| adr-018-d025-minimum-spacing | "**Status:** ACCEPTED (director ruling, 2026-08-08). Amends **D-025**, a frozen decision." |
| **adr-019** | **ABSENT from the candidate tree.** Exists only on `origin/adr-019-architecture-addendum` (`a36b94d`, unmerged). See **E1** |
| **adr-020-clone-architecture-r3** | "**STATUS: DESIGN AND MEASUREMENT COMPLETE. No implementation. Awaiting director ruling on which candidate, if any, is scheduled and when.**" — see **C1** |
| adr-021-unplaced-departure-demand | "**Status: ACCEPTED** (director ruling, T4.4 — Option A)." |
| adr-022-reference-platform-linux-x64 | "**Status: ACCEPTED** (director ruling on CR-013, T4.19-B: options 1 + 3 accepted, option 2…)" |
| cr-001-dt-fragile-demography | "**Status: CLOSED — director ruled OPTION (a), exact-exponential cohort integration**" |
| cr-002 | "**Status:** RESOLVED 2026-07-26 — the directed packet T3.2b (ADR-013) fixed the denomination at its root and was accepted in full by the CR-003 ruling §4. …" |
| **cr-003** | "**Status:** RULED 2026-07-26 — Option 3 ACCEPTED (see §5, the ruling). The Malthus corridors remain quarantined BY RULING until a mechanism legitimately restores them; the derived 26.0 stands and is not negotiable against any corridor." |
| cr-004-granary-turn-length-mismatch | "**Status: WITHDRAWN BY ITS OWN EVIDENCE. No code change proposed or applied.**" |
| **cr-005-m5-research-technology-institutions-placement** | "**Status: OPEN — awaiting director ruling. No frozen document has been edited.**" — see **D1** |
| **cr-006-continuous-time-and-campaign-epoch** | "**Status: OPEN — awaiting director ruling. No frozen document has been edited and…**" — see **D1** |
| cr-007-b3-exemplar-reconciliation | "**Status: RESOLVED WITHOUT A NEW RULING — the alleged contradiction does not…**" |
| cr-011-founding-polity-id-vs-the-order-corpus | "**Status: RULED (director, 2026-09-01). Option 1 accepted: the initial player Empire is `PolityId 1`.**" |
| cr-012-malthus-architecture | "**STATUS: RULED — KEEP.** CR-012 closed. No code changed; none is authorized by this ruling." |
| cr-013-cross-platform-determinism | "**Status: CLOSED / ACCEPTED (director, T4.19 finalization) — as implemented in…**" |
| cr-014-craft-input-cap-ignores-the-banked-remainder | "**Status: CLOSED / ACCEPTED (director, T4.19 finalization) — option 1 accepted as…**" |
| **cr-008, cr-009, cr-010** | **ABSENT from the candidate tree.** `cr-008` exists only on `origin/m5-full-build` (`83c8c1a`: "M5 phases 1-3: CR-005 ruled, CR-008 raised…"); `cr-009` and `cr-010` were never added on any branch — `git log --all --diff-filter=A` returns nothing for either. See **E2** |

**Standing OPEN CRs on the candidate: CR-005, CR-006 — and neither blocks M4** (both are M5
ownership/scheduling questions; `m4-exit-inventory.md` §9 says so explicitly). **CR-003 is RULED,
not open** — its ruling is *that the quarantine stands*, which is why its two reds are expected.

---

## §5 `docs/current-state.md` — HOW IT IS STALE, CLAIM BY CLAIM

The file is dated **"Measured 2026-08-31"** and marks itself as a router that must be re-derived
(its own §9). Re-derived here, four of its load-bearing claims are wrong on the candidate:

1. **"`origin/main` = `070f05b` (T4.4 colonization, schema v22)" — STALE.** `git rev-parse
   origin/main` returns **`dbef61a`**, "Merge M4 completion (director-certified): the M4 baseline",
   and schema is **v24**, unchanged across the whole completion pass
   (`m4-exit-inventory.md` header).
2. **"M4-B, M4-C and M4-D CERTIFIED on `m4-empire-order-seam` — not merged; the merge is the
   director's" — STALE.** All three are merged: `main`'s tip *is* the M4 completion merge and its
   history carries `7f725f2` (colony control inheritance, derived happiness, revolt),
   `badef96` (merchants, AI-count seam, calibration dispositions, exit artifact) and
   `78fd288` (M4 exit certification). MAIN is now true where the file says only REMOTE was.
3. **§2's milestone contradiction — RESOLVED, and the file has not caught up.** §2 says
   "`CLAUDE.md:10` reads **Current milestone: M3**". `CLAUDE.md:10` on the candidate reads
   "**Current milestone: M4 — active packets: `docs/m4-spec.md` §4.**" The contradiction §2 records
   is gone; §2 itself is now the stale artifact.
4. **Its §4 open-CR list names CR-008, CR-009 and CR-010 — two of which have never existed.**
   Verified by `git log --all --diff-filter=A -- docs/adr/cr-009* docs/adr/cr-010*`, which returns
   nothing on any branch. CR-008 exists only on `origin/m5-full-build`. On the candidate the open
   set is CR-005 and CR-006.

Additionally its §4 certification runs (541–572 passed / **6 failed** / 6 skipped, with two
`ClassSystemTests` reds) are superseded by the measurement in §2 above: **722 / 4 / 6**, and neither
`ClassSystemTests` red survives.

**Classification: E3.** `current-state.md`'s own update rule (§8) is "update after an accepted
implementation packet"; four accepted packets have landed since. It is not a blocker — it announces
its own unreliability in its first three lines and GOV-4 §1 ranks it below the tree — but the next
agent entering with zero context will be misled by it, which is precisely the failure mode it
exists to prevent.

`docs/handoff-status.md` is likewise stale (dated 2026-07-28 against `t3.4c-variance-fix` at
`719152a`, "355/355 passing", milestone M3). It is *already declared* stale and deliberately
preserved by `current-state.md` §6 as a historical measurement. **Classification: B** — an
explicitly ruled do-not-touch document, not an outstanding item.

---

## §6 THE SIX BUCKETS

### A — M4 BLOCKERS (4)

Everything here is required by `docs/m4-spec.md` §6 or by T4.15's own stated scope, and is
currently absent from the tree.

**A1 — The version strings still say M3, and a test pins them there.**
`Sim.Ui/BuildInfo.cs:18` → `Describe() => $"civ-sim M3 ({Sha}, {Date})"`, asserted by
`Sim.Ui.Tests/UiSessionReplayTests.cs:128` (`Assert.Equal("civ-sim M3 (dev, local)", …)`).
`README.md:1` is "# civ-sim (M3)", line 6 "**M3 — The economy arrives**", line 226 documents the
window title as `civ-sim M3`, line 288 lists M3 as "AT THE EXIT GATE", and line 36 points a reader
at `docs/m3-spec.md` as "(current milestone spec)". T4.15's scope line names "version strings,
README" first.
**To close:** edit `BuildInfo.cs`, its pinned assertion, and the README's milestone sections.
Docs and one string; no simulation change, no golden movement.

**A2 — `docs/milestones.md` has no M4 entry.**
`grep -nE '^#+.*M4' docs/milestones.md` returns nothing; the file has "M1 exit checklist", "M2 exit
checklist" and "M3 — The economy arrives *(at the exit gate)*" with its known-open list, and then
ends. `m4-spec.md` §6 names "`milestones.md` M4 entry with its known-open list" as an exit
criterion, verbatim.
**To close:** one section, with the known-open list already written for it in
`m4-exit-inventory.md` §7/§8/§9 and §12.

**A3 — There is no `m4-exit` tag.**
`git ls-remote --tags origin` returns `m0-exit`, `m1-exit`, `m2-exit`, `m3-exit` only.
`m4-spec.md` §6 ends "…; `m4-exit` Release", and `m4-exit-inventory.md`'s header row already
reserves the name under "repository convention: `m2-exit`, `m3-exit`".
**To close:** cut the tag at the merge. This is genuinely the director's action, not an agent's —
it marks the accepted commit — so it closes *with* the merge approval rather than before it.

**A4 — The density corridor is out of band on the candidate, the quarantine is inactive, and on
the evidence in the tree the CI *nightly gate* has not been checked against that.**
This is the one A item that is not paperwork, and it needs care, because the director has already
ruled on half of it.

*What is ruled.* `docs/t4.19-verification-record.md` §9 ("MEASUREMENT HELD") records the director's
ruling: "`canonical.densityPerArableKm2 = [0.15, 0.60]` kept unchanged; formula, growth, founding,
arable and the test untouched. `Canonical_FedCorridors_AllInBand` seeds 1, 2 stay red." That ruling
is explicit, and the two reds I measured above are its expected output. **That half is settled.**

*What appears not to be.* The same tree also runs a **second** density instrument: the
`calibration-nightly` job in `.github/workflows/ci.yml`. Its gate step is quarantine-aware —
`def gated($c; $v): ($c.quarantine? and $c.quarantine.active) or ($v >= $c.band[0] and $v <=
$c.band[1]);` — so a corridor gates unless its quarantine is *active*. On the candidate,
`Sim.Data/content/corridors.json` has `canonical.densityPerArableKm2.quarantine.active = false`,
lifted at M4 completion with `liftedBy: "M4 completion, director ruling (2026-09-04)"` and
`liftEvidence` of 20/20 seeds in band at `e6cf705` (max 0.56483). But T4.19 lane C's founding
correction moved the numerator: `docs/t4.19c-remeasurement.md` §3.2, cited in the verification
record, measures the NEW arm at **14/20 inside, min 0.36857 / mean 0.54063 / max 0.74211 — six
seeds over the 0.6 ceiling**. `docs/m4-founding-demographics-correction.md` §10 confirms
`corridors.json` bands and quarantine windows were "deliberately left alone".

So on the candidate the nightly's own predicate returns false for six of twenty seeds and the job
would print `NIGHTLY CORRIDOR BREACH` and `exit 1`. That directly contradicts the `m4-spec.md` §6
criterion "**The nightly has been green, and someone has read it**". I searched
`m4-exit-inventory.md`, `t4.19-verification-record.md` and `queue.md` for any record of the nightly
being considered when the density measurement was held: **there is none.** The verification record
discusses only `Canonical_FedCorridors_AllInBand`, the in-process battery.

*Honest framing.* I am not asserting the director ruled wrongly, and I am **not** proposing a
re-band — CR-002's cancellation-identity precedent and CR-003's ruling both forbid fitting the
instrument to the artifact, and the record is emphatic that the band must not be re-cut to match
whatever the world does. What I am reporting is that a stated exit criterion and a live CI gate
disagree with a held measurement, and nothing in the tree shows anyone noticed.
**To close, three routes, none chosen here:** (a) re-activate the density quarantine with a window
covering the measured NEW-arm range, so the nightly *reports* it with its measured range instead of
gating — the exact mechanism T3.12 built and the shape used for `migrationGrossPerDecade`, which
remains `active: true` and therefore does not gate; (b) re-derive the ceiling on 20 seeds, which
the verification record already names as the director's option and which is a calibration packet,
not a doc edit; or (c) rule explicitly that the M4 nightly criterion is discharged for migration
and density *as reported* and accept the red, which would mean amending the §6 wording. **(a) is
the cheapest and is the mechanism the project already ratified for exactly this case**, but which
route is taken is the director's, not mine.

### B — STANDING RULED EXCEPTIONS (8)

**B1 — No AutoResolver, no armies; the Spine's "Conflict v1 at M4" is not delivered.**
Permitted by `docs/d011-battle-layer-addendum.md` §6 (battle layer resequenced to M6; "M4 = trade +
strategic war, **AutoResolver only**"), carried into `m4-spec.md` §1.6 and §2 ("**M6 FIGHTS**"),
executed at `docs/t4.8-review-record.md` line 4, and certified by the director in
`m4-exit-inventory.md` §0A as class **D** ("no armies; no AutoResolver; player-side auto-win is the
standing boundary until the military stage") on the merge that made `dbef61a`. D-011 is a later
accepted D-decision and outranks the Spine outline under GOV-4 §1.
*One traceability note, not a reopening:* `m4-spec.md` §1.6 itself says "Strategic war is
AutoResolver ONLY at M4", so the spec expected an AutoResolver and T4.8 shipped without one. The
ruling that dropped it is the director's certification of the exit artifact, not a separate written
ruling. That is sufficient authority; it is simply worth knowing where the ruling lives.

**B2 — CR-003 Malthus quarantine: `Dev_MalthusCorridors_AllInBand` seeds 7 and 42 red.**
CR-003 "**Status:** RULED 2026-07-26 — Option 3 ACCEPTED. The Malthus corridors remain quarantined
BY RULING until a mechanism legitimately restores them." §7.6 anticipates the exact message the
tests emit; `m4-exit-inventory.md` §12 forbids reopening at M5 startup.

**B3 — `Canonical_FedCorridors` seeds 1 and 2 red on density.**
Ruled held at `t4.19-verification-record.md` §9 "MEASUREMENT HELD" — recorded as an OBSERVED
CALIBRATION RESULT and a deferred calibration question, explicitly not a re-band. *(The in-process
battery half only; the nightly half is A4.)*

**B4 — Four measurement-rig `Skip=` tests.** Kept off the default path by design, each naming the
review record that holds its numbers; `current-state.md` §4 and `gov-4-repository-freshness.md` §9
both record the standing rule.

**B5 — T4.3 claims/control/recognition dormant; T4.8 notables table always empty; foreign-trade
classification unreachable at `aiEmpires = 0`.** All three certified as class **B** in
`m4-exit-inventory.md` §0A and explained in §7 "WHAT IS DORMANT, AND WHY THAT IS HONEST".

**B6 — Migration corridor 1/20 in band, cause unidentified.** `corridors.json`
`migrationGrossPerDecade.quarantine.disposition`: "ACCEPTED AS MEASURED (M4 completion, director
ruling 2026-09-04) - NOT tuned, NOT re-banded", `quarantine.active: true` so it does not gate the
nightly. `m4-exit-inventory.md` §11 and §12: "Migration is not an M5 tuning target."

**B7 — Artisan emergence `[10, 95]` window retired.** Director ruling executed at T4.19-E
(`t4.19-verification-record.md` §10), replaced by four structural tests. *(The structural set's
composition is C3.)*

**B8 — `docs/handoff-status.md` stale and deliberately preserved.** `current-state.md` §6 declares
it superseded for routing and "left untouched as a historical measurement"; GOV-4 §6 forbids
rewriting frozen records.

### C — DIRECTOR RULING REQUIRED (6) — *not resolved here, by instruction*

**C1 — ADR-020 clone architecture.** Verbatim: "STATUS: DESIGN AND MEASUREMENT COMPLETE. No
implementation. Awaiting director ruling on which candidate, if any, is scheduled and when." T4.16
delivered exactly what `m4-spec.md` §4 asked of it (an ADR, not a code change), and the spec says
"the implementation slot is the ADR's to propose and the director's to rule". **So this does not
block M4** — the packet is complete; the ruling schedules future work. Listed in C because a
question is genuinely put and unanswered.

**C2 — ADR-017 is still "proposed (director certification pending)", dated 2026-07-29.**
It is nonetheless *relied upon*: `m4-spec.md` §4 T4.1 check 4 cites its "SITING: STANDS UNAMENDED"
ruling as settled, and ADR-018 (ACCEPTED 2026-08-08) amends the same D-025. Genuinely ambiguous —
either the certification happened and the status line was never updated, or an accepted ADR now
sits on top of an uncertified one. Only the director can say which.

**C3 — The T4.19-E structural test set was chosen by the implementer and flagged for
confirmation.** `t4.19-verification-record.md` §10: "**THE STRUCTURAL SET — the implementer's
reading of 'the structural tests above', FLAGGED FOR THE DIRECTOR'S CONFIRMATION.** The director's
list of structural tests did not arrive with the ruling." Four tests S1–S4 were written under a
stated reading; S3's independent mutant kill was "not independently observed" and recorded as such.

**C4 — Food semantics: are grain, livestock and fish the same kind of thing?** `t4.20-food-
semantics.md` Phase 4 findings 1–3: grain is bounded, spoils, overflows, is raided, provisions
colonies, gates migration and is the founding endowment; livestock and fish are none of those; and
"the repository records NO intent either way". Measured: grain does 88.1 % of the feeding while
holding 0.8 % of the food inventory at turn 300. The document is explicit that "a director ruling,
not an agent's judgement, is what closes it" and that the answer is an ADR, not a code change.
**Not an M4 blocker** — nothing is mis-computed and no identity fails (Phase 4: "Nothing here is
classified BUG") — but it is a real missing decision record.

**C5 — Observability §0 wording: is a cross-sectional difference "DIFFERENCED"?**
`t4.20-food-semantics.md` Phase 5 and `queue.md`: `FoodBalance` is `FoodProduced − DemandUnits`,
two same-turn terms, while `observability-architecture.md` §0 defines DIFFERENCED as next − prev.
The field is labelled "DIFFERENCED (cross-sectional, same turn)" pending a ruling that either
widens the wording or creates a sixth kind. §0 was deliberately not amended by an implementation
packet — the correct restraint.

**C6 — Publishing `BoundStore`'s capacity and spoil/overflow split as public statics.**
`m4-observability-gaps.md` gap 2 and `t4.20-food-semantics.md` Phase 5: "**NOT AUTHORIZED.** It is
a change under `Sim.Core/Systems`, outside this packet's absolute fence, and it touches a system the
goldens pin. It is queued for a director ruling and is not attempted here." Correctly refused;
queued.

*(Also in C by strict reading, though the record treats them as answered: gap 10's "a director
ruling on an attribution rule, or none" for per-need grievance accrual.)*

### D — M5+ , EXPLICITLY DEFERRED (7)

**D1 — CR-005 (M5 ownership of Research/Technology/Institutions) and CR-006 (continuous time and
campaign epoch)** — both "OPEN — awaiting director ruling"; `m4-exit-inventory.md` §9: "open;
neither blocks M4". Placeholder specs exist at `docs/m5-research-technology-institutions-
placeholder.md` and `docs/m5-temporal-control-and-player-agency-placeholder.md`.

**D2 — Money, taxation, the fiscal system** — `m4-spec.md` §1.1 and §2 ("**M5 GOVERNS**"); GOV-2
§1a; notables' *purchase* lifecycle not merely omitted but "not expressible, because there is no
money" (`t4.8-review-record.md` §1). *(But see F4 — the record says money itself is unowned.)*

**D3 — The battle layer, BattleSetup/BattleOutcome, general stats and experience** — D-011 §6, M6.

**D4 — D-039 command friction, reconnaissance, siege** — `m4-spec.md` §2: M6, except Part B's
investment mechanism; D5 siege starvation additionally hard-blocked on B-2.

**D5 — Map art and composed settlement sprites** — `m4-spec.md` §2: the visual milestone sits
between M5 and M6 (D-038 E1). "No M4 packet authors map art."

**D6 — Observability gaps 12 and 13** (`m4-observability-gaps.md`): player trade lever — "M5+
policy"; policy consequence attribution — "a counterfactual replay from the change turn —
deterministic, so feasible; a later packet".

**D7 — Cultural plurality (M8/M9), vintaged capital, D-040 map extent/discovery (M7) and the
political consequences of weak control (M8)** — `m4-spec.md` §2 and the D-040 note in
`d011-battle-layer-addendum.md`.

### E — HOUSEKEEPING (6)

**E1 — ADR-019 exists only on an unmerged branch.** `origin/adr-019-architecture-addendum`
(`a36b94d`) holds the only copy; the ADR sequence on `main` jumps 018 → 020. `current-state.md` §3
already flags this ("Do not treat the ADR sequence on main as complete"). Non-blocking, but an
agent reading `docs/adr/` will silently miss an architecture addendum.

**E2 — CR-009 and CR-010 are cited by `current-state.md` §4 and have never existed.** Verified by
`git log --all --diff-filter=A`. CR-008 exists only on `m5-full-build`. Fixed by E3.

**E3 — `docs/current-state.md` is stale in four load-bearing claims** (§5 above). Its own §8 update
rule triggers on an accepted implementation packet; four have landed. Cheap to fix, and it is the
first file the next agent reads.

**E4 — `Explain/CausalChain.cs:318` still selects food by `goods.json`'s `"category":"food"`
string**, while T4.20 moved `SettlementRecord.FoodGoods` onto `BasketBook.FoodGoods` (the
simulation's own rule). `queue.md`: "this remaining occurrence was outside the packet's scope…
Same divergence risk under a needs.json tuning edit. Reconcile in a packet that owns that file."
One expression, no behaviour change expected, but it is a second rule for the same question.

**E5 — `docs/m4-integration-audit.md` §3 states "M4's §6 exit criteria cannot be met while CR-003
is open".** Superseded by its own header note (lines 10–14) and by the exit ruling: §6's
calibration criterion explicitly allows quarantined corridors "reported with measured ranges rather
than silently gating", which is what CR-003's corridors do. The document is evidence, not
authority, and says so at line 23 — but the sentence will mislead a reader who stops there.

**E6 — Twelve `m4-*.md` documents, several superseded in part, with no index.** The authoritative
exit artifact is `docs/m4-exit-inventory.md`; `m4-blocking-material.md`, `m4-pre-spec-
dependencies.md` and `m4-integration-audit.md` are pre-decision evidence documents that read as
current. GOV-4 §6 forbids rewriting them, which is right — but A2's `milestones.md` entry is the
natural place to say which is which.

### F — UNOWNED / NEEDS AN OWNER (4)

**F1 — Two skipped migration tests name "Owner: M4 migration", and M4's migration work has
shipped.** `MigrationTests.cs:378` and `:532`, verbatim: "Lifts when M4's migration work
re-derives the assertion. Owner: M4 migration." T4.10 and T4.12 were that work — they dropped the
food term from migration attractiveness and shipped at `34f7ad4`/`e6cf705` — and the assertions
were not re-derived; the skips remain. No document in the tree disposes of them. Either the
obligation passes to M5 or it lapses, and nobody has said which. **This is the closest thing in
bucket F to a genuine loose end**, because the skip text asserts an owner that has since closed.

**F2 — Village-scale catchments.** `docs/t4.9-review-record.md` §"Village-scale catchments — remain
unowned, not solved here": "No currently-scheduled milestone claims sub-32 km catchments (GOV-2
§2d: 'unowned — no milestone…')". Carried unowned through T4.9's ruling by design; still unowned.

**F3 — "A nightly failure must surface where someone reads it."** `queue.md`: "**CI PROCESS DEFECT
— A NIGHTLY FAILURE MUST SURFACE WHERE SOMEONE READS IT. OWNER: CI, M4-era (director ruling,
T3.12).**" The *instrument* half was closed — `ci.yml` is now quarantine-aware and `corridors.json`
is the single source both readers use, so the eleven-silent-reds masking cannot recur. The
*notification* half was not: the queue entry lists four candidate mechanisms ("fail the scheduled
run loudly into a channel the director reads; a badge; a 'days since last green nightly' line in the
run summary; or a follow-up issue opened automatically on the first red") and records "none chosen
here". No mechanism exists in `ci.yml` today. Directly relevant to A4: the reason nobody noticed the
density gate is the defect this entry describes.

**F4 — Money is unowned.** `queue.md:1210` and `docs/capability-architecture-decision.md:329`:
"**Money is unowned and must be assigned.**" — M4 rules it out (GOV-2 §1a, taxes in kind) and GOV-2
§1a also rules money is not at M5, so no milestone currently owns it. Not an M4 blocker; M4's fence
is clean. Recorded because the record says someone must own it and nobody does.

*(Also unowned, MINOR: "D-018 artisan trigger vs shipped predicate (D-040 F2, unowned)",
`capability-architecture-decision.md:377` and `milestone-architecture-governance.md:224`. And
`queue.md:1063`: "**OPEN DESIGN ITEM, NO OWNER — SPACING SHOULD DERIVE FROM COMPUTED STATE (law
4).**" — recorded with the director's own two-step reasoning, deliberately not scheduled.)*

---

## §7 THE DIRECT ANSWER

**Is M4 enough done? No — but only just barely, and not because of the simulation.**

The simulation is done. Every `m4-spec.md` §4 packet T4.1–T4.16 is merged and director-certified on
`main` at `dbef61a`. The candidate adds four post-certification fix packets and, measured here in
Release on `79dec68`: **722 passed / 4 failed / 6 skipped** in `Sim.Tests`, **236 / 236** in
`Sim.Ui.Tests`, **0 build warnings**, and all three repository gates OK. The four reds are
*exactly* the four expected standing reds and nothing else. There is not one `TODO`, `FIXME` or
`HACK` in the simulation source. `m4-spec` conforms to S8 §4.1 on all four mandatory items.

What stands between the candidate and closure is **four items, three of which are paperwork**:

| # | what remains | what closing it requires |
| --- | --- | --- |
| **A1** | `BuildInfo.cs` and `README.md` still say **M3**, pinned by a UI test | one string, one test assertion, and the README's milestone sections — docs and one literal |
| **A2** | `docs/milestones.md` has **no M4 section**, which §6 names as an exit criterion | one section; its known-open list is already written in `m4-exit-inventory.md` §7–§9 and §12 |
| **A3** | **no `m4-exit` tag** on origin — only m0/m1/m2/m3 | cut the tag at the merge; this is the director's action and closes *with* the merge approval |
| **A4** | the density corridor is **out of band on 6/20 seeds** with its quarantine **inactive**, so the `calibration-nightly` gate would breach — against §6's "the nightly has been green, and someone has read it". The in-process battery half is ruled held; the nightly half appears unexamined | one director ruling, choosing among: (a) re-activate the quarantine with a window over the measured range so the nightly *reports* rather than gates — the ratified T3.12 mechanism, already in use for migration; (b) re-derive the ceiling on 20 seeds (a calibration packet); or (c) rule the §6 nightly criterion discharged as-reported. **Do not re-band to fit** — CR-002 and CR-003 both forbid it |

**A1–A3 are an hour of work and move nothing.** A4 is the only item that needs the director to
think, and he is already half-way through it: he ruled at T4.19 §9 that the density measurement is
held and the two battery reds stand. A4 asks him to extend that same ruling to the second
instrument that reads the same corridor.

**Once A1–A4 are closed, bucket A is empty and the only thing left between the candidate and
closure is the director's playtest** — the §6 exit session from the CI zip, replaying
hash-identical with a T3.12a replay report attached — **plus his merge approval.** That machinery
is complete and tested: `docs/session-records.md` describes the four-file session record, `sim
inspect` answers "around turn 85 something went wrong", `SessionRecordTests` proves a played session
reproduces turn-for-turn from its own log, and ADR-022 tells him exactly how to read a Windows
session on a Linux runner.

Nothing in buckets B through F blocks M4. B is ruled, D is deferred by name, E is cosmetic, and F is
four loose threads the record itself flags as ownerless — of which **F1 (two skipped migration tests
whose stated owner, "M4 migration", has shipped and closed) is the one worth a sentence in the M4
entry**, so it is carried forward deliberately rather than lost.

---

## §8 PROVENANCE

Measured in a detached worktree pinned to `79dec68`, 2026-09-15: the Release build, both test
suites, all three gate scripts, every `Skip=` string, every ADR/CR status line, the
`corridors.json` quarantine blocks, `.github/workflows/ci.yml`'s gate predicate, the absence of an
M4 section in `milestones.md`, the M3 strings in `BuildInfo.cs` and `README.md`, and the code greps.
Read from git in `/home/user/civDemo` after `git fetch --all`: all branch tips, `git ls-remote
--tags origin`, and `git log --all --diff-filter=A` for ADR-019 / CR-008 / CR-009 / CR-010.

Carried from documents in the tree, and therefore **secondary evidence** under GOV-4 §1, each cited
inline where used: the 20-seed NEW-arm density distribution (`t4.19c-remeasurement.md` §3.2, not
re-run here — 650-turn × 20-seed sweeps were out of this sweep's budget), the per-packet review
records, and the director rulings recorded in `t4.19-verification-record.md` §8–§10 and
`m4-exit-inventory.md`.

**Nothing in this document was resolved, ruled on, or fixed.** No file outside `docs/` was touched,
and no file at all was touched in the worktree the measurements were taken in.
