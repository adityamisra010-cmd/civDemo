# ADR-022 — The reference platform is Linux x64

**Status: ACCEPTED** (director ruling on CR-013, T4.19-B: options 1 + 3 accepted, option 2
rejected for now and reopenable). This ADR carries the scope statement; no frozen spec file is
edited. No simulation expression, constant, config, golden, corridor, band or quarantine moved.

**ADR number:** 022. `adr-021` is the highest on `main`; 019 is held by an unmerged branch
(`a36b94d`) and is skipped rather than reused, as ADR-021 did.

## Context — what CR-013 measured

CLAUDE.md law 5 and the kernel contract promise that a world is a pure function of its seed and
its order log. Nothing in the repository said on which machine. CR-013 §2 found, on the director's
first real played session, that the same seed and the same `UiSession` code path produce
different world hashes on Windows x64 and Linux x64 from turn 2, while population, food and
settlement count agree to the unit on every turn measured (0–5).

CR-013 §8 then measured the cause rather than inferring it, on the runners themselves (run
`34419607514`, commit `d8630e7`; Windows RID `win-x64`, Linux RID `linux-x64`, both SDK 10.0.401):

| turn | first divergent field | ulp | other tables |
| --- | --- | --- | --- |
| 1 | none — 40 blocks, 106,889 bytes identical | | |
| 2 | `PriceTerms[43].Consumption` | −2 | `HarvestWeather` 2 of 12 rows |
| 3 | `GoodStocks[154].ProduceRemainder` | 8,192 | `HarvestWeather` 1 row |

Every `long` column and every other double column compared equal on all three turns. The two
tables that differ at turn 2 are exactly the two whose values pass through library
transcendentals (`Math.Exp`/`Math.Sqrt` in `HarvestWeatherSystem`, `Math.Exp` in `PriceSystem`):
a last-ulp `libm` disagreement, not FMA. The Windows runner's turn-2 hash equals the director's
own trace value, so the runner reproduces his machine. Determinism WITHIN a platform was
re-verified on both sides: live `UiSession` vs headless replay agree hash-for-hash on Linux
(§2), and the Windows runner reproduced the Windows trace (§8.4).

## Decision

### 1. The determinism promise is scoped to the reference platform: Linux x64

