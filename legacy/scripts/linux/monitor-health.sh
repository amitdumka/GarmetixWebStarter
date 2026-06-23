#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

API_URL="${PUBLIC_HTTPS_URL:-http://localhost:${WEB_PORT:-3000}}/api/health"
LOCAL_API_URL="http://localhost:${API_PORT:-5080}/api/health"

echo "Checking local API: $LOCAL_API_URL"
curl -fsS "$LOCAL_API_URL" || { echo; echo "Local API health failed" >&2; exit 1; }
echo

echo "Checking public API through web/proxy: $API_URL"
if curl -fsS "$API_URL"; then
  echo
  echo "Public health check OK."
else
  echo
  echo "Public health failed. If HTTPS/tunnel is not enabled yet, this can be expected." >&2
  exit 2
fi
