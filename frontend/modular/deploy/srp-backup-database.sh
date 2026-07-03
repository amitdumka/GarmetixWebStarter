#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP database backup

Usage:
  bash frontend/modular/deploy/srp-backup-database.sh --dry-run
  bash frontend/modular/deploy/srp-backup-database.sh
  bash frontend/modular/deploy/srp-backup-database.sh --list

Creates a PostgreSQL custom-format backup on the SRP Ubuntu host before
controlled live POS acceptance or deployment.

Reads config from:
  $GARMETIX_SRP_DEPLOY_CONFIG, or ~/.config/garmetix/srp-deploy.env

Optional private secrets file:
  $SRP_SECRETS_PATH, or ~/.config/garmetix/srp-deploy.secrets.env
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
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
LIST_ONLY=false

for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=true ;;
    --list) LIST_ONLY=true ;;
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
SRP_BACKUP_DIR="${SRP_BACKUP_DIR:-$SRP_REMOTE_BASE/backups}"

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
SRP database backup plan
  Target:       $SRP_DEPLOY_TARGET
  API env:      $SRP_API_ENV_PATH
  Backup dir:   $SRP_BACKUP_DIR
  Config file:  $CONFIG_PATH
  Secrets file: $SRP_SECRETS_PATH
  Mode:         $(if [ "$DRY_RUN" = true ]; then echo "dry-run"; elif [ "$LIST_ONLY" = true ]; then echo "list"; else echo "backup"; fi)
PLAN

if [ "$DRY_RUN" = true ]; then
  echo "DRY SSH $SRP_DEPLOY_TARGET test API env, parse connection string and create pg_dump backup."
  exit 0
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

if [ ! -f "$SRP_API_ENV_PATH" ]; then
  echo "API env file not found: $SRP_API_ENV_PATH" >&2
  exit 2
fi

if [ "$LIST_ONLY" = true ]; then
  sudo_cmd mkdir -p "$SRP_BACKUP_DIR"
  sudo_cmd find "$SRP_BACKUP_DIR" -maxdepth 1 -type f \( -name '*.dump' -o -name '*.sha256' \) -printf '%TY-%Tm-%Td %TH:%TM %s %p\n' 2>/dev/null | sort | tail -20
  exit 0
fi

if ! command -v pg_dump >/dev/null 2>&1; then
  echo "pg_dump is not installed on the SRP host." >&2
  exit 3
fi

CONNECTION_STRING="$(sudo_cmd sed -n 's/^ConnectionStrings__Default=//p' "$SRP_API_ENV_PATH" | tail -1)"
if [ -z "$CONNECTION_STRING" ]; then
  echo "ConnectionStrings__Default was not found in $SRP_API_ENV_PATH" >&2
  exit 4
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
  exit 5
fi

sudo_cmd mkdir -p "$SRP_BACKUP_DIR"
sudo_cmd chown "$(id -u):$(id -g)" "$SRP_BACKUP_DIR"

STAMP="$(TZ=Asia/Kolkata date +%Y%m%d-%H%M%S)"
BACKUP_FILE="$SRP_BACKUP_DIR/garmetix-srp-v${GARMETIX_VERSION}-${STAMP}-manual.dump"

export PGPASSWORD="$DB_PASSWORD"
pg_dump -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -Fc -f "$BACKUP_FILE"
unset PGPASSWORD

sha256sum "$BACKUP_FILE" > "$BACKUP_FILE.sha256"
chmod 600 "$BACKUP_FILE" "$BACKUP_FILE.sha256"

echo "Backup completed:"
ls -lh "$BACKUP_FILE" "$BACKUP_FILE.sha256"
REMOTE
)"

ssh_cmd "${sudo_prefix} export SRP_API_ENV_PATH=$(shell_quote "$SRP_API_ENV_PATH"); export SRP_BACKUP_DIR=$(shell_quote "$SRP_BACKUP_DIR"); export GARMETIX_VERSION=$(shell_quote "${GARMETIX_VERSION:-6.0.2}"); export LIST_ONLY=$(shell_quote "$LIST_ONLY"); bash -s" <<<"$remote_script"
