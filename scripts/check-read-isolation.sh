#!/usr/bin/env bash
# Read-isolation gate for the T2.6 needs/grievance tables — m2 spec T2.6:
# "grievance is read by nothing but UI/chronicle" (grep-level assert).
#
# Grievance accrues but ACTS only at M5 (D-021: the unrest valves ship with
# the gas pedal). Until then, any sim-side reference to the Grievances or
# NeedSatisfactions tables — or their row types — outside the allowlist below
# is a violation. Runs locally (./scripts/check-read-isolation.sh) and as a
# CI gate on every push.
#
# ALLOWLIST (each entry a reviewable reason):
#   Sim.Core/Systems/NeedsGrievance/           - the owning system
#   Sim.Core/State/WorldState.cs               - row/table declarations
#   Sim.Tests/TestUtil/WorldStates.cs          - StateEquals coverage
#   Sim.Core/Kernel/CanonicalSchema.cs         - serialization
#   Sim.Core/SystemCatalog.cs                  - ownership handout (composition root)
#   Sim.Core/Worldgen/WorldFounding.cs         - founding seeds grievance rows at 0
#   Sim.Core/Systems/PathBuild/PathBuildSystem.cs - NetworkOverlayView interface
#     completeness ONLY (forwards prev tables; routes over network tables, never
#     reads needs state — documented at the forwarding lines)
#   Sim.Ui/, Sim.Ui.Tests/, Sim.Tests/         - display + tests (packet-sanctioned)
set -uo pipefail
cd "$(dirname "$0")/.."

PATTERN='\bGrievances\b|\bNeedSatisfactions\b|\bGrievanceRow\b|\bNeedSatisfactionRow\b'
ALLOW='^(Sim\.Core/Systems/NeedsGrievance/|Sim\.Core/State/WorldState\.cs|Sim\.Core/Kernel/CanonicalSchema\.cs|Sim\.Core/SystemCatalog\.cs|Sim\.Core/Worldgen/WorldFounding\.cs|Sim\.Core/Systems/PathBuild/PathBuildSystem\.cs)'

matches=$(grep -RnE --include='*.cs' --exclude-dir=bin --exclude-dir=obj \
  "$PATTERN" Sim.Core Sim.Data Sim.Cli 2>/dev/null || true)
if [[ -n "$matches" ]]; then
  matches=$(grep -vE "$ALLOW" <<<"$matches" || true)
fi
if [[ -n "$matches" ]]; then
  printf 'READ-ISOLATION VIOLATION — grievance/needs tables referenced outside the allowlist:\n%s\n\n' "$matches"
  echo 'check-read-isolation: FAILED — grievance drives no behavior until M5 (D-021).'
  exit 1
fi
echo 'check-read-isolation: OK — needs/grievance tables read only by their owner, serialization, UI and tests.'
