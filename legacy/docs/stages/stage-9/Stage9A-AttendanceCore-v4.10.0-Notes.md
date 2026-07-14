# Stage 9A - Attendance Core v4.10.0

Version: `4.10.0`  
Stage: `Stage 9A Attendance Core`  
Release: `Attendance Core and Kiosk Planning`  
Build code: `GARMETIX-9A-20260618-4100`

## Implemented now

- Attendance Core domain models: device, punch, shift, policy, biometric enrollment placeholder, regularization, approval and monthly summary.
- Backend APIs under `/api/attendance` for today, monthly, history, manual punch, recalculate, lock month, shifts, policies, devices, regularization and payroll summary.
- Kiosk API base under `/api/attendance/kiosk` for bootstrap, employee lookup, punch and sync-pending.
- Registered kiosk device model with device code/token validation and heartbeat.
- Duplicate punch prevention through attendance policy duplicate window, default 5 minutes.
- Face support is placeholder/photo-proof path only. Real face recognition is not included.
- Fingerprint support is enrollment/bridge placeholder only. Raw fingerprint images are not stored.
- Payroll support is summary/review only. Automatic deduction or salary posting is not included.

## Migration

`20260619093000_AddAttendanceCoreStage9A`

Startup schema drift repair also creates missing Attendance Core tables when older Docker volumes are upgraded with migrations disabled or stale migration history.

## Later list

1. Android/.NET MAUI kiosk app with local SQLite offline queue.
2. Face photo proof UI and manager mismatch review.
3. Face recognition and liveness check.
4. Fingerprint device bridge using selected vendor SDK/API.
5. Attendance payroll deduction and payroll lock integration.
