#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
REPORT_DIR="$ROOT/reports/invoice-correction-audit"; mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT_FILE="$REPORT_DIR/invoice-correction-audit-$STAMP.txt"
docker cp scripts/production/sql/invoice-correction-audit-report.sql "$PG_CONTAINER:/tmp/invoice-correction-audit-report.sql"
docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -f /tmp/invoice-correction-audit-report.sql | tee "$REPORT_FILE"
echo "Report saved: $REPORT_FILE"
