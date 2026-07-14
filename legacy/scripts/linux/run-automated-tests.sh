#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

RUN_DOCKER="${RUN_DOCKER_SMOKE:-false}"
RUN_FRONTEND_BUILD="${RUN_FRONTEND_BUILD:-true}"
RUN_BACKEND_TESTS="${RUN_BACKEND_TESTS:-true}"
RUN_FRONTEND_SMOKE="${RUN_FRONTEND_SMOKE:-false}"
ENV_FILE="${ENV_FILE:-.env.production}"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }
}

if [ "$RUN_BACKEND_TESTS" = "true" ]; then
  echo "== Backend xUnit tests =="
  require_cmd dotnet
  dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
fi

if [ "$RUN_FRONTEND_BUILD" = "true" ]; then
  echo "== Frontend Nuxt build =="
  require_cmd npm
  (cd frontend/garmetix-web && npm ci && npm run build)
fi

if [ "$RUN_FRONTEND_SMOKE" = "true" ]; then
  echo "== Frontend browserless smoke =="
  require_cmd npm
  (cd frontend/garmetix-web && npm run smoke:frontend)
fi

if [ "$RUN_DOCKER" = "true" ]; then
  echo "== Docker deployment smoke =="
  require_cmd docker
  ./scripts/linux/docker-smoke-test.sh "$ENV_FILE"
fi

python3 scripts/validation/stage8f-package2-static-checks.py
python3 scripts/validation/current-release-checks.py

echo "Automated test runner completed."
