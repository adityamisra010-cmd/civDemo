#!/usr/bin/env bash
# Paints the KNOWLEDGE & TECHNOLOGY progression screen from a real stepped world to SVG
# (sim-ui --research-preview) and, when a headless Chromium is present, screenshots each
# SVG to a 1600x1000 PNG. Usage (from repo root): docs/architecture/research-tree-ui/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/research-tree-ui}"
dotnet run --project Sim.Ui -c Release -- --research-preview "$OUT"

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
