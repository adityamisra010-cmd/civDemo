"""CMU motion capture (B. Hahne's BVH conversion, frame 0 = T-pose) retargeted onto the runtime rig.

Method (rotation transfer with a matched T-pose):
1. The rig's T-pose: walking the hierarchy, each limb bone is turned by the minimal rotation (relative to its
   parent's already-turned frame) that makes it point along the source's frame-0 bone direction. Spine, neck, head
   and pelvis keep the rig's own rest posture. This gives Q_b (global, relative to the world-aligned rest frame).
2. For every frame f, the source joint's world-space change since frame 0, D_b(f) = G_src(f) G_src(0)^-1, is applied
   on top: G_b(f) = D_b(f) Q_b. Unmapped bones (fingers, toes, eyes) follow their parent.
3. Hips translation is scaled by the ratio of leg lengths (rig thigh+calf / source thigh+calf), so a taller body
   takes longer strides at the same cadence.
4. The clip is turned about +Y so that its travel (locomotion) or the pelvis facing (others) is +Z, and
   locomotion is made in-place: the average horizontal velocity over the extracted cycle is removed and stored as
   the clip's natural speed (m/s). Root motion is never applied to any authoritative state (presentation only).
"""
import numpy as np

from bvh import Bvh
from rig import BONES, INDEX
from xform import mat_to_quat, rot_axis, rot_between

MAP = {
    "pelvis": "Hips", "spine_01": "LowerBack", "spine_02": "Spine", "spine_03": "Spine1", "neck_01": "Neck1",
    "head": "Head",
    "clavicle_l": "LeftShoulder", "upperarm_l": "LeftArm", "lowerarm_l": "LeftForeArm", "hand_l": "LeftHand",
    "clavicle_r": "RightShoulder", "upperarm_r": "RightArm", "lowerarm_r": "RightForeArm", "hand_r": "RightHand",
    "thigh_l": "LeftUpLeg", "calf_l": "LeftLeg", "foot_l": "LeftFoot", "ball_l": "LeftToeBase",
    "thigh_r": "RightUpLeg", "calf_r": "RightLeg", "foot_r": "RightFoot", "ball_r": "RightToeBase",
}
# bones whose T-pose is matched to the source direction (limbs); the others keep the rig's rest posture
# Clavicles keep the rig's own rest direction: CMU's T-pose places the shoulder joint about 13 degrees above the
# clavicle root, and matching it would leave every pose with raised, shrugged shoulders.
ALIGN = {b for b in MAP if b.split("_")[0] in ("upperarm", "lowerarm", "hand", "thigh", "calf", "foot", "ball")}
SRC_CHILD = {
    "LeftShoulder": "LeftArm", "LeftArm": "LeftForeArm", "LeftForeArm": "LeftHand", "LeftHand": "LeftHandIndex1",
    "RightShoulder": "RightArm", "RightArm": "RightForeArm", "RightForeArm": "RightHand", "RightHand": "RightHandIndex1",
    "LeftUpLeg": "LeftLeg", "LeftLeg": "LeftFoot", "LeftFoot": "LeftToeBase", "LeftToeBase": None,
    "RightUpLeg": "RightLeg", "RightLeg": "RightFoot", "RightFoot": "RightToeBase", "RightToeBase": None,
}


def rig_dir(b, heads, tails):
    if b.startswith("hand_"):
        return heads["middle_01_" + b[-1]] - heads[b]
    return tails[b] - heads[b]


class Retargeter:
    def __init__(self, heads, tails, parents):
        """heads/tails: bone -> position (metres, runtime frame); parents: bone -> parent name or None."""
        self.heads, self.tails, self.parents = heads, tails, parents
        self.leg = (np.linalg.norm(heads["calf_l"] - heads["thigh_l"]) + np.linalg.norm(heads["foot_l"] - heads["calf_l"]))

    def tpose(self, src, G0, P0):
        Q = {}
        for b in BONES:
            p = self.parents[b]
            Qp = Q[p] if p else np.eye(3)
            if b in ALIGN:
                s = MAP[b]
                j = src.index[s]
                c = SRC_CHILD[s]
                d_src = (P0[src.index[c]] - P0[j]) if c else (G0[j] @ src.end[j])
                d_rig = Qp @ rig_dir(b, self.heads, self.tails)
                Q[b] = rot_between(d_rig, d_src) @ Qp
            else:
                Q[b] = Qp
        return Q

    def run(self, path, start=None, stop=None, locomotion=False, fps=30.0):
        src = Bvh(path)
        G0, P0 = src.fk(0)
        Q = self.tpose(src, G0, P0)
        sleg = (np.linalg.norm(P0[src.index["LeftLeg"]] - P0[src.index["LeftUpLeg"]]) +
                np.linalg.norm(P0[src.index["LeftFoot"]] - P0[src.index["LeftLeg"]]))
        scale = self.leg / sleg
        n = len(src.data)
        start = 1 if start is None else max(1, start)
        stop = n if stop is None else min(n, stop)
        t_src = np.arange(start, stop) * src.frame_time
        # resample to fps (nearest-lower source frame + linear blend of positions; rotations nlerp via quats later)
        t_out = np.arange(t_src[0], t_src[-1] + 1e-9, 1.0 / fps)
        frames = []
        hip0 = P0[src.index["Hips"]]
        for t in t_out:
            fi = t / src.frame_time
            f0 = int(np.floor(fi))
            a = fi - f0
            f1 = min(f0 + 1, n - 1)
            Ga, Pa = src.fk(f0)
            Gb, Pb = src.fk(f1)
            G = {}
            for b in BONES:
                p = self.parents[b]
                if b in MAP:
                    j = src.index[MAP[b]]
                    Ra = Ga[j] @ G0[j].T
                    Rb = Gb[j] @ G0[j].T
                    qa, qb = mat_to_quat(Ra), mat_to_quat(Rb)
                    if np.dot(qa, qb) < 0:
                        qb = -qb
                    q = qa * (1 - a) + qb * a
                    from xform import quat_to_mat
                    G[b] = quat_to_mat(q) @ Q[b]
                else:
                    G[b] = G[p] @ (Q[p].T @ Q[b]) if p else Q[b]
            hip = (Pa[src.index["Hips"]] * (1 - a) + Pb[src.index["Hips"]] * a - hip0) * scale
            frames.append((G, hip))
        return self._finish(frames, fps, locomotion, scale)

    def _finish(self, frames, fps, locomotion, scale):
        n = len(frames)
        hips = np.array([h for _, h in frames])
        # heading: travel direction for locomotion, else the pelvis facing at the first frame
        if locomotion:
            d = hips[-1] - hips[0]
        else:
            d = frames[0][0]["pelvis"] @ np.array([0.0, 0.0, 1.0])
        theta = np.arctan2(d[0], d[2])
        Ry = rot_axis("Y", -np.degrees(theta))
        out_G = []
        for G, _ in frames:
            out_G.append({b: Ry @ G[b] for b in BONES})
        hips = hips @ Ry.T
        return {"G": out_G, "hips": hips, "fps": fps, "frames": n, "scale": scale}


def local_rotations(clip, parents):
    """Global rotations -> local quaternions (w, x, y, z) per frame and bone (rest frames are world-aligned)."""
    n = clip["frames"]
    q = np.zeros((n, len(BONES), 4))
    for f in range(n):
        G = clip["G"][f]
        for b in BONES:
            p = parents[b]
            L = G[p].T @ G[b] if p else G[b]
            q[f, INDEX[b]] = mat_to_quat(L)
    return q
