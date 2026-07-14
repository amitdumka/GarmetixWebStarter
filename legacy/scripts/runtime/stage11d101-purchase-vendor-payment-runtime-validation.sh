#!/usr/bin/env bash
set -euo pipefail

MODE="${1:---smoke}"
PROJECT_ROOT="${2:-/opt/garmetix/current}"
API_PORT="${API_PORT:-5080}"
WEB_PORT="${WEB_PORT:-3000}"
EXPECTED_VERSION="4.12.16"
EXPECTED_STAGE="11D-101"

cd "$PROJECT_ROOT"

if docker compose version >/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE="docker-compose"
else
  echo "docker compose not found" >&2
  exit 1
fi

echo "Using project root: $PROJECT_ROOT"
echo "Using compose: $COMPOSE"

case "$MODE" in
  --build)
    echo "Running clean build before runtime validation..."
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

if [[ "$MODE" != "--db-only" ]]; then
  echo "Checking container status..."
  $COMPOSE ps

  if command -v curl >/dev/null 2>&1; then
    echo "Checking API app-info..."
    APP_INFO="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/app-info" || true)"
    echo "$APP_INFO" | head -c 1200 || true
    echo
    if ! echo "$APP_INFO" | grep -q "$EXPECTED_VERSION"; then
      echo "WARNING: /api/app-info did not show expected version $EXPECTED_VERSION. Check deployment/container cache."
    fi
    if ! echo "$APP_INFO" | grep -q "$EXPECTED_STAGE"; then
      echo "WARNING: /api/app-info did not show expected stage $EXPECTED_STAGE."
    fi

    echo "Checking purchase API reachability..."
    curl -fsS "http://127.0.0.1:${API_PORT}/api/app-info/version" >/dev/null || echo "WARNING: API version endpoint was not reachable."

    echo "Checking web root..."
    curl -fsS "http://127.0.0.1:${WEB_PORT}/" >/dev/null || echo "WARNING: web root was not reachable on port ${WEB_PORT}."
  else
    echo "curl not installed; skipping API/web HTTP smoke checks."
  fi
fi

POSTGRES_CONTAINER="$($COMPOSE ps -q postgres)"
if [[ -z "$POSTGRES_CONTAINER" ]]; then
  echo "Could not find postgres container from docker compose." >&2
  exit 1
fi

echo "Checking PostgreSQL readiness..."
docker exec "$POSTGRES_CONTAINER" sh -lc 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

echo "Copying and running Purchase/Vendor Payment DB checks..."
docker cp scripts/runtime/stage11d101-purchase-vendor-payment-db-check.sql "$POSTGRES_CONTAINER:/tmp/stage11d101-purchase-vendor-payment-db-check.sql"
docker exec "$POSTGRES_CONTAINER" sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/stage11d101-purchase-vendor-payment-db-check.sql'

echo
cat <<'EOF'
Stage 11D-101 purchase/vendor payment runtime validation completed.

Manual UI checks still required:
1. Purchase -> New Inward: create one inward with an older inward date and confirm stock movement uses that date.
2. Purchase -> Inward list: edit header/inward date and verify receipt/list reflects the date.
3. Purchase -> Vendor Payments: view one payment, edit date/amount/reference/bank, then verify invoice status and vendor paid amount.
4. Delete a wrong vendor payment and verify linked voucher, bank transaction and accounting journal are not active.
5. Create a new non-cash vendor payment and confirm bank account is mandatory.

Next part after this stage: Stage 11D-102 Purchase/Vendor Payment Live QA Fixes.
EOF
