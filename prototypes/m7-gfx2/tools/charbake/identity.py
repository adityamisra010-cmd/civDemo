"""One identity: the morphed hm08 body, eyes, mouth parts, skeleton, expressions and clips (no outfit)."""
import numpy as np

import mhcore
import meshutil
from expressions import FaceRig, ORDER as EXPR_ORDER
from rig import BONES, INDEX, Rig


class Assets:
    def __init__(self, raw):
        self.raw = raw
        D = f"{raw}/mh-core/makehuman/data"
        self.data = D
        self.mesh = mhcore.BaseMesh(f"{D}/3dobjs/base.obj")
        self.targets = mhcore.Targets(f"{D}/targets")
        self.rig = Rig(f"{raw}/mpfb2/src/mpfb/data")
        self.face = FaceRig(D)
        self.eye_hi = mhcore.Proxy(f"{D}/eyes/high-poly/high-poly.mhclo")
        self.eye_hi_obj = mhcore.load_simple_obj(self.eye_hi.obj)
        self.eye_lo = mhcore.Proxy(f"{D}/eyes/low-poly/low-poly.mhclo")
        self.eye_lo_obj = mhcore.load_simple_obj(self.eye_lo.obj)
        self.joints, self.weights = self.rig.skin_weights(len(self.mesh.V))
        m = self.mesh
        bf = m.group_faces("body")
        self.body_fv = meshutil.quads_to_tris(m.fv[bf])
        self.body_fuv = meshutil.quads_to_tris(m.fuv[bf])
        self.cmu = f"{raw}/cmu-bvh/data"


def identity_targets(spec, A):
    items = list(mhcore.macro_targets(spec["macro"], A.targets))
    for mod, val in sorted(spec.get("modifiers", {}).items()):
        items += mhcore.modifier_targets(mod, val)
    return items


