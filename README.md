# civ-sim (M5 — Governing Gameplay, in progress)

A deterministic, turn-based civilization simulation spanning 6,000 years. One human
director; AI agents build it, one task packet per session.

**M5 — Governing Gameplay: the CURRENT milestone, in progress (being finished, NOT complete).** M0–M4 are
complete (tags `m0-exit` … `m4-exit`). M5 is being finished on the integration branch `m5-integration`, which is
not merged to `main`; its decisions are ADR-033 ([`docs/adr/adr-033-m5-integration-pass.md`](docs/adr/adr-033-m5-integration-pass.md))
and its forensic playtest baseline is [`docs/m5-playtest-baseline.md`](docs/m5-playtest-baseline.md). On that branch
a player can, beyond M4:

- **Research** the Technology and Civics trees — one target at a time, research points from population, Eurekas,
  progress kept when switching (ADR-029, ADR-030) — and see actions appear from knowledge: turn 1 lives on gathered
  wild food, and Farming appears only once a crop is researched.
- **Advance through the Ages** when the realm's simulated state makes it eligible (the AdvanceAge order,
  ADR-031); the UI's era theme follows the player's Age.
- **Govern**: levy a tax through the tax edict, which opens with the **Taxation** civic (Civics, an Age III node;
  `sim.json governance.taxationRequires`); develop the roads between settlements; found universities.
- **Play against AI empires** (`--ai-empires N`); a settlement that revolts founds a new AI polity carrying its
  parent's completed knowledge (D-048).

The founding Warband is shown, but recruitment, movement and battle belong to the M7 Battle Layer. The full roadmap
is below and in [`docs/milestones.md`](docs/milestones.md).

> *Dated note (2026-10-05):* until this date the headline here was M3's, reproduced below unchanged as the
> M3 summary.

**M3 — The economy arrives (M3 summary).** At M2 every settlement was the same food-machine
running at a different size. At M3 they are *places that make different things*.
What the world can now do that it could not before:

- **Produce across five sectors** — farming, herding, extraction, crafting,
  construction — over a roster of real goods (grain, timber, stone, clay, ores,
  fibre, hides → tools, pottery, cloth, bronze). Recipes consume inputs; a
  workshop with no clay makes no pots.
- **Be ruled into a production mix.** The director allocates labour per
  settlement across the five sectors and the settlement's whole economy follows:
  what it makes, what it runs short of, what its people's needs read.
- **Price things.** A per-settlement, per-good price solver (D-033) runs on the
  exact closed form of its damped step (ADR-016), driven by consumption, input
  demand, production and stock release, and it settles rather than oscillating.
- **Want more than food.** Consumption is a class-weighted basket over six goods
  (D-035), aggregated by CES into Sustenance, Shelter and Comfort — so a
  well-fed settlement can still be poorly housed.
- **House people.** Dwellings are built, maintained and decay; Shelter is a real
  stock, and a settlement that stops maintaining housing degrades (T3.8).
- **Grow its own hinterland.** One dirt path can enlarge a settlement's arable
  catchment by 16.6% — infrastructure as a differentiator between places, live
  for the first time (T3.2b).

Its known-open edges are recorded with the same care as its features — see the
M3 entry in [`docs/milestones.md`](docs/milestones.md), which names what this
milestone deliberately did NOT deliver, each with a measurement and an owner.
The largest: **goods do not yet trade on the canonical world**, for two measured
and escalated reasons.

Start with [`CLAUDE.md`](CLAUDE.md) (agent constitution), [`docs/current-state.md`](docs/current-state.md) (the
routing document — verify it against git) and M5's decision record,
[`docs/adr/adr-033-m5-integration-pass.md`](docs/adr/adr-033-m5-integration-pass.md) (M5 has no separate spec file);
[`docs/m4-spec.md`](docs/m4-spec.md) is the previous milestone's spec.

**Roadmap (Director rebase, 2026-10-03):** M0 Kernel · M1 Walking Skeleton · M2 Demography / Food · M3 Production /
Markets · M4 Empire / Strategic Foundation (all complete) · **M5 Governing Gameplay (current, being finished)** · M6
Knowledge / Research / Technology (next) · M7 Battle Layer · M8 Politics / Diplomacy · M9 Society · M10 Integrated Civilization Simulation · M11+ Depth & Content Expansion. See [`docs/milestones.md`](docs/milestones.md). Ages (A1–A9) are a
simulation dimension, not milestones. (2026-10-05: the spec pointer above, which until then named `m4-spec.md` as
the current spec, now names M5's decision record.)

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.0.x)

## Run commands

