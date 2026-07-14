# Stage 11D-38: Monthly Attendance Bulk Delete (v4.11.53)

Adds multi-select delete on Attendance → Monthly Attendance.

## UI

- Select individual attendance day cards.
- Select/unselect current page.
- Add delete reason.
- Delete selected rows in one action.

## Backend

New endpoint:

```http
POST /api/attendance/monthly/delete-selected
```

Payload:

```json
{
  "items": [
    { "employeeId": "...", "onDate": "2026-06-25" }
  ],
  "deletePunches": true,
  "deleteDailyAttendance": true,
  "reason": "Wrong imported attendance"
}
```

## Safety

- Requires Attendance + Edit policy.
- Also restricted to Admin/Owner/PowerUser using the same setup-management gate.
- Refuses delete in locked attendance/payroll months.
- Deletes only selected employee/date rows inside the current workspace.
- Soft-deletes daily Attendance rows, AttendancePunches and matching photo proofs.
- Soft-deletes related monthly summaries so they can be recalculated.
