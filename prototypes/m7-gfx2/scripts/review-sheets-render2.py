#!/usr/bin/env python3
"""Compose the small labelled review JPEGs of stage render2 from full-size captures (never commits PNGs).

Usage: review-sheets-render2.py G1_DIR CAPTURE_DIR OUT_DIR
  G1_DIR       pristine G1 captures (prototypes/m7-gfx1 Gfx.Demo --capture): units-clash-*, units-wide5k-*
  CAPTURE_DIR  output of scripts/capture-render2.sh (one folder per capture set)
  OUT_DIR      where the JPEGs go (reports/captures/render2); each is kept under 170 KB
Crops that show one effect (the LOD dither, TAA edges) are found automatically: the dither crop is centred on the
pixels that change when the cross-fade is switched off, the TAA crops on the window with the most edge energy.
Every sheet carries a legend: renderer llvmpipe (not Victus evidence); the PROTOTYPE-ONLY input in frame is the CMU
motion capture that poses the characters and the crowd (provisional terms, assets/PROVENANCE.md 3.4 and 3.12).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

LEGEND = ("llvmpipe software GL, not Victus evidence. PROTOTYPE-ONLY in frame: CMU motion capture poses the characters "
          "and the crowd (provisional terms). Soldiers CC0 MakeHuman/MPFB2 + generated kit; terrain render1 bake "
          "(CC0/MIT/CC-BY); environment stage env (CC0/MIT, generated).")
LEGEND_G1 = LEGEND + " G1 panels: the pristine G1 renderer and its procedural soldiers."


def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def load(path):
    return Image.open(path).convert("RGB")


def panel(src, size, box=None, nearest=False):
    im = load(src) if isinstance(src, str) else src
    if box:
        im = im.crop(box)
    return im.resize(size, Image.NEAREST if nearest else Image.LANCZOS)


def sheet(out, title, items, cols, cell, budget=170_000, legend=LEGEND):
    """items: (path or image, label, crop box or None[, nearest])."""
    w, h = cell
    rows = (len(items) + cols - 1) // cols
    head, foot = 24, 30
    img = Image.new("RGB", (cols * w, head + rows * h + foot), (18, 18, 18))
    d = ImageDraw.Draw(img)
    d.text((8, 4), title, fill=(235, 235, 235), font=font(14))
    for i, it in enumerate(items):
        src, lab, box = it[0], it[1], it[2]
        nearest = len(it) > 3 and it[3]
        x, y = (i % cols) * w, head + (i // cols) * h
        img.paste(panel(src, (w, h), box, nearest), (x, y))
        tw = d.textlength(lab, font=font(12))
        d.rectangle([x + 5, y + 5, x + 13 + tw, y + 22], fill=(0, 0, 0))
        d.text((x + 9, y + 6), lab, fill=(255, 255, 255), font=font(12))
    words, line, lines = legend.split(" "), "", []
    for wd in words:
        if d.textlength(line + " " + wd, font=font(10)) > cols * w - 16:
            lines.append(line)
            line = wd
        else:
            line = (line + " " + wd).strip()
    lines.append(line)
    for k, ln in enumerate(lines[:2]):
        d.text((8, head + rows * h + 3 + 13 * k), ln, fill=(170, 170, 170), font=font(10))
    for q in (85, 80, 75, 70, 64, 58, 52):
        img.save(out, quality=q, optimize=True, progressive=True)
        if os.path.getsize(out) <= budget:
            break
    print(f"{os.path.basename(out)}: {os.path.getsize(out)} bytes, {cols * w}x{head + rows * h + foot}")


def diff_box(a, b, w, h, thresh=12):
    """A w x h box centred on the pixels that differ between two captures (by more than thresh in any channel)."""
    x = np.asarray(load(a), dtype=np.int16)
    y = np.asarray(load(b), dtype=np.int16)
    m = np.abs(x - y).max(axis=2) > thresh
    ys, xs = np.nonzero(m)
    if len(xs) == 0:
        cx, cy = x.shape[1] // 2, x.shape[0] // 2
    else:
        cx, cy = int(np.median(xs)), int(np.median(ys))
    return clamp_box(cx - w // 2, cy - h // 2, w, h, x.shape[1], x.shape[0]), int(m.sum())


def edge_box(path, w, h, step=16):
    """The w x h window with the most luminance-gradient energy (where aliasing shows)."""
    g = np.asarray(load(path).convert("L"), dtype=np.float32)
    e = np.abs(np.diff(g, axis=1))[:-1, :] + np.abs(np.diff(g, axis=0))[:, :-1]
    ii = np.pad(e, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
    best, bx, by = -1.0, 0, 0
    for y0 in range(0, e.shape[0] - h, step):
        for x0 in range(0, e.shape[1] - w, step):
            s = ii[y0 + h, x0 + w] - ii[y0, x0 + w] - ii[y0 + h, x0] + ii[y0, x0]
            if s > best:
                best, bx, by = s, x0, y0
    return (bx, by, bx + w, by + h)


def diff_img(a, b, gain=6):
    """|a − b| (largest channel) times gain, as a grey image: where a feature changed the frame."""
    x = np.asarray(load(a), dtype=np.int16)
    y = np.asarray(load(b), dtype=np.int16)
    d = np.clip(np.abs(x - y).max(axis=2) * gain, 0, 255).astype(np.uint8)
    return Image.fromarray(d).convert("RGB")


def clamp_box(x0, y0, w, h, W, H):
    x0 = max(0, min(W - w, x0))
    y0 = max(0, min(H - h, y0))
    return (x0, y0, x0 + w, y0 + h)


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    g1, cap, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)

    def c(folder, name):
        return os.path.join(cap, folder, name + ".png")

    def g(name):
        return os.path.join(g1, name + ".png")

    # 1. AO and contact shadows: render2 off (= the trunk frame, bit for bit) | on | where it changed | the terms
    feet = (0, 440, 480, 710)          # the left group's feet in lines-low
    gate = (300, 250, 940, 610)        # palisade, gate and grass in the env village gate shot
    off_low, on_low = c("forces-off", "chars-forces-lines-low"), c("forces", "chars-forces-lines-low")
    off_gate, on_gate = c("env-off", "env-temperate-settlement-gate"), c("env", "env-temperate-settlement-gate")
    sheet(os.path.join(out, "render2-01-ao-contact.jpg"),
          "AO + contact shadows, 16-sample stills: off (= trunk frame, bit for bit) | on (default) | |on - off| x6 | the term alone",
          [(off_low, "off", None), (on_low, "on", None), (diff_img(off_low, on_low), "|on - off| x6", None),
           (c("forces-debug-ao", "chars-forces-lines-low"), "AO term", None),
           (off_low, "off: feet", feet), (on_low, "on: feet", feet), (diff_img(off_low, on_low), "|on - off| x6: feet", feet),
           (c("forces-debug-contact", "chars-forces-lines-low"), "contact term", None),
           (off_gate, "env gate, off", gate), (on_gate, "env gate, on", gate), (diff_img(off_gate, on_gate), "|on - off| x6", gate),
           (c("forces-debug-shares", "chars-forces-lines-low"), "shares: ambient r, direct g", None)],
          cols=4, cell=(320, 180))

    # 2. Crowd scale: G1 BEFORE vs render2 AFTER at 220, about 2,000 and about 10,000
    sheet(os.path.join(out, "render2-02-crowd-g1-vs-g2.jpg"),
          "Crowds: G1 procedural soldiers (BEFORE) vs render2 instanced stage-chars soldiers (AFTER, 16 / 16 / 8-sample stills)",
          [(g("units-clash-melee"), "G1 units-clash, 220", None),
           (c("crowd220", "crowd-formation-front-low"), "render2 crowd-formation, 220", None),
           (g("units-wide5k-frontline"), "G1 units-wide5k, 5,000", None),
           (c("crowd2k", "crowd-battle-2k-front-low"), "render2 crowd-battle-2k, 2,032", None),
           (g("units-wide5k-wide"), "G1 units-wide5k, wide", None),
           (c("crowd10k", "crowd-battle-10k-vista"), "render2 crowd-battle-10k, 10,084", None)],
          cols=2, cell=(480, 270), legend=LEGEND_G1)

    # 3. Crowd levels: forced lod1 / lod2 / lod3 and the dithered cross-fade
    close = (420, 120, 900, 390)
    dbox, n = diff_box(c("fade-1spp", "crowd-formation-front-low"), c("nofade-1spp", "crowd-formation-front-low"), 200, 113)
    print(f"dither crop {dbox} ({n} px differ between crowdfade=1 and crowdfade=0)")
    sheet(os.path.join(out, "render2-03-crowd-lod.jpg"),
          "Crowd levels, forced: lod1 (~3,200 tris) | lod2 (~1,600) | lod3 (638-781); cross-fade (crowdlod=0.11 puts the nearest soldiers in it)",
          [(c("lod0", "crowd-formation-close"), "lod1", close),
           (c("lod1", "crowd-formation-close"), "lod2", close),
           (c("lod2", "crowd-formation-close"), "lod3", close),
           (c("fade-1spp", "crowd-formation-front-low"), "lod2/lod3 cross-fade, 1 sample, x2", dbox, True),
           (c("fade-16spp", "crowd-formation-front-low"), "same, 16 samples, x2", dbox, True),
           (c("nofade-1spp", "crowd-formation-front-low"), "hard switch (crowdfade=0), x2", dbox, True),
           (c("crowd220", "crowd-formation-rear"), "variation: rear of force A", None),
           (c("crowd220", "crowd-formation-dusk"), "17:36 sun", None),
           (c("crowd2k", "crowd-battle-2k-line"), "2k line: auto levels", None)],
          cols=3, cell=(400, 225))

    # 4. TAA in motion: last frame of a 16-frame move, none | FXAA | TAA, 2x crops of the edgiest 200x113 window
    rows = []
    for folder, scene, shot, move in (("motion-220", "crowd-formation", "front-low", "pan"), ("motion-forces", "chars-forces", "lines-34", "orbit")):
        f = {aa: c(folder, f"{scene}-{shot}-{aa}-f15") for aa in ("none", "fxaa", "taa")}
        box = edge_box(f["none"], 200, 113)
        print(f"taa crop {folder}: {box}")
        rows += [(f["none"], f"{scene} ({move}), no AA, x2", box, True), (f["fxaa"], "FXAA, x2", box, True),
                 (f["taa"], "TAA, frame 16 of the move, x2", box, True)]
    sheet(os.path.join(out, "render2-04-taa-motion.jpg"),
          "Anti-aliasing with the camera moving every frame: none | FXAA | TAA (stills keep their 16-sample accumulation)",
          rows, cols=3, cell=(400, 225))

    # 5. Cascades and culling
    sheet(os.path.join(out, "render2-05-cascades-culling.jpg"),
          "Cascade debug view (cascades 1-4 red, green, blue, yellow; blend bands) and the culling shots (culling on = off, 0 px differ)",
          [(c("forces-debug-cascades", "chars-forces-lines-34"), "cascades, chars-forces", None),
           (c("crowd10k-debug-cascades", "crowd-battle-10k-vista"), "cascades, 10k vista", None),
           (c("cull-on", "crowd-battle-2k-village"), "2k village: 774 of 2,032 occlusion-culled", None),
           (c("crowd10k", "crowd-battle-10k-rear"), "10k rear", None)],
          cols=2, cell=(480, 270))
    return 0


if __name__ == "__main__":
    sys.exit(main())
