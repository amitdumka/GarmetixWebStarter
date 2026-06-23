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
MAX_SAMPLE_FETCHES="${GARMETIX_PRINT_MAX_SAMPLE_FETCHES:-8}"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

require_cmd curl
require_cmd python3

if [ -z "${GARMETIX_SMOKE_USER:-}" ] || [ -z "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD for an admin user before running print/PDF acceptance." >&2
  exit 2
fi

printf 'Garmetix print/PDF acceptance drill\n'
printf 'API: %s\n' "$API_BASE"

LOGIN_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({"userName": os.environ["GARMETIX_SMOKE_USER"], "password": os.environ["GARMETIX_SMOKE_PASSWORD"]}))
PY
)
LOGIN_RESPONSE=$(curl -fsS --max-time 30 -X POST "$API_BASE/auth/login" -H 'Content-Type: application/json' -d "$LOGIN_BODY")
TOKEN=$(printf '%s' "$LOGIN_RESPONSE" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$TOKEN" ]; then
  echo "Login did not return a bearer token." >&2
  exit 3
fi

STATUS=$(curl -fsS --max-time 30 "$API_BASE/print-acceptance/status" -H "Authorization: Bearer $TOKEN")
printf '%s\n' "$STATUS" | python3 -m json.tool > /tmp/garmetix-print-acceptance-status.json
cat /tmp/garmetix-print-acceptance-status.json

STATUS_JSON="$STATUS" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['STATUS_JSON'])
docs = payload.get('documents') or []
required = {
    'salesInvoice', 'salesReturn', 'voucher', 'cashVoucher', 'pettyCash',
    'purchaseInward', 'purchaseReturn', 'commercialNote', 'nonGstGoods',
    'tailoringOrder', 'salaryPayslip', 'salaryPayment', 'gstReturn'
}
keys = {item.get('key') for item in docs}
missing = sorted(required - keys)
if missing:
    raise SystemExit(f'Missing print acceptance document keys: {missing}')
if payload.get('totalCount') != len(docs):
    raise SystemExit('totalCount does not match documents length')
ready = sum(1 for item in docs if item.get('status') == 'Ready')
print(f'Print acceptance catalog OK: {ready}/{len(docs)} sample documents ready')
PY

printf '\nChecking available sample document endpoints...\n'
STATUS_JSON="$STATUS" API_BASE="$API_BASE" TOKEN="$TOKEN" MAX_SAMPLE_FETCHES="$MAX_SAMPLE_FETCHES" python3 - <<'PY'
import json, os, subprocess, sys
payload = json.loads(os.environ['STATUS_JSON'])
api_base = os.environ['API_BASE'].rstrip('/')
token = os.environ['TOKEN']
limit = int(os.environ.get('MAX_SAMPLE_FETCHES') or 8)
ready = [item for item in payload.get('documents') or [] if item.get('endpoint')]
for item in ready[:limit]:
    endpoint = item['endpoint']
    url = api_base + endpoint.replace('/api', '', 1)
    print(f"- {item.get('label')}: {url}")
    result = subprocess.run([
        'curl', '-fsS', '--max-time', '45', '-o', '/tmp/garmetix-print-sample.bin',
        '-w', '%{http_code} %{content_type}',
        '-H', f'Authorization: Bearer {token}', url
    ], text=True, capture_output=True)
    if result.returncode != 0:
        raise SystemExit(result.stderr or f'Failed to fetch {url}')
    code_type = result.stdout.strip()
    if not code_type.startswith('200 '):
        raise SystemExit(f'Unexpected response for {url}: {code_type}')
    print(f'  OK {code_type}')
print(f'Fetched {min(len(ready), limit)} ready sample endpoint(s); set GARMETIX_PRINT_MAX_SAMPLE_FETCHES to change the limit.')
PY

printf '\nPrint/PDF acceptance drill completed.\n'
