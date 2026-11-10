"""Garments and armour generated in code over each identity's own body (no external garment meshes).

A garment is either an offset shell of a region of the skin (or of MakeHuman's CC0 tights helper), or a parametric
surface fitted to the body (skirts and footwear). Shells are cut by smooth scalar fields (heights, distances along the
limbs, distance from the neck): every boundary is the straight-segment contour of a smooth field across the mesh
triangles, so hems and necklines are smooth curves, not quad staircases. A woven border is split off along the
iso-line "distance to the edge = border width" (crisp and even, with its own UVs for a woven pattern), and every
edge gets a turned-back hem (the cloth folds inwards by its thickness). Shells inherit the skin weights of the
surface they were made from; skin under a garment is removed from that outfit's visible-skin view (VIEW chunk)
wherever it is covered with a margin, so skin cannot show through at shoulders or elbows.
Vertex colour: R = 255 on the woven border, G = occlusion, B = garment-defined secondary region, A = wear.
"""
import numpy as np

import meshutil
from fields import smoothstep
from rig import INDEX, quantize_weights


class BodyCoords:
    """Continuous coordinates on the rest body (metres): height, distance along each arm and leg, neck distance."""

    def __init__(self, ident):
        self.I = ident
        A = ident.A
        h = ident.heads
        V = ident.Vm
        self.V = V
        J, W = A.joints, A.weights / 255.0
        self.dominant = J[np.arange(len(V)), np.argmax(W, 1)]
        self.arm = {}
        self.leg = {}
        for s in ("l", "r"):
            sh, el, wr = h[f"upperarm_{s}"], h[f"lowerarm_{s}"], h[f"hand_{s}"]
            self.arm[s] = self._polyline(V, [sh, el, wr, wr + (wr - el) * 0.6])
            hip, kn, an = h[f"thigh_{s}"], h[f"calf_{s}"], h[f"foot_{s}"]
            self.leg[s] = self._polyline(V, [hip, kn, an, an + np.array([0, -0.08, 0])])
        self.neck_base = 0.5 * (h["neck_01"] + h["spine_03"]) * 0 + h["neck_01"]
        self.shoulder_y = 0.5 * (h["upperarm_l"][1] + h["upperarm_r"][1])
        self.hip_y = h["thigh_l"][1]
        self.knee_y = h["calf_l"][1]
        self.ankle_y = h["foot_l"][1]
        self.waist_y = h["spine_01"][1] + 0.03
        self.side = np.where(V[:, 0] >= 0, "l", "r")
        bones = {n: INDEX[n] for n in INDEX}
        armb = {bones[f"{p}_{s}"] for s in "lr" for p in ("upperarm", "lowerarm", "hand", "clavicle")}
        armb |= {bones[n] for n in INDEX if any(n.startswith(f) for f in ("thumb", "index", "middle", "ring", "pinky"))}
        legb = {bones[f"{p}_{s}"] for s in "lr" for p in ("thigh", "calf", "foot", "ball")}
        # how much a vertex belongs to an arm / a leg (sum of its weights there)
        self.armness = np.zeros(len(V))
        self.legness = np.zeros(len(V))
        self.headness = np.zeros(len(V))
        for k in range(4):
            self.headness += np.where(np.isin(J[:, k], [bones["head"], bones["eye_l"], bones["eye_r"]]), W[:, k], 0)
        for k in range(4):
            self.armness += np.where(np.isin(J[:, k], list(armb - {bones["clavicle_l"], bones["clavicle_r"]})), W[:, k], 0)
            self.legness += np.where(np.isin(J[:, k], list(legb)), W[:, k], 0)

    @staticmethod
    def _polyline(V, pts):
        """Arc length of each vertex's projection on a polyline (negative before its start)."""
        best_d = np.full(len(V), np.inf)
        best_s = np.zeros(len(V))
        acc = 0.0
        for a, b in zip(pts[:-1], pts[1:]):
            ab = b - a
            L = np.linalg.norm(ab)
            t = ((V - a) @ ab) / (L * L)
            tc = np.clip(t, 0, 1)
            q = a + tc[:, None] * ab
            d = np.linalg.norm(V - q, axis=1)
            s = acc + t * L
            better = d < best_d
            best_d[better] = d[better]
            best_s[better] = s[better]
            acc += L
        return best_s

    def arm_s(self):
        return np.where(self.V[:, 0] >= 0, self.arm["l"], self.arm["r"])

    def leg_s(self):
        return np.where(self.V[:, 0] >= 0, self.leg["l"], self.leg["r"])


