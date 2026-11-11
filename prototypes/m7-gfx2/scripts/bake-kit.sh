#!/usr/bin/env bash
# Bake the stage-kit hero asset (the bronze aspis) into $G2_ASSETS/baked/kit/hero: aspis.glb (glTF 2.0 with three LODs
# and the 2048-texel PBR maps embedded as PNG), MANIFEST.tsv (sha256 of the file and of each embedded image) and
# aspis.provenance.json (generator, parameters, the commit of the tree that baked it).
# Usage: scripts/bake-kit.sh [--check]     (needs a Release build of Gfx.Demo; --check re-bakes in memory and compares)
# The generator reads no external file: every byte comes from Gfx.Gear's code and its fixed parameters.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
root="${G2_ASSETS:-/home/user/g2w/assets}"
demo="$here/Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll"
commit="$(git -C "$here" rev-parse HEAD 2>/dev/null || echo unknown)"
if [ -n "$(git -C "$here" status --porcelain -- . 2>/dev/null)" ]; then commit="$commit+dirty"; fi
dotnet "$demo" --kit-bake --out "$root/baked/kit/hero" --commit "$commit" "$@"
