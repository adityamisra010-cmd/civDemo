"""Hair, beard, brows and lashes as alpha cards generated in code over the identity's own skin (no PROTOTYPE-ONLY
MakeHuman proxy meshes). Strand texture atlas: procedural, shared by every identity.

A card is a ribbon along a grown strand curve. Roots are sampled on the skin where a smooth field (fields.py) says
hair grows; strands grow by steps that follow a comb direction (tied back, combed back, downward for beards, lateral
for brows), are pulled by gravity and kept off the skin by a nearest-point push-out against the head point cloud.
Cards lie flat on the surface (their normal follows the skin normal at the root). Each card vertex is skinned like
the skin under its root and inherits the root's expression deltas, so beards and brows move with the face.
"""
import numpy as np

import meshutil
import texio
from rig import INDEX

ATLAS = 1024
# atlas columns (u0, u1 in texels): 0 dense clump, 1 medium strands, 2 wispy, 3 beard (few thick curly strands),
# 4 brow (short, slightly curved strands). Thick strands stay visible at the mip levels a beard card is seen at.
COLUMNS = [(0, 256), (256, 512), (512, 768), (768, 896), (896, 1024)]
COLS = len(COLUMNS)


def strand_atlas(seed=5):
    """RGBA atlas: R per-strand random value, G position along the strand (root 0 -> tip 1), B shift, A coverage."""
    rng = np.random.default_rng(seed)
    R = np.zeros((ATLAS, ATLAS))
    B = np.full((ATLAS, ATLAS), 0.5)
    Acov = np.zeros((ATLAS, ATLAS))
    rows = np.arange(ATLAS)
    v = rows / (ATLAS - 1.0)
    G = np.tile(v[:, None], (1, ATLAS))
    # strands, width (px), coverage, waviness
    # (strands, width px, coverage, waviness) per column: many fine strands, so a card reads as a lock of hair (the
    # coverage averages towards the root and thins out at the staggered tips), never as a few dark scribbles
    specs = [(420, 0.9, 0.95, 0.4), (260, 0.9, 0.90, 0.6), (110, 0.8, 0.80, 1.0), (170, 1.1, 0.90, 2.0), (60, 1.3, 0.90, 0.8)]
    for col, (n, wpx, cover, wav) in enumerate(specs):
        x0, x1 = COLUMNS[col]
        W = x1 - x0
        for s in range(n):
            xc0 = x0 + 6 + rng.random() * (W - 12)
            amp = rng.random() * wav * (3.0 if col >= 3 else 1.5)
            freq = (1 + rng.random() * 3) * (2.0 if col == 3 else 1.0)
            ph = rng.random() * 6.283
            drift = (rng.random() - 0.5) * (14 if col < 3 else 24)
            start = 0.0 if rng.random() < 0.8 else rng.random() * 0.3
            end = 1.0 - rng.random() * (0.35 if col < 2 else 0.55)
            width = wpx * (0.7 + 0.6 * rng.random())
            val = rng.random()
            sh = rng.random()
            vv = v
            xc = xc0 + drift * vv + amp * np.sin(freq * 6.283 * vv + ph)
            taper = np.clip((end - vv) / 0.18, 0, 1) * np.clip((vv - start) / 0.03, 0, 1)
            wv = width * (0.35 + 0.65 * taper)
            lo = np.floor(xc - wv - 2).astype(int)
            for k in range(5 + int(wpx * 2.5)):
                xx = lo + k
                inside = (xx >= x0) & (xx < x0 + W)
                # lateral envelope: strands near a card's long edges are sparser, so overlapping cards blend
                # into each other instead of showing their rectangles
                edge = 1.0 - np.abs(2.0 * (xc - x0) / W - 1.0) ** 3
                cov = np.clip(wv - np.abs(xx + 0.5 - xc) + 0.5, 0, 1) * cover * (taper > 0) * np.clip(edge, 0, 1)
                sel = inside & (cov > 0)
                r, c = rows[sel], xx[sel]
                newa = Acov[r, c] + cov[sel] * (1 - Acov[r, c])
                upd = cov[sel] > 0.3
                R[r[upd], c[upd]] = val
                B[r[upd], c[upd]] = sh
                Acov[r, c] = newa
    return np.stack([R, G, B, np.clip(Acov, 0, 1)], -1)


def atlas_levels():
    a = strand_atlas()
    levels = []
    cur = a
    cov0 = float((a[..., 3] > 0.5).mean())
    while True:
        lv = cur.copy()
        if cur.shape[0] < a.shape[0]:
            lo, hi = 0.0, 4.0
            for _ in range(14):
                mid = (lo + hi) / 2
                if (np.clip(cur[..., 3] * mid, 0, 1) > 0.5).mean() < cov0:
                    lo = mid
                else:
                    hi = mid
            lv[..., 3] = np.clip(cur[..., 3] * hi, 0, 1)
        levels.append(texio.to8(lv))
        if cur.shape[0] == 1:
            break
        cur = texio.down2(cur)
    return levels