class Identity:
    """Geometry of one person in the runtime frame (metres, ground at y = 0, +Z forward, +X = left)."""

    def __init__(self, spec, A):
        self.spec = spec
        self.A = A
        m = A.mesh
        self.items = identity_targets(spec, A)
        V = mhcore.apply_targets(m.V, A.targets, self.items)
        self.V_dm = V
        g = V[m.group_verts("joint-ground")].mean(0)
        self.ground = g[1]
        self.Vm = self.to_m(V)
        # eyes (CC0 high-poly proxy), fitted to this face
        ev, euv, efv, efuv = A.eye_hi_obj
        self.eye_V = self.to_m(A.eye_hi.fit(V))
        left = self.eye_V[:, 0] > 0
        self.eye_centres = [self.eye_V[left].mean(0), self.eye_V[~left].mean(0)]
        self.heads, self.tails = A.rig.fit(m, self.Vm, self.eye_centres)
        self.parents = A.rig.parent
        self.N = meshutil.vertex_normals(self.Vm, A.body_fv)
        # expressions: deltas over every base vertex (body, helpers), metres
        self.expr = {name: A.face.expression(V, name) * 0.1 for name in EXPR_ORDER}
        self.height = float(self.Vm[m.group_verts("body"), 1].max())

    def to_m(self, X):
        return (X - np.array([0.0, self.ground, 0.0])) * 0.1

    # ------------------------------------------------------------------ meshes
    def body_mesh(self):
        A = self.A
        src_v, src_uv, F = meshutil.split_by_uv(A.body_fv, A.body_fuv)
        P = self.Vm[src_v]
        N = self.N[src_v]
        UV = A.mesh.UV[src_uv].copy()
        UV[:, 1] = 1.0 - UV[:, 1]
        T = meshutil.tangents(P, N, UV, F)
        return {"name": "body", "material": "skin", "pos": P, "nrm": N, "tan": T, "uv": UV,
                "joints": A.joints[src_v], "weights": A.weights[src_v], "idx": F.ravel(), "src": src_v, "F": F}

    def body_morphs(self, body):
        A = self.A
        out = []
        for name in EXPR_ORDER:
            d = self.expr[name]
            Np = meshutil.vertex_normals(self.Vm + d, A.body_fv)
            dpos = d[body["src"]]
            dn = (Np - self.N)[body["src"]]
            keep = np.nonzero((np.abs(dpos).max(1) > 2e-6) | (np.abs(dn).max(1) > 2e-4))[0]
            out.append((name, keep, dpos[keep], dn[keep]))
        return out

    def helper_mesh(self, group, name, material, color):
        """A base-mesh helper (teeth, tongue) as its own skinned part, UVs from the base mesh."""
        A = self.A
        fq = A.mesh.group_faces(group)
        fv = meshutil.quads_to_tris(A.mesh.fv[fq])
        fuv = meshutil.quads_to_tris(A.mesh.fuv[fq])
        src_v, src_uv, F = meshutil.split_by_uv(fv, fuv)
        Nfull = meshutil.vertex_normals(self.Vm, fv)
        P = self.Vm[src_v]
        N = Nfull[src_v]
        UV = A.mesh.UV[src_uv].copy()
        UV[:, 1] = 1 - UV[:, 1]
        T = meshutil.tangents(P, N, UV, F)
        col = np.tile(np.array(color, np.uint8), (len(P), 1))
        morphs = []
        for ename in EXPR_ORDER:
            d = self.expr[ename]
            Np = meshutil.vertex_normals(self.Vm + d, fv)
            dpos, dn = d[src_v], (Np - Nfull)[src_v]
            keep = np.nonzero((np.abs(dpos).max(1) > 2e-6) | (np.abs(dn).max(1) > 2e-4))[0]
            morphs.append((ename, keep, dpos[keep], dn[keep]))
        mesh = {"name": name, "material": material, "pos": P, "nrm": N, "tan": T, "uv": UV, "color": col,
                "joints": A.joints[src_v], "weights": A.weights[src_v], "idx": F.ravel()}
        return mesh, morphs

    def eye_meshes(self, eye_alpha):
        """The CC0 high-poly eye proxy split into the eyeball (opaque: sclera, iris, pupil) and the cornea shell (the
        faces textured with the image's transparent region), each skinned rigidly to its eye bone."""
        import uvraster
        A = self.A
        ev, euv, efv, efuv = A.eye_hi_obj
        efv, efuv = np.array(efv), np.array(efuv)
        uvf = euv.copy()
        uvf[:, 1] = 1 - uvf[:, 1]
        cen = uvf[efuv].mean(1)
        clear = uvraster.sample(eye_alpha, cen) < 0.5
        out = []
        for name, mat, sel in (("eyes", "eye", ~clear), ("cornea", "cornea", clear)):
            fv = meshutil.quads_to_tris(efv[sel])
            fuv = meshutil.quads_to_tris(efuv[sel])
            Nfull = meshutil.vertex_normals(self.eye_V, fv)
            src_v, src_uv, F = meshutil.split_by_uv(fv, fuv)
            P = self.eye_V[src_v]
            N = Nfull[src_v]
            UV = euv[src_uv].copy()
            UV[:, 1] = 1 - UV[:, 1]
            T = meshutil.tangents(P, N, UV, F)
            left = P[:, 0] > 0
            joints = np.zeros((len(P), 4), np.uint8)
            joints[:, 0] = np.where(left, INDEX["eye_l"], INDEX["eye_r"])
            weights = np.zeros((len(P), 4), np.uint8)
            weights[:, 0] = 255
            out.append({"name": name, "material": mat, "pos": P, "nrm": N, "tan": T, "uv": UV, "joints": joints,
                        "weights": weights, "idx": F.ravel()})
        return out

    def skeleton(self):
        names = BONES
        parents = [INDEX[self.parents[b]] if self.parents[b] else -1 for b in BONES]
        return names, parents, [self.heads[b] for b in BONES], [self.tails[b] for b in BONES]
