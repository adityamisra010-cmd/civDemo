"""Crowd LOD3 bake (stage render2): a third, coarser level of detail for the instanced crowd, made from each soldier
outfit's baked LOD2 mesh (stage chars, tools/charbake/lod.py) with meshoptimizer v1.3 (MIT; the library that
scripts/bake-chars.sh builds from the pinned sources, loaded through charbake's lod.simplify: a bake tool only).

Input:  $G2_ASSETS/baked/chars/<identity>__<outfit>.g2ch (its "lod2" mesh, about 1,500 triangles, one material "lod":
        vertex colour = albedo, alpha = surface code; skin weights for the 53+2-bone game-engine rig).
Output: $G2_ASSETS/baked/crowd/<identity>__<outfit>__lod3.g2ch with one MESH "lod3" (same vertex layout, about 520
        triangles) and an INFO chunk naming the source file and its sha256; MANIFEST.tsv lists every output with its
        sha256. --check re-bakes in memory and compares byte for byte (exit 1 on a difference).

How: the LOD2 mesh is split into connected components (body, garments, helmet, shield, spear ...). Each component gets
a triangle budget proportional to its surface area, but never less than a floor (so thin parts such as spear shafts
and shield rims keep their silhouette instead of collapsing), and is simplified on its own with
meshopt_simplifyWithAttributes (positions + normals, absolute error 25 mm, retried at 2.5x and 6x). Unused vertices
are dropped; every kept vertex keeps its attributes (position, normal, tangent, uv, joints, weights, colour) unchanged.
Deterministic: single-threaded, no randomness, canonical JSON metadata.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "charbake"))

import argparse  # noqa: E402
import hashlib  # noqa: E402
import struct  # noqa: E402

import numpy as np  # noqa: E402

import g2ch  # noqa: E402
import lod  # noqa: E402

SOLDIERS = [("a1", "kit_a"), ("a2", "kit_a"), ("a3", "kit_a"), ("a4", "kit_a"), ("commander_a", "kit_a"),
            ("b1", "kit_b"), ("b2", "kit_b"), ("b3", "kit_b"), ("b4", "kit_b")]
TARGET_TRIS = 520
MIN_TRIS = 6
ERROR_M = 0.025


def read_meshes(path):
    """MESH chunks of a G2CH file as dicts of numpy arrays (layout of tools/charbake/g2ch.py)."""
    b = open(path, "rb").read()
    magic, ver, n = struct.unpack_from("<4sII", b, 0)
    assert magic == b"G2CH" and ver == g2ch.VERSION, path
    p = 12
    out = {}
    for _ in range(n):
        tag = b[p:p + 4].decode("ascii")
        ln = struct.unpack_from("<I", b, p + 4)[0]
        o = p + 8
        p = o + ln
        if tag != "MESH":
            continue
        q = o

        def s():
            nonlocal q
            k = struct.unpack_from("<H", b, q)[0]
            v = b[q + 2:q + 2 + k].decode("utf-8")
            q += 2 + k
            return v

        def arr(dt, count, width):
            nonlocal q
            a = np.frombuffer(b, dt, count * width, q).reshape(count, width) if width > 1 else np.frombuffer(b, dt, count, q)
            q += a.nbytes
            return a.copy()

        name = s()
        mat = s()
        flags, nv, ni = struct.unpack_from("<III", b, q)
        q += 12
        m = {"name": name, "material": mat, "flags": flags}
        m["pos"] = arr("<f4", nv, 3)
        m["nrm"] = arr("<f4", nv, 3)
        m["tan"] = arr("<f4", nv, 4)
        m["uv"] = arr("<f4", nv, 2)
        m["joints"] = arr(np.uint8, nv, 4)
        m["weights"] = arr(np.uint8, nv, 4)
        m["color"] = arr(np.uint8, nv, 4)
        m["idx"] = arr("<u4", ni, 1)
        assert q == o + ln, (path, name)
        out[name] = m
    return out


def components(tris, nv):
    """Connected component id of every triangle (triangles sharing a vertex are connected); ids in first-seen order."""
    parent = np.arange(nv)

    def find(x):
        r = x
        while parent[r] != r:
            r = parent[r]
        while parent[x] != r:
            parent[x], x = r, parent[x]
        return r

    for a, b2, c in tris:
        ra, rb, rc = find(a), find(b2), find(c)
        lo = min(ra, rb, rc)
        parent[ra] = lo
        parent[rb] = lo
        parent[rc] = lo
    roots = np.array([find(t[0]) for t in tris])
    ids = {}
    out = np.empty(len(tris), np.int64)
    for i, r in enumerate(roots):
        out[i] = ids.setdefault(int(r), len(ids))
    return out


def tri_areas(P, tris):
    a, b, c = P[tris[:, 0]], P[tris[:, 1]], P[tris[:, 2]]
    return 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)


def lod3(m):
    P = m["pos"].astype(np.float32)
    N = m["nrm"].astype(np.float32)
    tris = m["idx"].reshape(-1, 3)
    comp = components(tris, len(P))
    area = tri_areas(P, tris)
    ncomp = int(comp.max()) + 1
    carea = np.array([area[comp == k].sum() for k in range(ncomp)])
    ctris = np.array([(comp == k).sum() for k in range(ncomp)])
    budget = np.maximum(MIN_TRIS, np.round(TARGET_TRIS * carea / max(carea.sum(), 1e-9))).astype(int)
    budget = np.minimum(budget, ctris)
    keep = []
    for k in range(ncomp):
        sub = tris[comp == k].reshape(-1).astype(np.uint32)
        if ctris[k] <= budget[k]:
            keep.append(sub)
        else:
            keep.append(lod.simplify(P, N, sub, int(budget[k]), ERROR_M))
    idx = np.concatenate(keep).astype(np.uint32)
    used = np.unique(idx)
    remap = np.full(len(P), -1, np.int64)
    remap[used] = np.arange(len(used))
    out = {"name": "lod3", "material": "lod", "flags": 0}
    for key in ("pos", "nrm", "tan", "uv", "joints", "weights", "color"):
        out[key] = m[key][used]
    out["idx"] = remap[idx].astype(np.uint32)
    return out, ncomp


def bake(chars_dir, out_dir, check):
    outputs = {}
    for ident, outfit in SOLDIERS:
        src = os.path.join(chars_dir, f"{ident}__{outfit}.g2ch")
        data = open(src, "rb").read()
        meshes = read_meshes(src)
        m2 = meshes["lod2"]
        m3, ncomp = lod3(m2)
        w = g2ch.Writer()
        w.info({"id": f"{ident}__{outfit}__lod3", "identity": ident, "outfit": outfit, "kind": "crowd_lod3",
                "source": f"{ident}__{outfit}.g2ch", "source_sha256": hashlib.sha256(data).hexdigest(),
                "source_triangles": int(len(m2["idx"]) // 3), "triangles": int(len(m3["idx"]) // 3),
                "components": ncomp, "method": f"meshopt_simplifyWithAttributes per component, target {TARGET_TRIS}, "
                f"floor {MIN_TRIS} per component, absolute error {ERROR_M} m"})
        w.mesh(m3)
        outputs[f"{ident}__{outfit}__lod3.g2ch"] = (w.bytes(), int(len(m2["idx"]) // 3), int(len(m3["idx"]) // 3))
    manifest = "".join(f"{k}\t{hashlib.sha256(v[0]).hexdigest()}\t{len(v[0])}\n" for k, v in sorted(outputs.items()))
    if check:
        bad = 0
        for k, (data, _, _) in sorted(outputs.items()):
            p = os.path.join(out_dir, k)
            if not os.path.exists(p) or open(p, "rb").read() != data:
                print(f"crowdbake --check: {k} differs")
                bad += 1
        mp = os.path.join(out_dir, "MANIFEST.tsv")
        if not os.path.exists(mp) or open(mp, "r", encoding="utf-8").read() != manifest:
            print("crowdbake --check: MANIFEST.tsv differs")
            bad += 1
        print(f"crowdbake --check: {len(outputs) + 1} files compared, {bad} differ")
        return 1 if bad else 0
    os.makedirs(out_dir, exist_ok=True)
    for k, (data, t2, t3) in sorted(outputs.items()):
        with open(os.path.join(out_dir, k), "wb") as f:
            f.write(data)
        print(f"crowdbake: {k}: lod2 {t2} -> lod3 {t3} triangles")
    with open(os.path.join(out_dir, "MANIFEST.tsv"), "w", encoding="utf-8") as f:
        f.write(manifest)
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--chars", required=True, help="baked chars folder (input)")
    ap.add_argument("--out", required=True, help="output folder")
    ap.add_argument("--check", action="store_true", help="re-bake in memory and compare with the existing outputs")
    a = ap.parse_args()
    sys.exit(bake(a.chars, a.out, a.check))


if __name__ == "__main__":
    main()
