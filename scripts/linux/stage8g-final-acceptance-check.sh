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
WEB_PORT="${WEB_PORT:-3000}"
PUBLIC_DOMAIN="${PUBLIC_DOMAIN:-${DOMAIN_NAME:-}}"
FAILED=0
WARNED=0

run_required() {
  local title="$1"; shift
  echo
  echo "== ${title} =="
  if "$@"; then
    echo "PASS: ${title}"
  else
    echo "FAIL: ${title}"
    FAILED=$((FAILED+1))
  fi
}

run_optional() {
  local title="$1"; shift
  echo
  echo "== ${title} =="
  if "$@"; then
    echo "PASS: ${title}"
  else
    echo "WARNING: ${title} needs attention"
    WARNED=$((WARNED+1))
  fi
}

curl_json() {
  local url="$1"
  curl -fsS --max-time 20 "${url}" >/tmp/garmetix-stage8g-check.json
  python3 -m json.tool /tmp/garmetix-stage8g-check.json >/dev/null
}

run_required "API health" curl_json "http://127.0.0.1:${API_PORT}/api/health"
run_required "Web proxy health" curl_json "http://127.0.0.1:${WEB_PORT}/api/health"
run_required "App info endpoint" curl_json "http://127.0.0.1:${API_PORT}/api/app-info"
run_required "Production readiness summary" curl_json "http://127.0.0.1:${API_PORT}/api/production-readiness/summary"
run_required "Backup maintenance status" curl_json "http://127.0.0.1:${API_PORT}/api/backups/maintenance/status"
run_required "Runtime smoke" curl_json "http://127.0.0.1:${API_PORT}/api/test-automation/runtime-smoke"

for script in   ./scripts/linux/production-security-hardening-check.sh   ./scripts/linux/log-retention-check.sh   ./scripts/linux/go-live-readiness-check.sh   ./scripts/linux/backup-maintenance-check.sh   ./scripts/linux/gstin-provider-readiness-check.sh   ./scripts/linux/oracle-cloud-readiness-check.sh; do
  if [[ -x "${script}" ]]; then
    run_optional "${script}" "${script}" "${ENV_FILE}"
  else
    echo "WARNING: ${script} missing or not executable"
    WARNED=$((WARNED+1))
  fi
done

if [[ -n "${PUBLIC_DOMAIN}" ]]; then
  run_optional "Public HTTPS" curl -fsSI --max-time 20 "https://${PUBLIC_DOMAIN}"
  run_optional "Public API health" curl -fsS --max-time 20 "https://${PUBLIC_DOMAIN}/api/health"
fi

echo
echo "== Stage 8G final acceptance summary =="
echo "Critical failures: ${FAILED}"
echo "Warnings: ${WARNED}"

if [[ "${FAILED}" -gt 0 ]]; then
  echo "Stage 8G is NOT ready. Fix critical failures first."
  exit 1
fi

if [[ "${WARNED}" -gt 0 ]]; then
  echo "Stage 8G has warnings. Review them before final production sign-off."
else
  echo "Stage 8G acceptance checks passed without warnings."
fi
