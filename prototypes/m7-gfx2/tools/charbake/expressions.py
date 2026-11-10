"""Facial expression blend shapes from MakeHuman's CC0 face pose units.

The pose units (`poseunits/face-poseunits.bvh`, 60 frames: Rest + 59 units, names in `face-poseunits.json`) drive the
default 163-bone skeleton (`rigs/default.mhskel`) whose CC0 weights (`default_weights.mhw`) cover the face, teeth,
tongue and eyelid helpers. BVH joints have world-aligned rest frames, so a unit's global joint rotation is a
world-space rotation about the joint. The BVH file is Z-up (x, -z, y) relative to MakeHuman's Y-up mesh; rotations
are conjugated into the mesh frame. An expression is a weighted set of units: per joint, the product (in unit order)
of each unit's local rotation raised to its weight. The posed mesh minus the rest mesh is the blend shape.
"""
import json

import numpy as np

from bvh import Bvh
from xform import mat_to_quat, quat_mul, quat_pow, quat_to_mat

C = np.array([[1.0, 0, 0], [0, 0, -1.0], [0, 1.0, 0]])  # MakeHuman vector -> BVH vector

# The five expressions the brief asks for (and two used by the demonstrator), as pose-unit weights.
EXPRESSIONS = {
    "blink": {"LeftUpperLidClosed": 1.0, "RightUpperLidClosed": 1.0, "LeftLowerLidUp": 0.25, "RightLowerLidUp": 0.25},
    "battlecry": {"JawDrop": 0.8, "UpperLipUp": 0.55, "lowerLipDown": 0.55, "LeftBrowDown": 0.85, "RightBrowDown": 0.85,
                  "NoseWrinkler": 0.55, "LeftCheekUp": 0.5, "RightCheekUp": 0.5, "MouthLeftPullSide": 0.3,
                  "MouthRightPullSide": 0.3, "MouthLeftPlatysma": 0.55, "MouthRightPlatysma": 0.55,
                  "LeftLowerLidUp": 0.35, "RightLowerLidUp": 0.35},
    "pain": {"LeftInnerBrowUp": 0.9, "RightInnerBrowUp": 0.9, "LeftBrowDown": 0.35, "RightBrowDown": 0.35,
             "LeftUpperLidClosed": 0.55, "RightUpperLidClosed": 0.55, "LeftCheekUp": 0.6, "RightCheekUp": 0.6,
             "NoseWrinkler": 0.45, "MouthLeftPullDown": 0.4, "MouthRightPullDown": 0.4, "MouthLeftPlatysma": 0.6,
             "MouthRightPlatysma": 0.6, "JawDrop": 0.2, "UpperLipUp": 0.35},
    "grim": {"LeftBrowDown": 0.6, "RightBrowDown": 0.6, "LeftUpperLidClosed": 0.2, "RightUpperLidClosed": 0.2,
             "MouthLeftPullDown": 0.35, "MouthRightPullDown": 0.35, "lowerLipUp": 0.3, "NasolabialDeepener": 0.4,
             "ChinForward": 0.15},
    "calm": {"LeftUpperLidClosed": 0.15, "RightUpperLidClosed": 0.15, "MouthLeftPullUp": 0.18, "MouthRightPullUp": 0.18,
             "LeftOuterBrowUp": 0.1, "RightOuterBrowUp": 0.1},
}
ORDER = ["blink", "battlecry", "pain", "grim", "calm"]


class FaceRig:
    def __init__(self, data_root):
        meta = json.load(open(f"{data_root}/poseunits/face-poseunits.json", encoding="utf-8"))
        self.units = meta["framemapping"]
        self.bvh = Bvh(f"{data_root}/poseunits/face-poseunits.bvh")
        sk = json.load(open(f"{data_root}/rigs/default.mhskel", encoding="utf-8"))
        self.joints = sk["joints"]
        self.bones = sk["bones"]
        wts = json.load(open(f"{data_root}/rigs/default_weights.mhw", encoding="utf-8"))["weights"]
        self.names = list(self.bvh.names)
        missing = [n for n in self.names if n not in self.bones]
        if missing:
            raise ValueError(f"pose-unit joints missing from default.mhskel: {missing[:5]}")
        self.weights = wts
        # per unit, per joint local rotation as a quaternion (BVH frame)
        self.unit_q = {}
        for k, u in enumerate(self.units):
            rots, _ = self.bvh.local(k)
            self.unit_q[u] = [mat_to_quat(R) for R in rots]

    def heads(self, V):
        return np.array([V[self.joints[self.bones[n]["head"]]].mean(0) for n in self.names])

    def pose(self, V, unit_weights):
        """Posed copy of V (all base-mesh vertices) for a dict of unit weights."""
        n = len(self.names)
        q = [np.array([1.0, 0, 0, 0]) for _ in range(n)]
        for u in self.units:
            w = unit_weights.get(u, 0.0)
            if w == 0:
                continue
            for j in range(n):
                q[j] = quat_mul(q[j], quat_pow(self.unit_q[u][j], w))
        H = self.heads(V)
        G = np.zeros((n, 3, 3))
        P = np.zeros((n, 3))
        for j in range(n):
            Rl = C.T @ quat_to_mat(q[j]) @ C
            p = self.bvh.parent[j]
            if p < 0:
                G[j] = Rl
                P[j] = H[j]
            else:
                G[j] = G[p] @ Rl
                P[j] = P[p] + G[p] @ (H[j] - H[p])
        out = np.zeros_like(V)
        acc = np.zeros(len(V))
        for j, name in enumerate(self.names):
            lst = self.weights.get(name)
            if not lst:
                continue
            idx = np.array([a for a, _ in lst], dtype=np.int64)
            w = np.array([b for _, b in lst])
            out[idx] += w[:, None] * ((V[idx] - H[j]) @ G[j].T + P[j])
            acc[idx] += w
        miss = acc < 1e-9
        out[~miss] /= acc[~miss, None]
        out[miss] = V[miss]
        return out

    def expression(self, V, name):
        return self.pose(V, EXPRESSIONS[name]) - V
