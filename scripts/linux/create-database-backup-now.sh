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
cat > "$FILE.manifest.json" <<JSON
{
  "fileName": "$(basename "$FILE")",
  "source": "cli",
  "createdAtUtc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "database": "$POSTGRES_DB",
  "application": "Garmetix",
  "stage": "Stage 8G Package 1 Backup Restore Maintenance"
}
JSON

echo "Created $FILE"
