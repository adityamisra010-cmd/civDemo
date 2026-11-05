#!/usr/bin/env bash
# Determinism scan for the AUTHORITATIVE prototype code (implementation-spec §1).
# Scans every *.cs under Proto.M7/ (bin/obj excluded), comments included (stricter than code-only).
# Exit 0 = clean; exit 1 = findings (printed); exit 2 = setup error.
# Usage: scripts/check-proto-determinism.sh [prototype-root]   (default: the folder containing scripts/)
set -euo pipefail
ROOT="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
SRC="$ROOT/Proto.M7"
[ -d "$SRC" ] || { echo "no Proto.M7 under $ROOT" >&2; exit 2; }

# The only file allowed to contain 'unchecked' (the RNG mixer; spec §1 allowlist). Matched by its exact path
# relative to the root, not by basename, so a CounterRng.cs in any other directory gets no exemption (verify-B F5).
UNCHECKED_ALLOW='Proto.M7/Rng/CounterRng.cs'

PATTERNS=(
  '\bdouble\b'
  '\bfloat\b'
  '\bdecimal\b'
  # BCL names of the IEEE/decimal types (verify-B F5): System.Double, Single, Half, Decimal, also as bare type names.
  'System\.(Double|Single|Half|Decimal|Numerics\.Complex)\b'
  '(^|[^.[:alnum:]_])(Double|Single|Half|Decimal|NFloat)\b'
  'MathF'
  # Whitespace-tolerant member access, and the 'using static' form that allows a bare Sqrt(...) (verify-B F5).
  'Math[[:space:]]*\.[[:space:]]*(Sqrt|Exp|Log|Log2|Log10|Pow|Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|Cbrt|Round|Floor|Ceiling|FusedMultiplyAdd|ScaleB|ReciprocalEstimate|ReciprocalSqrtEstimate|IEEERemainder)\b'
  'using[[:space:]]+static[[:space:]]+(System\.)?Math'
  '\bRandom\b'
  'RandomNumberGenerator'
  'Guid[[:space:]]*\.[[:space:]]*NewGuid'
  'DateTime'
  'TimeProvider'
  'Stopwatch'
  'Environment\.TickCount'
  'GetHashCode\('
  '\bHashCode\b'
  # Unordered containers by any name or construction path (SortedDictionary/SortedSet are allowed: comparer order).
  '\b(Dictionary|HashSet|ConcurrentDictionary|ConcurrentBag|FrozenDictionary|FrozenSet|ImmutableDictionary|ImmutableHashSet)[[:space:]]*<|\bHashtable\b'
  '\bTo(HashSet|Dictionary|Lookup|FrozenDictionary|FrozenSet|ImmutableDictionary|ImmutableHashSet)\b'
  'AsParallel'
  'Parallel\.'
  'ThreadPool|Task[[:space:]]*\.[[:space:]]*Run'
  # LINQ ordering/grouping in every spelling (verify-B F5: OrderByDescending, Order, OrderDescending were missed).
  '\b(OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy|Order|OrderDescending|ToLookup)[[:space:]]*[(<]'
)

mapfile -t FILES < <(find "$SRC" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | LC_ALL=C sort)
[ "${#FILES[@]}" -gt 0 ] || { echo "no source files under $SRC" >&2; exit 2; }

hits=0
for pat in "${PATTERNS[@]}"; do
  if out=$(grep -nE -- "$pat" "${FILES[@]}"); then
    echo "BANNED /$pat/:"; echo "$out" | sed 's/^/  /'; hits=$((hits + 1))
  fi
done
for f in "${FILES[@]}"; do
  if [ "${f#"$ROOT"/}" != "$UNCHECKED_ALLOW" ] && out=$(grep -nE '\bunchecked\b' "$f"); then
    echo "BANNED /unchecked/ outside $UNCHECKED_ALLOW:"; echo "$out" | sed "s|^|  $f:|"; hits=$((hits + 1))
  fi
done
# Overflow checking must be on for Proto.M7 (spec §1).
if ! grep -q "'\$(MSBuildProjectName)' == 'Proto.M7'" "$ROOT/Directory.Build.props" \
   || ! grep -q '<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>' "$ROOT/Directory.Build.props"; then
  echo "MISSING: CheckForOverflowUnderflow=true for Proto.M7 in Directory.Build.props"; hits=$((hits + 1))
fi
# ...and nothing may override it (verify-B F5): any MSBuild file under Proto.M7/ or the root Directory.Build.targets
# that sets CheckForOverflowUnderflow to anything but true; and no nested Directory.Build.props/targets under
# Proto.M7/ (a nested props file silently stops the root props from being imported).
mapfile -t MSB < <( { find "$SRC" \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -not -path '*/bin/*' -not -path '*/obj/*'; \
                      [ -f "$ROOT/Directory.Build.targets" ] && echo "$ROOT/Directory.Build.targets"; } | LC_ALL=C sort)
for f in "${MSB[@]}"; do
  if out=$(grep -nE 'CheckForOverflowUnderflow' "$f" | grep -vE '<CheckForOverflowUnderflow>[[:space:]]*true[[:space:]]*</CheckForOverflowUnderflow>'); then
    echo "OVERRIDE: CheckForOverflowUnderflow not 'true' in $f:"; echo "$out" | sed 's/^/  /'; hits=$((hits + 1))
  fi
  case "$(basename "$f")" in Directory.Build.props|Directory.Build.targets)
    [ "$f" != "$ROOT/Directory.Build.targets" ] && { echo "NESTED MSBUILD FILE under Proto.M7/: $f"; hits=$((hits + 1)); } ;;
  esac
done
echo "scanned ${#FILES[@]} files under Proto.M7/; pattern groups with findings: $hits"
[ "$hits" -eq 0 ] && { echo "DETERMINISM SCAN: CLEAN"; exit 0; }
echo "DETERMINISM SCAN: FAILED"; exit 1
