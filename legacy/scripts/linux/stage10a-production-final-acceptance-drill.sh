#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
if [ -f "$ENV_FILE" ]; then
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
EXPECTED_VERSION="${GARMETIX_EXPECTED_VERSION:-4.10.12}"
EXPECTED_BUILD_CODE="${GARMETIX_EXPECTED_BUILD_CODE:-GARMETIX-10B-20260620-4112}"
: "${GARMETIX_SMOKE_USER:?Set GARMETIX_SMOKE_USER}"
: "${GARMETIX_SMOKE_PASSWORD:?Set GARMETIX_SMOKE_PASSWORD}"

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }; }
require_cmd curl
require_cmd python3

printf 'Stage 10B Excel Import Export Center drill\n'
printf 'API: %s\n' "$API_BASE"

LOGIN_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
TOKEN=$(curl -fsS --max-time 30 -X POST "$API_BASE/auth/login" -H 'Content-Type: application/json' -d "$LOGIN_BODY" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$TOKEN" ]; then
  echo "Login failed or token missing." >&2
  exit 3
fi

APP_INFO=$(curl -fsS --max-time 30 "$API_BASE/app-info/version")
APP_INFO_JSON="$APP_INFO" EXPECTED_VERSION="$EXPECTED_VERSION" EXPECTED_BUILD_CODE="$EXPECTED_BUILD_CODE" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['APP_INFO_JSON'])
if payload.get('version') != os.environ['EXPECTED_VERSION'] or payload.get('buildCode') != os.environ['EXPECTED_BUILD_CODE']:
    raise SystemExit(f"Stale app-info: {payload.get('version')} / {payload.get('buildCode')}")
print(f"Version OK: {payload.get('version')} / {payload.get('buildCode')}")
PY

curl -fsS --max-time 30 "$API_BASE/health" >/tmp/garmetix-stage10a-health.json
curl -fsS --max-time 30 "$API_BASE/production-readiness/summary" -H "Authorization: Bearer $TOKEN" >/tmp/garmetix-stage10a-readiness.json
curl -fsS --max-time 30 "$API_BASE/test-automation/runtime-smoke" >/tmp/garmetix-stage10a-runtime.json
curl -fsS --max-time 30 "$API_BASE/dashboard/todays" -H "Authorization: Bearer $TOKEN" >/tmp/garmetix-stage10a-todays.json
curl -fsS --max-time 30 "$API_BASE/attendance/final-acceptance" -H "Authorization: Bearer $TOKEN" >/tmp/garmetix-stage10a-attendance.json
FINAL=$(curl -fsS --max-time 30 "$API_BASE/stage10a/final-acceptance" -H "Authorization: Bearer $TOKEN")

FINAL_JSON="$FINAL" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['FINAL_JSON'])
required = ['version','stage','releaseName','buildCode','overallStatus','requiredManifestCodes','missingManifestCodes','sections']
missing = [key for key in required if key not in payload]
if missing:
    raise SystemExit(f'Missing fields from final acceptance: {missing}')
if payload.get('missingManifestCodes'):
    raise SystemExit(f"Missing manifest codes: {payload.get('missingManifestCodes')}")
sections = payload.get('sections') or []
if len(sections) < 5:
    raise SystemExit('Expected at least five final acceptance sections.')
print(f"Stage 10A final acceptance OK: {payload.get('overallStatus')} with {len(sections)} sections")
PY

python3 - <<'PY'
import json
for label, path in [
    ('health', '/tmp/garmetix-stage10a-health.json'),
    ('production-readiness', '/tmp/garmetix-stage10a-readiness.json'),
    ('runtime-smoke', '/tmp/garmetix-stage10a-runtime.json'),
    ('todays-dashboard', '/tmp/garmetix-stage10a-todays.json'),
    ('attendance-final', '/tmp/garmetix-stage10a-attendance.json'),
]:
    with open(path, encoding='utf-8') as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict) or not payload:
        raise SystemExit(f'{label} returned an empty/invalid JSON body')
    print(f'{label}: JSON OK')
PY

printf '\nStage 10A drill completed. Review readiness/runtime warnings before go-live.\n'