class NearGrid:
    """Nearest-point queries against a fixed point cloud through a uniform voxel grid (27-cell neighbourhood)."""

    def __init__(self, P, cell=0.012):
        self.P = P
        self.h = cell
        self.lo = P.min(0) - 2 * cell
        ijk = np.floor((P - self.lo) / cell).astype(np.int64)
        self.dims = ijk.max(0) + 3
        flat = (ijk[:, 0] * self.dims[1] + ijk[:, 1]) * self.dims[2] + ijk[:, 2]
        order = np.argsort(flat, kind="stable")
        fs = flat[order]
        ncell = int(np.prod(self.dims))
        counts = np.bincount(fs, minlength=ncell)
        self.maxper = int(counts.max())
        start = np.concatenate([[0], np.cumsum(counts)[:-1]])
        rank = np.arange(len(fs)) - start[fs]
        self.table = -np.ones((ncell, self.maxper), np.int64)
        self.table[fs, rank] = order
        off = np.array([(a, b, c) for a in (-1, 0, 1) for b in (-1, 0, 1) for c in (-1, 0, 1)])
        self.off = off

    def nearest(self, X, chunk=4096):
        out = np.zeros(len(X), np.int64)
        for i0 in range(0, len(X), chunk):
            Xc = X[i0:i0 + chunk]
            ijk = np.floor((Xc - self.lo) / self.h).astype(np.int64)
            ijk = np.clip(ijk, 1, self.dims - 2)
            nb = ijk[:, None, :] + self.off[None, :, :]
            flat = (nb[..., 0] * self.dims[1] + nb[..., 1]) * self.dims[2] + nb[..., 2]
            cand = self.table[flat].reshape(len(Xc), -1)
            valid = cand >= 0
            Q = self.P[np.where(valid, cand, 0)]
            d2 = ((Q - Xc[:, None, :]) ** 2).sum(2)
            d2 = np.where(valid, d2, np.inf)
            j = np.argmin(d2, 1)
            best = cand[np.arange(len(Xc)), j]
            # exact: the 27-cell block extends at least one cell (h) around the query in every direction, so a
            # nearest point found within h is the true nearest; farther ones fall back to brute force (rare)
            miss = ~(d2[np.arange(len(Xc)), j] <= self.h * self.h)
            if miss.any():
                Dm = ((Xc[miss][:, None, :] - self.P[None]) ** 2).sum(2)
                best[miss] = np.argmin(Dm, 1)
            out[i0:i0 + chunk] = best
        return out


class Surface:
    """The identity's skin as a sampling and collision surface (rest pose, metres)."""

    def __init__(self, ident):
        A = ident.A
        self.I = ident
        self.V = ident.Vm
        self.F = A.body_fv
        self.N = ident.N
        head = np.nonzero(ident.Vm[:, 1] > ident.heads["neck_01"][1] - 0.06)[0]
        body = set(A.mesh.group_verts("body").tolist())
        self.cloud_idx = np.array([i for i in head if i in body])
        self.cloud = self.V[self.cloud_idx]
        self.cloud_n = self.N[self.cloud_idx]
        self.grid = NearGrid(self.cloud)

    def sample(self, field, count, rng, min_field=0.35):
        """Area- and field-weighted random points on body triangles: (positions, normals, tri, barycentrics)."""
        F = self.F
        fv = field[F].mean(1)
        ok = np.nonzero(fv > min_field)[0]
        a, b, c = self.V[F[ok, 0]], self.V[F[ok, 1]], self.V[F[ok, 2]]
        area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
        w = area * fv[ok]
        if w.sum() <= 0 or count <= 0:
            return np.zeros((0, 3)), np.zeros((0, 3)), np.zeros(0, int), np.zeros((0, 3))
        pick = rng.choice(len(ok), size=count, p=w / w.sum())
        r1, r2 = rng.random(count), rng.random(count)
        s1 = np.sqrt(r1)
        bary = np.stack([1 - s1, s1 * (1 - r2), s1 * r2], 1)
        tri = ok[pick]
        P = (self.V[F[tri]] * bary[:, :, None]).sum(1)
        Nn = (self.N[F[tri]] * bary[:, :, None]).sum(1)
        Nn /= np.linalg.norm(Nn, axis=1, keepdims=True)
        return P, Nn, tri, bary

    def hug(self, P, clearance, amount):
        """Moves points towards the offset surface at `clearance` above the skin (amount 1 = exactly onto it)."""
        j = self.grid.nearest(P)
        q = self.cloud[j]
        n = self.cloud_n[j]
        clearance = np.broadcast_to(np.asarray(clearance, float), (len(P),))
        h = ((P - q) * n).sum(1)
        amount = np.broadcast_to(np.asarray(amount, float), (len(P),))
        return P + ((clearance - h) * amount)[:, None] * n

    def push_out(self, P, clearance):
        """Moves points that are closer than `clearance` to the skin (or inside it) back out along the skin normal."""
        j = self.grid.nearest(P)
        q = self.cloud[j]
        n = self.cloud_n[j]
        h = ((P - q) * n).sum(1)
        clearance = np.broadcast_to(np.asarray(clearance, float), h.shape)
        need = h < clearance
        P = P.copy()
        P[need] += (clearance[need] - h[need])[:, None] * n[need]
        return P


def grow(surface, roots, normals, lengths, steps, direction_fn, clearance, gravity, rng, stiffness=0.6, jitter=0.15, hug=None):
    """Grows strands: returns (n, steps+1, 3) curves. hug(k) -> 0..1 pulls step k onto the offset surface (combed
    hair lies on the head instead of lifting off where the skull curves away)."""
    n = len(roots)
    C = np.zeros((n, steps + 1, 3))
    C[:, 0] = roots + normals * clearance[:, None] * 0.6
    d = direction_fn(C[:, 0], normals)
    seg = (lengths / steps)[:, None]
    for k in range(1, steps + 1):
        want = direction_fn(C[:, k - 1], normals)
        g = np.array([0.0, -1.0, 0.0]) * gravity * (k / steps)
        noise = (rng.random((n, 3)) - 0.5) * jitter
        d = stiffness * d + (1 - stiffness) * want + g + noise
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        p = C[:, k - 1] + d * seg
        if hug is not None:
            p = surface.hug(p, clearance, hug(k / steps))
        p = surface.push_out(p, clearance)
        # keep the step length
        dd = p - C[:, k - 1]
        dd /= np.linalg.norm(dd, axis=1, keepdims=True)
        C[:, k] = C[:, k - 1] + dd * seg
        d = dd
    return C


