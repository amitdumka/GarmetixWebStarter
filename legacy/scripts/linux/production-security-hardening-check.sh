#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

failures=0
warnings=0
pass() { printf 'PASS %s
' "$*"; }
warn() { printf 'WARN %s
' "$*"; warnings=$((warnings+1)); }
fail() { printf 'FAIL %s
' "$*"; failures=$((failures+1)); }

if [[ -f "deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "deploy/lib/env-file.sh"
  normalize_env_file "${ENV_FILE}" || true
  export_env_file_safe "${ENV_FILE}" || true
elif [[ -f "${ENV_FILE}" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  set +a
fi

API_PORT="${API_PORT:-5080}"
WEB_PORT="${WEB_PORT:-3000}"
PUBLIC_DOMAIN="${PUBLIC_DOMAIN:-${DOMAIN_NAME:-}}"
PUBLIC_HTTPS_URL="${PUBLIC_HTTPS_URL:-${PUBLIC_DOMAIN:+https://${PUBLIC_DOMAIN}}}"

echo "== Production security hardening check =="
# Expected public security headers include Strict-Transport-Security, X-Content-Type-Options, X-Frame-Options and Referrer-Policy.
echo "ROOT_DIR=${ROOT_DIR}"
echo "ENV_FILE=${ENV_FILE}"
echo "PUBLIC_HTTPS_URL=${PUBLIC_HTTPS_URL:-<not-set>}"
echo

[[ -f "${ENV_FILE}" ]] || fail "Missing env file ${ENV_FILE}"
if [[ -f "${ENV_FILE}" ]]; then
  mode="$(stat -c '%a' "${ENV_FILE}" 2>/dev/null || echo unknown)"
  case "${mode}" in
    600|640|400|440) pass ".env file permissions are private enough (${mode})" ;;
    unknown) warn "Could not read env file permissions" ;;
    *) warn ".env file permissions are ${mode}; prefer chmod 600 ${ENV_FILE}" ;;
  esac
fi

[[ "${RESET_DATABASE_ON_DEPLOY:-false}" == "false" ]] && pass "RESET_DATABASE_ON_DEPLOY=false" || fail "RESET_DATABASE_ON_DEPLOY must be false before live use"
[[ ${#POSTGRES_PASSWORD:-0} -ge 24 ]] && pass "POSTGRES_PASSWORD length OK" || fail "POSTGRES_PASSWORD should be at least 24 chars"
[[ ${#JWT_SIGNING_KEY:-0} -ge 48 ]] && pass "JWT_SIGNING_KEY length OK" || fail "JWT_SIGNING_KEY should be at least 48 chars"
[[ "${PUBLIC_HTTPS_URL:-}" == https://* ]] && pass "Public URL uses HTTPS" || warn "PUBLIC_HTTPS_URL should be HTTPS"
[[ "${CORS_ALLOWED_ORIGINS:-}" == https://* ]] && pass "CORS origin uses HTTPS" || warn "CORS_ALLOWED_ORIGINS should be the exact HTTPS origin"
[[ -n "${DOCKER_LOG_MAX_SIZE:-}" && -n "${DOCKER_LOG_MAX_FILE:-}" ]] && pass "Docker log rotation env is set" || warn "Set DOCKER_LOG_MAX_SIZE and DOCKER_LOG_MAX_FILE"

if systemctl is-enabled docker >/dev/null 2>&1; then
  pass "Docker service is enabled for reboot auto-start"
else
  warn "Docker service is not enabled; run: sudo systemctl enable --now docker"
fi

echo
if docker compose --env-file "${ENV_FILE}" -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml config >/tmp/garmetix-compose-security-check.yml 2>/tmp/garmetix-compose-security-check.err; then
  pass "Docker Compose config renders successfully"
  if grep -q '127.0.0.1:.*:5080' /tmp/garmetix-compose-security-check.yml     && grep -q '127.0.0.1:.*:3000' /tmp/garmetix-compose-security-check.yml     && grep -q '127.0.0.1:.*:5432' /tmp/garmetix-compose-security-check.yml; then
    pass "API, web and database ports are bound to localhost only"
  else
    warn "One or more container ports may not be localhost-only; inspect docker-compose config"
  fi
else
  fail "Docker Compose config failed"
  cat /tmp/garmetix-compose-security-check.err || true
fi

echo
for url in "http://127.0.0.1:${API_PORT}/api/health" "http://127.0.0.1:${WEB_PORT}/api/health"; do
  if curl -fsS --max-time 10 "${url}" >/dev/null; then
    pass "Health endpoint OK: ${url}"
  else
    warn "Health endpoint did not respond: ${url}"
  fi
done

if [[ -n "${PUBLIC_HTTPS_URL}" ]]; then
  headers="$(mktemp)"
  if curl -I -fsS --max-time 20 "${PUBLIC_HTTPS_URL}" >"${headers}"; then
    pass "Public HTTPS endpoint responds"
    grep -iq '^strict-transport-security:' "${headers}" && pass "HSTS header present" || warn "HSTS header not observed on public response"
    grep -iq '^x-content-type-options:' "${headers}" && pass "X-Content-Type-Options header present" || warn "X-Content-Type-Options header not observed"
    grep -iq '^x-frame-options:' "${headers}" && pass "X-Frame-Options header present" || warn "X-Frame-Options header not observed"
    grep -iq '^referrer-policy:' "${headers}" && pass "Referrer-Policy header present" || warn "Referrer-Policy header not observed"
  else
    warn "Public HTTPS endpoint did not respond during security check"
  fi
  rm -f "${headers}"
fi

echo
echo "Security hardening check completed: ${failures} failure(s), ${warnings} warning(s)."
if (( failures > 0 )); then
  exit 1
fi
