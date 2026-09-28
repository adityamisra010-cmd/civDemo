#!/usr/bin/env bash
# The Trees + Ages UI preview (docs/architecture/the-trees-ui.md §9).
# Paints the preview scenarios to SVG with `sim-ui --trees-preview` (no window, no
# world), then — when a headless Chromium is available — screenshots each SVG to a
# 1280x800 PNG. Usage: scripts/trees-preview.sh [out-dir]
set -euo pipefail
OUT="${1:-trees-preview}"
dotnet run --project Sim.Ui -c Release -- --trees-preview "$OUT"

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
