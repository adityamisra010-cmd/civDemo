# Baseline research progression — current build, four seeds

**What this is.** A measurement of how research progresses in the build as it stands (m5-exit source), produced
for the M6 knowledge-system redesign package. It is evidence, not a proposal. No tracked file was changed, nothing
under `Sim.*` was touched, and every simulation number comes from the existing CLI.

**Labels.** Every figure is **MEASURED** (printed by a CLI run listed in §2, or read directly from a file such a run
wrote: order log, population trace, session trace, telemetry) or **INFERENCE** (derived by me from measured
artefacts by a rule stated in this document). In running text, [M] = MEASURED and [I] = INFERENCE. Tables carry the
label in a column or in their title. No figure here is an ESTIMATE. One input is approximated, the AI polity's RP
in step 0 (§6.2), and it is labelled where it is used.

**Vocabulary.** *Driver* = `sim research --auto cheapest`. It is a measurement driver, not an AI: whenever polity 1
has no research target it orders the cheapest available node (EffectiveCost, then key). *AI polity* = PolityId 2 of
`sim run --founded --ai-empires 1`. *Turn* = the clock turn of a world state; a node "completes at turn c" when it
is first complete in the state of turn c (the CLI's own "N nodes complete by turn c" convention). *Sim-year* =
`floor(Clock.WorldDateYears)`, years since the campaign start, as the CLI prints it (era-pacing.json puts sim-year
0 at −4000 and 6100 at 2100 in its own frame; no calendar meaning is claimed here).

## 0. Key numbers and findings

#### T0 key numbers

| | label | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|
| driver: research stage reached, turn (sim-year) | MEASURED | 996 (5492) | 998 (5496) | 1016 (5532) | 1010 (5520) |
| driver: nodes completed by turn 1630 (of 433) | MEASURED | 238 | 238 | 234 | 235 |
| driver: RP generated over the 1630 steps | INFERENCE | 739,003 | 734,298 | 708,392 | 715,742 |
| driver: taxation completes (turn) | MEASURED | 478 | 480 | 489 | 487 |
| driver: law_code completes (turn) | MEASURED | 619 | 621 | 631 | 629 |
| driver: legal_code_roman completes (turn) | MEASURED | 671 | 673 | 685 | 683 |
| driver: census completes (turn) | MEASURED | 886 | 888 | 905 | 900 |
| driver: coined_wage completes (turn) | MEASURED | 1400 | 1403 | 1432 | 1423 |
| driver: joint_stock completes (turn) | MEASURED | 1409 | 1412 | 1441 | 1432 |
| driver: patent completes (turn) | MEASURED | never | never | never | never |
| AI polity: research stage reached (turn) | INFERENCE | 702 | 716 | 710 | 705 |
| AI polity: nodes completed by turn 1630 | INFERENCE | 340 | 338 | 344 | 263 |
| AI polity: RP generated over the 1630 steps | INFERENCE | 2,803,367 | 2,692,309 | 3,141,274 | 1,428,123 |
| AI polity: taxation completes (turn) | INFERENCE | 328 | 339 | 336 | 335 |
| AI polity: law_code completes (turn) | INFERENCE | 608 | 622 | 618 | 613 |
| AI polity: legal_code_roman completes (turn) | INFERENCE | 635 | 649 | 644 | 639 |
| AI polity: census completes (turn) | INFERENCE | 1374 | 1386 | 1358 | 1624 |
| AI polity: coined_wage completes (turn) | INFERENCE | 1233 | 1245 | 1225 | 1422 |
| AI polity: joint_stock completes (turn) | INFERENCE | 1287 | 1299 | 1279 | 1495 |
| AI polity: patent completes (turn) | INFERENCE | 1350 | 1362 | 1339 | 1598 |
| AI polity: population at turn 1630 | MEASURED | 8,141,111 | 7,720,582 | 10,293,255 | 3,476,632 |
| driver: population at turn 1630 | MEASURED | 553,245 | 548,460 | 520,530 | 527,651 |

1. **Pace.** The driver completes 234–238 of the 433 nodes in the 1630-turn game [M]. That is 6.1–6.3 % of the
   11,059,750 RP finite workload [I]. Ages A1–A4 are completed in full, A5 29/32, A6 36–40/42, A7 11/32, A8 9/53
   and A9 0/125 [I, T5].
2. **Research stage** (the five subtrees open) at turns 996–1016, sim-years 5492–5532 [M]. In every run,
   driver and AI alike, `geometry_axiomatic` is the stage atom that completes last [I, T7/T11].
3. **Civics.** Six of the seven civic nodes complete [M]. `patent` never does: at turn 1630 it is LOCKED because
   `printing_press` (39,780 RP) is never the cheapest available node [M, `--node patent`].
4. **Cheapest-first defers core milestones.** The A2 core node `cereal_cultivation` completes only at turns 517–529
   [I]. The A6 core node `heavy_plough` (3,520 RP) never completes, because it sits behind `cast_iron` (16,730 RP)
   via `iron_mouldboard` [I, content prerequisites]. The A7 core node `printing_press` never completes [I].
5. **Seed sensitivity is small and is pace only.** The driver's sequence of targets is identical in all four seeds
   [I: each shorter order log is an exact prefix of seed 1's]. Seeds change only how fast it moves. The spread
   between seeds grows from 8 turns at the 50-node mark to 28 turns at the 200-node mark [M], and the stage spread
   is 20 turns [M].
6. **RP lost.** RP past a node's completion is discarded by design (no bank). That is 46.6k–49.7k RP per game,
   6.5–6.7 % of all RP generated [I]. The same 8 Eurekas fire in every seed, for 54,116 RP of credit [M].
7. **The AI polity researches far more**, and it does so on a much larger population. By turn 1630 it has
   completed 263–344 nodes [I] against the driver's 234–238 [M]. It reaches the research stage 282–306 turns earlier
   [I], with 46 nodes complete against the driver's 153 [I], and it completes all seven civics [I]. Its population
   reaches 3.5–10.3 M [M] against the driver's 0.52–0.55 M [M], and its RP over the game is 2.0–4.4× the driver's
   [I]. No CLI output reports the AI's research directly. It was reconstructed from the AI run's own order log and
   checked against telemetry (§6).
8. **The prior calibration report's measured section is stale for this build.** For seed 42 the population at
   turn 1 is 5,118 today against 5,245 in `scripts/research-calibration/population-trace-seed42.csv` [M]. Cumulative
   RP under that report's convention is 709,153 against its 696,745 [M]. `research.json` is byte-identical (same
   SHA-256), so the difference comes from the world's dynamics, not from research content.

## 1. Provenance

| item | value |
|---|---|
| worktree / branch | `/home/user/m5w/dev`, `claude/new-session-yavghm` |
| HEAD | `edba2b57b8e821cc128218216e4cbad3693839b5` (committed 2026-10-21T18:22:47Z) |
| main | `github/main` = tag `m5-exit` = `6e51a3e26c6b10e179335db3042898f1251099de`; HEAD descends from it |
| source equivalence | `git diff --stat 6e51a3e HEAD -- . ':!docs'` printed nothing: the commits beyond main are docs-only, so the binaries below are m5-exit's code and content |
| build | `dotnet build Sim.Cli -c Release` with SDK 10.0.112, linux-x64, run 2026-10-22T06:50:14Z–06:50:44Z (30 s wall; dotnet reported 00:00:21.13): `Build succeeded. 0 Warning(s) 0 Error(s)` |
| DLLs (bin/Release/net10.0) | `Sim.Cli.dll` mtime 2026-10-22T06:50:44Z, SHA-256 `86e5b2109ccc619b2369875b101930aca8099b5c6257a1d1d51f1d1fe44905cb`; `Sim.Core.dll` 06:50:43Z, `1f7f48bde9c7069a6aeae039ec949ea1267efa5b467a24627f749bab7e8c8816`; `Sim.Data.dll` 06:50:39Z, `b802d7cf089541774192a8849a69a4d80abec29e29606b4aa525fe8dd50385b3` |
| content SHA-256 | research.json `7a3bf38896f443e4cdfe582503d57f7f17abbab4f87acafe5ac4c3ef8ed9b791` (the hash `docs/research-calibration-report.md` states); ages.json `90ea2c2a89279ab3f1b9e3fe541c40132918ccddf0bf739355fae45f6c399840`; era-pacing.json `61fb4aff6b0827fc189283b277812138584a3ca12d0b3aaaef7864d47f4afc81`; sim.json `5f38c4a771762005221f27e891d6ebc749fbde42817d499e6c7b9ff626c77bea`; worldgen.json `ef018566879209660992fa6ba82e6bc46d0eac00623ed8feffd7550fd9851f1a` |
| machine | 4 vCPU, x86_64; runs executed four at a time (wall-clock figures below include that contention, so they are not benchmarks) |
| run window | 2026-10-22T06:51Z–07:22Z (CLI runs) |

The session manifests the AI runs wrote record `buildSha: "dev"`, `buildDate: "local"`, because a local build
carries no stamp. This table is therefore the provenance of record.

## 2. Configuration and exact commands

**Full game = 1630 turns.** The era-pacing.json bands give Neolithic 250 turns at dt 10, Bronze/Iron 400 at 5,
Medieval 300 at 3, Early Modern 200 at 2, Industrial 120 at 1, Modern 160 at 0.5 and Information+ 200 at 0.5. That
is 1630 turns, from sim-year 0 to 6100 [M, T1]. This is exactly the configuration of
`docs/research-calibration-report.md` §5 (`sim research --seed 42 --turns 1630 --auto cheapest
--trace-population`). That report's generator header names the same command. I reused it unchanged and extended
it to seeds 1, 2 and 7, adding two observers: `--emit-orders` and `--node patent`.

**World.** The founded canonical world (worldgen.json: sizePx 1024, settlementCount 12). worldgen.json has no
`aiEmpires` key, so it defaults to 0, and polity 1 is the only Empire, holding all 12 settlements at founding.
`sim research` has no `--ai-empires` option.

All commands were run from `/home/user/m5w/dev`, with `SIM = dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll` and
`M = docs/m6-plan/knowledge-redesign/measurements`:

```
# driver, full game (4 concurrent; wall 50.6 / 48.3 / 49.6 / 47.7 s for seeds 1 / 2 / 42 / 7)
$SIM research --seed S --turns 1630 --auto cheapest --emit-orders $M/raw/research-auto-orders-seedS.bin \
     --trace-population $M/raw/population-trace-seedS.csv --node patent  > $M/raw/research-seedS.stdout.txt

# turn-0 population (the trace starts at turn 1); row 0 of the session trace kept as raw/turn0-session-trace-seedS.csv
$SIM run --founded --seed S --turns 1 --emit-session <scratch dir>

# direct prefix checks (56 runs, 4 concurrent, 501 s total): each completed civic node at c-1 and c,
# and the 200th technology node at t-1 and t
$SIM research --seed S --turns N --auto cheapest --node ID  > $M/raw/checks/research-seedS-turnsN-node-ID.stdout.txt

# fidelity: the driver's log replayed through `sim run` (which since H4 also drives any AI polity a revolt founds)
$SIM run --founded --seed S --turns 1630 --orders $M/raw/research-auto-orders-seedS.bin   # wall 44.4-46.8 s

# AI polity (4 concurrent; wall 130.0 / 151.8 / 137.2 / 159.0 s for seeds 1 / 2 / 42 / 7)
$SIM run --founded --seed S --ai-empires 1 --turns 1630 --emit-session <scratch dir>
python3 -I $M/ai_telemetry_reduce.py <telemetry-*.jsonl> $M/raw/ai/ai-polity-population-seedS.csv

# analysis (reads raw/ and the content files; writes the CSVs and the JSON) and tables (this document's tables)
python3 -I $M/baseline_analysis.py
python3 -I $M/baseline_tables.py $M/baseline-summary.json
```

`--emit-orders`, `--trace-population` and `--node` are observers. The final world hash of every driver run equals
the hash `sim run --orders` reaches from the same log (§3, check h), which also shows that nothing in the driver
loop perturbs the world.

## 3. Completion turns from the order log: rule and cross-checks

**Order-log format.** `OrderLog.Save` writes a BinaryWriter stream, little-endian: the 8-byte magic `CIVORDR\0`,
an int32 version (1) and an int32 record count, then 28 bytes per record: int64 Turn, int32 ActorId, int32 Kind,
int32 TargetId and int64 Amount (IEEE-754 bits). No CLI subcommand prints a log. `run`, `replay` and `research`
consume one with `--orders`, and `inspect` reads one only through a session manifest. So the logs were decoded
offline (`read_orders` in `baseline_analysis.py`), with no code change. The driver logs hold 235–239 records, all
`SetResearchTarget` (kind 6) by actor 1 [M].

**Rule.** Let order *i* be stamped turn `s_i` and target node `k_i`.

- `target_turn(k_i) = s_i`. The driver stamps the pre-step clock. By the `OrderKind.SetResearchTarget` delivery
  contract, an order stamped *t* retargets the step *t → t+1*, and that step's RP already goes to the new target.
- `completion_turn(k_i) = s_{i+1}`.
- The last order's node is complete only if the final report no longer shows it as the active target. In all four
  runs it is still the active target, so it is not counted.
- `turns as target = completion_turn − target_turn`, the number of steps whose RP funded the node.

**Why this rule is exact for the driver.** Each premise is read from the code:

- (P1) The driver appends an order only when `TryGetTarget` is false (`ResearchCli.cs`).
- (P2) A target is cleared only by completion. `ResearchSystem` keeps the PREV target while it is AVAILABLE. AVAILABLE
  is a monotone predicate over the completed set (AND/OR prerequisites, the AND/OR stage expression, recursive-node
  exhaustion), and that set only grows. The driver never sends −1.
- (P3) Only the target can complete. Eureka and foreign-exposure credit is capped at 0.40 × BaseCost per node,
  while EffectiveCost = BaseCost for polity 1: every report prints "specialized-university cost modifiers: none",
  and the driver issues no construction order. RP goes only to the target.
- (P4) So at most one node completes per step, and the next order is delivered in the very next step. No step is
  left without a target.

**Cross-checks. All passed, for all four seeds.**

| check | result |
|---|---|
| a. completed count per view at turn 1630 (7 views × 4 seeds) vs the CLI's view table | 28/28 equal |
| b. CLI milestones "50/100/150/200 nodes complete by turn X" vs the turn of the 50th/100th/150th/200th reconstructed completion | 16/16 equal |
| c. CLI "research stage reached at turn X" vs the first reconstructed turn at which `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman` holds | 4/4 equal (996, 998, 1016, 1010) |
| d. per-node RP bounds. RP(state t) = 0.08 × P_t^0.699 from the traced population, with RP(state 0) from the turn-0 population. For a node with no evaluable Eureka (232/232/228/229 nodes): Σ RP over its target steps ≥ BaseCost > Σ RP before its last step. For the 6 Eureka-bearing completions: ≥ 0.6 BaseCost, and the strict upper bound | 0 violations |
| e. RP formula vs the trace's printed RP (3 decimals), every turn | 4 × 1630 rows equal after rounding |
| f. final active target: reported progress vs Σ RP since it was targeted | exact to 0.001 RP in 4/4 (e.g. seed 42 `woodblock_print` 7911.769 vs 7911.769) |
| g. 56 direct prefix runs (T12): node state at c−1 / c, per-view counts, active target, active-target progress | 56/56 consistent |
| h. `sim run --founded --orders <driver log>` final hash vs the research run's world hash | 4/4 equal (`raw/fidelity/hash-comparison.txt`) |
| i. Eureka identity: the six Eureka-bearing completed nodes need credit to close their RP gap; 0.4 × BaseCost over those six plus the two credited, never-targeted nodes = 54,116 | equals the reported total exactly |

Check d pins each completion to the unique step at which cumulative RP crosses the cost. It also rules out idle
steps: had a node completed earlier, RP would already have exceeded its cost before its last step.

Check h also answers a fidelity question. Since the M5 H4 hardening, `sim run` drives any AI polity that a revolt
founds, while `sim research` does not. Equal hashes mean that for these seeds and this length, the driver's world
is exactly the game's world for the same player orders.

## 4. Driver results

### 4.1 Time base, population and RP

#### T1 time base

| turns | dtYears | turns in band | sim-year at band end |
|---|---|---|---|
| 1–250 | 10 | 250 | 2500 |
| 251–650 | 5 | 400 | 4500 |
| 651–950 | 3 | 300 | 5400 |
| 951–1150 | 2 | 200 | 5800 |
| 1151–1270 | 1 | 120 | 5920 |
| 1271–1630 | 0.5 | 360 | 6100 |

The dt-years per turn and the sim-year progression are identical in every seed [M]. The last two era bands (Modern
and Information+) share dt 0.5.

#### T2 population, RP/turn, completed nodes at selected turns

| turn | sim-year | pop s1 | pop s2 | pop s42 | pop s7 | RP s1 | RP s2 | RP s42 | RP s7 | done s1 | done s2 | done s42 | done s7 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 10 | 5,442 | 5,382 | 5,118 | 5,137 | 32.7 | 32.4 | 31.3 | 31.4 | 0 | 0 | 0 | 0 |
| 100 | 1000 | 11,538 | 11,391 | 10,818 | 11,024 | 55.3 | 54.8 | 52.8 | 53.5 | 11 | 11 | 11 | 11 |
| 200 | 2000 | 24,706 | 24,352 | 23,163 | 23,550 | 94.1 | 93.2 | 90.0 | 91.0 | 22 | 22 | 22 | 22 |
| 250 | 2500 | 36,141 | 35,647 | 33,898 | 34,461 | 122.8 | 121.6 | 117.4 | 118.8 | 31 | 31 | 30 | 31 |
| 300 | 2750 | 43,674 | 43,094 | 40,995 | 41,678 | 140.2 | 138.8 | 134.1 | 135.6 | 45 | 44 | 43 | 44 |
| 500 | 3750 | 93,413 | 92,182 | 87,592 | 89,095 | 238.5 | 236.3 | 228.0 | 230.7 | 72 | 72 | 71 | 72 |
| 650 | 4500 | 164,885 | 163,115 | 154,969 | 157,540 | 354.7 | 352.1 | 339.7 | 343.6 | 101 | 101 | 99 | 99 |
| 800 | 4950 | 231,836 | 229,575 | 218,052 | 221,421 | 450.1 | 447.1 | 431.3 | 435.9 | 124 | 124 | 121 | 122 |
| 950 | 5400 | 325,908 | 323,240 | 307,019 | 311,600 | 571.1 | 567.9 | 547.8 | 553.5 | 147 | 146 | 143 | 143 |
| 1000 | 5500 | 351,602 | 348,793 | 331,169 | 336,236 | 602.3 | 598.9 | 577.6 | 583.7 | 154 | 153 | 151 | 152 |
| 1150 | 5800 | 441,220 | 438,035 | 415,721 | 421,979 | 705.8 | 702.3 | 677.1 | 684.2 | 177 | 177 | 175 | 176 |
| 1200 | 5850 | 458,317 | 454,736 | 431,835 | 437,949 | 724.8 | 720.9 | 695.3 | 702.2 | 187 | 186 | 182 | 183 |
| 1270 | 5920 | 482,932 | 479,103 | 455,199 | 461,447 | 751.8 | 747.7 | 721.4 | 728.3 | 198 | 198 | 192 | 194 |
| 1400 | 5985 | 507,203 | 502,854 | 478,063 | 484,558 | 778.1 | 773.4 | 746.5 | 753.6 | 212 | 210 | 209 | 209 |
| 1430 | 6000 | 512,967 | 508,486 | 483,144 | 490,034 | 784.2 | 779.4 | 752.1 | 759.6 | 216 | 215 | 211 | 213 |
| 1630 | 6100 | 553,245 | 548,460 | 520,530 | 527,651 | 826.8 | 821.8 | 792.3 | 799.9 | 238 | 238 | 234 | 235 |

In T2, population and RP are MEASURED (trace). The "done" columns are INFERENCE: the number of nodes completed by
that turn, from the reconstruction.

#### T3 trajectory statistics and RP totals

| metric | label | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|
| population at turn 0 (session trace row 0) | MEASURED | 5,410 | 5,326 | 5,108 | 5,095 |
| population min / median / max (turns 1–1630) | MEASURED | 5,442 / 240,188 / 553,245 | 5,382 / 237,793 / 548,460 | 5,118 / 225,910 / 520,530 | 5,137 / 229,332 / 527,651 |
| RP/turn min / median / max | MEASURED | 32.7 / 461.4 / 826.8 | 32.4 / 458.2 / 821.8 | 31.3 / 442.1 / 792.3 | 31.4 / 446.7 / 799.9 |
| cumulative RP, prior-report convention (Σ trace RP, turns 1–1630) | MEASURED (sum of printed values) | 739,798 | 735,088 | 709,153 | 716,511 |
| RP generated in the run's 1630 steps (RP of states 0–1629) | INFERENCE | 739,003 | 734,298 | 708,392 | 715,742 |
| RP applied to research (progress, excl. Eureka credit) | INFERENCE | 689,268 | 686,746 | 661,402 | 669,159 |
| RP lost as overflow past completion | INFERENCE | 49,735 (6.7%) | 47,553 (6.5%) | 46,990 (6.6%) | 46,583 (6.5%) |
| Eurekas fired / Eureka credit RP | MEASURED | 8 / 54,116 | 8 / 54,116 | 8 / 54,116 | 8 / 54,116 |
| SetResearchTarget orders issued | MEASURED | 239 | 239 | 235 | 236 |
| nodes completed by turn 1630 | MEASURED (view table sum) | 238 | 238 | 234 | 235 |
| final active target (progress / cost) | MEASURED | coke (10728.123 / 14,070) | coke (8205.673 / 14,070) | woodblock_print (7911.769 / 14,070) | movable_type_ceramic (1599.216 / 3,520) |
| available nodes at turn 1630 | MEASURED | 11 | 11 | 12 | 13 |
| world hash at turn 1630 | MEASURED | a11ab9b9884c65b3… | 3b30a5fa318b6374… | 5241e99ec21d9312… | 4e670239eec6e3dd… |

RP accounting [I]:

- RP generated = RP(state 0) + Σ_{t=1}^{1629} RP(state t), the RP spent in the run's 1630 steps.
- RP applied = Σ BaseCost(completed) + Σ partial progress at turn 1630 − Eureka credit.
- Overflow = generated − applied.

The prior report's convention, Σ_{t=1}^{1630} of the printed RP, is kept beside it for comparability.

### 4.2 What was completed

#### T4 completed nodes by view (MEASURED, CLI view table at turn 1630)

| view | total nodes | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|
| Main Technology | 177 | 174 | 174 | 174 | 174 |
| 1.1 Military | 37 | 11 | 11 | 11 | 11 |
| 1.2 Medicine | 22 | 2 | 2 | 2 | 2 |
| 1.3 Engineering | 131 | 29 | 29 | 25 | 26 |
| 1.4 Natural Science | 44 | 10 | 10 | 10 | 10 |
| 1.5 Agriculture | 15 | 6 | 6 | 6 | 6 |
| Civics | 7 | 6 | 6 | 6 | 6 |
| **all** | **433** | **238** | **238** | **234** | **235** |

#### T5 completed nodes by Age label (INFERENCE from the reconstruction; totals from research.json)

| Age | nodes in content | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|
| A1 | 28 | 28 | 28 | 28 | 28 |
| A2 | 51 | 51 | 51 | 51 | 51 |
| A3 | 38 | 38 | 38 | 38 | 38 |
| A4 | 32 | 32 | 32 | 32 | 32 |
| A5 | 32 | 29 | 29 | 29 | 29 |
| A6 | 42 | 40 | 40 | 36 | 37 |
| A7 | 32 | 11 | 11 | 11 | 11 |
| A8 | 53 | 9 | 9 | 9 | 9 |
| A9 | 125 | 0 | 0 | 0 | 0 |

The three Main-tree nodes never completed are all A5: `cast_iron` (16,730 RP, AVAILABLE at the end), plus
`iron_mouldboard` and `seed_drill`, which both require it [M final report; I content].

### 4.3 Key turns

#### T6 key turns — turn (sim-year)

| event | label | seed 1 | seed 2 | seed 42 | seed 7 | spread (turns) |
|---|---|---|---|---|---|---|
| research stage reached | MEASURED (CLI milestone) | 996 (5492) | 998 (5496) | 1016 (5532) | 1010 (5520) | 20 |
| 50 nodes complete | MEASURED (CLI milestone) | 326 (2880) | 328 (2890) | 334 (2920) | 333 (2915) | 8 |
| 100 nodes complete | MEASURED (CLI milestone) | 641 (4455) | 643 (4465) | 653 (4509) | 651 (4503) | 12 |
| 150 nodes complete | MEASURED (CLI milestone) | 967 (5434) | 969 (5438) | 986 (5472) | 981 (5462) | 19 |
| 200 nodes complete | MEASURED (CLI milestone) | 1281 (5925) | 1284 (5927) | 1309 (5939) | 1301 (5935) | 28 |
| civic 1001 law_code completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 619 (4345) | 621 (4355) | 631 (4405) | 629 (4395) | 12 |
| civic 1002 legal_code_roman completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 671 (4563) | 673 (4569) | 685 (4605) | 683 (4599) | 14 |
| civic 1003 census completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 886 (5208) | 888 (5214) | 905 (5265) | 900 (5250) | 19 |
| civic 1004 coined_wage completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 1400 (5985) | 1403 (5986) | 1432 (6001) | 1423 (5996) | 32 |
| civic 1005 patent completes | MEASURED: never — LOCKED at turn 1630 (`--node patent`: printing_press missing) | — | — | — | — | n/a |
| civic 1006 joint_stock completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 1409 (5989) | 1412 (5991) | 1441 (6005) | 1432 (6001) | 32 |
| civic 1007 taxation completes | MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction) | 478 (3640) | 480 (3650) | 489 (3695) | 487 (3685) | 11 |
| research_count technology >= 200 (a8_knowledge) | MEASURED (prefix runs: technology count 199 → 200, T12) | 1331 (5950) | 1334 (5952) | 1359 (5964) | 1351 (5960) | 28 |
| research_count technology >= 280 (a9_knowledge) | not reached: technology count at turn 1630 = 232/232/228/229 (MEASURED) | — | — | — | — | n/a |

