#!/usr/bin/env python3
"""charbake: bake the G2 character identities and outfits (stage chars) from pinned CC0 sources.

Usage:
  charbake.py [--raw DIR] [--out DIR] [--only ID[,ID...]]      bake into OUT (default $G2_ASSETS/baked/chars)
  charbake.py --check [--raw DIR] [--out DIR]                     re-bake into a temporary folder and compare every
                                                                  output byte for byte with OUT (exit 1 on any difference)
Inputs: $G2_ASSETS/raw (MakeHuman hm08 core, MPFB2 rigs, mhjs CC0 skins, CMU clips), checked against
assets/manifest.tsv by scripts/bake-chars.sh. Outputs are never committed; MANIFEST.tsv lists each with its sha256.
Deterministic: single-threaded numpy, sorted iteration, integer-hash noise, canonical JSON.
"""
import os

for _k in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS"):
    os.environ.setdefault(_k, "1")

import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import argparse  # noqa: E402
import hashlib  # noqa: E402
import shutil  # noqa: E402

import numpy as np  # noqa: E402

import bakeall  # noqa: E402


def sha256(b):
    return hashlib.sha256(b).hexdigest()


def write_outputs(outdir, files):
    os.makedirs(outdir, exist_ok=True)
    rows = []
    for rel in sorted(files):
        data = files[rel]
        p = os.path.join(outdir, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "wb") as f:
            f.write(data)
        rows.append(f"{rel}\t{sha256(data)}\t{len(data)}")
    manifest = "# path\tsha256\tbytes\n" + "\n".join(rows) + "\n"
    manifest += f"# numpy {np.__version__}\n"
    with open(os.path.join(outdir, "MANIFEST.tsv"), "w", encoding="utf-8") as f:
        f.write(manifest)
    return rows


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    root = os.environ.get("G2_ASSETS", "/home/user/g2w/assets")
    ap.add_argument("--raw", default=os.path.join(root, "raw"))
    ap.add_argument("--out", default=os.path.join(root, "baked", "chars"))
    ap.add_argument("--only", default="")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    only = [x for x in a.only.split(",") if x]
    files = bakeall.bake(a.raw, only)
    if not a.check:
        rows = write_outputs(a.out, files)
        print(f"charbake: wrote {len(rows)} files to {a.out}")
        return 0
    # --check: compare every output with the existing bake
    bad = 0
    for rel in sorted(files):
        p = os.path.join(a.out, rel)
        if not os.path.exists(p):
            print(f"MISSING  {rel}")
            bad += 1
            continue
        with open(p, "rb") as f:
            old = f.read()
        if old != files[rel]:
            print(f"DIFFERS  {rel}  ({len(old)} vs {len(files[rel])} bytes)")
            bad += 1
    print(f"charbake --check: {len(files)} files compared, {bad} differ")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
