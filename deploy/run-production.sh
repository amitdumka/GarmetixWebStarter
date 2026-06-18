#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DOMAIN="${DOMAIN:-garmetix.aadwikafashion.in}"
PUBLIC_HTTPS_URL="${PUBLIC_HTTPS_URL:-https://${DOMAIN}}"
export DOMAIN PUBLIC_HTTPS_URL

if [[ ! -f .env.production ]]; then
  ./deploy/create-production-env.sh
fi

# shellcheck source=deploy/lib/env-file.sh
source "${ROOT_DIR}/deploy/lib/env-file.sh"
normalize_env_file .env.production

WEB_PORT="$(dotenv_get .env.production WEB_PORT 3000)"
API_PORT="$(dotenv_get .env.production API_PORT 5080)"
PUBLIC_DOMAIN="$(dotenv_get .env.production PUBLIC_DOMAIN "$DOMAIN")"
CLOUDFLARE_TUNNEL_TOKEN="$(dotenv_get .env.production CLOUDFLARE_TUNNEL_TOKEN "")"
RESET_DATABASE_ON_DEPLOY="$(dotenv_get .env.production RESET_DATABASE_ON_DEPLOY false)"

mkdir -p backups secrets
chmod 700 secrets 2>/dev/null || true

DOCKER=(docker)
if ! docker ps >/dev/null 2>&1; then
  if sudo -n docker ps >/dev/null 2>&1; then
    DOCKER=(sudo docker)
  else
    echo "Current user cannot access Docker. Re-login after Docker group update or run this script with sudo." >&2
    exit 1
  fi
fi

COMPOSE_FILES=(-f docker-compose.prod.yml)
if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
  COMPOSE_FILES+=(-f deploy/docker-compose.cloudflare.yml)
  echo "Cloudflare Tunnel container will be started."
else
  echo "CLOUDFLARE_TUNNEL_TOKEN is blank; starting local Docker stack without Cloudflare Tunnel."
fi

show_diagnostics() {
  echo ""
  echo "==== Docker container status ===="
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" ps || true
  echo ""
  echo "==== API logs, last 160 lines ===="
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=160 api || true
  echo ""
  echo "==== Web logs, last 120 lines ===="
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=120 web || true
  echo ""
  echo "==== Postgres logs, last 80 lines ===="
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=80 postgres || true
  if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
    echo ""
    echo "==== Cloudflared logs, last 100 lines ===="
    "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=100 cloudflared || true
  fi
}

wait_for_url() {
  local name="$1" url="$2" max_attempts="$3" delay_seconds="$4"
  local http_code
  echo "Waiting for ${name}: ${url}"
  for i in $(seq 1 "$max_attempts"); do
    http_code="$(curl -sS -o /tmp/garmetix-health-body.$$ -w '%{http_code}' "$url" 2>/tmp/garmetix-health-error.$$ || true)"
    if [[ "$http_code" =~ ^2|3 ]]; then
      rm -f /tmp/garmetix-health-body.$$ /tmp/garmetix-health-error.$$
      echo "${name} is responding."
      return 0
    fi
    if [[ "$i" -eq 1 || $((i % 6)) -eq 0 ]]; then
      echo "  attempt ${i}/${max_attempts}: ${name} not ready yet, HTTP=${http_code:-none}"
      if [[ -s /tmp/garmetix-health-error.$$ ]]; then
        sed 's/^/    curl: /' /tmp/garmetix-health-error.$$ || true
      fi
      if [[ -s /tmp/garmetix-health-body.$$ ]]; then
        head -c 500 /tmp/garmetix-health-body.$$ | sed 's/^/    body: /' || true
        echo ""
      fi
    fi
    sleep "$delay_seconds"
  done
  rm -f /tmp/garmetix-health-body.$$ /tmp/garmetix-health-error.$$
  return 1
}

export COMPOSE_PROJECT_NAME=garmetix

if [[ "${RESET_DATABASE_ON_DEPLOY,,}" == "true" || "${RESET_DATABASE_ON_DEPLOY,,}" == "yes" || "${RESET_DATABASE_ON_DEPLOY}" == "1" ]]; then
  echo "RESET_DATABASE_ON_DEPLOY is enabled. Removing existing Garmetix containers and PostgreSQL volume for a clean database."
  echo "This is intended only for fresh installs or failed test deployments."
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" down --remove-orphans --volumes || true
  set_env_var .env.production RESET_DATABASE_ON_DEPLOY false
fi

"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" up -d --build

if ! wait_for_url "API direct health endpoint" "http://127.0.0.1:${API_PORT}/api/health" 120 3; then
  echo "API direct health check did not pass. Showing diagnostics:" >&2
  show_diagnostics >&2
  exit 1
fi

if ! wait_for_url "Nuxt web proxy health endpoint" "http://127.0.0.1:${WEB_PORT}/api/health" 80 3; then
  echo "Nuxt web proxy health did not pass, but API is healthy. Showing diagnostics:" >&2
  show_diagnostics >&2
  exit 1
fi

"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" ps

echo "Local web URL: http://127.0.0.1:${WEB_PORT}"
echo "Local API URL: http://127.0.0.1:${API_PORT}/api/health"
echo "Public URL: https://${PUBLIC_DOMAIN}"
