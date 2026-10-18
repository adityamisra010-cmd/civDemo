#!/usr/bin/env python3
"""Generates a sample order-log binary for the determinism-xproc CI job.

Writes the OrderLog format documented in Sim.Core/Kernel/OrderLog.cs:
  magic "CIVORDR\0" | int32 version=1 | int32 count |
  records: int64 turn, int32 actorId, int32 kind, int32 targetId, float64 amount (raw bits)
Little-endian throughout.

Default (toy runs): SetRainBias (kind=1) on alternating regions every 25th turn.
--labor (T1.6, founded runs: sim run --founded): LaborAllocation (kind=2) on
settlement 0, farm percentage sweeping 30/50/70 every 20th turn.
--founded-mix (M5 integration, founded runs with --ai-empires 1): the PLAYER Empire
(polity 1, which rules settlement 0) issues SectorAllocation (kind 3) on settlement 0
sweeping the construction weight every 20th turn, SetResearchTarget (kind 6) on
knapping_oldowan (key 2) at turn 0 and a clear (-1) at turn 150, and
EnqueueConstruction (kind 4) of a granary (project 1) at settlement 0 at turn 10.
D-054 WS1: a SECOND and a THIRD granary at turn 10 (they queue behind the first,
slots 1 and 2), a CancelConstruction (kind 11) of slot 2 at turn 11, and a
CancelConstruction of a slot that does not exist (slot 7) at turn 12 -- a well-formed
order the delivery-time check ignores. The first granary completes in step 10->11
(measured on the seed-42 world), so at the turn-11 cancel's delivery slot 1 is the head
and slot 2 is a genuine NON-HEAD entry (a cancel of slot 1 would have removed the head).
D-054 WS2: SetResourceAllocation (kind 12) by the player for itself (TargetId 1):
building 30 shares in 100 at turn 40, 70 at turn 200 (when the run reaches them) --
the standing split is upserted and recomputed every step from then on.
The AI Empire's orders (kinds 4-9, and 11 for its structurally stalled heads) are
produced in-process and written by --emit-session; replay reads that run log.
--found (M5 S1b, ADR-036; founded runs: sim run --founded --seed 42): two
FoundSettlement orders (kind 9) by the player (polity 1). Turn 2: from settlement 0
to cell 349391. Turn 5: from settlement 12 -- the colony the first order founds in
state 3, a source ABOVE the turn-0 maximum (the ADR-034 deferral) -- to cell 335063.
Both sites are the nearest LEGAL sites to their source settlement (squared px on the
cylinder, then cell id) by FoundingQuery.Check at their turns on the seed-42 founded
world; Sim.Tests FoundSettlementCiLegTests re-derives them and fails if these literals
drift from what the simulation accepts. Batch 3 WS3 (D-054): re-derived on the
cylindrical world (worldgen.json topology.wrapX) -- the pre-cylinder literals 330985
and 322801 are PoorSite there.
--allocation (batch 4, D-055 / ADR-040: the scenario S42-ALLOC-H; founded runs with no
AI Empire: sim run --founded --seed 42, the player rules all 12 settlements). The
building/roads split (kind 12) made to BIND, across processes and a save/load:
(1) the player researches the built road (kind 6: cordage, shelter_hut, knapping,
ground_stone, timber_frame, track_road, stone_dry -- stone_dry completes in step
115->116); (2) at 116 it sets b = 30 and orders DevelopRoads (kind 8) EVERY turn from
116 (100 % to 125, then 1 %, one route at a time), and at 117 queues three granaries at
settlements 0 and 2 -- the competing-heads binding window 117..125; (3) the hold
window in the 1 % road phase: settlement 4 gets few builders (kind 3, construction
weight 0.01, at 125), a granary there at 126 (labour-paced: part-built, progress not a
round number), and NO builders at 127 (weight 0) -- an OLDER part-built head that sets
aside what it still needs; granaries at settlements 0 and 2 at 128 wait for it
(HeldForEarlier); at 132 it is cancelled (kind 11) and the same project re-enqueued
(kind 4) in one batch; its builders return at 134; (4) at 136, in ONE batch, b = 70
and three granaries at settlements 0, 2 and 4 -- the new share binds in its own
delivery step (T3 round 2: building work draws 70 % of the stone, its twin without
the order 30 %).
--no-split drops every kind 12 (the b = 100 control); --drop TURN:KIND drops that
turn's orders of that kind (the turn-exact twins). Sim.Tests AllocationCiLegTests pins
these literals, re-measures every premise of the scenario on the same founded world
and pins the sha256 of the logs the ci.yml step checks.
"""
import struct
import sys

