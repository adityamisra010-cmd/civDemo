# M4 — The review state for an out-of-band density observation

Packet: `m4-density-review-state`, cut from `dac4ffc` (`t4.19-glass-box`).
Status: DOCUMENT FIRST. This file states the traced contract and the exact proposed
change. Nothing was implemented before it was committed.

**This document changes no simulation semantics.** `Sim.Data/content/corridors.json`
is not simulation input (traced below), and the change proposed here touches only that
file, `Sim.Tests`, and docs.

---

## 1. The traced nightly contract

Traced at `dac4ffc` in `.github/workflows/ci.yml`, step
"Corridor teeth across all 20 seeds (bands + quarantines from corridors.json)".

**Where results surface.** The step first prints a per-corridor status line over the
20-seed `nightly-metrics.json`, between two rule lines, for
`densityPerArableKm2` and `migrationGrossPerDecade`:

```
QUARANTINED  <key>  measured [<lo>, <hi>]  window <w>  band <b>  owner: <o>
GATED        <key>  measured [<lo>, <hi>]  band <b>
```

`<lo>`/`<hi>` are the min and max of that metric ACROSS ALL SEEDS, always printed,
in both branches. Visibility is therefore unconditional; only the *classification*
differs.

**The pass/fail rule.** A second `jq -e` then gates:

```
def gated($c; $v): ($c.quarantine? and $c.quarantine.active)
  or ($v >= $c.band[0] and $v <= $c.band[1]);
[ .seeds[] | (.finalPopulation > 0) and gated($dc; ...) and gated($mc; ...) ] | all
```

`jq -e` exits non-zero when the result is `false`, and the `||` branch prints
`NIGHTLY CORRIDOR BREACH` and `exit 1`. So a metric outside its band on ANY seed
gives a non-zero job exit — unless the corridor carries `quarantine.active == true`,
in which case `gated` short-circuits to true for every seed. Note the shape of that
short-circuit: **the gate keys on `active`, not on the window.** The window's teeth
live in the battery, not in `jq`.

**What quarantine is, and is not.** The T3.12 comment in the same step states the
contract verbatim: a quarantine is "a KNOWN, RULED deviation whose target band is
deliberately left un-retuned"; such a corridor "is reported QUARANTINED WITH ITS
MEASURED RANGE and does NOT gate"; and "it must not go silent either — that is the
whole defect this closes." A quarantine is therefore
**classification + acceptance + mandatory visibility**. It is *not* suppression, it
is *not* a band change, and it is *not* a claim that the deviation is fine forever —
every quarantine in this file carries an owner and a lift condition.

`canonical.migrationGrossPerDecade` is the live working example: `active: true`,
band untouched at `[0.001, 0.01]`, `disposition: "ACCEPTED AS MEASURED (M4
completion, director ruling 2026-09-04) — NOT tuned, NOT re-banded."`

**The second reader.** `Sim.Tests/TestUtil/Corridors.cs` reads the same file, and
`Corridors.Quarantine(group, corridor)` returns the window only when `active` is
true, else null ("gate normally"). T3.12's stated purpose: "corridors.json is now
the single source both readers use." Confirmed readers of `corridors.json` at
`dac4ffc`, by grep over the tree: `.github/workflows/ci.yml`,
`Sim.Data/Sim.Data.csproj` (embeds it), `Sim.Data/DataFiles.cs`,
`Sim.Tests/TestUtil/Corridors.cs`, `Sim.Tests/Systems/MigrationTests.cs`,
`Sim.Tests/Systems/CalibrationBatteryTests.cs`. **Nothing under `Sim.Core`,
`Sim.Cli` or `Sim.Ui` reads it.**

---

## 2. The three states, and which one density is in

| | meaning | gates? | visible? |
|---|---|---|---|
| **A — RATIFIED / PASS** | inside the band, or covered by an accepted quarantine whose window contains the measurement | no | as `GATED`/`QUARANTINED` line |
| **B — OBSERVED / REVIEW** | outside the historical corridor, NOT ruled a simulation defect: reported loudly with measured range and owner | no | `QUARANTINED … measured […]` + battery console line |
| **C — FAILURE** | violates a ratified invariant, or is a regression against a recorded envelope | **yes, exit 1** | breach message / red test |

