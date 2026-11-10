"""Debug previews for the bake (never shipped): painter's-algorithm flat-shaded renders with Pillow."""
import numpy as np
from PIL import Image, ImageDraw


def look_at(eye, target, up=(0, 1, 0)):
    eye, target, up = np.asarray(eye, float), np.asarray(target, float), np.asarray(up, float)
    f = target - eye
    f /= np.linalg.norm(f)
    r = np.cross(f, up)
    r /= np.linalg.norm(r)
    u = np.cross(r, f)
    return eye, np.stack([r, u, -f])


def render(parts, eye, target, size=(512, 512), fov_deg=30.0, light=(0.4, 0.8, 0.6), bg=(40, 44, 52)):
    """parts: list of (V (n,3), F (m,3) triangle indices, rgb tuple). Returns a PIL image."""
    w, h = size
    eye, R = look_at(eye, target)
    L = np.asarray(light, float)
    L /= np.linalg.norm(L)
    f = 0.5 * h / np.tan(np.radians(fov_deg) / 2)
    polys = []
    for V, F, col in parts:
        F = np.asarray(F, dtype=np.int64)
        if len(F) == 0:
            continue
        P = (np.asarray(V, float) - eye) @ R.T
        a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
        Wv = np.asarray(V, float)
        n = np.cross(Wv[F[:, 1]] - Wv[F[:, 0]], Wv[F[:, 2]] - Wv[F[:, 0]])
        nl = np.linalg.norm(n, axis=1)
        n = n / np.maximum(nl, 1e-12)[:, None]
        shade = np.clip(np.abs(n @ L), 0, 1) * 0.75 + 0.25
        z = (a[:, 2] + b[:, 2] + c[:, 2]) / 3
        ok = (a[:, 2] < -0.01) & (b[:, 2] < -0.01) & (c[:, 2] < -0.01)
        def proj(p):
            return np.stack([w / 2 + f * p[:, 0] / -p[:, 2], h / 2 - f * p[:, 1] / -p[:, 2]], 1)
        pa, pb, pc = proj(a), proj(b), proj(c)
        colv = np.asarray(col, float)
        for i in np.nonzero(ok)[0]:
            s = shade[i]
            polys.append((z[i], (tuple(pa[i]), tuple(pb[i]), tuple(pc[i])), tuple(int(min(255, x * s)) for x in colv)))
    polys.sort(key=lambda t: t[0])
    img = Image.new("RGB", size, bg)
    d = ImageDraw.Draw(img)
    for _, pts, c in polys:
        d.polygon(pts, fill=c)
    return img


def quads_to_tris(Fq):
    Fq = np.asarray(Fq)
    return np.concatenate([Fq[:, [0, 1, 2]], Fq[:, [0, 2, 3]]])
