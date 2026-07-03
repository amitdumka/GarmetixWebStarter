#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
API_BASE="${API_BASE:-http://localhost:8080}"
TOKEN="${GARMETIX_API_TOKEN:-}"
if [[ -z "$TOKEN" ]]; then
  echo "GARMETIX_API_TOKEN is not set. Login token is required for Admin-only API smoke check."
  echo "Example: GARMETIX_API_TOKEN='Bearer ey...' $0 /opt/garmetix/current"
  exit 2
fi

echo "Checking purchase payment reconciliation report endpoint..."
curl -fsS \
  -H "Authorization: $TOKEN" \
  "$API_BASE/api/purchase/payments/reconciliation?take=20" | python3 -m json.tool >/tmp/stage11d104-purchase-reconciliation.json

echo "Saved report to /tmp/stage11d104-purchase-reconciliation.json"
echo "If report shows mismatches and you want repair, call POST /api/purchase/payments/reconciliation/repair after taking DB backup."
