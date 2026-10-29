#!/usr/bin/env bash
# Tiered, change-aware validation (docs/validation-policy.md).
#   scripts/validate.sh targeted [--plan] [--paths P...] [--scope CAT...] [--base REF]
#   scripts/validate.sh integration [...]
#   scripts/validate.sh full [--heavy] [--strict]
# Without --paths/--scope the changed files are the working tree vs HEAD (or vs --base) plus untracked files.
set -euo pipefail
cd "$(dirname "$0")/.."
tier="${1:-}"
case "$tier" in
  targeted|integration|full) shift ;;
  *) echo "usage: scripts/validate.sh {targeted|integration|full} [options]  (see scripts/tiered_validation.py --help)" >&2; exit 2 ;;
esac
exec python3 scripts/tiered_validation.py --tier "$tier" "$@"
