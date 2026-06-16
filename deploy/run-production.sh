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

set -a
# shellcheck disable=SC1091
source .env.production
set +a

mkdir -p backups secrets
chmod 700 secrets || true

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

export COMPOSE_PROJECT_NAME=garmetix
"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" up -d --build

echo "Waiting for API health endpoint..."
for i in $(seq 1 90); do
  if curl -fsS http://127.0.0.1:${WEB_PORT:-3000}/api/health >/dev/null 2>&1; then
    echo "Garmetix is healthy through Nuxt proxy."
    break
  fi
  if [[ "$i" -eq 90 ]]; then
    echo "Health check did not pass yet. Showing container status:" >&2
    "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" ps >&2 || true
    exit 1
  fi
  sleep 5
done

"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" ps

echo "Local server URL: http://127.0.0.1:${WEB_PORT:-3000}"
echo "Public URL: https://${PUBLIC_DOMAIN:-$DOMAIN}"
