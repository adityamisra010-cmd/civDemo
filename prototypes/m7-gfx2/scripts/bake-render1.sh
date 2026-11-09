#!/usr/bin/env bash
# Bake the render1 (renderer core) assets from the pinned raw files into $G2_ASSETS/baked/render1.
#
# Usage: scripts/bake-render1.sh            (verifies the raw files it reads first, then bakes)
#
# Inputs (all pinned in assets/manifest.tsv, SHIP-OK): Terrain3D Ground037 and Rock023 (CC0 via MIT repo),
# BabylonJS rockyGround, sand and dirt (CC-BY-4.0). Outputs are never committed; see assets/PROVENANCE.md
# ("render1 bake") for every transformation. Needs python3 with numpy and Pillow.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
raw="$root/raw"
out="$root/baked/render1/terrain"
inputs=(
  terrain3d/project/demo/assets/textures/ground037_alb_ht.png
  terrain3d/project/demo/assets/textures/ground037_nrm_rgh.png
  terrain3d/project/demo/assets/textures/rock023_alb_ht.png
  terrain3d/project/demo/assets/textures/rock023_nrm_rgh.png
  babylon-assets/textures/rockyGround_basecolor.png
  babylon-assets/textures/rockyGround_normal.png
  babylon-assets/textures/sand.jpg
  babylon-assets/textures/dirt.jpg
)
manifest="$here/../assets/manifest.tsv"
for f in "${inputs[@]}"; do
  want="$(awk -F'\t' -v d="$f" '$1 == d { print $5 }' "$manifest")"
  [ -n "$want" ] || { echo "bake-render1: $f is not pinned in assets/manifest.tsv" >&2; exit 1; }
  [ -f "$raw/$f" ] || { echo "bake-render1: missing $raw/$f (run scripts/fetch-assets.sh)" >&2; exit 3; }
  have="$(sha256sum "$raw/$f" | cut -d' ' -f1)"
  [ "$have" = "$want" ] || { echo "bake-render1: sha256 mismatch for $f" >&2; exit 1; }
done
mkdir -p "$out"
python3 -I "$here/bake-render1-terrain.py" "$raw" "$out"
echo "bake-render1: wrote $out"
