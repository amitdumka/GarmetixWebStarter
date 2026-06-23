#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi
BASE_URL="${GARMETIX_BASE_URL:-${PUBLIC_API_BASE:-http://localhost:5000/api}}"
BASE_URL="${BASE_URL%/}"
USER_NAME="${GARMETIX_SMOKE_USER:-admin}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"
if [[ -z "$PASSWORD" ]]; then
  echo "Set GARMETIX_SMOKE_PASSWORD before running this drill." >&2
  exit 2
fi
login_payload=$(python3 - <<PY
import json, os
print(json.dumps({"userName": os.environ.get("GARMETIX_SMOKE_USER", "admin"), "password": os.environ.get("GARMETIX_SMOKE_PASSWORD", "")}))
PY
)
token=$(curl -fsS -H 'content-type: application/json' -d "$login_payload" "$BASE_URL/auth/login" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("token", ""))')
if [[ -z "$token" ]]; then
  echo "Login did not return token." >&2
  exit 1
fi
curl_auth() {
  curl -fsS -H "Authorization: Bearer $token" "$@"
}
printf 'Stage 10B Import / Export Center drill\n'
app_info=$(curl_auth "$BASE_URL/app-info/version")
python3 - <<PY <<<"$app_info"
import json, sys
payload=json.load(sys.stdin)
assert payload.get('version') == '4.10.12', payload
assert payload.get('buildCode') == 'GARMETIX-10B-20260620-4112', payload
print('Version OK:', payload.get('version'), payload.get('buildCode'))
PY
center=$(curl_auth "$BASE_URL/import-export/center")
python3 - <<PY <<<"$center"
import json, sys
payload=json.load(sys.stdin)
assert payload.get('moduleCount', 0) >= 1, payload
assert 'attendancePunches' in payload.get('counts', {}), payload
print('Center OK:', payload.get('moduleCount'), 'modules')
PY
health=$(curl_auth "$BASE_URL/import-export/health")
python3 - <<PY <<<"$health"
import json, sys
payload=json.load(sys.stdin)
assert payload.get('status') == 'Ready', payload
keys=[m.get('key') for m in payload.get('modules', [])]
assert 'attendance' in keys, keys
print('Health OK: attendance module present')
PY
curl_auth -o /tmp/garmetix-attendance-template.csv "$BASE_URL/import-export/template/attendance"
test -s /tmp/garmetix-attendance-template.csv
grep -q 'EmployeeCode' /tmp/garmetix-attendance-template.csv
curl_auth -o /tmp/garmetix-attendance-export.csv "$BASE_URL/import-export/export/attendance"
test -s /tmp/garmetix-attendance-export.csv
printf 'Stage 10B Import / Export drill completed.\n'
