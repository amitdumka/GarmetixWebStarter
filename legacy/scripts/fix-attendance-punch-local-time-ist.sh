#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"

ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}

PG_DB="$(get_env_value POSTGRES_DB || true)"
PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"
PG_USER="${PG_USER:-garmetix}"

BACKUP_DIR="$ROOT/backups/attendance-timezone-repair"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/before-attendance-ist-repair-$STAMP.dump"

echo "Creating DB backup before attendance IST time repair:"
echo "  $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"

echo "Copying SQL repair script into Postgres container..."
docker cp scripts/data-fixes/fix-attendance-punch-local-time-ist.sql "$PG_CONTAINER:/tmp/fix-attendance-punch-local-time-ist.sql"

echo "Running attendance IST local time repair on DB=$PG_DB USER=$PG_USER ..."
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/fix-attendance-punch-local-time-ist.sql

echo
echo "Attendance IST local time repair completed."
echo "Backup saved at: $BACKUP_FILE"
echo
echo "Restart API if needed:"
echo "COMPOSE_PROJECT_NAME=garmetix docker compose --env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml restart api"
