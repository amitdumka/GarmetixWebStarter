#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
BACKUP_FILE="${2:-}"
if [ -z "$BACKUP_FILE" ]; then
  echo "Usage: $0 /opt/garmetix/current /path/to/full-db-before-admin-data-YYYYMMDD-HHMMSS.dump" >&2
  exit 1
fi
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value(){ local key="$1"; grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
echo "This will restore full database from: $BACKUP_FILE"
read -r -p "Type RESTORE FULL DB to continue: " CONFIRM
if [ "$CONFIRM" != "RESTORE FULL DB" ]; then
  echo "Restore cancelled."
  exit 1
fi
TMP="/tmp/$(basename "$BACKUP_FILE")"
docker cp "$BACKUP_FILE" "$PG_CONTAINER:$TMP"
docker exec "$PG_CONTAINER" dropdb -U "$PG_USER" --if-exists "$PG_DB"
docker exec "$PG_CONTAINER" createdb -U "$PG_USER" "$PG_DB"
docker exec "$PG_CONTAINER" pg_restore -U "$PG_USER" -d "$PG_DB" --clean --if-exists "$TMP" || docker exec "$PG_CONTAINER" pg_restore -U "$PG_USER" -d "$PG_DB" "$TMP"
PROOF_ARCHIVE="$BACKUP_FILE.purchase-import-proofs.tar.gz"
if [[ -f "$PROOF_ARCHIVE" ]]; then
  echo "Restoring purchase invoice proof archive: $PROOF_ARCHIVE"
  APP_DATA_VOLUME="${APP_DATA_VOLUME:-${COMPOSE_PROJECT_NAME:-garmetix}_garmetix_app_data}"
  if docker volume inspect "$APP_DATA_VOLUME" >/dev/null 2>&1; then
    docker run --rm -v "${APP_DATA_VOLUME}:/appdata" -v "$(cd "$(dirname "$PROOF_ARCHIVE")" && pwd):/backup:ro" alpine sh -lc 'mkdir -p /appdata && tar -xzf "/backup/'"$(basename "$PROOF_ARCHIVE")"'" -C /appdata'
  elif [[ -d "$ROOT/data" ]]; then
    tar -xzf "$PROOF_ARCHIVE" -C "$ROOT/data"
  fi
fi
echo "Full database restore completed. Restart API if needed."