def cards(curves, normals, widths, col, rng, ao_inner=1.0, flip_u=False):
    """Ribbon geometry for each curve. UV: u across the atlas column, v root->tip. Colour: R = card random value,
    A = layer occlusion (0..255)."""
    n, m, _ = curves.shape
    pos, nrm, uv, col8, tri, root_of = [], [], [], [], [], []
    c0, c1 = COLUMNS[col][0] / ATLAS, COLUMNS[col][1] / ATLAS
    base = 0
    for i in range(n):
        Cc = curves[i]
        t = np.gradient(Cc, axis=0)
        t /= np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
        side = np.cross(t, normals[i])
        side /= np.maximum(np.linalg.norm(side, axis=1, keepdims=True), 1e-9)
        nn = np.cross(side, t)
        w = widths[i] * (1.0 - 0.45 * np.linspace(0, 1, m) ** 2)
        u0 = c0 + rng.random() * 0.05 * (c1 - c0)
        u1 = c1 - rng.random() * 0.05 * (c1 - c0)
        rv = int(rng.integers(0, 256))
        for k in range(m):
            for s, u in ((-1, u0), (1, u1)):
                pos.append(Cc[k] + side[k] * w[k] * 0.5 * s)
                nrm.append(nn[k])
                uv.append((u, k / (m - 1)))
                col8.append((rv, 255, 255, int(255 * ao_inner)))
                root_of.append(i)
        for k in range(m - 1):
            a = base + 2 * k
            tri += [(a, a + 2, a + 1), (a + 1, a + 2, a + 3)]
        base += 2 * m
    return (np.array(pos), np.array(nrm), np.array(uv), np.array(col8, np.uint8), np.array(tri, np.int64).reshape(-1, 3),
            np.array(root_of))


def attach(ident, surface, tri, bary, root_of, P):
    """Skin weights and expression deltas of card vertices from the skin under each root."""
    A = ident.A
    F = surface.F
    vids = F[tri[root_of]]                          # (nv, 3) skin vertices under each card vertex's root
    b = bary[root_of]
    W = np.zeros((len(P), len(INDEX)))
    for c in range(3):
        for k in range(4):
            j = A.joints[vids[:, c], k]
            w = A.weights[vids[:, c], k] / 255.0 * b[:, c]
            np.add.at(W, (np.arange(len(P)), j), w)
    from rig import quantize_weights
    joints, weights = quantize_weights(W)
    deltas = {}
    for name, d in ident.expr.items():
        deltas[name] = (d[vids] * b[:, :, None]).sum(1)
    return joints, weights, deltas


def build_part(name, material, ident, surface, P, N, UV, C8, T, tri, bary, root_of, morphs=True):
    joints, weights, deltas = attach(ident, surface, tri, bary, root_of, P)
    tan = meshutil.tangents(P, N, UV, T)
    mesh = {"name": name, "material": material, "pos": P, "nrm": N, "tan": tan, "uv": UV, "color": C8,
            "joints": joints, "weights": weights, "idx": T.ravel()}
    mlist = []
    if morphs:
        for ename in sorted(deltas):
            dp = deltas[ename]
            keep = np.nonzero(np.abs(dp).max(1) > 2e-6)[0]
            mlist.append((ename, keep, dp[keep], np.zeros((len(keep), 3))))
    return mesh, mlist


def head_frame(fields):
    """The crown (top of the head) and the occiput (the back of the skull at brow height)."""
    V = fields.V[fields.body]
    band = (np.abs(V[:, 0]) < 0.02) & (np.abs(V[:, 1] - (fields.eye_y + 0.03)) < 0.03)
    back = V[band][np.argmin(V[band][:, 2])]
    return fields.head_top, back


def _tangential(d, nrm, keep=1.0):
    d = d - (d * nrm).sum(1, keepdims=True) * nrm * keep
    return d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)


def tie_point(fields):
    top, back = head_frame(fields)
    return back + np.array([0.0, -0.005, -0.012])


def scalp_comb(fields, kind):
    """Comb direction of scalp hair by style: tied back (towards the tie at the occiput), medium (falling from the
    crown), short and cropped (back and down from the crown)."""
    top, back = head_frame(fields)
    tie = tie_point(fields)
    crown = top + np.array([0.0, 0.0, -0.035])

    def comb(p, nrm):
        if kind == "tied_back":
            d = tie - p
        elif kind == "medium":
            d = (p - crown) * np.array([1.0, 0.0, 1.0]) + np.array([0.0, -0.06, -0.01])
        else:
            d = np.array([0.0, -0.02, -0.05]) + (p - crown) * np.array([1.0, 0.0, 0.6])
        return _tangential(d, nrm)
    return comb


def beard_comb(fields):
    """Beard hair grows down and slightly forward, fanning out from the chin."""
    def comb(p, nrm):
        d = np.array([0.0, -1.0, 0.25]) + 0.25 * (p - fields.chin_low) * np.array([1.0, 0.0, 0.0]) / 0.05
        return _tangential(d, nrm, 0.85)
    return comb


def brow_comb(fields):
    """Brow hairs point outwards, rising at the inner end and lying flat towards the tail."""
    def comb(p, nrm):
        s = np.sign(p[:, 0])
        uu = (p[:, 0] - np.where(s > 0, fields.eye_l[0], fields.eye_r[0])) * s
        up = np.clip(0.9 - (uu + 0.02) / 0.03, -0.3, 0.9)
        d = np.stack([s, up, np.zeros(len(p))], 1)
        return _tangential(d, nrm)
    return comb


