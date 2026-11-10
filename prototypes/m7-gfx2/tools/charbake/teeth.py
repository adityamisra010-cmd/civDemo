"""Procedural dentition fitted to the CC0 base mesh's teeth helper bands (which are plain curved strips).

Each jaw gets 14 crowns (incisors, canines, premolars, molars, human mesio-distal widths in mm) shaped as rounded
superellipsoid blocks, laid along the arch traced from the helper band, plus a gum band. Every vertex takes the
skin weights and expression deltas of the nearest helper-band vertices (inverse-distance), so the lower teeth open
with the jaw in the battle-cry and pain expressions.
"""
import numpy as np

import meshutil
from rig import quantize_weights

UPPER = [8.5, 6.5, 7.5, 7.0, 6.5, 10.0, 9.0]   # central incisor .. second molar (mm)
LOWER = [5.3, 5.9, 7.0, 7.0, 7.0, 11.0, 10.5]
HEIGHT_U = [10.5, 9.0, 10.0, 8.0, 7.5, 7.0, 6.5]
HEIGHT_L = [9.0, 9.0, 10.0, 8.0, 8.0, 7.5, 7.0]
DEPTH = [7.0, 6.5, 8.0, 9.0, 9.0, 11.0, 10.5]


def arch(points):
    """Arch centreline of a U-shaped band: per azimuth bin around the band's centroid (front = +Z), the mean point."""
    c = points.mean(0)
    back = points[:, 2].min()
    centre = np.array([c[0], c[1], back])
    ang = np.arctan2(points[:, 0] - centre[0], points[:, 2] - centre[2])
    bins = np.linspace(ang.min(), ang.max(), 15)
    pts = []
    for a0, a1 in zip(bins[:-1], bins[1:]):
        sel = (ang >= a0) & (ang <= a1)
        if sel.any():
            pts.append(points[sel].mean(0))
    pts = np.array(pts)
    pts = pts[np.argsort(np.arctan2(pts[:, 0] - centre[0], pts[:, 2] - centre[2]))]
    return pts


def resample(curve, n):
    seg = np.linalg.norm(np.diff(curve, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, curve[:, k]) for k in range(3)], 1), s[-1]


def superellipsoid(nu=7, nv=6, e=0.35):
    """Unit rounded block (|x|,|y|,|z| <= 1) as a closed quad grid -> (verts, tris)."""
    us = np.linspace(-np.pi, np.pi, nu * 2, endpoint=False)
    vs = np.linspace(-np.pi / 2, np.pi / 2, nv + 1)

    def sp(a, p):
        return np.sign(np.cos(a)) * np.abs(np.cos(a)) ** p, np.sign(np.sin(a)) * np.abs(np.sin(a)) ** p

    V = []
    for v in vs:
        cv, sv = sp(v, e)
        for u in us:
            cu, su = sp(u, e)
            V.append((cv * cu, sv, cv * su))
    V = np.array(V)
    T = []
    m = len(us)
    for i in range(nv):
        for j in range(m):
            a, b = i * m + j, i * m + (j + 1) % m
            c, d = a + m, b + m
            T += [(a, c, b), (b, c, d)]
    return V, np.array(T)


