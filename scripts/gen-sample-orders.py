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
The AI Empire's orders (kinds 4-8) are produced in-process and written by
--emit-session; replay reads that run log.
"""
import struct
import sys

args = [a for a in sys.argv[1:] if a not in ("--labor", "--founded-mix")]
labor = "--labor" in sys.argv
mix = "--founded-mix" in sys.argv
path = args[0] if len(args) > 0 else "sample-orders.bin"
max_turn = int(args[1]) if len(args) > 1 else 400

if mix:
    records = [(0, 1, 6, 2, 0.0)]
    for turn in range(0, max_turn, 20):
        records.append((turn, 1, 3, (0 << 3) | 4, [10.0, 30.0, 20.0][(turn // 20) % 3]))
        if turn <= 10 < turn + 20:
            records.append((10, 1, 4, 0, 1.0))
        if turn <= 150 < turn + 20:
            records.append((150, 1, 6, -1, 0.0))
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
