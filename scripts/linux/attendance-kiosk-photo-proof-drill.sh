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

printf 'Checking Stage 9B kiosk monitor endpoints at %s\n' "$API_BASE"
auth_get 'attendance/photo-proofs' | python3 -m json.tool >/tmp/garmetix-attendance-photo-proofs.json
auth_get 'attendance/sync-batches' | python3 -m json.tool >/tmp/garmetix-attendance-sync-batches.json
curl -fsS --max-time 30 -X POST "$API_BASE/attendance/kiosk/bootstrap" -H 'Content-Type: application/json' -d '{}' | python3 -m json.tool >/tmp/garmetix-attendance-kiosk-bootstrap-stage9b.json

if [ -n "${GARMETIX_KIOSK_DEVICE_ID:-}" ] && [ -n "${GARMETIX_KIOSK_DEVICE_TOKEN:-}" ]; then
  readiness_body=$(python3 - <<'PY'
import json, os
print(json.dumps({"deviceId": os.environ["GARMETIX_KIOSK_DEVICE_ID"], "deviceToken": os.environ["GARMETIX_KIOSK_DEVICE_TOKEN"]}))
PY
)
  curl -fsS --max-time 30 -X POST "$API_BASE/attendance/kiosk/readiness" -H 'Content-Type: application/json' -d "$readiness_body" | python3 -m json.tool >/tmp/garmetix-attendance-kiosk-readiness.json
else
  printf 'Skipping live kiosk readiness/photo upload: set GARMETIX_KIOSK_DEVICE_ID and GARMETIX_KIOSK_DEVICE_TOKEN to test a registered kiosk.\n'
fi

printf 'Stage 9B kiosk photo-proof acceptance drill completed.\n'
