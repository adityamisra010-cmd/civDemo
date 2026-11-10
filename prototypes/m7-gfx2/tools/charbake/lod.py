"""Character LODs: one merged, single-material mesh per identity x outfit at about 3,000 (LOD1) and 1,500 (LOD2)
triangles, simplified with meshoptimizer v1.3 (MIT; built from the pinned sources by scripts/bake-chars.sh and
loaded through ctypes: a bake tool only, nothing of it ships).

What a LOD keeps: the visible skin (the outfit's body view) with the identity's skin UVs, the eyes, the innermost
hair and beard shell (a coloured cap), every garment, armour and equipment part. What it drops: brows, lashes, cornea,
teeth, tongue and the outer hair layers (sub-pixel at LOD distances). Each part is simplified separately with a
triangle budget proportional to its surface area (the head counts three times: faces carry recognition; budgets a part
cannot use are shared out again), with meshopt_simplifyWithAttributes on positions plus normals (absolute error limit
12 mm at LOD1, 30 mm at LOD2, retried at 2.5x and 6x); only a part still left at more than three times its budget
falls back to meshopt_simplifySloppy, and never down to less than a quarter of it. The merged mesh uses material "lod": vertex colour = albedo (sRGB) and
alpha = surface code (0..99 dielectric roughness, 128..227 metal roughness, 254 = painted shield face sampled from
the emblem texture, 255 = skin sampled from the skin texture, both through the UVs). Inner garment layers that lie
under an outer layer are dropped before simplifying (layer_hidden), so they cannot poke through it.
"""
import ctypes
import os

import numpy as np

import meshutil

_LIB = None


def library():
    global _LIB
    if _LIB is None:
        path = os.environ.get("G2_MESHOPT_LIB", "/home/user/g2w/build/meshopt/libmeshopt.so")
        if not os.path.exists(path):
            raise FileNotFoundError(f"{path}: build it with scripts/bake-chars.sh (meshoptimizer v1.3 sources)")
        lib = ctypes.CDLL(path)
        f = lib.meshopt_simplifyWithAttributes
        f.restype = ctypes.c_size_t
        f.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t,
                      ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t,
                      ctypes.c_float, ctypes.c_uint, ctypes.c_void_p]
        g = lib.meshopt_simplifySloppy
        g.restype = ctypes.c_size_t
        g.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t,
                      ctypes.c_void_p, ctypes.c_size_t, ctypes.c_float, ctypes.c_void_p]
        _LIB = lib
    return _LIB


OPT_ERROR_ABSOLUTE = 1 << 2
OPT_PRUNE = 1 << 3


def simplify(P, N, idx, target_tris, error=0.012):
    """Indices of the simplified triangle list (into the same vertex arrays). meshopt_simplifyWithAttributes (positions
    plus normals) at the given absolute error, retried at 2.5x and 6x the error when the budget is out of reach;
    meshopt_simplifySloppy (2 % of the part's extent) only when even that leaves more than three times the budget, and
    never when it would leave less than a quarter of the budget."""
    lib = library()
    P = np.ascontiguousarray(P, np.float32)
    A = np.ascontiguousarray(N, np.float32)
    idx = np.ascontiguousarray(idx, np.uint32)
    if len(idx) // 3 <= target_tris:
        return idx
    dst = np.zeros(len(idx), np.uint32)
    w = np.ascontiguousarray([0.25, 0.25, 0.25], np.float32)
    err = ctypes.c_float(0)
    n = len(idx)
    for e in (error, error * 2.5, error * 6.0):
        n = lib.meshopt_simplifyWithAttributes(dst.ctypes.data, idx.ctypes.data, len(idx), P.ctypes.data, len(P), 12,
                                               A.ctypes.data, 12, w.ctypes.data, 3, None, target_tris * 3,
                                               ctypes.c_float(e), OPT_ERROR_ABSOLUTE, ctypes.byref(err))
        if n // 3 <= target_tris * 1.25:
            return dst[:n].copy()
    best = dst[:n].copy()
    if n // 3 > target_tris * 3:
        sl = np.zeros(len(idx), np.uint32)
        m = lib.meshopt_simplifySloppy(sl.ctypes.data, idx.ctypes.data, len(idx), P.ctypes.data, len(P), 12, None,
                                       target_tris * 3, ctypes.c_float(0.02), ctypes.byref(err))
        if m // 3 >= max(4, target_tris // 4):
            return sl[:m].copy()
    return best


def srgb8(lin):
    lin = np.clip(np.asarray(lin, float), 0, 1)
    s = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
    return np.clip(np.round(s * 255), 0, 255).astype(np.uint8)


def hex_lin(h):
    c = np.array([int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)]) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


