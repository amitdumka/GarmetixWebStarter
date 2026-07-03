# Attendance Punch Daily Sync v4.11.21

This update connects the Attendance Core punch register with the HR daily Attendance table.

## Changes

- Manual punch CheckIn creates or updates the daily Attendance row.
- Manual punch CheckOut updates the same daily Attendance row checkout time.
- Kiosk punch and offline sync use the same service, so kiosk CheckIn/CheckOut also sync daily Attendance.
- HR daily Attendance create/edit creates or updates matching AttendancePunch rows for CheckIn and CheckOut.
- Attendance employee dropdowns show only active/working employees.
- Payroll/benefit employee dropdowns also prefer active/working employees for new entries.

## Notes

The sync is non-destructive. If an old punch exists for a date, editing the daily attendance row updates the matching CheckIn/CheckOut punch instead of creating a duplicate.
