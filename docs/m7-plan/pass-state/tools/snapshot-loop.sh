#!/bin/bash
# Runs the pass-state snapshot every 30 minutes, three times (about 91 minutes, inside the harness's 2-hour limit for a
# background command), then exits so the orchestrator is re-invoked and starts it again.
S=/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad
for i in 1 2 3; do
  sleep 1800
  bash "$S/m7gov/snapshot.sh" >> "$S/m7gov/snapshot.log" 2>&1
  tail -1 "$S/m7gov/snapshot.log"
done
