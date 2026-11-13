#!/bin/bash
# Resource monitor (orchestrator): every 15 s, free memory, free disk and the top 3 processes by RSS, appended to a log on disk,
# so a container restart leaves evidence of what the machine looked like just before it.
L=/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/resmon.log
while true; do
  printf '%s mem_avail_MB=%s disk_free_MB=%s load=%s top=%s\n' "$(date -u +%H:%M:%S)" \
    "$(free -m | awk 'NR==2{print $7}')" "$(df -m / | awk 'NR==2{print $4}')" "$(cut -d' ' -f1 /proc/loadavg)" \
    "$(ps -eo rss=,comm= --sort=-rss | head -3 | awk '{printf "%s:%dMB,", $2, $1/1024}')" >> "$L"
  sleep 15
done
