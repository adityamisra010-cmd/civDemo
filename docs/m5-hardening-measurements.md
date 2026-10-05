# M5 HARDENING MEASUREMENTS (stream H4)

**Status:** APPEND-ONLY MEASUREMENT RECORD. Created 2026-10-05 for the Director's M5 hardening instruction of
2026-10-05 (§10, §11, §14, §16). Do not edit a dated section in place; append a new dated section below the
last one. A later stream re-measuring after the hardening merge appends its own section with its commit.

**Scope of this stream.** Colonies (§14), the ledger (§11), the calibration sweep and battery (§16 "20-seed
calibration sweep"), the AI soak (§16 "600-turn AI run", extended to 1000 turns and three rivals), and the
benchmark. The tax/revolt model, the UI, the preview tools and the stale-document cleanup belong to sibling
streams (H1–H3). This record measures their absence: everything here was run on the tree named below, which
does **not** contain their changes.

**Labels.** MEASURED = run by this stream on the commit named, 2026-10-05. CARRIED = an earlier record's number,
cited and not re-run. INFERRED = this stream's reading, neither measured nor ruled. RATIFIED / OPEN / DEFERRED
as in `docs/m5-playtest-baseline.md`.

---

## 2026-10-05 — measurements on `m5h-h4-measure` (base `9bb7423`)

### 0. Tree, machine and how to re-run

| item | value |
|---|---|
| Base commit | `9bb7423` (`m5i-playtest-baseline`) = `m5-integration` `bc87ef8` + the baseline record. `git diff --stat bc87ef8 9bb7423` touches documents only, so every simulation number below is the played build's. |
| This stream's code changes | `9bcad5b` (order validation, §4.4) and `619b109` (`sim run`, §4.4). Neither moves a world hash: the `ci.yml` founded legs were re-run on the fixed CLI — `FOUNDED_GOLDEN` `02c7f9eb…` reproduced by two processes, the ordered founded leg `924059ca…` with replay identical. |
| Measurement rig | `Sim.Tests/Systems/HardeningMeasurementRigs.cs` (`31c3adb`, extended `60225a5`, `2502439`, `cbbc704`). Two facts, **skipped unless `SIM_MEASURE=1`**, parameters from `H4_*` environment variables (documented in the class header). It is a diagnostic, not a gate; nothing was tuned against it. |
| Rig ≡ `sim autoplay` | MEASURED: canonical seed 1, 650 turns, no orders — the rig's world hash `2eb5c4a0…` and every metrics series are identical to `sim autoplay`'s for the same seed. |
| Machine | 4 CPUs shared with three sibling streams and a verifier; load average 7–17 during these runs. Wall-clock figures are therefore not performance data (the bench, §5, records its own load). |

**Commands** (run from the repository root after `dotnet build Sim.slnx -c Release`; `T=Sim.Tests/bin/Release/net10.0/Sim.Tests.dll`):

```bash
# one rig arm: world canonical|dev; arm none|allgather|lone|famine|disaster|preforager|tax99|tax100
SIM_MEASURE=1 H4_WORLD=canonical H4_ARM=none H4_AI=0 H4_FOUNDERS=400 H4_SEEDS=1-20 \
  H4_TURNS=900 H4_SNAPSHOT=650 H4_OUT=out.tsv \
  dotnet test $T --filter "FullyQualifiedName~HardeningMeasurementRigs.ColoniesAndLedger"
#   optional: H4_TRACE=1 (starvation trace), H4_GATHER=x / H4_KM2=y (counterfactual forager rates),
#             H4_METRICS_OUT=m.json (sim-autoplay metrics shape over the first H4_SNAPSHOT turns)

# the nightly sweep (ci.yml calibration-nightly) and its scoring against every canonical corridor
dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json
dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll corridors --metrics nightly-metrics.json
SIM_MEASURE=1 H4_METRICS=nightly-metrics.json H4_OUT=corridors.tsv \
  dotnet test $T --filter "FullyQualifiedName~HardeningMeasurementRigs.NightlyCorridors"

# the CI calibration battery
dotnet test Sim.Tests -c Release --filter CalibrationBatteryTests

# AI soak, per (AI, seed): two processes, replay, inspect (exactly the ci.yml AI leg, at 1000 turns)
CLI="dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll"
$CLI run --founded --seed S --turns 1000 --ai-empires N --hash-log a.log --emit-session sa
$CLI run --founded --seed S --turns 1000 --ai-empires N --hash-log b.log --emit-session sb
diff a.log b.log && cmp sa/orders-*.bin sb/orders-*.bin
$CLI replay --founded --seed S --turns 1000 --ai-empires N --orders sa/orders-*.bin --hash-log r.log && diff a.log r.log
$CLI inspect --manifest sa/session-*.json --turn 1000 | grep "reproduction VERIFIED"
$CLI inspect --manifest sa/session-*.json --turn 1000 --answer polities

# bench
$CLI bench --founded --seed 42 --turns 300
```

---

### 1. Colonies (Director §14)

#### 1.1 What the records say colonies are for, and the ranges they set

| source | what it rules | status |
|---|---|---|
| D-037 B1; ADR-012; ADR-021 | A colony is founded only from migration's **unplaced** departure demand: famine-flight desire at a source with **no reachable, viable destination**. A destination is viable while it holds any food (store or last harvest > 0) and its deficit is below 1 / `destinationDeficitRepulsion` (= 1.0, so any partial deficit still leaves it viable). | RATIFIED |
| T4.4 record §0 (Director ruling, option A) | B1 is **collapse-driven**. "T4.4's lack of activation in current canonical worlds is therefore NOT a defect." Voluntary / frontier expansion is a **separate future mechanism**; CR-003 stays quarantined until it exists. | RATIFIED |
| T4.4 D2; ColonizationSystem | **No zero-provision founding** (an expedition must be outfitted from a real granary); the **cascade brake**: a founded daughter holds food, so it is a viable destination and the founder's demand goes to zero the next turn — at most one daughter per source per turn, no runaway (the defect was 12 → 178 settlements by turn 77). | RATIFIED / IMPLEMENTED |
| ADR-025 §2.5 | The founding party is a bounded hazard of the source; a source is never emptied by one founding. | RATIFIED |
| M4 completion §10 | Control is inherited from the parent (a stateless parent founds a stateless colony). | RATIFIED |
| T4.4 §3.2; coherence matrix §6.5 (R2b) | 0 foundings on canonical seeds 1/2/3 × 650 turns and 42/7/1234 × 300–500 turns (no orders, one AI, 40 % and 99 % tax). | CARRIED, pre-forager |
| `queue.md` T4.21 G7(b) | "Stranded by capacity" founding is queued as the first M5 colonization line. **Not implemented.** | OPEN (no packet) |

So the intended range is not a count. It is: **zero foundings in a world where a fed neighbour is always reachable;
foundings, one per episode, surviving and braked, where people are genuinely stranded; never from an empty
granary; never emptying the source; control inherited.**

#### 1.2 Measured (MEASURED, forager layer ON at 4.3 / 2.0, 400 founders, seeds 1–20, 650 turns unless stated)

Colonisation is never scripted. Every stressed arm sets a cause and the pipeline decides: `allgather` (the player
orders 100 % of labour to gathering at turn 0 → Malthus at the forager land ceiling), `lone` (a one-settlement
world: being alone is B1's condition), `famine` (the player orders gathering to 10 % in every settlement at turn
100), `disaster` (COUNTERFACTUAL: CR-016's famine-class disaster armed at λ = 0.01; shipped 0.0).

| world | arm | foundings | unplaced settlement-turns | deficit settlement-turns | colonies alive at end | starving seeds | notes |
|---|---|---|---|---|---|---|---|
| canonical 12 | none (900 t) | **0** | 0 | 1,064 | — | 17/20 | snapshot at 650: 0 foundings |
| canonical 12 | one AI empire | **0** | 0 | 920 | — | 11/20 | |
| canonical 12 | allgather (900 t) | **0** | 0 | 317 | — | 6/20 (late, land ceiling) | 10,209 starved, all after turn 650 |
| canonical 12 | famine | **0** | 87 | 3,700 | — | 20/20 | world extinct by 650; stranded demand on 87 settlement-turns, nothing founded |
| canonical 12 | disaster (counterfactual), seeds 1–11 | **0** | 0 | 4,160 | — | 11/11 | 133,806 starved; 158–295 people left at 650 (arm stopped at 11 seeds for machine load) |
| dev 4 | none (900 t) | **0** | 0 | 406 | — | 12/20 | |
| dev 4 | one AI empire | **0** | 0 | 356 | — | 10/20 | |
| dev 4 | allgather (900 t) | **0** | 0 | 865 | — | 18/20 | 150,872 starved: a Malthusian world that still never strands anyone |
| dev 4 | famine | **0** | 101 | 1,258 | — | 20/20 | extinct; stranded demand on 101 settlement-turns, nothing founded |
| dev 4 | disaster (counterfactual) | **0** | 0 | 2,574 | — | 20/20 | 30–126 people left at 650 |
| canonical, 1 settlement | lone | **20** (1 per seed) | 31 | 161 | **20/20** | — | founded turns 3–374; party 2–141; provisions 1–155 grain; colony population at 650: 913–8,311 |
| dev, 1 settlement | lone | **22** (seed 9: 3) | 31 | 152 | **22/22** | — | colony population at 650: 105–9,704 |
| dev 4, R3 crash rates (1.0 / 2.0), 900 t | none, 100 / 200 / 400 founders | **38 / 43 / 42** in 14–17 of 20 seeds | 1,369–1,696 | ~24,000 | 14 / 11 / 16 alive | 20/20 | a world that cannot feed itself: B1 fires (founded turns 85–889); colonies of 1–20 people, most dying with their world |

Across every arm (525 seed-runs, 206 colonies): **most foundings in one turn = 1**; **0 zero-provision foundings**
(every colony's grain at founding ≥ 1); **0 control mismatches** (every colony's controller is its founder's); 0
exceptions; conservation exact. Sources with demand that stood at population 0 after the step occur only in the
collapsing worlds (famine arms, crash rates), where starvation kills in the same step; that a founding itself
never empties a source is ADR-025's bound (each bucket's party is the floor of a hazard strictly below its count),
pinned by `ColonizationTests.Colonization_PartyNeverEmptiesSource` — the attribution here is INFERRED, the rig
cannot separate the two causes within a step.

#### 1.2a Why a starving or collapsing multi-settlement world never founds (MEASURED + reading of the code)

In every multi-settlement arm, unplaced demand is 0 except where granaries are already empty. The viability gate
(`MigrationSystem`, T2.13) keeps a destination viable while it has food and a deficit below 1.0. In the Malthusian
`allgather` worlds and the disaster collapse, some reachable neighbour always qualifies, so famine flight is placed
by migration. In the `famine` arm demand IS stranded (87 / 101 settlement-turns) and still nothing is founded: a
world eating at 10 % gathering has no grain to outfit an expedition, and an empty granary outfits nobody (T4.4 D2's
binding clearing cost). That last step is INFERRED — the rig records foundings, not the reason a founding was
refused. This is the ruled behaviour of a collapse-driven mechanism. It is not a calibration
failure. The only way to raise the founding rate is a **new** mechanism: the queued "stranded by capacity" line
or the frontier mechanism CR-003 waits for. Neither is ruled, so neither was built.

#### 1.3 Verdict

**Colonies are within the intended behaviour, with the current forager food.**

- Zero activation in canonical and dev play is the ruled reading (T4.4 §0). It matches every earlier measurement:
  the carried pre-forager tables, and the pre-forager control in §3.3, which also founds 0.
- Where B1's condition actually arises (`lone`; and a world starving at the R3 crash rates), colonisation is
  correct and well-behaved:
  - never more than one founding per turn, and the brake holds (no runaway);
  - control is inherited;
  - provisions are real;
  - a colony of a FED parent survives and grows (`lone`: 42/42 alive, 105–9,704 people at 650);
  - a colony of a world that cannot feed anyone shares that world's fate (crash rates: 41 of 123 alive at 900),
    which is the honest outcome, not a colony defect.
- **Nothing was retuned.** No colony defect was found, so nothing needed fixing.

**What the Director should expect in a playtest.** Colonies effectively never appear on the 12-settlement map,
with or without AI, under famine or under Malthus. A colony needs a stranded population. This is unchanged from
the baseline (§2.7, §12 item 16).

---

### 2. Ledger (Director §11)

#### 2.1 The regression test

`SubstitutionCreditRegressionTests` (the R3 crash configuration, dev seed 42, 900 turns) **passes** on `9bb7423`
(MEASURED, 1/1, 8 s). The rig reproduces its state independently: crash configuration, seed 42 → 1 credit-carrying
row-turn, conservation exact, no negative stock, and population 13 at turn 900.

#### 2.2 Stress (MEASURED; every row is 20 seeds × 900 turns, no orders, forager ON at the shipped rates unless stated)

| world | founders / settlement | exceptions | conservation audit at 650 and at 900 | turns with a negative stock | remainder-invariant violations |
|---|---|---|---|---|---|
| dev | 400 | 0 | 20/20 exact | 0 | 0 |
| dev | 200 | 0 | 20/20 exact | 0 | 0 |
| dev | 100 | 0 | 20/20 exact | 0 | 0 |
| canonical | 400 | 0 | 20/20 exact | 0 | 0 |
| canonical | 200 | 0 | 20/20 exact | 0 | 0 |
| canonical | 100 | PENDING | | | |
| dev, R3 crash rates (1.0 / 2.0) | 100 / 200 / 400 | 0 / 0 / 0 | 60/60 exact | 0 | 0 |
| dev, R3 crash rates, seeds 21–120 (100 seeds) | 100 | 0 | 100/100 exact | 0 | 0 — credit carried on seeds 42 and 99 (2 row-turns) |

Notes on the table:

- The founding vectors are re-apportioned by largest remainder over the shipped stable vector, with
  `foodStore` 15 per founder. This reproduces the R3 / regression-test 100-founder vector exactly.
- "Remainder invariants" means:
  - only the staple carries a negative (credit) remainder;
  - every credit is greater than −3;
  - every other remainder is below 1.
- Every rig run in this record — every arm of §1, §3.3 and §4.3, 525 seed-runs at the time of writing — threw
  nothing and closed its conservation audit exactly. (The CLI soak runs of §4.1 run no audit; they are checked by
  hash agreement instead.)

**The credit path is rare at the shipped rates.** Across all the shipped-rate runs, 0 row-turns carried a credit.
In the crash configuration it is reached on 2 of 120 seeds (42 and 99, one row-turn each), settles without a
negative amount, and the audit closes exactly. The regression test therefore remains the instrument that holds it.

**Code review of the fix's edges** (INFERRED, read on `e6b7e42`):

- A negative staple request asks nothing of the store. The credit is carried exactly.
- `BoundStore`'s granary cap is guarded on a positive annual demand, so a negative substituted demand cannot
  produce a negative or over-large overflow sink.
- Spoilage reads only the held stock.

No remaining edge case was found, and no clamp was added.

#### 2.3 Verdict

**The ledger regression stays fixed.** No Ledger exception and no conservation failure appeared in any run. No
fix was needed.

---

### 3. Calibration (Director §16; CI `calibration` and `calibration-nightly` jobs)

#### 3.1 The CI battery (MEASURED)

`dotnet test … --filter CalibrationBatteryTests`: **7/7 passed** in 297 s under load. The members are:

- canonical fed corridors, seeds 1 and 2;
- the era-boundary continuity pin;
- dev Malthus quarantine, seeds 42 and 7;
- the dev migration-quarantine teeth;
- the corridors file.

#### 3.2 The nightly sweep (MEASURED; `sim autoplay --seeds 20 --turns 650`, 21 m 39 s under load, exit 0)

Scored against every canonical corridor with the battery's own definitions (`NightlyCorridors`):

| corridor | band | status | measured range, 20 seeds | in band | in quarantine window |
|---|---|---|---|---|---|
| fedGrowthPerYear | [0.0005, 0.001] | **ratified gate** | 0.000707 – 0.000741 | **20/20** | — |
| crudeBirthRatePer1000 | [37, 46] | **ratified gate** | 41.370 – 41.386 | **20/20** | — |
| crudeDeathRatePer1000 | [36, 45] | **ratified gate** | 40.643 – 40.670 | **20/20** | — |
| pyramidChildShare | [0.35, 0.46] | **ratified gate** | 0.4098 – 0.4103 | **20/20** | — |
| pyramidAdultShare | [0.46, 0.57] | **ratified gate** | 0.5633 – 0.5638 | **20/20** | — |
| pyramidElderShare | [0.015, 0.08] | **ratified gate** | 0.02626 – 0.02642 | **20/20** | — |
| era-boundary continuity (\|Δr\| ≤ 0.0001/yr) | — | **permanent battery pin** | max 3.24e-5 | **20/20** | — |
| densityPerArableKm2 | [0.15, 0.6] | quarantined (window [0.36857, 0.74211]) | 0.3229 – 0.6454 | 19/20 | **18/20** — seeds 3 (0.3229) and 9 (0.3629) below the window floor |
| migrationGrossPerDecade | [0.001, 0.01] | quarantined (window [0.0009, 0.01]); not a gate since M4 | 0.000430 – 0.000914 | 0/20 | **1/20** |

`sim corridors` (the nightly's gate step) exits **0**. It reports both quarantined corridors as WINDOW BREACH,
which does not gate (T3.12). Final populations are 89,812 – 145,393. Crashes: 0/20.

**Attribution of the two window breaches** (MEASURED with the pre-forager control on the same tree, §3.3):

| corridor | forager ON (shipped) | forager OFF (control) | attribution |
|---|---|---|---|
| density, seed 3 | 0.3229 (below the window) | 0.3527 (already below) | **pre-existing**, deepened by the forager layer |
| density, seed 9 | 0.3629 (below the window) | 0.4062 (inside) | **forager layer**: population about 10 % lower at 650 on every seed |
| migration | 1/20 in the window | **0/20** in the window (0.00023–0.00044) | **pre-existing**: the forager layer *raises* gross migration. ADR-033 §R2c already recorded these windows breached at `f1fe76f`. |

Neither breach is a ratified gate, and the quarantine windows were not re-pinned. The window literals stay a
Director ruling (T3.12, `corridors.json`).

**One stale printed line (reported, not changed).** The battery's density message still prints "14/20 seeds in
band; six over the ceiling (20, 8, 6, 1, 13, 2)", which is the T4.19c sweep. The sweep above measures 19/20 in
band and seed 2 as the only seed over the ceiling. This is test output text, not a tooth.

#### 3.3 Starvation in the order-free world (MEASURED; a calibration observation for §10)

No ratified band reads starvation, and the dev quarantine tooth `starvedTotal == 0` (CR-003) runs on seeds 42 and 7
only, where it is 0. Over 20 seeds:

| world, horizon | forager ON (shipped) | forager OFF (control, same tree) |
|---|---|---|
| canonical, 650 turns | **11/20 seeds starve; 2,402 deaths** | 2/20; 423 |
| canonical, 900 turns | 17/20; 6,284 | — |
| dev, 650 turns | 10/20; 1,431 | 1/20; 185 |
| dev, 1000 turns (the battery's horizon) | **15/20 starve** (5/20 pass the tooth); 0 crashes; 18/20 monotone; 16/20 inside [71k, 119k] | 19/20 pass; 0 crashes; 19/20 monotone; 13/20 inside |

**Cause** (MEASURED with `H4_TRACE=1`, dev seeds 1–20). Starvation comes in two forms.

- **Early weather shortfalls.** These hit with an empty granary, at populations far below the forager land ceiling.
  For example, seed 1 at turn 18: 282 people, a land ceiling of 8,845 a year, a deficit of 0.24 and a store of 0.
  The thin labour-side margin (0.55 × 4.3 = 2.37 person-years per adult) leaves no surplus to store.
- **Late, land-bound shortfalls.** After about turn 650 on dev, settlements approach the forager land ceiling
  (seed 4, turn 898: 20,781 people against 25,602 a year at 2.0 per km², deficit 0.35, store 0), where a weather
  draw tips them into deficit. An order-free world never researches agriculture, which would lift that ceiling 13×.

R4a chose 4.3 / 2.0 as "zero starvation on dev seeds 42 and 7" (CARRIED). That criterion holds on those two seeds
only.

**Counterfactual yield sweep** (MEASURED on dev; not shipped; `H4_GATHER`):

| per gatherer | starving seeds by 650 | starving seeds by 900 | deaths by 900 |
|---|---|---|---|
| 4.3 (shipped) | 10 | 12 | 3,851 |
| 4.5 | 4 | 5 | 3,305 |
| 5.0 | 1 (equal to the pre-forager control) | 4 | 7,455 |
| 5.5 | 0 | 4 | 10,906 |

A higher per-gatherer yield removes the early weather starvation. It cannot remove the late land-ceiling starvation:
a faster-growing order-free world reaches the 2.0 per km² ceiling sooner, so deaths after 650 rise.

**Not retuned.** This is a calibration question on the Director's accepted lever (§10). Any change moves every
world golden, and sibling stream H2 re-pinned those goldens in this same pass. The bands that are ratified pass.
In a played world agriculture is researched (`root_crop`, 440 RP, is the cheapest route), so the late form needs a
player or AI who never researches it (INFERRED). **OPEN for the Director.** The options are:

- (a) accept the shipped 4.3 / 2.0 with this record;
- (b) raise per-gatherer output (about 5.0 restores the pre-forager early-starvation rate on dev; canonical not
  measured at 5.0);
- (c) re-aim the dev quarantine tooth, a CR-003 question.

**Is the gathering-only architecture still met (§10)?** Yes (MEASURED behaviour plus the code read in the baseline
§2.2):

- Turn-1 food is the forager block.
- Agriculture is not known at founding.
- The cultivated rates apply only once `activity.farming` is knowledge-eligible.

---

### 4. AI soak (Director §16)

#### 4.1 Runs (MEASURED; CLI built from `9bb7423`; canonical world, no player orders; 1000 turns)

| AI empires | seed | two processes | replay of the run log | `sim inspect` | control-row losses | final hash |
|---|---|---|---|---|---|---|
| 1 | 1 | byte-identical (hash logs and run logs) | identical | VERIFIED | 0 | `a9dbd4ef3188…` |
| 1 | 2 | identical | identical | VERIFIED | 0 | `e5f6a4773f67…` |
| 1 | 3 | identical | identical | VERIFIED | 0 | `f939c23a3cae…` |
| 1 | 4 | identical | identical | VERIFIED | 0 | `184fa99fac65…` |
| 1 | 5 | identical | identical | VERIFIED | 0 | `2401991d13a3…` |
| 3 | 1 | identical | identical | VERIFIED | 0 | `8d5f39cbec95…` |
| 3 | 2 | identical | identical | VERIFIED | 0 | `a880b510da94…` |
| 3 | 3 | identical | identical | VERIFIED | 0 | `80fd84dfaa9a…` |
| 3 | 4 | identical | identical | VERIFIED | 0 | `7b8cc53530d4…` |
| 3 | 5 | identical | identical | VERIFIED | 0 | `d484fdda7b13…` |

No run threw. All 50 CLI steps (10 configurations × run A, run B, replay, inspect, `--answer polities`) exited 0.

#### 4.2 AI milestones (MEASURED from the run logs: the order turn; it applies at the next turn)

| run | polity | first granary order | first road order | first levy (rate) | max rate | A2 | A3 |
|---|---|---|---|---|---|---|---|
| ai1 s1 | 2 | 2 | 141 | 373 (5 %) | 40 % | 239 | 472 |
| ai1 s2 | 2 | 6 | 138 | 360 | 40 % | 232 | 456 |
| ai1 s3 | 2 | 2 | 148 | 390 | 40 % | 250 | 492 |
| ai1 s4 | 2 | 3 | 157 | 392 | 40 % | 256 | 492 |
| ai1 s5 | 2 | 6 | 150 | 384 | 40 % | 249 | 484 |
| ai3 s1 | 2 / 3 / 4 | 2 / 7 / 9 | 209 / 202 / 186 | 512 / 481 / 462 | 40 % | 335 / 316 / 300 | 632 / 595 / 576 |
| ai3 s2 | 2 / 3 / 4 | 6 / 5 / 6 | 196 / 185 / 185 | 485 / 456 / 453 | 40 % | 315 / 297 / 296 | 602 / 569 / 565 |
| ai3 s3 | 2 / 3 / 4 | 12 / 4 / 2 | 203 / 206 / 211 | 504 / 490 / 506 | 40 % | 328 / 322 / 333 | 624 / 606 / 624 |
| ai3 s4 | 2 / 3 / 4 | 4 / 6 / 3 | 199 / 201 / 231 | 466 / 491 / 558 | 40 % | 309 / 321 / 369 | 578 / 608 / 684 |
| ai3 s5 | 2 / 3 / 4 | 6 / 30 / 9 | 206 / 204 / 204 | 501 / 500 / 493 | 40 % | 328 / 327 / 323 | 619 / 618 / 610 |

- The AI's tax valve never exceeds 40 %.
- No polity reached A4 within 1000 turns.
- With three rivals, each AI holds 3 settlements. Its research income is lower and every milestone is about 60–100
  turns later.

**KNOWN DEFECT, confirmed on every run (owned by H2, §7 of the Director's instruction).** Every AI polity's first
levy is ordered while it is still in **A2**: 373 vs A3 at 472, and every row above. This is the "AI taxed while
Neolithic" defect. Sibling stream H2's Age half of the tax gate (`taxationMinAge` 3 on `m5h-h2-tax-revolt`) is
built to remove it. **Re-run this table after the merge**: each first levy should land after that polity's A3
order.

#### 4.3 Revolt soak (MEASURED in-process with the rig; canonical; the player's capability is a completed Taxation node and the levy an ordinary turn-0 order)

| arm | AI empires | seeds | exceptions | audit | control-row losses | polities at the end | AI orders produced |
|---|---|---|---|---|---|---|---|
| tax 99 % | 1 | 1–5, 1000 t | 0 | exact | **0** | 2 | 137–208 |
| tax 100 % | 1 | 1–5, 1000 t | 0 | exact | **1** per seed (the capital, turn 2: the 100 %-at-full-reach corner) | 3 (the revolt founds polity 3: capital-less, AI-driven for 998 turns) | 194–605 |
| tax 99 % | 3 | 1–5, 1000 t | 0 | exact | **0** | 4 | 214–454 |
| tax 100 % | 3 | PENDING | | | | | |

What this says about the base tree (MEASURED; the model is H2's to change):

- **100 % revolts the capital on turn 2, every seed.** This is the deterministic corner that the Director's §4
  rejects ("Do NOT implement an instant revolt at 100 % tax").
- **99 % never revolts in 1000 turns** (10 seed-runs). §5 asks that sustained extreme taxation *eventually* produce
  instability.

Sibling stream H2's gradual model is built to change both. **Re-run this table after the merge.** The expected
reading is no turn-2 loss at 100 %, and delayed, segment-based instability under sustained extremes.

The soak's own purpose holds on this tree: a capital-less AI polity founded by revolt was driven by the producer
for 998 turns on 5 seeds with 0 exceptions and an exact audit.

#### 4.4 Defects found and fixed (both proved, both pinned, no golden moved)

**D-H4-1. `sim replay`, `sim inspect` and `sim run --orders` rejected any log containing a revolt-founded
polity's orders.** Fixed in `9bcad5b`.

- **How it happens.** Every revolt founds a new AI polity at roster max + 1 (D-048), and the UI's producer drives
  it. The up-front `OrderValidation.ValidateAgainstWorld` pass checks actors against the **turn-0** roster.
- **Reproduced** (MEASURED): canonical seed 42 with the `FoundedHarness` labour orders revolts settlement 0 at turn
  58, and the new polity's first order is stamped 58. Then `sim replay` and `sim inspect` of that run log exit 2:
  "SetResearchTarget is issued by polity 2, which is not a registered Empire".
- **Playtest impact.** Any playtest session in which a settlement revolts (a path the Director intends to exercise
  under the new tax model) could not be replayed or inspected with the baseline's forensic commands
  (`m5-playtest-baseline.md` §12). The UI's own `--resume` does not run this pass and was not affected.
- **Fix.** The stream-V colony rule (`7100c77`) is applied to actors. An actor id above every turn-0 roster id,
  delivered after the first step, has its existence and control checks deferred to delivery, where every consumer
  refuses it on PREV.
- **Pinned by** `RevoltPolityOrderValidationTests` (5 pins). One of them asserts that a never-founded actor's
  orders leave the world hash-identical turn by turn.

**D-H4-2. `sim run` never drove a revolt-founded polity when `--ai-empires` was 0; the UI does.** Fixed in
`619b109`.

- **How it happens.** `UiSession.EndTurn` calls `AiOrders.Append` every turn. The CLI decided once, at founding,
  whether to run the producer, a gate written before revolts created polities.
- **Reproduced** (MEASURED): the same run's log held only the player's 6 orders, so the CLI world diverged from what
  the UI would play.
- **Fix.** Every founded run takes the run-log path. A world with no AI polity issues nothing, so hashes are
  unchanged.
- **Pinned by** `CliRevoltPolityProducerTests`. It invokes the real CLI entry point and asserts three things: the
  revolt polity's orders are in the run log; every per-turn hash equals an in-process replica of the UI end-turn
  loop; and `sim replay` of the run log reproduces every hash.

**Mutant record** (bound: 480 s, about 5× the 94 s clean run of the two classes). Each mutant was killed by a
semantic pin, and none hung.

| mutant | change | killed by |
|---|---|---|
| M1 | Roster check ignores the deferral. | 4 pins, including the CLI end-to-end. |
| M2 | Control checks are not deferred. | 2 pins. |
| M3 | Turn-0 orders are deferred as well. | `TheSameActorOnTurn0_IsStillRejected…` |
| M4 | The CLI producer is gated at founding again. | `CliRevoltPolityProducerTests` |
| M5 | Tax authority is deferred as well. | `AFutureActorStillLegislatesOnlyItsOwnTax…` |

**Merge note for the orchestrator.** After H2 merges, revolts become reachable under sustained taxation. If a
`ci.yml` founded leg that replays the **input** order file (not the emitted run log) ever revolts, its replay will
legitimately diverge, because the run log now carries the revolt polity's orders. The AI leg already replays the
emitted run log; that is the pattern to follow. MEASURED on `9bb7423`: the ordered founded leg has 0 control-row
losses in 300 turns.

---

### 5. Bench (PENDING)

---

### 6. Open items for the Director (from this stream)

1. **Starvation in the order-free world (§3.3).** The forager calibration reintroduces weather-driven starvation on
   about half of all seeds. The ratified bands pass, and the dev tooth passes only on its two seeds. The options
   are listed in §3.3. **OPEN.**
2. **Quarantine windows (§3.2).** Density breaks its window on 2/20 seeds (1 caused by the forager layer, 1
   pre-existing). Migration is outside its window on 19/20, and that was already the case before the forager
   layer. Not gates; window literals are the Director's. **OPEN** (CR-003 lineage).
3. **Colonies (§1).** They behave as ruled. More colonisation in normal play needs the unbuilt "stranded by
   capacity" line or the frontier mechanism. **OPEN / DEFERRED** (no packet).
4. **AI levy before A3 (§4.2).** Confirmed for every AI polity on 10/10 soak runs (20 polity-runs). The fix belongs to H2; re-measure after the
   merge.
