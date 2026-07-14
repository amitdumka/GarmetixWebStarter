#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

BACKUP_SCRIPT="${ROOT_DIR}/scripts/linux/create-database-backup-now.sh"
MAINT_SCRIPT="${ROOT_DIR}/scripts/linux/backup-maintenance-check.sh"

if [[ -x "${BACKUP_SCRIPT}" ]]; then
  echo "== Creating a fresh backup =="
  "${BACKUP_SCRIPT}" "${ENV_FILE}"
fi

echo "== Backup maintenance status =="
if [[ -x "${MAINT_SCRIPT}" ]]; then
  "${MAINT_SCRIPT}" "${ENV_FILE}"
fi

echo "== Restore drill guidance =="
echo "1. Copy the chosen .dump file to a disposable environment."
echo "2. Run scripts/linux/restore-db.sh <path-to-dump>."
echo "3. Log in and verify health before using the environment."
