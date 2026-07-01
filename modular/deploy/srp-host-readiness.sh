#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP host readiness

Usage:
  bash modular/deploy/srp-host-readiness.sh
  bash modular/deploy/srp-host-readiness.sh --local-only
  bash modular/deploy/srp-host-readiness.sh --install-remote-packages

Checks the WSL/local deploy runtime and the Ubuntu SRP target before running
the full deployment script.
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MODULAR_ROOT="$REPO_ROOT/modular"
DEFAULT_CONFIG_PATH="$HOME/.config/garmetix/srp-deploy.env"
CONFIG_PATH="${GARMETIX_SRP_DEPLOY_CONFIG:-$DEFAULT_CONFIG_PATH}"

LOCAL_ONLY=false
INSTALL_REMOTE_PACKAGES=false

for arg in "$@"; do
  case "$arg" in
    --local-only) LOCAL_ONLY=true ;;
    --install-remote-packages) INSTALL_REMOTE_PACKAGES=true ;;
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
    CONFIG_WAS_AUTO_DETECTED=true
  fi
fi

if [ -f "$CONFIG_PATH" ]; then
  # shellcheck disable=SC1090
  source "$CONFIG_PATH"
else
  echo "Config not found at $CONFIG_PATH; using defaults. Run deploy --init-config first."
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
SRP_REMOTE_BASE="${SRP_REMOTE_BASE:-/opt/garmetix-srp}"
SRP_NGINX_PORT="${SRP_NGINX_PORT:-8088}"
SRP_API_PORT="${SRP_API_PORT:-5080}"
SRP_API_ENV_PATH="${SRP_API_ENV_PATH:-/etc/garmetix/srp-api.env}"
SRP_API_PUBLISH_SELF_CONTAINED="${SRP_API_PUBLISH_SELF_CONTAINED:-true}"
SRP_CLOUDFLARE_CREDENTIALS_FILE="${SRP_CLOUDFLARE_CREDENTIALS_FILE:-/etc/cloudflared/garmetix-srp.json}"
SRP_CLOUDFLARE_CONFIG_PATH="${SRP_CLOUDFLARE_CONFIG_PATH:-/etc/cloudflared/garmetix-srp.yml}"

FAILURES=0
WARNINGS=0

pass() { echo "PASS $1"; }
warn() { WARNINGS=$((WARNINGS + 1)); echo "WARN $1"; }
fail() { FAILURES=$((FAILURES + 1)); echo "FAIL $1"; }

check_local_command() {
  local command_name="$1"
  local required="${2:-true}"
  local fallback_name="${3:-}"
  if command -v "$command_name" >/dev/null 2>&1; then
    pass "local command '$command_name' is available"
  elif [ -n "$fallback_name" ] && command -v "$fallback_name" >/dev/null 2>&1; then
    pass "local command '$command_name' is available through '$fallback_name'"
  elif [ "$required" = true ]; then
    fail "local command '$command_name' is missing"
  else
    warn "local command '$command_name' is missing"
  fi
}

ssh_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    if ! command -v sshpass >/dev/null 2>&1; then
      fail "sshpass is required for password auth but is missing locally"
      return 1
    fi
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e ssh -p "$SRP_SSH_PORT" -o BatchMode=no -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  else
    ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  fi
}

shell_quote() {
  printf "%q" "$1"
}

remote_sudo_env_prefix() {
  local sudo_password="${SRP_SUDO_PASSWORD:-${SRP_SSH_PASSWORD:-}}"
  if [ -n "$sudo_password" ]; then
    printf "export SRP_REMOTE_SUDO_PASSWORD=%s; " "$(shell_quote "$sudo_password")"
  fi
}

