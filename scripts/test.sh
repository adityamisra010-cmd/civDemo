#!/usr/bin/env bash
# Local test runner with FLAKE CAPTURE (director ruling after a T1.6 one-off
# failure whose name was lost): every run logs a trx; on failure the failed
# test names and any FsCheck replay seed are printed and preserved. FsCheck
# seeds are deliberately NOT pinned — random exploration is the value; capture
# is the fix. Use this for all local runs instead of bare `dotnet test`.
set -uo pipefail
cd "$(dirname "$0")/.."

RESULTS_DIR="${TEST_RESULTS_DIR:-TestResults}"
mkdir -p "$RESULTS_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"   # wall clock is fine here: tooling, not sim code
TRX="$RESULTS_DIR/run-$STAMP.trx"
LOG="$RESULTS_DIR/run-$STAMP.log"

dotnet test Sim.slnx -c Debug --logger "trx;LogFileName=$(basename "$TRX")" \
    --results-directory "$RESULTS_DIR" "$@" 2>&1 | tee "$LOG"
status=${PIPESTATUS[0]}

if [ "$status" -ne 0 ]; then
  echo ""
  echo "=== FLAKE CAPTURE ($TRX) ==="
  # Failed test names from the trx (no external tools: grep the XML directly).
  grep -o 'testName="[^"]*" [^>]*outcome="Failed"' "$TRX" 2>/dev/null \
    | sed 's/testName="\([^"]*\)".*/FAILED: \1/' | sort -u
  # xUnit puts failure detail (incl. FsCheck seeds) in the console log too.
  grep -E "Replay|StdGen|seed" "$LOG" | head -10
  echo "full detail preserved in: $TRX and $LOG"
fi
exit "$status"
