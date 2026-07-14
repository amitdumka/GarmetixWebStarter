#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  set -a; source "$ENV_FILE"; set +a
fi
BASE_URL="${GARMETIX_BASE_URL:-${PUBLIC_BASE_URL:-http://localhost:8080}}"
USER_NAME="${GARMETIX_SMOKE_USER:-admin}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"
if [[ -z "$PASSWORD" ]]; then
  echo "GARMETIX_SMOKE_PASSWORD is required" >&2
  exit 2
fi
login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
token=$(curl -fsS -H 'Content-Type: application/json' -d "$login_payload" "$BASE_URL/api/auth/login" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("token", ""))')
if [[ -z "$token" ]]; then
  echo "Login succeeded but no token was returned" >&2
  exit 3
fi
for path in \
  /api/barcode/final-acceptance \
  /api/gst-production/final-acceptance \
  /api/google-drive-backup/final-acceptance \
  /api/audit-trail/final-acceptance \
  /api/stage10/final-acceptance \
  /api/app-info/version; do
  echo "Checking $path"
  curl -fsS -H "Authorization: Bearer $token" "$BASE_URL$path" >/dev/null
done
echo "Stage 10 complete final acceptance drill passed."
