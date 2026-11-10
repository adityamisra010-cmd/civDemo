#!/usr/bin/env python3
"""Stage chars, one-off review sheet: the defects this run found in its own first two capture rounds, next to the
same frames from the final bake (reports/chars.md §3 and §4 say which bakes and commits made each round). Every
folder holds the five scenes of scripts/capture-chars.sh.

Usage: review-sheet-chars-fixes.py ROUND1_DIR ROUND2_DIR FINAL_DIR OUT_JPG   (layout and legend style from
review-sheets-chars.py)"""
import importlib.util
import os
import sys

spec = importlib.util.spec_from_file_location("rs", os.path.join(os.path.dirname(os.path.abspath(__file__)), "review-sheets-chars.py"))
rs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rs)
b9, b10, fin, out = sys.argv[1:5]


def p(d, n):
    return f"{d}/{n}.png"


items = [
    (p(b9, "chars-eras-face-ancient"), "round 1: cuirass and helmet edges", (240, 0, 1040, 720)),
    (p(fin, "chars-eras-face-ancient"), "final: same frame", (240, 0, 1040, 720)),
    (p(b9, "chars-eras-lineup-34"), "round 1: boot shaft through hose", (826, 380, 1134, 655)),
    (p(fin, "chars-eras-lineup-34"), "final: same frame", (826, 380, 1134, 655)),
    (p(b9, "chars-eras-face-early_modern"), "round 1: shirt pinholes", (442, 330, 878, 720)),
    (p(fin, "chars-eras-face-early_modern"), "final: same frame", (442, 330, 878, 720)),
    (p(b9, "chars-forces-force-a"), "round 1: mail through surcoat", (600, 470, 869, 710)),
    (p(fin, "chars-forces-force-a"), "final: same frame", (600, 470, 869, 710)),
    (p(b10, "chars-forces-force-b"), "round 2: hose through skirt", (1060, 370, 1280, 567)),
    (p(fin, "chars-forces-force-b"), "final: same frame", (1060, 370, 1280, 567)),
]
legend = ("llvmpipe software GL, not Victus evidence. PROTOTYPE-ONLY in frame: CMU motion capture poses (provisional terms). "
          "Defects found in this run's own first two capture rounds (report section 4), and the same frames after the fixes.")
rs.sheet(out, "Stage chars: defects found in this run's own first capture rounds, and the same frames from the final bake",
         items, cols=4, cell=(280, 250), legend=legend)
