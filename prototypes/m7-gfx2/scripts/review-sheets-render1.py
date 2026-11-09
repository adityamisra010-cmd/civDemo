#!/usr/bin/env python3
"""Compose the small labelled review JPEGs of stage render1 from full-size captures (never commits PNGs).

Usage: review-sheets-render1.py G1_DIR CAPTURE_DIR OUT_DIR
  G1_DIR       pristine G1 captures (prototypes/m7-gfx1 Gfx.Demo --capture all)
  CAPTURE_DIR  output of scripts/capture-render1.sh (g2/, g1path/, ab/<switch>/)
  OUT_DIR      where the JPEGs go (reports/captures/render1); each is kept under 300 KB
Every sheet carries a legend line: renderer llvmpipe; the props, soldiers and avatars in frame are G1's procedural
placeholders; no PROTOTYPE-ONLY asset appears in any render1 sheet.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

LEGEND = "llvmpipe software GL (not Victus evidence). Props, soldiers, avatars: G1 procedural placeholders. No PROTOTYPE-ONLY assets."


def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def panel(path, box=None, size=(640, 360), resample=Image.LANCZOS):
    im = Image.open(path).convert("RGB")
    if box:
        im = im.crop(box)
    return im.resize(size, resample)


def sheet(out, title, panels, labels, cols, cell, budget=300_000):
    w, h = cell
    rows = (len(panels) + cols - 1) // cols
    head, foot = 26, 18
    img = Image.new("RGB", (cols * w, head + rows * h + foot), (18, 18, 18))
    d = ImageDraw.Draw(img)
    d.text((8, 5), title, fill=(235, 235, 235), font=font(15))
    for i, (p, lab) in enumerate(zip(panels, labels)):
        x, y = (i % cols) * w, head + (i // cols) * h
        img.paste(p, (x, y))
        tw = d.textlength(lab, font=font(14))
        d.rectangle([x + 6, y + 6, x + 14 + tw, y + 26], fill=(0, 0, 0))
        d.text((x + 10, y + 8), lab, fill=(255, 255, 255), font=font(14))
    d.text((8, head + rows * h + 2), LEGEND, fill=(170, 170, 170), font=font(11))
    for q in (84, 78, 72, 66, 60):
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
    g2, g1p, ab = os.path.join(cap, "g2"), os.path.join(cap, "g1path"), os.path.join(cap, "ab")
    P = lambda *a: os.path.join(*a)
    pair = ["G1 (pristine build)", "G2 render1 (16-sample still)"]
    for name, shot, title in [
        ("r1-01-temperate-day", "terrain-temperate-day.png", "Temperate day: analytic sky, aerial perspective, far field to the horizon, photographic terrain"),
        ("r1-02-temperate-dusk", "terrain-temperate-dusk.png", "Temperate dusk (sun 0.9 deg): sunset sky, reddened sun, long cascaded shadows, lit haze"),
        ("r1-03-arid-day", "terrain-arid-day.png", "Arid coast: far sea and coastline beyond the 1 km map (G1: grey void)"),
        ("r1-04-battlefield-wide", "battlefield-wide.png", "Battlefield wide (230 m): photographic, hex-tiled ground; PBR shading; CSM"),
        ("r1-05-battlefield-dusk", "battlefield-dusk.png", "Battlefield 'dusk' shot (17:42, sun 11 deg, back-lit): G2 reads sky-lit and cool, not golden"),
    ]:
        sheet(P(out, name + ".jpg"), title, [panel(P(g1, shot)), panel(P(g2, shot))], pair, 2, (640, 360))
    # ground at a grazing angle, 1:1 crop
    box = (300, 520, 940, 720)
    sheet(P(out, "r1-06-ground-grazing.jpg"), "Tactical-low ground, 1:1 crop: G1 smears (no anisotropy) and its shadow edge steps; G2 photographic, x16 anisotropic, CSM",
          [panel(P(g1, "terrain-temperate-tactical-low.png"), box, (640, 200), Image.NEAREST),
           panel(P(g2, "terrain-temperate-tactical-low.png"), box, (640, 200), Image.NEAREST)], pair, 2, (640, 200))
    sheet(P(out, "r1-07-materials.jpg"), "PBR spheres (white dielectric, gold, iron; roughness 0 to 1): G1 path, G2 sky IBL, G2 CC0 HDRI IBL",
          [panel(P(g1p, "render1-materials-noon.png"), size=(560, 315)), panel(P(g2, "render1-materials-noon.png"), size=(560, 315)),
           panel(P(ab, "ibl-hdri", "render1-materials-noon.png"), size=(560, 315))],
          ["G1 frame path (Lambert)", "G2: GGX + sky IBL", "G2: GGX + HDRI IBL"], 3, (560, 315))
    box = (320, 200, 960, 560)
    sheet(P(out, "r1-08-cliff-hex.jpg"), "Rock close-up (1:1 crop): G1 path; G2 without hex-tiling (grid repeat); G2 hex-tiled",
          [panel(P(g1p, "render1-closeups-cliff.png"), box, (426, 240)), panel(P(ab, "hex-0", "render1-closeups-cliff.png"), box, (426, 240)),
           panel(P(ab, "g2-fxaa", "render1-closeups-cliff.png"), box, (426, 240))],
          ["G1 frame path", "G2 hex=0", "G2 hex-tiled"], 3, (426, 240))
    box = (470, 120, 790, 330)
    sheet(P(out, "r1-09-anti-aliasing.jpg"), "Anti-aliasing, 2x crop of the follow shot: G1; G2 no AA; G2 FXAA (interactive); G2 16-sample jittered still",
          [panel(P(g1, "battlefield-follow-low.png"), box, (480, 315), Image.NEAREST),
           panel(P(ab, "aa-none", "battlefield-follow-low.png"), box, (480, 315), Image.NEAREST),
           panel(P(ab, "g2-fxaa", "battlefield-follow-low.png"), box, (480, 315), Image.NEAREST),
           panel(P(g2, "battlefield-follow-low.png"), box, (480, 315), Image.NEAREST)],
          ["G1", "G2 aa=none", "G2 FXAA", "G2 16x accumulation"], 2, (480, 315))
    sheet(P(out, "r1-10-tonemappers.jpg"), "Tonemappers on the dusk battlefield: ACES fit (G1's), ACES (Hill), AgX, PBR Neutral (G2 default)",
          [panel(P(ab, f"tonemap-{t}", "battlefield-dusk.png"), size=(480, 270)) for t in ("acesfit", "aces", "agx")]
          + [panel(P(ab, "g2-fxaa", "battlefield-dusk.png"), size=(480, 270))],
          ["ACES fit (G1)", "ACES (Hill)", "AgX", "PBR Neutral"], 2, (480, 270))
    sheet(P(out, "r1-11-coast.jpg"), "Coast from 75 m: G1 path (sea stops at the map edge); G2 with unfiltered ripples; G2 (distance-filtered ripples, far sea)",
          [panel(P(g1p, "render1-coast-coast-high.png"), size=(426, 240)), panel(P(ab, "ripplefade-0", "render1-coast-coast-high.png"), size=(426, 240)),
           panel(P(g2, "render1-coast-coast-high.png"), size=(426, 240))],
          ["G1 frame path", "G2 ripplefade=0", "G2"], 3, (426, 240))
    sheet(P(out, "r1-12-ground-closeups.jpg"), "Meadow at a grazing angle and the gravel track: G1 path vs G2 (photographic sets, x16 anisotropic, hex-tiled)",
          [panel(P(g1p, "render1-closeups-meadow-grazing.png"), size=(480, 270)), panel(P(g2, "render1-closeups-meadow-grazing.png"), size=(480, 270)),
           panel(P(g1p, "render1-closeups-gravel-track.png"), size=(480, 270)), panel(P(g2, "render1-closeups-gravel-track.png"), size=(480, 270))],
          ["G1 frame path", "G2", "G1 frame path", "G2"], 2, (480, 270))
    sheet(P(out, "r1-13-atmosphere-ab.jpg"), "Atmosphere switches on the temperate day shot: far field off; aerial perspective off; legacy sky; all on",
          [panel(P(ab, "farfield-0", "terrain-temperate-day.png"), size=(480, 270)), panel(P(ab, "aerial-0", "terrain-temperate-day.png"), size=(480, 270)),
           panel(P(ab, "sky-legacy", "terrain-temperate-day.png"), size=(480, 270)), panel(P(ab, "g2-fxaa", "terrain-temperate-day.png"), size=(480, 270))],
          ["G2 farfield=0", "G2 aerial=0", "G2 sky=legacy", "G2 all on"], 2, (480, 270))
    sheet(P(out, "r1-14-dusk-shadows.jpg"), "Temperate dusk: one 2048 map around the target (G1 technique) vs four cascades reaching every caster up-sun",
          [panel(P(ab, "shadows-single", "terrain-temperate-dusk.png")), panel(P(ab, "g2-fxaa", "terrain-temperate-dusk.png"))],
          ["G2 shadows=single", "G2 cascaded"], 2, (640, 360))
    sheet(P(out, "r1-15-post-ab.jpg"), "Post switches, one off (top) vs G2 (bottom): auto exposure on the dusk shot, bloom on golden-hour spheres, 3D LUT grade",
          [panel(P(ab, "autoexposure-0", "terrain-temperate-dusk.png"), size=(426, 240)), panel(P(ab, "bloom-0", "render1-materials-golden.png"), size=(426, 240)),
           panel(P(ab, "grade-0", "terrain-temperate-day.png"), size=(426, 240)),
           panel(P(ab, "g2-fxaa", "terrain-temperate-dusk.png"), size=(426, 240)), panel(P(ab, "g2-fxaa", "render1-materials-golden.png"), size=(426, 240)),
           panel(P(ab, "g2-fxaa", "terrain-temperate-day.png"), size=(426, 240))],
          ["autoexposure=0 (manual)", "bloom=0", "grade=0", "G2 (auto exposure)", "G2 (bloom)", "G2 (grade)"], 3, (426, 240))
    box = (320, 200, 960, 560)
    sheet(P(out, "r1-16-terrain-ab.jpg"), "Terrain switches, grazing meadow crop (top) and tactical-low (bottom): G1 materials with G2 light; aniso=1 / macro=0; G2",
          [panel(P(ab, "terrain-procedural", "render1-closeups-meadow-grazing.png"), box, (426, 240)),
           panel(P(ab, "aniso-1", "render1-closeups-meadow-grazing.png"), box, (426, 240)),
           panel(P(ab, "g2-fxaa", "render1-closeups-meadow-grazing.png"), box, (426, 240)),
           panel(P(ab, "terrain-procedural", "terrain-temperate-tactical-low.png"), size=(426, 240)),
           panel(P(ab, "macro-0", "terrain-temperate-tactical-low.png"), size=(426, 240)),
           panel(P(ab, "g2-fxaa", "terrain-temperate-tactical-low.png"), size=(426, 240))],
          ["terrain=procedural", "aniso=1", "G2 (aniso x16)", "terrain=procedural", "macro=0", "G2 (macro variation)"], 3, (426, 240))
    return 0


if __name__ == "__main__":
    sys.exit(main())
