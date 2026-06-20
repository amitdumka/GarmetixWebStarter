#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
BASE_URL="${GARMETIX_BASE_URL:-http://localhost:3000}"
USER_NAME="${GARMETIX_SMOKE_USER:-}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

if [[ -z "$USER_NAME" || -z "$PASSWORD" ]]; then
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD."
  exit 2
fi

login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
token=$(curl -fsS -X POST "$BASE_URL/api/auth/login" -H 'Content-Type: application/json' -d "$login_payload" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [[ -z "$token" ]]; then
  echo "Login did not return a token."
  exit 3
fi

auth=(-H "Authorization: Bearer $token")
for path in \
  /api/app-info/version \
  /api/attendance/final-acceptance \
  /api/attendance/device-bridge/status \
  /api/attendance/salary-payment-candidates; do
  echo "Checking $path"
  curl -fsS "${BASE_URL}${path}" "${auth[@]}" >/dev/null
done

echo "Stage 9 final attendance drill passed."
