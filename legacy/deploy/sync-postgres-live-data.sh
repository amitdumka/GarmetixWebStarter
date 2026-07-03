#!/usr/bin/env bash
set -euo pipefail

SOURCE_HOST="${GMX_SOURCE_HOST:-192.168.11.126}"
SOURCE_USER="${GMX_SOURCE_SSH_USER:-amit}"
TARGET_HOST="${GMX_TARGET_HOST:-192.168.11.127}"
TARGET_USER="${GMX_TARGET_SSH_USER:-amitkumar}"
SOURCE_APP_DIR="${GMX_SOURCE_APP_DIR:-/opt/garmetix}"
TARGET_APP_DIR="${GMX_TARGET_APP_DIR:-/opt/garmetix}"
COMPOSE_FILE="${GMX_COMPOSE_FILE:-docker-compose.prod.yml}"
REMOTE_DUMP="${GMX_REMOTE_DUMP:-/tmp/garmetix-live-data.dump}"
LOCAL_DUMP="${GMX_LOCAL_DUMP:-/tmp/garmetix-live-data-$(date +%Y%m%d-%H%M%S).dump}"

if [[ "${GMX_CONFIRM_RESTORE:-}" != "YES" ]]; then
  cat <<'EOF'
Refusing to restore without explicit confirmation.

This script copies PostgreSQL data from the source host to the target host and
replaces the target PostgreSQL database.

Set:
  export GMX_CONFIRM_RESTORE=YES

Optional:
  export GMX_SSH_PASSWORD='...'
  export GMX_SOURCE_SSH_USER=amit
  export GMX_TARGET_SSH_USER=amitkumar
  export GMX_SOURCE_APP_DIR=/opt/garmetix
  export GMX_TARGET_APP_DIR=/opt/garmetix
EOF
  exit 2
fi

SSH=(ssh -o StrictHostKeyChecking=accept-new)
SCP=(scp -o StrictHostKeyChecking=accept-new)
if [[ -n "${GMX_SSH_PASSWORD:-}" ]]; then
  if ! command -v sshpass >/dev/null 2>&1; then
    echo "GMX_SSH_PASSWORD is set, but sshpass is not installed. Install sshpass or use SSH keys." >&2
    exit 3
  fi
  SSH=(sshpass -p "${GMX_SSH_PASSWORD}" ssh -o StrictHostKeyChecking=accept-new)
  SCP=(sshpass -p "${GMX_SSH_PASSWORD}" scp -o StrictHostKeyChecking=accept-new)
fi

SOURCE="${SOURCE_USER}@${SOURCE_HOST}"
TARGET="${TARGET_USER}@${TARGET_HOST}"

echo "==> Dump source database from ${SOURCE}:${SOURCE_APP_DIR}"
"${SSH[@]}" "${SOURCE}" "bash -s" -- "${SOURCE_APP_DIR}" "${COMPOSE_FILE}" "${REMOTE_DUMP}" <<'REMOTE_SOURCE'
set -euo pipefail
app_dir="$1"
compose_file="$2"
dump_path="$3"
cd "$app_dir"
set -a
[[ -f .env ]] && . ./.env
set +a
db_name="${POSTGRES_DB:-garmetix}"
db_user="${POSTGRES_USER:-garmetix}"
docker compose -f "$compose_file" up -d postgres
docker compose -f "$compose_file" exec -T postgres pg_dump -U "$db_user" -d "$db_name" -Fc > "$dump_path"
ls -lh "$dump_path"
REMOTE_SOURCE

echo "==> Copy dump to local ${LOCAL_DUMP}"
"${SCP[@]}" "${SOURCE}:${REMOTE_DUMP}" "${LOCAL_DUMP}"

echo "==> Copy dump to target ${TARGET}:${REMOTE_DUMP}"
"${SCP[@]}" "${LOCAL_DUMP}" "${TARGET}:${REMOTE_DUMP}"

echo "==> Restore target database on ${TARGET}:${TARGET_APP_DIR}"
"${SSH[@]}" "${TARGET}" "bash -s" -- "${TARGET_APP_DIR}" "${COMPOSE_FILE}" "${REMOTE_DUMP}" <<'REMOTE_TARGET'
set -euo pipefail
app_dir="$1"
compose_file="$2"
dump_path="$3"
cd "$app_dir"
set -a
[[ -f .env ]] && . ./.env
set +a
db_name="${POSTGRES_DB:-garmetix}"
db_user="${POSTGRES_USER:-garmetix}"
docker compose -f "$compose_file" up -d postgres
docker compose -f "$compose_file" stop api web || true
docker compose -f "$compose_file" exec -T postgres dropdb -U "$db_user" --if-exists "$db_name"
docker compose -f "$compose_file" exec -T postgres createdb -U "$db_user" "$db_name"
docker compose -f "$compose_file" exec -T postgres pg_restore -U "$db_user" -d "$db_name" --no-owner --no-privileges < "$dump_path"
docker compose -f "$compose_file" up -d
docker compose -f "$compose_file" exec -T postgres psql -U "$db_user" -d "$db_name" -c 'select now() as restored_at;'
REMOTE_TARGET

echo "==> Live data sync completed."
