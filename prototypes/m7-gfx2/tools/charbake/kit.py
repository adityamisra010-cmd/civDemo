"""Helmets, hats, shields and weapons generated in code (lathe and extrusion shapes after historical forms; no game
asset or likeness is recreated). Rigid parts are bound to one bone: helmets to the head, weapons to the right-hand
grip socket, shields to the left-forearm socket (sockets from outfits.socket_frames)."""
import numpy as np

import garments as G
import meshutil
import prims
from outfits import socket_frames

WHITE = (0, 255, 0, 255)  # vertex colour: R trim 0, G occlusion 1


def place(P, sock):
    bone, c, R = sock
    return np.asarray(P) @ R.T + c, bone


def _bowl(b, rim_h, pad, apex=0.0, apex_pow=2.5, smooth=6, flat_top=0.0):
    """A helmet or hat bowl: the skull above `rim_h` (relative to the eyes), heavily smoothed and offset by `pad`
    (plus an apex term that raises the top into a cone or point). Returns (P, F, rim loop indices) in bind space."""
    I = b.I
    src, F = b.body
    V = b.V[src]
    eye_y = 0.5 * (I.eye_centres[0][1] + I.eye_centres[1][1])
    head = b.bc.headness[src] > 0.5
    y0 = eye_y + rim_h
    top = V[head, 1].max()
    region = np.where(head, y0 - V[:, 1], 1.0)
    P = meshutil.laplacian_smooth(V, F, iters=smooth)
    N = meshutil.vertex_normals(P, F)
    u = np.clip((P[:, 1] - y0) / max(top - y0, 1e-3), 0, 1)
    off = pad + apex * u ** apex_pow - flat_top * u ** 6
    P = P + N * off[:, None]
    g = G.finish(P, F, region, np.arange(len(P)), trim=0.0, hem=0.0035)
    loops = meshutil.boundary_loops(g["T"])
    loops = [lp for lp in loops if len(lp) > 8]
    rim = min(loops, key=lambda lp: g["pos"][lp, 1].mean()) if loops else []
    b.helm_c = np.array([0.0, eye_y, 0.5 * (V[head, 2].max() + V[head, 2].min())])
    b.helm_top = top
    return g["pos"], g["T"], rim


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def crested_edge(a):
    """Lower edge of the crested helmet (metres relative to the eyes) by azimuth a = |angle from the face front|
    (radians): the brow line 26 mm above the eyes; the cheek plates, from behind the outer eye corner (about 0.5 rad)
    to in front of the ear (about 1.34 rad), down to the mouth line (-75 mm); 45 mm above the eyes over the ear, so the
    cut clears the ear's bump in the smoothed skull; the nape (-30 mm) at the back."""
    return (0.026 - 0.101 * _smoothstep(0.62, 0.76, a) + 0.120 * _smoothstep(1.04, 1.24, a)
            - 0.075 * _smoothstep(1.86, 2.24, a))


def _shell_helmet(b, edge, pad, apex=0.0, apex_pow=2.5, smooth=6, proxy=20, flare=None, hem=0.0035, fair=6):
    """A helmet that is one shell over the identity's own head: the skull and face smoothed (`smooth` Taubin passes)
    and offset by `pad` (plus an apex term towards the crown and an optional outward `flare(a, y_rel)`), kept above
    the lower edge `edge(a)` (metres relative to the eyes, by azimuth from the face front). The edge is tested on a
    further-smoothed copy of the shell, so it is one clean curve over the face's unevenness; islands are dropped and
    the edge faired. Returns (P, F) in bind space and records the edge for the hair and beard views."""
    I = b.I
    src, F = b.body
    V = b.V[src]
    eye_y = 0.5 * (I.eye_centres[0][1] + I.eye_centres[1][1])
    headness = b.bc.headness[src]
    head = headness > 0.5
    c = np.array([0.0, eye_y, 0.5 * (V[head, 2].max() + V[head, 2].min())])
    top = V[head, 1].max()
    P = meshutil.laplacian_smooth(V, F, iters=smooth)
    N = meshutil.vertex_normals(P, F)
    y0 = eye_y + 0.02
    u = np.clip((P[:, 1] - y0) / max(top - y0, 1e-3), 0, 1)
    off = pad + apex * u ** apex_pow
    if flare is not None:
        a0 = np.abs(np.arctan2(P[:, 0] - c[0], P[:, 2] - c[2]))
        off = off + flare(a0, P[:, 1] - eye_y)
    P = P + N * off[:, None]
    Pf = meshutil.laplacian_smooth(P, F, iters=proxy)
    a = np.abs(np.arctan2(Pf[:, 0] - c[0], Pf[:, 2] - c[2]))
    # head weight smoothed over the skin, so where the head meets the neck the cut is not ragged either
    E = meshutil.adjacency(F, len(src))
    deg = np.zeros(len(src))
    np.add.at(deg, E[:, 0], 1)
    np.add.at(deg, E[:, 1], 1)
    deg[deg == 0] = 1
    hs = headness.copy()
    for _ in range(10):
        acc = np.zeros(len(src))
        np.add.at(acc, E[:, 0], hs[E[:, 1]])
        np.add.at(acc, E[:, 1], hs[E[:, 0]])
        hs = 0.5 * hs + 0.5 * acc / deg
    region = np.maximum((eye_y + edge(a)) - Pf[:, 1], 0.25 - hs)
    region = G.keep_largest(region, F, len(P))
    g = G.finish(P, F, region, np.arange(len(P)), trim=0.0, hem=hem, fair=fair)
    b.helm_c = c
    b.helm_top = top
    b.helm_edge = (c, eye_y, edge)
    return g["pos"], g["T"]


