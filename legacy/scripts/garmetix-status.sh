#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}"
FILES=()
[[ -f docker-compose.prod.yml ]] && FILES+=( -f docker-compose.prod.yml )
[[ -f deploy/docker-compose.prod.yml ]] && FILES+=( -f deploy/docker-compose.prod.yml )
[[ -f deploy/docker-compose.cloudflare.yml ]] && FILES+=( -f deploy/docker-compose.cloudflare.yml )
COMPOSE_PROJECT_NAME="$COMPOSE_PROJECT_NAME" docker compose --env-file .env.production "${FILES[@]}" ps
curl -I http://127.0.0.1:3000 || true
curl -I http://127.0.0.1:5080/api/health || true
docker logs --tail=40 garmetix-cloudflared-1 2>/dev/null | grep -E 'Starting tunnel|Registered tunnel|Provided Tunnel token|ERR|WRN' || true
