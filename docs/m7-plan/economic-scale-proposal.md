# Economic scale of armies — PROPOSAL with measured evidence (M7 lane D, 2026-11-09)

**Status: PROPOSAL and evidence.** No data changes (D-079 §4 D10, D20); the calibration itself goes through the ADR-055
amendment A-38 and M7.14 later. The food-allocation ordering is **not chosen** (DR27, Q3 OPEN): every result below that
depends on it shows both orderings.

**Authority.** D-079 **DR25** (DIR5 §2.10): "Economic calibration should make 500–1,000 troops readily achievable while
making 50,000 troops difficult." "Recruitment costs and recurring maintenance should constrain normal military scale.
Calibrate resource acquisition rates logically. Hard caps may protect performance, but ordinary military scale should be
governed by the economy." §4 **D20** (DELEGATED): "The mechanism that makes 50,000 troops difficult (D-076 Q-L1);
re-measuring the population trace on HEAD before calibration (Q-L2)". DR26 (logistics; "Do not invent arbitrary costs or
duplicate existing resource models"), DR27 (food allocation, both orderings).

**Measurement tool.** `prototypes/m7-sprint1/Proto.M7.Econ/ScaleProbe.cs` (command `scale-probe`), read-only use of Sim.Core:
it steps the canonical founded world with **no orders** (the FOUNDED golden recipe, lane F's baseline) or with the **research
measurement driver** of `sim research --auto cheapest` (the generator of the calibration trace), and measures the shipped
recruitment predicate (`RecruitmentQuery.OwnPredicate`, ADR-055 §6.5) at checkpoints. All runs on HEAD `45c3313`, Release,
`nice -n 19`, a 4-CPU host shared with another agent. Raw outputs are listed in `prototypes/m7-sprint1/reports/H-D.md` §8.

Labels: **MEASURED** (Sim.Core itself, these runs); **MODEL** (lane F's comparison model, re-run on HEAD); **INFERRED**.

**[Lane CB, 2026-11-11, (D-082 DA3).]** [D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) DA3 answered Q-S1
(§6): DR25's numbers hold "the same at every stage" (lane D's option (a)). The research twins at turns 100, 325 and 400 that lane
D did not run are now MEASURED (§8.2), and §8 lays out the calibration path for absolute numbers at every stage. Statements
below that the new measurements change are marked in place with "(D-082 DA3)". No data change; the calibration itself is A-38
and M7.14.

---

## 1. Q-L2: the population trace re-measured on HEAD (MEASURED)

The conflict (D-076 Q-L2): "current seed-42 trace 83.7M at t1630 against older 506k". The trace is
`scripts/research-calibration/population-trace-seed42.csv`, the driver polity's population per turn under
`sim research --seed 42 --turns 1630 --auto cheapest`.

| Measurement | Result |
|---|---|
| CLI on HEAD, `sim research --seed S --turns 1630 --auto cheapest --trace-population` | runs in 56–59 s per seed |
| HEAD trace vs the committed CSV (seed 42) | **differs from turn 1** (5,096 against 5,118); ratio HEAD ÷ committed 0.9957 at t1, 0.9815 at t200, 0.9724 at t325, 1.0333 at t500, 1.0863 at t1000, **1.0747 at t1630 (89,971,315 against 83,718,486)** |
| Economy prototype (`scale-probe --driver research`) vs the CLI on HEAD | **byte-identical (turn, population) for all 1,630 turns, seeds 42, 1, 2, 7** |
| The committed CSV's provenance | `e41ee4c` (2026-10-24); 22 commits touching Sim.Core or Sim.Data in `e41ee4c..45c3313` |

**Resolution of Q-L2 (MEASURED):** on HEAD the driver polity reaches **80.1M–90.2M at turn 1630** (seeds 1, 2, 7, 42:
80,062,724; 81,578,464; 90,185,723; 89,971,315). The ~0.5M figure belongs to an older tree (the M6 baseline of 2026-10-22
measured 520,530–553,245 for the same command, `docs/m6-plan/knowledge-redesign/measurements/baseline-progression.md` T1);
the committed CSV (83.7M) is stale by −2.8 % to +8.6 % depending on the turn. Any force-size calibration must use HEAD's
trace (or a re-generated CSV), not either recorded figure. This lane regenerates nothing in `scripts/` (not in its scope).
*Superseded (verification round 1, D-R1-11): "−2.8 % to +8.6 %" holds only at the ten turns sampled in the table above. Over
all 1,630 turns, HEAD's seed-42 trace deviates from the committed CSV by **−4.62 % (turn 370: 8,406,671 against 8,813,574) to
+13.69 % (turn 1061: 73,059,072 against 64,259,400)**; 72 turns lie above +8.63 % and 84 below −2.76 %, and no turn is equal.
MEASURED in round 1 by a Release `Sim.Cli` built from this clone (`Sim.*` unchanged since `45c3313`), `research --seed 42
--turns 1630 --auto cheapest --trace-population`. Its trace is byte-identical to the round-0 trace and to the verifier's,
and every turn was compared with exact ratios.*

**The driver matters more than the seed (MEASURED):**

| Turn (sim-year) | No orders (6 seeds: 1, 2, 3, 5, 7, 42) | Research driver (4 seeds: 1, 2, 7, 42) |
|---:|---:|---:|
| 25 (y250) | 5,138–6,491 | 6,115–6,542 |
| 100 (y1000) | 9,126–11,487 | 23,527–25,842 |
| 200 (y2000) | 19,482–24,487 | 240,135–267,618 |
| 325 (y2875) | 37,779–47,369 | 3,503,402–4,081,244 |
| 500 (y3750) | — | 25,089,406–29,199,242 |
| 1000 (y5500) | — | 59,777,430–67,089,243 |
| 1630 (y6100) | — | 80,062,724–90,185,723 |

Lane F's evidence (`reports/H-F.md`) uses the no-order world. A polity that researches is 10.4–10.9× larger at turn 200 (same seed) and
78–86× larger at turn 325 (same seed). **Force-size calibration must use worlds with research progress.**

---

## 2. What the shipped predicate allows (MEASURED)

The shipped recruitment predicate accepts a formation at a settlement when (among content gates) the settlement has more
adults than the formation's personnel (one stays) and the purse's free **grain** covers `supportYears` (1.0) years of upkeep
at `upkeepGrainPerPersonYear` (1.0) for every formation of the owner, the new one included (`sim.json:364–365`;
`Sim.Core/State/RecruitmentQuery.cs:224–240`). In formations of the first recruitable identity (20 personnel), the extra soldiers it allows
are per × min(⌊(free grain ÷ 1.0 − soldiers) ÷ per⌋, Σ settlements ⌊(adults − 1) ÷ per⌋) (`ScaleProbe.Measure`).

**Validated against real orders (MEASURED):** lane F's max-recruitment twin (every adult-limited `RecruitFormation` order
issued, then one step) re-run on HEAD accepts **2,240–3,500** extra soldiers at turn 25 and **10,880–13,680** at turn 200
(no orders; six seeds) — exactly lane F's recorded ranges. The formula equals the twin where adults bind (turn 25: seeds 1, 3;
turn 200: four of six seeds) and is one formation (20) lower in two seeds at turn 200 (the twin judges the next step's tables);
at turn 25 **stone binds** in four of six seeds ("MaterialsShort: needs 5 stone"), which the formula does not model, so it
overstates there. Lane F's six baseline fixtures regenerated on HEAD are identical apart from the wall-time field, and its
battery CSV regenerated on HEAD has the same SHA-256 (`47b67aeb…cf0c`): lane F's figures stand on HEAD.

| Turn | World | Free purse grain | Adults | Predicate bound (extra soldiers) | What binds |
|---:|---|---:|---:|---:|---|
| 25 | no orders | 5,446–6,899 | 2,898–3,648 | 2,240–3,500 (twin) | adults; stone (4 of 6) |
| 25 | research | 6,448–6,929 | 3,445–3,683 | **2,260–3,540 (twin)**; formula 3,320–3,540 | stone (3 of 4: seeds 42, 2, 7); adults (seed 1) |
| 100 | no orders | 9,667–12,321 | 5,146–6,466 | 5,040–6,340 | adults |
| 100 | research | 24,877–27,416 | 13,226–14,530 | 13,140–14,380 | adults |
| 200 | no orders | 20,920–26,450 | 10,974–13,795 | 10,880–13,680 (twin) | adults |
| 200 | research | 284,781–317,754 | 134,941–150,385 | **134,840–150,240** (formula); twin 134,840–150,260 (round 2) | adults |
| 325 | no orders | 40,679–51,662 | 21,285–26,687 | 21,120–26,540 | adults |
| 325 | research | 4,436,168–5,157,109 | 1,966,112–2,289,200 | 1,966,000–2,289,080 | adults |
| 400 | research | 20,403,966–21,576,011 | 8,957,354–10,078,483 | 8,957,200–10,078,380 | adults |
| 500 | research | 0 (3 seeds); 20,140,268 (seed 42) | 14.6M–16.9M | **0** in 3 of 4 seeds | no grain in the purse |
| 650–1630 | research | **0** in all 4 seeds at every checkpoint | 20.9M–53.6M | **0** | no grain in the purse |

*Superseded in part (verification round 2, D-R2-05): the research row at turn 25 read "3,320–3,540 | adults". Those numbers
were the probe's formula, never validated by a real twin: the probe ran the twin only without orders, so the research twins file
held only a header. The real twin on research-driven worlds (MEASURED this round on HEAD, Sim.* unchanged since `45c3313`;
lane F's `Calibration.MaxRecruitment`, horizon 1) accepts 3,080 (seed 42), 3,540 (seed 1), 3,000 (seed 2) and 2,260 (seed 7)
extra soldiers against the formula's 3,320, 3,540, 3,520 and 3,380: stone binds in three of four seeds ("MaterialsShort: needs 5
stone"), up to 1,120 soldiers below the formula; adults bind only in seed 1. The row now shows both. MEASURED twice: a
scratchpad harness calling the twin directly, and the probe itself (`scale-probe --driver research --twin-at 25`, which runs the
twin for either driver since round 2), with equal results. The conclusion of §3 holds: at least 2,260 at turn 25.
At turn 200 the research twin (MEASURED this round with the same harness) accepts 134,840 (seed 42), 150,260 (seed 1), 147,920
(seed 2) and 139,600 (seed 7) extra soldiers against the formula's 134,840, 150,240, 147,920 and 139,600: every issued order was
accepted in every seed ("Ok (cap reached by the order count)"), so adults bind, as the row says; the twin is one formation above
the formula in seed 1 (INFERRED: the twin issues ⌊adults ÷ 20⌋ orders per settlement, one more than the formula's
⌊(adults − 1) ÷ 20⌋ where a settlement holds an exact multiple of 20 adults, and the predicate judges them during the step). The
research rows at turns 100, 325 and 400 are still the formula only (no twin was run there).*
*(D-082 DA3) Lane CB, 2026-11-11: replaces "the research rows at turns 100, 325 and 400 are still the formula only". At turn 100
the full research twin (six seeds, 42, 1, 2, 7, 3, 5) accepts 13,140, 14,380, 14,200, 13,440, 10,360 and 11,240 extra soldiers
at once, equal to the formula in five seeds and one formation (20) above it in seed 2; adults bind. At turns 325 and 400 the full
twin cannot be run (its cost grows with the cube of the orders it accepts: §8.2), so capped twins were run: with at most 220 and
then 1,000 orders per settlement, every order was accepted in every seed, 52,800 and 240,000 soldiers at once. MEASURED, §8.2.*

Two findings (MEASURED):
1. **Wherever grain is present the predicate's binding constraint is the adult population**, not the economy: every adult but
   one per settlement may be recruited at once. With research progress **50,000 soldiers are recruitable instantly from about
   turn 200** (sim-year 2000).
   *Superseded in part (verification round 2, D-R2-05): not at turn 25, where stone binds in four of six no-order seeds (as
   stated above) and in three of four research seeds (the twin; see the note under the table).*
2. **In later research-driven eras the purse holds no grain at most measured boundaries**: all four seeds at every
   checkpoint from turn 650 to 1630 and three of four at turn 500; seed 42, sampled more densely, has 0 at turns 425, 450,
   475, 550, 600 and 650 and 20,140,268 at 500; the predicate then refuses every recruitment ("cannot carry the upkeep … the
   purse has 0 free") although the polity's food surplus over all food goods is positive (6.3M–32.8M a year at these
   turns). The gate reads grain only; later-era diets are not grain-only. So in later eras the shipped predicate is an
   intermittent on/off switch, not a cost. (Why grain reaches 0 is INFERRED to be consumption of the grain stock by the food
   baskets; not isolated here.)
   *(D-082 DA3) Lane CB, 2026-11-11: measured again in six seeds (42, 1, 2, 7, 3, 5) at turns 500, 650, 1000, 1629 and 1630, the
   purse holds no grain at 29 of the 30 points (seed 42 at turn 500 is the exception, with the same 20,140,268) and the predicate
   refuses every recruitment there; a twin capped at 50 orders per settlement (run at turns 500 to 1629) issues no order at the 23
   such points where it ran and accepts all 600 at seed 42's turn 500. MEASURED, §8.2.2. Why the grain reaches 0 was not isolated
   here either.*

---

## 3. "500–1,000 readily achievable" (evidence)

- **Recruiting them:** yes in every measured world and horizon where grain is present (≥ 2,240 at turn 25, no orders;
  ≥ 2,260 at turn 25 with research, the real twin, MEASURED in verification round 2).
- **Keeping them as a garrison:** yes. Lane F (MODEL, both orderings identical): a 1,000-man garrison is fed in full at turns 25
  and 200 in NORMAL harvests; in a SEVERE two-season shortfall it gets 90.6 % of its ration under CGM and CMG alike
  (`reports/H-F.md` §4.1). Its cost is labour: −7.4 % population at +100 turns against peace (T0 = 25, both orderings).
- **Campaigning with them at turn 25 depends on logistics and, slightly, on the ordering** (lane F MODEL, 21-day pulses and
  march legs, FULL haul; **CGM / CMG**):

| Army (T0 = 25) | Regime | Ration supplied | March days granted | Growth claim served |
|---|---|---|---|---|
| WAR-500 | NORMAL | 100 / 100 % | 81.4 / 82.4 % | 100 / 73.2 % |
| WAR-1000 | NORMAL | 100 / 100 % | 28.3 / 29.1 % | 100 / 20.6 % |
| WAR-1000 | BAD-1s | 53.8 / 54.2 % | 18.8 / 19.1 % | 87.9 / 17.7 % |

  The march-day share depends strongly on the unratified leg length (lane F §15: 76–77 % for WAR-1000 at 7-day legs, 14–15 %
  at 42-day legs). At turn 200 both are fully supplied in NORMAL and BAD under both orderings. **The orderings differ by at most
  about one point in what the army gets; they differ a lot in what population growth gets** — which is DR27's question, not
  this one.

**Reading:** "readily achievable" holds for recruitment and garrisons today; for a marching 1,000 at the earliest horizon it is
a logistics question (the haul from the depot), which the supply-train capability (`docs/m7-plan/support-capabilities-proposal.md`)
and lane E's carriage model address — not a recruitment-cost question.

---

## 4. "50,000 difficult" (evidence)

| Horizon | No orders | Research driver |
|---|---|---|
| ≤ turn 100 | impossible: the whole adult population is 5,146–14,530 | impossible |
| turn 200 | impossible (≤ 13,680 recruitable) | **recruitable at once** (134,840–150,240 allowed) |
| turn 325 | impossible (≤ 26,540) | trivially (2.2–2.5 % of the adults) **(D-082 DA3: MEASURED by capped twins, six seeds: 240,000 accepted at once, every order; §8.2)** |
| turns 500–1630 | — | **blocked** at most measured boundaries by the empty grain purse, not by its cost **(D-082 DA3: six seeds, 29 of 30 points; §8.2.2)** |

**What 50,000 soldiers cost to keep under the shipped rules (INFERRED from measured quantities):** a soldier eats exactly what
an adult civilian eats (`sim.json:364`, DERIVED 1.0), so the only recurring cost is the labour he no longer gives:
1.99–2.86 grain per removed adult-year at turn 200 (lane F's labour twins, MEASURED; reproduced on HEAD by the identical
fixtures). 50,000 soldiers therefore cost about 99,500–143,000 grain a year — **34–56 % of the research world's food surplus
at turn 200** (255,415–291,684 a year, MEASURED), but **1.3–3.8 % at turn 325** (3.8M–7.5M a year) and **0.3–0.6 % at turn
1630** (24.3M–31.4M a year). The economy grows by four orders of magnitude over the game; a fixed number of soldiers becomes
cheap by the same factor.

---

## 5. The existing levers and what each would do (PROPOSAL; no data change)

| Lever (existing) | Where | What it does to scale | Evidence | Consequence |
|---|---|---|---|---|
| **L1 `supportYears`** (TUNE) | `sim.json:365` | divides the instantaneous grain bound | at turn 200 (research) the grain bound is 284,781–317,754 persons against 134,941–150,385 adults: below `supportYears` ≈ 2 it never binds; to refuse a 50,000 army at once there it must exceed 5.7–6.4 | a stock test, not a recurring cost: a polity can wait and accumulate; useless where the purse has no grain (§2 finding 2) |
| **L2 `upkeepGrainPerPersonYear`** (DERIVED 1.0) | `sim.json:364` | a soldier's ration above a civilian's adds (u − 1) grain per soldier-year to the labour cost | lane E's prototype already prices the field ration (+0.3) and march exertion (+0.6) per man-year (`reports/H-E.md` §3) | changes a DERIVED value; scales with soldiers only, so it shares §4's dilution over the game |
| **L3 recurring non-food maintenance** (pay, kit wear, munitions) | lane E PROPOSALS; charter §6.7 "Upkeep: food per person plus equipment wear" | a cost per soldier-year in goods the economy makes (cloth, iron, powder, fuel) and in money where coinage exists | lane E measures the cascade (arrears → morale → desertion → mutiny → disbandment) per pulse | needs the ADR-055 amendment (A-38); its per-soldier size can follow the identity's Age through its EQUIPMENT (computed state, Law 4), not a date |
| **L4 recruitment inputs** (one-off) | `unit-families.json` `recruitment.inputs` | materials per formation | MEASURED: stone binds at turn 25 in 4 of 6 seeds (no orders) and in 3 of 4 research seeds (twin, verification round 2: up to 1,120 soldiers below the adult bound); never binds later in the twins measured (turn 200) | an early brake only |
| **L5 the adult predicate** | `Sim.Core/State/RecruitmentQuery.cs:224–229` | every adult but one per settlement | binds everywhere grain is present | a ceiling at the whole adult population, not a cost |
| **L6 food and logistics** | lanes E and F | campaigning armies are limited by the haul | lane F (both orderings, §3); WAR-LARGE (12,563 at T0 = 200) gets 3.8 % of its ration under both orderings | governs CAMPAIGNING scale, not standing scale |

**Proposal (labelled):** the mechanism DR25 asks for is **L3 through A-38** — a recurring per-soldier maintenance in produced
goods and money, settled per War Pulse (DR14) through lane E's existing path, sized per identity by its equipment class —
with **L6** as the campaign limit it already is, and **L1/L5 left as they are** (they are gates, not costs). And, because of §2
finding 2, A-38 should also decide what the support test reads (all food goods, or the soldiers' actual ration source) — a
Sim.Core behaviour this lane measured but does not change. The calibration battery for A-38: research-driven worlds, several
seeds, horizons at least turns 25, 100, 200, 325, 500 and 1000, reporting the sustained force each horizon's surplus pays for.

---

## 6. For the Director (genuinely undecided)

**Q-S1. At which stage of the game do DR25's two numbers apply?** The measured economy grows from about 6,000 people at turn
25 to 80–90 million at turn 1630 (research driver), so no per-soldier cost can make "50,000 difficult" at every stage without
making "500–1,000 readily achievable" false at the earliest ones.

| Alternative | Consequence |
|---|---|
| (a) **At every stage, as absolute numbers** | the per-soldier cost must grow with the era by orders of magnitude (equipment-based L3); a late polity of 90M people would find 50,000 soldiers (0.06 % of its people) "difficult" only if each costs on the order of hundreds of grain-equivalents a year — INFERRED implausible against modern mass armies |
| (b) **At a reference stage** (for example the early and middle Ages the M7 battles first cover), larger armies becoming affordable as the economy grows | L3 sized so that at the reference horizon 50,000 takes a large share of the surplus (at turn 200 the labour cost alone is already 34–56 %); later eras naturally field larger armies |
| (c) **As shares of the polity** (for example "readily" below about 1 % of adults, "difficult" above some share of adults or of the surplus) | one calibration for all stages; DR25 restated in relative terms by the Director |

No alternative is recommended over another here: the choice sets what M7.14 calibrates against. (A-38 cannot be calibrated
until it is answered; the food-ordering decision Q3 also gates A-38, D-079 A-38.)

**(D-082 DA3) Answered: (a).** "Same at every stage" (DA3). Lane D's stated consequence of (a), "a per-soldier cost growing by
orders of magnitude with the era", and its INFERRED doubt ("implausible against modern mass armies") are examined in §8 with the
measured surplus; the A-38 calibration targets absolute numbers at every stage (D-082 §3).

---

## 7. Limitations

- One driver (cheapest research first); a player or AI that researches differently grows differently.
- The predicate bound counts formations of the FIRST recruitable identity (20 personnel) and ignores material inputs (stone
  binds early); the real twin is the authority where they differ (turn 25: four seeds).
  *Superseded in part (verification round 2, D-R2-05): at turn 25 they differ in four of six no-order seeds and in three of four
  research seeds; the research twins were measured only in round 2 (§2 note).*
- Food surplus is all food goods (production − civilian consumption); the labour cost per soldier (1.99–2.86) was measured at
  turns 25 and 200 only (lane F); later horizons are INFERRED to be similar.
- No campaign war was run on the research-driven worlds; §3's campaigning evidence is lane F's no-order MODEL.

---

## 8. DA3: DR25's numbers at every stage — the calibration path (lane CB, 2026-11-11)

**Status: PROPOSAL and evidence.** No data change; the calibration itself goes through the ADR-055 amendment A-38 and M7.14
(D-082 §5: "the calibration numbers for DA3 (A-38, M7.14)" stay OPEN). The food-allocation ordering is not chosen (DR27; Q3
OPEN). Lane report: `prototypes/m7-sprint1/reports/H-CB.md`.

### 8.1 The decision

[D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) **DA3** (DECIDED, answers Q-S1): "DR25's numbers ("500–1,000
troops readily achievable", "50,000 troops difficult") hold the same at every stage of the game: lane D's option (a), absolute at
every stage." Director's words: "Same at every stage". D-082 §3: "The A-38 calibration targets absolute numbers at every stage";
D-079 D20 and D-076 Q-L1 "(the mechanism that makes 50,000 troops difficult): narrowed by DA3; the mechanism must hold at every
stage." On HEAD the research driver's polity numbers 80.1M–90.2M at turn 1630 (lane D, MEASURED, §1); with this lane's two
further seeds, 69.1M–104.6M (MEASURED, §8.2.2).

### 8.2 What the shipped rules allow at each stage (MEASURED)

The economy prototype's probe (`scale-probe --driver research`, Release build of `Sim.Core`, `nice -n 19`), the real
max-recruitment twin (lane F's `Calibration.MaxRecruitment`, horizon 1: every adult-limited `RecruitFormation` order issued, then
one step) and the new capped twin (`--twin-caps`: at most that many orders per settlement, a lower bound of the full twin). Raw
outputs: `…/scratchpad/m7c/CB-work/econ/` (`m2/`, `m1r/`, `m3a/`, `m3b/`, `m4a/`, `m4b/`).

| Turn | Seeds | What was run | Extra soldiers accepted AT ONCE | What binds |
|---:|---|---|---|---|
| 25 | 42, 1, 2, 7 | full twin (lane D, round 2) | 2,260–3,540 | stone (3 of 4), adults |
| **100** | **42, 1, 2, 7, 3, 5** | **full twin (lane CB)** | **13,140; 14,380; 14,200; 13,440; 10,360; 11,240** | adults ("PersonnelShort" in at least one settlement of every seed; the rest "Ok (cap reached by the order count)") |
| 200 | 42, 1, 2, 7 | full twin (lane D, round 2; seed 42 again here, 134,840) | 134,840–150,260 | adults |
| **325** | **42, 1, 2, 7, 3, 5** | **capped twins, 220 and 1,000 orders per settlement** | **every order accepted: 52,800 and 240,000** | the cap (no refusal) |
| **400** | **42, 1, 2, 7, 3, 5** | **capped twins, 220 and 1,000** | **every order accepted: 52,800 and 240,000** | the cap (no refusal) |
| **500** | **42, 1, 2, 7, 3, 5** | **probe; twin capped at 50 orders per settlement** | **seed 42: 12,000 (every order); the other five: 0 (none recruitable)** | seed 42: the cap; the others: the empty grain purse (§2 finding 2) |
| **650, 1000, 1629** | **42, 1, 2, 7, 3, 5** | **probe; twin capped at 50** | **0 in every seed (none recruitable)** | the empty grain purse (§8.2.2) |
| **1630** | **42, 1, 2, 7, 3, 5** | **probe only** (no twin at the last turn, §8.2.2) | the predicate refuses in every seed | the empty grain purse |

At turn 100 the twin equals lane D's formula bound in five seeds (42: 13,140; 1: 14,380; 7: 13,440; 3: 10,360; 5: 11,240) and is
one formation (20) above it in seed 2 (14,200 against 14,180), the effect lane D inferred at turn 200 (an order judged during the
step). At turns 325 and 400 lane D's formula bound is 1,966,000–2,289,080 and 8,957,200–10,078,380 (adults bind, §2); the capped
twins show the predicate accepts at least 240,000 at once in every seed, and no order was refused.
*Verification round 1 (CB-R1-04): the two ranges just quoted are lane D's four seeds (42, 1, 2, 7) in a six-seed paragraph. Over
the six seeds the formula bound is **1,330,040–2,289,080** at turn 325 and **5,638,120–10,078,380** at turn 400 (seeds 3 and 5 are
the lowest: 1,330,040 and 1,518,320; 5,638,120 and 6,590,800). MEASURED: this lane's M3 probe CSVs (`m3a/`, `m3b/`), reproduced
row for row in round 1 by a fresh probe run without twins (Release `Sim.Core` built from this branch; 12 of 12 rows identical).*

#### 8.2.1 Why the full twin was not run at turns 325 and 400 (MEASURED cost, INFERRED extrapolation)

`Sim.Core` judges each `RecruitFormation` order with `RecruitmentQuery.OwnerPersonnel`, which visits every formation the owner
holds and finds each one's unit row with a linear scan (`RecruitmentQuery.UnitIndex`), so the k-th accepted order costs about k²/2
row visits and one step of n accepted orders about n³/6. Timed at turn 200, seed 42, Release `Sim.Core` (`m1r/stdout.txt`):

| Orders accepted | 600 | 1,200 | 2,400 | 4,536 | 6,742 (the full twin) | 12,000 (turn 325 and 400, cap 1,000) |
|---|---:|---:|---:|---:|---:|---:|
| Wall time | 0.18 s | 0.39 s | 2.16 s | 14.3 s | 51.1 s | 291–303 s (twelve runs: six seeds × two turns) |

The local exponent between the last two turn-200 points is 3.2 (ln(51.1 ÷ 14.3) ÷ ln(6,742 ÷ 4,536)); the cubic fit from the full
twin, 51.1 s × (12,000 ÷ 6,742)³ = 288 s, matches the measured 291–303 s. Extrapolated (INFERRED): a full twin at turn 325 (about
98,000–114,000 orders) would take 44–69 hours, and at turn 400 (about 450,000–500,000 orders) 173–247 days, each.
*Verification round 1 (CB-R1-04): those order counts and times are lane D's four seeds only. Over the six seeds (orders = formula
bound ÷ 20, the same cubic fit, DERIVED): about 66,500–114,500 orders and **13.6–69.4 hours** at turn 325; about 282,000–504,000
orders and **43.2–247.0 days** at turn 400 (seeds 3 and 5 are the cheapest). The conclusion stands: the full twin is not feasible
here.* The capped twins
answer the decision-relevant question (is 50,000 recruitable at once?) in 3 s (cap 220) and the stronger bound (240,000) in five
minutes. (The same twins with the Debug `Sim.Core` that `M7Econ.slnx` builds ran 6 to 21 times slower on this shared host:
`m1-debug-simcore/` against `m1r/`. The accepted counts are identical: seed 42 at turn 100 gives 13,140 in both builds.) The quadratic `OwnerPersonnel` scan is a
`Sim.Core` cost observation; nothing in `Sim.*` changed (D-079 Q5).

#### 8.2.2 Late stages: the support test refuses recruitment at 29 of 30 points (MEASURED)

Run M4 (`m4a/`, `m4b/`): the research-driven worlds of the six seeds, the probe at turns 500, 650, 1000, 1629 and 1630, and at
turns 500, 650, 1000 and 1629 a twin capped at 50 orders per settlement (at most 1,000 soldiers per settlement and 12,000 for the
polity's 12 settlements, enough to test "500–1,000 readily achievable").

| Turn | Driver polity (people) | Free grain in the purse | First recruitable identity | Capped twin (50 orders per settlement) | Food surplus per year |
|---:|---:|---|---|---|---:|
| 500 | 21.3M–29.6M | seed 42: 20,140,268; seeds 1, 2, 7, 3, 5: 0 | seed 42: Skirmishers (20 personnel); the others: none | seed 42: 600 orders, 12,000 accepted, every order ("Ok (cap reached by the order count)"); the others: no order (none recruitable) | 5.54M–12.54M |
| 650 | 29.3M–43.7M | 0 in all six | none | no order in any seed | 7.87M–15.12M |
| 1000 | 46.8M–79.7M | 0 in all six | none | no order in any seed | 11.84M–26.29M |
| 1629 | 69.1M–104.6M | 0 in all six | none | no order in any seed | 24.42M–31.76M |
| 1630 | 69.1M–104.6M | 0 in all six | none | NOT RUN: the campaign's last turn (below) | 24.33M–31.35M |

- **The refusal (MEASURED):** at each of the 29 points without grain the predicate gives the same reason, "Skirmishers: cannot
  carry the upkeep: 40 grain needed for a year of every formation (the purse has 0 free)": the polity's 20 soldiers and the new
  formation's 20, at 1.0 grain per person-year for `supportYears` 1.0. The adults are there (12.4M–61.9M) and the food surplus over
  all food goods is positive at every point. Lane D's §2 finding 2 (four seeds) holds in six: under the shipped support test "500–1,000
  readily achievable" fails at 29 of the 30 late points, and "50,000 difficult" holds there only because nothing at all can be
  recruited. The test is an on/off switch, not a cost.
- **Seed 42 at turn 500:** the twin's 12,000 is its cap, not the predicate's bound (MEASURED). Lane D's formula bound there is
  16,222,940 (adults bind below the grain bound of 20,140,268), so 50,000 is recruitable at once at that point (INFERRED from the
  formula, which the full twins equal or exceed by one formation at turns 100 and 200).
- **Lane D's numbers reproduce exactly (MEASURED):** seeds 42, 1, 2 and 7 reach 89,971,315; 80,062,724; 81,578,464 and 90,185,723
  people at turn 1630, and seed 42's purse holds 20,140,268 at turn 500: lane D's figures (§1, §2). Seeds 3 and 5 widen the
  turn-1630 range from lane D's 80.1M–90.2M to 69.1M–104.6M (69,074,169 and 104,612,260).
- **No twin at the last turn (MEASURED):** a twin issues its orders and steps one turn; from turn 1630 there is no next turn, and
  `Sim.Core` refuses the step ("sim day 2196000 is outside the campaign [0, 2196000)", `EraTable.DtDaysAt`). That ended the first
  M4 run (`m4a-crashed-at-1630/`, `m4b-crashed-at-1630/`; its twins before turn 1630 and its seed-42 and seed-7 traces equal the
  rerun's). The rerun puts the last twin at turn 1629 and keeps the probe at 1630. This is a limit of the probe (lane D's
  `--twin-at` path around lane F's twin), not changed here.
- **Food orderings:** these points read the purse and the predicate within one step and allocate no shortfall, so they do not
  depend on the ordering (DERIVED, as §8.5). *Verification round 1 (CB-R1-10): INFERRED, not DERIVED: this is an argument
  from what the step reads (the purse and the predicate, no shortfall allocated), not exact arithmetic, and it was not isolated: the
  two DR27 orderings are lane F's model and cannot run in `Sim.Core`.*

### 8.3 What "the same at every stage" asks of the economy (DERIVED from measured quantities)

Write c(t) for what one soldier costs the polity per sim-year at turn t (grain-equivalents) and S(t) for the polity's yearly food
surplus over all food goods (the research probe, MEASURED: six seeds, 42, 1, 2, 7, 3 and 5, in this lane's runs M2, M3 and M4;
lane D's four at turn 200). Under the shipped rules a soldier eats exactly what a civilian eats (`sim.json` upkeep 1.0,
DERIVED), so c is the labour he no longer gives: ℓ = 1.99–2.86 grain per removed adult-year
(lane F's labour twins at turn 200, MEASURED; other turns INFERRED similar). The share of the surplus an army of N costs is N × c ÷ S.

- **The two numbers are compatible at every stage only together:** 50,000 soldiers always cost exactly 50 times what 1,000 cost, so
  "1,000 readily" at a share r of the surplus and "50,000 difficult" at a share d are both met at a stage iff the per-soldier cost
  lies between d × S ÷ 50,000 and r × S ÷ 1,000, which needs d ≤ 50 r (DERIVED). Example operationalization (PROPOSAL, not a
  Director value): d = 50 % and r = 1 %, so c(t) = S(t) ÷ 100,000.
- **What the shipped economy gives (c = ℓ ≈ 2.5):**

| Turn | Seeds | Surplus S per year (MEASURED) | People | 50,000 at c = 2.5: share of S | c(t) for 50,000 to cost 50 % of S (DERIVED) |
|---:|---|---:|---:|---:|---:|
| 25 | six (M2) | 3,157–7,058 | 5,141–6,542 | impossible (fewer people) | — |
| 100 | six (M2) | 16,746–30,644 | 18,631–25,842 | impossible (fewer adults: 10,483–14,530) | — |
| 200 | four (lane D) | 255,415–291,684 | 240,135–267,618 | 43–49 % | 2.6–2.9 (≈ today's ℓ) |
| 325 | six (M3) | 2.81M–7.51M | 2.37M–4.08M | 1.7–4.4 % | 28–75 |
| 400 | six (M3) | 4.36M–10.92M | 10.06M–17.96M | 1.1–2.9 % | 44–109 |
| 500 | six (M4) | 5.54M–12.54M | 21.3M–29.6M | 1.0–2.3 % | 55–125 |
| 650 | six (M4) | 7.87M–15.12M | 29.3M–43.7M | 0.83–1.6 % | 79–151 |
| 1000 | six (M4) | 11.84M–26.29M | 46.8M–79.7M | 0.48–1.06 % | 118–263 |
| 1630 | six (M4) | 24.33M–31.35M | 69.1M–104.6M | 0.40–0.51 % | 243–314 |

*Lane CB, 2026-11-11: the table first held lane D's four seeds (42, 1, 2, 7) at every turn; it now holds this lane's six wherever
they were measured; turns 500 and 650 are new rows. Seeds 3 and 5 widen the surplus ranges at turns 25, 100, 325, 400 and 1000 (the
lowest surplus is seed 5's at each of them except turn 100, where it is seed 3's) and the people at every turn they were measured;
the turn-1630 surplus range is unchanged.*

- **Reading of the table.** At turn 200 the shipped economy comes close to both numbers through labour alone: 50,000 soldiers cost
  43–49 % of the surplus at ℓ ≈ 2.5 against the example's 50 % (lane D's 34–56 % at ℓ = 1.99–2.86, §4, is the same measure). From
  turn 325 on, 50,000 soldiers cost a few percent of the surplus or less, so "difficult" needs a recurring per-soldier cost that
  grows about a hundredfold between turns 200 and 1630, in step with the surplus. At turn 25 the same 1,000 soldiers cost 35–79 % of
  the surplus (2,500 ÷ S; 8–15 % at turn 100): readily recruitable (§3), but heavy to keep; this is the economy as it is, not a
  lever's effect. "Readily" at that stage rests on the polity's stock and population, not its surplus (lane F: a 1,000 garrison is
  fed in full in NORMAL harvests at turn 25 under both orderings and costs −7.4 % population after 100 turns).

### 8.4 The levers, stage by stage (PROPOSAL; no data change)

| Lever (existing) | Early (turns 25–100) | Middle (turn 200) | Late (turns 325–1630) | What A-38 / M7.14 would set |
|---|---|---|---|---|
| **L5 the personnel predicate** (adults; one stays) | binds (2,260–14,380 at once) | binds (134,840–150,260) | never binds below millions | nothing: a ceiling, not a cost |
| **L4 recruitment inputs** (one-off materials per formation, `unit-families.json` `recruitment.inputs`) | stone binds in 3 of 4 seeds at turn 25 | does not bind | does not bind up to 240,000 (capped twins: no `MaterialsShort`) | inputs that grow with each identity's equipment (TUNE per identity row; Law 4 holds because the identity's Age and equipment are computed state, not dates) |
| **L1 the support test** (`supportYears` × upkeep, read on grain only) | does not bind | does not bind | does not bind at turns 325 and 400; from turn 500 refuses EVERY recruitment at 29 of 30 measured points (six seeds; the purse holds no grain; §8.2.2) | read the soldiers' actual ration sources (all food goods), lane D's P-D12; otherwise "500–1,000 readily achievable" fails at every late stage |
| **L3 recurring maintenance** (A-38: pay, kit wear, munitions per soldier-year, settled per War Pulse through lane E's path) | small (labour already ≈ 2.5 per soldier-year) | small | the main lever: c(t) of §8.3, about 28–75 at turn 325 rising to about 243–314 at turn 1630 under the example operationalization | per-identity maintenance sized by its equipment class, calibrated on research-driven worlds against §8.3 |
| **L6 food and logistics** (lanes E and F) | governs CAMPAIGNING: a marching 1,000 at turn 25 gets 28–29 % of its march days (lane F, both orderings, §3) | fully supplied in NORMAL and BAD (both orderings) | INFERRED not binding: an army's ration is a small share of the surplus (§8.5) | M7.14 with lane E's carriage model; Q3 decides the order |
| L2 `upkeepGrainPerPersonYear` (DERIVED 1.0) | — | — | — | leave DERIVED; a soldier's field ration above a civilian's is lane E's (+0.3 field, +0.6 march, PROPOSAL) |

**PROPOSAL (labelled):** the calibration path for DA3 is **L3 sized per identity by its equipment**, so that the recurring cost of
a soldier of the polity's current identities grows with what those identities need (bows and clubs early, firearms, powder and
motor transport late), **plus L1 fixed to read the soldiers' real ration sources**, with L4 as an early brake and L6 as the
campaign limit. The calibration battery: research-driven worlds, six seeds, turns 25, 100, 200, 325, 400, 1000 and 1630, measuring
the sustained army each stage's surplus pays for at the shares d and r, with the twins of §8.2 as the recruitment check. A recruitment
throughput limit (formations a settlement can raise per year through the identity's required building, `recruitment.building`) would
be a NEW mechanism (Law 2 allows it; DR26 forbids arbitrary costs), not an existing lever; it is named here only as an option for
A-38's design, not proposed.

### 8.5 The food orderings (DR27; Q3 OPEN)

- **The twins of §8.2 do not depend on the ordering** (DERIVED from what they measure): each judges the shipped predicate within
  one step on the free grain in the purse; no allocation of a shortfall between growth and the military takes place.
  *Verification round 1 (CB-R1-10): INFERRED, not DERIVED (argued from what the twin reads; not isolated, since the two DR27
  orderings are lane F's model and cannot run in `Sim.Core`).*
- **Where the ordering matters** is sustaining and campaigning under shortage: lane F's model (no-order worlds) shows both
  orderings at turns 25 and 200 (§3: the army's ration differs by at most about one point; population growth differs a lot).
- **At the late research stages** (turns 325–1630) a 50,000 army's ration is 50,000 a year against a surplus of 2.8M–31.8M a year
  (0.2–1.8 %; six seeds, §8.3 and §8.2.2), so no shortfall arises from the army and both orderings would feed it in full
  (INFERRED: the orderings differ only when food is short, `reports/H-F.md`). NOT RUN: lane F's comparison on research-driven worlds. Its fixtures come from the
  order-less world, and its labour twins on a research world at turns 325 and 400 would carry the same cubic cost as §8.2.1.
  Deferred to the A-38 calibration with a research-fixture generator.

### 8.6 For the Director

Nothing new needs a decision: DA3 is decided and the calibration numbers are OPEN for A-38 and M7.14 (D-082 §5). Two facts are
reported because they shape that calibration:

1. Under the shipped support test, **nobody can be recruited at 29 of the 30 measured points from turn 500 to 1630** in
   research-driven worlds (six seeds; the exception is seed 42 at turn 500; §8.2.2); DA3's "500–1,000 readily achievable" then fails
   at every late stage until A-38 changes what the test reads.
2. Meeting both numbers at every stage needs a recurring per-soldier cost that grows about a hundredfold from turn 200 to turn
   1630 (§8.3, DERIVED from measured surplus under an example operationalization). The operationalization (the shares d and r) is
   calibration work (D20); it is a PROPOSAL here, not a Director value.

### 8.7 Limitations

- One research driver (cheapest first); six seeds at turns 25, 100, 325, 400, 500, 650, 1000, 1629 and 1630, four (lane D's) at
  turn 200.
- The capped twins bound the full twin from below; the full twin at turns 325 and 400 is not measured (§8.2.1). No twin can run at
  the campaign's last turn (1630); the latest is at 1629 (§8.2.2).
- The late stages are sampled at five turns per seed. Between them the purse can switch on and off (lane D's denser seed-42
  sampling, §2 finding 2: grain at turn 500 only, among turns 425–650); the other seeds were not sampled more densely.
- c(t) of §8.3 uses the food surplus as the measure of what a polity can spare; a polity's other goods (and money, where coinage
  exists) are not in it. The labour cost ℓ was measured at turn 200 only (lane F).
- The table's thresholds d and r are an example; another operationalization moves c(t) proportionally.
