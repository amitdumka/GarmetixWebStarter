#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value(){ local key="$1"; grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"; PG_USER="${PG_USER:-garmetix}"
REPORT_DIR="$ROOT/reports/stage11d35-error-hotspots"
mkdir -p "$REPORT_DIR"
REPORT="$REPORT_DIR/error-hotspots-$(date +%Y%m%d-%H%M%S).txt"
docker cp scripts/production/sql/stage11d35-error-hotspots.sql "$PG_CONTAINER:/tmp/stage11d35-error-hotspots.sql"
docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -f /tmp/stage11d35-error-hotspots.sql | tee "$REPORT"
echo "Report: $REPORT"
