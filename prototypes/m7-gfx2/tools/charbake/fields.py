"""Landmarks and smooth scalar fields on an identity's rest mesh (metres): scalp, beard, brows, redness, T-zone,
thin parts and cavities. They drive hair placement and the per-identity skin maps, and they are smooth by
construction (products of smoothsteps of distances), so every boundary they define is a smooth curve."""
import numpy as np

import meshutil
import uvraster
from rig import INDEX


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


class Fields:
    def __init__(self, ident, masks1024):
        A = ident.A
        self.I = ident
        V = ident.Vm
        self.V = V
        m = A.mesh
        body = m.group_verts("body")
        self.body = body
        # per-vertex mask samples from the MPFB2 CC0 region masks (max over a vertex's UV corners)
        fv, fuv = A.body_fv.ravel(), A.body_fuv.ravel()
        uv = A.mesh.UV[fuv].copy()
        uv[:, 1] = 1 - uv[:, 1]
        self.vmask = {}
        for k, tex in masks1024.items():
            s = uvraster.sample(tex, uv)
            acc = np.zeros(len(V))
            np.maximum.at(acc, fv, s)
            self.vmask[k] = acc
        self.N = ident.N
        h = ident.heads
        self.eye_l, self.eye_r = ident.eye_centres
        self.eye_y = 0.5 * (self.eye_l[1] + self.eye_r[1])
        self.eye_z = 0.5 * (self.eye_l[2] + self.eye_r[2])
        self.eye_x = 0.5 * (self.eye_l[0] - self.eye_r[0])
        hb = V[body]
        mid = np.abs(hb[:, 0]) < 0.006
        nose_band = mid & (hb[:, 1] < self.eye_y - 0.01) & (hb[:, 1] > self.eye_y - 0.08)
        self.nose = hb[nose_band][np.argmax(hb[nose_band][:, 2])]
        lips = self.vmask["lips"] > 0.5
        self.mouth = V[lips].mean(0) if lips.any() else self.nose - np.array([0, 0.035, 0.01])
        chin_band = mid & (hb[:, 1] < self.mouth[1] - 0.015) & (hb[:, 1] > self.mouth[1] - 0.09) & (hb[:, 2] > self.mouth[2] - 0.05)
        cb = hb[chin_band]
        self.chin = cb[np.argmax(cb[:, 2] - 0.6 * (cb[:, 1] - cb[:, 1].min()))] if len(cb) else self.mouth - np.array([0, 0.05, 0])
        self.chin_low = cb[np.argmin(cb[:, 1])] if len(cb) else self.chin
        ears = self.vmask["ears"] > 0.5
        self.ear_l = V[ears & (V[:, 0] > 0)].mean(0)
        self.ear_r = V[ears & (V[:, 0] < 0)].mean(0)
        self.head_top = V[body][np.argmax(V[body][:, 1])]
        self.neck = h["neck_01"]
        self.head_joint = h["head"]
        # head-relative coordinates for field definitions
        self.hc = np.array([0.0, self.eye_y, 0.5 * (self.ear_l[2] + self.ear_r[2])])  # centre between the ears, eye height
        self.is_head = self._head_weight()

    def _head_weight(self):
        A = self.I.A
        j, w = A.joints, A.weights / 255.0
        hw = np.zeros(len(self.V))
        for k in range(4):
            hw += np.where(j[:, k] == INDEX["head"], w[:, k], 0.0)
        return hw

    def azimuth(self, P):
        """Angle around the vertical head axis from the face front (0) to the back (+-pi)."""
        d = P - self.hc
        return np.arctan2(d[:, 0], d[:, 2])

    def scalp(self, hairline_drop=0.0):
        """Hair-bearing scalp, 0..1. The hairline height runs from the forehead (eye + 7 cm) over the temples and
        above the ears down to the nape (eye - 5.5 cm), as a smooth function of the azimuth."""
        P = self.V
        az = np.abs(self.azimuth(P))
        # hairline height above the eyes (m) by azimuth: front, temple, above ear, behind ear, nape
        knots = np.array([0.0, 0.55, 1.15, 1.75, 2.35, np.pi])
        hts = np.array([0.072, 0.064, 0.040, 0.020, -0.045, -0.058]) - hairline_drop
        hl = self.eye_y + np.interp(az, knots, hts)
        # an irregular hairline: a few millimetres of smooth noise along the azimuth (fixed per identity)
        ph = (self.I.spec["seed"] * 1.7) % 6.283
        hl = hl + 0.0025 * np.sin(az * 11.0 + ph) + 0.0015 * np.sin(az * 23.0 + 2 * ph)
        # a transition wider than the mesh spacing (about 1 cm), so the hairline is a smooth gradient of thinning
        # hair, not a staircase along the quads
        f = smoothstep(hl - 0.012, hl + 0.022, P[:, 1])
        # not on the face, ears or neck front
        f *= smoothstep(0.30, 0.55, self.is_head + 0.3 * (P[:, 1] > self.eye_y))
        f *= 1 - np.clip(self.vmask["ears"] * 1.5, 0, 1)
        return np.clip(f, 0, 1)

    def beard(self, coverage=1.0):
        """Beard region: upper lip, chin, jaw, cheeks below the cheekbones, down onto the throat."""
        P = self.V
        ey = self.eye_y
        az = np.abs(self.azimuth(P))
        top = ey - 0.030 - 0.010 * smoothstep(0.0, 0.6, az)          # cheek line
        f = 1 - smoothstep(top - 0.012, top, P[:, 1])
        # under the chin onto the throat, thinning out over 4 cm (no hard lower edge)
        f *= smoothstep(self.chin_low[1] - 0.060, self.chin_low[1] - 0.012, P[:, 1]) ** 1.5
        f *= 1 - smoothstep(1.55, 1.80, az)                          # sideburns end behind the jaw
        # keep the nose, nostrils and lips clear
        dn = np.linalg.norm((P - self.nose) * np.array([1.0, 1.4, 1.0]), axis=1)
        f *= smoothstep(0.016, 0.024, dn)
        f *= 1 - np.clip(self.vmask["lips"] * 1.6, 0, 1)
        # a little thinner on the upper cheeks
        f *= 0.55 + 0.45 * (1 - smoothstep(top - 0.03, top, P[:, 1]))
        f *= self.is_head > 0.2
        return np.clip(f * coverage, 0, 1)

    def brows(self):
        P = self.V
        out = np.zeros(len(P))
        for c, s in ((self.eye_l, 1.0), (self.eye_r, -1.0)):
            # an arched band 1.2-2.0 cm above the eye centre, from the inner corner to past the outer corner
            u = (P[:, 0] - c[0]) * s  # + outward
            arch = c[1] + 0.0165 + 0.004 * np.cos(np.clip(u / 0.025, -1.6, 1.6)) - 0.0025 * smoothstep(0.0, 0.03, u)
            band = 1 - smoothstep(0.0035, 0.0075, np.abs(P[:, 1] - arch))
            span = smoothstep(-0.022, -0.016, u) * (1 - smoothstep(0.022, 0.030, u))
            front = smoothstep(c[2] - 0.005, c[2] + 0.004, P[:, 2])
            out = np.maximum(out, band * span * front)
        return out * (self.is_head > 0.3)

    def redness(self):
        P = self.V
        r = np.zeros(len(P))
        for c in (self.eye_l, self.eye_r):
            cheek = c + np.array([0.004 * np.sign(c[0]), -0.032, 0.0])
            r = np.maximum(r, 1 - smoothstep(0.012, 0.032, np.linalg.norm(P - cheek, axis=1)))
        r = np.maximum(r, 1 - smoothstep(0.006, 0.022, np.linalg.norm(P - self.nose, axis=1)))
        r = np.maximum(r, self.vmask["ears"] * 0.8)
        r = np.maximum(r, self.vmask["lips"] * 0.3)
        return r * (self.is_head > 0.2)

    def tzone(self):
        P = self.V
        fore = (1 - smoothstep(0.02, 0.045, np.abs(P[:, 0]))) * smoothstep(self.eye_y + 0.01, self.eye_y + 0.025, P[:, 1]) * (1 - smoothstep(self.eye_y + 0.06, self.eye_y + 0.08, P[:, 1]))
        nose = 1 - smoothstep(0.008, 0.03, np.linalg.norm(P - self.nose, axis=1))
        return np.clip(np.maximum(fore, nose) * (self.is_head > 0.3), 0, 1)

    def thin(self):
        P = self.V
        nostr = 1 - smoothstep(0.004, 0.012, np.linalg.norm(P - (self.nose - np.array([0, 0.01, 0.006])), axis=1))
        return np.clip(np.maximum(self.vmask["ears"], nostr), 0, 1)

    def cavity(self, F):
        """Concavity from the umbrella operator (vertex minus neighbour mean along the normal), 0 = deep crease."""
        V = self.V
        E = meshutil.adjacency(F, len(V))
        S = np.zeros_like(V)
        cnt = np.zeros(len(V))
        np.add.at(S, E[:, 0], V[E[:, 1]])
        np.add.at(S, E[:, 1], V[E[:, 0]])
        np.add.at(cnt, E[:, 0], 1)
        np.add.at(cnt, E[:, 1], 1)
        cnt[cnt == 0] = 1
        lap = S / cnt[:, None] - V
        h = (lap * self.N).sum(1)  # > 0 when the neighbours bulge out around a dip
        # smooth a little so the result reads as occlusion, not as triangle noise
        val = np.clip(1 - h / 0.0018, 0, 1)
        for _ in range(3):
            acc = np.zeros(len(V))
            np.add.at(acc, E[:, 0], val[E[:, 1]])
            np.add.at(acc, E[:, 1], val[E[:, 0]])
            val = 0.5 * val + 0.5 * acc / cnt
        return np.clip(val, 0.25, 1.0)
