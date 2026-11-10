#!/usr/bin/env bash
# Capture every stage-chars scene (16-sample stills, 1280x720) into OUT, then print the llvmpipe costs.
# Usage: scripts/capture-chars.sh OUT [extra Gfx.Demo args...]   (needs scripts/bake-chars.sh and a Release build)
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?output folder}"; shift || true
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-offscreen}" LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
demo="$here/Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
mkdir -p "$out"
for s in chars-hero chars-eras chars-forces chars-lod chars-anim; do
  dotnet "$demo" --capture "$s" --out "$out" --size 1280x720 --set samples=16 "$@"
done
dotnet "$demo" --chars-costs
