"""MakeHuman hm08 core (CC0): base mesh, targets, macro and regional modifiers, proxy fitting.

Everything stays in MakeHuman's own units here (decimetres, +Y up, +Z forward, +X = the figure's left).
`to_metres` converts to the runtime frame (metres, ground at y = 0) at export time.
"""
import os

import numpy as np

AGE_YEARS = (1.0, 25.0, 90.0)  # MakeHuman: slider 0 -> 1 y, 0.5 -> 25 y, 1 -> 90 y


def age_slider(years):
    lo, mid, hi = AGE_YEARS
    if years <= mid:
        return 0.5 * (years - lo) / (mid - lo)
    return 0.5 + 0.5 * (years - mid) / (hi - mid)


class BaseMesh:
    """hm08 base.obj: 19,158 vertices, 18,486 quads in 139 groups (body, helpers, joint cubes)."""

    def __init__(self, path):
        vs, uvs, fv, fuv, fg = [], [], [], [], []
        groups, gindex = [], {}
        cur = -1
        with open(path, encoding="utf-8") as f:
            for line in f:
                if line.startswith("v "):
                    vs.append([float(t) for t in line.split()[1:4]])
                elif line.startswith("vt "):
                    uvs.append([float(t) for t in line.split()[1:3]])
                elif line.startswith("g "):
                    name = line.split()[1]
                    if name not in gindex:
                        gindex[name] = len(groups)
                        groups.append(name)
                    cur = gindex[name]
                elif line.startswith("f "):
                    toks = line.split()[1:]
                    if len(toks) != 4:
                        raise ValueError("hm08 faces are quads")
                    fv.append([int(t.split("/")[0]) - 1 for t in toks])
                    fuv.append([int(t.split("/")[1]) - 1 for t in toks])
                    fg.append(cur)
        self.V = np.array(vs, dtype=np.float64)
        self.UV = np.array(uvs, dtype=np.float64)
        self.fv = np.array(fv, dtype=np.int64)
        self.fuv = np.array(fuv, dtype=np.int64)
        self.fg = np.array(fg, dtype=np.int64)
        self.groups = groups
        self.gindex = gindex
        self._gverts = {}

    def group_faces(self, name):
        return np.nonzero(self.fg == self.gindex[name])[0]

    def group_verts(self, name):
        if name not in self._gverts:
            self._gverts[name] = np.unique(self.fv[self.group_faces(name)].ravel())
        return self._gverts[name]


class Targets:
    """Reads MakeHuman .target files (vertex index and dx dy dz, decimetres) with a cache."""

    def __init__(self, root):
        self.root = root
        self._cache = {}

    def get(self, rel):
        if rel not in self._cache:
            idx, d = [], []
            with open(os.path.join(self.root, rel), encoding="utf-8") as f:
                for line in f:
                    if not line.strip() or line.startswith("#"):
                        continue
                    t = line.split()
                    idx.append(int(t[0]))
                    d.append([float(t[1]), float(t[2]), float(t[3])])
            self._cache[rel] = (np.array(idx, dtype=np.int64), np.array(d, dtype=np.float64).reshape(-1, 3))
        return self._cache[rel]

    def exists(self, rel):
        return os.path.exists(os.path.join(self.root, rel))


def _tri(v, lo, mid, hi):
    mn = max(0.0, 1 - v * 2)
    mx = max(0.0, v * 2 - 1)
    return {lo: mn, mid: 1 - (mn + mx), hi: mx}


def _age(a):
    if a < 0.5:
        young = max(0.0, (a - 0.1875) * 3.2)
        return {"baby": max(0.0, 1 - a * 5.333), "child": max(0.0, min(1.0, 5.333 * a) - young), "young": young, "old": 0.0}
    old = max(0.0, a * 2 - 1)
    return {"baby": 0.0, "child": 0.0, "young": 1 - old, "old": old}


