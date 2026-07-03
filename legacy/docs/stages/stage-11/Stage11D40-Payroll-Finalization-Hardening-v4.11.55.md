# Stage 11D-40 Payroll Finalization Hardening — v4.11.55

Base: `v4.11.54 Stage 11D-39 Version Reconciliation Merge`

## Goal

Harden payroll finalization so the operator has a single safe backend action after attendance has been recalculated and checked. The action should avoid the older half-complete UI-only flow where review, draft, payslip, payment and month lock were separate HTTP calls.

## Implemented

### Backend

- Added `PayrollFinalizationService`.
- Added `POST /api/payroll/finalization/finalize-month`.
- The endpoint runs inside one EF execution strategy and database transaction.
- It performs:
  1. monthly attendance summary validation,
  2. payroll review row creation/update,
  3. review approval to `ApprovedForPayroll`,
  4. salary draft creation/update,
  5. full-day / half-day / absent payable-day calculation,
  6. optional overtime earning calculation using request multiplier,
  7. optional late penalty calculation using request amount per late day,
  8. HR payroll adjustment application,
  9. payslip create/update,
  10. optional salary payment voucher creation,
  11. salary payment accounting posting through the existing `AccountingPostingService.PostSalaryPaymentAsync`,
  12. salary advance recovery marking,
  13. optional month lock with payroll metadata in `AttendanceMonthlySummary.SummaryJson`.

### Frontend

- Payroll Finalization page now has a Stage 11D-40 hardened action:
  - `Finalize Payroll Safely`
- The page still keeps the older step-by-step buttons for diagnosis/manual recovery.
- Added controls for:
  - payment mode,
  - overtime multiplier,
  - late penalty per day,
  - post salary payments toggle,
  - lock after finalization toggle,
  - finalization note.
- Added result card showing:
  - employees finalized,
  - payslips created/updated,
  - payments created,
  - locked rows,
  - gross/net/paid totals,
  - backend step log.

## Files changed

- `backend/Garmetix.Api/Payroll/PayrollFinalizationService.cs`
- `backend/Garmetix.Api/Payroll/PayrollEndpoints.cs`
- `backend/Garmetix.Api/Payroll/PayrollDtos.cs`
- `backend/Garmetix.Api/Program.cs`
- `frontend/garmetix-web/pages/payroll/finalization.vue`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `README.md`

## What is intentionally not changed

- No new database table was added in this stage, so existing deployed databases do not need a new migration only for the hardening metadata.
- Existing manual salary payment screens remain available.
- Existing step-by-step attendance payroll review and draft pages remain available.
- Payslip PDF format itself is not changed here; final print acceptance is planned for Stage 11D-42.

## Runtime checklist

1. Recalculate attendance for a test month.
2. Open Payroll Finalization.
3. Set OT multiplier and late penalty if needed.
4. Keep `Post salary payments` enabled for full finalization.
5. Keep `Lock after finalize` enabled for final payroll lock.
6. Click `Finalize Payroll Safely`.
7. Verify:
   - payslips created,
   - salary payment vouchers created,
   - salary accounting journal posted,
   - attendance month locked,
   - advance recovery rows marked recovered where applicable.

## Remaining after Stage 11D-40

- Stage 11D-41: invoice replacement approval, atomic replacement link/audit ledger.
- Stage 11D-42: real PDF print testing and final print evidence for sale/purchase A4/A5, large invoices, amount box, footer, signature and page summary.
