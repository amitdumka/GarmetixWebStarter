#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${1:-.env.production}"
COMPOSE_FILE="${GARMETIX_COMPOSE_FILE:-docker-compose.prod.yml}"
POSTGRES_SERVICE="${GARMETIX_POSTGRES_SERVICE:-postgres}"
BACKUP_DIR="${GARMETIX_BACKUP_DIR:-./backups}"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
DRILL_DB="garmetix_restore_drill_${STAMP//-/}"
BACKUP_FILE="$BACKUP_DIR/garmetix-drill-${STAMP}.dump"
MARKER_FILE="$BACKUP_DIR/restore-drill-status.json"

printf 'Garmetix backup/restore drill\n'
printf 'Env file: %s\n' "$ENV_FILE"
printf 'Compose file: %s\n' "$COMPOSE_FILE"
printf 'Temporary database: %s\n' "$DRILL_DB"

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 2; }; }
require_cmd docker
require_cmd python3

if [ ! -f "$ENV_FILE" ]; then
  echo "Environment file was not found: $ENV_FILE" >&2
  exit 2
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

POSTGRES_DB="${POSTGRES_DB:-garmetix}"
POSTGRES_USER="${POSTGRES_USER:-garmetix}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-}"
COMPOSE_ARGS=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE")
mkdir -p "$BACKUP_DIR"

cleanup() {
  set +e
  docker compose "${COMPOSE_ARGS[@]}" exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_SERVICE" \
    dropdb --if-exists -U "$POSTGRES_USER" "$DRILL_DB" >/dev/null 2>&1
}
trap cleanup EXIT

printf '\n[1/6] Ensure PostgreSQL container is running...\n'
docker compose "${COMPOSE_ARGS[@]}" up -d "$POSTGRES_SERVICE"

printf '\n[2/6] Create production backup dump on host...\n'
docker compose "${COMPOSE_ARGS[@]}" exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_SERVICE" \
  pg_dump -Fc --no-owner --no-privileges -U "$POSTGRES_USER" "$POSTGRES_DB" > "$BACKUP_FILE"

if [ ! -s "$BACKUP_FILE" ]; then
  echo "Backup file was not created or is empty: $BACKUP_FILE" >&2
  exit 3
fi

printf 'Backup created: %s (%s bytes)\n' "$BACKUP_FILE" "$(wc -c < "$BACKUP_FILE")"

printf '\n[3/6] Validate backup header and pg_restore listing...\n'
python3 - "$BACKUP_FILE" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1])
if p.read_bytes()[:5] != b'PGDMP':
    raise SystemExit(f'{p} is not a PostgreSQL custom-format dump')
print('PGDMP header OK')
PY

docker compose "${COMPOSE_ARGS[@]}" exec -T "$POSTGRES_SERVICE" pg_restore --version >/dev/null
cat "$BACKUP_FILE" | docker compose "${COMPOSE_ARGS[@]}" exec -T "$POSTGRES_SERVICE" \
  sh -c 'cat > /tmp/garmetix-drill.dump && pg_restore --list /tmp/garmetix-drill.dump >/tmp/garmetix-drill-list.txt'

printf '\n[4/6] Restore into disposable database...\n'
docker compose "${COMPOSE_ARGS[@]}" exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_SERVICE" \
  createdb -U "$POSTGRES_USER" "$DRILL_DB"
docker compose "${COMPOSE_ARGS[@]}" exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_SERVICE" \
  pg_restore --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$DRILL_DB" /tmp/garmetix-drill.dump

printf '\n[5/6] Verify required table coverage in disposable database...\n'
TABLE_REPORT=$(docker compose "${COMPOSE_ARGS[@]}" exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_SERVICE" \
  psql -U "$POSTGRES_USER" -d "$DRILL_DB" -Atc "select table_name from information_schema.tables where table_schema='public' order by table_name")
TABLE_REPORT="$TABLE_REPORT" python3 - <<'PY'
import os
required = {
    'Companies', 'StoreGroups', 'Stores', 'Users', 'Products', 'Stocks',
    'SalesInvoices', 'PurchaseInvoices', 'Ledgers', 'Vouchers',
    'PettyCashSheets', 'CashDetails', 'Employees'
}
found = {line.strip() for line in os.environ.get('TABLE_REPORT', '').splitlines() if line.strip()}
missing = sorted(required - found)
if missing:
    raise SystemExit('Missing required tables after restore: ' + ', '.join(missing))
print(f'Required tables OK: {len(required)} checked; restored tables: {len(found)} total')
PY

printf '\n[6/6] Write restore drill marker...\n'
BACKUP_FILE="$BACKUP_FILE" DRILL_DB="$DRILL_DB" MARKER_FILE="$MARKER_FILE" python3 - <<'PY'
import json, os, pathlib, datetime
backup = pathlib.Path(os.environ['BACKUP_FILE'])
marker = pathlib.Path(os.environ['MARKER_FILE'])
payload = {
    'status': 'Pass',
    'completedAtUtc': datetime.datetime.utcnow().replace(microsecond=0).isoformat() + 'Z',
    'backupFile': backup.name,
    'backupSizeBytes': backup.stat().st_size,
    'temporaryDatabase': os.environ['DRILL_DB'],
    'message': 'Backup restored into a disposable PostgreSQL database and required Garmetix tables were verified.'
}
marker.write_text(json.dumps(payload, indent=2), encoding='utf-8')
print(marker)
PY

printf '\nBackup/restore drill completed successfully. Production database was not modified.\n'
