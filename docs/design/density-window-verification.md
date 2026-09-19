# DENSITY-WINDOW BREACH — VERIFICATION LANE VERDICT

**Lane:** density-breach verification (measurement lane, M4 closure audit blocker **B5**).
**Date:** 2026-09-19. **Author:** verification agent, own detached worktrees.
**Role constraint:** this lane MEASURES and reports. It makes no Director-level ruling, and it
edited no band, no window, no test and no workflow. `Sim.Data/content/corridors.json`,
`Sim.Tests/Systems/CalibrationBatteryTests.cs` and `.github/workflows/ci.yml` are byte-unchanged on
every tree this lane touched (§8.3).

---

## 0. VERDICT

> ## **CONFIRMED.**
>
> **The number reproduces exactly.** Canonical `densityPerArableKm2` at **seed 3** on the candidate
> `9f6c6ae` measures **0.35415668759623087**, against the recorded quarantine-window floor
> **0.3685744951368359** — **3.9117757 % below**. MEASURED (this lane, `9f6c6ae`, §2).
>
> **The instrument gap reproduces exactly, and it is total.** The shipped nightly gate
> (`.github/workflows/ci.yml:232-233` at `9f6c6ae`) returns **exit 0 / PASS** on the candidate's own
> 20-seed metrics; an otherwise-identical control gate that consults `quarantine.window` returns
> **exit 1 / BREACH** on the same file. `CalibrationBatteryTests` is **7 passed / 0 failed** in
> Release on the candidate tree, because its canonical theory runs **seeds 1 and 2 only**
> (`CalibrationBatteryTests.cs:426-428` at `9f6c6ae`) and both are comfortably inside the window.
> MEASURED (this lane, §4).
>
> **Attribution: T4.21 CAUSED this breach; it is not pre-existing.** On the pre-T4.21 tree
> `45046eb` the same instrument measures **20/20 inside the window**, reproducing both window
> endpoints **bit-for-bit** (min = seed 3 = `0.3685744951368359`, max = seed 2 =
> `0.7421101248166698`). MEASURED (this lane, §3).
>
> **The mechanism claim is CONFIRMED with one correction.** Seed 3's arable is **bit-identical**
> across the two arms, so the density move *is* the population move: 133,750 → 128,518 people,
> **−3.9117757009345686 %** — the same number, to every digit, as the window shortfall (§3.3). Across
> the sweep the per-seed density ratio equals the per-seed population ratio to machine precision on
> **18 of 20** seeds with arable bit-identical; on **seeds 9 and 14 arable itself moved**
> (−2.4692 % and +0.0401 %), so "arable is invariant between arms" is **not** universally true on
> this arm. MEASURED (this lane, §3.2).
>
> **One sub-claim of the prior report is REFUTED** (minor, does not disturb the finding): "T4.21
> moves every seed down" is false — **seed 16 moves UP** (+1.1776 %, population 147,500 → 149,237).
> The aggregate figures the same sentence quotes (ratio min 0.9421, mean 0.9691) are correct.
> MEASURED (this lane, §5).

**What this verdict does NOT do.** It does not decide whether the breach is a *defect*, whether the
window should be re-pinned, whether the quarantine should lift, or whether M4 may close over it.
Those are **DIRECTOR DECISION REQUIRED** (§7).

---

## 1. WHAT THE CORRIDOR SAYS — quoted, not paraphrased

All quotations from `Sim.Data/content/corridors.json` **at the candidate `9f6c6ae`**. The file is
**byte-identical between `45046eb` and `9f6c6ae`** (MEASURED: `git diff 45046eb 9f6c6ae --
Sim.Data/content/corridors.json` is empty), so the candidate is judged against the same instrument
the pre-T4.21 tree shipped.

### 1.1 The band — the TARGET

`corridors.json:60-62` — RATIFIED:

```json
"densityPerArableKm2": {
  "band": [ 0.15, 0.6 ],
```

The band note (`:63`) adds, and this lane repeats it because it constrains how the number below may
be read: *"CAUTION FOR WHOEVER RULES: population is NOT what moved and is not what this metric is
now measuring … this reading is very nearly a statement about catchment GEOMETRY divided into a
demography-driven numerator."* RATIFIED (`corridors.json:63`). **On the arm measured here that
caution inverts** — see §3.2.

### 1.2 The quarantine — the RECORDED DEVIATION

`corridors.json:66-71` — RATIFIED:

```json
"quarantine": {
  "active": true,
  "window": [
    0.3685744951368359,
    0.7421101248166698
  ],
```

`:72` **owner** — RATIFIED: *"T4.19 lane C founding-demographic correction — the M4 exit packet that
owns the founding vector must resolve this: either re-derive the corridor under a director ruling,
or demonstrate the world back inside [0.15, 0.6]."*

