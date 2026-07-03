#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${1:-/opt/garmetix/current}"
API_BASE="${API_BASE:-http://localhost:5000}"
cd "$PROJECT_ROOT"

echo "Stage 11D-102 Purchase/Vendor Payment Live QA"
echo "Project root: $PROJECT_ROOT"

echo "Docker compose config check..."
docker compose config >/dev/null

echo "API health check..."
curl -fsS "$API_BASE/health" >/dev/null || curl -fsS "$API_BASE/api/health" >/dev/null

echo "App version check..."
APP_INFO="$(curl -fsS "$API_BASE/api/app-info/version" || true)"
echo "$APP_INFO"
if ! printf '%s' "$APP_INFO" | grep -q '4.12.17'; then
  echo "WARNING: app-info did not report v4.12.17. Check deployment/container cache."
fi

echo "Vendor payment API smoke check requires auth in normal deployment."
echo "Open Purchase page and test: current month payments, search, mode filter, pagination, edit, delete."
echo "Then run Stage 11D-101 DB check for accounting consistency."
