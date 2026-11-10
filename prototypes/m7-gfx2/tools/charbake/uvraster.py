"""Rasterises per-vertex attributes of a UV-mapped triangle mesh into texture space (barycentric interpolation)."""
import numpy as np


def rasterize(UV, F, values, size):
    """UV: (n,2) in [0,1] (row 0 = v 0); F: (m,3); values: (n,k). Returns (size,size,k) image and coverage mask."""
    k = values.shape[1]
    img = np.zeros((size, size, k))
    cov = np.zeros((size, size), bool)
    P = UV * size - 0.5
    for tri in F:
        a, b, c = P[tri[0]], P[tri[1]], P[tri[2]]
        x0 = int(np.floor(min(a[0], b[0], c[0])))
        x1 = int(np.ceil(max(a[0], b[0], c[0])))
        y0 = int(np.floor(min(a[1], b[1], c[1])))
        y1 = int(np.ceil(max(a[1], b[1], c[1])))
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / d
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / d
        w2 = 1 - w0 - w1
        e = -1e-3
        inside = (w0 >= e) & (w1 >= e) & (w2 >= e)
        if not inside.any():
            continue
        vals = (w0[inside, None] * values[tri[0]] + w1[inside, None] * values[tri[1]] + w2[inside, None] * values[tri[2]])
        img[ys[inside], xs[inside]] = vals
        cov[ys[inside], xs[inside]] = True
    return img, cov


def dilate(img, cov, iters=16):
    img = img.copy()
    m = cov.copy()
    for _ in range(iters):
        acc = np.zeros_like(img)
        cnt = np.zeros(m.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sm = np.roll(m, (dy, dx), (0, 1))
            acc += np.roll(img, (dy, dx), (0, 1)) * sm[..., None]
            cnt += sm
        grow = (~m) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        m |= grow
    return img


def sample(tex, UV):
    """Bilinear sample of a (h,w) or (h,w,k) array at UVs (row 0 = v 0)."""
    h, w = tex.shape[:2]
    x = np.clip(UV[:, 0] * w - 0.5, 0, w - 1)
    y = np.clip(UV[:, 1] * h - 0.5, 0, h - 1)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    x1, y1 = np.minimum(x0 + 1, w - 1), np.minimum(y0 + 1, h - 1)
    fx, fy = x - x0, y - y0
    if tex.ndim == 2:
        fx2, fy2 = fx, fy
    else:
        fx2, fy2 = fx[:, None], fy[:, None]
    return (tex[y0, x0] * (1 - fx2) + tex[y0, x1] * fx2) * (1 - fy2) + (tex[y1, x0] * (1 - fx2) + tex[y1, x1] * fx2) * fy2
