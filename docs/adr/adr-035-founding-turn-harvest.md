# ADR-035 — THE FOUNDING TURN HARVESTS: an absent catchment row is an unmeasured land side, not a zero one

**Status: PROPOSED — awaits the Director's acceptance ruling.** Written 2026-10-07 by the M5 polish stream SIM
(packet P1, branch `m5p-sim`), from the population audit's P-F1 as corrected by its skeptic review
(`scratchpad/m5p/audit-pop.md`, `audit-pop-skeptic.md` §2.4). Closes the **ADR-025 §2.4a deviation** and the
queue lines T4.21-2 (turn-2 vacancy refusal) and T4.21-6 (turn-2 REFUGEES REFUSED burst).

**Touches:** `Sim.Core/Systems/Production/ProductionSystem.cs` (`Farm`, the land side of the Leontief
minimum) and the type comment of `Sim.Core/State/FoodHeadroom.cs`. No schema change, no kernel change, no
new order kind, no content change. Goldens move (§4).

---

## 1. Context

`ProductionSystem.Farm` reads the settlement's arable land from `prev.CatchmentSummaries`. Catchment is
pipeline entry 1 and writes into NEXT, so on a settlement's first stepped turn — turn 1 of a founded world,
and the first turn of every colony — PREV holds **no** catchment row. Until this ADR, Farm read that absence
as `arable = 0`, so the land side of `min(land, labour)` was 0 and the founding-turn staple harvest was 0
everywhere (the "T4.18 warm-up artefact").

The consequences were measured three times (T4.21-2, T4.21-4, the M5 polish population audit):

- turn 2 reads a zero food influx, so `FoodHeadroom.Limit` gives `N_lim = 0`: the vacancy bound refuses every
  gap flow world-wide (first migration turn 3, not 2) and the growth cap holds turn-2 births to replacement —
  the dominant part of the turn-2 world population dip;
- a colony's first turn harvests nothing, so a fresh colony can fall into Severe food state on its second turn
  (single-settlement starts: seed 2 colony 89 → 31, seed 5 colony 154 → 42);
- the first food figure the player sees is a deficit that no mechanism produced.

T4.21-4's RULE 2 added catchment **row absence** to `FoodHeadroom`'s null arm, keyed on row absence only,
but could not close the turn-2 refusal: the refusal came from the turn-1 harvest being zero, which the rule
forbade keying on. ADR-025 §2.4a recorded that as a DEVIATION awaiting the Director.

## 2. Decision

When PREV holds **no** `CatchmentSummaryRow` for the settlement, Farm's land side is **unmeasured**, not
zero:

```
landSide = haveCatchmentRow ? arable × yieldPerKm2
                            : (yieldPerKm2 > 0 ? +∞ : 0)
```

- With a positive yield the land side does not bind: the harvest is labour-limited for that one turn.
- With a zero yield (farming disabled) the land side stays 0 — never `∞ × 0 = NaN`, never a phantom
  labour-limited harvest (the skeptic's specification hole in the audit's literal wording).
- **Keyed on row ABSENCE only.** A present row with arable 0 stays 0: the abandoned settlement's genuine zero
  influx keeps `N_lim = 0` (the abandonment semantics and `D_Cap_NoGrowthOnAGranary` depend on it).
- This mirrors `FoodHeadroom.Limit`'s existing null arm (row absence ⇒ the influx is unmeasured). The two
  arms now agree that row absence is a missing measurement.
- The window is exactly one turn: `CatchmentSystem.IsStale`'s summary-count check gives the settlement its row
  on the next turn.

Why not seed the catchment at founding instead: ColonizationSystem may not write CatchmentSystem's tables
(law 6; pinned by `ColonizationTests.CatchmentGoesStaleThroughTheEXISTINGCountMechanism_NotANewPath`), and a
founding-time catchment would duplicate the catchment computation in worldgen.

## 3. What the one-turn labour-only semantics means

In a world where land would bind on the founding turn, that turn harvests more than land allows. No shipped
world is land-bound at founding or at a colony's first turn: the skeptic measured land/labour = 27–103× at turn
1 (canonical), 11–42× (dev), and 65–3,787× on colonies' first turns, with zero land-bound Farm calls in more
than 4,000 settlement-turns. The land-capped rig
(`HarvestWeatherTests.Weather_InALandCappedWorld_ScalesRealisedHarvestExactlyOnce_NeverTheCap`, labour ×10⁶) is
the one place it shows. Its bind check now skips the row-absent turn and asserts that turn is labour-limited.

## 4. Evidence

MEASURED by the agent writing this record (Release, Linux, `sim run --founded`), on `5ed1370` (before) and on
the P-F1 commit (after). The full before → after set across all three founding changes of this ADR is in §9.