def paint_layers(ident, fields, surface, spec, size, rng):
    """Painted hair under the cards: scalp hair (with a hairline that thins out strand by strand), beard or stubble,
    and brows, as (rgb_lin, coverage) layers at the skin texture size. Densities scale with the texture size."""
    layers = []
    k = (size / 2048.0) ** 2
    style = spec["hair"]["style"]
    hair_lin = hex_lin(spec["hair"]["colour"])
    if style not in ("shaved", "none"):
        scalp = fields.scalp(spec["hair"].get("hairline_drop", 0.0))
        L = {"cropped": (0.003, 0.007), "short": (0.010, 0.020), "medium": (0.014, 0.026), "tied_back": (0.014, 0.026)}[style]
        layers.append(paint_strokes(ident, surface, scalp, scalp_comb(fields, style), int(36000 * k) + 4000, L, 4, rng,
                                    size, hair_lin * 0.85, opacity=0.95, value_jitter=0.35, min_field=0.15) + ("scalp",))
    bd = spec.get("beard") or {}
    if bd.get("style") not in (None, "none"):
        field = fields.beard(1.0)
        if bd["style"] == "moustache":
            Pv = fields.V
            mz = 1 - np.clip(np.linalg.norm((Pv - (fields.mouth + np.array([0, 0.011, 0.004]))) * np.array([0.7, 1.6, 1.0]), axis=1) / 0.03, 0, 1)
            field = field * np.clip(mz * 1.5, 0, 1)
        stub = bd["style"] == "stubble"
        L = (0.0012, 0.0025) if stub else (0.006, 0.014)
        n = int((26000 if stub else 20000) * k) + 3000
        layers.append(paint_strokes(ident, surface, field, beard_comb(fields), n, L, 2 if stub else 3, rng, size,
                                    hair_lin * 0.6, opacity=0.55 if stub else 0.95, value_jitter=0.3, min_field=0.15,
                                    width=1 if stub else 2) + ("beard",))
    layers.append(paint_strokes(ident, surface, fields.brows(), brow_comb(fields), int(1400 * k) + 300, (0.004, 0.008), 3,
                                rng, size, hair_lin * 0.9, opacity=0.6, value_jitter=0.3, min_field=0.3) + ("brows",))
    return layers


def hex_lin(h):
    return texio.srgb_to_linear(np.array([int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)]) / 255.0)


