#!/bin/bash
# Pass-state auto-snapshot (Director, 2026-11-12: "Do stuff in a way that if i stop you, you are able to re continue
# without losing in your work").
#
# Captures every piece of work in flight into docs/m7-plan/pass-state/wip/, copies the orchestrator's notes into notes/,
# writes wip/SNAPSHOT.md (what each file is, its head, how to restore it, and a restore test), then commits ONLY
# docs/m7-plan/pass-state/{wip,notes,tools} with "[skip ci]" and pushes. It never touches a clone's own index or tree
# (uncommitted edits are read through a temporary index), never stages other paths, and takes the orchestrator's git lock,
# so it cannot interleave with the orchestrator's own commits. Safe to run at any time; a run with nothing new commits
# nothing.
set -u
DEV=${DEV:-/home/user/civDemo}
SP=${SP:-/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad}
G2W=${G2W:-/home/user/g2w}
G2BASE=${G2BASE:-21a181e}          # the G2 trunk commit whose prototypes/m7-gfx2 tree equals this branch's
M7W=${M7W:-/home/user/m7w}
INT=${INT:-/home/user/int}
WF=${WF:-/root/.claude/projects/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/subagents/workflows}
BRANCH=${BRANCH:-claude/new-session-yavghm}
LANES=$SP/m7gov/inflight-lanes.txt   # one line per lane in flight: KEY BASE   (clone $M7W/KEY)
INTS=$SP/m7gov/inflight-int.txt      # one line per orchestrator clone in flight: NAME BASE   (clone $INT/NAME)
PS=$DEV/docs/m7-plan/pass-state
REL=docs/m7-plan/pass-state
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)

exec 9>"$SP/m7gov/git.lock"; flock 9
TMP=$(mktemp -d "$SP/m7gov/snap.XXXXXX"); trap 'rm -rf "$TMP"' EXIT
NEW=$TMP/wip; mkdir -p "$NEW/journals"
ROWS=$TMP/rows.md; TESTS=$TMP/tests.md; : > "$ROWS"; : > "$TESTS"; SUMMARY=()

uncommitted() {  # $1 repo, $2 output patch: the clone's uncommitted edits against its HEAD, through a temporary index
  local idx="$TMP/idx"; rm -f "$idx"
  GIT_INDEX_FILE="$idx" git -C "$1" read-tree HEAD && GIT_INDEX_FILE="$idx" git -C "$1" add -A \
    && GIT_INDEX_FILE="$idx" git -C "$1" diff --cached --binary HEAD > "$2"
  rm -f "$idx"; [ -s "$2" ] || rm -f "$2"
}
row() { printf '| %s | %s | %s | %s | %s |\n' "$@" >> "$ROWS"; }