def _resample_loop(P, loop, n):
    """A closed rim loop resampled to n points, starting at the front (+Z) and running towards +X."""
    pts = P[np.array(loop + [loop[0]])]
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], n, endpoint=False)
    R = np.stack([np.interp(t, s, pts[:, k]) for k in range(3)], 1)
    c = R.mean(0)
    az = np.arctan2(R[:, 0] - c[0], R[:, 2] - c[2])
    k0 = int(np.argmin(np.abs(az)))
    R = np.roll(R, -k0, axis=0)
    if np.arctan2(R[1, 0] - c[0], R[1, 2] - c[2]) < 0:
        R = R[::-1]
        R = np.roll(R, 1, axis=0)
    return R, c


def _strip(rows):
    """Quad strip grid from a list of rows (each an (n,3) closed ring) -> (P, F); rings are closed."""
    n = len(rows[0])
    P = np.concatenate(rows)
    F = []
    for j in range(len(rows) - 1):
        for k in range(n):
            a = j * n + k
            b_ = j * n + (k + 1) % n
            F += [(a, b_, a + n), (b_, b_ + n, a + n)]
    return P, np.array(F, np.int64)


def _orient_out(P, F, centre):
    fn = np.cross(P[F[:, 1]] - P[F[:, 0]], P[F[:, 2]] - P[F[:, 0]])
    if ((P[F].mean(1) - centre) * fn).sum() < 0:
        F = F[:, ::-1]
    return F


