#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP database restore drill

Usage:
  bash frontend/modular/deploy/srp-restore-drill.sh --dry-run
  bash frontend/modular/deploy/srp-restore-drill.sh --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
  bash frontend/modular/deploy/srp-restore-drill.sh --confirm-non-production-restore --backup-file=garmetix-srp-db-YYYYMMDD-HHMMSS-IST-Stage-vX.dump

Safety:
  - Restores only into a non-production database.
  - Refuses the live database name.
  - Refuses custom restore database names unless --allow-custom-restore-db is supplied.
  - Defaults restore database names to garmetix_restore_drill_<stage>_<timestamp>.
  - Verifies .sha256, pg_restore -l, SQL smoke and temporary API /api/health smoke.

Reads config from:
  $GARMETIX_SRP_DEPLOY_CONFIG, or ~/.config/garmetix/srp-deploy.env

Optional private secrets file:
  $SRP_SECRETS_PATH, or ~/.config/garmetix/srp-deploy.secrets.env
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
MODULAR_ROOT="$REPO_ROOT/frontend/modular"
DEFAULT_CONFIG_PATH="$HOME/.config/garmetix/srp-deploy.env"
CONFIG_PATH="${GARMETIX_SRP_DEPLOY_CONFIG:-$DEFAULT_CONFIG_PATH}"

detect_windows_config_path() {
  case "$REPO_ROOT" in
    /mnt/?/Users/*/*)
      local drive user
      drive="$(printf '%s' "$REPO_ROOT" | cut -d/ -f3)"
      user="$(printf '%s' "$REPO_ROOT" | cut -d/ -f5)"
      printf '/mnt/%s/Users/%s/.config/garmetix/srp-deploy.env' "$drive" "$user"
      ;;
    /?/Users/*/*)
      local drive user
      drive="$(printf '%s' "$REPO_ROOT" | cut -d/ -f2)"
      user="$(printf '%s' "$REPO_ROOT" | cut -d/ -f4)"
      printf '/%s/Users/%s/.config/garmetix/srp-deploy.env' "$drive" "$user"
      ;;
  esac
}

if [ -z "${GARMETIX_SRP_DEPLOY_CONFIG:-}" ] && [ ! -f "$CONFIG_PATH" ]; then
  DETECTED_CONFIG_PATH="$(detect_windows_config_path || true)"
  if [ -n "${DETECTED_CONFIG_PATH:-}" ] && [ -f "$DETECTED_CONFIG_PATH" ]; then
    CONFIG_PATH="$DETECTED_CONFIG_PATH"
  fi
fi

DRY_RUN=false
CONFIRM_RESTORE=false
ALLOW_CUSTOM_RESTORE_DB=false
KEEP_RESTORE_DB=false
APP_SMOKE=true
STAGE_NAME="${GARMETIX_RESTORE_STAGE:-BS21RestoreDrillProductionSafety}"
BACKUP_FILE=""
RESTORE_DB=""
SMOKE_PORT="${SRP_RESTORE_DRILL_SMOKE_PORT:-18088}"

for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=true ;;
    --confirm-non-production-restore) CONFIRM_RESTORE=true ;;
    --allow-custom-restore-db) ALLOW_CUSTOM_RESTORE_DB=true ;;
    --keep-restore-db) KEEP_RESTORE_DB=true ;;
    --skip-app-smoke) APP_SMOKE=false ;;
    --stage=*) STAGE_NAME="${arg#--stage=}" ;;
    --backup-file=*) BACKUP_FILE="${arg#--backup-file=}" ;;
    --restore-db=*) RESTORE_DB="${arg#--restore-db=}" ;;
    --smoke-port=*) SMOKE_PORT="${arg#--smoke-port=}" ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage; exit 1 ;;
  esac
done

if [ -f "$CONFIG_PATH" ]; then
  # shellcheck disable=SC1090
  source "$CONFIG_PATH"