# 1. G2: the trunk's work since G2BASE, and every g2/<name> clone with commits not in the trunk or uncommitted edits.
T=$G2W/trunk
if [ -d "$T/.git" ]; then
  th=$(git -C "$T" rev-parse HEAD)
  git -C "$T" diff --binary "$G2BASE" "$th" -- prototypes/m7-gfx2 > "$NEW/g2-trunk.patch"
  uncommitted "$T" "$NEW/g2-trunk-uncommitted.patch"
  row "g2-trunk.patch" "G2 trunk $T (branch g2/trunk)" "${th:0:7}" "git diff --binary $G2BASE ${th:0:7} -- prototypes/m7-gfx2" \
      "\`git apply --binary\` on this branch's tree (its prototypes/m7-gfx2 equals $G2BASE's)"
  [ -f "$NEW/g2-trunk-uncommitted.patch" ] && row "g2-trunk-uncommitted.patch" "the trunk's uncommitted edits" "${th:0:7}" "temporary index" "after g2-trunk.patch"
  SUMMARY+=("g2 trunk ${th:0:7}")
  for d in "$G2W"/*/; do
    d=${d%/}; n=$(basename "$d"); [ "$n" = trunk ] && continue; [ -d "$d/.git" ] || continue
    [ "$(git -C "$d" rev-parse --abbrev-ref HEAD 2>/dev/null)" = "g2/$n" ] || continue
    h=$(git -C "$d" rev-parse HEAD); dirty=$(git -C "$d" status --porcelain | wc -l)
    intrunk=0; git -C "$T" merge-base --is-ancestor "$h" "$th" 2>/dev/null && intrunk=1
    [ $intrunk = 1 ] && [ "$dirty" = 0 ] && continue
    if [ $intrunk = 0 ]; then
      git -C "$d" diff --binary "$G2BASE" "$h" -- prototypes/m7-gfx2 > "$NEW/g2-$n.patch"
      row "g2-$n.patch" "G2 stage clone $d (branch g2/$n), commits not yet in the trunk" "${h:0:7}" "git diff --binary $G2BASE ${h:0:7} -- prototypes/m7-gfx2" "instead of g2-trunk.patch"
    fi
    uncommitted "$d" "$NEW/g2-$n-uncommitted.patch"
    [ -f "$NEW/g2-$n-uncommitted.patch" ] && row "g2-$n-uncommitted.patch" "G2 stage clone $d: uncommitted edits ($dirty paths)" "${h:0:7}" "temporary index" \
      "after $([ $intrunk = 1 ] && echo g2-trunk.patch || echo g2-$n.patch)"
    SUMMARY+=("g2 $n ${h:0:7} +$dirty")
  done
fi

# 2. Lanes in flight: their commits as a bundle, their uncommitted edits as a patch.
if [ -s "$LANES" ]; then
  while read -r key base; do
    [ -z "${key:-}" ] && continue; c=$M7W/$key; [ -d "$c/.git" ] || continue
    h=$(git -C "$c" rev-parse HEAD)
    if [ "$(git -C "$c" rev-parse "$base")" != "$h" ]; then
      git -C "$c" bundle create "$NEW/$key-wip.bundle" "$base..HEAD" 2>/dev/null
      row "$key-wip.bundle" "lane clone $c" "${h:0:7}" "git bundle create … $base..HEAD" "\`git fetch <bundle> HEAD:lane/$key\` in a clone of this branch"
    fi
    uncommitted "$c" "$NEW/$key-uncommitted.patch"
    [ -f "$NEW/$key-uncommitted.patch" ] && row "$key-uncommitted.patch" "lane clone $c: uncommitted edits" "${h:0:7}" "temporary index" "on lane/$key"
    SUMMARY+=("lane $key ${h:0:7}")
  done < "$LANES"
fi

# 3. The orchestrator's own clones in flight (integrations, small fixes): commits as a bundle, edits as a patch.
if [ -s "$INTS" ]; then
  while read -r name base; do
    [ -z "${name:-}" ] && continue; c=$INT/$name; [ -d "$c/.git" ] || continue
    h=$(git -C "$c" rev-parse HEAD)
    if [ "$(git -C "$c" rev-parse "$base")" != "$h" ]; then
      git -C "$c" bundle create "$NEW/int-$name.bundle" "$base..HEAD" 2>/dev/null
      row "int-$name.bundle" "orchestrator clone $c" "${h:0:7}" "git bundle create … $base..HEAD" "\`git fetch <bundle> HEAD:int/$name\`"
    fi
    uncommitted "$c" "$NEW/int-$name-uncommitted.patch"
    [ -f "$NEW/int-$name-uncommitted.patch" ] && row "int-$name-uncommitted.patch" "orchestrator clone $c: uncommitted edits" "${h:0:7}" "temporary index" "\`git apply --binary\` on ${h:0:7}"
    SUMMARY+=("int $name ${h:0:7}")
  done < "$INTS"
fi

# 4. Every workflow's journal (each finished agent's result), so a new session can read what was already done.
for j in "$WF"/*/journal.jsonl; do [ -f "$j" ] && cp "$j" "$NEW/journals/$(basename "$(dirname "$j")").jsonl"; done
row "journals/*.jsonl" "every workflow run's journal" "—" "copy" "read, to see which agents finished and what they returned"

