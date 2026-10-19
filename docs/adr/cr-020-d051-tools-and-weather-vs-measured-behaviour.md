# CR-020 — D-051 §4 AND §1(b) AGAINST MEASURED BEHAVIOUR AND TWO FROZEN DEV-CORRIDOR TEETH

**Status: RULED 2026-10-12 (D-052 §9 and §11)** — §A: option 1 for the playtest (the guard), with the approved
direction recorded and its mechanism deferred; §B: option 3 (1(b) on, both teeth left red, disclosed), calibration to be
reassessed from measured evidence after the playtest. *As first written:* **OPEN — ESCALATED TO THE DIRECTOR.** Raised
2026-10-10 on branch `m5p-d051-fixes` (cut from `m5p-integration` @ `2dc6af2`) under CLAUDE.md's STOP rule. D-051 asks
for this CR ("A CR records the conflict and this ruling", §1); none existed. Every number below was MEASURED by the
agent raising it.

> **2026-10-12 — Director ruling (`docs/d052-m5-integration-audit-rulings.md` §9, §11).**
> - **§A:** the Workshop guard (`InputUnobtainable`) stays. The approved direction is a primitive stone-and-wood recipe
>   that produces the existing tools good at lower productivity than bronze, with no stone exhaustion, no granary
>   blockage and full Bronze Age productivity preserved. Its mechanism (option 2, option 3 or another) and its
>   milestone are deferred until after the M5 playtest. Nothing is implemented.
> - **§B:** time-averaged weather stays ON. Every frozen calibration reference is preserved: the `corridors.json`
>   bands, the in-test dev envelope [71,000, 119,000], and the per-seed recorded migration values with their 0.75×
>   tooth. Nothing is widened, waived, re-pinned or reverted. The two failing cases stay red and are disclosed for the
>   playtest. Calibration is reassessed from measured evidence after the playtest.
> - **The recommendation below (§B option 1) is NOT adopted now.** The reds still stop a merge to `main` and the M5
>   exit sweep (GOV-3 B6).
> - Evidence the audit added is in §B.4.

**Not a request to change D-051.** Both rulings were executed as written. What is escalated is (A) that
§4's mechanism, as a plain recipe, makes construction worse, and (B) that §1(b), implemented as ruled,
moves two frozen dev-corridor teeth that no agent may move.

---

## A. D-051 §4 — pre-bronze toolmaking

