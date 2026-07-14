#!/usr/bin/env bash
set -euo pipefail

RELEASE_ZIP="${1:?Usage: deploy-release.sh <release-zip> [env-file] [deploy-root]}"
ENV_FILE="${2:-.env.production}"
DEPLOY_ROOT="${3:-/opt/garmetix}"
STAMP="$(date +%Y%m%d-%H%M%S)"
RELEASE_DIR="$DEPLOY_ROOT/releases/$STAMP"
CURRENT_LINK="$DEPLOY_ROOT/current"
PREVIOUS_LINK="$DEPLOY_ROOT/previous"

[[ -f "$RELEASE_ZIP" ]] || { echo "Missing release zip: $RELEASE_ZIP" >&2; exit 1; }
[[ -f "$ENV_FILE" ]] || { echo "Missing env file: $ENV_FILE" >&2; exit 1; }

mkdir -p "$DEPLOY_ROOT/releases" "$DEPLOY_ROOT/shared/backups" "$DEPLOY_ROOT/shared/secrets"

if [[ -L "$CURRENT_LINK" ]]; then
  ln -sfn "$(readlink -f "$CURRENT_LINK")" "$PREVIOUS_LINK"
fi

mkdir -p "$RELEASE_DIR"
unzip -q "$RELEASE_ZIP" -d "$RELEASE_DIR"
cp "$ENV_FILE" "$RELEASE_DIR/.env.production"
ln -sfn "$DEPLOY_ROOT/shared/backups" "$RELEASE_DIR/backups"
ln -sfn "$DEPLOY_ROOT/shared/secrets" "$RELEASE_DIR/secrets"

cd "$RELEASE_DIR"

if [[ -L "$PREVIOUS_LINK" && -f "$PREVIOUS_LINK/docker-compose.prod.yml" ]]; then
  echo "Creating safety backup from previous release before switch..."
  (cd "$PREVIOUS_LINK" && docker compose --env-file .env.production -f docker-compose.prod.yml exec -T postgres pg_dump -U "${POSTGRES_USER:-garmetix}" -d "${POSTGRES_DB:-garmetix}" -Fc > "$DEPLOY_ROOT/shared/backups/pre-update-$STAMP.dump") || true
fi

scripts/linux/production-preflight.sh .env.production

docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
ln -sfn "$RELEASE_DIR" "$CURRENT_LINK"

if scripts/linux/monitor-health.sh .env.production; then
  echo "Deployment completed: $RELEASE_DIR"
else
  echo "Deployment health failed. Run scripts/linux/rollback-release.sh if needed." >&2
  exit 2
fi