# 5. Restore test in a fresh clone of the pushed branch (the patches and bundles as written now).
R=$TMP/restore; git clone -q --no-checkout --single-branch --branch "$BRANCH" "$DEV" "$R/c" 2>/dev/null
check() { if "$@" > /dev/null 2>&1; then echo PASS; else echo FAIL; fi; }
if [ -d "$R/c/.git" ]; then
  idx=$TMP/ridx
  for p in "$NEW"/g2-*.patch; do
    [ -f "$p" ] || continue; b=$(basename "$p")
    case "$b" in *-uncommitted.patch) continue;; esac
    rm -f "$idx"; GIT_INDEX_FILE=$idx git -C "$R/c" read-tree HEAD
    r1=$(GIT_INDEX_FILE=$idx check git -C "$R/c" apply --cached --binary "$p"); r2=""
    if [ "$b" = g2-trunk.patch ]; then
      # on top of the trunk: the trunk's own edits, and those of every stage clone whose commits are in the trunk
      for q in "$NEW"/g2-*-uncommitted.patch; do
        [ -f "$q" ] || continue; qb=$(basename "$q"); stage=${qb#g2-}; stage=${stage%-uncommitted.patch}
        [ "$stage" != trunk ] && [ -f "$NEW/g2-$stage.patch" ] && continue
        r2="$r2 + $qb $(GIT_INDEX_FILE=$idx check git -C "$R/c" apply --cached --check --binary "$q")"
      done
    else
      u=${p%.patch}-uncommitted.patch
      [ -f "$u" ] && r2=" + $(basename "$u") $(GIT_INDEX_FILE=$idx check git -C "$R/c" apply --cached --check --binary "$u")"
    fi
    echo "- $b on this branch's tree: $r1$r2" >> "$TESTS"
  done
  if [ -s "$INTS" ]; then
    while read -r name base; do
      q=$NEW/int-$name-uncommitted.patch; [ -n "${name:-}" ] && [ -f "$q" ] || continue
      h=$(git -C "$INT/$name" rev-parse HEAD); rm -f "$idx"
      if GIT_INDEX_FILE=$idx git -C "$R/c" read-tree "$h" 2>/dev/null; then
        echo "- $(basename "$q") on ${h:0:7}: $(GIT_INDEX_FILE=$idx check git -C "$R/c" apply --cached --check --binary "$q")" >> "$TESTS"
      else
        echo "- $(basename "$q"): its head ${h:0:7} is not on the branch; apply it in $INT/$name" >> "$TESTS"
      fi
    done < "$INTS"
  fi
  for bdl in "$NEW"/*.bundle; do [ -f "$bdl" ] && echo "- $(basename "$bdl"): bundle verify $(check git -C "$R/c" bundle verify "$bdl")" >> "$TESTS"; done
  rm -f "$idx"
else
  echo "- restore test NOT RUN: the branch could not be cloned" >> "$TESTS"
fi

# 6. SNAPSHOT.md.
{
  echo "# Pass-state snapshot: $NOW"
  echo
  echo "Written by \`docs/m7-plan/pass-state/tools/snapshot.sh\` every 30 minutes while work is in flight, and committed with"
  echo "\`[skip ci]\`. It holds every piece of work that is not yet on this branch as a commit, so that a stopped session or a lost"
  echo "container costs at most the work since this time. The finished work is in the branch itself (README §1)."
  echo
  echo "Branch head when written: \`$(git -C "$DEV" rev-parse --short HEAD)\`. In flight: ${SUMMARY[*]:-nothing}."
  echo
  echo "| File | What | Head | Made with | Restore |"
  echo "|---|---|---|---|---|"
  cat "$ROWS"
  echo
  echo "## Restore test (in a fresh clone of the branch, at this snapshot)"
  echo
  if [ -s "$TESTS" ]; then cat "$TESTS"; else echo "- nothing to test"; fi
  echo
  echo "## How to resume from this snapshot in a new session"
  echo
  echo "1. Run \`./scripts/bootstrap.sh\`; read \`CLAUDE.md\`, \`docs/current-state.md\` and \`docs/m7-plan/pass-state/README.md\`."
  echo "2. **G2:** cut a trunk from this branch (\`git clone\` it to \`/home/user/g2w/trunk\`, branch \`g2/trunk\`), apply"
  echo "   \`g2-trunk.patch\` with \`git apply --binary\` and commit. For a stage clone with its own patch, apply that patch instead,"
  echo "   then its \`-uncommitted\` patch. Re-fetch the assets (\`prototypes/m7-gfx2/scripts/fetch-assets.sh\`). Re-run the G2"
  echo "   workflow from README §2 with \`from\` set to the segment in flight; its agents continue from a clone that exists."
  echo "3. **Lanes:** \`git fetch <KEY>-wip.bundle HEAD:lane/<KEY>\` in a clone of this branch at \`/home/user/m7w/<KEY>\`, check"
  echo "   it out, apply \`<KEY>-uncommitted.patch\`, and re-run the lane workflow with the brief in \`args/<KEY>.json\` plus a resume"
  echo "   note. The journals show which of its agents had finished."
  echo "4. **Orchestrator clones:** apply \`int-<NAME>-uncommitted.patch\` on its head and re-run the validation the README names."
} > "$NEW/SNAPSHOT.md"

# 7. Replace wip/ with the new snapshot, copy the notes and this tool, commit only those paths, push.
rm -rf "$PS/wip"; mkdir -p "$PS/wip"; cp -r "$NEW"/. "$PS/wip/"
cp "$SP/m7gov/final-report-notes.md" "$PS/notes/final-report-notes.md" 2>/dev/null
cp "$SP/m7gov/throttle-2026-11-05.md" "$PS/notes/orchestration-log.md" 2>/dev/null
mkdir -p "$PS/tools"; cp "$SP/m7gov/snapshot.sh" "$PS/tools/snapshot.sh" 2>/dev/null
cd "$DEV" || exit 1
git add -A -- "$REL/wip" "$REL/notes" "$REL/tools"
if git diff --cached --quiet -- "$REL/wip" "$REL/notes" "$REL/tools"; then echo "$NOW snapshot: nothing new"; exit 0; fi
MSG=$TMP/msg
{
  echo "docs: pass-state auto-snapshot $NOW [skip ci]"
  echo
  echo "Work in flight, saved so that a stop loses nothing (Director, 2026-11-12):"
  for s in "${SUMMARY[@]:-nothing}"; do echo "- $s"; done
  echo
  echo "Only docs/m7-plan/pass-state/{wip,notes,tools}. Restore test: see wip/SNAPSHOT.md."
  echo
  echo "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  echo "Claude-Session: https://claude.ai/code/session_01E8f45P3v9QX9mUMHnn5E7A"
} > "$MSG"
git commit -q -F "$MSG" -- "$REL/wip" "$REL/notes" "$REL/tools" || { echo "$NOW snapshot: commit FAILED"; exit 1; }
for i in 1 2 3 4; do git push -q origin "$BRANCH" && break; sleep $((2**i)); done
echo "$NOW snapshot: $(git rev-parse --short HEAD) pushed=$([ "$(git rev-parse HEAD)" = "$(git rev-parse "origin/$BRANCH")" ] && echo yes || echo NO) (${SUMMARY[*]:-nothing}); tests: $(grep -c PASS "$TESTS") PASS, $(grep -c FAIL "$TESTS") FAIL"
