#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
if [ -f "$ENV_FILE" ]; then
  # Normalize possible CRLF from Windows/WSL before sourcing.
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
fi

API_BASE="${API_BASE_URL:-${PUBLIC_API_BASE_URL:-http://localhost:5080/api}}"
API_BASE="${API_BASE%/}"
ROOT_BASE="${API_BASE%/api}"
EXPECTED_VERSION="${GARMETIX_EXPECTED_VERSION:-4.9.18}"
EXPECTED_BUILD_CODE="${GARMETIX_EXPECTED_BUILD_CODE:-GARMETIX-8I-20260619-49180}"

printf 'Garmetix API smoke test\n'
printf 'API: %s\n' "$API_BASE"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

require_cmd curl
require_cmd python3

curl_json() {
  local method="$1"
  local url="$2"
  local body="${3:-}"
  if [ -n "$body" ]; then
    curl -fsS --max-time 20 -X "$method" "$url" -H 'Content-Type: application/json' -d "$body"
  else
    curl -fsS --max-time 20 -X "$method" "$url"
  fi
}

json_get() {
  python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get(sys.argv[1]) or "")' "$1"
}

printf '\n[1/6] API health...\n'
curl_json GET "$API_BASE/health" >/tmp/garmetix-health.json || curl_json GET "$ROOT_BASE/api/health" >/tmp/garmetix-health.json
cat /tmp/garmetix-health.json | python3 -m json.tool >/dev/null 2>&1 || cat /tmp/garmetix-health.json
printf 'Health OK\n'

printf '\n[2/6] App version...\n'
APP_INFO=$(curl_json GET "$API_BASE/app-info/version")
printf '%s\n' "$APP_INFO" | python3 -m json.tool
APP_VERSION=$(printf '%s' "$APP_INFO" | json_get version)
APP_BUILD=$(printf '%s' "$APP_INFO" | json_get buildCode)
if [ "$APP_VERSION" != "$EXPECTED_VERSION" ] || [ "$APP_BUILD" != "$EXPECTED_BUILD_CODE" ]; then
  echo "Expected $EXPECTED_VERSION / $EXPECTED_BUILD_CODE but got $APP_VERSION / $APP_BUILD" >&2
  exit 3
fi
printf 'Version OK\n'

printf '\n[3/6] Test automation manifest...\n'
MANIFEST=$(curl_json GET "$API_BASE/test-automation/manifest")
MANIFEST_JSON="$MANIFEST" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['MANIFEST_JSON'])
checks = payload.get('checks') or []
codes = {item.get('code') or item.get('Code') for item in checks}
required = {'BACKEND_UNIT_TESTS','DASHBOARD_HOME_CONTRACT','FRONTEND_BUILD','FRONTEND_SMOKE','DOCKER_COMPOSE_BUILD','DOCKER_HEALTH','FRONTEND_ROUTE_ACCESS_AUDIT','DOCKER_ACCEPTANCE_DRILL','SECRET_HYGIENE_AUDIT','FRONTEND_HYDRATION_GUARD','BACKUP_RESTORE_SAFETY','PERMISSION_ROLE_ACCEPTANCE','PRINT_PDF_ACCEPTANCE','AUTHENTICATED_API_SMOKE'}
missing = sorted(required - codes)
if missing:
    raise SystemExit(f'Missing test manifest codes: {missing}')
print(f'Manifest OK: {len(checks)} checks')
PY

printf '\n[4/6] Runtime smoke endpoint...\n'
curl_json GET "$API_BASE/test-automation/runtime-smoke" | python3 -m json.tool

TOKEN=""
if [ -n "${GARMETIX_SMOKE_USER:-}" ] && [ -n "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  printf '\n[5/6] Admin login...\n'
  LOGIN_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
  LOGIN_RESPONSE=$(curl_json POST "$API_BASE/auth/login" "$LOGIN_BODY")
  TOKEN=$(printf '%s' "$LOGIN_RESPONSE" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
  if [ -z "$TOKEN" ]; then
    echo "Login did not return a token." >&2
    exit 4
  fi
  printf 'Login OK\n'

  printf '\n[6/6] Authenticated release/readiness checks...\n'
  DASHBOARD_HOME=$(curl -fsS --max-time 30 "$API_BASE/dashboard/home" -H "Authorization: Bearer $TOKEN")
  DASHBOARD_HOME_JSON="$DASHBOARD_HOME" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['DASHBOARD_HOME_JSON'])
route = payload.get('route')
dashboard_type = payload.get('dashboardType')
if not route or not dashboard_type:
    raise SystemExit(f'/api/dashboard/home returned an invalid body: {payload}')
print(f'Dashboard home OK: {route} ({dashboard_type})')
PY
  curl -fsS --max-time 30 "$API_BASE/release-stabilization/smoke-checks" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
  curl -fsS --max-time 30 "$API_BASE/production-readiness/summary" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool >/tmp/garmetix-production-readiness.json
  cat /tmp/garmetix-production-readiness.json
else
  printf '\n[5/6] Authenticated checks skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD to enable them.\n'
fi

printf '\nSmoke test completed.\n'
