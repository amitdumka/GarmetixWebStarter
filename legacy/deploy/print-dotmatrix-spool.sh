#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${GARMETIX_DOTMATRIX_ENV:-/etc/garmetix-dotmatrix.env}"
if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$ENV_FILE"
fi

SPOOL_DIR="${GARMETIX_DOTMATRIX_SPOOL_DIR:-/opt/garmetix/current/data/dotmatrix-spool}"
PRINTER_NAME="${GARMETIX_DOTMATRIX_PRINTER:-}"
LP_OPTIONS="${GARMETIX_DOTMATRIX_LP_OPTIONS:--o raw}"
PRINTED_DIR="$SPOOL_DIR/printed"
FAILED_DIR="$SPOOL_DIR/failed"

mkdir -p "$SPOOL_DIR" "$PRINTED_DIR" "$FAILED_DIR"

if [[ -z "$PRINTER_NAME" ]]; then
  echo "GARMETIX_DOTMATRIX_PRINTER is empty. Set it in $ENV_FILE after checking lpstat -p -d." >&2
  exit 2
fi

shopt -s nullglob
for file in "$SPOOL_DIR"/*.txt; do
  base="$(basename "$file")"
  if lp -d "$PRINTER_NAME" $LP_OPTIONS "$file"; then
    mv -f "$file" "$PRINTED_DIR/$base"
    echo "Printed $base to $PRINTER_NAME"
  else
    mv -f "$file" "$FAILED_DIR/$base"
    echo "Failed $base to $PRINTER_NAME" >&2
  fi
done
