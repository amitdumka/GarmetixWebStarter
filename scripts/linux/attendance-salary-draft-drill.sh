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
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD before running attendance salary draft drill." >&2
  exit 2
fi

login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
login_response=$(curl -fsS -H 'Content-Type: application/json' -d "$login_payload" "$BASE_URL/api/auth/login")
token=$(printf '%s' "$login_response" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$token" ]; then
  echo "Login succeeded but token was not found." >&2
  exit 3
fi

curl -fsS -H "Authorization: Bearer $token" "$BASE_URL/api/attendance/salary-slip-drafts?year=$YEAR&month=$MONTH" >/tmp/garmetix-attendance-salary-draft.json
python3 - <<'PY'
import json
from pathlib import Path
payload = json.loads(Path('/tmp/garmetix-attendance-salary-draft.json').read_text())
assert 'previewOnly' in payload or 'rows' in payload, 'salary draft response missing expected contract'
print('Attendance salary draft preview endpoint contract OK')
PY
