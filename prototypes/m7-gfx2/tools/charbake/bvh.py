"""BVH reader (ROOT/JOINT hierarchy, any channel order) and forward kinematics in the file's own frame."""
import numpy as np

from xform import rot_axis


class Bvh:
    def __init__(self, path):
        self.names, self.parent, self.offset, self.channels, self.end = [], [], [], [], {}
        stack, name, pending_end = [], None, False
        with open(path, encoding="utf-8") as f:
            lines = f.read().split("\n")
        i = 0
        while i < len(lines):
            t = lines[i].split()
            i += 1
            if not t:
                continue
            k = t[0]
            if k in ("ROOT", "JOINT"):
                name = t[1]
                self.names.append(name)
                self.parent.append(stack[-1] if stack else -1)
                self.offset.append(None)
                self.channels.append([])
                pending_end = False
            elif k == "End":
                pending_end = True
            elif k == "{":
                stack.append(-2 if pending_end else len(self.names) - 1)
            elif k == "}":
                stack.pop()
                pending_end = False
            elif k == "OFFSET":
                o = np.array([float(x) for x in t[1:4]])
                if pending_end:
                    self.end[stack[-2] if stack[-1] == -2 else stack[-1]] = o
                else:
                    self.offset[-1] = o
            elif k == "CHANNELS":
                self.channels[-1] = t[2:]
            elif k == "MOTION":
                break
        frames = int(lines[i].split()[1])
        self.frame_time = float(lines[i + 1].split()[2])
        data = []
        for ln in lines[i + 2:]:
            if ln.strip():
                data.append([float(x) for x in ln.split()])
        self.data = np.array(data[:frames])
        self.offset = np.array(self.offset)
        self.index = {n: j for j, n in enumerate(self.names)}
        col = 0
        self.chan_col = []
        for ch in self.channels:
            self.chan_col.append(col)
            col += len(ch)

    def local(self, frame):
        """Per joint: local rotation (3x3) and translation channels (or None)."""
        row = self.data[frame]
        rots, trans = [], []
        for j, ch in enumerate(self.channels):
            c0 = self.chan_col[j]
            R = np.eye(3)
            tr = np.zeros(3)
            has_t = False
            for k, c in enumerate(ch):
                v = row[c0 + k]
                if c.endswith("rotation"):
                    R = R @ rot_axis(c[0], v)
                else:
                    tr["XYZ".index(c[0])] = v
                    has_t = True
            rots.append(R)
            trans.append(tr if has_t else None)
        return rots, trans

    def fk(self, frame):
        """Global rotations and joint positions (file units and axes)."""
        rots, trans = self.local(frame)
        n = len(self.names)
        G = np.zeros((n, 3, 3))
        P = np.zeros((n, 3))
        for j in range(n):
            p = self.parent[j]
            if p < 0:
                G[j] = rots[j]
                P[j] = self.offset[j] + (trans[j] if trans[j] is not None else 0.0)
            else:
                G[j] = G[p] @ rots[j]
                P[j] = P[p] + G[p] @ self.offset[j]
        return G, P
