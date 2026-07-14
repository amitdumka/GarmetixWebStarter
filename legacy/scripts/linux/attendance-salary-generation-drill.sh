#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
BASE_URL="${GARMETIX_BASE_URL:-http://localhost:3000}"
USER_NAME="${GARMETIX_SMOKE_USER:-}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"
YEAR="${GARMETIX_ATTENDANCE_YEAR:-$(date +%Y)}"
MONTH="${GARMETIX_ATTENDANCE_MONTH:-$(date +%-m)}"

if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

if [ -z "$USER_NAME" ] || [ -z "$PASSWORD" ]; then
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD before running attendance salary generation drill." >&2
  exit 2
fi

login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
login_response=$(curl -fsS -H 'Content-Type: application/json' -d "$login_payload" "$BASE_URL/api/auth/login")
token=$(printf '%s' "$login_response" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$token" ]; then
  echo "Login succeeded but token was not found." >&2
  exit 3
fi

curl -fsS -H "Authorization: Bearer $token" "$BASE_URL/api/attendance/salary-slip-drafts?year=$YEAR&month=$MONTH" >/tmp/garmetix-attendance-salary-generation.json
python3 - <<'PY'
import json
from pathlib import Path
payload = json.loads(Path('/tmp/garmetix-attendance-salary-generation.json').read_text())
assert 'rows' in payload, 'salary draft response missing rows'
print('Attendance salary slip generation read contract OK')
PY

if [ "${GARMETIX_ATTENDANCE_GENERATE_TEST:-false}" = "true" ]; then
  generate_payload=$(printf '{"year":%s,"month":%s,"confirm":true,"notes":"Host acceptance drill"}' "$YEAR" "$MONTH")
  curl -fsS -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$generate_payload" "$BASE_URL/api/attendance/salary-slip-drafts/generate-payslips" >/tmp/garmetix-attendance-salary-generation-post.json
  echo "Attendance salary slip generation POST contract OK"
else
  echo "Skipping real salary slip generation. Set GARMETIX_ATTENDANCE_GENERATE_TEST=true to test POST generation."
fi
