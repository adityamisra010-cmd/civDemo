# Slice E — AutoResolver and parity battery (M7 Sprint 1)

Spec: `m7s1/implementation-spec.md` §5 (with §1, §8). Decision authority: **O-16** (delegated). Repo `/home/user/m5w/dev`,
branch `claude/new-session-yavghm`.

- All code is under `prototypes/m7-sprint1/`. No git state was changed. Nothing outside the folder was written, and
  `Sim.slnx` was never built.
- `sha256sum -c m7s1/baseline/sha256.txt` now reports 12 governance docs as differing from the 08:43 baseline. All 12 have
  mtime 12:37:46, which is before this slice's first write (12:47:54). No file outside `prototypes/` has been modified since
  12:38. They belong to the Director or orchestrator lane (D-076/D-077), not to this slice.
- Results: `reports/parity-report.md` (also copied to `m7s1/reports/parity-report.md`).

## 1. What exists

New files in `Proto.M7/Tactical/`, all integer-only and scan-clean:

| file | contents |
|---|---|
| `FormationCondition.cs` | `ConditionMath`: experience, supply and leadership. These are the shared equations that BOTH resolvers use (stat bonus, supply factor on attack, morale loss per casualty). They are the exact identity at the defaults. |
| `AutoResolver.cs` | `AutoCoefficients` (fitted defaults plus two doctrine assumptions), `AutoStreams` (counter-RNG streams), `AutoResolver : IBattleResolver` (Kind = Auto), and `AutoBattle`, the model with an optional per-step decision log `AutoStepRecord`. |
| `ParityScenarios.cs` | 10 battery scenarios (`ParityScenarioId`), each in as-built and mirrored orientation. |
| `ParityBattery.cs` | `ParityCase`, `ParityOutcome` (integer per-role outcome), `Dist` (nearest-rank quantiles), `ScenarioStats`, `ParityTolerance` (the proposed rule), `ParityCheck`, `ParityBattery` (runs, stats, loss), and `AutoCalibration` (deterministic coordinate descent with `CoefficientAxis` and `FitStep`). |

Bench: `Proto.M7.Bench/ParityBench.cs`, mode `parity [seeds] [threads] [out.md] [out.csv]`. It parallelises tactical cases
by index; Parallel and Stopwatch appear only in the bench, and results are stored by case index.

Changes to existing shared files, all minimal:

- `Tactical/BattleSetup.cs`: `FormationSpec` gains `Experience`, `Leadership` and `Supply` (init properties, defaults
  0 / 0 / Q32.One).
- `Tactical/TacticalBattle.cs`, 5 edits:
  - per-formation `EffMelee`, `EffRanged`, `EffDefence` computed through `ConditionMath` (with validation);
  - the hit chance and damage of melee and ranged fire use them;
  - the morale loss uses `ConditionMath.MoraleLoss`.
- `Proto.M7.Bench/Program.cs`: one dispatch line.
- `README.md`: one line.

**Why the tactical engine was touched.** The spec requires low-supply and leadership scenarios, and an AutoResolver whose
strength depends on supply, leadership and experience. Without the same inputs in the tactical engine, those scenarios would
compare the auto model against a battle that ignores the input. Both resolvers now evaluate one set of equations.

**Golden neutrality, measured.** At the defaults every equation is the exact identity: `MulDivFloor(x, 100, 100) = x`;
supply One skips the factor; `MoraleLoss(k, 2000, S, 0) = floor(2000·k/S)`.

- The test `ConditionMathIsTheExactIdentityAtTheDefaults` proves this, including the Slice D morale rule for k = 0..50.
- The Slice D replay goldens `GoldenTraceHash E53AA685…` and `GoldenStateHash 88F6322C…` are unchanged: all 271 B+C+D tests
  passed right after the change, before any Slice E test existed.

## 2. AutoResolver model (decisions taken under O-16)

The AutoResolver is a separate, formation-level, integer Lanchester-type exchange. One round is one tactical step (StepMs);
there is no step cap. Each formation is a point on the battle axis, which runs from the mean anchor of side 0 to that of side 1.

Per step:

