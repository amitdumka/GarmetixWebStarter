#!/usr/bin/env bash
set -Eeuo pipefail

MODE="--smoke"
ROOT="/opt/garmetix/current"

if [[ $# -gt 0 ]]; then
  case "$1" in
    --smoke|--build|--up-only|--db-only|--help)
      MODE="$1"
      shift || true
      ;;
  esac
fi

if [[ "$MODE" == "--help" ]]; then
  cat <<'HELP'
Stage 11D-46 runtime validation runner

Usage:
  ./scripts/runtime/stage11d46-runtime-validation.sh --smoke /opt/garmetix/current
  ./scripts/runtime/stage11d46-runtime-validation.sh --build /opt/garmetix/current
  ./scripts/runtime/stage11d46-runtime-validation.sh --up-only /opt/garmetix/current
  ./scripts/runtime/stage11d46-runtime-validation.sh --db-only /opt/garmetix/current

Modes:
  --smoke   Validate scripts, compose config, running API/web/db, and non-mutating DB checks.
  --build   Run validation, docker compose build --no-cache, up -d, then smoke checks.
  --up-only Run docker compose up -d, then smoke checks.
  --db-only Run only PostgreSQL non-mutating checks.
HELP
  exit 0
fi

if [[ $# -gt 0 ]]; then
  ROOT="$1"
fi

if [[ ! -d "$ROOT" ]]; then
  echo "ERROR: project root not found: $ROOT" >&2
  exit 1
fi

cd "$ROOT"

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker command not found." >&2
  exit 1
fi

COMPOSE=(docker compose)
if ! docker compose version >/dev/null 2>&1; then
  echo "ERROR: docker compose plugin not available." >&2
  exit 1
fi

if [[ ! -f scripts/validation/stage11d46-runtime-validation-check.py ]]; then
  echo "ERROR: Stage 11D-46 validation script missing. Are you in the v4.11.61 package?" >&2
  exit 1
fi

python3 scripts/validation/stage11d46-runtime-validation-check.py

if [[ "$MODE" != "--db-only" ]]; then
  echo "Checking docker compose configuration..."
  "${COMPOSE[@]}" config >/dev/null
fi

if [[ "$MODE" == "--build" ]]; then
  echo "Building containers with --no-cache..."
  "${COMPOSE[@]}" build --no-cache
  echo "Starting containers..."
  "${COMPOSE[@]}" up -d
elif [[ "$MODE" == "--up-only" ]]; then
  echo "Starting containers..."
  "${COMPOSE[@]}" up -d
fi

wait_for_url() {
  local name="$1"
  local url="$2"
  local seconds="${3:-90}"
  local i=0
  while (( i < seconds )); do
    if curl -fsS "$url" >/tmp/stage11d46_${name}.json 2>/dev/null; then
      echo "$name OK: $url"
      return 0
    fi
    sleep 2
    i=$((i+2))
  done
  echo "ERROR: $name did not respond: $url" >&2
  return 1
}

if [[ "$MODE" != "--db-only" ]]; then
  echo "Container status:"
  "${COMPOSE[@]}" ps
  wait_for_url api_health http://localhost:5080/api/health 120
  wait_for_url app_info http://localhost:5080/api/app-info/version 60
  if ! grep -q '4.11.61' /tmp/stage11d46_app_info.json; then
    echo "ERROR: API app-info did not report version 4.11.61" >&2
    cat /tmp/stage11d46_app_info.json >&2 || true
    exit 1
  fi
  wait_for_url web_root http://localhost:3000 60 || echo "WARNING: web root did not respond on localhost:3000. Check web logs/proxy if this host uses a different port."
fi

POSTGRES_CONTAINER="$("${COMPOSE[@]}" ps -q postgres || true)"
if [[ -z "$POSTGRES_CONTAINER" ]]; then
  echo "ERROR: postgres container not found from docker compose ps." >&2
  exit 1
fi

echo "Checking PostgreSQL connectivity..."
docker exec "$POSTGRES_CONTAINER" pg_isready -U garmetix -d garmetix >/dev/null

echo "Running non-mutating sale payment DB checks..."
docker cp scripts/runtime/stage11d46-sale-payment-db-check.sql "$POSTGRES_CONTAINER":/tmp/stage11d46-sale-payment-db-check.sql >/dev/null
docker exec "$POSTGRES_CONTAINER" psql -U garmetix -d garmetix -v ON_ERROR_STOP=1 -f /tmp/stage11d46-sale-payment-db-check.sql

if [[ -x scripts/imports/royalwood-international-v4.11.54/validate-royalwood-international-v4.11.54.sh ]]; then
  echo "Royalwood import validator found. Running it..."
  scripts/imports/royalwood-international-v4.11.54/validate-royalwood-international-v4.11.54.sh "$ROOT" || {
    echo "WARNING: Royalwood validator failed. If import has not been applied yet, this warning is expected."
  }
else
  echo "Royalwood import validator not installed under scripts/imports; skipping Royalwood check."
fi

if [[ "$MODE" != "--db-only" ]]; then
  echo "Recent API log tail for manual review:"
  "${COMPOSE[@]}" logs --tail=80 api || true
fi

echo "Stage 11D-46 runtime validation completed. Review mismatch_count and mixed-payment rows above before accepting production use."
