#!/usr/bin/env bash
# Renders the TEST-ONLY institution fixture (Sim.Ui.Tests InstitutionWorldFixture: seed-42 world + constructed
# StructureRow / ResearchCostModifierRow rows) through the world lens at settlement and regional zoom, then
# screenshots each SVG to a 1600x1000 PNG when a headless Chromium is present. Production code never sees
# the fixture: the SVGs are written by the test Preview_InstitutionFixture_Svg.
# Usage (from repo root): docs/architecture/age-and-world-ui/render-institution-preview.sh [out-dir]
set -euo pipefail
OUT="$(realpath "${1:-docs/architecture/age-and-world-ui}")"
CIV_INSTITUTION_PREVIEW_OUT="$OUT" dotnet test Sim.Ui.Tests -c Release --filter "FullyQualifiedName~Preview_InstitutionFixture_Svg" >/dev/null
CHROME="${CHROME:-}"
if [ -z "$CHROME" ]; then
  for c in /opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell \
           /opt/pw-browsers/chromium-*/chrome-linux/chrome chromium chromium-browser google-chrome; do
    if [ -x "$c" ] || command -v "$c" >/dev/null 2>&1; then CHROME="$c"; break; fi
  done
fi
if [ -z "$CHROME" ]; then echo "no headless Chromium found: SVGs only"; exit 0; fi
for svg in "$OUT"/1[01]-institutions-*.svg; do
  png="${svg%.svg}.png"
  "$CHROME" --headless --no-sandbox --disable-gpu --hide-scrollbars --allow-file-access-from-files \
    --window-size=1600,1000 --screenshot="$png" "file://$svg" >/dev/null 2>&1
  rm -f "$svg"
  echo "screenshot: $png"
done
