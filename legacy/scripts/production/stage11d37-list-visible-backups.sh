#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
BACKUP_DIR="$ROOT/backups"
if [ ! -d "$BACKUP_DIR" ]; then
  echo "Backup directory not found: $BACKUP_DIR"
  exit 0
fi
echo "Visible backup files under: $BACKUP_DIR"
find "$BACKUP_DIR" -type f -name "*.dump" ! -name "restore-*.dump" -printf '%TY-%Tm-%Td %TH:%TM  %s bytes  %p\n' 2>/dev/null | sort -r || true
