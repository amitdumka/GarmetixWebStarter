#!/usr/bin/env bash
set -Eeuo pipefail

log(){ printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warn(){ printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }

sudo_cmd(){
  if [[ -n "${GARMETIX_SUDO_PASSWORD:-}" ]]; then
    printf '%s\n' "$GARMETIX_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
}

log "Updating apt and installing base tools"
sudo_cmd apt-get update -y
sudo_cmd env DEBIAN_FRONTEND=noninteractive apt-get install -y \
  ca-certificates curl gnupg lsb-release rsync openssh-server jq python3 \
  iproute2 net-tools ethtool iw

log "Ensuring SSH server is enabled"
sudo_cmd systemctl enable ssh || sudo_cmd systemctl enable sshd || true
sudo_cmd systemctl restart ssh || sudo_cmd systemctl restart sshd || true

if ! command -v docker >/dev/null 2>&1; then
  log "Installing Docker using official convenience script"
  tmp="$(mktemp)"
  curl -fsSL https://get.docker.com -o "$tmp"
  sudo_cmd sh "$tmp"
  rm -f "$tmp"
else
  log "Docker already installed"
fi

log "Enabling Docker service"
sudo_cmd systemctl enable docker
sudo_cmd systemctl start docker

if ! docker compose version >/dev/null 2>&1; then
  log "Installing docker-compose-plugin from apt if available"
  sudo_cmd env DEBIAN_FRONTEND=noninteractive apt-get install -y docker-compose-plugin || true
fi

log "Adding current user to docker group"
sudo_cmd usermod -aG docker "$USER" || true

log "Host install completed. If docker permission fails, log out/in once or reboot."
