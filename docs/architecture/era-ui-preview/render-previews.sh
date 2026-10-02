#!/usr/bin/env bash
# Paints, for each of the nine Ages on the SAME stepped seed-42 world with only the player's Age
# differing, the Technology tree, the capital Age panel and a chrome sample in that Age's derived era
# theme (sim-ui --era-preview), screenshots each SVG with headless Chromium, and composes a contact
# sheet. The SVGs are deleted after the screenshot (the PNGs, the contact sheet and the log are kept).
# Usage (from repo root): docs/architecture/era-ui-preview/render-previews.sh [out-dir]
set -euo pipefail
OUT="${1:-docs/architecture/era-ui-preview}"
dotnet run --project Sim.Ui -c Release -- --era-preview "$OUT"

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
for svg in "$OUT"/era-*-tree.svg; do shot "$svg" 1600 1000; done
for svg in "$OUT"/era-*-age.svg; do shot "$svg" 560 1000; done
for svg in "$OUT"/era-*-chrome.svg; do shot "$svg" 1280 800; done

# The contact sheet: one row per era (tree | Age panel | chrome sample), A1 at the top.
SHEET="$OUT/contact-sheet.html"
{
  echo '<!doctype html><html><head><meta charset="utf-8"><style>'
  echo 'body{margin:0;background:#2b2722;font:15px Georgia,serif;color:#efe3c8}'
  echo '.row{display:flex;align-items:center;gap:10px;padding:6px 10px;border-bottom:1px solid #4a4238}'
  echo '.lab{width:110px;font-weight:bold;line-height:1.3}.lab small{display:block;font-weight:normal;color:#c9b588}'
  echo 'img{display:block;border:1px solid #4a4238}</style></head><body>'
  for n in 1 2 3 4 5 6 7 8 9; do
    tree=$(ls "$OUT"/era-$n-*-tree.png); age=$(ls "$OUT"/era-$n-*-age.png); chrome=$(ls "$OUT"/era-$n-*-chrome.png)
    name=$(basename "$tree" -tree.png | sed 's/^era-[0-9]-//')
    echo "<div class=\"row\"><div class=\"lab\">A$n<small>$name</small></div>"
    echo "<img src=\"$(basename "$tree")\" width=\"480\" height=\"300\"><img src=\"$(basename "$age")\" width=\"168\" height=\"300\"><img src=\"$(basename "$chrome")\" width=\"480\" height=\"300\"></div>"
  done
  echo '</body></html>'
} > "$SHEET"
"$CHROME" --headless --no-sandbox --disable-gpu --hide-scrollbars --allow-file-access-from-files \
  --window-size=1290,2826 --screenshot="$OUT/contact-sheet.png" "file://$(realpath "$SHEET")" >/dev/null 2>&1
rm -f "$SHEET" "$OUT"/era-*.svg
echo "contact sheet: $OUT/contact-sheet.png"
