#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
ENV_FILE="${1:-.env.production}"
COMPOSE_FILES=(-f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml)

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing $ENV_FILE" >&2
  exit 2
fi

python3 - "$ENV_FILE" <<'PY'
from pathlib import Path
import sys
p=Path(sys.argv[1])
data=p.read_bytes()
p.write_bytes(data.replace(b'\r\n', b'\n').replace(b'\r', b'\n'))
PY

compose() {
  docker compose --env-file "$ENV_FILE" "${COMPOSE_FILES[@]}" "$@"
}

echo "== Docker compose status =="
compose ps

api_state=$(docker inspect -f '{{.State.Status}}' garmetix-api-1 2>/dev/null || true)
web_state=$(docker inspect -f '{{.State.Status}}' garmetix-web-1 2>/dev/null || true)
postgres_health=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' garmetix-postgres-1 2>/dev/null || true)

[ "$api_state" = "running" ] || { echo "API container is not running: $api_state" >&2; compose logs --tail=120 api || true; exit 3; }
[ "$web_state" = "running" ] || { echo "Web container is not running: $web_state" >&2; compose logs --tail=120 web || true; exit 3; }
[ "$postgres_health" = "healthy" ] || { echo "Postgres is not healthy: $postgres_health" >&2; compose logs --tail=120 postgres || true; exit 3; }

echo "== Local health endpoints =="
curl -fsS --max-time 20 http://127.0.0.1:5080/api/health | python3 -m json.tool
curl -fsS --max-time 20 http://127.0.0.1:3000/api/health | python3 -m json.tool
curl -fsS --max-time 20 http://127.0.0.1:5080/api/test-automation/runtime-smoke | python3 -m json.tool

echo "== Web root =="
curl -fsS --max-time 20 http://127.0.0.1:3000/ | grep -qi 'Garmetix'

echo "Docker smoke test completed."
