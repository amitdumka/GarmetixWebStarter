#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

STAMP="$(date -u +%Y%m%d-%H%M%S)"
FILE="backups/garmetix-cli-${STAMP}.dump"
mkdir -p backups

docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml exec -T postgres \
  pg_dump --format=custom --compress=6 --no-owner --no-privileges \
  --username "$POSTGRES_USER" --file "/backups/$(basename "$FILE")" "$POSTGRES_DB"

sha256sum "$FILE" > "$FILE.sha256"
APP_DATA_VOLUME="${APP_DATA_VOLUME:-${COMPOSE_PROJECT_NAME:-garmetix}_garmetix_app_data}"
if docker volume inspect "$APP_DATA_VOLUME" >/dev/null 2>&1; then
  docker run --rm -v "${APP_DATA_VOLUME}:/appdata:ro" -v "$(pwd)/backups:/backup" alpine sh -lc 'if [ -d /appdata/purchase-imports ] && [ "$(find /appdata/purchase-imports -type f | head -1)" ]; then tar -czf "/backup/'"$(basename "$FILE")"'.purchase-import-proofs.tar.gz" -C /appdata purchase-imports; fi'
elif [[ -d "./data/purchase-imports" ]]; then
  tar -czf "$FILE.purchase-import-proofs.tar.gz" -C ./data purchase-imports
fi
[[ -f "$FILE.purchase-import-proofs.tar.gz" ]] && sha256sum "$FILE.purchase-import-proofs.tar.gz" > "$FILE.purchase-import-proofs.tar.gz.sha256"
cat > "$FILE.manifest.json" <<JSON
{
  "fileName": "$(basename "$FILE")",
  "source": "cli",
  "createdAtUtc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "database": "$POSTGRES_DB",
  "application": "Garmetix",
  "stage": "Stage 11D-130 Sale Billing Final QA Closure",
  "proofArchiveFileName": "$(basename "$FILE").purchase-import-proofs.tar.gz",
  "proofArchivePresent": $(if [[ -f "$FILE.purchase-import-proofs.tar.gz" ]]; then echo true; else echo false; fi)
}
JSON

echo "Created $FILE"