`:73` **reason** — RATIFIED, the load-bearing sentences:

- *"The window here is the MEASURED envelope of the new arm, stated at FULL measured precision
  (min = seed 3 at 0.3685744951368359, max = seed 2 at 0.7421101248166698, re-measured in-battery
  at this commit …). The extremes are NOT rounded outward to give headroom and NOT rounded inward,
  which would put the extreme seeds outside their own envelope. It is not a new target."*
- *"Per T3.12 the nightly reports this corridor QUARANTINED WITH ITS MEASURED RANGE PRINTED and
  does not gate; it does not go silent. The battery keeps per-seed teeth in BOTH directions against
  'window' plus a band-immovability guard."*

**INFERRED, and it is the crux of this lane:** a window pinned as the *exact, un-padded* envelope of
a specific arm has **zero tolerance by construction**. Any change that lowers the minimum seed by
any amount breaches it. The window was pinned on the arm `45046eb` reproduces; T4.21 lowers that
arm's minimum seed. The breach is therefore the arithmetically guaranteed consequence of a
zero-headroom pin meeting a down-only mechanism — not a surprise, and not on its own evidence of a
simulation fault.

`:74` **liftCondition** — RATIFIED: *"lifts on either of: (a) a >=20-seed / 650-turn sweep measuring
densityPerArableKm2 IN BAND on 20/20 against the untouched [0.15, 0.6] … or (b) an explicit
director ruling re-deriving the corridor itself on evidence, which is a change to the instrument and
needs its own ruling … Six seeds drifting further out is NOT a lift condition and NOT permission to
move the band."*

**MEASURED (this lane): neither lift condition is met on the candidate.** The candidate is **17/20**
in band, not 20/20 (§2.2); and no Director ruling re-deriving the corridor exists on this tree.

### 1.3 The history and the prior record

`:75` **history** — RATIFIED, and it carries the precedent this lane's finding rhymes with:
*"Fitted at T3.8 on canonical seeds 1 and 2 only (1.53756 / 1.60184). T3.12 measured all 20 nightly
seeds for the first time … **A FLOOR VALIDATED ON TWO SEEDS IS NOT A FLOOR.**"* The same two seeds
are the only two the battery still runs (§4.2).

`:76-78` **liftedBy / liftEvidence** — RATIFIED: the 2026-09-04 lift at `e6cf705`, 20/20 in band,
min 0.28080 / mean 0.41290 / max 0.56483. Preserved and untouched by the re-activation.

### 1.4 The battery's own teeth

`Sim.Tests/Systems/CalibrationBatteryTests.cs:557-566` at `9f6c6ae` — RATIFIED. The DOWN tooth says
what a sub-window reading means:

```csharp
Assert.True(value >= wlo,
    $"seed {seed}: {Inv(value)} is BELOW the measured quarantine window [{wlo}, {whi}] — " +
    "a NEW defect, not the recorded founding-correction level shift. …");
```

and the UP tooth (`:562-566`) says an above-window reading *"is a NEW defect or a ruled substrate
change that must re-pin this window deliberately."*

**The tooth for the direction that actually moved exists, is written, is correct — and never runs on
the seed that breaches.** MEASURED (§4.2).

---

## 2. THE MEASUREMENT

### 2.1 Instrument and method

