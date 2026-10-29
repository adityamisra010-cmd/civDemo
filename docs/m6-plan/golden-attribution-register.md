# Golden attribution register — every pin move since M6 began

**Status:** RECORD (M6 closure audit, D-066 R2 "persistence and determinism", 2026-10-28). Consolidated from ADRs 043–056,
the RE-PIN comments on each pinned constant and their attribution tests, on the base tree (`0bf7626` + the uncommitted M7
pass + D-066). It adds no pin and moves none: the closure audit track changes no simulation code. Each row names the
**cause** that was measured and the **control** that reproduces the old value exactly (a switch-off twin, or a schema
projection that strips the new rows and asserts them empty). Hashes are abbreviated to 8 hex digits; the full values are
in the test files named.

M6 began at schema v34. The v34 values are the "M6 start" column; every later value is reached through the rows below.

## 1. The five headline pins

| Pin | Where | M6 start (v34) | Base tree now |
|---|---|---|---|
| Founded, seed 42, turn 300 | `SnapshotTests.FoundedGoldenHash`; `.github/workflows/ci.yml` `FOUNDED_GOLDEN` (`CiPinAgreementTests`) | `0f29efc7` | `d5ebd0b2` |
| Driven, seed 42, turn 300 | `DrivenGoldenTests.Golden` | `eb88663a` | `55afd498` |
| First reign, turn 40 | `FirstReignTests.PostR1Golden` | `f7ca60a5` | `064ed6b7` |
| AI world, seed 42, turn 300 | `AiFoundingAttributionTests.S1cTurn300` | `25f4799d` | `ac4554bf` |
| Toy, seed 42, turn 200 | `SnapshotTests.ToyGoldenHash` | `2e6d4461` | `ff4ea411` |

## 2. Every move, in order

"Superseded" values were pinned on a branch and replaced at merge by the next row's single re-pin; they are listed so a
reader meeting them in a commit or ADR can place them.

