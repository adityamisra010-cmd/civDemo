#!/usr/bin/env bash
# The world visualization preview (docs/architecture/world-visualization.md §10).
# Paints the DEMO world scenarios to SVG with `sim-ui --world-preview` (no window, no
# simulation), then — when a headless Chromium is available — screenshots each SVG to a
# 1280x800 PNG. Usage: scripts/world-preview.sh [out-dir]
set -euo pipefail
OUT="${1:-world-preview}"
dotnet run --project Sim.Ui -c Release -- --world-preview "$OUT"

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
    --window-size=1280,800 --screenshot="$png" "file://$(realpath "$svg")" >/dev/null 2>&1
  echo "screenshot: $png"
done
