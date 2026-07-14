#!/usr/bin/env bash
set -euo pipefail

echo "== Garmetix production release preflight =="

echo "1) Backend restore/build check"
( cd backend && dotnet restore && dotnet build --configuration Release )

echo "2) Frontend dependency/build check"
( cd frontend/garmetix-web && npm ci && npm run build )

echo "3) Docker compose config check"
docker compose config >/tmp/garmetix-compose-config.yml

echo "4) Static consistency scripts"
python3 scripts/validation/stage4d-static-checks.py
python3 scripts/validation/stage4e-static-checks.py
python3 scripts/validation/stage5a-static-checks.py

echo "Preflight passed."
