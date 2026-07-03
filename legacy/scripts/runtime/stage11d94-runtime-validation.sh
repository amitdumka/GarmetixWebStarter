#!/usr/bin/env bash
set -euo pipefail

MODE="${1:---smoke}"
PROJECT_ROOT="${2:-/opt/garmetix/current}"
API_PORT="${API_PORT:-5080}"
EXPECTED_VERSION="4.12.09"

cd "$PROJECT_ROOT"

if docker compose version >/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE="docker-compose"
else
  echo "docker compose not found" >&2
  exit 1
fi

case "$MODE" in
  --build)
    $COMPOSE build --no-cache
    $COMPOSE up -d
    ;;
  --smoke|--db-only)
    ;;
  *)
    echo "Usage: $0 [--smoke|--build|--db-only] [/opt/garmetix/current]" >&2
    exit 2
    ;;
esac

POSTGRES_CONTAINER="$($COMPOSE ps -q postgres)"
if [[ -z "$POSTGRES_CONTAINER" ]]; then
  echo "Could not find postgres container from docker compose." >&2
  exit 1
fi

echo "Checking PostgreSQL readiness..."
docker exec "$POSTGRES_CONTAINER" sh -lc 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

echo "Ensuring SalesInvoices.Remarks exists..."
docker cp scripts/production/sql/stage11d94-sales-invoices-remarks-repair.sql "$POSTGRES_CONTAINER:/tmp/stage11d94-sales-invoices-remarks-repair.sql"
docker exec "$POSTGRES_CONTAINER" sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/stage11d94-sales-invoices-remarks-repair.sql'

if [[ "$MODE" != "--db-only" ]]; then
  if command -v curl >/dev/null 2>&1; then
    echo "Checking app-info version..."
    APP_INFO="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/app-info" || true)"
    echo "$APP_INFO" | head -c 1200 || true
    echo
    if ! echo "$APP_INFO" | grep -q "$EXPECTED_VERSION"; then
      echo "WARNING: API app-info did not show expected version $EXPECTED_VERSION. You may still be running an older container."
    fi

    echo "Checking billing sales endpoint..."
    curl -fsS "http://127.0.0.1:${API_PORT}/api/billing/sales?datePreset=today&page=1&pageSize=50" >/tmp/stage11d94-sales.json || echo "WARNING: billing sales endpoint still failed. Check API logs."

    echo "Checking recent sales endpoint..."
    curl -fsS "http://127.0.0.1:${API_PORT}/api/billing/sales/recent" >/tmp/stage11d94-recent-sales.json || echo "WARNING: recent sales endpoint still failed. Check API logs."

    echo "Checking invoice replacement pending endpoint..."
    curl -fsS "http://127.0.0.1:${API_PORT}/api/invoice-replacements/pending?take=150" >/tmp/stage11d94-replacements.json || echo "WARNING: invoice replacement endpoint still failed. Check API logs."
  else
    echo "curl not installed; skipped HTTP endpoint checks."
  fi
fi

echo "Stage 11D-94 runtime validation completed."
