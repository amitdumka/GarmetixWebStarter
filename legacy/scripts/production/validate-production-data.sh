#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"

ENV_FILE="${ENV_FILE:-.env.production}"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
COMPANY_NAME="${COMPANY_NAME:-Aadwika Fashion}"
STORE_NAME="${STORE_NAME:-Smart Menswear}"

get_env_value() {
  local key="$1"
  if [ -f "$ENV_FILE" ]; then
    grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
  fi
}

PG_DB="$(get_env_value POSTGRES_DB || true)"
PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"
PG_USER="${PG_USER:-garmetix}"

REPORT_DIR="$ROOT/reports/production-validation"
mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT_FILE="$REPORT_DIR/production-data-validation-$STAMP.txt"

SQL_LOCAL="$ROOT/scripts/production/sql/production-data-validation.sql"
SQL_REMOTE="/tmp/production-data-validation.sql"

if [ ! -f "$SQL_LOCAL" ]; then
  echo "Missing SQL file: $SQL_LOCAL" >&2
  exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
  echo "Postgres container not running: $PG_CONTAINER" >&2
  docker ps --format 'table {{.Names}}\t{{.Status}}'
  exit 1
fi

echo "Copying validation SQL into Postgres container..."
docker cp "$SQL_LOCAL" "$PG_CONTAINER:$SQL_REMOTE"

echo "Running production validation..."
echo "Company: $COMPANY_NAME"
echo "Store:   $STORE_NAME"
echo "DB:      $PG_DB"
echo "User:    $PG_USER"
echo "Report:  $REPORT_FILE"
echo

{
  echo "Production validation started: $(date -Is)"
  echo "Company: $COMPANY_NAME"
  echo "Store: $STORE_NAME"
  echo "Database: $PG_DB"
  echo "Container: $PG_CONTAINER"
  echo
  docker exec "$PG_CONTAINER" psql \
    -v ON_ERROR_STOP=1 \
    -v company_name="$COMPANY_NAME" \
    -v store_name="$STORE_NAME" \
    -U "$PG_USER" \
    -d "$PG_DB" \
    -f "$SQL_REMOTE"
} | tee "$REPORT_FILE"

echo
echo "Validation report saved:"
echo "  $REPORT_FILE"
echo
echo "Quick view of warnings/failures:"
grep -nE 'FAIL|WARN' "$REPORT_FILE" || true
