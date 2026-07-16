#!/usr/bin/env bash
# T0.2 acceptance gate — mutation through IReadOnlyWorldState must NOT compile.
#
# Builds Sim.Tests.ReadOnlyViolation (deliberately excluded from Sim.slnx) and
# passes only when compilation FAILS with the expected diagnostics:
#   CS0200 — read-only indexer cannot be assigned to
#   CS1061 — no Add/Ref mutation members on IReadOnlyTable<T>
# A build that succeeds, or fails for any other reason, fails this gate.
set -uo pipefail
cd "$(dirname "$0")/.."

out=$(dotnet build Sim.Tests.ReadOnlyViolation/Sim.Tests.ReadOnlyViolation.csproj --configuration Release 2>&1)
status=$?

if [[ $status -eq 0 ]]; then
  echo 'check-readonly-proof: FAILED — mutation through IReadOnlyWorldState COMPILED.'
  echo 'The read-only view contract (kernel contract §3.1) has been broken.'
  exit 1
fi

if grep -q 'CS0200' <<<"$out" && grep -q 'CS1061' <<<"$out"; then
  echo 'check-readonly-proof: OK — mutation attempts fail to compile (CS0200, CS1061).'
  exit 0
fi

echo 'check-readonly-proof: FAILED — build failed, but not with the expected diagnostics:'
echo "$out"
exit 1
