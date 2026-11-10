"""Animation clips for one identity: CMU retargets cut into cycles, with foot contacts and natural speed.

CLIP chunk payload (little endian): string name | u32 flags (1 = loop) | f32 fps | u32 frames | u32 bones |
f32 natural speed (m/s, 0 for non-locomotion) | f32 cycle phase offset | frames x bones x f32x4 local rotation
(w, x, y, z) | frames x f32x3 pelvis position (metres, clip space: +Z = travel) | frames x u8 contact bits
(1 = left foot planted, 2 = right foot planted) | string source note.
"""
import struct

import numpy as np

from g2ch import s16
from retarget import local_rotations
from rig import BONES, INDEX
from xform import mat_to_quat, quat_to_mat

# name, CMU file, source frame range (120 fps) or None, kind
CLIPS = [
    ("idle", "077/77_02.bvh", None, "idle"),
    ("idle_proud", "137/137_41.bvh", (2160, 3000), "idle"),
    ("walk", "016/16_15.bvh", None, "cycle"),
    ("walk_b", "035/35_01.bvh", None, "cycle"),
    ("march", "020/20_07.bvh", None, "cycle"),
    ("jog", "035/35_22.bvh", None, "cycle"),
    ("run", "143/143_01.bvh", None, "cycle"),
    ("thrust", "144/144_17.bvh", (708, 948), "once"),
    ("swing_over", "002/02_07.bvh", (72, 312), "once"),
    ("swing_side", "002/02_07.bvh", (1236, 1440), "once"),
    ("block", "144/144_26.bvh", (1848, 2016), "once"),
    ("cower", "076/76_04.bvh", (120, 420), "once"),
    ("kneel", "023/23_03.bvh", (72, 456), "once"),
    ("death_fwd", "090/90_16.bvh", (1, 560), "once"),
    ("death_back", "090/90_18.bvh", (1, 395), "once"),
    ("stumble", "104/104_13.bvh", (1, 300), "once"),
    ("wounded_walk", "139/139_19.bvh", None, "cycle"),
    ("bow", "079/79_86.bvh", (1, 682), "once"),
]


def fk_positions(qlocal, pelvis, heads, parents):
    """World positions of every bone head per frame (rest frames world-aligned)."""
    n = len(qlocal)
    P = np.zeros((n, len(BONES), 3))
    for f in range(n):
        G = {}
        for b in BONES:
            i = INDEX[b]
            p = parents[b]
            L = quat_to_mat(qlocal[f, i])
            if p is None:
                G[b] = L
                P[f, i] = heads[b]
            else:
                G[b] = G[p] @ L
                if b == "pelvis":
                    P[f, i] = heads[b] + pelvis[f]
                else:
                    P[f, i] = P[f, INDEX[p]] + G[p] @ (heads[b] - heads[p])
    return P


def contacts(P, fps, heads):
    """Planted feet: ball or ankle low and slow. Thresholds relative to the identity's rest heights."""
    out = np.zeros(len(P), np.uint8)
    for bit, side in ((1, "l"), (2, "r")):
        ball = P[:, INDEX[f"ball_{side}"]]
        ankle = P[:, INDEX[f"foot_{side}"]]
        v_ball = np.zeros(len(P))
        v_ankle = np.zeros(len(P))
        v_ball[1:] = np.linalg.norm(np.diff(ball[:, [0, 2]], axis=0), axis=1) * fps
        v_ankle[1:] = np.linalg.norm(np.diff(ankle[:, [0, 2]], axis=0), axis=1) * fps
        v_ball[0], v_ankle[0] = v_ball[1], v_ankle[1]
        low_ball = ball[:, 1] < heads[f"ball_{side}"][1] + 0.03
        low_ankle = ankle[:, 1] < heads[f"foot_{side}"][1] + 0.03
        planted = (low_ball & (v_ball < 0.35)) | (low_ankle & (v_ankle < 0.35))
        out |= np.where(planted, bit, 0).astype(np.uint8)
    return out


def strikes(c, bit):
    """Frames where the foot becomes planted."""
    on = (c & bit) != 0
    return [i for i in range(1, len(on)) if on[i] and not on[i - 1]]


