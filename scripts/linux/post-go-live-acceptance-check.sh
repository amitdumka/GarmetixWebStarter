#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

if [[ -f "${ROOT_DIR}/deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "${ROOT_DIR}/deploy/lib/env-file.sh"
  normalize_text_file "${ENV_FILE}" || true
  export_env_file_safe "${ENV_FILE}" || true
fi

API_PORT="${API_PORT:-5080}"
WEB_PORT="${WEB_PORT:-3000}"

echo "== Stage 8H Package 1 post-go-live acceptance check =="

check_url() {
  local title="$1"
  local url="$2"
  echo "--- ${title}: ${url}"
  curl -fsS "${url}" >/tmp/garmetix-acceptance-response.json
  python3 - <<'PY'
from pathlib import Path
import json
p = Path('/tmp/garmetix-acceptance-response.json')
text = p.read_text()
try:
    data = json.loads(text)
    print(json.dumps(data, indent=2)[:1200])
except Exception:
    print(text[:1200])
PY
  echo
}

check_url "API health" "http://127.0.0.1:${API_PORT}/api/health"
check_url "App info" "http://127.0.0.1:${API_PORT}/api/app-info"
check_url "Production readiness" "http://127.0.0.1:${API_PORT}/api/production-readiness/summary"
check_url "Smoke manifest" "http://127.0.0.1:${API_PORT}/api/test-automation/manifest"
check_url "Web health proxy" "http://127.0.0.1:${WEB_PORT}/api/health"

echo "== Manual role acceptance reminders =="
cat <<'EOF'
[ ] Admin/Owner can see Legacy Overview; normal/store users cannot.
[ ] Salary Structures is hidden from normal/store users.
[ ] Payslip and Salary Payment are visible to Store Manager / Accountant / Power User as intended.
[ ] HR Attendance page shows Add/New Attendance for authorized users.
[ ] Forgot password gives clear SMTP-not-configured feedback when SMTP is disabled.
[ ] Purchase New Inward opens as a full page at /purchase/new.
[ ] Vendor Payments supports invoice-linked payment and advance payment list.
EOF

echo "Post-go-live acceptance script completed."