METAL_F0 = {"iron": (0.46, 0.45, 0.44), "mail": (0.30, 0.29, 0.28), "steel": (0.56, 0.56, 0.55),
            "bronze": (0.72, 0.47, 0.26), "gold": (0.95, 0.74, 0.33)}


def surface_of(mesh, mat, hair_lin):
    """Per-vertex (albedo linear rgb, alpha code) of one part for the LOD's single material."""
    n = len(mesh["pos"])
    kind = mesh["material"]
    if kind == "skin":
        return np.ones((n, 3)), np.full(n, 255, np.uint8)
    if kind in ("hairshell", "fur", "hair"):
        return np.tile(hair_lin * 0.8, (n, 1)), np.full(n, 75, np.uint8)
    if kind == "eye":
        return np.tile(np.array([0.55, 0.52, 0.50]), (n, 1)), np.full(n, 20, np.uint8)
    if mat is None:
        return np.tile(np.array([0.3, 0.3, 0.3]), (n, 1)), np.full(n, 80, np.uint8)
    if mat["kind"] == "metal":
        if mat.get("metal") == "mail":
            # at LOD distances riveted mail is the average of bright rings and dark gaps: a dark, rough grey
            return np.tile(np.array([0.11, 0.11, 0.105]), (n, 1)), np.full(n, int(round(0.62 * 99)), np.uint8)
        f0 = np.array(METAL_F0.get(mat.get("metal", "iron"), (0.5, 0.5, 0.5)))
        r = min(1.0, mat.get("rough", 0.4))
        return np.tile(f0, (n, 1)), np.full(n, 128 + int(round(r * 99)), np.uint8)
    if mat["kind"] in ("wood",):
        return np.tile(hex_lin(mat["colour"]), (n, 1)), np.full(n, int(round(mat.get("rough", 0.7) * 99)), np.uint8)
    if mat["kind"] == "paint":
        # the painted face keeps its UVs and samples the emblem texture (code 254); the colour is a fallback
        return np.tile(hex_lin(mat.get("lod_colour", "#5A4A3A")), (n, 1)), np.full(n, 254, np.uint8)
    base = hex_lin(mat.get("colour", "#808080"))
    alb = np.tile(base, (n, 1))
    if "colour2" in mat and "color" in mesh:
        band = mesh["color"][:, 0] > 127
        alb[band] = hex_lin(mat["colour2"]) * 0.75
    if "colour3" in mat and "color" in mesh:
        sole = mesh["color"][:, 2] > 127
        alb[sole] = hex_lin(mat["colour3"])
    return alb, np.full(n, int(round(mat.get("rough", 0.85) * 99)), np.uint8)


RIGID = {"helmet", "crest", "shield_face", "shield_back", "shield_rim", "spear_shaft", "spear_head", "sword", "sword_grip"}


def layer_hidden(ident, parts):
    """Per part, the vertices of an inner garment layer that lie under an outer layer (each garment vertex is mapped to
    its nearest skin vertex; a vertex is hidden when another garment reaches at least 5 mm further out over that skin
    vertex). Full-detail parts keep every layer; a LOD drops the hidden triangles, which the simplified outer layers
    would otherwise let poke through."""
    from hair import NearGrid
    V = ident.Vm
    ids = ident.A.mesh.group_verts("body")
    grid = NearGrid(V[ids], cell=0.02)
    info = []
    for mesh, mat in parts:
        if mesh["material"] not in ("cloth", "metal") or mesh["name"] in RIGID or mat is None:
            info.append(None)
            continue
        b = ids[grid.nearest(mesh["pos"])]
        info.append((b, np.linalg.norm(mesh["pos"] - V[b], axis=1)))
    maxoff = np.zeros(len(V))
    for x in info:
        if x is not None:
            np.maximum.at(maxoff, x[0], x[1])
    return [None if x is None else (x[1] + 0.005 < maxoff[x[0]]) for x in info]