1. At a pulse start, doctrine withdrawal applies below morale 450. The formation escapes after its withdrawal time to the
   exit edge, at catch-up speed and with terrain cost. The time uses the straight-line distance to the edge line at the cost
   of the current cell, not the path length (an approximation; see §9 E-V3). A formation with no passable path to its edge
   (the tactical `EdgeField` is unreachable from its cell) does not withdraw.
2. Movement: speed × StepMs / cost at the formation's current 2-D point, clamped at contact.
   - Attack the nearest known enemy.
   - A ranged formation stops when a known target is within range.
   - An Ambush-posture formation waits until a known enemy is within 3 cells.
   - With no known enemy, a formation moves to its nearest objective (Normal posture only).
3. Detection: one counter-RNG draw per (side, unknown enemy formation, step) within sight. It uses the same formula as the
   tactical engine: p = concealment of the target's cell, × the rules' StationaryConcealment if the target neither moved nor
   fought. Contact reveals.
4. Combat: target = the nearest enemy by composite key (signed gap, formation id).
   - Melee strikers = min(MeleeEngage × alive, MaxAttackersPerTarget × enemy alive). This is the frontage limit.
   - A ranged formation keeps a free share shooting.
   - Expected milli-HP = shots × the tactical hit chance (attack/(attack+defence), clamped to [1/20, 19/20]) × the tactical
     damage max(1, attack/5) × (1000 + fortune(side) + noise(formation, step))/1000. As in the tactical engine this holds
     at effective attack 0 too (HitMin, 1 HP), so every attack deals positive damage (§9 E-V1/E-V4).
5. Casualties = floor((damage × KillEff + phase) / (10⁶ × MaxHp)). The phase is a per-formation counter-RNG phase that
   de-synchronises symmetric routs. Then morale loss through ConditionMath, and rout below 300 (strict, as in the tactical
   engine). A fitted share of a routed formation is recorded as Surrendered; a routed formation with no passable path to its
   exit edge surrenders entirely (spec §4 surrender rule, added in §9 E-V3).
6. Termination follows the tactical rules at formation level (it is not an entity-exact copy: see §9 for what differs):
   - elimination: a side with no Active entity left. Withdrawing formations stay Active until they escape.
   - at a pulse end, the five-pulse inactivity rule with the active-concealment clause, using formation-level activity:
     movement, damage, or a first detection.

The output has the same `BattleResult` shape: exact conservation, and `End()` throws on any violation. It carries
`Resolver = Auto`, the duration in pulses, `EndMs`, a trace hash over per-step state, and a state hash.

Further decisions:

- **Domain:** Land only. Naval and air throw `NotSupportedException`. O-1: auto is optional, never the default, and is never
  chosen implicitly (`BattleResolution` is unchanged).
- **Orientation invariance:** by construction (the projected axis), and tested on all 10 scenarios.
- **Terrain-defence modifier and special abilities:** not added. The tactical engine has neither (parity-report §6.6).

## 3. Placeholders and citations

| item | value | source |
|---|---|---|
| Experience mapping | stat × (100+E)/100, E ∈ [0,100] | PLACEHOLDER. The campaign field `MilitaryUnitRow.Experience` is floating point (recon-military); its mapping is OPEN. |
| Supply mapping | attack × (½ + ½·supply) | PLACEHOLDER (floor ½ invented). The supply source is the Slice F logistics lane (OPEN). |
| Leadership mapping | morale loss × 100/(100+L), L ∈ [0,100] | PLACEHOLDER |
| Auto coefficients | Melee 500, MaxAttackersPerTarget 500, Ranged 300, KillEff 600, Fortune 200, Noise 0, Ambush 0, Surrender 50 | FITTED on battery seeds 1–20 against the PLACEHOLDER Slice D rules (parity-report §3) |
| Doctrine | retreat morale 450, ambush trigger 3 cells | `KnowledgeAi` defaults (PLACEHOLDER there) |
| Scenario inputs | supply 1/4, morale 550, leadership 50, experience 50, the forest and jungle blocks | TEST INPUTs |
| Formation sizes | HI 50, archers 60 | PROPOSED caps, docs/m7-spec.md:395-396 (cited by Slice D) |
| Every tactical rule and stat | | Slice D §3 |

