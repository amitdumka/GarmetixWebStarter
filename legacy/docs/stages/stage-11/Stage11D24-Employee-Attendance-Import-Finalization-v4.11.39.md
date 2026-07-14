# Stage 11D-24 Aadwika Smart Menswear Employee + Attendance Import Finalization

This import replaces the old daily-only attendance import with a shift-aware import that connects:

- Employees
- AttendanceShifts
- EmployeeAttendanceShiftRules
- AttendancePunches
- Daily Attendance rows

## Business rules implemented

- Male/default store timing: 09:00 to 21:00 with lunch break 13:00 to 14:30.
- Female timing: 10:00 to 20:00, no break punch required.
- Accounts timing: 10:00 to 19:00, no break punch required.
- Housekeeping double shift: one completed session = HalfDay, two sessions = FullDay.
- Present rows generate punches.
- HalfDay rows generate only one session.
- Absent rows generate no punch.
- India time is stored in LocalPunchTime; UTC audit time is LocalPunchTime minus 05:30.

## Files

- `data/employees_shift_final.csv` - employees with gender and shift code.
- `data/attendance_status_final.csv` - daily status rows.
- `data/shift_templates.csv` - shift timing rules.
- `sql/aadwika-smart-attendance-shift-import-v2.sql` - import SQL.
- `import-aadwika-smart-attendance-shift-v2.sh` - run import with pre-backup.
- `restore-before-aadwika-attendance-shift-v2.sh` - restore backup if needed.
- `validate-aadwika-attendance-shift-v2.sh` - post-import validation.

## Run

```bash
cd /opt/garmetix/current
chmod +x scripts/imports/aadwika-attendance-v2/*.sh
./scripts/imports/aadwika-attendance-v2/import-aadwika-smart-attendance-shift-v2.sh
./scripts/imports/aadwika-attendance-v2/validate-aadwika-attendance-shift-v2.sh
```

## Counts

- Employees: 10
- Attendance status rows: 2616

## Restore

```bash
cd /opt/garmetix/current
./scripts/imports/aadwika-attendance-v2/restore-before-aadwika-attendance-shift-v2.sh
```

## Project stage

Version: 4.11.39
Stage: Stage 11D-24 Employee Attendance Import Finalization
Build: GARMETIX-11D24-20260625-4139
