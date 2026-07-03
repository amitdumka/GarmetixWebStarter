#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-$(docker compose ps -q postgres)}"
if [[ -z "$POSTGRES_CONTAINER" ]]; then
  echo "Postgres container was not found. Run from the deployed Garmetix project root." >&2
  exit 1
fi
SQL="scripts/runtime/stage11d103-purchase-vendor-payment-reconciliation-db-check.sql"
if [[ ! -f "$SQL" ]]; then
  echo "Missing SQL check: $SQL" >&2
  exit 1
fi
DB_NAME="${POSTGRES_DB:-garmetix}"
DB_USER="${POSTGRES_USER:-garmetix}"
docker compose cp "$SQL" "$POSTGRES_CONTAINER:/tmp/stage11d103-purchase-vendor-payment-reconciliation-db-check.sql"
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -f /tmp/stage11d103-purchase-vendor-payment-reconciliation-db-check.sql
