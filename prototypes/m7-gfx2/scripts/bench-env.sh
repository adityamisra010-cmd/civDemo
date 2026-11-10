#!/usr/bin/env bash
# Per-feature cost of the environment stage: p50/p95 frame time, draw calls and triangles with every switch on, with
# the environment off (env=0: the G1 content in the G2 frame), through the G1 preset, and with one environment switch
# off at a time, on four shots; plus a per-pass profile of the full frame and the scene load costs.
#
# Usage: scripts/bench-env.sh OUT_DIR [FRAMES]     (needs a Release build of M7Gfx2.slnx and scripts/bake-env.sh)
#
# Numbers measured in a container without a GPU are llvmpipe (software GL) numbers: NOT Victus evidence (D-078 QM9).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: bench-env.sh OUT_DIR [FRAMES]}"
frames="${2:-10}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "bench-env: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
mkdir -p "$out"
tsv="$out/bench-env.tsv"
printf 'scene\tshot\tvariant\tp50_ms\tp95_ms\tdraws\ttris\ttrees_full\ttrees_reduced\ttrees_impostor\tcover_cards\n' > "$tsv"
run() { # scene shot variants
  local scene=$1 shot=$2 variants=$3
  local log; log="$out/bench-$scene-$shot.log"
  nice -n 10 dotnet "$demo" --bench "$scene" --shot "$shot" --frames "$frames" --size 1280x720 --variants "$variants" > "$log" 2>&1
  awk -v scene="$scene" -v shot="$shot" -F'\n' '
    /^\[variant\]/ { v = substr($0, 11) }
    /^\[SOFTWARE|^\[GPU/ {
      p50 = $0; sub(/.*p50=/, "", p50); sub(/[^0-9.].*/, "", p50)
      p95 = $0; sub(/.*p95=/, "", p95); sub(/[^0-9.].*/, "", p95)
      dr = $0; sub(/.*draws=/, "", dr); sub(/[^0-9].*/, "", dr)
      tr = $0; sub(/.*tris=/, "", tr); sub(/[^0-9].*/, "", tr)
    }
    /^\[env\] last frame/ {
      f = $0; sub(/.*trees full /, "", f); sub(/,.*/, "", f)
      r = $0; sub(/.*reduced /, "", r); sub(/,.*/, "", r)
      i = $0; sub(/.*impostor /, "", i); sub(/ .*/, "", i)
      c = $0; sub(/.*cover cards /, "", c); sub(/ .*/, "", c)
      printf "%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n", scene, shot, v, p50, p95, dr, tr, f, r, i, c
    }' "$log" | tee -a "$tsv"
}
common="g2;env=0;g1;trees=0;impostors=0;lodfade=0;wind=0;groundcover=0;rocks=0;forestfloor=0"
run env-temperate forest-edge "$common"
run env-temperate river "g2;env=0;g1;water=0;reflect=0;groundcover=0;rocks=0;trees=0"
run env-temperate vista "$common;structures=0"
run env-arid fortification-gate "g2;env=0;g1;structures=0;trees=0;groundcover=0;rocks=0"
# per-pass profile of the full frame (one variant, the deliverable settings)
nice -n 10 dotnet "$demo" --bench env-temperate --shot forest-edge --frames "$frames" --size 1280x720 --profile 2>&1 \
  | grep -E '^\[pass\]|^\[bench\]|^\[load\]|^\[env\]|^\[SOFTWARE|^\[GPU' > "$out/profile-env-temperate-forest-edge.txt"
grep -h -E '^\[load\]|^\[env\] load' "$out"/bench-*.log | sort -u > "$out/load-env.txt"
echo "bench-env: wrote $tsv (llvmpipe numbers when run without a GPU: not Victus evidence)"
