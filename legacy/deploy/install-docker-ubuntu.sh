#!/usr/bin/env bash
set -Eeuo pipefail

log() { printf '\n[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"; }

if [[ "${EUID}" -ne 0 ]]; then
  echo "This script must run as root. Use: sudo bash deploy/install-docker-ubuntu.sh" >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive

log "Installing base packages"
apt-get update -y
apt-get install -y ca-certificates curl gnupg lsb-release openssl unzip tar jq

if command -v docker >/dev/null 2>&1; then
  log "Docker command already exists; starting/enabling Docker service"
  systemctl enable --now docker >/dev/null 2>&1 || service docker start || true
else
  log "Installing Docker Engine from Docker official apt repository"
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc

  . /etc/os-release
  DOCKER_CODENAME="${UBUNTU_CODENAME:-${VERSION_CODENAME:-}}"
  ARCH="$(dpkg --print-architecture)"

  cat >/etc/apt/sources.list.d/docker.sources <<EOF2
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${DOCKER_CODENAME}
Components: stable
Architectures: ${ARCH}
Signed-By: /etc/apt/keyrings/docker.asc
EOF2

  if apt-get update -y; then
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  else
    log "Docker official repo did not update cleanly for codename '${DOCKER_CODENAME}'. Falling back to Ubuntu packages."
    rm -f /etc/apt/sources.list.d/docker.sources
    apt-get update -y
    apt-get install -y docker.io docker-compose-v2 || apt-get install -y docker.io docker-compose
  fi
fi

systemctl enable --now docker >/dev/null 2>&1 || service docker start || true

if [[ -n "${SUDO_USER:-}" && "${SUDO_USER}" != "root" ]]; then
  usermod -aG docker "${SUDO_USER}" || true
fi

if docker compose version >/dev/null 2>&1; then
  log "Docker Compose plugin detected: $(docker compose version)"
elif command -v docker-compose >/dev/null 2>&1; then
  log "Legacy docker-compose detected: $(docker-compose --version)"
else
  echo "Docker installed, but Docker Compose was not found." >&2
  exit 1
fi

log "Docker is ready: $(docker --version)"
