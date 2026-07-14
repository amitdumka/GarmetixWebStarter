#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f .env.production ]]; then
  echo "Missing .env.production in $ROOT_DIR" >&2
  exit 1
fi

dotenv_get() {
  local key="$1" default_value="${2:-}" line value
  line="$(grep -E "^${key}=" .env.production 2>/dev/null | tail -n 1 || true)"
  if [[ -z "$line" ]]; then
    printf '%s' "$default_value"
    return
  fi
  value="${line#*=}"
  value="${value%$'\r'}"
  if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]]; then
    value="${value:1:${#value}-2}"
  elif [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
    value="${value:1:${#value}-2}"
  fi
  printf '%s' "${value:-$default_value}"
}

WEB_PORT="$(dotenv_get WEB_PORT 3000)"
API_PORT="$(dotenv_get API_PORT 5080)"
PUBLIC_DOMAIN="$(dotenv_get PUBLIC_DOMAIN garmetix.aadwikafashion.in)"
CLOUDFLARE_TUNNEL_TOKEN="$(dotenv_get CLOUDFLARE_TUNNEL_TOKEN "")"

DOCKER=(docker)
if ! docker ps >/dev/null 2>&1; then
  if sudo -n docker ps >/dev/null 2>&1; then
    DOCKER=(sudo docker)
  else
    echo "Current user cannot access Docker." >&2
    exit 1
  fi
fi

COMPOSE_FILES=(-f docker-compose.prod.yml)
if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
  COMPOSE_FILES+=(-f deploy/docker-compose.cloudflare.yml)
fi

export COMPOSE_PROJECT_NAME=garmetix

echo "==== Environment summary ===="
echo "ROOT_DIR=$ROOT_DIR"
echo "WEB_PORT=$WEB_PORT"
echo "API_PORT=$API_PORT"
echo "PUBLIC_DOMAIN=$PUBLIC_DOMAIN"
echo "Cloudflare enabled=$([[ ${#COMPOSE_FILES[@]} -gt 2 ]] && echo yes || echo no)"

echo ""
echo "==== Docker status ===="
"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" ps || true

echo ""
echo "==== Local curl tests ===="
for url in "http://127.0.0.1:${API_PORT}/api/health" "http://127.0.0.1:${WEB_PORT}/api/health" "http://127.0.0.1:${WEB_PORT}"; do
  echo "--- $url"
  curl -i --max-time 10 "$url" | head -80 || true
  echo ""
done

if command -v ss >/dev/null 2>&1; then
  echo "==== Listening ports ===="
  ss -ltnp 2>/dev/null | grep -E ":(${WEB_PORT}|${API_PORT}|5432)" || true
fi

echo ""
echo "==== API logs, last 220 lines ===="
"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=220 api || true

echo ""
echo "==== Web logs, last 180 lines ===="
"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=180 web || true

echo ""
echo "==== Postgres logs, last 100 lines ===="
"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=100 postgres || true

if [[ ${#COMPOSE_FILES[@]} -gt 2 ]]; then
  echo ""
  echo "==== Cloudflared logs, last 160 lines ===="
  "${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" logs --tail=160 cloudflared || true
fi

echo ""
echo "If local web is OK, also test from browser: https://${PUBLIC_DOMAIN}"
