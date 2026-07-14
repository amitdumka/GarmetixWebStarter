#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value(){ local key="$1"; grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
BACKUP_FILE="${2:-$(find "$ROOT/backups" -type f -name "*.dump" ! -name "restore-*.dump" -printf "%T@ %p\n" 2>/dev/null | sort -rn | head -1 | cut -d" " -f2- || true)}"
if [ -z "$BACKUP_FILE" ] || [ ! -f "$BACKUP_FILE" ]; then echo "No backup found. Run stage11d33-backup-now.sh first." >&2; exit 1; fi
DRILL_DB="garmetix_restore_drill_$(date +%Y%m%d_%H%M%S)"
REPORT_DIR="$ROOT/reports/stage11d33-restore-drill"
mkdir -p "$REPORT_DIR"
echo "Restoring backup into temporary drill DB: $DRILL_DB"
docker exec "$PG_CONTAINER" createdb -U "$PG_USER" "$DRILL_DB"
docker cp "$BACKUP_FILE" "$PG_CONTAINER:/tmp/garmetix-restore-drill.dump"
docker exec "$PG_CONTAINER" pg_restore -U "$PG_USER" -d "$DRILL_DB" --no-owner --role="$PG_USER" /tmp/garmetix-restore-drill.dump
REPORT="$REPORT_DIR/restore-drill-$DRILL_DB.txt"
{
  echo "Restore drill OK"
  echo "Backup: $BACKUP_FILE"
  echo "Drill DB: $DRILL_DB"
  echo "Time: $(date -Is)"
  docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$DRILL_DB" -c 'select count(*) as companies from "Companies";' || true
  docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$DRILL_DB" -c 'select count(*) as stores from "Stores";' || true
  docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$DRILL_DB" -c 'select count(*) as products from "Products";' || true
} | tee "$REPORT"
echo "Dropping temporary drill DB: $DRILL_DB"
docker exec "$PG_CONTAINER" dropdb -U "$PG_USER" "$DRILL_DB"
echo "Restore drill report: $REPORT"
