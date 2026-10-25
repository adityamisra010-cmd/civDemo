# Empty-lane RP rule: a visible technology-lane reserve (PROPOSAL; NOT IMPLEMENTED)

**Status:** a design proposal with measurements, for the Director's review. The engine, the save schema and
`research.json` are unchanged. Everything below was measured with a test-harness model (`ResearchPacingRunner`) that sits
outside the engine, so nothing ships until the policy is ruled on.

## 1. The problem

RP is per turn and is never stored (D-044 R20-D). A technology lane that funds nothing in a step loses that step's
whole RP. It happens in three situations (`rp-cp-lifecycle.md` cases b, c, d, f, g):
- the lane has no destination;
- its destination waits on the other tree, or on the Age sweep;
- a destination or a repeatable level has just completed and the player has not re-ordered.

**The AI never leaves a lane empty**: it re-orders in the same step. **A human does.** CP does not have the problem:
it is banked (ADR-043), and the civics lane loses nothing.

**Human proxy.** The AI's own decisions, except that after a lane empties the proxy re-targets it only `d` turns later.
`d` = 1 is an attentive player; `d` = 3 is a casual one.

| Discard (today), overflow OFF | RP lost to empty lanes | Age 9 entered (mean of 4 seeds) | vs the AI |
|---|---:|---:|---:|
| AI (no delay) | 1.2 % | 1,199 | — |
| d = 1 | 3.4 % | 1,215 | +16 |
| d = 3 | 7.2 % (17 % in Age 4, 15 % in Age 5) | 1,244 | +46 |

A casual player ends up 46 turns behind the AI on research alone, for no reason the screens explain. That is the
defect this rule fixes. The k2–k5 display fixes now at least show the lost RP.

## 2. What was measured

`Sim.Tests/Kernel/ResearchEconomyMeasurement.EmptyLaneReserveStudy_FourSeeds` (run with `SIM_MEASURE=1`): seeds 1, 2,
42 and 7, 1,630 turns.

**Deposit rule (all variants).** A step whose technology lane funds nothing puts that step's RP into the reserve, up to
the cap. RP above the cap is lost.

**Variants:**
- **Cap.** 1 or 3 turns of the current RP pool, or uncapped.
- **Draw.**
  - EXACT: the reserve tops up the funded node to its remaining cost, without limit.
  - Draw-limited: at most 1 turn of RP per turn.
  - LOSSY: the drawn RP joins the step's points and obeys the same one-node rule, so any part the node cannot absorb is
    lost exactly as surplus RP is.
- **References.** Every policy was also run for the AI (no delay). A real rule applies to every polity, AI included.

Tables and raw data: §7 below, `data/reserve/`. Regenerate with
`python3 -I tools/research_economy_report.py reserve <dir> <trial-dir>`.

## 3. Findings

1. **Any reserve recovers the casual player's lag.** With d = 3, Age 9 arrives at 1,244 turns with discard, 1,219 with
   cap 1, 1,174 with cap 3 (EXACT draws), and 1,161 uncapped.
2. **EXACT draws reward idling.** This is the decisive finding. Under the same rule, the casual player beats the
   attentive AI:

   | Policy | AI reaches Age 9 | Casual player reaches Age 9 |
   |---|---:|---:|
   | Cap 3, EXACT | 1,194 | 1,174 |
   | Uncapped, EXACT | 1,170 | 1,161 |
   | Cap 3, EXACT, route overflow ON | 1,136 | 1,116 |

   The cause: banked RP tops a node up to exactly its cost, so it is never lost as completion surplus. Spent RP loses
   50 % or more to surplus late in the game. Banked RP is therefore strictly more efficient than spending, and leaving a
   lane empty becomes a strategy. Any adopted rule must remove this edge.
3. **LOSSY draws remove it.** Banked RP becomes exactly as efficient as the turn's own RP, no more:

   | Cap 3, draw at most 1 turn of RP, LOSSY | Age 9 entered | vs AI under the same rule |
   |---|---:|---:|
   | AI | 1,197 (−2 vs no rule) | — |
   | d = 1 | 1,194 | −3 |
   | d = 3 | 1,205 | +8 |

   The casual player's lag falls from 46 turns to 8. The attentive player is level with the AI (3 turns is within seed
   noise). The AI itself moves only 2 turns, so the rule barely touches AI pacing. Empty-lane loss for d = 3 falls from
   7.2 % to 0.3 %.
