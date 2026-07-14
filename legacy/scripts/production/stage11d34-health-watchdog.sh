#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
MODE="${2:-report}"
cd "$ROOT"
REPORT_DIR="$ROOT/reports/stage11d34-health-watchdog"
mkdir -p "$REPORT_DIR"
REPORT="$REPORT_DIR/health-$(date +%Y%m%d-%H%M%S).txt"
COMPOSE_FILES="--env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml"
API_URL="${API_HEALTH_URL:-http://127.0.0.1:5000/health}"
WEB_URL="${WEB_HEALTH_URL:-http://127.0.0.1:3000}"
status=0
log(){ echo "$*" | tee -a "$REPORT"; }
check(){ local name="$1"; shift; if "$@" >>"$REPORT" 2>&1; then log "PASS $name"; else log "FAIL $name"; status=1; fi; }
log "Garmetix health watchdog: $(date -Is)"
log "Mode: $MODE"
check "docker running" docker info
check "compose services" bash -lc "COMPOSE_PROJECT_NAME=garmetix docker compose $COMPOSE_FILES ps"
check "postgres container" bash -lc "docker inspect -f '{{.State.Running}}' garmetix-postgres-1 | grep true"
check "api container" bash -lc "docker inspect -f '{{.State.Running}}' garmetix-api-1 | grep true"
check "web container" bash -lc "docker inspect -f '{{.State.Running}}' garmetix-web-1 | grep true"
check "api health" curl -fsS --max-time 10 "$API_URL"
check "web response" curl -fsS --max-time 10 "$WEB_URL"
check "disk usage under 90 percent" bash -lc "df -P / | awk 'NR==2 {gsub(/%/,\"\",$5); exit !($5 < 90)}'"
check "memory available" bash -lc "free -m | awk '/Mem:/ {exit !($7 > 200)}'"
log "Last API logs:"
docker logs --tail=80 garmetix-api-1 >>"$REPORT" 2>&1 || true
if [ "$status" -ne 0 ] && [ "$MODE" = "--self-heal" ]; then
  log "Self-heal enabled: restarting unhealthy app containers"
  COMPOSE_PROJECT_NAME=garmetix docker compose $COMPOSE_FILES up -d postgres api web cloudflared >>"$REPORT" 2>&1 || true
  sleep 10
  curl -fsS --max-time 10 "$API_URL" >>"$REPORT" 2>&1 && log "PASS api after self-heal" || log "FAIL api after self-heal"
fi
log "Report: $REPORT"
exit 0