| reading | before (`5ed1370`) | after P-F1 |
|---|---|---|
| seed 42 world population, turns 0–10 | 5143 5245 **5148** 5153 5208 5234 5272 5318 5342 5389 5445 | 5143 5245 **5189** 5212 5243 5284 5324 5366 5404 5458 5510 |
| turn-2 world change, seeds 1–8 | −82 −70 −33 −39 −34 −44 −55 −43 | −30 −3 −4 +38 +35 +79 +64 +13 |
| world-decline turns in 300, seeds 42 / 1 / 2 / 7 | 1 / 2 / 1 / 1 (turn 2; seed 1 also 18) | 1 / 1 / 1 / 0 (turn 2 only) |
| fed growth T80–240, %/yr, seeds 42 / 1 / 2 / 7 | 0.0735 / 0.0726 / 0.0726 / 0.0728 | 0.0735 / 0.0726 / 0.0724 / 0.0730 |
| population at turn 300, seeds 42 / 1 / 2 / 7 | 38,351 / 40,406 / 41,151 / 39,543 | 38,818 / 40,688 / 41,739 / 40,609 |
| colony, `--settlements 1` seed 2 (founded T53) | 69 → 89 → **31 (Severe)** | 69 → 89 → 103 (Normal) |
| colony, `--settlements 1` seed 5 (founded T96) | 124 → 154 → **42 (Severe)** | 135 → 168 → 208 (Normal) |
| first migration turn, founded / driven seed 42 | 3 / 3 | **2 / 2** (247 movers) |

P-F1 alone does NOT remove the turn-2 dip on the played seed (−97 → −56 on seed 42; seeds 1, 2, 3 of 1–8
still fall on turn 2): the remaining dip is the founding-composition noise and the D-004 warm-up, which §6–§7
address. The fed-growth band (0.05–0.1 %/yr) is unchanged.

## 5. Tests

- `PopulationTests.UnfedWorld_DeclinesToFloor_NeverNegative_FoodHitsZeroExactly`: the yield-0 arm — every turn,
  including the row-absent founding turn, harvests exactly 0 grain.
- `HarvestWeatherTests…LandCappedWorld…`: bind check re-aimed (row-absent turn labour-limited, land binds from
  turn 2).
- `WorldReconciliationTests.Founded/Driven…`: migration on turn 2 (RULE 2's acceptance criterion, previously
  unmet), harvest on turn 1.
- `SubstitutionCreditRegressionTests` (the e6b7e42 ledger-crash regression, directive §20): the 900-turn forager
  world no longer reaches the crash state (no dev seed 1–7 at founding stores 0 / 300 / 1500 carries a credit
  in 900 turns, measured), so it is re-rigged at the consumption level, where it reaches the state directly
  and is RED against the pre-e6b7e42 code (`ArgumentOutOfRangeException: Ledger amounts are never negative`).

## 5a. ADR-025 §2.4a

The deviation recorded there is CLOSED by this ADR: the first migration turn returns to 2 on the founded and
driven seed-42 worlds. A dated pointer is added to ADR-025; its historical text is not edited.

## 6. P-F0 — a new bucket row's death accumulator starts at its stationary mean

**Context.** Founding (`WorldFounding`) and frontier founding (`ColonizationSystem`) created every bucket row
with `deathRemainder = 0.0`. D-004's integer reconciliation floors `exact + remainder`, so a row's FIRST
reconciliation was a pure floor: about 8 people per settlement who died in the exact micro-state stayed alive.
They sit mostly in the small, high-mortality elder rows and die on turn 2. The skeptic measured this "D-004
warm-up" at −24.0 of seed 42's −93.8 exact turn-2 change, and showed it was the death remainder alone (birth-
and aging-only arms do not move turn 2).

**Decision.** `WorldFounding.FoundingDeathRemainder = 0.5` seeds the death accumulator of every new bucket row,
at turn-zero founding and at frontier founding (ColonizationSystem uses the same constant, so the two cannot
drift). The remainder of a floored flow is spread over [0, 1) in its stationary state, mean ½; seeding it there
makes the first reconciliation ROUND, deterministically, as later ones do on average. D-004 fixes the
convention (floor with a carried remainder), not the initial value. Birth, aging and starvation accumulators
stay 0.0 (no measured warm-up).

**Tests.** `FoundingRemainderTests`: every founded row carries 0.5 (and only the death accumulator); a colony's
new rows carry 0.5; on a deaths-only rig the first reconciliation rounds exact deaths with the seed and floors
them without it.

**Measured** (on the P-F0 commit, P-F1 + P-F0):

| reading | P-F1 | P-F1 + P-F0 |
|---|---|---|
| seed 42 world population, turns 0–10 | 5143 5245 5189 5212 5243 5284 5324 5366 5404 5458 5510 | 5143 **5141 5125** 5143 5156 5199 5243 5293 5324 5367 5406 |
| turn-2 world change, seeds 1–8 | −30 −3 −4 +38 +35 +79 +64 +13 | +11 +18 +36 +67 +63 +99 +90 +36 |
| world-decline turns in 300, seeds 42 / 1 / 2 / 7 | 1 / 1 / 1 / 0 | 2 (turns 1, 2) / 0 / 0 / 0 |
| fed growth T80–240, %/yr, seeds 42 / 1 / 2 / 7 | 0.0735 / 0.0726 / 0.0724 / 0.0730 | 0.0733 / 0.0728 / 0.0724 / 0.0729 |

The turn-1 integer gain falls from +102 to −2 on seed 42: the ~100 phantom survivors of the floor are gone, and
what remains on seed 42 is the composition noise of §7 (P-F0 alone unmasks it, as the skeptic predicted: seed
42's turn-1 and turn-2 declines are the per-cohort jitter's, removed by P-F2).
