#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
failures=0
warnings=0

red='\033[0;31m'; yellow='\033[1;33m'; green='\033[0;32m'; nc='\033[0m'

fail() { echo -e "${red}FAIL${nc} $*"; failures=$((failures+1)); }
warn() { echo -e "${yellow}WARN${nc} $*"; warnings=$((warnings+1)); }
pass() { echo -e "${green}PASS${nc} $*"; }

[[ -f "$ENV_FILE" ]] || fail "Missing env file: $ENV_FILE"
[[ -f "$COMPOSE_FILE" ]] || fail "Missing compose file: $COMPOSE_FILE"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

command -v docker >/dev/null 2>&1 && pass "Docker installed" || fail "Docker is not installed"
docker compose version >/dev/null 2>&1 && pass "Docker Compose plugin installed" || fail "docker compose plugin is not available"

[[ "${POSTGRES_PASSWORD:-}" != REPLACE_* && ${#POSTGRES_PASSWORD:-0} -ge 24 ]] && pass "POSTGRES_PASSWORD length OK" || fail "POSTGRES_PASSWORD must be replaced with a strong value"
[[ "${JWT_SIGNING_KEY:-}" != REPLACE_* && ${#JWT_SIGNING_KEY:-0} -ge 48 ]] && pass "JWT_SIGNING_KEY length OK" || fail "JWT_SIGNING_KEY must be replaced with a strong random value"

if [[ "${PUBLIC_HTTPS_URL:-}" =~ ^https:// ]]; then
  pass "PUBLIC_HTTPS_URL uses HTTPS"
else
  warn "PUBLIC_HTTPS_URL should be an HTTPS URL for production"
fi

if [[ "${CORS_ALLOWED_ORIGINS:-}" =~ ^https:// ]]; then
  pass "CORS_ALLOWED_ORIGINS set to HTTPS origin"
else
  warn "CORS_ALLOWED_ORIGINS should be set to your public HTTPS origin"
fi

if [[ "${EMAIL_ENABLED:-false}" == "true" ]]; then
  [[ -n "${EMAIL_HOST:-}" && "${EMAIL_HOST:-}" != smtp.example.com ]] && pass "SMTP host configured" || warn "EMAIL_ENABLED=true but EMAIL_HOST is placeholder"
else
  warn "EMAIL_ENABLED=false; password reset emails will not send in production"
fi

mkdir -p backups secrets
[[ -w backups ]] && pass "backups directory writable" || fail "backups directory is not writable"
[[ -d secrets ]] && pass "secrets directory exists" || fail "secrets directory missing"

if docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" config >/dev/null; then
  pass "docker compose config is valid"
else
  fail "docker compose config failed"
fi

if [[ "${GOOGLE_DRIVE_BACKUP_ENABLED:-false}" == "true" ]]; then
  [[ -n "${GOOGLE_DRIVE_FOLDER_ID:-}" ]] && pass "Google Drive folder configured" || fail "Google Drive backup enabled without GOOGLE_DRIVE_FOLDER_ID"
  [[ -f secrets/google-drive-service-account.json ]] && pass "Google service account file exists" || fail "Missing secrets/google-drive-service-account.json"
else
  warn "Off-site backup is not enabled. Configure Google Drive or another off-site target."
fi

echo
echo "Preflight completed: $failures failure(s), $warnings warning(s)."
if (( failures > 0 )); then
  exit 1
fi
