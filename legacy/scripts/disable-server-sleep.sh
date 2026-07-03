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

sudo_install_text(){
  local dest="$1"
  local tmp
  tmp="$(mktemp)"
  cat > "$tmp"
  sudo_cmd install -m 0644 "$tmp" "$dest"
  rm -f "$tmp"
}

log "Masking sleep/suspend/hibernate targets"
sudo_cmd systemctl mask sleep.target suspend.target hibernate.target hybrid-sleep.target suspend-then-hibernate.target || true

log "Configuring systemd-logind to ignore idle and lid/suspend actions"
sudo_cmd mkdir -p /etc/systemd/logind.conf.d
sudo_install_text /etc/systemd/logind.conf.d/99-garmetix-disable-idle-sleep.conf <<'CONF'
[Login]
IdleAction=ignore
HandleSuspendKey=ignore
HandleHibernateKey=ignore
HandleLidSwitch=ignore
HandleLidSwitchExternalPower=ignore
HandleLidSwitchDocked=ignore
CONF
sudo_cmd systemctl restart systemd-logind || true

log "Disabling Wi-Fi power saving where possible"
if command -v iw >/dev/null 2>&1; then
  for IFACE in $(iw dev 2>/dev/null | awk '$1=="Interface"{print $2}'); do
    sudo_cmd iw dev "$IFACE" set power_save off || true
  done
fi

if systemctl list-unit-files | grep -q '^NetworkManager.service'; then
  sudo_cmd mkdir -p /etc/NetworkManager/conf.d
  sudo_install_text /etc/NetworkManager/conf.d/99-garmetix-disable-wifi-powersave.conf <<'CONF'
[connection]
wifi.powersave = 2
CONF
  sudo_cmd systemctl restart NetworkManager || true
fi

log "Sleep prevention applied"
