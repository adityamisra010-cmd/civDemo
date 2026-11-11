#!/usr/bin/env bash
# Bake the stage-kit garment outfits (ancient chitons and pteruges for force A, wool tunics and trousers for force B;
# no helmets, shields or weapons: Gfx.Gear adds the era kit at runtime) into $G2_ASSETS/baked/kit/outfits.
# Usage: scripts/bake-kit-outfits.sh [--check]
# Needs the stage-chars bake (its MANIFEST gives the identities' sha256) and the same pinned inputs as bake-chars.sh.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
build="${G2_BUILD:-/home/user/g2w/build}"
"$here/fetch-assets.sh" --verify --only mh-core,mpfb2,mhjs-data,meshoptimizer >/dev/null || {
  echo "bake-kit-outfits: pinned inputs missing or changed (run scripts/fetch-assets.sh)" >&2; exit 3; }
lib="$build/meshopt/libmeshopt.so"
if [ ! -f "$lib" ]; then
  mkdir -p "$build/meshopt"
  g++ -O2 -std=c++11 -shared -fPIC -o "$lib.tmp" "$root"/raw/meshoptimizer/src/*.cpp
  mv -f "$lib.tmp" "$lib"
fi
export G2_MESHOPT_LIB="$lib"
python3 -I "$here/../tools/charbake/kitbake.py" --raw "$root/raw" --out "$root/baked/kit/outfits" "$@"
