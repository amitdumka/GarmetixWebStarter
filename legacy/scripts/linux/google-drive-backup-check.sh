#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

if [[ -f "deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "deploy/lib/env-file.sh"
  normalize_text_file "${ENV_FILE}" || true
  export_env_file_safe "${ENV_FILE}" || true
fi

SERVICE_ACCOUNT_PATH="${GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON_PATH:-secrets/google-drive-service-account.json}"

echo "== Google Drive off-site backup readiness =="
echo "Enabled: ${GOOGLE_DRIVE_BACKUP_ENABLED:-false}"
echo "Upload on backup: ${GOOGLE_DRIVE_UPLOAD_ON_BACKUP:-true}"
echo "Folder ID: ${GOOGLE_DRIVE_FOLDER_ID:-<missing>}"
echo "Retention: ${GOOGLE_DRIVE_RETENTION_COUNT:-30}"

if [[ "${GOOGLE_DRIVE_BACKUP_ENABLED:-false}" != "true" ]]; then
  echo "WARN: GOOGLE_DRIVE_BACKUP_ENABLED is not true."
fi

if [[ -z "${GOOGLE_DRIVE_FOLDER_ID:-}" ]]; then
  echo "FAIL: GOOGLE_DRIVE_FOLDER_ID is missing."
  exit 1
fi

if [[ ! -f "${SERVICE_ACCOUNT_PATH}" ]]; then
  echo "FAIL: Google service account JSON file is missing: ${SERVICE_ACCOUNT_PATH}"
  echo "Place the file at secrets/google-drive-service-account.json or set GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON_PATH."
  exit 1
fi

if command -v jq >/dev/null 2>&1; then
  email="$(jq -r '.client_email // empty' "${SERVICE_ACCOUNT_PATH}")"
  project="$(jq -r '.project_id // empty' "${SERVICE_ACCOUNT_PATH}")"
  key="$(jq -r '.private_key // empty' "${SERVICE_ACCOUNT_PATH}")"
  [[ -n "${email}" ]] || { echo "FAIL: service account JSON missing client_email"; exit 1; }
  [[ -n "${key}" ]] || { echo "FAIL: service account JSON missing private_key"; exit 1; }
  echo "Service account: ${email}"
  echo "Project: ${project:-<not-set>}"
else
  echo "WARN: jq not installed; skipping JSON field validation."
fi

echo "PASS: Google Drive configuration files are present."
echo "Next: share the Drive folder with the service account email, then use Maintenance > Backup Maintenance to list/upload cloud backups."