def helmet(b, kind, mat, plume=None):
    """kinds: kettle (iron bowl with a wide sloping brim), nasal (pointed iron cap with a nose guard), cap (felt arming
    cap), crested (bronze bowl, cheek plates, neck guard, horsehair crest), tricorne (felt hat, brim turned up on three
    sides). Bowls are smoothed offset shells of the identity's own skull, so every helmet fits its wearer."""
    parts = []
    if kind == "kettle":
        P, F, rim = _bowl(b, 0.024, 0.016, apex=0.02, apex_pow=4.0)
        parts.append((P, F, np.zeros((len(P), 2)), None))
        R, c = _resample_loop(P, rim, 64)
        d = R - np.array([c[0], R[0, 1] * 0 + c[1], c[2]])
        d[:, 1] = 0
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        rows = [R, R + d * 0.045 + np.array([0, -0.018, 0]), R + d * 0.066 + np.array([0, -0.031, 0]),
                R + d * 0.070 + np.array([0, -0.036, 0])]
        Pb, Fb = _strip(rows)
        # a thickness: the underside, 3 mm below
        Pu = Pb + np.array([0, -0.003, 0])
        Fu = Fb[:, ::-1] + len(Pb)
        parts.append((np.vstack([Pb, Pu]), np.vstack([Fb, Fu]), np.zeros((2 * len(Pb), 2)), None))
        b.hide_hair_above(0.024)
    elif kind == "nasal":
        P, F, rim = _bowl(b, 0.028, 0.013, apex=0.05, apex_pow=2.2)
        parts.append((P, F, np.zeros((len(P), 2)), None))
        R, c = _resample_loop(P, rim, 64)
        front = R[0]
        out = front - np.array([c[0], front[1], c[2]])
        out /= np.linalg.norm(out)
        bar = [front + out * 0.002 + np.array([0, 0.012, 0]), front + out * 0.010 + np.array([0, -0.050, 0])]
        Pn, Fn, UVn = prims.tube(bar, [0.0065, 0.0055], segs=8)
        Pn = (Pn - front) * np.array([2.0, 1.0, 0.6]) + front
        parts.append((Pn, Fn, UVn, None))
        # a brow band around the rim, standing 1.5 mm proud of the bowl
        d = R - np.array([c[0], 0, c[2]])
        d[:, 1] = 0
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        Pr, Fr = _strip([R + d * 0.0015, R + d * 0.0015 + np.array([0, 0.018, 0])])
        parts.append((Pr, _orient_out(Pr, Fr, np.array([c[0], R[:, 1].mean(), c[2]])), np.zeros((len(Pr), 2)), None))
        b.hide_hair_above(0.028)
    elif kind == "cap":
        P, F, rim = _bowl(b, 0.050, 0.007, apex=0.004)
        parts.append((P, F, np.zeros((len(P), 2)), None))
        R, c = _resample_loop(P, rim, 64)
        d = R - np.array([c[0], 0, c[2]])
        d[:, 1] = 0
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        rows = [R + np.array([0, 0.012, 0]) + d * 0.004, R + d * 0.010 + np.array([0, 0.004, 0]), R + d * 0.006 - np.array([0, 0.004, 0])]
        Pb, Fb = _strip(rows)
        parts.append((Pb, _orient_out(Pb, Fb, np.array([c[0], R[:, 1].mean(), c[2]])), np.zeros((len(Pb), 2)), None))
        b.hide_hair_above(0.050)
    elif kind == "crested":
        # one bronze shell over the wearer's own skull and face: bowl, cheek plates and nape in a single surface whose
        # lower edge is crested_edge (the earlier rim-and-hanging-plates build crossed the ears and came out torn);
        # the nape flares out by up to 12 mm, the cheek plates by 4 mm at their foot
        def flare(a, y):
            nape = _smoothstep(1.9, 2.4, a) * _smoothstep(0.02, -0.03, y) * 0.012
            cheek = _smoothstep(0.6, 0.8, a) * (1 - _smoothstep(1.1, 1.3, a)) * _smoothstep(-0.02, -0.075, y) * 0.004
            return nape + cheek
        P, F = _shell_helmet(b, crested_edge, 0.015, apex=0.010, smooth=12, proxy=30, flare=flare)
        parts.append((P, F, np.zeros((len(P), 2)), None))
    elif kind == "tricorne":
        P, F, rim = _bowl(b, 0.030, 0.014, apex=0.012, flat_top=0.01)
        parts.append((P, F, np.zeros((len(P), 2)), None))
        R, c = _resample_loop(P, rim, 96)
        d = R - np.array([c[0], 0, c[2]])
        d[:, 1] = 0
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        az = np.arctan2(d[:, 0], d[:, 2])
        corner = (0.5 + 0.5 * np.cos(3 * az)) ** 6     # 1 only at the three corners (front and two back corners)
        rows = [R]
        for j, f in enumerate((0.3, 0.6, 0.85, 1.0)):
            # the brim is cocked up steeply between the corners and stays out at the points, which leaves the
            # triangular plan of the hat; the turned-up sides lean slightly outwards
            w = (0.040 + 0.040 * corner) * f + 0.010 * f
            lift = (0.085 * (1 - corner)) * f ** 1.4 + 0.004 * f
            rows.append(R + d * w[:, None] + np.array([0, 1.0, 0]) * lift[:, None])
        Pb, Fb = _strip(rows)
        Pu = Pb - (np.array([0, 0.003, 0]))
        parts.append((np.vstack([Pb, Pu]), np.vstack([Fb, Fb[:, ::-1] + len(Pb)]), np.zeros((2 * len(Pb), 2)), None))
        b.hide_hair_above(0.030)
    Pm, Fm, UVm, Cm = prims.merge(parts)
    if kind == "tricorne":
        # lace on the outer brim edge: vertex colour R marks the woven-border colour
        n = 96
        Cm[len(P) + 4 * n: len(P) + 5 * n, 0] = 255
    b.rigid("helmet", Pm, Fm, UVm, Cm, "head", mat)
    if plume:
        crest(b, plume)