def build(ident):
    A = ident.A
    out = []
    unit, ut = superellipsoid()
    for grp, widths, heights, sign, name in (("helper-upper-teeth", UPPER, HEIGHT_U, 1.0, "teeth_upper"),
                                              ("helper-lower-teeth", LOWER, HEIGHT_L, -1.0, "teeth_lower")):
        verts = A.mesh.group_verts(grp)
        P = ident.Vm[verts]
        line = arch(P)
        curve, length = resample(line, 200)
        half = sum(widths) / 1000.0
        scale = (length / 2) / half * 0.98
        mid = len(curve) // 2
        s_all = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(curve, axis=0), axis=1))])
        s_mid = s_all[mid]
        top = P[:, 1].max() if sign > 0 else P[:, 1].min()
        bite = P[:, 1].min() if sign > 0 else P[:, 1].max()
        pos, tris = [], []
        cols = []
        for side in (-1, 1):
            cum = 0.0
            for k, w in enumerate(widths):
                w_m = w / 1000.0 * scale
                sc = s_mid + side * (cum + w_m / 2)
                cum += w_m
                p = np.array([np.interp(sc, s_all, curve[:, d]) for d in range(3)])
                i0 = int(np.clip(np.searchsorted(s_all, sc), 1, len(curve) - 1))
                tdir = curve[i0] - curve[i0 - 1]
                tdir /= np.linalg.norm(tdir)
                up = np.array([0.0, 1.0, 0.0])
                outw = np.cross(up, tdir)
                if np.dot(outw, p - curve.mean(0)) < 0:
                    outw = -outw
                h = heights[k] / 1000.0 * scale
                dep = DEPTH[k] / 1000.0 * scale * 0.9
                # crown: biting edge at the band's biting side, root side hidden in the gum
                cy = bite + sign * h * 0.45
                centre = np.array([p[0], cy, p[2]])
                V = unit.copy()
                taper = 1.0 - 0.25 * ((V[:, 1] * sign) > 0)
                X = (V[:, 0] * w_m * 0.48 * taper)[:, None] * tdir[None, :]
                Y = (V[:, 1] * h * 0.5)[:, None] * up[None, :]
                Z = (V[:, 2] * dep * 0.5 * (0.55 if k < 3 else 1.0))[:, None] * outw[None, :]
                base = len(pos)
                pos += list(centre + X + Y + Z)
                tris += [tuple(base + x for x in t) for t in ut]
                shade = 0.94 + 0.06 * np.cos(k)
                cols += [(int(226 * shade), int(216 * shade), int(194 * shade), 255)] * len(V)
        # gum band: a slightly inflated copy of the helper strip on the root side
        gF = meshutil.quads_to_tris(A.mesh.fv[A.mesh.group_faces(grp)])
        gremap = {int(v): i for i, v in enumerate(verts)}
        gP = ident.Vm[verts].copy()
        gN = meshutil.vertex_normals(ident.Vm, gF)[verts]
        keep_root = (gP[:, 1] - bite) * sign > (top - bite) * sign * 0.55
        gP = gP + gN * 0.0012
        base = len(pos)
        pos += list(gP)
        cols += [(178, 92, 96, 255)] * len(gP)
        for t in gF:
            a, b, c = (gremap[int(x)] for x in t)
            if keep_root[a] or keep_root[b] or keep_root[c]:
                tris.append((base + a, base + b, base + c))
        pos = np.array(pos)
        tris = np.array(tris, np.int64)
        # attachment: inverse-distance to the 3 nearest helper vertices
        d2 = ((pos[:, None, :] - P[None, :, :]) ** 2).sum(2)
        nn = np.argsort(d2, 1)[:, :3]
        wd = 1.0 / (np.sqrt(np.take_along_axis(d2, nn, 1)) + 1e-4)
        wd /= wd.sum(1, keepdims=True)
        src = verts[nn]
        W = np.zeros((len(pos), A.joints.max() + 1 if False else 55))
        for c in range(3):
            for k in range(4):
                np.add.at(W, (np.arange(len(pos)), A.joints[src[:, c], k]), A.weights[src[:, c], k] / 255.0 * wd[:, c])
        joints, weights = quantize_weights(W)
        N = meshutil.vertex_normals(pos, tris)
        UV = np.zeros((len(pos), 2))
        tan = meshutil.tangents(pos, N, UV, tris)
        morphs = []
        for ename in sorted(ident.expr):
            d = (ident.expr[ename][src] * wd[:, :, None]).sum(1)
            keep = np.nonzero(np.abs(d).max(1) > 2e-6)[0]
            morphs.append((ename, keep, d[keep], np.zeros((len(keep), 3))))
        out.append(({"name": name, "material": "mouth", "pos": pos, "nrm": N, "tan": tan, "uv": UV,
                     "color": np.array(cols, np.uint8), "joints": joints, "weights": weights, "idx": tris.ravel()}, morphs))
    return out
