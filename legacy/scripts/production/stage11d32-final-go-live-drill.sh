#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
REPORT_DIR="$ROOT/reports/stage11d32-final-go-live-drill"
mkdir -p "$REPORT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT="$REPORT_DIR/stage11d32-final-go-live-drill-$STAMP.txt"
exec > >(tee "$REPORT") 2>&1

echo "Stage 11D-32 Final Go-Live Drill"
echo "Time: $(date -Is)"
echo

echo "1) Docker container status"
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' || true
echo

echo "2) Compose services"
COMPOSE_PROJECT_NAME=garmetix docker compose --env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps || true
echo

echo "3) API health"
curl -fsS http://127.0.0.1:5000/health || curl -fsS http://127.0.0.1:8080/health || true
echo

echo "4) App info"
curl -fsS http://127.0.0.1:5000/api/app-info || curl -fsS http://127.0.0.1:8080/api/app-info || true
echo

echo "5) Run Stage 11D-30 import/GST validation"
if [ -x scripts/production/stage11d30-import-validation-and-gst.sh ]; then
  scripts/production/stage11d30-import-validation-and-gst.sh || true
else
  echo "Stage 11D-30 script missing"
fi
echo

echo "6) Run Stage 11D-31 payroll readiness"
if [ -x scripts/production/stage11d31-payroll-readiness.sh ]; then
  scripts/production/stage11d31-payroll-readiness.sh || true
else
  echo "Stage 11D-31 script missing"
fi
echo

echo "7) Audit scripts presence"
test -x scripts/production/install-attendance-correction-audit.sh && echo "Attendance audit installer present"
test -x scripts/production/install-invoice-correction-audit.sh && echo "Invoice audit installer present"
echo

echo "Final Go-Live manual UI checklist:"
cat <<'EOF'
[ ] Login as Admin
[ ] Login as Store Manager and punch attendance
[ ] Purchase -> Vendors opens
[ ] Inventory -> Brands/Categories/Sub-categories opens
[ ] Purchase -> Purchase Inward opens and PDF A4/A5 works
[ ] Billing -> New Sale creates StoreCode/YYYYMM/INV/series
[ ] Billing -> Sales Invoice PDF A4/A5 works
[ ] Attendance -> Monthly Attendance shows current month
[ ] Payroll -> Generate/review salary for one employee
[ ] Backup restore script tested on staging or backup copy
EOF

echo
echo "Report saved to $REPORT"