4. **Uncapped reserves hoard.**
   - With EXACT draws, the reserve reaches 46–49 turns of RP, and 2–49 turns of RP (above 20 on three of the four seeds) are still
     unspent at the end of the game (Age 9: one repeatable level per step cannot absorb it).
   - With LOSSY draws, the AI hoards 30 turns of RP, mostly banked during the Age-5–7 sweep waits, and the casual player
     still trails the AI by 16 turns.
   - A cap of 3 turns is enough. The reserve stays at or below 3.1 turns of RP on every run.
   - A cap of 1 turn is too small: the casual player still trails the no-reserve AI by 20 turns, even with EXACT
     draws.
5. **Bursts.** Under LOSSY at most 1 turn of RP, no draw exceeds one turn's RP (by construction) and no turn completes
   more than one technology (as in the shipped game). EXACT draws reach 3 turns of RP (cap 3) and 6.6 turns (uncapped),
   and the draw limit removes this at no measured pacing cost (uncapped, draw-limited: Age 9 at 1,161, same as without
   the limit).
6. **Overflow and the reserve cover different losses.**
   - Route overflow recovers surplus.
   - The reserve recovers empty-lane turns.
   - But the sweep waits that overflow creates (`overflow-trial.md` finding 3) last about 20 turns each, far beyond a
     3-turn cap. With cap 3 and overflow ON, the AI still loses 22.6 % of Age-7 RP to empty lanes.
   - The reserve does not solve that wait. See §5, decision 3.
7. **RP and CP stay separate.** The reserve holds RP only and feeds only the technology lane's own funding. That
   funding may still be a civic it assists, as the engine allows today. The CP bank is untouched in every run, and the CP
   identity holds everywhere.

## 4. Proposed rule (for review)

1. **Reserve.** Each polity has one RP reserve for its technology lane. A step whose technology lane funds nothing puts
   that step's RP pool into the reserve, up to a cap of **3 × the current RP pool**. RP above the cap is lost, and the
   screen says so.
2. **Draw.** On a step whose technology lane funds a node, up to **1 × the current RP pool** is drawn from the reserve
   and added to the step's points. It is spent by exactly the same rules as the pool: one node per step with overflow
   OFF, or along the route with overflow ON. It is never more efficient than the step's own RP.
3. **No automatic choice.** The reserve never picks a technology. Spending waits for the player's (or AI's) order.
4. **Lanes.** The CP bank is unchanged: the civics lane already banks, uncapped, as ruled in ADR-043. RP and CP never
   mix.
5. **Determinism.**
   - The reserve is a `long` milli-RP stock in a new serialized table: schema bump, populated-table round-trip test,
     replay pins.
   - Deposits and draws happen inside `ResearchSystem`'s step, read from PREV, in the fixed polity order.
   - No new randomness.

## 5. UI specification (required with the rule)

The research line of every surface (action surface, progression screen, Glass Box research card, status band) shows
three RP numbers and two CP numbers:
- **Technology:**
  - "+X RP this turn"
  - "reserve Y of Z RP" (Z = the cap)
  - "spent this turn W RP"
- **When the lane funds nothing:** "X RP go to the reserve (Y of Z)", or "reserve full - X RP unused this turn".
  The lane state from the k3/k4 fixes stays: idle, waiting on the other tree, or assisting.
- **Civics:**
  - "+X CP this turn"
  - "banked Y CP"
  - "spent this turn W CP"

Numbers use the next step's dt (the k5 fix). A tooltip on the reserve states the cap and the draw rule in one sentence.

## 6. Decisions requested

1. **Adopt the reserve as in §4: cap 3 turns, draw at most 1 turn, LOSSY?**
   - Alternatives measured: cap 1 (too small), uncapped (hoards), and EXACT draws (rewards idling).
2. **Adopt it together with route overflow** (`overflow-trial.md`), and refresh the pacing baseline once with both on.
   Measured with EXACT draws, cap 3 plus route overflow moves the AI's Age 9 to 1,136 (−63 turns). The LOSSY combination
   needs one more measurement after the engine change, because the harness cannot route drawn RP along an overflow route.
3. **The sweep wait under overflow is a separate question**: the technology lane waits about 20 turns on the civics
   cohort. Options:
   - (a) accept it;
   - (b) let a waiting technology lane assist the blocking civics cohort, using the existing assist path;
   - (c) a larger cap for sweep waits only.

   The reserve as proposed does not solve it.