def loop_segments(P, loops):
    """Boundary loops -> segment arrays (a, b), the loop id and the arc length at a, plus each loop's length."""
    A, B, L, S0, lengths = [], [], [], [], []
    for li, loop in enumerate(loops):
        pts = P[np.array(loop + [loop[0]])]
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        acc = np.concatenate([[0.0], np.cumsum(seg)[:-1]])
        A.append(pts[:-1]); B.append(pts[1:]); L.append(np.full(len(seg), li)); S0.append(acc)
        lengths.append(float(seg.sum()))
    if not A:
        z = np.zeros((0, 3))
        return z, z, np.zeros(0, np.int64), np.zeros(0), []
    return np.concatenate(A), np.concatenate(B), np.concatenate(L), np.concatenate(S0), lengths


def distance_to_segments(X, A, B, S0, chunk=4096):
    """Euclidean distance of each point to the nearest segment, that segment's index and the arc length there."""
    n = len(X)
    best = np.full(n, np.inf)
    seg = np.zeros(n, np.int64)
    arc = np.zeros(n)
    if len(A) == 0:
        return best, seg, arc
    AB = B - A
    L2 = np.maximum((AB * AB).sum(1), 1e-18)
    for i0 in range(0, n, chunk):
        Xc = X[i0:i0 + chunk]
        t = np.clip((((Xc[:, None, :] - A[None]) * AB[None]).sum(2)) / L2[None], 0.0, 1.0)
        Q = A[None] + t[:, :, None] * AB[None]
        d = np.linalg.norm(Xc[:, None, :] - Q, axis=2)
        j = np.argmin(d, 1)
        r = np.arange(len(Xc))
        best[i0:i0 + chunk] = d[r, j]
        seg[i0:i0 + chunk] = j
        arc[i0:i0 + chunk] = S0[j] + t[r, j] * np.sqrt(L2[j])
    return best, seg, arc


def weld_normals(P, T, decimals=7):
    """Smooth normals over coincident positions (vertices duplicated along a trim cut shade as one surface)."""
    key = np.round(P, decimals)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.ravel()
    Pw = np.zeros((inv.max() + 1, 3))
    Pw[inv] = P
    Nw = meshutil.vertex_normals(Pw, inv[T])
    return Nw[inv]


def fair_loops(P, loops, iters, lam=0.5, mu=-0.53):
    """Taubin smoothing of each closed boundary loop's positions along the loop (the interior is left alone): removes
    the small kinks a level set picks up where the surface under it is uneven, without shrinking the loop."""
    P = P.copy()
    for lp in loops:
        idx = np.asarray(lp)
        q = P[idx]
        for _ in range(iters):
            for f in (lam, mu):
                q = q + f * (0.5 * (np.roll(q, 1, 0) + np.roll(q, -1, 0)) - q)
        P[idx] = q
    return P


def _components(mask, F, n):
    """Connected components of the masked vertices over the mesh edges: root id per vertex (-1 outside the mask)."""
    parent = np.arange(n)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for a, c in meshutil.adjacency(F, n):
        if mask[a] and mask[c]:
            ra, rc = find(a), find(c)
            if ra != rc:
                parent[max(ra, rc)] = min(ra, rc)
    roots = np.array([find(i) for i in range(n)])
    roots[~mask] = -1
    return roots