| # | Record | Cause (measured) | Founded | Driven | First reign | AI world | Toy | Control (returns OLD exactly) |
|---|---|---|---|---|---|---|---|---|
| 1 | ADR-043 (Track CP, D-062), superseded at the M6 merge | schema v35 section 62 (CivicsPoints) and the player's CP account, accrued every step | `0f29efc7`→`5c56a1f9` | `eb88663a`→`1fb16d8b` | `f7ca60a5`→`3fee0f31` | `25f4799d`→`5623de7a` | `2e6d4461`→`1d3f5be2` (layout alone) | `CivicsPointsAttributionTests` — `TestConfigs.PreTrackCp` and CP-off through `SchemaProjection.V34` |
| 2 | Track CP + EX merge (ADR-044), superseded | + the four v35 discovery tables (sections 63–66), populated; concealed copper and tin | `0a0759ef` | `62c3345d` | `91584395` | `8378f993` | `f97432f0` (layout alone) | `DiscoveryLayoutAttributionTests` (toy: `SchemaProjection.V34`) |
| 3 | ADR-045 (Track TREE), superseded | the research-content tranche — moves only worlds where an AI chooses research | unchanged | unchanged | unchanged | `25f4799d`→`a7858f32` | unchanged | ADR-045 §6 recorded `TheResearchTranche_IsTheWholeMove_…` (`TestConfigs.PreTreeTranche`); that test is no longer in the tree — row 4's `TheM6Slice_IsTheWholeMove_…` replaced it at the merge |
| 4 | **M6 re-pin (D-062, the whole slice once; ADR-044 §"goldens")** | CP (row 1) + EX (row 2, with the coordinator's rulings) + TREE (row 3; measured to move nothing in the AI-free worlds) | `0f29efc7`→**`8ccbe80f`** | `eb88663a`→**`392e2806`** | `f7ca60a5`→**`e3a86ce6`** | `25f4799d`→**`776d5d0c`** | **`f97432f0`** | `DiscoveryLayoutAttributionTests` (`TestConfigs.PreM6` through `SchemaProjection.V34` = v34 value) and `CivicsPointsAttributionTests` (the CP part on the EX-off, tranche-off world); AI: `TheM6Slice_IsTheWholeMove_…` |
| 5 | ADR-046 §2 (M6 review: the multi-good Prospect bill) | the AI's surveys spend timber and stone too; the cheapest-route closure moves nothing by turn 300 | unchanged | unchanged | unchanged | `776d5d0c`→`2e4759ac` | unchanged | `Adr046PinMeasurement`; `AiWorldTurn300_TheCheapestRouteMovesNothingByTurn300` (routes-off twin = the new hash) |
| 6 | ADR-046 §6–§7 (review batch 3) | the AI's supporting-milestone goal and build-first priority (each alone moves it: `3b29c636`, `3c6c35ff`) | unchanged | unchanged | unchanged | `2e4759ac`→`b2201d25` | unchanged | `AiWorldTurn300_BothBatch3SwitchesOff_IsTheE91d10bWorld` |
| 7 | ADR-049 (schema v36, ResearchLevels; levels shipped off) | the layout alone: one empty section-67 prefix | `8ccbe80f`→`35bb235f` | `392e2806`→`96ddd581` | `e3a86ce6`→`f4073f42` | `b2201d25`→`80ace282` | `f97432f0`→`f52a3298` | `ResearchLevelsLayoutAttributionTests` (`SchemaProjection.V35` = v35 value) |
| 8 | D-063 tranche 2 (ADR-047 effects, ADR-048 sweep and routing, ADR-049 levels on, pacing scale) | moves only the AI world: from turn 0 the AI targets other research | unchanged | unchanged | unchanged | `80ace282`→`d6b4a3cf` | unchanged | `AiWorldTurn300_WithTranche2Off_IsThePreTranche2World` (`TestConfigs.PreTranche2`) |
| 9 | ADR-050 (overflow), ADR-052, ADR-053 (AI gate goals) | new switches absent from the shipped `research.json` | none | none | none | none | none | their ADRs: "no golden moves" |
| 10 | ADR-051 (schema v37, ResearchReserves; switch off) | the layout alone: one empty section-68 prefix | `35bb235f`→`8abc32b0` | `96ddd581`→`af5be6a1` | `f4073f42`→`6b9571cd` | `d6b4a3cf`→`2a6a61d3` | `f52a3298`→`1e215573` | `ResearchReserveLayoutAttributionTests` (`SchemaProjection.V36`) |
| 11 | ADR-054 (schema v38; D-065 R6) | CP per turn from the RP base and the two-turn average population; section 69 | `8abc32b0`→`1f9f3d4f` | `af5be6a1`→`f23e0dcd` | `6b9571cd`→`389e0526` | `2a6a61d3`→`02500b4a` | `1e215573`→`175f55be` (layout alone) | `Adr054AttributionTests` (`TestConfigs.PrePerTurnCivics` through `SchemaProjection.V37`) |
| 12 | ADR-055 (schema v39, M7 recruitment; D-065 R7) | the military layer: the founding warband's 20 people and their grain; sections 70–73 | `1f9f3d4f`→`bb825342` | `f23e0dcd`→`f320440f` | `389e0526`→`df73d4f2` | `02500b4a`→`3153b7e3` | `175f55be`→`ff4ea411` (layout alone) | `MilitaryAttributionTests` (`TestConfigs.PreMilitary` through `SchemaProjection.V38`) |
| 13 | ADR-056 (M7 economy packets; D-065 R2) on the research tree | the packets' content rows, asserted inert | `1f9f3d4f`→`bd8a343b` | `f23e0dcd`→`d6862db0` | `389e0526`→`58f01705` | `02500b4a`→`56e8e867` | none (no goods) | `MilitaryAttributionTests` (`SchemaProjection.V38KeepingPackets`) |
| 14 | **M7 merge (research + units + economy)** | rows 12 and 13 together | →**`d5ebd0b2`** | →**`55afd498`** | →**`064ed6b7`** | →**`ac4554bf`** | `ff4ea411` | (a) packets off: `EconomyProjection.HashWithout(live)` = the units pin (`*PreEconomy` constants), asserted first in each pin test; (b) military off: `MilitaryAttributionTests` = the research+econ pin (V38KeepingPackets) and the research pin (V38) |

## 3. Secondary pins

These move with the rows above and are recorded in the ADR named, each with its own control: the twin-world values
(`AiFoundingAttributionTests.PreTrancheS1cTurn300`, `PreTranche2Turn300`; `DiscoveryLayoutAttributionTests.PreM6FoundedGolden`,
`PreM6AiTurn300` — ADR-054 §"Goldens move" and ADR-055 §"Moved pins"); the AI decision turns
(`AiEmpireIntegrationTests`, `AiAgeAdvancementIntegrationTests`, `AiPolicyTests` — ADR-045 §6, ADR-054); the small CLI world
(`RunFromSaveCliTests` — ADR-046 §5); `ConstructionHoldsV33FixtureTests.PremiseIV` (ADR-054).

## 4. Open items

- The M6 closure tracks running on 2026-10-28 may move these pins again; each records its own old → new → control table
  for the coordinator, who re-derives the combined values (the brief's rule). This register is updated with them at the
  exit gate.
- ADR-056 recorded EC-2 content-layout moves (`f87e2cf0`, `71fc678b`, `c6b6963c`) as "not re-pinned here"; they are
  absorbed by row 14's combined pins.

## Dated note — 2026-10-28 (M6 integration: KNOW ADR-058 + FOG D-066 A18 combined; MEASURED on the integrated tree)

| pin | KNOW only (A18 off) | FOG only (KNOW off) | COMBINED (shipped) | both off (base) |
|---|---|---|---|---|
| `S1cTurn300` | `a519b5c9…` | `7aed09b9…` | `5458cb3d…` | `ac4554bf…` |
| `S1cTurn300PreEconomy` | `5edcc3e0…` | `2b4d88e0…` | `a56313fc…` | `3153b7e3…` |
| AiEmpireIntegration levy / A3 | 380/381, 343 | 381/382, 345 | 373/374, 344 | 380/381, 345 |
| AiEmpireIntegration foundings | 38, 78, … 367 | 44, 80, … 366 | 44, 80, 116, 152, 188, 224, 295, 366 | 38, 78, … 367 |

Controls (exact): `TestConfigs.PreKnow` returns the FOG-only column, `TestConfigs.PreAiKnownTerrain` the KNOW-only column,
both together the base. Founded, driven and first-reign goldens unmoved. ci.yml AI leg: 351 orders, kind set unchanged.

## Dated note — 2026-10-28 (M6 exit audit: completeness)

- **§1 "Base tree now"** is superseded for the AI world: the shipped value is `5458cb3d…` (row above). The founded, driven and first-reign pins are unchanged (`d5ebd0b2`, `55afd498`, `064ed6b7`).
- **`RunFromSaveCliTests`, AI colony founding turn 76 → 77** (CLI `sim run` from a save). The cause is the combined M6 AI-world change: KNOW's Eurekas plus FOG's A18 known-terrain valuation.
  - Control: the in-process AI world's twins (`PreKnow` returns the FOG-only world, `PreAiKnownTerrain` the KNOW-only world, both exact) prove each layer's effect on the same AI.
  - The CLI path has no per-layer switch, so the 76 → 77 split between the two layers is NOT measured. This is recorded here, not hidden.
- **`DiscoveryTests` realized concealed share, seed 1, turn 300: 9/38 → 9/36.** Cause: A18 changes the AI's foundings. Control: `RealizedConcealedShare_Seed1Turn300_WithTheKnownTerrainValuationOff_IsThePreA18Count`, recorded in the ADR-044 dated note.

## Dated note — 2026-10-28 (D-067; documentation only, appended, the text above is unchanged)

- **ADR-058 §C is corrected by this register's own measured rows.** ADR-058 §C ("Goldens") says both of its parts move
  "the founded, driven, first-reign and AI-world pins". The measured record says otherwise: only the AI-world pins moved
  (`S1cTurn300` `ac4554bf…` → `5458cb3d…`, `S1cTurn300PreEconomy` `3153b7e3…` → `a56313fc…`, with the AI decision turns
  in the integration note above). The founded, driven and first-reign pins are unchanged at `d5ebd0b2`, `55afd498` and
  `064ed6b7` (this register's two 2026-10-28 notes above; re-read on 2026-10-28 in `SnapshotTests.FoundedGoldenHash`,
  `DrivenGoldenTests.Golden`, `FirstReignTests.PostR1Golden`, `AiFoundingAttributionTests.S1cTurn300` and ci.yml
  `FOUNDED_GOLDEN`). ADR-058 §C was corrected to this record in the same pass (ADR-058 amendment log, 2026-10-28).
- **No pin value is changed by D-067.** D-067 is governance, scheduling and documentation
  (`docs/d067-m6-exit-gate-director-rulings.md`: "No gameplay behaviour is changed"); this note adds no row to §2. This
  is read from the ruling, NOT measured: the pass that wrote this note ran no build or test. D-067 §7 also lists an edit
  to the `military` description string in `Sim.Data/content/sim.json` (no value changes); whether any pinned hash reads
  that string is for the validation run to confirm.
