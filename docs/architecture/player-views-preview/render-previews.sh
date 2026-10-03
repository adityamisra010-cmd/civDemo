#!/usr/bin/env bash
# Paints the PLAYER VIEWS (sim-ui --player-views-preview, ADR-033 D9): SETTLEMENT, EMPIRE and INSTITUTIONS at
# turn 1 on the canonical founded world (Age I) and at a developed Age III state with a university, each as
# the panel alone at full height; screenshots each SVG with headless Chromium. The SVGs are deleted after the screenshot (the PNGs and the log are kept).
# Usage (from repo root): docs/architecture/player-views-preview/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/player-views-preview}"
dotnet run --project Sim.Ui -c Release -- --player-views-preview "$OUT"

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
for svg in "$OUT"/*.svg; do
  # The panel is as tall as the surface it holds: read the size from the SVG's own header.
  read -r w h < <(awk 'match($0, /<svg[^>]*>/) { s = substr($0, RSTART, RLENGTH);
    if (match(s, /width="[0-9.]+"/)) w = substr(s, RSTART+7, RLENGTH-8);
    if (match(s, /height="[0-9.]+"/)) h = substr(s, RSTART+8, RLENGTH-9);
    print int(w+0.5), int(h+0.5); exit }' "$svg")
  shot "$svg" "$w" "$h"
done
rm -f "$OUT"/*.svg
