#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  set -a; source "$ENV_FILE"; set +a
fi

BASE_URL="${GARMETIX_BASE_URL:-${APP_BASE_URL:-http://localhost:3000}}"
USER_NAME="${GARMETIX_SMOKE_USER:-admin}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"
STORE_ID="${GARMETIX_SMOKE_STORE_ID:-}"
TODAY="$(date +%F)"

if [[ -z "$PASSWORD" ]]; then
  echo "Set GARMETIX_SMOKE_PASSWORD before running stage10i store operations drill." >&2
  exit 1
fi

TOKEN="$(curl -fsS -X POST "$BASE_URL/api/auth/login" \
  -H 'content-type: application/json' \
  -d "{\"userName\":\"$USER_NAME\",\"password\":\"$PASSWORD\"}" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("token") or d.get("accessToken") or "")')"

if [[ -z "$TOKEN" ]]; then
  echo "Login did not return a bearer token." >&2
  exit 1
fi

if [[ -z "$STORE_ID" ]]; then
  STORE_ID="$(curl -fsS "$BASE_URL/api/stores" -H "authorization: Bearer $TOKEN" | python3 -c 'import json,sys; d=json.load(sys.stdin); print((d[0] if isinstance(d,list) and d else {}).get("id", ""))')"
fi

if [[ -z "$STORE_ID" ]]; then
  echo "No store found for store-day drill." >&2
  exit 1
fi

echo "Running stage10i store operations cash closing drill for store $STORE_ID on $TODAY"
curl -fsS "$BASE_URL/api/store-day/status?storeId=$STORE_ID&onDate=$TODAY" -H "authorization: Bearer $TOKEN" >/tmp/garmetix-store-day-status.json
curl -fsS "$BASE_URL/api/store-day/book-summary?storeId=$STORE_ID&onDate=$TODAY" -H "authorization: Bearer $TOKEN" >/tmp/garmetix-store-day-summary.json
python3 - <<'PY'
import json
for path in ['/tmp/garmetix-store-day-status.json', '/tmp/garmetix-store-day-summary.json']:
    data=json.load(open(path))
    summary=data.get('bookSummary', data)
    for key in ['openingBalance', 'cashInHand', 'openingBalanceSource', 'hasPreviousPettyCashSheet', 'openingBalanceMismatch']:
        if key not in summary:
            raise SystemExit(f'{path} missing {key}')
print('stage10i store operations drill passed: day-open opening and previous-petty-cash mismatch fields are available.')
PY
