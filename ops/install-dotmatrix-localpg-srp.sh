#!/usr/bin/env bash
set -Eeuo pipefail

PRINTER_NAME="${GARMETIX_DOTMATRIX_PRINTER:-EPSON_LX310}"
DEVICE_URI="${GARMETIX_DOTMATRIX_DEVICE_URI:-}"
SCRIPT_SRC="${GARMETIX_DOTMATRIX_SCRIPT_SRC:-/tmp/garmetix-dotmatrix-bridge-localpg.sh}"
INSTALL_DIR="/opt/garmetix-srp/shared/dotmatrix"
ENV_FILE="/etc/garmetix-dotmatrix.env"
API_ENV="/etc/garmetix/srp-api.env"
SERVICE_FILE="/etc/systemd/system/garmetix-dotmatrix-bridge.service"

if [[ $EUID -ne 0 ]]; then
  echo "Run this installer with sudo/root." >&2
  exit 1
fi

apt-get update
apt-get install -y cups cups-client postgresql-client coreutils util-linux
systemctl enable --now cups

if [[ ! -f "$SCRIPT_SRC" ]]; then
  echo "Bridge script missing: $SCRIPT_SRC" >&2
  exit 2
fi

mkdir -p "$INSTALL_DIR"
install -m 0755 "$SCRIPT_SRC" "$INSTALL_DIR/garmetix-dotmatrix-bridge-localpg.sh"

if [[ ! -f "$API_ENV" ]]; then
  echo "API env file missing: $API_ENV" >&2
  exit 2
fi

CONNECTION_STRING="$(grep -E '^ConnectionStrings__Default=' "$API_ENV" | tail -1 | cut -d= -f2-)"
cs_get() {
  printf '%s' "$CONNECTION_STRING" | tr ';' '\n' | grep -E "^$1=" | tail -1 | cut -d= -f2-
}

DB_HOST="$(cs_get Host)"
DB_PORT="$(cs_get Port)"
DB_NAME="$(cs_get Database)"
DB_USER="$(cs_get Username)"
DB_PASSWORD="$(cs_get Password)"

DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-garmetix_srp}"
DB_USER="${DB_USER:-garmetix}"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "Could not read DB password from $API_ENV." >&2
  exit 2
fi

dotenv_quote() {
  printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

if lpstat -p "$PRINTER_NAME" >/dev/null 2>&1; then
  echo "CUPS printer queue already exists: $PRINTER_NAME"
else
  if [[ -z "$DEVICE_URI" ]]; then
    DEVICE_URI="$(lpinfo -v 2>/dev/null | awk '/usb:\/\/EPSON\/LX-310/ { print $2; exit }')"
  fi

  if [[ -n "$DEVICE_URI" ]]; then
    echo "Creating CUPS raw queue $PRINTER_NAME at $DEVICE_URI"
    lpadmin -p "$PRINTER_NAME" -E -v "$DEVICE_URI" -m raw
  else
    echo "No Epson LX-310 USB device URI found. The bridge will run, but CUPS queue must be added later." >&2
    lpinfo -v || true
  fi
fi

if lpstat -p "$PRINTER_NAME" >/dev/null 2>&1; then
  cupsenable "$PRINTER_NAME" || true
  cupsaccept "$PRINTER_NAME" || true
  lpoptions -d "$PRINTER_NAME" || true
fi

{
  echo "# Garmetix DotMatrix Bridge configuration for SRP local PostgreSQL deployment"
  printf 'GARMETIX_DOTMATRIX_DB_HOST=%s\n' "$(dotenv_quote "$DB_HOST")"
  printf 'GARMETIX_DOTMATRIX_DB_PORT=%s\n' "$(dotenv_quote "$DB_PORT")"
  printf 'GARMETIX_DOTMATRIX_DB_NAME=%s\n' "$(dotenv_quote "$DB_NAME")"
  printf 'GARMETIX_DOTMATRIX_DB_USER=%s\n' "$(dotenv_quote "$DB_USER")"
  printf 'GARMETIX_DOTMATRIX_DB_PASSWORD=%s\n' "$(dotenv_quote "$DB_PASSWORD")"
  printf 'GARMETIX_DOTMATRIX_PRINTER=%s\n' "$(dotenv_quote "$PRINTER_NAME")"
  echo "GARMETIX_DOTMATRIX_LP_OPTIONS=-o raw"
  echo "GARMETIX_DOTMATRIX_DEFAULT_ENABLED=false"
  echo "GARMETIX_DOTMATRIX_DEFAULT_OUTPUT_MODE=BridgeService"
  echo "GARMETIX_DOTMATRIX_POLL_SECONDS=2"
  echo "GARMETIX_DOTMATRIX_BATCH_SIZE=20"
  echo "GARMETIX_DOTMATRIX_RETRY_LIMIT=10"
  echo "GARMETIX_DOTMATRIX_NORMALIZE_CRLF=true"
  echo "GARMETIX_DOTMATRIX_RESET_BEFORE_JOB=true"
} >"$ENV_FILE"
chmod 600 "$ENV_FILE"

cat >"$SERVICE_FILE" <<UNIT
[Unit]
Description=Garmetix DotMatrix Bridge - Epson LX-310 SRP Local PostgreSQL
After=postgresql.service cups.service network-online.target
Wants=postgresql.service cups.service network-online.target

[Service]
Type=simple
EnvironmentFile=-$ENV_FILE
ExecStart=$INSTALL_DIR/garmetix-dotmatrix-bridge-localpg.sh
Restart=always
RestartSec=5
User=root
KillSignal=SIGINT
TimeoutStopSec=20

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable --now garmetix-dotmatrix-bridge.service

echo "Garmetix DotMatrix Bridge installed."
echo "Printer queue: $PRINTER_NAME"
echo "Service: garmetix-dotmatrix-bridge.service"
