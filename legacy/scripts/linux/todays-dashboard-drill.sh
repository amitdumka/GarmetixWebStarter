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

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }; }
require_cmd curl
require_cmd python3

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

TODAYS=$(curl -fsS --max-time 40 "$API_BASE/dashboard/todays" -H "Authorization: Bearer $TOKEN")
TODAYS_JSON="$TODAYS" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['TODAYS_JSON'])
required = ['businessDate', 'metrics', 'salesTrend', 'cashFlow', 'attendance', 'recentActivities', 'quickActions']
missing = [name for name in required if name not in payload]
if missing:
    raise SystemExit(f'Missing fields from /api/dashboard/todays: {missing}')
metrics = {item.get('label') for item in payload.get('metrics') or []}
for label in ["Today's Sales", "Today's Purchase", 'Receipts', 'Payments', 'Expenses', 'Cash Vouchers', 'Present Employees', 'Absent Employees']:
    if label not in metrics:
        raise SystemExit(f'Missing metric: {label}')
attendance = payload.get('attendance') or {}
for key in ['activeEmployees', 'presentEmployees', 'absentEmployees', 'present', 'absent']:
    if key not in attendance:
        raise SystemExit(f'Missing attendance field: {key}')
print(f"Today's dashboard OK: {len(payload.get('metrics') or [])} metrics, {len(payload.get('salesTrend') or [])} trend points")
PY
