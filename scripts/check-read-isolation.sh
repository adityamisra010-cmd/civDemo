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
#   Sim.Core/Systems/Colonization/             - T4.4 dynamic founding, the SAME
#     reason as WorldFounding above and no more: a newly founded settlement needs
#     a GrievanceRow per class or NeedsGrievanceSystem never accrues anything for
#     it (it iterates existing rows only). Colonization CREATES those rows at 0.0
#     and never reads one, so D-021's property - grievance drives no BEHAVIOUR
#     until M5 - is untouched. This is the second founding path, not a new reader.
#   Sim.Core/Systems/PathBuild/PathBuildSystem.cs - NetworkOverlayView interface
#     completeness ONLY (forwards prev tables; routes over network tables, never
#     reads needs state — documented at the forwarding lines)
#   Sim.Core/Kernel/ReplayReport.cs            - T3.12a replay diagnostic reporter:
#     a READ-ONLY observer OUTSIDE the determinism surface (ADR-009). It writes
#     JSONL from tables the owning systems already wrote and is consulted by NO
#     system - the same standing UI and the chronicle have, and the reason the
#     T2.6 rule exists (grievance must drive no BEHAVIOUR until M5, D-021) is
#     untouched by something that only prints it. The allowlist predates
#     reporters and had no entry for one; T3.12a landed and this gate has been
#     RED ON main ever since, taking the whole build-and-test job with it.
#   Sim.Ui/, Sim.Ui.Tests/, Sim.Tests/         - display + tests (packet-sanctioned)
#
# WHAT THIS GUARD ACTUALLY MATCHES, stated because it is weaker than its name
# (measured at the T4.1e follow-up): a bare grep for four identifiers. It does
# NOT distinguish a WRITE from a READ, and it does NOT distinguish sim code from
# reporting code or from PROSE - two of the seven lines it flagged on
# ReplayReport.cs were DOC COMMENT text. A path allowlist is therefore the only
# lever it has, and every future reporter, exporter or debug dump will trip it
# for the same non-reason. Same class as the FirstReign ordering finding: the
# guard's name claims more than its mechanism delivers. Not fixed here.
set -uo pipefail
cd "$(dirname "$0")/.."

PATTERN='\bGrievances\b|\bNeedSatisfactions\b|\bGrievanceRow\b|\bNeedSatisfactionRow\b'
ALLOW='^(Sim\.Core/Systems/NeedsGrievance/|Sim\.Core/State/WorldState\.cs|Sim\.Core/Kernel/CanonicalSchema\.cs|Sim\.Core/SystemCatalog\.cs|Sim\.Core/Worldgen/WorldFounding\.cs|Sim\.Core/Systems/Colonization/|Sim\.Core/Systems/PathBuild/PathBuildSystem\.cs|Sim\.Core/Kernel/ReplayReport\.cs)'

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
