# ADR-025 — BOUNDED MIGRATION: exact flight hazard, exit openness, basin caps, the vacancy bound, and the readout value

**Status: accepted under CR-015; implementation T4.21-2** (and the skips lifted in T4.21-4 under
CR-015 N9). Ruling: `docs/adr/cr-015-famine-is-exceptional.md` §6 (mandate 2026-09-17, items 3–4,
8). Spec: `docs/t4.21-architecture.md` §3.4–§3.5, §3.7–§3.11, §6.3.

**ADR number:** 025 (see ADR-024 for the numbering evidence).

> HOW MANY is separated from WHERE. Per bucket, the fraction that leaves a stressed source in a
> turn is the exactly integrated hazard on the NOMINAL deficit and the best exit's openness,
> `φ = 1 − exp(−profile·K·ω·d·dt)`, never a sum over destinations; shares distribute it. The gap
> channel is bounded at BOTH ends of a basin by the pooled equalisation physics T2.8 already uses
> for a pair. Total inflow into a destination is bounded by its VACANCY in food terms — the same
> relaxation law that bounds births — so VACANCY ≠ ATTRACTIVENESS is literal. The D-037 B1 readout
> carries the same hazard with ω := 1. No constant moves; the overdraw scaler remains the backstop.

---

## §1 WHAT THIS ADR AMENDS (quoted), AND WHAT IT DOES NOT CHANGE

**Amends (CR-015 N4, N5, N6):**

- `docs/adr/adr-012-destination-viability.md:53-63`: *"flight desire remains source-driven
  (`FamineFlightFactor × deficit_source`), uncapped by the gap mechanism, exactly as D-021 ratified.
  Viability only redistributes WHERE the fleeing go"* → flight remains source-driven and
  gap-independent; it is now bounded by exact integration on the best exit; viability GATES and,
  through ω, SCALES the source's hazard; vacancy BOUNDS the destination; shares DISTRIBUTE. "When
  every reachable destination is itself non-viable, flight goes to zero … people die at home" is
  preserved bit-exactly (ω = 0 ⇒ φ = 0). One line appended to ADR-012.
- `Sim.Core/Systems/Migration/MigrationSystem.cs:44-51`: *"the Exit valve is a surge by design,
  bounded by the overdraw scaler alone"* → rewritten in the header: bounded by exact integration;
  the overdraw scaler is the backstop.
- `MigrationSystem.cs:93-95`: *"the collective inflow stays inside the basin — pinned empirically
  by the oscillation regression tests"* → bounded BY CONSTRUCTION at both ends (§2.3); the T2.8
  stabilization doc (a) is rewritten to say so.