## 4. Tests and results

| check | result |
|---|---|
| build: `dotnet build prototypes/m7-sprint1/M7Proto.slnx -c Release -nologo --disable-build-servers` | **0 warnings, 0 errors** |
| tests: `dotnet test … --no-build` | **Passed 318, Failed 0** (B+C+D 271 + Slice E 47: `AutoResolverTests` 32, `ParityBatteryTests` 15). After the verification round (§9): **Passed 335, Failed 0** (+17 in `AutoResolverVerificationFixTests`). |
| `scripts/check-proto-determinism.sh` | **CLEAN** (41 files, comments included). It flagged two comments, which were reworded: "Random phase" and the floating-point type name. |

| spec / CLAUDE.md item | test(s) |
|---|---|
| Shared equations, identity at the defaults, hand values, validation in both resolvers | `ConditionMathIsTheExactIdentityAtTheDefaults` ×6, `ConditionMathHandComputedValues`, `BothResolversRefuseOutOfRangeConditions` |
| Tactical engine consumes the conditions | `TacticalLowSupplyFormationDealsLessDamagePerAttack`, `TacticalLeadershipReducesMoraleLossPerCasualty`, `TacticalExperienceEdgeWinsAndEqualFormationsDoNot` |
| Exact conservation, auto | `AutoConservesExactlyAcrossTheBattery`: 10 scenarios × 4 seeds × 2 orientations |
| Determinism, seed sensitivity, input-order metamorphic, orientation invariance | `AutoIsDeterministicAndSeedSensitive`, `AutoIgnoresFormationInputOrder` ×3, `AutoGivesTheSameOutcomeInBothOrientations` (30 pairs) |
| Tie-dense composite key (score, id) | `AutoTargetTieBreaksOnTheLowerFormationId` ×4: equal gaps, ids swapped between rows, input reversed. Also `QuantilesOfATieDenseSampleIgnoreInputOrder` ×3 and `ModalWinnerTieBreaksAThenBThenDraw`. |
| Resolver choice explicit, never implicit; naval refused | `AutoIsSelectedOnlyExplicitlyAndRefusesNaval` |
| Factor mechanics | `FrontageLimitCapsTheEngagedShare` (exact milli-HP for 12 vs 25 strikers), `AmbushSurpriseWindowSilencesTheVictimForExactlyTheFittedSteps`, `DoctrineWithdrawalBelowTheRetreatMoraleEscapesWithoutLosses`, `AutoRoutThresholdIsStrict` ×2, `RoutedLoserIsRecordedAsRoutedEscapeAndSurrenderFraction`, `KillPhaseDesynchronisesOtherwiseSymmetricRouts`, `AutoLowSupplyAndExperienceMoveTheOutcomeTheRightWay`, `CoefficientValidationKeepsEveryContactStepDamaging` |
| Termination without a cap | Every auto run in the tests is under a 30 s harness guard; exceeding it is a FAILURE, not a rule. `MutualStandoffDisengagesAfterExactlyFivePulsesInBothResolvers`: both resolvers end BothDisengaged at pulse 5 with EndMs 300,000, everyone a disengaged survivor. |
| Statistics, tolerance, check | `NearestRankQuantilesHandComputed`, `OutcomeCasualtyFractionsArePersonnelLossPermilleByRole`, `ToleranceDerivationHandComputed` (474 permille; floors 8 and 177), `ToleranceWidensWithFewerSeedsAndModalRuleBindsClearFavourites`, `ParityCheckFlagsEachCriterionSeparately` (boundaries), `LossIsZeroForIdenticalStatsAndCountsEveryComponent` |
| Calibration: deterministic, monotone, fit-set isolation, tie rule | `CalibrationIsDeterministicNeverWorsensTheLossAndUsesOnlyTheFitCases` (corrupting the holdout outcomes leaves the fit unchanged), `CalibrationKeepsTheCurrentValueOnTies` |
| Reduced deterministic battery subset | `ReducedBatterySubsetIsDeterministicAndHasTheExpectedShape`: 3 scenarios × seeds 1–3 × 2 orientations, both resolvers, run twice. Semantic assertions: archers win without losses and HI loses ≥ 30 %; low supply loses with more casualties. Pinned digests (Linux-measured; cross-platform UNTESTED): tactical `11482777…7B5A40E7`, auto `87BB6323…7574DD77`. |
| Battery shape | `BatteryCasesCoverBothOrientationsOfEverySeed`: 800 cases, 10 scenarios. `MirroredSetupIsTheExactMirrorImage`: anchors, exits, every terrain cell. |