def scalp_hair(ident, fields, surface, style, rng, lod=0):
    """Scalp hair cards for a style: short, cropped, medium, tied_back. Returns a list of (mesh, morphs)."""
    kind = style["style"]
    if kind in ("shaved", "none"):
        return []
    scalp = fields.scalp(style.get("hairline_drop", 0.0))
    top, back = head_frame(fields)
    tie = tie_point(fields)
    cfg = {
        "cropped": dict(n=1600, length=(0.008, 0.016), steps=3, width=(0.006, 0.010), clear=(0.001, 0.003), grav=0.0, col=3),
        "short": dict(n=1500, length=(0.030, 0.055), steps=5, width=(0.010, 0.016), clear=(0.0015, 0.007), grav=0.05, col=1),
        "medium": dict(n=1500, length=(0.08, 0.13), steps=7, width=(0.012, 0.020), clear=(0.002, 0.011), grav=0.12, col=1),
        "tied_back": dict(n=1600, length=(0.07, 0.16), steps=8, width=(0.011, 0.017), clear=(0.0010, 0.0045), grav=0.04, col=0),
    }[kind]
    if lod:
        cfg = dict(cfg, n=max(40, cfg["n"] // 12), width=(cfg["width"][0] * 3.0, cfg["width"][1] * 3.2))
    n = cfg["n"]
    P, Nn, tri, bary = surface.sample(scalp, n, rng)
    # layers: inner cards hug the scalp (darker), outer cards sit higher
    layer = rng.random(n)
    clear = cfg["clear"][0] + (cfg["clear"][1] - cfg["clear"][0]) * layer
    lengths = cfg["length"][0] + (cfg["length"][1] - cfg["length"][0]) * rng.random(n)
    comb = scalp_comb(fields, kind)

    hugf = {"tied_back": lambda u: 0.95, "cropped": lambda u: 0.9, "short": lambda u: 0.7 * (1 - 0.5 * u),
            "medium": lambda u: 0.6 * (1 - u)}[kind]
    curves = grow(surface, P, Nn, lengths, cfg["steps"], comb, clear, cfg["grav"], rng, stiffness=0.3, jitter=0.06, hug=hugf)
    widths = cfg["width"][0] + (cfg["width"][1] - cfg["width"][0]) * rng.random(n)
    pos, nrm, uv, c8, T, root_of = cards(curves, Nn, widths, cfg["col"], rng)
    c8[:, 3] = (255 * (0.55 + 0.45 * layer[root_of])).astype(np.uint8)
    parts = [build_part("hair" if not lod else "hair_lod", "hair", ident, surface, pos, nrm, uv, c8, T, tri, bary, root_of, morphs=False)]
    if kind == "tied_back":
        parts.append(ponytail(ident, surface, tie, rng, lod))
    return parts


def ponytail(ident, surface, tie, rng, lod):
    """A queue hanging from the tie point: a bundle of cards around a curved axis, skinned head -> neck -> spine."""
    n = 26 if not lod else 6
    steps = 7
    L = 0.24
    axis = np.zeros((steps + 1, 3))
    for k in range(steps + 1):
        t = k / steps
        axis[k] = tie + np.array([0.0, -L * t, -0.02 * np.sin(t * 2.0) - 0.012 * t])
    curves = np.zeros((n, steps + 1, 3))
    normals = np.zeros((n, 3))
    for i in range(n):
        a = 2 * np.pi * i / n + rng.random() * 0.3
        r0 = 0.010 + 0.004 * rng.random()
        off = np.array([np.cos(a), 0.0, np.sin(a)])
        for k in range(steps + 1):
            t = k / steps
            r = r0 * (1.0 + 0.6 * np.sin(t * np.pi) - 0.5 * t)
            curves[i, k] = axis[k] + off * r
        normals[i] = off
    widths = np.full(n, 0.022 if not lod else 0.05)
    pos, nrm, uv, c8, T, root_of = cards(curves, normals, widths, 0, rng)
    # skin: head at the tie, then neck and upper back
    t = np.clip((tie[1] - pos[:, 1]) / L, 0, 1)
    W = np.zeros((len(pos), len(INDEX)))
    W[:, INDEX["head"]] = 1 - np.clip(t * 1.6, 0, 1)
    W[:, INDEX["neck_01"]] = np.clip(t * 1.6, 0, 1) * (1 - np.clip((t - 0.5) * 2, 0, 1))
    W[:, INDEX["spine_03"]] = np.clip((t - 0.5) * 2, 0, 1)
    from rig import quantize_weights
    joints, weights = quantize_weights(W)
    tan = meshutil.tangents(pos, nrm, uv, T)
    return ({"name": "ponytail" if not lod else "ponytail_lod", "material": "hair", "pos": pos, "nrm": nrm, "tan": tan,
             "uv": uv, "color": c8, "joints": joints, "weights": weights, "idx": T.ravel()}, [])


def brows(ident, fields, surface, rng, density=1.0):
    field = fields.brows()
    n = int(200 * density)
    P, Nn, tri, bary = surface.sample(field, n, rng, min_field=0.35)
    side = np.sign(P[:, 0])
    eye_x = np.where(side > 0, fields.eye_l[0], fields.eye_r[0])
    u = (P[:, 0] - eye_x) * side  # + outward
    lengths = 0.006 + 0.004 * rng.random(n)
    clear = np.full(n, 0.0008)

    comb = brow_comb(fields)

    curves = grow(surface, P, Nn, lengths, 2, comb, clear, 0.0, rng, stiffness=0.3, jitter=0.2)
    widths = 0.0018 + 0.0010 * rng.random(n)
    pos, nrm, uv, c8, T, root_of = cards(curves, Nn, widths, 4, rng)
    return [build_part("brows", "brow", ident, surface, pos, nrm, uv, c8, T, tri, bary, root_of)]


def lashes(ident):
    """Upper and lower lash rows on the CC0 base-mesh lash helpers, with code-generated UVs (root 0 -> tip 1)."""
    A = ident.A
    out = []
    for grp in ("helper-l-eyelashes-1", "helper-l-eyelashes-2", "helper-r-eyelashes-1", "helper-r-eyelashes-2"):
        fq = A.mesh.group_faces(grp)
        fv = A.mesh.fv[fq]
        verts = np.unique(fv)
        side = 0 if grp.startswith("helper-l") else 1
        c = ident.eye_centres[side]
        P = ident.Vm[verts]
        dist = np.linalg.norm(P - c, axis=1)
        root = dist < np.median(dist)
        ang = np.arctan2(P[:, 1] - c[1], P[:, 0] - c[0])
        u = (ang - ang.min()) / max(np.ptp(ang), 1e-6)
        out.append((grp, verts, u, root))
    # one part for all four rows
    pos, nrm, uv, joints, weights, tris, src = [], [], [], [], [], [], []
    base = 0
    for grp, verts, u, root in out:
        remap = {int(v): i for i, v in enumerate(verts)}
        fq = A.mesh.group_faces(grp)
        F = meshutil.quads_to_tris(A.mesh.fv[fq])
        for v_i, v in enumerate(verts):
            pos.append(ident.Vm[v])
            uv.append((0.875 + 0.125 * (0.1 + 0.8 * u[v_i]), 0.02 if root[v_i] else 0.6))
            joints.append(A.joints[v])
            weights.append(A.weights[v])
            src.append(v)
        for t in F:
            tris.append([remap[int(x)] + base for x in t])
        base += len(verts)
    P = np.array(pos)
    T = np.array(tris, np.int64)
    N = meshutil.vertex_normals(P, T)
    UV = np.array(uv)
    tan = meshutil.tangents(P, N, UV, T)
    src = np.array(src)
    mesh = {"name": "lashes", "material": "lash", "pos": P, "nrm": N, "tan": tan, "uv": UV,
            "color": np.tile(np.array([128, 255, 255, 255], np.uint8), (len(P), 1)),
            "joints": np.array(joints, np.uint8), "weights": np.array(weights, np.uint8), "idx": T.ravel()}
    morphs = []
    for name in sorted(ident.expr):
        d = ident.expr[name][src]
        keep = np.nonzero(np.abs(d).max(1) > 2e-6)[0]
        morphs.append((name, keep, d[keep], np.zeros((len(keep), 3))))
    return [(mesh, morphs)]


# ---------------------------------------------------------------------------------------------- painted hair
def uv_maps(ident, tri):
    """Per body triangle: root UV corners and the linear map M (2x3) from a 3D displacement on the triangle's plane
    to the UV displacement (the triangle's own parameterisation, v flipped to image rows)."""
    A = ident.A
    P = ident.Vm[A.body_fv[tri]]                     # (n,3,3)
    U = A.mesh.UV[A.body_fuv[tri]].copy()            # (n,3,2)
    U[..., 1] = 1.0 - U[..., 1]
    E = np.stack([P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]], 2)       # (n,3,2)
    D = np.stack([U[:, 1] - U[:, 0], U[:, 2] - U[:, 0]], 2)       # (n,2,2)
    EtE = np.einsum("nki,nkj->nij", E, E)
    det = EtE[:, 0, 0] * EtE[:, 1, 1] - EtE[:, 0, 1] * EtE[:, 1, 0]
    det = np.where(np.abs(det) < 1e-20, 1e-20, det)
    inv = np.stack([np.stack([EtE[:, 1, 1], -EtE[:, 0, 1]], 1), np.stack([-EtE[:, 1, 0], EtE[:, 0, 0]], 1)], 1) / det[:, None, None]
    pinv = np.einsum("nij,nkj->nik", inv, E)          # (n,2,3)
    M = np.einsum("nij,njk->nik", D, pinv)            # (n,2,3)
    return U, M


def field_uv_mask(ident, field, size):
    """The field rasterised into UV space (for masking strokes that leave their island)."""
    import uvraster
    A = ident.A
    body = ident.A.mesh  # noqa: F841
    fv, fuv = A.body_fv, A.body_fuv
    UV = A.mesh.UV.copy()
    UV[:, 1] = 1.0 - UV[:, 1]
    # rasterise per corner: build a corner-indexed triangle list
    corners_uv = UV[fuv.ravel()]
    vals = field[fv.ravel()][:, None]
    F = np.arange(len(corners_uv)).reshape(-1, 3)
    img, cov = uvraster.rasterize(corners_uv, F, vals, size)
    return img[..., 0]


def paint_strokes(ident, surface, field, comb, count, length, steps, rng, size, colour_lin, opacity=1.0, width=1,
                  ss=2, value_jitter=0.25, min_field=0.2, base=None):
    """Strand strokes painted into UV space: roots sampled on the field, paths grown on the skin along the comb
    direction (surface-hugging), mapped to UV through each root triangle's linear UV map, drawn supersampled ss x
    with per-stroke brightness, then box-filtered. Returns (rgb_lin (size,size,3), coverage (size,size))."""
    from PIL import Image, ImageDraw
    P0, N0, tri, bary = surface.sample(field, count, rng, min_field=min_field)
    if len(P0) == 0:
        return np.zeros((size, size, 3)), np.zeros((size, size))
    lens = length[0] + (length[1] - length[0]) * rng.random(len(P0))
    curves = grow(surface, P0, N0, lens, steps, comb, np.zeros(len(P0)), 0.0, rng, stiffness=0.5, jitter=0.18,
                  hug=lambda u: 1.0)
    U, M = uv_maps(ident, tri)
    uv0 = (U * bary[:, :, None]).sum(1)
    d = curves - curves[:, :1, :]
    uvs = uv0[:, None, :] + np.einsum("nij,nkj->nki", M, d)
    S = size * ss
    cov = Image.new("L", (S, S), 0)
    col = Image.new("RGB", (S, S), (0, 0, 0))
    dc, dr = ImageDraw.Draw(cov), ImageDraw.Draw(col)
    vals = 1.0 + value_jitter * (rng.random(len(P0)) * 2 - 1)
    import texio
    srgb = np.clip(texio.linear_to_srgb(np.clip(colour_lin[None, :] * vals[:, None], 0, 1)) * 255 + 0.5, 0, 255).astype(int)
    a8 = int(round(255 * opacity))
    for i in range(len(P0)):
        pts = [(float(x) * S, float(y) * S) for x, y in uvs[i]]
        dc.line(pts, fill=a8, width=width)
        dr.line(pts, fill=tuple(int(c) for c in srgb[i]), width=width)
    covf = np.asarray(cov).astype(np.float64) / 255.0
    rgb = texio.srgb_to_linear(np.asarray(col).astype(np.float64) / 255.0)
    # premultiplied box filter down to the texture size
    k = ss
    covd = covf.reshape(size, k, size, k).mean((1, 3))
    rgbd = (rgb * covf[..., None]).reshape(size, k, size, k, 3).mean((1, 3)) / np.maximum(covd[..., None], 1e-6)
    if base is not None:
        mask = base > 0.02
        covd = covd * mask
    return rgbd, covd


# ---------------------------------------------------------------------------------------------- fur shells
FUR = 256


def fur_texture(seed=17):
    """Tiling strand cross-sections for shells: R = strand length (0 = no strand; a strand shows on the shells below
    its length), G = per-strand tone, B = 1 inside a strand, A = 1. About 900 strands per tile on a jittered grid."""
    rng = np.random.default_rng(seed)
    R = np.zeros((FUR, FUR))
    G = np.full((FUR, FUR), 0.5)
    B = np.zeros((FUR, FUR))
    n = 30
    cell = FUR / n
    yy, xx = np.mgrid[0:FUR, 0:FUR]
    for gy in range(n):
        for gx in range(n):
            cx = (gx + 0.15 + 0.7 * rng.random()) * cell
            cy = (gy + 0.15 + 0.7 * rng.random()) * cell
            rad = 1.3 + 1.1 * rng.random()
            length = 0.35 + 0.65 * rng.random() ** 0.6
            tone = rng.random()
            x0, y0 = int(cx - 4), int(cy - 4)
            ys = np.arange(y0, y0 + 10) % FUR
            xs = np.arange(x0, x0 + 10) % FUR
            dy = (np.arange(y0, y0 + 10) - cy)[:, None]
            dx = (np.arange(x0, x0 + 10) - cx)[None, :]
            inside = np.clip(rad - np.sqrt(dx * dx + dy * dy) + 0.5, 0, 1)
            sub = np.ix_(ys, xs)
            R[sub] = np.maximum(R[sub], length * (inside > 0.5))
            G[sub] = np.where(inside > 0.5, tone, G[sub])
            B[sub] = np.maximum(B[sub], inside)
    data = np.stack([R, G, B, np.ones_like(R)], -1)
    return texio.mips_data(data)


def fur_shells(ident, fields, surface, field, length_fn, comb, name, layers, shear, density_floor=0.08):
    """Fur shells over the skin region where `field` > density_floor: `layers` offset copies of those skin triangles,
    layer k at p + d * L(p) * k/layers with d = normalize(normal + shear * comb) (the strands lean along the comb).
    Vertex colour: R = density (the field), A = layer fraction; tangent = strand direction. Skinned and morphed with
    the skin vertex under each shell vertex, so the beard opens with the jaw."""
    A = ident.A
    F = A.body_fv
    fv = field[F]
    tri = np.nonzero(fv.max(1) > density_floor)[0]
    Ft = F[tri]
    src = np.unique(Ft.ravel())
    remap = -np.ones(len(ident.Vm), np.int64)
    remap[src] = np.arange(len(src))
    Fl = remap[Ft]
    # UVs: the skin's own (one corner per vertex; the beard lies inside the face island)
    fuv = A.body_fuv[tri].ravel()
    uvv = np.zeros((len(src), 2))
    uvv[remap[Ft.ravel()]] = A.mesh.UV[fuv]
    uvv[:, 1] = 1 - uvv[:, 1]
    P0 = ident.Vm[src]
    N0 = ident.N[src]
    L = length_fn(P0)
    c = comb(P0, N0)
    d = N0 + shear * c
    d /= np.linalg.norm(d, axis=1, keepdims=True)
    dens = np.clip(field[src], 0, 1)
    pos, nrm, tan, uv, col, T, srcl = [], [], [], [], [], [], []
    for k in range(1, layers + 1):
        f = k / layers
        base = (k - 1) * len(src)          # vertex offset of this layer (every layer repeats the region's vertices)
        pos.append(P0 + d * (L * f)[:, None])
        nrm.append(N0)
        tan.append(np.concatenate([c, np.ones((len(src), 1))], 1))
        uv.append(uvv)
        cc = np.zeros((len(src), 4))
        cc[:, 0] = dens
        cc[:, 1] = 1.0
        cc[:, 3] = f
        col.append(cc)
        T.append(Fl + base)
        srcl.append(src)
    pos = np.concatenate(pos); nrm = np.concatenate(nrm); tan = np.concatenate(tan); uv = np.concatenate(uv)
    col = (np.clip(np.concatenate(col), 0, 1) * 255).astype(np.uint8)
    T = np.concatenate(T)
    srcl = np.concatenate(srcl)
    mesh = {"name": name, "material": "fur", "pos": pos, "nrm": nrm, "tan": tan, "uv": uv, "color": col,
            "joints": A.joints[srcl], "weights": A.weights[srcl], "idx": T.ravel()}
    morphs = []
    for ename in sorted(ident.expr):
        dp = ident.expr[ename][srcl]
        keep = np.nonzero(np.abs(dp).max(1) > 2e-6)[0]
        morphs.append((ename, keep, dp[keep], np.zeros((len(keep), 3))))
    return mesh, morphs


def beard_fur(ident, fields, surface, spec, lod=0):
    """Beard as fur shells: full and short beards (12 layers), moustaches (8), stubble (3 short layers)."""
    if not spec or spec.get("style") in (None, "none"):
        return []
    kind = spec["style"]
    field = fields.beard(1.0)
    if kind == "moustache":
        Pv = fields.V
        mz = 1 - np.clip(np.linalg.norm((Pv - (fields.mouth + np.array([0, 0.011, 0.004]))) * np.array([0.7, 1.6, 1.0]), axis=1) / 0.03, 0, 1)
        field = field * np.clip(mz * 1.5, 0, 1)
    base_len = {"stubble": 0.0018}.get(kind, spec.get("length", 0.025) * (0.6 if kind == "short" else 1.0))

    def length_fn(P):
        chin = np.clip(1 - np.linalg.norm(P - fields.chin_low, axis=1) / 0.07, 0, 1)
        near_mouth = np.clip(1 - np.linalg.norm((P - fields.mouth) * np.array([0.8, 1.0, 1.0]), axis=1) / 0.028, 0, 1)
        k = np.ones(len(P)) if kind == "stubble" else (0.55 + 0.9 * chin) * (1 - 0.6 * near_mouth)
        return base_len * k

    layers = {"stubble": 3, "moustache": 8}.get(kind, 12)
    if lod:
        layers = 2
    shear = 0.4 if kind == "stubble" else 1.6
    return [fur_shells(ident, fields, surface, field, length_fn, beard_comb(fields), "beard" if not lod else "beard_lod", layers, shear)]


# ---------------------------------------------------------------------------------------------- scalp shells
def lying_texture(seed=23, W=512, H=512):
    """Tiling texture of hair lying along +v (rows): 300 strands across, each made of long segments with staggered
    ends, gently waving. R = strand height (a strand shows on the shells below it), G = tone, B = coverage, A = 1.
    Square (the engine's mip upload expects square or power-of-two halving in both axes)."""
    rng = np.random.default_rng(seed)
    R = np.zeros((H, W))
    G = np.full((H, W), 0.5)
    B = np.zeros((H, W))
    rows = np.arange(H)
    for s in range(300):
        x0 = rng.random() * W
        amp = 0.6 + 1.6 * rng.random()
        freq = (1 + rng.integers(0, 3)) / H * 2 * np.pi
        ph = rng.random() * 2 * np.pi
        width = 0.7 + 0.9 * rng.random()
        xc = x0 + amp * np.sin(rows * freq + ph)
        # segments: each 120..480 rows long with a short gap; per segment its own height and tone
        start = rng.random() * H
        pos = 0.0
        while pos < H:
            seglen = 120 + 360 * rng.random()
            gap = 4 + 30 * rng.random()
            height = 0.25 + 0.75 * rng.random() ** 0.7
            tone = rng.random()
            a = (start + pos) % H
            idx = (np.arange(int(seglen)) + int(a)) % H
            u = np.arange(int(seglen)) / max(seglen, 1)
            taper = np.clip(u / 0.06, 0, 1) * np.clip((1 - u) / 0.15, 0, 1)
            for dx in range(-2, 3):
                xx = (np.floor(xc[idx]).astype(int) + dx) % W
                cov = np.clip(width * taper - np.abs(np.floor(xc[idx]) + dx + 0.5 - xc[idx]) + 0.5, 0, 1)
                upd = cov > B[idx, xx]
                R[idx[upd], xx[upd]] = height
                G[idx[upd], xx[upd]] = tone
                B[idx[upd], xx[upd]] = cov[upd]
            pos += seglen + gap
    data = np.stack([R, G, B, np.ones_like(R)], -1)
    return texio.mips_data(data)


def polar_uv(P, pole, pole_n, repeats, along):
    """Flow-aligned UVs around a pole (the tie point or the crown): u = angle around the pole's normal axis
    (repeats tiles per turn), v = distance from the pole / along. Meridians from the pole run along +v, which is the
    direction the hair lies in for tied-back and crown-combed styles."""
    n = pole_n / np.linalg.norm(pole_n)
    e1 = np.cross(n, [0.0, 1.0, 0.0])
    if np.linalg.norm(e1) < 1e-6:
        e1 = np.cross(n, [1.0, 0.0, 0.0])
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(n, e1)
    d = P - pole
    th = np.arctan2(d @ e2, d @ e1)
    r = np.linalg.norm(d, axis=1)
    return np.stack([th / (2 * np.pi) * repeats, r / along], 1)


def scalp_shells(ident, fields, surface, style, lod=0):
    """Scalp hair as shells: tied back (7 mm, towards the tie), short and cropped (from the crown), medium (from the
    crown; the hanging lengths stay cards). Hairline: density = the scalp field, so strands thin out at the edge."""
    kind = style["style"]
    if kind in ("shaved", "none"):
        return []
    A = ident.A
    scalp = fields.scalp(style.get("hairline_drop", 0.0))
    top, back = head_frame(fields)
    if kind == "tied_back":
        pole = tie_point(fields)
    else:
        pole = top + np.array([0.0, -0.004, -0.045])
    j = surface.grid.nearest(pole[None])[0]
    pole_n = surface.cloud_n[j]
    thick, layers = {"tied_back": (0.007, 10), "short": (0.011, 10), "cropped": (0.005, 7), "medium": (0.014, 12)}[kind]
    if lod:
        layers = 2
    F = A.body_fv
    tri = np.nonzero(scalp[F].max(1) > 0.05)[0]
    Ft = F[tri]
    src = np.unique(Ft.ravel())
    P0 = ident.Vm[src]
    N0 = ident.N[src]
    comb = scalp_comb(fields, kind)
    c = comb(P0, N0)
    d = N0 + 0.25 * c
    d /= np.linalg.norm(d, axis=1, keepdims=True)
    uvp = polar_uv(P0, pole, pole_n, 26.0, 0.07)
    # split triangles across the u seam (angle wrap): per-corner UVs, unwrapped per triangle, then de-duplicated
    remap = -np.ones(len(ident.Vm), np.int64)
    remap[src] = np.arange(len(src))
    Fl = remap[Ft]
    cu = uvp[Fl, 0]
    lo = cu.min(1, keepdims=True)
    cu = np.where(cu - lo > 13.0, cu - 26.0, cu)
    keys = {}
    vid, cuv, T = [], [], []
    for t in range(len(Fl)):
        tri_ids = []
        for k in range(3):
            key = (int(Fl[t, k]), round(float(cu[t, k]) - float(uvp[Fl[t, k], 0])))
            if key not in keys:
                keys[key] = len(vid)
                vid.append(Fl[t, k])
                cuv.append((cu[t, k], uvp[Fl[t, k], 1]))
            tri_ids.append(keys[key])
        T.append(tri_ids)
    vid = np.array(vid)
    cuv = np.array(cuv)
    T = np.array(T, np.int64)
    sv = src[vid]
    dens = np.clip(scalp[sv], 0, 1)
    pos, nrm, tan, uv, col, TT, srcl = [], [], [], [], [], [], []
    for k in range(1, layers + 1):
        f = k / layers
        base = len(vid) * (k - 1)
        pos.append(P0[vid] + d[vid] * (thick * f))
        nrm.append(N0[vid])
        tan.append(np.concatenate([c[vid], np.ones((len(vid), 1))], 1))
        uv.append(cuv)
        cc = np.zeros((len(vid), 4))
        cc[:, 0] = dens
        cc[:, 1] = 1.0
        cc[:, 3] = f
        col.append(cc)
        TT.append(T + base)
        srcl.append(sv)
    pos = np.concatenate(pos); nrm = np.concatenate(nrm); tan = np.concatenate(tan); uv = np.concatenate(uv)
    col = (np.clip(np.concatenate(col), 0, 1) * 255).astype(np.uint8)
    TT = np.concatenate(TT)
    srcl = np.concatenate(srcl)
    mesh = {"name": "hair" if not lod else "hair_lod", "material": "hairshell", "pos": pos, "nrm": nrm, "tan": tan, "uv": uv,
            "color": col, "joints": A.joints[srcl], "weights": A.weights[srcl], "idx": TT.ravel()}
    return [(mesh, [])]