- `docs/adr/adr-021-unplaced-departure-demand.md:34` (value row) and `:70-74` (*"MigrationSystem's
  equations … untouched"* — true at T4.4, historical now): the VALUE follows §2.4; the CONDITION
  (`:98-101`, binary) is untouched. One line appended to ADR-021.
- `docs/t4.4-review-record.md:90` (§D2): *"it can be emptied, and that is ratified"* → superseded:
  `DrawParty` floors a readout `< count`, so a source is never emptied in one founding. One line
  appended.
- `docs/t4.12-derivation-record.md:426-428` STOP condition 2: DISCHARGED by CR-015 N5 — the
  strength change is by integration form, ω and the argument, not by re-weighting; the constants and
  the gauge freedom are untouched. Pointer line appended.
- `docs/t4.10-review-record.md:432-436` Option A *"No new channel was added"*: STANDS — ω is the
  NORMALISATION of the existing flight channel over destinations and `vacScale` a destination-side
  scale on the total; neither is a fourth channel; `R_i = LandWeight × arableKm2_i` is untouched.

**Does NOT change:** `BaseRatePerYear` 0.03, `FamineFlightFactor` 8.0, `CohortProfile`,
`GapClosingFraction` 0.25, `DestinationDeficitRepulsion` 1.0, `AttractivenessLandWeight`,
`DampingDecayCostUnits`; viability (`:225-235`, reads NOMINAL `d_j`; `HappinessMigrationTests` and
`DestinationDeficit_StillRepels` pin it); the attractiveness EMA and damping; the T2.8 per-PAIR gap
cap `f·m*_ij` (`:348-382`); the overdraw scaler (`:430-431`, now a backstop); the pinned ascending
transfer order; `ClampToAvailable`; D-004 remainders; `ColonizationSystem` (no line); B1's binary
condition (CR-015 G7(a)); the pipeline position.

---

## §2 MECHANISMS (all from prev; replaces the flight term at `MigrationSystem.cs:400-402` and `:415-417` only)

### 2.1 Source: stress → hazard → bounded outflow → shares

```
per source i:
    d_i  = prev deficit (NOMINAL — CR-015 G3(b) concerns demographics, not flight)
    ω_i  = max_{j≠i} damping_ij × viability_j          // EXIT OPENNESS ∈ [0,1]; 0 ⇒ nowhere to go ⇒ die at home
    Z_i  = Σ_{j≠i} damping_ij × viability_j
    w_ij = Z_i > 0 ? damping_ij × viability_j / Z_i : 0 // WHERE: shares sum to 1
per bucket b of i:
    K    = BaseRatePerYear × FamineFlightFactor          // 0.24/yr — no constant moved
    φ_b  = 1 − exp(−CohortProfile[c_b] × K × ω_i × d_i × dt)   // HOW MANY: per-turn flight fraction ∈ [0,1)
    flight_b→j = φ_b × count_b × w_ij × vacScale_j       // Σ_j ≤ φ_b × count_b ≤ count_b
    desiredTotal_b = Σ_j gap-capped desire_b→j + Σ_j flight_b→j   // overdraw scaler unchanged (backstop)
```

- **Exact integration:** the survival-kernel form the demographics and decay systems already use
  (ADR-011, the ADR-016 family); two dt = 5 steps at constant `(ω, d)` compose to one dt = 10 step
  exactly (`M_Flight_DtComposes`). The Euler form `K·d·dt` let a decade desire 2.4× a bucket per
  destination.
- **HOW MANY separated from WHERE:** `φ_b` depends on the source's deficit and on the BEST exit,
  never on the number of exits; adding a second identical destination leaves `φ_b` unchanged and
  halves each share (`M_DestinationCount_DoesNotMultiplyQuantity`, kills **M-MIG-SUMNOTMAX**).
- **ω = max**, not a Σ-normalisation: keeps ADR-012's "die at home" bit-exact (`φ = 0` iff every
  `damping·viability = 0`; `UnreachablePair_ZeroFlow`, `DeadStaysDead`, `NoStarvingSettlementGains`
  need exactly this) with no 0/0 rule, and "how open is the best exit" is the physical bound on how
  many can leave.