## 5. Mutation testing (spec §8)

Private copy: `m7s1/work/E-mut/tree/` (a tar copy of the prototype without bin/obj).

- Runner: `m7s1/work/E-mut/tools/mutants.py`. Log: `m7s1/work/E-mut/logs/mutants.txt`.
- Clean build + test in the copy: 14 s + 20 s = 34 s. Each mutant was bounded at **170 s (5×)**. No mutant hung.

| mutant | change | result | killing tests (semantic) |
|---|---|---|---|
| E1 | auto target tie picks the higher id | killed | `AutoTargetTieBreaksOnTheLowerFormationId` ×4 |
| E2 | leadership ignored in the morale loss | killed | `ConditionMathHandComputedValues`, `TacticalLeadershipReducesMoraleLossPerCasualty` |
| E3 | tactical ignores supply | killed | `TacticalLowSupplyFormationDealsLessDamagePerAttack` (+ subset digest) |
| E4 | auto frontage cap removed | killed | `FrontageLimitCapsTheEngagedShare` (+ digest) |
| E5 | ambush window one step longer | killed | `AmbushSurpriseWindowSilencesTheVictimForExactlyTheFittedSteps` |
| E6 | doctrine withdrawal disabled | killed | `DoctrineWithdrawalBelowTheRetreatMoraleEscapesWithoutLosses` |
| E7 | kill-phase staircase removed | killed | `KillPhaseDesynchronisesOtherwiseSymmetricRouts` (+ digest) |
| E8 | quantile rank floor instead of ceil | killed | `NearestRankQuantilesHandComputed` |
| E9 | tolerance drops the median sampling floor | killed | `ToleranceDerivationHandComputed` |
| E10 | calibration accepts equal-loss moves | killed | `CalibrationKeepsTheCurrentValueOnTies` |
| E11 | parity check casualty bound strict | killed | `ParityCheckFlagsEachCriterionSeparately` |
| E12 | auto rout check `<=` | **survived the first run** → killed after adding `AutoRoutThresholdIsStrict` | `AutoRoutThresholdIsStrict(0, false)` |

All 12 are killed by at least one semantic test; none relies on the digest alone. On aim: each killing test asserts a stated
rule (composite-key tie-break, the shared equations, frontage, the surprise window, doctrine, the strict rout threshold,
nearest-rank, the sampling floor, strict-improvement fitting, closed tolerance bounds), not an incidental number.

## 6. Performance (Release, Linux x64, 4 CPUs shared; Stopwatch only in the bench)

| item | measurement |
|---|---|
| full battery `parity 40 4` | **109.7 s wall** |
| tactical part | 800 battles, 32.2 s wall on 4 threads; 122.3 s summed per-battle time; mean 152.8 ms, max 1,204 ms (ambush); median ms per scenario from 32 (numbers-2to1) to 549 (combined arms) |
| calibration | 261 loss evaluations × 400 auto battles = 76.6 s, the dominant cost after the tactical runs |
| auto | 734 µs per battle (single thread); about 200× cheaper than a tactical battle |
| test suite | about 10–20 s for 318 tests (Slice E adds about 10 s, mostly the reduced subset run twice and the 40-seed kill-phase test) |

Timings on this shared host vary by 1.5–2× between runs (run40a: total 93 s; final: 110 s). The per-case outcomes were
byte-identical in all three runs.

## 7. Findings

