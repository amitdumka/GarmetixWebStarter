#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG_FILE="${ROOT_DIR}/deploy/macmini.env"

if [[ ! -f "$CONFIG_FILE" ]]; then
  echo "Missing deploy/macmini.env. Create it first:" >&2
  echo "  cp deploy/macmini.env.example deploy/macmini.env" >&2
  echo "  nano deploy/macmini.env" >&2
  exit 1
fi

# shellcheck disable=SC1090
source "$CONFIG_FILE"

: "${SERVER_HOST:=192.168.11.125}"
: "${SERVER_USER:=amit}"
: "${SSH_PORT:=22}"
: "${REMOTE_APP_DIR:=/opt/garmetix}"
: "${DOMAIN:=garmetix.aadwikafashion.in}"
: "${PUBLIC_HTTPS_URL:=https://${DOMAIN}}"

SSH_BASE=(-p "$SSH_PORT" -o StrictHostKeyChecking=accept-new)
SCP_BASE=(-P "$SSH_PORT" -o StrictHostKeyChecking=accept-new)

use_sshpass=false
if [[ -n "${SSH_PASSWORD:-}" && "${SSH_PASSWORD}" != CHANGE_ME* ]]; then
  if ! command -v sshpass >/dev/null 2>&1; then
    echo "sshpass is required for password automation. Install it or use SSH key auth." >&2
    echo "Ubuntu/Debian: sudo apt-get install sshpass" >&2
    echo "macOS: brew install hudochenkov/sshpass/sshpass" >&2
    exit 1
  fi
  use_sshpass=true
fi

run_ssh() {
  if [[ "$use_sshpass" == true ]]; then
    sshpass -p "$SSH_PASSWORD" ssh "${SSH_BASE[@]}" "${SERVER_USER}@${SERVER_HOST}" "$@"
  else
    ssh "${SSH_BASE[@]}" "${SERVER_USER}@${SERVER_HOST}" "$@"
  fi
}

run_scp() {
  if [[ "$use_sshpass" == true ]]; then
    sshpass -p "$SSH_PASSWORD" scp "${SCP_BASE[@]}" "$@"
  else
    scp "${SCP_BASE[@]}" "$@"
  fi
}

export DOMAIN PUBLIC_HTTPS_URL CLOUDFLARE_TUNNEL_ID="${CLOUDFLARE_TUNNEL_ID:-}" CLOUDFLARE_TUNNEL_TOKEN="${CLOUDFLARE_TUNNEL_TOKEN:-}"
"${ROOT_DIR}/deploy/create-production-env.sh"

if [[ -n "${CLOUDFLARE_API_TOKEN:-}" && "${CLOUDFLARE_API_TOKEN}" != CHANGE_ME* ]]; then
  export CLOUDFLARE_API_TOKEN CLOUDFLARE_ACCOUNT_ID CLOUDFLARE_ZONE_ID CLOUDFLARE_TUNNEL_NAME="${CLOUDFLARE_TUNNEL_NAME:-garmetix-macmini}"
  "${ROOT_DIR}/deploy/cloudflare-create-or-update-tunnel.sh"
fi

RELEASE="release-$(date +%Y%m%d-%H%M%S)"
ARCHIVE="/tmp/garmetix-${RELEASE}.tar.gz"
REMOTE_ARCHIVE="/tmp/garmetix-${RELEASE}.tar.gz"

cd "$ROOT_DIR"
echo "Creating deployment archive..."
tar \
  --exclude='./.git' \
  --exclude='./frontend/garmetix-web/node_modules' \
  --exclude='./frontend/garmetix-web/.nuxt' \
  --exclude='./frontend/garmetix-web/.output' \
  --exclude='./backend/**/bin' \
  --exclude='./backend/**/obj' \
  --exclude='./backups' \
  -czf "$ARCHIVE" .

SUDO_PASS="${SUDO_PASSWORD:-}"
if [[ -z "$SUDO_PASS" && -n "${SSH_PASSWORD:-}" && "${SSH_PASSWORD}" != CHANGE_ME* ]]; then
  SUDO_PASS="$SSH_PASSWORD"
fi
SUDO_PASS_B64="$(printf '%s' "$SUDO_PASS" | base64 | tr -d '\n')"

run_scp "$ARCHIVE" "${SERVER_USER}@${SERVER_HOST}:${REMOTE_ARCHIVE}"
run_scp "${ROOT_DIR}/deploy/install-docker-ubuntu.sh" "${SERVER_USER}@${SERVER_HOST}:/tmp/install-docker-ubuntu.sh"

remote_script=$(cat <<'EOS'
set -Eeuo pipefail
REMOTE_APP_DIR="__REMOTE_APP_DIR__"
RELEASE="__RELEASE__"
REMOTE_ARCHIVE="__REMOTE_ARCHIVE__"
SUDO_PASS_B64="__SUDO_PASS_B64__"

sudo_run() {
  if [[ -n "$SUDO_PASS_B64" ]]; then
    local sp
    sp="$(printf '%s' "$SUDO_PASS_B64" | base64 -d)"
    printf '%s\n' "$sp" | sudo -S "$@"
  else
    sudo "$@"
  fi
}

sudo_run bash /tmp/install-docker-ubuntu.sh
sudo_run mkdir -p "${REMOTE_APP_DIR}/releases/${RELEASE}"
sudo_run chown -R "$USER:$USER" "$REMOTE_APP_DIR"
tar -xzf "$REMOTE_ARCHIVE" -C "${REMOTE_APP_DIR}/releases/${RELEASE}"
ln -sfn "${REMOTE_APP_DIR}/releases/${RELEASE}" "${REMOTE_APP_DIR}/current"
cd "${REMOTE_APP_DIR}/current"
chmod +x deploy/*.sh
./deploy/run-production.sh
rm -f "$REMOTE_ARCHIVE" /tmp/install-docker-ubuntu.sh
EOS
)
remote_script="${remote_script//__REMOTE_APP_DIR__/$REMOTE_APP_DIR}"
remote_script="${remote_script//__RELEASE__/$RELEASE}"
remote_script="${remote_script//__REMOTE_ARCHIVE__/$REMOTE_ARCHIVE}"
remote_script="${remote_script//__SUDO_PASS_B64__/$SUDO_PASS_B64}"

run_ssh "bash -s" <<<"$remote_script"
rm -f "$ARCHIVE"

echo "Deployment complete. Open: https://${DOMAIN}"
