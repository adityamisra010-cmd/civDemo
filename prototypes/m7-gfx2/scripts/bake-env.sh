#!/usr/bin/env bash
# Bake the environment-stage materials (trees, ground cover, water, structures) from the pinned raw files into
# $G2_ASSETS/baked/env.
#
# Usage: scripts/bake-env.sh [section ...]     (sections: bark leaves cover water struct; default all)
#
# Inputs (all pinned in assets/manifest.tsv, SHIP-OK): six ambientCG bark sets (CC0, via the MIT ez-tree repository),
# four ez-tree leaf cards (MIT), Terrain3D Rock023 (CC0 via the MIT repository). The ground-cover atlas, the water
# normal map and the drawn building materials have no external input. Outputs are never committed; every
# transformation is listed in assets/PROVENANCE.md ("env bake"). Needs python3 with numpy and Pillow.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
raw="$root/raw"
out="$root/baked/env"
ez=ez-tree/src/app/public/textures
inputs=()
for b in Bark001 Bark002 Bark004 Bark007 Bark012 Bark014; do
  for m in Color NormalGL Roughness; do inputs+=("$ez/bark/${b}_1K-JPG/${b}_1K-JPG_$m.jpg"); done
done
for l in oak ash aspen pine; do inputs+=("$ez/leaves/$l.png"); done
inputs+=(terrain3d/project/demo/assets/textures/rock023_alb_ht.png)
manifest="$here/../assets/manifest.tsv"
for f in "${inputs[@]}"; do
  want="$(awk -F'\t' -v d="$f" '$1 == d { print $5 }' "$manifest")"
  [ -n "$want" ] || { echo "bake-env: $f is not pinned in assets/manifest.tsv" >&2; exit 1; }
  [ -f "$raw/$f" ] || { echo "bake-env: missing $raw/$f (run scripts/fetch-assets.sh)" >&2; exit 3; }
  have="$(sha256sum "$raw/$f" | cut -d' ' -f1)"
  [ "$have" = "$want" ] || { echo "bake-env: sha256 mismatch for $f" >&2; exit 1; }
done
mkdir -p "$out"
python3 -I "$here/bake-env.py" "$raw" "$out" "$@"
echo "bake-env: wrote $out"
