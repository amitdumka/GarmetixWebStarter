#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
REPORT_DIR="$ROOT/reports/stage11d30-import-gst-validation"
mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
echo "Running base production validation..."
if [ -x scripts/production/validate-production-data.sh ]; then
  scripts/production/validate-production-data.sh || true
fi
echo "Running Stage 11D-30 GST/import validation..."
docker cp scripts/production/sql/stage11d30-gst-import-validation.sql "$PG_CONTAINER:/tmp/stage11d30-gst-import-validation.sql"
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/stage11d30-gst-import-validation.sql | tee "$REPORT_DIR/stage11d30-import-gst-validation-$STAMP.txt"
echo "Report saved to $REPORT_DIR/stage11d30-import-gst-validation-$STAMP.txt"