1. **The tolerance must count independent seeds, not cases.** The two orientations of a seed are highly correlated:
   - equal-infantry pairs had the same winner in 34 of 40 seeds;
   - the archer scenarios gave identical outcomes in 40 of 40 pairs.

   A first tolerance (v1) that counted cases and used 2 σ rejected the tactical resolver against itself in 2 of 10
   scenarios. The proposed v2 (n_eff = seeds, 3 σ, a median sampling floor) accepts it in 10 of 10 (parity-report §5.3).
2. **The AutoResolver meets the proposed tolerance in 8 of 10 holdout scenarios.** The two misses are casualty medians:
   - equal-infantry: auto overstates the winner's losses and has too many simultaneous routs (15 % vs 0–10 %);
   - combined arms: auto is about 5 points low.

   Both are model-structure limits (parity-report §6), not fitting noise.
3. **No side-index bias.** Relabelling formation ids flips the equal-infantry counts exactly (44 : 32 ↔ 32 : 44). The
   higher-id lean is 1.4 σ, which is not significant.
4. **Ambush gives no first-strike edge under the placeholders.** The fitted surprise window is 0.
5. **Archers dominate heavy infantry 80 of 80 under the placeholders,** including against a defender in forest. This is
   inherited from Slice D (D §8), and both resolvers agree on it.

## 8. Dependencies and blockers for integration (not decided here)

- **O-16 follow-through:** the Director's adoption or amendment of the proposed tolerance, and whether 8/10 suffices for an
  optional auto-resolve.
- **O-14:** where both resolvers live. Namespace `Proto.M7.Tactical`.
- **Campaign inputs:**
  - experience: the mapping of the floating-point `MilitaryUnitRow.Experience` to the integer input;
  - supply: the fraction from the logistics lane (Slice F);
  - leadership: no campaign source exists yet.

  All three mappings are PLACEHOLDER.
- **RNG:** the battle seed must derive from a `RngRegistry` stream with its state in `WorldState`. This needs kernel sign-off,
  as for B and D.
- **Doctrine:** the auto model assumes the `KnowledgeAi` doctrine. Parity with player control or other AIs is unmeasured.
- **Re-fit discipline:** every change to a tactical PLACEHOLDER moves the fitted coefficients and the subset digests. Re-run
  `parity 40 4` and re-pin with attribution.
- **Not built:** naval and air auto models, a terrain defence modifier, and special abilities (none exist in the data).
- **O-17:** cross-platform equality of the digests and of the battery outcomes is UNTESTED.

## 9. Verification and fixes (verify-E round, 2026-11-04)

Every finding was first reproduced by this fixer on `prototypes/m7-sprint1` itself (ADR-015 §6), not taken from the
verifier's copy. Work dir: `m7s1/work/fixE/` (`tree/` = private copy for mutants and digests, `tools/mut.py`, `logs/`).

**Battery neutrality, measured.** The code fixes touch only situations that do not occur in the battery (effective attack 0,
impassable terrain). SHA-256 over the `ResultHash` of all 800 auto battles (10 scenarios × seeds 1–40 × 2 orientations),
per scenario and overall, is identical before and after (`logs/digest-before.txt` = `logs/digest-after.txt`, overall
`DADD8AFC…EB090281`). The tactical engine was not touched. So §1–§7, the fitted coefficients, the subset digests and
`parity-report.md` §0–§5.2 are unchanged.

