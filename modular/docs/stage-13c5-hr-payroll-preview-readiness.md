# Stage 13C.5 HR Payroll Preview Readiness

Version: 5.13.23
Branch: Version5

## Scope

This stage verifies that modular HR can safely inspect salary payment candidates and optionally call the non-mutating salary payment preview endpoint. It does not create salary payment vouchers.

## Commands

Dry run:

```powershell
npm.cmd run modular:hr:payroll-preview-readiness
```

Live read-only check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:hr:payroll-preview-readiness -- --live --require-token --strict-permissions
```

Optional non-mutating preview with explicit data:

```powershell
npm.cmd run modular:hr:payroll-preview-readiness -- --live --preview --employee-id=<employee-guid> --salary-payslip-id=<payslip-guid> --salary-month=202606
```

Optional non-mutating preview using the first available candidate:

```powershell
npm.cmd run modular:hr:payroll-preview-readiness -- --live --preview --use-first-candidate --salary-month=202606
```

## Safety

- `POST /api/salary-payments/preview` is the only POST allowed here and it is a preview endpoint.
- `POST /api/salary-payments` is intentionally not called.
- `POST /api/attendance/salary-payments/generate` is intentionally not called.
- Generated salary payment voucher numbers and accounting postings remain outside this readiness stage.

## Expected Preview Fields

- Gross salary
- Base deductions
- Salary advance
- Previous due
- Outstanding amount
- Rounded paid amount
- Round-off
- Already paid amount
