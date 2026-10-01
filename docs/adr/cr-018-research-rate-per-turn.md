# CR-018 — "RP per turn" (D-045 §2) against law 3 (rates are per sim-year)

**Status: OPEN — escalated to the Director.** The implementation ships a law-3-compliant reading (§3, option 1) as
PROVISIONAL CALIBRATION, so nothing is blocked. The reading is a single data value
(`research.json` `tuning.rpReferenceTurnYears`), so the Director's ruling changes data, not code.

Raised while implementing D-045 on `research-progression-foundation`, under CLAUDE.md's STOP rule: *"If
implementation reveals a genuine conflict between frozen items, STOP and write `docs/adr/cr-NNN.md`."* Every
number below was measured by the agent writing this CR on that tree.

## 1. Frozen items in conflict

| # | Item | Where |
|---|---|---|
| A | **D-045 §2 (Director ruling, 2026-10-01):** "Population 100 → 2 RP / turn; Population 1,000 → 10 RP / turn" and "RP(P) = 2 * (P / 100)^0.69897" | `docs/d045-research-calibration-rulings.md` §2 |
| B | **Law 3 (frozen):** "every rate is per-sim-year; integrate with `dtYears`. Never hardcode per-turn amounts." | `CLAUDE.md`; `m0-kernel-spec.md` |
| C | **The shipped era table:** a turn is 10, 5, 3, 2, 1 or 0.5 sim-years, depending on the band. | `Sim.Data/content/era-pacing.json` |

Under C, a per-turn amount and a per-year amount cannot both hold in every era. A literal "RP per turn" that ignores dt
is exactly what B forbids. A per-year reading makes the anchors true in only one turn length.

## 2. Evidence (measured, seed-42 canonical world, polity 1)

| turn | year | dt | population | RP(P) | credited this turn (implemented reading) |
|---|---|---|---|---|---|
| 1 | −3990 | 10 | 5,245 | 31.85 | 31.85 |
| 601 | 255 | 5 | 126,289 | 294.3 | 147.2 |
| 1201 | 1851 | 1 | 419,951 | 681.6 | 68.2 |
| 1601 | 2085 | 0.5 | 500,573 | 770.6 | 38.5 |

Under the literal per-turn reading, the last row would credit 770.6 RP per turn, 20 times the implemented 38.5. Research
per sim-year would then be 40 times higher at dt 0.5 than at dt 10 for the same population.

## 3. Options

1. **(Implemented.) A reference turn length.** RP(P) is the yield of one turn of `rpReferenceTurnYears` = 10 sim-years
   (the campaign-start Neolithic dt), so the anchors hold literally when the game begins. A step credits
   RP(P) × dtYears / 10. Law 3 holds. Late-era turns yield proportionally less per turn, and costs are calibrated
   against that (`docs/research-calibration-report.md`).
2. **A different reference length** (for example 1 sim-year). This is the same rule with another constant, and needs a
   data edit and a re-calibration.
3. **Per turn, regardless of dt.** This needs a ruled exception to law 3 for research. Pacing in turns then becomes
   independent of the era table, and the final-Age costs must be scaled up about 20×.

## 4. Blast radius

- **Options 1 and 2:** one tuning value, plus regenerating `research.json` and the calibration report (the Age units
  are re-derived automatically).
- **Option 3:** `ResearchQuery.ResearchPerYear` and the system's throughput line, plus an amendment to law 3.
- **Goldens:** any option moves only research rows. The research-strip attribution controls prove this.

## 5. Recommendation

Rule **option 1** or **option 2**. Law 3 is what keeps the simulation dt-correct across the era table. The Director's
anchors were stated for small populations, which exist only at the campaign start, where option 1 makes them exactly
true.
