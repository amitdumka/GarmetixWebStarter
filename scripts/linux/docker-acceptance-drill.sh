#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
COMPOSE_FILE="${GARMETIX_COMPOSE_FILE:-docker-compose.prod.yml}"
CLOUDFLARE_COMPOSE_FILE="${GARMETIX_CLOUDFLARE_COMPOSE_FILE:-deploy/docker-compose.cloudflare.yml}"
EXPECTED_VERSION="${GARMETIX_EXPECTED_VERSION:-4.10.9}"
EXPECTED_BUILD_CODE="${GARMETIX_EXPECTED_BUILD_CODE:-GARMETIX-9J-20260619-4109}"

printf 'Garmetix Docker acceptance drill\n'
printf 'Env file: %s\n' "$ENV_FILE"
printf 'Expected: %s / %s\n' "$EXPECTED_VERSION" "$EXPECTED_BUILD_CODE"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

require_cmd docker
require_cmd curl
require_cmd python3

if [ -f "$ENV_FILE" ]; then
  python3 - "$ENV_FILE" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1])
data = p.read_bytes()
fixed = data.replace(b'\r\n', b'\n').replace(b'\r', b'\n')
if fixed != data:
    p.write_bytes(fixed)
PY
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
else
  echo "Environment file was not found: $ENV_FILE" >&2
  exit 2
fi

API_PORT="${API_PORT:-5080}"
WEB_PORT="${WEB_PORT:-3000}"
API_BASE="${API_BASE_URL:-${PUBLIC_API_BASE_URL:-http://localhost:${API_PORT}/api}}"
API_BASE="${API_BASE%/}"
WEB_BASE="${GARMETIX_WEB_BASE_URL:-${NUXT_PUBLIC_SITE_URL:-http://localhost:${WEB_PORT}}}"
WEB_BASE="${WEB_BASE%/}"

COMPOSE_ARGS=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE")
if [ "${GARMETIX_ACCEPTANCE_WITH_CLOUDFLARE:-0}" = "1" ] && [ -f "$CLOUDFLARE_COMPOSE_FILE" ]; then
  COMPOSE_ARGS+=(-f "$CLOUDFLARE_COMPOSE_FILE")
fi

printf '\n[1/8] Static route-access audit...\n'
python3 scripts/validation/frontend-route-access-check.py

printf '\n[2/8] Build production Docker images...\n'
docker compose "${COMPOSE_ARGS[@]}" build

printf '\n[3/8] Start production containers...\n'
docker compose "${COMPOSE_ARGS[@]}" up -d

printf '\n[4/8] Wait for API health...\n'
for attempt in $(seq 1 60); do
  if curl -fsS --max-time 5 "$API_BASE/health" >/tmp/garmetix-acceptance-health.json 2>/dev/null; then
    printf 'API health OK after %s attempt(s).\n' "$attempt"
    break
  fi
  if [ "$attempt" -eq 60 ]; then
    echo "API health did not become ready at $API_BASE/health" >&2
    docker compose "${COMPOSE_ARGS[@]}" ps >&2 || true
    docker compose "${COMPOSE_ARGS[@]}" logs --tail=150 api >&2 || true
    exit 3
  fi
  sleep 3
done

printf '\n[5/8] Web root and proxy checks...\n'
curl -fsS --max-time 20 "$WEB_BASE/" >/tmp/garmetix-acceptance-web.html
WEB_APP_INFO=$(curl -fsS --max-time 20 "$WEB_BASE/api/app-info/version")
WEB_APP_INFO_JSON="$WEB_APP_INFO" EXPECTED_VERSION="$EXPECTED_VERSION" EXPECTED_BUILD_CODE="$EXPECTED_BUILD_CODE" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['WEB_APP_INFO_JSON'])
version = payload.get('version')
build = payload.get('buildCode')
if version != os.environ['EXPECTED_VERSION'] or build != os.environ['EXPECTED_BUILD_CODE']:
    raise SystemExit(f'Web proxy app-info mismatch: {version} / {build}')
