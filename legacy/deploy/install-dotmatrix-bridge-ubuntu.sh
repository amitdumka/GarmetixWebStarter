#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_DIR="${PROJECT_DIR:-/opt/garmetix/current}"
PRINTER_NAME="${GARMETIX_DOTMATRIX_PRINTER:-EPSON_LX310}"
DEVICE_URI="${GARMETIX_DOTMATRIX_DEVICE_URI:-}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}"
POSTGRES_SERVICE="${GARMETIX_POSTGRES_SERVICE:-postgres}"
ENV_FILE="${GARMETIX_DOTMATRIX_ENV:-/etc/garmetix-dotmatrix.env}"

if [[ $EUID -ne 0 ]]; then
  echo "Run with sudo: sudo PROJECT_DIR=$PROJECT_DIR GARMETIX_DOTMATRIX_PRINTER=$PRINTER_NAME $0" >&2
  exit 1
fi

if [[ ! -d "$PROJECT_DIR" ]]; then
  echo "Project directory not found: $PROJECT_DIR" >&2
  exit 2
fi

apt-get update
apt-get install -y cups cups-client coreutils util-linux
systemctl enable --now cups

# Stop old v4.12.46 spool-file timer if it exists; the DB bridge is the production-safe worker now.
systemctl disable --now garmetix-dotmatrix-spooler.timer 2>/dev/null || true
systemctl stop garmetix-dotmatrix-spooler.service 2>/dev/null || true

if lpstat -p "$PRINTER_NAME" >/dev/null 2>&1; then
  echo "CUPS printer queue already exists: $PRINTER_NAME"
elif [[ -n "$DEVICE_URI" ]]; then
  echo "Creating CUPS raw queue $PRINTER_NAME at $DEVICE_URI"
  lpadmin -p "$PRINTER_NAME" -E -v "$DEVICE_URI" -m raw
  lpoptions -d "$PRINTER_NAME"
else
  echo "CUPS printer queue $PRINTER_NAME does not exist yet."
  echo "Available detected devices:"
  lpinfo -v || true
  echo ""
  echo "Create/add the Epson LX-310 in Ubuntu Settings/Printers or rerun with:"
  echo "sudo GARMETIX_DOTMATRIX_DEVICE_URI='<device-uri-from-lpinfo-v>' GARMETIX_DOTMATRIX_PRINTER=$PRINTER_NAME $0"
  echo "The bridge service will still be installed; printing will work after the CUPS queue exists."
fi

cat >"$ENV_FILE" <<ENV
# Garmetix DotMatrix Bridge configuration
PROJECT_DIR=$PROJECT_DIR
COMPOSE_PROJECT_NAME=$COMPOSE_PROJECT_NAME
GARMETIX_COMPOSE_FILE=$PROJECT_DIR/docker-compose.prod.yml
GARMETIX_ENV_PRODUCTION=$PROJECT_DIR/.env.production
GARMETIX_POSTGRES_SERVICE=$POSTGRES_SERVICE
GARMETIX_DOTMATRIX_PRINTER=$PRINTER_NAME
GARMETIX_DOTMATRIX_LP_OPTIONS=-o raw
GARMETIX_DOTMATRIX_DEFAULT_ENABLED=false
GARMETIX_DOTMATRIX_DEFAULT_OUTPUT_MODE=BridgeService
GARMETIX_DOTMATRIX_POLL_SECONDS=2
GARMETIX_DOTMATRIX_BATCH_SIZE=20
GARMETIX_DOTMATRIX_RETRY_LIMIT=10
ENV
chmod 600 "$ENV_FILE"

install -m 0644 "$PROJECT_DIR/deploy/systemd/garmetix-dotmatrix-bridge.service" /etc/systemd/system/garmetix-dotmatrix-bridge.service
systemctl daemon-reload
systemctl enable --now garmetix-dotmatrix-bridge.service

echo ""
echo "Garmetix DotMatrix Bridge installed."
echo "Default printer queue: $PRINTER_NAME"
echo "Check status: sudo systemctl status garmetix-dotmatrix-bridge.service --no-pager"
echo "View logs:    sudo journalctl -u garmetix-dotmatrix-bridge.service -f"
echo "Health check: sudo ./deploy/check-dotmatrix-bridge-status.sh"
echo "CUPS list:    lpstat -p -d"
echo "Test CUPS:    printf 'GARMETIX LX810 TEST\\n' | lp -d $PRINTER_NAME -o raw"