else
  echo "Config not found at $CONFIG_PATH; using safe defaults."
fi

SRP_SECRETS_PATH="${SRP_SECRETS_PATH:-$HOME/.config/garmetix/srp-deploy.secrets.env}"
RAW_SRP_SECRETS_PATH="$SRP_SECRETS_PATH"
if [[ "$SRP_SECRETS_PATH" == "~/"* ]]; then
  SRP_SECRETS_PATH="$HOME/${SRP_SECRETS_PATH#"~/"}"
fi
CONFIG_DIR="$(dirname "$CONFIG_PATH")"
CONFIG_SIDE_SECRETS="$CONFIG_DIR/srp-deploy.secrets.env"
if [ -f "$CONFIG_SIDE_SECRETS" ]; then
  if [[ "$RAW_SRP_SECRETS_PATH" == "~/"* ]] || [[ "$SRP_SECRETS_PATH" == "$HOME/.config/garmetix/srp-deploy.secrets.env" ]]; then
    SRP_SECRETS_PATH="$CONFIG_SIDE_SECRETS"
  fi
fi
if [ -f "$SRP_SECRETS_PATH" ]; then
  # shellcheck disable=SC1090
  source "$SRP_SECRETS_PATH"
fi

SRP_DEPLOY_TARGET="${SRP_DEPLOY_TARGET:-amitkumar@192.168.11.127}"
SRP_SSH_PORT="${SRP_SSH_PORT:-22}"
SRP_REMOTE_BASE="${SRP_REMOTE_BASE:-/opt/garmetix-srp}"
SRP_API_ENV_PATH="${SRP_API_ENV_PATH:-/etc/garmetix/srp-api.env}"
SRP_BACKUP_DIR="${SRP_BACKUP_DIR:-/opt/garmetix/backup/database}"
SRP_GARMETIX_VERSION="${GARMETIX_VERSION:-}"
if [ -z "$SRP_GARMETIX_VERSION" ] && [ -f "$MODULAR_ROOT/config/version.ts" ]; then
  SRP_GARMETIX_VERSION="$(sed -n "s/.*version: '\([^']*\)'.*/\1/p" "$MODULAR_ROOT/config/version.ts" | head -1)"
fi
SRP_GARMETIX_VERSION="${SRP_GARMETIX_VERSION:-6.0.0}"
SRP_GIT_COMMIT="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"

need_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

shell_quote() {
  printf "%q" "$1"
}

ssh_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    need_command sshpass
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  else
    ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  fi
}

remote_sudo_env_prefix() {
  local sudo_password="${SRP_SUDO_PASSWORD:-${SRP_SSH_PASSWORD:-}}"
  if [ -n "$sudo_password" ]; then
    printf "export SRP_REMOTE_SUDO_PASSWORD=%s; " "$(shell_quote "$sudo_password")"
  fi
}

cat <<PLAN
SRP restore drill plan
  Target:        $SRP_DEPLOY_TARGET
  API env:       $SRP_API_ENV_PATH
  Remote base:   $SRP_REMOTE_BASE
  Backup dir:    $SRP_BACKUP_DIR
  Stage:         $STAGE_NAME
  Backup file:   ${BACKUP_FILE:-latest .dump in backup dir}
  Restore DB:    ${RESTORE_DB:-auto garmetix_restore_drill_<stage>_<timestamp>}
  App smoke:     $APP_SMOKE
  Keep DB:       $KEEP_RESTORE_DB
  Version:       $SRP_GARMETIX_VERSION
  Git commit:    $SRP_GIT_COMMIT
  Config file:   $CONFIG_PATH
  Secrets file:  $SRP_SECRETS_PATH
  Mode:          $(if [ "$DRY_RUN" = true ]; then echo "dry-run"; else echo "restore-drill"; fi)
PLAN

