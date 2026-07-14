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

BACKUP_DIR="$ROOT/backups/gst-split-recalc"
REPORT_DIR="$ROOT/reports/gst-split-recalc"
mkdir -p "$BACKUP_DIR" "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/before-gst-split-recalc-$STAMP.dump"
REPORT_FILE="$REPORT_DIR/gst-split-recalc-$STAMP.txt"

echo "Creating database backup before GST split recalculation:"
echo "  $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"

docker cp scripts/production/sql/recalculate-gst-split-by-gstin.sql "$PG_CONTAINER:/tmp/recalculate-gst-split-by-gstin.sql"

echo "Running GST split recalculation from GSTIN state codes..."
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/recalculate-gst-split-by-gstin.sql | tee "$REPORT_FILE"

echo
echo "Report saved: $REPORT_FILE"
echo "Backup saved: $BACKUP_FILE"
