#!/usr/bin/env bash
set -euo pipefail

MOCK_PORT="${MOCK_PORT:-8788}"
BRIDGE_PORT="${BRIDGE_PORT:-8787}"
UNSAFE_BRIDGE_PORT="${UNSAFE_BRIDGE_PORT:-8789}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MOCK_PROJECT="$REPO_ROOT/apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj"
BRIDGE_PROJECT="$REPO_ROOT/apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj"
MOCK_DLL="$REPO_ROOT/apps/Garmetix.MantraMockService/bin/Release/net10.0/Garmetix.MantraMockService.dll"
BRIDGE_DLL="$REPO_ROOT/apps/Garmetix.FingerprintBridge/bin/Release/net10.0/Garmetix.FingerprintBridge.dll"
LOG_DIR="${TMPDIR:-/tmp}/garmetix-stage11b-mantra-$(date +%s)-$$"
PIDS=()

cleanup() {
  for pid in "${PIDS[@]:-}"; do
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
      wait "$pid" >/dev/null 2>&1 || true
    fi
  done
  echo "Logs: $LOG_DIR"
}
trap cleanup EXIT

wait_json() {
  local url="$1"
  local deadline=$((SECONDS + 60))
  until curl -fsS "$url" >/dev/null 2>&1; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "Timed out waiting for $url" >&2
      exit 1
    fi
    sleep 0.5
  done
}

post_json() {
  local url="$1"
  curl -fsS -H "Content-Type: application/json" -X POST "$url" \
    --data '{"employeeCode":"MGR-REHEARSAL","employeeName":"Manager Rehearsal","rawPayloadAllowed":false}'
}

assert_json() {
  local label="$1"
  local json="$2"
  local script="$3"
  JSON_PAYLOAD="$json" python3 - "$label" "$script" <<'PY'
import json
import os
import sys

label = sys.argv[1]
script = sys.argv[2]
data = json.loads(os.environ["JSON_PAYLOAD"])
if not eval(script, {}, {"data": data}):
    raise SystemExit(f"{label} failed")
PY
}

mkdir -p "$LOG_DIR"
cd "$REPO_ROOT"

echo "Building Mantra mock service and fingerprint bridge..."
dotnet build "$MOCK_PROJECT" -c Release --nologo
dotnet build "$BRIDGE_PROJECT" -c Release --nologo

dotnet "$MOCK_DLL" "--MockMantra:Urls=http://127.0.0.1:$MOCK_PORT" >"$LOG_DIR/mantra-mock.out.log" 2>"$LOG_DIR/mantra-mock.err.log" &
PIDS+=("$!")
wait_json "http://127.0.0.1:$MOCK_PORT/health"
echo "PASS: Mantra mock service health is available."

dotnet "$BRIDGE_DLL" "--Bridge:Urls=http://127.0.0.1:$BRIDGE_PORT" "--Bridge:Adapter=Mantra" "--Bridge:MantraServiceUrl=http://127.0.0.1:$MOCK_PORT/" "--Bridge:MantraEnrollPath=/enroll" >"$LOG_DIR/fingerprint-bridge-safe.out.log" 2>"$LOG_DIR/fingerprint-bridge-safe.err.log" &
PIDS+=("$!")
wait_json "http://127.0.0.1:$BRIDGE_PORT/garmetix-fingerprint/health"
SAFE_ENROLL="$(post_json "http://127.0.0.1:$BRIDGE_PORT/garmetix-fingerprint/enroll")"
assert_json "safe enroll success" "$SAFE_ENROLL" "data.get('success') is True"
assert_json "safe enroll status" "$SAFE_ENROLL" "data.get('matchStatus') == 'Enrolled'"
assert_json "safe enroll raw payload" "$SAFE_ENROLL" "data.get('rawPayloadStored') is False"
assert_json "safe enroll template reference" "$SAFE_ENROLL" "bool(data.get('templateRef'))"
echo "PASS: Safe Mantra enroll returns template reference without raw payload."

kill "${PIDS[-1]}" >/dev/null 2>&1 || true
wait "${PIDS[-1]}" >/dev/null 2>&1 || true

dotnet "$BRIDGE_DLL" "--Bridge:Urls=http://127.0.0.1:$UNSAFE_BRIDGE_PORT" "--Bridge:Adapter=Mantra" "--Bridge:MantraServiceUrl=http://127.0.0.1:$MOCK_PORT/" "--Bridge:MantraEnrollPath=/unsafe/enroll-with-raw" >"$LOG_DIR/fingerprint-bridge-raw-block.out.log" 2>"$LOG_DIR/fingerprint-bridge-raw-block.err.log" &
PIDS+=("$!")
wait_json "http://127.0.0.1:$UNSAFE_BRIDGE_PORT/garmetix-fingerprint/health"
BLOCKED_ENROLL="$(post_json "http://127.0.0.1:$UNSAFE_BRIDGE_PORT/garmetix-fingerprint/enroll")"
assert_json "blocked enroll success" "$BLOCKED_ENROLL" "data.get('success') is False"
assert_json "blocked enroll status" "$BLOCKED_ENROLL" "data.get('matchStatus') == 'RawPayloadBlocked'"
assert_json "blocked enroll raw payload" "$BLOCKED_ENROLL" "data.get('rawPayloadStored') is False"
assert_json "blocked enroll template reference" "$BLOCKED_ENROLL" "not data.get('templateRef')"
echo "PASS: Raw biometric-looking Mantra response is blocked."

echo "Stage 11B Mantra contract rehearsal passed."
