# Pacing recalibration under the trial configuration (2026-10-26; NOTHING SHIPPED)

**Director ruling 2026-10-26:** "Proceed with pacing recalibration using the combined trial configuration" (route overflow +
capped reserve {3, 1} + gate-aware goals), "Do not change the default configuration to enable those three features yet",
"Keep pacing adjustments separate from node cost changes" and "Avoid restoring accidental RP waste as a pacing mechanism".

**What changed:** only the trial overlay, `trial/research-economy-trial.json`. It holds the three switches and its OWN
`pacingScale`. The shipped `research.json` is unchanged: same scale, switches absent. No node cost moved (cost batches are
measured separately, `cost-batches.md`). The pass-0 control (the overlay carrying the shipped scale) reproduces the earlier
trial's final hashes for seeds 1 (`eeee45cd…`) and 7 (`fb813a03…`), so the overlay mechanism adds nothing of its own.

## 1. Result

| | Shipped default (switches off) | Trial, shipped scale (pass 0) | **Trial, calibrated (pass 8)** |
|---|---:|---:|---:|
| RP lost, 1,630 turns, four seeds | 55.7 % | 49.5 % | **46.3 %** |
| RP lost, Ages 1–8 | — | 1.7–9.7 % per Age | **1.2–6.5 % per Age** |
| AI turns with the reserve at its cap | — | 0 | **0** |
| Every seed completes every Age, both trees | yes | yes | **yes** |
| Slowest seed vs target, technology Ages 2–8 | −81 … +6 | −178 … −15 | **−7 … +20** |
| Slowest seed vs target, civics Ages 2–6 and 8 | — | −159 … +3 | **−6 … +54** |
| Replay / save-load (all four seeds) | exact | exact | **exact** |

Calibrated scale (pass 8):

| Tree | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| technology | 0.395862 (held) | 0.943333 | 0.959209 | 2.66088 | 6.005702 | 9.786664 | 7.007961 | 0.926249 | 0.068141 (held) |
| civics | 0.645983 (held) | 0.989987 | 0.715481 | 0.9204 | 1.504901 | 1.729102 | 0.757433 (held) | 0.044319 | 0.016684 |
| shipped technology | 0.395862 | 0.803516 | 1.367587 | 3.40823 | 5.044871 | 4.822191 | 5.072868 | 0.775796 | 0.068141 |
| shipped civics | 0.645983 | 0.998856 | 0.907987 | 1.306316 | 1.396621 | 1.791944 | 0.757433 | 0.070609 | 0.008686 |

The calibrated trial is SLOWER than the uncalibrated trial in Ages 6–9, which is what the targets ask for: Age 9 is entered 132–175
turns later than pass 0. Waste went DOWN, not up. The pace comes from costs the AI pays, not from RP thrown away. Against the
shipped default (switches off), Age 4 is entered 112–120 turns sooner (the A4 entry target is turn 620; the default enters at
683–728, the trial at 571–608), and Ages 8–9 54–77 turns later (A9 entry target 1,400; default 1,134–1,264, trial 1,190–1,341).

## 2. Method and passes

`tools/recalibrate_trial.py` applies the shipped calibration algorithm (`recalibrate_observed.py`, D-063 R6) to the runner's
CSVs and writes only the overlay. The algorithm:
- **Target:** Age x complete in both trees by the midpoint of E[x+1] and E[x+2].
- **Target term:** the slowest seed binds.
- **Pincer:** each tree is pulled toward the pair's midpoint.

Two guards were added after pass 1, and they are written into the tool:
- **Bounded step:** `--max-step`, 1.5, then 1.25 from pass 3 on. Unbounded, pass 1 multiplied civics A7 by 4.9. The slower
  civics cohort then held the technology lane at the Age sweep, and the reserve sat at its cap for 86–161 turns per seed.
- **Frozen scales:** see §3.