| id | verdict | evidence | change |
|---|---|---|---|
| E-V1 (blocking) | **CONFIRMED → FIXED** | Before: `AutoTerminatesWhenEffectiveRangedAttackFloorsToZero` seeds 1 and 2 both fail with "HARNESS GUARD: AutoResolver did not terminate within 10 s (NON-TERMINATION)" (`logs/before-ev1.txt`). After: both pass in milliseconds, both sides deal damage, and so does the tactical engine. | `ExpectedMilli` now follows the tactical formula at attack 0 (HitMin, damage max(1, 0) = 1) instead of returning 0. Every attack therefore deals > 0 milli-HP. This is guarded twice: the `AutoBattle` constructor refuses rules whose HitMin is too small for a minimal attack to deal ≥ 1 milli-HP, and `ExpectedMilli` throws if the invariant is ever broken. The verifier's suggested `FoughtStep = dr > 0` would only have hidden the symptom. By code reading (not measured), it also misses the contact-branch twin (`mode |= 2` at dr = 0), and it leaves E-V4 open. |
| E-V2 (major) | **CONFIRMED → FIXED (tests)** | V1 and V2 survived all 318 tests (verifier log). Reproduced the non-equivalence in both resolvers with near-perfect stationary concealment (`TT.HideRules`). | New semantic tests (below). Both mutants are now killed. |
| E-V3 (major) | **CONFIRMED → FIXED** | Before: `TrappedRoutSurrendersEntirelyInBothResolvers`: tactical surrenders 20, auto 0 (escaped 20). `TrappedFormationCannotWithdrawThroughImpassableTerrain`: auto withdrew 20 out of a Water ring (`logs/before-nonhang.txt`). | New `Trapped(f)` = the tactical `EdgeField` distance at the formation's cell is `Unreachable`. A trapped routed formation surrenders entirely, as in the tactical Resolve. A trapped formation does not start a doctrine withdrawal: it stays on the field, as the tactical retreater does. Remaining, now disclosed (§2 step 1): the withdrawal time uses the straight-line edge distance at the current cell's cost, not the path length. A tactical retreater on a trapped cell also does not strike back, while the auto formation keeps fighting. Neither occurs in the battery. |
| E-V4 (minor) | **CONFIRMED → FIXED** (same root as E-V1) | Before: `ZeroEffectiveMeleeStrikesAtTheTacticalFloorInBothResolvers` failed: "Auto: zero-attack melee must still wound". | The `EffMelee > 0` guard was removed and the tactical floor added (above). The test pins the exact first melee step: 10 strikers × 1 HP × 1000 × HitMin = 499 milli-HP, with fortune and noise off. |
| E-V5 (minor) | **CONFIRMED → FIXED (tests)** | V3 and V4 survived 318/318. V6 also survived, and the verifier made no claim about it. This round found a setup where V6 changes behaviour, so V6 is NOT equivalent: two Ambush-posture formations 8 cells apart in the open. | New tests kill V3, V4 and V6 (below). |
| E-V6 (minor) | **CONFIRMED → FIXED** | The old assertion compared the outputs of two `Fit` calls whose inputs were identical (the corruption was filtered out by the same index), so it could not fail. | New `ParityBattery.FitHalf(cases, outcomes, lastFitSeed)`, used by `ParityBench` in place of its inline LINQ split. The test now applies `FitHalf` to the FULL arrays with corrupted holdout entries and asserts that the inputs are equal to the oracle split, that the fit is equal, and that the loss is equal. Teeth: corrupting ONE fit entry changes the `FitHalf` output and the loss. Mutant F6 (`FitHalf` takes one more seed) is killed by this test. |
| E-V7 (minor) | **CONFIRMED; remedied by a pre-committed confirmation run** | `holdout:` line in run40a.log: self 8/10, rule v1. run40b.log and final.log: self 10/10, rule v2. The per-case outcomes of the three runs are identical (SHA-256 over all CSV columns except `tac_ms`: `3db5cf84176b3b6f` for all three), so only the rule changed. | The v2 rule could not be un-fitted to seeds 1–40. Instead, v2 and the shipped coefficients were frozen and written down (`logs/confirm-precommit.txt`, with file hashes) BEFORE a run on seeds 41–80, which had never been run. The new bench mode is `parity 40 4 md csv 41 frozen`: no calibration, the same half split. Result: **Auto 9/10** (fails combined-arms cas A and cas B, the known structural gap of parity-report §6.2; equal-infantry passes this time). **Tactical-vs-itself 10/10.** Log: `logs/confirm41-80.{log,md,csv}`. Wall time 32.7 s. This is the first evaluation of v2 that was not fitted to its own data. |
| E-V8 (nit) | **CONFIRMED, not changed** | Neither resolver has a terrain-defence modifier (grep). This is disclosed in parity-report §6.6. | Awaits Director direction. Adding the modifier to only one resolver would create a parity gap by construction. Nothing was invented. |

New tests (`Proto.M7.Tests/Tactical/AutoResolverVerificationFixTests.cs`, 17 cases). Every one states a rule. Wherever
the rule is shared, it is checked in BOTH resolvers:

