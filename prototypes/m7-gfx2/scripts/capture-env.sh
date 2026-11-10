#!/usr/bin/env bash
# The environment-stage capture set: G2 deliverable stills of every environment shot (16-sample jittered
# accumulation), the BEFORE frames of the same cameras (the G2 frame with every environment switch off, i.e. the G1
# content trunk draws, and the G1 preset), and per-feature A/B pairs (one switch off, one-sample FXAA frames).
#
# Usage: scripts/capture-env.sh OUT_DIR [SAMPLES]     (needs a Release build of M7Gfx2.slnx and scripts/bake-env.sh)
#
# Pristine G1 (prototypes/m7-gfx1) has no environment scenes; "before-g1" is these cameras through the G1 preset
# (G1 frame path, G1 content). Renderer here: llvmpipe software GL, so frame times are NOT Victus evidence.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: capture-env.sh OUT_DIR [SAMPLES]}"
samples="${2:-16}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "capture-env: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
cap() { # dir scene [args...]
  local dir=$1 scene=$2; shift 2
  mkdir -p "$out/$dir"
  nice -n 10 dotnet "$demo" --capture "$scene" --out "$out/$dir" --size 1280x720 "$@" 2>&1 \
    | grep -E '^\[capture\] /|^\[load\]|^\[env\]|rror|xception' | sed "s|$out/||" | tee -a "$out/capture-env.log" || true
}
scenes=(env-trees env-temperate env-arid)
# 1. G2 deliverable stills (16 jittered samples, no FXAA)
for s in "${scenes[@]}"; do cap final "$s" --set "samples=$samples"; done
# 2. BEFORE: the same cameras with the environment off (the G1 content in the G2 frame), and through the G1 preset
for s in "${scenes[@]}"; do cap before-g2-env-off "$s" --set "samples=$samples,env=0"; done
for s in "${scenes[@]}"; do cap before-g1-preset "$s" --preset g1; done
# 3. per-feature A/B (one switch off; one-sample FXAA frames, the 'on' frame is ab/on)
ab() { local name=$1 scene=$2 shots=$3; shift 3; cap "ab/$name" "$scene" --shots "$shots" "$@"; }
ab on env-temperate forest-edge,vista,river,river-high,settlement
ab on env-arid fortification,maquis,vista
ab on env-trees lod-sweep,oak-close
ab trees-0 env-temperate forest-edge,vista --set trees=0
ab impostors-0 env-temperate vista --set impostors=0
ab impostors-0 env-arid vista --set impostors=0
ab lodfade-0 env-trees lod-sweep --set lodfade=0
ab wind-0 env-trees oak-close --set wind=0
ab groundcover-0 env-temperate forest-edge,river --set groundcover=0
ab groundcover-0 env-arid maquis --set groundcover=0
ab rocks-0 env-temperate river,river-high --set rocks=0
ab water-0 env-temperate river,river-high --set water=0
ab reflect-0 env-temperate river --set reflect=0
ab structures-0 env-temperate settlement --set structures=0
ab structures-0 env-arid fortification --set structures=0
ab forestfloor-0 env-temperate vista,forest-edge --set forestfloor=0
echo "capture-env: wrote $out (llvmpipe)"
