#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP runtime bootstrap

Usage:
  bash modular/deploy/srp-runtime-bootstrap.sh
  bash modular/deploy/srp-runtime-bootstrap.sh --skip-cloudflared
  bash modular/deploy/srp-runtime-bootstrap.sh --restart-api

Bootstraps the SRP Ubuntu host runtime after the static/API package has been
uploaded:
  - installs PostgreSQL server
  - creates the SRP database and user
  - writes /etc/garmetix/srp-api.env with generated secrets
  - starts/restarts garmetix-srp-api.service
  - installs cloudflared package, without creating a tunnel token
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEFAULT_CONFIG_PATH="$HOME/.config/garmetix/srp-deploy.env"
CONFIG_PATH="${GARMETIX_SRP_DEPLOY_CONFIG:-$DEFAULT_CONFIG_PATH}"
SKIP_CLOUDFLARED=false
RESTART_API=false

for arg in "$@"; do
  case "$arg" in
    --skip-cloudflared) SKIP_CLOUDFLARED=true ;;
    --restart-api) RESTART_API=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage; exit 1 ;;
  esac
done

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

if [ -f "$CONFIG_PATH" ]; then
  # shellcheck disable=SC1090
  source "$CONFIG_PATH"
else
  echo "Config not found at $CONFIG_PATH; using defaults."
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
SRP_DOMAIN="${SRP_DOMAIN:-srp.aadwikafashion.in}"
SRP_API_ENV_PATH="${SRP_API_ENV_PATH:-/etc/garmetix/srp-api.env}"
SRP_DB_NAME="${SRP_DB_NAME:-garmetix_srp}"
SRP_DB_USER="${SRP_DB_USER:-garmetix}"
SRP_API_PORT="${SRP_API_PORT:-5080}"
SRP_CLOUDFLARE_CREDENTIALS_FILE="${SRP_CLOUDFLARE_CREDENTIALS_FILE:-/etc/cloudflared/garmetix-srp.json}"
SRP_CLOUDFLARE_CONFIG_PATH="${SRP_CLOUDFLARE_CONFIG_PATH:-/etc/cloudflared/garmetix-srp.yml}"

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

remote_script="$(cat <<'REMOTE'
set -euo pipefail

