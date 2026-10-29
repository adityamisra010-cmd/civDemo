# CR-026: base Research Points and base Civics Points must be equal (Director direction) vs Law 3 and ADR-043

**Status: RESOLVED (2026-10-27) by the Director's binding rule D-065 R6:** "Every Research Point (RP) and Civics Point
(CP) calculation is per turn and based on average population." "This is a binding model requirement." Option 1 below is
adopted, implemented by [ADR-054](adr-054-per-turn-civics-points-and-average-population.md). Raised 2026-10-27 from the
Director's direction recorded as D-065 R5. The text below is the record as raised; the resolution is appended at the end.

## The direction

> "do ensure that the base production rate (like without benefits of universities, buildings, buffs, penalties,
> espionage) of knowledge and civics points will be the same (since theyre population dependent)"

## 1. Frozen items in conflict

1. **CLAUDE.md Law 3** (dt-correctness): every rate is per sim-year, integrated with `dtYears`.
2. **ADR-030** (RATIFIED):
   - RP are per strategic turn, a scoped exception to Law 3;
   - §2 limits the exception to RP;
   - §3: "A second per-turn quantity needs its own ADR."
3. **ADR-043 §3**: CP are per sim-year × `dtYears`, chosen so that "no second exception is created". Its coefficients are
   marked PROPOSED and tunable; the per-year form is its rationale.

The direction asks for equal base rates. Equal **per turn** cannot hold while RP is per turn and CP is per year, whatever
the coefficients, unless one of these items changes.

## 2. Evidence (measured on head `0bf7626`)

- **Shipped tuning** (`research.json`):
  - `rpPerTurn` = 0.08 × P^0.699 per turn;
  - `civicsPoints.coefficientPerYear` = 0.008, exponent 0.699, × `dtYears`.
- **The same population input** (`ResearchQuery.Population`, ADR-043 §3).
- **The same city-state pace factor** (`cityStatePaceFraction`, applied to both).
- **The only difference is the unit.**

Base rates at P = 10,000 by the era-pacing bands (`era-pacing.json`):

| `dtYears` per turn | Base RP per turn | Base CP per turn | CP ÷ RP |
|---|---|---|---|
| 10 (founding band) | 50.0 | 50.0 | 1.00 |
| 5 | 50.0 | 25.0 | 0.50 |
| 3 | 50.0 | 15.0 | 0.30 |
| 2 | 50.0 | 10.0 | 0.20 |
| 1 | 50.0 | 5.0 | 0.10 |
| 0.5 | 50.0 | 2.5 | 0.05 |

ADR-043 §3 states this consequence itself: "in shorter late-era turns CP accrues proportionally less per turn than RP".
The per-(tree, Age) `pacingScale` acts on **costs**, not on production, so it does not make the base rates equal. It
partly offsets the gap in time-to-complete, which is a separate quantity.

## 3. Options

1. **CP per turn, with RP's function** (recommended).
   - Base CP per turn = pace × 0.08 × P^0.699, not multiplied by `dtYears`.
   - A second scoped per-turn exception, in a new ADR as ADR-030 §3 requires, limited to CP generation, allocation and
     progress.
   - Law 3's text is unchanged.
   - Base RP = base CP at every turn length, and a test asserts it at several `dtYears` values (the ADR-030 §5 pattern).
2. **RP per year, with CP's function.** Reverses ratified ADR-030 and re-times all research. Rejected as far larger.
3. **A dt-dependent CP coefficient** (c × 10 / `dtYears`). A per-turn rate in disguise; it hides the exception that
   option 1 states openly. Rejected.

## 4. Blast radius (option 1)

| Area | Change |
|---|---|
| Code | `ResearchQuery.CivicsPointsMilli` (one site, citing the new ADR) |
| Content | `research.json`: the civics-points unit and coefficient (tuning, always allowed once the form is ruled) |
| Calibration | All nine civics `pacingScale` values must be recalibrated by the D-063 R6 procedure (four seeds; the ±25-turn pincer). CP per turn rises by 10 ÷ `dtYears` in every band shorter than 10 years, so without recalibration civics would run far ahead. TC-1 is unaffected: tuning and the pacing scale are outside both closure hashes (`TreeClosureBaselineTests`). |
| Tests and goldens | A parity test (base RP = base CP at equal population across `dtYears`); golden re-pins with a switch-off attribution twin returning the old hashes |
| Records | ADR-043 §3 is superseded in part by the new ADR (append-only); ADR-030 is untouched |
| UI | None in kind: the civics panel already shows CP per turn |

**Not affected:** the benefit terms the direction excludes are untouched in every option. These are universities,
buildings, the realized research effects (`research.researchPointsCoefficient`, `research.civicsPointsCoefficient`),
penalties, and espionage, which is M10.

## 5. Recommendation

**Option 1.** The new ADR and the calibration go into a single packet, adopted only with the D-063 R6 battery green.
The base functions are made identical by construction: one shared function, read by both lanes. They are not two
coefficients kept equal by hand.

## 6. Resolution (appended 2026-10-27; §1–§5 above are unchanged)

- **Ruling.** D-065 R6 (binding): RP and CP are calculated per turn from the average population. It selects option 1 and
  adds the "average population" input, which nothing in the repository defined.
- **Implemented by** [ADR-054](adr-054-per-turn-civics-points-and-average-population.md):
  - one shared base function, `ResearchQuery.BaseKnowledgePointsPerTurn` = `rpPerTurn.coefficient` × P^`rpPerTurn.exponent`,
    read by both lanes (base RP = base CP by construction, at every `dtYears`);
  - CP per turn in `ResearchQuery.CivicsPointsMilli` (never × `dtYears`), behind `tuning.civicsPoints.perTurn` (shipped on;
    absent = the ADR-043 §3 form, the attribution twin);
  - the average population, `ResearchQuery.KnowledgePopulation`, behind `tuning.populationAverageTurns` (shipped 2; schema
    v38 `ResearchPopulations`).
- **Records.** ADR-043 §3 is superseded in part (dated note appended to ADR-043). ADR-030 is untouched; Law 3's text is
  unchanged.
- **Calibration.** The civics `pacingScale` was recalibrated by the D-063 R6 procedure for the shipped content and the
  research-economy candidate: `docs/m6-plan/knowledge-redesign/research-cost-pass/per-turn-cp-recalibration.md`.
