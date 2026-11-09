#!/usr/bin/env python3
"""Bake the G2 photographic terrain material sets (stage render1).

Reads pinned raw files from $G2_ASSETS/raw (see assets/manifest.tsv) and writes
$G2_ASSETS/baked/render1/terrain/<set>_{alb_ht,nrm_rgh}.r1tex plus MANIFEST.tsv and calibration.json.
Nothing it writes is committed. Every transformation is listed in assets/PROVENANCE.md (section "render1 bake").

Per set:
  albedo  -> linear, scaled to a natural mean albedo (luminance), back to sRGB 8 bit
  height  -> upstream height (Terrain3D) or a percentile-stretched, lightly blurred luminance (derived)
  normal  -> upstream (Terrain3D, OpenGL +Y), Y-flipped (rockyGround, DirectX -Y), or derived from height (Sobel, wrap)
  rough   -> upstream (grass), remapped (rock, kept rough), or derived (gravel, sand, soil)
  mips    -> box filter in linear light (albedo), mean height, mean normal vector; roughness widened by the
             Toksvig factor of the mean normal's length (specular anti-aliasing); every level renormalised
Output container (little endian): "R1TX" u32 version=1, width, height, levels, format=0 (RGBA8), then every level.

Usage: bake-render1-terrain.py RAW_DIR OUT_DIR     (run through scripts/bake-render1.sh)
Deterministic for a given numpy/Pillow version (recorded in MANIFEST.tsv).
"""
import hashlib
import json
import os
import struct
import sys

import numpy as np
from PIL import Image
import PIL

# Natural mean albedo targets (linear luminance), chosen from published ranges for the surface type:
# green grass/meadow 0.08-0.15, dry stony soil and gravel 0.15-0.25, grey rock 0.20-0.30, dry sand 0.30-0.45,
# dry brown soil 0.10-0.20.
SETS = [
    # name,   albedo source,                                          normal/rough source,                                   target, kind
    ("grass", "terrain3d/project/demo/assets/textures/ground037_alb_ht.png", "terrain3d/project/demo/assets/textures/ground037_nrm_rgh.png", 0.105, "terrain3d"),
    ("gravel", "babylon-assets/textures/rockyGround_basecolor.png", "babylon-assets/textures/rockyGround_normal.png", 0.19, "flipY"),
    ("rock", "terrain3d/project/demo/assets/textures/rock023_alb_ht.png", "terrain3d/project/demo/assets/textures/rock023_nrm_rgh.png", 0.21, "terrain3d"),
    ("sand", "babylon-assets/textures/sand.jpg", None, 0.30, "derived"),
    ("soil", "babylon-assets/textures/dirt.jpg", None, 0.13, "derived"),
]
SIZE = 1024


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def lum(lin):
    return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float64) / 255.0


def resize_wrap(img, size):
    """Resize a tileable image to size x size without border seams: tile 3x3, resize, crop the centre."""
    if img.shape[0] == size and img.shape[1] == size:
        return img
    h, w = img.shape[:2]
    big = np.tile(img, (3, 3, 1))
    chans = []
    for c in range(img.shape[2]):
        im = Image.fromarray(big[..., c].astype(np.float32), mode="F").resize((3 * size, 3 * size), Image.LANCZOS)
        chans.append(np.asarray(im, dtype=np.float64)[size:2 * size, size:2 * size])
    return np.clip(np.stack(chans, axis=-1), 0.0, 1.0)


def blur3(a):
    """3x3 box blur with wrap."""
    s = np.zeros_like(a)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            s += np.roll(np.roll(a, dy, axis=0), dx, axis=1)
    return s / 9.0


def stretch(a, lo=2.0, hi=98.0):
    p0, p1 = np.percentile(a, lo), np.percentile(a, hi)
    return np.clip((a - p0) / max(p1 - p0, 1e-6), 0.0, 1.0)


def normals_from_height(h, strength):
    """OpenGL convention (+Y = towards decreasing row): n = (-dh/dcol, +dh/drow, 1/strength), wrap."""
    dcol = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    drow = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    n = np.stack([-dcol * strength, drow * strength, np.ones_like(h)], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def decode_normal(rgb):
    n = rgb * 2.0 - 1.0
    n[..., 2] = np.maximum(n[..., 2], 1e-3)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def down2(a):
    return 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])