def keep_largest(region, F, n):
    """Region (<= 0 keeps) with every kept island except the largest (by vertex count over the mesh edges) removed."""
    keep = region <= 0
    if not keep.any():
        return region
    roots = _components(keep, F, n)
    ids, counts = np.unique(roots[keep], return_counts=True)
    main = ids[np.argmax(counts)]
    return np.where(keep & (roots != main), np.maximum(region, 1e-3), region)


def fill_holes(region, F, n, max_verts=150):
    """Region (<= 0 keeps) with every cut-out island of at most `max_verts` vertices kept instead. A level set over
    the skin cuts small holes where the skin dips under its threshold (the hollows above the collarbones under a
    neck field, an armpit crease under an arm weight); real openings (neck, arms, hands, legs) are far larger."""
    out = region > 0
    if not out.any():
        return region
    roots = _components(out, F, n)
    ids, counts = np.unique(roots[out], return_counts=True)
    small = ids[counts <= max_verts]
    if not len(small):
        return region
    hole = np.isin(roots, small)
    return np.where(hole, -1e-3, region)


def finish(P, F, region, src, trim=0.0, trim_repeat=0.036, hem=0.004, secondary=None, fair=0):
    """Clip a surface (P, F) to region <= 0, split off a woven border of width `trim` along every edge, fold the
    edges inwards by `hem`. src: an integer per input vertex (base-mesh id for shells, own id for parametric
    garments): every output vertex carries the src of its nearest input vertex (weights are not linear data).
    fair: Taubin passes along each cut edge loop before the border and hem are made (0 = none).
    Returns dict(pos, T, nrm, band, uv, src, sec)."""
    P = np.asarray(P, float)
    sec = np.zeros(len(P)) if secondary is None else np.asarray(secondary, float)
    attrs = {"P": P, "src": np.asarray(src, float), "sec": sec}
    Fc, at, _ = meshutil.clip_mesh(F, region, attrs, keep_below=True)
    nP = at["P"]
    src_near = np.round(at["src"]).astype(np.int64)
    src_near[len(P):] = nearest_src(nP[len(P):], P, np.asarray(src))
    Fc, at2, _ = meshutil.compact(Fc, {"P": nP, "sec": at["sec"], "src": src_near.astype(float)})
    P2 = at2["P"]
    srcv = np.round(at2["src"]).astype(np.int64)
    sec2 = at2["sec"]
    loops = [lp for lp in meshutil.boundary_loops(Fc) if len(lp) >= 3]
    if fair > 0 and loops:
        P2 = fair_loops(P2, loops, fair)
    band = np.zeros(len(P2), bool)
    uv = np.zeros((len(P2), 2))
    if trim > 0 and loops:
        SA, SB, SL, S0, lengths = loop_segments(P2, loops)
        dist, seg, arc = distance_to_segments(P2, SA, SB, S0)
        attrs3 = {"P": P2, "src": srcv.astype(float), "sec": sec2}
        Fb, atb, _ = meshutil.clip_mesh(Fc, dist - trim, attrs3, keep_below=True)
        Fm, atm, _ = meshutil.clip_mesh(Fc, dist - trim, attrs3, keep_below=False)
        nb = len(atb["P"]) - len(P2)
        Fm = np.where(Fm >= len(P2), Fm + nb, Fm)
        P3 = np.concatenate([atb["P"], atm["P"][len(P2):]])
        # crossing vertices: the source id of the nearest pre-split vertex (an interpolated id would name an
        # unrelated body vertex and follow the wrong bone)
        src3 = np.concatenate([srcv, nearest_src(P3[len(P2):], P2, srcv)]).astype(float)
        sec3 = np.concatenate([atb["sec"], atm["sec"][len(P2):]])
        bnd = np.zeros(len(P3), bool)
        bnd[np.unique(Fb.ravel())] = True
        Fc, at4, _ = meshutil.compact(np.concatenate([Fb, Fm]), {"P": P3, "src": src3, "sec": sec3, "band": bnd.astype(float)})
        P2 = at4["P"]; srcv = np.round(at4["src"]).astype(np.int64); sec2 = at4["sec"]; band = at4["band"] > 0.5
        # border UVs: u = arc length along the nearest edge in whole repeats per loop, v = distance / width
        d3, s3, a3 = distance_to_segments(P2, SA, SB, S0)
        rep = np.array([max(1.0, round(L / trim_repeat)) for L in lengths])
        per = np.array(lengths) / rep
        uv = np.stack([a3 / per[SL[s3]], np.clip(d3 / trim, 0.0, 1.0)], 1)
        uv[~band] = 0.0
        loops = [lp for lp in meshutil.boundary_loops(Fc) if len(lp) >= 3]
        # the split leaves two coincident vertex rows along the cut; only the outer edges are real boundaries
        loops = [lp for lp in loops if band[lp].all() and (uv[lp, 1] < 0.05).all()]
    N2 = weld_normals(P2, Fc)
    pos, srcl, secl, bandl, uvl = list(P2), list(srcv), list(sec2), list(band), list(uv)
    tris = [tuple(t) for t in Fc]
    for loop in loops:
        base = len(pos)
        for v_ in loop:
            pos.append(P2[v_] - N2[v_] * hem)
            srcl.append(srcv[v_]); secl.append(sec2[v_]); bandl.append(trim > 0); uvl.append((uv[v_, 0], 0.0))
        n = len(loop)
        for i in range(n):
            a, b = loop[i], loop[(i + 1) % n]
            a2, b2 = base + i, base + (i + 1) % n
            tris += [(b, a, a2), (b, a2, b2)]
    pos = np.array(pos)
    T = np.array(tris, np.int64).reshape(-1, 3)
    return {"pos": pos, "T": T, "nrm": weld_normals(pos, T), "band": np.array(bandl, bool),
            "uv": np.array(uvl, float).reshape(-1, 2), "src": np.array(srcl, np.int64), "sec": np.array(secl, float)}


