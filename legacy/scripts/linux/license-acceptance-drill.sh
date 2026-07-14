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

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }; }
require_cmd curl
require_cmd python3

curl_json() {
  local method="$1"
  local url="$2"
  local token="${3:-}"
  local body="${4:-}"
  local args=(-fsS --max-time 30 -X "$method" "$url" -H 'Content-Type: application/json')
  if [ -n "$token" ]; then
    args+=(-H "Authorization: Bearer $token")
  fi
  if [ -n "$body" ]; then
    args+=(-d "$body")
  fi
  curl "${args[@]}"
}

if [ -z "${GARMETIX_SMOKE_USER:-}" ] || [ -z "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD to run license acceptance."
  exit 2
fi

LOGIN_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
LOGIN_RESPONSE=$(curl_json POST "$API_BASE/auth/login" "" "$LOGIN_BODY")
TOKEN=$(printf '%s' "$LOGIN_RESPONSE" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$TOKEN" ]; then
  echo "Login did not return a token." >&2
  exit 3
fi

echo "[1/3] License status"
STATUS=$(curl_json GET "$API_BASE/license/status" "$TOKEN")
printf '%s\n' "$STATUS" | python3 -m json.tool
STATUS_JSON="$STATUS" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['STATUS_JSON'])
required = ['enforcementEnabled','ready','activated','valid','state','productCode','requiredModules','recommendedEnvironmentKeys']
missing = [key for key in required if key not in payload]
if missing:
    raise SystemExit(f'License status is missing keys: {missing}')
print(f"License status contract OK: {payload.get('state')} / enforced={payload.get('enforcementEnabled')}")
PY

if [ "${GARMETIX_LICENSE_GENERATE_TEST:-false}" = "true" ]; then
  echo "[2/3] Generate test license"
  BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({
    "clientCode": os.environ.get("GARMETIX_LICENSE_CLIENT_CODE", "ACCEPTANCE-TEST"),
    "clientName": os.environ.get("GARMETIX_LICENSE_CLIENT_NAME", "Garmetix Acceptance Test"),
    "plan": os.environ.get("GARMETIX_LICENSE_PLAN", "Trial"),
    "validityDays": int(os.environ.get("GARMETIX_LICENSE_VALIDITY_DAYS", "30")),
    "maxStores": int(os.environ.get("GARMETIX_LICENSE_MAX_STORES", "1")),
    "maxUsers": int(os.environ.get("GARMETIX_LICENSE_MAX_USERS", "5")),
    "modules": os.environ.get("GARMETIX_LICENSE_MODULES", "Billing,Inventory,Purchase,Accounting,GST,HR,Payroll").split(','),
    "issuedBy": "license-acceptance-drill"
}))
PY
)
  GENERATED=$(curl_json POST "$API_BASE/license/generate" "$TOKEN" "$BODY")
  printf '%s\n' "$GENERATED" | python3 -m json.tool
  LICENSE_KEY=$(printf '%s' "$GENERATED" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("licenseKey") or "")')
  if [ -z "$LICENSE_KEY" ]; then
    echo "Generate did not return a license key." >&2
    exit 4
  fi

  if [ "${GARMETIX_LICENSE_ACTIVATE_GENERATED:-false}" = "true" ]; then
    echo "[3/3] Activate generated license"
    ACTIVATE_BODY=$(LICENSE_KEY="$LICENSE_KEY" python3 - <<'PY'
import json, os
print(json.dumps({"licenseKey": os.environ["LICENSE_KEY"]}))
PY
)
    curl_json POST "$API_BASE/license/activate" "$TOKEN" "$ACTIVATE_BODY" | python3 -m json.tool
  else
    echo "[3/3] Activation skipped. Set GARMETIX_LICENSE_ACTIVATE_GENERATED=true to activate the generated key."
  fi
else
  echo "[2/3] Generate/activation skipped. Set GARMETIX_LICENSE_GENERATE_TEST=true to test generation."
  echo "[3/3] License acceptance drill completed."
fi
