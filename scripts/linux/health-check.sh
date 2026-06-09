#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"

echo "Checking ${BASE_URL}/api/health"
curl --fail --show-error --silent "${BASE_URL}/api/health" | python3 -m json.tool
