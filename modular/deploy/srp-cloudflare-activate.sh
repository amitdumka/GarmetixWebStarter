#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP Cloudflare activation

Usage:
  bash modular/deploy/srp-cloudflare-activate.sh
  bash modular/deploy/srp-cloudflare-activate.sh --install
  bash modular/deploy/srp-cloudflare-activate.sh --verify-public

Status mode is read-only. Install mode activates cloudflared when the private
secrets file contains either:
  SRP_CLOUDFLARE_TUNNEL_TOKEN
  SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE
  SRP_CLOUDFLARE_CREDENTIALS_JSON_B64

Do not commit Cloudflare tokens or credential JSON.
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEFAULT_CONFIG_PATH="$HOME/.config/garmetix/srp-deploy.env"
CONFIG_PATH="${GARMETIX_SRP_DEPLOY_CONFIG:-$DEFAULT_CONFIG_PATH}"
INSTALL=false
VERIFY_PUBLIC=false

for arg in "$@"; do
  case "$arg" in
    --install) INSTALL=true ;;
    --verify-public) VERIFY_PUBLIC=true ;;
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
SRP_NGINX_PORT="${SRP_NGINX_PORT:-8088}"
SRP_CLOUDFLARE_TUNNEL_NAME="${SRP_CLOUDFLARE_TUNNEL_NAME:-garmetix-srp}"
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

scp_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    need_command sshpass
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e scp -P "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$1" "$SRP_DEPLOY_TARGET:$2"
  else
    scp -P "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$1" "$SRP_DEPLOY_TARGET:$2"
  fi
}

remote_sudo_env_prefix() {
  local sudo_password="${SRP_SUDO_PASSWORD:-${SRP_SSH_PASSWORD:-}}"
  if [ -n "$sudo_password" ]; then
    printf "export SRP_REMOTE_SUDO_PASSWORD=%s; " "$(shell_quote "$sudo_password")"
  fi
}

verify_url() {
  local label="$1"
  local url="$2"
  local expected="${3:-200}"
  local code
  code="$(curl -k -L -sS -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || true)"
  if [[ "$expected" == *"-"* ]]; then
    local low high
    low="${expected%-*}"
    high="${expected#*-}"
    if [ "$code" -ge "$low" ] && [ "$code" -le "$high" ]; then
      echo "PASS $label $url HTTP $code"
    else
      echo "WARN $label $url HTTP $code expected $expected"
    fi
  elif [ "$code" = "$expected" ]; then
    echo "PASS $label $url HTTP $code"
  else
    echo "WARN $label $url HTTP $code expected $expected"
  fi
}

if [ "$VERIFY_PUBLIC" = true ]; then
  need_command curl
  echo "SRP public verification"
  verify_url "Main" "https://$SRP_DOMAIN/" "200-499"
  verify_url "POS" "https://$SRP_DOMAIN/pos/" "200-499"
  verify_url "HR" "https://$SRP_DOMAIN/hr/" "200-499"
  verify_url "AI Sense" "https://$SRP_DOMAIN/ai-sense/" "200-499"
  verify_url "Books" "https://$SRP_DOMAIN/books/" "200-499"
  verify_url "Admin" "https://$SRP_DOMAIN/admin/" "200-499"
  verify_url "API health" "https://$SRP_DOMAIN/api/health" "200"
  exit 0
fi

REMOTE_CREDENTIALS_STAGING=""
if [ "$INSTALL" = true ] && [ -n "${SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE:-}" ]; then
  if [ ! -f "$SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE" ]; then
    echo "Credential file not found: $SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE" >&2
    exit 1
  fi
  REMOTE_CREDENTIALS_STAGING="/tmp/garmetix-srp-cloudflare-credentials.$$.$RANDOM.json"
  scp_cmd "$SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE" "$REMOTE_CREDENTIALS_STAGING"
fi

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

redact_secret_stream() {
  sed -E \
    -e 's/(--token[= ]+)[A-Za-z0-9._=-]+/\1[redacted]/g' \
    -e 's/(run --token )[A-Za-z0-9._=-]+/\1[redacted]/g' \
    -e 's/eyJ[A-Za-z0-9._=-]{40,}/[redacted-token]/g'
}

write_root_file() {
  local path="$1"
  local content="$2"
  printf '%s' "$content" | sudo_cmd tee "$path" >/dev/null
}

install_named_tunnel_service() {
  sudo_cmd mkdir -p "$(dirname "$SRP_CLOUDFLARE_CONFIG_PATH")"

  if [ -n "${SRP_REMOTE_CREDENTIALS_STAGING:-}" ]; then
    sudo_cmd mv "$SRP_REMOTE_CREDENTIALS_STAGING" "$SRP_CLOUDFLARE_CREDENTIALS_FILE"
    sudo_cmd chmod 600 "$SRP_CLOUDFLARE_CREDENTIALS_FILE"
  elif [ -n "${SRP_CLOUDFLARE_CREDENTIALS_JSON_B64:-}" ]; then
    printf '%s' "$SRP_CLOUDFLARE_CREDENTIALS_JSON_B64" | base64 -d | sudo_cmd tee "$SRP_CLOUDFLARE_CREDENTIALS_FILE" >/dev/null
    sudo_cmd chmod 600 "$SRP_CLOUDFLARE_CREDENTIALS_FILE"
  fi

  if [ ! -f "$SRP_CLOUDFLARE_CREDENTIALS_FILE" ]; then
    echo "Missing tunnel credentials at $SRP_CLOUDFLARE_CREDENTIALS_FILE"
    return 2
  fi

  CONFIG_CONTENT="tunnel: ${SRP_CLOUDFLARE_TUNNEL_NAME}
credentials-file: ${SRP_CLOUDFLARE_CREDENTIALS_FILE}

ingress:
  - hostname: ${SRP_DOMAIN}
    service: http://localhost:${SRP_NGINX_PORT}
  - service: http_status:404
"
  write_root_file "$SRP_CLOUDFLARE_CONFIG_PATH" "$CONFIG_CONTENT"
  sudo_cmd chmod 644 "$SRP_CLOUDFLARE_CONFIG_PATH"

  sudo_cmd tee /etc/systemd/system/cloudflared.service >/dev/null <<SERVICE
[Unit]
Description=Cloudflare Tunnel for Garmetix SRP
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/local/bin/cloudflared --no-autoupdate --config ${SRP_CLOUDFLARE_CONFIG_PATH} tunnel run
Restart=always
RestartSec=5s

[Install]
WantedBy=multi-user.target
SERVICE

  sudo_cmd systemctl daemon-reload
  sudo_cmd systemctl enable cloudflared.service
  sudo_cmd systemctl restart cloudflared.service
}

