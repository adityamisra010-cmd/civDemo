#!/usr/bin/env python3
"""Independent oracle for ez-tree's random-number generator (src/lib/rng.js, MIT, Daniel Greenheck).

Re-implements the JavaScript semantics from the ECMAScript rules, without reference to the C# port:
  - `x & 0xffffffff` and `x | 0` are ToInt32 (wrap modulo 2^32 into the signed range);
  - `x >> 16` is an arithmetic shift of the ToInt32 value; `x << 16` shifts the ToInt32 value and wraps to int32;
  - number arithmetic is IEEE-754 double (Python float), integer products are exact before the wrap;
  - `x >>> 0` is ToUint32.
Writes one line per (seed, index): seed, index, raw uint32 (decimal), the double from random() as 16 hex digits of its
IEEE-754 bits, and random(max, min) for (max, min) = (0.3, -0.3) as 16 hex digits. Usage:
  scripts/ez-rng-oracle.py > fixtures/ez-rng-oracle.txt
"""
import struct
import sys

MASK32 = 0xFFFFFFFF


def to_int32(v):
    v &= MASK32
    return v - (1 << 32) if v >= (1 << 31) else v


def to_uint32(v):
    return v & MASK32


class Rng:
    def __init__(self, seed):
        self.m_w = to_int32(123456789 + seed)
        self.m_z = to_int32(987654321 - seed)

    def raw(self):
        # Python's >> on negative ints is an arithmetic shift, as JavaScript's >> on int32
        self.m_z = to_int32(36969 * (self.m_z & 65535) + (self.m_z >> 16))
        self.m_w = to_int32(18000 * (self.m_w & 65535) + (self.m_w >> 16))
        shifted = to_int32(self.m_z << 16)
        return to_uint32(shifted + (self.m_w & 65535))

    def random(self, mx=1.0, mn=0.0):
        r = self.raw() / 4294967296.0
        return (mx - mn) * r + mn


def hexd(x):
    return struct.pack('>d', x).hex()


SEEDS = [0, 1, 42, 12345, 23399, 29919, 18020, 44166, 45590, -7, -123456789, 987654321, 2147483647, -2147483648, 4294967296 + 5]


def main(out):
    out.write('# ez-tree RNG oracle (scripts/ez-rng-oracle.py): seed index raw_u32 random_bits random(0.3,-0.3)_bits\n')
    for seed in SEEDS:
        a = Rng(seed)
        b = Rng(seed)
        for i in range(128):
            raw = a.raw()
            r = raw / 4294967296.0
            s = b.random(0.3, -0.3)
            out.write(f'{seed} {i} {raw} {hexd(r)} {hexd(s)}\n')


if __name__ == '__main__':
    main(sys.stdout)
