#!/usr/bin/env bash
# Paints the Age surfaces (capital Age panel, ADVANCE AGE flow, transition toast) and the world lens at
# three zoom levels from a REAL seed-42 world played to Age eligibility through the real order pathway
# (sim-ui --age-preview) and, when a headless Chromium is present, screenshots each SVG to a 1600x1000 PNG.
# Stream U3 (roads by class): also paints the TEST-ONLY road fixture (Sim.Ui.Tests RoadWorldFixture — every
# road tier built by real DevelopRoads orders from constructed knowledge and materials, one route partially
# modernized, the player in Age II) at the three zoom levels -> 12-14 and roads-preview-log.txt.
# Usage (from repo root): docs/architecture/age-and-world-ui/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/age-and-world-ui}"
dotnet run --project Sim.Ui -c Release -- --age-preview "$OUT"
CIV_ROAD_PREVIEW_OUT="$(realpath "$OUT")" dotnet test Sim.Ui.Tests -c Release \
  --filter "FullyQualifiedName~Preview_RoadFixture_Svg" >/dev/null

CHROME="${CHROME:-}"
if [ -z "$CHROME" ]; then
  for c in /opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell \
           /opt/pw-browsers/chromium-*/chrome-linux/chrome chromium chromium-browser google-chrome; do
    if [ -x "$c" ] || command -v "$c" >/dev/null 2>&1; then CHROME="$c"; break; fi
  done
fi
if [ -z "$CHROME" ]; then echo "no headless Chromium found: SVGs only"; exit 0; fi

for svg in "$OUT"/*.svg; do
  png="${svg%.svg}.png"
  "$CHROME" --headless --no-sandbox --disable-gpu --hide-scrollbars --allow-file-access-from-files \
    --window-size=1600,1000 --screenshot="$png" "file://$(realpath "$svg")" >/dev/null 2>&1
  echo "screenshot: $png"
done
