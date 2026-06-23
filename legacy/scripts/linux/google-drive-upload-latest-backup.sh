#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

./scripts/linux/google-drive-backup-check.sh "${ENV_FILE}"

echo "== Create local backup before cloud upload =="
./scripts/linux/create-database-backup-now.sh "${ENV_FILE}"

echo "Open Maintenance > Backup Maintenance, verify the newest backup, then use Upload local backup."
echo "API upload requires an authenticated admin session by design."