| test | rule |
|---|---|
| `AutoTerminatesWhenEffectiveRangedAttackFloorsToZero` ×2 | O-4 termination at effective attack 0; zero-attack shots land at the tactical floor (both resolvers deal damage) |
| `ZeroEffectiveMeleeStrikesAtTheTacticalFloorInBothResolvers` | the tactical hit/damage floor at attack 0, with the exact first-step milli-HP |
| `TrappedRoutSurrendersEntirelyInBothResolvers` | spec §4 surrender rule, both resolvers. Controls: the rout rule alone with doctrine off (trapped → 20; open ground → fitted share 1, routed escape 19) |
| `TrappedFormationCannotWithdrawThroughImpassableTerrain` | no withdrawal escape without a path (both resolvers); control: open ground withdraws 20 |
| `StationaryTargetInConcealingTerrainGetsTheStationaryFactor` ×3 | detection p × StationaryConcealment for a stationary target. Near-perfect factor: no shot (tactical as well, seed 1). Factor 1: the archers find the target and fire |
| `HiddenAmbusherFacingAnAdvancingEnemyStaysAndSprings` ×4 | spec §4 inactivity test (i), the active-concealment clause. On: the ambush is sprung and fought (tactical as well, seed 1). Off: SideDisengaged side 1, no kills (both resolvers) |
| `IdleFormationRecoversHoldMoraleEachPulseInBothResolvers` | Hold recovery: 800 + 5 × 20 = 900, and the 1000 cap, exact in both resolvers |
| `DoctrineWithdrawalTakesTheTravelTimeToTheExitEdge` ×2 | escape after ceil(distance / (speed × 3/2 × 2 s / cost)) steps. Hand values: 3900 mm per step; 75 steps from column 14 and 100 from column 19. EndMs = (steps − 1) × StepMs |
| `FirstDetectionIsActivityInBothResolvers` ×2 | spec §4: a first detection is primary activity. At 8 cells (in sight) both resolvers end after 6 pulses; the control at 13 cells (out of sight) ends after 5 |

Mutation round (private copy `m7s1/work/fixE/tree`, runner `tools/mut.py`, log `logs/mutants.txt`):

- Clean build + test in the copy: 16 s + 21 s. Each mutant's test run was bounded at 150 s, which is ≤ 5× the clean
  build + test.
- No mutant hung. Under F1, the harness guards report non-termination as two test failures in 33 s.
- Every kill below is by a semantic test (no golden/digest kills counted).

| mutant | change | killed | killing tests |
|---|---|---|---|
| V1 (verifier) | detection ignores StationaryConcealment | yes | `StationaryTargetInConcealingTerrainGetsTheStationaryFactor` ×3, `HiddenAmbusher…` ×4 |
| V2 (verifier) | `Concealed()` returns false | yes | `HiddenAmbusherFacingAnAdvancingEnemyStaysAndSprings` ×4 |
| V3 (verifier) | pulse-end Hold recovery removed | yes | `IdleFormationRecoversHoldMoraleEachPulseInBothResolvers` |
| V4 (verifier) | withdrawal escapes the next step | yes | `DoctrineWithdrawalTakesTheTravelTimeToTheExitEdge` ×2 |
| V6 (verifier) | first detection no longer counted as activity | yes (NOT equivalent) | `FirstDetectionIsActivityInBothResolvers(8, 6)` |
| F1 | the E-V1 fix reverted (attack 0 deals 0) | yes | `AutoTerminates…` ×2 (guard: non-termination), `ZeroEffectiveMelee…` |
| F2 | melee skipped at EffMelee 0 | yes | `ZeroEffectiveMeleeStrikesAtTheTacticalFloorInBothResolvers` |
| F3 | `Trapped()` always false | yes | `TrappedRout…`, `TrappedFormationCannotWithdraw…` |
| F4 | trapped rout gets the fitted share | yes | `TrappedRoutSurrendersEntirelyInBothResolvers` |
| F5 | trapped formation still withdraws | yes | `TrappedRout…`, `TrappedFormationCannotWithdraw…` |
| F6 | `FitHalf` takes one more seed | yes | `CalibrationIsDeterministicNeverWorsensTheLossAndUsesOnlyTheFitCases` |

