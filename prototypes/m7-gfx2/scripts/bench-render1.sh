#!/usr/bin/env bash
# Per-feature cost of the render1 renderer core: p50 frame time with every G2 switch on, and with one switch off at a
# time, on three shots; plus a per-pass profile of the full G2 frame and the G1 preset baseline.
#
# Usage: scripts/bench-render1.sh OUT_DIR [FRAMES]     (needs a Release build of M7Gfx2.slnx)
#
# Numbers measured in a container without a GPU are llvmpipe (software GL) numbers: NOT Victus evidence (D-078 QM9).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: bench-render1.sh OUT_DIR [FRAMES]}"
frames="${2:-20}"
demo="$here/../Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
[ -f "$demo" ] || { echo "bench-render1: build Release first ($demo)" >&2; exit 2; }
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
mkdir -p "$out"
tsv="$out/bench-render1.tsv"
printf 'scene\tshot\tvariant\tp50_ms\tp95_ms\tdraws\ttris\n' > "$tsv"
shots=("terrain-temperate day" "battlefield follow-low" "render1-materials noon" "render1-coast coast-high")
variants=(
  "g2"
  "g1-preset"
  "sky=legacy" "aerial=0" "farfield=0" "pbr=0" "ibl=none" "ibl=hdri" "shadows=single" "cascades=2"
  "tonemap=acesfit" "tonemap=agx" "autoexposure=0" "bloom=0" "grade=0" "aa=none"
  "terrain=procedural" "aniso=1" "aniso=8" "hex=0" "macro=0" "ripplefade=0"
)
# VARIANTS="g2 hex=0" scripts/bench-render1.sh OUT   runs a subset
if [ -n "${VARIANTS:-}" ]; then read -r -a variants <<<"$VARIANTS"; fi
run() { # scene shot variant
  local scene=$1 shot=$2 v=$3 args=()
  case "$v" in
    g2) ;;
    g1-preset) args=(--preset g1) ;;
    *) args=(--set "$v") ;;
  esac
  local log
  log="$(nice -n 10 dotnet "$demo" --bench "$scene" --shot "$shot" --frames "$frames" --size 1280x720 "${args[@]}" 2>&1)"
  local line; line="$(grep -E '^\[SOFTWARE|^\[GPU' <<<"$log" | tail -1)"
  local p50 p95 draws tris
  p50="$(sed -E 's/.*p50=([0-9.]+).*/\1/' <<<"$line")"; p95="$(sed -E 's/.*p95=([0-9.]+).*/\1/' <<<"$line")"
  draws="$(sed -E 's/.*draws=([0-9]+).*/\1/' <<<"$line")"; tris="$(sed -E 's/.*tris=([0-9]+).*/\1/' <<<"$line")"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$scene" "$shot" "$v" "$p50" "$p95" "$draws" "$tris" | tee -a "$tsv"
}
for s in "${shots[@]}"; do
  read -r scene shot <<<"$s"
  for v in "${variants[@]}"; do
    case "$v" in ripplefade=0) [ "$scene" = render1-coast ] || continue ;; esac
    run "$scene" "$shot" "$v"
  done
  nice -n 10 dotnet "$demo" --bench "$scene" --shot "$shot" --frames "$frames" --size 1280x720 --profile 2>&1 \
    | grep -E '^\[pass\]|^\[bench\]|^\[cpu\]' > "$out/profile-$scene-$shot.txt"
done
echo "bench-render1: wrote $tsv (llvmpipe numbers when run without a GPU: not Victus evidence)"
