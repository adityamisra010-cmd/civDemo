# Parity report — TacticalResolver vs AutoResolver (M7 Sprint 1, Slice E)

Spec: `m7s1/implementation-spec.md` §5 (with §1, §8). Decision authority: **O-16** (delegated: "design, implement and test a
quantitative parity model"). Repo `/home/user/m5w/dev`, branch `claude/new-session-yavghm`; all code under
`prototypes/m7-sprint1/`. Measured 2026-11-04 on Linux x64, .NET 10.0.12, 4 CPUs (shared host). Nothing here is a balance
claim: both resolvers run on the PLACEHOLDER tactical rules and unit stats of Slice D (`reports/D-tactical.md` §3).
Implementation details: `reports/E-autoresolve.md`.

## 0. Headline

| quantity | value |
|---|---|
| battery | 10 scenarios × 40 seeds × 2 orientations = **800 tactical + 800 auto battles** |
| calibration | coordinate descent on the **FIT half (seeds 1–20)** only; everything below marked "holdout" is **seeds 21–40**, never seen by the fit |
| per-case victor agreement (same seed, same orientation) | 659 / 800 = 82.4 % overall; **331 / 400 = 82.8 % on the holdout** |
| proposed tolerance (§5) applied to the holdout | AutoResolver passes **8 / 10** scenarios (fails: equal-infantry cas A, combined-arms cas B); the tactical resolver against itself (fit half vs holdout half) passes **10 / 10** |
| full-battery runtime (Release, `parity 40 4`) | **109.7 s wall** in total: tactical 800 battles 32.2 s wall (122.3 s summed per-battle time, mean 152.8 ms, max 1,204 ms, 4 threads); calibration 261 loss evaluations × 400 auto battles = 76.6 s; auto 800 battles 0.59 s (734 µs/battle, 1 thread) |
| reproducibility | three full runs (run40a, run40b, final) gave byte-identical per-case outcome tables (SHA-256 of the CSV without the timing column `eb5262de…16830ec`), under different parallel schedules |

## 1. The battery

Builder: `Proto.M7/Tactical/ParityScenarios.cs`; runner and statistics: `ParityBattery.cs`; bench mode:
`dotnet Proto.M7.Bench.dll parity [seeds=40] [threads=4] [out.md] [out.csv]` (`Proto.M7.Bench/ParityBench.cs`).

Field: the Slice D 48 × 32 open field, 20,000 mm cells (PLACEHOLDER). Both sides are driven by the Slice D knowledge-only
`KnowledgeAi`. Role A = side 0, role B = side 1. Every numeric scenario input is a TEST INPUT that exercises one factor.

| # | scenario | role A | role B | factor exercised |
|---|---|---|---|---|
| 1 | equal-infantry | HI 50 | HI 50 | baseline symmetry |
| 2 | numbers-2to1 | HI 50 | HI 25 | numbers / frontage |
| 3 | archers-vs-infantry-open | archers 60 | HI 50 | composition (ranged vs melee) |
| 4 | archers-vs-infantry-forest | archers 60 (attacker) | HI 50 holding in a forest block, no objective | terrain: concealment and movement cost |
| 5 | low-supply | HI 50, supply 1/4 | HI 50 | supply (attack × 5/8) |
| 6 | low-morale | HI 50, morale 550 | HI 50 | morale |
| 7 | leadership-edge | HI 50, leadership 50 | HI 50 | leadership (morale loss × 100/150) |
| 8 | ambush | HI 50 advancing through a full-height jungle belt | HI 50, Ambush posture, in the jungle | ambush / concealment |
| 9 | experience-edge | HI 50, experience 50 | HI 50 | experience (stats × 1.5) |
| 10 | combined-arms | HI 50 + archers 60 | HI 50 + archers 60 | composition, multi-formation sides (mirror-exact anchors; Slice D's `CombinedArms` asymmetry, D §11.4, is not used) |

Spec scenario 4 says "defender in forest" without naming the defender. The interpretation used here is that the infantry
defends in the forest and the archers attack (labelled).

**Orientation.** Slice D §9 / §11.3 records a residual East/West effect whose cause was not isolated. Every seed is therefore
run in both orientations: as built, and mirrored (`x → FieldW-1-x` for anchors, objectives and terrain, with the exits
swapped). Statistics are reported **per role, never per compass side**. Measured consequence: the two orientations of one seed
are **strongly correlated** in the tactical engine. Equal-infantry pairs had the same winner in 34 of 40 seeds, and the archer
pairs gave identical outcomes in 40 of 40. The effective sample size n_eff is therefore the number of seeds, not the number of
cases (§5).

## 2. Tactical distributions (all 80 cases per scenario)

Casualty fraction = (Killed + Surrendered) / Start of the role, in %. "Win" = the side holding the field
(`BattleResult.HoldsField`). Quantiles are nearest-rank (integer, `Dist`).

| scenario | A wins % | B wins % | draw % | cas A p10/p50/p90 % | cas B p10/p50/p90 % | pulses p10/p50/p90 | mean ms/battle |
|---|---:|---:|---:|---|---|---|---:|
| equal-infantry | 40.0 | 55.0 | 5.0 | 26.0/34.0/38.0 | 26.0/34.0/38.0 | 3/3/9 | 92.0 |
| numbers-2to1 | 100.0 | 0.0 | 0.0 | 10.0/18.0/24.0 | 36.0/36.0/44.0 | 3/3/3 | 34.7 |
| archers-vs-infantry-open | 100.0 | 0.0 | 0.0 | 0.0/0.0/0.0 | 36.0/40.0/40.0 | 3/3/3 | 80.9 |
| archers-vs-infantry-forest | 100.0 | 0.0 | 0.0 | 0.0/0.0/0.0 | 36.0/40.0/40.0 | 5/5/5 | 146.8 |
| low-supply | 0.0 | 100.0 | 0.0 | 36.0/36.0/42.0 | 6.0/12.0/18.0 | 3/3/4 | 73.6 |
| low-morale | 0.0 | 100.0 | 0.0 | 14.0/14.0/20.0 | 8.0/14.0/20.0 | 3/3/3 | 83.4 |
| leadership-edge | 85.0 | 15.0 | 0.0 | 26.0/34.0/46.0 | 32.0/36.0/42.0 | 3/3/8 | 80.4 |
| ambush | 33.7 | 57.5 | 8.7 | 30.0/36.0/40.0 | 28.0/32.0/38.0 | 5/5/6 | 332.8 |
| experience-edge | 100.0 | 0.0 | 0.0 | 12.0/20.0/28.0 | 36.0/38.0/42.0 | 3/3/3 | 62.4 |
| combined-arms | 45.0 | 40.0 | 15.0 | 36.3/41.8/46.3 | 38.1/42.7/45.4 | 4/4/4 | 541.2 |

End reasons, tactical / auto (SideEliminated / BothEliminated / SideDisengaged / BothDisengaged):

| scenario | tactical | auto |
|---|---|---|
| equal-infantry | 76 / 4 / 0 / 0 | 68 / 12 / 0 / 0 |
| ambush | 73 / 7 / 0 / 0 | 68 / 12 / 0 / 0 |
| combined-arms | 68 / 12 / 0 / 0 | 70 / 10 / 0 / 0 |
| the other 7 scenarios | 80 / 0 / 0 / 0 | 80 / 0 / 0 / 0 |

Tactical maximum duration over all 800 battles: 11 pulses (auto: 7). Tactical events per battle: mean 964, max 3,127.
Battles end by morale collapse, not by annihilation. The rout threshold (morale 300) is crossed at about 35 % losses, which is
why the loser's casualty fraction concentrates at 36–40 %.

**Orientation check (tactical).** A-win % as built vs mirrored: equal 40.0 / 40.0, leadership 82.5 / 87.5, ambush 35.0 / 32.5,
combined arms 55.0 / 35.0, and every other scenario identical. No systematic compass effect is visible after the D-F1 fix.

**Side/id probe (equal-infantry, 40 seeds × 2 orientations; `m7s1/work/E/probe`).**

- With the formation ids swapped, the counts flip exactly: the higher-id formation won 44 and the lower-id formation 32, with
  4 draws, in both labellings. So there is no side-index effect.
- The 44 : 32 split among 76 decided battles is 1.4 σ from even. That is **not significant**, and no id bias is claimed.

## 3. Calibration (fit half only)

Procedure (`AutoCalibration.Fit`): deterministic coordinate descent over integer candidate grids. It minimises an integer loss
summed over the 10 scenarios. Per scenario:

    2(|ΔwinA| + |ΔwinB|) + |ΔcasA.p50| + |ΔcasB.p50| + (|ΔcasA.p10| + |ΔcasA.p90| + |ΔcasB.p10| + |ΔcasB.p90|)/2 + 100·|Δpulses.p50|

All terms are in permille. The loss is computed on the 400 FIT cases only (seeds 1–20, both orientations). A test shows that
corrupting the holdout tactical outcomes does not change the fit.

Tie rules: the current value is kept unless a candidate is strictly better, and the earlier grid entry wins between
candidates. The procedure starts from a stated pre-fit point (600 / 2000 / 600 / 1000 / 100 / 200 / 2 / 0) and stops after a
sweep with no change.

The fit took 261 evaluations and reduced the loss from 6,169 to 2,552:

| sweep | accepted moves (loss after) |
|---|---|
| 1 | Melee 600→500 (5464); MaxAttackersPerTarget 2000→500 (5398); KillEff 1000→300 (4834); Fortune 100→200 (3613); Noise 200→0 (3406); AmbushSteps 2→1 (3386); Surrender 0→50 (3281) |
| 2 | Melee 500→600 (3216); KillEff 300→600 (3084); Noise 0→100 (3022); AmbushSteps 1→0 (2842); Surrender 50→25 (2818) |
| 3 | Ranged 600→300 (2627); Noise 100→0 (2592); Surrender 25→50 (2577) |
| 4 | Melee 600→500 (2552); no further change |

**Fitted (= the shipped `AutoCoefficients` defaults; the bench verifies this and printed "YES"):**

| coefficient | value | meaning |
|---|---:|---|
| MeleeEngagePermille | 500 | share of alive entities striking per melee step |
| MaxAttackersPerTargetPermille | 500 | frontage: at most 0.5 strikers per alive enemy entity |
| RangedEngagePermille | 300 | share of free ranged entities getting a shot per step |
| KillEfficiencyPermille | 600 | damage → deaths conversion (spread damage wounds before it kills) |
| FortunePermille | 200 | per-battle per-side multiplicative swing ±20 % |
| NoisePermille | 0 | per-step noise; the fit found none useful |
| AmbushFirstStrikeSteps | 0 | the fit found no surprise window useful (see §6) |
| SurrenderPermille | 50 | 5 % of a routed formation's survivors recorded as Surrendered |
| DoctrineRetreatMorale / AmbushTriggerCells | 450 / 3 | NOT fitted: the `KnowledgeAi` defaults (PLACEHOLDER there) |

The fitted values are the measured best fit to placeholder rules. They are not gameplay values. They must be re-fitted, never
hand-tuned, whenever a tactical placeholder changes.

## 4. Tactical vs Auto — HOLDOUT half (seeds 21–40, both orientations, n = 40 cases, n_eff = 20)

T = tactical, Au = auto.

| scenario | A wins % T / Au | B wins % T / Au | draw % T / Au | cas A p10/p50/p90 % T | cas A p10/p50/p90 % Au | cas B p10/p50/p90 % T | cas B p10/p50/p90 % Au | pulses p50 T / Au (T p10–p90) | per-case victor agreement % (independence expectation %) |
|---|---|---|---|---|---|---|---|---|---|
| equal-infantry | 60.0 / 40.0 | 40.0 / 45.0 | 0.0 / 15.0 | 22/32/38 | 30/38/38 | 28/36/40 | 28/38/38 | 3 / 3 (3–9) | 47.5 (42.0) |
| numbers-2to1 | 100 / 100 | 0 / 0 | 0 / 0 | 8/16/20 | 14/18/22 | 36/36/48 | 36/36/36 | 3 / 3 (3–3) | 100 (100) |
| archers-vs-infantry-open | 100 / 100 | 0 / 0 | 0 / 0 | 0/0/0 | 0/0/0 | 36/40/40 | 38/40/40 | 3 / 3 (3–3) | 100 (100) |
| archers-vs-infantry-forest | 100 / 100 | 0 / 0 | 0 / 0 | 0/0/0 | 0/0/0 | 36/40/40 | 38/40/40 | 5 / 4 (5–5) | 100 (100) |
| low-supply | 0 / 0 | 100 / 100 | 0 / 0 | 36/36/44 | 38/38/38 | 10/14/18 | 14/16/20 | 3 / 3 (3–4) | 100 (100) |
| low-morale | 0 / 0 | 100 / 100 | 0 / 0 | 14/14/20 | 18/18/20 | 10/16/24 | 12/14/16 | 3 / 3 (3–3) | 100 (100) |
| leadership-edge | 92.5 / 100 | 7.5 / 0 | 0 / 0 | 22/32/38 | 30/36/44 | 36/38/40 | 38/38/38 | 3 / 3 (3–4) | 92.5 (92.5) |
| ambush | 32.5 / 40.0 | 57.5 / 45.0 | 10.0 / 15.0 | 30/36/38 | 30/38/38 | 26/34/36 | 28/38/38 | 5 / 5 (5–6) | 50.0 (40.4) |
| experience-edge | 100 / 100 | 0 / 0 | 0 / 0 | 10/18/26 | 14/16/20 | 36/38/40 | 38/38/40 | 3 / 3 (3–3) | 100 (100) |
| combined-arms | 52.5 / 40.0 | 35.0 / 55.0 | 12.5 / 5.0 | 37.2/41.8/45.4 | 33.6/37.2/40.9 | 38.1/42.7/45.4 | 30.9/37.2/40.9 | 4 / 4 (4–4) | 37.5 (40.9) |

The fit-half table has the same layout and is in the bench output (`m7s1/work/E/final.md`). Fit-half per-case agreement:
equal 50.0 %, leadership 77.5 %, ambush 50.0 %, combined 42.5 %, all others 100 %.

**Reading the agreement column.** In the balanced scenarios the two resolvers draw their randomness independently, so for the
same seed, per-case victor agreement cannot exceed what independent draws give. The "independence expectation" is
Σ_w p_T(w)·p_Au(w). The measured agreement sits at that expectation, which is what an unbiased resolver should show. In the
decisive scenarios (6 of 10) agreement is 100 %.

## 5. Proposed measurable parity tolerance

### 5.1 How the tolerance is derived

The tolerance is derived **only from the tactical resolver's own seed-to-seed spread** on the FIT half. It is then applied to
the HOLDOUT comparison. Code: `ParityTolerance.Derive` and `ParityCheck.Of`, integer, unit-tested by hand-computed cases.
n_eff = 20 independent seeds per half. A scenario passes when all of the following hold:

1. **Victor (modal):** if the tactical modal winner has a rate ≥ 60 %, the AutoResolver's modal winner must be the same.
2. **Win rate:** for each role, |win-rate gap| ≤ **3 σ of a difference of two binomial proportions**:
   `ISqrt(18·p(1000−p)/n_eff)` permille. p is the Laplace-adjusted tactical rate, (wins+½)/(n+1), taken for the role with the
   wider band. This makes the band 10.7 points for a 100 % / 0 % scenario and 43.6–47.2 points for the near-even ones.
3. **Casualties:** for each role, |median casualty-fraction gap| ≤ the largest of three bands:
   - **substantive band:** the tactical inter-quartile half-width (the spec's suggestion);
   - **sampling floor:** 3 σ of a difference of two medians, `3·√2·0.929·IQR/√n_eff`, where 0.929 = 1.2533/1.349 is the
     normal-approximation median SE from the IQR;
   - **resolution floor:** two entities of the role (4.0 % for 50; 3.4 % for 60; 1.8 % for 110).
4. **Duration:** |median pulse gap| ≤ max(1 pulse, IQR half-width, the same sampling floor applied to the pulse IQR).

Battery acceptance (proposed): **every scenario passes on a holdout of ≥ 20 independent seeds, and the tactical resolver
passes against itself** (fit half vs holdout half). The second condition guards against a band too tight to be met by
sampling noise alone.

### 5.2 Holdout results

Tolerances are in percentage points except pulses.

| scenario | tol win | tol cas A | tol cas B | tol pulses | modal victor | Auto gaps (win A/B, cas A, cas B, pulses) | Auto | Tactical self gaps | Tactical self |
|---|---:|---:|---:|---:|---|---|---|---|---|
| equal-infantry | 43.6 | 4.0 | 8.9 | 1 | B | 20.0/5.0, **6.0**, 2.0, 0 | **FAIL (cas A)** | 40.0/30.0, 4.0, 6.0, 0 | PASS |
| numbers-2to1 | 10.7 | 7.1 | 8.0 | 1 | A | 0/0, 2.0, 0.0, 0 | PASS | 0/0, 4.0, 0.0, 0 | PASS |
| archers-vs-infantry-open | 10.7 | 3.4 | 4.0 | 1 | A | 0/0, 0.0, 0.0, 0 | PASS | 0/0, 0, 0, 0 | PASS |
| archers-vs-infantry-forest | 10.7 | 3.4 | 4.0 | 1 | A | 0/0, 0.0, 0.0, 1 | PASS | 0/0, 0, 0, 0 | PASS |
| low-supply | 10.7 | 4.0 | 5.3 | 1 | B | 0/0, 2.0, 2.0, 0 | PASS | 0/0, 0.0, 2.0, 0 | PASS |
| low-morale | 10.7 | 4.0 | 7.1 | 1 | B | 0/0, 4.0, 2.0, 0 | PASS | 0/0, 2.0, 4.0, 0 | PASS |
| leadership-edge | 40.0 | 8.9 | 4.0 | 1 | A | 7.5/7.5, 4.0, 0.0, 0 | PASS | 15.0/15.0, 8.0, 2.0, 0 | PASS |
| ambush | 46.9 | 7.1 | 5.3 | 1 | — (57.5 % < 60 %) | 7.5/12.5, 2.0, 4.0, 0 | PASS | 2.5/0.0, 0.0, 2.0, 0 | PASS |
| experience-edge | 10.7 | 7.1 | 4.0 | 1 | A | 0/0, 2.0, 0.0, 0 | PASS | 0/0, 4.0, 0.0, 0 | PASS |
| combined-arms | 47.2 | 4.9 | 4.0 | 1 | — (45 % < 60 %) | 12.5/20.0, 4.6, **5.5**, 0 | **FAIL (cas B)** | 15.0/10.0, 0.9, 0.0, 0 | PASS |

**Verdict.** On the holdout, the AutoResolver meets the proposed tolerance in **8 of 10** scenarios, so it is **not at parity
under the proposed rule**.

- The two failures are casualty medians: equal-infantry role A, 38 % vs 32 % (band 4.0), and combined-arms role B, 37.2 % vs
  42.7 % (band 4.0).
- The victor, win-rate and duration criteria pass everywhere.

### 5.3 History of the tolerance rule (disclosed, not hidden)

The first version (v1, run40a) used three different choices:

| choice | v1 | v2 (current) |
|---|---|---|
| width of the win-rate band | 2 σ | 3 σ |
| n in the band | number of cases (80 per scenario, 40 per half) | n_eff = independent seeds |
| casualty band | IQR half-width or one entity | §5.1 item 3 |

v1 **rejected the tactical resolver against itself in 2 of 10 scenarios**:

- equal-infantry: win rate 20 % vs 60 % between the halves, and cas B 6 points against a band of 5;
- leadership-edge: cas A 8 points against a band of 5.

That made v1 an invalid band. Two reasons, both measured:

- the orientation pairs are correlated (§1);
- about 50 checks at 2 σ reject equal distributions by chance.

v2 fixes exactly those two points. Under v1 the AutoResolver also passed 8 of 10. The v2 change did not change the
AutoResolver's verdict.

**Pre-committed confirmation (verify-E E-V7).** v2 was chosen after it had been measured on the same seeds 1–40, so the
"tactical against itself 10/10" result above is not independent evidence for v2. To get independent evidence, v2 and the
shipped coefficients were frozen and recorded with file hashes before the run (`m7s1/work/fixE/logs/confirm-precommit.txt`).
They were then evaluated on seeds **41–80**, which had never been run before:
`parity 40 4 out.md out.csv 41 frozen` (no calibration; tolerance from tactical seeds 41–60; check on 61–80).

- **AutoResolver: 9 / 10.** It fails only combined-arms (cas A 3.7 and cas B 7.3 points against a 3.2-point band: the
  §6.2 structural gap). Equal-infantry passes on these seeds.
- **Tactical against itself: 10 / 10.**

Log: `m7s1/work/fixE/logs/confirm41-80.log`.

## 6. Where the AutoResolver deviates (model limitations, measured)

1. **Simultaneous routs.** The auto model has too many draws in equal-infantry: 12 of 80, against 4 of 80 tactical. It also
   overstates the casualties of the side that wins (holdout median cas A 38 % vs 32 %). The per-formation casualty-staircase
   phase lowers the simultaneous-rout rate, but only weakly: 5 of 40 symmetric battles were decided with it, 0 of 40 without.
   In the tactical engine the winner breaks contact sooner. The auto model's aggregate exchange cannot reproduce that
   asymmetry.
2. **Combined arms.** Auto casualties are about 5 points low for both roles (37 % vs 42 %). The auto model has no
   inter-formation shielding or retargeting dynamics beyond "nearest formation on the axis".
3. **Duration resolution.** Pulse medians match everywhere except forest (auto 4, tactical 5, within the 1-pulse band). Pulses
   are too coarse here (3–5 typical) to calibrate approach speed finely.
4. **Ambush.** The fitted surprise window is 0 steps. In the tactical engine an ambusher in Ambush posture does not, on
   balance, profit from surprise at these placeholders: role B (the ambusher) wins 57.5 % against 33.7 %, a lean of
   about 1.5–2 σ at n_eff = 40 seeds, which is not conclusive. The
   coefficient exists and is tested (`AmbushSurpriseWindowSilencesTheVictimForExactlyTheFittedSteps`), but its fitted value
   says the current tactical ambush mechanics give no first-strike edge.
5. **Doctrine coupling.** The AutoResolver ignores the controllers and assumes the `KnowledgeAi` doctrine: advance, nearest
   target, withdraw below morale 450, ambush trigger 3 cells. Parity is measured only for that doctrine. Any other
   controller, a human player included, needs its own parity check.
6. **No terrain defence bonus, no special abilities.** Neither resolver has them. Terrain enters both resolvers only through
   the two mechanisms the tactical engine has: movement cost and concealment (detection). Adding a defence bonus to one
   resolver only would create a parity gap by construction. No "special ability" exists in the unit data (the charter bands
   carry none).
7. **Placeholder dominance.** With the PLACEHOLDER stats, archers beat heavy infantry 80 of 80 with zero losses, even when the
   infantry defends in forest. The defender sees the archers and attacks into their fire. This is a property of the Slice D
   placeholders (D §8), faithfully reproduced by the auto model. It is not a balance claim.
8. **Exit paths are approximated (verify-E E-V3).** The tactical surrender rule now applies to the AutoResolver. A routed
   formation with no passable path to its exit edge surrenders entirely, and a trapped formation does not withdraw. The
   withdrawal TIME still uses the straight-line distance to the edge line at the current cell's cost, not the path length.
   A tactical retreater stuck on a trapped cell does not strike back, while the auto formation keeps fighting. The battery
   contains no impassable terrain, so none of this affects the numbers above. The fixes left all 800 auto results
   bit-identical.

## 7. Reproduce

```bash
cd prototypes/m7-sprint1
dotnet build M7Proto.slnx -c Release -nologo --disable-build-servers
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll parity 40 4 out.md out.csv   # ~110 s on 4 CPUs
dotnet test  M7Proto.slnx -c Release --no-build -nologo   # includes the reduced deterministic subset (3 scenarios x 3 seeds x 2)
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll parity 40 4 c.md c.csv 41 frozen   # §5.3 confirmation, ~33 s
```

- Raw per-case data (scenario, seed, orientation, half, winner, cas A, cas B, pulses, reason, events and ms for both
  resolvers): `m7s1/work/E/final.csv`.
- Bench output: `m7s1/work/E/final.log`.
- Earlier runs, kept as evidence: `run40a.*` (tolerance v1) and `run40b.*`.
- Cross-platform equality of any of these numbers is **UNTESTED** (O-17).

## 8. Open items for the Director (not decided here)

- **Adopt or amend the tolerance.** The rule in §5.1 is proposed, including:
  - the 60 % modal threshold;
  - 3 σ;
  - the two-entity resolution floor;
  - n_eff ≥ 20.
- **Whether 8/10 at parity is acceptable** for an optional auto-resolve, or whether the remaining casualty gaps must close
  first. Closing them needs either a richer auto model or tactical-side changes; it is not attempted here.
- **The doctrine assumption** (§6.5), and whether parity must hold per controller type.
- **Every tactical PLACEHOLDER** (D §3) and the condition mappings (`ConditionMath`: experience %, supply floor ½, leadership
  divisor). After any change the battery must be re-run and the coefficients re-fitted.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S488** (2 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`parity-report.md:257`) — "**No terrain defence bonus, no special abilities.** Neither resolver has them." — in part: DIRECTOR DECISION - terrain must influence AutoResolver outcomes (DIR5 §3.1) and terrain, concealment, forest cover, elevation and LOS must materially influence combat (§2.7); terrain influence in both resolvers is Phase C item 6 (§8); special abilities must be considered in the parity model (§3.1). Retained: add it to BOTH resolvers together (one-sided addition creates a parity gap by construction); the mechanism is DELEGATED TECHNICAL. **Replaced by** DIR5 §3.1, §2.7, §8 Phase C item 6 (D-079 DR21, DR34, DR51). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`parity-report.md:294`) — "**Whether 8/10 at parity is acceptable** for an optional auto-resolve, or whether the remaining casualty gaps must close" — in full: DIRECTOR DECISION (DIR5 §3.1, §10) - even 9/10 is insufficient, so 8/10 is too; improve the AutoResolver before adoption. Retained: the tolerance proposal as input to the Phase D acceptance-criteria recommendation (OPEN for the Director). **Replaced by** DIR5 §3.1, §10 (D-079 DR34, DR53). [DIRECTOR DECISION]
