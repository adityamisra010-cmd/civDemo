# T4.21 EVIDENCE — the Libur cascade counterfactuals (CR-015 §2)

**These files are EVIDENCE, not code.** Nothing in this directory is built, run, referenced by a
project file, or loaded by any test. They are committed because GOV-4 (`docs/gov-4-repository-
freshness.md`) makes a previous agent's report SECONDARY evidence that must be verifiable against the
tree, and because `docs/adr/cr-015-famine-is-exceptional.md` §2 rests on numbers that were otherwise
only in a session scratchpad (`docs/t4.21-architecture.md` §5.2, §9 R16). The patch is committed
as a text file so the instrumentation that produced the numbers is inspectable; it is NOT applied
to the tree and must not be — it is a diagnostic overlay on the observer, superseded by T4.21-5's
observability work.

## Provenance

| | |
| --- | --- |
| tool build | commit `45046eb` (= `main` `dbef61a` + the unmerged M4 playtest build `2807155` + the forensic CLI P0/P1/P3), `Sim.Core`/`Sim.Data` byte-identical to the candidate `2807155` |
| instrumentation | `diag-telemetry.patch` (read-only additions to `Sim.Core/Observability/Observer.cs` and the telemetry writer; 111 lines) applied on top of `45046eb` |
| platform | Linux x86-64 (the ADR-022 reference platform) |
| world | seed 42, founded world, 12 settlements, dt = 10 y, 161 turns, the director's 15 playtest orders replayed |
| run id | `02ee1038911d8d53` |
| state identity | the instrumented replay reaches the same final hash as the pristine tool build: `e234616b0a38dfa89a7894333fc04067f97496984fe0504f79ec8a57e139b4e9` at turn 161 (`hash.log`, 161 lines, sha256 `c133756d11bdf7c94484796d504f052fcb75a93c5575de8acfa9700e1aa5b788`); `cascade-report.md` §1.1 records the `cmp` against the pristine hash log exiting 0 — the instrumentation changes no state |
| written | 2026-09-17 (`predictions.md` pre-registered BEFORE any counterfactual result existed — its mtime precedes every patch and run, `cascade-report.md` §3) |

## Files

| file | what it is | sha256 (as committed) |
| --- | --- | --- |
| `cascade-report.md` | the diagnostic investigation report (Step 13): baseline, the four counterfactual arms M1–M4 (+ M2b, M3b), prediction-vs-reality scoring, the cascade diagnosis, the second finding on weather variance. The loci CR-015 cites: §1.2 (Libur t118 809 → 69; t117 deficit 0.297207; outflow 641/809; inflow 1213/69), §1.4 (the Libur t110–t125 series with Σ damping·viability = 4.35–4.43), §3 A (the M3 arm: corrected-σ deficit 0.0786), §3 D/§4 Branch D (the residual M4-arm trough 321 = 49 pp deaths + 39 pp missing births), §3 G (D_Libur = 4.384 — the destination count as a magnitude multiplier) | `3a3792aa24925e1c20dc56e59a6ed53394a3ce1a11e146263235e8d44307dfb3` |
| `predictions.md` | the pre-registered IF/THEN/ELSE predictions for every arm, written against `45046eb` before any counterfactual ran | `135bd233f8dc50ce830331ce85ab910daf9f9534b2482f4719f17bc212a49e06` |
| `diag-telemetry.patch` | the read-only instrumentation patch (observer-side: damping, viability, destination sums, per-cohort prev buckets) that produced the extra telemetry columns; state-neutral by the hash equality above | `aca2c75f863d3ccfab8afa4c96b8cdfcd2747ebc6781cffa1ad78f5fdf05ed5d` |
| `base-metrics.json` | the BASE arm's metrics (`cascade_metrics.py` output over `telemetry-02ee1038911d8d53.jsonl`): extremes, distributions, per-settlement-turn summaries — the numbers every counterfactual is judged against | `5ca458d7d33545805b8e47eff3647df1574dcd36a20044ea222b6c95db823386` |

Not committed (too large, reproducible from the run id on `45046eb` + the patch): the telemetry,
trace, forensic and session files of the BASE run and of the counterfactual arms M1–M4, and the
arm patches `m1.patch`–`m4-composed-m1-m2.patch`. The counterfactual arms are DIAGNOSTIC forms, not
the selected design (`cascade-report.md` §3 C "ADMISSIBILITY"); the design is
`docs/t4.21-architecture.md`.

## How to read the numbers

Every counterfactual is judged against the LINUX BASE replay, not the director's Windows run
(CR-013 / ADR-022); `cascade-report.md` §1.1 establishes that the two agree bit-for-bit on every
population/food/migration row on this seed and differ only at the last ulp in 42 price/grievance
leaves, so the Windows observation IS the BASE column on every summary metric. "59 %", "49 pp",
"39 pp" refer to the M4 arm's residual Libur trough (782 → 321 at t118 with migration bounded),
which is the Demographics decade-integration finding CR-015 §2 relies on for N2/N3 and G3; "91.5 %"
(809 → 69) is the BASE loss with the unbounded flight surge, the migration finding for N4.