| Pass | Step | Every seed finishes | Slowest seed, tech A2–A8 | Slowest seed, civics A2–A6, A8 | Tech A9 | Civics A9 | RP lost | AI turns at reserve cap |
|---|---|---|---|---|---:|---:|---:|---:|
| 0 | shipped scale | yes | −178 … −15 | −159 … +3 | −72 | −131 | 49.5 % | 0 |
| 1 | damp 0.7, unbounded | no | never | never | never | never | 35.8 % * | 529 |
| 2 | max 1.5×, A1 held | no | −84 … +38 | never | never | never | 50.0 % | 112 |
| 3 | max 1.25×, damp 0.5 | no | never | never | never | never | 48.4 % | 157 |
| 4 | + civics A7 held | yes | −42 … +0 | −44 … +27 | +69 | +12 | 51.5 % | 0 |
| 5 | | no | +2 … +37 | −20 … +21 | never | +65 | 48.5 % | 0 |
| 6 | | yes | −67 … +5 | −53 … +10 | +42 | −16 | 55.5 % | 0 |
| 7 | geometric mean of 5 and 6 | yes | −7 … +18 | −6 … +54 | +99 | +45 | 51.4 % | 0 |
| **8** | **7, technology A9 held at shipped** | **yes** | **−7 … +20** | **−6 … +54** | **+101** | **+47** | **46.3 %** | **0** |

\* Pass 1 lost less because three seeds never reached Age 9, where most loss happens. Waste is comparable across passes only when
Age 9 timing is.

**The response is not smooth.** A 7.5 % cut to technology A3 (pass 6) moved Age 3 by 59 turns, because the AI's choices are
discrete. Passes 5 and 6 bracketed the target. Pass 7 took their geometric mean.

Per-pass scales: `data/pacing/scales/p0..p8.json`. Every pass's summaries: `data/pacing/pacing-summaries.txt`. Pass 8's CSVs, AI
and proxies: `data/pacing/pacing-runs.tar.xz`. Tables regenerate with `tools/research_economy_report.py pacing <dir> p8 <dir> p0`.

## 3. Held scales: three residuals no scale can fix

| Scale | Held at | Why a scale cannot meet its target (measured) | Residual, slowest seed |
|---|---|---|---:|
| Age 1, both trees | shipped | Not cost-bound. Pass 1 cut both scales 13–15 % and seed 1's Age 1 finished LATER (255 → 259). The AI researches its next Age's milestones before the last Age-1 nodes. | +40 tech, +65 civics |
| Civics Age 7 | shipped | Bound to civics Age 6 by the AI's order. With no milestone goal the civics lane funds the `Cheapest` available civic (`AiResearchPolicy.DecideLane`). Age-7 civics open with Age 5, and all 46 are cheaper than `caliphate`, the dearest Age-6 civic, which gates the Age-8 sweep in BOTH trees. In pass 0 the 46 cost 183k CP and the lane cleared them quickly. In pass 3 they cost 498k CP and `caliphate` landed 100–277 turns after the rest of Age 6. Technology then had nothing fundable at the gate: 157 turns at the reserve cap, 22.9 % of Age-7 RP lost. Civics targets of 1,155 for A6 and 1,325 for A7 cannot both be met under this ordering. | −75 (early) |
| Technology Age 9 | shipped | Throughput-bound. With route overflow a lane completes at most ~2 nodes a turn (the route, then the destination). Age 9 has ~140 finite technologies, so it takes 140–145 turns after Age 8 at EVERY scale tried (0.025–0.068). The 60-turn window the targets leave (A8 1,460 → A9 1,520) is unreachable. A lower scale only raised Age-9 waste (84.5 % at 0.028, 75.9 % at the shipped 0.068) without moving the finish (1,540 → 1,542, seed 1). | +101 (seed 2) |

Civics A5 +54 is seed 2 only: the other seeds finish 73–77 turns early. It is left as is because the slowest seed binds.

## 4. Pass 8 in detail (calibrated trial; reference = pass 0, the uncalibrated trial)