### A.1 Frozen items
D-051 §4 (a non-bronze, lower-productivity tools path from founding; queue semantics unchanged) against
D-032 / T3.3 crafting (labour split EQUALLY across available recipes, Leontief over inputs, no demand
stop) and M4-D construction (queue head blocks; materials drawn from the settlement's own stock).

### A.2 Evidence (throwaway probe; seeds 42, 1, 2, 7; all 12 settlements; one project enqueued on turn 0; 150 turns)

| tree | granaries built | workshops built | world stone at t1 / t20 / t150 |
|---|---|---|---|
| `1ecbc0b` (no pre-bronze tools) | 46 / 48 (turns 2–126) | **0 / 48** | 9–23 / 162–569 / 3,555–7,017 |
| + recipe `stone-toolmaking` (stone 2 + timber 1 → 1 tool, 0.4 labour-years) | **0 / 48** | **0 / 48** | 0 / 0 / 0 |

The default sector split gives crafting 12 % (`Sectors.Default`), so every order-free world crafts. The
recipe consumed every unit of stone as it was extracted, so no granary ever saw its 20 stone; and the
few tools made each turn were equipped and worn out by farmers (0.1 per equipped farmer-year), so no
settlement ever held the workshop's 10. The recipe is NOT on the branch.

### A.3 What is on the branch instead (`355608f`)
Per the Director's 2026-10-10 instruction ("ensure a Workshop is not a legal construction choice if it
would become permanently blocked"): `ConstructionQuery.Availability` → `InputUnobtainable` when a
crafted input is neither held in full nor makeable now through an available recipe chain. The workshop
is therefore not offered in Age I (shown locked, with "tools ← Toolmaking ← bronze ← Bronze casting ←
Tin bronze (research, Age III)"). This removes the trap; it does NOT give Age I a working workshop.

### A.4 Options (≤ 3)
1. **Accept the guard as M5's answer to §4** and move pre-bronze tools to M6+ (no further code).
2. **Recipe + material reservation:** crafting may not draw a settlement's stock below what its
   queue head needs, and tools reserved for a queued project are not equipped. Medium; changes T3.3
   crafting semantics (a new reservation rule), every golden moves.
3. **Recipe + demand-limited toolmaking:** toolmaking runs only while the tool stock is below the
   equip target plus queued demand. Medium; a new demand stop inside the flat split.

**Recommendation: 1 for the playtest; decide 2 vs 3 after the Director has played.**

---

## B. D-051 §1(b) — time-averaged decade weather

### B.1 Frozen items
D-051 §1(b) (RATIFIED) against `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand`: the dev
population band [71,000, 119,000] at turn 1000 ("the band may not move while the quarantine stands")
and the CR-003 dev-migration quarantine's DOWN tooth (`dev.migrationGrossPerDecade` below 0.75× its
recorded per-seed value is "a NEW defect, or a ruled substrate change that must re-pin this envelope
deliberately").

### B.2 Evidence
- Seed 7: dev population at turn 1000 = **124,455**, above 119,000 (was inside before 1(b)).
- Seed 42: dev migration gross per decade = **8.71773E-05**, ×0.35 of the recorded 0.000248624 (the
  tooth's "disablement signature" is ×0.536). 1(b) removes most weather-driven famine flight, which is
  its purpose.
- 20-seed order-free batteries (`HardeningMeasurementRigs`, 400 founders):

| world, horizon | before 1(b): starving seeds (deaths) | after 1(b) | median population before → after |
|---|---|---|---|
| canonical, 650 | 11/20 (2,383) | **0/20 (0)** | 126,479 → 144,851 |
| canonical, 900 | 17/20 (6,257) | **1/20 (637)** | 216,626 → 253,249 |
| dev, 650 | 10/20 (1,417) | **0/20 (0)** | 42,553 → 48,558 |
| dev, 1000 | 15/20 (23,237) | **13/20 (53,949)** | 87,856 → 97,910 |

  Dev seeds inside [71k, 119k] at turn 1000: 14/20 → 16/20; above the band: 0 → 2. Every run
  conserves exactly; 0 negative-stock turns; 0 exceptions. The late dev deaths are the land-bound
  mode (order-free worlds never research agriculture), not weather.
- **Colonisation (a side effect, not a broken test).** D-037 B1 founds a colony only from famine flight
  with no viable destination. In the single-settlement canonical rig of `ColonizationFrontierKeyTests`,
  seed 2 colonised at step 53 before 1(b) and at step 807 after; seeds 1–6 found none in 400 turns;
  seed 5 none in 1,500. Early colonisation was being driven by single-year weather shortfalls. (On the
  12-settlement canonical world the 20-seed batteries found 0 colonies both before and after.) This
  makes D-051 §5 (a player "Found settlement" order) the only practical expansion path.
- Mechanism checks: E[turn multiplier] over 40 seeds × 2000 turns = 1.0013 / 0.9992 / 0.9980 at dt
  10 / 5 / 1; decade log-spread ≈ 0.193 (D-051: "~0.19"); the carried AR(1) state keeps σ and
  exp(−dt/τ). A switch (`harvestVariance.timeAveragedHarvest = false`) reproduces every pre-1(b)
  golden byte for byte — verified by a separate CLI process (switch off: `e9e089bb…`, the full 300-turn
  hash log identical to the base tree's; switch on: two processes agree on `937577ff…`).

### B.3 Options (≤ 3)
1. **Re-record the dev envelope and band for the ruled substrate** (the tooth's own text allows a
   deliberate re-pin for a ruled change; the band is the Director's).
2. **Ship 1(b) inert** (switch off, the CR-016 precedent) until the dev corridor is re-ruled — the
   starvation fix then does not reach the playtest.
3. **Ship 1(b) on and leave the two teeth red** pending a ruling (the T4.21-4 precedent).

**Recommendation: 1.** The quarantine exists to catch an unruled loss of flight; this loss is ruled.
The branch currently carries option 3.

### B.4 Evidence added by the 2026-10-12 integration audit (MEASURED on `m5w-integration` @ `f0427d2`)

- **Seed 7 also fails the migration tooth.** `Dev_MalthusCorridors_AllInBand` calls `AssertMalthusKnownDeviation`
  before `AssertDevMigrationQuarantine` (`CalibrationBatteryTests.cs:622` / `:623`). Seed 7 throws at the population
  envelope (`:770`), so its migration teeth never run.
  - Measured with 1(b) on: seed 7's `dev.migrationGrossPerDecade` = 1.0083155359603418E-04. That is ×0.3497 of the
    recorded 2.8835491401298867E-04 (`:262`), below its drift floor 2.1627E-04 (`:317`).
  - §B.2 above reports only seed 7's population and seed 42's migration.
  - A ruling that re-records only the population envelope would therefore leave seed 7 red.
- **The 0.75× tooth cannot separate a ruled weather change from a defect.**
  - Across dev seeds 1–20, the per-seed ratio of 1(b) on to 1(b) off has a median of 0.664 (range 0.281–2.134).
    13 / 20 seeds fall below 0.75, and 6 / 20 below the 0.536 "disablement signature".
  - Seed 42 moves ×0.3466 against the switch-off control, and seed 7 ×0.3530.
  - The drop is mostly in the number of migrants. Seed 42 moved 2,831 → 1,127 people, with person-years ×1.149; seed 7
    moved 4,033 → 1,670, ×1.173.
  - The tooth's calibration (×0.836 largest legitimate correction, ×0.536 disablement, `:186-192`) dates from T3.4c and
    was not re-measured through the later re-pins.
- **Attribution re-confirmed.** `f0427d2`'s dev world hashes equal `82bde01`'s. With `PreTimeAveragedHarvest` they equal
  `2dc6af2`'s, and both seeds pass every check. No m5w packet changes the dev calibration world.
- **The "band may not move" quote in §B.1 is the migration quarantine's docstring (`:159`).** It refers to the
  `corridors.json` band [0.001, 0.01]. The population range [71k, 119k] is an in-test literal the test calls
  "re-pinned like a golden", re-pinned four times: [70k, 100k], [68k, 119k], [82k, 138k] and [71k, 119k]. The
  2026-10-12 ruling treats both, and the per-seed recorded values, as frozen (D-052 §9).

## Blast radius
A: `ConstructionQuery`, `InfoQuery`, `AvailableActionsQuery`, `ActionSurfaceModel`; no golden moved.
B: `HarvestWeatherSystem`, `HarvestVarianceConfig` (+ `sim.json harvestVariance.timeAveragedHarvest`); every
world golden and the 38 attribution constants move (re-pinned, the switch as control: FOUNDED `e9e089bb…` →
`937577ff…`, first-reign `6720be6a…` → `467f2cb6…`, driven `5a8c6c64…` → `7bcdc03f…`; ci.yml `FOUNDED_GOLDEN`
re-derived by two processes); semantic turn pins moved by a few turns (AI Age, levy, road, merchant, artisan,
save points, first dwelling decay 149 → 201); three rigs that needed weather famine were re-aimed (Population
reconciliation horizon 900 → 1000; FoodFlow founded arm 300 → 700 turns, renamed `Founded700_…`; Colonization
frontier-key rig runs on the pre-1(b) control, its property being cache keying); no schema change. The two
`Dev_MalthusCorridors_AllInBand` cases (seeds 42, 7) are left RED and named here.

## Pointer added 2026-10-19 (D-058; dated note, historical text kept)

- **§B is resolved by option 1.** The Director's 2026-10-19 instruction ("CI main test run, full authorization,
  resolve the issue best you can") supersedes the §B hold.
- The per-seed recorded dev migration values and the in-test population envelope are re-recorded on the shipped tree:
  - 8.357547346004101E-05 / 8.367965454628822E-05;
  - [86,000, 139,000].
- Bands, the 0.75 tooth and every other tooth are unchanged, and no hash moved.
- The re-measurement adds two more ruled causes: the D-054 cylinder (seed 42 ×0.959, seed 7 ×0.847) and the D-053
  food relief (seed 7 ×0.980).
- Record: `docs/d058-calibration-rerecord-d051-regime.md`.
