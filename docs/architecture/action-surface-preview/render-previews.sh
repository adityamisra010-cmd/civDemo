#!/usr/bin/env bash
# Paints the REAL action surface (sim-ui --action-preview): turn 1 on the canonical founded world (Age I),
# and a later state — a crop, a taxation node and a road class known — at Age III and Age VIII, each as the
# game screen with the POLICY panel open and as the panel alone at full height; screenshots each SVG with
# headless Chromium. The SVGs are deleted after the screenshot (the PNGs and the log are kept).
# Usage (from repo root): docs/architecture/action-surface-preview/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/action-surface-preview}"
dotnet run --project Sim.Ui -c Release -- --action-preview "$OUT"

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
for svg in "$OUT"/*-panel.svg; do
  # The panel is as tall as the surface it holds: read the size from the SVG's own header.
  read -r w h < <(awk 'match($0, /<svg[^>]*>/) { s = substr($0, RSTART, RLENGTH);
    if (match(s, /width="[0-9.]+"/)) w = substr(s, RSTART+7, RLENGTH-8);
    if (match(s, /height="[0-9.]+"/)) h = substr(s, RSTART+8, RLENGTH-9);
    print int(w+0.5), int(h+0.5); exit }' "$svg")
  shot "$svg" "$w" "$h"
done
rm -f "$OUT"/*.svg
