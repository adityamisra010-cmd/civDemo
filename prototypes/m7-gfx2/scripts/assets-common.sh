# shellcheck shell=bash
# Shared helpers for fetch-assets.sh and pin-assets.sh (sourced, not executed).
#
# Layout:
#   prototypes/m7-gfx2/assets/manifest.tsv      one row per pinned file or file group (committed)
#   prototypes/m7-gfx2/assets/lists/*.sha256    per-file sha256 listings for group rows (committed)
#   $G2_ASSETS/raw/<source-id>/<upstream path>  fetched files (NOT committed; default /home/user/g2w/assets)
#   $G2_ASSETS/baked/...                        outputs of the bake scripts (NOT committed)
#
# manifest.tsv columns (tab-separated; lines starting with '#' are comments):
#   dest    path under raw/ (kind=file) or directory prefix ending in '/' (kind=list)
#   kind    file | list
#   role    asset | tool | evidence | reference
#   url     pinned download URL (kind=file) or pinned base URL ending in '/' (kind=list)
#   sha256  of the file (kind=file) or of the list file (kind=list)
#   bytes   file size (kind=file) or total size of the group (kind=list)
#   spdx    SPDX licence id or LicenseRef-... (explained in assets/PROVENANCE.md)
#   status  SHIP-OK | PROTOTYPE-ONLY | REJECTED
#   source  source id (assets/PROVENANCE.md, section "Sources")
#   author  author / vendor
#   list    kind=list only: path of the listing under assets/ (lines "<sha256>  <path relative to dest>")

G2_HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"   # prototypes/m7-gfx2
MANIFEST="$G2_HERE/assets/manifest.tsv"
LISTDIR="$G2_HERE/assets/lists"
ASSETS="${G2_ASSETS:-/home/user/g2w/assets}"
RAW="$ASSETS/raw"

# Hosts this session can reach (checked 2026-11-09). codeload.github.com answered 403 to an archive download on
# 2026-11-09, so no manifest row uses it; Poly Haven, ambientCG, the MakeHuman file server, the CMU site and the
# github.com web UI/API are denied and are never contacted.
ALLOWED_HOSTS=(raw.githubusercontent.com media.githubusercontent.com files.pythonhosted.org registry.npmjs.org)

url_host() { local h="${1#https://}"; printf '%s' "${h%%/*}"; }

url_allowed() {
  case "$1" in https://*) ;; *) return 1 ;; esac
  local h; h="$(url_host "$1")"
  local a; for a in "${ALLOWED_HOSTS[@]}"; do [ "$h" = "$a" ] && return 0; done
  return 1
}

# A URL is "pinned" when its content cannot change: a GitHub raw/media URL at a full 40-hex commit, a PyPI file
# URL (content-addressed path) or an npm tarball URL (immutable per version).
url_pinned() {
  [[ "$1" =~ ^https://raw\.githubusercontent\.com/[^/]+/[^/]+/[0-9a-f]{40}/ ]] && return 0
  [[ "$1" =~ ^https://media\.githubusercontent\.com/media/[^/]+/[^/]+/[0-9a-f]{40}/ ]] && return 0
  [[ "$1" =~ ^https://files\.pythonhosted\.org/packages/ ]] && return 0
  [[ "$1" =~ ^https://registry\.npmjs\.org/.+/-/.+\.tgz$ ]] && return 0
  return 1
}

sha_of() { sha256sum -- "$1" | cut -d' ' -f1; }
size_of() { stat -c %s -- "$1"; }

# download URL DEST: fetch into DEST.part.<pid> next to DEST, then rename. Never follows a redirect off HTTPS.
download() {
  local url="$1" dest="$2" tmp
  url_allowed "$url" || { echo "refusing non-allowlisted URL: $url" >&2; return 4; }
  mkdir -p -- "$(dirname -- "$dest")"
  tmp="$dest.part.$$"
  if curl -sS -f -L --proto '=https' --proto-redir '=https' --max-redirs 3 --retry 3 --retry-delay 2 \
          --connect-timeout 30 --max-time 1800 -o "$tmp" -- "$url"; then
    mv -f -- "$tmp" "$dest"
  else
    local rc=$?; rm -f -- "$tmp"; return "$rc"
  fi
}

# expand_manifest: print "dest<TAB>url<TAB>sha256<TAB>source<TAB>status" for every FILE the manifest pins
# (group rows expanded through their list). Fails if a list file does not match its row's sha256.
expand_manifest() {
  local filter="${1:-}"
  local dest kind role url sha bytes spdx status source author list
  while IFS=$'\t' read -r dest kind role url sha bytes spdx status source author list; do
    [ -z "$dest" ] && continue
    case "$dest" in \#*) continue ;; esac
    if [ -n "$filter" ] && [[ ",$filter," != *",$source,"* ]]; then continue; fi
    if [ "$kind" = file ]; then
      printf '%s\t%s\t%s\t%s\t%s\n' "$dest" "$url" "$sha" "$source" "$status"
    elif [ "$kind" = list ]; then
      local lf="$G2_HERE/assets/$list"
      [ -f "$lf" ] || { echo "manifest: missing list file $list" >&2; return 1; }
      [ "$(sha_of "$lf")" = "$sha" ] || { echo "manifest: list file $list does not match its pinned sha256" >&2; return 1; }
      local fsha rel
      while read -r fsha rel; do
        [ -z "$fsha" ] && continue
        printf '%s\t%s\t%s\t%s\t%s\n' "$dest$rel" "$url$rel" "$fsha" "$source" "$status"
      done < "$lf"
    else
      echo "manifest: unknown kind '$kind' for $dest" >&2; return 1
    fi
  done < "$MANIFEST"
}
