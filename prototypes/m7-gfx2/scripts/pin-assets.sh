#!/usr/bin/env bash
# Pin external assets into assets/manifest.tsv: download each PATH from an immutable BASE URL, hash it, place it at
# $G2_ASSETS/raw/<DEST-PREFIX><PATH>, and add one manifest row per file (or one row for the whole group with
# --group, per-file hashes going to assets/lists/<NAME>.sha256).
#
# Usage:
#   scripts/pin-assets.sh --source ID --base URL/ --dest PREFIX/ --role ROLE --spdx SPDX --status STATUS \
#                         --author TEXT [--jobs N] PATH...
#   scripts/pin-assets.sh ... --group NAME --paths-file FILE        (paths, one per line, relative to BASE)
#
# BASE must be pinned (GitHub raw/media URL at a 40-hex commit, a PyPI file path or an npm tarball) on an
# allowlisted host (scripts/assets-common.sh). Re-running with the same arguments is a no-op when the files and
# rows already match; a row whose pinned sha256 would change is an error (exit 1). Licence evidence for every
# source goes into assets/PROVENANCE.md by hand.
set -euo pipefail
# shellcheck source=assets-common.sh
. "$(dirname "$0")/assets-common.sh"

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
source_id='' base='' prefix='' role='' spdx='' status='' author='' group='' pathsfile='' JOBS=6
paths=()
while [ $# -gt 0 ]; do
  case "$1" in
    --source) source_id=$2; shift 2 ;;
    --base) base=$2; shift 2 ;;
    --dest) prefix=$2; shift 2 ;;
    --role) role=$2; shift 2 ;;
    --spdx) spdx=$2; shift 2 ;;
    --status) status=$2; shift 2 ;;
    --author) author=$2; shift 2 ;;
    --group) group=$2; shift 2 ;;
    --paths-file) pathsfile=$2; shift 2 ;;
    --jobs) JOBS=$2; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    --) shift; paths+=("$@"); break ;;
    -*) echo "pin-assets: unknown option $1" >&2; exit 2 ;;
    *) paths+=("$1"); shift ;;
  esac
done
if [ -n "$pathsfile" ]; then while IFS= read -r p; do [ -n "$p" ] && paths+=("$p"); done < "$pathsfile"; fi

die() { echo "pin-assets: $*" >&2; exit 2; }
[ -n "$source_id" ] && [ -n "$base" ] && [ -n "$prefix" ] && [ -n "$role" ] && [ -n "$spdx" ] && [ -n "$status" ] \
  && [ -n "$author" ] || { usage >&2; exit 2; }
[ ${#paths[@]} -gt 0 ] || die "no paths given"
case "$status" in SHIP-OK|PROTOTYPE-ONLY|REJECTED) ;; *) die "status must be SHIP-OK, PROTOTYPE-ONLY or REJECTED" ;; esac
case "$role" in asset|tool|evidence|reference) ;; *) die "role must be asset, tool, evidence or reference" ;; esac
case "$base" in */) ;; *) die "--base must end with /" ;; esac
case "$prefix" in */) ;; *) die "--dest must end with /" ;; esac
case "$prefix" in /*|*..*) die "--dest must be a relative path without '..'" ;; esac
url_allowed "$base" || die "host not allowlisted: $(url_host "$base")"
url_pinned "$base" || die "base URL is not pinned (needs a 40-hex commit, a PyPI file path or an npm tarball): $base"
for p in "${paths[@]}"; do case "$p" in /*|*..*|*$'\t'*) die "bad path: $p" ;; esac; done

mkdir -p "$RAW" "$LISTDIR"
[ -f "$MANIFEST" ] || printf '# dest\tkind\trole\turl\tsha256\tbytes\tspdx\tstatus\tsource\tauthor\tlist\n' > "$MANIFEST"

# Download (or re-hash, when already present) every path; workers print "<sha256>\t<bytes>\t<path>".
pin_one() {
  local p="$1" dest="$RAW/$prefix$1"
  if [ ! -f "$dest" ]; then
    download "$base$p" "$dest" || { printf 'FAIL\t0\t%s\n' "$p"; return 1; }
  fi
  printf '%s\t%s\t%s\n' "$(sha_of "$dest")" "$(size_of "$dest")" "$p"
}
export -f pin_one download url_allowed url_host sha_of size_of
export RAW prefix base
export ALLOWED_HOSTS_STR="${ALLOWED_HOSTS[*]}"
results="$(mktemp "${TMPDIR:-/tmp}/pin-assets.XXXXXX")"
trap 'rm -f -- "$results"' EXIT
set +e
printf '%s\0' "${paths[@]}" | xargs -0 -P "$JOBS" -I{} bash -c 'ALLOWED_HOSTS=($ALLOWED_HOSTS_STR); pin_one "$1"' _ {} > "$results"
rc=$?
set -e
if [ $rc -ne 0 ] || grep -q '^FAIL' "$results"; then
  echo "pin-assets: downloads failed:" >&2; grep '^FAIL' "$results" | cut -f3 >&2; exit 1
fi
sort -t $'\t' -k3,3 -o "$results" "$results"

# add_row DEST KIND URL SHA BYTES LIST: append, or accept an identical existing row; refuse a changed pin.
add_row() {
  local dest="$1" kind="$2" url="$3" sha="$4" bytes="$5" list="$6" old
  old="$(awk -F'\t' -v d="$dest" '$1==d {print $5; exit}' "$MANIFEST")"
  if [ -n "$old" ]; then
    [ "$old" = "$sha" ] && return 0
    echo "pin-assets: $dest is already pinned to $old, refusing $sha" >&2; return 1
  fi
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$dest" "$kind" "$role" "$url" "$sha" "$bytes" "$spdx" \
    "$status" "$source_id" "$author" "$list" >> "$MANIFEST"
}

if [ -n "$group" ]; then
  case "$group" in *[!A-Za-z0-9._-]*) die "bad group name: $group" ;; esac
  lf="$LISTDIR/$group.sha256"
  tmpl="$(mktemp "${TMPDIR:-/tmp}/pin-list.XXXXXX")"
  awk -F'\t' '{printf "%s  %s\n", $1, $3}' "$results" > "$tmpl"
  if [ -f "$lf" ] && ! cmp -s "$tmpl" "$lf"; then rm -f -- "$tmpl"; die "list $lf exists with different content"; fi
  mv -f -- "$tmpl" "$lf"
  total="$(awk -F'\t' '{s+=$2} END {printf "%d", s}' "$results")"
  add_row "$prefix" list "$base" "$(sha_of "$lf")" "$total" "lists/$group.sha256"
  echo "pin-assets: group $group: $(wc -l < "$lf") files, $total bytes -> $prefix"
else
  while IFS=$'\t' read -r sha bytes p; do add_row "$prefix$p" file "$base$p" "$sha" "$bytes" ""; done < "$results"
  echo "pin-assets: $(wc -l < "$results") files -> $prefix"
fi
