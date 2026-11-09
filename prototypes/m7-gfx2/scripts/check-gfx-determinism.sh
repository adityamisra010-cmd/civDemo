#!/usr/bin/env bash
# Integer-only / determinism scan for GAMEPLAY-RELEVANT graphics code (implementation-spec §0).
# Scans ONLY the authoritative folder (default: Gfx.Proc/Authoritative). Presentation code may use float.
# Usage: scripts/check-gfx-determinism.sh [dir ...]      exit 0 = clean, 1 = violations, 2 = usage error
# Comment lines (// ... and /// ...) are ignored; trailing // comments are stripped before matching.
set -u
here="$(cd "$(dirname "$0")/.." && pwd)"
dirs=("$@"); [ ${#dirs[@]} -eq 0 ] && dirs=("$here/Gfx.Proc/Authoritative")

# pattern|reason
rules=(
  '\bfloat\b|float type'
  '\bdouble\b|double type'
  '\bdecimal\b|decimal type'
  '\bSingle\b|System.Single'
  '\bDouble\b|System.Double'
  '\bHalf\b|System.Half'
  '[0-9]\.[0-9]+[fFdDmM]?\b|floating-point literal'
  '\b[0-9]+[fFdD]\b|float/double literal suffix'
  '\b[0-9][0-9_]*(\.[0-9_]*)?[eE][+-]?[0-9]+|exponent floating-point literal'
  '(?<![\w.)\]])\.[0-9]+|leading-dot floating-point literal'
  '\b[0-9]+\.(?![\w.])|trailing-dot floating-point literal'
  '\bMathF\.|MathF (float math)'
  '\bMath\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|Asinh|Acosh|Atanh|Exp|Log|Log2|Log10|Pow|Sqrt|Cbrt|Round|Floor|Ceiling|Truncate|FusedMultiplyAdd|ScaleB|ILogB|IEEERemainder|CopySign|BitIncrement|BitDecrement|ReciprocalEstimate|ReciprocalSqrtEstimate|SinCos)\b|transcendental/floating-point Math call'
  '\bBitConverter\.(Int64BitsToDouble|Int32BitsToSingle|DoubleToInt64Bits|SingleToInt32Bits)\b|float bit reinterpretation'
  '\bMathHelper\b|MonoGame MathHelper (float)'
  '\bVector[234]\b|float vector type'
  '\bSystem\.Random\b|System.Random'
  '\bnew Random\b|System.Random'
  '\bRandom\.Shared\b|System.Random'
  '\bDictionary<|Dictionary (iteration order)'
  '\bHashSet<|HashSet (iteration order)'
  '\bDateTime\.(Now|UtcNow|Today)\b|wall clock'
  '\bGetHashCode\b|GetHashCode as logic input'
  '\bAsParallel\b|unordered parallelism'
  '\bParallel\.|unordered parallelism'
)

status=0; files=0
for d in "${dirs[@]}"; do
  if [ ! -d "$d" ]; then echo "check-gfx-determinism: no such directory: $d" >&2; exit 2; fi
  while IFS= read -r -d '' f; do
    files=$((files+1))
    # strip full-line comments and trailing // comments, keep line numbers
    clean="$(sed -e 's#^[[:space:]]*//.*$##' -e 's#[[:space:]]//.*$##' "$f")"
    for r in "${rules[@]}"; do
      pat="${r%|*}"; why="${r##*|}"
      hits="$(printf '%s\n' "$clean" | grep -nP -- "$pat" || true)"
      if [ -n "$hits" ]; then
        while IFS= read -r h; do echo "VIOLATION [$why] ${f#$here/}:$h"; done <<< "$hits"
        status=1
      fi
    done
  done < <(find "$d" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 | sort -z)
done
if [ $status -eq 0 ]; then echo "check-gfx-determinism: OK ($files files scanned in: ${dirs[*]})"; fi
exit $status
