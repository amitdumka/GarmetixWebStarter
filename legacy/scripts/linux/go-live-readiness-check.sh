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
WEB_PORT="${WEB_PORT:-3000}"
PUBLIC_DOMAIN="${PUBLIC_DOMAIN:-${DOMAIN_NAME:-}}"

echo "== Go-live readiness =="
echo "ROOT_DIR=${ROOT_DIR}"
echo "PUBLIC_DOMAIN=${PUBLIC_DOMAIN:-<not-set>}"
echo

echo "== Docker compose status =="
cd "${ROOT_DIR}"
docker compose --env-file "${ENV_FILE}" -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps || true

echo
for url in   "http://127.0.0.1:${API_PORT}/api/health"   "http://127.0.0.1:${API_PORT}/api/app-info"   "http://127.0.0.1:${API_PORT}/api/production-readiness/summary"   "http://127.0.0.1:${API_PORT}/api/production-readiness/checklist"   "http://127.0.0.1:${API_PORT}/api/backups/maintenance/status"   "http://127.0.0.1:${WEB_PORT}/api/health"; do
  echo "--- ${url}"
  curl -fsS "${url}" || echo "FAILED: ${url}"
  echo
  echo
 done

if [[ -n "${PUBLIC_DOMAIN}" ]]; then
  echo "== Public domain checks =="
  for url in     "https://${PUBLIC_DOMAIN}"     "https://${PUBLIC_DOMAIN}/api/health"; do
    echo "--- ${url}"
    curl -I -fsS "${url}" || echo "FAILED: ${url}"
    echo
  done
fi

echo "Go-live readiness check completed."
