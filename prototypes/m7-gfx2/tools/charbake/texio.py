"""Texture I/O for the character bake: sRGB helpers, mip chains and the R1TX container (Gfx.Engine BakedTexture)."""
import struct

import numpy as np
from PIL import Image


def srgb_to_linear(c):
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float64) / 255.0


def load_gray(path, size=None):
    im = Image.open(path).convert("L")
    if size and im.size != (size, size):
        im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im).astype(np.float64) / 255.0


def resize(a, size, resample=Image.LANCZOS):
    """Resize an HxWxC float array in [0,1] (8-bit precision per channel through Pillow, deterministic)."""
    chans = []
    for c in range(a.shape[2]):
        im = Image.fromarray(np.clip(np.round(a[..., c] * 255), 0, 255).astype(np.uint8), "L")
        chans.append(np.asarray(im.resize(size, resample)).astype(np.float64) / 255.0)
    return np.stack(chans, -1)


def resize_float(a, size):
    """Bilinear resize of a float array via Pillow 'F' mode (deterministic)."""
    chans = []
    a3 = a if a.ndim == 3 else a[..., None]
    for c in range(a3.shape[2]):
        im = Image.fromarray(a3[..., c].astype(np.float32), "F")
        chans.append(np.asarray(im.resize(size, Image.BILINEAR)).astype(np.float64))
    out = np.stack(chans, -1)
    return out if a.ndim == 3 else out[..., 0]


def down2(a):
    return 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])


def to8(a):
    return np.clip(np.round(a * 255.0), 0, 255).astype(np.uint8)


def mips_color(rgb_lin, alpha=None, alpha_mode="mean"):
    """sRGB colour (filtered in linear light) + optional alpha (mean, or coverage-preserving 'cov')."""
    levels = []
    a, al = rgb_lin, alpha
    cov0 = None if al is None else float((al > 0.5).mean())
    while True:
        rgb8 = to8(linear_to_srgb(a))
        if al is None:
            lv = np.concatenate([rgb8, np.full(a.shape[:2] + (1,), 255, np.uint8)], -1)
        else:
            aa = al
            if alpha_mode == "cov" and cov0 is not None and al.shape[0] < rgb_lin.shape[0]:
                # alpha-to-coverage preservation (Castano 2010): rescale so the alpha-tested coverage stays constant
                lo, hi = 0.0, 4.0
                for _ in range(16):
                    mid = (lo + hi) / 2
                    if (np.clip(al * mid, 0, 1) > 0.5).mean() < cov0:
                        lo = mid
                    else:
                        hi = mid
                aa = np.clip(al * hi, 0, 1)
            lv = np.concatenate([rgb8, to8(aa)[..., None]], -1)
        levels.append(lv)
        if a.shape[0] == 1 or a.shape[1] == 1:
            break
        a = down2(a)
        if al is not None:
            al = down2(al)
    return levels


def mips_data(rgba):
    """Linear data (normal maps already encoded 0..1, masks): plain box filter, normals renormalised by caller."""
    levels = []
    a = rgba
    while True:
        levels.append(to8(a))
        if a.shape[0] == 1 or a.shape[1] == 1:
            break
        a = down2(a)
    return levels


def mips_normal(n, extra=None):
    """Tangent-space normal map: RG = xy*0.5+0.5, B = extra[0], A = extra[1] (e.g. roughness, AO)."""
    levels = []
    v = n
    e = extra
    while True:
        ln = np.linalg.norm(v, axis=-1, keepdims=True)
        nn = v / np.maximum(ln, 1e-6)
        rg = nn[..., :2] * 0.5 + 0.5
        if e is None:
            ba = np.stack([nn[..., 2] * 0.5 + 0.5, np.ones(nn.shape[:2])], -1)
        else:
            ba = e
        levels.append(to8(np.concatenate([rg, ba], -1)))
        if v.shape[0] == 1 or v.shape[1] == 1:
            break
        v = down2(v)
        if e is not None:
            e = down2(e)
    return levels


def r1tx_bytes(levels):
    h, w = levels[0].shape[:2]
    out = [b"R1TX", struct.pack("<IIIII", 1, w, h, len(levels), 0)]
    for lv in levels:
        assert lv.dtype == np.uint8 and lv.shape[2] == 4
        out.append(np.ascontiguousarray(lv).tobytes())
    return b"".join(out)


def normals_from_height(h, strength, wrap=True):
    if wrap:
        dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
        dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    else:
        dx = np.gradient(h, axis=1)
        dy = np.gradient(h, axis=0)
    # tangent frame convention of the character bake: T = dP/du, B = dP/dv with v = image row (downwards)
    n = np.stack([-dx * strength, -dy * strength, np.ones_like(h)], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)
