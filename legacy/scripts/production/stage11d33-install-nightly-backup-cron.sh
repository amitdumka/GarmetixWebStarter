#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
CRON_FILE="/etc/cron.d/garmetix-nightly-backup"
SCRIPT="$ROOT/scripts/production/stage11d33-backup-now.sh"
if [ ! -f "$SCRIPT" ]; then echo "Missing $SCRIPT" >&2; exit 1; fi
sudo tee "$CRON_FILE" >/dev/null <<EOF
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
# Garmetix nightly backup at 02:15 India time. Server may be UTC; use system cron time.
15 2 * * * root cd $ROOT && $SCRIPT $ROOT >> $ROOT/reports/stage11d33-backup/cron.log 2>&1
EOF
sudo chmod 644 "$CRON_FILE"
echo "Installed nightly backup cron: $CRON_FILE"
