# Stage 12C.2 HR Safe Actions

Version: 5.12.10

## What Changed

- Connected `/attendance/payroll-review` in the HR modular app.
  - Load review rows.
  - Rebuild review rows from monthly attendance.
  - Mark rows `Reviewed`, `ApprovedForPayroll`, or `OnHold` with notes.
- Connected `/attendance/salary-draft`.
  - Load salary draft previews.
  - Rebuild drafts from reviewed attendance payroll rows.
  - Mark rows `ReadyForPayroll` or `OnHold` with notes.
- Connected `/attendance/regularization`.
  - Load regularization requests.
  - Approve or reject pending rows with manager remarks.
- Enhanced `/attendance/salary-payment`.
  - Load salary payment candidates.
  - Preview salary payment amounts through the existing API.
  - Show advance, previous due, outstanding, rounded payable, round off, and already paid values.
- Added these routes to the HR sidebar and modular structure validation.

## Safety Notes

- No backend changes.
- No database schema changes.
- No final salary payment vouchers are generated from the modular HR app.
- No final salary slips are generated from the modular HR app.
- Salary payment preview calls `POST /api/salary-payments/preview` only.

## API Calls

- `GET /api/attendance/payroll-review`
- `POST /api/attendance/payroll-review/rebuild`
- `POST /api/attendance/payroll-review/{id}/mark-reviewed`
- `GET /api/attendance/salary-slip-drafts`
- `POST /api/attendance/salary-slip-drafts/rebuild`
- `POST /api/attendance/salary-slip-drafts/{id}/mark-ready`
- `GET /api/attendance/regularization`
- `POST /api/attendance/regularization/{id}/approve`
- `POST /api/attendance/regularization/{id}/reject`
- `GET /api/attendance/salary-payment-candidates`
- `POST /api/salary-payments/preview`

## How To Test

```powershell
npm run modular:check
npm --prefix modular run build:hr
```

Then run the HR app:

```powershell
npm --prefix modular --workspace @garmetix/hr-web run dev
```

Open `http://localhost:3102/`, login, and check:

- `/attendance/payroll-review`
- `/attendance/salary-draft`
- `/attendance/regularization`
- `/attendance/salary-payment`

## Next Step

Stage 12C.3 should add the HR static deploy script and deployment notes for `hr.garmetix.aadwikafashion.in`, following the existing POS static deploy pattern.
