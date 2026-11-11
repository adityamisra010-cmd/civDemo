#!/usr/bin/env bash
# Captures of stage render2 (shadows, AO, contact shadows, crowds, culling, TAA): every feature on and off, the debug
# views, the crowd at 220 / 2,000 / 10,000, forced crowd levels, culling on/off identity, and TAA in motion.
#
# Usage: scripts/capture-render2.sh OUT_DIR [GROUPS]   GROUPS: any of ss,crowd,lod,cull,taa (default: all)
# Needs a Release build of M7Gfx2.slnx and the render1, env, chars and crowd bakes. Frames rendered without a GPU are
# llvmpipe frames (not Victus evidence). Characters and crowds are posed by CMU motion capture: PROTOTYPE-ONLY.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: capture-render2.sh OUT_DIR [GROUPS]}"
groups="${2:-ss,crowd,lod,cull,taa}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "capture-render2: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
mkdir -p "$out"
cap() { # name scene [args...]
  local name=$1 scene=$2; shift 2
  echo "== $name: $scene $*"
  nice -n 10 dotnet "$demo" --capture "$scene" --out "$out/$name" --size 1280x720 "$@" > "$out/$name.log" 2>&1
}
motion() { # name scene shot aa [args...]
  local name=$1 scene=$2 shot=$3 aa=$4; shift 4
  echo "== $name: --motion $scene $shot aa=$aa $*"
  nice -n 10 dotnet "$demo" --motion "$scene" --shot "$shot" --size 1280x720 --out "$out/$name" --tag "$aa" --set "aa=$aa" "$@" \
    > "$out/$name-$aa.log" 2>&1
}
has() { case ",$groups," in *",$1,"*) return 0 ;; *) return 1 ;; esac; }
if has ss; then
  # AO and contact shadows: render2 defaults against both off (the trunk frame), 16-sample stills
  cap forces chars-forces --set samples=16
  cap forces-off chars-forces --set samples=16,ssao=0,contact=0
  cap forces-single chars-forces --shots lines-low,force-a --set samples=16,shadows=single
  cap forces-debug-ao chars-forces --shots lines-low,force-a --set debug=ao
  cap forces-debug-contact chars-forces --shots lines-low,force-a --set debug=contact
  cap forces-debug-shares chars-forces --shots lines-low --set debug=shares
  cap forces-debug-cascades chars-forces --shots lines-34 --set debug=cascades
  cap env env-temperate --shots settlement,settlement-gate --set samples=16
  cap env-off env-temperate --shots settlement,settlement-gate --set samples=16,ssao=0,contact=0
fi
if has crowd; then
  cap crowd220 crowd-formation --set samples=16
  cap crowd220-off crowd-formation --shots front-low,close --set samples=16,ssao=0,contact=0
  cap crowd2k crowd-battle-2k --set samples=16
  cap crowd10k crowd-battle-10k --set samples=8
  cap crowd10k-debug-cascades crowd-battle-10k --shots vista --set debug=cascades
fi
if has lod; then
  for lv in 0 1 2; do cap "lod$lv" crowd-formation --shots front-low,close,wide --set "samples=16,crowdlevel=$lv"; done
  # the level cross-fade made visible: a bias of 0.11 puts the nearest soldiers inside the lod2/lod3 fade band
  cap fade-1spp crowd-formation --shots front-low --set samples=1,aa=none,crowdlod=0.11
  cap fade-16spp crowd-formation --shots front-low --set samples=16,crowdlod=0.11
  cap nofade-1spp crowd-formation --shots front-low --set samples=1,aa=none,crowdfade=0,crowdlod=0.11
fi
if has cull; then
  cap cull-on crowd-battle-2k --shots village,vista --set samples=1
  cap cull-off crowd-battle-2k --shots village,vista --set samples=1,occlusion=0,frustum=0,castercull=0
  cap cull10k-on crowd-battle-10k --shots village --set samples=1
  cap cull10k-off crowd-battle-10k --shots village --set samples=1,occlusion=0,frustum=0,castercull=0
fi
if has taa; then
  for aa in none fxaa taa; do
    motion motion-220 crowd-formation front-low "$aa" --frames 16 --pan 0.06,0,0.02 --save 15
    motion motion-forces chars-forces lines-34 "$aa" --frames 16 --orbit 0.4 --save 15
  done
  cap still-forces chars-forces --shots lines-34 --set samples=16
fi
echo "capture-render2: wrote $out (llvmpipe frames; CMU motion PROTOTYPE-ONLY)"