print(f'Web proxy app-info OK: {version} / {build}')
PY

printf '\n[6/8] API smoke test...\n'
GARMETIX_EXPECTED_VERSION="$EXPECTED_VERSION" \
GARMETIX_EXPECTED_BUILD_CODE="$EXPECTED_BUILD_CODE" \
API_BASE_URL="$API_BASE" \
./scripts/linux/smoke-test.sh "$ENV_FILE"

if [ -n "${GARMETIX_SMOKE_USER:-}" ] && [ -n "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  printf '\n[7/8] Authenticated workspace/navigation acceptance...\n'
  LOGIN_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
  LOGIN_RESPONSE=$(curl -fsS --max-time 30 -X POST "$API_BASE/auth/login" -H 'Content-Type: application/json' -d "$LOGIN_BODY")
  TOKEN=$(printf '%s' "$LOGIN_RESPONSE" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
  if [ -z "$TOKEN" ]; then
    echo "Login did not return a bearer token." >&2
    exit 4
  fi

  DASHBOARD_HOME=$(curl -fsS --max-time 30 "$API_BASE/dashboard/home" -H "Authorization: Bearer $TOKEN")
  WORKSPACE_OPTIONS=$(curl -fsS --max-time 30 "$API_BASE/workspace/options" -H "Authorization: Bearer $TOKEN")
  SETUP_STATUS=$(curl -fsS --max-time 30 "$API_BASE/setup/status" -H "Authorization: Bearer $TOKEN")
  RELEASE_CHECKS=$(curl -fsS --max-time 30 "$API_BASE/release-stabilization/smoke-checks" -H "Authorization: Bearer $TOKEN")
  PRODUCTION_READINESS=$(curl -fsS --max-time 30 "$API_BASE/production-readiness/summary" -H "Authorization: Bearer $TOKEN")

  DASHBOARD_HOME_JSON="$DASHBOARD_HOME" WORKSPACE_OPTIONS_JSON="$WORKSPACE_OPTIONS" SETUP_STATUS_JSON="$SETUP_STATUS" RELEASE_CHECKS_JSON="$RELEASE_CHECKS" PRODUCTION_READINESS_JSON="$PRODUCTION_READINESS" python3 - <<'PY'
import json, os
home = json.loads(os.environ['DASHBOARD_HOME_JSON'])
workspace = json.loads(os.environ['WORKSPACE_OPTIONS_JSON'])
setup = json.loads(os.environ['SETUP_STATUS_JSON'])
release = json.loads(os.environ['RELEASE_CHECKS_JSON'])
readiness = json.loads(os.environ['PRODUCTION_READINESS_JSON'])
if not home.get('route') or not home.get('dashboardType'):
    raise SystemExit(f'Dashboard home body is invalid: {home}')
for key in ('companies', 'storeGroups', 'stores'):
    if key not in workspace:
        raise SystemExit(f'Workspace options missing {key}: {workspace}')
if 'hasCompany' not in setup or 'hasStore' not in setup:
    raise SystemExit(f'Setup status body is invalid: {setup}')
if not release.get('checks'):
    raise SystemExit('Release smoke checks returned no checks')
if not readiness.get('checks'):
    raise SystemExit('Production readiness returned no checks')
print(f"Dashboard route: {home['route']} ({home['dashboardType']})")
print(f"Workspace options: {len(workspace.get('companies') or [])} companies, {len(workspace.get('stores') or [])} stores")
print(f"Release status: {release.get('overallStatus') or release.get('status')}; readiness: {readiness.get('status')}")
PY
else
  printf '\n[7/8] Authenticated workspace/navigation acceptance skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD.\n'
fi

printf '\n[8/8] Container status...\n'
docker compose "${COMPOSE_ARGS[@]}" ps

printf '\nDocker acceptance drill completed.\n'
