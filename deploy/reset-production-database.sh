#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f .env.production ]]; then
  echo "Missing .env.production. Run deploy/create-production-env.sh first." >&2
  exit 1
fi

# shellcheck source=deploy/lib/env-file.sh
source "${ROOT_DIR}/deploy/lib/env-file.sh"

DOCKER=(docker)
if ! docker ps >/dev/null 2>&1; then
  if sudo -n docker ps >/dev/null 2>&1; then
    DOCKER=(sudo docker)
  else
    echo "Current user cannot access Docker. Re-login after Docker group update or run with sudo." >&2
    exit 1
  fi
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

CLOUDFLARE_TUNNEL_TOKEN="$(dotenv_get CLOUDFLARE_TUNNEL_TOKEN "")"
COMPOSE_FILES=(-f docker-compose.prod.yml)
if [[ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" && "${CLOUDFLARE_TUNNEL_TOKEN}" != CHANGE_ME* ]]; then
  COMPOSE_FILES+=(-f deploy/docker-compose.cloudflare.yml)
fi

export COMPOSE_PROJECT_NAME=garmetix

echo "WARNING: this will remove the Garmetix PostgreSQL Docker volume and delete all local app data."
if [[ "${1:-}" != "--yes" ]]; then
  echo "Run: ./deploy/reset-production-database.sh --yes"
  exit 1
fi

"${DOCKER[@]}" compose --env-file .env.production "${COMPOSE_FILES[@]}" down --remove-orphans --volumes
set_env_var .env.production RESET_DATABASE_ON_DEPLOY false

echo "Database volume removed. Run ./deploy/run-production.sh to create a clean database from the current schema baseline."
