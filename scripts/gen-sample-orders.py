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
"""
import struct
import sys

args = [a for a in sys.argv[1:] if a not in ("--labor", "--founded-mix", "--found")]
labor = "--labor" in sys.argv
mix = "--founded-mix" in sys.argv
found = "--found" in sys.argv
path = args[0] if len(args) > 0 else "sample-orders.bin"
max_turn = int(args[1]) if len(args) > 1 else 400

if found:
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

with open(path, "wb") as f:
    f.write(b"CIVORDR\0")
    f.write(struct.pack("<i", 1))
    f.write(struct.pack("<i", len(records)))
    for turn, actor, kind, target, amount in records:
        f.write(struct.pack("<qiiid", turn, actor, kind, target, amount))

print(f"wrote {len(records)} orders to {path}")