- **Nominal `d` — a continuum from ordinary pressure to crisis flight:** at `d = 0.05`, ω = 0.84 a
  profile-1 bucket loses 9.6 %; at 0.15, 26 %; at 0.40, 55 %; at 1.0, 87 %. Ordinary migration
  pressure (the mandate's "shortage remains meaningful") survives; FAMINE's exceptional response is
  the mortality channel (ADR-026), not a separate flight regime.
- **The old gauge is preserved in the small:** `φ ≈ profile·K·ω·d·dt` for small exponent — the
  pre-amendment expression with Σ_j replaced by max_j. T4.12's ruling is untouched numerically.

### 2.2 Destination (a): viability — CAN `j` accept at all (UNCHANGED, `:225-235`)

`anyFood gate × max(0, 1 − Repulsion·d_j) × happiness shade`, multiplying every flow of both
channels exactly as today.

### 2.3 Destination (b): equalisation bounds on the GAP channel at both ends of a basin

T2.8(a)'s pair cap `f × m*_ij`, `m*_ij = (R_j P_i − R_i P_j)/(R_i + R_j)`, is the pooled-
equalisation flow for the pair; the same derivation for a BASIN gives the aggregate:

```
destination j, basin B_j = { i : gap-capped desire i→j > 0 }   (|B_j| ≥ 2, else skip — bit-identity by branch)
    P_pool = P_j + Σ_{i∈B} P_i,  R_pool = R_j + Σ_{i∈B} R_i
    M*_j^in = max(0, R_j × P_pool / R_pool − P_j)                 // ≡ (R_j P_B − R_B P_j)/(R_B + R_j); equals m*_ij at |B| = 1 ALGEBRAICALLY, not in bits — hence the skip
    destScale_j = inflow_j > f × M*_j^in ? f × M*_j^in / inflow_j : 1.0
source i, basin C_i = { j : gap-capped desire i→j > 0 }         (|C_i| ≥ 2, else skip)
    M*_i^out = max(0, P_i − R_i × P_pool / R_pool)   over C_i ∪ {i}
    srcScale_i = outflow_i > f × M*_i^out ? f × M*_i^out / outflow_i : 1.0
gap desire i→j executed = gapScale_ij × destScale_j × srcScale_i × gapDesire_ij
```

`f = GapClosingFraction`; `R`, `P` the instantaneous physics T2.8 uses — no new constant. Fan-OUT
is in scope (with 11 destinations a source's pair caps sum to up to `(k+1)/2 × f × m*`; measured
6.1–6.4× at Libur t119). In the fed canonical world the gap channel moves ≤ 1 % of a settlement per
decade; these bounds bite on the multi-source refill after a collapse, which is their purpose.

### 2.4 Destination (c): the VACANCY bound on TOTAL inflow — HOW MANY `j` can absorb per turn

```
destination j:
    V_j   = FoodHeadroom.Vacancy(prev, j, cfg)      // ADR-026 §2.1: max(0, N_lim,j − N_j) in adult-equivalents; +∞ when j has no demand row
    cap_j = (1 − exp(−k × dt)) × V_j                // the relaxation law that bounds births; k = ln2/10 per year (ADR-026)
    in_j  = Σ_i Σ_b ( gapExecuted_ib→j + φ_b × count_b × w_ij ) × w_{c_b}   // this turn's desired inflow, ae
    vacScale_j = in_j > cap_j ? cap_j / in_j : 1.0  // one factor on EVERY flow into j, both channels
```

- **Semantic — one law for growth and immigration.** `N_lim,j` is the population `j`'s last-turn
  FEEDABLE food influx sustains; `V_j` its vacancy in food terms; a settlement approaches its
  food-influx limit at rate `k` whether the arrivals are born or walk in. A 10-person settlement on
  tiny land (`ρ = 1.3`, `V = 3`) accepts ≈ 1 person per decade however attractive; a 10-person colony
  on open land (`ρ ≈ 8`, `V ≈ 70`) accepts ≈ 35 per decade, then more as its labour grows — the "empty
  high-capacity destination" fills geometrically, never in one turn.
- **Not an arbitrary cap:** no new constant; `k` is ADR-026's relaxation, `V_j` a derived state
  quantity. `inflow/population` is NOT the bound (a vacant valley may take many times its residents).
- **Refused refugees stay at their source** ("die at home"); no water-filling to other
  destinations in the same turn (single-pass scaling keeps the plan a pure function); they do not
  become `UnplacedDeparture` (CR-015 G7(a)).
- **Absent demand row ⇒ `V_j = +∞`:** every hand rig's destination is bit-identical, and a
  settlement's FIRST turn (founding turn 1; a colony's first turn) accepts unboundedly for that one
  turn (stated; spec §9 R12). A destination with `d_j > 0` has `V_j = 0` in general ⇒ accepts nobody
  — consistent with repulsion, and it closes the ping-pong (`queue.md:65`) at the destination end.
- **Order of scales is pinned:** pair → destination basin → source basin → vacancy → overdraw; each
  a pure function of prev, so the whole plan is computable before the transfer loop.

### 2.5 D-037 B1 readout (ADR-021) — value follows, condition does not

Condition (`:320-333`) UNCHANGED. Value (`:338-341`) becomes the DESTINATION-FREE hazard:

```
UnplacedDeparture_b = (1 − exp(−CohortProfile[c_b] × K × d_i × dt)) × count_b      // ω := 1, nominal d
```