def make_cycle(q, pelvis, P, c, fps):
    """Cut one stride cycle (left strike to the next left strike) nearest the middle; loop-correct; in-place."""
    ls = strikes(c, 1)
    if len(ls) >= 2:
        mid = len(q) / 2
        best = min(range(len(ls) - 1), key=lambda k: (abs((ls[k] + ls[k + 1]) / 2 - mid), k))
        a, b = ls[best], ls[best + 1]
    else:
        # a short capture holding about one stride (the CMU run 143_01): use all of it
        a, b = 0, len(q) - 1
    q = q[a:b + 1].copy()
    pelvis = pelvis[a:b + 1].copy()
    c = c[a:b + 1].copy()
    n = b - a
    dur = n / fps
    travel = pelvis[-1] - pelvis[0]
    speed = float(np.hypot(travel[0], travel[2]) / dur)
    t = np.arange(n + 1) / n
    # remove the average horizontal velocity (in place); keep the vertical bob and lateral sway
    pelvis[:, 0] -= travel[0] * t
    pelvis[:, 2] -= travel[2] * t
    pelvis[:, 0] -= pelvis[:, 0].mean()
    pelvis[:, 2] -= pelvis[:, 2].mean()
    # loop correction: distribute the end-start rotation mismatch linearly over the cycle
    for j in range(q.shape[1]):
        q0, q1 = q[0, j], q[-1, j]
        if np.dot(q0, q1) < 0:
            q1 = -q1
        for f in range(n + 1):
            qf = q[f, j]
            corr = (q0 - q1) * t[f]
            qn = qf + (corr if np.dot(qf, q1) >= 0 else -corr)
            q[f, j] = qn / np.linalg.norm(qn)
    dy = pelvis[-1, 1] - pelvis[0, 1]
    pelvis[:, 1] -= dy * t
    # drop the duplicated last frame (the loop wraps to frame 0)
    return q[:-1], pelvis[:-1], c[:-1], speed


def bake_clips(retargeter, parents, heads, cmu_root, fps=30.0):
    out = []
    for name, rel, rng, kind in CLIPS:
        start, stop = (rng if rng else (None, None))
        clip = retargeter.run(f"{cmu_root}/{rel}", start, stop, locomotion=(kind == "cycle"), fps=fps)
        q = local_rotations(clip, parents)
        pelvis = clip["hips"].copy()
        P = fk_positions(q, pelvis, heads, parents)
        # ground the clip: the lowest foot point (ball or ankle, relative to its rest height) sits at rest height
        # (5th percentile over the clip for cycles and idles; over the first 0.5 s for one-shots, which start standing)
        low = np.min(np.stack([P[:, INDEX[f"{b}_{s}"], 1] - heads[f"{b}_{s}"][1] for b in ("ball", "foot") for s in "lr"]), 0)
        off = float(np.percentile(low, 5) if kind in ("cycle", "idle") else np.percentile(low[: int(0.5 * fps)], 5))
        pelvis[:, 1] -= off
        P[:, :, 1] -= off
        c = contacts(P, fps, heads)
        speed = 0.0
        flags = 0
        if kind == "cycle":
            q, pelvis, c, speed = make_cycle(q, pelvis, P, c, fps)
            flags = 1
        elif kind == "idle":
            flags = 1
            # cross-fade the last 0.5 s into the first frames so the idle loops
            m = int(0.5 * fps)
            n = len(q)
            for k in range(m):
                w = (k + 1) / (m + 1)
                f = n - m + k
                for j in range(q.shape[1]):
                    a, b = q[f, j], q[k, j]
                    if np.dot(a, b) < 0:
                        b = -b
                    v = a * (1 - w) + b * w
                    q[f, j] = v / np.linalg.norm(v)
                pelvis[f] = pelvis[f] * (1 - w) + pelvis[k] * w
            pelvis[:, 0] -= pelvis[:, 0].mean()
            pelvis[:, 2] -= pelvis[:, 2].mean()
        else:
            pelvis[:, 0] -= pelvis[0, 0]
            pelvis[:, 2] -= pelvis[0, 2]
        out.append({"name": name, "flags": flags, "ground_offset": off, "fps": fps, "q": q.astype(np.float32), "pelvis": pelvis.astype(np.float32),
                    "contacts": c, "speed": speed, "source": f"CMU {rel} (Hahne BVH conversion), frames {rng or 'all'}",
                    "scale": clip["scale"]})
    return out


def clip_chunk(c):
    q = np.asarray(c["q"], "<f4")
    n, nb, _ = q.shape
    return (s16(c["name"]) + struct.pack("<IfIIff", c["flags"], c["fps"], n, nb, c["speed"], 0.0) + q.tobytes()
            + np.asarray(c["pelvis"], "<f4").tobytes() + np.asarray(c["contacts"], np.uint8).tobytes() + s16(c["source"]))
