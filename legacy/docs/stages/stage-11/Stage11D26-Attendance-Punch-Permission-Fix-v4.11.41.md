# Stage 11D-26 Attendance Punch Permission Fix - v4.11.41

## Problem

Store Manager users could open the attendance page but received HTTP 403 when posting to `/api/attendance/manual-punch`.

The route was protected by both:

- Attendance module policy
- Global Edit policy

Store Manager has Attendance module access, but does not have global Edit permission. This made manual punch work for Admin but fail for Store Manager.

## Fix

`POST /api/attendance/manual-punch` now requires only Attendance module access.

This allows operational attendance entry for:

- Admin / Owner / SuperAdmin
- PowerUser
- StoreManager
- HR
- Payroll

Global Edit permission is still required for setup/correction/admin endpoints such as:

- shifts create/update/delete
- shift rules create/update/delete
- policies create/update
- device bridge simulator/admin endpoints
- photo proof review/regularization approval
- payroll review rebuild/generate actions

## Files changed

- `backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs`
- version metadata files

## Test

1. Login as Store Manager.
2. Open Attendance.
3. Manual punch an employee.
4. Expected: HTTP 200, no 403.
5. Login as Admin and verify shift setup pages still work.
