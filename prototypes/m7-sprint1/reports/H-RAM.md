# Lane RAM — D-081 ramming realism in the naval TacticalResolver (M7 Phase C)

Date: 2026-11-05. Clone `/dev/shm/m7c/RAM`, branch `lane/RAM`, base `4914eb6` (dev HEAD: T1–T3, lane N, lane TR, the D-081
record). Everything is in `prototypes/m7-sprint1` (D-079 Q5 open: nothing touches `Sim.*`; no new project). Every
coefficient is a **PLACEHOLDER** with the cited rationale below; every statement is marked **measured** (run in this clone)
or **inferred** (argued, not measured).

Provenance: the brief said this lane was paused and resumed, but on start `/dev/shm/m7c/RAM` did not exist (the 20:39Z
container restart wiped `/dev/shm`) and no RAM checkpoint existed (no `RAM-wip.bundle`, no `RAM.bundle`, no
`wip/m7c-RAM.patch` in the scratchpad), so the clone was created fresh from `4914eb6` as the brief's fallback says. Run
**throttled** ("Throttle by 90%"): `nice -n 19`, single-node builds (`-m:1`), one full-suite run per mutant, a prioritized
mutant subset, a 3-seed bench.

Director ruling implemented (D-081, verbatim): *"ramming happens once. It can't back up and ram again, but it can steer away
and go ram another ship. It will need to build to speed and should be some distance away at least to build to that speed.
Make it realistic like how ramming would work irl"* — RM1–RM3 DECIDED, RM4 DELEGATED with the realism criterion, RM-R
readings (per pair; run-up from acceleration; cooldown superseded).

## 1. What now exists (summary)

| Rule | Mechanism (all measured by tests, §7) |
|---|---|
| RM1 once per pair | Each hull keeps the sorted ids of the hulls it has rammed (`RamState.Rammed`, battle state, in the state hash). A bow contact with a hull already in that list is never a ram again, whatever the speed: it is a collision (`DomainKinds.RamCollision`, no breach). Another hull may still ram that target (RM-R reading). |
| RM2 steer away, ram another | After any bow contact the rammer opens the range from the hull it struck (pivoting in place while it still touches it), then its ram target is the nearest **tracked** on-field hull of the directive's target formation it has **not** rammed, key (distance², id). With no such hull the ram planner yields and lane N's stand-off rule applies (other weapons at their preferred range). |
| RM3 run-up to ramming speed | A contact is a ram only at a closing speed ≥ the class's ramming speed **[round 2, §14 RAM2-V1/V2: the gate is now the NORMAL closing speed v_n, which only the rammer's own way supplies; the planner aims the run at the own way s_req = v_ram/\|sin a\|]**. Speed comes only from the existing acceleration integration. The planner presses in only if a straight full-power run from the current speed reaches ramming speed before contact (the exact step integration, `RamPhysics.RunUpGapMm`); otherwise it opens the range to the target's beam station at D_open = run-up from rest + full-speed turning-circle diameter, and turns in. |
| RM4 realism | Damage from the kinetic energy of the normal closing component (∝ v_n²); glancing blows deflect (below a glance angle, or with a normal component below ramming speed **[round 2: below the glance angle only; a normal component below ramming speed is no ram at all, §14]**); the rammer's bow takes a share of the energy on every contact; after a breach the beak is embedded and must be backed out, t = √(2·depth/a_back), during which the rammer is held at speed 0 with evasion 0 (exposed); people killed aboard the struck hull go through the personnel ledger. |
| Cooldown | Lane N's 30 s `RamCooldownMs` is **removed** (D-081 RM-R). The only timer left is the free time above, derived from the beak depth and the backing acceleration — a physical quantity, not a rate limit. |
| Board | ~~A Board order no longer rams.~~ **Withdrawn in verification round 1 (RAM-V7, §13):** a boarding approach rams as lane N allowed, now under the D-081 rules (closing ≥ ramming speed, once per pair); its slow contacts are the grapple, not collisions. "Board does not ram" stays proposal P2, not applied. |

Battles without a ram-capable hull are bit-identical to `4914eb6` (measured: sail, turret and amphibious `domain golden`
trace / state / result / personnel hashes unchanged, §8).

## 2. Cited values (PLACEHOLDER; sources read through web-search summaries in this session)

The primary texts were **not** consulted (en.wikipedia.org, eodg.atm.ox.ac.uk and cris.haifa.ac.il are outside this
session's allowed network domains); every figure below comes from search-result summaries of secondary pages and is to be checked against
the primary source before calibration.

| Value (galley, `DomainProfiles.Galley`) | Was | Now | Rationale |
|---|---|---|---|
| `AccelMmPerS2` | 500 | **110** | Olympias sea trials (Morrison, Coates & Rankov, *The Athenian Trireme*, 2nd ed. 2000; 1987–88 trial reports): about 7 knots (3.6 m/s) from a standing start in 32 s ≈ 0.11 m/s²; another account gives 6 knots in 30 s ≈ 0.10 m/s². Lane N's 0.5 m/s² is ~4.5× the trial figure. The run-up is integrated from this value. |
| `MaxSpeedMmPerS` | 4 600 | 4 600 | unchanged (lane N; Olympias sprinted to just under 9 knots ≈ 4.6 m/s). |
| `RamSpeedMmPerS` (new) | — | **1 542** (3 kn) | Itzhack, Cvikel & Me-Bar, "Damaging a trireme by ramming: The kinetics", *J. Archaeol. Sci.: Reports* 57 (2024) 104678: energy balance / three-point bending gives a minimum damaging impact speed of about 1.3–3 knots; the upper end is used. A summary of the older literature gives penetration at 4 kn at 60° and 8 kn at 30° (normal components 3.5 and 4 kn) — about 15–30 % above this value; the disagreement is recorded, not resolved. |
| `RamDamage` | 90 | 90 | lane N value, re-read as the damage of a **perpendicular** strike at `MaxSpeed` (reference of the v_n² law). |
| `RamCrewDamage` (new) | — | **6** people | the beak strikes at the waterline by the lowest oar bank (27 thalamites a side over ~22 m of oar bank in the reconstruction, ≈ 1.2 men/m); a smashed zone of 4–5 m. Inferred, not sourced. |
| `RamBowDamagePermille` (new) | — | **100** | the cast ram spreads the load into the bow timbers and wales; at Lissa (20 July 1866) the purpose-built ram ship *Erzherzog Ferdinand Max* sank *Re d'Italia* and was only slightly damaged in the bow (bow repaired after the war). |
| `RamLengthMm` (new) | — | **2 000** | the Athlit ram is about 2.26 m long; beak reach beyond the hull disc and full embedding depth taken as 2 m. |
| `DomainRules.RamGlanceAngle` (new) | — | **20°** | below it the beak skids along the planking at any speed. The cited 8 kn-at-30° datum shows 30° can still penetrate, so the floor lies below 30°; 20° is a placeholder. |
| `DomainRules.RamAttackAngle` (new) | — | **45°** | the planner starts a run only when the rammer lies ≥ 45° off the target's keel line (it manoeuvres for the beam, as oared tactics aimed at the side or quarter); a run is aborted below the glance angle. Placeholder. |
| `DomainRules.BackingAccelPermille` (new) | — | **500** | backing water (astern stroke) is less effective than the forward stroke; half is a placeholder. At Lissa the rammer reversed engines at once to free itself. |

Search results the summaries came from (not opened in full): Olympias trials — https://en.wikipedia.org/wiki/Olympias_(trireme),
https://csmonitor.com/1988/0825/dtri.html, https://navyhistory.au/the-athenian-trireme-design-and-history/2/,
https://www.ww2wrecks.com/?p=1504, https://eodg.atm.ox.ac.uk/user/dudhia/rowing/trireme/poster.pdf; ramming kinetics —
https://cris.haifa.ac.il/en/publications/damaging-a-trireme-by-ramming-the-kinetics/,
https://cris.iucc.ac.il/en/publications/damaging-a-trireme-by-ramming-the-kinetics/,
https://en.wikipedia.org/wiki/Oared_vessel_tactics, https://en.wikipedia.org/wiki/Naval_ram,
https://bmcr.brynmawr.edu/?p=19773; Lissa — https://en.wikipedia.org/wiki/SMS_Erzherzog_Ferdinand_Max_(1865),
https://en.wikipedia.org/wiki/Italian_ironclad_Re_d%27Italia, https://Www.wikipedia.org/wiki/Battle_of_Lissa_(1866).

Galley figures that follow (measured by `RunUpAndFreeTimeFollowTheKinematics`): ramming speed is reached after 8 steps of
220 mm/s (1 760 mm/s); the run-up gap from rest is **14 321 mm** (12 320 mm of travel + the 2 m beak + 1); D_open =
14 321 + 52 724 (turning circle at 4.6 m/s and 10°/s) = 67 045 mm; backing a fully embedded beak out takes **8 529 ms**,
a ramming-speed blow (292 mm embedded) 3 259 ms.

## 3. Equations (`Proto.M7/Tactical/RamPhysics.cs`, integer only)

Rammer a, target b; v_a, v_b = velocity vectors from the WAY speed of the last movement step (before the separation step
truncates the move: the speed the hull had when it struck) along the heading; w = v_a − v_b.

- closing speed v_c = w·(p_b − p_a)/|p_b − p_a| (floor); normal component v_n = |w × k_b| (k_b = target keel unit vector,
  Q30, floor); tangential v_t = |w·k_b|; impact angle θ = atan2(v_n, v_t) ∈ [0°, 90°].
- **Contact** (Fire phase): a ram-capable hull under **Attack** (round 1: or **Board**), not backing its beak out, whose beak
  reaches a hull of the directive's target formation (surface gap ≤ `RamLengthMm`) inside ±30° of its bow (nearest by
  (distance², id)), with v_c > 0 — and (round 1, RAM-V1 d) the two hulls came together in this step: centre distance at the
  end of the step < at its start. A hull pressed against another (its move blocked) has a commanded way but no impact.
