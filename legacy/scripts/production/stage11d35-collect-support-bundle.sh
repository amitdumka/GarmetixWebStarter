#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
OUT_DIR="$ROOT/reports/stage11d35-support-bundles"
mkdir -p "$OUT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
WORK="$OUT_DIR/support-bundle-$STAMP"
mkdir -p "$WORK"
redact_env(){ sed -E 's/(PASSWORD|TOKEN|SECRET|KEY|CONNECTIONSTRING|JWT)[^=]*=.*/\1=***REDACTED***/Ig'; }
{
  echo "Garmetix support bundle"
  echo "Created: $(date -Is)"
  echo "Host: $(hostname)"
  echo "Kernel: $(uname -a)"
} > "$WORK/README.txt"
cp frontend/garmetix-web/utils/appVersion.ts "$WORK/appVersion.ts" 2>/dev/null || true
cp backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs "$WORK/AppInfoEndpoints.cs" 2>/dev/null || true
if [ -f .env.production ]; then redact_env < .env.production > "$WORK/env.production.redacted"; fi
docker ps -a > "$WORK/docker-ps.txt" 2>&1 || true
COMPOSE_PROJECT_NAME=garmetix docker compose --env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps > "$WORK/docker-compose-ps.txt" 2>&1 || true
df -h > "$WORK/df-h.txt" 2>&1 || true
free -m > "$WORK/free-m.txt" 2>&1 || true
docker logs --tail=500 garmetix-api-1 > "$WORK/api.log" 2>&1 || true
docker logs --tail=300 garmetix-web-1 > "$WORK/web.log" 2>&1 || true
docker logs --tail=200 garmetix-postgres-1 > "$WORK/postgres.log" 2>&1 || true
docker logs --tail=200 garmetix-cloudflared-1 > "$WORK/cloudflared.log" 2>&1 || true
find reports -maxdepth 3 -type f -name '*.txt' -o -name '*.log' 2>/dev/null | tail -100 | while read -r f; do mkdir -p "$WORK/reports/$(dirname "$f")"; cp "$f" "$WORK/reports/$f" 2>/dev/null || true; done
ZIP="$OUT_DIR/support-bundle-$STAMP.zip"
( cd "$OUT_DIR" && zip -qr "$ZIP" "support-bundle-$STAMP" )
echo "Support bundle created: $ZIP"
