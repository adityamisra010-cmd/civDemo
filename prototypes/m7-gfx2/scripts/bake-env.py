#!/usr/bin/env python3
"""Bake the G2 environment materials (stage env).

Reads pinned raw files from RAW_DIR (see assets/manifest.tsv) and writes R1TX textures (the render1 container:
"R1TX" u32 version=1, width, height, levels, format=0 (RGBA8), then every level, little endian) into OUT_DIR, plus
MANIFEST.tsv and calibration.json. Nothing it writes is committed; every transformation is listed in
assets/PROVENANCE.md (section "env bake").

Sections (all deterministic for a given numpy/Pillow version, recorded in MANIFEST.tsv):
  bark    ambientCG bark sets (CC0, via ez-tree): albedo calibrated to a natural mean, roughness, OpenGL normals,
          cavity from the normal map; mips in linear light with Toksvig-widened roughness.
  leaves  ez-tree leaf cards (MIT): alpha bleeding, albedo calibration (aspen recoloured from autumn yellow to summer
          green, two arid foliage recolours), translucency and a mild normal from luminance; mips keep the
          alpha-tested coverage of level 0 (coverage-preserving alpha scale per level).
  cover   ground-cover card atlas drawn in code (grass, dry grass, sedge, fern fronds, flowers, weeds): no external
          input.
  struct  structure materials: bark logs (CC0), drawn timber, wattle-and-daub, thatch, ashlar and rubble masonry
          (block layouts drawn in code, surface from Rock023, CC0), lime plaster, terracotta tiles, paving, turf.
  water   a tileable water-wave normal map (sum of directional Gerstner-like octaves, drawn in code).

Usage: bake-env.py RAW_DIR OUT_DIR [section ...]      (run through scripts/bake-env.sh)
"""
import hashlib
import json
import math
import os
import struct
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import PIL

# ---------------------------------------------------------------------------------------------------- helpers


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def lum(lin):
    return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float64) / 255.0


def write_r1tex(path, levels):
    h, w = levels[0].shape[:2]
    with open(path, "wb") as f:
        f.write(b"R1TX")
        f.write(struct.pack("<IIIII", 1, w, h, len(levels), 0))
        for lv in levels:
            f.write(np.ascontiguousarray(lv).tobytes())


def sha256(path):
    d = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            d.update(chunk)
    return d.hexdigest()


def to8(a):
    return np.clip(np.round(a * 255.0), 0, 255).astype(np.uint8)


def down2(a):
    """2x2 box filter; odd sizes keep their last row/column."""
    h, w = a.shape[:2]
    if h > 1 and h % 2:
        a = np.concatenate([a, a[-1:]], axis=0)
    if w > 1 and w % 2:
        a = np.concatenate([a, a[:, -1:]], axis=1)
    if a.shape[0] == 1:
        return 0.5 * (a[:, 0::2] + a[:, 1::2])
    if a.shape[1] == 1:
        return 0.5 * (a[0::2] + a[1::2])
    return 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])


def blur(a, r):
    """Separable box blur of radius r (wrap), repeated twice (tent-like)."""
    for _ in range(2):
        acc = np.zeros_like(a)
        for d in range(-r, r + 1):
            acc += np.roll(a, d, axis=0)
        a = acc / (2 * r + 1)
        acc = np.zeros_like(a)
        for d in range(-r, r + 1):
            acc += np.roll(a, d, axis=1)
        a = acc / (2 * r + 1)
    return a


