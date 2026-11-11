#!/usr/bin/env python3
"""Compose the small labelled review JPEGs of stage kit from full-size captures (never commits PNGs).

Usage: review-sheets-kit.py S16_DIR CAPTURE_DIR OUT_DIR
  S16_DIR      16-sample stills of the review shots (Gfx.Demo --capture <scene> --set samples=16 --shots ...):
               kit-hero-*, kit-forces-*, kit-medieval-*, kit-gallery-*
  CAPTURE_DIR  whole-scene captures at the default single sample + FXAA (Gfx.Demo --capture kit-combat)
  OUT_DIR      where the JPEGs go (reports/captures/kit); each is kept under 200 KB
Every sheet carries a legend: renderer llvmpipe; which tiles show the one PROTOTYPE-ONLY input (CMU motion capture,
assets/PROVENANCE.md 3.4 and 3.11); everything else is CC0 (MakeHuman, MPFB2), CC-BY/CC0+MIT (render1 ground) or
generated in code.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

BASE = "llvmpipe software GL, not Victus evidence."
GROUND = "Ground: render1 terrain bake (CC0+MIT, CC-BY-4.0)."


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


def sheet(out, title, items, cols, cell, legend, budget=200_000):
    """items: (path, label, crop box or None)."""
    w, h = cell
    rows = (len(items) + cols - 1) // cols
    head, foot = 24, 44
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
    for k, ln in enumerate(lines[:3]):
        d.text((8, head + rows * h + 3 + 13 * k), ln, fill=(170, 170, 170), font=font(10))
    for q in (85, 80, 75, 70, 64, 58, 52):
        img.save(out, quality=q, optimize=True, progressive=True)
        if os.path.getsize(out) <= budget:
            break
    print(f"{os.path.basename(out)}: {os.path.getsize(out)} bytes")


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    s16, cap, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)

    def s(name):
        return os.path.join(s16, name + ".png")

    def c(name):
        return os.path.join(cap, name + ".png")

    sheet(os.path.join(out, "kit-01-hero-before-after.jpg"),
          "Hero asset, BEFORE (stage-chars aspis) vs AFTER (generated aspis, glTF bake, 2,048-texel maps): same cameras",
          [(s("kit-hero-before-face"), "BEFORE face", None), (s("kit-hero-face"), "AFTER face", None),
           (s("kit-hero-before-turn045"), "BEFORE 45 deg", None), (s("kit-hero-turn045"), "AFTER 45 deg", None),
           (s("kit-hero-before-back"), "BEFORE back", None), (s("kit-hero-back"), "AFTER back", None),
           (s("kit-hero-held-before"), "BEFORE held (avatar)", None), (s("kit-hero-held-after"), "AFTER held (avatar)", None)],
          cols=2, cell=(480, 270),
          legend=BASE + " 16-sample stills. PROTOTYPE-ONLY in frame: CMU motion capture idle posing the avatar in the two"
          " 'held' tiles (provisional terms). Both aspides generated in code (project-owned); avatar body CC0 MakeHuman/MPFB2,"
          " garments generated. " + GROUND)
    sheet(os.path.join(out, "kit-02-hero-closeups.jpg"),
          "Hero aspis close-ups: guilloche rim and bead, painted device with chips and scratches, porpax and antilabe, late sun",
          [(s("kit-hero-rim"), "rim", None), (s("kit-hero-device"), "device", None),
           (s("kit-hero-fittings"), "fittings (inside)", None), (s("kit-hero-dusk"), "17:36 sun", None)],
          cols=2, cell=(480, 270),
          legend=BASE + " 16-sample stills. No PROTOTYPE-ONLY input in frame: the aspis is generated in code (geometry and"
          " maps, no external file). " + GROUND)
    sheet(os.path.join(out, "kit-03-gallery.jpg"),
          "Era kit generated in code: shields (thureos, round, kite, heater), helmets, swords, axe, spearhead",
          [(s("kit-gallery-shields"), "shields", None), (s("kit-gallery-shields-back"), "shields, backs", None),
           (s("kit-gallery-helmet0"), "Corinthian", None), (s("kit-gallery-helmet1"), "Montefortino", None),
           (s("kit-gallery-helmet2"), "nasal", None), (s("kit-gallery-helmet3"), "kettle hat", None),
           (s("kit-gallery-helmet4"), "great helm", None), (s("kit-gallery-hilts"), "xiphos, long sword, arming sword", None),
           (s("kit-gallery-axe"), "axe", None), (s("kit-gallery-spearhead"), "spearhead", None)],
          cols=5, cell=(288, 162),
          legend=BASE + " 16-sample stills. No PROTOTYPE-ONLY input in frame: every item, post and rack is generated in"
          " code (Gfx.Gear). " + GROUND)
    sheet(os.path.join(out, "kit-04-forces.jpg"),
          "Two forces fully kitted: hoplites (aspides, Corinthian helmets, spears overhand) vs Celtic warriors (thureoi, round shield, Montefortino)",
          [(s("kit-forces-lines-34"), "both lines", None), (s("kit-forces-lines-low"), "low", None),
           (s("kit-forces-force-a"), "force A", None), (s("kit-forces-force-b"), "force B (central grips)", None),
           (s("kit-forces-commander"), "commander (hero aspis)", None), (s("kit-forces-lines-dusk"), "17:36 sun", None)],
          cols=2, cell=(480, 270),
          legend=BASE + " 16-sample stills. No PROTOTYPE-ONLY input in frame: poses are authored clips (project code) from the"
          " first animator step. Bodies CC0 MakeHuman/MPFB2; garments from the kit outfit bake (generated); helmets, shields,"
          " weapons and standards generated in code. " + GROUND)
    sheet(os.path.join(out, "kit-05-combat.jpg"),
          "Combat animation authored in code: thrust, block, hit, victory; overhead and side cuts, forward death; bow; surrender; backward death; walk",
          [(c("kit-combat-hoplite-thrust-wind"), "thrust: wind", None), (c("kit-combat-hoplite-thrust-strike"), "thrust: strike", None),
           (c("kit-combat-hoplite-block"), "block", None), (c("kit-combat-hoplite-hit"), "hit", None),
           (c("kit-combat-hoplite-victory"), "victory", None),
           (c("kit-combat-sword-over-raise"), "overhead: raise", None), (c("kit-combat-sword-over-strike"), "overhead: strike", None),
           (c("kit-combat-sword-side-wind"), "side cut: wind", None), (c("kit-combat-sword-side-strike"), "side cut: strike", None),
           (c("kit-combat-sword-death-2"), "forward death", None),
           (c("kit-combat-bow-draw"), "bow: draw", None), (c("kit-combat-bow-hold"), "bow: hold", None),
           (c("kit-combat-bow-after"), "bow: loosed", None), (c("kit-combat-surrender-drop"), "surrender: drop", None),
           (c("kit-combat-surrender-hands"), "surrender: hands", None),
           (c("kit-combat-deathback-buckle"), "backward death: hit", None), (c("kit-combat-deathback-fall"), "falling", None),
           (c("kit-combat-deathback-down"), "down", None), (c("kit-combat-walk-0"), "walk + guard layer", None),
           (c("kit-combat-walk-2"), "walk + guard layer", None)],
          cols=5, cell=(288, 162),
          legend=BASE + " Single-sample stills with FXAA. PROTOTYPE-ONLY in frame: CMU motion capture in the two 'walk' tiles"
          " (the walk cycle under the authored guard on the upper body); every other pose is an authored clip (project code)."
          " Bodies CC0 MakeHuman/MPFB2; garments and kit generated. " + GROUND)
    sheet(os.path.join(out, "kit-06-medieval.jpg"),
          "Later forms on the stage-chars medieval soldiers: great helm, nasal helmets, kettle hats, kite and heater shields, arming swords, axe, bow",
          [(s("kit-medieval-lines-34"), "both lines", None), (s("kit-medieval-force-a"), "force A", None),
           (s("kit-medieval-force-b"), "force B", None), (s("kit-medieval-face-commander_a"), "commander (great helm)", None),
           (s("kit-medieval-face-a2"), "nasal helmet, kite", None), (s("kit-medieval-face-b3"), "kettle hat, heater, axe", None)],
          cols=3, cell=(400, 225),
          legend=BASE + " 16-sample stills. No PROTOTYPE-ONLY input in frame: poses are authored clips (project code). Bodies"
          " CC0 MakeHuman/MPFB2; garments from the stage-chars bake (generated); helmets, shields, weapons and standards"
          " generated in code. " + GROUND)
    return 0


if __name__ == "__main__":
    sys.exit(main())
