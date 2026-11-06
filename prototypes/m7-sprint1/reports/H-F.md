# H-F — Food allocation: both orderings compared numerically, and granary overflow (M7 Phase C, lane F)

**Status: evidence for the Director. No ordering is chosen, adopted or recommended here.** D-079 Q3 is the Director's
open question. This report gives the measured consequences of both orderings and the trade-offs between them.

**Authority.**
- DIR5 §2.11, "Food allocation: evidence required before a decision". It asks for population, growth, military size,
  movement, grain stores, starvation, overflow and wastage, and Ledger consequences, over several seeds and several
  horizons.
- D-079 DR27 and Q3. DR51 places the comparison in Phase C, item 7.
- DIR5 §2.4: economic effects happen per War Pulse.
- DIR5 §2.9: soldier attrition and mortality belong to M7.14.
- D-080 WA4: the realism criterion for a shortfall. WA1: an event at a boundary instant applies to the period that
  starts at that instant.

**Scope.** Everything is in `prototypes/m7-sprint1/`, branch `lane/F` cut from `fc5230c`. No Sim.* file was changed.
No new project was created. Lane T3's shared files (`WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`) were not
touched, nor were `Proto.M7/War` and `Proto.M7/WarEcon` (lane E).

**Revision 3 (2026-11-06).** The lane was restored from its 2026-11-05 bundle (`97ee570`) after container restarts,
merged with dev `444b85e` (T3, lanes N, TR, RAM, P; no conflict, no lane-F file touched by the merge), re-verified on
the merged tree, and completed where the brief still needed it:
- the **trade** fate of the granary overflow, which revision 1 did not cover (brief item 1: "loss flow vs trade vs
  carry"), as two PROPOSALS: `TradeCredit` and `TradeCreditSinceFounding` (§6.6);
- a check that the earlier determinism claim holds with Sim.Core actually compiled in Release (revision 1 never
  compiled it in Release; §10);
- a small bench and a prioritized mutant round on the final tree (§10).
An earlier attempt at this work after 12:06 on 2026-11-05 (revision 2) was lost in the restarts; only its mutant script
survived on disk. Everything in revision 3 was re-implemented and re-measured on 2026-11-06. Sections 3–6.5 and 7 are unchanged: their battery rows are byte-identical
in the revision-3 battery (§2.4).

**Verification round 1 (2026-11-06).** All eleven verifier findings (F-V1 to F-V11) were reproduced on `d1a4342`; none
was refuted. The most serious was F-V1: an undrawn trade credit still relaxes the allocator, so most of TradeCredit's
per-campaign-turn result is an accounting effect, not a buy-back. Each correction is marked in place ("verification
round 1"), and §14 lists the verdicts, tests, mutants and hashes. No model arithmetic changed. The main CSV and both
pinned state hashes are byte-identical to revision 3.

**Verification round 2 (2026-11-06).** Five findings against `b431323` (F2-1 to F2-5). F2-1, F2-3, F2-4 and F2-5 were
reproduced and fixed; F2-2 was reproduced in its headline claim and partly refuted in its mechanism (§15). Two model
changes: the march haul is now billed per sim-year by an explicit march LEG (F2-1; the battery is unchanged by it,
because every war-window period is one leg), and the growth claim is integrated with a remainder accumulator (F2-5; it
moved the battery CSV to `47b67aeb…cf0c` and the figures updated in place). The march figures and the Desert divergence
now carry their dependence on the unratified pulse length (§0 items 6a and 8, §6.1, §7, §8, §11). §15 lists the
verdicts, the sensitivity tables (`H-F-pulse.md`), tests, mutants and hashes.

Abbreviations used throughout:
- **CGM** = civilian requirements → growth → military.
- **CMG** = civilian requirements → military → growth.

The Slice F prototype implemented only CMG; this lane adds CGM.

---

## 0. Evidence summary

Labels: MEASURED = Sim.Core itself, real founded worlds. MODEL = the comparison model of §2, anchored to those worlds.

1. **MEASURED: the existing granary rules destroy 42–48 % of every harvest.** Over the 20 turns after turn 25 and
   after turn 200, across 6 seeds, spoilage plus granary overflow removed 41.9–47.7 % of the harvest. Overflow alone
   removed 14.1–17.1 % (§3a).
2. **MEASURED: Sim.Core's turn order lets the army go unfed while that grain is destroyed.** Consumption (slot 6)
   spoils and caps the store before Military (slot 23) draws upkeep. In a real recruitment twin, 980 soldiers raised
   at turn 25 went unfed in 19 of 20 turns on every seed. The unfed total was 62–92 k units. In those same turns,
   spoilage plus overflow destroyed 387–523 k grain (§3b). This is the overflow that "was not deducted under either
   interpretation" (DIR5).
3. **MODEL: at War Pulse settlement, the ordering barely changes what the army gets.** It decides whether growth
   continues during a shortage.
   - Over 2,592 paired runs (identical inputs except the order), 64 % end in byte-identical final states.
   - With the existing unfed-soldier consequence ("nothing happens"), the share of the army's ration that was
     supplied differs by at most 5.1 points per seed. The share of march days granted differs by at most 1.9 points.
     With the Starve proposal the bounds are 4.8 and 1.9 points.
   - The share of the growth claim served differs by up to 100 points.
   - Civilian starvation is never different between the two orders: 0 of 2,592 pairs (§4, §5). *(Verification round
     1, F-V4:)* civilian-SHORT periods (a nominal deficit) do differ: in 29 of the 2,592 pairs (32 before the round-2
     growth-claim fix, F2-5) CMG leaves civilians short in more pulses than CGM (never fewer), e.g. 41 against 33 of 52 pulses (seed 7, WAR-1000, BAD-1s). The
     shipped 0.20 dead zone (`adaptationAbsorbableShortfall`) absorbs those small shortfalls, which is why starvation
     is equal (§5).
4. **MODEL: why the orders converge.** In the shipped design, growth is headroom: permission for the population to
   grow. It is not an eaten sink. So "growth first" costs the army only a buffer of one period's growth claim kept in
   store. In steady shortage both orders hand the army the same per-pulse surplus (§4.3 trace).
5. **MODEL: the growth bought by the claim is small in these worlds.** Natural increase is about 0.1 %/yr: the net
   reproduction ratio implied by the shipped rates is about 1.03. A 3-year campaign of 1,000 at turn 25 leaves CGM
   +21 people at +20 turns (range 18 to 23) and +129 people at +300 turns (range 114 to 143) above CMG. That is about
   0.3 % (§4.4).
6. **MODEL: the orders do diverge sharply in three places.** Each is a qualitative difference:
   - **(a) Unfed soldiers desert.** Small per-pulse ration gaps under CGM ratchet into permanent losses. *(Qualified
     in verification round 2, F2-2: the Desert proposal has no time constant, so these numbers hold at the 21-day
     settlement period and the 21-day march leg only. Across 7–42-day settlement periods and legs, CMG keeps more
     soldiers than CGM in every configuration measured (mean gap 85–223 soldiers), but the per-seed pattern below does not
     hold: at 42-day settlement CMG also loses the whole army in seed 7 (§15).)* Example: WAR-1000 in a bad-harvest
     regime at turn 25, per seed:
     - CGM dissolves the whole army in 3 of 6 seeds (0 left), where CMG keeps 242–397.
     - In seed 1, CGM keeps 194 and CMG 628.
     - In seeds 2 and 42 the army is fed and both keep 1,000.
     - Means: 366 against 587 soldiers.
     - The army is never larger under CGM, apart from four rounding cases under Starve (§6.1): two of 1 soldier
       (AllocateBeforeCap seeds 5 and 7) and two of 2 soldiers (NoCap seed 2 and TradeCredit seed 42, WAR-1000,
       SEVERE-2s, T0 = 25: 758 against 756 in both; the NoCap case was 757 against 756 before the round-2 F2-5 fix). *(Corrected in verification round 1, F-V10; it said "three 1-soldier
       cases", which missed the TradeCredit case.)*
   - **(b) Settlement per campaign turn** (Sim.Core's present period, 10 years at these turns). Each order becomes
     pathological. CGM supplies 0.1–0.2 % of a standing army's ration, because a 10-year growth claim exceeds the
     1.5-year store. CMG freezes growth: a 1,000-man garrison at turn 25 leaves the population −55 % below peace at
     +100 turns (§6.3).
   - **(c) The growth claim is treated as eaten.** Bounding sensitivity only; no Sim.Core counterpart. CGM supplies
     16.7 % of a 1,000-man campaign's ration against CMG's 100 % (§6.4).
7. **MODEL: the overflow rule matters little per pulse and a great deal per turn.** Per pulse, allocating before the
   cap changes window overflow from 1,130 to 578 and the march days granted by about 2 points. Per turn, it moves
   CGM's garrison ration from 0.1 % to 68–79 % (§6.2, §6.3).
8. **Side findings against the DIR5 §2.10 targets.** These are calibration facts, not decisions.
   - Garrisons of 500–1,000 are fed in full at both epochs under NORMAL and BAD harvests.
   - Continuous campaigning by the same armies is not affordable on the existing haul cost: a 1,000-man army at
     turn 25 is granted 28 % of its march days. *(Qualified in verification round 2, F2-1:)* this holds for a 21-day
     march leg, T2's proposed pulse (one operational move per pulse; D-079 Q1 is open). The haul a year of marching
     costs grows with the leg (a leg of L days carries L days of rations over L days of route): at a 7-day leg the
     same army is granted 75.6–76.7 % of its march days and 500 men 100 %; at a 42-day leg 14.4–14.8 % and
     44.2–45.2 % (§15).
   - The existing personnel rule caps mobilisation at 2,240–3,500 (turn 25) and 10,880–13,680 (turn 200). 50,000 is
     unreachable in these worlds.
   - Mobilising that maximum starves civilians under both orders identically (§7).
9. **MODEL: selling the overflow instead of destroying it (trade fate, PROPOSALS, §6.6).** Grain is the numeraire
   (price pinned at 1.0, D-033) and the shipped TradeArbitrage never moves it, so no existing rule trades it; both
   variants are bounds (no transport cost, no price movement, an unlimited partner).
   - *(Corrected in verification round 1, F-V1.)* While **no claim is short** (the allocator never binds), selling the
     overflow is **food-identical** to the loss flow; only the Ledger reason changes (a conserved Transfer instead of a
     sink). A test proves this on both orders. Revision 3 said "sold and never bought back", which is FALSE: an
     undrawn credit is not neutral once a claim is short. The allocator counts store + credit, and the growth-headroom
     grant is not a physical draw, so growth is granted against the credit while the physical store goes to the army
     with zero imports. This is an **accounting effect of the proposal**, separate from buying grain back (§6.6;
     tables §13; tests `TradeCredit_WhileNoClaimIsShort_…` and `TradeCredit_AnUndrawnCreditStillRelaxesTheAllocator_…`).
   - `TradeCredit` (the credit opens at 0 when the rule switches on at T0) gives small gains per pulse and **keeps the
     ordering differences**: WAR-1000 at turn 25 in NORMAL is granted 32.8 / 33.4 % of its march days (CGM / CMG)
     instead of 28.3 / 29.1 %; 554 of 864 pairs are byte-identical (545 under the loss flow).
   - Per campaign turn it removes both §6.3 pathologies: CGM's garrison ration rises from 0.1 % to 98.8 %, CMG's
     population gap at +100 turns from −54.85 % to −10.08 %. *(Verification round 1, F-V1: this comes **mostly from the
     accounting effect, not from buying back sold grain**.)* Of the ration the GARRISON-1000 draws over the 300-turn
     horizon under TradeCredit, 93.0 % (CGM) and 91.6 % (CMG) comes from the physical store and only 7.0 % / 8.4 % is
     bought back. The CGM loss flow leaves that store to the growth-headroom claim. For PEACE and GARRISON-500 per turn
     nothing at all is bought back (0 imports), yet CGM's ration still rises from 0.1 % to 99.3 % (PEACE) and 99.2 %
     (GARRISON-500) (tables §11, §13).
     Per War Pulse the gain is mostly a real buy-back: WAR-1000 in NORMAL at T0 = 25 draws +1,160 (CGM) / +1,129 (CMG)
     grain over the loss flow, of which 973 / 1,130 is bought back.
   - `TradeCreditSinceFounding` (the credit opens at the grain the REAL world sank as overflow before T0: 287–361 k at
     turn 25, 4.2–5.3 M at turn 200) **feeds every claim in every cell, WAR-LARGE included, and all 864 pairs are
     byte-identical: the ordering question disappears while the credit lasts.** *(Verification round 1, F-V7: this is
     true **by construction**, not an independent finding.)* No claim is short in any of its 1,800 runs, in the window
     or over the horizon (MEASURED from the CSV). When nothing is short, every priority order gives the same state. The
     opening credit covers 57–72 years of the civilians' grain requirement at turn 25 and 257–267 years at turn 200,
     and it never decays. A 3-year war draws, as a ratio of seed means, 1–5 % (WAR-500), 6–9 % (WAR-1000) and 25–26 %
     (WAR-LARGE) of that opening credit at turn 25, and 8 % (WAR-LARGE) at turn 200. Per seed, across regimes and
     orders (trade CSV, verification round 1), the ranges are 0–5.8 %, 4.3–10.9 % and 18.2–34.3 % at turn 25, and
     7.4–7.8 % (WAR-LARGE) at turn 200.
   - Side effect (same mechanism as §4.2, INFERRED): fed from outside, WAR-LARGE leaves +13.7 % (turn 25) and +30.4 %
     (turn 200) more people than peace at +100 turns.
10. **MEASURED: determinism holds across builds.** The revision-3 battery (10,128 runs) gives the same CSV SHA-256
    `05061b8f…d634` in Release, in Debug and with Sim.Core compiled in Release. Removing the two trade rules' rows gives
    exactly the revision-1 CSV `f3c80b53…3336` (§10). *(Verification round 2:)* the growth-claim fix (F2-5) moved the
    CSV to `47b67aeb…cf0c`, again the same in Release, Debug and with Sim.Core in Release; it no longer reduces to the
    revision-1 CSV, because F2-5 changes every run whose growth claim is finite (§15).

---

## 1. What was built

| File | What it is |
|---|---|
| `Proto.M7.Econ/LogisticsPriority.cs` (edited, delimited) | `AllocationOrder { CivilianMilitaryGrowth, CivilianGrowthMilitary }` and an overload `Resolve(…, AllocationOrder)`. The old 4-argument signature delegates with `CivilianMilitaryGrowth`. *(Corrected in verification round 1, F-V9: revision 3 said "is byte-identical; a grid test proves this". That grid test only proves the delegation, because both sides run the same code. The evidence that the CMG path is unchanged is the pre-existing `LogisticsPriorityTests`, unchanged and passing, and, since round 1, a grid digest measured on fc5230c's allocator and pinned in `CivilianMilitaryGrowth_ReproducesThePreLaneFAllocator_…`.)* Under CGM, growth is served first, then moves, with the same (Priority, Id) order, shorten/deny arithmetic and conservation identity. `LogisticsDecision` gains `Order`, `GrowthWasReduced` and `MilitaryWasRestricted` (a defaulted positional parameter, so existing constructors are unchanged). |
| `Proto.M7.Econ/FoodBaseline.cs` (new) | Measures REAL Sim.Core worlds: the per-turn series (cohorts, stock, ledger flows, headroom limit, hash), the labour-response twins and the recruitment overflow twins. Has an exact text round trip. |
| `Proto.M7.Econ/FoodAllocationModel.cs` (new) | The comparison model (§2): `OverflowRule`, `UnfedConsequence`, `GrowthClaim`, `SettlementGranularity`, `FoodModelParams` (every constant read from config), `FoodScenario`, `FoodLedger`, and `FoodPulseModel`. Revision 3 adds `OverflowRule.TradeCredit` and `TradeCreditSinceFounding`, `FoodScenario.OpeningCredit`, `FoodLedger.Exported`/`Imported`/`CreditInitial` and an exact credit check every period (§6.6). |
| `Proto.M7.Econ/FoodComparison.cs` (new) | The battery plan, per-run metrics, the CSV (the determinism fingerprint) and the generated tables. Revision 3: the two trade rules join the primary grid and the per-turn sensitivity (appended last, so earlier rows keep their bytes); table §12 of `H-F-tables.md`. |
| `Proto.M7.Econ/FoodComparisonCli.cs` (new); `Program.cs` (+3 lines) | Commands `food-baseline`, `food-compare`, `food-trace` and (revision 3) `food-bench`. `Program.Main` dispatches to them first; every other argument list is the old calibration command, unchanged. |
| `Proto.M7.Econ.Tests/FoodAllocationComparisonTests.cs` (new) | 34 tests (28 in revision 1, 6 added in revision 3; §10). Verification round 1 adds 17 test cases (§14): 51. |
| `fixtures/lane-f/baseline-seed{1,2,3,5,7,42}.txt` (new) | The six measured real series the battery ran on, 75 KB each. Re-generable with `food-baseline`. |
| `reports/H-F.md` (this file); `reports/H-F-tables.md` (generated) | The report. The tables file is generated verbatim by `food-compare`. |
| `reports/F-economy.md` | Appended dated correction note only (§12). |

---

## 2. Method

### 2.1 Real Sim.Core measurements (MEASURED)

**Six seeds:** 1, 2, 3, 5, 7 and 42.
- The canonical founded world is built and stepped by the shipped `TurnExecutor` with no orders (the FOUNDED golden
  recipe).
- Each run goes to turn 325: 250 turns at dt 10 y and 75 turns at dt 5 y, about 2 minutes per seed in Release.

Per turn, the run records:
- cohort counts, adults, soldiers and grain stock;
- nutritional persons Σ w·n;
- Σ `FoodHeadroom.Limit`;
- the step's ledger flows: Harvest, Eaten, Spoilage, GranaryOverflow, FormationUpkeep, Fodder, Births, Deaths and
  Starvation;
- the world hash.

The grain identity (stock change = harvest − eaten − spoilage − overflow − upkeep − fodder) closes with a residual of 0
on all 1,956 rows of the six fixtures. A test also checks this on a real run.

**Labour twins**, at turns 25 and 200. Real `RecruitFormation` orders remove 960 adults (4 Warbands per settlement),
and then as many adults as the existing predicate accepts. The harvest of step T+2 gives the marginal grain per removed
adult-year:
- Turn 25: 2.19–2.44 per adult-year, an elasticity of 0.98–1.02 against the average product. Production is labour-limited.
- Turn 200: 1.99–2.86 per adult-year, an elasticity of 0.99–1.01.

**Overflow twins.** Real recruitment of 480 and 960 soldiers at turns 25 and 200. Every step's spoilage, overflow,
upkeep drawn and unfed units are recorded for 20 turns.

### 2.2 The comparison model (MODEL)

Both orderings have to run on the same world with only the order switched. Sim.Core fixes its own order: civilians eat
in slot 6, Demographics runs in slot 17, Military in slot 23. Changing that would be a Sim.* change, which D-079 Q5
forbids. So the comparison runs in a reduced-order model of one polity's food economy, anchored to the real series.

**Starting state.** Each run starts from the real world's cohorts, store and soldiers at T0.

**Inputs taken from the real series, per turn:**
- the harvest rate;
- the labour;
- grain eaten per adult-equivalent-year (0.880–0.893);
- the headroom limit.

**What the model adds:**
- **Harvest.** The real rate, adjusted by the measured labour response (piecewise-linear through the two twins),
  times the regime multiplier. Production labour is held at its turn-start value, as Sim.Core's Production reads PREV,
  and moved only by army transfers. The integration is cumulative-floor, so a constant-rate segment yields the same
  grain however it is cut.
- **Claims per period:**
  - civilian requirement R = Σ w·n × grain need × h;
  - growth claim G = vacancy × grain need × h, where vacancy = limit × (model harvest ÷ real harvest) − Σ w·n
    (Slice F's definition, "Σ FoodHeadroom.Vacancy × dt"), in whole units with a remainder accumulator (verification
    round 2, F2-5; it was rounded up per period);
  - military: the ration is soldiers × `upkeepGrainPerPersonYear` × h, with priority 0;
  - military, when campaigning: a continuous march, resupplied every LEG. One leg is billed by the existing
    `MovementFood.FromRouteCost`, haul only (the rations are the ration claim), with priority 1; a period of len days
    claims len ÷ leg of that bill, with a remainder accumulator, so a year's haul does not depend on the settlement
    period (verification round 2, F2-1). The leg defaults to T2's proposed pulse length (21 d at T0 = 25, 14 d at
    T0 = 200; T2 defines a pulse as one operational move). Before round 2 the leg WAS the settlement period, and the
    battery's numbers are unchanged by the fix because every war-window period is one full leg. The yearly haul still
    grows almost linearly with the leg, a physical parameter that is not ratified (§15).
- **Allocation:** `LogisticsPriority.Resolve(…, order)`. The order is the only difference between the two runs of a
  pair.
- **Store:** spoilage 1 − e^(−0.08·h), then capacity 1.5 × annual civilian grain demand, in the order that
  `OverflowRule` names.
- **Demography.** The DemographicsSystem arithmetic on one aggregate cohort vector:
  - fertility;
  - base mortality and starvation mortality;
  - the effective-deficit dead zone (0.20);
  - fertility suppression (slope 3);
  - the rebound bank (0.5 at 0.08/yr);
  - the headroom cap with k = ln2/10;
  - fractional aging;
  - integer reconciliation in the kernel's pinned order, with remainder accumulators.

  It runs on the kernel's own grid of 180-day micro-steps (`MicroStepYears`, ADR-011) regardless of the allocation
  period. The period's grants and deficits are prorated onto those blocks.

**The growth grant caps the headroom the kernel may fill:** H_eff = min(vacancy, G_granted ÷ (grain need × h)).

**Ledger.** Every grain and person change goes through Flow/Transfer helpers that post to a `FoodLedger`. Grain,
civilians and soldiers are checked exactly after every period: the run throws on any mismatch.

**Why the 180-day grid matters (MEASURED on the model).** A first version integrated demography at the 21-day pulse.
It diverged from Sim.Core: +9 % population at +20 turns and +230 % at +300.
- Natural growth here is close to zero (net reproduction ratio about 1.03), so the integration step shifts it. This
  cause is INFERRED: the fix moved both the integration step and the headroom recomputation onto the 180-day grid, and
  the error disappeared (§2.3).
- The shipped kernel fixes MicroStepYears = 0.5 as "the integration scale, not TUNE" (ADR-011).
- The model therefore uses that grid.

**Not modelled.** These are stated, not hidden:
- per-settlement stores, relief and caps (aggregated per polity);
- classes and cultures;
- knowledge effects on demography;
- the health multiplier;
- migration;
- weather beyond the regime overlay;
- non-grain food beyond its baseline share;
- soldier aging and mortality (as shipped: soldiers never die, H7);
- cross-pulse reserves for civilians (the allocator works per period, as prototyped).

### 2.3 Validation against the real Sim.Core (MODEL vs MEASURED)

Setting: PEACE, NORMAL, the existing overflow order, both granularities, all 6 seeds, both epochs.

| quantity | model − real |
|---|---|
| population at +20 turns | −0.17 % to +0.55 % |
| population at +100 turns | −0.25 % to +1.22 % |
| population at +300 turns (T0 = 25) | +0.01 % to +1.77 % |
| Σ harvest over the horizon | −0.07 % to +1.46 % |
| Σ spoilage + overflow over the horizon | within ±1.5 % |
| end stock | +0.6 % to +7.0 % |

- The War Pulse and campaign-turn runs give **identical** populations and harvests in peace, which is exact
  partition-invariance.
- Full table: `H-F-tables.md` §2.
- A model that reproduces the real baseline does not guarantee the model's response to war is right. The response
  uses the measured labour elasticity and shipped laws, but no real-world war run with these orderings exists to
  compare against.

### 2.4 The battery

**Seeds:** 6.
- **Epochs and horizons:**
  - T0 = 25: about 5.2–6.5 k people, with horizons of +20, +100 and +300 turns (to turn 325, 3,000 sim-years);
  - T0 = 200: about 19.5–24.5 k people, with horizons of +20 and +100 turns.
- **Armies** (total soldiers; the founding warband of 20 counts toward each figure):
  - PEACE;
  - GARRISON-500 and GARRISON-1000, standing for the whole horizon;
  - WAR-500 and WAR-1000, mobilised for the window and campaigning every pulse;
  - WAR-LARGE, the most the existing recruitment predicate accepted at T0: 2,260–3,520 at turn 25 and
    10,900–13,700 at turn 200.

  50,000 exceeds every measured world.
- **Window.** 3 sim-years = `harvestVariance.correlationTimeYears`, from day 0 of T0+1.
  - Mobilisation and demobilisation apply at the first pulse starting at or after the instant (WA1). In practice the
    window runs over days 0–1,092.
- **Shortage regimes, applied inside the window:**
  - NORMAL;
  - BAD-1s: harvest × e^(−σ) = 0.7456, where σ = `harvestVariance.sigmaLogYield` 0.2936;
  - SEVERE-2s: × 0.5559;
  - SEVERE-2s+EMPTY: the same, with the store emptied at T0.
- **Variants:**
  - orders: CGM and CMG;
  - overflow rules: CapBeforeMilitary (existing), AllocateBeforeCap (PROPOSAL), NoCap (COUNTERFACTUAL), and, from
    revision 3, TradeCredit and TradeCreditSinceFounding (PROPOSALS, bounding; §6.6);
  - unfed consequences: None (existing), Desert (PROPOSAL) and Starve (PROPOSAL, the existing law reused).
- **Sensitivities:**
  - haul basis MEAN against FULL;
  - growth claim Consumed against Headroom;
  - settlement per campaign turn against per War Pulse.
- **Pulse lengths:** lane T2's PROPOSED values, 21 d (Neolithic band) and 14 d (Bronze/Iron). D-079 Q1 is still open.
  They are also the march leg (§2.2), and every march-days, haul and growth-served figure moves with the leg (§15).

**Totals (revision 1, 2026-11-05):** 6,528 battery runs plus 24 validation runs.
- Release runtime: 219 s.
- CSV SHA-256: `f3c80b532cf07bf4d0c87834aa0c584fdb21a83bfdd9c8b50a14a15a77fb3336`.

**Totals (revision 3, 2026-11-06, the battery `H-F-tables.md` now shows):** 10,128 battery runs plus 24 validation runs
(1,800 TradeCredit and 1,800 TradeCreditSinceFounding runs added).
- Release runtime: 61 s (the host was far less loaded than on 2026-11-05).
- CSV SHA-256: `05061b8f9f560ac4ea49a420c9976be0228aa782bccb2fd3e7c7a088363bd634`.
- **The earlier rows are unchanged, byte for byte:** deleting the rows of the two trade rules from this CSV gives
  SHA-256 `f3c80b53…3336`, the revision-1 hash. So every number in §3–§6.5 and §7 still describes the current code.

**Totals (verification round 2, 2026-11-06, the battery `H-F-tables.md` now shows):** the same 10,128 battery runs plus
24 validation runs; CSV SHA-256 `47b67aebef3224d6cc5e1787bb04f622f50ce8f941fdb438b95500456e97cf0c`, trade CSV
`6946b581870fa06307b19269aa4531556240b482d38cf6a64e986b8c1dda272b`; Release runtime 68 s. Only the growth-claim
remainder (F2-5) moved numbers; the march-leg change (F2-1) moved none (§15). The figures in §0–§9 were updated where
they moved (§15 lists each).

Every number below is a mean over the 6 seeds unless a range is given. Per-seed values are in the CSV, which the
battery regenerates byte-identically. *(Corrected in verification round 1, F-V11: the main CSV has no trade columns.
Exported, imported (also split by purpose), credit and the opening credit of every trade-rule run are now in a
separate trade CSV, written by `food-compare --trade-csv`, with its own SHA-256 `ea6fcc14…fe7d`. The main CSV keeps its
bytes and its hash `05061b8f…d634`.)*

---

## 3. Granary overflow in the real Sim.Core (MEASURED; no model)

### 3a. What the rules destroy (order-less worlds, 20 turns from each epoch)

| seed | turns | Σ harvest | Σ civilians ate | Σ spoilage | Σ overflow | overflow % of harvest | (spoilage + overflow) % |
|---|---|---|---|---|---|---|---|
| 1 | 26–45 | 1,814,728 | 1,027,475 | 514,943 | 267,162 | 14.72 % | 43.10 % |
| 2 | 26–45 | 1,763,071 | 1,018,958 | 489,427 | 249,609 | 14.16 % | 41.92 % |
| 3 | 26–45 | 1,432,000 | 816,241 | 403,172 | 207,644 | 14.50 % | 42.65 % |
| 5 | 26–45 | 1,578,729 | 852,315 | 467,268 | 254,208 | 16.10 % | 45.70 % |
| 7 | 26–45 | 1,740,898 | 979,055 | 497,242 | 259,598 | 14.91 % | 43.47 % |
| 42 | 26–45 | 1,732,097 | 959,918 | 501,466 | 265,621 | 15.34 % | 44.29 % |
| 1–42 | 201–220 | 5.5–7.4 M | 3.1–3.8 M | 1.6–2.3 M | 0.84–1.26 M | 14.05–17.14 % | 41.85–47.72 % |

In the real worlds the store at a turn boundary holds about 1.1–1.3 years of civilian grain demand.
- At dt 10 the turn's residual is spoiled as a lump: 1 − e^(−0.8) = 55 %.
- It is then capped at 1.5 years of demand, so most of a decade's surplus never reaches the next turn.

### 3b. The army goes unfed while that grain is destroyed (real RecruitFormation twins, 20 turns)

| seed | from turn | soldiers | turns with unfed upkeep | Σ unfed units | Σ spoilage + overflow in those turns | civilian starvation |
|---|---|---|---|---|---|---|
| 1 | 25 | 980 | 19 / 20 | 62,661 | 523,032 | 0 |
| 2 | 25 | 980 | 19 / 20 | 66,960 | 478,754 | 0 |
| 3 | 25 | 980 | 19 / 20 | 92,202 | 387,338 | 0 |
| 5 | 25 | 980 | 19 / 20 | 87,040 | 480,178 | 0 |
| 7 | 25 | 980 | 19 / 20 | 70,101 | 510,896 | 0 |
| 42 | 25 | 980 | 19 / 20 | 71,533 | 500,222 | 0 |
| 2 | 25 | 500 | 1 / 20 | 1,313 | 13,935 | 0 |
| all others | 25 | 500 | 0 / 20 | 0 | — | 0 |
| all | 200 | 500 or 980 | 0 / 20 | 0 | — | 0 |

- Each seed destroyed 4.2–8.3 times its unfed amount in the unfed turns. Pipeline order is the cause: slot 6 caps,
  then slot 23 draws.
- Sim.Core sizes the capacity on civilian demand only. Mobilising shrinks the granary by 1.5 × the recruits' grain
  demand.
- MODEL: at War Pulse settlement this effect almost vanishes, because the store refills every pulse and the army eats
  the next pulse's surplus (§6.2). The "not deducted" overflow is therefore mainly an artifact of the 10-year
  settlement period.

---

## 4. The two orders, primary configuration (MODEL)

Primary configuration: per War Pulse; CapBeforeMilitary (existing); unfed consequence None (existing); FULL haul;
growth as headroom.

### 4.1 The 3-year window at T0 = 25

| army | regime | order | ration supplied % | march days granted % | growth claim served % | periods growth cut % | periods military restricted % | civilian-short periods (of 52) | fed soldier-years | min stock | overflow in window | haul eaten |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| GARRISON-1000 | NORMAL | both | 100.0 | — | 100.0 | 0 | 0 | 0 | 3,033 | 4,912 | 2,648 | 0 |
| GARRISON-1000 | SEVERE-2s | CGM / CMG | 90.6 / 90.6 | — | 97.4 / 97.2 | 2.9 / 3.2 | 9.6 / 9.6 | 4.8 / 4.8 | 2,748 | 491 / 491 | 992 | 0 |
| WAR-500 | NORMAL | CGM / CMG | 100 / 100 | 81.4 / 82.4 | **100 / 73.2** | 0 / 27.6 | 27.6 / 26.3 | 0 | 1,516 | 616 / 496 | 503 | 10,672 / 10,804 |
| WAR-500 | BAD-1s | CGM / CMG | 91.3 / 91.5 | 51.0 / 51.8 | **100 / 46.3** | 0 / 53.8 | 53.8 / 53.2 | 0 | 1,384 / 1,387 | 96 / 0 | 407 | 6,682 / 6,788 |
| WAR-1000 | NORMAL | CGM / CMG | 100 / 100 | 28.3 / 29.1 | **100 / 20.6** | 0 / 79.5 | 79.5 / 79.2 | 0 | 3,033 | 157 / 0 | 1,130 | 7,314 / 7,512 |
| WAR-1000 | BAD-1s | CGM / CMG | 53.8 / 54.2 | 18.8 / 19.1 | **87.9 / 17.7** | 14.1 / 82.4 | 82.4 / 82.4 | 5.5 / 6.8 | 1,631 / 1,644 | 58 / 0 | 1,050 | 4,859 / 4,926 |
| WAR-1000 | SEVERE-2s | CGM / CMG | 18.0 / 18.0 | 16.9 / 16.9 | 48.5 / 45.3 | 53.5 / 55.4 | 84.0 / 84.0 | 40.8 / 42.3 | 546 / 547 | 0 | 992 | 4,358 / 4,362 |
| WAR-1000 | SEVERE-2s+EMPTY | both | 0.0 | 0.0 | 33.3 | 66.7 | 100 | 52.0 | 0 | 0 | 0 | 0 |
| WAR-LARGE | NORMAL | both | 4.7 | 3.4 | 84.7 / 84.3 | 15.4 / 15.7 | 97.1 | 48.8 / 49.0 | 389 / 392 | 0 | 3,434 | 2,331 |

At T0 = 200 the economy is 3.5–4 times larger:
- WAR-500 is fully supplied in NORMAL and BAD, and WAR-1000 in NORMAL, with identical states under both orders.
- In BAD, WAR-1000 gets its full ration and 93.5 % (CGM) or 94.1 % (CMG) of its march days; growth served is 100 %
  against 93.3 %.
- Under SEVERE-2s, WAR-1000 gets 72.5 % of its ration and 67.4–67.5 % of its march days under both orders. Growth
  served is 85.8 % (CGM) against 81.3 % (CMG).
- WAR-LARGE (12,563 soldiers) gets 3.8 % of its ration under both orders; civilians are short in half the periods.

### 4.2 Population consequences at T0 = 25

Gap = against PEACE in the same regime and the same order.

| army | regime | order | pop +20 | gap +20 % | pop +100 | gap +100 % | pop +300 | gap +300 % | civilian starvation (cum.) |
|---|---|---|---|---|---|---|---|---|---|
| PEACE | NORMAL | both | 6,961 | 0 | 12,778 | 0 | 43,950 | 0 | 0 |
| GARRISON-1000 | NORMAL | both | 6,897 | −0.93 | 11,839 | −7.41 | 38,315 | −12.92 | 0 |
| WAR-500 | NORMAL | CGM / CMG | 6,967 / 6,962 | +0.09 / +0.02 | 12,790 / 12,780 | +0.10 / +0.02 | 43,989 / 43,956 | +0.09 / +0.01 | 0 |
| WAR-1000 | NORMAL | CGM / CMG | 6,994 / 6,973 | +0.48 / +0.18 | 12,839 / 12,800 | +0.49 / +0.18 | 44,154 / 44,025 | +0.47 / +0.18 | 0 |
| WAR-1000 | BAD-1s | CGM / CMG | 6,990 / 6,973 | +0.44 / +0.18 | 12,833 / 12,800 | +0.44 / +0.18 | 44,133 / 44,024 | +0.43 / +0.17 | 0 |
| WAR-1000 | SEVERE-2s | CGM / CMG | 6,916 / 6,916 | −0.59 / −0.58 | 12,695 / 12,694 | −0.58 | 43,661 / 43,658 | −0.59 | 37 / 37 |
| WAR-LARGE | NORMAL | both | 7,305 | +4.95 | 13,409 | +4.95 | 46,124 | +4.96 | 388 |

**Why some wars leave more people than peace.** This comes from MODEL mechanics that follow the shipped rules, and it
is the same under both orders:
- soldiers never die (H7), so their avoided deaths compound into descendants;
- famine kills dependants first (child ×1.5, elder ×1.3), which raises the labour share;
- production is labour-proportional (elasticity about 1), so a population level shift persists and is never undone.

For the same reason a gap never "recovers" to below 1 % once it exceeds 1 %. The "recovered" column in
`H-F-tables.md` §4b is mostly "never" or "≤1".

### 4.3 Why the orders converge on military supply

The trace below is MODEL output for seed 42, T0 = 25, WAR-1000, BAD-1s (from `food-trace`). Per 21-day pulse:
harvest 277, civilian requirement 210, growth claim G 114–115 (114.1–114.6 before rounding; 115 in every pulse
before the round-2 F2-5 fix), ration 58, march haul 496.

| pulses | CGM | CMG |
|---|---|---|
| 1–18 | The store of 4,830 carries the campaign: ration and march in full, growth in full. **Identical.** | Identical |
| about 19 onwards | Growth keeps its 114–115 reserved. The store stays at about 114–137. Ration 58/58; march 24 → 0 of 496. | Growth gets 0. The store is 1–20. Ration 58/58; march 24 → 0 of 496. |

- The surplus per pulse (about 67) cannot pay the haul under either order.
- The growth claim is permission, not food eaten, so the grain it protects in CGM is the same grain every pulse. CGM
  keeps a one-pulse buffer; CMG hands that buffer to the haul once.
- Per seed, the difference in march days granted for WAR-1000 is 0–1.0 points (NORMAL and BAD; §4.4).

### 4.4 Paired differences, CGM − CMG, per seed

T0 = 25. Mean [min, max] over 6 seeds. Full table: `H-F-tables.md` §9.

| army | regime | Δ ration supplied (pp) | Δ march granted (pp) | Δ growth served (pp) | Δ pop +20 | Δ pop +100 | Δ pop +300 | Δ civilian starvation |
|---|---|---|---|---|---|---|---|---|
| WAR-500 | NORMAL | 0.0 [0, 0] | −1.0 [−1.9, 0] | +26.8 [0, 49.9] | +5 [0, 9] | +10 [0, 21] | +34 [0, 68] | 0 |
| WAR-500 | BAD-1s | −0.2 [−0.9, 0] | −0.8 [−1.3, −0.2] | +53.7 [40.3, 63.4] | +11 [8, 14] | +23 [17, 27] | +79 [58, 99] | 0 |
| WAR-1000 | NORMAL | 0.0 [0, 0] | −0.8 [−1.0, −0.4] | +79.4 [74.6, 84.6] | +21 [18, 23] | +40 [36, 44] | +129 [114, 143] | 0 |
| WAR-1000 | BAD-1s | −0.4 [−1.2, 0] | −0.3 [−0.5, 0] | +70.2 [10.7, 86.5] | +18 [1, 23] | +33 [3, 44] | +109 [10, 143] | 0 |
| WAR-1000 | SEVERE-2s | −0.0 [−0.3, 0] | −0.0 [−0.1, 0] | +3.2 [0, 11.5] | −0 [−1, 0] | +1 [0, 4] | +3 [0, 10] | 0 |
| GARRISON-500 | SEVERE-2s+EMPTY | −1.2 [−5.1, 0] | — | +32.3 [0, 97.3] | +7 [0, 23] | +12 [0, 36] | +43 [0, 131] | 0 |

---

## 5. Where the orders diverge qualitatively

Primary grid: 2,592 pairs, each identical except the order. Computed from the CSV.

| observation | pairs |
|---|---|
| byte-identical final state | 1,649 (64 %) |
| any difference | 943 |
| growth served higher under CGM / under CMG | 438 / **0** |
| ration supplied higher under CMG / under CGM | 190 / 8 (all 8 are WAR-1000 in BAD or SEVERE. Inferred cause: CGM's buffered growth grain feeds a later ration) |
| civilian starvation different | **0** |
| civilian-short periods in the window different *(verification round 1, F-V4)* | **29** (32 before round 2's F2-5 fix): CMG leaves civilians short in more pulses in every one (never fewer); all at T0 = 25 in WAR-500 SEVERE-2s, WAR-1000 BAD-1s and SEVERE-2s, WAR-LARGE NORMAL. Starvation is equal because the 0.20 dead zone absorbs those small nominal shortfalls |
| soldiers at the window's end different | 106 (all with a Desert or Starve consequence; CMG keeps more in every case except three rounding cases under Starve: 1, 1 and 2 soldiers) |
| population +20 higher under CGM / under CMG | 341 / 100 (88 of the 100 are Starve: CGM's unfed soldiers die) |
| sign of (CGM − CMG) population flips between +20 and the last horizon | 14 (WAR-1000 SEVERE-2s at T0 = 25; WAR-500 SEVERE-2s with Desert at T0 = 200; WAR-LARGE with Desert) |

**Cells where the orders never differ.** Here the final state is byte-identical for every seed, overflow rule and
consequence (54 pairs per cell):
- PEACE in NORMAL, BAD and SEVERE, at both epochs.
- GARRISON-500 in NORMAL, BAD and SEVERE, at both epochs.
- GARRISON-1000 in NORMAL and BAD at T0 = 25, and in NORMAL, BAD and SEVERE at T0 = 200.
- WAR-500 in NORMAL and BAD, and WAR-1000 in NORMAL, at T0 = 200.

**Per overflow rule (revision 3; 864 pairs each; computed from the CSV):**

| overflow rule | byte-identical pairs | growth served higher CGM / CMG | ration supplied higher CMG / CGM | civilian starvation differs | soldiers at window end differ |
|---|---|---|---|---|---|
| CapBeforeMilitary (existing) | 545 (63 %) | 146 / 0 | 62 / 0 | 0 | 37 |
| AllocateBeforeCap (PROPOSAL) | 552 (64 %) | 145 / 0 | 61 / 5 | 0 | 35 |
| NoCap (COUNTERFACTUAL) | 552 (64 %) | 147 / 0 | 67 / 3 | 0 | 34 |
| TradeCredit (PROPOSAL, bounding) | 554 (64 %) | 139 / 0 | 67 / 5 | 0 | 35 |
| TradeCreditSinceFounding (PROPOSAL, bounding) | **864 (100 %)** | 0 / 0 | 0 / 0 | 0 | 0 |

The first three rows sum to the 1,649 / 2,592 of the table above.

*(Verification round 1.)* Civilian-short periods differ in 10 / 9 / 10 / 9 / 0 pairs per row (10 / 12 / 10 / 9 / 0 before the round-2 F2-5 fix) (CMG more in every
one; civilian starvation still equal, F-V4). Among the TradeCredit row's 35 army-size differences there is one where CGM
keeps more: seed 42, WAR-1000, SEVERE-2s, Starve, 758 against 756 (F-V10). TradeCreditSinceFounding's 864 identical
pairs hold by construction: no claim is short in any of its runs (F-V7, §6.6).

Every other cell differs in at least one pair. Where civilians are short (WAR-LARGE; SEVERE-2s+EMPTY with an army) the
differences in the window are small, because almost nothing is left beyond the civilian requirement. Example:
WAR-LARGE in NORMAL at T0 = 25 serves 84.7 % (CGM) against 84.3 % (CMG) of the growth claim, and the final states
differ. With Desert, WAR-LARGE also differs in army size, because deserters return to farming.

---

## 6. Sensitivities

### 6.1 The unfed-soldier consequence ("cannot pay": None existing, Desert and Starve PROPOSALS)

T0 = 25, CapBeforeMilitary.

| army | regime | consequence | order | ration % | soldiers start → end | deserted | starved | fed soldier-years | gap +20 % |
|---|---|---|---|---|---|---|---|---|---|
| WAR-1000 | BAD-1s | None | CGM / CMG | 53.8 / 54.2 | 1000 → 1000 | 0 | 0 | 1,631 / 1,644 | +0.44 / +0.18 |
| WAR-1000 | BAD-1s | Desert | CGM / CMG | 94.4 / 98.2 | 1000 → **366 / 587** | 634 / 413 | 0 | **1,466 / 2,005** | +0.21 / −0.08 |
| WAR-1000 | BAD-1s | Starve | CGM / CMG | 55.5 / 55.9 | 1000 → 861 / 863 | 0 | 139 / 137 | 1,632 / 1,644 | −1.88 / −2.10 |
| WAR-500 | BAD-1s | Desert | CGM / CMG | 99.6 / 99.7 | 500 → 420 / 446 | 87 / 54 | 0 | 1,375 / 1,420 | +0.07 / −0.09 |
| WAR-1000 | SEVERE-2s+EMPTY | Desert | both | 0 | 1000 → 0 | 1,000 | 0 | 0 | +0.02 / +0.03 |
| WAR-1000 | SEVERE-2s+EMPTY | Starve | both | 0 | 1000 → 695 | 0 | 305 | 0 | −5.42 |
| GARRISON-1000 | SEVERE-2s+EMPTY | Starve | both | 0 | 1000 → 695 | 0 | 311 / 305 | 0 | −6.44 / −6.35 |

**Desert.** Under Desert the army shrinks to what its supply feeds. Each desertion is permanent, so the small
per-pulse ration gaps of CGM (the growth buffer, §4.3) accumulate into large differences in strength. *(Qualified in
verification round 2, F2-2:)* the rule has no time constant: the army adjusts at once to the strength the period's
grant feeds, so a fully unfed period dissolves it whatever its history, and its outcome depends on the settlement
period. With the march leg held at 21 days, the per-seed strengths below move by up to 308 soldiers between 7- and
42-day settlement, while the per-year Starve law moves by at most 8 (§15). The figures below are the 21-day case.

WAR-1000 BAD at T0 = 25, CapBeforeMilitary, soldiers at the window's end (CGM / CMG):

| seed | 1 | 2 | 3 | 5 | 7 | 42 |
|---|---|---|---|---|---|---|
| soldiers | 194 / 628 | 1,000 / 1,000 | 0 / 255 | 0 / 397 | 0 / 242 | 1,000 / 1,000 |
| fed soldier-years | 1,120 / 2,143 | 3,033 / 3,033 | 489 / 1,120 | 539 / 1,523 | 583 / 1,176 | 3,033 / 3,033 |

At T0 = 200, WAR-LARGE with Desert ends with 1,201–1,320 more soldiers under CMG.

Ration supply rises, because the remaining army is smaller.

**Starve.** Under Starve the ration share decides deaths at the shipped law: 0.12/yr at full deprivation, with a
20 % dead zone. The orders differ by at most a few soldiers.

### 6.2 Granary overflow rule (CapBeforeMilitary existing; AllocateBeforeCap PROPOSAL; NoCap COUNTERFACTUAL)

T0 = 25, consequence None.

| army | regime | rule | order | overflow in window | march granted % | growth served % | Σ overflow over the horizon | Σ spoilage over the horizon |
|---|---|---|---|---|---|---|---|---|
| WAR-1000 | NORMAL | CapBeforeMilitary | CGM / CMG | 1,130 | 28.3 / 29.1 | 100 / 20.6 | 25.36 M / 25.29 M | 4.51 M / 4.49 M |
| WAR-1000 | NORMAL | AllocateBeforeCap | CGM / CMG | 578 | 30.6 / 31.3 | 100 / 23.0 | 25.36 M / 25.29 M | 4.51 M / 4.49 M |
| WAR-1000 | NORMAL | NoCap | CGM / CMG | 0 | 32.7 / 33.4 | 100 / 25.3 | 0 | 29.60 M / 29.51 M |
| PEACE | NORMAL | Cap… / NoCap | both | 7,660 / 0 | — | 100 | 25.25 M / 0 | 4.49 M / 29.48 M |

- **Per pulse, the overflow rule is a second-order effect:** about 2 points of march days, and no change in rations.
- **Without the capacity rule, spoilage replaces overflow.** It destroys 29.5 M instead of 25.3 + 4.5 = 29.7 M: the
  store grows until spoilage equals the surplus, the "12.5 years of surplus" equilibrium in `sim.json`'s own `_doc`.
- **Mobilisation shrinks the granary.** In the seed-42 trace (§4.3) the whole window's overflow (1,105) falls at the
  first pulse, when the capacity basis (civilian demand only) drops as 980 people leave it. Across seeds the mean
  window overflow is 1,130.

### 6.3 Settlement granularity: per campaign turn (Sim.Core's period) against per War Pulse

Standing armies, NORMAL, whole-horizon ration and growth.

| T0 | army | granularity | rule | order | ration supplied % | growth served % | gap +20 % | gap +100 % |
|---|---|---|---|---|---|---|---|---|
| 25 | GARRISON-500 | WarPulse | either | both | 100 | 100 | −0.46 | −3.64 |
| 25 | GARRISON-500 | CampaignTurn | CapBeforeMilitary | CGM / CMG | **0.1 / 100** | 23.8 / 18.8 | −0.46 / −2.78 | −3.64 / −5.99 |
| 25 | GARRISON-1000 | CampaignTurn | CapBeforeMilitary | CGM / CMG | **0.1 / 57.9** | 23.8 / **0.7** | −0.93 / **−17.13** | −7.41 / **−54.85** |
| 25 | GARRISON-1000 | CampaignTurn | AllocateBeforeCap | CGM / CMG | 67.9 / 100 | 95.0 / 91.6 | −0.93 | −7.41 |
| 200 | GARRISON-1000 | CampaignTurn | CapBeforeMilitary | CGM / CMG | 0.2 / 100 | 26.2 / 20.3 | −0.26 | −1.55 |
| 25 | PEACE (the warband of 20) | CampaignTurn | CapBeforeMilitary | CGM / CMG | 0.1 / 100 | 23.7 / 23.6 | 0 | 0 |

**Per turn, the claims are 10 years long while the store holds 1.5 years**, so each order gives one claimant
everything:
- CGM's 10-year growth claim takes the whole capped store, and even the founding warband gets 0.1 % of its ration;
- CMG's 10-year ration takes it whenever the army is large enough, and growth headroom is then zero every turn. A
  1,000-man garrison at turn 25 freezes the population: 5,770 people at both +20 and +100 turns.

**Per War Pulse (DIR5 §2.4) neither pathology appears.**

### 6.4 Growth claim eaten instead of headroom (bounding sensitivity; no Sim.Core counterpart)

T0 = 25, per pulse, CapBeforeMilitary.

| army | regime | consequence | order | ration % | march % | growth served % | soldiers end |
|---|---|---|---|---|---|---|---|
| GARRISON-1000 | NORMAL | None | CGM / CMG | **81.0 / 100** | — | 92.3 / 85.4 | 1000 |
| GARRISON-1000 | NORMAL | Desert | CGM / CMG | 98.2 / 100 | — | 90.8 / 85.4 | **333 / 1000** |
| WAR-500 | NORMAL | None | CGM / CMG | **32.1 / 100** | 31.2 / 63.2 | 79.8 / 31.2 | 500 |
| WAR-1000 | NORMAL | None | CGM / CMG | **16.7 / 100** | 16.0 / 24.3 | 73.1 / 15.4 | 1000 |
| WAR-1000 | NORMAL | Desert | CGM / CMG | 89.6 / 100 | 84.4 / 24.3 | 75.3 / 15.4 | **0 / 1000** |

If the growth claim were a real sink, CGM would starve or dissolve the army in ordinary harvests, and CMG would keep
it. But this reading has no counterpart in Sim.Core: growth eats nothing there. It also changes peace: the "growth
eaten" absorbs 26.8 M grain that is otherwise spoilage or overflow. So it bounds the effect; it does not predict it.

### 6.5 Haul basis (FULL against MEAN load; the choice is still open)

WAR-1000 NORMAL, march days granted:

| basis | CGM | CMG |
|---|---|---|
| FULL | 28.3 % | 29.1 % |
| MEAN | 55.5 % | 56.9 % |

Growth served under MEAN is 100 % (CGM) against 47.9 % (CMG). The basis changes what campaigns can afford. It does
not change the comparison's shape.

### 6.6 The fate of the overflow: lost, carried or traded (revision 3; trade = PROPOSALS, bounding)

**What the existing rules say.** Searched in `docs/` and `Sim.Core`:
- **Lost** is the shipped rule: `ConsumptionSystem` sinks everything above `granaryYearsOfDemand` × annual civilian
  demand as `GranaryOverflow` (a Ledger sink; `docs/food-anomaly-investigation.md`, `docs/explain-queries.md`).
- **Carried** has no shipped form; `NoCap` (§6.2) is the counterfactual: everything is kept and spoilage alone limits
  the store.
- **Traded** has no shipped form either. Grain is the numeraire, its price pinned at 1.0 (D-033), so its price gap is
  structurally zero and `TradeArbitrageSystem` never moves grain ("grain redistribution awaits a mechanism that responds
  to quantities", its own header). DIR5 §2.11 asks for the overflow to be investigated but sets no fate.

So the trade fate is offered as two PROPOSALS, each run on both orders:
- **`TradeCredit`:** the existing timing (civilians eat, spoilage, cap, then the military), but the grain above the
  capacity is sold. It leaves through a conserved Transfer (`Exported`) and earns a credit at the numeraire price. The
  credit buys grain back (`Imported`) at the same price whenever the store cannot cover civilians before they eat, and
  the allocator sees store + credit, so a granted claim is bought back if the store lacks it. The credit opens at 0 at
  T0, when the rule switches on, as every other rule does.
- **`TradeCreditSinceFounding`:** the same, with the credit opening at the grain the REAL world sank as
  `GranaryOverflow` in turns 1..T0 (287–361 k at turn 25; 4.18–5.33 M at turn 200). An upper estimate: it ignores
  anything the polity would have bought back before T0.

Both are bounds: no transport cost, no price movement, an unlimited partner and a credit that never decays.

**While no claim is short, sold = lost, for food.** *(Corrected in verification round 1, F-V1. Revision 3 headed this
"Sold but never bought back = lost" and claimed it held whenever the credit is never drawn. That claim is false.)* In
peace with a surplus, per War Pulse, no claim is ever short, so the allocator never binds and the credit is never drawn.
The run is then identical to the loss flow in population, stock, births, spoilage and growth grants, under both orders,
and the Ledger records a Transfer instead of a sink (test `TradeCredit_WhileNoClaimIsShort_…`, which now also asserts
that nothing is short).

**An undrawn credit still changes the allocation once a claim is short (accounting effect; F-V1).** Both trade rules
hand the allocator store + credit. The growth-headroom grant is permission to grow, not a physical draw (§4.3), so it
can be granted against the credit, with no grain imported. The physical store is then free for the army:
- CGM: growth takes its claim against store + credit, and the ration and the march are drawn from the physical store
  that the loss flow would have left to growth.
- CMG: the army is served first, as before, but growth is then granted against the credit instead of being cut.
Seed-42 trace, PEACE, per campaign turn, CGM (`food-trace … --granularity CampaignTurn --horizon 6`, MEASURED):
`imported` is 0 in every turn, yet turn 1 grants growth 22,647 against the loss flow's 6,685, and the founding ration
is 200/200 from turn 3 against 0/200. The test `TradeCredit_AnUndrawnCreditStillRelaxesTheAllocator_…` pins this on a
synthetic world under both orders (Imported = 0; growth, and under CGM the ration, served more than under the loss
flow). Whether a trade credit may back the growth-headroom grant, or only physical draws, is a detail of this PROPOSAL
that nothing in Sim.Core decides (open question 8, §11). Tables §13 splits each trade cell's army draws into "bought
back" and "from the physical store".

**Per War Pulse, T0 = 25, consequence None, CGM / CMG** (full table: `H-F-tables.md` §12):

| army | regime | fate | ration % | march % | growth served % | civilian-short periods | pop +100 |
|---|---|---|---|---|---|---|---|
| GARRISON-1000 | SEVERE-2s | lost | 90.6 / 90.6 | — | 97.4 / 97.2 | 4.8 / 4.8 | 11,801 / 11,800 |
| | | TradeCredit | 99.3 / 99.3 | — | 100 / 100 | 0.2 / 0.2 | 11,818 |
| | | SinceFounding | 100 / 100 | — | 100 / 100 | 0 / 0 | 11,818 |
| WAR-500 | BAD-1s | lost | 91.3 / 91.5 | 51.0 / 51.8 | 100 / 46.3 | 0 | 12,790 / 12,767 |
| | | TradeCredit | 91.6 / 91.9 | 54.1 / 54.9 | 100 / 49.7 | 0 | 12,790 / 12,768 |
| | | SinceFounding | 100 / 100 | 100 / 100 | 100 / 100 | 0 | 12,790 |
| WAR-1000 | NORMAL | lost | 100 / 100 | 28.3 / 29.1 | 100 / 20.6 | 0 | 12,839 / 12,800 |
| | | TradeCredit | 100 / 100 | 32.8 / 33.4 | 100 / 25.5 | 0 | 12,839 / 12,800 |
| | | SinceFounding | 100 / 100 | 100 / 100 | 100 / 100 | 0 | 12,839 |
| WAR-1000 | BAD-1s | lost | 53.8 / 54.2 | 18.8 / 19.1 | 87.9 / 17.7 | 5.5 / 6.8 | 12,833 / 12,800 |
| | | TradeCredit | 56.0 / 56.2 | 22.7 / 22.9 | 88.5 / 21.6 | 5.2 / 6.5 | 12,833 / 12,800 |
| | | SinceFounding | 100 / 100 | 100 / 100 | 100 / 100 | 0 | 12,839 |
| WAR-1000 | SEVERE-2s | lost | 18.0 / 18.0 | 16.9 / 16.9 | 48.5 / 45.3 | 40.8 / 42.3 | 12,695 / 12,694 |
| | | TradeCredit | 21.3 / 21.3 | 20.1 / 20.2 | 50.8 / 47.7 | 39.2 / 40.7 | 12,709 / 12,707 |
| | | SinceFounding | 100 / 100 | 100 / 100 | 100 / 100 | 0 | 12,818 |
| WAR-LARGE | NORMAL | lost | 4.7 / 4.7 | 3.4 / 3.4 | 84.7 / 84.3 | 48.8 / 49.0 | 13,409 |
| | | TradeCredit | 8.7 / 8.8 | 7.5 / 7.5 | 85.8 / 84.9 | 47.0 / 47.2 | 13,436 |
| | | SinceFounding | 100 / 100 | 100 / 100 | 100 / 100 | 0 | 14,522 |

With **Desert** (WAR-1000, BAD-1s, T0 = 25), soldiers at the window's end per seed (1, 2, 3, 5, 7, 42), CGM / CMG:
- lost: 194, 1000, 0, 0, 0, 1000 (mean 366) / 628, 1000, 255, 397, 242, 1000 (mean 587);
- TradeCredit: 207, 1000, 0, 0, 0, 1000 (mean 368) / 656, 1000, 432, 84, 242, 1000 (mean 569);
- SinceFounding: 1,000 in every seed under both orders.

At T0 = 200, WAR-1000 in SEVERE-2s: lost 72.5 / 72.5 % ration, TradeCredit 74.0 / 74.0 %, SinceFounding 100 / 100 %.

**Per campaign turn** (GARRISON-1000, T0 = 25, NORMAL; `H-F-tables.md` §11):

| fate / timing | ration % CGM / CMG | growth served % CGM / CMG | gap +100 % CGM / CMG |
|---|---|---|---|
| lost, CapBeforeMilitary (existing) | 0.1 / 57.9 | 23.8 / 0.7 | −7.41 / −54.85 |
| lost, AllocateBeforeCap | 67.9 / 100 | 95.0 / 91.6 | −7.41 / −7.41 |
| TradeCredit | 98.8 / 100 | 99.9 / 99.3 | −7.41 / −10.08 |
| TradeCreditSinceFounding | 100 / 100 | 100 / 100 | −7.41 / −7.41 |

*(Verification round 1, F-V1.)* Where the TradeCredit garrison's grain came from over the 300-turn horizon (tables
§13, mean over seeds):

| T0 = 25, GARRISON-1000, per campaign turn | CGM | CMG |
|---|---|---|
| ration drawn, loss flow (CapBeforeMilitary) | 1,667 | 1,519,681 |
| ration drawn, TradeCredit | 2,594,010 | 2,623,808 |
| of which bought back with the credit | 181,321 (7.0 %) | 219,424 (8.4 %) |
| of which from the physical store | 2,412,689 (93.0 %) | 2,404,384 (91.6 %) |

So the per-turn rise is mostly the accounting effect, not a buy-back:
- **CGM:** the physical store is released from the growth claim.
- **CMG:** growth is no longer frozen (0.7 % → 99.3 % served), so the population, the labour and the harvest stay
  larger, and so does the capped store the ration draws from (INFERRED from the §11 population columns).
- **PEACE and GARRISON-500** per turn import nothing at all, at both epochs.
- **Per War Pulse, the gains are mostly real buy-backs.** WAR-1000 in NORMAL at T0 = 25 draws 1,160 (CGM) / 1,129
  (CMG) more grain than under the loss flow, and 973 / 1,130 of it is bought back.

**What this shows (consequences, not a choice):**
- With the credit opening at zero, the credit a 3-year war draws on is what the war itself earned: the mobilisation
  shrinks the granary, so the first pulse's overflow is sold (WAR-1000, NORMAL, T0 = 25: 1,130 exported and 973
  (CGM) / 1,130 (CMG) bought back in the window). That bought-back grain is 8–10 % of the army's draws. The ordering
  differences of §4–§6 survive almost unchanged. *(Reworded in verification round 1. Revision 3 said the war was
  "fought mostly on the credit it earns itself", which reads as if most of the army's grain was bought back. Tables
  §13 shows it was not.)*
- With the history of overflow sold, every claim is fed and the two orders are indistinguishable. *(Verification round
  1, F-V7: indistinguishable by construction.)* No claim is short in any TradeCreditSinceFounding run, and when
  nothing is short any priority order gives the same state. The opening credit covers 57–72 (turn 25) and 257–267
  (turn 200) years of the civilians' grain requirement and never decays. Under this fate the ordering would matter
  only after a long run of wars or famines had spent the credit (not measured).
- Fed from outside, WAR-LARGE leaves +13.7 % (T0 = 25) and +30.4 % (T0 = 200) more people than peace at +100 turns,
  with no civilian starvation (MEASURED in the model). Births in the 300-turn horizon rise by 283 k against peace at
  T0 = 25. The cause is INFERRED: the headroom counts civilians only, so the army's absence opens civilian headroom
  that births fill while the army eats imported grain; the returning soldiers then add to a larger civilian base, and
  production is labour-proportional (the same mechanism as §4.2, amplified).
- The DIR5 phrase "overflow losses … not deducted under either interpretation" (§3b) has a third reading under trade:
  the grain is deducted from the store but not destroyed, and it is available to the army later.

---

## 7. Against the DIR5 §2.10 economic targets (side findings; calibration is not decided here)

| target | what was measured |
|---|---|
| "500–1,000 troops readily achievable" | **Garrisons yes.** 500 and 1,000 are fed in full at both epochs in NORMAL and BAD harvests (MODEL). **Campaigning no** at turn 25: a continuously marching 1,000 is granted 28–29 % of its march days and 500 is granted 81–82 %, on FULL haul and a 21-day march leg (T2's proposed pulse). *(Verification round 2, F2-1:)* the haul grows with the leg: at a 7-day leg 1,000 get 76–77 % and 500 get 100 %; at a 42-day leg 14–15 % and 44–45 % (§15). At turn 200, both are fully supplied in NORMAL and BAD. |
| "50,000 troops difficult" | **Unreachable.** The existing personnel predicate accepted at most 2,240–3,500 (turn 25) and 10,880–13,680 (turn 200), MEASURED. At that maximum (WAR-LARGE) civilians are short in about half the pulses and the army gets about 4 % of its ration, **under both orders identically**. |
| Garrison cost | Permanent labour loss plus soldiers who never die or reproduce (H7): a 1,000 garrison leaves the population −7.4 % below peace at +100 turns and −12.9 % at +300 (T0 = 25), under both orders. |

---

## 8. Trade-offs for the Director (consequences only; no recommendation)

**If the order is CGM (civilian → growth → military):**
- Growth continues through a shortage. In these worlds that buys few people: +0.07 to +0.30 points of population
  against CMG at T0 = 25 (WAR-500 and WAR-1000, NORMAL and BAD), lasting to +300 turns, and about 0 at T0 = 200,
  because natural increase is near zero.
- On per-pulse settlement the army's ration is within 0–5.1 points of CMG and its march days within 0–1.9 points. Under the existing "nothing
  happens" consequence, its strength is unchanged.
- **With a desertion consequence (M7.14 is open)**, CGM's per-pulse buffer makes the army melt faster. After a 3-year
  bad-harvest campaign of 1,000 at turn 25, the whole army has gone in 3 of 6 seeds, against 242–397 soldiers left
  under CMG. *(Verification round 2, F2-2:)* that pattern holds at 21-day settlement and a 21-day march leg only;
  the Desert proposal has no time constant and is not dt-correct. Across 7–42-day settlement periods and legs, CMG
  keeps more soldiers on average in every configuration measured (85–223 more), and at 42-day settlement CMG also
  loses the whole army in seed 7 (§15).
- If economics ever settle per campaign turn, or if growth is read as eaten, **CGM starves the army**: 0.1 % per turn,
  and 16.7–81.0 % of the ration if growth is eaten.
- It matches DIR5's tier list less literally ("1. Population growth reduces first. 2. Movement is then restricted"),
  because under CGM movement is restricted first. This is a reading of wording, not a ruling.

**If the order is CMG (civilian → military → growth):**
- The army takes the surplus first. Growth is cut in 27.6–82.4 % of the window's periods, against 0–14.1 % under CGM
  (WAR-500 and WAR-1000, NORMAL and BAD, T0 = 25). The demographic cost in these worlds is small, but it lasts: a
  level shift that never closes.
- On per-pulse settlement the army gains only 0–1.9 points of march days and 0–5.1 points of ration over CGM.
- **With desertion**, the army keeps its strength: 587 against 366 soldiers on average, and 2,005 against 1,466 fed
  soldier-years (21-day settlement and leg; 456–599 against 363–405 across the configurations of §15, F2-2).
- If economics settle per campaign turn, **CMG freezes population growth whenever a 10-year ration exceeds the
  1.5-year store**: −55 % population at +100 turns for a 1,000-man garrison at turn 25.
- It is the ordering the Slice F prototype already implements, and the one whose stage names match the existing
  `ConstraintStage` enum.

**Under both orders:**
- Civilians are fed first, and civilian starvation is identical in all 2,592 model pairs. *(Verification round 1,
  F-V4:)* the civilians' nominal shortfall is not identical. CMG's military draw leaves them short in more pulses in 29
  pairs. The shipped dead zone absorbs this, so it does not show in starvation. A smaller dead zone, or any rule that
  reads the nominal deficit, would see it.
- Neither order fixes the real Sim.Core artifact of §3b (soldiers unfed while overflow and spoilage destroy 4–8 times
  as much). That artifact belongs to turn-atomic settlement and the slot order. In the MODEL it disappears at
  per-pulse settlement under either order.

**The overflow fate interacts with the ordering (revision 3; §6.6):**
- Kept as the loss flow (existing), the ordering trade-offs above stand as measured.
- If the overflow were sold and could be bought back (PROPOSALS, both bounds), the outcome depends on how much credit
  the polity has when a war starts. Earned only from T0, it changes little and the orders still differ in 310 of 864
  pairs. Earned since founding, every claim is fed and the orders never differ (864 of 864 pairs identical). That
  is by construction: nothing is ever short, with an opening credit of 57–267 years of civilian grain (F-V7). A large
  army fed from outside leaves the population 13.7–30.4 % above peace.
- Per campaign turn, any trade fate removes both orders' pathologies (CGM ration 98.8–100 %, CMG population gap
  −10.08 % or −7.41 % instead of −54.85 %). *(Verification round 1, F-V1:)* under TradeCredit that comes mostly from
  counting the credit against the growth-headroom grant (91.6–93.0 % of the garrison's ration is drawn from the
  physical store, and 7.0–8.4 % is bought back). So it depends on whether a credit may back growth headroom at all
  (open question 8).
- Trade is a new mechanism with no Sim.Core counterpart (grain never moves through TradeArbitrage). Its realism
  depends on transport cost, prices and partners that these bounds leave out.

---

## 9. "When a civilisation cannot pay": realistic sequence for food (WA4; PROPOSALS measured, none adopted)

**What historically happens when an army's food fails:**
1. Ration cuts.
2. Foraging and requisition.
3. **Desertion.** For pre-modern armies in their own territory this was often a major channel of loss: the unfed went
   home. This is general historical background, not measured here.
4. Disease and death, which grow with the length of the deprivation.
5. Mutiny.

**What this lane implements and measures:**
- **Desert:** coefficient-free. The unfed share leaves at the end of each period and returns to civilian life, so
  people are conserved.
- **Starve:** the shipped starvation law applied to soldiers.

Foraging and requisition from civilians are **not** modelled. Under DIR5 §2.11 civilians are always first, and taking
from them would need the open "military may starve civilians" rule.

These are inputs to M7.14 (DIR5 §2.9), not decisions. The choice of consequence changes the comparison materially
(§6.1).

---

## 10. Determinism, conservation, tests and mutants

### Revision 3 (2026-11-06; merged tree, final code)

**Restore and merge.** `/dev/shm/m7c/F` was rebuilt from `scratchpad/m7c/bundles/F.bundle` (head `97ee570`), then
dev `444b85e` was merged (`df5e742`, no conflict; `git diff fc5230c 444b85e` touches no file under
`Proto.M7.Econ*`, `fixtures/lane-f` or `Sim.*`).

**Tests** (`dotnet test <solution> -c <cfg>`, every run 0 failed, 0 skipped; builds 0 warnings, 0 errors):

| tree | M7Econ.slnx Debug | M7Econ.slnx Release | M7Proto.slnx Debug | M7Proto.slnx Release |
|---|---|---|---|---|
| `444b85e` (dev, measured detached in this clone) | 34 | 34 | 775 | 775 |
| `df5e742` (lane F revision 1 merged onto `444b85e`) | 62 | 62 | 775 | 775 |
| final (revision 3) | **68** | **68** | 775 (lane F changes no Proto.M7* file) | 775 |

The 6 new tests: the trade fate is food-identical to the loss flow while the credit is never drawn (renamed
`…WhileNoClaimIsShort_…` and corrected in verification round 1, F-V1: the property holds only while no claim is short); the sold overflow
feeds a per-turn garrison the loss flow starves; the credit feeds civilians in a later famine; with enough credit both
orders feed every claim and reach the byte-identical state; an opening credit without a trade rule (or negative) is
refused; a pinned TradeCredit state hash. The exact conservation test now covers 90 populated runs (3 armies × 5
overflow rules × 3 consequences × 2 orders) and checks the credit (opening + exported − imported ≥ 0) at every
checkpoint.

**Determinism (revision 3):**

| check | result |
|---|---|
| Revision-1 battery re-run on the merged tree, Release | CSV `f3c80b53…3336`, identical to revision 1 |
| Revision-1 battery with Sim.Core compiled in **Release** | `f3c80b53…3336`, identical |
| Real baseline seed 42, turns 0–60, labour and overflow twins at 25, Sim.Core in **Release**, against the committed fixture (Sim.Core Debug) | **identical**: 61 of 61 turn rows (cohorts, stock, flows, world hash), both labour twins, both 20-turn overflow twins |
| Final battery, Release / Debug / Sim.Core in Release | CSV `05061b8f…d634` in all three; generated tables identical apart from the wall-time line (Debug 313 s) |
| Final battery without the two trade rules' rows | `f3c80b53…3336`: the revision-1 rows are byte-identical |
| `food-bench` model runs with tiered JIT on and off | identical state hashes |

**A correction to revision 1's determinism claim.** `M7Econ.slnx` does not contain Sim.Core, so
`dotnet build M7Econ.slnx -c Release` builds the referenced Sim.Core in its default **Debug** configuration (the build
log prints `Sim.Core -> …/Sim.Core/bin/Debug/…`; MEASURED). Revision 1's "Debug against Release" checks therefore
compared Proto.M7.Econ Debug and Release on a Debug Sim.Core both times. Revision 3 built `Proto.M7.Econ.csproj`
directly with `-c Release` (Sim.Core then compiles in Release) and repeated the checks: identical, as in the table.

**Bench** (`food-bench`, Release, seed 42, host load average ≈ 1, median of 9 timed repetitions):

| item | median |
|---|---|
| `LogisticsPriority.Resolve`, CGM / CMG | 66 / 70 ns per call (69 / 74 with tiered JIT off) |
| one 300-turn model run (T0 = 25, WAR-1000, BAD-1s), CapBeforeMilitary CGM / CMG | 11.4 / 11.5 ms (tiered JIT off); 8.9 ms CMG with tiered JIT on |
| the same, TradeCredit CGM / CMG | 11.3 / 10.9 ms (tiered JIT off); 9.1 / 9.1 ms with tiered JIT on |
| whole battery, 10,128 runs | 61 s Release, 313 s Debug |

With tiered JIT on, the first scenario timed reads 36 ms (minimum 9.2 ms) while the other three read 9 ms: a JIT
warm-up effect of measurement order, which disappears with tiered compilation off (MEASURED). The trade rules cost no
measurable time.

**Mutants (revision 3).** A prioritized subset of 11: five of revision 1's eight, re-anchored to the final code, and
six on the trade fate.
- They ran in a private clone `/dev/shm/m7c/F-mut` at the final commit, with one filtered suite run per mutant (44
  tests: the lane-F class without the real-world test, plus `LogisticsPriorityTests`).
- Each source file was restored from the lane clone after its run and its SHA-256 checked; a CLEAN run before and after
  passed 44 / 44.
- Bound: 50 s = 10 × the 5 s clean incremental build-and-test run. No mutant hung; each took 4–5 s.
- Script: `scratchpad/m7c/work/F/mutants-r3.py`; logs in `/dev/shm/m7c/F-logs/mut-r3/` (not committed).

| Mutant | Change | Killed by (semantic tests) |
|---|---|---|
| MU1 | the order switch is ignored (CGM behaves as CMG) | 5, assertions: the pinned-numbers allocator test, the stage sequence, the grid test, `Model_TheOrderDecidesWhoIsCut`, the trade pinned hash |
| MU2 | AllocateBeforeCap spoils and caps before the allocation | `OverflowRule_TheExistingOrder_…AtCampaignTurnGranularity` (assertion) |
| MU4 | the growth grant is ignored by the demography | `Model_TheOrderDecidesWhoIsCut` (births; assertion) |
| MU6 | WA1 boundary `≥` becomes `>` | 12, assertions: all window-start and window-end pins, Desert, Starve, the order test, a trade famine test, both pinned hashes |
| MU7 | linear spoilage s·h instead of 1 − e^(−s·h) | `Spoilage_IsTheSurvivalExponential…` ×2, the per-turn trade test, both pinned hashes (assertions) |
| MU9 | civilians are not bought back from the credit | `TradeCredit_FeedsCiviliansFromTheCreditInALaterFamine_…` (short periods 5 vs 5), `TradeCreditSinceFounding_EnoughCredit_…` (assertions) |
| MU10 | the allocator does not see the credit | the per-turn trade test (0 vs 0), `…SinceFounding_EnoughCredit_…`, the trade pinned hash (assertions) |
| MU11 | the overflow is sunk instead of exported | 6, assertions: `…WhileTheCreditIsNeverDrawn_…`, the per-turn test, the famine test, `…SinceFounding_…`, conservation (`Overflow` must be 0 under trade), the pinned hash |
| MU12 | the credit is not debited when grain is bought back | 5 tests, all through the model's exact credit guard (`InvalidOperationException: trade credit not conserved`): an exception, not an assertion |
| MU13 | the opening credit is ignored | `…SinceFounding_EnoughCredit_…` and conservation (`CreditInitial` must equal `OpeningCredit`) (assertions) |
| MU14 | granted military draws are not bought back | 5 tests, all through the model's grain guard (`InvalidOperationException: grain overdraw`): an exception, not an assertion |

MU12 and MU14 break conservation itself, and the model refuses to continue; no assertion-level test can observe them
past that guard. They are recorded as guard kills, in the same way as revision 1's MU3.

**Banned constructs (revision 3 scan of every lane-F file).** No `System.Random`, `DateTime`, `float`, `Parallel`,
`Dictionary`/`HashSet`, `GetHashCode` or LINQ. Every parse and format call is culture-invariant. Wall time
(`Environment.TickCount64`, `Stopwatch`) appears only in the CLI reporting and bench paths.

### Revision 1 (2026-11-05; before the merge)

#### Determinism

In every revision-1 row below, Sim.Core was compiled in Debug for both the Debug and the Release run (see the
correction in revision 3, which repeats the checks with Sim.Core in Release).

| check | result |
|---|---|
| Battery CSV, Release | SHA-256 `f3c80b53…3336`, 6,528 runs |
| Battery CSV, Debug | `f3c80b53…3336`, **identical** to Release. The generated tables are identical too, apart from the wall-time line. The Debug run took 607 s. |
| Real baseline seed 42, turns 0–60, labour and overflow twins at 25, Debug against the Release fixture | **Identical**: 61 of 61 turn rows (cohorts, stocks, flows and world hash; the turn-60 hash is `a2ae7ef2e7d27825` in both); both labour twins; both 20-turn overflow twins. |
| Battery re-run from the committed `fixtures/lane-f` with the final Release build | `f3c80b53…3336`, **identical**. |
| Pinned model golden `SameInputs_SameState_AndThePinnedHash` | Passes in Debug and Release (`84a4a967…2b0b`) |

#### Conservation (exact)

- **Allocator:** Civ + Growth + Mil + Unallocated = Free, for both orders, over a grid of 576 input combinations
  (1,152 decisions).
- **Model:** grain, civilians and soldiers are checked against the ledger after every period of every run (the run
  throws on a mismatch), and in tests on 54 populated runs (3 armies × 3 overflow rules × 3 consequences × 2 orders).
- **People:** civilians + soldiers = initial + births − deaths − starvation − soldier starvation.

#### Other rules

- **Orderings:** moves are ordered by (Priority, Id), with a tie-dense test for both orders (8 equal moves, permuted
  input order, identical decisions). No new double-scored ordering was added. `Proportional` breaks remainder ties to
  the lower cohort index and has its own test.
- **WA1 delivery pins:**
  - window start exactly at a pulse start → that pulse;
  - start −1 day → that pulse;
  - start +1 day → the next pulse;
  - the same three cases for the window end;
  - each pin also checks the period count and the march days actually requested.
- **Serialized row types:** none added. The baseline text form has a populated exact round-trip test.

#### Tests (M7Econ.slnx)

| | Debug | Release |
|---|---|---|
| before (fc5230c) | 34 passed, 0 failed, 0 skipped | 34 passed, 0 failed, 0 skipped |
| after | **62 passed**, 0 failed, 0 skipped (6 m 38 s) | **62 passed**, 0 failed, 0 skipped (6 m 47 s) |

- The 28 new tests are in `FoodAllocationComparisonTests`. One of them, the real-world series test, steps seed 42 to
  turn 28 (about 20 s).
- Builds: Debug and Release, 0 warnings and 0 errors (`TreatWarningsAsErrors`).
- The durations come from a host shared by about 9 agents (load average about 50). The baseline runs took 2 m 27 s to
  2 m 29 s on the same host before it was this loaded.

#### Mutants

- **Where they ran:** a private copy `/dev/shm/m7c/F-mut`. The source was restored after each run and its SHA-256
  checked.
- **Bound:** 540 s, which is 10 × the 54 s clean filtered build-and-test run (38 tests).

No mutant hung. Each run took 35–66 s, including the build.

| Mutant | Change | Killed by (semantic tests) |
|---|---|---|
| MU1 | The order switch is ignored: CGM behaves as CMG | 4 tests: `SameInputs_TheTwoOrdersServe…PinnedNumbers`, `CivilianGrowthMilitary_StageSequence`, `BothOrders_…OverAGrid`, `Model_TheOrderDecidesWhoIsCut` (assertions) |
| MU2 | AllocateBeforeCap spoils and caps before the allocation | `OverflowRule_TheExistingOrder_…AtCampaignTurnGranularity` (assertion) |
| MU3 | Returning soldiers (demobilised or deserted) vanish instead of rejoining the cohorts | 12 tests, including `Conservation_IsExact_…`, `Desert_ReturnsTheUnfedShare…` and the WA1 pins. All fail through the model's own exact conservation guard (`InvalidOperationException: civilians not conserved`), an exception rather than an assertion. |
| MU4 | The growth grant is ignored by the demography (H_eff = vacancy) | `Model_TheOrderDecidesWhoIsCut` (births: assertion) |
| MU5 | The harvest remainder is dropped (per-period floor, not dt-correct) | `HarvestAndDemography_DoNotDependOnThePeriodLength` ×2, the pinned hash (assertions) |
| MU6 | WA1 boundary `≥` becomes `>` | 10 tests: `WindowStart` (0 → 0, 21 → 21), all 4 `WindowEnd` pins, Desert, Starve, the order test, the pinned hash (assertions) |
| MU7 | Linear spoilage s·h instead of 1 − e^(−s·h) (the CR-001 fragility) | `Spoilage_IsTheSurvivalExponential…` ×2 (both granularities), the pinned hash (assertions) |
| MU8 | Demography integrated per allocation period instead of the kernel's 180-day grid | `HarvestAndDemography_DoNotDependOnThePeriodLength` ×2, the pinned hash (assertions) |

Logs are in `/dev/shm/m7c/F-logs/mut/` (not committed). The script is
`…/scratchpad/m7c/work/F/mutants.py`.

#### Banned constructs

Scan of the new and edited files:
- no System.Random, DateTime, float, Parallel, Dictionary or HashSet, GetHashCode or LINQ;
- `Environment.TickCount64` appears only for report wall time, as in the existing calibration;
- every Parse and format call is culture-invariant.

---

## 11. Limitations and open questions

**Limitations:**
- **It is a model.** The comparison runs in a reduced-order model anchored to real Sim.Core series. It is not Sim.Core
  with a switch. Validation covers peace only (§2.3).
- **One polity, aggregated:**
  - per-settlement stores, relief and caps are not modelled;
  - the cap is aggregated (exact only when every settlement is above or below its own cap together).
- **Soldiers neither age nor die** outside the Starve consequence (as shipped, H7). This drives the "war leaves more
  people" artefact of §4.2.
- **The War Pulse lengths are T2's PROPOSAL** (D-079 Q1 is open). *(Verification round 2, F2-1:)* they are also the
  default march leg, and every march-days, haul and growth-served figure in §0–§8 is close to a linear function of the
  leg: a year of continuous marching costs about leg × (haul of one day's rations over one day's route). The haul is
  now dt-correct in the SETTLEMENT period (§15 table A), but it still depends on the leg, which no rule ratifies.
- **The campaign is a stress definition:** a continuous march, resupplied every leg (since round 2; before, one move
  of the pulse's length every pulse, which is the same thing at the default leg). The haul basis is open.
- *(Verification round 2, F2-2.)* **The Desert proposal is not dt-correct.** It has no time constant: the army adjusts
  at once to the strength the period's grant feeds, and each loss is permanent. Under a constant supply that is
  partition-invariant, but supply transients (the store running out, the growth buffer) are read per period, so the
  outcome depends on the settlement period (§15 tables D and E). WA4 keeps Law 3 binding on the "cannot pay" rules, so
  Desert as built is not admissible; its numbers are reported at 21-day settlement and leg. The Starve proposal is a
  per-year law and is stable across settlement periods (§15 table F; test `Starve_IsAPerSimYearLaw_…`).
- **No forest or jungle terrain**, as in Slice F.
- **The shortage regimes** are overlays derived from `sigmaLogYield`, not the stochastic weather process.
- *(Verification round 1, F-V4.)* Military-caused civilian **shortfall** across pulses is NOT zero: CMG leaves
  civilians nominally short in more pulses in 29 primary pairs (32 before the round-2 F2-5 fix). Starvation is equal only because the shipped 0.20 dead
  zone absorbs these shortfalls.
- **The allocator reserves nothing** for future civilian needs. Military-caused civilian starvation across pulses was
  measured instead: it is 0 in every cell except where civilians are short in peace too.
- **The labour response is measured at T0** and applied as an elasticity for the whole horizon.
- **The "recovered" metric is nearly uninformative**, because population levels are neutrally stable in this economy.
- **The trade fate is a pair of bounds (revision 3).** Price fixed at the numeraire 1.0, no transport cost, an
  unlimited partner, a credit that never decays and no counterparty that could refuse or default. Nothing in Sim.Core
  trades grain today. `TradeCreditSinceFounding` ignores purchases before T0, so its opening credit is an upper
  estimate. The credit is a grain-value account in the model's own ledger, not a Sim.Core money stock.
- **Every rule switches on at T0.** NoCap and TradeCredit start from the real, capped T0 store with no carried history;
  only TradeCreditSinceFounding gives the trade fate a history. A NoCap world would also hold a larger store at T0 (the
  spoilage equilibrium); that was not modelled.
- **No charts.** The brief made PNG charts optional; `matplotlib` is not installed in this container, so the evidence is
  in tables only.

**Open questions for the Director:**
1. **D-079 Q3:** the ordering. This report is the evidence. The lane brief forbids a recommendation. DIR5 §2.11 and
   D-079 Q3 say "the agent will add a recommendation", which by DR51 belongs to Phase D. The orchestrator should
   reconcile this.
2. **What "growth" claims physically:** headroom (the Slice F and Sim.Core reading, primary here) or eaten food (§6.4).
   The answer changes the comparison more than the ordering does.
3. **Settlement period of food (WA4 delegated):** per War Pulse or per campaign turn. At per-turn settlement both
   orders are pathological (§6.3).
4. **M7.14 unfed consequence:** None, Desert or Starve. Under Desert the orders diverge in military strength (§6.1).
5. **Granary capacity basis:** civilian demand only (existing; mobilisation shrinks the granary) or including the army.
6. **Overflow rule:** cap before or after the military draw. It matters per turn and hardly per pulse (§6.2).
7. **Fate of the overflow (revision 3):** lost (existing), carried, or traded. A trade fate with a history of sold
   overflow removes the ordering question in every cell measured (§6.6). *(Verification round 1, F-V7: that holds by
   construction. Its credit covers 57–267 years of civilian grain, so no claim is ever short, and the result says
   nothing about the orders themselves.)* It would be a new mechanism (grain never moves
   through TradeArbitrage), with transport cost, prices and partners still to be specified.
8. **(Verification round 1, F-V1) What a trade credit may back.** In both trade PROPOSALS the allocator counts store +
   credit for every claim, so the growth-headroom grant (permission, not a physical draw) can be granted against money,
   and the physical store is released to the army without any import. That accounting effect accounts for most of
   TradeCredit's per-campaign-turn result (§6.6, tables §13). A credit that backs only physical draws (civilians,
   ration, haul, eaten growth) would be a different rule. It is not implemented or measured here, and it is a detail
   of a proposal that nothing in Sim.Core decides.
9. **(Verification round 2, F2-1) The march leg.** How many days a campaigning army marches between two resupplies.
   The model's default is T2's proposed pulse (one operational move per pulse), but it is a physical parameter, not the
   settlement period, and the cost of campaigning scales with it almost linearly (§15 table B: a 1,000-man army at
   turn 25 is granted 75.6–76.7 % of its march days at a 7-day leg and 14.4–14.8 % at a 42-day leg). The lane does not
   choose it.
10. **(Verification round 2, F2-2) A desertion time constant.** A dt-correct desertion rule needs a rate (how fast
    unfed soldiers leave), which no shipped rule supplies. The lane does not invent one; the Desert proposal as built is
    not dt-correct (§11 limitations), so its divergence between the orders is evidence of direction, not of size.

---

## 12. Correction note on `F-economy.md` (appended there; earlier text unchanged)

`F-economy.md` line 23 reports "Passed: 23" in Debug and Release. That is the first-pass count, from before
`EconVerificationFixTests.cs` (11 cases) was added. The D-079 dated note there already records this.

Measured on `fc5230c` in this lane on 2026-11-05: **34 passed in Debug and 34 in Release.** After lane F the count is
**62** in both configurations (34 + 28; §10).

Revision 3 (2026-11-06) appended a second dated note there: **34** in Debug and Release on dev `444b85e` (measured in
this clone), and **68** after revision 3 (34 + 34 in `FoodAllocationComparisonTests`; §10).

Verification round 1 (2026-11-06) appended a third: **85** in Debug and Release (34 + 51; §14).

Verification round 2 (2026-11-06) appended a fourth: **101** in Debug and Release (34 + 67; §15).

---

## 13. How to reproduce

```
cd prototypes/m7-sprint1
dotnet build M7Econ.slnx -c Release -m:2 -nodeReuse:false -nologo
dotnet test  M7Econ.slnx -c Release --no-build -nologo
E=Proto.M7.Econ/bin/Release/net10.0/Proto.M7.Econ.dll
dotnet $E food-baseline --seeds 1,2,42,7,3,5 --max-turn 325 --twins 25,200 --otwin-horizon 20 --out-dir /tmp/lf   # ~2 min/seed
dotnet $E food-compare --in-dir fixtures/lane-f --out H-F-tables.md --csv runs.csv --trade-csv trade.csv           # ~1-5 min
dotnet $E food-trace --in-dir fixtures/lane-f --seed 42 --army WAR-1000 --regime BAD-1s --order CGM --periods 60 \
    --overflow TradeCredit                                                                                         # trade lines show exported/imported/credit
dotnet $E food-bench --in-dir fixtures/lane-f --seed 42 --reps 9
dotnet $E food-pulse --in-dir fixtures/lane-f --out H-F-pulse.md                                                  # §15 tables A-F (~1.5 s)
dotnet $E food-trace --in-dir fixtures/lane-f --seed 42 --army WAR-1000 --regime BAD-1s --order CGM --pulse-days 7 --leg-days 21
grep -v -E ',TradeCredit(SinceFounding)?,' runs.csv | sha256sum                                                    # = f3c80b53...3336 up to round 1 only (F2-5 changed the rows)
# Sim.Core in Release (M7Econ.slnx builds it in Debug; see §10):
dotnet build Proto.M7.Econ/Proto.M7.Econ.csproj -c Release -p:OutDir=/tmp/lf-rel/
```

The committed fixtures are the series this report used. `food-baseline` regenerates them, deterministic apart from the
wall-time field on the `series` line.

---

## 14. Verification round 1 (2026-11-06; findings F-V1 to F-V11 against `d1a4342`)

**Method.** Each finding was reproduced on the lane tree at `d1a4342` before any change. Code was read, traces and
the revision-3 CSV were analysed, and mutants were run in the private clone `/dev/shm/m7c/F-mut`, checked out at
`d1a4342` and then at the fix commit. A finding counts as REPRODUCED only on this session's own measurement. The
verifier's logs were read but not used as evidence.
- **Scripts:**
  - `scratchpad/m7c/work/F/mutants-fix-r1.py` (mutants);
  - `repro-fix-r1.py` and `analyse-fix-r1.py` (CSV analysis).
- **Logs:** `/dev/shm/m7c/F-logs/fix-r1/` (not committed).
- **Mutant runs:**
  - one filtered suite run per mutant: the lane-F class without the real-world test, plus `LogisticsPriorityTests`;
  - bound = 10 × the CLEAN run: 100 s at `d1a4342` (CLEAN 10 s), 80 s at the fix (CLEAN 8 s);
  - no mutant hung, and none took more than 7 s;
  - after each run, the file was restored with `git checkout` and its SHA-256 compared with the HEAD blob.

**No finding was refuted.** Every fix is a semantic test, a report correction, or both. Neither the model's
arithmetic nor any rule changed. The main CSV and both pinned state hashes are byte-identical to revision 3.

| id | severity | verdict | reproduction (this session) | fix | semantic test (new unless stated) |
|---|---|---|---|---|---|
| F-V1 | major | **REPRODUCED** | Trace, seed 42, PEACE, per campaign turn, CGM: `imported 0` in every turn. Turn-1 growth grant 22,647 against 6,685; ration 200/200 from turn 3 against 0/200; end stock 6,741 against 6,941. GARRISON-1000: turn 3 grants 1,587 ration with 0 imported, then 10,000/10,000 with 3,989–4,205 imported per turn. Mutant V9 (under trade, growth may only be granted against the physical store) was killed only by the trade pinned hash. | False claim corrected in the `OverflowRule.TradeCredit` doc and in §0 item 9, §6.6, §8, §10 and §11 (new open question 8). New reporting split `FoodLedger.ImportedForCivilians` (not a flow; not hashed). New table §13: the army's draws split into bought back and from the store. Per-turn headline re-attributed: 93.0 % (CGM) and 91.6 % (CMG) of the TradeCredit garrison's ration comes from the physical store. | `TradeCredit_AnUndrawnCreditStillRelaxesTheAllocator_TheGrowthGrantIsCountedAgainstTheCredit`; `TradeCredit_WhileNoClaimIsShort_…` (renamed; now asserts that nothing is short) |
| F-V2 | major | **REPRODUCED** | Mutant V1 (desertion × 0.5): killed only by the two pinned hashes (golden). | Test | `Desert_TheArmyLosesExactlyTheUnfedShare_OnePulse` (CMG and CGM; exactly ⌊n × unfed share⌋ leave a partly fed pulse) |
| F-V3 | minor | **REPRODUCED** | Mutant V2 (no dead zone): SURVIVED, 44/44. | Test | `Starve_TheDeadZoneAbsorbsASmallUnfedShare_AndTheExcessFollowsTheShippedLaw` (4 cases). Unfed share 0.146 → 0 deaths, where 160 would die without the dead zone. Share 0.571 → exactly ⌊n(1 − e^(−0.12·dEff·10))⌋. The law is restated in the test, not called. |
| F-V4 | minor | **REPRODUCED** | CSV: civilian-short periods differ in 10 / 12 / 10 / 9 pairs (CapBeforeMilitary / AllocateBeforeCap / NoCap / TradeCredit), CMG higher in all 41. Civilian starvation differs in 0. The verifier's three examples match exactly (33/41, 32/41, 35/36). | §0 item 3, §5 (new row and note), §8 and §11 | `CivilianShortPeriods_DifferByOrder_WhileCivilianStarvationIsIdentical` (synthetic WAR-500, BAD-1s: 35 against 31 short pulses, starvation 0 = 0) |
| F-V5 | minor | **REPRODUCED** | Mutant V3 (per-period floor of the ration): killed only by the trade pinned hash. | Test | `RationClaim_IsIntegratedPerSimYear_IndependentOfThePeriodLength` (GARRISON-1000 and PEACE: 516 War Pulses request ⌊soldiers × upkeep × 30 y⌋, −1 at most, the same as 3 campaign-turn periods) |
| F-V6 | minor | **REPRODUCED** | Mutant V4 (capacity basis includes soldiers): killed only by the trade pinned hash. | Test | `GranaryCapacity_IsCivilianDemandOnly_SoMobilisationShrinksIt` (CapBeforeMilitary and AllocateBeforeCap × both orders): the capacity read back from the run equals ⌊1.5 × Σw·civilians × need⌋ after mobilisation, and the garrison's capacity is smaller than peace's. |
| F-V7 | minor | **REPRODUCED** | CSV: 0 of 1,800 TradeCreditSinceFounding runs are short of anything, in the window or over the horizon. Opening credit = 57.1–72.0 years (T0 = 25) and 257.2–266.8 years (T0 = 200) of the civilian requirement at T0 + 1, per seed. | The tautology is now stated next to every claim: §0 item 9, §5, §6.6, §8, §11 Q7. | None needed (a report claim). The existing `TradeCreditSinceFounding_EnoughCredit_…` already pins the mechanism: identical only when nothing is short. |
| F-V8 | nit | **REPRODUCED** | Mutant V6 (eaten growth not bought back): SURVIVED, 44/44. The growth sensitivity runs CapBeforeMilitary only. | Test. The battery is unchanged; it still does not run TradeCredit with eaten growth. | `TradeCredit_GrowthEatenBeyondTheStore_IsBoughtBackFromTheCredit`. It kills V6 only through the model's grain guard (`InvalidOperationException: grain overdraw`), not through an assertion: the mutant breaks conservation itself, as MU12 and MU14 did in revision 3. |
| F-V9 | nit | **REPRODUCED** | `LogisticsPriority.cs:103-104`: the 4-argument overload delegates to the 5-argument one, so the grid test compares the code with itself. | §1 corrected | `CivilianMilitaryGrowth_ReproducesThePreLaneFAllocator_OverAGrid_DigestPinnedAtFc5230c`. The digest `576 c28a6bff…752b` was measured by compiling `fc5230c`'s `LogisticsPriority.cs` on its own with the same digest code. CGM must differ from it. |
| F-V10 | nit | **REPRODUCED** | CSV: four cases where CGM keeps more soldiers. AllocateBeforeCap seeds 5 and 7 (753 against 752), NoCap seed 2 (757 against 756), and TradeCredit seed 42, WAR-1000, SEVERE-2s, Starve (758 against 756). | §0 item 6(a) and §5 corrected | None (a report count; the evidence is the CSV analysis script) |
| F-V11 | nit | **REPRODUCED** | CSV header (`FoodComparison.cs:233-236`) has no Exported, Imported or Credit column. | New `FoodComparison.TradeCsv`, written by `food-compare --trade-csv`: one row per trade-rule run, with window and horizon flows, the civilian split, ration, haul, eaten growth, opening and final credit, and the state hash. Its own hash is printed and shown in tables §13. The main CSV is unchanged. §2.4 corrected; §0 item 9 now gives per-seed ranges. | `TradeCsv_HasOneRowPerTradeRun_AndItsColumnsAreTheLedgers` (populated: 8 trade rows; columns equal to the ledgers; Σ exported and Σ imported > 0) |

**Mutants** (`d1a4342` → fix commit; every kill below includes at least one assertion-level semantic test, except V6):

| mutant | change | at `d1a4342` | at the fix commit |
|---|---|---|---|
| V1 | desertion × 0.5 | killed by 2 pinned hashes only | KILLED: `Desert_…OnePulse` ×2 (assertions) + both hashes |
| V2 | dead zone removed from Starve | **SURVIVED** | KILLED: `Starve_TheDeadZone…` ×4 (assertions) |
| V3 | ration floored per period (no remainder) | trade hash only | KILLED: `RationClaim_…` ×2 (assertions) + trade hash |
| V4 | capacity basis includes soldiers (CapBeforeMilitary and trade path) | trade hash only | KILLED: `GranaryCapacity_…` ×2, the CapBeforeMilitary cases (assertions) + trade hash |
| V6 | eaten growth not bought back | **SURVIVED** | KILLED by `TradeCredit_GrowthEaten…` through the grain guard (exception, not an assertion) |
| V9 | under trade, growth granted against the physical store only | trade hash only | KILLED: `…AnUndrawnCreditStillRelaxes…` and `…GrowthEaten…` (assertions) + trade hash |
| MU10 | the allocator does not see the credit | (revision 3: killed) | KILLED by 6 tests, including `…AnUndrawnCreditStillRelaxes…` and `TradeCsv_…` (assertions) |

CLEAN runs: 44/44 at `d1a4342` and 61/61 at the fix commit, both before the mutants.

**Determinism (fix commit, MEASURED).**

| run | wall time | main CSV SHA-256 | trade CSV SHA-256 |
|---|---|---|---|
| Release (`M7Econ.slnx -c Release`) | 66.7 s | `05061b8f…d634` | `ea6fcc141b3dfdcd1966ab47693101ca9f11a8d3fa0dbd09569ae4f3e8e2fe7d` |
| Debug | 319.2 s | `05061b8f…d634` | `ea6fcc14…fe7d` |
| Sim.Core compiled in Release (`Proto.M7.Econ.csproj -c Release`) | 67.3 s | `05061b8f…d634` | `ea6fcc14…fe7d` |

- The main CSV is byte-identical to revision 3: the round changed no model arithmetic.
- The three generated tables files are identical apart from the wall-time line.
- `H-F-tables.md` differs from revision 3 only in that line and in the appended §13.

**Tests** (`dotnet test M7Econ.slnx -c <cfg>`; builds 0 warnings and 0 errors; 0 failed, 0 skipped):

| tree | M7Econ Debug | M7Econ Release | M7Proto |
|---|---|---|---|
| `444b85e` (dev baseline) | 34 | 34 | 775 |
| `d1a4342` (revision 3) | 68 | 68 | 775 |
| fix commit (round 1) | **85** | **85** | not re-run: no `Proto.M7*` file and no `Sim.*` file changed (`git diff d1a4342 --stat` touches only the 4 `Proto.M7.Econ*` files and the 2 reports) |

**Bench** (`food-bench`, Release, seed 42, 9 repetitions, host load average 1.2–1.7). With tiered JIT off, for
comparison with revision 3:
- `Resolve`: 58.5 / 62.3 ns per call (CGM / CMG).
- 300-turn run, CapBeforeMilitary: 9.3 / 11.3 ms.
- 300-turn run, TradeCredit: 11.4 / 10.8 ms.

Revision 3 measured 69 / 74 ns, 11.4 / 11.5 ms and 11.3 / 10.9 ms. With tiered JIT on, the first two scenarios read
21.3 and 16.3 ms (the warm-up effect noted in revision 3), and the trade runs 10.7 / 10.4 ms. The only hot-path change
is one counter increment in `Reimport`.

**Banned constructs (round-1 scan of every lane-F file):**
- None of `System.Random`, `DateTime`, `float`, `Parallel`, `Dictionary`/`HashSet`, `GetHashCode` or LINQ.
- Every parse and format call is culture-invariant.
- Wall time (`TickCount64`, `Stopwatch`) appears only in the CLI and bench paths.

**Limitations of the fixes:**
- **F-V1:** §13 splits each TradeCredit run's draws by where the grain came from inside that run. It is not a
  counterfactual attribution: the loss-flow and trade trajectories diverge, through population, labour and harvest.
  The alternative rule, a credit that backs only physical draws, is neither implemented nor measured (open question 8).
- **F-V4:** the property is pinned on one synthetic case. The real-world count (32 primary pairs) comes from the CSV.
- **F-V8:** the kill is a guard kill. TradeCredit with eaten growth is still absent from the battery.
- **Chosen by the lane:** the Desert and Starve tests use scenario parameters (harvest multipliers 0.50 / 0.58 / 0.66 /
  1.00) chosen with a probe so that a pulse or turn is partly fed. Each test asserts that precondition before it pins
  the law.

---

## 15. Verification round 2 (2026-11-06; findings F2-1 to F2-5 against `b431323`)

**Method.** Each finding was reproduced on the packet commit `b431323` before any change, in the private clone
`/dev/shm/m7c/F-mut`. The verifier's logs were read but not used as evidence.
- **Pulse probe on `b431323`:** a temporary edit made the 3,600-day turn's pulse length read `M7F_PULSE` and added the
  exact growth claim and the march days to the trace, built to a separate output folder and reverted with
  `git checkout` (script `scratchpad/m7c/work/F/repro-fix-r2.py`; log `/dev/shm/m7c/F-logs/fix-r2/repro-b431323.txt`).
- **Mutants:** `scratchpad/m7c/work/F/mutants-fix-r2.py`; one filtered suite run per mutant (the lane-F class without
  the real-world test, plus `LogisticsPriorityTests`); after each run the file is restored with `git checkout` and its
  SHA-256 compared with the HEAD blob. The timeout passed was 600 s; the CLEAN runs took 8–9 s, so the 10 × CLEAN
  bound is about 90 s, and no mutant took more than 5 s. No mutant hung.
- **CSV recomputation:** `section5-fix-r2.py` (§5 counts; it reproduces every §5 count on the round-1 CSV except
  "growth served higher under CGM", where it gives 441 against the 438 revision 1 printed), plus the round-1 scripts
  `analyse-r3.py`, `repro-fix-r1.py` and `analyse-fix-r1.py` run on both CSVs and diffed.
- **Sensitivity tables:** `food-pulse` (new), written to `H-F-pulse.md` (T0 = 25, horizon 20 turns, 6 seeds).

| id | severity | verdict | reproduction (this session) | fix | semantic test (new) |
|---|---|---|---|---|---|
| F2-1 | major | **REPRODUCED** | Probe on `b431323`, WAR-1000 NORMAL, None, 6 seeds, pulse 7 / 14 / 21 / 42 d: window haul requested 8,680 / 17,082 / 25,792 / 51,116; march days granted (CGM) 76.9 / 42.9 / 28.3 / 13.8 %; growth served (CMG) 75.0 / 34.3 / 20.6 / 8.0 %. WAR-500 march (CGM) 100 / 98.7 / 81.4 / 42.7 %. The verifier's figures, exactly. Cause: one move of the period's length per period, billed by MovementFood, whose haul grows with the square of the move length. | The march is now a continuous march resupplied every LEG (`FoodPeriods.MarchLegDays`; default T2's proposed pulse; `FoodScenario.MarchLegDays` / `--leg-days` to vary it). One leg is billed by MovementFood and a period draws len ÷ leg of it with a remainder accumulator, so the yearly haul does not depend on the settlement period (table A). The battery is byte-identical under this change (every war-window period is one full leg; MEASURED: the round-2 tree with only the growth ceiling restored reproduces the round-1 main and trade CSV hashes, determinism table below). The report now states the dependence on the leg (§0 item 8, §2.2, §2.4, §7, §11 limitation and open question 9). | `MarchHaul_IsBilledPerSimYear_ByTheLeg_NotByTheSettlementPeriod` (6 cases: period 7, 14, 21, 42 d at a 21-d leg; leg 7 and 42 d at 21-d settlement): the claim over a continuous march of D days is ⌊legHaul × D ÷ leg⌋, −1 at most. |
| F2-2 | major | **REPRODUCED** (headline) / **PARTLY REFUTED** (mechanism) | Probe on `b431323`, WAR-1000 BAD-1s Desert, soldiers at the window's end, CGM / CMG per seed: identical to the verifier's 7, 14, 21 and 42-d rows (e.g. 7 d: 328/579, 1000/1000, 0/158, 0/0, 0/0, 1000/1000). GARRISON-1000 SEVERE-2s Desert: seeds 3, 5, 7 lose all 1,000 under both orders at every pulse length. **Refuted in part:** (1) most of the dependence the verifier measured is F2-1's haul, not Desert: with the leg held at 21 d (table D), CMG keeps 250–385 soldiers in seeds 5 and 7 at 7- and 14-d settlement, where the probe lost them all. (2) The rule does not remove an amount that scales with the period (a derivation, not a measurement): it adjusts the army at once to the strength the period's grant feeds (soldiers × unfed share = soldiers − grant ÷ (upkeep × h)), which is partition-invariant under a constant supply. The garrison case is evidence of that: the same outcome at every period length (table E). **Reproduced:** (3) a residual dependence on the settlement period remains with the leg fixed: per-seed strengths move by up to 308 soldiers between 7- and 42-d settlement (seed 7 under CMG: 250 / 308 / 242 / 0), while the per-year Starve law moves by at most 8 (table F). The rule has no time constant, so it is not dt-correct, and WA4 keeps Law 3 binding on it. | Report: the headline divergence is qualified as a 21-d result in §0 item 6(a), §6.1 and §8; what holds in every configuration measured is stated (CMG keeps 85–223 more soldiers on average); §11 adds a limitation and open question 10 (a desertion time constant, which the lane does not invent). No rule was changed: a dt-correct desertion needs a rate no shipped rule supplies. | `Starve_IsAPerSimYearLaw_TheSettlementPeriodDoesNotMoveTheArmy` (7, 14, 21, 42 d; a constant deprivation follows 1000 × e^(−0.12 × years), ±2). Desert gets no test of a property it does not have. |
| F2-3 | minor | **REPRODUCED** | Mutant M1 (`ImportedForCivilians += 0`) on `b431323`: SURVIVED 61/61. | Test | `TradeCredit_TheImportSplit_CiviliansBuyBackWhatTheyAte_TheArmyWhatItDrew` (both orders; empty store, no harvest in the window, ample credit): ImportedForCivilians = EatenCivilian and Imported − ImportedForCivilians = Ration + Haul, exactly. |
| F2-4 | minor | **REPRODUCED** | Mutant M4 (march metric from the haul ratio) on `b431323`: SURVIVED 61/61. A second mutant, M5 (growth served from the ration ratio), also SURVIVED. | Test | `Metrics_AreTheWindowRatiosTheirNamesSay_AndTheCsvColumnsCarryThem`: 15 `Metrics` fields (window ratios and shares, window ledger differences, army sizes, pop +20 and its gap) against an independent computation from the run (with march days ≠ haul and growth ≠ ration asserted first), and the CSV columns `marchAvail`, `rationSupply`, `growthServed`, `gap20`, `haulWar`, `soldiersWarEnd`, `deserted` against the metrics. |
| F2-5 | nit | **REPRODUCED** (now MEASURED) | Probe on `b431323`, seed 42, WAR-1000 NORMAL CMG, 20 turns: Σ ceiled claims − Σ exact = +0.49 to +0.52 unit per period: +0.64 % of the claim at 7-d pulses, +0.33 % at 14 d, +0.22 % at 21 d, +0.12 % at 42 d. | `growthDemand = Carry(ref _remGrowth, exact)`. It moved the battery CSV (`05061b8f…d634` → `47b67aeb…cf0c`) and the trade-rule pinned hash (re-pinned; with the ceiling restored on the round-2 tree the old hash `7372c81b…101a` is reproduced exactly, so F2-5 is its only cause). The other pinned hash did not move. Figures updated in place (list below). | `GrowthClaim_IsIntegratedWithARemainder_NoPerPeriodRounding` (7-d and 21-d pulses, per campaign turn): after every period 0 ≤ Σ exact − Σ claimed < 1, the exact claim read from the trace's new `Gx` field. |

**Sensitivity to the settlement period and the march leg** (`H-F-pulse.md`, regenerated by `food-pulse`; CGM / CMG;
means over 6 seeds; T0 = 25, NORMAL, consequence None unless stated):

| table | what varies | WAR-1000 march granted % | WAR-1000 growth served % (CMG) | WAR-500 march granted % |
|---|---|---|---|---|
| A | settlement 7 / 14 / 21 / 42 d, leg 21 d | 29.2 / 29.4, 28.7 / 29.2, 28.3 / 29.1, 27.2 / 28.7 | 21.9, 21.3, 20.6, 19.2 | 82.7 / 83.0, 82.0 / 82.6, 81.4 / 82.4, 79.9 / 81.9 |
| B | leg 7 / 14 / 21 / 42 d, settlement 21 d | 75.6 / 76.7, 42.4 / 43.5, 28.3 / 29.1, 14.4 / 14.8 | 73.7, 33.6, 20.6, 9.5 | 100 / 100, 98.3 / 98.9, 81.4 / 82.4, 44.2 / 45.2 |
| C | both together (the march definition before round 2; growth claim as in round 2, so a few cells differ by 0.1 from the `b431323` probe in the F2-1 row) | 76.9 / 77.3, 42.9 / 43.6, 28.3 / 29.1, 13.8 / 14.6 | 75.0, 34.2, 20.6, 8.0 | 100 / 100, 98.7 / 99.1, 81.4 / 82.4, 42.7 / 44.6 |

- In table A the haul per march day is 23.62 (WAR-1000) and 12.00 (WAR-500) at every settlement period: the bill is
  dt-correct. What remains is the allocation granularity: about 2 points of march days from 7 to 42 days.
- In table B the haul per march day is 8.00 / 15.64 / 23.62 / 46.81 for 7 / 14 / 21 / 42-day legs (WAR-1000): nearly
  proportional to the leg. Every march-days, haul and growth-served figure of this report is a 21-day-leg (T0 = 25) or
  14-day-leg (T0 = 200) figure.
- The ordering comparison keeps its shape in every row: CMG grants 0.2–2.0 more points of march days and serves less
  of the growth claim; growth under CGM is served in full. At a 7-day leg WAR-500 is fully supplied under both orders
  and the two orders give the same state; at 21 days CMG serves 73.2 % of the growth claim.

Desert, WAR-1000 BAD-1s, soldiers at the window's end, mean CGM / CMG (table D; per seed in `H-F-pulse.md`):

| settlement (d) \ leg (d) | 7 | 14 | 21 | 42 |
|---|---|---|---|---|
| 7 | 371 / 456 | | 405 / 599 | |
| 14 | | 372 / 492 | 363 / 586 | |
| 21 | 370 / 456 | 374 / 514 | **366 / 587** | 376 / 592 |
| 42 | | | 367 / 487 | 364 / 574 |

The 21 / 21 cell is the battery's. CMG keeps more in every cell (85–223 on average); the per-seed pattern does not
hold across cells (e.g. seed 7 under CMG: 250, 308, 242 and 0 soldiers at 7, 14, 21 and 42-day settlement with a
21-day leg). Starve (table F) on the same cells: 861–862 / 862–864.

**Figures that moved with F2-5** (round-1 value → round-2 value; all others in §0–§9 checked unchanged against the
regenerated tables and CSV analyses):
- §4.1: GARRISON-1000 SEVERE growth served 97.3 / 97.0 → 97.4 / 97.2, fed soldier-years 2,747 / 2,748 → 2,748;
  WAR-500 BAD growth 46.2 → 46.3 (CMG); WAR-1000 NORMAL min stock 158 → 157 (CGM); WAR-1000 BAD growth 88.0 / 17.8 →
  87.9 / 17.7 and periods with growth cut 13.5 → 14.1 % (CGM); WAR-1000 SEVERE growth 49.0 / 45.8 → 48.5 / 45.3, fed
  soldier-years 545 → 546 (CGM); WAR-LARGE growth 84.8 → 84.7 (CGM); T0 = 200 WAR-1000 SEVERE growth 85.9 / 81.2 →
  85.8 / 81.3.
- §4.2: WAR-1000 SEVERE (CGM) gap +20 −0.58 → −0.59, pop +100 12,694 → 12,695, pop +300 43,662 → 43,661.
- §4.3: the trace's growth claim 115 → 114–115; the CGM store 115–134 → 114–137.
- §4.4: WAR-500 NORMAL growth max 50.0 → 49.9; WAR-500 BAD 53.8 [40.3, 63.5] → 53.7 [40.3, 63.4]; WAR-1000 BAD min
  10.4 → 10.7; WAR-1000 SEVERE ration −0.1 → −0.0, growth max 11.4 → 11.5, Δ pop +100 0 [−3, 3] → +1 [0, 4], Δ pop
  +300 +4 → +3; GARRISON-500 SEVERE+EMPTY growth max 97.1 → 97.3.
- §5: ration higher under CMG 196 → 190; civilian-short pairs 32 → 29; rounding cases −1, −1, −1 → −1, −1, −2;
  population +20 higher 336 / 104 (90 Starve) → 341 / 100 (88 Starve); sign flips 15 → 14 (the two T0 = 200
  WAR-1000 SEVERE None pairs no longer flip); per rule: CapBeforeMilitary 546 → 545 identical and ration 67 → 62;
  AllocateBeforeCap 551 → 552 identical and growth 148 → 145; NoCap ration 68 → 67; TradeCredit ration 65 / 7 → 67 / 5.
  Unchanged: 1,649 identical, 943 different, 438 / 0 growth, 0 civilian starvation, 106 army sizes.
- §6.1: WAR-500 BAD Desert (CGM) 421 → 420 soldiers, deserted 86 → 87, fed soldier-years 1,377 → 1,375, gap +20
  +0.08 → +0.07.
- §6.4: GARRISON-1000 None 80.9 → 81.0 % ration and growth 92.1 / 85.3 → 92.3 / 85.4; Desert growth 90.6 / 85.3 →
  90.8 / 85.4; WAR-500 None 32.0 → 32.1 % and growth 79.7 / 31.1 → 79.8 / 31.2; WAR-1000 None march 15.9 → 16.0 and
  growth 73.0 → 73.1; WAR-1000 Desert march 85.8 → 84.4 and growth 75.2 → 75.3 (CGM).
- §6.6 and tables §13: the growth-served and pop +100 cells above, TradeCredit WAR-500 BAD ration 91.8 → 91.6 (CGM),
  bought back per pulse 972 → 973 (WAR-1000 NORMAL CGM), and 181,322 → 181,321 bought back per turn (the 93.0 % stands).
- §8: 16.7–80.9 % → 16.7–81.0 %; growth cut under CGM 0–13.5 % → 0–14.1 %.

**Mutants** (every kill includes an assertion-level semantic test):

| mutant | change | at `b431323` | at the fix commit |
|---|---|---|---|
| M1 | `ImportedForCivilians` never incremented (F2-3) | **SURVIVED** 61/61 | KILLED: `TradeCredit_TheImportSplit_…` ×2 |
| M2 | the buy-back for draws counted as civilian | killed (`TradeCredit_GrowthEaten…`) | KILLED: `…TheImportSplit_…` ×2 + `…GrowthEaten…` |
| M4 | march metric = haul ratio (F2-4) | **SURVIVED** 61/61 | KILLED: `Metrics_AreTheWindowRatios…` |
| M5 | growth metric = ration ratio | **SURVIVED** 61/61 | KILLED: `Metrics_AreTheWindowRatios…` |
| R1 | the leg is the settlement period again (F2-1) | (not applicable) | KILLED: `MarchHaul_IsBilledPerSimYear_…` ×5 (the 21 / 21 case is the same code) |
| R2 | the whole leg bill drawn every period | (not applicable) | KILLED: `MarchHaul_…` ×5 |
| R3 | growth claim ceiled per period (F2-5) | (not applicable) | KILLED: `GrowthClaim_IsIntegratedWithARemainder_…` ×3 + the trade pinned hash |
| R4 | Starve law with h fixed at 21 days | (not applicable) | KILLED: `Starve_IsAPerSimYearLaw_…` ×3 (the 21-d case is the same code) + `Starve_TheDeadZone…` ×2 |

CLEAN: 61/61 at `b431323`, 77/77 at the fix commit (the filtered set), both before the mutants.

**Determinism (fix commit, MEASURED).**

| run | wall time | main CSV SHA-256 | trade CSV SHA-256 | `H-F-pulse.md` SHA-256 |
|---|---|---|---|---|
| Release (`M7Econ.slnx -c Release`) | 68.5 s | `47b67aebef3224d6cc5e1787bb04f622f50ce8f941fdb438b95500456e97cf0c` | `6946b581870fa06307b19269aa4531556240b482d38cf6a64e986b8c1dda272b` | `7696e418…d7a6` |
| Debug | 331.2 s | `47b67aeb…cf0c` | `6946b581…272b` | `7696e418…d7a6` |
| Sim.Core compiled in Release (`Proto.M7.Econ.csproj -c Release`) | 69.5 s | `47b67aeb…cf0c` | `6946b581…272b` | `7696e418…d7a6` |
| F2-1 alone (the round-2 tree with the growth ceiling restored; Release) | 67.6 s | `05061b8f…d634` (= round 1) | `ea6fcc14…fe7d` (= round 1) | — |

- The three generated tables files are identical apart from the wall-time line. `H-F-tables.md` is regenerated from
  the Release run. On the final commit the Release battery was run once more (68.3 s, the same two CSV hashes) and the
  M7Econ suite passed 101/101 in Debug and in Release; `H-F-pulse.md` was regenerated in all three builds after its
  table-C heading was reworded (`7696e418f75c18a5034f095366e4f30ae88290f659ac5435ddbdbe624584d7a6`; before the
  rewording `bf7f3981…95d6`, with identical numbers).
- The last row is the measurement behind "F2-1 moved no battery byte": with only the march-leg change, the main and
  trade CSVs are the round-1 files.

**Tests** (`dotnet test M7Econ.slnx -c <cfg>`; builds 0 warnings and 0 errors; 0 failed, 0 skipped):

| tree | M7Econ Debug | M7Econ Release | M7Proto |
|---|---|---|---|
| `444b85e` (dev baseline) | 34 | 34 | 775 |
| `b431323` (round 1) | 85 | 85 | 775 |
| fix commit (round 2) | **101** | **101** | not re-run: no `Proto.M7*` file and no `Sim.*` file changed (`git diff b431323 --stat` touches only `Proto.M7.Econ*` files and the reports) |

**Bench** (`food-bench`, Release, seed 42, 9 repetitions, tiered JIT off, host load average 2.2–2.4): `Resolve`
78.4 / 64.9 ns per call (CGM / CMG); 300-turn run, CapBeforeMilitary, 11.5 / 11.7 ms; TradeCredit 12.1 / 12.1 ms. Round
1 measured 58.5 / 62.3 ns, 9.3 / 11.3 ms and 11.4 / 10.8 ms at load 1.2–1.7. `Resolve` is unchanged code; the
differences are within the host's load noise (INFERRED: the host load was higher). The hot-path changes are one
remainder accumulator for the growth claim and one per campaigning period for the haul.

**Banned constructs (scan of every file this round touched):** none of `System.Random`, `DateTime`, `float`,
`Parallel`, `Dictionary`/`HashSet`, `GetHashCode` or LINQ; every parse and format call is culture-invariant; wall time
(`TickCount64`, `Stopwatch`) appears only in the CLI and bench paths (the `food-pulse` output carries none).

**Limitations of the fixes:**
- **F2-1:** the haul is now dt-correct in the settlement period, but it still depends on the march leg, which defaults
  to T2's unratified pulse. The lane reports the dependence (table B) and does not choose a leg (open question 9).
  The leg is constant over a run; a march that stops and starts, or a leg that differs by terrain, is not modelled.
- **F2-2:** Desert is unchanged and is not dt-correct. A dt-correct desertion needs a time constant the shipped rules
  do not supply, and the lane does not invent one (open question 10). The direction of its divergence (CMG keeps more
  soldiers) held in all 10 configurations of table D; its size did not.
- **F2-4:** the test pins the meaning of the metrics on one run; the battery's own CSV hash is still pinned only in
  this report.
- **F2-5:** the exact claim is read back from the trace for the test; the trace is debug output and never feeds the run.