def crest(b, colour):
    """A horsehair crest: hair cards along the helmet's top from front to back (hair shader, dyed)."""
    c = b.helm_c
    top = b.helm_top + 0.015
    P, F, UV = [], [], []
    rng = np.random.default_rng(7)
    for i in range(40):
        t = -0.9 + 1.8 * i / 39
        z = c[2] + t * 0.12
        y = c[1] + (top - c[1]) * np.sqrt(max(1 - t * t * 0.8, 0.05))
        root = np.array([0.0, y + 0.01, z])
        tip = root + np.array([0.0, 0.05 + 0.02 * (1 - abs(t)), 0.0]) + np.array([0, 0, -0.06 * (t + 0.3) - 0.03])
        side = np.array([0.012 + 0.006 * rng.random(), 0, 0])
        base = len(P)
        for p in (root - side, root + side, tip - side * 1.5, tip + side * 1.5):
            P.append(p)
        u0 = 0.0 + 0.2 * rng.random()
        UV += [(u0, 0.0), (u0 + 0.05, 0.0), (u0, 1.0), (u0 + 0.05, 1.0)]
        F += [(base, base + 2, base + 1), (base + 1, base + 2, base + 3)]
    m = b.rigid("crest", np.array(P), np.array(F, np.int64), np.array(UV), np.tile(np.array([255, 255, 255, 230], np.uint8), (len(P), 1)), "head",
                {"kind": "hair", "colour": colour})
    return m


def shield(b, kind, emblem_tex, rim_metal, back_wood):
    """Shield on the left forearm: painted face (planar UVs for the emblem), rim, boss (round kinds), wooden back."""
    sock = socket_frames(b.I)["shield_l"]
    if kind in ("round", "aspis"):
        r = 0.45 if kind == "aspis" else 0.40
        outline = [(r * np.sin(a), r * np.cos(a)) for a in np.linspace(0, 2 * np.pi, 48, endpoint=False)]
        curve = (lambda o: 0.05 * (1 - (o ** 2).sum(1) / (r * r))) if kind == "aspis" else (lambda o: 0.03 * (1 - (o ** 2).sum(1) / (r * r)))
    elif kind == "heater":
        outline = []
        for a in np.linspace(-1, 1, 12):
            outline.append((0.31 * a, 0.36 - 0.02 * a * a))
        for t in np.linspace(0, 1, 18)[1:]:
            x = 0.31 * (1 - t) ** 0.6
            outline.append((x, 0.36 - 0.80 * t))
        for t in np.linspace(1, 0, 18)[1:-1]:
            x = -0.31 * (1 - t) ** 0.6
            outline.append((x, 0.36 - 0.80 * t))
        outline = outline[:12] + outline[12:29] + outline[29:]
        curve = lambda o: 0.06 * (1 - (o[:, 0] / 0.32) ** 2)
    else:  # kite
        outline = []
        for a in np.linspace(np.pi, 0, 16):
            outline.append((0.26 * np.cos(a), 0.40 + 0.10 * np.sin(a)))
        for t in np.linspace(0, 1, 20)[1:]:
            outline.append((0.26 * (1 - t) ** 0.8, 0.40 - 1.05 * t))
        for t in np.linspace(1, 0, 20)[1:-1]:
            outline.append((-0.26 * (1 - t) ** 0.8, 0.40 - 1.05 * t))
        curve = lambda o: 0.07 * (1 - (o[:, 0] / 0.27) ** 2)
    o = np.array(outline)
    # local frame: face normal +Z (outward), +Y along the forearm. Long shields are strapped across the forearm:
    # their long axis is turned 70 degrees from it, so a level forearm holds them nearly upright.
    if kind in ("heater", "kite"):
        a = np.radians(70.0)
        Rz = np.array([[np.cos(a), -np.sin(a)], [np.sin(a), np.cos(a)]])
        o = o @ Rz.T
        f0 = curve
        curve = lambda q, f0=f0, Rz=Rz: f0(q @ Rz)
    P, F, UV = prims.extrude(o, 0.016, bevel=0.004, curve=curve)
    lo, hi = o.min(0), o.max(0)
    # face (front fan triangles) vs back and sides: split by which vertices they use
    n = len(o)
    front = F[: n]
    rest = F[n:]
    Pw, bone = place(P, sock)
    # vertex colour R on the painted face: 0 at the centre, 255 on the outline (the fan interpolates it radially),
    # so paint chips and edge wear gather at the rim
    Cf = np.tile(np.array(WHITE, np.uint8), (len(P), 1))
    Cf[1:n + 1, 0] = 255
    b.rigid("shield_face", Pw, front, UV, Cf, bone, {"kind": "paint", "emblem": emblem_tex})
    b.rigid("shield_back", Pw, rest, UV, np.tile(np.array(WHITE, np.uint8), (len(P), 1)), bone, back_wood)
    # rim: a tube along the outline
    path = [np.array([x, y, float(curve(np.array([[x, y]]))[0]) + 0.002]) for x, y in o] + [None]
    path[-1] = path[0]
    Pr, Fr, UVr = prims.tube(np.array(path), 0.009, segs=6)
    Prw, _ = place(Pr, sock)
    parts = [(Prw, Fr, UVr, None)]
    if kind in ("round", "aspis"):
        Pb, Fb, UVb = prims.lathe([(0.0, 0.055), (0.035, 0.05), (0.07, 0.03), (0.085, 0.022)], 16)
        R90 = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0.0]])
        Pb = Pb @ R90.T
        Pbw, _ = place(Pb, sock)
        parts.append((Pbw, Fb, UVb, None))
    Pm, Fm, UVm, Cm = prims.merge(parts)
    b.rigid("shield_rim", Pm, Fm, UVm, Cm, bone, rim_metal)