**Instrument used: the nightly's own instrument** — `sim autoplay --seeds 20 --turns 650 --metrics
<file>`, reading `derived.densityPerArableKm2` from the emitted `autoplay-metrics/v1` JSON. This is
literally the command `.github/workflows/ci.yml:196-199` runs, and the value is computed by
`CalibrationAnalysis.DensityPerArableKm2(m)` at `Sim.Cli/Program.cs:865` — **the same static the
in-process battery calls** at `CalibrationBatteryTests.cs:485-486`. The two instruments cannot
disagree on the number; they disagree only on which seeds they see and what they do with it.
MEASURED (this lane).

Trees, each in its own detached worktree created by this lane and used by nobody else
(`git -C /home/user/civDemo worktree add --detach <path> <sha>`), each built `-c Release`:

| tree | sha | what it is |
|---|---|---|
| main | `dbef61a486396c8ad82e1aebcaee0b0720bbaed0` | `Merge M4 completion (director-certified): the M4 baseline` |
| pre-T4.21 | `45046ebc946fabfdd274a3fcb394c9eb3ee7fb41` | `P3: headless inspection …` — the T4.21 base |
| candidate | `9f6c6ae9d860b5e2ea0c0200b42c1528e40782f7` | `T4.21 DIRECTOR'S REPORT: apply the adversarial reader's corrections` |

MEASURED (this lane): `dbef61a` is an ancestor of `45046eb`, which is an ancestor of `9f6c6ae`
(`git merge-base --is-ancestor`, both true). MEASURED: `git diff --stat 64a3f2f 9f6c6ae --
Sim.Core Sim.Data Sim.Cli` is **empty** — the candidate's simulation code is identical to `64a3f2f`,
the "AFTER" tree the prior lane measured, so this lane's numbers are directly comparable to it.

`claude/civdemo-work-b1z2y4`'s own checkout at `/home/user/civDemo` was never used for a build or a
run; it was read only.

### 2.2 Canonical `densityPerArableKm2`, 20 seeds × 650 turns, three trees

MEASURED (this lane). Density is `finalPopulation / arableKm2`, checked on every row. Window is
`[0.3685744951368359, 0.7421101248166698]`; band is `[0.15, 0.6]`.

| seed | main `dbef61a` density | pre-T4.21 `45046eb` density | candidate `9f6c6ae` density | cand. pop | pre pop | pop ratio | arable ratio | cand. vs window |
|---|---|---|---|---|---|---|---|---|
| 1 | 0.480633 | 0.625062 | 0.607323 | 162,658 | 167,409 | 0.971620 | 1.000000000 (bit-identical) | inside |
| 2 | 0.563834 | 0.742110 | 0.716465 | 161,397 | 167,174 | 0.965443 | 1.000000000 (bit-identical) | inside |
| 3 | 0.280329 | 0.368574 | 0.354157 | 128,518 | 133,750 | 0.960882 | 1.000000000 (bit-identical) | **BELOW by 3.912 %** |
| 4 | 0.420977 | 0.550469 | 0.527108 | 135,001 | 140,984 | 0.957563 | 1.000000000 (bit-identical) | inside |
| 5 | 0.405423 | 0.532183 | 0.522325 | 133,090 | 135,602 | 0.981475 | 1.000000000 (bit-identical) | inside |
| 6 | 0.478651 | 0.622998 | 0.586963 | 158,294 | 168,012 | 0.942159 | 1.000000000 (bit-identical) | inside |
| 7 | 0.447356 | 0.585288 | 0.557814 | 157,187 | 164,929 | 0.953059 | 1.000000000 (bit-identical) | inside |
| 8 | 0.464645 | 0.608121 | 0.584843 | 152,179 | 158,236 | 0.961722 | 1.000000000 (bit-identical) | inside |
| 9 | 0.314634 | 0.411364 | 0.408026 | 100,991 | 104,395 | 0.967393 | **0.975308313** | inside |
| 10 | 0.376938 | 0.494086 | 0.480608 | 133,010 | 136,740 | 0.972722 | 1.000000000 (bit-identical) | inside |
| 11 | 0.393272 | 0.519253 | 0.508608 | 127,187 | 129,849 | 0.979499 | 1.000000000 (bit-identical) | inside |
| 12 | 0.372753 | 0.487428 | 0.482530 | 147,286 | 148,781 | 0.989952 | 1.000000000 (bit-identical) | inside |
| 13 | 0.508439 | 0.661009 | 0.622733 | 158,791 | 168,551 | 0.942095 | 1.000000000 (bit-identical) | inside |
| 14 | 0.388572 | 0.503580 | 0.490982 | 142,662 | 146,264 | 0.975373 | **1.000400580** | inside |
| 15 | 0.365833 | 0.480180 | 0.470366 | 123,565 | 126,143 | 0.979563 | 1.000000000 (bit-identical) | inside |
| 16 | 0.375602 | 0.498402 | 0.504271 | 149,237 | 147,500 | 1.011776 | 1.000000000 (bit-identical) | inside |
| 17 | 0.406317 | 0.526604 | 0.508081 | 138,599 | 143,652 | 0.964825 | 1.000000000 (bit-identical) | inside |
| 18 | 0.392703 | 0.518929 | 0.495920 | 139,863 | 146,352 | 0.955662 | 1.000000000 (bit-identical) | inside |
| 19 | 0.360585 | 0.473185 | 0.453729 | 132,177 | 137,845 | 0.958881 | 1.000000000 (bit-identical) | inside |
| 20 | 0.460358 | 0.603792 | 0.582953 | 149,577 | 154,924 | 0.965486 | 1.000000000 (bit-identical) | inside |

**Summary.** MEASURED (this lane):

| tree | min (seed) | mean | max (seed) | in window 20/20? | in band `[0.15, 0.6]` |
|---|---|---|---|---|---|
| main `dbef61a` | 0.280328805 (3) | 0.412892726 | 0.563833942 (2) | 16/20 — seeds 3, 9, 15, 19 below | **20/20** |
| pre-T4.21 `45046eb` | **0.3685744951368359** (3) | 0.540630956 | **0.7421101248166698** (2) | **20/20** | 14/20 |
| candidate `9f6c6ae` | **0.35415668759623087** (3) | 0.523290307 | 0.716465167 (2) | **19/20 — seed 3 below** | 17/20 |

**The claim under verification, restated with the measured numbers:**

```
seed 3, candidate 9f6c6ae : 0.35415668759623087
recorded window floor     : 0.3685744951368359
absolute shortfall        : 0.01441780754060501
relative shortfall        : 3.9117757009345686 %
```

The prior report's `0.354157` and "3.9 % below" are **exact roundings of these values**. CONFIRMED.

**Cross-validation of the pipeline.** `45046eb` reproduces the two window endpoints to the last
digit — `0.3685744951368359` and `0.7421101248166698`, the literal values in `corridors.json:69-70`.
MEASURED (this lane). That is an unfalsifiable-by-accident check: this lane's instrument, built and
run independently, lands on the same two 17-digit doubles the window was pinned from. It also
**dates the window to the `45046eb` arm**, exactly as `corridors.json:73` claims.

`dbef61a` reproduces `docs/t4.19c-remeasurement.md:125`'s OLD arm (`feaf218`) to five decimal places
on all three summary statistics — 0.28033 / 0.41289 / 0.56383 — and its seed-3 population, 101,727,
is that record's value verbatim (`:101`). `45046eb` reproduces the same record's NEW arm (`c41c896`)
likewise: seed-3 population 133,750 (`:101`), seed-2 density 0.74211 (`:100`). MEASURED (this lane).

### 2.3 Also measured, recorded because it sits in the same gate

`canonical.migrationGrossPerDecade`, same sweeps, against its own window `[0.0009, 0.01]`:
**0/20 inside on all three trees** (candidate `[0.00023291, 0.00042778]`; pre-T4.21
`[0.00023862, 0.00091615]`; main `[0.00029353, 0.00101349]`). MEASURED (this lane).

**This is the already-recorded state, not a new finding**: `corridors.json:94`'s disposition already
rules this corridor *"ACCEPTED AS MEASURED … retained as a RECORD of a historical expectation this
world no longer meets, not as an acceptance gate"*, and its window is stale against that disposition
by the disposition's own account. RATIFIED. It is noted here only so a reader of §4's dry-run is not
surprised that a window-consulting control gate fails on *every* tree when migration is included —
which is why §4.3 isolates density.

---

## 3. ATTRIBUTION

### 3.1 Is the breach pre-existing, or did T4.21 cause it?

**T4.21 caused it, against the recorded window.** MEASURED (this lane):

- On `45046eb` (pre-T4.21): **20/20 inside the window**, minimum exactly on the floor.
- On `9f6c6ae` (candidate): **19/20**; seed 3 falls 3.9118 % under.

There is no tree between them at which the corridor was already out. The T4.21 diff is the only
variable. **CONFIRMED.**

**But the honest framing needs main stated too, and it cuts the other way.** On `dbef61a` (main),
**four** seeds — 3, 9, 15, 19 — sit below the *current* window, seed 3 by 23.9 %. That is **not** a
breach in any governance sense: on main the quarantine block reads `"active": false` with the
superseded window `[1.4, 1.8]` (MEASURED: `git show dbef61a:Sim.Data/content/corridors.json`), so
main's gate is the **band**, and main is **20/20 in band**. MEASURED (this lane, §4.3 dry-run).

**INFERRED:** the window is not a property of the simulation, it is a property of *one arm*. Main is
"below the window" only because the window describes an arm main predates. The only defensible
statement of causality is the narrow one: *relative to the arm the window was pinned on, the T4.21
diff moved one seed out of it.*

### 3.2 Numerator or denominator? — the prior report's claim, checked

The prior report attributes the movement 100 % to the numerator with arable bit-identical. Checked
directly. MEASURED (this lane), per-seed `45046eb` → `9f6c6ae`:

| check | result |
|---|---|
| density ratio == population ratio to machine precision (`\|dr/pr − 1\| ≤ 2.3e−16`) | **18 of 20** seeds |
| `arableKm2` bit-identical between arms | **18 of 20** seeds |
| seeds where arable moved | **9** (×0.975308313, **−2.4692 %**) and **14** (×1.000400580, **+0.0401 %**) |
| density ratio min / mean / max | 0.942095 (seed 13) / 0.969063 / **1.011776 (seed 16)** |
| population ratio min / mean / max | 0.942095 / 0.967857 / 1.011776 |

**CONFIRMED with the stated correction.** On the 18 seeds where arable is bit-identical, density =
population / arable with an unchanged denominator, so the density move **is** the population move —
identically, not approximately. On seeds 9 and 14 the denominator moved as well, so **"arable is
invariant between arms" is refuted as a universal claim on this arm**, exactly as the prior report
itself flagged. The T4.19c attribution (`corridors.json:73`, *"arableKm2 is BIT-IDENTICAL between the
two arms on all 20 seeds"*) was true of **its** arm; it is **not** true of the T4.21 arm.

**INFERRED (mechanism):** bounded migration changes where people settle and therefore which lattice
blocks fall inside catchments, so the denominator becomes arm-sensitive. This is the plumbing the
prior report named; this lane measured the same two seeds and the same two magnitudes and did not
attempt an independent single-variable bisection of which T4.21 sub-packet did it (out of scope,
§8.2).

### 3.3 Seed 3 specifically

MEASURED (this lane):

```
arable, both arms : 362884.577649206 km²  (bit-identical)
population        : 133,750  (45046eb)  →  128,518  (9f6c6ae)      −5,232 people
population change : −3.9117757009345686 %
density           : 0.3685744951368359  →  0.35415668759623087
density change    : −3.9117757009345686 %
```

The density shortfall against the floor and the population shortfall are **the same number to every
digit**, because seed 3's pre-T4.21 density *is* the floor. **CONFIRMED:** at seed 3 the breach is
100 % numerator, and the "plausibly reduced population slightly" hypothesis in the task is exactly
right — 5,232 people out of 133,750.

### 3.4 Was the direction pre-committed?

**RATIFIED — yes, and stated in advance.** `docs/t4.21-architecture.md:899` pre-commits the reading
for this very corridor:

> `canonical.densityPerArableKm2` … *"headroom cap can only LOWER growth; expected unchanged at 20
> seeds"* → pre-committed reading: *"unchanged within noise"*

and `:912` lists, under the outbound coupling map, *"headroom cap … density corridor (down-only)"*.

**INFERRED:** the **direction** is exactly as pre-committed (19 of 20 seeds down). What the
pre-commitment got wrong is the **magnitude**: "unchanged within noise" is not what a 3.1 % mean
population reduction does to a window pinned with zero headroom. The gap between a pre-committed
direction and an un-sized magnitude is the whole of this finding.

---

## 4. THE INSTRUMENT GAP — known-open item 11, verified

### 4.1 The nightly

`.github/workflows/ci.yml` at `9f6c6ae`. RATIFIED (quoted verbatim).

The **report** step (`:219-227`) does read the window, and prints it:

```
:224            if ($c.quarantine? and $c.quarantine.active)
:225            then "QUARANTINED  \($k)  measured [\($lo), \($hi)]  window \($c.quarantine.window)  band \($c.band)  owner: \($c.quarantine.owner)"
:226            else "GATED        \($k)  measured [\($lo), \($hi)]  band \($c.band)"
```

The **gate** step (`:229-239`) does not:

```
:232            def gated($c; $v): ($c.quarantine? and $c.quarantine.active)
:233              or ($v >= $c.band[0] and $v <= $c.band[1]);
:234            [ .seeds[] |
:235              (.finalPopulation > 0) and
:236              gated($dc; .derived.densityPerArableKm2) and
:237              gated($mc; .derived.migrationGrossPerDecade)
:238            ] | all' nightly-metrics.json \
:239          || { echo "NIGHTLY CORRIDOR BREACH — inspect nightly-metrics.json artifact"; exit 1; }
```

`quarantine.active` is `true`, so the first disjunct is `true`, so `$v` is **never read**. MEASURED
(this lane): the word `window` does not appear anywhere in `:229-239`.

**A precision the claim as stated does not carry, and it matters for the remedy.** The nightly is
**not blind — it is mute**. `:225` prints the measured range *beside* the window, so the candidate's
own nightly log would contain, on one line:

```
QUARANTINED  densityPerArableKm2  measured [0.35415668759623087, 0.7164651669221055]  window [0.3685744951368359,0.7421101248166698]  band [0.15,0.6]  owner: …
```

MEASURED (this lane, §4.3 — that is real output from the shipped jq against the candidate's own
metrics). A human comparing the two bracketed pairs sees the breach immediately. **Nothing in the
workflow performs that comparison, and nothing fails.** This is the second half of the M3 process
defect that `docs/milestones.md:390` names in item 11's own words — *"the instrument reports, but no
mechanism makes anyone read it"* (RATIFIED).

**Third layer, INFERRED and worth stating.** `calibration-nightly` is gated `if: github.event_name ==
'schedule' || github.event_name == 'workflow_dispatch'` (`ci.yml:187`, RATIFIED). GitHub fires
`schedule` only on the repository's **default branch**. The candidate lives on
`claude/civdemo-work-b1z2y4`, not on `main`. So on top of a gate that cannot fail, the job that
prints the line a human could read **has almost certainly never run against this tree at all**. This
lane did not query GitHub Actions run history to confirm it, so the layer is INFERRED, not MEASURED.

### 4.2 The battery

`Sim.Tests/Systems/CalibrationBatteryTests.cs` at `9f6c6ae`. RATIFIED:

```csharp
:426    [InlineData(1ul)]
:427    [InlineData(2ul)]
:428    public void Canonical_FedCorridors_AllInBand(ulong seed)
```

Two seeds. `ci.yml:179` names them in its own step title: *"Battery (CI members — 2 canonical + 2 dev
seeds, corridors.json bands)"* (RATIFIED).

MEASURED (this lane, candidate tree): seed 1 = 0.6073228123619765, seed 2 = 0.7164651669221055.
Both **inside** `[0.3685744951368359, 0.7421101248166698]`. The breaching seed is **3**, which this
theory does not run.

**The decisive measurement.** `dotnet test Sim.Tests --configuration Release --filter
CalibrationBatteryTests` on the candidate worktree:

```
Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 1 m 59 s
```

MEASURED (this lane, `9f6c6ae`, Release, 2026-09-19). **The battery is fully green while the corridor
it owns is 3.91 % outside its own recorded envelope.** The DOWN tooth at `:557-561` — which would
print *"is BELOW the measured quarantine window … a NEW defect"* — is not weak, not disabled and not
wrong. It simply never sees seed 3.

`corridors.json:75` (RATIFIED) already wrote the epitaph for this shape, about this same corridor and
these same two seeds: **"A FLOOR VALIDATED ON TWO SEEDS IS NOT A FLOOR."**

### 4.3 Dry-run: the shipped gate against the candidate's own metrics

MEASURED (this lane). The jq from `ci.yml:218-239` was extracted **verbatim** and run against each
tree's `corridors.json` and that tree's own 20-seed sweep. A **control** gate — identical except that
it consults `quarantine.window` when the quarantine is active — was run on the same inputs. Nothing
in the repository was modified; the control exists only in this lane's scratch script and is **not**
proposed here as an edit.

Density corridor in isolation (migration excluded, because migration's own window is breached on all
three trees and would mask the signal — §2.3):

| tree | shipped gate (`ci.yml:232-233`) | control gate (window consulted) |
|---|---|---|
| main `dbef61a` | **exit 0 — PASS** | exit 0 — no breach |
| pre-T4.21 `45046eb` | **exit 0 — PASS** | exit 0 — no breach |
| candidate `9f6c6ae` | **exit 0 — PASS** | **exit 1 — DENSITY BREACH** |

This is a clean single-variable isolate. The only row where the two gates disagree is the candidate,
and the only thing that differs between the two gates is whether `quarantine.window` is read.
**CONFIRMED: the breach exists, and the shipped gate cannot see it.**

### 4.4 The gap, stated exactly

MEASURED (this lane). For the reading `densityPerArableKm2 = 0.354157` at seed 3 on `9f6c6ae`:

| instrument | sees seed 3? | compares against window? | outcome |
|---|---|---|---|
| `calibration` battery (per push) | **no** — seeds 1, 2 only | yes, both directions, correctly | **7/7 green** |
| `calibration-nightly` gate (`jq -e`) | yes, all 20 seeds | **no** — short-circuits on `quarantine.active` | **exit 0** |
| `calibration-nightly` report (`jq -r`) | yes | prints window and measured range side by side | **a log line nothing reads and nothing fails on** |

**No automated instrument in this repository fails on this breach.** CONFIRMED. The claim "neither
instrument can see it" is correct as to *gating*; it slightly understates the nightly, which *prints*
the evidence without acting on it.

---

## 5. CORRECTIONS TO THE PRIOR LANE'S REPORT

`docs/t4.21-director-report.md:1288-1304`. Nothing in that file was edited by this lane.

| prior claim | this lane |
|---|---|
| seed 3 = 0.354157 vs floor 0.3685744951368359, 3.9 % below | **CONFIRMED**, exactly (0.35415668759623087, 3.9117757 %) |
| the nightly does not gate a quarantined corridor | **CONFIRMED**, `ci.yml:232-233`, and dry-run exit 0 |
| the battery's per-seed teeth run only seeds 1 and 2, both inside | **CONFIRMED**, `:426-428`, and 7/7 green measured |
| BEFORE reproduces the T4.19c window endpoints bit-for-bit | **CONFIRMED** independently on `45046eb` |
| ratio min 0.9421, mean 0.9691 | **CONFIRMED** (0.942095 / 0.969063) |
| move is 100 % numerator, arable bit-identical, on 18 of 20 | **CONFIRMED** (machine precision, ≤ 2.3e−16) |
| on seeds 9 and 14 arable moved (−2.47 %, +0.04 %) | **CONFIRMED** (−2.4692 %, +0.0401 %) |
| **"T4.21 moves every seed down"** | **REFUTED** — seed 16 moves **UP**, +1.1776 % (147,500 → 149,237 people). 19 down, 1 up. Minor; the sentence's own quoted aggregates are right |
| §12.4 table: candidate `[0.354157, 0.716465]`, mean 0.523290, 17/20 in band, 19/20 in window | **CONFIRMED**, every figure |

Under ADR-015 §6 the finding was SECONDARY and non-actionable. **It is now verified.** This lane
took no action on it beyond measuring, and none should be taken on this document alone either —
`corridors.json` is a Director instrument (§7).

---

## 6. WHAT THIS VERDICT DOES **NOT** ESTABLISH

Stated explicitly so the verdict is not over-read.

1. **It does not establish that the simulation is defective.** A 3.1 % mean population reduction from
   a ruled, pre-committed, down-only mechanism is not on its face a fault. The window has zero
   headroom by design (§1.2), so *any* downward motion breaches it. INFERRED.
2. **It does not establish that the window should move.** `corridors.json:74` is explicit that
   re-deriving the instrument *"needs its own ruling"*, and `:73` that the band *"is NOT re-tuned"*.
   RATIFIED. This lane re-pinned nothing.
3. **It does not attribute the population reduction to a specific T4.21 sub-packet.** The candidate
   was measured whole. A per-packet bisection across `t4.21-1` … `t4.21-5` was not run (§8.2).
4. **It does not establish that the breach is stable.** It is one seed, 3.9 % out, on one seed set.
   Whether seed 3 is an outlier or the leading edge of a distribution shift is not answered by 20
   seeds. INFERRED.
5. **It says nothing about CR-016, the disarm, or any other M4 blocker.** Out of lane.

---

## 7. RECOMMENDATION TO THE DIRECTOR

**PROPOSED.** Presented as options with their consequences; this lane picks no side and rules
nothing.

### 7.1 On B5 (the breach) — the question that must be answered first

The window's own text (`:73`) says the extremes are pinned *un-padded*; its lift condition (`:74`)
says re-deriving the instrument needs a ruling. So the Director's question is **not** "is 3.9 % a
lot" — it is:

> **DIRECTOR DECISION REQUIRED.** Is a zero-headroom envelope pinned on one arm the right instrument
> to carry across a packet that was *pre-committed to move this corridor down* (`t4.21-architecture.md:899`)?

Three shapes, PROPOSED:

| option | what it says | cost |
|---|---|---|
| **(a) Record the second deviation, as T4.19c's was recorded.** Append a new dated entry to this quarantine's `history` stating the T4.21 arm's measured envelope `[0.35415668759623087, 0.7164651669221055]`, mean 0.523290, 19/20 inside the prior window, attributed to the headroom cap and bounded migration, *without* moving the `window`. | Preserves the T4.19c record intact and keeps the breach visible as a breach. Follows the precedent the same block already set — *"a SECOND, SEQUENTIAL deviation on the same corridor, not an overturning of the first ruling"* (`:75`, RATIFIED) | The battery's DOWN tooth then stays armed against a floor the world no longer meets — it will fire the moment seed 3 joins the battery, which is arguably the point |
| **(b) Re-pin the window to the T4.21 arm's measured envelope**, under an explicit ruling that names T4.21 as the cause. | Restores 20/20-inside and keeps the instrument meaningful for the *next* packet | This is a change to the instrument and `:74` requires a ruling for it. It also re-arms the same zero-headroom trap for the next down-only mechanism. **The band `[0.15, 0.6]` must not move either way** — `:73` and the battery's immovability guard at `CalibrationBatteryTests.cs:550-553` both forbid it |
| **(c) Rule the breach accepted and leave everything as it is.** | Zero repository churn | Leaves a corridor whose recorded envelope is known-false and whose only teeth cannot reach the seed that falsifies it. This lane notes, INFERRED, that this is the shape `corridors.json:75` calls *"a floor validated on two seeds"* |

**This lane's own reading, offered as PROPOSED and nothing more:** (a) is the option most consistent
with what is already written in the file, because the file has already ruled once that a sequential
deviation is *recorded* rather than absorbed. But (a) and (b) are both rulings, and neither is this
lane's to take.

### 7.2 On known-open item 11 — and it matters whichever way B5 goes

**PROPOSED.** Item 11 is no longer hypothetical — B5 is its predicted failure mode, occurred, and now
verified. Note that under option (b) above the item-11 gap becomes *more* dangerous, not less: a
freshly re-pinned window that nothing gates is a number that will silently go stale again.

The minimal shape, PROPOSED (**not applied by this lane, and it touches `ci.yml`, which is
out of this lane's scope**): make `gated()` consult the window when a quarantine is active, i.e. the
control gate of §4.3. Measured consequence if it were applied today, MEASURED (this lane):

- density: main **exit 0**, pre-T4.21 **exit 0**, candidate **exit 1**;
- density **and** migration together: **exit 1 on all three trees**, because
  `migrationGrossPerDecade`'s window `[0.0009, 0.01]` is 0/20 on every tree (§2.3).

**So the item-11 fix cannot be applied to `ci.yml` without first disposing of the migration
window** — whose disposition text (`corridors.json:94`) already says the corridor is *"retained as a
RECORD … not as an acceptance gate"*, which reads as an argument for a per-corridor `gate:
true|false` flag rather than a blanket window check. **DIRECTOR DECISION REQUIRED**; this lane
flags the coupling and stops.

A second, cheaper and strictly additive shape, PROPOSED: **widen the battery's canonical theory to
include the extremal seeds of the recorded envelope** — seed 3 (the floor) and, if the envelope is
re-pinned, whichever seed defines it. Cost is roughly one extra minute of CI per added seed
(MEASURED: 7 tests / 1 m 59 s covers 2 canonical × 650 turns + 2 dev × 1000 turns). This would have
caught B5 on the push that caused it, needs no `ci.yml` change, and does not require the migration
question to be settled first. It is still a test change and therefore still a ruling.

### 7.3 On the merge

**Out of this lane's authority.** For the record, the two facts a merge ruling would want:
the breach is **real** and it is **caused by this candidate** (§3.1); and it is **3.9 % on one seed
of twenty, in the pre-committed direction, from a ruled mechanism** (§3.4). Both are true at once.
**DIRECTOR DECISION REQUIRED.**

---

## 8. PROVENANCE, REPRODUCTION, CAVEATS

### 8.1 Reproduce

```bash
git -C /home/user/civDemo worktree add --detach <path>/<sha> <sha>   # 9f6c6ae, 45046eb, dbef61a
cd <path>/<sha> && dotnet build Sim.Cli -c Release
dotnet run --project Sim.Cli -c Release --no-build -- \
    autoplay --seeds 20 --turns 650 --metrics sweep-<sha>.json
