# D-049 — TAXATION AS CONTINUOUS PRESSURE, SEGMENT REVOLT, THE A3 TAX GATE, REVOLT-AGE INHERITANCE

**Status:** DIRECTOR RULINGS, **RATIFIED 2026-10-05** (M5 hardening instruction §4–§9, §17, §18), and the record of
the model that implements them (stream H2, branch `m5h-h2-tax-revolt`).
**Number:** D-049 was checked free on every local and remote ref (`git fetch`; tree and `git grep` of every
`origin/*` ref) before it was minted here.
**Supersedes by pointer, rewrites nothing.** The records it supersedes keep their text; each carries a dated pointer
note appended on 2026-10-05 (§10).

Labels used below: **MEASURED** (a number read off a run on this branch, the command named), **DERIVED** (computed
from measured numbers by the stated arithmetic), **CHOSEN** (a value picked within a measured frame, the frame
stated), **INFERRED** (an implementation choice the Director's text does not fix; listed in §11 for ratification).

---

## 1. The rulings (RATIFIED, Director 2026-10-05)

These are the Director's §18 statements, verbatim in substance, with the §4–§9 detail they carry.

1. **Taxation.** Taxation is a continuous welfare/governance pressure, not an instant-revolt switch. Higher
   taxation gradually reduces happiness and legitimacy and raises grievance/unrest pressure; the effects accumulate
   over multiple turns. **The earlier proposal "100 % tax ⇒ immediate/guaranteed revolt" is REJECTED.** There is no
   `tax ≥ X → revolt` rule.
2. **Extreme taxation.** 100 % tax is permitted as an extreme player action but does not directly or instantly
   revolt the population. 99 % does not inherently mean revolt; sustained extreme taxation should eventually create
   serious instability; local prosperity delays or reduces it.
3. **Revolt.** Revolt emerges after accumulated grievance/unrest/happiness deterioration reaches LOCAL tipping
   conditions — delayed, driven by accumulated conditions, sensitive to local conditions. No simplistic fixed rule
   ("after 5 turns at 100 %, 20 % revolts"); thresholds calibrated from the existing model, not invented.
4. **Population impact.** A revolt affects a population segment, not automatically the entire settlement; it can
   affect only a fraction of the settlement/population.
5. **Local conditions.** Food, amenities, housing, comfort goods, services, development and institutions/state
   capacity can mitigate tax pressure; a wealthy, developed, well-served settlement sustains significantly higher
   taxation than a poor one. Poor settlements must accumulate GREATER grievance from the same rate — gradually,
   never automatically revolting.
6. **Taxation Age.** Taxation is an A3 (Bronze Age) capability and cannot be operational before A3. The Taxation
   node stays in Civics with its prerequisites; the AI, hand-built orders, save/load, replay and the UI all respect
   the one legal predicate.
7. **Revolt Age.** A civilization created by revolt inherits the parent's CURRENT Age. It receives completed
   research, no research progress, no Eureka credit, no capital, and researches at the normal rate; then develops
   independently. "Parent A5 → revolt → child A1 with A5 knowledge" must not occur. The final settlement still
   cannot revolt away.
8. **Knowledge persistence (§9, restated, unchanged).** Completed research is copied on revolt; progress and Eureka
   credit stay with the parent; completed knowledge never decays; annexation (when it exists) merges by union.
   These are D-048 rulings 1–8 and stand as written.

---

## 2. The implemented model (IMPLEMENTED at this branch's head)

### 2.1 The stock: the levy's grievance per population segment

A **population segment** is one class in one settlement — the D-010 bucket that already carries its own needs
grievance. Each segment holds a second stock, **the levy's grievance T** (`TaxGrievanceRow(Settlement, Class,
Value)`, table `TaxGrievances`, **schema v32**), owned by `NeedsGrievanceSystem` and integrated per year with
`dtYears` by the same `StepGrievance` arithmetic as the needs grievance:

```
T_next = StepGrievance(T_prev, accrual, decay, dt)
accrual = W × (1 − S_levy)                         (per year)
decay   = base + generational turnover + protestDischargePerYear × p_segment     (per year)
```

`S_levy` is the D-035-B aggregate of the class's bound needs in which **every need but Dignity is held at its
expectation (1)** and Dignity sits at its felt satisfaction — the shortfall the levy ALONE causes, through the
ratified aggregation, Tier-A gate rule and weights (`NeedsGrievanceSystem.LevyAccrualPerYear`). It is never diluted
by the segment's other shortfalls and never zeroed by the Tier-A collapse of a destitute class's upper needs — the
R2b attribution did both, which is why "a poorly-off settlement never protests the levy" (baseline §5.5 G4).

Rows appear only where a levy has been felt; **an untaxed world holds none**, and every reader below is then
exactly neutral. Empty classes and extinct settlements hold 0 (the T2.13 ghost-grievance rule). Read by the
simulation only through `State/Unrest.cs` (the read-isolation gate covers the new names).

### 2.2 What the segment feels: the burden and its offsets

```
felt = r × (1 − taxBurdenOffsetMax × P) × (1 − taxServiceOffsetMax × V)       (State.Unrest.FeltBurden)
Dignity satisfaction = 1 − felt                                               (NeedsGrievanceSystem.DignitySatisfaction)
```

- `r` — the settlement's EFFECTIVE rate: declared rate × the ruler's stored administrative reach there
  (`Governance.EffectiveTaxRate`, unchanged).
- `P` — the class's PROVISION: the D-035-B CES of its other bound needs (Sustenance = food, Shelter = housing,
  Comfort = comfort goods/amenities). R4a's offset, `taxBurdenOffsetMax` = 0.5, unchanged.
- `V` — PUBLIC SERVICES, DEVELOPMENT, INSTITUTIONS AND STATE CAPACITY (`State.Unrest.ServiceOffset`):
  `V = reach × (1 − e^(−X))`, `X` = completed public works at the settlement (goods.json projects that found no
  institution: granary, workshop) + the maturity of the institutions it hosts (universities); reach =
  `ControlRow.Strength` (state capacity; roads raise it). `1 − e^(−X)` is ADR-028's saturating effect curve
  (`InstitutionEffects.Saturation`). `taxServiceOffsetMax` = 0.25 (§5.3).

Coefficients inside the resolution equation (law 2): none acts without a levy, each scales the felt burden.

### 2.3 What T does, per segment and per settlement

With `o` = `protestOnsetGrievance` = 12 and `U` = `uprisingGrievance` = 20 (§5):

| reading | definition | where |
|---|---|---|
| protest `p` | `clamp((T − o) / (U − o), 0, 1)` | `Unrest.ProtestOf` |
| segment RISEN (its tipping point) | `T ≥ U` and the class has members | `Unrest.IsSegmentRisen` |
| rebel fraction `q` | `clamp((T − U) / (U − o), 0, 1)` — the portion of the segment in open revolt | `Unrest.RebelFractionOf` |
| settlement levy pressure | population-weighted `min(1, T / U)` over its segments | `Unrest.LevyPressure` |
| settlement rebels | `Σ share × q` | `Unrest.RebelShare` |
| settlement UPRISING | rebels > `uprisingPopulationShare` (0.5) of its people | `Unrest.IsUprising` |

Consequences, each exactly inert where no levy has ever been felt (output's drag also ends the turn a levy is cut,
since its carrier is the levy; the pressure on welfare decays with the stock):

- **Output** (`ProductionSystem`, via `Governance.OutputMultiplier` = extraction response × `Unrest.OutputFactor`):
  realised production ×
  `(1 − r × Σ share·[protestOutputDragMax·p·(1 − q) + q])` — protesters withhold part of the levied work in
  proportion to their protest (`protestOutputDragMax` = 0.5, unchanged), rebels withhold all of it.
- **Happiness and legitimacy** (`SettlementHappiness.Of`, `Governance.Legitimacy`): the provision reading ×
  `(1 − levy pressure)` (`SettlementHappiness.TaxSufficiency`). Happiness falls as pressure builds and recovers as it
  decays; **the edict alone moves nothing** (ADR-033 D4's `× (1 − r)` is the superseded first form).
- **Discharge** (D-021 valve 1): `protestDischargePerYear × p` adds to the segment's decay (and the population-
  weighted protest to the settlement's needs-grievance decay), unchanged at 0.005.
- **Revolt** (`RevoltSystem`): a settlement throws off its ruler when (a) its PROVISION reading is 0 (total
  deprivation — `SettlementHappiness.IsRevoltReady` now reads `Provision`, which the tax never touches) or (b) it
  is in UPRISING. A ruler's final settlement never revolts away (D-048 ruling 5). A rebel MINORITY is the segment's
  own revolt: its levied labour is withheld, nobody else's, and the settlement stays its ruler's.

There is **no tax → revolt rule anywhere**. The old "second corner" — a declared 100 % levy at full reach read
happiness 0 and was revolt-ready the next turn (`GovernanceTests.AFullLevyAtFullReachIsTotalExtraction_
TheSecondRevoltCorner`) — is removed; its pin is replaced by `AFullLevyAtFullReach_IsNoLongerARevoltCorner_H2`,
carrying a dated note.

### 2.4 Why a new stock (and a schema change) rather than re-reading the existing grievance

The R2b design attributed a share of the ONE needs grievance to the levy, re-computed each turn from current
shortfalls (baseline §5.5 G4, G5). No share rule can be both undiluted for a poor segment (G4) and gradual (the
share jumps with the shortfall mix every turn). A segment's own levy stock is both, and it is the D-010 "grievance
stock whose source is taxation pain" the Spine already names. Its cost is one serialized table; on every untaxed
world (all four goldens) it is empty, so the goldens move by one 4-byte count prefix only (§8).

---

## 3. The tax Age gate (ruling 6) — IMPLEMENTED

- `sim.json governance.taxationMinAge` = **3** (content, not code: no node id and no Age literal in C#). The loader
  refuses a value outside the Ages content.
- `Governance.CanLevyTax` = `GateOf(...) == TaxGate.Open`; `GateOf` returns the first unmet condition:
  `Inert` (no governance or research content) → `NeedsKnowledge` (`KnowsTaxation`: sim.json
  `governance.taxationRequires` = the Civics node `taxation`, evaluated by the existing knowledge evaluator) →
  `NeedsAge` (`MeetsTaxationAge`: the polity's CURRENT Age ≥ taxationMinAge; a declared minimum with no Age content
  FAILS CLOSED) → `Open`.
- Every caller shares the one predicate: `GovernanceSystem` (orders judged on PREV — an edict issued in the same
  turn as the AdvanceAge to A3 is refused, the next turn's is applied: `TaxAgeGateTests.
  AnEdictIssuedWithTheAdvance_IsRefused_TheNextTurnsIsApplied_TurnExact`), the AI valve (`AiGovernance`; the AI
  research policy reads `KnowsTaxation` so it still researches the node before A3), `AvailableActionsQuery` / the
  UI (which names the Age the levy waits for), and hand-built orders. Save/load and replay carry it because the Age
  is serialized state (`AgeStates`) and the predicate reads nothing else new
  (`TaxAgeGateTests.TheGate_SurvivesSaveLoad_AndReplaysExactly`).
- The Taxation node is unchanged: still in Civics, same prerequisites; researching it in A1/A2 is knowledge, not the
  capability.
- **AI first levy, re-measured** (§7): the AI no longer taxes while Neolithic (baseline §1.6: turns 369–376 in A2).

## 4. Revolt-Age inheritance (ruling 7) — IMPLEMENTED

`RevoltSystem.InheritAge`: the revolt-born polity receives an `AgeStateRow` with its parent's CURRENT Age (read from
PREV, the same instant the knowledge copy is taken) and surge emphasis; `EnteredTurn` and `SurgeStartTurn` are the
child's first turn (the first state that shows it in that Age), not a date it never lived. No `AgeTransitionRow` is
logged (no transition was made) and no unit is converted. A founding-Age parent writes nothing (absence of a row IS
the founding Age). D-048 rulings 1–6 hold unchanged alongside. `RevoltSystem` now writes `AgeStates` (a sanctioned
shared table, `SystemCatalog`). Tests: `RevoltAgeInheritanceTests` — parent A5 → child A5 with the parent's
knowledge and nothing else; every inherited Age (2, 3, 9); founding-Age parent; persistence across later steps and
the child's own tax gate; the final settlement still cannot revolt away; save/load and replay.

---

## 5. Thresholds: how each number was obtained

### 5.1 The calibration measurement (MEASURED)

`TaxPressureCalibrationProbe.NaturalPlateaus` (skipped; run manually): the CANONICAL founded world (seed 42,
`WorldFounding.Found`, the canonical era table), the player's capital as founded ("natural": nothing touched),
crafts known, A3 entered, a levy from turn 0, 60 turns. The capital's population-weighted levy grievance T:

| world | rate | T turn 10 | T turn 20 | T turn 40 | T turn 60 | felt (turn 6) |
|---|---|---|---|---|---|---|
| canonical | 40 % | 4.99 | 5.79 | 6.23 | **6.16** (plateau) | 0.215 |
| canonical | 70 % | 10.41 | 11.73 | 12.66 | **12.59** (plateau) | 0.375 |
| canonical | 99 % | 17.29 | 19.01 | **22.51** | 0.36 (the capital rose and was lost; the stock decays under its new ruler) | 0.528 |
| canonical | 100 % | 17.55 | 19.51 | 6.56 (lost) | 0.14 | 0.534 |
| dev | 40 % | 4.89 | 6.14 | 6.40 | 6.30 | 0.218 |
| dev | 70 % | 10.21 | 12.73 | 13.00 | 12.85 | 0.377 |
| dev | 99 % | 17.24 | 21.92 | 1.23 (lost) | 0.03 | 0.532 |
| dev | 100 % | 17.57 | 23.51 | 0.66 (lost) | 0.01 | 0.537 |

### 5.2 The two thresholds (DERIVED from 5.1)

- **`protestOnsetGrievance` = 12 (DERIVED)** — R2b's own rule carried to the new scale: twice the plateau of the
  AI-ceiling levy (40 %), so normal taxation never protests in an ordinary settlement (2 × 6.16 = 12.3, rounded
  down).
- **`uprisingGrievance` = 20 (CHOSEN within a DERIVED frame)** — the level at which a segment's welfare is fully
  consumed (`happiness × (1 − min(1, T/U))`). Fitting the long-run pressure `T/U` on the ordinary capital to
  ADR-033 D4's RATIFIED long-run reading `r` (happiness × (1 − r)) by least squares, `U = Σ T² / Σ T·r`, gives
  **17.4** on the two stable plateaus (40 %, 70 %: (6.16² + 12.59²) / (6.16·0.4 + 12.59·0.7)) and **21.0** with the
  99 % pre-rising level 22.51 added. 20 lies inside that frame. So in the long run an ordinary place reads close to
  what D4 ratified — but reaches it gradually, a prosperous or served place reads better, a poor one worse, and the
  ordinary capital reaches its tipping point only under sustained near-total exaction (99–100 %), never at 70 %.
  The fit is circular in one respect, stated plainly: protest's discharge depends on the thresholds, so the 99 %
  level is measured with them in place; the two stable plateaus (below the onset's discharge) are not.
- **The rebel fraction** spreads over the same span `U − o` over which the segment's protest grew — an identity,
  not a second knob.

### 5.3 The CHOSEN values

- **`taxServiceOffsetMax` = 0.25** — half of `taxBurdenOffsetMax`: what the state returns through its works offsets
  the levy less directly than what the population itself holds. One granary at full reach (V = 0.63) lightens the
  felt levy by 16 %; granary + workshop + a mature university (V = 0.95) by 24 %; a fully provided AND fully served
  segment feels ~0.38 of the levy. **0.5 was measured and REJECTED** (MEASURED, dev world, natural capital plus ONE
  granary, 80 turns, dt 10): at 0.5 one cheap granary (40 timber, 20 stone) holds the capital under a permanent
  99 / 100 % levy at the protest onset (peak T 12.47 / 12.61 — total exaction bought off by one building); at 0.25
  the same capital stays in sustained heavy protest (peak T 16.22 / 16.45, protest ≈ 0.53–0.56, output dragged)
  without rising. Either way one granary keeps an ordinary capital from rising under total exaction — the ordinary
  capital's equilibrium sits just above the tipping point (§6.1: peak 25.7) — which is the ruling's "local
  prosperity should delay/reduce that instability"; 0.25 keeps the instability serious.
- **`uprisingPopulationShare` = 0.5** — the majority. D-010's ladder: a riot is local and of a faction; an uprising
  sets up parallel authority, which a minority cannot do (D-037 A3's control relation is the settlement's, so the
  settlement changes hands only when the rebels carry it). Not a tuned value.
- **Unchanged:** `taxBurdenOffsetMax` 0.5, `protestOutputDragMax` 0.5, `protestDischargePerYear` 0.005 (R2b/R4a,
  their frames in needs.json).

### 5.4 The no-instant-revolt bound (DERIVED, pinned)

The largest accrual the aggregation can charge the levy alone (Dignity at the satisfaction floor, every other need
met), times one 10-year turn (the longest dt the game has), stays below `U`:
`TaxPressureTests.C_FullLevy_NeverRevoltsImmediately_TheEarliestRisingIsBoundByTheAccrual` computes it with the
system's own `LevyAccrualPerYear` and asserts it. So from a standing start no segment can rise within one turn of
felt levy at any dt ≤ 10, the earliest rising is turn 3 (the edict lands in turn 1's state, is first felt in turn
2) and the earliest change of hands turn 4 — whatever the rate.

---

## 6. Measured behaviour (MEASURED)

`TaxPressureMeasurement.TaxPressure_ByCondition_AndRate_AndRecovery` (skipped rig; run manually, output
`h2-tax-pressure.txt`). The founded DEV world (seed 42, four settlements, one player Empire holding all), crafts
known, A3, a levy declared at turn 0 (in force from turn 1), the CAPITAL (reach 1.0) read each turn, 80 turns.
Conditions:

- **WELL-OFF** — every stock the capital holds topped up to twice its population each turn, housed with room to
  spare, a granary and a workshop standing (provision AND services);
- **SERVED** — as founded plus a granary and a workshop (services alone);
- **NATURAL** — as founded;
- **POOR** — housed for a quarter of its people each turn, no public works.

### 6.1 dt 10 years per turn (the canonical era table at founding — the FASTEST accumulation per turn)

`felt` = the felt burden at turn 2; turns are the first turn the reading holds; "lost" = the capital changed hands.

| condition | rate | felt | first protest | first segment risen | lost | peak T | peak rebels | min output × |
|---|---|---|---|---|---|---|---|---|
| well-off | 0 % | 0.000 | — | — | — | 0.0 | 0.000 | 1.000 |
| well-off | 40 % | 0.163 | — | — | — | 4.4 | 0.000 | 1.120 |
| well-off | 70 % | 0.286 | — | — | — | 8.7 | 0.000 | 1.210 |
| well-off | 99 % | 0.405 | 11 | — | — | 13.5 | 0.000 | 1.180 |
| well-off | 100 % | 0.409 | 11 | — | — | 13.7 | 0.000 | 1.170 |
| served | 40 % | 0.176 | — | — | — | 4.8 | 0.000 | 1.120 |
| served | 70 % | 0.309 | — | — | — | 9.6 | 0.000 | 1.210 |
| served | 99 % | 0.437 | 10 | — | — | 14.8 | 0.000 | 1.079 |
| served | 100 % | 0.441 | 10 | — | — | 15.0 | 0.000 | 1.063 |
| natural | 40 % | 0.225 | — | — | — | 6.4 | 0.000 | 1.120 |
| natural | 70 % | 0.394 | 14 | — | — | 13.1 | 0.000 | 1.153 |
| natural | 99 % | 0.557 | 5 | 15 | 26 | 26.8 | 0.847 | 0.301 |
| natural | 100 % | 0.563 | 5 | 14 | 22 | 25.7 | 0.714 | 0.294 |
| poor | 40 % | 0.332 | — | — | — | 10.6 | 0.000 | 1.120 |
| poor | 70 % | 0.582 | 5 | 14 | — | 20.5 | 0.060 | 0.761 |
| poor | 99 % | 0.823 | 3 | 4 | 5 | 31.1 | 1.000 | 0.176 |
| poor | 100 % | 0.831 | 3 | 3 | 5 | 32.2 | 1.000 | 0.098 |

(Untaxed rows of served/natural/poor read exactly as well-off 0 %: no row, every reader neutral. Output above 1 is
ADR-033 D4's extraction response, `1 + 0.3 r`, unchanged; protest and rebels take it back.)

The ordinary capital at 100 %, turn by turn (happiness, legitimacy of the whole realm, segments: c1 peasants, c2
artisans, `R` = risen with its rebel fraction):

| turn | T | protest | rebels | output × | happiness | legitimacy | held | segments |
|---|---|---|---|---|---|---|---|---|
| 1 | 0.0 | 0.000 | 0.000 | 1.300 | 100.0 | 100.0 | Y | c1 0.0 |
| 2 | 4.3 | 0.000 | 0.000 | 1.300 | 78.5 | 93.4 | Y | c1 4.3 |
| 3 | 7.5 | 0.000 | 0.000 | 1.300 | 62.3 | 80.3 | Y | c1 7.5 |
| 5 | 12.3 | 0.041 | 0.000 | 1.273 | 38.4 | 60.6 | Y | c1 12.3 |
| 10 | 17.6 | 0.696 | 0.000 | 0.848 | 12.2 | 36.6 | Y | c1 18.0, c2 12.2 |
| 20 | 23.5 | 1.000 | 0.438 | 0.365 | 0.0 | 28.1 | Y | c1 23.5 R0.44, c2 22.6 R0.32 |
| 40 | 0.7 | 0.000 | 0.000 | 1.000 | 96.7 | 97.6 | N (lost at 22) | c1 0.7 |

The poor capital at 70 % (a rebel minority that never carries the settlement): protest from turn 5, both segments
past their tipping point from turn 14, rebels 3–6 % of the people from then on, output ×0.77, held for all 80 turns.

### 6.2 dt 5 years per turn (flat era — the Bronze/Iron band, where A3 taxation actually begins)

| condition | rate | first protest | first segment risen | lost | peak T | peak rebels |
|---|---|---|---|---|---|---|
| well-off | 99 % / 100 % | 22 / 21 | — | — | 13.5 / 13.6 | 0 |
| served | 99 % / 100 % | 19 / 18 | — | — | 14.6 / 14.8 | 0 |
| natural | 70 % | 27 | — | — | 13.0 | 0 |
| natural | 99 % / 100 % | 10 / 10 | 27 / 25 | 46 / 39 | 24.9 / 26.3 | 0.611 / 0.793 |
| poor | 70 % | 8 | 29 | — | 20.4 | 0.048 |
| poor | 99 % / 100 % | 4 / 4 | 6 / 6 | 8 / 8 | 27.3 / 28.2 | 0.911 / 1.000 |

40 % raises no protest under any condition at either dt. The plateaus are dt-invariant (dt-correct integration);
the turn counts scale with dt, as a per-year model must.

### 6.3 Recovery (cutting the levy to 0 %; dt 10)

| arm | at the cut | +1 turn | +2 | +5 | +10 | +20 | +40 |
|---|---|---|---|---|---|---|---|
| natural 99 %, cut at 10: T / happiness | 17.2 / 13.8 | 17.8 / 11.2 | 14.2 / 29.2 | 7.8 / 61.0 | 3.0 / 84.8 | 0.5 / 97.7 | 0.0 / 99.9 |
| well-off 99 %, cut at 20 | 13.3 / 33.7 | 13.3 / 33.5 | 10.9 / 45.6 | 6.2 / 69.2 | 2.4 / 88.1 | 0.4 / 98.2 | 0.0 / 100.0 |
| poor 70 %, cut at 10 | 19.0 / 4.8 | 19.3 / 3.5 | 15.1 / 24.5 | 8.3 / 58.6 | 3.2 / 84.0 | 0.5 / 97.6 | 0.0 / 99.9 |

The cut stamped turn t is in force from t+1 (GovernanceSystem on PREV), so the stock still rises at t+1 and falls
from t+2 on, every turn; recovery takes turns (memory) and it comes. (At dt 5 the same arms recover over twice the
turns: natural 99 % cut at 10 reads happiness 88.8 at turn 30.)

### 6.4 What the tables show against the rulings

- **Normal tax → manageable (A):** 40 % never protests, never rises, anywhere; happiness dips (natural 68, poor 47
  at the plateau) and holds.
- **High tax → progressive (B):** 70 % on an ordinary place deteriorates turn after turn, protest only after 14
  turns (140 years), no rising.
- **100 % → never immediate (C):** no segment rises before turn 3 and no settlement falls before turn 4 under ANY
  condition (bound §5.4); happiness one turn after the edict is far from 0.
- **Sustained extreme → instability through stages (D):** ordinary capital at 100 %: protest (5) → segments past
  their tipping point (14) → a growing portion in revolt, output down to ×0.29 → the rebels carry it (22).
- **A segment, not the whole (E):** the poor capital at 70 % holds a rebel minority (3–6 % of its people) from
  turn 14 to the end of the 80-turn run without falling.
- **Prosperity mitigates (F):** well-off < served < natural at every rate; well-off and served never rise even at
  100 %; the well-off capital settles at happiness 32 with mild protest under a permanent 100 % levy.
- **Poverty aggravates (G):** the poor capital accumulates more at every rate and rises/falls sooner — still
  gradual (never in the first two turns).
- **Recovery (H):** §6.3.

---

## 7. AI and the A3 gate in the full pipeline (MEASURED)

**The ci.yml AI-empire leg, run locally verbatim** (`sim run --founded --seed 42 --turns 600 --ai-empires 1 --orders
mix-orders.bin`, the player's sample orders merged with the AI's; two processes, `sim replay`, `sim inspect`):

| turn | the AI's order (actor 2, from the emitted run log) |
|---|---|
| 239 | AdvanceAge → A2 |
| 465 | AdvanceAge → A3 |
| 466 | SetTaxRate 5 % — its first A3 turn (before H2: 369–376, while still in A2; baseline §1.6) |
| 467–473 | SetTaxRate 10 → 40 % (the AI ceiling, unchanged) |

- The levy is first FELT in the state of turn 468 (edict 466, in force in the state of 467, read on PREV by the
  needs system the turn after) — the turn-exact delivery chain.
- Over the remaining 132 turns the AI's levy pressure peaks at 0.253 (its capital, turn 596: happiness 74.7, the
  lowest of any AI-held place in the run) — a population-weighted levy grievance of about 5, well under the protest
  onset 12 (the 40 % ceiling is "normal tax", ruling A); **0 control-row losses over 600 turns** (`sim inspect --answer polities`). Final hash
  `37a43349a67fbfc168d48c0590b8940cb622c8d3b8963142b99e1b557506ee92`, identical in both processes and the replay;
  `reproduction VERIFIED: 601 turns, hash-for-hash`; run log 194 orders, kinds 3–8.
- In the suite (`AiEmpireIntegrationTests`, no player orders): the AI enters A3 at 463 (decided 462) and levies
  463/464, never before A3; the control `WithTheTaxAgeGateStripped_TheAiLeviesAtThePreH2Turn_InA2` strips the Age
  half and returns the pre-H2 367/368 — the Age gate is the entire cause of the move.

---

## 8. Goldens (MEASURED on this branch)

No golden run levies a tax, so the new table is EMPTY in all four and the entire movement is one four-byte zero
count prefix. On an untaxed world the old revolt rule and the new model are the same function (effective rate 0,
no levy row, pressure 0, happiness × 1.0, output × 1.0, Dignity 1.0, provision reading = old happiness reading), so
the old-rule twin of each run is the run itself; `IntegratedPinAttributionTests.*_MovedForTheV32Levy*` strips the
(asserted absent) rows and drops the prefix and returns each OLD pin byte for byte; every older layer control strips
the v32 layer first and is unmoved.

| pin | OLD | NEW | derived by |
|---|---|---|---|
| toy s42 t200 (`SnapshotTests`) | `0af7143f…` | `5994e97838d275d3dd2a82cc6d0df1e23c70d2a9bbe412f4ba44538e043db180` | in-test harness + `sim run --seed 42 --turns 200` |
| founded s42 t300 (`SnapshotTests.FoundedGoldenHash`, `ci.yml FOUNDED_GOLDEN`) | `02c7f9eb…` | `07c6ec45902428d210361719fa1fbf90de5aacd3baf3f18c09aebdccb64f2c61` | in-test harness + two CLI processes |
| first reign t40 (`FirstReignTests.PostR1Golden`) | `74a97abc…` | `27dd99c66bb7bb13363bd694eeb1b9a1d8b11bfb094c44f27e21f259656baa8a` | in-test harness |
| driven s42 t300 (`DrivenGoldenTests.Golden`) | `3a9f007a…` | `6664a9b258c2d999f8f307d6aba4c4729087b9bf22e73c9beca185c02a194f33` | in-test harness |

The Age half of the gate and the revolt-Age inheritance moved none of them (no golden run levies or advances an
Age). The AI-empire 600-turn leg is the one CI run that taxes; it has no pinned hash (its gate is two-process,
replay and inspect agreement, §7).

---

## 9. Offsets: wired and not wired

| Director's offset | wired? | through |
|---|---|---|
| Food availability | **WIRED** | Sustenance in P (the felt burden) and the food factor of the provision reading |
| Housing | **WIRED** | Shelter in P and the housing factor of the provision reading |
| Comfort goods / amenities | **WIRED** | Comfort (pottery, cloth) in P |
| Public services | **WIRED (public works)** | completed granary and workshop count in V |
| Development | **WIRED (built works)** | the same structure count in V; roads only through reach (they raise state capacity, which raises both V and the effective rate) |
| Institutions | **WIRED (universities)** | maturity-weighted in V |
| State capacity | **WIRED** | reach multiplies V (and, as before, the effective rate) |
| Safety, Health, Belonging/Faith, Prospects needs | **NOT WIRED — DEFERRED** | unbound needs (no state); they enter P automatically when bound |
| Temples, police, sanitation, schools as services | **NOT WIRED — DEFERRED** | no such structures exist |
| `inst.census` collection efficiency | **NOT WIRED — DEFERRED** | a DEFERRED research entity |
| The four fiscal-refinement civics | **NOT WIRED — DEFERRED** | knowledge only; no effect term |
| Legitimacy as an offset | **NOT WIRED (by design)** | it is an OUTPUT of the pressure; feeding it back would be circular |

## 10. Supersession (pointer notes appended 2026-10-05; nothing rewritten)

| superseded text | where | pointer |
|---|---|---|
| R2b attribution (share of the needs grievance, re-computed each turn); "100 % at full reach is a second revolt corner"; protest 15 → 50, uprising at p = 1 | `docs/m5-integration-coherence-matrix.md` §6 | §10 appended |
| R4a §3 "OPEN for the Director: a poorly-off settlement never protests the levy" | `docs/r4a-m5-closure-record.md` §3 | §10 appended |
| Baseline §5.2 items 2, 5–7; §5.3 "public services / development / institutions: NO"; §5.5 G1–G5; §3.1 A2 "100 % revolt corner"; §9 open items; §12 rows 11, 22; §13 rows tax/unrest/revolt; §4.3 AI first-tax timing | `docs/m5-playtest-baseline.md` | §14 appended (the §18 decision record) |
| "a new polity at Age A1" (OPEN) | `docs/m5-playtest-baseline.md` §12/§13; D-048 | D-048 §5 appended |
| ADR-033 D4 "happiness × (1 − r)"; the second zero corner | `docs/adr/adr-033-m5-integration-pass.md`; `docs/m5-governing-loop-port.md` | pointer sections appended |

## 11. INFERRED choices (for ratification) and known limitations

1. **Segments are classes.** The D-010 bucket (class × settlement) is the segment. Today's classes have near-
   identical baskets and peasants are ~90 % of most places, so most risings are "the peasants" and a settlement
   whose peasants rise is close to carrying it. Minority risings arise where a minority's own provision is worse
   (UnrestTests' rig) and as PARTIAL risings (the rebel fraction). Finer segments (faith, ethnicity, occupation)
   are Tier-B, DEFERRED to M8.
2. **Deterministic expected-value revolt.** The Director's "increasing probability/severity" is modelled as a
   deterministic portion (the rebel fraction) — the expected share — not a per-segment draw; no RNG is used. A
   stochastic draw would go through `RngRegistry` if ruled.
3. **The rebel fraction's span** equals the protest span (identity, §5.2).
4. **Rebels withhold all their levied work; protesters part of it** (§2.3) — strike and riot against the
   exaction (D-010 "riot: damage, strikes"); other riot damage is not modelled.
5. **A settlement that changes hands keeps its segments' T**; the new ruler (no levy) lets it decay.
6. **D-021 valves 2 (fatigue) and 3 (unrest-driven exit)** remain DEFERRED; only valve 1 (discharge) ships.
7. **Rebel hunger escalation** is bounded: the drag's carrier is the levy, so an untaxed (e.g. famine) protest drags
   nothing, and a lost settlement's levy ends.
8. **The UI** (`PlayerViews`, `StateChronicle`) names resentment, protest, risen segments and their portions in
   revolt, and annals a segment rising and subsiding; it computes nothing.

## 12. Tests (the §17 map)

| §17 | test |
|---|---|
| A normal tax | `TaxPressureTests.A_NormalTax_AtTheAiCeiling_IsManageable_NoProtestNoRisingAnywhere_HappinessDipsButHolds` |
| B high tax progressive | `TaxPressureTests.B_HighTax_DeterioratesWelfareProgressively_OverTurns_NotAtTheEdict` |
| C 100 % not immediate | `TaxPressureTests.C_FullLevy_NeverRevoltsImmediately_TheEarliestRisingIsBoundByTheAccrual`; `GovernanceTests.AFullLevyAtFullReach_IsNoLongerARevoltCorner_H2` |
| D sustained extreme | `TaxPressureTests.D_SustainedExtremeTaxation_EventuallyProducesSeriousInstability_ThroughStages` |
| E a segment | `TaxPressureTests.E_ARising_AffectsOnlyThePortionOfTheSegmentInRevolt_TheSettlementStaysItsRulers`; `UnrestTests.ARisenMinority_WithholdsOnlyItsOwnLevy_AndDoesNotTakeTheSettlement_AMajorityDoes`, `UnrestTests.TheSettlementUprising_IsRebelsAboveTheShare_Strictly` |
| F prosperity | `TaxPressureTests.F_ProvisionAndServices_ReduceAndDelayThePressure_AWellOffPlaceBearsTheExtremeLevy` |
| G poverty | `TaxPressureTests.G_ThePoorSettlement_AccumulatesMorePressure_FromTheSameRate_AndRisesSooner` |
| H recovery | `TaxPressureTests.H_CuttingTheLevy_LetsThePressureDecay_AndWelfareRecover_OverTurns` |
| tipping point, offsets, schema | `UnrestTests.TheTippingPoint_RisenFromU_TheRebelFractionGrowsOverTheProtestSpan`, `UnrestTests.ServiceOffset_ReadsPublicWorksAndInstitutions_ScaledByReach`, `UnrestTests.ThePoorerSegment_FeelsTheSameLevyMore_AndAccruesMore_InTheSystem`; `TaxGrievanceSchemaTests` (populated: ExpectedLength, round-trip, hash) |
| Age gate | `TaxAgeGateTests`; AI first-levy pins in `AiEmpireIntegrationTests` |
| revolt Age | `RevoltAgeInheritanceTests` |

## 13. Mutation record (MEASURED, ADR-015 §7)

Each mutant applied alone to a scratch copy of this branch (never a shared tree), Release build, filter
`UnrestTests | TaxPressureTests | Systems.GovernanceTests | TaxAgeGateTests | RevoltAgeInheritanceTests` (110 tests;
clean baseline 110/110 in 98 s). Bound: max(600 s, 5 × baseline) per mutant; none hung (84–100 s each). Every mutant
is killed by at least one SEMANTIC test (no golden in the filter).

| mutant | killed by (semantic) |
|---|---|
| M1 the settlement uprising reads the RISEN share instead of the rebels | `UnrestTests.TheSettlementUprising_IsRebelsAboveTheShare_Strictly`; `TaxPressureTests` D, E, F (4 failed) |
| M2 no service offset | `UnrestTests.Dignity_IsTheBurdenOffsetByProvision_AndServices_AndExactlyTheR2bReadingAtItsEdges`; `TaxPressureTests.F` (2) |
| M3 happiness ignores the accumulated pressure | `UnrestTests.Happiness_FallsWithTheAccumulatedPressure_NotWithTheEdict_AndRevoltReadsProvisionOnly`; five `GovernanceTests`; `TaxPressureTests` A, B, F, H (10) |
| M4 revolt reads the taxed happiness (the old corner restored) | `GovernanceTests.AFullLevyAtFullReach_IsNoLongerARevoltCorner_H2`; `UnrestTests.Happiness_Falls…RevoltReadsProvisionOnly`; `TaxPressureTests` D, E, F (5) |
| M5 the Age half of the tax gate removed | all five `TaxAgeGateTests` behaviour tests (hand-built edict, AI valve, turn-exact delivery, save/load) (5) |
| M6 the revolt child does not inherit the Age | six `RevoltAgeInheritanceTests`; `UnrestTests.ARevoltedSeat_BecomesANewAiPolity_HoldingTheCompleteParentKnowledge` (7) |
| M7 the whole segment revolts at its tipping point | `UnrestTests.TheTippingPoint_RisenFromU_TheRebelFractionGrowsOverTheProtestSpan`, `…ARisenMinority…`, `…TheSettlementUprising…`; `TaxPressureTests` D, E, F (6) |
| M8 the levy stock decays without protest's discharge | `UnrestTests.ExtremeTax_OnAWellProvidedSeat_IsBorneInProtest_NotARevolt`; `TaxPressureTests` D, E, F, H (5) |

## 14. Validation (MEASURED, Release, 2026-10-05; machine shared with other agents)

- **Sim.Tests** (code at `af316ee`): `Passed! - Failed: 0, Passed: 1500, Skipped: 8, Total: 1508` (34 m 38 s).
  The 8 skipped are the six pre-existing manual rigs plus this record's two (`TaxPressureMeasurement`,
  `TaxPressureCalibrationProbe`).
- **Sim.Ui.Tests** (same build): `Passed! - Failed: 0, Passed: 476, Skipped: 0, Total: 476`.
- **Gates:** banned-constructs, read-isolation, readonly-proof OK; research content audit, calibration report and
  gameplay-unlock audit `--check` current.
- **ci.yml determinism-xproc step, run locally verbatim** (build at `c027c43`; `af316ee` changes comments and
  content `_doc` strings only): orderless 2 processes × 400 turns byte-identical; ordered vs replay × 400
  byte-identical; founded 2 processes × 300 byte-identical and equal to `FOUNDED_GOLDEN` `07c6ec45…`; founded
  ordered vs replay × 300 byte-identical; AI-empire leg 2 processes × 600, replay and `sim inspect`
  (`reproduction VERIFIED: 601 turns, hash-for-hash`, 194 orders, kinds 3–8) — exit 0.
- **Calibration battery** (`CalibrationBatteryTests`): 7/7 passed.
- **Nightly corridor sweep** (`sim autoplay --seeds 20 --turns 650` + `sim corridors`): exit 0 (no gating failure).
  Two QUARANTINED corridors report window drift (`densityPerArableKm2` 18/20 seeds inside its window,
  `migrationGrossPerDecade` 1/20) — both quarantined before this branch (t4.21 director report: migration 0/20 in
  window) and both on untaxed autoplay worlds, where this model is inert (§8). Not caused here: INFERRED from the
  inertness proven by the golden controls, not re-measured at the base commit.
- **Not run:** `sim bench` (excluded by the stream instructions).

---

## 15. F1 fix pass (appended 2026-10-05; nothing above rewritten)

This section records the changes made after the adversarial verifier reviewed H2. The Director's §18 rulings in §1
remain **RATIFIED** and unchanged. Every choice below is the fix stream's own and is marked **INFERRED**, for the
Director to ratify. Every number was measured on branch `m5h-f1-tax-fixes` (Release, dev world seed 42, canonical
era table, levy from turn 0, A3, crafts known) unless it is marked otherwise.

### 15.1 Corrections to the record above

- **§6.4 "40 % never protests, never rises, anywhere" overclaims.** It holds only for the measured conditions:
  well-off, served, natural and poor (housed for a quarter) capitals, plus every settlement of the 4-settlement dev
  world. With F1's capacity term those settlements' peak segment T at 40 % over 80 turns is 6.46 (reach 1.0),
  6.71 (0.81), 6.85 (0.78) and 6.85 (0.68), all below the onset of 12. A capital with no housing at all can
  protest at 40 % (the verifier's probe P2 measured a peak T of 12.92 and protest 0.115 from turn 14). F1 did not
  re-measure that case (**INFERRED** to still hold). This fits Director §6: a destitute place protests a levy that
  an ordinary place bears.
- **§11.7 "Rebel hunger escalation is bounded" was false for the levied place itself.** The rebels' drag reached
  every product, food included (factor 1 − r × withheld, which is 0 at r = 1 and q = 1). A final settlement under a
  99–100 % levy starved to extinction: the verifier's probe P6 had population 0 by turn 39 at 100 %, and F1
  reproduced it as a failing test. Fixed in §15.2.
- **§9 "State capacity WIRED" had the wrong sign.** Reach multiplied the effective rate, so a weakly reached place
  felt less of the same declared levy. Fixed in §15.3.

### 15.2 Rebels withhold the levy, not their own bread (**INFERRED**; BLOCKER fix)

`Governance.FoodOutputMultiplier` = `max(extraction × drag, 1)` when protest drags, and exactly the extraction
multiplier otherwise. `ProductionSystem` applies it to food: farming and gathering (`Farm`) and the
herding/fishing pathway. Non-food output (extraction sector, crafts) keeps the full `OutputMultiplier`, so the
strike against the levied crafts, ore and stone is unchanged.

The reading: protesters and rebels withhold the work the levy COMPELS (the extraction gain), never the work that
feeds them. Food output under revolt therefore falls back to what the people would produce untaxed, and no lower.

**The minimal alternative, rejected:** withholding only the surplus above the settlement's own consumption. It
would need a production-side read of demand, which is a new coupling.

**Measured over 300 turns** (`TaxPressureMeasurement.RebelSubsistence_300Turns`, skipped rig; "final" is one
settlement with colonization off, so it cannot revolt away):

| condition | 0 % | 40 % | 70 % | 99 % | 100 % |
|---|---|---|---|---|---|
| well-off: pop t300 / max deficit / lost | 3673 / 0.000 / never | 3781 / 0.000 / never | 3747 / 0.000 / never | 3681 / 0.000 / never | 3667 / 0.000 / never |
| natural | 3487 / 0.074 / never | 3707 / 0.000 / never | 3750 / 0.000 / never | 3514 / 0.071 / t24 (pop 677) | 3509 / 0.072 / t22 (pop 668) |
| poor | 3490 / 0.076 / never | 3725 / 0.000 / never | 3583 / 0.073 / never (rebels ≤ 4.9 %) | 3475 / 0.073 / t5 (pop 526) | 3491 / 0.073 / t5 (pop 526) |
| final settlement | 3521 / 0.057 / — | 3745 / 0.000 / — | 3753 / 0.000 / — | 3519 / 0.055 / — (rebels 1.0, non-food ×0.013) | 3553 / 0.058 / — (rebels 1.0, non-food ×0.000) |

- No arm goes extinct.
- No arm's food deficit exceeds the untaxed arm's (0.057–0.076).
- At the change of hands the ordinary capital still holds its population: 677 and 668 people at 99 % and 100 %,
  against 480 and 436 measured by F1 at the pre-fix commit (the verifier's P7 recorded 129 and 320 in its own 4-settlement read)

Tests: `TaxRebelSubsistenceTests`. The final-settlement case failed first.

### 15.3 State capacity offsets the levy (**INFERRED** form and value; Director §5/§6)

    felt = d × (1 + taxCapacityOffsetMax × (1 − reach)) × (1 − taxBurdenOffsetMax × P) × (1 − taxServiceOffsetMax × V)

Here d is the DECLARED rate and reach is `ControlRow.Strength`. Collection is unchanged: the settlement yields
d × reach, which is ADR-033 D4's effective rate, used by extraction and by the rebels' drag. Two consequences:

- The felt burden per unit COLLECTED rises as reach falls.
- At the same declared rate, and also at the same effective rate, a weakly administered place feels more.

The reading: arbitrary, unpredictable collection by agents the centre does not control.

**Where the term is inert.** At full reach (the capital) it is exactly 1, so every capital number in §5–§6 is
unchanged. So are every golden (§15.6) and the AI first-levy pins.

**`taxCapacityOffsetMax` = 0.25 (CHOSEN).** It equals `taxServiceOffsetMax`: services and administration are
treated as the two halves of the fiscal exchange.

**Measured** (all settlements of the dev world, 80 turns):

| rate | reach 1.00 | reach 0.81 | reach 0.78 | reach 0.68 |
|---|---|---|---|---|
| 40 %: peak segment T | 6.46 | 6.71 | 6.85 | 6.85 |
| 70 %: peak protest | 0.135 | 0.202 | 0.227 | 0.235 |

Tests: `TaxStateCapacityTests`, covering equal declared rate, equal effective rate (with and without a granary),
exact inertness at full reach, and the full pipeline against `taxCapacityOffsetMax = 0`. The tests failed first.
The H2 pin `TheBurdenFeltIsTheEFFECTIVERateNotTheDeclaredOne` ("the frontier accumulates less pressure") is
replaced by its opposite. **§9 row "State capacity" now reads:** WIRED, as an aggravating offset of the declared
burden and as the multiplier of V and of collection.

### 15.4 An uprising against the levy needs the current ruler's levy (**INFERRED**)

`Unrest.IsUprising` additionally requires `Governance.EffectiveTaxRate > 0` at the settlement, meaning its current
ruler collects a levy there. Levy grievance inherited across a change of hands still decays and still reads as
protest, but it cannot throw off a ruler that takes nothing. This was the verifier's probe P3.

The drag already carried this condition: with no levy, the output factor is 1.

Test: `UnrestTests.InheritedLevyGrievance_CannotThrowOffARulerThatLeviesNothing`, which failed first.
`TheSettlementUprising_IsRebelsAboveTheShare_Strictly` now declares a levy.

### 15.5 Other items

- **Ghost rule for levy rows (§2.1) is now pinned:** `UnrestTests.AnEmptyClass_HoldsNoLevyGrievance_TheGhostRuleZeroesItsRow`.
  Measured to fail with the zeroing line removed (the verifier's surviving mutant V6).
- **Glass Box Dignity chain:**
  - It states the shipped formula above. Its nodes are DeclaredTaxRate, AdministrativeReach, ServiceOffset and
    EffectiveTaxRate (all recomputed on Prev), plus LevyGrievance (the segment's `TaxGrievances` row on Next).
  - All of them are lever-less.
  - Test: `DignityChainTests`.
- **The A3 requirement is cross-validated (INFERRED: validate rather than derive).** The four-stream load refuses a
  `governance.taxationMinAge` earlier than the earliest Age at which `governance.taxationRequires` can hold, using
  research.json's node Age tags and the live knowledge evaluator (`SimConfigLoader.EarliestRequirementAge`).
  - A later minimum stays legal: knowledge first, capability later.
  - Test: `TaxAgeContentAgreementTests`.
  - Both statements are kept rather than one derived from the other, because research.json's Age tag is metadata
    (D-044 R13) and the gate is sim.json's.
- **Turn pins (Director §17):**
  - `TaxPressureTests.D` no longer pins (5, 14, 22). It asserts that the stages are ordered and delayed, and
    monotone in rate and in poverty.
  - The AI first-levy pins 463/464 are KEPT under the repository's AI-pin convention (dated re-pin history). They
    were re-measured unchanged after F1: everything before the first levy is untaxed.
- **Text:**
  - research.json's taxation description now says the edict is operational only from Age III.
  - The pushback rig's comment no longer names arithmetic_babylonian.
  - Sim.Ui Trend comments no longer call happiness 0 the revolt condition. Revolt reads the provision reading.

### 15.6 Goldens

None moved: FOUNDED_GOLDEN 07c6ec45… and every golden, attribution and CI-pin test pass unchanged. The
`aiEmpires = 0` worlds are untaxed, and every F1 term is exactly inert there: the food floor applies only under
drag, the capacity term only under a levy, and the uprising condition only adds a requirement. The AI 600-turn
ci.yml leg has no pinned hash. Its two-process, replay and inspect agreement is recorded in the stream report.


## §16 — Director rulings 2026-10-06
The INFERRED choices of §11 and §15 (classes as segments, deterministic expected rebel fraction, uprisingGrievance 20, food floor with permanent revolt of a last settlement, taxCapacityOffsetMax 0.25) are RATIFIED by D-050.

*(Dated note appended 2026-10-06, M5 polish pass; §11 is unchanged.)* D-050 ruling 3 places the finer segments
(faith, ethnicity, occupation) at **M9 Society**: §11 item 1's "Finer segments … are Tier-B, DEFERRED to M8" reads
**M9**. D-050 ruling 7 defers capital succession to **M8 Politics / Diplomacy** (revolt-founded civilizations have no
capital and cannot tax until then). Record: `docs/d050-m5-hardening-rulings.md`.
