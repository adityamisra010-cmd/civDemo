#!/usr/bin/env bash
# ramdisk-restore.sh: run once after a container restart, before resuming G2, lane OC or lane RT2.
#
# On 2026-11-13 the session disk had about 200 MB free, so the orchestrator put new clones and build outputs on
# the RAM disk (/dev/shm/ram) behind symlinks. A container restart keeps the disk but empties /dev/shm: the
# symlinks then point at nothing. This script recreates their targets and restores the two clones that lived
# there with work not yet on GitHub as a branch:
#   - G2's polish round 3 (/home/user/g2w/polish3);
#   - lane RT2 (/home/user/m7w/RT2).
# It restores each from its bundle and its patch of uncommitted edits, saved at the last stop (2026-11-13 13:1x UTC).
# Lane OC's clone is on the disk; only its build outputs were on the RAM disk.
#
# It is idempotent and never deletes anything: a target that exists is kept as it is.
# Override SP, RAM, G2W or DEV through the environment (the restore test runs it against a scratch RAM root).
set -euo pipefail
SP=${SP:-/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad}
RAM=${RAM:-/dev/shm/ram}
G2W=${G2W:-/home/user/g2w}
DEV=${DEV:-/home/user/civDemo}

# The state saved at the last stop: update these four lines whenever a stop saves newer bundles and patches.
P3_BUNDLE=${P3_BUNDLE:-$SP/m7gfx2/bundles/polish3-9dbf72d.bundle}            # 1601c74..9dbf72d (g2/polish3)
P3_PATCH=${P3_PATCH:-$SP/m7gfx2/bundles/polish3-uncommitted-9dbf72d.patch}
RT2_BUNDLE=${RT2_BUNDLE:-$SP/m7c/bundles/RT2-50e4642.bundle}                # 3bdea00..50e4642 (lane/RT2)
RT2_PATCH=${RT2_PATCH:-$SP/m7c/bundles/RT2-uncommitted-50e4642.patch}

# 1. Empty folders behind the symlinks. Build outputs, captures and verify clones are remade by their stages.
for d in g2w/polish3-work g2w/scratch-integ7 m7w/verify-OC-r2 m7w/verify-OC-r2b captures/polish-3 m7w/RT2 m7w/verify-RT2-r1 m7w/verify-RT2-r1b m7w/verify-RT2-r2 m7w/verify-RT2-r2b; do
  mkdir -p "$RAM/$d"
done
for p in Proto.DetMath Proto.DetMath.Tests Proto.M7 Proto.M7.Bench Proto.M7.Econ Proto.M7.Econ.Tests Proto.M7.Tests; do
  mkdir -p "$RAM/oc-build/$p/bin" "$RAM/oc-build/$p/obj"   # lane OC's clone links its Proto.*/bin and obj here
done

# 2. G2 polish round 3: clone the trunk (on disk, g2/trunk at f677707), fetch the bundle as branch g2/polish3,
#    check it out, then apply the uncommitted edits.
P3=$RAM/g2w/polish3
if [ -d "$P3/.git" ]; then
  echo "kept $P3 at $(git -C "$P3" rev-parse --short HEAD)"
else
  git -C "$G2W/trunk" bundle verify -q "$P3_BUNDLE" >/dev/null 2>&1 || { echo "bundle $P3_BUNDLE does not verify against $G2W/trunk" >&2; exit 1; }
  git clone -q --no-checkout "$G2W/trunk" "$P3"
  git -C "$P3" fetch -q "$P3_BUNDLE" "HEAD:g2/polish3"
  git -C "$P3" checkout -q g2/polish3
  git -C "$P3" apply --binary "$P3_PATCH"
  echo "restored $P3 at $(git -C "$P3" rev-parse --short HEAD) + $(basename "$P3_PATCH")"
fi

# 3. Lane RT2: a shared clone of the development checkout (as the lane made it), the bundle as branch lane/RT2,
#    then the uncommitted edits. Step 1 left an empty folder; git clones into an empty folder.
R2=$RAM/m7w/RT2
if [ -e "$R2/.git" ]; then
  echo "kept $R2 at $(git -C "$R2" rev-parse --short HEAD)"
else
  git -C "$DEV" bundle verify -q "$RT2_BUNDLE" >/dev/null 2>&1 || { echo "bundle $RT2_BUNDLE does not verify against $DEV" >&2; exit 1; }
  git clone -q --shared --no-checkout "$DEV" "$R2"
  git -C "$R2" fetch -q "$RT2_BUNDLE" "HEAD:lane/RT2"
  git -C "$R2" checkout -q lane/RT2
  git -C "$R2" apply --binary "$RT2_PATCH"
  echo "restored $R2 at $(git -C "$R2" rev-parse --short HEAD) + $(basename "$RT2_PATCH")"
fi
