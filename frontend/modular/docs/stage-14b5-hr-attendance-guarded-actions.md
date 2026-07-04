# Stage 14B.5 HR Attendance Guarded Actions

Version: `6.0.13`

## Scope

This stage promotes monthly attendance write actions into the modular HR app with explicit safety gates.

- Monthly Attendance can now recalculate monthly summaries through `/api/attendance/recalculate`.
- Monthly Attendance can lock or unlock the selected month through `/api/attendance/lock-month`.
- Monthly Attendance can delete selected employee/date rows through `/api/attendance/monthly/delete-selected`.
- All live actions require the exact confirmation phrase shown on screen.
- Delete selected rows additionally requires at least one selected row, an audit reason, and at least one delete scope.
- Selected-row delete keeps the backend lock protection intact.

## Guardrails

- Confirmation phrase: `CONFIRM MM/YYYY`.
- Delete requires a reason.
- Delete can target daily attendance rows and optionally punches/photo proofs.
- After any successful live action, the confirmation phrase and row selections are cleared and the month is reloaded.

## Validation

Run from the repository root:

```bash
npm run modular:hr:attendance-guarded-actions-readiness
npm run modular:hr:attendance-monthly-readiness
npm run modular:check
npm --prefix frontend/modular run build:hr
```

## Deployment And Data Safety

No database schema change is included. No database backup is required for local validation.

This is checkpoint 1 after the last `.127` deployment, so deployment is deferred by cadence.

## Next

Stage 14B.6 should continue HR parity with attendance device and kiosk readiness, then payroll approval evidence can be tightened after the Mantra device arrives.
