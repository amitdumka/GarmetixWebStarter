#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

COMPOSE=(docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml)
if [[ -f deploy/docker-compose.cloudflare.yml ]]; then
  COMPOSE+=(-f deploy/docker-compose.cloudflare.yml)
fi

"${COMPOSE[@]}" ps
"${COMPOSE[@]}" exec -T api sh -lc 'find /app/backups -maxdepth 1 -type f -printf "%TY-%Tm-%Td %TH:%TM %10s %f\n" | sort -r | head -30'
"${COMPOSE[@]}" exec -T api sh -lc 'df -h /app/backups && du -sh /app/backups || true'