if [ "$DRY_RUN" = true ]; then
  echo "DRY SSH $SRP_DEPLOY_TARGET verify latest backup, checksum, pg_restore list, restore to non-production DB and run /api/health smoke."
  exit 0
fi

if [ "$CONFIRM_RESTORE" != true ]; then
  echo "Refusing to run restore drill without --confirm-non-production-restore." >&2
  echo "This command creates/drops a non-production restore database on the SRP database host." >&2
  exit 2
fi

sudo_prefix="$(remote_sudo_env_prefix)"

remote_script="$(cat <<'REMOTE'
set -euo pipefail

sudo_cmd() {
  if [ -n "${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
}

conn_value() {
  local key="$1"
  printf '%s' "$CONNECTION_STRING" | tr ';' '\n' | awk -F= -v wanted="$key" 'tolower($1) == tolower(wanted) { print substr($0, index($0, "=") + 1); exit }'
}

need_remote_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required remote command: $1" >&2
    exit 10
  fi
}

safe_name() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]' | tr -cd '[:alnum:]_' | cut -c1-54
}

if [ ! -f "$SRP_API_ENV_PATH" ]; then
  echo "API env file not found: $SRP_API_ENV_PATH" >&2
  exit 11
fi

need_remote_command pg_restore
need_remote_command psql
need_remote_command createdb
need_remote_command dropdb
need_remote_command sha256sum
if [ "$APP_SMOKE" = true ]; then
  need_remote_command curl
fi

CONNECTION_STRING="$(sudo_cmd sed -n 's/^ConnectionStrings__Default=//p' "$SRP_API_ENV_PATH" | tail -1)"
if [ -z "$CONNECTION_STRING" ]; then
  echo "ConnectionStrings__Default was not found in $SRP_API_ENV_PATH" >&2
  exit 12
fi

DB_HOST="$(conn_value Host)"
DB_PORT="$(conn_value Port)"
DB_NAME="$(conn_value Database)"
DB_USER="$(conn_value Username)"
DB_PASSWORD="$(conn_value Password)"

DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-5432}"

