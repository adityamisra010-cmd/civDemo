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

Across every rig run of this record (655 seed-runs, 358 colonies, including the 100-seed crash-rate sweep of §2.2): **most foundings in one turn = 1**; **0 zero-provision foundings**
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
| canonical | 100 | 0 | 20/20 exact | 0 | 0 |
| dev, R3 crash rates (1.0 / 2.0) | 100 / 200 / 400 | 0 / 0 / 0 | 60/60 exact | 0 | 0 |
| dev, R3 crash rates, seeds 21–120 (100 seeds) | 100 | 0 | 100/100 exact | 0 | 0 — credit carried on seeds 42 and 99 (2 row-turns) |

Notes on the table:

- The founding vectors are re-apportioned by largest remainder over the shipped stable vector, with
  `foodStore` 15 per founder. This reproduces the R3 / regression-test 100-founder vector exactly.
- "Remainder invariants" means:
  - only the staple carries a negative (credit) remainder;
  - every credit is greater than −3;
  - every other remainder is below 1.
- Every rig run in this record — every arm of §1, §2.2, §3.3 and §4.3, 655 seed-runs — threw
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
| tax 100 % | 3 | 1–5, 1000 t | 0 | exact | **1** per seed (the capital, turn 2) | 5 (the revolt founds polity 5) | 255–708 |

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

