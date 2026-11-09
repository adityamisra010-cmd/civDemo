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
| turn 325 | impossible (≤ 26,540) | trivially (2.2–2.5 % of the adults) |
| turns 500–1630 | — | **blocked** at most measured boundaries by the empty grain purse, not by its cost |

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
