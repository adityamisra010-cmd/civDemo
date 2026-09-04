# M4 INTEGRATION AND STABILIZATION — AUDIT RECORD

**Measured 2026-09-04** against `origin/main` at `e6cf705`, in worktrees pinned to that commit.
Every number below was produced by running the tree, not recalled. Where something was not
measured, it says so.

**This document is evidence, not authority.** It resolves nothing that requires a ruling. Its
purpose is to put the director in a position to rule on CR-003 with the measurements in hand.

---

## 1. THE HEADLINE

**All six standing quarantine failures trace to ONE open governance item: CR-003.** Not to six
independent defects, and not to anything M4 broke. The engineering question "why is the suite red"
has the same answer six times, and it is not an engineering answer.

M4's §6 exit criteria cannot be met while CR-003 is open, because one of them —
*"calibration battery green across ≥20 seeds with proven teeth"* — is exactly what the quarantine
suspends.

**AND ONE NEW BLOCKER, found by measuring rather than by reading.** The `migrationGrossPerDecade`
corridor is breached on **19 of 20 seeds** (§4.2), where the record has one seed 2 % under. Its
recorded cause — T4.2's granary cap — is **refuted by a single-variable control** that moves the
metric the WRONG WAY (§4.3). The cause is unidentified. This is not a quarantined deviation; it is
an undiagnosed regression sitting inside an M4 corridor, and it is the reason this pass reports M4
**NOT READY** on evidence rather than on opinion.

---

## 2. QUARANTINE DISPOSITION — SIX ROWS, RE-EARNED

Every row was reproduced on `e6cf705` and its failure message read. None inherited its
classification.

| failure | observed behaviour (measured) | previous | current | evidence | action |
| --- | --- | --- | --- | --- | --- |
| `ClassSystemTests.Artisans_EmergeInFedAutoplay_PlateauAtTheCap_DocumentedWindow` | post-boom artisan share min **0.035** (guard fires below 0.05) | CR-003 quarantine | **KEEP QUARANTINED — tripwire fired as designed** | `Cr003Quarantine.FamineGuardStillDisarmed`; T4.5's review record attributes the drain to the herding-weather coupling by single-variable control | none — director ruling on CR-003 |
| `ClassSystemTests.Famine_DrainsArtisansBeforePeasantStarvationPeaks` | `drain at turn 3, starvation peaked at turn 3` | quarantined | **ALREADY RULED — no action available** | director ruling 2026-08-13 (`t4.2-review-record.md`) classifies demote-first as *"INVALID / UNREPRESENTABLE AT CURRENT CAUSAL RESOLUTION"* | none — the ruling stands |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed 1)` | density **0.564833** | CR-002/CR-003 | **EVIDENCE OF LIFT — ruling required** | value is INSIDE the target corridor `[0.15, 0.6]`; it fails only the *deviation* window `[1.4, 1.8]` | 20-seed sweep run; §4 |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(seed 2)` | density **0.480596** | CR-002/CR-003 | **EVIDENCE OF LIFT — ruling required** | as above | 20-seed sweep run; §4 |
| `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed 7)` | **4** starvation deaths / 1000 turns | CR-003 | **KEEP QUARANTINED — explicitly anticipated** | `cr-003.md` §7.6 predicts this exact message on "any world where one deficit floors one person, whether or not a crash cycle exists", and records it NOT ACTIONED | tripwire message corrected; §5 |
| `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed 42)` | **3** starvation deaths / 1000 turns | CR-003 | **KEEP QUARANTINED — explicitly anticipated** | as above | as above |

**Nothing was repinned, rebanded, weakened, skipped or deleted to produce a green suite.**

### 2.1 The distinction that matters for the ruling

The two families fail for OPPOSITE reasons, and conflating them would produce a wrong ruling:

- **Density (fed corridors)** has moved BACK INTO its target corridor. Its quarantine's own stated
  lift condition is "back inside `[0.15, 0.6]`", and 0.48/0.56 satisfy it.
- **Malthus (dev corridors)** has NOT. `cr-003.md` §7.5, written after T4.7 on 20-seed evidence,
  finds the world moved AWAY from Malthusian constraint — more land per head, fewer deficits,
  `crashCount` 0/20 — and rules that this is evidence **AGAINST** lifting. The 3–4 isolated
  starvation deaths are not the crash cycle CR-003 lifts on.

**So CR-003 may be liftable in one half and not the other.** That is a director call.

---

## 3. WHY THE DENSITY CORRIDOR MOVED — T4.7, NOT T4.10

Attribution matters here because the obvious suspect is wrong. The mover is **T4.7's river-aware
traversal lattice** (`b25c395`), not T4.10's migration change and not T4.2's store bounding:

- river-aware travel cost enlarges catchments → mean effective arable **90,886 → 273,561 km² (×3.01)**
  at population ×0.993, so density falls ×0.34 (`t4.10-review-record.md` N+1.1/N+1.2, single-variable control);
- **T4.10's food-term removal moves density ×1.0002** — nothing.

The density corridor is a ratio whose DENOMINATOR M4 was always expected to move (`m4-spec.md`
§3.3 names transport as one of its drivers). It moved as predicted.

---

## 4. THE 20-SEED SWEEP

`m4-spec.md` §6 requires ≥20 seeds, and the quarantine record's own standard is that
*"a floor validated on two seeds is not a floor"*. Two seeds are what the unit test gives.
Run at `e6cf705`, unmodified: `sim autoplay --seeds 20 --turns 650`.

**Command:** `sim autoplay --seeds 20 --turns 650` at `e6cf705`, unmodified tree.
**Artifact:** deliberately NOT committed (it is a 375 KB measurement, and the T4.10 precedent is
that these stay untracked). Reproducible by the command above.

### 4.1 DENSITY — the quarantine's lift condition is MET

| | |
| --- | --- |
| target corridor | `[0.15, 0.6]` |
| **in band** | **20 / 20** |
| min / mean / max | 0.28080 / 0.41290 / 0.56483 |

Every seed is inside the corridor. The recorded DEVIATION window `[1.4, 1.8]` now describes a world
that no longer exists: the failing assertion is `AssertDensityKnownDeviation`, which fires because
the value fell BELOW the deviation window — i.e. because it came home. This satisfies, at the
20-seed standard, the condition the quarantine itself names for lifting.

**It is not lifted here.** CR-002/CR-003 own it and both are the director's. What this pass
supplies is the evidence the lift requires.

### 4.2 MIGRATION — a NEW breach, materially worse than the recorded one

| | |
| --- | --- |
| target corridor | `[0.001, 0.01]` (floor is ABSOLUTE) |
| **in band** | **1 / 20** |
| min / mean / max | 0.00029 / 0.00057 / 0.00103 |

**This is not the deviation on record.** The quarantine (M3 exit) records *one* seed in twenty
falling **2 %** under the floor. Measured now: **nineteen** of twenty fall under it, the worst by
**3.4×**. That is a change in kind, not in degree, and no document on the tree describes it.

Per-seed:

| seed | migrationGrossPerDecade | vs floor 0.001 |
| --- | --- | --- |
| 1 | 0.00029 | **UNDER FLOOR** |
| 2 | 0.00057 | **UNDER FLOOR** |
| 3 | 0.00103 | IN |
| 4 | 0.00036 | **UNDER FLOOR** |
| 5 | 0.00043 | **UNDER FLOOR** |
| 6 | 0.00040 | **UNDER FLOOR** |
| 7 | 0.00051 | **UNDER FLOOR** |
| 8 | 0.00031 | **UNDER FLOOR** |
| 9 | 0.00055 | **UNDER FLOOR** |
| 10 | 0.00047 | **UNDER FLOOR** |
| 11 | 0.00100 | **UNDER FLOOR** |
| 12 | 0.00045 | **UNDER FLOOR** |
| 13 | 0.00039 | **UNDER FLOOR** |
| 14 | 0.00077 | **UNDER FLOOR** |
| 15 | 0.00075 | **UNDER FLOOR** |
| 16 | 0.00085 | **UNDER FLOOR** |
| 17 | 0.00050 | **UNDER FLOOR** |
| 18 | 0.00058 | **UNDER FLOOR** |
| 19 | 0.00050 | **UNDER FLOOR** |
| 20 | 0.00075 | **UNDER FLOOR** |

**No band was touched.** Re-banding this to green would be precisely the
"test fails → repin → green" this repository forbids.

### 4.3 ARM C — THE RECORDED ATTRIBUTION IS REFUTED BY MEASUREMENT

The standing explanation for the migration shortfall is T4.2's `granaryYearsOfDemand = 1.5` cap.
`t4.10-review-record.md` §N+2 measured a **×5.56 recovery, 8/8 back in band**, when that cap is
lifted. That measurement was taken on a PRE-T4.10 tree. It was re-run here on `e6cf705`, config-only,
single variable — `granaryYearsOfDemand: 1.5 → 1e6`, nothing else, 20 seeds, 650 turns:

| | baseline `e6cf705` | ARM C (cap lifted) |
| --- | --- | --- |
| migration in band `[0.001, 0.01]` | 1 / 20 | **0 / 20** |
| migration mean | 0.00057 | **0.00036** |
| per-seed ratio armC ÷ baseline | — | min 0.36× · median **0.67×** · max 1.02× |
| density in band `[0.15, 0.6]` | 20 / 20 | 20 / 20 (mean 0.4129 → 0.4234) |

