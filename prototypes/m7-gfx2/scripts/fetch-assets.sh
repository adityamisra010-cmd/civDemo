#!/usr/bin/env bash
# Fetch the G2 demonstrator's external assets into $G2_ASSETS/raw (default /home/user/g2w/assets/raw) from the
# pinned URLs in assets/manifest.tsv, verifying every file's sha256. Nothing fetched here is committed.
#
# Usage: scripts/fetch-assets.sh [--verify | --offline [--from DIR]] [--only SOURCE[,SOURCE...]] [--jobs N] [--summary]
#   (default)   idempotent fetch: files already present with the pinned sha256 are kept; missing or mismatched
#               files are downloaded again (allowlisted hosts only, see scripts/assets-common.sh) and verified.
#   --verify    no network and no writes: re-hash every pinned file and report.
#   --offline   no network: fill missing files from DIR (a copy of an earlier raw/ tree, same relative paths)
#               when their sha256 matches, then verify. Without --from it only reports what is missing.
#   --only      restrict to the given source ids (manifest column "source").
#   --summary   print files, bytes and status per source from the manifest, and exit.
# Exit: 0 all pinned files present and verified; 1 a sha256 mismatch or failed download; 2 usage or manifest
# error; 3 files missing (--verify / --offline).
# On a complete, unfiltered, clean run (default or --offline) it writes $G2_ASSETS/raw/MANIFEST.sha256, the
# sha256 of assets/manifest.tsv and its lists, so bake scripts can check that raw/ matches the committed pins.
set -euo pipefail
# shellcheck source=assets-common.sh
. "$(dirname "$0")/assets-common.sh"

mode=fetch only='' from='' JOBS=6
while [ $# -gt 0 ]; do
  case "$1" in
    --verify) mode=verify; shift ;;
    --offline) mode=offline; shift ;;
    --from) from=$2; shift 2 ;;
    --only) only=$2; shift 2 ;;
    --jobs) JOBS=$2; shift 2 ;;
    --summary) mode=summary; shift ;;
    -h|--help) awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; exit 0 ;;
    *) echo "fetch-assets: unknown argument $1" >&2; exit 2 ;;
  esac
done
[ -f "$MANIFEST" ] || { echo "fetch-assets: no manifest at $MANIFEST" >&2; exit 2; }
if [ -n "$from" ] && [ "$mode" != offline ]; then echo "fetch-assets: --from needs --offline" >&2; exit 2; fi
if [ -n "$from" ] && [ ! -d "$from" ]; then echo "fetch-assets: --from: no such directory $from" >&2; exit 2; fi

if [ "$mode" = summary ]; then
  # files per source/status from the expanded manifest (groups included); bytes from the manifest rows
  printf '%-16s %-15s %8s %12s\n' source status files bytes
  { expand_manifest "$only" | awk -F'\t' '{print "F\t"$4"\t"$5}'
    awk -F'\t' '!/^#/ && NF>=10 {print "B\t"$9"\t"$8"\t"$6}' "$MANIFEST"; } |
  awk -F'\t' -v only="$only" '$1=="F" {n[$2"\t"$3]++} $1=="B" && (only=="" || index(","only",", ","$2",")) {b[$2"\t"$3]+=$4}
    END { for (k in n) { split(k,a,"\t"); printf "%-16s %-15s %8d %12d\n", a[1], a[2], n[k], b[k]; tn+=n[k]; tb+=b[k] }
          printf "%-16s %-15s %8d %12d\n", "~total", "", tn, tb }' | sort
  exit 0
fi

entries="$(mktemp "${TMPDIR:-/tmp}/fetch-assets.XXXXXX")"
results="$(mktemp "${TMPDIR:-/tmp}/fetch-assets-r.XXXXXX")"
trap 'rm -f -- "$entries" "$results"' EXIT
expand_manifest "$only" > "$entries" || exit 2
total=$(wc -l < "$entries")

# One worker per file; prints "<STATE>\t<dest>" with STATE in OK GOT COPIED MISSING BAD FAIL.
fetch_one() {
  local dest="$1" url="$2" sha="$3" f="$RAW/$1"
  if [ -f "$f" ]; then
    if [ "$(sha_of "$f")" = "$sha" ]; then printf 'OK\t%s\n' "$dest"; return 0; fi
    if [ "$MODE" = verify ]; then printf 'BAD\t%s\n' "$dest"; return 0; fi
  fi
  case "$MODE" in
    verify) printf 'MISSING\t%s\n' "$dest" ;;
    offline)
      if [ -n "$FROM" ] && [ -f "$FROM/$dest" ] && [ "$(sha_of "$FROM/$dest")" = "$sha" ]; then
        mkdir -p -- "$(dirname -- "$f")"; cp -f -- "$FROM/$dest" "$f.part.$$" && mv -f -- "$f.part.$$" "$f"
        printf 'COPIED\t%s\n' "$dest"
      elif [ -f "$f" ]; then printf 'BAD\t%s\n' "$dest"
      else printf 'MISSING\t%s\n' "$dest"; fi ;;
    fetch)
      if download "$url" "$f"; then
        if [ "$(sha_of "$f")" = "$sha" ]; then printf 'GOT\t%s\n' "$dest"
        else printf 'BAD\t%s\n' "$dest"; echo "fetch-assets: sha256 mismatch after download: $dest" >&2; fi
      else printf 'FAIL\t%s\n' "$dest"; fi ;;
  esac
}
export -f fetch_one download url_allowed url_host sha_of size_of
export RAW MODE="$mode" FROM="$from" ALLOWED_HOSTS_STR="${ALLOWED_HOSTS[*]}"
mkdir -p "$RAW"
cut -f1-3 "$entries" | tr '\t\n' '\0\0' | xargs -0 -n 3 -P "$JOBS" \
  bash -c 'ALLOWED_HOSTS=($ALLOWED_HOSTS_STR); fetch_one "$1" "$2" "$3"' _ > "$results" || true

count() { grep -c "^$1"$'\t' "$results" || true; }
ok=$(count OK) got=$(count GOT) copied=$(count COPIED) missing=$(count MISSING) bad=$(count BAD) fail=$(count FAIL)
done_n=$((ok + got + copied + missing + bad + fail))
echo "fetch-assets [$mode${only:+, only $only}]: $total pinned files: $ok verified, $got downloaded, $copied copied," \
     "$missing missing, $bad sha256 mismatch, $fail download failures (raw root: $RAW)"
grep -E $'^(MISSING|BAD|FAIL)\t' "$results" | sort | head -50 | sed 's/^/  /' || true
[ "$done_n" -eq "$total" ] || { echo "fetch-assets: internal error: $done_n results for $total files" >&2; exit 2; }
if [ "$bad" -gt 0 ] || [ "$fail" -gt 0 ]; then exit 1; fi
if [ "$missing" -gt 0 ]; then exit 3; fi
if [ "$mode" != verify ] && [ -z "$only" ]; then
  stamp="$( { sha_of "$MANIFEST"; for l in "$LISTDIR"/*.sha256; do [ -f "$l" ] && sha_of "$l"; done; } | sha256sum | cut -d' ' -f1)"
  printf '%s  assets/manifest.tsv + assets/lists/*.sha256\n' "$stamp" > "$RAW/MANIFEST.sha256"
fi
exit 0
