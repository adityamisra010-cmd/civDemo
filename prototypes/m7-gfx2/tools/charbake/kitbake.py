#!/usr/bin/env python3
"""kitbake (stage kit): bake only the stage-kit garment outfits (recipes.soldier_ancient) for existing identities into
a separate folder (default $G2_ASSETS/baked/kit/outfits), without writing any stage-chars output.

Usage: kitbake.py [--raw DIR] [--out DIR] [--check]
Each outfit file (<identity>__anc_a.g2ch / __anc_b.g2ch) is built exactly as charbake builds outfits (bakeall.bake_outfit:
garment shells, visible-skin view, LODs by meshoptimizer); the identity is rebuilt in memory (body, eyes, hair parts; no
skin textures, no clips) only to fit the garments and the LODs. The identity file's sha256 recorded in each outfit is
read from the stage-chars bake's MANIFEST (the in-memory identity is not written). Deterministic like charbake.
"""
import os

for _k in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS"):
    os.environ.setdefault(_k, "1")

import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import argparse  # noqa: E402

import numpy as np  # noqa: E402

import bakeall  # noqa: E402
import charbake  # noqa: E402
import hair  # noqa: E402
import materials  # noqa: E402
import recipes  # noqa: E402
import skin  # noqa: E402
import teeth  # noqa: E402
import texio  # noqa: E402
from fields import Fields  # noqa: E402
from identities import IDENTITIES  # noqa: E402
from identity import Assets, Identity  # noqa: E402

KINDS = {"a1": "hoplite", "a2": "hoplite", "a3": "hoplite", "a4": "hoplite", "commander_a": "officer",
         "b1": "warrior", "b2": "warrior", "b3": "warrior", "b4": "warrior"}


def built_parts(spec, A):
    """The identity's meshes the outfit bake needs (body, eyes, hair parts), as bake_identity builds them."""
    ident = Identity(spec, A)
    body = ident.body_mesh()
    fl = Fields(ident, skin.fields_masks(A.raw, 1024))
    surf = hair.Surface(ident)
    eye_rgb, eye_a = skin.eye_texture(A.raw, spec["eyes"])
    eyes = ident.eye_meshes(eye_a)
    rng = np.random.default_rng(1000 + spec["seed"])
    parts = hair.scalp_shells(ident, fl, surf, spec["hair"])
    if spec["hair"]["style"] == "tied_back":
        parts.append(hair.ponytail(ident, surf, hair.tie_point(fl), rng, 0))
    elif spec["hair"]["style"] == "medium":
        for mesh, morphs in hair.scalp_hair(ident, fl, surf, spec["hair"], rng):
            mesh["name"] = "hair_long"
            parts.append((mesh, morphs))
    parts += hair.beard_fur(ident, fl, surf, spec.get("beard"))
    parts += hair.brows(ident, fl, surf, rng, spec.get("brow_density", 1.0))
    parts += hair.lashes(ident)
    built = {"body": body, "eyes": eyes[0]}
    for mesh, morphs in parts:
        built[mesh["name"]] = mesh
    _ = teeth
    return ident, built


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    root = os.environ.get("G2_ASSETS", "/home/user/g2w/assets")
    ap.add_argument("--raw", default=os.path.join(root, "raw"))
    ap.add_argument("--out", default=os.path.join(root, "baked", "kit", "outfits"))
    ap.add_argument("--chars", default=os.path.join(root, "baked", "chars"))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    shas = {}
    with open(os.path.join(a.chars, "MANIFEST.tsv"), encoding="utf-8") as f:
        for line in f:
            if line.startswith("#") or not line.strip():
                continue
            rel, sha, _ = line.rstrip("\n").split("\t")
            shas[rel] = sha
    A = Assets(a.raw)
    files = {"tex/fabric.r1tx": texio.r1tx_bytes(materials.fabric()), "tex/wood.r1tx": texio.r1tx_bytes(materials.wood()),
             "tex/trim.r1tx": texio.r1tx_bytes(materials.trim_patterns())}
    for spec in IDENTITIES:
        if spec["id"] not in KINDS:
            continue
        spec2 = bakeall.solve_stature(spec, A)
        ident, built = built_parts(spec2, A)
        force = recipes.FORCE_A if spec["id"].startswith(("a", "commander")) else recipes.FORCE_B
        oid = "anc_a" if force is recipes.FORCE_A else "anc_b"
        kind = KINDS[spec["id"]]
        outfit = {"id": oid, "label": f"Force {force['id']} (ancient, stage kit): {kind} garments", "force": force["id"], "era": "ancient",
                  "textures": [], "recipe": (lambda b, f=force, k=kind, sd=spec["seed"]: recipes.soldier_ancient(b, f, k, seed=sd))}
        bakeall.bake_outfit(spec2, outfit, ident, built, shas[f"{spec['id']}.g2ch"], files)
        print(f"kitbake: {spec['id']}__{oid}", flush=True)
    if not a.check:
        rows = charbake.write_outputs(a.out, files)
        print(f"kitbake: wrote {len(rows)} files to {a.out}")
        return 0
    bad = 0
    for rel in sorted(files):
        p = os.path.join(a.out, rel)
        if not os.path.exists(p) or open(p, "rb").read() != files[rel]:
            print(f"DIFFERS  {rel}")
            bad += 1
    print(f"kitbake --check: {len(files)} files compared, {bad} differ")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
