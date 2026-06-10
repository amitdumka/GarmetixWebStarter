#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.yml}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
DB_SERVICE="${DB_SERVICE:-postgres}"
DB_NAME="${POSTGRES_DB:-garmetix}"
DB_USER="${POSTGRES_USER:-garmetix}"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
OUT="${BACKUP_DIR}/garmetix-manual-${STAMP}.dump"

mkdir -p "${BACKUP_DIR}"

echo "Creating PostgreSQL custom-format backup: ${OUT}"
docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" \
  pg_dump --format=custom --compress=6 --no-owner --no-privileges \
  --username "${DB_USER}" --file "/tmp/garmetix-${STAMP}.dump" "${DB_NAME}"

docker compose -f "${COMPOSE_FILE}" cp "${DB_SERVICE}:/tmp/garmetix-${STAMP}.dump" "${OUT}"
docker compose -f "${COMPOSE_FILE}" exec -T "${DB_SERVICE}" rm -f "/tmp/garmetix-${STAMP}.dump"

sha256sum "${OUT}" > "${OUT}.sha256"
cat > "${OUT}.manifest.json" <<JSON
{
  "fileName": "$(basename "${OUT}")",
  "createdAtUtc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "source": "manual-shell",
  "database": "${DB_NAME}",
  "service": "${DB_SERVICE}",
  "format": "PostgreSQL custom pg_dump",
  "sha256": "$(cut -d ' ' -f1 "${OUT}.sha256")"
}
JSON

echo "Backup complete: ${OUT}"
echo "Checksum: $(cat "${OUT}.sha256")"
