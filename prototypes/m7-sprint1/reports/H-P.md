# H-P — AutoResolver parity with terrain, repeatable battery (M7 Phase C, lane P)

Branch `lane/P` in a private clone (`/dev/shm/m7c/P`), cut from dev `6bd3aa2` (contains lanes RAM, N and TR). Everything
lives in `prototypes/m7-sprint1` (D-079 Q5 open: nothing in Sim.Core / Sim.Data / Sim.Cli / Sim.Tests / Sim.Ui).

Status words: **MEASURED** (run on this tree, output quoted), **DERIVED** (hand-derived and pinned by a test),
**INFERRED** (argued, not isolated), **PROPOSAL** (a Director-reserved choice — decided by nobody here),
**PLACEHOLDER** (a magnitude without a ratified value).

## 0. Summary (added after the acceptance run; §2 below is unchanged since its pre-commit `1ac95dd`)

| Brief item | State | Evidence |
|---|---|---|
| (1) Pre-committed per-metric bands, by family, held-out battery (PROPOSAL, D-079 Q4) | Done: rule P1 + battery P (4 families, 20 tuning + 9 held-out land scenarios incl. terrain-heavy and combined arms; seeds tuning 1–80 / acceptance 5001–5160 / control 9001–9160), committed BEFORE any AutoResolver change | §2; commit `1ac95dd` |
| (2) Terrain in the AutoResolver, mirroring lane TR's coefficients | **PARTIAL** (corrected in verification round 1, P-V3; this row said "Implemented"): model 2 reads lane TR's terrain terms at each formation's single axis point — path axis, Naismith climb, layered sight, slope / footing / cohesion / impetus in melee, elevation and cover in missiles, convergence mobility, fire saturation; exact-wiring tests against `TerrainCombat` / `TerrainSight` (impetus wiring tested only since round 1). **NOT implemented:** the brief's aggregation over the battle map (cover share, forest / urban share, river-crossing count) and any **frontage-width** mechanism; the defile scenario does not exercise frontage (case-by-case identical to hold-line-open in both resolvers). *Round 2 (P2-V7, REPRODUCED, not fixed): still not implemented — reasons and a PROPOSAL in §12.4* | §3, §8, §11, §12 |
| (3) Improve until the bands are met on the held-out battery, or show why not | **NOT met: 16 / 29 scenarios pass (held-out 4 / 9)**; the Sprint 1 model scores 11 / 29 on the same battery. Bands never relaxed. The 16 include defile and hold-line-open, which are identical case by case in both resolvers (160 / 160 each, MEASURED round 1): **15 distinct of 28 distinct scenarios** (terrain family 5 of 9 distinct). At n = 160 the binding win-rate band is the sampling floor, 10.4–20.2 points, not the 10 points of §2.6 (§11, P-V6). Why not: §6 (duration tail and withdrawal escape, simultaneous routs, weak-side non-linearity, slope magnitude, 1-D manoeuvre). *Round 2: the model changed (P2-V3 depth fix); on the same, already CONSUMED battery the current tree still gives 16 / 29 (held-out 4 / 9) — not acceptance evidence (§12.2)* | §5, §6, §12 |
| (4) Repeatable battery, bit-identical across Debug/Release and repeats; battery hash | MEASURED identical in Release (2 threads), Debug (2 threads) and Release (1 thread): battery hash `18222D80D7E3A5E69BE133B68D0E00A79E768694D3667DF3CF2CBF46740CD009` (the model as of `c31b6a9`). *Round 2: the current tree's battery hash is `BEA60C2F89F0D965AC2B7FD3945B5AAFD0FC8C38DB9D80DF53E139025B236442` (P2-V3 moved the 320 combined-arms / combined-arms-vs-mass rows), identical in Release, Release repeat and Debug (§12.2)* | §7, §12 |
| (5) Limitations with evidence | §6 (10 items; 11–14 added in round 1; round-2 notes on 6 and 11) | §6, §12 |
| Inactivity rule per side | Tactical DIR5 + Standoff; auto model 2 ported DIR5 + Standoff; auto model 1 legacy. Round 1 found three port defects (hidden = "never known", Standoff counter reset by a rout, rule read from a coefficient instead of the setup) and fixed them; the battery hash is unchanged by every round-1 fix (MEASURED). Round 2 found three more (P2-V1 first-detection credit by formation id, P2-V2 no approach-minimum progress, P2-V4 exemption (a) ignored the setup's clause) and fixed them; none of them moves any battery case (MEASURED: the axis fix alone reproduces the round-2 acceptance CSV byte for byte) | §3, §5, §11, §12 |
| AI-vs-AI → auto; naval → tactical by default | `ResolverPolicy` (default only); no naval auto model | §3 |
| Tests / mutants | 625 → 691 / 0 / 0 (Release and Debug); 20 mutants: 19 killed by their named semantic test, 1 non-termination finding. Round 1: 691 → **733 / 0 / 0** (Release and Debug); 15 verification mutants, all KILLED by their named semantic test on the final tree (two needed a second test, §11.3). Round 2: 733 → **775 / 0 / 0** (Release and Debug); 12 mutants (the verifier's 5 survivors / golden-only kill re-run, 7 new), all KILLED by their named semantic test (§12.3) | §8, §11, §12 |

> **Commit order:** §1–§2 were written and committed BEFORE any AutoResolver change or tuning
> (the pre-commitment the brief requires). Everything after §2 was appended later; §2 is never edited after its commit
> (corrections, if any, are appended as dated notes below it).

---

## 1. Provenance and baseline

- **Resume.** The lane was resumed after a Director pause. `/dev/shm/m7c/P` did **not** exist (the container had
  restarted: uptime 5 min at 00:53Z on 2026-11-06, `/dev/shm/m7c` empty) and **no checkpoint existed**: no
  `bundles/P-wip.bundle`, no `bundles/P.bundle`, no `wip/m7c-P.patch` (searched the scratchpad and the file system).
  The previous session's work-in-progress is therefore lost; this lane was re-done from scratch on a fresh clone of
  `6bd3aa2`. Nothing of another lane was touched.
- **Baseline on `6bd3aa2` (MEASURED, throttled: `nice -n 19`, `-m:1`):** `dotnet test M7Proto.slnx` — Release **625 / 0 / 0**
  (test run 12 s), Debug **625 / 0 / 0** (27 s).

---

## 2. PRE-COMMITTED parity acceptance criteria and battery (PROPOSAL for the Director, D-079 Q4)

Committed before any tuning and before the AutoResolver was changed. Code: `Proto.M7/Tactical/ParityCriteriaP1.cs`
(rule), `ParityBatteryP.cs` (battery), `ParityCalibrationP.cs` (tuning-set isolation); tests
`Proto.M7.Tests/Tactical/ParityPTests.cs` (15 tests: hand-computed bands, exact boundaries of every check, tie-dense
statistics, deployment legality, mirror images, disjoint seed sets, calibration refuses non-tuning cases).

### 2.1 Rule P1 (per scenario)

The candidate C (AutoResolver) is compared with the reference R (TacticalResolver) on two independent samples (n_C, n_R
battles; independent seeds). All arithmetic is exact integer (rationals, ceilings of integer square roots).

| Check | Metric | Passes when |
|---|---|---|
| V victor | modal outcome (A wins / B wins / neither holds the field; ties A < B < neither) | if R's modal rate ≥ **600 ‰**, C's modal outcome equals R's |
| W win rates | rate of each of the 3 outcome classes | \|r_C − r_R\| ≤ max(**100 ‰**, z·√(p̃(1−p̃)(1/n_C + 1/n_R))), p̃ = (k_R + ½)/(n_R + 1) |
| CA, CB casualties | per role: (killed + surrendered) / start strength, ‰; **mean** | \|m_C − m_R\| ≤ max(**30 ‰**, z·s_R·√(1/n_C + 1/n_R), one entity of the role = ⌈1000/start⌉) |
| D duration | end instant − start (ms); **mean** | \|m_C − m_R\| ≤ max(**15 %** of m_R, z·s_R·√(1/n_C + 1/n_R), one step = 2,000 ms) |
| P pulses | median pulse count | \|median_C − median_R\| ≤ **1** |

z = **3.6** (Bonferroni: ~180 sampling checks in the battery at a 5 % battery-wide false-rejection rate). s_R is the
REFERENCE's sample standard deviation only, so a noisier candidate cannot widen its own band.

### 2.2 Battery acceptance

**ACCEPT** only if, on the ACCEPTANCE seeds, **every check of every scenario passes** — the 9 held-out scenarios (never
seen by the calibration; they decide) **and** the 20 tuning scenarios (on fresh seeds: seed generalisation) — **and** the
TacticalResolver passes the same rule against its own independent control sample in every scenario (attainability: a
scenario whose bands reject the tactical resolver against itself is **INCONCLUSIVE**, never a pass). There is no "k of n"
acceptance: 28/29 is a rejection. Minimum battery: 160 independent battles per scenario per resolver; every family has
held-out scenarios. Domain: **land only** — the AutoResolver has no naval or air model and naval battles default to the
TacticalResolver (DIR5 §2.1); a naval parity battery would be needed before any naval auto model could be offered.

### 2.3 The battery (`ParityBatteryP`)

Field: Slice D 48 × 32 cells of 20 m; PLACEHOLDER cost, concealment and lane-TR terrain-effects tables. Both sides are
driven by `KnowledgeAi` (the AutoResolver assumes its doctrine). Role A = side 0 (the attacker where there is one), role B =
side 1. Every NEW scenario is deployment-legal under DIR5 §2.14 (`Deployment = Thirds`, re-checked by the tactical engine:
A in columns 0–15, B in columns 32–47). Terrain-defence defenders use the new `Posture.Defend` (KnowledgeAi orders an
explicit Hold at pulse 0 and never attacks or advances; it still withdraws below morale 450) with the front rank 400 mm
inside column 32 (x = 640,400 mm), so an attacker in contact stands one cell out — on the slope, in the ford, in the marsh
(lane TR's test geometry).

| Family | TUNING scenarios (seeds 1–80) | HELD-OUT scenarios (decide acceptance) |
|---|---|---|
| Open conditions | equal-infantry, numbers-2to1, low-supply, low-morale, leadership-edge, experience-edge (Sprint 1 #1, 2, 5, 6, 7, 9) | numbers-5to4 (HI 50 v 40); mixed-conditions (A: experience 25 + leadership 30; B: supply 3/4 + morale 850); meeting-asymmetric (pre-F1 anchors, B 20 m nearer its exit — realistic asymmetry kept, DIR5 §2.14) |
| Composition | archers-vs-infantry-open (#3), combined-arms (#10), riflemen-vs-infantry (riflemen 60 v HI 50), sniper-screen (HI 50 + snipers 2 v HI 50: personal concealment, fire interval), shock-infantry (HI with charge impetus ½, TEST INPUT) | combined-arms-vs-mass (HI 50 + archers 60 v HI 50 + HI 50); riflemen-vs-archers |
| Concealment | archers-vs-infantry-forest (#4), jungle-ambush (deployment-legal ambush: jungle belt columns 32–37, ambusher at 34) | forest-ambush-archers (forest belt 32–36, archers in Ambush at 33) |
| Terrain | hill-crest (10 m terrace), ford-crossing (water 30–31, 3-cell ford), marsh-front (marsh 26–31), forest-hold-archers (archers v HI holding one cell inside a forest), defile (walls 26–31, 1-cell gap), archers-on-hill (archers holding a 10 m crest), hold-line-open (the same assault on flat ground: the terrain-free control) | high-ridge (20 m terrace: gradient at the cap 1), narrow-ford-archers (2-cell ford, archers hold the bank), urban-street (opaque 8 m buildings, impassable, cut by a 1-cell road) |

The Sprint 1 ambush (#8) is **not used**: its ambushers stand in the middle third (H-TR P-TR13).

### 2.4 Seeds, orientation and sample sizes

- Case (scenario, seed) is built **mirrored iff the seed is even** (columns x → 47 − x, exits swapped). Each seed is one
  independent battle and both orientations occur equally often, so n cases = n independent samples (the Sprint 1 battery's
  two orientations of one seed were strongly correlated, parity-report.md §1).
- **TUNING** seeds 1–80, tuning scenarios only (1,600 cases): the only cases the calibration may see
  (`ParityBatteryP.IsTuningCase`; `ParityCalibrationP.TotalLoss` throws on any other case — tested).
- **ACCEPTANCE** seeds 5001–5160 (n = 160), every scenario, both resolvers.
- **CONTROL** seeds 9001–9160 (n = 160), held-out scenarios, TacticalResolver only (attainability control); a tuning
  scenario's control sample is its tuning-seed tactical sample (n = 80).
- Seeds 5001+ and 9001+ have never been run by any battery before. The acceptance outcomes of BOTH resolvers are produced
  only after the model is frozen and committed, in one acceptance run that also measures the unchanged Sprint 1 model
  (`AutoCoefficients.Sprint1`) for comparison.

### 2.5 Inactivity rule on each side

The TacticalResolver runs its default rules: **DIR5 §2.6 hidden-stationary rule** (`InactivityModel.HiddenStationary`) with
lane TR's Standoff end (UNRATIFIED, P-TR1) and the mutual guard OFF. At this commit the AutoResolver still implements the
Sprint 1 `LegacySideDisengage` rule; this lane ports the DIR5 predicate to it. The acceptance output states the rule used
by each side, and every scenario in which any tactical battle ended by an inactivity rule (Standoff, routs by the
hidden-stationary rule) is listed.

### 2.6 Rationale of the constants (stated before any result)

- **Bands are the larger of a substantive tolerance and a sampling floor.** The substantive tolerances (10 points of win
  rate, 3 points of strength lost, 15 % of duration, 1 pulse) are what "meaningful comparability" (DIR5 §3.1) is taken to
  mean for a campaign that consumes winner, losses and duration. The sampling floor exists because two resolvers with
  independent randomness cannot agree more closely than sampling allows; it is computed from the tactical spread only.
- **Means, not medians, for casualties and duration.** The Sprint 1 rule used medians; in balanced scenarios the casualty
  distribution of a role is bimodal (winner ~20 %, loser ~36–40 %), so a median jumps between modes on a few cases (the
  Sprint 1 equal-infantry failure, parity-report.md §6.1). The mean is stable and has a known sampling error. The
  median pulse count is kept as a second, coarse duration check because pulses are the campaign-facing unit.
- **z = 3.6, n = 160, all scenarios must pass.** With ~180 sampling checks, z = 3.6 keeps the chance that an
  equal-in-distribution model fails anywhere at ≈ 5 %; n = 160 halves the sampling floors of the Sprint 1 battery
  (n_eff = 20–40). "9/10 is insufficient" (DIR5 §3.1) is implemented as: one failing check anywhere rejects.
- **Self-check required.** A band the tactical resolver cannot meet against itself would make the rule unattainable;
  such a scenario is reported INCONCLUSIVE rather than silently dropped or passed.

### 2.7 Disclosed before commitment

- A tactical-only sanity run (2 seeds per scenario: seeds 1–2 for tuning scenarios, control seeds 9001–9002 for held-out
  scenarios; `parityp sanity 2 2`, 3.4 s) was made to confirm every scenario builds, is deployment-legal in both
  orientations and terminates. It showed, among others, that an attacking `KnowledgeAi` formation beats a holding line on
  flat ground by a wide margin (defile: attacker losses 8–12 %), which is why the terrain-free control `hold-line-open` was
  added to the tuning set. No AutoResolver battle of the lane-P battery had been run when this section was committed.
- `Posture.Defend` (appended enum value 2) and its `KnowledgeAi` handling (explicit Hold at pulse 0, never attack or
  advance) are new; no existing scenario uses them, so every existing result is unchanged.

> **Dated note 2026-11-06 (verification round 1; §2 above is unchanged).** §2.6 calls 10 points of win rate, 3 points of
> strength and 15 % of duration the "substantive tolerances". At the committed sample sizes (n_R = n_C = 160) they are NOT the
> bands that bind in balanced scenarios: the z = 3.6 sampling floor is the binding win band for every reference rate from
> 11 / 160 to 149 / 160 — 17.5 points at a 25 % / 75 % rate, 20.2 at 50 % — and the duration floor reaches 19.4 % of the
> mean (equal-infantry: 40.3 s on 207.9 s). Rule P1 also has no check of the SHAPE of an outcome distribution (only means,
> the pulse median and class rates). Nothing in the rule or its constants was changed; the consequences and a proposal are in
> §11 (P-V6) and §9 (P-P6).

---

## 3. The AutoResolver model 2 (implemented; `Proto.M7/Tactical/AutoResolver.cs`, `AutoAxis.cs`)

`AutoCoefficients.ModelVersion = 2` is the new default; `AutoCoefficients.Sprint1` (ModelVersion 1) is the Slice E model,
kept bit-exact: the Sprint 1 test files (`AutoResolverTests`, `AutoResolverVerificationFixTests`, `ParityBatteryTests`)
now pin it explicitly and pass unchanged, including the Slice E golden digest `87BB6323…` (MEASURED). Every model-2
mechanism mirrors a TacticalResolver mechanism; the terrain terms are lane TR's `TerrainEffects` / `TerrainCombat` /
`TerrainSight` functions evaluated at the formations' points — the SAME tables the tactical engine uses, so parity uses
TR's coefficients by construction (exact-wiring tests in `AutoResolverPTests`).

| Mechanism (model 2) | What it mirrors in the TacticalResolver | Coefficient | Test |
|---|---|---|---|
| **Path axis**: the battle axis is the least-cost route between the sides' mean anchors (`GridPathfinder` + Naismith climb), not the straight line — a ford, a defile gap, a street, a detour round a hill | anchors march along A* paths | none | `PathAxisRunsThroughTheFordTheGapAndTheStreet`, `AxisProjectionTieBreaksOnTheLowerSegment` (tie-dense) |
| **Movement**: speed / (class cost + Naismith climb toward the point one cell ahead); a melee formation within the engage radius of a known enemy closes at catch-up pace | `MoveAnchor` + `ClimbPerMm`; entities leave their slots at catch-up pace within `EngageRadiusCells` | none | `ClimbingTheTerraceSlowsTheAdvanceByNaismithExactly` (2,600 → 503 mm per step) |
| **Decision latency**: the doctrine's order (MoveTo / Attack / ambush wait / Defend Hold) is decided at PULSE STARTS from the knowledge at that instant; no volleys while marching on MoveTo | `KnowledgeAi` decides only at pulse starts; `FiringStance` excludes MoveTo | none | via the battery (jungle-ambush passes only with it, §4) |
| **Detection**: the tactical sight query (`TerrainSight.Query`: canopy layers, elevation, buildings) from each observer formation's point; stationary factor; lane-N personal concealment unless the target attacked in the last step; aggregated over the target's entities, P = 1 − (1 − p)^n | one draw per (side, target ENTITY, instant) | none | `AnyOfIsOneMinusTheMissProduct`, `PersonalConcealmentNeverHastensDetection` (metamorphic, exact) |
| **Melee terms**: slope S(gradient cell a → cell t), footing, cohesion c0 + (1 − c0)·OrderCap, charge impetus of this step's advance | `TerrainCombat.MeleeAttackFactor` / `MeleeDefenceFactor` / `HitChance` | none (TR's table) | `CrestMeleeUsesTheTacticalSlopeTermsExactly` (3/4, 5/4), `FordWaderFightsWithoutFootingOrOrder` (3/8) |
| **Missile terms**: elevation 1 + γ·g; cover = `TerrainSight.FirePath` transmission; an opaque structure blocks the shot | `MissileHitChance` + `FirePath` | none (TR's table) | `ArcheryIntoForestPaysTheTacticalCoverExactly`, `ArchersOnTheCrestShootWithTheElevationFactor` |
| **Defend doctrine**: a holding line fights where touched (`HoldEngagePermille`); attackers converge on it (`MaxAttackersPerHolderPermille`) | Hold-directive entities stay in their slots; Attack-directive entities leave them (many-on-one) | 2 FITTED | `DefendFormationNeverAdvances`, `DefendHoldIsDeliveredAtPulseZeroOnly` (order-delivery pin) |
| **Convergence mobility**: the per-holder frontage cap × (1000 − w + w·m)/1000, m = 1000/(cost + climb toward the holder) | converging around a holder needs lateral movement, which terrain slows | `ConvergenceMobilityPermille` FITTED | `ConvergenceCapFollowsTheTerrainMobility` (exact) |
| **Fire saturation**: effective shots ≤ RangedPerExposed × the target's exposed front (min(alive, FormationColumns)) | every shooter takes the nearest tracked enemy entity: volleys concentrate (overkill) on the front rank | `RangedPerExposedPermille` FITTED | `FireSaturatesOnTheExposedFrontOnly` (exact) |
| **Fire interval** (lane N snipers): one volley per `FireIntervalSteps` | `NextRangedMs` | none | `SnipersFireAtMostOncePerFireInterval` |
| **Wound buffer**: deaths = floor(max(0, damage·KE + phase − buffer)/MaxHp) | damage wounds before it kills | `WoundBufferPermille` FITTED (the fit chose 0) | `WoundBufferDelaysDeathsAndStaysMonotone` |
| **Withdrawal time**: the exit field's least-cost distance (climb included) at catch-up pace | retreaters descend the `EdgeField` | none | via the battery |
| **DIR5 inactivity** (port of lane TR's predicate): hidden + no progress for N pulses → ROUTED unless (a) concealed with an enemy in reach, (b) Defend / own objective, (c) ambush waiting; whole-field Standoff (UNRATIFIED P-TR1); `LegacySideDisengage` still selectable | `HiddenStationaryRule` | none | `GenuinelyIdleHiddenFormationRoutsAtTheEndOfTheNthPulse` (N = 4, 5, 6: turn-exact with ±1), the three exemptions with the (a) boundary 79,200 / 79,201 mm, `TheLegacyRuleRemainsSelectableInModelTwo` |
| **Termination**: a valid attack's expected damage is floored at 1 milli-HP (a missile through dense cover may round to 0) | every tactical attack can consume HP | none | `AValidAttackAlwaysConsumesHpInModelTwo` |
| **Refusals**: vessels, aircraft, crew-served domain weapons, embarked formations → `NotSupportedException` (the Sprint 1 model silently ignored weapons) | — | none | `AutoRefusesDomainWeaponsAndEmbarkedFormations` |

**Default resolver** (`ResolverPolicy.Default`, new): a battle with a player participant → TacticalResolver; AI-vs-AI →
AutoResolver only when every formation is inside model 2 (land, no domain weapons, not embarked); any vessel / aircraft →
TacticalResolver, so **naval never defaults to the AutoResolver** (DIR5 §2.1). Test
`DefaultResolverIsAutoOnlyForAiVsAiLandBattles` (lane N line battle and amphibious battle → Tactical). No naval auto model
was built. OPEN POINT (not decided here): the M7.2/M7.3 revision's READING RDG-3 ("campaign AI-vs-AI naval battles use the
AutoResolver") cannot apply while no naval auto model exists.

**Which inactivity rule each side uses (brief note B).** In every lane-P battery comparison: TacticalResolver = its default
`HiddenStationary` (DIR5) with the Standoff end; AutoResolver model 2 = the ported `HiddenStationary` (DIR5) with the same
Standoff end; AutoResolver model 1 (Sprint 1, measured for comparison only) = `LegacySideDisengage`. **Correction (verification
round 1, P-V4):** until round 1 the model-2 port was NOT the same rule — "hidden" meant never known, a rout reset the Standoff
counter, and the rule came from an AutoResolver coefficient instead of the setup's `TacticalRules.Inactivity`; all three were
fixed in round 1 (§11) and none changed any battery outcome (battery hash identical, MEASURED). **Correction (verification round 2):** three
further differences remained — the first-detection credit went to the lowest-id formation (P2-V1), a tracked enemy's approach
was not progress (P2-V2), and exemption (a) ignored the setup's clause (P2-V4); all fixed in round 2, none moves a battery case
(MEASURED, §12).

## 4. Tuning (TUNING cases only: 20 tuning scenarios × seeds 1–80 = 1,600 cases)

`ParityCalibrationP.Fit` (deterministic coordinate descent; refuses any non-tuning case — tested) on the tactical outcomes of
the tuning seeds (`parityp run tuning`: 1,600 tactical battles, 65.4 s wall on 2 threads, outcome digest
`90E58DCB…F65D6914`). Loss per scenario = Σ 1000·gap/band over the rule-P1 checks + penalties for failed checks (raised
from 1000 to 4000 per failed check in the last fit so that passing checks dominates; the loss is a tool, not part of the
pre-committed rule). History (MEASURED; logs in the clone's `/dev/shm/m7c/P-fit*.log`, not committed):

| Step | Change | Tuning scenarios passing rule P1 (n = 80 vs 80) |
|---|---|---|
| Sprint 1 model (model 1, as shipped) | — | 7 / 20 (the Defend scenarios are attacked as if Normal; no terrain terms) |
| model 2, mechanisms only, Sprint 1 coefficients | path axis, terrain terms, detection, Defend doctrine | 8 / 20 |
| + per-entity detection aggregation; HoldEngage 200, MaxAttackersPerHolder 2000 (hand probe) | | 13 / 20 |
| fit 1 (4 sweeps): Ranged 300→600, KillEff 600→700, Noise 0→300, Surrender 50→25 | | 14 / 20 |
| + decision latency (pulse-start orders, no volleys on MoveTo) | | 15 / 20 |
| + convergence mobility (w = 1000), wound buffer axis; fit 3 | | 15 / 20 |
| + fire saturation; fit 4: KillEff 700→800, Noise 300→500, Surrender 25→0, WoundBuffer 25→0 (Ranged 1000, PerExposed 3000 start values kept) | | **16 / 20** — FROZEN |

**Frozen model-2 coefficients** (= the shipped defaults; `parityp eval … new` reproduces loss 48,769 and 16/20):
MeleeEngage 500, MaxAttackersPerTarget 500, RangedEngage 1000, KillEfficiency 800, Fortune 200, Noise 500, AmbushFirstStrikeSteps 0,
Surrender 0, HoldEngage 200, MaxAttackersPerHolder 2000, WoundBuffer 0, ConvergenceMobility 1000, RangedPerExposed 3000;
doctrine (not fitted) RetreatMorale 450, AmbushTrigger 3 cells. They are fitted to PLACEHOLDER tactical rules — not gameplay
values; re-fit (never hand-tune) when a tactical placeholder changes.

Tuning-set result of the frozen model (MEASURED, `parityp eval /dev/shm/m7c/P-tac-tuning.csv new`; T = tactical, Au = auto,
casualties and win rates in %, durations in s, bands in brackets):

| scenario | fam | n | win A/B/- % T | win A/B/- % Au | casA mean T/Au (band) | casB mean T/Au (band) | dur mean s T/Au (band) | pulses p50 T/Au | case agree % (indep. %) | verdict |
|---|---|---:|---|---|---|---|---|---|---|---|
| equal-infantry | OpenConditions | 80 | 35.0/57.5/7.5 | 37.5/55.0/7.5 | 33.8/33.8 (3.0) | 32.1/33.0 (3.1) | 207.7/157.4 (57.2) | 3/3 | 46.2 (45.3) | PASS |
| numbers-2to1 | OpenConditions | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 17.8/18.1 (3.1) | 39.0/36.0 (4.0) | 157.5/159.0 (23.6) | 3/3 | 100.0 (100.0) | PASS |
| low-supply | OpenConditions | 80 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 37.9/36.4 (3.0) | 12.1/16.7 (3.0) | 177.1/158.9 (26.6) | 3/3 | 100.0 (100.0) | FAIL(casB) |
| low-morale | OpenConditions | 80 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 15.9/15.2 (3.0) | 13.6/15.4 (3.1) | 152.9/145.8 (22.9) | 3/3 | 100.0 (100.0) | PASS |
| leadership-edge | OpenConditions | 80 | 88.7/11.2/0.0 | 97.5/0.0/2.5 | 35.8/37.6 (4.1) | 37.6/36.3 (3.0) | 210.2/160.2 (51.4) | 3/3 | 86.2 (86.5) | PASS |
| experience-edge | OpenConditions | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 20.0/17.7 (3.2) | 38.3/37.7 (3.0) | 164.8/150.4 (24.7) | 3/3 | 100.0 (100.0) | PASS |
| archers-vs-infantry-open | Composition | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 38.6/40.2 (3.0) | 141.3/139.1 (21.2) | 3/3 | 100.0 (100.0) | PASS |
| combined-arms | Composition | 80 | 36.2/50.0/13.7 | 38.7/30.0/31.2 | 41.8/38.9 (3.0) | 41.8/39.5 (3.0) | 208.1/159.1 (31.2) | 4/3 | 26.2 (33.3) | FAIL(duration) |
| riflemen-vs-infantry | Composition | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 37.9/37.9 (3.0) | 110.7/113.1 (16.6) | 2/2 | 100.0 (100.0) | PASS |
| sniper-screen | Composition | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 27.0/27.4 (4.0) | 37.0/35.9 (3.0) | 172.6/179.9 (25.9) | 3/3 | 100.0 (100.0) | PASS |
| shock-infantry | Composition | 80 | 41.2/52.5/6.2 | 40.0/51.2/8.7 | 33.5/33.4 (3.0) | 32.9/33.7 (3.0) | 196.0/157.2 (47.5) | 3/3 | 48.8 (44.0) | PASS |
| archers-vs-infantry-forest | Concealment | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 38.5/39.3 (3.0) | 255.0/212.9 (38.2) | 5/4 | 100.0 (100.0) | FAIL(duration) |
| jungle-ambush | Concealment | 80 | 42.5/43.7/13.7 | 32.5/48.7/18.7 | 34.3/34.2 (3.0) | 32.9/33.3 (3.1) | 397.1/337.9 (59.6) | 6/6 | 30.0 (37.7) | PASS |
| hill-crest | Terrain | 80 | 91.2/7.5/1.2 | 95.0/1.2/3.7 | 28.7/24.6 (3.0) | 36.4/36.1 (3.0) | 334.1/327.7 (50.1) | 6/6 | 87.5 (86.8) | FAIL(casA) |
| ford-crossing | Terrain | 80 | 23.7/63.7/12.5 | 45.0/47.5/7.5 | 35.4/33.3 (3.0) | 31.4/32.7 (3.1) | 333.1/328.9 (50.0) | 6/6 | 46.2 (41.9) | PASS |
| marsh-front | Terrain | 80 | 47.5/42.5/10.0 | 35.0/58.7/6.2 | 33.3/33.6 (3.0) | 32.6/31.8 (3.3) | 457.2/452.0 (68.6) | 8/8 | 45.0 (42.2) | PASS |
| forest-hold-archers | Terrain | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.1/0.0 (3.0) | 36.3/37.7 (3.0) | 313.9/275.4 (47.1) | 6/5 | 100.0 (100.0) | PASS |
| defile | Terrain | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.1/13.4 (3.0) | 37.1/37.3 (3.0) | 281.4/275.7 (42.2) | 5/5 | 100.0 (100.0) | PASS |
| archers-on-hill | Terrain | 80 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 38.8/39.8 (3.0) | 0.0/0.0 (3.0) | 248.4/244.5 (37.3) | 5/5 | 100.0 (100.0) | PASS |
| hold-line-open | Terrain | 80 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.1/13.4 (3.0) | 37.1/37.3 (3.0) | 281.4/275.7 (42.2) | 5/5 | 100.0 (100.0) | PASS |

The four tuning scenarios still failing at the freeze: low-supply (cas B 16.7 vs 12.1: the tactical engine punishes the weak
side harder than any linear exchange model here), combined-arms (duration 159 vs 208 s; draws 31 vs 14 %),
archers-vs-infantry-forest (duration 213 vs 255 s) and hill-crest (cas A 24.6 vs 28.7). They are analysed in §6.

**Freeze.** The model and its defaults were committed (this commit) BEFORE any acceptance-seed or control-seed battle was
run; the acceptance run follows in §5.

---

## 5. ACCEPTANCE RUN (held-out battery) — result: NOT ACCEPTED under the pre-committed rule P1

Run once, after the freeze commit `03c07cc`, in Release (`parityp run acceptance|control`, then `parityp accept … new` and
`… sprint1`; logs `/dev/shm/m7c/P-accept-release.log`, per-case CSV `P-accept-new-release.csv` — 4,640 rows, kept in the
clone's RAM directory, not committed). Rule P1 and every constant exactly as committed in §2 (nothing relaxed).

| Quantity | Value (MEASURED) |
|---|---|
| Acceptance battles | 29 scenarios × 160 seeds (5001–5160) = **4,640 tactical + 4,640 auto** (+ 1,440 tactical control battles, seeds 9001–9160); tactical wall 203.7 s + 65.9 s on 2 threads; auto 3.3 s single thread (0.71 ms/battle) |
| Attainability control (tactical vs its own control sample) | **29 / 29 PASS** — no scenario is inconclusive. *Round-1 qualification (P-V6):* the 20 tuning scenarios were checked against their n = 80 tuning sample, i.e. against WIDER bands (win band 24.6 points at 50 %) than the candidate faced; round 1 ran a fresh n = 160 tactical control sample of the tuning scenarios (control seeds 9001–9160) and the self-check at the candidate's band sizes also passes **20 / 20** (held-out 9 / 9 already at n = 160) — MEASURED, §11 |
| **Model 2 (lane P), held-out scenarios** | **4 / 9 pass** |
| Model 2, tuning scenarios on fresh seeds | 12 / 20 pass |
| **Model 2, battery** | **16 / 29 → NOT ACCEPTED** (one failing check anywhere rejects) |
| Model 1 (Sprint 1, for comparison), held-out / tuning-fresh / battery | 3 / 9, 8 / 20, **11 / 29** |
| Inactivity rule per side | Tactical: DIR5 hidden-stationary + Standoff; auto model 2: ported DIR5 + Standoff; auto model 1: legacy side-disengage. Tactical end reasons over 4,640 battles: SideEliminated 4,523, BothEliminated 115, **Standoff 2** (no hidden-stationary rout); auto model 2: SideEliminated 4,423, BothEliminated 217. Inactivity ends are therefore negligible in this battery (2 / 4,640) |
| BATTERY HASH (model 2; tactical + auto outcome of every acceptance case, case order) | **`18222D80D7E3A5E69BE133B68D0E00A79E768694D3667DF3CF2CBF46740CD009`** (§7 for Debug/Release and repeat identity) |
| Tactical acceptance digest / auto (model 2) digest | `12961E15…2CFD0D6D` / `E6F232E3…4D05EEBC` |
| Battery hash with model 1 | `D04E12BF…D3A4F522` |

**Checks passing per metric (of 29 scenarios)** — the metric-level picture behind the scenario count:

| Metric | V victor | W win rates | CA cas A | CB cas B | D duration | P pulses | scenarios passing all |
|---|---:|---:|---:|---:|---:|---:|---:|
| Model 1 (Sprint 1) | 24 | 23 | 20 | 23 | 15 | 22 | 11 |
| **Model 2 (lane P)** | **28** | **24** | **28** | **25** | **21** | **28** | **16** |

**Per family (model 2, scenarios passing / total):** open conditions 5 / 9 (held-out 2 / 3); composition 3 / 7 (held-out 0 / 2);
concealment 2 / 3 (held-out 1 / 1); terrain 6 / 10 (held-out 1 / 3).

### 5.1 Held-out scenarios (decide acceptance), model 2, n = 160 per resolver

| scenario | fam | n | win A/B/- % T | win A/B/- % Au | casA mean T/Au (band) | casB mean T/Au (band) | dur mean s T/Au (band) | pulses p50 T/Au | case agree % (indep. %) | verdict |
|---|---|---:|---|---|---|---|---|---|---|---|
| numbers-5to4 | OpenConditions | 160 | 82.5/14.3/3.1 | 81.2/10.6/8.1 | 28.9/29.6 (3.0) | 38.0/37.4 (3.0) | 179.9/159.8 (27.0) | 3/3 | 69.4 (68.8) | PASS |
| mixed-conditions | OpenConditions | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 12.4/14.7 (3.0) | 29.6/29.2 (3.0) | 163.3/150.2 (24.5) | 3/3 | 100.0 (100.0) | PASS |
| meeting-asymmetric | OpenConditions | 160 | 65.0/31.8/3.1 | 42.5/40.0/17.5 | 31.4/33.7 (3.0) | 32.4/34.1 (3.0) | 277.1/165.7 (60.3) | 4/3 | 36.9 (40.9) | FAIL(win,duration) |
| combined-arms-vs-mass | Composition | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 13.9/13.3 (3.0) | 39.5/41.0 (3.0) | 194.4/150.2 (29.2) | 4/3 | 100.0 (100.0) | FAIL(duration) |
| riflemen-vs-archers | Composition | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 43.1/39.7 (3.0) | 111.1/108.5 (16.7) | 2/2 | 100.0 (100.0) | FAIL(casB) |
| forest-ambush-archers | Concealment | 160 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 37.8/38.6 (3.0) | 0.0/0.0 (3.0) | 272.9/269.4 (40.9) | 5/5 | 100.0 (100.0) | PASS |
| high-ridge | Terrain | 160 | 26.8/65.0/8.1 | 0.0/100.0/0.0 | 35.3/34.7 (3.0) | 30.4/21.1 (3.0) | 378.5/428.4 (56.8) | 7/7 | 65.0 (65.0) | FAIL(win,casB) |
| narrow-ford-archers | Terrain | 160 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 38.3/40.3 (3.0) | 0.0/0.0 (3.0) | 248.5/244.9 (37.3) | 5/5 | 100.0 (100.0) | PASS |
| urban-street | Terrain | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.3/12.0 (3.0) | 37.3/33.6 (3.0) | 246.8/324.0 (37.0) | 5/7 | 100.0 (100.0) | FAIL(casB,duration,pulses) |
scenarios passing: 4/9

### 5.2 Tuning scenarios on the fresh acceptance seeds, model 2, n = 160 per resolver

| scenario | fam | n | win A/B/- % T | win A/B/- % Au | casA mean T/Au (band) | casB mean T/Au (band) | dur mean s T/Au (band) | pulses p50 T/Au | case agree % (indep. %) | verdict |
|---|---|---:|---|---|---|---|---|---|---|---|
| equal-infantry | OpenConditions | 160 | 46.8/45.0/8.1 | 45.0/35.0/20.0 | 33.6/33.8 (3.0) | 33.4/34.4 (3.0) | 207.9/157.9 (40.3) | 3/3 | 38.1 (38.5) | FAIL(win,duration) |
| numbers-2to1 | OpenConditions | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 16.5/17.6 (3.0) | 38.9/36.0 (4.0) | 156.4/158.8 (23.5) | 3/3 | 100.0 (100.0) | PASS |
| low-supply | OpenConditions | 160 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 38.2/36.7 (3.0) | 12.9/17.3 (3.0) | 177.6/159.6 (26.6) | 3/3 | 100.0 (100.0) | FAIL(casB) |
| low-morale | OpenConditions | 160 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 15.9/14.8 (3.0) | 14.9/15.0 (3.0) | 153.2/145.8 (23.0) | 3/3 | 100.0 (100.0) | PASS |
| leadership-edge | OpenConditions | 160 | 90.6/9.3/0.0 | 97.5/1.8/0.6 | 34.8/36.1 (3.0) | 37.9/36.4 (3.0) | 205.5/159.7 (35.3) | 3/3 | 90.0 (88.5) | FAIL(duration) |
| experience-edge | OpenConditions | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 18.1/17.2 (3.0) | 38.2/37.7 (3.0) | 163.8/150.3 (24.6) | 3/3 | 100.0 (100.0) | PASS |
| archers-vs-infantry-open | Composition | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 38.4/39.7 (3.0) | 141.3/138.8 (21.2) | 3/3 | 100.0 (100.0) | PASS |
| combined-arms | Composition | 160 | 45.0/41.2/13.7 | 24.3/33.1/42.5 | 41.5/39.9 (3.0) | 42.0/39.3 (3.0) | 209.1/159.0 (31.4) | 4/3 | 29.4 (30.5) | FAIL(win,duration) |
| riflemen-vs-infantry | Composition | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 37.9/37.8 (3.0) | 110.7/114.5 (16.6) | 2/2 | 100.0 (100.0) | PASS |
| sniper-screen | Composition | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 26.0/27.8 (3.0) | 37.5/36.5 (3.0) | 173.5/167.8 (26.0) | 3/3 | 100.0 (100.0) | PASS |
| shock-infantry | Composition | 160 | 49.3/41.2/9.3 | 52.5/31.8/15.6 | 33.4/33.3 (3.0) | 34.2/34.6 (3.0) | 200.5/157.8 (36.0) | 3/3 | 37.5 (40.5) | FAIL(duration) |
| archers-vs-infantry-forest | Concealment | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 38.6/40.2 (3.0) | 254.6/212.8 (38.2) | 5/4 | 100.0 (100.0) | FAIL(duration) |
| jungle-ambush | Concealment | 160 | 47.5/42.5/10.0 | 45.0/39.3/15.6 | 34.3/33.6 (3.0) | 33.1/33.8 (3.0) | 393.9/337.3 (59.1) | 6/6 | 37.5 (39.7) | PASS |
| hill-crest | Terrain | 160 | 87.5/8.7/3.7 | 98.7/1.2/0.0 | 28.6/23.4 (3.0) | 36.4/36.0 (3.0) | 334.3/326.3 (50.1) | 6/6 | 87.5 (86.5) | FAIL(casA) |
| ford-crossing | Terrain | 160 | 28.7/63.1/8.1 | 53.7/37.5/8.7 | 35.2/32.6 (3.0) | 30.9/33.7 (3.0) | 332.6/328.4 (49.9) | 6/6 | 43.8 (39.8) | FAIL(victor,win) |
| marsh-front | Terrain | 160 | 48.7/45.6/5.6 | 43.7/49.3/6.8 | 33.3/32.9 (3.0) | 33.1/32.9 (3.0) | 456.6/452.2 (68.5) | 8/8 | 47.5 (44.2) | PASS |
| forest-hold-archers | Terrain | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 (3.0) | 36.2/37.9 (3.0) | 307.1/275.1 (46.1) | 6/5 | 100.0 (100.0) | PASS |
| defile | Terrain | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.8/12.9 (3.0) | 37.2/37.2 (3.0) | 281.2/275.1 (42.2) | 5/5 | 100.0 (100.0) | PASS |
| archers-on-hill | Terrain | 160 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 38.5/40.2 (3.0) | 0.0/0.0 (3.0) | 248.2/244.5 (37.2) | 5/5 | 100.0 (100.0) | PASS |
| hold-line-open | Terrain | 160 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.8/12.9 (3.0) | 37.2/37.2 (3.0) | 281.2/275.1 (42.2) | 5/5 | 100.0 (100.0) | PASS |
scenarios passing: 12/20

Reading the columns: win % per outcome (A / B / neither); casualty means in % of the role's start strength with the band in
brackets; duration mean in s with its band; the pulse median; "case agree" = per-case same-winner rate with the
independence expectation Σ p_T·p_Au in brackets (the two resolvers draw independent randomness, so per-case agreement
cannot exceed that expectation in balanced scenarios; it is reported, not a criterion).

### 5.3 Model 1 (Sprint 1) on the same battery, for comparison

Held-out: 3 / 9 (passes numbers-5to4, mixed-conditions, forest-ambush-archers); tuning scenarios on fresh seeds: 8 / 20. It
fails every terrain-defence scenario on the victor (it treats a Defend line as an attacker) and most terrain durations
(no climb, no Defend doctrine). Full tables: `/dev/shm/m7c/P-accept-sprint1-release.md`.

### 5.4 Verdict

**The AutoResolver is NOT at parity under the proposed rule P1**: 16 of 29 scenarios pass (4 of the 9 held-out ones). The
improvement over the Sprint 1 model is real and measured on the same held-out battery (11 → 16 scenarios; victor 24 → 28,
casualties A 20 → 28, duration 15 → 21, pulses 22 → 28), but parity is not claimed. The held-out battery is now CONSUMED:
any further model change must be judged on a NEW held-out battery (new seeds and new scenarios), pre-committed the same way.

---

## 6. Limitations — where the AutoResolver does not (or structurally cannot) match, with evidence

Status words as above. Numbers are from the acceptance run (§5, n = 160 per resolver) unless stated.

1. **Duration — the dominant failing metric (8 of 29 scenarios).** Two components, both MEASURED from the per-case CSV:
   - *a long tail the auto model has no mechanism for*: in meeting engagements 9–29 % of tactical battles last more than
     300 s (equal-infantry 18 / 160, leadership-edge 15 / 160, shock-infantry 14 / 160, meeting-asymmetric 47 / 160; p90
     486 s), the auto model never (0 / 160; p90 160–168 s). INFERRED cause, not isolated: after the first rout wave the
     tactical fight fragments (entities scattered, pursuit, re-engagement of the remaining formations) — a single 1-D contact
     front cannot represent a second phase of the fight;
   - *a bulk offset*: even without the tail the auto battle ends 40–50 s early in combined-arms (median 158 vs 208 s),
     combined-arms-vs-mass (150 vs 192 s) and archers-vs-infantry-forest (212 vs 254 s). INFERRED: per-entity range and
     target tracking (only tracked entities can be shot; rear ranks out of range) slow the tactical exchange; the aggregate
     volley does not.
   - *the opposite case — withdrawal escape (urban-street, auto LONGER, 324 vs 247 s; p50 392 s, 88 / 160 battles > 300 s)*:
     MEASURED on seed 5002 (`parityp diag 109 5002`): the defender's morale stops at 320 (above the rout threshold 300,
     below the doctrine's 450), it withdraws at the next pulse start, and the auto battle then waits 154 s for the
     withdrawal to reach the exit edge; in the tactical battle the retreating entities stay in contact, keep losing men and
     rout (244 s). The auto model applies no pursuit damage to a withdrawing formation — a missing mechanism (§9 P-P4).
2. **Simultaneous routs → too many draws in balanced fights.** Neither side holds the field: equal-infantry 32 / 160 auto vs
   13 / 160 tactical; combined-arms 68 vs 22; meeting-asymmetric 28 vs 5. Already found in Sprint 1 (parity-report §6.1).
   The per-formation casualty phase and the noise term reduce it but cannot remove it: the aggregate morale of two equal
   formations crosses the rout threshold within the same step far more often than the tactical engine's, where the winner
   breaks contact first. This is the main cause of the win-rate failures of the balanced scenarios.
3. **The tactical engine punishes the weaker side more than any linear exchange.** low-supply: the stronger side's losses are
   12.9 % tactical vs 17.3 % auto; riflemen-vs-archers: the losers' 43.1 vs 39.7 %; high-ridge cas B 30.4 vs 21.1 %. A
   wound buffer (deaths convex in damage) was implemented and offered to the fit; it fixes low-supply on the tuning set but
   breaks others (tuning probes with buffer 50 and 100 permille, both with KillEfficiency 1000: 11 / 20 and 8 / 20 tuning
   scenarios passing, against 13–15 / 20 without), and the fit set it to 0. INFERRED: the tactical non-linearity comes from focus fire and local many-on-one fights (square-law-like),
   which an aggregate engaged share does not reproduce in all regimes at once.
4. **Slope magnitude is not one coefficient away.** At the 10 m crest the auto attacker loses too little (28.6 % tactical vs
   23.4 % auto, hill-crest); at the 20 m ridge (gradient at the cap 1) the auto defender always wins (100 % vs 65 % tactical,
   high-ridge, held-out). The same mechanisms (TR's slope term + the convergence-mobility cap 1/(cost + climb)) undershoot
   at 10 m and overshoot at 20 m: the tactical effect of height is not monotone-proportional in the way the aggregate model
   is. Structural with the current mechanisms (MEASURED both ways; cause INFERRED).
5. **Ford victor.** ford-crossing: the tactical defender wins 63 %, the auto defender 37.5 % (the modal-victor check fails).
   On the 80 tuning seeds it passed (51 vs 64 %, inside the wider n = 80 band); on 160 fresh seeds the gap is significant.
6. **Manoeuvre and geometry the 1-D axis cannot represent.** Formations are points on one route; lateral offsets, flanks,
   encirclement and two formations converging from different sides are projected onto the axis (combined-arms-vs-mass: B's
   two formations 80 m apart become one front; duration fails). Any battle decided by manoeuvre rather than by exchange
   on a front is outside the model by construction.
   *Round-2 correction (P2-V3):* until round 2 the model-2 axis ALSO clamped every projection to the segment between the
   two mean anchors, so a rear echelon deployed in depth was moved up to its side's mean anchor (the battery's 40 m depths
   became 20 m) — a regression against model 1 that this item did not disclose. Fixed in round 2 (the axis is extended
   beyond both ends); its measured battery effect is in §12.2. Lateral offsets remain projected (unchanged).
7. **Battery coverage gaps (disclosed, measured).**
   - The PATH AXIS is not exercised by any battery outcome: every terrain feature straddles the straight line between the
     armies, so the planned route equals the straight one. Mutant M14 (straight axis instead of the path) changed no
     battery-subset golden; only the axis unit test killed it (§8). A ford or gap off the straight line is untested at
     battle level.
   - The DEFILE is degenerate: its tactical statistics are identical to the flat hold-line control (11.8 / 37.2 %, 281.2 s
     in both) — a 1-cell gap does not constrain a 10-wide, 13.5 m block, so the frontage-width mechanism is not exercised
     by the battery (the auto model has no defile-width term; none was needed here).
   - Charge impetus has little tactical effect at these placeholders (shock-infantry ≈ equal-infantry: 49 / 41 vs 47 / 45 %).
8. **Doctrine coupling.** The model assumes the `KnowledgeAi` doctrine (including the new Defend posture and the
   pulse-start decision timing). Any other controller — a player's orders, a different AI — needs its own parity check.
9. **Domains.** Land only. Vessels, aircraft, crew-served domain weapons and embarked formations are refused; such battles
   default to the TacticalResolver (`ResolverPolicy`). No naval or air parity was measured, so none is claimed.
10. **Placeholders.** Every tactical number (unit stats, terrain table, morale, cell size) is a PLACEHOLDER; the fitted
    coefficients follow them and must be re-fitted, never hand-tuned, when they move. Cross-platform equality of any hash
    here is UNTESTED (CR-013); Debug/Release and repeat identity on this machine is MEASURED (§7).
11. *(added in verification round 1)* **Terrain is read at one point per formation.** Model 2 evaluates lane TR's terms at
    each formation's single axis point; it does not aggregate terrain over the battle map (cover share, forest / urban share,
    river crossings) and has NO frontage-width term — the brief's item (2) is only partly implemented (P-V3). The defile
    scenario cannot show the gap (identical to hold-line-open in both resolvers, 160 / 160 cases). *Round 2 (P2-V7):
    unchanged; why a fix round does not add it, and a PROPOSAL, in §12.4.*
12. *(round 1)* **Targeting still uses "ever known".** Since round 1 the DIR5 hidden conjunct reads current tracking (lost
    without line of sight or contact, as in the tactical engine), but model 2's target choice and orders still use the
    permanent "known" set (the tactical engine targets tracked entities only). Changing targeting is a model change; no
    battery case is known to be affected (battery hash identical with and without the tracking state), not measured beyond that.
13. *(round 1)* **Formation point vs front rank.** The lose-track pin agrees with the tactical engine for N = 3 and 4 idle
    pulses but not for N = 5 (MEASURED: tactical — the front rank regains sight on the ridge, contact, side 0 eliminated after
    10 pulses; auto — the formation point is still in the valley, the hidden formation routs after 6 pulses). Sight from a
    formation's point is a structural approximation of sight from its leading entities.
14. *(round 1)* **One legacy-rule difference remains on model 2.** Re-running the Sprint 1 rule tests on model 2 (temporary
    edit, MEASURED): 46 / 49 pass (13 failed before round 1). The 3 differences: the withdrawal-time pin ×2 (by design — model
    2 measures the exit field to the edge cell's centre, 280,000 / 380,000 mm, model 1 the straight line to the edge line,
    290,000 / 390,000 mm; model 2's own pin is in `AutoResolverPRuleTests`) and the clause-off variant of
    `HiddenAmbusherFacingAnAdvancingEnemyStaysAndSprings`, seed 3 of 4 (model 2 reaches the ambusher before the legacy five idle
    pulses elapse; cause INFERRED — model 2's path-axis march and catch-up closing — not isolated).

---

## 7. Repeatability (MEASURED)

| Run | Configuration / schedule | Tactical acceptance digest | Tactical control digest | Tactical tuning digest | Auto (model 2) acceptance digest | BATTERY HASH |
|---|---|---|---|---|---|---|
| 1 | Release, 2 threads | `12961E15…2CFD0D6D` | `F1B471C5…9C61E134` | `90E58DCB…F65D6914` | `E6F232E3…4D05EEBC` | `18222D80…740CD009` |
| 2 | Debug, 2 threads | identical | identical | identical | identical | identical |
| 3 | Release, 1 thread (repeat; control and tuning CSVs of run 1 reused) | identical (375.0 s single-thread wall) | — | — | identical | identical |

Full hashes: battery `18222D80D7E3A5E69BE133B68D0E00A79E768694D3667DF3CF2CBF46740CD009`; tactical acceptance
`12961E1535883875F531C26F9742AEB8FB96C72E2E4796F82586334F2CFD0D6D`; auto acceptance
`E6F232E3F0B10ED8CDD0C2348706FDFA14E0AD96CDE8364CC0DD8D674D05EEBC`; tuning `90E58DCB18DEA369EB1E2464D09DF590482D7473FFB018B550C62FCBF65D6914`;
control `F1B471C5C1AF23CBE3DFF7F346002BD6798F1E6A683BB730EA85DD419C61E134`. The lane-P subset goldens
(`parityp subset`) are identical in Debug and Release. Every case is an independent pure function of (scenario, seed); the
bench's parallel schedule only changes wall time. Cross-platform (Windows / ARM) equality is UNTESTED (CR-013).

**Cost (small bench, MEASURED on this shared 4-CPU host, nice 19):** tactical 4,640 acceptance battles: 407 s summed
(87.8 ms / battle) in Release, 1,433 s (309 ms) in Debug; the AutoResolver model 2: 3.29 s for 4,640 battles
(0.71 ms / battle, single thread, Release), model 1 3.18 s (0.68 ms). Model 2 costs about the same as model 1 and ~120×
less than the tactical engine.

---

## 8. Tests and mutants

**New tests (all MEASURED passing in Release and Debug, §7):** `ParityPTests` (16: rule P1 hand-computed bands and exact
boundaries of every check incl. the modal threshold, tie-dense statistics, populated outcome extraction, disjoint seed sets
sized as committed, calibration refuses non-tuning cases, deployment legality of every new scenario in both orientations,
exact mirror images, defender front-rank geometry, the lane-P subset goldens) and `AutoResolverPTests` (§3 table; incl. the
turn-exact DIR5 pins with ±1 neighbours, two decision-latency pins, the Defend order-delivery pin, the C37 metamorphic terrain
test `FavourableDefensiveTerrainNeverRaisesTheDefendersLosses`, the personal-concealment metamorphic test, input-order and
orientation invariance and repeatability of model 2). The Sprint 1 test files pin `AutoCoefficients.Sprint1` and pass
unchanged (bit-exactness of model 1, golden `87BB6323…`).

**Goldens (re-pin attribution):** `ParityBatteryTests.GoldenAutoSubset` did NOT move (model 1 pinned explicitly).
`ParityPTests.GoldenTacticalSubsetP` = `F8E2634E…05837E` and `GoldenAutoSubsetP` = `D10B68EB…AD1A6415` are NEW (lane-P
subset: 5 scenarios × tuning seeds 1–4; the auto one pins the frozen model-2 defaults). No existing golden moved.

**Mutants** (prioritized subset of 20, one full-suite run each, in a separate source copy `/dev/shm/m7c/P-mut`; bound = 10×
the clean suite wall time; clean suite 689 / 0 / 0 in 17.5 s for round 1, 691 / 0 / 0 in 11.6 s for round 2). "Named
semantic killer" = the test written for that mechanism; golden-only kills are not counted.

| # | Mutation | Verdict | Named semantic killer |
|---|---|---|---|
| M1 | detection without the per-entity aggregation (AnyOf returns p) | KILLED | `AnyOfIsOneMinusTheMissProduct` |
| M2 | melee slope sign reversed | KILLED | `CrestMeleeUsesTheTacticalSlopeTermsExactly` |
| M3 | missile cover ignored | KILLED | `ArcheryIntoForestPaysTheTacticalCoverExactly` |
| M4 | cohesion read from the other formation's cell | KILLED | `FordWaderFightsWithoutFootingOrOrder` |
| M5 | no Naismith climb in movement | KILLED | `ClimbingTheTerraceSlowsTheAdvanceByNaismithExactly` |
| M6 | Defend formations obey the attack doctrine | KILLED | `DefendFormationNeverAdvances` (+ 7 others) |
| M7 | orders re-decided every step (no pulse-start latency) | round 1: killed by the subset GOLDEN ONLY — `OrdersChangeOnlyAtPulseStarts` had no teeth against it (disclosed; kept as a property test); round 2: KILLED | `AHaltedFormationAttacksOnlyFromTheNextPulseStart` (added for it) |
| M8 | fire interval ignored | KILLED | `SnipersFireAtMostOncePerFireInterval` |
| M9 | DIR5 exemption (b) Defend removed | KILLED | `ExemptIdleFormationsEndTheBattleAsAStandoff` (+ 3) |
| M10 | whole-field Standoff removed | **HANG — non-termination under this mutation** (suite stopped at the 10× bound, 175 s); the named test fails through its 60 s harness guard | `ExemptIdleFormationsEndTheBattleAsAStandoff` |
| M11 | exemption (a) reach boundary off by one | KILLED | `ConcealedWithAnEnemyInReachIsExemptExactlyToTheReach` |
| M12 | convergence mobility not capped at open ground | KILLED | `ConvergenceCapFollowsTheTerrainMobility` |
| M13 | fire saturation on the whole formation, not its front | KILLED | `FireSaturatesOnTheExposedFrontOnly` |
| M14 | path axis replaced by the straight axis | KILLED (only by the axis test: no battery outcome moved — limitation 7) | `PathAxisRunsThroughTheFordTheGapAndTheStreet` |
| M15 | naval / air battles default to auto | KILLED | `DefaultResolverIsAutoOnlyForAiVsAiLandBattles` |
| M16 | mean band boundary exclusive | KILLED | `MeanComparisonIsExactAtTheBoundary` |
| M17 | modal threshold exclusive | KILLED | `ModalRuleBindsOnlyAtTheThreshold` |
| M18 | held-out scenarios admitted to the calibration | KILLED | `CalibrationRefusesEveryNonTuningCase` |
| M19 | no mirrored orientation | KILLED | `SeedSetsAreDisjointAndSizedAsCommitted` (+ mirror test) |
| M20 | KnowledgeAi never orders the explicit Hold (Defend) | KILLED | `DefendHoldIsDeliveredAtPulseZeroOnly` |

Round 1 had six mutants (M1, M5, M7-teeth, M9, M14, M20) whose first textual form did not compile (unreachable-code
warnings are errors here) or had no semantic kill; they were re-expressed and re-run in round 2 (log
`/dev/shm/m7c/P-mutants2.log`). Every mutated file was restored and byte-compared after each run. Both "teeth" and "aim"
(ADR-015 §7.2): each killer asserts a property the model ought to have (an exact lane-TR coefficient, a DIR5 boundary, a
pre-committed rule constant), not an incidental value.

## 9. Proposals and open points for the Director (decided by nobody here)

- **P-P1 (D-079 Q4): adopt rule P1 as the AutoResolver acceptance criterion**, or amend it — §2 (substantive bands 10
  points of win rate / 3 points of strength / 15 % of duration / 1 pulse; sampling floors at z = 3.6 from the tactical
  spread; n ≥ 160 independent battles per scenario; every scenario must pass; held-out scenarios decide; tactical
  self-check). The rule proved attainable (29 / 29 self-checks; since round 1 also at the candidate's n = 160 bands for every
  scenario) and discriminating (model 1: 11 / 29, model 2: 16 / 29). **What the Director would be adopting (round-1
  correction, P-V6):** at n = 160 the binding win-rate band in balanced scenarios is the SAMPLING FLOOR — 10.4 to 20.2 points
  (17.5 at a 25 % or 75 % rate, 20.2 at 50 %), not 10 points; the substantive 10 points binds only below ~6.6 % or above
  ~93 %. The duration floor reaches 19.4 % of the mean. Tighter bands need larger samples (the floor scales with
  1/sqrt(n): n = 640 per resolver halves it), not a smaller z.
- **P-P6 (round 1, PROPOSAL): a distribution-shape check for a future rule (P2), never applied retroactively.** P1 compares
  means, the pulse median and class rates only; a model can pass them while missing a tail (limitation 1: tactical duration
  p90 486 s, auto 160–168 s). Candidate: a quantile check on duration (e.g. |p90_C − p90_R| within a sampling band computed by
  order statistics of the reference), pre-committed with the next held-out battery. Not decided here.
- **P-P2: the AutoResolver is not adoptable as the AI-vs-AI default on this evidence** if parity is required for that use;
  the default-resolver policy implemented here (`ResolverPolicy`) follows the brief (AI-vs-AI → auto, naval / player →
  tactical) and is a default only. Whether AI-vs-AI may use a non-parity auto model meanwhile is the Director's call.
- **P-P3: `Posture.Defend`** (explicit Hold at pulse 0 for KnowledgeAi) is a new doctrine input; the terrain-defence
  scenarios need it. Keep / rename / fold into an order-based deployment (D-079 deployment work) — Director or integrator.
- **P-P4 (next model work, needs a NEW held-out battery):** pursuit damage on withdrawing formations (limitation 1, measured
  cause of the urban-street failure); a second-phase / fragmentation term for the long duration tail; a draw-breaking
  mechanism for simultaneous routs; a height response that is not proportional (limitation 4). Each must be pre-committed
  and judged on fresh scenarios and seeds — the present held-out battery is consumed.
- **P-P5: battery coverage** — add a route that bends (ford off the straight line), a defile narrower than a formation's
  frontage, and a scenario where impetus matters, before the next acceptance run.
- **Open (RDG-3 reading):** campaign AI-vs-AI NAVAL battles cannot use the AutoResolver while no naval auto model exists;
  `ResolverPolicy` sends them to the TacticalResolver. A naval auto model, if wanted, needs its own battery (never the
  default per DIR5 §2.1).

## 10. Reproduce, files, git

```bash
cd prototypes/m7-sprint1
dotnet build M7Proto.slnx -c Release -m:1
B=Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll
dotnet $B parityp run tuning tuning.csv 2            # 1,600 tactical battles (~65 s on 2 threads)
dotnet $B parityp eval tuning.csv new                # tuning-set table (§4); 'sprint1' for model 1
dotnet $B parityp fit tuning.csv 4 "new"             # re-fit (tuning cases only; refuses anything else)
dotnet $B parityp run acceptance acc.csv 2           # 4,640 tactical battles (~200 s)
dotnet $B parityp run control ctl.csv 2              # 1,440 tactical battles
dotnet $B parityp accept acc.csv ctl.csv tuning.csv new out.md out.csv    # §5, battery hash
dotnet $B parityp diag <scenario id> <seed> new      # one case, both resolvers, timelines
dotnet $B parityp subset                             # the ParityPTests golden digests
```

Files (all under `prototypes/m7-sprint1`): NEW `Proto.M7/Tactical/{ParityBatteryP, ParityCriteriaP1, ParityCalibrationP,
AutoAxis, ResolverPolicy}.cs`, `Proto.M7.Bench/ParityPBench.cs`, `Proto.M7.Tests/Tactical/{ParityPTests,
AutoResolverPTests}.cs`, `reports/H-P.md`; round 1 added `Proto.M7/Tactical/ParityPCsv.cs` and
`Proto.M7.Tests/Tactical/AutoResolverPRuleTests.cs` (round-1 bench additions: `parityp run control-tuning <out.csv>` and
`parityp selfcheck <reference.csv> <control.csv>`). CHANGED: `Proto.M7/Tactical/AutoResolver.cs` (model 2; model 1 kept),
`Control.cs` (KnowledgeAi Defend, `AiReason.HoldingLine` appended), `TacticalTypes.cs` (`Posture.Defend` appended),
`Proto.M7.Bench/Program.cs` (one dispatch line), and the three Sprint 1 auto test files (pinned to
`AutoCoefficients.Sprint1`; only the coefficient source changed, no assertion's meaning). Not touched: `TacticalBattle*.cs`, `WarTheaterWorld.cs`, `Runner.cs`,
`EventScheduler.cs`, everything outside `prototypes/` (D-079 Q5).

Test counts (MEASURED): baseline `6bd3aa2` 625 / 0 / 0 (Release and Debug); final 691 / 0 / 0 in Release and in Debug.
After verification round 1: **733 / 0 / 0 in Release and in Debug** (§11). *(Corrected in round 2, P2-V8: this line said 728; the
measured round-1 count is 733, as §0 and §11.4 state.)* After verification round 2: **775 / 0 / 0** (§12).

---

## 11. Verification round 1 (fix agent, 2026-11-06)

Tree under review: `bd93586` (lane/P). Every finding was REPRODUCED against that tree before any change (probes in a scratch
file never committed; mutants in a separate source copy `/dev/shm/m7c/P-r1-mut`, one full-suite run each, bound 10× the clean
suite). Fixes are minimal; each has a named semantic test. Status words as in the header.

### 11.1 Verdicts

| Finding | Verdict | Reproduction evidence (MEASURED on `bd93586`) | Fix and evidence (MEASURED after the fix) |
|---|---|---|---|
| **P-V1** (major) step-0 reveal: `LastFoughtStep = -1` and `LastFoughtStep == k - 1` revealed every formation at step 0 | **REPRODUCED** | archers vs Ambush HI in forest, `TT.HideRules`: model 2 first-known step 0, damage 1,166 / 1,096 / 1,220 (seeds 1/2/3), SideEliminated after 1 pulse; tactical 0 damage, BothDisengaged after 5; model 1 never detects. Mutant V3 (revealed exception dropped) survived 691/0/0 | guard `LastFoughtStep >= 0` (`AutoResolver.cs`). Same probe: model 2 never detects, 0 damage, BothDisengaged after 5 pulses = tactical. Tests `StationaryTargetInConcealingTerrainIsNeverFoundByModelTwo` (6 cases: both rules, both resolvers, same end) and `AConcealedShooterIsRevealedExactlyInTheStepAfterItsVolley` (sniper team with personal concealment 0: volley at 55, known at exactly 56; earlier reveals out of sight; draw chances 0 before 56) |
| **P-V2** (major) Slice E rules untested on the default model 2; false header claim | **REPRODUCED** | Sprint 1 rule tests pointed at model 2 (temporary edit, reverted and byte-compared): **13 / 49 fail** — exactly the verifier's list. `AutoResolverPTests` had no model-2 test of trapped surrender, impassable withdrawal, stationary factor, hold recovery or withdrawal time. Mutant V6 killed by a golden only | new `AutoResolverPRuleTests`: trapped-rout surrender and impassable withdrawal (both resolvers), hold-morale recovery under the legacy AND the DIR5 rule (both resolvers: same end, pulse, EndMs, morale 900 / 1000), exact model-2 withdrawal time (exit field 280,000,000 / 380,000,000 cost-mm at 3,900 mm per step → 72 / 98 steps → 142,000 / 194,000 ms), strict rout threshold. Header corrected in `AutoResolverVerificationFixTests`. The same temporary edit after round 1: **3 / 49 fail** (limitation 14: two by design, one legacy-rule case not isolated) |
| **P-V3** (major) item (2) only partly implemented but marked done; no frontage term; defile duplicates the control; impetus untested | **REPRODUCED** | `MeleeHitV2` / `MissileHitV2` read `Pos(a)` / `Pos(t)` only; no width / frontage term in `AutoResolver.cs`; defile vs hold-line-open acceptance outcomes identical **160 / 160** (tactical) and **160 / 160** (auto); mutant V2 (impetus advance 0) survived | §0 row (2) now **PARTIAL** with what is not implemented; §0 row (3) gives the distinct count 15 / 28; limitation 11. Test `ChargeImpetusEntersTheModelTwoMeleeAttackFactorExactly` (first charge factor exactly 3/2 = ImpetusFactor(1/2, 3,900, 3,900); One while standing; HI never). **Not implemented (out of a fix round's scope; a model change that needs a new held-out battery):** battle-map aggregation and a frontage-width mechanism (§9 P-P5) |
| **P-V4** (major) DIR5 port ≠ tactical predicate | **REPRODUCED** (a), (b), (c) | (a) `Hidden(i) => !_known[...]`, `_known` set-only; (b) Standoff-after-rout probe: tactical Standoff after **9** pulses (N = 4) / **11** (N = 5), auto **8** / **10**; tactical trace shows G is not reset at the rout pulse and the routing members' approach to their edge is progress in the next pulse (G = 4 → 0); (c) `_c.Inactivity` read, no `_r.Inactivity`; hold-recovery setup under `TT.Legacy`: tactical BothDisengaged, auto BothEliminated. Mutant V1 (`Hidden => true`) survived | (a) model 2 keeps per-side TRACKING like the tactical engine (kept while an observer has a visible line of sight within sight range or is in contact; lost otherwise; re-acquired only by a new draw; only the first ever detection is a first detection); `Hidden` reads it. (b) G updated before the routs and never reset by them; a routed formation still in flight at a pulse end (flight = the model-2 exit travel time) is progress. (c) `AutoCoefficients.Inactivity` removed; model 2 applies the setup's `TacticalRules.Inactivity`. Cross-resolver tests on the SAME setups: `StandoffAfterADir5RoutEndsAtTheSameInstantInBothResolvers` (9 / 540,000 ms and 11 / 660,000 ms; short-flight neighbours — the routed formation leaves within the next pulse, no progress — 5 / 300,000 ms and 6 / 360,000 ms; identical verdict lists), `ATrackedIdleFormationIsNotHiddenAndNeverRoutsInBothResolvers`, `AFormationTheEnemyLostIsHiddenAgainAndRoutsInBothResolvers` (ridge; N = 3, 4), hold recovery under both rules, `TheLegacyRuleRemainsSelectableInModelTwo` (now via the setup) |
| **P-V5** (major) untoothed properties: P1 reference spread, detection fed Alive vs Start, withdrawal time golden-only | **REPRODUCED** (all three) | mutants V4 and V5 survived 691/0/0; V6 killed only by `ReducedLanePSubsetIsDeterministicAndHasTheExpectedShape` (golden). V5 analysis: on `bd93586` it was an EQUIVALENT mutant — a draw only happens for a formation its enemy does not know, and a formation loses entities only to an enemy that knows it, so Alive = Start at every draw | (1) `RuleP1SamplingBandsUseTheReferenceSpreadOnly` (reference spread 100 → bands 41 ‰ / 41 ‰ / 40,376 ms; the candidate's double spread would give 81 ‰ / 80,752 ms). (2) detection-draw record (`AutoDetectionDraw`, RecordLog only); `EveryDetectionDrawAggregatesOverTheLivingEntities` and, since tracking can now be lost, `ReacquisitionAfterCasualtiesAggregatesOverTheSurvivors` (re-detection of a depleted line: 6–14 draws per seed with 29–31 alive and chance < 1). (3) the withdrawal-time test above |
| **P-V6** (major) effective bands ≈ 2× the presented 10 points; duration floor 19 %; no win band printed; self-check at n_C = 80; no distribution-shape check | **REPRODUCED** | `WinBand` at n = 160 / 160: k = 16 → 123 ‰, 40 → 175, 80 → 202, 120 → 175, 152 → 100; n_C = 80 → 247; equal-infantry duration band 40.3 s on 207.9 s (19.4 %); `Table` printed no win band; `accept`'s tuning self-check used the n = 80 tuning sample; `Check` compares means, the pulse median and class rates only | Rule and constants UNCHANGED (pre-committed). Corrections: dated note under §2, §0, §5, §9 P-P1; new proposal P-P6 (shape check, future rule only). The tables now print the win band (`win A/B/- % Au (band)`). Test `WinBandsAtTheAcceptanceSampleSizesAreTheSamplingFloors` pins the binding bands. **Attainability at the candidate's band sizes MEASURED:** new tactical control sample of the 20 tuning scenarios on control seeds 9001–9160 (`parityp run control-tuning`: 3,200 battles, 130 s wall on 2 threads, digest `62B67D97…0EE52714`) — `parityp selfcheck` acceptance vs control: **20 / 20 PASS** at n = 160 / 160 (held-out: 9 / 9) |
| **P-V7** (minor) held-out hygiene | **REPRODUCED** | `git log -S` → C37 test added in `03c07cc` (01:41:04Z); acceptance CSV written 01:44:35Z; the test runs model 2 on held-out high-ridge seeds 1–20 | source comment corrected (`ParityBatteryP.cs`); disclosed here. The calibration never saw high-ridge (`ParityCalibrationP` refuses non-tuning cases, tested); the C37 test asserts only the direction of losses on seeds 1–20, never acceptance seeds. The held-out battery is consumed anyway (§5.4) |
| **P-V8** (minor) latency pins not turn-exact | **REPRODUCED** | both tests asserted lower bounds only; measured: the forest test's first volley is range-limited (known 58–60, volley 103), so it cannot see the delivery step | `AHaltedFormationAttacksExactlyAtTheFirstPulseStartAfterItLearns`: the enemy's start shifted by whole steps so K lands at 119 / 120 / 121 / 123 → the Attack takes effect at exactly 120 / 150 / 150 / 150, the step before still halted (K = 120: learned in the pulse-start step after that step's decision — the tactical `PulseStart` also decides before the step's `Detect`, code-read; tactical cross-check at K = 118 / 119 / 123: Attack order at pulse 4 / 4 / 5 = the auto's effect step, MEASURED). The forest test is re-labelled a lower-bound property test |
| **P-V9** (nit) | **REPRODUCED** | "~150" vs "~180"; `Cohesion(AF)` unused; interpolated `long` in `ResolverPolicy`; bench CSV unparsed by any test | comment "~180 (29 × 6 = 174)"; dead helper removed; `string.Create(CultureInfo.InvariantCulture, …)` in `ResolverPolicy` and the lane-P refusals of `AutoBattle`; the CSV row / parser / digest moved to `ParityPCsv` (bench uses it; output byte-format unchanged — the reloaded acceptance digest is `12961E15…` as before) with `TacticalOutcomeCsvRoundTripsAPopulatedTableExactly` (every scenario × 2 orientations + every end reason; exact hand row, 38 chars; bit-exact re-serialisation; digest equality) |

### 11.2 Effect on the battery (MEASURED)

None. With every round-1 model change in place, `parityp accept` on the same tactical CSVs gives the **same battery hash
`18222D80D7E3A5E69BE133B68D0E00A79E768694D3667DF3CF2CBF46740CD009`**, the same auto acceptance digest `E6F232E3…4D05EEBC`,
a per-case acceptance CSV identical in all 4,640 rows, the same 16 / 29 (held-out 4 / 9) and attainability 29 / 29; the
tuning evaluation still gives loss 48,769 and 16 / 20. So §5's verdict (NOT ACCEPTED) and every number in §4–§7 stand; the
defects were real but no battery case exercised them (the verifier's point). The lane-P subset goldens did not move. Model-2
cost after the tracking change: 4,640 acceptance battles in 3.43 s single thread (0.74 ms / battle; 3.29 s before — shared
host, not isolated).

### 11.3 Mutants of round 1

Verifier's mutants re-run on `bd93586` (clean 691 / 0 / 0 in 13.4 s): V1–V5 SURVIVED, V6 KILLED by the golden only — as
reported. On the fixed tree (clean suite in the mutant copy 728 / 0 / 0 in 19.0 s, bound 190 s; log `/dev/shm/m7c/P-r1-mutants-verify.log`):

| # | Mutation (fixed tree) | Verdict | Named semantic killer (other failures) |
|---|---|---|---|
| V1 | `Hidden(i) => true` (hidden conjunct dropped) | KILLED | `ATrackedIdleFormationIsNotHiddenAndNeverRoutsInBothResolvers` (+ lose-track test) |
| V1b | `Hidden` = never known (the pre-fix port) | KILLED | `AFormationTheEnemyLostIsHiddenAgainAndRoutsInBothResolvers` |
| V2 | impetus advance forced to 0 | KILLED | `ChargeImpetusEntersTheModelTwoMeleeAttackFactorExactly` |
| V3 | personal concealment kept after a volley | KILLED | `AConcealedShooterIsRevealedExactlyInTheStepAfterItsVolley` |
| V4 | casualty-A floor from the candidate's spread | KILLED | `RuleP1SamplingBandsUseTheReferenceSpreadOnly` |
| V5 | detection over the start strength (`n = g.Start`) | first run SURVIVED (equivalent on every draw then in the suite: no draw met a depleted target); after adding the re-acquisition test: KILLED | `ReacquisitionAfterCasualtiesAggregatesOverTheSurvivors` |
| V6 | withdrawal / flight at walking pace | KILLED | `DoctrineWithdrawalTravelsTheExitFieldAtCatchUpPaceInModelTwo` (+ Standoff pin, + subset golden) |
| V7 | orders only at every second pulse start | KILLED | `AHaltedFormationAttacksExactlyAtTheFirstPulseStartAfterItLearns` (+ 3 others, + golden) |
| V8 | the P-V1 defect restored (no `>= 0` guard) | KILLED | `StationaryTargetInConcealingTerrainIsNeverFoundByModelTwo` |
| V9a | a routed formation's flight is no progress | KILLED | `StandoffAfterADir5RoutEndsAtTheSameInstantInBothResolvers` |
| V9b | the P-V4(b) reset restored (`_fieldIdle = 0` on a rout) | first run SURVIVED (the long-flight cases reset G at the next pulse anyway); after adding the short-flight neighbour cases (Standoff after N + 1 pulses in both resolvers): KILLED | `StandoffAfterADir5RoutEndsAtTheSameInstantInBothResolvers` |
| V10 | model 2 ignores the setup's legacy rule | KILLED | `IdleFormationRecoversHoldMoraleInBothResolversModelTwo` (+ `TheLegacyRuleRemainsSelectableInModelTwo`, legacy stationary cases) |
| V11 | tracking never lost | KILLED | `AFormationTheEnemyLostIsHiddenAgainAndRoutsInBothResolvers` |
| V12 | CSV parser swaps the casualty columns | KILLED | `TacticalOutcomeCsvRoundTripsAPopulatedTableExactly` |
| V13 | orders delivered one step after the pulse start | KILLED | `AHaltedFormationAttacksExactlyAtTheFirstPulseStartAfterItLearns` (+ 3 others, + golden) |

No mutant hung (every run ≤ 25 s against the 190 s bound). Second run (V5, V9b, V13, V12; log `P-r1-mutants-verify2.log`):
its clean run showed **1 failure out of 733 while two other test runs of mine were executing concurrently on the shared host**
(name not captured by the runner); an immediate clean re-run of the same copy gave 733 / 0 / 0 — treated as a transient,
INFERRED to be one of the existing wall-clock harness guards under CPU starvation, not isolated. Every kill above is by a
named deterministic test, never by that guard. Every mutated file was restored and byte-compared.

### 11.4 Tests and validation (MEASURED)

- `dotnet build M7Proto.slnx` (nice 19, `-m:1`): Release and Debug, 0 warnings, 0 errors.
- `dotnet test M7Proto.slnx`: **Release 733 / 0 / 0** (test run 18 s), **Debug 733 / 0 / 0** (45 s). Baseline `bd93586`: 691.
  New: `AutoResolverPRuleTests` 36, `ParityPTests` +2, `AutoResolverPTests` +4 (the turn-exact latency pin has 6 cases, the
  test it replaces had 2). M7Econ.slnx and M7DetMath.slnx do not reference Proto.M7 and were not touched (not re-run).
- Determinism scan `scripts/check-proto-determinism.sh`: CLEAN (73 files).
- Battery repeatability after round 1: battery hash `18222D80…740CD009` in Release and in Debug (Debug auto 4,640 battles in
  11.7 s), the lane-P subset goldens unchanged in both configurations.

### 11.5 Not done in round 1

- Brief item (2)'s battle-map aggregation and a frontage-width mechanism (P-V3): not implemented — a model change after the
  acceptance run needs a new pre-committed held-out battery (§9 P-P4 / P-P5).
- Targeting on current tracking (limitation 12) and the N = 5 ridge divergence (limitation 13): reported, not changed.
- The one remaining legacy-rule difference on model 2 (limitation 14, seed 3): cause not isolated.

---

## 12. Verification round 2 (fix agent, 2026-11-06)

Tree under review: `c31b6a9` (lane/P). Every finding was REPRODUCED against that tree before any change: the verifier's probe
file was copied unchanged (class renamed) into a scratch test file of this clone that was never committed and was removed
afterwards (Release; log `/dev/shm/m7c/P-r2-repro-probes.log`), and the verifier's surviving mutants were re-run in a source
copy of `c31b6a9` made in this round (`/dev/shm/m7c/P-r2-mut`; one full-suite run each, `nice -n 19`, `-m:1`; clean
733 / 0 / 0 in 16.8 s, bound 168 s; log `/dev/shm/m7c/P-r2-mutants-repro.log`). Fixes are minimal; each has a named semantic
test (new file `Proto.M7.Tests/Tactical/AutoResolverPRound2Tests.cs`). Status words as in the header.

### 12.1 Verdicts

| Finding | Verdict | Reproduction evidence (MEASURED on `c31b6a9`) | Fix and evidence (MEASURED after the fix) |
|---|---|---|---|
| **P2-V1** (major) the first-detection progress credit goes to the lowest-id formation | **REPRODUCED** | `FirstDetectionCreditIsolated`, idle 1 / observer 2: tactical {pulse 2, f1, 3, None, routed}, Standoff after 6 pulses; auto: no f1 verdict, SideEliminated side 1 after 10 pulses (550,000 ms), f1 fighting (2 killed). n = 5: tactical {4, f1, 5, None, routed}, Standoff after 10; auto no verdict, SideEliminated after 11 (614,000 ms). Ids swapped: identical verdict lists. N = 1 (enemy seen at once), idle 1: tactical routs f1, Standoff after 2 pulses; auto never routs it, SideEliminated after 10 | Model 2 credits the OBSERVER that made the first detection — the nearest observer in contact, else the first observer of the best clarity (ascending id, strict comparisons: the tactical `Detect`'s `contactObs` / `bestObs`). The credit is a new `ProgressPulse` flag read only by the inactivity rules (DIR5 progress; the legacy rule's activity and its searcher clause), NOT by the Hold morale recovery (the tactical recovery depends on the order and casualties only; before round 2 the credit set `ActivePulse`, which also blocks recovery). Model 1 keeps the Sprint 1 credit bit-exact. Same probes after the fix: all four isolated cases identical in both resolvers including the end instant (6 pulses / 360,000 ms; 10 / 600,000 ms); N = 1 identical under both labellings (Standoff 2 / 120,000 ms). Tests `AFirstDetectionIsTheObserversProgressUnderAnyIdLabelling` (4 cases, both resolvers: reason, pulses, EndMs, verdict lists), `AnImmediateFirstDetectionIsTheObserversProgressInBothResolvers` (2), `AFirstDetectionDoesNotStopTheHoldRecovery` (the Defend observer at morale 500 recovers 20 in every one of the 2N pulses in both resolvers: 620 / 700) |
| **P2-V2** (major) no approach-minimum progress | **REPRODUCED** | probe `ApproachIsTacticalProgress` (n = 3, idle 2 / observer 1, the enemy keeps marching; first known at step 69 = pulse 2): tactical — no verdict at all (its AI: NoContact in pulses 0–2, Attack at 3); auto — {2, f2, 0, 3, None, Routed}. Code: `progress = f.ActivePulse` | `UpdateApproachMinima` (model 2): per fighting formation the running minimum of \|gap\| to the nearest on-field enemy formation its side currently TRACKS, updated after the step's movement from the previous step's tracking (the tactical order: move → `UpdateMinima` → `Detect`); the first measurement only initialises; never reset; a new minimum at least one cell below the last recorded one is progress (`ProgressPulse`), whoever moved. The objective and exit-edge minima need no port (every advance, every flight and every withdrawal in transit already counts). `BestEnemy` enters the model-2 state hash (the tactical hash has it). Same probe: no f2 verdict in the auto. Tests `AnApproachingTrackedEnemyIsProgressInBothResolvers` (3 cases, both resolvers: no verdict for the idle formation) and `AnApproachOfOneCellIsExactlyTheProgressThreshold` (auto, turn-exact with ±1: the enemy's objective is placed so that it stops S − 1, S or S + 1 mm nearer than the first measurement, inside pulse N − 1: S − 1 → rout at the end of pulse N − 1 with exemption None; S and S + 1 → no verdict, idle through pulse N − 1, attacks from pulse N) |
| **P2-V3** (major) the axis clamp halves the depth of rear echelons | **REPRODUCED** | `DepthOfRearFormationsOnTheAxis` (seed 1, step 0): combined-arms (both sides), combined-arms-vs-mass, sniper-screen — true depth 40,000 mm, axis depth 20,000 mm. Flight: relabelled N = 1 probe — tactical Standoff after 2 pulses / 120,000 ms, auto 3 / 180,000; isolated n = 3 (idle 2) 6 vs 7. `AutoAxis.Project` clamped the foot to [0, Length] and `At` clamped s; model 1 (x = dot / len) never clamped | `AutoAxis` is extended beyond both ends along its end segments: `Project` keeps a negative foot on the first segment and a foot past the end of the last one (interior segments still clamp); `At` extrapolates s < 0 and s > Length on those lines with the same sign-symmetric rounding toward zero. Same probe: 40,000 = 40,000 in all three scenarios; the relabelled N = 1 and n = 3 probes end at the same instant in both resolvers (2 / 120,000 ms; 6 / 360,000 ms). MOVED PIN (attributed): `AxisProjectionTieBreaksOnTheLowerSegment` asserted the clamp (0 and 20,000) — now −3,000 and 109,000 with the extrapolated points and the mirror case. Test `RearEchelonsKeepTheirTrueDepthOnTheModelTwoAxis` (5 cases incl. mirrored seeds: every formation on the axis line projects back onto its own anchor, every same-row pair keeps its anchor separation, a 40 m pair exists). Battery effect §12.2 |
| **P2-V4** (minor) exemption (a) ignores `ActiveConcealmentRule` | **REPRODUCED** | clause off, n = 4: tactical SideEliminated side 0 after 4 pulses, f1 {None, routed}; auto Standoff after 4, f1 {ConcealedEnemyInReach, not routed}; n = 5 the same; clause on: identical | `ExemptionsOf` evaluates (a) only when the setup's clause is on (the tactical `ExemptionsNow`). Same probe: all four cases identical. Test `ExemptionAFollowsTheSetupsConcealmentClauseInBothResolvers` (4 cases, both resolvers: reason, pulses, verdict lists) |
| **P2-V5** (minor) convergence-mobility wiring killed by a golden only | **REPRODUCED** | mutant MV6 (climb sign reversed) on `c31b6a9`: KILLED only by `ReducedLanePSubsetIsDeterministicAndHasTheExpectedShape` (golden); `ConvergenceCapFollowsTheTerrainMobility` calls only the static formula | `AutoTerrainTerms` records `PerTargetPermille` (the frontage limit each melee attack was capped by; log only, RecordLog). Test `ConvergenceCapChargesTheClimbTowardTheHolder` (hill-crest seeds 1 / 2, and the same terrace turned round): every attack on the Defend holder carries exactly ConvergenceCapOf(2000, 1000, cost(attacker cell) + ClimbCostPerMm(h(holder) − h(attacker), run)) — 386 climbing the crest, 2,000 descending onto a holder at its foot — and the holder's blows carry 500 |
| **P2-V6** (minor) survivors MV3, MV4, MV11 (+ MV2) | **REPRODUCED** — all four SURVIVED 733 / 0 / 0 on `c31b6a9` (`P-r2-mutants-repro.log`) | — | MV3: `AFlightEndingExactlyOnAPulseEndIsNoProgressThere` (a routed formation at Cell(6, 16), exit field 120,000,000 cost-mm, flight of 29 / 30 / 31 steps by speed 1,400 / 1,340 / 1,300 mm/s: Standoff after N + 1, N + 1, 2N + 1 pulses — it has left the field AT its escape step). MV4: `OwnObjectiveExemptsExactlyWithinOneCell` (objective S − 1, S, S + 1 mm to the flank: exempt, exempt, routed). MV11: `TheRevealWindowIsExactlyTheStepAfterAVolley` (7 start shifts: every draw on the team has chance One iff it volleyed at k − 1, else Zero; in-sight draws two steps after a volley occur and are Zero). MV2 is NOT equivalent: `AContactKeepsTrackingWithoutLineOfSight` (a sight-0 formation idle in contact with a withdrawing sight-0 enemy that does not attack it: tracked by contact only, never hidden, never routed, N = 1 and 2). Verdicts §12.3 |
| **P2-V7** (major) brief item (2): map aggregation and frontage width not implemented | **REPRODUCED** (accurate; disclosed as PARTIAL since round 1) | `MeleeHitV2` / `MissileHitV2` read `Pos(a)` / `Pos(t)` only; no width or frontage term in `AutoResolver.cs`; defile = hold-line-open case by case (round 1: 160 / 160 in both resolvers) | **NOT FIXED in round 2** — reasons and a PROPOSAL in §12.4 |
| **P2-V8** (nit) two final test counts | **REPRODUCED** | §10 said 728; §0 and §11.4 said 733; measured 733 | §10 corrected |

**Residual difference found while reproducing P2-V1 (reported, not changed).** In the verifier's first probe
(`FirstDetectionCreditDoesNotDependOnIdLabels`, n = 3 / 5: the enemy keeps marching into the 2-vs-1 fight) the two resolvers
still end differently after the fixes (tactical: side 1 eliminated after 8 pulses, side 0 unharmed; auto: after 13–15 pulses,
the formerly idle formation morale-routed in combat), and the auto's n = 5 ending depends on the labelling (15 vs 8 pulses).
No inactivity verdict is involved (the auto's verdict lists hold no rout). Cause INFERRED from the code, not isolated: the
auto's combat noise and casualty phase are counter-RNG streams keyed by formation id (`AutoStreams.Noise` / `KillPhase`), so
a relabelling is a different random draw (as the tactical engine's entity-keyed draws are); the 2-vs-1 exchange itself is
the combat-model difference of limitations 2–3.

### 12.2 Effect on the battery (MEASURED)

Same tactical CSVs as §5 and §11 (acceptance `12961E15…`, control, tuning), `parityp accept … new` on the round-2 tree,
Release (log `/dev/shm/m7c/P-r2-accept-release.log`, CSV `P-r2-accept-release.csv`):

| Quantity | Before (`c31b6a9`, = §5) | After round 2 |
|---|---|---|
| BATTERY HASH | `18222D80…740CD009` | **`BEA60C2F89F0D965AC2B7FD3945B5AAFD0FC8C38DB9D80DF53E139025B236442`** |
| Auto (model 2) acceptance digest | `E6F232E3…4D05EEBC` | `4038507198FBDCDC0B5ECBE8C66031104E26A5E0EF9804C4A55F15ADEEB3B65F` |
| Changed per-case rows (of 4,640) | — | **320**: all 160 combined-arms and all 160 combined-arms-vs-mass rows; nothing else |
| Attribution | — | the axis fix ALONE (only `AutoAxis.cs` of this round on `c31b6a9`) gives a byte-identical acceptance CSV and the same hash: **P2-V1, V2, V4 and V5 move no battery case** |
| Scenarios passing (rule P1, unchanged) | 16 / 29 (held-out 4 / 9, tuning-fresh 12 / 20) | **16 / 29 (held-out 4 / 9, tuning-fresh 12 / 20)**; per-metric failures identical (win 5, duration 8, casB 4, casA 1, pulses 1, victor 1) |
| combined-arms (T: win A/B/- 45.0 / 41.2 / 13.7 %, duration 209.1 s) | Au 24.3 / 33.1 / 42.5 %, cas 39.9 / 39.3 %, 159.0 s — FAIL(win, duration) | Au 25.0 / 24.3 / 50.6 %, cas 40.0 / 39.7 %, 171.4 s (band 31.4) — FAIL(win, duration) |
| combined-arms-vs-mass (T: cas A 13.9 %, duration 194.4 s) | Au cas A 13.3 %, cas B 41.0 %, 150.2 s — FAIL(duration) | Au cas A 16.9 % (band 3.0: passes at its edge), cas B 40.0 %, 157.7 s (band 29.2) — FAIL(duration) |
| Auto end reasons (4,640) | SideEliminated 4,423, BothEliminated 217 | SideEliminated 4,410, BothEliminated 230 |
| Tuning set (1,600 cases, frozen coefficients) | loss 48,769, 16 / 20 | loss **52,844**, 16 / 20 (coefficients NOT re-fitted) |
| Lane-P subset goldens (`ParityPTests`) | `F8E2634E…` / `D10B68EB…` | unchanged (MEASURED: the test passes; its sniper-screen seeds 1–4 did not move) |

Reading: the depth clamp was real and explains part of the duration gap of the two composition scenarios (the auto battles
lengthen by 12.4 s and 7.5 s of 50.1 s and 44.2 s gaps) but not most of it, and it does not move them to a pass; in
combined-arms it RAISES the auto's simultaneous-rout draws (42.5 → 50.6 %, limitation 2). The verifier's inference that the
clamp contributes to these failures is now MEASURED in both directions. **This is NOT acceptance evidence**: the held-out
battery was consumed by §5 (§5.4); the §5 verdict (NOT ACCEPTED) stands, and the round-2 model can only be judged on a new
pre-committed held-out battery. Repeatability of the new hash: §12.6.

### 12.3 Mutants of round 2

On the final tree (source copy `/dev/shm/m7c/P-r2-mut` refreshed from it; clean 775 / 0 / 0 in 12.8 s, bound 128 s; log
`/dev/shm/m7c/P-r2-mutants-final.log`). "Named killer" = the semantic test written for the property; the golden is listed
only when it also failed.

| # | Mutation (final tree) | Verdict | Named semantic killer (other failures) |
|---|---|---|---|
| MV2 | tracking kept on line of sight only (contact clause dropped) | KILLED (on `c31b6a9`: SURVIVED) | `AContactKeepsTrackingWithoutLineOfSight` |
| MV3 | a flight ending on a pulse-end step counted as in transit (`<=`) | KILLED (was SURVIVED) | `AFlightEndingExactlyOnAPulseEndIsNoProgressThere` |
| MV4 | objective clause of exemption (b) exclusive (`<`) | KILLED (was SURVIVED) | `OwnObjectiveExemptsExactlyWithinOneCell` |
| MV6 | convergence climb sign reversed | KILLED (was golden-only) | `ConvergenceCapChargesTheClimbTowardTheHolder` (+ subset golden) |
| MV11 | reveal window two steps (`>= k - 2`) | KILLED (was SURVIVED) | `TheRevealWindowIsExactlyTheStepAfterAVolley` |
| R1 | first-detection credit back to the lowest-id formation (the P2-V1 defect) | KILLED | `AFirstDetectionIsTheObserversProgressUnderAnyIdLabelling` (+ `AnImmediateFirstDetection…`, `AFirstDetectionDoesNotStopTheHoldRecovery`, `FirstDetectionIsActivityInBothResolvers`) |
| R2 | approach minima never updated (the P2-V2 defect) | KILLED | `AnApproachingTrackedEnemyIsProgressInBothResolvers` (+ threshold test) |
| R2b | approach threshold exclusive (`d < best - S`) | KILLED | `AnApproachOfOneCellIsExactlyTheProgressThreshold` |
| R2c | approach minima to UNTRACKED enemies too | KILLED | `AFirstDetectionIsTheObserversProgressUnderAnyIdLabelling` (+ threshold, recovery and `AFormationTheEnemyLostIsHiddenAgainAndRoutsInBothResolvers`) |
| R3 | projection clamped to the axis ends again (the P2-V3 defect) | KILLED | `RearEchelonsKeepTheirTrueDepthOnTheModelTwoAxis` (+ the end-instant assertions of the P2-V1 tests, the axis pin) |
| R4 | exemption (a) regardless of the setup's clause (the P2-V4 defect) | KILLED | `ExemptionAFollowsTheSetupsConcealmentClauseInBothResolvers` |
| R5 | inactivity progress also blocks the Hold recovery | KILLED | `AFirstDetectionDoesNotStopTheHoldRecovery` |

No mutant hung (every run 13–17 s against the 128 s / 160 s bounds; R1 first failed to compile — a shadowed local name — and
was re-expressed and re-run with its own clean run, 775 / 0 / 0 in 16.0 s). Every mutated file was restored and
byte-compared with the committed source after each run. Teeth and aim (ADR-015 §7.2): each killer asserts a property of the
TacticalResolver's predicate or of lane TR's coefficients (the observer's Primary, `UpdateMinima`'s one-cell potential, true
deployment depth, the clause gate, the climb toward the holder, `RevealUntilMs = t + StepMs`, tracking while visible OR in
contact, escape at the escape step, `|p − o|² ≤ S²`), most of them on the same setup in both resolvers.

### 12.4 P2-V7 — why item (2)'s aggregation and frontage width are not added in a fix round, and a PROPOSAL

1. **The held-out battery is consumed (§5.4).** A new terrain mechanism is a model change that can only be judged on a NEW,
   pre-committed held-out battery (new scenarios and seeds). Designing the mechanism, its battery and its judgement is a new
   lane iteration, not a minimal fix of a reproduced defect.
2. **Under the current tactical rules a frontage-width term cannot bind a single formation.** A block of 10 columns at
   1,500 mm slot spacing with 300 mm entity radius is (10 − 1) × 1,500 + 2 × 300 = 14,100 mm wide (`TacticalRules`
   FormationColumns 10, SlotSpacingMm 1,500, EntityRadiusMm 300 — code-read), narrower than the narrowest possible gap of one
   20,000 mm cell. A width cap can therefore bind only two or more formations sharing a gap — the geometry the 1-D axis
   already collapses (limitation 6) — and its tactical magnitude has never been measured. Adding a term that is inert in
   every existing case and unmeasured where it would act would be an unvalidated modifier.
3. **Point sampling vs map aggregation is a choice, not a defect.** The brief lists map-aggregated quantities; model 2 evaluates
   the same lane-TR terms at the points where the formations stand and fight, along a route that carries the ford, the gap and
   the street. Which representation the Director wants is reserved to the Director.

**PROPOSAL P-P7 (for the Director; decided by nobody here):** (a) rule whether item (2)'s "aggregate over the battle map" is a
requirement or an illustration; if a requirement, offer an aggregated-share variant (cover / forest / urban share of the
corridor along the route, river-crossing count on the route) as an alternative model, compared with point sampling on a new
battery; (b) before any frontage term, add tactical scenarios where a gap constrains a front (two formations abreast at a
one-cell gap; a block wider than a cell as a labelled rules TEST INPUT) and measure the tactical effect; (c) pre-commit the new
held-out battery (new scenarios and seeds, rule P1 or its successor) before any of these model changes, as §2 did.

### 12.5 Corrections to earlier sections

- §3 "Which inactivity rule each side uses": round 1 called the model-2 port the same rule after its three fixes; round 2
  found three more differences (P2-V1 credit, P2-V2 approach minima, P2-V4 the clause gate), now fixed. The progress
  definition of the port is now: moved, fought, took damage, a first detection THIS formation made, a new approach minimum to
  a tracked enemy (AutoResolver class summary).
- §6 limitation 6 (the depth clamp — round-2 note there), limitation 11 (P2-V7 pointer), §10 (test count, P2-V8), §0 (rows
  2–5, inactivity, tests).

### 12.6 Tests and validation (MEASURED)

- `dotnet build M7Proto.slnx` (`nice -n 19`, `-m:1`): Release and Debug, 0 warnings, 0 errors.
- `dotnet test M7Proto.slnx`: **Release 775 / 0 / 0** (test run 12 s), **Debug 775 / 0 / 0** (37 s). Baseline `c31b6a9`:
  733. New: `AutoResolverPRound2Tests` 42 cases (12 test methods); one moved pin (`AxisProjectionTieBreaksOnTheLowerSegment`,
  P2-V3, attributed above). M7Econ.slnx and M7DetMath.slnx do not reference Proto.M7 and were not touched (not re-run).
- Determinism scan `scripts/check-proto-determinism.sh`: CLEAN (73 files). The new test code uses no hash-set iteration.
- Battery repeatability of the round-2 model: battery hash `BEA60C2F…5B236442` in Release, in a Release repeat and in Debug,
  with byte-identical per-case acceptance CSVs (4,640 rows; auto 3.33 s / 3.47 s Release, 11.2 s Debug, single thread). The
  tactical side of the hash comes from the cached tactical CSVs (digest `12961E15…`, Release and Debug identical in §7); this
  round changed no tactical code (`TacticalBattle*.cs`, `WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs` untouched).
- Lane-P subset goldens (`parityp subset`): tactical `F8E2634E…05837E`, auto `D10B68EB…AD1A6415` in Release and Debug —
  unchanged.
- Files changed in round 2: `Proto.M7/Tactical/AutoResolver.cs` (P2-V1, V2, V4, V5 log field; model 1 untouched),
  `Proto.M7/Tactical/AutoAxis.cs` (P2-V3), `Proto.M7.Tests/Tactical/AutoResolverPTests.cs` (moved pin),
  NEW `Proto.M7.Tests/Tactical/AutoResolverPRound2Tests.cs`, this report. Nothing outside `prototypes/` (D-079 Q5).

### 12.7 Not done in round 2

- P2-V7: battle-map aggregation and a frontage-width mechanism (§12.4, PROPOSAL P-P7).
- No re-fit of the coefficients after the geometry fix (tuning loss 48,769 → 52,844, still 16 / 20): any re-fit makes a new
  candidate that needs a new held-out battery.
- The auto's relabelling sensitivity through id-keyed combat RNG streams (§12.1 residual): not changed, not isolated.
- Limitation 12 (targeting on "ever known") and limitation 13 (N = 5 ridge) unchanged; limitation 13 not re-measured.
