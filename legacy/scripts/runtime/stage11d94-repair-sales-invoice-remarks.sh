#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${1:-/opt/garmetix/current}"
cd "$PROJECT_ROOT"

if docker compose version >/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE="docker-compose"
else
  echo "docker compose not found" >&2
  exit 1
fi

POSTGRES_CONTAINER="$($COMPOSE ps -q postgres)"
if [[ -z "$POSTGRES_CONTAINER" ]]; then
  echo "Could not find postgres container from docker compose." >&2
  exit 1
fi

SQL_FILE="scripts/production/sql/stage11d94-sales-invoices-remarks-repair.sql"
if [[ ! -f "$SQL_FILE" ]]; then
  echo "Missing $SQL_FILE from project root $PROJECT_ROOT" >&2
  exit 1
fi

echo "Using project root: $PROJECT_ROOT"
echo "Using compose: $COMPOSE"
echo "Using postgres container: $POSTGRES_CONTAINER"

echo "Copying repair SQL into Postgres container..."
docker cp "$SQL_FILE" "$POSTGRES_CONTAINER:/tmp/stage11d94-sales-invoices-remarks-repair.sql"

echo "Running SalesInvoices.Remarks repair..."
docker exec "$POSTGRES_CONTAINER" sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/stage11d94-sales-invoices-remarks-repair.sql'

echo
echo "Repair completed. Restarting API so EF/runtime schema cache is clean..."
$COMPOSE restart api

echo
echo "Now test these endpoints after API is healthy:"
echo "  /api/billing/sales?datePreset=today&page=1&pageSize=50"
echo "  /api/billing/sales/recent"
echo "  /api/invoice-replacements/pending?take=150"
echo "  /api/sale-import/vyapar/imported"
