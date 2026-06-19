#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
BASE_URL="${GARMETIX_BASE_URL:-http://localhost:3000}"
API_URL="${GARMETIX_API_URL:-http://localhost:8080/api}"
USER_NAME="${GARMETIX_SMOKE_USER:-}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"
if [[ -f "$ENV_FILE" ]]; then set -a; # shellcheck source=/dev/null
  source "$ENV_FILE" || true; set +a; fi
if [[ -z "$USER_NAME" || -z "$PASSWORD" ]]; then echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD." >&2; exit 2; fi
TOKEN=$(curl -fsS -X POST "$API_URL/auth/login" -H 'Content-Type: application/json' -d "{\"userName\":\"$USER_NAME\",\"password\":\"$PASSWORD\"}" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("token", ""))')
[[ -n "$TOKEN" ]] || { echo "Login did not return token" >&2; exit 3; }
curl -fsS "$API_URL/hr-payroll/adjustments/summary" -H "Authorization: Bearer $TOKEN" | python3 - <<'PY'
import json, sys
payload = json.load(sys.stdin)
required = ['openAdvances','openAdvanceAmount','recoveredAmount','leaveDays','bonusAmount','pfEmployee','pfEmployer','gratuityAmount','readinessMessages']
missing = [k for k in required if k not in payload]
if missing:
    raise SystemExit(f'Missing HR benefits summary keys: {missing}')
print('HR benefits/payroll summary OK:', payload.get('openAdvances'), 'open advance(s)')
PY
curl -fsS -o /dev/null -w '%{http_code}\n' "$BASE_URL/hr-benefits" | grep -Eq '200|302|304'
echo "HR benefits/payroll acceptance drill passed."
