#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

if [[ -f "${ROOT_DIR}/deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "${ROOT_DIR}/deploy/lib/env-file.sh"
  normalize_text_file "${ENV_FILE}" || true
  export_env_file_safe "${ENV_FILE}" || true
fi

API_PORT="${API_PORT:-5080}"
TEST_GSTIN="${GSTIN_TEST_NUMBER:-27AAECA1234F1Z5}"

echo "== GSTIN provider readiness =="
echo "Provider enabled: ${GSTIN_LOOKUP_ENABLED:-false}"
echo "Source name: ${GSTIN_LOOKUP_SOURCE_NAME:-Configured GSTIN Provider}"
echo "Base URL: ${GSTIN_LOOKUP_BASE_URL:-<empty>}"
echo

echo "Local/API status endpoint requires admin login, so this script checks config and public health."
curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null
echo "API health OK. Test GSTIN for manual UI/API validation: ${TEST_GSTIN}"