#### T12 direct prefix-run observations (MEASURED: `research --seed S --turns N --auto cheapest --node ID`)

| event | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|
| technology count (Tree 1, all views) | 1330: 199 → 1331: 200 | 1333: 199 → 1334: 200 | 1358: 199 → 1359: 200 | 1350: 199 → 1351: 200 |
| coined_wage (state, progress RP) | 1399: AVAILABLE 777.6 → 1400: COMPLETED | 1402: AVAILABLE 773.6 → 1403: COMPLETED | 1431: AVAILABLE 752.1 → 1432: COMPLETED | 1422: AVAILABLE 757.8 → 1423: COMPLETED |
| joint_stock (state, progress RP) | 1408: AVAILABLE 2,337.9 → 1409: COMPLETED | 1411: AVAILABLE 2,325.8 → 1412: COMPLETED | 1440: AVAILABLE 2,261.0 → 1441: COMPLETED | 1431: AVAILABLE 2,278.1 → 1432: COMPLETED |
| taxation (state, progress RP) | 477: AVAILABLE 891.3 → 478: COMPLETED | 479: AVAILABLE 887.8 → 480: COMPLETED | 488: AVAILABLE 877.3 → 489: COMPLETED | 486: AVAILABLE 883.2 → 487: COMPLETED |
| law_code (state, progress RP) | 618: AVAILABLE 973.8 → 619: COMPLETED | 620: AVAILABLE 970.0 → 621: COMPLETED | 630: AVAILABLE 961.2 → 631: COMPLETED | 628: AVAILABLE 967.1 → 629: COMPLETED |
| legal_code_roman (state, progress RP) | 670: AVAILABLE 2,185.2 → 671: COMPLETED | 672: AVAILABLE 2,175.2 → 673: COMPLETED | 684: AVAILABLE 2,139.8 → 685: COMPLETED | 682: AVAILABLE 2,157.0 → 683: COMPLETED |
| census (state, progress RP) | 885: AVAILABLE 4,598.9 → 886: COMPLETED | 887: AVAILABLE 4,585.4 → 888: COMPLETED | 904: AVAILABLE 4,544.9 → 905: COMPLETED | 899: AVAILABLE 4,556.7 → 900: COMPLETED |

