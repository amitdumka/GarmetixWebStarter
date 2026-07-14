#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
YEAR="${PAYROLL_YEAR:-$(date +%Y)}"
MONTH="${PAYROLL_MONTH:-$(date +%-m)}"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}
PG_DB="$(get_env_value POSTGRES_DB || true)"
PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"
PG_USER="${PG_USER:-garmetix}"
REPORT_DIR="$ROOT/reports/payroll-finalization"
mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT="$REPORT_DIR/payroll-finalization-${YEAR}-${MONTH}-${STAMP}.txt"

echo "Copying payroll finalization validation SQL..."
docker cp scripts/production/sql/payroll-finalization-validation.sql "$PG_CONTAINER:/tmp/payroll-finalization-validation.sql"

echo "Running payroll finalization validation for ${YEAR}-${MONTH}..."
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 \
  -v target_year="$YEAR" \
  -v target_month="$MONTH" \
  -v generated_at="$(date -Iseconds)" \
  -U "$PG_USER" -d "$PG_DB" \
  -f /tmp/payroll-finalization-validation.sql | tee "$REPORT"

echo
echo "Report saved: $REPORT"
echo "Set PAYROLL_YEAR=2026 PAYROLL_MONTH=6 before running for a specific month."
