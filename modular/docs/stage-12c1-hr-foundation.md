# Stage 12C.1 HR Modular Foundation

Version: 5.12.9

## What Changed

- Added the first runnable HR modular shell inside `modular/apps/hr`.
- Added a global HR route guard and login page that reuse shared auth storage and the existing API login endpoint.
- Added read-only HR pages for:
  - HR dashboard at `/`
  - Employee summary at `/hr`
  - Attendance landing at `/attendance`
  - Today attendance at `/attendance/today`
  - Monthly attendance at `/attendance/monthly`
  - Payroll summary at `/attendance/payroll-summary`
  - Payslips at `/payroll`
  - Salary payments at `/attendance/salary-payment`
  - Kiosk devices at `/attendance/devices`
- Added placeholder coverage for the remaining HR attendance routes so route ownership does not produce broken pages.

## API Use

The first HR slice is read-only and calls existing API endpoints:

- `GET /api/hr/employee-master/summary`
- `GET /api/attendance/today`
- `GET /api/attendance/monthly`
- `GET /api/attendance/payroll-summary`
- `GET /api/payroll/payslips/recent`
- `GET /api/salary-payments`
- `GET /api/attendance/devices`

## Safety Notes

- No database changes.
- No backend changes.
- No attendance, payroll, salary payment, or device write actions are enabled in this stage.
- Remaining HR routes show explicit next-slice placeholders instead of 404 pages.

## How To Test

From the repository root:

```powershell
npm run modular:check
npm --prefix modular run build:hr
```

For local browser testing:

```powershell
npm --prefix modular --workspace @garmetix/hr-web run dev
```

Then open `http://localhost:3102/`.

## Next Step

Stage 12C.2 should connect the first safe HR write flows:

- Attendance manual punch or regularization draft with approval guard.
- Payroll draft rebuild and mark-ready controls.
- Salary payment preview only, before generation.
- HR static deployment script after the HR app is smoke-tested.
