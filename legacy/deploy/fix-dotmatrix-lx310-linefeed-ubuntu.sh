#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_DIR="${PROJECT_DIR:-/opt/garmetix/current}"
ENV_FILE="${GARMETIX_DOTMATRIX_ENV:-/etc/garmetix-dotmatrix.env}"
BRIDGE_SRC="${PROJECT_DIR}/deploy/garmetix-dotmatrix-bridge.sh"
SERVICE="garmetix-dotmatrix-bridge.service"
PRINTER="${GARMETIX_DOTMATRIX_PRINTER:-EPSON_LX310}"

if [[ ! -f "$BRIDGE_SRC" ]]; then
  echo "Bridge script not found: $BRIDGE_SRC" >&2
  echo "Copy this patch into $PROJECT_DIR first." >&2
  exit 2
fi

sudo chmod +x "$BRIDGE_SRC"

sudo mkdir -p "$(dirname "$ENV_FILE")"
if [[ ! -f "$ENV_FILE" ]]; then
  sudo touch "$ENV_FILE"
fi

set_env() {
  local key="$1" value="$2"
  if sudo grep -qE "^${key}=" "$ENV_FILE"; then
    sudo sed -i "s|^${key}=.*|${key}=${value}|" "$ENV_FILE"
  else
    printf '%s=%s\n' "$key" "$value" | sudo tee -a "$ENV_FILE" >/dev/null
  fi
}

set_env GARMETIX_DOTMATRIX_PRINTER "$PRINTER"
set_env GARMETIX_DOTMATRIX_LP_OPTIONS '"-o raw"'
set_env GARMETIX_DOTMATRIX_NORMALIZE_CRLF true
set_env GARMETIX_DOTMATRIX_RESET_BEFORE_JOB true

sudo systemctl daemon-reload
sudo systemctl restart "$SERVICE"
sudo systemctl reset-failed "$SERVICE" || true

echo "LX-310 line-feed/carriage-return fix applied."
echo "Printer queue : $PRINTER"
echo "Bridge script : $BRIDGE_SRC"
echo "Env file      : $ENV_FILE"
echo
echo "Run this direct CRLF test:"
echo "printf 'LEFT-1\\nLEFT-2\\nLEFT-3\\n' | awk '{ printf \"%s\\\\r\\\\n\", \\$0 }' | lp -d '$PRINTER' -o raw"
echo
echo "Check bridge:"
echo "sudo ./deploy/check-dotmatrix-bridge-status.sh"
