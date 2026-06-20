#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck source=/dev/null
  source "$ENV_FILE"
  set +a
fi

BASE_URL="${GARMETIX_API_BASE_URL:-${API_BASE_URL:-http://localhost:5000/api}}"
USER_NAME="${GARMETIX_SMOKE_USER:-admin}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-admin123}"

echo "Running stage10j import/export engine drill against $BASE_URL"
login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
token=$(curl -fsS -H 'Content-Type: application/json' -d "$login_payload" "$BASE_URL/auth/login" | python3 -c 'import json,sys; data=json.load(sys.stdin); print(data.get("token") or data.get("accessToken") or "")')
if [ -z "$token" ]; then
  echo "Could not login for smoke user" >&2
  exit 1
fi

curl_auth() {
  curl -fsS -H "Authorization: Bearer $token" "$@"
}

center=$(curl_auth "$BASE_URL/import-export/center")
printf '%s' "$center" | python3 -c 'import json,sys; data=json.load(sys.stdin); assert data.get("moduleCount",0) >= 13; counts=data.get("counts",{}); assert "customers" in counts and "vendors" in counts and "stocks" in counts; print("center ok")'

for module in products customers vendors stock-opening attendance; do
  curl_auth -o "/tmp/garmetix-${module}-template.csv" "$BASE_URL/import-export/template/$module"
  test -s "/tmp/garmetix-${module}-template.csv"
  head -n 1 "/tmp/garmetix-${module}-template.csv" | grep -q ','
  echo "template/$module ok"
done

health=$(curl_auth "$BASE_URL/import-export/health")
printf '%s' "$health" | python3 -c 'import json,sys; data=json.load(sys.stdin); modules=[m["key"] for m in data.get("modules",[])]; assert "stock-opening" in modules and "customers" in modules and "vendors" in modules; print("health ok")'

echo "stage10j import/export engine drill passed"
