"""Skin and eye textures for one identity (CC0 inputs only).

Skin albedo: the MakeHuman 1.1 skin closest to the identity (512 px, CC0 badge in the image, mhjs mirror) is
upscaled to the identity's texture size (Lanczos), tone-matched in linear light to the identity's skin tone over the
face region (MPFB2 CC0 face mask), and given procedural detail: low-frequency mottling, redness on cheeks, nose and
ears, beard shadow and painted brow density where the identity has them. The CC0 badge strip at the right edge lies
outside every UV island (checked) and is overwritten by edge dilation.
Eye: MakeHuman's CC0 brown_eye.png with the iris hue/value moved to the identity's eye colour.
"""
import numpy as np
from PIL import Image, ImageDraw

import texio


def hex_lin(h):
    return texio.srgb_to_linear(np.array([int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)]) / 255.0)


def skin_albedo_from_display(hexcol):
    """READING: G1's skin tones were display colours for flat-shaded faces, not albedos. Real skin albedo (linear) is
    less saturated (red/blue ratio about 1.6-2.6 across tones): keep the luminance, pull the chroma 30 % towards grey,
    then scale to 90 % (sub-surface light adds back saturation and brightness in the shader)."""
    c = hex_lin(hexcol)
    lum = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
    return (lum + (c - lum) * 0.70) * 0.90


def uv_coverage(UV, F, size):
    """Rasterised UV coverage of triangles (UV with v already flipped: row 0 = v 0)."""
    img = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(img)
    pts = UV * size
    for tri in F:
        d.polygon([tuple(pts[tri[0]]), tuple(pts[tri[1]]), tuple(pts[tri[2]])], fill=255)
    return np.asarray(img) > 0


def dilate_fill(rgb, mask, iters=24):
    """Pushes island colours outwards into uncovered texels (mip-safe padding)."""
    rgb = rgb.copy()
    m = mask.copy()
    for _ in range(iters):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(mask.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sm = np.roll(m, (dy, dx), (0, 1))
            acc += np.roll(rgb, (dy, dx), (0, 1)) * sm[..., None]
            cnt += sm
        grow = (~m) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        m = m | grow
    # whatever is still uncovered takes the mean island colour
    rgb[~m] = rgb[mask].mean(0)
    return rgb


def value_noise(size, cells, seed):
    """Tileable value noise (bilinear-smoothstep) in [0,1], deterministic (integer hash)."""
    y, x = np.mgrid[0:size, 0:size] / size * cells
    xi, yi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - xi, y - yi
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)

    def h(ix, iy):
        ix %= cells
        iy %= cells
        v = (ix * 374761393 + iy * 668265263 + seed * 2147483647) & 0xFFFFFFFF
        v = ((v ^ (v >> 13)) * 1274126177) & 0xFFFFFFFF
        return ((v ^ (v >> 16)) & 0xFFFF) / 65535.0

    a, b, c, d = h(xi, yi), h(xi + 1, yi), h(xi, yi + 1), h(xi + 1, yi + 1)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(size, base_cells, octaves, seed, gain=0.5):
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += amp * value_noise(size, base_cells * (2 ** o), seed + 101 * o)
        tot += amp
        amp *= gain
    return out / tot


BADGE_BOX = (0.965, 0.70, 1.0, 1.0)  # u0, v0, u1, v1 of the CC0 badge strip in the MakeHuman skin atlases


def check_badge_clear(body_uv, body_F):
    """The skin atlases carry a CC0 badge at the right edge: no UV island may reach it (bake error otherwise)."""
    cov = uv_coverage(body_uv, body_F, 512)
    u0, v0, u1, v1 = BADGE_BOX
    box = cov[int(v0 * 512):int(v1 * 512), int(u0 * 512):int(u1 * 512)]
    if box.any():
        raise RuntimeError(f"UV islands reach the CC0 badge strip ({int(box.sum())} texels)")
    return cov


