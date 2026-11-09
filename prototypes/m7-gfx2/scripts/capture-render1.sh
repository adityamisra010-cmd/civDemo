#!/usr/bin/env bash
# The render1 capture set: G2 deliverable stills (16-sample jittered accumulation), the G1 frame path of the new
# render1 scenes (--preset g1; pristine G1 has no such scenes), and per-feature A/B pairs (one switch off).
#
# Usage: scripts/capture-render1.sh OUT_DIR        (needs a Release build of M7Gfx2.slnx and the render1 bake)
#
# BEFORE images of the G1 scenes come from pristine G1 itself (prototypes/m7-gfx1, Gfx.Demo --capture all).
# Renderer here: llvmpipe software GL, so the frame times printed are NOT Victus evidence.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: capture-render1.sh OUT_DIR}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "capture-render1: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
cap() { # dir scene [args...]
  local dir=$1 scene=$2; shift 2
  mkdir -p "$out/$dir"
  nice -n 10 dotnet "$demo" --capture "$scene" --out "$out/$dir" --size 1280x720 "$@" 2>&1 | grep -E '^\[capture\] /|rror|xception' | sed "s|$out/||" || true
}
# 1. G2 deliverable stills (16 jittered samples, no FXAA)
for s in terrain-temperate terrain-arid battlefield units-closeup avatar-faces render1-materials render1-closeups render1-coast; do
  cap g2 "$s" --set samples=16
done
cap g2 cine-surrender --set samples=16 --shots 02-opponentintro-62,04-outcomebeat-62
# 2. the G1 frame path for the render1 scenes
for s in render1-materials render1-closeups render1-coast; do cap g1path "$s" --preset g1; done
# 3. per-feature A/B (one switch off, FXAA frames; the G2 'on' frame is ab/g2-fxaa)
ab() { local name=$1 scene=$2 shots=$3; shift 3; cap "ab/$name" "$scene" --shots "$shots" "$@"; }
ab g2-fxaa terrain-temperate day,dusk,tactical-low
ab g2-fxaa battlefield dusk,follow-low,wide
ab g2-fxaa render1-materials noon,golden
ab g2-fxaa render1-closeups cliff,meadow-grazing,gravel-track
ab g2-fxaa render1-coast coast-high,beach
ab sky-legacy terrain-temperate day,dusk --set sky=legacy
ab aerial-0 terrain-temperate day,dusk --set aerial=0
ab farfield-0 terrain-temperate day --set farfield=0
ab farfield-0 render1-coast coast-high --set farfield=0
ab pbr-0 render1-materials noon --set pbr=0
ab ibl-none render1-materials noon --set ibl=none
ab ibl-hdri render1-materials noon,golden --set ibl=hdri
ab ibl-hdri-bg render1-materials noon --set ibl=hdri,hdribg=1
ab shadows-single battlefield dusk,follow-low --set shadows=single
ab shadows-single terrain-temperate dusk --set shadows=single
for t in acesfit aces agx linear; do ab "tonemap-$t" battlefield dusk --set "tonemap=$t"; ab "tonemap-$t" render1-materials noon --set "tonemap=$t"; done
ab autoexposure-0 terrain-temperate day,dusk --set autoexposure=0
ab bloom-0 render1-materials golden --set bloom=0
ab bloom-0 terrain-temperate dusk --set bloom=0
ab grade-0 terrain-temperate day --set grade=0
ab grade-0 render1-materials noon --set grade=0
ab aa-none battlefield follow-low --set aa=none
ab terrain-procedural terrain-temperate tactical-low --set terrain=procedural
ab terrain-procedural render1-closeups meadow-grazing,gravel-track,cliff --set terrain=procedural
ab aniso-1 render1-closeups meadow-grazing,gravel-track --set aniso=1
ab hex-0 render1-closeups cliff,meadow-grazing --set hex=0
ab hex-0 battlefield dusk --set hex=0
ab macro-0 terrain-temperate tactical-low --set macro=0
ab ripplefade-0 render1-coast coast-high,beach --set ripplefade=0
echo "capture-render1: wrote $out (llvmpipe)"