sudo_cmd() {
  if [ -n "${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' true
    sudo "$@"
  else
    sudo "$@"
  fi
}

write_root_file() {
  local path="$1"
  local content="$2"
  printf '%s' "$content" | sudo_cmd tee "$path" >/dev/null
}

generate_secret() {
  openssl rand -base64 "$1" | tr -d '\n'
}

sudo_cmd apt-get update
sudo_cmd env DEBIAN_FRONTEND=noninteractive apt-get install -y postgresql openssl curl ca-certificates
sudo_cmd systemctl enable --now postgresql

DB_PASSWORD="${SRP_DB_PASSWORD:-}"
if [ -z "$DB_PASSWORD" ]; then
  DB_PASSWORD="$(generate_secret 32)"
fi

JWT_KEY="${SRP_JWT_SIGNING_KEY:-}"
if [ -z "$JWT_KEY" ]; then
  JWT_KEY="$(generate_secret 48)"
fi

sudo_cmd -u postgres psql -v ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${SRP_DB_USER}') THEN
    CREATE ROLE ${SRP_DB_USER} LOGIN PASSWORD '${DB_PASSWORD}';
  ELSE
    ALTER ROLE ${SRP_DB_USER} WITH LOGIN PASSWORD '${DB_PASSWORD}';
  END IF;
END
\$\$;
SELECT 'CREATE DATABASE ${SRP_DB_NAME} OWNER ${SRP_DB_USER}'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${SRP_DB_NAME}')\\gexec
GRANT ALL PRIVILEGES ON DATABASE ${SRP_DB_NAME} TO ${SRP_DB_USER};
SQL

sudo_cmd mkdir -p "$(dirname "$SRP_API_ENV_PATH")"
API_ENV_CONTENT="Database__AutoMigrate=true
Database__SchemaBootstrapMode=FreshBaseline
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=${SRP_DB_NAME};Username=${SRP_DB_USER};Password=${DB_PASSWORD}
Cors__AllowedOriginsCsv=https://${SRP_DOMAIN}
Jwt__Issuer=Garmetix
Jwt__Audience=Garmetix
Jwt__SigningKey=${JWT_KEY}
PasswordReset__FrontendBaseUrl=https://${SRP_DOMAIN}
License__EnforcementEnabled=false
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:${SRP_API_PORT}
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
"
write_root_file "$SRP_API_ENV_PATH" "$API_ENV_CONTENT"
sudo_cmd chmod 600 "$SRP_API_ENV_PATH"

if [ "${SKIP_CLOUDFLARED}" != "true" ]; then
  sudo_cmd mkdir -p --mode=0755 /usr/share/keyrings
  curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg | sudo_cmd tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null
  echo "deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main" | sudo_cmd tee /etc/apt/sources.list.d/cloudflared.list >/dev/null
  sudo_cmd apt-get update
  sudo_cmd env DEBIAN_FRONTEND=noninteractive apt-get install -y cloudflared
fi

sudo_cmd systemctl daemon-reload
sudo_cmd systemctl enable garmetix-srp-api.service
sudo_cmd systemctl restart garmetix-srp-api.service
sleep 3
sudo_cmd systemctl --no-pager --full status garmetix-srp-api.service | sed -n '1,18p'

echo
echo "API health:"
curl -fsS "http://127.0.0.1:${SRP_API_PORT}/api/health" || true

echo
echo "Cloudflared:"
if command -v cloudflared >/dev/null 2>&1; then
  cloudflared --version
  if [ -f "$SRP_CLOUDFLARE_CREDENTIALS_FILE" ]; then
    echo "Tunnel credentials exist at $SRP_CLOUDFLARE_CREDENTIALS_FILE"
  else
    echo "Tunnel credentials missing at $SRP_CLOUDFLARE_CREDENTIALS_FILE"
  fi
else
  echo "cloudflared not installed"
fi
REMOTE
)"

sudo_prefix="$(remote_sudo_env_prefix)"

cat <<PLAN
SRP runtime bootstrap
  Target:       $SRP_DEPLOY_TARGET
  Domain:       $SRP_DOMAIN
  API env:      $SRP_API_ENV_PATH
  Database:     $SRP_DB_NAME
  DB user:      $SRP_DB_USER
  Cloudflared:  $(if [ "$SKIP_CLOUDFLARED" = true ]; then echo "skip"; else echo "install"; fi)
PLAN

ssh_cmd "${sudo_prefix} export SRP_DOMAIN=$(shell_quote "$SRP_DOMAIN"); export SRP_API_ENV_PATH=$(shell_quote "$SRP_API_ENV_PATH"); export SRP_DB_NAME=$(shell_quote "$SRP_DB_NAME"); export SRP_DB_USER=$(shell_quote "$SRP_DB_USER"); export SRP_DB_PASSWORD=$(shell_quote "${SRP_DB_PASSWORD:-}"); export SRP_JWT_SIGNING_KEY=$(shell_quote "${SRP_JWT_SIGNING_KEY:-}"); export SRP_API_PORT=$(shell_quote "$SRP_API_PORT"); export SKIP_CLOUDFLARED=$(shell_quote "$SKIP_CLOUDFLARED"); export SRP_CLOUDFLARE_CREDENTIALS_FILE=$(shell_quote "$SRP_CLOUDFLARE_CREDENTIALS_FILE"); export SRP_CLOUDFLARE_CONFIG_PATH=$(shell_quote "$SRP_CLOUDFLARE_CONFIG_PATH"); bash -s" <<<"$remote_script"
