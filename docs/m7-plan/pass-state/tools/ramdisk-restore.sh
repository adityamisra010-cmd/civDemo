#!/usr/bin/env bash
# ramdisk-restore.sh: run once after a container restart, before resuming G2 or lane OC.
#
# On 2026-11-13 the session disk had about 270 MB free, so the orchestrator put new clones and build outputs on
# the RAM disk (/dev/shm/ram) behind symlinks. A container restart keeps the disk but empties /dev/shm: the
# symlinks then point at nothing. This script recreates their targets and restores the one clone that held work
# not yet on GitHub as a branch, G2's polish round 3 (/home/user/g2w/polish3), from its bundle and patch.
#
# It is idempotent and never deletes anything: a target that exists is kept as it is.
# Override SP, RAM or G2W through the environment (the restore test runs it against a scratch RAM root).
set -euo pipefail
SP=${SP:-/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad}
RAM=${RAM:-/dev/shm/ram}
G2W=${G2W:-/home/user/g2w}

# 1. Empty folders behind the symlinks. Build outputs, captures and verify clones are remade by their stages.
for d in g2w/polish3-work g2w/scratch-integ7 m7w/verify-OC-r2 m7w/verify-OC-r2b captures/polish-3 m7w/RT2 m7w/verify-RT2-r1 m7w/verify-RT2-r1b m7w/verify-RT2-r2 m7w/verify-RT2-r2b; do
  mkdir -p "$RAM/$d"
done
for p in Proto.DetMath Proto.DetMath.Tests Proto.M7 Proto.M7.Bench Proto.M7.Econ Proto.M7.Econ.Tests Proto.M7.Tests; do
  mkdir -p "$RAM/oc-build/$p/bin" "$RAM/oc-build/$p/obj"   # lane OC's clone links its Proto.*/bin and obj here
done

# 2. G2 polish round 3: clone the trunk (on disk, g2/trunk at f677707), fetch the bundle (1601c74..4f8b953) as
#    branch g2/polish3, check it out, then apply the uncommitted edits (the report draft reports/polish3.md).
P3=$RAM/g2w/polish3
B=$SP/m7gfx2/bundles/polish3-wip.bundle
U=$SP/m7gfx2/bundles/polish3-uncommitted-4f8b953.patch
if [ -d "$P3/.git" ]; then
  echo "kept $P3 at $(git -C "$P3" rev-parse --short HEAD)"
else
  git -C "$G2W/trunk" bundle verify -q "$B" >/dev/null 2>&1 || { echo "bundle $B does not verify against $G2W/trunk" >&2; exit 1; }
  git clone -q --no-checkout "$G2W/trunk" "$P3"
  git -C "$P3" fetch -q "$B" "HEAD:g2/polish3"
  git -C "$P3" checkout -q g2/polish3
  git -C "$P3" apply --binary "$U"
  echo "restored $P3 at $(git -C "$P3" rev-parse --short HEAD) + $(basename "$U") (expected 4f8b953)"
fi
