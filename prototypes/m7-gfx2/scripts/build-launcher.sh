#!/usr/bin/env bash
# Builds CivSimBench.exe (Windows x64 discrete-GPU launcher, Gfx.Perf/launcher/civsimbench.c) without the C
# runtime, then checks that it exports NvOptimusEnablement and AmdPowerXpressRequestHighPerformance.
#   scripts/build-launcher.sh [OUTDIR]        (default /home/user/g2w/perf-work/launcher)
# Toolchain: x86_64-w64-mingw32-gcc (apt: gcc-mingw-w64-x86-64), else clang --target=x86_64-w64-mingw32.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
src="$here/Gfx.Perf/launcher/civsimbench.c"
out="${1:-/home/user/g2w/perf-work/launcher}"
mkdir -p "$out"
exe="$out/CivSimBench.exe"
flags=(-Os -s -nostdlib -nostartfiles -ffreestanding -fno-stack-protector -mno-stack-arg-probe -fno-asynchronous-unwind-tables
       -Wall -Wextra -Werror -Wl,--subsystem,console -Wl,-e,entry -Wl,--gc-sections -Wl,--no-insert-timestamp)
if command -v x86_64-w64-mingw32-gcc >/dev/null 2>&1; then
  cc=(x86_64-w64-mingw32-gcc)
elif command -v clang >/dev/null 2>&1; then
  cc=(clang --target=x86_64-w64-mingw32 -fuse-ld=lld)
else
  echo "build-launcher: no mingw-w64 gcc or clang found" >&2; exit 1
fi
"${cc[@]}" "${flags[@]}" -o "$exe" "$src" -lkernel32 -lshell32
dump="$(x86_64-w64-mingw32-objdump -p "$exe" 2>/dev/null || llvm-objdump -p "$exe")"
for sym in NvOptimusEnablement AmdPowerXpressRequestHighPerformance; do
  grep -q "$sym" <<<"$dump" || { echo "build-launcher: $sym is NOT exported" >&2; exit 1; }
done
grep -q "Subsystem.*0003\|Subsystem.*(Windows CUI)" <<<"$dump" || echo "build-launcher: warning: subsystem is not console" >&2
echo "build-launcher: $exe ($(stat -c %s "$exe") bytes, ${cc[0]}); exports:"
grep -E "\] (NvOptimusEnablement|AmdPowerXpressRequestHighPerformance)" <<<"$dump" || grep -E "NvOptimusEnablement|AmdPowerXpress" <<<"$dump"
echo "imports: $(grep -E "DLL Name" <<<"$dump" | awk '{print $3}' | tr '\n' ' ')"
sha256sum "$exe"