def decode_normal(rgb):
    n = rgb * 2.0 - 1.0
    n[..., 2] = np.maximum(n[..., 2], 1e-3)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def normals_from_height(h, strength, wrap=True):
    if wrap:
        dcol = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
        drow = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    else:
        dcol = np.gradient(h, axis=1)
        drow = np.gradient(h, axis=0)
    n = np.stack([-dcol * strength, drow * strength, np.ones_like(h)], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.max(rgb, axis=-1)
    mn = np.min(rgb, axis=-1)
    d = mx - mn
    h = np.zeros_like(mx)
    nz = d > 1e-9
    rm = nz & (mx == r)
    gm = nz & (mx == g) & ~rm
    bm = nz & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6.0
    h[gm] = (b - r)[gm] / d[gm] + 2.0
    h[bm] = (r - g)[bm] / d[bm] + 4.0
    h = h / 6.0
    s = np.where(mx > 1e-9, d / np.maximum(mx, 1e-9), 0.0)
    return np.stack([h, s, mx], axis=-1)


def hsv_to_rgb(hsv):
    h, s, v = hsv[..., 0] % 1.0, hsv[..., 1], hsv[..., 2]
    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p = v * (1 - s)
    q = v * (1 - s * f)
    t = v * (1 - s * (1 - f))
    i = i.astype(int) % 6
    out = np.zeros(h.shape + (3,))
    for k, (a, b, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        m = i == k
        out[m, 0], out[m, 1], out[m, 2] = a[m], b[m], c[m]
    return out


def bleed(rgb, alpha, iters=48):
    """Alpha bleeding: fill transparent texels with the mean colour of their already-filled neighbours (no dark
    fringes when the filtered colour mixes in texels outside the card's alpha)."""
    filled = alpha > 0.02
    rgb = rgb.copy()
    rgb[~filled] = 0.0
    for _ in range(iters):
        if filled.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(filled.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            f = np.roll(np.roll(filled, dy, axis=0), dx, axis=1)
            c = np.roll(np.roll(rgb, dy, axis=0), dx, axis=1)
            acc += c * f[..., None]
            cnt += f
        new = (~filled) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]
        filled = filled | new
    if not filled.all():
        rgb[~filled] = rgb[filled].mean(axis=0)
    return rgb


def coverage(alpha, cutoff):
    return float((alpha > cutoff).mean())


def scale_alpha_to_coverage(alpha, target, cutoff):
    """Castano's coverage-preserving mip alpha: find s with coverage(alpha * s) == target (bisection)."""
    lo, hi = 0.0, 8.0
    for _ in range(24):
        mid = 0.5 * (lo + hi)
        if coverage(np.clip(alpha * mid, 0, 1), cutoff) < target:
            lo = mid
        else:
            hi = mid
    return np.clip(alpha * hi, 0.0, 1.0)


def mips_card(lin_rgb, alpha, nrm, extra, cutoff=0.5):
    """Card textures (alpha tested): colour averaged with alpha weights (premultiplied), alpha rescaled per level so
    the tested coverage stays that of level 0, normal averaged and renormalised. Returns (albedo+alpha levels,
    normal+extra levels) as RGBA8 arrays. extra: (H, W, 2) channels stored in B and A of the second texture."""
    alb_levels, nrm_levels = [], []
    c, a, n, e = lin_rgb, alpha, nrm, extra
    base_cov = coverage(alpha, cutoff)
    level = 0
    while True:
        a_use = a if level == 0 else scale_alpha_to_coverage(a, base_cov, cutoff)
        alb_levels.append(to8(np.concatenate([linear_to_srgb(c), a_use[..., None]], axis=-1)))
        nn = n / np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
        nrm_levels.append(to8(np.concatenate([nn[..., :2] * 0.5 + 0.5, e], axis=-1)))
        if c.shape[0] == 1 and c.shape[1] == 1:
            break
        w = np.maximum(a, 1e-4)[..., None]
        c = down2(c * w) / np.maximum(down2(w), 1e-6)
        n = down2(n * w) / np.maximum(down2(w), 1e-6)
        e = down2(e)
        a = down2(a)
        level += 1
    return alb_levels, nrm_levels


def mips_surface(lin_rgb, rough, nrm, ao):
    """Opaque surface textures: (albedo sRGB + roughness) and (normal xy + ao + 1); Toksvig-widened roughness."""
    alb_levels, nrm_levels = [], []
    c, r, n, o = lin_rgb, rough, nrm, ao
    while True:
        length = np.linalg.norm(n, axis=-1)
        nn = n / np.maximum(length[..., None], 1e-6)
        var = np.clip((1.0 - length) / np.maximum(length, 1e-6), 0.0, 1.0)
        r_adj = np.sqrt(np.sqrt(np.clip(r ** 4 + var, 0.0, 1.0)))
        alb_levels.append(to8(np.concatenate([linear_to_srgb(c), r_adj[..., None]], axis=-1)))
        nrm_levels.append(to8(np.concatenate([nn[..., :2] * 0.5 + 0.5, o[..., None], np.ones_like(o)[..., None]], axis=-1)))
        if c.shape[0] == 1 and c.shape[1] == 1:
            break
        c, r, n, o = down2(c), down2(r), down2(n), down2(o)
    return alb_levels, nrm_levels


class Out:
    def __init__(self, out):
        self.out = out
        self.rows = []
        self.calib = {}

    def write(self, name, levels, source):
        path = os.path.join(self.out, name)
        write_r1tex(path, levels)
        self.rows.append((name, sha256(path), os.path.getsize(path), source))

    def preview(self, name, rgba8):
        """A small PNG of level 0 for review (not used by the renderer)."""
        os.makedirs(os.path.join(self.out, "preview"), exist_ok=True)
        Image.fromarray(rgba8).save(os.path.join(self.out, "preview", name))


# ---------------------------------------------------------------------------------------------------- bark

EZ = "ez-tree/src/app/public/textures"
# id, ambientCG set, target mean linear luminance (bark of the species: oak grey-brown, ash grey, aspen pale
# greenish white, pine red-brown plates, shrub brown, scrub grey-brown), roughness floor
BARK = [
    ("oak", "Bark001", 0.12, 0.80),
    ("ash", "Bark004", 0.15, 0.78),
    ("aspen", "Bark007", 0.28, 0.70),
    ("pine", "Bark014", 0.11, 0.80),
    ("shrub", "Bark002", 0.11, 0.80),
    ("scrub", "Bark012", 0.13, 0.82),
]


def bake_bark(raw, o):
    for bid, aset, target, rfloor in BARK:
        base = f"{EZ}/bark/{aset}_1K-JPG/{aset}_1K-JPG_"
        col = load_rgba(os.path.join(raw, base + "Color.jpg"))[..., :3]
        nrm = decode_normal(load_rgba(os.path.join(raw, base + "NormalGL.jpg"))[..., :3])
        rgh = load_rgba(os.path.join(raw, base + "Roughness.jpg"))[..., 0]
        lin = srgb_to_linear(col)
        before = float(lum(lin).mean())
        lin = np.clip(lin * (target / before), 0, 1)
        rough = np.clip(rfloor + (1.0 - rfloor) * rgh, 0, 1)
        # cavity: how far the normal map bends away from flat, blurred (crevices darken the ambient term)
        bend = 1.0 - nrm[..., 2]
        ao = np.clip(1.0 - 2.2 * blur(bend, 3), 0.35, 1.0)
        alb, nl = mips_surface(lin, rough, nrm, ao)
        o.write(f"bark_{bid}_alb_rgh.r1tex", alb, base + "Color.jpg + Roughness.jpg")
        o.write(f"bark_{bid}_nrm_ao.r1tex", nl, base + "NormalGL.jpg (cavity derived)")
        o.preview(f"bark_{bid}.png", alb[2])
        o.calib[f"bark_{bid}"] = {"source": aset, "mean_lum_upstream": round(before, 5), "target": target,
                                  "size": list(col.shape[:2]), "rough_mean": round(float(rough.mean()), 4)}
        print(f"bark {bid}: {aset} mean {before:.4f} -> {target}")


# ---------------------------------------------------------------------------------------------------- leaves

# id, card, target mean linear luminance of the opaque texels, recolour (None or (hue, sat multiplier, value
# multiplier)), translucency strength
LEAVES = [
    ("oak", "oak", 0.075, None),
    ("ash", "ash", 0.080, None),
    ("aspen", "aspen", 0.085, (0.235, 0.78)),     # autumn yellow -> summer green
    ("pine", "pine", 0.055, None),
    ("scrub", "ash", 0.062, (0.17, 0.42)),       # dusty olive maquis foliage (arid)
    ("juniper", "pine", 0.050, (0.30, 0.35)),    # blue-grey-green scale foliage (arid)
]


def bake_leaves(raw, o):
    for lid, card, target, recolour in LEAVES:
        src = f"{EZ}/leaves/{card}.png"
        rgba = load_rgba(os.path.join(raw, src))
        alpha = rgba[..., 3]
        rgb = bleed(rgba[..., :3], alpha)
        if recolour is not None:
            hue, smul = recolour
            hsv = rgb_to_hsv(rgb)
            # keep the card's own hue variation around the new centre (scaled down), multiply saturation
            centre = float(np.median(hsv[alpha > 0.5][:, 0]))
            dh = ((hsv[..., 0] - centre + 0.5) % 1.0) - 0.5
            hsv[..., 0] = (hue + 0.5 * dh) % 1.0
            hsv[..., 1] = np.clip(hsv[..., 1] * smul, 0, 1)
            rgb = hsv_to_rgb(hsv)
        lin = srgb_to_linear(rgb)
        m = alpha > 0.5
        before = float(lum(lin)[m].mean())
        lin = np.clip(lin * (target / before), 0, 1)
        # translucency: thin, light blade transmits more than veins and stems (normalised luminance of the card)
        l = lum(lin)
        lo, hi = np.percentile(l[m], 5), np.percentile(l[m], 95)
        tr = np.clip(0.35 + 0.65 * (l - lo) / max(hi - lo, 1e-6), 0, 1)
        # mild relief from luminance (veins slightly raised), not wrapped (cards are not tiles)
        h = blur(l / max(hi, 1e-6), 1)
        nrm = normals_from_height(h, 2.5, wrap=False)
        extra = np.stack([tr, np.ones_like(tr)], axis=-1)
        alb, nl = mips_card(lin, alpha, nrm, extra)
        o.write(f"leaf_{lid}_alb.r1tex", alb, src + (" (recoloured)" if recolour else ""))
        o.write(f"leaf_{lid}_nrm_tr.r1tex", nl, src + " (normal and translucency derived)")
        o.preview(f"leaf_{lid}.png", alb[2])
        o.calib[f"leaf_{lid}"] = {"card": card, "mean_lum_upstream": round(before, 5), "target": target,
                                  "coverage": round(coverage(alpha, 0.5), 4), "recolour": recolour,
                                  "mean_linear_rgb": [round(float(v), 4) for v in lin[m].mean(axis=0)]}
        print(f"leaf {lid}: {card} mean {before:.4f} -> {target} coverage {coverage(alpha, 0.5):.3f}")



# ---------------------------------------------------------------------------------------------------- ground cover
# A 4x4 atlas of 512 px cards drawn in code (no external input). Each card is drawn at 2x and downsampled. Channels:
# albedo (linear, then sRGB) + coverage; a height field from the strokes gives the normal; translucency per stroke.
# Card coordinates: x right, y down (row 0 is the top of the card, the base of the plant is at the bottom edge).

COVER_CELLS = [
    "grass_a", "grass_b", "grass_c", "grass_d",
    "dry_a", "dry_b", "sedge", "fern_a",
    "fern_b", "daisy", "buttercup", "knapweed",
    "poppy", "dock", "thistle", "cushion",
]


class Card:
    """Supersampled drawing surface: colour (linear), coverage, height and translucency layers."""

    def __init__(self, size, rng):
        self.n = size
        self.col = Image.new("RGB", (size, size), (0, 0, 0))
        self.cov = Image.new("L", (size, size), 0)
        self.hgt = Image.new("L", (size, size), 0)
        self.trn = Image.new("L", (size, size), 0)
        self.dc, self.da, self.dh, self.dt = (ImageDraw.Draw(x) for x in (self.col, self.cov, self.hgt, self.trn))
        self.rng = rng

    def poly(self, pts, rgb, height, trans):
        """rgb in linear 0..1; drawn as sRGB 8-bit into the colour layer (converted back at the end)."""
        c = tuple(int(round(255 * v)) for v in linear_to_srgb(np.array(rgb)))
        self.dc.polygon(pts, fill=c)
        self.da.polygon(pts, fill=255)
        self.dh.polygon(pts, fill=int(np.clip(height, 0, 1) * 255))
        self.dt.polygon(pts, fill=int(np.clip(trans, 0, 1) * 255))

    def line(self, pts, width, rgb, height, trans):
        c = tuple(int(round(255 * v)) for v in linear_to_srgb(np.array(rgb)))
        w = max(1, int(round(width)))
        self.dc.line(pts, fill=c, width=w, joint="curve")
        self.da.line(pts, fill=255, width=w, joint="curve")
        self.dh.line(pts, fill=int(np.clip(height, 0, 1) * 255), width=w, joint="curve")
        self.dt.line(pts, fill=int(np.clip(trans, 0, 1) * 255), width=w, joint="curve")

    def ellipse(self, box, rgb, height, trans):
        c = tuple(int(round(255 * v)) for v in linear_to_srgb(np.array(rgb)))
        self.dc.ellipse(box, fill=c)
        self.da.ellipse(box, fill=255)
        self.dh.ellipse(box, fill=int(np.clip(height, 0, 1) * 255))
        self.dt.ellipse(box, fill=int(np.clip(trans, 0, 1) * 255))


def jitter_col(rng, rgb, amount=0.12):
    f = 1.0 + (rng.random(3) - 0.5) * 2 * amount
    v = 1.0 + (rng.random() - 0.5) * 2 * amount
    return np.clip(np.array(rgb) * f * v, 0, 1)


def blade(card, base, length, lean, curve, width, rgb, tip_rgb, trans=0.7, steps=10):
    """A tapered, curved grass blade from base (x, y) upwards: a polygon of left/right edges."""
    x0, y0 = base
    left, right = [], []
    ang0 = -math.pi / 2 + lean
    for i in range(steps + 1):
        t = i / steps
        ang = ang0 + curve * t * t
        # centre line by integrating the direction
        if i == 0:
            cx, cy = x0, y0
        else:
            cx += math.cos(ang) * length / steps
            cy += math.sin(ang) * length / steps
        w = width * (1.0 - t) ** 0.8 * 0.5
        nx, ny = -math.sin(ang), math.cos(ang)
        left.append((cx + nx * w, cy + ny * w))
        right.append((cx - nx * w, cy - ny * w))
    pts = left + right[::-1]
    # base darker, tip colour towards the top: draw in three segments of colour
    n = len(left)
    for k, (a, b) in enumerate(((0, n // 3 + 1), (n // 3, 2 * n // 3 + 1), (2 * n // 3, n))):
        t = (k + 0.5) / 3
        c = np.array(rgb) * (1 - t) + np.array(tip_rgb) * t
        seg = left[a:b] + right[a:b][::-1]
        if len(seg) >= 3:
            card.poly(seg, c * (0.72 + 0.28 * t), 0.35 + 0.65 * t, trans * (0.5 + 0.5 * t))
    return pts


def draw_grass(card, kind):
    rng = card.rng
    n = card.n
    params = {
        "grass_a": (150, 0.62, (0.055, 0.095, 0.024), (0.110, 0.150, 0.042), 9, 0.30),
        "grass_b": (120, 0.85, (0.058, 0.092, 0.026), (0.120, 0.150, 0.048), 8, 0.35),
        "grass_c": (170, 0.42, (0.052, 0.092, 0.022), (0.100, 0.145, 0.040), 7, 0.25),
        "grass_d": (90, 0.70, (0.050, 0.100, 0.025), (0.095, 0.160, 0.045), 14, 0.30),
        "dry_a": (130, 0.70, (0.12, 0.10, 0.05), (0.26, 0.22, 0.12), 8, 0.35),
        "dry_b": (70, 0.80, (0.13, 0.11, 0.06), (0.30, 0.25, 0.14), 7, 0.45),
        "sedge": (80, 0.95, (0.025, 0.055, 0.020), (0.050, 0.085, 0.030), 9, 0.15),
    }[kind]
    count, hfrac, base_rgb, tip_rgb, width, spread = params
    for i in range(count):
        bx = n * (0.5 + (rng.random() - 0.5) * spread)
        by = n * (0.995 - rng.random() * 0.02)
        length = n * hfrac * (0.45 + 0.55 * rng.random())
        lean = (bx / n - 0.5) * 1.6 + (rng.random() - 0.5) * 0.5
        curve = (rng.random() - 0.5) * 1.2 + math.copysign(0.25, lean)
        w = width * (0.6 + 0.8 * rng.random()) * n / 512
        b = jitter_col(rng, base_rgb, 0.18)
        t = jitter_col(rng, tip_rgb, 0.22)
        if kind.startswith("grass") and rng.random() < 0.08:
            t = np.array(tip_rgb) * np.array([2.0, 1.6, 1.2])   # a few yellowed blades
        blade(card, (bx, by), length, lean, curve, w, b, t, 0.75)
    # seed heads on the taller tufts
    if kind in ("grass_b", "dry_b"):
        for i in range(10 if kind == "grass_b" else 14):
            bx = n * (0.5 + (rng.random() - 0.5) * 0.35)
            length = n * hfrac * (0.75 + 0.25 * rng.random())
            lean = (rng.random() - 0.5) * 0.6
            pts = blade(card, (bx, n * 0.995), length, lean, (rng.random() - 0.5) * 0.4, 2.5 * n / 512,
                        (0.06, 0.07, 0.03), (0.10, 0.09, 0.05), 0.5, 8)
            tip = pts[len(pts) // 2 - 1]
            head = jitter_col(rng, (0.16, 0.14, 0.07) if kind == "grass_b" else (0.32, 0.27, 0.15), 0.1)
            for k in range(9):
                ox = (rng.random() - 0.5) * 10 * n / 512
                oy = k * 6 * n / 512
                card.ellipse((tip[0] + ox - 4 * n / 512, tip[1] + oy - 7 * n / 512, tip[0] + ox + 4 * n / 512, tip[1] + oy + 7 * n / 512), head, 0.95, 0.45)


def leaflet(card, x, y, ang, length, width, rgb, height, trans):
    """A teardrop leaflet from (x, y) along ang: widest at a third, pointed tip."""
    ca, sa = math.cos(ang), math.sin(ang)
    pts = []
    for k in range(9):
        t = k / 8
        w = width * math.sin(math.pi * min(1.0, t * 0.85 + 0.08)) * (1.0 - 0.5 * t)
        pts.append((x + ca * length * t - sa * w, y + sa * length * t + ca * w))
    for k in range(8, -1, -1):
        t = k / 8
        w = width * math.sin(math.pi * min(1.0, t * 0.85 + 0.08)) * (1.0 - 0.5 * t)
        pts.append((x + ca * length * t + sa * w, y + sa * length * t - ca * w))
    card.poly(pts, rgb, height, trans)


def draw_fern(card, kind):
    """One arching frond: a curved rachis with alternating pinnae; each pinna is a thin stalk lined with pinnules
    (small teardrop leaflets angled towards the pinna tip, shrinking towards it)."""
    rng = card.rng
    n = card.n
    s = n / 512
    pinnae = 16 if kind == "fern_a" else 20
    base_rgb = np.array((0.030, 0.082, 0.022) if kind == "fern_a" else (0.034, 0.088, 0.030))
    pts = []
    for i in range(41):
        t = i / 40
        pts.append((n * (0.50 + 0.09 * math.sin(t * 2.2) - 0.03), n * (0.985 - 0.93 * t)))
    for k in range(pinnae):
        t = 0.12 + 0.85 * k / pinnae
        i = int(t * 40)
        px, py = pts[i]
        side = 1 if k % 2 == 0 else -1
        span = (0.34 if kind == "fern_a" else 0.26) * math.sin(math.pi * min(1.0, 0.18 + t * 0.95)) ** 0.8 * (0.9 + 0.2 * rng.random())
        length = n * span
        ang = (-math.pi / 2 + side * (1.05 - 0.35 * t)) + (rng.random() - 0.5) * 0.1
        ca, sa = math.cos(ang), math.sin(ang)
        c = jitter_col(rng, base_rgb, 0.12) * (0.75 + 0.45 * t)
        card.line([(px, py), (px + ca * length, py + sa * length)], 1.8 * s, c * 0.8, 0.6, 0.4)
        pin = 11 if kind == "fern_a" else 8
        for j in range(pin):
            u = (j + 0.6) / (pin + 0.6)
            qx, qy = px + ca * length * u, py + sa * length * u
            pl = length / pin * (1.25 if kind == "fern_a" else 1.6) * (1.0 - 0.65 * u)
            pw = pl * (0.36 if kind == "fern_a" else 0.30)
            for sd in (-1, 1):
                a2 = ang + sd * (1.0 - 0.25 * u)
                leaflet(card, qx, qy, a2, pl, pw, c * (0.9 + 0.2 * rng.random()), 0.65 + 0.35 * u, 0.7)
    card.line(pts, 3.5 * s, (0.05, 0.07, 0.03), 0.6, 0.3)


def flower_stems(card, count, height, rgb):
    rng = card.rng
    n = card.n
    tops = []
    for i in range(count):
        bx = n * (0.5 + (rng.random() - 0.5) * 0.5)
        length = n * height * (0.55 + 0.45 * rng.random())
        lean = (bx / n - 0.5) * 0.9 + (rng.random() - 0.5) * 0.3
        pts = blade(card, (bx, n * 0.995), length, lean, (rng.random() - 0.5) * 0.4, 3.5 * n / 512, rgb, np.array(rgb) * 1.3, 0.5, 10)
        tops.append(pts[len(pts) // 2 - 1])
    return tops


def draw_flowers(card, kind):
    rng = card.rng
    n = card.n
    s = n / 512
    # a little grass and leaves at the base so the stems do not stand bare
    for i in range(45):
        bx = n * (0.5 + (rng.random() - 0.5) * 0.4)
        blade(card, (bx, n * 0.995), n * 0.25 * (0.4 + 0.6 * rng.random()), (bx / n - 0.5) * 1.8, (rng.random() - 0.5), 7 * s,
              jitter_col(rng, (0.052, 0.092, 0.022)), jitter_col(rng, (0.10, 0.145, 0.040)), 0.7)
    if kind == "daisy":
        for (x, y) in flower_stems(card, 9, 0.62, (0.05, 0.09, 0.03)):
            petals = 16
            r = 15 * s * (0.8 + 0.4 * rng.random())
            tilt = 0.55 + 0.3 * rng.random()   # seen from the side: flattened
            for k in range(petals):
                a = 2 * math.pi * k / petals + rng.random() * 0.2
                ex, ey = math.cos(a) * r, math.sin(a) * r * tilt
                card.poly([(x, y), (x + ex * 0.45 - ey * 0.18, y + ey * 0.45 + ex * 0.06 * tilt), (x + ex, y + ey), (x + ex * 0.45 + ey * 0.18, y + ey * 0.45 - ex * 0.06 * tilt)],
                          jitter_col(rng, (0.78, 0.78, 0.74), 0.04), 0.95, 0.6)
            card.ellipse((x - 5 * s, y - 4 * s * tilt, x + 5 * s, y + 4 * s * tilt), (0.70, 0.45, 0.02), 1.0, 0.3)
    elif kind == "buttercup":
        for (x, y) in flower_stems(card, 11, 0.58, (0.05, 0.10, 0.03)):
            r = 10 * s * (0.8 + 0.4 * rng.random())
            for k in range(5):
                a = 2 * math.pi * k / 5 + rng.random() * 0.3
                cx, cy = x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.4
                card.ellipse((cx - r * 0.55, cy - r * 0.5, cx + r * 0.55, cy + r * 0.5), jitter_col(rng, (0.80, 0.55, 0.01), 0.05), 0.95, 0.55)
            card.ellipse((x - 3 * s, y - 3 * s, x + 3 * s, y + 3 * s), (0.45, 0.30, 0.01), 1.0, 0.3)
    elif kind == "knapweed":
        for (x, y) in flower_stems(card, 8, 0.66, (0.045, 0.075, 0.03)):
            card.ellipse((x - 8 * s, y - 2 * s, x + 8 * s, y + 13 * s), (0.10, 0.09, 0.05), 0.85, 0.3)   # bract
            for k in range(26):
                a = -math.pi * rng.random()
                l = (9 + 7 * rng.random()) * s
                card.line([(x, y + 2 * s), (x + math.cos(a) * l, y + math.sin(a) * l)], 2.4 * s, jitter_col(rng, (0.30, 0.06, 0.38), 0.12), 1.0, 0.6)
    elif kind == "poppy":
        for (x, y) in flower_stems(card, 6, 0.70, (0.05, 0.09, 0.03)):
            r = 17 * s * (0.8 + 0.3 * rng.random())
            for k in range(4):
                a = 2 * math.pi * k / 4 + 0.4 + rng.random() * 0.3
                cx, cy = x + math.cos(a) * r * 0.45, y + math.sin(a) * r * 0.3 - r * 0.15
                card.ellipse((cx - r * 0.62, cy - r * 0.55, cx + r * 0.62, cy + r * 0.5), jitter_col(rng, (0.65, 0.025, 0.012), 0.08), 0.95, 0.65)
            card.ellipse((x - 4 * s, y - 6 * s, x + 4 * s, y + 1 * s), (0.02, 0.025, 0.015), 1.0, 0.2)


def draw_weed(card, kind):
    rng = card.rng
    n = card.n
    s = n / 512
    if kind == "dock":
        # broad lance-shaped leaves with a midrib, arching from the base
        for i in range(9):
            ang = -math.pi / 2 + (i - 4) * 0.28 + (rng.random() - 0.5) * 0.2
            length = n * (0.42 + 0.25 * rng.random())
            width = n * 0.075 * (0.8 + 0.4 * rng.random())
            bx, by = n * 0.5, n * 0.99
            left, right, mid = [], [], []
            for k in range(13):
                t = k / 12
                a = ang + 0.5 * t * t * math.copysign(1, ang + math.pi / 2)
                if k == 0:
                    cx, cy = bx, by
                else:
                    cx += math.cos(a) * length / 12
                    cy += math.sin(a) * length / 12
                w = width * math.sin(math.pi * min(1.0, 0.15 + t)) * (1 - 0.4 * t)
                nx, ny = -math.sin(a), math.cos(a)
                left.append((cx + nx * w, cy + ny * w)); right.append((cx - nx * w, cy - ny * w)); mid.append((cx, cy))
            c = jitter_col(rng, (0.035, 0.085, 0.025), 0.15)
            card.poly(left + right[::-1], c, 0.8, 0.55)
            card.line(mid, 2.5 * s, c * 1.6, 0.95, 0.4)
        # a seed spike of rusty brown
        pts = blade(card, (n * 0.52, n * 0.99), n * 0.85, 0.05, 0.1, 5 * s, (0.08, 0.07, 0.03), (0.10, 0.05, 0.02), 0.3, 10)
        top = pts[len(pts) // 2 - 1]
        for k in range(40):
            yy = top[1] + k * 6 * s
            card.ellipse((top[0] - 7 * s + (rng.random() - 0.5) * 6 * s, yy - 3 * s, top[0] + 7 * s, yy + 3 * s), jitter_col(rng, (0.17, 0.07, 0.025), 0.15), 0.9, 0.3)
    elif kind == "thistle":
        for i in range(7):
            ang = -math.pi / 2 + (i - 3) * 0.45
            bx, by = n * 0.5, n * 0.99
            length = n * 0.35 * (0.7 + 0.5 * rng.random())
            pts = [(bx, by)]
            for k in range(1, 9):
                t = k / 8
                r = length * t
                spike = (1 if k % 2 else -1) * length * 0.07 * (1 - t)
                a = ang + 0.3 * t
                pts.append((bx + math.cos(a) * r - math.sin(a) * spike, by + math.sin(a) * r + math.cos(a) * spike))
            pts.append((bx + 3, by))
            card.poly(pts, jitter_col(rng, (0.05, 0.08, 0.04), 0.12), 0.75, 0.45)
        for (x, y) in flower_stems(card, 4, 0.75, (0.06, 0.08, 0.04)):
            card.ellipse((x - 9 * s, y - 2 * s, x + 9 * s, y + 16 * s), (0.08, 0.10, 0.05), 0.9, 0.3)
            for k in range(24):
                a = -math.pi * (0.15 + 0.7 * rng.random())
                l = (8 + 8 * rng.random()) * s
                card.line([(x, y), (x + math.cos(a) * l, y + math.sin(a) * l)], 2.4 * s, jitter_col(rng, (0.28, 0.05, 0.30), 0.12), 1.0, 0.6)
    elif kind == "cushion":
        # a low grey-green shrublet (thyme, sage): wiry stems carrying small paired leaves, dome shaped, a few flowers
        for i in range(70):
            a = -math.pi * (0.08 + 0.84 * rng.random())
            r = n * 0.40 * (0.55 + 0.45 * rng.random())
            bx = n * 0.5 + (rng.random() - 0.5) * n * 0.2
            ex, ey = bx + math.cos(a) * r * 1.15, n * 0.99 + math.sin(a) * r * 0.75
            c = jitter_col(rng, (0.068, 0.082, 0.055), 0.15)
            card.line([(bx, n * 0.99), ((bx + ex) / 2, (n * 0.99 + ey) / 2 - 6 * s), (ex, ey)], 2.0 * s, (0.08, 0.06, 0.04), 0.5, 0.3)
            for k in range(9):
                t = 0.3 + 0.7 * k / 9
                x = bx + (ex - bx) * t
                y = n * 0.99 + (ey - n * 0.99) * t - 6 * s * math.sin(math.pi * t)
                for sd in (-1, 1):
                    leaflet(card, x, y, a + sd * 0.9, 9 * s, 3.2 * s, c * (0.9 + 0.2 * rng.random()), 0.5 + 0.5 * t, 0.4)
        for i in range(26):
            a = -math.pi * (0.15 + 0.7 * rng.random())
            r = n * 0.38 * (0.8 + 0.25 * rng.random())
            x, y = n * 0.5 + math.cos(a) * r * 1.1, n * 0.99 + math.sin(a) * r * 0.75
            card.ellipse((x - 3.5 * s, y - 3.5 * s, x + 3.5 * s, y + 3.5 * s), jitter_col(rng, (0.22, 0.13, 0.26), 0.12), 0.95, 0.4)


def bake_cover(raw, o):
    cell = 512
    ss = 2
    atlas_c = np.zeros((4 * cell, 4 * cell, 3))
    atlas_a = np.zeros((4 * cell, 4 * cell))
    atlas_n = np.zeros((4 * cell, 4 * cell, 3)); atlas_n[..., 2] = 1
    atlas_t = np.zeros((4 * cell, 4 * cell))
    for idx, name in enumerate(COVER_CELLS):
        rng = np.random.default_rng(1000 + idx)
        card = Card(cell * ss, rng)
        if name.startswith(("grass", "dry", "sedge")):
            draw_grass(card, name)
        elif name.startswith("fern"):
            draw_fern(card, name)
        elif name in ("daisy", "buttercup", "knapweed", "poppy"):
            draw_flowers(card, name)
        else:
            draw_weed(card, name)
        col = srgb_to_linear(np.asarray(card.col).astype(np.float64) / 255.0)
        cov = np.asarray(card.cov).astype(np.float64) / 255.0
        hgt = np.asarray(card.hgt).astype(np.float64) / 255.0
        trn = np.asarray(card.trn).astype(np.float64) / 255.0
        # downsample 2x (premultiplied colour)
        def d2(a):
            return 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])
        a2 = d2(cov)
        c2 = d2(col * cov[..., None]) / np.maximum(a2[..., None], 1e-6)
        h2 = d2(hgt * cov) / np.maximum(a2, 1e-6)
        t2 = d2(trn * cov) / np.maximum(a2, 1e-6)
        # strokes are rounded across their width: height = coverage-blurred stroke height
        hb = blur(h2 * a2, 2) + 0.15 * a2
        nrm = normals_from_height(hb, 6.0, wrap=False)
        y0, x0 = (idx // 4) * cell, (idx % 4) * cell
        atlas_c[y0:y0 + cell, x0:x0 + cell] = bleed(c2, a2, 24)
        atlas_a[y0:y0 + cell, x0:x0 + cell] = a2
        atlas_n[y0:y0 + cell, x0:x0 + cell] = nrm
        atlas_t[y0:y0 + cell, x0:x0 + cell] = t2
        print(f"cover {name}: coverage {coverage(a2, 0.5):.3f}")
    extra = np.stack([atlas_t, np.ones_like(atlas_t)], axis=-1)
    alb, nl = mips_card(atlas_c, atlas_a, atlas_n, extra)
    o.write("cover_alb.r1tex", alb[:10], "drawn in code (bake-env.py cover)")
    o.write("cover_nrm_tr.r1tex", nl[:10], "drawn in code (bake-env.py cover)")
    o.preview("cover.png", alb[1])
    o.calib["cover"] = {"cells": COVER_CELLS, "cell_px": cell, "supersample": ss}


# ---------------------------------------------------------------------------------------------------- water
# A tileable wave normal map: a height field summed from directional waves whose wave vectors are whole numbers of
# cycles per tile (so it tiles exactly), with a steepened (trochoid-like) profile and a k^-1.6 amplitude spectrum,
# plus fine capillary noise. Stored as OpenGL normal xy + height + 1.


def bake_water(raw, o):
    n = 512
    rng = np.random.default_rng(4242)
    y, x = np.mgrid[0:n, 0:n] / n
    h = np.zeros((n, n))
    for i in range(48):
        kmag = 2 + rng.integers(0, 22)
        ang = rng.random() * 2 * math.pi
        kx, ky = int(round(math.cos(ang) * kmag)), int(round(math.sin(ang) * kmag))
        if kx == 0 and ky == 0:
            continue
        k = math.hypot(kx, ky)
        amp = k ** -1.6
        ph = rng.random() * 2 * math.pi
        arg = 2 * math.pi * (kx * x + ky * y) + ph
        # steepened crest: sharper peaks, flatter troughs
        h += amp * (np.sin(arg) + 0.35 * np.sin(2 * arg + 0.5))
    h += 0.02 * blur(rng.random((n, n)), 1)
    h = (h - h.min()) / (h.max() - h.min())
    nrm = normals_from_height(h, 9.0, wrap=True)
    rough = np.full((n, n), 0.06)
    alb, nl = mips_surface(np.zeros((n, n, 3)) + 0.02, rough, nrm, h)
    o.write("water_nrm.r1tex", nl, "drawn in code (bake-env.py water)")
    o.preview("water.png", nl[0])
    o.calib["water"] = {"size": n, "waves": 48}


# ---------------------------------------------------------------------------------------------------- structures
# Tileable building materials, drawn in code over photographic stone where stone is needed (Rock023 via Terrain3D,
# CC0). Every set is two RGBA8 textures: albedo (sRGB) + roughness, OpenGL normal xy + ambient occlusion + 1.
# World size of one repeat (m) is listed in calibration.json; the runtime maps UVs in metres divided by it.

def vnoise(n, cells, rng, octaves=4, persistence=0.5):
    """Tileable value noise in [0, 1] (bicubic-upsampled random lattices, summed octaves)."""
    out = np.zeros((n, n))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        c = cells * (2 ** o)
        if c > n:
            break
        lat = rng.random((c, c))
        big = np.tile(lat, (3, 3))
        im = Image.fromarray(big.astype(np.float32), mode="F").resize((3 * n, 3 * n), Image.BICUBIC)
        out += amp * np.asarray(im, dtype=np.float64)[n:2 * n, n:2 * n]
        total += amp
        amp *= persistence
    out /= total
    return np.clip((out - out.min()) / max(out.max() - out.min(), 1e-6), 0, 1)


def rock_photo(raw, n, rng, offset):
    """Rock023 albedo (linear) and height, rolled by an offset so each use is a different part of the photo."""
    a = load_rgba(os.path.join(raw, "terrain3d/project/demo/assets/textures/rock023_alb_ht.png"))
    if a.shape[0] != n:
        im = Image.fromarray((a * 255).astype(np.uint8), mode="RGBA").resize((n, n), Image.LANCZOS)
        a = np.asarray(im).astype(np.float64) / 255.0
    a = np.roll(np.roll(a, offset[0], axis=0), offset[1], axis=1)
    return srgb_to_linear(a[..., :3]), a[..., 3]


def finish(o, name, lin, rough, height, strength, ao_extra=None, world_m=2.0, note="drawn in code"):
    rough = np.array(np.broadcast_to(rough, lin.shape[:2]), dtype=np.float64)
    nrm = normals_from_height(height, strength, wrap=True)
    ao = np.clip(0.55 + 0.45 * blur(height, 3) / max(height.max(), 1e-6), 0, 1)
    if ao_extra is not None:
        ao = ao * ao_extra
    alb, nl = mips_surface(np.clip(lin, 0, 1), np.clip(rough, 0, 1), nrm, ao)
    o.write(f"st_{name}_alb_rgh.r1tex", alb, note)
    o.write(f"st_{name}_nrm_ao.r1tex", nl, note)
    o.preview(f"st_{name}.png", alb[1])
    o.calib[f"st_{name}"] = {"world_m": world_m, "mean_lum": round(float(lum(lin).mean()), 4)}
    print(f"struct {name}: mean albedo {float(lum(lin).mean()):.3f}")


def bake_struct(raw, o):
    rng = np.random.default_rng(777)
    n = 1024

    # ---- wood: hewn timber, grain along v (down the image), knots, weathered grey-brown
    y, x = np.mgrid[0:n, 0:n] / n
    warp = vnoise(n, 4, rng, 3) * 0.08
    grain = 0.5 + 0.5 * np.sin((x + warp + 0.02 * np.sin(y * 2 * math.pi * 3)) * 2 * math.pi * 26)
    fibre = vnoise(n, 64, rng, 2)
    tone = vnoise(n, 3, rng, 3)
    base = np.array([0.105, 0.075, 0.048])
    lin = base * (0.75 + 0.35 * grain[..., None] * 0.6 + 0.25 * tone[..., None]) * (0.92 + 0.16 * fibre[..., None])
    lin = lin * (1 - 0.18 * (vnoise(n, 8, rng, 2) > 0.7)[..., None])
    height = 0.6 * grain + 0.3 * fibre + 0.1 * tone
    finish(o, "wood", lin, 0.78 + 0.15 * fibre, height, 2.5, world_m=1.0)

    # ---- planks: boards 22-30 cm wide with dark seams, each board its own tone and grain phase
    bounds = [0]
    while bounds[-1] < n:
        bounds.append(bounds[-1] + int(n * (0.11 + 0.04 * rng.random())))
    bounds[-1] = n
    lin = np.zeros((n, n, 3)); height = np.zeros((n, n)); rough = np.zeros((n, n))
    for b0, b1 in zip(bounds[:-1], bounds[1:]):
        w = b1 - b0
        xx = (np.arange(w)[None, :] / w)
        ph = rng.random() * 10
        g = 0.5 + 0.5 * np.sin((xx * 3 + ph + 0.04 * np.sin(y[:, b0:b1] * 2 * math.pi * 2 + ph)) * 2 * math.pi * 3)
        t = 0.8 + 0.4 * rng.random()
        lin[:, b0:b1] = np.array([0.11, 0.078, 0.05])[None, None, :] * t * (0.85 + 0.25 * g[..., None]) * (0.9 + 0.2 * fibre[:, b0:b1, None])
        edge = np.minimum(xx, 1 - xx) * w
        seam = np.clip(edge / 3.0, 0, 1)
        lin[:, b0:b1] *= (0.35 + 0.65 * seam)[..., None]
        height[:, b0:b1] = seam * (0.6 + 0.3 * g + 0.1 * fibre[:, b0:b1])
        rough[:, b0:b1] = 0.8
    finish(o, "planks", lin, rough, height, 4.0, world_m=2.0)

    # ---- daub: clay plaster over wattle, hand-smoothed, with cracks and straw flecks
    clay = vnoise(n, 6, rng, 5)
    lin = np.array([0.30, 0.24, 0.16]) * (0.78 + 0.35 * clay[..., None])
    cracks = vnoise(n, 24, rng, 2)
    crack = np.clip(1 - np.abs(cracks - 0.5) * 40, 0, 1) * (vnoise(n, 5, rng, 2) > 0.55)
    lin *= (1 - 0.5 * crack)[..., None]
    for i in range(1500):
        cx, cy = rng.integers(0, n, 2)
        L = rng.integers(4, 14); ang = rng.random() * math.pi
        for t in range(L):
            px = int(cx + math.cos(ang) * t) % n; py = int(cy + math.sin(ang) * t) % n
            lin[py, px] = lin[py, px] * 0.6 + np.array([0.42, 0.34, 0.18]) * 0.4
    dirt = np.clip(1 - y * 1.6, 0, 1) ** 0  # no vertical gradient in a tileable set
    height = clay * 0.7 - crack * 0.6 + 0.2 * vnoise(n, 48, rng, 2)
    finish(o, "daub", lin * dirt[..., None], 0.88 + 0.08 * clay, height, 3.0, world_m=2.5)

    # ---- thatch: rows of straw courses, strands running down the slope (down the image = down the roof)
    hb = np.zeros((n, n)); col = np.zeros((n, n, 3)) + np.array([0.13, 0.11, 0.07])
    img = Image.new("RGB", (n, n), (0, 0, 0)); d = ImageDraw.Draw(img)
    himg = Image.new("L", (n, n), 0); dh = ImageDraw.Draw(himg)
    rows = 8
    for r in range(rows + 1):
        y0 = int((r - 0.15) * n / rows)
        for i in range(1400):
            x0 = rng.random() * n
            L = n / rows * (1.15 + 0.35 * rng.random())
            ang = math.pi / 2 + (rng.random() - 0.5) * 0.25
            x1 = x0 + math.cos(ang) * L; y1 = y0 + math.sin(ang) * L
            shade = 0.55 + 0.45 * rng.random()
            c = np.array([0.26, 0.21, 0.12]) * shade * (0.85 + 0.3 * rng.random())
            if rng.random() < 0.25:
                c = c * np.array([0.75, 0.78, 0.8])   # weathered grey strands
            cs = tuple(int(255 * v) for v in linear_to_srgb(c))
            for wrapx in (-n, 0, n):
                for wrapy in (-n, 0, n):
                    d.line([(x0 + wrapx, y0 + wrapy), (x1 + wrapx, y1 + wrapy)], fill=cs, width=2)
                    dh.line([(x0 + wrapx, y0 + wrapy), (x1 + wrapx, y1 + wrapy)], fill=int(80 + 175 * shade), width=2)
    lin = srgb_to_linear(np.asarray(img).astype(np.float64) / 255.0)
    height = np.asarray(himg).astype(np.float64) / 255.0
    # course shadow: each row's lower edge overlaps the next row
    row_t = (y * rows) % 1.0
    course = 0.55 + 0.45 * np.clip(row_t * 1.4, 0, 1)
    lin = lin * course[..., None] + 0.002
    height = height * 0.6 + course * 0.4
    finish(o, "thatch", lin, 0.95 + 0 * height, height, 5.0, world_m=2.0)

    # ---- ashlar: limestone courses of 0.5 m with random block lengths, drafted margins, thin mortar joints;
    # each block takes its own part of the Rock023 photograph and its own tone (no repeating marks within 6 m)
    N = 2048
    world = 6.0
    courses = 12
    rock_lin, rock_h = rock_photo(raw, N, rng, (0, 0))
    lin = np.zeros((N, N, 3)); height = np.zeros((N, N)); ao = np.ones((N, N))
    ch = N // courses
    yy, xx = np.mgrid[0:ch, 0:N]
    for c in range(courses):
        y0 = c * ch
        xpos = [0]
        while xpos[-1] < N:
            xpos.append(xpos[-1] + int(N / world * (0.7 + 0.9 * rng.random())))
        xpos[-1] = N
        # tileable seam: shift the course by a random offset (wraps)
        shift = int(rng.random() * N)
        for b0, b1 in zip(xpos[:-1], xpos[1:]):
            w = b1 - b0
            sy, sx = rng.integers(0, N, 2)
            patch = np.roll(np.roll(rock_lin, -sy, axis=0), -sx, axis=1)[:ch, :w]
            ph = np.roll(np.roll(rock_h, -sy, axis=0), -sx, axis=1)[:ch, :w]
            # limestone: desaturate the photo towards a warm pale stone, block tone varies
            g = lum(patch)[..., None]
            stone = np.array([0.40, 0.36, 0.29]) * (0.8 + 0.35 * rng.random())
            blk = stone * (0.55 + 0.9 * g / max(float(g.mean()), 1e-4)) * 0.5 + stone * 0.5
            ex = np.minimum(np.arange(w)[None, :], w - 1 - np.arange(w)[None, :]).astype(float)
            ey = np.minimum(np.arange(ch)[:, None], ch - 1 - np.arange(ch)[:, None]).astype(float)
            e = np.minimum(ex, ey)
            margin = np.clip(e / 14.0, 0, 1)                   # drafted margin band
            joint = np.clip(e / 3.0, 0, 1)                     # mortar joint
            chip = vnoise(64, 8, rng, 2)
            chipmask = (e < 10) & (np.asarray(Image.fromarray(chip.astype(np.float32), mode="F").resize((w, ch))) > 0.72)
            hblk = 0.55 + 0.25 * ph + 0.2 * margin
            hblk = np.where(chipmask, hblk * 0.6, hblk)
            col = blk * (0.75 + 0.25 * margin[..., None])
            col = np.where(joint[..., None] < 1, np.array([0.30, 0.28, 0.24]) * (0.6 + 0.4 * joint[..., None]), col)
            idx = (np.arange(b0, b1) + shift) % N
            lin[y0:y0 + ch][:, idx] = col
            height[y0:y0 + ch][:, idx] = hblk * joint
            ao[y0:y0 + ch][:, idx] = 0.6 + 0.4 * joint
    finish(o, "ashlar", lin, 0.86 - 0.1 * height, height, 6.0, ao_extra=ao, world_m=world, note="drawn in code over Rock023 (CC0)")

    # ---- rubble: irregular fieldstones in mortar (Voronoi cells), for footings and the Iron Age gate
    n2 = 1024
    pts = rng.random((260, 2)) * n2
    gy, gx = np.mgrid[0:n2, 0:n2]
    best = np.full((n2, n2), 1e9); second = np.full((n2, n2), 1e9); owner = np.zeros((n2, n2), dtype=int)
    for i, (px, py) in enumerate(pts):
        for ox in (-n2, 0, n2):
            for oy in (-n2, 0, n2):
                dd = np.hypot(gx - px - ox, (gy - py - oy) * 1.35)
                m = dd < best
                second = np.where(m, best, np.minimum(second, dd))
                best = np.where(m, dd, best)
                owner = np.where(m, i, owner)
    edge = second - best
    rock_lin, rock_h = rock_photo(raw, n2, rng, (311, 517))
    tones = 0.65 + 0.6 * rng.random(len(pts))
    stone = np.array([0.24, 0.22, 0.19])[None, None, :] * tones[owner][..., None] * (0.6 + 0.8 * lum(rock_lin)[..., None] / max(float(lum(rock_lin).mean()), 1e-4)) * 0.6
    mortar = np.clip(edge / 7.0, 0, 1)
    lin = stone * mortar[..., None] + np.array([0.20, 0.18, 0.15]) * (1 - mortar[..., None])
    height = mortar * (0.6 + 0.4 * np.clip(edge / 40.0, 0, 1)) + 0.15 * rock_h
    finish(o, "rubble", lin, 0.9 - 0.05 * mortar, height, 7.0, world_m=2.0, note="drawn in code over Rock023 (CC0)")

    # ---- plaster: lime render, off-white with soft mottling and faint stains; tinted per building at run time
    m1 = vnoise(n, 5, rng, 5); m2 = vnoise(n, 30, rng, 3)
    lin = np.array([0.62, 0.60, 0.55]) * (0.86 + 0.14 * m1[..., None]) * (0.96 + 0.04 * m2[..., None])
    stain = np.clip((vnoise(n, 3, rng, 3) - 0.6) * 3, 0, 1)
    lin *= (1 - 0.18 * stain)[..., None]
    finish(o, "plaster", lin, 0.9 + 0.05 * m2, 0.6 * m1 + 0.4 * m2, 1.2, world_m=3.0)

    # ---- roof tiles: terracotta pan-and-cover (tegula and imbrex) in columns 0.25 m apart, courses 0.42 m deep;
    # runs down the image (= down the roof)
    world = 2.0
    cols, rows = 8, 5
    lin = np.zeros((n, n, 3)); height = np.zeros((n, n)); ao = np.ones((n, n))
    cw, rh = n // cols, n // rows
    yy, xx = np.mgrid[0:rh, 0:cw]
    u = xx / cw; v = yy / rh
    for r in range(rows):
        for c in range(cols):
            t = 0.75 + 0.45 * rng.random()
            hue = np.array([0.36, 0.13, 0.06]) * t * np.array([1.0, 0.9 + 0.25 * rng.random(), 0.9 + 0.3 * rng.random()])
            # cover tile (imbrex) along the column edge: a half cylinder; pan (tegula) flat with raised flanges
            imb = np.clip(1 - np.abs(u - 0.0) / 0.22, 0, 1) + np.clip(1 - np.abs(u - 1.0) / 0.22, 0, 1)
            cyl = np.sqrt(np.clip(1 - (1 - imb) ** 2, 0, 1)) * (imb > 0)
            lap = np.clip(v / 0.12, 0, 1)                 # the course above overlaps the top
            h = 0.25 + 0.75 * cyl
            h = h * (0.75 + 0.25 * lap)
            sh = 0.55 + 0.45 * lap
            col = hue * (0.8 + 0.3 * cyl[..., None]) * sh[..., None]
            lin[r * rh:(r + 1) * rh, c * cw:(c + 1) * cw] = col
            height[r * rh:(r + 1) * rh, c * cw:(c + 1) * cw] = h
            ao[r * rh:(r + 1) * rh, c * cw:(c + 1) * cw] = (0.5 + 0.5 * lap) * (0.7 + 0.3 * cyl)
    lin *= (0.85 + 0.3 * vnoise(n, 40, rng, 2))[..., None]
    lichen = np.clip((vnoise(n, 6, rng, 3) - 0.62) * 4, 0, 1)
    lin = lin * (1 - 0.35 * lichen[..., None]) + np.array([0.18, 0.17, 0.12]) * 0.35 * lichen[..., None]
    finish(o, "tiles", lin, 0.72 + 0.15 * lichen, height, 9.0, ao_extra=ao, world_m=world)

    # ---- paving: irregular rectangular limestone slabs
    lin = np.zeros((n, n, 3)); height = np.zeros((n, n))
    rock_lin, rock_h = rock_photo(raw, n, rng, (97, 640))
    yv = 0
    while yv < n:
        hgt = int(n * (0.12 + 0.08 * rng.random()))
        hgt = min(hgt, n - yv)
        xv = int(rng.random() * n * 0.2)
        while xv < n + int(n * 0.2):
            wd = int(n * (0.15 + 0.15 * rng.random()))
            x0, x1 = xv, xv + wd
            t = 0.7 + 0.4 * rng.random()
            ex = np.minimum(np.arange(wd)[None, :], wd - 1 - np.arange(wd)[None, :]).astype(float)
            ey = np.minimum(np.arange(hgt)[:, None], hgt - 1 - np.arange(hgt)[:, None]).astype(float)
            e = np.clip(np.minimum(ex, ey) / 4.0, 0, 1)
            idx = np.arange(x0, x1) % n
            g = lum(rock_lin[yv:yv + hgt][:, idx])
            lin[yv:yv + hgt][:, idx] = np.array([0.34, 0.31, 0.25]) * t * (0.7 + 0.6 * g[..., None] / max(float(g.mean()), 1e-4) * 0.5) * (0.4 + 0.6 * e[..., None])
            height[yv:yv + hgt][:, idx] = e * (0.7 + 0.3 * rock_h[yv:yv + hgt][:, idx])
            xv = x1
        yv += hgt
    finish(o, "paving", lin, 0.85, height, 5.0, world_m=4.0, note="drawn in code over Rock023 (CC0)")

# ---------------------------------------------------------------------------------------------------- main

SECTIONS = {"bark": bake_bark, "leaves": bake_leaves, "cover": bake_cover, "water": bake_water, "struct": bake_struct}


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    raw, out = sys.argv[1], sys.argv[2]
    which = sys.argv[3:] or list(SECTIONS.keys())
    os.makedirs(out, exist_ok=True)
    o = Out(out)
    for s in which:
        SECTIONS[s](raw, o)
    # merge with an existing manifest so partial runs keep the other sections' rows
    mpath = os.path.join(out, "MANIFEST.tsv")
    rows = {}
    if os.path.exists(mpath):
        for line in open(mpath):
            if line.startswith("#") or not line.strip():
                continue
            f = line.rstrip("\n").split("\t")
            rows[f[0]] = f
    for r in o.rows:
        rows[r[0]] = [str(x) for x in r]
    with open(mpath, "w") as f:
        f.write(f"# env bake; numpy {np.__version__}; Pillow {PIL.__version__}\n")
        f.write("# file\tsha256\tbytes\tsource\n")
        for k in sorted(rows):
            f.write("\t".join(rows[k]) + "\n")
    cpath = os.path.join(out, "calibration.json")
    calib = json.load(open(cpath)) if os.path.exists(cpath) else {}
    calib.update(o.calib)
    with open(cpath, "w") as f:
        json.dump(calib, f, indent=2, sort_keys=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
