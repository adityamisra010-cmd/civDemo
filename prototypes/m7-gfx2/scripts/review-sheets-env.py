#!/usr/bin/env python3
"""Compose the small labelled review JPEGs of stage env from full-size captures (never commits PNGs).

Usage: review-sheets-env.py CAPTURE_DIR OUT_DIR
  CAPTURE_DIR  output of scripts/capture-env.sh (final/, before-g2-env-off/, before-g1-preset/, ab/<switch>/)
  OUT_DIR      where the JPEGs go (reports/captures/env); each is kept under 220 KB
Every sheet carries a legend line: renderer llvmpipe; no PROTOTYPE-ONLY asset appears in any env sheet; BEFORE frames
show the G1 content (procedural placeholder props) on the same camera.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

LEGEND = ("llvmpipe software GL (not Victus evidence). No PROTOTYPE-ONLY assets. Environment = presentation only; "
          "BEFORE = same camera, environment off (G1 content in the render1 frame).")


def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def panel(path, size, box=None):
    im = Image.open(path).convert("RGB")
    if box:
        im = im.crop(box)
    return im.resize(size, Image.LANCZOS)


def sheet(out, title, items, cols, cell, budget=220_000):
    w, h = cell
    rows = (len(items) + cols - 1) // cols
    head, foot = 26, 18
    img = Image.new("RGB", (cols * w, head + rows * h + foot), (18, 18, 18))
    d = ImageDraw.Draw(img)
    d.text((8, 5), title, fill=(235, 235, 235), font=font(15))
    for i, (path, lab, box) in enumerate(items):
        x, y = (i % cols) * w, head + (i // cols) * h
        img.paste(panel(path, (w, h), box), (x, y))
        f = font(13)
        tw = d.textlength(lab, font=f)
        d.rectangle([x + 5, y + 5, x + 13 + tw, y + 24], fill=(0, 0, 0))
        d.text((x + 9, y + 7), lab, fill=(255, 255, 255), font=f)
    d.text((8, head + rows * h + 2), LEGEND, fill=(170, 170, 170), font=font(11))
    for q in (82, 76, 70, 64, 58, 52):
        img.save(out, quality=q, optimize=True, progressive=True)
        if os.path.getsize(out) <= budget:
            break
    print(f"{os.path.basename(out)}: {os.path.getsize(out)} bytes")


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    cap, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)
    fin = lambda n: os.path.join(cap, "final", n + ".png")
    bef = lambda n: os.path.join(cap, "before-g2-env-off", n + ".png")
    g1p = lambda n: os.path.join(cap, "before-g1-preset", n + ".png")
    ab = lambda sw, n: os.path.join(cap, "ab", sw, n + ".png")
    T, A = "env-temperate-", "env-arid-"

    sheet(os.path.join(out, "env-01-forest-edge.jpg"), "Forest edge: BEFORE (environment off) | AFTER (16 samples)",
          [(bef(T + "forest-edge"), "BEFORE", None), (fin(T + "forest-edge"), "AFTER", None)], 2, (800, 450))
    sheet(os.path.join(out, "env-02-river.jpg"), "River: on the bank and from the valley side",
          [(bef(T + "river"), "BEFORE river", None), (fin(T + "river"), "AFTER river", None),
           (bef(T + "river-high"), "BEFORE river-high", None), (fin(T + "river-high"), "AFTER river-high", None)], 2, (640, 360))
    sheet(os.path.join(out, "env-03-iron-age-village.jpg"), "Iron Age palisaded village: settlement and gate",
          [(bef(T + "settlement"), "BEFORE settlement", None), (fin(T + "settlement"), "AFTER settlement", None),
           (bef(T + "settlement-gate"), "BEFORE gate", None), (fin(T + "settlement-gate"), "AFTER gate", None)], 2, (640, 360))
    sheet(os.path.join(out, "env-04-classical-town.jpg"), "Classical stone-walled town: fortification and its gate",
          [(bef(A + "fortification"), "BEFORE fortification", None), (fin(A + "fortification"), "AFTER fortification", None),
           (bef(A + "fortification-gate"), "BEFORE gate", None), (fin(A + "fortification-gate"), "AFTER gate", None)], 2, (640, 360))
    sheet(os.path.join(out, "env-05-vistas.jpg"), "Vistas from 120 m and more: temperate forest and arid maquis",
          [(bef(T + "vista"), "BEFORE temperate vista", None), (fin(T + "vista"), "AFTER temperate vista", None),
           (bef(A + "vista"), "BEFORE arid vista", None), (fin(A + "vista"), "AFTER arid vista", None)], 2, (640, 360))
    sheet(os.path.join(out, "env-06-trees.jpg"), "Species (ez-tree port): oak | ash, aspen | pine, shrubs; oak close, backlit, arid maquis scrub",
          [(fin("env-trees-lineup-oak-ash"), "oak | ash", None), (fin("env-trees-lineup-aspen-pine"), "aspen | pine", None),
           (fin("env-trees-lineup-shrubs"), "shrubs (hazel/hawthorn-like)", None), (fin("env-trees-oak-close"), "oak close (bark, leaf cards)", None),
           (fin("env-trees-backlit"), "backlit (leaf translucency)", None), (fin(A + "maquis"), "arid maquis (scrub, dry grass)", None)],
          3, (427, 240))
    sheet(os.path.join(out, "env-07-ground-lod.jpg"), "LOD sweep, town street, ground cover in the G2 frame against the G1 preset",
          [(fin("env-trees-lod-sweep"), "AFTER LOD sweep (oaks 15 m to 290 m)", None), (fin(A + "town-street"), "AFTER town street", None),
           (fin(T + "forest-edge"), "AFTER forest edge, foreground", (0, 360, 640, 720)),
           (g1p(T + "forest-edge"), "same camera, --preset g1", (0, 360, 640, 720))], 2, (640, 360))
    sheet(os.path.join(out, "env-08-switches.jpg"), "One switch off at a time (one-sample FXAA frames): ON | OFF",
          [(ab("on", T + "forest-edge"), "ON", None), (ab("trees-0", T + "forest-edge"), "trees=0", None),
           (ab("on", T + "forest-edge"), "ON (foreground)", (0, 240, 853, 720)), (ab("groundcover-0", T + "forest-edge"), "groundcover=0", (0, 240, 853, 720)),
           (ab("on", T + "river"), "ON", None), (ab("water-0", T + "river"), "water=0 (render1 water)", None),
           (ab("on", T + "river"), "ON", None), (ab("reflect-0", T + "river"), "reflect=0 (sky only)", None),
           (ab("on", T + "settlement"), "ON", None), (ab("structures-0", T + "settlement"), "structures=0", None),
           (ab("on", T + "vista"), "ON", None), (ab("forestfloor-0", T + "vista"), "forestfloor=0 (cell-based)", None)],
          4, (400, 225))
    return 0


if __name__ == "__main__":
    sys.exit(main())
