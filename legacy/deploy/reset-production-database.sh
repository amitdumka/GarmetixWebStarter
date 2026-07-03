#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f .env.production ]]; then
  echo "Missing .env.production. Run deploy/create-production-env.sh first." >&2
  exit 1
fi

# shellcheck source=deploy/lib/env-file.sh
source "${ROOT_DIR}/deploy/lib/env-file.sh"
normalize_env_file .env.production

DOCKER=(docker)
if ! docker ps >/dev/null 2>&1; then
  if sudo -n docker ps >/dev/null 2>&1; then
    DOCKER=(sudo docker)
  else
    echo "Current user cannot access Docker. Re-login after Docker group update or run with sudo." >&2
    exit 1
  fi
fi

CLOUDFLARE_TUNNEL_TOKEN="$(dotenv_get .env.production CLOUDFLARE_TUNNEL_TOKEN "")"
COMPOSE_FILES=(-f docker-compose.prod.yml)
if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
  COMPOSE_FILES+=(-f deploy/docker-compose.cloudflare.yml)
fi

if [[ "${1:-}" != "--yes" ]]; then
  echo "WARNING: this will remove the Garmetix PostgreSQL Docker volume and delete all local app data."
  echo "Run: ./deploy/reset-production-database.sh --yes"
  exit 1
fi

echo "Removing Garmetix containers and PostgreSQL volumes for a clean initial migration database..."
"${DOCKER[@]}" compose --env-file .env.production -p garmetix "${COMPOSE_FILES[@]}" down --remove-orphans --volumes || true
"${DOCKER[@]}" compose --env-file .env.production -p current "${COMPOSE_FILES[@]}" down --remove-orphans --volumes || true
"${DOCKER[@]}" rm -f \
  garmetix-cloudflared-1 garmetix-web-1 garmetix-api-1 garmetix-postgres-1 \
  current-cloudflared-1 current-web-1 current-api-1 current-postgres-1 2>/dev/null || true
"${DOCKER[@]}" volume rm -f \
  garmetix_garmetix_pg current_garmetix_pg garmetix_pg \
  garmetix_postgres_data current_postgres_data postgres_data 2>/dev/null || true

set_env_var .env.production DATABASE_AUTO_MIGRATE true
set_env_var .env.production DATABASE_SCHEMA_BOOTSTRAP_MODE Migrate
set_env_var .env.production RESET_DATABASE_ON_DEPLOY false

echo "Database volume removed. Run ./deploy/run-production.sh to create a clean database from the current InitialCreate migration/schema baseline."
