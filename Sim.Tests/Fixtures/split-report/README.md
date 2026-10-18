# The split-report fixtures (D-055 / ADR-040, batch 4 item 3)

`a.jsonl` and `control.jsonl` are REAL split reports, written by `sim run --split-report` (schema `split-report/v1`,
`Sim.Core/Observability/SplitProbe.cs`). They are the input of `Sim.Tests/Observability/SplitReportCheckTests.cs`: the
real pair must pass every property of `sim split-check` (`SplitReportCheck`), and one mutant report per property,
derived from them in the test, must fail it. They are DATA for the checker's tests: no test re-runs the simulation to
compare against them, so a later change to the simulation does not invalidate them (the live CLI path is covered by
`RunFromSaveCliTests` and `SplitProbeTests`).

## The build that wrote them

Batch 4, track T3 step 1 (2026-10-17): base-b3 (de86ace + `b3-final-v9`) + the frozen T1a patch `t1a-v4.patch`
(sha256 `68167754b9638ee52186de065b7d944eae88f78801cd5a53debb53f161eb8897`) + the T3 step-1 delta, `Sim.Cli` Release,
.NET 10.0.112.

## The commands

`orders.bin` holds the player's 42 records below; `orders-control.bin` is the same log without the kind-12 record (so the
control runs at the default split, b = 100).

    sim run --founded --seed 42 --turns 116 --orders orders.bin --save-at 116 --save s116.civsnap
    sim run --from-save s116.civsnap --turns 24 --orders orders.bin --split-report a.jsonl
    sim run --from-save s116.civsnap --turns 24 --orders orders-control.bin --split-report control.jsonl
    sim split-check a.jsonl --control control.jsonl --expect-hold-at 120        # PASS

The first run prints `run complete: seed 42, 116 turns, hash b64c4f52f49dca2517cda3ec7fe6da143f1af1ca9425e2cba69b97946fa7db4c`;
the two continuations end on `dac3fb26152dbd7db38cbfb57b7904836c1b65ab3be58eb2c042035a073971e5` (the split run) and
`fdb9a89f6fc4c6340f9398edc305baeb1c15dc50454088b314fc2d4f65ebf567` (the control). Re-run with the final T3 step-1 build, the
three commands reproduced both reports byte for byte.

All records are by the player (polity 1). The world has no AI Empire (worldgen.json `aiEmpires` 0), so the player rules
all twelve settlements.

| turn | kind | target | amount | meaning |
|---|---|---|---|---|
| 0, 32, 39, 53, 66, 86, 99 | 6 SetResearchTarget | 12, 17, 2, 8, 52, 74, 53 | 0 | cordage, shelter_hut, knapping_oldowan, ground_stone_early, timber_frame, track_road, stone_dry (both known by turn 116: in step 116 -> 117 road works draw the purse's 4,913 stone) |
| 116 | 12 SetResourceAllocation | 1 | 30 | building 30 shares in 100 (absent from `orders-control.bin`) |
| 116-125 | 8 DevelopRoads | 0 | 100 | every turn: road works ask for the whole purse's stone |
| 117 | 4 EnqueueConstruction | 0, 0, 0, 2, 2, 2 | 1 | three granaries at settlement 0, three at settlement 2 |
| 118 | 3 SectorAllocation | (0 << 3) \| 4 | 0 | settlement 0's construction share to 0: from step 119 -> 120 its head has no builders |
| 124 | 3 SectorAllocation | (0 << 3) \| 4 | 10 | settlement 0's builders back |
| 126-139 | 8 DevelopRoads | 0 | 1 | one route at a time |
| 130 | 4 EnqueueConstruction | 0, 2 | 1 | one more granary at each |

## What the reports show (`sim split-check`)

- Binding steps (build + holds = G_b < D_b, stone): 117, 118, 119, 121, 122, 123, 125, 126.
- Road works' unused share used by building work: 130. Building work's unused share used by road works: 116, 124, 127-139.
- Competing heads: 117 (s0:0 served, s2:0 waited), 119 (s0:1 set aside, s2:0 waited), 126 (s0:2 served, s2:2 waited).
- Held for an earlier head (the D-055 hold): in step 119 -> 120 settlement 0's promoted head (slot 1, no builders) sets
  aside all 19 stone of the grant, and settlement 2's part-built head (850,000) waits HeldForEarlier naming it although it
  needed 3; again in 121, 122 and 123 (s2:1, holder s0:1). World 120 therefore holds a set-aside that binds a younger
  head (`--expect-hold-at 120`).
- The control (b = 100) claims 40 stone at the first binding step 117 -> 118, where the split run claims 19.

## Files

| file | sha256 |
|---|---|
| `a.jsonl` | `57b6de5a42a35e921dec3a765a6c64e3b2bcae6f5554653ab473d12796e202d2` |
| `control.jsonl` | `f656253590bf270e45d98e414487680691ef47d27debb7314ec752259e769603` |
| `orders.bin` | `1eee23f8745e055c644f3d509a8a83d25587bcfdc61ffc619b6bcf48d1ebe9f4` |
| `orders-control.bin` | `b15054eba88aed582b434a63b03b5f384610504e32425ed1ee7bbbf9dae19abb` |

## `k.jsonl` and `k-no12.jsonl` (T3 round 2, 2026-10-17)

The ci.yml step "Allocation split, binding, cross-process (D-055)" (scenario S42-ALLOC-H, `gen-sample-orders.py
--allocation`) as it writes them: `k.jsonl` is its report B (`alloc-b.rep`: the continuation from save_K, world 130, to
140 — A's last ten rows byte for byte) and `k-no12.jsonl` the report of its kind-12 twin (`alloc-t12.rep`: the same
continuation for seven steps with the b = 70 order stamped 136 dropped). In the step from 136 the b = 70 order shares its
batch with three granaries (settlements 0, 2 and 4), so building work draws 51 of 73 stone (70 %) in `k.jsonl` and 21
(30 %) in the twin. They are the input of `Sim.Tests/Observability/SplitReportRederivationTests.cs` (P11, P12, PT).

    python3 scripts/gen-sample-orders.py alloc.bin 140 --allocation
    python3 scripts/gen-sample-orders.py alloc-no12.bin 140 --allocation --drop 136:12
    sim run --founded --seed 42 --turns 116 --orders alloc.bin --save-at 116 --save alloc-116.civsnap
    sim run --from-save alloc-116.civsnap --turns 24 --orders alloc.bin --save-at 130 --save alloc-130.civsnap
    sim run --from-save alloc-130.civsnap --turns 10 --orders alloc.bin --split-report k.jsonl
    sim run --from-save alloc-130.civsnap --turns 7 --orders alloc-no12.bin --split-report k-no12.jsonl
    sim split-check k.jsonl --expect-hold-at 131 --twin k-no12.jsonl --order-at 136:70     # PASS

Build: base-b3 + T1a v4 + the T3 round-2 delta (`t3-v3.patch`), `Sim.Cli` Release, .NET 10.0.112; the ci.yml step run
locally with the final build reproduces both files byte for byte.

| file | sha256 |
|---|---|
| `k.jsonl` | `89f8207676a83908de85bb1ccba878538cc5125af71de313516c087c4a2f7bee` |
| `k-no12.jsonl` | `8825fcb0ae5d61b991f31143efac504ef252d4a3f405c86ea513ad029b659959` |
