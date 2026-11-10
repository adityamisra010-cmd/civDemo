"""Outfits: garment layers, armour, helmets, shields and weapons for one identity, plus the outfit's views of the
identity's meshes (visible skin, scalp hair under a helmet). See garments.py for how shells are cut and hemmed."""
import numpy as np

import garments as G
import meshutil
import prims
from fields import smoothstep
from rig import INDEX, quantize_weights


def group_surface(A, group):
    fq = A.mesh.group_faces(group)
    Fb = meshutil.quads_to_tris(A.mesh.fv[fq])
    src = np.unique(Fb)
    remap = -np.ones(len(A.mesh.V), np.int64)
    remap[src] = np.arange(len(src))
    return src, remap[Fb]


class Builder:
    def __init__(self, ident):
        self.I = ident
        self.A = ident.A
        self.bc = G.BodyCoords(ident)
        self.body = group_surface(self.A, "body")
        self.tights = group_surface(self.A, "helper-tights")
        V = ident.Vm
        self.V = V
        self.N = ident.N
        h = ident.heads
        self.h = h
        self.arm_len = np.linalg.norm(h["lowerarm_l"] - h["upperarm_l"])
        self.fore_len = np.linalg.norm(h["hand_l"] - h["lowerarm_l"])
        self.thigh_len = np.linalg.norm(h["calf_l"] - h["thigh_l"])
        self.shin_len = np.linalg.norm(h["foot_l"] - h["calf_l"])
        self.covered = np.zeros(len(V), bool)
        self.meshes = []
        self.materials = {}
        self.leg_layers = []  # positions of legwear already made (braies, hose, boot shafts): skirts must clear them

    # ------------------------------------------------------------------ fields (<= 0 keeps)
    def f_neck(self, r=0.078, front_drop=0.0, back_raise=0.0):
        """Collar: keep outside an ellipsoid around the neck base, and never above it (the head is excluded)."""
        c = self.h["neck_01"] + np.array([0.0, -0.015, 0.0])
        d = self.V - c
        dist = np.linalg.norm(d * np.array([1.0, 1.25, 1.1]), axis=1)
        front = smoothstep(-0.02, 0.06, d[:, 2])
        collar = (r + front_drop * front + back_raise * (1 - front)) - dist
        above = d[:, 1] - 0.035          # nothing of a body garment rises 3.5 cm above the neck base
        return np.maximum(np.maximum(collar, above), self.bc.headness - 0.3)

    def f_sleeve(self, length):
        s = self.bc.arm_s()
        return np.where(self.bc.armness >= 0.5, s - length, -1.0)

    def f_above(self, y):  # keep above y
        return y - self.V[:, 1]

    def f_below(self, y):  # keep below y
        return self.V[:, 1] - y

    def f_torso(self, legs=0.45):
        return self.bc.legness - legs

    def f_noarms(self, a=0.35):
        return self.bc.armness - a

    def f_armhole(self, a=0.3, iters=12):
        """An armhole cut by the skin's arm weight smoothed over the mesh (`iters` neighbour averages): the raw weights
        vary from vertex to vertex near the shoulder, so their iso-line is ragged; the smoothed field's is a clean curve."""
        cache = self.__dict__.setdefault("_arm_smooth", {})
        if iters not in cache:
            src, F = self.body
            E = meshutil.adjacency(F, len(src))
            v = self.bc.armness[src].copy()
            deg = np.zeros(len(src))
            np.add.at(deg, E[:, 0], 1)
            np.add.at(deg, E[:, 1], 1)
            deg[deg == 0] = 1
            for _ in range(iters):
                acc = np.zeros(len(src))
                np.add.at(acc, E[:, 0], v[E[:, 1]])
                np.add.at(acc, E[:, 1], v[E[:, 0]])
                v = 0.5 * v + 0.5 * acc / deg
            full = np.zeros(len(self.V))
            full[src] = v
            cache[iters] = full
        return cache[iters] - a

    def f_leg_below(self, s):  # keep leg_s >= s (towards the foot)
        return np.where(self.bc.legness > 0.3, s - self.bc.leg_s(), 1.0)

    # ------------------------------------------------------------------ layers
    def add(self, mesh, material, covered=None, morph=True):
        mesh["material"] = material["kind"]
        self.materials[mesh["name"]] = material
        self.meshes.append((mesh, G.morphs_for(self.I, mesh) if morph else []))
        if covered is not None:
            self.covered |= covered

    def body_shell(self, name, region, offset, material, smooth=2, stiff=0.0, hide_margin=0.012, secondary=None,
                   trim=0.0, trim_repeat=0.036, hem=0.004, gather=None, field=None, field_smooth=20, islands=False,
                   fair=0):
        """A garment shell of the skin (garments.build_shell); the skin it covers by `hide_margin` leaves the outfit's
        visible-skin view. field/field_smooth/islands/fair: see build_shell (the region they produce is the one used
        for the covered skin too)."""
        src, F = self.body
        sec = None if secondary is None else secondary[src]
        mesh, used = G.build_shell(self.I, src, F, region[src], offset, smooth=smooth, name=name, hem=hem, secondary=sec,
                                   stiff=stiff, trim=trim, trim_repeat=trim_repeat, gather=gather, field=field,
                                   field_smooth=field_smooth, islands=islands, fair=fair, return_region=True)
        cov = np.zeros(len(self.V), bool)
        cov[src] = used < -hide_margin
        self.add(mesh, material, cov)
        return mesh

    def body_section(self, y, S=96, band=0.018):
        """Radius of the body (arms excluded) and of the legwear already made, by azimuth around the pelvis axis at
        height y (m); gaps interpolated."""
        c = self.h["pelvis"]
        bsrc = self.body[0]
        Vb = self.V[bsrc]
        ok = (np.abs(Vb[:, 1] - y) < band) & (self.bc.armness[bsrc] < 0.25)
        P = Vb[ok]
        if self.leg_layers:
            # legwear made from the tights helper stands 6 to 22 mm off the skin: a skirt fitted to the skin alone
            # hung inside the hose at the thighs and hips
            L = np.concatenate(self.leg_layers)
            P = np.concatenate([P, L[np.abs(L[:, 1] - y) < band]])
        az = np.arctan2(P[:, 0] - c[0], P[:, 2] - c[2])
        rr = np.hypot(P[:, 0] - c[0], P[:, 2] - c[2])
        bins = np.linspace(-np.pi, np.pi, S, endpoint=False)
        r = np.zeros(S)
        for k, a in enumerate(bins):
            da = np.abs((az - a + np.pi) % (2 * np.pi) - np.pi)
            m = da < 2.2 * np.pi / S
            r[k] = rr[m].max() if m.any() else 0.0
        have = r > 0
        if have.any() and not have.all():
            idx = np.nonzero(have)[0]
            r = np.interp(np.arange(S), idx, r[idx], period=S)
        for _ in range(2):
            r = 0.25 * np.roll(r, 1) + 0.5 * r + 0.25 * np.roll(r, -1)
        return r

    def skirt(self, name, top_y, hem_y, clearance, flare, material, pteruges=0, trim=0.0, folds=1.0, seed=0,
              front_slit=0.0, top_clear=None, S=96, R=26):
        """A skirt fitted to the body and hanging from the hips: rings from top_y to hem_y around the pelvis axis.
        Below the widest point the cloth hangs straight (cumulative maximum of the body section) and flares towards
        the hem; hanging folds (three sine waves with fixed random phases, outward only) grow towards the hem.
        Skinned to the pelvis at the top, sharing with each thigh towards the hem by side."""
        c = self.h["pelvis"]
        rng = np.random.default_rng(seed + 977 * sum(map(ord, name)))
        ys = np.linspace(top_y, hem_y, R + 1)
        az = np.linspace(-np.pi, np.pi, S, endpoint=False)
        base = np.zeros((R + 1, S))
        run = np.zeros(S)
        for i, y in enumerate(ys):
            run = np.maximum(run, self.body_section(y, S))
            base[i] = run
        circ = float(2 * np.pi * base[-1].mean())
        n1 = max(5, int(round(circ / 0.13)))
        ph = rng.random(3) * 2 * np.pi
        wave = 0.6 * np.sin(n1 * az + ph[0]) + 0.3 * np.sin((n1 + 3) * az * 0.5 + ph[1]) + 0.15 * np.sin(2.3 * n1 * az + ph[2])
        wave = (wave - wave.min()) / (wave.max() - wave.min())
        tc = clearance if top_clear is None else top_clear
        P, UV = [], []
        for i, y in enumerate(ys):
            t = i / R
            amp = folds * (0.010 + 0.45 * flare) * t ** 1.3
            r = base[i] + tc + (clearance - tc) * min(1.0, t * 3) + flare * t ** 1.5 + amp * wave
            for k in range(S):
                P.append((c[0] + r[k] * np.sin(az[k]), y, c[2] + r[k] * np.cos(az[k])))
                UV.append((k / S, t))
        P = np.array(P)
        F = []
        for i in range(R):
            for k in range(S):
                a, b = i * S + k, i * S + (k + 1) % S
                F += [(a, a + S, b), (b, a + S, b + S)]
        F = np.array(F, np.int64)
        t_all = np.repeat(np.arange(R + 1) / R, S)
        az_all = np.tile(az, R + 1)
        region = np.full(len(P), -1.0)
        if pteruges:
            lower = smoothstep(0.25, 0.32, t_all)
            region = np.maximum(region, (np.cos(az_all * pteruges) - 0.6) * 0.06 * lower - 0.0005 * (1 - lower))
        if front_slit > 0:  # an open front from the hem up (riding coats)
            front = np.cos(az_all)
            region = np.maximum(region, (front - np.cos(front_slit)) * 0.2 * smoothstep(0.42, 0.55, t_all) - 0.002 * (1 - smoothstep(0.42, 0.55, t_all)))
        g = G.finish(P, F, region, np.arange(len(P)), trim=trim, hem=0.004)
        # weights by side and by absolute height (from the hips to the knee, the same for every skirt): skirts worn one
        # over another then carry the same weights at the same height and move together. (Measured along each skirt's
        # own length, a short mail skirt followed the thigh more than the long surcoat over it and showed through it.)
        Pg = g["pos"]
        y0 = self.bc.hip_y + 0.03
        tt = np.clip((y0 - Pg[:, 1]) / max(y0 - self.bc.knee_y, 1e-3), 0, 1)
        W = np.zeros((len(Pg), len(INDEX)))
        side = smoothstep(-0.06, 0.06, Pg[:, 0] - c[0])
        W[:, INDEX["pelvis"]] = 1 - 0.6 * tt
        W[:, INDEX["thigh_l"]] = 0.6 * tt * side
        W[:, INDEX["thigh_r"]] = 0.6 * tt * (1 - side)
        top = 1 - smoothstep(0.0, 0.2, tt)
        W[:, INDEX["spine_01"]] = 0.4 * top
        W[:, INDEX["pelvis"]] *= 1 - 0.4 * top
        mesh = G.to_mesh(name, "cloth", g, W)
        mesh["src"] = np.zeros(len(Pg), np.int64)
        mesh["flags"] = 1  # skirt: the runtime pushes it out of the leg capsules
        self.add(mesh, material, None, morph=False)
        return mesh

    def footwear(self, name, collar, material, shaft_top=None, clearance=0.006, sole=0.012, over=None):
        """Shoes (and boot shafts): per foot a loft from heel to toe fitted to that foot's vertices (super-ellipse
        sections over a flat sole, rounded toe box and heel), opened at the collar height; a boot shaft is an offset
        shell of the leg from the ankle to `shaft_top` (distance along the leg from the hip joint, m). over: the offset
        of legwear made from the tights helper (trousers) that the shaft is worn over, or None (bare leg)."""
        h = self.h
        bsrc = self.body[0]
        Vb = self.V[bsrc]
        legs = self.bc.legness[bsrc]
        cov = np.zeros(len(self.V), bool)
        parts = []
        for sd in ("l", "r"):
            fh, bh = h[f"foot_{sd}"], h[f"ball_{sd}"]
            f = bh - fh
            f[1] = 0
            f /= np.linalg.norm(f)
            lat = np.cross(np.array([0.0, 1.0, 0.0]), f)
            on = (legs > 0.5) & (Vb[:, 1] < fh[1] + 0.06) & ((Vb[:, 0] > 0) == (sd == "l"))
            Q = Vb[on]
            al = (Q - fh) @ f
            la = (Q - fh) @ lat
            a0, a1 = al.min() - clearance, al.max() + clearance
            K, M = 16, 28
            P, U = [], []
            us = np.linspace(0, 1, K + 1)
            for i, u in enumerate(us):
                a = a0 + (a1 - a0) * u
                m = np.abs(al - a) < (a1 - a0) / K * 1.2
                if m.any():
                    lo, hi, top = la[m].min(), la[m].max(), Q[m, 1].max()
                else:
                    lo, hi, top = -0.03, 0.03, 0.05
                cl = 0.5 * (lo + hi)
                w = 0.5 * (hi - lo) + clearance
                H = top + clearance
                # rounded ends: shrink the first and last sections
                endk = min(u, 1 - u)
                shrink = np.sqrt(np.clip(endk / 0.06, 0.0, 1.0))
                w *= 0.55 + 0.45 * shrink
                for j in range(M):
                    th = np.pi * j / (M - 1)  # 0 .. pi over the upper arch, sole across the bottom
                    x = cl + w * np.cos(th)
                    yy = sole * 0.6 + (H - sole * 0.6) * (np.sin(th) ** 0.6) * (0.75 + 0.25 * shrink)
                    P.append(fh + f * a + lat * x + np.array([0.0, yy - fh[1], 0.0]))
                    U.append((u, j / (M - 1)))
                for j in range(1, 7):  # flat sole back across
                    x = cl - w + 2 * w * j / 7
                    P.append(fh + f * a + lat * x + np.array([0.0, -fh[1] + 0.001, 0.0]))
                    U.append((u, 1.0 + j / 7))
            P = np.array(P)
            ring = M + 6
            Fs = []
            for i in range(K):
                for j in range(ring):
                    a_, b_ = i * ring + j, i * ring + (j + 1) % ring
                    Fs += [(a_, b_, a_ + ring), (b_, b_ + ring, a_ + ring)]
            # caps at heel and toe (fans)
            for i, flip in ((0, True), (K, False)):
                c_ = len(P)
                P = np.vstack([P, P[i * ring:(i + 1) * ring].mean(0)])
                for j in range(ring):
                    a_, b_ = i * ring + j, i * ring + (j + 1) % ring
                    Fs.append((a_, c_, b_) if flip else (a_, b_, c_))
            Fs = np.array(Fs, np.int64)
            # fix winding outward: compare with the centroid
            cen = P.mean(0)
            fn = np.cross(P[Fs[:, 1]] - P[Fs[:, 0]], P[Fs[:, 2]] - P[Fs[:, 0]])
            if ((P[Fs].mean(1) - cen) * fn).sum() < 0:
                Fs = Fs[:, ::-1]
            # collar: open the top behind the instep, where the leg leaves the shoe
            alP = (P - fh) @ f
            # cut (region > 0) where the section is above the collar AND behind the instep: min of two smooth fields
            region = np.minimum(P[:, 1] - collar, al.max() * 0.35 - alP)
            g = G.finish(P, Fs, region, np.arange(len(P)), trim=0.0, hem=0.003,
                         secondary=(P[:, 1] < sole * 1.05).astype(float))
            Pg = g["pos"]
            alg = (Pg - fh) @ f
            ball_a = (bh - fh) @ f
            wb = smoothstep(ball_a - 0.015, ball_a + 0.015, alg)
            W = np.zeros((len(Pg), len(INDEX)))
            W[:, INDEX[f"ball_{sd}"]] = wb
            W[:, INDEX[f"foot_{sd}"]] = 1 - wb
            parts.append(G.to_mesh(f"{name}_{sd}", "cloth", g, W))
            covm = np.zeros(len(self.V), bool)
            covm[bsrc[on & (Vb[:, 1] < collar - 0.012)]] = True
            cov |= covm
        for m in parts:
            m["src"] = np.zeros(len(m["pos"]), np.int64)
            self.add(m, material, None, morph=False)
        self.covered |= cov
        if shaft_top is not None:
            ank = 0.5 * (h["foot_l"][1] + h["foot_r"][1])
            region = np.where(self.bc.legness > 0.3, np.maximum(shaft_top - self.bc.leg_s(), (ank - 0.035) - self.V[:, 1]), 1.0)
            if over is None:
                shaft = self.body_shell(name + "_shaft", region, clearance + 0.002, material, smooth=3)
                self.leg_layers.append(shaft["pos"])
            else:
                # over hose: the shaft is the hose's own surface (the tights helper, which stands 6 to 22 mm off the
                # skin) 4 mm further out, so the two layers keep their order wherever the helper sits on the leg (a
                # shaft offset from the skin lay under the hose in places and showed through it in patches)
                src, F = self.tights
                mesh = G.build_shell(self.I, src, F, region[src], over + 0.004, smooth=1, name=name + "_shaft", hem=0.003)
                self.add(mesh, material, None)
                self.leg_layers.append(mesh["pos"])

    def trousers(self, name, top_y, bottom_s, offset, material):
        """bottom_s: distance along the leg from the hip joint (m) where the legwear ends."""
        src, F = self.tights
        P = self.V[src]
        legs = self.bc.leg_s()[src]
        region = np.maximum(P[:, 1] - top_y, np.where(self.bc.legness[src] > 0.3, legs - bottom_s, -1.0))
        region = np.maximum(region, self.bc.armness[src] - 0.3)
        mesh = G.build_shell(self.I, src, F, region, offset, smooth=1, name=name, hem=0.003)
        # skin under the trousers (evaluated on the body with the same coordinates)
        bsrc, _ = self.body
        cov = np.zeros(len(self.V), bool)
        Pb = self.V[bsrc]
        rb = np.maximum(Pb[:, 1] - (top_y - 0.03), np.where(self.bc.legness[bsrc] > 0.3, self.bc.leg_s()[bsrc] - (bottom_s - 0.03), -1.0))
        rb = np.maximum(rb, self.bc.armness[bsrc] - 0.3)
        cov[bsrc] = rb < 0
        self.add(mesh, material, cov)
        self.leg_layers.append(mesh["pos"])
        return mesh

    # ------------------------------------------------------------------ rigid parts (bound to one bone)
    def rigid(self, name, P, F, UV, C, bone, material):
        n = len(P)
        N = meshutil.vertex_normals(P, F)
        joints = np.zeros((n, 4), np.uint8)
        joints[:, 0] = INDEX[bone]
        weights = np.zeros((n, 4), np.uint8)
        weights[:, 0] = 255
        tan = meshutil.tangents(P, N, UV, F)
        mesh = {"name": name, "material": material["kind"], "pos": P, "nrm": N, "tan": tan, "uv": UV, "color": C,
                "joints": joints, "weights": weights, "idx": F.ravel(), "src": np.zeros(n, np.int64)}
        self.materials[name] = material
        self.meshes.append((mesh, []))
        return mesh

    def hide_hair_above(self, rim_h):
        self.hair_rim = rim_h


