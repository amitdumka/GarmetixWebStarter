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
REPORT_DIR="$ROOT/reports/attendance-correction-audit"
mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT_FILE="$REPORT_DIR/attendance-correction-audit-$STAMP.txt"
docker cp scripts/production/sql/attendance-correction-audit-report.sql "$PG_CONTAINER:/tmp/attendance-correction-audit-report.sql"
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/attendance-correction-audit-report.sql | tee "$REPORT_FILE"
echo "Report saved: $REPORT_FILE"
