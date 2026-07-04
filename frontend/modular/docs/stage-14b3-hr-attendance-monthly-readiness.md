# Stage 14B.3 HR Attendance Monthly Readiness

Version: `6.0.11`

## Scope

This stage improves modular HR attendance review without writing attendance data.

- Today Attendance now shows half-day totals, working minutes, overtime, shift, attendance mode, session progress and review flags.
- Monthly Attendance now shows generated day rows instead of only raw JSON.
- Monthly Attendance shows locked-month state and preview-only recalculation payload details.
- The monthly page references the existing backend generation endpoints but does not execute POST actions in this stage.
- A dry readiness script checks backend DTOs, endpoint mapping, UI tokens and non-mutating page behavior.

## Backend Contract Verified

- `GET /api/attendance/today`
- `GET /api/attendance/monthly`
- `POST /api/attendance/recalculate`
- `POST /api/attendance/lock-month`

The backend recalculation endpoint saves monthly summaries and skips locked rows. The modular HR page remains preview-only until a later guarded writable stage promotes the action.

## Validation

Run from the repository root:

```bash
npm run modular:hr:attendance-monthly-readiness
npm run modular:hr:attendance-contract
npm run modular:check
npm --prefix frontend/modular run build:hr
```

## Deployment And Data Safety

No database schema or live data mutation is introduced here. No `.127` deployment is required by cadence for this checkpoint.

## Next

Stage 14B.4 should promote the next HR attendance workflow safely:

- manual punch/regularization review parity,
- delete/recalculate actions behind explicit live-write guards,
- attendance device handoff once the Mantra device is available,
- optional live evidence only after the deployment cadence and backup rule require it.
