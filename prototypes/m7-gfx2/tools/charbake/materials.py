"""Shared procedural material textures for garments, armour and weapons (generated in code, deterministic).

fabric.r1tx (512, tiling, linear data): R plain weave height (linen), G twill height (wool), B riveted mail rings
height, A fine grain noise (leather, felt, grime). wood.r1tx (512): R grain height, G albedo variation. Emblems
(shield and surcoat paint): sRGB colour with alpha = painted area, original geometric heraldry drawn in code.
"""
import numpy as np
from PIL import Image, ImageDraw

import texio
from skin import fbm, value_noise

S = 512


def fabric():
    y, x = np.mgrid[0:S, 0:S].astype(np.float64)
    # plain weave: warp/weft threads alternating over/under (16 threads per tile)
    n = 32.0
    u, v = x / S * n, y / S * n
    wu = np.abs(np.sin(np.pi * u)) ** 0.6
    wv = np.abs(np.sin(np.pi * v)) ** 0.6
    checker = (np.floor(u) + np.floor(v)) % 2
    weave = np.where(checker > 0, wu * (0.6 + 0.4 * wv), wv * (0.6 + 0.4 * wu))
    weave += 0.15 * (fbm(S, 32, 2, 41) - 0.5)
    # twill: diagonal ribs
    t = (x + y) / S * 48.0
    twill = 0.5 + 0.5 * np.sin(2 * np.pi * t) * (0.75 + 0.25 * np.sin(np.pi * x / S * 48.0))
    twill += 0.2 * (fbm(S, 32, 2, 42) - 0.5)
    # riveted mail: 4-in-1 rings on a staggered grid (16 x 16 rings per tile)
    m = 16.0
    gu, gv = x / S * m, y / S * m
    rows = np.floor(gv)
    gu2 = gu + 0.5 * (rows % 2)
    du, dv = gu2 - np.floor(gu2) - 0.5, gv - rows - 0.5
    r = np.sqrt((du * 1.0) ** 2 + (dv * 1.25) ** 2)
    ring = np.exp(-((r - 0.36) ** 2) / (2 * 0.075 ** 2))
    # overlapping neighbour rings (offset half a cell up) give the interlocking look
    dv2 = gv + 0.5 - np.floor(gv + 0.5) - 0.5
    rows2 = np.floor(gv + 0.5)
    gu3 = gu + 0.5 * (rows2 % 2) + 0.5
    du2 = gu3 - np.floor(gu3) - 0.5
    r2 = np.sqrt(du2 ** 2 + (dv2 * 1.25) ** 2)
    ring2 = np.exp(-((r2 - 0.36) ** 2) / (2 * 0.075 ** 2))
    mail = np.clip(np.maximum(ring, ring2 * 0.85), 0, 1)
    grain = fbm(S, 64, 4, 43)
    data = np.stack([norm01(weave), norm01(twill), mail, norm01(grain)], -1)
    return texio.mips_data(data)


def wood():
    y, x = np.mgrid[0:S, 0:S].astype(np.float64)
    warp = fbm(S, 4, 3, 51)
    rings = np.sin((x / S * 26.0 + warp * 6.0) * np.pi * 2) * 0.5 + 0.5
    fibre = fbm(S, 128, 2, 52)
    h = norm01(0.6 * rings + 0.4 * fibre)
    alb = norm01(0.7 * rings + 0.3 * fbm(S, 16, 3, 53))
    noise = fbm(S, 32, 4, 54)
    return texio.mips_data(np.stack([h, alb, noise, np.ones_like(h)], -1))


