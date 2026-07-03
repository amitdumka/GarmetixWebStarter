#!/usr/bin/env bash
set -euo pipefail

MODE="${1:---smoke}"
PROJECT_ROOT="${2:-/opt/garmetix/current}"
API_PORT="${API_PORT:-5080}"
WEB_PORT="${WEB_PORT:-3000}"
EXPECTED_VERSION="4.12.06"
EXPECTED_STAGE="Stage 11D-91 Vyapar Sale Import Runtime Validation"

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

  echo "Checking API app-info..."
  if command -v curl >/dev/null 2>&1; then
    APP_INFO="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/app-info" || true)"
    echo "$APP_INFO" | head -c 1200 || true
    echo
    if ! echo "$APP_INFO" | grep -q "$EXPECTED_VERSION"; then
      echo "WARNING: /api/app-info did not show expected version $EXPECTED_VERSION. Check deployment/container cache."
    fi
    if ! echo "$APP_INFO" | grep -q "11D-91"; then
      echo "WARNING: /api/app-info did not show expected stage $EXPECTED_STAGE."
    fi

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

echo "Copying and running Vyapar Sale Import DB checks..."
docker cp scripts/runtime/stage11d91-vyapar-sale-import-db-check.sql "$POSTGRES_CONTAINER:/tmp/stage11d91-vyapar-sale-import-db-check.sql"
docker exec "$POSTGRES_CONTAINER" sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/stage11d91-vyapar-sale-import-db-check.sql'

echo
cat <<'EOF'
Stage 11D-91 smoke/DB validation completed.

Manual UI checks still required:
1. Open Sales -> Vyapar Sale Import.
2. Upload each Vyapar file and preview.
3. Verify already imported invoices are auto-hidden on re-preview.
4. Use Fully Matched filter and Import Fully Matched.
5. Map every non-cash Bank/POS/UPI source before confirm.
6. Open Sales -> Imported Vyapar Sales and verify invoice/source/date/customer mapping.
7. Confirm Day Closing/Petty Cash shows cash vs non-cash split correctly.

Next part after this stage: Stage 11D-92 Vyapar Import Batch Undo + Bulk Barcode Mapping Upload.
EOF