def nearest_src(X, P, src, chunk=2048):
    """For each point of X the src entry of its nearest point in P."""
    out = np.zeros(len(X), np.int64)
    for i0 in range(0, len(X), chunk):
        d2 = ((X[i0:i0 + chunk, None, :] - P[None, :, :]) ** 2).sum(2)
        out[i0:i0 + chunk] = src[np.argmin(d2, 1)]
    return out


def to_mesh(name, material, g, W, wear=None):
    """A finished garment (finish()) with a per-vertex joint-weight matrix -> mesh dict for the G2CH writer."""
    col = np.zeros((len(g["pos"]), 4))
    col[:, 0] = g["band"]
    col[:, 1] = 1.0
    col[:, 2] = np.clip(g["sec"], 0, 1)
    col[:, 3] = 0.0 if wear is None else wear
    joints, weights = quantize_weights(W)
    tan = meshutil.tangents(g["pos"], g["nrm"], g["uv"], g["T"])
    return {"name": name, "material": material, "pos": g["pos"], "nrm": g["nrm"], "tan": tan, "uv": g["uv"],
            "color": (np.clip(col, 0, 1) * 255).astype(np.uint8), "joints": joints, "weights": weights,
            "idx": g["T"].ravel(), "src": g["src"]}


def skin_weights_of(A, src):
    W = np.zeros((len(src), len(INDEX)))
    for k in range(4):
        np.add.at(W, (np.arange(len(src)), A.joints[src, k]), A.weights[src, k] / 255.0)
    return W


