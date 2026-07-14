#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}"
FILES=()
[[ -f docker-compose.prod.yml ]] && FILES+=( -f docker-compose.prod.yml )
[[ -f deploy/docker-compose.prod.yml ]] && FILES+=( -f deploy/docker-compose.prod.yml )
[[ -f deploy/docker-compose.cloudflare.yml ]] && FILES+=( -f deploy/docker-compose.cloudflare.yml )
compose(){ COMPOSE_PROJECT_NAME="$COMPOSE_PROJECT_NAME" docker compose --env-file .env.production "${FILES[@]}" "$@"; }
echo "This will stop Garmetix and remove known Postgres volumes. Press Ctrl+C to cancel; Enter to continue."
read -r _
compose down --remove-orphans || true
docker rm -f garmetix-postgres-1 garmetix-api-1 garmetix-web-1 garmetix-cloudflared-1 current-postgres-1 current-api-1 current-web-1 current-cloudflared-1 2>/dev/null || true
for V in garmetix_postgres_data garmetix_garmetix_pg garmetix_garmetix_postgres_data garmetix_pg current_postgres_data current_garmetix_pg postgres_data; do
  docker volume rm "$V" 2>/dev/null || true
done
compose up -d --build --force-recreate
compose ps