def spear(b, metal, wood, length=2.4):
    sock = socket_frames(b.I)["grip_r"]
    butt = -0.95
    shaft = [(0.0, butt, 0.0), (0.0, butt + length - 0.25, 0.0)]
    Ps, Fs, UVs = prims.tube(shaft, [0.0155, 0.0135], segs=10)
    # leaf-shaped head (a flattened lathe) and a butt spike
    prof = [(0.014, 0.0), (0.016, 0.06), (0.028, 0.11), (0.030, 0.15), (0.020, 0.21), (0.0, 0.29)]
    Ph, Fh, UVh = prims.lathe(prof, 12)
    Ph[:, 0] *= 1.25
    Ph[:, 2] *= 0.30
    Ph[:, 1] += butt + length - 0.27
    prof2 = [(0.016, 0.0), (0.014, -0.04), (0.004, -0.10)]
    Pb, Fb, UVb = prims.lathe(prof2, 10)
    Pb[:, 1] += butt
    wood_mesh = place(Ps, sock)
    b.rigid("spear_shaft", wood_mesh[0], Fs, UVs * np.array([1.0, length * 4]), np.tile(np.array(WHITE, np.uint8), (len(Ps), 1)), wood_mesh[1], wood)
    Pm, Fm, UVm, Cm = prims.merge([(Ph, Fh, UVh, None), (Pb, Fb, UVb, None)])
    Pmw, bone = place(Pm, sock)
    b.rigid("spear_head", Pmw, Fm, UVm, Cm, bone, metal)


def sword(b, metal, grip_mat, length=0.80):
    sock = socket_frames(b.I)["grip_r"]
    blade = length - 0.18
    # blade: a flat lathe-like profile, tapering to a point, with a fuller groove suggested by vertex colour
    prof = [(0.024, 0.0), (0.024, blade * 0.25), (0.021, blade * 0.75), (0.010, blade * 0.95), (0.0, blade)]
    Pb, Fb, UVb = prims.lathe(prof, 8)
    Pb[:, 2] *= 0.16
    Pb[:, 1] += 0.065
    guard = prims.tube([(-0.10, 0.05, 0.0), (0.10, 0.05, 0.0)], 0.011, segs=8, up=(0, 1, 0))
    pommel = prims.sphere((0.0, -0.11, 0.0), 0.026, segs=10)
    grip = prims.tube([(0.0, -0.09, 0.0), (0.0, 0.045, 0.0)], [0.016, 0.017], segs=8)
    Pm, Fm, UVm, Cm = prims.merge([(Pb, Fb, UVb, None), guard + (None,), pommel + (None,)])
    Pmw, bone = place(Pm, sock)
    b.rigid("sword", Pmw, Fm, UVm, Cm, bone, metal)
    Pg, bone = place(grip[0], sock)
    b.rigid("sword_grip", Pg, grip[1], grip[2], np.tile(np.array(WHITE, np.uint8), (len(Pg), 1)), bone, grip_mat)
