#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

API_BASE="${API_BASE_URL:-${PUBLIC_API_BASE_URL:-http://localhost:8080/api}}"
API_BASE="${API_BASE%/}"
ROOT_BASE="${API_BASE%/api}"

printf 'Garmetix smoke test
'
printf 'API: %s
' "$API_BASE"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

require_cmd curl

curl_json() {
  local method="$1"
  local url="$2"
  local body="${3:-}"
  if [ -n "$body" ]; then
    curl -fsS -X "$method" "$url" -H 'Content-Type: application/json' -d "$body"
  else
    curl -fsS -X "$method" "$url"
  fi
}

printf '
[1/4] API health...
'
curl_json GET "$API_BASE/health" || curl_json GET "$ROOT_BASE/api/health"
printf '
Health OK
'

TOKEN=""
if [ -n "${GARMETIX_SMOKE_USER:-}" ] && [ -n "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  printf '
[2/4] Admin login...
'
  LOGIN_RESPONSE=$(curl_json POST "$API_BASE/auth/login" "{"userName":"$GARMETIX_SMOKE_USER","password":"$GARMETIX_SMOKE_PASSWORD"}")
  TOKEN=$(printf '%s' "$LOGIN_RESPONSE" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
  if [ -z "$TOKEN" ]; then
    echo "Login did not return a token." >&2
    exit 3
  fi
  printf 'Login OK
'

  printf '
[3/4] Release smoke checks...
'
  curl -fsS "$API_BASE/release-stabilization/smoke-checks" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

  printf '
[4/4] Production readiness summary...
'
  curl -fsS "$API_BASE/production-readiness/summary" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool >/tmp/garmetix-production-readiness.json
  cat /tmp/garmetix-production-readiness.json
else
  printf '
[2/4] Authenticated checks skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD to enable them.
'
fi

printf '
Smoke test completed.
'
