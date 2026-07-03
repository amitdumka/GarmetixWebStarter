#!/usr/bin/env bash
set -euo pipefail

: "${GARMETIX_TOKEN:?Set GARMETIX_TOKEN from browser/API login token}"
: "${COMPANY_ID:?Set COMPANY_ID}"
: "${STORE_GROUP_ID:?Set STORE_GROUP_ID}"
: "${STORE_ID:?Set STORE_ID}"
: "${VYAPAR_FILE:?Set VYAPAR_FILE=/path/to/Vyapar.xlsx}"

API_BASE="${API_BASE:-http://127.0.0.1:5080/api}"

curl -fsS \
  -H "Authorization: Bearer ${GARMETIX_TOKEN}" \
  -F "file=@${VYAPAR_FILE}" \
  "${API_BASE}/sale-import/vyapar/preview?companyId=${COMPANY_ID}&storeGroupId=${STORE_GROUP_ID}&storeId=${STORE_ID}" \
  | python3 -m json.tool