def skin_maps(spec, raw, body, fields, size, mask_size=1024, layers=()):
    """Per-identity skin albedo (RGB, alpha = cavity occlusion) and mask (R lips, G T-zone, B mouth interior, A thin).

    Albedo: the closest MakeHuman 1.1 CC0 skin, upscaled (Lanczos), tone-matched in linear light to the identity's
    tone over the face, plus procedural mottling, capillary redness (cheeks, nose, ears), beard shadow and brow
    density (vertex fields rasterised into UV space)."""
    import glob
    import os
    import uvraster
    base = spec["skin"]["base"]
    path = f"{raw}/mhjs-data/public/data/skins/{base}"
    cands = sorted(glob.glob(os.path.join(path, "textures", "*_diffuse*.png")))
    if not cands:
        raise FileNotFoundError(path)
    check_badge_clear(body["uv"], body["F"])
    src = texio.load_rgba(cands[0])[..., :3]
    lin = texio.srgb_to_linear(texio.resize(src, (size, size)))
    cov = uv_coverage(body["uv"], body["F"], size)
    # vertex fields -> UV (1024, then bilinear to the albedo size: the fields are smooth)
    I = fields.I
    src_v = body["src"]
    beard_cov = spec.get("beard", {}).get("coverage", 1.0) if spec.get("beard") else 0.0
    style = spec["hair"]["style"]
    scalp = fields.scalp(spec["hair"].get("hairline_drop", 0.0)) if style not in ("shaved", "none") else np.zeros(len(fields.V))
    vals = np.stack([fields.redness(), fields.beard(1.0), scalp, fields.cavity(I.A.body_fv),
                     fields.tzone(), fields.thin(), fields.brows()], 1)[src_v]
    img, fcov = uvraster.rasterize(body["uv"], body["F"], vals, mask_size)
    img = uvraster.dilate(img, fcov, 8)
    up = texio.resize_float(img, (size, size)) if size != mask_size else img
    red, beard, scalp_uv, cav = up[..., 0], up[..., 1], up[..., 2], up[..., 3]
    face = texio.resize_float(fields_masks(raw, mask_size)["face"], (size, size)) > 0.5
    target = skin_albedo_from_display(spec["skin"]["tone"])
    mean = lin[face & cov].mean(0)
    lin = lin * (target / mean)[None, None, :]
    seed = spec["seed"]
    mott = fbm(size, 10, 4, seed) - 0.5
    blotch = fbm(size, 40, 3, seed + 7) - 0.5
    lin *= (1 + 0.09 * mott + 0.05 * blotch)[..., None]
    # fine detail the 512-texel source lacks: pore-scale speckle and a few small darker spots (freckles, blemishes),
    # scaled to the texture size so a soldier's 1,024 texture and the hero's 2,048 get the same physical scale
    fine = value_noise(size, max(64, size // 3), seed + 13) - 0.5
    spots = np.clip((value_noise(size, max(32, size // 8), seed + 17) - 0.86) / 0.14, 0, 1) ** 2
    lin *= (1 + 0.06 * fine - 0.10 * spots)[..., None]
    lin[..., 0] *= 1 + 0.14 * red
    lin[..., 1] *= 1 - 0.06 * red
    lin[..., 2] *= 1 - 0.03 * red
    hair = hex_lin(spec["hair"]["colour"])
    # shadow of hair roots under the beard (shaved or not) and a dense base under scalp hair, then painted strands
    if beard_cov > 0 and spec.get("beard_shadow", 0) > 0:
        k = np.clip(beard * spec["beard_shadow"], 0, 1)[..., None]
        grain = 0.75 + 0.5 * value_noise(size, 256, seed + 3)[..., None]
        lin = lin * (1 - 0.45 * k * grain) + hair[None, None, :] * (0.45 * k * grain) * 0.5
    sb = np.clip(scalp_uv, 0, 1) ** 2 * 0.65
    lin = lin * (1 - sb[..., None]) + (hair * 0.55)[None, None, :] * sb[..., None]
    # painted strands stay on their own region: a stroke extrapolated across a UV seam must not land on another island
    region = {"scalp": scalp_uv, "beard": beard, "brows": up[..., 6]}
    for rgb, cv, kind in layers:
        cv = cv * (region[kind] > 0.01)
        lin = lin * (1 - cv[..., None]) + rgb * cv[..., None]
    lin = np.clip(lin, 0, 1)
    lin = dilate_fill(lin, cov)
    alpha = np.clip(cav, 0, 1)
    albedo_levels = texio.mips_color(lin, alpha)
    # mask texture
    mk = fields_masks(raw, mask_size)
    mask = np.stack([mk["lips"], img[..., 4], mk["inside-mouth"], np.clip(img[..., 5], 0, 1)], -1)
    mask_levels = texio.mips_data(np.clip(mask, 0, 1))
    return albedo_levels, mask_levels


_MASKS = {}


def fields_masks(raw, size):
    key = (raw, size)
    if key not in _MASKS:
        D = f"{raw}/mpfb2/src/mpfb/data/textures"
        _MASKS[key] = {k: texio.load_gray(f"{D}/mpfb_{k}.jpg", size) for k in ("face", "lips", "eyelids", "ears", "inside-mouth")}
    return _MASKS[key]


def detail_normal(size=512, seed=11):
    """Shared tiling skin micro-relief: pores (jittered pits), crossing fine lines and a little noise."""
    rng = np.random.default_rng(seed)
    h = np.zeros((size, size))
    yy, xx = np.mgrid[0:size, 0:size]
    cell = 7
    n = size // cell
    jitter = rng.random((n, n, 2))
    depth = 0.6 + 0.4 * rng.random((n, n))
    rad = 0.9 + 0.9 * rng.random((n, n))
    for gy in range(n):
        for gx in range(n):
            cx = (gx + jitter[gy, gx, 0]) * cell
            cy = (gy + jitter[gy, gx, 1]) * cell
            x0, y0 = int(cx) - 4, int(cy) - 4
            ys = (np.arange(y0, y0 + 9) % size)
            xs = (np.arange(x0, x0 + 9) % size)
            dy = (np.arange(y0, y0 + 9) - cy)[:, None]
            dx = (np.arange(x0, x0 + 9) - cx)[None, :]
            pit = depth[gy, gx] * np.exp(-(dx * dx + dy * dy) / (2 * rad[gy, gx] ** 2))
            h[np.ix_(ys, xs)] -= pit
    # fine crossing lines (skin micro-furrows), modulated by noise
    a1 = np.sin((xx * 0.8 + yy * 0.55) * 2 * np.pi / 11.0)
    a2 = np.sin((xx * -0.5 + yy * 0.85) * 2 * np.pi / 13.0)
    mod = value_noise(size, 16, seed + 1)
    h += 0.18 * (np.abs(a1) * mod + np.abs(a2) * (1 - mod))
    h += 0.25 * (value_noise(size, 64, seed + 2) - 0.5)
    h = (h - h.mean()) / (h.std() + 1e-9)
    n = texio.normals_from_height(h, 0.35)
    return texio.mips_normal(n)


def eye_texture(raw, colour_hex, size=1024):
    """CC0 brown_eye.png with the iris recoloured: hue and value from the identity's eye colour."""
    src = texio.load_rgba(f"{raw}/mh-core/makehuman/data/eyes/materials/brown_eye.png")
    if src.shape[0] != size:
        src = texio.resize(src, (size, size))
    rgb = texio.srgb_to_linear(src[..., :3])
    # iris pixels: saturated (the sclera is near-grey, the pupil black)
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-4)
    iris = np.clip((sat - 0.25) / 0.25, 0, 1) * (mx > 0.01)
    lum = 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]
    tgt = hex_lin(colour_hex)
    tl = 0.2126 * tgt[0] + 0.7152 * tgt[1] + 0.0722 * tgt[2]
    rec = (lum / max(float(lum[iris > 0.5].mean()), 1e-4))[..., None] * tgt[None, None, :] * (0.55 + 0.45 * tl / max(tl, 1e-4))
    rgb = rgb * (1 - iris[..., None]) + rec * iris[..., None]
    alpha = src[..., 3]
    return np.clip(rgb, 0, 1), alpha