- **Ram** iff v_c ≥ `RamSpeedMmPerS` **and** the pair has not been rammed (RM1); otherwise a **collision**. **[Revised in round 2
  (§14, RAM2-V1/V2): ram iff v_n ≥ `RamSpeedMmPerS`; v_n is computed from the rammer's own velocity (= \|w × k_b\| exactly).]**
- **Breach** iff ram, θ ≥ `RamGlanceAngle` and v_n ≥ `RamSpeedMmPerS` **[round 2: iff ram and θ ≥ `RamGlanceAngle`]**; otherwise the ram **glances** (it still uses up the
  pair — reading RAM-R1, §4).
- Energy scaling: E(x) = ⌊x · v_n² / v_ref²⌋, v_ref = the rammer class's `MaxSpeedMmPerS` (Int128, exact).
- Breach: target hull −max(1, E(RamDamage)); people killed aboard the target E(RamCrewDamage), split crew / embarked by the
  existing DomainHit rule, every person through a ledger transfer; rammer speed 0; beak depth d = max(1, min(L, E(L)))
  with L = `RamLengthMm`; locked until t + ⌈√(2 d · 10⁶ / a_back)⌉ ms, a_back = Accel · `BackingAccelPermille`/1000.
- Glance: no hull damage; rammer speed max(0, way − v_n) (the normal way is lost, the tangential kept).
- Collision: no hull damage to the target; the rammer loses its way (speed 0) **[round 2, §14: the normal component of its way —
  speed max(0, way − v_n), the glance's rule]**; the pair is not used up. Rationale (round 1,
  RAM-V6): below the minimum damaging impact speed the planking is not broken (Itzhack, Cvikel & Me-Bar 2024 — the source of
  `RamSpeedMmPerS`); the arrest of the rammer's way is **inferred, not sourced** (the hulls do not interpenetrate and no hull
  displacement is modelled, P6). A full-speed contact on a hull ALREADY rammed leaves that hull undamaged — strict RM1,
  reading **RAM-R5**, flagged for the Director (P9). Under Board a contact that is not a ram is the grapple: no event.
- Bow (every ram, glance or collision): rammer hull −⌊RamDamage · `RamBowDamagePermille` · v_n² / (1000 v_ref²)⌋ through
  DomainHit effect kind 3 (vessel hull only; round 1, RAM-V10: credited to NO source — self-inflicted; the lane credited it
  to the struck hull's formation as DamageDealt and set its activity flag).
- While locked (t < LockedUntilMs): the hull is held (heading kept, speed 0) whatever its order, makes no new strike, and
  its evasion term in every hit roll against it is 0.

Run-up (RM3): `RunUpGapMm(v0)` = the smallest gap such that a straight full-power run from way speed v0 (speed +=
⌊a·dt/1000⌋ per step, capped at the crew-limited max; displacement ⌊v·dt/1000⌋) is still out of beak reach at the end of
every step before the first step at which speed ≥ ramming speed: X_{k−1} + L + 1. It is d = (v_ram² − v0²)/2a integrated
in the simulation's own steps (galley: 14 321 mm vs the continuous 10 808 mm + 2 000 mm beak). Turn allowance: turning
diameter 2v/ω = v · 65 536 · 113 / (355 · TurnRate) (π = 355/113).

## 4. The ram planner (`TacticalBattle.Ram.cs`, `RamIntent`), and the readings it rests on

Per step, for a ram-capable hull under Attack on a naval formation:
1. Locked → held (speed 0).
2. Target T = nearest tracked, on-field, un-rammed hull of the formation, key (distance², id) (`RamPhysics.ArgMinTarget`).
   None, or the crew-limited max speed is below ramming speed → the planner yields to lane N's stand-off rule.
3. Mode **Open** from a hull other than T (the one just struck, or a fouling hull): steer directly away until the gap ≥
   D_open; pivot in place (speed 0) while that hull lies forward of the beam within beak reach + one step at a quarter of
   max speed (one bank rows, the other backs — no way is made into the hull).
4. Mode **Open** toward T: steer for T's beam station (the beam normal on the rammer's side, at r_a + r_b + D_open + one
   step of way); switch to **Close** when gap ≥ D_open and the approach angle φ (rammer off T's keel line) ≥ 45°.
5. **Fouled**: in Close, if the last move was cut short by a hull forward of the beam that the beak is not striking (a
   friend, another enemy, or T outside ±30°), open from it.
6. **Close**: abort to Open (toward T) when φ < glance angle or the gap < `RunUpGapMm(own closing speed)` **[round 2, §14: the run-up
   to the own way s_req = ⌈v_ram / \|sin a\|⌉ in the target's frame; ∞ when the range cannot close]**; otherwise the
   attack run: head at T at full speed.
7. Every ram, glance or collision sets Open with the struck hull as reference.
8. (Round 1, §13) In Open mode, before steps 3–4, the **clearing rule**: a hull with any hull inside its pivot reach first
   gets clear of all of them (bisector of the widest free sector; pivot while any lies forward of the beam). A fouled hull
   (step 5) clears the same way. The Close-mode run-up check of step 6 is made in the target's frame (its motion along the
   line of sight counts).

Readings / choices made here (flagged for confirmation; none reopens D-081):
- **RAM-R1** — a glancing blow at ramming speed **is** the rammer's one ram on that hull (the ram was made; it deflected).
  Alternative: only a breach uses up the pair. Proposal P1.
- **RAM-R2** — "nearest tracked enemy hull it has NOT rammed" is read inside the directive's target formation (the order
  names a formation). With none left the hull keeps the formation under its other weapons; the AI or player re-targets.
- **RAM-R3** — the ram PLANNER is the Attack directive's. ~~Board no longer rams (P2).~~ Round 1 (RAM-V7): withdrawn — a
  boarding approach rams under the D-081 rules as lane N allowed; P2 stays a proposal, not applied.