4. Until those are ruled: no engine change, no schema change, no cost changes (instruction 4 of 2026-10-25).

## 7. Measured tables (generated)

### RP economy per policy (four seeds pooled)

| Policy | RP generated | RP lost | lost: surplus / empty lane | Deposited | Drawn | Max reserve (turns of RP) | Turns a draw exceeded one turn's RP | Largest draw (turns of RP) |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| ai | 56,061,755 | 54.5% | 53.3% / 1.2% | 0 | 0 | 0.00 | 0 | 0.00 |
| ai-route | 60,649,676 | 50.8% | 48.0% / 2.8% | 0 | 0 | 0.00 | 0 | 0.00 |
| ai-cap3 | 56,364,028 | 54.2% | 53.2% / 1.0% | 166,702 | 166,702 | 3.08 | 7 | 3.01 |
| ai-uncapped | 57,674,929 | 53.2% | 53.2% / 0.0% | 1,152,765 | 1,152,765 | 48.46 | 34 | 14.57 |
| ai-cap3-route | 60,945,485 | 50.5% | 48.0% / 2.6% | 264,903 | 264,903 | 3.05 | 11 | 3.00 |
| ai-cap3-lossy | 56,203,920 | 54.4% | 53.4% / 1.0% | 166,717 | 166,717 | 3.08 | 0 | 1.00 |
| ai-uncapped-lossy | 56,952,074 | 53.9% | 53.9% / 0.0% | 899,181 | 899,181 | 29.89 | 0 | 1.00 |
| d1-cap3-lossy | 55,724,831 | 55.7% | 55.1% / 0.6% | 1,876,595 | 1,849,488 | 3.06 | 0 | 1.00 |
| d3-cap3-lossy | 54,301,352 | 56.7% | 56.4% / 0.3% | 3,256,946 | 3,082,903 | 3.02 | 0 | 1.00 |
| d3-uncapped-lossy | 54,547,269 | 56.3% | 56.3% / 0.0% | 3,745,146 | 3,392,719 | 12.05 | 0 | 1.00 |
| d1-discard | 54,158,388 | 56.0% | 52.6% / 3.4% | 0 | 0 | 0.00 | 0 | 0.00 |
| d1-uncapped | 57,601,955 | 51.8% | 51.8% / 0.0% | 2,942,348 | 1,457,915 | 45.86 | 30 | 6.31 |
| d3-discard | 50,167,407 | 56.9% | 49.8% / 7.2% | 0 | 0 | 0.00 | 0 | 0.00 |
| d3-cap1 | 53,630,912 | 55.8% | 50.9% / 4.9% | 782,155 | 693,274 | 1.02 | 6 | 1.03 |
| d3-cap3 | 57,258,646 | 55.6% | 50.2% / 5.4% | 1,748,709 | 1,456,101 | 3.06 | 71 | 3.04 |
| d3-uncapped | 58,075,147 | 50.1% | 50.1% / 0.0% | 5,489,275 | 2,233,314 | 48.76 | 94 | 6.59 |
| d3-uncapped-draw1 | 57,870,967 | 50.3% | 50.3% / 0.0% | 5,549,664 | 2,268,997 | 47.76 | 0 | 1.00 |
| d3-discard-route | 55,361,998 | 56.8% | 46.3% / 10.6% | 0 | 0 | 0.00 | 0 | 0.00 |
| d3-cap3-route | 61,083,507 | 56.2% | 45.8% / 10.4% | 1,687,267 | 1,407,401 | 3.06 | 78 | 3.04 |

### Pacing: polity Age entered (effective turn), mean over the four seeds, and the shift against the AI reference

