# CURRENT STATE — the routing document

> **STALENESS CORRECTION, M4 exit gate (2026-09-16).** The measurement below is dated 2026-08-31 and
> four of its load-bearing claims have since been overtaken by git. They are corrected inline and
> marked `CORRECTED`; nothing else in this file was rewritten, per the director's instruction that the
> remaining stale-document findings stay housekeeping. Re-derive anything you rely on, as §9 says.
>
> | claim as written | git, re-derived 2026-09-16 |
> | --- | --- |
> | `origin/main` = `070f05b`, schema v22 | **`dbef61a`**, "Merge M4 completion (director-certified): the M4 baseline", schema **v24** |
> | M4-B/C/D "CERTIFIED … not merged" | **all merged** — main's tip IS the M4 completion merge |
> | §2's milestone contradiction (`CLAUDE.md:10` reads M3) | **RESOLVED** — `CLAUDE.md` now reads M4 with `docs/m4-spec.md`. §2 is itself now the stale artifact |
> | open CRs include CR-009, CR-010 | **neither has ever existed on any branch**; CR-008 exists only on `m5-full-build` |
>
> The current M4 exit candidate is `t4.19-glass-box`. Its suite, measured in Release at the exit gate:
> Sim.Tests **722 passed / 4 failed / 6 skipped**, Sim.Ui.Tests **236/236**, three gates green — which
> supersedes the 541–572 / 6 / 6 counts recorded below.
>
> **2026-09-17 — T4.21 IN PROGRESS on `claude/civdemo-work-b1z2y4`** (= `main` `dbef61a` + the unmerged
> M4 playtest build `2807155` + the forensic CLI, `45046eb`): famine semantics, bounded migration and
> shock integration. Governance landed first (T4.21-0, branch `t4.21-0-governance`):
> `docs/adr/cr-015-famine-is-exceptional.md` RULED from the director's 2026-09-17 mandate (G1 — the
> harvest-weather decade variance — ESCALATED and open), ADR-024/025/026, spec
> `docs/t4.21-architecture.md`, evidence `docs/t4.21-evidence/`. Code packets T4.21-1..6 follow on
> their own branches; nothing is merged to `main`. Suite baseline on `45046eb` (Release): 754 passed /
> 2 failed (`Dev_MalthusCorridors` seeds 42, 7 — red by CR-003 ruling) / 6 skipped. Verify against git.
> **2026-09-18 — T4.21-4 DONE, on branch `t4.21-4-arm` (cut from `claude/civdemo-work-b1z2y4` @
> `8f7f9da`), NOT merged.** The famine-class disaster is ARMED (`sim.json disaster.hazardPerYear`
> 0.0 → 0.01). Record: `docs/t4.21-4-record.md`. **ESCALATION — READ BEFORE RELYING ON THIS TREE:**
> `docs/adr/cr-016-armed-disaster-fallout.md` is OPEN — the ruled arming breaks the CR-001 permanent
> dt-continuity detonator at the era gate (measured 2.5259 per 1000 yr against a 0.1 bar), puts
> `canonical.fedGrowthPerYear` below its band, and extinguishes the dev world (93,910 → 38 over 1000
> turns). No band, constant or derivation was moved; six tests are left RED and named in CR-016 §5.
> Suite on the packet tree, MEASURED in Release: Sim.Tests **867 passed / 6 failed / 4 skipped**
> (the 6 skips became 4: `MigrationTests.cs:378` and `:532` were LIFTED under CR-015 N9),
> Sim.Ui.Tests **294/294**, and all three gate scripts green
> (`check-banned-constructs.sh`, `check-read-isolation.sh`, `check-readonly-proof.sh`). The ONE
> inherited red on `8f7f9da` (`Dev_MalthusCorridors_AllInBand(seed: 7)`, the migration drift tooth)
> is RESOLVED — its envelope was re-pinned to the measured value — but both seeds of that theory are
> now red for the CR-003 Malthus teeth instead. Goldens moved for the arming alone, proved
> bit-exactly: every layout control returns its constant byte for byte on the λ = 0 twin.
> Verify against git.
>
> T4.21-1 merged to the integration branch at `05b23e6` (Release suite, measured by the fix lane on
> the fix tree: Sim.Tests 819 passed / 2 failed / 6 skipped in 5 m 36 s, Sim.Ui.Tests 294/294 — the
> SAME two `Dev_MalthusCorridors_AllInBand(seed: 42)` and `(seed: 7)` fail on `45046eb` in the fix
> lane's own worktree with the byte-identical message "3 starvation deaths": INHERITED from the M4
> playtest build, not caused by the packet; CI on this branch is red until T4.21-4 restores the `Cr003Quarantine` guards and re-reads the
> `Dev_MalthusCorridors` message). T4.21-1 fix lane (verifier's findings): the measured foundations
> audit `docs/t4.21-1-foundations-audit.md` (per-settlement `ρ` for seed 42 ± the director's orders and
> seeds 1–3, F4/F5), the measured mutant kill-record `docs/t4.21-1-mutants.md`, and the M-FS-ABANDON
> correction in ADR-024 §8 / FoodState.cs (the Share variant is equivalent).
>
> **2026-09-18 — T4.21-2 ∥ T4.21-3 MERGED AND FINISHED on `claude/civdemo-work-b1z2y4`.** The merge
> commit `a621c86` (T4.21-2 bounded migration, ADR-025 + T4.21-3 shock integration, CR-015/ADR-026)
> was left unfinished: a `-999` placeholder in `MerchantTests`, every golden and measured pin resolved
> to the T4.21-3 side (both parents measured against `1735d41` with the other absent, so neither
> value can hold on a tree carrying both), and a conflict of fact — T4.21-3 asserted migrants move on
> turn 2, T4.21-2's vacancy bound refuses every gap flow that turn. The merge-finish lane resolved all
> three BY MEASUREMENT on the merged tree, changing NO mechanism (the diff against `a621c86` touches
> `Sim.Tests`, `Sim.Ui.Tests`, `ci.yml`, `docs/queue.md` only): T4.21-2's form is the measured truth —
> `MigrantsMoved` 0 on turn 2, 252 on turn 3, founded and driven. Four world goldens, ci.yml's
> `FOUNDED_GOLDEN` (reproduced with two separate CLI processes, byte-identical), every
> `IntegratedPinAttribution` strip and every measured-value pin re-measured; the synthetic
> `GoldenHash_Seed42Turn200` and both its strips are UNMOVED, which is the no-unrelated-movement
> control. Suite measured on the finished tree (Release): Sim.Tests **858 passed / 1 failed / 6
> skipped** in 6 m 31 s, Sim.Ui.Tests **294/294**, three gate scripts green. The ONE red is
> `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7)` — the dev migration quarantine's
> drift tooth, red on BOTH parent branches for the same pre-existing, previously masked cause
> (measured here: seed 7 `dev.migrationGrossPerDecade` 1.0200612834541973E-04 against a recorded
> 0.000799951; seed 42 8.336943780925534E-05 against 7.21744E-05, which passes). Its re-pin is
> T4.21-4's deliverable; the `corridors.json` bands and the CR-003 quarantine were NOT touched.
> The two open T4.21-2 findings (the turn-2 vacancy refusal, the fed-world basin-cap magnitude) stay
> open in `docs/queue.md` — this lane measured and recorded, it did not act on them. Verify against git.
>
> **2026-09-18 — T4.21-4 RULE APPLICATION on `claude/civdemo-work-b1z2y4`.** The orchestrator's two
> pre-registered rules were applied to the two findings above. **RULE 1: the source basin (fan-out)
> cap STAYS as shipped** — measured on the canonical founded world (seed 42, no orders), gross
> migration per decade over turns 2..300 is 0.001021 on the merged tree against 0.001509 pre-packet
> (`1735d41`), i.e. 0.677x, clearing the 0.4x threshold (0.000604) and inside the corridor band
> [0.001, 0.01]; both conditions hold, so no mechanism changed and no test or mutant was removed.
> **RULE 2: `FoodHeadroom.Limit`'s null arm now includes catchment ROW ABSENCE** (ADR-026 §2.1a),
> keyed on row absence only — never on `production == 0`, which is the abandoned settlement's genuine
> zero — with `H_NullArm_NoCatchmentRow_IsPositiveInfinity_ButAbandonedWithARowIsZero` pinning both
> arms. **It does NOT close its queue line, and it moves no golden:** measured bit-exact on the
> canonical founded world (300-turn hash `db7c7a09…` before and after — the ci.yml `FOUNDED_GOLDEN`,
> reproduced via the ci step's own `sim run --founded --seed 42 --turns 300 --hash-log`; first
> migration turn 3 both ways) and byte-identical over every turn of the driven world. Catchment is
> pipeline entry 1 and writes into NEXT, so the turn-1 world — PREV on turn 2 — already carries a row
> for every founded settlement; the turn-2 refusal comes from that row being PRESENT alongside the
> turn-1 zero staple harvest, which the rule forbids keying on. No substitute key was designed.
> Suite on this tree (Release, sequential): Sim.Tests **859 passed / 1 failed / 6 skipped** in 5 m 33 s,
> Sim.Ui.Tests **294/294**, three gate scripts green, `dotnet build -c Release` 0 warnings 0 errors.
> The +1 pass over the merge-finish tree is the new null-arm test; the ONE red is still
> `Dev_MalthusCorridors_AllInBand(seed: 7)` at the identical value (0.000102006), untouched here —
> `corridors.json` and the CR-003 quarantine were NOT re-pinned. Where the residual pre-packet →
> merged migration cut sits (vacancy bound, bounded flight, exit-openness rewrite) is ESCALATED to the
> director per RULE 1's own wording. Records: ADR-025 §2.3a/§2.4a, ADR-026 §2.1a,
> `docs/t4.21-2-record.md` §9, `docs/queue.md`. Verify against git.

> **2026-09-18 — T4.21-7 THE DISARM-AND-SETTLE LANE, on `claude/civdemo-work-b1z2y4` (worked
> directly on the integration branch, from `ee27c17`). THE FAMINE-CLASS DISASTER SHIPS COMPLETE AND
> TESTED BUT INERT.** `sim.json disaster.hazardPerYear` **0.01 → 0.0**, the exact inverse of
> T4.21-4's arming. This implements the orchestrator's decision on
> `docs/adr/cr-016-armed-disaster-fallout.md` — recorded there in full under "ORCHESTRATOR DECISION"
> — that the MECHANISM ships and the RATE becomes the director's ruling, because a world that dies
> by construction plus a broken PERMANENT dt-invariance detonator is a worse pathology than the one
> T4.21 fixed, and because choosing a lower rate to make the world survive would be the
> tuning-to-outcome CR-015 §6.5 forbids. `durationYears`, `severityMin/Max`, the §3.3 derivation and
> `corridors.json` are UNTOUCHED — the band is not what CR-016 disputes. **CR-016 STAYS OPEN: what
> is settled is the TREE, not the question.**
>
> **Suite, MEASURED by this lane on this tree** (Debug, sequential, `dotnet test`):
> Sim.Tests **884 passed / 0 failed / 4 skipped** (30 m 31 s), Sim.Ui.Tests **296 / 0 / 0** (1 m 35 s),
> and all three gate scripts exit 0 (`check-banned-constructs.sh`, `check-read-isolation.sh`,
> `check-readonly-proof.sh`). **THE RED SET IS EMPTY.** All six reds T4.21-4 escalated (both
> canonical fed-corridor seeds, both CR-001 dt-continuity detonators, both dev Malthus seeds) are
> RESOLVED by the disarming, which is itself evidence that the arming was their sole cause — none of
> them needed chasing. The ONE red inherited from `8f7f9da` (`Dev_MalthusCorridors_AllInBand(7)`,
> the migration drift tooth) is resolved deliberately, like a golden, by re-pinning the recorded
> envelope to the measured λ = 0 values. The 4 skips are the four manual measurement rigs
> (`FoundingVariationItem0Tests`, `WaterRouteCounterfactualTests` ×2, `HousingBeforeColumnTests`);
> CR-015 N9's two lifts are NOT re-skipped.
>
> **All four world goldens and `ci.yml`'s `FOUNDED_GOLDEN` returned to their pre-arming constants
> BYTE FOR BYTE** (`db7c7a09…`, `98ee3a7a…`; the founded one derived twice — in-test harness and the
> Release CLI reproducing the ci step). That round trip is a stronger attribution than the λ = 0
> twin controls were: everything merged since `8f7f9da` (T4.21-5's observability, the chain-link
> merge fix, T4.21-6's eight finding fixes) is now MEASURED to move no world golden. Every measured
> pin returned too — artisan latch 70 / 13, merchant latch 119, founded/driven populations
> 40,539 / 6,373 at turn 300, first trade 28 / 7, and no pin failed to return.
>
> **What the disarming COSTS is recorded, not hidden.** Two `Cr003Quarantine` guards go back to
> quarantined (the dev world starves nobody and writes no famine chronicle line at λ = 0 — measured),
> `WorldReconciliationTests`' founded-starvation and driven-dwelling-decay coverage returns to
> ABSENCE pins, and `MigrationTests.MagnitudeCorridor_FedPhaseDrift_WithTeeth`'s UPWARD rate-lever
> tooth is quarantined in place (×1.0082 at λ = 0 against ×1.88 armed — below ADR-018 §11's own dead
> signature of ×1.07); its corridor assertion and both downward teeth stay live and it is NOT
> re-skipped. Each site carries BOTH readings and names CR-016 as what decides it, and every one
> reverses with the same single data edit. `docs/queue.md` carries them as open items.
>
> **The MECHANISM is still proven, by tests that arm λ in their own rigs, never by re-arming the
> shipped value**: a disaster CAN cause famine (forced-strike rigs and the λ = 0.01 in-rig battery
> arms), an ordinary bad harvest CANNOT, deliberate abandonment CAN on the SHIPPED config (mandate
> item 1(B), reachable in play today), λ = 0 gives zero famine while still giving STRESS, and the
> three determinism legs each assert a disaster actually fired. One claim was found exercised only
> by a dead test — the forensic disaster cross-check — and was given its own hazard.
>
> Records: `docs/adr/cr-016-armed-disaster-fallout.md` (decision, options, what the director is
> asked to rule), ADR-024 §11 (the disarmed re-measurement), CR-015's T4.21-7 append-only block,
> `docs/t4.21-4-record.md` (RETAINED as CR-016's evidence, now headed as measured AT λ = 0.01),
> `docs/t4.21-architecture.md` §4 row T4.21-4, `docs/queue.md`. Verify against git.

**Read this first, then verify it.** This file exists so an agent entering the repository with zero
conversation context can work out what to read next. It is a ROUTER and a STATUS BOARD. It is not
the Spine, not a milestone spec, not a D-decision, not an ADR, and it never restates one — where a
fact belongs to another document, this file names the document and stops.

**It is also not evidence.** Every claim below carries its provenance. Before you rely on any of
them, re-derive it from git or from the named document (§9). A value that could not be verified from
the repository is written `UNVERIFIED` with the check that would settle it.

**Measured 2026-08-31** in worktree `wt-food` against `food-anomaly-observability` at `8bd433a`,
with `origin/main` at `070f05b` as fetched. Every git figure below was read from `git rev-list`,
`git branch` or `git log` at that moment, not recalled from a session.

---

## 1. WHERE THE PROJECT IS

| | |
| --- | --- |
| **Current milestone** | **M4** — see §2 for why this contradicts `CLAUDE.md` |
| **Current objective** | M4 "Empire Control Foundation" — the structural minimum for the ratified Empire model |
| **Governing architecture** | `docs/d042-empire-and-player-control-addendum.md` (D-042) |
| **Milestone spec** | `docs/m4-spec.md` (R-1, R-2, R-3 ruled 2026-08-07; packet list FINAL) |
| **Authoritative baseline** | `CORRECTED` — `origin/main` = `dbef61a` (M4 completion merge, schema v24). As written: `070f05b` (T4.4 colonization, schema v22) |
| **Active implementation branch** | `m4-empire-control-foundation`, rebased onto `origin/main` — see §3 |
| **Integration state** | T4.4 v22 + M4 v23 + capacity-floor fix + D-042 + GOV-4; four goldens re-derived and causally attributed |
| **Certification** | `CORRECTED` — M4-A, M4-B, M4-C and M4-D are ALL MERGED; `origin/main`'s tip is the M4 completion merge. As written: B/C/D certified but unmerged |
| **Schema version** | **v24** (unchanged at the M4 exit gate) — v22 T4.4's `BucketRow`; v23 M4-A's Polities/Capitals; v24 M4-D's ConstructionQueue/Structures |

**Documents required before touching current work:** `CLAUDE.md` · `docs/m4-spec.md` ·
`docs/d042-empire-and-player-control-addendum.md` · `docs/spine-s8-governance-freeze.md` ·
`docs/civ-sim-architecture-v3-outline.md` · `docs/adr/adr-015-verification-hygiene.md` ·
`docs/gov-3-execution-protocol.md` · `docs/gov-4-repository-freshness.md` · `docs/queue.md`.

---

## 2. THE MILESTONE CONTRADICTION, STATED RATHER THAN RESOLVED

`CLAUDE.md:10` reads **"Current milestone: M3 — active packets: `docs/m3-spec.md` §4"**, and adds a
director amendment about the unmerged art-substrate branch.

The repository disagrees with that line. `docs/m4-spec.md` exists on `origin/main` and declares its
packet list FINAL under a dated director ruling; the merged history on `origin/main` carries T4.3,
T4.4, T4.8, T4.14 and T4.16 merges described as director-certified; and the branch list is dominated
by `t4.*` packet branches.

**The line has not been changed.** It is the director's — `CLAUDE.md` itself says it "changes only
at a milestone exit gate", and no exit-gate ruling for M3→M4 was found in the tree
(`UNVERIFIED`: an explicit M3 exit-gate record would settle it; searching `docs/` for one returns
`docs/m3-exit-session.md`, which records the session but was not confirmed to be the gate ruling).
Treat M4 as current per §5's hierarchy — the later milestone spec and the implementation both
outrank an older line in `CLAUDE.md` — and treat the `CLAUDE.md` line as a known stale entry
awaiting a director edit.

---

## 3. BRANCH AND PUBLICATION STATE — LOCAL, REMOTE AND MAIN ARE DIFFERENT THINGS

Three facts here are load-bearing, and collapsing any of them into "the repository has it" will
mislead the next agent.

1. **Local `main` is stale.** Local `main` = `87fb866`, `origin/main` = `070f05b`: **0 ahead, 8
   behind**. The 8 are the T4.4 colonization merge and the PR#4 AI-constitution admission.
2. **The work is now rebased onto `origin/main`, on a new branch.** `m4-empire-control-foundation`
   carries T4.4 + the whole M4 body and is **0 behind `origin/main`**. The pre-rebase history is
   preserved untouched at `origin/food-anomaly-observability` = `358b9a9`, so no force-push was ever
   needed and the old provenance is recoverable. REMOTE is true for both; **MAIN is still false**.
3. **Local `main` is still stale and was deliberately not moved.** It is checked out in other
   worktrees, so fast-forwarding it would disturb them. `origin/main` is the reference to use.

Anything on that branch is therefore **LOCAL**, not **REMOTE**, and not **MAIN**. Say which one you
mean, every time.

The full branch classification (64 remote branches: 49 MERGED, 15 unmerged) is recorded in the
session report for this packet rather than duplicated here, because it goes stale the moment anyone
pushes. **Re-derive it** with `git branch -r --no-merged origin/main` and the ahead/behind counts —
that command is the record, this file is not.

Two branch facts worth carrying anyway, because they are easy to miss:

- **`adr-019-architecture-addendum`** (`a36b94d`, 1 ahead / 63 behind) holds the **only copy of
  ADR-019**, which is absent from `origin/main`. Do not treat the ADR sequence on main as complete.
- **`claude/civdemo-work-b1z2y4`** exists but is 0 ahead / 63 behind and unrelated to current work.

---

## 4. STATUS OF OPEN THREADS

**M4-D — THE SETTLEMENT CONSTRUCTION QUEUE. CERTIFIED.**

A settlement holds an ordered construction queue; only its head is eligible, and that head is either
built whole this turn or waits unchanged. No build timer, no fractional progress, no construction
currency, no second labour pool. Capacity is the existing construction sector in adult-years less
housing's published draw, so builders genuinely compete with farmers. Materials leave settlement
good stocks all-or-nothing under `ReasonIds.ConstructionMaterials`. An Empire may only build where
it rules — the first production caller of `EmpireQuery.ControlsSettlement`.

A construction project is deliberately NOT a `RecipeEntry`: a recipe must output a good, and a
structure is not one. `ConstructionProjectEntry` is the smallest parallel definition.

Schema v23 → **v24** adds `ConstructionQueue` and `Structures`, empty in every canonical world, so
all four pinned worlds moved for two zero count prefixes alone. Audited by the generalised
attribution control before any repin, then repinned on director approval.

Certification run: `Sim.Tests` **572 passed / 6 failed / 6 skipped** (584, 24m07), `Sim.Ui.Tests`
**151/151**, build 0 warnings, all three gates OK. The 6 are the unchanged mainline quarantine.

**M4-C — FOUNDING INSTANTIATES THE EMPIRE. CERTIFIED.**

`WorldFounding.Found` now creates the founded world's one player-commanded Empire in the same
operation that creates its settlements: `PolityRow(PolityId 1, CommandSource.Player)`, one
`ControlRow` per founded settlement, one `CapitalRow` on the first. So `Found` never returns a
playable world whose settlements answer to nobody. Identity remains the existing D-037 `PolityId`;
`SettlementRow` carries no ownership field and no Empire container exists.

**The id is 1 by director ruling (CR-011), not 0** — the convention governing an order's actor is
the order corpus, which has stamped `ActorId = 1` since M1 including both binary replay fixtures.
Those fixtures were preserved unmodified and now resolve naturally.

**Three founded-world goldens moved, each audited before repin.** `IntegratedPinAttributionTests`
separates M4-A's schema LAYOUT from M4-C's founding CONTENT: emptying Polities/Controls/Capitals
returns each world's pre-M4-C pin byte for byte, so the Empire rows are the whole delta and no
simulation state moved with them. `GoldenHash_Seed42Turn200` did NOT move — the
no-unrelated-movement control holding.

Certification run: `Sim.Tests` **559 passed / 6 failed / 6 skipped** (571, 19m06), `Sim.Ui.Tests`
**151/151**, build 0 warnings, all three gates OK. The 6 are the unchanged mainline quarantine.

**M4-B — STRATEGIC ACTOR IN THE ORDER SEAM. CERTIFIED at `7d1734d`.**

An order's issuing strategic actor is the **existing `PolityId`**. The kernel spec §3.9 already
defined the log as `{turn, actorId, orderPayload}` for "player/AI orders", so `ActorId` was always
the strategic actor — it merely had no type and no enforcement. `PolityId` is a one-`int` identity,
so the binding required **no new field, no second identity, and no serialization change**:
`OrderRecord.Actor` is a projection with `Actor.Value == ActorId` always. `PolityId` is the typed
in-memory form; `ActorId` is the serialized form; they are one identity.

Actor existence is enforced in `OrderValidation` (the world-dependent layer), guarded on a non-empty
roster — **dormant today**, because nothing seeds `Polities`. Player/AI symmetry is preserved: the
only production references to `CommandSource` are the state definition, the serializer, the
`EmpireQuery` readers, and an existence probe in validation that discards the value. **No simulation
system branches on it.** No golden moved.

Certification run at `7d1734d` on a verified-clean tree: `Sim.Tests` **553 passed / 6 failed /
6 skipped** (565, 26m57), `Sim.Ui.Tests` **151/151**, build 0 warnings, all three gates OK. The 6
failures are the unchanged mainline quarantine set.

**CERTIFICATION RUN, measured at `5ebc1e3` — the tip this report certifies.**
`dotnet test Sim.Tests` → **541 passed / 6 failed / 6 skipped** (553 total, 16m45).
`dotnet test Sim.Ui.Tests` → **151 passed / 0 failed**. Build: 0 warnings, 0 errors. All three
repository gates OK.

The 6 failures are exactly the mainline quarantine set, unchanged from the pre-integration run and
identical test-for-test: four `CalibrationBatteryTests` (`Canonical_FedCorridors` seeds 1 and 2,
`Dev_MalthusCorridors` seeds 7 and 42) and two `ClassSystemTests` (`Artisans_EmergeInFedAutoplay`,
`Famine_DrainsArtisansBeforePeasantStarvationPeaks`). **No new deterministic failure appeared.**
`ProductionPipeline_PerPhaseBench_Reported` passed; it is load-sensitive wall-clock and is NOT part
of the deterministic quarantine.

**Food-anomaly certification: PAUSED, NOT COMPLETE.** The investigation concluded the reported
symptom is not reproducible (0/40 seeds) and that the real defect was a granary capacity floor. The
director ruled ACCEPT on the capacity-floor fix and approved the resulting `DrivenGolden` repin
(`24d107f`). The final full-suite regression run before merge was interrupted and **has not been
re-run**. The fix and its 7-test regression suite are on the active branch; **nothing is merged.**

**M4 Empire Control Foundation: implemented, blocked at the golden boundary.** `8bd433a` adds
`PolityRow`, `CapitalRow`, `CommandSource` and `EmpireQuery`, and moves `CanonicalSchema` to v22.
Two new empty count prefixes move all four pinned world hashes. That is a schema-only change, not a
behavioural one, so **the goldens were deliberately not repinned** — the pin change needs a director
ruling, on the T4.3/T4.8 precedent.

**The schema-version collision is RESOLVED by director ruling.** T4.4's v22 (`BucketRow` gaining
`UnplacedDeparture` and `UnplacedRemainder`) is authoritative because it merged to `origin/main`
first and is certified; M4's Polities and Capitals are **v23**. Exactly one meaning of every version
number survives, and T4.4's representation was not altered or reordered.

**All three moved goldens are re-pinned on the integrated tree, with the cause MEASURED.**
`IntegratedPinAttributionTests` strips the two empty v23 count prefixes — the tables' entire
contribution, since nothing writes them — and re-hashes. `GoldenHash_Seed42Turn200` and
`FoundedGolden_Seed42Turn300` return **main's exact pins** under that control, so both moved for the
M4 schema and nothing else. `DrivenGolden_Seed42Turn300` does not, which isolates the capacity-floor
fix as its second, behavioural cause. `ci.yml`'s `FOUNDED_GOLDEN` moved with its test.

**Open change requests awaiting director ruling:** CR-005 (M5 ownership of Research/Technology/
Institutions) · CR-006 (temporal control and epoch) · CR-007 (B3 exemplar — headline withdrawn by
the author; the record stands) · CR-008 (money owner) · CR-009 (era gates) · CR-010 (institution
definition) · D-042 §14.1 (D-018 income-column staleness).

**Quarantined / manually-run tests:** six `Skip =` entries in `Sim.Tests`. Two are quarantined
pending M4 migration work (T4.1b/ADR-018 §11 — asserts on gross migration, the wrong observable);
four are expensive measurement rigs kept off the default path by design, each naming the review
record that holds its numbers. The historical food-investigation harnesses were removed from the
default test path in `ca0aef0` and must not be re-run casually — see `docs/gov-4-repository-freshness.md` §9.

---

## 5. SOURCE-OF-TRUTH HIERARCHY

When two sources disagree, the higher one wins. **Do not silently reconcile them** — name both
sources, name which is authoritative, and say whether the conflict needs a director ruling.

1. Explicit director rulings in the current task or session
2. Later accepted D-numbered decisions and architecture addenda
3. Accepted ADRs
4. The current milestone specification
5. The current repository implementation
6. Tests and certification records — for behavioural truth specifically
7. Older architecture documents
8. Previous Claude/agent reports
9. Conversation memory
10. Agent inference

Note what this ordering does to a common mistake: a previous agent's report (8) loses to the code
(5), and agent inference (10) loses to everything. §2 above is an application of the rule, not an
exception to it.

---

## 6. KNOWN STALE AND FROZEN DOCUMENTS

**Stale — describes a state the repository has moved past:**

- `CLAUDE.md:10` — the M3 milestone line and its art-substrate amendment (§2).
- `docs/handoff-status.md` — a previous, measured attempt at this same routing role, dated
  2026-07-28 against `t3.4c-variance-fix` at `719152a`. Its branch table, suite counts and milestone
  are all from M3. **It is superseded by this file for routing purposes and left untouched as a
  historical measurement.**

**Frozen — stale in places but deliberately not rewritten:** the Spine
(`docs/civ-sim-architecture-v3-outline.md`), `docs/spine-s8-governance-freeze.md`, closed
D-decisions, accepted ADRs, and every investigation and review record. A later decision superseding
part of one of these does **not** license editing it. If a frozen document creates a real ambiguity
for current work, record the ambiguity (queue entry or CR) — do not resolve it by editing.

---

## 7. QUEUED AND NEXT

`docs/queue.md` is the queue; it is not summarized here. The immediate decisions the tree is waiting
on are the CRs in §4 and the golden-repin ruling for `8bd433a`.

---

## 8. UPDATE RULE

Update this file **after an accepted implementation packet**, and only with what the repository can
show: the new accepted commit, the new current task, newly accepted decisions, newly resolved
blockers, newly discovered stale or conflicting documents, and the next queued packet.

Never update it speculatively, and never to record an intention. It represents repository truth, and
a forecast written here becomes next session's false premise.

---

## 9. PROVENANCE OF EVERYTHING ABOVE

Read from git in worktree `wt-food` on 2026-08-31: branch tips, ahead/behind counts, merged/unmerged
partitions, the local-vs-`origin/main` divergence, and commit subjects. Read from the working tree:
`CLAUDE.md:10`, `docs/m4-spec.md`, `docs/handoff-status.md`, `docs/queue.md`, the `Skip =`
attributes in `Sim.Tests`, and the presence or absence of documents on `origin/main`.

Carried from the session that produced this file, and therefore **secondary evidence** under §5:
the director's ACCEPT ruling on the capacity-floor fix, the paused state of its certification, and
the list of open CRs. Each is verifiable — the CRs as files under `docs/adr/`, the ruling and pause
from the session record — but none was re-derived from git for this file.

`UNVERIFIED` values are marked inline. There is one: the M3→M4 exit-gate ruling (§2).
