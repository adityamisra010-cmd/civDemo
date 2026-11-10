"""Small mesh builders for rigid equipment (all in metres): lathe, tube along a path, extruded outlines, spheres."""
import numpy as np


def lathe(profile, segs, axis_pts=None, close_top=False):
    """Surface of revolution about +Y. profile: list of (radius, y); returns (P, F, UV) with u around, v along."""
    prof = np.asarray(profile, float)
    n = len(prof)
    P, UV = [], []
    for i in range(n):
        r, y = prof[i]
        for k in range(segs + 1):
            a = 2 * np.pi * k / segs
            P.append((r * np.sin(a), y, r * np.cos(a)))
            UV.append((k / segs, i / (n - 1)))
    F = []
    for i in range(n - 1):
        for k in range(segs):
            a = i * (segs + 1) + k
            b, c, d = a + 1, a + segs + 1, a + segs + 2
            F += [(a, c, b), (b, c, d)]
    return np.array(P), np.array(F, np.int64), np.array(UV)


def tube(path, radii, segs=10, up=(0.0, 0.0, 1.0)):
    """Generalised cylinder along a polyline (closed caps omitted): returns (P, F, UV)."""
    path = np.asarray(path, float)
    radii = np.broadcast_to(np.asarray(radii, float), (len(path),))
    P, UV, F = [], [], []
    up = np.asarray(up, float)
    for i, p in enumerate(path):
        t = path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]
        t /= np.linalg.norm(t)
        s = np.cross(t, up)
        if np.linalg.norm(s) < 1e-6:
            s = np.cross(t, [1.0, 0, 0])
        s /= np.linalg.norm(s)
        b = np.cross(t, s)
        for k in range(segs + 1):
            a = 2 * np.pi * k / segs
            P.append(p + radii[i] * (np.cos(a) * s + np.sin(a) * b))
            UV.append((k / segs, i / (len(path) - 1)))
    for i in range(len(path) - 1):
        for k in range(segs):
            a = i * (segs + 1) + k
            F += [(a, a + segs + 1, a + 1), (a + 1, a + segs + 1, a + segs + 2)]
    return np.array(P), np.array(F, np.int64), np.array(UV)


def extrude(outline, depth, bevel=0.0, curve=None):
    """Prism from a closed 2D outline (x, y) with optional per-point z curvature: front at z=+depth/2.
    Returns (P, F, UV) with planar UVs from the outline's bounding box (front and back), side strip u along."""
    o = np.asarray(outline, float)
    n = len(o)
    lo, hi = o.min(0), o.max(0)
    uvo = (o - lo) / (hi - lo)
    uvo[:, 1] = 1 - uvo[:, 1]
    z = np.zeros(n) if curve is None else curve(o)
    c = o.mean(0)
    P, UV, F = [], [], []
    # front fan (centre + ring) and back fan
    for side, sz in ((0, 1.0), (1, -1.0)):
        base = len(P)
        cz = (0.0 if curve is None else float(curve(c[None, :])[0]))
        P.append((c[0], c[1], cz + sz * depth / 2))
        UV.append(tuple((c - lo) / (hi - lo) * np.array([1, -1]) + np.array([0, 1])))
        for i in range(n):
            inset = o[i] + (c - o[i]) * (bevel / max(np.linalg.norm(c - o[i]), 1e-6))
            P.append((inset[0], inset[1], z[i] + sz * depth / 2))
            UV.append(tuple(uvo[i]))
        for i in range(n):
            a, b = base + 1 + i, base + 1 + (i + 1) % n
            F.append((base, a, b) if sz > 0 else (base, b, a))
    # bevelled side band: front ring -> outer front -> outer back -> back ring
    ring = len(P)
    for i in range(n):
        P.append((o[i][0], o[i][1], z[i] + depth / 2 - bevel))
        P.append((o[i][0], o[i][1], z[i] - depth / 2 + bevel))
        UV.append((i / n, 0.0))
        UV.append((i / n, 1.0))
    for i in range(n):
        j = (i + 1) % n
        f0, f1 = 1 + i, 1 + j                  # front inset ring
        b0, b1 = n + 2 + i, n + 2 + j          # back inset ring
        o0, o1 = ring + 2 * i, ring + 2 * j
        p0, p1 = o0 + 1, o1 + 1
        F += [(f0, o0, f1), (f1, o0, o1), (o0, p0, o1), (o1, p0, p1), (p0, b0, p1), (p1, b0, b1)]
    return np.array(P), np.array(F, np.int64), np.array(UV)


def sphere(c, r, segs=12):
    prof = [(r * np.sin(a), -r * np.cos(a)) for a in np.linspace(0, np.pi, segs // 2 + 1)]
    P, F, UV = lathe(prof, segs)
    return P + np.asarray(c), F, UV


def transform(P, R=None, t=None):
    P = np.asarray(P, float)
    if R is not None:
        P = P @ np.asarray(R).T
    if t is not None:
        P = P + np.asarray(t)
    return P


def merge(parts):
    """[(P, F, UV, colour rgba8 or None)] -> (P, F, UV, C)."""
    Ps, Fs, UVs, Cs = [], [], [], []
    base = 0
    for P, F, UV, C in parts:
        Ps.append(P)
        Fs.append(F + base)
        UVs.append(UV)
        Cs.append(np.tile(np.array(C if C is not None else (0, 255, 0, 255), np.uint8), (len(P), 1)))
        base += len(P)
    return np.concatenate(Ps), np.concatenate(Fs), np.concatenate(UVs), np.concatenate(Cs)
