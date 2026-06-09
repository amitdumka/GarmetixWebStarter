#!/usr/bin/env bash
set -euo pipefail

SKIP_DOCKER=0
NO_CACHE_API=0
KEEP_RUNNING=1

for arg in "$@"; do
  case "$arg" in
    --skip-docker) SKIP_DOCKER=1 ;;
    --no-cache-api) NO_CACHE_API=1 ;;
    --down-after) KEEP_RUNNING=0 ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS="$ROOT/validation-results"
STAMP="$(date +%Y%m%d-%H%M%S)"
SUMMARY="$RESULTS/validation-$STAMP.md"
mkdir -p "$RESULTS"
cd "$ROOT"

echo "# Garmetix Local Validation - $STAMP" > "$SUMMARY"
echo >> "$SUMMARY"
echo "Project root: $ROOT" >> "$SUMMARY"
echo >> "$SUMMARY"

run_step() {
  local name="$1"
  shift
  echo -e "\n==> $name"
  echo "## $name" >> "$SUMMARY"
  if "$@" 2>&1 | tee "$RESULTS/${name//[^a-zA-Z0-9-]/_}-$STAMP.log"; then
    echo "Status: PASS" >> "$SUMMARY"
  else
    echo "Status: FAIL" >> "$SUMMARY"
    exit 1
  fi
  echo >> "$SUMMARY"
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1" >&2; exit 1; }
}

run_step tool-versions bash -lc "require_cmd() { command -v \"\$1\" >/dev/null 2>&1 || exit 1; }; require_cmd dotnet; require_cmd npm; dotnet --info; npm --version; if [ $SKIP_DOCKER -eq 0 ]; then require_cmd docker; docker version; fi"
run_step backend-restore dotnet restore backend/Garmetix.Api/Garmetix.Api.csproj
run_step backend-build-release dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release --no-restore
run_step backend-publish-release dotnet publish backend/Garmetix.Api/Garmetix.Api.csproj -c Release -o validation-results/publish-api --no-restore
run_step frontend-npm-ci bash -lc 'cd frontend/garmetix-web && npm ci'
run_step frontend-build bash -lc 'cd frontend/garmetix-web && npm run build'

if [ "$SKIP_DOCKER" -eq 0 ]; then
  run_step docker-compose-config docker compose config
  if [ "$NO_CACHE_API" -eq 1 ]; then
    run_step docker-compose-build-api-no-cache docker compose build --no-cache api
    run_step docker-compose-build-web docker compose build web
  else
    run_step docker-compose-build docker compose build
  fi
  run_step docker-compose-up bash -lc 'docker compose up -d && sleep 12 && docker compose ps'
  run_step api-health bash -lc 'curl -fsS http://localhost:5080/api/health'
  run_step web-health bash -lc 'curl -fsSI http://localhost:3000 | head -20'
  run_step docker-logs-tail bash -lc "docker compose logs api --tail=200 | tee '$RESULTS/api-tail-$STAMP.log'; docker compose logs web --tail=120 | tee '$RESULTS/web-tail-$STAMP.log'; docker compose logs postgres --tail=120 | tee '$RESULTS/postgres-tail-$STAMP.log'"
  if [ "$KEEP_RUNNING" -eq 0 ]; then
    run_step docker-compose-down docker compose down
  fi
fi

echo "## Final note" >> "$SUMMARY"
echo "If this file shows PASS for backend, frontend, docker, API health, and web health, the build is ready for manual functional testing." >> "$SUMMARY"
echo "Validation complete. Summary: $SUMMARY"