def gather_folds(ident, P, N, y_top, y_belt, amp, seed=0):
    """Cloth gathered by a belt: vertical folds on the torso that grow from nothing at y_top to `amp` (m, outward
    only) at the belt line, spaced about 5 cm round the body, with fixed random phases."""
    h = ident.heads
    c = h["spine_02"]
    az = np.arctan2(P[:, 0] - c[0], P[:, 2] - c[2])
    rng = np.random.default_rng(seed + 4049)
    ph = rng.random(3) * 2 * np.pi
    wave = 0.55 * np.sin(18 * az + ph[0]) + 0.3 * np.sin(11 * az + ph[1]) + 0.15 * np.sin(29 * az + ph[2])
    wave = 0.5 + 0.5 * wave / 1.0
    t = np.clip((y_top - P[:, 1]) / max(y_top - y_belt, 1e-3), 0, 1)
    below = P[:, 1] < y_belt
    k = np.where(below, np.clip(1 - (y_belt - P[:, 1]) / 0.05, 0, 1), t ** 1.6)
    return P + N * (amp * k * wave)[:, None]


def build_shell(ident, src, F, region, offset, smooth=0, name="garment", material="cloth", hem=0.004,
                secondary=None, flare=None, stiff=0.0, trim=0.0, trim_repeat=0.036, gather=None, field=None,
                field_smooth=20, islands=False, fair=0, return_region=False):
    """Offset shell of the surface (src: base-mesh vertex ids, F: triangles indexing src order) inside region <= 0.

    offset: per-src-vertex or scalar metres above the skin (after `smooth` Taubin iterations); flare: optional extra
    outward offset per vertex; trim: woven border width (m); stiff: share of the weights moved to the spine and
    pelvis (rigid plate). field: optional function of the shell's own positions, taken after `field_smooth` more
    Taubin passes so that the skin's local unevenness does not bend its level set, combined into the region with max;
    islands: drop every kept island but the largest; fair: Taubin passes along the cut edges (finish).
    return_region: also return the region used (per src vertex)."""
    A = ident.A
    V = ident.Vm[src]
    P = V.copy()
    if smooth:
        P = meshutil.laplacian_smooth(P, F, iters=smooth)
    N = meshutil.vertex_normals(P, F)
    off = np.broadcast_to(np.asarray(offset, float), (len(P),)).copy()
    if flare is not None:
        off = off + flare
    P = P + N * off[:, None]
    if gather is not None:
        y_top, y_belt, amp, seed = gather
        P = gather_folds(ident, P, N, y_top, y_belt, amp, seed)
    if field is not None:
        Pf = meshutil.laplacian_smooth(P, F, iters=field_smooth) if field_smooth else P
        region = np.maximum(region, field(Pf))
    if islands:
        region = keep_largest(region, F, len(P))
    region = fill_holes(region, F, len(P))
    g = finish(P, F, region, src, trim=trim, trim_repeat=trim_repeat, hem=hem, secondary=secondary, fair=fair)
    W = skin_weights_of(A, g["src"])
    if stiff > 0:
        W = stiffen(W, stiff)
    mesh = to_mesh(name, material, g, W)
    return (mesh, region) if return_region else mesh


def stiffen(W, amount):
    """Rigid parts (cuirass, helmets): push influences towards the spine and pelvis so plate does not bend like skin."""
    keep = [INDEX[n] for n in ("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head", "clavicle_l", "clavicle_r")]
    Wk = np.zeros_like(W)
    Wk[:, keep] = W[:, keep]
    s = Wk.sum(1, keepdims=True)
    Wk = np.where(s > 1e-6, Wk / np.maximum(s, 1e-9), W)
    return W * (1 - amount) + Wk * amount


def morphs_for(ident, mesh):
    """Garment vertices follow the expression deltas of their source skin vertices (collars move with the jaw)."""
    out = []
    src = mesh["src"]
    for name in sorted(ident.expr):
        d = ident.expr[name][src]
        keep = np.nonzero(np.abs(d).max(1) > 2e-6)[0]
        if len(keep):
            out.append((name, keep, d[keep], np.zeros((len(keep), 3))))
    return out


def covered_body(ident, region_by_vertex, margin=0.0):
    """Base-mesh vertex mask that a garment hides (inside its region by a margin)."""
    return region_by_vertex < -margin
