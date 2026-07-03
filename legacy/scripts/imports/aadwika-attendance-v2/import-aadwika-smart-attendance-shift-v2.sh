#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"

ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
IMPORT_DIR="scripts/imports/aadwika-attendance-v2"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}

PG_DB="$(get_env_value POSTGRES_DB || true)"
PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"
PG_USER="${PG_USER:-garmetix}"

if [ ! -f "$IMPORT_DIR/sql/aadwika-smart-attendance-shift-import-v2.sql" ]; then
  echo "Import SQL not found: $IMPORT_DIR/sql/aadwika-smart-attendance-shift-import-v2.sql" >&2
  exit 1
fi

BACKUP_DIR="$ROOT/backups/attendance-final-import-v2"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/before-attendance-final-import-v2-$STAMP.dump"

echo "Creating database backup before attendance import:"
echo "  $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"

echo "Copying Stage 11D-24 attendance import files into Postgres container..."
docker cp "$IMPORT_DIR/data/employees_shift_final.csv" "$PG_CONTAINER:/tmp/employees_shift_final.csv"
docker cp "$IMPORT_DIR/data/shift_templates.csv" "$PG_CONTAINER:/tmp/shift_templates.csv"
docker cp "$IMPORT_DIR/data/attendance_status_final.csv" "$PG_CONTAINER:/tmp/attendance_status_final.csv"
docker cp "$IMPORT_DIR/sql/aadwika-smart-attendance-shift-import-v2.sql" "$PG_CONTAINER:/tmp/aadwika-smart-attendance-shift-import-v2.sql"

echo "Running Stage 11D-24 shift-aware attendance import on DB=$PG_DB USER=$PG_USER ..."
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/aadwika-smart-attendance-shift-import-v2.sql

echo
echo "Attendance import completed. Backup saved at:"
echo "  $BACKUP_FILE"
echo
echo "Restart API so attendance screens refresh cleanly:"
echo "  COMPOSE_PROJECT_NAME=garmetix docker compose --env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml restart api"
