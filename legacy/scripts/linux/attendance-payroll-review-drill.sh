#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

API_BASE="${API_BASE_URL:-${PUBLIC_API_BASE_URL:-http://localhost:5080/api}}"
API_BASE="${API_BASE%/}"
: "${GARMETIX_SMOKE_USER:?Set GARMETIX_SMOKE_USER}"
: "${GARMETIX_SMOKE_PASSWORD:?Set GARMETIX_SMOKE_PASSWORD}"

login_body=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
login=$(curl -fsS --max-time 30 -X POST "$API_BASE/auth/login" -H 'Content-Type: application/json' -d "$login_body")
token=$(printf '%s' "$login" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("token") or d.get("accessToken") or "")')
if [ -z "$token" ]; then
  echo "Login did not return token" >&2
  exit 2
fi

auth_get() {
  curl -fsS --max-time 30 "$API_BASE/$1" -H "Authorization: Bearer $token"
}

auth_post() {
  curl -fsS --max-time 30 -X POST "$API_BASE/$1" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$2"
}

year=$(date +%Y)
month=$(date +%-m)
printf 'Checking Stage 9D attendance payroll review endpoints at %s for %s-%s\n' "$API_BASE" "$year" "$month"
auth_get "attendance/payroll-summary?year=$year&month=$month" | python3 -m json.tool >/tmp/garmetix-attendance-payroll-summary.json
auth_get "attendance/payroll-review?year=$year&month=$month" | python3 -m json.tool >/tmp/garmetix-attendance-payroll-review.json
auth_post 'attendance/payroll-review/rebuild' "{\"year\":$year,\"month\":$month}" | python3 -m json.tool >/tmp/garmetix-attendance-payroll-review-rebuild.json

printf 'Stage 9D attendance payroll review drill completed. Mark-reviewed actions need live review row IDs and are tested from the UI.\n'
