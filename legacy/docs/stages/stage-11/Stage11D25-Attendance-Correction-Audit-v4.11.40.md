# Stage 11D-25 Attendance Correction Audit - v4.11.40

This stage adds a database-level attendance correction audit trail for production payroll safety.

## Why

Admin/Owner can correct CheckIn, BreakOut, BreakIn and CheckOut times. Those corrections must be traceable before payroll is finalized.

## What is added

- `AttendanceCorrectionAudits` table
- trigger on `Attendance` updates
- trigger on `AttendancePunches` updates
- installer script with pre-install backup
- report script for recent corrections

## Install

```bash
cd /opt/garmetix/current
chmod +x scripts/production/*.sh
./scripts/production/install-attendance-correction-audit.sh
```

## Report

```bash
./scripts/production/attendance-correction-audit-report.sh
```

## Audit fields

The audit stores:

- employee
- date
- audit source: DailyAttendance or AttendancePunch
- action
- changed fields
- old values JSON
- new values JSON
- database user
- reason/remarks
- UTC change time

## Note

This is database-level safety. It captures changes even if the change happens from app, import script, or direct SQL. A later UI page can expose this audit table inside the Attendance menu.