**Density is in B and is currently reported as C.** The 2026-09-04 lift moved it to
state A on correct evidence; the later T4.19 lane C founding-demographic correction
shifted the level; nothing reclassified it, so it fell through to C.

## 3. No new mechanism is invented

State B already has a mechanism in this repository: the T3.12 quarantine block, with
the nightly's `QUARANTINED` branch and the battery's window-teeth pattern
(`AssertDevMigrationQuarantine`). The gap the director identified is **process, not
machinery**: there is no step that takes a new out-of-band observation and *classifies*
it. This packet performs that classification for density using the existing mechanism,
and adds nothing to the mechanism itself.

## 4. The observation being classified

`docs/t4.19c-remeasurement.md`, 20 seeds / 650 turns, `canonical.densityPerArableKm2`:

* NEW arm: **14/20 in band**, min **0.36857**, mean **0.54063**, max **0.74211**.
* Six seeds above the 0.6 ceiling: 20 (0.6038), 8 (0.6081), 6 (0.6230), 1 (0.6251),
  13 (0.6610), 2 (0.7421).
* `arableKm2` **bit-identical** between arms on all 20 seeds, and the per-seed density
  ratio equals the per-seed population ratio to 4 decimals. The move is **100%
  numerator**: a level shift at unchanged growth rate, fully attributed to the T4.19
  lane C founding demographic correction.

This is a level shift with a named, ruled cause — the signature of B, not of C.

## 5. The honest fact about the 2026-09-04 lift

**Re-activating this quarantine does NOT reverse the lift ruling.** The lift was
*correct on its evidence*: at `e6cf705`, 20/20 seeds in band (min 0.28080, mean
0.41290, max 0.56483) against an untouched `[0.15, 0.6]`, meeting the quarantine's own
stated lift condition at the ≥20-seed standard. That record stands and is preserved
verbatim in `liftedBy` / `liftEvidence`.

What follows it is a **NEW deviation with a NEW, attributed cause** — the T4.19
founding demographic correction, landed after the lift — recorded as its own entry,
with its own evidence and its own lift condition. Two sequential rulings on the same
corridor, not one ruling overturned.

## 6. The exact proposed change

1. `Sim.Data/content/corridors.json`, `canonical.densityPerArableKm2.quarantine`:
   `active` false → **true**; `window` `[1.40, 1.80]` → **`[0.36857, 0.74211]`**, the
   measured NEW min and max at their measured precision (not rounded); `owner`,
   `reason` (attributed cause; band is the TARGET and is NOT re-tuned) and a stated
   `liftCondition` for this new quarantine. The existing `history`, `liftedBy` and
   `liftEvidence` text is **preserved entire and appended to** — the lift record is
   not deleted. **The band stays `[0.15, 0.6]`.** Migration's entry is untouched.
2. `Sim.Tests/Systems/CalibrationBatteryTests.cs`: canonical density goes through a
   quarantine-aware assertion with **teeth in both directions against the window**,
   exactly as `AssertDevMigrationQuarantine` does — below the window is a NEW defect,
   above the window is a NEW defect — plus a band-immovability guard asserting the band
   is still `[0.15, 0.6]` while the quarantine stands, and a loud console line carrying
   the measured range and owner. It reuses `Corridors.Quarantine(...)`. It is **not** a
   bare skip.

Nothing else changes. No file under `Sim.Core`, `Sim.Cli` or `Sim.Ui` is touched.

## 7. The test-count consequence, stated plainly

Baseline at `dac4ffc`: Sim.Tests **722 passed / 4 failed / 6 skipped**; Sim.Ui.Tests
236/236. The four reds are `Canonical_FedCorridors` seeds 1 and 2 (density) and
`Dev_MalthusCorridors` seeds 42 and 7 (CR-003).

This change is expected to turn the **two density reds green**, because they will
assert against the window instead of the band.

> **THIS IS A RECLASSIFICATION FROM C TO B, NOT A FIX.** No simulation behaviour
> changed. The density measurement is exactly what it was; what changed is that the
> project now *classifies* it as an accepted, owned, loudly-reported observation
> instead of letting it read as an invariant failure.

The two CR-003 reds MUST remain red and are untouched. Any other test changing state
is a stop-and-report condition.
