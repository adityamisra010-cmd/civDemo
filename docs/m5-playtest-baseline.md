# M5 PLAYTEST BASELINE / DECISION RECORD

**Status:** APPEND-ONLY BASELINE RECORD. Created 2026-10-04 on the Director's instruction of 2026-10-04 ("Before
I personally play-test the current M5 build, create a complete, append-only M5 PLAYTEST BASELINE / DECISION
RECORD"). This record changes no behaviour. Do not edit it in place. Append dated sections below §13 instead.

**Purpose.** This is a forensic baseline. If something in a playtest behaves unexpectedly, this record should
let you decide which of these it is:

- a known behaviour;
- a ratified decision;
- a new regression;
- a calibration problem;
- a documentation mismatch;
- an architectural bug.

**Applies to every player, human or AI.** The baseline describes the simulation, and the simulation does not
care who issues an order. Every rule below holds equally for the human player and for AI polities unless this
record names an asymmetry. Every asymmetry found is listed in §3.2.

**Labels.** Every claim carries one of these labels.

| label | meaning |
|---|---|
| **RATIFIED** | A Director ruling, with a cited record. |
| **IMPLEMENTED** | Present in the code or content at `bc87ef8`, verified by reading the tree. The file is cited. |
| **MEASURED** | Run by this record's author on `bc87ef8` (or a byte-identical build), 2026-10-04. |
| **CARRIED** | A measurement made by an earlier stream and recorded elsewhere. It is cited and was not re-run here. |
| **OBSERVED** | A playtest or run observation. It is not a ratified target. |
| **KNOWN BUG** | A defect that exists in the tree at `bc87ef8`. |
| **OPEN** | An open question, awaiting a ruling or work. It is **not** an assumption. |
| **DEFERRED** | Ruled out of M5, or with no mechanism in M5. |
| **INFERRED** | This author's reading, not measured and not ruled. |

---

## 0. Where the Director's instruction text is out of date relative to the tree

The Director's instruction was written from an earlier state of the work. The tree at `bc87ef8` differs in the
places below. This record follows the tree and says so wherever the two disagree. It does not copy the text.

| # | Instruction text | What the tree says at `bc87ef8` | Where |
|---|---|---|---|
| D1 | §6: "the shipped setting currently keeps the cereal-farming-related yield active because the alternative gathering-only setting could not sustain the founding population" | **Out of date.** Since R4a (`07832a4`), `sim.json farming.preCultivation` is **ON**. The rates are 4.3 per gatherer and 2.0 per fertility-weighted km². The founding population is unchanged at 400 per settlement. Turn-1 food is wild food, and the cereal yield (26.0 / 5.0) applies only once `activity.farming` is knowledge-eligible. The failure the instruction describes is R3's founding-population sweep (400/200/100/50 at the R2a rates 1.0 / 2.0). R4a's yield sweep replaced it. | §6 |
| D2 | §6: "this crash must be fixed before that configuration is enabled"; §7 "exact commit fixing it, once fixed" | **Fixed already**, in `e6b7e42`, before the forager layer shipped in `07832a4`. A regression test exists. | §7 |
| D3 | §6: "The exact final calibration is still OPEN until fixed and validated" | The values were chosen against the **ratified** bands, and those bands pass: calibration battery and CI calibration job green at `bc87ef8`. The **20-seed nightly sweep has not been run** on this tree, and the quarantined corridors stay quarantined. Final calibration status is therefore **PROVISIONALLY VALIDATED, not closed** (see §6.5). | §6 |
| D4 | §8: "Ratify and record these decisions 1–8" | Rulings 1–7 are already **RATIFIED** in `docs/d048-knowledge-persistence-rulings.md` (2026-10-04). Ruling 8 was not stated separately there; ruling 7 mentions annexation. It is added now as an append-only note in that file. | §8 |
| D5 | §5: food, amenities, public services, development and institutional/state capacity "can offset" the burden | **Only partly implemented.** The shipped offset reads food (Sustenance), shelter, and comfort/amenities (pottery, cloth). Public services, development and institutions feed **nothing**. State capacity (administrative reach) enters only as a *reduction of the effective rate*, not as an offset. A **100 % levy at full reach is a deterministic revolt corner** (happiness = 0). | §5 |
| D6 | §4: "AI first-tax timing changed … because the new Taxation path is cheaper" | **Only true for the AI's goal metric.** The AI prices a goal as the union of every OR branch's ancestors. Under that metric the old four-node gate cost 44,480 RP of base content cost and the new one 19,010. The cheapest single path from zero is the other way round: old gate 6,600 RP via `standard_weights`, Taxation 11,970 RP. So the change made taxation **earlier for the AI** but **dearer for a human taking the cheapest path** (computed from `research.json` base costs, §4.4). | §4 |
| D7 | §4: "Taxation is positioned before Written Law according to the current tree" | **Only in depth and cost.** Both are A3 Civics. Taxation is depth 6 / 1,050 RP and Written law (`law_code`) is depth 7 / 1,240 RP. **Neither is a prerequisite of the other.** Written law requires `cuneiform OR hieroglyphic OR chinese_script`. | §4 |
| D8 | §4: "no tax in the Stone Age" | True **at founding and by prerequisite depth**: every Taxation path needs an A3 script. It is **not enforced by Age state**: research availability ignores the polity's current Age (`ResearchQuery.IsAvailable`), so a polity still in A1 or A2 that completes Taxation can levy. **MEASURED:** in the `ci.yml` AI leg the AI first levied at turn 369 while in A2, and entered A3 only at turn 465. | §4 |
| D9 | §3: "the 12 starting settlements are approximately split" | With one AI empire the split is **exactly 6/6, interleaved by siting rank**. The player (id 1) holds rows 0, 2, 4, …; the AI (id 2) holds rows 1, 3, 5, …. It is not a geographic split. | §3 |
| D10 | §3: "AI empires do not spontaneously appear mid-game" | True for worldgen-founded rivals. However, **every revolt founds a new AI polity mid-game** (`RevoltSystem`, D-048). That polity is CommandSource.Ai and is driven by the same AI producer. | §3 |
| D11 | §3: "worldgen.json … aiEmpires default 0" (instruction to verify) | `worldgen.json` has **no `aiEmpires` key**. The default 0 comes from the C# record default (`WorldgenConfig.AiEmpires = 0`). | §3 |
| D12 | §9: "Trade research/content follow-up if still outstanding" | Trade node 426 is **IMPLEMENTED**. Its follow-up content (the full Trade chain) is still DEFERRED. | §9 |
| D13 | §1: test counts to be "measured by R5 (Sim.Tests 1460/0/6, Sim.Ui.Tests 473/473)" | No repository record holds the R5 counts. They are an orchestrator report (CARRIED). This record re-measured them on `bc87ef8` (§1.3). | §1 |

---

## 1. The exact baseline

### 1.1 Identity

| item | value | label |
|---|---|---|
| Date of record | 2026-10-04 | — |
| Branch played | `m5-integration` (local and `origin/m5-integration` both at `bc87ef8`, checked 2026-10-04) | MEASURED |
| HEAD commit | `bc87ef8a87943fe2b54984895378a76135c8ebd1` "docs(readme): fix duplicated word in the build-3 paragraph" | MEASURED |
| Build commit | `5c364b515c2b8176ce7aa52744998f0691be2426` "test(r5): re-pin the AI levy 546/547 -> 367/368 …" (branch `m5i-r5-taxation-node`) | MEASURED |
| Build artifact | `sim-ui-win-x64-5c364b5`, Windows x64 zip, 81,567,886 bytes, digest `sha256:5678d4acbfe06f2cc42501791348f3acf5345dbd222063d0c07f938627260e44`, created 2026-10-04T12:44:47Z, expires 2026-11-03T12:44:43Z | MEASURED (GitHub API) |
| Build/download identifier | `ui-artifact` workflow run **37203130829** (workflow_dispatch on `m5i-r5-taxation-node` @ `5c364b5`, conclusion success) | MEASURED (GitHub API) |
| Build commit vs HEAD | `git diff --stat 5c364b5 bc87ef8` = `README.md` only (4 insertions, 4 deletions). The played binary is functionally `bc87ef8`. | MEASURED |
| Parent / integration commits | R5: `9f7c82b` (Taxation civic), `5c364b5` (AI levy re-pin). R4b merge `3570a24` (roadmap). R4a: `e6b7e42` (ledger fix), `70900b5` (D-048), `07832a4` (forager ON), `95982bd` (tax offset), `9648f7b` / `c5575e8` (re-pins, closure record). | MEASURED (git log) |
| Working tree | Clean at `bc87ef8` before this record (`git status`: nothing to commit). This record is committed on the local branch `m5i-playtest-baseline` and is **not pushed**. | MEASURED |
| Window-title label in the build | `civ-sim M4 (<sha>, <date>)` (`Sim.Ui/BuildInfo.cs:18`). The "M4" label is stale; see §11. | IMPLEMENTED |
| Schema | canonical schema v31 (`sim inspect` on the AI leg session) | MEASURED |

**To reconstruct what you played:** download the artifact above, then run `Sim.Ui.exe` with your flags. Each
session writes these files under `runs/`:

- `orders-<stamp>[-sPX][-nN][-aN].bin`
- `chronicle-<stamp>…txt`
- a session manifest

`sim replay --founded --seed S [--ai-empires N] --orders runs/orders-<stamp>.bin --turns T` built from
`bc87ef8` reproduces the world hash-for-hash (README "Download & Play").

### 1.2 GitHub CI on the baseline commits (MEASURED via GitHub API, conclusions only; the job logs could not be fetched from this session)

| run | commit | jobs |
|---|---|---|
| CI 37203237727 (push, `m5-integration`) | `bc87ef8` | build-and-test success; determinism success; determinism-xproc success; calibration success; calibration-nightly **skipped** |
| CI 37201461243 (push, `m5i-r5-taxation-node`) | `5c364b5` | success |

### 1.3 Tests (MEASURED on `bc87ef8`, Release, this machine, 2026-10-04)

**MEASURED on `bc87ef8`:**

| suite | passed | failed | skipped | total | duration |
|---|---|---|---|---|---|
| Sim.Tests | **1460** | **0** | **6** | 1466 | 23 m 17 s, under load |
| Sim.Ui.Tests | **473** | **0** | 0 | 473 | — |

The suites ran with `dotnet test … -c Release --no-build`. Raw results are in §1.6.

CARRIED, for comparison:

- R5 (orchestrator report, no repository record): Sim.Tests 1460 passed / 0 failed / 6 skipped; Sim.Ui.Tests
  473/473.
- R4a at `9648f7b` (`docs/r4a-m5-closure-record.md` §9): 1458 / 0 / 6 and 473/473.

The 6 skips are manual measurement rigs:

- `FoundingVariationItem0Tests`
- `WaterRouteCounterfactualTests` ×2
- `HousingBeforeColumnTests`
- `GovernancePushbackMeasurement`
- `ScarcityConflictReachabilityMeasurement`

### 1.4 Gates and consistency checks (MEASURED on `bc87ef8`, 2026-10-04)

| gate | result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 errors |
| `scripts/check-banned-constructs.sh` | exit 0, "no banned constructs found" |
| `scripts/check-read-isolation.sh` | exit 0 |
| `scripts/check-readonly-proof.sh` | exit 0, "mutation attempts fail to compile (CS0200, CS1061)" |
| `scripts/research-content-audit.py --check` | exit 0, `docs/research-corpus-audit.md` is current |
| `scripts/research-calibration-report.py --check` | exit 0, `docs/research-calibration-report.md` is current |
| `scripts/research-gameplay-unlock-audit.py --check` | exit 0, `docs/research-gameplay-unlock-audit.md` is current |

### 1.5 Determinism, replay, save/load, AI run, goldens, benchmark

**Determinism (MEASURED).** The `ci.yml` determinism-xproc step was run locally, verbatim, against the Release
CLI built from `bc87ef8`:

| leg | result |
|---|---|
| Orderless, 2 processes × 400 turns | byte-identical, final `7f93ac50…` |
| Ordered vs replay, 400 turns | byte-identical, final `86a31edf…` |
| Founded, 2 processes × 300 turns | byte-identical, final `02c7f9eb0d08bf0ab31e6a9b0afb64041e4283e1fc4af1d4fbc94c8fa7b10ef2`, which equals `FOUNDED_GOLDEN` |
| Founded ordered vs replay, 300 turns | byte-identical, final `924059ca…` |
| AI leg: `--ai-empires 1`, player `--founded-mix` orders, 600 turns | 2 processes byte-identical (hash logs and run logs), final `2954c51f…`; run log 194 orders, kinds [3, 4, 5, 6, 7, 8]; `sim replay` of the run log identical; `sim inspect` result in §1.6 |

**Save/load (IMPLEMENTED + CARRIED).**

- `IntegratedSaveLoadBatteryTests` (8 tests) is in the suite run. It covers a dev world with aiEmpires = 1 and
  every player order kind, with save points:
  - mid-research;
  - after a completion;
  - mid partial road;
  - mid university maturation;
  - tax before issue, issued, and in force;
  - Age before decision, at decision, and entered.