```bash
# Build everything
dotnet build Sim.slnx

# Run the test suite (xUnit + FsCheck)
dotnet test Sim.slnx

# Banned-constructs check (determinism gate, m0-kernel-spec §3.7) — run before every commit
./scripts/check-banned-constructs.sh

# Read-only view proof (T0.2 acceptance): passes only when mutation through
# IReadOnlyWorldState FAILS to compile
./scripts/check-readonly-proof.sh

# Headless CLI runner — see "CLI" below
dotnet run --project Sim.Cli --configuration Release -- run --seed 42 --turns 1630 --report
```

## CLI

`sim` is a scripting surface: deterministic output; exit code **0** on success,
**1** on usage errors, **2** on runtime failures — exit codes are its contract.

The block below is the original core. `sim` with no arguments prints the authoritative usage, including what this
block omits: `--founded [--size PX] [--settlements N] [--ai-empires N]` on `run` and `replay`, and the later
commands `diff`, `inspect` (the forensic reader of a played session's `runs/` files), `worldgen`, `corridors` and
`research` (dated note, 2026-10-05).

```bash
# Run a campaign; optionally save a snapshot at turn K, log per-turn hashes
# (one lowercase hex WorldHash per line, \n-terminated), consume an order log
sim run --seed S --turns N [--report] [--save-at K --save PATH]
        [--orders PATH] [--hash-log PATH]

# Recompute and print the canonical hash of a save
sim hash SAVEFILE

# Replay from seed + order log (the D-008 recovery path)
sim replay --seed S --orders PATH --turns N [--hash-log PATH]
        [--report-jsonl PATH [--report-every N]]

# Per-phase wall time and allocations (clone + each system, first-seen order),
# plus the state footprint: bucket row count and clone bytes per turn (T3.11 —
# the instrument for the m0-kernel-spec §3.2 clone-size claim)
sim bench --seed S --turns N [--founded [--settlements N]] [--json]

# T2.8 calibration data source: N independent canonical founded worlds
# (seeds seed-base..seed-base+N-1, default base 1), T no-order turns each,
# per-seed metrics to OUT.json. Deterministic: same (seed, turns) => same bytes.
sim autoplay --seeds N --turns T --metrics OUT.json [--seed-base S]
```

`sim bench --json` emits one JSON object — the future perf-gate input (no gate
yet: toy systems would make thresholds meaningless):

```json
{ "seed": 42, "turns": 500, "totalMs": 9.19,
  "bucketRows": 384, "cloneBytesPerTurn": 82096,
  "phases": [ { "name": "clone", "totalMs": 0.88, "allocatedBytes": 335648 }, … ] }
```

### Replay diagnostic report (schema `replay-report/v1`)

`sim replay --report-jsonl PATH` turns any played session into a **reproducible
dataset**. Without it, an orders `.bin` plus a chronicle `.txt` carry almost no
state — the chronicle records emergence and migration events only, the orders log
records inputs rather than outcomes, and everything between (stocks, prices,
needs, grievance, class counts, sector mixes) is visible only to whoever is
sitting at the machine. That makes the player the measuring instrument.

One JSONL line per reported turn:

```json
{ "schema": "replay-report/v1", "turn": 60, "year": 600.0, "dtYears": 10.0,
  "hash": "…64 hex…", "totalPopulation": 20431, "totalFood": 3120044,
  "totalTradeFlow": 0,
  "settlements": [ {
    "id": 0, "population": 1783,
    "cohorts": [16 counts],
    "classes": [ { "id": 0, "name": "Peasants", "count": 1783, "active": 1,
                   "needs": { "Sustenance": 0.97, "Shelter": 1.0, "Comfort": 0.0 },
                   "grievance": 132.46 } ],
    "sectors": { "farming": 0.55, "herding": 0.15, … },
    "goods":   [ { "name": "grain", "stock": 260003, "demanded": 8915,
                   "eaten": 8915, "produced": 12004, "price": 1.0 }, … ],
    "housing": { "dwellings": 421, "maintenanceFraction": 1.0,
                 "sizeTier": 2, "arableKm2": 4210.6 }
  } ] }
```

**JSONL, not CSV, deliberately.** The data is ragged — variable classes per
settlement, each with variable bound needs, alongside ~13 goods carrying several
numbers each. CSV forces either a column explosion or several files joined on a
composite key, and both bake registry sizes into a header contract that breaks
whenever a good or class is added. JSONL is self-describing per line, streams,
appends without a header, and `jq` reads it directly.

**Volume, measured** (canonical founded world, N = 12, 650 turns):

| interval | size |
|---|---|
| `--report-every 1` (**default**) | **12.5 MiB** |
| `--report-every 10` | 1.25 MiB |

The default is **every turn**: a diagnostic that silently skips turns can hide
the exact turn a finding occurred, so the default is lossless and the flag exists
for when volume matters.

**It is strictly an observer.** The report is written FROM the post-step world and
never feeds back; the step call is identical whether or not reporting is on.
Asserted, not assumed (`ReplayReportTests`): the same log produces the same world
hash with and without `--report-jsonl`, and the report bytes themselves are
identical across runs.

### Autoplay metrics (schema `autoplay-metrics/v1`)

The JSON `sim autoplay --metrics` emits is the calibration battery's **input
contract** — `Sim.Core.Kernel.AutoplayMetrics`/`CalibrationAnalysis` compute
the same objects in-process for the CI battery
(`Sim.Tests/Systems/CalibrationBatteryTests.cs`), and the corridor bands live
in `Sim.Data/content/corridors.json` (TUNE data, D-006):

```json
{ "schema": "autoplay-metrics/v1", "turns": 650,
  "seeds": [ {
    "seed": 1, "worldHash": "…64 hex…",
    "finalPopulation": 115627, "finalYear": 4500.0,
    "settlementCount": 12,
    "arableKm2": 388145.2,          // Σ EffectiveFarmland × lattice-block km²
                                    // (fertility-WEIGHTED arable — the honest
                                    // definition; raw land area would flatter
                                    // density by counting desert as arable)
    "finalCohortTotals": [16, 5-year cohort counts],
    "series": {                     // parallel arrays, one entry per turn
      "year": [...], "dtYears": [...], "population": [...],
      "births": [...], "deaths": [...],        // deaths = base + starvation
      "starvationDeaths": [...],               // per-turn ledger-sink delta
      "migrationGross": [...]                  // Σ settlement outflows
    },
    "derived": {
      "densityPerArableKm2": 0.298,
      "migrationGrossPerDecade": 0.0004,       // fraction of pop per decade
      "crashCount": 0                          // ≥20% peak-to-trough drawdowns
    } } ] }
```

Nightly (`calibration-nightly` job, cron + manual dispatch) sweeps ≥20 seeds:
`sim autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json`.

**Density vs D-015 ("map feels small") verdict, T2.8:** at year 4500 the
canonical world holds ~0.30–0.36 people per fertility-weighted arable km²
(measured across seeds) — three orders of magnitude below mature agrarian
land use (~10–30/km²). The map is **not** small for M2's horizon; the D-015
concern is about *travel scale*, not carrying capacity, and no worldgen
resize is warranted on density grounds.

> **SUPERSEDED AT T3.2b (CR-002) — kept because the reasoning is still
> instructive.** The verdict above was measuring a DENOMINATION BUG: "arable
> km²" was fertility-weighted lattice NODES scaled by block area in one consumer
> and not in the other, and the 205 km catchment radius that made the world look
> full was compensating for a yield constant denominated 256× too coarse. Both
> are fixed. At a 50 km economic hinterland the twelve settlements claim ~2 % of
> the continent, so the honest picture is the opposite of "small": the world is
> overwhelmingly EMPTY, and the open item is that no mechanism lets a growing
> population take the frontier. Reframed in `docs/queue.md` as an expansion
> opportunity, M4-targeted (colonization / land clearance, CR-003 §5.2(a)).

CI runs four jobs on every push and pull request: `build-and-test` (gates + full suite),
`determinism` (the T0.8 in-process harness), `determinism-xproc` (T0.9:
two separate `sim run` processes must produce byte-identical hash logs, and
`sim replay` must reproduce an ordered run byte-identically — separate processes
surface environment/JIT divergence the in-process twins share), and `calibration` (the battery, time-boxed);
`calibration-nightly` (the ≥20-seed sweep) runs on the daily schedule or on dispatch. (Until 2026-10-05 this
paragraph named three jobs.)

CI (`.github/workflows/ci.yml`) runs the banned-constructs check, build, and tests on
every push and pull request.

## Download & Play

**M5 playtest build 3 (2026-10-04, branch `m5i-r5-taxation-node` @ `5c364b5`, not on `main`)**:
[ui-artifact run 37203130829](https://github.com/adityamisra010-cmd/civDemo/actions/runs/37203130829)
→ download `sim-ui-win-x64-5c364b5` (Windows x64 zip; artifact expires after 30 days; sign in to GitHub to download).
Unzip and double-click `Play civ-sim.cmd` (it starts `app\Sim.Ui.exe`). For flags, open a terminal in the unzipped
folder and run `app\Sim.Ui.exe` with them: try `--ai-empires 1` for an AI rival; F12 or `--dev` opens the developer
panels; `--resume <session-dir>` continues a saved session. Turn 1 now lives on gathered wild food (agriculture must be
researched); the tax edict appears only once the Taxation civic is researched; later-Age military milestones are pending the Battle Layer (M7).

> *Dated note (2026-10-05):* known in build 3 — opening Research aborted the Director's 2026-10-05 playtest with
> ImGui's assertion "Too many vertices in ImDrawList using 16-bit indices", and its window title reads
> `civ-sim M4`. The M5 hardening pass is fixing both on its branches; a newer build will replace this paragraph.
> (Until this date the paragraph said "Unzip, run `Sim.Ui.exe`"; since T3.11 the zip's root holds only the
> launcher and `app/`.)

No toolchain needed — download, unzip, double-click `Play civ-sim.cmd`.

**Latest build** (every merge to `main`): Actions → the newest `ui-artifact`
run on `main` → download the `sim-ui-win-x64-<sha>` artifact.
**Stable milestones**: the [Releases page](../../releases) — publishing a
release automatically attaches its zip as a permanent asset.
**Gate builds**: every `t<N>.*` packet-branch push produces the same artifact
for Director Visual Gates.

The window title and debug panel both show `civ-sim M5 (<sha>, <date>)` — the
build you are holding is never ambiguous (the label is the current milestone, `Sim.Ui/BuildInfo.cs`; M5 playtest
builds up to build 3 show `civ-sim M4`, and this sentence said `civ-sim M3` until 2026-10-05). Optional flags:
`--seed N` (default
42), `--size PX` (dev-preview world size; a non-canonical size is recorded
in the session-log filename), `--settlements N`, `--ai-empires N` (found N
AI-commanded Empires to play against, ADR-033 D5; default worldgen.json's
`aiEmpires`, which is 0 — the override is recorded in the session manifest and
the log name, `-aN`), `--dev` (open with the developer panels; F12 toggles them) and
`--resume DIR|MANIFEST` (continue a saved session by replaying its order log). `--help` prints the usage line.

Each played session writes six files into `runs/` (beside the launcher; in the
current directory when `Sim.Ui.exe` is run directly), twinned by the same
timestamp and the same `[-sPX][-nN][-aN]` suffixes (until 2026-10-05 this list
named only the first two):

- `orders-<yyyyMMdd-HHmmss>[-sPX][-nN][-aN].bin` — the order log (the replay
  input; lexicographic order = chronological, so back-to-back gate logs sort
  and sweep trivially);
- `chronicle-….txt` — the annals export (T2.9), byte-exactly the Annals panel's lines;
- `session-….json` — the manifest (seed, overrides, build identity), written once at launch so a session that
  crashes is still reproducible; `--resume` and `sim inspect --manifest` read it;
- `trace-….csv` — one line per turn ending in that turn's world hash (`--resume` checks the replay against it);
- `telemetry-….jsonl` — every turn's world and settlement records, appended each turn;
- `forensic-….jsonl` — the run record, opened at launch and closed (with every companion's content hash) on a
  clean exit.

A session log + its seed replays hash-identically:

```bash
sim replay --founded --seed S --orders runs/orders-<stamp>.bin --turns N
# played on --size PX? add: --size PX  (the -sPX filename suffix tells you)
# played with --settlements N? add: --settlements N  (the -nN suffix tells you)
# played with --ai-empires N? add: --ai-empires N  (the -aN suffix tells you)
```

## Solution layout

| Project | Purpose |
|---|---|
| `Sim.Core/` | Kernel + all simulation systems; zero UI/IO deps beyond data loading. Subfolders: `Kernel/` (turn executor, clock, RNG, state infra, hashing, snapshots), `Systems/` (one folder per system), `State/` (WorldState — single source of truth) |
| `Sim.Data/` | JSON content files + schema validation (era table lives here) |
| `Sim.Cli/` | Headless runner: run / hash / diff / replay / inspect / bench / autoplay / worldgen / corridors / research (`sim` with no arguments prints the usage) |
| `Sim.Ui/` | The game window (MonoGame + ImGui.NET): map, panels, research trees, Ages, the action surface; also the headless `--*-preview` SVG tools |
| `Sim.Tests/` | xUnit + FsCheck: unit, property, determinism, golden-run |
| `Sim.Ui.Tests/` | UI view-model, rendering and preview-tool tests (headless) |
| `Sim.Tests.ReadOnlyViolation/` | Compile-failure proof that `IReadOnlyWorldState` cannot be mutated (`scripts/check-readonly-proof.sh`; not in `Sim.slnx`) |
| `docs/` | Specs, addenda, ADRs (`docs/adr/`), amendment queue (`docs/queue.md`) |

**Dependency rule:** systems never reference each other — only `State` and `Kernel`.
Cross-system communication is exclusively through state tables and events.

## Milestone status

Statuses brought up to date on 2026-10-05 from `docs/milestones.md` and the exit tags; the descriptions are kept as
written at each milestone's gate.

- **M0 — Simulation kernel: COMPLETE.** T0.1–T0.9 per `docs/m0-kernel-spec.md`:
  state infrastructure, PCG32 RNG registry, integer-day clock + era pacing,
  turn executor + pipeline-as-data, Ledger + exact conservation, canonical
  serialization + WorldHash + snapshots + order-log replay, the permanent
  determinism harness and cross-process CI jobs, and the `sim` CLI.
- **M1 — Walking skeleton: COMPLETE.** T1.1–T1.10 per
  `docs/m1-walking-skeleton-spec.md`: worldgen fields + hydrology, traversal
  lattice + pathfinding, settlement + catchment, population + food loop, labor
  orders + PathBuild, the Sim.Ui window (terrain, overlays, HUD, End Turn),
  founded-world harness + goldens, and the CI Windows artifact.
- **M2 — Population & Society: COMPLETE** (exit 2026-07-25, tag `m2-exit`; this line read "at the exit gate"
  until 2026-10-05). T2.1–T2.12 per
  `docs/m2-spec.md`: cohort buckets (D-026), class system + D-020 DSL, plural
  worldgen with partitioned catchments (N = 12), per-settlement UI rule,
  migration (D-021, stabilized: gap-closing caps + EMA-smoothed
  attractiveness), historical demographic retune on the ADR-011
  exponential-survival micro-step kernel (dt-invariant growth, era-boundary
  continuity pinned forever), needs registry + grievance stocks (read by
  nothing but UI/chronicle — grep-gated), autoplay + calibration battery with
  two-sided corridors, chronicle-lite + procedural names + annals export,
  time-series graphs on the D-028 UI ring buffer, and the T2.11 determinism
  horizon across the era gate. Exit accepted 2026-07-25 on the T2.13 fix
  evidence. (Post-exit record, CR-003: M2's Malthus corridor was measuring an
  artifact of two compensating errors, corrected at T3.2b — the mechanism is
  intact but the condition that made it visible was false. M2 does not reopen;
  the record states it.)
- **M3 — The economy arrives: COMPLETE** (Director's exit ruling 2026-08-06, tag `m3-exit`; this line read "AT
  THE EXIT GATE … Awaiting the director's exit session" until 2026-10-05). T3.1–T3.12 per
  `docs/m3-spec.md`: worldgen refresh + the goods/recipe roster, five-sector
  production (D-032) with the M2 scaffolding demolished, the CR-002 spatial and
  agronomic recalibration, the D-033 price solver on ADR-016 exact integration,
  D-035 consumption baskets and CES needs, D-034 trade & arbitrage, founding
  variation (ADR-017), settlement size + housing as a real stock, the market and
  sector-control UI with the trade panel, and the T3.11 harness work (a DRIVEN
  golden that finally exercises the goods economy). **What it deliberately did
  NOT deliver is recorded beside what it did** — see `docs/milestones.md`.
- **M4 — Empire / Strategic Foundation: COMPLETE** (closed on the Director's final closure rulings 2026-09-21,
  tag `m4-exit`): the Empire control foundation (polities, control, capitals, command source), neighbours and
  conflict, and a world that can run short — see `docs/milestones.md` §M4 and `docs/m4-exit-inventory.md`.
- **M5 — Governing Gameplay: CURRENT, in progress (being finished, NOT complete)** on the unmerged
  `m5-integration` branch: see the headline above, ADR-033 and `docs/m5-playtest-baseline.md`.
- **Next:** M6 Knowledge / Research / Technology (owns and completes the research engine, Ages, unlock pipeline and
  universities built early on the M5 branches) · M7 Battle Layer · M8 Politics / Diplomacy · M9 Society · M10
  Integrated Civilization Simulation · M11+ Depth & Content Expansion (`docs/milestones.md`, roadmap rebase
  2026-10-03).

## Calibration battery

```bash
# The CI battery members (2 canonical + 2 dev seeds, bands from
# Sim.Data/content/corridors.json — TUNE data; two-sided, no-output-is-failure)
dotnet test Sim.Tests --configuration Release --filter CalibrationBatteryTests

# The >=20-seed sweep (the calibration-nightly CI job; also manual)
dotnet run --project Sim.Cli -c Release -- \
  autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json
```
