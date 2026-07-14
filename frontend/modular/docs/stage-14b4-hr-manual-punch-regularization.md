# Stage 14B.4 HR Manual Punch And Regularization

Version: `6.0.12`

## Scope

This stage promotes the next HR attendance workflow in the modular app.

- Added `/attendance/manual-punch` for audited manual attendance punch entry.
- Manual Punch loads employees from `/api/employees`, fills company/store scope from the selected employee, and posts to `/api/attendance/manual-punch`.
- Regularization now supports creating a correction request from the modular HR app.
- Regularization keeps manager approve/reject actions explicit and remark based.
- Attendance landing and route registry now expose Manual Punch and Regularization.
- Added a dry readiness script for backend DTOs, endpoints, route ownership and page wiring.

## Backend Contract Verified

- `POST /api/attendance/manual-punch`
- `GET /api/attendance/regularization`
- `POST /api/attendance/regularization`
- `POST /api/attendance/regularization/{id}/approve`
- `POST /api/attendance/regularization/{id}/reject`

## Validation

Run from the repository root:

```bash
npm run modular:hr:manual-punch-regularization-readiness
npm run modular:hr:parity-baseline
npm run modular:check
npm --prefix frontend/modular run build:hr
```

## Deployment And Data Safety

This stage adds live-write UI for attendance punch and correction request entry. It does not change the database schema.

Because this is the third modular checkpoint after the last `.127` deployment cadence, deploy the modular build to SRP after local validation passes.

## Next

Stage 14B.5 should harden attendance recalculation, lock and delete actions behind explicit live-write gates, then add optional live evidence after backup/cadence checks.
