#!/usr/bin/env bash
# R2a previews (sim-ui --r2a-preview): turn 1 (Age I); researched pre-Trade and post-Trade (Age III: crafts, a
# crop, the Taxation civic, a road class and Trade's prerequisites known; post-Trade adds Trade through the order
# pathway); and a city-state left to develop for 600 turns (its screen and a knowledge card). Each state is
# painted as the game screen with the POLICY panel open and (except the city-state) as the panel alone at full
# height; screenshots each SVG with headless Chromium. The SVGs are deleted after the screenshot (the PNGs and the
# log are kept). (2026-10-05: this header was a copy of the action-surface script's until then.)
# Usage (from repo root): docs/architecture/r2-previews/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/r2-previews}"
dotnet run --project Sim.Ui -c Release -- --r2a-preview "$OUT"

CHROME="${CHROME:-}"
if [ -z "$CHROME" ]; then
  for c in /opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell \
           /opt/pw-browsers/chromium-*/chrome-linux/chrome chromium chromium-browser google-chrome; do
    if [ -x "$c" ] || command -v "$c" >/dev/null 2>&1; then CHROME="$c"; break; fi
  done
fi
if [ -z "$CHROME" ]; then echo "no headless Chromium found: SVGs only"; exit 0; fi

shot() { # svg width height
  local png="${1%.svg}.png"
  "$CHROME" --headless --no-sandbox --disable-gpu --hide-scrollbars --allow-file-access-from-files \
    --window-size="$2,$3" --screenshot="$png" "file://$(realpath "$1")" >/dev/null 2>&1
  echo "screenshot: $png"
}
for svg in "$OUT"/*-screen.svg; do shot "$svg" 1280 800; done
for svg in "$OUT"/*-panel.svg "$OUT"/*-card.svg; do
  # The panel is as tall as the surface it holds: read the size from the SVG's own header.
  read -r w h < <(awk 'match($0, /<svg[^>]*>/) { s = substr($0, RSTART, RLENGTH);
    if (match(s, /width="[0-9.]+"/)) w = substr(s, RSTART+7, RLENGTH-8);
    if (match(s, /height="[0-9.]+"/)) h = substr(s, RSTART+8, RLENGTH-9);
    print int(w+0.5), int(h+0.5); exit }' "$svg")
  shot "$svg" "$w" "$h"
done
rm -f "$OUT"/*.svg