| Policy | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| ai | 67 | 344 | 699 | 735 | 804 | 947 | 1074 | 1199 |
| ai-route | 67 (+0) | 336 (-8) | 680 (-18) | 716 (-19) | 786 (-18) | 906 (-41) | 1040 (-34) | 1140 (-59) |
| ai-cap3 | 67 (+0) | 344 (+0) | 699 (+0) | 735 (+0) | 804 (-0) | 944 (-3) | 1070 (-4) | 1194 (-4) |
| ai-uncapped | 67 (+0) | 344 (+0) | 699 (+0) | 735 (+0) | 803 (-1) | 934 (-13) | 1056 (-18) | 1170 (-28) |
| ai-cap3-route | 67 (+0) | 336 (-8) | 680 (-19) | 716 (-19) | 783 (-21) | 902 (-44) | 1036 (-39) | 1136 (-63) |
| ai-cap3-lossy | 67 (+0) | 344 (+0) | 699 (+0) | 735 (+0) | 804 (-0) | 946 (-1) | 1073 (-1) | 1197 (-2) |
| ai-uncapped-lossy | 67 (+0) | 344 (+0) | 699 (+0) | 735 (+0) | 804 (-0) | 938 (-8) | 1062 (-12) | 1185 (-14) |
| d1-cap3-lossy | 67 (+0) | 345 (+0) | 699 (+0) | 729 (-6) | 806 (+2) | 937 (-10) | 1070 (-4) | 1194 (-5) |
| d3-cap3-lossy | 66 (-0) | 347 (+2) | 701 (+2) | 731 (-4) | 813 (+9) | 944 (-3) | 1074 (-0) | 1205 (+6) |
| d3-uncapped-lossy | 66 (-0) | 347 (+2) | 701 (+2) | 731 (-4) | 813 (+9) | 941 (-6) | 1070 (-4) | 1201 (+2) |
| d1-discard | 68 (+2) | 348 (+4) | 705 (+6) | 737 (+2) | 818 (+14) | 948 (+1) | 1084 (+10) | 1215 (+16) |
| d1-uncapped | 67 (+0) | 343 (-1) | 698 (-1) | 728 (-8) | 805 (+1) | 934 (-13) | 1056 (-18) | 1169 (-30) |
| d3-discard | 72 (+5) | 356 (+11) | 716 (+17) | 751 (+16) | 837 (+33) | 970 (+23) | 1106 (+31) | 1244 (+46) |
| d3-cap1 | 70 (+4) | 350 (+6) | 710 (+10) | 743 (+8) | 825 (+21) | 956 (+9) | 1090 (+15) | 1219 (+20) |
| d3-cap3 | 66 (-0) | 342 (-2) | 696 (-2) | 726 (-9) | 802 (-2) | 931 (-16) | 1060 (-14) | 1174 (-25) |
| d3-uncapped | 66 (-0) | 342 (-2) | 696 (-2) | 726 (-9) | 802 (-2) | 931 (-16) | 1048 (-27) | 1161 (-38) |
| d3-uncapped-draw1 | 66 (-0) | 342 (-2) | 696 (-2) | 726 (-9) | 806 (+2) | 931 (-16) | 1052 (-22) | 1161 (-38) |
| d3-discard-route | 72 (+5) | 347 (+2) | 698 (-2) | 734 (-1) | 814 (+10) | 934 (-12) | 1070 (-4) | 1184 (-15) |
| d3-cap3-route | 66 (-0) | 335 (-10) | 678 (-21) | 708 (-26) | 780 (-24) | 894 (-53) | 1026 (-48) | 1116 (-83) |

### Research bursts and end state (four seeds)

| Policy | Most tech in one turn (max over seeds) | Tech completed at end (mean) | Civics (mean) | Levels (mean) | Reconstruction mismatches |
|---|---:|---:|---:|---:|---:|
| ai | 1 | 525 | 480 | 299 | 0 |
| ai-route | 2 | 525 | 480 | 335 | 0 |
| ai-cap3 | 1 | 525 | 480 | 303 | 0 |
| ai-uncapped | 1 | 525 | 480 | 324 | 0 |
| ai-cap3-route | 2 | 525 | 480 | 338 | 0 |
| ai-cap3-lossy | 1 | 525 | 480 | 301 | 0 |
| ai-uncapped-lossy | 1 | 525 | 480 | 310 | 0 |
| d1-cap3-lossy | 1 | 525 | 480 | 291 | 0 |
| d3-cap3-lossy | 1 | 525 | 480 | 274 | 0 |
| d3-uncapped-lossy | 1 | 525 | 480 | 276 | 0 |
| d1-discard | 1 | 525 | 480 | 275 | 0 |
| d1-uncapped | 1 | 525 | 480 | 318 | 0 |
| d3-discard | 1 | 522 | 480 | 217 | 0 |
| d3-cap1 | 1 | 525 | 480 | 272 | 0 |
| d3-cap3 | 1 | 525 | 480 | 305 | 0 |
| d3-uncapped | 1 | 525 | 480 | 316 | 0 |
| d3-uncapped-draw1 | 1 | 525 | 480 | 312 | 0 |
| d3-discard-route | 2 | 525 | 480 | 277 | 0 |
| d3-cap3-route | 2 | 525 | 480 | 322 | 0 |