All 56 prefix runs agree with the reconstruction (per-view counts, active target, node state, active-target progress): True.

### 4.4 Age research milestone facts

The driver never issues `AdvanceAge`, so polity 1 stays in Age I for the whole game. The table below therefore
shows research-fact *eligibility*, not Age transitions.

A research fact holds when ANY listed node is complete (ages.json loader: "a research fact holds when ANY listed
node is complete"). `research_count` counts completed Technology-tree nodes. Age entry also needs non-research facts
(population, structures, classes, trade, formations, settlements) and a supporting count. Those are not evaluated
here.

#### T7 Age research milestone facts — first turn the fact holds for the driver (INFERENCE), turn (sim-year)

| Age | role | milestone | nodes (ANY) | seed 1 | seed 2 | seed 42 | seed 7 | satisfying node |
|---|---|---|---|---|---|---|---|---|
| A2 | CORE | a2_cultivation | cereal_cultivation | 517 (3835) | 519 (3845) | 529 (3895) | 527 (3885) | cereal_cultivation |
| A2 | SUPPORTING | a2_pottery | pottery_open_fired | 450 (3500) | 452 (3510) | 461 (3555) | 459 (3545) | pottery_open_fired |
| A2 | SUPPORTING | a2_herding | sheep_goat | 132 (1320) | 132 (1320) | 136 (1360) | 135 (1350) | sheep_goat |
| A3 | CORE | a3_bronze | arsenical_bronze, tin_bronze | 785 (4905) | 787 (4911) | 802 (4956) | 798 (4944) | arsenical_bronze |
| A3 | SUPPORTING | a3_writing | proto_writing, token_counting | 225 (2250) | 226 (2260) | 230 (2300) | 229 (2290) | token_counting |
| A3 | SUPPORTING | a3_law | law_code | 619 (4345) | 621 (4355) | 631 (4405) | 629 (4395) | law_code |
| A3 | SUPPORTING | a3_wheel | wheel_solid | 582 (4160) | 584 (4170) | 594 (4220) | 592 (4210) | wheel_solid |
| A4 | CORE | a4_iron | iron_bloomery | 1174 (5824) | 1176 (5826) | 1198 (5848) | 1191 (5841) | iron_bloomery |
| A4 | SUPPORTING | a4_alphabet | abjad | 720 (4710) | 722 (4716) | 735 (4755) | 733 (4749) | abjad |
| A4 | SUPPORTING | a4_census | census | 886 (5208) | 888 (5214) | 905 (5265) | 900 (5250) | census |
| A5 | CORE | a5_systematic_law | legal_code_roman | 671 (4563) | 673 (4569) | 685 (4605) | 683 (4599) | legal_code_roman |
| A5 | CORE | a5_coinage | coinage_electrum | 1398 (5984) | 1401 (5985) | 1430 (6000) | 1421 (5995) | coinage_electrum |
| A5 | SUPPORTING | a5_geometry | geometry_axiomatic | 996 (5492) | 998 (5496) | 1016 (5532) | 1010 (5520) | geometry_axiomatic |
| A5 | SUPPORTING | a5_roads | road_paved | 1331 (5950) | 1334 (5952) | 1359 (5964) | 1351 (5960) | road_paved |
| A5 | SUPPORTING | a5_concrete | concrete_pozzolan | 1519 (6044) | 1522 (6046) | 1554 (6062) | 1545 (6057) | concrete_pozzolan |
| A5 | SUPPORTING | a5_wages | coined_wage | 1400 (5985) | 1403 (5986) | 1432 (6001) | 1423 (5996) | coined_wage |
| A6 | CORE | a6_heavy_plough | heavy_plough | — | — | — | — | — |
| A6 | SUPPORTING | a6_paper | paper | 1537 (6053) | 1540 (6055) | 1573 (6071) | 1564 (6067) | paper |
| A6 | SUPPORTING | a6_mills | water_mill, windmill_post | 1425 (5997) | 1428 (5999) | 1457 (6013) | 1448 (6009) | water_mill |
| A6 | SUPPORTING | a6_finance | double_entry, bill_of_exchange | 1405 (5987) | 1408 (5989) | 1437 (6003) | 1428 (5999) | bill_of_exchange |
| A6 | SUPPORTING | a6_numerals | hindu_arabic | 913 (5289) | 915 (5295) | 932 (5346) | 927 (5331) | hindu_arabic |
| A7 | CORE | a7_printing | printing_press | — | — | — | — | — |
| A7 | SUPPORTING | a7_ocean | caravel | — | — | — | — | — |
| A7 | SUPPORTING | a7_powder | corned_powder | 1166 (5816) | 1168 (5818) | 1190 (5840) | 1183 (5833) | corned_powder |
| A7 | SUPPORTING | a7_science | mechanics_newtonian | — | — | — | — | — |
| A7 | SUPPORTING | a7_patent | patent | — | — | — | — | — |
| A7 | SUPPORTING | a7_company | joint_stock | 1409 (5989) | 1412 (5991) | 1441 (6005) | 1432 (6001) | joint_stock |
| A8 | CORE | a8_steam | steam_rotary | — | — | — | — | — |
| A8 | SUPPORTING | a8_iron | puddling | — | — | — | — | — |
| A8 | SUPPORTING | a8_rail | railway | — | — | — | — | — |
| A8 | SUPPORTING | a8_telegraph | telegraph | — | — | — | — | — |
| A8 | SUPPORTING | a8_germ | germ_theory | — | — | — | — | — |
| A8 | SUPPORTING | a8_knowledge | technology count >= 200 | 1331 (5950) | 1334 (5952) | 1359 (5964) | 1351 (5960) | — |
| A9 | CORE | a9_electricity | electricity_generation | — | — | — | — | — |
| A9 | CORE | a9_combustion | internal_combustion | — | — | — | — | — |
| A9 | SUPPORTING | a9_radio | radio | — | — | — | — | — |
| A9 | SUPPORTING | a9_antibiotics | antibiotic_penicillin | — | — | — | — | — |
| A9 | SUPPORTING | a9_computing | computer | — | — | — | — | — |
| A9 | SUPPORTING | a9_knowledge | technology count >= 280 | — | — | — | — | — |

### 4.5 Waits, idle turns, Eurekas, overflow

#### T8 longest single-target waits (turns as the active target; INFERENCE)

- seed 1: astrolabe 18 (14,070 RP, turns 1565–1583); woodblock_print 18 (14,070 RP, turns 1583–1601); coinage_electrum 16 (11,830 RP, turns 1382–1398); water_mill 16 (11,830 RP, turns 1409–1425); cereal_cultivation 15 (3,520 RP, turns 502–517); concrete_pozzolan 15 (11,830 RP, turns 1504–1519); road_paved 14 (9,950 RP, turns 1317–1331); pottery_open_fired 13 (2,490 RP, turns 437–450); hacksilver 13 (2,960 RP, turns 489–502); cereal_domesticated 13 (3,520 RP, turns 553–566)
- seed 2: astrolabe 18 (14,070 RP, turns 1568–1586); woodblock_print 18 (14,070 RP, turns 1586–1604); coinage_electrum 16 (11,830 RP, turns 1385–1401); water_mill 16 (11,830 RP, turns 1412–1428); cereal_cultivation 15 (3,520 RP, turns 504–519); concrete_pozzolan 15 (11,830 RP, turns 1507–1522); road_paved 14 (9,950 RP, turns 1320–1334); pottery_open_fired 13 (2,490 RP, turns 439–452); hacksilver 13 (2,960 RP, turns 491–504); cereal_domesticated 13 (3,520 RP, turns 555–568)
- seed 42: astrolabe 18 (14,070 RP, turns 1602–1620); coinage_electrum 16 (11,830 RP, turns 1414–1430); water_mill 16 (11,830 RP, turns 1441–1457); concrete_pozzolan 16 (11,830 RP, turns 1538–1554); cereal_cultivation 15 (3,520 RP, turns 514–529); road_paved 14 (9,950 RP, turns 1345–1359); telescope 14 (9,950 RP, turns 1386–1400); microscope 14 (9,950 RP, turns 1400–1414); knapping_oldowan 13 (520 RP, turns 54–67); pottery_open_fired 13 (2,490 RP, turns 448–461)
- seed 7: astrolabe 18 (14,070 RP, turns 1592–1610); woodblock_print 18 (14,070 RP, turns 1610–1628); coinage_electrum 16 (11,830 RP, turns 1405–1421); water_mill 16 (11,830 RP, turns 1432–1448); concrete_pozzolan 16 (11,830 RP, turns 1529–1545); cereal_cultivation 15 (3,520 RP, turns 512–527); road_paved 14 (9,950 RP, turns 1337–1351); telescope 14 (9,950 RP, turns 1377–1391); microscope 14 (9,950 RP, turns 1391–1405); knapping_oldowan 13 (520 RP, turns 54–67)

- **Idle turns** (no available node, or no target) [I]: **0** in every seed. The first order is stamped turn 0,
  each later order is stamped on the completion turn of the previous node (check d), and the last target is still
  active at turn 1630. The final reports list 11–13 available nodes [M].
- **Eurekas** [M count, I identity]: 8 fired in every seed, for 54,116 RP of credit [M]. They are `copper_smelting`,
  `tin_bronze`, `iron_bloomery`, `windmill_post`, `paper` and `vaccination` (all completed), plus `printing_press`
  (15,912) and `germ_theory` (18,924), which are credited 40 % but never targeted [M partial-progress rows]. Foreign
  exposure: 0 [M]. No source exists.
- **Overflow** is not reported by the CLI. Inferred at 46,583–49,735 RP per game, 6.5–6.7 % of RP generated [I, T3].
- **Longest waits** sit late in the game, on 11,830–14,070 RP nodes (16–18 turns each). Mid-game,
  `cereal_cultivation` takes 15 turns (3,520 RP at 241–244 RP/turn). The longest early wait is `knapping_oldowan`
  (520 RP, 12 turns in seeds 1 and 2 and 13 in seeds 42 and 7, at 41–45 RP/turn) [I].

## 5. Cross-seed comparison and sensitivity

- **Order is seed-independent; pace is not.** The driver's ranking key, EffectiveCost = BaseCost, is static
  content, and availability depends only on the completed set. The four target sequences are therefore identical:
  each shorter log is a prefix of seed 1's (239 orders) [I, decoded logs]. Every difference between seeds is a
  difference in pace.
- **Pace follows population.** The seed ranking is the same at every milestone, seed 1 < 2 < 7 < 42 in turns, and
  it matches the population ranking (T2) [M].
- **Spread** (max − min across the four seeds, T6) grows with time. It is 8 turns at 50 nodes, 12 at 100, 19 at
  150 and 28 at 200 [M]. The stage spread is 20 turns, 40 sim-years [M]; the civic spread is 11–32 turns [M]; the
  final count spread is 4 nodes (234–238) [M]. In relative terms the spread stays at 1.8–2.4 % of the turn
  count [I].

## 6. The AI polity (`sim run --founded --ai-empires 1`)

### 6.1 Can the existing CLI expose the AI's research progression without code changes?

**Partially.** No CLI output reports an AI polity's research state, but the decisions the AI takes are recorded,
and the completions can be reconstructed from them with the same rule.

| route | what it gives for research | evidence |
|---|---|---|
| `sim research` | nothing for an AI. It has no `--ai-empires` option (`error: unknown option '--ai-empires'`), and it founds the default world with one Empire: `--polity 2` → `error: --polity 2 is not a registered Empire in this world`, and an AI run's log is refused ("SetResearchTarget is issued by polity 2, which is not a registered Empire in this world (1 registered)") | `raw/ai/research-cli-negative-tests.txt` |
| `sim run --report` | nothing (it prints the M0 toy-good stock lines) | same file |
| `sim run --emit-session` → order log | the AI's research **decisions**: every `SetResearchTarget` with its stamped turn, every `AdvanceAge` | `raw/ai/ai-run-orders-seedS.bin` |
| → session trace | world totals per turn (population, settlements, food, hash); no research | `raw/ai/ai-run-session-trace-seedS.csv` |
| → telemetry | per settlement per turn: controller (the research holder's rule, `EmpireQuery.TryGetController` on the post-step state) and population; no research table | reduced to `raw/ai/ai-polity-population-seedS.csv` |
| `sim replay --report-jsonl`, `sim inspect --answer` | no research fields or topics | `ReplayReport.cs`, `SessionInspector.cs` |

**Not exposed for the AI at all:** the completed set, stage and Eureka state, credits, university cost modifiers,
partial progress, the fate of the last target, and overflow.

**What can be done without code.** The AI's rule (`AiResearchPolicy.Decide`) orders a target only when it holds
none, keeps a live target, and never clears. That is the driver's premise P1 and P2, so the same reconstruction
applies. P3 still holds for the AI, but for a different reason: university cost modifiers do exist for it, yet
`InstitutionsSystem` writes one row per polity and type with factor ≥ 1 − 1/3, and each type serves one subtree.
EffectiveCost therefore stays ≥ ⅔ BaseCost, above the 0.40 credit ceiling, so credit alone still cannot complete a
node.

### 6.2 Validation of the AI reconstruction

- **RP bounds** [I]. RP is computed per step from the telemetry population of the settlements polity 2 controls.
  RP(state 0) is approximated from the turn-1 *opening* populations under turn-1 control. The checks:
  - Main-tree and Civics nodes with no evaluable Eureka (no modifier can apply): the exact bound of check d. That
    is 180 / 180 / 180 / 178 nodes for seeds 1 / 2 / 42 / 7.
  - All other completions: the weak bound. Prior < BaseCost, and offered + (0.4 × BaseCost if the node carries
    an evaluable Eureka) ≥ the lowest EffectiveCost possible (BaseCost on Main/Civics, ⅔ BaseCost in a subtree).
    That is 160 / 158 / 164 / 85 nodes.
  - **0 violations.** Even the first node, which uses the approximated RP(state 0), passes.
- **AdvanceAge consistency** [M orders, I comparison]. This check is independent of the reconstruction. The AI
  advances on the first turn it is eligible (`AgeAdvancePolicy`), and eligibility needs every CORE milestone met.
  In all 28 AdvanceAge orders (7 per seed), every CORE research fact of the new Age is met, by the reconstruction,
  on or before the order turn. In 19 of 28 the order turn **equals** a reconstructed research completion turn: the
  research was the last missing requirement, and the AI acted on the very turn the reconstruction says it
  completed.
- **The last order's node** (seed 1 `xray` at 1629, seed 2 `superalloy` at 1624, seed 42 `plastics` at 1619,
  seed 7 `spinning_jenny` at 1628 [M]) has unknown status. It is not counted.

### 6.3 Results

#### T9 AI polity (sim run --ai-empires 1) — key results

| metric | label | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|
| orders in the run log (all kinds) / SetResearchTarget | MEASURED | 573 / 341 | 688 / 339 | 643 / 345 | 759 / 264 |
| nodes completed by turn 1630 (last target's status unknown) | INFERENCE | 340 | 338 | 344 | 263 |
| of which technology / civics | INFERENCE | 333 / 7 | 331 / 7 | 337 / 7 | 256 / 7 |
| last order (target turn) | MEASURED | xray (1629) | superalloy (1624) | plastics (1619) | spinning_jenny (1628) |
| research stage reached | INFERENCE | 702 (4656) | 716 (4698) | 710 (4680) | 705 (4665) |
| AI population turn 1 / median / turn 1630 | MEASURED (telemetry) | 2,664 / 311,160 / 8,141,111 | 2,633 / 271,618 / 7,720,582 | 2,917 / 305,352 / 10,293,255 | 2,326 / 158,479 / 3,476,632 |
| AI settlements turn 1 → turn 1630 | MEASURED (telemetry) | 6 → 21 | 6 → 21 | 6 → 21 | 6 → 21 |
| AI RP/turn min / median / max | INFERENCE (formula on measured population) | 19.7 / 552.9 / 5,415.7 | 19.7 / 502.8 / 5,218.6 | 20.2 / 545.7 / 6,380.6 | 18.0 / 345.0 / 2,987.9 |
| AI RP generated in the run | INFERENCE | 2,803,367 | 2,692,309 | 3,141,274 | 1,428,123 |
| civic law_code completes | INFERENCE | 608 (4290) | 622 (4360) | 618 (4340) | 613 (4315) |
| civic legal_code_roman completes | INFERENCE | 635 (4425) | 649 (4495) | 644 (4470) | 639 (4445) |
| civic census completes | INFERENCE | 1374 (5972) | 1386 (5978) | 1358 (5964) | 1624 (6097) |
| civic coined_wage completes | INFERENCE | 1233 (5883) | 1245 (5895) | 1225 (5875) | 1422 (5996) |
| civic patent completes | INFERENCE | 1350 (5960) | 1362 (5966) | 1339 (5954) | 1598 (6084) |
| civic joint_stock completes | INFERENCE | 1287 (5928) | 1299 (5934) | 1279 (5924) | 1495 (6032) |
| civic taxation completes | INFERENCE | 328 (2890) | 339 (2945) | 336 (2930) | 335 (2925) |
| AdvanceAge → Age 2 (order turn; Age holds from the next turn) | MEASURED (order log) | 218 (2180) | 223 (2230) | 221 (2210) | 220 (2200) |
| AdvanceAge → Age 3 (order turn; Age holds from the next turn) | MEASURED (order log) | 412 (3310) | 425 (3375) | 421 (3355) | 420 (3350) |
| AdvanceAge → Age 4 (order turn; Age holds from the next turn) | MEASURED (order log) | 827 (5031) | 842 (5076) | 833 (5049) | 868 (5154) |
| AdvanceAge → Age 5 (order turn; Age holds from the next turn) | MEASURED (order log) | 1233 (5883) | 1245 (5895) | 1225 (5875) | 1422 (5996) |
| AdvanceAge → Age 6 (order turn; Age holds from the next turn) | MEASURED (order log) | 1237 (5887) | 1249 (5899) | 1229 (5879) | 1427 (5998) |
| AdvanceAge → Age 7 (order turn; Age holds from the next turn) | MEASURED (order log) | 1287 (5928) | 1299 (5934) | 1279 (5924) | 1496 (6033) |
| AdvanceAge → Age 8 (order turn; Age holds from the next turn) | MEASURED (order log) | 1288 (5929) | 1300 (5935) | 1280 (5925) | 1497 (6033) |

#### T13 AI polity at selected turns — population and settlements (MEASURED, telemetry), RP/turn (INFERENCE)

| turn | pop s1 | settl. s1 | RP s1 | pop s2 | settl. s2 | RP s2 | pop s42 | settl. s42 | RP s42 | pop s7 | settl. s7 | RP s7 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2,664 | 6 | 19.8 | 2,633 | 6 | 19.7 | 2,917 | 6 | 21.1 | 2,326 | 6 | 18.0 |
| 100 | 6,314 | 8 | 36.3 | 5,838 | 8 | 34.3 | 6,153 | 8 | 35.6 | 6,163 | 8 | 35.7 |
| 200 | 15,224 | 11 | 67.1 | 13,630 | 11 | 62.1 | 14,031 | 11 | 63.4 | 14,080 | 11 | 63.5 |
| 250 | 22,913 | 12 | 89.3 | 20,322 | 12 | 82.1 | 20,946 | 12 | 83.9 | 21,049 | 12 | 84.1 |
| 300 | 27,550 | 13 | 101.6 | 25,153 | 13 | 95.3 | 25,995 | 13 | 97.5 | 25,939 | 13 | 97.4 |
| 500 | 59,580 | 16 | 174.1 | 56,550 | 16 | 167.9 | 57,584 | 16 | 170.0 | 58,756 | 16 | 172.4 |
| 650 | 106,531 | 18 | 261.4 | 101,260 | 18 | 252.3 | 105,985 | 18 | 260.5 | 106,800 | 18 | 261.9 |
| 800 | 270,681 | 19 | 501.6 | 235,810 | 19 | 455.5 | 263,043 | 19 | 491.7 | 153,054 | 19 | 336.7 |
| 950 | 1,068,520 | 20 | 1,309.8 | 1,010,637 | 20 | 1,259.8 | 1,096,961 | 20 | 1,334.1 | 327,205 | 20 | 572.7 |
| 1000 | 1,505,132 | 20 | 1,664.2 | 1,365,574 | 20 | 1,554.8 | 1,573,551 | 20 | 1,716.8 | 452,583 | 20 | 718.5 |
| 1150 | 3,721,708 | 21 | 3,133.6 | 3,591,712 | 21 | 3,056.7 | 4,243,279 | 21 | 3,434.4 | 1,237,799 | 21 | 1,451.6 |
| 1200 | 4,317,679 | 21 | 3,476.4 | 4,150,682 | 21 | 3,381.9 | 5,022,386 | 21 | 3,863.9 | 1,434,054 | 21 | 1,608.9 |
| 1270 | 5,269,386 | 21 | 3,995.8 | 5,190,690 | 21 | 3,954.0 | 6,347,325 | 21 | 4,550.9 | 1,820,851 | 21 | 1,901.2 |
| 1400 | 6,302,223 | 21 | 4,528.3 | 5,935,628 | 21 | 4,342.5 | 7,740,291 | 21 | 5,227.9 | 2,271,257 | 21 | 2,218.8 |
| 1430 | 6,589,046 | 21 | 4,671.4 | 6,133,941 | 21 | 4,443.4 | 8,031,083 | 21 | 5,364.5 | 2,393,116 | 21 | 2,301.4 |
| 1630 | 8,141,111 | 21 | 5,415.7 | 7,720,582 | 21 | 5,218.6 | 10,293,255 | 21 | 6,380.6 | 3,476,632 | 21 | 2,987.9 |

#### T10 AI vs driver, same seed — completed nodes at selected turns (AI: INFERENCE; driver: INFERENCE verified)

| turn | driver s1 | AI s1 | driver s2 | AI s2 | driver s42 | AI s42 | driver s7 | AI s7 |
|---|---|---|---|---|---|---|---|---|
| 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 100 | 11 | 4 | 11 | 4 | 11 | 4 | 11 | 4 |
| 200 | 22 | 8 | 22 | 8 | 22 | 8 | 22 | 8 |
| 250 | 31 | 14 | 31 | 13 | 30 | 14 | 31 | 14 |
| 300 | 45 | 17 | 44 | 17 | 43 | 17 | 44 | 17 |
| 500 | 72 | 33 | 72 | 31 | 71 | 31 | 72 | 32 |
| 650 | 101 | 43 | 101 | 42 | 99 | 42 | 99 | 42 |
| 800 | 124 | 63 | 124 | 61 | 121 | 62 | 122 | 60 |
| 950 | 147 | 84 | 146 | 83 | 143 | 84 | 143 | 74 |
| 1000 | 154 | 92 | 153 | 90 | 151 | 92 | 152 | 79 |
| 1150 | 177 | 105 | 177 | 103 | 175 | 105 | 176 | 94 |
| 1200 | 187 | 120 | 186 | 108 | 182 | 128 | 183 | 99 |
| 1270 | 198 | 189 | 198 | 177 | 192 | 197 | 194 | 101 |
| 1400 | 212 | 275 | 210 | 269 | 209 | 284 | 209 | 131 |
| 1430 | 216 | 291 | 215 | 284 | 211 | 297 | 213 | 159 |
| 1630 | 238 | 340 | 238 | 338 | 234 | 344 | 235 | 263 |

#### T14 AI polity — completed nodes by Age label (INFERENCE), beside the driver (INFERENCE, verified)

| Age | nodes in content | AI s1 | driver s1 | AI s2 | driver s2 | AI s42 | driver s42 | AI s7 | driver s7 |
|---|---|---|---|---|---|---|---|---|---|
| A1 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 |
| A2 | 51 | 51 | 51 | 51 | 51 | 51 | 51 | 51 | 51 |
| A3 | 38 | 38 | 38 | 38 | 38 | 38 | 38 | 36 | 38 |
| A4 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 |
| A5 | 32 | 32 | 29 | 32 | 29 | 32 | 29 | 32 | 29 |
| A6 | 42 | 42 | 40 | 42 | 40 | 42 | 36 | 37 | 37 |
| A7 | 32 | 32 | 11 | 32 | 11 | 32 | 11 | 22 | 11 |
| A8 | 53 | 53 | 9 | 53 | 9 | 53 | 9 | 23 | 9 |
| A9 | 125 | 32 | 0 | 30 | 0 | 36 | 0 | 2 | 0 |

Seeds 1, 2 and 42 complete every A1–A8 node and 30–36 of A9's 125. Seed 7 completes 2 A9 nodes and leaves gaps in
A3, A6, A7 and A8 [I, T14].

#### T11 AI Age research facts — first turn the fact holds (INFERENCE), turn (sim-year)

| Age | role | milestone | seed 1 | seed 2 | seed 42 | seed 7 |
|---|---|---|---|---|---|---|
| A2 | CORE | a2_cultivation | 218 (2180) | 223 (2230) | 221 (2210) | 220 (2200) |
| A2 | SUPPORTING | a2_pottery | 309 (2795) | 319 (2845) | 317 (2835) | 316 (2830) |
| A2 | SUPPORTING | a2_herding | 730 (4740) | 744 (4782) | 737 (4761) | 743 (4779) |
| A3 | CORE | a3_bronze | 412 (3310) | 425 (3375) | 421 (3355) | 420 (3350) |
| A3 | SUPPORTING | a3_writing | 265 (2575) | 273 (2615) | 271 (2605) | 270 (2600) |
| A3 | SUPPORTING | a3_law | 608 (4290) | 622 (4360) | 618 (4340) | 613 (4315) |
| A3 | SUPPORTING | a3_wheel | 791 (4923) | 806 (4968) | 798 (4944) | 818 (5004) |
| A4 | CORE | a4_iron | 481 (3655) | 494 (3720) | 490 (3700) | 489 (3695) |
| A4 | SUPPORTING | a4_alphabet | 649 (4495) | 663 (4539) | 658 (4524) | 653 (4509) |
| A4 | SUPPORTING | a4_census | 1374 (5972) | 1386 (5978) | 1358 (5964) | 1624 (6097) |
| A5 | CORE | a5_systematic_law | 635 (4425) | 649 (4495) | 644 (4470) | 639 (4445) |
| A5 | CORE | a5_coinage | 860 (5130) | 875 (5175) | 865 (5145) | 922 (5316) |
| A5 | SUPPORTING | a5_geometry | 702 (4656) | 716 (4698) | 710 (4680) | 705 (4665) |
| A5 | SUPPORTING | a5_roads | 598 (4240) | 612 (4310) | 608 (4290) | 603 (4265) |
| A5 | SUPPORTING | a5_concrete | 953 (5406) | 967 (5434) | 954 (5408) | 1054 (5608) |
| A5 | SUPPORTING | a5_wages | 1233 (5883) | 1245 (5895) | 1225 (5875) | 1422 (5996) |
| A6 | CORE | a6_heavy_plough | 1237 (5887) | 1249 (5899) | 1229 (5879) | 1427 (5998) |
| A6 | SUPPORTING | a6_paper | 959 (5418) | 973 (5446) | 960 (5420) | 1062 (5624) |
| A6 | SUPPORTING | a6_mills | 922 (5316) | 936 (5358) | 925 (5325) | 1011 (5522) |
| A6 | SUPPORTING | a6_finance | 1262 (5912) | 1274 (5922) | 1254 (5904) | 1453 (6011) |
| A6 | SUPPORTING | a6_numerals | 676 (4578) | 690 (4620) | 684 (4602) | 679 (4587) |
| A7 | CORE | a7_printing | 1051 (5602) | 1065 (5630) | 1049 (5598) | 1181 (5831) |
| A7 | SUPPORTING | a7_ocean | 1417 (5993) | 1438 (6004) | 1410 (5990) | — |
| A7 | SUPPORTING | a7_powder | 1274 (5922) | 1286 (5928) | 1266 (5916) | 1475 (6022) |
| A7 | SUPPORTING | a7_science | 1426 (5998) | 1435 (6002) | 1401 (5985) | — |
| A7 | SUPPORTING | a7_patent | 1350 (5960) | 1362 (5966) | 1339 (5954) | 1598 (6084) |
| A7 | SUPPORTING | a7_company | 1287 (5928) | 1299 (5934) | 1279 (5924) | 1495 (6032) |
| A8 | CORE | a8_steam | 1124 (5748) | 1137 (5774) | 1119 (5738) | 1281 (5925) |
| A8 | SUPPORTING | a8_iron | 1128 (5756) | 1141 (5782) | 1123 (5746) | 1286 (5928) |
| A8 | SUPPORTING | a8_rail | 1507 (6038) | 1520 (6045) | 1485 (6027) | — |
| A8 | SUPPORTING | a8_telegraph | 1593 (6081) | 1606 (6088) | 1567 (6068) | — |
| A8 | SUPPORTING | a8_germ | 1565 (6067) | 1578 (6074) | 1535 (6052) | — |
| A8 | SUPPORTING | a8_knowledge | 1285 (5927) | 1297 (5933) | 1277 (5923) | 1491 (6030) |
| A9 | CORE | a9_electricity | 1319 (5944) | 1331 (5950) | 1308 (5939) | 1546 (6058) |
| A9 | CORE | a9_combustion | 1177 (5827) | 1189 (5839) | 1169 (5819) | 1360 (5965) |
| A9 | SUPPORTING | a9_radio | — | — | — | — |
| A9 | SUPPORTING | a9_antibiotics | — | — | — | — |
| A9 | SUPPORTING | a9_computing | — | — | — | — |
| A9 | SUPPORTING | a9_knowledge | 1420 (5995) | 1438 (6004) | 1407 (5988) | — |

Unlike the driver, the AI actually changes Age (the AdvanceAge rows of T9) [M]. It issued no AdvanceAge to Age IX
within 1630 turns [M]. Of Age IX's research facts, `radio`, `antibiotic_penicillin` and `computer` never complete
[I, T11].

### 6.4 AI versus the driver

- **Early on the AI is slower.** It starts with 6 of the 12 settlements: 2,326–2,917 people and 18–21 RP/turn at
  turn 1, against the driver's 5,118–5,442 people and 31–33 RP/turn [M; AI RP I]. Its goal-directed choices also
  buy fewer, more expensive nodes. At turn 650 it has 42–43 nodes against the driver's 99–101 [I].
- **It reaches what matters sooner.** The research stage comes 282–306 turns (798–855 sim-years) earlier, with
  46 nodes complete against 153 [I]. The A2 core comes at 218–223 against 517–529 [I]. `taxation` comes at 328–339
  against 478–489 [I/M].
- **Its population then explodes.** It goes from about 0.1 M at turn 650 to 3.5–10.3 M at turn 1630, across
  21 settlements [M, T13]. That is 6.6–19.8× the driver's final population [M], and it lifts the AI's RP over the
  game to 2.0–4.4× the driver's [I].
- **It overtakes the driver's completed count** from turn 1265 / 1280 / 1293 / 1573 (seeds 42 / 1 / 2 / 7) [I]. It
  ends at 263–344 nodes, including A9 nodes, and completes all 7 civics. `census` is the AI's latest civic
  (1358–1386; seed 7 1624), much later than the driver's (886–905). Census, like `coined_wage`, `patent` and
  `joint_stock`, lies outside every goal closure the AI policy can form (Age CORE milestones, road classes, taxation,
  universities: 119 nodes in all). The AI reaches it only through its cheapest-available fallback [I, content].
- **Its target sequence is identical across seeds for the first 153 targets** (seeds 1, 2, 42; 93 for seed 7),
  then diverges. The AI's goals depend on state (Age, known road class, taxation, universities), so unlike the
  driver its *order* is seed-dependent [I, decoded logs].
- **Seed 7's AI is the outlier.** It has the slowest population growth (3.5 M at turn 1630), 263 nodes, and Ages
  V–VIII entered 177–217 turns later than in the other seeds [M]. Seed sensitivity of the AI is therefore
  much larger than the driver's.

### 6.5 Smallest harness that would measure the AI directly (proposal; not built)

The reconstruction above is checked but indirect. Direct measurement needs code, and the smallest version that
stays outside production code paths would look like this:

- **Where it lives.** A stand-alone console tool, e.g. `tools/research-probe/`, with its own .csproj referencing
  Sim.Core, Sim.Data and Sim.Cli for the public `HeadlessFounding` / `CliRecipes` recipes. It would be in no
  solution build or CI leg, and referenced by nothing.
- **What it runs.** It founds the world exactly as `sim run --founded --ai-empires N`
  (`HeadlessFounding.Found(seed, aiEmpiresOverride: N)`), builds the executor from the same recipe, and runs the
  same per-turn loop as `Cli.Steps` (`AiOrders.Append` on the pre-step world, then `TurnExecutor.Step`).
- **What it records.** After each step, for every polity, it writes rows from read-only queries only:
  - `ResearchQuery.CompletedBetween(prev, next, p)` (completions, with turn);
  - `TryGetTarget` and `Progress`;
  - `ResearchPointPool(prev, …)` (the RP actually spent);
  - `IsStageReached` transitions;
  - new `ResearchEurekas` / `ResearchCredits` rows;
  - `CostModifiers` when they change;
  - `AgeQuery.Evaluate` (per-milestone status).
- **How it proves it changed nothing.** It prints the final world hash, which must equal `sim run`'s hash for the
  same seed, turns and `--ai-empires`.
- **Size and cost.** About 100–150 lines. It needs no schema change and no second implementation, because every
  number comes from the query functions the systems themselves call. It would cost about 2.5 min per 1630-turn
  seed, the AI run times measured here.

Adding `--ai-empires` and an AI-order step to `sim research` would also work. That is a production change to
Sim.Cli, though, and would need its own packet.

## 7. Limitations

1. **The driver is not a player or an AI.**
   - It picks the cheapest node by static BaseCost, issues no economic orders (construction, tax, roads,
     allocation) and never advances Age.
   - Its population is that of a polity whose only action is research, so its RP is a lower-activity baseline. The
     AI comparison (§6.4) shows how much economic play changes it.
   - Its research order is fixed by content.
2. **Age facts are research facts only.** Non-research entry requirements were not evaluated for the driver, and
   no Age transition occurs in its runs.
3. **AI completion turns are reconstructed, not printed.** The last target's fate is unknown. Eureka firing,
   credits and university discounts are unobserved for the AI. AI RP is the RP formula applied to telemetry
   population, with RP(state 0) approximated (about 20 RP of 1.4–3.1 M). The AI CSV records each row's basis.
4. **Fidelity checked only where measured.** The research-CLI world equals the `sim run` world for these four logs
   at 1630 turns (check h). It was not checked for other seeds or lengths.
5. **The telemetry was not kept.** It was about 300 MB per AI run. Its size and SHA-256 are in
   `raw/ai/ai-run-telemetry-not-kept.txt`, and the per-turn, per-controller population reduction that the analysis
   uses is kept. A rerun reproduces it (the runs are deterministic).
6. **Wall-clock times are not benchmarks.** They were measured with four processes on four vCPUs.
7. **No AI "Eurekas fired" or "overflow" figure exists.** Neither is observable from the CLI for an AI polity.

## 8. Files (all under `docs/m6-plan/knowledge-redesign/measurements/`)

| file | content |
|---|---|
| `baseline-progression.md` | this document |
| `baseline-node-completions.csv` | driver: one row per completed node per seed (seed, node_key, node_id, tree, branch, age, base_cost, target_turn, completion_turn, completion_sim_year). `branch` is `main`, a subtree id, or `civics` for Tree 2. Completion turns are INFERENCE (rule §3, verified); the civic and 200-technology rows are also MEASURED by T12 |
| `baseline-ai-node-completions.csv` | AI polity: same columns plus `completion_basis` (INFERENCE) |
| `baseline-summary.json` | per-seed metrics split into `MEASURED` / `INFERENCE` / `CHECKS` / `SPOT_CHECKS_MEASURED`, plus `ai_polity` |
| `baseline_analysis.py` | order-log decoder, reconstruction, checks; writes the two CSVs and the JSON |
| `baseline_tables.py` | renders every table in this document from the JSON |
| `ai_telemetry_reduce.py` | telemetry → per-turn, per-controller population (the AI RP input) |
| `raw/research-seedS.stdout.txt`, `.stderr.txt`, `.time.txt` | the four driver runs (stdout, stderr (empty), wall clock) |
| `raw/research-auto-orders-seedS.bin` | driver order logs (6.6–6.7 KB each) |
| `raw/population-trace-seedS.csv` | driver population / RP traces (1630 rows each) |
| `raw/turn0-session-trace-seedS.csv` | turn-0 population source |
| `raw/checks/` | 56 prefix-run reports (T12) |
| `raw/fidelity/` | `sim run --orders` stdout and wall clock, and `hash-comparison.txt` |
| `raw/ai/` | AI run order logs, session traces, manifests, forensic records, stdout and wall clock; per-controller population CSVs; `research-cli-negative-tests.txt`; `ai-run-telemetry-not-kept.txt` |
