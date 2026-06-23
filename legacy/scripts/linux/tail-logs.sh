#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
SERVICE="${2:-}"
if [[ -n "$SERVICE" ]]; then
  docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml logs -f --tail=200 "$SERVICE"
else
  docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml logs -f --tail=200 api web postgres
fi
