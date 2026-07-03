#!/usr/bin/env bash
set -Eeuo pipefail
SERVICE="${1:-}"
if [[ -n "$SERVICE" ]]; then
  docker logs -f --tail=200 "garmetix-${SERVICE}-1"
else
  docker logs --tail=120 garmetix-api-1 || true
  docker logs --tail=120 garmetix-web-1 || true
  docker logs --tail=120 garmetix-cloudflared-1 || true
fi