def norm01(a):
    a = a - a.min()
    return a / max(a.max(), 1e-9)


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def emblem(field, charge, kind, size=512, shape="heater"):
    """Painted heraldry, drawn at 4x and box-filtered: field colour, a charge in the second colour, a darker edge.
    kinds: chevron, saltire, cross, pale, bend, sun, star, wheel, tree (all simple geometric charges)."""
    k = 4
    W = size * k
    img = Image.new("RGBA", (W, W), hex_rgb(field) + (255,))
    d = ImageDraw.Draw(img)
    c = hex_rgb(charge) + (255,)
    w = W
    if kind == "chevron":
        t = 0.13 * w
        d.polygon([(0.08 * w, 0.78 * w), (0.5 * w, 0.30 * w), (0.92 * w, 0.78 * w), (0.92 * w, 0.78 * w + t), (0.5 * w, 0.30 * w + t * 1.2), (0.08 * w, 0.78 * w + t)], fill=c)
        for cx, cy in ((0.27, 0.22), (0.73, 0.22), (0.5, 0.82)):
            star(d, cx * w, cy * w, 0.075 * w, c)
    elif kind == "saltire":
        t = 0.10 * w
        d.polygon([(0, t), (t, 0), (w, w - t), (w - t, w)], fill=c)
        d.polygon([(w - t, 0), (w, t), (t, w), (0, w - t)], fill=c)
    elif kind == "cross":
        t = 0.09 * w
        d.rectangle([w / 2 - t, 0, w / 2 + t, w], fill=c)
        d.rectangle([0, 0.42 * w - t, w, 0.42 * w + t], fill=c)
    elif kind == "pale":
        d.rectangle([0.36 * w, 0, 0.64 * w, w], fill=c)
    elif kind == "bend":
        t = 0.12 * w
        d.polygon([(0, t), (t, 0), (w, w - t), (w - t, w)], fill=c)
    elif kind == "sun":
        cx, cy, r = 0.5 * w, 0.46 * w, 0.17 * w
        for i in range(16):
            a = i * np.pi / 8
            b = a + np.pi / 16
            d.polygon([(cx + r * 0.8 * np.cos(a - 0.12), cy + r * 0.8 * np.sin(a - 0.12)), (cx + r * 1.9 * np.cos(b - np.pi / 16), cy + r * 1.9 * np.sin(b - np.pi / 16)),
                       (cx + r * 0.8 * np.cos(a + 0.12), cy + r * 0.8 * np.sin(a + 0.12))], fill=c)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c)
    elif kind == "star":
        star(d, 0.5 * w, 0.45 * w, 0.3 * w, c)
    elif kind == "wheel":
        cx, cy, r = 0.5 * w, 0.46 * w, 0.28 * w
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=c, width=int(0.06 * w))
        for i in range(8):
            a = i * np.pi / 4
            d.line([(cx, cy), (cx + r * np.cos(a), cy + r * np.sin(a))], fill=c, width=int(0.035 * w))
        d.ellipse([cx - 0.06 * w, cy - 0.06 * w, cx + 0.06 * w, cy + 0.06 * w], fill=c)
    elif kind == "tree":
        d.rectangle([0.46 * w, 0.45 * w, 0.54 * w, 0.85 * w], fill=c)
        d.polygon([(0.5 * w, 0.10 * w), (0.80 * w, 0.55 * w), (0.20 * w, 0.55 * w)], fill=c)
    img = img.resize((size, size), Image.LANCZOS)
    a = np.asarray(img).astype(np.float64) / 255.0
    rgb = texio.srgb_to_linear(a[..., :3])
    # paint wear: lighter scuffs and a darker rim
    wear = fbm(size, 24, 4, 77)
    rgb *= (0.92 + 0.12 * wear)[..., None]
    yy, xx = np.mgrid[0:size, 0:size] / size
    rim = np.minimum.reduce([xx, yy, 1 - xx, 1 - yy])
    rgb *= (0.75 + 0.25 * np.clip(rim / 0.06, 0, 1))[..., None]
    return texio.mips_color(np.clip(rgb, 0, 1), np.ones((size, size)))


def star(d, cx, cy, r, c):
    pts = []
    for i in range(10):
        a = -np.pi / 2 + i * np.pi / 5
        rr = r if i % 2 == 0 else r * 0.42
        pts.append((cx + rr * np.cos(a), cy + rr * np.sin(a)))
    d.polygon(pts, fill=c)


def trim_patterns(w=256, h=64):
    """Woven borders (tablet-weave look), four original geometric patterns stacked vertically, each w x h, tiling in u:
    0 plain band between edge cords, 1 zigzag, 2 lozenges, 3 stepped key. R = motif thread (1) / ground thread (0),
    G = thread relief (height), B = edge cords, A = 1. Linear data (the shader colours it)."""
    rows = []
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    u = (x + 0.5) / w
    v = (y + 0.5) / h
    # weft ridges across the band (each pass of the tablets), and the warp twist (slanted threads)
    twist = 0.5 + 0.5 * np.sin(2 * np.pi * (u * 48 + (np.floor(v * 12) % 2) * 0.5 + v * 6))
    cords = ((v < 0.12) | (v > 0.88)).astype(float)
    inner = (v - 0.12) / 0.76
    for p in range(4):
        if p == 0:
            motif = ((np.abs(inner - 0.5) < 0.12)).astype(float)
        elif p == 1:
            zz = np.abs(((u * 4) % 1.0) - 0.5) * 2          # 0..1 triangle wave, 4 zigzags per repeat
            motif = (np.abs(inner - (0.2 + 0.6 * zz)) < 0.13).astype(float)
        elif p == 2:
            cu = ((u * 3) % 1.0) - 0.5
            cv = inner - 0.5
            dd = np.abs(cu) * 2.2 + np.abs(cv) * 2.0
            motif = ((dd < 0.95) & (dd > 0.55)).astype(float) + (dd < 0.22).astype(float)
        else:
            cu = (u * 4) % 1.0
            step = np.floor(cu * 4) / 4
            motif = ((inner > 0.15 + 0.6 * step) & (inner < 0.35 + 0.6 * step)).astype(float)
        motif = np.clip(motif, 0, 1) * (1 - cords)
        relief = 0.55 + 0.25 * twist * (1 - cords) + 0.35 * cords * (0.5 + 0.5 * np.sin(2 * np.pi * u * 64))
        rows.append(np.stack([motif, np.clip(relief, 0, 1), cords, np.ones_like(u)], -1))
    data = np.concatenate(rows, 0)
    return texio.mips_data(data)
