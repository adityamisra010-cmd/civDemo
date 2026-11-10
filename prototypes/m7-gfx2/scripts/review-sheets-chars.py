#!/usr/bin/env python3
"""Compose the small labelled review JPEGs of stage chars from full-size captures (never commits PNGs).

Usage: review-sheets-chars.py G1_DIR CAPTURE_DIR OUT_DIR
  G1_DIR       pristine G1 captures (prototypes/m7-gfx1 Gfx.Demo --capture all): avatar-turntable-*, avatar-eras-*,
               units-closeup-kits-clips
  CAPTURE_DIR  output of scripts/capture-chars.sh (chars-hero-*, chars-eras-*, chars-forces-*, chars-lod-*, chars-anim-*)
  OUT_DIR      where the JPEGs go (reports/captures/chars); each is kept under 170 KB
Every sheet carries a legend: renderer llvmpipe; the one PROTOTYPE-ONLY input in frame is the CMU motion capture that
poses the characters (provisional terms, assets/PROVENANCE.md 3.4); everything else is CC0 (MakeHuman, MPFB2), CC-BY
(BabylonJS ground textures, render1) or generated in code.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

LEGEND = ("llvmpipe software GL, not Victus evidence. PROTOTYPE-ONLY in frame: CMU motion capture poses (provisional terms). "
          "Bodies CC0 MakeHuman/MPFB2; garments, armour, hair, beards generated in code.")


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


def sheet(out, title, items, cols, cell, budget=170_000, legend=LEGEND):
    """items: (path, label, crop box or None)."""
    w, h = cell
    rows = (len(items) + cols - 1) // cols
    head, foot = 24, 30
    img = Image.new("RGB", (cols * w, head + rows * h + foot), (18, 18, 18))
    d = ImageDraw.Draw(img)
    d.text((8, 4), title, fill=(235, 235, 235), font=font(14))
    for i, (path, lab, box) in enumerate(items):
        x, y = (i % cols) * w, head + (i // cols) * h
        img.paste(panel(path, (w, h), box), (x, y))
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
    for q in (85, 80, 75, 70, 64, 58):
        img.save(out, quality=q, optimize=True, progressive=True)
        if os.path.getsize(out) <= budget:
            break
    print(f"{os.path.basename(out)}: {os.path.getsize(out)} bytes")


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    g1, cap, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)

    def c(name):
        return os.path.join(cap, name + ".png")

    def g(name):
        return os.path.join(g1, name + ".png")

    face = (320, 0, 960, 720)  # centre 8:9 crop for faces in 8:9 cells (1280x720 frames)
    face169 = (160, 90, 1120, 630)  # centre 16:9 crop for faces in 16:9 cells (no distortion)
    sheet(os.path.join(out, "chars-01-g1-vs-g2.jpg"), "Stage chars: G1 (BEFORE) vs G2 (AFTER), same framings where G1 has them",
          [(g("avatar-turntable-yaw000"), "G1 avatar", None), (c("chars-hero-yaw000"), "G2 avatar (same camera)", None),
           (g("avatar-turntable-face-front"), "G1 face", face169), (c("chars-hero-face-g1cam"), "G2 face (same camera)", face169),
           (g("avatar-eras-lineup-military"), "G1 eras (military)", None), (c("chars-eras-lineup"), "G2 eras (same camera)", None),
           (g("units-closeup-kits-clips"), "G1 soldiers", None), (c("chars-forces-lines-34"), "G2 two forces", None)],
          cols=2, cell=(480, 270))
    sheet(os.path.join(out, "chars-02-hero-turntable.jpg"), "Player avatar (G1 PlayerFixture identity), medieval kit: turntable, 45-degree steps",
          [(c(f"chars-hero-yaw{a:03d}"), f"{a} deg", (330, 60, 950, 700)) for a in range(0, 360, 45)], cols=4, cell=(300, 310))
    sheet(os.path.join(out, "chars-03-faces.jpg"), "Player avatar: face close-ups (bare-headed), helmet, golden hour",
          [(c("chars-hero-face-front"), "front", face), (c("chars-hero-face-34"), "three-quarter", face),
           (c("chars-hero-face-side"), "profile", face), (c("chars-hero-helmet-34"), "kettle hat", face),
           (c("chars-hero-face-dusk"), "17:36 sun", face), (c("chars-forces-commander"), "force A commander", face)],
          cols=3, cell=(320, 360))
    sheet(os.path.join(out, "chars-04-expressions.jpg"), "Expressions (blend shapes from the MakeHuman CC0 face pose units)",
          [(c("chars-hero-expr-" + e), e, face) for e in ("neutral", "battlecry", "pain", "grim", "calm", "blink")],
          cols=3, cell=(320, 360))
    sheet(os.path.join(out, "chars-05-eras.jpg"), "One identity, three eras: same face, body, skin, eyes and hair; different outfits",
          [(c("chars-eras-lineup"), "line-up", None), (c("chars-eras-lineup-34"), "three-quarter", None),
           (c("chars-eras-face-ancient"), "ancient", face169), (c("chars-eras-face-medieval"), "medieval", face169),
           (c("chars-eras-face-early_modern"), "early modern", face169)], cols=2, cell=(480, 270))
    sheet(os.path.join(out, "chars-06-forces.jpg"), "Two opposing forces: force A (4 soldiers + commander) vs force B (4 soldiers), distinct identities",
          [(c("chars-forces-lines-34"), "both lines", None), (c("chars-forces-lines-low"), "low", None),
           (c("chars-forces-force-a"), "force A", None), (c("chars-forces-force-b"), "force B", None),
           (c("chars-forces-lines-dusk"), "17:36 sun", None), (c("chars-forces-commander"), "commander", None)],
          cols=2, cell=(480, 270))
    sheet(os.path.join(out, "chars-07-lod.jpg"), "LOD strip: LOD0 (every part), LOD1 (~3,000 triangles), LOD2 (~1,500), meshoptimizer v1.3",
          [(c("chars-lod-strip"), "LOD0 | LOD1 | LOD2, same distance", None), (c("chars-lod-strip-faces"), "faces", None),
           (c("chars-lod-distances"), "at 6 m, 18 m, 40 m", None)], cols=3, cell=(400, 225))
    sheet(os.path.join(out, "chars-08-anim-locomotion.jpg"), "Locomotion on a moving root: walk (6 frames of one cycle), run (4); stride-synchronised, feet locked",
          [(c(f"chars-anim-walk-{i}"), f"walk {i}/6", (360, 40, 920, 700)) for i in range(6)]
          + [(c(f"chars-anim-run-{i}"), f"run {i}/4", (360, 40, 920, 700)) for i in range(4)], cols=5, cell=(224, 266))
    sheet(os.path.join(out, "chars-09-anim-actions.jpg"), "Actions: thrust, block, additive hit, death, surrender (kneel, hands up), head look",
          [(c(f"chars-anim-{n}"), n, (280, 20, 1000, 720)) for n in
           ("thrust-a", "thrust-b", "block", "hit-0", "hit-1", "hit-2", "death-1", "death-3", "surrender-1", "surrender-2", "look-0", "look-2")],
          cols=6, cell=(200, 194))
    return 0


if __name__ == "__main__":
    sys.exit(main())