**Lifting the cap makes migration WORSE, not better.** The ×5.56 recovery does not reproduce; the
measured effect has the opposite sign. **T4.2's granary cap is therefore NOT the cause of the
migration shortfall on the integrated tree, and the recorded attribution is superseded.**

**The actual cause is UNIDENTIFIED, and this pass does not guess at it.** What is now known:

- it is not the granary cap (refuted above, single-variable, 20 seeds);
- it is not T4.10's food-term removal, measured at ×0.95 — far too small for a shortfall that needs
  ~1.75× to clear the floor;
- the M3-exit record has 19/20 seeds INSIDE the band with one seed 2 % under, so the change happened
  somewhere in M4;
- density and migration move independently here — arm C moved migration 0.63× while leaving density
  effectively unchanged — so whatever moved migration is not simply the catchment enlargement that
  moved density.

**Recommended next task: a per-merge bisection of `migrationGrossPerDecade` across the M4 merge
sequence** (`d8d8f48` T4.2 → `51008d8` T4.1b spacing → `b25c395` T4.7 → `070f05b` T4.4 → `e6cf705`),
20 seeds per arm. T4.1b is worth naming as a prior suspect that this pass did NOT test: ADR-018 moved
`minSpacingKm` 480 → 95.2, which changes settlement count and therefore every per-capita migration
denominator. That is a hypothesis, not a finding.

---

## 5. CHANGES MADE IN THIS PASS

All are either defect repairs inside the ratified architecture or corrections of statements the
tree contradicts. **No behaviour that any golden observes was changed, and no golden moved.**

1. **M4-D's control rule now has teeth where orders are CONSUMED.**
   `ConstructionSystem.Step` checked only settlement existence; the control rule lived solely in
   `OrderValidation`, which runs ONCE before turn 1 against the turn-0 world. It therefore could
   never cover a colonised settlement (which does not exist at turn 0) nor any order not arriving
   through a loaded log. The check is now made at the point of consumption as well.
   Mutant-verified: removing it kills the new trespass test, and the anti-vacuity companion
   correctly survives the mutation.

2. **Three in-code contracts asserting "nothing writes Controls" corrected.** False since M4-C, and
   load-bearing: T4.5's argument for why appropriation is safe rested on it.

3. **The Malthus tripwire message corrected.** It instructed the reader to *"delete this quarantine"*
   — the opposite of what `cr-003.md` §7.5 rules. A future agent following the message would have
   closed an open CR by accident.

4. **Stale documentation repaired:** `t4.7-review-record.md`'s "DO NOT MERGE" headline (it was
   director-certified and merged at `b25c395`); `CLAUDE.md`'s milestone line; `handoff-status.md`'s
   "M4 not started"; `queue.md`'s "M4-B not merged".

---

## 6. QUESTIONS FOR THE DIRECTOR — implementation is blocked on these

See the session report for the full statement of each. In brief:

0. **The migration corridor breach (§4.2/§4.3)** — 19/20 seeds under an absolute floor, recorded
   cause refuted. Needs a bisection, and it is the single largest obstacle to M4 exit.
1. **CR-003 disposition** — liftable for density (20/20 in band), not for Malthus? Blocks M4 exit.
2. **Do colonies inherit their founder's control?** `WorldFounding` says colonization "extends the
   same Empire by appending one more control row"; `ColonizationSystem` writes none and calls the
   result stateless per D-037 B1. The two contracts contradict. Blocks T4.5 reachability and
   foreign-trade reachability.
3. **T4.5 appropriation is now doubly unreachable** because M4-C controls every founded settlement.
   The queued worldgen fix no longer suffices.

---

## 7. WHAT WAS NOT DONE, AND WHY

- **T4.11 (merchants)** — not started anywhere. Blocked empirically: it emerges on trade volume,
  and foreign trade is structurally unreachable while one polity controls everything.
- **T4.13 (comfort-as-stock)** — exists UNMERGED on `t4.13-comfort-as-stock` (`21e6aa5`, 25 files,
  ~1,732 insertions, with a review record). Cut from an older main; claims schema v22 for
  `HouseholdGoodsRow` where main is now v24. Needs re-derivation, not a merge.
- **T4.15 (the M4 exit artifact)** — exists UNMERGED on `t4.15-exit-inventory` (`01cb191`, docs
  only, `docs/m4-exit-inventory.md`). It inventories a tree five merges old. An exit artifact
  written before the milestone can exit would be fiction; it is deliberately left for after the
  CR-003 ruling.
