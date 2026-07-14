#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
CRON_FILE="/etc/cron.d/garmetix-health-watchdog"
SCRIPT="$ROOT/scripts/production/stage11d34-health-watchdog.sh"
if [ ! -f "$SCRIPT" ]; then echo "Missing $SCRIPT" >&2; exit 1; fi
sudo tee "$CRON_FILE" >/dev/null <<EOF
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
# Report-only every 15 minutes. Change 'report' to '--self-heal' when ready.
*/15 * * * * root cd $ROOT && $SCRIPT $ROOT report >> $ROOT/reports/stage11d34-health-watchdog/cron.log 2>&1
EOF
sudo chmod 644 "$CRON_FILE"
echo "Installed health watchdog cron: $CRON_FILE"