if [ -z "$DB_NAME" ] || [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
  echo "Could not parse database name/user/password from SRP API env." >&2
  exit 13
fi

sudo_cmd mkdir -p "$SRP_BACKUP_DIR"
sudo_cmd chown "$(id -u):$(id -g)" "$SRP_BACKUP_DIR"

if [ -z "$BACKUP_FILE" ]; then
  BACKUP_FILE="$(find "$SRP_BACKUP_DIR" -maxdepth 1 -type f -name '*.dump' -printf '%T@ %p\n' | sort -n | tail -1 | cut -d' ' -f2-)"
elif [[ "$BACKUP_FILE" != /* ]]; then
  BACKUP_FILE="$SRP_BACKUP_DIR/$BACKUP_FILE"
fi

if [ -z "$BACKUP_FILE" ] || [ ! -f "$BACKUP_FILE" ]; then
  echo "Backup file was not found. Run deploy:srp:backup first." >&2
  exit 14
fi

case "$BACKUP_FILE" in
  "$SRP_BACKUP_DIR"/*.dump) ;;
  *) echo "Backup file must be a .dump under $SRP_BACKUP_DIR." >&2; exit 15 ;;
esac

SHA_FILE="$BACKUP_FILE.sha256"
if [ ! -f "$SHA_FILE" ]; then
  echo "Checksum file was not found: $SHA_FILE" >&2
  exit 16
fi

STAMP="$(TZ=Asia/Kolkata date +%Y%m%d-%H%M%S)"
HUMAN_STAMP="$(TZ=Asia/Kolkata date '+%Y-%m-%d %H:%M:%S %Z')"
SAFE_STAGE="$(safe_name "$STAGE_NAME")"
if [ -z "$SAFE_STAGE" ]; then
  SAFE_STAGE="restore"
fi

if [ -z "$RESTORE_DB" ]; then
  RESTORE_DB="garmetix_restore_drill_${SAFE_STAGE}_${STAMP//[-]/_}"
fi

if [ "$RESTORE_DB" = "$DB_NAME" ]; then
  echo "Refusing to restore into the live database name: $RESTORE_DB" >&2
  exit 17
fi

if [ "$ALLOW_CUSTOM_RESTORE_DB" != true ] && [[ "$RESTORE_DB" != garmetix_restore_drill_* ]]; then
  echo "Restore DB must start with garmetix_restore_drill_ unless --allow-custom-restore-db is supplied." >&2
  exit 18
fi

HISTORY_FILE="$SRP_BACKUP_DIR/Backupfilehistory.md"
if [ ! -f "$HISTORY_FILE" ]; then
  echo "Backup history file is missing: $HISTORY_FILE" >&2
  exit 19
fi

if ! grep -Fq "$(basename "$BACKUP_FILE")" "$HISTORY_FILE"; then
  echo "Backup history does not reference $(basename "$BACKUP_FILE")." >&2
  exit 20
fi

echo "Verifying checksum..."
(cd "$SRP_BACKUP_DIR" && sha256sum -c "$(basename "$SHA_FILE")")

RESTORE_LIST="/tmp/${SAFE_STAGE}-${STAMP}-restore-list.txt"
echo "Reading pg_restore catalog..."
pg_restore -l "$BACKUP_FILE" > "$RESTORE_LIST"
if [ ! -s "$RESTORE_LIST" ]; then
  echo "pg_restore list output was empty." >&2
  exit 21
fi

export PGPASSWORD="$DB_PASSWORD"
echo "Restoring $(basename "$BACKUP_FILE") into non-production database $RESTORE_DB..."
dropdb -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" --if-exists "$RESTORE_DB"
createdb -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" "$RESTORE_DB"
pg_restore -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$RESTORE_DB" --clean --if-exists --no-owner --no-privileges "$BACKUP_FILE"

echo "Running SQL smoke against $RESTORE_DB..."
TABLE_COUNT="$(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$RESTORE_DB" -Atc "select count(*) from information_schema.tables where table_schema not in ('pg_catalog','information_schema');")"
JOURNAL_LINE_CHECK="$(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$RESTORE_DB" -Atc "select to_regclass('public.\"JournalLines\"') is not null;")"
if [ "${TABLE_COUNT:-0}" -le 0 ]; then
  echo "Restored database has no user tables." >&2
  exit 22
fi

APP_SMOKE_STATUS="Skipped"
if [ "$APP_SMOKE" = true ]; then
  API_DIR="$SRP_REMOTE_BASE/current/api"
  API_LOG="/tmp/garmetix-restore-drill-${SAFE_STAGE}-${STAMP}.log"
  API_URL="http://127.0.0.1:$SMOKE_PORT"
  RESTORE_CONNECTION="Host=$DB_HOST;Port=$DB_PORT;Database=$RESTORE_DB;Username=$DB_USER;Password=$DB_PASSWORD"
  echo "Starting temporary API smoke on $API_URL..."

  if [ -x "$API_DIR/Garmetix.Api" ]; then
    (
      cd "$API_DIR"
      env ASPNETCORE_URLS="$API_URL" ConnectionStrings__Default="$RESTORE_CONNECTION" ./Garmetix.Api > "$API_LOG" 2>&1
    ) &
  elif [ -f "$API_DIR/Garmetix.Api.dll" ]; then
    need_remote_command dotnet
    (
      cd "$API_DIR"
      env ASPNETCORE_URLS="$API_URL" ConnectionStrings__Default="$RESTORE_CONNECTION" dotnet Garmetix.Api.dll > "$API_LOG" 2>&1
    ) &
  else
    echo "API binary was not found under $API_DIR." >&2
    exit 23
  fi

  API_PID="$!"
  cleanup_api() {
    if kill -0 "$API_PID" >/dev/null 2>&1; then
      kill "$API_PID" >/dev/null 2>&1 || true
      wait "$API_PID" >/dev/null 2>&1 || true
    fi
  }
  trap cleanup_api EXIT

  for _ in $(seq 1 45); do
    if curl -fsS "$API_URL/api/health" >/tmp/garmetix-restore-drill-health.json 2>/dev/null; then
      APP_SMOKE_STATUS="Passed"
      break
    fi
    if ! kill -0 "$API_PID" >/dev/null 2>&1; then
      echo "Temporary API process exited early." >&2
      tail -80 "$API_LOG" >&2 || true
      exit 24
    fi
    sleep 1
  done

  if [ "$APP_SMOKE_STATUS" != "Passed" ]; then
    echo "Temporary API /api/health smoke did not pass." >&2
    tail -80 "$API_LOG" >&2 || true
    exit 25
  fi

  cleanup_api
  trap - EXIT
fi
unset PGPASSWORD

DRILL_HISTORY="$SRP_BACKUP_DIR/RestoreDrillHistory.md"
if [ ! -f "$DRILL_HISTORY" ]; then
  cat > "$DRILL_HISTORY" <<HISTORY
# Garmetix Database Restore Drill History

Location: \`$SRP_BACKUP_DIR\`

| Date/Time IST | Stage | Backup File | Restore DB | Restore Kept | SQL Tables | JournalLines Table | App Smoke | Version | Git Commit |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- |
HISTORY
fi

RESTORE_KEPT="$KEEP_RESTORE_DB"
if [ "$KEEP_RESTORE_DB" != true ]; then
  export PGPASSWORD="$DB_PASSWORD"
  echo "Dropping restore drill database $RESTORE_DB after successful smoke. Use --keep-restore-db to retain it."
  dropdb -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" --if-exists "$RESTORE_DB"
  unset PGPASSWORD
fi

printf '| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
  "$HUMAN_STAMP" \
  "$STAGE_NAME" \
  "$(basename "$BACKUP_FILE")" \
  "$RESTORE_DB" \
  "$RESTORE_KEPT" \
  "$TABLE_COUNT" \
  "$JOURNAL_LINE_CHECK" \
  "$APP_SMOKE_STATUS" \
  "$GARMETIX_VERSION" \
  "$GIT_COMMIT" >> "$DRILL_HISTORY"

chmod 600 "$DRILL_HISTORY"

echo "Restore drill completed:"
echo "  Backup:       $BACKUP_FILE"
echo "  Restore DB:   $RESTORE_DB"
echo "  Tables:       $TABLE_COUNT"
echo "  App smoke:    $APP_SMOKE_STATUS"
echo "  History file: $DRILL_HISTORY"
REMOTE
)"

ssh_cmd "${sudo_prefix} export SRP_API_ENV_PATH=$(shell_quote "$SRP_API_ENV_PATH"); export SRP_REMOTE_BASE=$(shell_quote "$SRP_REMOTE_BASE"); export SRP_BACKUP_DIR=$(shell_quote "$SRP_BACKUP_DIR"); export GARMETIX_VERSION=$(shell_quote "$SRP_GARMETIX_VERSION"); export GIT_COMMIT=$(shell_quote "$SRP_GIT_COMMIT"); export STAGE_NAME=$(shell_quote "$STAGE_NAME"); export BACKUP_FILE=$(shell_quote "$BACKUP_FILE"); export RESTORE_DB=$(shell_quote "$RESTORE_DB"); export ALLOW_CUSTOM_RESTORE_DB=$(shell_quote "$ALLOW_CUSTOM_RESTORE_DB"); export KEEP_RESTORE_DB=$(shell_quote "$KEEP_RESTORE_DB"); export APP_SMOKE=$(shell_quote "$APP_SMOKE"); export SMOKE_PORT=$(shell_quote "$SMOKE_PORT"); bash -s" <<<"$remote_script"