- `ResearchUnlockPipelineTests.T17` (save/load preserves capabilities, including `taxation`).
- `UnrestTests.CapitalLoss_SaveLoadAndReplay_AreExact`.
- The **UI does not write snapshots.** A played session is saved as its order log plus manifest. `--resume`
  re-founds and **replays** it, checking each turn's hash against the saved trace and throwing on divergence
  (`UiSession.Resume`, `Sim.Ui/UiSession.cs:686-742`).

**Replay (IMPLEMENTED + MEASURED).** Order logs are the replay input. The AI producer is not re-run on replay,
because the log carries the AI's orders (`AiOrders` header; `UiSession.Resume`). Turn-exact replay of the tax
gate is covered by `ResearchUnlockPipelineTests.T18_Replay_PreservesCapabilityAvailability_TurnByTurn`.

**Golden hashes (verified in source; the founded pin MEASURED by the CLI legs above).**

| pin | value | where |
|---|---|---|
| founded seed 42 × 300 | `02c7f9eb0d08bf0ab31e6a9b0afb64041e4283e1fc4af1d4fbc94c8fa7b10ef2` | `SnapshotTests.FoundedGoldenHash` (`Sim.Tests/Kernel/SnapshotTests.cs:1057`); `ci.yml` `FOUNDED_GOLDEN` (line 186) — they agree |
| first reign × 40 | `74a97abc39d1191061a2c39748c870faf62d308a8d2cc7b3d28feff8b35e4419` | `FirstReignTests.PostR1Golden` (`Sim.Tests/Systems/FirstReignTests.cs:28`) |
| driven seed 42 × 300 | `3a9f007aeeb6dcd9c28efd3c47492b66d19a863057f5cfcba80c3d0c52ca12dd` | `DrivenGoldenTests.Golden` (`Sim.Tests/Kernel/DrivenGoldenTests.cs:85`) |

- All three were last moved by R4a (forager layer). `TestConfigs.PreForager` returns the R3 pins (founded
  `1368df9f…`, first reign `680a20c5…`, driven `efeec45d…`).
- R5 moved **no** aiEmpires = 0 golden (`5c364b5` commit message; consistent with the founded leg above).
- R5 moved the AI-world pin in `AiEmpireIntegrationTests`: levy 546/547 → 367/368.

**AI deterministic run (IMPLEMENTED + MEASURED).**

- In-suite: `AiEmpireIntegrationTests` (canonical world, seed 42, aiEmpires = 1, no player orders, 600-turn
  horizon). It pins these turns:

  | event | turns |
  |---|---|
  | second research target | 26 |
  | first granary order | 9 |
  | Age 2 decided / in force | 238 / 239 |
  | road ordered / applied | 143 / 144 |
  | levy ordered / positive rate | 367 / 368 |

- It also replays the log with no AI producer, hash-identical.
- Cross-process: the AI leg above.

