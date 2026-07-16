#!/usr/bin/env bash
# Banned-constructs check — docs/m0-kernel-spec.md §3.7 / CLAUDE.md law 5.
#
# Greps C# sources for determinism-breaking constructs. Runs locally
# (./scripts/check-banned-constructs.sh) and as a CI gate on every push.
#
# Greppable items covered here:
#   System.Random / Random.Shared (any project) - all randomness goes through RngRegistry (PCG32)
#   float, incl. literal suffixes (1.5f), MathF, System.Single (any project) - D-004: banned project-wide
#   DateTime/DateTimeOffset.Now/UtcNow/Today (sim code)
#   AsParallel / unordered Parallel.For/ForAsync/ForEach/ForEachAsync/Invoke (sim code)
#   GetHashCode() in sim code - banned as logic input; a legitimate equality-plumbing
#     line (e.g. an override on an Id struct) must carry the marker  // gate:allow-gethashcode
#     so every exception is explicit and visible in diffs.
#
# NOT greppable by regex - enforced by review instead:
#   Dictionary/HashSet iteration in sim logic; culture-sensitive parse/format
#   (mitigated by InvariantGlobalization in Directory.Build.props); LINQ in hot paths.
set -uo pipefail
cd "$(dirname "$0")/.."

SIM_DIRS=(Sim.Core Sim.Data Sim.Cli)                 # simulation code: full ban list
ALL_DIRS=(Sim.Core Sim.Data Sim.Cli Sim.Tests)       # project-wide bans

fail=0

# scan <description> <pattern> <exclusion-filter> <dirs...>
# Lines matching <pattern> are violations unless they match <exclusion-filter>.
scan() {
  local desc="$1" pattern="$2" filter="${3:-}"
  shift 3
  local matches
  matches=$(grep -RnE --include='*.cs' --exclude-dir=bin --exclude-dir=obj "$pattern" "$@" 2>/dev/null || true)
  if [[ -n "$filter" && -n "$matches" ]]; then
    matches=$(grep -vE "$filter" <<<"$matches" || true)
  fi
  if [[ -n "$matches" ]]; then
    printf 'BANNED CONSTRUCT — %s:\n%s\n\n' "$desc" "$matches"
    fail=1
  fi
}

# --- Project-wide bans ---
scan 'float keyword (D-004)'               '\bfloat\b'                                     '' "${ALL_DIRS[@]}"
scan 'float literal suffix (D-004)'        '\b[0-9]+\.?[0-9]*[fF]\b'                       '0[xX][0-9a-fA-F]' "${ALL_DIRS[@]}"
scan 'MathF / System.Single (D-004)'       '\bMathF\b|System\.Single'                      '' "${ALL_DIRS[@]}"
scan 'System.Random (use RngRegistry)'     'System\.Random|\bnew Random\b|Random\.Shared'  '' "${ALL_DIRS[@]}"

# --- Sim-code bans (§3.7) ---
scan 'wall clock in sim code'              '\b(DateTime|DateTimeOffset)\.(Now|UtcNow|Today)\b' '' "${SIM_DIRS[@]}"
scan 'AsParallel'                          '\bAsParallel\b'                                '' "${SIM_DIRS[@]}"
scan 'unordered Parallel.*'                '\bParallel\.(For(Each)?(Async)?|Invoke)\b'     '' "${SIM_DIRS[@]}"
scan 'GetHashCode() as logic input'        '\bGetHashCode\s*\('                            'gate:allow-gethashcode' "${SIM_DIRS[@]}"

if [[ "$fail" -ne 0 ]]; then
  echo 'check-banned-constructs: FAILED — see matches above.'
  exit 1
fi
echo 'check-banned-constructs: OK — no banned constructs found.'
