#!/usr/bin/env bash
# Session bootstrap for ephemeral containers (Claude Code remote resets every session).
# Installs the .NET 10 SDK from the Ubuntu archive — the direct Microsoft hosts
# (dot.net, builds.dotnet.microsoft.com) are blocked by the session egress proxy,
# but archive.ubuntu.com carries dotnet-sdk-10.0 on Ubuntu 24.04+.
# Idempotent: exits fast if a .NET 10 SDK is already present.
set -euo pipefail

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
