#!/usr/bin/env bash
set -Eeuo pipefail

PRINTER_NAME="${GARMETIX_DOTMATRIX_PRINTER:-EPSON_LX310}"
SERVICE_NAME="${GARMETIX_DOTMATRIX_SERVICE:-garmetix-dotmatrix-bridge.service}"
ENV_FILE="${GARMETIX_DOTMATRIX_ENV:-/etc/garmetix-dotmatrix.env}"

echo "Garmetix DotMatrix Bridge Status"
echo "================================="
echo "Env file     : $ENV_FILE"
echo "Printer queue: $PRINTER_NAME"
echo "Service      : $SERVICE_NAME"
echo

if command -v systemctl >/dev/null 2>&1; then
  echo "-- systemd service --"
  systemctl is-enabled "$SERVICE_NAME" 2>/dev/null || true
  systemctl is-active "$SERVICE_NAME" 2>/dev/null || true
  systemctl status "$SERVICE_NAME" --no-pager -n 20 || true
  echo
fi

if command -v lpstat >/dev/null 2>&1; then
  echo "-- CUPS printers --"
  lpstat -p -d || true
  echo
  echo "-- Printer queue detail --"
  lpstat -p "$PRINTER_NAME" -l || true
  echo
else
  echo "lpstat not found. Install cups-client."
fi

if command -v lp >/dev/null 2>&1; then
  echo "-- Raw test command --"
  echo "printf 'GARMETIX DOTMATRIX STATUS TEST\\n' | lp -d '$PRINTER_NAME' -o raw"
fi

echo
if command -v journalctl >/dev/null 2>&1; then
  echo "-- recent bridge logs --"
  journalctl -u "$SERVICE_NAME" -n 80 --no-pager || true
fi