remote_sudo_function() {
  cat <<'REMOTE_SUDO'
sudo_cmd() {
  if [ -n "${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
};
REMOTE_SUDO
}

remote_check() {
  local label="$1"
  local command_text="$2"
  local required="${3:-true}"
  if output="$(ssh_cmd "$command_text" 2>&1)"; then
    pass "$label"
    if [ -n "$output" ]; then
      printf '%s\n' "$output" | sed 's/^/  /'
    fi
  elif [ "$required" = true ]; then
    fail "$label"
    printf '%s\n' "$output" | sed 's/^/  /'
  else
    warn "$label"
    printf '%s\n' "$output" | sed 's/^/  /'
  fi
}

remote_sudo_check() {
  local label="$1"
  local command_text="$2"
  local required="${3:-true}"
  local sudo_prefix
  sudo_prefix="$(remote_sudo_env_prefix)"
  remote_check "$label" "${sudo_prefix}$(remote_sudo_function) $command_text" "$required"
}

cat <<PLAN
SRP host readiness
  Target:       $SRP_DEPLOY_TARGET
  Domain:       $SRP_DOMAIN
  Config file:  $CONFIG_PATH
  Secrets file: $SRP_SECRETS_PATH
  Auth mode:    $(if [ -n "${SRP_SSH_PASSWORD:-}" ]; then echo "password via sshpass"; else echo "SSH key or interactive"; fi)
PLAN

echo
echo "Local checks"
check_local_command node true node.exe
check_local_command npm
check_local_command dotnet true dotnet.exe
check_local_command ssh
check_local_command tar
check_local_command rsync false
if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
  check_local_command sshpass
fi

if [ ! -f "$MODULAR_ROOT/package.json" ]; then
  fail "modular package.json not found"
else
  pass "modular package.json found"
fi

if [ "$LOCAL_ONLY" = true ]; then
  echo
  echo "Local-only mode complete."
  echo "Failures: $FAILURES, warnings: $WARNINGS"
  exit "$FAILURES"
fi

echo
echo "Remote checks"
remote_check "SSH login works" "printf 'connected as '; whoami"
remote_check "Remote OS" "uname -a"
remote_check "Remote tar is available" "command -v tar"
remote_check "Remote sudo works" "$(remote_sudo_env_prefix)$(remote_sudo_function) sudo_cmd true"

if [ "$INSTALL_REMOTE_PACKAGES" = true ]; then
  remote_sudo_check "Install base remote packages" "sudo_cmd apt-get update && sudo_cmd env DEBIAN_FRONTEND=noninteractive apt-get install -y nginx curl ca-certificates tar postgresql-client"
fi

remote_check "Nginx is installed" "command -v nginx && nginx -v" false
if [ "$SRP_API_PUBLISH_SELF_CONTAINED" = true ]; then
  remote_check "dotnet is installed (optional for self-contained API)" "command -v dotnet && dotnet --list-runtimes | sed -n '1,8p'" false
  remote_check "ASP.NET Core runtime is available (optional for self-contained API)" "dotnet --list-runtimes 2>/dev/null | grep -E 'Microsoft.AspNetCore.App (10|[1-9][0-9])\\.'" false
else
  remote_check "dotnet is installed" "command -v dotnet && dotnet --list-runtimes | sed -n '1,8p'" false
  remote_check "ASP.NET Core runtime is available" "dotnet --list-runtimes 2>/dev/null | grep -E 'Microsoft.AspNetCore.App (10|[1-9][0-9])\\.'" false
fi
remote_check "cloudflared is installed" "command -v cloudflared && cloudflared --version" false
remote_check "PostgreSQL client is installed" "command -v psql && psql --version" false
remote_sudo_check "SRP API env exists" "sudo_cmd test -f '$SRP_API_ENV_PATH' && sudo_cmd sed -n '1,12p' '$SRP_API_ENV_PATH' | sed -E 's/(Password=)[^;[:space:]]+/\\1***REDACTED/g; s/(Jwt__SigningKey=).*/\\1***REDACTED/g'" false
remote_check "Cloudflare tunnel credentials exist" "test -f '$SRP_CLOUDFLARE_CREDENTIALS_FILE' && ls -l '$SRP_CLOUDFLARE_CREDENTIALS_FILE'" false
remote_check "Cloudflare SRP config exists" "test -f '$SRP_CLOUDFLARE_CONFIG_PATH' && sed -n '1,20p' '$SRP_CLOUDFLARE_CONFIG_PATH'" false
remote_check "SRP remote base status" "if [ -d '$SRP_REMOTE_BASE' ]; then ls -ld '$SRP_REMOTE_BASE'; else echo '$SRP_REMOTE_BASE not created yet'; fi" false
remote_check "SRP ports in use" "ss -ltnp 2>/dev/null | grep -E ':($SRP_NGINX_PORT|$SRP_API_PORT)\\b' || true" false

echo
echo "Summary"
echo "Failures: $FAILURES"
echo "Warnings: $WARNINGS"

if [ "$FAILURES" -gt 0 ]; then
  exit 1
fi