- **RAM-R4** — the ram threshold is on the closing speed (target motion counts: a hull steaming into the blow adds to it, a
  fleeing hull subtracts — a stern chase on a hull nearly as fast cannot ram). The breach threshold is on the normal
  component. **[Withdrawn in round 2 (§14, RAM2-V1/V2): it contradicted the decided RM3 ("the ship must build up to that
  speed") and split gate and damage over two speeds. Replaced by reading RAM-R6: the ram threshold is the closing velocity
  component normal to the target's side, the speed the damage uses; the target's own motion lies along its keel and enters
  only through the impact angle (tangential slide).]**

## 5. Shared-file edits (delimited `[RAM]`)

| File | Edit |
|---|---|
| `DomainTypes.cs` | `VesselClass`: `RamCooldownMs` removed; `RamSpeedMmPerS`, `RamCrewDamage`, `RamBowDamagePermille`, `RamLengthMm` + validation. `DomainRules`: `RamGlanceAngle`, `RamAttackAngle`, `BackingAccelPermille` + validation. |
| `DomainProfiles.cs` | Galley: accel 110, the four ram values, cited doc comment. |
| `TacticalBattle.Domain.cs` | `DomainKinds.RamCollision = 125`; DomainHit kind 3 documented; `DEnt.RamReadyMs` removed; `RamInit()` call; dispatch of `RamCollision`; hash slot `RamReadyMs` → `RamHashSlot` (LockedUntilMs, long.MinValue when never rammed); `AppendRamState(h)` (empty without ram ships). 14 lines. |
| `TacticalBattle.DomainCombat.cs` | the ram trigger block → `RamContacts(t, i, d, f)`; the old `RamStrike` removed; evasion → `VesselEvasion`. |
| `TacticalBattle.DomainMove.cs` | lock check at the top of `VesselIntent`; `RamIntent` call in the Attack branch (replacing the straight-at-target line); `RamRecordWay(i, sp)`. 5 lines. |
| `Proto.M7.Bench/DomainBench.cs` | `domain golden` prints breach / glance / collision counts. |
| tests | `NavalTests.RamDamageScales…` re-pinned; `VerificationRound2Tests.TheRamCooldownSpacesAHullsRams` removed (§8). |

New files: `Proto.M7/Tactical/RamPhysics.cs`, `Proto.M7/Tactical/TacticalBattle.Ram.cs`,
`Proto.M7.Tests/MultiDomain/RamTests.cs`.

## 6. Determinism and state

Integer only; no LINQ, no unordered containers in `Proto.M7` (determinism scan CLEAN, 67 files — measured). Rammed lists
are sorted `long[]` (binary search); target choice and contact choice by (distance², id), ascending scans. Ram state
(LockedUntil in lane N's old `RamReadyMs` slot; Mode, Ref, Target, Way, Rammed for every ram-capable hull) is in the state
hash. The galley battle replays exactly from its order log (trace, state, personnel — measured). No new serialized row type
(the ram state is hashed, not serialized), so no POPULATED-table round-trip test is owed. No new order kind or delivery
semantic; the lock release is pinned turn-exactly with ±1-step neighbours (§7). **[Corrected in round 2 (§14, RAM2-V7): the
beak lock IS an order-effect semantic — an order delivered during the lock is delivered at its own instant, but the hull
executes it only from the first step instant at or after the lock's end; now pinned turn-exactly with ±1 neighbours.]**

## 7. Tests (measured, `M7Proto.slnx` — the only solution whose projects were touched)

| Build | Before (`4914eb6`) | After (lane RAM) |
|---|---|---|
| Release | 581 pass / 0 fail / 0 skip (14 s) | **597 / 0 / 0** (10–13 s) |
| Debug | 581 / 0 / 0 (28 s) | **597 / 0 / 0** (35 s) |

Before = `4914eb6` built and tested in this session (Release in the lane clone before any edit; Debug in a separate clone
`/dev/shm/m7c/RAM-base` at `4914eb6`). 597 = 581 − 1 removed (`TheRamCooldownSpacesAHullsRams`, §8) + 17 new
(`RamTests`). Build 0 warnings / 0 errors (Release and Debug). Determinism scan (`scripts/check-proto-determinism.sh`):
CLEAN, 67 files. `M7Econ.slnx` / `M7DetMath.slnx` reference neither `Proto.M7` nor its tests (their projects reference
`Sim.Core` / `Sim.Data` / `Proto.DetMath` — read in the `.csproj` files) and were not re-run.

New tests (`Proto.M7.Tests/MultiDomain/RamTests.cs`; every battle test checks the personnel ledger at every completed
instant — `Watch` / `RunChecked`):

| Rule | Test | What it pins |
|---|---|---|
| RM3/RM4 pure | `ClassificationIsExactAtTheRammingSpeedBoundary` | v_c = v_n = s exactly for a perpendicular strike; s = v_ram − 1 → collision, s = v_ram → ram + breach; the target's own motion adds to / subtracts from the closing speed (exact) **[round 2: … but never makes a ram — v_n = 0 bow-on and in a stern chase]** |
| RM4 pure | `GlancingBlowsDeflectAndThePerpendicularBlowIsTheStrongest` | damage strictly grows from 25° to 90°, = RamDamage at 90° and MaxSpeed; 15° skids; the angle floor on its own (TEST INPUT 40°: 30° glances with v_n ≥ v_ram, 50° breaches); a glancing blow needs more speed (30°: 3 000 mm/s glances, 3 100 breaches) |
| RM4 pure | `EveryRamEffectScalesWithTheKineticEnergyOfTheNormalSpeed` | E(x) and the bow share equal the exact BigInteger v² formula over 0..2·vmax |
| RM3 pure | `RunUpAndFreeTimeFollowTheKinematics` | run-up gap 14 321 mm = an independent reference loop, within one step of the continuous v²/2a; 0 at speed; never below ramming speed; free time 8 529 ms exact (t² bracket) |
| RM2 pure | `RamTargetChoiceIsTieDenseLowestUnrammedId` | 12 candidates at equal distance, 24 input orders: lowest id; lowest UN-rammed id; 1 mm² nearer beats any id |
| RM2 battle | `TheBattlePlannerPicksTheLowestIdAmongEquidistantHulls` | five hulls at exactly 100 m (Pythagorean lattice), scrambled ids, input order forward and reversed: target = lowest id |
| RM3 boundary | `ARunUpJustShortOfTheRequiredDistanceDoesNotRamJustEnoughDoes` | from rest on a perpendicular line: gap g* (14 321 mm) → straight run (heading 0 throughout), ram at the first contact, closing 1 760 mm/s ≥ 1 542; g* − 1 mm → no ram and no contact at all through the direct run's time + 2 steps; it turns away and opens the range |
| RM3 manoeuvre | `TheRamDirectiveOpensTheRangeAndReachesRammingSpeed` | from rest 5 m off a hull's beam: never a low-speed contact; opens to ≥ D_open; strikes (breach) at closing speed ≥ ramming speed |
| RM1 | `AShipRamsAGivenShipOnlyOnceEvenAtFullRammingSpeedAgain` | built explicitly: rams 2000, backs out, opens to ≥ D_open, runs at 2001 (beyond 2000) and strikes 2000 again at closing speed ≥ ramming speed (measured 4 164 mm/s in the probe) — a collision, no second Ram, no further hull damage to 2000; it was running at 2001 at the re-contact |
| RM2 | `AfterARamTheShipSteersAwayAndRamsAnotherShip` | Ram(2000) then Ram(2001); the gap to 2000 reached D_open in between; the target switched to 2001 |
| RM-R | `ADifferentShipMayRamAHullAlreadyRammed` | rammers 1000 and 3000 both ram 2000 |
| RM4 effects | `AFullSpeedRamHolesTheTargetDamagesTheBowAndHoldsTheRammerWhileItBacksOut` | at 4 600 mm/s perpendicular: target hull −90, crew −6 through the ledger (Killed balance 6), rammer bow −9; locked until t + 8 529; every step instant before it: same position and heading, speed 0, evasion 0; release pinned at t + 10 000 with the −1-step neighbour still held; at release it pivots (speed 0, heading changed), evasion back to 25; no contact with the hull while turning away |
| RM4 | `ARammerBackingItsBeakOutMakesNoStrike` | the struck hull steers into the locked rammer: no Ram / collision of the rammer's until its beak is free |
| state | `TheBeakLockIsPartOfTheStateHash` | two duels differing only in backing acceleration (500 vs 499 ‰): identical trace, lock 8 529 vs 8 607 ms, state hashes differ at the impact |
| planner | `AFouledRammerSheersOffAndStillRams` | a friendly hulk on the line to the target: the rammer is never held bow-on against it for more than one step and still rams the target at ramming speed |
| all | `GalleyLineBattleObeysTheRamRulesInEveryContactAndReplaysExactly` | AI-vs-AI galley 5v5 (seed 42): no pair twice; every ram at closing ≥ ramming speed; every collision below it or on a rammed pair; each breach carries exactly the energy-scaled hull and crew damage, each glance none; both occur; exact replay from the order log; trace golden |
| ~~RAM-R3~~ | ~~`ABoardingOrderNeverRams`~~ | REMOVED in round 1 with the withdrawn reading (§13); replaced by `RamVerificationRound1Tests.ABoardingApproachAtRammingSpeedRamsOnceThenGrapples` |


## 8. Moved pins and goldens (attribution measured)

| Pin | Change | Cause (measured) |
|---|---|---|
| `VerificationRound2Tests.TheRamCooldownSpacesAHullsRams` (N-R2-07) | **removed** (pointer comment left in the file) | it pinned lane N's 30 s `RamCooldownMs`, which D-081 RM-R supersedes and this lane removes; the field no longer exists. Replaced by the RM1 / lock / free-time tests (§7). |
| `NavalTests.RamDamageScalesWithClosingSpeedAndOnlyAtRammingSpeed` | re-pinned: damage = max(1, ⌊90·v_n²/4 600²⌋) exactly from the event's v_n (breach) or no hull damage (glance); rammer stopped on a breach; Board part unchanged | lane N asserted damage ∈ [Ram/2, Ram] from v/vmax at speed ≥ max/2 — the rule D-081 replaces |
| galley 5v5 `domain golden` (bench output, never pinned in a test before) | trace `D46E355C…223B` (17 pulses, SideEliminated side 1) → `D3B4D73A…7E52` (6 pulses, SideEliminated side 1); now pinned in `RamTests.GalleyTrace` | decomposed: `4914eb6` + ONLY the galley acceleration 500 → 110 gives a third trace `648A1092…970C` (18 pulses, SideEliminated side 0) — so both the cited acceleration and the D-081 rules move it; they are not separable further (the rules are one mechanism) |
| sail line, turret, amphibious `domain golden` | **unchanged** (trace, state, result, personnel) | no ram-capable hull: the ram state hashes nothing and the old `RamReadyMs` slot holds the same long.MinValue (measured byte-identical) |
| amphibious test golden (`AmphibTrace/State/Personnel`), sail test golden, Slice D goldens, FixedTrig table | unchanged | the full suites pass with them |

Galley battle after the change (seed 42, measured `domain golden`): 7 breaches, 2 glances, 0 collisions, closing speeds
2 145–6 813 mm/s (two galleys converging add their CLOSING speeds), F1 5 = 4 + 1 + 0, F2 5 = 3 + 2 + 0 (3 escaped routing).
Before: the cooldown rule, 17 pulses. **[Corrected in verification round 1, RAM-V2]** The lane wrote here that the shorter
battle was "the measured consequence of v_n²-scaled damage on converging hulls (a crossing ram at 6.8 m/s does 197 hull
points)". That is FALSE: damage uses v_n = |(v_a − v_b) × k_b|, and the target's own velocity lies along its keel k_b, so
v_n ≤ the rammer's way (+1 mm/s of per-axis rounding) ≤ its MaxSpeed and a breach never does more than `RamDamage` (90).
The two 6 813 mm/s contacts of seed 42 were GLANCES (A = 1) and did no hull damage; the largest ram hull damage measured is
89 (seed 42 and seeds 1–20, before and after round 1). The cause of the shorter battle is NOT attributed (the accel-only
decomposition above stands). The seed-42 battle moved again in round 1 (§13).


## 9. Mutation testing (measured; scratch copy, full Release suite per mutant, bound = 10 × the clean time)

Harness: `/dev/shm/m7c/RAM-mut-harness.py` (not committed) mutates only the scratch copy `/dev/shm/m7c/RAM-mut`, one exact
string replacement per mutant (each pattern checked to match exactly once), restores the file from the lane clone after
every run and byte-compares it. One run = incremental Release build + the full `M7Proto.slnx` Release suite. Clean time of
that run = **21 s** → bound **210 s**; no run reached it (no hang; runs took 16–23 s). Prioritized subset (throttle): one
mutant per rule and per planner mechanism. Every kill below has at least one semantic (non-golden) killer; the galley test
in brackets combines semantic invariants with the trace golden.

| Mutant | Result | Semantic killer(s) [galley test also failed] |
|---|---|---|
| M01 RM1 pair check dropped (a hull may re-ram) | KILLED | `AShipRamsAGivenShipOnlyOnceEvenAtFullRammingSpeedAgain` |
| M02 RM2 target choice ignores the rammed list | KILLED | `AfterARamTheShipSteersAwayAndRamsAnotherShip`, `AShipRamsAGivenShipOnlyOnce…` [galley] |
| M03 RM2 tie-break to the highest id | KILLED | `RamTargetChoiceIsTieDenseLowestUnrammedId`, `TheBattlePlannerPicksTheLowestIdAmongEquidistantHulls` |
| M04 RM3 threshold strict (v_c > v_ram) | KILLED | `ClassificationIsExactAtTheRammingSpeedBoundary` |
| M05 RM3 planner presses in whatever the run-up | KILLED | `ARunUpJustShortOfTheRequiredDistance…`, `TheRamDirectiveOpensTheRange…` [galley] |
| M06 RM3 run-up gap off by one | KILLED | `ARunUpJustShortOfTheRequiredDistance…`, `RunUpAndFreeTimeFollowTheKinematics` [galley] |
| M07 RM4 damage linear in v_n (not energy) | KILLED | `EveryRamEffectScalesWithTheKineticEnergyOfTheNormalSpeed` [galley] |
| M08 RM4 glance-angle floor removed | KILLED | `GlancingBlowsDeflectAndThePerpendicularBlowIsTheStrongest` [galley] |
| M09 RM4 breach ignores the normal speed | KILLED | `GlancingBlowsDeflect…` |
| M10 RM4 no bow damage | KILLED | `AFullSpeedRamHolesTheTarget…` [galley] |
| M11 RM4 no beak lock (free time 0) | KILLED | `AFullSpeedRamHolesTheTarget…` [galley] |
| M12 RM4 locked rammer keeps its evasion | KILLED | `AFullSpeedRamHolesTheTarget…` [galley] |
| M13 RM4 no crew casualties from a breach | KILLED | `AFullSpeedRamHolesTheTarget…` [galley] |
| M14 pivot rule off (makes way into the hull) | KILLED | `AFullSpeedRamHolesTheTarget…`, `AShipRamsAGivenShipOnlyOnce…` [galley] |
| M15 fouling rule off | KILLED | `AFouledRammerSheersOffAndStillRams` [galley] |
| M16 Board directive rams again | KILLED | `ABoardingOrderNeverRams`, `NavalTests.RamDamageScales…` |
| M17 beam station on the far side | KILLED | `TheRamDirectiveOpensTheRange…`, `ARunUpJustShortOfTheRequiredDistance…` [galley] |
| M18 D_open without the turning circle | KILLED | `TheRamDirectiveOpensTheRange…` [galley] |
| M19 contacts processed while the beak is embedded | first run: killed by the galley test ONLY (not counted); after adding `ARammerBackingItsBeakOutMakesNoStrike`: **KILLED** by it [galley] |
| M20 rammed list left out of the state hash | **SURVIVED** (597/0) — round 1: KILLED (§13) | — see below |
| M21 beak lock left out of the state hash | KILLED | `TheBeakLockIsPartOfTheStateHash` |

Totals: 21 mutants, 20 killed by semantic tests, **1 survived**, 0 hung, 0 build failures. M20 is a real test gap: no
scenario was found that produces two states differing only in the rammed list (every way of using up a pair also changes
an event or a speed), so the inclusion of the rammed list in the state hash is shown by code inspection and by exact replay
(which does not need the hash to include it), not by a test that fails without it (§11). Teeth and aim (ADR-015 §7.2):
each killing test asserts a property D-081 or this report states (once per pair; nearest un-rammed hull with an id
tie-break; ramming speed exact; no low-speed pressing; v² energy; glance rules; bow damage; the free time and its
exposure; crew losses through the ledger; no Board ram; a lock that is state). Raw output: `/dev/shm/m7c/RAM-mutants.out`
and `RAM-mutants-r2.out` (not committed).


## 10. Performance (`Proto.M7.Bench domain 3`, Release, `nice -n 19`, interleaved A/B, 3 pairs; Stopwatch, reporting only)

Baseline binaries = `4914eb6` (copied before any edit); lane binaries = the delivered tree. Load average 1.2–2.0 on the
shared 4-CPU machine.

| battle (3 seeds) | mean pulses base → lane | mean events base → lane | total ms base (3 runs) | total ms lane (3 runs) | median ms/pulse base | median ms/pulse lane |
|---|---|---|---|---|---|---|
| galleys 5v5 (oar, ram) | 18.7 → **12.7** | 2 712 → **1 808** | 325 / 358 / 297 | 316 / 425 / 311 | 4.15 / 7.87 / 3.42 | 9.56 / 11.62 / 9.55 |
| amphibious combined arms | 18.0 → 18.0 | 2 433 → 2 433 | 690 / 675 / 686 | 664 / 574 / 707 | 13.38 / 12.13 / 12.50 | 11.87 / 10.37 / 13.30 |
| sail line 5v5 | 49.7 → 49.7 | 4 903 → 4 903 | 261 / 282 / 272 | 268 / 280 / 240 | — | — |
| turret 5v5 | 10.3 → 10.3 | 1 788 → 1 788 | 31 / 29 / 30 | 38 / 28 / 35 | — | — |

Galley battles are shorter (fewer pulses and events, identical totals within noise) but about twice as expensive per pulse
(median 3.4–7.9 → 9.6–11.6 ms). Not attributed by profiling; the per-step planner work per ram hull (target choice, approach
angle and bearing by `Atan2`, run-up loop, beak-contact and fouling scans) is the inferred cause. The amphibious battle and
the sail / turret lines show no change beyond noise (identical pulses and events; they have no ram ship).


## 11. Limitations (honest list)

- **Sources.** Every cited figure was read through web-search summaries of secondary pages; the primary texts (Morrison,
  Coates & Rankov; the Olympias trial reports; Itzhack, Cvikel & Me-Bar 2024; accounts of Lissa) were not consulted, and the
  two speed sources disagree by a factor of up to ~1.3 at the normal component. All values stay PLACEHOLDERS.
- **Only the galley rams.** `NavalDesign.Derive` gives a ram only to the oared warship (capability rule unchanged, D-081
  §3). The steam rams of Lissa are cited as a mechanism precedent, not modelled (P3).
- **Hulls are discs.** The strike geometry uses the target's keel line for the side normal and the centre line for the
  closing speed **[round 2: the centre line now only decides that the hulls close (v_c &gt; 0); the ram gate is v_n]**; bow-to-bow, stern and quarter strikes are distinguished only through the impact angle. Bow-on clashes with
  reinforced bows (Syracusans, Thucydides 7.36) are not modelled.
- **Energy from the rammer only.** The target is treated as rigid (energy ½ m_a v_n², m_a inside the class coefficients);
  no displacement of either hull, no reduced mass (P6). ~~Converging hulls make v_n exceed MaxSpeed …~~ **[Corrected in round
  1, RAM-V2]**: v_n ≤ the rammer's way ≤ MaxSpeed (the target's velocity lies along its keel), so breach damage ≤ `RamDamage`;
  the "197 hull points" was the closing speed v_c of a glance, not damage. Measured maximum: 89.
- **No reverse kinematics.** Backing water is modelled as a hold of the computed free time, then a pivot; the hull does not
  move astern.
- **Ram sticking in a sinking target is NOT implemented.** A realistic mechanism needs a flooding / buoyancy model and a
  coefficient for what a sinking wreck does to the rammer's bow (dragged down, beak torn away); none could be sourced or
  tested convincingly here (P5). A deeper (more energetic) embedding does lengthen the free time.
- **The planner is a local heuristic.** No lead on a moving target, no planned path around obstacles (the fouling rule
  only sheers off), turning arcs can carry a run off the line (observed: it can miss a hull it was not aiming at). Its
  run-up feasibility is exact only for an aligned straight run; with a turn, D_open's turning-circle term covers it
  conservatively (inferred, consistent with every planner test).
- **Glance after-speed** (way − v_n) is a simplification of the tangential sliding.
- **State-hash teeth** for the rammed list: ~~missing (M20 survived, §9)~~ round 1: a reflection test kills M20 (§13); the
  lock slot has teeth (M21).
- **Mutation pass is a prioritized subset** (21 mutants); untested by mutation: the approach-angle hysteresis value, the
  glance after-speed rule, the beak reach in contact detection, the closing-speed sign check.
- **Bench**: 3 seeds, 3 interleaved pairs; the per-pulse galley cost increase is measured, not profiled.
- **AutoResolver untouched** (lane P): it still refuses naval battles; no parity statement is made.
- **AI behaviour changed**: AI-vs-AI galley battles are now much shorter (18.7 → 12.7 mean pulses; seed 42: 17 → 6) and
  every ram is set up by the planner; no AI-side ram doctrine (formations, kyklos, diekplous) exists.

## 12. Proposals for the Director (not decided here)

1. **P1 — glance uses up the pair (reading RAM-R1).** Applied: a ram at ramming speed that glances is the rammer's one ram
   on that hull ("ramming happens once"). Alternative: only a breach uses it up, so a captain whose blow glanced may come
   round and try again. Rationale for the applied reading: the blow was struck; the Director's "it can't back up and ram
   again" reads naturally on any ram.
2. **P2 — Board does not ram (reading RAM-R3). NOT APPLIED (withdrawn from the code in round 1, RAM-V7).** Lane N let a galley ram during a boarding approach at ≥ max/2. Under RM1
   that would spend the one ram on a hull as a side effect of a boarding order. Proposal: confirm; if ram-then-board is
   wanted, make it an explicit Attack-then-Board sequence (two actions, as the action budget already allows).
3. **P3 — steam rams by capability.** Lissa 1866 shows the ram returning with steam and armour. Proposal: a capability
   flag (e.g. `NavalCapability.RamBow`) that gives engine warships a ram with their own cited ramming speed / damage / bow
   share; no date or era input (law 4).
4. **P4 — recalibrate galley manoeuvre from the trials.** Applied here only to acceleration (in scope: the run-up). The
   trials also report a 180° turn in about one minute within ~2.5 ship lengths (~3°/s against lane N's 10°/s placeholder);
   proposal: recalibrate the turn rate (it changes D_open and every galley battle, so it is left to the Director).
5. **P5 — ram sticking.** Implement only with a flooding model: a wreck sinking while the beak is still embedded either
   tears the beak away (the rammer loses its ram) or floods the rammer's bow; needs a sourced coefficient.
6. **P6 — displacement per hull class.** Add displacement so the impact energy uses the reduced mass and a heavy target
   absorbs more of it; it would also let ships of different size collide realistically.
7. **P7 — rower fatigue.** A sprint to ramming speed is short-lived for an oared crew; a fatigue state reducing maximum speed
   after sustained sprints would make repeated run-ups costly (a mechanism, not a cooldown).
8. **P8 — readings RAM-R2 and RAM-R4** (target inside the ordered formation; closing speed relative, so a stern chase on a
   nearly-as-fast hull cannot ram) are applied and reported for confirmation. **[Round 2: RAM-R4 withdrawn, replaced by
   RAM-R6 (§14); RAM-R2 unchanged.]**


## 13. Verification round 1 (2026-11-05)

Fix agent, round 1. Clone `/dev/shm/m7c/RAM` (it survived; branch `lane/RAM` at `c2b5f4a`, no checkpoint restore needed).
Throttled as before (`nice -n 19`, `-m:1`, one suite run per mutant). Every finding was REPRODUCED against `c2b5f4a`
before any change: the verifier's probe file was copied into the clone as an uncommitted file and its output on `c2b5f4a`
was byte-identical to the verifier's (probes A, B, D, E, F, G, H, I); own scenario probes (S1–S13) and a pristine
`c2b5f4a` copy were used for the rest. All probe files were deleted before the final validation and never committed.

### 13.1 Verdicts

| ID | Sev. | Verdict | Reproduction on `c2b5f4a` (measured) | Change | Semantic test(s) | Mutants |
|---|---|---|---|---|---|---|
| RAM-V1 | major | **REPRODUCED** (all four causes) | Probe H identical (1003, 2003, 2004 still 1 004 000–1 072 000 ms; 33 COLL 2003→1003, C = 97). S1, rammer touching its target's stern: still 178 steps, no ram in 360 s (cause c). S2, rammer touching a friend at the bow and another at the port quarter: 172 still steps, no ram (cause a). S3, struck hull pressing on the idle rammer: 33 collisions, one per step from the release to 120 s, with neither hull moving (cause d). Seeds 1–20: longest stillness 255 steps (seed 2, hull 1004 at 768 000 ms — a second deadlock), 15–19 in eight other seeds; 38 same-pair collisions on consecutive steps. | (a, c) **clearing rule**; (d) **closure gate**; (b) unchanged — lane N's stand-off rule for a hull with nothing left to ram (it holds still in archery range) no longer locks anyone once (a, c, d) are fixed | `ARammerTouchingItsTargetsSternGetsClearAndStillRams`, `ARammerBoxedBowAndQuarterEscapesBetweenThemAndStillRams`, `ARammerClearingAHullAheadPivotsAndNeverClosesOnIt`, `AHullPressingOnAnIdleRammersBowMakesNoImpact`, `NoGalleyIsDeadlockedInAiBattles` | N1–N4 killed |
| RAM-V2 | major | **REPRODUCED** | Code: v_n = \|(v_a − v_b) × k_b\| and v_b ∥ k_b. Probe I: both 6 813 mm/s contacts of seed 42 are glances (A = 1), no hull damage; max ram hull damage 89 (seed 42; seeds 1–20) | Report corrected (§8, §11, struck-through and marked); the lane's open question on recalibrating `RamDamage` for >MaxSpeed blows loses its premise | none owed (a false claim, not code; exact damage per breach is already asserted by the galley test and `EveryRamEffect…`) | — |
| RAM-V3 | minor | **REPRODUCED** | Probes E/F identical (169 collisions, 109 below v_ram; of 21 unrammed below-v_ram contacts 4 in Close mode on the own target, 17 in Open mode). S9 sweep (16 receding-target geometries): Close mode while the target outran ramming speed in 15 of 16, up to 21 steps | **Relative run-up check** (target's motion along the line of sight); report claim corrected with the contact rates | `TheAttackRunAccountsForTheTargetsOwnMotion` (2 cases, + stationary control) | N5 killed |
| RAM-V4 | minor | **REPRODUCED** | On pristine `c2b5f4a`: mutants V1 (lock check removed), V5 (glance keeps way), V8 (collision keeps way) pass 597/597 (clean run 20 s, bound 210 s, runs 17–18 s) | tests only | `TheBeakLockHoldsTheRammerWhateverItsOrder`, `AGlancingBlowKeepsOnlyItsTangentialWay`, `ACollisionArrestsTheRammersWay` | V1, V5, V8 killed |
| RAM-V5 | minor | **REPRODUCED** | On pristine `c2b5f4a`: V2 (depth always full) and V7 (attack angle = glance angle) killed only by the galley golden; V6 (untracked target) by the golden and the premise pin of `ARammerBackingItsBeakOutMakesNoStrike` | tests only | `ALighterBlowEmbedsTheBeakLessAndFreesItSooner`, `AnAttackRunStartsOnlyFromTheAttackAngleOffTheKeelLine`, `TheRamTargetIsTheNearestTrackedHull` | V2, V6, V7 killed |
| RAM-V6 | minor | **REPRODUCED** | Code: no citation for the collision effect; a full-speed contact on a rammed hull damages only the rammer's bow | Rationale added (below-threshold: Itzhack et al. 2024; arrest of way: INFERRED, labelled); the RM1-at-speed effect flagged as reading **RAM-R5**, proposal P9 — code unchanged (no neutral alternative exists; the Director decides) | — (documentation) | — |
| RAM-V7 | minor | **REPRODUCED** | Code diff; S8: a Board approach makes no ram on `c2b5f4a` (after round 1 the same approach strikes at 4 160 mm/s, a breach) | **Withdrawn**: a Board approach rams under the D-081 rules (lane N's behaviour, its rule replaced by RM1–RM3); a boarder's slow contact is the grapple, no collision event; P2 stays a proposal | `ABoardingApproachAtRammingSpeedRamsOnceThenGrapples`; `NavalTests.RamDamageScales…` re-rigged (§13.4) | N7, N8 killed |
| RAM-V8 | minor | **REPRODUCED** | Code: only the rammer is locked; the lane's own test shows the struck hull turning during the lock | Listed as a limitation (§13.6) with proposal P10; no code change (pinning the struck hull is a new mechanism with open questions: two rammers, a sinking target) | — (documentation) | — |
| RAM-V9 | nit | **REPRODUCED** | grep: the name occurred only in the pointer comment | The test now exists under that exact name in `RamTests` (N-R2-07 scenario: three distinct hulls, each lock = the free time of its depth exactly, each next ram ≥ release + 7 steps) | `RamsOnALineOfHullsAreSpacedByTheFreeTimeAndTheRunUpNotByATimer` | (also kills V2) |
| RAM-V10 | nit | **REPRODUCED** | S10: the unarmed struck hulk's formation got DamageDealt 9 (its activity flag set too) | `DomainHit` credits no source for kind 3 | `ARammersOwnBowDamageCreditsNoOne` | N6 killed |
| RAM-V11 | nit | **REPRODUCED** | On pristine `c2b5f4a`: M20 passes 597/597; `RamCrewDamage` "inferred"; no rammer-side crew effect | M20: a reflection test on a live battle (remove the pair → hash changes; restore → hash restored). `RamCrewDamage` stays INFERRED and rammer-side crew effects stay unimplemented: proposal P13 | `TheRammedListIsPartOfTheStateHash` | M20 killed |

### 13.2 What changed (code; `TacticalBattle.Ram.cs` unless noted)

- **Clearing rule** (`ClearOfHulls`, RAM-V1 a, c). In Open mode, and for a fouled hull, before any other steering: the
  bearings to every on-field hull (either side) whose surface lies within the pivot reach (beak 2 m + one step at a quarter
  of the maximum speed = 4.3 m for the galley) are sorted; the escape heading is the bisector of the WIDEST free sector
  between consecutive bearings (one hull: directly away; first sector in ascending order on a tie); the hull pivots (speed 0)
  while any of them lies forward of its beam, then makes way. Boxed in (no sector wider than a half turn) it pivots there
  until a neighbour moves. The lane's `PivotNear` in the Open branches became unreachable and was removed there.
- **Closure gate** (RAM-V1 d). `RamState.FromPos` = the hull's position at the start of the step (written by the movement
  hook, `TacticalBattle.DomainMove.cs` one line; read only by the same instant's Fire phase, so it carries no state across
  instants and is not hashed). A bow contact is an impact only if |p_a − p_b|² at the end of the step < at its start.
- **Relative run-up check** (RAM-V3, reading RAM-R4 applied to the planner): u = target speed · cos(bearing − target
  heading) (its speed along the line of sight, + receding); c0 = max(0, own component − u); need =
  `RunUpGapMm(c0, v_ram, vmax − u, a, dt, beak)` (∞ when vmax − u < v_ram). A constant-velocity assumption — still no lead.
- **Board** (RAM-V7): `RamContacts` runs for Attack or Board; under Board only a ram is scheduled.
- **Bow credit** (RAM-V10, `TacticalBattle.DomainCombat.cs` one line): `if (ev.A != BowHitKind) CreditSource(...)`.

### 13.3 AI-vs-AI galley battles before / after (measured; both sides Attack at pulse 0)

| Metric | `c2b5f4a` | round 1 |
|---|---|---|
| seeds 1–20: breaches / glances / collisions (below v_ram) | 205 / 96 / 169 (109) | 185 / 81 / 58 (24) |
| seeds 1–20: largest breach hull damage | 89 | 89 |
| seeds 1–20: longest stillness of a planner-steered, unlocked hull | 255 steps (seed 2) | 13 steps (seed 4: a pivot among three moving neighbours) |
| seeds 1–20: same-pair collisions on consecutive steps | 38 | 1 (seed 19: a real re-closure at 585 mm/s, both hulls moving) |
| seeds 1–20: mean pulses | 10.2 | 10.6 |
| seeds 1, 2, 8, 14, 18, 20: unrammed contacts below v_ram (Close on own target / Open) | 21 (4 / 17) | 4 (3 / 1) |
| S9 receding-target sweep: geometries with Close mode while the target outruns v_ram | 15 of 16 | 0 of 16 |

The three Close-mode contacts left are crossing targets (target heading ~90° off the rammer's): the planner still aims at
the target's present position (no lead) — limitation, proposal P11. **[Corrected in round 2 (§14, RAM2-V3): these three
were NOT below ramming speed in any physical sense — the rammers had 67–94 % of maximum way and drove into the target's side
at v_n 3 289–3 789 mm/s (2.1–2.5 × v_ram); they were "below" only in the centre-line v_c of round 1's gate (RAM2-V1). The
target-heading measurement (~93° off) stands; the stated speed and the stated cause did not.]**

### 13.4 Moved pins and goldens (attribution measured)

| Pin | Change | Cause |
|---|---|---|
| galley 5v5 trace golden `RamTests.GalleyTrace` (seed 42) | `D3B4D73A…7E52` (6 pulses, side 1 eliminated, 7 breaches, 2 glances, 0 collisions) → `01DF25FF…9262` (10 pulses, side 0 eliminated, 8 breaches, 2 glances, 3 collisions, closing 1 705–5 726 mm/s) | Measured chain, reverting the fixes one by one in the final tree (reverting all reproduces `D3B4D73A` exactly): bow credit, Board and closure gate leave the trace unchanged; the **clearing rule** moves it to `F95F835B…` (10 pulses, side 1 eliminated); the **relative run-up** then gives `01DF25FF…`. |
| `NavalTests.RamDamageScales…` Board part | boarder starts 30 m off (was 110 m); also asserts no collision | premise lost: with Board ramming again and the cited 0.11 m/s² the boarder could not brake to its 1 m/s cap inside the 37 m zone (measured strike: 3 300 mm/s, a ram) |
| `RamTests.ABoardingOrderNeverRams` | removed | pinned the withdrawn reading (RAM-V7) |
| sail line, turret, amphibious `domain golden` | **unchanged** (trace, state, result, personnel — measured byte-identical) | no ram-capable hull |

### 13.5 Mutants (fixed tree, `M7Proto.slnx` Release, one full suite per mutant; clean run 21 s → bound 210 s)

15 mutants, **15 killed, each by at least one semantic test**, 0 hung (runs 16–21 s), 1 build failure (the first form of
N4 left an unused variable under TreatWarningsAsErrors; replaced by an equivalent mutant that compiles). Teeth and aim: each
killing test asserts a rule this report states.

| Mutant | Semantic killer(s) (golden also failed: G) |
|---|---|
| V1 lock check removed (locked hull obeys a MoveTo) | `TheBeakLockHoldsTheRammerWhateverItsOrder` |
| V2 beak depth always full | `ALighterBlowEmbedsTheBeakLessAndFreesItSooner`, `RamsOnALineOfHulls…` (G) |
| V5 glance keeps full way | `AGlancingBlowKeepsOnlyItsTangentialWay` (G) |
| V6 untracked target allowed | `TheRamTargetIsTheNearestTrackedHull`, `ARammerBackingItsBeakOutMakesNoStrike` (G) |
| V7 attack angle = glance angle | `AnAttackRunStartsOnlyFromTheAttackAngleOffTheKeelLine` (G) |
| V8 collision keeps way | `ACollisionArrestsTheRammersWay` (G) |
| M20 rammed list not hashed | `TheRammedListIsPartOfTheStateHash` |
| N1 closure gate removed | `AHullPressingOnAnIdleRammersBowMakesNoImpact` |
| N2 clearing rule removed | `ARammerBoxedBowAndQuarter…`, `NoGalleyIsDeadlockedInAiBattles` (G) |
| N3 clearing directly away from the lowest-bearing hull | `ARammerBoxedBowAndQuarter…` (G) |
| N4 clearing never pivots | `ARammerClearingAHullAheadPivotsAndNeverClosesOnIt` (G) |
| N5 run-up ignores the target's motion | `TheAttackRunAccountsForTheTargetsOwnMotion` ×2 (G) |
| N6 bow damage credited to the struck hull | `ARammersOwnBowDamageCreditsNoOne` |
| N7 boarder's slow contact logged as a collision | `ABoardingApproachAtRammingSpeedRamsOnceThenGrapples` |
| N8 Board does not ram | `ABoardingApproachAtRammingSpeedRamsOnceThenGrapples` |

Against pristine `c2b5f4a` (reproduction of V4, V5, V11): V1, V5, V8, M20 pass 597/597; V2, V7 fail only the golden; V6 the
golden and a premise pin. Raw output: `/dev/shm/m7c/RAM-r1-mutants.out`, `/dev/shm/m7c/RAM-r1-lane-mutants.out` (not committed).

### 13.6 Tests, determinism, bench

| Build (`M7Proto.slnx`) | `c2b5f4a` (lane and verifier logs) | round 1 (measured) |
|---|---|---|
| Release | 597 / 0 / 0 | **613 / 0 / 0** (11–13 s); build 0 warnings, 0 errors |
| Debug | 597 / 0 / 0 | **613 / 0 / 0** (29–33 s); build 0 warnings, 0 errors |

613 = 597 − 1 (`ABoardingOrderNeverRams`) + 1 (`RamsOnALineOfHulls…`) + 16 (`RamVerificationRound1Tests`, one theory with 2
cases). Determinism scan: CLEAN, 67 files. `M7Econ.slnx` / `M7DetMath.slnx` do not reference the touched projects (§7)
and were not re-run.

Bench (`Proto.M7.Bench domain 3`, Release, `nice -n 19`, 3 interleaved pairs `c2b5f4a` vs round 1; load 1.6–2.3):

| battle | pulses / events lane → r1 | total ms lane | total ms r1 | median ms/pulse lane | median ms/pulse r1 |
|---|---|---|---|---|---|
| galleys 5v5 | 12.7 / 1 808 → 11.7 / 1 635 | 324 / 357 / 350 | 389 / 405 / 386 | 9.19 / 9.13 / 8.75 | 14.93 / 15.06 / 14.91 |
| amphibious | 18.0 / 2 433 → 18.0 / 2 433 | 619 / 616 / 645 | 679 / 612 / 667 | 12.25 / 13.05 / 12.08 | 13.50 / 10.77 / 11.80 |

Galley battles cost ~1.6× per pulse (~+15 % in total) — measured, not profiled (inferred: the clearing scan, its Atan2 and
sort, for every Open-mode hull each step). Amphibious, sail and turret: identical pulses and events, times within noise.

### 13.7 Limitations after round 1 (honest list; adds to §11)

- **Crossing targets**: no lead pursuit; 3 Close-mode contacts below ramming speed remain in the 6-seed probe (P11).
  **[Corrected in round 2 (§14, RAM2-V3): they were full-way beak strikes misfiled by the v_c gate; see §14.6 for the residual.]**
- **Boxed hull**: with no free sector wider than a half turn it pivots in place until a neighbour moves (no astern motion is
  modelled). Not observed in seeds 1–20 (inferred, not tested).
- **The struck hull is not held** while the beak is embedded (RAM-V8): it may turn, and move off if its course leads away
  (the verifier's Probe D measured no displacement in its scenario; the lane's test shows the heading change). P10.
- **Boarding approach braking**: lane N's 37 m boarding zone is too short to brake from full speed at the cited
  0.11 m/s², so a Board order now rams at up to near full speed (S8: 4 160 mm/s breach) — lane N's own approach rule, now
  with D-081's ram (P12).
- **Collision effect**: the arrest of way is inferred; RAM-R5 (a full-speed contact on a rammed hull damages only the
  rammer's bow) is a reading for the Director (P9).
- **Crew**: `RamCrewDamage` (6) remains INFERRED; no crew effect on the rammer's side (P13).
- **M20 teeth** come from a white-box reflection test (no battle differing only in the rammed list was found).
- `FromPos` is deliberately not in the state hash (written and read within one instant).

### 13.8 Proposals added (Director; none applied)

9. **P9 — RAM-R5**: what does a full-speed bow contact on a hull this rammer already rammed do to that hull? Applied (strict
   RM1): nothing to the struck hull, the rammer's bow takes its energy share. Alternatives: the struck hull takes the same
   energy-scaled "bow share" as hull damage (a hull-to-hull collision, not a beak breach); or treat it as a second breach
   (contradicts "can't … ram again").
10. **P10 — pin the struck hull** for the free time (both hulls joined by the beak); open: two rammers in one hull, a target
    sinking with the beak in it (P5).
11. **P11 — lead pursuit**: aim the attack run at the intercept point of a moving target (constant-velocity prediction).
12. **P12 — boarding braking distance from the kinematics** (like the run-up): cap the boarding approach from
    d = (v² − v_board²) / 2a, so a boarder arrives at boarding speed instead of ramming.
13. **P13 — crew effects**: a sourced casualty figure for the struck hull, and the rammer's own crew thrown by the impact
    (both sides energy-scaled), through the personnel ledger.


## 14. Verification round 2 (2026-11-05)

Fix agent, round 2. Clone `/dev/shm/m7c/RAM` (it survived; branch `lane/RAM` at `c7d7498`; no checkpoint restore needed).
Throttled as before (`nice -n 19`, `-m:1`, one suite run per mutant). Every finding was REPRODUCED against `c7d7498` before
any change: the verifier's probe file (`VerifierR2Probes.cs`) was copied into the clone uncommitted, with only its output
path changed to a file of this run; its four sections (Probes A, B, C, D) were **byte-identical** to the verifier's
`probe-delivered.txt`. The verifier's mutants MV1, MV4, MV6, MV7, MV8 were re-run with its exact patterns in this run's own
mutant copy: same outcomes, and the same "Actual" trace hashes for the golden-only kills (C052906A…, BB648D32…, 0E7FB82E…).
Own probes (`RamR2Probes.cs`, uncommitted; copies of both probe sources and their outputs in `/dev/shm/m7c/RAM-r2-probes/`)
supplied the census and scenario measurements below. Both probe files were deleted before the final validation.

### 14.1 Verdicts

| ID | Sev. | Verdict | Reproduction on `c7d7498` (measured) | Change | Semantic test(s) (`RamVerificationRound2Tests` unless noted) | Mutants |
|---|---|---|---|---|---|---|
| RAM2-V1 | major | **REPRODUCED** | Probe C identical: 21 collisions on unrammed pairs with v_c &lt; v_ram, 12 of them with v_n ≥ v_ram (2 332–4 153 mm/s); e.g. seed 7 t = 314 s, 2001 → 1003, v_n 4 153, v_c 1 342: no damage, rammer stopped, bow −7. Probe B: three crossing-target contacts filed as collisions (v_n 3 222 / 3 390 / 1 380). Code: `Classify` gated `ram` on v_c, damage on v_n. | **Ram gate on v_n** (the closing velocity component normal to the target's side, the speed the damage uses); v_n computed from the rammer's own velocity; **planner** aims the run at the own way s_req = ⌈v_ram / \|sin a\|⌉ (a = run-to-keel angle) and treats a range that cannot close (u ≥ vmax) as no run; **collision after-speed** max(0, way − v_n) (see 14.2) | `AFullWayStrikeOffTheTargetsCentreIsARamByItsNormalSpeed`, `ACrossingTargetStruckAtFullWayIsHoledByTheNormalEnergy`, `TheRunNeedsTheWayWhoseNormalComponentIsRammingSpeed`, `AnObliqueRunDoesNotPressInWithoutTheWayItsAngleNeeds`, `ARammerDoesNotRunAtATargetThatOutrunsIt`; galley invariants now on v_n | R1, R3, R4, R5, R6 killed; R2 see 14.7 |
| RAM2-V2 | major | **REPRODUCED** | Probe A identical: `RAM t=60000 src=1000 tgt=2000 A=1 B(vn)=0 C(vc)=3449`, rammer max speed 0, never moved, RammedBy(1000) = [2000], mode Open. Probe C: 18 of 258 rams with own way &lt; v_ram, 2 with own way 0. | Same gate: only the rammer's own way supplies v_n (the target's velocity lies along its keel), so a hull that has not built ramming speed cannot ram (D-081 RM3, DECIDED) | `OnlyTheRammersOwnWayCanMakeARam` (exhaustive 2° grid × 3 target speeds, 97 200 cases), `AHullLyingStillIsNotCreditedWithARamTheTargetRanInto` (Probe A as a test) | R1 killed |
| RAM2-V3 | minor | **REPRODUCED** | The lane's round-1 probe log (`/dev/shm/m7c/RAM-r1-repro-fix2.txt`, "PROBE F collision causes", written in round 1) lists exactly the verifier's three contacts — seed 1 t = 358 s 2000 → 1003 (v_n 3 789, v_c 1 006), seed 8 t = 256 s 2002 → 1001 (3 289 / 1 445), seed 18 t = 270 s 2001 → 1001 (3 446 / 1 525), all prevMode Close — so the verifier's inference that they are the same three is now **proven**. Rammer speeds 3 080–4 324 mm/s; v_n 2.1–2.5 × v_ram. | Report corrected (§13.3, §13.7 marked); the residual after round 2 re-measured with its cause (14.6) | — (report) | — |
| RAM2-V4 | minor | **REPRODUCED** | MV4: full suite 612 / 1, the galley test failing only at its trace-golden line (Actual C052906A…) | test | `AGlancingRamUsesUpThePair` (new geometry `ObliqueMeeting`: two ram galleys meeting bow to bow obliquely; 1000 glances off 2000 at v_n 1 954 ≥ v_ram; pair recorded, planner target −1 for every later step, the next contact on 2000 a collision, 2000 may still ram 1000) | MV4 killed |
| RAM2-V5 | minor | **REPRODUCED** | MV1 (Mode and Ref dropped from the hash): 613 / 613 pass (18 s) | test | `ThePlannersModeAndReferenceArePartOfTheStateHash` (twin battles; editing Mode or Ref by reflection changes the hash at once, restoring restores it, and the edited twin's course differs on the next step — so both are behaviour state) | MV1 killed |
| RAM2-V6 | nit | **REPRODUCED** | MV6 and MV8: only the galley golden failed (Actual BB648D32…, 0E7FB82E…); MV7: survived 613 / 613 | tests; test-only inspection accessors `BeakContactOf`, `FouledByOf` (geometry only, no behaviour) | `ACollisionDamagesTheRammersBowByItsNormalEnergy` (RM1 collision: kind-3 hit of exactly ⌊90·100·v_n²/(1000·4 600²)⌋ on the rammer, nothing on the struck hull); `TheBeakStrikesInsideItsBowArcAndReachNearestThenLowestId` (±30° arc: 29.5° struck, 40.2° not; reach 18 000 mm struck, 18 001 not; equidistant hulls → lower id on either side, 1 mm nearer wins; fouling scan keyed the same way) | MV6, MV7, MV8 killed |
| RAM2-V7 | nit | **REPRODUCED** | `TheBeakLockHoldsTheRammerWhateverItsOrder` asserts only "moved at some time after release"; §6 said "no new … delivery semantic". Measured (probe): MoveTo delivered at its own instant (directive MoveTo from t = 48 s, Attack at 46 s), lock until 54.529 s, held at 54 s, first effect at 56 s (heading 0° → 340°, commanded way 220 mm/s), the struck hull it still touches blocks the move itself for three steps (pivot), way made from 62 s | §6 corrected; turn-exact pin | `AnOrderIssuedUnderTheBeakLockTakesEffectAtTheFirstStepAfterIt` (delivery ±1; held to release − 1 step with way 0; release step: turned by exactly the max turn toward the MoveTo point, commanded way = one step of acceleration; +1 step: turned further) | — |

### 14.2 What changed (code)

- `RamPhysics.Classify` (RAM2-V1, V2): `ram = v_n ≥ v_ram`, `breach = ram ∧ θ ≥ glance angle`. v_n = \|v_a × k_b\| (= \|w × k_b\|
  exactly, because the target's velocity lies along its keel; computed from v_a so the per-axis rounding of v_b cannot leak
  into it, which makes v_n ≤ the rammer's own way exact). v_c is still computed and logged (event field C); it still decides
  that the hulls are closing (v_c &gt; 0) together with the round-1 closure gate.
- `RamPhysics.RequiredWayMmPerS(v_ram, runHeading, keelHeading)` = ⌈v_ram · 2³⁰ / \|sin(run − keel)\|⌉ (Q30, exact against
  the gate on a run along heading 0; v_ram on the beam; ∞ along the keel).
- `RamIntent`, Close mode: need = ∞ when s_req &gt; vmax or vmax − u ≤ 0, else `RunUpGapMm(c0, s_req − u, vmax − u, …)` — the
  round-1 target-frame integration with its stop speed changed from v_ram (closing) to s_req (own way). D_open is unchanged
  (the beam station is perpendicular: s_req = v_ram there; at the 45° attack angle the run-up from rest, 21 801 mm, is
  below D_open).
- `RamCollisionStrike`: speed max(0, way − v_n) instead of 0 — the round-1 rationale ("the normal way is absorbed by the
  struck hull", INFERRED) now applied as written. Necessary companion of the gate: a full-way skim at a shallow angle is now
  a collision (normal speed below ramming speed), and with speed 0 it would stop dead; for a perpendicular contact the two
  rules are equal.
- Inspection (tests only): `RamWayMmPerS`, `BeakContactOf`, `FouledByOf`. `TacticalBattle.Domain.cs`: doc comment of
  `DomainKinds.RamCollision` only.

### 14.3 Reading RAM-R6 (replaces RAM-R4; flagged for the Director)

**RAM-R6**: the ram threshold of D-081 RM3 is the closing velocity component normal to the struck side (v_n), the same speed
whose kinetic energy RM4's damage uses; the target's own motion along its keel adds tangential slide only, so it enters
through the impact angle (a hull steaming toward the blow makes it skid — `GlancingBlowsDeflect…`: the same full-way strike
at 155° breaches a still hull, θ 25°, and glances on one making 2 m/s toward it, θ 17.5°). Why: RM3 is DECIDED ("the ship
must build up to that speed") and RM4 names the normal component; round 1's RAM-R4 contradicted the first (RAM2-V2) and split
gate and damage over two speeds (RAM2-V1). Consequences (measured, 14.4): no ram without own way; fewer glances (a glance now
needs v_n ≥ v_ram and θ &lt; 20°); oblique low-normal contacts are collisions that do not use up the pair (RAM-R1 / P1 still
governs real glances). Alternatives for the Director: (a) RAM-R6 as applied; (b) gate on the rammer's own way ≥ v_ram and
v_n ≥ v_ram (stricter: a beam blow from a hull whose way is mostly along the target's keel never rams); (c) round 1's v_c
gate (rejected here: contradicts RM3).

### 14.4 AI-vs-AI galley battles, seeds 1–20 (measured; verifier's Probe C method — contacts at a battle's final instant are not visited)

| Metric | `c7d7498` | round 2 |
|---|---|---|
| rams / own way &lt; v_ram / own way 0 | 258 / 18 / 2 | 221 / **0** / **0** |
| rams whose velocity normal points AWAY from the struck side (signed check) | 14 | **0** (so no signed-normal change was needed) |
| breaches / glances / collisions | 178 / 80 / 58 | 189 / 32 / 90 |
| collisions on unrammed pairs / of them with v_n ≥ v_ram | 21 / 12 | 66 / **0** |
| unrammed collisions by the previous step's mode: Close on own target / Open | 15 / 6 | 15 / 51 |
| contacts (ram or collision) in Close mode on the own target, unrammed, v_n &lt; v_ram | 19 | 15 |
| mean pulses | 10.6 | 11.2 |
| Probe B sweep (24 geometries): breaches / collisions | 2 / 3 | 6 / 0 |

The 51 Open-mode collisions are hulls brushing others at a low normal speed while opening the range (often mutual pairs at
the same instant; in the six seeds the probe lists, own way 660–4 278 mm/s, v_n 3–1 215). INFERRED, not measured contact by
contact: under round 1 many of them were "rams that glanced" (v_c ≥ v_ram, v_n &lt; v_ram) and used up pairs — glances fell
from 80 to 32. They do no hull damage, cost the rammer its normal way and at most 1 bow point.

### 14.5 Moved goldens (attribution measured)

| Pin | Change | Cause (measured: revert chain on the final tree, `domain golden`, harness `/dev/shm/m7c/RAM-r2-mut-harness.py attrib`) |
|---|---|---|
| galley 5v5 trace `RamTests.GalleyTrace` (seed 42) | `01DF25FF…9262` (10 pulses, 8 breaches, 2 glances, 3 collisions) → `6EE765E5…B2EE` (11 pulses, side 0 eliminated, 10 breaches, 1 glance, 4 collisions, closing 969–5 860 mm/s) | final `6EE765E5`; − collision after-speed `434A2597`; − planner s_req `03575A15`; − gate on v_n `65D6E701`; − v_n from v_a = `01DF25FF` exactly (round 1). Each of the four changes moves it. |
| sail line, turret, amphibious `domain golden` | unchanged (trace, state; amphibious test goldens pass) | no ram-capable hull |
| re-rigged tests (premise or asserted rule changed by RAM-R6) | `RamTests.ClassificationIsExact…` (bow-on / stern-chase: v_c as before, no ram), `GlancingBlowsDeflect…` (15° full way: no ram; 30° at 3 000 mm/s: no ram; a true glance added), galley invariants v_c → v_n, `NavalTests.RamDamageScales…` v_c → v_n, `ACollisionArrestsTheRammersWay` (speed = way − v_n; measured 95 mm/s after the contact), `AGlancingBlowKeepsOnlyItsTangentialWay` (new geometry: round 1's 25° run is now a collision), `TheAttackRunAccountsForTheTargetsOwnMotion` (predicate: u ≥ vmax or s_req &gt; vmax; TEST INPUT 3 200 → 2 900, because under RAM-R6 the 3 450 mm/s hulk no longer outruns a 3 200 rammer in any step), eight `.C ≥ v_ram` asserts → `.B ≥ v_ram` (stronger; all pass) | rule change, not a regression |

### 14.6 Residual Close-mode contacts below ramming speed (RAM2-V3 follow-up, measured)

15 in seeds 1–20 (4 of them in seeds 1, 2, 8, 14, 18, 20), against 19 on `c7d7498` in the same metric. In **all 15** the
target turned its full 20° (its turn rate over one step) in the step before contact; the previous step's approach angle
was 18–39° (Close mode continues down to the 20° abort angle) and the run-to-keel angle at contact 5–19°. One of
them (seed 12, t = 446 s) is a target running into a pivoting rammer with own way 0 — correctly no ram (RM3). Cause: the
planner neither predicts the target's turn nor aborts a run between the glance angle and the attack angle; proposal P14.

### 14.7 Mutants (round-2 tree, `M7Proto.slnx` Release, one full suite per mutant; clean 20 s → bound 200 s; runs 15–20 s; 0 hung, 0 build failures)

| Mutant | Result | Semantic killer(s) (G: the galley golden also failed) |
|---|---|---|
| MV1 hash drops Mode, Ref | KILLED | `ThePlannersModeAndReferenceArePartOfTheStateHash` |
| MV4 only a breach records the pair | KILLED | `AGlancingRamUsesUpThePair` (G) |
| MV6 collision without bow damage | KILLED | `ACollisionDamagesTheRammersBowByItsNormalEnergy` |
| MV7 beak tie → highest id | KILLED | `TheBeakStrikesInsideItsBowArcAndReachNearestThenLowestId` |
| MV8 bow arc 60° | KILLED | `TheBeakStrikes…`, `AGlancingBlowKeepsOnlyItsTangentialWay`, `AGlancingRamUsesUpThePair` (G) |
| R1 gate back on v_c | KILLED | `ACrossingTargetStruckAtFullWay…`, `AHullLyingStillIsNotCredited…`, `TheRunNeedsTheWay…`, others (G) |
| R2 v_n from w (round 1's formula) | golden only — **not counted** | equivalent at the gate as far as measured: an exhaustive 2° grid (97 200 cases) and 2.1 M near-perpendicular BAM-step cases found no input where the target velocity's rounding lifts v_n to v_ram at own way v_ram − 1; it changes v_n by at most a rounding unit. Retained because it makes "v_n ≤ own way" true by construction. |
| R3 planner aims at v_ram (round 1) | KILLED | `AnObliqueRunDoesNotPressInWithoutTheWayItsAngleNeeds`, `TheAttackRunAccountsForTheTargetsOwnMotion(4600)`, `AGlancingBlowKeeps…`, `AGlancingRamUses…` (G) |
| R4 planner ignores "range never closes" | first run **SURVIVED** (624 / 624) → test added → **KILLED** | `ARammerDoesNotRunAtATargetThatOutrunsIt` |
| R5 collision stops dead (round 1) | KILLED | `ACollisionArrestsTheRammersWay` (G) |
| R6 s_req rounded down | KILLED | `TheRunNeedsTheWayWhoseNormalComponentIsRammingSpeed` |

Totals: 11 mutants; 10 killed by semantic tests (one only after a test was added), 1 golden-only (R2, argued equivalent),
0 hung. Teeth and aim: each killing test asserts a rule this report states (RM1 pair use by a glance, RM3 own-way gate, the
s_req run-up, no chase of an outrunning target, the collision's normal-way loss and bow energy, beak geometry, hashed
planner state). Raw output: `/dev/shm/m7c/RAM-r2-mutants.out` (not committed). Observed, not a finding and not addressed:
the verifier's MV5 (target choice ignores `OnField`) survived on `c7d7498`.

### 14.8 Tests, determinism, bench

| Build (`M7Proto.slnx`) | `c7d7498` | round 2 (measured) |
|---|---|---|
| Release | 613 / 0 / 0 | **625 / 0 / 0** (14 s); build 0 warnings, 0 errors |
| Debug | 613 / 0 / 0 | **625 / 0 / 0** (29 s); build 0 warnings, 0 errors |

625 = 613 + 12 (`RamVerificationRound2Tests`). Determinism scan (`scripts/check-proto-determinism.sh`): CLEAN, 67 files.
`M7Econ.slnx` / `M7DetMath.slnx` reference neither touched project (§7) and were not re-run.

Bench (`Proto.M7.Bench domain 3`, Release, `nice -n 19`, 3 interleaved pairs `c7d7498` vs round 2; load 1.8–1.9):

| battle | pulses / events r1 → r2 | total ms r1 | total ms r2 | median ms/pulse r1 | median ms/pulse r2 |
|---|---|---|---|---|---|
| galleys 5v5 | 11.7 / 1 635 → 11.3 / 1 600 | 390 / 396 / 403 | 353 / 228 / 260 | 14.94 / 14.65 / 14.68 | 7.98 / 2.80 / 3.14 |
| amphibious | 18.0 / 2 433 → 18.0 / 2 433 | 681 / 624 / 659 | 619 / 678 / 654 | 13.07 / 11.56 / 12.35 | 12.39 / 12.74 / 13.16 |

Galley battles measured faster per pulse in round 2 (noisy: 2.8–8.0 ms); not profiled, not attributed. Amphibious, sail and
turret: identical pulses and events, times within noise.

### 14.9 Limitations after round 2 (adds to §11 and §13.7)

- **RAM-R6 is a reading** of RM3 + RM4 (14.3), applied and flagged; the collision after-speed (way − v_n) stays INFERRED.
- **Target turns are not anticipated**: 15 Close-mode contacts below ramming speed in seeds 1–20, every one after the target
  turned in the last step (14.6; P14).
- **Glances are rarer** (32 vs 80 in seeds 1–20); RAM-R1 (a glance uses up the pair) still applies to them (P1).
- **The freed rammer can be held by the hull it touches**: in the RAM2-V7 scenario the order takes effect at the release
  step but the move is blocked for three steps while it pivots clear (measured; no astern motion is modelled, §11).
- **R2 survives except for the golden** (14.7), argued equivalent by search, not proven.
- The census counts exclude each battle's final instant (the verifier's method, kept for comparability).
- Bench: 3 pairs, noisy; probe sources not committed (copies in `/dev/shm/m7c/RAM-r2-probes/`).

### 14.10 Proposals added (Director; none applied)

14. **P14 — target-turn-aware attack run**: re-evaluate s_req with the target's heading rate (or abort a run whose
    approach angle falls below the attack angle while the target is turning toward or away), extending P11 (lead pursuit).
15. **P15 — confirm RAM-R6** (ram gate on the normal closing speed) or choose alternative (b) or (c) of 14.3.