Under B1's own condition `ω_i = 0`, so reusing `φ_b` verbatim would write 0 and kill colonization;
`ω := 1` is exactly ADR-012's "flight desire remains source-driven; viability only redistributes
WHERE" and ADR-021's "structurally absent, not discarded" quantity. At `d = 0.40` a profile-1
bucket's readout is 0.617 of the bucket (today 0.96); at 0.90, 0.885 (today clamped 1.0) — parties
shrink by roughly a third at the rigs' deficits; every `ColonizationTests` rig (`:114, :130, :144,
:287-288, :318, :358, :531`) is RE-MEASURED in T4.21-2 before the cut, and where per-bucket flooring
makes a founding vacuous the RIG's buckets are re-sized, never the readout. `ColonizationTests
:184-211` (party == Σ floor(readout)) holds by construction.

### 2.6 The `Plan` refactor (one implementation)

`MigrationSystem.Plan(prev, cfg, dt)` — a PUBLIC static returning the per-settlement plan (ω, φ at
profile 1, flight out, per-destination channel totals, `f·M*` caps, `cap_j`, `vacScale`) that
`Step` ITSELF consumes (`Step = Plan + transfer loop`; the arithmetic order of every product
preserved so the goldens pin the refactor). Landed as its own bit-identical commit BEFORE the
mechanism commit (ADR-021 precedent; spec §9 R2). The observer RECOMPUTES from it; no schema, no
observer-side formula copy. The `MigrationSystem` ctor gains `BasketBook` (precedent
`ClassMobilitySystem`) for `FoodHeadroom`.

---

## §3 CONSTANTS

| constant | value | status | null / identity arm |
| --- | --- | --- | --- |
| `K = BaseRatePerYear × FamineFlightFactor` | 0.03 × 8.0 = 0.24/yr | UNCHANGED (T4.12 ruling; the gauge freedom untouched) | — |
| `f = GapClosingFraction` | 0.25 per TURN (a discrete-feedback stability fraction, T2.8) | UNCHANGED; reused for the basin caps | `|B| < 2` / `|C| < 2` skips ⇒ bit-identical single-pair basins |
| `k` (vacancy relaxation) | ln 2 / 10 ≈ 0.0693/yr | ADR-026's `headroomRelaxationPerYear` — ONE law, ONE constant | absent demand row ⇒ `V = +∞` ⇒ `vacScale = 1.0` literal |
| new constants | **none** | | |

---

## §4 dt-CORRECTNESS, DETERMINISM, BIT-IDENTITY

- **dt (law 3):** `φ` and `cap_j` are exact integrals of per-year rates; one dt-10 == two dt-5 at
  held `(ω, d)` and on a held-`N_lim` rig within flooring (`M_Flight_DtComposes`). `f` is per TURN by
  inheritance (T2.8) and stated as such.
- **Determinism (law 5):** ω is a `max` over a table-ordered scan (order-independent); shares and
  scales are pure functions of prev; no new RNG; the pinned ascending transfer order is untouched;
  every guard is `if (scale < 1.0)` so the fed path executes today's instruction sequence.
- **Bit-identity ledger:**

| regime | identical? | why |
| --- | --- | --- |
| source with `d = 0` | YES | `φ = 0` (today's desire is also 0); ω is only a factor of the flight term |
| destination without a demand row (every hand rig; turn 1; a colony's first turn) | YES | `V_j = +∞` ⇒ `vacScale = 1.0` literal ⇒ guarded |
| destination with a demand row and `cap_j ≥ in_j` (every fed canonical destination: `cap ≈ 15 % N`/decade vs gap inflow ≤ 1 %) | YES | `vacScale = 1.0` literal ⇒ guarded |
| gap channel, single-source and single-destination basins | YES | `destScale = srcScale = 1.0` by the skips; pair caps untouched |
| gap channel, multi-source or multi-destination basins | NO (intended) | basin caps bite |
| any source with `d > 0` | NO (intended) | `φ` differs from the linear response wherever `d > 0` |
| destination receiving refugees beyond `cap_j` | NO (intended) | the vacancy bound bites |
| `Plan` refactor alone | YES | its own commit, proven by the strip control before the mechanism lands |

---

## §5 S8 §4.1 ITEMS FOR THIS PIECE

- **Audit:** `BaseRatePerYear`, `FamineFlightFactor`, `CohortProfile`, `GapClosingFraction`,
  `DampingDecayCostUnits`, `DestinationDeficitRepulsion`, `AttractivenessLandWeight` (0.078125
  persons/km² — an attractiveness scale 5–10× below the canonical density corridor, so `R < P`
  everywhere and land vacancy `R_j − P_j` is NEVER a capacity; rejected form), `SettlementDistanceRow.
  TravelCost`, `EffectiveArableKm2`, `cohortWeights` (for `in_j` in ae).
- **Dimensional declaration:** `ω`, `w_ij`, `φ_b`, `f`, `gapScale`, `destScale`, `srcScale`,
  `vacScale` dimensionless; `K`, `k` per sim-year; `V_j`, `cap_j`, `in_j` adult-equivalents; `M*` raw
  persons (R/P physics); `count`, `flight`, `party` heads; `R_i` persons-equivalent of land.
  Checks: `φ = 1 − exp(−[1]·[1/yr]·[1]·[1]·[yr])` ✓; `cap_j = (1 − e^{−[1/yr][yr]})·[ae]` ✓;
  `in_j = Σ [head]·[ae/head] = [ae]` ✓; `M*_j^in = [R]·[P]/[R] − [P] = [P]` ✓; `flight = [1]·[head]·
  [1]·[1]` ✓.
- **Corridor independence:** no new corridor, no new constant. `migrationGrossPerDecade` (dev and
  canonical, quarantined "accepted as measured"): direction NOT pre-committed — reported; the
  below-floor tooth (`CalibrationBatteryTests.cs:236-239`) and the dev drift tooth become recorded
  envelopes under CR-015 N7. `MalthusLite` / `FamineAtOneOfTwelve` starvation control: bounded flight
  ⇒ more stay ⇒ the 8 %-in-40-turns control may now cross — re-derived in T4.21-4.
- **Coupling map:** CR-015 §4 rows 4–6.

---

## §6 TESTS THAT PIN EACH PROPERTY (spec §6.3)

`M_Flight_BoundedPerTurn` (kills **M-MIG-CAP**: Euler `K·d·dt` restored ⇒ moved > 950 of 1000) ·
`M_DestinationCount_DoesNotMultiplyQuantity` (kills **M-MIG-SUMNOTMAX**) · `M_EmptyRuin_StillRefused`
· `M_VacantViableDestination_AcceptsUpToCap` (`V = 70`, `cap = 35` ⇒ inflow == floor(35/w̄); kills
**M-MIG-VACANCY**: 860) · `M_Basin_FanIn_Flight_BoundedByVacancy` (4 famine sources → one `V = 70`
destination; refused people remain at their sources; kills **M-MIG-VACANCY**: 3440) ·
`M_Basin_FanIn_AggregateInflowBounded` (kills **M-MIG-FANIN**; with 1 source bit-identical to
`GapCap_PairGrossFlow`) · `M_Basin_FanOut_AggregateOutflowBounded` (kills **M-MIG-FANOUT**) ·
`M_NoViableExit_NobodyLeaves` · `M_SmallSettlement_FlooringAndRemainders` (3-, 7-, 20-person sources;
conservation exact; never negative) · `M_Readout_ValueTurnExact` (turn-exact; a Stress-stranded
source founds at ordinary magnitude) · `M_Flight_DtComposes` · `Overdraw_ScalesProportionally`
re-rigged to assert shares (moved == floor(1000·φ), φ ≈ 0.860 BY CONSTRUCTION; aim kept — no
first-destination grab; ADR-015 §7.2 aim check by the verifier) · `M_Recovery_FlowsStopWhenDeficitClears`
· every `ColonizationTests` rig re-run + `Colonization_PartyNeverEmptiesSource` · `M_Plan_EqualsStep`
· `CollapseStabilityTests` (T2.13) kept green AND re-verified to bite · the mandate's two mutation
requirements (item 8) are **M-MIG-CAP** and **M-MIG-FANIN/M-MIG-VACANCY**. Every ordering over
doubles in the new code uses `(score, id)` composite keys; there is no new sort (ω is a max, shares
are table-ordered).
