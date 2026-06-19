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
SEND_MODE="${GARMETIX_SMTP_SEND_TEST:-false}"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

require_cmd curl
require_cmd python3

if [ -z "${GARMETIX_SMOKE_USER:-}" ] || [ -z "${GARMETIX_SMOKE_PASSWORD:-}" ]; then
  echo "Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD for an admin user before running SMTP acceptance." >&2
  exit 2
fi

printf 'Garmetix SMTP delivery acceptance drill\n'
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

STATUS=$(curl -fsS --max-time 30 "$API_BASE/email-diagnostics/status" -H "Authorization: Bearer $TOKEN")
printf '%s\n' "$STATUS" | python3 -m json.tool > /tmp/garmetix-smtp-status.json
cat /tmp/garmetix-smtp-status.json

STATUS_JSON="$STATUS" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['STATUS_JSON'])
required_keys = {
    'EMAIL_ENABLED', 'EMAIL_HOST', 'EMAIL_PORT', 'EMAIL_ENABLE_SSL',
    'EMAIL_USERNAME', 'EMAIL_PASSWORD', 'EMAIL_FROM_EMAIL', 'EMAIL_FROM_NAME',
    'EMAIL_REPLY_TO_EMAIL', 'PASSWORD_RESET_FRONTEND_BASE_URL'
}
keys = set(payload.get('recommendedEnvironmentKeys') or [])
missing = sorted(required_keys - keys)
if missing:
    raise SystemExit(f'SMTP status contract missing environment keys: {missing}')
if 'providerName' not in payload:
    raise SystemExit('SMTP status does not include providerName')
if 'timeoutSeconds' not in payload:
    raise SystemExit('SMTP status does not include timeoutSeconds')
print(f"SMTP status contract OK: provider={payload.get('providerName')} ready={payload.get('ready')}")
PY

if [ "$SEND_MODE" = "true" ]; then
  if [ -z "${GARMETIX_SMTP_TEST_TO:-}" ]; then
    echo "Set GARMETIX_SMTP_TEST_TO when GARMETIX_SMTP_SEND_TEST=true." >&2
    exit 2
  fi
  TEST_BODY=$(python3 - <<'PY'
import json, os
print(json.dumps({
    "toEmail": os.environ["GARMETIX_SMTP_TEST_TO"],
    "toName": os.environ.get("GARMETIX_SMTP_TEST_NAME", ""),
    "subject": os.environ.get("GARMETIX_SMTP_TEST_SUBJECT", "Garmetix SMTP acceptance drill"),
    "message": os.environ.get("GARMETIX_SMTP_TEST_MESSAGE", "This email confirms Garmetix SMTP delivery is working from the production host.")
}))
PY
)
  SEND_RESPONSE=$(curl -fsS --max-time 60 -X POST "$API_BASE/email-diagnostics/send-test" \
    -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' \
    -d "$TEST_BODY")
  printf '%s\n' "$SEND_RESPONSE" | python3 -m json.tool
else
  printf '\nLive send skipped. Set GARMETIX_SMTP_SEND_TEST=true and GARMETIX_SMTP_TEST_TO to send a real acceptance email.\n'
fi

printf '\nSMTP delivery acceptance drill completed.\n'