jq -r '.seeds[] | "\(.seed) \(.derived.densityPerArableKm2) \(.finalPopulation) \(.arableKm2)"' sweep-<sha>.json
# battery, candidate tree:
dotnet test Sim.Tests --configuration Release --filter CalibrationBatteryTests
```

Raw metrics and logs for all three sweeps are in this lane's scratch directory
(`…/scratchpad/wt/sweep-{9f6c6ae,45046eb,dbef61a}.json`). They are **ephemeral** — the numbers in §2
and §3 are the record, and the command above regenerates them deterministically.

### 8.2 Scope this lane did not exceed

No band, window, quarantine field, test, workflow, golden or constant was edited, on any tree. No
merge, no re-pin, no implementation. No per-packet bisection of the T4.21 diff. No M5 code. This
document is new under `docs/design/` and modifies no existing document.

### 8.3 Cleanliness proof

MEASURED (this lane), on each of the three worktrees after all runs:
`git status --porcelain` reports no modification to `Sim.Data/content/corridors.json`,
`Sim.Tests/Systems/CalibrationBatteryTests.cs` or `.github/workflows/ci.yml`. Each worktree is
detached, was created by this lane, and was used by no other agent.

### 8.4 Caveats a reader must carry

1. **Determinism assumption.** Comparability across trees rests on law 5 holding. Supporting
   evidence, MEASURED: the sweeps reproduce `docs/t4.19c-remeasurement.md`'s independently-recorded
   per-seed populations verbatim on two different arms, and reproduce the window's two 17-digit
   endpoints exactly. Not a determinism proof; strong circumstantial agreement.
2. **Twenty seeds.** The whole corridor, its window and this verdict rest on the same 20-seed set.
   A one-seed breach at 3.9 % is not a distributional claim. INFERRED.
3. **Hardware.** All runs on one container, .NET 10.0.112, Linux x64, Release. The prior lane's
   numbers were produced elsewhere and agree to six significant figures, which is itself evidence
   against a floating-point-environment confound.
4. **The 3.9 % is measured against a zero-headroom pin.** Read §1.2 before treating the percentage
   as a severity.
5. **`docs/current-state.md` is a router, not evidence** (`docs/gov-4-repository-freshness.md`). Every
   git fact in §2.1 was re-derived against the tree by this lane.
