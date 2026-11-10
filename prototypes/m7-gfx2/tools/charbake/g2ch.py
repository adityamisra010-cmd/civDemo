"""Writer for the G2CH character container (read by Gfx.Chars/CharacterFile.cs).

Little-endian. "G2CH" | u32 version (1) | u32 chunk count | chunks. A chunk is a 4-byte ASCII tag, a u32 payload
length and the payload. Strings are u16 length + UTF-8. Metadata is canonical JSON (sorted keys, no spaces, floats
written with repr), so a re-bake is byte-identical.

Chunks:
  INFO  JSON object (identity, kind, provenance, materials, LOD stats ...)
  SKEL  u32 n | n x (string name, i32 parent, f32x3 head, f32x3 tail)            metres, +Y up, +Z forward
  SOCK  u32 n | n x (string name, i32 bone, f32x3 position, f32x4 rotation w,x,y,z)   bone-local (rest frame)
  MESH  string name | string material | u32 flags | u32 nv | u32 ni | arrays:
        pos f32x3, nrm f32x3, tan f32x4, uv f32x2, joints u8x4, weights u8x4 (sum 255), color u8x4 | indices u32
  MRPH  string mesh | u32 n | n x (string name, u32 count, u32 idx[count], f32x3 dpos[count], f32x3 dnrm[count])
  VIEW  string mesh | string view | u32 ni | indices u32   (an outfit's subset of an identity mesh: hidden skin, hair)
  CLIP  see clips.py
"""
import json
import struct

import numpy as np

VERSION = 1


def s16(s):
    b = s.encode("utf-8")
    return struct.pack("<H", len(b)) + b


def canon_json(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


class Writer:
    def __init__(self):
        self.chunks = []

    def chunk(self, tag, payload):
        assert len(tag) == 4
        self.chunks.append(tag.encode("ascii") + struct.pack("<I", len(payload)) + payload)

    def info(self, obj):
        self.chunk("INFO", canon_json(obj).encode("utf-8"))

    def skeleton(self, names, parents, heads, tails):
        p = [struct.pack("<I", len(names))]
        for n, par, h, t in zip(names, parents, heads, tails):
            p.append(s16(n) + struct.pack("<i", par) + np.asarray(h, "<f4").tobytes() + np.asarray(t, "<f4").tobytes())
        self.chunk("SKEL", b"".join(p))

    def sockets(self, socks):
        p = [struct.pack("<I", len(socks))]
        for name, bone, pos, quat in socks:
            p.append(s16(name) + struct.pack("<i", bone) + np.asarray(pos, "<f4").tobytes() + np.asarray(quat, "<f4").tobytes())
        self.chunk("SOCK", b"".join(p))

    def mesh(self, m):
        nv = len(m["pos"])
        idx = np.asarray(m["idx"], "<u4")
        assert idx.max(initial=0) < nv or nv == 0
        col = m.get("color")
        if col is None:
            col = np.full((nv, 4), 255, np.uint8)
        parts = [s16(m["name"]), s16(m["material"]), struct.pack("<III", m.get("flags", 0), nv, len(idx)),
                 np.asarray(m["pos"], "<f4").tobytes(), np.asarray(m["nrm"], "<f4").tobytes(),
                 np.asarray(m["tan"], "<f4").tobytes(), np.asarray(m["uv"], "<f4").tobytes(),
                 np.asarray(m["joints"], np.uint8).tobytes(), np.asarray(m["weights"], np.uint8).tobytes(),
                 np.asarray(col, np.uint8).tobytes(), idx.tobytes()]
        for a, w in (("pos", 3), ("nrm", 3), ("tan", 4), ("uv", 2), ("joints", 4), ("weights", 4)):
            assert np.asarray(m[a]).shape == (nv, w), (m["name"], a, np.asarray(m[a]).shape)
        self.chunk("MESH", b"".join(parts))

    def morphs(self, mesh_name, morphs):
        p = [s16(mesh_name), struct.pack("<I", len(morphs))]
        for name, idx, dpos, dnrm in morphs:
            idx = np.asarray(idx, "<u4")
            p.append(s16(name) + struct.pack("<I", len(idx)) + idx.tobytes() + np.asarray(dpos, "<f4").tobytes()
                     + np.asarray(dnrm, "<f4").tobytes())
        self.chunk("MRPH", b"".join(p))

    def view(self, mesh_name, view_name, idx):
        idx = np.asarray(idx, "<u4")
        self.chunk("VIEW", s16(mesh_name) + s16(view_name) + struct.pack("<I", len(idx)) + idx.tobytes())

    def raw(self, tag, payload):
        self.chunk(tag, payload)

    def bytes(self):
        return b"G2CH" + struct.pack("<II", VERSION, len(self.chunks)) + b"".join(self.chunks)