The canonical deterministic artifact — every golden pin (`SnapshotTests`, `DrivenGoldenTests`,
`FirstReign`, ci.yml's `FOUNDED_GOLDEN`), replay evidence, the corridor battery, and any hash
cited as a finding — is **defined on Linux x64**. Law 5's "same seed, same world" is a statement
about that platform. It is not weakened there: two Linux x64 processes, a container and a CI
runner, a live session and its replay, must still agree byte for byte, and a divergence among
them remains a determinism failure that fails CI.

The reference is identified by the .NET runtime identifier, and the accepted spellings are the
two that have been **measured** to produce the reference hashes, no more:

- `linux-x64` — the portable RID Microsoft's build reports; the CI runner (run `34419607514`).
- `ubuntu.24.04-x64` — the distro-qualified RID the Ubuntu-archive SDK reports; the
  remote-session container (`ubuntu.24.04-x64`, SDK 10.0.110), whose turn-1/2/3 saves equal the
  runner's hash for hash (CR-013 §8.2 vs §8.4).

`SessionManifest.ReferencePlatform` holds the constant and `SessionManifest.IsReferencePlatform`
the rule, in Sim.Core, as pure string logic — Sim.Core reads no runtime information. Other glibc
distributions that the RID graph would fold into `linux-x64` are deliberately NOT accepted until
someone measures them: the divergence lives in `libm`, about which the RID graph says nothing. The
set grows by adding a RID with the run that produced the reference hashes.

### 2. What a Windows build is for

Windows x64 is a **supported execution platform under determinism surveillance**, not a wrong
one. A Windows build is for **play**, and for **session records that reproduce on that
machine**: the director's trace was reproduced exactly by the Windows runner (CR-013 §8.4), so a
Windows session inspected on Windows is held to the full within-platform standard — a hash
mismatch there is a finding. What a Windows session is not is evidence against a Linux pin: its
hashes are that platform's, and against a reference replay they diverge from the first turn a
library transcendental runs (turn 2 on the measured seed).

The integer columns are the part that carries across. Population, food and settlement count
agreed to the unit on every turn measured (CR-013 §2, turns 0–5); the trajectory, the population
transient and the famine crises the director saw on Windows are reproduced on Linux to the unit
(CR-013 §3). That equality is measured, not guaranteed: by turn 3 the weather multiplier's ulp
difference has been banked in a `ProduceRemainder` that decides a later integer floor, so a
`long` divergence at some later turn is expected, and its turn is one of the things the
surveillance report exists to record.

### 3. How cross-platform divergence is surfaced

Three places, none of which can be silent:

- **The session manifest records the platform that played it** (`session-manifest/v2`,
  `platform` = `RuntimeInformation.RuntimeIdentifier`, supplied by Sim.Ui where interrogating
  the runtime is legal under ADR-009). v1 manifests remain readable and report
  `not recorded (pre-v2 session)`; nothing is guessed for them.
- **`sim inspect` says which comparison it is making before it prints the verdict.** A pure
  function, `PlatformNotice.For(manifestPlatform, runningPlatform)`, prints a plain
  "reference platform on both sides" line when it is, and otherwise a NOTICE: that a hash
  comparison against a reference replay is expected to diverge from turn 2 (CR-013 §8), that
  the population/food/settlement columns remain comparable (measured to the unit, §2), and that
  reproduction is judged on the machine that played the session. A second NOTICE fires when
  `sim inspect` itself runs off the reference. The four cases and the two edge cases (same
  non-reference platform on both sides; pre-v2 manifest) are pinned by `PlatformNoticeTests`.
- **`.github/workflows/xplat-surveillance.yml`** (renamed from lane E's dispatch-only
  `xplat-diagnostic.yml`) is a **permanent standing report**: on every push to `main` and to
  `t[0-9]*.*` branches, weekly, and on dispatch, a Windows runner runs the founded golden with
  the exact command ci.yml's `determinism-xproc` pins (`sim run --founded --seed 42 --turns 300
  --hash-log`), compares its final hash to `FOUNDED_GOLDEN` (read from ci.yml, never copied —
  `CiPinAgreementTests` guards the existing two copies), and writes **MATCH** or
  **DIVERGED-AT-TURN-N** — the first differing turn against the Linux runner's hash log, both
  logs uploaded as artifacts — into the log and `$GITHUB_STEP_SUMMARY`, with a warning
  annotation on divergence. The job carries `continue-on-error: true` and its summary states
  that it is surveillance per this ADR: visible, never blocking. A **Linux** divergence from
  `FOUNDED_GOLDEN` in the same workflow exits 1, because that is a reference-platform failure.
  The turn-1/2/3 snapshot jobs and the runner-side `sim diff` stay as the diagnostic detail, so
  a DIVERGED verdict comes with the first divergent table/row/field bit-exact.

### 4. Option 2 is rejected for now, and reopenable

A correctly-rounded `Exp`/`Sqrt` in Sim.Core (exact-form or table-driven) would make the
promise cross-platform at the cost of moving every golden once and adding an unmeasured
per-call cost to two hot systems. It is **not** done in this packet and no simulation expression
is touched. CR-013 stays reopenable. What would reopen it, each of which the surveillance
report is built to show at the commit that causes it:

- the first differing turn on the founded golden **moves earlier** than the measured turn 2, or
  a table outside `HarvestWeather`/`PriceTerms`/their downstream remainders diverges at turn 2 —
  a new divergence source, not the measured one;
- a Windows/Linux disagreement in an **integer column** of the session trace within the horizon
  a director actually plays, so that a Windows playtest stops being comparable to the unit;
- a need to **pool** cross-machine playtest evidence at the hash level (CR-013 §3, third bullet)
  that the integer columns cannot serve;
- a runtime or `libm` change on the reference runner that moves a pin with no simulation
  change, which would show that "reference platform" needs a runtime version in it as well as
  an OS and an architecture (the linux job in the surveillance workflow exits 1 on exactly
  that, and records the SDK version and RID beside the hash).

## Consequences

- No golden moves. Measured on this lane: `SnapshotTests` (synthetic, founded), `FirstReign`,
  `CiPinAgreementTests`, `ReplayTests` and `SessionRecordTests` pass unchanged; the two
  `DrivenGoldenTests` throw `LedgerOverdrawException` on this base exactly as before the lane,
  which is CR-014 (lane A's) and is unrelated to this ADR — the same two failures, with the same
  exception at the same site, on the untouched base.
- `SessionManifest` is a manifest contract, not `CanonicalSchema`: `CanonicalSchema.Version` is
  untouched, no world hash changes, and no populated-table schema test is affected.
- The kernel contract is not edited. This ADR is the place the scope lives; the M0 spec's law
  reads as before and is to be read together with this document.
- Anyone reporting a hash as evidence names the platform. `sim inspect` does it for them.
