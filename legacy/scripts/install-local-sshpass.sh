#!/usr/bin/env bash
set -Eeuo pipefail
if command -v sshpass >/dev/null 2>&1; then
  echo "sshpass already installed"
  exit 0
fi
if command -v apt-get >/dev/null 2>&1; then
  sudo apt-get update
  sudo apt-get install -y sshpass
else
  echo "Install sshpass manually for your OS, or use SSH keys." >&2
  exit 1
fi
