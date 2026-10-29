# ADR-043 — Civics Points: two research lanes, and the research stage without a civic

**Status: ACCEPTED under the Director's ruling of 2026-10-22 (D-062, recorded by the coordinator).** Implemented as M6
slice Track CP on branch `m6-cp`. It supersedes, for Civics only, the single-pool / no-reserve rulings (D-044 R2, R12,
R20-D as it applied to civics; ledger §26 items 2–3; D-I of 2026-10-20) and the research-stage civic atom (ADR-029 §8,
D-046 F§1), exactly as D-062 records. It does not touch ADR-030: Research Points stay per turn and stay the only
per-turn quantity (CP is per sim-year — law 3 as written, so no second exception is created).

## 1. The decision

1. **Stage gate.** `research.json researchStage.requires` drops `legal_code_roman`:
   `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal`. The subtrees open on technology
   knowledge alone. `inst.university` keeps `library AND legal_code_roman` (the institution needs the civic; the stage
   does not).
2. **Two lanes.** Research Points (RP) fund only the **Technology** target; Civics Points (CP) fund only the **Civics**
   target. A polity holds at most one target per tree; the lane is the node's tree. `ResearchTargetRow` and
   `ResearchProgressRow` are reused unchanged (a polity may now hold two target rows). A civic order no longer
   displaces the technology target.
3. **Order kind 6 keeps its shape.** TargetId = a node key selects the lane by the node's tree; **−1 clears the
   Technology lane; −2 (new) clears the Civics lane**. `OrderLog` validation accepts {key ≥ 1, −1, −2}; Amount stays 0.
   No new order kind, no wire change (IoVersion 1).
4. **Bankable CP.** Each step `Balance += generated`, `Generated += generated`; with a Civics target the step spends
   `min(Balance, ⌈remaining EffectiveCost × 1000⌉)` milli-CP into its progress (`Spent += spent`, `Balance −= spent`).
   Spending the whole remainder sets progress to exactly the cost (the `Credit` rule). Unspent CP stays banked: no loss,
   no cap. RP keeps today's semantics: RP reaching no node is lost (no RP bank, D-044 R20-D).
5. **Order of operations inside ResearchSystem (fixed):** directives → credits (Eureka, foreign exposure) → RP
   throughput → CP accrual → CP spend → completions → persist.
6. **Accounting.** CP is not people, money or goods, so it has no Ledger flow (law 1 is not engaged). Its audit is the
   identity `BalanceMilli == GeneratedMilli − SpentMilli`, every field ≥ 0, rows strictly ascending by Polity — asserted
   every step in tests (seeds 1, 2, 42, 7) and refused by `Snapshot.Load` when broken.

## 2. Schema v35, section 62

