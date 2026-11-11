#!/usr/bin/env bash
# Bake the crowd's third level of detail (stage render2) from the stage-chars bake into $G2_ASSETS/baked/crowd, or
# check an existing bake byte for byte.
#
# Usage: scripts/bake-crowd.sh [--check]
#
# Needs the chars bake ($G2_ASSETS/baked/chars, scripts/bake-chars.sh) and the meshoptimizer v1.3 library that
# scripts/bake-chars.sh builds from the pinned sources (built here the same way when missing; a bake tool only,
# nothing of it ships). Runs tools/crowdbake/crowdbake.py (python3 with numpy). Outputs are never committed;
# MANIFEST.tsv in the output folder lists each with its sha256.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
build="${G2_BUILD:-/home/user/g2w/build}"
chars="$root/baked/chars"
[ -f "$chars/a1__kit_a.g2ch" ] || { echo "bake-crowd: no chars bake in $chars (run scripts/bake-chars.sh)" >&2; exit 3; }
lib="$build/meshopt/libmeshopt.so"
if [ ! -f "$lib" ]; then
  "$here/fetch-assets.sh" --verify --only meshoptimizer >/dev/null || {
    echo "bake-crowd: pinned meshoptimizer sources missing or changed (run scripts/fetch-assets.sh)" >&2; exit 3; }
  mkdir -p "$build/meshopt"
  g++ -O2 -std=c++11 -shared -fPIC -o "$lib.tmp" "$root"/raw/meshoptimizer/src/*.cpp
  mv -f "$lib.tmp" "$lib"
fi
export G2_MESHOPT_LIB="$lib"
python3 -I "$here/../tools/crowdbake/crowdbake.py" --chars "$chars" --out "$root/baked/crowd" "$@"