**The full `ci.yml` determinism-xproc step on the fixed CLI** (MEASURED: the step's script extracted verbatim, run
on a CLI built from this branch's code at `cbbc704` — every later commit is documents only — exit 0, 485 s under load). Every leg passed with the baseline's hashes:

| leg | result |
|---|---|
| toy orderless, 2 processes × 400 turns | `7f93ac50…` |
| toy ordered vs replay | `86a31edf…` |
| founded orderless, 2 processes × 300 turns | `02c7f9eb…`, which equals `FOUNDED_GOLDEN` |
| founded ordered vs replay | `924059ca…` |
| AI leg, 600 turns | `2954c51f…`; 194 orders of kinds 3–8; replay identical; `sim inspect` "reproduction VERIFIED: 601 turns" |

**Merge note for the orchestrator.** After H2 merges, revolts become reachable under sustained taxation. If a
`ci.yml` founded leg that replays the **input** order file (not the emitted run log) ever revolts, its replay will
legitimately diverge, because the run log now carries the revolt polity's orders. The AI leg already replays the
emitted run log; that is the pattern to follow. MEASURED on `9bb7423`: the ordered founded leg has 0 control-row
losses in 300 turns.

---

### 5. Bench (MEASURED; `sim bench --founded --seed 42 --turns 300`; the branch CLI, code = `cbbc704`)

The full Sim.Tests suite was finishing on one core during these runs; other agents had gone quiet.

| run | load average (1 min) before | total |
|---|---|---|
| 1 | 1.01 | 35,787.96 ms |
| 2 | 1.49 | 37,569.11 ms |
| 3 | 1.94 | 36,723.52 ms |

**A/B against the base tree under the same conditions**, alternating binaries. The base is a CLI built from
`31c3adb`, whose simulation code is `9bb7423`.

| pair | base `9bb7423` | branch `cbbc704` |
|---|---|---|
| 1 | 38,285.85 ms (load 1.86) | 37,373.12 ms (load 2.08) |
| 2 | 36,638.75 ms (load 1.76) | 35,854.89 ms (load 1.39) |

This stream changes no simulation hot path. `sim bench` calls neither the order validation nor `Cli.Run`, and the
A/B shows no regression.

Against R4a's carried 29,860.75 ms at `9648f7b` (a different load, CARRIED), both binaries here are about 20–28 %
slower. The A/B places that gap in the environment, not in this branch. Whether `9648f7b` → `9bb7423` (R5:
content and tests only) costs anything is not separable on a shared machine. INFERRED: nil.

---

### 6. Validation of this branch (MEASURED, 2026-10-05)

| gate | result |
|---|---|
| `dotnet build Sim.slnx -c Release` | 0 warnings, 0 errors |
| Sim.Tests (full, `--no-build`) | **1466 passed / 0 failed / 8 skipped (1474)**, 32 m 52 s under load. Baseline 1460 / 0 / 6, plus 6 new passing tests (`RevoltPolityOrderValidationTests` ×5, `CliRevoltPolityProducerTests` ×1) and 2 new skipped rigs (`HardeningMeasurementRigs` ×2, skipped unless `SIM_MEASURE=1`). |
| Sim.Ui.Tests (full) | **473 / 473** |
| `CalibrationBatteryTests` | 7 / 7 (§3.1) |
| `SubstitutionCreditRegressionTests` | 1 / 1 (§2.1) |
| `scripts/check-banned-constructs.sh` | exit 0 |
| `scripts/check-read-isolation.sh` | exit 0 |
| `scripts/check-readonly-proof.sh` | exit 0 (CS0200, CS1061) |
| `research-content-audit.py`, `research-calibration-report.py`, `research-gameplay-unlock-audit.py` with `--check` | exit 0 ×3 |
| `ci.yml` determinism-xproc step, verbatim | exit 0, every hash as the baseline (§4.4) |
| Goldens | **none moved** |

---

### 7. Open items for the Director (from this stream)

1. **Starvation in the order-free world (§3.3).** The forager calibration reintroduces weather-driven starvation on
   about half of all seeds. The ratified bands pass, and the dev tooth passes only on its two seeds. The options
   are listed in §3.3. **OPEN.**
2. **Quarantine windows (§3.2).** Density breaks its window on 2/20 seeds (1 caused by the forager layer, 1
   pre-existing). Migration is outside its window on 19/20, and that was already the case before the forager
   layer. Not gates; window literals are the Director's. **OPEN** (CR-003 lineage).
3. **Colonies (§1).** They behave as ruled. More colonisation in normal play needs the unbuilt "stranded by
   capacity" line or the frontier mechanism. **OPEN / DEFERRED** (no packet).
4. **AI levy before A3 (§4.2).** Confirmed for every AI polity on 10/10 soak runs (20 polity-runs). The fix
   belongs to H2. Re-measure after the merge.
5. **Revolt corner at 100 %, and no revolt at 99 % in 1000 turns (§4.3).** This is the base tree's tax model. H2
   replaces it. Re-measure after the merge.
6. **Replaying a played session that contained a revolt (§4.4, D-H4-1).** Fixed here. Until this branch is merged,
   `sim replay` and `sim inspect` cannot read such a session.

#### Re-run checklist after the hardening streams merge (for the orchestrator)

Run these with the §0 commands on the merged tree, and append the results as a new dated section.

1. The four `tax99` / `tax100` arms of §4.3. Expected:
   - no turn-2 capital loss at 100 %;
   - instability only after accumulation.
2. The AI soak of §4.1 and §4.2. Expected: each polity's first levy after its A3 order.
3. The nightly sweep and `NightlyCorridors` of §3.2, and `CalibrationBatteryTests`.
4. The colony arms of §1.2 for `none`, one AI and `lone`.
5. The ledger rows of §2.2 for 400 and 100 founders on both worlds.
6. The `ci.yml` determinism-xproc step. If a founded ordered leg ever records a revolt, replay the emitted run log,
   not the input file (§4.4 merge note).

---

## 2026-10-05 — stream F2: order-free forager starvation re-measured and swept (base `b2dd24f`, `m5-hardening`)

**Question (Director §10).** Can the smallest defensible change to the existing forager levers bring order-free
starvation down to the pre-forager rate (≤ 2/20 canonical seeds at 650 turns; ≤ 1/20 dev seeds at 1000 turns)
while agriculture stays researched and measurably raises food?

**Rig.** `HardeningMeasurementRigs.ColoniesAndLedger`, arm `none`, no AI, 400 founders, seeds 1–20, counterfactual
rates via `H4_GATHER` / `H4_KM2` (not shipped). A seed "starves" if any starvation death occurs by the horizon.
All rows MEASURED on `b2dd24f` (H1–H4 merged), Release build.

### Re-measurement at the shipped values (4.3 per gatherer, 2.0 per km²)

| world, horizon | starving seeds | deaths |
|---|---|---|
| canonical, 650 | **11/20** | 2,402 |
| dev, 650 | 10/20 | — |
| dev, 1000 | **15/20** | — |

Identical to H4's record (§3.3): the H1–H4 merge did not move the order-free world.

### Sweep

| per gatherer | per km² | canonical 650 (seeds / deaths) | dev 650 | dev 1000 (seeds / deaths) |
|---|---|---|---|---|
| 4.3 (shipped) | 2.0 | 11 / 2,402 | 10 | 15 / — |
| 4.5 | 2.0 | — | 4 | 10 / 31,255 |
| 4.7 | 2.0 | — | 2 | 10 / 40,798 |
| 4.9 | 2.0 | — | 1 | 10 / 50,069 |
| 5.0 | 2.0 | **2 / 423** (target met) | 1 | **10 / 52,868** (target missed) |
| 4.3 | 2.5 | — | 10 | 13 / 2,752 |
| 4.3 | 3.0 | — | 10 | 12 / 1,779 |
| 4.3 | 4.0 | — | 10 | 12 / 1,779 |
| 4.7 | 3.0 | 4 / 686 | 2 | 2 / 336 |
| 5.0 | 4.0 | — | 1 | 1 / 185 (target met) |

Pre-forager control (H4 §3.3, same rates of measurement): canonical 2/20 at 650, dev 1/20 at 1000.

### Reading

- **Per gatherer alone cannot reach the dev target.** It removes the early weather form (dev 650: 10 → 1 at
  4.9–5.0; canonical 650: 11 → 2 at 5.0) but the late land-ceiling form stays at 10/20 dev seeds by 1000 at every
  value from 4.5 to 5.0, and deaths grow with the rate (faster growth reaches the 2.0 per km² ceiling sooner).
- **The land ceiling alone does not help** (4.3 with 2.5–4.0: 12–13/20): the early form is labour-bound.
- **Only the pair reaches both targets**, at roughly 5.0 per gatherer and ≥ 3–4 per km² (4.7 / 3.0 still misses
  canonical with 4/20 and dev with 2/20).
- **Neither half of that pair is defensible from the records.**
  - Per km²: the recorded reference range (`sim.json` farming.preCultivation `_doc`; Binford 2001, Kelly 2013,
    Kroeber 1939) puts the richest riverine foragers at 0.5–2 per km²; 2.0 already sits at its top. 3–4 is
    1.5–2× above the richest recorded foragers.
  - Per gatherer: 5.0 equals the cultivated `outputPerFarmerPerYear` 5.0, so agriculture's per-worker gain would
    fall from +16 % (5.0 vs 4.3) to **0 %**; agriculture would raise food only through the land ceiling
    (26 / 2 = 13×, or 26 / 4 = 6.5× at the pair). 5.0 per food-sector worker = 2.75 per adult, inside Kelly's /
    Kaplan et al.'s 1.7–3×, so it is literature-admissible on its own, but it does not achieve the dev target.
- **Storage / weather-buffer levers.** The only store parameters (`consumption.grainSpoilagePerYear` 0.08,
  `granaryYearsOfDemand` 1.5; T4.2) are derived for fixed cereal granaries and apply to every economy; the records
  hold no forager-specific storage reference class, so they are not a forager lever. Not changed.

### Decision (F2)

**STOP — shipped values kept (4.3 / 2.0).** No defensible setting among the existing levers meets both targets
with every record honoured. No value, golden, band, tooth or `ci.yml` pin changed. Agriculture is unchanged:
still researched (not known at founding), land ceiling 13× (26.0 vs 2.0 per km²), per-worker +16 % (5.0 vs 4.3).

**OPEN for the Director** (the sweep above is the evidence):
- (a) accept 4.3 / 2.0 with the order-free starvation recorded here (a player or AI who researches agriculture
  lifts the late ceiling; the early form remains on about half the worlds);
- (b) raise per gatherer to 5.0 only: canonical meets ≤ 2/20 at 650, dev stays 10/20 at 1000 with more deaths, and
  agriculture's per-worker gain becomes 0 %;
- (c) rule a forager land density above the recorded 0.5–2 per km² range (≈ 4.0 with 5.0 per gatherer meets both
  targets on dev; canonical at that pair not measured);
- (d) re-aim the dev CR-003 tooth / accept land-ceiling Malthus as the honest outcome of a world that never
  researches agriculture.

## 2026-10-05 — final integration: forager decision and verifier correction (append-only; the F2 section above is unchanged)

**Correction (F2 verifier, MEASURED on `0fad91d`, dev world, seeds 1–20, 1000 turns).** The F2 reading "only the
pair reaches both targets, at roughly 5.0 per gatherer and ≥ 3–4 per km²" and option (c)'s "≈ 4.0" overstate the
land density needed. **5.0 / 3.0 also meets both dev targets**: 1/20 starving at 650, 1/20 by 1000, 185 deaths —
identical to 5.0 / 4.0. 5.0 / 2.5 gives 1/20 at 650 but 6/20 by 1000 (7,923 deaths); 4.8 / 2.5 gives 2/20 and
5/20 (5,977). The land-ceiling constraint saturates near 3.0 per km² (4.3/3.0 and 4.3/4.0 are also identical), so
option (c) should read "5.0 per gatherer with ≈ 3.0 per km² (1.5× the top of the recorded 0.5–2 per km² range)".
**Canonical and every corridor/band are UNMEASURED at 5.0/3.0 and 5.0/4.0** — no counterfactual pair has canonical
or corridor evidence.

**Decision (orchestrator, final for this pass): keep the shipped 4.3 per gatherer / 2.0 per km².** Reasons: the
ratified calibration bands pass at the shipped values; 5.0 per gatherer equals the cultivated 5.0 per farmer and
would erase agriculture's per-worker gain (+16 % → 0 %); 3.0+ per km² exceeds the recorded forager range; and no
counterfactual pair has canonical or corridor evidence. **Status: OPEN for the Director** — options (a)–(d) above
stand, with (c) corrected to 5.0 / ≈ 3.0.

## 2026-10-06 — Director rulings (pointer appended in the M5 polish pass; append-only, the sections above are unchanged)

D-050 (`docs/d050-m5-hardening-rulings.md`, Director 2026-10-06) rules this record's forager question. Ruling 2
keeps **4.3 per gatherer / 2.0 per km²** and rejects the 5.0 / 3.0 alternative: order-free worlds may starve in bad
weather (11/20 canonical seeds), and a player who neglects food can starve. The four "OPEN for the Director" places
above — §3.3, §7 item 1, the F2 "Decision (F2)" list and the final-integration "Status: OPEN for the Director" — are
therefore **RULED: option (a)**, accept the shipped values with this record. §7 items 2 (quarantine windows) and 3
(colonies) are not addressed by D-050 and keep their status. D-050 ruling 1 also ratifies ADR-034 (the order-
validation deferral that §4.4's revolt-session replay fix relies on).

## 2026-10-10 — map frame cost, `Sim.Ui --frame-cost map` (M5 map workstream P4; append-only, the sections above are unchanged)

**Status:** MEASURED by the P4 agent on branch `m5w-p4-map-frame-cost` at commit **`86bd07f`** (base `82bde01`
`m5p-d051-fixes` + `55b71b2` projection stride + `13e3536` the tool + `86bd07f` its run-wide JIT warm-up), 2026-10-10.
This is a measurement, not a budget: ADR-009 sets no frame budget and none is proposed here.

**Load (read before the numbers).** The machine has 4 CPUs shared with several other agent jobs running at the same
time; load average was 9.0 when the run started and 8.2 when it ended (68 s). Every millisecond below is wall-clock
under that load. It is **not performance data for a budget**; read the vertex, command and cull columns (exact, the
same on any machine) as the structural record, and the milliseconds as an order of magnitude.

**What one frame is.** Exactly the per-frame map path of `GameUi.DrawWorldLensAndAge`: `WorldLens.Paint` of the turn's
`WorldProjection` (the era's map ink and type, the window's automatic UI scale, the opening selection, territory on)
→ `DrawListImGuiBackend` replay with the off-screen cull ON (as in the game) into ImGui's background list →
`ImGui.Render`, with `ImGuiDrawData.Check` on every frame (a violation aborts the run; none occurred). NOT included:
the GPU terrain bake, rivers and grain (drawn by the MonoGame host, not measurable headless), the chrome, the Age
panel/flow/toast, and the per-turn projection build (second table).

**The sweep.** From the fit zoom to `Camera.MaxZoom` (32) in wheel notches (×1.25 at the window centre) through the
real `Camera` and its clamp; at every zoom stop a serpentine pan over a 4×4 grid of the world. Every band (World,
Regional, Settlement by `WorldLens.LevelFor`) must be crossed or the run fails. A row is one (window, world, band):
frames per sweep in that band, the median over 5 sweeps of the per-sweep mean ms/frame (after a 3 s run-wide warm-up
and 2 warm-up sweeps per configuration), the mean total vertices, the largest single draw list, the lens commands per
frame (mean and max), and the commands the replay cull dropped per frame.

**The worlds** (seed 42, canonical 1024-px size; founded and stepped by the real `UiSession`, in memory only, never
persisted; no row constructed):

- canonical: turn 150, 12 settlements, 1359 territory blocks, 287 dirt-path steps, 0 built routes, 1 formations
- high-count: turn 1, 400 settlements, 4978 territory blocks, 0 dirt-path steps, 0 built routes, 1 formations

The high-count world is the simulation's own founding with `--settlements 400` — the existing launch option, at about
the canonical siting capacity (median 457 over 5 seeds, `worldgen.json` `_docMinSpacing`) — so the gap the plan
recorded ("no fixture with a high settlement count exists") is closed without appending rows. It is measured at turn 1
(the first turn with catchment territory) because each of its turns costs tens of seconds to step; it therefore has no
dirt-path network (none exists at turn 1 on any world) — **DATA GAP: the Paths layer's cost at a high settlement count
is not measured.** The canonical world is measured at turn 150, when its 12 settlements have laid 287 dirt-path steps.
Neither world has built routes (no road was ordered); the road world of `RoadWorldFixture` is a 256-px test world and
was not used for timing.

**Command.**

```
dotnet build Sim.slnx -c Release
dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --frame-cost map            # defaults: --rounds 5 --settlements 400 --turns 150
```

### Lens frame cost per window × world × band (`86bd07f`)

| window | world | band | frames/sweep | median ms/frame | mean vertices | max list vertices | commands/frame | max commands | culled/frame |
|---|---|---|---|---|---|---|---|---|---|
| 1280x800 | canonical | World | 32 | 0.24 | 10517 | 11688 | 1658 | 1735 | 20.3 |
| 1280x800 | canonical | Regional | 96 | 0.11 | 3471 | 12610 | 996 | 2001 | 464.0 |
| 1280x800 | canonical | Settlement | 144 | 0.05 | 254 | 3030 | 584 | 664 | 571.5 |
| 1280x800 | high-count | World | 32 | 1.96 | 89094 | 98790 | 7058 | 7693 | 303.3 |
| 1280x800 | high-count | Regional | 96 | 0.64 | 27241 | 96916 | 2543 | 8061 | 546.7 |
| 1280x800 | high-count | Settlement | 144 | 0.10 | 4437 | 37100 | 115 | 732 | 42.5 |
| 1920x1009 | canonical | World | 48 | 0.34 | 10939 | 11688 | 1687 | 1735 | 12.7 |
| 1920x1009 | canonical | Regional | 96 | 0.12 | 3904 | 12610 | 1039 | 2001 | 439.7 |
| 1920x1009 | canonical | Settlement | 128 | 0.05 | 368 | 3682 | 586 | 664 | 568.4 |
| 1920x1009 | high-count | World | 48 | 2.03 | 93918 | 102500 | 7293 | 7783 | 173.7 |
| 1920x1009 | high-count | Regional | 96 | 0.73 | 32562 | 104138 | 2813 | 8136 | 456.5 |
| 1920x1009 | high-count | Settlement | 128 | 0.13 | 5872 | 40046 | 133 | 742 | 41.5 |
| 2560x1440 | canonical | World | 48 | 0.27 | 12118 | 12904 | 1693 | 1735 | 10.8 |
| 2560x1440 | canonical | Regional | 96 | 0.12 | 3835 | 12790 | 1021 | 2014 | 436.4 |
| 2560x1440 | canonical | Settlement | 112 | 0.05 | 430 | 3622 | 586 | 664 | 566.8 |
| 2560x1440 | high-count | World | 48 | 2.24 | 94653 | 102500 | 7301 | 7783 | 115.8 |
| 2560x1440 | high-count | Regional | 96 | 0.78 | 32161 | 105044 | 2610 | 8129 | 315.9 |
| 2560x1440 | high-count | Settlement | 112 | 0.15 | 6720 | 39314 | 128 | 690 | 30.3 |
| 3840x2160 | canonical | World | 48 | 0.29 | 13218 | 13984 | 1693 | 1735 | 10.7 |
| 3840x2160 | canonical | Regional | 96 | 0.13 | 3942 | 12918 | 1014 | 2014 | 429.5 |
| 3840x2160 | canonical | Settlement | 80 | 0.06 | 558 | 3668 | 587 | 664 | 563.3 |
| 3840x2160 | high-count | World | 48 | 2.23 | 94583 | 102500 | 7279 | 7783 | 95.4 |
| 3840x2160 | high-count | Regional | 96 | 0.78 | 33632 | 104890 | 2494 | 8073 | 204.2 |
| 3840x2160 | high-count | Settlement | 80 | 0.18 | 9270 | 46026 | 153 | 660 | 27.0 |

### Projection build per turn (`86bd07f`)

| world | turn | settlements | territory blocks | path steps | built routes | formations | projection build ms (median) | lattice build ms (median; no longer run by the projection) |
|---|---|---|---|---|---|---|---|---|
| canonical | 150 | 12 | 1359 | 287 | 0 | 1 | 1.07 | 3.89 |
| high-count | 1 | 400 | 4978 | 0 | 0 | 1 | 52.69 | 3.84 |

The last column is the full-terrain `TraversalLattice.Build` that `WorldProjection.Build` ran every turn before
`55b71b2` only to read the lattice's size and stride; it now takes both by arithmetic (`WorldProjection.LatticeGrid`).
On lighter-loaded runs of the same tool it read 2.1–2.6 ms; it is saved once per turn, not per frame.

### Reading (INFERRED from the tables above; nothing here is ruled)

- **Canonical world:** every band's lens frame is at most 0.34 ms here, at every window size, with at most 13,984
  vertices in one list.
- **High settlement count:** the World band costs about 2 ms per frame and puts 98,790–102,500 vertices into ONE draw
  list; the widest Regional views reach 105,044. Both are above the 16-bit 65,535 and carried by the `VtxOffset` path
  of M5 hardening H1 (no frame violated the draw-data contract). The vertices are not decomposed by layer here
  (INFERRED: the 4,978 territory blocks plus the 400 settlements' marks, names and banners). Regional costs about
  0.6–0.8 ms, Settlement 0.1–0.2 ms.
- **The Paths layer is emitted whole and culled at replay.** At the canonical world's Settlement band ~584 lens
  commands are emitted per frame and ~570 of them are dropped by the replay cull. `RoadLens.PaintNetwork` has no
  viewport test: it emits every dirt-path step (two strokes each at this band: 287 × 2 = 574) at every zoom — the
  audit's "not culled in Paint", now measured. It is cheap at 287 steps (0.05 ms); its growth with a large network is
  not measured (data gap above).
- **The projection build scales with the settlement count:** 1.07 ms at 12 settlements, 52.7 ms at 400 — once per turn,
  not per frame; its per-settlement scans of whole tables (buckets, housing, catchment nodes, network edges, notables,
  class states) are the likely cause (INFERRED; not profiled).
- **Warm-up artifact, fixed before this table:** on `13e3536` the first configuration measured (1280×800, canonical,
  World) read 1.00 ms against 0.25–0.27 ms at every other window; with `DOTNET_TieredCompilation=0` it read 0.15 ms. The
  tiered JIT had not promoted the paint path. `86bd07f` adds the run-wide warm-up; the first row now reads 0.24 ms.

### The stride change keeps the map byte-identical (`55b71b2`)

MEASURED: the digests of `Sim.Ui.Tests/ProjectionDigest.cs` — every projection field, and for the fit, 3× fit
(Regional), 10× fit (Settlement) and maximum-zoom cameras the SVG bytes' SHA-256 and the full `LensFrame` — over nine
worlds (seed 42 turns 0/1/4, seed 7 turns 1/4, seed 3 turn 1 at 1024 px; seed 42 at 250 px, not a multiple of the
stride, turns 1/4; the `RoadWorldFixture` world with 6 built routes and 2 corridor-covered steps) were computed on
`82bde01` with the old `Build` and after the change: the 54-line files are identical (SHA-256
`61b4ccd1a943163494a55aeed4a5e20f23862cd02a350394b40c7cc178cf5d42` both times). The standing pin is
`Sim.Ui.Tests/WorldProjectionStrideTests.cs`: the arithmetic grid equals the built lattice's geometry on every world,
the projection built on either draws identical SVG bytes and `LensFrame`s at every band, and a control shows a
geometry one px wider draws a different map.

## 2026-10-10 — the map rect's share of the window, before and after P1 (M5 map workstream P1; append-only, the sections above are unchanged)

**Status:** MEASURED by the P1 agent on branch `m5w-p1-map-viewport`: BEFORE on its base **`55640ae`**
(`m5w-integration`, Wave A integrated) and AFTER on **`f874b6d`**, 2026-10-10, through the live `GameUi` in the headless
frame harness (`UiFrameHarness`, real native ImGui, the window's automatic UI scale: 1.0 at 1280×800 and 1920×1009,
1.375 at 2560×1440), canonical founded world, seed 42, turn 1. Geometry, not timing: the numbers are exact on any
machine.

**What is measured.** The *map rect* is the rectangle the camera frames the map through — under the status band, above
the command bar, left of the context panel when it is shown (`PanelLayout.MapRect`). Before P1 there was no map rect:
the camera framed the whole window and the chrome lay over it, so the BEFORE column is the same rectangle computed from
the same placed chrome (P1 moves no chrome). *Unobstructed* subtracts what floats inside the rect: the selection card
(and, after P1, the folded column's tab). "+ H" is after the H key (every panel folded; a section stays open).

| window | state | map rect, before | map rect, after | unobstructed, before | unobstructed, after |
|---|---|---|---|---|---|
| 1280×800 | no section | 86.00 % | 86.00 % | 81.19 % | 81.19 % |
| 1280×800 | no section + H | — (no fold) | 86.00 % | — | 84.25 % |
| 1280×800 | a section open | 56.97 % | 56.97 % | 52.16 % | 52.16 % |
| 1280×800 | a section open + H | — (no fold) | **86.00 %** | — | **83.63 %** |
| 1920×1009 | no section | 88.90 % | 88.90 % | 86.36 % | 86.36 % |
| 1920×1009 | no section + H | — | 88.90 % | — | 87.97 % |
| 1920×1009 | a section open | 66.12 % | 66.12 % | 63.58 % | 63.58 % |
| 1920×1009 | a section open + H | — | **88.90 %** | — | **87.65 %** |
| 2560×1440 | no section | 89.31 % | 89.31 % | 86.82 % | 86.82 % |
| 2560×1440 | no section + H | — | 89.31 % | — | 88.42 % |
| 2560×1440 | a section open | 66.40 % | 66.40 % | 63.92 % | 63.92 % |
| 2560×1440 | a section open + H | — | **89.31 %** | — | **88.09 %** |

The selection card is 320×154 at scale 1 (440×208 at 1.375) and folds to 320×56 (440×74). The two bars are 112 px of
height at scale 1 (154 px at 1.375) and do not fold.

**The fit.** Before: the fit zoom fitted the WINDOW's shorter side (`min(w, h) / 1024`): 800 / 1009 / 1440 px of world,
of which 112 / 112 / 154 px of world height sat under the two bars, and at the clamp the world's edges were parked
under the status band, the command bar and an open panel (they could never be brought into the clear map). After: the
fit fits the MAP RECT's shorter side — 688 / 897 / 1286 px of world, the WHOLE world inside the clear map, nothing
under a bar (`CameraTests.MapRect_AtTheFit_NoWorldSitsUnderTheBars_AndTheWholeWorldIsInTheRect`, with the old camera as
its control) — and the clamp brings every world edge to the clear map's edge
(`CameraTests.MapRect_Clamp_KeepsTheRectOverTheWorld_AndEveryEdgeOfTheWorldReachesTheClearMap`). The fitted world is
therefore SMALLER on screen than the old fit's (688 against 800 px at 1280×800) — the old fit's extra 112 px were the
part under the bars.

**Reading (INFERRED from the table; nothing here is ruled).** The enlargement is the open-section case: folding the
column gives the map back the panel's 25–30 % of the window (52 → 84 % unobstructed at 1280×800) without closing the
section. With no section open the chrome already left 86–89 %, and folding the card adds 1.6–3.1 points. What is left
is structural: the two bars (14 % of a 1280×800 window) and, at the fit, the paper beside a square world in a wide rect
(296 px each side at 1280×800 — a contain fit). A cover or fit-to-width zoom floor would fill that paper but put world
outside the clear map at the floor, which the packet's "no world under the bars at fit" excludes; it is recorded in
`docs/queue.md`, not built.

## 2026-10-11 — map frame cost before / after the continuous level of detail (M5 map workstream P2; append-only, the sections above are unchanged)

**Status:** MEASURED by the P2 agent with P4's `Sim.Ui --frame-cost map` (defaults: `--rounds 5 --settlements 400
--turns 150`), Release. BEFORE: the base `9e066b2` (P3's tip), built in the P2 worktree before any edit, run
2026-10-11 01:44–01:45 UTC. AFTER: `4fc40ee` (P2's code), run 02:22–02:24 UTC. A measurement, not a budget (ADR-009).

**Load (read before the numbers).** The machine has 4 CPUs shared with other agents' builds and test runs; the load
average was 3.2 → 4.0 over the BEFORE run and 4.1 → 3.7 over the AFTER run (5–6 in the minutes around both). The
milliseconds are wall-clock under that load — an order of magnitude only. The vertex and command columns are exact
(the same on any machine) and are the structural record.

**What changed in the sweep.** The tool paints what the game now paints: `WorldLens.Paint` at the continuous
`MapDetail` of each camera (keyed on the visible km), its row's band the derived band. The sweep itself is unchanged
(fit → 32× in ×1.25 notches at the window centre, a 4×4 serpentine pan at each stop), and so are both worlds (the
world hashes before and after the sweeps are the same as P4's: canonical `4035cf18…`, high-count `127236…`). A new
last column counts the band's frames that fell inside a cross-fade: one zoom stop of the World band (16 frames) and two
of the Regional band (32) — the stops between 1894 and 2679 km and between 551 and 779 km; no Settlement stop lands in
the second window's Settlement half.

| window | world | band | frames/sweep | cross-fade frames | ms/frame before → after | mean vertices before → after | max list vertices before → after | commands/frame before → after | max commands before → after |
|---|---|---|---|---|---|---|---|---|---|
| 1280x800 | canonical | World | 32 | 16 | 0.25 → 0.32 | 10608 → 13820 | 11784 → 18948 | 1658 → 2024 | 1735 → 2467 |
| 1280x800 | canonical | Regional | 96 | 32 | 0.12 → 0.13 | 3493 → 4087 | 12692 → 17144 | 996 → 1048 | 2001 → 2313 |
| 1280x800 | canonical | Settlement | 144 | 0 | 0.05 → 0.05 | 256 → 256 | 3048 → 3048 | 584 → 584 | 664 → 664 |
| 1280x800 | high-count | World | 32 | 16 | 1.77 → 2.62 | 91240 → 119831 | 101060 → 159272 | 7058 → 8152 | 7693 → 9749 |
| 1280x800 | high-count | Regional | 96 | 32 | 0.63 → 0.97 | 27782 → 33456 | 98588 → 130640 | 2543 → 2660 | 8061 → 8808 |
| 1280x800 | high-count | Settlement | 144 | 0 | 0.11 → 0.10 | 4469 → 4469 | 37372 → 37372 | 115 → 115 | 732 → 732 |
| 1920x1009 | canonical | World | 48 | 16 | 0.25 → 0.42 | 11031 → 13194 | 11784 → 18948 | 1687 → 1931 | 1735 → 2467 |
| 1920x1009 | canonical | Regional | 96 | 32 | 0.12 → 0.16 | 3931 → 4675 | 12692 → 18312 | 1039 → 1091 | 2001 → 2313 |
| 1920x1009 | canonical | Settlement | 128 | 0 | 0.06 → 0.09 | 371 → 371 | 3700 → 3700 | 586 → 586 | 664 → 664 |
| 1920x1009 | high-count | World | 48 | 16 | 1.86 → 2.32 | 96164 → 115340 | 104924 → 159520 | 7293 → 8008 | 7783 → 9735 |
| 1920x1009 | high-count | Regional | 96 | 32 | 0.75 → 1.07 | 33202 → 39767 | 105952 → 140732 | 2813 → 2934 | 8136 → 8867 |
| 1920x1009 | high-count | Settlement | 128 | 0 | 0.13 → 0.12 | 5913 → 5913 | 40336 → 40336 | 133 → 133 | 742 → 742 |
| 2560x1440 | canonical | World | 48 | 16 | 0.28 → 0.31 | 12209 → 14407 | 13000 → 20164 | 1693 → 1937 | 1735 → 2467 |
| 2560x1440 | canonical | Regional | 96 | 32 | 0.13 → 0.14 | 3860 → 4727 | 12872 → 19556 | 1021 → 1073 | 2014 → 2326 |
| 2560x1440 | canonical | Settlement | 112 | 0 | 0.06 → 0.06 | 432 → 432 | 3640 → 3640 | 586 → 586 | 664 → 664 |
| 2560x1440 | high-count | World | 48 | 16 | 1.94 → 2.28 | 96895 → 116288 | 104924 → 159824 | 7301 → 7985 | 7783 → 9551 |
| 2560x1440 | high-count | Regional | 96 | 32 | 0.69 → 1.00 | 32769 → 39114 | 107020 → 141880 | 2610 → 2723 | 8129 → 8852 |
| 2560x1440 | high-count | Settlement | 112 | 0 | 0.14 → 0.13 | 6763 → 6763 | 39596 → 39596 | 128 → 128 | 690 → 690 |
| 3840x2160 | canonical | World | 48 | 16 | 0.28 → 0.33 | 13311 → 15503 | 14080 → 21244 | 1693 → 1937 | 1735 → 2467 |
| 3840x2160 | canonical | Regional | 96 | 32 | 0.13 → 0.15 | 3966 → 5080 | 13000 → 21892 | 1014 → 1065 | 2014 → 2326 |
| 3840x2160 | canonical | Settlement | 80 | 0 | 0.06 → 0.06 | 562 → 562 | 3688 → 3688 | 587 → 587 | 664 → 664 |
| 3840x2160 | high-count | World | 48 | 16 | 1.95 → 2.34 | 96824 → 116174 | 104924 → 159724 | 7279 → 7946 | 7783 → 9452 |
| 3840x2160 | high-count | Regional | 96 | 32 | 0.70 → 0.96 | 34229 → 40539 | 106772 → 141624 | 2494 → 2599 | 8073 → 8786 |
| 3840x2160 | high-count | Settlement | 80 | 0 | 0.18 → 0.17 | 9327 → 9327 | 46308 → 46308 | 153 → 153 | 660 → 660 |

**Reading (INFERRED from the table; nothing here is ruled).**
- **Every Settlement row is identical** in its vertices and commands, and the plateau frames of the other bands paint
  exactly what they painted (the plateau paint is the banded paint, command for command:
  `OnAPlateau_TheContinuousPaint_IsTheBandedPaint_CommandForCommand`). The whole difference is the cross-fade frames,
  which paint both bands' layers.
- **A cross-fade frame costs about 1.4–1.6× the plateau frames of its band.** Taking the plateau frames as unchanged,
  the World band's fading frames average 1658 + 2 × 366 = 2390 commands on the canonical world at 1280×800 (1658 for
  the band before), and 7058 + 2 × 1094 = 9246 on the high-count world, with a largest single list of 159,272–159,824
  vertices at a high settlement count (before: 101,060–104,924). That is above the 16-bit 65,535 like the World band
  already was, and is carried by M5 hardening H1's `VtxOffset` path: no frame violated the draw-data contract (a
  violation aborts the run).
- **Milliseconds:** canonical ≤ 0.42 ms per frame in any band (before ≤ 0.28); high-count World 2.3–2.6 ms (before
  1.8–1.9) and Regional 1.0–1.1 ms (before 0.6–0.8), the increase confined to the cross-fade stops. Under the load
  above, read as an order of magnitude.
- An eased zoom crosses a window in a fraction of a second (the ease is 90 % done in 0.16 s), so a player sees the
  heavier frames only while gliding through 2^±¼ of an edge; a camera that comes to rest inside a window keeps paying
  them (both layer sets) until it moves.

**Smoke gate on the same code (`4fc40ee`).** `dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --smoke --monkey 200`:
9 states, 1892 checks: 1782 pass, 110 not offered, 0 FAIL; 0 problems; 62,426 frames, 1,800 monkey actions; largest
draw list 104,004 vertices; 124.8 s. Against P3's record (1892 / 1782 / 61,527 frames) the checks and passes are the
same; the +899 frames are INFERRED to be the restated wheel rows letting the ease settle (not decomposed).

## 2026-10-11 — map frame cost with the resource overlay (M5 map workstream P9; append-only, the sections above are unchanged)

**Status:** MEASURED by the P9 agent with P4's `Sim.Ui --frame-cost map` (defaults: `--rounds 5 --settlements 400
--turns 150`), Release, the P9 branch at `5d7abc7` (the tool's one-layer variant; every other Sim.Ui file as at the
smoke's `e788b36`), run 14:40–14:42 UTC. A measurement, not a budget (ADR-009).

**Load (read before the numbers).** 4 CPUs shared with other agents; load average 5.6 → 6.0 over the run. The
milliseconds are wall-clock under that load — an order of magnitude only. The command and vertex columns are exact.

**What was swept.** P4's two worlds unchanged (world hashes `4035cf18…` canonical and `127236…` high-count, the same as
P4's and P2's: painting changed neither), plus the canonical world swept twice more through the same `WorldLens.Paint`
with the overlay the game paints: every one of its six layers shown at the default opacity 0.5 with its key (the worst
case — the layers paint over each other), and the settlement-suitability layer alone with its key (the common case).
Without the overlay the lens paints exactly what it painted (canonical World 1280x800: 2024 commands and 13820 mean
vertices, P2's after-figures).

| window | band | canonical ms / commands / mean vertices | + one layer (suitability) | + every layer (6) |
|---|---|---|---|---|
| 1280x800 | World | 0.31 / 2024 / 13820 | 1.65 / 8492 / 41992 | 12.01 / 42236 / 181015 |
| 1280x800 | Regional | 0.13 / 1048 / 4087 | 0.52 / 2752 / 13202 | 4.42 / 11672 / 52374 |
| 1280x800 | Settlement | 0.05 / 584 / 256 | 0.15 / 702 / 3028 | 0.46 / 1204 / 8005 |
| 1920x1009 | World | 0.29 / 1931 / 13194 | 2.10 / 8810 / 43012 | 13.56 / 44835 / 191145 |
| 1920x1009 | Regional | 0.14 / 1091 / 4675 | 0.98 / 3090 / 14970 | 4.72 / 13585 / 60872 |
| 1920x1009 | Settlement | 0.05 / 586 / 371 | 0.24 / 733 / 3256 | 0.41 / 1363 / 9094 |
| 2560x1440 | World | 0.44 / 1937 / 14407 | 2.45 / 8876 / 44459 | 17.00 / 45146 / 193577 |
| 2560x1440 | Regional | 0.23 / 1073 / 4727 | 0.80 / 3028 / 14850 | 5.09 / 13290 / 59815 |
| 2560x1440 | Settlement | 0.07 / 586 / 432 | 0.18 / 744 / 3363 | 0.64 / 1431 / 9431 |
| 3840x2160 | World | 0.44 / 1937 / 15503 | 1.83 / 8876 / 45555 | 14.84 / 45146 / 194673 |
| 3840x2160 | Regional | 0.20 / 1065 / 5080 | 0.91 / 3021 / 15203 | 5.42 / 13282 / 60169 |
| 3840x2160 | Settlement | 0.08 / 587 / 562 | 0.17 / 783 / 3646 | 0.63 / 1653 / 10467 |

The overlay is one rect per run of equal ramp levels along each lattice row (16 levels): ~6,500 rects a layer at World
zoom on the canonical map (22,937 known land nodes), fewer closer in as runs leave the viewport. Merging equal runs
across rows was measured and cuts only 15–25 % of them (6,313 boxes for 7,535 runs on the suitability layer), so it was
not taken. The bake (once per session while the knowledge is the same) and the per-turn status (access isochrones,
catchments, rolls; once per turn while a layer is shown):

| world | bake ms (median) | status ms (median) | nodes | known land nodes | layers |
|---|---|---|---|---|---|
| canonical | 49.8 | 7.6 | 65536 | 22937 | 6 |
| high-count | 58.6 | 49.9 | 65536 | 22937 | 6 |

## 2026-10-11 — map frame cost with the 'now usable' outline (M5 map workstream P10; append-only, the sections above are unchanged)

**Status:** MEASURED by the P10 agent with P4's `Sim.Ui --frame-cost map` (defaults: `--rounds 5 --settlements 400
--turns 150`), Release, the P10 branch at `cec0b63`, run 16:56–16:58 UTC; load average 0.77 → 0.05 over the run (the
machine was otherwise quiet). A measurement, not a budget (ADR-009).

**What was swept.** P9's four sweeps unchanged (canonical, canonical + every overlay layer, canonical + the suitability
layer alone, high-count; world hashes `4035cf18…` and `127236…`, the same as P4's, P2's and P9's), plus one: the
region-fertility layer alone with a 'now usable' shortcut's outline of its qualifying places and five shortcut rows
opening the key — the farming shortcut's view. The outline is one line per boundary run between qualifying and
non-qualifying lattice nodes: on the canonical map (all known) the region-fertility layer qualifies 22,243 of the 22,937
known land nodes in 1,024 segments; each deposit channel qualifies all 22,937 in 1,024 segments (the coast at the
lattice's 16 km); the suitability and water-access layers (not opened by any shortcut today) 10,412 / 2,746 and
10,831 / 2,800.

| window | band | + one layer (suitability), P9's | + one layer (region fertility) + outline |
|---|---|---|---|
| 1280x800 | World | 1.60 ms / 8492 commands / 41992 mean vertices | 2.07 / 9508 / 46711 |
| 1280x800 | Regional | 0.66 / 2752 / 13202 | 0.77 / 3010 / 14892 |
| 1280x800 | Settlement | 0.14 / 702 / 3028 | 0.18 / 716 / 3740 |
| 1920x1009 | World | 2.63 / 8810 / 43012 | 2.87 / 9884 / 47964 |
| 2560x1440 | World | 2.44 / 8876 / 44459 | 2.98 / 9949 / 49409 |
| 3840x2160 | World | 2.19 / 8876 / 45555 | 2.69 / 9949 / 50505 |

The two columns are different layers (their fills differ by a few dozen runs), so the difference is an upper bound on
the outline's cost: at most ~1,070 commands and ~0.5 ms a frame at World zoom, under 0.1 ms closer in. The outline is
built once per bake and layer (the bake is immutable) and costs nothing while no shortcut is shown.

## 2026-10-11 — map frame cost before / after stable placement and the compressive size class (M5 map workstream P12a; append-only, the sections above are unchanged)

`dotnet Sim.Ui.dll --frame-cost map --rounds 3`, Release, the same two worlds as above (canonical seed 42 at turn 150;
400 settlements at turn 1). Before = `b49ae20` (`m5w-integration`), after = `a20af42`
(`m5w-p12a-settlement-placement-stability`), run one after the other on the same 4-CPU container (load 2.6–5.2). Both
runs: every world's hash unchanged by every sweep (canonical `4035cf18…`, high-count `12723634…`).

| window | world | band | frames/sweep | cross-fade frames | median ms/frame | mean vertices | max list vertices | commands/frame | max commands |
|---|---|---|---|---|---|---|---|---|---|
| 1280x800 | canonical | World | 32 | 16 | 0.32 → 0.33 | 13820 → 14075 | 18948 → 19508 | 2024 → 2024 | 2467 → 2467 |
| 1280x800 | canonical | Regional | 96 | 32 | 0.13 → 0.15 | 4087 → 4280 | 17144 → 17752 | 1048 → 1048 | 2313 → 2313 |
| 1280x800 | canonical | Settlement | 144 | 0 | 0.05 → 0.06 | 256 → 280 | 3048 → 3624 | 584 → 584 | 664 → 664 |
| 1280x800 | high-count | World | 32 | 16 | 2.65 → 2.69 | 119831 → 125075 | 159272 → 173876 | 8152 → 8149 | 9749 → 9742 |
| 1280x800 | high-count | Regional | 96 | 32 | 0.88 → 0.98 | 33456 → 37495 | 130640 → 143120 | 2660 → 2658 | 8808 → 8801 |
| 1280x800 | high-count | Settlement | 144 | 0 | 0.12 → 0.11 | 4469 → 5156 | 37372 → 43984 | 115 → 115 | 732 → 732 |
| 1920x1009 | canonical | World | 48 | 16 | 0.29 → 0.30 | 13194 → 13359 | 18948 → 19508 | 1931 → 1931 | 2467 → 2467 |
| 1920x1009 | canonical | Regional | 96 | 32 | 0.14 → 0.14 | 4675 → 4882 | 18312 → 18920 | 1091 → 1091 | 2313 → 2313 |
| 1920x1009 | canonical | Settlement | 128 | 0 | 0.05 → 0.05 | 371 → 394 | 3700 → 4128 | 586 → 586 | 664 → 664 |
| 1920x1009 | high-count | World | 48 | 16 | 3.02 → 3.22 | 115340 → 117708 | 159520 → 174108 | 8008 → 8006 | 9735 → 9728 |
| 1920x1009 | high-count | Regional | 96 | 32 | 1.09 → 1.13 | 39767 → 44443 | 140732 → 154524 | 2934 → 2931 | 8867 → 8860 |
| 1920x1009 | high-count | Settlement | 128 | 0 | 0.16 → 0.13 | 5913 → 6629 | 40336 → 47132 | 133 → 132 | 742 → 742 |
| 2560x1440 | canonical | World | 48 | 16 | 0.57 → 0.36 | 14407 → 14571 | 20164 → 20724 | 1937 → 1937 | 2467 → 2467 |
| 2560x1440 | canonical | Regional | 96 | 32 | 0.24 → 0.15 | 4727 → 4877 | 19556 → 20068 | 1073 → 1073 | 2326 → 2326 |
| 2560x1440 | canonical | Settlement | 112 | 0 | 0.09 → 0.06 | 432 → 431 | 3640 → 3632 | 586 → 586 | 664 → 664 |
| 2560x1440 | high-count | World | 48 | 16 | 2.61 → 2.62 | 116288 → 118650 | 159824 → 174416 | 7985 → 7983 | 9551 → 9544 |
| 2560x1440 | high-count | Regional | 96 | 32 | 1.14 → 1.02 | 39114 → 42943 | 141880 → 155288 | 2723 → 2721 | 8852 → 8845 |
| 2560x1440 | high-count | Settlement | 112 | 0 | 0.14 → 0.17 | 6763 → 7238 | 39596 → 46208 | 128 → 128 | 690 → 690 |
| 3840x2160 | canonical | World | 48 | 16 | 0.32 → 0.35 | 15503 → 15667 | 21244 → 21804 | 1937 → 1937 | 2467 → 2467 |
| 3840x2160 | canonical | Regional | 96 | 32 | 0.14 → 0.15 | 5080 → 5165 | 21892 → 22276 | 1065 → 1065 | 2326 → 2326 |
| 3840x2160 | canonical | Settlement | 80 | 0 | 0.06 → 0.06 | 562 → 561 | 3688 → 3680 | 587 → 587 | 664 → 664 |
| 3840x2160 | high-count | World | 48 | 16 | 2.45 → 3.03 | 116174 → 118467 | 159724 → 174168 | 7946 → 7944 | 9452 → 9445 |
| 3840x2160 | high-count | Regional | 96 | 32 | 1.01 → 1.00 | 40539 → 44069 | 141624 → 154636 | 2599 → 2597 | 8786 → 8779 |
| 3840x2160 | high-count | Settlement | 80 | 0 | 0.18 → 0.18 | 9327 → 9503 | 46308 → 47272 | 153 → 153 | 660 → 658 |

**Reading (INFERRED from the table; nothing here is ruled).**
- **Commands per frame are unchanged on the canonical world** in every band and window: its settlements' dwellings / 25
  block counts are the counts the old ring drew (below its cap of 48), and each part is still one glyph.
- **The 400-settlement world draws up to 7 fewer commands per frame**: at turn 1 some settlements have fewer than 25
  dwellings, and the composition no longer draws the one block the old ring invented for them.
- **Vertices rise by up to 18 %** (mean and largest list; e.g. high-count Settlement at 1280×800: mean 4,469 → 5,156, largest 37,372 → 43,984): blocks and glyphs are sized by
  the zoom's px per lattice unit, so at close zoom they are larger and the anti-aliased outlines longer. The largest
  single list is 174,416 vertices (before 159,824), above the 16-bit limit as before and carried by H1's `VtxOffset`.
- **Milliseconds** move both ways by up to ±0.6 ms between the two runs at the same command counts — run-to-run load, not
  a measured cost of the change. The composition is computed once per settlement view and turn (memoised by the
  projection), so a paint only scales it.

## 2026-10-11 — map frame cost before / after the settlement cull follows what a settlement paints (M5 map workstream P12a review fix; append-only, the sections above are unchanged)

`dotnet Sim.Ui.dll --frame-cost map --rounds 3`, Release, the same two worlds. Before = `00e7217` (P12a as reviewed: a
fixed 200 px margin about each settlement's centre), after = the fix on `m5w-p12a-settlement-placement-stability`
(`WorldLens.Reaches`). Run alternately on the same 4-CPU container — before, after, before, after (load 6.1–6.9) — and
both milliseconds of each side are given; every count column was identical between the two runs of a side. Every world's
hash unchanged by every sweep (canonical `4035cf18…`, high-count `12723634…`).

| window | world | band | median ms/frame (before ×2 → after ×2) | commands/frame | max commands | culled/frame (replay) | mean vertices | max list vertices |
|---|---|---|---|---|---|---|---|---|
| 1280x800 | canonical | World | 0.32, 0.43 → 0.34, 0.33 | 2024 → 2016 | 2467 → 2467 | 60.4 → 51.9 | 14076 → 14076 | 19508 → 19508 |
| 1280x800 | canonical | Regional | 0.14, 0.14 → 0.14, 0.14 | 1048 → 1027 | 2313 → 2295 | 481.1 → 460.0 | 4281 → 4281 | 17752 → 17752 |
| 1280x800 | canonical | Settlement | 0.05, 0.06 → 0.06, 0.06 | 584 → 586 | 664 → 708 | 571.5 → 573.1 | 280 → 280 | 3624 → 3624 |
| 1280x800 | high-count | World | 2.78, 3.13 → 2.57, 2.91 | 8149 → 7792 | 9742 → 9326 | 487.5 → 130.4 | 125076 → 125076 | 173876 → 173876 |
| 1280x800 | high-count | Regional | 0.93, 1.23 → 1.05, 1.12 | 2658 → 2267 | 8801 → 8281 | 575.1 → 182.4 | 37495 → 37495 | 143100 → 143100 |
| 1280x800 | high-count | Settlement | 0.11, 0.21 → 0.47, 0.39 | 115 → 117 | 732 → 797 | 42.1 → 41.9 | 5155 → 5155 | 43984 → 43984 |
| 1920x1009 | canonical | World | 0.45, 0.31 → 0.30, 0.31 | 1931 → 1925 | 2467 → 2467 | 37.1 → 31.4 | 13359 → 13359 | 19508 → 19508 |
| 1920x1009 | canonical | Regional | 0.21, 0.15 → 0.14, 0.14 | 1091 → 1075 | 2313 → 2295 | 456.5 → 440.5 | 4882 → 4882 | 18920 → 18920 |
| 1920x1009 | canonical | Settlement | 0.09, 0.05 → 0.06, 0.08 | 586 → 588 | 664 → 708 | 568.5 → 569.8 | 394 → 394 | 4132 → 4132 |
| 1920x1009 | high-count | World | 2.53, 2.57 → 2.35, 2.30 | 8006 → 7794 | 9728 → 9283 | 277.4 → 66.1 | 117709 → 117709 | 174104 → 174104 |
| 1920x1009 | high-count | Regional | 1.19, 1.32 → 1.18, 1.05 | 2931 → 2614 | 8860 → 8551 | 475.5 → 156.9 | 44443 → 44443 | 154524 → 154524 |
| 1920x1009 | high-count | Settlement | 0.13, 0.19 → 0.30, 0.34 | 132 → 131 | 742 → 836 | 41.1 → 37.7 | 6628 → 6628 | 47132 → 47132 |
| 2560x1440 | canonical | World | 0.50, 0.34 → 0.30, 0.32 | 1937 → 1932 | 2467 → 2467 | 30.9 → 25.3 | 14571 → 14571 | 20724 → 20724 |
| 2560x1440 | canonical | Regional | 0.25, 0.15 → 0.14, 0.14 | 1073 → 1060 | 2326 → 2308 | 453.3 → 441.1 | 4877 → 4877 | 20068 → 20068 |
| 2560x1440 | canonical | Settlement | 0.09, 0.06 → 0.06, 0.07 | 586 → 589 | 664 → 708 | 567.0 → 569.3 | 431 → 435 | 3632 → 3632 |
| 2560x1440 | high-count | World | 2.62, 2.92 → 2.33, 2.46 | 7983 → 7855 | 9544 → 9290 | 181.2 → 53.3 | 118650 → 118650 | 174412 → 174412 |
| 2560x1440 | high-count | Regional | 1.07, 1.46 → 1.05, 1.14 | 2721 → 2520 | 8845 → 8552 | 333.0 → 129.8 | 42943 → 42946 | 155280 → 155280 |
| 2560x1440 | high-count | Settlement | 0.14, 0.25 → 0.32, 0.33 | 128 → 143 | 690 → 783 | 30.1 → 41.7 | 7238 → 7260 | 46208 → 46208 |
| 3840x2160 | canonical | World | 0.33, 0.34 → 0.32, 0.34 | 1937 → 1932 | 2467 → 2467 | 31.5 → 25.8 | 15667 → 15667 | 21804 → 21804 |
| 3840x2160 | canonical | Regional | 0.14, 0.15 → 0.14, 0.18 | 1065 → 1058 | 2326 → 2308 | 446.5 → 439.2 | 5165 → 5165 | 22276 → 22276 |
| 3840x2160 | canonical | Settlement | 0.06, 0.06 → 0.07, 0.07 | 587 → 591 | 664 → 708 | 563.4 → 566.5 | 561 → 566 | 3680 → 3680 |
| 3840x2160 | high-count | World | 2.42, 2.51 → 2.39, 3.20 | 7944 → 7848 | 9445 → 9290 | 145.9 → 49.4 | 118467 → 118467 | 174164 → 174164 |
| 3840x2160 | high-count | Regional | 0.98, 1.08 → 1.03, 1.20 | 2597 → 2503 | 8779 → 8501 | 215.4 → 119.3 | 44070 → 44095 | 154636 → 154636 |
| 3840x2160 | high-count | Settlement | 0.17, 0.19 → 0.35, 0.65 | 153 → 181 | 658 → 783 | 26.7 → 50.1 | 9503 → 9575 | 47272 → 47272 |

**Reading (the counts MEASURED; the causes INFERRED from the change, not decomposed per frame).**
- **World and Regional bands: fewer commands** (high-count World at 1280×800 8,149 → 7,792; Regional 2,658 → 2,267) and
  as many fewer culled at replay (487.5 → 130.4): the 200 px margin painted every settlement within 200 px of the
  viewport whether or not anything it paints reached it; the fix paints only those whose commands can.
- **Settlement band: more commands where the fix draws what was popping out** (high-count at 3840×2160 153 → 181;
  canonical max commands 664 → 708): settlements whose footprint, key or lines reach the viewport from beyond 200 px are
  now drawn. Mean vertices move by at most +72 and the largest list not at all.
- **Settlement band milliseconds rise on the 400-settlement world** (0.11–0.25 → 0.30–0.65 ms median): a settlement whose
  marks miss the viewport but whose band of labels overlaps its rows has its labels laid out to be tested (strings
  formatted, the name measured). The other bands and the canonical world are within the run-to-run noise (the two runs
  of one side differ by up to 0.81 ms, high-count World at 3840×2160 after; 0.39 ms before). The cheap vertical test first culls every
  settlement whose labels lie wholly above or below the viewport; without it (the first measured version of the fix,
  one run) this band cost 0.65–0.77 ms.

## 2026-10-14 — map frame cost before / after the cylinder's camera and copies (batch 3 WS3, rendering stage W2; append-only, the sections above are unchanged)

`dotnet Sim.Ui.dll --frame-cost map --rounds 3 --settlements 360`, Release, on a shared 4-CPU container (load average
5.6–6.1 at the start of each run). Before = the batch-3 W1 tree (the cylindrical world, the FLAT camera and lens: the
x clamp, the shorter-side fit, one copy), after = W2 (`MapFrameCost` now sweeps with the game's wrapped camera — its floor
one circumference across the window, x normalised — keys the level of detail on the window's width and paints with the
lens's copies, `WorldLens.Paint(…, wrapX: true)`). Run alternately: before, after, before, after; both milliseconds of each
side are given; every count column was identical between the two runs of a side. The same worlds both sides (every
world's hash unchanged by every sweep and equal before and after: canonical `906a332f…`, high-count `f796ab1b…`).

**`--settlements 360`, not the default 400: on the cylindrical seed-42 world the founding places only 362 sites at the
siting spacing** (MEASURED: the W1 tree's `--frame-cost map` with the default aborts "settlement siting could only place
362 of 400 sites at minSpacingKm 95.2"). The default was left unchanged by the W2 stage (a W1 consequence); **W2 review F3
then set `MapFrameCost.DefaultSettlements` to 360** (MEASURED capacity: `--settlements 2000` → "could only place 362 of 2000
sites"; the default run `--frame-cost map --rounds 1` now exits 0 with "high-count: turn 1, 360 settlements" and the same
world hashes this section records, canonical `906a332f…` and high-count `f796ab1b…`), and a `--settlements` past the capacity is refused as a usage error (exit 2, one line
naming the argument and the capacity, before the canonical world is stepped) instead of an unhandled exception (exit 134).

| window | world | band | frames/sweep | median ms/frame (before ×2 → after ×2) | commands/frame | max commands | culled/frame (replay) | mean vertices | max list vertices |
|---|---|---|---|---|---|---|---|---|---|
| 1280x800 | canonical | World | 32 → 48 | 0.29, 0.33 → 0.38, 0.40 | 2091 → 1388 | 2550 → 2193 | 47.0 → 276.7 | 14544 → 7981 | 20104 → 17472 |
| 1280x800 | canonical | Regional | 96 → 96 | 0.15, 0.16 → 0.21, 0.21 | 1066 → 791 | 2360 → 2054 | 481.2 → 609.7 | 4396 → 1386 | 17960 → 15596 |
| 1280x800 | canonical | Settlement | 144 → 112 | 0.06, 0.06 → 0.11, 0.13 | 616 → 613 | 759 → 663 | 604.9 → 606.2 | 247 → 128 | 4584 → 3808 |
| 1280x800 | canonical + overlay | World | 32 → 48 | 10.18, 10.32 → 7.96, 6.61 | 38857 → 24280 | 44893 → 42274 | 48.5 → 278.4 | 167927 → 105042 | 188592 → 179384 |
| 1280x800 | canonical + overlay | Regional | 96 → 96 | 2.47, 2.65 → 2.86, 2.43 | 11497 → 4365 | 37049 → 19689 | 483.1 → 611.2 | 51888 → 21104 | 164012 → 93008 |
| 1280x800 | canonical + overlay | Settlement | 144 → 112 | 1.19, 1.55 → 2.09, 2.25 | 1220 → 1011 | 4890 → 2829 | 606.0 → 607.3 | 7926 → 6965 | 25800 → 18276 |
| 1280x800 | high-count | World | 32 → 48 | 2.94, 2.75 → 2.69, 1.51 | 7125 → 4323 | 8603 → 7257 | 108.3 → 207.8 | 110019 → 57966 | 153916 → 84720 |
| 1280x800 | high-count | Regional | 96 → 96 | 1.17, 1.12 → 1.39, 0.83 | 2199 → 761 | 7550 → 3425 | 160.7 → 69.3 | 35724 → 12659 | 124256 → 58488 |
| 1280x800 | high-count | Settlement | 144 → 112 | 0.33, 0.31 → 0.59, 0.33 | 101 → 66 | 691 → 543 | 35.9 → 26.5 | 4516 → 2778 | 44768 → 19164 |
| 1920x1009 | canonical | World | 48 → 48 | 0.42, 0.42 → 0.36, 0.22 | 1992 → 1166 | 2550 → 2073 | 29.7 → 334.0 | 13767 → 6498 | 20104 → 16744 |
| 1920x1009 | canonical | Regional | 96 → 96 | 0.19, 0.23 → 0.22, 0.13 | 1115 → 783 | 2360 → 2020 | 455.6 → 612.4 | 5053 → 1357 | 19096 → 16960 |
| 1920x1009 | canonical | Settlement | 128 → 80 | 0.08, 0.10 → 0.13, 0.08 | 618 → 613 | 759 → 663 | 602.1 → 605.5 | 335 → 151 | 4568 → 3848 |
| 1920x1009 | canonical + overlay | World | 48 → 48 | 12.75, 11.89 → 6.63, 6.52 | 41940 → 19998 | 47347 → 38690 | 29.9 → 334.5 | 179857 → 87559 | 198740 → 165840 |
| 1920x1009 | canonical + overlay | Regional | 96 → 96 | 2.42, 3.38 → 2.17, 1.98 | 13217 → 3871 | 37715 → 17144 | 456.3 → 612.7 | 59666 → 19452 | 168144 → 84676 |
| 1920x1009 | canonical + overlay | Settlement | 128 → 80 | 1.31, 1.37 → 0.49, 0.75 | 1374 → 1045 | 5286 → 2707 | 602.2 → 605.6 | 8970 → 7460 | 28976 → 18204 |
| 1920x1009 | high-count | World | 48 → 48 | 1.65, 2.73 → 2.29, 2.26 | 7226 → 3471 | 8603 → 6518 | 59.8 → 133.8 | 104720 → 46786 | 154092 → 74956 |
| 1920x1009 | high-count | Regional | 96 → 96 | 1.20, 1.51 → 0.98, 0.97 | 2524 → 633 | 7782 → 2965 | 140.4 → 45.5 | 41872 → 10891 | 132848 → 52948 |
| 1920x1009 | high-count | Settlement | 128 → 80 | 0.21, 0.34 → 0.56, 0.52 | 116 → 66 | 716 → 447 | 35.8 → 25.0 | 5749 → 3077 | 47400 → 16680 |
| 2560x1440 | canonical | World | 48 → 48 | 0.29, 0.40 → 0.29, 0.40 | 1998 → 1239 | 2550 → 2095 | 25.5 → 317.7 | 15030 → 7891 | 21344 → 19372 |
| 2560x1440 | canonical | Regional | 96 → 96 | 0.14, 0.14 → 0.20, 0.22 | 1103 → 784 | 2367 → 2020 | 459.1 → 610.8 | 5076 → 1435 | 20276 → 17992 |
| 2560x1440 | canonical | Settlement | 112 → 64 | 0.06, 0.06 → 0.13, 0.14 | 618 → 613 | 759 → 663 | 601.6 → 604.1 | 375 → 205 | 4512 → 3864 |
| 2560x1440 | canonical + overlay | World | 48 → 48 | 9.74, 11.50 → 7.42, 7.43 | 42257 → 21602 | 47347 → 40135 | 25.6 → 317.9 | 182384 → 95116 | 199916 → 173024 |
| 2560x1440 | canonical + overlay | Regional | 96 → 96 | 4.58, 3.93 → 1.81, 1.63 | 12957 → 4046 | 38137 → 17954 | 459.3 → 611.0 | 58699 → 20226 | 170992 → 88948 |
| 2560x1440 | canonical + overlay | Settlement | 112 → 64 | 0.50, 0.54 → 2.52, 3.61 | 1442 → 1117 | 5289 → 2707 | 601.7 → 604.2 | 9287 → 7808 | 28876 → 18220 |
| 2560x1440 | high-count | World | 48 → 48 | 1.72, 2.33 → 1.87, 2.25 | 7283 → 3737 | 8608 → 6823 | 55.5 → 135.8 | 105453 → 50162 | 154380 → 78512 |
| 2560x1440 | high-count | Regional | 96 → 96 | 1.02, 1.11 → 0.95, 0.94 | 2442 → 665 | 7776 → 3056 | 119.3 → 43.8 | 40494 → 12102 | 133636 → 54956 |
| 2560x1440 | high-count | Settlement | 112 → 64 | 0.22, 0.22 → 0.63, 0.63 | 126 → 80 | 703 → 474 | 36.3 → 26.7 | 6473 → 4003 | 46908 → 18220 |
| 3840x2160 | canonical | World | 48 → 48 | 0.28, 0.34 → 0.44, 0.43 | 1998 → 1238 | 2550 → 2095 | 25.9 → 317.3 | 16187 → 9055 | 22520 → 21348 |
| 3840x2160 | canonical | Regional | 96 → 96 | 0.13, 0.17 → 0.23, 0.23 | 1102 → 784 | 2367 → 2020 | 458.9 → 610.9 | 5384 → 1615 | 22532 → 21752 |
| 3840x2160 | canonical | Settlement | 80 → 32 | 0.06, 0.07 → 0.14, 0.14 | 621 → 614 | 759 → 663 | 599.9 → 602.5 | 484 → 271 | 4584 → 4008 |
| 3840x2160 | canonical + overlay | World | 48 → 48 | 9.82, 10.68 → 6.81, 7.67 | 42257 → 21600 | 47347 → 40135 | 26.0 → 317.4 | 183542 → 96280 | 201156 → 175360 |
| 3840x2160 | canonical + overlay | Regional | 96 → 96 | 4.24, 4.33 → 2.90, 2.72 | 12956 → 4046 | 38137 → 17954 | 459.2 → 611.0 | 59009 → 20405 | 173248 → 92708 |
| 3840x2160 | canonical + overlay | Settlement | 80 → 32 | 0.54, 0.65 → 1.39, 1.29 | 1652 → 1282 | 5289 → 2707 | 599.9 → 602.5 | 10240 → 8541 | 28940 → 18364 |
| 3840x2160 | high-count | World | 48 → 48 | 2.52, 1.74 → 1.38, 1.50 | 7271 → 3719 | 8608 → 6781 | 48.4 → 126.4 | 105279 → 49880 | 154068 → 78000 |
| 3840x2160 | high-count | Regional | 96 → 96 | 1.29, 1.01 → 0.58, 0.58 | 2425 → 661 | 7702 → 3045 | 107.9 → 41.5 | 41525 → 13162 | 133524 → 54532 |
| 3840x2160 | high-count | Settlement | 80 → 32 | 0.47, 0.25 → 0.41, 0.41 | 158 → 112 | 716 → 474 | 43.4 → 37.1 | 8349 → 5543 | 47712 → 19376 |

**Reading (counts MEASURED; causes INFERRED from the change).**
- **The sweep starts at the cylinder's floor** (one circumference across the window: ×1.25 at 1280×800, ×1.875 at
  1920×1009, ×2.5 at 2560×1440, ×3.75 at 3840×2160) instead of the flat fit (the whole world in the map rect's shorter
  side), so fewer of each world's elements are in view at the World band: commands and vertices per frame FALL at every
  window and band (canonical World at 1280×800 2,091 → 1,388 commands; high-count World 7,125 → 4,323). The wrapped
  keying (visible km across the window's width) moves the band edges to deeper zooms, so the Settlement band takes fewer
  notches of the sweep (144 → 112 frames per sweep at 1280×800; 80 → 32 at 3840×2160) and the World band more (32 → 48 at
  1280×800).
- **Culled per frame rises at the World band** (canonical 47.0 → 276.7 at 1280×800): at the floor the window spans one
  circumference across but only the map rect's height over the zoom down it (688 / 1.25 ≈ 550 of the world's 1,024 px
  at 1280×800), where the flat fit showed the whole world, so more of the world's elements lie off the window and are
  culled.
- **The copies themselves are cheap**: at the floor only the elements cut by the window's edges are drawn twice (the
  lens's `LensFrame.Copies`; the unit tests pin it per element).
- **Milliseconds** move both ways by up to a few ms between runs of one side on this loaded container (canonical +
  overlay Settlement at 2560×1440: 2.52 / 3.61 after against 0.50 / 0.54 before at fewer commands — run-to-run load, not a
  measured cost of the change). It is a measurement, not a budget (ADR-009).

### Moved P2 pins and gate wheel rows (W2 review F4; MEASURED 2026-10-14)

The design spec asks this stage to "record the moved P2 pins and gate wheel rows". Nothing here is re-pinned: the old
assertions stand for the FLAT camera, which flat worlds still use, and the cylinder's behaviour is pinned by new tests.

**Two live-session P2 pins (`MapViewportTests.MapViewportUiTests`), now founded flat (`wrapXOverride: false`).** With the
switch off they pass with their assertions unchanged (the attribution control). Against the shipped world (the cylinder;
the two calls changed back to `UiSession.Start(42)` in a scratch edit, built and run, then restored) both fail:
- `LevelOfDetail_IsTheMapRectsVisibleSpan_NotTheWindows`: "Assert.Equal() Failure: Values differ / Expected: 1 /
  Actual: 1.25". The game's camera now opens at the cylinder's floor, 1280 / 1024 = 1.25 at 1280×800 (was the flat fit
  1.0), and the band is keyed on visible km across the WINDOW's width (`MapDetail.Across`; was the map rect's visible span).
  Now pinned by `CylinderSessionTests.TheGame_OpensAtTheFloor_CentredOnTheOpeningSettlement` and the
  `CylinderCameraTests` LOD-band theory.
- `OpeningThePanel_ReframesWithoutMovingTheWorldPointUnderTheCursor_AndAFitViewReFits`: "the fitted world
  363.125,-30.875..1643.125,1249.125 is not inside the map rect RectD { X = 0, Y = 52, W = 848, H = 688 … }". A view at
  the floor no longer re-fits to the narrower rect when a panel opens: the zoom is unchanged (no world point twice, no
  re-zoom). Now pinned by `CylinderSessionTests.AtTheFloor_OpeningAndClosingAPanel_ChangesNeitherZoomNorBandNorLayers`.

**The gate's wheel rows** (`PlayabilityGate` "wheel zoom to Regional / to Settlement / out to World"; each notches at the map
rect's centre until the TARGET view's band is the one wanted). Notch counts were measured by a temporary instrumentation
of `WheelUntil` (a line per row, not in any patch), run through `--smoke --monkey 0` on the W1 tree (cylindrical world,
flat camera: before) and the W2 tree (wrapped camera, `MapDetail.Across` keying: after). Both smokes: "12 states, 2896
checks: 2747 pass, 149 not offered, 0 FAIL; 0 problems". Every row passes before and after. What moved is the zooms they
start and stop at and, in some rows, the notch count:

| state | # | row (target band) | map rect | before: notches (zoom from -> to) | after: notches (zoom from -> to) |
|---|---|---|---|---|---|
| A1 turn 1 | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| A1 turn 1 | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| A1 turn 1 | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| one AI | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| one AI | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| one AI | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| wide window | 1 | Regional | 1920x897 | 3 (1.0000 -> 1.9531) | 3 (1.8750 -> 3.6621) |
| wide window | 2 | Settlement | 1920x897 | 5 (1.9531 -> 5.9605) | 6 (3.6621 -> 13.9698) |
| wide window | 3 | World | 1920x897 | 6 (5.9605 -> 1.5625) | 7 (13.9698 -> 2.9297) |
| narrow window | 1 | Regional | 1080x528 | 0 (1.0000 -> 1.0000) | 3 (1.0547 -> 2.0599) |
| narrow window | 2 | Settlement | 1080x528 | 6 (1.0000 -> 3.8147) | 6 (2.0599 -> 7.8580) |
| narrow window | 3 | World | 1080x528 | 7 (3.8147 -> 0.8000) | 7 (7.8580 -> 1.6479) |
| target set | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| target set | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| target set | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| research done | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| research done | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| research done | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| research done: now usable | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| research done: now usable | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| research done: now usable | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| tax available | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| tax available | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| tax available | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| age advance | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| age advance | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| age advance | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| colony+revolt | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| colony+revolt | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| colony+revolt | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| placement | 1 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 3 (1.2500 -> 2.4414) |
| placement | 2 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| placement | 3 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| hidden rival | 1 | Settlement | 1280x688 | 7 (1.0000 -> 4.7684) | 9 (1.2500 -> 9.3132) |
| hidden rival | 2 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| hidden rival | 3 | Regional | 1280x688 | 1 (1.0000 -> 1.2500) | 1 (1.9531 -> 2.4414) |
| hidden rival | 4 | Settlement | 1280x688 | 6 (1.2500 -> 4.7684) | 6 (2.4414 -> 9.3132) |
| hidden rival | 5 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |
| hidden rival | 6 | Settlement | 1280x688 | 8 (1.0000 -> 4.7684) | 8 (1.9531 -> 9.3132) |
| hidden rival | 7 | World | 1280x688 | 7 (4.7684 -> 1.0000) | 7 (9.3132 -> 1.9531) |

Reading (MEASURED from the table):
- The rows now start at the cylinder's floor: 1.25 at 1280×800, 1.875 at 1920×1009, 1.0547 at 1080×640. Before, they started at 1.0.
- "To Regional" takes 3 notches in every state, where it took 1 at 1280×800 (3 in the wide window, which is unchanged; 0
  in the narrow window, whose flat opening was already Regional). The one exception is hidden rival row 3, which takes 1,
  as before, because it starts from the World row's stop. (Corrected 2026-10-15, final-round fix 2, verifier finding
  REC-WHEEL-WIDE: this line first said "where it took 1" for every state but the narrow window; the table's wide-window
  row took 3 before as well.)
- "To Settlement" ends at 9.3132 at 1280×800 (was 4.7684), 13.9698 at 1920×1009 (was 5.9605) and 7.8580 at 1080×640 (was
  3.8147). It takes 6 notches from Regional (5 → 6 in the wide window) and 9 from the floor (hidden rival row 1, was 7).
- "Out to World" stops at 1.9531 at 1280×800, the first zoom whose window width shows more than the World band's edge
  (1280 / 1.9531 × 4 km = 2621 km > 2253). It does not stop at the floor.

The deviation behind these rows (the band keyed on the WINDOW's width, not the map rect's width the spec names) is a
Director question for D-054. The measured reason is in `CylinderCameraTests`: keyed on the shown-panel map rect, the
256-px floor is on no plateau at any gate window.
