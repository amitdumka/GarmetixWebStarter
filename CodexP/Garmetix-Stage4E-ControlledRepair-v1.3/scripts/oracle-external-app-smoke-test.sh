#!/usr/bin/env bash
set -euo pipefail
API_BASE_URL="${API_BASE_URL:-http://localhost:5080/api}"
ENTITY_NAME="${ENTITY_NAME:-Customer}"
TOKEN="${TOKEN:-}"
if [ -z "$TOKEN" ]; then
  echo "Set TOKEN to an Admin JWT first." >&2
  exit 1
fi
curl -sS -X POST "$API_BASE_URL/oracle-sync/external-app-test" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"entityName\":\"$ENTITY_NAME\",\"sourceApplication\":\"ExternalAppSmokeTest\",\"pullAfterSeed\":true,\"repairFirst\":true}"
echo