#### Technology: turn each Age's finite nodes are complete (residual against the target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 269 (+29) | 480 (-30) | 649 (-41) | 758 (-62) | 905 (-65) | 1084 (-71) | 1235 (-90) | 1397 (-63) | 1542 (+22) |
| 2 | 280 (+40) | 510 (+0) | 693 (+3) | 821 (+1) | 971 (+1) | 1158 (+3) | 1318 (-7) | 1480 (+20) | 1621 (+101) |
| 42 | 271 (+31) | 485 (-25) | 657 (-33) | 770 (-50) | 900 (-70) | 1051 (-104) | 1174 (-151) | 1324 (-136) | 1469 (-51) |
| 7 | 271 (+31) | 485 (-25) | 655 (-35) | 765 (-55) | 897 (-73) | 1056 (-99) | 1191 (-134) | 1348 (-112) | 1489 (-31) |
| slowest seed | +40 | +0 | +3 | +1 | +1 | +3 | -7 | +20 | +101 |

#### Civics: turn each Age's finite nodes are complete (residual against the target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 281 (+41) | 477 (-33) | 641 (-49) | 756 (-64) | 894 (-76) | 1075 (-80) | 1183 (-142) | 1387 (-73) | 1484 (-36) |
| 2 | 305 (+65) | 509 (-1) | 700 (+10) | 821 (+1) | 1024 (+54) | 1149 (-6) | 1250 (-75) | 1470 (+10) | 1567 (+47) |
| 42 | 286 (+46) | 482 (-28) | 647 (-43) | 767 (-53) | 897 (-73) | 1044 (-111) | 1130 (-195) | 1327 (-133) | 1411 (-109) |
| 7 | 287 (+47) | 483 (-27) | 647 (-43) | 766 (-54) | 893 (-77) | 1048 (-107) | 1142 (-183) | 1343 (-117) | 1436 (-84) |
| slowest seed | +65 | -1 | +10 | +1 | +54 | -6 | -75 | +10 | +47 |

#### Polity Age entered (effective turn) — reference → this run

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 67 → 74 (+7) | 329 → 348 (+19) | 664 → 571 (-93) | 697 → 705 (+8) | 768 → 788 (+20) | 888 → 941 (+53) | 1003 → 1123 (+120) | 1097 → 1258 (+161) |
| 2 | 67 → 75 (+8) | 348 → 367 (+19) | 694 → 608 (-86) | 744 → 761 (+17) | 836 → 851 (+15) | 940 → 1046 (+106) | 1097 → 1197 (+100) | 1166 → 1341 (+175) |
| 42 | 66 → 74 (+8) | 333 → 351 (+18) | 673 → 578 (-95) | 711 → 716 (+5) | 773 → 799 (+26) | 896 → 932 (+36) | 969 → 1084 (+115) | 1058 → 1190 (+132) |
| 7 | 67 → 74 (+7) | 334 → 352 (+18) | 671 → 576 (-95) | 707 → 712 (+5) | 769 → 795 (+26) | 871 → 919 (+48) | 972 → 1092 (+120) | 1062 → 1210 (+148) |

#### RP lost by polity Age (four seeds pooled; lost = surplus past a funded node + a lane that funded nothing)

| Age | reference | this run |
|---|---:|---:|
| A1 | 3.5% | 3.0% |
| A2 | 2.4% | 2.4% |
| A3 | 1.7% | 1.2% |
| A4 | 6.9% | 3.6% |
| A5 | 7.5% | 3.9% |
| A6 | 5.3% | 4.0% |
| A7 | 8.0% | 3.0% |
| A8 | 9.7% | 6.5% |
| A9 | 63.4% | 75.9% |
| **All** | 49.5% | 46.3% |

Age-9 loss is surplus. Once the finite Age-9 content is done, a turn's RP exceeds what the route and the final-Age repeatables
(one level per step, ×1.2 growth) absorb. Repeatables are deferred by the ruling until pacing and cost calibration are complete.
This is the one remaining structural loss.

#### Reserve: time at the cap versus size (turns of RP), and the technology lane

| Seed | Turns at the cap | Turns holding a reserve | Largest (turns of RP) | Mean while held (turns of RP) | Technology waiting steps | Goal switches |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 0 | 0 | 0.00 | - | 0 | 0 |
| 2 | 0 | 0 | 0.00 | - | 0 | 3 |
| 42 | 0 | 0 | 0.00 | - | 0 | 0 |
| 7 | 0 | 0 | 0.00 | - | 0 | 1 |

#### Civics, roads, milestones and end state

