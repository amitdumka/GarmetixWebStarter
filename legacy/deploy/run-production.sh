#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DOMAIN="${DOMAIN:-garmetix.aadwikafashion.in}"
PUBLIC_HTTPS_URL="${PUBLIC_HTTPS_URL:-https://${DOMAIN}}"
export DOMAIN PUBLIC_HTTPS_URL COMPOSE_PROJECT_NAME=garmetix

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

if [[ "${CLOUDFLARE_TUNNEL_TOKEN:-}" == *'${CLOUDFLARE_TUNNEL_TOKEN}'* || "${CLOUDFLARE_TUNNEL_TOKEN:-}" == *'$${CLOUDFLARE_TUNNEL_TOKEN}'* || "${CLOUDFLARE_TUNNEL_TOKEN:-}" == *'cloudflared'* || "${CLOUDFLARE_TUNNEL_TOKEN:-}" == *'--token'* ]]; then
  echo "CLOUDFLARE_TUNNEL_TOKEN in .env.production is invalid. Paste only the connector token, not a literal variable or full cloudflared command." >&2
  exit 1
fi

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

compose_garmetix() {
  "${DOCKER[@]}" compose --env-file .env.production -p garmetix "${COMPOSE_FILES[@]}" "$@"
}

show_diagnostics() {
  echo ""
  echo "==== Docker container status ===="
  compose_garmetix ps || true
  echo ""
  echo "==== API logs, last 160 lines ===="
  compose_garmetix logs --tail=160 api || true
  echo ""
  echo "==== Web logs, last 120 lines ===="
  compose_garmetix logs --tail=120 web || true
  echo ""
  echo "==== Postgres logs, last 80 lines ===="
  compose_garmetix logs --tail=80 postgres || true
  if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
    echo ""
    echo "==== Cloudflared logs, last 100 lines ===="
    compose_garmetix logs --tail=100 cloudflared || true
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

truthy() {
  local value="${1:-}"
  [[ "${value,,}" == "true" || "${value,,}" == "yes" || "$value" == "1" ]]
}

reset_database_volume() {
  echo "RESET_DATABASE_ON_DEPLOY is enabled. Removing old Garmetix containers and PostgreSQL volumes for a clean initial migration database."
  echo "This is destructive and intended only for fresh installs/test redeploys."

  # Stop both the intended project name and the accidental symlink-derived project name used by older deploys.
  "${DOCKER[@]}" compose --env-file .env.production -p garmetix "${COMPOSE_FILES[@]}" down --remove-orphans --volumes || true
  "${DOCKER[@]}" compose --env-file .env.production -p current "${COMPOSE_FILES[@]}" down --remove-orphans --volumes || true

  # Remove stale containers that block ports even when they were created by a different compose file/name.
  "${DOCKER[@]}" rm -f \
    garmetix-cloudflared-1 garmetix-web-1 garmetix-api-1 garmetix-postgres-1 \
    current-cloudflared-1 current-web-1 current-api-1 current-postgres-1 2>/dev/null || true

  # Remove only known Garmetix Postgres volumes. Do not use docker volume prune.
  "${DOCKER[@]}" volume rm -f \
    garmetix_garmetix_pg current_garmetix_pg garmetix_pg \
    garmetix_postgres_data current_postgres_data postgres_data 2>/dev/null || true

  # Force the first API start to build a clean schema and then mark the reset flag as consumed.
  set_env_var .env.production DATABASE_AUTO_MIGRATE true
  set_env_var .env.production DATABASE_SCHEMA_BOOTSTRAP_MODE Migrate
  set_env_var .env.production RESET_DATABASE_ON_DEPLOY false
}

if truthy "$RESET_DATABASE_ON_DEPLOY"; then
  reset_database_volume
fi

compose_garmetix up -d --build

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

compose_garmetix ps

echo "Local web URL: http://127.0.0.1:${WEB_PORT}"
echo "Local API URL: http://127.0.0.1:${API_PORT}/api/health"
echo "Public URL: https://${PUBLIC_DOMAIN}"

DOTMATRIX_BRIDGE_AUTO_INSTALL="$(dotenv_get .env.production DOTMATRIX_BRIDGE_AUTO_INSTALL false)"
DOTMATRIX_PRINTER_NAME="$(dotenv_get .env.production DOTMATRIX_PRINTER_NAME EPSON_LX310)"
if truthy "$DOTMATRIX_BRIDGE_AUTO_INSTALL"; then
  echo "Installing/updating Garmetix DotMatrix Bridge service for printer ${DOTMATRIX_PRINTER_NAME}."
  if [[ $EUID -eq 0 ]]; then
    PROJECT_DIR="$ROOT_DIR" GARMETIX_DOTMATRIX_PRINTER="$DOTMATRIX_PRINTER_NAME" ./deploy/install-dotmatrix-bridge-ubuntu.sh || true
  else
    sudo env PROJECT_DIR="$ROOT_DIR" GARMETIX_DOTMATRIX_PRINTER="$DOTMATRIX_PRINTER_NAME" ./deploy/install-dotmatrix-bridge-ubuntu.sh || true
  fi
else
  echo "DotMatrix Bridge auto-install is off. Manual install: sudo PROJECT_DIR=$ROOT_DIR GARMETIX_DOTMATRIX_PRINTER=$DOTMATRIX_PRINTER_NAME ./deploy/install-dotmatrix-bridge-ubuntu.sh"
fi
