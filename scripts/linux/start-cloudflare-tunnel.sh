#!/usr/bin/env bash
set -euo pipefail
TUNNEL_NAME="${1:-garmetix}"
CONFIG_FILE="${2:-infra/cloudflare/config.yml}"

command -v cloudflared >/dev/null 2>&1 || {
  echo "cloudflared is not installed. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/" >&2
  exit 1
}

[[ -f "$CONFIG_FILE" ]] || {
  echo "Missing tunnel config: $CONFIG_FILE" >&2
  echo "Create it from infra/cloudflare/config.example.yml and set your hostname/tunnel id." >&2
  exit 1
}

cloudflared tunnel --config "$CONFIG_FILE" run "$TUNNEL_NAME"
