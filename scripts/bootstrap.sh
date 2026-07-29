#!/usr/bin/env bash
# Session bootstrap for ephemeral containers (Claude Code remote resets every session).
# Installs the .NET 10 SDK from the Ubuntu archive — the direct Microsoft hosts
# (dot.net, builds.dotnet.microsoft.com) are blocked by the session egress proxy,
# but archive.ubuntu.com carries dotnet-sdk-10.0 on Ubuntu 24.04+.
# Idempotent: exits fast if a .NET 10 SDK is already present.
set -euo pipefail

# TOOL-DEFECT PATCH (T3.6 director ruling — second packet whose commit-order
# evidence was destroyed): the CCR stop hook's remediation text prescribes
# `git commit --amend --no-edit --reset-author`, which RESETS AUTHOR DATES and
# collapses the commit-order evidence every derivation-honesty lens depends
# on. The hook's own CHECK reads only the COMMITTER email (%ce) and signature
# presence, so a plain `--amend --no-edit` after fixing git config satisfies
# it while preserving author identity and dates. The hook lives in the
# ephemeral container home (provisioned by the CCR launcher, outside the
# repo), so this patch re-applies every session; it edits ONLY the advice
# string, never the check.
HOOK="$HOME/.claude/stop-hook-git-check.sh"
if [[ -f "$HOOK" ]] && grep -q -- '--reset-author' "$HOOK"; then
  sed -i 's|git commit --amend --no-edit --reset-author|git commit --amend --no-edit|g' "$HOOK"
  # FAIL LOUDLY (T3.6 certification queue line): a silently stale workaround is
  # worse than the defect — the next packet loses its commit-order evidence and
  # nobody notices until a derivation-honesty lens asks. If the launcher ships a
  # hook whose --reset-author advice no longer matches the pattern above, STOP
  # the bootstrap so a human re-derives the patch before any commits are made.
  if grep -q -- '--reset-author' "$HOOK"; then
    echo "bootstrap: FAILED — $HOOK still prescribes --reset-author after patching." >&2
    echo "  The launcher shipped a changed hook; the one-line patch no longer matches." >&2
    echo "  Re-derive the fix (the advice must not reset author dates — they are the" >&2
    echo "  repo's commit-order evidence) before committing anything. See docs/queue.md." >&2
    exit 1
  fi
  echo "bootstrap: patched stop-hook remediation text (preserve author dates)."
fi

if command -v dotnet >/dev/null 2>&1; then
  v="$(dotnet --version)"
  if [[ "$v" == 10.* ]]; then
    echo "bootstrap: .NET SDK $v already installed."
    exit 0
  fi
  echo "bootstrap: found .NET SDK $v — a 10.x SDK is required (D-001); installing dotnet-sdk-10.0."
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq || true   # PPA fetch failures behind the proxy are non-fatal
apt-get install -y -qq dotnet-sdk-10.0

v="$(dotnet --version)"
if [[ "$v" != 10.* ]]; then
  echo "bootstrap: FAILED — expected a 10.x SDK, got '$v'." >&2
  exit 1
fi
echo "bootstrap: .NET SDK $v ready."