def build(ident, built, outfit_meshes, materials, body_view, hair_view, hair_lin, total):
    """Merged LOD of one identity x outfit with about `total` triangles. built: identity meshes by name; outfit_meshes:
    [(mesh, morphs)]; body_view / hair_view: the outfit's visible index lists (None = all)."""
    parts = []
    body = dict(built["body"])
    body["idx"] = np.asarray(body_view if body_view is not None else body["idx"])
    parts.append((body, None))
    if "eyes" in built:
        # eyeballs shrunk by 4 % about each eye centre, so the simplified spheres stay behind the lids
        e = dict(built["eyes"])
        P = e["pos"].copy()
        for c in ident.eye_centres:
            near = np.linalg.norm(P - c, axis=1) < 0.02
            P[near] = c + (P[near] - c) * 0.96
        e["pos"] = P
        parts.append((e, None))
    for name in ("hair", "beard"):
        m = built.get(name)
        if m is None:
            continue
        # the innermost shell only: vertex colour A holds the layer fraction (first layer = smallest)
        frac = m["color"][:, 3]
        first = frac == frac.min()
        T = np.asarray(m["idx"]).reshape(-1, 3)
        if name == "hair" and hair_view is not None:
            T = np.asarray(hair_view).reshape(-1, 3)
        T = T[first[T].all(1)]
        # only where the hair is dense (vertex colour R = density)
        dens = m["color"][:, 0]
        T = T[(dens[T] > 90).all(1)]
        mm = dict(m)
        mm["idx"] = T.ravel()
        parts.append((mm, None))
    for mesh, _ in outfit_meshes:
        parts.append((mesh, materials.get(mesh["name"])))
    hidden = layer_hidden(ident, parts)
    for k, (mesh, mat) in enumerate(parts):
        if hidden[k] is not None and hidden[k].any():
            T = np.asarray(mesh["idx"]).reshape(-1, 3)
            m2 = dict(mesh)
            m2["idx"] = T[~hidden[k][T].all(1)].ravel()
            parts[k] = (m2, mat)
    # budgets by surface area (the head counts three times)
    areas = []
    head_y = ident.heads["neck_01"][1]
    for mesh, _ in parts:
        T = np.asarray(mesh["idx"]).reshape(-1, 3)
        P = mesh["pos"]
        if len(T) == 0:
            areas.append(0.0)
            continue
        a = 0.5 * np.linalg.norm(np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]]), axis=1)
        # the face (skin above the neck) counts three times: it carries recognition; hair, beard and helmets do not
        w = np.where((P[T].mean(1)[:, 1] > head_y) & (mesh["material"] == "skin"), 3.0, 1.0)
        areas.append(float((a * w).sum()))
    # budgets: area-proportional, eyes at least ~64 triangles each at LOD1; parts that have fewer triangles than their
    # share keep them all and the leftover is shared out again among the others (water filling)
    have = [len(np.asarray(m["idx"])) // 3 for m, _ in parts]
    floor_ = [int(total * 0.043) if m["material"] == "eye" else 8 for m, _ in parts]
    budgets = [0] * len(parts)
    open_ = [k for k in range(len(parts)) if have[k] > 0]
    left = total
    for _ in range(8):
        a_open = sum(areas[k] for k in open_) or 1.0
        for k in open_:
            budgets[k] = max(floor_[k], int(left * areas[k] / a_open))
        done = [k for k in open_ if have[k] <= budgets[k]]
        if not done:
            break
        for k in done:
            budgets[k] = have[k]
        left = total - sum(budgets[k] for k in range(len(parts)) if k not in open_ or k in done)
        open_ = [k for k in open_ if k not in done]
        if not open_:
            break
    pos, nrm, uv, col, joints, weights, idx = [], [], [], [], [], [], []
    base = 0
    for k, ((mesh, mat), area) in enumerate(zip(parts, areas)):
        T = np.asarray(mesh["idx"], np.uint32)
        if len(T) == 0:
            continue
        budget = max(8, budgets[k])
        S = simplify(mesh["pos"], mesh["nrm"], T, budget, error=0.012 if total >= 2500 else 0.03)
        if len(S) == 0:
            S = T
        if len(S) == 0:
            continue
        used = np.unique(S)
        remap = -np.ones(len(mesh["pos"]), np.int64)
        remap[used] = np.arange(len(used))
        alb, code = surface_of(mesh, mat, hair_lin)
        pos.append(mesh["pos"][used]); nrm.append(mesh["nrm"][used])
        uv.append(mesh["uv"][used] if mesh["material"] in ("skin", "paint") else np.zeros((len(used), 2)))
        c = np.zeros((len(used), 4), np.uint8)
        c[:, :3] = srgb8(alb[used])
        c[:, 3] = code[used]
        col.append(c)
        joints.append(mesh["joints"][used]); weights.append(mesh["weights"][used])
        idx.append(remap[S.astype(np.int64)] + base)
        base += len(used)
    P = np.concatenate(pos)
    N = np.concatenate(nrm)
    UV = np.concatenate(uv)
    I = np.concatenate(idx)
    T = I.reshape(-1, 3)
    tan = meshutil.tangents(P, N, UV, T)
    return {"pos": P, "nrm": N, "tan": tan, "uv": UV, "color": np.concatenate(col), "joints": np.concatenate(joints),
            "weights": np.concatenate(weights), "idx": I, "material": "lod"}