| Seed | Civics done | Tech done | Levels | Road developments | Structures | Institutions | Median A5 / A7 / A9 milestone turn | CP identity |
|---|---:|---:|---:|---:|---:|---:|---|---|
| 1 | 480 | 525 | 235 | 699 | 55 | 15 | 669 / 917 / 1230 | holds |
| 2 | 480 | 522 | 137 | 941 | 57 | 15 | 715 / 939 / 1327 | holds |
| 42 | 480 | 525 | 314 | 789 | 57 | 15 | 678 / 901 / 1160 | holds |
| 7 | 480 | 525 | 288 | 859 | 55 | 15 | 676 / 847 / 1199 | holds |

Technologies done counts completed technology nodes including levelled repeatables. Seed 2 ends with 10 frontier repeatables
not yet started.

#### Determinism

| Seed | Reconstruction mismatches | Replay | Save/load at mid-run |
|---|---:|---|---|
| 1 | 0 | EQUAL | equal |
| 2 | 0 | EQUAL | equal |
| 42 | 0 | EQUAL | equal |
| 7 | 0 | EQUAL | equal |

## 5. Reserve behaviour, with the metric clarified

The earlier report's "35 turns at cap (d3) / 125 (d6)" counted TIME AT THE CAP: steps ending with the reserve at 3 turns of RP.
It is now reported apart from RESERVE SIZE in turns of RP, which is the reserve after a step divided by that step's RP.

| Player (calibrated trial) | Age entered A2 / A3 / A4 / A5 / A6 / A7 / A8 / A9 (mean of four seeds; shift vs the AI) | RP lost | Turns at the cap, per seed | Turns holding a reserve, per seed | Largest reserve (turns of RP) | Mean reserve while held (turns of RP) | RP drawn, four seeds |
|---|---|---:|---|---|---:|---:|---:|
| AI | 74 / 354 / 583 / 724 / 808 / 960 / 1124 / 1250 | 46.3 % | 0, 0, 0, 0 | 0, 0, 0, 0 | 0 | — | 0 |
| Casual, 3-turn delay | +0 / +0 / +0 / +0 / +1 / −14 / −2 / +1 | 47.6 % | 7, 4, 4, 2 | 194, 177, 161, 163 | 3.00–3.01 | 1.72–1.80 | 3,168,436 |
| Idling, 6-turn delay | +5 / +8 / +10 / +16 / +24 / +17 / +32 / +48 | 46.8 % | 30, 16, 23, 24 | 303, 284, 238, 261 | 3.01–3.07 | 2.19–2.23 | 3,122,361 |

- **The AI never uses the reserve.** Gate-aware goals keep its technology lane funded.
- **A casual player keeps pace with the AI.** They hold a reserve about 10–12 % of the turns, at under two turns of RP on
  average, and reach the cap rarely (2–7 turns per run).
- **Idling still loses ground,** 48 turns behind by Age 9. It holds a reserve on about 15–19 % of the turns and sits at the cap
  for 16–30.
- The cap is reached exactly; the largest reserve is 3.00–3.07 turns of RP. The excess over 3.00 is the next turn's RP being
  smaller than the turn's that filled the reserve.

## 6. Risks and open decisions

1. **Civics ordering (decision).** A civics-side counterpart of ADR-052 would let civics Age 7 be calibrated and remove the
   `caliphate` stall: when the technology lane is held at an Age sweep, the civics lane funds the blocking cohort first. It is
   not implemented, because it is new AI behaviour (queue).
2. **Age-9 throughput (decision).** Technology Age 9 needs ~140 turns whatever it costs. Accept the residual, or revisit it
   with the final-Age repeatables (deferred) or the lane variant of overflow (evidence only, ADR-050 §2.6).
3. **Seed spread.** Seed 2 trails the others by 40–70 turns in most Ages. The slowest-seed rule makes the other three seeds
   finish ahead of target (up to −195 in civics A7). This follows D-063 R6 and is not a defect, but a single-seed playtest will
   feel faster than the targets.
4. **Coupling.** The scale is calibrated for THIS configuration. Turning any of the three switches off, or applying a cost
   batch, moves completion turns. Each cost batch is therefore measured against pass 8, not the shipped default.

