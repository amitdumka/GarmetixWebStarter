#!/usr/bin/env bash
set -euo pipefail
DEPLOY_ROOT="${1:-/opt/garmetix}"
CURRENT_LINK="$DEPLOY_ROOT/current"
PREVIOUS_LINK="$DEPLOY_ROOT/previous"

[[ -L "$PREVIOUS_LINK" ]] || { echo "No previous release symlink found at $PREVIOUS_LINK" >&2; exit 1; }
PREVIOUS_DIR="$(readlink -f "$PREVIOUS_LINK")"
[[ -d "$PREVIOUS_DIR" ]] || { echo "Previous release directory missing: $PREVIOUS_DIR" >&2; exit 1; }

if [[ -L "$CURRENT_LINK" ]]; then
  CURRENT_DIR="$(readlink -f "$CURRENT_LINK")"
  (cd "$CURRENT_DIR" && docker compose --env-file .env.production -f docker-compose.prod.yml down) || true
fi

ln -sfn "$PREVIOUS_DIR" "$CURRENT_LINK"
cd "$CURRENT_LINK"
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
scripts/linux/monitor-health.sh .env.production

echo "Rolled back to $PREVIOUS_DIR"
