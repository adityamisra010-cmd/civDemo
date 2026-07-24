# ADR-012 — Destination viability gates migration

**Status: accepted** (T2.13, director packet — M2 exit held on the defect this fixes).

## Context

The M2 exit session exposed a migration inversion on a collapsing world
(director's order log, replayed headlessly and confirmed):

1. **Starvation magnetism.** Attractiveness is per-capita
   (`A = (foodWeight·food + landWeight·farmland) / max(pop, 1)`), so a
   settlement emptied by famine — zero food, full catchment — read as the
   world's STRONGEST magnet: few mouths per unit land. The gap channel pulled
   migrants toward it from everywhere, and famine flight (destination-blind,
   damping-weighted only) funneled refugees from the rest of the starving
   cluster INTO it: 1,520 arrivals against 884 same-turn deaths in one turn.
2. **The resurrection cycle.** When the last inhabitant died, demand hit zero
   and the deficit signal RESET to 0.00 — the food-less ruin then read as a
   deficit-free, astronomically attractive destination. A colonist wave
   arrived within a turn, bred one turn on the stale Prev-deficit-0 signal,
   starved, died — repeating every ~9 turns indefinitely. The endless 40–70%
   per-turn migration surges in the director's chronicle were this
   circulation: dying hamlets refilled each other faster than they could
   finish dying.

The famine DEMOGRAPHIC response was verified correct throughout (fertility
suppression zeroes births at deficit ≥ 1/3-slope; starvation mortality
dominates); only migration's destination choice was inverted.

## Decision — the mechanism

Every pairwise migration flow (BOTH channels: gap-driven and famine flight)
is multiplied by the DESTINATION's viability, computed from Prev:

```
viability(j) = (store_j > 0  OR  lastHarvest_j > 0)
                 ? max(0, 1 − DestinationDeficitRepulsion × deficit_j)
                 : 0
```

- **The deficit gate** — migrants know whether a destination can feed them; a
  settlement in famine repels in proportion to its hunger, and at deficit 1.0
  receives exactly zero migrants. `DestinationDeficitRepulsion` is TUNE,
  validated ≥ 1 (below 1 a fully starving destination would still receive
  migrants — the defect this parameter exists to kill).
- **The absolute food gate** — a destination with no store AND no harvest
  feeds nobody, whatever its deficit signal says (an empty ruin's deficit
  reads 0.00 because nobody demands anything). An empty granary on unfarmed
  land repels regardless of how empty the land is — the director's candidate
  fix (1), taken as a hard gate because the graded response already exists in
  the deficit gate the moment anyone lives there.

## D-021 Exit-valve preservation

"Flee a starving settlement" and "walk into a starving settlement" are
different claims. The Exit valve is the FIRST: flight desire remains
source-driven (`FamineFlightFactor × deficit_source`), uncapped by the gap
mechanism, exactly as D-021 ratified. Viability only redistributes WHERE the
fleeing go — toward destinations that can feed them. When every reachable
destination is itself non-viable, flight goes to zero: there is no exodus
without a destination; people die at home instead of circulating between
ruins. The collapse teeth pin both halves (flight still empties a starving
settlement into healthy neighbors; zero arrivals into severe famine).

## Consequences

- No schema change, no new state: viability derives from Prev rows migration
  already read (FoodStores gained a LastHarvestUnits read).
- Healthy worlds are BIT-IDENTICAL: with no deficits, viability ≡ 1.0 and
  every product is unchanged — the founded 300-turn golden did not move, and
  no calibration corridor shifted. The fix is confined to the regime that was
  broken.
- The small-N churn resolved WITHOUT a population floor: the sustained surges
  were refugee circulation, not noise — once no one walks into a famine,
  dying settlements finish dying and the surges end. The director's candidate
  floors/quantization were therefore NOT adopted (they would have been
  modifiers patched over the real mechanism; law 2).
- CollapseStabilityTests (T2.13) is the permanent regression battery for this
  regime: no-starving-gain, no-sustained-surge, dead-stays-dead, and small-N
  no-churn — each verified to FAIL on the pre-fix code.
