"""The runtime skeleton: MPFB2's CC0 game_engine rig (53 bones) plus two eye bones, fitted to an identity mesh.

Bone frames are world-aligned at rest (like BVH): a bone is a head position plus a parent; animation stores local
rotations relative to that rest frame. Heads and tails come from MPFB2's joint-cube strategy evaluated on the
identity's morphed base mesh, so the skeleton follows each body's proportions.
"""
import json

import numpy as np

FINGERS = ["thumb", "index", "middle", "ring", "pinky"]


def bone_order():
    order = ["Root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head"]
    for s in ("l", "r"):
        order += [f"clavicle_{s}", f"upperarm_{s}", f"lowerarm_{s}", f"hand_{s}"]
        for f in FINGERS:
            order += [f"{f}_0{k}_{s}" for k in (1, 2, 3)]
    for s in ("l", "r"):
        order += [f"thigh_{s}", f"calf_{s}", f"foot_{s}", f"ball_{s}"]
    order += ["eye_l", "eye_r"]
    return order


BONES = bone_order()
INDEX = {n: i for i, n in enumerate(BONES)}


class Rig:
    def __init__(self, mpfb_root):
        self.spec = json.load(open(f"{mpfb_root}/rigs/standard/rig.game_engine.json", encoding="utf-8"))
        w = json.load(open(f"{mpfb_root}/rigs/standard/weights.game_engine.json", encoding="utf-8"))["weights"]
        self.weights = w
        self.parent = {}
        for n in BONES:
            if n in ("eye_l", "eye_r"):
                self.parent[n] = "head"
            else:
                p = self.spec[n]["parent"]
                self.parent[n] = p if p else None
        for i, n in enumerate(BONES):
            p = self.parent[n]
            if p is not None and INDEX[p] >= i:
                raise ValueError("bone order must list parents first")

    def _pos(self, s, mesh, V):
        if s["strategy"] == "CUBE":
            return V[mesh.group_verts(s["cube_name"])].mean(0)
        if s["strategy"] == "MEAN":
            return V[s["vertex_indices"]].mean(0)
        return V[s["vertex_index"]]

    def fit(self, mesh, V, eye_centres):
        """Heads and tails (MakeHuman units) for every bone; eye bones from the fitted eye proxies."""
        heads, tails = {}, {}
        for n in BONES:
            if n in ("eye_l", "eye_r"):
                c = eye_centres[0 if n == "eye_l" else 1]
                heads[n] = c
                tails[n] = c + np.array([0, 0, 0.25])
                continue
            heads[n] = self._pos(self.spec[n]["head"], mesh, V)
            tails[n] = self._pos(self.spec[n]["tail"], mesh, V)
        return heads, tails

    def skin_weights(self, nverts, max_influences=4):
        """Top-4 influences per base-mesh vertex: (joints u8 n x 4, weights u8 n x 4 summing to 255)."""
        W = np.zeros((nverts, len(BONES)))
        for b, lst in self.weights.items():
            j = INDEX[b]
            for v, x in lst:
                W[v, j] += x
        return quantize_weights(W, max_influences)


def quantize_weights(W, max_influences=4):
    n = W.shape[0]
    # stable selection: sort by (-weight, joint index)
    order = np.lexsort((np.broadcast_to(np.arange(W.shape[1]), W.shape), -W), axis=1)[:, :max_influences]
    top = np.take_along_axis(W, order, 1)
    s = top.sum(1, keepdims=True)
    s[s <= 0] = 1
    top = top / s
    q = np.floor(top * 255 + 0.5).astype(np.int64)
    # make each row sum to exactly 255: put the rounding residue on the largest influence
    q[:, 0] += 255 - q.sum(1)
    joints = np.where(q > 0, order, 0).astype(np.uint8)
    return joints, q.astype(np.uint8)