def macro_targets(m, targets):
    """MakeHuman 1.1 macro system: gender, age, muscle, weight, height, body proportions and race mix.

    Returns an ordered list of (target path, weight) with weight > 0 (ordinal by path)."""
    out = {}

    def add(rel, w):
        if w <= 0:
            return
        if not targets.exists(rel):
            raise FileNotFoundError(rel)
        out[rel] = out.get(rel, 0.0) + w

    gender = {"female": 1 - m["gender"], "male": m["gender"]}
    age = _age(m["age"])
    muscle = _tri(m["muscle"], "minmuscle", "averagemuscle", "maxmuscle")
    weight = _tri(m["weight"], "minweight", "averageweight", "maxweight")
    h, p = m["height"], m["proportions"]
    hw = {"minheight": max(0.0, 1 - 2 * h), "maxheight": max(0.0, 2 * h - 1)}
    pw = {"uncommonproportions": max(0.0, 1 - 2 * p), "idealproportions": max(0.0, 2 * p - 1)}
    races = {r: m[r] for r in ("african", "asian", "caucasian")}
    for gk, gw in sorted(gender.items()):
        for ak, aw in sorted(age.items()):
            if gw * aw <= 0:
                continue
            for rk, rw in sorted(races.items()):
                add(f"macrodetails/{rk}-{gk}-{ak}.target", gw * aw * rw)
            for mk, mw in sorted(muscle.items()):
                for wk, ww in sorted(weight.items()):
                    w = gw * aw * mw * ww
                    if w <= 0:
                        continue
                    add(f"macrodetails/universal-{gk}-{ak}-{mk}-{wk}.target", w)
                    for hk, hv in sorted(hw.items()):
                        add(f"macrodetails/height/{gk}-{ak}-{mk}-{wk}-{hk}.target", w * hv)
                    if ak != "baby":
                        for pk, pv in sorted(pw.items()):
                            add(f"macrodetails/proportions/{gk}-{ak}-{mk}-{wk}-{pk}.target", w * pv)
    return sorted(out.items())


def modifier_targets(spec, value):
    """A regional modifier "group/name:lo|hi" (value -1..1) or "group/name" (value 0..1)."""
    if value == 0:
        return []
    if ":" in spec:
        path, ends = spec.split(":")
        lo, hi = ends.split("|")
        group, name = path.split("/")
        end = hi if value > 0 else lo
        return [(f"{group}/{name}-{end}.target", abs(value))]
    group, name = spec.split("/")
    return [(f"{group}/{name}.target", value)]


def apply_targets(V0, targets, items):
    V = V0.copy()
    for rel, w in items:
        idx, d = targets.get(rel)
        V[idx] += w * d
    return V


class Proxy:
    """A MakeHuman .mhclo proxy (CC0 eyes): per proxy vertex up to 3 base vertices, weights and a scaled offset."""

    def __init__(self, mhclo_path):
        self.refs, self.w, self.off = [], [], []
        self.scale = {}
        self.obj = None
        in_verts = False
        with open(mhclo_path, encoding="utf-8") as f:
            for line in f:
                t = line.split()
                if not t or t[0].startswith("#"):
                    continue
                if in_verts:
                    if len(t) == 9:
                        self.refs.append([int(t[0]), int(t[1]), int(t[2])])
                        self.w.append([float(t[3]), float(t[4]), float(t[5])])
                        self.off.append([float(t[6]), float(t[7]), float(t[8])])
                    elif len(t) == 1:
                        self.refs.append([int(t[0]), int(t[0]), int(t[0])])
                        self.w.append([1.0, 0.0, 0.0])
                        self.off.append([0.0, 0.0, 0.0])
                    else:
                        in_verts = False
                    continue
                if t[0] in ("x_scale", "y_scale", "z_scale"):
                    self.scale[t[0][0]] = (int(t[1]), int(t[2]), float(t[3]))
                elif t[0] == "obj_file":
                    self.obj = os.path.join(os.path.dirname(mhclo_path), t[1])
                elif t[0] == "verts":
                    in_verts = True
        self.refs = np.array(self.refs, dtype=np.int64)
        self.w = np.array(self.w)
        self.off = np.array(self.off)

    def fit(self, V):
        s = np.ones(3)
        for k, axis in (("x", 0), ("y", 1), ("z", 2)):
            a, b, ref = self.scale[k]
            s[axis] = abs(V[a, axis] - V[b, axis]) / ref
        return (V[self.refs] * self.w[:, :, None]).sum(1) + self.off * s


def load_simple_obj(path):
    """Positions, UVs and faces (vertex and uv indices) of a small OBJ (proxy meshes)."""
    vs, uvs, fv, fuv = [], [], [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("v "):
                vs.append([float(t) for t in line.split()[1:4]])
            elif line.startswith("vt "):
                uvs.append([float(t) for t in line.split()[1:3]])
            elif line.startswith("f "):
                toks = line.split()[1:]
                fv.append([int(t.split("/")[0]) - 1 for t in toks])
                fuv.append([int(t.split("/")[1]) - 1 for t in toks])
    return np.array(vs), np.array(uvs), fv, fuv
