#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [[ -f "${ROOT_DIR}/deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "${ROOT_DIR}/deploy/lib/env-file.sh"
  normalize_text_file "${ENV_FILE}" || true
  export_env_file_safe "${ENV_FILE}" || true
fi

API_PORT="${API_PORT:-5080}"
BASE_URL="${GARMETIX_API_BASE_URL:-http://127.0.0.1:${API_PORT}/api}"

echo "== Oracle Cloud / External App Sync readiness =="
echo "ROOT_DIR=${ROOT_DIR}"
echo "BASE_URL=${BASE_URL}"
echo

echo "== Local environment hints =="
echo "ORACLE_SYNC_ENABLED=${ORACLE_SYNC_ENABLED:-<not-set>}"
echo "ORACLE_SYNC_DIRECTION=${ORACLE_SYNC_DIRECTION:-<not-set>}"
echo "ORACLE_SYNC_CONFLICT_POLICY=${ORACLE_SYNC_CONFLICT_POLICY:-<not-set>}"
echo "ORACLE_SYNC_TNS_ADMIN=${ORACLE_SYNC_TNS_ADMIN:-<not-set>}"
echo "ORACLE_SYNC_WALLET_DIRECTORY=${ORACLE_SYNC_WALLET_DIRECTORY:-<not-set>}"
echo

if [[ -n "${ORACLE_SYNC_TNS_ADMIN:-}" ]]; then
  if [[ -d "${ORACLE_SYNC_TNS_ADMIN}" ]]; then
    echo "PASS: TNS_ADMIN directory exists: ${ORACLE_SYNC_TNS_ADMIN}"
    ls -la "${ORACLE_SYNC_TNS_ADMIN}" | sed -n '1,40p'
  else
    echo "WARN: TNS_ADMIN directory does not exist: ${ORACLE_SYNC_TNS_ADMIN}"
  fi
fi

if [[ -n "${ORACLE_SYNC_WALLET_DIRECTORY:-}" ]]; then
  if [[ -d "${ORACLE_SYNC_WALLET_DIRECTORY}" ]]; then
    echo "PASS: Wallet directory exists: ${ORACLE_SYNC_WALLET_DIRECTORY}"
    ls -la "${ORACLE_SYNC_WALLET_DIRECTORY}" | sed -n '1,40p'
  else
    echo "WARN: Wallet directory does not exist: ${ORACLE_SYNC_WALLET_DIRECTORY}"
  fi
fi

echo
for endpoint in   "oracle-sync/status"   "oracle-sync/cloud-readiness"   "oracle-sync/ownership"   "oracle-sync/auto-apply-policy"   "oracle-sync/external-app-test-plan"   "oracle-sync/dead-letters?take=10"; do
  echo "--- GET ${BASE_URL}/${endpoint}"
  curl -fsS "${BASE_URL}/${endpoint}" || echo "WARN: endpoint failed or requires admin login token"
  echo
  echo
done

echo "== Optional live connection test =="
echo "Run this only after Oracle wallet/TNS and credentials are configured in .env.production:"
echo "curl -X POST ${BASE_URL}/oracle-sync/test"
echo

echo "Oracle Cloud readiness script completed."
