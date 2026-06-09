#!/usr/bin/env bash
set -euo pipefail
API_BASE="${API_BASE:-http://localhost:5080/api}"
TOKEN="${1:-${TOKEN:-}}"
if [ -z "$TOKEN" ]; then
  echo "Usage: scripts/repair-database.sh <admin-jwt-token>" >&2
  exit 2
fi
curl -fsS -X POST "$API_BASE/database/repair" -H "Authorization: Bearer $TOKEN"
echo
