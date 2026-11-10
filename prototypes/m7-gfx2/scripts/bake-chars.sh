#!/usr/bin/env bash
# Bake the stage-chars characters (identities, outfits, LODs, clips, textures) from the pinned raw files into
# $G2_ASSETS/baked/chars, or check an existing bake byte for byte.
#
# Usage: scripts/bake-chars.sh [--check] [--only ID[,ID...]]
#
# 1. Verifies every source the bake reads against assets/manifest.tsv (scripts/fetch-assets.sh --verify): MakeHuman
#    hm08 core and targets, MPFB2 rigs and masks, the mhjs-data CC0 skins, the CMU clips, meshoptimizer v1.3.
# 2. Builds meshoptimizer v1.3 (MIT, pinned sources) into $G2_BUILD/meshopt/libmeshopt.so when missing: a bake tool
#    only (LOD simplification), nothing of it ships.
# 3. Runs tools/charbake/charbake.py (python3 with numpy and Pillow). --check re-bakes in memory and compares every
#    output with the existing bake (exit 1 on any difference).
# Outputs are never committed; MANIFEST.tsv in the output folder lists each with its sha256.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
build="${G2_BUILD:-/home/user/g2w/build}"
"$here/fetch-assets.sh" --verify --only mh-core,mpfb2,mhjs-data,cmu-bvh,meshoptimizer >/dev/null || {
  echo "bake-chars: pinned inputs missing or changed (run scripts/fetch-assets.sh)" >&2; exit 3; }
lib="$build/meshopt/libmeshopt.so"
if [ ! -f "$lib" ]; then
  mkdir -p "$build/meshopt"
  g++ -O2 -std=c++11 -shared -fPIC -o "$lib.tmp" "$root"/raw/meshoptimizer/src/*.cpp
  mv -f "$lib.tmp" "$lib"
fi
export G2_MESHOPT_LIB="$lib"
python3 -I "$here/../tools/charbake/charbake.py" --raw "$root/raw" --out "$root/baked/chars" "$@"
