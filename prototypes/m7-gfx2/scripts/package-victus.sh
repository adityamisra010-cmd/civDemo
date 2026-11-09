#!/usr/bin/env bash
# Builds the Windows Victus benchmark package from the COMMITTED tree of this prototype:
#   dotnet publish Gfx.Demo (with Gfx.Perf) -r win-x64 --self-contained -c Release, framework trimmed (partial);
#   CivSimBench.exe (scripts/build-launcher.sh); run-benchmark.bat, README-VICTUS.txt, GPU-preference scripts
#   (converted to CRLF); PACKAGE-SHA256.txt; zipped as <name>-<shortcommit>.zip.
#   scripts/package-victus.sh [NAME] [OUTDIR]     defaults: g2-before, the scratchpad victus/ directory
# Large outputs never go into the repository. Trimming: only assemblies marked trimmable are trimmed (the
# framework); MonoGame (reflected into by the harness and SysInfo) and every Gfx.* assembly are kept whole.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
name="${1:-g2-before}"
outdir="${2:-/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/victus}"
work="${PERF_WORK:-/home/user/g2w/perf-work}/package"
sha="$(git -C "$here" rev-parse HEAD)"; short="$(git -C "$here" rev-parse --short=7 HEAD)"
# reports/ never goes into the package, so a report in progress does not make the package "dirty".
if [ -n "$(git -C "$here" status --porcelain -- . ':!reports')" ]; then
  if [ "${ALLOW_DIRTY:-0}" != 1 ]; then
    echo "package-victus: the prototype has uncommitted changes; commit first (the package names its commit)" >&2; exit 1
  fi
  short="$short-dirty"; sha="$sha-dirty"
fi
stage="$work/$name-$short"; app="$stage"; pub="$work/publish/$name-$short"
rm -rf "$stage" "$pub"
mkdir -p "$app" "$pub" "$outdir"
export DOTNET_CLI_USE_MSBUILD_SERVER=0
nice -n 19 dotnet publish "$here/Gfx.Demo/Gfx.Demo.csproj" -c Release -r win-x64 --self-contained true \
  -m:1 -nodeReuse:false -o "$pub" \
  -p:SourceCommit="$sha" -p:PublishTrimmed=true -p:TrimMode=partial -p:SuppressTrimAnalysisWarnings=true \
  -p:EnableTrimAnalyzer=false -p:TrimmerSingleWarn=false -p:NoWarn=NETSDK1074 \
  -p:"TrimmerRootAssembly=MonoGame.Framework" -p:GenerateDocumentationFile=false 2>&1 | tail -3
# Runtime files the benchmark never loads (debugger and dump support, QUIC, standalone GCs) are not copied.
# openal.dll (OpenAL Soft, MonoGame's audio backend) is native and loaded only when audio is used: the benchmark has none.
skip='^(createdump\.exe|mscordbi\.dll|mscordaccore\.dll|mscordaccore_amd64_amd64_.*\.dll|Microsoft\.DiaSymReader\.Native\.amd64\.dll|msquic\.dll|clrgc\.dll|clrgcexp\.dll|openal\.dll)$'
( cd "$pub" && find . -type f -printf '%P\n' ) | LC_ALL=C sort | while read -r f; do
  if [[ "$(basename "$f")" =~ $skip ]]; then echo "  not packaged: $f"; else mkdir -p "$app/$(dirname "$f")"; cp -p "$pub/$f" "$app/$f"; fi
done
[ -f "$app/fixtures/battle-trace-combinedarms-s7.json" ] || { echo "package-victus: the battle-trace fixture is missing from the publish output" >&2; exit 1; }
"$here/scripts/build-launcher.sh" "$work/launcher" | tail -4
cp "$work/launcher/CivSimBench.exe" "$app/"
for f in run-benchmark.bat Set-GPU-Preference-HighPerformance.cmd Undo-GPU-Preference.cmd README-VICTUS.txt THIRD-PARTY-NOTICES.txt; do
  sed -e "s/@PACKAGE@/$name-$short/g" -e "s/@COMMIT@/$sha/g" -e "s/@BUILT@/$(date -u +%Y-%m-%dT%H:%MZ)/g" \
      "$here/Gfx.Perf/package/$f" | sed 's/\r$//; s/$/\r/' > "$app/$f"
done
( cd "$app" && find . -type f ! -name PACKAGE-SHA256.txt -printf '%P\n' | LC_ALL=C sort | xargs -d '\n' sha256sum ) > "$app/PACKAGE-SHA256.txt"
zip="$outdir/$name-$short.zip"
rm -f "$zip"
( cd "$work" && zip -q -9 -r "$zip" "$name-$short" )
echo "package-victus: $zip"
echo "  $(stat -c %s "$zip") bytes zipped, $(find "$app" -type f | wc -l) files, $(du -sb "$app" | cut -f1) bytes unpacked"
sha256sum "$zip"
