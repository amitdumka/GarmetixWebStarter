#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
BACKUP_DIR="$ROOT/backups/attendance-final-import-v2"
LATEST="$(ls -1t "$BACKUP_DIR"/*.dump 2>/dev/null | head -1 || true)"
if [ -z "$LATEST" ]; then
  echo "No backup found in $BACKUP_DIR" >&2
  exit 1
fi

echo "This will restore the full database from:"
echo "  $LATEST"
echo "Type RESTORE to continue:"
read -r CONFIRM
if [ "$CONFIRM" != "RESTORE" ]; then
  echo "Restore cancelled."
  exit 0
fi

docker cp "$LATEST" "$PG_CONTAINER:/tmp/attendance-final-import-restore.dump"
docker exec "$PG_CONTAINER" pg_restore --clean --if-exists --no-owner -U "$PG_USER" -d "$PG_DB" /tmp/attendance-final-import-restore.dump

echo "Restore completed. Restarting api is recommended."