def build_mips(alb_lin, height, normal, rough):
    """Lists of RGBA8 arrays per level for (albedo+height, normal+rough)."""
    alb_levels, nrm_levels = [], []
    a, h, n, r = alb_lin, height, normal, rough
    while True:
        alb8 = np.concatenate([linear_to_srgb(a), h[..., None]], axis=-1)
        length = np.linalg.norm(n, axis=-1)
        nn = n / np.maximum(length[..., None], 1e-6)
        # Toksvig: variance of the mean normal widens the GGX lobe (alpha = rough^2)
        var = np.clip((1.0 - length) / np.maximum(length, 1e-6), 0.0, 1.0)
        alpha = np.sqrt(np.clip(r ** 4 + var, 0.0, 1.0))
        r_adj = np.sqrt(alpha)
        nrm8 = np.concatenate([nn[..., :2] * 0.5 + 0.5, r_adj[..., None], np.ones_like(r_adj)[..., None]], axis=-1)
        alb_levels.append(np.clip(np.round(alb8 * 255.0), 0, 255).astype(np.uint8))
        nrm_levels.append(np.clip(np.round(nrm8 * 255.0), 0, 255).astype(np.uint8))
        if a.shape[0] == 1:
            break
        a, h, n, r = down2(a), down2(h), down2(n), down2(r)
    return alb_levels, nrm_levels


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


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    raw, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    calib, rows = {}, []
    for name, alb_src, nrm_src, target, kind in SETS:
        alb_rgba = resize_wrap(load_rgba(os.path.join(raw, alb_src)), SIZE)
        lin = srgb_to_linear(alb_rgba[..., :3])
        mean_before = float(lum(lin).mean())
        scale = target / mean_before
        lin = np.clip(lin * scale, 0.0, 1.0)
        if kind == "terrain3d":
            height = alb_rgba[..., 3]
            nr = resize_wrap(load_rgba(os.path.join(raw, nrm_src)), SIZE)
            normal = decode_normal(nr[..., :3])
            rough = nr[..., 3]
            if name == "rock":  # upstream 0.56-0.79: keep dry rock rough so it never mirrors the sky
                rough = 0.72 + 0.25 * np.clip((rough - 0.557) / (0.788 - 0.557), 0.0, 1.0)
            else:
                rough = np.clip(rough, 0.62, 1.0)
        elif kind == "flipY":
            height = stretch(blur3(lum(srgb_to_linear(alb_rgba[..., :3]))))
            nr = resize_wrap(load_rgba(os.path.join(raw, nrm_src)), SIZE)
            nrgb = nr[..., :3].copy()
            nrgb[..., 1] = 1.0 - nrgb[..., 1]  # DirectX (-Y) to OpenGL (+Y)
            normal = decode_normal(nrgb)
            rough = 0.80 + 0.14 * (1.0 - height)
        else:
            height = stretch(blur3(blur3(lum(srgb_to_linear(alb_rgba[..., :3])))))
            normal = normals_from_height(height, 3.0 if name == "sand" else 4.0)
            rough = (0.90 if name == "sand" else 0.84) + 0.08 * (1.0 - height)
        mean_after = float(lum(lin).mean())
        alb_levels, nrm_levels = build_mips(lin, height, normal, np.clip(rough, 0.0, 1.0))
        for suffix, levels in (("alb_ht", alb_levels), ("nrm_rgh", nrm_levels)):
            path = os.path.join(out, f"{name}_{suffix}.r1tex")
            write_r1tex(path, levels)
            rows.append((f"{name}_{suffix}.r1tex", sha256(path), os.path.getsize(path), alb_src if suffix == "alb_ht" else (nrm_src or alb_src + " (derived)")))
        calib[name] = {
            "albedo_source": alb_src, "normal_source": nrm_src or "derived from albedo luminance", "kind": kind,
            "mean_linear_luminance_upstream": round(mean_before, 5), "target": target, "scale": round(scale, 5),
            "mean_linear_luminance_baked": round(mean_after, 5),
            "mean_linear_rgb_baked": [round(float(v), 5) for v in lin.reshape(-1, 3).mean(axis=0)],
            "roughness_mean": round(float(np.clip(rough, 0, 1).mean()), 4),
        }
        print(f"{name}: mean albedo {mean_before:.4f} -> {mean_after:.4f} (x{scale:.3f}), rough mean {calib[name]['roughness_mean']}")
    with open(os.path.join(out, "calibration.json"), "w") as f:
        json.dump(calib, f, indent=2, sort_keys=True)
    with open(os.path.join(out, "MANIFEST.tsv"), "w") as f:
        f.write(f"# render1 terrain bake; numpy {np.__version__}; Pillow {PIL.__version__}\n")
        f.write("# file\tsha256\tbytes\tsource\n")
        for r in rows:
            f.write("\t".join(str(x) for x in r) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