On aim (ADR-015 §7.2): each test asserts a spec §4/§5 rule, or a parity property the tactical engine demonstrably has in
the same setup. None asserts an incidental number. The fixed-value pins (499 milli-HP, 75 and 100 steps, 900 morale) are
hand-derived from the stated formulas.

Final state:

| check | result |
|---|---|
| `dotnet build M7Proto.slnx -c Release -nologo --disable-build-servers` | 0 warnings, 0 errors |
| `dotnet test M7Proto.slnx -c Release --no-build -nologo` | **Passed 335, Failed 0** |
| `scripts/check-proto-determinism.sh` | **CLEAN** (41 files) |
| confirmation battery `parity 40 4 … 41 frozen` | 32.7 s wall: tactical 31.3 s (mean 152.1 ms/battle); auto 587.5 µs/battle |

Files changed in this round:

- `Proto.M7/Tactical/AutoResolver.cs`: the attack-0 floor, the termination guards, `Trapped`, the doc comment;
- `Proto.M7/Tactical/ParityBattery.cs`: `FitHalf`;
- `Proto.M7.Bench/ParityBench.cs`: uses `FitHalf`; optional `firstSeed` and `fit|frozen` arguments;
- `Proto.M7.Tests/Tactical/ParityBatteryTests.cs`: the isolation assertion;
- new `Proto.M7.Tests/Tactical/AutoResolverVerificationFixTests.cs`;
- this report, and `parity-report.md` (§5.3 and §6.8 notes).

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S485** (5 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`E-autoresolve.md:91`) — "**Terrain-defence modifier and special abilities:** not added. The tactical engine has neither (parity-report §6.6)." — in part: DIRECTOR DECISION - terrain must influence AutoResolver outcomes (DIR5 §3.1) and terrain, concealment, forest cover, elevation and LOS must materially influence combat (§2.7); terrain influence in both resolvers is Phase C item 6 (§8); special abilities must be considered in the parity model (§3.1). Retained: add it to BOTH resolvers together (one-sided addition creates a parity gap by construction); the mechanism is DELEGATED TECHNICAL. **Replaced by** DIR5 §3.1, §2.7, §8 Phase C item 6 (D-079 DR21, DR34, DR51). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`E-autoresolve.md:103`) — "| Formation sizes | HI 50, archers 60 | PROPOSED caps, docs/m7-spec.md:395-396 (cited by Slice D) |" — in part: DIRECTOR DECISION (DIR5 §2.10) - the values are Director-preserved caps ('Recover and preserve the existing numerical caps'), not 'proposed' limits; values unchanged. DELEGATED TECHNICAL: the mortar counted object and the resolution of CAP-1..CAP-5 without new Director decisions; hard caps protect performance, ordinary scale is economy-governed. **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`E-autoresolve.md:189`) — "the Director's adoption or amendment of the proposed tolerance, and whether 8/10 suffices for an" — in part: 'whether 8/10 suffices' is closed - DIR5 holds even 9/10 insufficient; improve the AutoResolver before adoption (DIRECTOR DECISION). Retained and OPEN: the tolerance (acceptance criteria) returns as a Phase D recommendation for Director approval, built on a repeatable battery covering the §3.1 factors. **Replaced by** DIR5 §3.1, §10, §8 Phase D (D-079 DR34, DR51, DR53). [DIRECTOR DECISION; OPEN residue]
- (`E-autoresolve.md:191`) — "**O-14:** where both resolvers live. Namespace `Proto.M7.Tactical`." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`E-autoresolve.md:226`) — "Awaits Director direction. Adding the modifier to only one resolver would create a parity gap by construction." — in full as to 'Awaits Director direction': direction given - terrain must influence AutoResolver outcomes, in both resolvers (DIRECTOR DECISION). Retained: the both-resolvers caution; the mechanism is DELEGATED TECHNICAL. **Replaced by** DIR5 §3.1, §2.7, §8 Phase C item 6 (D-079 DR21, DR34, DR51). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
