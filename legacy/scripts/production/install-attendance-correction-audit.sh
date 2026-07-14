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
BACKUP_DIR="$ROOT/backups/attendance-correction-audit"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/before-attendance-correction-audit-$STAMP.dump"
echo "Creating backup before installing attendance correction audit: $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"
echo "Copying SQL audit installer into Postgres container..."
docker cp scripts/production/sql/install-attendance-correction-audit.sql "$PG_CONTAINER:/tmp/install-attendance-correction-audit.sql"
echo "Installing audit triggers on DB=$PG_DB USER=$PG_USER..."
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/install-attendance-correction-audit.sql
echo "Done. Backup saved at: $BACKUP_FILE"