install_token_service() {
  sudo_cmd cloudflared service install "$SRP_CLOUDFLARE_TUNNEL_TOKEN" >/dev/null
  sudo_cmd systemctl enable cloudflared.service
  sudo_cmd systemctl restart cloudflared.service
}

echo "Cloudflared status on SRP host"
if command -v cloudflared >/dev/null 2>&1; then
  cloudflared --version
else
  echo "cloudflared is not installed"
  exit 3
fi

if [ "${SRP_INSTALL_CLOUDFLARE}" = "true" ]; then
  if [ -n "${SRP_CLOUDFLARE_TUNNEL_TOKEN:-}" ]; then
    echo "Installing cloudflared service using private tunnel token."
    install_token_service
  else
    echo "Installing cloudflared service using named tunnel credentials."
    install_named_tunnel_service
  fi
fi

echo
echo "Service:"
{
  sudo_cmd systemctl --no-pager show cloudflared.service \
    -p LoadState -p ActiveState -p SubState -p UnitFileState -p MainPID 2>&1 || true
  sudo_cmd journalctl -u cloudflared.service -n 8 --no-pager 2>&1 || true
} | redact_secret_stream | sed -n '1,18p'

echo
echo "Files:"
if [ -n "${SRP_CLOUDFLARE_TUNNEL_TOKEN:-}" ]; then
  echo "token-managed service active; named-tunnel credential JSON is not required"
elif [ -f "$SRP_CLOUDFLARE_CREDENTIALS_FILE" ]; then
  sudo_cmd ls -l "$SRP_CLOUDFLARE_CREDENTIALS_FILE"
else
  echo "missing $SRP_CLOUDFLARE_CREDENTIALS_FILE"
fi
if [ -f "$SRP_CLOUDFLARE_CONFIG_PATH" ]; then
  sudo_cmd sed -n '1,20p' "$SRP_CLOUDFLARE_CONFIG_PATH"
else
  echo "missing $SRP_CLOUDFLARE_CONFIG_PATH"
fi

echo
echo "Local SRP origin:"
curl -fsS -I "http://127.0.0.1:${SRP_NGINX_PORT}/" | sed -n '1,8p' || true
curl -fsS "http://127.0.0.1:${SRP_NGINX_PORT}/api/health" || true
REMOTE
)"

sudo_prefix="$(remote_sudo_env_prefix)"

cat <<PLAN
SRP Cloudflare activation
  Target:       $SRP_DEPLOY_TARGET
  Domain:       $SRP_DOMAIN
  Tunnel:       $SRP_CLOUDFLARE_TUNNEL_NAME
  Config file:  $CONFIG_PATH
  Secrets file: $SRP_SECRETS_PATH
  Mode:         $(if [ "$INSTALL" = true ]; then echo "install"; else echo "status"; fi)
PLAN

if [ "$INSTALL" = true ] && [ -z "${SRP_CLOUDFLARE_TUNNEL_TOKEN:-}" ] && [ -z "${SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE:-}" ] && [ -z "${SRP_CLOUDFLARE_CREDENTIALS_JSON_B64:-}" ]; then
  echo
  echo "No Cloudflare tunnel token or credentials were found in the private secrets file."
  echo "Status will still run, but install will not change the host."
  INSTALL=false
fi

ssh_cmd "${sudo_prefix} export SRP_INSTALL_CLOUDFLARE=$(shell_quote "$INSTALL"); export SRP_DOMAIN=$(shell_quote "$SRP_DOMAIN"); export SRP_NGINX_PORT=$(shell_quote "$SRP_NGINX_PORT"); export SRP_CLOUDFLARE_TUNNEL_NAME=$(shell_quote "$SRP_CLOUDFLARE_TUNNEL_NAME"); export SRP_CLOUDFLARE_CREDENTIALS_FILE=$(shell_quote "$SRP_CLOUDFLARE_CREDENTIALS_FILE"); export SRP_CLOUDFLARE_CONFIG_PATH=$(shell_quote "$SRP_CLOUDFLARE_CONFIG_PATH"); export SRP_CLOUDFLARE_TUNNEL_TOKEN=$(shell_quote "${SRP_CLOUDFLARE_TUNNEL_TOKEN:-}"); export SRP_CLOUDFLARE_CREDENTIALS_JSON_B64=$(shell_quote "${SRP_CLOUDFLARE_CREDENTIALS_JSON_B64:-}"); export SRP_REMOTE_CREDENTIALS_STAGING=$(shell_quote "$REMOTE_CREDENTIALS_STAGING"); bash -s" <<<"$remote_script"
