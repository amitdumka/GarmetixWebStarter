#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"
BASE_URL="${GARMETIX_BASE_URL:-http://localhost:3000}"
API_URL="${GARMETIX_API_URL:-http://localhost:5000}"
USER_NAME="${GARMETIX_SMOKE_USER:-admin}"
PASSWORD="${GARMETIX_SMOKE_PASSWORD:-}"

if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

if [ -z "$PASSWORD" ]; then
  echo "Set GARMETIX_SMOKE_PASSWORD before running runtime diagnostics drill." >&2
  exit 2
fi

login_payload=$(printf '{"userName":"%s","password":"%s"}' "$USER_NAME" "$PASSWORD")
token=$(curl -fsS -X POST "$API_URL/api/auth/login" -H 'Content-Type: application/json' -d "$login_payload" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("token") or "")')
if [ -z "$token" ]; then
  echo "Login did not return a token." >&2
  exit 3
fi

auth_header="Authorization: Bearer $token"

curl -fsS "$API_URL/api/runtime-diagnostics" -H "$auth_header" | python3 -m json.tool >/tmp/garmetix-runtime-diagnostics.json
curl -fsS "$API_URL/api/runtime-diagnostics/page-contracts" -H "$auth_header" | python3 -m json.tool >/tmp/garmetix-runtime-page-contracts.json
curl -fsS "$API_URL/api/runtime-diagnostics/known-runtime-checks" -H "$auth_header" | python3 -m json.tool >/tmp/garmetix-runtime-known-checks.json

python3 - <<'PY'
import json
from pathlib import Path
payload = json.loads(Path('/tmp/garmetix-runtime-diagnostics.json').read_text())
failed = int(payload.get('failedProbeCount') or 0)
print(f"Runtime diagnostics status: {payload.get('status')} | failed probes: {failed}")
if failed:
    for probe in payload.get('probes', []):
        if not probe.get('passed'):
            print(f"FAILED: {probe.get('area')} / {probe.get('title')} - {probe.get('detail')}")
    raise SystemExit(1)
PY

echo "Stage 10H runtime diagnostics drill passed."
