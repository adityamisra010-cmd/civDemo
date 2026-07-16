#!/usr/bin/env bash
# Banned-constructs check — docs/m0-kernel-spec.md §3.7 / CLAUDE.md law 5.
#
# Greps C# sources for determinism-breaking constructs. Runs locally
# (./scripts/check-banned-constructs.sh) and as a CI gate on every push.
#
# Greppable items covered here:
#   System.Random (any project) - all randomness goes through RngRegistry (PCG32)
#   float         (any project) - D-004: float is banned project-wide
#   DateTime.Now/UtcNow (sim code)
#   AsParallel / unordered Parallel.* (sim code)
#   GetHashCode() as logic input (sim code; override declarations are allowed)
#
# NOT greppable by regex - enforced by review instead:
#   Dictionary/HashSet iteration in sim logic; culture-sensitive parse/format
#   (mitigated by InvariantGlobalization in Directory.Build.props); LINQ in hot paths.
set -uo pipefail
cd "$(dirname "$0")/.."

SIM_DIRS=(Sim.Core Sim.Data Sim.Cli)                 # simulation code: full ban list
ALL_DIRS=(Sim.Core Sim.Data Sim.Cli Sim.Tests)       # project-wide bans

fail=0

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

# Project-wide bans
scan 'float (D-004: banned project-wide)'  '\bfloat\b'                                        '' "${ALL_DIRS[@]}"
scan 'System.Random (use RngRegistry)'     'System\.Random|\bnew Random\b|Random\.Shared'     '' "${ALL_DIRS[@]}"

# Sim-code bans (§3.7)
scan 'DateTime.Now/UtcNow in sim code'     'DateTime\.(Now|UtcNow)'                           '' "${SIM_DIRS[@]}"
scan 'AsParallel'                          '\bAsParallel\b'                                   '' "${SIM_DIRS[@]}"
scan 'unordered Parallel.*'                '\bParallel\.(For|ForEach|ForEachAsync|Invoke)\b'  '' "${SIM_DIRS[@]}"
scan 'GetHashCode() as logic input'        '\bGetHashCode\s*\('                               'override' "${SIM_DIRS[@]}"

if [[ "$fail" -ne 0 ]]; then
  echo 'check-banned-constructs: FAILED — see matches above.'
  exit 1
fi
echo 'check-banned-constructs: OK — no banned constructs found.'