def socket_frames(ident):
    """Rest-pose sockets: right-hand grip, left-hand grip, left-forearm shield strap (positions, rotation matrices)."""
    h = ident.heads
    out = {}
    for s in ("r", "l"):
        hand = h[f"hand_{s}"]
        idx, mid, ring, pin = (h[f"{f}_01_{s}"] for f in ("index", "middle", "ring", "pinky"))
        across = idx - pin
        across /= np.linalg.norm(across)
        fing = mid - hand
        fing /= np.linalg.norm(fing)
        n = np.cross(fing, across)
        n /= np.linalg.norm(n)
        if np.dot(h[f"thumb_02_{s}"] - idx, n) < 0:
            n = -n
        centre = 0.5 * (hand + mid) + n * 0.022 + fing * 0.012
        y = across                          # weapon axis (+Y) runs from the little finger to the index side
        z = n - np.dot(n, y) * y
        z /= np.linalg.norm(z)
        x = np.cross(y, z)
        out[f"grip_{s}"] = (f"hand_{s}", centre, np.stack([x, y, z], 1))
    el, wr = h["lowerarm_l"], h["hand_l"]
    axis = (wr - el) / np.linalg.norm(wr - el)
    # the back of the forearm faces away from the left palm's normal
    _, _, Rg = out["grip_l"]
    palm = Rg[:, 2]
    back = -(palm - np.dot(palm, axis) * axis)
    back /= np.linalg.norm(back)
    centre = el + (wr - el) * 0.55 + back * 0.05
    x = np.cross(axis, back)
    out["shield_l"] = ("lowerarm_l", centre, np.stack([x, axis, back], 1))
    return out
