#!/usr/bin/env bash
# Costs of the render2 features: crowd at 220 / 2,000 / 10,000 soldiers (with and without the crowd), the crowd LOD
# sweep, frustum / occlusion / cascade-caster culling, screen-space AO and contact shadows, and TAA vs FXAA vs none;
# per-pass profiles of the full G2 frame. Raw logs go to OUT_DIR/raw, a table to OUT_DIR/bench-render2.tsv.
#
# Usage: scripts/bench-render2.sh OUT_DIR [GROUPS]      GROUPS: any of scale,lod,cull,ss,aa,profile (default: all)
# Needs a Release build of M7Gfx2.slnx and the crowd bake (scripts/bake-crowd.sh).
#
# Numbers measured in a container without a GPU are llvmpipe (software GL) numbers: NOT Victus evidence (D-078 QM9).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: bench-render2.sh OUT_DIR [GROUPS]}"
groups="${2:-scale,lod,cull,ss,aa,profile}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "bench-render2: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
mkdir -p "$out/raw"
bench() { # name scene shot frames variants [extra args...]
  local name=$1 scene=$2 shot=$3 frames=$4 variants=$5; shift 5
  echo "== $name: $scene/$shot frames=$frames variants=$variants $*"
  nice -n 10 dotnet "$demo" --bench "$scene" --shot "$shot" --frames "$frames" --size 1280x720 --variants "$variants" "$@" > "$out/raw/$name.log" 2>&1
}
has() { case ",$groups," in *",$1,"*) return 0 ;; *) return 1 ;; esac; }
if has scale; then
  bench scale-220-wide crowd-formation wide 10 "g2;crowd=0"
  bench scale-220-frontlow crowd-formation front-low 10 "g2;crowd=0"
  bench scale-2k-vista crowd-battle-2k vista 6 "g2;crowd=0"
  bench scale-2k-line crowd-battle-2k line 6 "g2;crowd=0"
  bench scale-10k-vista crowd-battle-10k vista 4 "g2;crowd=0"
  bench scale-10k-line crowd-battle-10k line 4 "g2;crowd=0"
fi
if has lod; then
  bench lod-220-wide crowd-formation wide 10 "g2;crowdlevel=0;crowdlevel=1;crowdlevel=2;crowdlod=0.5;crowdlod=2;crowdfade=0"
  bench lod-2k-line crowd-battle-2k line 6 "g2;crowdlevel=0;crowdlevel=1;crowdlevel=2;crowdlod=0.5;crowdlod=2;crowdfade=0"
fi
if has cull; then
  bench cull-2k-village crowd-battle-2k village 6 "g2;occlusion=0;frustum=0;castercull=0"
  bench cull-2k-vista crowd-battle-2k vista 6 "g2;occlusion=0;castercull=0"
  bench cull-10k-village crowd-battle-10k village 4 "g2;occlusion=0;frustum=0;castercull=0"
  bench cull-10k-line crowd-battle-10k line 4 "g2;occlusion=0;castercull=0"
fi
if has ss; then
  bench ss-forces chars-forces lines-34 10 "g2;ssao=0;contact=0;ssao=0,contact=0"
  bench ss-220-frontlow crowd-formation front-low 10 "g2;ssao=0;contact=0;ssao=0,contact=0"
  bench ss-env env-temperate settlement 6 "g2;ssao=0,contact=0"
fi
if has aa; then
  bench aa-forces chars-forces lines-34 10 "aa=none;aa=fxaa;aa=taa"
  bench aa-220-frontlow crowd-formation front-low 10 "aa=none;aa=fxaa;aa=taa"
fi
if has profile; then
  bench profile-forces chars-forces lines-34 6 "g2;ssao=0,contact=0;aa=taa" --profile
  bench profile-220-wide crowd-formation wide 6 "g2;crowd=0;aa=taa" --profile
  bench profile-2k-line crowd-battle-2k line 4 "g2;crowd=0" --profile
  bench profile-10k-line crowd-battle-10k line 3 "g2;crowd=0" --profile
fi
python3 -I "$here/bench-render2-table.py" "$out/raw" > "$out/bench-render2.tsv"
echo "bench-render2: wrote $out/bench-render2.tsv (llvmpipe numbers when run without a GPU: not Victus evidence)"