**Benchmark (NOT RUN on `bc87ef8`).** The last measurement is R4a's at `9648f7b`: `sim bench --seed 42 --turns 300
--founded` took 29,860.75 ms against 29,167.07 ms at `12754e3` (+2.4%), on a shared machine (CARRIED). R5 changed
content and tests only, so no hot path. Bench impact is INFERRED nil and was not measured.

### 1.6 Measured results appended at completion of this record's runs

See "§1.6 results" at the end of this record.

---

## 2. The M5 gameplay model as implemented

Each mechanic below separates IMPLEMENTED behaviour, RATIFIED decisions, KNOWN LIMITATIONs (including KNOWN
BUGs) and DEFERRED behaviour. "Player/AI" notes any asymmetry; §3.2 is the full list.

### 2.1 Founding: population, settlements, capital, warband

**IMPLEMENTED.**

- **Settlements.** `worldgen.json siting.settlementCount` = 12, sited best-first by a jittered score
  (`SettlementSiting`, composite key (score DESC, cell ASC)).
- **Founding population.** `sim.json founding.cohortCounts` sums to **400 per settlement**: the T4.19 stable age
  vector [57, 56, 51, 45, 40, 34, 29, 24, 19, 15, 11, 8, 5, 3, 2, 1]. Each settlement's total and cohorts are
  jittered by `endowmentJitter` 0.69 (a settlement-common factor and a per-slot factor; `WorldFounding.Jittered`).
- **Founding food.** `founding.foodStore` 6,000 grain per settlement, jittered.
- **Founding housing.** Every settlement is founded housed: dwellings = round(population / personsPerDwelling),
  via `InitialEndowment`.
- **Polities.** The player is always `PolityId(1)` (CR-011). AI empires are `PolityId(2 …)`.
  `FoundInitialEmpire` gives settlements out round-robin (`holder = 1 + s % (aiEmpires + 1)`).
- **Capital.** Each empire's capital is the **first settlement it receives**.
- **Warband.** Each empire that holds a capital fields `unit-families.json founding.formationsPerPolity` = **1**
  warband at its capital. It is a formation token: it draws no people and no goods (`FoundInitialFormations`).
- **Founding Age.** A1 Prehistoric / Stone Age (`ages.json foundingAge` 1). No research node is complete at
  founding (D-044 T7).

**RATIFIED.** CR-011 (player id 1). T4.19 founding demographics. M4 §11 (one player empire plus N AI).
ADR-031 / D-047 (founding warband).

**KNOWN LIMITATION.** The round-robin split is an acknowledged placeholder (`WorldFounding.cs:296-306`: "Who
deserves the better ground is a real design question and this is not an answer to it"). Because sites are
ranked best-first, the player receives the best-ranked site as capital and the higher-ranked site of every
pair. This is an advantage in the player's favour; it is INFERRED and its magnitude has not been measured.

### 2.2 Primitive food, gathering, farming / agriculture

**IMPLEMENTED (R4a, `07832a4`).**

- `sim.json farming.preCultivation`: `enabled: true`, `yieldPerArableKm2PerYear: 2.0`,
  `outputPerGathererPerYear: 4.3`.
- `ProductionSystem.Farm` takes min(land side, labour side). While a settlement's controller knowledge does not
  make `activity.farming` eligible, it uses the forager rates. Once farming is known, it uses the cultivated
  CR-003 rates: `yieldPerArableKm2PerYear` 26.0 and `outputPerFarmerPerYear` 5.0.
- `activity.farming` requires
  `cereal_cultivation OR root_crop OR rice_wet OR millet OR maize OR sorghum_pearl_millet`. The cheapest route is
  `root_crop` (A2, depth 0, 440 RP, no prerequisite). The A2 core milestone is `cereal_cultivation` (3,520 RP,
  closure 3,890).
- One predicate, `LabourActivities.HarvestsWildFood`, feeds both production's yield choice and the Policy panel.
  The turn-1 Farming sector reads "Gathering — makes wild food (grain)". Internally it is still the grain good.

**RATIFIED.**

- D-046 G3: initial food production is not completion of agriculture.
- R2a decision 8: "Do not fabricate an agricultural yield while claiming Agriculture is unavailable".
- Director 2026-10-04 (R4): raising primitive gathering yield is the preferred, legitimate lever, and agriculture
  stays researched.
- Director 2026-10-04 (this instruction): increasing hunting/gathering yield is a legitimate calibration lever and
  is **not architecturally forbidden**. The only requirement is that primitive gathering remains a baseline
  subsistence capability and does not falsely grant researched Agriculture.
- The shipped implementation satisfies that requirement: the forager rates are a separate content block,
  selected by knowledge eligibility, and grant no research.

**KNOWN LIMITATION.**

- The all-food 900-turn rig reaches the forager land ceiling and starves (R4a §1).
- A settlement whose food-sector share is cut hard on wild food starves. In `FoundedHarnessTests`, a 30 % farm
  order from turn 34 drives settlement 0 to total deprivation and revolt at turn 58.
- Herding/fishing (`activity.herding`) and logging/mining are research-derived *labels* on unchanged sectors
  (ADR-033 D1).

**DEFERRED.** A cultivation-adoption mechanism (ADR-033 D1 option b) is not built.

### 2.3 Research and research-gated actions

**IMPLEMENTED (ADR-029/030, R1, R2a, R5).**

- **Trees.** 426 Technology nodes and 7 Civics nodes, 433 in total (`docs/research-gameplay-unlock-audit.md` §2).
- **Research points.** `RP/turn = 0.08 × population^0.699` per strategic turn. ADR-030 makes this a scoped
  exception to the dt law.
- **Cost.** One target per polity, set by the `SetResearchTarget` order (kind 6). Cost model U × K^magnitude,
  with U = 92.4 and K = 2.
- **Availability** is prerequisites only, plus the research stage for the five university subtrees. **Availability
  does not read the polity's current Age** (`ResearchQuery.IsAvailable`).
- **Gated actions.** One predicate per action, shared by order validation, the UI action surface
  (`AvailableActionsQuery`) and the AI:
  - production recipes: `recipe.pottery_firing` ← `pottery_open_fired`; `recipe.bronze_casting` ← `tin_bronze`;
  - the labour activity labels;
  - trade (`activity.trade` ← node 426 `trade`);
  - the tax edict (§4);
  - road classes;
  - construction projects (granary baseline; universities);
  - Age milestones.
- **Uncontrolled settlements** (city-states) research under their own key at 0.25 pace
  (`research.json tuning.cityStatePaceFraction`).

**RATIFIED.** D-044/045/046/047. ADR-029/030. R2a items 1, 4, 12, 13.

**KNOWN LIMITATION.** 115 research entities are declared in content but realized by no system (§9).

**DEFERRED.** M6 owns the review and completion of the research engine (§10).

### 2.4 Taxation, happiness, amenities, food availability, legitimacy, unrest, revolt

See §4 (the gate) and §5 (the model). Summary of what is IMPLEMENTED:

- **Effective rate** = declared rate × administrative reach. Reach is `ControlRow.Strength` = exp(−travel cost
  from capital / 25), and the capital itself is 1.0.
- **Extraction.** Output × (1 + 0.3 × effective rate).
- **Happiness** (0–100) = CES(food sufficiency, housing sufficiency) × (1 − effective rate). It is a derived
  reading, never stored. **Amenities and comfort are NOT in happiness** (`SettlementHappiness` header: comfort is
  deferred).
- **Legitimacy** = population-weighted happiness of the settlements an empire holds. Its consumers are the AI tax
  valve and the UI display only.
- **Dignity** need = 1 − r × (1 − 0.5 × P) (R4a). It feeds grievance.
- **Protest** p from the tax-attributed grievance, linear between 15 and 50. Protest drags output, vents
  grievance, and at p = 1 causes an uprising.
- **Revolt** has three paths:
  - happiness = 0 by deprivation (unfed AND unhoused);
  - happiness = 0 by a 100 % levy at full reach;
  - an uprising.
  
  A revolt is refused when it would take the ruler's last settlement (D-048 ruling 5).

### 2.5 Institutions and universities

**IMPLEMENTED (ADR-033 D6, schema v31).**

- **Types.** Five university types: military, medical, engineering, natural science, agricultural.
- **Founding.** Founded by the `EnqueueConstruction` order (projects 11–15: 1,800 timber, 1,200 stone, labour
  240).
- **Research gates.**
  - `building.university` requires `medicine_hippocratic AND (geometry_axiomatic OR algebra)`.
  - `inst.university` requires `library AND legal_code_roman`.
- **Market threshold.** adults ≥ 2,000 × (hosted + 1) to found.
- **Lifecycle.** Maturity tau 16 years, decay tau 21.7 years; staff 5 % × 2,000 × maturity.
- **Effects.**
  - A branch research-cost reduction of up to 1/3 (`ResearchCostModifierRow`).
  - A medical mortality reduction of up to 10 %, diffusing by travel cost / 25.
- **No other institution type is realized.** For example `inst.law_code` and `inst.census` are DEFERRED entities.

**RATIFIED.** ADR-028; D-047 ruling 9 (five university types).

**KNOWN LIMITATION.** Institutions do **not** feed happiness, legitimacy or the tax offset (§5).

### 2.6 Roads

**IMPLEMENTED (ADR-032, transport FROZEN at `997824b`).**

- Order `DevelopRoads` (kind 8). Classes track / built / paved / macadam / highway, each gated by an
  `infra.road_*` entity. The track road requires `track_road`, whose closure costs 3,850 RP.
- Materials are per km. Routes are capped at 600 km.
- Roads cut `SettlementDistances` travel cost. That raises administrative reach, and therefore the effective
  tax, with a timing pin (`GovernanceSystem` header).
- Weather correlation uses geographic distance, not roads (R2b).

**KNOWN LIMITATION.** Straight-line route geometry (transport frozen).

### 2.7 Colonies

**IMPLEMENTED (T4.4, D-037 B1).**

- **Trigger.** `ColonizationSystem` founds a settlement only from migration's **unplaced departure demand**:
  people with no reachable, viable destination.
- **Control.** Inherited from the parent: a controlled parent gives a controlled colony, a stateless parent a
  stateless colony.
- **No player or AI order founds a colony.**

**CARRIED.** On the canonical founded world, 0 colonies in 300–500 turns on seeds 42, 7 and 1234, with and
without one AI and a 40 % tax (`docs/m5-integration-coherence-matrix.md` §6.5, measured at R2b, before the forager
layer). **Not re-measured since R4a** (OPEN).

### 2.8 Age progression and unit Age conversion

**IMPLEMENTED (ADR-031).**

- **Age is computed state.** A polity's Age changes only when (a) the `ages.json` entry requirements evaluate
  true (all core milestones, ≥ `supportingRequired` supporting milestones across ≥ `minCategories` categories)
  **and** (b) it issues an `AdvanceAge` order (kind 7, applied next turn).
- **The player is never auto-advanced.** The AI advances on its first eligible turn with the lowest surge key
  (`AgeAdvancePolicy`). Surges are stored, but **have no numeric effect** (`ages.json` _doc; DEFERRED).
- **Units.** At each Age entry the founding warband line converts for free to that Age's identity (ruling 18).
- **Military milestones.** Every "military realization" milestone requires a formation realized at or after that
  Age (`minIdentityAge`), and is **PENDING Battle Layer (M7) recruitment**. No such milestone can hold in M5.
  Every Age still keeps ≥ minCategories non-military categories (coherence matrix §6.6). Reachability of A3–A9 in
  play is INFERRED (never run to those Ages).
- **UI era theme** = f(the player's current Age), derived and never stored (ADR-033 D8).

**CARRIED.**

| world | A2 decided / in force |
|---|---|
| Canonical AI (no player orders) | 238 / 239 |
| Dev world, A2 eligibility | AI 394, player 346 (R4a §8) |

**KNOWN LIMITATION.**

- Age thresholds are PROVISIONAL (`ages.json status`).
- A **revolted polity starts at Age A1** whatever its parent's Age, because `AgeQuery.CurrentAge` returns
  `foundingAge` when a polity has no Age row and `RevoltSystem` copies research only. It holds the parent's
  knowledge and may advance by orders. This is IMPLEMENTED behaviour with no ruling (OPEN).

### 2.9 Knowledge persistence, revolt, capital loss

See §8. Revolt and capital loss are summarized here.

**IMPLEMENTED.** A revolted settlement's control row passes to a **new AI polity**:

- Its id is the roster maximum + 1.
- Several revolts on one turn are handled in settlement-table order.
- The new polity has **no capital**, gets a complete copy of the parent's completed research, starts at Age A1
  (§2.8), has no warband, and its effective tax is 0 (no capital, so no reach).

The parent keeps its capital row even when the capital revolts. A **lost seat administers nothing**: its reach
is 0 everywhere, so no remaining settlement is taxed (`UnrestTests.CapitalLoss_*`;
`GovernanceTests.AnEmpireThatNoLongerControlsItsCapital_ReachesNothing_SoItsTaxFallsNowhere`).

Revolted settlements refuse the former ruler's labour and construction orders, because every order domain asks
the control relation.

**DEFERRED.** Capital succession or relocation; recovery, re-annexation and reconquest of revolted settlements;
conquest.

### 2.10 AI

See §3.

### 2.11 Save/load, replay

See §1.5. UI save = order log + manifest + trace. Resume = replay with hash check. The CLI also has snapshot
save (`--save-at K --save PATH`).

### 2.12 Visual / map

**IMPLEMENTED.**

- The UI era theme is derived from the player's Age (D8).
- Roads are drawn by class (U3).
- Walls come from state, not from the Age label (D-038 H5).
- Debug panels sit behind F12 / `--dev` (D9).

**KNOWN BUG (developer tooling, not the game).** `Sim.Ui --action-preview` and `--player-views-preview` crash at
`bc87ef8` with `InvalidOperationException: action preview rig: could not declare a 20% levy`
(`ActionSurfacePreview.LaterRig`, `Sim.Ui/Actions/ActionSurfacePreview.cs:63`). Both were MEASURED to exit 134.

- **Cause.** The rig grants the ancestry of `arithmetic_babylonian`, which since R5 no longer opens the tax gate.
- `--r2a-preview` still runs (MEASURED exit 0). Its "pre-trade" description claims taxation is known, which is
  now false.
- No test covers these rigs.
- Not fixed here (no code changes in this record).

---

## 3. AI-rival configuration

### 3.1 Exact behaviour (IMPLEMENTED, verified in code)

| statement | verified | where |
|---|---|---|
| AI empires are created only at world generation | **TRUE for founded rivals.** `FoundInitialEmpire` is the only place that creates rival empires. **But** `RevoltSystem` creates a new `CommandSource.Ai` polity at every revolt (D-048), mid-game. | `Sim.Core/Worldgen/WorldFounding.cs:310-345`; `Sim.Core/Systems/Revolt/RevoltSystem.cs:169` |
| `--ai-empires N` controls the number | TRUE: UI (`UiArgs.Parse`, N ≥ 0) and CLI `run`/`replay` (requires `--founded`). The override is recorded in the session manifest and log name (`-aN`). | `Sim.Ui/UiArgs.cs:59`; `Sim.Cli/Program.cs:175`; `Sim.Core/Kernel/SessionManifest.cs` |
| Without the flag there are zero AI empires | TRUE. `worldgen.json` has no `aiEmpires` key; the C# default is `AiEmpires = 0`. | `Sim.Core/Worldgen/WorldgenConfig.cs:55` |
| With one AI empire the 12 settlements are split between two civilizations | **Exactly 6/6**, round-robin over the siting-ranked rows: player rows 0, 2, …, 10; AI rows 1, 3, …, 11. Interleaved, not geographic. | `WorldFounding.cs:323-329` |
| Each civilization gets its own capital | TRUE: the first settlement it receives (player row 0, AI row 1). An AI receiving none (rivals > 11) is founded empty and immediately extinct. | `WorldFounding.cs:331-345` |
| Each gets its founding warband | TRUE: 1 per empire with a capital. A revolt-founded polity gets none. | `WorldFounding.FoundInitialFormations` |
| New settlements can arise later as colonies | TRUE. Control is inherited from the parent, so an AI parent gives an AI colony. Rare in measured play (§2.7). | `ColonizationSystem` |
| AI empires do not spontaneously appear mid-game | TRUE for rivals. A revolt is not spontaneous: it is caused by deprivation, a 100 % levy, or an uprising. Each revolt **does** add an AI polity. | as above |

**How the AI decides (IMPLEMENTED, `Sim.Core/AiOrders.cs`).** For every `CommandSource.Ai` polity that is not
extinct, in roster order:

1. `AiResearchPolicy`: goal-sequential. The goals are the next Age core, the next road class, the tax gate and
   the university. It picks the goal with the cheapest remaining closure (union over OR), then that goal's
   cheapest available node.
2. `AgeAdvancePolicy`: advance on the first eligible turn.
3. `RoadDevelopmentPolicy`.
4. `AiConstructionPolicy`: one affordable available project per idle settlement, and at most one viable
   university per polity.
5. Then `AiGovernance`, the tax valve.

Every AI decision is an ordinary order through the same validators and systems as the player's. It goes into the
log, so replay needs no AI.

### 3.2 Player / AI symmetry: "every decision applies whoever plays"

**SYMMETRIC (IMPLEMENTED).** Each of these is the same predicate and the same system for both: every rule of
the simulation, every order validation, the tax gate (`Governance.CanLevyTax`), research, Age eligibility,
revolt, the knowledge rulings, colonization and capital loss. Tests:

- `GovernanceTests.AiOrdersGoThroughTheSameValidationAndSystemAsThePlayers`
- `GovernanceTests.TheAiActsOnlyWhenItCanLevyTax`

**ASYMMETRIES FOUND (IMPLEMENTED unless labelled).** These are AI-only *decision policies*, or founding
differences. They are not different rules.

| # | asymmetry | detail | label |
|---|---|---|---|
| A1 | Research choice | AI: `AiResearchPolicy` (automatic, goal-sequential). Player: chooses manually, with no auto-research. | IMPLEMENTED |
| A2 | Tax valve | AI: `AiGovernance`. It raises the rate 5 points while legitimacy ≥ 60, lowers it while < 35, and **never exceeds 40 %** (`sim.json governance.ai`). Player: any rate 0–100 %, including the 100 % revolt corner. | IMPLEMENTED |
| A3 | Age advance | AI advances on its first eligible turn with the lowest surge. The player is never auto-advanced. | IMPLEMENTED |
| A4 | Roads | AI: `RoadDevelopmentPolicy`. Player: manual. | IMPLEMENTED |
| A5 | Construction | AI: `AiConstructionPolicy` (granary first, one university per polity at most per decision). Player: manual. | IMPLEMENTED |
| A6 | Labour allocation | **The AI issues no labour/sector orders** (coherence matrix E3, REMAINS). AI settlements run the never-ordered default mix forever. The player can reallocate (`SectorAllocation`, kind 3). | KNOWN LIMITATION |
| A7 | Starting position | Player id 1 takes the best-ranked site as capital and the higher-ranked of each pair (§2.1). | INFERRED advantage, not measured |
| A8 | Revolt-founded polities | AI-driven, capital-less, warband-less, Age A1, knowledge copied (§2.9). They are not peers of a founded rival. | IMPLEMENTED; OPEN for a ruling (Age A1) |
| A9 | Tax timing after R5 | The AI's goal metric (union over OR) made Taxation **cheaper** to target (old 44,480 → new 19,010 base RP). The cheapest single path got **dearer** (old 6,600 via `standard_weights` → new 11,970). So R5 moved the AI's levy earlier while raising the minimum RP a human needs to tax. | computed from `research.json` (§4.4) |

---

## 4. Taxation decision

### 4.1 The decision (RATIFIED — Director 2026-10-04, "taxation is supposed to be a researchable node"; implemented R5 `9f7c82b`)

**Taxation is its own research node, and it is the single authoritative tax gate.**

| field | value (IMPLEMENTED, `research.json` civics) |
|---|---|
| id / key | `taxation` / **1007** |
| tree / Age tag | **Civics** / **A3** (Bronze Age) |
| depth / cost | 6 / **1,050 RP** (T2, magnitude 3.5, no Eureka) |
| prerequisites | `token_counting AND stamp_seal AND (proto_writing OR hieroglyphic OR chinese_script)`, i.e. clay-token counting AND stamp seal AND any one early script (proto-cuneiform, Egyptian hieroglyphic, oracle-bone/Chinese script) |
| gate | `sim.json governance.taxationRequires = "taxation"`, evaluated by `Governance.CanLevyTax` |

- **Not tax unlocks any more.** `arithmetic_babylonian` (Babylonian arithmetic), `surveying` (land surveying),
  `standard_weights` (standardised weights) and `coinage_electrum` (coinage) keep their capability text ("tax
  assessment", "taxation by area", "by weight", "in coin") as later refinements. They **do not** gate the edict,
  alone or all together (`GovernanceTests.TheFourTaxationRefinementNodes_NoLongerOpenTheGate`, `T08b`).
- **For comparison.** Trade = node 426 `trade`, Technology tree, A3, depth 6, **880 RP**, prerequisites
  `token_counting AND (donkey OR camel OR sail_square)`.
- **Written law** = civic 1001 `law_code`, A3, depth 7, **1,240 RP**, prerequisites
  `cuneiform OR hieroglyphic OR chinese_script`.
- Taxation therefore sits *before* Written law **in depth and cost only**. Neither is a prerequisite of the
  other.

### 4.2 Gate properties (IMPLEMENTED; test evidence)

| property | status | evidence |
|---|---|---|
| No tax at founding (Stone Age, A1) | TRUE: Taxation is unavailable at turn 1 and every path needs an A3 script. **Not Age-enforced**: research ignores the current Age, so a polity in A1/A2 that completes Taxation can levy (§0 D8). | `ResearchUnlockPipelineTests.T08b` (turn-1 unavailable) |
| Tax orders rejected before Taxation | TRUE. `GovernanceSystem` applies a `SetTaxRate` only if `CanLevyTax` holds **on PREV**; otherwise the order changes nothing. A hand-built order is refused. | `T08b`; `GovernanceTests.WithoutTaxationKnowledge_TheEdictChangesNothing` |
| AI cannot bypass the gate | TRUE: the same system check, and `AiGovernance` returns no order without the gate. | `GovernanceTests.TheAiActsOnlyWhenItCanLevyTax`, `…AiOrdersGoThroughTheSameValidationAndSystemAsThePlayers` |
| Save/load preserves the gate | TRUE (knowledge is serialized ResearchCompleted). | `ResearchUnlockPipelineTests.T17_SaveLoad_PreservesCapabilities` |
| Replay preserves the gate | TRUE, turn by turn. | `ResearchUnlockPipelineTests.T18_Replay_PreservesCapabilityAvailability_TurnByTurn` |
| UI control appears only after Taxation | TRUE. The action surface lists the Governance action only when `CanLevyTax` holds, and `UiSession.EmitTaxOrder` refuses otherwise. | `Sim.Ui.Tests` `TaxControlTests`, `ActionSurfaceAgeAndTaxTests`, `ActionEmitterGuardTests` |
| Order timing | A `SetTaxRate` stamped turn t writes the policy into the state of t+1. Production, migration and revolt respond in the step t+1 → t+2. | `GovernanceTimingTests`; `GovernanceSystem` header |

### 4.3 AI first-tax timing (OBSERVATION, not a ratified target)

| world | before R5 | after R5 | source |
|---|---|---|---|
| Canonical world, seed 42, aiEmpires = 1, no player orders | levy ordered 546, positive rate 547 | **367 / 368** | `AiEmpireIntegrationTests` (pinned, measured by R5) |
| `ci.yml` AI leg (with `--founded-mix` player orders) | first `SetTaxRate` in run log 549 | 369 | `5c364b5` commit message (CARRIED) |

The instruction's "≈547 → ≈368" agrees with these figures.

**MEASURED 2026-10-04** by decoding the run log of the local `ci.yml` AI leg. The leg ran at `bc87ef8` with
player `--founded-mix` orders, 600 turns. The AI's orders were:

| turn | order |
|---|---|
| 239 | AdvanceAge → A2 |
| **369** | first SetTaxRate, 5 % |
| 370–376 | +5 % each turn, up to the 40 % ceiling |
| **465** | AdvanceAge → A3 |

**The AI therefore levies for about 96 turns while still in Age A2 (Neolithic).** This follows from §0 D8:
Taxation carries an A3 tag, but research is not Age-gated. It is not a Stone Age (A1) levy. Classification:
OBSERVED; OPEN for a ruling on whether the A3 tag should bind.

### 4.4 Why the AI levies earlier (computed from `research.json` base costs; EffectiveCost can differ by Eurekas/modifiers)

| measure | old gate (any of the four refinements) | new gate (Taxation) |
|---|---|---|
| AI goal metric: union-over-OR ancestor closure | 24 nodes, 44,480 RP | 16 nodes, 19,010 RP |
| Cheapest single path from zero | 6,600 RP (`standard_weights`) | 11,970 RP |

Classification: OBSERVED and computed. Not a ratified target, and not a defect.

---

## 5. Tax / governance model: intended versus implemented

### 5.1 Intended model (RATIFIED — Director 2026-10-04 R4 and this instruction §5)

- High taxation does NOT directly mean revolt.
- Tax burden leads to happiness and legitimacy pressure.
- That pressure is offset by food availability, amenities, public services, development and
  institutional/state capacity.
- High taxation may be sustainable for a developed civilization.
- 99 % should create severe pressure. Revolt remains an emergent downstream outcome, not a direct tax rule.

### 5.2 Implemented model at `bc87ef8` (IMPLEMENTED; R2b unrest-lite + R4a offset `95982bd`)

1. **Effective rate** r = declared × reach (`Governance.EffectiveTaxRate`).
2. **Happiness** = 100 × normalized CES(food, housing) × **(1 − r)**. The tax multiplies happiness
   **without any offset**.
3. **Legitimacy** = population-weighted happiness. It is read by the AI valve and the UI only.
4. **Dignity** satisfaction = **1 − r × (1 − m × P)**, with m = `needs.json unrest.taxBurdenOffsetMax` = 0.5.
   P = the D-035-B CES aggregate of the class's **other bound needs**:
   - **Sustenance** (food basket: grain, livestock, fish);
   - **Shelter** (housing stock);
   - **Comfort** (pottery, cloth).
   
   The unbound needs (Safety, Health, Belonging/Faith, Prospects) are not in P
   (`NeedsGrievanceSystem.cs:296-313`, `needs.json`).
5. **Grievance** stock (dt-integrated, generational decay). The **tax-attributed** grievance is the stock × the
   share of the aggregate shortfall that lifting the levy would close (`Unrest.TaxGrievance`).
6. **Protest** p = clamp((G_tax − 15) / (50 − 15), 0, 1) (`needs.json unrest`). Protest has these effects:
   - output × (1 − 0.5 · p · r);
   - extra grievance decay of 0.005 · p per year;
   - at p = 1, an **uprising**: revolt.
7. **Revolt** if happiness = 0 (deprivation; or **r = 1, i.e. a 100 % levy at full reach**) or an uprising,
   except a ruler's final settlement.

### 5.3 What feeds the offset: exact wiring (IMPLEMENTED unless stated)

| intended offset | wired? |
|---|---|
| Food availability | **YES**, via Sustenance in P (the Dignity offset only). Happiness's food factor is separate and does not offset the tax multiplier. |
| Amenities | **YES (partially)**, via Comfort (pottery, cloth) in P. Pottery firing needs `pottery_open_fired`. Comfort is **not** in happiness. |
| Housing | YES, via Shelter in P (the offset); also a happiness factor. |
| Public services | **NO.** No public-service need or good exists. |
| Development (roads, structures, universities) | **NO** direct offset. Roads *raise* reach, and therefore raise the effective burden away from the capital. |
| Institutional / state capacity | **NO** as an offset. Reach (capacity) scales the *rate* collected: lower reach means less burden and less extraction. No institution reads into the tax equations. `inst.census` (planned as a "collection-efficiency term") is a DEFERRED entity. |

### 5.4 Measured behaviour (CARRIED — `docs/r4a-m5-closure-record.md` §3; founded seat rigs, single polity)

At a 99 % levy:

| arm | R2b (no offset) | R4a shipped (m = 0.5), within 80 turns (300-turn peaks the same) |
|---|---|---|
| well-provided seat | revolt at turn 6 | **no revolt**; peak tax grievance 19.1; protest 0.118 |
| natural seat | revolt at turn 24 | **no revolt**; 19.9 / 0.139 |
| poorly-off seat | no revolt | **no revolt**; grievance 0 / protest 0 |

At 40 % and 70 % no arm protests under R4a. Pinned by `UnrestTests.ExtremeTax_OnAWellProvidedSeat_IsBorneInProtest_NotADeterministicRevolt`.

### 5.5 Discrepancies between intended and implemented

| # | discrepancy | label |
|---|---|---|
| G1 | **100 % at full reach is a deterministic revolt** of that settlement (the capital, reach 1.0) the next turn, if the ruler holds another place. `GovernanceTests.AFullLevyAtFullReachIsTotalExtraction_TheSecondRevoltCorner`. The player can enter 100 (`TaxOrderFactory.MaxPercent`); the AI cannot exceed 40. | IMPLEMENTED; conflicts with the intended "no deterministic tax revolt" at exactly 100 %; OPEN |
| G2 | Happiness and legitimacy take the full (1 − r) with **no offset**. At 99 % happiness reads ~1/100 of its untaxed value. Only the grievance/protest path is offset. | IMPLEMENTED; OPEN |
| G3 | Public services, development and institutions do not offset (§5.3). | KNOWN LIMITATION; OPEN |
| G4 | **A poorly-off settlement never protests the levy.** The attribution share gives a homeless seat's grievance to its other shortfalls (R4a §3 "OPEN for the Director"). | KNOWN BUG in attribution (open design defect); OPEN |
| G5 | The attribution is re-computed each turn from current shortfalls (INFERRED simplification, R2b). | INFERRED; OPEN |
| G6 | D-021 valves 2 (fatigue) and 3 (unrest-driven exit), and the ignite-and-burn-out battery entry, are not shipped. | DEFERRED |
| G7 | `taxBurdenOffsetMax` 0.5 is CHOSEN (the smallest measured value at which a provided seat carries less tax grievance than a natural one). It is not derived. | IMPLEMENTED (chosen value) |

---

## 6. The turn-1 food issue: complete history

### 6.1 Origin

ADR-033 D1 ESCALATED (with CR-006) that the turn-1 "Gathering" output was the CR-003 cultivated cereal yield.
The integration pass implemented option (a), "keep", in its first form.

### 6.2 R2a (2026-10-03): forager layer built, switch committed OFF

- Content block `farming.preCultivation`. The values were fixed before measurement at 1.0 per km² and 2.0 per
  gatherer (`sim.json` _doc).
- With the switch ON the world starved. It was committed OFF.

### 6.3 R3 (2026-10-03): founding-population sweep FAILED (`docs/r3-final-reconcile-record.md` §1; MEASURED there)

- Ruling (RATIFIED, R2-final item 7): use a smaller founding population rather than hidden farming.
- Swept N per settlement over 400 / 200 / 100 / 50 at the R2a rates 1.0 / 2.0. **No N passed the ratified
  bands.** Fed growth was negative on both seeds at every N, and the dev Malthus worlds starved or went extinct.
- **Land was not the limit.** About 184,000 fertility-weighted km² of arable area supports about 184,000
  person-years per year, against about 1,400 people at N = 100.
- **The labour side binds.** 2.0 per gatherer × the 0.55 never-ordered food share × the adult share is a
  per-capita ceiling below consumption at any N.
- **Nobody researched Agriculture.** The battery runs have no player orders and `aiEmpires` = 0, and zero nodes
  were completed in 60 turns. Research could not rescue the population.
- **Negative-ledger crash** at N = 100, dev seed 42 (§7).
- Outcome: switch stays OFF. OPEN for the Director.

### 6.4 R4a (2026-10-04): yield sweep, switch ON (`docs/r4a-m5-closure-record.md` §1; MEASURED there)

- Director ruling (RATIFIED 2026-10-04): raising primitive gathering yield is the preferred lever. Agriculture
  stays researched. Use the smallest defensible value, not a maximum.
- The sweep held founding population at 400 and varied per-gatherer output over 2.0–4.5 and per-km² yield over
  1.0–4.0.
- **Chosen: 4.3 per gatherer, 2.0 per km²** (`07832a4`). This is the smallest grid point with zero starvation
  and no crash on both dev seeds, and every corridor band green.
  - 4.25 / 2.0 starves 5 people on dev seed 7.
  - 4.3 / 1.5 starves 418.
- 100 founders at 4.3 / 2.0 was measured worse: fed density on seed 1 falls below the 0.15 floor. So **N stays
  400**.
- Per-adult reading: 0.55 × 4.3 = 2.37 person-years per adult. Kelly 2013 and Kaplan et al. 2000 report a
  prime-age forager producing 1.7–3× own consumption.
- Re-pinned: three goldens, the dev migration quarantine envelope, the dev final-population envelope
  [71k, 119k], and the AI-world pins (R4a §7–8).

### 6.5 Current decision and status

| item | status |
|---|---|
| Raising hunting/gathering yield is a legitimate calibration lever, not architecturally forbidden, provided primitive gathering stays baseline and grants no researched Agriculture | **RATIFIED** (Director 2026-10-04, R4 and this instruction) |
| Shipped: forager ON, 4.3 / 2.0, founding population 400 | **IMPLEMENTED** (`07832a4`) |
| Ratified bands (calibration battery, CI calibration job) | **PASS** at `bc87ef8` (CI 37203237727 calibration success; local suite §1.6) |
| 20-seed nightly corridor sweep on this tree | **NOT RUN** (OPEN) |
| Quarantined corridors (`canonical.densityPerArableKm2`, `canonical.migrationGrossPerDecade`, dev migration envelope) | quarantine active, not retuned (OPEN, CR-003 lineage) |
| All-food 900-turn rig starves at the forager land ceiling | KNOWN LIMITATION (R4a §1) |
| Final calibration | **PROVISIONALLY VALIDATED on the ratified bands. Not closed until the nightly sweep and the quarantines are resolved.** |

---

## 7. The ledger crash

| field | value |
|---|---|
| First seen | R3 founding-population sweep (`docs/r3-final-reconcile-record.md` §1, "Defect found (OPEN)") |
| Configuration | `farming.preCultivation` **ON** at the R2a rates **1.0 / 2.0**; founding cohorts re-apportioned to **100 per settlement** `[14,14,13,11,10,9,7,6,5,4,3,2,1,1,0,0]`; `foodStore` 1,500 (15 per founder) |
| World / seed | **dev world** (`TestConfigs.DevWorldgen`: 256 px, 4 settlements), **seed 42**, no orders |
| Turn | **853** |
| Exception | `ArgumentOutOfRangeException: Ledger amounts are never negative (−1)`, thrown from `ConsumptionSystem.Consume` (per the R3 and R4a records) |
| Stack trace | **Not retained.** Neither record nor the test stores more than the exception type, message and throwing method. |
| Violated invariant | **A consumption request is never negative.** The staple (grain) is asked for its own demand plus each non-staple food's *exact*, *signed* shortfall. A sub-unit fraction lent to the staple while banked in the non-staple's remainder is repaid (negative) when that remainder pays out a whole unit. In a settlement whose demand had collapsed, the repayment exceeded the staple's whole request, and flooring gave −1. |
| Fix | **`e6b7e42`** "fix(consumption): carry a staple substitution credit instead of a negative ledger amount" (2026-10-04, before the forager layer shipped in `07832a4`). A negative request asks nothing of the store. The credit is carried exactly in the staple's `ConsumeRemainder` and settled against its next request. No clamp, nothing dropped. Non-negative requests are computed as before, so no shipped world moved. |
| Regression test | `Sim.Tests/Systems/SubstitutionCreditRegressionTests.cs`: the exact crash configuration for 900 turns. It asserts no negative stock, that only the staple carries a credit and the credit stays > −3, that a credit was carried (non-vacuous), and that the conservation audit is exact. The R4a record reports that it fails with the crash when the fix is reverted (CARRIED). |
| Status | **FIXED.** The historical failure is preserved above and in the R3 record. The shipped configuration (4.3 / 2.0, N = 400) differs from the crash configuration. |

---

## 8. Knowledge-persistence rulings

| # | ruling | status | implementation / test |
|---|---|---|---|
| 1 | Revolt copies completed research only | RATIFIED (D-048, 2026-10-04); IMPLEMENTED | `RevoltSystem` → `KnowledgeTransfer.MergeInto`; `KnowledgeMonotonicTests`, `UnrestTests.ARevoltedSeat_BecomesANewAiPolity_…` |
| 2 | Research progress remains with the parent | RATIFIED (D-048); IMPLEMENTED | only `ResearchCompleted` rows are copied |
| 3 | Eureka credit remains with the parent | RATIFIED (D-048); IMPLEMENTED | as above |
| 4 | The new civilization starts without a capital | RATIFIED (D-048); IMPLEMENTED | `RevoltSystem` adds no capital row |
| 5 | A civilization's final settlement cannot revolt away | RATIFIED (D-048); IMPLEMENTED, including simultaneous revolts (capital kept, else first in table order) | `RevoltSystem` per-place guard + `LosesEveryPlace`; `KnowledgeMonotonicTests.*_D048` |
| 6 | The new civilization researches at the normal rate | RATIFIED (D-048); IMPLEMENTED | an ordinary `CommandSource.Ai` polity on its own RP curve; 0.25 applies only to uncontrolled places |
| 7 | Completed knowledge never decays | RATIFIED (D-048 ruling 7; R3 item 1); IMPLEMENTED | `UnrestTests.CapitalLoss_ErasesNoKnowledge_…` |
| 8 | Annexation, when implemented, merges knowledge by union and never removes completed knowledge | **RATIFIED** (Director 2026-10-04, this instruction). Recorded as an append-only note in `docs/d048-knowledge-persistence-rulings.md`. The domain operation is **IMPLEMENTED** (`KnowledgeTransfer.MergeInto`: union, idempotent, never deletes; tested in `KnowledgeMonotonicTests.Annexation_…`) and has **no caller**. **Annexation gameplay is DEFERRED.** | — |

Implemented revolt behaviour is live in play. Annexation and conquest behaviour does not exist in play.

---

## 9. Known open / deferred issues (baseline list)

| issue | status | milestone | reference |
|---|---|---|---|
| Turn-1 food calibration | **ACCEPTED** (ratified bands pass) with **OPEN** residue: nightly sweep not run; quarantines | M5 (residue: calibration, any milestone) | §6 |
| Negative-ledger crash | **FIXED** (`e6b7e42`) | M5 | §7 |
| CR-019 raider / revolt-polity conflict (T4.5 raider gate has no producer in founded worlds) | **DEFERRED** (RATIFIED 2026-10-04) | M7 / M8 | `docs/adr/cr-019-…` §6 |
| Conquest | **DEFERRED** | M7 (Battle Layer), M8 | ADR-033 R3; D-048 §3 |
| Annexation gameplay | **DEFERRED** (operation implemented, no caller) | M8 (politics) / M7 | §8 |
| Capital succession / relocation | **DEFERRED** | M8 (INFERRED placement; no ruling names the milestone) | R3 §3; matrix §6.3 |
| Recovery of revolted settlements | **DEFERRED** | M7 / M8 | matrix §6.3 |
| 115 deferred research entities | **DEFERRED** (0 realized) | M6 / M7 / M11+ per tier | `docs/deferred-entity-realization-plan.md`; audit `--check` |
| Quarantined calibration ranges (`densityPerArableKm2`, `migrationGrossPerDecade`, dev migration envelope) | **OPEN** (quarantine active) | CR-003 lineage; no milestone ruled | `corridors.json` |
| Later-Age military milestones (pending recruitment) | **DEFERRED** (PENDING in content) | M7 | `ages.json` `pending` |
| Trade | node 426 **IMPLEMENTED**; full Trade content **DEFERRED** | M6 / M11+ | R2a record; R4a §4–6 |
| M6 research work (engine review, unlock pipeline, universities completion) | **DEFERRED** (NEXT milestone) | M6 | `docs/milestones.md` |
| M7 Battle Layer (recruitment, movement, war) | **DEFERRED** | M7 | ADR-033 D7 |
| Tax/unrest attribution: poorly-off settlements never protest | **OPEN** | M5 (Director ruling needed) | §5.5 G4 |
| 100 % levy at full reach is a deterministic revolt | **OPEN** | M5 (ruling) | §5.5 G1 |
| AI issues no labour orders | **OPEN** (E3) | AI work, unassigned | §3.2 A6 |
| Revolt-founded polity starts at Age A1 | **OPEN** (no ruling) | M6 (Ages) / M8 | §2.8 |
| Research is not Age-gated (a polity in A1/A2 can hold Taxation; the AI levies in A2 from turn 369, MEASURED) | **IMPLEMENTED** by design (law 4: no calendar gates). Its interaction with "no tax in the Stone Age" and with Taxation's A3 tag is OPEN for a ruling. | M6 | §0 D8, §4.3 |
| UI preview rigs crash (`--action-preview`, `--player-views-preview`) | **KNOWN BUG** (dev tooling) | M5 cleanup | §2.12 |
| Benchmark not measured on `bc87ef8` | **OPEN** | M5 | §1.5 |
| CR-016 (famine-class disaster hazard 0.0, rate unruled) | **OPEN** | — | `docs/adr/cr-016-…` |
| CR-005 placement, CR-008 money / no treasury | **OPEN** | — | ADR-033 D4 |
| Colonies not re-measured since the forager layer | **OPEN** | M5 | §2.7 |

---

## 10. Roadmap (RATIFIED — Director rebase 2026-10-03, `docs/milestones.md` §"Roadmap rebase 2026-10-03")

| M | Milestone | status |
|---|---|---|
| M0 | Kernel | COMPLETE |
| M1 | Walking Skeleton | COMPLETE |
| M2 | Demography / Food | COMPLETE |
| M3 | Production / Markets | COMPLETE |
| M4 | Empire / Strategic Foundation | COMPLETE |
| **M5** | **Governing Gameplay** | **CURRENT, being finished, NOT complete, integration unmerged** |
| M6 | Knowledge / Research / Technology | NEXT |
| M7 | Battle Layer | future |
| M8 | Politics / Diplomacy | future |
| M9 | Society | future |
| M10 | Integrated Civilization Simulation | future |
| M11+ | Depth & Content Expansion | future |

- **Ages A1–A9 are a simulation dimension, not development milestones.**
- **M6 is Knowledge / Research / Technology, not Battle.** The research engine, Ages, unlock pipeline and
  universities built early on the integration branches belong to M6, which will review and complete them. That
  work does not make M6 done.
- **M7 is the Battle Layer and consumes M6's technology/capability state.** It has no parallel tech vocabulary.
- **M10** integrates and validates the whole civilization model across all Ages A1–A9.
- **M11+** adds depth and content. It does **not** implement later Ages sequentially, because all nine Ages
  already belong to the model.
- The earlier orderings are superseded and kept as history with pointer notes (§11):
  - Spine v3: M6 knowledge, M7 politics, M9 Ancient Vertical Slice, M10+ era expansions;
  - D-011 §6: M6 battle, M7 knowledge, M10 Ancient Vertical Slice.

---

## 11. Documentation status

Classification legend:

- **corrected**: fixed in place earlier, with a dated note;
- **historical**: intentionally preserved text;
- **superseded**: preserved, with a pointer to the authority;
- **needs cleanup**: still wrong and not yet fixed.

"Pointer added here" means this record appended a dated note (2026-10-04).

| reference | location | classification |
|---|---|---|
| Old four-node tax gate (D4 decision text) | `docs/adr/adr-033-m5-integration-pass.md` D4 | **superseded**: text kept; R4a/R5 pointer section **appended here** |
| Old four-node tax gate | `docs/m5-governing-loop-port.md` §2 item 2 | **superseded**: pointer **appended here** |
| Old four-node tax gate (doc comment) | `Sim.Core/State/Governance.cs:213-215` (`CanLevyTax` comment: "today 'arithmetic_babylonian OR surveying OR standard_weights OR coinage_electrum'") | **needs cleanup** (code comment; not changed here: no code changes) |
| Tax gate in previews | `Sim.Ui/Actions/ActionSurfacePreview.cs:55, 76-85` and `Sim.Ui/Actions/R2aPreview.cs:49` grant `arithmetic_babylonian` as "taxation" | **needs cleanup**; KNOWN BUG for `LaterRig` (§2.12) |
| `arithmetic_babylonian` granted for the levy | `docs/m5-integration-coherence-matrix.md` §6.1 measurement table; `Sim.Tests/Systems/GovernancePushbackMeasurement.cs` rig (skipped) | **historical** (R2b measurement, correct for its tree). Matrix pointer **appended here**. |
| Tax gate in `docs/institutions-universities.md` | only `governance.taxationRequires` by key (no node list) | **current** (no stale node list found) |
| Tax gate in `docs/current-state.md` | no node list; but its top entries stop at R3 and still read "forager food STOPPED … switch stays OFF" (line 13) | **superseded**: newer dated entry **added here** at the top |
| Coherence matrix rows for the four-node gate | none found | — |
| Old M6/M7 ordering | Spine v3 outline, D-011, `milestone-architecture-governance.md`, `m4-pre-spec-dependencies.md`, `m5-roadmap-dependency-audit.md`, `capability-architecture-decision.md`, `spine-s8-governance-freeze.md`, `queue.md`, ADR-033 D7, `current-state.md`, `d037`, and `docs/design/*` (arch-C/D/E/FGH/M/N/O/PQ, director ledger, recovered-decisions ×3, research-capability-ownership) | **superseded**: each carries an R4b rebase pointer note (`3570a24`) |
| "Ancient Vertical Slice", "era expansions", "slice gate" | same family of files (`grep` 2026-10-04: Spine outline, D-011, `spine-s8-governance-freeze.md`, `queue.md`, `milestones.md`, `m4-pre-spec-dependencies.md`, `m5-roadmap-dependency-audit.md`, `capability-architecture-decision.md`, `milestone-architecture-governance.md`, `docs/design/*`) | **historical / superseded**: obsolete as milestone names (`milestones.md` rebase header), each file pointered by R4b |
| Stale milestone comments in code | grep of `Sim.Core`, `Sim.Ui`, `Sim.Data`, `Sim.Cli`, `scripts` for M6 battle / M7 knowledge: **none** (retargeted to M7 by R4b) | **corrected** |
| Build label `civ-sim M4` | `Sim.Ui/BuildInfo.cs:18` (window title of the played build) | **needs cleanup** |
| Generated audit references | `docs/research-gameplay-unlock-audit.md`, `research-corpus-audit.md`, `research-calibration-report.md`: all `--check` current at `bc87ef8`; recruitment reads "M7 Battle Layer"; the `taxation` row is present | **corrected** (regenerated by R4b and R5) |
| `ages.json` milestone text | every military milestone's `pending` reads "Battle Layer (M7) recruitment"; no M6 battle text | **corrected** (R4b) |
| README | title "civ-sim (M4)"; "M3 — The economy arrives" section; "current milestone spec" pointer to `m4-spec.md` (self-labelled as predating M5); "window title … `civ-sim M3`" (the build shows M4); the Download & Play build-3 paragraph and the roadmap line are current | **needs cleanup** (headline, M3 section, window-title sentence); roadmap line **corrected** |
| `CLAUDE.md` "Current milestone: M4" | `CLAUDE.md` line 10 | **needs cleanup by the Director only** (agents may not edit `CLAUDE.md`). R4b touched it for the roadmap; the milestone line still reads M4 / `m4-spec.md`. |
| D-048 ruling 8 | `docs/d048-knowledge-persistence-rulings.md` | **appended here** |
| ADR-033 R4a / R5 sections | absent before this record | **appended here** (pointer) |

---

## 12. Playtest forensic checklist

General capture for every item:

- the session's `runs/` folder (orders `.bin`, chronicle `.txt`, manifest, trace);
- the seed and flags (`--ai-empires N`, `--settlements`, `--size`);
- the turn number;
- the build label from the window title;
- a screenshot of the panel concerned.

Reproduce with `sim replay --founded --seed S [--ai-empires N] --orders <bin> --turns T`. Inspect with
`sim inspect --manifest runs/session-*.json --turn T [--answer TOPIC] [--explain happiness|needs|migration|chain --settlement ID --turn T]`.

| # | TEST | EXPECTED BASELINE | REGRESSION IF | CAPTURE |
|---|---|---|---|---|
| 1 | Turn-1 economy | 12 settlements (6 player / 6 AI with `--ai-empires 1`; 12 player without), each ~400 people (jitter ±69 % amplitude) housed, ~6,000 grain (jittered). Five sectors labelled Gathering / Hunting & fishing / Gathering wood & stone / Crafts & toolmaking / Building. | Food sector reads "Farming" at turn 1; any settlement unhoused at turn 1; population far from ~4,800 total | Settlement view, Policy panel, `--answer world`, `--answer settlements` at turn 1 |
| 2 | Food | No starvation on the default mix (the dev seeds starve 0 at 4.3 / 2.0). Turn-1 food is wild food. | Starvation deaths on the default mix in the first ~50 turns; deficit on several settlements | `--answer resources`, `--answer happiness`, chronicle famine lines |
| 3 | Gathering | Label "Gathering — makes wild food (grain)". Output at the forager rates (4.3 per food-sector worker, land ceiling 2.0 per fertility-weighted km²). | Cultivated-rate output before farming is known; label wrong | Policy panel text; production numbers from `--answer resources` |
| 4 | Farming / research gate | Farming appears only once `root_crop`, `cereal_cultivation`, `rice_wet`, `millet`, `maize` or `sorghum_pearl_millet` is complete. `root_crop` (440 RP) is the cheapest. Afterwards the food sector reads Farming and the harvest rises (26.0 / 5.0). | Farming before research; no yield change after | Research tree; turn of completion; harvest before and after |
| 5 | Research | One target at a time, RP ≈ 0.08 × pop^0.699 per turn. Completion announced; progress kept when switching. | RP lost; node available without prerequisites; a completed node disappears | Research panel; `sim research` report |
| 6 | Taxation gate | No tax control until **Taxation (Civics, A3, 1,050 RP)** is complete. Prerequisites: clay tokens + stamp seal + one script. Babylonian arithmetic, surveying, weights or coinage do **not** open it. | Tax control visible earlier, or opened by a refinement node; a levy recorded before Taxation | Civics tree; Policy panel; turn Taxation completed; order log |
| 7 | Taxation behaviour | Order on turn t, in force at t+1, effects from t+1 → t+2. Effective rate = declared × reach (capital 100 %, falling with travel cost). Output gains up to +30 % × rate. Burden lines show reach and the collected %. | Rate applies the same turn; a non-capital collects at full rate without reach 1.0; output unchanged | Tax panel burden lines; `--explain chain` |
| 8 | Happiness | 0–100. Food and housing only, × (1 − effective rate). Untaxed, fed and housed reads ~100. | Happiness rises from a non-provision source; comfort shifts happiness (it should not) | `--explain happiness --settlement ID --turn T` |
| 9 | Amenities | Comfort (pottery, cloth) affects only grievance and the Dignity offset, **not** happiness. Pottery needs `pottery_open_fired`. | Pottery before research; happiness reacting to comfort | `--explain needs`; stocks |
| 10 | Legitimacy | Population-weighted happiness of held settlements, shown in the tax panel. It drives only the AI valve. | Legitimacy changing with nothing in happiness changing | Tax panel legitimacy line |
| 11 | Unrest | At ≤ 70 % no measured protest. At 99 %, protest (output drag), with **no revolt from tax alone** within 300 turns on measured seats. A **poorly-off settlement never protests the tax** (KNOWN). **100 % on the capital revolts it** (KNOWN corner). | Revolt at < 100 % from tax alone on a provided seat; protest with no levy | Turn of each protest or revolt; `--explain needs`; grievance figures |
| 12 | AI rival founding | `--ai-empires 1`: exactly 6/6 interleaved; AI capital = siting row 1; one warband each; no AI without the flag. | Uneven split; AI capital missing; a rival appearing later (other than a revolt polity) | `--answer polities` at turn 1; map |
| 13 | AI research | First targets `knapping_oldowan` (turn 0), `ground_stone_early` (turn 26); A2 decided at 238 (canonical, no player orders). With player orders the timings differ. | AI idle with no target; AI researching unavailable nodes | `--answer polities`; order log kinds 6 and 7 |
| 14 | AI taxation | First AI levy about turn 367/368 (canonical, no player orders), ≤ 40 %, stepping by 5. OBSERVATION, not a target. | AI levy before it knows Taxation; > 40 %; a levy on the player | Order log kind 5 with actor 2; tax policy history |
| 15 | Settlements | Control by `Controls` rows; capital is a designation. Revolted places refuse your orders. | Orders on a revolted place accepted; a settlement with two controllers | Settlement view controller; refusal messages |
| 16 | Colonies | Founded only from people with no viable destination (rare; 0 in measured canonical runs, pre-forager). They inherit the parent's controller. | A colony of player stock owned by another polity; colonies with no unplaced demand | Annals colony lines; `--answer movements` |
| 17 | Roads | Road classes appear when researched (track road ← `track_road`), cost materials per km, and shorten travel cost, which raises reach (and tax collected) a few turns later. | Road without research; reach unchanged after the distance falls | Road panel; burden lines before and after |
| 18 | Institutions | Only universities exist, gated by `medicine_hippocratic AND (geometry_axiomatic OR algebra)` and `library AND legal_code_roman`. They need ≥ 2,000 adults per instance, mature over decades, and reduce branch costs (≤ 1/3) or mortality (≤ 10 %). | University with fewer adults or without research; instant maturity | Institutions view; research cost modifiers |
| 19 | Universities | As above. Staff drawn from labour (5 % of 2,000 × maturity). | Staff not removed from the labour pool | Institutions view; labour figures |
| 20 | Age progression | Advance only by your order when eligible (core + supporting milestones). Military milestones show "pending (M7)". The theme changes with the Age. | Auto-advance of the player; a military milestone met; the theme changing without an Age change | Age panel; turn of advance |
| 21 | Units | One warband per empire at its capital; it converts free at each Age entry; no recruitment or movement. | New formations; units consuming people or goods | Military view at each Age |
| 22 | Revolt | Paths: total deprivation, 100 % levy at full reach, or uprising (grievance ≥ 50). The last settlement never revolts. The revolted place becomes a new AI polity (no capital, Age A1, your knowledge copied). | Your last settlement revolting; the revolted polity lacking your completed knowledge; it receiving a capital | Annals; `--answer polities`; research of the new polity |
| 23 | Knowledge persistence | You never lose completed nodes or progress through a revolt or capital loss. | A node lost; progress reset | Research tree before and after |
| 24 | Capital loss | If the capital revolts, you keep the capital row; reach = 0 everywhere, so no tax is collected anywhere. No successor capital. | A successor capital appears; tax still collected | Tax burden lines after the loss |
| 25 | Save / load | Quit, then `--resume <session-dir>`: the replay reproduces each turn hash (throws on divergence). | Resume divergence error; any visible difference after resume | The divergence message; the manifest and trace files |
| 26 | Replay | `sim replay` of your log matches the trace hash every turn. | Any hash mismatch (a determinism finding on linux-x64; cross-platform see CR-013) | Hash logs; `sim inspect` verdict |
| 27 | Visual / map | Era theme from your Age; roads drawn by class; straight road geometry (KNOWN); debug behind F12. Previews `--action-preview` / `--player-views-preview` crash (KNOWN BUG, dev tooling). | Map showing a state that is not in the simulation; theme ≠ Age | Screenshots with the turn and Age |

---

## 13. Final baseline table

| System | Expected baseline | Current implementation | Known issue? | Milestone | Test status |
|---|---|---|---|---|---|
| Founding | 12 settlements × ~400, housed, 6,000 grain, A1 | `WorldFounding` + `sim.json founding` | Player gets the best-ranked sites (INFERRED) | M4 | goldens; `FoundingDemographicsTests` green |
| Turn-1 food | Wild food 4.3 / 2.0; no starvation on the default mix | `farming.preCultivation` ON (`07832a4`) | Nightly not run; quarantines; all-food rig starves | M5 | battery green (CI); R4a sweep |
| Farming | Cultivated 26.0 / 5.0 after a crop node | `ProductionSystem.Farm`, `LabourActivities` | — | M5 / M6 | `TurnOneActionSurfaceTests`, `FoodSectorLabelTests` |
| Research | Prerequisite-gated; RP = 0.08 p^0.699 | ADR-029/030 | Not Age-gated; 115 deferred entities | M6 (owner) | `ResearchUnlockPipelineTests`, audits current |
| Tax gate | Taxation civic 1007 only | `governance.taxationRequires = "taxation"` | Preview rigs crash; stale code comment | M5 | `T08b`, `T17`, `T18`, `GovernanceTests`, UI tax tests |
| Tax effect | Extraction +0.3 r; happiness × (1 − r) | `Governance`, `SettlementHappiness` | 100 % = revolt corner; happiness not offset | M5 | `GovernanceTests` |
| Unrest | Offset Dignity; protest 15→50; uprising at p = 1 | R2b + R4a (`95982bd`) | Poorly-off never protest; services, development and institutions do not offset | M5 | `UnrestTests` |
| Legitimacy | Population-weighted happiness | `Governance.Legitimacy` | Only the AI valve consumes it | M5 | `GovernanceTests` |
| Revolt | Deprivation / 100 % levy / uprising; last place kept | `RevoltSystem` | New polity at Age A1 (OPEN) | M5 | `RevoltTests`, `KnowledgeMonotonicTests` |
| Knowledge persistence | D-048 rulings 1–8 | `KnowledgeTransfer` | Annexation has no caller | M5 / M8 | `KnowledgeMonotonicTests` |
| Capital loss | Reach 0 everywhere; no succession | `Governance.AdministrativeReach` | Succession DEFERRED | M8 | `UnrestTests.CapitalLoss_*` |
| AI rivals | `--ai-empires N`, default 0, 6/6 split, capital + warband | `FoundInitialEmpire`, `AiOrders` | AI no labour orders; revolts add AI polities | M5 | `AiEmpireIntegrationTests` (367/368), xproc AI leg MEASURED |
| AI tax | Valve ≤ 40 %, step 5, first levy ~367 | `AiGovernance` | OBSERVATION only | M5 | `AiEmpireIntegrationTests` |
| Ages | Order + eligibility; military pending | ADR-031 | Provisional thresholds; surges inert | M6 / M7 | `AgeProgressionTests`, `AiAgeAdvancementIntegrationTests` |
| Units | One warband, free conversion | `AgeTransitionSystem` | No recruitment | M7 | `AgeProgressionTests` |
| Roads | Researched classes; reach coupling | ADR-032 (frozen `997824b`) | Straight geometry | M5 / M6 | road tests; xproc kind 8 |
| Colonies | From unplaced demand; inherit control | `ColonizationSystem` | Not re-measured post-forager | M4 | `ColonyOrderValidationTests`, colonization tests |
| Institutions | Five university types only | v31 Institutions | No other institution realized | M5 / M6 | university / institution tests; save/load battery |
| Trade | Node 426 gates trade | `TradeQuery.CanTrade` | Full trade content DEFERRED | M5 / M6 | `TradeResearchUnlockTests` |
| Save / load | Order log + replay resume; CLI snapshots | `UiSession.Resume`, `Snapshot` | UI writes no snapshot | M0 / M5 | `IntegratedSaveLoadBatteryTests` |
| Replay / determinism | Byte-identical cross-process | kernel | — | M0 | xproc legs MEASURED 2026-10-04 |
| Goldens | founded `02c7f9eb…`, first reign `74a97abc…`, driven `3a9f007a…` | pins + `ci.yml` | — | — | founded MEASURED via CLI; all in suite |
| Benchmark | ≈ R4a 29.9 s founded 300 turns | — | Not measured on `bc87ef8` | — | not run |
| UI / map | Era from Age; roads by class | Sim.Ui | Preview rigs crash; "M4" build label | M5 | Sim.Ui.Tests |
| Documentation | Roadmap rebase pointers everywhere | — | README / BuildInfo / `Governance.cs` comment / `CLAUDE.md` need cleanup | — | audits `--check` green |

---

## §1.6 results

All of the following were MEASURED on `bc87ef8`, Release build, 2026-10-04, on a machine shared with other agents.

**Test suites**

- `dotnet test Sim.Tests/Sim.Tests.csproj -c Release --no-build`: `Passed! - Failed: 0, Passed: 1460, Skipped: 6,
  Total: 1466, Duration: 23 m 17 s`. This equals the R5 count the orchestrator carried.
- `dotnet test Sim.Ui.Tests/Sim.Ui.Tests.csproj -c Release --no-build`: `Passed! - Failed: 0, Passed: 473,
  Skipped: 0, Total: 473`.

**`ci.yml` determinism-xproc step, run locally and verbatim** (exit 0; final lines in §1.5)

- The AI leg's `sim inspect --turn 600` reported `reproduction VERIFIED: 601 turns, hash-for-hash.`
- `sim inspect --answer polities` reported 0 control-row losses over 600 turns. At turn 600 the player held the
  even siting rows and the AI the odd rows (6/6).

**AI orders in the AI leg's run log** (194 orders in total, kinds 3–8). These are the AI's own orders, actor 2:

| turn | order |
|---|---|
| 239 | AdvanceAge → A2 |
| 369–376 | SetTaxRate 5 → 40 % |
| 465 | AdvanceAge → A3 |

**Gates**

- All gate scripts and the three audit `--check`s exited 0 (§1.4).

**Preview tools**

- `Sim.Ui --action-preview` and `--player-views-preview` exit 134 with the levy exception (§2.12).
- `--r2a-preview` exits 0.

**Not run**

- `sim bench`.
- The 20-seed nightly corridor sweep (`ci.yml` calibration-nightly).

---

## 14. Taxation and revolt decision record — H2 (appended 2026-10-05, M5 hardening §18)

**RATIFIED (Director, 2026-10-05, M5 hardening instruction §4–§9, §18).** Full record, model, measurements and
supersession: `docs/d049-taxation-and-revolt-model.md`.

- **Taxation.** Taxation is a continuous welfare/governance pressure, not an instant-revolt switch.
- **Extreme taxation.** 100 % tax is permitted as an extreme player action but does not directly or instantly
  revolt the population.
- **Revolt.** Revolt emerges after accumulated grievance/unrest/happiness deterioration reaches local tipping
  conditions.
- **Population impact.** A revolt affects a population segment, not automatically the entire settlement.
- **Local conditions.** Food, amenities, housing, comfort goods, services, development and institutions/state
  capacity can mitigate tax pressure.
- **Taxation Age.** Taxation is A3/Bronze Age and cannot be operational before A3.
- **Revolt Age.** A revolted civilization inherits the parent's current Age.

**Implemented (H2, `m5h-h2-tax-revolt`):** a levy-grievance stock per population segment (class × settlement,
schema v32) accrues the felt burden `r × (1 − 0.5 P) × (1 − 0.25 V)` over years; protest from 12, a segment's
tipping point at 20, a growing portion of it in revolt past it; the settlement changes hands only when its rebels
are a majority; happiness and legitimacy fall with the accumulated pressure and recover after a cut;
`sim.json governance.taxationMinAge = 3` inside `Governance.CanLevyTax`; `RevoltSystem` gives the revolt-born
polity its parent's Age.

**Superseded in this record (text above kept as history):** §5.2 items 2 and 5–7; §5.3 rows "Public services",
"Development", "Institutional / state capacity" (now wired: public works, universities and reach, d049 §9); §5.4
(R4a measurements, historical); §5.5 G1 (the 100 % corner — removed), G2 (happiness now offset through the
pressure), G3 (partly closed, d049 §9), G4 (closed — a poor place accumulates more), G5 (closed — a stock, not a
per-turn share); §3.1 A2's "100 % revolt corner"; §4.3 (AI first tax: now 466 in the CI leg, 463/464 in the suite,
on its first A3 turn); §9's two OPEN tax items and "new polity at Age A1"; §12 rows 11 and 22; §13 rows "Tax
effect", "Unrest", "Revolt". The D-021 valves 2/3 (G6) stay DEFERRED; `taxBurdenOffsetMax` 0.5 (G7) is kept.

---

## 15. M5 HARDENING PASS — final integration (appended 2026-10-05; nothing above rewritten)

**Scope.** The Director's M5 hardening instruction of 2026-10-05 (§1–§19). Branch `m5-hardening` (not merged, not
tagged; M6 not started). Labels as in the header; **MEASURED** here means measured on this branch's final tree
(`6ef1596` code) on 2026-10-05 by the integrating agent unless a stream is named.

### 15.1 Identity

| item | value | label |
|---|---|---|
| Branch | `m5-hardening` (`origin/m5-hardening`), cut from the baseline `9bb7423` (`m5i-playtest-baseline`) | MEASURED (git) |
| Hardening merges | H1 `e2f9661` (UI playability), H2 `cd6dbd3` (tax/revolt), H3 `de4eeb3` (previews/docs), H4 `b2dd24f` (measurements); F1 `1e74c8f` (tax fixes), F2 `42c5af4` (forager, docs only), F3 `473fd6a` (UI/docs fixes, ADR-034) | MEASURED (git) |
| Integration commits | `f5d7040` (ADR-034 call-site pins), `803bc81` (preview logs use the player turn), `2b049f4` (preview sets regenerated), `6ef1596` (forager decision recorded) | MEASURED (git) |
| **Playable-build commit** | `6ef1596c00825399322a827ea262adbf613fb2db` | MEASURED |
| Build artifact | `sim-ui-win-x64-6ef1596`, 81,635,456 bytes, `sha256:146c6fba24927ab05bb14dcb6d43237bed7aeda7769a05b979d8687aa0c8f388`, created 2026-10-05T15:52:12Z, expires 2026-11-04 | MEASURED (GitHub API) |
| Build run | `ui-artifact` run **37336233575** (workflow_dispatch on `m5-hardening` @ `6ef1596`): job `ui-artifact` success, job `windows-smoke` success | MEASURED (GitHub API) |
| HEAD vs build | Every commit after `6ef1596` on this branch is documentation only (this section, `docs/current-state.md`, README). `git diff --stat 6ef1596 HEAD` lists only `docs/` and `README.md`. | MEASURED |
| Window title | `civ-sim M5 (<sha>, <date>)` (`BuildInfo.Milestone = "M5"`, H3) | IMPLEMENTED |
| Schema | canonical v32 (H2: `TaxGrievances`) | IMPLEMENTED |

### 15.2 Every hardening change (what you will see differently)

**Research crash (Director §1) — FIXED (H1, F3).** Root cause: the renderer honoured `ImDrawCmd.VtxOffset` but
never declared `ImGuiBackendFlags.RendererHasVtxOffset`, so every ImGui draw list was capped at 65,535 vertices. The
A1 Technology tree needs 60,310 vertices as it opens at 1280×800, 68,034 with the pointer over it, 88,362 at FIT and
94,664–130,088 in wider windows (H1, MEASURED) — hence the assertion in `imgui_draw.cpp:2261`. Fix:
`ImGuiDrawData.Configure` declares the flag (called from `ImGuiRenderer.PrepareContext`, used by both the windowed
renderer and the headless harness, F3), `ImGuiDrawData.Plan/Check` compute and validate base vertex/index per frame,
and `DrawListCull` drops off-screen commands. The assertion is not suppressed and the screen is not disabled.
Regression tests render the real Research screen on the canonical turn-1 world (`ResearchScreenRenderTests`,
`ImGuiRendererSetupTests`); the shipped Windows binary is smoked in CI with a pre-fix control that must reproduce the
Director's exact dialog.

**GameUi / headless harness / `--smoke` gate (Director §2, §15) — NEW (H1, F3).** `SimUiGame` is a thin MonoGame
host; the per-frame UI is `GameUi`, driven headlessly inside a real native ImGui context through the game's own input
path. Every frame is checked for the 16-bit draw contract, duplicate ImGui ids and command-bar overflow. `Sim.Ui
--smoke` clicks every control the code draws in 9 states (A1 turn 1; one AI; wide window; target set; research done;
tax available at A3; Age advance; colony + revolt; narrow window at the 1080×640 minimum), then a seeded monkey pass.
Coverage: §15.6. Coverage table: `docs/m5-playability-gate.md` §6. Defects the gate found and fixed: chrome ignored
the window size; context panel kept scroll between sections; Age dialog not modal (End Turn worked underneath);
Escape on the Age panel exited the game; Age panel overlapped the selection card; Warband token did nothing.

**Windows smoke (Director §19) — NEW (H1).** `ui-artifact.yml` job `windows-smoke` runs the exact published
`app/Sim.Ui.exe --smoke` on windows-latest; a native assertion dialog is detected and fails the job (exit 134)
instead of hanging.

**Warband (Director §3) — RESOLVED from the code (H1; F3 test).** No order kind moves a formation; only
`AgeTransitionSystem` writes `MilitaryUnits` (identity conversion at an Age entry). Movement and battle are the M7
Battle Layer (ADR-033 D7). Clicking the token now selects it and opens a unit card (owner, station, family line
Warband → Axe warriors → Bronze-armed infantry, current form, next modernization) that states it cannot be moved or
ordered until the Battle Layer; its only control is Close.

**Taxation as continuous pressure (Director §4–§6, §17) — IMPLEMENTED (H2, F1).** `docs/d049-taxation-and-revolt-model.md`.
A levy-grievance stock per population segment (class × settlement, schema v32) integrates the felt burden per
sim-year; protest from T = 12, a segment's tipping point at T = 20, a growing rebel fraction past it; the settlement
changes hands only when rebels are a majority of its people. No `tax ≥ X → revolt` rule exists; the 100 % corner is
removed (`AFullLevyAtFullReach_IsNoLongerARevoltCorner_H2`). Happiness and legitimacy fall with accumulated pressure
and recover after a cut.
- **Local conditions:** felt = declared × (1 + 0.25 (1 − reach)) × (1 − 0.5 P) × (1 − 0.25 V) — P provision (food,
  housing, comfort goods), V services/development (public works, university maturity, reach). A poor place
  accumulates more from the same rate; a well-off or served capital can sustain 100 % without rising (d049 §6).
- **Food floor (F1):** rebels and protesters withhold the levied work, never their own food — food output is
  floored at the untaxed level (`Governance.FoodOutputMultiplier`). Before F1 a final settlement at 100 % starved to
  0 by turn 39; after, it reaches 3,553 people at turn 300 (F1, MEASURED).
- **State capacity (F1):** weak administrative reach aggravates the felt burden (`unrest.taxCapacityOffsetMax`
  0.25); at full reach the factor is exactly 1, so capital behaviour and goldens are unchanged.
- **Uprising needs the current ruler's levy (F1):** inherited grievance decays but cannot throw off a ruler who
  levies nothing at that settlement.

**A3 tax gate (Director §7) — IMPLEMENTED (H2, F1).** `sim.json governance.taxationMinAge = 3` inside
`Governance.CanLevyTax` (knowledge first, then Age; fails closed with no Age content). AI valve, hand-built orders,
the UI, save/load and replay all use that one predicate. The content loader refuses a `taxationMinAge` earlier than
the earliest Age at which the Taxation requirement can be met (F1). AI first levy in the `ci.yml` AI leg: Age III
decided at turn 465, first levy 5 % at turn 466 (MEASURED here from the run log); previously 369 while in A2.

**Revolt-Age inheritance (Director §8–§9) — IMPLEMENTED (H2).** A revolt-born polity takes its parent's current Age
(`RevoltSystem.InheritAge`), a copy of completed research, no progress, no Eureka credit, no capital, normal
research rate. The final settlement still cannot revolt away. D-048 rulings unchanged.

**Preview tools (Director §12) — FIXED (H3, F3, this pass).** `--action-preview` and `--player-views-preview` rigs
complete the content-named Taxation gate and stand at A3; process tests run all six preview flags. SVG font URLs are
relative (portable hashes, F3). Developer preview logs now print the player turn number. Every committed preview set
was regenerated once on this tree (`2b049f4`).

**Title, turn numbering, minimum window (Director §13; F3).** Title reads `civ-sim M5`. The player-facing turn is
`Clock.Turn + 1` everywhere on screen (HUD, research header, previews): the founded world reads **turn 1**. The window
cannot be resized below 1080×640 (below that the command bar's territory toggle left the window).

**ADR-034 order-validation deferral — PROPOSED (F3; pins added this pass).** `OrderValidation.ValidateAtDelivery`
(kernel file `Sim.Core/Kernel/OrderValidation.cs`) rejects, at delivery, a deferred actor or settlement id that does
not exist then. Called by `sim run --orders`, `sim replay`, `sim inspect`, `sim research`; each call site is now
pinned by a CLI test whose mutant (call removed) was measured to fail. **Awaits the Director's ruling** (kernel
file); no hash moves (it only reads PREV before Step).

**Colonies, ledger, calibration, AI soak (Director §10, §11, §14, §16) — MEASURED (H4; re-run here, §15.6).**
Colonies within the ruled behaviour (0 foundings in multi-settlement worlds — B1 is collapse-driven; 42 alive and
growing in lone-settlement worlds); ledger regression test passes and stress runs show exact conservation; two
replay defects fixed (revolt-polity actors deferred, `9bcad5b`; CLI drives revolt-born AI, `619b109`).

### 15.3 The Director's §18 decision record — RATIFIED (Director, 2026-10-05)

- **Taxation:** a continuous welfare/governance pressure, not an instant-revolt switch.
- **Extreme taxation:** 100 % tax is permitted as an extreme player action but does not directly or instantly revolt
  the population.
- **Revolt:** emerges after accumulated grievance/unrest/happiness deterioration reaches local tipping conditions.
- **Population impact:** a revolt affects a population segment, not automatically the entire settlement.
- **Local conditions:** food, amenities, housing, comfort goods, services, development and institutions/state
  capacity can mitigate tax pressure.
- **Taxation Age:** Taxation is A3/Bronze Age and cannot be operational before A3.
- **Revolt Age:** a revolted civilization inherits the parent's current Age.

### 15.4 Corrections to this record's earlier text

- **§11 (and §13 "Documentation") said `CLAUDE.md` "still reads M4 / `m4-spec.md`". False.** `CLAUDE.md` line 10
  has read "M5 Governing Gameplay — in progress" since `4448350` (verified on `bc87ef8`, `9bb7423` and this tree).
- §2.12 / §9 / §12 row 27 "preview rigs crash (KNOWN BUG)": **resolved** (H3).
- §9 "Revolt-founded polity starts at Age A1 (OPEN)": **resolved** — it inherits the parent's Age (RATIFIED §15.3).
- §9 "Research is not Age-gated … the AI levies in A2": research remains un-Age-gated by design (law 4); the **tax
  edict** is now Age-gated (A3), so the AI's levy moved to its first A3 turn.
- §9 "Colonies not re-measured since the forager layer": **re-measured** (H4) — within ruled behaviour.
- §9 "Benchmark not measured": measured (§15.6).
- §12 rows 11 and 22 and §13 rows "Tax gate", "Tax effect", "Unrest", "Revolt", "Units", "UI / map",
  "Documentation", "Benchmark", "Colonies" are superseded by §15.5 and §15.7.

### 15.5 Playtest checklist — changed rows (replace the §12 rows of the same number)

| # | TEST | EXPECTED BASELINE (hardening) | REGRESSION IF | CAPTURE |
|---|---|---|---|---|
| 0 | Research screen | Opens at any window size without an assertion; tree pans/zooms, FIT, hover, target selection all work. | Any ImGui assertion dialog; screen blank or frozen | Screenshot; window size |
| 1′ | Turn label | The status band reads **turn 1** on the founded world. | "turn 0" on screen | Status band |
| 6′ | Taxation gate | No tax control until **both** Taxation (Civics, 1,050 RP) is complete **and** your realm is in **Age III**. Until then POLICY shows no tax control at all (enabled or disabled) but a **locked line**, "Tax edict", with the reason: "needs Taxation (Civics) and the Bronze Age (Age III)", or, with Taxation known but Age < III, "Taxation (Civics) is known; needs the Bronze Age (Age III)". Shift+click the line for the edict's card. *(Corrected 2026-10-06: see the note below the table.)* | Tax control usable before A3; a levy recorded before A3; no locked line (or a wrong reason) before A3 | Policy panel; Age; order log |
| 11′ | Unrest | Taxation builds pressure over turns. Even 100 % does not revolt on the next turn. A well-provided capital can sustain 100 % (heavy protest, no rising); a poor or distant settlement accumulates faster. Protest drags non-food output; **food output never falls below the untaxed level**. Cutting the levy lets pressure decay. | Revolt on the turn after a levy; revolt with no levy by the current ruler; food output below untaxed under protest | Grievance / unrest lines per settlement; annals |
| 21′ | Units | Clicking the Warband opens a unit card that says it cannot be moved or ordered until the Battle Layer (M7). | Clicking it does nothing; any move control | Unit card |
| 22′ | Revolt | Only a **segment** (class) past its tipping point rises; the settlement changes hands only when rebels are a majority. Your last settlement never revolts away. The revolt-born polity starts in **your current Age**, with your completed knowledge, no progress, no capital. | Whole settlement revolting instantly; new polity at A1 above your Age; last settlement lost | Annals; `--answer polities` |
| 25′ | AI levy | With `--ai-empires 1` the AI's first levy comes on its first Age III turn (≈ 466 in the canonical CI leg). | AI levy while the AI is in A1/A2 | Order log kind 5, actor 2 |
| 28 | Window | Cannot be resized below 1080×640; every command-bar button stays visible. | A control off-screen | Screenshot |

> **Correction to row 6′, 2026-10-06 (M5 polish, stream INFO, branch `m5p-info`).** The row said "With Taxation known
> but Age < III the control is shown disabled with the reason." That was not true of the hardening build `6ef1596`
> (polish audit, verify finding G2): the POLICY tax control was **absent**, and the reason was only EMPIRE-view text.
> Under ADR-033 D2 the surface lists only available actions, so the control stays absent. What changed is that POLICY
> now shows a **locked line** for each governing domain the query does not list: the tax edict, road development and
> the Age advance. Each line gives the reason from the predicate that gates it (`Governance.GateOf`, the road-class
> knowledge query, `AgeQuery`). The row above now says this. Nothing else in this document changed.

### 15.6 Validation on the final tree (MEASURED, Release, 2026-10-05, code `6ef1596`)

Tests were run on `2b049f4`; its code is identical to `6ef1596` (the commits between them change only `docs/`).

| check | result |
|---|---|
| Release build `Sim.slnx` | 0 warnings, 0 errors |
| Sim.Tests (full) | **1531 passed, 0 failed, 12 skipped (manual measurement rigs), 1543 total**, 32 m 8 s |
| Sim.Ui.Tests (full) | **515 passed, 0 failed, 515 total**, 5 m 48 s |
| Gates | check-banned-constructs OK; check-read-isolation OK; check-readonly-proof OK; research-content-audit, research-calibration-report and research-gameplay-unlock-audit `--check` all current |
| `ci.yml` determinism-xproc step, run verbatim | exit 0. Orderless 2×400 identical; ordered vs replay 400 identical; founded 2×300 identical, final hash = FOUNDED_GOLDEN `07c6ec45…`; founded ordered vs replay identical; AI leg 2×600 `--ai-empires 1` identical (hash logs and run logs), 194 orders, kinds 3–8, replay identical (final `492659ef…`, unpinned), `sim inspect` "reproduction VERIFIED: 601 turns" |
| AI in that leg | A2 at turn 239; A3 decided at 465; first levy 5 % at 466 (one turn after A3) |
| Integrated save/load battery | `IntegratedSaveLoadBattery*` 8/8 |
| Calibration battery | `CalibrationBatteryTests` 7/7 |
| 20-seed autoplay (`autoplay --seeds 20 --turns 650`) | exit 0 (1,090 s) |
| `sim corridors` on that sweep | exit 0. Every gating corridor passes. The two quarantined corridors report QUARANTINE DRIFT and do not gate: density 19/20 seeds in the band, 18/20 in the recorded window; migration 0/20 in the band, 1/20 in the window. H4 measured the same before this pass. |
| `Sim.Ui --smoke` | exit 0. 9 states, 1363 checks: 1295 pass, 68 not offered, **0 fail**, 0 problems. 42,019 frames, 3,600 monkey actions, largest draw list 135,226 vertices |
| `Sim.Ui --smoke --ai-empires 1` | exit 0. 8 states, 1213 checks: 1154 pass, 59 not offered, **0 fail**, 0 problems. 37,498 frames, 3,200 monkey actions |
| windows-smoke (shipped `Sim.Ui.exe`, run 37336233575) | success. 9 states, 1363 checks: 1295 pass, 68 not offered, 0 fail, 0 problems. 37,474 frames. The pre-fix control reproduces the assertion dialog, as it must. |
| Preview tools | `--action-preview`, `--player-views-preview`, `--r2a-preview`, `--era-preview`, `--research-preview`, `--age-preview` and the institution/road fixtures all exit 0 through their scripts. PNGs were read: the turn-1 A1 screen shows "turn 1"; the A1 tree is usable; the A3 empire view shows the levy. |
| ADR-034 call sites | `OrderDeliveryValidationTests` 7/7. Removing the call in `run`, `research` or `inspect` makes the matching test fail (measured for each). |
| `sim bench --founded --seed 42 --turns 300` ×3, alternating, load 0.95–1.12 | base `9bb7423`: 39,924 / 38,829 / 40,228 ms (mean 39,660). Final: 40,362 / 39,234 / 39,118 ms (mean 39,572). **No regression.** |

### 15.7 Open / deferred list (supersedes §9 where they differ)

| issue | status | reference |
|---|---|---|
| Order-free forager starvation (canonical 11/20 seeds starve by 650; dev 15/20 by 1000; pre-forager 2/20, 1/20) | **OPEN** — shipped 4.3 / 2.0 kept; options (a) accept, (b) 5.0 per gatherer only, (c) 5.0 with ≈ 3.0 per km² (meets both dev targets; canonical/corridors unmeasured), (d) re-aim the dev tooth | `docs/m5-hardening-measurements.md` F2 + final-integration sections |
| ADR-034 (delivery-time order validation in a kernel file) | **PROPOSED** — awaits ruling | `docs/adr/adr-034-order-validation-deferral.md` |
| d049 §11 / §15 INFERRED choices: segments = classes; deterministic expected-value rebel fraction (no random draw); `uprisingGrievance` 20 chosen inside the fitted 17.4–21.0; food floor at the untaxed level (consequence: a final settlement at a permanent 100 % levy can sit in permanent revolt with non-food output near 0); state-capacity factor 0.25; uprising needs the current ruler's levy | **OPEN** — awaiting ruling | d049 §11, §15 |
| Colonies dormant in multi-settlement worlds | **BY RULING** (T4.4 §0: B1 is collapse-driven); the "stranded by capacity" line is unbuilt | H4 measurements §1 |
| Quarantined corridors (density, migration) | **OPEN** (quarantine; not gates) | `corridors.json`; CR-003 lineage |
| CR-019 raider / revolt-polity conflict | **DEFERRED** M7 / M8 | `docs/adr/cr-019-…` |
| Conquest, annexation gameplay, capital succession, reintegration of revolted settlements | **DEFERRED** M7 / M8 | §9; D-048 |
| 115 deferred research entities | **DEFERRED** M6 / M7 / M11+ | `docs/deferred-entity-realization-plan.md` |
| Later-Age military milestones; recruitment, movement, battle | **DEFERRED** M7 | ADR-033 D7 |
| Full Trade content | **DEFERRED** M6 / M11+ | R2a record |
| M6 Knowledge / Research / Technology | **NEXT** — not started | `docs/milestones.md` |
| M7 Battle Layer | **DEFERRED** | `docs/milestones.md` |
| AI issues no labour orders | **OPEN** (unchanged) | §3.2 A6 |
| CR-016, CR-005, CR-008 | **OPEN** (unchanged) | §9 |
| Window snap-back to the minimum size on resize | **INFERRED** to work (no display in CI); startup size is above the minimum | F3 |

### 15.8 Final baseline table (hardening; supersedes §13 rows of the same system)

| System | Expected baseline | Current implementation | Known issue? | Milestone | Test status |
|---|---|---|---|---|---|
| Research screen | Opens and is usable at any size | VtxOffset declared; per-frame check; cull | — | M5 | `ResearchScreenRenderTests`, `ImGuiRendererSetupTests`, `--smoke`, windows-smoke |
| UI controls | Every drawn control clicked in 9 states | `GameUi` + headless harness | 68 controls not offered (reason shown) | M5 | `PlayabilityGateTests`, `--smoke` |
| Warband | Selectable, card states M7 limitation | unit card | No movement (M7) | M5 / M7 | `PlayabilityGateTests` (Warband card) |
| Turn-1 food | Wild food 4.3 / 2.0 | `farming.preCultivation` ON | Order-free starvation OPEN | M5 | battery green; nightly in band |
| Tax gate | Taxation civic **and** Age ≥ III | `Governance.CanLevyTax` | — | M5 | `TaxAgeGateTests`, `TaxAgeContentAgreementTests` |
| Tax effect | Continuous pressure per segment; food floor | d049 v32 | INFERRED choices await ruling | M5 | `TaxPressureTests` A–H, `TaxRebelSubsistenceTests`, `TaxStateCapacityTests` |
| Revolt | Segment past tipping point; majority changes hands; Age inherited | `RevoltSystem` | — | M5 | `RevoltTests`, `RevoltAgeInheritanceTests` |
| Order validation | Deferred ids checked at delivery | ADR-034 (PROPOSED) | Ruling pending | M5 | `OrderDeliveryValidationTests` (7) |
| Colonies | Collapse-driven only | `ColonizationSystem` | Dormant in normal worlds (ruled) | M4 | H4 measurements |
| Goldens | founded `07c6ec45…`; AI leg final `492659ef…` (unpinned) | pins + `ci.yml` | — | — | xproc MEASURED §15.6 |
| Window / title | `civ-sim M5`; min 1080×640; turn 1 | Sim.Ui | — | M5 | `BuildInfoTests`, `PlayerTurnNumberingTests` |


## §16 — Director rulings 2026-10-06
See `docs/d050-m5-hardening-rulings.md`: ADR-034 ratified; forager 4.3/2.0 kept; segments = classes; deterministic rebel fraction, uprisingGrievance 20; permanent revolt of a last settlement accepted; capacity factor 0.25; capital succession deferred to M8. These were the open items of §15.
