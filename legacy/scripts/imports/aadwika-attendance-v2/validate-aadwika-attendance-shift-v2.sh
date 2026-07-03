#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
REPORT_DIR="$ROOT/reports/attendance-final-import-v2"
mkdir -p "$REPORT_DIR"
REPORT="$REPORT_DIR/attendance-final-import-v2-$(date +%Y%m%d-%H%M%S).txt"
docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -X -P pager=off > "$REPORT" <<'SQL'
\echo 'Stage 11D-24 attendance import validation'
SELECT e."EmployeeCode", e."FirstName" || ' ' || e."LastName" AS employee, e."Gender", e."Department", e."Designation", e."Working", e."EmployeeStatus", sh."Name" AS shift
FROM "Employees" e
LEFT JOIN "EmployeeAttendanceShiftRules" r ON r."EmployeeId" = e."Id" AND NOT r."Deleted" AND r."Active"
LEFT JOIN "AttendanceShifts" sh ON sh."Id" = r."AttendanceShiftId"
WHERE e."CompanyId" IN (SELECT "Id" FROM "Companies" WHERE "Name" ILIKE '%Aadwika%Fashion%')
  AND e."StoreId" IN (SELECT "Id" FROM "Stores" WHERE "Name" ILIKE '%Smart%Menswear%')
  AND NOT e."Deleted"
ORDER BY e."EmployeeCode";
\echo 'Monthly counts'
SELECT date_trunc('month', a."OnDate")::date AS month, a."Status", count(*)
FROM "Attendance" a
WHERE a."CompanyId" IN (SELECT "Id" FROM "Companies" WHERE "Name" ILIKE '%Aadwika%Fashion%')
  AND a."StoreId" IN (SELECT "Id" FROM "Stores" WHERE "Name" ILIKE '%Smart%Menswear%')
  AND NOT a."Deleted"
GROUP BY 1,2 ORDER BY 1,2;
\echo 'Punch count by type'
SELECT "PunchType", count(*) FROM "AttendancePunches" p
WHERE p."CompanyId" IN (SELECT "Id" FROM "Companies" WHERE "Name" ILIKE '%Aadwika%Fashion%')
  AND p."StoreId" IN (SELECT "Id" FROM "Stores" WHERE "Name" ILIKE '%Smart%Menswear%')
  AND NOT p."Deleted" AND p."CreatedBy" = 'attendance-final-import-v2'
GROUP BY "PunchType" ORDER BY "PunchType";
\echo 'Potential UTC/local time issues (should be 0)'
SELECT count(*) AS suspicious_rows FROM "AttendancePunches" p
WHERE p."CreatedBy" = 'attendance-final-import-v2'
  AND ABS(EXTRACT(EPOCH FROM ((p."LocalPunchTime" - p."PunchTimeUtc") - interval '5 hours 30 minutes'))) > 60;
SQL
cat "$REPORT"
echo "Report saved: $REPORT"
