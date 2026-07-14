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

printf 'Checking Attendance Core endpoints at %s\n' "$API_BASE"
auth_get 'attendance/today' | python3 -m json.tool >/tmp/garmetix-attendance-today.json
auth_get 'attendance/monthly' | python3 -m json.tool >/tmp/garmetix-attendance-monthly.json
auth_get 'attendance/shifts' | python3 -m json.tool >/tmp/garmetix-attendance-shifts.json
auth_get 'attendance/policies' | python3 -m json.tool >/tmp/garmetix-attendance-policies.json
auth_get 'attendance/devices' | python3 -m json.tool >/tmp/garmetix-attendance-devices.json
auth_get 'attendance/regularization' | python3 -m json.tool >/tmp/garmetix-attendance-regularization.json
auth_get 'attendance/payroll-summary' | python3 -m json.tool >/tmp/garmetix-attendance-payroll-summary.json
curl -fsS --max-time 30 -X POST "$API_BASE/attendance/kiosk/bootstrap" -H 'Content-Type: application/json' -d '{}' | python3 -m json.tool >/tmp/garmetix-attendance-kiosk-bootstrap.json
printf 'Attendance Core acceptance drill completed.\n'