argv = sys.argv[1:]
drop = None
if "--drop" in argv:  # --drop TURN:KIND (batch 4: the --allocation twins)
    i = argv.index("--drop")
    drop = tuple(int(x) for x in argv[i + 1].split(":"))
    argv = argv[:i] + argv[i + 2:]
args = [a for a in argv if a not in ("--labor", "--founded-mix", "--found", "--allocation", "--no-split")]
allocation = "--allocation" in sys.argv
labor = "--labor" in sys.argv
mix = "--founded-mix" in sys.argv
found = "--found" in sys.argv
path = args[0] if len(args) > 0 else "sample-orders.bin"
max_turn = int(args[1]) if len(args) > 1 else 400

if allocation:
    # ALLOCATION_ORDERS (S42-ALLOC-H) -- keep in step with Sim.Tests/Kernel/AllocationScenario.cs
    records = [(0, 1, 6, 12, 0.0), (32, 1, 6, 17, 0.0), (39, 1, 6, 2, 0.0), (53, 1, 6, 8, 0.0),
               (66, 1, 6, 52, 0.0), (86, 1, 6, 74, 0.0), (99, 1, 6, 53, 0.0), (116, 1, 12, 1, 30.0)]
    records += [(117, 1, 4, 0, 1.0)] * 3 + [(117, 1, 4, 2, 1.0)] * 3
    records += [(125, 1, 3, (4 << 3) | 4, 0.01), (126, 1, 4, 4, 1.0), (127, 1, 3, (4 << 3) | 4, 0.0)]
    records += [(128, 1, 4, 0, 1.0)] * 2 + [(128, 1, 4, 2, 1.0)] * 2
    records += [(132, 1, 11, 4, 0.0), (132, 1, 4, 4, 1.0), (134, 1, 3, (4 << 3) | 4, 10.0), (136, 1, 12, 1, 70.0)]
    records += [(136, 1, 4, 0, 1.0), (136, 1, 4, 2, 1.0), (136, 1, 4, 4, 1.0)]
    records += [(turn, 1, 8, 0, 100.0 if turn < 126 else 1.0) for turn in range(116, max_turn)]
    records = [r for r in records if r[0] < max_turn]
    records.sort(key=lambda r: r[0])  # stable: same-turn rows keep their order
elif found:
    # FOUND_SETTLEMENT_ORDERS -- keep in step with Sim.Tests/Kernel/FoundSettlementCiLegTests.cs
    records = [(2, 1, 9, 0, 349391.0), (5, 1, 9, 12, 335063.0)]
elif mix:
    records = [(0, 1, 6, 2, 0.0)]
    for turn in range(0, max_turn, 20):
        records.append((turn, 1, 3, (0 << 3) | 4, [10.0, 30.0, 20.0][(turn // 20) % 3]))
        if turn <= 10 < turn + 20:
            records.append((10, 1, 4, 0, 1.0))
            records.append((10, 1, 4, 0, 1.0))    # D-054 WS1: a second granary, slot 1
            records.append((10, 1, 4, 0, 1.0))    # and a third, slot 2
            records.append((11, 1, 11, 0, 2.0))   # kind 11: cancel slot 2 -- non-head at delivery (slot 1 is head then)
            records.append((12, 1, 11, 0, 7.0))   # kind 11: no slot 7 -- ignored at delivery
        if turn <= 150 < turn + 20:
            records.append((150, 1, 6, -1, 0.0))
    for turn, share in ((40, 30.0), (200, 70.0)):  # D-054 WS2: kind 12, the player's own split
        if turn < max_turn:
            records.append((turn, 1, 12, 1, share))
    records.sort(key=lambda r: r[0])  # stable: same-turn rows keep their order
elif labor:
    records = [(turn, 1, 2, 0, [30.0, 50.0, 70.0][(turn // 20) % 3])
               for turn in range(0, max_turn, 20)]
else:
    records = [(turn, 1, 1, (turn // 25) % 2, 250.0 + turn)
               for turn in range(0, max_turn, 25)]

if "--no-split" in sys.argv:
    records = [r for r in records if r[2] != 12]
if drop is not None:
    records = [r for r in records if (r[0], r[2]) != drop]

with open(path, "wb") as f:
    f.write(b"CIVORDR\0")
    f.write(struct.pack("<i", 1))
    f.write(struct.pack("<i", len(records)))
    for turn, actor, kind, target, amount in records:
        f.write(struct.pack("<qiiid", turn, actor, kind, target, amount))

print(f"wrote {len(records)} orders to {path}")