## 7. Civics Age 7 recalibrated with the civics gate rule on (2026-10-26, later)

**Director instruction (2026-10-26, "execute the next approved trial"):** "Recalibrate Civics Age 7 with the civics-gate rule
enabled. Use the existing bounded calibration procedure. Keep the other calibrated parameters fixed."

**Method.**
- **Procedure:** `tools/recalibrate_trial.py` with `--damp 0.5 --max-step 1.25`. Every scale is frozen except civics:7
  (`data/civics-a7/`).
- **Start:** pass 8 + `aiCivicsGateGoals`, which is bit-identical to pass 8 (`civics-gate-trial.md`, arm A).
- **Runs:** seeds 1, 2, 42 and 7, 1,630 turns.
- **Why q2–q4 ran in parallel:** completion did not move in q1, so those steps were fixed in advance. Each is exactly the
  next ×1.25 step.
- **Bracketing:** the procedure's own next step after q3 (1.744) and the geometric midpoint (1.606) were also measured.

| Pass | Civics A7 scale | Civics A7 complete (s1, s2, s42, s7) | Slowest-seed residual | Civics A8 / A9 (slowest) | Technology A8 / A9 (slowest) | Age 9 entered | RP lost | AI turns at the reserve cap |
|---|---:|---|---:|---|---|---|---:|---:|
| start (`cg1`) | 0.757433 | 1183, 1250, 1130, 1142 | −75 | +10 / +47 | +20 / +101 | 1258, 1341, 1190, 1210 | 46.3 % | 0 |
| q1 | 0.946791 | identical | −75 | +10 / +47 | +20 / +101 | identical | 46.2 % | 0 |
| q2 | 1.183489 | 1184, 1253, 1130, 1142 | −72 | +12 / +49 | +22 / +103 | +1…+2 | 46.2 % | 0 |
| **q3 (selected)** | **1.479361** | 1186, **1310**, 1135, 1143 | **−15** | +14 / +51 | +24 / +105 | 1261, 1345, 1192, 1211 | 46.2 % | 0 |
| q5m | 1.606201 | 1187, 1365, 1135, 1145 | +40 | +53 / +58 | +31 / **never** | 1262, 1371, 1196, 1212 | 46.1 % | 0 |
| q5 (the procedure's next step) | 1.743916 | 1186, 1430, 1134, 1143 | +105 | −65 / +64 | +37 / **never** | 1262, 1438, 1191, 1211 | 46.0 % | 0 |
| q4 | 1.849201 | 1186, 1468, 1135, 1143 | +143 | −29 / +99 | −25 / **never** | 1261, 1474, 1195, 1211 | 45.9 % | 0 |

**Why civics A7 hardly responds.**
- **Bound by technology.** On every seed the last Age-7 civics complete one turn after a technology prerequisite:
  - `insurance_underwriting` after `probability_statistics`;
  - `military_engineers` after `trace_italienne`;
  - `central_banking` after `paper_money`.
- **Up to 1.18:** the cohort's completion turn is set by technology, not by its CP price.
- **Seed 2 only:** its CP flow is the smallest, so it becomes cost-bound from about 1.4. Seeds 1, 42 and 7 never do.
- **Consequence for the procedure:** its pincer term pulls civics toward the technology midpoint. On the three
  prerequisite-bound seeds no price can satisfy it, so it keeps raising the scale (1.158 at q3) until seed 2 overshoots.

**Selected: 1.479361 (q3).**
- **Why:** it is the only measured value above the shipped 0.757 at which every seed completes every Age AND the slowest
  seed is within ±30 of the civics A7 target (−15).
- **Cost:** about +4 turns on the downstream slowest-seed residuals (civics A8/A9, technology A8/A9) and Age 9 entered 2–4
  turns later. RP lost is unchanged (46.2 %), and the AI never holds a reserve.
- **The next two steps fail.** The procedure's own next step (1.744) and the midpoint (1.606) both cost seed 2 its
  technology Age 9 within 1,630 turns. That is a hard regression, because the slowest seed binds.
- **Not done:** the other scales stay frozen, as instructed. The +4 drift downstream is reported, not compensated.
