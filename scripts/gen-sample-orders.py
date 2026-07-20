#!/usr/bin/env python3
"""Generates a sample order-log binary for the determinism-xproc CI job.

Writes the OrderLog format documented in Sim.Core/Kernel/OrderLog.cs:
  magic "CIVORDR\0" | int32 version=1 | int32 count |
  records: int64 turn, int32 actorId, int32 kind, int32 targetId, float64 amount (raw bits)
Little-endian throughout. SetRainBias (kind=1) on alternating regions every 25th turn.
"""
import struct
import sys

path = sys.argv[1] if len(sys.argv) > 1 else "sample-orders.bin"
max_turn = int(sys.argv[2]) if len(sys.argv) > 2 else 400

records = [(turn, 1, 1, (turn // 25) % 2, 250.0 + turn)
           for turn in range(0, max_turn, 25)]

with open(path, "wb") as f:
    f.write(b"CIVORDR\0")
    f.write(struct.pack("<i", 1))
    f.write(struct.pack("<i", len(records)))
    for turn, actor, kind, target, amount in records:
        f.write(struct.pack("<qiiid", turn, actor, kind, target, amount))

print(f"wrote {len(records)} orders to {path}")
