# M6 slice — validation report (evidence)

Branch `claude/new-session-yavghm`. Baseline before this work: `06e0153` (CI run 39801418809 and ui-artifact run
39801418680, both success). Implementation integrated at `1fb5fdf` (merge of `050e1ea` = Track EX + Track CP
(`5af05ad`) + Track TREE (`ad21520`), on top of the governance commits `490941d`, `4cb34db`). `git diff 050e1ea
1fb5fdf` over `Sim.*`, `.github`, `scripts` is empty (0 lines): the merge added documents only. `main` = `m5-exit` =
`6e51a3e`, untouched.

## 1. Local checks on the integrated tree (`1fb5fdf` code; run 2026-10-23)

| Command | Exit | Result |
|---|---|---|
| `dotnet build Sim.slnx -c Release` | 0 | 0 errors |
| `./scripts/check-banned-constructs.sh` | 0 | OK |
| `./scripts/check-read-isolation.sh` | 0 | OK |
| `python3 scripts/research-content-audit.py --check` | 0 | current |
| `python3 scripts/research-calibration-report.py --check` | 0 | current |
| `python3 scripts/research-gameplay-unlock-audit.py --check` | 0 | current |
| `python3 -I docs/m6-plan/knowledge-redesign/tools/implement_tranche.py --check` | 0 | up to date |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Tests | 0 | 2,707 passed, 13 skipped, 0 failed (2,720) |
| `dotnet test Sim.slnx -c Release --no-build` — Sim.Ui.Tests | 0 | 1,099 passed, 0 failed |

Run by the implementation agent on `050e1ea` (same code), from scripts extracted from the final `ci.yml`: the
cross-process determinism step (found-settlement leg agrees over 300 turns; AI-empire leg agrees over 600 turns; run
log 400 orders, kinds {3–9, 12, 13, 14, 15}) and the allocation leg (4 split-checks PASS) — exit 0 each.

## 2. What the tests pin (new test classes)

- **Civics Points:** `CivicsPointsTests` (lanes, turn-exact civic-target delivery, banking, dt scaling, flooring,
  tie-dense AI lanes, switch, the identity `balance = generated − spent` every step on seeds 1, 2, 42, 7),
  `CivicsPointsSchemaV35Tests` (populated table, exact length, bit-exact round-trip, hash, v34 migration, refusals),
  `CivicsPointsAttributionTests`.
- **Stage gate:** `ResearchContentTests` (stage holds without `legal_code_roman`); `ResearchUnlockPipelineTests` T14
  (the university still needs it).
- **Discovery:** `DiscoveryTests` (concealment reproducibility, tie-dense ranking, realized share pinned per seed,
  deposit table unchanged by concealment, Prospect and Explore turn-exact pins, ledger-exact grain sinks, refusals
  charge nothing, no repeat survey, concealed row produces nothing before and produces after, explore radius growth
  and cap, extent monotone, cylinder wrap, founding refused on unknown land, sighting memory), `DiscoveryLeakTests`
  (metamorphic: perturbing unknown land, undiscovered concealed deposits and unseen rival settlements leaves AI orders
  and UI output identical, with positive controls), `DiscoverySchemaV35Tests` (sections 63–66 populated),
  `DiscoveryOrderLogTests`, `DiscoveryLayoutAttributionTests`, `DiscoveryActionsUiTests` (Actions menu listing, costs,
  blockers, emit guards, the "last turn" refusal line; Construction still lists Build Granary).
- **Content tranche:** `ResearchContentTests` (counts, fixture identity, D-047 chronology), the `PreTreeTranche` twin.

Realized concealment share over eligible ore rows (pinned):

| Seed | Turn 0 | Turn 300 (AI world, colonies included) |
|---|---|---|
| 1 | 6/24 = 25.0 % | 9/38 = 23.7 % |
| 2 | 7/24 = 29.2 % | 10/38 = 26.3 % |
| 42 | 7/24 = 29.2 % | 10/38 = 26.3 % |
| 7 | 6/24 = 25.0 % | 10/38 = 26.3 % |

## 3. Golden re-pins and their attribution

| Golden | v34 (pre-M6) | Final | Cause |
|---|---|---|---|
| Founded t300 (= ci.yml `FOUNDED_GOLDEN`) | `0f29efc7` | `8ccbe80f` | CP account + discovery rows/effects |
| Toy t200 | `2e6d4461` | `f97432f0` | layout only (empty sections 62–66) |
| Driven t300 | `eb88663a` | `392e2806` | CP + discovery |
| First reign t40 | `f7ca60a5` | `e3a86ce6` | CP + discovery |
| AI world t300 | `25f4799d` | `776d5d0c` | CP + discovery + content tranche |

Control: `TestConfigs.PreM6` (Civics Points off, discovery off, pre-M6 `research.json` fixture) reproduces every v34
value after `SchemaProjection` strips sections 62–66; single-layer switches (`PreTrackCp`, `PreStageGate`,
`PreTreeTranche`, discovery off) attribute each move. The stage-gate change alone moves no golden.

## 4. Measured gameplay consequences (MEASURED, AI empire, canonical seed)

| Pin | Pre-M6 | Final |
|---|---|---|
| First research targets | knapping_oldowan, ground_stone_early | grinding_stone (tech lane), kin_council (civics lane) |
| Advance to A2 | turn 221 | 111 |
| First road | 134 | 166 |
| First tax levy / A3 entry | 422 | **579** |
| Foundings by turn 300 (settlements) | 8 (14) | 10, first at 38 (16) |
| First build | 2 | 2 |

The levy/A3 delay is the CP economy pacing `taxation` (CP ≈ RP at 10-year turns, slower as turns shorten) — flagged
for tuning (roadmap priority 1). These are single-seed measurements of the canonical AI run, not a balance study.

## 5. Not covered or still open

- **CI on the pushed head**: recorded in the final report once the runs finish (workflow IDs).
- `--without-discovery` remains on the D-055 allocation leg and `CliConstructionLifecycleTests` (pre-M6 measurements).
- The calibration population trace is stale (predates M6); not re-measured here.
- `docs/m5-playability-gate.md` §6 not regenerated for the new gate rows.
- The educational corpus remains blocked from shipping (0 of 561 source-checked; source hosts unreachable).
- No Windows smoke or 20-seed soak run locally; they run in CI (`ui-artifact`, nightly) only.
