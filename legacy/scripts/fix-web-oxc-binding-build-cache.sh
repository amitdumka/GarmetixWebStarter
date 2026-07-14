#!/usr/bin/env bash
set -Eeuo pipefail
cd "${1:-/opt/garmetix/current}"

echo "Rebuilding web image without stale npm optional-dependency cache..."
COMPOSE_PROJECT_NAME=garmetix docker compose \
  --env-file .env.production \
  -f deploy/docker-compose.prod.yml \
  -f deploy/docker-compose.cloudflare.yml \
  build --no-cache web

echo "Starting web container..."
COMPOSE_PROJECT_NAME=garmetix docker compose \
  --env-file .env.production \
  -f deploy/docker-compose.prod.yml \
  -f deploy/docker-compose.cloudflare.yml \
  up -d web
