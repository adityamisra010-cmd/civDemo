#!/usr/bin/env python3
"""Generates a sample order-log binary for the determinism-xproc CI job.

Writes the OrderLog format documented in Sim.Core/Kernel/OrderLog.cs:
  magic "CIVORDR\0" | int32 version=1 | int32 count |
  records: int64 turn, int32 actorId, int32 kind, int32 targetId, float64 amount (raw bits)
Little-endian throughout.

Default (toy runs): SetRainBias (kind=1) on alternating regions every 25th turn.
--labor (T1.6, founded runs: sim run --founded): LaborAllocation (kind=2) on
settlement 0, farm percentage sweeping 30/50/70 every 20th turn.
"""
import struct
import sys

args = [a for a in sys.argv[1:] if a != "--labor"]
labor = "--labor" in sys.argv
path = args[0] if len(args) > 0 else "sample-orders.bin"
max_turn = int(args[1]) if len(args) > 1 else 400

if labor:
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