`CivicsPointsRow(PolityId Polity, long BalanceMilli, long GeneratedMilli, long SpentMilli)` — 28 bytes; at most one row
per knowledge holder (a roster Empire, or a city-state's reserved local key −1 − settlementId), ascending Polity; a
holder that never accrued CP has no row. Owned by ResearchSystem. Appended after ConstructionHolds; no existing row
changed width. Readable set {32, 33, 34, 35}. **Migration:** a v34 stream stops after section 61 and loads with an
EMPTY CivicsPoints table — every bank starts at zero (v34 had no CP, so nothing is lost). Existing progress rows on
civic nodes are carried 1:1 (progress is in node-cost units, which are now CP for civics); a v34 save's one target row
becomes the target of its node's lane.

## 3. The CP formula and its tunables (PROPOSED)

```
cpMilli(step) = floor(1000 × pace × c × P^β × dtYears)
  P    = ResearchQuery.Population (the input RP reads: every person of the settlements the holder controls)
  c    = tuning.civicsPoints.coefficientPerYear   PROPOSED 0.008   (loader: (0, 1])
  β    = tuning.civicsPoints.exponent             PROPOSED 0.699   (loader: (0, 1) — sublinear, §8.1.2)
  pace = 1 for an Empire; tuning.cityStatePaceFraction (0.25) for a city-state — the factor its RP carries
```

Rationale: CP is a rate per **sim-year** integrated over `dtYears` (law 3), so civic progress keeps calendar meaning.
With β = RP's exponent and c = RP's coefficient ÷ 10, a 10-year turn (the founding band) accrues exactly as many CP
as RP — a founded civilization of 5 000 pays a civic at the pace it paid a technology of the same cost before this
change; in shorter late-era turns CP accrues proportionally less per turn than RP. Stored in integer milli-CP so the
bank is exact (no double drift); the floor never rounds up. Both values are calibration, not rulings: tune in
`research.json` (always allowed).

**The switch.** `tuning.civicsPoints.enabled` (true as shipped). Off — or the block absent — is the v34 ONE-POOL model
exactly (one target in either tree, funded by RP; −2 clears that target only when it is a civic; no CP row is ever
written). It exists for attribution: `TestConfigs.PreCivicsPoints` / `PreTrackCp` (with the v34 stage) reproduce every
v34 hash through `SchemaProjection.V34`.

## 4. DEFERRED (named, not implemented)

- **Other CP sources:** institutions, specialists (artists, scholars), embassies, spies — each a future term beside the
  population term; none is invented here.
- **The RP ↔ CP exchange.** The Director allows it only on the government-change turn; government change is M8. The
  slot is named: a future exchange runs inside ResearchSystem between "CP accrual" and "CP spend" (§1.5), reads the
  government-change fact from Prev, and moves value only through the CivicsPoints account (Generated/Spent stay the
  audit). Not implemented.
- **Civic adoption** (M8) and any adoption UI; the 60 proposed new civics (Track TREE / M8).

## 5. AI and UI

- **AI.** `AiResearchPolicy.DecideAll` returns up to two orders, Technology lane first, then Civics; each lane orders
  only when it holds no live target, choosing by the unchanged rule (goal composite key (remaining closure cost, goal
  ordinal), then node key (EffectiveCost, key)) with every goal closure and the fallback restricted to the lane's tree.
  The taxation goal's civic is therefore worked on the Civics lane; its technology prerequisites stay goal work on the
  Technology lane (so the civic never waits on a lane that cannot fund them). Tie-dense test:
  `CivicsPointsTests.AiLanes_EachTheArgminOfItsOwnTree_TieDense_LowerKeyWins_AndALiveLaneIsKept`. City-states choose
  the cheapest available node per lane. The CLI `--auto cheapest` driver fills each free lane.
- **UI.** The research screen shows the Technology and Civics targets separately (the capsule follows the open tab);
  on the Civics tab the rate is CP per turn and the panel shows "Civics per turn" and "Civics Points banked"; costs and
  turn estimates of civics are in CP. The action surface keeps "Learning" (the Technology target, Stop = −1) and adds a
  Civics line with its own target, CP a turn, the bank, and "Stop civics" (−2: `ActionCommandKind.ClearCivicsResearch`,
  `UiSession.ClearResearchTarget(ResearchTree)`); `AvailableActionsQuery` lists `research.clear-civics-target` (Id 3).
  No adoption UI.

## 6. What breaks, and the schedule price

**Saves.** v35 is written; v32–v34 load (CivicsPoints empty). A v34 save continues on the two-lane model from its first
step. `Snapshot.Load` refuses a CP table no rule writes. Order logs: kind 6 is unchanged on the wire; −2 is newly valid,
so a v35 log carrying −2 is refused by an older build (D-008: replay with the build that recorded it).

**Behaviour.** Every world with a populated polity accrues CP from turn 1, so every research-bearing golden moves (VALUE +
LAYOUT). Measured on this tree (in-test harness; the switch-off twin `TestConfigs.PreTrackCp` returns every OLD value
through `SchemaProjection.V34`; Civics Points off alone returns OLD; the stage gate off alone returns NEW — the stage gate
moves no golden, none reaches the research stage by its horizon):

| Golden | OLD (v34) | NEW (v35) | Cause |
|---|---|---|---|
| founded seed 42 t300 (`SnapshotTests.FoundedGoldenHash`, ci.yml `FOUNDED_GOLDEN`) | `0f29efc7…` | `5c56a1f9…` | the player's CP account (no target set) |
| toy seed 42 t200 (`ToyGoldenHash`) | `2e6d4461…` | `1d3f5be2…` | layout alone (no research content) |
| driven seed 42 t300 (`DrivenGoldenTests.Golden`) | `eb88663a…` | `1fb16d8b…` | the player's CP account |
| first reign t40 (`FirstReignTests.PostR1Golden`) | `f7ca60a5…` | `3fee0f31…` | the player's CP account |
| AI world seed 42 t300 (`AiFoundingAttributionTests.S1cTurn300`) | `25f4799d…` | `5623de7a…` | CP accounts and the AI's civics lane |
| AI integration levy (`AiEmpireIntegrationTests`) | 422/423, A3 422; 8 foundings, 14 settlements | 440/441, A3 440; 9 foundings (+428), 15 settlements | the A3 closure's civics now paid on CP pace; the later levy lets one more founding stand before it |

Attribution: `CivicsPointsAttributionTests` (per golden: live = NEW; PreTrackCp → V34 = OLD; PreCivicsPoints → V34 = OLD;
PreStageGate = NEW; and the turn-exact first-step proof). By the R4 rule every older chained twin
(`PreIncrementalConstruction` and through it `PreD054Sim`, `PreD053`, `PreForager`…, plus `PreReservations` and
`PreTradeKnowledge`'s research content) now strips Track CP, so every older attribution control keeps returning its
historical literal; `SchemaProjection.V33`/`V32` drop section 62 and assert it empty. The SINGLE-LAYER twins
(`PreContinueWithinTurn`, `SingleSwitchTwins.*`, `PreEmpire*`, `PreGranaryEffects`) keep Track CP, because they are
compared turn by turn with the live world.

**Tests changed.** Superseded with dated pins, nothing deleted: `ResearchContentTests` (the stage holds without the
civic), `ResearchOrderTests` (−2 valid; −3 is the first malformed negative), `AiResearchGoalTests` (stage ⊂ university
goal, the civic only in the latter), the five one-pool `ResearchEngineTests` (kept: they pin the switch-off model), the
schema-version and stream-trailer pins of the schema tests (v35: the layout alone), the CLI migration-note / usage /
reproduce texts, the golden re-pins above, and the layout controls (`ReservationsLayoutAttributionTests`,
`IncrementalConstructionAttributionTests`) moved onto the pre-CP world. New: `CivicsPointsTests`,
`CivicsPointsSchemaV35Tests`, `CivicsPointsAttributionTests`.

**UI gate.** `PlayabilityGate` gains the row "Stop civics (clear the civics target)"; the existing "per-turn-table" row
now scrolls until the whole toggle clears the panel's clipped bottom band (MEASURED: with the shorter Civics line after a
Stop, the old 10-px margin left the toggle's click unreceived).

**Docs regenerated.** `docs/research-corpus-audit.md`, `docs/research-calibration-report.md` (its "one shared pool" text is
now the two-lane statement plus a CP bullet), `docs/research-gameplay-unlock-audit.md` (stage links 5 → 4). The
calibration report's measured input `scripts/research-calibration/population-trace-seed42.csv` is NOT re-measured here: it
was already stale before this change (its turn-1 population 5 245 against 5 118 that `sim research --seed 42 --turns 1630
--auto cheapest --trace-population` prints on this tree), so re-measuring it belongs to a calibration packet.

**Schedule price.** One schema bump (shared with Track EX at v35), five golden re-pins with attribution, and a slower
civics pace in the late eras (CP per sim-year shrinks per turn as turns shorten) — to be watched in calibration; the
coefficient is a tunable, not a ruling.

## Dated note — 2026-10-27 (D-065 R6; ADR-054; appended, the text above is unchanged)

**§3 is superseded in part.** The Director's binding rule D-065 R6 ("Every Research Point (RP) and Civics Point (CP)
calculation is per turn and based on average population") resolves CR-026. With `tuning.civicsPoints.perTurn` (shipped on),
CP are generated **per turn** by the Research Point base function — `cpMilli(step) = floor(1000 × pace × c' × P^β)`, with
c' the holder's `research.civicsPointsCoefficient` benefit applied to `rpPerTurn.coefficient` and β = `rpPerTurn.exponent` —
**never multiplied by `dtYears`**, and P is the holder's AVERAGE population (`ResearchQuery.KnowledgePopulation`,
`tuning.populationAverageTurns` 2). The block's own `coefficientPerYear` and `exponent` are removed (the loader refuses them
beside `perTurn`). The statement in the header that CP "is per sim-year — law 3 as written, so no second exception is
created" no longer describes the shipped model: ADR-054 creates that second scoped per-turn exception, as ADR-030 §3
requires. Everything else in this record (two lanes, the bank, the order of operations, the accounting identity, schema
v35) stands. The §3 per-sim-year form remains reachable as the attribution twin (`perTurn` absent;
`TestConfigs.PrePerTurnCivics`).
