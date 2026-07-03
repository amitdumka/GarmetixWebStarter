#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 /path/to/garmetix-backup.dump"
  exit 1
fi

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.yml}"
DB_SERVICE="${DB_SERVICE:-postgres}"
DB_NAME="${POSTGRES_DB:-garmetix}"
DB_USER="${POSTGRES_USER:-garmetix}"
BACKUP="$1"

if [[ ! -f "${BACKUP}" ]]; then
  echo "Backup file not found: ${BACKUP}"
  exit 1
fi

if [[ -f "${BACKUP}.sha256" ]]; then
  echo "Verifying checksum..."
  sha256sum -c "${BACKUP}.sha256"
fi

if ! head -c 5 "${BACKUP}" | grep -q "PGDMP"; then
  echo "This is not a PostgreSQL custom-format dump. Expected PGDMP header."
  exit 1
fi

echo "Running pg_restore --list preflight..."
docker compose -f "${COMPOSE_FILE}" cp "${BACKUP}" "${DB_SERVICE}:/tmp/garmetix-restore.dump"
docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" pg_restore --list "/tmp/garmetix-restore.dump" >/tmp/garmetix-restore-list.txt
head -30 /tmp/garmetix-restore-list.txt

echo
read -r -p "Type RESTORE to replace database ${DB_NAME}: " CONFIRM
if [[ "${CONFIRM}" != "RESTORE" ]]; then
  echo "Restore cancelled."
  docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" rm -f "/tmp/garmetix-restore.dump"
  exit 1
fi

SAFETY="./backups/garmetix-pre-restore-$(date -u +%Y%m%d-%H%M%S).dump"
mkdir -p ./backups
echo "Creating safety backup: ${SAFETY}"
"$(dirname "$0")/backup-db.sh"

echo "Restoring database..."
docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" \
  pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error --single-transaction \
  --username "${DB_USER}" --dbname "${DB_NAME}" "/tmp/garmetix-restore.dump"

docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" rm -f "/tmp/garmetix-restore.dump"
PROOF_ARCHIVE="${BACKUP}.purchase-import-proofs.tar.gz"
if [[ -f "${PROOF_ARCHIVE}" ]]; then
  echo "Restoring purchase invoice proof files from ${PROOF_ARCHIVE}..."
  APP_DATA_VOLUME="${APP_DATA_VOLUME:-${COMPOSE_PROJECT_NAME:-garmetix}_garmetix_app_data}"
  if docker volume inspect "${APP_DATA_VOLUME}" >/dev/null 2>&1; then
    docker run --rm -v "${APP_DATA_VOLUME}:/appdata" -v "$(cd "$(dirname "${PROOF_ARCHIVE}")" && pwd):/backup:ro" alpine sh -lc 'mkdir -p /appdata && tar -xzf "/backup/'"$(basename "${PROOF_ARCHIVE}")"'" -C /appdata'
  else
    mkdir -p ./data
    tar -xzf "${PROOF_ARCHIVE}" -C ./data
  fi
fi
echo "Restore completed. Restart application services if needed."
